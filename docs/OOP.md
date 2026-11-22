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
| Sepeda, Jalan Kaki | `TransportasiAktif` | – (emisi selalu 0) | – |

Kereta sempat menjadi moda keenam, tetapi dihapus. Tidak ada layanan routing kereta yang gratis, dan jalur KRL hanya melewati beberapa stasiun di Yogyakarta, sehingga kebanyakan perjalanan dalam kota tidak bisa ditempuh dengan kereta. Rute kereta multimoda (misalnya jalan kaki ke stasiun, naik KRL, lalu naik ojek ke tujuan) menjadi bahan pengembangan selanjutnya.

Nilai diambil dari tabel `emission_factor` di PostgreSQL. Kalau database tidak tersambung, aplikasi memakai nilai cadangan yang sama di `MainViewModel`.

## Mengapa bukan `Vehicle` → `Car`, `Motorcycle`, `Bus`, `Train`?

Spesifikasi awal mencontohkan satu subclass untuk setiap kendaraan. Desain tersebut diganti karena alasan berikut:

1. **Rumus Mobil, Motor, dan Bus sama persis**: `jarak × faktor dasar × penalti`. Yang berbeda hanya angkanya. Membuat tiga subclass dengan isi `HitungEmisi` yang identik berarti duplikasi kode tanpa perilaku baru. Perbedaan angka cukup disimpan sebagai data (field yang diisi dari database).
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

## Rute dan waktu tempuh per moda

Saat *Hitung Emisi* ditekan, `RoutingService.HitungSemuaRuteAsync` mengambil tiga rute sekaligus dari server OSRM komunitas OpenStreetMap: jalan raya, sepeda, dan jalan kaki. Tiap moda memakai rute sesuai jalurnya (`RoutingService.ProfilUntuk`), sehingga jarak dan emisinya dihitung dari rutenya sendiri. Garis rute di peta juga ikut berganti saat moda diganti.

| Moda | Rute | Waktu tempuh |
|---|---|---|
| Mobil | Jalan raya | Durasi rute × pengali kemacetan |
| Motor | Jalan raya | Durasi rute × (1 + separuh tambahan akibat macet) |
| Bus | Trayek Trans Jogja (kalau ada yang langsung), selain itu jalan raya | Durasi bagian bus × 1.3 × pengali kemacetan + 10 menit (+ jalan kaki ke dan dari halte) |
| Sepeda | Jalur sepeda | Durasi rute sepeda, tidak terpengaruh macet |
| Jalan Kaki | Jalur pejalan kaki | Durasi rute jalan kaki, tidak terpengaruh macet |

Alasan tiap rumus:

- **Motor memakai rute mobil.** Sudah diuji dengan layanan routing lain (Valhalla) yang punya profil motor, dan hasilnya sama dengan mobil untuk rute-rute di Yogyakarta. Di data OpenStreetMap, perbedaan motor dan mobil hampir hanya soal jalan tol. Google Maps punya mode motor, tetapi API-nya berbayar. Meskipun rutenya sama, motor tetap berbeda dari mobil dalam emisi (hampir setengahnya) dan penalti kemacetan.
- **Motor hanya kena separuh tambahan waktu akibat macet**, karena motor bisa menyelip di antara kendaraan. Contoh: kalau macet membuat mobil 2× lebih lama (tambah 100%), motor hanya 1.5× lebih lama (tambah 50%). Angka "separuh" ini adalah asumsi.
- **Bus dikali sekaligus ditambah**, karena ada dua jenis waktu tambahan:
  - *Dikali 1.3*: waktu berhenti di halte sepanjang jalan. Makin jauh perjalanan, makin banyak halte yang dilewati, sehingga waktunya sebanding dengan jarak.
  - *Ditambah 10 menit*: waktu menunggu bus datang. Waktu ini tetap per perjalanan, tidak tergantung jarak. Nilai 10 menit diambil dari kira-kira setengah jarak kedatangan bus Trans Jogja (15–20 menit).
  - Kedua angka adalah asumsi. Jadwal jam keberangkatan tidak dibutuhkan, karena Trans Jogja berangkat dengan interval tetap, bukan jadwal jam seperti kereta.
- **Sepeda dan jalan kaki** memakai durasi dari profil rutenya sendiri. Kalau rute sepeda atau jalan kaki gagal diambil, aplikasi memakai rute jalan raya dengan kecepatan rata-rata 15 km/jam (sepeda) atau 5 km/jam (jalan kaki).

