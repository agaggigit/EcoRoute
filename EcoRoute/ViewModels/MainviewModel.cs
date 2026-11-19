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
        new KendaraanBermotor("Mobil",      "",      0.1920),
        new KendaraanBermotor("Motor",      "\U0001F6F5",  0.1030),
        new KendaraanBermotor("Bus",        "",      0.0890),
        new KendaraanBermotor("Kereta",     "",      0.0410),
        new TransportasiAktif("Sepeda",     "\U0001F6B2"),
        new TransportasiAktif("Jalan Kaki", ""),
    ];

    // Ikon dari Segoe UI Symbol (lihat IconFont di App.xaml)
    public IReadOnlyList<KategoriTempat> DaftarKategori { get; } =
    [
        new KategoriTempat("Wisata",       "\U0001F4F7"),
        new KategoriTempat("Kuliner",      "\U0001F374"),
        new KategoriTempat("Kampus",       "\U0001F393"),
        new KategoriTempat("Transportasi", "\U0001F686"),
        new KategoriTempat("Belanja",      "\U0001F6CD"),
        new KategoriTempat("Taman",        "\U0001F333"),
    ];

    // Tempat populer Yogyakarta: tampil sebagai pin di peta dan chip di card State A
    public IReadOnlyList<Place> TempatPopuler { get; } =
    [
        new Place("Malioboro",               "Jl. Malioboro, Yogyakarta",                 "Wisata",       -7.792583, 110.365861),
        new Place("Tugu Yogyakarta",         "Jl. Jend. Sudirman, Yogyakarta",            "Wisata",       -7.782889, 110.367083),
        new Place("Keraton Yogyakarta",      "Jl. Rotowijayan, Kraton, Yogyakarta",       "Wisata",       -7.805284, 110.364203),
        new Place("Taman Sari",              "Patehan, Kraton, Yogyakarta",               "Wisata",       -7.810074, 110.359257),
        new Place("Benteng Vredeburg",       "Jl. Margo Mulyo, Yogyakarta",               "Wisata",       -7.800269, 110.366260),
        new Place("Bakpia Pathok 25",        "Jl. AIP II KS Tubun, Ngampilan",            "Kuliner",      -7.793900, 110.357400),
        new Place("Mie Ayam Bu Tumini",      "Jl. Imogiri Timur, Umbulharjo",             "Kuliner",      -7.819900, 110.378600),
        new Place("Universitas Gadjah Mada", "Bulaksumur, Sleman",                        "Kampus",       -7.770717, 110.377724),
        new Place("Universitas Negeri Yogyakarta", "Jl. Colombo, Karangmalang, Sleman",   "Kampus",       -7.776500, 110.386700),
        new Place("Stasiun Yogyakarta",      "Jl. Margo Utomo, Gedong Tengen",            "Transportasi", -7.789173, 110.363358),
        new Place("Stasiun Lempuyangan",     "Jl. Lempuyangan, Bausasran",                "Transportasi", -7.790200, 110.375000),
        new Place("Pasar Beringharjo",       "Jl. Pabringan, Ngupasan, Yogyakarta",       "Belanja",      -7.798700, 110.365400),
        new Place("Galeria Mall",            "Jl. Jend. Sudirman, Terban",                "Belanja",      -7.782900, 110.380400),
        new Place("Ambarrukmo Plaza",        "Jl. Laksda Adisucipto, Caturtunggal",       "Belanja",      -7.782800, 110.401400),
        new Place("Alun-Alun Kidul",         "Patehan, Kraton, Yogyakarta",               "Taman",        -7.811860, 110.363470),
        new Place("Gembira Loka Zoo",        "Jl. Kebun Raya, Rejowinangun",              "Taman",        -7.806000, 110.396300),
    ];

    // Yang tampil sebagai chip di card State A
    public IReadOnlyList<Place> TempatSorotan => TempatPopuler
        .Where(t => t.Nama is "Malioboro" or "Tugu Yogyakarta" or "Keraton Yogyakarta"
                           or "Universitas Gadjah Mada" or "Stasiun Yogyakarta" or "Taman Sari")
        .ToList();

    // null = semua kategori tampil
    private string? _kategoriAktif;
    public string? KategoriAktif
    {
        get => _kategoriAktif;
        private set
        {
            if (!SetProperty(ref _kategoriAktif, value)) return;
            foreach (var k in DaftarKategori)
                k.IsAktif = k.Nama == value;
        }
    }

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
    public ICommand PilihTempatCommand { get; }
    public ICommand PilihKategoriCommand { get; }

    public MainViewModel()
    {
        PilihTempatCommand = new RelayCommand(ExecutePilihTempat, p => p is Place);
        // Klik chip yang sama lagi -> filter dimatikan
        PilihKategoriCommand = new RelayCommand(p =>
            KategoriAktif = p is string nama && nama != KategoriAktif ? nama : null);
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

    // A / B -> B. Dari chip tempat populer atau klik pin di peta
    private void ExecutePilihTempat(object? parameter)
    {
        if (parameter is not Place place) return;
        SearchQuery = place.Nama;
        SelectedPlace = place;
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
        var emisiPerModa = DaftarModa.Select(m => (Moda: m, Emisi: m.HitungEmisi(JarakKm))).ToList();
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
            .Where(x => x.Moda is KendaraanBermotor && x.Emisi < EmisiKarbonKg)
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
