using EcoRoute.Models;
using EcoRoute.Services;

namespace EcoRoute.Tests;

public class RoutingServiceTests
{
    private static RouteResult Rute(double jarakKm, double durasiMobilMenit) => new(
        new Place("A", "A", "Lokasi", -7.77, 110.37),
        new Place("B", "B", "Lokasi", -7.79, 110.36),
        jarakKm,
        durasiMobilMenit,
        []);

    [Fact]
    public void EstimasiWaktu_Mobil_PakaiDurasiOsrm()
    {
        Assert.Equal(22, RoutingService.EstimasiWaktuMenit("Mobil", Rute(10, 21.6)));
    }

    [Theory]
    [InlineData("Jalan Kaki", 120)] // 10 km / 5 km/jam
    [InlineData("Sepeda", 40)]      // 10 km / 15 km/jam
    [InlineData("Bus", 30)]         // 10 km / 20 km/jam
    public void EstimasiWaktu_ModaLain_PakaiKecepatanRataRata(string moda, int menitDiharapkan)
    {
        Assert.Equal(menitDiharapkan, RoutingService.EstimasiWaktuMenit(moda, Rute(10, 15)));
    }

    [Fact]
    public void EstimasiWaktu_JarakSangatPendek_MinimalSatuMenit()
    {
        Assert.Equal(1, RoutingService.EstimasiWaktuMenit("Kereta", Rute(0.01, 0.01)));
    }
}
