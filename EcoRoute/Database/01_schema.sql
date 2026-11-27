-- =====================================================================
-- EcoRoute: skema database PostgreSQL (Neon)
-- Dijalankan sekali oleh admin (pemilik database), BUKAN oleh aplikasi.
-- Urutan: 01_schema.sql -> 02_seed.sql -> 03_functions.sql -> 04_roles.sql
-- Aman dijalankan ulang (IF NOT EXISTS / OR REPLACE).
-- =====================================================================

CREATE TABLE IF NOT EXISTS emission_factor (
    emission_factor_id       integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    jenis_kendaraan          text NOT NULL UNIQUE,
    kategori                 text NOT NULL
        CHECK (kategori IN ('bermotor', 'aktif')),
    faktor_emisi_dasar       double precision NOT NULL
        CHECK (faktor_emisi_dasar >= 0),
    faktor_penalti_kemacetan double precision NOT NULL DEFAULT 1.0
        CHECK (faktor_penalti_kemacetan >= 1.0),
    updated_at               timestamptz NOT NULL DEFAULT now(),
    CHECK (kategori = 'bermotor' OR faktor_emisi_dasar = 0)
);

CREATE TABLE IF NOT EXISTS users (
    user_id         integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nama_user       text NOT NULL,
    email_user      text NOT NULL UNIQUE
        CHECK (email_user = lower(email_user)),
    password_hash   text,
    total_eco_score integer NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS vehicle (
    vehicle_id         integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id            integer NOT NULL
        REFERENCES users(user_id) ON DELETE CASCADE,
    emission_factor_id integer NOT NULL
        REFERENCES emission_factor(emission_factor_id),
    tahun_kendaraan    integer
        CHECK (tahun_kendaraan BETWEEN 1950 AND 2100)
);

CREATE TABLE IF NOT EXISTS route (
    route_id        integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id         integer NOT NULL
        REFERENCES users(user_id) ON DELETE CASCADE,
    nama_asal       text,
    nama_tujuan     text,
    titik_awal_lat  double precision NOT NULL
        CHECK (titik_awal_lat BETWEEN -90 AND 90),
    titik_awal_lng  double precision NOT NULL
        CHECK (titik_awal_lng BETWEEN -180 AND 180),
    titik_akhir_lat double precision NOT NULL
        CHECK (titik_akhir_lat BETWEEN -90 AND 90),
    titik_akhir_lng double precision NOT NULL
        CHECK (titik_akhir_lng BETWEEN -180 AND 180),
    jarak           double precision NOT NULL CHECK (jarak >= 0),
    estimasi_waktu  integer NOT NULL CHECK (estimasi_waktu >= 0),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS route_segment (
    segment_id    integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    route_id      integer NOT NULL
        REFERENCES route(route_id) ON DELETE CASCADE,
    urutan_segmen integer NOT NULL CHECK (urutan_segmen >= 1),
    jarak_segmen  double precision NOT NULL CHECK (jarak_segmen >= 0),
    UNIQUE (route_id, urutan_segmen)
);

CREATE TABLE IF NOT EXISTS trip_history (
    trip_id        integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id        integer NOT NULL
        REFERENCES users(user_id) ON DELETE CASCADE,
    vehicle_id     integer
        REFERENCES vehicle(vehicle_id) ON DELETE SET NULL,
    route_id       integer NOT NULL
        REFERENCES route(route_id) ON DELETE CASCADE,
    tanggal        timestamptz NOT NULL DEFAULT now(),
    moda_dipilih   text NOT NULL
        REFERENCES emission_factor(jenis_kendaraan) ON UPDATE CASCADE,
    total_emisi_kg double precision NOT NULL CHECK (total_emisi_kg >= 0)
);

CREATE TABLE IF NOT EXISTS trip_mode_comparison (
    comparison_id             integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    trip_id                   integer NOT NULL
        REFERENCES trip_history(trip_id) ON DELETE CASCADE,
    jenis_kendaraan_bandingan text NOT NULL
        REFERENCES emission_factor(jenis_kendaraan) ON UPDATE CASCADE,
    estimasi_emisi_kg         double precision NOT NULL
        CHECK (estimasi_emisi_kg >= 0),
    UNIQUE (trip_id, jenis_kendaraan_bandingan)
);

CREATE TABLE IF NOT EXISTS trip_segment_snapshot (
    snapshot_id       integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    trip_id           integer NOT NULL
        REFERENCES trip_history(trip_id) ON DELETE CASCADE,
    segment_id        integer NOT NULL
        REFERENCES route_segment(segment_id) ON DELETE CASCADE,
    tingkat_kemacetan text
        CHECK (tingkat_kemacetan IN ('lancar', 'sedang', 'padat', 'macet')),
    intensitas_polusi double precision,
    recorded_at       timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_vehicle_user_id ON vehicle(user_id);
CREATE INDEX IF NOT EXISTS idx_vehicle_emission_factor_id ON vehicle(emission_factor_id);
CREATE INDEX IF NOT EXISTS idx_route_user_id ON route(user_id);
CREATE INDEX IF NOT EXISTS idx_route_segment_route_id ON route_segment(route_id);
CREATE INDEX IF NOT EXISTS idx_trip_history_user_tanggal ON trip_history(user_id, tanggal DESC);
CREATE INDEX IF NOT EXISTS idx_trip_history_vehicle_id ON trip_history(vehicle_id);
CREATE INDEX IF NOT EXISTS idx_trip_history_route_id ON trip_history(route_id);
CREATE INDEX IF NOT EXISTS idx_trip_history_moda_dipilih ON trip_history(moda_dipilih);
CREATE INDEX IF NOT EXISTS idx_trip_segment_snapshot_trip_id ON trip_segment_snapshot(trip_id);
CREATE INDEX IF NOT EXISTS idx_trip_segment_snapshot_segment_id ON trip_segment_snapshot(segment_id);

CREATE OR REPLACE VIEW v_riwayat_perjalanan AS
SELECT
    t.trip_id,
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
JOIN users u ON u.user_id = t.user_id
JOIN route r ON r.route_id = t.route_id;
