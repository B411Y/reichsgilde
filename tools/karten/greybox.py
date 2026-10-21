#!/usr/bin/env python3
"""
Greybox-Stadt – die heutige, fest im Code gezeichnete Stadt des Spiels als Kartendatei mit Bauzonen
(Bereich Leveldesign).

Aufruf (aus dem Projektordner):
    python3 tools/karten/greybox.py

Schreibt game/data/karten/greybox.json und docs/karten/greybox.svg (dazu greybox-belegung.svg), aber nur, wenn alle
Regeln erfüllt sind (siehe karte.py und „Prüfungen der Greybox“ unten).

Die Karte beschreibt die Stadt so, wie `game/scripts/Stadtansicht*.cs` sie zeichnet. Der Spielcode wird hier nur
nachgebildet, nie geändert. Die Koordinaten sind die **Weltkoordinaten der Stadtansicht in Metern** (Godot x/z), damit Kern
und Darstellung ohne Umrechnung arbeiten. Quellen im Spielcode:
  • Stadtplan, Gassen, Abzweige:        Stadtansicht.cs (Stadtplan, Gassen, Abzweige, Wegkreuz, Osttor, Westtor)
  • Mauer, Tore, Türme, Marktpflaster:  Stadtansicht.cs (Stadtmauer, _Ready)
  • Ringgasse, Ringplätze, Vorstadt:    Stadtansicht.Wohnviertel.cs
  • Doppelplätze der Spielhäuser:       Stadtansicht.Spielhaeuser.cs (SpielhausPlaetze)
Ändert sich dort etwas, muss dieses Skript nachziehen (die Zahlen unten tragen den Stand vom 21.10.2026).

Werkstätten in der Stadt gibt es seit 20.10.2026 nur noch vier (Startwerkstatt am Weinmarkt und die drei Rivalen), jede auf
16 × 14 m wie ihr Modell; die Werkstätten zum Kauf sind gestrichen. Wo sie im Süden der Stadt standen, liegen seit 21.10.2026 die
Kernzonen K1 bis K3, die übrigen Bauzonen vor dem Westtor und an der Straße zum Steinbruch am Felsenhang (siehe „Bauzonen“).
"""
import math
import os
import struct
import sys

sys.dont_write_bytecode = True   # keinen __pycache__-Ordner im Repo anlegen
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from karte import Karte, RASTER, KARRENSTRASSEN, abstand, abstand_linie, abstand_strecke, schnitt  # noqa: E402
from kapazitaet import FARBE, _drin  # noqa: E402

# Soll der Stadthof getestet werden können, obwohl innerhalb der Mauer kein Platz für 16 × 14 m frei ist? Dann zählt die
# Zone „V4“ (12–28 m vor dem Westtor, an der Westmauer entlang) als Mauerring. Auf False: ehrlich „Vorstadt“, dann gibt
# es in dieser Karte keinen Stadthof-Platz. Die Abweichung steht in `fiktion`.
HOF_ZONE_ALS_MAUERRING = True


# ── Vorgaben aus dem Spielcode ───────────────────────────────────────────────
MAUERRAND = 50.0                       # Stadtansicht.Mauerrand: Mauer bei x, z = ±50 (2 m dick)
RINGGASSE = 38.0                       # Stadtansicht.Ringgasse: Ringgasse bei x, z = ±38
OSTTOR, WESTTOR = (50.0, 0.0), (-50.0, 6.0)
LANDSTRASSE = (90.0, -2.0)             # Ende der Landstraße vor dem Osttor
WEGKREUZ = (76.0, -1.3)                # hier zweigen die Wege zu den Gruben, Höfen und Gärten im Osten ab
WESTGASSE_KNICK, WESTSTRASSE = (-12.0, 6.0), (-118.0, 6.0)
PLATZ_BREITE, PLATZ_TIEFE = 8.5, 8.0   # Ringplatz (Wohnviertel.cs)
MARKTPFLASTER = 24.0

# Stadtansicht.Stadtplan: Mitte des Grundstücks je Liegenschaft (Name wie in wirtschaft.json)
STADTPLAN = {
    "Gießerei am Weinmarkt": (-23, 17), "Tischlerei am Weinmarkt": (-23, 17), "Gießerei Eisenbart": (22, 14),
    "Tischlerei Eichwald": (22, -12), "Maurerei am Weinmarkt": (-23, 17), "Maurerei Kieselbach": (25, -28),
    "Steinbruch Kieselbach": (74, 66), "Steinbruch am Felsenhang": (-6, -80), "Tinkturei am Weinmarkt": (-23, 17),
    "Kräutergarten am Bach": (106, 70), "Schänke am Weinmarkt": (-23, 17),
    "Bauernhof an der Mühle": (124, -62), "Kirche am Weinmarkt": (-23, 17),
    "Schmugglerloch am Weinmarkt": (-23, 17), "Wachhaus am Weinmarkt": (-23, 17),
    "Wechselstube am Weinmarkt": (-23, 17), "Siederei am Weinmarkt": (-23, 17),
    "Duftgarten am Lavendelhang": (124, 8), "Wachturm an der Landstraße": (80, -10),
    "Klosterhof am Osthang": (134, 80), "Grube Eisenbart": (102, -12), "Grube am Silberberg": (100, 32),
    "Grube im Tannenwald": (82, -66), "Holzschlag Eichwald": (-92, -30), "Holzschlag im Eichenforst": (-94, 46),
}

# Stadtansicht.Gassen: Knicke der Gasse vom Hof eines Werkstattplatzes zum Markt, je Platz (Weinmarkt: ein Platz für zehn
# Berufe). Die Gasse läuft vom Hof (5,5 m vor dem Stadtplan-Punkt) an der Vorderseite entlang und dann zum Markt, nie durch ein Haus.
GASSEN_KNICKE = {
    "Weinmarkt": [(-34, 22.5), (-34, 6), (-12, 6)],      # nach Westen, an der Ringgasse entlang nach Norden, dann die Westgasse
    "Gießerei Eisenbart": [(12, 19.5), (12, 12)],        # nach Westen am Vorplatz entlang, am Markt entlang nach Norden
    "Tischlerei Eichwald": [(12.5, -6.5)],               # nach Westen bis an den Markt
    "Maurerei Kieselbach": [(11, -22.5), (11, -13)],     # zwischen Rathaus und Eichwald nach Süden
}

