"""Innenraum des Rathauses: der Ratssaal im Fachwerkgeschoss über der Markthalle (rathaus.glb), Schauplatz der sichtbaren
Ratssitzung (PR #202).

    blender -b --python tools/modelle/rathaus_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/rathaus_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/innen/rathaus_innen.glb und Bilder nach docs/modelle/ (rathaus_innen-blicke.png: das Bild jedes
Blickpunkts mit Figuren-Platzhaltern auf allen Sitzen, rathaus_innen-grundriss.png: der Saal von oben mit den Ankern).

Koordinaten wie bei den Werkstätten (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte des Saals
auf dem Boden. Ein Raum, rundum geschlossen, die Kamera steht im Raum:
    Rat     „Ratssaal“ innen x −5,0 … 5,0, y −5,5 … 5,5, 4,4 m hoch. Wände hoch getäfelt (1,75 m), darüber gekalkter Putz
            zwischen Fachwerkständern und ein gemalter Fries in Stadtrot und Ocker, bemalte Balkendecke, Dielen.
            In der Mitte der lange Ratstisch (5,2 × 1,1 m, grünes Tuch) mit sechs Lehnstühlen je Seite auf einem großen
            Teppich; hinten ein Podest (0,3 m, eine Stufe) mit dem Tisch und dem hohen Stuhl des Bürgermeisters, dahinter an
            der Rückwand das Stadtwappen zwischen zwei Wandbehängen, daneben zwei Standleuchter. Links vor dem Podest das
            Schreiberpult mit dem Ratsprotokoll, an der linken Wand ein Wandteppich (Baum mit Hirschen), an der rechten eine
            Bank für Zuhörer und eine Truhe mit den Urkunden der Stadt. Bleiglasfenster (Butzenscheiben in Bleiruten) in den
            Seitenwänden, drei rechts, zwei links; vorn die zweiflügelige Saaltür. Über dem Tisch zwei Hängeleuchter, auf dem
            Tisch drei Leuchter.

Knoten im GLB – dieselben Regeln wie in den Innenräumen der Werkstätten (Raum_…, Decke_…, Blick_…, Licht_…, Bedienung_…),
aber ohne Stufen, Einrichtung und Arbeitsplätze (das Rathaus ist kein Betrieb; Prüfung `pruefen.py <glb> --innen --sonderbau`):
    Raum_Rat                    der Saal (Raumschlüssel „Rat“), Decke_Rat seine Decke
    Blick_Rat_0                 von der Tür über den Redner den Tisch hinauf zum Bürgermeister
    Blick_Rat_L1, _L2           vorn rechts schräg auf die linke Reihe und das Schreiberpult; von rechts quer auf die linke Reihe
    Blick_Rat_R1, _R2           vorn links schräg auf die rechte Reihe; vom Podest hinter dem Bürgermeister zurück zum Redner
    Blick_Rat_O1                steil von oben über den Tisch (alle Sitze und Hände im Bild – zum Auszählen)
    Anker_Sitz_1 … 12           Sitzplätze der Ratsherren (Fußpunkt vor dem Stuhl, Blick zum Tisch), extras „bewegung“ „Sitzen“,
                                „sitzhoehe“ (Oberkante des Polsters), „platz“ (1 … 12, Rang: 1 und 2 nächst dem Bürgermeister,
                                ungerade auf seiner rechten Hand = links im Bild) und „seite“ („links“/„rechts“ von der Tür aus)
    Anker_Buergermeister        der hohe Stuhl auf dem Podest (Fußpunkt auf dem Podest, z 0,3), Blick zur Tür, „Sitzen“
    Anker_Schreiber             am Schreiberpult (Bewegung „Schreiben“, sitzhoehe wie die Hocker der Schreibstuben)
    Anker_Redner                Platz des Antragstellers vor dem Tisch, Blick zum Bürgermeister (Bewegung „Reden“)
    Bedienung_Rat_Ratsbuch      das Ratsbuch auf dem Tisch des Bürgermeisters (Klick: Tafel des Rates, das bindet das Spiel)
    Stadtwappen                 roter Schild mit weißem Kreuz im Sandsteinrahmen an der Rückwand; der Schild in „Hausfarbe“
                                (Vorgabe Stadtrot): das Spiel kann ihn wie die Hauswappen umfärben
    Wandbehang                  die zwei Behänge neben dem Wappen, ebenfalls in „Hausfarbe“ mit weißem Streifen
    Antrag                      eine versiegelte Rolle vor dem Redner auf dem Tisch, extras „zustand“ (aus; das Spiel zeigt sie,
                                solange ein Antrag zur Abstimmung steht)
    Saaltuer                    die beiden Flügel der Tür (nicht anklickbar: Rechtsklick verlässt das Gebäude)
Lichter: extras „art“ = „Fenster“ (die fünf Bleiglasfenster), „Lampe“ (Hängeleuchter) und „Glut“ (Kerzen).
Materialien: „Farben“ (Eckenfarben, Materialart als UV.y), „Hausfarbe“, „Fenster“, „Glut“, „Eisen“, „Gold“, „Silber“; die
Farbnamen sind die der anderen Innenräume (Kalkputz, Taeferholz, Fachwerk, Sandstein, Diele …), damit die Material-Bibliothek
sie mit der Gruppe der Innenräume erfasst.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import alchimist_innen as ai  # noqa: E402  (Drehkörper, Scheiben, Verkleinern)
import dieb_innen as di  # noqa: E402  (flache Teile, Kerzen)
import figuren as fg  # noqa: E402  (Völker, Schreiben)
import geldleiher_innen as gi  # noqa: E402  (Putzraum, Bleiglasfenster, Decke, Leuchter, Teppiche, Schreibpult, Hauptbuch)
import innenausbau as ia  # noqa: E402  (Wand, Prüfbericht, Bühne, Marken)
import parfumeur_innen as pi  # noqa: E402  (Lichter, Wandquader, Täfelung, freie Stücke)
import prediger_innen as pr  # noqa: E402  (Pult, Leuchter, Truhe)
import schmied_innen as si  # noqa: E402  (Türen, Anker, Blickpunkte)
import tischler_innen as ti  # noqa: E402  (Lichter, Export)
import wirt_innen as wi  # noqa: E402  (Dielen, Bank)

NAME = "rathaus_innen"

bs.FARBEN.update({
    "Hausfarbe": bs.FARBEN["Stadtrot"],        # Stadtwappen und Wandbehänge: Vorgabe Stadtrot, das Spiel kann umfärben
})

# ── Maße ─────────────────────────────────────────────────────────────────────
WAND = 0.5
X0, X1, Y0, Y1 = -5.0, 5.0, -5.5, 5.5                               # Ratssaal innen
H = 4.4
TAEFEL = 1.75                                                       # Höhe der Täfelung
FENSTER_V, FENSTER_B, FENSTER_H = 1.9, 1.0, 1.95                    # Bleiglasfenster (Rundbogen) in den Seitenwänden
FENSTER_RECHTS, FENSTER_LINKS = (-3.0, 0.0, 3.0), (-3.0, 0.0)
TUER_B, TUER_H = 1.6, 2.8                                           # zweiflügelige Saaltür vorn in der Mitte
STAENDER_RECHTS, STAENDER_LINKS = (-4.4, -1.5, 1.5, 4.4), (-4.4, -1.5, 0.95)  # Fachwerkständer über der Täfelung (Welt-y)
STAENDER_HINTEN, STAENDER_VORNE = (-4.0, -2.5, 2.5, 4.0), (-3.6, -1.6, 1.6, 3.6)

TISCH_L, TISCH_B, TISCH_H = 5.2, 1.1, 0.78                         # Ratstisch entlang y, Mitte im Ursprung
STUHL = 0.45                                                        # Oberkante der Polster der Lehnstühle
SITZ_Y = tuple(2.15 - 0.86 * k for k in range(6))                  # Sitze je Seite, vom Bürgermeister zur Tür
SITZ_X = TISCH_B / 2 + 0.24                                         # Fußpunkt neben der Tischkante (wie die Gäste beim Wirt)
PODEST_Y, PODEST_H, PODEST_X = 3.6, 0.3, 2.3                        # Podest von y 3,6 bis zur Rückwand, x ±2,3
STUFE_Y = 3.35                                                      # Stufe vor dem Podest (0,15 m)
BM_TISCH = Vector((0.0, 3.95, PODEST_H))                           # Tisch des Bürgermeisters (Mitte, auf dem Podest)
BM_TISCH_L, BM_TISCH_B = 1.7, 0.5
BM = Vector((0.0, 4.47, PODEST_H))                                 # Fußpunkt des Bürgermeisters, Blick −y
THRON = 0.46                                                        # Oberkante seines Polsters über dem Podest
SCHREIBER = Vector((-2.85, 2.9, 0.0))                              # Schreiberpult, Blick +x zum Tisch
REDNER = Vector((0.0, -3.45, 0.0))                                 # der Antragsteller, Blick +y
WAPPEN_V, WAPPEN_G = 3.15, 1.2                                      # Stadtwappen: Mitte über dem Podest, Größe
BEHANG_X, BEHANG_B = 1.45, 0.7                                      # Wandbehänge neben dem Wappen
ZUHOERER = ((4.62, -4.6), (4.62, -1.9))                             # Bank an der rechten Wand
TRUHE_Y = 1.5                                                       # Truhe der Urkunden an der rechten Wand
WANDTEPPICH_Y = (1.5, 4.4)                                          # an der linken Wand, hinter dem Schreiber

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Rat_0": ((0.0, -5.15, 3.0), (0.0, 3.6, 1.1)),             # von der Tür über den Redner zum Bürgermeister
    "Blick_Rat_L1": ((3.9, -4.9, 2.6), (-2.6, 3.1, 0.9)),            # linke Reihe, Schreiberpult, Wandteppich
    "Blick_Rat_L2": ((4.55, 0.4, 2.45), (-3.0, -0.9, 0.9)),          # quer auf die linke Reihe, Fenster links
    "Blick_Rat_R1": ((-3.9, -4.9, 2.6), (2.6, 3.1, 0.9)),            # rechte Reihe, Truhe, Fenster rechts
    "Blick_Rat_R2": ((0.95, 5.25, 3.3), (0.0, -3.8, 0.9)),           # vom Podest zurück: der Redner von vorn, die Tür
    "Blick_Rat_O1": ((2.3, -3.2, 4.1), (-0.3, 0.6, 0.4)),            # steil über den Tisch: alle Sitze
}

Oe = si.Oe
OEFFNUNGEN = {
    "vorne": [Oe(0.0, 0.0, TUER_B, TUER_H)],
    "rechts": [Oe(y, FENSTER_V, FENSTER_B, FENSTER_H) for y in FENSTER_RECHTS],
    "hinten": [],
    "links": [Oe(y, FENSTER_V, FENSTER_B, FENSTER_H) for y in FENSTER_LINKS],
}

ecken = ia.ecken
drehkoerper, scheibe = ai.drehkoerper, ai.scheibe
licht, wq = pi.licht, pi.wq


# ─────────────────────────────────────────────────────────────────────────────
#  Anker
# ─────────────────────────────────────────────────────────────────────────────

def sitzplatz(name, punkt, blick, bewegung, sitz=None, **extras):
    """Anker_<…>: Fußpunkt (z = Boden darunter) mit Blick `blick`, extras „bewegung“, bei sitzenden „sitzhoehe“ (Oberkante des
    Sitzes über dem Fußpunkt) und weitere `extras`."""
    o = bs.anker(name, (punkt[0], punkt[1], punkt[2] if len(punkt) > 2 else 0.0), si.drehung_von(blick))
    o["bewegung"] = bewegung
    if sitz is not None:
        o["sitzhoehe"] = sitz
    for k, v in extras.items():
        o[k] = v
    return o


# ─────────────────────────────────────────────────────────────────────────────
#  Hülle: Wände, Fenster, Täfelung, Fachwerk, Fries, Decke, Boden
# ─────────────────────────────────────────────────────────────────────────────

def huelle(netz, decke, st, anker):
    """Gekalkte Wände (geldleiher_innen.putzraum) mit Sandsteingewände um die Saaltür, Bleiglasfenster mit Butzenscheiben,
    Täfelung bis 1,75 m, darüber Fachwerkständer, ein Fries in Stadtrot zwischen Ockerlinien mit Rosetten, bemalte Balkendecke,
    Dielen."""
    waende = gi.putzraum(netz, st, X0, X1, Y0, Y1, H, OEFFNUNGEN, eigene_laibung=("rechts", "links"))
    for seite in ("rechts", "links"):
        for o in OEFFNUNGEN[seite]:
            gi.rundbogenfenster(netz, st, anker, waende[seite], o, butzen=True)
    kanten = ecken(X0, X1, Y0, Y1)
    for seite, w in waende.items():
        pi.taefelung(netz, st, *kanten[seite], OEFFNUNGEN[seite], frei=gi.tuer_frei(w), hoehe=TAEFEL)
    staender = {"rechts": STAENDER_RECHTS, "links": STAENDER_LINKS, "hinten": STAENDER_HINTEN, "vorne": STAENDER_VORNE}
    for seite, w in waende.items():                                                     # Fachwerkständer und Rähm
        for s in staender[seite]:
            u = pr.u_von(w, s)
            wq(netz, st, w, u - 0.11, u + 0.11, TAEFEL, H - 0.52, 0.0, 0.05, "Fachwerk", ohne=("-y", "-z"))
        for u0, u1 in pi.freie_stuecke(w, H - 0.52, H - 0.44, rand=0.0):
            wq(netz, st, w, u0, u1, H - 0.52, H - 0.44, 0.0, 0.05, "Fachwerk", ohne=("-y", "+x", "-x"))
    for seite, w in waende.items():                                                     # Fries über dem Rähm
        for v0, v1, dw, stoff in ((H - 0.42, H - 0.39, 0.004, "Ocker"), (H - 0.39, H - 0.13, 0.003, "Deckenrot"),
                                  (H - 0.13, H - 0.10, 0.004, "Ocker")):
            for u0, u1 in pi.freie_stuecke(w, v0, v1, rand=0.04):
                netz.vieleck_nach([w.p(u0, v0, dw), w.p(u1, v0, dw), w.p(u1, v1, dw), w.p(u0, v1, dw)], w.n, st[stoff])
        vm = H - 0.26
        for u0, u1 in pi.freie_stuecke(w, H - 0.39, H - 0.13, rand=0.04):
            n = max(1, round((u1 - u0) / 0.5))
            for k in range(n):
                um = u0 + (u1 - u0) * (k + 0.5) / n
                netz.vieleck_nach([w.p(um + 0.08 * math.cos(2 * math.pi * t / 8), vm + 0.08 * math.sin(2 * math.pi * t / 8), 0.005)
                                   for t in range(8)], w.n, st["Ocker"])
                netz.vieleck_nach([w.p(um + 0.03 * math.cos(2 * math.pi * t / 6), vm + 0.03 * math.sin(2 * math.pi * t / 6), 0.006)
                                   for t in range(6)], w.n, st["Stadtweiss"])
    gi.bemalte_decke(decke, st, X0, X1, Y0, Y1, H, abdeckung=WAND, saat=11)
    wi.dielen(netz, st, X0, X1, Y0, Y1, 0.0, saat=17)
    return waende


def saaltuer(netz, st):
    """Die zweiflügelige Saaltür vorn (Eiche mit Eisenbändern, zwei Ringe)."""
    for s in (-1, 1):
        si.bretttuer(netz, st, si.rahmen(s * TUER_B / 4, Y0, 0), TUER_B / 2, TUER_H, stoff="Taeferholz dunkel", ring=False)
        si.q(netz, st, si.rahmen(s * 0.1, Y0, 0), (0, -0.09, 1.3), (0.05, 0.05, 0.16), "Eisen", ohne=("-y",))


def podest(netz, st):
    """Podest aus Eichenbohlen von y 3,6 bis zur Täfelung der Rückwand, vorn eine Stufe über die ganze Breite."""
    hinten = Y1 - 0.035                                                                   # vor der Täfelung
    netz.quader((0, (PODEST_Y + hinten) / 2, PODEST_H / 2), (2 * PODEST_X, hinten - PODEST_Y, PODEST_H), st["Taeferholz dunkel"],
                ohne=("-z", "+z"))
    for k in range(9):                                                                      # Bohlen oben, entlang x
        y0 = PODEST_Y + (hinten - PODEST_Y) * k / 9
        y1 = PODEST_Y + (hinten - PODEST_Y) * (k + 1) / 9
        ia.waagrecht(netz, (-PODEST_X, PODEST_X, y0 + 0.004, y1 - 0.004), PODEST_H, True, st[ia.DIELEN[k % 4]])
    ia.waagrecht(netz, (-PODEST_X, PODEST_X, PODEST_Y, hinten), PODEST_H - 0.008, True, st["Dielenfuge"])
    netz.quader((0, (STUFE_Y + PODEST_Y) / 2, PODEST_H / 4), (2 * PODEST_X - 0.4, PODEST_Y - STUFE_Y, PODEST_H / 2), st["Taeferholz"],
                ohne=("-z", "+y"))
    netz.quader((0, PODEST_Y + 0.02, PODEST_H - 0.015), (2 * PODEST_X + 0.04, 0.06, 0.04), st["Taeferholz dunkel"], ohne=("-z",))


# ─────────────────────────────────────────────────────────────────────────────
#  Möbel
# ─────────────────────────────────────────────────────────────────────────────

def lehnstuhl(netz, st, fuss, blick):
    """Lehnstuhl eines Ratsherrn: Gestell aus dunkler Eiche, Sitz und Rückenpolster in Leder mit Messingnägeln, Lehne mit
    geschnitztem Abschluss. `fuss`: Fußpunkt der Figur, der Stuhl steht 5 cm dahinter."""
    vor = Vector((blick[0], blick[1], 0)).normalized()
    rechts = Vector((vor.y, -vor.x, 0))
    M = Matrix((rechts, vor, Vector((0, 0, 1)))).transposed().to_4x4()
    M.translation = Vector((fuss[0], fuss[1], 0.0)) - vor * 0.05
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader((sx * 0.21, sy * 0.19, (STUHL - 0.06) / 2), (0.05, 0.05, STUHL - 0.06), st["Thronholz"], M)
    netz.quader((0, 0, 0.12), (0.42, 0.03, 0.04), st["Thronholz"], M)                     # Steg
    netz.quader((0, 0, STUHL - 0.05), (0.5, 0.46, 0.05), st["Thronholz"], M)
    netz.quader((0, 0.01, STUHL - 0.015), (0.44, 0.4, 0.03), st["Leder"], M)
    netz.quader((0, -0.215, STUHL + 0.45), (0.5, 0.05, 0.9), st["Thronholz"], M, ohne=("-z",))   # Lehne
    netz.quader((0, -0.185, STUHL + 0.42), (0.4, 0.012, 0.5), st["Leder"], M)
    for k in range(5):
        netz.quader((-0.16 + 0.08 * k, -0.178, STUHL + 0.68), (0.014, 0.004, 0.014), st["Messing"], M)
    netz.prisma([(-0.24, 0.0), (-0.12, 0.08), (0.0, 0.12), (0.12, 0.08), (0.24, 0.0)], -0.025, 0.025, st["Thronholz"],
                matrix=M @ Matrix.Translation((0, -0.215, STUHL + 0.9)) @ Matrix.Rotation(math.radians(90), 4, "Z"))


def ratstisch(netz, st, anker):
    """Der lange Ratstisch: dicke Eichenplatte auf sechs gedrechselten Beinen mit Zarge, darüber das grüne Tuch, das an allen
    Seiten herabhängt; vor jedem Platz ein Blatt, Tintenfässer, drei Leuchter auf der Mittellinie."""
    L, B, Hh = TISCH_L, TISCH_B, TISCH_H
    netz.quader((0, 0, Hh - 0.035), (B, L, 0.07), st["Thronholz"], ohne=("+z",))
    netz.quader((0, 0, Hh - 0.12), (B - 0.12, L - 0.12, 0.1), st["Thronholz"], ohne=("+z", "-z"))   # Zarge
    for sx in (-1, 1):
        for y in (-L / 2 + 0.2, 0.0, L / 2 - 0.2):
            drehkoerper(netz, st, (sx * (B / 2 - 0.12), y, 0.0), [(0.0, 0.05), (0.08, 0.045), (0.12, 0.03), (0.3, 0.05), (0.42, 0.03),
                                                                (0.55, 0.045), (Hh - 0.17, 0.035), (Hh - 0.07, 0.04)], "Thronholz", n=8)
    z = Hh + 0.003                                                                          # das Tuch
    netz.quader((0, 0, z), (B + 0.02, L + 0.02, 0.006), st["Tuch"], ohne=("-z",))
    for sx in (-1, 1):
        netz.quader((sx * (B / 2 + 0.013), 0, Hh - 0.1), (0.006, L + 0.02, 0.2), st["Tuch"], ohne=("-z", "+z"))
        netz.quader((sx * (B / 2 + 0.015), 0, Hh - 0.185), (0.006, L + 0.02, 0.03), st["Tuch dunkel"], ohne=("+z",))
    for sy in (-1, 1):
        netz.quader((0, sy * (L / 2 + 0.013), Hh - 0.1), (B + 0.02, 0.006, 0.2), st["Tuch"], ohne=("-z", "+z"))
        netz.quader((0, sy * (L / 2 + 0.015), Hh - 0.185), (B + 0.02, 0.006, 0.03), st["Tuch dunkel"], ohne=("+z",))
    oben = Hh + 0.006
    zufall = random.Random(5)
    for y in SITZ_Y:                                                                        # vor jedem Platz ein Blatt
        for sx in (-1, 1):
            M = Matrix.Translation((sx * (B / 2 - 0.2), y, oben)) @ Matrix.Rotation(zufall.uniform(-0.2, 0.2), 4, "Z")
            netz.quader((0, 0, 0.001), (0.2, 0.27, 0.002), st["Papier"], M)
            for j in range(4):
                netz.quader((0, 0.08 - 0.035 * j, 0.0025), (0.14 - 0.03 * (j % 2), 0.004, 0.001), st["Tinte"], M)
    for y in (SITZ_Y[1] - 0.43, SITZ_Y[4] + 0.43):                                         # Tintenfässer mit Federn
        bs.achteck(netz, st, Vector((0.0, y, oben)), 0.03, 0.0, 0.05, "Kohle", n=6)
        netz.stab(Vector((0.0, y, oben + 0.045)), Vector((0.05, y - 0.06, oben + 0.22)), 0.012, 0.03, st["Federweiss"])
    for y in (-1.7, 0.0, 1.7):                                                              # Leuchter
        pr.leuchter(netz, st, Vector((0.0, y, oben)), h=0.24)
        anker.append(licht(Vector((0.0, y, oben + 0.75)), (0, 0, 1), "Glut"))


def antrag(netz, st):
    """Die versiegelte Rolle des Antrags vor dem Redner auf dem Tisch (Knoten „Antrag“, aus)."""
    y = -TISCH_L / 2 + 0.25
    oben = TISCH_H + 0.006
    netz.stab(Vector((-0.17, y, oben + 0.03)), Vector((0.17, y, oben + 0.03)), 0.06, 0.06, st["Papier"], quer=(0, 0, 1))
    for x in (-0.17, 0.17):
        scheibe(netz, st, (x, y, 0), 0.03, oben + 0.03, "Papier", n=8)
    netz.quader((0.0, y, oben + 0.03), (0.03, 0.065, 0.065), st["Stadtrot"])                # Band
    bs.achteck(netz, st, Vector((0.0, y - 0.04, oben + 0.0)), 0.03, 0.0, 0.012, "Siegelwachs", n=8)


def thron(netz, st):
    """Der hohe Stuhl des Bürgermeisters: breiter Sitz mit rotem Polster, Armlehnen auf gedrechselten Säulchen, eine hohe
    Rückenlehne mit Füllung und einem Giebel mit drei Knäufen."""
    f = BM + Vector((0, 0.08, 0))
    s = THRON
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader(f + Vector((sx * 0.27, sy * 0.22, (s - 0.06) / 2)), (0.07, 0.07, s - 0.06), st["Thronholz"])
    netz.quader(f + Vector((0, 0, s - 0.05)), (0.64, 0.54, 0.06), st["Thronholz"])
    netz.quader(f + Vector((0, -0.02, s - 0.005)), (0.56, 0.46, 0.04), st["Polster"])
    for sx in (-1, 1):                                                                      # Armlehnen
        netz.quader(f + Vector((sx * 0.3, -0.02, s + 0.24)), (0.07, 0.52, 0.05), st["Thronholz"])
        drehkoerper(netz, st, f + Vector((sx * 0.3, -0.22, s - 0.02)), [(0.0, 0.03), (0.06, 0.035), (0.13, 0.022), (0.24, 0.03)],
                    "Thronholz", n=6)
    hoch = 1.9
    netz.quader(f + Vector((0, 0.26, s + hoch / 2 - 0.02)), (0.68, 0.07, hoch), st["Thronholz"])
    netz.quader(f + Vector((0, 0.22, s + 0.62)), (0.52, 0.012, 0.95), st["Polster"])       # Rückenpolster
    for k in range(7):
        netz.quader(f + Vector((-0.24 + 0.08 * k, 0.212, s + 1.1)), (0.016, 0.004, 0.016), st["Messing"])
    for x in (-0.22, 0.22):                                                                 # Füllung oben
        netz.quader(f + Vector((x, 0.218, s + 1.48)), (0.18, 0.014, 0.4), st["Taeferholz"])
    netz.prisma([(-0.36, 0.0), (0.0, 0.28), (0.36, 0.0)], -0.04, 0.04, st["Thronholz"],
                matrix=Matrix.Translation(f + Vector((0, 0.26, s + hoch - 0.02))) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    for x, z in ((-0.34, 0.0), (0.34, 0.0), (0.0, 0.28)):
        drehkoerper(netz, st, f + Vector((x, 0.26, s + hoch - 0.02 + z)), [(0.0, 0.03), (0.05, 0.045), (0.1, 0.0)], "Gold", n=8)


def bm_tisch(netz, st, buch, glocke):
    """Der Tisch des Bürgermeisters auf dem Podest: Kasten mit Füllungen zur Tür hin, rotes Tuch, darauf das Ratsbuch
    (Bedienung_Rat_Ratsbuch), die Ratsglocke, Siegel und Wachs."""
    m, L, B, Hh = BM_TISCH, BM_TISCH_L, BM_TISCH_B, TISCH_H
    netz.quader(m + Vector((0, 0, Hh - 0.03)), (L, B, 0.06), st["Thronholz"])
    netz.quader(m + Vector((0, -B / 2 + 0.04, (Hh - 0.06) / 2)), (L - 0.04, 0.04, Hh - 0.06), st["Taeferholz"], ohne=("-z",))
    for k in range(4):
        x = -L / 2 + 0.02 + (L - 0.04) * k / 3
        netz.quader(m + Vector((x, -B / 2 + 0.012, (Hh - 0.06) / 2)), (0.08, 0.012, Hh - 0.06), st["Taeferholz dunkel"])
    for z in (0.08, Hh - 0.12):
        netz.quader(m + Vector((0, -B / 2 + 0.01, z)), (L - 0.04, 0.012, 0.08), st["Taeferholz dunkel"])
    for sx in (-1, 1):
        netz.quader(m + Vector((sx * (L / 2 - 0.04), 0.01, (Hh - 0.06) / 2)), (0.06, B - 0.06, Hh - 0.06), st["Taeferholz dunkel"], ohne=("-z",))
    oben = m.z + Hh
    netz.quader(m + Vector((0, 0, Hh + 0.003)), (L - 0.06, B - 0.06, 0.006), st["Teppich rot"])
    netz.quader(m + Vector((0, -B / 2 - 0.004, Hh - 0.07)), (L - 0.06, 0.006, 0.15), st["Teppich rot"])
    netz.quader(m + Vector((0, -B / 2 - 0.006, Hh - 0.14)), (L - 0.06, 0.006, 0.025), st["Teppich gold"])
    oben += 0.006
    gi.hauptbuch(buch, st, Matrix.Translation((m.x, m.y + 0.02, oben)), b=0.52, t=0.36, farbe="Buchrot")
    g = Vector((m.x + 0.55, m.y + 0.05, oben))                                              # Ratsglocke
    drehkoerper(glocke, st, g, [(0.0, 0.06), (0.02, 0.058), (0.08, 0.045), (0.11, 0.03), (0.12, 0.015), (0.2, 0.012), (0.23, 0.025)],
                "Messing", n=10)
    bs.achteck(netz, st, Vector((m.x - 0.6, m.y - 0.02, oben)), 0.022, 0.0, 0.08, "Messing", n=6)      # Stadtsiegel
    bs.achteck(netz, st, Vector((m.x - 0.52, m.y - 0.06, oben)), 0.014, 0.0, 0.03, "Siegelwachs", n=6)
    M = Matrix.Translation((m.x - 0.42, m.y + 0.08, oben)) @ Matrix.Rotation(-0.25, 4, "Z")
    netz.quader((0, 0, 0.001), (0.2, 0.27, 0.002), st["Papier"], M)


def stadtwappen(netz, st, w):
    """Stadtwappen an der Rückwand (Wand w): Sandsteinrahmen, roter Schild („Hausfarbe“, Vorgabe Stadtrot) mit weißem Kreuz – wie
    rathaus.stadtwappen außen am Ratssaal; darunter ein Konsolstein."""
    u = w.laenge / 2
    M = Matrix.Translation(w.p(u, 0.0)) @ Matrix((-w.d, -w.n, Vector((0, 0, 1)))).transposed().to_4x4()  # Wandsystem: −y in den Raum
    g = WAPPEN_G
    bs.platte(netz, M, [(u_ * 1.16 * g, WAPPEN_V + v * 1.12 * g) for u_, v in bs.SCHILD], -0.06, -0.002, st["Sandstein"])
    bs.platte(netz, M, [(u_ * g, WAPPEN_V + v * g) for u_, v in bs.SCHILD], -0.09, -0.06, st["Hausfarbe"])
    netz.quader((0, -0.1, WAPPEN_V), (0.12 * g, 0.018, 0.7 * g), st["Stadtweiss"], M)
    netz.quader((0, -0.1, WAPPEN_V + 0.08 * g), (0.6 * g, 0.022, 0.12 * g), st["Stadtweiss"], M)
    unten = WAPPEN_V - 0.44 * 1.12 * g
    netz.quader((0, -0.08, unten - 0.06), (0.36, 0.16, 0.12), st["Sandstein"], M, ohne=("+y",))


def wandbehang(netz, st, w):
    """Zwei Behänge neben dem Wappen: an einer Stange mit Knäufen, Tuch in „Hausfarbe“ mit einem weißen Streifen in der Mitte und
    einer goldenen Borte, unten in zwei Zipfeln mit Quasten."""
    for s in (-1, 1):
        u = w.laenge / 2 + s * BEHANG_X
        v0, v1 = TAEFEL + 0.15, H - 0.6
        b = BEHANG_B / 2
        netz.stab(w.p(u - b - 0.08, v1 + 0.05, 0.06), w.p(u + b + 0.08, v1 + 0.05, 0.06), 0.035, 0.035, st["Thronholz"], quer=w.n)
        for uu in (u - b - 0.08, u + b + 0.08):
            drehkoerper(netz, st, w.p(uu, v1 + 0.02, 0.06), [(0.0, 0.0), (0.03, 0.035), (0.06, 0.0)], "Gold", n=6)
        umriss = [(u - b, v1), (u + b, v1), (u + b, v0 + 0.2), (u + b / 2, v0), (u, v0 + 0.2), (u - b / 2, v0), (u - b, v0 + 0.2)]
        netz.vieleck_nach([w.p(a, c, 0.05) for a, c in umriss], w.n, st["Hausfarbe"])
        streifen = [(u - 0.08, v1 - 0.06), (u + 0.08, v1 - 0.06), (u + 0.08, v0 + 0.14), (u - 0.08, v0 + 0.14)]
        netz.vieleck_nach([w.p(a, c, 0.052) for a, c in streifen], w.n, st["Stadtweiss"])
        for a0, a1, c0, c1 in ((u - b, u + b, v1 - 0.06, v1 - 0.03), (u - b + 0.03, u - b + 0.06, v0 + 0.2, v1 - 0.06),
                               (u + b - 0.06, u + b - 0.03, v0 + 0.2, v1 - 0.06)):
            netz.vieleck_nach([w.p(a0, c0, 0.053), w.p(a1, c0, 0.053), w.p(a1, c1, 0.053), w.p(a0, c1, 0.053)], w.n, st["Teppich gold"])
        for uu in (u - b / 2, u + b / 2):                                                  # Quasten
            drehkoerper(netz, st, w.p(uu, v0 - 0.12, 0.05), [(0.0, 0.0), (0.02, 0.03), (0.1, 0.02), (0.12, 0.0)], "Teppich gold", n=6)


def schreiberpult(netz, st, anker):
    """Das Pult des Stadtschreibers mit dem Ratsprotokoll (geldleiher_innen.schreibpult) und eine Kerze auf einem Wandarm."""
    gi.schreibpult(netz, st, pr.Pult(SCHREIBER, (1, 0)))
    leuchter = SCHREIBER + Vector((0.15, 0.62, 0))                                          # Ständer mit Kerze neben dem Pult
    drehkoerper(netz, st, leuchter, [(0.0, 0.14), (0.04, 0.1), (0.06, 0.025), (1.1, 0.02), (1.12, 0.06), (1.14, 0.06)], "Eisen", n=8)
    di.kerze(netz, st, leuchter + Vector((0, 0, 1.14)), 0.16, anker=anker)


def zuhoerer_und_truhe(netz, st):
    """An der rechten Wand: eine Bank für Zuhörer und die Truhe mit den Urkunden der Stadt (Eisenbänder)."""
    wi.bank(netz, st, (ZUHOERER[0][0], ZUHOERER[0][1], 0), (ZUHOERER[1][0], ZUHOERER[1][1], 0))
    pr.truhe(netz, st, si.rahmen(X1, TRUHE_Y, 90), eisen=True)


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen
# ─────────────────────────────────────────────────────────────────────────────

def bauen():
    import bpy
    bs.neue_szene()
    ia.RUSS_QUELLEN.clear()
    ti._lichter[0] = 0
    st = bs.Stoffe()
    raum = bs.Netz("Raum_Rat")
    decke = bs.Netz("Decke_Rat", alle=True)
    teile = {n: bs.Netz(n) for n in ("Stadtwappen", "Wandbehang", "Saaltuer", "Antrag", "Bedienung_Rat_Ratsbuch")}
    anker = []

    waende = huelle(raum, decke, st, anker)
    saaltuer(teile["Saaltuer"], st)
    podest(raum, st)
    gi.teppich(raum, st, Vector((0.0, -0.2, 0.0)), 3.3, 6.4)
    ratstisch(raum, st, anker)
    antrag(teile["Antrag"], st)
    plaetze = []
    for k, y in enumerate(SITZ_Y):                                                          # Rang: erst nahe am Bürgermeister
        for j, sx in enumerate((-1, 1)):                                                    # seine Rechte = links im Bild
            fuss = (sx * SITZ_X, y)
            lehnstuhl(raum, st, fuss, (-sx, 0))
            nummer = 2 * k + j + 1
            plaetze.append(sitzplatz(f"Anker_Sitz_{nummer}", fuss, (-sx, 0), "Sitzen", STUHL, platz=nummer,
                                     seite="links" if sx < 0 else "rechts"))
    thron(raum, st)
    bm_tisch(raum, st, teile["Bedienung_Rat_Ratsbuch"], raum)
    plaetze.append(sitzplatz("Anker_Buergermeister", BM, (0, -1), "Sitzen", THRON))
    stadtwappen(teile["Stadtwappen"], st, waende["hinten"])
    wandbehang(teile["Wandbehang"], st, waende["hinten"])
    for sx in (-1, 1):
        gi.stehleuchter(raum, st, anker, (sx * 1.95, 4.85, PODEST_H))
    schreiberpult(raum, st, anker)
    plaetze.append(sitzplatz("Anker_Schreiber", SCHREIBER, (1, 0), "Schreiben", gi.SITZ))
    plaetze.append(sitzplatz("Anker_Redner", REDNER, (0, 1), "Reden"))
    w = waende["links"]
    gi.wandteppich(raum, st, w, pr.u_von(w, WANDTEPPICH_Y[1]), pr.u_von(w, WANDTEPPICH_Y[0]), TAEFEL + 0.2, TAEFEL + 1.75)
    zuhoerer_und_truhe(raum, st)
    for y in (-1.3, 1.3):
        gi.haengeleuchter(decke, st, anker, (0.0, y), 2.75, H)

    # ── Blickpunkte ──────────────────────────────────────────────────────────
    for name, (auge, ziel) in BLICKE.items():
        anker.append(si.blick(name, auge, ziel))

    # ── Objekte, Hierarchie ───────────────────────────────────────────────────
    r = raum.objekt()
    kinder = [decke.objekt()] + plaetze + anker
    for name, netz in teile.items():
        o = netz.objekt()
        if name == "Antrag":
            bs.zustand(o)
        kinder.append(o)
    for o in kinder:
        o.parent = r
        o.matrix_parent_inverse = Matrix.Identity(4)
    bpy.context.view_layer.update()
    return [r] + kinder


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

MARKEN = {"Sitz": (0.95, 0.25, 0.2), "Buergermeister": (0.95, 0.8, 0.15), "Schreiber": (0.2, 0.45, 1.0), "Redner": (0.3, 0.85, 0.3)}


def platz_marken(z):
    """Marken für die Anker im Grundriss (Pfeil = Blickrichtung): Sitze rot, Bürgermeister gelb, Schreiber blau, Redner grün."""
    import bpy
    stoffe = {k: bs._hilfsstoff("_Marke_" + k, f) for k, f in MARKEN.items()}
    liste = []
    for o in list(bpy.context.scene.objects):
        if o.type != "EMPTY" or not o.name.startswith("Anker_"):
            continue
        art = o.name.split("_")[1]
        netz = bs.Netz("_Marke_" + o.name)
        t = o.matrix_world
        p = t.translation.copy()
        p.z = z
        vor = t.to_3x3() @ Vector((0, 1, 0))
        quer = Vector((-vor.y, vor.x, 0))
        netz.vieleck_nach([p - quer * 0.16 - vor * 0.12, p + vor * 0.34, p + quer * 0.16 - vor * 0.12], (0, 0, 1), stoffe[art])
        liste.append(netz.objekt())
    return liste


def figuren_platzhalter():
    """Für die Bilder der Blickpunkte: auf jedem Sitz eine einfache Figur (Kapsel in Ratsrot, Kopf), damit man sieht, ob Tisch,
    Stühle und Kamera zu sitzenden Ratsherren passen; Redner stehend, Bürgermeister in Gold. Nicht im GLB."""
    import bpy
    gewand = {"Sitz": bs._hilfsstoff("_Gewand", (0.45, 0.12, 0.12)), "Buergermeister": bs._hilfsstoff("_Gewand_BM", (0.75, 0.6, 0.2)),
              "Schreiber": bs._hilfsstoff("_Gewand_S", (0.2, 0.22, 0.3)), "Redner": bs._hilfsstoff("_Gewand_R", (0.25, 0.35, 0.22))}
    haut = bs._hilfsstoff("_Haut", (0.8, 0.62, 0.5))
    liste = []
    mensch = fg.VOELKER["mensch"]
    for o in list(bpy.context.scene.objects):
        if o.type != "EMPTY" or not o.name.startswith("Anker_"):
            continue
        art = o.name.split("_")[1]
        t = o.matrix_world
        p = t.translation.copy()
        vor = (t.to_3x3() @ Vector((0, 1, 0))).normalized()
        netz = bs.Netz("_Figur_" + o.name)
        if "sitzhoehe" in o:
            huefte = p + Vector((0, 0, o["sitzhoehe"] + 0.06)) - vor * 0.02
            netz.stab(huefte, huefte + vor * 0.42, 0.3, 0.14, gewand[art], quer=(0, 0, 1))          # Oberschenkel
            schulter = huefte + Vector((0, 0, 0.52))
        else:
            netz.stab(p + Vector((0, 0, 0.0)), p + Vector((0, 0, mensch["huefte"])), 0.3, 0.2, gewand[art], quer=vor)
            huefte = p + Vector((0, 0, mensch["huefte"]))
            schulter = huefte + Vector((0, 0, 0.5))
        netz.stab(huefte, schulter, 0.38, 0.24, gewand[art], quer=vor)
        drehkoerper(netz, None, schulter + Vector((0, 0, 0.04)), [(0.0, 0.0), (0.05, 0.09), (0.14, 0.11), (0.24, 0.08), (0.28, 0.0)], haut, n=8)
        liste.append(netz.objekt())
    return liste


def vorschau(schnell):
    """Bilder nach docs/modelle/: rathaus_innen-blicke.png (jeder Blickpunkt, mit Figuren-Platzhaltern auf allen Plätzen und
    dem Antrag auf dem Tisch) und rathaus_innen-grundriss.png (von oben ohne Decke, mit den Ankern: Sitze rot, Bürgermeister
    gelb, Schreiber blau, Redner grün; Blickpunkte gelb)."""
    import bpy
    szene, kam = ia.buehne(schnell, lampe=80, glut=160, fuell=[(0, -2.0, H - 0.6, 130, 1), (0, 2.5, H - 0.6, 130, 1)])
    szene.render.resolution_x, szene.render.resolution_y = 640, 360
    szene.render.resolution_percentage = 50 if schnell else 100
    figuren = figuren_platzhalter()
    ia.zeigen(szene, 3, True)
    bilder = []
    for blick in BLICKE:
        o = bpy.data.objects[blick]
        kam.matrix_world = o.matrix_world @ Matrix.Rotation(math.radians(90), 4, "X")
        bilder.append(bs.rendern_einzeln(f"_{NAME}-{blick}", blick))
    print(bs.zusammensetzen(NAME + "-blicke", bilder, spalten=2))
    for o in figuren:
        bpy.data.objects.remove(o)
    kam.data.type = "ORTHO"
    kam.data.sensor_fit = "AUTO"
    sonne = bpy.data.objects.new("_Sonne", bpy.data.lights.new("_Sonne", "SUN"))
    sonne.data.energy = 2.5
    sonne.rotation_euler = (math.radians(25), math.radians(-15), 0)
    szene.collection.objects.link(sonne)
    rand = 0.6
    kam.data.ortho_scale = (Y1 - Y0) + 2 * WAND + 2 * rand
    kam.matrix_world = Matrix.Translation((0.0, 0.0, 40.0))
    szene.render.resolution_x = szene.render.resolution_y = 900
    liste = ia.marken(4.3, {"Rat"}) + platz_marken(4.3)
    ia.zeigen(szene, 3, True, decke=False)
    teil = bs.rendern_einzeln(f"_{NAME}-grundriss", "Ratssaal von oben")
    for o in liste:
        bpy.data.objects.remove(o)
    print(bs.zusammensetzen(NAME + "-grundriss", [teil], spalten=1))


def main():
    objekte = bauen()
    vorher, nachher = ia.farben_zusammenlegen(objekte)
    print(f"Materialien: {vorher} → {nachher}")
    ia.pruefbericht(objekte, NAME)
    print("geschrieben:", ti.exportieren(objekte, NAME))
    if "--ohne-bilder" in sys.argv:
        return
    vorschau("--schnell" in sys.argv)


if __name__ == "__main__":
    main()
