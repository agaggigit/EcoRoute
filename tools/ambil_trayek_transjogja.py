"""
Ambil data trayek Trans Jogja dari OpenStreetMap (Overpass API) lalu simpan sebagai
EcoRoute/Data/trayek-transjogja.json. Dijalankan sekali oleh developer saat ingin
memperbarui data, BUKAN saat aplikasi berjalan (server Overpass sering penuh).

Yang disimpan per trayek:
- ruas : jalan-jalan yang dilalui trayek (deretan indeks ke tabel "simpul")
- halte: nama, koordinat, dan simpul jalan terdekat

Urutan halte & ruas di relasi OSM tidak bisa diandalkan (sering melompat-lompat), jadi
aplikasi mencari jalur bus dari halte naik ke halte turun di dalam jaringan ruas trayek itu
sendiri (lihat Services/TransJogjaService.cs).

Cara pakai (dari folder repo):
    python tools/ambil_trayek_transjogja.py

Data (c) OpenStreetMap contributors, lisensi ODbL.
"""
import json
import math
import sys
import time
import urllib.parse
import urllib.request
from datetime import date
from pathlib import Path

OVERPASS = [
    "https://overpass-api.de/api/interpreter",
    "https://overpass.private.coffee/api/interpreter",
]
USER_AGENT = "EcoRoute-data-script (tugas kuliah)"

# Kotak sekitar DIY (selatan, barat, utara, timur)
QUERY = """
[out:json][timeout:120];
relation["route"="bus"]["network"="Trans Jogja"](-8.2,110.0,-7.5,110.8)->.r;
.r out body;
way(r.r)->.w;
.w out body qt;
(node(w.w); node(r.r););
out body qt;
"""
OUTPUT = Path(__file__).resolve().parent.parent / "EcoRoute" / "Data" / "trayek-transjogja.json"

BATAS_TEMPEL_HALTE_M = 100   # halte lebih jauh dari ini ke jalan trayek dianggap data salah
BATAS_SAMBUNG_CELAH_M = 400  # ruas yang putus disambung kalau celahnya sedekat ini


def ambil_overpass():
    data = urllib.parse.urlencode({"data": QUERY}).encode()
    for url in OVERPASS:
        for percobaan in range(3):
            try:
                req = urllib.request.Request(url, data=data, headers={"User-Agent": USER_AGENT})
                with urllib.request.urlopen(req, timeout=180) as resp:
                    return json.load(resp)
            except Exception as e:  # server penuh / timeout -> coba lagi
                print(f"  gagal {url} ({e}), coba lagi...", file=sys.stderr)
                time.sleep(5 * (percobaan + 1))
    raise SystemExit("Semua server Overpass gagal. Coba lagi nanti.")


