
using System;
using EcoRoute.Services;

try
{
    Console.WriteLine("=== PENGUJIAN LOGIN & REGISTER ECOROUTE ===");

    await SqliteDatabaseInitializer.InitializeAsync();

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

    Console.WriteLine("SEMUA PENGUJIAN AUTENTIKASI BERHASIL!");
}
catch (Exception ex)
{
    Console.WriteLine("PENGUJIAN GAGAL:");
    Console.WriteLine(ex.ToString());
    Environment.ExitCode = 1;
}