Contoh UGM → Malioboro saat lancar (data asli):

| Moda | Jarak | Waktu | Emisi |
|---|---|---|---|
| Mobil | 3.62 km | 6 menit | 0.695 kg |
| Motor | 3.62 km | 6 menit | 0.373 kg |
| Bus (Trans Jogja 3A) | 4.2 km (3.6 km naik bus + 0.6 km jalan kaki) | ±26 menit | ±0.32 kg |
| Sepeda | 3.25 km | 15 menit | 0 kg |
| Jalan Kaki | 3.43 km | 46 menit | 0 kg |

### Trayek Trans Jogja

Jalur Bus mengikuti trayek Trans Jogja asli kalau ada **satu trayek yang langsung** menghubungkan titik awal dan tujuan (tanpa pindah bus).

**Sumber data.** Trayek dan halte diambil dari OpenStreetMap lewat Overpass API, yang gratis dan tanpa API key. Ada 20 trayek dengan 30–75 halte per trayek. Data diunduh sekali oleh developer dengan `tools/ambil_trayek_transjogja.py`, lalu disimpan di `EcoRoute/Data/trayek-transjogja.json` (±360 KB) yang ikut terpasang bersama aplikasi. Data tidak diambil saat aplikasi berjalan, karena server Overpass sering penuh dan aplikasi jadi bergantung pada server itu. Tanggal pengambilan tercatat di file data.

**Cara mencari rute** (`Services/TransJogjaService.cs`):

1. Cari halte dalam jarak 1 km (garis lurus) dari titik awal dan dari tujuan. Untuk tiap trayek dicoba maksimal 5 halte terdekat di tiap ujung.
2. Tiap trayek disimpan sebagai jaringan ruas jalan yang dilaluinya. Jalur bus dari halte naik ke halte turun adalah jalur terpendek di dalam jaringan itu (algoritma Dijkstra), jadi garisnya pasti lewat jalan yang memang dilalui trayek tersebut.
3. Dari semua kombinasi trayek, halte naik, dan halte turun, dipilih yang paling cepat (jalan kaki 5 km/jam, bus ±20 km/jam). Kombinasi yang total jalan kakinya lebih jauh daripada langsung berjalan ke tujuan dilewati.
4. Jarak jalan kaki = jarak garis lurus × 1.3, untuk belokan dan menyeberang jalan.

**Emisi dan waktu.** Hanya bagian naik bus yang beremisi. Durasi bagian bus dihitung dari kecepatan rata-rata rute mobil untuk perjalanan yang sama, lalu dipakai rumus bus di atas (× 1.3 untuk halte, × kemacetan, + 10 menit menunggu), ditambah waktu jalan kaki. Kemacetan per segmen dari TomTom diukur di rute jalan raya, jadi untuk bagian bus dipakai rata-rata emisi per km-nya.

**Kalau tidak ada trayek langsung**, misalnya halte terlalu jauh atau harus pindah bus, Bus kembali memakai perkiraan jalan raya seperti sebelumnya. Card hasil menuliskan keterangan itu dengan jelas.

**Batasan yang diketahui:**

- Belum mendukung pindah bus (transit). Perjalanan yang butuh dua trayek memakai perkiraan jalan raya.
- Arah jalan satu arah belum diperhitungkan. Jalur bus pergi dan pulang bisa saja berbeda di jalan satu arah seperti Malioboro, tetapi aplikasi memakai jalur terpendek di jaringan trayek ke dua arah.
- Urutan halte di OpenStreetMap sering tidak berurutan, jadi aplikasi tidak memakai urutan itu. Akibatnya jumlah halte yang dilewati tidak ditampilkan.
- Data OpenStreetMap dibuat komunitas dan bisa tertinggal dari perubahan trayek terbaru. Untuk memperbarui, jalankan ulang skrip di atas.

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
| `RoutingServiceTests.cs` | Jalur tiap moda, estimasi waktu tempuh per moda (separuh efek macet untuk motor, rumus bus, bus lewat trayek plus jalan kaki, durasi profil sepeda dan jalan kaki) |
| `TransJogjaServiceTests.cs` | Pencarian trayek langsung, jalur bus mengikuti ruas trayek, jarak jalan kaki, halte di luar jangkauan, ruas yang terputus, data asli UGM → Malioboro |
| `EcoScoreCalculatorTests.cs` | CO₂ dihemat per moda, tidak negatif saat macet, moda tidak dikenal, ambang level, progres, setara pohon |

```bash
dotnet test EcoRoute.slnx
```
