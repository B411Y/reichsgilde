#!/usr/bin/env python3
"""
Greybox-Stadt – die Platzhalter-Stadt des Spiels als Kartendatei (Kartenformat 2, Bereich Leveldesign).

Aufruf (aus dem Projektordner):
    python3 tools/karten/greybox.py

Schreibt game/data/karten/greybox.json und docs/karten/greybox.svg (dazu greybox-belegung.svg), aber nur, wenn alle
Regeln erfüllt sind (siehe karte.py und „Prüfungen der Greybox“ unten).

Seit PR #163 ist diese Karte die **einzige Quelle** dafür, wo in der Greybox-Stadt etwas steht: Die Stadtansicht zeichnet Mauer, Wege,
Gebäude, Plätze und Natur nur noch aus ihr. Bis zum Level-Editor entsteht sie nur aus diesem Skript, nie von Hand. Die Zahlen stammen aus
dem Code der Stadtansicht, wie er bis 21.10.2026 die Stadt fest gezeichnet hat (game/scripts/Stadtansicht*.cs):
  • Stadtplan, Gassen, Abzweige, Wegkreuz, Tore:   Stadtansicht.cs
  • Umlanddrehung, Felsberg:                       Stadtansicht.Umland.cs (UmlandDrehungGrad, BergGroesse, BergAnsatz)
  • Ringgasse, Ringplätze, Vorstadt, Umlandstellen: Stadtansicht.Wohnviertel.cs (RingPlaetze, VorstadtPlaetze, Umlandstellen)
  • Doppelplätze der Spielhäuser:                  Stadtansicht.Spielhaeuser.cs (SpielhausPlaetze)
  • Wald und Streu:                                Stadtansicht.Natur.cs und Stadtansicht.cs (Wald, NachAussen, ImWegFuerWald)
Anker (Halteplatz der Karren, Gatter) liest das Skript aus den Modellen (katalog_pruefen.anker), wie das Spiel.

Koordinaten: **Weltkoordinaten der Stadtansicht in Metern** (Godot x/z), `punkt` = Mitte der Grundfläche des Bausteins im Katalog.
Werkstätten in der Stadt gibt es seit 20.10.2026 nur noch vier (Startwerkstatt am Weinmarkt und die drei Rivalen); wo die Werkstätten
zum Kauf standen, liegen seit 21.10.2026 die Kernzonen K1 bis K3.
"""
import math
import os
import struct
import sys

sys.dont_write_bytecode = True   # keinen __pycache__-Ordner im Repo anlegen
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from karte import (Karte, RASTER, KARRENSTRASSEN, abstand, abstand_linie, abstand_vielecke, drehung_zu, dreh, ecken_von,  # noqa: E402
                   groesse_von, hof_von, im_vieleck, kennung_von, lokal)
from kapazitaet import FARBE, _drin  # noqa: E402
from katalog_pruefen import anker  # noqa: E402

# Soll der Stadthof getestet werden können, obwohl innerhalb der Mauer kein Platz für 16 × 14 m frei ist? Dann zählt die
# Zone „M1“ (vor der Westmauer an der Straße zum Steinbruch am Felsenhang) als Mauerring. Auf False: ehrlich „Vorstadt“ („V4“),
# dann gibt es in dieser Karte keinen Stadthof-Platz. Die Abweichung steht in `fiktion`.
HOF_ZONE_ALS_MAUERRING = True


# ── Feste Punkte ─────────────────────────────────────────────────────────────
MAUERRAND = 50.0                       # Mauer bei x, z = ±50 (2 m dick)
RINGGASSE = 38.0                       # Ringgasse bei x, z = ±38
MARKT = (0.0, 0.0)
OSTTOR, WESTTOR = (50.0, 0.0), (-50.0, 6.0)
LANDSTRASSE = (90.0, -2.0)             # Ende der Landstraße vor dem Osttor
WEGKREUZ = (76.0, -1.3)                # hier zweigen die Wege zu den Gruben, Höfen und Gärten im Osten ab
WESTGASSE_KNICK, WESTSTRASSE = (-12.0, 6.0), (-118.0, 6.0)
RATHAUS, RATHAUSTUER = (0.0, -20.0), (-1.8, -14.5)   # Rathaus; das Gassennetz beginnt vor dem Rundbogen der Markthalle
DOM, DOMTUER = (-20.0, -14.0), (-20.0, 4.2)          # Ursprung des Dom-Modells; das Gassennetz beginnt am Fuß der Domstufen
PLATZ_BREITE, PLATZ_TIEFE = 8.5, 8.0   # Ringplatz der Einwohnerhäuser

# Werkstattplätze in der Stadt: Punkt der Stadtansicht (Mitte des alten 10 × 8-m-Hauses). Die Fläche (16 × 14 m mit Vorplatz) liegt mit
# ihrer Mitte 1 m dahinter, die Tür zeigt nach Süden. Der Weinmarkt ist der Startplatz des Spielers (für jeden Beruf derselbe).
WERKSTATTPLAETZE = {"Weinmarkt": (-23, 17), "Gießerei Eisenbart": (22, 14), "Tischlerei Eichwald": (22, -12),
                    "Maurerei Kieselbach": (25, -28)}
WERKSTATT_VORN = 1

