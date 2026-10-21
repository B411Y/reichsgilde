"""Kachelbare Strukturen für die Oberflächendetails – alles prozedural erzeugt, nahtlos, 512 × 512 Pixel je Schicht.

    python tools/oberflaeche/texturen.py        (braucht nur numpy; z. B. mit dem Python von Blender)

Schreibt game/assets/texturen/strukturen.png: elf Schichten untereinander (512 × 5632), die Godot als Texture2DArray
lädt (strukturen.png.import: slices/vertical=11). Jede Schicht ist ein Material:
    0 Holz (Balken)  1 Bretter  2 Mauer  3 Fels  4 Ziegel  5 Stroh  6 Schiefer  7 Laub  8 Boden
    9 Pflaster (Mauer von oben gesehen)  10 Flecken (großflächige Unterschiede, für alle Arten)
Kanäle je Pixel, alle um 128 als „nichts“ (der Shader rechnet (Wert − 128) / 127):
    R  Helligkeit   (+ heller, − dunkler; 127 Schritte = ±50 %)
    G  Farbton      (+ wärmer, − kühler)
    B  Neigung der Fläche in U-Richtung (Normal-Map x)
    A  Neigung der Fläche in V-Richtung (Normal-Map y; V zeigt im Bild nach unten)
Bedeutung und Stärke im Spiel: game/assets/shader/oberflaeche.gdshader. Welche Materialart welches Material bekommt:
game/daten/materialarten.json.

Die Strukturen sind bewusst dezent: Die Grundfarbe der Modelle bleibt bestimmend, die Schicht gibt nur Unterschiede
von Stein zu Stein, weiche Fugen, Maserung und eine leichte Unebenheit im Licht.
"""
import os
import struct
import zlib

import numpy as np

N = 512
ZIEL = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "game", "assets", "texturen")
V, U = np.mgrid[0:N, 0:N] / N          # 0..1, U nach rechts, V nach unten – Muster mit ganzen Perioden sind kachelbar


# ═════════════════════════════════════════════════════════════════════════════
#  Bausteine
# ═════════════════════════════════════════════════════════════════════════════

def rauschen(groesse, saat, streckung=1.0):
    """Kachelbares Rauschen (Zufallsspektrum, tiefpassgefiltert), 0..1. `groesse`: Größe der Flecken in Pixeln,
    `streckung` > 1 zieht sie in V-Richtung lang (Halme, Maserung)."""
    r = np.random.default_rng(saat)
    f = np.fft.fftfreq(N)
    fx, fy = np.meshgrid(f, f)
    filt = np.exp(-((fx * groesse) ** 2 + (fy * groesse * streckung) ** 2) * 4.0)
    z = np.fft.ifft2(np.fft.fft2(r.standard_normal((N, N))) * filt).real
    return (z - z.min()) / (z.max() - z.min())


def fbm(saat, groessen, gewichte=None):
    """Mehrere Rauschen übereinander (grob bis fein), 0..1."""
    gewichte = gewichte or [0.5 ** i for i in range(len(groessen))]
    z = sum(w * rauschen(g, saat + i) for i, (g, w) in enumerate(zip(groessen, gewichte)))
    return (z - z.min()) / (z.max() - z.min())


def weich(feld, radius):
    """Kachelbarer Weichzeichner (Gauß über die FFT); `radius` in Pixeln."""
    f = np.fft.fftfreq(N)
    fx, fy = np.meshgrid(f, f)
    return np.fft.ifft2(np.fft.fft2(feld) * np.exp(-2 * (np.pi * radius) ** 2 * (fx ** 2 + fy ** 2))).real


