// Pilihan moda di UI. Faktor emisi (kg CO2/km) sementara disalin dari Database/seed.sql,
// nanti diisi dari tabel emission_factor (faktor_emisi_dasar, faktor_penalti_kemacetan).
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

    // Emisi (kg CO2) untuk jarak tertentu; tiap jenis moda punya rumus sendiri.
    public abstract double HitungEmisi(double jarakKm);

    protected static void ValidasiJarak(double jarakKm)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(jarakKm);
    }
}

// Mobil, Motor, Bus, Kereta: emisi = jarak x faktor dasar x penalti kemacetan.
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

    public override double HitungEmisi(double jarakKm)
    {
        ValidasiJarak(jarakKm);
        return jarakKm * _faktorEmisiDasar * _faktorPenaltiKemacetan;
    }
}

// Sepeda, Jalan Kaki: tenaga manusia, tanpa emisi.
public class TransportasiAktif : ModaTransportasi
{
    public TransportasiAktif(string nama, string ikon) : base(nama, ikon) { }

    public override double HitungEmisi(double jarakKm)
    {
        ValidasiJarak(jarakKm);
        return 0;
    }
}