# Gassen vom Hof eines Werkstattplatzes zum Markt: die Knicke dazwischen. Die Gasse läuft vom Hof (im Vorplatz) an der Vorderseite
# entlang und dann zum Markt, nie durch ein Haus.
GASSEN_KNICKE = {
    "Weinmarkt": [(-34, 22.5), (-34, 6), WESTGASSE_KNICK],   # nach Westen, an der Ringgasse entlang nach Norden, dann die Westgasse
    "Gießerei Eisenbart": [(12, 19.5), (12, 12)],         # nach Westen am Vorplatz entlang, am Markt entlang nach Norden
    "Tischlerei Eichwald": [(12.5, -6.5)],                # nach Westen bis an den Markt
    "Maurerei Kieselbach": [(11, -22.5), (11, -13)],      # zwischen Rathaus und Eichwald nach Süden
}

# Umlandbetriebe: Mitte, und wo ihr Weg von der Straße abgeht. Die Vorderseite (Halteplatz der Karren) zeigt zum Abzweig, außer bei den
# Gruben mit Handdrehung: Ihr Felsberg läge sonst einem Nachbarn im Weg.
UMLAND = {
    "Steinbruch Kieselbach": ((74, 66), (80, 26)),
    "Steinbruch am Felsenhang": ((-4.5, -80), (-62, -58)), # vom Westtor um die Nordwestecke der Mauer herum (bis 21.10.2026 bei x −6)
    "Kräutergarten am Bach": ((106, 70), (86, 18)),
    "Bauernhof an der Mühle": ((124, -62), (80, -26)),
    "Duftgarten am Lavendelhang": ((124, 8), LANDSTRASSE),   # am Ende der Landstraße weiter nach Osten
    "Wachturm an der Landstraße": ((80, -10), (80, -1)),    # direkt an der Landstraße
    "Klosterhof am Osthang": ((134, 80), (98, 57)),         # auf dem Weg zum Kräutergarten
    "Grube Eisenbart": ((102, -12), (90, -2)),
    "Grube am Silberberg": ((100, 32), (86, 18)),
    "Grube im Tannenwald": ((82, -66), (80, -26)),
    "Holzschlag Eichwald": ((-92, -30), (-84, 6)),           # im Wald vor dem Westtor, nördlich der Weststraße
    "Holzschlag im Eichenforst": ((-94, 46), (-102, 6)),     # südlich der Weststraße
}
IM_WESTEN = {"Holzschlag Eichwald", "Holzschlag im Eichenforst", "Steinbruch am Felsenhang"}   # erreicht man durchs Westtor
HANDDREHUNG = {"Grube am Silberberg": -110, "Grube Eisenbart": -60}
# Knicke im Weg vom Abzweig zum Halteplatz: Die Bauzonen an diesen Wegen liegen an der Linie, die bis 21.10.2026 zur Mitte des Betriebs
# führte; bis zum Knick folgt der Weg ihr, erst dann biegt er zum Halteplatz ab.
WEGKNICKE = {"Steinbruch am Felsenhang": [(-16, -76.7)], "Holzschlag im Eichenforst": [(-97.5, 36)]}
BERGGROESSE = {"Grube am Silberberg": 0.85}   # der Felsberg schrumpft um den Punkt, an dem er an den Felshang der Grube stößt
BERG_ANSATZ = -5                              # lokales z, wo der Berg an das Grubenmodell stößt
UMLANDKATALOG = {"Grube": "grube", "Holzschlag": "holzschlag", "Steinbruch": "steinbruch", "Kräutergarten": "kraeutergarten",
                 "Duftgarten": "duftgarten", "Bauernhof": "bauernhof", "Klosterhof": "klosterhof", "Wachturm": "wachturm"}
VORKOMMEN = {"Grube": "Mine", "Steinbruch": "Steinbruch", "Holzschlag": "Holzschlag"}   # Namensanfang → Rohstoffbetrieb

# Arbeitsstellen der Einwohner (Weide, Feld, Wald): Mitte und wo ihr Fußweg von der Weststraße abgeht. Die Vorderseite (Gatter) zeigt
# zum Zugang.
UMLANDSTELLEN = {"Weide": ("weide", (-70, 26), (-70, 6)), "Feld": ("feld", (-60, 62), (-58, 6)), "Wald": ("sammelwald", (-68, -14), (-66, 6))}

# Die vier Häuser der Kulisse (Modell einwohner_buergerhaus): Mitte und Blickrichtung der Tür (zur nächsten Gasse). Das erste stand bis
# 21.10.2026 bei (10|−29); seine Fläche (8 × 8,5 m) reichte 0,25 m in die des Rathauses.
KULISSE = [((10, -29.5), (0, 1)), ((-8, -32), (0, -1)), ((1, -32), (0, -1)), ((-32, -14), (-1, 0))]

# Vorstadt vor dem Osttor: Plätze beiderseits der Vorstadtgasse, nördlich und südlich der Landstraße
VORSTADT_GASSE_X, VORSTADT_RAND = 64, 46
VORSTADT_BREITE, VORSTADT_TIEFE, VORSTADT_ABSTAND = 7.5, 6.2, 1.5 + 0.8 + 3.1   # Abstand = Gassenrand + Vorplatz + halbe Tiefe

ANZAHL_HAEUSER = 8                     # Wohnhausplätze am Ring (heute spielen höchstens 4 Häuser)


def im_stadtgebiet(p):
    return abs(p[0]) < MAUERRAND and abs(p[1]) < MAUERRAND


