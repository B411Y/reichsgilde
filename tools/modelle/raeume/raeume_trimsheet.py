"""Trim-Sheet der Raeume im Wohnhaus (Hinterzimmer, Bibliothek): raeume_innen_trim_{albedo,normal,orm}.png, 2048 x 2048.

    blender -b --factory-startup --python tools/modelle/raeume/raeume_trimsheet.py -- --quellen=<ordner mit Poly-Haven-Texturen>

Wie tools/modelle/neubau/trimsheet.py (dessen Werkzeuge hier benutzt werden): Baender quer ueber das Blatt, jedes waagerecht
nahtlos, umgefaerbt auf eine Zielfarbe, die Zeichnung der Aufnahme bleibt. Quellen: fremde CC0-Texturen von Poly Haven (2K-JPG:
Farbe, Normale OpenGL, ARM), je ein Ordner <name>/<name>_{diff,nor_gl,arm}_2k.jpg; sie kommen nicht ins Repository
(Herkunft: game/assets/texturen/neubau/HERKUNFT.md). Selbst gerechnet (in diesem Skript, keine Bild-KI): das Muster des
Teppichs, die Buchruecken, die Landkarte und die Schriftzeilen auf dem Pergament.

Die Baender stehen in BAENDER_RAEUME; die Raumskripte (raum_hinterzimmer.py, raum_bibliothek.py) lesen sie von hier.
"""
import math
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "neubau"))
from trimsheet import N, Blatt, Quelle, faerbe, linear_zu_srgb, srgb_zu_linear   # noqa: E402  (trimsheet braucht bpy: laeuft in Blender)

# (v0, v1, Meter je UV.x 0..1): Holz und Stein laufen entlang UV.x, quer fuellt die Flaeche das Band
BAENDER_RAEUME = {
    "dielen":     (0.00, 0.10, 4.0),   # alte dunkle Bohlen (Hinterzimmer: Boden, Kisten, Regalbretter)
    "parkett":    (0.10, 0.18, 4.0),   # warme Eichendielen (Bibliothek: Boden)
    "moebel":     (0.18, 0.28, 2.0),   # geoeltes Moebelholz (Regale, Schreibtisch, Pult, Taefelung, Stuehle)
    "balken":     (0.28, 0.35, 4.0),   # rohes Holz: Fachwerk, Deckenbalken, Faesser
    "putz":       (0.35, 0.43, 8.0),   # Kalkputz der Gefache
    "bruchstein": (0.43, 0.51, 4.0),   # graue Bruchsteinmauer (Hinterzimmer, Ofen)
    "quader":     (0.51, 0.57, 4.0),   # Sandstein: Kamin, Fensterbank
    "teppich":    (0.57, 0.65, 2.0),   # roter Teppich mit Borte (quer: Borte, Feld, Borte)
    "buecher":    (0.65, 0.75, 1.0),   # Buchruecken nebeneinander (quer: von unten bis oben ein Buch)
    "leder":      (0.75, 0.79, 2.0),   # rotbraunes Leder: Sessel, Einbaende
    "sackleinen": (0.79, 0.83, 2.0),   # Saecke
    "papier":     (0.83, 0.95, 1.0),   # Pergament: u 0..0,25 Landkarte, 0,25..0,5 beschriebenes Blatt, 0,5..1 leer
    "kleinteile": (0.95, 1.00, 1.0),   # Farbfelder nebeneinander, siehe FELDER_RAEUME
}
FELDER_RAEUME = ["eisen", "stahl", "messing", "gold", "wachs", "tinte", "glut", "glas", "wein", "siegel"]
KARTE_U = (0.0, 0.25)          # die Landkarte im Band "papier"
SCHRIFT_U = (0.25, 0.5)        # beschriebenes Blatt (Brief, aufgeschlagenes Buch)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Gemalte Teile (Rechnung, keine Bild-KI)
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def stempel(bild, maske, punkte, dicke, farbe, deckung=1.0):
    """Linienzug in ein Feld [Zeile, Spalte, 3] (sRGB 0..1) stempeln; `maske` sammelt, wo Farbe liegt (fuer Normale und Rauheit)."""
    h, b = bild.shape[:2]
    r = max(dicke / 2, 0.6)
    k = int(math.ceil(r)) + 1
    yy, xx = np.mgrid[-k:k + 1, -k:k + 1]
    kern = np.clip(r + 0.5 - np.sqrt(xx * xx + yy * yy), 0, 1) * deckung
    f = np.array(farbe, dtype=np.float32)[None, None, :]
    for (x0, y0), (x1, y1) in zip(punkte[:-1], punkte[1:]):
        n = max(int(math.hypot(x1 - x0, y1 - y0) / max(r * 0.5, 0.5)), 1)
        for t in np.linspace(0, 1, n + 1):
            cx, cy = int(round(x0 + (x1 - x0) * t)), int(round(y0 + (y1 - y0) * t))
            ya, yb, xa, xb = cy - k, cy + k + 1, cx - k, cx + k + 1
            if ya < 0 or xa < 0 or yb > h or xb > b: continue
            g = np.maximum(kern, 0)[..., None]
            bild[ya:yb, xa:xb] = bild[ya:yb, xa:xb] * (1 - g) + f * g
            maske[ya:yb, xa:xb] = np.maximum(maske[ya:yb, xa:xb], kern)