def jarak_m(a, b):
    lat1, lon1, lat2, lon2 = map(math.radians, (a[0], a[1], b[0], b[1]))
    h = math.sin((lat2 - lat1) / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin((lon2 - lon1) / 2) ** 2
    return 6371000 * 2 * math.asin(math.sqrt(h))


def komponen(simpul_set, tetangga):
    """Kelompok simpul yang saling tersambung."""
    sisa, hasil = set(simpul_set), []
    while sisa:
        mulai = sisa.pop()
        grup, tumpuk = {mulai}, [mulai]
        while tumpuk:
            u = tumpuk.pop()
            for v in tetangga.get(u, ()):
                if v in sisa:
                    sisa.remove(v)
                    grup.add(v)
                    tumpuk.append(v)
        hasil.append(grup)
    return hasil


def main():
    print("Mengambil data dari Overpass...")
    data = ambil_overpass()
    node = {el["id"]: el for el in data["elements"] if el["type"] == "node"}
    way = {el["id"]: el["nodes"] for el in data["elements"] if el["type"] == "way"}
    relasi = [el for el in data["elements"] if el["type"] == "relation"]

    def koord(n):
        return (node[n]["lat"], node[n]["lon"])

    tabel_simpul, nomor = [], {}

    def nomor_simpul(n):
        if n not in nomor:
            nomor[n] = len(tabel_simpul)
            lat, lon = koord(n)
            tabel_simpul.append([round(lat, 6), round(lon, 6)])
        return nomor[n]

    trayek = []
    for r in sorted(relasi, key=lambda x: x["tags"].get("ref", "")):
        tags = r["tags"]
        kode = tags.get("ref", "?")

        # Ruas jalan trayek (setiap way cukup sekali)
        ruas, sudah = [], set()
        for m in r["members"]:
            if m["type"] == "way" and m["role"] in ("", "forward", "backward") and m["ref"] in way:
                if m["ref"] in sudah:
                    continue
                sudah.add(m["ref"])
                simpul = [n for n in way[m["ref"]] if n in node]
                if len(simpul) >= 2:
                    ruas.append(simpul)

        tetangga = {}
        for s in ruas:
            for a, b in zip(s, s[1:]):
                tetangga.setdefault(a, set()).add(b)
                tetangga.setdefault(b, set()).add(a)

        # Sambung celah: kelompok ruas yang terputus disambung ke kelompok terdekat
        disambung = 0
        while True:
            grup = sorted(komponen(tetangga.keys(), tetangga), key=len)
            if len(grup) <= 1:
                break
            kecil, lain = grup[0], set().union(*grup[1:])
            terbaik = min(((jarak_m(koord(a), koord(b)), a, b) for a in kecil for b in lain), key=lambda x: x[0])
            if terbaik[0] > BATAS_SAMBUNG_CELAH_M:
                # Terlalu jauh: buang kelompok kecil itu (biasanya potongan data yang salah)
                ruas = [s for s in ruas if not set(s) <= kecil]
                for u in kecil:
                    tetangga.pop(u, None)
                for u in tetangga:
                    tetangga[u] -= kecil
                continue
            _, a, b = terbaik
            ruas.append([a, b])
            tetangga[a].add(b)
            tetangga[b].add(a)
            disambung += 1

        semua_simpul = list(tetangga.keys())

        # Halte: tempel ke simpul jalan trayek terdekat; gabung platform & stop dari halte yang sama
        halte = []
        for m in r["members"]:
            if m["type"] != "node" or m["ref"] not in node:
                continue
            if not (m["role"].startswith("stop") or m["role"].startswith("platform")):
                continue
            k = koord(m["ref"])
            nama = node[m["ref"]].get("tags", {}).get("name")
            terdekat = min(semua_simpul, key=lambda n: jarak_m(k, koord(n)))
            jarak = jarak_m(k, koord(terdekat))
            if jarak > BATAS_TEMPEL_HALTE_M:
                continue
            sama = next((h for h in halte if jarak_m(h["k"], k) < 60 and
                         (nama is None or h["nama"] is None or h["nama"] == nama)), None)
            if sama:
                sama["nama"] = sama["nama"] or nama
                continue
            halte.append({"nama": nama, "k": k, "simpul": terdekat})

        panjang = sum(jarak_m(koord(a), koord(b)) for s in ruas for a, b in zip(s, s[1:])) / 1000
        print(f"  {kode:4} {len(halte):3} halte, {len(ruas):4} ruas, {panjang:5.1f} km jalan"
              + (f", {disambung} celah disambung" if disambung else ""))

        trayek.append({
            "kode": kode,
            "nama": tags.get("name", ""),
            "ruas": [[nomor_simpul(n) for n in s] for s in ruas],
            "halte": [{"nama": h["nama"] or "Halte tanpa nama", "lat": round(h["k"][0], 6),
                       "lng": round(h["k"][1], 6), "simpul": nomor_simpul(h["simpul"])} for h in halte],
        })

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT, "w", encoding="utf-8") as f:
        json.dump({
            "sumber": "OpenStreetMap contributors (ODbL), diambil lewat Overpass API",
            "diambil": date.today().isoformat(),
            "simpul": tabel_simpul,
            "trayek": trayek,
        }, f, ensure_ascii=False, separators=(",", ":"))
    print(f"Tersimpan: {OUTPUT} ({OUTPUT.stat().st_size // 1024} KB, {len(trayek)} trayek)")


if __name__ == "__main__":
    main()
