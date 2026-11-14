"""Neubau der Gebaeude, Probe: Wohnhaus Mensch, Stufe 1 (Gemeiner), Grundform 1 (schmal und hoch) - aussen und innen.

    ~/werkzeuge/blender/blender.sh -b --python tools/modelle/neubau/wohnhaus_mensch_s1_g1.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt
    game/assets/modelle/gebaeude/neubau/wohnhaus_mensch_s1_g1.glb         Huelle: Knoten Haus, Dach, Scheiben, Anker_Tuer, Anker_Schornstein
    game/assets/modelle/gebaeude/neubau/wohnhaus_mensch_s1_g1_innen.glb   Innenraum (im Spiel der Knoten InteriorRoot): Raum, Glas, fuenf Moebel-Cluster
    game/assets/texturen/neubau/wohnhaus_mensch_s1_g1_ao.png, ..._innen_ao.png   gebackene Verdeckung auf UV2 (512 x 512)

Regeln aus dem Style-Guide (3D-Asset-Pipeline, November 2026): Budget 1.500-3.500 Dreiecke, Alpha-Cards an Traufe, First und
Giebel, Dach in drei ueberlappenden Stufen, Sockel eingelassen, UV1 auf dem Trim-Sheet (trimsheet.py), UV2 ueberlappungsfrei
(Smart UV Project, Rand 0,02) mit gebackener Verdeckung, Eckenfarbe Rot auf den Fensterlaeden.
Fuers Spiel (Stadtansicht.Modelle.cs, .Brandschaden.cs, .Neubau.cs): Vorderseite bei -Y (im Spiel +Z), Grundflaeche 6 x 5,6 m
(x -3..3, y -3..2,6) wie das bisherige fachwerkhaus.glb; Materialnamen "Stroh", "Strohkante", "Fachwerk", "Lehmputz",
"Bruchstein", "Fenster" - nach ihnen greifen Brandschaden, Fensterlicht, Traufen und Kaminrauch.
Innen und aussen entstehen aus denselben Massen (MASSE, FENSTER): gleiche Waende, Fenster und Tuer an derselben Stelle.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import netzbau                                                   # noqa: E402
from netzbau import ROT, Teil                                      # noqa: E402
from trimsheet import BAENDER_AUSSEN, BAENDER_INNEN, FELDER_INNEN  # noqa: E402

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
NAME = "wohnhaus_mensch_s1_g1"
ZIEL_MODELL = os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau")
ZIEL_TEXTUR = os.path.join(WURZEL, "game", "assets", "texturen", "neubau")

# ── Masse (Meter) ────────────────────────────────────────────────────────────────────────────────────────────────────
BX = 2.2                    # halbe Breite des Erdgeschosses (aussen)
VORN, HINTEN = -2.6, 2.4    # Erdgeschoss aussen; die Grundflaeche (mit Dachueberstand und Stufe) reicht von -3,0 bis 2,6
T = 0.22                    # Wandstaerke
KRAG = 0.22                 # Auskragung des Obergeschosses nach vorn und zu den Seiten
Z_BODEN = 0.15              # Fussboden innen
Z_SOCKEL = 0.45             # Oberkante des Bruchsteinsockels
Z_OG = 2.55                 # Unterkante Obergeschoss (Schwelle)
Z_TRAUFE = 4.85             # Oberkante der Seitenwaende
NEIGUNG = math.tan(math.radians(52))
UEBER = 0.38                # Dachueberstand an der Traufe (waagerecht)
DACH_VORN, DACH_HINTEN = -3.0, 2.6
STROH = 0.28                # Dicke der Strohdeckung (senkrecht)
OX = BX + KRAG              # halbe Breite des Obergeschosses
OVORN = VORN - KRAG
Z_FIRST = Z_TRAUFE + OX * NEIGUNG          # Unterseite des Dachs am First
GALERIE_AB = 0.6            # die Schlafgalerie liegt ueber dem hinteren Teil (y ab hier)
KAMIN = (1.38, 1.98, -1.75, -1.05)        # Schlot (x0, x1, y0, y1): innen an der rechten Wand, oben durchs Dach

# Oeffnungen: Wand, (a0, a1) entlang der Wand in Weltkoordinaten (x bei vorn/hinten, y bei links/rechts), (z0, z1), Art
FENSTER = [
    ("vorn_eg", (0.62, 1.58), (Z_BODEN, 2.10), "tuer"),
    ("vorn_eg", (-1.35, -0.35), (1.05, 2.00), "fenster"),
    ("links_eg", (-0.50, 0.40), (1.10, 1.95), "fenster"),
    ("vorn_og", (-1.45, -0.75), (3.35, 4.20), "fenster"),
    ("vorn_og", (0.75, 1.45), (3.35, 4.20), "fenster"),
    ("links_og", (-1.90, -1.20), (3.35, 4.20), "fenster"),
    ("rechts_og", (1.00, 1.70), (3.35, 4.20), "fenster"),
    ("hinten_og", (-0.35, 0.35), (3.35, 4.20), "fenster"),
]
# Waende: Name -> (Ursprung aussen, Richtung, Breite, Normale nach aussen); innen liegt dieselbe Wand um T nach innen versetzt
WAENDE = {
    "vorn_eg": ((-BX, VORN), (1, 0), 2 * BX, (0, -1)),
    "hinten_eg": ((-BX, HINTEN), (1, 0), 2 * BX, (0, 1)),
    "links_eg": ((-BX, VORN), (0, 1), HINTEN - VORN, (-1, 0)),
    "rechts_eg": ((BX, VORN), (0, 1), HINTEN - VORN, (1, 0)),
    "vorn_og": ((-OX, OVORN), (1, 0), 2 * OX, (0, -1)),
    "hinten_og": ((-OX, HINTEN), (1, 0), 2 * OX, (0, 1)),
    "links_og": ((-OX, OVORN), (0, 1), HINTEN - OVORN, (-1, 0)),
    "rechts_og": ((OX, OVORN), (0, 1), HINTEN - OVORN, (1, 0)),
}
MITTE = Vector((0, 0, 3.0))


def loecher(wand):
    """Oeffnungen einer Wand in Wandkoordinaten (s ab dem Ursprung der Wand)."""
    (ox, oy), (ex, ey), _, _ = WAENDE[wand]
    start = ox if ex else oy
    return [(a0 - start, a1 - start, z0, z1, art) for w, (a0, a1), (z0, z1), art in FENSTER if w == wand]


def punkt(wand, s, z, raus=0.0):
    (ox, oy), (ex, ey), _, (nx, ny) = WAENDE[wand]
    return Vector((ox + ex * s + nx * raus, oy + ey * s + ny * raus, z))


def dach_z(x, oben=False):
    """Hoehe des Dachs (Unterseite, mit `oben` die Oberkante der Deckung ohne Stufen) ueber der Stelle x."""
    return Z_FIRST - abs(x) * NEIGUNG + (STROH if oben else 0.0)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Aussen
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def baue_haus():
    h = Teil("Haus", BAENDER_AUSSEN, saat=11)
    lehm, holz, stein = "Lehmputz", "Fachwerk", "Bruchstein"
    umfang = 0.0
    for wand in ("vorn_eg", "rechts_eg", "hinten_eg", "links_eg"):
        (ox, oy), (ex, ey), breite, n = WAENDE[wand]
        o, eu = (ox, oy, 0), (ex, ey, 0)
        l = [x[:4] for x in loecher(wand)]
        # Sockel aus Bruchstein, 12 cm in den Boden eingelassen (keine Unterseite)
        tuer = [x for x in l if x[2] < Z_SOCKEL]
        h.wand(stein, o, eu, breite, -0.12, Z_SOCKEL, "bruchstein", loecher=[(a, b, c, min(d, Z_SOCKEL + 1)) for a, b, c, d in tuer], hoehe=Z_SOCKEL + 0.12, zbasis=-0.12, weg_von=MITTE, u_start=umfang)
        h.wand(lehm, o, eu, breite, Z_SOCKEL, Z_OG, "putz", loecher=l, zbasis=Z_SOCKEL, laibung=(holz, "balken_c"), laibung_tiefe=T, weg_von=MITTE, u_start=umfang)
        umfang += breite
    for wand in ("vorn_og", "rechts_og", "hinten_og", "links_og"):
        (ox, oy), (ex, ey), breite, n = WAENDE[wand]
        l = [x[:4] for x in loecher(wand)]
        h.wand(lehm, (ox, oy, 0), (ex, ey, 0), breite, Z_OG, Z_TRAUFE, "putz", loecher=l, zbasis=Z_OG, hoehe=Z_TRAUFE - Z_OG, laibung=(holz, "balken_c"), laibung_tiefe=T, weg_von=MITTE, u_start=umfang)
        umfang += breite
    # Giebel vorn und hinten: Trapez bis zum Kehlbalken, darueber das Dreieck
    zk = Z_TRAUFE + 1.45
    xk = OX - (zk - Z_TRAUFE) / NEIGUNG
    for y, n in ((OVORN, -1), (HINTEN, 1)):
        o, eu = (-OX, y, 0), (1, 0, 0)
        h.welt(lehm, [(-OX, y, Z_TRAUFE), (OX, y, Z_TRAUFE), (xk, y, zk), (-xk, y, zk)], "putz", o, eu, Z_TRAUFE, zk - Z_TRAUFE, weg_von=MITTE)
        h.welt(lehm, [(-xk, y, zk), (xk, y, zk), (0, y, Z_FIRST)], "putz", o, eu, zk, Z_FIRST - zk, weg_von=MITTE)
    # Untersicht der Auskragung (Bretter) und Balkenkoepfe unter der Schwelle
    for (a, b) in (((-OX, OVORN), (OX, VORN)), ((-OX, VORN), (-BX, HINTEN)), ((BX, VORN), (OX, HINTEN))):
        lx, ly = b[0] - a[0], b[1] - a[1]
        h.rechteck(holz, (a[0], a[1], Z_OG), (1, 0, 0), (0, 1, 0), lx, ly, "balken_c", laengs="u" if lx > ly else "v", zu=(0, 0, 0))
    for i in range(6):
        x = -OX + 0.2 + i * (2 * OX - 0.4) / 5
        h.kasten(holz, (x - 0.08, OVORN - 0.02, Z_OG - 0.17), (x + 0.08, VORN + 0.02, Z_OG), "balken_c", ohne=("+z", "+y"), laengs="y")
    for seite in (-1, 1):
        for i in range(4):
            y = VORN + 0.5 + i * 1.4
            h.kasten(holz, (min(seite * BX, seite * (OX + 0.02)), y - 0.08, Z_OG - 0.17), (max(seite * BX, seite * (OX + 0.02)), y + 0.08, Z_OG), "balken_c", ohne=("+z", "-x" if seite > 0 else "+x"), laengs="x")

    # ── Fachwerk des Obergeschosses ──────────────────────────────────────────────────────────────────────────────────
    bw = 0.17
    for wand in ("vorn_og", "rechts_og", "hinten_og", "links_og"):
        _, _, breite, n = WAENDE[wand]
        n3 = (n[0], n[1], 0)
        P = lambda s, z: punkt(wand, s, z)
        h.brett(holz, P(-0.05, Z_OG + 0.10), P(breite + 0.05, Z_OG + 0.10), 0.20, n3, "balken_c", tiefe=0.07)          # Schwelle
        h.brett(holz, P(-0.05, Z_TRAUFE - 0.09), P(breite + 0.05, Z_TRAUFE - 0.09), 0.18, n3, "balken_b", tiefe=0.06)  # Raehm
        l = loecher(wand)
        pfosten = {bw / 2, breite - bw / 2}
        for s0, s1, _, _, _ in l: pfosten |= {s0 - bw / 2, s1 + bw / 2}
        # Zwischenstaender, wo das Feld sonst breiter als 1,5 m waere
        fest = sorted(pfosten)
        for a, b in zip(fest[:-1], fest[1:]):
            if b - a > 1.5 and not any(s0 - 0.1 < (a + b) / 2 < s1 + 0.1 for s0, s1, *_ in l): pfosten.add((a + b) / 2)
        for s in sorted(pfosten):
            h.brett(holz, P(s, Z_OG + 0.20), P(s, Z_TRAUFE - 0.18), bw, n3, "balken_a", laengs="v")
        # Brustriegel durchlaufend, Sturzriegel ueber den Fenstern
        zr = 3.35 - 0.07
        h.brett(holz, P(bw, zr), P(breite - bw, zr), 0.14, n3, "balken_b", tiefe=0.045)
        for s0, s1, _, zb, _ in l:
            h.brett(holz, P(s0 - bw, zb + 0.07), P(s1 + bw, zb + 0.07), 0.14, n3, "balken_b", tiefe=0.045)
        # Streben in den Eckfeldern (unten aussen nach oben innen)
        for s_a, s_b in ((bw, 0.62), (breite - bw, breite - 0.62)):
            if any(min(s_a, s_b) - 0.1 < s0 < max(s_a, s_b) + 0.1 or min(s_a, s_b) - 0.1 < s1 < max(s_a, s_b) + 0.1 for s0, s1, *_ in l): continue
            h.brett(holz, P(s_a, Z_OG + 0.22), P(s_b, zr - 0.07), 0.13, n3, "balken_a", tiefe=0.04)
    # Giebelfachwerk: Kehlbalken, Mittelstaender, zwei Streben, Ortgang-Sparren; vorn dazu die Ladeluke (Akzentfarbe)
    for y, n in ((OVORN, (0, -1, 0)), (HINTEN, (0, 1, 0))):
        h.brett(holz, (-xk, y, zk), (xk, y, zk), 0.16, n, "balken_b")
        h.brett(holz, (0, y, zk + 0.08), (0, y, Z_FIRST - 0.25), 0.15, n, "balken_a", laengs="v")
        for s in (-1, 1):
            h.brett(holz, (s * 1.25, y, Z_TRAUFE), (s * 1.25, y, dach_z(1.25) - 0.12), 0.15, n, "balken_a", laengs="v")
            h.brett(holz, (s * (xk - 0.15), y, zk + 0.1), (s * 0.12, y, Z_FIRST - 0.75), 0.12, n, "balken_a", tiefe=0.04)
            h.brett(holz, (s * (OX + 0.02), y, Z_TRAUFE - 0.02), (s * 0.02, y, Z_FIRST - 0.02), 0.16, n, "balken_c", tiefe=0.07)
    for x in (-0.165, 0.165):
        h.brett("Fachwerk", (x, OVORN, 5.20), (x, OVORN, 6.04), 0.33, (0, -1, 0), "farbholz", tiefe=0.06, farbe=ROT)
    h.brett(holz, (-0.42, OVORN, 5.15), (0.42, OVORN, 5.15), 0.11, (0, -1, 0), "balken_b", tiefe=0.07)

    # ── Tuer, Fensterkreuze, Laeden ──────────────────────────────────────────────────────────────────────────────────
    for wand, (a0, a1), (z0, z1), art in FENSTER:
        (ox, oy), (ex, ey), breite, n = WAENDE[wand]
        n3 = Vector((n[0], n[1], 0))
        start = ox if ex else oy
        s0, s1 = a0 - start, a1 - start
        P = lambda s, z, raus=0.0: punkt(wand, s, z, raus)
        eg = wand.endswith("_eg")
        if art == "tuer":
            # Tuerblatt 8 cm zurueckgesetzt, Rahmen und Sturz, zwei Eisenbaender, Steinstufe davor
            for i in range(3):   # drei Bahnen zu je zwei Brettern, Maserung senkrecht
                h.rechteck(holz, P(s0 + i * (s1 - s0) / 3, z0, -0.08), (ex, ey, 0), (0, 0, 1), (s1 - s0) / 3, z1 - z0, "planken", laengs="v", weg_von=MITTE)
            for s in (s0 - 0.07, s1 + 0.07):
                h.brett(holz, P(s, z0), P(s, z1 + 0.14), 0.14, n3, "balken_c", laengs="v")
            h.brett(holz, P(s0 - 0.2, z1 + 0.08), P(s1 + 0.2, z1 + 0.08), 0.17, n3, "balken_c", tiefe=0.07)
            for z in (z0 + 0.45, z1 - 0.4):
                h.rechteck(stein, P(s0 + 0.03, z - 0.035, -0.07), (ex, ey, 0), (0, 0, 1), 0.62, 0.07, "eisen", weg_von=MITTE)
            h.kasten(stein, (a0 - 0.15, VORN - 0.36, -0.12), (a1 + 0.15, VORN, Z_BODEN), "bruchstein", ohne=("-z", "+y"))
            continue
        # Rahmen (unten Fensterbank), Fensterkreuz in der Wandmitte
        h.brett(holz, P(s0 - 0.12, z0 - 0.05), P(s1 + 0.12, z0 - 0.05), 0.10, n3, "balken_c", tiefe=0.08)
        if eg:
            h.brett(holz, P(s0 - 0.12, z1 + 0.06), P(s1 + 0.12, z1 + 0.06), 0.12, n3, "balken_c", tiefe=0.06)
            for s in (s0 - 0.06, s1 + 0.06):
                h.brett(holz, P(s, z0), P(s, z1), 0.12, n3, "balken_c", laengs="v")
        else:
            # im Fachwerk sitzt ein schmaler Blendrahmen in der Oeffnung
            for a, b in ((P(s0 + 0.025, z0, -0.03), P(s0 + 0.025, z1, -0.03)), (P(s1 - 0.025, z0, -0.03), P(s1 - 0.025, z1, -0.03)), (P(s0, z1 - 0.025, -0.03), P(s1, z1 - 0.025, -0.03))):
                h.brett(holz, a, b, 0.05, n3, "balken_b", tiefe=0.03, seiten=False)
        sm, zm = (s0 + s1) / 2, (z0 + z1) / 2
        h.rechteck(holz, P(sm - 0.025, z0, -T / 2 + 0.012), (ex, ey, 0), (0, 0, 1), 0.05, z1 - z0, "balken_c", laengs="v", weg_von=MITTE, anteil=(0.2, 0.6))
        h.rechteck(holz, P(s0, zm - 0.025, -T / 2 + 0.012), (ex, ey, 0), (0, 0, 1), s1 - s0, 0.05, "balken_c", weg_von=MITTE, anteil=(0.2, 0.6))
        # Laeden: vorn zwei Fluegel, sonst einer; offen an der Wand, Eckenfarbe Rot = Akzentfarbe je Haus
        lb = (s1 - s0) / 2
        fluegel = [(s0 - lb - 0.03, s0 - 0.03), (s1 + 0.03, s1 + lb + 0.03)] if wand.startswith("vorn") else [(s1 + 0.03, s1 + 2 * lb * 0.55 + 0.03)]
        for f0, f1 in fluegel:
            if f0 < 0.02 or f1 > breite - 0.02: continue
            vor = 0.085 if not eg else 0.04
            h.rechteck(holz, P(f0, z0 - 0.02, vor), (ex, ey, 0), (0, 0, 1), f1 - f0, z1 - z0 + 0.04, "farbholz", laengs="v", weg_von=MITTE, farbe=ROT)
            for z in (z0 + 0.12, z1 - 0.12):
                h.rechteck(stein, P(f0, z - 0.03, vor + 0.006), (ex, ey, 0), (0, 0, 1), f1 - f0, 0.06, "eisen", weg_von=MITTE)

    # ── Eckquader im Erdgeschoss (Bruchstein schaut unter dem Putz hervor), Knaggen unter der Auskragung, Bank neben der Tuer ──
    for sx in (-1, 1):
        for y, sy in ((VORN, -1), (HINTEN, 1)):
            hoehen = [(Z_SOCKEL, 0.95, 0.42), (0.95, 1.40, 0.26), (1.40, 1.92, 0.38), (1.92, Z_OG - 0.17, 0.24)]
            for i, (za, zb, lang) in enumerate(hoehen):
                lx, ly = (lang, 0.2) if i % 2 == 0 else (0.2, lang)
                h.rechteck(stein, (sx * BX - (lx if sx > 0 else 0), y + sy * 0.022, za), (1, 0, 0), (0, 0, 1), lx, zb - za, "bruchstein", weg_von=MITTE, anteil=(0.05 + 0.2 * (i % 3), 0.5 + 0.2 * (i % 3)))
                h.rechteck(stein, (sx * (BX + 0.022), y - (ly if sy > 0 else 0), za), (0, 1, 0), (0, 0, 1), ly, zb - za, "bruchstein", weg_von=MITTE, anteil=(0.05 + 0.2 * (i % 3), 0.5 + 0.2 * (i % 3)))
    for sx in (-1, 1):
        for y in (VORN + 0.12, HINTEN - 0.12):       # Knaggen an den Seiten
            a, b, c = Vector((sx * BX, y, Z_OG - 0.62)), Vector((sx * BX, y, Z_OG - 0.17)), Vector((sx * (OX - 0.02), y, Z_OG - 0.17))
            for dy in (-0.07, 0.07):
                h.vieleck(holz, [a + Vector((0, dy, 0)), b + Vector((0, dy, 0)), c + Vector((0, dy, 0))], [(0.1, 0.49), (0.1, 0.43), (0.2, 0.43)], weg_von=(a + c) / 2)
            h.vieleck(holz, [a + Vector((0, -0.07, 0)), a + Vector((0, 0.07, 0)), c + Vector((0, 0.07, 0)), c + Vector((0, -0.07, 0))], [(0.3, 0.49), (0.3, 0.44), (0.45, 0.44), (0.45, 0.49)], weg_von=b)
        a, b, c = Vector((sx * (BX - 0.12), VORN, Z_OG - 0.62)), Vector((sx * (BX - 0.12), VORN, Z_OG - 0.17)), Vector((sx * (BX - 0.12), OVORN + 0.02, Z_OG - 0.17))
        for dx in (-0.07, 0.07):
            h.vieleck(holz, [a + Vector((dx, 0, 0)), b + Vector((dx, 0, 0)), c + Vector((dx, 0, 0))], [(0.1, 0.49), (0.1, 0.43), (0.2, 0.43)], weg_von=(a + c) / 2)
        h.vieleck(holz, [a + Vector((-0.07, 0, 0)), a + Vector((0.07, 0, 0)), c + Vector((0.07, 0, 0)), c + Vector((-0.07, 0, 0))], [(0.3, 0.49), (0.3, 0.44), (0.45, 0.44), (0.45, 0.49)], weg_von=b)
    # Bank unter dem Fenster an der Gasse
    h.kasten(holz, (-1.5, VORN - 0.38, 0.42), (-0.2, VORN - 0.06, 0.48), "balken_b", laengs="x")
    for x in (-1.36, -0.34):
        h.kasten(holz, (x - 0.05, VORN - 0.34, 0.0), (x + 0.05, VORN - 0.10, 0.42), "balken_c", ohne=("-z", "+z"), laengs="z")
    # Scheitholz an der rechten Seitenwand unter der Auskragung, Regenfass an der linken vorderen Ecke
    for i, (y, z) in enumerate([(0.55 + 0.19 * k, 0.09) for k in range(5)] + [(0.64 + 0.19 * k, 0.26) for k in range(4)] + [(0.74 + 0.19 * k, 0.43) for k in range(3)]):
        h.stab(holz, (BX + 0.03, y, z), (BX + 0.62, y + 0.02 * (i % 3 - 1), z), 0.17, "balken_a", kappen=False)
        h.rechteck(holz, (BX + 0.62, y - 0.085, z - 0.085), (0, 1, 0), (0, 0, 1), 0.17, 0.17, "farbholz", weg_von=MITTE, anteil=(0.1, 0.9), u_bereich=(0.1 + 0.07 * i, 0.14 + 0.07 * i))
    h.dreh(holz, (-BX - 0.36, VORN + 0.45, 0.0), [(0.25, 0.0), (0.31, 0.3), (0.31, 0.5), (0.26, 0.8)], "planken", seiten=10, deckel=False, gedreht=True)
    h.dreh(stein, (-BX - 0.36, VORN + 0.45, 0.72), [(0.255, 0.0), (0.0, 0.0)], "eisen", seiten=8, deckel=False, anteil=(0.0, 0.3))
    for z in (0.18, 0.62):
        h.dreh(stein, (-BX - 0.36, VORN + 0.45, z), [(0.305 if z < 0.5 else 0.30, 0.0), (0.305 if z < 0.5 else 0.295, 0.05)], "eisen", seiten=8, deckel=False)
    # Tuerring
    tx = next(f for f in FENSTER if f[3] == "tuer")[1]
    h.rechteck(stein, (tx[0] + 0.12, VORN + 0.072, 1.05), (1, 0, 0), (0, 0, 1), 0.1, 0.14, "eisen", weg_von=MITTE)

    # ── Schornstein ueber dem Dach (innen laeuft der Schlot in derselben Lage) ───────────────────────────────────────
    x0, x1, y0, y1 = KAMIN
    zu, zo = dach_z(x1, True) - 0.25, Z_FIRST - 0.35
    z = zu
    while z < zo - 1e-3:
        zn = min(z + 0.5, zo)
        h.kasten(stein, (x0, y0, z), (x1, y1, zn), "bruchstein", ohne=("-z", "+z"), laengs="x")
        z = zn
    h.kasten(stein, (x0 - 0.05, y0 - 0.05, zo), (x1 + 0.05, y1 + 0.05, zo + 0.12), "bruchstein", laengs="x")
    h.rechteck(stein, (x0 + 0.1, y0 + 0.1, zo + 0.125), (1, 0, 0), (0, 1, 0), x1 - x0 - 0.2, y1 - y0 - 0.2, "eisen", zu=(0, 0, 99), anteil=(0.0, 0.2))
    return h


def baue_dach():
    d = Teil("Dach", BAENDER_AUSSEN, saat=23)
    xe = OX + UEBER                                   # Traufe
    laenge = DACH_HINTEN - DACH_VORN
    stufen, felder = 3, 7                             # drei ueberlappende Stufen, entlang des Firsts in sieben Felder geteilt
    dick_unten, dick_oben = 0.30, 0.15                # jede Stufe ist ein Keil: unten dick, oben duenn (dort deckt die naechste)
    ey = Vector((0, 1, 0))
    ys = [DACH_VORN + laenge * j / felder for j in range(felder + 1)]
    v0, v1, m = BAENDER_AUSSEN["stroh"]
    f0, f1, fm = BAENDER_AUSSEN["stroh_franse"]
    for seite in (-1, 1):
        def P(a, hoch, y=0.0):
            """Punkt auf der Dachseite: a = 0 an der Traufe, 1 am First; `hoch` senkrecht ueber der Unterseite."""
            x = seite * xe * (1 - a)
            return Vector((x, y, dach_z(x) + hoch))
        # Untersicht (von innen die Decke): Stroh, dunkel durch den oberen Rand des Bandes
        d.vieleck("Stroh", [P(0, 0, DACH_VORN), P(0, 0, DACH_HINTEN), P(1, 0, DACH_HINTEN), P(1, 0, DACH_VORN)],
                  [(0, 0.02), (laenge / 4, 0.02), (laenge / 4, 0.07), (0, 0.07)], zu=(0, 0, 0))
        hang = (P(0, 0) - P(1, 0)).normalized() * 0.75 + Vector((0, 0, -0.25))
        for k in range(stufen):
            a0, a1 = k / stufen, min(1.0, (k + 1) / stufen + 0.02)     # die Stufe reicht etwas unter die naechste
            u0 = d.zufall.random()
            # die Schnittkante ist nicht schnurgerade: je Feld etwas hoeher oder tiefer, etwas vor oder zurueck
            wellen = [(d.zufall.uniform(-0.022, 0.022), d.zufall.uniform(-0.006, 0.006)) for _ in ys]
            unten = [P(a0 + da, dick_unten + dz, y) for y, (dz, da) in zip(ys, wellen)]
            oben = [P(a1, dick_oben, y) for y in ys]
            fuss = [P(a0 + da, 0.0 if k == 0 else dick_oben - 0.01, y) for y, (dz, da) in zip(ys, wellen)]
            for j in range(felder):
                ua, ub = u0 + (ys[j] - DACH_VORN) / m, u0 + (ys[j + 1] - DACH_VORN) / m
                d.vieleck("Stroh", [unten[j], unten[j + 1], oben[j + 1], oben[j]], [(ua, v1), (ub, v1), (ub, v0), (ua, v0)], weg_von=MITTE)
                # Stirn der Stufe (Schnittkante des Strohs)
                d.vieleck("Stroh", [fuss[j], fuss[j + 1], unten[j + 1], unten[j]], [(ua, v0 + 0.01), (ub, v0 + 0.01), (ub, v0 + 0.035), (ua, v0 + 0.035)], weg_von=MITTE + Vector((0, 0, 1)))
            for j, aussen in ((0, -1), (felder, 1)):       # Giebelseiten
                sl = (oben[j] - unten[j]).length
                d.vieleck("Stroh", [P(a0, 0, ys[j]), P(a1, 0, ys[j]), oben[j], unten[j]],
                          [(u0, v0 + 0.03), (u0 + sl / m, v0 + 0.03), (u0 + sl / m, v0 + 0.06), (u0, v0 + 0.085)], weg_von=(0, ys[j] - aussen, 5))
            # Alpha-Cards unter der Stufenkante: haengen steiler als das Dach, unten ausgefranst (zwei Karten je Reihe)
            tief = 0.27 if k == 0 else 0.21
            for ja, jb in ((0, 4), (4, felder)):
                ka, kb = unten[ja] + Vector((0, 0, -0.03)), unten[jb] + Vector((0, 0, -0.03))
                ua, ub = u0 + (ys[ja] - DACH_VORN) / fm, u0 + (ys[jb] - DACH_VORN) / fm
                d.vieleck("Strohkante", [ka, kb, kb + hang * tief, ka + hang * tief], [(ua, f0), (ub, f0), (ub, f1), (ua, f1)], weg_von=MITTE)
            if k == 0:   # zweite Reihe an der Traufe, etwas zurueckgesetzt und versetzt
                k2a, k2b = P(0.035, dick_unten * 0.5, DACH_VORN), P(0.035, dick_unten * 0.5, DACH_HINTEN)
                d.vieleck("Strohkante", [k2a, k2b, k2b + hang * 0.3, k2a + hang * 0.3],
                          [(u0 + 0.37, f0), (u0 + 0.37 + laenge / fm, f0), (u0 + 0.37 + laenge / fm, f1), (u0 + 0.37, f1)], weg_von=MITTE)
        # Alpha-Cards am Ortgang (Giebelkante): haengen aussen herunter
        for y, aussen in ((DACH_VORN, -1), (DACH_HINTEN, 1)):
            a, b = P(0, dick_unten * 0.8, y), P(1, dick_oben, y)
            ab = Vector((0, aussen * 0.05, -0.22))
            sl = (b - a).length
            d.vieleck("Strohkante", [a, b, b + ab, a + ab], [(0.2, f0), (0.2 + sl / fm, f0), (0.2 + sl / fm, f1), (0.2, f1)], weg_von=(0, y - aussen, 5))
        # Sparrenkoepfe unter der Traufe
        for j in range(6):
            y = DACH_VORN + 0.45 + j * (laenge - 0.9) / 5
            x_innen, x_aussen = seite * (OX + 0.01), seite * (xe - 0.07)
            for dy in (-0.05, 0.05):
                d.vieleck("Fachwerk", [(x_innen, y + dy, dach_z(x_innen) - 0.13), (x_aussen, y + dy, dach_z(x_aussen) - 0.13), (x_aussen, y + dy, dach_z(x_aussen) - 0.005), (x_innen, y + dy, dach_z(x_innen) - 0.005)],
                          [(0.1, 0.43), (0.2, 0.43), (0.2, 0.47), (0.1, 0.47)], weg_von=(x_innen, y, 0))
            d.vieleck("Fachwerk", [(x_innen, y - 0.05, dach_z(x_innen) - 0.13), (x_aussen, y - 0.05, dach_z(x_aussen) - 0.13), (x_aussen, y + 0.05, dach_z(x_aussen) - 0.13), (x_innen, y + 0.05, dach_z(x_innen) - 0.13)],
                      [(0.3, 0.43), (0.4, 0.43), (0.4, 0.47), (0.3, 0.47)], zu=(x_innen, y, 0))
    # First: Wulst aus zwei Flaechen ueber der obersten Stufe (in Feldern, leicht durchhaengend), mit Fransen nach beiden Seiten
    zf = Z_FIRST + dick_oben
    sack = [0.0, -0.02, -0.045, -0.055, -0.05, -0.035, -0.015, 0.0]
    for seite in (-1, 1):
        kamm = [Vector((0, y + (-0.02 if j == 0 else 0.02 if j == felder else 0), zf + 0.20 + sack[j])) for j, y in enumerate(ys)]
        rand = [Vector((seite * 0.42, k.y, zf - 0.42 * NEIGUNG + 0.16 + sack[j])) for j, k in enumerate(kamm)]
        for j in range(felder):
            ua, ub = 0.5 + (ys[j] - DACH_VORN) / m, 0.5 + (ys[j + 1] - DACH_VORN) / m
            d.vieleck("Stroh", [rand[j], rand[j + 1], kamm[j + 1], kamm[j]], [(ua, v0 + 0.06), (ub, v0 + 0.06), (ub, v0 + 0.11), (ua, v0 + 0.11)], weg_von=MITTE)
        ab = Vector((seite * 0.10, 0, -0.17))
        d.vieleck("Strohkante", [rand[0], rand[-1], rand[-1] + ab, rand[0] + ab], [(0.6, f0), (0.6 + laenge / fm, f0), (0.6 + laenge / fm, f1), (0.6, f1)], weg_von=MITTE)
        for j, aussen in ((0, -1), (felder, 1)):
            d.vieleck("Stroh", [rand[j], kamm[j], Vector((0, kamm[j].y, zf - 0.05))], [(0.1, v0 + 0.03), (0.2, v0 + 0.03), (0.15, v0 + 0.06)], weg_von=(0, kamm[j].y - aussen, 5))
    # Windbretter: am Giebel gekreuzte Bretter ueber dem First (halten das Stroh am Ortgang)
    for y in (DACH_VORN - 0.03, DACH_HINTEN + 0.03):
        for seite in (-1, 1):
            a = Vector((seite * 0.45, y, dach_z(0.45) + dick_oben + 0.06)); b = Vector((-seite * 0.16, y, zf + 0.42))
            d.brett("Fachwerk", a, b, 0.09, (0, -1 if y < 0 else 1, 0), "balken_c", tiefe=0.025)
            d.brett("Fachwerk", a, b, 0.09, (0, 1 if y < 0 else -1, 0), "balken_c", tiefe=0.0, seiten=False)
    return d


def scheibe(teil, stoff, wand, a0, a1, z0, z1, raus, **kw):
    """Scheibe in der Oeffnung; UV in Metern (x entlang der Wand ab der linken Kante der Oeffnung, y ab der Fensterbank):
    Danach teilt der Butzen-Shader die Scheibe – innen und aussen gleich, weil beide dieselbe Wandkoordinate benutzen."""
    (ox, oy), (ex, ey), _, _ = WAENDE[wand]
    start = ox if ex else oy
    b, hoch = a1 - a0, z1 - z0
    pts = [punkt(wand, a0 - start, z0, raus), punkt(wand, a1 - start, z0, raus), punkt(wand, a1 - start, z1, raus), punkt(wand, a0 - start, z1, raus)]
    teil.vieleck(stoff, pts, [(0, 1), (b, 1), (b, 1 - hoch), (0, 1 - hoch)], **kw)   # objekt() spiegelt v: im Spiel ist UV.y die Hoehe ueber der Bank


def baue_scheiben():
    """Die Scheiben mit dem Material „Fenster“ (Fensterlicht bei Nacht, Fensterstellen fuer den Brand) und davor das Bleinetz der
    Butzenscheiben: eigener Knoten, den die Innenansicht ausblendet (von innen zeichnet das Glas der Innenansicht dieselbe Teilung)."""
    s = Teil("Scheiben", BAENDER_AUSSEN, saat=5)
    for wand, (a0, a1), (z0, z1), art in FENSTER:
        if art != "fenster": continue
        scheibe(s, "Fenster", wand, a0, a1, z0, z1, -T / 2, weg_von=MITTE)
        scheibe(s, "Bleinetz", wand, a0, a1, z0, z1, -T / 2 + 0.006, weg_von=MITTE)
    return s


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Innen
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

IX, IV, IH = BX - T, VORN + T, HINTEN - T             # Erdgeschoss innen
JX, JV = OX - T, OVORN + T                            # Obergeschoss innen
RAUM = Vector((0, -0.3, 1.6))
Z_GALERIE = Z_OG + 0.12                               # Oberkante der Galeriebohlen


def feld(name):
    """UV.x-Bereich eines Farbfelds im Band „kleinteile“ (mit Rand, damit nichts vom Nachbarn hereinblutet)."""
    i = FELDER_INNEN.index(name); n = len(FELDER_INNEN)
    return ((i + 0.15) / n, (i + 0.85) / n)


def baue_raum():
    r = Teil("Raum", BAENDER_INNEN, saat=31)
    S = "Innen"
    innen = {"vorn_eg": ((-IX, IV), (1, 0), 2 * IX), "hinten_eg": ((-IX, IH), (1, 0), 2 * IX), "links_eg": ((-IX, IV), (0, 1), IH - IV), "rechts_eg": ((IX, IV), (0, 1), IH - IV),
             "vorn_og": ((-JX, JV), (1, 0), 2 * JX), "hinten_og": ((-JX, IH), (1, 0), 2 * JX), "links_og": ((-JX, JV), (0, 1), IH - JV), "rechts_og": ((JX, JV), (0, 1), IH - JV)}
    zw = dach_z(JX)                                    # die Seitenwaende reichen innen bis unter das Dach
    for wand, ((ox, oy), (ex, ey), breite) in innen.items():
        (ax, ay), _, _, _ = WAENDE[wand]
        versatz = (ox - ax) if ex else (oy - ay)       # die Innenwand beginnt um T (bzw. 0) spaeter als die Aussenwand
        l = [(a - versatz, b - versatz, c, d) for a, b, c, d, _ in loecher(wand)]
        eg = wand.endswith("_eg")
        z0, z1 = (Z_BODEN, Z_OG) if eg else (Z_OG, zw if "links" in wand or "rechts" in wand else Z_TRAUFE)
        r.wand(S, (ox, oy, 0), (ex, ey, 0), breite, z0, z1, "lehmwand", loecher=l, zbasis=z0, hoehe=2.4, zu=RAUM + Vector((0, 0, 0 if eg else 2.2)))
    # Giebel innen bis unter das Dach
    for y in (JV, IH):
        r.welt(S, [(-JX, y, Z_TRAUFE), (JX, y, Z_TRAUFE), (JX, y, zw), (0, y, Z_FIRST), (-JX, y, zw)][:4], "lehmwand", (-JX, y, 0), (1, 0, 0), Z_TRAUFE, 3.4, zu=RAUM + Vector((0, 0, 3)))
        r.welt(S, [(-JX, y, zw), (0, y, Z_FIRST), (-JX, y, Z_TRAUFE)], "lehmwand", (-JX, y, 0), (1, 0, 0), Z_TRAUFE, 3.4, zu=RAUM + Vector((0, 0, 3)))
    # Absatz zwischen Erd- und Obergeschoss (die Wand springt um die Auskragung nach aussen): Balkenlage von oben
    for (a, b) in (((-JX, JV), (JX, IV)), ((-JX, IV), (-IX, IH)), ((IX, IV), (JX, IH))):
        r.rechteck(S, (a[0], a[1], Z_OG), (1, 0, 0), (0, 1, 0), b[0] - a[0], b[1] - a[1], "balken", laengs="u" if b[0] - a[0] > b[1] - a[1] else "v", zu=(0, 0, 99))
    # Boden: gestampfter Lehm, zwei Binsenmatten
    r.rechteck(S, (-IX, IV, Z_BODEN), (1, 0, 0), (0, 1, 0), 2 * IX, IH - IV, "lehmboden", zu=(0, 0, 99), u0=0.0)
    for (x0, y0, lx, ly) in ((-1.15, -2.0, 1.9, 1.75), (-0.5, 0.75, 1.6, 1.0)):
        r.rechteck(S, (x0, y0, Z_BODEN + 0.012), (1, 0, 0), (0, 1, 0), lx, ly, "stroh", zu=(0, 0, 99))
    # Fenster von innen: dasselbe Kreuz wie aussen (gleiche Lage in der Wandmitte, gleiche Breite), Rahmen und Bank auf der Innenwand
    for wand, (a0, a1), (z0, z1), art in FENSTER:
        if art != "fenster": continue
        (ox, oy), (ex, ey), _, (nx, ny) = WAENDE[wand]
        start = ox if ex else oy
        s0, s1 = a0 - start, a1 - start
        sm, zm = (s0 + s1) / 2, (z0 + z1) / 2
        ziel = punkt(wand, sm, zm, -2.0)                                        # ein Punkt im Raum vor dem Fenster
        r.rechteck(S, punkt(wand, sm - 0.025, z0, -T / 2 - 0.012), (ex, ey, 0), (0, 0, 1), 0.05, z1 - z0, "balken", laengs="v", zu=ziel, anteil=(0.2, 0.6))
        r.rechteck(S, punkt(wand, s0, zm - 0.025, -T / 2 - 0.012), (ex, ey, 0), (0, 0, 1), s1 - s0, 0.05, "balken", zu=ziel, anteil=(0.2, 0.6))
        n_innen = (-nx, -ny, 0)
        for a, b, breite in ((punkt(wand, s0 - 0.10, z0 - 0.04, -T), punkt(wand, s1 + 0.10, z0 - 0.04, -T), 0.08),      # Bank
                             (punkt(wand, s0 - 0.10, z1 + 0.04, -T), punkt(wand, s1 + 0.10, z1 + 0.04, -T), 0.08),      # Sturz
                             (punkt(wand, s0 - 0.05, z0, -T), punkt(wand, s0 - 0.05, z1, -T), 0.10),
                             (punkt(wand, s1 + 0.05, z0, -T), punkt(wand, s1 + 0.05, z1, -T), 0.10)):
            r.brett(S, a, b, breite, n_innen, "balken", tiefe=0.035)
    # Tuerblatt von innen (Bretter), Tuersturz
    for wand, (a0, a1), (z0, z1), art in FENSTER:
        if art == "tuer":
            r.rechteck(S, (a0, VORN + 0.10, z0), (1, 0, 0), (0, 0, 1), a1 - a0, z1 - z0, "bohlen", laengs="v", zu=RAUM)
            r.kasten(S, (a0 - 0.1, IV - 0.02, z1), (a1 + 0.1, IV + 0.05, z1 + 0.14), "balken", ohne=("-y",))
    # Sichtbares Fachwerk innen: Eckstaender, Staender an den Seitenwaenden, Raehm unter dem Absatz
    bw = 0.16
    for sx in (-1, 1):
        for y in (IV, IH):
            sy = 1 if y == IV else -1
            r.kasten(S, (min(sx * IX, sx * (IX - bw)), min(y, y + sy * bw), Z_BODEN), (max(sx * IX, sx * (IX - bw)), max(y, y + sy * bw), Z_OG), "balken", ohne=("-z", "+z"), laengs="z")
        for y in (-0.85, 0.75):
            if sx > 0 and KAMIN[2] - 0.2 < y < KAMIN[3] + 0.2: continue
            r.kasten(S, (min(sx * IX, sx * (IX - 0.07)), y - bw / 2, Z_BODEN), (max(sx * IX, sx * (IX - 0.07)), y + bw / 2, Z_OG), "balken", ohne=("-z", "+z", "+x" if sx > 0 else "-x"), laengs="z")
        r.kasten(S, (min(sx * IX, sx * (IX - 0.09)), IV, Z_OG - 0.18), (max(sx * IX, sx * (IX - 0.09)), IH, Z_OG), "balken", ohne=("+z", "+x" if sx > 0 else "-x", "-y", "+y"), laengs="y")
        # Staender im Obergeschoss
        for y in (JV + 0.08, -0.6, 0.75, IH - 0.08):
            r.kasten(S, (min(sx * JX, sx * (JX - 0.06)), y - bw / 2, Z_OG), (max(sx * JX, sx * (JX - 0.06)), y + bw / 2, zw - 0.05), "balken", ohne=("-z", "+z", "+x" if sx > 0 else "-x"), laengs="z")
    for y, n in ((IV, 1), (IH, -1)):
        r.kasten(S, (-IX, min(y, y + n * 0.09), Z_OG - 0.18), (IX, max(y, y + n * 0.09), Z_OG), "balken", ohne=("+z", "-x", "+x", "-y" if n > 0 else "+y"), laengs="x")
    # Schlafgalerie: Bohlen auf drei Deckenbalken, Brustholz mit drei Pfosten
    r.kasten(S, (-JX, GALERIE_AB, Z_OG), (JX, IH, Z_GALERIE), "bohlen", ohne=("+y", "-x", "+x"), laengs="x")
    for y in (GALERIE_AB + 0.09, (GALERIE_AB + IH) / 2, IH - 0.2):
        r.kasten(S, (-IX, y - 0.09, Z_OG - 0.2), (IX, y + 0.09, Z_OG), "balken", ohne=("+z", "-x", "+x"), laengs="x")
    r.stab(S, (-0.95, GALERIE_AB + 0.06, Z_GALERIE + 0.85), (JX, GALERIE_AB + 0.06, Z_GALERIE + 0.85), 0.08, "balken")
    for x in (-0.95, 0.6, JX - 0.05):
        r.stab(S, (x, GALERIE_AB + 0.06, Z_GALERIE), (x, GALERIE_AB + 0.06, Z_GALERIE + 0.85), 0.08, "balken")
    # Leiter zur Galerie (links)
    fuss, kopf = Vector((-1.5, GALERIE_AB - 0.95, Z_BODEN)), Vector((-1.5, GALERIE_AB - 0.02, Z_GALERIE + 0.35))
    for dx in (-0.24, 0.24):
        r.stab(S, fuss + Vector((dx, 0, 0)), kopf + Vector((dx, 0, 0)), 0.06, "balken", breite=0.09, kappen=True)
    for i in range(8):
        p = fuss.lerp(kopf, (i + 0.8) / 9.2)
        r.stab(S, p + Vector((-0.24, 0, 0)), p + Vector((0.24, 0, 0)), 0.045, "bohlen")
    # Dachstuhl: fuenf Sparrenpaare, Firstpfette, zwei Kehlbalken
    for y in (JV + 0.3, -1.45, -0.3, 0.9, IH - 0.3):
        for sx in (-1, 1):
            r.stab(S, (sx * JX, y, zw - 0.07), (0, y, Z_FIRST - 0.07), 0.12, "balken", breite=0.10)
    r.stab(S, (0, JV, Z_FIRST - 0.12), (0, IH, Z_FIRST - 0.12), 0.14, "balken")
    for y in (-1.45, 0.9):
        zk = Z_TRAUFE + 1.45
        r.stab(S, (-(OX - 1.45 / NEIGUNG) + 0.1, y, zk), ((OX - 1.45 / NEIGUNG) - 0.1, y, zk), 0.12, "balken")
    # Schlot: Bruchstein von der Herdhaube bis durchs Dach (gleiche Lage wie der Schornstein aussen)
    x0, x1, y0, y1 = KAMIN
    z = 1.9
    zo = dach_z(x0) + 0.05
    while z < zo - 1e-3:
        zn = min(z + 0.6, zo)
        r.kasten(S, (x0, y0, z), (JX if z >= Z_OG else IX, y1, zn), "bruchstein", ohne=("-z", "+z", "+x"), laengs="y")
        z = zn
    return r


def baue_glas():
    g = Teil("Glas", BAENDER_INNEN, saat=7)
    for wand, (a0, a1), (z0, z1), art in FENSTER:
        if art == "fenster": scheibe(g, "Innenglas", wand, a0, a1, z0, z1, -T / 2 - 0.004, zu=RAUM)
    return g


def baue_herd():
    """Herd-Set: offene Bruchstein-Herdstelle mit Dreibein-Kessel, Zwiebelzoepfen, Kraeuterbuendeln und Hackklotz."""
    c = Teil("Herd_Set", BAENDER_INNEN, saat=41)
    S = "Innen"
    x0, x1, y0, y1 = KAMIN
    hy0, hy1 = y0 - 0.3, y1 + 0.3
    c.kasten(S, (1.0, hy0, Z_BODEN), (IX, hy1, 0.5), "bruchstein", ohne=("-z", "+x"), laengs="y")
    c.kasten(S, (x0 - 0.1, hy0, 0.5), (IX, hy0 + 0.2, 1.7), "bruchstein", ohne=("-z", "+z", "+x"), laengs="z")   # Wange zur Gassenwand
    # zum Raum hin offen: die Haube ruht vorn auf einem Pfosten, so sieht man das Feuer vom Tisch und von hinten im Raum
    c.kasten(S, (x0 - 0.12, hy1 - 0.14, 0.5), (x0 + 0.02, hy1, 1.7), "balken", ohne=("-z", "+z"), laengs="z")
    c.kasten(S, (x0 - 0.16, hy0 - 0.04, 1.7), (IX, hy1 + 0.04, 1.9), "balken", ohne=("+x",), laengs="y")
    c.rechteck(S, (x0 - 0.1, hy0 + 0.2, 0.5), (0, 1, 0), (0, 0, 1), hy1 - hy0 - 0.4, 1.2, "kleinteile", u_bereich=feld("russ"), zu=RAUM)   # verrusste Rueckwand
    c.rechteck(S, (IX - 0.01, hy0 + 0.2, 0.5), (0, 1, 0), (0, 0, 1), hy1 - hy0 - 0.4, 1.2, "bruchstein", zu=RAUM)
    ua, ub = feld("glut")
    v0, v1, _ = BAENDER_INNEN["kleinteile"]
    c.vieleck(S, [(1.22, -1.68, 0.512), (1.78, -1.68, 0.512), (1.78, -1.12, 0.512), (1.22, -1.12, 0.512)], [(ua, v1), (ub, v1), (ub, v0), (ua, v0)], zu=(0, 0, 99))
    for a, b in (((1.25, -1.62, 0.56), (1.75, -1.2, 0.58)), ((1.3, -1.15, 0.56), (1.72, -1.6, 0.62)), ((1.15, -1.4, 0.55), (1.8, -1.42, 0.6))):
        c.stab(S, a, b, 0.09, "balken")
    # Dreibein mit Kessel
    spitze = Vector((1.5, -1.4, 1.45))
    for w in (90, 210, 330):
        c.stab(S, (1.5 + 0.34 * math.cos(math.radians(w)), -1.4 + 0.34 * math.sin(math.radians(w)), 0.5), spitze, 0.03, "kleinteile", u_bereich=feld("eisen"))
    ue = feld("eisen")
    c.dreh(S, (1.5, -1.4, 0.78), [(0.0, 0.0), (0.13, 0.03), (0.19, 0.14), (0.17, 0.27), (0.19, 0.30)], "kleinteile", seiten=6, deckel=False, u_bereich=ue)
    c.dreh(S, (1.5, -1.4, 1.06), [(0.17, 0.0), (0.0, 0.01)], "kleinteile", seiten=6, deckel=False, u_bereich=feld("russ"))
    c.stab(S, (1.5, -1.4, 1.08), spitze, 0.02, "kleinteile", u_bereich=feld("eisen"))
    return c


def baue_vorrat():
    """Vorrat-Set: Zwiebelzoepfe und Kraeuterbuendel an der Wand neben dem Herd, Hackklotz mit Scheiten."""
    c = Teil("Vorrat_Set", BAENDER_INNEN, saat=59)
    S = "Innen"
    x0, x1, y0, y1 = KAMIN
    hy0, hy1 = y0 - 0.3, y1 + 0.3
    # Zwiebelzoepfe und Kraeuterbuendel an der Wand neben dem Herd
    for (y, farbe, n) in ((hy1 + 0.22, "zwiebel_rot", 4), (hy1 + 0.42, "zwiebel_gelb", 4)):
        for i in range(n):
            m = Vector((IX - 0.055 - 0.02 * (i % 2), y + 0.028 * ((i % 2) * 2 - 1), 2.0 - 0.082 * i))
            c.dreh(S, m, [(0.0, -0.045), (0.03, -0.035), (0.046, -0.005), (0.034, 0.028), (0.0, 0.052)], "kleinteile", seiten=5, deckel=False, u_bereich=feld(farbe))
        c.stab(S, (IX - 0.06, y, 2.03), (IX - 0.03, y, 2.25), 0.015, "stroh")
    for y in (hy0 - 0.3, hy0 - 0.55):
        c.dreh(S, (IX - 0.09, y, 1.72), [(0.0, 0.0), (0.085, 0.07), (0.05, 0.3), (0.012, 0.42)], "kleinteile", seiten=5, deckel=False, u_bereich=feld("kraut"))
    # Hackklotz mit Scheiten
    c.dreh(S, (1.55, -0.45, Z_BODEN), [(0.23, 0.0), (0.22, 0.42)], "kleinteile", seiten=8, u_bereich=feld("rinde"))
    for a, b in (((1.2, -0.72, Z_BODEN + 0.05), (1.75, -0.74, Z_BODEN + 0.05)), ((1.25, -0.62, Z_BODEN + 0.14), (1.78, -0.66, Z_BODEN + 0.14))):
        c.stab(S, a, b, 0.1, "balken", kappen=True)
    return c


def baue_tisch():
    """Esstisch-Set: grober Brettertisch mit zwei Baenken, Tonkruege und Holzteller."""
    c = Teil("Esstisch_Set", BAENDER_INNEN, saat=43)
    S = "Innen"
    tx, y0, y1, zt = -0.2, -1.9, -0.4, Z_BODEN + 0.74
    c.kasten(S, (tx - 0.37, y0, zt - 0.05), (tx + 0.37, y1, zt), "bohlen", laengs="y")
    for y in (y0 + 0.22, y1 - 0.22):
        for sx in (-1, 1):
            c.stab(S, (tx + sx * 0.33, y, Z_BODEN), (tx + sx * 0.2, y, zt - 0.05), 0.07, "balken")
        c.stab(S, (tx - 0.27, y, Z_BODEN + 0.3), (tx + 0.27, y, Z_BODEN + 0.3), 0.05, "balken")
    c.stab(S, (tx, y0 + 0.22, Z_BODEN + 0.3), (tx, y1 - 0.22, Z_BODEN + 0.3), 0.05, "balken")
    for bx in (tx - 0.62, tx + 0.62):
        zs = Z_BODEN + 0.44
        c.kasten(S, (bx - 0.14, y0 + 0.05, zs - 0.045), (bx + 0.14, y1 - 0.05, zs), "bohlen", laengs="y")
        for y in (y0 + 0.25, y1 - 0.25):
            for sx in (-1, 1):
                c.stab(S, (bx + sx * 0.13, y, Z_BODEN), (bx + sx * 0.08, y, zs - 0.045), 0.05, "balken")
    krug = [(0.05, 0.0), (0.075, 0.03), (0.09, 0.09), (0.082, 0.15), (0.05, 0.2), (0.046, 0.235), (0.058, 0.26)]
    c.dreh(S, (tx + 0.08, -1.55, zt), krug, "kleinteile", seiten=8, deckel=False, u_bereich=feld("ton"))
    c.dreh(S, (tx - 0.15, -0.8, zt), [(0.036, 0.0), (0.05, 0.035), (0.052, 0.08), (0.047, 0.11)], "kleinteile", seiten=8, deckel=False, u_bereich=feld("ton"))
    for (px, py) in ((tx - 0.18, -1.5), (tx + 0.17, -1.05), (tx - 0.14, -1.12)):
        c.dreh(S, (px, py, zt), [(0.06, 0.0), (0.1, 0.022)], "bohlen", seiten=7, deckel=True, u_bereich=(0.1, 0.16), anteil=(0.2, 0.6))
    return c


def baue_schlafplatz():
    """Schlafplatz-Set (auf der Galerie): zwei Strohsaecke auf niedrigen Holzgestellen mit Wolldecken."""
    c = Teil("Schlafplatz_Set", BAENDER_INNEN, saat=47)
    S = "Innen"
    z = Z_GALERIE
    for (x0, x1) in ((-2.05, -0.3), (0.3, 2.05)):
        y0, y1 = 1.2, 2.08
        c.kasten(S, (x0, y0, z), (x1, y1, z + 0.2), "bohlen", ohne=("-z", "+z"), laengs="x")
        for px in (x0 + 0.04, x1 - 0.04):
            for py in (y0 + 0.04, y1 - 0.04):
                c.kasten(S, (px - 0.04, py - 0.04, z), (px + 0.04, py + 0.04, z + 0.27), "balken", ohne=("-z",), laengs="z")
        c.kasten(S, (x0 + 0.05, y0 + 0.05, z + 0.12), (x1 - 0.05, y1 - 0.05, z + 0.33), "sackleinen", ohne=("-z",), laengs="x")
        kopf = x0 if x0 < 0 else x1
        r = 1 if x0 < 0 else -1
        c.kasten(S, (min(kopf + r * 0.1, kopf + r * 0.5), y0 + 0.14, z + 0.33), (max(kopf + r * 0.1, kopf + r * 0.5), y1 - 0.14, z + 0.42), "sackleinen", ohne=("-z",), laengs="y")
        c.kasten(S, (min(kopf + r * 0.62, kopf + r * 1.72), y0 + 0.02, z + 0.31), (max(kopf + r * 0.62, kopf + r * 1.72), y1 - 0.02, z + 0.37), "wolle", ohne=("-z",), laengs="x")
        # Stroh quillt am Fussende heraus
        c.rechteck(S, (kopf + r * 1.7 if r > 0 else kopf + r * 1.74, y0 + 0.1, z + 0.2), (1, 0, 0), (0, 1, 0), 0.04, y1 - y0 - 0.2, "stroh", zu=(0, 0, 99))
    return c


def baue_truhe():
    """Truhen-Set: eisenbeschlagene Truhe, zwei Tonkruege, Reisigbesen."""
    c = Teil("Truhe_Set", BAENDER_INNEN, saat=53)
    S = "Innen"
    x0, x1, y0, y1 = -IX + 0.05, -IX + 0.6, -2.25, -1.3
    zb, zk = Z_BODEN + 0.04, Z_BODEN + 0.46
    c.kasten(S, (x0, y0, zb), (x1, y1, zk), "bohlen", ohne=("-z", "+z"), laengs="y")
    # gewoelbter Deckel aus drei Flaechen und zwei Stirnseiten
    xm = (x0 + x1) / 2
    profil = [(x0, zk), (x0 + 0.13, zk + 0.13), (x1 - 0.13, zk + 0.13), (x1, zk)]
    for (ax, az), (bx, bz) in zip(profil[:-1], profil[1:]):
        c.rechteck(S, (ax, y0, az), (0, 1, 0), (bx - ax, 0, bz - az), y1 - y0, math.hypot(bx - ax, bz - az), "bohlen", weg_von=(xm, 0, zb))
    v0, v1, m = BAENDER_INNEN["bohlen"]
    for y, aussen in ((y0, -1), (y1, 1)):
        c.vieleck(S, [(p[0], y, p[1]) for p in profil], [(0.1, v1 - 0.02), (0.13, v0 + 0.1), (0.2, v0 + 0.1), (0.23, v1 - 0.02)], weg_von=(xm, y - aussen, zk))
    # Eisenbaender (vorn und ueber den Deckel) und Schloss
    ue = feld("eisen")
    k0, k1, _ = BAENDER_INNEN["kleinteile"]
    for y in (y0 + 0.14, y1 - 0.14):
        for pts in ([(x1 + 0.006, y - 0.03, zb), (x1 + 0.006, y + 0.03, zb), (x1 + 0.006, y + 0.03, zk), (x1 + 0.006, y - 0.03, zk)],
                    [(x1 - 0.125, y - 0.03, zk + 0.136), (x1 - 0.125, y + 0.03, zk + 0.136), (x1 + 0.006, y + 0.03, zk + 0.004), (x1 + 0.006, y - 0.03, zk + 0.004)],
                    [(x0 + 0.13, y - 0.03, zk + 0.136), (x0 + 0.13, y + 0.03, zk + 0.136), (x1 - 0.13, y + 0.03, zk + 0.136), (x1 - 0.13, y - 0.03, zk + 0.136)]):
            c.vieleck(S, pts, [(ue[0], k1), (ue[1], k1), (ue[1], k0), (ue[0], k0)], weg_von=(xm, y, zb))
    ym = (y0 + y1) / 2
    c.vieleck(S, [(x1 + 0.008, ym - 0.05, zk - 0.13), (x1 + 0.008, ym + 0.05, zk - 0.13), (x1 + 0.008, ym + 0.05, zk + 0.0), (x1 + 0.008, ym - 0.05, zk + 0.0)], [(ue[0], k1), (ue[1], k1), (ue[1], k0), (ue[0], k0)], weg_von=(xm, ym, zb))
    # Tonkruege und Besen in der Ecke neben der Truhe
    c.dreh(S, (-IX + 0.3, -1.0, Z_BODEN), [(0.09, 0.0), (0.14, 0.06), (0.17, 0.17), (0.16, 0.28), (0.09, 0.39), (0.08, 0.44), (0.097, 0.48)], "kleinteile", seiten=9, deckel=False, u_bereich=feld("ton"))
    c.dreh(S, (-IX + 0.62, -1.12, Z_BODEN), [(0.07, 0.0), (0.105, 0.06), (0.115, 0.15), (0.07, 0.25), (0.062, 0.28), (0.074, 0.31)], "kleinteile", seiten=8, deckel=False, u_bereich=feld("ton"))
    fuss, kopf = Vector((-IX + 0.24, -0.62, Z_BODEN + 0.3)), Vector((-IX + 0.05, -0.6, Z_BODEN + 1.55))      # Besen an der linken Wand
    c.stab(S, fuss, kopf, 0.03, "balken")
    c.dreh(S, (-IX + 0.27, -0.62, Z_BODEN), [(0.13, 0.0), (0.09, 0.2), (0.03, 0.34)], "stroh", seiten=6, deckel=False, u_bereich=(0.0, 0.3))
    return c


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Besitz und Zustand aussen (Knotennamen wie bei den bisherigen Haeusern: Stadtansicht.Modelle.cs, Stadtansicht.Fahnen.cs)
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

SCHILD = [(-0.34, 0.42), (0.34, 0.42), (0.34, -0.02), (0.0, -0.44), (-0.34, -0.02)]   # Umriss wie bausteine.SCHILD


def prisma(teil, stoff, umriss, o, eu, ev, n, t0, t1, band, farbe_uv=(0.1, 0.2), **kw):
    """Platte mit dem Umriss `umriss` (in der Ebene o + x*eu + y*ev), von t0 bis t1 entlang n: Vorderseite und Rand."""
    o, eu, ev, n = Vector(o), Vector(eu), Vector(ev), Vector(n)
    v0, v1, m = teil.baender[band]
    P = lambda x, y, t: o + eu * x + ev * y + n * t
    mitte = P(0, 0, (t0 + t1) / 2)
    uv = lambda x, y: (farbe_uv[0] + x / m, v1 - (0.5 + y) * 0.5 * (v1 - v0))
    teil.vieleck(stoff, [P(x, y, t1) for x, y in umriss], [uv(x, y) for x, y in umriss], weg_von=P(0, 0, t0), **kw)
    for (ax, ay), (bx, by) in zip(umriss, umriss[1:] + umriss[:1]):
        teil.vieleck(stoff, [P(ax, ay, t0), P(bx, by, t0), P(bx, by, t1), P(ax, ay, t1)], [uv(ax, ay), uv(bx, by), uv(bx, by), uv(ax, ay)], weg_von=mitte, **kw)


def baue_wappen():
    """Wappenschild an der Gassenwand zwischen Fenster und Tuer: Rahmen aus Holz, Schild im Material „Hausfarbe“ (das Spiel
    faerbt es in der Farbe des Besitzers), Eisenhaken. Nur mit Besitzer sichtbar."""
    w = Teil("Wappen", BAENDER_AUSSEN, saat=61)
    g = 0.62
    o = (0.13, VORN, 1.52)
    eu, ev, n = (1, 0, 0), (0, 0, 1), (0, -1, 0)
    prisma(w, "Fachwerk", [(x * 1.16 * g, y * 1.12 * g) for x, y in SCHILD], o, eu, ev, n, 0.0, 0.045, "balken_c")
    prisma(w, "Hausfarbe", [(x * g, y * g) for x, y in SCHILD], o, eu, ev, n, 0.045, 0.075, "farbholz", farbe=(1.0, 1.0, 1.0, 1.0))   # weisse Eckenfarbe: Godot multipliziert sie auf die Hausfarbe
    w.kasten("Bruchstein", (0.13 - 0.02, VORN - 0.05, 1.52 + 0.47 * g), (0.13 + 0.02, VORN, 1.52 + 0.58 * g), "eisen", ohne=("+y",))
    return w


def baue_fahne():
    """Fahne des Besitzers auf dem First ueber dem Giebel zur Gasse: Mast „Holz“, Knauf „Eisen“, Tuch „Hausfarbe“ als flaches Prisma
    mit Schwalbenschwanz – genau der Aufbau, den Stadtansicht.Fahnen.cs erwartet (das Spiel ersetzt das Tuch durch ein wehendes)."""
    f = Teil("Fahne", BAENDER_AUSSEN, saat=67)
    x, y, z = 0.0, DACH_VORN + 0.55, Z_FIRST + 0.25
    hoehe = 2.3
    f.stab("Holz", (x, y, z), (x, y, z + hoehe), 0.08, "balken_a")
    f.kasten("Eisen", (x - 0.07, y - 0.07, z + hoehe - 0.02), (x + 0.07, y + 0.07, z + hoehe + 0.12), "eisen")
    tuch = [(0, 0), (1.25, 0.05), (0.98, 0.38), (1.25, 0.72), (0, 0.72)]
    o = Vector((x + 0.04, y, z + hoehe - 0.85))
    for t, sicht in ((-0.015, -1), (0.015, 1)):
        pts = [o + Vector((u, t, v)) for u, v in tuch]
        f.vieleck("Hausfarbe", pts, [(0.1, 0.8)] * 5, weg_von=o + Vector((0.6, -t, 0.36)))
    for (au, av), (bu, bv) in zip(tuch, tuch[1:] + tuch[:1]):
        f.vieleck("Hausfarbe", [o + Vector((au, -0.015, av)), o + Vector((bu, -0.015, bv)), o + Vector((bu, 0.015, bv)), o + Vector((au, 0.015, av))],
                  [(0.1, 0.8)] * 4, weg_von=o + Vector((0.6, 0, 0.36)))
    return f


def baue_leerstand():
    """Leerstand: Tuer und Fenster mit gekreuzten Brettern vernagelt (Knoten „Leerstand“, im Spiel nur bei leerem Haus sichtbar)."""
    l = Teil("Leerstand", BAENDER_AUSSEN, saat=71)
    for wand, (a0, a1), (z0, z1), art in FENSTER:
        (ox, oy), (ex, ey), _, n = WAENDE[wand]
        start = ox if ex else oy
        s0, s1 = a0 - start, a1 - start
        n3 = (n[0], n[1], 0)
        if art == "tuer":
            for z in (z0 + 0.55, z0 + 1.05, z0 + 1.5):
                l.brett("Fachwerk", punkt(wand, s0 - 0.16, z - 0.06), punkt(wand, s1 + 0.16, z + 0.07), 0.17, n3, "planken", tiefe=0.10)
        else:
            l.brett("Fachwerk", punkt(wand, s0 - 0.1, z0 + 0.05), punkt(wand, s1 + 0.1, z1 - 0.08), 0.15, n3, "planken", tiefe=0.11)
            l.brett("Fachwerk", punkt(wand, s0 - 0.1, z1 - 0.02), punkt(wand, s1 + 0.1, z0 + 0.12), 0.15, n3, "planken", tiefe=0.135)
    return l


def baue_geruest():
    """Baugeruest vor der Gassenseite (Knoten „Geruest“: Ausbau, Sanierung, nach einem Brand): Stangen, zwei Laufebenen, Leiter."""
    g = Teil("Geruest", BAENDER_AUSSEN, saat=73)
    yi, ya = OVORN - 0.22, OVORN - 1.05
    xs = (-OX - 0.1, 0.0, OX + 0.1)
    for x in xs:
        g.stab("Fachwerk", (x, yi, 0), (x, yi, 4.5), 0.09, "balken_a")
        g.stab("Fachwerk", (x, ya, 0), (x, ya, 5.3), 0.09, "balken_a")
    for z in (1.9, 3.9):
        for x in xs:
            g.stab("Fachwerk", (x, yi - 0.05, z - 0.07), (x, ya + 0.05, z - 0.07), 0.08, "balken_b")
        g.kasten("Fachwerk", (xs[0] - 0.15, ya + 0.08, z), (xs[-1] + 0.15, yi - 0.08, z + 0.05), "planken", laengs="x")
        g.stab("Fachwerk", (xs[0], ya, z + 0.95), (xs[-1], ya, z + 0.95), 0.06, "balken_a")
    g.stab("Fachwerk", (xs[0], ya, 0.1), (xs[1], ya, 1.85), 0.07, "balken_c")
    g.stab("Fachwerk", (xs[1], ya, 2.0), (xs[2], ya, 3.85), 0.07, "balken_c")
    fuss, kopf = Vector((1.75, ya - 0.55, 0)), Vector((1.75, ya + 0.02, 2.5))
    for dx in (-0.2, 0.2):
        g.stab("Fachwerk", fuss + Vector((dx, 0, 0)), kopf + Vector((dx, 0, 0)), 0.05, "balken_a")
    for i in range(7):
        p = fuss.lerp(kopf, (i + 0.7) / 7.6)
        g.stab("Fachwerk", p + Vector((-0.2, 0, 0)), p + Vector((0.2, 0, 0)), 0.04, "balken_b")
    return g


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Einrichtung des Wohnhauses, die man kauft oder hat (game/daten/wirtschaft.json, wohnen.einrichtung): je Stueck ein Knoten
#  „Einrichtung_<Kennung>“, im Spiel erst sichtbar, wenn das Haus das Stueck hat. Hier die drei Stuecke der Stufe 1.
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def baue_hausapotheke():
    """Hausapotheke (hat jedes Haus): Wandbord an der Gassenwand zwischen Fenster und Tuer mit Kraeuterschnaps, Salbentiegeln,
    Moerser und einem Buendel Heilkraut."""
    c = Teil("Einrichtung_Hausapotheke", BAENDER_INNEN, saat=81)
    S = "Innen"
    x0, x1, y = -0.22, 0.48, IV
    for z in (1.38, 1.72):
        c.kasten(S, (x0, y, z), (x1, y + 0.17, z + 0.03), "bohlen", ohne=("-y",), laengs="x")
    for x in (x0 + 0.03, x1 - 0.03):
        c.kasten(S, (x - 0.02, y, 1.20), (x + 0.02, y + 0.15, 1.92), "balken", ohne=("-y",), laengs="z")
    flasche = [(0.036, 0.0), (0.04, 0.09), (0.015, 0.14), (0.015, 0.185), (0.0, 0.19)]
    for i, (x, z, farbe, hoch) in enumerate(((x0 + 0.12, 1.41, "kraut", 1.0), (x0 + 0.26, 1.41, "ton", 0.8), (x0 + 0.54, 1.41, "kraut", 1.15), (x0 + 0.2, 1.75, "ton", 0.7), (x0 + 0.44, 1.75, "zwiebel_gelb", 0.9))):
        c.dreh(S, (x, y + 0.085, z), [(r, h * hoch) for r, h in flasche], "kleinteile", seiten=6, deckel=False, u_bereich=feld(farbe))
    c.dreh(S, (x0 + 0.39, y + 0.09, 1.41), [(0.03, 0.0), (0.055, 0.025), (0.06, 0.07), (0.05, 0.075)], "bruchstein", seiten=7, deckel=True, anteil=(0.2, 0.5), u_bereich=(0.1, 0.16))   # Moerser
    c.stab(S, (x0 + 0.39, y + 0.09, 1.46), (x0 + 0.43, y + 0.11, 1.56), 0.018, "balken")                                                                                       # Stoessel
    c.dreh(S, (x1 + 0.1, y + 0.07, 1.42), [(0.0, 0.0), (0.07, 0.06), (0.045, 0.24), (0.012, 0.34)], "kleinteile", seiten=6, deckel=False, u_bereich=feld("kraut"))               # Heilkraut am Nagel
    c.stab(S, (x1 + 0.1, y + 0.07, 1.76), (x1 + 0.1, y + 0.01, 1.84), 0.012, "stroh")
    return c


def baue_hauswaechter():
    """Hauswaechter (angestellt): sein Platz an der Gassenwand links vom Fenster – Hellebarde in der Ecke, Laterne am Wandhaken,
    Schemel mit Knueppel unter dem Fenster."""
    c = Teil("Einrichtung_Hauswaechter", BAENDER_INNEN, saat=83)
    S = "Innen"
    ue = feld("eisen")
    k0, k1, _ = BAENDER_INNEN["kleinteile"]
    fuss, kopf = Vector((-IX + 0.16, IV + 0.20, Z_BODEN + 0.46)), Vector((-IX + 0.05, IV + 0.05, Z_BODEN + 2.3))   # steht hinter der Truhe in der Ecke
    c.stab(S, fuss, kopf, 0.035, "balken")
    achse = (kopf - fuss).normalized()
    quer = Vector((1, 0, 0))
    blatt = kopf - achse * 0.38
    for pts in ([blatt, blatt + quer * 0.20 + achse * 0.06, blatt + quer * 0.22 + achse * 0.22, blatt + achse * 0.26],     # Beil
                [blatt + achse * 0.08, blatt - quer * 0.10 + achse * 0.12, blatt + achse * 0.17],                           # Haken
                [kopf - achse * 0.1, kopf + achse * 0.22, kopf - achse * 0.1 + quer * 0.035]):                              # Stossklinge
        for vorn in (1, -1):
            versetzt = [p + Vector((0, 0.004 * vorn, 0)) for p in pts]
            c.vieleck(S, versetzt, [(ue[0], k1), (ue[1], k1), (ue[1], k0), (ue[0], k0)][:len(pts)], zu=versetzt[0] + Vector((0, vorn, 0)))
    # Laterne am Wandhaken: Eisenrahmen, Hornscheiben mit Lichtschein
    lx, ly, lz = -1.66, IV + 0.13, 1.52
    c.stab(S, (lx, IV, lz + 0.42), (lx, ly, lz + 0.42), 0.02, "kleinteile", u_bereich=ue)
    c.stab(S, (lx, ly, lz + 0.42), (lx, ly, lz + 0.3), 0.012, "kleinteile", u_bereich=ue)
    c.kasten(S, (lx - 0.06, ly - 0.06, lz + 0.05), (lx + 0.06, ly + 0.06, lz + 0.24), "kleinteile", ohne=("-z", "+z"), laengs="z")
    ug = feld("glut")
    for fl in c.flaechen[-4:]:
        fl[1][:] = [(ug[0], k1), (ug[1], k1), (ug[1], k0), (ug[0], k0)]
    c.kasten(S, (lx - 0.075, ly - 0.075, lz), (lx + 0.075, ly + 0.075, lz + 0.05), "balken")
    c.dreh(S, (lx, ly, lz + 0.24), [(0.085, 0.0), (0.03, 0.06), (0.0, 0.07)], "kleinteile", seiten=4, deckel=False, u_bereich=ue, glatt=False)
    # Schemel und Knueppel unter dem Fenster
    sx, sy = -1.05, IV + 0.27
    c.dreh(S, (sx, sy, Z_BODEN + 0.40), [(0.17, 0.0), (0.17, 0.045)], "bohlen", seiten=8, deckel=True, u_bereich=(0.1, 0.2), anteil=(0.2, 0.7), glatt=False)
    c.dreh(S, (sx, sy, Z_BODEN + 0.40), [(0.17, 0.0), (0.0, 0.0)], "bohlen", seiten=8, deckel=False, u_bereich=(0.1, 0.2), anteil=(0.2, 0.7), glatt=False)
    for w in (30, 150, 270):
        c.stab(S, (sx + 0.17 * math.cos(math.radians(w)), sy + 0.17 * math.sin(math.radians(w)), Z_BODEN), (sx + 0.09 * math.cos(math.radians(w)), sy + 0.09 * math.sin(math.radians(w)), Z_BODEN + 0.40), 0.04, "balken")
    c.stab(S, (sx - 0.2, sy - 0.03, Z_BODEN + 0.475), (sx + 0.24, sy + 0.04, Z_BODEN + 0.48), 0.055, "balken", kappen=True)   # Knueppel quer auf dem Schemel
    return c


def baue_hauswappen():
    """Wappen ueber der Tuer (gekauft, „Ansehen“): geschnitzter Schild in der Hausfarbe innen ueber der Haustuer, mit Rahmen."""
    c = Teil("Einrichtung_Hauswappen", BAENDER_INNEN, saat=87)
    tuer = next(f for f in FENSTER if f[3] == "tuer")
    xm = (tuer[1][0] + tuer[1][1]) / 2
    g = 0.9
    o = (xm, JV, 3.25)
    eu, ev, n = (-1, 0, 0), (0, 0, 1), (0, 1, 0)
    prisma(c, "Innen", [(x * 1.2 * g, y * 1.14 * g) for x, y in SCHILD], o, eu, ev, n, 0.0, 0.05, "balken")
    prisma(c, "Hausfarbe", [(x * g, y * g) for x, y in SCHILD], o, eu, ev, n, 0.05, 0.085, "bohlen")
    # Zierleiste: zwei gekreuzte Staebe hinter dem Schild und ein Balken, auf dem er sitzt
    for sx in (-1, 1):
        c.stab("Innen", (xm + sx * 0.5, JV + 0.03, 2.82), (xm - sx * 0.5, JV + 0.03, 3.75), 0.035, "balken")
    c.kasten("Innen", (xm - 0.5, JV, 2.78), (xm + 0.5, JV + 0.1, 2.86), "balken", ohne=("-y",), laengs="x")
    return c



# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Blender: Materialien, UV2, Verdeckung backen, Export
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def stoff(name, blatt=None, farbe=(0.5, 0.5, 0.5), alpha=1.0, schere=False, rauheit=0.8):
    """Material fuer Kontrollbild und Export. Die Trim-Texturen haengen nur fuers Kontrollbild daran; die GLB bekommt keine Bilder
    (das Spiel setzt die Trim-Sheets beim Laden, Stadtansicht.Neubau.cs)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*farbe, 1.0)
    bsdf.inputs["Roughness"].default_value = rauheit
    bsdf.inputs["Alpha"].default_value = alpha
    if alpha < 1.0: m.surface_render_method = "BLENDED"
    if blatt:
        bild = bpy.data.images.load(os.path.join(ZIEL_TEXTUR, f"{blatt}_albedo.png"), check_existing=True)
        tex = m.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = bild; tex.name = "Trim"
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if schere: m.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    return m


