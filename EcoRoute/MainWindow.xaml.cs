
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

    // ==========================================
    // TITLE BAR CUSTOM
    // ==========================================

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        bool maximized = WindowState == WindowState.Maximized;

        // Jendela yang di-maximize meluber sebesar bingkai resize di tiap sisi
        Thickness bingkai = SystemParameters.WindowResizeBorderThickness;
        RootGrid.Margin = maximized
            ? new Thickness(bingkai.Left + 4, bingkai.Top + 4, bingkai.Right + 4, bingkai.Bottom + 4)
            : new Thickness(0);

        MaximizeButton.Content = maximized ? "" : "";
        MaximizeButton.ToolTip = maximized ? "Kembalikan ukuran" : "Maximize";
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
