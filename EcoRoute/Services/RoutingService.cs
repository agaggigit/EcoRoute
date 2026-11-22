// Layanan peta pihak ketiga (REST API via HttpClient), semuanya gratis tanpa API key:
// - Nominatim (OpenStreetMap) : cari tempat dari nama -> koordinat (geocoding)
// - Photon (komoot, data OSM) : saran tempat saat mengetik (Nominatim melarang autocomplete)
// - OSRM (routing.openstreetmap.de, server komunitas OSM) : hitung rute, jarak, dan durasi
//   untuk tiga profil: mobil (jalan raya), sepeda, dan jalan kaki
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using EcoRoute.Models;

namespace EcoRoute.Services;

// Error yang pesannya sudah ramah pengguna dan boleh langsung ditampilkan di UI (State E).
public class RoutingException : Exception
{
    public RoutingException(string message, Exception? inner = null) : base(message, inner) { }
}

public class RoutingService
{
    private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";
    private const string RoutingUrl = "https://routing.openstreetmap.de";
    private const string PhotonUrl = "https://photon.komoot.io/api/";

    // Asumsi waktu tempuh bus (lihat docs/OOP.md):
    // dikali untuk waktu berhenti di halte sepanjang jalan (makin jauh, makin banyak halte),
    // ditambah untuk waktu menunggu bus datang (tetap per perjalanan, ±setengah jarak kedatangan Trans Jogja).
    public const double PengaliHalteBus = 1.3;
    public const double MenitTungguBus = 10;

    // Kecepatan cadangan (km/jam) kalau rute sepeda / jalan kaki gagal diambil
    private const double KecepatanCadanganSepeda = 15;
    private const double KecepatanCadanganJalanKaki = 5;

    // Prioritaskan hasil di sekitar Yogyakarta (lon kiri, lat atas, lon kanan, lat bawah)
    private const string ViewboxYogyakarta = "110.25,-7.65,110.50,-7.95";

    // Satu HttpClient dipakai bersama (dianjurkan Microsoft supaya socket tidak habis)
    private static readonly HttpClient Http = BuatHttpClient();

