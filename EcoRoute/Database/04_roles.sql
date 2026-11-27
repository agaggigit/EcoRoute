-- =====================================================================
-- EcoRoute: hak akses role aplikasi
--
-- Role ecoroute_app dibuat terpisah (password TIDAK ditulis di repo):
--   CREATE ROLE ecoroute_app LOGIN PASSWORD '<password acak>';
-- Setelah itu jalankan file ini sebagai admin.
--
-- Hasil akhirnya, ecoroute_app hanya bisa:
--   - membaca tabel emission_factor (daftar moda dan faktor emisi)
--   - memanggil fn_register, fn_ambil_login, fn_simpan_perjalanan, fn_riwayat
-- Tidak bisa SELECT/INSERT/UPDATE/DELETE tabel lain, tidak bisa membuat
-- atau menghapus tabel.
-- =====================================================================

-- Tutup semua akses bawaan dulu.
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM PUBLIC, ecoroute_app;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM PUBLIC, ecoroute_app;
REVOKE CREATE ON SCHEMA public FROM PUBLIC, ecoroute_app;

-- PostgreSQL memberi EXECUTE ke PUBLIC untuk setiap fungsi baru; dicabut.
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;

-- Buka hanya yang dibutuhkan aplikasi.
GRANT CONNECT ON DATABASE neondb TO ecoroute_app;
GRANT USAGE ON SCHEMA public TO ecoroute_app;
GRANT SELECT ON emission_factor TO ecoroute_app;

GRANT EXECUTE ON FUNCTION fn_register(text, text, text) TO ecoroute_app;
GRANT EXECUTE ON FUNCTION fn_ambil_login(text) TO ecoroute_app;
GRANT EXECUTE ON FUNCTION fn_simpan_perjalanan(
    integer, text, text,
    double precision, double precision, double precision, double precision,
    double precision, integer, text, double precision) TO ecoroute_app;
GRANT EXECUTE ON FUNCTION fn_riwayat(integer) TO ecoroute_app;
