// Mencari perjalanan Trans Jogja langsung (satu trayek, tanpa pindah bus) dari data trayek
// OpenStreetMap yang disimpan di Data/trayek-transjogja.json (dibuat tools/ambil_trayek_transjogja.py).
//
// Tiap trayek disimpan sebagai jaringan ruas jalan yang dilaluinya. Jalur bus dari halte naik ke
// halte turun = jalur terpendek di dalam jaringan trayek itu (Dijkstra), jadi garisnya selalu lewat
// jalan yang memang dilalui trayek tersebut. Urutan halte dari OSM tidak dipakai karena sering berantakan.
using System.IO;
using System.Text.Json;
using EcoRoute.Models;

namespace EcoRoute.Services;

public class TransJogjaService
{
    // Batas jalan kaki ke / dari halte (garis lurus). Lebih jauh dari ini dianggap tidak ada halte dekat.
    public const double MaksJalanKakiKm = 1.0;

    // Jalan kaki sungguhan lebih panjang dari garis lurus (belokan, menyeberang)
    public const double PengaliJalanKaki = 1.3;

    // Hanya untuk memilih kombinasi halte terbaik; waktu tempuh akhir dihitung di RoutingService
    private const double KecepatanJalanKakiKmj = 5;
    private const double KecepatanBusKmj = 20;

    // Halte naik / turun yang dicoba per trayek (yang paling dekat)
    private const int MaksKandidatHalte = 5;

    private static readonly Lazy<TransJogjaService> _default = new(MuatDariFileAplikasi);

    // Data dari folder aplikasi. Kalau file tidak ada / rusak, layanan tetap jalan tanpa trayek.
    public static TransJogjaService Default => _default.Value;

    private readonly List<Trayek> _trayek;

    public string TanggalData { get; }

    public bool AdaData => _trayek.Count > 0;

    public int JumlahTrayek => _trayek.Count;

    public TransJogjaService(string json)
    {
        (_trayek, TanggalData) = Baca(json);
    }

