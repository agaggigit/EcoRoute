-- EcoRoute database schema (PostgreSQL)
-- Run: psql -v ON_ERROR_STOP=1 -d ecoroute -f EcoRoute/Database/schema.sql
-- WARNING: drops and recreates every table, all data is lost.

BEGIN;

-- Drop in reverse dependency order (children first)
DROP TABLE IF EXISTS trip_segment_snapshot;
DROP TABLE IF EXISTS trip_mode_comparison;
DROP TABLE IF EXISTS trip_history;
DROP TABLE IF EXISTS route_segment;
DROP TABLE IF EXISTS route;
DROP TABLE IF EXISTS vehicle;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS emission_factor;

-- Emission factor per vehicle type (Motor, Mobil, Bus, Kereta)
CREATE TABLE emission_factor (
    emission_factor_id       SERIAL PRIMARY KEY,
    jenis_kendaraan          VARCHAR(50)    NOT NULL UNIQUE,
    faktor_emisi_dasar       NUMERIC(10,4)  NOT NULL,              -- kg CO2 per km
    faktor_penalti_kemacetan NUMERIC(10,4)  NOT NULL DEFAULT 1.0,
    updated_at               TIMESTAMP      NOT NULL DEFAULT NOW()
);

-- "users" rather than "user" because USER is a reserved word
CREATE TABLE users (
    user_id         SERIAL PRIMARY KEY,
    nama_user       VARCHAR(100) NOT NULL,
    email_user      VARCHAR(150) NOT NULL UNIQUE,
    total_eco_score INTEGER      NOT NULL DEFAULT 0
);

CREATE TABLE vehicle (
    vehicle_id         SERIAL PRIMARY KEY,
    user_id            INT NOT NULL REFERENCES users (user_id) ON DELETE CASCADE,
    emission_factor_id INT NOT NULL REFERENCES emission_factor (emission_factor_id),
    tahun_kendaraan    INT CHECK (tahun_kendaraan BETWEEN 1950 AND 2100)
);

CREATE TABLE route (
    route_id        SERIAL PRIMARY KEY,
    user_id         INT           NOT NULL REFERENCES users (user_id) ON DELETE CASCADE,
    titik_awal_lat  NUMERIC(9,6)  NOT NULL,
    titik_awal_lng  NUMERIC(9,6)  NOT NULL,
    titik_akhir_lat NUMERIC(9,6)  NOT NULL,
    titik_akhir_lng NUMERIC(9,6)  NOT NULL,
    jarak           NUMERIC(10,3) NOT NULL,                        -- km
    estimasi_waktu  INT           NOT NULL,                        -- minutes
    created_at      TIMESTAMP     NOT NULL DEFAULT NOW()
);

CREATE TABLE route_segment (
    segment_id    SERIAL PRIMARY KEY,
    route_id      INT           NOT NULL REFERENCES route (route_id) ON DELETE CASCADE,
    urutan_segmen INT           NOT NULL,
    jarak_segmen  NUMERIC(10,3) NOT NULL,                          -- km
    UNIQUE (route_id, urutan_segmen)
);

CREATE TABLE trip_history (
    trip_id              SERIAL PRIMARY KEY,
    user_id              INT         NOT NULL REFERENCES users (user_id) ON DELETE CASCADE,
    -- NULL for public transport (Bus, Kereta): no personal vehicle
    vehicle_id           INT         NULL REFERENCES vehicle (vehicle_id) ON DELETE SET NULL,
    route_id             INT         NOT NULL REFERENCES route (route_id),
    tanggal              TIMESTAMP   NOT NULL DEFAULT NOW(),
    total_paparan_polusi NUMERIC(10,4),
    moda_dipilih         VARCHAR(50) NOT NULL
);

CREATE TABLE trip_mode_comparison (
    comparison_id             SERIAL PRIMARY KEY,
    trip_id                   INT           NOT NULL REFERENCES trip_history (trip_id) ON DELETE CASCADE,
    jenis_kendaraan_bandingan VARCHAR(50)   NOT NULL,
    estimasi_polusi_bandingan NUMERIC(10,4) NOT NULL
);

CREATE TABLE trip_segment_snapshot (
    snapshot_id       SERIAL PRIMARY KEY,
    trip_id           INT         NOT NULL REFERENCES trip_history (trip_id) ON DELETE CASCADE,
    segment_id        INT         NOT NULL REFERENCES route_segment (segment_id),
    tingkat_kemacetan VARCHAR(20) CHECK (tingkat_kemacetan IN ('lancar', 'sedang', 'padat', 'macet')),
    intensitas_polusi NUMERIC(10,4),
    recorded_at       TIMESTAMP   NOT NULL DEFAULT NOW()
);

-- Indexes on foreign key columns
CREATE INDEX idx_vehicle_user_id                  ON vehicle (user_id);
CREATE INDEX idx_vehicle_emission_factor_id       ON vehicle (emission_factor_id);
CREATE INDEX idx_route_user_id                    ON route (user_id);
CREATE INDEX idx_route_segment_route_id           ON route_segment (route_id);
CREATE INDEX idx_trip_history_user_id             ON trip_history (user_id);
CREATE INDEX idx_trip_history_vehicle_id          ON trip_history (vehicle_id);
CREATE INDEX idx_trip_history_route_id            ON trip_history (route_id);
CREATE INDEX idx_trip_mode_comparison_trip_id     ON trip_mode_comparison (trip_id);
CREATE INDEX idx_trip_segment_snapshot_trip_id    ON trip_segment_snapshot (trip_id);
CREATE INDEX idx_trip_segment_snapshot_segment_id ON trip_segment_snapshot (segment_id);

COMMIT;
