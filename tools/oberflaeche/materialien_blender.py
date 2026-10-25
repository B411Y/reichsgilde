"""Material-Bibliothek für die Modelle (Modell-Texturen, Gruppen 1 bis 3) – in Blender modelliert und mit Cycles von vorn gebacken.

    ~/werkzeuge/blender/blender.sh -b --python tools/oberflaeche/materialien_blender.py -- [Optionen]

        --nur=bruchstein,quader  nur diese Materialien neu backen, die übrigen kommen aus dem Zwischenspeicher
        --groesse=1024           Kantenlänge je Textur in Pixeln (256 für schnelle Proben; geschrieben wird nur bei 1024)
        --proben=64              Cycles-Proben je Pixel
        --zwischen=<ordner>      Zwischenspeicher der gebackenen Rohdaten (Vorgabe ~/.cache/reichsgilde/materialien)
        --nur-packen             nichts backen, nur aus dem Zwischenspeicher packen und die Vorschau schreiben
        --nah                    dazu Nahaufnahmen über die Kachelecke in den Zwischenspeicher (vorschau/<name>_nah.jpg)

Wie die Bodentexturen (tools/oberflaeche/bodentexturen_blender.py, dessen Werkzeuge hier mitbenutzt werden): Jedes Material ist
eine kleine 3D-Szene, so groß wie eine Kachel (KACHEL, Meter): ein Höhenfeld mit einem Pixel Maschenweite (Steine, Fugen, Putz,
Maserung, Ziegel als Höhe und Farbe je Eckpunkt) und für das Stroh Tausende Halme. Was über den Rand ragt, liegt ein zweites
Mal auf der Gegenseite: Das Bild ist nahtlos. Die Kamera schaut senkrecht auf die Fläche (orthografisch), Cycles backt Grundfarbe,
Normale, Umgebungsverdeckung, Höhe und Rauheit. Der obere Bildrand ist auf der Wand **oben**, auf dem Dach **firstwärts**.

Schreibt nach game/assets/texturen/modelle/ (Format wie beim Boden, Beschreibung in docs/ARCHITEKTUR.md, „Material-Bibliothek“):
    modelle_farbe_hoehe.webp     RGBA: RGB Farbe (sRGB, Verdeckung eingerechnet, verlustbehaftet q95), A Höhe (1 tief … 255 hoch,
                                 verlustfrei)
    modelle_normal_rauheit.webp  RGB:  R/G Normale (OpenGL: G+ zum oberen Bildrand), B Rauheit; verlustfrei
je eine Schicht zu 1024 × 1024 je Material, Zeile für Zeile in einem Gitter (siehe gitter: ein WebP darf höchstens 16.383 Pixel
hoch sein, ab 16 Schichten also zwei Spalten; slices/horizontal und slices/vertical in den .import-Dateien passen dazu)
und in game/daten/materialbibliothek.json den Block „schichten“ (Name, Kachelgröße, Projektion, mittlere Farbe linear). Die mittlere
Farbe braucht der Shader: Er teilt die Textur durch sie und färbt mit der Modellfarbe – im Mittel bleibt jedes Material so hell und
so bunt wie bisher, die Textur bringt nur Steine, Fugen, Maserung und Licht. Die Zuordnung Materialname → Schicht („namen“) steht
von Hand in derselben Datei und bleibt beim Schreiben erhalten. Vorschaubilder: docs/bilder/materialien/ (je Material 2 × 2 gekachelt).
"""
import json
import os
import sys
import time

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bodentexturen_blender as bt   # noqa: E402  (main() läuft dort nur als eigenes Skript)
from bodentexturen_blender import (Teile, abtasten, drehung, fbm, form_blatt, form_stab, glatt, je_zelle,  # noqa: E402
                                   kanal, mische, norm, rauschen, srgb, verteilen)

WURZEL = bt.WURZEL
ZIEL = os.path.join(WURZEL, "game", "assets", "texturen", "modelle")
DATEN = os.path.join(WURZEL, "game", "daten", "materialbibliothek.json")
VORSCHAU = os.path.join(WURZEL, "docs", "bilder", "materialien")

_args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
_opt = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in _args)
N = int(_opt.get("groesse", 1024))
bt.N = N
bt.PROBEN = int(_opt.get("proben", 64))
ZWISCHEN = os.path.expanduser(_opt.get("zwischen", "~/.cache/reichsgilde/materialien"))
bt.ZWISCHEN = ZWISCHEN

# Name, Kachelgröße in Metern, Projektion (wand: an der Fläche ausgerichtet; dach: entlang der Dachneigung; reihen: dazu die
# Spalten je Modellreihe versetzt, für Deckungen, deren Reihen das Modell als Form hat). Neue Materialien kommen ans Ende:
# Die Nummer der Schicht steht in Modellen mit „Farben“ je Fläche im Netz (tools/modelle/bausteine.py, arten_setzen).
SCHICHTEN = [
    ("bruchstein", 2.4, "wand"), ("quader", 2.4, "wand"), ("kalkputz", 3.0, "wand"), ("lehmputz", 2.0, "wand"),
    ("fachwerkholz", 1.6, "wand"), ("bretter", 2.0, "wand"), ("dachziegel", 1.2, "reihen"), ("schiefer", 1.2, "reihen"),
    ("stroh", 1.5, "dach"), ("sandstein", 1.5, "wand"), ("ziegelstein", 1.2, "wand"),
    # Gruppe 2
    ("eisen", 0.8, "wand"), ("kupfer", 1.2, "dach"), ("holzschindeln", 1.2, "reihen"), ("leder", 0.6, "wand"),
    ("tuch", 0.5, "wand"), ("farbholz", 1.2, "wand"),
    # Gruppe 3
    ("rinde", 1.2, "wand"), ("fels", 6.0, "wand"), ("planken", 2.0, "wand"), ("laub", 1.5, "wand"), ("nadeln", 1.2, "wand"),
]
# Höhe einer Reihe (Meter) für Deckungen mit Projektion „reihen“ – wie in tools/modelle/bausteine.py (ZIEGEL, SCHIEFER,
# SCHINDEL). Damit zeichnet der Shader Reihen auf Dachflächen, die das Modell ohne Reihen hat (Turmhelme, Pultdächer).
REIHE = {"dachziegel": 0.45, "schiefer": 0.32, "holzschindeln": 0.28}
NAMEN = [s[0] for s in SCHICHTEN]
KACHEL = {s[0]: s[1] for s in SCHICHTEN}


def K():
    return bt.K


def raster():
    return bt.raster()


def zellen2(x, y, nx, ny, saat, zittern=0.85):
    """Kachelbare Voronoi-Zellen wie bt.zellen, aber nx × ny Zellen (breiter als hoch für liegende Steine). Gibt Nummer,
    F1, F2 (Meter) und den Weg vom nächsten Punkt zurück."""
    r = np.random.default_rng(saat)
    k = K()
    cx, cy = k / nx, k / ny
    px = (np.arange(nx)[None, :] + 0.5 + (r.random((ny, nx)) - 0.5) * zittern) * cx
    py = (np.arange(ny)[:, None] + 0.5 + (r.random((ny, nx)) - 0.5) * zittern) * cy
    ci, cj = np.floor(x / cx).astype(int), np.floor(y / cy).astype(int)
    f1 = np.full(x.shape, 1e9)
    f2 = np.full(x.shape, 1e9)
    nummer = np.zeros(x.shape, int)
    dx1 = np.zeros(x.shape)
    dy1 = np.zeros(x.shape)
    for dj in (-2, -1, 0, 1, 2):
        for di in (-2, -1, 0, 1, 2):
            ni, nj = ci + di, cj + dj
            wi, wj = ni % nx, nj % ny
            qx = px[wj, wi] + (ni - wi) * cx
            qy = py[wj, wi] + (nj - wj) * cy
            dx, dy = x - qx, y - qy
            d = np.hypot(dx, dy)
            naeher = d < f1
            f2 = np.where(naeher, f1, np.minimum(f2, d))
            f1 = np.where(naeher, d, f1)
            nummer = np.where(naeher, wj * nx + wi, nummer)
            dx1 = np.where(naeher, dx, dx1)
            dy1 = np.where(naeher, dy, dy1)
    return nummer, f1, f2, dx1, dy1


def streifen(groesse, saat, laenge=6.0):
    """Kachelbares Rauschen, senkrecht lang gezogen (Laufspuren, Maserung): `laenge`-mal so hoch wie breit
    (bt.rauschen zieht mit `streckung` die Flecken in y lang; quer liegende Bänder: dasselbe mit .T)."""
    return rauschen(groesse, saat, laenge)


def palette(rng, n, farben):
    """Je Stück eine Farbe aus [(hex, gewicht), …], dazu ±12 % Helligkeit."""
    p = np.array([w for _, w in farben], float)
    basis = np.array([srgb(c) for c, _ in farben])[rng.choice(len(farben), n, p=p / p.sum())]
    return basis * rng.uniform(0.88, 1.12, n)[:, None]


