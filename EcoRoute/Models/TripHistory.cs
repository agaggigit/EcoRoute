using System;

namespace EcoRoute.Models;

public class TripHistory
{
    public int TripId { get; set; }

    public DateTime Tanggal { get; set; }

    public string NamaAsal { get; set; } = string.Empty;

    public string NamaTujuan { get; set; } = string.Empty;

    public double JarakKm { get; set; }

    public string ModaDipilih { get; set; } = string.Empty;

    public double TotalEmisiKg { get; set; }

    // Koordinat asal & tujuan, dipakai untuk menghitung ulang rute yang sama dari panel Riwayat
    public double LatAsal { get; set; }

    public double LngAsal { get; set; }

    public double LatTujuan { get; set; }

    public double LngTujuan { get; set; }

    public int EstimasiWaktuMenit { get; set; }

    public bool AdaKoordinat => LatAsal != 0 || LngAsal != 0 || LatTujuan != 0 || LngTujuan != 0;

    // Ikon moda untuk daftar riwayat (glyph sama dengan daftar moda)
    public string IkonModa => ModaDipilih switch
    {
        "Mobil" => "\uE804",
        "Motor" => "\U0001F6F5",
        "Bus" => "\uE806",
        "Kereta" => "\uE7C0",
        "Sepeda" => "\U0001F6B2",
        "Jalan Kaki" => "\uE805",
        _ => "\uE80F"
    };
}
