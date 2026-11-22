using System.IO;
using EcoRoute.Models;
using EcoRoute.Services;

namespace EcoRoute.Tests;

public class TransJogjaServiceTests
{
    // Jalan lurus barat-timur di lat -7.80: simpul 0..6, tiap simpul ±0,55 km
    private const string SimpulJalan =
        "[[-7.8,110.36],[-7.8,110.365],[-7.8,110.37],[-7.8,110.375],[-7.8,110.38],[-7.8,110.385],[-7.8,110.39]]";

    private static TransJogjaService BuatLayanan(string trayekJson, string simpul = SimpulJalan) =>
        new($$"""{"diambil":"2026-11-22","simpul":{{simpul}},"trayek":[{{trayekJson}}]}""");

    // Jalan yang sama, tapi bus hanya lewat ke timur (simpul 0 -> 6)
    private const string TrayekSearahKeTimur = """
        {"kode":"7","nama":"Transjogja 7: Barat → Timur","searah":[[0,1,2,3,4,5,6]],"ruas":[],
         "halte":[{"nama":"Halte Barat","lat":-7.8,"lng":110.36,"simpul":0},
                  {"nama":"Halte Tengah","lat":-7.8,"lng":110.375,"simpul":3},
                  {"nama":"Halte Timur","lat":-7.8,"lng":110.39,"simpul":6}]}
        """;

    private const string TrayekPanjang = """
        {"kode":"1A","nama":"Transjogja 1A: Barat → Timur","ruas":[[0,1,2],[2,3,4,5,6]],
         "halte":[{"nama":"Halte Barat","lat":-7.8,"lng":110.36,"simpul":0},
                  {"nama":"Halte Tengah","lat":-7.8,"lng":110.375,"simpul":3},
                  {"nama":"Halte Timur","lat":-7.8,"lng":110.39,"simpul":6}]}
        """;

    private static Place Titik(double lat, double lng) => new("Titik", "", "Koordinat", lat, lng);

    [Fact]
    public void AsalDanTujuanDekatHalte_DapatTrayekLangsung()
    {
        var layanan = BuatLayanan(TrayekPanjang);

        RuteBus? rute = layanan.CariRuteLangsung(Titik(-7.801, 110.36), Titik(-7.801, 110.39));

        Assert.NotNull(rute);
        Assert.Equal("1A", rute.KodeTrayek);
        Assert.Equal("Halte Barat", rute.HalteNaik.Nama);
        Assert.Equal("Halte Timur", rute.HalteTurun.Nama);
    }

    [Fact]
    public void JalurBus_MengikutiRuasTrayekDariHalteNaikKeHalteTurun()
    {
        var layanan = BuatLayanan(TrayekPanjang);

        RuteBus rute = layanan.CariRuteLangsung(Titik(-7.801, 110.36), Titik(-7.801, 110.39))!;

        double jarakJalan = TransJogjaService.JarakKm(-7.8, 110.36, -7.8, 110.39);
        Assert.Equal(jarakJalan, rute.JarakBusKm, 3);
        Assert.Equal(7, rute.TitikBus.Count);
        Assert.Equal(110.36, rute.TitikBus[0][1], 6);
        Assert.Equal(110.39, rute.TitikBus[^1][1], 6);
    }

    [Fact]
    public void JalanKaki_DihitungDariJarakLurusDikaliPengali()
    {
        var layanan = BuatLayanan(TrayekPanjang);

        RuteBus rute = layanan.CariRuteLangsung(Titik(-7.803, 110.36), Titik(-7.801, 110.39))!;

        double lurus = TransJogjaService.JarakKm(-7.803, 110.36, -7.8, 110.36);
        Assert.Equal(lurus * TransJogjaService.PengaliJalanKaki, rute.JalanKakiAwalKm, 6);
        Assert.Equal(rute.JalanKakiAwalKm + rute.JarakBusKm + rute.JalanKakiAkhirKm, rute.JarakTotalKm, 9);
    }

    [Fact]
    public void TidakAdaHalteDalamJangkauan_HasilNull()
    {
        var layanan = BuatLayanan(TrayekPanjang);

        // Asal ±2 km di selatan jalan trayek
        RuteBus? rute = layanan.CariRuteLangsung(Titik(-7.82, 110.36), Titik(-7.801, 110.39));

        Assert.Null(rute);
    }

    [Fact]
    public void JalanKakiLangsungLebihDekat_HasilNull()
    {
        var layanan = BuatLayanan(TrayekPanjang);

        // Asal & tujuan berdekatan tapi jauh dari halte: naik bus tidak masuk akal
        RuteBus? rute = layanan.CariRuteLangsung(Titik(-7.806, 110.367), Titik(-7.806, 110.369));

        Assert.Null(rute);
    }

    [Fact]
    public void RuasTidakTersambung_TidakDianggapSatuTrayek()
    {
        var layanan = BuatLayanan("""
            {"kode":"9","nama":"Transjogja 9","ruas":[[0,1,2],[4,5,6]],
             "halte":[{"nama":"A","lat":-7.8,"lng":110.36,"simpul":0},
                      {"nama":"B","lat":-7.8,"lng":110.37,"simpul":2},
                      {"nama":"C","lat":-7.8,"lng":110.39,"simpul":6}]}
            """);

        RuteBus? rute = layanan.CariRuteLangsung(Titik(-7.801, 110.36), Titik(-7.801, 110.39));

        Assert.Null(rute);
    }

