using EcoRoute.Models;

namespace EcoRoute.Tests;

public class EcoScoreCalculatorTests
{
    // Faktor emisi sama dengan nilai cadangan di MainViewModel / seed.sql
    private static readonly IReadOnlyList<ModaTransportasi> DaftarModa =
    [
        new KendaraanBermotor("Mobil",  "", 0.1920, 1.35),
        new KendaraanBermotor("Motor",  "", 0.1030, 1.15),
        new KendaraanBermotor("Bus",    "", 0.0890, 1.25),
        new KendaraanBermotor("Kereta", "", 0.0410),
        new TransportasiAktif("Sepeda", ""),
        new TransportasiAktif("Jalan Kaki", ""),
    ];

    private static readonly EcoScoreCalculator Calculator = new(DaftarModa);

    private static TripHistory Trip(string moda, double jarakKm, double emisiTersimpan = 0) =>
        new() { ModaDipilih = moda, JarakKm = jarakKm, TotalEmisiKg = emisiTersimpan };

    [Fact]
    public void RiwayatKosong_SemuaNolDanLevelBibit()
    {
        EcoScoreRingkasan hasil = Calculator.Hitung([]);

        Assert.Equal(0, hasil.TotalPerjalanan);
        Assert.Equal(0, hasil.EmisiDihematKg);
        Assert.Equal(0, hasil.Poin);
        Assert.Equal(LevelEco.Bibit, hasil.Level);
        Assert.Equal(0, hasil.SetaraPohon);
    }

    [Fact]
    public void NaikMobil_TidakAdaPenghematan()
    {
        Assert.Equal(0, Calculator.HitungPenghematan(Trip("Mobil", 10)), precision: 6);
    }

    [Fact]
    public void NaikMobilSaatMacet_PenghematanTidakNegatif()
    {
        // Emisi tersimpan sudah termasuk penalti macet (10 x 0.192 x 1.35), tapi tidak dipakai di rumus
        Assert.Equal(0, Calculator.HitungPenghematan(Trip("Mobil", 10, emisiTersimpan: 2.592)), precision: 6);
    }

    [Fact]
    public void Bersepeda_HematSemuaEmisiMobil()
    {
        Assert.Equal(1.92, Calculator.HitungPenghematan(Trip("Sepeda", 10)), precision: 6);
    }

    [Fact]
    public void NaikBus_HematSelisihFaktorEmisi()
    {
        Assert.Equal(10 * (0.192 - 0.089), Calculator.HitungPenghematan(Trip("Bus", 10)), precision: 6);
    }

    [Fact]
    public void NamaModaTidakPekaHurufBesarKecil()
    {
        Assert.Equal(1.92, Calculator.HitungPenghematan(Trip("sepeda", 10)), precision: 6);
    }

    [Fact]
    public void ModaTidakDikenal_DianggapNol()
    {
        Assert.Equal(0, Calculator.HitungPenghematan(Trip("Pesawat", 10)), precision: 6);
    }

    [Fact]
    public void TanpaModaMobil_PenghematanNol()
    {
        var calculator = new EcoScoreCalculator([new TransportasiAktif("Sepeda", "")]);

        Assert.Equal(0, calculator.HitungPenghematan(Trip("Sepeda", 10)), precision: 6);
    }

    [Fact]
    public void Hitung_MenjumlahkanRingkasanRiwayat()
    {
        TripHistory[] riwayat =
        [
            Trip("Sepeda", 10),
            Trip("Bus", 10, emisiTersimpan: 0.89),
            Trip("Mobil", 5, emisiTersimpan: 0.96),
        ];

        EcoScoreRingkasan hasil = Calculator.Hitung(riwayat);

        Assert.Equal(3, hasil.TotalPerjalanan);
        Assert.Equal(25, hasil.TotalJarakKm, precision: 6);
        Assert.Equal(1.85, hasil.TotalEmisiKg, precision: 6);
        Assert.Equal(1, hasil.PerjalananTanpaEmisi);
        Assert.Equal(1.92 + 1.03, hasil.EmisiDihematKg, precision: 6);
        Assert.Equal(30, hasil.Poin); // 2.95 kg x 10, dibulatkan
    }

    [Fact]
    public void SetaraPohon_EmisiDihematDibagiSerapanPohon()
    {
        // 105 km bersepeda = 20.16 kg CO2 dihemat
        EcoScoreRingkasan hasil = Calculator.Hitung([Trip("Sepeda", 105)]);

        Assert.Equal(20.16 / EcoScoreCalculator.KgCo2PerPohonPerTahun, hasil.SetaraPohon, precision: 6);
    }

    [Theory]
    [InlineData(0, LevelEco.Bibit)]
    [InlineData(99, LevelEco.Bibit)]
    [InlineData(100, LevelEco.Tunas)]
    [InlineData(499, LevelEco.Tunas)]
    [InlineData(500, LevelEco.Pohon)]
    [InlineData(1999, LevelEco.Pohon)]
    [InlineData(2000, LevelEco.Hutan)]
    public void TentukanLevel_SesuaiAmbangPoin(int poin, LevelEco harapan)
    {
        Assert.Equal(harapan, EcoScoreCalculator.TentukanLevel(poin));
    }

    [Fact]
    public void Progres_DihitungDalamRentangLevelSaatIni()
    {
        // 15 km bersepeda = 2.88 kg = 29 poin -> Bibit, 29% menuju Tunas (100 poin)
        EcoScoreRingkasan hasil = Calculator.Hitung([Trip("Sepeda", 15)]);

        Assert.Equal(29, hasil.Poin);
        Assert.Equal(LevelEco.Bibit, hasil.Level);
        Assert.Equal(0.29, hasil.ProgresKeLevelBerikut, precision: 6);
        Assert.Equal(71, hasil.PoinMenujuLevelBerikut);
    }

    [Fact]
    public void LevelHutan_ProgresPenuhTanpaLevelBerikut()
    {
        // 1100 km bersepeda = 211.2 kg = 2112 poin
        EcoScoreRingkasan hasil = Calculator.Hitung([Trip("Sepeda", 1100)]);

        Assert.Equal(LevelEco.Hutan, hasil.Level);
        Assert.Equal(1, hasil.ProgresKeLevelBerikut);
        Assert.Null(hasil.PoinMenujuLevelBerikut);
    }
}
