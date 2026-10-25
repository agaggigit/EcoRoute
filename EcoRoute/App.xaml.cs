using System.Globalization;
using System.Windows;

namespace EcoRoute;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Format angka di C# pakai bahasa Indonesia (2,78 bukan 2.78).
        // Untuk binding XAML, diatur lewat Language="id-ID" di MainWindow.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("id-ID");
        base.OnStartup(e);
    }
}
