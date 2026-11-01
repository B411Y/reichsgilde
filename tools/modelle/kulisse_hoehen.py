"""Höhenprofil der Kulissenberge um Köln aus dem DGM NRW (ohne Blender, nur Python-Standardbibliothek).

    python3 -I tools/modelle/kulisse_hoehen.py <ordner-mit-dgm-kacheln> [<ordner-mit-gipfelkacheln>]

Liest die GeoTIFF-Kacheln (Float32, LZW, EPSG:25832) eines Ordners, wie sie der WCS-Dienst von Geobasis NRW liefert
(Abruf siehe game/assets/modelle/kulisse/HERKUNFT.md): im ersten Ordner das Umland in 100-m-Pixeln, im zweiten
(wahlweise) je Gipfel eine feine Kachel (5 m), aus der die Gipfelhöhe kommt – 100-m-Pixel mitteln Gipfel um 40–80 m ab.
Es schreibt:

  * tools/modelle/kulisse_koeln_profil.json – je Staffel und halbem Grad Peilung die höchste echte Höhe (m über NN)
    im Entfernungsband der Staffel und die Entfernung, in der sie liegt. Daraus baut kulisse_koeln.py das Modell.
  * die Gipfel und Gebiete mit Lage und Höhe (aus dem DGM nachgeschlagen) in dieselbe Datei; kulisse_koeln.py ergänzt
    ihre Lage im Spiel und schreibt game/daten/berge.json.

Die Höhendaten selbst kommen nicht ins Repository; das Profil ist daraus abgeleitet (Datenlizenz Deutschland – Zero 2.0,
jede Nutzung ohne Bedingungen erlaubt).

Peilung: Grad im Uhrzeigersinn ab Norden. Die Kölner Karte ist genordet (x = Osten, z = Süden), deshalb gilt die Peilung
unverändert im Spiel.
"""

import json
import math
import os
import struct
import sys

WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PROFIL = os.path.join(WURZEL, "tools", "modelle", "kulisse_koeln_profil.json")

# Mitte der Kölner Karte (Spiel −50 | 25, also 50 m westlich und 25 m südlich vom Heumarkt) in Breite/Länge (WGS84/ETRS89)
HEUMARKT = (50.93590, 6.96010)
KARTENMITTE_SPIEL = (-50.0, 25.0)

# Staffeln: Entfernungsband in km (echt). Nah: Ville, vorderes Bergisches Land; Mitte: Bergisches Land, Vorgebirge;
# Fern: Siebengebirge, Eifelrand.
STAFFELN = (("nah", 6.0, 16.0), ("mitte", 16.0, 27.0), ("fern", 27.0, 49.0))
SCHRITT_GRAD = 0.5

# Braunkohle: Tagebaue und Halden gab es um 1400 nicht. Dort zählen die Höhen nicht (Kreis: Breite, Länge, Radius km).
BRAUNKOHLE = (
    ("Tagebau Hambach und Sophienhöhe", 50.912, 6.460, 6.5),
    ("Tagebau Garzweiler", 51.060, 6.500, 6.0),
    ("Tagebau Inden", 50.850, 6.360, 5.0),
    ("Glessener Höhe", 50.958, 6.735, 2.0),
    ("Vollrather Höhe", 51.045, 6.580, 2.5),
    ("Tagebau Fortuna", 50.980, 6.620, 3.5),
    ("Tagebau Frechen", 50.900, 6.790, 2.5),
)

# Gipfel mit Namen: ungefähre Lage; die genaue Lage ist der höchste DGM-Punkt im Umkreis (km), die Höhe kommt aus dem DGM.
GIPFEL = (
    ("Großer Ölberg", "Siebengebirge", 50.6822, 7.2481, 0.3),
    ("Löwenburg", "Siebengebirge", 50.6640, 7.2503, 0.3),
    ("Petersberg", "Siebengebirge", 50.6862, 7.2095, 0.3),
    ("Drachenfels", "Siebengebirge", 50.6656, 7.2106, 0.25),
    ("Michelsberg", "Eifel", 50.5126, 6.8243, 0.5),
)
# Gebiete: Name, Peilungsbereich (Grad) und Staffeln; die Beschriftung sitzt am höchsten Punkt des Profils darin.
GEBIETE = (
    ("Bergisches Land", 30.0, 120.0, ("nah", "mitte")),
    ("Siebengebirge", 135.0, 160.0, ("fern",)),
    ("Eifel", 185.0, 240.0, ("fern",)),
    ("Ville", 200.0, 290.0, ("nah",)),
)
TIEFSTE_NN = 30.0      # natürlich liegt um Köln nichts tiefer; tiefer sind Tagebaue (auch außerhalb der Kreise oben)


