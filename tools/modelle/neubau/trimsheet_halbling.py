"""Trim-Sheets fuer die Wohnhaeuser der Halblinge, Stufe 1 (Neubau): dieselben Baender wie bei den Menschen (trimsheet.py), anderer Ton.

    python tools/modelle/neubau/trimsheet_halbling.py [--steine=<ordner>] [--nur=s1|s23]      (numpy und Pillow, kein Blender)

Vorbild sind die Kunstwerke „Wohnhaus Halbling, Stufe 1“ aussen und innen (wohnhaus_lvl1_halbling.jpg, wohnhaus_in_lvl1_halbling.jpg):
ein Fachwerkhaus wie das der Menschen mit dickem, goldgelbem Strohdach, warm-braunem Eichenfachwerk und ockergelbem Lehmputz; innen
warmer Lehmputz, Dielenboden, Herd und Schlot aus Stein. Wie bei den Stufen 2 und 3 der Menschen (trimsheet_stufen.py) liest dieses
Skript die fertigen Blaetter der Menschen (trim_*, innen_trim_*) und aendert nur den Ton der Baender (Zeichnung, Alpha der Strohfransen
und Normalen bleiben). Das Steinband ist K2 `large_sandstone_blocks_01` (Poly Haven, CC0; Flaechen der Halblinge, wie in der
Steinebene, trimsheet_ebenen.stein_band; Herkunft in game/assets/texturen/neubau/HERKUNFT.md). Das Kunstwerk zeigt Sockel, Schlot und
Herd aus rotem Backstein; K2/K5 sind kein Backstein (Designfrage im Bericht). Die Farben sind Proben aus den Kunstwerken (sRGB/255 im
Kopf der Konstanten), etwas unter den Proben, weil die Kunstwerke hell ausgeleuchtet sind. Keine Bild-KI.
Ergebnis: trim_halbling_{albedo,normal,orm}.png und innen_trim_halbling_{albedo,normal,orm}.png (2048 x 2048). Im Spiel liegt
trim_halbling als Ebene „halbling_s1“ im Haeuser-Array (trimsheet_ebenen.py, Bruchsteinband dort K2), innen_trim_halbling im Innen-Array.
"""
import os
import sys

import numpy as np
from PIL import Image

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import trimsheet_ebenen as te                                      # noqa: E402  (Layout lesen, Stein laden, Steinband)

NEUBAU = te.NEUBAU
N = te.N
STEINE = r"C:\Reichsgilde_Austausch\texturen_stein\dl"           # Abruf der Steinebenen (15.11.2026), nicht im Repository

# Farben aus dem Kunstwerk aussen (Proben 10 x 10 Bildpunkte, sRGB/255): Stroh 158/130/90 bis 185/152/101, Putz 182/148/104 bis
# 189/157/110, Balken 118/92/64, Tuer 121/89/62. Die Werte unten stehen im Mittel des Bands in der gespeicherten PNG (sRGB 0..1).
DACH = (0.60, 0.49, 0.32)
PUTZ = (0.68, 0.56, 0.39)
BALKEN = ((0.43, 0.33, 0.23), (0.41, 0.31, 0.22), (0.35, 0.27, 0.19))   # balken_a, _b, _c (Schwellen, Sparrenkoepfe dunkler)
PLANKEN = (0.44, 0.33, 0.23)
# Innen (Kunstwerk innen: Wand 134/98/61, Dielen 193/134/81 im Licht bis 114/93/71, Balken 105/84/69 bis 131/98/65)
INNEN_PUTZ = (0.62, 0.50, 0.35)
INNEN_DIELEN = (0.52, 0.38, 0.25)
INNEN_BALKEN = (0.42, 0.32, 0.23)


def lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


class Blatt:
    """Ein fertiges Blatt (albedo RGBA, normal, orm) als float32 0..1, Zeile 0 oben (wie die PNG)."""

    def __init__(self, name):
        self.albedo = te.bild(os.path.join(NEUBAU, f"{name}_albedo.png"), 4)
        self.normal = te.bild(os.path.join(NEUBAU, f"{name}_normal.png"), 3)
        self.orm = te.bild(os.path.join(NEUBAU, f"{name}_orm.png"), 3)

    @staticmethod
    def zeilen(band):
        return int(round(band[0] * N)), int(round(band[1] * N))

    def speichere(self, name):
        for art, feld, modus in (("albedo", self.albedo, "RGBA"), ("normal", self.normal, "RGB"), ("orm", self.orm, "RGB")):
            pfad = os.path.join(NEUBAU, f"{name}_{art}.png")
            Image.fromarray(np.round(np.clip(feld, 0, 1) * 255).astype(np.uint8), modus).save(pfad, optimize=True)
            print("geschrieben", pfad)