def drehung_von(vorne):
    """Drehung in Grad (Godot Rotation.Y) für eine Blickrichtung (x, z): 0 = +z, 90 = +x, 180 = −z, 270 = −x."""
    return round(math.degrees(math.atan2(vorne[0], vorne[1]))) % 360


def f32(x):
    """Wie `float` in C#: Die Reihenfolge der Plätze hängt von Gleichständen in der Sortierung ab."""
    return struct.unpack("f", struct.pack("f", x))[0]


def laenge32(p):
    return f32(math.sqrt(f32(f32(p[0] * p[0]) + f32(p[1] * p[1]))))


# ── Ringplätze und Doppelplätze der Spielhäuser (so wie die Stadtansicht sie bis 21.10.2026 gerechnet hat) ──
def ring_plaetze():
    """Die Einzelplätze am Ring (Mitte, Blickrichtung): je Seite eine Reihe zwischen Ringgasse und Mauer, die Vorderseite zur Gasse."""
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
    """Die Doppelplätze der Spielhäuser in Konto-Reihenfolge: je zwei benachbarte Ringplätze derselben Seite, die nächsten am Markt
    zuerst. Gibt (Mitte, Blickrichtung, Nummern der beiden Ringplätze) und die Ringplätze zurück."""
    ring = ring_plaetze()
    paare = []
    for i in range(len(ring) - 1):
        (am, av), (bm, bv) = ring[i], ring[i + 1]
        if av != bv or math.dist(am, bm) > f32(PLATZ_BREITE + 0.1):
            continue
        paare.append(((f32((am[0] + bm[0]) / 2), f32((am[1] + bm[1]) / 2)), av, (i, i + 1)))
    paare.sort(key=lambda p: (laenge32(p[0]), p[0][0], p[0][1]))
    gewaehlt = []
    for p in paare:
        if len(gewaehlt) >= anzahl:
            break
        if any(set(g[2]) & set(p[2]) for g in gewaehlt):
            continue
        gewaehlt.append(p)
    return gewaehlt, ring


def vorstadt_plaetze():
    """Plätze in der Vorstadt (Mitte, Blickrichtung), die nächsten an der Landstraße zuerst."""
    liste = []
    for i in range(int((VORSTADT_RAND - 5) / VORSTADT_BREITE)):
        z = 5 + VORSTADT_BREITE * (i + 0.5)
        for seite in (-1, 1):              # west, ost
            for richtung in (-1, 1):       # nord, süd
                liste.append(((VORSTADT_GASSE_X + seite * VORSTADT_ABSTAND, z * richtung), (-seite, 0)))
    return liste


# ── Die Karte ────────────────────────────────────────────────────────────────
k = Karte("greybox", "Greybox-Stadt", "Die Platzhalter-Stadt des Spiels (bis 21.10.2026 fest in game/scripts/Stadtansicht*.cs gezeichnet), "
          "Mauer bei ±50 m", schritt=0.05)
k.d["achsen"] = ("x = Osten, z = Süden (−z = Norden), Ursprung am Marktplatz – Weltkoordinaten der Stadtansicht in Metern "
                 "(Godot x/z, y = oben)")

# ── Mauer und Tore ───────────────────────────────────────────────────────────
# Ein Quadrat bei ±50 m, geschlossen; Ecktürme an den Knicken und die Torburgen setzt das Spiel aus dem Baukasten stadtmauer.
R = MAUERRAND
k.mauer("Stadtmauer", [(-R, -R), (R, -R), (R, R), (-R, R)], geschlossen=True)
k.tor("Stadtmauer", "Osttor", OSTTOR)
k.tor("Stadtmauer", "Westtor", WESTTOR)

# ── Wege ─────────────────────────────────────────────────────────────────────
# Haupt-, Ring- und Land-/Weststraßen sind Karrenstraßen, Gassen und Fußwege nicht. Türen von Häusern und Kulissen verbindet das Spiel
# selbst mit dem nächsten Weg; Werkstätten, Umlandbetriebe und Umlandstellen haben ihren eigenen Weg bis an den Hof bzw. das Gatter.
k.weg("Ostgasse", "haupt", [MARKT, OSTTOR], breite=5)
k.weg("Landstraße", "land", [OSTTOR, LANDSTRASSE], breite=4)
k.weg("Westgasse", "haupt", [MARKT, WESTGASSE_KNICK, (-38, 6), WESTTOR], breite=5)
k.weg("Weststraße", "land", [WESTTOR, WESTSTRASSE], breite=4)
r = RINGGASSE
k.weg("Ringgasse", "ring", [(-r, -r), (r, -r), (r, r), (-r, r), (-r, -r)], breite=3)
k.weg("Domgasse", "gasse", [DOMTUER, (DOMTUER[0], WESTGASSE_KNICK[1])], breite=2.5)   # von den Stufen vor dem Domportal zur Westgasse
k.weg("Rathausgasse", "gasse", [RATHAUSTUER, MARKT], breite=2.5)                      # vom Rundbogen der Markthalle über den Markt
k.weg("Vorstadtgasse", "gasse", [(VORSTADT_GASSE_X, -42.5), (VORSTADT_GASSE_X, 42.5)], breite=3)   # kreuzt die Landstraße