# ── Koordinaten: UTM Zone 32 (ETRS89/GRS80, EPSG:25832) ─────────────────────────────────────────────────────────────
_A, _F = 6378137.0, 1 / 298.257222101
_K0, _E0, _L0 = 0.9996, 500000.0, math.radians(9.0)


def _konstanten():
    n = _F / (2 - _F)
    a_ = _A / (1 + n) * (1 + n * n / 4 + n ** 4 / 64)
    al = (n / 2 - 2 * n * n / 3 + 5 * n ** 3 / 16, 13 * n * n / 48 - 3 * n ** 3 / 5, 61 * n ** 3 / 240)
    be = (n / 2 - 2 * n * n / 3 + 37 * n ** 3 / 96, n * n / 48 + n ** 3 / 15, 17 * n ** 3 / 480)
    de = (2 * n - 2 * n * n / 3 - 2 * n ** 3, 7 * n * n / 3 - 8 * n ** 3 / 5, 56 * n ** 3 / 15)
    return n, a_, al, be, de


def utm(breite, laenge):
    """Breite/Länge (Grad) → (Ost, Nord) in m, Zone 32 (Krüger-Reihe, genau auf Millimeter)."""
    n, a_, al, _, _ = _konstanten()
    phi, lam = math.radians(breite), math.radians(laenge) - _L0
    t = math.sinh(math.atanh(math.sin(phi)) - 2 * math.sqrt(n) / (1 + n) * math.atanh(2 * math.sqrt(n) / (1 + n) * math.sin(phi)))
    xi, eta = math.atan(t / math.cos(lam)), math.atanh(math.sin(lam) / math.sqrt(1 + t * t))
    e = eta + sum(al[j] * math.cos(2 * (j + 1) * xi) * math.sinh(2 * (j + 1) * eta) for j in range(3))
    nn = xi + sum(al[j] * math.sin(2 * (j + 1) * xi) * math.cosh(2 * (j + 1) * eta) for j in range(3))
    return _E0 + _K0 * a_ * e, _K0 * a_ * nn


def geo(ost, nord):
    """(Ost, Nord) Zone 32 → Breite/Länge (Grad)."""
    n, a_, _, be, de = _konstanten()
    xi, eta = nord / (_K0 * a_), (ost - _E0) / (_K0 * a_)
    xi_ = xi - sum(be[j] * math.sin(2 * (j + 1) * xi) * math.cosh(2 * (j + 1) * eta) for j in range(3))
    eta_ = eta - sum(be[j] * math.cos(2 * (j + 1) * xi) * math.sinh(2 * (j + 1) * eta) for j in range(3))
    chi = math.asin(math.sin(xi_) / math.cosh(eta_))
    phi = chi + sum(de[j] * math.sin(2 * (j + 1) * chi) for j in range(3))
    lam = _L0 + math.atan(math.sinh(eta_) / math.cos(xi_))
    return math.degrees(phi), math.degrees(lam)


# ── GeoTIFF lesen (Float32, eine Ebene, unkomprimiert oder LZW) ──────────────────────────────────────────────────────
def _lzw(daten):
    """LZW-Entpacker nach TIFF 6.0 (MSB zuerst, frühes Umschalten der Codelänge)."""
    aus = bytearray()
    tabelle = [bytes([i]) for i in range(256)] + [b"", b""]
    bits, puffer, laenge, vorher = 9, 0, 0, None
    for byte in daten:
        puffer = (puffer << 8) | byte
        laenge += 8
        while laenge >= bits:
            laenge -= bits
            code = (puffer >> laenge) & ((1 << bits) - 1)
            if code == 256:
                tabelle = tabelle[:258]
                bits, vorher = 9, None
                continue
            if code == 257:
                return bytes(aus)
            if vorher is None:
                eintrag = tabelle[code]
            elif code < len(tabelle):
                eintrag = tabelle[code]
                tabelle.append(vorher + eintrag[:1])
            else:
                eintrag = vorher + vorher[:1]
                tabelle.append(eintrag)
            aus += eintrag
            vorher = eintrag
            if len(tabelle) + 1 >= (1 << bits) and bits < 12:
                bits += 1
    return bytes(aus)