def zellen(nu, nv, saat, zittern=0.8):
    """Kachelbare Voronoi-Zellen: je Zelle eines Rasters nu × nv ein Punkt. Gibt je Pixel die Nummer der nächsten Zelle,
    den Abstand zum nächsten Punkt (F1) und zum zweitnächsten (F2) und den Weg vom nächsten Punkt (x, y) zurück – in Pixeln."""
    r = np.random.default_rng(saat)
    px = (np.arange(nu)[None, :] + 0.5 + (r.random((nv, nu)) - 0.5) * zittern) / nu
    py = (np.arange(nv)[:, None] + 0.5 + (r.random((nv, nu)) - 0.5) * zittern) / nv
    ci, cj = np.floor(U * nu).astype(int), np.floor(V * nv).astype(int)
    f1 = np.full((N, N), 1e9)
    f2 = np.full((N, N), 1e9)
    nummer = np.zeros((N, N), int)
    wx = np.zeros((N, N))
    wy = np.zeros((N, N))
    for dj in (-1, 0, 1):
        for di in (-1, 0, 1):
            i, j = (ci + di) % nu, (cj + dj) % nv
            dx = (px[j, i] - U + 0.5) % 1.0 - 0.5          # kürzester Weg über den Rand (kachelbar)
            dy = (py[j, i] - V + 0.5) % 1.0 - 0.5
            d = np.hypot(dx, dy) * N
            naeher = d < f1
            f2 = np.where(naeher, f1, np.minimum(f2, d))
            f1 = np.where(naeher, d, f1)
            nummer = np.where(naeher, j * nu + i, nummer)
            wx = np.where(naeher, -dx * N, wx)
            wy = np.where(naeher, -dy * N, wy)
    return nummer, f1, f2, wx, wy


def zufall_je(nummer, saat, anzahl):
    """Eine Zufallszahl −1..1 je Zellennummer (für Helligkeit und Farbton je Stein, Ziegel, Brett …)."""
    return (np.random.default_rng(saat).random(anzahl) * 2 - 1)[nummer]


def teilung(anzahl, streuung, rng):
    """Grenzen von `anzahl` Stücken, die zusammen 1 ergeben, jedes um ±`streuung` verschieden lang."""
    laengen = 1 + (rng.random(anzahl) * 2 - 1) * streuung
    return np.concatenate([[0], np.cumsum(laengen / laengen.sum())])


def verband(reihen, steine, saat, streuung_reihe=0.2, streuung_stein=0.35):
    """Mauerverband: Reihen verschiedener Höhe, darin Steine verschiedener Länge, jede Reihe zufällig versetzt.
    Gibt je Pixel die Steinnummer und den Abstand zur nächsten Stoßfuge (in U) und Lagerfuge (in V) in Pixeln zurück."""
    rng = np.random.default_rng(saat)
    zeilen = teilung(reihen, streuung_reihe, rng)
    nummer = np.zeros((N, N), int)
    stoss = np.zeros((N, N))
    lager = np.zeros((N, N))
    zeile = np.clip(np.searchsorted(zeilen, V, side="right") - 1, 0, reihen - 1)
    for r in range(reihen):
        drin = zeile == r
        grenzen = teilung(steine, streuung_stein, rng)
        versatz = rng.random()
        u = (U + versatz) % 1.0
        stein = np.clip(np.searchsorted(grenzen, u, side="right") - 1, 0, steine - 1)
        du = np.minimum(u - grenzen[stein], grenzen[stein + 1] - u) * N
        dv = np.minimum(V - zeilen[r], zeilen[r + 1] - V) * N
        nummer = np.where(drin, r * steine + stein, nummer)
        stoss = np.where(drin, du, stoss)
        lager = np.where(drin, dv, lager)
    return nummer, stoss, lager


def maserung(saat, linien, aeste=0, streckung=6.0):
    """Holzmaserung entlang V: feine Jahreslinien, die um Äste herumlaufen. Gibt Helligkeit (−1..1) und Höhe zurück."""
    rng = np.random.default_rng(saat)
    phase = U * linien + 1.5 * (rauschen(40, saat, streckung) - 0.5) + 0.6 * (rauschen(12, saat + 1, streckung) - 0.5)
    ast = np.zeros((N, N))
    for _ in range(aeste):
        cx, cy = rng.random(), rng.random()
        rx, ry = rng.uniform(0.010, 0.022), rng.uniform(0.018, 0.035)
        dx = (U - cx + 0.5) % 1.0 - 0.5
        dy = (V - cy + 0.5) % 1.0 - 0.5
        q = (dx / rx) ** 2 + (dy / ry) ** 2
        phase += 2.2 * np.exp(-q / 6) * np.sign(dx) * 0.5          # die Linien weichen dem Ast aus
        ast = np.maximum(ast, np.exp(-q * 1.5))
    t = phase % 1.0
    linie = np.exp(-((t - 0.5) / 0.16) ** 2)                        # dunkle Spätholz-Linien
    streifen = rauschen(6, saat + 2, streckung * 3)                      # hellere und dunklere Fasern
    helligkeit = -0.42 * linie - 0.45 * (streifen - 0.5) - 0.45 * ast
    hoehe = 0.5 - 0.25 * linie - 0.4 * ast
    return helligkeit, hoehe


