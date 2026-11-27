
using System;
using System.Linq;
using EcoRoute.Services;

try
{
    // Memakai database dari environment variable ECOROUTE_DB_CONNECTION
    Console.WriteLine("=== PENGUJIAN DATABASE CLOUD ECOROUTE ===");

    var auth = new AuthService();

    string email = $"test-{Guid.NewGuid():N}@ecoroute.local";
    string password = "EcoRoute123!";

    // TEST 1: Register akun baru
    var user = await auth.RegisterAsync(
        "Pengguna Pengujian",
        email,
        password
    );

    Console.WriteLine($"Register berhasil: ID {user.UserId}");

    // TEST 2: Login dengan password benar
    var login = await auth.LoginAsync(email, password);

    if (login is null || login.UserId != user.UserId)
        throw new Exception("Login dengan password benar gagal.");

    Console.WriteLine("Login password benar: BERHASIL");

    // TEST 3: Login dengan password salah
    var loginSalah = await auth.LoginAsync(
        email,
        "PasswordSalah123"
    );

    if (loginSalah is not null)
        throw new Exception("Password salah ternyata diterima.");

    Console.WriteLine("Login password salah: DITOLAK");

    // TEST 4: Email duplikat
    bool duplikatDitolak = false;

    try
    {
        await auth.RegisterAsync(
            "Pengguna Duplikat",
            email,
            password
        );
    }
    catch (InvalidOperationException)
    {
        duplikatDitolak = true;
    }

    if (!duplikatDitolak)
        throw new Exception("Email duplikat ternyata diterima.");

    Console.WriteLine("Email duplikat: DITOLAK");

    // TEST 5: Akun tidak ditemukan
    var tidakAda = await auth.LoginAsync(
        "tidakada@ecoroute.local",
        password
    );

    if (tidakAda is not null)
        throw new Exception("Akun tidak dikenal bisa login.");

    Console.WriteLine("Akun tidak ditemukan: DITOLAK");

    // TEST 6: Daftar moda dari tabel emission_factor
    var db = new DatabaseService();
    var moda = await db.GetModaTransportasiAsync();

    if (moda.Count == 0)
        throw new Exception("Data moda kosong.");

    Console.WriteLine($"Daftar moda: {string.Join(", ", moda.Select(m => m.Nama))}");

    // TEST 7: Simpan perjalanan lalu baca lagi lewat riwayat
    int tripId = await db.SaveTripAsync(
        user.UserId, "Tugu Yogyakarta", "Malioboro",
        -7.7829, 110.3671, -7.7926, 110.3658,
        1.4, 6, "Motor", 0.144);

    var riwayat = await db.GetTripHistoryAsync(user.UserId);

    if (riwayat.Count != 1 || riwayat[0].TripId != tripId ||
        riwayat[0].ModaDipilih != "Motor" ||
        Math.Abs((riwayat[0].Tanggal - DateTime.Now).TotalMinutes) > 5)
        throw new Exception("Riwayat tidak sesuai dengan perjalanan yang disimpan.");

    Console.WriteLine($"Simpan & baca riwayat: BERHASIL (trip {tripId}, {riwayat[0].Tanggal:dd MMM HH:mm})");

    // TEST 8: Riwayat pengguna lain tidak ikut terbaca
    var riwayatLain = await db.GetTripHistoryAsync(user.UserId + 100000);

    if (riwayatLain.Count != 0)
        throw new Exception("Riwayat pengguna lain ikut terbaca.");

    Console.WriteLine("Riwayat terpisah per pengguna: BERHASIL");

    Console.WriteLine("SEMUA PENGUJIAN DATABASE BERHASIL!");
}
catch (Exception ex)
{
    Console.WriteLine("PENGUJIAN GAGAL:");
    Console.WriteLine(ex.ToString());
    Environment.ExitCode = 1;
}
