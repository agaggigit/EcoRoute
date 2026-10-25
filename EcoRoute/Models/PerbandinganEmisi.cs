// Satu baris perbandingan emisi antar moda di State D.
// RasioBar = 0..1, dipakai untuk panjang bar (1 = moda dengan emisi terbesar).
namespace EcoRoute.Models;

public record PerbandinganEmisi(
    string Nama,
    string Ikon,
    double EmisiKg,
    double RasioBar,
    bool IsTerpilih,
    bool IsTerhemat);
