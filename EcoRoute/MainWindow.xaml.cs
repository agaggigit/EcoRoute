
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EcoRoute.ViewModels;

namespace EcoRoute;

public partial class MainWindow : Window
{
    private readonly int _userId;
    private bool _databaseLoaded;

    public bool LogoutRequested { get; private set; }

    public MainWindow(int userId)
    {
        _userId = userId;

        InitializeComponent();

        DataContext = new MainViewModel(_userId);

        Loaded += MainWindow_Loaded;
        TambahkanTombolLogout();
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
                "Gagal memuat database SQLite:\n\n" +
                ex.Message,
                "EcoRoute",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void TambahkanTombolLogout()
    {
        if (Content is not Grid rootGrid)
            throw new InvalidOperationException(
                "Root MainWindow harus berupa Grid.");

        var tombolLogout = new Button
        {
            Content = "Logout",
            Width = 90,
            Height = 36,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 24, 24),
            Background = new SolidColorBrush(
                Color.FromRgb(33, 122, 83)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Keluar dari akun"
        };

        tombolLogout.Click += (_, _) =>
        {
            var hasil = MessageBox.Show(
                "Yakin ingin logout dari EcoRoute?",
                "Konfirmasi Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (hasil != MessageBoxResult.Yes)
                return;

            LogoutRequested = true;
            Close();
        };

        Grid.SetRowSpan(
            tombolLogout,
            Math.Max(1, rootGrid.RowDefinitions.Count));

        Grid.SetColumnSpan(
            tombolLogout,
            Math.Max(1, rootGrid.ColumnDefinitions.Count));

        Panel.SetZIndex(tombolLogout, 1000);

        rootGrid.Children.Add(tombolLogout);
    }
}
