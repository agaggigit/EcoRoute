using System.Globalization;
using System.Text.Json;
using EcoRoute.Models;
using EcoRoute.Services;

namespace EcoRoute.Tests;

// Ruas tanpa data live TomTom (abu-abu di peta) dan ringkasan kondisi lalu lintas di card hasil
public class DataLiveLaluLintasTests
{
    private static DataFlow? Baca(string flowSegmentData)
    {
        using var json = JsonDocument.Parse($$"""{"flowSegmentData":{{flowSegmentData}}}""");
        return TrafficService.BacaDataFlow(json.RootElement);
    }

    [Fact]
    public void JalanKecilPersisKecepatanNormal_TanpaDataLive()
    {
        // Hasil nyata di Malioboro: FRC3, 20/20 km/jam, confidence 1
        DataFlow? data = Baca("""{"frc":"FRC3","currentSpeed":20,"freeFlowSpeed":20,"confidence":1.0,"roadClosure":false}""");

        Assert.NotNull(data);
        Assert.Equal(1.0, data.Rasio);
        Assert.False(data.AdaDataLive);
    }

    [Fact]
    public void JalanUtamaPersisKecepatanNormal_TetapDataLive()
    {
        // Jalan utama yang memang lancar (Ring Road Utara: FRC1, 44/44)
        DataFlow? data = Baca("""{"frc":"FRC1","currentSpeed":44,"freeFlowSpeed":44,"confidence":1.0}""");

        Assert.True(data!.AdaDataLive);
    }

    [Fact]
    public void KecepatanBerbedaDariNormal_DataLive()
    {
        DataFlow? data = Baca("""{"frc":"FRC3","currentSpeed":15,"freeFlowSpeed":20,"confidence":1.0}""");

        Assert.Equal(0.75, data!.Rasio, precision: 6);
        Assert.True(data.AdaDataLive);
    }

    [Fact]
    public void ConfidenceDiBawahSatu_DataLive()
    {
        DataFlow? data = Baca("""{"frc":"FRC4","currentSpeed":30,"freeFlowSpeed":30,"confidence":0.7}""");

        Assert.True(data!.AdaDataLive);
    }

    [Fact]
    public void JalanDitutup_MacetTotalDenganDataLive()
    {
        DataFlow? data = Baca("""{"frc":"FRC3","currentSpeed":20,"freeFlowSpeed":20,"confidence":1.0,"roadClosure":true}""");

        Assert.Equal(0, data!.Rasio);
        Assert.True(data.AdaDataLive);
    }

    private static SegmenLaluLintas Segmen(double km, double rasio, bool live = true) =>
        new(km, rasio, TingkatKemacetanInfo.DariRasioKecepatan(rasio), 0, 1, live);

    private static string Ringkasan(params SegmenLaluLintas[] segmen) =>
        new KondisiLaluLintas(segmen, live: true).Ringkasan(CultureInfo.InvariantCulture);

    [Fact]
    public void Ringkasan_SemuaLancar_MenyebutRataRata()
    {
        Assert.Equal(
            "Lancar: kecepatan rata-rata 95 % dari kondisi normal.",
            Ringkasan(Segmen(2, 0.9), Segmen(2, 1.0)));
    }

    [Fact]
    public void Ringkasan_AdaRuasTersendat_MenyebutPanjangnyaWalauRataRataLancar()
    {
        // Kasus Tugu -> Prambanan: rata-rata 88% (lancar), tapi 4 ruas kuning di Jl. Solo
        string teks = Ringkasan(
            Segmen(6.8, 1.0), Segmen(1.7, 0.71), Segmen(1.7, 0.71), Segmen(1.7, 0.70), Segmen(1.7, 0.70), Segmen(3.4, 1.0));

        Assert.Equal("Tersendat 6.8 km dari 17.0 km (Sedang 6.8 km). Ruas lain lancar.", teks);
    }

    [Fact]
    public void Ringkasan_UrutanDariYangPalingParah()
    {
        string teks = Ringkasan(Segmen(1, 0.7), Segmen(2, 0.2), Segmen(3, 0.5));

        Assert.Contains("(Macet 2.0 km, Padat 3.0 km, Sedang 1.0 km)", teks);
    }

    [Fact]
    public void Ringkasan_RuasTanpaDataLive_DisebutTerpisahDanTidakIkutRataRata()
    {
        string teks = Ringkasan(Segmen(1.0, 0.9), Segmen(0.6, 1.0, live: false));

        Assert.Equal(
            "Lancar: kecepatan rata-rata 90 % dari kondisi normal. 0.6 km tanpa data live, dianggap lancar.",
            teks);
    }

    [Fact]
    public void Ringkasan_SemuaTanpaDataLive()
    {
        Assert.Equal(
            "Tidak ada data live untuk ruas ini, dianggap lancar.",
            Ringkasan(Segmen(0.5, 1.0, live: false), Segmen(0.6, 1.0, live: false)));
    }

    [Fact]
    public void RuasTanpaDataLive_EmisinyaTetapDihitungLancar()
    {
        var mobil = new KendaraanBermotor("Mobil", "", 0.192, faktorPenaltiKemacetan: 1.5);
        var kondisi = new KondisiLaluLintas([Segmen(3, 1.0, live: false)], live: true);

        Assert.Equal(mobil.HitungEmisi(3, TingkatKemacetan.Lancar), kondisi.HitungEmisi(mobil), precision: 6);
    }
}
