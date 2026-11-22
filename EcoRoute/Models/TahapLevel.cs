// Satu tahap level Eco Score untuk panel Eco Score: Bibit -> Tunas -> Pohon -> Hutan.
namespace EcoRoute.Models;

public record TahapLevel(string Ikon, string Nama, int PoinMinimal, bool Tercapai, bool Aktif);
