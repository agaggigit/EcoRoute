
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
    private readonly TransJogjaService _transJogja;
    private readonly Func<double, double, CancellationToken, Task<Place?>> _cariNamaTitik;
    private DatabaseService? _databaseService;

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

    // Rute jalan raya, sepeda, dan jalan kaki dari Hitung Emisi terakhir. null = belum ada rute.
    private RuteSemuaModa? _ruteSemuaModa;

    // Rute Bus lewat trayek Trans Jogja langsung. null = tidak ada trayek langsung (Bus ikut jalan raya).
    private RouteResult? _ruteBus;

    // Nilai cadangan sebelum data moda berhasil dimuat dari database.
    private IReadOnlyList<ModaTransportasi> _daftarModa =
    [
        new KendaraanBermotor("Mobil", "\uE804", 0.1920, 1.35),
        new KendaraanBermotor("Motor", "\U0001F6F5", 0.1030, 1.15),
        new KendaraanBermotor("Bus", "\uE806", 0.0890, 1.25),
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
        private set
        {
            if (SetProperty(ref _daftarModa, value))
                HitungUlangEcoScore();
        }
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
            if (!SetProperty(ref _notifikasi, value))
                return;

            // Notifikasi biasa = peringatan (merah); hanya TampilkanSuksesAsync yang hijau
            if (!_aturNotifikasiSukses)
                IsNotifikasiSukses = false;

            OnPropertyChanged(nameof(AdaNotifikasi));
        }
    }

    private bool _aturNotifikasiSukses;
    private bool _isNotifikasiSukses;

    public bool IsNotifikasiSukses
    {
        get => _isNotifikasiSukses;
        private set => SetProperty(ref _isNotifikasiSukses, value);
    }

    // Notif hijau singkat (misalnya perjalanan berhasil dicatat), hilang sendiri setelah beberapa detik
    private async Task TampilkanSuksesAsync(string pesan)
    {
        _aturNotifikasiSukses = true;
        IsNotifikasiSukses = true;
        Notifikasi = pesan;
        _aturNotifikasiSukses = false;

        await Task.Delay(TimeSpan.FromSeconds(4));
        if (Notifikasi == pesan)
            Notifikasi = string.Empty;
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
            if (SetProperty(ref _riwayatPerjalanan, value))
                HitungUlangEcoScore();
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
            if (!SetProperty(ref _currentState, value))
                return;

            CommandManager.InvalidateRequerySuggested();
            TutupSemuaSaran();
        }
    }

    // Panel Riwayat (IsMenuOpen) dan Eco Score tampil di posisi card kiri, menggantikan card
    // Jelajah selama dibuka. Isi card Jelajah (pencarian, rute, hasil) tetap tersimpan.
    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        set
        {
            if (SetProperty(ref _isMenuOpen, value))
                OnPropertyChanged(nameof(IsJelajahAktif));
        }
    }

    public bool IsEcoScoreOpen
    {
        get => _isEcoScoreOpen;
        set
        {
            if (SetProperty(ref _isEcoScoreOpen, value))
                OnPropertyChanged(nameof(IsJelajahAktif));
        }
    }

    // true = card Jelajah yang tampil (tidak ada panel Riwayat / Eco Score yang terbuka)
    public bool IsJelajahAktif => !IsMenuOpen && !IsEcoScoreOpen;

    // ==========================================
    // ECO SCORE (dihitung dari riwayat pengguna yang login, lihat Models/EcoScoreCalculator.cs)
    // ==========================================
    private EcoScoreRingkasan _ecoScore = EcoScoreRingkasan.Kosong;

    public EcoScoreRingkasan EcoScore
    {
        get => _ecoScore;
        private set
        {
            if (!SetProperty(ref _ecoScore, value))
                return;

            OnPropertyChanged(nameof(TotalPerjalanan));
            OnPropertyChanged(nameof(TotalJarakKm));
            OnPropertyChanged(nameof(TotalEmisiRiwayatKg));
            OnPropertyChanged(nameof(PerjalananTanpaEmisi));
        }
    }

    public int TotalPerjalanan => EcoScore.TotalPerjalanan;

    public double TotalJarakKm => EcoScore.TotalJarakKm;

    public double TotalEmisiRiwayatKg => EcoScore.TotalEmisiKg;

    public int PerjalananTanpaEmisi => EcoScore.PerjalananTanpaEmisi;

    // Dipanggil saat riwayat atau daftar moda (faktor emisi) berubah
    private void HitungUlangEcoScore()
    {
        EcoScore = new EcoScoreCalculator(DaftarModa).Hitung(RiwayatPerjalanan);
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
        set
        {
            if (!SetProperty(ref _titikAsal, value))
                return;

            // Teks titik awal diubah -> pratinjau lama tidak berlaku lagi (penanda di peta hilang)
            if (PratinjauAsal is not null &&
                !string.Equals(
                    (value ?? string.Empty).Trim(),
                    _teksPratinjauAsal,
                    StringComparison.OrdinalIgnoreCase))
            {
                PratinjauAsal = null;
            }
        }
    }

    // ==========================================
    // PRATINJAU TITIK AWAL (Enter di kolom titik awal / klik peta)
    // ==========================================
    private Place? _pratinjauAsal;
    private string _teksPratinjauAsal = string.Empty;

    // Lokasi titik awal yang sudah dicari dan ditandai di peta. null = belum ada pratinjau.
    public Place? PratinjauAsal
    {
        get => _pratinjauAsal;
        private set
        {
            if (SetProperty(ref _pratinjauAsal, value) && value is null)
                _teksPratinjauAsal = string.Empty;
        }
    }

    public ICommand PratinjauAsalCommand { get; }

    // Cari lokasi titik awal (koordinat, tempat populer, atau Nominatim) lalu tandai di peta
    public async Task PratinjauAsalAsync()
    {
        string teks = TitikAsal.Trim();
        if (teks.Length == 0 || IsMencari)
            return;

        IsMencari = true;
        try
        {
            Place asal = await CariKoordinatAsync(teks, "Titik awal");

            // Teks bisa saja berubah selama pencarian berjalan
            if (!string.Equals(TitikAsal.Trim(), teks, StringComparison.OrdinalIgnoreCase))
                return;

            _teksPratinjauAsal = teks;
            PratinjauAsal = asal;
            Notifikasi = string.Empty;
            SetelahKolomTerisi(KolomRute.Asal);
        }
        catch (Exception ex)
        {
            PratinjauAsal = null;
            _ = TampilkanNotifikasiSementaraAsync(
                ex is RoutingException
                    ? ex.Message
                    : $"Titik awal tidak ditemukan: {ex.Message}");
        }
        finally
        {
            IsMencari = false;
        }
    }

    // Dipanggil MapView saat peta diklik di State C: koordinat langsung jadi titik awal + pratinjau
    public void PilihAsalDariPeta(double lat, double lng)
    {
        string teks = string.Create(
            CultureInfo.InvariantCulture,
            $"{lat:F6}, {lng:F6}");

        // Teks pratinjau diisi dulu supaya setter TitikAsal tidak menghapus penanda sesaat
        _teksPratinjauAsal = teks;
        TitikAsal = teks;
        PratinjauAsal = new Place("Titik di peta", teks, "Koordinat", lat, lng);
    }

    // ==========================================
    // KOLOM AKTIF + KLIK PETA
    // ==========================================
    // Klik peta mengisi kolom yang aktif (yang terakhir difokus). Setelah titik awal terisi,
    // kolom aktif pindah ke tujuan supaya klik berikutnya tidak mengubah titik awal lagi.
    private KolomRute _kolomAktif = KolomRute.Asal;

    public KolomRute KolomAktif
    {
        get => _kolomAktif;
        set
        {
            if (!SetProperty(ref _kolomAktif, value))
                return;

            OnPropertyChanged(nameof(IsAsalAktif));
            OnPropertyChanged(nameof(IsTujuanAktif));
            OnPropertyChanged(nameof(PetunjukKlikPeta));
        }
    }

    public bool IsAsalAktif => KolomAktif == KolomRute.Asal;
    public bool IsTujuanAktif => KolomAktif == KolomRute.Tujuan;

    public string PetunjukKlikPeta => KolomAktif == KolomRute.Asal
        ? "Klik peta untuk memilih titik awal"
        : "Klik peta untuk memilih tujuan";

    // Titik yang diklik dianggap tempat populer kalau sejauh ini (km) atau kurang
    public const double JarakTempatPopulerKm = 0.075;

    // Dipanggil MapView saat peta diklik. State A/B: titik jadi lokasi terpilih (kartu tempat).
    // State C/D/E: mengisi kolom aktif. Nama tempatnya dicari menyusul (reverse geocoding).
    public async Task PilihTitikDariPetaAsync(double lat, double lng)
    {
        switch (CurrentState)
        {
            case AppState.StateA_Idle or AppState.StateB_SearchLocation:
                await PilihTempatDariPetaAsync(lat, lng);
                break;

            case AppState.StateC_InputRoute or AppState.StateD_RouteResult or AppState.StateE_Error:
                await IsiKolomDariPetaAsync(lat, lng);
                break;
        }
    }

    private async Task PilihTempatDariPetaAsync(double lat, double lng)
    {
        var sementara = new Place("Titik di peta", "Mencari nama tempat...", "Titik di peta", lat, lng);

        TutupPanel();
        SearchQuery = string.Empty;
        SelectedPlace = sementara;
        CurrentState = AppState.StateB_SearchLocation;

        Place? hasil = await CariNamaTitikAsync(lat, lng);

        // User sudah memilih tempat lain selama nama dicari
        if (!ReferenceEquals(SelectedPlace, sementara))
            return;

        SelectedPlace = hasil ?? sementara with { Alamat = TeksKoordinat(lat, lng) };
        SearchQuery = hasil?.Nama ?? string.Empty;
    }

    private async Task IsiKolomDariPetaAsync(double lat, double lng)
    {
        KolomRute kolom = KolomAktif;
        string teksKoordinat = TeksKoordinat(lat, lng);

        if (kolom == KolomRute.Asal)
            PilihAsalDariPeta(lat, lng);
        else
            PilihTujuanDariPeta(lat, lng);

        PindahKolomSetelahTerisi(kolom);

        Place? hasil = await CariNamaTitikAsync(lat, lng);

        // Ganti teks koordinat dengan nama tempat, kecuali user sudah mengubah kolomnya
        if (hasil is not null)
        {
            if (kolom == KolomRute.Asal && TitikAsal == teksKoordinat)
            {
                _teksPratinjauAsal = hasil.Nama;
                TitikAsal = hasil.Nama;
                PratinjauAsal = hasil;
            }
            else if (kolom == KolomRute.Tujuan && TitikTujuan == teksKoordinat)
            {
                TitikTujuan = hasil.Nama;
                TujuanTerpilih = hasil;
            }
        }

        HitungUlangOtomatis();
    }

    public void PilihTujuanDariPeta(double lat, double lng)
    {
        string teks = TeksKoordinat(lat, lng);
        TitikTujuan = teks;
        TujuanTerpilih = new Place("Titik di peta", teks, "Koordinat", lat, lng);
    }

    private static string TeksKoordinat(double lat, double lng) =>
        string.Create(CultureInfo.InvariantCulture, $"{lat:F6}, {lng:F6}");

    // Tempat populer terdekat (kalau dalam 75 m) atau nama dari Nominatim. Gagal -> null (koordinat tetap dipakai).
    private async Task<Place?> CariNamaTitikAsync(double lat, double lng)
    {
        Place? populer = TempatPopuler
            .Select(p => (Tempat: p, Km: TransJogjaService.JarakKm(lat, lng, p.Lat, p.Lng)))
            .Where(x => x.Km <= JarakTempatPopulerKm)
            .OrderBy(x => x.Km)
            .Select(x => x.Tempat)
            .FirstOrDefault();

        if (populer is not null)
            return populer;

        try
        {
            return await _cariNamaTitik(lat, lng, CancellationToken.None);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Kolom terisi (klik peta, Enter, atau saran) -> pindah kolom aktif, lalu hitung ulang kalau hasil sudah tampil
    private void SetelahKolomTerisi(KolomRute kolom)
    {
        PindahKolomSetelahTerisi(kolom);
        HitungUlangOtomatis();
    }

    private void PindahKolomSetelahTerisi(KolomRute kolom)
    {
        if (kolom == KolomRute.Asal && string.IsNullOrWhiteSpace(TitikTujuan))
            KolomAktif = KolomRute.Tujuan;
        else if (kolom == KolomRute.Tujuan && string.IsNullOrWhiteSpace(TitikAsal))
            KolomAktif = KolomRute.Asal;
    }

    // Hasil (State D) atau error (State E) sedang tampil dan lokasi diganti -> langsung hitung ulang
    private void HitungUlangOtomatis()
    {
        if (CurrentState is AppState.StateD_RouteResult or AppState.StateE_Error &&
            HitungEmisiCommand.CanExecute(null))
        {
            HitungEmisiCommand.Execute(null);
        }
    }

    // ==========================================
    // PRATINJAU TUJUAN (Enter di kolom tujuan)
    // ==========================================
    public ICommand PratinjauTujuanCommand { get; }

    public async Task PratinjauTujuanAsync()
    {
        string teks = TitikTujuan.Trim();
        if (teks.Length == 0 || IsMencari)
            return;

        IsMencari = true;
        try
        {
            Place tujuan = await CariKoordinatAsync(teks, "Tujuan");

            if (!string.Equals(TitikTujuan.Trim(), teks, StringComparison.OrdinalIgnoreCase))
                return;

            TujuanTerpilih = tujuan with { Nama = teks };
            Notifikasi = string.Empty;
            SetelahKolomTerisi(KolomRute.Tujuan);
        }
        catch (Exception ex)
        {
            TujuanTerpilih = null;
            _ = TampilkanNotifikasiSementaraAsync(
                ex is RoutingException
                    ? ex.Message
                    : $"Tujuan tidak ditemukan: {ex.Message}");
        }
        finally
        {
            IsMencari = false;
        }
    }

    public string TitikTujuan
    {
        get => _titikTujuan;
        set
        {
            if (!SetProperty(ref _titikTujuan, value))
                return;

            // Teks tujuan diubah -> tempat yang dipilih dari saran tidak berlaku lagi
            if (TujuanTerpilih is not null &&
                !string.Equals(
                    (value ?? string.Empty).Trim(),
                    TujuanTerpilih.Nama,
                    StringComparison.OrdinalIgnoreCase))
            {
                TujuanTerpilih = null;
            }
        }
    }

    // ==========================================
    // SARAN LOKASI SAAT MENGETIK (search bar, titik awal, tujuan)
    // ==========================================
    // Daftar saran tiap kolom. View memanggil PerbaruiAsync saat user mengetik (lihat SaranLokasiBehavior).
    public SaranLokasiViewModel SaranCari { get; }
    public SaranLokasiViewModel SaranAsal { get; }
    public SaranLokasiViewModel SaranTujuan { get; }

    // Tujuan yang dipilih dari saran: dipakai langsung saat Hitung Emisi tanpa dicari ulang
    private Place? _tujuanTerpilih;
    public Place? TujuanTerpilih
    {
        get => _tujuanTerpilih;
        private set => SetProperty(ref _tujuanTerpilih, value);
    }

    // Saran titik awal dipilih -> langsung jadi pratinjau (penanda Titik Asal + peta bergeser)
    private void PilihSaranAsal(Place place)
    {
        _teksPratinjauAsal = place.Nama;
        TitikAsal = place.Nama;
        PratinjauAsal = place;
        Notifikasi = string.Empty;
        SetelahKolomTerisi(KolomRute.Asal);
    }

    private void PilihSaranTujuan(Place place)
    {
        TitikTujuan = place.Nama;
        TujuanTerpilih = place;
        SetelahKolomTerisi(KolomRute.Tujuan);
    }

    private void TutupSemuaSaran()
    {
        // Bisa terpanggil dari setter CurrentState sebelum konstruktor selesai membuat daftar saran
        SaranCari?.Tutup();
        SaranAsal?.Tutup();
        SaranTujuan?.Tutup();
    }

    // Tukar titik awal & tujuan, termasuk lokasi yang sudah dipilih / dipratinjau
    private void ExecuteTukar(object? parameter)
    {
        Place? asalLama = PratinjauAsal;
        Place? tujuanLama = TujuanTerpilih;

        (TitikAsal, TitikTujuan) = (TitikTujuan, TitikAsal);

        if (tujuanLama is not null)
        {
            _teksPratinjauAsal = TitikAsal.Trim();
            PratinjauAsal = tujuanLama;
        }

        if (asalLama is not null)
            TujuanTerpilih = asalLama with { Nama = TitikTujuan.Trim() };

        TutupSemuaSaran();
    }

    public string SelectedModa
    {
        get => _selectedModa;
        set
        {
            // Ganti moda saat hasil sudah tampil -> rute di peta & jarak ikut moda baru, lalu hitung ulang
            if (SetProperty(ref _selectedModa, value) &&
                CurrentState == AppState.StateD_RouteResult)
            {
                TampilkanRuteModaTerpilih();
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
    // Ringkasan kemacetan rute jalan raya; card memakai ringkasan jalur bus kalau Bus lewat trayek
    private string _ringkasanJalanRaya = string.Empty;
    public string RingkasanLaluLintas
    {
        get => _ringkasanLaluLintas;
        private set => SetProperty(ref _ringkasanLaluLintas, value);
    }

    // Keterangan trayek di card hasil saat moda Bus dipilih (kosong untuk moda lain)
    private string _keteranganBus = string.Empty;
    public string KeteranganBus
    {
        get => _keteranganBus;
        private set
        {
            if (SetProperty(ref _keteranganBus, value))
                OnPropertyChanged(nameof(AdaKeteranganBus));
        }
    }

    public bool AdaKeteranganBus => !string.IsNullOrEmpty(KeteranganBus);

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
    public ICommand BukaJelajahCommand { get; }
    public ICommand CariCommand { get; }
    public ICommand MulaiRuteCommand { get; }
    public ICommand TukarCommand { get; }
    public ICommand HitungEmisiCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand TutupCommand { get; }
    public ICommand PilihTempatCommand { get; }
    public ICommand PilihKategoriCommand { get; }

    public MainViewModel(
        int userId,
        TransJogjaService? transJogja = null,
        Func<string, CancellationToken, Task<IReadOnlyList<Place>>>? cariSaranOnline = null,
        TimeSpan? jedaSaran = null,
        Func<double, double, CancellationToken, Task<Place?>>? cariNamaTitik = null)
    {
        if (userId <= 0)
        {
            throw new ArgumentException(
                "ID pengguna tidak valid.",
                nameof(userId));
        }

        _penggunaAktifId = userId;
        _transJogja = transJogja ?? TransJogjaService.Default;
        _cariNamaTitik = cariNamaTitik ?? ((lat, lng, ct) => _routingService.CariNamaTitikAsync(lat, lng, ct));

        Func<string, CancellationToken, Task<IReadOnlyList<Place>>> online =
            cariSaranOnline ?? ((teks, ct) => _routingService.SaranTempatAsync(teks, ct));

        SaranCari = new SaranLokasiViewModel(TempatPopuler, online, ExecutePilihTempat, jedaSaran);
        SaranAsal = new SaranLokasiViewModel(TempatPopuler, online, PilihSaranAsal, jedaSaran);
        SaranTujuan = new SaranLokasiViewModel(TempatPopuler, online, PilihSaranTujuan, jedaSaran);

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

        // Tombol Jelajah di sidebar: cukup tutup panel, rute yang sedang tampil tidak di-reset
        BukaJelajahCommand =
            new RelayCommand(_ => TutupPanel());

        CariCommand = new RelayCommand(
            ExecuteCari,
            _ => !IsMencari &&
                 !string.IsNullOrWhiteSpace(SearchQuery));

        MulaiRuteCommand =
            new RelayCommand(ExecuteMulaiRute);

        TukarCommand = new RelayCommand(ExecuteTukar);

        HitungEmisiCommand = new RelayCommand(
            ExecuteHitungEmisi,
            CanExecuteHitungEmisi);

        PratinjauAsalCommand = new RelayCommand(
            async _ => await PratinjauAsalAsync(),
            _ => !IsMencari &&
                 CurrentState != AppState.StateL_Loading &&
                 !string.IsNullOrWhiteSpace(TitikAsal));

        PratinjauTujuanCommand = new RelayCommand(
            async _ => await PratinjauTujuanAsync(),
            _ => !IsMencari &&
                 CurrentState != AppState.StateL_Loading &&
                 !string.IsNullOrWhiteSpace(TitikTujuan));

        CatatPerjalananCommand = new RelayCommand(
            ExecuteCatatPerjalanan,
            _ => CurrentState == AppState.StateD_RouteResult && !IsPerjalananTercatat);

        UlangiPerjalananCommand = new RelayCommand(
            ExecuteUlangiPerjalanan,
            p => p is TripHistory { AdaKoordinat: true } && CurrentState != AppState.StateL_Loading);

        PilihModaCommand = new RelayCommand(p =>
        {
            if (p is string nama)
                SelectedModa = nama;
        });

        // Properti turunan untuk card hasil
        PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(RingkasanLaluLintas):
                    OnPropertyChanged(nameof(KeteranganKemacetan));
                    break;
                case nameof(KeteranganBus):
                    OnPropertyChanged(nameof(KeteranganBusSingkat));
                    break;
                case nameof(SelectedModa):
                    OnPropertyChanged(nameof(IsModaRekomendasi));
                    break;
                case nameof(EstimasiWaktuMenit):
                    OnPropertyChanged(nameof(WaktuTampil));
                    break;
                case nameof(RiwayatPerjalanan):
                    OnPropertyChanged(nameof(PerjalananTerakhir));
                    OnPropertyChanged(nameof(AdaPerjalananTerakhir));
                    break;
                case nameof(EcoScore):
                    OnPropertyChanged(nameof(TahapanLevel));
                    break;
            }
        };

        ResetCommand = new RelayCommand(ExecuteReset);
        TutupCommand = new RelayCommand(ExecuteTutup);
    }

    // ==========================================
    // DATABASE (PostgreSQL cloud)
    // ==========================================

    public async Task LoadModaFromDatabaseAsync()
    {
        var database = new DatabaseService();

        // Hanya moda yang punya jalur (lihat RoutingService.ProfilUntuk). Kereta sudah dihapus,
        // tapi barisnya bisa masih ada di database lama karena dipakai riwayat perjalanan.
        List<ModaTransportasi> modaDariDb =
            (await database.GetModaTransportasiAsync())
            .Where(m => RoutingService.ProfilUntuk(m.Nama) is not null)
            .ToList();

        if (modaDariDb.Count == 0)
        {
            throw new InvalidOperationException(
                "Data moda transportasi di database kosong.");
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
                $"Gagal memuat riwayat: {DatabaseConfig.PesanRamah(ex)}";
        }
    }

    private async Task<bool> SimpanPerjalananAsync(RouteResult rute)
    {
        if (_databaseService is null)
        {
            Notifikasi =
                "Database belum siap. Perjalanan tidak tersimpan.";
            return false;
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
            return true;
        }
        catch (Exception ex)
        {
            Notifikasi =
                $"Hasil tidak tersimpan ke riwayat: {DatabaseConfig.PesanRamah(ex)}";
            return false;
        }
    }

    // ==========================================
    // CATAT PERJALANAN ("Pakai rute ini")
    // ==========================================
    // Hitung Emisi hanya mencari rute. Perjalanan baru masuk Riwayat dan Eco Score saat user
    // menekan "Pakai rute ini", supaya mencoba-coba rute tidak ikut dihitung sebagai poin.
    private bool _isPerjalananTercatat;

    public bool IsPerjalananTercatat
    {
        get => _isPerjalananTercatat;
        private set
        {
            if (SetProperty(ref _isPerjalananTercatat, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand CatatPerjalananCommand { get; }

    private async void ExecuteCatatPerjalanan(object? parameter)
    {
        if (HasilRute is null || IsPerjalananTercatat)
            return;

        int poinSebelum = EcoScore.Poin;
        IsPerjalananTercatat = true;

        if (!await SimpanPerjalananAsync(HasilRute))
        {
            IsPerjalananTercatat = false;
            return;
        }

        int tambahan = EcoScore.Poin - poinSebelum;
        _ = TampilkanSuksesAsync(tambahan > 0
            ? $"Perjalanan tercatat · +{tambahan} poin Eco Score"
            : "Perjalanan tercatat di riwayat");
    }

    // ==========================================
    // ULANGI PERJALANAN DARI RIWAYAT
    // ==========================================
    // Rute yang sama (koordinat asal & tujuan + moda) dihitung ulang dengan kondisi lalu lintas saat ini
    public ICommand UlangiPerjalananCommand { get; }

    public IReadOnlyList<TripHistory> PerjalananTerakhir =>
        RiwayatPerjalanan.Where(t => t.AdaKoordinat).Take(3).ToList();

    public bool AdaPerjalananTerakhir => PerjalananTerakhir.Count > 0;

    private void ExecuteUlangiPerjalanan(object? parameter)
    {
        if (parameter is not TripHistory trip || !trip.AdaKoordinat)
            return;

        TutupPanel();
        SearchQuery = string.Empty;
        SelectedPlace = null;

        // State diubah dulu supaya mengganti moda tidak memicu hitung ulang hasil lama
        CurrentState = AppState.StateC_InputRoute;
        if (DaftarModa.Any(m => m.Nama == trip.ModaDipilih))
            SelectedModa = trip.ModaDipilih;

        var asal = new Place(trip.NamaAsal, "Dari riwayat", "Riwayat", trip.LatAsal, trip.LngAsal);
        _teksPratinjauAsal = asal.Nama;
        TitikAsal = asal.Nama;
        PratinjauAsal = asal;

        TitikTujuan = trip.NamaTujuan;
        TujuanTerpilih = new Place(trip.NamaTujuan, "Dari riwayat", "Riwayat", trip.LatTujuan, trip.LngTujuan);
        KolomAktif = KolomRute.Asal;

        if (HitungEmisiCommand.CanExecute(null))
            HitungEmisiCommand.Execute(null);
    }

    // Tahapan level Eco Score (Bibit -> Tunas -> Pohon -> Hutan) untuk panel Eco Score
    public IReadOnlyList<TahapLevel> TahapanLevel =>
        Enum.GetValues<LevelEco>()
            .Select(level => new TahapLevel(
                (EcoScoreRingkasan.Kosong with { Level = level }).IkonLevel,
                level.ToString(),
                EcoScoreCalculator.PoinMinimal(level),
                level <= EcoScore.Level,
                level == EcoScore.Level))
            .ToList();

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

    // Kembali ke card Jelajah tanpa menghapus isinya
    private void TutupPanel()
    {
        IsMenuOpen = false;
        IsEcoScoreOpen = false;
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

        // Klik pin di peta saat panel terbuka -> tampilkan lagi card Jelajah supaya detail tempatnya terlihat
        TutupPanel();

        SearchQuery = place.Nama;
        SelectedPlace = place;

        CurrentState =
            AppState.StateB_SearchLocation;
    }

    private void ExecuteMulaiRute(object? parameter)
    {
        // Tempat terpilih jadi tujuan (tidak perlu dicari ulang), klik peta berikutnya mengisi titik awal
        if (SelectedPlace is not null)
        {
            TitikTujuan = SelectedPlace.Nama;
            TujuanTerpilih = SelectedPlace;
        }

        KolomAktif = KolomRute.Asal;

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
            if ((PratinjauAsal is null || TujuanTerpilih is null) &&
                string.Equals(
                TitikAsal.Trim(),
                TitikTujuan.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new RoutingException(
                    "Titik awal dan tujuan tidak boleh sama.");
            }

            // Titik awal yang sudah dipratinjau (Enter / klik peta) tidak perlu dicari ulang
            Place asal = PratinjauAsal ?? await CariKoordinatAsync(
                TitikAsal,
                "Titik awal");

            // Tujuan yang dipilih dari daftar saran juga tidak perlu dicari ulang
            Place tujuan = TujuanTerpilih ?? await CariKoordinatAsync(
                TitikTujuan,
                "Tujuan");

            if (Math.Abs(asal.Lat - tujuan.Lat) < 0.0002 &&
                Math.Abs(asal.Lng - tujuan.Lng) < 0.0002)
            {
                throw new RoutingException(
                    "Titik awal dan tujuan berada di lokasi yang sama.");
            }

            // Rute jalan raya, sepeda, dan jalan kaki diambil sekaligus
            RuteSemuaModa semuaRute =
                await _routingService.HitungSemuaRuteAsync(
                    asal,
                    tujuan);

            // Bus: cari trayek Trans Jogja yang langsung menghubungkan asal & tujuan (data lokal, instan)
            RuteBus? trayek = _transJogja.CariRuteLangsung(asal, tujuan);
            _ruteBus = trayek is null
                ? null
                : RoutingService.BuatRuteBus(trayek, semuaRute.JalanRaya);

            // Kemacetan hanya relevan untuk jalan raya (Mobil, Motor, Bus) dan jalur trayek bus.
            // Keduanya diambil bersamaan supaya tidak menambah waktu tunggu.
            Task lalinJalanRaya = AmbilLaluLintasAsync(semuaRute.JalanRaya);
            Task<KondisiLaluLintas?> lalinBus = AmbilLaluLintasBusAsync(_ruteBus);
            await Task.WhenAll(lalinJalanRaya, lalinBus);
            LaluLintasBus = lalinBus.Result;

            _ruteSemuaModa = semuaRute;
            TampilkanRuteModaTerpilih();

            HitungUlangEmisi();

            CurrentState =
                AppState.StateD_RouteResult;
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

    // Rute milik suatu moda (Sepeda & Jalan Kaki punya jalur sendiri, Bus ikut trayek Trans Jogja
    // kalau ada yang langsung, sisanya jalan raya)
    private RouteResult? RuteUntuk(string moda)
    {
        if (_ruteSemuaModa is null)
            return null;

        if (moda == "Bus" && _ruteBus is not null)
            return _ruteBus;

        return _ruteSemuaModa.Untuk(RoutingService.ProfilUntuk(moda) ?? ProfilRute.JalanRaya);
    }

    // Peta (lewat HasilRute) dan jarak mengikuti moda yang sedang dipilih
    private void TampilkanRuteModaTerpilih()
    {
        RouteResult? rute = RuteUntuk(SelectedModa);
        if (rute is null)
            return;

        // Diisi sebelum HasilRute, karena peta membaca LaluLintasBus saat HasilRute berubah
        RingkasanLaluLintas = rute.Bus is not null && LaluLintasBus is not null
            ? $"Jalur bus Trans Jogja {rute.Bus.KodeTrayek}: {LaluLintasBus.Ringkasan()} (live TomTom)"
            : _ringkasanJalanRaya;

        HasilRute = rute;
        JarakKm = rute.JarakKm;
        KeteranganBus = SelectedModa == "Bus" ? BuatKeteranganBus(rute.Bus) : string.Empty;
    }

    // Kemacetan di sepanjang jalur trayek Trans Jogja (bagian naik bus saja).
    // null = tidak ada trayek langsung, atau data live TomTom tidak tersedia (dipakai rata-rata jalan raya).
    private KondisiLaluLintas? _laluLintasBus;
    public KondisiLaluLintas? LaluLintasBus
    {
        get => _laluLintasBus;
        private set => SetProperty(ref _laluLintasBus, value);
    }

    private async Task<KondisiLaluLintas?> AmbilLaluLintasBusAsync(RouteResult? ruteBus)
    {
        if (ruteBus?.Bus is null)
            return null;

        // JarakKm rute bus = total dengan jalan kaki; segmen kemacetan hanya untuk jarak naik bus
        return await _trafficService.AmbilKondisiAsync(ruteBus with { JarakKm = ruteBus.Bus.JarakBusKm });
    }

    // Pengali waktu tempuh akibat kemacetan untuk rute suatu moda
    private double PengaliWaktuUntuk(RouteResult rute) =>
        rute.Bus is not null && LaluLintasBus is not null
            ? LaluLintasBus.PengaliWaktu
            : LaluLintas?.PengaliWaktu ?? 1.0;

    private static string BuatKeteranganBus(RuteBus? bus)
    {
        if (bus is null)
        {
            return "Belum ada trayek Trans Jogja langsung dengan halte dalam " +
                   $"{TransJogjaService.MaksJalanKakiKm:0} km dari titik awal dan tujuan, " +
                   "jadi jalur dan waktu bus diperkirakan dari jalan raya.";
        }

        // Nama OSM: "Transjogja 4A: Terminal Giwangan → Terminal Condongcatur"
        int titikDua = bus.NamaTrayek.IndexOf(':');
        string arah = titikDua >= 0 ? $" ({bus.NamaTrayek[(titikDua + 1)..].Trim()})" : string.Empty;

        return $"Trans Jogja {bus.KodeTrayek}{arah}: naik di {bus.HalteNaik.Nama}, " +
               $"turun di {bus.HalteTurun.Nama} ({bus.JarakBusKm:0.0} km naik bus). " +
               $"Jalan kaki {FormatJarak(bus.JalanKakiAwalKm)} ke halte dan " +
               $"{FormatJarak(bus.JalanKakiAkhirKm)} dari halte.";
    }

    private static string FormatJarak(double km) =>
        km < 1
            ? $"±{Math.Max(10, Math.Round(km * 100) * 10):0} m"
            : $"±{km:0.0} km";

    // Jarak yang ditempuh kendaraan (tanpa jalan kaki ke / dari halte)
    private static double JarakKendaraanKm(RouteResult rute) =>
        rute.Bus?.JarakBusKm ?? rute.JarakKm;

    // Emisi satu moda dengan jarak rutenya sendiri. Jalan raya memakai kemacetan per segmen.
    private double HitungEmisiModa(ModaTransportasi moda)
    {
        RouteResult? rute = RuteUntuk(moda.Nama);
        if (rute is null)
            return moda.HitungEmisi(JarakKm);

        if (rute.Profil == ProfilRute.JalanRaya && LaluLintas is not null)
            return LaluLintas.HitungEmisi(moda);

        // Trayek Trans Jogja: hanya bagian naik bus yang beremisi. Kemacetan diukur per segmen di
        // sepanjang jalur bus; kalau datanya tidak ada, dipakai rata-rata emisi per km jalan raya.
        if (rute.Bus is not null)
            return KondisiLaluLintas.HitungEmisiBus(moda, rute.Bus.JarakBusKm, LaluLintasBus, LaluLintas);

        return moda.HitungEmisi(rute.JarakKm);
    }

    // Kemacetan per segmen dari TomTom. Gagal / tanpa key -> tebakan dari jam
    // (tidak dianggap error, karena hasil emisi tetap bisa dihitung).
    private async Task AmbilLaluLintasAsync(RouteResult rute)
    {
        KondisiLaluLintas? live = await _trafficService.AmbilKondisiAsync(rute);
        if (live is not null)
        {
            LaluLintas = live;
            _ringkasanJalanRaya = BuatRingkasanLive(live);
            return;
        }

        LaluLintas = KondisiLaluLintas.Tebakan(rute.JarakKm, rute.Titik.Count, DateTime.Now);
        string tingkat = LaluLintas.TingkatRataRata.ToString();

        if (_trafficService.AdaApiKey)
        {
            _ringkasanJalanRaya = $"{tingkat} (ditebak dari jam, data TomTom tidak tersedia)";
            _ = TampilkanNotifikasiSementaraAsync("Data lalu lintas TomTom tidak bisa diambil. Kemacetan sementara ditebak dari jam.");
        }
        else
        {
            _ringkasanJalanRaya = $"{tingkat} (ditebak dari jam, API key TomTom belum diatur)";
        }
    }

    private static string BuatRingkasanLive(KondisiLaluLintas kondisi) =>
        kondisi.Ringkasan() + " (live TomTom)";

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
        IsPerjalananTercatat = false;

        // Tiap moda memakai jarak dan rutenya sendiri; jalan raya dihitung per segmen sesuai kemacetannya
        var perModa = DaftarModa
            .Select(m =>
            {
                RouteResult? rute = RuteUntuk(m.Nama);
                int waktu = rute is null
                    ? 0
                    : RoutingService.EstimasiWaktuMenit(m.Nama, rute, PengaliWaktuUntuk(rute));
                return (Moda: m, Emisi: HitungEmisiModa(m), Waktu: waktu, JarakKm: rute?.JarakKm ?? JarakKm);
            })
            .ToList();

        double emisiMax = perModa.Max(x => x.Emisi);
        double emisiMin = perModa.Min(x => x.Emisi);

        ModaRekomendasi = RekomendasiModa.Pilih(
            perModa.Select(x => new KandidatModa(x.Moda.Nama, x.Emisi, x.Waktu, x.JarakKm)));

        PerbandinganModa = perModa
            .Select(x => new PerbandinganEmisi(
                x.Moda.Nama,
                x.Moda.Ikon,
                x.Emisi,
                emisiMax > 0 ? x.Emisi / emisiMax : 0,
                x.Moda.Nama == SelectedModa,
                x.Emisi == emisiMin,
                x.Waktu,
                x.Moda.Nama == ModaRekomendasi))
            .ToList();

        var terpilih = perModa.FirstOrDefault(x => x.Moda.Nama == SelectedModa);
        EmisiKarbonKg = terpilih.Emisi;

        if (HasilRute is not null)
        {
            EstimasiWaktuMenit =
                RoutingService.EstimasiWaktuMenit(
                    SelectedModa,
                    HasilRute,
                    PengaliWaktuUntuk(HasilRute));
        }

        KondisiWaktu = HitungKondisiWaktu();

        var rekomendasi = perModa.FirstOrDefault(x => x.Moda.Nama == ModaRekomendasi);
        InsightText = BuatInsight(rekomendasi.Moda, rekomendasi.Emisi, rekomendasi.Waktu);
        InsightDetail = BuatInsightDetail();
    }

    // ==========================================
    // RINGKASAN HASIL (State D)
    // ==========================================

    // Moda yang direkomendasikan untuk rute ini (lihat Models/RekomendasiModa.cs)
    private string? _modaRekomendasi;
    public string? ModaRekomendasi
    {
        get => _modaRekomendasi;
        private set
        {
            if (SetProperty(ref _modaRekomendasi, value))
                OnPropertyChanged(nameof(IsModaRekomendasi));
        }
    }

    public bool IsModaRekomendasi => ModaRekomendasi == SelectedModa;

    // Penjelasan lengkap saran (tooltip ikon info di card hasil)
    private string _insightDetail = string.Empty;
    public string InsightDetail
    {
        get => _insightDetail;
        private set => SetProperty(ref _insightDetail, value);
    }

    // Kondisi lalu lintas yang memengaruhi waktu tempuh moda terpilih: "Lancar", "Sedang", "Padat", "Macet",
    // atau kosong untuk moda tanpa kemacetan (sepeda, jalan kaki). Dipakai untuk mewarnai waktu tempuh.
    private string _kondisiWaktu = string.Empty;
    public string KondisiWaktu
    {
        get => _kondisiWaktu;
        private set => SetProperty(ref _kondisiWaktu, value);
    }

    // Ringkasan kemacetan untuk tooltip, tanpa nama penyedia data
    public string KeteranganKemacetan
    {
        get
        {
            string teks = RingkasanLaluLintas.Replace(" (live TomTom)", string.Empty);
            int kurung = teks.IndexOf("(ditebak dari jam", StringComparison.Ordinal);
            return kurung >= 0 ? teks[..kurung] + "(perkiraan dari jam)" : teks;
        }
    }

    public ICommand PilihModaCommand { get; }

    public string WaktuTampil => PerbandinganEmisi.TeksWaktu(EstimasiWaktuMenit);

    private string HitungKondisiWaktu()
    {
        if (HasilRute is null ||
            DaftarModa.FirstOrDefault(m => m.Nama == SelectedModa) is not KendaraanBermotor { TerpengaruhKemacetan: true })
        {
            return string.Empty;
        }

        // Pengali waktu 1,5 = kecepatan tinggal 67% dari normal, dst.
        double pengali = PengaliWaktuUntuk(HasilRute);
        return TingkatKemacetanInfo.DariRasioKecepatan(1.0 / Math.Max(pengali, 1.0)).ToString();
    }

    // Satu kalimat singkat: moda terpilih sudah terbaik, atau saran moda lain beserta untung-ruginya
    private string BuatInsight(ModaTransportasi? rekomendasi, double emisiRekomendasi, int waktuRekomendasi)
    {
        if (rekomendasi is null)
            return string.Empty;

        if (rekomendasi.Nama == SelectedModa)
        {
            return EmisiKarbonKg == 0
                ? "Pilihan terbaik: tanpa emisi untuk rute ini."
                : "Pilihan terbaik untuk rute ini.";
        }

        double hemat = EmisiKarbonKg - emisiRekomendasi;
        int selisihMenit = waktuRekomendasi - EstimasiWaktuMenit;
        string waktu = selisihMenit switch
        {
            > 2 => $", +{selisihMenit} menit",
            < -2 => $", {-selisihMenit} menit lebih cepat",
            _ => ", waktunya hampir sama"
        };

        return hemat > 0.005
            ? $"Coba {rekomendasi.Nama}: hemat {hemat:0.00} kg CO₂{waktu}."
            : $"Coba {rekomendasi.Nama}{waktu}.";
    }

    private string BuatInsightDetail()
    {
        var kalimat = new List<string> { RekomendasiModa.Aturan };

        // Tambahan emisi akibat kemacetan saat ini dibanding kalau semua ruas lancar
        ModaTransportasi? moda = DaftarModa.FirstOrDefault(m => m.Nama == SelectedModa);
        if (moda is KendaraanBermotor { TerpengaruhKemacetan: true })
        {
            double tambahan = EmisiKarbonKg -
                moda.HitungEmisi(HasilRute is null ? JarakKm : JarakKendaraanKm(HasilRute));

            if (tambahan >= 0.005)
                kalimat.Add($"Kemacetan saat ini menambah {tambahan:0.00} kg CO₂ untuk {SelectedModa} dibanding kalau semua jalan lancar.");
        }

        return string.Join("\n\n", kalimat);
    }

    // Keterangan bus singkat di card hasil; rincian lengkap (KeteranganBus) ada di tooltip
    public string KeteranganBusSingkat =>
        HasilRute?.Bus is { } bus
            ? $"Trans Jogja {bus.KodeTrayek} · naik di {bus.HalteNaik.Nama}"
            : "Belum ada trayek Trans Jogja langsung";

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
        _ruteSemuaModa = null;
        _ruteBus = null;
        KeteranganBus = string.Empty;
        LaluLintas = null;
        LaluLintasBus = null;
        _ringkasanJalanRaya = string.Empty;
        RingkasanLaluLintas = string.Empty;
    }
}
