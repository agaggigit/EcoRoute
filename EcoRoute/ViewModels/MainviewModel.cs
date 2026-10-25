// Untuk menyimpan state, menyimpan data dari user, menyimpan data kalkulasi, menyediakan perintah tombol
using System.Windows.Input;
using EcoRoute.Models;

namespace EcoRoute.ViewModels;

public class MainViewModel : BaseViewModel
{
    private AppState _currentState = AppState.StateA_Idle;
    private bool _isMenuOpen;

    // State A & B
    private string _searchQuery = string.Empty;
    private Place? _selectedPlace;

    // State C
    private string _titikAsal = string.Empty;
    private string _titikTujuan = string.Empty;
    private string _selectedModa = "Mobil";

    // State D & E
    private double _jarakKm;
    private int _estimasiWaktuMenit;
    private double _emisiKarbonKg;
    private IReadOnlyList<PerbandinganEmisi> _perbandinganModa = [];
    private string _insightText = string.Empty;
    private string _errorMessage = string.Empty;

    public IReadOnlyList<ModaTransportasi> DaftarModa { get; } =
    [
        new("Mobil",      "",      0.1920),
        new("Motor",      "\U0001F6F5",  0.1030),
        new("Bus",        "",      0.0890),
        new("Kereta",     "",      0.0410),
        new("Sepeda",     "\U0001F6B2",  0),
        new("Jalan Kaki", "",      0),
    ];