# Stadtansicht.Abzweige: wo der Weg zu einem Platz vor der Stadt von der Straße abgeht
ABZWEIGE = {
    "Grube Eisenbart": (90, -2), "Grube am Silberberg": (86, 18), "Grube im Tannenwald": (80, -26),
    "Holzschlag Eichwald": (-84, 6), "Holzschlag im Eichenforst": (-102, 6), "Steinbruch Kieselbach": (80, 26),
    "Steinbruch am Felsenhang": (-62, -58), "Kräutergarten am Bach": (86, 18), "Bauernhof an der Mühle": (80, -26),
    "Klosterhof am Osthang": (98, 57), "Wachturm an der Landstraße": (80, -1), "Duftgarten am Lavendelhang": LANDSTRASSE,
}
IM_WESTEN = {"Holzschlag Eichwald", "Holzschlag im Eichenforst", "Steinbruch am Felsenhang"}

# Wohnviertel.cs: die Arbeitsstellen der Einwohner im Umland (Mitte, Größe) und ihre Fußwege von der Weststraße
# (Größe einschließlich der Zaunpfosten, Furchen und der Tannen, die bis 9 m neben der Mitte stehen)
UMLANDSTELLEN = {"Weide": ((-70, 26), (14, 10), (-70, 6)), "Feld": ((-60, 62), (14, 9), (-58, 6)),
                 "Wald": ((-68, -14), (18, 16), (-66, 6))}

# Größen der Umlandbetriebe nach LEVELDESIGN, „Gebäudegrößen“ (Wachturm: sein Fundament)
def umlandgroesse(name):
    if "Bauernhof" in name: return [20, 16]
    if "Klosterhof" in name: return [18, 16]
    if "Wachturm" in name: return [4, 4]
    return [16, 14]

VORKOMMEN = {"Grube": "Mine", "Steinbruch": "Steinbruch", "Holzschlag": "Holzschlag"}   # Namensanfang → Betriebsart
WERKSTATT = (16, 14)                   # jede Werkstatt der Greybox: 16 m breit (x) und 14 m tief (z) mit 2 m Vorplatz, Tür nach Süden (+z)
WERKSTATT_VORN = 1                     # der Stadtplan-Punkt liegt 1 m vor der Mitte der Fläche (Mitte des alten 10 × 8-m-Hauses, wie bei Neubauten)
HOF_VORN = 5.5                         # der Hof (Gassenanfang, Karren, Gäste) liegt 5,5 m vor dem Stadtplan-Punkt, im Vorplatz


def im_stadtgebiet(p):
    return abs(p[0]) < MAUERRAND and abs(p[1]) < MAUERRAND


def kennung_von(name):
    """Kennung aus einem Namen: nur Buchstaben und Ziffern, Umlaute umschrieben."""
    t = name
    for a, b in (("ä", "ae"), ("ö", "oe"), ("ü", "ue"), ("ß", "ss"), ("Ä", "Ae"), ("Ö", "Oe"), ("Ü", "Ue")):
        t = t.replace(a, b)
    return "".join(c if c.isalnum() else "_" for c in t).strip("_")


def drehung_von(vorne):
    """Drehung in Grad (Godot Rotation.Y) für eine Blickrichtung (x, z): 0 = +z, 90 = +x, 180 = −z, 270 = −x."""
    return round(math.degrees(math.atan2(vorne[0], vorne[1]))) % 360


def f32(x):
    """Wie `float` in C#: Die Reihenfolge der Doppelplätze hängt von Gleichständen in der Sortierung ab."""
    return struct.unpack("f", struct.pack("f", x))[0]


# ── Ringplätze und Doppelplätze der Spielhäuser (Wohnviertel.cs und Spielhaeuser.cs, Zeile für Zeile nachgebildet) ──
def ring_plaetze():
    """Die Einzelplätze am Ring (Mitte, Blickrichtung), in der Reihenfolge von Stadtansicht.RingPlaetze()."""
    liste = []
    abstand_ = f32(f32(f32(RINGGASSE + 1.5) + 0.8) + f32(PLATZ_TIEFE / 2))   # Gassenrand + Vorplatz + halbe Haustiefe

    def reihe(von, bis, mitte, vorne):
        anzahl = int(f32(f32(bis - von) / PLATZ_BREITE))
        rand = f32(f32(bis - von - f32(anzahl * PLATZ_BREITE)) / 2)
        for i in range(anzahl):
            liste.append((mitte(f32(von + rand + f32(PLATZ_BREITE * (i + 0.5)))), vorne))

    ecke = MAUERRAND - 4.5                # vor dem Eckturm
    reihe(-ecke, ecke, lambda x: (x, -abstand_), (0, 1))     # Norden, Front nach Süden (+z)
    reihe(-ecke, ecke, lambda x: (x, abstand_), (0, -1))     # Süden, Front nach Norden (−z)
    innen = RINGGASSE - 2
    reihe(-innen, OSTTOR[1] - 5, lambda z: (abstand_, z), (-1, 0))
    reihe(OSTTOR[1] + 5, innen, lambda z: (abstand_, z), (-1, 0))
    reihe(-innen, WESTTOR[1] - 5, lambda z: (-abstand_, z), (1, 0))
    reihe(WESTTOR[1] + 5, innen, lambda z: (-abstand_, z), (1, 0))
    return liste