def verband(y, x, hoehen, laengen_von, rng, versatz_zufall=True):
    """Lagen eines Mauerverbands: `hoehen` (Lagenhöhen, Summe = Kachel), je Lage Steinlängen aus `laengen_von(j)` (Summe =
    Kachel). Gibt je Pixel Steinnummer, Abstand zur nächsten Lager- und Stoßfuge (Meter) und Lage zurück."""
    k = K()
    grenzen = np.concatenate([[0], np.cumsum(hoehen)])
    lage = np.clip(np.searchsorted(grenzen, y, side="right") - 1, 0, len(hoehen) - 1)
    ey = np.minimum(y - grenzen[lage], grenzen[lage + 1] - y)
    ex = np.full(x.shape, 1e9)
    nummer = np.zeros(x.shape, int)
    zaehler = 0
    for j in range(len(hoehen)):
        l = np.asarray(laengen_von(j), float)
        start = rng.random() * k if versatz_zufall else 0.0
        stoesse = (np.concatenate([[0], np.cumsum(l)])[:-1] + start) % k
        stoesse = np.sort(stoesse)
        s = lage == j
        xs = x[s]
        b = np.concatenate([stoesse - k, stoesse, stoesse + k])
        i = np.searchsorted(b, xs, side="right")
        ex[s] = np.minimum(xs - b[i - 1], b[i] - xs)
        nummer[s] = zaehler + (i - 1) % len(stoesse)
        zaehler += len(stoesse)
    return nummer, ex, ey, lage, zaehler


def flaechennetz(h, farbe, rau):
    """Das Höhenfeld als Netz (wie der Boden: eine Masche je Pixel, über den Rand periodisch)."""
    return bt.bodennetz(h, farbe, np.broadcast_to(np.asarray(rau, float), h.shape))


# ═════════════════════════════════════════════════════════════════════════════
#  Die Materialien. Jedes baut seine Szene und gibt (Anteil der Verdeckung in der Farbe, Normalenstärke) zurück.
# ═════════════════════════════════════════════════════════════════════════════

def bruchstein(rng):
    """Bruchsteinmauer: unregelmäßige, eher liegende Steine in grob durchgehenden Lagen, gewölbt, Mörtel zurückgesetzt."""
    x, y = raster()
    wx, wy = bt.verzerrt(x, y, 0.06, [0.25, 0.08], 1)
    nummer, f1, f2, dx, dy = zellen2(wx, wy, 6, 11, 2, 0.7)
    anzahl = 6 * 11
    rand = (f2 - f1) / 2                                             # Abstand zur Fuge
    fuge = 0.008 + 0.008 * fbm(3, [0.3, 0.1])
    stein = glatt(fuge, fuge + 0.035, rand)
    kipp_x, kipp_y = je_zelle(nummer, 4, anzahl) - 0.5, je_zelle(nummer, 5, anzahl) - 0.5
    buckel = je_zelle(nummer, 6, anzahl)
    narben = fbm(7, [0.06, 0.02, 0.008])
    h = stein * (0.018 + 0.014 * buckel + 0.06 * (kipp_x * dx + kipp_y * dy) + 0.006 * narben) - 0.012 * (1 - stein)
    h += 0.0015 * rauschen(0.006, 8)
    farbe_stein = palette(rng, anzahl, [("#8a857c", 3), ("#9a9284", 2), ("#77736c", 2), ("#a08c74", 1.2), ("#6a655e", 1),
                                        ("#8c7c6c", 1)])[nummer]
    farbe_stein *= (0.86 + 0.28 * narben)[..., None]
    farbe_stein = mische(farbe_stein, srgb("#55504a"), 0.35 * (1 - glatt(0.0, 0.05, rand)) * stein)   # Kanten dunkler
    moertel = mische(srgb("#a29a88"), srgb("#8a8272"), fbm(9, [0.1, 0.03]))
    farbe = mische(moertel, farbe_stein, stein)
    lauf = glatt(0.55, 0.85, streifen(0.25, 10))                     # dunkle Laufspuren von oben
    farbe = mische(farbe, srgb("#4a463e"), 0.25 * lauf)
    flechte = glatt(0.78, 0.9, fbm(11, [0.08, 0.03])) * stein
    farbe = mische(farbe, srgb("#9a9a7a"), 0.35 * flechte)
    flaechennetz(h, farbe, 0.92 - 0.1 * stein)
    return 0.6, 1.0


def quader(rng):
    """Quadermauerwerk: Lagen von 30–45 cm, Steine 45–110 cm lang, schmale Fugen, bestoßene Kanten, Spuren des Werkzeugs."""
    x, y = raster()
    k = K()
    hoehen = rng.uniform(0.32, 0.46, 6)
    hoehen *= k / hoehen.sum()

    def laengen(j):
        l = rng.uniform(0.45, 1.1, 12)
        n = max(2, int(round(k / l.mean())))
        l = rng.uniform(0.45, 1.1, n)
        return l * k / l.sum()

    wx, wy = bt.verzerrt(x, y, 0.004, [0.05, 0.015], 21)
    nummer, ex, ey, lage, anzahl = verband(wy, wx, hoehen, laengen, rng)
    rand = np.minimum(ex, ey) + 0.005 * (fbm(22, [0.03, 0.01]) - 0.5)
    fuge = 0.006
    stein = glatt(fuge, fuge + 0.012, rand)
    # Scharrierung, je Stein in eigener Richtung (ganze Zahl Linien über die Kachel: nahtlos)
    winkel = np.random.default_rng(23).random(anzahl) * 0.8 - 0.4 + np.pi / 2
    fa, fb = np.round(np.cos(winkel) * k / 0.009), np.round(np.sin(winkel) * k / 0.009)
    hieb = np.sin(2 * np.pi * (x * fa[nummer] + y * fb[nummer]) / k)
    narben = fbm(24, [0.05, 0.015, 0.006])
    h = stein * (0.012 + 0.004 * je_zelle(nummer, 25, anzahl) + 0.0004 * hieb + 0.003 * narben) - 0.004 * (1 - stein)
    h -= 0.004 * glatt(0.82, 0.95, fbm(26, [0.04, 0.015])) * stein     # ausgebrochene Stellen
    farbe_stein = palette(rng, anzahl, [("#8e8a82", 3), ("#9a948a", 2), ("#85817a", 2), ("#a09482", 1), ("#7a7670", 1)])[nummer]
    farbe_stein *= (0.9 + 0.2 * narben + 0.03 * hieb)[..., None]
    farbe_stein = mische(farbe_stein, srgb("#6a665e"), 0.3 * (1 - glatt(fuge, 0.03, rand)))
    farbe = mische(srgb("#a8a294"), farbe_stein, stein)
    lauf = glatt(0.5, 0.85, streifen(0.3, 27, 8.0))
    farbe = mische(farbe, srgb("#4e4a42"), 0.3 * lauf)
    flechte = glatt(0.8, 0.92, fbm(28, [0.1, 0.03]))
    farbe = mische(farbe, srgb("#a8a68a"), 0.3 * flechte)
    flaechennetz(h, farbe, 0.88 - 0.06 * stein)
    return 0.55, 1.0


def kalkputz(rng):
    """Kalkputz: leicht wellig von der Kelle, feine Körnung, schwache Laufspuren, hier und da ein abgeplatztes Stück."""
    x, y = raster()
    k = K()
    welle = fbm(31, [0.8, 0.25, 0.08])
    korn = rauschen(0.004, 32)
    h = 0.004 * welle + 0.0005 * korn
    for _ in range(140):                                              # Kellenschläge: flache, gegeneinander versetzte Mulden
        cx, cy = rng.random() * k, rng.random() * k
        ddx = (x - cx + k / 2) % k - k / 2
        ddy = (y - cy + k / 2) % k - k / 2
        w = rng.random() * np.pi
        u = ddx * np.cos(w) + ddy * np.sin(w)
        v = -ddx * np.sin(w) + ddy * np.cos(w)
        d = (u / rng.uniform(0.12, 0.25)) ** 2 + (v / rng.uniform(0.06, 0.1)) ** 2
        h -= 0.0012 * (1 - glatt(0.5, 1.0, d))
    platz = np.zeros_like(h)
    for _ in range(2):                                                # abgeplatzte Stellen: der Stein schaut durch
        cx, cy = rng.random() * k, rng.random() * k
        ddx = (x - cx + k / 2) % k - k / 2
        ddy = (y - cy + k / 2) % k - k / 2
        r = rng.uniform(0.06, 0.12)
        d = np.hypot(ddx, ddy * 1.6) / r + 0.9 * (fbm(33 + _, [0.06, 0.02]) - 0.5)
        platz = np.maximum(platz, 1 - glatt(0.85, 1.0, d))
    h -= 0.005 * platz
    farbe = mische(srgb("#d6cdb8"), srgb("#cbc1aa"), glatt(0.4, 0.8, fbm(34, [0.6, 0.2])))
    farbe = mische(farbe, srgb("#e2dccc"), 0.35 * glatt(0.55, 0.85, welle))
    farbe *= (0.95 + 0.08 * korn)[..., None]
    lauf = glatt(0.55, 0.9, streifen(0.35, 35, 10.0))
    farbe = mische(farbe, srgb("#9a907c"), 0.22 * lauf)
    stein = mische(srgb("#9a9284"), srgb("#867e70"), fbm(36, [0.03, 0.01]))
    farbe = mische(farbe, stein, 0.8 * platz)
    flaechennetz(h, farbe, 0.93 - 0.05 * platz)
    return 0.4, 0.9


