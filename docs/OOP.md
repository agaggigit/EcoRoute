# Penerapan OOP: Perhitungan Emisi per Moda

Proses yang dipilih adalah **perhitungan emisi karbon per moda transportasi**. Proses ini berjalan setiap kali pengguna menekan *Hitung Emisi* atau mengganti moda di layar hasil.

Sebelumnya `ModaTransportasi` hanyalah `record` berisi data. Rumus `jarak × faktor` ditulis langsung di ViewModel, dan sepeda serta jalan kaki dibedakan hanya karena faktornya bernilai `0`. Sekarang setiap jenis moda menjadi class yang menghitung emisinya sendiri.

## Diagram class

```
              ModaTransportasi  (abstract)
              + Nama : string
              + Ikon : string
              + HitungEmisi(jarakKm) : double   «abstract»
              # ValidasiJarak(jarakKm)
                 ▲                      ▲
                 │                      │
      KendaraanBermotor            TransportasiAktif
      - _faktorEmisiDasar          + HitungEmisi() => 0
      - _faktorPenaltiKemacetan
      + HitungEmisi() => jarak × dasar × penalti
```

File: `EcoRoute/Models/ModaTransportasi.cs`

| Moda | Class | Faktor (kg CO₂/km) |
|---|---|---|
| Mobil, Motor, Bus, Kereta | `KendaraanBermotor` | 0.192, 0.103, 0.089, 0.041 |
| Sepeda, Jalan Kaki | `TransportasiAktif` | (tidak ada, emisi selalu 0) |

## 1. Encapsulation

- Faktor emisi disimpan di field `private readonly` (`_faktorEmisiDasar`, `_faktorPenaltiKemacetan`, baris 29–30). Kode di luar class tidak bisa membaca atau mengubahnya. Yang bisa dipakai hanya hasil `HitungEmisi`.
- Nilainya hanya bisa diisi lewat konstruktor, dan konstruktor memvalidasinya (baris 35–36). Faktor negatif atau penalti di bawah 1 ditolak dengan `ArgumentOutOfRangeException`.
- Validasi jarak ada di satu method `protected` milik base class, `ValidasiJarak` (baris 22). Method ini dipakai oleh semua subclass tetapi tidak terlihat dari luar.

Akibatnya objek moda tidak mungkin berada dalam keadaan tidak valid, misalnya emisinya negatif.

## 2. Inheritance

- `KendaraanBermotor` (baris 27) dan `TransportasiAktif` (baris 49) mewarisi `ModaTransportasi`.
- Keduanya mendapat `Nama`, `Ikon`, dan `ValidasiJarak` dari base class tanpa menulis ulang kodenya. Konstruktornya cukup meneruskan nilai lewat `: base(nama, ikon)`.
- `ModaTransportasi` dibuat `abstract`, jadi tidak bisa dibuat objeknya secara langsung. Setiap moda harus jelas jenisnya.

## 3. Polymorphism

- `HitungEmisi` dideklarasikan `abstract` di base class (baris 18) lalu di-`override` oleh tiap subclass (baris 41 dan 53) dengan rumus yang berbeda.
- ViewModel menyimpan semua moda dalam satu `IReadOnlyList<ModaTransportasi>` (`MainviewModel.cs` baris 29), lalu menghitung emisinya dengan satu baris tanpa `if` per jenis moda (baris 199):

  ```csharp
  DaftarModa.Select(m => (Moda: m, Emisi: m.HitungEmisi(JarakKm)))
  ```

  Saat program berjalan, C# memanggil versi `HitungEmisi` sesuai tipe asli objeknya.
- Tipe objek juga dipakai saat menyusun insight (baris 224): alternatif yang disarankan hanya `x.Moda is KendaraanBermotor`. Pemilihannya tidak lagi bergantung pada kebetulan bahwa emisinya bernilai 0.

**Manfaatnya:** moda baru, misalnya kendaraan listrik dengan rumus lain, cukup ditambahkan sebagai subclass baru tanpa mengubah kode ViewModel.

## Hubungan dengan database

Parameter konstruktor `KendaraanBermotor` sengaja dibuat sama dengan kolom tabel `emission_factor` (`faktor_emisi_dasar`, `faktor_penalti_kemacetan`). Saat integrasi Npgsql, setiap baris tabel cukup diubah menjadi `new KendaraanBermotor(jenis_kendaraan, ikon, faktor_emisi_dasar, faktor_penalti_kemacetan)`.

## Pengujian

Unit test ada di `EcoRoute.Tests/ModaTransportasiTests.cs` (xUnit, 7 test). Yang diuji: rumus emisi, penalti kemacetan, emisi nol untuk transportasi aktif, penolakan nilai negatif, dan pemanggilan polimorfik pada daftar moda campuran.

```bash
dotnet test EcoRoute.slnx
```
