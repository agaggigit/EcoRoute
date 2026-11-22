
using System;
using System.Windows;
using EcoRoute.Services;

namespace EcoRoute;

public partial class LoginWindow : Window
{
    private readonly AuthService _authService = new();
    private bool _modeRegister;

    public AuthUser? LoggedInUser { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();
    }

    private void TombolGantiMode_Click(
        object sender, RoutedEventArgs e)
    {
        _modeRegister = !_modeRegister;

        PanelNama.Visibility = _modeRegister
            ? Visibility.Visible
            : Visibility.Collapsed;

        JudulForm.Text = _modeRegister
            ? "Buat Akun Baru"
            : "Masuk ke Akun";

        TombolSubmit.Content = _modeRegister
            ? "Register"
            : "Login";

        TombolGantiMode.Content = _modeRegister
            ? "Sudah punya akun? Login"
            : "Belum punya akun? Register";

        PesanError.Visibility = Visibility.Collapsed;
        InputPassword.Clear();
    }

    private async void TombolSubmit_Click(
        object sender, RoutedEventArgs e)
    {
        TombolSubmit.IsEnabled = false;
        PesanError.Visibility = Visibility.Collapsed;

        try
        {
            string email = InputEmail.Text.Trim();
            string password = InputPassword.Password;

            AuthUser? user;

            if (_modeRegister)
            {
                user = await _authService.RegisterAsync(
                    InputNama.Text,
                    email,
                    password
                );
            }
            else
            {
                user = await _authService.LoginAsync(
                    email,
                    password
                );

                if (user is null)
                {
                    TampilkanError(
                        "Email atau password salah."
                    );
                    return;
                }
            }

            LoggedInUser = user;

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            TampilkanError(ex.Message);
        }
        finally
        {
            TombolSubmit.IsEnabled = true;
        }
    }

    private void TampilkanError(string pesan)
    {
        PesanError.Text = pesan;
        PesanError.Visibility = Visibility.Visible;
    }
}