def doppelplaetze(anzahl):
    """Die Doppelplätze der Spielhäuser in der Reihenfolge, in der die Darstellung sie den Konten zuweist
    (Stadtansicht.SpielhausPlaetze): je zwei benachbarte Ringplätze derselben Seite, die nächsten am Markt zuerst.
    Gibt (Mitte, Blickrichtung, Nummern der beiden Ringplätze) zurück."""
    ring = ring_plaetze()
    paare = []
    for i in range(len(ring) - 1):
        (am, av), (bm, bv) = ring[i], ring[i + 1]
        if av != bv or math.dist(am, bm) > f32(PLATZ_BREITE + 0.1):
            continue
        paare.append(((f32((am[0] + bm[0]) / 2), f32((am[1] + bm[1]) / 2)), av, (i, i + 1)))
    paare.sort(key=lambda p: (f32(math.sqrt(f32(f32(p[0][0] * p[0][0]) + f32(p[0][1] * p[0][1])))), p[0][0], p[0][1]))
    gewaehlt = []
    for p in paare:
        if len(gewaehlt) >= anzahl:
            break
        if any(set(g[2]) & set(p[2]) for g in gewaehlt):
            continue
        gewaehlt.append(p)
    return gewaehlt, ring


# ── Die Karte ────────────────────────────────────────────────────────────────
k = Karte("greybox", "Greybox-Stadt",
          "Die heutige Platzhalter-Stadt des Spiels (fest in game/scripts/Stadtansicht*.cs gezeichnet), Mauer bei ±50 m",
          schritt=0.05)
k.d["achsen"] = ("x = Osten, z = Süden (−z = Norden), Ursprung am Marktplatz – Weltkoordinaten der Stadtansicht in Metern "
                 "(Godot x/z, y = oben)")
k.d["gebaeude"] = ("feste[]: `punkt` = Mitte des Gebäudes, `groesse` = [Breite, Tiefe] in Metern, Breite entlang der Vorderseite. "
                   "`drehung` = Grad um die Hochachse wie Godot Rotation.Y; die Vorderseite zeigt für 0 nach +z (Süden), 90 nach +x (Osten), "
                   "180 nach −z (Norden), 270 nach −x (Westen). `eingang` = Mitte der Vorderseite. Bei 90 und 270 vertauschen sich "
                   "Breite und Tiefe auf den Achsen: x-Ausdehnung = Tiefe, z-Ausdehnung = Breite.")

# ── Mauer, Tore, Türme ───────────────────────────────────────────────────────
# Die Mauer ist ein Quadrat bei ±50 m. Im Osten (z = 0) und Westen (z = 6) klafft je eine 8 m breite Lücke für das Tor
# (Stadtansicht.Stadtmauer: MauerMitTor). Weil `mauer.linie` ein einziger Linienzug ist, steht die Südhälfte unter `nebenmauern`.
R = MAUERRAND
k.mauer([(R, OSTTOR[1] - 4), (R, -R), (-R, -R), (-R, WESTTOR[1] - 4)])
k.nebenmauer("Stadtmauer Süd", [(-R, WESTTOR[1] + 4), (-R, R), (R, R), (R, OSTTOR[1] + 4)])
k.tor("Osttor", OSTTOR)
k.tor("Westtor", WESTTOR)

# ── Wege ─────────────────────────────────────────────────────────────────────
# Klassen wie im Auftrag: Haupt-, Ring- und Land-/Weststraßen sind Karrenstraßen, Gassen und Fußwege nicht.
k.weg("Ostgasse", "haupt", [(0, 0), OSTTOR], breite=5)
k.weg("Landstraße", "land", [OSTTOR, LANDSTRASSE], breite=4)
k.weg("Westgasse", "haupt", [(0, 0), WESTGASSE_KNICK, (-38, 6), WESTTOR], breite=5)
k.weg("Weststraße", "land", [WESTTOR, WESTSTRASSE], breite=4)
r = RINGGASSE
k.weg("Ringgasse", "ring", [(-r, -r), (r, -r), (r, r), (-r, r), (-r, -r)], breite=3)
k.weg("Domgasse", "gasse", [(-20, 4.2), (-20, WESTGASSE_KNICK[1])], breite=2.5)   # von den Stufen vor dem Domportal zur Westgasse
k.weg("Vorstadtgasse", "gasse", [(64, -42.5), (64, 42.5)], breite=3)     # kreuzt die Landstraße, gezeichnet so weit Häuser an ihr stehen

# Gassen vom Hof jeder Werkstatt zum Markt (Gassennetz: Hof 5,5 m vor dem Stadtplan-Punkt, dann Knicke, dann Marktplatz)
plaetze_stadt = {}                           # Mitte → alle Liegenschaften auf diesem Platz
for name, p in STADTPLAN.items():
    if im_stadtgebiet(p):
        plaetze_stadt.setdefault(p, []).append(name)
def platzname(namen):
    """Name eines Werkstattplatzes: der Name der Liegenschaft, auf dem Weinmarkt (zehn Berufe, ein Platz) „Weinmarkt“."""
    return namen[0] if len(namen) == 1 else "Weinmarkt"

for p, namen in plaetze_stadt.items():
    knicke = GASSEN_KNICKE.get(platzname(namen)) or [(p[0] * 0.45, p[1] * 0.45)]
    k.weg(f"Gasse {platzname(namen)}", "gasse", [(p[0], p[1] + HOF_VORN)] + knicke + [(0, 0)], breite=3.5)

# Wege zu den Plätzen vor der Stadt (Karren fahren sie, deshalb Klasse „land“)
for name, p in STADTPLAN.items():
    if im_stadtgebiet(p):
        continue
    abzweig = ABZWEIGE[name]
    kreuz = WESTTOR if name in IM_WESTEN else WEGKREUZ
    strasse = (WESTTOR, WESTSTRASSE) if name in IM_WESTEN else (OSTTOR, LANDSTRASSE)
    if abstand_strecke(abzweig, *strasse) > 1 and not any(w["linie"] == [list(kreuz), list(abzweig)] for w in k.d["wege"]):
        k.weg(f"Weg zum Abzweig {name}", "land", [kreuz, abzweig], breite=4)
    k.weg(f"Weg zu {name}", "land", [abzweig, (p[0] - 2, p[1])], breite=4)
