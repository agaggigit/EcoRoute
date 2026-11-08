
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Npgsql;
using EcoRoute.ViewModels;

namespace EcoRoute;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _databaseLoaded;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        // Jangan memuat database dua kali.
        if (_databaseLoaded)
            return;

        // MainViewModel harus sudah menjadi DataContext.
        if (DataContext is not MainViewModel viewModel)
        {
            MessageBox.Show(
                "MainViewModel belum terhubung ke MainWindow.",
                "Kesalahan Konfigurasi",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            return;
        }

        // Minta password melalui dialog sederhana.
        string? password = ShowPasswordDialog();

        if (password is null)
        {
            MessageBox.Show(
                "Database belum dimuat. Perhitungan emisi belum dapat digunakan.",
                "EcoRoute",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            return;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = "ecoroute",
            Username = "postgres",
            Password = password
        };

        try
        {
            await viewModel.LoadModaFromDatabaseAsync(
                builder.ConnectionString
            );

            _databaseLoaded = true;

            MessageBox.Show(
                $"Berhasil mengambil {viewModel.DaftarModa.Count} moda transportasi dari PostgreSQL!",
                "Koneksi Database Berhasil",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Gagal memuat data PostgreSQL:\n\n" +
                ex.Message +
                "\n\nPastikan PostgreSQL aktif, database ecoroute tersedia, dan password benar.",
                "Koneksi Database Gagal",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private string? ShowPasswordDialog()
    {
        var dialog = new Window
        {
            Title = "Koneksi PostgreSQL - EcoRoute",
            Width = 390,
            Height = 210,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(20)
        };

        var label = new TextBlock
        {
            Text = "Masukkan password PostgreSQL:",
            Margin = new Thickness(0, 0, 0, 10)
        };

        var passwordBox = new PasswordBox
        {
            Height = 32
        };

        var button = new Button
        {
            Content = "Hubungkan Database",
            Height = 34,
            Margin = new Thickness(0, 15, 0, 0),
            IsDefault = true
        };

        button.Click += (_, _) =>
        {
            dialog.DialogResult = true;
            dialog.Close();
        };

        panel.Children.Add(label);
        panel.Children.Add(passwordBox);
        panel.Children.Add(button);

        dialog.Content = panel;

        dialog.Loaded += (_, _) =>
        {
            passwordBox.Focus();
        };

        bool? result = dialog.ShowDialog();

        return result == true
            ? passwordBox.Password
            : null;
    }
}
