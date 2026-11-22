"""
Ambil data trayek Trans Jogja dari OpenStreetMap (Overpass API) lalu simpan sebagai
EcoRoute/Data/trayek-transjogja.json. Dijalankan sekali oleh developer saat ingin
memperbarui data, BUKAN saat aplikasi berjalan (server Overpass sering penuh).

Yang disimpan per trayek:
- searah: jalan yang dilalui bus ke satu arah saja, simpul diurutkan sesuai arah bus
- ruas  : jalan yang boleh ditempuh ke dua arah (arah tidak diketahui, atau bus lewat bolak-balik)
- halte : nama, koordinat, dan simpul jalan tempat bus berhenti

Arah tiap jalan ditentukan berurutan dari:
1. tag oneway / junction=roundabout (kecuali ada oneway:bus=no atau oneway:psv=no)
2. role "forward" / "backward" di relasi
3. urutan member relasi: jalan disambung dari ujung jalan sebelumnya
Kalau tidak ada yang bisa dipakai, jalan dianggap dua arah.

Urutan halte di relasi OSM tidak bisa diandalkan (sering melompat-lompat), jadi aplikasi
mencari jalur bus dari halte naik ke halte turun di dalam jaringan berarah trayek itu
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
BATAS_SAMBUNG_ARAH_M = 100   # ujung jalan satu arah disambung ke awal jalan satu arah lain sedekat ini


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


def arah_oneway(tags):
    """+1 = bus hanya boleh searah urutan simpul way, -1 = hanya kebalikannya, 0 = dua arah."""
    if tags.get("oneway:bus") == "no" or tags.get("oneway:psv") == "no":
        return 0
    oneway = tags.get("oneway")
    if oneway in ("yes", "1", "true"):
        return 1
    if oneway == "-1":
        return -1
    if oneway is None and tags.get("junction") in ("roundabout", "circular"):
        return 1
    return 0


def arah_dari_urutan(urutan):
    """Arah tiap way dari urutan member relasi: +1 searah urutan simpul, -1 terbalik, 0 tidak diketahui.

    Way disambung ke jalan sebelumnya: ujung yang menempel ke jalan sebelumnya adalah tempat bus masuk.
    Kalau tidak menempel (data putus), arah ditebak dari ujung yang menempel ke jalan berikutnya.
    """
    hasil, keluar_sebelumnya = [], None
    for i, s in enumerate(urutan):
        awal, akhir = s[0], s[-1]
        arah = 0
        if awal != akhir:
            if keluar_sebelumnya is not None:
                if awal == keluar_sebelumnya:
                    arah = 1
                elif akhir == keluar_sebelumnya:
                    arah = -1
            if arah == 0 and i > 0:
                sebelum = set(urutan[i - 1])
                if awal in sebelum and akhir not in sebelum:
                    arah = 1
                elif akhir in sebelum and awal not in sebelum:
                    arah = -1
            if arah == 0 and i + 1 < len(urutan):
                sesudah = set(urutan[i + 1])
                if akhir in sesudah and awal not in sesudah:
                    arah = 1
                elif awal in sesudah and akhir not in sesudah:
                    arah = -1
        hasil.append(arah)
        keluar_sebelumnya = akhir if arah == 1 else awal if arah == -1 else None
    return hasil


def porsi_halte_terjangkau(searah, dua_arah, simpul_halte):
    """Porsi halte yang bisa dicapai bus dari halte awal terbaik, mengikuti arah jalan.

    1.0 = ada satu halte (awal trayek) yang dari situ semua halte lain bisa dicapai.
    Kurang dari itu berarti jaringan berarah putus, biasanya karena arah way di OSM tidak konsisten.
    """
    maju = {}
    for s in searah:
        for a, b in zip(s, s[1:]):
            maju.setdefault(a, set()).add(b)
    for s in dua_arah:
        for a, b in zip(s, s[1:]):
            maju.setdefault(a, set()).add(b)
            maju.setdefault(b, set()).add(a)

    target = set(simpul_halte)
    if len(target) < 2:
        return 1.0
    terbaik = 0
    for mulai in target:
        sudah, tumpuk = {mulai}, [mulai]
        while tumpuk:
            for v in maju.get(tumpuk.pop(), ()):
                if v not in sudah:
                    sudah.add(v)
                    tumpuk.append(v)
        terbaik = max(terbaik, len(target & sudah))
    return terbaik / len(target)


def main():
    print("Mengambil data dari Overpass...")
    data = ambil_overpass()
    node = {el["id"]: el for el in data["elements"] if el["type"] == "node"}
    way = {el["id"]: el["nodes"] for el in data["elements"] if el["type"] == "way"}
    tag_way = {el["id"]: el.get("tags", {}) for el in data["elements"] if el["type"] == "way"}
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

        # Way trayek sesuai urutan relasi (way yang sama bisa muncul lebih dari sekali)
        member_way = []
        for m in r["members"]:
            if m["type"] == "way" and m["role"] in ("", "forward", "backward") and m["ref"] in way:
                simpul = [n for n in way[m["ref"]] if n in node]
                if len(simpul) >= 2:
                    member_way.append((m["ref"], m["role"], simpul))

        arah_urutan = arah_dari_urutan([s for _, _, s in member_way])

        # Kumpulkan arah tiap way dari semua kemunculannya. 0 = tidak diketahui.
        arah_way, simpul_way = {}, {}
        for (ref, role, simpul), dari_urutan in zip(member_way, arah_urutan):
            arah = arah_oneway(tag_way[ref])
            if arah == 0:
                arah = {"forward": 1, "backward": -1}.get(role, dari_urutan)
            arah_way.setdefault(ref, set()).add(arah)
            simpul_way[ref] = simpul

        searah, ruas = [], []  # ruas = dua arah
        for ref, semua_arah in arah_way.items():
            simpul = simpul_way[ref]
            if semua_arah == {1}:
                searah.append(simpul)
            elif semua_arah == {-1}:
                searah.append(simpul[::-1])
            else:  # arah tidak diketahui, atau bus lewat bolak-balik
                ruas.append(simpul)

        # Untuk sambung celah & tempel halte, arah tidak penting
        tetangga = {}
        for s in searah + ruas:
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
                searah = [s for s in searah if not set(s) <= kecil]
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

        # Sambung celah berarah: way persimpangan yang tidak ikut di relasi membuat ujung satu jalan
        # satu arah (buntu) tidak tersambung ke awal jalan berikutnya. Sambung yang terdekat lebih dulu.
        keluar, masuk = {}, {}
        for s in searah:
            for a, b in zip(s, s[1:]):
                keluar.setdefault(a, set()).add(b)
                masuk.setdefault(b, set()).add(a)
        for s in ruas:
            for a, b in zip(s, s[1:]):
                for x, y in ((a, b), (b, a)):
                    keluar.setdefault(x, set()).add(y)
                    masuk.setdefault(y, set()).add(x)
        buntu = [n for n in tetangga if not keluar.get(n)]
        tanpa_masuk = [n for n in tetangga if not masuk.get(n)]
        pasangan = sorted((jarak_m(koord(a), koord(b)), a, b) for a in buntu for b in tanpa_masuk if a != b)
        dipakai_a, dipakai_b, disambung_arah = set(), set(), 0
        for jarak, a, b in pasangan:
            if jarak > BATAS_SAMBUNG_ARAH_M:
                break
            if a in dipakai_a or b in dipakai_b:
                continue
            dipakai_a.add(a)
            dipakai_b.add(b)
            searah.append([a, b])
            disambung_arah += 1

        semua_simpul = list(tetangga.keys())

        # Halte: gabung platform & stop dari halte yang sama. Stop position yang memang ada di jalan
        # trayek dipakai apa adanya (sudah di lajur yang benar); selain itu tempel ke simpul terdekat.
        # Stop diproses lebih dulu supaya simpulnya yang dipakai kalau ada platform dari halte yang sama.
        member_halte = [m for m in r["members"]
                        if m["type"] == "node" and m["ref"] in node
                        and (m["role"].startswith("stop") or m["role"].startswith("platform"))]
        member_halte.sort(key=lambda m: not m["role"].startswith("stop"))

        halte = []
        for m in member_halte:
            k = koord(m["ref"])
            nama = node[m["ref"]].get("tags", {}).get("name")
            if m["ref"] in tetangga:
                terdekat = m["ref"]
            else:
                terdekat = min(semua_simpul, key=lambda n: jarak_m(k, koord(n)))
                if jarak_m(k, koord(terdekat)) > BATAS_TEMPEL_HALTE_M:
                    continue
            sama = next((h for h in halte if jarak_m(h["k"], k) < 60 and
                         (nama is None or h["nama"] is None or h["nama"] == nama)), None)
            if sama:
                sama["nama"] = sama["nama"] or nama
                continue
            halte.append({"nama": nama, "k": k, "simpul": terdekat})

        panjang = sum(jarak_m(koord(a), koord(b)) for s in searah + ruas for a, b in zip(s, s[1:])) / 1000
        terjangkau = porsi_halte_terjangkau(searah, ruas, [h["simpul"] for h in halte])
        print(f"  {kode:4} {len(halte):3} halte, {len(searah):4} searah + {len(ruas):4} dua arah, "
              f"{panjang:5.1f} km jalan, {terjangkau:4.0%} halte terjangkau"
              + (f", {disambung} celah disambung" if disambung else "")
              + (f", {disambung_arah} celah arah disambung" if disambung_arah else ""))

        trayek.append({
            "kode": kode,
            "nama": tags.get("name", ""),
            "searah": [[nomor_simpul(n) for n in s] for s in searah],
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
