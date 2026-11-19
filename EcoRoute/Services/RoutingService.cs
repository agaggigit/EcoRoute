// Layanan peta pihak ketiga (REST API via HttpClient), semuanya gratis tanpa API key:
// - Nominatim (OpenStreetMap) : cari tempat dari nama -> koordinat (geocoding)
// - OSRM                      : hitung rute, jarak, dan durasi antara dua koordinat
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
    private const string OsrmUrl = "https://router.project-osrm.org/route/v1/driving";

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

    // Hitung rute jalan raya dari asal ke tujuan.
    public async Task<RouteResult> HitungRuteAsync(Place asal, Place tujuan)
    {
        string koordinat = string.Create(CultureInfo.InvariantCulture,
            $"{asal.Lng},{asal.Lat};{tujuan.Lng},{tujuan.Lat}");
        string url = $"{OsrmUrl}/{koordinat}?overview=full&geometries=geojson";

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

        return new RouteResult(asal, tujuan, jarakKm, durasiMenit, titik);
    }

    // Estimasi waktu tempuh per moda. Mobil memakai durasi asli OSRM,
    // moda lain memakai kecepatan rata-rata dalam kota (km/jam).
    // Kendaraan yang lewat jalan raya (Mobil, Motor, Bus) melambat saat macet.
    public static int EstimasiWaktuMenit(string moda, RouteResult rute,
        TingkatKemacetan kemacetan = TingkatKemacetan.Lancar)
    {
        double? kecepatan = moda switch
        {
            "Motor" => 30,
            "Bus" => 20,
            "Kereta" => 45,
            "Sepeda" => 15,
            "Jalan Kaki" => 5,
            _ => null // Mobil
        };

        double menit = kecepatan is null
            ? rute.DurasiMobilMenit
            : rute.JarakKm / kecepatan.Value * 60.0;

        if (moda is "Mobil" or "Motor" or "Bus")
            menit *= kemacetan.PengaliWaktu();

        return Math.Max(1, (int)Math.Round(menit));
    }

    private static async Task<JsonDocument> AmbilJsonAsync(string url)
    {
        try
        {
            using HttpResponseMessage response = await Http.GetAsync(url);

            if ((int)response.StatusCode == 429)
                throw new RoutingException("Server peta sedang sibuk. Tunggu sebentar lalu coba lagi.");

            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream);
        }
        catch (RoutingException)
        {
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
