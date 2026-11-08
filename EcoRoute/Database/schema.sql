-- EcoRoute database schema (PostgreSQL)
-- Run: psql -v ON_ERROR_STOP=1 -d ecoroute -f EcoRoute/Database/schema.sql
-- WARNING: drops and recreates every table, all data is lost.

BEGIN;

-- Drop in reverse dependency order (children first)
DROP VIEW  IF EXISTS v_riwayat_perjalanan;
DROP TABLE IF EXISTS trip_segment_snapshot;
DROP TABLE IF EXISTS trip_mode_comparison;
DROP TABLE IF EXISTS trip_history;
DROP TABLE IF EXISTS route_segment;
DROP TABLE IF EXISTS route;
DROP TABLE IF EXISTS vehicle;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS emission_factor;

-- Master data: one row per transport mode shown in the UI (6 modes).
-- kategori decides which C# class is built from the row:
--   'bermotor' -> KendaraanBermotor (Mobil, Motor, Bus, Kereta)
--   'aktif'    -> TransportasiAktif (Sepeda, Jalan Kaki), always 0 emission
CREATE TABLE emission_factor (
    emission_factor_id       SERIAL PRIMARY KEY,
    jenis_kendaraan          VARCHAR(50)    NOT NULL UNIQUE,
    kategori                 VARCHAR(20)    NOT NULL CHECK (kategori IN ('bermotor', 'aktif')),
    faktor_emisi_dasar       NUMERIC(10,4)  NOT NULL CHECK (faktor_emisi_dasar >= 0),       -- kg CO2 per km
    faktor_penalti_kemacetan NUMERIC(10,4)  NOT NULL DEFAULT 1.0 CHECK (faktor_penalti_kemacetan >= 1.0),
    updated_at               TIMESTAMP      NOT NULL DEFAULT NOW(),
    CHECK (kategori = 'bermotor' OR faktor_emisi_dasar = 0)
);

-- "users" rather than "user" because USER is a reserved word
CREATE TABLE users (
    user_id         SERIAL PRIMARY KEY,
    nama_user       VARCHAR(100) NOT NULL,
    email_user      VARCHAR(150) NOT NULL UNIQUE,
    total_eco_score INTEGER      NOT NULL DEFAULT 0
);

-- Personal vehicle owned by a user (optional; public transport has none)
CREATE TABLE vehicle (
    vehicle_id         SERIAL PRIMARY KEY,
    user_id            INT NOT NULL REFERENCES users (user_id) ON DELETE CASCADE,
    emission_factor_id INT NOT NULL REFERENCES emission_factor (emission_factor_id),
    tahun_kendaraan    INT CHECK (tahun_kendaraan BETWEEN 1950 AND 2100)
);

-- One calculated route (State D): origin, destination, distance, duration
CREATE TABLE route (
    route_id        SERIAL PRIMARY KEY,
    user_id         INT           NOT NULL REFERENCES users (user_id) ON DELETE CASCADE,
    nama_asal       VARCHAR(200),                                  -- e.g. 'UGM'
    nama_tujuan     VARCHAR(200),                                  -- e.g. 'Malioboro'
    titik_awal_lat  NUMERIC(9,6)  NOT NULL CHECK (titik_awal_lat  BETWEEN -90  AND 90),
    titik_awal_lng  NUMERIC(9,6)  NOT NULL CHECK (titik_awal_lng  BETWEEN -180 AND 180),
    titik_akhir_lat NUMERIC(9,6)  NOT NULL CHECK (titik_akhir_lat BETWEEN -90  AND 90),
    titik_akhir_lng NUMERIC(9,6)  NOT NULL CHECK (titik_akhir_lng BETWEEN -180 AND 180),
    jarak           NUMERIC(10,3) NOT NULL CHECK (jarak >= 0),     -- km
    estimasi_waktu  INT           NOT NULL CHECK (estimasi_waktu >= 0), -- minutes
    created_at      TIMESTAMP     NOT NULL DEFAULT NOW()
);

-- Optional: route split into parts (for the traffic-colour feature)
CREATE TABLE route_segment (
    segment_id    SERIAL PRIMARY KEY,
    route_id      INT           NOT NULL REFERENCES route (route_id) ON DELETE CASCADE,
    urutan_segmen INT           NOT NULL CHECK (urutan_segmen >= 1),
    jarak_segmen  NUMERIC(10,3) NOT NULL CHECK (jarak_segmen >= 0), -- km
    UNIQUE (route_id, urutan_segmen)
);

