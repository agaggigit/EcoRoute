# Penerapan OOP: Perhitungan Emisi per Moda

Proses yang dipilih adalah **perhitungan emisi karbon per moda transportasi**. Proses ini berjalan setiap kali pengguna menekan *Hitung Emisi*, mengganti moda, atau mengganti kondisi lalu lintas di layar hasil.

Sebelumnya `ModaTransportasi` hanyalah `record` berisi data. Rumus `jarak × faktor` ditulis langsung di ViewModel, dan sepeda serta jalan kaki dibedakan hanya karena faktornya bernilai `0`. Sekarang setiap jenis moda menjadi class yang menghitung emisinya sendiri.

## Diagram class

```
                   ModaTransportasi  (abstract)
                   + Nama : string
                   + Ikon : string
                   + HitungEmisi(jarakKm, kemacetan) : double   «abstract»
                   + HitungEmisi(jarakKm) : double              (= kondisi Lancar)
                   # ValidasiJarak(jarakKm)
                      ▲                          ▲
                      │                          │
      KendaraanBermotor                     TransportasiAktif
      - _faktorEmisiDasar                   + HitungEmisi() => 0
      - _faktorPenaltiKemacetan
      + TerpengaruhKemacetan : bool
      + HitungEmisi() => jarak × dasar × penalti efektif

      «enum» TingkatKemacetan : Lancar | Sedang | Padat | Macet
```

File: `EcoRoute/Models/ModaTransportasi.cs`, `EcoRoute/Models/TingkatKemacetan.cs`

| Moda | Class | Faktor dasar (kg CO₂/km) | Penalti kemacetan maks. |
|---|---|---|---|
| Mobil | `KendaraanBermotor` | 0.192 | 1.35 |
| Motor | `KendaraanBermotor` | 0.103 | 1.15 |
| Bus | `KendaraanBermotor` | 0.089 | 1.25 |
| Kereta | `KendaraanBermotor` | 0.041 | 1.00 (tidak terpengaruh) |
| Sepeda, Jalan Kaki | `TransportasiAktif` | – (emisi selalu 0) | – |

Nilai diambil dari tabel `emission_factor` di PostgreSQL. Kalau database tidak tersambung, aplikasi memakai nilai cadangan yang sama di `MainViewModel`.

## Mengapa bukan `Vehicle` → `Car`, `Motorcycle`, `Bus`, `Train`?

Spesifikasi awal mencontohkan satu subclass untuk setiap kendaraan. Desain tersebut diganti karena alasan berikut:

1. **Rumus Mobil, Motor, Bus, dan Kereta sama persis**: `jarak × faktor dasar × penalti`. Yang berbeda hanya angkanya. Membuat empat subclass dengan isi `HitungEmisi` yang identik berarti duplikasi kode tanpa perilaku baru. Perbedaan angka cukup disimpan sebagai data (field yang diisi dari database).
2. **Perilaku yang benar-benar berbeda** hanya ada dua: kendaraan bermotor (punya emisi dan penalti kemacetan) dan transportasi aktif (emisi selalu nol). Karena itu hierarki dibagi menurut perilaku, bukan menurut nama kendaraan.
3. **Data-driven**: moda baru dengan rumus yang sama, misalnya Taksi, cukup ditambahkan satu baris di tabel `emission_factor` tanpa mengubah kode. Subclass baru hanya dibutuhkan untuk rumus baru, misalnya kendaraan listrik.
4. Nama class memakai Bahasa Indonesia supaya konsisten dengan seluruh kode dan skema database.

Pilar OOP yang diminta (abstract base class, inheritance, polymorphism, encapsulation) tetap terpenuhi, seperti dijelaskan di bawah.

## 1. Encapsulation

- Faktor emisi disimpan di field `private readonly` (`_faktorEmisiDasar`, `_faktorPenaltiKemacetan`, baris 33–34). Kode di luar class tidak bisa membaca atau mengubahnya. Yang terlihat dari luar hanya hasil `HitungEmisi` dan properti baca-saja `TerpengaruhKemacetan` (baris 46).
- Nilainya hanya bisa diisi lewat konstruktor, dan konstruktor memvalidasinya (baris 39–40). Faktor negatif atau penalti di bawah 1 ditolak dengan `ArgumentOutOfRangeException`.
- Validasi jarak ada di satu method `protected` milik base class, `ValidasiJarak` (baris 23). Method ini dipakai semua subclass tetapi tidak terlihat dari luar.

