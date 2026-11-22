// Kondisi lalu lintas di sepanjang rute, dipecah per segmen.
// Data live: tiap segmen punya kecepatan asli dari TomTom (lihat TrafficService).
// Cadangan: satu segmen untuk seluruh rute dengan tingkat yang ditebak dari jam.
namespace EcoRoute.Models;

// RasioKecepatan = kecepatan saat ini / kecepatan normal (0..1).
// IndeksAwal/IndeksAkhir = posisi segmen di RouteResult.Titik (untuk mewarnai rute di peta).
public record SegmenLaluLintas(
    double JarakKm,
    double RasioKecepatan,
    TingkatKemacetan Tingkat,
    int IndeksAwal,
    int IndeksAkhir)
{
    // Kecepatan tinggal setengah -> waktu tempuh 2x.
    // Dibatasi x4 supaya satu titik yang macet total tidak membuat estimasi tidak masuk akal.
    public double PengaliWaktu => 1.0 / Math.Max(RasioKecepatan, 0.25);
}

public class KondisiLaluLintas
{
    public IReadOnlyList<SegmenLaluLintas> Segmen { get; }

    // true = data asli TomTom, false = tebakan dari jam
    public bool Live { get; }

    public KondisiLaluLintas(IReadOnlyList<SegmenLaluLintas> segmen, bool live)
    {
        if (segmen.Count == 0)
            throw new ArgumentException("Minimal harus ada satu segmen.", nameof(segmen));
        Segmen = segmen;
        Live = live;
    }

    public double JarakTotalKm => Segmen.Sum(s => s.JarakKm);

    // Rata-rata rasio kecepatan, ditimbang dengan panjang segmen
    public double RasioRataRata => JarakTotalKm > 0
        ? Segmen.Sum(s => s.RasioKecepatan * s.JarakKm) / JarakTotalKm
        : Segmen.Average(s => s.RasioKecepatan);

    public TingkatKemacetan TingkatRataRata => Live
        ? TingkatKemacetanInfo.DariRasioKecepatan(RasioRataRata)
        : Segmen[0].Tingkat;

    // Total panjang segmen yang padat atau macet
    public double JarakTersendatKm => Segmen
        .Where(s => s.Tingkat is TingkatKemacetan.Padat or TingkatKemacetan.Macet)
        .Sum(s => s.JarakKm);

    // Emisi dihitung per segmen sesuai kondisi masing-masing, lalu dijumlahkan
    public double HitungEmisi(ModaTransportasi moda) =>
        Segmen.Sum(s => moda.HitungEmisi(s.JarakKm, s.Tingkat));

    // Pengali waktu tempuh seluruh rute (rata-rata pengali tiap segmen, ditimbang dengan jarak)
    public double PengaliWaktu => JarakTotalKm > 0
        ? Segmen.Sum(s => s.PengaliWaktu * s.JarakKm) / JarakTotalKm
        : 1.0;

    // Cadangan kalau TomTom tidak bisa dipakai
    public static KondisiLaluLintas Tebakan(double jarakKm, int jumlahTitik, DateTime waktu)
    {
        TingkatKemacetan tingkat = TingkatKemacetanInfo.Default(waktu);
        var segmen = new SegmenLaluLintas(jarakKm, 1.0 / tingkat.PengaliWaktu(), tingkat, 0, Math.Max(0, jumlahTitik - 1));
        return new KondisiLaluLintas([segmen], live: false);
    }
}
