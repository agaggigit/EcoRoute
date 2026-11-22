// Hasil rute dari layanan routing (State D).
// Titik = koordinat [lat, lng] untuk menggambar garis rute di peta.
// DurasiMenit = durasi dari layanan routing untuk profil rute ini (tanpa efek kemacetan).
namespace EcoRoute.Models;

// Jenis jalur: Mobil, Motor, dan Bus lewat jalan raya; Sepeda dan Jalan Kaki punya jalurnya sendiri.
public enum ProfilRute
{
    JalanRaya,
    Sepeda,
    JalanKaki
}

public record RouteResult(
    Place Asal,
    Place Tujuan,
    double JarakKm,
    double DurasiMenit,
    IReadOnlyList<double[]> Titik,
    ProfilRute Profil = ProfilRute.JalanRaya);

// Rute untuk semua profil, diambil sekaligus saat Hitung Emisi supaya ganti moda tetap instan.
// Kalau rute sepeda / jalan kaki gagal diambil, dipakai rute jalan raya sebagai cadangan.
public record RuteSemuaModa(RouteResult JalanRaya, RouteResult Sepeda, RouteResult JalanKaki)
{
    public RouteResult Untuk(ProfilRute profil) => profil switch
    {
        ProfilRute.Sepeda => Sepeda,
        ProfilRute.JalanKaki => JalanKaki,
        _ => JalanRaya
    };
}