    public AppState CurrentState
    {
        get => _currentState;
        set
        {
            if (SetProperty(ref _currentState, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        set => SetProperty(ref _isMenuOpen, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public Place? SelectedPlace
    {
        get => _selectedPlace;
        set => SetProperty(ref _selectedPlace, value);
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
        set
        {
            // Ganti moda saat hasil sudah tampil -> langsung hitung ulang
            if (SetProperty(ref _selectedModa, value) && CurrentState == AppState.StateD_RouteResult)
                HitungUlangEmisi();
        }
    }

    public double JarakKm
    {
        get => _jarakKm;
        set => SetProperty(ref _jarakKm, value);
    }

    public int EstimasiWaktuMenit
    {
        get => _estimasiWaktuMenit;
        set => SetProperty(ref _estimasiWaktuMenit, value);
    }

    public double EmisiKarbonKg
    {
        get => _emisiKarbonKg;
        set => SetProperty(ref _emisiKarbonKg, value);
    }

    public IReadOnlyList<PerbandinganEmisi> PerbandinganModa
    {
        get => _perbandinganModa;
        set => SetProperty(ref _perbandinganModa, value);
    }

    public string InsightText
    {
        get => _insightText;
        set => SetProperty(ref _insightText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand ToggleMenuCommand { get; }
    public ICommand CariCommand { get; }
    public ICommand MulaiRuteCommand { get; }
    public ICommand TukarCommand { get; }
    public ICommand HitungEmisiCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand TutupCommand { get; }

    public MainViewModel()
    {
        ToggleMenuCommand = new RelayCommand(_ => IsMenuOpen = !IsMenuOpen);
        CariCommand = new RelayCommand(ExecuteCari, _ => !string.IsNullOrWhiteSpace(SearchQuery));
        MulaiRuteCommand = new RelayCommand(ExecuteMulaiRute);
        TukarCommand = new RelayCommand(_ => (TitikAsal, TitikTujuan) = (TitikTujuan, TitikAsal));
        HitungEmisiCommand = new RelayCommand(ExecuteHitungEmisi, CanExecuteHitungEmisi);
        ResetCommand = new RelayCommand(ExecuteReset);
        TutupCommand = new RelayCommand(ExecuteTutup);
    }

    // A -> B
    private void ExecuteCari(object? parameter)
    {
        // MOCK, SIMULASI HASIL PENCARIAN (Nanti manggil geocoding)
        // ===============================
        SelectedPlace = new Place(SearchQuery.Trim(), "Yogyakarta, Daerah Istimewa Yogyakarta", "Tempat Wisata", -7.792583, 110.365861);
        // ===============================
        CurrentState = AppState.StateB_SearchLocation;
    }

    // A / B -> C. Dari State B, lokasi yang dipilih otomatis jadi tujuan.
    private void ExecuteMulaiRute(object? parameter)
    {
        if (SelectedPlace is not null)
            TitikTujuan = SelectedPlace.Nama;
        CurrentState = AppState.StateC_InputRoute;
    }

    // C -> L -> D / E
    private async void ExecuteHitungEmisi(object? parameter)
    {
        ErrorMessage = string.Empty;
        CurrentState = AppState.StateL_Loading;

        try
        {
            await Task.Delay(1500);

            if (string.Equals(TitikAsal.Trim(), TitikTujuan.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Titik awal dan tujuan tidak boleh sama.");

            // MOCK, SIMULASI BERHASIL (Nanti manggil backend)
            // ===============================
            JarakKm = 14.5;
            EstimasiWaktuMenit = 35;
            // ===============================

            HitungUlangEmisi();
            CurrentState = AppState.StateD_RouteResult;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CurrentState = AppState.StateE_Error;
        }
    }

    private bool CanExecuteHitungEmisi(object? parameter)
    {
        return CurrentState != AppState.StateL_Loading && !string.IsNullOrWhiteSpace(TitikAsal) && !string.IsNullOrWhiteSpace(TitikTujuan);
    }

    private void HitungUlangEmisi()
    {
        var emisiPerModa = DaftarModa.Select(m => (Moda: m, Emisi: JarakKm * m.FaktorEmisiKgPerKm)).ToList();
        double emisiMax = emisiPerModa.Max(x => x.Emisi);
        double emisiMin = emisiPerModa.Min(x => x.Emisi);

        PerbandinganModa = emisiPerModa
            .Select(x => new PerbandinganEmisi(
                x.Moda.Nama,
                x.Moda.Ikon,
                x.Emisi,
                emisiMax > 0 ? x.Emisi / emisiMax : 0,
                x.Moda.Nama == SelectedModa,
                x.Emisi == emisiMin))
            .ToList();

        EmisiKarbonKg = emisiPerModa.First(x => x.Moda.Nama == SelectedModa).Emisi;
        InsightText = BuatInsight(emisiPerModa);
    }

    private string BuatInsight(List<(ModaTransportasi Moda, double Emisi)> emisiPerModa)
    {
        if (EmisiKarbonKg == 0)
            return "Perjalanan ini tanpa emisi karbon. Mantap!";

        // Alternatif kendaraan bermotor paling hemat (sepeda/jalan kaki tidak dihitung)
        var alternatif = emisiPerModa
            .Where(x => x.Emisi > 0 && x.Emisi < EmisiKarbonKg)
            .OrderBy(x => x.Emisi)
            .FirstOrDefault();

        return alternatif.Moda is null
            ? $"{SelectedModa} sudah jadi kendaraan bermotor paling hemat untuk rute ini."
            : $"Naik {alternatif.Moda.Nama} menghemat {EmisiKarbonKg - alternatif.Emisi:0.00} kg CO₂ dibanding {SelectedModa}.";
    }

    // D / E -> C, input dikosongkan
    private void ExecuteReset(object? parameter)
    {
        TitikAsal = string.Empty;
        TitikTujuan = string.Empty;
        ErrorMessage = string.Empty;
        CurrentState = AppState.StateC_InputRoute;
    }

    // Tombol X: kembali ke State A dan bersihkan semuanya
    private void ExecuteTutup(object? parameter)
    {
        SearchQuery = string.Empty;
        SelectedPlace = null;
        TitikAsal = string.Empty;
        TitikTujuan = string.Empty;
        JarakKm = 0;
        EstimasiWaktuMenit = 0;
        EmisiKarbonKg = 0;
        PerbandinganModa = [];
        InsightText = string.Empty;
        ErrorMessage = string.Empty;
        CurrentState = AppState.StateA_Idle;
    }
}
