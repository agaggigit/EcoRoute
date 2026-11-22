
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using EcoRoute.Models;
using Microsoft.Data.Sqlite;

namespace EcoRoute.Services;

public class SqliteDatabaseService
{
    private readonly string _connectionString;

    public SqliteDatabaseService()
    {
        _connectionString =
            SqliteDatabaseInitializer.ConnectionString;
    }

    public async Task<bool> TestConnectionAsync()
    {
        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";

        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) == 1;
    }

    public async Task<List<string>> GetTransportModesAsync()
    {
        var modes = new List<string>();

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT jenis_kendaraan
            FROM emission_factor
            ORDER BY emission_factor_id;
            """;

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            modes.Add(reader.GetString(0));
        }

        return modes;
    }

    public async Task<List<ModaTransportasi>>
        GetModaTransportasiAsync()
    {
        var daftarModa = new List<ModaTransportasi>();

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                jenis_kendaraan,
                kategori,
                faktor_emisi_dasar,
                faktor_penalti_kemacetan
            FROM emission_factor
            ORDER BY emission_factor_id;
            """;

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            string nama = reader.GetString(0);
            string kategori = reader.GetString(1);

            double faktorDasar = reader.GetDouble(2);
            double faktorPenalti = reader.GetDouble(3);

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
        if (userId <= 0)
            throw new ArgumentException(
                "ID pengguna tidak valid.", nameof(userId));

        if (string.IsNullOrWhiteSpace(modaDipilih))
            throw new ArgumentException(
                "Moda transportasi belum dipilih.",
                nameof(modaDipilih));

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync();

        try
        {
            await using var routeCommand =
                connection.CreateCommand();

            routeCommand.Transaction = transaction;
            routeCommand.CommandText = """
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

            routeCommand.Parameters.AddWithValue(
                "@user_id", userId);
            routeCommand.Parameters.AddWithValue(
                "@nama_asal", namaAsal);
            routeCommand.Parameters.AddWithValue(
                "@nama_tujuan", namaTujuan);
            routeCommand.Parameters.AddWithValue(
                "@lat_asal", latAsal);
            routeCommand.Parameters.AddWithValue(
                "@lng_asal", lngAsal);
            routeCommand.Parameters.AddWithValue(
                "@lat_tujuan", latTujuan);
            routeCommand.Parameters.AddWithValue(
                "@lng_tujuan", lngTujuan);
            routeCommand.Parameters.AddWithValue(
                "@jarak", jarakKm);
            routeCommand.Parameters.AddWithValue(
                "@waktu", estimasiWaktuMenit);

            int routeId = Convert.ToInt32(
                await routeCommand.ExecuteScalarAsync());

            await using var tripCommand =
                connection.CreateCommand();

            tripCommand.Transaction = transaction;
            tripCommand.CommandText = """
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

            tripCommand.Parameters.AddWithValue(
                "@user_id", userId);
            tripCommand.Parameters.AddWithValue(
                "@route_id", routeId);
            tripCommand.Parameters.AddWithValue(
                "@moda", modaDipilih);
            tripCommand.Parameters.AddWithValue(
                "@emisi", totalEmisiKg);

            int tripId = Convert.ToInt32(
                await tripCommand.ExecuteScalarAsync());

            await transaction.CommitAsync();

            return tripId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<TripHistory>>
        GetTripHistoryAsync(int userId)
    {
        var history = new List<TripHistory>();

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        // Langsung dari tabel (bukan view v_riwayat_perjalanan) supaya koordinat ikut terbaca
        // tanpa perlu mengubah view di database yang sudah ada
        command.CommandText = """
            SELECT
                t.trip_id,
                t.tanggal,
                r.nama_asal,
                r.nama_tujuan,
                r.jarak,
                t.moda_dipilih,
                t.total_emisi_kg,
                r.titik_awal_lat,
                r.titik_awal_lng,
                r.titik_akhir_lat,
                r.titik_akhir_lng,
                r.estimasi_waktu
            FROM trip_history t
            JOIN route r ON r.route_id = t.route_id
            WHERE t.user_id = @user_id
            ORDER BY t.tanggal DESC, t.trip_id DESC;
            """;

        command.Parameters.AddWithValue("@user_id", userId);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            string tanggalText = reader.GetString(1);

            DateTime tanggal = DateTime.ParseExact(
                tanggalText,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture
            );

            var trip = new TripHistory
            {
                TripId = reader.GetInt32(0),
                Tanggal = tanggal,
                NamaAsal = reader.IsDBNull(2)
                    ? "" : reader.GetString(2),
                NamaTujuan = reader.IsDBNull(3)
                    ? "" : reader.GetString(3),
                JarakKm = reader.GetDouble(4),
                ModaDipilih = reader.GetString(5),
                TotalEmisiKg = reader.GetDouble(6),
                LatAsal = reader.GetDouble(7),
                LngAsal = reader.GetDouble(8),
                LatTujuan = reader.GetDouble(9),
                LngTujuan = reader.GetDouble(10),
                EstimasiWaktuMenit = reader.GetInt32(11)
            };

            history.Add(trip);
        }

        return history;
    }

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
