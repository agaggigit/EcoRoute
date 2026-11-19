// Pilihan moda di UI. Faktor emisi (kg CO2/km) diambil dari tabel emission_factor
// (faktor_emisi_dasar, faktor_penalti_kemacetan); nilai cadangan ada di MainViewModel.
namespace EcoRoute.Models;

// Base class: semua moda punya Nama & Ikon, tapi cara menghitung emisinya diserahkan ke subclass.
public abstract class ModaTransportasi
{
    public string Nama { get; }
    public string Ikon { get; }

    protected ModaTransportasi(string nama, string ikon)
    {
        Nama = nama;
        Ikon = ikon;
    }

    // Emisi (kg CO2) untuk jarak dan kondisi lalu lintas tertentu; tiap jenis moda punya rumus sendiri.
    public abstract double HitungEmisi(double jarakKm, TingkatKemacetan kemacetan);

    // Versi singkat: lalu lintas lancar (tanpa penalti kemacetan).
    public double HitungEmisi(double jarakKm) => HitungEmisi(jarakKm, TingkatKemacetan.Lancar);

    protected static void ValidasiJarak(double jarakKm)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(jarakKm);
    }
}

// Mobil, Motor, Bus, Kereta: emisi = jarak x faktor dasar x penalti efektif.
// Penalti efektif = 1 + (faktor penalti - 1) x bobot kemacetan, jadi Lancar = x1 dan Macet = penalti penuh.
public class KendaraanBermotor : ModaTransportasi
{
    private readonly double _faktorEmisiDasar;
    private readonly double _faktorPenaltiKemacetan;

    public KendaraanBermotor(string nama, string ikon, double faktorEmisiDasar, double faktorPenaltiKemacetan = 1.0)
        : base(nama, ikon)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(faktorEmisiDasar);
        ArgumentOutOfRangeException.ThrowIfLessThan(faktorPenaltiKemacetan, 1.0);
        _faktorEmisiDasar = faktorEmisiDasar;
        _faktorPenaltiKemacetan = faktorPenaltiKemacetan;
    }

    // true kalau emisinya terpengaruh kemacetan (Kereta punya penalti 1.0, jadi tidak)
    public bool TerpengaruhKemacetan => _faktorPenaltiKemacetan > 1.0;

    public override double HitungEmisi(double jarakKm, TingkatKemacetan kemacetan)
    {
        ValidasiJarak(jarakKm);
        double penaltiEfektif = 1.0 + (_faktorPenaltiKemacetan - 1.0) * kemacetan.BobotPenalti();
        return jarakKm * _faktorEmisiDasar * penaltiEfektif;
    }
}

// Sepeda, Jalan Kaki: tenaga manusia, tanpa emisi di kondisi lalu lintas apa pun.
public class TransportasiAktif : ModaTransportasi
{
    public TransportasiAktif(string nama, string ikon) : base(nama, ikon) { }

    public override double HitungEmisi(double jarakKm, TingkatKemacetan kemacetan)
    {
        ValidasiJarak(jarakKm);
        return 0;
    }
}
