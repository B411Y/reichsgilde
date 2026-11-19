"""Pruefung 37 (Pixel-Look, reine Baender): Wie sieht eine Flaeche im Pixel-Look aus, wenn der Shader die grobe Mip-Stufe liest?

Der Haeuser-Shader (building_trim.gdshaderinc) liest im Pixel-Look aussen Farbe, Rauheit und Normale um pixel_mip (2,5) Mip-Stufen
groeber, die Farbe der Steinbaender um stein_mip (1) und zur Haelfte (stein_ruhe) um pixel_mip. Die Mip-Stufen erzeugt Godot beim Import
(2 x 2 gemittelt im gespeicherten Wert). Bei Zoom 25, 50 und 100 liegt die Grundstufe im Pixel-Look (Pixel 2) bei etwa 5,15, 6,15 und
7,15 (Pixel 3 je 0,58 hoeher), mit dem Zuschlag also bei 7,6 bis 10,2: Ein Texel umfasst dann 128 bis 1024 Bildpunkte des 2048er Blatts,
und schmale Baender mischen ihre Nachbarn bei (Klicktest NAS 19.11.2026, Halblinge: Dach S3 dunkelgruen statt blau, S2 grau,
Backstein in Streifen wie ein Blockhaus, Glanzfleck auf dem Erkerdach). Fuer die Ebenen mit BAND_REIN liest der Shader die Dach- und
Steinbaender je Stufe nur aus Texeln im eigenen Band (band_lesen); diese Pruefung rechnet beides nach (numpy, ohne Blender und GPU).

Je Flaeche der GLB (Ecken und Schwerpunkt der UV) im Pixel-Look gegen ohne Pixel-Look (Grundstufe eine feiner, ohne Zuschlag):
  (a) Deckung blau: Dachband „stroh_franse“ im Pixel-Look Blau > 1,05 Rot und Blau > Gruen (wie 26c an der Textur);
  (b) Deckung matt: Rauheit des Dachbands im Pixel-Look hoechstens 0,05 unter der ohne Pixel-Look (sonst Glanzflecken);
  (d) Efeu (Stoff Efeublatt) im Pixel-Look klar gruen: Gruen ueber 1,15 x Rot und Blau, Helligkeit (sRGB) ab 0,28 (der Efeu vom 19.11. mit 0,21 galt als fast schwarz);
  (c) Stein ohne Streifen: in jedem Steinband, das die Waende nutzen, schwankt die Helligkeit quer zum Band (ueber den v-Bereich der
      Flaechen, Mittel ueber u) im Pixel-Look hoechstens um den Faktor 1,15 (ohne Pixel-Look 1,10), und Metall bleibt unter 0,05.

    python tools/modelle/neubau/pixel_mip_pruefung.py <haus.glb> <blatt_albedo.png> [--stein=0.85,0.95[,a,b]] [--alter-shader]

--alter-shader rechnet den Shader vor BAND_REIN nach (Gegenprobe: muss bei den Halblingen S2/S3 Fehler finden).
"""
import json
import os
import struct
import sys

import numpy as np
from PIL import Image

GRUND = {"Pixel 2": (5.15, 6.15, 7.15), "Pixel 3": (5.735, 6.735, 7.735)}   # Grundstufe im Pixel-Look bei Zoom 25, 50, 100
PIXEL_MIP, STEIN_MIP, STEIN_RUHE = 2.5, 1.0, 0.5    # building_trim.gdshaderinc
DACH = ((0.0, 0.1875), (0.1875, 0.25))              # Baender „stroh“ und „stroh_franse“
BAENDER = {"stroh": (0.0, 0.1875), "stroh_franse": (0.1875, 0.25), "balken": (0.25, 0.5), "putz": (0.5, 0.7),
           "planken": (0.7, 0.775), "farbholz": (0.775, 0.85), "bruchstein": (0.85, 0.95), "eisen": (0.95, 1.0)}
LUM = np.array([0.2126, 0.7152, 0.0722])