    [Fact]
    public void JalanSearah_BusSesuaiArah_DapatTrayek()
    {
        var layanan = BuatLayanan(TrayekSearahKeTimur);

        RuteBus? rute = layanan.CariRuteLangsung(Titik(-7.801, 110.36), Titik(-7.801, 110.39));

        Assert.NotNull(rute);
        Assert.Equal("Halte Barat", rute.HalteNaik.Nama);
        Assert.Equal("Halte Timur", rute.HalteTurun.Nama);
        Assert.Equal(110.39, rute.TitikBus[^1][1], 6);
    }

    [Fact]
    public void JalanSearah_BusTidakDitempuhMelawanArah()
    {
        var layanan = BuatLayanan(TrayekSearahKeTimur);

        // Timur -> barat: bus hanya lewat ke timur
        RuteBus? rute = layanan.CariRuteLangsung(Titik(-7.801, 110.39), Titik(-7.801, 110.36));

        Assert.Null(rute);
    }

    [Fact]
    public void TrayekBerputar_HalteDiBelakangTidakDitempuhSatuPutaranPenuh()
    {
        // Putaran searah jarum jam terbalik: ke timur di lat -7.80, lalu kembali ke barat di lat -7.83
        const string simpul =
            "[[-7.8,110.36],[-7.8,110.365],[-7.8,110.37],[-7.8,110.375],[-7.8,110.38],[-7.8,110.385],[-7.8,110.39]," +
            "[-7.83,110.39],[-7.83,110.36]]";
        var layanan = BuatLayanan("""
            {"kode":"8","nama":"Transjogja 8: Putaran","searah":[[0,1,2,3,4,5,6],[6,7],[7,8],[8,0]],"ruas":[],
             "halte":[{"nama":"Halte A","lat":-7.8,"lng":110.37,"simpul":2},
                      {"nama":"Halte B","lat":-7.8,"lng":110.38,"simpul":4}]}
            """, simpul);

        RuteBus? searahPutaran = layanan.CariRuteLangsung(Titik(-7.801, 110.37), Titik(-7.801, 110.38));
        RuteBus? harusMemutar = layanan.CariRuteLangsung(Titik(-7.801, 110.38), Titik(-7.801, 110.37));

        Assert.NotNull(searahPutaran);
        Assert.Equal("Halte A", searahPutaran.HalteNaik.Nama);
        Assert.Null(harusMemutar); // B -> A lewat ±12 km putaran, padahal jaraknya ±1 km
    }

    [Fact]
    public void DataRusak_LayananTetapBisaDipakaiTanpaTrayek()
    {
        var layanan = new TransJogjaService("""{"simpul":[],"trayek":[]}""");

        Assert.False(layanan.AdaData);
        Assert.Null(layanan.CariRuteLangsung(Titik(-7.77, 110.37), Titik(-7.79, 110.36)));
    }

    // Data asli dari OpenStreetMap (EcoRoute/Data/trayek-transjogja.json)
    [Fact]
    public void DataAsli_UgmKeMalioboro_AdaTrayekYangMasukAkal()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "trayek-transjogja.json");
        var layanan = new TransJogjaService(File.ReadAllText(path));
        Place ugm = Titik(-7.770717, 110.377724);
        Place malioboro = Titik(-7.792583, 110.365861);

        RuteBus? rute = layanan.CariRuteLangsung(ugm, malioboro);

        Assert.True(layanan.JumlahTrayek >= 15);
        Assert.NotNull(rute);
        double lurus = TransJogjaService.JarakKm(ugm.Lat, ugm.Lng, malioboro.Lat, malioboro.Lng);
        Assert.InRange(rute.JarakBusKm, lurus * 0.5, lurus * 3);
        Assert.True(rute.JalanKakiAwalKm <= TransJogjaService.MaksJalanKakiKm * TransJogjaService.PengaliJalanKaki);
        Assert.True(rute.JalanKakiAkhirKm <= TransJogjaService.MaksJalanKakiKm * TransJogjaService.PengaliJalanKaki);
    }

    // Jalan Margo Utomo (Tugu sampai rel kereta) satu arah ke selatan. Bus 1A lewat sini dari Tugu
    // menuju rel, lalu keluar lagi ke utara lewat Jl. Mataram. Dulu Malioboro -> Tugu menghasilkan
    // "naik di Teras Malioboro 2 (Jl. Mataram), turun di Mangkubumi 1", dan jalurnya naik ke utara di
    // Margo Utomo karena jaringan trayek diperlakukan dua arah.
    [Fact]
    public void DataAsli_MalioboroKeTugu_BusTidakMelawanArahDiMargoUtomo()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "trayek-transjogja.json");
        var layanan = new TransJogjaService(File.ReadAllText(path));
        Place malioboro = Titik(-7.792583, 110.365861);
        Place tugu = Titik(-7.782889, 110.367083);

        RuteBus? rute = layanan.CariRuteLangsung(malioboro, tugu);

        if (rute is null)
            return; // tidak ada bus langsung yang masuk akal: jalan kaki, itu juga benar

        for (int i = 0; i + 1 < rute.TitikBus.Count; i++)
        {
            double[] a = rute.TitikBus[i];
            double[] b = rute.TitikBus[i + 1];
            bool diMargoUtomo = DiMargoUtomo(a) && DiMargoUtomo(b);
            Assert.False(diMargoUtomo && b[0] > a[0],
                $"Trayek {rute.KodeTrayek} ({rute.HalteNaik.Nama} -> {rute.HalteTurun.Nama}) " +
                $"bergerak ke utara di Margo Utomo: ({a[0]}, {a[1]}) -> ({b[0]}, {b[1]})");
        }
    }

    // Bagian lurus Jl. Margo Utomo antara Tugu dan perlintasan rel (tanpa tikungan di ujung selatan)
    private static bool DiMargoUtomo(double[] titik) =>
        titik[0] is > -7.7893 and < -7.7832 && titik[1] is > 110.3660 and < 110.3673;
}
