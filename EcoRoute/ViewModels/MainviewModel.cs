
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using EcoRoute.Models;
using EcoRoute.Services;

namespace EcoRoute.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    // ID akun yang berhasil Login.
    private readonly int _penggunaAktifId;

    private readonly RoutingService _routingService = new();
    private readonly TrafficService _trafficService = new();
    private SqliteDatabaseService? _databaseService;

    private AppState _currentState = AppState.StateA_Idle;
    private bool _isMencari;
    private string _notifikasi = string.Empty;
    private RouteResult? _hasilRute;
    private IReadOnlyList<TripHistory> _riwayatPerjalanan = [];
    private string _statusRiwayat =
        "Sedang memuat riwayat perjalanan...";
    private bool _isMenuOpen;
    private bool _isEcoScoreOpen;

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

    // Nilai cadangan sebelum data moda berhasil dimuat dari SQLite.
    private IReadOnlyList<ModaTransportasi> _daftarModa =
    [
        new KendaraanBermotor("Mobil", "\uE804", 0.1920, 1.35),
        new KendaraanBermotor("Motor", "\U0001F6F5", 0.1030, 1.15),
        new KendaraanBermotor("Bus", "\uE806", 0.0890, 1.25),
        new KendaraanBermotor("Kereta", "\uE7C0", 0.0410),
        new TransportasiAktif("Sepeda", "\U0001F6B2"),
        new TransportasiAktif("Jalan Kaki", "\uE805"),
    ];

    public IReadOnlyList<KategoriTempat> DaftarKategori { get; } =
    [
        new KategoriTempat("Wisata", "\U0001F4F7"),
        new KategoriTempat("Kuliner", "\U0001F374"),
        new KategoriTempat("Kampus", "\U0001F393"),
        new KategoriTempat("Transportasi", "\U0001F686"),
        new KategoriTempat("Belanja", "\U0001F6CD"),
        new KategoriTempat("Taman", "\U0001F333"),
    ];

    public IReadOnlyList<Place> TempatPopuler { get; } =
    [
        new Place(
            "Malioboro",
            "Jl. Malioboro, Yogyakarta",
            "Wisata",
            -7.792583,
            110.365861),

        new Place(
            "Tugu Yogyakarta",
            "Jl. Jend. Sudirman, Yogyakarta",
            "Wisata",
            -7.782889,
            110.367083),

        new Place(
            "Keraton Yogyakarta",
            "Jl. Rotowijayan, Kraton, Yogyakarta",
            "Wisata",
            -7.805284,
            110.364203),

        new Place(
            "Taman Sari",
            "Patehan, Kraton, Yogyakarta",
            "Wisata",
            -7.810074,
            110.359257),

        new Place(
            "Benteng Vredeburg",
            "Jl. Margo Mulyo, Yogyakarta",
            "Wisata",
            -7.800269,
            110.366260),

        new Place(
            "Bakpia Pathok 25",
            "Jl. AIP II KS Tubun, Ngampilan",
            "Kuliner",
            -7.793900,
            110.357400),

        new Place(
            "Mie Ayam Bu Tumini",
            "Jl. Imogiri Timur, Umbulharjo",
            "Kuliner",
            -7.819900,
            110.378600),

        new Place(
            "Universitas Gadjah Mada",
            "Bulaksumur, Sleman",
            "Kampus",
            -7.770717,
            110.377724),

        new Place(
            "Universitas Negeri Yogyakarta",
            "Jl. Colombo, Karangmalang, Sleman",
            "Kampus",
            -7.776500,
            110.386700),

        new Place(
            "Stasiun Yogyakarta",
            "Jl. Margo Utomo, Gedong Tengen",
            "Transportasi",
            -7.789173,
            110.363358),

        new Place(
            "Stasiun Lempuyangan",
            "Jl. Lempuyangan, Bausasran",
            "Transportasi",
            -7.790200,
            110.375000),

        new Place(
            "Pasar Beringharjo",
            "Jl. Pabringan, Ngupasan, Yogyakarta",
            "Belanja",
            -7.798700,
            110.365400),

        new Place(
            "Galeria Mall",
            "Jl. Jend. Sudirman, Terban",
            "Belanja",
            -7.782900,
            110.380400),

        new Place(
            "Ambarrukmo Plaza",
            "Jl. Laksda Adisucipto, Caturtunggal",
            "Belanja",
            -7.782800,
            110.401400),

        new Place(
            "Alun-Alun Kidul",
            "Patehan, Kraton, Yogyakarta",
            "Taman",
            -7.811860,
            110.363470),

        new Place(
            "Gembira Loka Zoo",
            "Jl. Kebun Raya, Rejowinangun",
            "Taman",
            -7.806000,
            110.396300),
    ];

    public IReadOnlyList<Place> TempatSorotan =>
        TempatPopuler
            .Where(t =>
                t.Nama is
                    "Malioboro" or
                    "Tugu Yogyakarta" or
                    "Keraton Yogyakarta" or
                    "Universitas Gadjah Mada" or
                    "Stasiun Yogyakarta" or
                    "Taman Sari")
            .ToList();

    private string? _kategoriAktif;

    public string? KategoriAktif
    {
        get => _kategoriAktif;
        private set
        {
            if (!SetProperty(ref _kategoriAktif, value))
                return;

            foreach (var kategori in DaftarKategori)
                kategori.IsAktif = kategori.Nama == value;
        }
    }

    public IReadOnlyList<ModaTransportasi> DaftarModa
    {
        get => _daftarModa;
        private set => SetProperty(ref _daftarModa, value);
    }

    public bool IsMencari
    {
        get => _isMencari;
        private set
        {
            if (SetProperty(ref _isMencari, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public string Notifikasi
    {
        get => _notifikasi;
        private set
        {
            if (SetProperty(ref _notifikasi, value))
                OnPropertyChanged(nameof(AdaNotifikasi));
        }
    }

    public bool AdaNotifikasi =>
        !string.IsNullOrEmpty(Notifikasi);

    public RouteResult? HasilRute
    {
        get => _hasilRute;
        private set => SetProperty(ref _hasilRute, value);
    }

    public IReadOnlyList<TripHistory> RiwayatPerjalanan
    {
        get => _riwayatPerjalanan;
        private set
        {
            if (!SetProperty(ref _riwayatPerjalanan, value))
                return;

            OnPropertyChanged(nameof(TotalPerjalanan));
            OnPropertyChanged(nameof(TotalJarakKm));
            OnPropertyChanged(nameof(TotalEmisiRiwayatKg));
            OnPropertyChanged(nameof(PerjalananTanpaEmisi));
        }
    }

    public string StatusRiwayat
    {
        get => _statusRiwayat;
        private set => SetProperty(ref _statusRiwayat, value);
    }

    public bool IsDatabaseTerhubung =>
        _databaseService is not null;

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

    public bool IsEcoScoreOpen
    {
        get => _isEcoScoreOpen;
        set => SetProperty(ref _isEcoScoreOpen, value);
    }

    // Eco Score dihitung dari riwayat pengguna yang login.
    public int TotalPerjalanan =>
        RiwayatPerjalanan.Count;

    public double TotalJarakKm =>
        RiwayatPerjalanan.Sum(r => r.JarakKm);

    public double TotalEmisiRiwayatKg =>
        RiwayatPerjalanan.Sum(r => r.TotalEmisiKg);

    public int PerjalananTanpaEmisi =>
        RiwayatPerjalanan.Count(
            r => r.TotalEmisiKg <= 0);

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
            if (SetProperty(ref _selectedModa, value) &&
                CurrentState == AppState.StateD_RouteResult)
            {
                HitungUlangEmisi();
            }
        }
    }

    // Kondisi lalu lintas per segmen rute (data live TomTom, atau tebakan dari jam kalau gagal).
    // Bukan input user: user tidak tahu kondisi tiap ruas jalan. null = belum ada rute.
    private KondisiLaluLintas? _laluLintas;
    public KondisiLaluLintas? LaluLintas
    {
        get => _laluLintas;
        private set => SetProperty(ref _laluLintas, value);
    }

    // Ringkasan kondisi lalu lintas di card hasil (State D)
    private string _ringkasanLaluLintas = string.Empty;
    public string RingkasanLaluLintas
    {
        get => _ringkasanLaluLintas;
        private set => SetProperty(ref _ringkasanLaluLintas, value);
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
    public ICommand ToggleEcoScoreCommand { get; }
    public ICommand CariCommand { get; }
    public ICommand MulaiRuteCommand { get; }
    public ICommand TukarCommand { get; }
    public ICommand HitungEmisiCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand TutupCommand { get; }
    public ICommand PilihTempatCommand { get; }
    public ICommand PilihKategoriCommand { get; }

    public MainViewModel(int userId)
    {
        if (userId <= 0)
        {
            throw new ArgumentException(
                "ID pengguna tidak valid.",
                nameof(userId));
        }

        _penggunaAktifId = userId;

        PilihTempatCommand = new RelayCommand(
            ExecutePilihTempat,
            p => p is Place);

        PilihKategoriCommand = new RelayCommand(
            p => KategoriAktif =
                p is string nama && nama != KategoriAktif
                    ? nama
                    : null);

        ToggleMenuCommand =
            new RelayCommand(ExecuteToggleMenu);

        ToggleEcoScoreCommand =
            new RelayCommand(ExecuteToggleEcoScore);

        CariCommand = new RelayCommand(
            ExecuteCari,
            _ => !IsMencari &&
                 !string.IsNullOrWhiteSpace(SearchQuery));

        MulaiRuteCommand =
            new RelayCommand(ExecuteMulaiRute);

        TukarCommand = new RelayCommand(
            _ => (TitikAsal, TitikTujuan) =
                (TitikTujuan, TitikAsal));

        HitungEmisiCommand = new RelayCommand(
            ExecuteHitungEmisi,
            CanExecuteHitungEmisi);

        ResetCommand = new RelayCommand(ExecuteReset);
        TutupCommand = new RelayCommand(ExecuteTutup);
    }

    // ==========================================
    // DATABASE SQLITE
    // ==========================================

    public async Task LoadModaFromDatabaseAsync()
    {
        var database = new SqliteDatabaseService();

        List<ModaTransportasi> modaDariDb =
            await database.GetModaTransportasiAsync();

        if (modaDariDb.Count == 0)
        {
            throw new InvalidOperationException(
                "Data moda transportasi SQLite kosong.");
        }

        // Mengganti ItemsSource membuat ListBox moda mengosongkan pilihannya dan
        // menulis null balik ke SelectedModa lewat binding, jadi pulihkan pilihannya
        string modaSebelumnya = SelectedModa;
        DaftarModa = modaDariDb;
        SelectedModa = modaDariDb.Any(m => m.Nama == modaSebelumnya)
            ? modaSebelumnya
            : modaDariDb[0].Nama;

        _databaseService = database;

        OnPropertyChanged(nameof(IsDatabaseTerhubung));

        await MuatRiwayatAsync();
    }

    private async Task MuatRiwayatAsync()
    {
        if (_databaseService is null)
            return;

        try
        {
            RiwayatPerjalanan =
                await _databaseService.GetTripHistoryAsync(
                    _penggunaAktifId);

            StatusRiwayat =
                RiwayatPerjalanan.Count == 0
                    ? "Belum ada perjalanan. Hitung emisi rute pertamamu!"
                    : $"{RiwayatPerjalanan.Count} perjalanan · total " +
                      $"{RiwayatPerjalanan.Sum(r => r.TotalEmisiKg):0.00} kg CO₂";
        }
        catch (Exception ex)
        {
            StatusRiwayat =
                $"Gagal memuat riwayat: {ex.Message}";
        }
    }

    private async Task SimpanPerjalananAsync(RouteResult rute)
    {
        if (_databaseService is null)
        {
            Notifikasi =
                "Database belum siap. Perjalanan tidak tersimpan.";
            return;
        }

        try
        {
            await _databaseService.SaveTripAsync(
                _penggunaAktifId,
                rute.Asal.Nama,
                rute.Tujuan.Nama,
                rute.Asal.Lat,
                rute.Asal.Lng,
                rute.Tujuan.Lat,
                rute.Tujuan.Lng,
                rute.JarakKm,
                EstimasiWaktuMenit,
                SelectedModa,
                EmisiKarbonKg);

            await MuatRiwayatAsync();
        }
        catch (Exception ex)
        {
            Notifikasi =
                $"Hasil tidak tersimpan ke riwayat: {ex.Message}";
        }
    }

    private async void ExecuteToggleMenu(object? parameter)
    {
        bool buka = !IsMenuOpen;

        IsEcoScoreOpen = false;
        IsMenuOpen = buka;

        if (buka)
            await MuatRiwayatAsync();
    }

    private async void ExecuteToggleEcoScore(object? parameter)
    {
        bool buka = !IsEcoScoreOpen;

        IsMenuOpen = false;
        IsEcoScoreOpen = buka;

        if (buka)
            await MuatRiwayatAsync();
    }

    // ==========================================
    // PENCARIAN TEMPAT (Nominatim)
    // ==========================================

    private async void ExecuteCari(object? parameter)
    {
        Notifikasi = string.Empty;
        IsMencari = true;

        try
        {
            IReadOnlyList<Place> hasil =
                await _routingService.CariTempatAsync(
                    SearchQuery);

            SelectedPlace = hasil[0];

            CurrentState =
                AppState.StateB_SearchLocation;
        }
        catch (Exception ex)
        {
            Notifikasi = ex is RoutingException
                ? ex.Message
                : $"Pencarian gagal: {ex.Message}";
        }
        finally
        {
            IsMencari = false;
        }
    }

    [GeneratedRegex(
        @"^\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*$")]
    private static partial Regex PolaKoordinat();

    private async Task<Place> CariKoordinatAsync(
        string teks,
        string label)
    {
        teks = teks.Trim();

        Match match = PolaKoordinat().Match(teks);

        if (match.Success)
        {
            double lat = double.Parse(
                match.Groups[1].Value,
                CultureInfo.InvariantCulture);

            double lng = double.Parse(
                match.Groups[2].Value,
                CultureInfo.InvariantCulture);

            if (lat is < -90 or > 90 ||
                lng is < -180 or > 180)
            {
                throw new RoutingException(
                    $"Koordinat {label} tidak valid.");
            }

            return new Place(
                "Titik di peta",
                teks,
                "Koordinat",
                lat,
                lng);
        }

        if (SelectedPlace is not null &&
            string.Equals(
                SelectedPlace.Nama,
                teks,
                StringComparison.OrdinalIgnoreCase))
        {
            return SelectedPlace;
        }

        Place? populer = TempatPopuler.FirstOrDefault(
            p => string.Equals(
                p.Nama,
                teks,
                StringComparison.OrdinalIgnoreCase));

        if (populer is not null)
            return populer;

        try
        {
            IReadOnlyList<Place> hasil =
                await _routingService.CariTempatAsync(
                    teks,
                    limit: 1);

            return hasil[0] with { Nama = teks };
        }
        catch (RoutingException ex)
        {
            throw new RoutingException(
                $"{label}: {ex.Message}",
                ex);
        }
    }

    private void ExecutePilihTempat(object? parameter)
    {
        if (parameter is not Place place)
            return;

        SearchQuery = place.Nama;
        SelectedPlace = place;

        CurrentState =
            AppState.StateB_SearchLocation;
    }

    private void ExecuteMulaiRute(object? parameter)
    {
        if (SelectedPlace is not null)
            TitikTujuan = SelectedPlace.Nama;

        CurrentState =
            AppState.StateC_InputRoute;
    }

    // ==========================================
    // PERHITUNGAN RUTE DAN EMISI
    // ==========================================

    private async void ExecuteHitungEmisi(object? parameter)
    {
        ErrorMessage = string.Empty;
        CurrentState = AppState.StateL_Loading;

        Notifikasi = string.Empty;
        HasilRute = null;

        try
        {
            if (string.Equals(
                TitikAsal.Trim(),
                TitikTujuan.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new RoutingException(
                    "Titik awal dan tujuan tidak boleh sama.");
            }

            Place asal = await CariKoordinatAsync(
                TitikAsal,
                "Titik awal");

            Place tujuan = await CariKoordinatAsync(
                TitikTujuan,
                "Tujuan");

            if (Math.Abs(asal.Lat - tujuan.Lat) < 0.0002 &&
                Math.Abs(asal.Lng - tujuan.Lng) < 0.0002)
            {
                throw new RoutingException(
                    "Titik awal dan tujuan berada di lokasi yang sama.");
            }

            RouteResult rute =
                await _routingService.HitungRuteAsync(
                    asal,
                    tujuan);

            await AmbilLaluLintasAsync(rute);

            JarakKm = rute.JarakKm;
            HasilRute = rute;

            HitungUlangEmisi();

            CurrentState =
                AppState.StateD_RouteResult;

            await SimpanPerjalananAsync(rute);
        }
        catch (RoutingException ex)
        {
            ErrorMessage = ex.Message;
            CurrentState = AppState.StateE_Error;
        }
        catch (Exception ex)
        {
            ErrorMessage =
                $"Terjadi kesalahan: {ex.Message}";

            CurrentState = AppState.StateE_Error;
        }
    }

    // Kemacetan per segmen dari TomTom. Gagal / tanpa key -> tebakan dari jam
    // (tidak dianggap error, karena hasil emisi tetap bisa dihitung).
    private async Task AmbilLaluLintasAsync(RouteResult rute)
    {
        KondisiLaluLintas? live = await _trafficService.AmbilKondisiAsync(rute);
        if (live is not null)
        {
            LaluLintas = live;
            RingkasanLaluLintas = BuatRingkasanLive(live);
            return;
        }

        LaluLintas = KondisiLaluLintas.Tebakan(rute.JarakKm, rute.Titik.Count, DateTime.Now);
        string tingkat = LaluLintas.TingkatRataRata.ToString();

        if (_trafficService.AdaApiKey)
        {
            RingkasanLaluLintas = $"{tingkat} (ditebak dari jam, data TomTom tidak tersedia)";
            _ = TampilkanNotifikasiSementaraAsync("Data lalu lintas TomTom tidak bisa diambil. Kemacetan sementara ditebak dari jam.");
        }
        else
        {
            RingkasanLaluLintas = $"{tingkat} (ditebak dari jam, API key TomTom belum diatur)";
        }
    }

    private static string BuatRingkasanLive(KondisiLaluLintas kondisi)
    {
        string ringkasan = $"{kondisi.TingkatRataRata}: kecepatan rata-rata {kondisi.RasioRataRata:P0} dari kondisi normal.";
        double tersendat = kondisi.JarakTersendatKm;
        if (tersendat >= 0.05)
            ringkasan += $" Ruas padat/macet {tersendat:0.0} km dari {kondisi.JarakTotalKm:0.0} km.";
        return ringkasan + " (live TomTom)";
    }

    // Notif singkat di bawah peta, hilang sendiri setelah beberapa detik
    private async Task TampilkanNotifikasiSementaraAsync(string pesan)
    {
        Notifikasi = pesan;
        await Task.Delay(TimeSpan.FromSeconds(6));
        if (Notifikasi == pesan)
            Notifikasi = string.Empty;
    }

    private bool CanExecuteHitungEmisi(object? parameter)
    {
        return CurrentState != AppState.StateL_Loading &&
               !string.IsNullOrWhiteSpace(TitikAsal) &&
               !string.IsNullOrWhiteSpace(TitikTujuan);
    }

    private void HitungUlangEmisi()
    {
        // Emisi tiap moda dihitung per segmen sesuai kondisi lalu lintasnya masing-masing
        var emisiPerModa = DaftarModa
            .Select(m => (
                Moda: m,
                Emisi: LaluLintas?.HitungEmisi(m) ?? m.HitungEmisi(JarakKm)))
            .ToList();

        double emisiMax =
            emisiPerModa.Max(x => x.Emisi);

        double emisiMin =
            emisiPerModa.Min(x => x.Emisi);

        PerbandinganModa = emisiPerModa
            .Select(x => new PerbandinganEmisi(
                x.Moda.Nama,
                x.Moda.Ikon,
                x.Emisi,
                emisiMax > 0
                    ? x.Emisi / emisiMax
                    : 0,
                x.Moda.Nama == SelectedModa,
                x.Emisi == emisiMin))
            .ToList();

        EmisiKarbonKg = emisiPerModa
            .FirstOrDefault(
                x => x.Moda.Nama == SelectedModa)
            .Emisi;

        if (HasilRute is not null)
        {
            EstimasiWaktuMenit =
                RoutingService.EstimasiWaktuMenit(
                    SelectedModa,
                    HasilRute,
                    LaluLintas?.PengaliWaktu ?? 1.0);
        }

        InsightText = BuatInsight(emisiPerModa);
    }

    private string BuatInsight(
        List<(ModaTransportasi Moda, double Emisi)> emisiPerModa)
    {
        if (EmisiKarbonKg == 0)
        {
            return "Perjalanan ini tanpa emisi karbon. Mantap!";
        }

        string insightModa =
            BuatInsightModa(emisiPerModa);

        ModaTransportasi? moda =
            DaftarModa.FirstOrDefault(
                m => m.Nama == SelectedModa);

        // Tambahan emisi akibat kemacetan saat ini dibanding kalau semua ruas lancar
        if (moda is not KendaraanBermotor
            {
                TerpengaruhKemacetan: true
            })
        {
            return insightModa;
        }

        double tambahan =
            EmisiKarbonKg -
            moda.HitungEmisi(JarakKm);

        if (tambahan < 0.005)
            return insightModa;

        return $"{insightModa} Kemacetan saat ini " +
               $"menambah {tambahan:0.00} kg CO₂ " +
               "dibanding kalau semua jalan lancar.";
    }

    private string BuatInsightModa(
        List<(ModaTransportasi Moda, double Emisi)> emisiPerModa)
    {
        var alternatif = emisiPerModa
            .Where(x =>
                x.Moda is KendaraanBermotor &&
                x.Emisi < EmisiKarbonKg)
            .OrderBy(x => x.Emisi)
            .FirstOrDefault();

        return alternatif.Moda is null
            ? $"{SelectedModa} sudah jadi kendaraan " +
              "bermotor paling hemat untuk rute ini."
            : $"Naik {alternatif.Moda.Nama} menghemat " +
              $"{EmisiKarbonKg - alternatif.Emisi:0.00} " +
              $"kg CO₂ dibanding {SelectedModa}.";
    }

    // ==========================================
    // RESET DAN NAVIGASI
    // ==========================================

    private void ExecuteReset(object? parameter)
    {
        TitikAsal = string.Empty;
        TitikTujuan = string.Empty;
        ErrorMessage = string.Empty;
        HasilRute = null;
        ResetLaluLintas();

        CurrentState =
            AppState.StateC_InputRoute;
    }

    private void ExecuteTutup(object? parameter)
    {
        IsMenuOpen = false;
        IsEcoScoreOpen = false;

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
        Notifikasi = string.Empty;

        HasilRute = null;
        ResetLaluLintas();

        CurrentState =
            AppState.StateA_Idle;
    }

    private void ResetLaluLintas()
    {
        LaluLintas = null;
        RingkasanLaluLintas = string.Empty;
    }
}
