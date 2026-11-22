
using System;
using System.Globalization;
using System.Windows;
using EcoRoute.Services;

namespace EcoRoute;

public partial class App : Application
{
    protected override async void OnStartup(
        StartupEventArgs e)
    {
        CultureInfo.CurrentCulture =
            CultureInfo.CurrentUICulture =
                new CultureInfo("id-ID");

        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            await SqliteDatabaseInitializer.InitializeAsync();

            while (true)
            {
                var loginWindow = new LoginWindow();

                bool? loginBerhasil =
                    loginWindow.ShowDialog();

                if (loginBerhasil != true ||
                    loginWindow.LoggedInUser is null)
                {
                    break;
                }

                int userId =
                    loginWindow.LoggedInUser.UserId;

                var dashboard = new MainWindow(userId);

                MainWindow = dashboard;

                dashboard.ShowDialog();

                if (!dashboard.LogoutRequested)
                {
                    break;
                }

                MainWindow = null;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Gagal membuka EcoRoute:\n\n" +
                ex.Message,
                "Kesalahan Aplikasi",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        finally
        {
            Shutdown();
        }
    }
}
