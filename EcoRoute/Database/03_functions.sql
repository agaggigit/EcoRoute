-- =====================================================================
-- EcoRoute: fungsi "loket" untuk aplikasi
--
-- Aplikasi desktop terhubung langsung ke database tanpa backend, dan
-- connection string tertanam di exe. Supaya kredensial itu tidak bisa
-- dipakai untuk membaca atau mengubah tabel sembarangan, role aplikasi
-- (ecoroute_app) tidak diberi akses ke tabel. Aplikasi hanya boleh
-- memanggil fungsi-fungsi di bawah ini.
--
-- SECURITY DEFINER: fungsi berjalan dengan hak pemiliknya (admin),
-- jadi fungsi bisa mengakses tabel walaupun pemanggilnya tidak bisa.
-- search_path dikunci supaya fungsi tidak bisa dibelokkan ke objek palsu.
-- =====================================================================

-- Daftar akun baru. Hash password dibuat di aplikasi (PBKDF2-SHA256).
-- Email ganda menghasilkan unique_violation (SQLSTATE 23505).
CREATE OR REPLACE FUNCTION fn_register(
    p_nama text,
    p_email text,
    p_password_hash text)
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_user_id integer;
BEGIN
    IF coalesce(trim(p_nama), '') = '' OR coalesce(trim(p_email), '') = '' THEN
        RAISE EXCEPTION 'Nama dan email wajib diisi.'
            USING ERRCODE = 'check_violation';
    END IF;

    IF coalesce(p_password_hash, '') NOT LIKE 'PBKDF2-SHA256$%' THEN
        RAISE EXCEPTION 'Format hash password tidak valid.'
            USING ERRCODE = 'check_violation';
    END IF;

    INSERT INTO users (nama_user, email_user, password_hash)
    VALUES (trim(p_nama), lower(trim(p_email)), p_password_hash)
    RETURNING user_id INTO v_user_id;

    RETURN v_user_id;
END;
$$;

-- Data login untuk SATU email. Tidak ada cara mengambil daftar semua akun.
CREATE OR REPLACE FUNCTION fn_ambil_login(p_email text)
RETURNS TABLE (
    user_id integer,
    nama_user text,
    email_user text,
    password_hash text)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
    SELECT u.user_id, u.nama_user, u.email_user, u.password_hash
    FROM users u
    WHERE u.email_user = lower(trim(p_email));
$$;

-- Simpan rute + perjalanan dalam satu transaksi. Mengembalikan trip_id.
CREATE OR REPLACE FUNCTION fn_simpan_perjalanan(
    p_user_id integer,
    p_nama_asal text,
    p_nama_tujuan text,
    p_lat_asal double precision,
    p_lng_asal double precision,
    p_lat_tujuan double precision,
    p_lng_tujuan double precision,
    p_jarak_km double precision,
    p_estimasi_waktu_menit integer,
    p_moda text,
    p_total_emisi_kg double precision)
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_route_id integer;
    v_trip_id integer;
BEGIN
    INSERT INTO route (
        user_id, nama_asal, nama_tujuan,
        titik_awal_lat, titik_awal_lng,
        titik_akhir_lat, titik_akhir_lng,
        jarak, estimasi_waktu)
    VALUES (
        p_user_id, p_nama_asal, p_nama_tujuan,
        p_lat_asal, p_lng_asal,
        p_lat_tujuan, p_lng_tujuan,
        p_jarak_km, p_estimasi_waktu_menit)
    RETURNING route_id INTO v_route_id;

    INSERT INTO trip_history (user_id, route_id, moda_dipilih, total_emisi_kg)
    VALUES (p_user_id, v_route_id, p_moda, p_total_emisi_kg)
    RETURNING trip_id INTO v_trip_id;

    RETURN v_trip_id;
END;
$$;

-- Riwayat perjalanan satu pengguna, terbaru di atas.
CREATE OR REPLACE FUNCTION fn_riwayat(p_user_id integer)
RETURNS TABLE (
    trip_id integer,
    tanggal timestamptz,
    nama_asal text,
    nama_tujuan text,
    jarak_km double precision,
    moda_dipilih text,
    total_emisi_kg double precision,
    titik_awal_lat double precision,
    titik_awal_lng double precision,
    titik_akhir_lat double precision,
    titik_akhir_lng double precision,
    estimasi_waktu_menit integer)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
    SELECT
        t.trip_id, t.tanggal, r.nama_asal, r.nama_tujuan, r.jarak,
        t.moda_dipilih, t.total_emisi_kg,
        r.titik_awal_lat, r.titik_awal_lng,
        r.titik_akhir_lat, r.titik_akhir_lng,
        r.estimasi_waktu
    FROM trip_history t
    JOIN route r ON r.route_id = t.route_id
    WHERE t.user_id = p_user_id
    ORDER BY t.tanggal DESC, t.trip_id DESC;
$$;
