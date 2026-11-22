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
// JalanUtama = FRC0-2 (jalan tol, arteri, kolektor besar): di sini TomTom punya data live yang berubah-ubah.
public record DataFlow(double Rasio, bool AdaDataLive, bool JalanUtama = false);

public class TrafficService
{
    private const string FlowUrl = "https://api.tomtom.com/traffic/services/4/flowSegmentData/absolute/10/json";

    // Tahap 1: kira-kira 1 segmen per km, minimal 3 dan maksimal 10.
    private const int MinSegmen = 3;
    private const int MaxSegmen = 10;

    // Tahap 2: segmen jalan utama minimal 750 m dipecah tiga (±250-570 m per titik).
    // Maksimal 20 request per hitung rute -> kuota free tier Flow Segment Data (20.000 request/bulan,
    // lihat docs.tomtom.com/pricing) cukup untuk ±1.000 kali hitung rute per bulan (lebih kalau rutenya
    // pendek atau lewat jalan kecil, karena hanya jalan utama yang dipecah).
    public const int MaksRequestPerRute = 20;
    public const double MinPanjangDipecahKm = 0.75;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private readonly string? _apiKey;

    // Pengambil data per titik: request ke TomTom, atau pengganti di unit test. null = tidak ada API key.
    private readonly Func<double, double, Task<DataFlow?>>? _ambilDataFlow;

    public TrafficService() : this(BacaApiKey()) { }

