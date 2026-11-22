
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace EcoRoute.Services;

public static class SqliteDatabaseInitializer
{
    // ECOROUTE_DB_PATH (opsional) memakai file database lain, misalnya untuk uji coba / demo
    // tanpa menyentuh riwayat asli di LocalAppData.
    public static string DatabasePath { get; } =
        Environment.GetEnvironmentVariable("ECOROUTE_DB_PATH") is { Length: > 0 } jalurKhusus
            ? jalurKhusus
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EcoRoute",
                "ecoroute.db");

    public static string ConnectionString =>
        new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true
        }.ToString();

    public static async Task InitializeAsync()
    {
        string? directory = Path.GetDirectoryName(DatabasePath);

        if (directory is null)
            throw new InvalidOperationException("Lokasi database tidak valid.");

        Directory.CreateDirectory(directory);

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        await using var transaction = connection.BeginTransaction();

        const string schema = """
            CREATE TABLE IF NOT EXISTS emission_factor (
                emission_factor_id INTEGER PRIMARY KEY,
                jenis_kendaraan TEXT NOT NULL UNIQUE,
                kategori TEXT NOT NULL
                    CHECK (kategori IN ('bermotor', 'aktif')),
                faktor_emisi_dasar REAL NOT NULL
                    CHECK (faktor_emisi_dasar >= 0),
                faktor_penalti_kemacetan REAL NOT NULL DEFAULT 1.0
                    CHECK (faktor_penalti_kemacetan >= 1.0),
                updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CHECK (kategori = 'bermotor' OR faktor_emisi_dasar = 0)
            );

            CREATE TABLE IF NOT EXISTS users (
                user_id INTEGER PRIMARY KEY,
                nama_user TEXT NOT NULL,
                email_user TEXT NOT NULL UNIQUE,
                password_hash TEXT,
                total_eco_score INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS vehicle (
                vehicle_id INTEGER PRIMARY KEY,
                user_id INTEGER NOT NULL
                    REFERENCES users(user_id) ON DELETE CASCADE,
                emission_factor_id INTEGER NOT NULL
                    REFERENCES emission_factor(emission_factor_id),
                tahun_kendaraan INTEGER
                    CHECK (tahun_kendaraan BETWEEN 1950 AND 2100)
            );

            CREATE TABLE IF NOT EXISTS route (
                route_id INTEGER PRIMARY KEY,
                user_id INTEGER NOT NULL
                    REFERENCES users(user_id) ON DELETE CASCADE,
                nama_asal TEXT,
                nama_tujuan TEXT,
                titik_awal_lat REAL NOT NULL
                    CHECK (titik_awal_lat BETWEEN -90 AND 90),
                titik_awal_lng REAL NOT NULL
                    CHECK (titik_awal_lng BETWEEN -180 AND 180),
                titik_akhir_lat REAL NOT NULL
                    CHECK (titik_akhir_lat BETWEEN -90 AND 90),
                titik_akhir_lng REAL NOT NULL
                    CHECK (titik_akhir_lng BETWEEN -180 AND 180),
                jarak REAL NOT NULL CHECK (jarak >= 0),
                estimasi_waktu INTEGER NOT NULL
                    CHECK (estimasi_waktu >= 0),
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS route_segment (
                segment_id INTEGER PRIMARY KEY,
                route_id INTEGER NOT NULL
                    REFERENCES route(route_id) ON DELETE CASCADE,
                urutan_segmen INTEGER NOT NULL
                    CHECK (urutan_segmen >= 1),
                jarak_segmen REAL NOT NULL
                    CHECK (jarak_segmen >= 0),
                UNIQUE(route_id, urutan_segmen)
            );

            CREATE TABLE IF NOT EXISTS trip_history (
                trip_id INTEGER PRIMARY KEY,
                user_id INTEGER NOT NULL
                    REFERENCES users(user_id) ON DELETE CASCADE,
                vehicle_id INTEGER
                    REFERENCES vehicle(vehicle_id) ON DELETE SET NULL,
                route_id INTEGER NOT NULL
                    REFERENCES route(route_id) ON DELETE CASCADE,
                tanggal TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                moda_dipilih TEXT NOT NULL
                    REFERENCES emission_factor(jenis_kendaraan)
                    ON UPDATE CASCADE,
                total_emisi_kg REAL NOT NULL
                    CHECK (total_emisi_kg >= 0)
            );

            CREATE TABLE IF NOT EXISTS trip_mode_comparison (
                comparison_id INTEGER PRIMARY KEY,
                trip_id INTEGER NOT NULL
                    REFERENCES trip_history(trip_id) ON DELETE CASCADE,
                jenis_kendaraan_bandingan TEXT NOT NULL
                    REFERENCES emission_factor(jenis_kendaraan)
                    ON UPDATE CASCADE,
                estimasi_emisi_kg REAL NOT NULL
                    CHECK (estimasi_emisi_kg >= 0),
                UNIQUE(trip_id, jenis_kendaraan_bandingan)
            );

            CREATE TABLE IF NOT EXISTS trip_segment_snapshot (
                snapshot_id INTEGER PRIMARY KEY,
                trip_id INTEGER NOT NULL
                    REFERENCES trip_history(trip_id) ON DELETE CASCADE,
                segment_id INTEGER NOT NULL
                    REFERENCES route_segment(segment_id) ON DELETE CASCADE,
                tingkat_kemacetan TEXT
                    CHECK (tingkat_kemacetan IN
                        ('lancar', 'sedang', 'padat', 'macet')),
                intensitas_polusi REAL,
                recorded_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_vehicle_user_id
                ON vehicle(user_id);

            CREATE INDEX IF NOT EXISTS idx_vehicle_emission_factor_id
                ON vehicle(emission_factor_id);

            CREATE INDEX IF NOT EXISTS idx_route_user_id
                ON route(user_id);

            CREATE INDEX IF NOT EXISTS idx_route_segment_route_id
                ON route_segment(route_id);

            CREATE INDEX IF NOT EXISTS idx_trip_history_user_tanggal
                ON trip_history(user_id, tanggal DESC);

            CREATE INDEX IF NOT EXISTS idx_trip_history_vehicle_id
                ON trip_history(vehicle_id);

            CREATE INDEX IF NOT EXISTS idx_trip_history_route_id
                ON trip_history(route_id);

            CREATE INDEX IF NOT EXISTS idx_trip_history_moda_dipilih
                ON trip_history(moda_dipilih);

            CREATE INDEX IF NOT EXISTS idx_trip_segment_snapshot_trip_id
                ON trip_segment_snapshot(trip_id);

            CREATE INDEX IF NOT EXISTS idx_trip_segment_snapshot_segment_id
                ON trip_segment_snapshot(segment_id);

            CREATE VIEW IF NOT EXISTS v_riwayat_perjalanan AS
            SELECT
                t.trip_id,
                t.user_id,
                u.nama_user,
                t.tanggal,
                r.nama_asal,
                r.nama_tujuan,
                r.jarak AS jarak_km,
                r.estimasi_waktu AS estimasi_waktu_menit,
                t.moda_dipilih,
                t.total_emisi_kg
            FROM trip_history t
            JOIN users u ON u.user_id = t.user_id
            JOIN route r ON r.route_id = t.route_id;
            """;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = schema;
            await command.ExecuteNonQueryAsync();
        }

        const string seed = """
            INSERT OR IGNORE INTO emission_factor
                (jenis_kendaraan, kategori,
                 faktor_emisi_dasar, faktor_penalti_kemacetan)
            VALUES
                ('Motor', 'bermotor', 0.1030, 1.1500),
                ('Mobil', 'bermotor', 0.1920, 1.3500),
                ('Bus', 'bermotor', 0.0890, 1.2500),
                ('Sepeda', 'aktif', 0.0000, 1.0000),
                ('Jalan Kaki', 'aktif', 0.0000, 1.0000);

            -- Kereta sudah dihapus dari aplikasi. Baris lama ikut dihapus,
            -- kecuali masih dipakai riwayat perjalanan (tetap disimpan supaya riwayat tidak rusak).
            DELETE FROM emission_factor
            WHERE jenis_kendaraan = 'Kereta'
              AND NOT EXISTS (SELECT 1 FROM trip_history WHERE moda_dipilih = 'Kereta')
              AND NOT EXISTS (SELECT 1 FROM trip_mode_comparison WHERE jenis_kendaraan_bandingan = 'Kereta')
              AND NOT EXISTS (SELECT 1 FROM vehicle v WHERE v.emission_factor_id = emission_factor.emission_factor_id);
            """;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = seed;
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }
}