def umtoenen(blatt, band, ziel, sattheit=1.0):
    """Toent ein Band linear auf die mittlere Farbe `ziel` (sRGB; Zeichnung, Helligkeitsverlauf und Alpha bleiben; gemittelt wird nur
    ueber deckende Bildpunkte). `sattheit` < 1 nimmt die eigene Buntheit vorher zurueck (wie trimsheet_elb.entsaettigen)."""
    a, e = blatt.zeilen(band)
    l = lin(blatt.albedo[a:e, :, :3])
    if sattheit < 1.0:
        hell = l @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
        l = l * sattheit + hell[..., None] * (1 - sattheit)
    deckt = blatt.albedo[a:e, :, 3] > 0.5
    mittel = l[deckt].mean(axis=0)
    blatt.albedo[a:e, :, :3] = srgb(np.clip(l * (lin(np.array(ziel, dtype=np.float32)) / np.maximum(mittel, 1e-4)), 0, 1))


def steinband(blatt, band, meter, steine, stein="k2"):
    """Ein Stein der Steinebenen (Standard K2) in eigener Farbe ins Band, Massstab und Fugen wie dort (trimsheet_ebenen.stein_band)."""
    a, e = blatt.zeilen(band)
    f, n, orm, text = te.stein_band(te.stein_laden(steine, stein), e - a, meter)
    blatt.albedo[a:e, :, :3] = f; blatt.albedo[a:e, :, 3] = 1.0
    blatt.normal[a:e] = n
    blatt.orm[a:e] = orm
    print("Steinband", text)


def baue_aussen(steine):
    B = te.layout_lesen(os.path.join(HIER, "trimsheet.py"), "BAENDER_AUSSEN")
    blatt = Blatt("trim")
    umtoenen(blatt, B["stroh"], DACH, sattheit=0.8)
    umtoenen(blatt, B["stroh_franse"], tuple(c * 0.90 for c in DACH), sattheit=0.8)
    for name, ton in zip(("balken_a", "balken_b", "balken_c"), BALKEN):
        umtoenen(blatt, B[name], ton)
    umtoenen(blatt, B["putz"], PUTZ)
    umtoenen(blatt, B["planken"], PLANKEN)
    steinband(blatt, B["bruchstein"], B["bruchstein"][2], steine)        # Sockel und Schlot (im Spiel ersetzt die Ebene das Band durch K2)
    blatt.speichere("trim_halbling")


def baue_innen(steine):
    B = te.layout_lesen(os.path.join(HIER, "trimsheet.py"), "BAENDER_INNEN")
    blatt = Blatt("innen_trim")
    umtoenen(blatt, B["bohlen"], INNEN_DIELEN)
    umtoenen(blatt, B["balken"], INNEN_BALKEN)
    umtoenen(blatt, B["lehmwand"], INNEN_PUTZ)
    # Dielenboden statt Lehm (Kunstwerk innen): das Bohlenband im Bodenstreifen, gespiegelt (andere Bretter) und etwas dunkler
    a0, e0 = blatt.zeilen(B["bohlen"]); a1, e1 = blatt.zeilen(B["lehmboden"])
    zeilen = np.arange(e1 - a1) % (e0 - a0)
    for feld in (blatt.albedo, blatt.normal, blatt.orm):
        feld[a1:e1] = feld[a0:e0][zeilen][:, ::-1]
    blatt.normal[a1:e1, :, 0] = 1.0 - blatt.normal[a1:e1, :, 0]       # waagerecht gespiegelt: x der Normale kehrt sich um
    umtoenen(blatt, B["lehmboden"], tuple(c * 0.92 for c in INNEN_DIELEN))
    steinband(blatt, B["bruchstein"], B["bruchstein"][2], steine)        # Herd und Schlot aus Stein
    blatt.speichere("innen_trim_halbling")


