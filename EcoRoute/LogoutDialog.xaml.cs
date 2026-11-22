
using System.Windows;

namespace EcoRoute;

public partial class LogoutDialog : Window
{
    public LogoutDialog()
    {
        InitializeComponent();
    }

    private void Batal_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Keluar_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
