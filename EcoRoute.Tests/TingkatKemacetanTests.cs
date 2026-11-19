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
            JarakKm: 10, DurasiMobilMenit: 20, Titik: []);

        Assert.Equal(40, RoutingService.EstimasiWaktuMenit("Mobil", rute, TingkatKemacetan.Macet));
        Assert.Equal(13, RoutingService.EstimasiWaktuMenit("Kereta", rute, TingkatKemacetan.Macet)); // tidak terpengaruh
    }
}