for name, (mitte, groesse, zugang) in UMLANDSTELLEN.items():
    k.weg(f"Fußweg {name}", "gasse", [zugang, mitte], breite=2)

# ── Feste Gebäude ────────────────────────────────────────────────────────────
def gebaeude(kennung, art, name, mitte, groesse, drehung=0, eingang=True, **mehr):
    """Ein festes Gebäude: Mitte, [Breite, Tiefe], Drehung; der Eingang liegt in der Mitte der Vorderseite."""
    vx, vz = math.sin(math.radians(drehung)), math.cos(math.radians(drehung))
    punkte = {}
    if eingang:
        punkte["eingang"] = (mitte[0] + vx * groesse[1] / 2, mitte[1] + vz * groesse[1] / 2)
    k.fest(kennung, art, name, mitte, groesse=groesse, drehung=drehung, **punkte, **mehr)

# Sonderbauten (RathausBauen, DomBauen, Marktpflaster, Torhaus): Das Torhaus ist 5 × 9 m mit zwei Türmen davor und dahinter
gebaeude("Markt", "sonderbau", "Marktplatz mit Ständen und Brunnen", (0, 0), [MARKTPFLASTER, MARKTPFLASTER], eingang=False)
gebaeude("Rathaus", "sonderbau", "Rathaus", (0, -20), [16, 10])
gebaeude("Dom", "sonderbau", "Dom", (-20, -11.15), [12.5, 30.2])   # Langhaus 10 × 22 m um (−20|−14) mit Strebewerk und Westturm bis z = 3,9 (dom.glb)
gebaeude("TorhausOst", "sonderbau", "Torhaus Osttor", OSTTOR, [16, 6], drehung=90, eingang=False)
gebaeude("TorhausWest", "sonderbau", "Torhaus Westtor", WESTTOR, [16, 6], drehung=90, eingang=False)
for n, (ex, ez) in enumerate([(-R, -R), (R, -R), (-R, R), (R, R)], start=1):
    gebaeude(f"Eckturm{n}", "turm", f"Eckturm {n}", (ex, ez), [7, 7], eingang=False)

# Werkstätten in der Stadt: eine je Platz; mehrere Namen auf einem Platz = je nach Startberuf (Weinmarkt). Die Fläche (16 × 14 m mit
# Vorplatz) liegt mit ihrer Mitte 1 m hinter dem Stadtplan-Punkt, ihre Vorderkante 6 m vor ihm.
def werkstatt_mitte(p):
    return (p[0], p[1] - WERKSTATT_VORN)

for p, namen in plaetze_stadt.items():
    for name in namen:                       # ein Eintrag je Liegenschaft; `name` ist genau der Name aus dem Stadtplan
        gebaeude(kennung_von(name), "werkstatt", name, werkstatt_mitte(p), list(WERKSTATT))

# Umland: Gruben, Höfe, Gärten, Wachturm (nicht bebaubar, aber Gebäude der Stadtansicht)
for name, p in STADTPLAN.items():
    if im_stadtgebiet(p):
        continue
    gebaeude(kennung_von(name), "umland", name, p, umlandgroesse(name))
    for anfang, betrieb in VORKOMMEN.items():
        if name.startswith(anfang):
            k.vorkommen(kennung_von(name), betrieb, p)

# Arbeitsstellen der Einwohner im Umland (Weide, Feld, Wald)
for name, (mitte, groesse, _) in UMLANDSTELLEN.items():
    gebaeude(f"Stelle{name}", "umlandstelle", f"{name} der Einwohner", mitte, list(groesse), eingang=False)

# Die vier alten Häuser der Kulisse (Wohnhaeuser(), Modell einwohner_buergerhaus, 7,6 × 8,1 m um die Mitte) – sie stehen immer.
# Seit dem 21.10.2026 stehen drei davon an der Ringgasse im Norden und Westen (der Süden ist Bauzone).
KULISSE = [((10, -29), (0, 1)), ((-8, -32), (0, -1)), ((1, -32), (0, -1)), ((-32, -14), (-1, 0))]
for n, (p, vorne) in enumerate(KULISSE, start=1):
    gebaeude(f"Kulisse{n}", "einwohnerhaus", f"Haus der Kulisse {n}", p, [8, 8.5], drehung_von(vorne))

# Doppelplätze der Spielhäuser am Ring: die Wohnhaus-Startplätze, in der Reihenfolge der Konten (Spieler zuerst)
ANZAHL_HAEUSER = 8
doppel, ring = doppelplaetze(ANZAHL_HAEUSER)
for n, (mitte, vorne, _) in enumerate(doppel, start=1):
    gebaeude(f"Wohnhaus{n}", "wohnhaus", f"Wohnhaus {n}", mitte, [2 * PLATZ_BREITE, PLATZ_TIEFE],
             drehung_von(vorne), reihenfolge=n, lage="mauerring", **({"haeuserAb": n} if n > 4 else {}))
belegt = {i for _, _, nr in doppel for i in nr}

# Die übrigen Ringplätze: Einwohnerhäuser (Ratsherren-, Bürger- und einfache Wohnhäuser)
for i, (mitte, vorne) in enumerate(ring):
    if i not in belegt:
        gebaeude(f"Ring{i + 1}", "einwohnerplatz", f"Ringplatz {i + 1}", mitte, [PLATZ_BREITE, PLATZ_TIEFE], drehung_von(vorne))

# Vorstadt vor dem Osttor (VorstadtPlaetze): 5 Reihen beiderseits der Vorstadtgasse (x = 64), nördlich und südlich der Landstraße
VORSTADT_BREITE, VORSTADT_TIEFE, VORSTADT_ABSTAND = 7.5, 6.2, 1.5 + 0.8 + 3.1
n = 0
for i in range(int((46 - 5) / VORSTADT_BREITE)):
    z = 5 + VORSTADT_BREITE * (i + 0.5)
    for seite in (-1, 1):
        for richtung in (-1, 1):
            n += 1
            gebaeude(f"Vorstadt{n}", "einwohnerplatz", f"Vorstadtplatz {n}", (64 + seite * VORSTADT_ABSTAND, z * richtung),
                     [VORSTADT_BREITE, VORSTADT_TIEFE], drehung_von((-seite, 0)))


