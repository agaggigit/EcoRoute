
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace EcoRoute.Services;

public record AuthUser(int UserId, string Nama, string Email);

public class AuthService
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public async Task<AuthUser> RegisterAsync(
        string nama, string email, string password)
    {
        nama = nama.Trim();
        email = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(nama))
            throw new ArgumentException("Nama wajib diisi.");

        if (string.IsNullOrWhiteSpace(email) ||
            !System.Net.Mail.MailAddress.TryCreate(email, out var alamat) ||
            alamat.Address != email)
            throw new ArgumentException("Format email tidak valid.");

        if (password.Length < 8)
            throw new ArgumentException(
                "Password minimal 8 karakter.");

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations,
            HashAlgorithmName.SHA256, HashSize);

        string passwordHash =
            $"PBKDF2-SHA256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";

        await using var connection = new SqliteConnection(
            SqliteDatabaseInitializer.ConnectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (nama_user, email_user, password_hash)
            VALUES (@nama, @email, @hash)
            RETURNING user_id;
            """;

        command.Parameters.AddWithValue("@nama", nama);
        command.Parameters.AddWithValue("@email", email);
        command.Parameters.AddWithValue("@hash", passwordHash);

        try
        {
            int userId = Convert.ToInt32(
                await command.ExecuteScalarAsync());

            return new AuthUser(userId, nama, email);
        }
        catch (SqliteException ex)
            when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException(
                "Email sudah terdaftar atau data tidak valid.", ex);
        }
    }

    public async Task<AuthUser?> LoginAsync(
        string email, string password)
    {
        email = email.Trim().ToLowerInvariant();

        await using var connection = new SqliteConnection(
            SqliteDatabaseInitializer.ConnectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT user_id, nama_user, email_user, password_hash
            FROM users
            WHERE email_user = @email COLLATE NOCASE;
            """;

        command.Parameters.AddWithValue("@email", email);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync() || reader.IsDBNull(3))
            return null;

        string stored = reader.GetString(3);
        string[] parts = stored.Split('$');

        if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256" ||
            !int.TryParse(parts[1], out int iterations) ||
            iterations < 100_000 || iterations > 2_000_000)
            return null;

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (salt.Length != SaltSize || expected.Length != HashSize)
            return null;

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations,
            HashAlgorithmName.SHA256, expected.Length);

        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            return null;

        return new AuthUser(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2));
    }
}
