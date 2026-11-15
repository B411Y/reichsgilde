"""Bibliothek der Raumskripte (Blender): Raumhuelle im Grundriss des Wohnhauses, Fenster mit Butzenglas, Moebel-Grundformen,
Stoffe, UV2 und gebackene Verdeckung, Anker, Export und Kontrollbilder.

Uebernommen und angepasst aus tools/modelle/neubau/wohnhaus_mensch_s1_g1.py (Stoffe, UV2/AO, Anker, Export) und aus der
Bibliothek des Rathaus-Neubaus (Branch phase5/neubau-rathaus, tools/modelle/neubau/rathaus_bib.py: Boden in Streifen, Tisch,
Stuhl, Ablauf von Bau und Bildern) – jener Branch ist noch nicht in main, darum hier als eigene Kopie.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
NEUBAU = os.path.join(os.path.dirname(HIER), "neubau")
sys.path.insert(0, HIER)
sys.path.insert(0, NEUBAU)
import raeume_masse as M                                                          # noqa: E402
from netzbau import Teil                                                           # noqa: E402
from raeume_trimsheet import BAENDER_RAEUME, FELDER_RAEUME                         # noqa: E402

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
ZIEL_MODELL = os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau")
ZIEL_TEXTUR = os.path.join(WURZEL, "game", "assets", "texturen", "neubau")
HAUS = "wohnhaus_mensch_s1_g1"
GRUPPE = "wohnhaus_mensch_raeume_innen"
BLATT = "raeume_innen_trim"
EZ = Vector((0, 0, 1))
S = "Innen"


def optionen():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))


def feld(name):
    """u-Bereich eines Farbfelds im Kleinteile-Band (mit Rand, damit nichts vom Nachbarn hereinblutet)."""
    i = FELDER_RAEUME.index(name); n = len(FELDER_RAEUME)
    return ((i + 0.15) / n, (i + 0.85) / n)


def huelle_pruefen():
    """Vergleicht die Masse in raeume_masse.py mit dem Hausskript (gleiche Waende, gleiche Fenster)."""
    import wohnhaus_mensch_s1_g1 as W
    paare = [("BX", M.BX, W.BX), ("VORN", M.VORN, W.VORN), ("HINTEN", M.HINTEN, W.HINTEN), ("T", M.T, W.T), ("Z_BODEN", M.Z_BODEN, W.Z_BODEN),
             ("Z_OG", M.Z_OG, W.Z_OG), ("GALERIE_AB", M.GALERIE_AB, W.GALERIE_AB)]
    eg = [(w, a, z) for w, a, z, art in W.FENSTER if art == "fenster" and w.endswith("_eg")]
    tuer = next(a for w, a, z, art in W.FENSTER if art == "tuer")
    fehler = [n for n, a, b in paare if abs(a - b) > 1e-6]
    if [(w, tuple(a), tuple(z)) for w, a, z in eg] != [(w, tuple(a), tuple(z)) for w, a, z in M.FENSTER_EG]: fehler.append("FENSTER_EG")
    if tuple(tuer) != M.HAUSTUER: fehler.append("HAUSTUER")
    if fehler: raise SystemExit(f"raeume_masse.py passt nicht zum Hausskript: {fehler}")
    print("Huelle: Masse wie im Hausskript")


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Raumhuelle
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

# Innenwaende: Name -> (Ursprung, Richtung, Laenge); Sichtseite in den Raum
WAENDE = {
    "vorn": ((-M.IX, M.IV), (1, 0), 2 * M.IX),
    "hinten": ((-M.IX, M.IH), (1, 0), 2 * M.IX),
    "links": ((-M.IX, M.IV), (0, 1), M.IH - M.IV),
    "rechts": ((M.IX, M.IV), (0, 1), M.IH - M.IV),
}
MITTE = Vector((0, -0.1, 1.3))


def wandpunkt(wand, a, z, raus=0.0):
    """Punkt auf der Innenwand (a in Weltkoordinaten entlang der Wand: x bei vorn/hinten, y bei links/rechts), um `raus` in den Raum."""
    (ox, oy), (ex, ey), _ = WAENDE[wand]
    n = {"vorn": (0, 1), "hinten": (0, -1), "links": (1, 0), "rechts": (-1, 0)}[wand]
    if ex: return Vector((a, oy + n[1] * raus, z))
    return Vector((ox + n[0] * raus, a, z))


def fenster_der_wand(wand):
    """Fensteroeffnungen der Huelle in dieser Innenwand: (a0, a1, z0, z1) in Weltkoordinaten entlang der Wand."""
    name = {"vorn": "vorn_eg", "links": "links_eg"}.get(wand)
    return [(a0, a1, z0, z1) for w, (a0, a1), (z0, z1) in M.FENSTER_EG if w == name]


def waende(teil, band, sockel=None, sockel_hoehe=0.0, tafel=None, tafel_hoehe=0.0, hoehe_band=2.4):
    """Die vier Innenwaende vom Boden bis zur Decke mit den Fensteroeffnungen der Huelle. Optional unten ein Sockel (Band
    `sockel` bis `sockel_hoehe`) bzw. eine Taefelung (Band `tafel` bis `tafel_hoehe`) als eigene Lage."""
    for wand, ((ox, oy), (ex, ey), laenge) in WAENDE.items():
        start = ox if ex else oy
        loecher = [(a0 - start, a1 - start, z0, z1) for a0, a1, z0, z1 in fenster_der_wand(wand)]
        lagen = []
        z = M.Z_BODEN
        if sockel: lagen.append((z, z + sockel_hoehe, sockel)); z += sockel_hoehe
        if tafel: lagen.append((z, z + tafel_hoehe, tafel)); z += tafel_hoehe
        lagen.append((z, M.DECKE, band))
        for z0, z1, b in lagen:
            if b == band:
                teil.wand(S, (ox, oy, 0), (ex, ey, 0), laenge, z0, z1, b, loecher=loecher, zbasis=z0, hoehe=hoehe_band, zu=MITTE)
                continue
            v0, v1, m = teil.baender[b]
            kurs = (v1 - v0) * m                        # Sockel und Taefelung in Lagen von der Hoehe des Bandes (keine Streckung)
            z = z0
            while z < z1 - 1e-4:
                zn = min(z + kurs, z1)
                teil.wand(S, (ox, oy, 0), (ex, ey, 0), laenge, z, zn, b, loecher=loecher, zbasis=z, hoehe=kurs, zu=MITTE, u_start=(z * 3.7) % 1)
                z = zn


def boden(teil, band, x0, x1, y0, y1, z, oben=True, laengs="x", saat=3):
    """Waagerechte Flaeche in Streifen von der Hoehe des Bandes (keine Streckung), jeder Streifen an anderer Stelle des Bandes."""
    v0, v1, m = teil.baender[band]
    kurs = (v1 - v0) * m
    q0, q1 = (y0, y1) if laengs == "x" else (x0, x1)
    k = 0
    q = q0
    while q < q1 - 1e-4:
        qn = min(q + kurs, q1)
        u0 = ((k * 7919 + saat * 104729) % 1000) / 1000.0 * 3
        anteil = (0.0, (qn - q) / kurs)
        if laengs == "x":
            teil.rechteck(S, (x0, q, z), (1, 0, 0), (0, 1, 0), x1 - x0, qn - q, band, u0=u0, anteil=anteil, zu=((x0 + x1) / 2, (q + qn) / 2, z + (1 if oben else -1)))
        else:
            teil.rechteck(S, (q, y0, z), (0, 1, 0), (1, 0, 0), y1 - y0, qn - q, band, u0=u0, anteil=anteil, zu=((q + qn) / 2, (y0 + y1) / 2, z + (1 if oben else -1)))
        q = qn; k += 1


def decke(teil, bretter="dielen", balken="balken"):
    """Bretterdecke mit Balken quer (entlang x) – wie der Absatz unter dem Obergeschoss der Wohnstube."""
    boden(teil, bretter, -M.IX, M.IX, M.IV, M.IH, M.DECKE, oben=False, laengs="x", saat=11)
    for y in M.BALKEN_Y:
        teil.kasten(S, (-M.IX, y - 0.08, M.DECKE - M.DECKENBALKEN), (M.IX, y + 0.08, M.DECKE), balken, ohne=("+z", "-x", "+x"), laengs="x")


def fensterlaibung(teil, band, bank_band=None):
    """Rahmen und Bank auf der Innenwand um jede Fensteroeffnung (wie in der Wohnstube); das Fensterkreuz ist das der Huelle."""
    for wand in ("vorn", "links"):
        for a0, a1, z0, z1 in fenster_der_wand(wand):
            n = wandpunkt(wand, 0, 0, 1) - wandpunkt(wand, 0, 0, 0)
            for a, b, breite in ((wandpunkt(wand, a0 - 0.10, z0 - 0.04), wandpunkt(wand, a1 + 0.10, z0 - 0.04), 0.08),
                                 (wandpunkt(wand, a0 - 0.10, z1 + 0.04), wandpunkt(wand, a1 + 0.10, z1 + 0.04), 0.08),
                                 (wandpunkt(wand, a0 - 0.05, z0), wandpunkt(wand, a0 - 0.05, z1), 0.10),
                                 (wandpunkt(wand, a1 + 0.05, z0), wandpunkt(wand, a1 + 0.05, z1), 0.10)):
                teil.brett(S, a, b, breite, n, band, tiefe=0.035)
            if bank_band:   # tiefe Fensterbank in der Laibung
                p0 = wandpunkt(wand, a0, z0, 0.04); p1 = wandpunkt(wand, a1, z0, -M.T / 2 + 0.03)
                teil.kasten(S, (min(p0.x, p1.x), min(p0.y, p1.y), z0 - 0.03), (max(p0.x, p1.x), max(p0.y, p1.y), z0), bank_band)


def spitzbogenfenster(teil, band, rahmen_band, wand, a0, a1, z0, z1, kaempfer, glas_teil=None):
    """Spitzbogen in die rechteckige Oeffnung der Huelle: Zwickel ueber dem Bogen auf der Innenwand gefuellt (Band `band`),
    Bogenleiste aus Stuecken (Band `rahmen_band`), Glas (Butzen) als Dreiecksfaecher. kaempfer: Hoehe, an der der Bogen ansetzt."""
    n = wandpunkt(wand, 0, 0, 1) - wandpunkt(wand, 0, 0, 0)
    am = (a0 + a1) / 2; halb = (a1 - a0) / 2
    hoch = z1 - kaempfer
    teile = 10
    def bogen(t, r_ab=0.0):
        """Punkt auf dem Spitzbogen: t von 0 (linker Kaempfer) bis 1 (rechter Kaempfer); zwei Kreisboegen mit Radius r um die
        Gegenseite (gedrueckter Spitzbogen: Scheitel in z1)."""
        r = (halb * halb + hoch * hoch) / (2 * halb)          # Radius, damit der Bogen bei a0/a1 senkrecht ansetzt und in z1 spitz endet
        if t <= 0.5:
            mx = a0 + r; w0 = math.pi; w1 = math.pi - math.atan2(hoch, r - halb)
            w = w0 + (w1 - w0) * (t / 0.5)
        else:
            mx = a1 - r; w0 = math.atan2(hoch, r - halb); w1 = 0.0
            w = w0 + (w1 - w0) * ((t - 0.5) / 0.5)
        rr = r - r_ab
        return mx + rr * math.cos(w), kaempfer + rr * math.sin(w)
    pts = [bogen(i / teile) for i in range(teile + 1)]
    # Zwickel: zwischen Bogen und Oberkante der Oeffnung, auf der Wandflaeche
    v0, v1, m = teil.baender[band]
    def uv(a, z): return ((a - a0) / m, v1 - min(max((z - z0) / 2.4, 0), 1) * (v1 - v0))
    for (aa, za), (ab, zb) in zip(pts[:-1], pts[1:]):
        if abs(zb - z1) < 1e-4 and abs(za - z1) < 1e-4: continue
        ecken = [(aa, za), (ab, zb), (ab, z1), (aa, z1)]
        if abs(zb - z1) < 1e-4: ecken = [(aa, za), (ab, zb), (aa, z1)]           # am Scheitel ein Dreieck (kein Viereck mit doppelter Ecke)
        elif abs(za - z1) < 1e-4: ecken = [(aa, za), (ab, zb), (ab, z1)]
        teil.vieleck(S, [wandpunkt(wand, a, z) for a, z in ecken], [uv(a, z) for a, z in ecken], zu=wandpunkt(wand, (aa + ab) / 2, z1, 1))
    # Bogenleiste und Gewaende
    for (aa, za), (ab, zb) in zip(pts[:-1], pts[1:]):
        teil.stab(S, wandpunkt(wand, aa, za, 0.02), wandpunkt(wand, ab, zb, 0.02), 0.04, rahmen_band, breite=0.07, oben=n)
    for a in (a0, a1):
        teil.stab(S, wandpunkt(wand, a, z0, 0.02), wandpunkt(wand, a, kaempfer, 0.02), 0.04, rahmen_band, breite=0.07, oben=n)
    if glas_teil is not None:
        # Glas in der Mitte der Wand (wie die Wohnstube), UV in Metern ab der linken unteren Ecke der Oeffnung
        raus = -M.T / 2 + 0.004   # wie das Glas der Wohnstube: in der Wandmitte vor dem Fensterkreuz der Huelle
        rand = [(a0, z0), (a1, z0), (a1, kaempfer)] + [p for p in reversed(pts[1:-1])] + [(a0, kaempfer)]
        mitte = (am, (z0 + kaempfer) / 2)
        for p, q in zip(rand, rand[1:] + rand[:1]):
            dreieck = [wandpunkt(wand, mitte[0], mitte[1], raus), wandpunkt(wand, p[0], p[1], raus), wandpunkt(wand, q[0], q[1], raus)]
            uvs = [(mitte[0] - a0, 1 - (mitte[1] - z0)), (p[0] - a0, 1 - (p[1] - z0)), (q[0] - a0, 1 - (q[1] - z0))]
            glas_teil.vieleck("Innenglas", dreieck, uvs, zu=wandpunkt(wand, mitte[0], mitte[1], 2))


def rechteckglas(glas_teil, wand, a0, a1, z0, z1):
    """Glas in einer rechteckigen Oeffnung (Butzen-Shader, UV in Metern) – Lage wie in der Wohnstube."""
    raus = -M.T / 2 + 0.004   # wie das Glas der Wohnstube: in der Wandmitte vor dem Fensterkreuz der Huelle
    pts = [wandpunkt(wand, a0, z0, raus), wandpunkt(wand, a1, z0, raus), wandpunkt(wand, a1, z1, raus), wandpunkt(wand, a0, z1, raus)]
    glas_teil.vieleck("Innenglas", pts, [(0, 1), (a1 - a0, 1), (a1 - a0, 1 - (z1 - z0)), (0, 1 - (z1 - z0))], zu=wandpunkt(wand, (a0 + a1) / 2, (z0 + z1) / 2, 2))


def spiegeln_x(teil, ab=0):
    """Spiegelt die Flaechen ab Nummer `ab` an der Ebene x = 0 (Reihenfolge der Ecken umgedreht, damit die Sichtseite bleibt)."""
    teil.flaechen[ab:] = [([Vector((-p.x, p.y, p.z)) for p in reversed(pts)], list(reversed(uvs)), st, fa, gl) for pts, uvs, st, fa, gl in teil.flaechen[ab:]]


def drehen_180(teil, ab=0):
    """Dreht die Flaechen ab Nummer `ab` um 180 Grad um die senkrechte Achse durch die Raummitte: Gassenwand -> Rueckwand."""
    my = M.IV + M.IH
    teil.flaechen[ab:] = [([Vector((-p.x, my - p.y, p.z)) for p in pts], uvs, st, fa, gl) for pts, uvs, st, fa, gl in teil.flaechen[ab:]]


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Moebel-Grundformen
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def tisch(teil, band, x0, x1, y0, y1, hoehe=0.76, platte=0.05, zarge=0.10, bein=0.07, z0=0.0):
    """Tisch mit Platte, Zarge und vier Beinen (achsparallel)."""
    teil.kasten(S, (x0, y0, z0 + hoehe - platte), (x1, y1, z0 + hoehe), band)
    zz = z0 + hoehe - platte - zarge
    e = 0.05
    teil.kasten(S, (x0 + e, y0 + e, zz), (x1 - e, y1 - e, z0 + hoehe - platte - 0.002), band, ohne=("+z",))
    for bx, by in ((x0 + e, y0 + e), (x1 - e - bein, y0 + e), (x0 + e, y1 - e - bein), (x1 - e - bein, y1 - e - bein)):
        teil.kasten(S, (bx + 0.002, by + 0.002, z0), (bx + bein - 0.002, by + bein - 0.002, zz - 0.002), band, ohne=("+z", "-z"))


def stuhl(teil, band, punkt, blick_grad, sitz=0.44, breite=0.46, tiefe=0.44, vorn=0.16, lehne=0.98, polster=None, armlehnen=False):
    """Stuhl um den Sitzpunkt (Boden unter der Huefte), Blick in Grad (0 = +Y, 90 = +X); Lehne hinten, `polster` = Band des Kissens."""
    f = Vector(punkt)
    w = math.radians(blick_grad)
    vor = Vector((math.sin(w), math.cos(w), 0)); quer = Vector((math.cos(w), -math.sin(w), 0))
    c = f + vor * (vorn - tiefe / 2)
    v0, v1, _ = teil.baender[band]
    def box(mitte, gr_q, gr_v, gr_z, z0, bd=band):
        b0, b1, _ = teil.baender[bd]
        m = c + quer * mitte[0] + vor * mitte[1] + EZ * z0
        ecken = [m + quer * sx * gr_q / 2 + vor * sy * gr_v / 2 for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        for i in range(4):
            a, b = ecken[i], ecken[(i + 1) % 4]
            teil.vieleck(S, [a, b, b + EZ * gr_z, a + EZ * gr_z], [(0, b1), (max(gr_z, (a - b).length) / 2, b1), (max(gr_z, (a - b).length) / 2, b0), (0, b0)], zu=m + EZ * gr_z / 2 + (a + b - 2 * m) * 3)
        teil.vieleck(S, [e + EZ * gr_z for e in ecken], [(0, b1), (gr_q / 2, b1), (gr_q / 2, b0), (0, b0)], zu=m + EZ * (gr_z + 1))
    brett = sitz - (0.05 if polster else 0.0)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box((sx * (breite / 2 - 0.035), sy * (tiefe / 2 - 0.035)), 0.045, 0.045, brett - 0.042 if sy > 0 else lehne, 0.0)
    box((0, 0), breite, tiefe, 0.04, brett - 0.04)
    box((0, -tiefe / 2 + 0.035), breite - 0.07, 0.035, lehne - sitz - 0.12, sitz + 0.08)   # 5 mm vor der Rueckseite der Beine (sonst deckungsgleich)
    if polster: box((0, 0.01), breite - 0.06, tiefe - 0.06, 0.05, brett, polster)
    if armlehnen:
        for sx in (-1, 1): box((sx * (breite / 2 - 0.03), 0), 0.06, tiefe, 0.05, sitz + 0.22)


def schemel(teil, band, x, y, sitz=0.44, r=0.17, beine=3):
    """Runder Schemel mit drei gespreizten Beinen; `sitz` ueber dem Fussboden."""
    z = M.Z_BODEN
    teil.dreh(S, (x, y, z + sitz - 0.045), [(r, 0.0), (r, 0.045)], band, seiten=10, glatt=False)
    for i in range(beine):
        w = 2 * math.pi * i / beine + 0.4
        teil.stab(S, (x + math.cos(w) * r * 0.85, y + math.sin(w) * r * 0.85, z),
                  (x + math.cos(w) * r * 0.45, y + math.sin(w) * r * 0.45, z + sitz - 0.045), 0.04, band)


def fass(teil, band, eisen_ub, x, y, z, hoehe=0.78, r=0.27, liegend=False):
    """Fass aus Dauben (Textur laengs) mit zwei Eisenreifen."""
    bauch = [(r * 0.86, 0.0), (r * 0.97, hoehe * 0.25), (r, hoehe * 0.5), (r * 0.97, hoehe * 0.75), (r * 0.86, hoehe)]
    teil.dreh(S, (x, y, z), bauch, band, seiten=12, deckel=True, gedreht=True)
    for h in (hoehe * 0.14, hoehe * 0.86):
        rr = r * (0.89 if h < hoehe / 2 else 0.89) + 0.006
        teil.dreh(S, (x, y, z + h - 0.025), [(rr, 0.0), (rr, 0.05)], "kleinteile", seiten=12, deckel=False, u_bereich=eisen_ub)


def kiste(teil, band, eisen_ub, x0, y0, z0, x1, y1, z1, latten=True):
    """Holzkiste mit Eckleisten."""
    teil.kasten(S, (x0, y0, z0), (x1, y1, z1), band, laengs="x" if x1 - x0 >= y1 - y0 else "y")
    if latten:
        for (ax, ay) in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
            teil.kasten(S, (ax - 0.012, ay - 0.012, z0), (ax + 0.012, ay + 0.012, z1), "balken", ohne=("-z",), laengs="z")


def sack(teil, x, y, z, r=0.2, hoehe=0.5, saat=0):
    """Sack aus Sackleinen (Drehkoerper, oben zugebunden)."""
    teil.dreh(S, (x, y, z), [(r * 0.85, 0.0), (r, hoehe * 0.25), (r * 0.95, hoehe * 0.6), (r * 0.55, hoehe * 0.85), (r * 0.18, hoehe * 0.93), (r * 0.3, hoehe)], "sackleinen", seiten=9, deckel=True)


def flasche(teil, x, y, z, farbe="glas", hoch=0.26, r=0.045):
    teil.dreh(S, (x, y, z), [(r, 0.0), (r, hoch * 0.6), (r * 0.35, hoch * 0.78), (r * 0.3, hoch), (0.0, hoch)], "kleinteile", seiten=8, deckel=False, u_bereich=feld(farbe))


def kerze(teil, x, y, z, hoch=0.14, r=0.02, leuchter=True):
    """Kerze (Wachs) im eisernen Leuchter; gibt die Spitze zurueck (Ort der Flamme)."""
    if leuchter:
        teil.dreh(S, (x, y, z), [(0.06, 0.0), (0.05, 0.012), (0.012, 0.03), (0.012, 0.06), (0.035, 0.065), (0.035, 0.075)], "kleinteile", seiten=8, deckel=True, u_bereich=feld("messing"))
        z += 0.075
    teil.dreh(S, (x, y, z), [(r, 0.0), (r, hoch)], "kleinteile", seiten=7, deckel=True, u_bereich=feld("wachs"))
    return Vector((x, y, z + hoch))


def buchreihe(teil, x0, x1, y_vorn, tiefe, z, h_min, h_max, saat=1, achse="x", innen=1):
    """Reihe von Buechern auf einem Brett: einzelne Kaesten unterschiedlicher Hoehe und Tiefe, Ruecken zur Raumseite (Band "buecher":
    die volle Bandhoehe ist die Buchhoehe), oben und hinten Pergament (Schnitt). `achse`: entlang x (Wand vorn/hinten) oder y."""
    import random
    z_ = random.Random(saat)
    a = x0
    v0, v1, m = teil.baender["buecher"]
    p0, p1, _ = teil.baender["papier"]
    while a < x1 - 0.03:
        d = min(z_.uniform(0.03, 0.075), x1 - a)
        h = z_.uniform(h_min, h_max)
        t = tiefe * z_.uniform(0.8, 1.0)
        u = z_.random()
        if z_.random() < 0.07:   # Luecke
            a += d; continue
        if achse == "x":
            q0, q1 = y_vorn, y_vorn + innen * t
            mn = (a, min(q0, q1), z); mx = (a + d, max(q0, q1), z + h)
            ruecken = [Vector((a, y_vorn, z)), Vector((a + d, y_vorn, z)), Vector((a + d, y_vorn, z + h)), Vector((a, y_vorn, z + h))]
            raus = Vector((0, -innen, 0))
        else:
            q0, q1 = y_vorn, y_vorn + innen * t
            mn = (min(q0, q1), a, z); mx = (max(q0, q1), a + d, z + h)
            ruecken = [Vector((y_vorn, a, z)), Vector((y_vorn, a + d, z)), Vector((y_vorn, a + d, z + h)), Vector((y_vorn, a, z + h))]
            raus = Vector((-innen, 0, 0))
        teil.vieleck(S, ruecken, [(u, v1), (u + d / m, v1), (u + d / m, v0), (u, v0)], zu=ruecken[0] + raus + Vector((0, 0, h / 2)))
        seite = "-y" if achse == "x" and innen > 0 else "+y" if achse == "x" else "-x" if innen > 0 else "+x"
        teil.kasten(S, mn, mx, "papier", ohne=(seite, "-z"))
        for fl in teil.flaechen[-5:]:   # Deckel und Schnitt: helles Pergament aus dem leeren Teil des Bandes
            fl[1][:] = [(0.6 + 0.1 * u, p1), (0.62 + 0.1 * u, p1), (0.62 + 0.1 * u, p0 + 0.02), (0.6 + 0.1 * u, p0 + 0.02)][:len(fl[1])]
        a += d + 0.002


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Blender: Stoffe, UV2, AO, Anker, Export, Bilder
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def stoff(name, blatt=None, farbe=(0.5, 0.5, 0.5), alpha=1.0, rauheit=0.8, leuchten=0.0):
    """Material fuer Kontrollbild und Export (die GLB bekommt keine Bilder; das Spiel setzt die Trim-Sheets beim Laden)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(nd for nd in m.node_tree.nodes if nd.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*farbe, 1.0)
    bsdf.inputs["Roughness"].default_value = rauheit
    bsdf.inputs["Alpha"].default_value = alpha
    if leuchten:
        bsdf.inputs["Emission Color"].default_value = (*farbe, 1.0)
        bsdf.inputs["Emission Strength"].default_value = leuchten
    if alpha < 1.0: m.surface_render_method = "BLENDED"
    if blatt:
        bild = bpy.data.images.load(os.path.join(ZIEL_TEXTUR, f"{blatt}_albedo.png"), check_existing=True)
        tex = m.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = bild; tex.name = "Trim"
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def stoffe():
    return {"Innen": stoff("Innen", BLATT), "Innenglas": stoff("Innenglas", farbe=(0.55, 0.62, 0.6), alpha=0.16, rauheit=0.15)}


