using EcoRoute.Models;
using EcoRoute.Services;

namespace EcoRoute.Tests;

public class TingkatKemacetanTests
{
    [Theory]
    [InlineData("2026-11-19 07:30", TingkatKemacetan.Padat)]  // Kamis, jam sibuk pagi
    [InlineData("2026-11-19 17:00", TingkatKemacetan.Padat)]  // Kamis, jam sibuk sore
    [InlineData("2026-11-19 12:00", TingkatKemacetan.Sedang)] // Kamis, siang
    [InlineData("2026-11-19 22:00", TingkatKemacetan.Lancar)] // Kamis, malam
    [InlineData("2026-11-21 07:30", TingkatKemacetan.Sedang)] // Sabtu, jam sibuk lebih ringan
    public void Default_DitebakDariJam(string waktu, TingkatKemacetan diharapkan)
    {
        Assert.Equal(diharapkan, TingkatKemacetanInfo.Default(DateTime.Parse(waktu)));
    }

    [Fact]
    public void EstimasiWaktu_MacetMemperlambatKendaraanJalanRaya()
    {
        var rute = new RouteResult(
            new Place("A", "A", "Lokasi", 0, 0),
            new Place("B", "B", "Lokasi", 0, 0),
            JarakKm: 10, DurasiMenit: 20, Titik: []);

        double pengaliMacet = TingkatKemacetan.Macet.PengaliWaktu();
        Assert.Equal(40, RoutingService.EstimasiWaktuMenit("Mobil", rute, pengaliMacet));
        Assert.Equal(40, RoutingService.EstimasiWaktuMenit("Sepeda", rute, pengaliMacet)); // tidak terpengaruh (10 km / 15 km/jam)
    }
}
