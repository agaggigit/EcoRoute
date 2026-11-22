using System.Windows.Controls;
using System.Windows.Input;
using EcoRoute.Models;
using EcoRoute.ViewModels;

namespace EcoRoute.Views.Components;

public partial class RouteInputView : UserControl
{
    public RouteInputView()
    {
        InitializeComponent();
    }

    // Kolom yang terakhir difokus jadi kolom aktif: klik peta berikutnya mengisi kolom ini
    private void KolomAsal_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.KolomAktif = KolomRute.Asal;
    }

    private void KolomTujuan_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.KolomAktif = KolomRute.Tujuan;
    }
}
