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

// Hasil TomTom di satu titik: rasio kecepatan (0..1) dan apakah nilainya benar-benar data live
public record DataFlow(double Rasio, bool AdaDataLive);

public class TrafficService
{
    private const string FlowUrl = "https://api.tomtom.com/traffic/services/4/flowSegmentData/absolute/10/json";

    // Kira-kira 1 segmen per km, minimal 3 dan maksimal 10.
    // Maksimal 10 request per hitung rute -> kuota free tier Flow Segment Data (20.000 request/bulan,
    // lihat docs.tomtom.com/pricing) cukup untuk ±2.000 kali hitung rute per bulan.
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

        DataFlow?[] data = await Task.WhenAll(potongan.Select(p =>
            AmbilDataFlowAsync(rute.Titik[p.IndeksTengah][0], rute.Titik[p.IndeksTengah][1])));

        List<DataFlow> berhasil = data.OfType<DataFlow>().ToList();
        if (berhasil.Count == 0)
            return null;

        // Segmen yang gagal diisi rata-rata segmen lain yang punya data live (kalau ada),
        // dan ditandai tanpa data live supaya di peta tidak tampak seperti hasil pengukuran
        List<DataFlow> live = berhasil.Where(d => d.AdaDataLive).ToList();
        double rataRata = (live.Count > 0 ? live : berhasil).Average(d => d.Rasio);
        var segmen = potongan.Select((p, i) =>
        {
            double r = data[i]?.Rasio ?? rataRata;
            bool adaDataLive = data[i]?.AdaDataLive ?? false;
            return new SegmenLaluLintas(p.JarakKm, r, TingkatKemacetanInfo.DariRasioKecepatan(r),
                p.IndeksAwal, p.IndeksAkhir, adaDataLive);
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

    // Data flow di satu titik, null kalau gagal (key salah, kuota habis, tidak ada data, dll.)
    private async Task<DataFlow?> AmbilDataFlowAsync(double lat, double lng)
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
            return BacaDataFlow(json.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    // Rasio kecepatan saja (dipakai test dan pemanggil yang tidak butuh status data live)
    public static double? HitungRasio(JsonElement root) => BacaDataFlow(root)?.Rasio;

    // Baca respons flowSegmentData. Jalan ditutup dihitung sebagai macet total.
    //
    // Tanpa data live: di jalan kecil (FRC3 ke bawah) TomTom sering mengembalikan kecepatan saat ini
    // persis sama dengan kecepatan normal dan confidence 1. Diagnosis di Yogyakarta (Malioboro, jalan
    // dalam kota) selalu memberi hasil begitu, sedangkan jalan utama (FRC0-2) memberi nilai yang
    // berubah-ubah. Ruas seperti itu tetap dihitung lancar, tapi ditandai tanpa data live.
    public static DataFlow? BacaDataFlow(JsonElement root)
    {
        if (!root.TryGetProperty("flowSegmentData", out JsonElement data))
            return null;

        if (data.TryGetProperty("roadClosure", out JsonElement tutup) && tutup.ValueKind == JsonValueKind.True)
            return new DataFlow(0, AdaDataLive: true);

        if (!data.TryGetProperty("currentSpeed", out JsonElement saatIni) ||
            !data.TryGetProperty("freeFlowSpeed", out JsonElement normal))
            return null;

        double kecepatanNormal = normal.GetDouble();
        if (kecepatanNormal <= 0)
            return null;

        double kecepatanSaatIni = saatIni.GetDouble();
        bool persisNormal = kecepatanSaatIni == kecepatanNormal;
        bool yakinPenuh = !data.TryGetProperty("confidence", out JsonElement c) || c.GetDouble() >= 1.0;
        bool jalanKecil = data.TryGetProperty("frc", out JsonElement frc) &&
                          frc.GetString() is "FRC3" or "FRC4" or "FRC5" or "FRC6";

        return new DataFlow(
            Math.Clamp(kecepatanSaatIni / kecepatanNormal, 0, 1),
            AdaDataLive: !(persisNormal && yakinPenuh && jalanKecil));
    }
}
