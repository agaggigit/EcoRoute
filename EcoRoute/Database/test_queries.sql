-- EcoRoute database test queries
-- Run after schema.sql + seed.sql: psql -v ON_ERROR_STOP=1 -d ecoroute -f EcoRoute/Database/test_queries.sql
-- Every change is rolled back at the end, so the sample data stays the same.
-- The queries marked [APP] are the ones the C# DatabaseService is expected to use.

BEGIN;

\echo '== 1. Tabel yang ada =='
\dt

\echo '== 2. Jumlah baris tiap tabel =='
SELECT 'emission_factor' AS tabel, COUNT(*) FROM emission_factor
UNION ALL SELECT 'users',                 COUNT(*) FROM users
UNION ALL SELECT 'vehicle',               COUNT(*) FROM vehicle
UNION ALL SELECT 'route',                 COUNT(*) FROM route
UNION ALL SELECT 'route_segment',         COUNT(*) FROM route_segment
UNION ALL SELECT 'trip_history',          COUNT(*) FROM trip_history
UNION ALL SELECT 'trip_mode_comparison',  COUNT(*) FROM trip_mode_comparison
UNION ALL SELECT 'trip_segment_snapshot', COUNT(*) FROM trip_segment_snapshot;

\echo '== 3. [APP] Ambil faktor emisi (isi DaftarModa) =='
SELECT jenis_kendaraan, kategori, faktor_emisi_dasar, faktor_penalti_kemacetan
FROM emission_factor
ORDER BY emission_factor_id;

\echo '== 4. [APP] Ambil riwayat perjalanan user 3, terbaru dulu =='
SELECT trip_id, tanggal, nama_asal, nama_tujuan, jarak_km, moda_dipilih, total_emisi_kg
FROM v_riwayat_perjalanan
WHERE user_id = 3
ORDER BY tanggal DESC;

\echo '== 5. [APP] Simpan perjalanan baru (route + trip dalam satu transaksi) =='
WITH new_route AS (
    INSERT INTO route (user_id, nama_asal, nama_tujuan,
                       titik_awal_lat, titik_awal_lng, titik_akhir_lat, titik_akhir_lng,
                       jarak, estimasi_waktu)
    VALUES (3, 'UGM', 'Malioboro', -7.770717, 110.377724, -7.792583, 110.365861, 3.400, 12)
    RETURNING route_id
)
INSERT INTO trip_history (user_id, route_id, moda_dipilih, total_emisi_kg)
SELECT 3, route_id, 'Kereta', 0.1394 FROM new_route
RETURNING trip_id, route_id, tanggal, moda_dipilih, total_emisi_kg;

\echo '== 6. Riwayat user 3 sekarang berisi 3 perjalanan =='
SELECT trip_id, nama_asal, nama_tujuan, moda_dipilih, total_emisi_kg
FROM v_riwayat_perjalanan WHERE user_id = 3 ORDER BY tanggal DESC;

\echo '== 7. Total emisi dan jumlah perjalanan per user =='
SELECT u.nama_user, COUNT(t.trip_id) AS jumlah_trip, COALESCE(SUM(t.total_emisi_kg), 0) AS total_emisi_kg
FROM users u
LEFT JOIN trip_history t ON t.user_id = u.user_id
GROUP BY u.user_id, u.nama_user
ORDER BY total_emisi_kg DESC;

\echo '== 8. Perbandingan moda untuk trip 1 (bar di State D) =='
SELECT t.moda_dipilih AS moda, t.total_emisi_kg AS emisi_kg, TRUE AS dipilih
FROM trip_history t WHERE t.trip_id = 1
UNION ALL
SELECT c.jenis_kendaraan_bandingan, c.estimasi_emisi_kg, FALSE
FROM trip_mode_comparison c WHERE c.trip_id = 1
ORDER BY emisi_kg;

\echo '== 9. Uji constraint: moda yang tidak terdaftar harus DITOLAK =='
SAVEPOINT uji_fk;
\set ON_ERROR_STOP 0
INSERT INTO trip_history (user_id, route_id, moda_dipilih, total_emisi_kg) VALUES (1, 1, 'Pesawat', 5.0);
\set ON_ERROR_STOP 1
ROLLBACK TO SAVEPOINT uji_fk;

\echo '== 10. Uji constraint: emisi negatif harus DITOLAK =='
SAVEPOINT uji_check;
\set ON_ERROR_STOP 0
INSERT INTO trip_history (user_id, route_id, moda_dipilih, total_emisi_kg) VALUES (1, 1, 'Motor', -1);
\set ON_ERROR_STOP 1
ROLLBACK TO SAVEPOINT uji_check;

ROLLBACK;
\echo '== Selesai: semua perubahan di-rollback =='