def glb_lesen(pfad):
    """Netze einer GLB: Liste (Netzname, Stoff, uv[n,2], dreiecke[m,3], orte[n,3])."""
    with open(pfad, "rb") as f:
        daten = f.read()
    jl, _ = struct.unpack_from("<II", daten, 12)
    j = json.loads(daten[20:20 + jl])
    puf = daten[20 + jl + 8:]

    def lese(acc_i):
        a = j["accessors"][acc_i]
        bv = j["bufferViews"][a["bufferView"]]
        typ = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[a["componentType"]]
        n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
        ab = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        schritt = bv.get("byteStride", 0)
        if schritt and schritt != np.dtype(typ).itemsize * n:
            roh = np.frombuffer(puf, np.uint8, count=schritt * a["count"], offset=ab).reshape(a["count"], schritt)
            return roh[:, :np.dtype(typ).itemsize * n].copy().view(typ).reshape(a["count"], n)
        return np.frombuffer(puf, typ, count=a["count"] * n, offset=ab).reshape(a["count"], n)

    netze = []
    for m in j["meshes"]:
        for p in m["primitives"]:
            if "TEXCOORD_0" not in p["attributes"]: continue
            stoff = j["materials"][p["material"]]["name"] if "material" in p else ""
            netze.append((m.get("name", ""), stoff.split(".")[0], lese(p["attributes"]["TEXCOORD_0"]).astype(np.float64),
                          lese(p["indices"]).reshape(-1, 3).astype(np.int64), lese(p["attributes"]["POSITION"]).astype(np.float64)))
    return netze


def mip_kette(pfad):
    """Mip-Kette wie Godot beim Import (2 x 2 gemittelt im gespeicherten Wert), Werte 0..1."""
    b = np.asarray(Image.open(pfad).convert("RGB"), dtype=np.float32) / 255.0
    kette = [b]
    while kette[-1].shape[0] > 1:
        a = kette[-1]
        kette.append(0.25 * (a[0::2, 0::2] + a[1::2, 0::2] + a[0::2, 1::2] + a[1::2, 1::2]))
    return kette


def _bilinear(bild, u, v):
    h, w = bild.shape[:2]
    x = u * w - 0.5; y = v * h - 0.5
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    fx = (x - x0)[..., None]; fy = (y - y0)[..., None]
    x0 %= w; y0 %= h; x1 = (x0 + 1) % w; y1 = (y0 + 1) % h
    return (bild[y0, x0] * (1 - fx) + bild[y0, x1] * fx) * (1 - fy) + (bild[y1, x0] * (1 - fx) + bild[y1, x1] * fx) * fy


def lesen(kette, u, v, stufe):
    """Trilinear wie die GPU (Wiederholung in u und v); stufe als Zahl oder Feld."""
    s = np.clip(np.broadcast_to(np.asarray(stufe, np.float64), np.shape(u)), 0.0, len(kette) - 1.0)
    a = np.floor(s).astype(int); t = (s - a)[..., None]
    c = np.zeros(np.shape(u) + (3,))
    for m in np.unique(a):
        i = a == m
        lo = _bilinear(kette[m], u[i], v[i])
        hi = _bilinear(kette[min(m + 1, len(kette) - 1)], u[i], v[i])
        c[i] = lo * (1 - t[i]) + hi * t[i]
    return c


def _band_y(y, a, e, m):
    t = 2.0 ** m
    lo = (np.ceil(a / t) + 0.5) * t; hi = (np.floor(e / t) - 0.5) * t
    return np.where(lo <= hi, np.clip(y, lo, np.maximum(lo, hi)), 0.5 * (a + e))


def band_lesen(kette, u, v, v0, v1, stufe):
    """building_trim.band_lesen: je Mip-Stufe v auf die Mitten der Texel geklemmt, die ganz im Band [v0, v1) liegen; Stufe hoechstens
    bis zur groebsten mit einem solchen Texel."""
    h = kette[0].shape[0]
    a, e = v0 * h, v1 * h
    mmax = max([k for k in range(1, 12) if np.floor(e / 2.0 ** k) - 1 >= np.ceil(a / 2.0 ** k)] or [0])
    st = np.clip(stufe, 0.0, mmax)
    m0 = np.floor(st)
    y = (v % 1.0) * h
    c = np.zeros(np.shape(u) + (3,))
    for m in np.unique(m0):
        i = m0 == m
        c0 = _bilinear(kette[int(m)], u[i], _band_y(y[i], a, e, m) / h)
        c1 = _bilinear(kette[min(int(m) + 1, len(kette) - 1)], u[i], _band_y(y[i], a, e, m + 1) / h)
        c[i] = c0 * (1 - (st[i] - m)[..., None]) + c1 * (st[i] - m)[..., None]
    return c


