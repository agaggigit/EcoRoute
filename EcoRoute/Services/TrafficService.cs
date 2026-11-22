// Kondisi lalu lintas langsung dari TomTom Traffic Flow API (free tier, butuh API key).
// Cara kerja: rute OSRM dipecah jadi beberapa segmen sama panjang. Untuk titik tengah tiap segmen,
// TomTom memberi kecepatan saat ini dan kecepatan normal (tanpa macet). Rasio keduanya menentukan
// tingkat kemacetan segmen itu, sehingga emisi bisa dihitung per segmen sesuai kondisi aslinya.
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using EcoRoute.Models;

namespace EcoRoute.Services;

// Potongan rute: indeks awal/akhir/tengah di RouteResult.Titik dan panjangnya.
public record PotonganRute(int IndeksAwal, int IndeksAkhir, int IndeksTengah, double JarakKm);

public class TrafficService
{
    private const string FlowUrl = "https://api.tomtom.com/traffic/services/4/flowSegmentData/absolute/10/json";

    // Kira-kira 1 segmen per km, minimal 3 dan maksimal 10.
    // 10 request per hitung rute -> kuota free tier (2.500 request/hari) cukup untuk ±250 kali hitung.
    private const int MinSegmen = 3;
    private const int MaxSegmen = 10;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private readonly string? _apiKey;

    public TrafficService() : this(BacaApiKey()) { }

    public TrafficService(string? apiKey)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
    }

    public bool AdaApiKey => _apiKey is not null;

    // Urutan: environment variable TOMTOM_API_KEY (saat development),
    // lalu key yang ditanam saat build (dotnet publish -p:TomTomApiKey=...).
    private static string? BacaApiKey()
    {
        string? dariEnv = Environment.GetEnvironmentVariable("TOMTOM_API_KEY");
        if (!string.IsNullOrWhiteSpace(dariEnv))
            return dariEnv;

        return typeof(TrafficService).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "TomTomApiKey")?.Value;
    }

    // null = tidak ada API key atau semua request gagal (pemanggil memakai cadangan).
    public async Task<KondisiLaluLintas?> AmbilKondisiAsync(RouteResult rute)
    {
        if (_apiKey is null)
            return null;

        IReadOnlyList<PotonganRute> potongan = BagiSegmen(rute.Titik, rute.JarakKm);
        if (potongan.Count == 0)
            return null;

        double?[] rasio = await Task.WhenAll(potongan.Select(p =>
            AmbilRasioAsync(rute.Titik[p.IndeksTengah][0], rute.Titik[p.IndeksTengah][1])));

        List<double> berhasil = rasio.OfType<double>().ToList();
        if (berhasil.Count == 0)
            return null;

        // Segmen yang gagal (misal jalan kecil tanpa data) diisi rata-rata segmen lain
        double rataRata = berhasil.Average();
        var segmen = potongan.Select((p, i) =>
        {
            double r = rasio[i] ?? rataRata;
            return new SegmenLaluLintas(p.JarakKm, r, TingkatKemacetanInfo.DariRasioKecepatan(r), p.IndeksAwal, p.IndeksAkhir);
        }).ToList();

        return new KondisiLaluLintas(segmen, live: true);
    }

    // Pecah garis rute jadi segmen sama panjang. Panjang tiap segmen disesuaikan
    // supaya totalnya sama dengan jarak dari OSRM.
    public static IReadOnlyList<PotonganRute> BagiSegmen(IReadOnlyList<double[]> titik, double jarakOsrmKm)
    {
        if (titik.Count < 2)
            return [];

        double[] kumulatif = new double[titik.Count];
        for (int i = 1; i < titik.Count; i++)
            kumulatif[i] = kumulatif[i - 1] + JarakHaversineKm(titik[i - 1], titik[i]);

        double total = kumulatif[^1];
        if (total <= 0)
            return [];

        int jumlah = Math.Clamp((int)Math.Round(total), MinSegmen, MaxSegmen);
        double skala = jarakOsrmKm > 0 ? jarakOsrmKm / total : 1.0;

        var hasil = new List<PotonganRute>();
        int awal = 0;
        for (int k = 1; k <= jumlah; k++)
        {
            int akhir = k == jumlah ? titik.Count - 1 : IndeksPadaJarak(kumulatif, total * k / jumlah);
            if (akhir <= awal)
                continue; // rute pendek dengan sedikit titik

            int tengah = Math.Clamp(IndeksPadaJarak(kumulatif, (kumulatif[awal] + kumulatif[akhir]) / 2), awal, akhir);
            hasil.Add(new PotonganRute(awal, akhir, tengah, (kumulatif[akhir] - kumulatif[awal]) * skala));
            awal = akhir;
        }

        return hasil;
    }

    // Indeks titik pertama yang jarak kumulatifnya >= target
    private static int IndeksPadaJarak(double[] kumulatif, double target)
    {
        int i = Array.BinarySearch(kumulatif, target);
        if (i < 0) i = ~i;
        return Math.Min(i, kumulatif.Length - 1);
    }

    // Jarak garis lurus dua titik [lat, lng] di permukaan bumi
    private static double JarakHaversineKm(double[] a, double[] b)
    {
        const double RadiusBumiKm = 6371.0;
        double dLat = (b[0] - a[0]) * Math.PI / 180;
        double dLng = (b[1] - a[1]) * Math.PI / 180;
        double h = Math.Pow(Math.Sin(dLat / 2), 2) +
                   Math.Cos(a[0] * Math.PI / 180) * Math.Cos(b[0] * Math.PI / 180) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * RadiusBumiKm * Math.Asin(Math.Sqrt(h));
    }

    // Rasio kecepatan di satu titik, null kalau gagal (key salah, kuota habis, tidak ada data, dll.)
    private async Task<double?> AmbilRasioAsync(double lat, double lng)
    {
        string url = string.Create(CultureInfo.InvariantCulture,
            $"{FlowUrl}?point={lat},{lng}&unit=KMPH&key={Uri.EscapeDataString(_apiKey!)}");

        try
        {
            using HttpResponseMessage response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            await using Stream stream = await response.Content.ReadAsStreamAsync();
            using JsonDocument json = await JsonDocument.ParseAsync(stream);
            return HitungRasio(json.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    // Baca respons flowSegmentData. Jalan ditutup dihitung sebagai macet total.
    public static double? HitungRasio(JsonElement root)
    {
        if (!root.TryGetProperty("flowSegmentData", out JsonElement data))
            return null;

        if (data.TryGetProperty("roadClosure", out JsonElement tutup) && tutup.ValueKind == JsonValueKind.True)
            return 0;

        if (!data.TryGetProperty("currentSpeed", out JsonElement saatIni) ||
            !data.TryGetProperty("freeFlowSpeed", out JsonElement normal))
            return null;

        double kecepatanNormal = normal.GetDouble();
        if (kecepatanNormal <= 0)
            return null;

        return Math.Clamp(saatIni.GetDouble() / kecepatanNormal, 0, 1);
    }
}
