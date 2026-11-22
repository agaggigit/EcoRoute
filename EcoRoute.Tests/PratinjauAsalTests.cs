using EcoRoute.Models;
using EcoRoute.ViewModels;

namespace EcoRoute.Tests;

// Pratinjau titik awal (Enter di kolom titik awal / klik peta), tanpa memanggil jaringan:
// koordinat dan tempat populer diselesaikan lokal oleh CariKoordinatAsync.
public class PratinjauAsalTests
{
    private static MainViewModel BuatViewModel() => new(userId: 1)
    {
        CurrentState = AppState.StateC_InputRoute
    };

    [Fact]
    public async Task Koordinat_LangsungJadiPratinjau()
    {
        var vm = BuatViewModel();
        vm.TitikAsal = "-7.78, 110.36";

        await vm.PratinjauAsalAsync();

        Assert.NotNull(vm.PratinjauAsal);
        Assert.Equal(-7.78, vm.PratinjauAsal.Lat, precision: 6);
        Assert.Equal(110.36, vm.PratinjauAsal.Lng, precision: 6);
    }

    [Fact]
    public async Task TempatPopuler_DipakaiTanpaGeocoding()
    {
        var vm = BuatViewModel();
        vm.TitikAsal = "malioboro";

        await vm.PratinjauAsalAsync();

        Assert.Same(vm.TempatPopuler.Single(p => p.Nama == "Malioboro"), vm.PratinjauAsal);
    }

    [Fact]
    public async Task TeksDiubah_PratinjauDihapus()
    {
        var vm = BuatViewModel();
        vm.TitikAsal = "Malioboro";
        await vm.PratinjauAsalAsync();

        vm.TitikAsal = "Malioboro Mall";

        Assert.Null(vm.PratinjauAsal);
    }

    [Fact]
    public async Task HanyaSpasiYangBerubah_PratinjauTetap()
    {
        var vm = BuatViewModel();
        vm.TitikAsal = "Malioboro";
        await vm.PratinjauAsalAsync();

        vm.TitikAsal = "Malioboro ";

        Assert.NotNull(vm.PratinjauAsal);
    }

    [Fact]
    public async Task KoordinatTidakValid_TampilkanNotifikasiTanpaPratinjau()
    {
        var vm = BuatViewModel();
        vm.TitikAsal = "95, 110";

        await vm.PratinjauAsalAsync();

        Assert.Null(vm.PratinjauAsal);
        Assert.Contains("tidak valid", vm.Notifikasi);
        Assert.False(vm.IsMencari);
    }

    [Fact]
    public void KlikPeta_IsiTitikAsalDanPratinjau()
    {
        var vm = BuatViewModel();

        vm.PilihAsalDariPeta(-7.7827, 110.3671);

        Assert.Equal("-7.782700, 110.367100", vm.TitikAsal);
        Assert.NotNull(vm.PratinjauAsal);
        Assert.Equal(-7.7827, vm.PratinjauAsal.Lat, precision: 6);
    }

    [Fact]
    public async Task ResetDanTukar_MenghapusPratinjau()
    {
        var vm = BuatViewModel();
        vm.TitikAsal = "Malioboro";
        vm.TitikTujuan = "Tugu Yogyakarta";
        await vm.PratinjauAsalAsync();

        vm.TukarCommand.Execute(null);
        Assert.Null(vm.PratinjauAsal);

        vm.TitikAsal = "Malioboro";
        await vm.PratinjauAsalAsync();
        vm.ResetCommand.Execute(null);
        Assert.Null(vm.PratinjauAsal);
    }
}
