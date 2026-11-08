
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
}