    public TrafficService(string? apiKey)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _ambilDataFlow = _apiKey is null ? null : AmbilDataFlowAsync;
    }

    // Untuk unit test: data per titik (lat, lng) diberikan tanpa jaringan
    public TrafficService(Func<double, double, Task<DataFlow?>> ambilDataFlow)
    {
        _ambilDataFlow = ambilDataFlow;
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
        if (_ambilDataFlow is null)
            return null;

        IReadOnlyList<double[]> titik = rute.Titik;
        IReadOnlyList<PotonganRute> kasar = BagiSegmen(titik, rute.JarakKm);
        if (kasar.Count == 0)
            return null;

        // Tahap 1: satu titik per segmen (±1 km)
        DataFlow?[] dataKasar = await Task.WhenAll(kasar.Select(p => AmbilDiTitikAsync(titik, p.IndeksTengah)));
        if (dataKasar.All(d => d is null))
            return null;

        // Tahap 2: segmen jalan utama dipecah tiga supaya kemacetan pendek tidak terlewat
        (List<PotonganRute> potongan, List<DataFlow?> data) =
            await PerhalusJalanUtamaAsync(titik, rute.JarakKm, kasar, dataKasar);

        List<DataFlow> berhasil = data.OfType<DataFlow>().ToList();

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

    // Segmen jalan utama (FRC0-2) yang ber-data live dipecah tiga. Bagian tengahnya memakai ulang titik
    // tahap 1, jadi tiap segmen hanya butuh 2 request tambahan. Segmen paling lambat didahulukan supaya
    // kemacetan mendapat detail paling banyak, dan total request per rute tidak lebih dari MaksRequestPerRute.
    // Jalan kecil tidak dipecah karena TomTom umumnya tidak punya data live di sana (lihat BacaDataFlow).
    private async Task<(List<PotonganRute> Potongan, List<DataFlow?> Data)> PerhalusJalanUtamaAsync(
        IReadOnlyList<double[]> titik, double jarakOsrmKm,
        IReadOnlyList<PotonganRute> kasar, DataFlow?[] dataKasar)
    {
        double[] kumulatif = HitungKumulatif(titik);
        double skala = jarakOsrmKm > 0 && kumulatif[^1] > 0 ? jarakOsrmKm / kumulatif[^1] : 1.0;

        // Rencana pemecahan: indeks segmen kasar -> tiga bagian
        var rencana = new Dictionary<int, IReadOnlyList<PotonganRute>>();
        int sisa = MaksRequestPerRute - kasar.Count;
        foreach (int i in Enumerable.Range(0, kasar.Count)
                     .Where(i => dataKasar[i] is { AdaDataLive: true, JalanUtama: true } &&
                                 kasar[i].JarakKm >= MinPanjangDipecahKm)
                     .OrderBy(i => dataKasar[i]!.Rasio))
        {
            IReadOnlyList<PotonganRute> bagian = BagiRentang(kumulatif, kasar[i].IndeksAwal, kasar[i].IndeksAkhir, 3, skala);
            if (bagian.Count != 3)
                continue; // titik rute terlalu jarang untuk dipecah

            // Bagian tengah memakai titik tahap 1 kalau titik itu ada di dalamnya
            PotonganRute tengah = bagian[1];
            bool pakaiUlang = kasar[i].IndeksTengah >= tengah.IndeksAwal && kasar[i].IndeksTengah <= tengah.IndeksAkhir;
            int biaya = pakaiUlang ? 2 : 3;
            if (biaya > sisa)
                continue;

            sisa -= biaya;
            rencana[i] = pakaiUlang ? [bagian[0], tengah with { IndeksTengah = kasar[i].IndeksTengah }, bagian[2]] : bagian;
        }

        // Ambil data titik-titik baru sekaligus
        var permintaan = rencana
            .SelectMany(r => r.Value.Select((b, k) => (Kasar: r.Key, Ke: k, Bagian: b)))
            .Where(x => !(x.Ke == 1 && x.Bagian.IndeksTengah == kasar[x.Kasar].IndeksTengah))
            .ToList();
        DataFlow?[] dataBaru = await Task.WhenAll(permintaan.Select(x => AmbilDiTitikAsync(titik, x.Bagian.IndeksTengah)));
        var hasilBaru = permintaan
            .Select((x, n) => (x.Kasar, x.Ke, Data: dataBaru[n]))
            .ToDictionary(x => (x.Kasar, x.Ke), x => x.Data);

        // Susun ulang sesuai urutan rute
        var potongan = new List<PotonganRute>();
        var data = new List<DataFlow?>();
        for (int i = 0; i < kasar.Count; i++)
        {
            if (!rencana.TryGetValue(i, out IReadOnlyList<PotonganRute>? bagian))
            {
                potongan.Add(kasar[i]);
                data.Add(dataKasar[i]);
                continue;
            }

            for (int k = 0; k < bagian.Count; k++)
            {
                potongan.Add(bagian[k]);
                // Bagian yang request-nya gagal memakai hasil titik tahap 1 di dekatnya (jalan yang sama)
                data.Add(hasilBaru.TryGetValue((i, k), out DataFlow? d) ? d ?? dataKasar[i] : dataKasar[i]);
            }
        }

        return (potongan, data);
    }

    // Hanya dipanggil dari AmbilKondisiAsync, yang sudah memastikan _ambilDataFlow tidak null
    private Task<DataFlow?> AmbilDiTitikAsync(IReadOnlyList<double[]> titik, int indeks) =>
        _ambilDataFlow!(titik[indeks][0], titik[indeks][1]);

    // Pecah garis rute jadi segmen sama panjang. Panjang tiap segmen disesuaikan
    // supaya totalnya sama dengan jarak dari OSRM.
    public static IReadOnlyList<PotonganRute> BagiSegmen(IReadOnlyList<double[]> titik, double jarakOsrmKm)
    {
        if (titik.Count < 2)
            return [];

        double[] kumulatif = HitungKumulatif(titik);
        double total = kumulatif[^1];
        if (total <= 0)
            return [];

        int jumlah = Math.Clamp((int)Math.Round(total), MinSegmen, MaxSegmen);
        double skala = jarakOsrmKm > 0 ? jarakOsrmKm / total : 1.0;
        return BagiRentang(kumulatif, 0, titik.Count - 1, jumlah, skala);
    }

    // Pecah rentang titik [awal, akhir] jadi `jumlah` bagian sama panjang (bisa kurang kalau titiknya jarang)
    public static IReadOnlyList<PotonganRute> BagiRentang(double[] kumulatif, int awal, int akhir, int jumlah, double skala)
    {
        var hasil = new List<PotonganRute>();
        double mulai = kumulatif[awal];
        double panjang = kumulatif[akhir] - mulai;

        int dari = awal;
        for (int k = 1; k <= jumlah; k++)
        {
            int sampai = k == jumlah ? akhir : Math.Min(IndeksPadaJarak(kumulatif, mulai + panjang * k / jumlah), akhir);
            if (sampai <= dari)
                continue; // titik terlalu jarang

            int tengah = Math.Clamp(IndeksPadaJarak(kumulatif, (kumulatif[dari] + kumulatif[sampai]) / 2), dari, sampai);
            hasil.Add(new PotonganRute(dari, sampai, tengah, (kumulatif[sampai] - kumulatif[dari]) * skala));
            dari = sampai;
        }

        return hasil;
    }

    // Jarak kumulatif (km) dari titik pertama sampai tiap titik
    public static double[] HitungKumulatif(IReadOnlyList<double[]> titik)
    {
        double[] kumulatif = new double[titik.Count];
        for (int i = 1; i < titik.Count; i++)
            kumulatif[i] = kumulatif[i - 1] + JarakHaversineKm(titik[i - 1], titik[i]);
        return kumulatif;
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

        string? kelas = data.TryGetProperty("frc", out JsonElement frc) ? frc.GetString() : null;
        bool jalanUtama = kelas is "FRC0" or "FRC1" or "FRC2";
        bool jalanKecil = kelas is "FRC3" or "FRC4" or "FRC5" or "FRC6";

        if (data.TryGetProperty("roadClosure", out JsonElement tutup) && tutup.ValueKind == JsonValueKind.True)
            return new DataFlow(0, AdaDataLive: true, jalanUtama);

        if (!data.TryGetProperty("currentSpeed", out JsonElement saatIni) ||
            !data.TryGetProperty("freeFlowSpeed", out JsonElement normal))
            return null;

        double kecepatanNormal = normal.GetDouble();
        if (kecepatanNormal <= 0)
            return null;

        double kecepatanSaatIni = saatIni.GetDouble();
        bool persisNormal = kecepatanSaatIni == kecepatanNormal;
        bool yakinPenuh = !data.TryGetProperty("confidence", out JsonElement c) || c.GetDouble() >= 1.0;

        return new DataFlow(
            Math.Clamp(kecepatanSaatIni / kecepatanNormal, 0, 1),
            AdaDataLive: !(persisNormal && yakinPenuh && jalanKecil),
            jalanUtama);
    }
}