def zufallsweg(z, start, schritte, laenge, drall=0.5, richtung=None):
    """Maeandernder Linienzug (Fluss, Weg) aus `schritte` Stuecken der Laenge `laenge`."""
    x, y = start
    w = z.uniform(0, 2 * math.pi) if richtung is None else richtung
    pts = [(x, y)]
    for _ in range(schritte):
        w += z.normal(0, drall)
        x += math.cos(w) * laenge; y += math.sin(w) * laenge
        pts.append((x, y))
    return pts


def innerhalb(vieleck, b, h):
    """Maske der Punkte innerhalb eines Vielecks (Strahlverfahren)."""
    yy, xx = np.mgrid[0:h, 0:b].astype(np.float32)
    drin = np.zeros((h, b), dtype=bool)
    for (x0, y0), (x1, y1) in zip(vieleck[:-1], vieleck[1:]):
        if y0 == y1: continue
        kreuzt = ((y0 > yy) != (y1 > yy)) & (xx < (x1 - x0) * (yy - y0) / (y1 - y0) + x0)
        drin ^= kreuzt
    return drin


def male_karte(feld, saat=7):
    """Landkarte auf das Pergament `feld` [h, b, 3]: Kueste mit Meer, zwei Fluesse, Wege, Staedte als rote Punkte, Windrose, Rahmen.
    Gibt die Tintenmaske zurueck."""
    h, b = feld.shape[:2]
    z = np.random.default_rng(saat)
    maske = np.zeros((h, b), dtype=np.float32)
    tinte = (0.17, 0.11, 0.07); rot = (0.58, 0.13, 0.08); meer = np.array((0.52, 0.56, 0.52), dtype=np.float32)
    mitte = (b * 0.40, h * 0.52)
    kueste = []
    for i in range(97):
        w = 2 * math.pi * i / 96
        r = min(b, h) * (0.40 + 0.06 * math.sin(3 * w + 1) + 0.035 * math.sin(7 * w + 2) + 0.012 * z.standard_normal())
        kueste.append((mitte[0] + r * math.cos(w) * 1.35, mitte[1] + r * math.sin(w)))
    kueste[-1] = kueste[0]
    land = innerhalb(kueste, b, h)
    feld[~land] = feld[~land] * 0.55 + meer * 0.45                     # Meer leicht graugruen laviert
    stempel(feld, maske, kueste, 2.2, tinte)
    for s in range(1, 4):                                              # Kuestenschraffur: drei feine Linien aussen
        aussen = [(mitte[0] + (x - mitte[0]) * (1 + 0.035 * s), mitte[1] + (y - mitte[1]) * (1 + 0.035 * s)) for x, y in kueste]
        stempel(feld, maske, aussen, 0.9, tinte, deckung=0.55 - 0.12 * s)
    for i in range(2):                                                 # Fluesse vom Inland zur Kueste
        weg = zufallsweg(z, (mitte[0] + z.uniform(-20, 20), mitte[1] + z.uniform(-15, 15)), 26, 5.5, drall=0.35, richtung=(0.3 if i else 2.6))
        stempel(feld, maske, weg, 1.6, (0.22, 0.28, 0.36))
    for i in range(3):                                                 # Wege gestrichelt
        weg = zufallsweg(z, (mitte[0] + z.uniform(-50, 50), mitte[1] + z.uniform(-30, 30)), 18, 6.0, drall=0.25)
        for j in range(0, len(weg) - 1, 2): stempel(feld, maske, weg[j:j + 2], 1.0, tinte, deckung=0.8)
    for i in range(6):                                                 # Staedte
        x, y = mitte[0] + z.uniform(-70, 70), mitte[1] + z.uniform(-40, 40)
        if 0 <= int(y) < h and 0 <= int(x) < b and land[int(y), int(x)]:
            stempel(feld, maske, [(x, y), (x + 0.1, y)], 6.0, rot)
            stempel(feld, maske, [(x - 4, y - 7), (x + 4, y - 7)], 1.0, tinte)
    wx, wy, wr = b * 0.86, h * 0.20, h * 0.11                           # Windrose
    for w in range(8):
        a = w * math.pi / 4; l = wr if w % 2 == 0 else wr * 0.55
        stempel(feld, maske, [(wx, wy), (wx + math.cos(a) * l, wy + math.sin(a) * l)], 1.2 if w % 2 == 0 else 0.8, tinte)
    stempel(feld, maske, [(wx, wy - wr), (wx, wy - wr - 0.1)], 4, rot)
    for x0, y0, x1, y1 in ((4, 4, b - 5, 4), (b - 5, 4, b - 5, h - 5), (b - 5, h - 5, 4, h - 5), (4, h - 5, 4, 4)):   # Rahmen
        stempel(feld, maske, [(x0, y0), (x1, y1)], 1.8, tinte)
    return maske