def verschmelzen(obj):
    """Gleiche Ecken zusammenlegen (die Flaechen wurden einzeln gebaut): fuer zusammenhaengende UV2-Inseln."""
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bm.to_mesh(obj.data); bm.free()


def atlas_platz(gruppe, modell):
    """Platz eines Modells im AO-Atlas seiner Gruppe (ao_atlas.json): (Datei, Kante in Pixeln, Zellen je Seite, Spalte, Zeile).
    Alle Haeuser einer Gruppe teilen sich eine AO-Textur – so genuegt im Spiel ein Material je Gruppe (Style-Guide Abschnitt 4)."""
    import json
    daten = json.load(open(os.path.join(HIER, "ao_atlas.json"), encoding="utf-8"))[gruppe]
    nummer = daten["plaetze"][modell]
    n = daten["zellen"]
    return daten["datei"], daten["kante"], n, nummer % n, nummer // n


def uv2_und_ao(gruppen, gruppe, modell, mit_ao=True):
    """UV2 (Smart UV Project, Rand 0,02; alle Objekte des Modells in einer Zelle des Atlas) und die Verdeckung dorthin backen (Cycles).
    `gruppen`: Liste aus (Objekte, Verdecker) – jede wird fuer sich gebacken (das Geruest soll das Haus nicht verschatten)."""
    import numpy as np
    datei, kante, n, spalte, zeile = atlas_platz(gruppe, modell)
    zelle = kante // n
    objekte = [o for objs, _ in gruppen for o in objs]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objekte:
        o.hide_set(False); o.hide_viewport = False
        verschmelzen(o)
        uv2 = o.data.uv_layers.new(name="UV2")
        o.data.uv_layers.active = uv2
        o.select_set(True)
    bpy.context.view_layer.objects.active = objekte[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bild = None
    if mit_ao:
        bild = bpy.data.images.new("AO-Zelle", zelle, zelle, alpha=False)
        bild.colorspace_settings.name = "Non-Color"
        bild.generated_color = (1, 1, 1, 1)
        szene = bpy.context.scene
        szene.render.engine = "CYCLES"
        szene.cycles.device = "CPU"
        szene.cycles.samples = 96
        szene.render.bake.margin = 6
        szene.render.bake.use_clear = False
        if szene.world is None: szene.world = bpy.data.worlds.new("Welt")
        szene.world.light_settings.distance = 1.1          # nur Kontaktschatten (Dachueberstand, Balken, Moebel), kein dunkler Raum
        for objs, verdecker in gruppen:
            for o in bpy.data.objects:
                o.hide_render = o not in objs and o not in verdecker
            for o in objs:
                for m in o.data.materials:
                    knoten = m.node_tree.nodes
                    ziel = knoten.get("AO-Ziel") or knoten.new("ShaderNodeTexImage")
                    ziel.name = "AO-Ziel"; ziel.image = bild
                    knoten.active = ziel
            for o in objs:
                bpy.ops.object.select_all(action="DESELECT")
                o.select_set(True); bpy.context.view_layer.objects.active = o
                bpy.ops.object.bake(type="AO", uv_layer="UV2")
        # weich zeichnen: die Karte soll nur Kontaktschatten tragen
        feld = np.empty(zelle * zelle * 4, dtype=np.float32); bild.pixels.foreach_get(feld)
        grau = feld.reshape(zelle, zelle, 4)[..., 0]
        grau = np.clip((grau + np.roll(grau, 1, 0) + np.roll(grau, -1, 0) + np.roll(grau, 1, 1) + np.roll(grau, -1, 1)) / 5, 0, 1)
        # in den Atlas legen (vorhandene Zellen anderer Modelle bleiben)
        pfad = os.path.join(ZIEL_TEXTUR, datei)
        if os.path.exists(pfad):
            alt = bpy.data.images.load(pfad); alt.colorspace_settings.name = "Non-Color"
            ganz = np.empty(kante * kante * 4, dtype=np.float32); alt.pixels.foreach_get(ganz)
            ganz = ganz.reshape(kante, kante, 4).copy(); bpy.data.images.remove(alt)
        else:
            ganz = np.ones((kante, kante, 4), dtype=np.float32)
        ganz[zeile * zelle:(zeile + 1) * zelle, spalte * zelle:(spalte + 1) * zelle, :3] = grau[..., None]
        ganz[..., 3] = 1
        atlas = bpy.data.images.new("AO-Atlas", kante, kante, alpha=False)
        atlas.colorspace_settings.name = "Non-Color"
        atlas.pixels.foreach_set(ganz.ravel())
        atlas.filepath_raw = pfad; atlas.file_format = "PNG"; atlas.save()
        print("geschrieben", pfad, "Zelle", spalte, zeile)
    # UV2 in die Zelle des Atlas schieben (mit etwas Rand zur Nachbarzelle)
    rand = 3.0 / zelle
    for o in objekte:
        kanal = o.data.uv_layers["UV2"]
        for d in kanal.data:
            d.uv = ((spalte + rand + d.uv[0] * (1 - 2 * rand)) / n, (zeile + rand + d.uv[1] * (1 - 2 * rand)) / n)
        for m in o.data.materials:
            knoten = m.node_tree.nodes
            if knoten.get("AO-Ziel"): knoten.remove(knoten["AO-Ziel"])
            if knoten.get("Trim"): knoten.active = knoten["Trim"]
        o.data.uv_layers.active = o.data.uv_layers["UVMap"]
    for o in bpy.data.objects: o.hide_render = False


def anker(name, ort):
    o = bpy.data.objects.new(name, None)
    o.location = ort
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


def kontrollbild(datei, auge, ziel, brennweite=35, verbergen=()):
    szene = bpy.context.scene
    kam = bpy.data.objects.get("Kontrollkamera")
    if kam is None:
        kam = bpy.data.objects.new("Kontrollkamera", bpy.data.cameras.new("Kontrollkamera"))
        szene.collection.objects.link(kam)
        sonne = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN"))
        sonne.data.energy = 3.2; sonne.rotation_euler = (math.radians(52), 0, math.radians(-35))
        szene.collection.objects.link(sonne)
        szene.world.use_nodes = True
        szene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.62, 0.66, 0.72, 1)
        szene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    kam.location = auge
    kam.rotation_euler = (Vector(ziel) - Vector(auge)).to_track_quat("-Z", "Y").to_euler()
    kam.data.lens = brennweite
    szene.camera = kam
    szene.render.engine = "CYCLES"; szene.cycles.device = "CPU"; szene.cycles.samples = 24
    szene.render.resolution_x, szene.render.resolution_y = 1280, 960
    szene.render.filepath = datei
    szene.render.image_settings.file_format = "JPEG"
    for o in bpy.data.objects: o.hide_render = o.name in verbergen
    bpy.ops.render.render(write_still=True)
    for o in bpy.data.objects: o.hide_render = False


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")

    aussen_stoffe = {
        "Stroh": stoff("Stroh", "trim"), "Strohkante": stoff("Strohkante", "trim", schere=True), "Fachwerk": stoff("Fachwerk", "trim"),
        "Lehmputz": stoff("Lehmputz", "trim"), "Bruchstein": stoff("Bruchstein", "trim"),
        "Fenster": stoff("Fenster", farbe=(0.09, 0.11, 0.14), rauheit=0.2), "Bleinetz": stoff("Bleinetz", farbe=(0.1, 0.1, 0.11), alpha=0.5),
        "Hausfarbe": stoff("Hausfarbe", farbe=(0.55, 0.12, 0.10)), "Holz": stoff("Holz", farbe=(0.30, 0.22, 0.15)), "Eisen": stoff("Eisen", farbe=(0.12, 0.12, 0.13), rauheit=0.5),
    }
    innen_stoffe = {"Innen": stoff("Innen", "innen_trim"), "Innenglas": stoff("Innenglas", farbe=(0.55, 0.62, 0.6), alpha=0.16, rauheit=0.15),
                    "Hausfarbe": aussen_stoffe["Hausfarbe"]}
    huelle = [baue_haus(), baue_dach()]                                            # zaehlt zum Budget des Style-Guides
    scheiben = baue_scheiben()
    zusatz = [baue_wappen(), baue_leerstand(), baue_geruest()]                     # Besitz und Zustand, wie bei den bisherigen Haeusern
    fahne = baue_fahne()
    raum = [baue_raum(), baue_herd(), baue_vorrat(), baue_tisch(), baue_schlafplatz(), baue_truhe()]
    einrichtung = [baue_hausapotheke(), baue_hauswaechter(), baue_hauswappen()]
    glas_teil = baue_glas()
    zaehle = lambda teile: {t.name: t.dreiecke() for t in teile}
    print("DREIECKE aussen:", zaehle(huelle + [scheiben]), "Summe", sum(t.dreiecke() for t in huelle + [scheiben]))
    print("DREIECKE Zustand und Besitz:", zaehle(zusatz + [fahne]))
    print("DREIECKE innen:", zaehle(raum + [glas_teil]), "Summe", sum(t.dreiecke() for t in raum + [glas_teil]))
    print("DREIECKE Einrichtung:", zaehle(einrichtung))
    o_huelle = [t.objekt(aussen_stoffe) for t in huelle]
    o_zusatz = [t.objekt(aussen_stoffe) for t in zusatz]
    o_scheiben = scheiben.objekt(aussen_stoffe, mit_farbe=False)
    o_fahne = fahne.objekt(aussen_stoffe, mit_farbe=False)
    o_raum = [t.objekt(innen_stoffe, mit_farbe=False) for t in raum]
    o_einrichtung = [t.objekt(innen_stoffe, mit_farbe=False) for t in einrichtung]
    o_glas = glas_teil.objekt(innen_stoffe, mit_farbe=False)
    for o in o_zusatz[1:]:                          # Leerstand und Geruest: Zustandsknoten, im Spiel nach Bedarf sichtbar
        o["zustand"] = True; o["sichtbar"] = False
    aussen = o_huelle + [o_scheiben] + o_zusatz + [o_fahne]
    innen = o_raum + o_einrichtung + [o_glas]

    # Boden nur fuers Backen und fuers Kontrollbild
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    boden = bpy.context.active_object; boden.name = "Kontrollboden"
    boden.data.materials.append(stoff("Kontrollboden", farbe=(0.33, 0.3, 0.24)))

    mit_ao = "ohne-ao" not in opt
    uv2_und_ao([(o_huelle, [boden]), (o_zusatz, o_huelle + [boden])], "wohnhaus_mensch", NAME, mit_ao=mit_ao)
    uv2_und_ao([(o_raum, []), (o_einrichtung, o_raum)], "wohnhaus_mensch_innen", NAME, mit_ao=mit_ao)

    tuer = next(f for f in FENSTER if f[3] == "tuer")
    x0, x1, y0, y1 = KAMIN
    anker_aussen = [
        anker("Anker_Tuer", ((tuer[1][0] + tuer[1][1]) / 2, VORN - 0.2, Z_BODEN + 0.02)),               # auf der Stufe, Blick ins Haus (+Y)
        anker("Anker_Schornstein", ((x0 + x1) / 2, (y0 + y1) / 2, Z_FIRST - 0.35 + 0.2)),
    ]
    anker_innen = [
        anker("Blick_Auge", (0.6, IH - 0.3, Z_BODEN + 1.55)),                                           # steht hinten unter der Galerie ...
        anker("Blick_Ziel", (-0.3, IV, Z_BODEN + 1.55)),                                                # ... und schaut zur Gassenwand mit Tuer und Fenster
        anker("Licht_Herd", ((x0 + x1) / 2 - 0.25, (y0 + y1) / 2, 0.95)),
        anker("Licht_Raum", (-0.3, -1.2, 3.6)),
        anker("Feuer_Herd", ((x0 + x1) / 2 - 0.18, (y0 + y1) / 2, 0.53)),                               # Fuss der Flamme auf der Herdplatte
        # Plaetze der Familie (Stadtansicht.Neubau.cs): Oberhaupt und Kinder auf den Baenken, Ehepartner am Kessel, ein Kind spielt auf der Matte
        anker("Platz_Oberhaupt", (0.42, -1.5, Z_BODEN)), anker("Platz_Partner", (0.72, -1.4, Z_BODEN)),
        anker("Platz_Kind_1", (-0.82, -1.55, Z_BODEN)), anker("Platz_Kind_2", (-0.82, -0.85, Z_BODEN)),
        anker("Platz_Kind_3", (0.42, -0.75, Z_BODEN)), anker("Platz_Kind_4", (0.2, 1.15, Z_BODEN)),
    ]
    exportiere(aussen + anker_aussen, os.path.join(ZIEL_MODELL, f"{NAME}.glb"))
    exportiere(innen + anker_innen, os.path.join(ZIEL_MODELL, f"{NAME}_innen.glb"))

    if "bilder" in opt:
        o = opt["bilder"]; os.makedirs(o, exist_ok=True)
        namen_innen = [x.name for x in innen]
        namen_innen += ["Leerstand", "Geruest"]
        kontrollbild(os.path.join(o, "blender_aussen_vorn.jpg"), (-8.5, -12.5, 9.5), (0, 0, 3.6), verbergen=namen_innen)
        kontrollbild(os.path.join(o, "blender_aussen_hinten.jpg"), (9.5, 11.0, 8.0), (0, 0, 3.6), verbergen=namen_innen)
        lampe = bpy.data.objects.new("Kontrolllampe", bpy.data.lights.new("Kontrolllampe", "POINT"))
        lampe.data.energy = 220; lampe.location = (-0.3, -0.6, 2.3)
        bpy.context.scene.collection.objects.link(lampe)
        kontrollbild(os.path.join(o, "blender_innen_blick.jpg"), (0.55, IH - 0.32, Z_BODEN + 1.62), (-0.35, IV, Z_BODEN + 1.25), brennweite=15, verbergen=["Haus", "Dach", "Scheiben"])
        kontrollbild(os.path.join(o, "blender_innen_oben.jpg"), (-1.6, -2.3, 5.6), (0.6, 0.6, 1.2), brennweite=17, verbergen=["Haus", "Dach", "Scheiben"])


if __name__ == "__main__":
    main()
