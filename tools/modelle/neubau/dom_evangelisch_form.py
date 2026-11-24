"""Neubau Dom evangelisch, Form der Huelle nach dem Artwork des Nutzers (24.11.2026) - reines Python, alle Masse an einer Stelle.

Verschachtelte Backsteinkirche: Langhaus mit Sandsteingiebel und Vorhalle vorn (-Y), dahinter ein Achteck mit Zeltdach und Gauben,
dahinter der Chor mit 5/8-Schluss, zwei Treppentuermchen mit Kegeldach, ein grosser runder Turm links vor der Westwand.
Die Module des Bausatzes (Dom_Module.blend) geben die Grossenordnungen vor (Achteck 11,5 m, Turm 8 m, Treppentuermchen 4 m,
Giebel 8 m); die Huelle wird aus denselben Bausteinen wie der Dom katholisch gebaut (dom_bib.py), nicht aus den Modulen.

Koordinaten wie bei allen Bauskripten: X rechts, Y nach hinten (Portal bei -Y), Z oben. Platz hoechstens 20 x 38 m.
"""
import math

FLAECHE = (20.0, 38.0)
BUDGET = (8000, 15000)
XA = 1.2                   # Achse von Langhaus, Achteck und Chor (der Turm links ragt weiter hinaus als die Tuermchen rechts: Huelle mittig im Platz)

Z_STEIN = 2.4              # Oberkante des Sandsteinsockels (zwei Lagen Backstein = 2,40 m, die Lagen der Mauer sind 1,20 m)
# ── Langhaus ──────────────────────────────────────────────────────────────
LANG_X = 5.5               # halbe Breite (aussen)
LANG_Y = (-15.0, 0.4)      # Giebelwand vorn bis zum Achteck
Z_TRAUFE_L = 8.4
NEIGUNG_L = math.tan(math.radians(42))
Z_FIRST_L = Z_TRAUFE_L + (LANG_X + 0.30) * NEIGUNG_L
# ── Vorhalle ──────────────────────────────────────────────────────────────
VH_X = 0.75                # Mitte der Vorhalle (Achse + VH_X)
VH_HALB = 2.75
VH_TIEFE = 3.3
VH_Z = (4.8, 4.8 + VH_HALB + 0.25)       # Traufe, First (45 Grad)
PORTAL = (2.4, 0.30, 3.0)  # Breite, Schwelle, Sturz
# ── Achteck ───────────────────────────────────────────────────────────────
OKT_M = (0.0, 3.5)         # Mitte relativ zur Achse
OKT_A = 6.2                # Apothem (Mitte bis Wandmitte)
Z_TRAUFE_O = 12.0
Z_SPITZE_O = 21.4
# ── Chor ──────────────────────────────────────────────────────────────────
CHOR_M = (0.0, 14.2)       # Mitte des Schlusses
CHOR_A = 4.5
Z_TRAUFE_C = 7.2
NEIGUNG_C = math.tan(math.radians(50))
Z_FIRST_C = Z_TRAUFE_C + (CHOR_A + 0.30) * NEIGUNG_C
CHOR_Y0 = 7.0              # die Seitenwaende des Chors beginnen im Achteck
# ── Treppentuermchen ──────────────────────────────────────────────────────
TUERMCHEN = [(6.3, 0.6), (5.4, 8.2)]     # Mitte relativ zur Achse (am Langhaus/Achteck, am Achteck/Chor)
TM_R = 1.30
Z_TM_WAND = 12.0
Z_TM_SPITZE = 17.0
# ── Turm ──────────────────────────────────────────────────────────────────
TURM_M = (-7.2, -15.5)     # relativ zur Achse: vor der Westwand des Langhauses
TURM_SEITEN = 16
TURM_R = 3.30              # Eckenradius des Schafts
Z_TURMSOCKEL = 3.00
Z_GLOCKEN = (18.40, 22.00)
Z_HAUBE = 22.00
Z_SPITZE = 29.80
Z_KNAUF = 31.30


def welt(x, y):
    """Punkt relativ zur Achse -> Weltkoordinate."""
    return (XA + x, y)


def vieleck(mx, my, apothem, n=8, phase=0.0):
    """Regelmaessiges n-Eck, Flaechen k mit Normale phase + k * 360/n Grad: Liste (Ecke a, Ecke b, Normale), gegen den Uhrzeigersinn."""
    r = apothem / math.cos(math.pi / n)
    aus = []
    for k in range(n):
        w = phase + 2 * math.pi * k / n
        a = (mx + r * math.cos(w - math.pi / n), my + r * math.sin(w - math.pi / n))
        b = (mx + r * math.cos(w + math.pi / n), my + r * math.sin(w + math.pi / n))
        aus.append((a, b, (math.cos(w), math.sin(w))))
    return aus
