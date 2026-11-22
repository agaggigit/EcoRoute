using System.Windows.Controls;
using System.Windows.Input;
using EcoRoute.ViewModels;

namespace EcoRoute.Views.Components;

public partial class DaftarSaranView : UserControl
{
    public DaftarSaranView()
    {
        InitializeComponent();
    }

    // Pilih saat tombol mouse ditekan (bukan dilepas): kalau menunggu dilepas, TextBox sudah
    // kehilangan fokus duluan dan daftar keburu tertutup.
    private void Item_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: var place } && DataContext is SaranLokasiViewModel saran)
        {
            saran.PilihCommand.Execute(place);
            e.Handled = true;
        }
    }
}
