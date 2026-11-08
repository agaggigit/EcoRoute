-- EcoRoute sample data (Yogyakarta)
-- Run after schema.sql: psql -v ON_ERROR_STOP=1 -d ecoroute -f EcoRoute/Database/seed.sql
-- Safe to re-run: existing rows are cleared and IDs restart at 1.

BEGIN;

TRUNCATE emission_factor, users, vehicle, route, route_segment,
         trip_history, trip_mode_comparison, trip_segment_snapshot
    RESTART IDENTITY CASCADE;

-- jenis_kendaraan must match the mode names in MainViewModel.DaftarModa
INSERT INTO emission_factor (jenis_kendaraan, kategori, faktor_emisi_dasar, faktor_penalti_kemacetan) VALUES
    ('Motor',      'bermotor', 0.1030, 1.1500),   -- id 1
    ('Mobil',      'bermotor', 0.1920, 1.3500),   -- id 2
    ('Bus',        'bermotor', 0.0890, 1.2500),   -- id 3 (per passenger)
    ('Kereta',     'bermotor', 0.0410, 1.0000),   -- id 4 (per passenger, unaffected by traffic)
    ('Sepeda',     'aktif',    0.0000, 1.0000),   -- id 5
    ('Jalan Kaki', 'aktif',    0.0000, 1.0000);   -- id 6

INSERT INTO users (nama_user, email_user, total_eco_score) VALUES
    ('Akio Afifian Ahsan',               'akio.afifian@mail.ugm.ac.id',  320),  -- id 1
    ('Annora Farah Aprilla Setyawan',    'annora.farah@mail.ugm.ac.id',  275),  -- id 2
    ('Muhammad Affandi Argya Bagaskara', 'affandi.argya@mail.ugm.ac.id', 190),  -- id 3
    ('Rina Kusumawati',                  'rina.kusuma@gmail.com',         85);  -- id 4

INSERT INTO vehicle (user_id, emission_factor_id, tahun_kendaraan) VALUES
    (1, 1, 2021),   -- id 1: Akio, Motor
    (2, 2, 2019),   -- id 2: Annora, Mobil
    (3, 1, 2018),   -- id 3: Affandi, Motor
    (4, 2, 2015);   -- id 4: Rina, Mobil

-- Landmarks:
--   UGM (-7.770717, 110.377724)          Tugu Jogja (-7.782889, 110.367083)
--   Malioboro (-7.792583, 110.365861)     Kraton (-7.805284, 110.364203)
--   Stasiun Tugu (-7.789139, 110.363417)  Bandara Adisucipto (-7.788181, 110.431755)
--   Candi Prambanan (-7.752020, 110.491474)
INSERT INTO route (user_id, nama_asal, nama_tujuan, titik_awal_lat, titik_awal_lng, titik_akhir_lat, titik_akhir_lng, jarak, estimasi_waktu, created_at) VALUES
    (1, 'UGM',                'Malioboro',       -7.770717, 110.377724, -7.792583, 110.365861,  3.400, 12, '2026-09-20 07:15:00'),  -- id 1
    (2, 'Tugu Jogja',         'Kraton',          -7.782889, 110.367083, -7.805284, 110.364203,  2.800, 10, '2026-09-21 09:30:00'),  -- id 2
    (3, 'Bandara Adisucipto', 'Candi Prambanan', -7.788181, 110.431755, -7.752020, 110.491474, 11.200, 25, '2026-09-22 14:00:00'),  -- id 3
    (4, 'Stasiun Tugu',       'UGM',             -7.789139, 110.363417, -7.770717, 110.377724,  3.100, 11, '2026-09-23 17:45:00'),  -- id 4
    (3, 'UGM',                'Tugu Jogja',      -7.770717, 110.377724, -7.782889, 110.367083,  2.000,  8, '2026-10-01 06:30:00'),  -- id 5
    (2, 'Malioboro',          'Kraton',          -7.792583, 110.365861, -7.805284, 110.364203,  1.500, 20, '2026-10-02 16:00:00');  -- id 6

-- Segment lengths sum to each route's jarak
INSERT INTO route_segment (route_id, urutan_segmen, jarak_segmen) VALUES
    (1, 1, 1.200), (1, 2, 1.100), (1, 3, 1.100),   -- ids 1-3
    (2, 1, 1.300), (2, 2, 1.500),                  -- ids 4-5
    (3, 1, 4.000), (3, 2, 4.200), (3, 3, 3.000),   -- ids 6-8
    (4, 1, 1.600), (4, 2, 1.500),                  -- ids 9-10
    (5, 1, 1.000), (5, 2, 1.000),                  -- ids 11-12
    (6, 1, 1.500);                                 -- id 13

-- total_emisi_kg = jarak x faktor_emisi_dasar x faktor_penalti_kemacetan
INSERT INTO trip_history (user_id, vehicle_id, route_id, tanggal, moda_dipilih, total_emisi_kg) VALUES
    (1, 1,    1, '2026-09-20 07:20:00', 'Motor',      0.4027),  -- id 1: 3.4 x 0.103 x 1.15
    (2, 2,    2, '2026-09-21 09:35:00', 'Mobil',      0.7258),  -- id 2: 2.8 x 0.192 x 1.35
    (3, NULL, 3, '2026-09-22 14:05:00', 'Bus',        1.2460),  -- id 3: TransJogja
    (4, NULL, 4, '2026-09-23 17:50:00', 'Kereta',     0.1271),  -- id 4
    (3, NULL, 5, '2026-10-01 06:35:00', 'Sepeda',     0.0000),  -- id 5
    (2, NULL, 6, '2026-10-02 16:05:00', 'Jalan Kaki', 0.0000);  -- id 6

INSERT INTO trip_mode_comparison (trip_id, jenis_kendaraan_bandingan, estimasi_emisi_kg) VALUES
    (1, 'Mobil',  0.8813), (1, 'Bus',   0.3783), (1, 'Kereta', 0.1394),
    (2, 'Motor',  0.3317), (2, 'Bus',   0.3115), (2, 'Kereta', 0.1148),
    (3, 'Mobil',  2.9030), (3, 'Motor', 1.3266), (3, 'Kereta', 0.4592),
    (4, 'Motor',  0.3672), (4, 'Mobil', 0.8035),
    (5, 'Motor',  0.2369), (5, 'Mobil', 0.5184),
    (6, 'Motor',  0.1777), (6, 'Mobil', 0.3888);

-- segment_id values belong to each trip's route
INSERT INTO trip_segment_snapshot (trip_id, segment_id, tingkat_kemacetan, intensitas_polusi, recorded_at) VALUES
    (1, 1,  'sedang', 0.1421, '2026-09-20 07:24:00'),
    (1, 2,  'padat',  0.1519, '2026-09-20 07:28:00'),
    (1, 3,  'lancar', 0.1088, '2026-09-20 07:31:00'),
    (2, 4,  'macet',  0.3369, '2026-09-21 09:40:00'),
    (2, 5,  'sedang', 0.3284, '2026-09-21 09:44:00'),
    (3, 6,  'lancar', 0.3560, '2026-09-22 14:12:00'),
    (3, 8,  'padat',  0.3338, '2026-09-22 14:26:00'),
    (4, 9,  'lancar', 0.0656, '2026-09-23 17:55:00'),
    (4, 10, 'lancar', 0.0615, '2026-09-23 17:59:00');

COMMIT;