def risse(saat, anzahl, laenge, breite=1.2):
    """Kurze Schwundrisse entlang V (Balken): 1 im Riss, 0 daneben."""
    rng = np.random.default_rng(saat)
    feld = np.zeros((N, N))
    for _ in range(anzahl):
        cx, cy = rng.random(), rng.random()
        dx = ((U - cx + 0.5) % 1.0 - 0.5) * N + 2.0 * (rauschen(20, saat + 7, 4.0) - 0.5) * 3
        dy = ((V - cy + 0.5) % 1.0 - 0.5) * N
        l = laenge * rng.uniform(0.6, 1.2)
        verlauf = np.clip(1 - np.abs(dy) / l, 0, 1)                   # spitz auslaufend
        feld = np.maximum(feld, np.exp(-(dx / (breite * verlauf + 0.01)) ** 2) * (np.abs(dy) < l))
    return feld


# ═════════════════════════════════════════════════════════════════════════════
#  Schichten: je Material Helligkeit (−1..1, ×0,5 = Anteil), Farbton (−1..1) und Höhe (Pixel)
# ═════════════════════════════════════════════════════════════════════════════

def holz():
    """Balken, Fachwerk, Rinde: Maserung mit Ästen, wenige Schwundrisse. Kachel 1,6 m."""
    hell, hoehe = maserung(101, 22, aeste=5)
    riss = risse(102, 9, 50)
    hell = hell - 0.35 * riss
    ton = 0.25 * (rauschen(80, 103, 3.0) - 0.5)
    return hell, ton, 3.0 * hoehe - 4.0 * riss


def bretter():
    """Bretterwand, Dielen: acht Bretter je Kachel (2 m), jedes mit eigener Maserung, Farbe und einem Stoß."""
    rng = np.random.default_rng(111)
    grenzen = teilung(8, 0.18, rng)
    brett = np.clip(np.searchsorted(grenzen, U, side="right") - 1, 0, 7)
    stoss = rng.random(8)                                           # wo das Brett aufhört und das nächste anfängt
    vor_stoss = (V > stoss[brett]).astype(int)
    nummer = brett * 2 + vor_stoss
    rand = np.minimum(U - grenzen[brett], grenzen[brett + 1] - U) * N
    dv = np.abs((V - stoss[brett] + 0.5) % 1.0 - 0.5) * N
    hell_m, hoehe_m = maserung(112, 40, aeste=6, streckung=8.0)
    # jedes Brett hat seine Maserung etwas verschoben
    verschiebung = (np.random.default_rng(113).random(16) * N).astype(int)[nummer]
    hell_m = hell_m[np.arange(N)[:, None], (np.arange(N)[None, :] + verschiebung) % N]
    fuge = np.minimum(rand, dv)
    kante = np.clip(fuge / 3.0, 0, 1) ** 0.5                         # gebrochene Kante, 3 px
    hell = 0.18 * zufall_je(nummer, 114, 16) + 0.8 * hell_m - 0.45 * (1 - np.clip(fuge / 1.6, 0, 1))
    ton = 0.45 * zufall_je(nummer, 115, 16)
    hoehe = 3.0 * kante + 1.0 * hoehe_m + 0.6 * zufall_je(nummer, 116, 16)
    return hell, ton, hoehe


def mauer():
    """Quadermauer: acht Reihen (2 m, also gut 25 cm) mit Steinen verschiedener Länge, weichen Fugen, abgeschlagenen
    Kanten und leichten Farbunterschieden von Stein zu Stein."""
    nummer, stoss, lager = verband(8, 5, 121)
    abstand = np.minimum(stoss, lager)
    abstand = abstand + 3.5 * (rauschen(6, 122) - 0.5) + 2.0 * (rauschen(2.5, 123) - 0.5)   # Kanten nicht gerade
    stein = np.clip((abstand - 2.5) / 5.0, 0, 1)
    flaeche = fbm(124, [40, 12, 4])
    fuge = 1 - np.clip((abstand - 1.5) / 3.0, 0, 1)                    # gut 1,5 cm breit, weich auslaufend
    hell = 0.32 * zufall_je(nummer, 125, 40) + 0.20 * (flaeche - 0.5) - 0.60 * fuge
    ton = 0.55 * zufall_je(nummer, 126, 40) + 0.2 * (rauschen(30, 127) - 0.5)
    hoehe = 7.0 * np.sqrt(stein) + 3.0 * flaeche + 1.0 * zufall_je(nummer, 128, 40)
    return hell, ton, hoehe


