using System.Text.Json;
using EcoRoute.Models;
using EcoRoute.Services;

namespace EcoRoute.Tests;

public class TrafficServiceTests
{
    [Theory]
    [InlineData(1.00, TingkatKemacetan.Lancar)]
    [InlineData(0.80, TingkatKemacetan.Lancar)]
    [InlineData(0.70, TingkatKemacetan.Sedang)]
    [InlineData(0.50, TingkatKemacetan.Padat)]
    [InlineData(0.39, TingkatKemacetan.Macet)]
    [InlineData(0.00, TingkatKemacetan.Macet)]
    public void DariRasioKecepatan_BatasTiapTingkat(double rasio, TingkatKemacetan diharapkan)
    {
        Assert.Equal(diharapkan, TingkatKemacetanInfo.DariRasioKecepatan(rasio));
    }

    [Fact]
    public void HitungRasio_KecepatanSaatIniDibagiKecepatanNormal()
    {
        using var json = JsonDocument.Parse(
            """{"flowSegmentData":{"currentSpeed":20,"freeFlowSpeed":40,"roadClosure":false}}""");

        Assert.Equal(0.5, TrafficService.HitungRasio(json.RootElement));
    }

    [Fact]
    public void HitungRasio_JalanDitutupDianggapMacetTotal()
    {
        using var json = JsonDocument.Parse(
            """{"flowSegmentData":{"currentSpeed":40,"freeFlowSpeed":40,"roadClosure":true}}""");

        Assert.Equal(0, TrafficService.HitungRasio(json.RootElement));
    }

    [Fact]
    public void HitungRasio_ResponsTidakLengkapMenghasilkanNull()
    {
        using var json = JsonDocument.Parse("""{"error":"Invalid key"}""");

        Assert.Null(TrafficService.HitungRasio(json.RootElement));
    }

    // Garis lurus ke utara, titik tiap ±0.111 km (0.001 derajat lintang)
    private static List<double[]> GarisLurus(int jumlahTitik) =>
        Enumerable.Range(0, jumlahTitik).Select(i => new double[] { -7.8 + i * 0.001, 110.36 }).ToList();

    [Fact]
    public void BagiSegmen_KiraKiraSatuSegmenPerKm()
    {
        var titik = GarisLurus(55); // ±6 km

        var potongan = TrafficService.BagiSegmen(titik, jarakOsrmKm: 6);

        Assert.Equal(6, potongan.Count);
        Assert.Equal(0, potongan[0].IndeksAwal);
        Assert.Equal(titik.Count - 1, potongan[^1].IndeksAkhir);
    }

    [Fact]
    public void BagiSegmen_SegmenNyambungDanTotalJarakSamaDenganOsrm()
    {
        var potongan = TrafficService.BagiSegmen(GarisLurus(55), jarakOsrmKm: 6.5);

        for (int i = 1; i < potongan.Count; i++)
            Assert.Equal(potongan[i - 1].IndeksAkhir, potongan[i].IndeksAwal);
        Assert.All(potongan, p => Assert.InRange(p.IndeksTengah, p.IndeksAwal, p.IndeksAkhir));
        Assert.Equal(6.5, potongan.Sum(p => p.JarakKm), precision: 6);
    }

    [Theory]
    [InlineData(10, 3)]   // ±1 km -> minimal 3 segmen
    [InlineData(400, 10)] // ±44 km -> maksimal 10 segmen
    public void BagiSegmen_JumlahSegmenDibatasi(int jumlahTitik, int jumlahSegmen)
    {
        Assert.Equal(jumlahSegmen, TrafficService.BagiSegmen(GarisLurus(jumlahTitik), 0).Count);
    }

    [Fact]
    public void BagiSegmen_RuteDenganDuaTitikTetapSatuSegmen()
    {
        var potongan = TrafficService.BagiSegmen(GarisLurus(2), 0.1);

        Assert.Single(potongan);
    }

    [Fact]
    public async Task AmbilKondisi_TanpaApiKeyMengembalikanNull()
    {
        var service = new TrafficService(apiKey: null);
        var rute = new RouteResult(
            new Place("A", "A", "Lokasi", -7.8, 110.36),
            new Place("B", "B", "Lokasi", -7.75, 110.36),
            JarakKm: 6, DurasiMenit: 12, Titik: GarisLurus(55));

        Assert.False(service.AdaApiKey);
        Assert.Null(await service.AmbilKondisiAsync(rute));
    }
}