def tiff_lesen(pfad):
    """→ (breite, hoehe, werte als Liste Float, (ost0, nord0) der linken oberen Ecke, pixelgröße, nodata)."""
    with open(pfad, "rb") as f:
        d = f.read()
    if d[:4] != b"II*\0":
        raise ValueError(f"{pfad}: kein TIFF (Little Endian)")
    groesse = {1: 1, 2: 1, 3: 2, 4: 4, 5: 8, 11: 4, 12: 8, 16: 8}
    form = {1: "B", 2: "c", 3: "H", 4: "I", 5: "II", 11: "f", 12: "d", 16: "Q"}
    off = struct.unpack_from("<I", d, 4)[0]
    tags = {}
    for i in range(struct.unpack_from("<H", d, off)[0]):
        tag, typ, anz = struct.unpack_from("<HHI", d, off + 2 + 12 * i)
        n = groesse[typ] * anz
        ort = off + 2 + 12 * i + 8 if n <= 4 else struct.unpack_from("<I", d, off + 2 + 12 * i + 8)[0]
        if typ == 2:
            tags[tag] = d[ort:ort + anz].rstrip(b"\0").decode("ascii")
        elif typ == 5:
            tags[tag] = struct.unpack_from("<" + "II" * anz, d, ort)
        else:
            tags[tag] = struct.unpack_from("<" + form[typ] * anz, d, ort)
    breite, hoehe = tags[256][0], tags[257][0]
    if tags[258][0] != 32 or tags.get(339, (1,))[0] != 3 or tags.get(277, (1,))[0] != 1:
        raise ValueError(f"{pfad}: erwartet eine Ebene Float32")
    if tags.get(317, (1,))[0] != 1:
        raise ValueError(f"{pfad}: Prädiktor wird nicht unterstützt")
    komp = tags.get(259, (1,))[0]
    roh = bytearray()
    for start, n in zip(tags[273], tags[279]):
        stueck = d[start:start + n]
        roh += _lzw(stueck) if komp == 5 else stueck if komp == 1 else b""
        if komp not in (1, 5):
            raise ValueError(f"{pfad}: Kompression {komp} wird nicht unterstützt")
    werte = list(struct.unpack_from(f"<{breite * hoehe}f", roh))
    sx = tags[33550][0]
    ost0, nord0 = tags[33922][3], tags[33922][4]
    nodata = float(tags[42113]) if 42113 in tags else None
    return breite, hoehe, werte, (ost0, nord0), sx, nodata


