using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcoRoute.Models;
using Npgsql;
using NpgsqlTypes;

namespace EcoRoute.Services;

// Akses data moda dan riwayat perjalanan di database cloud.
// Selain membaca emission_factor, semua operasi lewat fungsi fn_* (lihat Database/03_functions.sql)
// karena role aplikasi tidak punya akses langsung ke tabel.
public class DatabaseService
{
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseService()
        : this(DatabaseConfig.DataSource)
    {
    }

    public DatabaseService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<List<ModaTransportasi>> GetModaTransportasiAsync()
    {
        var daftarModa = new List<ModaTransportasi>();

        await using var command = _dataSource.CreateCommand("""
            SELECT
                jenis_kendaraan,
                kategori,
                faktor_emisi_dasar,
                faktor_penalti_kemacetan
            FROM emission_factor
            ORDER BY emission_factor_id;
            """);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            string nama = reader.GetString(0);
            string kategori = reader.GetString(1);
            double faktorDasar = reader.GetDouble(2);
            double faktorPenalti = reader.GetDouble(3);

            string ikon = GetIkonModa(nama);

            ModaTransportasi moda = kategori switch
            {
                "bermotor" => new KendaraanBermotor(nama, ikon, faktorDasar, faktorPenalti),
                "aktif" => new TransportasiAktif(nama, ikon),
                _ => throw new InvalidOperationException(
                    $"Kategori moda tidak dikenal: {kategori}")
            };

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

        // Rute dan perjalanan disimpan dalam satu transaksi di dalam fungsi
        await using var command = _dataSource.CreateCommand("""
            SELECT fn_simpan_perjalanan(
                $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11);
            """);

        command.Parameters.Add(new() { Value = userId, NpgsqlDbType = NpgsqlDbType.Integer });
        command.Parameters.Add(new() { Value = namaAsal, NpgsqlDbType = NpgsqlDbType.Text });
        command.Parameters.Add(new() { Value = namaTujuan, NpgsqlDbType = NpgsqlDbType.Text });
        command.Parameters.Add(new() { Value = latAsal, NpgsqlDbType = NpgsqlDbType.Double });
        command.Parameters.Add(new() { Value = lngAsal, NpgsqlDbType = NpgsqlDbType.Double });
        command.Parameters.Add(new() { Value = latTujuan, NpgsqlDbType = NpgsqlDbType.Double });
        command.Parameters.Add(new() { Value = lngTujuan, NpgsqlDbType = NpgsqlDbType.Double });
        command.Parameters.Add(new() { Value = jarakKm, NpgsqlDbType = NpgsqlDbType.Double });
        command.Parameters.Add(new() { Value = estimasiWaktuMenit, NpgsqlDbType = NpgsqlDbType.Integer });
        command.Parameters.Add(new() { Value = modaDipilih, NpgsqlDbType = NpgsqlDbType.Text });
        command.Parameters.Add(new() { Value = totalEmisiKg, NpgsqlDbType = NpgsqlDbType.Double });

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<List<TripHistory>> GetTripHistoryAsync(int userId)
    {
        var history = new List<TripHistory>();

        await using var command = _dataSource.CreateCommand("""
            SELECT
                trip_id,
                tanggal,
                nama_asal,
                nama_tujuan,
                jarak_km,
                moda_dipilih,
                total_emisi_kg,
                titik_awal_lat,
                titik_awal_lng,
                titik_akhir_lat,
                titik_akhir_lng,
                estimasi_waktu_menit
            FROM fn_riwayat($1);
            """);

        command.Parameters.Add(new() { Value = userId, NpgsqlDbType = NpgsqlDbType.Integer });

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            history.Add(new TripHistory
            {
                TripId = reader.GetInt32(0),
                // timestamptz dibaca sebagai UTC, ditampilkan dalam jam lokal (WIB)
                Tanggal = reader.GetDateTime(1).ToLocalTime(),
                NamaAsal = reader.IsDBNull(2) ? "" : reader.GetString(2),
                NamaTujuan = reader.IsDBNull(3) ? "" : reader.GetString(3),
                JarakKm = reader.GetDouble(4),
                ModaDipilih = reader.GetString(5),
                TotalEmisiKg = reader.GetDouble(6),
                LatAsal = reader.GetDouble(7),
                LngAsal = reader.GetDouble(8),
                LatTujuan = reader.GetDouble(9),
                LngTujuan = reader.GetDouble(10),
                EstimasiWaktuMenit = reader.GetInt32(11)
            });
        }

        return history;
    }

    private static string GetIkonModa(string nama)
    {
        return nama switch
        {
            "Mobil" => "",
            "Motor" => "\U0001F6F5",
            "Bus" => "",
            "Kereta" => "",
            "Sepeda" => "\U0001F6B2",
            "Jalan Kaki" => "",
            _ => ""
        };
    }
}
