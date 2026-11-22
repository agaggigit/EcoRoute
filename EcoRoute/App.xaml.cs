
using System;
using System.Globalization;
using System.Windows;
using EcoRoute.Services;

namespace EcoRoute;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        // Menggunakan format angka dan bahasa Indonesia.
        CultureInfo.CurrentCulture =
            CultureInfo.CurrentUICulture =
                new CultureInfo("id-ID");

        base.OnStartup(e);

        // Jangan tutup aplikasi ketika LoginWindow ditutup.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            // Membuat atau membuka database SQLite lokal.
            await SqliteDatabaseInitializer.InitializeAsync();

            // Menampilkan halaman Login/Register.
            var loginWindow = new LoginWindow();

            bool? loginBerhasil = loginWindow.ShowDialog();

            // Jika pengguna menutup Login tanpa berhasil masuk.
            if (loginBerhasil != true ||
                loginWindow.LoggedInUser is null)
            {
                Shutdown();
                return;
            }

            // Membuka dashboard setelah Login berhasil.
            var mainWindow = new MainWindow();

            MainWindow = mainWindow;

            // Tampilkan dashboard.
            mainWindow.Show();

            // Aplikasi akan ditutup ketika dashboard ditutup.
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Gagal membuka EcoRoute:\n\n" + ex.Message,
                "Kesalahan Aplikasi",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            Shutdown();
        }
    }
}
