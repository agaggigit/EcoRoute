using System.Collections.Concurrent;
using EcoRoute.Models;
using EcoRoute.Services;

namespace EcoRoute.Tests;

// Tahap 2 pengambilan data TomTom: segmen jalan utama dipecah tiga, dengan batas request per rute.
// Data per titik diberikan oleh fungsi palsu (tanpa jaringan).
public class SamplingJalanUtamaTests
{
    // Garis lurus ke utara, titik tiap ±0.111 km
    private static List<double[]> GarisLurus(int jumlahTitik) =>
        Enumerable.Range(0, jumlahTitik).Select(i => new double[] { -7.8 + i * 0.001, 110.36 }).ToList();

    private static RouteResult Rute(List<double[]> titik, double jarakKm) =>
        new(new Place("A", "A", "Lokasi", titik[0][0], titik[0][1]),
            new Place("B", "B", "Lokasi", titik[^1][0], titik[^1][1]),
            jarakKm, 10, titik, ProfilRute.JalanRaya);

    private sealed class TomTomPalsu(Func<double, DataFlow?> data)
    {
        public ConcurrentBag<double> Titik { get; } = [];

        public Task<DataFlow?> AmbilAsync(double lat, double lng)
        {
            Titik.Add(lat);
            return Task.FromResult(data(lat));
        }
    }

    private static readonly DataFlow JalanUtamaLancar = new(1.0, AdaDataLive: true, JalanUtama: true);
    private static readonly DataFlow JalanKecil = new(1.0, AdaDataLive: false, JalanUtama: false);

    private static void AssertSegmenNyambung(KondisiLaluLintas kondisi, int jumlahTitik, double jarakKm)
    {
        Assert.Equal(0, kondisi.Segmen[0].IndeksAwal);
        Assert.Equal(jumlahTitik - 1, kondisi.Segmen[^1].IndeksAkhir);
        for (int i = 1; i < kondisi.Segmen.Count; i++)
            Assert.Equal(kondisi.Segmen[i - 1].IndeksAkhir, kondisi.Segmen[i].IndeksAwal);
        Assert.Equal(jarakKm, kondisi.JarakTotalKm, precision: 6);
    }

    [Fact]
    public async Task JalanUtama_TiapSegmenDipecahTigaDenganDuaRequestTambahan()
    {
        var titik = GarisLurus(55); // ±6 km -> 6 segmen tahap 1
        var tomtom = new TomTomPalsu(_ => JalanUtamaLancar);

        KondisiLaluLintas? kondisi = await new TrafficService(tomtom.AmbilAsync).AmbilKondisiAsync(Rute(titik, 6));

        Assert.NotNull(kondisi);
        Assert.Equal(18, kondisi.Segmen.Count);   // 6 x 3
        Assert.Equal(6 + 6 * 2, tomtom.Titik.Count); // titik tengah tahap 1 dipakai ulang
        AssertSegmenNyambung(kondisi, titik.Count, 6);
    }

    [Fact]
    public async Task JalanKecil_TidakDipecah()
    {
        var titik = GarisLurus(55);
        var tomtom = new TomTomPalsu(_ => JalanKecil);

        KondisiLaluLintas? kondisi = await new TrafficService(tomtom.AmbilAsync).AmbilKondisiAsync(Rute(titik, 6));

        Assert.Equal(6, kondisi!.Segmen.Count);
        Assert.Equal(6, tomtom.Titik.Count);
    }

    [Fact]
    public async Task RuteJauh_RequestTidakMelebihiBatasDanYangPalingLambatDidahulukan()
    {
        var titik = GarisLurus(400); // ±44 km -> 10 segmen tahap 1, masing-masing ±4.4 km
        // Bagian paling utara (lintang > -7.6) sedang macet
        var tomtom = new TomTomPalsu(lat => lat > -7.6
            ? new DataFlow(0.3, AdaDataLive: true, JalanUtama: true)
            : JalanUtamaLancar);

        KondisiLaluLintas? kondisi = await new TrafficService(tomtom.AmbilAsync).AmbilKondisiAsync(Rute(titik, 44));

        Assert.True(tomtom.Titik.Count <= TrafficService.MaksRequestPerRute);
        AssertSegmenNyambung(kondisi!, titik.Count, 44);

        // Sisa kuota (20 - 10 = 10) cukup untuk 5 segmen dipecah
        Assert.Equal(10 + 5 * 2, tomtom.Titik.Count);
        Assert.Equal(10 + 5 * 2, kondisi!.Segmen.Count);

        IReadOnlyList<PotonganRute> tahap1 = TrafficService.BagiSegmen(titik, 44);
        // Segmen paling utara (macet) dipecah tiga...
        Assert.Equal(tahap1[^1].IndeksAwal, kondisi.Segmen[^3].IndeksAwal);
        Assert.All(kondisi.Segmen.TakeLast(3), s => Assert.Equal(TingkatKemacetan.Macet, s.Tingkat));
        // ...sedangkan segmen paling selatan (lancar) tidak
        Assert.Equal(tahap1[0].IndeksAkhir, kondisi.Segmen[0].IndeksAkhir);
    }

    [Fact]
    public async Task SegmenPendek_TidakDipecah()
    {
        var titik = GarisLurus(15); // ±1.6 km -> 3 segmen ±0.5 km (< 750 m)
        var tomtom = new TomTomPalsu(_ => JalanUtamaLancar);

        KondisiLaluLintas? kondisi = await new TrafficService(tomtom.AmbilAsync).AmbilKondisiAsync(Rute(titik, 1.6));

        Assert.Equal(3, kondisi!.Segmen.Count);
        Assert.Equal(3, tomtom.Titik.Count);
    }

    [Fact]
    public async Task BagianYangGagal_MemakaiHasilTitikTahapSatu()
    {
        var titik = GarisLurus(55);
        var sedang = new DataFlow(0.7, AdaDataLive: true, JalanUtama: true);
        var tahap1 = TrafficService.BagiSegmen(titik, 6).Select(p => titik[p.IndeksTengah][0]).ToHashSet();
        // Titik tahap 1 berhasil (Sedang), titik tambahan tahap 2 gagal semua
        var tomtom = new TomTomPalsu(lat => tahap1.Contains(lat) ? sedang : null);

        KondisiLaluLintas? kondisi = await new TrafficService(tomtom.AmbilAsync).AmbilKondisiAsync(Rute(titik, 6));

        Assert.Equal(18, kondisi!.Segmen.Count);
        Assert.All(kondisi.Segmen, s =>
        {
            Assert.Equal(TingkatKemacetan.Sedang, s.Tingkat);
            Assert.True(s.AdaDataLive);
        });
    }

    [Fact]
    public async Task TanpaApiKey_TidakAdaRequest()
    {
        var titik = GarisLurus(55);

        Assert.Null(await new TrafficService(apiKey: null).AmbilKondisiAsync(Rute(titik, 6)));
    }
}
