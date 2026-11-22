// Hasil rute dari layanan routing (State D).
// Titik = koordinat [lat, lng] untuk menggambar garis rute di peta.
// DurasiMenit = durasi dari layanan routing untuk profil rute ini (tanpa efek kemacetan).
namespace EcoRoute.Models;

// Jenis jalur: Mobil, Motor, dan Bus lewat jalan raya; Sepeda dan Jalan Kaki punya jalurnya sendiri.
// TrayekBus = Bus mengikuti trayek Trans Jogja asli (detailnya di RouteResult.Bus).
public enum ProfilRute
{
    JalanRaya,
    Sepeda,
    JalanKaki,
    TrayekBus
}

// Untuk TrayekBus: JarakKm = total perjalanan (jalan kaki + bus), Titik = jalur bus saja,
// DurasiMenit = waktu tempuh bagian bus kalau jalan lancar (tanpa halte, tanpa menunggu).
public record RouteResult(
    Place Asal,
    Place Tujuan,
    double JarakKm,
    double DurasiMenit,
    IReadOnlyList<double[]> Titik,
    ProfilRute Profil = ProfilRute.JalanRaya,
    RuteBus? Bus = null);

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