Akibatnya objek moda tidak mungkin berada dalam keadaan tidak valid, misalnya emisinya negatif.

## 2. Inheritance

- `KendaraanBermotor` (baris 31) dan `TransportasiAktif` (baris 57) mewarisi `ModaTransportasi`.
- Keduanya mendapat `Nama`, `Ikon`, `ValidasiJarak`, dan versi singkat `HitungEmisi(jarakKm)` (baris 21) dari base class tanpa menulis ulang kodenya. Konstruktornya cukup meneruskan nilai lewat `: base(nama, ikon)`.
- `ModaTransportasi` dibuat `abstract`, jadi objeknya tidak bisa dibuat secara langsung. Setiap moda harus jelas jenisnya.

## 3. Polymorphism

- `HitungEmisi(jarakKm, kemacetan)` dideklarasikan `abstract` di base class (baris 18) lalu di-`override` oleh tiap subclass (baris 48 dan 61) dengan rumus berbeda:
  - `KendaraanBermotor`: penalti efektif = `1 + (penalti − 1) × bobot`, dengan bobot Lancar 0, Sedang ⅓, Padat ⅔, Macet 1.
  - `TransportasiAktif`: selalu `0`, apa pun kondisi lalu lintasnya.
- ViewModel menyimpan semua moda dalam satu `IReadOnlyList<ModaTransportasi>` (`MainviewModel.cs` baris 44), lalu menghitung emisinya dengan satu baris tanpa `if` per jenis moda (baris 479):

  ```csharp
  DaftarModa.Select(m => (Moda: m, Emisi: m.HitungEmisi(JarakKm, Kemacetan)))
  ```

  Saat program berjalan, C# memanggil versi `HitungEmisi` sesuai tipe asli objeknya.
- Tipe objek juga dipakai saat menyusun insight: alternatif yang disarankan hanya `x.Moda is KendaraanBermotor` (baris 520), dan tambahan emisi akibat macet hanya ditampilkan untuk `KendaraanBermotor { TerpengaruhKemacetan: true }` (baris 508).

**Manfaatnya:** moda baru dengan rumus lain cukup ditambahkan sebagai subclass baru tanpa mengubah kode ViewModel.

## Penalti kemacetan

Kemacetan bukan input pengguna, karena pengguna tidak tahu kondisi tiap ruas jalan. Nilainya diambil otomatis **per segmen rute**:

1. `TrafficService.BagiSegmen` memecah garis rute OSRM menjadi segmen sama panjang (sekitar 1 segmen per km, minimal 3, maksimal 10).
2. Untuk titik tengah tiap segmen, TomTom Traffic Flow API memberi kecepatan saat ini dan kecepatan normal. Rasionya diubah oleh `TingkatKemacetanInfo.DariRasioKecepatan`: ≥ 80% Lancar, ≥ 60% Sedang, ≥ 40% Padat, di bawahnya Macet. Jalan yang ditutup dihitung rasio 0. Segmen yang datanya gagal diambil diisi rata-rata segmen lain.
3. `KondisiLaluLintas.HitungEmisi` menghitung emisi tiap segmen dengan tingkat kemacetannya sendiri, lalu menjumlahkannya. Contoh: 6 km lancar + 4 km macet dihitung sebagai `HitungEmisi(6, Lancar) + HitungEmisi(4, Macet)`, bukan memakai satu tingkat rata-rata untuk seluruh 10 km.
4. Waktu tempuh memakai rasio kecepatan asli: segmen dengan kecepatan tinggal setengah butuh waktu 2× (dibatasi maksimal 4×). Pengali seluruh rute adalah rata-rata pengali segmen yang ditimbang dengan panjangnya.

Kalau API key belum diatur atau TomTom gagal dihubungi, aplikasi menampilkan notif singkat dan memakai `KondisiLaluLintas.Tebakan`: satu segmen untuk seluruh rute dengan tingkat dari jam (`TingkatKemacetanInfo.Default`). Jam sibuk hari kerja (06.30–09.00 dan 16.00–19.00) dianggap Padat, siang hari Sedang, malam dan dini hari Lancar. Pada akhir pekan, jam sibuk turun satu tingkat menjadi Sedang.

