// Satu baris perbandingan antar moda di State D.
// RasioBar = 0..1, dipakai untuk panjang bar (1 = moda dengan emisi terbesar).
// IsRekomendasi = moda pilihan RekomendasiModa (emisi rendah dengan waktu tempuh yang masih wajar).
namespace EcoRoute.Models;

public record PerbandinganEmisi(
    string Nama,
    string Ikon,
    double EmisiKg,
    double RasioBar,
    bool IsTerpilih,
    bool IsTerhemat,
    int WaktuMenit = 0,
    bool IsRekomendasi = false)
{
    public string WaktuTeks => TeksWaktu(WaktuMenit);

    // 45 -> "45 mnt", 80 -> "1 j 20 mnt"
    public static string TeksWaktu(int menit) =>
        menit < 60 ? $"{menit} mnt" : menit % 60 == 0 ? $"{menit / 60} j" : $"{menit / 60} j {menit % 60} mnt";
}
