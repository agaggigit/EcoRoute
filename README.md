<p align="center"><img src="EcoRoute/Assets/logo-256.png" width="96" alt="Logo EcoRoute"></p>

# EcoRoute

> *Jalan hemat, bumi sehat.*

EcoRoute adalah aplikasi desktop Windows (C# WPF) untuk menghitung dan membandingkan jejak karbon perjalanan. Pengguna masuk dengan akun, lalu memilih titik awal, tujuan, dan moda transportasi. Aplikasi mengambil rute asli dari peta, membaca kondisi lalu lintas tiap ruas secara otomatis, menghitung emisi CO₂ setiap moda, lalu menyimpan perjalanan yang dipakai ke riwayat milik akun tersebut.

**Tema:** Climate Action, kalkulator dan pelacak jejak karbon perjalanan.

## Fitur

- **Register dan Login**: akun disimpan di database lokal, password disimpan sebagai hash PBKDF2-SHA256 (bukan teks asli).
- **Riwayat dan Eco Score per akun**: tiap akun hanya melihat perjalanannya sendiri.
- **Peta interaktif** (Leaflet + OpenStreetMap) dengan pin tempat populer Yogyakarta dan filter kategori.
- **Pencarian tempat** lewat Nominatim (OpenStreetMap), dengan saran lokasi saat mengetik.
- **Pilih lokasi dari peta**: klik peta di mana saja. Titik yang diklik diubah jadi nama tempat atau jalan terdekat, dan dibulatkan ke tempat populer kalau jaraknya dekat.
- **Rute asli per moda** (jarak, durasi, dan garis rute) dari OSRM: jalan raya untuk Mobil dan Motor, serta jalur khusus untuk Sepeda dan Jalan Kaki. Titik awal bisa diketik atau ketik koordinat langsung (contoh -7.7829, 110.3671).
- **Trayek Trans Jogja asli untuk Bus**: aplikasi mencari satu trayek yang langsung menghubungkan titik awal dan tujuan, lalu menampilkan halte naik, halte turun, jalur bus, dan jalan kaki ke serta dari halte. Data 20 trayek diambil dari OpenStreetMap (detail di [docs/OOP.md](docs/OOP.md#trayek-trans-jogja)).
- **Perbandingan emisi 5 moda**: Mobil, Motor, Bus, Sepeda, Jalan Kaki, masing-masing dengan jarak rutenya sendiri, beserta **rekomendasi** moda dengan emisi paling rendah yang waktu tempuhnya masih wajar (bukan selalu jalan kaki atau sepeda).
- **Kemacetan live per ruas**: rute dipecah menjadi 3–10 segmen, kecepatan tiap segmen diambil dari TomTom Traffic Flow, lalu emisi kendaraan bermotor dihitung per segmen (Lancar, Sedang, Padat, Macet). Tanpa API key atau saat TomTom gagal, kemacetan ditebak dari jam dan aplikasi tetap berjalan.
- **Akun dan riwayat perjalanan** tersimpan di database PostgreSQL cloud (Neon), jadi bisa dibuka dari komputer mana pun. Perjalanan dicatat lewat tombol *Pakai rute ini*, dan bisa diklik dari menu *Riwayat* untuk menghitung ulang rute yang sama dengan kondisi lalu lintas saat ini.
- **Eco Score**: total CO₂ yang dihemat dibanding naik mobil, poin, level (Bibit → Tunas → Pohon → Hutan), dan perbandingan "setara X pohon menyerap CO₂ selama setahun". CO₂ yang dihemat dihitung dalam kondisi lalu lintas lancar, jadi skor tidak naik-turun karena macet.
- **Penanganan error** yang ramah pengguna untuk koneksi internet terputus, lokasi tidak ditemukan, atau database gagal dimuat.
- **Logout** dengan dialog konfirmasi. Riwayat tidak hilang saat logout.

## Teknologi

| Bagian | Teknologi |
|---|---|
| Aplikasi | C# .NET 10, WPF, pola MVVM |
| Peta | WebView2 + Leaflet 1.9.4 + tile OpenStreetMap |
| Web API (HttpClient) | Nominatim (geocoding), OSRM (routing), TomTom Traffic Flow (kemacetan live) |
| Database | PostgreSQL cloud ([Neon](https://neon.tech), region Singapura) + Npgsql |
| Pengujian | xUnit |
| Keamanan | Hash password PBKDF2-SHA256, 210.000 iterasi, salt acak; role database aplikasi tanpa akses langsung ke tabel |

Nominatim dan OSRM gratis tanpa API key. TomTom membutuhkan API key (free tier, opsional); tanpa key, kemacetan ditebak dari jam.

## Struktur Project

```
EcoRoute/
├── EcoRoute/                  Aplikasi WPF
│   ├── Models/                Domain & OOP (ModaTransportasi, KendaraanBermotor, TransportasiAktif,
│   │                          TingkatKemacetan, KondisiLaluLintas, EcoScoreCalculator, Place, RouteResult, ...)
│   ├── ViewModels/            MainViewModel (state A–E dan Loading), BaseViewModel, RelayCommand
│   ├── Views/Components/      IdleView, PlaceCardView, RouteInputView, EmissionResultView, EcoScoreView, MapView
│   ├── Services/              AuthService, RoutingService (Nominatim + OSRM), TrafficService (TomTom),
│   │                          DatabaseConfig, DatabaseService (Npgsql)
│   ├── Database/              Skrip SQL: skema, data awal, fungsi, hak akses role aplikasi
│   ├── LoginWindow, LogoutDialog, MainWindow
│   └── Maps/map.html          Peta Leaflet yang dimuat di WebView2
├── EcoRoute.Tests/            Unit test xUnit
├── EcoRoute.DbTest/           Program konsol untuk menguji Register, Login, dan riwayat ke database cloud
└── docs/                      Dokumentasi (OOP, spesifikasi, halaman project)
```

## Kebutuhan Sistem

- Windows 10 (versi 2004 / build 19041) atau Windows 11, 64-bit
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (sudah terpasang bawaan di Windows 11)
- Koneksi internet (untuk database, peta, pencarian, rute, dan data lalu lintas)
- API key TomTom (opsional, untuk data kemacetan live)
- .NET 10 SDK (hanya untuk build dari source)

## Cara Menjalankan

### 1. Database (PostgreSQL cloud)

Akun dan riwayat perjalanan disimpan di PostgreSQL yang di-host di [Neon](https://neon.tech). Aplikasi terhubung langsung ke database tanpa backend terpisah.

**Exe hasil build resmi sudah membawa koneksi database**, jadi tinggal dijalankan. Langkah di bawah hanya untuk menjalankan dari source code atau memasang database sendiri.

**Keamanan.** Connection string tidak pernah ditulis di repository. Aplikasi memakai role `ecoroute_app` yang **tidak punya akses ke tabel**. Role ini hanya boleh membaca tabel `emission_factor` dan memanggil empat fungsi `SECURITY DEFINER`:

| Fungsi | Kegunaan |
|---|---|
| `fn_register` | Membuat akun baru (hash password dibuat di aplikasi) |
| `fn_ambil_login` | Mengambil data login untuk **satu** email; daftar akun tidak bisa dibaca |
| `fn_simpan_perjalanan` | Menyimpan rute dan perjalanan dalam satu transaksi |
| `fn_riwayat` | Mengambil riwayat perjalanan satu pengguna |

Karena connection string tertanam di exe dan bisa dibongkar, pembatasan ini memastikan kredensial tersebut tidak bisa dipakai untuk membaca semua akun, mengubah data, atau menghapus tabel. Pengembangan selanjutnya adalah backend API sebagai lapisan autentikasi di antara aplikasi dan database.

**Memasang database sendiri** (sekali, sebagai pemilik database):

1. Buat project di Neon, lalu jalankan `EcoRoute/Database/01_schema.sql`, `02_seed.sql`, dan `03_functions.sql`.
2. Buat role aplikasi dengan password acak: `CREATE ROLE ecoroute_app LOGIN PASSWORD '...';`
3. Jalankan `EcoRoute/Database/04_roles.sql`.

**Menjalankan dari source.** Atur environment variable `ECOROUTE_DB_CONNECTION` berisi connection string role `ecoroute_app`. Bisa berupa format Npgsql (`Host=...;Database=...;Username=ecoroute_app;Password=...`) atau URI dari dashboard Neon (`postgresql://...`):

```powershell
[Environment]::SetEnvironmentVariable('ECOROUTE_DB_CONNECTION', 'isi-connection-string', 'User')
```

Buka ulang terminal atau Visual Studio setelah mengatur variabel ini. Variabel yang sama dipakai untuk mengarahkan aplikasi ke database lain, misalnya database uji.

### 2. Atur API key TomTom (opsional)

Kondisi lalu lintas diambil dari [TomTom Traffic Flow API](https://developer.tomtom.com/traffic-api/documentation/traffic-flow/flow-segment-data). Tanpa key, aplikasi tetap jalan dan kemacetan ditebak dari jam.

1. Daftar gratis di [developer.tomtom.com](https://developer.tomtom.com/) lalu salin API key dari dashboard (free tier: 2.500 request/hari, satu kali hitung rute memakai 3–10 request, satu per segmen rute).
2. Simpan sebagai environment variable `TOMTOM_API_KEY`, lalu buka ulang terminal atau Visual Studio:

   ```powershell
   setx TOMTOM_API_KEY "isi-api-key-kamu"
   ```

Key tidak pernah ditulis di source code. Saat build `.exe`, key dari environment variable ikut ditanam ke dalam exe (lihat [Build `.exe`](#build-exe)).

### 3a. Jalankan dari file `.exe`

1. Unduh hasil build atau buat sendiri (lihat [Build `.exe`](#build-exe)).
2. Pastikan `EcoRoute.exe` dan folder `Maps` berada di folder yang sama.
3. Jalankan `EcoRoute.exe`, lalu buat akun lewat Register atau masuk dengan akun yang sudah ada.

### 3b. Jalankan dari source

```bash
dotnet run --project EcoRoute/EcoRoute.csproj
```

Bisa juga dengan membuka `EcoRoute.slnx` di Visual Studio 2026 (mendukung .NET 10 dan format `.slnx`) lalu tekan **F5**.

## Panduan Penggunaan

0. **Register / Login.** Pada halaman awal, klik *Belum punya akun? Daftar*, isi nama, email, dan password (minimal 8 karakter), lalu klik *Daftar*. Setelah berhasil kamu langsung masuk ke dashboard. Untuk pemakaian berikutnya, cukup *Masuk* dengan email dan password yang sama.
1. **Cari tujuan.** Ketik nama tempat di kolom pencarian lalu tekan Enter (atau pilih dari saran), klik pin/chip tempat populer, atau klik langsung di peta. Detail lokasi tampil di card kiri. Beranda juga menampilkan strip Eco Score dan tiga perjalanan terakhir yang bisa diklik untuk langsung dihitung ulang.
2. **Buat rute.** Tekan *Rute ke sini*. Tempat yang dipilih otomatis menjadi tujuan.
3. **Isi titik awal.** Kolom yang sedang aktif diberi garis hijau, dan klik peta mengisi kolom tersebut. Setelah titik awal terisi, klik berikutnya otomatis mengisi tujuan. Bisa juga mengetik nama tempat lalu Enter (penandanya muncul di peta dulu sebelum menghitung), atau mengetik koordinat (contoh `-7.7829, 110.3671`). Tombol tukar menukar titik awal dan tujuan.
4. **Pilih moda.** Kondisi lalu lintas tidak perlu dipilih: saat rute dihitung, kemacetan tiap ruas diambil live dari TomTom (kalau API key diatur). Kalau TomTom tidak bisa dihubungi, muncul notif singkat dan kemacetan ditebak dari jam.
5. **Tekan *Hitung Emisi*.** Garis rute tampil di peta, dan card menampilkan:
   - waktu tempuh (warnanya mengikuti kondisi lalu lintas: hijau, kuning, oranye, merah), jarak, dan emisi CO₂,
   - perbandingan waktu dan emisi kelima moda, dengan badge *Rekomendasi*,
   - saran singkat. Penjelasan lengkapnya muncul saat ikon ⓘ di-hover.
6. **Ganti moda atau lokasi.** Klik ikon moda atau baris moda di daftar perbandingan untuk menghitung ulang secara langsung tanpa memanggil ulang API. Mengganti titik awal atau tujuan (klik peta, Enter, atau saran) langsung menghitung ulang rutenya.
7. **Tekan *Pakai rute ini*** untuk mencatat perjalanan ke Riwayat dan Eco Score. Hitung Emisi saja tidak dicatat, jadi mencoba-coba rute tidak menambah poin.
8. Buka menu **Riwayat** di rail kiri untuk melihat perjalanan yang tercatat. Klik salah satu perjalanan untuk menghitung ulang rute yang sama dengan kondisi lalu lintas saat ini. Riwayat hanya menampilkan perjalanan milik akun yang sedang login.
9. Buka menu **Eco Score** untuk melihat level (Bibit → Tunas → Pohon → Hutan), poin, CO₂ yang dihemat dibanding naik mobil, dan setara berapa pohon. 1 kg CO₂ yang dihemat bernilai 10 poin. Cara hitungnya ada di tooltip ikon ⓘ.
10. Ikon tumpukan di kanan atas peta membuka **keterangan peta**: warna lalu lintas, penanda rute, dan warna kategori tempat.
11. Klik **Logout** di bagian bawah sidebar, lalu pilih *Ya, keluar* untuk kembali ke halaman Login.

## Build `.exe`

Build menghasilkan `EcoRoute.exe` mandiri (self-contained, tidak perlu install .NET di komputer tujuan):

```bash
dotnet publish EcoRoute/EcoRoute.csproj -p:PublishProfile=win-x64
```

Kalau environment variable `TOMTOM_API_KEY` dan `ECOROUTE_DB_CONNECTION` sudah diatur, keduanya otomatis ditanam ke exe, sehingga data kemacetan live dan database jalan di komputer mana pun tanpa konfigurasi. Key juga bisa diberikan langsung dengan `-p:TomTomApiKey=isi-api-key`. Key yang tertanam bisa dibongkar dari exe, jadi pakai key free tier khusus untuk project ini.

Hasilnya ada di `EcoRoute/bin/publish/win-x64/`: `EcoRoute.exe` (±70 MB) dan folder `Maps/`. Bagikan keduanya bersama-sama, misalnya dalam satu file zip. Komputer tujuan hanya perlu WebView2 Runtime (bawaan Windows 11).

## Pengujian

```bash
dotnet test EcoRoute.slnx
```

Unit test (150 kasus uji) mencakup rumus emisi per moda dan penalti kemacetan, tebakan kondisi lalu lintas, pembacaan data TomTom dan pembagian segmen rute, estimasi waktu tempuh, perhitungan Eco Score, pratinjau titik awal dan tujuan, klik peta dan nama tempat dari titik, rekomendasi moda, saran lokasi, pencatatan perjalanan, dan perpindahan panel. 

Alur Register, Login, daftar moda, simpan perjalanan, dan riwayat terhadap database cloud bisa diuji dengan (butuh `ECOROUTE_DB_CONNECTION`):

```bash
dotnet run --project EcoRoute.DbTest
```

## Dokumentasi

- [Penerapan OOP](docs/OOP.md): encapsulation, inheritance, polymorphism, dan alasan desain class
- [Spesifikasi awal](docs/Spesifikasi.md): state tampilan, prioritas fitur, model data, batasan project (rencana awal; beberapa hal berubah di versi final, lihat bagian Teknologi)
- [Class diagram](Class%20diagram%20EcoRoute.drawio.png)
- [Aturan pesan commit](AturanDeskripsiCommit.md)

## Tim

Departemen Teknik Elektro dan Teknologi Informasi, Fakultas Teknik, Universitas Gadjah Mada

| Nama | NIM | Peran |
|---|---|---|
| Akio Afifian Ahsan (Ketua) | 24/542230/TK/60198 | Input rute & hasil emisi, OOP |
| Annora Farah Aprilla Setyawan | 24/538937/TK/59766 |Peta, tampilan idle & detail lokasi, autentikasi (Register/Login), Eco Score |
| Muhammad Affandi Argya Bagaskara | 24/538984/TK/59778 | Fondasi UI & ViewModel, database (SQLite lalu migrasi ke PostgreSQL cloud), integrasi TomTom (kemacetan live), integrasi |

## Atribusi Data

Data peta © [OpenStreetMap contributors](https://www.openstreetmap.org/copyright). Routing oleh [OSRM](https://project-osrm.org/) melalui server komunitas [routing.openstreetmap.de](https://routing.openstreetmap.de/). Data lalu lintas oleh [TomTom](https://developer.tomtom.com/). Trayek dan halte Trans Jogja dari OpenStreetMap (lisensi ODbL) lewat [Overpass API](https://overpass-api.de/). Faktor emisi adalah estimasi rata-rata per kilometer (per penumpang untuk Bus).