# ── Stufe 2 (Buergerhaus) und Stufe 3 (Steinhaus) der Halblinge nach den Vorlagen (Nutzer 18.11.2026; Kunstwerke wohnhaus_lvl2_halbling,
#    wohnhaus_lvl3_halbling, innen wohnhaus_in_lvl2/3_halbling): roter Backstein (K6, Poly Haven medieval_red_brick, CC0) statt Naturstein,
#    blaues Schieferdach statt Stroh, im Steinhaus Gauben mit Kupferdach (Gruenspan, selbst gemalt).
#    trim_s2_halbling:           trim_halbling (Fachwerk, Lehmputz, Holz der Halblinge), Dachbaender = Schiefer der Menschen (trim_s3) blau
#                                getoent, Band „bruchstein“ K6 (Erdgeschoss)
#    trim_s3_halbling:           trim_s3 der Menschen (Werkstein im Band „putz“), Holz der Halblinge, Band „stroh_franse“ Schiefer blau
#                                (Hauptdach), Band „stroh“ Kupfer mit Gruenspan (Gaubendaecher, First), Band „bruchstein“ K6 (Mauer)
#    innen_trim_s23_halbling:    innen_trim_s23 der Menschen (Tonfliesen und Schachbrett im Bodenband, Kunstwerke innen: Fliesen vor dem
#                                Herd) mit hellem, warmem Putz, Dielen und Balken der Halblinge, Herd und Schlot K2
#    raeume_innen_trim_halbling: raeume_innen_trim der Menschen, Putz, Dielen, Balken und Moebelholz der Halblinge, Bruchstein K2
#    python tools/modelle/neubau/trimsheet_halbling.py --nur=s23 [--steine=<ordner>] [--backstein=<ordner>]
INNEN_PUTZ_S23 = (0.72, 0.62, 0.46)          # Kunstwerke innen Stufe 2/3: cremefarbener Putz (Proben 214/190/150 bis 226/204/164, hell ausgeleuchtet)
SCHIEFER_BLAU = (0.80, 0.92, 1.22)           # Toenung des Schiefers der Menschen ins Blaue (Vorlagen: blaugraues Dach), Helligkeit bleibt
KUPFER = dict(gruen=(0.36, 0.58, 0.54), braun=(0.42, 0.30, 0.22), falz_m=0.40)   # Gruenspan, durchscheinendes Kupfer, Abstand der Falze
BACKSTEIN = r"C:\rg-werkzeuge\backstein_dl"   # Abruf K6 (18.11.2026), nicht im Repository


def blau_toenen(blatt, band):
    """Toent ein Dachband ins Blaue (mittlere Helligkeit bleibt)."""
    a, e = blatt.zeilen(band)
    l = lin(blatt.albedo[a:e, :, :3])
    hell = l @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    f = np.array(SCHIEFER_BLAU, dtype=np.float32)
    f = f / (f @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32))
    neu = hell[..., None] * 0.35 * f + l * 0.65 * f
    blatt.albedo[a:e, :, :3] = srgb(np.clip(neu, 0, 1))


