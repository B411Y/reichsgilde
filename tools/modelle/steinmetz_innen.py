"""Innenraum des Steinmetzen: Werkraum, darüber das Steinlager unter dem Dach und ab der Steinmetzhütte die Bauhütte;
Stufen Maurerei, Steinschneiderei, Steinmetzhütte.

    blender -b --python tools/modelle/steinmetz_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/steinmetz_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/steinmetz_innen.glb und Bilder nach docs/modelle/ (steinmetz_innen-blicke.png: das Bild jedes
Blickpunkts, steinmetz_innen-stufen.png: Erdgeschoss von oben je Stufe, steinmetz_innen-grundriss.png: Erdgeschoss und
Dachboden mit Ankern).

Koordinaten wie beim Schmied (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte des
Werkraums auf dem Boden. Werkraum wie beim Schmied ein Steinraum (0,5 m, gekalkter Putz, an Ecken und unten bricht der
Bruchstein durch; Boden in Stufe 1 und 2 gestampfter Lehm, in Stufe 3 Backsteinpflaster im Fischgrätmuster;
Balkendecke): innen x −4 … 4, y −3,5 … 3,5, 4,0 m hoch (Platz für das Dreibein). Das Steinlager liegt darüber unter dem
Dach (Boden in 4,05 m, First entlang x, Kniestock 1,3 m, 48°, Ladeluke mit Kranbalken im rechten Giebel – die Steine kommen
mit dem Seil hinauf); eine steile Treppe an der rechten Wand führt hinauf. Bauhütte hinter dem Werkraum x −4 … 4,
y 4,0 … 10,0, 4,6 m hoch – eine Bretterhalle mit Ständern und Lehmboden, an die Rückwand des Hauses gebaut (ihre
Vorderwand ist dessen Bruchstein, außen unverputzt). Alle Räume sind rundum geschlossen, die Kamera steht im Raum.

Knoten im GLB – dieselben Regeln wie in schmied_innen.glb und tischler_innen.glb (Raum_…, Decke_…, Stufe1–Stufe3,
Bedienung_…, Tuer_…, Einrichtung_…, Holztuer, Blick_…, Arbeit_…, Lager_…, Werkstueck_…, Licht_…, Kranbalken,
Himmel_Lager; Tuer_Werk_Lager ist die Treppe, Tuer_Lager_Werk die Bodenluke, beide mit extras „schild“), dazu:
    Raum_Huette                 die Bauhütte, extras „stufe“ = 3: das Spiel zeigt sie erst ab der Steinmetzhütte
    Tuer_Werk_Huette,           Tür zwischen Werkraum und Bauhütte, extras „stufe“ = 3 (vorher unsichtbar und nicht
    Tuer_Huette_Werk            anklickbar); in Stufe1 und Stufe2 ist die Öffnung im Werkraum vermauert
    Spaene1–Spaene3             Steinsplitter und Staub unter Werkblock und Schleifstein in drei Mengen; jeder Knoten ist
                                ein ganzer Stand, alle sind standardmäßig aus. Darunter je ein Kind „Spaene<n>_Saege“ mit
                                extras „stufe“ = 2: Schlamm und Splitter unter der Steinsäge
    Arbeit_3_Saegen             extras „requisit“ = „keins“: Die Figur sägt mit der Bewegung des Tischlers, aber ohne Handsäge
                                – der Griff der Steinsäge liegt in ihrer rechten Hand
    Schleifstein, Tritt, Kurbelstange   bewegte Teile des Schleifsteins (unter Raum_Werk, Ursprung im Drehpunkt)
    Steinsaege2, Saegeseil_vorn2, Saegeseil_hinten2 (unter Stufe2) und dieselben mit 3 (unter Stufe3): Rahmen der Steinsäge
                                und die Seile, an denen er hängt
    Last2, Lastseil2, Haspel2 (unter Stufe2) und dieselben mit 3: Block mit Steinzange und unterer Flasche am Dreibein, das
                                Seil des Flaschenzugs, die Winde
Animation „Arbeit“ (6 s, als Schleife zu spielen, solange gearbeitet wird): Der Schleifstein dreht sich fünfmal (1,2 s je
Umdrehung; Tritt und Kurbelstange im Takt der Bewegung „Schleifen“: Tritt oben bei Phase 0,25, also Phase = Zeit modulo
1,2 s), der Rahmen der Steinsäge geht sechsmal vor und zurück – genau mit der rechten Hand der Bewegung „Saegen“ (Phase =
Zeit modulo 1 s) –, der Flaschenzug hebt den Block in 3 s um 0,6 m an und senkt ihn wieder, die Winde dreht sich mit.
Ruht der Betrieb, steht alles in der Lage der Datei (Block unten, Rahmen hinten, Kurbel vorn).
Lichter: extras „art“ = „Fenster“ (Tageslicht) und „Lampe“ (Laterne an der Decke, Blick −Z nach unten) wie beim Tischler.
Materialien: „Farben“ (Eckenfarben; die Materialart für den Holzschnitt je Fläche als UV.y, bausteine.arten_setzen),
„Hausfarbe“ (Hoftor, Holztür, Ladetor, Flügel der Ladeluke), „Fenster“ (auch der Himmel hinter der Ladeluke), „Glut“
(Laternen), „Eisen“, „Wasser“. Die Steine heißen so, dass materialarten.json passt: roher Stein „Fels …“ (Holzschnitt
„fels“), modellierte Mauersteine „Bruch …“ (keine Struktur: Steine und Fugen zeichnet das Modell), glatt behauene Stücke
„Quader“ (keine Struktur, nur Schraffur), Staub „Steinstaub“ und der Lehmboden „Erde hell“ („boden“).
Einrichtung_Flaschenzug: ein kleiner Drehkran neben dem Werkblock (Pfosten, Ausleger, Kloben mit vier Seilen, Steinzange) –
unabhängig vom Dreibein der Steinschneiderei, deshalb schon in der Maurerei sinnvoll.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import figuren as fg  # noqa: E402  (Hand der Bewegung „Saegen“ für den Griff der Steinsäge, Arbeitshöhen)
import innenausbau as ia  # noqa: E402  (Böden, Putz, Decke, Treppe, Dachboden, Prüfbericht, Vorschau)
import schmied_innen as si  # noqa: E402  (Mauern mit Fugen, Fenster, Steinboden, Pult, Türen, Werkzeugwand, Kisten, Anker)
import steinmetz  # noqa: E402  (Säule und Quaderstapel wie außen)
import tischler_innen as ti  # noqa: E402  (Hoftor, Schürzen, Laterne, Fenster in dünnen Wänden, Animation, Export)

NAME = "steinmetz_innen"
STUFEN = ("Maurerei", "Steinschneiderei", "Steinmetzhütte")

bs.FARBEN.update({
    "Fels Sandstein": (0.66, 0.57, 0.43),   # roher Sandstein: Werkblock, Rohblöcke, Schleifstein
    "Fels Granit": (0.49, 0.48, 0.46),      # roher Granit
    "Fels Bruch": (0.44, 0.42, 0.38),       # grobe Bruchsteine im Haufen
    "Quader": (0.77, 0.68, 0.52),           # glatt behauener Sandstein: Quader, Säule, Figur, Taufbecken
    "Quader grau": (0.63, 0.62, 0.59),      # glatt behauener Kalkstein: Grabmal, Platten
    "Splitt": (0.80, 0.75, 0.66),           # frische Steinsplitter
    "Erde hell": (0.47, 0.39, 0.28),        # festgetretener Lehm in der Bauhütte (wie landbau.py)
})

# ── Maße ─────────────────────────────────────────────────────────────────────
WAND = si.WAND                                                     # Bruchstein, 0,5 m
WX0, WX1, WY0, WY1 = -4.0, 4.0, -3.5, 3.5                          # Werkraum innen
H_WERK = 4.0
Z_LAGER = H_WERK + ia.DIELE                                        # Boden des Steinlagers unter dem Dach
HX0, HX1, HY0, HY1 = WX0, WX1, WY1 + WAND, WY1 + WAND + 6.0        # Bauhütte innen
H_HUETTE = 4.6
BRETTWAND = 0.25                                                   # Bretterwand der Bauhütte
TREPPE_X, TREPPE_FUSS, TREPPE_OBEN = 3.63, 3.0, 1.6               # steile Treppe an der rechten Wand, steigt nach vorn
LUKE = (3.25, WX1, 1.56, 2.70)                                     # Treppenloch in der Decke (x0, x1, y0, y1)
LADELUKE_Y = -1.1                                                  # Ladeluke im rechten Giebel (Mitte), 1,2 × 1,8 m
DACH = ia.Dachboden(WX0, WX1, WY0, WY1, Z_LAGER, 48)
HUETTENTUER_X, HUETTENTUER_B, HUETTENTUER_H = 2.6, 1.1, 2.3        # Tür Werkraum – Bauhütte (Rückwand)
HOFTOR_Y, HOFTOR_B, HOFTOR_H = -0.4, 2.6, 3.0                      # Hoftor in der linken Wand
HOLZTUER_X, HOLZTUER_B, HOLZTUER_H = -1.8, 0.95, 2.1               # Tür zum Hof in der Vorderwand
LADETOR_X, LADETOR_B, LADETOR_H = -1.6, 2.6, 3.0                   # Ladetor der Bauhütte (Rückwand)
SICHTFELD = 50.0                                                   # Grad, senkrecht; Bild 16:9
FPS = 24
ARBEIT_BILDER = 6 * FPS        # Länge der Animation „Arbeit“ (6 s: fünf Umdrehungen zu 1,2 s, sechs Sägestriche zu 1 s)
SCHLEIF_TAKT = 1.2             # Sekunden je Umdrehung des Schleifsteins = Dauer der Bewegung „Schleifen“
SAEGE_TAKT = 1.0               # Sekunden je Sägestrich = Dauer der Bewegung „Saegen“
HUB = 0.6                      # so weit hebt der Flaschenzug den Block

# Arbeitsplätze – die Höhen passen zu figuren.ARBEITSHOEHEN (Block, Stein, Figur) und zur Hand der Bewegung „Saegen“
WERKBLOCK = Vector((-0.8, 0.85, 0))        # Mitte des Blocks auf den Böcken
BLOCK = (0.9, 0.6, 0.38)                   # Block auf dem Werkblock: Breite (x), Tiefe (y), Höhe
SCHLEIF = Vector((2.6, -2.0, 0))           # Schleifstein: Mitte unter der Achse, Achse entlang y
SCHLEIF_Z, SCHLEIF_R = 0.72, 0.38          # Achshöhe und Halbmesser (wie der Schleifbock des Schmieds)
KURBEL_R, STANGE_Y = 0.11, -0.36           # Kurbel des Schleifsteins; die Kurbelstange läuft bei SCHLEIF.y + STANGE_Y
TRITT_DREH = Vector((SCHLEIF.x - 0.75, SCHLEIF.y - 0.1, 0.03))   # Drehpunkt des Tritts hinter der Ferse
SAEGER = Vector((0.7, 2.3, 0))             # Arbeitsplatz an der Steinsäge, Blick +x
DREIBEIN = Vector((-2.4, 1.9, 0))          # Mitte des Dreibeins; der Block hängt darunter
DREIBEIN_H = 3.55
LAST = (0.8, 0.55, 0.5)                    # Block am Flaschenzug
BILDHAUER = Vector((0.6, 8.0, 0))          # Arbeitsplatz an der Steinfigur, Blick −x
KRAN = Vector((0.25, 0.75, 0))             # Pfosten des Drehkrans (Einrichtung_Flaschenzug)

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Werk_0": ((0.3, -3.1, 2.8), (-0.4, 1.8, 0.9)),          # Werkblock, Dreibein, Steinsäge, Tür zur Bauhütte
    "Blick_Werk_L1": ((1.8, -2.4, 2.7), (-4.0, 0.3, 0.9)),         # Hoftor, Personalbuch, Dreibein
    "Blick_Werk_L2": ((2.2, 0.2, 2.8), (-3.6, -3.0, 0.8)),         # Holztür, Personalbuch, Schürzen, Löschfass
    "Blick_Werk_R1": ((-3.0, -2.9, 2.8), (3.4, 2.9, 0.9)),         # schräg: Steinsäge, Treppe, Tür zur Bauhütte
    "Blick_Werk_R2": ((-1.4, -1.2, 2.7), (4.0, -0.6, 1.0)),        # Schleifstein, Werkzeugwand
    "Blick_Lager_0": ((-3.4, -1.4, Z_LAGER + 2.1), (3.2, -0.4, Z_LAGER + 0.7)),     # den First entlang: Ladeluke, Stapel
    "Blick_Lager_L1": ((3.2, -2.5, Z_LAGER + 2.0), (-3.8, 0.9, Z_LAGER + 0.9)),     # zurück: Quader, Grabplatten, Steingestell
    "Blick_Lager_R1": ((2.5, -0.25, Z_LAGER + 2.2), (3.6, 2.1, Z_LAGER - 0.6)),     # hinunter in die Bodenluke mit der Treppe
    "Blick_Huette_0": ((0.4, 4.4, 3.2), (-0.4, 8.6, 0.9)),         # Steinfigur mit dem Bildhauer, Ladetor
    "Blick_Huette_L1": ((3.0, 4.5, 3.1), (-3.4, 8.0, 0.8)),        # Taufbecken, Grabmal
    "Blick_Huette_R1": ((-2.6, 9.5, 3.2), (2.6, 4.4, 0.9)),        # Tür in den Werkraum, Säule, Gerüstbock
}

Oe = si.Oe
WERK_OEFFNUNGEN = {
    "vorne": [Oe(0.6, 1.4, 0.9, 1.2), Oe(2.7, 1.4, 0.9, 1.2), Oe(HOLZTUER_X, 0.0, HOLZTUER_B, HOLZTUER_H)],
    "rechts": [Oe(-2.5, 1.4, 0.9, 1.2)],
    "hinten": [Oe(HUETTENTUER_X, 0.0, HUETTENTUER_B, HUETTENTUER_H)],
    "links": [Oe(HOFTOR_Y, 0.0, HOFTOR_B, HOFTOR_H), Oe(2.4, 1.4, 0.9, 1.2)],
}
SCHILDER = {"Tuer_Werk_Lager": "Treppe zum Lager", "Tuer_Lager_Werk": "Treppe hinunter zum Werkraum"}   # extras „schild“
HUETTEN_OEFFNUNGEN = {
    "vorne": [Oe(HUETTENTUER_X, 0.0, HUETTENTUER_B, HUETTENTUER_H)],
    "rechts": [Oe(6.6, 1.4, 1.2, 1.3), Oe(9.0, 1.6, 0.8, 1.0)],
    "hinten": [Oe(LADETOR_X, 0.0, LADETOR_B, LADETOR_H), Oe(2.2, 1.7, 1.0, 1.1)],
    "links": [Oe(6.2, 1.4, 1.2, 1.3)],
}

ecken = ia.ecken


# ─────────────────────────────────────────────────────────────────────────────
#  Hülle: Mauern, Bretterwände, Decken, Fenster, Lichter
# ─────────────────────────────────────────────────────────────────────────────

RAEHM = 0.22           # Höhe des Rähms der Bretterwand (oben an der Decke)


def bretterwand(netz, st, a, b, hoehe, oeffnungen, enden=(True, True), eckpfosten=(True, True)):
    """Bretterwand der Bauhütte (a → b, Raum links): Bretter mit Fugen, Ständer, Rähm und ein Riegel, alles innen; die
    Öffnungen bleiben frei. `eckpfosten`: Ständer an den Enden – in einer Ecke steht nur einer, Rähm und Riegel der anderen
    Wand stoßen an ihn (so decken sich keine Flächen)."""
    si.wand(netz, st, a, b, hoehe, oeffnungen, stoff="Brett", dicke=BRETTWAND, enden=enden)
    a3, b3 = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    d = (b3 - a3).normalized()
    n = Vector((-d.y, d.x, 0))
    laenge = (b3 - a3).length
    entlang_x = abs(d.x) > 0.5
    ops = [((o.s - (a3.x if entlang_x else a3.y)) * (d.x if entlang_x else d.y), o) for o in oeffnungen]

    def in_oeffnung(u, rand=0.0):
        return next((o for m, o in ops if abs(u - m) < o.b / 2 + rand), None)

    def p(u, v, w=0.0):
        return a3 + d * u + Vector((0, 0, v)) + n * w

    u = 0.23
    while u < laenge - 0.1:                                                     # Fugen zwischen den Brettern
        o = in_oeffnung(u, 0.02)
        for v0, v1 in ([(0.0, hoehe)] if o is None else [(0.0, o.v - 0.02), (o.v + o.h + 0.02, hoehe)]):
            if v1 - v0 > 0.05:
                netz.vieleck_nach([p(u - 0.01, v0, 0.003), p(u + 0.01, v0, 0.003), p(u + 0.01, v1, 0.003), p(u - 0.01, v1, 0.003)],
                                  n, st["Fachwerk"])
        u += 0.23 + 0.02 * math.sin(u * 7.0)
    alle, netz.alle = netz.alle, True
    M = Matrix((d, n, Vector((0, 0, 1)))).transposed().to_4x4()

    def balken(u0, u1, v0, v1, tiefe):
        netz.quader((0, 0, 0), (1, 1, 1), st["Fachwerk"], Matrix.Translation(p((u0 + u1) / 2, (v0 + v1) / 2, tiefe / 2)) @ M
                    @ Matrix.Diagonal((u1 - u0, tiefe, v1 - v0, 1)), ohne=("-y",))
    innen = []
    u = 2.0
    while u < laenge - 1.0:
        while in_oeffnung(u, 0.12):
            u += 0.25
        innen.append(u)
        u += 2.0
    anfang, schluss = 0.18, laenge - 0.18                                       # am eigenen Eckpfosten oder an dem der Nachbarwand
    if eckpfosten[0]:
        balken(0.0, 0.18, 0.0, hoehe, 0.18)
    if eckpfosten[1]:
        balken(laenge - 0.18, laenge, 0.0, hoehe, 0.18)
    for u in innen:
        balken(u - 0.09, u + 0.09, 0.0, hoehe - RAEHM, 0.12)
    balken(anfang, schluss, hoehe - RAEHM, hoehe, 0.14)                        # Rähm
    grenzen = [anfang] + [g for u in innen for g in (u - 0.09, u + 0.09)] + [schluss]
    for u0, u1 in zip(grenzen[::2], grenzen[1::2]):                             # Riegel zwischen den Ständern
        stuecke = [(u0, u1)]
        for m, o in ops:
            if o.v < 2.25 < o.v + o.h + 0.1:
                stuecke = [s for x0, x1 in stuecke for s in ((x0, min(x1, m - o.b / 2 - 0.02)), (max(x0, m + o.b / 2 + 0.02), x1))
                           if s[1] - s[0] > 0.1]
        for x0, x1 in stuecke:
            balken(x0, x1, 2.18, 2.32, 0.1)
    netz.alle = alle


_lichter = [0]


def licht(punkt, richtung, art, stufe=1):
    """Licht_<n> (fortlaufend): „Fenster“ blickt in den Raum, „Lampe“ nach unten."""
    _lichter[0] += 1
    o = si.licht(f"Licht_{_lichter[0]}", punkt, richtung, art, stufe)
    if art == "Lampe":
        o.rotation_euler = (math.radians(-90), 0, 0)          # Blender +Y (Spiel −Z) zeigt nach unten
    return o


def fenster_und_licht(netz, st, kanten, oeffnungen, anker, stufe=1, mauer=True):
    """Scheiben aller Fenster eines Raums (in der Mauer mit Gittern wie beim Schmied, in der Bretterwand wie beim Tischler)
    und je ein Licht „Fenster“ (Tageslicht, Blick −Z in den Raum)."""
    for seite, liste in oeffnungen.items():
        a, b = kanten[seite]
        d = (Vector((b[0], b[1], 0)) - Vector((a[0], a[1], 0))).normalized()
        n = Vector((-d.y, d.x, 0))
        for o in liste:
            if o.v < 0.05:
                continue
            if mauer:
                si.fenster(netz, st, a, b, o)
            else:
                ti.fenster(netz, st, a, b, o)
            p = Vector((o.s, a[1], 0)) if abs(d.x) > 0.5 else Vector((a[0], o.s, 0))
            p += n * 0.1 + Vector((0, 0, o.v + o.h / 2))
            anker.append(licht(p, (n.x, n.y, 0), "Fenster", stufe))


def tor(netz, st, M, b, h):
    """Zweiflügeliges Tor (Hausfarbe) mit Pfosten und Sturzbalken innen vor der Mauer (Hoftor, Ladetor)."""
    ti.hoftor(netz, st, M, b, h, r=0.12)
    for s in (-1, 1):
        si.q(netz, st, M, (s * (b / 2 + 0.11), 0.04, (h + 0.1) / 2), (0.22, 0.08, h + 0.1), "Fachwerk", ohne=("-y",))
    si.q(netz, st, M, (0, 0.05, h + 0.16), (b + 0.6, 0.1, 0.28), "Fachwerk", ohne=("-y",))


def vermauert(netz, st):
    """Stufe 1 und 2: Die Öffnung zur späteren Bauhütte ist mit Bruchstein zugemauert, etwas zurückgesetzt, mit Fugen."""
    x0, x1 = HUETTENTUER_X - HUETTENTUER_B / 2, HUETTENTUER_X + HUETTENTUER_B / 2
    y = WY1 + 0.05
    netz.vieleck_nach([(x0, y, 0), (x1, y, 0), (x1, y, HUETTENTUER_H), (x0, y, HUETTENTUER_H)], (0, -1, 0), st["Bruch dunkel"])
    zufall = random.Random(17)
    f = 0.014
    for k, z in enumerate((0.38, 0.78, 1.16, 1.52, 1.92)):
        netz.vieleck_nach([(x0, y - 0.003, z - f), (x1, y - 0.003, z - f), (x1, y - 0.003, z + f), (x0, y - 0.003, z + f)], (0, -1, 0),
                          st["Fuge"])
        unten = (0.0, 0.38, 0.78, 1.16, 1.52)[k]
        x = x0 + zufall.uniform(0.15, 0.45)
        while x < x1 - 0.12:
            netz.vieleck_nach([(x - 0.012, y - 0.003, unten + f), (x + 0.012, y - 0.003, unten + f), (x + 0.012, y - 0.003, z - f),
                               (x - 0.012, y - 0.003, unten + f + (z - f - unten - f))], (0, -1, 0), st["Fuge"])
            x += zufall.uniform(0.35, 0.55)


# ─────────────────────────────────────────────────────────────────────────────
#  Werkraum
# ─────────────────────────────────────────────────────────────────────────────

def block(netz, st, mitte, masse, stoff="Fels Sandstein", oben=None, dreh=0.0, z0=0.0):
    """Quader auf `mitte` (Boden bei z0), um die Hochachse gedreht; `oben`: eigenes Material für die Oberseite."""
    t = Matrix.Translation((mitte[0], mitte[1], z0 + masse[2] / 2)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, 0), masse, st[stoff], t, ohne=("+z",) if oben else ())
    if oben:
        netz.quader((0, 0, masse[2] / 2), (masse[0], masse[1], 0.0), st[oben], t, ohne=("-z", "+x", "-x", "+y", "-y"))


def kanthoelzer(netz, st, mitte, laenge, abstand, dreh=0.0, h=0.1, z0=0.0):
    """Zwei Kanthölzer als Unterlage (quer zur Richtung `dreh`, Boden in z0)."""
    t = Matrix.Translation((mitte[0], mitte[1], z0 + h / 2)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for s in (-1, 1):
        netz.quader((s * abstand / 2, 0, 0), (0.12, laenge, h), st["Holz alt"], t)


def werkblock(netz, st):
    """Werkblock: zwei schwere Eichenklötze, darauf der rohe Sandsteinblock, oben schon glatt behauen. Daneben ein Eimer
    mit Eisen und ein Zweispitz am Boden."""
    bx, by, bh = BLOCK
    oben = fg.ARBEITSHOEHEN["Block"][1]
    unten = oben - bh
    for dx in (-0.29, 0.29):
        netz.quader((WERKBLOCK.x + dx, WERKBLOCK.y, unten / 2), (0.26, 0.7, unten), st["Holz alt"], ohne=("+z",))
        for z in (0.12, unten - 0.1):                                                 # Eisenbänder
            netz.quader((WERKBLOCK.x + dx, WERKBLOCK.y, z), (0.272, 0.712, 0.04), st["Eisen"], ohne=("+z", "-z"))
    block(netz, st, WERKBLOCK, (bx, by, bh), "Fels Sandstein", oben="Quader", z0=unten)
    netz.quader((WERKBLOCK.x, WERKBLOCK.y + by / 2 - 0.037, oben - 0.03), (bx + 0.004, 0.08, 0.06), st["Quader"],
                ohne=("-z", "+z", "-y"))                                              # Randschlag an der Kante zum Steinmetz
    eimer = WERKBLOCK + Vector((0.72, 0.45, 0))
    bs.achteck(netz, st, eimer, 0.13, 0.0, 0.3, "Holz alt")
    bs.rund_oben(netz, st, eimer, 0.115, 0.301, "Loch")
    for k in range(4):                                                                 # Eisen im Eimer
        w = 2 * math.pi * k / 4
        a = eimer + Vector((0.05 * math.cos(w), 0.05 * math.sin(w), 0.15))
        netz.stab(a, a + Vector((0.03 * math.cos(w), 0.03 * math.sin(w), 0.27)), 0.02, 0.02, st["Eisen"])
    netz.stab(WERKBLOCK + Vector((0.6, -0.5, 0.03)), WERKBLOCK + Vector((0.95, -0.35, 0.03)), 0.035, 0.035, st["Holz"])   # Zweispitz
    netz.stab(WERKBLOCK + Vector((0.92, -0.48, 0.035)), WERKBLOCK + Vector((0.98, -0.22, 0.035)), 0.04, 0.04, st["Eisen"])


def splitter(netz, st, mitte, rx, ry, menge, saat, z0, staub="Steinstaub"):
    """Steinsplitter und Staub um `mitte`: ein flacher Staubfleck, kantige Splitter (kleine Pyramiden), ab Menge 2 ein
    flacher Haufen Grus. menge 1–3."""
    zufall = random.Random(saat)
    f = (0.6, 0.85, 1.0)[menge - 1]
    rand = [mitte + Vector((rx * f * math.cos(2 * math.pi * k / 10) * zufall.uniform(0.75, 1.1),
                            ry * f * math.sin(2 * math.pi * k / 10) * zufall.uniform(0.75, 1.1), z0)) for k in range(10)]
    netz.vieleck_nach(rand, (0, 0, 1), st[staub])
    if menge >= 2:                                                                     # Grus
        h = 0.025 * menge
        fuss = [mitte + (q - mitte) * 0.45 + Vector((0, 0, 0.001)) for q in rand[::2]]
        spitze = mitte + Vector((zufall.uniform(-0.04, 0.04), zufall.uniform(-0.04, 0.04), z0 + h))
        for i in range(len(fuss)):
            a, b = fuss[i], fuss[(i + 1) % len(fuss)]
            netz.vieleck_nach([a, b, spitze], (a + b) / 2 - mitte + Vector((0, 0, 0.3)), st["Splitt"])
    for k in range(int(12 * menge ** 1.5)):                                            # Splitter
        w, r = zufall.uniform(0, 2 * math.pi), math.sqrt(zufall.uniform(0, 1))
        p = mitte + Vector((rx * f * r * math.cos(w), ry * f * r * math.sin(w), z0 + 0.001))
        g = zufall.uniform(0.018, 0.045)
        dreh = zufall.uniform(0, 2 * math.pi)
        fuss = [p + Vector((g * math.cos(dreh + 2.1 * i), g * 0.8 * math.sin(dreh + 2.1 * i), 0)) for i in range(3)]
        spitze = p + Vector((zufall.uniform(-0.3, 0.3) * g, zufall.uniform(-0.3, 0.3) * g, g * zufall.uniform(0.5, 0.9)))
        m = st["Splitt" if k % 3 else "Fels Sandstein"]
        for i in range(3):
            a, b = fuss[i], fuss[(i + 1) % 3]
            netz.vieleck_nach([a, b, spitze], (a + b) / 2 - p + Vector((0, 0, 0.2)), m)


def spaene_stand(menge, st):
    """Spaene<menge>: Splitter und Staub unter dem Werkblock (vor allem auf der Seite des Steinmetzen) und unter dem
    Schleifstein; Kind „Spaene<menge>_Saege“ (ab Stufe 2): Schlamm und Splitter unter der Steinsäge."""
    z0 = 0.003 + 0.0012 * menge                                                        # die drei Stände decken sich nicht
    netz = bs.Netz(f"Spaene{menge}")
    splitter(netz, st, WERKBLOCK + Vector((0.0, 0.42, 0)), 0.85, 0.55, menge, 11 + menge, z0)
    splitter(netz, st, SCHLEIF + Vector((-0.3, 0.05, 0)), 0.4, 0.32, menge, 21 + menge, z0)
    saege = bs.Netz(f"Spaene{menge}_Saege")
    h0 = saeger_hand(0.0)
    splitter(saege, st, Vector((h0.x + 0.92, SAEGER.y + 0.12 - 0.5, 0)), 0.55, 0.22, menge, 31 + menge, z0)
    splitter(saege, st, Vector((h0.x + 1.75, SAEGER.y + 0.12, 0)), 0.25, 0.35, menge, 41 + menge, z0)
    o, kind = netz.objekt(), saege.objekt()
    kind.parent = o
    kind.matrix_parent_inverse = Matrix.Identity(4)
    kind["stufe"] = 2
    bs.zustand(o)
    return o


# ── Schleifstein (bewegt) ─────────────────────────────────────────────────────

def schleifstein_fest(netz, st):
    """Gestell des Schleifsteins: zwei Böcke mit Lagern, Wassertrog unter dem Stein, Ablage für das Werkstück, Lager des
    Tritts. Stein, Kurbel, Kurbelstange und Tritt bewegen sich (schleifstein_teile)."""
    holz, alt = st["Holz"], st["Holz alt"]
    m = SCHLEIF
    for sy in (-1, 1):
        for dx in (-0.3, 0.3):
            netz.stab(m + Vector((dx, sy * 0.2, 0)), m + Vector((dx * 0.35, sy * 0.2, SCHLEIF_Z - 0.05)), 0.08, 0.08, alt)
        netz.stab(m + Vector((-0.32, sy * 0.2, 0.3)), m + Vector((0.32, sy * 0.2, 0.3)), 0.07, 0.07, alt)
        netz.quader(m + Vector((0, sy * 0.2, SCHLEIF_Z - 0.03)), (0.16, 0.09, 0.1), holz)               # Lager
    si.trog(netz, st, m + Vector((0, 0, 0)), 0.56, 0.3, 0.3, wasser=0.07)
    ablage = m + Vector((0.5, 0.0, 0))
    netz.quader(ablage + Vector((0, 0, 0.42)), (0.3, 0.42, 0.04), holz)
    for sy in (-1, 1):
        netz.stab(ablage + Vector((0, sy * 0.16, 0)), ablage + Vector((0, sy * 0.16, 0.4)), 0.05, 0.05, holz)
    netz.quader(TRITT_DREH + Vector((0.02, 0, -0.015)), (0.1, 0.18, 0.03), alt)                         # Lager des Tritts


def kurbel_winkel(t):
    """Drehung des Schleifsteins (um y) zur Zeit t: Kurbelzapfen oben bei Phase 0,25 der Bewegung „Schleifen“ (dann ist
    der Fuß am höchsten), eine Umdrehung je SCHLEIF_TAKT."""
    return -2 * math.pi * (t - 0.25 * SCHLEIF_TAKT) / SCHLEIF_TAKT - math.pi / 2


def kurbelzapfen(w):
    return Vector((SCHLEIF.x, SCHLEIF.y + STANGE_Y, SCHLEIF_Z)) + Matrix.Rotation(w, 3, "Y") @ Vector((KURBEL_R, 0, 0))


TRITT_L = SCHLEIF.x - TRITT_DREH.x          # vom Drehpunkt bis zum Ende unter der Kurbel
STANGE_L = SCHLEIF_Z - 0.13                 # Kurbelstange: Zapfen in Mittellage bis zum Ende des Tritts (0,13 m hoch)


def tritt_winkel(w):
    """Neigung des Tritts (Bogenmaß, + = vorn hoch), so dass sein Ende genau STANGE_L unter dem Kurbelzapfen liegt."""
    zapfen = kurbelzapfen(w)
    lo, hi = -0.6, 0.6
    for _ in range(50):
        m = (lo + hi) / 2
        ende = TRITT_DREH + Vector((TRITT_L * math.cos(m), 0, TRITT_L * math.sin(m)))
        abstand = math.hypot(zapfen.x - ende.x, zapfen.z - ende.z)
        if abstand > STANGE_L:
            lo = m
        else:
            hi = m
    return (lo + hi) / 2


def schleifstein_teile(st):
    """Bewegte Teile: Stein mit Achse und Kurbel (Ursprung auf der Achse), Tritt (Ursprung im Drehpunkt) und Kurbelstange
    (Ursprung am Kurbelzapfen). Gibt [(Objekt, Bewegung)] zurück; Bewegung(Bild) → (Ort, Drehung, Maßstab)."""
    achse = Vector((SCHLEIF.x, SCHLEIF.y, SCHLEIF_Z))
    stein = bs.Netz("Schleifstein", alle=True)
    teile = 14
    ring = [(SCHLEIF_R * math.cos(2 * math.pi * k / teile), SCHLEIF_R * math.sin(2 * math.pi * k / teile)) for k in range(teile)]
    dreh = Matrix.Rotation(math.radians(90), 4, "Z")                                   # Prisma-x → Welt-y (Achse)
    stein.prisma(ring, -0.075, 0.075, st["Fels Sandstein"], matrix=dreh, m_enden=st["Quader"])
    stein.stab((0, -0.3, 0), (0, 0.3, 0), 0.045, 0.045, st["Eisen"])
    stein.stab((0, STANGE_Y + 0.02, 0), (0, -0.3, 0), 0.045, 0.045, st["Eisen"])
    stein.stab((0, STANGE_Y + 0.03, 0), (KURBEL_R, STANGE_Y + 0.03, 0), 0.05, 0.025, st["Eisen"], quer=(0, 1, 0))
    stein.stab((KURBEL_R, STANGE_Y + 0.045, 0), (KURBEL_R, STANGE_Y - 0.02, 0), 0.025, 0.025, st["Eisen"])   # Zapfen
    for k in range(4):                                                                 # Zwingen am Stein
        w = math.pi / 4 + math.pi / 2 * k
        stein.stab((0.05 * math.cos(w), -0.081, 0.05 * math.sin(w)), (0.12 * math.cos(w), -0.081, 0.12 * math.sin(w)), 0.03, 0.01,
                   st["Eisen"], quer=(0, 1, 0))
    o_stein = stein.objekt()
    o_stein.location = achse

    tritt = bs.Netz("Tritt", alle=True)                                                # liegt flach entlang +x, dreht um y
    tritt.quader((TRITT_L / 2, 0, 0.0), (TRITT_L + 0.06, 0.14, 0.03), st["Holz alt"])
    tritt.quader((TRITT_L, (STANGE_Y + 0.1) / 2, 0.0), (0.06, -STANGE_Y - 0.1 + 0.06, 0.035), st["Holz alt"])
    tritt.quader((0.3, 0, 0.022), (0.22, 0.12, 0.015), st["Holz"])                   # Trittbrett unter dem rechten Fuß
    o_tritt = tritt.objekt()
    o_tritt.location = TRITT_DREH

    stange = bs.Netz("Kurbelstange", alle=True)                                        # entlang −z, dreht zum Tritt
    stange.stab((0, 0, 0.03), (0, 0, -STANGE_L - 0.02), 0.035, 0.035, st["Holz"])
    o_stange = stange.objekt()

    def zeit(bild):
        return bild / FPS

    def stein_bewegung(bild):
        return None, (0, kurbel_winkel(zeit(bild)), 0), None

    def tritt_bewegung(bild):
        return None, (0, -tritt_winkel(kurbel_winkel(zeit(bild))), 0), None

    def stange_bewegung(bild):
        w = kurbel_winkel(zeit(bild))
        zapfen = kurbelzapfen(w)
        neig = tritt_winkel(w)
        ende = TRITT_DREH + Vector((TRITT_L * math.cos(neig), STANGE_Y + 0.1, TRITT_L * math.sin(neig)))
        ende.y = zapfen.y
        richtung = (ende - zapfen).normalized()
        return zapfen, richtung.to_track_quat("-Z", "Y").to_euler(), None

    # Ruhelage der Datei = Bild 0
    for o, bewegung in ((o_stein, stein_bewegung), (o_tritt, tritt_bewegung), (o_stange, stange_bewegung)):
        ort, dreh_, _ = bewegung(0)
        if ort is not None:
            o.location = ort
        o.rotation_euler = dreh_
    return [(o_stein, stein_bewegung), (o_tritt, tritt_bewegung), (o_stange, stange_bewegung)]


# ── Steinsäge (bewegt, ab Stufe 2) ────────────────────────────────────────────

def saeger_hand(t):
    """Mitte der rechten Hand der Figur am Platz der Steinsäge (Welt) zur Zeit t: Bewegung „Saegen“, Phase = t modulo
    SAEGE_TAKT – dieselbe Rechnung wie Blender (figuren.unterarm_lage) für den Menschen."""
    p = fg.VOELKER["mensch"]
    winkel, hoch = fg.haltung("Saegen", (t % SAEGE_TAKT) / SAEGE_TAKT, p)
    hand = fg.unterarm_lage(p, winkel, hoch, "R") @ fg.hand_ruhe(p, "R")          # Figur: Blick −Y, links +X
    return SAEGER + Vector((-hand.y, hand.x, hand.z))                                # Blick +x: vor → +x, links → +y


SAEGE_Y = SAEGER.y + 0.12                    # Ebene des Rahmens: knapp rechts der linken Hand (die auf dem Spannholz liegt)
SPANNHOLZ_Z = 1.0                            # Oberkante des Spannholzes in Mittellage; die linke Hand liegt darauf
STEIN_OBEN = 0.72                            # Oberkante des Blocks unter der Säge


def steinsaege_fest(netz, st):
    """Galgen der Steinsäge (zwei Pfosten, Querbalken, Längsbalken, an dem der Rahmen hängt), der Block auf Kanthölzern mit
    dem Schnitt, ein Wasserfass."""
    h0 = saeger_hand(0.0)
    holz, alt = st["Fachwerk"], st["Holz alt"]
    mitte = h0.x + 0.875
    for s in (-1, 1):
        netz.quader((mitte, SAEGE_Y + s * 0.62, 1.4), (0.16, 0.16, 2.8), holz)
        netz.quader((mitte, SAEGE_Y + s * 0.62, 0.06), (0.6, 0.24, 0.12), holz)          # Fuß
        netz.stab((mitte - 0.28, SAEGE_Y + s * 0.62, 0.12), (mitte - 0.05, SAEGE_Y + s * 0.62, 0.65), 0.08, 0.08, holz)
        netz.stab((mitte + 0.28, SAEGE_Y + s * 0.62, 0.12), (mitte + 0.05, SAEGE_Y + s * 0.62, 0.65), 0.08, 0.08, holz)
    alle, netz.alle = netz.alle, True
    netz.quader((mitte, SAEGE_Y, 2.76), (0.18, 1.5, 0.16), holz)                          # Querbalken
    netz.quader((mitte, SAEGE_Y, 2.62), (1.7, 0.14, 0.14), holz)                          # Längsbalken
    netz.alle = alle
    x0, x1 = h0.x + 0.27, h0.x + 1.57
    kanthoelzer(netz, st, ((x0 + x1) / 2, SAEGE_Y), 0.9, 0.85, dreh=0, h=0.12)
    block(netz, st, ((x0 + x1) / 2, SAEGE_Y), (x1 - x0, 0.7, STEIN_OBEN - 0.12), "Fels Granit", oben="Quader grau", z0=0.12)
    tief = 0.52                                                                           # bis hierher ist gesägt
    netz.vieleck_nach([(x0 - 0.002, SAEGE_Y - 0.008, tief), (x0 - 0.002, SAEGE_Y + 0.008, tief), (x0 - 0.002, SAEGE_Y + 0.008, STEIN_OBEN),
                       (x0 - 0.002, SAEGE_Y - 0.008, STEIN_OBEN)], (-1, 0, 0), st["Loch"])
    netz.vieleck_nach([(x0, SAEGE_Y - 0.008, STEIN_OBEN + 0.002), (x1 - 0.12, SAEGE_Y - 0.008, STEIN_OBEN + 0.002),
                       (x1 - 0.12, SAEGE_Y + 0.008, STEIN_OBEN + 0.002), (x0, SAEGE_Y + 0.008, STEIN_OBEN + 0.002)], (0, 0, 1), st["Loch"])
    fass = Vector((h0.x - 0.35, SAEGE_Y + 0.75, 0))                                     # hinter dem Säger, links
    bs.fass(netz, st, fass, r=0.26, h=0.62, reifen=(0.12, 0.48))
    bs.rund_oben(netz, st, fass, 0.23, 0.58, "Wasser")
    netz.stab(fass + Vector((0.1, -0.1, 0.55)), fass + Vector((0.3, 0.2, 0.95)), 0.03, 0.03, st["Holz"])    # Schöpfkelle


def steinsaege_teile(st, k):
    """Bewegte Teile der Steinsäge für Stufe k: der Rahmen (Ursprung im Griff = Hand in Ruhe) und die beiden Seile
    (Ursprung oben am Längsbalken, nach unten). Der Rahmen folgt genau der rechten Hand der Bewegung „Saegen“."""
    h0 = saeger_hand(0.0)
    rahmen = bs.Netz(f"Steinsaege{k}", alle=True)
    y = SAEGE_Y - h0.y                                                                    # Ebene des Rahmens, örtlich
    z_blatt = 0.6 - h0.z                                                                  # Unterkante des Blatts in Ruhe
    z_spann = SPANNHOLZ_Z + 0.036 - h0.z                     # Oberkante des Spannholzes in Ruhe (der Rahmen steht 3,6 cm höher)
    holz, eisen = st["Holz"], st["Eisen"]
    for x, oben in ((0.0, 0.05), (1.75, z_spann + 0.12)):                               # Arme: vorn mit Horn am Griff
        rahmen.quader((x, y, (z_blatt + oben) / 2), (0.07, 0.05, oben - z_blatt), holz)
    rahmen.quader((0.875, y, z_spann - 0.03), (1.82, 0.06, 0.06), holz)                  # Spannholz
    rahmen.quader((0.875, y, z_blatt + 0.055), (1.86, 0.012, 0.11), eisen)               # Blatt im Schnitt
    for x in (0.25, 1.5):                                                                 # Ösen für die Seile
        rahmen.quader((x, y, z_spann + 0.01), (0.05, 0.07, 0.05), eisen)
    rahmen.stab((0.0, y + 0.02, 0.0), (0.0, -0.07, 0.0), 0.045, 0.045, holz)            # Griff quer durch die Hand
    for x0, x1 in ((0.04, 0.45), (1.3, 1.71)):                                            # Streben zwischen Arm und Spannholz
        rahmen.stab((x0, y, z_blatt + 0.14 if x0 < 1 else z_spann - 0.05), (x1, y, z_spann - 0.05 if x0 < 1 else z_blatt + 0.14),
                    0.04, 0.03, holz, quer=(0, 1, 0))
    o_rahmen = rahmen.objekt()
    o_rahmen.location = h0

    seile = []
    for name, x in (("vorn", 0.25), ("hinten", 1.5)):
        fuss = h0 + Vector((x, y, z_spann + 0.035))
        oben = Vector((fuss.x, fuss.y, 2.55))
        laenge = oben.z - fuss.z
        seil = bs.Netz(f"Saegeseil_{name}{k}", alle=True)
        seil.stab((0, 0, 0), (0, 0, -laenge), 0.02, 0.02, st["Seil"])
        o = seil.objekt()
        o.location = oben
        seile.append((o, oben, fuss, laenge))

    def rahmen_bewegung(bild):
        return saeger_hand(bild / FPS), None, None

    def seil_bewegung(oben, fuss, laenge):
        def bewegung(bild):
            ziel = fuss + (saeger_hand(bild / FPS) - h0)
            v = ziel - oben
            return None, v.normalized().to_track_quat("-Z", "Y").to_euler(), (1, 1, v.length / laenge)
        return bewegung

    for o, oben, fuss, laenge in seile:
        o.rotation_euler = seil_bewegung(oben, fuss, laenge)(0)[1]
    return [(o_rahmen, rahmen_bewegung)] + [(o, seil_bewegung(oben, fuss, laenge)) for o, oben, fuss, laenge in seile]


# ── Dreibein mit Flaschenzug (bewegt, ab Stufe 2) ─────────────────────────────

def dreibein_fuesse():
    return [DREIBEIN + Vector((1.25 * math.cos(math.radians(w)), 1.25 * math.sin(math.radians(w)), 0)) for w in (60, 180, 300)]


HASPEL = 0.28          # Lage der Winde auf den Beinen 1 und 3 (Anteil vom Fuß zur Spitze)
HASPEL_R = 0.08
FLASCHE_OBEN = DREIBEIN_H - 0.45           # Unterkante der oberen Flasche
LAST_Z = 0.1                               # Unterkante des Blocks in Ruhe (auf den Kanthölzern)


def hub(t):
    """Wie hoch der Block zur Zeit t hängt: in 3 s hinauf, in 3 s hinunter, weich."""
    return HUB * (0.5 - 0.5 * math.cos(2 * math.pi * t / (ARBEIT_BILDER / FPS)))


def dreibein_fest(netz, st):
    """Dreibein: drei Stangen, Kopf mit Eisenband, obere Flasche, Lager der Winde und das Seil von der Winde nach oben."""
    spitze = Vector((DREIBEIN.x, DREIBEIN.y, DREIBEIN_H))
    for f, w in zip(dreibein_fuesse(), (60, 180, 300)):                # je Bein anders gedreht: zwei Beine liegen in einer
        quer = (math.cos(math.radians(w + 35)), math.sin(math.radians(w + 35)), 0)   # Ebene, ihre Seiten sollen sich nicht decken
        netz.stab(f, spitze + (spitze - f).normalized() * 0.3, 0.15, 0.15, st["Fachwerk"], quer=quer)
    alle, netz.alle = netz.alle, True
    netz.quader(spitze + Vector((0, 0, 0.02)), (0.26, 0.26, 0.22), st["Eisen"])
    netz.quader(spitze + Vector((0, 0, -0.3)), (0.12, 0.22, 0.3), st["Holz"])               # obere Flasche
    for dy in (-0.05, 0.05):
        netz.stab(spitze + Vector((-0.07, dy, -0.3)), spitze + Vector((0.07, dy, -0.3)), 0.16, 0.03, st["Holz alt"])   # Rollen
    netz.alle = alle
    f1, _, f3 = dreibein_fuesse()
    a, b = f1.lerp(spitze, HASPEL), f3.lerp(spitze, HASPEL)
    for p in (a, b):
        netz.quader(p, (0.2, 0.2, 0.14), st["Holz alt"])                                     # Lager an den Beinen
    mitte = (a + b) / 2
    netz.stab(mitte + Vector((0, 0, HASPEL_R)), spitze + Vector((-0.06, 0, -0.32)), 0.025, 0.025, st["Seil"])


def dreibein_teile(st, k):
    """Bewegte Teile des Dreibeins für Stufe k: Last (Block, Steinzange, untere Flasche mit Haken; Ursprung unten in der
    Mitte), Lastseil (zwei Stränge von der oberen zur unteren Flasche, Ursprung oben) und Haspel (Winde, Ursprung auf der
    Achse)."""
    spitze = Vector((DREIBEIN.x, DREIBEIN.y, DREIBEIN_H))
    last = bs.Netz(f"Last{k}", alle=True)
    bx, by, bh = LAST
    block(last, st, (0, 0), LAST, "Fels Granit", z0=0.0)
    oben = bh
    flasche = oben + 0.4                                                                    # Unterkante der unteren Flasche
    for s in (-1, 1):                                                                        # Steinzange: zwei gekreuzte Schenkel
        y = s * 0.018                                                                        # nebeneinander, nicht ineinander
        last.stab((s * (bx / 2 + 0.02), y, oben - 0.12), (-s * 0.06, y, flasche - 0.12), 0.05, 0.03, st["Eisen"], quer=(0, 1, 0))
        last.stab((-s * 0.06, y, flasche - 0.12), (0, y, flasche - 0.03), 0.04, 0.03, st["Eisen"], quer=(0, 1, 0))
        last.quader((s * (bx / 2 + 0.03), 0, oben - 0.1), (0.04, 0.1, 0.1), st["Eisen"])
    last.quader((0, 0, flasche + 0.12), (0.12, 0.2, 0.24), st["Holz"])
    last.stab((-0.07, 0, flasche + 0.14), (0.07, 0, flasche + 0.14), 0.14, 0.03, st["Holz alt"])
    last.stab((0, 0, flasche), (0, 0, flasche - 0.05), 0.03, 0.03, st["Eisen"])
    o_last = last.objekt()
    o_last.location = (DREIBEIN.x, DREIBEIN.y, LAST_Z)
    flasche_oben_ruhe = LAST_Z + flasche + 0.24

    seil = bs.Netz(f"Lastseil{k}", alle=True)
    laenge = FLASCHE_OBEN - flasche_oben_ruhe
    for dy in (-0.05, 0.05):
        seil.stab((0, dy, 0), (0, dy, -laenge), 0.022, 0.022, st["Seil"])
    o_seil = seil.objekt()
    o_seil.location = (DREIBEIN.x, DREIBEIN.y, FLASCHE_OBEN)

    f1, _, f3 = dreibein_fuesse()
    a, b = f1.lerp(spitze, HASPEL), f3.lerp(spitze, HASPEL)
    mitte = (a + b) / 2
    haspel = bs.Netz(f"Haspel{k}", alle=True)
    halb = (b - a).length / 2
    profil = [(HASPEL_R * math.cos(2 * math.pi * j / 8), HASPEL_R * math.sin(2 * math.pi * j / 8)) for j in range(8)]
    haspel.prisma(profil, -halb + 0.1, halb - 0.1, st["Holz"], matrix=Matrix.Rotation(math.radians(90), 4, "Z"))
    for j in range(3):                                                                       # Seilwicklung
        y = -0.1 + 0.07 * j
        haspel.prisma([(x * 1.18, z * 1.18) for x, z in profil], y, y + 0.05, st["Seil"], matrix=Matrix.Rotation(math.radians(90), 4, "Z"))
    for s in (-1, 1):                                                                        # Kurbeln an beiden Enden
        y = s * (halb + 0.06)
        haspel.stab((0, s * (halb - 0.1), 0), (0, y, 0), 0.03, 0.03, st["Eisen"])
        haspel.stab((0, y, 0), (0, y, -0.25), 0.03, 0.03, st["Eisen"])
        haspel.stab((0, y, -0.25), (0, y + s * 0.12, -0.25), 0.035, 0.035, st["Holz"])
    o_haspel = haspel.objekt()
    o_haspel.location = mitte

    def zeit(bild):
        return bild / FPS

    def last_bewegung(bild):
        return (DREIBEIN.x, DREIBEIN.y, LAST_Z + hub(zeit(bild))), None, None

    def seil_bewegung(bild):
        return None, None, (1, 1, (laenge - hub(zeit(bild))) / laenge)

    def haspel_bewegung(bild):
        return None, (0, 2 * hub(zeit(bild)) / HASPEL_R, 0), None                          # zwei Stränge: doppelter Weg

    return [(o_last, last_bewegung), (o_seil, seil_bewegung), (o_haspel, haspel_bewegung)]


# ── weitere Ausstattung des Werkraums ─────────────────────────────────────────

def bruchsteinhaufen(netz, st, mitte, n=10, r=0.8, saat=3, z0=0.0):
    """Haufen grober Bruchsteine in zwei Lagen, zufällig gedreht (Boden in z0)."""
    zufall = random.Random(saat)
    for k in range(n):
        oben = k >= n * 2 // 3
        w = zufall.uniform(0, 2 * math.pi)
        a = zufall.uniform(0, r * (0.45 if oben else 1.0))
        g = zufall.uniform(0.3, 0.52)
        block(netz, st, (mitte[0] + a * math.cos(w), mitte[1] + a * math.sin(w)), (g, g * 0.8, g * 0.6),
              "Fels Bruch" if k % 2 else "Fels Granit", dreh=zufall.uniform(0, 90),
              z0=z0 + (0.2 if oben else 0.0) + 0.002 * k)                       # je Stein 2 mm höher: keine Flächen in einer Ebene


def werktisch(netz, st, M):
    """Tisch unter der Werkzeugwand (örtlich, +y in den Raum): Schablonen aus Brettern, ein Bossierhammer, Kreide."""
    holz = st["Holz"]
    si.q(netz, st, M, (0, 0.38, 0.82), (1.7, 0.7, 0.07), "Holz alt")
    for x in (-0.76, 0.76):
        for y in (0.08, 0.66):
            netz.stab(M @ Vector((x, y, 0)), M @ Vector((x, y, 0.79)), 0.08, 0.08, holz)
    si.q(netz, st, M, (0, 0.38, 0.2), (1.5, 0.6, 0.03), "Holz alt")
    for k, (x, b) in enumerate(((-0.45, 0.5), (0.15, 0.4))):                                # Schablonen mit Profil
        si.q(netz, st, M, (x, 0.35 + 0.08 * k, 0.865), (b, 0.3, 0.012), "Schnittholz")
        si.q(netz, st, M, (x + b / 2 - 0.06, 0.35 + 0.08 * k, 0.873), (0.08, 0.3, 0.012), "Holz")
    si.q(netz, st, M, (0.55, 0.3, 0.88), (0.08, 0.08, 0.05), "Eisen")                     # Bossierhammer
    netz.stab(M @ Vector((0.55, 0.3, 0.88)), M @ Vector((0.6, 0.62, 0.88)), 0.03, 0.03, st["Holz alt"])
    for k in range(3):                                                                      # Steinplatten unter dem Tisch
        si.q(netz, st, M, (-0.3 + 0.45 * k, 0.38, 0.25 + 0.06 * (k % 2)), (0.4, 0.45, 0.06), "Quader grau")


def kluepfel_an_wand(netz, st, M, x, z, laenge=0.3, r=0.06):
    """Klüpfel am Haken: Stiel nach unten, runder Kopf aus Holz."""
    netz.stab(M @ Vector((x, 0.1, z + 0.02)), M @ Vector((x, 0.1, z - laenge)), 0.03, 0.03, st["Holz"])
    bs.achteck(netz, st, M @ Vector((x, 0.1, z - laenge - 0.14)), r, 0.0, 0.14, "Holz alt")


def eisen_an_wand(netz, st, M, x, z, laenge=0.26, breite=0.018):
    """Meißel am Haken: Eisen mit breiterer Schneide unten."""
    netz.stab(M @ Vector((x, 0.1, z + 0.02)), M @ Vector((x, 0.1, z - laenge + 0.04)), 0.02, 0.02, st["Eisen"])
    netz.stab(M @ Vector((x, 0.1, z - laenge + 0.04)), M @ Vector((x, 0.1, z - laenge)), breite, 0.008, st["Eisen"])


def werkzeug1(netz, st, M):
    """Gekauft: Spitz-, Schlag- und Zahneisen an den oberen Haken, Klüpfel und Fäustel an den unteren (linke Hälfte)."""
    for k, (x, z) in enumerate(si.HAKEN):
        if x < 0:
            if z > 2:
                eisen_an_wand(netz, st, M, x, z, laenge=0.24 + 0.03 * (k % 2), breite=(0.018, 0.03, 0.045, 0.03)[k % 4])
            elif k % 2 == 0:
                kluepfel_an_wand(netz, st, M, x, z, r=0.055 + 0.01 * (k % 3))
            else:
                si.hammer_an_wand(netz, st, M, x, z, laenge=0.24, kopf=0.1)               # Fäustel


def werkzeug2(netz, st, M):
    """Gekauft (ab Stufe 2): feines Werkzeug – Scharriereisen und Beizeisen oben, großer Zirkel, Winkel und Richtscheit
    unten (rechte Hälfte)."""
    e = st["Eisen"]
    for k, (x, z) in enumerate(h for h in si.HAKEN if h[0] > 0.01 and h[1] > 2):
        eisen_an_wand(netz, st, M, x, z, laenge=0.22, breite=(0.06, 0.012)[k % 2])
    for s in (-1, 1):                                                                       # Zirkel
        netz.stab(M @ Vector((0.19, 0.1 + s * 0.007, 1.62)), M @ Vector((0.19 + s * 0.09, 0.1 + s * 0.007, 1.2)), 0.018, 0.012, e)
    netz.stab(M @ Vector((0.12, 0.1, 1.42)), M @ Vector((0.26, 0.1, 1.42)), 0.01, 0.008, e)
    netz.stab(M @ Vector((0.42, 0.1, 1.62)), M @ Vector((0.42, 0.1, 1.2)), 0.045, 0.01, e)                 # Winkel
    netz.stab(M @ Vector((0.42, 0.1, 1.22)), M @ Vector((0.62, 0.1, 1.22)), 0.045, 0.01, e)
    netz.stab(M @ Vector((0.76, 0.1, 1.62)), M @ Vector((0.73, 0.1, 0.92)), 0.08, 0.02, st["Schnittholz"])  # Richtscheit


def drehkran(netz, st):
    """Gekauft: kleiner Drehkran mit Flaschenzug neben dem Werkblock – Pfosten mit Fußkreuz, Ausleger mit Strebe, oberer
    Kloben, vier Seile, unterer Kloben mit Haken und Steinzange, das Zugseil zur Klampe am Pfosten. Der Flaschenzug ist
    hochgezogen: Er hängt über dem Kopf des Steinmetzen und verdeckt ihn von den Blickpunkten aus nicht."""
    holz, alt, eisen = st["Fachwerk"], st["Holz alt"], st["Eisen"]
    p = KRAN
    netz.quader((p.x, p.y, 1.8), (0.2, 0.2, 3.6), holz)
    netz.quader((p.x, p.y, 0.06), (0.9, 0.16, 0.12), alt)                                       # Fußkreuz
    for s in (-1, 1):
        netz.quader((p.x, p.y + s * 0.26, 0.05), (0.16, 0.36, 0.1), alt)
    for d in ((0.4, 0), (-0.4, 0), (0, 0.4), (0, -0.4)):
        netz.stab((p.x + d[0], p.y + d[1], 0.1), (p.x + d[0] * 0.2, p.y + d[1] * 0.2, 0.6), 0.07, 0.07, alt)
    alle, netz.alle = netz.alle, True
    z = 3.3
    ende = Vector((WERKBLOCK.x - 0.05, p.y, z))
    netz.stab((p.x + 0.14, p.y, z), ende, 0.16, 0.14, holz, quer=(0, 1, 0))                      # Ausleger (Zapfen durch den Pfosten)
    netz.stab((p.x - 0.1, p.y, z - 0.75), (p.x - 0.65, p.y, z - 0.07), 0.1, 0.1, holz)             # Strebe
    netz.quader((p.x, p.y, 3.63), (0.26, 0.26, 0.06), eisen)                                       # Zapfen oben
    kloben = Vector((ende.x + 0.12, p.y, z - 0.25))
    netz.quader(kloben, (0.12, 0.22, 0.3), st["Holz"])
    netz.stab(kloben + Vector((0, 0, 0.15)), kloben + Vector((0, 0, 0.25)), 0.03, 0.03, eisen)
    unten = kloben + Vector((0, 0, -0.42))
    for dx in (-0.04, 0.04):
        for dy in (-0.05, 0.05):
            netz.stab(kloben + Vector((dx, dy, -0.14)), unten + Vector((dx, dy, 0.1)), 0.016, 0.016, st["Seil"])
    netz.quader(unten, (0.1, 0.18, 0.2), st["Holz"])
    netz.stab(unten + Vector((0, 0, -0.1)), unten + Vector((0, 0, -0.18)), 0.025, 0.025, eisen)
    netz.stab(unten + Vector((0, 0, -0.18)), unten + Vector((0.05, 0, -0.22)), 0.025, 0.025, eisen)    # Haken
    for s in (-1, 1):                                                                              # Steinzange, geschlossen
        y = s * 0.016
        netz.stab(unten + Vector((0, y, -0.22)), unten + Vector((s * 0.12, y, -0.45)), 0.035, 0.025, eisen, quer=(0, 1, 0))
        netz.stab(unten + Vector((s * 0.12, y, -0.45)), unten + Vector((s * 0.05, y, -0.56)), 0.035, 0.025, eisen, quer=(0, 1, 0))
    netz.stab(kloben + Vector((0.06, 0, -0.1)), Vector((p.x - 0.1, p.y, 1.3)), 0.016, 0.016, st["Seil"])   # Zugseil
    netz.quader((p.x - 0.11, p.y, 1.3), (0.04, 0.12, 0.04), alt)                                   # Klampe
    netz.alle = alle


# ─────────────────────────────────────────────────────────────────────────────
#  Steinlager
# ─────────────────────────────────────────────────────────────────────────────

def quaderlager(netz, st, x0, y, z):
    """Behauene Quader auf Kanthölzern am hinteren Kniestock (ab x0, Mitte bei y, Boden in z): zwei Lagen, oben versetzt,
    dazu eine Platte auf dem Stapel – mit dem Kranbalken heraufgezogen."""
    for k in range(3):
        kanthoelzer(netz, st, (x0 + 0.95 * k, y), 0.8, 0.5, dreh=0, h=0.1, z0=z)
    for lage, (dx, n) in enumerate(((0.0, 3), (0.45, 2))):
        for i in range(n):
            mitte = (x0 + dx + 0.95 * i, y + 0.02 * ((i + lage) % 2))
            block(netz, st, mitte, (0.86, 0.62, 0.44), "Quader" if (i + lage) % 3 else "Quader grau", z0=z + 0.1 + 0.44 * lage)
    block(netz, st, (x0 + 0.9, y - 0.05), (1.1, 0.5, 0.08), "Quader grau", z0=z + 0.98, dreh=4)


def kisten(netz, st, mitte):
    """Kisten für fertige Ware um `mitte` (Boden in mitte.z), darauf ein Mörser und ein kleiner Weihwasserstein."""
    si.kisten(netz, st, mitte)
    moerser = mitte + Vector((0.0, 0.05, 1.05))
    bs.achteck(netz, st, moerser, 0.16, 0.0, 0.18, "Quader")
    bs.rund_oben(netz, st, moerser, 0.11, moerser.z + 0.181, "Loch")
    stein = mitte + Vector((0.05, -0.85, 0.45))
    bs.achteck(netz, st, stein, 0.13, 0.0, 0.12, "Quader grau")
    bs.achteck(netz, st, stein, 0.2, 0.12, 0.24, "Quader grau")
    bs.rund_oben(netz, st, stein, 0.15, stein.z + 0.241, "Loch")


def steingestell(netz, st, x0, x1):
    """Gekauft: niedriges, schweres Steingestell am vorderen Kniestock (von x0 bis x1, unter der Dachschräge) – Ständer aus
    Kantholz, drei Böden aus Bohlen, darauf Platten, Gesimsstücke und kleine Quader."""
    z, y = Z_LAGER, WY0 + 0.25
    for x in (x0, (x0 + x1) / 2, x1):
        netz.quader((x, y, z + 0.55), (0.12, 0.12, 1.1), st["Holz"])
        netz.quader((x, y, z + 0.06), (0.14, 0.4, 0.12), st["Holz alt"])
    zufall = random.Random(5)
    for k, h in enumerate((0.25, 0.6, 0.95)):
        netz.quader(((x0 + x1) / 2, y, z + h), (x1 - x0 + 0.1, 0.42, 0.06), st["Holz alt"])
        if k == 2:
            continue
        x = x0 + 0.12
        while x < x1 - 0.3:
            art = zufall.choice(("platte", "quader", "gesims"))
            b = zufall.uniform(0.3, 0.55)
            if x + b > x1 - 0.08:
                break
            if art == "platte":
                for j in range(3):
                    netz.quader((x + b / 2, y, z + h + 0.05 + 0.045 * j), (b, 0.36, 0.04), st["Quader grau" if j % 2 else "Quader"])
            elif art == "quader":
                netz.quader((x + b / 2, y, z + h + 0.15), (b, 0.32, 0.24), st["Quader"])
            else:
                netz.prisma([(-0.16, 0.03), (0.16, 0.03), (0.16, 0.1), (0.08, 0.16), (-0.16, 0.16)], x, x + b, st["Quader"],
                            matrix=Matrix.Translation((0, y, z + h)))
            x += b + zufall.uniform(0.05, 0.12)
    for x, b in ((x0 + 0.2, 0.3), (x1 - 0.35, 0.4)):                                        # oben nur ein paar Platten
        netz.quader((x + b / 2, y, z + 0.95 + 0.05), (b, 0.34, 0.04), st["Quader grau"])


def lagersteine(netz, st, z):
    """Rohblöcke bei der Ladeluke, ein Haufen Bruchsteine und an den linken Giebel gelehnte Grabplatten (Boden in z)."""
    block(netz, st, (0.8, -2.0), (0.9, 0.6, 0.55), "Fels Granit", dreh=6, z0=z)
    block(netz, st, (0.7, -1.95), (0.6, 0.45, 0.35), "Fels Sandstein", dreh=-10, z0=z + 0.55)
    bruchsteinhaufen(netz, st, (-0.4, -2.5), n=8, r=0.45, saat=7, z0=z)
    for k in range(3):
        y = -0.9 + 0.55 * k
        netz.stab((WX0 + 0.28 - 0.03 * k, y, z), (WX0 + 0.06, y, z + 1.25 - 0.1 * k), 0.5, 0.1,
                  st["Quader grau" if k % 2 else "Quader"], quer=(1, 0, 0.2))


# ─────────────────────────────────────────────────────────────────────────────
#  Bauhütte
# ─────────────────────────────────────────────────────────────────────────────

FIGUR_OBEN = fg.ARBEITSHOEHEN["Figur"]       # (vor, Höhe): Schneide des Meißels an der Flanke der Figur


def steinfigur(netz, st):
    """Steinfigur in Arbeit auf einem niedrigen Arbeitsbock: unten noch der rohe Block, darüber Gewand, gefaltete Hände,
    Schultern und Kopf. Ihre rechte Flanke (zum Bildhauer, +x) steht senkrecht dort, wo die Schneide des Meißels sitzt."""
    vor, _ = FIGUR_OBEN
    flanke = BILDHAUER.x - vor                                                               # x der Flanke
    r = 0.24
    mitte = Vector((flanke - r * math.cos(math.radians(22.5)), BILDHAUER.y, 0))
    netz.quader((mitte.x, mitte.y, 0.22), (0.95, 0.8, 0.44), st["Holz alt"])                  # Arbeitsbock
    for dx in (-0.3, 0.3):
        netz.quader((mitte.x + dx, mitte.y, 0.22), (0.12, 0.82, 0.446), st["Fachwerk"], ohne=("-z",))
    block(netz, st, mitte, (0.78, 0.7, 0.5), "Fels Sandstein", z0=0.44)
    oben = 0.94
    bs.achteck(netz, st, mitte, r * 1.06, oben, oben + 0.08, "Quader")                     # Saum
    bs.achteck(netz, st, mitte, r, oben + 0.08, 1.48, "Quader")                              # Gewand (die Flanke ist senkrecht)
    ringe = [(1.48, r), (1.6, r * 0.92), (1.66, r * 0.55)]
    for (z0, r0), (z1, r1) in zip(ringe, ringe[1:]):                                          # Schultern
        teile = 8
        for k in range(teile):
            w0, w1 = math.radians(22.5 + 45 * k), math.radians(22.5 + 45 * (k + 1))
            quad = [mitte + Vector((r0 * math.cos(w0), r0 * math.sin(w0), z0)), mitte + Vector((r0 * math.cos(w1), r0 * math.sin(w1), z0)),
                    mitte + Vector((r1 * math.cos(w1), r1 * math.sin(w1), z1)), mitte + Vector((r1 * math.cos(w0), r1 * math.sin(w0), z1))]
            netz.vieleck_nach(quad, sum(quad, Vector()) / 4 - (mitte + Vector((0, 0, (z0 + z1) / 2))), st["Quader"])
    bs.achteck(netz, st, mitte, r * 0.55, 1.66, 1.72, "Quader")
    bs.achteck(netz, st, mitte + Vector((0, -0.01, 0)), 0.075, 1.72, 1.77, "Quader")          # Hals
    kopf = [(1.77, 0.08), (1.83, 0.11), (1.92, 0.11), (1.99, 0.07)]
    for (z0, r0), (z1, r1) in zip(kopf, kopf[1:]):
        bs.achteck(netz, st, mitte + Vector((0, -0.01, 0)), (r0 + r1) / 2, z0, z1, "Quader")
    netz.vieleck_nach([mitte + Vector((0.07 * math.cos(2 * math.pi * k / 8), -0.01 + 0.07 * math.sin(2 * math.pi * k / 8), 1.991))
                       for k in range(8)], (0, 0, 1), st["Quader"])
    a = r * math.cos(math.radians(22.5))                                                       # Abstand der flachen Seiten
    netz.quader(mitte + Vector((0, -a - 0.04, 1.36)), (0.16, 0.08, 0.16), st["Quader"])          # gefaltete Hände
    for s in (-1, 1):                                                                          # Unterarme vor der Brust
        netz.stab(mitte + Vector((s * 0.17, -a + 0.02, 1.5)), mitte + Vector((s * 0.05, -a - 0.03, 1.38)), 0.07, 0.07, st["Quader"])
    for k in range(4):                                                                         # Falten am Saum (vorn)
        x = -0.13 + 0.085 * k
        netz.quader(mitte + Vector((x, -a - 0.008, 1.12)), (0.025, 0.02, 0.32), st["Quader"], ohne=("+y",))
    return mitte


def taufbecken(netz, st, boden):
    """Achteckiges Taufbecken: Stufe, Fuß, Schaft und Becken mit dunkler Mulde."""
    bs.achteck(netz, st, boden, 0.52, 0.0, 0.14, "Quader grau")
    bs.achteck(netz, st, boden, 0.3, 0.14, 0.3, "Quader")
    bs.achteck(netz, st, boden, 0.2, 0.3, 0.72, "Quader")
    bs.achteck(netz, st, boden, 0.58, 0.72, 1.04, "Quader")
    bs.rund_oben(netz, st, boden, 0.47, 1.043, "Loch")


def grabmal(netz, st, fuss):
    """Grabmal (Epitaph) an der Wand: Sockel, hohe Platte mit Giebel und eingetieftem Feld, Blick +x."""
    netz.quader(fuss + Vector((0, 0, 0.15)), (0.55, 1.1, 0.3), st["Quader grau"])
    profil = [(-0.4, 0.3), (0.4, 0.3), (0.4, 1.75), (0.0, 2.05), (-0.4, 1.75)]
    netz.prisma(profil, fuss.x - 0.12, fuss.x + 0.12, st["Quader grau"], matrix=Matrix.Translation((0, fuss.y, 0)))
    netz.vieleck_nach([fuss + Vector((0.121, y, z)) for y, z in ((-0.28, 0.5), (0.28, 0.5), (0.28, 1.6), (-0.28, 1.6))], (1, 0, 0),
                      st["Fels Granit"])
    netz.quader(fuss + Vector((0.13, 0, 1.68)), (0.04, 0.2, 0.06), st["Quader grau"], ohne=("-x",))


def geruestbock(netz, st, mitte):
    """Gerüstbock: zwei Böcke mit gespreizten Beinen, zwei Bohlen darauf in 1,5 m Höhe, eine Leiter lehnt daran."""
    alt, holz = st["Holz alt"], st["Fachwerk"]
    for dx in (-0.9, 0.9):
        for sy in (-1, 1):
            for sx in (-1, 1):
                netz.stab(mitte + Vector((dx + sx * 0.25, sy * 0.35, 0)), mitte + Vector((dx + sx * 0.05, sy * 0.3, 1.45)), 0.08, 0.08, holz)
        netz.quader(mitte + Vector((dx, 0, 1.47)), (0.18, 0.8, 0.08), holz)
        netz.stab(mitte + Vector((dx - 0.2, -0.33, 0.6)), mitte + Vector((dx + 0.2, -0.33, 0.6)), 0.06, 0.03, holz)
    for dy in (-0.15, 0.15):
        netz.quader(mitte + Vector((0, dy, 1.53)), (2.3, 0.28, 0.05), alt)
    for s in (-1, 1):                                                                            # Leiter
        netz.stab(mitte + Vector((-1.45, s * 0.2, 0)), mitte + Vector((-1.05, s * 0.2, 1.75)), 0.06, 0.05, alt)
    for k in range(1, 6):
        z = 0.3 * k
        x = -1.45 + 0.4 * z / 1.75
        netz.stab(mitte + Vector((x, -0.2, z)), mitte + Vector((x, 0.2, z)), 0.04, 0.04, alt)
    eimer = mitte + Vector((0.5, 0.05, 1.555))
    bs.achteck(netz, st, eimer, 0.12, 0.0, 0.22, "Holz alt")
    bs.rund_oben(netz, st, eimer, 0.105, 1.555 + 0.221, "Steinstaub")


def huette_ausstattung(netz, st):
    """Ausstattung der Bauhütte außer der Figur: liegende Säule auf Kanthölzern, Taufbecken, Grabmal, Gerüstbock, Ladetor,
    Schablonen an der Wand."""
    r = 0.21
    for y in (5.1, 8.27):
        netz.quader((2.95, y, 0.06), (0.9, 0.14, 0.12), st["Holz alt"])
    steinmetz.saeule(netz, st, (2.95, 5.0, 0.12 + 1.5 * r), (2.95, 8.4, 0.12 + 1.5 * r), r=r, stoff="Quader")
    taufbecken(netz, st, Vector((-2.7, 5.3, 0)))
    grabmal(netz, st, Vector((HX0 + 0.42, 8.3, 0)))
    geruestbock(netz, st, Vector((1.9, 9.25, 0)))
    tor(netz, st, si.rahmen(LADETOR_X, HY1, 180), LADETOR_B, LADETOR_H)
    M = si.rahmen(HX1 - 0.12, 4.95, 90)                                                         # Schablonen an der rechten Wand
    si.q(netz, st, M, (0, 0.03, 2.3), (1.6, 0.06, 0.08), "Fachwerk", ohne=("-y",))
    for k, (x, b, h) in enumerate(((-0.55, 0.35, 0.7), (-0.05, 0.45, 0.5), (0.5, 0.3, 0.8))):
        si.q(netz, st, M, (x, 0.07, 2.27 - h / 2), (b, 0.015, h), "Schnittholz")
        si.q(netz, st, M, (x + b / 2 - 0.05, 0.08, 2.27 - h / 2), (0.1, 0.015, h * 0.7), "Holz")


def steinbank(netz, st, mitte):
    """Kleine Bank aus zwei Steinen und einer Bohle neben der Figur (für das Werkstück), darunter Meißel."""
    for dx in (-0.3, 0.3):
        block(netz, st, (mitte.x + dx, mitte.y), (0.18, 0.4, 0.42), "Fels Sandstein")
    netz.quader((mitte.x, mitte.y, 0.45), (0.85, 0.42, 0.06), st["Holz alt"])
    for k in range(3):
        netz.stab((mitte.x - 0.15 + 0.1 * k, mitte.y - 0.25, 0.012), (mitte.x - 0.1 + 0.1 * k, mitte.y - 0.05, 0.012), 0.02, 0.02,
                  st["Eisen"])


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen
# ─────────────────────────────────────────────────────────────────────────────

def bauen():
    import bpy
    bs.neue_szene()
    _lichter[0] = 0
    st = bs.Stoffe()
    werk, lager, huette = bs.Netz("Raum_Werk"), bs.Netz("Raum_Lager", alle=True), bs.Netz("Raum_Huette")
    decke_w, decke_l = bs.Netz("Decke_Werk", alle=True), bs.Netz("Decke_Lager", alle=True)
    decke_h = bs.Netz("Decke_Huette", alle=True)
    kran, himmel = bs.Netz("Kranbalken", alle=True), bs.Netz("Himmel_Lager")
    teile = {}                    # Name → (Netz, Raum)

    def teil(name, raum="Werk"):
        if name not in teile:
            teile[name] = (bs.Netz(name), raum)
        return teile[name][0]

    anker = []                    # (Objekt, Raum)

    def setzen(o, raum="Werk", stufe=None):
        if stufe is not None:
            o["stufe"] = stufe
        anker.append((o, raum))

    # ── Werkraum: Hülle ───────────────────────────────────────────────────────
    W = WERK_OEFFNUNGEN
    si.steinraum(werk, decke_w, lager, st, WX0, WX1, WY0, WY1, H_WERK, {
        "vorne": dict(oeffnungen=W["vorne"]),
        "rechts": dict(oeffnungen=W["rechts"]),
        "hinten": dict(oeffnungen=W["hinten"], aussen=False),
        "links": dict(oeffnungen=W["links"]),
    }, luke=LUKE, saat=3)
    fenster_liste = []
    fenster_und_licht(werk, st, ecken(WX0, WX1, WY0, WY1), W, fenster_liste)
    for o in fenster_liste:
        setzen(o)
    werkblock(teil("Bedienung_Produktion_Werkblock"), st)
    schleifstein_fest(werk, st)
    kanthoelzer(werk, st, DREIBEIN, 0.75, 0.5, dreh=0)
    bruchsteinhaufen(werk, st, (-3.25, 3.0), n=10, r=0.55, saat=3)
    ti.laterne(werk, st, Vector((0.6, 0.0, 2.95)), H_WERK - 0.26)
    setzen(licht(Vector((0.6, 0.0, 2.8)), (0, 0, -1), "Lampe"))

    # ── Werkraum: Bedienung, Türen, Einrichtung ──────────────────────────────
    si.pult(teil("Bedienung_Personal_Buch"), st, si.rahmen(WX0 + 0.05, -2.6, -90))
    M_wand = si.rahmen(WX1, 0.6, 90)
    werktisch(werk, st, M_wand)
    si.werkzeugwand(teil("Bedienung_Einrichten_Werkzeugwand"), st, M_wand)
    werkzeug1(teil("Einrichtung_Werkzeug1"), st, M_wand)
    werkzeug2(teil("Einrichtung_Werkzeug2"), st, M_wand)
    drehkran(teil("Einrichtung_Flaschenzug"), st)
    tor(teil("Bedienung_Karren_Hoftor"), st, si.rahmen(WX0, HOFTOR_Y, -90), HOFTOR_B, HOFTOR_H)
    M_holz = si.rahmen(HOLZTUER_X, WY0, 0)
    si.bretttuer(teil("Holztuer"), st, M_holz, HOLZTUER_B, HOLZTUER_H)
    si.tuerstock(werk, st, M_holz, HOLZTUER_B, HOLZTUER_H)
    si.eisentuer(teil("Einrichtung_Eisentuer"), st, M_holz, HOLZTUER_B, HOLZTUER_H)
    si.loeschfass(teil("Einrichtung_Loeschfass"), st, Vector((-0.15, -3.05, 0)))
    ti.schuerzen(teil("Einrichtung_Schuerzen"), st, si.rahmen(-0.75, WY0, 0))
    ia.treppe(teil("Tuer_Werk_Lager"), st, TREPPE_X, TREPPE_FUSS, TREPPE_OBEN, Z_LAGER)       # Treppe hinauf ins Steinlager
    M_th = si.rahmen(HUETTENTUER_X, WY1, 180)
    si.bretttuer(teil("Tuer_Werk_Huette"), st, M_th, HUETTENTUER_B, HUETTENTUER_H, stoff="Brett")
    si.tuerstock(teil("Tuer_Werk_Huette"), st, M_th, HUETTENTUER_B, HUETTENTUER_H)
    si.bretttuer(teil("Tuer_Huette_Werk", "Huette"), st, si.rahmen(HUETTENTUER_X, HY0, 0), HUETTENTUER_B, HUETTENTUER_H,
                 stoff="Brett")

    # ── Werkraum: Arbeitsplätze ──────────────────────────────────────────────
    vor, oben = fg.ARBEITSHOEHEN["Block"]
    behauer = Vector((WERKBLOCK.x - 0.03, WERKBLOCK.y + BLOCK[1] / 2 - 0.06 + vor, 0))       # Schneide 6 cm hinter der Kante
    setzen(si.arbeit("Arbeit_1_Behauen", behauer, (0, -1), 1))
    setzen(bs.anker("Werkstueck_1", (WERKBLOCK.x - 0.05, WERKBLOCK.y - 0.05, oben + 0.04)))
    setzen(si.arbeit("Arbeit_2_Schleifen", SCHLEIF + Vector((-0.6, 0, 0)), (1, 0), 1))
    setzen(bs.anker("Werkstueck_2", SCHLEIF + Vector((0.5, 0.0, 0.48))))
    saeger = si.arbeit("Arbeit_3_Saegen", SAEGER, (1, 0), 2)
    saeger["requisit"] = "keins"
    setzen(saeger)
    h0 = saeger_hand(0.0)
    setzen(bs.anker("Werkstueck_3", (h0.x + 0.95, SAEGE_Y + 0.2, STEIN_OBEN + 0.04)), stufe=2)

    # ── Stufen (kumulativ) und bewegte Teile ─────────────────────────────────
    stufen = [bs.Netz(f"Stufe{k}") for k in (1, 2, 3)]
    ziegel = 0
    for k, netz in enumerate(stufen, 1):
        if k < 3:
            ia.lehmboden(netz, st, WX0, WX1, WY0, WY1, saat=9)
        else:
            ziegel = ia.fischgraet(netz, st, WX0, WX1, WY0, WY1, saat=9)
        if k <= 2:
            vermauert(netz, st)
        if k == 1:
            block(netz, st, DREIBEIN, LAST, "Fels Granit", z0=LAST_Z)                        # Rohblock, noch ohne Dreibein
        if k >= 2:
            steinsaege_fest(netz, st)
            dreibein_fest(netz, st)
    stufen_obj = [s.objekt() for s in stufen]
    bewegt = schleifstein_teile(st)
    raum_bewegt = [o for o, _ in bewegt]
    for k in (2, 3):
        for o, bewegung in steinsaege_teile(st, k) + dreibein_teile(st, k):
            o.parent = stufen_obj[k - 1]
            o.matrix_parent_inverse = Matrix.Identity(4)
            bewegt.append((o, bewegung))
    spaene_obj = [spaene_stand(m, st) for m in (1, 2, 3)]

    print(f"Backsteinpflaster: {ziegel} Ziegel")

    # ── Steinlager unter dem Dach ─────────────────────────────────────────────
    z = Z_LAGER
    kanten = ecken(WX0, WX1, WY0, WY1)
    DACH.kniestoecke(lager, st, "Kalkputz")
    luke = Oe(LADELUKE_Y, 0.0, 1.2, 1.8)                      # Höhen über dem Dachboden
    gfenster = Oe(0.0, 2.3, 0.6, 0.6)
    DACH.giebel(lager, st, False, [gfenster], "Kalkputz", WAND)
    DACH.giebel(lager, st, True, [luke], "Kalkputz", WAND)
    for i, (seite, oeff) in enumerate((("vorne", []), ("hinten", []), ("links", [gfenster]), ("rechts", [luke]))):
        ia.durchbruch(lager, st, *kanten[seite], DACH.kniestock, oeff, saat=40 + 7 * i, boden=z, ecke_hoch=(0.8, 1.25),
                      frei=[(0.0, 0.9)] if seite == "hinten" else
                      [(LUKE[2] - WY0 - 0.15, LUKE[3] - WY0 + 0.15)] if seite == "rechts" else ())        # nicht über der Bodenluke
    DACH.dach(decke_l, st)
    licht_luke = DACH.ladeluke(lager, kran, himmel, st, LADELUKE_Y, luke.b, luke.h, WAND)
    si.fenster(lager, st, *kanten["links"], Oe(gfenster.s, z + gfenster.v, gfenster.b, gfenster.h))
    ia.bodenluke(teil("Tuer_Lager_Werk", "Lager"), st, LUKE, z, gelaender_x=LUKE[0] - 0.05)
    setzen(licht(licht_luke, (-1, 0, 0), "Fenster"), "Lager")
    setzen(licht(Vector((WX0 + 0.1, gfenster.s, z + gfenster.v + gfenster.h / 2)), (1, 0, 0), "Fenster"), "Lager")
    quaderlager(teil("Bedienung_Lager_Quader", "Lager"), st, -3.3, WY1 - 0.5, z)                 # hinten
    kisten(teil("Bedienung_Lager_Kisten", "Lager"), st, Vector((3.2, -1.95, z)))                  # bei der Ladeluke
    steingestell(teil("Einrichtung_Regale", "Lager"), st, -3.6, -1.1)                             # vorn, niedrig
    lagersteine(lager, st, z)
    nummer = 1
    for y in (1.5, 0.55):
        for x in (-1.6, -0.6, 0.4, 1.4):
            setzen(bs.anker(f"Lager_{nummer}", (x, y, z)), "Lager")
            nummer += 1

    # ── Bauhütte (ab Stufe 3) ─────────────────────────────────────────────────
    H = HUETTEN_OEFFNUNGEN
    kanten = ecken(HX0, HX1, HY0, HY1)
    huette.alle = True
    si.wand(huette, st, *kanten["vorne"], H_HUETTE, H["vorne"], stoff="Bruch", aussen=False, laibung=False, fugen=True, saat=9)
    bretterwand(huette, st, *kanten["rechts"], H_HUETTE, H["rechts"], enden=(False, True))
    bretterwand(huette, st, *kanten["hinten"], H_HUETTE, H["hinten"], eckpfosten=(False, False))
    bretterwand(huette, st, *kanten["links"], H_HUETTE, H["links"], enden=(True, False))
    huette.alle = False
    ia.balkendecke(decke_h, st, HX0, HX1, HY0, HY1, H_HUETTE, abdeckung=BRETTWAND, rand_hinten=0.14, saat=11)
    fenster_liste = []
    fenster_und_licht(huette, st, kanten, {s: l for s, l in H.items() if s != "vorne"}, fenster_liste, stufe=3, mauer=False)
    for o in fenster_liste:
        setzen(o, "Huette")
    ia.lehmboden(huette, st, HX0, HX1, HY0, HY1, saat=12, flecken=12)
    steinfigur(huette, st)
    huette_ausstattung(huette, st)
    steinbank(huette, st, Vector((-1.15, BILDHAUER.y, 0)))
    for x, y, r in ((BILDHAUER.x - 0.35, BILDHAUER.y + 0.05, 0.55), (2.95, 6.6, 0.45)):
        splitter(huette, st, Vector((x, y, 0)), r, r * 0.8, 3, int(x * 10 + y), 0.011)
    ti.laterne(huette, st, Vector((-0.6, 7.2, 3.55)), H_HUETTE - 0.26)
    setzen(licht(Vector((-0.6, 7.2, 3.4)), (0, 0, -1), "Lampe", 3), "Huette")
    setzen(si.arbeit("Arbeit_4_Bildhauen", BILDHAUER, (-1, 0), 3), "Huette")
    setzen(bs.anker("Werkstueck_4", (-1.15, BILDHAUER.y, 0.52)), "Huette", stufe=3)

    # ── Blickpunkte ──────────────────────────────────────────────────────────
    for name, (auge, ziel) in BLICKE.items():
        raum = name.split("_")[1]
        setzen(si.blick(name, auge, ziel), raum, stufe=3 if raum == "Huette" else None)

    # ── Objekte, Zustände, Hierarchie ─────────────────────────────────────────
    raeume = {"Werk": werk.objekt(), "Lager": lager.objekt(), "Huette": huette.objekt()}
    raeume["Huette"]["stufe"] = 3
    kinder = {r: [] for r in raeume}
    kinder["Werk"] += [decke_w.objekt()] + stufen_obj + spaene_obj + raum_bewegt
    kinder["Lager"] += [decke_l.objekt(), kran.objekt(), himmel.objekt()]
    kinder["Huette"].append(decke_h.objekt())
    for name, (netz, raum) in teile.items():
        o = netz.objekt()
        kinder[raum].append(o)
        if name.startswith("Einrichtung_"):
            bs.zustand(o)
        if name in ("Tuer_Werk_Huette", "Tuer_Huette_Werk"):
            o["stufe"] = 3
        if name in SCHILDER:
            o["schild"] = SCHILDER[name]
    for o, raum in anker:
        kinder[raum].append(o)
    for raum, liste in kinder.items():
        for o in liste:
            o.parent = raeume[raum]
            o.matrix_parent_inverse = Matrix.Identity(4)
    stufen_obj[0]["zustand"] = True
    for o in stufen_obj[1:]:
        bs.zustand(o)
    ti.animieren(bewegt, ARBEIT_BILDER)
    bpy.context.view_layer.update()
    return list(raeume.values()) + [o for liste in kinder.values() for o in liste]


def main():
    objekte = bauen()
    vorher, nachher = ia.farben_zusammenlegen(objekte)
    print(f"Materialien: {vorher} → {nachher}")
    ia.pruefbericht(objekte, NAME)
    print("geschrieben:", ti.exportieren(objekte, NAME))
    if "--ohne-bilder" in sys.argv:
        return
    ia.vorschau(NAME, list(BLICKE), STUFEN, "--schnell" in sys.argv, (WX0 - WAND, WX1 + WAND, WY0 - WAND, HY1 + BRETTWAND),
                (WX0 - WAND, WX1 + WAND, WY0 - WAND, WY1 + WAND), DACH, {"Werk", "Huette"}, bild=1 + int(1.6 * FPS), lampe=80,
                fuell=[(0, 0, H_WERK - 0.4, 170, 1), (0, 0.3, Z_LAGER + 3.0, 90, 1), (0, (HY0 + HY1) / 2, H_HUETTE - 0.4, 170, 3)])


if __name__ == "__main__":
    main()