    private static TransJogjaService MuatDariFileAplikasi()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "trayek-transjogja.json");
        try
        {
            return new TransJogjaService(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"Data trayek Trans Jogja tidak bisa dimuat: {ex.Message}");
            return new TransJogjaService("""{"simpul":[],"trayek":[]}""");
        }
    }

    // Perjalanan satu trayek tercepat. null = tidak ada halte dalam jangkauan jalan kaki di kedua ujung,
    // tidak ada trayek yang menghubungkan keduanya, atau jalan kaki langsung lebih dekat.
    public RuteBus? CariRuteLangsung(Place asal, Place tujuan)
    {
        double jalanKakiLangsungKm = JarakKm(asal.Lat, asal.Lng, tujuan.Lat, tujuan.Lng) * PengaliJalanKaki;

        RuteBus? terbaik = null;
        double menitTerbaik = double.MaxValue;

        foreach (Trayek trayek in _trayek)
        {
            List<(Halte Halte, double Km)> naik = HalteTerdekat(trayek, asal);
            List<(Halte Halte, double Km)> turun = HalteTerdekat(trayek, tujuan);
            if (naik.Count == 0 || turun.Count == 0)
                continue;

            foreach (var (halteNaik, kmAwal) in naik)
            {
                (Dictionary<int, double> jarak, Dictionary<int, int> asalSimpul) = Dijkstra(trayek, halteNaik.Simpul);

                foreach (var (halteTurun, kmAkhir) in turun)
                {
                    if (halteTurun.Simpul == halteNaik.Simpul ||
                        !jarak.TryGetValue(halteTurun.Simpul, out double kmBus))
                    {
                        continue;
                    }

                    double kmJalanKaki = kmAwal + kmAkhir;
                    if (kmJalanKaki >= jalanKakiLangsungKm)
                        continue; // jalan kaki langsung saja sudah lebih dekat

                    double menit = kmJalanKaki / KecepatanJalanKakiKmj * 60 + kmBus / KecepatanBusKmj * 60;
                    if (menit >= menitTerbaik)
                        continue;

                    menitTerbaik = menit;
                    terbaik = new RuteBus(
                        trayek.Kode,
                        trayek.Nama,
                        halteNaik.Info,
                        halteTurun.Info,
                        kmAwal,
                        kmBus,
                        kmAkhir,
                        SusunJalur(trayek, asalSimpul, halteNaik.Simpul, halteTurun.Simpul));
                }
            }
        }

        return terbaik;
    }

    private static List<(Halte Halte, double Km)> HalteTerdekat(Trayek trayek, Place titik) =>
        trayek.Halte
            .Select(h => (Halte: h, Lurus: JarakKm(titik.Lat, titik.Lng, h.Info.Lat, h.Info.Lng)))
            .Where(x => x.Lurus <= MaksJalanKakiKm)
            .OrderBy(x => x.Lurus)
            .Take(MaksKandidatHalte)
            .Select(x => (x.Halte, x.Lurus * PengaliJalanKaki))
            .ToList();

    // Jarak terpendek (km) dari satu simpul ke semua simpul trayek, plus simpul sebelumnya untuk menyusun jalur
    private static (Dictionary<int, double> Jarak, Dictionary<int, int> Asal) Dijkstra(Trayek trayek, int mulai)
    {
        var jarak = new Dictionary<int, double> { [mulai] = 0 };
        var asal = new Dictionary<int, int>();
        var antrian = new PriorityQueue<int, double>();
        antrian.Enqueue(mulai, 0);

        while (antrian.TryDequeue(out int u, out double d))
        {
            if (d > jarak[u])
                continue;

            foreach (var (v, w) in trayek.Tetangga[u])
            {
                double nd = d + w;
                if (nd < jarak.GetValueOrDefault(v, double.MaxValue))
                {
                    jarak[v] = nd;
                    asal[v] = u;
                    antrian.Enqueue(v, nd);
                }
            }
        }

        return (jarak, asal);
    }

    private static IReadOnlyList<double[]> SusunJalur(Trayek trayek, Dictionary<int, int> asal, int mulai, int akhir)
    {
        var simpul = new List<int> { akhir };
        while (simpul[^1] != mulai)
            simpul.Add(asal[simpul[^1]]);
        simpul.Reverse();

        return simpul.Select(s => trayek.Koordinat[s]).ToList();
    }

    public static double JarakKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLng = (lng2 - lng1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                   Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Asin(Math.Sqrt(a));
    }

    // ==========================================
    // MEMBACA FILE DATA
    // ==========================================

    private sealed record Halte(HalteBus Info, int Simpul);

    private sealed class Trayek
    {
        public required string Kode { get; init; }
        public required string Nama { get; init; }
        public required List<Halte> Halte { get; init; }
        public required Dictionary<int, List<(int Simpul, double Km)>> Tetangga { get; init; }
        public required double[][] Koordinat { get; init; }
    }

    private static (List<Trayek>, string) Baca(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        double[][] simpul = root.GetProperty("simpul").EnumerateArray()
            .Select(s => new[] { s[0].GetDouble(), s[1].GetDouble() })
            .ToArray();

        string tanggal = root.TryGetProperty("diambil", out JsonElement t) ? t.GetString() ?? "" : "";

        var daftar = new List<Trayek>();
        foreach (JsonElement tr in root.GetProperty("trayek").EnumerateArray())
        {
            var tetangga = new Dictionary<int, List<(int, double)>>();
            void Sambung(int a, int b, double km)
            {
                if (!tetangga.TryGetValue(a, out var list))
                    tetangga[a] = list = [];
                list.Add((b, km));
            }

            foreach (JsonElement ruas in tr.GetProperty("ruas").EnumerateArray())
            {
                int[] s = ruas.EnumerateArray().Select(x => x.GetInt32()).ToArray();
                for (int i = 0; i + 1 < s.Length; i++)
                {
                    double km = JarakKm(simpul[s[i]][0], simpul[s[i]][1], simpul[s[i + 1]][0], simpul[s[i + 1]][1]);
                    Sambung(s[i], s[i + 1], km);
                    Sambung(s[i + 1], s[i], km);
                }
            }

            var halte = tr.GetProperty("halte").EnumerateArray()
                .Select(h => new Halte(
                    new HalteBus(h.GetProperty("nama").GetString() ?? "Halte",
                                 h.GetProperty("lat").GetDouble(),
                                 h.GetProperty("lng").GetDouble()),
                    h.GetProperty("simpul").GetInt32()))
                .Where(h => tetangga.ContainsKey(h.Simpul))
                .ToList();

            if (halte.Count < 2)
                continue;

            daftar.Add(new Trayek
            {
                Kode = tr.GetProperty("kode").GetString() ?? "",
                Nama = tr.GetProperty("nama").GetString() ?? "",
                Halte = halte,
                Tetangga = tetangga,
                Koordinat = simpul
            });
        }

        return (daftar, tanggal);
    }
}