# ── Bauzonen ─────────────────────────────────────────────────────────────────
# Zonen halten ABSTAND_STRASSE zum Rand der nächsten Straße und ABSTAND_FESTE zu festen Gebäuden (Vorplatz, Zaun, Schild).
# Alle Ecken liegen schon auf dem 2-m-Raster. Wo die Straße schräg läuft (Straße zum Steinbruch am Felsenhang), folgen die
# Zonen ihr in Stufen.
HOF = "M1" if HOF_ZONE_ALS_MAUERRING else "V4"
ZONEN = [
    # Kern: der Süden innerhalb der Ringgasse, wo bis 20.10.2026 Werkstätten zum Kauf und drei Kulissenhäuser standen. Werkstätten
    # stehen mit dem Eingang an der Ringgasse (Karrenstraße), Wohnhäuser auch an der Gasse der Gießerei Eisenbart.
    ("K1", "kern", [(4, 22), (36, 22), (36, 36), (4, 36)]),                                     # hinter der Gießerei Eisenbart
    ("K2", "kern", [(-12, 14), (8, 14), (8, 22), (4, 22), (4, 36), (-12, 36)]),                 # südlich vom Markt
    ("K3", "kern", [(-36, 26), (-12, 26), (-12, 36), (-36, 36)]),                               # hinter dem Weinmarkt
    # Vor dem Westtor an der Weststraße (Karrenstraße, z = 6) und am Weg zum Holzschlag im Eichenforst
    ("V1", "vorstadt", [(-118, -12), (-92, -12), (-92, 2), (-118, 2)]),               # Nordseite am Ende der Straße
    ("V2", "vorstadt", [(-120, 10), (-104, 10), (-104, 24), (-120, 24)]),             # Südseite, links vom Weg zum Eichenforst
    ("V3", "vorstadt", [(-96, 10), (-80, 10), (-80, 24), (-96, 24)]),                 # Südseite, rechts davon
    ("V5", "vorstadt", [(-112, 28), (-102, 28), (-102, 36), (-112, 36)]),             # am Weg zum Eichenforst
    # Vor der Westmauer an der Straße zum Steinbruch am Felsenhang (zuerst nach Norden, dann nach Nordosten)
    (HOF, "mauerring" if HOF_ZONE_ALS_MAUERRING else "vorstadt", [(-80, -58), (-66, -58), (-66, -44), (-62, -44), (-62, -26), (-80, -26)]),
    ("V6", "vorstadt", [(-76, -66), (-66, -66), (-66, -58), (-76, -58)]),             # Ecke, wo die Straße abknickt
    ("V7", "vorstadt", [(-42, -62), (-34, -62), (-34, -66), (-16, -66), (-16, -52), (-42, -52)]),   # südlich der Straße, an der Mauer
    ("V8", "vorstadt", [(-56, -78), (-48, -78), (-48, -82), (-40, -82), (-40, -84), (-32, -84), (-32, -96), (-16, -96),
                        (-16, -82), (-32, -82), (-32, -74), (-40, -74), (-40, -72), (-48, -72), (-48, -68), (-56, -68)]),  # nördlich der Straße
]
for kennung, art, poly in ZONEN:
    k.bauzone(kennung, art, poly)

# Probeplan: So passen die geforderten Gebäude hinein (Art, Zone, Rechteck x0, z0, x1, z1, Drehung). Nur zum Testen und Prüfen;
# die Spieler dürfen später frei setzen. Größen nach LEVELDESIGN: Werkstatt und Stadthof 16 × 14 m, Wohnhaus 8 × 10 m
# (Breite × Tiefe, die Vorderseite liegt an der Breite), jeweils mit 2 m Vorplatz.
GROESSE = {"Werkstatt": (16, 14), "Stadthof": (16, 14), "Wohnhaus": (8, 10)}
PLAN = [
    ("Werkstatt", "V1", -118, -12, -102, 2, 0), ("Wohnhaus", "V1", -100, -8, -92, 2, 0),
    ("Werkstatt", "V2", -120, 10, -104, 24, 180), ("Werkstatt", "V3", -96, 10, -80, 24, 180),
    ("Wohnhaus", "V5", -112, 28, -102, 36, 90),
    ("Stadthof", HOF, -80, -58, -66, -42, 90), ("Stadthof", HOF, -76, -42, -62, -26, 90),
    ("Wohnhaus", "V6", -76, -66, -66, -58, 90),
    ("Werkstatt", "V7", -32, -66, -16, -52, 180), ("Wohnhaus", "V7", -42, -62, -34, -52, 180),
    ("Wohnhaus", "V8", -56, -78, -48, -68, 0), ("Wohnhaus", "V8", -48, -82, -40, -72, 0), ("Wohnhaus", "V8", -40, -84, -32, -74, 0),
    ("Werkstatt", "V8", -32, -96, -16, -82, 0),
    ("Werkstatt", "K1", 4, 22, 20, 36, 0), ("Werkstatt", "K1", 20, 22, 36, 36, 0),
    ("Werkstatt", "K2", -12, 22, 4, 36, 0), ("Wohnhaus", "K2", -2, 14, 8, 22, 90),
    ("Wohnhaus", "K3", -36, 26, -28, 36, 0), ("Wohnhaus", "K3", -28, 26, -20, 36, 0), ("Wohnhaus", "K3", -20, 26, -12, 36, 0),
]
k.d["probeplan"] = []
for art, zone, x0, z0, x1, z1, drehung in PLAN:
    k.d["probeplan"].append({"art": art, "zone": zone, "punkt": [(x0 + x1) / 2, (z0 + z1) / 2],
                             "groesse": list(GROESSE[art]), "drehung": drehung})

