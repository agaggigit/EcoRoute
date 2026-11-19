using EcoRoute.Models;

namespace EcoRoute.Tests;

public class ModaTransportasiTests
{
    [Fact]
    public void KendaraanBermotor_EmisiSamaDenganJarakKaliFaktor()
    {
        var mobil = new KendaraanBermotor("Mobil", "", 0.192);

        Assert.Equal(1.92, mobil.HitungEmisi(10), precision: 6);
    }

    [Fact]
    public void KendaraanBermotor_SaatMacetPenaltiDipakaiPenuh()
    {
        var mobil = new KendaraanBermotor("Mobil", "", 0.192, faktorPenaltiKemacetan: 1.5);

        Assert.Equal(2.88, mobil.HitungEmisi(10, TingkatKemacetan.Macet), precision: 6);
    }

    [Fact]
    public void KendaraanBermotor_SaatLancarTanpaPenalti()
    {
        var mobil = new KendaraanBermotor("Mobil", "", 0.192, faktorPenaltiKemacetan: 1.5);

        Assert.Equal(1.92, mobil.HitungEmisi(10, TingkatKemacetan.Lancar), precision: 6);
        Assert.Equal(1.92, mobil.HitungEmisi(10), precision: 6);
    }

    [Theory]
    [InlineData(TingkatKemacetan.Sedang, 2.24)] // 1.92 x (1 + 0.5 x 1/3)
    [InlineData(TingkatKemacetan.Padat, 2.56)]  // 1.92 x (1 + 0.5 x 2/3)
    public void KendaraanBermotor_PenaltiNaikBertahap(TingkatKemacetan kemacetan, double emisiDiharapkan)
    {
        var mobil = new KendaraanBermotor("Mobil", "", 0.192, faktorPenaltiKemacetan: 1.5);

        Assert.Equal(emisiDiharapkan, mobil.HitungEmisi(10, kemacetan), precision: 6);
    }

    [Fact]
    public void TransportasiAktif_TetapNolSaatMacet()
    {
        Assert.Equal(0, new TransportasiAktif("Sepeda", "").HitungEmisi(10, TingkatKemacetan.Macet));
    }

    [Fact]
    public void TransportasiAktif_TanpaEmisi()
    {
        Assert.Equal(0, new TransportasiAktif("Sepeda", "").HitungEmisi(10));
    }

    [Fact]
    public void FaktorEmisiNegatif_Ditolak()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KendaraanBermotor("Mobil", "", -0.1));
    }

    [Fact]
    public void PenaltiDiBawahSatu_Ditolak()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KendaraanBermotor("Mobil", "", 0.192, 0.5));
    }

    [Fact]
    public void JarakNegatif_DitolakDiSemuaModa()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KendaraanBermotor("Bus", "", 0.089).HitungEmisi(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransportasiAktif("Jalan Kaki", "").HitungEmisi(-1));
    }

    [Fact]
    public void Polimorfisme_SatuPanggilanUntukDaftarModaCampuran()
    {
        List<ModaTransportasi> daftar =
        [
            new KendaraanBermotor("Mobil", "", 0.192),
            new KendaraanBermotor("Kereta", "", 0.041),
            new TransportasiAktif("Sepeda", ""),
        ];

        var emisi = daftar.Select(m => m.HitungEmisi(14.5)).ToList();

        Assert.Equal(2.784, emisi[0], precision: 6);
        Assert.Equal(0.5945, emisi[1], precision: 6);
        Assert.Equal(0, emisi[2]);
    }
}