-- Riwayat perjalanan: one row each time the user saves a calculated trip
CREATE TABLE trip_history (
    trip_id        SERIAL PRIMARY KEY,
    user_id        INT           NOT NULL REFERENCES users (user_id) ON DELETE CASCADE,
    -- NULL when no personal vehicle is used (Bus, Kereta, Jalan Kaki, ...)
    vehicle_id     INT           NULL REFERENCES vehicle (vehicle_id) ON DELETE SET NULL,
    route_id       INT           NOT NULL REFERENCES route (route_id) ON DELETE CASCADE,
    tanggal        TIMESTAMP     NOT NULL DEFAULT NOW(),
    moda_dipilih   VARCHAR(50)   NOT NULL REFERENCES emission_factor (jenis_kendaraan) ON UPDATE CASCADE,
    total_emisi_kg NUMERIC(10,4) NOT NULL CHECK (total_emisi_kg >= 0) -- jarak x faktor dasar x penalti
);

-- Emission of the other modes for the same trip (the comparison bars in State D)
CREATE TABLE trip_mode_comparison (
    comparison_id             SERIAL PRIMARY KEY,
    trip_id                   INT           NOT NULL REFERENCES trip_history (trip_id) ON DELETE CASCADE,
    jenis_kendaraan_bandingan VARCHAR(50)   NOT NULL REFERENCES emission_factor (jenis_kendaraan) ON UPDATE CASCADE,
    estimasi_emisi_kg         NUMERIC(10,4) NOT NULL CHECK (estimasi_emisi_kg >= 0),
    UNIQUE (trip_id, jenis_kendaraan_bandingan)
);

-- Optional: traffic condition per segment at the time of the trip
CREATE TABLE trip_segment_snapshot (
    snapshot_id       SERIAL PRIMARY KEY,
    trip_id           INT         NOT NULL REFERENCES trip_history (trip_id) ON DELETE CASCADE,
    segment_id        INT         NOT NULL REFERENCES route_segment (segment_id) ON DELETE CASCADE,
    tingkat_kemacetan VARCHAR(20) CHECK (tingkat_kemacetan IN ('lancar', 'sedang', 'padat', 'macet')),
    intensitas_polusi NUMERIC(10,4),
    recorded_at       TIMESTAMP   NOT NULL DEFAULT NOW()
);

-- Indexes on foreign key columns
CREATE INDEX idx_vehicle_user_id                  ON vehicle (user_id);
CREATE INDEX idx_vehicle_emission_factor_id       ON vehicle (emission_factor_id);
CREATE INDEX idx_route_user_id                    ON route (user_id);
CREATE INDEX idx_route_segment_route_id           ON route_segment (route_id);
CREATE INDEX idx_trip_history_user_tanggal        ON trip_history (user_id, tanggal DESC);
CREATE INDEX idx_trip_history_vehicle_id          ON trip_history (vehicle_id);
CREATE INDEX idx_trip_history_route_id            ON trip_history (route_id);
CREATE INDEX idx_trip_history_moda_dipilih        ON trip_history (moda_dipilih);
CREATE INDEX idx_trip_segment_snapshot_trip_id    ON trip_segment_snapshot (trip_id);
CREATE INDEX idx_trip_segment_snapshot_segment_id ON trip_segment_snapshot (segment_id);
-- trip_mode_comparison (trip_id) is already covered by its UNIQUE index

-- Ready-made "Riwayat" list for the app: SELECT * FROM v_riwayat_perjalanan WHERE user_id = @user_id
CREATE VIEW v_riwayat_perjalanan AS
SELECT t.trip_id,
       t.user_id,
       u.nama_user,
       t.tanggal,
       r.nama_asal,
       r.nama_tujuan,
       r.jarak          AS jarak_km,
       r.estimasi_waktu AS estimasi_waktu_menit,
       t.moda_dipilih,
       t.total_emisi_kg
FROM trip_history t
JOIN users u ON u.user_id  = t.user_id
JOIN route r ON r.route_id = t.route_id;

COMMIT;