def fels():
    """Gewachsener Fels (Berg, Erz): große Bruchflächen mit eigener Neigung, Risse dazwischen, dazu Schichtung. Kachel 4 m."""
    nummer, f1, f2, wx, wy = zellen(5, 5, 131, 0.9)
    rng = np.random.default_rng(132)
    neigung_x, neigung_y = (rng.random(25) * 2 - 1)[nummer], (rng.random(25) * 2 - 1)[nummer]
    facette = 0.12 * (neigung_x * wx + neigung_y * wy)                  # jede Bruchfläche schräg für sich
    grenze = (f2 - f1) / 2
    riss = np.exp(-(grenze / 1.2) ** 2) * np.clip((rauschen(30, 133) - 0.5) * 6, 0, 1)    # Risse nur stückweise
    flaeche = fbm(134, [60, 20, 7, 2.5])
    schicht = np.sin(2 * np.pi * (V * 7 + 0.5 * rauschen(60, 135))) * 0.5 + 0.5
    hell = 0.16 * zufall_je(nummer, 136, 25) + 0.22 * (flaeche - 0.5) + 0.06 * (schicht - 0.5) - 0.5 * riss
    ton = 0.45 * zufall_je(nummer, 137, 25) + 0.35 * (rauschen(50, 138) - 0.5)
    hoehe = weich(facette, 3.0) + 10.0 * flaeche + 1.5 * schicht - 3.0 * riss
    return hell, ton, hoehe


def ziegel():
    """Ziegeldach: Die Modelle haben die Reihen schon als Form (Sägezahn mit dunkler Kante alle 45 cm), darum hier keine Reihen,
    nur die Ziegel darin: Spalten von gut 17 cm (Kachel 1,2 m) mit feiner Stoßfuge, jeder Ziegel etwas anders gebrannt,
    dazu leichte Abnutzung (dunkle Flecken von Moos und Schmutz, hellere ausgeblichene Stellen)."""
    nummer, laengs, quer = (a.T for a in verband(7, 3, 141, streuung_reihe=0.08, streuung_stein=0.3))   # Spalten statt Reihen
    fuge = 1 - np.clip((quer - 0.6) / 1.6, 0, 1)
    ende = 1 - np.clip((laengs - 0.5) / 4.0, 0, 1)                       # wo ein Ziegel in den nächsten übergeht: nur ein Hauch
    alt = fbm(142, [10, 3])
    flecken = np.clip((rauschen(16, 143) - 0.6) * 4, 0, 1)
    bleich = np.clip((rauschen(24, 146) - 0.62) * 4, 0, 1)
    rund = np.sqrt(np.clip(quer / 8.0, 0, 1))                          # jeder Ziegel leicht gewölbt
    hell = 0.30 * zufall_je(nummer, 144, 21) + 0.12 * (alt - 0.5) - 0.30 * flecken + 0.12 * bleich - 0.45 * fuge - 0.08 * ende
    ton = 0.7 * zufall_je(nummer, 145, 21) - 0.4 * flecken - 0.2 * bleich
    return hell, ton, 4.0 * rund + 1.0 * alt - 1.0 * ende


def stroh():
    """Strohdach: Die Lagen sind im Modell schon Form (alle 70 cm), hier nur Halme und Strähnen in Fallrichtung,
    verschieden hell (Kachel 1,5 m)."""
    halme = rauschen(1.3, 151, 40.0)
    halme2 = rauschen(3.0, 152, 20.0)
    straehnen = rauschen(9, 153, 9.0)                                   # Strähnen von 2–3 cm Breite, gut 25 cm lang
    hell = 0.40 * (halme - 0.5) + 0.50 * (halme2 - 0.5) + 0.80 * (straehnen - 0.5)
    ton = 0.6 * (straehnen - 0.5) + 0.3 * (halme2 - 0.5)
    return hell, ton, 2.0 * halme + 2.0 * halme2 + 4.0 * straehnen