def male_schrift(feld, saat=11):
    """Schriftzeilen in Tinte (Brief, aufgeschlagenes Buch): Zeilen aus kurzen Worten, rote Initiale oben links."""
    h, b = feld.shape[:2]
    z = np.random.default_rng(saat)
    maske = np.zeros((h, b), dtype=np.float32)
    tinte = (0.12, 0.09, 0.07)
    zeile = 11.0
    y = 14.0
    while y < h - 10:
        x = 14.0 + (26 if y < 14 + 3 * zeile else 0)
        ende = b - 14 - z.uniform(0, 30)
        while x < ende:
            wort = z.uniform(8, 30)
            pts = [(x + i, y + 1.4 * math.sin(i * 1.7)) for i in np.arange(0, min(wort, ende - x), 2.0)]
            if len(pts) > 1: stempel(feld, maske, pts, 1.3, tinte, deckung=0.85)
            x += wort + z.uniform(4, 8)
        y += zeile
    stempel(feld, maske, [(20, 18), (30, 40)], 7, (0.58, 0.13, 0.08))
    return maske


def male_teppich(farbe):
    """Muster auf den Grund des Teppichs [h, N, 3]: Borten oben und unten (dunkel, Goldfaden), im Feld Rauten im Wechsel."""
    h = farbe.shape[0]
    y = np.arange(h, dtype=np.float32)[:, None]
    x = np.arange(N, dtype=np.float32)[None, :]
    lin = srgb_zu_linear(farbe)
    hell = (lin @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)) / max(float(lin.mean()), 1e-4)
    hell = np.clip(hell, 0.6, 1.4)[..., None]
    rot = srgb_zu_linear(np.array((0.50, 0.11, 0.09), dtype=np.float32))
    dunkel = srgb_zu_linear(np.array((0.14, 0.10, 0.12), dtype=np.float32))
    gold = srgb_zu_linear(np.array((0.70, 0.52, 0.24), dtype=np.float32))
    blau = srgb_zu_linear(np.array((0.18, 0.22, 0.34), dtype=np.float32))
    rand = np.minimum(y, h - 1 - y) / h                                        # 0 am Rand, 0,5 in der Mitte
    ziel = np.broadcast_to(rot, (h, N, 3)).copy()
    borte = rand < 0.17
    ziel[np.broadcast_to(borte, (h, N))] = blau
    faden = (np.abs(rand - 0.05) < 0.012) | (np.abs(rand - 0.15) < 0.012)
    ziel[np.broadcast_to(faden, (h, N))] = gold
    zacken = (rand > 0.075) & (rand < 0.125) & (np.abs(((x % 40) - 20) / 20) < (rand - 0.075) / 0.05)    # Zacken in der Borte
    ziel[np.broadcast_to(zacken, (h, N))] = rot
    # Feld: Rauten im Abstand von 1/8 der Bandhoehe, jede zweite dunkel mit goldenem Kern
    rh = h * 0.30
    cx = (x % (rh * 2)) - rh
    cy = y - h / 2
    raute = (np.abs(cx) / rh + np.abs(cy) / (h * 0.30)) < 1.0
    kern = (np.abs(cx) / rh + np.abs(cy) / (h * 0.30)) < 0.35
    linie = np.abs(np.abs(cx) / rh + np.abs(cy) / (h * 0.30) - 1.0) < 0.06
    feldmaske = rand > 0.19
    ziel[np.broadcast_to(feldmaske & linie, (h, N))] = dunkel
    ziel[np.broadcast_to(feldmaske & kern, (h, N))] = gold
    ziel[np.broadcast_to(feldmaske & raute & ~kern & ~linie & ((x // (rh * 2)) % 2 == 1), (h, N))] = dunkel * 0.6 + rot * 0.4
    return linear_zu_srgb(ziel * hell)


def male_buecher(farbe, normale, saat=5):
    """Buchruecken [h, N]: Breiten 3 bis 8 cm (m = 1,0: 2048 Pixel je Meter), Lederfarben, Buende und Goldlinien quer, Fuge
    dazwischen dunkel; die Normale woelbt jeden Ruecken rund."""
    h = farbe.shape[0]
    z = np.random.default_rng(saat)
    lin = srgb_zu_linear(farbe)
    hell = np.clip((lin @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)) / max(float(lin.mean()), 1e-4), 0.7, 1.3)
    toene = [(0.42, 0.12, 0.08), (0.30, 0.18, 0.10), (0.16, 0.24, 0.16), (0.14, 0.17, 0.30), (0.55, 0.38, 0.18), (0.24, 0.16, 0.10),
             (0.46, 0.30, 0.20), (0.36, 0.08, 0.10)]
    gold = srgb_zu_linear(np.array((0.78, 0.60, 0.28), dtype=np.float32))
    aus = np.zeros((h, N, 3), dtype=np.float32)
    nor = normale.copy()
    metall = np.zeros((h, N), dtype=np.float32)
    x = 0
    while x < N:
        w = int(z.uniform(60, 165))
        w = min(w, N - x)
        ton = srgb_zu_linear(np.array(toene[z.integers(len(toene))], dtype=np.float32)) * z.uniform(1.8, 2.3)
        sp = slice(x, x + w)
        aus[:, sp] = ton[None, None, :] * hell[:, sp, None]
        s = (np.arange(w, dtype=np.float32) + 0.5) / w * 2 - 1                         # -1..1 quer ueber den Ruecken
        nor[:, sp, 0] = np.clip(0.5 + s * 0.38, 0, 1)[None, :]
        # Buende (wulstige Querriegel) und Goldlinien; dazu bei jedem zweiten ein dunkles Titelschild
        anzahl = int(z.integers(3, 6))
        for i in range(anzahl):
            yb = int(h * (0.10 + 0.80 * (i + 0.5) / anzahl))
            aus[max(yb - 3, 0):yb + 3, sp] *= 0.55
            aus[max(yb - 6, 0):max(yb - 4, 0), sp] = gold
            aus[yb + 4:yb + 6, sp] = gold
            metall[max(yb - 6, 0):max(yb - 4, 0), sp] = 1; metall[yb + 4:yb + 6, sp] = 1
            nor[max(yb - 3, 0):yb, sp, 1] = 0.75; nor[yb:yb + 3, sp, 1] = 0.25
        if z.random() < 0.5:
            y0 = int(h * 0.30)
            aus[y0:y0 + int(h * 0.10), x + 6:x + w - 6] *= 0.45
        aus[:, x:x + 3] *= 0.4; aus[:, max(x + w - 3, x):x + w] *= 0.5                # Fuge zum Nachbarn
        aus[:4, sp] *= 0.5; aus[-4:, sp] *= 0.5                                          # Kanten oben und unten
        x += w
    return linear_zu_srgb(aus), nor, metall


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Blatt
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def baue(quellen):
    B = BAENDER_RAEUME
    blatt = Blatt()

    def band(name, q, ton, ab=0, versatz=0, kontrast=1.0, eigenfarbe=0.3, **kw):
        a, e = blatt.zeilen(B[name])
        f, n, arm = q.streifen(e - a, ab=ab, versatz=versatz)
        blatt.setze(B[name], faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe), n, arm, **kw)
        return a, e

    band("dielen", Quelle(quellen, "old_planks_02", gedreht=True), (0.44, 0.33, 0.23), ab=60, kontrast=1.05)
    band("parkett", Quelle(quellen, "dark_wooden_planks", gedreht=True), (0.56, 0.38, 0.22), ab=40, kontrast=1.0, eigenfarbe=0.25)
    band("moebel", Quelle(quellen, "wood_table_001", gedreht=True), (0.52, 0.33, 0.18), ab=200, kontrast=1.1, eigenfarbe=0.3)
    band("balken", Quelle(quellen, "rough_wood", gedreht=True), (0.42, 0.32, 0.22), ab=300, kontrast=1.1, eigenfarbe=0.2)
    blatt.fase(B["balken"])
    band("putz", Quelle(quellen, "white_plaster_rough_01"), (0.80, 0.76, 0.66), ab=100, kontrast=0.8, eigenfarbe=0.1)
    blatt.verlauf(B["putz"], 0.88, 0.80, 40)
    band("bruchstein", Quelle(quellen, "castle_wall_slates"), (0.42, 0.41, 0.40), ab=300, eigenfarbe=0.35)
    band("quader", Quelle(quellen, "sandstone_blocks_08"), (0.68, 0.62, 0.52), ab=0, kontrast=1.3, eigenfarbe=0.5)

    # Teppich: Samt als Grund (Faserstruktur), darauf das gerechnete Muster
    a, e = blatt.zeilen(B["teppich"])
    f, n, arm = Quelle(quellen, "velour_velvet", kante=512).streifen(e - a, ab=0)
    blatt.setze(B["teppich"], male_teppich(f), n, arm, rauheit=0.95)

    # Buchruecken: Leder als Grund, Ruecken, Buende und Gold gerechnet
    a, e = blatt.zeilen(B["buecher"])
    leder = Quelle(quellen, "leather_red_03", kante=1024)
    f, n, arm = leder.streifen(e - a, ab=100)
    farbe, nor, metall = male_buecher(f, n)
    blatt.setze(B["buecher"], farbe, nor, arm, rauheit=0.55)
    blatt.orm[a:e, :, 2] = metall
    blatt.orm[a:e, :, 1] = np.where(metall > 0, 0.35, blatt.orm[a:e, :, 1])

    band("leder", leder, (0.38, 0.16, 0.10), ab=400, kontrast=1.0, eigenfarbe=0.3, rauheit=0.5)
    band("sackleinen", Quelle(quellen, "hessian_230", kante=512), (0.55, 0.46, 0.33), ab=0, kontrast=1.1)

    # Pergament: Putz als Grund (ruhige Flecken), links die Landkarte, daneben das beschriebene Blatt
    a, e = band("papier", Quelle(quellen, "white_plaster_rough_01", kante=1024), (0.84, 0.76, 0.58), ab=500, kontrast=0.6, eigenfarbe=0.1, rauheit=0.85)
    blatt.verlauf(B["papier"], 0.85, 0.85, 12)
    for (u0, u1), maler in ((KARTE_U, male_karte), (SCHRIFT_U, male_schrift)):
        x0, x1 = int(u0 * N), int(u1 * N)
        stueck = blatt.albedo[a:e, x0:x1, :3].copy()
        maske = maler(stueck)
        blatt.albedo[a:e, x0:x1, :3] = stueck
        blatt.normal[a:e, x0:x1, 2] = np.where(maske > 0.3, 0.97, blatt.normal[a:e, x0:x1, 2])
        blatt.orm[a:e, x0:x1, 1] = np.where(maske > 0.3, 0.55, blatt.orm[a:e, x0:x1, 1])

    # Kleinteile: Farbfelder
    putz = Quelle(quellen, "white_plaster_rough_01", kante=512)
    rost = Quelle(quellen, "rusty_metal_02", kante=512)
    a, e = blatt.zeilen(B["kleinteile"])
    feld = N // len(FELDER_RAEUME)
    stoffe = {
        "eisen": (rost, (0.17, 0.17, 0.17), 0.60, 0.7), "stahl": (rost, (0.46, 0.47, 0.48), 0.40, 0.8),
        "messing": (rost, (0.66, 0.50, 0.24), 0.32, 1.0), "gold": (rost, (0.82, 0.63, 0.23), 0.28, 1.0),
        "wachs": (putz, (0.92, 0.87, 0.72), 0.6, 0.0), "tinte": (putz, (0.05, 0.05, 0.06), 0.35, 0.0),
        "glut": (putz, (0.95, 0.42, 0.10), 0.9, 0.0), "glas": (putz, (0.22, 0.34, 0.20), 0.12, 0.0),
        "wein": (putz, (0.30, 0.05, 0.08), 0.15, 0.0), "siegel": (putz, (0.62, 0.10, 0.08), 0.45, 0.0),
    }
    for i, name in enumerate(FELDER_RAEUME):
        q, ton, rau, metall = stoffe[name]
        f, n, arm = q.streifen(e - a, ab=40)
        blatt.setze(B["kleinteile"], faerbe(f, ton, kontrast=0.6, eigenfarbe=0.15), n, arm, rauheit=rau, metall=metall, x0=i * feld, x1=(i + 1) * feld)
    blatt.speichere("raeume_innen_trim")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict(a[2:].split("=", 1) for a in args if a.startswith("--") and "=" in a)
    baue(opt["quellen"])