def verschmelzen(obj):
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bm.to_mesh(obj.data); bm.free()


def atlas_platz(raum):
    """Platz des Raums im AO-Atlas der Gruppe (tools/modelle/raeume/ao_atlas.json)."""
    daten = json.load(open(os.path.join(HIER, "ao_atlas.json"), encoding="utf-8"))[GRUPPE]
    nummer = daten["plaetze"][raum]
    n = daten["zellen"]
    return daten["datei"], daten["kante"], n, nummer % n, nummer // n


def uv2_und_ao(gruppen, raum, mit_ao=True, proben=None):
    """UV2 und gebackene Verdeckung in die Zelle des Raums – mit der berichtigten Hilfsfunktion des Hausskripts
    (wohnhaus_mensch_s1_g1.uv2_und_ao, UV2-Korrektur 15.11.2026: Inselrand 0,003, Rand zur Nachbarzelle vor dem Backen in die UV2,
    ein Backzug je Gruppe, vierfach fein gebacken, Rand selbst gefuellt). Nur der Platz im Atlas kommt aus
    tools/modelle/raeume/ao_atlas.json; `proben` bleibt fuer den Aufruf, die Funktion backt mit 96 Proben."""
    import wohnhaus_mensch_s1_g1 as W
    vorher = W.atlas_platz
    W.atlas_platz = lambda gruppe, modell: atlas_platz(raum)
    try:
        W.uv2_und_ao(gruppen, GRUPPE, raum, mit_ao=mit_ao)
    finally:
        W.atlas_platz = vorher

