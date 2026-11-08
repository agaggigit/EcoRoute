
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcoRoute.Models;
using Npgsql;

namespace EcoRoute.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ==========================================
    // 1. TEST KONEKSI POSTGRESQL
    // ==========================================

    public async Task<bool> TestConnectionAsync()
    {
        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command =
            new NpgsqlCommand("SELECT 1", connection);

        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) == 1;
    }

    // ==========================================
    // 2. AMBIL NAMA MODA TRANSPORTASI
    // ==========================================

    public async Task<List<string>> GetTransportModesAsync()
    {
        var modes = new List<string>();

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
            SELECT jenis_kendaraan
            FROM emission_factor
            ORDER BY emission_factor_id;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            modes.Add(reader.GetString(0));
        }

        return modes;
    }

    // ==========================================
    // 3. AMBIL MODA + FAKTOR EMISI DARI DATABASE
    // ==========================================

    public async Task<List<ModaTransportasi>> GetModaTransportasiAsync()
    {
        var daftarModa = new List<ModaTransportasi>();

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
            SELECT
                jenis_kendaraan,
                kategori,
                faktor_emisi_dasar,
                faktor_penalti_kemacetan
            FROM emission_factor
            ORDER BY emission_factor_id;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            string nama = reader.GetString(0);
            string kategori = reader.GetString(1);

            double faktorDasar =
                Convert.ToDouble(reader.GetDecimal(2));

            double faktorPenalti =
                Convert.ToDouble(reader.GetDecimal(3));

            string ikon = GetIkonModa(nama);

            ModaTransportasi moda;

            if (kategori == "bermotor")
            {
                moda = new KendaraanBermotor(
                    nama,
                    ikon,
                    faktorDasar,
                    faktorPenalti
                );
            }
            else if (kategori == "aktif")
            {
                moda = new TransportasiAktif(
                    nama,
                    ikon
                );
            }
            else
            {
                throw new InvalidOperationException(
                    $"Kategori moda tidak dikenal: {kategori}"
                );
            }

            daftarModa.Add(moda);
        }

        return daftarModa;
    }

    // ==========================================
    // 4. SIMPAN PERJALANAN KE DATABASE
    // ==========================================

    public async Task<int> SaveTripAsync(
        int userId,
        string namaAsal,
        string namaTujuan,
        double latAsal,
        double lngAsal,
        double latTujuan,
        double lngTujuan,
        double jarakKm,
        int estimasiWaktuMenit,
        string modaDipilih,
        double totalEmisiKg)
    {
        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        // Transaction memastikan kedua INSERT berhasil
        // atau keduanya dibatalkan.
        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            // ------------------------------------------
            // A. Simpan data rute
            // ------------------------------------------

            const string insertRoute = """
                INSERT INTO route (
                    user_id,
                    nama_asal,
                    nama_tujuan,
                    titik_awal_lat,
                    titik_awal_lng,
                    titik_akhir_lat,
                    titik_akhir_lng,
                    jarak,
                    estimasi_waktu
                )
                VALUES (
                    @user_id,
                    @nama_asal,
                    @nama_tujuan,
                    @lat_asal,
                    @lng_asal,
                    @lat_tujuan,
                    @lng_tujuan,
                    @jarak,
                    @waktu
                )
                RETURNING route_id;
                """;

            await using var routeCommand =
                new NpgsqlCommand(
                    insertRoute,
                    connection,
                    transaction
                );

            routeCommand.Parameters.AddWithValue(
                "user_id", userId);

            routeCommand.Parameters.AddWithValue(
                "nama_asal", namaAsal);

            routeCommand.Parameters.AddWithValue(
                "nama_tujuan", namaTujuan);

            routeCommand.Parameters.AddWithValue(
                "lat_asal", Convert.ToDecimal(latAsal));

            routeCommand.Parameters.AddWithValue(
                "lng_asal", Convert.ToDecimal(lngAsal));

            routeCommand.Parameters.AddWithValue(
                "lat_tujuan", Convert.ToDecimal(latTujuan));

            routeCommand.Parameters.AddWithValue(
                "lng_tujuan", Convert.ToDecimal(lngTujuan));

            routeCommand.Parameters.AddWithValue(
                "jarak", Convert.ToDecimal(jarakKm));

            routeCommand.Parameters.AddWithValue(
                "waktu", estimasiWaktuMenit);

            int routeId = Convert.ToInt32(
                await routeCommand.ExecuteScalarAsync());

            // ------------------------------------------
            // B. Simpan riwayat perjalanan
            // ------------------------------------------

            const string insertTrip = """
                INSERT INTO trip_history (
                    user_id,
                    route_id,
                    moda_dipilih,
                    total_emisi_kg
                )
                VALUES (
                    @user_id,
                    @route_id,
                    @moda,
                    @emisi
                )
                RETURNING trip_id;
                """;

            await using var tripCommand =
                new NpgsqlCommand(
                    insertTrip,
                    connection,
                    transaction
                );

            tripCommand.Parameters.AddWithValue(
                "user_id", userId);

            tripCommand.Parameters.AddWithValue(
                "route_id", routeId);

            tripCommand.Parameters.AddWithValue(
                "moda", modaDipilih);

            tripCommand.Parameters.AddWithValue(
                "emisi", Convert.ToDecimal(totalEmisiKg));

            int tripId = Convert.ToInt32(
                await tripCommand.ExecuteScalarAsync());

            // Kedua data berhasil disimpan.
            await transaction.CommitAsync();

            return tripId;
        }
        catch
        {
            // Jika salah satu INSERT gagal,
            // semua perubahan dibatalkan.
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ==========================================
    // 5. AMBIL RIWAYAT PERJALANAN
    // ==========================================

    public async Task<List<TripHistory>> GetTripHistoryAsync(
        int userId)
    {
        var history = new List<TripHistory>();

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
            SELECT
                trip_id,
                tanggal,
                nama_asal,
                nama_tujuan,
                jarak_km,
                moda_dipilih,
                total_emisi_kg
            FROM v_riwayat_perjalanan
            WHERE user_id = @user_id
            ORDER BY tanggal DESC, trip_id DESC;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "user_id", userId);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var trip = new TripHistory
            {
                TripId = reader.GetInt32(0),

                Tanggal = reader.GetDateTime(1),

                NamaAsal = reader.IsDBNull(2)
                    ? ""
                    : reader.GetString(2),

                NamaTujuan = reader.IsDBNull(3)
                    ? ""
                    : reader.GetString(3),

                JarakKm = Convert.ToDouble(
                    reader.GetDecimal(4)),

                ModaDipilih = reader.GetString(5),

                TotalEmisiKg = Convert.ToDouble(
                    reader.GetDecimal(6))
            };

            history.Add(trip);
        }

        return history;
    }

    // ==========================================
    // 6. IKON MODA TRANSPORTASI
    // ==========================================

    private static string GetIkonModa(string nama)
    {
        return nama switch
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
}
