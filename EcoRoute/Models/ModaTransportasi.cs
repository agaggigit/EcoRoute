// Pilihan moda di UI. Faktor emisi (kg CO2/km) sementara disalin dari Database/seed.sql,
// nanti diganti data dari Vehicle / DatabaseService.
namespace EcoRoute.Models;

public record ModaTransportasi(string Nama, string Ikon, double FaktorEmisiKgPerKm);
