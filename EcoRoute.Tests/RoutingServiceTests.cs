using EcoRoute.Models;
using EcoRoute.Services;

namespace EcoRoute.Tests;

public class RoutingServiceTests
{
    private static RouteResult Rute(double jarakKm, double durasiMenit, ProfilRute profil = ProfilRute.JalanRaya) => new(
        new Place("A", "A", "Lokasi", -7.77, 110.37),
        new Place("B", "B", "Lokasi", -7.79, 110.36),
        jarakKm,
        durasiMenit,
        [],
        profil);

    [Fact]
    public void EstimasiWaktu_Mobil_PakaiDurasiLayananRouting()
    {
        Assert.Equal(22, RoutingService.EstimasiWaktuMenit("Mobil", Rute(10, 21.6)));
    }

    [Fact]
    public void EstimasiWaktu_MotorHanyaKenaSeparuhTambahanWaktuMacet()
    {
        var rute = Rute(10, 20);

        // Macet membuat mobil 2x lebih lama (tambah 100%), motor hanya tambah 50%
        Assert.Equal(40, RoutingService.EstimasiWaktuMenit("Mobil", rute, pengaliKemacetan: 2.0));
        Assert.Equal(30, RoutingService.EstimasiWaktuMenit("Motor", rute, pengaliKemacetan: 2.0));
        Assert.Equal(20, RoutingService.EstimasiWaktuMenit("Motor", rute)); // lancar: sama dengan mobil
    }

    [Fact]
    public void EstimasiWaktu_BusDikaliHalteDitambahWaktuTunggu()
    {
        // 20 menit x 1.3 (berhenti di halte) + 10 menit menunggu bus
        Assert.Equal(36, RoutingService.EstimasiWaktuMenit("Bus", Rute(10, 20)));
        // Saat macet (x1.5): 20 x 1.3 x 1.5 + 10
        Assert.Equal(49, RoutingService.EstimasiWaktuMenit("Bus", Rute(10, 20), pengaliKemacetan: 1.5));
    }

    private static RuteBus Trayek(double jalanKakiAwal, double jarakBus, double jalanKakiAkhir) => new(
        "4A", "Transjogja 4A: Giwangan → Condongcatur",
        new HalteBus("Halte Naik", -7.77, 110.37),
        new HalteBus("Halte Turun", -7.79, 110.36),
        jalanKakiAwal, jarakBus, jalanKakiAkhir, []);

    [Fact]
    public void BuatRuteBus_DurasiBusMengikutiKecepatanJalanRaya()
    {
        // Jalan raya 10 km dalam 20 menit = 0,5 km/menit -> bus 6 km = 12 menit kalau lancar
        RouteResult rute = RoutingService.BuatRuteBus(Trayek(0.5, 6, 0.3), Rute(10, 20));

        Assert.Equal(ProfilRute.TrayekBus, rute.Profil);
        Assert.Equal(12, rute.DurasiMenit, 6);
        Assert.Equal(6.8, rute.JarakKm, 6); // jalan kaki + bus
    }

    [Fact]
    public void EstimasiWaktu_BusLewatTrayek_DitambahJalanKakiKeDanDariHalte()
    {
        RouteResult rute = RoutingService.BuatRuteBus(Trayek(0.5, 6, 0.5), Rute(10, 20));

        // 12 x 1.3 + 10 menunggu + 1 km jalan kaki (12 menit) = 37,6
        Assert.Equal(38, RoutingService.EstimasiWaktuMenit("Bus", rute));
        // Macet x1.5 hanya memperlambat bagian bus: 12 x 1.3 x 1.5 + 10 + 12 = 45,4
        Assert.Equal(45, RoutingService.EstimasiWaktuMenit("Bus", rute, pengaliKemacetan: 1.5));
    }

    [Theory]
    [InlineData("Sepeda", ProfilRute.Sepeda)]
    [InlineData("Jalan Kaki", ProfilRute.JalanKaki)]
    public void EstimasiWaktu_SepedaDanJalanKaki_PakaiDurasiProfilSendiriTanpaMacet(string moda, ProfilRute profil)
    {
        Assert.Equal(45, RoutingService.EstimasiWaktuMenit(moda, Rute(3.4, 45.4, profil), pengaliKemacetan: 2.0));
    }

    [Theory]
    [InlineData("Jalan Kaki", 120)] // 10 km / 5 km/jam
    [InlineData("Sepeda", 40)]      // 10 km / 15 km/jam
    public void EstimasiWaktu_RuteCadanganJalanRaya_PakaiKecepatanRataRata(string moda, int menitDiharapkan)
    {
        Assert.Equal(menitDiharapkan, RoutingService.EstimasiWaktuMenit(moda, Rute(10, 15)));
    }

    [Fact]
    public void EstimasiWaktu_JarakSangatPendek_MinimalSatuMenit()
    {
        Assert.Equal(1, RoutingService.EstimasiWaktuMenit("Mobil", Rute(0.01, 0.01)));
    }

    [Theory]
    [InlineData("Mobil", ProfilRute.JalanRaya)]
    [InlineData("Motor", ProfilRute.JalanRaya)]
    [InlineData("Bus", ProfilRute.JalanRaya)]
    [InlineData("Sepeda", ProfilRute.Sepeda)]
    [InlineData("Jalan Kaki", ProfilRute.JalanKaki)]
    public void ProfilUntuk_TiapModaPunyaJalur(string moda, ProfilRute diharapkan)
    {
        Assert.Equal(diharapkan, RoutingService.ProfilUntuk(moda));
    }

    [Fact]
    public void ProfilUntuk_KeretaTidakDidukung()
    {
        Assert.Null(RoutingService.ProfilUntuk("Kereta"));
    }

    [Fact]
    public void RuteSemuaModa_MengembalikanRuteSesuaiProfil()
    {
        var raya = Rute(3.6, 6);
        var sepeda = Rute(3.2, 15, ProfilRute.Sepeda);
        var kaki = Rute(3.4, 46, ProfilRute.JalanKaki);
        var semua = new RuteSemuaModa(raya, sepeda, kaki);

        Assert.Same(raya, semua.Untuk(ProfilRute.JalanRaya));
        Assert.Same(sepeda, semua.Untuk(ProfilRute.Sepeda));
        Assert.Same(kaki, semua.Untuk(ProfilRute.JalanKaki));
    }
}
