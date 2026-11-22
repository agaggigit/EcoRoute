using EcoRoute.Models;

namespace EcoRoute.Tests;

public class KondisiLaluLintasTests
{
    private static readonly KendaraanBermotor Mobil = new("Mobil", "", 0.192, faktorPenaltiKemacetan: 1.5);

    private static SegmenLaluLintas Segmen(double jarakKm, double rasio) =>
        new(jarakKm, rasio, TingkatKemacetanInfo.DariRasioKecepatan(rasio), 0, 1);

    [Fact]
    public void HitungEmisi_DijumlahkanPerSegmenSesuaiKondisinya()
    {
        // 6 km lancar + 4 km macet
        var kondisi = new KondisiLaluLintas([Segmen(6, 0.9), Segmen(4, 0.2)], live: true);

        double diharapkan = Mobil.HitungEmisi(6, TingkatKemacetan.Lancar) + Mobil.HitungEmisi(4, TingkatKemacetan.Macet);
        Assert.Equal(diharapkan, kondisi.HitungEmisi(Mobil), precision: 6);
        Assert.Equal(4, kondisi.JarakTersendatKm);
    }

    [Fact]
    public void HitungEmisi_SemuaLancarSamaDenganTanpaPenalti()
    {
        var kondisi = new KondisiLaluLintas([Segmen(5, 1.0), Segmen(5, 0.85)], live: true);

        Assert.Equal(Mobil.HitungEmisi(10), kondisi.HitungEmisi(Mobil), precision: 6);
    }

    [Fact]
    public void PengaliWaktu_DitimbangDenganPanjangSegmen()
    {
        // 5 km normal (x1) + 5 km kecepatan setengah (x2) -> rata-rata x1.5
        var kondisi = new KondisiLaluLintas([Segmen(5, 1.0), Segmen(5, 0.5)], live: true);

        Assert.Equal(1.5, kondisi.PengaliWaktu, precision: 6);
        Assert.Equal(0.75, kondisi.RasioRataRata, precision: 6);
        Assert.Equal(TingkatKemacetan.Sedang, kondisi.TingkatRataRata);
    }

    [Fact]
    public void PengaliWaktu_JalanDitutupDibatasiEmpatKali()
    {
        Assert.Equal(4, Segmen(1, 0).PengaliWaktu);
    }

    [Fact]
    public void Tebakan_SatuSegmenDenganTingkatDariJam()
    {
        var kondisi = KondisiLaluLintas.Tebakan(10, 50, DateTime.Parse("2026-11-19 22:00")); // Kamis malam

        Assert.False(kondisi.Live);
        Assert.Single(kondisi.Segmen);
        Assert.Equal(TingkatKemacetan.Lancar, kondisi.TingkatRataRata);
        Assert.Equal(1.0, kondisi.PengaliWaktu, precision: 6);
    }
}