if HOF_ZONE_ALS_MAUERRING:
    k.fiktion("Zone M1 liegt knapp vor der Westmauer, zählt aber als Mauerring: Innerhalb der Mauer ist kein Platz für einen "
              "Stadthof (16 × 14 m) frei. Mit HOF_ZONE_ALS_MAUERRING = False in tools/karten/greybox.py wird sie „Vorstadt“.")
k.fiktion("Die Greybox hat keine Startgrundstücke, sondern Wohnhausplätze (Doppelplätze am Ring); die Reihenfolge ist die der Konten. "
          "Heute spielen höchstens 4 Häuser (Spieler und drei Rivalen), Wohnhausplatz 5 bis 8 gelten erst bei mehr Konten.")


# ── Prüfungen der Greybox ────────────────────────────────────────────────────
ABSTAND_STRASSE = 0.5      # Meter zwischen Zone und Straßenrand (Köln: 3,3 m ab Straßenmitte bei 6,5 m breiten Straßen)
ABSTAND_FESTE = 2.0        # Meter zwischen Zone und festem Gebäude
ABSTAND_MAUER = 2.0        # Meter zwischen Zone und Mauermitte (die Mauer ist 2 m dick)
STRASSENNAH = 7.0          # Eingangsregel des Kerns: mindestens 2 Felder der Vorplatzreihe höchstens 7 m von der Straßenmitte (wie kapazitaet.py)
MIN_WERKSTAETTEN, MIN_WOHNHAEUSER, MIN_STADTHOEFE = 6, 8, 2
ABSTAND_WERKSTATT = 2.0    # Meter zwischen den Flächen zweier Werkstätten der Stadt (Platz für Zaun und Durchgang)


def strecken(poly):
    return [(poly[i], poly[(i + 1) % len(poly)]) for i in range(len(poly))]


def abstand_strecken(a, b, c, d):
    if schnitt(a, b, c, d):
        return 0.0
    return min(abstand_strecke(a, c, d), abstand_strecke(b, c, d), abstand_strecke(c, a, b), abstand_strecke(d, a, b))