BLICK = {"nord": 0.0, "west": math.pi / 2, "sued": math.pi, "ost": -math.pi / 2}


def anker(name, ort, blick=None, grad=None, **extras):
    """Leerer Knoten. `blick`: Himmelsrichtung (nord = +Y) oder `grad` (0 = +Y, 90 = +X): im Spiel schaut -Z dorthin.
    `extras` landen als glTF-extras am Knoten."""
    o = bpy.data.objects.new(name, None)
    o.location = ort
    if blick: o.rotation_euler = (0, 0, BLICK[blick])
    if grad is not None: o.rotation_euler = (0, 0, -math.radians(grad))
    for k, v in extras.items(): o[k] = v
    bpy.context.scene.collection.objects.link(o)
    return o


def exportiere(objekte, datei):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objekte: o.select_set(True)
    os.makedirs(os.path.dirname(datei), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=datei, export_format="GLB", use_selection=True, export_yup=True, export_apply=True,
                              export_texcoords=True, export_normals=True, export_vertex_color="ACTIVE", export_all_vertex_colors=False,
                              export_image_format="NONE", export_materials="EXPORT", export_extras=True, export_cameras=False, export_lights=False)
    print("geschrieben", datei, os.path.getsize(datei), "Bytes")


def kontrollbild(datei, auge, ziel, brennweite=35, verbergen=(), proben=32, aufloesung=(1280, 960), sonne=True, welt=0.9):
    szene = bpy.context.scene
    kam = bpy.data.objects.get("Kontrollkamera")
    if kam is None:
        kam = bpy.data.objects.new("Kontrollkamera", bpy.data.cameras.new("Kontrollkamera"))
        szene.collection.objects.link(kam)
        s = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN"))
        s.data.energy = 3.2; s.rotation_euler = (math.radians(55), 0, math.radians(-150))
        szene.collection.objects.link(s)
        if szene.world is None: szene.world = bpy.data.worlds.new("Welt")
        szene.world.use_nodes = True
    hg = next(nd for nd in szene.world.node_tree.nodes if nd.type == "BACKGROUND")
    hg.inputs[0].default_value = (0.62, 0.66, 0.72, 1); hg.inputs[1].default_value = welt
    bpy.data.objects["Sonne"].hide_render = not sonne
    kam.location = auge
    kam.rotation_euler = (Vector(ziel) - Vector(auge)).to_track_quat("-Z", "Y").to_euler()
    kam.data.lens = brennweite
    kam.data.clip_start = 0.05
    szene.camera = kam
    szene.render.engine = "CYCLES"; szene.cycles.device = "CPU"; szene.cycles.samples = proben
    szene.render.resolution_x, szene.render.resolution_y = aufloesung
    szene.render.filepath = datei
    szene.render.image_settings.file_format = "JPEG"
    szene.view_settings.view_transform = "Standard"
    vorher = {o.name: o.hide_render for o in bpy.data.objects}
    for o in bpy.data.objects:
        if o.name in verbergen: o.hide_render = True
    bpy.ops.render.render(write_still=True)
    for o in bpy.data.objects: o.hide_render = vorher.get(o.name, False)