def schiefer():
    """Schieferdach: Die Reihen sind im Modell schon Form (alle 32 cm), hier die Platten darin: verschieden breit (Kachel 2 m),
    mit dunkler Stoßfuge, jede Platte etwas anders getönt, mit Spaltflächen."""
    nummer, laengs, quer = (a.T for a in verband(9, 4, 161, streuung_reihe=0.4, streuung_stein=0.4))
    fuge = 1 - np.clip((quer - 0.5) / 1.4, 0, 1)
    ende = 1 - np.clip((laengs - 0.5) / 4.0, 0, 1)
    spalt = fbm(162, [20, 5, 2])
    hell = 0.24 * zufall_je(nummer, 163, 36) + 0.16 * (spalt - 0.5) - 0.50 * fuge - 0.08 * ende
    ton = 0.6 * zufall_je(nummer, 164, 36)
    return hell, ton, 2.5 * np.sqrt(np.clip(quer / 6.0, 0, 1)) + 1.5 * spalt


def laub():
    """Laubkronen, Kraut, Farn: viele kleine Blätter übereinander, dazwischen Schatten (Kachel 1 m)."""
    rng = np.random.default_rng(171)
    hoehe = np.full((N, N), -1.0)
    blatt = np.zeros((N, N), int)
    for k in range(2600):
        cx, cy = rng.uniform(0, N), rng.uniform(0, N)
        l, b = rng.uniform(9, 15), rng.uniform(4.5, 7.5)
        w = rng.uniform(0, np.pi)
        r = int(l) + 2
        ys = (np.arange(int(cy) - r, int(cy) + r + 1)) % N
        xs = (np.arange(int(cx) - r, int(cx) + r + 1)) % N
        dy = np.arange(int(cy) - r, int(cy) + r + 1)[:, None] - cy
        dx = np.arange(int(cx) - r, int(cx) + r + 1)[None, :] - cx
        a = (dx * np.cos(w) + dy * np.sin(w)) / l
        c = (-dx * np.sin(w) + dy * np.cos(w)) / b
        q = a ** 2 + c ** 2
        z = k / 2600 + 0.25 * np.sqrt(np.clip(1 - q, 0, 1)) - 0.03 * np.abs(c) * (q < 1)   # leicht gewölbt, Mittelrippe
        ausschnitt = hoehe[np.ix_(ys, xs)]
        neu = (q < 1) & (z > ausschnitt)
        hoehe[np.ix_(ys, xs)] = np.where(neu, z, ausschnitt)
        b_alt = blatt[np.ix_(ys, xs)]
        blatt[np.ix_(ys, xs)] = np.where(neu, k, b_alt)
    luecke = hoehe < 0
    hoehe = np.where(luecke, -0.3, hoehe)
    tiefe = weich(np.where(luecke, 0.0, 1.0), 3.0)                     # Schatten zwischen den Blättern
    hell = 0.22 * zufall_je(blatt, 172, 2600) + 0.25 * (hoehe - 0.5) - 0.45 * (1 - tiefe) + 0.20 * (rauschen(60, 173) - 0.5)
    ton = 0.6 * zufall_je(blatt, 174, 2600) + 0.3 * (rauschen(50, 175) - 0.5)
    return hell, ton, 9.0 * weich(hoehe, 0.6)


def boden():
    """Gras, Erde, Lehm, Moos: feine Körnung, Krumen und kleine Steinchen, dazu Büschel (Kachel 3 m)."""
    korn = fbm(181, [8, 3, 1.2])
    nummer, f1, _, _, _ = zellen(24, 24, 182, 1.0)
    steinchen = (zufall_je(nummer, 183, 576) > 0.7) & (f1 < 3.2)
    bueschel = rauschen(14, 184)
    hell = 0.80 * (korn - 0.5) + 0.35 * (bueschel - 0.5) + 0.30 * steinchen * zufall_je(nummer, 185, 576)
    ton = 0.4 * (rauschen(30, 186) - 0.5) + 0.2 * (korn - 0.5)
    return hell, ton, 5.0 * korn + 2.5 * bueschel + 1.5 * steinchen * np.clip(1 - f1 / 3.2, 0, 1)


