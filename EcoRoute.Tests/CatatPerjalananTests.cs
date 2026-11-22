using EcoRoute.Models;
using EcoRoute.ViewModels;

namespace EcoRoute.Tests;

// Perjalanan hanya dicatat lewat "Pakai rute ini", dan tahapan level Eco Score untuk panel Eco Score
public class CatatPerjalananTests
{
    [Fact]
    public void PakaiRuteIni_HanyaBisaSaatHasilTampil()
    {
        var vm = new MainViewModel(userId: 1) { CurrentState = AppState.StateC_InputRoute };

        Assert.False(vm.CatatPerjalananCommand.CanExecute(null));
        Assert.False(vm.IsPerjalananTercatat);

        vm.CurrentState = AppState.StateD_RouteResult;
        Assert.True(vm.CatatPerjalananCommand.CanExecute(null));
    }

    [Fact]
    public void TahapanLevel_EmpatTahapDanBibitAktifSaatAwal()
    {
        var vm = new MainViewModel(userId: 1);

        Assert.Equal(["Bibit", "Tunas", "Pohon", "Hutan"], vm.TahapanLevel.Select(t => t.Nama));
        Assert.True(vm.TahapanLevel[0].Aktif);
        Assert.True(vm.TahapanLevel[0].Tercapai);
        Assert.False(vm.TahapanLevel[1].Tercapai);
        Assert.Equal(100, vm.TahapanLevel[1].PoinMinimal);
    }

    [Fact]
    public void RiwayatTanpaKoordinat_TidakBisaDiulangi()
    {
        var vm = new MainViewModel(userId: 1);

        Assert.False(vm.UlangiPerjalananCommand.CanExecute(new TripHistory { NamaAsal = "A", NamaTujuan = "B" }));
        Assert.True(vm.UlangiPerjalananCommand.CanExecute(new TripHistory
        {
            NamaAsal = "A", NamaTujuan = "B", LatAsal = -7.77, LngAsal = 110.37, LatTujuan = -7.79, LngTujuan = 110.36
        }));
    }
}
