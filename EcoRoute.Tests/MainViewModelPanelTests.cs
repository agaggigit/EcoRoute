using EcoRoute.Models;
using EcoRoute.ViewModels;

namespace EcoRoute.Tests;

// Panel Riwayat / Eco Score menggantikan card Jelajah sementara, tanpa menghapus isinya
public class MainViewModelPanelTests
{
    private static MainViewModel BuatViewModel() => new(userId: 1);

    [Fact]
    public void Awalnya_CardJelajahYangTampil()
    {
        var vm = BuatViewModel();

        Assert.True(vm.IsJelajahAktif);
        Assert.False(vm.IsMenuOpen);
        Assert.False(vm.IsEcoScoreOpen);
    }

    [Fact]
    public void BukaRiwayatLaluEcoScore_HanyaSatuPanelYangTerbuka()
    {
        var vm = BuatViewModel();

        vm.ToggleMenuCommand.Execute(null);
        Assert.True(vm.IsMenuOpen);
        Assert.False(vm.IsJelajahAktif);

        vm.ToggleEcoScoreCommand.Execute(null);
        Assert.True(vm.IsEcoScoreOpen);
        Assert.False(vm.IsMenuOpen);
        Assert.False(vm.IsJelajahAktif);
    }

    [Fact]
    public void KembaliKeJelajah_IsiCardTidakDireset()
    {
        var vm = BuatViewModel();
        vm.CurrentState = AppState.StateC_InputRoute;
        vm.TitikAsal = "Universitas Gadjah Mada";
        vm.TitikTujuan = "Malioboro";

        vm.ToggleEcoScoreCommand.Execute(null);
        vm.BukaJelajahCommand.Execute(null);

        Assert.True(vm.IsJelajahAktif);
        Assert.Equal(AppState.StateC_InputRoute, vm.CurrentState);
        Assert.Equal("Universitas Gadjah Mada", vm.TitikAsal);
        Assert.Equal("Malioboro", vm.TitikTujuan);
    }

    [Fact]
    public void KlikPinDiPeta_SaatPanelTerbuka_KembaliKeCardJelajah()
    {
        var vm = BuatViewModel();
        vm.ToggleMenuCommand.Execute(null);

        vm.PilihTempatCommand.Execute(new Place("Malioboro", "Jl. Malioboro", "Wisata", -7.79, 110.36));

        Assert.True(vm.IsJelajahAktif);
        Assert.Equal(AppState.StateB_SearchLocation, vm.CurrentState);
    }
}
