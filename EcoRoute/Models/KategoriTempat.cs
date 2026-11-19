// Kategori tempat populer di peta (chip filter di atas peta).
// Warna & ikon pin di peta diatur di Maps/map.html berdasarkan Nama.
using EcoRoute.ViewModels;

namespace EcoRoute.Models;

public class KategoriTempat : BaseViewModel
{
    private bool _isAktif;

    public string Nama { get; }
    public string Ikon { get; }

    public KategoriTempat(string nama, string ikon)
    {
        Nama = nama;
        Ikon = ikon;
    }

    public bool IsAktif
    {
        get => _isAktif;
        set => SetProperty(ref _isAktif, value);
    }
}
