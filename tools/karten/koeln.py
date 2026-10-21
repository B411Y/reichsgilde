#!/usr/bin/env python3
"""
Köln – erste Stadtkarte von Reichsgilde (Pilot, Bereich Leveldesign).

Aufruf (aus dem Projektordner):
    python3 tools/karten/koeln.py

Schreibt game/karten/koeln.json (Kartenformat 2) und docs/karten/koeln.svg, aber nur, wenn alle Kartenregeln
erfüllt sind (siehe karte.py, „Prüfungen“).

Entstanden aus der Planungsskizze (1000 × 720 Skizzenpunkte). Die Skizze wird hier in Meter umgerechnet:
Der Mauerhalbkreis hat in der Skizze 230 Punkte Radius, im Spiel 150 m (entschieden 16.10.2026).
Vorlage: das spätmittelalterliche Köln am linken Rheinufer, mit einem Hauch Fiktion (Liste „fiktion“).
"""
import math
import os
import sys

sys.dont_write_bytecode = True   # keinen __pycache__-Ordner im Repo anlegen
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from karte import Karte, dreh  # noqa: E402

# ── Umrechnung Skizze → Meter ────────────────────────────────────────────────
MARKT = (585, 360)                 # Heumarkt in der Skizze = Ursprung im Spiel
MASS = 150 / 230                   # Meter je Skizzenpunkt
def m(x, y):
    return ((x - MARKT[0]) * MASS, (y - MARKT[1]) * MASS)
def ml(punkte):
    return [m(x, y) for x, y in punkte]

# ── Stadtgrundriss (in Skizzenpunkten) ──────────────────────────────────────
MAUER_MITTE, MAUER_R = (610, 360), 230
RING_INNEN, RING_MITTE, WALLGASSE = 70, 130, 214   # Ringe um den Markt; Wallgasse um die Mauermitte
TORE = [("Eigelsteintor", 495, 161), ("Ehrentor", 394, 281), ("Hahnentor", 381, 380),
        ("Weyertor", 422, 492), ("Severinstor", 513, 568)]
UFER_NORD, UFER_SUED = (598, 138), (602, 585)      # Kunibertsturm und Bayenturm am Rhein

def winkel(x, y):
    """Winkel vom Markt aus: 0 = Norden, wächst nach Westen und weiter nach Süden."""
    a = math.atan2(-(x - MARKT[0]), -(y - MARKT[1]))
    return a + 2 * math.pi if a < -1 else a

def punkt(a, r):
    return (MARKT[0] - r * math.sin(a), MARKT[1] - r * math.cos(a))

def bis_wallgasse(a):
    """Abstand vom Markt bis zur Wallgasse (Kreis um die Mauermitte) in Richtung a."""
    dx, dz = -math.sin(a), -math.cos(a)
    ox, oz = MARKT[0] - MAUER_MITTE[0], MARKT[1] - MAUER_MITTE[1]
    b = ox * dx + oz * dz
    return -b + math.sqrt(b * b - (ox * ox + oz * oz - WALLGASSE ** 2))

RADIALEN = [UFER_NORD] + [(x, y) for _, x, y in TORE] + [UFER_SUED]
WINKEL = [winkel(x, y) for x, y in RADIALEN]

def bogen(r=None, wall=False, n=48):
    a1, a2 = WINKEL[0], WINKEL[-1]
    return [punkt(a1 + (a2 - a1) * i / n, bis_wallgasse(a1 + (a2 - a1) * i / n) if wall else r) for i in range(n + 1)]

RAND = 5   # Abstand Straßenmitte → Bauzone in Skizzenpunkten (5 × 0,65 = 3,3 m, Straßen gut 6,5 m breit; entschieden 16.10.2026)