def shader_lesen(kette, u, v, lod, stein_baender, rein, art="farbe", stroh_teil=0.0):
    """Pixel-Look aussen wie building_trim: art "farbe" (Albedo) oder "licht" (Rauheit, Verdeckung, Normale: immer pixel_mip)."""
    lod = np.broadcast_to(np.asarray(lod, np.float64), np.shape(u)).copy()
    vw = v % 1.0
    sb = list(stein_baender) + [-1.0, -1.0]
    st_b = [(sb[0], sb[1]), (sb[2], sb[3])]
    stein = np.zeros(np.shape(u), bool)
    for a, e in st_b: stein |= (vw >= a) & (vw < e)
    if art == "farbe":
        c = lesen(kette, u, v, lod + np.where(stein, STEIN_MIP, PIXEL_MIP))
        if stein.any():
            c[stein] = c[stein] * (1 - STEIN_RUHE) + lesen(kette, u[stein], v[stein], lod[stein] + PIXEL_MIP) * STEIN_RUHE
    else:
        c = lesen(kette, u, v, lod + PIXEL_MIP)
    if not rein:
        return c
    dach = ((0.0, stroh_teil), (stroh_teil, 0.1875), DACH[1]) if stroh_teil > 0 else DACH   # STROH_TEIL (Efeu und Kupfer, Halbling S3)
    for a, e in list(dach) + [b for b in st_b if b[0] >= 0]:
        i = (vw >= a) & (vw < e)
        if not i.any(): continue
        ist_stein = stein[i]
        if art == "farbe":
            g = np.where(ist_stein, STEIN_MIP, PIXEL_MIP)
            x = band_lesen(kette, u[i], v[i], a, e, lod[i] + g)
            if ist_stein.any():
                x[ist_stein] = x[ist_stein] * (1 - STEIN_RUHE) + band_lesen(kette, u[i][ist_stein], v[i][ist_stein], a, e,
                                                                             lod[i][ist_stein] + PIXEL_MIP) * STEIN_RUHE
        else:
            x = band_lesen(kette, u[i], v[i], a, e, lod[i] + PIXEL_MIP)
        c[i] = x
    return c


def band_von(v):
    for name, (v0, v1) in BAENDER.items():
        if v0 <= v < v1: return name
    return "eisen"


def flaechen_proben(netze, nur_netz=None):
    """Je Dreieck die UV-Punkte (Ecken nach innen geschoben und Schwerpunkt), Band des Schwerpunkts, Ort, Flaeche."""
    proben = []
    for name, stoff, uv, idx, pos in netze:
        if nur_netz and not name.startswith(nur_netz): continue
        for t in idx:
            q = uv[t]
            m = q.mean(axis=0)
            pk = np.vstack([q * 0.6 + m * 0.4, m[None]])
            flaeche = 0.5 * np.linalg.norm(np.cross(pos[t[1]] - pos[t[0]], pos[t[2]] - pos[t[0]]))
            proben.append((name, stoff, band_von(m[1] % 1.0 if m[1] != 1.0 else 0.999), pk, pos[t].mean(axis=0), flaeche))
    return proben


def _mittel(proben, cols):
    g = {}
    for p, c in zip(proben, cols):
        s = g.setdefault((p[1], p[2]), [np.zeros(3), 0.0])
        s[0] += c * p[5]; s[1] += p[5]
    return {k: (s[0] / max(s[1], 1e-9), s[1]) for k, s in g.items()}