# Gassen vom Hof jeder Werkstatt zum Markt (der Hof liegt 0,5 m hinter der Vorderkante der Fläche)
werkstatt_mitte = {name: (p[0], p[1] - WERKSTATT_VORN) for name, p in WERKSTATTPLAETZE.items()}
for name, (x, z) in werkstatt_mitte.items():
    hof = (x, z + 7 - 0.5)
    k.weg(f"Gasse {name}", "gasse", [hof] + GASSEN_KNICKE[name] + [MARKT], breite=3.5)

# Umlandbetriebe: Drehung (Vorderseite zum Abzweig oder von Hand) und Halteplatz der Karren aus dem Modell („Anker_Karren“)
umland_drehung, umland_hof = {}, {}
for name, (p, abzweig) in UMLAND.items():
    umland_drehung[name] = HANDDREHUNG.get(name, drehung_zu(abzweig[0] - p[0], abzweig[1] - p[1]))
    ax, az = anker(UMLANDKATALOG[name.split(" ")[0]], "Anker_Karren")
    dx, dz = dreh(ax, az, umland_drehung[name])
    umland_hof[name] = (p[0] + dx, p[1] + dz)

# Wege zu den Plätzen vor der Stadt (Karren fahren sie, deshalb Klasse „land“): vom Kreuz (Wegkreuz im Osten, Westtor im Westen) zum
# Abzweig, wenn der nicht an der Straße liegt, und vom Abzweig bis zum Halteplatz der Karren
for name, (p, abzweig) in UMLAND.items():
    kreuz = WESTTOR if name in IM_WESTEN else WEGKREUZ
    strasse = [WESTTOR, WESTSTRASSE] if name in IM_WESTEN else [OSTTOR, LANDSTRASSE]
    if abstand_linie(abzweig, strasse) > 1 and not any(w["linie"] == [list(kreuz), list(abzweig)] for w in k.d["wege"]):
        k.weg(f"Weg zum Abzweig {name}", "land", [kreuz, abzweig], breite=4)
    k.weg(f"Weg zu {name}", "land", [abzweig] + WEGKNICKE.get(name, []) + [umland_hof[name]], breite=4)

# Fußwege zu Weide, Feld und Wald: von der Weststraße bis ans Gatter („Anker_Tuer“ in umland_stellen.glb)
stelle_drehung = {name: drehung_zu(zugang[0] - mitte[0], zugang[1] - mitte[1]) for name, (_, mitte, zugang) in UMLANDSTELLEN.items()}
gatter_lokal = anker("umland_stellen", "Anker_Tuer")
for name, (_, mitte, zugang) in UMLANDSTELLEN.items():
    dx, dz = dreh(*gatter_lokal, stelle_drehung[name])
    k.weg(f"Fußweg {name}", "gasse", [zugang, (mitte[0] + dx, mitte[1] + dz)], breite=2)

# ── Gebäude ──────────────────────────────────────────────────────────────────
k.gebaeude("Markt", "markt", "Marktplatz", MARKT)
k.gebaeude("Rathaus", "rathaus", "Rathaus", RATHAUS)
dom_versatz = k.katalog["dom"]["versatz"]               # der Ursprung des Modells liegt 2,85 m nördlich der Mitte der Fläche
k.gebaeude("Dom", "dom", "Dom", (DOM[0] - dom_versatz[0], DOM[1] - dom_versatz[1]))

# Werkstattplätze: Am Weinmarkt arbeitet die Startwerkstatt des Spielers (Name aus `start`), an den anderen ein Rivale (`betrieb`).
for name, p in werkstatt_mitte.items():
    if name == "Weinmarkt":
        k.gebaeude("Weinmarkt", "werkstattplatz", "Weinmarkt", p)
    else:
        k.gebaeude(kennung_von(name), "werkstattplatz", name, p, betrieb=name)

# Umlandbetriebe mit echter Drehung; hinter jeder Grube ihr Felsberg (eigener Baustein, um den Ansatz am Felshang verkleinert)
for name, (p, abzweig) in UMLAND.items():
    art = name.split(" ")[0]
    kennung = kennung_von(name)
    k.gebaeude(kennung, UMLANDKATALOG[art], name, p, umland_drehung[name], betrieb=name)
    if art == "Grube":
        s = BERGGROESSE.get(name, 1)
        vx, vz = k.katalog["felsberg"]["versatz"]       # Ursprung des Modells (= Mitte der Grube) von der Mitte des Berges aus
        lx, lz = -vx * s, -vz * s + BERG_ANSATZ * (s - 1)
        wx, wz = dreh(lx, lz, umland_drehung[name])
        k.gebaeude("Berg_" + kennung, "felsberg", "Felsberg " + name, (p[0] + wx, p[1] + wz), umland_drehung[name], massstab=s)
    if art in VORKOMMEN:
        k.vorkommen(kennung, VORKOMMEN[art], p)

# Arbeitsstellen der Einwohner (Namen wie im Kern: Weide, Feld, Wald)
for name, (katalog, mitte, _) in UMLANDSTELLEN.items():
    k.gebaeude(f"Stelle{name}", katalog, name, mitte, stelle_drehung[name])

# Die vier Häuser der Kulisse (feste Modelle) – Tür zur nächsten Gasse
for n, (p, vorne) in enumerate(KULISSE, start=1):
    k.gebaeude(f"Kulisse{n}", "kulisse_buergerhaus", f"Haus der Kulisse {n}", p, drehung_von(vorne))