def block(a1, a2, r1, r2=None, rand=RAND, n=10):
    """Bauzone zwischen zwei Radialen und zwei Ringen, mit Abstand `rand` zu den Straßen."""
    aussen, innen = [], []
    for i in range(n + 1):
        a = a1 + (a2 - a1) * i / n
        ro = (bis_wallgasse(a) if r2 is None else r2) - rand
        ri = r1 + rand
        aussen.append(punkt(min(max(a, a1 + rand / ro), a2 - rand / ro), ro))
        innen.append(punkt(min(max(a, a1 + rand / ri), a2 - rand / ri), ri))
    return aussen + innen[::-1]

def naechster(p, linie):
    """Der Punkt auf der Linie, der p am nächsten liegt (für Stichwege, die genau anschließen sollen)."""
    best = None
    for i in range(len(linie) - 1):
        a, b = linie[i], linie[i + 1]
        dx, dz = b[0] - a[0], b[1] - a[1]
        t = max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dz) / (dx * dx + dz * dz)))
        q = (a[0] + t * dx, a[1] + t * dz)
        if best is None or math.dist(p, q) < math.dist(p, best):
            best = q
    return best

def bezier(p0, p1, p2, p3, n=12):
    return [tuple((1 - t) ** 3 * p0[k] + 3 * (1 - t) ** 2 * t * p1[k] + 3 * (1 - t) * t * t * p2[k] + t ** 3 * p3[k]
                  for k in (0, 1)) for t in (i / n for i in range(n + 1))]


k = Karte("koeln", "Köln", "Spätmittelalterliches Köln am linken Rheinufer, Deutz gegenüber")

# ── Gelände ──────────────────────────────────────────────────────────────────
k.gelaende("feld", "Kölner Bucht", ml([(150, 230), (360, 190), (370, 500), (160, 500)]))
k.gelaende("feld", "Deutzer Feld", ml([(640, 430), (760, 440), (770, 620), (670, 620)]))
k.gelaende("huegel", "Bergische Höhen", ml([(800, 60), (1000, 40), (1000, 340), (830, 330)]))
k.gelaende("huegel", "Siebengebirge", ml([(760, 560), (1000, 520), (1000, 720), (740, 720)]))
k.gelaende("huegel", "Eifel", ml([(0, 640), (115, 648), (150, 720), (0, 720)]))
k.gelaende("wald", "Ville", ml([(20, 470), (270, 500), (300, 600), (240, 720), (0, 720), (0, 490)]))
k.gelaende("wald", "Königsforst", ml([(780, 360), (950, 350), (965, 505), (800, 525)]))
k.gelaende("wildnis", "Bergische Höhen (Ork-Rotten)", ml([(930, 40), (1000, 40), (1000, 420), (955, 400)]))
k.gelaende("wildnis", "Eifel (Oger)", ml([(0, 640), (60, 642), (70, 720), (0, 720)]))
k.gelaende("wildnis", "Siebengebirge (Oger)", ml([(960, 525), (1000, 520), (1000, 720), (965, 720)]))

# ── Wasser ───────────────────────────────────────────────────────────────────
rhein = bezier((600, 0), (640, 150), (590, 300), (620, 420)) + bezier((620, 420), (650, 540), (610, 620), (650, 720))[1:]
k.wasser("fluss", "Rhein", linie=ml(rhein), breite=round(46 * MASS))
k.wasser("bach", "Duffesbach", linie=ml([(384, 438), (450, 430), (520, 452), (560, 440), (600, 450)]), breite=1.5)
k.wasser("bach", "Strunde", linie=ml([(1000, 190), (900, 172), (780, 205), (700, 165), (628, 134)]), breite=1.5)
k.wasser("see", "Altrheinarm", flaeche=ml([(380 + 78 * math.cos(t / 12 * 2 * math.pi), 72 + 24 * math.sin(t / 12 * 2 * math.pi)) for t in range(12)]))

