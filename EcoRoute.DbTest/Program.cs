
using System;
using EcoRoute.Services;
using Npgsql;

Console.WriteLine("=== ECO ROUTE DATABASE TEST ===");
Console.WriteLine();

Console.Write("Masukkan password PostgreSQL: ");
string password = ReadPassword();

Console.WriteLine();

var builder = new NpgsqlConnectionStringBuilder
{
    Host = "localhost",
    Port = 5432,
    Database = "ecoroute",
    Username = "postgres",
    Password = password
};

try
{
    var db = new DatabaseService(builder.ConnectionString);

    Console.WriteLine("Mencoba koneksi PostgreSQL...");

    bool terhubung = await db.TestConnectionAsync();

    if (!terhubung)
        throw new InvalidOperationException("Tes koneksi gagal.");

    Console.WriteLine("KONEKSI BERHASIL!");
    Console.WriteLine();

    // PENGUJIAN 1: Ambil nama moda dari PostgreSQL
    Console.WriteLine("=== PENGUJIAN DATA MODA ===");

    var namaModa = await db.GetTransportModesAsync();

    foreach (var nama in namaModa)
    {
        Console.WriteLine("- " + nama);
    }

    Console.WriteLine();
    Console.WriteLine($"Total moda ditemukan: {namaModa.Count}");

    // PENGUJIAN 2: Buat objek OOP dari data PostgreSQL
    Console.WriteLine();
    Console.WriteLine("=== PENGUJIAN OOP DARI DATABASE ===");

    var daftarModa = await db.GetModaTransportasiAsync();

    foreach (var moda in daftarModa)
    {
        double emisi = moda.HitungEmisi(10);

        Console.WriteLine(
            $"{moda.Nama} | " +
            $"Class: {moda.GetType().Name} | " +
            $"Emisi 10 km: {emisi:F4} kg CO2"
        );
    }

    Console.WriteLine();
    Console.WriteLine($"Total objek moda: {daftarModa.Count}");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("PENGUJIAN GAGAL!");
    Console.WriteLine(ex.Message);
}

static string ReadPassword()
{
    string password = "";

    while (true)
    {
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);

        if (key.Key == ConsoleKey.Enter)
            break;

        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0)
                password = password[..^1];

            continue;
        }

        if (!char.IsControl(key.KeyChar))
            password += key.KeyChar;
    }

    return password;
}