def pruefe(glb, albedo, stein_baender=(0.85, 0.95), rein=True, ausgabe_alles=False, stroh_teil=0.0):
    """Zeilen und Fehlerzahl der Pruefung 37 fuer ein Haus (alle Netze)."""
    orm_pfad = albedo.replace("_albedo.png", "_orm.png")
    k_alb, k_orm = mip_kette(albedo), mip_kette(orm_pfad)
    proben = flaechen_proben(glb_lesen(glb))
    pk = np.vstack([p[3] for p in proben]); n = pk.shape[1] if pk.ndim == 3 else len(proben[0][3])
    u, v = pk[:, 0], pk[:, 1]
    zeilen, fehler = [], 0

    def je_flaeche(c):
        return c.reshape(len(proben), -1, 3).mean(axis=1)

    aus_f = _mittel(proben, je_flaeche(lesen(k_alb, u, v, GRUND["Pixel 2"][0] - 1.0)))
    aus_o = _mittel(proben, je_flaeche(lesen(k_orm, u, v, GRUND["Pixel 2"][0] - 1.0)))
    deck = ("stroh_franse",)
    for pix, stufen in GRUND.items():
        for zoom, lod in zip((25, 50, 100), stufen):
            an_f = _mittel(proben, je_flaeche(shader_lesen(k_alb, u, v, lod, stein_baender, rein, stroh_teil=stroh_teil)))
            an_o = _mittel(proben, je_flaeche(shader_lesen(k_orm, u, v, lod, stein_baender, rein, "licht", stroh_teil)))
            for key in sorted(an_f, key=lambda k: -an_f[k][1]):
                c, fl = an_f[key]
                if fl < 0.3: continue
                c0 = aus_f[key][0]; r0, r1 = aus_o[key][0][1], an_o[key][0][1]
                zeile = (f"   {pix} Zoom {zoom}: {key[0]} / {key[1]} ({fl:.1f} m²) Farbe aus {c0[0]:.2f} {c0[1]:.2f} {c0[2]:.2f} "
                         f"an {c[0]:.2f} {c[1]:.2f} {c[2]:.2f}, Rauheit {r0:.2f} -> {r1:.2f}")
                f = []
                if key[1] in deck:
                    if not (c[2] > c[0] * 1.05 and c[2] > c[1]): f.append("(a) Deckung nicht blau")
                    if r1 < r0 - 0.05: f.append("(b) Deckung glaenzt")
                if key[0] == "Efeublatt" and fl >= 0.3:          # (d) Efeu im Pixel-Look klar gruen, nicht fast schwarz (Nutzer 19.11.2026)
                    if not (c[1] > c[0] * 1.15 and c[1] > c[2] * 1.15 and float(c @ LUM) >= 0.28): f.append("(d) Efeu nicht klar gruen")
                if f or ausgabe_alles:
                    zeilen.append(zeile + (" -> FEHLER " + ", ".join(f) if f else ""))
                fehler += len(f)
            # (c) Steinbaender: Profil quer zum Band ueber den v-Bereich der Flaechen
            sb = list(stein_baender) + [-1.0, -1.0]
            for a, e in ((sb[0], sb[1]), (sb[2], sb[3])):
                if a < 0: continue
                vs = np.array([p[3][:, 1] % 1.0 for p in proben if a <= p[3][-1, 1] % 1.0 < e and p[0] != "Fahne"]).ravel()
                if vs.size < 10: continue
                lo, hi = np.percentile(vs, 2), np.percentile(vs, 98)
                vv = np.repeat(np.linspace(lo, hi, 12), 64); uu = np.tile(np.linspace(0, 1, 64, endpoint=False), 12)
                prof = (shader_lesen(k_alb, uu, vv, lod, stein_baender, rein).reshape(12, 64, 3).mean(axis=1)) @ LUM
                prof0 = (lesen(k_alb, uu, vv, stufen[0] - 1.0).reshape(12, 64, 3).mean(axis=1)) @ LUM
                metall = shader_lesen(k_orm, uu, vv, lod, stein_baender, rein, "licht")[:, 2].max()
                q, q0 = prof.max() / max(prof.min(), 1e-4), prof0.max() / max(prof0.min(), 1e-4)
                f = []
                if q > 1.15: f.append(f"(c) Streifen quer zum Band (Faktor {q:.2f})")
                if metall > 0.05: f.append(f"(c) Metall {metall:.2f}")
                zeilen.append(f"   {pix} Zoom {zoom}: Steinband {a:.2f}-{e:.2f} (v {lo:.3f}..{hi:.3f}) Helligkeit quer {prof.min():.2f}..{prof.max():.2f} "
                              f"(Faktor {q:.2f}, ohne Pixel-Look {q0:.2f}), Metall bis {metall:.2f}" + (" -> FEHLER " + ", ".join(f) if f else " -> ok"))
                fehler += len(f)
    return zeilen, fehler


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in sys.argv[1:] if a.startswith("--"))
    sb = tuple(float(x) for x in opt.get("stein", "0.85,0.95").split(","))
    z, f = pruefe(args[0], args[1], sb, "alter-shader" not in opt, "alles" in opt)
    print(f"PIXELMIP {os.path.basename(args[0])} ({'Shader vor BAND_REIN' if 'alter-shader' in opt else 'Shader mit BAND_REIN'})")
    for zz in z: print("PIXELMIP " + zz)
    print(f"PIXELMIP Ergebnis: {f} Fehler")
    return 1 if f else 0


if __name__ == "__main__":
    sys.exit(main())