# ── Mauer und Tore ───────────────────────────────────────────────────────────
# Die Mauer endet an den beiden Ufertürmen (nicht in der Flussmitte), sonst kreuzt der Leinpfad sie ohne Tor. Sie ist offen
# (zum Rhein hin), die Ufertürme stehen als Tore an ihren Enden.
k.mauer("Stadtmauer", ml([UFER_NORD] + [(MAUER_MITTE[0] - MAUER_R * math.sin(a), MAUER_MITTE[1] - MAUER_R * math.cos(a))
                                        for a in (i / 32 * math.pi for i in range(1, 32))] + [UFER_SUED]))
for name, x, y in TORE:
    k.tor("Stadtmauer", name, m(x, y))
k.tor("Stadtmauer", "Kunibertsturm (Ufer)", m(*UFER_NORD)); k.tor("Stadtmauer", "Bayenturm (Ufer)", m(*UFER_SUED))

# Deutz: befestigter Brückenkopf am rechten Ufer, zum Rhein hin offen (dort liegt die Brücke).
k.mauer("Deutzer Mauer", ml([(652, 296), (795, 300), (798, 412), (652, 418)]))
k.tor("Deutzer Mauer", "Deutzer Nordtor", m(690, 298))     # Mülheimer Weg
k.tor("Deutzer Mauer", "Deutzer Osttor", m(796.5, 352))    # Bensberger Straße

# ── Wege in der Stadt ────────────────────────────────────────────────────────
for (name, x, y) in TORE:
    k.weg(f"{name}straße", "haupt", ml([(x, y), MARKT]))
k.weg("Rheinufer Nord", "haupt", ml([UFER_NORD, MARKT]))
k.weg("Rheinufer Süd", "haupt", ml([MARKT, UFER_SUED]))
k.weg("Innerer Ring", "ring", ml(bogen(RING_INNEN)))
k.weg("Mittlerer Ring", "ring", ml(bogen(RING_MITTE)))
k.weg("Wallgasse", "wall", ml(bogen(wall=True)))
for i in range(len(WINKEL) - 1):
    am = (WINKEL[i] + WINKEL[i + 1]) / 2
    k.weg(f"Gasse {i + 1}", "gasse", ml([punkt(am, RING_MITTE), punkt(am, bis_wallgasse(am))]))
k.weg("Brückenauffahrt", "haupt", ml([MARKT, (590, 371)]))

# ── Landstraßen (Gefahr > 1 = durch Wald oder nah an der Wildnis; Vorschlag, normiert der Kern) ──
aachener = [(381, 380), (190, 392), (0, 398)]
luxemburger_a, luxemburger_b = [(422, 492), (300, 600)], [(300, 600), (230, 720)]
bonner = [(513, 568), (548, 660), (562, 720)]
bensberg_a, bensberg_b = [(700, 372), (795, 352), (880, 333), (930, 322)], [(930, 322), (1000, 300)]
deutz_sued_a, deutz_sued_b = [(800, 351), (805, 416), (820, 560)], [(820, 560), (880, 640)]
k.weg("Neusser Straße", "land", ml([(495, 161), (500, 95), (520, 0)]))
k.weg("Aachener Straße", "land", ml(aachener))
k.weg("Venloer Straße", "land", ml([(394, 281), (180, 252), (0, 232)]))
k.weg("Luxemburger Straße", "land", ml(luxemburger_a))
k.weg("Luxemburger Straße (Ville)", "land", ml(luxemburger_b), gefahr=1.5)
k.weg("Bonner Straße", "land", ml(bonner))
k.weg("Deutzer Straße", "land", ml([(652, 371), (700, 372)]))
k.weg("Bensberger Straße", "land", ml(bensberg_a))
k.weg("Bensberger Straße (Höhen)", "land", ml(bensberg_b), gefahr=1.5)
k.weg("Siegburger Straße (Königsforst)", "land", ml(deutz_sued_a), gefahr=1.5)
k.weg("Siegburger Straße", "land", ml(deutz_sued_b))
k.weg("Mülheimer Weg", "land", ml([(650, 126), (680, 220), (690, 300), (700, 372)]))
k.weg("Leinpfad Nord", "leinpfad", ml([UFER_NORD, (600, 90), (601, 40), (604, 0)]))
k.weg("Leinpfad Süd", "leinpfad", ml([UFER_SUED, (612, 640), (615, 690), (622, 720)]))

