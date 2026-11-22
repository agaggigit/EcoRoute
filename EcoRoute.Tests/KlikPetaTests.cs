using System.Text.Json;
using EcoRoute.Models;
using EcoRoute.Services;
using EcoRoute.ViewModels;

namespace EcoRoute.Tests;

// Klik peta: kolom aktif, nama tempat dari titik (reverse geocoding palsu, tanpa jaringan), dan pratinjau tujuan.
public class KlikPetaTests
{
    private static readonly Place JalanPogung =
        new("Jalan Pogung Kidul", "Sinduadi, Mlati, Sleman", "Residential", -7.7621, 110.3733);

    private static MainViewModel BuatViewModel(AppState state = AppState.StateC_InputRoute, Place? hasilNama = null) =>
        new(userId: 1,
            cariSaranOnline: (_, _) => Task.FromResult<IReadOnlyList<Place>>([]),
            cariNamaTitik: (lat, lng, _) => Task.FromResult(hasilNama is null ? null : hasilNama with { Lat = lat, Lng = lng }))
        {
            CurrentState = state
        };

    [Fact]
    public async Task KlikPeta_IsiAsalLaluKolomAktifPindahKeTujuan()
    {
        var vm = BuatViewModel(hasilNama: JalanPogung);

        await vm.PilihTitikDariPetaAsync(-7.7621, 110.3733);

        Assert.Equal("Jalan Pogung Kidul", vm.TitikAsal);
        Assert.Equal("Jalan Pogung Kidul", vm.PratinjauAsal?.Nama);
        Assert.Equal(KolomRute.Tujuan, vm.KolomAktif);
    }

    [Fact]
    public async Task KlikKedua_MengisiTujuanBukanAsal()
    {
        var vm = BuatViewModel(hasilNama: JalanPogung);
        await vm.PilihTitikDariPetaAsync(-7.7621, 110.3733);

        await vm.PilihTitikDariPetaAsync(-7.7700, 110.3800);

        Assert.Equal("Jalan Pogung Kidul", vm.TitikAsal);
        Assert.Equal(-7.7621, vm.PratinjauAsal!.Lat, precision: 4);
        Assert.Equal(-7.7700, vm.TujuanTerpilih!.Lat, precision: 4);
    }

    [Fact]
    public async Task KolomAsalDipilihLagi_KlikMengubahAsal()
    {
        var vm = BuatViewModel(hasilNama: JalanPogung);
        await vm.PilihTitikDariPetaAsync(-7.7621, 110.3733);
        await vm.PilihTitikDariPetaAsync(-7.7700, 110.3800);

        vm.KolomAktif = KolomRute.Asal;
        await vm.PilihTitikDariPetaAsync(-7.7650, 110.3750);

        Assert.Equal(-7.7650, vm.PratinjauAsal!.Lat, precision: 4);
        Assert.Equal(-7.7700, vm.TujuanTerpilih!.Lat, precision: 4);
    }

    [Fact]
    public async Task KlikDekatTempatPopuler_DibulatkanKeTempatItu()
    {
        var vm = BuatViewModel(hasilNama: JalanPogung);
        Place tugu = vm.TempatPopuler.Single(p => p.Nama == "Tugu Yogyakarta");

        // ±30 m dari titik Tugu
        await vm.PilihTitikDariPetaAsync(tugu.Lat + 0.0002, tugu.Lng + 0.0001);

        Assert.Same(tugu, vm.PratinjauAsal);
        Assert.Equal("Tugu Yogyakarta", vm.TitikAsal);
    }

    [Fact]
    public async Task NamaTidakDitemukan_KoordinatTetapDipakai()
    {
        var vm = BuatViewModel(hasilNama: null);

        await vm.PilihTitikDariPetaAsync(-7.7621, 110.3733);

        Assert.Equal("-7.762100, 110.373300", vm.TitikAsal);
        Assert.NotNull(vm.PratinjauAsal);
    }

    [Fact]
    public async Task KlikPetaDiBeranda_MembukaKartuTempatDenganNama()
    {
        var vm = BuatViewModel(AppState.StateA_Idle, JalanPogung);

        await vm.PilihTitikDariPetaAsync(-7.7621, 110.3733);

        Assert.Equal(AppState.StateB_SearchLocation, vm.CurrentState);
        Assert.Equal("Jalan Pogung Kidul", vm.SelectedPlace?.Nama);

        vm.MulaiRuteCommand.Execute(null);
        Assert.Equal("Jalan Pogung Kidul", vm.TitikTujuan);
        Assert.Same(vm.SelectedPlace, vm.TujuanTerpilih);
        Assert.Equal(KolomRute.Asal, vm.KolomAktif);
    }

    [Fact]
    public async Task EnterDiKolomTujuan_JadiPratinjauTujuan()
    {
        var vm = BuatViewModel();
        vm.TitikTujuan = "malioboro";

        await vm.PratinjauTujuanAsync();

        Assert.NotNull(vm.TujuanTerpilih);
        Assert.Equal(-7.792583, vm.TujuanTerpilih.Lat, precision: 6);
        Assert.Equal(KolomRute.Asal, vm.KolomAktif);
    }

    [Fact]
    public void ParseNamaTitik_JalanDenganNomorDanKelurahan()
    {
        const string json = """
            {"category":"place","type":"house","name":"","display_name":"58, Jalan Kapten Haryadi, Sleman",
             "address":{"house_number":"58","road":"Jalan Kapten Haryadi","village":"Sardonoharjo",
                        "municipality":"Ngaglik","county":"Sleman"}}
            """;
        using JsonDocument doc = JsonDocument.Parse(json);

        Place? hasil = RoutingService.ParseNamaTitik(doc.RootElement, -7.7, 110.4);

        Assert.NotNull(hasil);
        Assert.Equal("Jalan Kapten Haryadi No. 58", hasil.Nama);
        Assert.Equal("Sardonoharjo, Ngaglik, Sleman", hasil.Alamat);
        Assert.Equal(-7.7, hasil.Lat);
    }

    [Fact]
    public void ParseNamaTitik_NamaTempatDidahulukan()
    {
        const string json = """
            {"category":"amenity","type":"university","name":"Universitas Gadjah Mada","display_name":"Universitas Gadjah Mada, Bulaksumur",
             "address":{"road":"Jalan Pancasila","suburb":"Caturtunggal","city":"Sleman"}}
            """;
        using JsonDocument doc = JsonDocument.Parse(json);

        Place? hasil = RoutingService.ParseNamaTitik(doc.RootElement, -7.77, 110.37);

        Assert.Equal("Universitas Gadjah Mada", hasil?.Nama);
        Assert.Equal("Jalan Pancasila, Caturtunggal, Sleman", hasil?.Alamat);
        Assert.Equal("Pendidikan", hasil?.Kategori);
    }

    [Fact]
    public void ParseNamaTitik_ErrorDariServer_Null()
    {
        using JsonDocument doc = JsonDocument.Parse("""{"error":"Unable to geocode"}""");

        Assert.Null(RoutingService.ParseNamaTitik(doc.RootElement, 0, 0));
    }
}