def kupfer(blatt, band, saat=17):
    """Kupferblech mit Gruenspan, selbst gemalt (numpy, keine Fremdtextur): Stehfalze in UV.x alle KUPFER["falz_m"] Meter (die Dachskripte
    legen UV.x entlang der Traufe), Gruenspan wolkig mit durchscheinendem braunem Kupfer, Laufspuren entlang des Gefaelles; Normale mit den
    Falzen als Grate, ORM matt (Rauheit 0,6, kaum Metall)."""
    a, e = blatt.zeilen(band)
    h, w = e - a, N
    meter = band[2]
    rng = np.random.default_rng(saat)

    def wolke(gross):
        f = np.zeros((h, w), np.float32)
        for s, g in ((gross, 1.0), (gross // 2, 0.5), (gross // 4, 0.25)):
            k = rng.random((h // s + 2, w // s + 2)).astype(np.float32)
            gy, gx = np.meshgrid(np.arange(h) / s, np.arange(w) / s, indexing="ij")
            y0, x0 = gy.astype(int), gx.astype(int); fy, fx = gy - y0, gx - x0
            x1 = (x0 + 1) % (w // s + 2)
            v = (k[y0, x0] * (1 - fx) + k[y0, x1] * fx) * (1 - fy) + (k[y0 + 1, x0] * (1 - fx) + k[y0 + 1, x1] * fx) * fy
            f += g * v
        return f / 1.75
    wl = wolke(64)
    lauf = np.repeat(wolke(16).mean(axis=0, keepdims=True), h, axis=0) * 0.5 + wolke(8) * 0.5     # Laufspuren entlang des Gefaelles
    gruen = np.array(KUPFER["gruen"], np.float32); braun = np.array(KUPFER["braun"], np.float32)
    anteil = np.clip((wl - 0.18) * 3.0, 0, 1)[..., None]          # meist Gruenspan, Kupfer nur in Flecken
    farbe = braun * (1 - anteil) + gruen * anteil
    farbe = farbe * (0.85 + 0.3 * lauf[..., None])
    falz = int(round(KUPFER["falz_m"] / meter * w))
    x = np.arange(w) % falz
    abst = np.minimum(x, falz - x).astype(np.float32)
    grat = np.clip(1 - abst / 6.0, 0, 1)
    farbe = farbe * (1 - 0.25 * grat[None, :, None]) + 0.08 * grat[None, :, None]
    blatt.albedo[a:e, :, :3] = np.clip(farbe, 0, 1); blatt.albedo[a:e, :, 3] = 1.0
    nx = np.zeros(w, np.float32)
    nx[x < 6] = 0.35; nx[falz - x <= 6] = -0.35
    n = np.stack([np.broadcast_to(0.5 + nx[None, :] * 0.5, (h, w)), np.full((h, w), 0.5, np.float32), np.full((h, w), 0.95, np.float32)], axis=-1)
    blatt.normal[a:e] = n
    blatt.orm[a:e] = np.stack([1.0 - 0.15 * (1 - lauf), np.full((h, w), 0.6, np.float32), np.full((h, w), 0.05, np.float32)], axis=-1)
    print(f"Kupfer: Falz alle {falz} Bildpunkte, Gruenspan {float(anteil.mean()):.2f}")


# Stufe 3 belebt (Nutzer 19.11.2026): eine Hausecke dicht mit Efeu bewachsen. Das Band „stroh“ des Blatts trim_s3_halbling teilt sich:
# oben (v 0 bis 0,125) der Efeu, darunter (0,125 bis 0,1875) das Kupfer der Gauben (wohnhaus_halbling_s23 legt die Kupferflaechen dorthin).
# Die Efeuflaechen am Haus liegen gedreht im Band: UV.x laeuft die Wand HINAUF (4 m je Einheit), UV.y quer von der Hausecke weg.
EFEU_STUFE3 = (0.0, 0.125, 4.0)
KUPFER_STUFE3 = (0.125, 0.1875, 4.0)
EFEU_FARBE = (0.20, 0.30, 0.13)            # wie elb_bewuchs.EFEU (Kunstwerk der Elben), sRGB


def efeu_dicht(blatt, band, saat=73):
    """Dichter Efeu, selbst gemalt (numpy, keine Fremdtextur): dunkler Grund aus Laub im Schatten, darueber viele Blaetter in drei
    Helligkeiten, zur Hausecke (Zeile 0) dichter; senkrecht (Spalten = Hoehe, 2 mm je Bildpunkt) nahtlos. Die Zeilen stehen fuer bis zu
    1,3 m Breite (etwa 5 mm je Bildpunkt): die Blaetter sind darum in den Zeilen schmaler gemalt, an der Wand wirken sie rund (etwa 6 cm)."""
    a, e = blatt.zeilen(band)
    h, w = e - a, N
    rng = np.random.default_rng(saat)
    grund = np.array(EFEU_FARBE, np.float32) * 0.42
    farbe = np.broadcast_to(grund, (h, w, 3)).copy()
    hoehe = np.zeros((h, w), np.float32)
    zeile = np.arange(h, dtype=np.float32)[:, None]
    for _ in range(5200):
        bx, by = rng.uniform(0, w), rng.uniform(0, h) ** 1.15 / h ** 0.15         # zur Ecke hin dichter
        rx, ry = rng.uniform(11, 19), rng.uniform(5, 9)
        x0, x1 = int(bx - rx - 1), int(bx + rx + 2)
        y0, y1 = max(int(by - ry - 1), 0), min(int(by + ry + 2), h)
        if y0 >= y1: continue
        xs = np.arange(x0, x1)
        d = ((xs[None, :] - bx) / rx) ** 2 + ((zeile[y0:y1] - by) / ry) ** 2
        drin = d < 1.0
        hell = rng.uniform(0.75, 1.30) * (1.0 - 0.25 * by / h)
        ton = np.array(EFEU_FARBE, np.float32) * hell * np.array([rng.uniform(0.9, 1.1), 1.0, rng.uniform(0.85, 1.1)], np.float32)
        spalten = xs % w
        f = farbe[y0:y1][:, spalten]
        f[drin] = ton * (0.85 + 0.15 * (1 - d[drin]))[:, None]
        farbe[y0:y1, spalten] = f
        hh = hoehe[y0:y1][:, spalten]
        hh[drin] = np.maximum(hh[drin], 1.0 - d[drin])
        hoehe[y0:y1, spalten] = hh
    blatt.albedo[a:e, :, :3] = np.clip(farbe, 0, 1); blatt.albedo[a:e, :, 3] = 1.0
    gy, gx = np.gradient(hoehe * 2.0)
    blatt.normal[a:e] = np.stack([np.clip(0.5 - gx * 0.5, 0, 1), np.clip(0.5 + gy * 0.5, 0, 1), np.full((h, w), 0.95, np.float32)], axis=-1)
    blatt.orm[a:e] = np.stack([0.55 + 0.45 * hoehe, np.full((h, w), 0.75, np.float32), np.zeros((h, w), np.float32)], axis=-1)
    print(f"Efeu: Blaetter auf {float((hoehe > 0).mean()):.2f} der Flaeche")


def baue_s23(steine, backstein=BACKSTEIN):
    B = te.layout_lesen(os.path.join(HIER, "trimsheet.py"), "BAENDER_AUSSEN")
    alle = steine + ";" + backstein
    # Stufe 2: Blatt der Stufe 1, Schiefer blau, Backstein
    blatt = Blatt("trim_halbling")
    schiefer = Blatt("trim_s3")
    for band in ("stroh", "stroh_franse"):
        a, e = blatt.zeilen(B[band])
        blatt.albedo[a:e], blatt.normal[a:e], blatt.orm[a:e] = schiefer.albedo[a:e], schiefer.normal[a:e], schiefer.orm[a:e]
        blau_toenen(blatt, B[band])
    steinband(blatt, B["bruchstein"], B["bruchstein"][2], alle, "k6")
    blatt.speichere("trim_s2_halbling")
    # Stufe 3: Steinhaus der Menschen, Schiefer blau, Kupfer, Backstein
    blatt = Blatt("trim_s3")
    blau_toenen(blatt, B["stroh_franse"])
    kupfer(blatt, KUPFER_STUFE3)                             # Band „stroh“: unten Kupfer, oben Efeu (Stufe 3 belebt, 19.11.2026)
    efeu_dicht(blatt, EFEU_STUFE3)
    for name, ton in zip(("balken_a", "balken_b", "balken_c"), BALKEN):
        umtoenen(blatt, B[name], ton)
    umtoenen(blatt, B["planken"], PLANKEN)
    steinband(blatt, B["bruchstein"], B["bruchstein"][2], alle, "k6")
    blatt.speichere("trim_s3_halbling")
    I = te.layout_lesen(os.path.join(HIER, "trimsheet.py"), "BAENDER_INNEN")
    i = Blatt("innen_trim_s23")
    umtoenen(i, I["bohlen"], INNEN_DIELEN)
    umtoenen(i, I["balken"], INNEN_BALKEN)
    umtoenen(i, I["lehmwand"], INNEN_PUTZ_S23)
    steinband(i, I["bruchstein"], I["bruchstein"][2], steine)
    i.speichere("innen_trim_s23_halbling")
    R = te.layout_lesen(os.path.join(os.path.dirname(HIER), "raeume", "raeume_trimsheet.py"), "BAENDER_RAEUME")
    r = Blatt("raeume_innen_trim")
    umtoenen(r, R["putz"], INNEN_PUTZ_S23)
    umtoenen(r, R["dielen"], INNEN_DIELEN)
    umtoenen(r, R["balken"], INNEN_BALKEN)
    umtoenen(r, R["moebel"], tuple(c * 0.95 for c in INNEN_BALKEN))
    steinband(r, R["bruchstein"], R["bruchstein"][2], steine)
    r.speichere("raeume_innen_trim_halbling")


if __name__ == "__main__":
    opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    steine = opt.get("steine", STEINE)
    nur = opt.get("nur", "")
    if nur in ("", "s1"):
        baue_aussen(steine)
        baue_innen(steine)
    if nur in ("", "s23"):
        baue_s23(steine, opt.get("backstein", BACKSTEIN))