# Wohnhausplätze der Spielhäuser: Doppelplätze am Ring, in Konto-Reihenfolge (Spieler zuerst). Die Kennungen merkt sich der Spielstand.
doppel, ring = doppelplaetze(ANZAHL_HAEUSER)
for n, (mitte, vorne, _) in enumerate(doppel, start=1):
    k.gebaeude(f"Wohnhaus{n}", "wohnhausplatz_ring", f"Wohnhaus {n}", mitte, drehung_von(vorne), lage="mauerring")
belegt = {i for _, _, nr in doppel for i in nr}

# Einwohnerplätze: die übrigen Ringplätze, in der Reihenfolge, in der sie bebaut werden (die nächsten am Markt zuerst – dort stehen die
# Ratsherren- und Bürgerhäuser), danach die Vorstadt vor dem Osttor (die Plätze an der Landstraße zuerst). Die Kennungen zählen die
# Ringplätze rundum (Norden, Süden, Osten, Westen), wie bisher.
uebrig = [(i, mitte, vorne) for i, (mitte, vorne) in enumerate(ring) if i not in belegt]
uebrig.sort(key=lambda e: (laenge32(e[1]), e[1][0], e[1][1]))
for i, mitte, vorne in uebrig:
    k.gebaeude(f"Ring{i + 1}", "einwohnerplatz_ring", f"Ringplatz {i + 1}", mitte, drehung_von(vorne))
for n, (mitte, vorne) in enumerate(vorstadt_plaetze(), start=1):
    k.gebaeude(f"Vorstadt{n}", "einwohnerplatz_vorstadt", f"Vorstadtplatz {n}", mitte, drehung_von(vorne))


# ── Natur ────────────────────────────────────────────────────────────────────
# Der Wald war ein Ring (48 bis 98 m um (10|0)), den die Stadtansicht mit NachAussen verschoben hat, als die Stadt von ±36 auf ±50 m
# wuchs: Was außerhalb von ±36 lag, rückt 14 m nach außen, im Osten noch 20 m weiter (Platz für die Vorstadt). Die Fläche hier ist genau
# dieser verschobene Ring. Die schmalen Schneisen, die die Verschiebung dabei aufriss, gehören jetzt zum Wald.
def nach_aussen(p):
    alt, dazu, vorstadt = 36, MAUERRAND - 36, 20
    schieben = lambda w: w + dazu if w > alt else w - dazu if w < -alt else w
    return (p[0] + dazu + vorstadt if p[0] > alt else schieben(p[0]), schieben(p[1]))


def ring_bild(mitte, radius, n):
    """Ein Kreis, mit NachAussen verschoben: an jeder Sprungstelle (x oder z = ±36) beide Seiten, damit die Kante dem Sprung folgt."""
    winkel = [2 * math.pi * i / n for i in range(n)]
    for grenze in (36, -36):
        for c, wert in ((0, grenze - mitte[0]), (1, grenze - mitte[1])):
            if abs(wert) < radius:
                a = math.acos(wert / radius) if c == 0 else math.asin(wert / radius)
                winkel += [a % (2 * math.pi), (-a if c == 0 else math.pi - a) % (2 * math.pi)]
    punkte = []
    for a in sorted(set(round(a, 9) for a in winkel)):
        for e in (-1e-7, 1e-7):
            q = nach_aussen((mitte[0] + radius * math.cos(a + e), mitte[1] + radius * math.sin(a + e)))
            punkte.append((round(q[0] * 2) / 2, round(q[1] * 2) / 2))   # halbe Meter genügen
    aus = []
    for q in punkte:
        if not aus or q != aus[-1]:
            aus.append(q)
    return aus if aus[0] != aus[-1] else aus[:-1]


def halbebene(poly, x_grenze, westlich):
    """Schneidet ein Vieleck an der Linie x = x_grenze ab (Sutherland–Hodgman) und behält die West- oder Ostseite."""
    drin = (lambda p: p[0] <= x_grenze) if westlich else (lambda p: p[0] >= x_grenze)
    aus = []
    for i, p in enumerate(poly):
        q = poly[(i + 1) % len(poly)]
        if drin(p):
            aus.append(p)
        if drin(p) != drin(q):
            t = (x_grenze - p[0]) / (q[0] - p[0])
            aus.append((x_grenze, round((p[1] + t * (q[1] - p[1])) * 2) / 2))
    return aus


WALDMITTE, WALD_INNEN, WALD_AUSSEN = (10, 0), 48, 98
aussen, innen = ring_bild(WALDMITTE, WALD_AUSSEN, 64), ring_bild(WALDMITTE, WALD_INNEN, 40)
STADT_MIT_RAND = [(-56, -56), (56, -56), (56, 56), (-56, 56)]    # in der Stadt und dicht an der Mauer wächst nichts
VORSTADT = [(50, -52), (80, -52), (80, 52), (50, 52)]            # die Vorstadt vor dem Osttor bis zum Wegkreuz
LAUBGRENZE = -20                                                 # westlich davon stehen zwischen den Tannen Eichen und Buchen
# 70 Bäume wie früher: Der verschobene Ring hat nach Abzug der Aussparungen rund 29.400 m² (das Spiel zählt auf einem Raster),
# also 2,4 je 1.000 m² (mit 3,05 standen rund 90 Bäume)
WALD_DICHTE = 2.4
k.naturflaeche("Tannenwald um die Stadt", "wald", halbebene(aussen, LAUBGRENZE, westlich=False), WALD_DICHTE, 1400,
               [(1, 0, [({"tanne": 1}, 1, 1)])], aussparungen=[innen, STADT_MIT_RAND, VORSTADT])