API key dibaca dari environment variable `TOMTOM_API_KEY`, atau dari key yang ditanam saat build (`AssemblyMetadata` di `EcoRoute.csproj`), sehingga tidak pernah tertulis di source code.

Contoh untuk satu segmen Mobil sejauh 10 km (faktor 0.192, penalti 1.35). Pengali waktu di tabel hanya dipakai untuk tebakan dari jam, karena data TomTom memakai rasio kecepatan aslinya:

| Kondisi | Penalti efektif | Emisi | Pengali waktu tempuh |
|---|---|---|---|
| Lancar | 1.00 | 1.92 kg | ×1.00 |
| Sedang | 1.12 | 2.14 kg | ×1.25 |
| Padat | 1.23 | 2.37 kg | ×1.60 |
| Macet | 1.35 | 2.59 kg | ×2.00 |

Pengali waktu hanya berlaku untuk moda yang lewat jalan raya (Mobil, Motor, Bus).

## Hubungan dengan database

Parameter konstruktor `KendaraanBermotor` sama dengan kolom tabel `emission_factor` (`faktor_emisi_dasar`, `faktor_penalti_kemacetan`). `DatabaseService.GetModaTransportasiAsync` membaca setiap baris tabel lalu membuat objek `KendaraanBermotor` atau `TransportasiAktif` sesuai kolom `kategori`.

## Eco Score (`EcoScoreCalculator`)

`Models/EcoScoreCalculator.cs` mengubah riwayat perjalanan menjadi ringkasan Eco Score (`EcoScoreRingkasan`): CO₂ yang dihemat, poin, level, dan perbandingan dengan pohon.

- **Composition**: `EcoScoreCalculator` menyimpan daftar `ModaTransportasi` (dari database atau nilai cadangan) dan memakai moda `Mobil` sebagai acuan.
- **Polymorphism**: penghematan satu perjalanan = `mobil.HitungEmisi(jarak) − moda.HitungEmisi(jarak)`. Calculator tidak perlu tahu apakah modanya `KendaraanBermotor` atau `TransportasiAktif`, karena tiap subclass menghitung emisinya sendiri.
- **Kondisi lancar**: kedua emisi dihitung ulang tanpa penalti kemacetan. Emisi tersimpan di riwayat sudah termasuk penalti, jadi kalau dipakai, naik mobil saat macet akan menghasilkan "penghematan" negatif. Hasilnya juga dibatasi minimal 0.
- **Poin dan level**: 1 kg CO₂ dihemat = 10 poin. Level: Bibit (0), Tunas (100), Pohon (500), Hutan (2000 poin).
- **Setara pohon**: CO₂ dihemat ÷ 21 kg, yaitu rata-rata serapan satu pohon dewasa per tahun (European Environment Agency; Arbor Day Foundation menyebut ±48 lb ≈ 21,8 kg).

`MainViewModel` menghitung ulang Eco Score setiap kali `RiwayatPerjalanan` atau `DaftarModa` berubah, lalu `Views/Components/EcoScoreView.xaml` menampilkannya. Datanya diambil dari riwayat pengguna yang sedang login, jadi layanan database tidak perlu diubah.

## Pengujian

Unit test (xUnit) ada di `EcoRoute.Tests/`:

| File | Yang diuji |
|---|---|
| `ModaTransportasiTests.cs` | Rumus emisi, penalti kemacetan bertahap, emisi nol untuk transportasi aktif, penolakan nilai negatif, pemanggilan polimorfik pada daftar moda campuran |
| `TingkatKemacetanTests.cs` | Tebakan kondisi lalu lintas dari jam, waktu tempuh saat macet |
| `RoutingServiceTests.cs` | Estimasi waktu tempuh per moda |
| `EcoScoreCalculatorTests.cs` | CO₂ dihemat per moda, tidak negatif saat macet, moda tidak dikenal, ambang level, progres, setara pohon |

```bash
dotnet test EcoRoute.slnx
```
