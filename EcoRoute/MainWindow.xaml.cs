
using System;
using System.Windows;
using EcoRoute.ViewModels;

namespace EcoRoute;

public partial class MainWindow : Window
{
    private bool _databaseLoaded;

    public bool LogoutRequested { get; private set; }

    public MainWindow(int userId)
    {
        InitializeComponent();

        DataContext = new MainViewModel(userId);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_databaseLoaded)
            return;

        _databaseLoaded = true;

        if (DataContext is not MainViewModel viewModel)
            return;

        try
        {
            await viewModel.LoadModaFromDatabaseAsync();
        }
        catch (Exception ex)
        {
            _databaseLoaded = false;

            MessageBox.Show(
                "Gagal memuat database SQLite:\n\n" + ex.Message,
                "EcoRoute",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void LogoutButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new LogoutDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
            return;

        LogoutRequested = true;
        Close();
    }
}