def abstand_zone_rechteck(poly, rechteck):
    """Kürzester Abstand zwischen einem Vieleck und einem achsparallelen Rechteck (0 bei Überdeckung)."""
    x0, z0, x1, z1 = rechteck
    ecken = [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
    if any(_drin(p, poly) for p in ecken) or any(x0 <= p[0] <= x1 and z0 <= p[1] <= z1 for p in poly):
        return 0.0
    return min(abstand_strecken(a, b, c, d) for a, b in strecken(poly) for c, d in strecken(ecken))


def rechteck_von(f):
    breite, tiefe = f["groesse"]
    wx, wz = (breite, tiefe) if f["drehung"] % 180 == 0 else (tiefe, breite)
    x, z = f["punkt"]
    return (x - wx / 2, z - wz / 2, x + wx / 2, z + wz / 2)


def abstand_zone_linie(poly, linie):
    stuecke = [(linie[i], linie[i + 1]) for i in range(len(linie) - 1)]
    if any(_drin(p, poly) for p in linie):
        return 0.0
    return min(abstand_strecken(a, b, c, d) for a, b in strecken(poly) for c, d in stuecke)


def felder_von(poly):
    xs = [p[0] for p in poly]; zs = [p[1] for p in poly]
    return {(i, j) for i in range(int(min(xs) // RASTER), int(max(xs) // RASTER) + 1)
            for j in range(int(min(zs) // RASTER), int(max(zs) // RASTER) + 1)
            if _drin(((i + 0.5) * RASTER, (j + 0.5) * RASTER), poly)}


def vorderseite(x0, z0, x1, z1, drehung):
    """Die Vorderseite eines Rechtecks als Strecke: bei Drehung 0 die Südseite (+z), 90 Ost, 180 Nord, 270 West."""
    return {0: ((x0, z1), (x1, z1)), 90: ((x1, z0), (x1, z1)), 180: ((x0, z0), (x1, z0)), 270: ((x0, z0), (x0, z1))}[drehung]


def vorplatzreihe(x0, z0, x1, z1, drehung):
    """Mittelpunkte der Rasterfelder in der Reihe an der Vorderseite (eine Feldtiefe = 2 m Vorplatz)."""
    xs = [x0 + RASTER * (i + 0.5) for i in range(int((x1 - x0) // RASTER))]
    zs = [z0 + RASTER * (j + 0.5) for j in range(int((z1 - z0) // RASTER))]
    return {0: [(x, z1 - RASTER / 2) for x in xs], 180: [(x, z0 + RASTER / 2) for x in xs],
            90: [(x1 - RASTER / 2, z) for z in zs], 270: [(x0 + RASTER / 2, z) for z in zs]}[drehung]


def kapazitaet(karte):
    """Prüft Abstände und Überlappungen der Bauzonen und den Probeplan. Gibt (Fehler, Hinweise, SVG-Ebene) zurück."""
    d = karte.d
    fehler, hinweise = [], []
    zonen = {z["kennung"]: z for z in d["bauzonen"]}
    mauern = ([d["mauer"]["linie"]] if d["mauer"] else []) + [m["linie"] for m in d["nebenmauern"]]
    karren = [w for w in d["wege"] if w["klasse"] in KARRENSTRASSEN]

    # 1. Zonen überdecken keine Gebäude und keine Wege, halten Abstand zur Mauer und liegen auf dem Raster
    for z in zonen.values():
        for p in z["flaeche"]:
            if p[0] % RASTER or p[1] % RASTER:
                fehler.append(f"Zone {z['kennung']}: Ecke {p} liegt nicht auf dem 2-m-Raster")
        for f in d["feste"]:
            if "drehung" not in f:
                continue
            a = abstand_zone_rechteck(z["flaeche"], rechteck_von(f))
            if a < ABSTAND_FESTE:
                fehler.append(f"Zone {z['kennung']}: nur {a:.1f} m zu {f['kennung']} ({f['name']}), mindestens {ABSTAND_FESTE:g} m")
        for w in d["wege"]:
            a = abstand_zone_linie(z["flaeche"], w["linie"])
            if a < w["breite"] / 2 + ABSTAND_STRASSE:
                fehler.append(f"Zone {z['kennung']}: nur {a:.1f} m zur Mitte von {w['name']} "
                              f"(Straße {w['breite']:g} m breit, mindestens {w['breite'] / 2 + ABSTAND_STRASSE:g} m)")
        for m in mauern:
            a = abstand_zone_linie(z["flaeche"], m)
            if a < ABSTAND_MAUER:
                fehler.append(f"Zone {z['kennung']}: nur {a:.1f} m zur Mauer, mindestens {ABSTAND_MAUER:g} m")
        # jede Zone liegt an einer Straße (Köln: höchstens 8 m zu einer Karrenstraße; hier gemessen vom Straßenrand)
        n = min(abstand_zone_linie(z["flaeche"], w["linie"]) - w["breite"] / 2 for w in karren)
        if n > 8:
            fehler.append(f"Zone {z['kennung']}: nächste Karrenstraße {n:.1f} m vom Straßenrand entfernt (höchstens 8 m)")

    # 2. Zonen überlappen einander nicht
    felder = {kz: felder_von(z["flaeche"]) for kz, z in zonen.items()}
    kennungen = list(zonen)
    for i, a in enumerate(kennungen):
        for b in kennungen[i + 1:]:
            if felder[a] & felder[b]:
                fehler.append(f"Zonen {a} und {b} überlappen")

    # 3. Probeplan: jedes Gebäude liegt ganz in seiner Zone, überlappt kein anderes und hat die Vorderseite an einer Straße
    #    (Eingangsregel des Kerns: mindestens 2 Rasterfelder der Vorplatzreihe höchstens 7 m von der Mittellinie einer passenden Straße – Werkstatt und Stadthof Karrenstraße, Wohnhaus jede);
    #    ein Stadthof steht nur in Kern oder Mauerring
    belegt = {}
    je_zone = {kz: {} for kz in zonen}
    ebene = []
    for art, kz, x0, z0, x1, z1, drehung in PLAN:
        wx, wz = (x1 - x0, z1 - z0)
        breite, tiefe = GROESSE[art]
        if (wx, wz) != ((breite, tiefe) if drehung % 180 == 0 else (tiefe, breite)):
            fehler.append(f"Probeplan {art} in {kz}: Maße {wx:g} × {wz:g} passen nicht zu {breite} × {tiefe} bei Drehung {drehung}")
        z = zonen.get(kz)
        if z is None:
            fehler.append(f"Probeplan {art}: Zone {kz} gibt es nicht")
            continue
        zellen = {(i, j) for i in range(int(x0 // RASTER), int(x1 // RASTER)) for j in range(int(z0 // RASTER), int(z1 // RASTER))}
        if not zellen <= felder[kz]:
            fehler.append(f"Probeplan {art} in {kz} ({x0:g}, {z0:g})–({x1:g}, {z1:g}) liegt nicht ganz in der Zone")
        for zelle in zellen:
            if zelle in belegt:
                fehler.append(f"Probeplan {art} in {kz} überlappt {belegt[zelle]}")
                break
            belegt[zelle] = f"{art} in {kz} ({x0:g}, {z0:g})"
        if art == "Stadthof" and z["art"] not in ("kern", "mauerring"):
            fehler.append(f"Probeplan Stadthof in {kz}: Zone ist {z['art']}, erlaubt sind nur Kern und Mauerring")
        vorn = vorderseite(x0, z0, x1, z1, drehung)
        wege = d["wege"] if art == "Wohnhaus" else karren
        reihe = vorplatzreihe(x0, z0, x1, z1, drehung)
        nah = sum(1 for c in reihe if min(abstand_linie(c, w["linie"]) for w in wege) <= STRASSENNAH)
        if nah < 2:
            fehler.append(f"Probeplan {art} in {kz} ({x0:g}, {z0:g}): nur {nah} Felder der Vorplatzreihe höchstens {STRASSENNAH:g} m von der Straßenmitte (mindestens 2)")
        je_zone[kz][art] = je_zone[kz].get(art, 0) + 1
        ebene.append(f'<rect x="{x0:g}" y="{z0:g}" width="{wx:g}" height="{wz:g}" fill="{FARBE[art]}" opacity="0.75" '
                     f'stroke="#1F2326" stroke-width="0.3"/>')
        ex, ez = (vorn[0][0] + vorn[1][0]) / 2, (vorn[0][1] + vorn[1][1]) / 2
        ebene.append(f'<circle cx="{ex:g}" cy="{ez:g}" r="0.9" fill="#fff"/>')

    for kz, z in zonen.items():
        zaehlung = ", ".join(f"{n} {art}" for art, n in sorted(je_zone[kz].items())) or "–"
        hinweise.append(f"Zone {kz} ({z['art']}, {len(felder[kz]) * 4} m²): Probeplan {zaehlung}")
    n = lambda art: sum(1 for p in PLAN if p[0] == art)
    hinweise.append(f"Probeplan zusammen: {n('Werkstatt') + n('Stadthof')} Plätze zu 16 × 14 m (davon {n('Stadthof')} Stadthof in Kern oder "
                    f"Mauerring) und {n('Wohnhaus')} Wohnhäuser zu 8 × 10 m")
    if n("Stadthof") < MIN_STADTHOEFE:
        fehler.append(f"Nur {n('Stadthof')} Stadthof-Plätze (verlangt {MIN_STADTHOEFE})")
    if n("Werkstatt") + n("Stadthof") < MIN_WERKSTAETTEN:
        fehler.append(f"Nur {n('Werkstatt') + n('Stadthof')} Plätze zu 16 × 14 m (verlangt {MIN_WERKSTAETTEN})")
    if n("Wohnhaus") < MIN_WOHNHAEUSER:
        fehler.append(f"Nur {n('Wohnhaus')} Wohnhaus-Plätze (verlangt {MIN_WOHNHAEUSER})")

    # 4. Wohnhausplätze: gleich weit vom Markt wie bei den Startgrundstücken (±10 %), Eingang an einem Weg –
    #    gilt für die ersten vier (heute spielenden) Häuser
    plaetze = sorted((f for f in d["feste"] if f["art"] == "wohnhaus"), key=lambda f: f["reihenfolge"])
    if len(plaetze) != ANZAHL_HAEUSER:
        fehler.append(f"{len(plaetze)} Wohnhausplätze statt {ANZAHL_HAEUSER}")
    abst = [abstand(f["punkt"], (0, 0)) for f in plaetze[:4]]
    mittel = sum(abst) / len(abst)
    for f, a in zip(plaetze, abst):
        if abs(a - mittel) > 0.10 * mittel:
            fehler.append(f"{f['kennung']}: {a:.1f} m zum Markt, Mittel {mittel:.1f} m (±10 %)")
    for f in plaetze:
        if min(abstand_linie(f["eingang"], w["linie"]) - w["breite"] / 2 for w in d["wege"]) > 1.5:
            fehler.append(f"{f['kennung']}: Eingang liegt nicht an einem Weg")
    hinweise.append("Wohnhausplätze 1–4: " + ", ".join(f"{a:.1f} m" for a in abst) + f" zum Markt (Mittel {mittel:.1f} m); Plätze 5–8: "
                    + ", ".join(f"{abstand(f['punkt'], (0, 0)):.1f} m" for f in plaetze[4:]))

    # 5. Werkstattplätze der Stadt (16 × 14 m): Keine Fläche überlappt ein anderes Gebäude, zwischen zwei Werkstätten bleiben
    #    ABSTAND_WERKSTATT Meter. Auf dem Haus (die Fläche ohne den Vorplatz) liegt keine Straße und keine Mauer. Der Hof liegt im
    #    Vorplatz, und dort beginnt die eigene Gasse.
    def ecken(r):
        return [(r[0], r[1]), (r[2], r[1]), (r[2], r[3]), (r[0], r[3])]

    def ueberlappen(a, b):
        return min(a[2], b[2]) > max(a[0], b[0]) and min(a[3], b[3]) > max(a[1], b[1])

    def ohne_vorplatz(f):
        x0, z0, x1, z1 = rechteck_von(f)
        return (x0, z0, x1, z1 - RASTER) if f["art"] == "werkstatt" else (x0, z0, x1, z1)

    werkstaetten = list({tuple(f["punkt"]): f for f in d["feste"] if f["art"] == "werkstatt"}.values())   # je Platz eine
    andere = [f for f in d["feste"] if "drehung" in f and f["art"] != "werkstatt"]
    for n, f in enumerate(werkstaetten):
        r = rechteck_von(f)
        if (r[2] - r[0], r[3] - r[1]) != WERKSTATT:
            fehler.append(f"Werkstatt {f['name']}: {r[2] - r[0]:g} × {r[3] - r[1]:g} m statt {WERKSTATT[0]} × {WERKSTATT[1]} m")
        for g in andere:
            if ueberlappen(r, rechteck_von(g)):
                fehler.append(f"Werkstatt {f['name']} überlappt {g['kennung']} ({g['name']})")
        for g in werkstaetten[n + 1:]:
            if abstand_zone_rechteck(ecken(r), rechteck_von(g)) < ABSTAND_WERKSTATT:
                fehler.append(f"Werkstätten {f['name']} und {g['name']}: weniger als {ABSTAND_WERKSTATT:g} m Abstand")
        for m in mauern:
            if abstand_zone_linie(ecken(r), m) < 1:
                fehler.append(f"Werkstatt {f['name']}: steht auf der Mauer")
        hof = (f["punkt"][0], f["punkt"][1] + WERKSTATT_VORN + HOF_VORN)
        if not (r[0] < hof[0] < r[2] and r[3] - RASTER < hof[1] < r[3]):
            fehler.append(f"Werkstatt {f['name']}: Hof {hof} liegt nicht im Vorplatz")
        if not any(w["klasse"] == "gasse" and w["linie"][0] == list(hof) for w in d["wege"]):
            fehler.append(f"Werkstatt {f['name']}: keine Gasse beginnt am Hof {hof}")

    # 6. Keine Straße läuft über eine Werkstatt und keine Gasse über ein Haus in der Stadt (Abstand zur Mitte mindestens die halbe
    #    Breite). Vorplätze sind frei; eine Gasse, die an der Tür eines Hauses beginnt (Hof, Domstufen), gehört zu ihm.
    for w in d["wege"]:
        for g in werkstaetten + (andere if w["klasse"] == "gasse" else []):
            if g["kennung"] == "Markt" or not im_stadtgebiet(g["punkt"]):
                continue                       # die Gassen enden in der Mitte des Marktplatzes
            if "eingang" in g and abstand(w["linie"][0], g["eingang"]) < 1:
                continue
            a = abstand_zone_linie(ecken(ohne_vorplatz(g)), w["linie"])
            if a < w["breite"] / 2:
                fehler.append(f"{w['name']} läuft über {g['kennung']} ({g['name']}): {a:.1f} m zur Mitte, {w['breite']:g} m breit")
    hinweise.append("Werkstattplätze der Stadt (16 × 14 m, Mitte der Fläche): " + ", ".join(
        f"{'Weinmarkt' if 'Weinmarkt' in f['name'] else f['name']} ({f['punkt'][0]:g}|{f['punkt'][1]:g})" for f in werkstaetten))
    return fehler, hinweise, "\n".join(ebene)


if __name__ == "__main__":
    print("Greybox-Stadt:")
    k.schreiben(pruefung=kapazitaet, startzahl=0)
