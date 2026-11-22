// Kondisi lalu lintas di sepanjang rute, dipecah per segmen.
// Data live: tiap segmen punya kecepatan asli dari TomTom (lihat TrafficService).
// Cadangan: satu segmen untuk seluruh rute dengan tingkat yang ditebak dari jam.
namespace EcoRoute.Models;

// RasioKecepatan = kecepatan saat ini / kecepatan normal (0..1).
// IndeksAwal/IndeksAkhir = posisi segmen di RouteResult.Titik (untuk mewarnai rute di peta).
// AdaDataLive = false: TomTom tidak punya data live untuk ruas ini (atau request-nya gagal), jadi
// rasionya hanya anggapan. Emisi tetap dihitung dengan rasio itu, tapi di peta diberi warna abu-abu.
public record SegmenLaluLintas(
    double JarakKm,
    double RasioKecepatan,
    TingkatKemacetan Tingkat,
    int IndeksAwal,
    int IndeksAkhir,
    bool AdaDataLive = true)
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

    // Total panjang segmen tanpa data live TomTom
    public double JarakTanpaDataLiveKm => Segmen.Where(s => !s.AdaDataLive).Sum(s => s.JarakKm);

    // Total panjang segmen ber-data live dengan tingkat tertentu
    public double JarakPerTingkat(TingkatKemacetan tingkat) => Segmen
        .Where(s => s.AdaDataLive && s.Tingkat == tingkat)
        .Sum(s => s.JarakKm);

    // Ringkasan untuk card hasil. Menyebut ruas yang tersendat (Sedang ke atas) supaya sesuai dengan
    // warna di peta; rata-rata seluruh rute bisa tetap "Lancar" walau sebagian ruas kuning/merah.
    public string Ringkasan(IFormatProvider? format = null)
    {
        format ??= System.Globalization.CultureInfo.CurrentCulture;
        string Km(double km) => km.ToString("0.0", format) + " km";

        List<SegmenLaluLintas> terpantau = Segmen.Where(s => s.AdaDataLive).ToList();
        if (terpantau.Count == 0)
            return "Tidak ada data live untuk ruas ini, dianggap lancar.";

        double tanpaData = JarakTanpaDataLiveKm;
        string catatanTanpaData = tanpaData >= 0.05
            ? $" {Km(tanpaData)} tanpa data live, dianggap lancar."
            : string.Empty;

        var tersendat = new[] { TingkatKemacetan.Macet, TingkatKemacetan.Padat, TingkatKemacetan.Sedang }
            .Select(t => (Tingkat: t, Km: JarakPerTingkat(t)))
            .Where(x => x.Km >= 0.05)
            .ToList();

        if (tersendat.Count == 0)
        {
            double jarak = terpantau.Sum(s => s.JarakKm);
            double rasio = jarak > 0
                ? terpantau.Sum(s => s.RasioKecepatan * s.JarakKm) / jarak
                : terpantau.Average(s => s.RasioKecepatan);
            return $"Lancar: kecepatan rata-rata {rasio.ToString("P0", format)} dari kondisi normal." + catatanTanpaData;
        }

        string rincian = string.Join(", ", tersendat.Select(x => $"{x.Tingkat} {Km(x.Km)}"));
        return $"Tersendat {Km(tersendat.Sum(x => x.Km))} dari {Km(JarakTotalKm)} ({rincian}). Ruas lain lancar." +
               catatanTanpaData;
    }

    // Emisi dihitung per segmen sesuai kondisi masing-masing, lalu dijumlahkan
    public double HitungEmisi(ModaTransportasi moda) =>
        Segmen.Sum(s => moda.HitungEmisi(s.JarakKm, s.Tingkat));

    // Pengali waktu tempuh seluruh rute (rata-rata pengali tiap segmen, ditimbang dengan jarak)
    public double PengaliWaktu => JarakTotalKm > 0
        ? Segmen.Sum(s => s.PengaliWaktu * s.JarakKm) / JarakTotalKm
        : 1.0;

    // Emisi bagian naik bus pada trayek Trans Jogja (jalan kaki ke / dari halte tidak beremisi).
    // Urutan sumber kemacetan: data per segmen di jalur bus sendiri, lalu rata-rata emisi per km
    // rute jalan raya (kalau jalur bus tidak punya data), lalu tanpa kemacetan.
    public static double HitungEmisiBus(
        ModaTransportasi moda, double jarakBusKm, KondisiLaluLintas? jalurBus, KondisiLaluLintas? jalanRaya)
    {
        if (jalurBus is not null)
            return jalurBus.HitungEmisi(moda);

        return jalanRaya is { JarakTotalKm: > 0 }
            ? jalanRaya.HitungEmisi(moda) / jalanRaya.JarakTotalKm * jarakBusKm
            : moda.HitungEmisi(jarakBusKm);
    }

    // Cadangan kalau TomTom tidak bisa dipakai
    public static KondisiLaluLintas Tebakan(double jarakKm, int jumlahTitik, DateTime waktu)
    {
        TingkatKemacetan tingkat = TingkatKemacetanInfo.Default(waktu);
        var segmen = new SegmenLaluLintas(jarakKm, 1.0 / tingkat.PengaliWaktu(), tingkat, 0, Math.Max(0, jumlahTitik - 1));
        return new KondisiLaluLintas([segmen], live: false);
    }
}