# Stichwege zu den Rohstoffvorkommen (Karren müssen hin, deshalb Klasse „land“)
VORKOMMEN = [("Hz1", "Holzschlag", (130, 560), luxemburger_b), ("Hz2", "Holzschlag", (220, 660), luxemburger_b),
             ("Hz3", "Holzschlag", (830, 410), deutz_sued_a), ("Hz4", "Holzschlag", (920, 460), bensberg_a),
             ("M1", "Mine", (870, 200), bensberg_a), ("M2", "Mine", (950, 270), bensberg_b),
             ("M3", "Mine", (900, 110), bensberg_a), ("St1", "Steinbruch", (880, 640), deutz_sued_b),
             ("St2", "Steinbruch", (960, 690), deutz_sued_b), ("St3", "Steinbruch", (70, 662), luxemburger_b)]
for kennung, betrieb, p, an in VORKOMMEN:
    ziel = naechster(p, an)
    gefahr = 1.5 if an in (luxemburger_b, deutz_sued_a, bensberg_b) else 1.0
    k.weg(f"Weg zu {kennung}", "land", ml([p, ziel]), gefahr=gefahr)
    k.vorkommen(kennung, betrieb, m(*p))

k.uebergang("bruecke", "Rheinbrücke", m(590, 371), m(652, 371))
k.uebergang("faehre", "Fähre Mülheim", m(*naechster((599, 118), [UFER_NORD, (600, 90)])), m(650, 126))

# ── Bauzonen ─────────────────────────────────────────────────────────────────
for i in range(len(WINKEL) - 1):
    a1, a2 = WINKEL[i], WINKEL[i + 1]; am = (a1 + a2) / 2
    k.bauzone(f"K{i + 1}", "kern", ml(block(a1, a2, RING_INNEN, RING_MITTE)))
    k.bauzone(f"M{2 * i + 1}", "mauerring", ml(block(a1, am, RING_MITTE)))
    k.bauzone(f"M{2 * i + 2}", "mauerring", ml(block(am, a2, RING_MITTE)))
for kennung, pts in [("V1", [(662, 306), (684, 306), (684, 364), (662, 364)]),
                     ("V2", [(696, 306), (788, 309), (789, 350), (700, 354)]),
                     ("V3", [(662, 380), (696, 380), (696, 410), (662, 411)]),
                     ("V4", [(708, 378), (790, 362), (790, 404), (708, 408)])]:
    k.bauzone(kennung, "vorstadt", ml(pts))
for kennung, pts in [("U1", [(200, 262), (380, 288), (380, 372), (200, 382)]),
                     ("U2", [(200, 398), (378, 396), (380, 470), (300, 575), (205, 505)]),
                     ("U3", [(300, 130), (480, 118), (488, 150), (390, 268), (300, 240)]),
                     ("U4", [(664, 440), (795, 430), (808, 545), (700, 610), (660, 560)]),
                     ("U5", [(555, 610), (600, 605), (606, 700), (566, 700)]),
                     ("U6", [(692, 215), (800, 225), (790, 290), (698, 286)])]:
    k.bauzone(kennung, "umland", ml(pts))

# ── Gebäude ──────────────────────────────────────────────────────────────────
# Bausteine aus dem Gebäudekatalog (game/daten/katalog.json); `punkt` ist die Mitte der Grundfläche.
k.gebaeude("Markt", "markt", "Heumarkt", m(*MARKT))
k.gebaeude("Rathaus", "rathaus", "Rathaus", m(568, 318))
k.gebaeude("Hafen", "hafen", "Hafen mit Kran", m(597, 300))
# Kaimauer am linken Ufer zwischen Dom und Heumarkt: das Ufer des Rheins, 23 Skizzenpunkte westlich der Flussmitte.
kai = [(x - 23, y) for x, y in rhein if 235 <= y <= 345]
k.hafen("Kölner Hafen", ml(kai), [m(*naechster((597, y), kai)) for y in (252, 270, 288, 306, 324)],
        m(*naechster((597, 300), kai)))