def pflaster():
    """Kopfsteinpflaster (und Mauerkronen von oben): rundliche Steine in Reihen, sandige Fugen (Kachel 2 m, Steine gut 20 cm)."""
    nummer, f1, f2, _, _ = zellen(9, 9, 191, 0.65)
    rand = (f2 - f1) / 2 + 2.0 * (rauschen(4, 192) - 0.5)
    kuppe = np.sqrt(np.clip((rand - 1.6) / 9.0, 0, 1))
    flaeche = fbm(193, [14, 4])
    fuge = 1 - np.clip((rand - 0.8) / 2.2, 0, 1)
    hell = 0.24 * zufall_je(nummer, 194, 81) + 0.10 * (flaeche - 0.5) + 0.10 * kuppe - 0.42 * fuge
    ton = 0.6 * zufall_je(nummer, 195, 81) + 0.2 * fuge
    return hell, ton, 7.0 * kuppe + 1.0 * flaeche


def flecken():
    """Großflächige Unterschiede (der Shader legt die Schicht etwa 14 m groß über alles): Helligkeit und Farbton."""
    return 2 * (fbm(201, [90, 40, 18]) - 0.5), 2 * (fbm(202, [110, 50]) - 0.5), np.zeros((N, N))


SCHICHTEN = [holz, bretter, mauer, fels, ziegel, stroh, schiefer, laub, boden, pflaster, flecken]


# ═════════════════════════════════════════════════════════════════════════════
#  Ausgabe
# ═════════════════════════════════════════════════════════════════════════════

def kodieren(wert):
    """−1..1 → 1..255 mit 128 als Null (der Shader rechnet (Wert − 128) / 127)."""
    return np.clip(np.round(128 + 127 * wert), 1, 255).astype(np.uint8)


def schicht(erzeuger):
    hell, ton, hoehe = erzeuger()
    hell = hell - hell.mean()                                           # im Mittel bleibt die Farbe des Modells
    ton = ton - ton.mean()
    # Neigung aus der Höhe (in Pixeln): eine Stufe von 1 px Höhe auf 1 px Weg ist 45°
    gu = (np.roll(hoehe, -1, axis=1) - np.roll(hoehe, 1, axis=1)) / 2
    gv = (np.roll(hoehe, -1, axis=0) - np.roll(hoehe, 1, axis=0)) / 2
    laenge = np.sqrt(1 + gu ** 2 + gv ** 2)
    return np.dstack([kodieren(hell), kodieren(ton), kodieren(-gu / laenge), kodieren(-gv / laenge)])


def png(pfad, bild):
    """RGBA als PNG ohne Pillow: je Zeile der kleinere von zwei Filtern (Sub, Up), dann zlib."""
    hoehe, breite, _ = bild.shape
    roh = bild.astype(np.int16)
    sub = (roh - np.pad(roh, ((0, 0), (1, 0), (0, 0)))[:, :-1]) % 256
    up = (roh - np.pad(roh, ((1, 0), (0, 0), (0, 0)))[:-1]) % 256
    kosten = lambda f: np.minimum(f, 256 - f).reshape(hoehe, -1).sum(axis=1)
    nimm_up = kosten(up) < kosten(sub)
    zeilen = np.where(nimm_up[:, None, None], up, sub).astype(np.uint8).reshape(hoehe, -1)
    filt = np.where(nimm_up, 2, 1).astype(np.uint8)[:, None]
    daten = np.hstack([filt, zeilen]).tobytes()
    def chunk(t, c): return struct.pack(">I", len(c)) + t + c + struct.pack(">I", zlib.crc32(t + c) & 0xffffffff)
    with open(pfad, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", breite, hoehe, 8, 6, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(daten, 9)) + chunk(b"IEND", b""))


def main():
    os.makedirs(ZIEL, exist_ok=True)
    bild = np.vstack([schicht(e) for e in SCHICHTEN])
    pfad = os.path.join(ZIEL, "strukturen.png")
    png(pfad, bild)
    print("geschrieben:", os.path.normpath(pfad), f"({os.path.getsize(pfad) // 1024} KB)")


if __name__ == "__main__":
    main()
