// Lokasi hasil pencarian (State B). Nanti diisi dari geocoding OpenStreetMap (Nominatim).
namespace EcoRoute.Models;

public record Place(string Nama, string Alamat, string Kategori, double Lat, double Lng);