k.gebaeude("Dom", "dom", "Dom (Baustelle mit Kran)", m(578, 190))   # 1,3 m nördlicher als in Format 1: sonst auf dem Startgrundstück 1
k.gebaeude("Gereon", "kirche", "St. Gereon", m(445, 215), konfession="katholisch")
k.gebaeude("Severin", "kirche", "St. Severin", m(575, 540), konfession="katholisch")
k.gebaeude("Antoniter", "kirche", "Antoniterkirche", m(437, 444), konfession="evangelisch")   # bis Format 1 (455|440) auf Startgrundstück 6
# L2 stand bis Format 1 bei (455|393) auf Startgrundstück 5
for kennung, x, y in [("L1", 490, 540), ("L2", 481, 391), ("L3", 770, 394), ("L4", 95, 610)]:
    k.gebaeude(kennung, "leerstand", "Ruine" if kennung == "L4" else "Leerstand", m(x, y))

# Startgrundstücke: je Haus ein Grundstück (24 × 14 m) mit Werkstattplatz (16 m, links) und Wohnhausplatz (8 × 10 m, rechts, mit der
# Vorderkante bündig), Eingang am Mittleren Ring. In den beiden breitesten Abschnitten liegen zwei, in den übrigen je eines – alle
# gleich weit vom Markt. Wohnhausplätze stehen rechtwinklig: Das Grundstück dreht sich in die Himmelsrichtung, die dem Ring am nächsten
# kommt, und beide Plätze bekommen diese Drehung.
spannen = [(WINKEL[i], WINKEL[i + 1]) for i in range(len(WINKEL) - 1)]
breiteste = set(sorted(range(len(spannen)), key=lambda i: spannen[i][0] - spannen[i][1])[:2])
start_winkel = []
for i, (a1, a2) in enumerate(spannen):
    am = (a1 + a2) / 2
    start_winkel += [(a1 + am) / 2, (am + a2) / 2] if i in breiteste else [am]
for n, a in enumerate(sorted(start_winkel), start=1):
    mitte, eingang = m(*punkt(a, RING_MITTE + 14)), m(*punkt(a, RING_MITTE))
    dx, dz = eingang[0] - mitte[0], eingang[1] - mitte[1]
    grad = ((1 if dx > 0 else 3) if abs(dx) > abs(dz) else (0 if dz >= 0 else 2)) * 90
    for teil, katalog, lx, lz in (("Werkstatt", "werkstattplatz", -4, 0), ("Wohnhaus", "wohnhausplatz", 8, 2)):
        wx, wz = dreh(lx, lz, grad)
        k.gebaeude(f"S{n}_{teil}", katalog, f"Startgrundstück {n}, {teil}", (round(mitte[0] + wx, 1), round(mitte[1] + wz, 1)), grad)

# ── Was von der Vorlage abweicht ─────────────────────────────────────────────
k.fiktion("Rheinbrücke Heumarkt–Deutz: im Mittelalter gab es keine feste Brücke, nur Fähren.")
k.fiktion("Fünf Tore statt zwölf Torburgen, damit die Stadt lesbar bleibt.")
k.fiktion("Straßennetz als Radialen und Ringe vereinfacht; die echten Gassen waren unregelmäßiger.")
k.fiktion("Wildnis mit Ork-Rotten (Bergische Höhen) und Ogern (Eifel, Siebengebirge).")
k.fiktion("Antoniterkirche als evangelische Kirche: im Spiel gibt es beide Konfessionen von Anfang an.")

if __name__ == "__main__":
    print("Köln:")
    k.schreiben(mauer_geschlossen=False)