class Gelaende:
    """Alle Kacheln als ein Raster (gleiche Pixelgröße), mit Mittelung 3 × 3 und bilinearem Abtasten."""

    def __init__(self, ordner, dateien=None, mitteln=True):
        dateien = dateien or [n for n in sorted(os.listdir(ordner)) if n.endswith(".tif")]
        kacheln = [tiff_lesen(os.path.join(ordner, n)) for n in dateien]
        if not kacheln:
            raise SystemExit(f"keine .tif-Dateien in {ordner}")
        self.px = kacheln[0][4]
        self.ost0 = min(k[3][0] for k in kacheln)
        self.nord0 = max(k[3][1] for k in kacheln)
        self.b = round((max(k[3][0] + k[0] * self.px for k in kacheln) - self.ost0) / self.px)
        self.h = round((self.nord0 - min(k[3][1] - k[1] * self.px for k in kacheln)) / self.px)
        z = [None] * (self.b * self.h)
        for b, h, werte, (o0, n0), px, nodata in kacheln:
            if abs(px - self.px) > 1e-6:
                raise SystemExit("Kacheln mit verschiedener Pixelgröße")
            i0, j0 = round((o0 - self.ost0) / px), round((self.nord0 - n0) / px)
            for j in range(h):
                for i in range(b):
                    w = werte[j * b + i]
                    if w != w or w < TIEFSTE_NN or w > 5000 or (nodata is not None and w == nodata):
                        continue
                    z[(j0 + j) * self.b + i0 + i] = w
        self.roh = z
        if not mitteln:
            return
        self.maske_braunkohle()
        self.z = self._mitteln(z)
        frei = sum(1 for w in z if w is not None)
        print(f"DGM: {len(kacheln)} Kacheln, {self.b} × {self.h} Pixel zu {self.px:.0f} m, {frei} mit Höhe "
              f"({100 * frei / len(z):.0f} %)")

    def maske_braunkohle(self):
        weg = 0
        for _, br, la, r in BRAUNKOHLE:
            o, n = utm(br, la)
            for j in range(self.h):
                for i in range(self.b):
                    po, pn = self.ost0 + (i + 0.5) * self.px, self.nord0 - (j + 0.5) * self.px
                    if (po - o) ** 2 + (pn - n) ** 2 < (r * 1000) ** 2 and self.roh[j * self.b + i] is not None:
                        self.roh[j * self.b + i] = None
                        weg += 1
        print(f"Braunkohle: {weg} Pixel ohne Wertung")

    def _mitteln(self, z):
        aus = [None] * len(z)
        for j in range(self.h):
            for i in range(self.b):
                s, n = 0.0, 0
                for dj in (-1, 0, 1):
                    for di in (-1, 0, 1):
                        ii, jj = i + di, j + dj
                        if 0 <= ii < self.b and 0 <= jj < self.h:
                            w = z[jj * self.b + ii]
                            if w is not None:
                                s, n = s + w, n + 1
                if n >= 5:
                    aus[j * self.b + i] = s / n
        return aus

    def hoehe(self, ost, nord, roh=False):
        """Bilinear; None außerhalb oder ohne Daten."""
        fx, fy = (ost - self.ost0) / self.px - 0.5, (self.nord0 - nord) / self.px - 0.5
        i, j = math.floor(fx), math.floor(fy)
        if i < 0 or j < 0 or i + 1 >= self.b or j + 1 >= self.h:
            return None
        q = self.roh if roh else self.z
        w = [q[(j + a) * self.b + i + c] for a in (0, 1) for c in (0, 1)]
        if any(v is None for v in w):
            return None
        tx, ty = fx - i, fy - j
        return (w[0] * (1 - tx) + w[1] * tx) * (1 - ty) + (w[2] * (1 - tx) + w[3] * tx) * ty

    def gipfel(self, ost, nord, radius):
        """Höchster Pixel (ungeglättet) im Umkreis → (ost, nord, höhe)."""
        beste = None
        r = int(radius * 1000 / self.px) + 1
        i0, j0 = int((ost - self.ost0) / self.px), int((self.nord0 - nord) / self.px)
        for j in range(j0 - r, j0 + r + 1):
            for i in range(i0 - r, i0 + r + 1):
                if 0 <= i < self.b and 0 <= j < self.h:
                    po, pn = self.ost0 + (i + 0.5) * self.px, self.nord0 - (j + 0.5) * self.px
                    w = self.roh[j * self.b + i]
                    if w is not None and (po - ost) ** 2 + (pn - nord) ** 2 <= (radius * 1000) ** 2:
                        if beste is None or w > beste[2]:
                            beste = (po, pn, w)
        return beste