k.naturflaeche("Mischwald im Westen", "wald", halbebene(aussen, LAUBGRENZE, westlich=True), WALD_DICHTE, 1401,
               [(1, 0, [({"tanne": 0.55, "eiche": 0.30, "buche": 0.15}, 1, 1)])], aussparungen=[innen, STADT_MIT_RAND])

# Streu im Umland: 220 Gruppen von Büschen, Steinen, Totholz und Wiesenpflanzen auf 330 × 230 m (2,9 je 1.000 m²)
STRAEUCHER = {"hasel": 1, "holunder": 1, "brombeere": 1, "ginster": 1, "wacholder": 1}
BODEN = {"farn": 1, "gras": 1, "brennnessel": 1, "pilze": 1, "wildblumen": 1, "maulwurfshuegel": 1}
STEINE = {"felsbrocken": 1, "findling": 1, "kiesel": 1, "lesesteinhaufen": 1}
TOTHOLZ = {"stumpf": 1, "totholz": 1, "reisighaufen": 1}
k.naturflaeche("Streu im Umland", "streu", [(-150, -115), (180, -115), (180, 115), (-150, 115)], 2.9, 3100, [
    (0.34, 3.5, [(STRAEUCHER, 2, 4), (BODEN, 2, 4)]),
    (0.20, 2.5, [(STEINE, 2, 4), ({"gras": 1}, 1, 3)]),
    (0.14, 2.5, [(TOTHOLZ, 1, 3), ({"farn": 1}, 1, 3), ({"pilze": 1}, 0, 2)]),
    (0.32, 4.0, [({"wildblumen": 1}, 2, 5), ({"gras": 1}, 3, 6), ({"maulwurfshuegel": 1}, 0, 1)]),
], aussparungen=[STADT_MIT_RAND, VORSTADT])

# Am Bach beim Kräutergarten: ein Bogen 23 bis 28 m westlich um den Garten (Winkel 1,9 bis 5,1 im Bogenmaß), Kopfweiden mit Schilf –
# bisher vier Gruppen, also etwa 10 je 1.000 m²
BACH, BACH_INNEN, BACH_AUSSEN, BACH_VON, BACH_BIS = UMLAND["Kräutergarten am Bach"][0], 23, 28, 1.9, 5.1
bogen = lambda radius: [(BACH[0] + radius * math.cos(BACH_VON + (BACH_BIS - BACH_VON) * i / 8),
                         BACH[1] + radius * math.sin(BACH_VON + (BACH_BIS - BACH_VON) * i / 8)) for i in range(9)]
bach_flaeche = [(round(x * 2) / 2, round(z * 2) / 2) for x, z in bogen(BACH_AUSSEN) + bogen(BACH_INNEN)[::-1]]
bach_dichte = round(4 / ((BACH_BIS - BACH_VON) / 2 * (BACH_AUSSEN ** 2 - BACH_INNEN ** 2)) * 1000, 1)
k.naturflaeche("Am Bach beim Kräutergarten", "streu", bach_flaeche, bach_dichte, 3101,
               [(1, 3, [({"kopfweide": 1}, 1, 2), ({"schilf": 1}, 4, 7), ({"gras": 1}, 1, 3)])])


# ── Start und Kamera ─────────────────────────────────────────────────────────
def haus(name, volk, geschlecht, wappen, farbe, wohnhaus, betriebe):
    return {"name": name, "volk": volk, "geschlecht": geschlecht, "wappen": dict(zip(("teilung", "farbe1", "farbe2", "figur"), wappen)),
            "farbe": farbe, "wohnhaus": wohnhaus, "betriebe": [dict(zip(("platz", "art", "produkt"), b)) for b in betriebe]}

k.start({"wohnhaus": "Wohnhaus1", "farbe": "#336bb8", "werkstatt": {"platz": "Weinmarkt", "name": "{art} am Weinmarkt"}}, [
    haus("Haus Eisenbart", "Zwerg", "Mann", ("Ohne", "Schwarz", "Gold", "Amboss"), "#b33329", "Wohnhaus2",
         [("Giesserei_Eisenbart", "Giesserei", "Rapier"), ("Grube_Eisenbart", "Mine")]),
    haus("Haus Eichwald", "Elb", "Frau", ("Bord", "Gruen", "Silber", "Baum"), "#388c47", "Wohnhaus3",
         [("Tischlerei_Eichwald", "Tischlerei"), ("Holzschlag_Eichwald", "Holzfaellerei")]),
    haus("Haus Kieselbach", "Halbling", "Mann", ("Schildhaupt", "Blau", "Silber", "Wellen"), "#d9ad2e", "Wohnhaus4",
         [("Maurerei_Kieselbach", "Maurerei"), ("Steinbruch_Kieselbach", "Steinbruch")]),
])
k.kamera((24, -6), 95)   # Stadt und Grube im Blick


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

