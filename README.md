# EcoRoute

EcoRoute adalah aplikasi desktop Windows (C# WPF) untuk menghitung dan membandingkan jejak karbon perjalanan. Pengguna memilih titik awal, tujuan, moda transportasi, dan kondisi lalu lintas. Aplikasi lalu mengambil rute asli dari peta, menghitung emisi CO₂ setiap moda, dan menyimpan perjalanan ke riwayat.

**Tema:** Climate Action, kalkulator dan pelacak jejak karbon perjalanan.

## Fitur

- **Peta interaktif** (Leaflet + OpenStreetMap) dengan pin tempat populer Yogyakarta dan filter kategori.
- **Pencarian tempat** lewat Nominatim (OpenStreetMap).
- **Rute asli** (jarak, durasi, dan garis rute) dari OSRM. Titik awal bisa diketik atau dipilih dengan klik di peta.
- **Perbandingan emisi 6 moda**: Mobil, Motor, Bus, Kereta, Sepeda, Jalan Kaki, beserta saran moda yang lebih hemat.
- **Penalti kemacetan**: kondisi Lancar, Sedang, Padat, atau Macet memengaruhi emisi dan waktu tempuh kendaraan bermotor.
- **Riwayat perjalanan** tersimpan di PostgreSQL dan bisa dilihat dari menu *Riwayat*.
- **Penanganan error** yang ramah pengguna untuk koneksi internet terputus, lokasi tidak ditemukan, atau database tidak tersambung.

## Teknologi

| Bagian | Teknologi |
|---|---|
| Aplikasi | C# .NET 10, WPF, pola MVVM |
| Peta | WebView2 + Leaflet 1.9.4 + tile OpenStreetMap |
| Web API (HttpClient) | Nominatim (geocoding), OSRM (routing) |
| Database | PostgreSQL + Npgsql |
| Pengujian | xUnit |

Semua layanan peta gratis dan tidak membutuhkan API key.

## Struktur Project

```
EcoRoute/
├── EcoRoute/                  Aplikasi WPF
│   ├── Models/                Domain & OOP (ModaTransportasi, TingkatKemacetan, Place, RouteResult, ...)
│   ├── ViewModels/            MainViewModel (state A–E), BaseViewModel, RelayCommand
│   ├── Views/Components/      IdleView, PlaceCardView, RouteInputView, EmissionResultView, MapView
│   ├── Services/              RoutingService (HttpClient), DatabaseService (Npgsql)
│   ├── Maps/map.html          Peta Leaflet yang dimuat di WebView2
│   └── Database/              schema.sql, seed.sql, test_queries.sql
├── EcoRoute.Tests/            Unit test xUnit
├── EcoRoute.DbTest/           Program konsol untuk menguji koneksi database
└── docs/                      Dokumentasi (OOP, spesifikasi, halaman project)
```

## Kebutuhan Sistem

- Windows 10/11 64-bit
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (sudah terpasang bawaan di Windows 11)
- Koneksi internet (untuk peta, pencarian, dan rute)
- PostgreSQL 14+ (opsional, untuk menyimpan riwayat)
- .NET 10 SDK (hanya untuk build dari source)

## Cara Menjalankan

### 1. Siapkan database (opsional)

Buat database `ecoroute`, lalu jalankan skema dan data contoh:

```bash
createdb -U postgres ecoroute
psql -U postgres -v ON_ERROR_STOP=1 -d ecoroute -f EcoRoute/Database/schema.sql
psql -U postgres -v ON_ERROR_STOP=1 -d ecoroute -f EcoRoute/Database/seed.sql
```

Panduan lengkap, termasuk setup lewat pgAdmin, ada di [EcoRoute/Database/README.md](EcoRoute/Database/README.md).

Aplikasi terhubung ke `localhost:5432`, database `ecoroute`, user `postgres`. Password diminta saat aplikasi dibuka. Kalau dialog password dibatalkan, aplikasi tetap bisa menghitung emisi, tetapi riwayat tidak disimpan.

### 2a. Jalankan dari file `.exe`

1. Unduh hasil build atau buat sendiri (lihat [Build `.exe`](#build-exe)).
2. Pastikan `EcoRoute.exe` dan folder `Maps` berada di folder yang sama.
3. Jalankan `EcoRoute.exe`.

### 2b. Jalankan dari source

```bash
dotnet run --project EcoRoute/EcoRoute.csproj
```

Bisa juga dengan membuka `EcoRoute.slnx` di Visual Studio 2026 (mendukung .NET 10 dan format `.slnx`) lalu tekan **F5**.

## Panduan Penggunaan

1. **Cari tujuan.** Ketik nama tempat di kolom pencarian lalu tekan Enter, atau klik pin/chip tempat populer. Detail lokasi tampil di card kiri.
2. **Buat rute.** Tekan tombol *Rute*. Tempat yang dipilih otomatis menjadi tujuan.
3. **Isi titik awal.** Ketik nama tempat, atau klik lokasi di peta (kursor berubah menjadi tanda tambah).
4. **Pilih moda dan kondisi lalu lintas.** Kondisi lalu lintas awal ditebak dari jam saat ini dan bisa diganti.
5. **Tekan *Hitung Emisi*.** Garis rute tampil di peta, dan card menampilkan:
   - total emisi CO₂, jarak, dan estimasi waktu tempuh,
   - perbandingan emisi keenam moda (moda paling hemat ditandai),
   - saran moda alternatif dan tambahan emisi akibat kemacetan.
6. Ganti moda atau kondisi lalu lintas untuk menghitung ulang secara langsung tanpa memanggil ulang API.
7. Buka menu **Riwayat** di rail kiri untuk melihat perjalanan yang tersimpan beserta total emisinya.

## Build `.exe`

Build menghasilkan `EcoRoute.exe` mandiri (self-contained, tidak perlu install .NET di komputer tujuan):

```bash
dotnet publish EcoRoute/EcoRoute.csproj -p:PublishProfile=win-x64
```

Hasilnya ada di `EcoRoute/bin/publish/win-x64/`: `EcoRoute.exe` (±70 MB) dan folder `Maps/`. Bagikan keduanya bersama-sama, misalnya dalam satu file zip.

## Pengujian

```bash
dotnet test EcoRoute.slnx
```

Unit test mencakup rumus emisi per moda, penalti kemacetan, tebakan kondisi lalu lintas, dan estimasi waktu tempuh. Koneksi database bisa diuji dengan:

```bash
dotnet run --project EcoRoute.DbTest
```

## Dokumentasi

- [Penerapan OOP](docs/OOP.md): encapsulation, inheritance, polymorphism, dan alasan desain class
- [Spesifikasi awal](docs/Spesifikasi.md): state tampilan, prioritas fitur, model data, batasan project
- [Database](EcoRoute/Database/README.md): struktur tabel dan setup PostgreSQL
- [Class diagram](Class%20diagram%20EcoRoute.drawio.png)
- [Aturan pesan commit](AturanDeskripsiCommit.md)

## Tim

Departemen Teknik Elektro dan Teknologi Informasi, Fakultas Teknik, Universitas Gadjah Mada

| Nama | NIM | Peran |
|---|---|---|
| Akio Afifian Ahsan (Ketua) | 24/542230/TK/60198 | Input rute & hasil emisi, OOP |
| Annora Farah Aprilla Setyawan | 24/538937/TK/59766 | Peta, tampilan idle & detail lokasi, integrasi Npgsql |
| Muhammad Affandi Argya Bagaskara | 24/538984/TK/59778 | Fondasi UI & ViewModel, database PostgreSQL, integrasi |

## Atribusi Data

Data peta © [OpenStreetMap contributors](https://www.openstreetmap.org/copyright). Routing oleh [OSRM](https://project-osrm.org/). Faktor emisi adalah estimasi rata-rata per kilometer (per penumpang untuk Bus dan Kereta).
