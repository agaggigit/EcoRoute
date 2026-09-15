// Untuk menyimpan state, menyimpan data dari user, menyimpan data kalkulasi, menyediakan perintah tombol
using System.Windows.Input;
using EcoRoute.Models;

namespace EcoRoute.ViewModels;

public class MainViewModel : BaseViewModel
{
    private AppState _currentState = AppState.StateA_Idle;
    private string _titikAsal = string.Empty;
    private string _titikTujuan = string.Empty;
    private string _selectedModa = "Mobil";

    private double _jarakKm;
    private double _emisiKarbonKg;
    private string _errorMessage = string.Empty;

    public AppState CurrentState
    {
        get => _currentState;
        set => SetProperty(ref _currentState, value);
    }

    public string TitikAsal
    {
        get => _titikAsal;
        set => SetProperty(ref _titikAsal, value);
    }

    public string TitikTujuan
    {
        get => _titikTujuan;
        set => SetProperty(ref _titikTujuan, value);
    }

    public string SelectedModa
    {
        get => _selectedModa;
        set => SetProperty(ref _selectedModa, value);
    }

    public double JarakKm
    {
        get => _jarakKm;
        set => SetProperty(ref _jarakKm, value);
    }
    public double EmisiKarbonKg
    {
        get => _emisiKarbonKg;
        set => SetProperty(ref _emisiKarbonKg, value);
    }
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand HitungEmisiCommand { get; }
    public ICommand ResetCommand { get; }

    public MainViewModel()
    {
        HitungEmisiCommand = new RelayCommand(ExecuteHitungEmisi, CanExecuteHitungEmisi);
        ResetCommand = new RelayCommand(ExecuteReset);
    }

    private async void ExecuteHitungEmisi(object? parameter)
    {
        CurrentState = AppState.StateL_Loading;

        await Task.Delay(2000);  

        // MOCK, SIMULASI BERHASIL (Nanti manggil backend)
        // ===============================
        JarakKm = 14.5;
        EmisiKarbonKg = 14.5;
        // ===============================
        CurrentState = AppState.StateD_RouteResult;
    }

    private bool CanExecuteHitungEmisi(object? parameter)
    {
        return CurrentState != AppState.StateL_Loading && !string.IsNullOrWhiteSpace(TitikAsal) && !string.IsNullOrWhiteSpace(TitikTujuan);
    }

    private void ExecuteReset(object? parameter)
    {
        TitikAsal = string.Empty;
        TitikTujuan = string.Empty;
        ErrorMessage = string.Empty;
        CurrentState = AppState.StateC_InputRoute;
    }
}