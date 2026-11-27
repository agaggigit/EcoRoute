using System;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Npgsql;

namespace EcoRoute.Services;

// Koneksi ke database PostgreSQL di cloud (Neon).
// Skema, data awal, dan hak akses dibuat sekali oleh admin lewat skrip di folder Database/,
// jadi aplikasi tidak menjalankan CREATE TABLE. Role yang dipakai aplikasi (ecoroute_app)
// hanya boleh membaca emission_factor dan memanggil fungsi fn_* (lihat 03_functions.sql).
public static class DatabaseConfig
{
    private const string NamaEnv = "ECOROUTE_DB_CONNECTION";
    private const string NamaMetadata = "EcoRouteDbConnection";

    private static readonly Lazy<NpgsqlDataSource> _dataSource =
        new(() => NpgsqlDataSource.Create(BuatConnectionString()));

    // Satu data source untuk seluruh aplikasi supaya koneksi dipakai ulang (connection pooling).
    public static NpgsqlDataSource DataSource => _dataSource.Value;

    // Urutan: environment variable ECOROUTE_DB_CONNECTION (development / database uji),
    // lalu connection string yang ditanam saat build. Keduanya tidak pernah ditulis di source code.
    private static string BuatConnectionString()
    {
        string? mentah = Environment.GetEnvironmentVariable(NamaEnv);

        if (string.IsNullOrWhiteSpace(mentah))
        {
            mentah = typeof(DatabaseConfig).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == NamaMetadata)?.Value;
        }

        if (string.IsNullOrWhiteSpace(mentah))
        {
            throw new InvalidOperationException(
                "Koneksi database belum dikonfigurasi. " +
                $"Atur environment variable {NamaEnv} atau gunakan exe hasil build resmi.");
        }

        var builder = new NpgsqlConnectionStringBuilder(KeFormatNpgsql(mentah))
        {
            SslMode = SslMode.Require,
            // Neon "tidur" saat idle; koneksi pertama butuh beberapa detik untuk membangunkannya
            Timeout = 20,
            CommandTimeout = 30,
            // Neon memutus koneksi setelah sekitar 5 menit idle, jadi koneksi menganggur
            // dibuang lebih awal dari pool supaya tidak ada yang dipakai dalam keadaan putus
            ConnectionIdleLifetime = 60,
            MaxPoolSize = 10,
            ApplicationName = "EcoRoute"
        };

        return builder.ConnectionString;
    }

    // Dashboard Neon memberi format URI (postgresql://user:pass@host/db?sslmode=require),
    // sedangkan Npgsql memakai format Host=...;Username=...
    internal static string KeFormatNpgsql(string connectionString)
    {
        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var uri = new Uri(connectionString);
        string[] userInfo = uri.UserInfo.Split(':', 2);

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null
        }.ConnectionString;
    }

    // Dipanggil saat aplikasi dibuka (tanpa ditunggu) supaya database sudah bangun
    // ketika pengguna selesai mengetik email dan password.
    public static async Task PanaskanAsync()
    {
        try
        {
            await using var command = DataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync();
        }
        catch
        {
            // Diabaikan: kalau memang gagal, pesan yang jelas muncul saat login.
        }
    }

    // Pesan untuk pengguna; detail teknis koneksi tidak perlu ditampilkan.
    public static string PesanRamah(Exception ex) => ex switch
    {
        InvalidOperationException { InnerException: null } => ex.Message,
        NpgsqlException { InnerException: SocketException or TimeoutException }
            or NpgsqlException { IsTransient: true }
            or TimeoutException =>
            "Tidak bisa terhubung ke database. Periksa koneksi internet lalu coba lagi.",
        PostgresException pg =>
            $"Database menolak permintaan ({pg.SqlState}).",
        NpgsqlException =>
            "Tidak bisa terhubung ke database. Periksa koneksi internet lalu coba lagi.",
        _ => ex.Message
    };
}
