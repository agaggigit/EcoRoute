// Hasil rute dari OSRM (State D).
// Titik = koordinat [lat, lng] untuk menggambar garis rute di peta.
namespace EcoRoute.Models;

public record RouteResult(
    Place Asal,
    Place Tujuan,
    double JarakKm,
    double DurasiMobilMenit,
    IReadOnlyList<double[]> Titik);
