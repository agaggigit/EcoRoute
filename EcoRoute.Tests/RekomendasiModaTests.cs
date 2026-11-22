using EcoRoute.Models;

namespace EcoRoute.Tests;

// Rekomendasi moda: emisi terendah di antara moda yang jarak & waktunya masih wajar
public class RekomendasiModaTests
{
    private static List<KandidatModa> Rute(double km, int mobil, int motor, int bus, int sepeda, int jalan) =>
    [
        new("Mobil", 0.192 * km, mobil, km),
        new("Motor", 0.103 * km, motor, km),
        new("Bus", 0.089 * km, bus, km),
        new("Sepeda", 0, sepeda, km),
        new("Jalan Kaki", 0, jalan, km),
    ];

    [Fact]
    public void RuteDekat_ModaTanpaEmisiYangLebihCepat()
    {
        // Sepeda dan jalan kaki sama-sama tanpa emisi -> yang lebih cepat
        Assert.Equal("Sepeda", RekomendasiModa.Pilih(Rute(1.4, 4, 4, 15, 6, 17)));
    }

    [Fact]
    public void JalanKakiLebihDari2Km_TidakDirekomendasikan()
    {
        List<KandidatModa> rute = [new("Motor", 0.3, 6, 2.5), new("Jalan Kaki", 0, 30, 2.5)];
        Assert.Equal("Motor", RekomendasiModa.Pilih(rute));
    }

    [Fact]
    public void RuteSedang_SepedaKalauWaktunyaMasihWajar()
    {
        // 5,7 km: mobil 7 menit -> batas 7 + 15 = 22 menit, sepeda 20 menit masih masuk
        Assert.Equal("Sepeda", RekomendasiModa.Pilih(Rute(5.7, 7, 7, 19, 20, 68)));
    }

    [Fact]
    public void SepedaTerlaluLama_BusDirekomendasikan()
    {
        Assert.Equal("Bus", RekomendasiModa.Pilih(Rute(5.7, 7, 7, 19, 30, 68)));
    }

    [Fact]
    public void RuteJauh_SepedaDanJalanKakiTidakWajar()
    {
        // 16,7 km: sepeda > 8 km, jalan kaki > 2 km. Bus 50 menit masih <= 2x motor (28 menit)
        Assert.Equal("Bus", RekomendasiModa.Pilih(Rute(16.7, 30, 28, 50, 60, 200)));
    }

    [Fact]
    public void BusTerlaluLama_MotorDirekomendasikan()
    {
        Assert.Equal("Motor", RekomendasiModa.Pilih(Rute(16.7, 30, 28, 70, 60, 200)));
    }

    [Fact]
    public void TanpaKandidat_Null()
    {
        Assert.Null(RekomendasiModa.Pilih([]));
    }

    [Fact]
    public void TeksWaktu_JamDanMenit()
    {
        Assert.Equal("45 mnt", PerbandinganEmisi.TeksWaktu(45));
        Assert.Equal("1 j", PerbandinganEmisi.TeksWaktu(60));
        Assert.Equal("1 j 20 mnt", PerbandinganEmisi.TeksWaktu(80));
    }
}
