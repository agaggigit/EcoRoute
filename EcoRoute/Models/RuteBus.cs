// Perjalanan naik satu trayek Trans Jogja tanpa pindah bus:
// jalan kaki ke halte naik -> naik bus sepanjang trayek -> turun -> jalan kaki ke tujuan.
// Dihasilkan TransJogjaService dari data trayek OpenStreetMap.
namespace EcoRoute.Models;

public record HalteBus(string Nama, double Lat, double Lng);

public record RuteBus(
    string KodeTrayek,
    string NamaTrayek,
    HalteBus HalteNaik,
    HalteBus HalteTurun,
    double JalanKakiAwalKm,
    double JarakBusKm,
    double JalanKakiAkhirKm,
    IReadOnlyList<double[]> TitikBus)
{
    public double JarakJalanKakiKm => JalanKakiAwalKm + JalanKakiAkhirKm;

    public double JarakTotalKm => JarakJalanKakiKm + JarakBusKm;
}
