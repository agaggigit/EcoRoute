// Eco Score: ringkasan riwayat perjalanan + CO2 yang dihemat dibanding naik mobil, diubah jadi poin dan level.
namespace EcoRoute.Models;

// Level naik berdasarkan poin. Urutan enum = urutan level.
public enum LevelEco
{
    Bibit,
    Tunas,
    Pohon,
    Hutan,
}

public sealed record EcoScoreRingkasan(
    int TotalPerjalanan,
    double TotalJarakKm,
    double TotalEmisiKg,
    int PerjalananTanpaEmisi,
    double EmisiDihematKg,
    int Poin,
    LevelEco Level,
    double ProgresKeLevelBerikut,
    int? PoinMenujuLevelBerikut,
    double SetaraPohon)
{
    public static EcoScoreRingkasan Kosong { get; } = new(0, 0, 0, 0, 0, 0, LevelEco.Bibit, 0, EcoScoreCalculator.PoinMinimal(LevelEco.Tunas), 0);

    public string NamaLevel => Level.ToString();

    public string IkonLevel => Level switch
    {
        LevelEco.Bibit => "\U0001F331", // 🌱
        LevelEco.Tunas => "\U0001F33F", // 🌿
        LevelEco.Pohon => "\U0001F333", // 🌳
        _              => "\U0001F332", // 🌲
    };

    public string KeteranganLevel => PoinMenujuLevelBerikut is int sisa
        ? $"{sisa} poin lagi menuju {Level + 1}"
        : "Level tertinggi tercapai!";
}

// Menghitung Eco Score dari riwayat. Emisi tiap moda dihitung ulang lewat HitungEmisi (polimorfik)
// dalam kondisi lancar, supaya skor tidak bergantung pada kemacetan saat perjalanan dilakukan
// (kalau memakai emisi tersimpan, naik mobil saat macet bisa menghasilkan "penghematan" negatif).
public class EcoScoreCalculator
{
    public const string ModaAcuan = "Mobil";

    // 1 kg CO2 dihemat = 10 poin
    public const double PoinPerKg = 10;

    // Rata-rata serapan satu pohon dewasa ±21 kg CO2 per tahun
    // (European Environment Agency; Arbor Day Foundation: ±48 lb ≈ 21,8 kg).
    public const double KgCo2PerPohonPerTahun = 21.0;

    private static readonly int[] AmbangPoin = [0, 100, 500, 2000]; // Bibit, Tunas, Pohon, Hutan

    private readonly IReadOnlyDictionary<string, ModaTransportasi> _modaPerNama;
    private readonly ModaTransportasi? _acuan;

    public EcoScoreCalculator(IEnumerable<ModaTransportasi> daftarModa)
    {
        ArgumentNullException.ThrowIfNull(daftarModa);

        var kamus = new Dictionary<string, ModaTransportasi>(StringComparer.OrdinalIgnoreCase);
        foreach (ModaTransportasi moda in daftarModa)
            kamus.TryAdd(moda.Nama, moda);

        _modaPerNama = kamus;
        _acuan = kamus.GetValueOrDefault(ModaAcuan);
    }

    public static int PoinMinimal(LevelEco level) => AmbangPoin[(int)level];

    public static LevelEco TentukanLevel(int poin)
    {
        for (int i = AmbangPoin.Length - 1; i > 0; i--)
        {
            if (poin >= AmbangPoin[i])
                return (LevelEco)i;
        }
        return LevelEco.Bibit;
    }

    // CO2 (kg) yang dihemat satu perjalanan dibanding naik mobil, kondisi lancar. Tidak pernah negatif.
    public double HitungPenghematan(TripHistory trip)
    {
        ArgumentNullException.ThrowIfNull(trip);

        if (_acuan is null || trip.JarakKm <= 0
            || !_modaPerNama.TryGetValue(trip.ModaDipilih, out ModaTransportasi? moda))
            return 0;

        double selisih = _acuan.HitungEmisi(trip.JarakKm) - moda.HitungEmisi(trip.JarakKm);
        return Math.Max(0, selisih);
    }

    public EcoScoreRingkasan Hitung(IEnumerable<TripHistory> riwayat)
    {
        ArgumentNullException.ThrowIfNull(riwayat);

        List<TripHistory> daftar = riwayat.ToList();
        if (daftar.Count == 0)
            return EcoScoreRingkasan.Kosong;

        double dihemat = daftar.Sum(HitungPenghematan);
        int poin = (int)Math.Round(dihemat * PoinPerKg, MidpointRounding.AwayFromZero);
        LevelEco level = TentukanLevel(poin);

        double progres = 1;
        int? menujuBerikut = null;
        if (level < LevelEco.Hutan)
        {
            int min = PoinMinimal(level);
            int berikut = PoinMinimal(level + 1);
            progres = (double)(poin - min) / (berikut - min);
            menujuBerikut = berikut - poin;
        }

        return new EcoScoreRingkasan(
            TotalPerjalanan: daftar.Count,
            TotalJarakKm: daftar.Sum(t => t.JarakKm),
            TotalEmisiKg: daftar.Sum(t => t.TotalEmisiKg),
            PerjalananTanpaEmisi: daftar.Count(t => t.TotalEmisiKg <= 0),
            EmisiDihematKg: dihemat,
            Poin: poin,
            Level: level,
            ProgresKeLevelBerikut: progres,
            PoinMenujuLevelBerikut: menujuBerikut,
            SetaraPohon: dihemat / KgCo2PerPohonPerTahun);
    }
}
