// Rekomendasi moda: bukan sekadar emisi terendah (itu pasti jalan kaki / sepeda), tapi moda
// beremisi paling rendah yang masih masuk akal untuk rute ini.
namespace EcoRoute.Models;

public record KandidatModa(string Nama, double EmisiKg, int WaktuMenit, double JarakKm);

public static class RekomendasiModa
{
    // Jarak terjauh yang masih wajar ditempuh tanpa kendaraan bermotor
    public const double MaksJalanKakiKm = 2.0;
    public const double MaksSepedaKm = 8.0;

    // Waktu tempuh boleh lebih lama dari moda tercepat, paling banyak sebesar ini (mana yang lebih besar):
    // 15 menit, atau sama dengan waktu moda tercepat (jadi maksimal 2x lipat)
    public const int ToleransiMinimalMenit = 15;

    public static string Aturan =>
        "Rekomendasi = moda dengan emisi paling rendah yang waktunya paling lama 2× moda tercepat " +
        $"(atau +{ToleransiMinimalMenit} menit). Jalan kaki hanya untuk rute ≤ {MaksJalanKakiKm:0} km, " +
        $"sepeda ≤ {MaksSepedaKm:0} km.";

    public static bool Wajar(KandidatModa moda) => moda.Nama switch
    {
        "Jalan Kaki" => moda.JarakKm <= MaksJalanKakiKm,
        "Sepeda" => moda.JarakKm <= MaksSepedaKm,
        _ => true
    };

    // Nama moda yang direkomendasikan, atau null kalau tidak ada kandidat
    public static string? Pilih(IEnumerable<KandidatModa> kandidat)
    {
        List<KandidatModa> wajar = kandidat.Where(Wajar).ToList();
        if (wajar.Count == 0)
            return null;

        int tercepat = wajar.Min(m => m.WaktuMenit);
        int batas = tercepat + Math.Max(ToleransiMinimalMenit, tercepat);

        return wajar
            .Where(m => m.WaktuMenit <= batas)
            .OrderBy(m => Math.Round(m.EmisiKg, 3))
            .ThenBy(m => m.WaktuMenit)
            .First()
            .Nama;
    }
}
