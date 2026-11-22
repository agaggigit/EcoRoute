// Daftar saran lokasi di bawah satu kolom input (search bar, titik awal, tujuan).
// Tempat populer lokal tampil langsung; hasil online (Photon) menyusul setelah user berhenti mengetik sebentar.
using System.Collections.ObjectModel;
using System.Windows.Input;
using EcoRoute.Models;
using EcoRoute.Services;

namespace EcoRoute.ViewModels;

public class SaranLokasiViewModel : BaseViewModel
{
    public const int MinHurufLokal = 2;
    public const int MinHurufOnline = 3;
    public const int MaksLokal = 5;
    public const int MaksSaran = 6;

    private readonly IReadOnlyList<Place> _lokal;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<Place>>> _cariOnline;
    private readonly Action<Place> _saatDipilih;
    private readonly TimeSpan _jeda;

    private CancellationTokenSource? _cts;
    private string? _teksTerpilih;
    private bool _isTerbuka;

    public SaranLokasiViewModel(
        IReadOnlyList<Place> lokal,
        Func<string, CancellationToken, Task<IReadOnlyList<Place>>> cariOnline,
        Action<Place> saatDipilih,
        TimeSpan? jeda = null)
    {
        _lokal = lokal;
        _cariOnline = cariOnline;
        _saatDipilih = saatDipilih;
        _jeda = jeda ?? TimeSpan.FromMilliseconds(400);

        PilihCommand = new RelayCommand(p =>
        {
            if (p is not Place place)
                return;

            Tutup();
            _teksTerpilih = place.Nama;
            _saatDipilih(place);
        }, p => p is Place);

        TutupCommand = new RelayCommand(_ => Tutup());
    }

    public ObservableCollection<Place> Daftar { get; } = [];

    public bool IsTerbuka
    {
        get => _isTerbuka;
        private set => SetProperty(ref _isTerbuka, value);
    }

    public ICommand PilihCommand { get; }
    public ICommand TutupCommand { get; }

    // Dipanggil setiap kali user mengubah teks kolom
    public async Task PerbaruiAsync(string? teks)
    {
        BatalkanPencarian();

        teks = (teks ?? string.Empty).Trim();

        // Teks baru saja diisi dari saran yang dipilih -> jangan buka daftar lagi
        if (string.Equals(teks, _teksTerpilih, StringComparison.OrdinalIgnoreCase))
        {
            Tutup();
            return;
        }
        _teksTerpilih = null;

        if (teks.Length < MinHurufLokal)
        {
            Tutup();
            return;
        }

        // 1. Tempat populer: langsung, tanpa jaringan. Yang diawali teks ketikan didahulukan.
        Daftar.Clear();
        foreach (Place p in _lokal
                     .Where(p => p.Nama.Contains(teks, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(p => p.Nama.StartsWith(teks, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .Take(MaksLokal))
        {
            Daftar.Add(p);
        }
        IsTerbuka = Daftar.Count > 0;

        if (teks.Length < MinHurufOnline)
            return;

        // 2. Online, setelah jeda supaya tidak memanggil server di setiap ketukan
        var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            if (_jeda > TimeSpan.Zero)
                await Task.Delay(_jeda, cts.Token);

            IReadOnlyList<Place> online = await _cariOnline(teks, cts.Token);
            if (cts.IsCancellationRequested)
                return;

            foreach (Place p in online)
            {
                if (Daftar.Count >= MaksSaran)
                    break;
                if (!Daftar.Any(d => SamaTempat(d, p)))
                    Daftar.Add(p);
            }
            IsTerbuka = Daftar.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Teks sudah berubah lagi; hasil lama dibuang
        }
        catch (Exception)
        {
            // Saran online gagal (offline / server sibuk): saran lokal tetap tampil, tanpa notifikasi.
            // Pencarian lewat Enter / Hitung Emisi tetap menampilkan error yang jelas.
        }
    }

    public void Tutup()
    {
        BatalkanPencarian();
        IsTerbuka = false;
        Daftar.Clear();
    }

    private void BatalkanPencarian()
    {
        _cts?.Cancel();
        _cts = null;
    }

    // Nama sama dan lokasinya berdekatan dianggap tempat yang sama. Batasnya longgar (1 km) karena titik
    // tempat populer dan titik OSM untuk tempat panjang (mis. jalan Malioboro) bisa berjauhan ratusan meter.
    public const double JarakTempatSamaKm = 1.0;

    private static bool SamaTempat(Place a, Place b) =>
        string.Equals(a.Nama, b.Nama, StringComparison.OrdinalIgnoreCase) &&
        TransJogjaService.JarakKm(a.Lat, a.Lng, b.Lat, b.Lng) < JarakTempatSamaKm;
}