def lehmputz(rng):
    """Lehmputz im Gefach: von Hand verstrichen, uneben, feine Schwundrisse, Strohhäcksel in der Oberfläche."""
    x, y = raster()
    welle = fbm(41, [0.4, 0.15, 0.05])
    korn = rauschen(0.004, 42)
    h = 0.008 * welle + 0.0007 * korn
    wx, wy = bt.verzerrt(x, y, 0.05, [0.12, 0.04], 43)
    nummer, f1, f2, _, _ = zellen2(wx, wy, 14, 14, 44, 0.9)
    riss = (f2 - f1)
    rissig = glatt(0.35, 0.7, fbm(45, [0.5, 0.2]))
    tief = (1 - glatt(0.0, 0.004, riss)) * rissig
    h -= 0.003 * tief
    farbe = mische(srgb("#b49a72"), srgb("#a88c68"), glatt(0.3, 0.8, fbm(46, [0.15, 0.05])))
    farbe = mische(farbe, srgb("#c4ae88"), 0.35 * glatt(0.55, 0.85, welle))
    farbe *= (0.92 + 0.12 * korn)[..., None]
    farbe = mische(farbe, srgb("#5a4632"), 0.7 * tief)
    lauf = glatt(0.55, 0.9, streifen(0.3, 47, 8.0))
    farbe = mische(farbe, srgb("#7a6448"), 0.2 * lauf)
    flaechennetz(h, farbe, 0.95)
    teile = Teile()
    stab = form_stab(3, 4, 0.05)
    n = 5000
    px, py = rng.random(n) * K(), rng.random(n) * K()
    l = rng.uniform(0.012, 0.045, n)
    d = rng.uniform(0.0012, 0.0022, n)
    f = mische(srgb("#c8aa66"), srgb("#9a8048"), rng.random(n))
    verteilen(teile, stab, np.stack([px, py, abtasten(h, px, py) - d * 0.3], -1),
              drehung(rng.random(n) * 7, rng.normal(0, 0.05, n)), np.stack([l, d, d * 0.5], -1), f, f * 0.9, 0.85)
    teile.objekt("haecksel")
    return 0.4, 0.9


def maserung(x, y, saat, ring=0.011, staerke=1.0):
    """Holzmaserung längs y (senkrecht): Jahreslinien, Fasern, Äste. Gibt (Höhe 0..1, Linie 0..1, Ast 0..1) zurück."""
    k = K()
    verzug = 0.05 * (fbm(saat, [0.4, 0.12]) - 0.5) + 0.012 * (streifen(0.05, saat + 1, 10.0) - 0.5)
    ast = np.zeros_like(x)
    for _ in range(3):                                                # Äste verbiegen die Jahreslinien
        r = np.random.default_rng(saat + 7 + _)
        cx, cy = r.random() * k, r.random() * k
        ddx = (x - cx + k / 2) % k - k / 2
        ddy = (y - cy + k / 2) % k - k / 2
        d = np.hypot(ddx * 1.0, ddy * 0.45)
        rr = r.uniform(0.012, 0.025)
        verzug += 0.03 * np.exp(-(d / (rr * 3)) ** 2) * np.sign(ddx)
        ast = np.maximum(ast, 1 - glatt(rr * 0.6, rr, np.hypot(ddx, ddy * 0.8)))
    # Ganze Zahl Jahreslinien über die Kachel, damit die Maserung nahtlos bleibt
    u = (x + verzug) * round(k / ring) / k
    linie = glatt(0.55, 0.95, 0.5 + 0.5 * np.sin(2 * np.pi * u)) ** 1.5
    fasern = streifen(0.006, saat + 3, 20.0)
    hoehe = norm(0.6 * (1 - linie) + 0.4 * fasern)
    return hoehe, linie, ast


def fachwerkholz(rng):
    """Eichenbalken, längs senkrecht: Maserung, Schwundrisse längs der Faser, flache Beilhiebe, Äste."""
    x, y = raster()
    k = K()
    hoehe, linie, ast = maserung(x, y, 51)
    hieb = 0.5 + 0.5 * np.cos(2 * np.pi * (y * round(k / 0.09) / k + 0.3 * fbm(52, [0.2]) + 0.2 * (fbm(53, [0.08]) - 0.5)))
    hieb = hieb * glatt(0.3, 0.7, fbm(54, [0.15, 0.05]))
    h = 0.002 * hoehe + 0.0012 * hieb - 0.0015 * ast
    riss = np.zeros_like(h)
    for _ in range(9):                                                # Schwundrisse: lang, schmal, leicht gewellt
        x0, y0 = rng.random() * k, rng.random() * k
        laenge = rng.uniform(0.25, 0.9)
        breite = rng.uniform(0.0015, 0.003)
        ddy = (y - y0 + k / 2) % k - k / 2
        welle = 0.006 * np.sin(ddy / laenge * 5 + rng.random() * 6)
        ddx = (x - x0 + k / 2) % k - k / 2 - welle
        auf = np.clip(1 - np.abs(ddy) / (laenge / 2), 0, 1) ** 0.5
        riss = np.maximum(riss, (1 - glatt(0.0, breite * auf + 1e-5, np.abs(ddx))) * (auf > 0))
    h -= 0.004 * riss
    farbe = mische(srgb("#6e5038"), srgb("#4a3222"), 0.8 * linie)
    farbe *= (0.85 + 0.25 * hoehe)[..., None]
    farbe = mische(farbe, srgb("#7a5a40"), 0.25 * glatt(0.4, 0.8, streifen(0.2, 55, 6.0)))
    farbe = mische(farbe, srgb("#3e2a1c"), 0.8 * ast)
    farbe = mische(farbe, srgb("#1e140e"), 0.85 * riss)
    farbe *= (0.96 + 0.06 * hieb)[..., None]
    grau = glatt(0.55, 0.85, streifen(0.3, 56, 5.0))                    # verwittert: grauer
    farbe = mische(farbe, srgb("#6e6458"), 0.25 * grau)
    flaechennetz(h, farbe, 0.8 + 0.12 * grau)
    return 0.45, 1.0


def bretter(rng):
    """Senkrechte Bretter 15–30 cm breit mit Fugen, leicht gewölbt, je Brett eigene Maserung und Farbe, Nägel."""
    x, y = raster()
    k = K()
    b = rng.uniform(0.15, 0.3, 10)
    n = max(4, int(round(k / b.mean())))
    b = rng.uniform(0.15, 0.3, n)
    b *= k / b.sum()
    kanten = np.concatenate([[0], np.cumsum(b)])
    brett = np.clip(np.searchsorted(kanten, x, side="right") - 1, 0, n - 1)
    t = (x - kanten[brett]) / b[brett]
    rand = np.minimum(x - kanten[brett], kanten[brett + 1] - x) + 0.002 * (fbm(61, [0.2, 0.05]) - 0.5)
    fuge = glatt(0.003, 0.006, rand)
    hoehe = np.zeros_like(x)
    linie = np.zeros_like(x)
    for i in range(n):                                                # je Brett eine eigene Maserung (anders verschoben)
        s = brett == i
        hh, ll, _ = maserung(x + rng.random() * 0.5, y + rng.random() * k, 62 + 10 * i, ring=rng.uniform(0.008, 0.016))
        hoehe[s], linie[s] = hh[s], ll[s]
    h = fuge * (0.004 * (1 - (2 * t - 1) ** 2) + 0.0012 * hoehe) - 0.012 * (1 - fuge)
    naegel = np.zeros_like(x)
    for i in range(n):
        for yy in (0.25 * k, 0.75 * k):
            for seite in (0.25, 0.75):
                cx = kanten[i] + b[i] * seite
                cy = yy + rng.normal(0, 0.02)
                d = np.hypot((x - cx + k / 2) % k - k / 2, (y - cy + k / 2) % k - k / 2)
                naegel = np.maximum(naegel, 1 - glatt(0.004, 0.007, d))
    h += 0.002 * naegel
    ton = palette(rng, n, [("#7a6a58", 3), ("#6e604e", 2), ("#867460", 2), ("#5e5244", 1)])[brett]
    farbe = mische(ton, ton * 0.7, 0.55 * linie)
    farbe = mische(farbe, srgb("#8a8478"), 0.3 * glatt(0.5, 0.85, fbm(63, [0.4, 0.1])))
    farbe = mische(farbe, srgb("#1a1410"), 0.9 * (1 - fuge))
    farbe = mische(farbe, srgb("#2a2420"), 0.9 * naegel)
    flaechennetz(h, farbe, 0.85 - 0.3 * naegel)
    return 0.5, 1.0