# Probeplan: So passen die geforderten Gebäude hinein (Art, Zone, Rechteck x0, z0, x1, z1, Drehung). Nur zum Testen und Prüfen
# (der Kern liest ihn in seinen Tests); die Spieler dürfen frei setzen. Größen nach LEVELDESIGN: Werkstatt und Stadthof 16 × 14 m,
# Wohnhaus 8 × 10 m (Breite × Tiefe, die Vorderseite liegt an der Breite), jeweils mit 2 m Vorplatz.
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
k.fiktion("Die Greybox hat keine Startgrundstücke, sondern Wohnhausplätze (Doppelplätze am Ring); die Reihenfolge in `start` ist die "
          "der Konten. Heute spielen höchstens 4 Häuser (Spieler und drei Rivalen), Wohnhaus 5 bis 8 gelten erst bei mehr Konten.")


# ── Prüfungen der Greybox ────────────────────────────────────────────────────
ABSTAND_STRASSE = 0.5      # Meter zwischen Zone und Straßenrand (Köln: 3,3 m ab Straßenmitte bei 6,5 m breiten Straßen)
ABSTAND_FESTE = 2.0        # Meter zwischen Zone und Gebäude
ABSTAND_MAUER = 2.0        # Meter zwischen Zone und Mauermitte (die Mauer ist 2 m dick)
STRASSENNAH = 7.0          # Eingangsregel des Kerns: mindestens 2 Felder der Vorplatzreihe höchstens 7 m von der Straßenmitte (wie kapazitaet.py)
MIN_WERKSTAETTEN, MIN_WOHNHAEUSER, MIN_STADTHOEFE = 6, 8, 2
ABSTAND_WERKSTATT = 2.0    # Meter zwischen den Flächen zweier Werkstätten der Stadt (Platz für Zaun und Durchgang)
WERKSTATT = (16, 14)       # jede Werkstatt der Greybox: 16 m breit und 14 m tief mit 2 m Vorplatz, Tür nach Süden


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
    """Prüft Abstände und Überlappungen der Bauzonen, den Probeplan, die Wohnhaus- und Werkstattplätze. Gibt (Fehler, Hinweise,
    SVG-Ebene) zurück."""
    d, kat = karte.d, karte.katalog
    fehler, hinweise = [], []
    zonen = {z["kennung"]: z for z in d["bauzonen"]}
    mauern = [m["linie"] + ([m["linie"][0]] if m["geschlossen"] else []) for m in d["mauern"]]
    karren = [w for w in d["wege"] if w["klasse"] in KARRENSTRASSEN]
    flaechen = {g["kennung"]: ecken_von(g, kat) for g in d["gebaeude"]}
    art_von = lambda g: kat[g["katalog"]]["art"]

    # 1. Zonen überdecken keine Gebäude (Grundfläche aus dem Katalog, gedreht) und keine Wege, halten Abstand zur Mauer und liegen auf
    #    dem Raster
    for z in zonen.values():
        for p in z["flaeche"]:
            if p[0] % RASTER or p[1] % RASTER:
                fehler.append(f"Zone {z['kennung']}: Ecke {p} liegt nicht auf dem 2-m-Raster")
        for g in d["gebaeude"]:
            a = abstand_vielecke(z["flaeche"], flaechen[g["kennung"]])
            if a < ABSTAND_FESTE:
                fehler.append(f"Zone {z['kennung']}: nur {a:.1f} m zu {g['kennung']} ({g['name']}), mindestens {ABSTAND_FESTE:g} m")
        for w in d["wege"]:
            a = abstand_vielecke(z["flaeche"], w["linie"], False)
            if a < w["breite"] / 2 + ABSTAND_STRASSE:
                fehler.append(f"Zone {z['kennung']}: nur {a:.1f} m zur Mitte von {w['name']} "
                              f"(Straße {w['breite']:g} m breit, mindestens {w['breite'] / 2 + ABSTAND_STRASSE:g} m)")
        for m in mauern:
            a = abstand_vielecke(z["flaeche"], m, False)
            if a < ABSTAND_MAUER:
                fehler.append(f"Zone {z['kennung']}: nur {a:.1f} m zur Mauer, mindestens {ABSTAND_MAUER:g} m")
        # jede Zone liegt an einer Straße (Köln: höchstens 8 m zu einer Karrenstraße; hier gemessen vom Straßenrand)
        n = min(abstand_vielecke(z["flaeche"], w["linie"], False) - w["breite"] / 2 for w in karren)
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
    #    (Eingangsregel des Kerns: mindestens 2 Rasterfelder der Vorplatzreihe höchstens 7 m von der Mittellinie einer passenden Straße –
    #    Werkstatt und Stadthof Karrenstraße, Wohnhaus jede); ein Stadthof steht nur in Kern oder Mauerring
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
            fehler.append(f"Probeplan {art} in {kz} ({x0:g}, {z0:g}): nur {nah} Felder der Vorplatzreihe höchstens {STRASSENNAH:g} m "
                          f"von der Straßenmitte (mindestens 2)")
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

    # 4. Wohnhausplätze: so viele wie Häuser, die ersten vier (heute spielenden) gleich weit vom Markt (±10 %), Eingang an einem Weg
    plaetze = [g for g in d["gebaeude"] if art_von(g) == "wohnhausplatz"]
    if len(plaetze) != ANZAHL_HAEUSER:
        fehler.append(f"{len(plaetze)} Wohnhausplätze statt {ANZAHL_HAEUSER}")
    abst = [abstand(g["punkt"], MARKT) for g in plaetze[:4]]
    mittel = sum(abst) / len(abst)
    for g, a in zip(plaetze, abst):
        if abs(a - mittel) > 0.10 * mittel:
            fehler.append(f"{g['kennung']}: {a:.1f} m zum Markt, Mittel {mittel:.1f} m (±10 %)")
    for g in plaetze:
        eingang = lokal(g, 0, groesse_von(g, kat)[1] / 2)
        if min(abstand_linie(eingang, w["linie"]) - w["breite"] / 2 for w in d["wege"]) > 1.5:
            fehler.append(f"{g['kennung']}: Eingang liegt nicht an einem Weg")
    hinweise.append("Wohnhausplätze 1–4: " + ", ".join(f"{a:.1f} m" for a in abst) + f" zum Markt (Mittel {mittel:.1f} m); Plätze 5–8: "
                    + ", ".join(f"{abstand(g['punkt'], MARKT):.1f} m" for g in plaetze[4:]))

    # 5. Werkstattplätze der Stadt (16 × 14 m): zwischen zwei Werkstätten bleiben ABSTAND_WERKSTATT Meter, auf dem Haus (ohne den
    #    Vorplatz) liegt keine Mauer, und am Hof beginnt die eigene Gasse. (Überlappung mit anderen Gebäuden prüft karte.py für alle.)
    werkstaetten = [g for g in d["gebaeude"] if art_von(g) == "werkstattplatz"]
    for i, g in enumerate(werkstaetten):
        if tuple(groesse_von(g, kat)) != WERKSTATT or g["drehung"] != 0:
            fehler.append(f"Werkstatt {g['name']}: {groesse_von(g, kat)} m, Drehung {g['drehung']} statt 16 × 14 m mit Tür nach Süden")
        for h in werkstaetten[i + 1:]:
            if abstand_vielecke(flaechen[g["kennung"]], flaechen[h["kennung"]]) < ABSTAND_WERKSTATT:
                fehler.append(f"Werkstätten {g['name']} und {h['name']}: weniger als {ABSTAND_WERKSTATT:g} m Abstand")
        for m in mauern:
            if abstand_vielecke(ecken_von(g, kat, ohne_vorplatz=True), m, False) < 1:
                fehler.append(f"Werkstatt {g['name']}: steht auf der Mauer")
        hof = [round(v, 4) for v in hof_von(g, kat)]
        if not any(w["klasse"] == "gasse" and w["linie"][0] == hof for w in d["wege"]):
            fehler.append(f"Werkstatt {g['name']}: keine Gasse beginnt am Hof {hof}")

    # 6. Keine Straße läuft über eine Werkstatt und keine Gasse über ein Haus in der Stadt (Abstand zur Mitte mindestens die halbe
    #    Breite; geprüft ohne den Vorplatz). Eine Gasse, die an der Tür eines Hauses beginnt (Hof, Domstufen, Rathaus), gehört zu ihm;
    #    die Gassen enden in der Mitte des Marktplatzes.
    stadt = [g for g in d["gebaeude"] if im_stadtgebiet(g["punkt"]) and g["kennung"] != "Markt"]
    for w in d["wege"]:
        for g in stadt:
            if w["klasse"] != "gasse" and art_von(g) != "werkstattplatz":
                continue
            haus = ecken_von(g, kat, ohne_vorplatz=True)
            if abstand_vielecke(haus, [w["linie"][0]], False) < 3 and w["klasse"] == "gasse":
                continue
            a = abstand_vielecke(haus, w["linie"], False)
            if a < w["breite"] / 2:
                fehler.append(f"{w['name']} läuft über {g['kennung']} ({g['name']}): {a:.1f} m zur Mitte, {w['breite']:g} m breit")

    # 7. Wege im Umland, die über die Fläche eines Umlandbetriebs laufen (ohne Vorplatz; der eigene Weg endet am Halteplatz): nur ein
    #    Hinweis – so zeichnet die Stadtansicht sie seit jeher, die Modelle sind ohnehin größer als ihre Fläche.
    for g in d["gebaeude"]:
        if im_stadtgebiet(g["punkt"]) or art_von(g) not in ("umlandbetrieb", "umlandstelle"):
            continue
        haus = ecken_von(g, kat, ohne_vorplatz=True)
        for w in d["wege"]:
            if w["name"] in (f"Weg zu {g['name']}", f"Fußweg {g['name']}"):
                # der eigene Weg kommt von außen: alle Punkte vor dem Halteplatz (bzw. Gatter) liegen außerhalb der Fläche
                if any(im_vieleck(p, flaechen[g["kennung"]]) for p in w["linie"][:-1]):
                    fehler.append(f"{w['name']} kommt nicht von vorn an den Halteplatz von {g['kennung']}")
                continue
            if abstand_vielecke(haus, w["linie"], False) < w["breite"] / 2:
                hinweise.append(f"Hinweis: {w['name']} läuft über {g['kennung']} ({g['name']})")
    hinweise.append("Werkstattplätze der Stadt (16 × 14 m, Mitte der Fläche): " + ", ".join(
        f"{g['name']} ({g['punkt'][0]:g}|{g['punkt'][1]:g})" for g in werkstaetten))
    return fehler, hinweise, "\n".join(ebene)


if __name__ == "__main__":
    print("Greybox-Stadt:")
    k.schreiben(pruefung=kapazitaet, startzahl=0, mauer_geschlossen=True)
