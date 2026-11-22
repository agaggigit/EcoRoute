using EcoRoute.Models;
using EcoRoute.Services;

namespace EcoRoute.Tests;

// Kemacetan di jalur trayek Trans Jogja: emisi dan pengambilan data memakai jalur bus sendiri
public class LaluLintasBusTests
{
    private static readonly KendaraanBermotor Bus = new("Bus", "", 0.08, faktorPenaltiKemacetan: 1.3);

    private static SegmenLaluLintas Segmen(double km, double rasio, bool live = true) =>
        new(km, rasio, TingkatKemacetanInfo.DariRasioKecepatan(rasio), 0, 1, live);

    [Fact]
    public void EmisiBus_MemakaiKemacetanJalurBusSendiri()
    {
        // Jalur bus: 2 km macet + 3 km lancar. Jalan raya mobil (rute lain) lancar semua.
        var jalurBus = new KondisiLaluLintas([Segmen(2, 0.2), Segmen(3, 1.0)], live: true);
        var jalanRaya = new KondisiLaluLintas([Segmen(6, 1.0)], live: true);

        double emisi = KondisiLaluLintas.HitungEmisiBus(Bus, 5, jalurBus, jalanRaya);

        double diharapkan = Bus.HitungEmisi(2, TingkatKemacetan.Macet) + Bus.HitungEmisi(3, TingkatKemacetan.Lancar);
        Assert.Equal(diharapkan, emisi, precision: 6);
        Assert.True(emisi > Bus.HitungEmisi(5));
    }

    [Fact]
    public void EmisiBus_TanpaDataJalurBus_PakaiRataRataPerKmJalanRaya()
    {
        // Jalan raya 4 km macet total -> emisi per km macet dipakai untuk 5 km naik bus
        var jalanRaya = new KondisiLaluLintas([Segmen(4, 0.2)], live: true);

        double emisi = KondisiLaluLintas.HitungEmisiBus(Bus, 5, jalurBus: null, jalanRaya);

        Assert.Equal(Bus.HitungEmisi(5, TingkatKemacetan.Macet), emisi, precision: 6);
    }

    [Fact]
    public void EmisiBus_TanpaDataSamaSekali_TanpaPenalti()
    {
        Assert.Equal(Bus.HitungEmisi(5), KondisiLaluLintas.HitungEmisiBus(Bus, 5, null, null), precision: 6);
    }

    [Fact]
    public async Task JalurBus_SegmenTotalnyaSamaDenganJarakNaikBus()
    {
        // Seperti MainViewModel: JarakKm rute bus diganti jarak naik bus sebelum diukur
        var titik = Enumerable.Range(0, 40).Select(i => new double[] { -7.8 + i * 0.001, 110.36 }).ToList();
        var bus = new RouteResult(
            new Place("A", "A", "Lokasi", -7.81, 110.36), new Place("B", "B", "Lokasi", -7.75, 110.36),
            JarakKm: 5.6, DurasiMenit: 10, titik, ProfilRute.TrayekBus);
        var traffic = new TrafficService((_, _) => Task.FromResult<DataFlow?>(new DataFlow(0.7, true, JalanUtama: false)));

        KondisiLaluLintas? kondisi = await traffic.AmbilKondisiAsync(bus with { JarakKm = 4.3 });

        Assert.NotNull(kondisi);
        Assert.Equal(4.3, kondisi.JarakTotalKm, precision: 6);
        Assert.All(kondisi.Segmen, s => Assert.Equal(TingkatKemacetan.Sedang, s.Tingkat));
    }
}
