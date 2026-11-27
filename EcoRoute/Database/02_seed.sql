-- =====================================================================
-- EcoRoute: data awal faktor emisi (kg CO2 per km)
-- Aman dijalankan ulang.
-- =====================================================================

INSERT INTO emission_factor
    (jenis_kendaraan, kategori, faktor_emisi_dasar, faktor_penalti_kemacetan)
VALUES
    ('Motor',      'bermotor', 0.1030, 1.1500),
    ('Mobil',      'bermotor', 0.1920, 1.3500),
    ('Bus',        'bermotor', 0.0890, 1.2500),
    ('Sepeda',     'aktif',    0.0000, 1.0000),
    ('Jalan Kaki', 'aktif',    0.0000, 1.0000)
ON CONFLICT (jenis_kendaraan) DO NOTHING;