def dachziegel(rng):
    """Biberschwanz, nur die Spalten: Die Reihen hat das Modell als Form, der Shader versetzt die Spalten je Reihe (Projektion
    „reihen“) – so wird aus jeder Spalte in jeder Reihe ein eigener Ziegel. Darum ist die Farbe längs der Spalte fest (sonst
    wechselte sie mitten im Ziegel) und unterscheidet sich von Spalte zu Spalte nur wenig (sonst Streifen bzw. Karos aus der
    Ferne). Gewölbt, feines Korn, kleine Flechten, Moos in den Stoßfugen; Laufspuren und Moosflecken über mehrere Reihen
    legt der Shader in Dachkoordinaten darüber."""
    x, y = raster()
    k = K()
    spalten = 7
    breite = k / spalten
    sp = np.clip((x // breite).astype(int), 0, spalten - 1)
    t = x / breite - sp
    rand = np.minimum(t, 1 - t) * breite + 0.002 * (fbm(71, [0.1, 0.03]) - 0.5)
    fuge = glatt(0.002, 0.006, rand)
    woelbung = np.sqrt(np.clip(1 - (2 * t - 1) ** 2, 0, 1))
    korn = rauschen(0.005, 73)
    h = fuge * (0.006 * woelbung + 0.0008 * korn) - 0.008 * (1 - fuge)
    ton = palette(rng, spalten, [("#94503a", 3), ("#8c4a34", 2), ("#9a563e", 2), ("#884832", 1.5), ("#7e4630", 1)])
    ton = ton / ton.mean(0) * np.array(srgb("#904c36")) * rng.uniform(0.95, 1.05, spalten)[:, None]
    farbe = ton[sp]
    farbe *= (0.93 + 0.12 * korn)[..., None]
    farbe *= (0.9 + 0.1 * woelbung)[..., None]
    farbe = mische(farbe, srgb("#8a6a58"), 0.12 * glatt(0.55, 0.9, fbm(74, [0.12, 0.04])))   # leicht ausgeblichen
    flechte = glatt(0.84, 0.9, fbm(75, [0.03, 0.01]))
    farbe = mische(farbe, srgb("#a8a07e"), 0.45 * flechte)
    moos = glatt(0.55, 0.8, fbm(76, [0.15, 0.05])) * (1 - glatt(0.003, 0.014, rand))
    farbe = mische(farbe, srgb("#4a5428"), 0.6 * moos)
    farbe = mische(farbe, srgb("#2a1a14"), 0.8 * (1 - fuge))
    flaechennetz(h + 0.001 * moos, farbe, 0.75 + 0.15 * flechte + 0.1 * moos)
    return 0.5, 1.0


def schiefer(rng):
    """Schiefer, nur die Spalten (versetzt wie beim Dachziegel): verschieden breite Stücke, spaltrauhe Oberfläche mit
    Schichtstufen, Farbe je Spalte fest und nah beieinander."""
    x, y = raster()
    k = K()
    b = rng.uniform(0.12, 0.24, 12)
    n = max(4, int(round(k / b.mean())))
    b = rng.uniform(0.12, 0.24, n)
    b *= k / b.sum()
    kanten = np.concatenate([[0], np.cumsum(b)])
    wx = x + 0.01 * (fbm(81, [0.1, 0.04]) - 0.5)
    sp = np.clip(np.searchsorted(kanten, wx, side="right") - 1, 0, n - 1)
    t = (wx - kanten[sp]) / b[sp]
    rand = np.minimum(wx - kanten[sp], kanten[sp + 1] - wx)
    fuge = glatt(0.002, 0.006, rand)
    schicht = np.floor(fbm(83, [0.08, 0.025]) * 6) / 6                 # Spaltstufen
    korn = rauschen(0.004, 84)
    h = fuge * (0.002 * schicht + 0.0006 * korn + 0.002 * t) - 0.006 * (1 - fuge)
    ton = palette(rng, n, [("#52565c", 3), ("#585c62", 2), ("#4c4e54", 2), ("#56534f", 1), ("#50575a", 1)])
    ton = ton / ton.mean(0) * np.array(srgb("#53565b")) * rng.uniform(0.95, 1.05, n)[:, None]
    farbe = ton[sp]
    farbe *= (0.92 + 0.12 * korn + 0.08 * schicht)[..., None]
    flechte = glatt(0.84, 0.91, fbm(85, [0.03, 0.01]))
    farbe = mische(farbe, srgb("#a8a89a"), 0.45 * flechte)
    farbe = mische(farbe, srgb("#1c1e20"), 0.8 * (1 - fuge))
    flaechennetz(h, farbe, 0.6 + 0.25 * korn + 0.15 * flechte)
    return 0.5, 1.0


def stroh(rng):
    """Strohdach (Reet), Halme in Fallrichtung (das Bild zeigt firstwärts nach oben): Strähnen, dunkle Lücken, vergraut."""
    x, y = raster()
    k = K()
    grund = fbm(91, [0.2, 0.06])
    h = 0.02 * grund
    flaechennetz(h, mische(srgb("#2a2216"), srgb("#3a3020"), grund), np.full(h.shape, 0.95))
    teile = Teile()
    stab = form_stab(4, 5, 0.06, 0.7)
    straehne = 9000                                                   # Strähnen: Bündel gleich liegender Halme
    sx, sy = rng.random(straehne) * k, rng.random(straehne) * k
    richtung = np.pi / 2 + rng.normal(0, 0.1, straehne)
    alter = np.clip(abtasten(fbm(92, [0.3, 0.1]), sx, sy) + rng.normal(0, 0.15, straehne), 0, 1)
    ton = mische(mische(srgb("#b49a62"), srgb("#8a7a5a"), alter), srgb("#6e6658"), 0.5 * alter ** 2)
    ton *= rng.uniform(0.8, 1.15, straehne)[:, None]
    je = rng.integers(10, 22, straehne)
    i = np.repeat(np.arange(straehne), je)
    n = len(i)
    quer = rng.normal(0, 0.012, n)
    laeng = rng.normal(0, 0.03, n)
    w = richtung[i]
    px = sx[i] + quer * np.sin(w) * -1 + laeng * np.cos(w)
    py = sy[i] + quer * np.cos(w) + laeng * np.sin(w)
    l = rng.uniform(0.18, 0.4, n)
    d = rng.uniform(0.003, 0.005, n)
    z = abtasten(h, px, py) + rng.uniform(0.0, 0.03, n) + 0.01
    farbe = ton[i] * rng.uniform(0.85, 1.12, n)[:, None]
    verteilen(teile, stab, np.stack([px, py, z], -1),
              drehung(w + rng.normal(0, 0.04, n), rng.random(n) * 6),             # flach, um die eigene Achse gedreht
              np.stack([l, d, d], -1), farbe * 0.7, farbe, 0.85, radius=l * 0.55)
    teile.objekt("halme")
    return 0.65, 0.9


def sandstein(rng):
    """Behauener Sandstein (Gewände, Eckquader, Gesimse): feines Korn, Schichtung, flache Hiebspuren, Eisenflecken, Patina."""
    x, y = raster()
    schicht = rauschen(0.08, 101, 8.0).T                              # liegende Bänder
    korn = rauschen(0.003, 102)
    poren = glatt(0.78, 0.92, rauschen(0.004, 103))
    m = K() / 0.012
    hieb = np.sin(2 * np.pi * (x * round(0.96 * m) + y * round(0.28 * m)) / K())
    hieb *= glatt(0.35, 0.75, fbm(104, [0.15, 0.05]))
    h = 0.0012 * schicht + 0.0006 * korn - 0.0008 * poren + 0.0004 * hieb + 0.003 * fbm(105, [0.3, 0.1])
    farbe = mische(srgb("#c4a47a"), srgb("#b08e66"), glatt(0.3, 0.8, schicht))
    farbe = mische(farbe, srgb("#d2b88e"), 0.3 * glatt(0.5, 0.9, fbm(106, [0.2, 0.06])))
    eisen = glatt(0.8, 0.92, fbm(107, [0.05, 0.015]))
    farbe = mische(farbe, srgb("#9a6a40"), 0.45 * eisen)
    farbe *= (0.92 + 0.12 * korn - 0.1 * poren)[..., None]
    patina = glatt(0.55, 0.9, streifen(0.25, 108, 6.0))
    farbe = mische(farbe, srgb("#6e6250"), 0.3 * patina)
    flaechennetz(h, farbe, 0.9)
    return 0.4, 0.9


def ziegelstein(rng):
    """Backstein im Läuferverband: Steine 29 × 7 cm, Fugen 1 cm zurückgesetzt, Farbe je Stein, einzelne Brandfarben."""
    x, y = raster()
    k = K()
    lagen = 15
    hoehen = np.full(lagen, k / lagen)
    # Läuferverband: jede zweite Lage um einen halben Stein versetzt
    lage = np.clip((y / (k / lagen)).astype(int), 0, lagen - 1)
    x2 = (x + (lage % 2) * k / 8) % k
    nummer, ex, ey, _, anzahl = verband(y, x2, hoehen, lambda j: np.full(4, k / 4), np.random.default_rng(111),
                                       versatz_zufall=False)
    rand = np.minimum(ex, ey) + 0.0015 * (fbm(112, [0.03, 0.01]) - 0.5)
    stein = glatt(0.005, 0.009, rand)
    korn = rauschen(0.004, 113)
    h = stein * (0.006 + 0.0015 * je_zelle(nummer, 114, anzahl) + 0.0006 * korn) - 0.003 * (1 - stein)
    h -= 0.002 * glatt(0.8, 0.95, rauschen(0.006, 115)) * stein
    ton = palette(rng, anzahl, [("#9a4a32", 3), ("#a85a3a", 2), ("#8a4030", 2), ("#b4684a", 1), ("#5a3028", 0.8),
                                ("#7a5040", 0.8)])[nummer]
    farbe = mische(srgb("#a49c8c"), ton * (0.9 + 0.2 * korn)[..., None], stein)
    farbe = mische(farbe, srgb("#4a3a30"), 0.2 * glatt(0.55, 0.9, streifen(0.3, 116, 6.0)))
    flaechennetz(h, farbe, 0.88)
    return 0.55, 1.0


# ─── Gruppe 2 (Kirche, Rathaus, Werkstätten): Metall, Schindeln, Leder, Tuch, gestrichenes Holz ───

def eisen(rng):
    """Geschmiedetes Eisen (Beschläge, Bänder, Gitter, Firstblech): Hammerschläge als flache Mulden mit Graten dazwischen,
    dunkler Zunder, Rost in Flecken und als Laufspur nach unten, feine Narben. Die Rauheit liegt im Mittel bei 0,9 wie bei
    allen Materialien (der Shader verschiebt sie auf die des Modells, Eisen 0,45): blanke Grate glatter, Rost rauer."""
    x, y = raster()
    wx, wy = bt.verzerrt(x, y, 0.004, [0.04, 0.012], 121)
    nummer, f1, f2, _, _ = zellen2(wx, wy, 22, 22, 122, 0.9)       # Hammerschläge, etwa 3,6 cm
    anzahl = 22 * 22
    tiefe = 0.25 + 0.75 * je_zelle(nummer, 123, anzahl)
    mulde = (f1 / (K() / 22)) ** 2                                    # 0 in der Mitte des Schlags, am Grat ≈ 1
    grat = 1 - glatt(0.0, 0.004, (f2 - f1) / 2)
    narben = glatt(0.72, 0.9, rauschen(0.003, 124))
    rost = glatt(0.7, 0.86, fbm(125, [0.08, 0.03, 0.01]))
    lauf = glatt(0.55, 0.85, streifen(0.08, 126, 9.0)) * glatt(0.3, 0.7, fbm(127, [0.25]))
    h = 0.0006 * tiefe * np.clip(mulde, 0, 1.5) + 0.0002 * grat - 0.0003 * narben + 0.0005 * rost * fbm(128, [0.01, 0.004])
    metall = mische(srgb("#3e3f43"), srgb("#2c2d31"), fbm(129, [0.2, 0.05]))
    metall *= (0.92 + 0.12 * (1 - np.clip(mulde, 0, 1)) * tiefe)[..., None]   # Mitte der Schläge etwas heller (blank)
    metall = mische(metall, srgb("#46484d"), 0.2 * grat)
    metall = mische(metall, srgb("#26283a"), 0.25 * glatt(0.6, 0.85, fbm(130, [0.06, 0.02])))   # bläulicher Zunder
    rostfarbe = mische(srgb("#6a3a22"), srgb("#7e4a2a"), fbm(131, [0.02, 0.006]))
    farbe = mische(metall, rostfarbe, 0.75 * rost)
    farbe = mische(farbe, srgb("#4e3426"), 0.25 * lauf * (1 - rost))
    farbe = mische(farbe, srgb("#1c1c1e"), 0.6 * narben)
    flaechennetz(h, farbe, 0.84 - 0.08 * grat + 0.14 * rost + 0.06 * lauf)
    return 0.45, 1.0


def kupfer(rng):
    """Kupferblech mit Patina (Dachreiter, Turmhelme, Ziertürme; auch gestrichene Blechspitzen): Bahnen von 60 cm mit
    stehenden Falzen in Fallrichtung, liegende Querfalze je Bahn versetzt, leicht gebeult. Patina fleckig, hellere
    Laufspuren vom Regen, dunkle Stellen unter den Falzen."""
    x, y = raster()
    k = K()
    bahnen = 2
    b = k / bahnen
    bahn = np.clip((x // b).astype(int), 0, bahnen - 1)
    t = x / b - bahn
    falz = 1 - glatt(0.004, 0.009, np.minimum(t, 1 - t) * b)          # stehender Falz, etwa 1,5 cm breit
    quer = np.zeros_like(x)
    for i in range(bahnen):                                           # Querfalze, je Bahn versetzt (3 je Kachel)
        s = bahn == i
        v = ((y[s] + i * k / 6) % (k / 3)) / (k / 3)
        quer[s] = 1 - glatt(0.0, 0.012, np.minimum(v, 1 - v))
    beule = fbm(141, [0.25, 0.08])
    hieb = fbm(142, [0.02, 0.008])
    h = 0.006 * falz + 0.0012 * quer + 0.0015 * beule + 0.0003 * hieb
    lauf = glatt(0.5, 0.85, streifen(0.12, 143, 8.0))
    flecken = fbm(144, [0.2, 0.06, 0.02])
    farbe = mische(srgb("#5c8a74"), srgb("#4e7a66"), glatt(0.3, 0.7, flecken))
    farbe = mische(farbe, srgb("#78a894"), 0.45 * lauf)                 # hellere Patina, wo das Wasser läuft
    farbe = mische(farbe, srgb("#4a5640"), 0.4 * glatt(0.72, 0.9, fbm(145, [0.08, 0.02])))   # dünne Patina, Kupfer schaut durch
    farbe = mische(farbe, srgb("#3a5244"), 0.5 * glatt(0.0, 1.0, quer) + 0.3 * falz)          # Schmutz an den Falzen
    farbe *= (0.95 + 0.08 * hieb)[..., None]
    flaechennetz(h, farbe, 0.9 - 0.06 * falz + 0.04 * lauf)
    return 0.5, 1.0


def holzschindeln(rng):
    """Holzschindeln, nur die Spalten (versetzt wie beim Dachziegel, Projektion „reihen“): Schindeln von 8–16 cm Breite,
    gespalten, Maserung längs, leicht gewölbt, Risse; vergrautes, morsches Holz mit Moos in den Fugen und Flechten. Farbe je
    Spalte fest und nah beieinander (die Reihen verschiebt der Shader)."""
    x, y = raster()
    k = K()
    b = rng.uniform(0.08, 0.16, 14)
    n = max(5, int(round(k / b.mean())))
    b = rng.uniform(0.08, 0.16, n)
    b *= k / b.sum()
    kanten = np.concatenate([[0], np.cumsum(b)])
    wx = x + 0.006 * (fbm(151, [0.1, 0.03]) - 0.5)
    sp = np.clip(np.searchsorted(kanten, wx, side="right") - 1, 0, n - 1)
    t = (wx - kanten[sp]) / b[sp]
    rand = np.minimum(wx - kanten[sp], kanten[sp + 1] - wx)
    fuge = glatt(0.002, 0.005, rand)
    hoehe = np.zeros_like(x)
    linie = np.zeros_like(x)
    for i in range(n):                                                # je Schindel eine eigene Maserung
        s = sp == i
        hh, ll, _ = maserung(x + rng.random() * 0.4, y + rng.random() * k, 152 + 11 * i, ring=rng.uniform(0.006, 0.012))
        hoehe[s], linie[s] = hh[s], ll[s]
    riss = np.zeros_like(x)
    for _ in range(14):                                               # Spaltrisse längs der Faser
        x0, y0 = rng.random() * k, rng.random() * k
        laenge = rng.uniform(0.1, 0.4)
        ddy = (y - y0 + k / 2) % k - k / 2
        ddx = (x - x0 + k / 2) % k - k / 2 - 0.003 * np.sin(ddy * 40)
        auf = np.clip(1 - np.abs(ddy) / (laenge / 2), 0, 1) ** 0.5
        riss = np.maximum(riss, (1 - glatt(0.0, 0.0015 * auf + 1e-5, np.abs(ddx))) * (auf > 0))
    h = fuge * (0.003 * (1 - (2 * t - 1) ** 2) + 0.0012 * hoehe - 0.002 * riss) - 0.006 * (1 - fuge)
    ton = palette(rng, n, [("#6e6252", 3), ("#665a4a", 2), ("#74685a", 2), ("#5e5446", 1)])
    ton = ton / ton.mean(0) * np.array(srgb("#6a5e4e")) * rng.uniform(0.95, 1.05, n)[:, None]
    farbe = ton[sp]
    farbe = mische(farbe, farbe * 0.72, 0.5 * linie)
    farbe *= (0.92 + 0.12 * hoehe)[..., None]
    farbe = mische(farbe, srgb("#8a867a"), 0.25 * glatt(0.5, 0.85, streifen(0.15, 153, 6.0)))   # vergraut
    flechte = glatt(0.84, 0.9, fbm(154, [0.03, 0.01]))
    farbe = mische(farbe, srgb("#a8a48a"), 0.4 * flechte)
    moos = glatt(0.5, 0.8, fbm(155, [0.12, 0.04])) * (1 - glatt(0.002, 0.012, rand))
    farbe = mische(farbe, srgb("#4a5428"), 0.6 * moos)
    farbe = mische(farbe, srgb("#1e1812"), 0.85 * np.maximum(1 - fuge, riss))
    flaechennetz(h + 0.0008 * moos, farbe, 0.88 + 0.06 * moos)
    return 0.5, 1.0


def leder(rng):
    """Leder (Blasebalg, Schurz, Sattel, Eimer): feines Narbenbild aus kleinen Buckeln, Knitterfalten, abgegriffene hellere
    Stellen und dunklere Flecken vom Fett."""
    x, y = raster()
    wx, wy = bt.verzerrt(x, y, 0.002, [0.02, 0.006], 161)
    nummer, f1, f2, _, _ = zellen2(wx, wy, 110, 110, 162, 0.9)        # Narben, etwa 5 mm
    narbe = glatt(0.0, 0.0012, (f2 - f1) / 2)
    falten = glatt(0.55, 0.9, rauschen(0.03, 163, 4.0)) * glatt(0.4, 0.7, fbm(164, [0.2]))   # Knitterfalten: kurz, quer
    griff = glatt(0.55, 0.85, fbm(166, [0.2, 0.06]))
    h = 0.00025 * narbe + 0.0003 * je_zelle(nummer, 167, 110 * 110) * narbe - 0.0006 * falten + 0.001 * fbm(168, [0.15])
    farbe = mische(srgb("#5e3e28"), srgb("#4c3020"), fbm(169, [0.1, 0.03]))
    farbe = mische(farbe, srgb("#7a5838"), 0.4 * griff)
    farbe = mische(farbe, srgb("#3a2618"), 0.35 * glatt(0.65, 0.88, fbm(170, [0.08, 0.025])))
    farbe *= (0.92 + 0.1 * narbe)[..., None]
    farbe = mische(farbe, srgb("#3a2618"), 0.25 * falten)
    flaechennetz(h, farbe, 0.92 - 0.12 * griff)
    return 0.4, 0.9


def tuch(rng):
    """Grobes Leinen (Lumpen, Planen, Säcke, Tücher): Leinwandbindung mit ungleich dicken Fäden (Noppen), Falten,
    Flecken. Die Fäden laufen ganzzahlig über die Kachel (nahtlos)."""
    x, y = raster()
    k = K()
    faeden = int(round(k / 0.0024))                                   # etwa 2,4 mm je Faden
    u, v = x * faeden / k, y * faeden / k
    i, j = np.floor(u).astype(int), np.floor(v).astype(int)
    fu, fv = u - i, v - j
    dick_kette = 0.75 + 0.5 * np.random.default_rng(171).random(faeden)[i % faeden]
    dick_schuss = 0.75 + 0.5 * np.random.default_rng(172).random(faeden)[j % faeden]
    noppen_k = glatt(0.7, 0.95, rauschen(0.02, 173, 5.0)) * 0.6       # Noppen: lang in Fadenrichtung
    noppen_s = glatt(0.7, 0.95, rauschen(0.02, 174, 0.2)) * 0.6
    oben = (i + j) % 2 == 0                                           # Kette oben, sonst Schuss oben
    kette = np.sin(np.pi * fu) ** 0.7 * (dick_kette + noppen_k)
    schuss = np.sin(np.pi * fv) ** 0.7 * (dick_schuss + noppen_s)
    gewebe = np.where(oben, np.maximum(kette, 0.6 * schuss), np.maximum(schuss, 0.6 * kette))
    falten = fbm(175, [0.2, 0.07])
    h = 0.00035 * gewebe + 0.004 * falten
    farbe = mische(srgb("#b4a688"), srgb("#a29276"), fbm(176, [0.08, 0.02]))
    faden = np.where(oben, np.random.default_rng(177).random(faeden)[i % faeden],
                     np.random.default_rng(178).random(faeden)[j % faeden])
    farbe *= (0.86 + 0.16 * faden + 0.1 * (gewebe - 0.5))[..., None]
    farbe = mische(farbe, srgb("#7a6c56"), 0.35 * glatt(0.68, 0.9, fbm(179, [0.06, 0.02])))   # Flecken
    farbe = mische(farbe, srgb("#4a4236"), 0.3 * (1 - glatt(0.2, 0.6, gewebe)))                # Lücken im Gewebe
    flaechennetz(h, farbe, 0.95)
    return 0.4, 0.9


def farbholz(rng):
    """Gestrichene Bretter (Hausfarbe, Stadtfarben: Läden, Türen, Tore, Wappen): senkrechte Bretter von 10–18 cm mit Fugen,
    darüber deckende Farbe – die Maserung zeichnet sich nur schwach ab, Pinselstriche, Haarrisse; an Kanten und in Flecken
    abgeblättert (dunkleres, vergrautes Holz). Die Farbe selbst gibt das Modell (das Spiel färbt um)."""
    x, y = raster()
    k = K()
    b = rng.uniform(0.1, 0.18, 12)
    n = max(4, int(round(k / b.mean())))
    b = rng.uniform(0.1, 0.18, n)
    b *= k / b.sum()
    kanten = np.concatenate([[0], np.cumsum(b)])
    brett = np.clip(np.searchsorted(kanten, x, side="right") - 1, 0, n - 1)
    t = (x - kanten[brett]) / b[brett]
    rand = np.minimum(x - kanten[brett], kanten[brett + 1] - x) + 0.0015 * (fbm(181, [0.2, 0.05]) - 0.5)
    fuge = glatt(0.002, 0.005, rand)
    hoehe = np.zeros_like(x)
    for i in range(n):
        s = brett == i
        hh, _, _ = maserung(x + rng.random() * 0.5, y + rng.random() * k, 182 + 10 * i, ring=rng.uniform(0.008, 0.014))
        hoehe[s] = hh[s]
    pinsel = streifen(0.006, 183, 25.0)
    ab = glatt(0.8, 0.88, fbm(184, [0.1, 0.03, 0.01]) + 0.12 * (1 - glatt(0.004, 0.02, rand)))   # abgeblättert
    haar = (1 - glatt(0.0, 0.01, np.abs(rauschen(0.05, 185, 0.15) - 0.5))) * glatt(0.5, 0.8, fbm(188, [0.15]))   # Haarrisse
    h = fuge * (0.002 * (1 - (2 * t - 1) ** 2) + 0.0004 * hoehe + 0.00012 * pinsel - 0.0003 * ab) - 0.008 * (1 - fuge)
    farbe = np.ones(x.shape + (3,)) * np.array(srgb("#b8b4ac"))
    farbe *= (0.95 + 0.06 * hoehe + 0.04 * pinsel)[..., None]
    farbe *= (0.97 + 0.06 * je_zelle(brett, 186, n))[..., None]       # je Brett etwas anders ausgeblichen
    farbe = mische(farbe, srgb("#c8c4bc"), 0.25 * glatt(0.5, 0.85, streifen(0.25, 187, 5.0)))
    holz = mische(srgb("#706658"), srgb("#5a5246"), hoehe)
    farbe = mische(farbe, holz, 0.6 * ab)
    farbe = mische(farbe, srgb("#5a564e"), 0.15 * haar * (1 - ab))
    farbe = mische(farbe, srgb("#1a1612"), 0.9 * (1 - fuge))
    flaechennetz(h, farbe, 0.82 + 0.14 * ab)
    return 0.45, 0.9


# ─── Gruppe 3 (Umland, Natur, Wasserbauten): Rinde, Fels, waagerechte Planken, Laub, Nadeln ───

def rinde(rng):
    """Borke (Stämme, Äste, Stümpfe, gefällte Stämme): längs aufgerissene Platten zwischen tiefen Furchen (senkrecht, leicht
    gewunden), quer gesprungen, dazu Flechten und Moos in den Furchen. Graubraun – den Ton gibt die Modellfarbe."""
    x, y = raster()
    k = K()
    m = round(k / 0.07)                                               # Furchen etwa alle 7 cm, ganzzahlig über die Kachel
    u = (x + 0.09 * (fbm(191, [0.4, 0.12]) - 0.5) + 0.05 * (streifen(0.06, 192, 5.0) - 0.5)) * m / k
    breit = streifen(0.04, 193, 4.0)                                  # Platten mal breit, mal schmal, Furchen laufen zusammen
    leiste = np.abs(np.sin(np.pi * u)) * (0.4 + 1.2 * breit) + 0.35 * (streifen(0.015, 189, 8.0) - 0.5)
    platte = glatt(0.25, 0.5, leiste)
    quer = glatt(0.72, 0.9, rauschen(0.02, 194, 0.12))               # Querrisse: kurz, waagerecht
    fasern = streifen(0.004, 195, 12.0)
    buckel = streifen(0.03, 196, 3.0)
    h = platte * (0.006 + 0.004 * buckel + 0.0012 * fasern - 0.003 * quer) - 0.006 * (1 - platte)
    h += 0.0015 * fbm(197, [0.1, 0.03])
    ton = mische(srgb("#544a40"), srgb("#6e6252"), glatt(0.3, 0.8, fbm(190, [0.15, 0.05])))
    farbe = ton * (0.86 + 0.2 * fasern + 0.08 * buckel)[..., None]
    farbe = mische(farbe, srgb("#8a8274"), 0.3 * platte * glatt(0.55, 0.85, fbm(198, [0.08, 0.03])))   # helle Kanten
    farbe = mische(farbe, srgb("#241c16"), 0.85 * (1 - platte))
    farbe = mische(farbe, srgb("#2e261e"), 0.5 * quer * platte)
    flechte = glatt(0.8, 0.9, fbm(199, [0.04, 0.012])) * platte
    farbe = mische(farbe, srgb("#9a9c84"), 0.45 * flechte)
    moos = glatt(0.55, 0.8, fbm(200, [0.2, 0.06])) * (1 - glatt(0.1, 0.35, leiste))
    farbe = mische(farbe, srgb("#4a5a28"), 0.6 * moos)
    flaechennetz(h + 0.0008 * flechte, farbe, 0.92)
    return 0.6, 1.0


def fels(rng):
    """Gewachsener Fels (Felsbrocken, Felsberg, Bruchwand): dieselbe Szene wie der Fels der Bodentexturen – Platten mit
    Rissen und Grus, Flechten, Moos und Rost –, hier als Kachel von 6 m (Platten um 1 m) an Wänden und Brocken."""
    return bt.fels(rng)


def planken(rng):
    """Waagerechte Planken (Rümpfe der Schiffe, Kähne und der Fähre, Blockwände der Hütten): Gänge von 18–30 cm, gestoßen
    und versetzt, Maserung längs, Fugen mit Pech gedichtet, Holznägel in Reihen, Kanten ausgewaschen. Den Ton gibt das Modell
    (Teerholz dunkel, Blockholz grau)."""
    x, y = raster()
    k = K()
    hoehen = rng.uniform(0.18, 0.3, 12)
    n = max(4, int(round(k / hoehen.mean())))
    hoehen = rng.uniform(0.18, 0.3, n)
    hoehen *= k / hoehen.sum()

    def laengen(j):
        l = rng.uniform(0.9, 1.6, 4)
        m = max(1, int(round(k / l.mean())))
        l = rng.uniform(0.9, 1.6, m)
        return l * k / l.sum()

    wy = y + 0.002 * (fbm(201, [0.3, 0.1]) - 0.5)
    nummer, ex, ey, lage, anzahl = verband(wy, x, hoehen, laengen, rng)
    grenzen = np.concatenate([[0], np.cumsum(hoehen)])
    t = np.clip((wy - grenzen[lage]) / hoehen[lage], 0, 1)
    fuge = glatt(0.002, 0.005, ey) * glatt(0.0015, 0.004, ex)
    hoehe = np.zeros_like(x)
    linie = np.zeros_like(x)
    for i in range(anzahl):                                           # je Planke eine eigene Maserung, längs (x)
        s = nummer == i
        if s.any():
            hh, ll, _ = maserung(y + rng.random() * 0.5, x + rng.random() * k, 202 + 7 * i, ring=rng.uniform(0.008, 0.014))
            hoehe[s], linie[s] = hh[s], ll[s]
    naegel = np.zeros_like(x)
    for j in range(len(hoehen)):                                      # Holznägel: je Planke eine Reihe, alle 0,5 m
        cy = grenzen[j] + hoehen[j] * 0.5
        for cx in np.arange(rng.random() * 0.5, k, 0.5):
            d = np.hypot((x - cx + k / 2) % k - k / 2, (y - cy + k / 2) % k - k / 2)
            naegel = np.maximum(naegel, 1 - glatt(0.006, 0.009, d))
    h = fuge * (0.003 * (1 - (2 * t - 1) ** 2) + 0.0012 * hoehe + 0.0006 * naegel) - 0.006 * (1 - fuge)
    ton = palette(rng, anzahl, [("#6a5c4c", 3), ("#625444", 2), ("#74665a", 2), ("#584c40", 1)])[nummer]
    farbe = mische(ton, ton * 0.7, 0.5 * linie)
    farbe *= (0.9 + 0.14 * hoehe)[..., None]
    farbe = mische(farbe, srgb("#8a8276"), 0.25 * (1 - glatt(0.004, 0.03, np.minimum(ey, ex))) * fuge)   # Kanten
    farbe = mische(farbe, srgb("#3a3026"), 0.3 * glatt(0.5, 0.85, streifen(0.2, 203, 6.0).T))        # Flecken längs
    farbe = mische(farbe, srgb("#3a3228"), 0.6 * naegel)
    farbe = mische(farbe, srgb("#141008"), 0.9 * (1 - fuge))
    flaechennetz(h, farbe, 0.85 - 0.1 * (1 - fuge))
    return 0.5, 1.0


def laub(rng):
    """Laub (Kronen der Laubbäume, Büsche, Hecken): Blätter in runden Büscheln von 15–30 cm, oben im Licht, an den Rändern und
    darunter im Schatten, dazwischen dunkle Lücken – aus Spielhöhe die gemalten Tupfen einer Krone. Den Ton gibt das Modell
    (das Spiel färbt je Jahreszeit um); die Farbtöne der Blätter liegen darum nah beieinander."""
    x, y = raster()
    k = K()
    h = 0.02 * fbm(211, [0.3, 0.1])
    flaechennetz(h, mische(srgb("#10160a"), srgb("#1a2410"), fbm(212, [0.2, 0.06])), np.full(h.shape, 0.9))
    teile = Teile()
    formen = [form_blatt(1.0, rng.uniform(0.5, 0.7), 4, 10, rng.uniform(0.1, 0.2), 0.12, 220 + i) for i in range(8)]
    farben = np.array([srgb(c) for c in ("#56702e", "#4a6428", "#62783a", "#506a2c", "#6a7e3a", "#445c26")])
    buschel = 110                                                     # Büschel: Mitte, Radius, Höhe
    bx, by = rng.random(buschel) * k, rng.random(buschel) * k
    br = rng.uniform(0.08, 0.15, buschel)
    bz = rng.uniform(0.0, 0.06, buschel)
    je = (br / 0.08) ** 2 * 260
    i = np.repeat(np.arange(buschel), je.astype(int))
    n = len(i)
    w = rng.random(n) * 2 * np.pi
    r = np.sqrt(rng.random(n)) * br[i]
    q = r / br[i]                                                     # 0 Mitte … 1 Rand
    px, py = (bx[i] + np.cos(w) * r) % k, (by[i] + np.sin(w) * r) % k
    z = bz[i] + 0.06 * br[i] / 0.15 * np.sqrt(np.clip(1 - q * q, 0, 1)) + 0.01  # gewölbt: Kuppe in der Mitte
    l = rng.uniform(0.045, 0.075, n)
    oben = np.clip(0.5 + 0.5 * np.sin(w) * q, 0, 1)                   # oben im Bild = oben am Baum: die Oberseite im Licht
    basis = farben[rng.integers(0, len(farben), n)] * rng.uniform(0.85, 1.1, n)[:, None]
    basis = mische(basis, srgb("#7e8c44"), 0.35 * oben * (1 - q))
    basis = mische(basis, srgb("#1e2a10"), 0.45 * q ** 2 * (1 - oben))
    wahl = rng.integers(0, len(formen), n)
    for j, form in enumerate(formen):
        s_ = wahl == j
        m = s_.sum()
        rot = drehung(w[s_] + rng.normal(0, 0.6, m), 0.9 * q[s_] + rng.normal(0, 0.3, m), rng.normal(0, 0.3, m))
        verteilen(teile, form, np.stack([px[s_], py[s_], z[s_]], -1), rot, np.stack([l[s_], l[s_], l[s_]], -1),
                  basis[s_], basis[s_] * 0.8, 0.75)
    teile.objekt("blaetter")
    return 0.75, 0.9


def nadeln(rng):
    """Nadelzweige (Tannen, Wacholder): fächerförmige Zweige von 20–35 cm, die schräg nach unten hängen (oben im Bild ist zur
    Spitze des Baums), dicht mit Nadeln besetzt; jeder Zweig oben im Licht und an der Spitze heller, darunter der Schatten des
    nächsten – aus Spielhöhe die gestuften Schuppen einer gemalten Tanne. Den Ton gibt das Modell."""
    x, y = raster()
    k = K()
    h = 0.02 * fbm(231, [0.3, 0.1])
    flaechennetz(h, mische(srgb("#0a0f08"), srgb("#141c10"), fbm(232, [0.2, 0.06])), np.full(h.shape, 0.9))
    teile = Teile()
    zweige = 90                                                       # Zweige: Ansatz oben, fächern nach unten
    zx, zy = rng.random(zweige) * k, rng.random(zweige) * k
    zl = rng.uniform(0.2, 0.35, zweige)
    zz = rng.uniform(0.0, 0.06, zweige)
    ton = np.array([srgb(c) for c in ("#2e4a26", "#36522a", "#2a4222", "#3e5a2e", "#324c2a")])[rng.integers(0, 5, zweige)]
    triebe = 14
    j = np.repeat(np.arange(zweige), triebe)                          # je Zweig Seitentriebe, fächerförmig
    tw = -np.pi / 2 + rng.uniform(-0.75, 0.75, len(j))
    tl = zl[j] * rng.uniform(0.6, 1.0, len(j))
    i = np.repeat(np.arange(len(j)), (tl / 0.005).astype(int))
    n = len(i)
    s = rng.random(n)                                                 # Stelle längs des Triebs
    wn = tw[i] + np.where(rng.random(n) < 0.5, -1.0, 1.0) * rng.uniform(0.6, 1.1, n)
    nl = rng.uniform(0.014, 0.022, n) * (1 - 0.3 * s)
    px = (zx[j[i]] + np.cos(tw[i]) * tl[i] * s + np.cos(wn) * nl * 0.5) % k
    py = (zy[j[i]] + np.sin(tw[i]) * tl[i] * s + np.sin(wn) * nl * 0.5) % k
    z = zz[j[i]] + 0.05 * (1 - s) ** 0.7 + 0.004 * rng.random(n)     # am Ansatz hoch, die Spitzen hängen
    farbe = ton[j[i]] * rng.uniform(0.85, 1.12, n)[:, None]
    farbe = mische(farbe, srgb("#6a8240"), 0.4 * s ** 2)              # frische Triebe an der Spitze heller
    farbe = mische(farbe, srgb("#5a7234"), 0.3 * (1 - s))                    # am Ansatz oben im Licht
    farbe = mische(farbe, srgb("#0e1610"), 0.45 * np.abs(np.cos(tw[i])) * s)                         # Fächerränder dunkler
    d = rng.uniform(0.0016, 0.0024, n)
    verteilen(teile, form_stab(2, 3, 0.04), np.stack([px, py, z], -1), drehung(wn, rng.normal(0, 0.25, n)),
              np.stack([nl, d, d], -1), farbe * 0.85, farbe, 0.8)
    mx, my = (zx[j] + np.cos(tw) * tl / 2) % k, (zy[j] + np.sin(tw) * tl / 2) % k   # die Triebe selbst, braun
    zd = rng.uniform(0.0025, 0.004, len(j))
    zf = np.repeat([srgb("#3a2c20")], len(j), 0)
    verteilen(teile, form_stab(3, 4, 0.03), np.stack([mx, my, zz[j] + 0.025 - 0.004], -1), drehung(tw, np.zeros(len(j))),
              np.stack([tl, zd, zd], -1), zf, zf, 0.85)
    teile.objekt("nadeln")
    return 0.75, 0.9


MATERIALIEN = {"bruchstein": bruchstein, "quader": quader, "kalkputz": kalkputz, "lehmputz": lehmputz,
               "fachwerkholz": fachwerkholz, "bretter": bretter, "dachziegel": dachziegel, "schiefer": schiefer,
               "stroh": stroh, "sandstein": sandstein, "ziegelstein": ziegelstein, "eisen": eisen, "kupfer": kupfer,
               "holzschindeln": holzschindeln, "leder": leder, "tuch": tuch, "farbholz": farbholz,
               "rinde": rinde, "fels": fels, "planken": planken, "laub": laub, "nadeln": nadeln}


# ═════════════════════════════════════════════════════════════════════════════
#  Backen, Aufbereiten, Packen
# ═════════════════════════════════════════════════════════════════════════════

def backe(name):
    t0 = time.time()
    bt.K = KACHEL[name]
    rng = np.random.default_rng(NAMEN.index(name) * 1000 + 11)
    geraet = bt.neue_szene(0.04)
    if geraet != "GPU":
        raise SystemExit("Cycles fand keine CUDA-Karte – nicht auf dem Prozessor backen (zu langsam), später erneut versuchen.")
    ao_anteil, n_staerke = MATERIALIEN[name](rng)
    dreiecke = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
    t1 = time.time()
    d = bt.backen(name)
    roh = {"albedo": kanal(d, "Diffuse Color"), "normale": kanal(d, "Normal", "XYZ"),
           "ao": kanal(d, "Ambient Occlusion", "R")[..., 0], "hoehe": kanal(d, "hoehe", "X")[..., 0],
           "rau": kanal(d, "rauheit", "X")[..., 0], "ao_anteil": ao_anteil, "n_staerke": n_staerke}
    np.savez(os.path.join(ZWISCHEN, f"{name}_{N}.npz"), **roh)
    print(f"[{name}] {geraet}, {dreiecke / 1e6:.1f} Mio. Polygone, Aufbau {t1 - t0:.0f} s, Backen {time.time() - t1:.0f} s",
          flush=True)


def aufbereiten(roh):
    albedo, normale, ao, hoehe, rau = (roh[k] for k in ("albedo", "normale", "ao", "hoehe", "rau"))
    ao_anteil, n_staerke = float(roh["ao_anteil"]), float(roh["n_staerke"])
    lin = np.clip(albedo * (1 - ao_anteil * (1 - ao[..., None])), 0, 1)
    farbe = bt.nach_srgb(lin)
    lo, hi = np.percentile(hoehe, [0.5, 99.5])
    h = np.clip((hoehe - lo) / (hi - lo + 1e-9), 0, 1)
    n = normale.copy()
    n[n[..., 2] < 0] *= -1
    n[..., :2] *= n_staerke
    n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-9
    return farbe, h, n, np.clip(rau, 0, 1)


def linear(c8):
    """8-bit-sRGB → linear (so liest die Grafikkarte die Textur mit source_color)."""
    c = c8 / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


WEBP_GRENZE = 16383   # größte Kantenlänge eines WebP-Bildes


def gitter():
    """Spalten und Zeilen, in denen die Schichten im Bild liegen: untereinander, solange das Bild nicht höher wird als ein WebP
    sein darf, sonst in mehreren Spalten (Zeile für Zeile, wie Godot die Slices liest: slices/horizontal und slices/vertical
    in den .import-Dateien müssen dazu passen). Freie Plätze am Ende bleiben schwarz."""
    spalten = 1
    while -(-len(NAMEN) // spalten) * N > WEBP_GRENZE:
        spalten += 1
    return spalten, -(-len(NAMEN) // spalten)


def zusammenlegen(schichten):
    """Die Schichten (je N × N) als ein Bild im Gitter (siehe gitter)."""
    spalten, zeilen = gitter()
    leer = np.zeros_like(schichten[0])
    voll = list(schichten) + [leer] * (spalten * zeilen - len(schichten))
    return np.vstack([np.hstack(voll[z * spalten:(z + 1) * spalten]) for z in range(zeilen)])


def packen():
    farben, normalen, vorschau, fehlt, kanaele, mittel = [], [], [], [], [], {}
    for name in NAMEN:
        pfad = os.path.join(ZWISCHEN, f"{name}_{N}.npz")
        if not os.path.exists(pfad):
            fehlt.append(name)
            vorschau.append(np.full((N, N, 3), 0.12))
            continue
        farbe, h, n, rau = aufbereiten(dict(np.load(pfad)))
        f8 = np.dstack([np.round(farbe * 255), 1 + np.round(h * 254)]).astype(np.uint8)
        mittel[name] = linear(f8[..., :3].astype(float)).reshape(-1, 3).mean(0)
        print(f"[{name}] Mittel sRGB {np.round(farbe.reshape(-1, 3).mean(0) * 255, 1)}, linear {np.round(mittel[name], 4)}"
              f" | Höhe {h.mean():.2f} ± {h.std():.2f} | Rauheit {rau.mean():.2f}")
        farben.append(f8)
        normalen.append(np.dstack([np.round((n[..., 0] * 0.5 + 0.5) * 255), np.round((n[..., 1] * 0.5 + 0.5) * 255),
                                   np.round(rau * 255)]).astype(np.uint8))
        vorschau.append(bt.beleuchtet(farbe, n, h))
        a = N // 4
        stueck = lambda b: np.roll(b, (a // 2, a // 2), (0, 1))[:a, :a]
        kanaele.append((name, np.hstack([stueck(farbe), stueck(np.dstack([h] * 3)), stueck(n * 0.5 + 0.5),
                                         stueck(np.dstack([rau] * 3))])))
    if fehlt or N != 1024:
        print("nichts geschrieben:", f"es fehlen {', '.join(fehlt)}" if fehlt else "Probegröße")
    else:
        os.makedirs(ZIEL, exist_ok=True)
        for datei, schichten, qualitaet in (("modelle_farbe_hoehe", farben, 95), ("modelle_normal_rauheit", normalen, 100)):
            pfad = os.path.join(ZIEL, datei + ".webp")
            groesst, mittel_abw = bt.webp(pfad, zusammenlegen(schichten), qualitaet)
            print(f"  WebP q{qualitaet}: Abweichung je Kanal höchstens {groesst}, im Mittel {mittel_abw.round(2)}")
            print("geschrieben:", os.path.relpath(pfad, WURZEL), f"({os.path.getsize(pfad) / 1e6:.1f} MB)")
        # Die mittlere Farbe aus der geschriebenen (verlustbehafteten) Datei nachmessen
        img = bpy.data.images.load(os.path.join(ZIEL, "modelle_farbe_hoehe.webp"))
        img.colorspace_settings.name = "Non-Color"
        w, hh = img.size
        p = np.empty(w * hh * 4, np.float32)
        img.pixels.foreach_get(p)
        p = np.round(p.reshape(hh, w, 4)[::-1, :, :3] * 255)
        spalten, zeilen = gitter()
        print(f"  Gitter {spalten} × {zeilen} (in den .import-Dateien slices/horizontal={spalten}, slices/vertical={zeilen})")
        for i, name in enumerate(NAMEN):
            z, sp = divmod(i, spalten)
            mittel[name] = linear(p[z * N:(z + 1) * N, sp * N:(sp + 1) * N]).reshape(-1, 3).mean(0)
        daten = json.load(open(DATEN, encoding="utf-8")) if os.path.exists(DATEN) else {}
        daten["schichten"] = [{"name": name, "kachel": groesse, "projektion": projektion,
                               "mittel": [round(float(c), 5) for c in mittel[name]], **({"reihe": REIHE[name]} if name in REIHE else {})}
                              for name, groesse, projektion in SCHICHTEN]
        schreiben_json(daten)
    ziel = VORSCHAU if N == 1024 else os.path.join(ZWISCHEN, "vorschau")
    os.makedirs(ziel, exist_ok=True)
    kacheln = []
    for name, v in zip(NAMEN, vorschau):
        gekachelt = np.vstack([np.hstack([v, v])] * 2)
        bt.jpeg(os.path.join(ziel, f"{name}.jpg"), bt.verkleinern(gekachelt, 2) if N == 1024 else gekachelt)
        kacheln.append(bt.verkleinern(gekachelt, max(1, gekachelt.shape[0] // 512)))
    if "nah" in _opt:
        os.makedirs(os.path.join(ZWISCHEN, "vorschau"), exist_ok=True)
        for name, bild in kanaele:
            bt.jpeg(os.path.join(ZWISCHEN, "vorschau", f"{name}_kanaele.jpg"), bild)
        for name, v in zip(NAMEN, vorschau):
            bt.jpeg(os.path.join(ZWISCHEN, "vorschau", f"{name}_nah.jpg"), np.roll(v, (N // 4, N // 4), (0, 1))[:N // 2, :N // 2])
    leer = np.full_like(kacheln[0], 0.12)
    reihen = [np.hstack((kacheln + [leer] * 4)[r * 4:(r + 1) * 4]) for r in range((len(kacheln) + 3) // 4)]
    bt.jpeg(os.path.join(ziel, "uebersicht.jpg"), np.vstack(reihen))
    print("Vorschau:", ziel)


def schreiben_json(daten):
    """materialbibliothek.json: der Hinweis oben, dann „schichten“ (je Zeile eine) und „namen“ (von Hand gepflegt)."""
    zeilen = ["{"]
    teile = []
    if "_hinweis" in daten:
        teile.append(f'  "_hinweis": {json.dumps(daten["_hinweis"], ensure_ascii=False)}')
    teile.append('  "schichten": [\n' + ",\n".join("    " + json.dumps(s, ensure_ascii=False) for s in daten["schichten"]) + "\n  ]")
    if "namen" in daten:
        teile.append('  "namen": {\n' + ",\n".join(f"    {json.dumps(k, ensure_ascii=False)}: {json.dumps(v, ensure_ascii=False)}"
                                                    for k, v in daten["namen"].items()) + "\n  }")
    zeilen.append(",\n".join(teile))
    zeilen.append("}")
    with open(DATEN, "w", encoding="utf-8") as f:
        f.write("\n".join(zeilen) + "\n")
    print("geschrieben:", os.path.relpath(DATEN, WURZEL))


def main():
    os.makedirs(ZWISCHEN, exist_ok=True)
    if "nur-packen" not in _opt:
        for name in _opt.get("nur", ",".join(NAMEN)).split(","):
            backe(name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    packen()


main()