def massfigur(teil, fuss, hoehe, breite):
    """Grobe Massfigur (Quader mit Kopf) fuer die Kontrollbilder: Mensch 1,75, Oger 2,50."""
    x, y, z = fuss
    teil.kasten("Mass", (x - breite / 2, y - 0.13, z), (x + breite / 2, y + 0.13, z + hoehe * 0.86), "putz")
    k = hoehe * 0.07
    teil.kasten("Mass", (x - k, y - k, z + hoehe * 0.86), (x + k, y + k, z + hoehe), "putz")


def raum_bauen(raum, teile, anker_fn, opt):
    """Ablauf je Raum: Teile zu Objekten, UV2 und Verdeckung (Huelle `teile[0]` fuer sich, die Moebel mit der Huelle als Verdecker,
    Glas ohne), Anker dazu, Export nach <HAUS>_<raum>_innen.glb. Gibt die Objekte zurueck."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    huelle_pruefen()
    st = stoffe()
    teile = [t() if callable(t) else t for t in teile]
    budget = {t.name: t.dreiecke() for t in teile}
    print(f"DREIECKE {raum}:", budget, "Summe", sum(budget.values()))
    objekte = [t.objekt(st, mit_farbe=False) for t in teile]
    rest = [o for o in objekte[1:] if o.name != "Glas"]
    uv2_und_ao([([objekte[0]], []), (rest, [objekte[0]])], raum, mit_ao="ohne-ao" not in opt, proben=int(opt.get("proben", 64)))
    anker = anker_fn()
    exportiere(objekte + anker, os.path.join(ZIEL_MODELL, f"{HAUS}_{raum}_innen.glb"))
    return objekte


def raum_bilder(o, praefix, sichten, uebersicht, mass=(), lichter=(), mit_huelle=True, schnitt=("vorn", "rechts")):
    """Kontrollbilder (Blender, kein Spielbild): je Sicht (Name, Auge, Ziel) mit Brennweite fuer 66 Grad senkrecht (wie die
    Raumkamera im Spiel, Bild 16:9), die erste auch mit Massfiguren (`mass`: (x, y, z, hoehe, breite)); dazu die Uebersicht
    schraeg von oben ohne Decke. `mit_huelle`: die Huelle des Hauses dazuladen (Fensterkreuze, Blick nach draussen)."""
    os.makedirs(o, exist_ok=True)
    if mit_huelle:
        pfad = os.path.join(ZIEL_MODELL, f"{HAUS}.glb")
        if os.path.exists(pfad):
            vorher = set(bpy.data.objects.keys())
            bpy.ops.import_scene.gltf(filepath=pfad)
            for ob in bpy.data.objects:
                if ob.name not in vorher and ob.name.split(".")[0] in ("Scheiben", "Leerstand", "Geruest", "Fahne", "Wappen"): ob.hide_render = True
    boden = bpy.data.objects.get("Kontrollboden")
    if boden is None:
        bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0))
        boden = bpy.context.active_object; boden.name = "Kontrollboden"
        boden.data.materials.append(stoff("Kontrollboden", farbe=(0.33, 0.3, 0.24)))
    if mass:
        mt = Teil("Massfiguren", BAENDER_RAEUME)
        for (x, y, z, h, b) in mass: massfigur(mt, (x, y, z), h, b)
        mt.objekt({"Mass": stoff("Mass", farbe=(0.85, 0.35, 0.1))}, mit_farbe=False)
    for i, (p, energie, farbe) in enumerate(lichter):
        l = bpy.data.objects.new(f"Kontrolllicht{i}", bpy.data.lights.new(f"Kontrolllicht{i}", "POINT"))
        l.data.energy = energie; l.data.color = farbe; l.location = p; l.data.shadow_soft_size = 0.05
        bpy.context.scene.collection.objects.link(l)
    brenn = (36.0 / 2) / math.tan(math.atan(math.tan(math.radians(66) / 2) * 16 / 9))   # 66 Grad senkrecht bei 16:9 -> waagerecht
    for i, (name, auge, ziel) in enumerate(sichten):
        kontrollbild(os.path.join(o, f"{praefix}_{name}.jpg"), auge, ziel, brennweite=brenn, verbergen=("Massfiguren", "Dach", "Kontrollboden"), proben=48, aufloesung=(1280, 720))
        if i == 0 and mass:
            kontrollbild(os.path.join(o, f"{praefix}_{name}_mass.jpg"), auge, ziel, brennweite=brenn, verbergen=("Dach", "Kontrollboden"), proben=32, aufloesung=(1280, 720))
    import bmesh
    def weg(m):   # Schnitt fuer die Uebersicht: Decke und die Waende zur Kamera ueber Sockelhoehe (wie die Vorlage des Nutzers)
        if m.z > M.DECKE - 0.3: return True
        if m.z < M.Z_BODEN + 0.45: return False
        return ("vorn" in schnitt and m.y < M.IV + 0.3) or ("rechts" in schnitt and m.x > M.IX - 0.3) or ("links" in schnitt and m.x < -M.IX + 0.3) or ("hinten" in schnitt and m.y > M.IH - 0.3)
    for ob in bpy.data.objects:
        if ob.name == "Raum":
            bm = bmesh.new(); bm.from_mesh(ob.data)
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if weg(f.calc_center_median())], context="FACES"); bm.to_mesh(ob.data); bm.free()
    weg = tuple(ob.name for ob in bpy.data.objects if ob.name.split(".")[0] in ("Dach", "Haus", "Scheiben", "Massfiguren"))
    kontrollbild(os.path.join(o, f"{praefix}_uebersicht.jpg"), uebersicht[0], uebersicht[1], brennweite=20, verbergen=weg, proben=48)