    private static HttpClient BuatHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // Nominatim mewajibkan User-Agent yang mengidentifikasi aplikasi
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EcoRoute/1.0 (aplikasi desktop tugas kuliah)");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("id");
        return client;
    }

    // Cari tempat berdasarkan nama. Hasil kosong -> RoutingException.
    public async Task<IReadOnlyList<Place>> CariTempatAsync(string query, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new RoutingException("Nama lokasi tidak boleh kosong.");

        string url = $"{NominatimUrl}?format=jsonv2&countrycodes=id&limit={limit}" +
                     $"&viewbox={ViewboxYogyakarta}&q={Uri.EscapeDataString(query.Trim())}";

        using JsonDocument json = await AmbilJsonAsync(url);

        var hasil = new List<Place>();
        foreach (JsonElement item in json.RootElement.EnumerateArray())
        {
            string alamat = item.GetProperty("display_name").GetString() ?? string.Empty;
            string nama = item.TryGetProperty("name", out var n) && !string.IsNullOrWhiteSpace(n.GetString())
                ? n.GetString()!
                : alamat.Split(',')[0];
            string kategori = item.TryGetProperty("type", out var t) ? Kapital(t.GetString()) : "Lokasi";

            hasil.Add(new Place(
                nama,
                alamat,
                kategori,
                ParseKoordinat(item.GetProperty("lat")),
                ParseKoordinat(item.GetProperty("lon"))));
        }

        if (hasil.Count == 0)
            throw new RoutingException($"Lokasi \"{query.Trim()}\" tidak ditemukan. Coba nama yang lebih lengkap.");

        return hasil;
    }

    // Saran tempat saat user mengetik (Photon, dibatasi area Yogyakarta).
    // Tidak ada hasil -> daftar kosong (bukan exception), karena user masih mengetik.
    public async Task<IReadOnlyList<Place>> SaranTempatAsync(string query, CancellationToken ct, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        string url = $"{PhotonUrl}?limit={limit}&lat=-7.7956&lon=110.3695&bbox={BboxYogyakarta}" +
                     $"&q={Uri.EscapeDataString(query.Trim())}";

        using JsonDocument json = await AmbilJsonAsync(url, ct);
        return ParsePhoton(json);
    }

    // Area yang sama dengan ViewboxYogyakarta, tapi urutan Photon: lon kiri, lat bawah, lon kanan, lat atas
    private const string BboxYogyakarta = "110.25,-7.95,110.50,-7.65";

    // Ubah GeoJSON Photon jadi daftar Place. Koordinat GeoJSON berurutan [lon, lat].
    public static IReadOnlyList<Place> ParsePhoton(JsonDocument json)
    {
        var hasil = new List<Place>();
        if (!json.RootElement.TryGetProperty("features", out JsonElement features))
            return hasil;

        foreach (JsonElement fitur in features.EnumerateArray())
        {
            JsonElement p = fitur.GetProperty("properties");
            JsonElement koordinat = fitur.GetProperty("geometry").GetProperty("coordinates");

            string? Teks(string nama) =>
                p.TryGetProperty(nama, out JsonElement e) && !string.IsNullOrWhiteSpace(e.GetString())
                    ? e.GetString()
                    : null;

            string? jalan = Teks("street") is { } s
                ? (Teks("housenumber") is { } no ? $"{s} {no}" : s)
                : null;
            string? nama = Teks("name") ?? jalan;
            if (nama is null)
                continue;

            string alamat = string.Join(", ",
                new[] { jalan, Teks("district"), Teks("city"), Teks("state") }
                    .Where(x => x is not null && x != nama)
                    .Distinct());

            hasil.Add(new Place(
                nama,
                alamat.Length > 0 ? alamat : nama,
                Kapital(Teks("osm_value")),
                koordinat[1].GetDouble(),
                koordinat[0].GetDouble()));
        }

        return hasil;
    }

    // Jalur yang dipakai tiap moda. null = moda tidak didukung (tidak ditampilkan di aplikasi).
    // OSM tidak punya profil motor yang berbeda dari mobil untuk jalan di Jogja, jadi Motor & Bus ikut jalan raya.
    public static ProfilRute? ProfilUntuk(string moda) => moda switch
    {
        "Mobil" or "Motor" or "Bus" => ProfilRute.JalanRaya,
        "Sepeda" => ProfilRute.Sepeda,
        "Jalan Kaki" => ProfilRute.JalanKaki,
        _ => null
    };

    private static string UrlProfil(ProfilRute profil) => profil switch
    {
        ProfilRute.Sepeda => $"{RoutingUrl}/routed-bike/route/v1/bike",
        ProfilRute.JalanKaki => $"{RoutingUrl}/routed-foot/route/v1/foot",
        _ => $"{RoutingUrl}/routed-car/route/v1/driving"
    };

    // Ambil rute jalan raya, sepeda, dan jalan kaki sekaligus (paralel).
    // Rute jalan raya wajib ada; sepeda / jalan kaki yang gagal diganti rute jalan raya.
    public async Task<RuteSemuaModa> HitungSemuaRuteAsync(Place asal, Place tujuan)
    {
        Task<RouteResult> jalanRaya = HitungRuteAsync(asal, tujuan, ProfilRute.JalanRaya);
        Task<RouteResult?> sepeda = CobaHitungRuteAsync(asal, tujuan, ProfilRute.Sepeda);
        Task<RouteResult?> jalanKaki = CobaHitungRuteAsync(asal, tujuan, ProfilRute.JalanKaki);

        RouteResult raya = await jalanRaya;
        return new RuteSemuaModa(raya, await sepeda ?? raya, await jalanKaki ?? raya);
    }

    private async Task<RouteResult?> CobaHitungRuteAsync(Place asal, Place tujuan, ProfilRute profil)
    {
        try
        {
            return await HitungRuteAsync(asal, tujuan, profil);
        }
        catch (RoutingException)
        {
            return null;
        }
    }

    // Hitung rute dari asal ke tujuan untuk satu profil.
    public async Task<RouteResult> HitungRuteAsync(Place asal, Place tujuan, ProfilRute profil = ProfilRute.JalanRaya)
    {
        string koordinat = string.Create(CultureInfo.InvariantCulture,
            $"{asal.Lng},{asal.Lat};{tujuan.Lng},{tujuan.Lat}");
        string url = $"{UrlProfil(profil)}/{koordinat}?overview=full&geometries=geojson";

        using JsonDocument json = await AmbilJsonAsync(url);
        JsonElement root = json.RootElement;

        if (root.GetProperty("code").GetString() != "Ok" ||
            root.GetProperty("routes").GetArrayLength() == 0)
        {
            throw new RoutingException("Rute tidak ditemukan. Pastikan kedua lokasi bisa dijangkau lewat jalan darat.");
        }

        JsonElement rute = root.GetProperty("routes")[0];
        double jarakKm = rute.GetProperty("distance").GetDouble() / 1000.0;
        double durasiMenit = rute.GetProperty("duration").GetDouble() / 60.0;

        // GeoJSON memakai urutan [lng, lat], Leaflet butuh [lat, lng]
        var titik = rute.GetProperty("geometry").GetProperty("coordinates")
            .EnumerateArray()
            .Select(c => new[] { c[1].GetDouble(), c[0].GetDouble() })
            .ToList();

        return new RouteResult(asal, tujuan, jarakKm, durasiMenit, titik, profil);
    }

    // Rute Bus mengikuti trayek Trans Jogja. Durasi bagian bus kalau lancar diambil dari kecepatan
    // rata-rata rute jalan raya (OSRM) untuk perjalanan yang sama, supaya sebanding dengan mobil.
    public static RouteResult BuatRuteBus(RuteBus bus, RouteResult jalanRaya)
    {
        double kecepatanKmPerMenit = jalanRaya.DurasiMenit > 0
            ? jalanRaya.JarakKm / jalanRaya.DurasiMenit
            : 0.5; // 30 km/jam kalau durasi tidak ada

        return new RouteResult(
            jalanRaya.Asal,
            jalanRaya.Tujuan,
            bus.JarakTotalKm,
            bus.JarakBusKm / kecepatanKmPerMenit,
            bus.TitikBus,
            ProfilRute.TrayekBus,
            bus);
    }

    // Estimasi waktu tempuh per moda. rute = rute milik moda itu (lihat RuteSemuaModa.Untuk).
    // pengaliKemacetan dari KondisiLaluLintas.PengaliWaktu (1.0 = lancar), hanya untuk jalan raya:
    // - Mobil : durasi x pengali kemacetan
    // - Motor : hanya separuh tambahan waktu akibat macet (motor bisa menyelip)
    // - Bus   : durasi x pengali halte x pengali kemacetan + waktu menunggu bus
    //           (+ jalan kaki ke / dari halte kalau rutenya mengikuti trayek Trans Jogja)
    // - Sepeda / Jalan Kaki : durasi profilnya sendiri, tidak terpengaruh macet
    public static int EstimasiWaktuMenit(string moda, RouteResult rute, double pengaliKemacetan = 1.0)
    {
        ProfilRute? profil = ProfilUntuk(moda);
        double menit;

        if (rute is { Profil: ProfilRute.TrayekBus, Bus: not null })
        {
            menit = rute.DurasiMenit * PengaliHalteBus * pengaliKemacetan + MenitTungguBus +
                    rute.Bus.JarakJalanKakiKm / KecepatanCadanganJalanKaki * 60.0;
        }
        else if (profil is ProfilRute.Sepeda or ProfilRute.JalanKaki)
        {
            // Rute cadangan (jalan raya) tidak punya durasi untuk profil ini -> pakai kecepatan rata-rata
            double kecepatan = profil == ProfilRute.Sepeda ? KecepatanCadanganSepeda : KecepatanCadanganJalanKaki;
            menit = rute.Profil == profil ? rute.DurasiMenit : rute.JarakKm / kecepatan * 60.0;
        }
        else
        {
            menit = moda switch
            {
                "Motor" => rute.DurasiMenit * (1 + (pengaliKemacetan - 1) / 2),
                "Bus" => rute.DurasiMenit * PengaliHalteBus * pengaliKemacetan + MenitTungguBus,
                _ => rute.DurasiMenit * pengaliKemacetan
            };
        }

        return Math.Max(1, (int)Math.Round(menit));
    }

    private static async Task<JsonDocument> AmbilJsonAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await Http.GetAsync(url, ct);

            if ((int)response.StatusCode == 429)
                throw new RoutingException("Server peta sedang sibuk. Tunggu sebentar lalu coba lagi.");

            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (RoutingException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Dibatalkan pemanggil (mis. user masih mengetik), bukan timeout
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new RoutingException("Server peta tidak merespons (timeout). Periksa koneksi internet lalu coba lagi.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new RoutingException("Tidak bisa terhubung ke server peta. Periksa koneksi internet kamu.", ex);
        }
        catch (JsonException ex)
        {
            throw new RoutingException("Data dari server peta tidak bisa dibaca. Coba lagi nanti.", ex);
        }
    }

    private static double ParseKoordinat(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number
            ? e.GetDouble()
            : double.Parse(e.GetString()!, CultureInfo.InvariantCulture);

    private static string Kapital(string? s) =>
        string.IsNullOrEmpty(s) ? "Lokasi" : char.ToUpper(s[0]) + s[1..].Replace('_', ' ');
}