def peilung_entfernung(mitte, ost, nord):
    de, dn = ost - mitte[0], nord - mitte[1]
    return math.degrees(math.atan2(de, dn)) % 360, math.hypot(de, dn) / 1000


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    gel = Gelaende(sys.argv[1])
    ho, hn = utm(*HEUMARKT)
    mitte = (ho + KARTENMITTE_SPIEL[0], hn - KARTENMITTE_SPIEL[1])
    anzahl = round(360 / SCHRITT_GRAD)
    staffeln = []
    for name, r0, r1 in STAFFELN:
        kamm, ort, leer = [], [], 0
        for k in range(anzahl):
            beste = None
            for teil in (-0.2, 0.0, 0.2):            # drei Strahlen je halbem Grad, damit kein Gipfel durchrutscht
                w = math.radians((k + teil) * SCHRITT_GRAD)
                r = r0
                while r <= r1 + 1e-6:
                    h = gel.hoehe(mitte[0] + math.sin(w) * r * 1000, mitte[1] + math.cos(w) * r * 1000)
                    if h is not None and (beste is None or h > beste[0]):
                        beste = (h, r)
                    r += gel.px / 1000
            if beste is None:
                leer += 1
                kamm.append(None)
                ort.append(None)
            else:
                kamm.append(round(beste[0], 1))
                ort.append(round((beste[1] - r0) / (r1 - r0), 3))
        # Peilungen ganz ohne Daten (außerhalb von NRW): aus den Nachbarn füllen
        for liste in (kamm, ort):
            bekannt = [i for i, v in enumerate(liste) if v is not None]
            for i, v in enumerate(liste):
                if v is None:
                    a = max((b for b in bekannt if b < i), default=bekannt[-1] - anzahl)
                    c = min((b for b in bekannt if b > i), default=bekannt[0] + anzahl)
                    t = (i - a) / (c - a)
                    liste[i] = round(liste[a % anzahl] * (1 - t) + liste[c % anzahl] * t, 3)
        print(f"Staffel {name} ({r0:.0f}–{r1:.0f} km): Kamm {min(kamm):.0f}–{max(kamm):.0f} m ü. NN, "
              f"{leer} Peilungen ohne Daten (aus Nachbarn gefüllt)")
        staffeln.append({"name": name, "von_km": r0, "bis_km": r1, "kamm_nn": kamm, "kamm_lage": ort})

    fein = sys.argv[2] if len(sys.argv) > 2 else None
    gipfel = []
    for name, gebiet, br, la, rad in GIPFEL:
        o, n = utm(br, la)
        datei = name.replace(" ", "_") + ".tif"
        if fein and os.path.exists(os.path.join(fein, datei)):
            g = Gelaende(fein, [datei], mitteln=False).gipfel(o, n, rad)
        else:
            g = gel.gipfel(o, n, rad)
        if g is None:
            print(f"  {name}: keine Daten")
            continue
        pe, en = peilung_entfernung(mitte, g[0], g[1])
        b2, l2 = geo(g[0], g[1])
        gipfel.append({"name": name, "gebiet": gebiet, "breite": round(b2, 4), "laenge": round(l2, 4),
                       "hoehe_nn": round(g[2]), "peilung": round(pe, 1), "entfernung_km": round(en, 1)})
        print(f"  {name}: {g[2]:.0f} m ü. NN, Peilung {pe:.1f}°, {en:.1f} km")
    gebiete = []
    for name, p0, p1, welche in GEBIETE:
        beste = None
        for s in (s for s in staffeln if s["name"] in welche):
            for k in range(anzahl):
                pe = k * SCHRITT_GRAD
                if p0 <= pe <= p1 and (beste is None or s["kamm_nn"][k] > beste[0]):
                    r = s["von_km"] + s["kamm_lage"][k] * (s["bis_km"] - s["von_km"])
                    beste = (s["kamm_nn"][k], pe, r, s["name"])
        h, pe, r, st = beste
        w = math.radians(pe)
        b2, l2 = geo(mitte[0] + math.sin(w) * r * 1000, mitte[1] + math.cos(w) * r * 1000)
        gebiete.append({"name": name, "breite": round(b2, 4), "laenge": round(l2, 4), "hoehe_nn": round(h),
                        "peilung": pe, "entfernung_km": round(r, 1), "staffel": st})
        print(f"  Gebiet {name}: höchster Punkt {h:.0f} m ü. NN bei {pe:.1f}°, {r:.1f} km ({st})")

    with open(PROFIL, "w", encoding="utf-8") as f:
        json.dump({"quelle": "DGM NRW (Geobasis NRW, WCS nw_dgm, 100-m-Abtastung), Datenlizenz Deutschland – Zero 2.0",
                   "mitte_utm32": [round(mitte[0], 1), round(mitte[1], 1)], "schritt_grad": SCHRITT_GRAD,
                   "staffeln": staffeln, "gipfel": gipfel, "gebiete": gebiete},
                  f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")
    print(f"geschrieben: {os.path.relpath(PROFIL, WURZEL)}")


if __name__ == "__main__":
    main()
