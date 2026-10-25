# EcoRoute Database

PostgreSQL schema and sample data for EcoRoute. Tables and columns use `snake_case`, so Npgsql queries need no quoted identifiers.

## Files

- `schema.sql` drops and recreates every table and index. **Running it deletes all existing data.**
- `seed.sql` loads sample data (Yogyakarta locations). It clears the tables first, so it's safe to re-run.

## Setup

Run from the repository root:

```bash
createdb ecoroute                                   # once; skip if it already exists
psql -v ON_ERROR_STOP=1 -d ecoroute -f EcoRoute/Database/schema.sql
psql -v ON_ERROR_STOP=1 -d ecoroute -f EcoRoute/Database/seed.sql
```

`-v ON_ERROR_STOP=1` makes psql stop at the first error instead of continuing. Each file runs in a single transaction, so a failure leaves the database unchanged.

## Verify

```bash
psql -d ecoroute -c '\dt'
psql -d ecoroute -c "SELECT jenis_kendaraan, faktor_emisi_dasar FROM emission_factor;"
```

## Notes

- `emission_factor.jenis_kendaraan` values (`Motor`, `Mobil`, `Bus`, `Kereta`) map to the `Motorcycle`, `Car`, `Bus` and `Train` classes.
- `trip_history.vehicle_id` is `NULL` for public transport trips.
- If the server isn't on the default port 5432, run `export PGPORT=<port>` first (the local dev server here uses 5433).
- Example Npgsql connection string: `Host=localhost;Port=5433;Database=ecoroute;Username=<user>;Password=<password>`
