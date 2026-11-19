// Kondisi lalu lintas saat perjalanan. Nilainya sama dengan kolom tingkat_kemacetan di schema.sql.
// Tidak ada API lalu lintas gratis, jadi nilai awalnya ditebak dari jam (lihat Default) dan bisa diganti user.
namespace EcoRoute.Models;

public enum TingkatKemacetan
{
    Lancar,
    Sedang,
    Padat,
    Macet
}

public static class TingkatKemacetanInfo
{
    // Seberapa besar faktor_penalti_kemacetan dipakai: 0 = tidak sama sekali, 1 = penuh.
    // Contoh Mobil (penalti 1.35): Lancar x1.00, Sedang x1.12, Padat x1.23, Macet x1.35
    public static double BobotPenalti(this TingkatKemacetan kemacetan) => kemacetan switch
    {
        TingkatKemacetan.Sedang => 1.0 / 3.0,
        TingkatKemacetan.Padat => 2.0 / 3.0,
        TingkatKemacetan.Macet => 1.0,
        _ => 0.0
    };

    // Pengali waktu tempuh untuk kendaraan yang lewat jalan raya (Mobil, Motor, Bus)
    public static double PengaliWaktu(this TingkatKemacetan kemacetan) => kemacetan switch
    {
        TingkatKemacetan.Sedang => 1.25,
        TingkatKemacetan.Padat => 1.6,
        TingkatKemacetan.Macet => 2.0,
        _ => 1.0
    };

    // Tebakan awal dari jam: jam sibuk hari kerja = Padat, siang = Sedang, malam/dini hari = Lancar
    public static TingkatKemacetan Default(DateTime waktu)
    {
        double jam = waktu.TimeOfDay.TotalHours;
        bool akhirPekan = waktu.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        bool jamSibuk = jam is >= 6.5 and < 9 || jam is >= 16 and < 19;
        bool siang = jam is >= 9 and < 16;

        if (jamSibuk && !akhirPekan) return TingkatKemacetan.Padat;
        if (jamSibuk || siang) return TingkatKemacetan.Sedang;
        return TingkatKemacetan.Lancar;
    }
}
