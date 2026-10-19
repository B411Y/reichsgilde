"""Innenraum des Tischlers: Werkraum, Holzlager und ab dem Sägewerk die Sägehalle; Stufen Tischlerei, Drechslerei, Sägewerk.

    blender -b --python tools/modelle/tischler_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/tischler_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/tischler_innen.glb und Bilder nach docs/modelle/ (tischler_innen-blicke.png: das Bild jedes
Blickpunkts, tischler_innen-stufen.png: Grundriss je Stufe, tischler_innen-grundriss.png: Grundriss mit Ankern).

Koordinaten wie beim Schmied (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte des
Werkraums auf dem Boden. Fachwerkwände 0,3 m dick, innen verputzt, Dielenboden. Werkraum innen x −4 … 4, y −3,5 … 3,5,
3,4 m hoch; Holzlager rechts daneben x 4,3 … 9,3, y −1,5 … 3,5, 3,0 m hoch; Sägehalle hinter dem Werkraum x −4 … 4,
y 3,8 … 9,8, 4,2 m hoch. Alle Räume sind rundum geschlossen, die Kamera steht im Raum.

Knoten im GLB – dieselben Regeln wie in schmied_innen.glb (Raum_…, Decke_…, Stufe1–Stufe3, Bedienung_…, Tuer_…,
Einrichtung_…, Holztuer, Blick_…, Arbeit_…, Lager_…, Werkstueck_…, Licht_…), dazu:
    Raum_Saege                  die Sägehalle, extras „stufe“ = 3: das Spiel zeigt sie erst ab dem Sägewerk
    Tuer_Werk_Saege,            Tür zwischen Werkraum und Sägehalle, extras „stufe“ = 3 (vorher unsichtbar und nicht
    Tuer_Saege_Werk             anklickbar); in Stufe1 und Stufe2 schließt ein verputztes Gefach die Öffnung im Werkraum
    Spaene1–Spaene3             Hobelspäne unter der Hobelbank und Sägemehl unter dem Sägebock in drei Mengen; jeder Knoten
                                ist ein ganzer Stand, alle sind standardmäßig aus (das Spiel zeigt höchstens einen). Darunter
                                je ein Kind „Spaene<n>_Drechselbank“ mit extras „stufe“ = 2: die Späne unter der Drechselbank
    Spindel2, Federstange2, Pedal2, SchnurOben2, SchnurUnten2 (unter Stufe2) und dieselben mit 3 (unter Stufe3):
                                bewegte Teile der Wippdrehbank, Ursprung im Drehpunkt
    Gatterrahmen, Welle, Pleuel bewegte Teile der Gattersäge (unter Raum_Saege)
Animation „Arbeit“ (2 s, als Schleife zu spielen, solange gearbeitet wird): Die Drehbank läuft zweimal (Pedal treten,
die Federstange zieht zurück, das Werkstück dreht hin und her), die Welle dreht sich zweimal und hebt und senkt dabei den
Gatterrahmen. Ruht der Betrieb, steht alles in der Lage der Datei (Pedal oben, Gatterrahmen oben).
Lichter: extras „art“ = „Fenster“ wie beim Schmied, dazu „Lampe“ (Laterne an der Decke, Blick −Z nach unten). Glut gibt
es nicht; das Material „Glut“ tragen Laternen und Leimofen (leuchten, solange gearbeitet wird).
Materialien: „Farben“ (Eckenfarben), „Hausfarbe“ (Hoftor, Holztür, Ladetor), „Fenster“, „Glut“, „Eisen“, „Wasser“.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import schmied_innen as si  # noqa: E402  (Wände, Fenster, Dielen, Pult, Türen, Werkzeugwand, Kisten, Löschfass, Anker)
import tischler  # noqa: E402  (Rundholz wie außen)

NAME = "tischler_innen"
STUFEN = ("Tischlerei", "Drechslerei", "Sägewerk")

bs.FARBEN.update({
    "Spaene": (0.82, 0.70, 0.50),          # frische Hobelspäne
    "Saegemehl": (0.72, 0.60, 0.43),
    "Leim": (0.36, 0.23, 0.10),            # Knochenleim im Topf
    "Ziegel": (0.52, 0.30, 0.22),          # Leimofen
})

# ── Maße ─────────────────────────────────────────────────────────────────────
WAND = 0.3                                                         # Fachwerkwand mit Putz
WX0, WX1, WY0, WY1 = -4.0, 4.0, -3.5, 3.5                           # Werkraum innen
H_WERK = 3.4
LX0, LX1, LY0, LY1 = WX1 + WAND, WX1 + WAND + 5.0, -1.5, WY1        # Holzlager innen
H_LAGER = 3.0
SX0, SX1, SY0, SY1 = WX0, WX1, WY1 + WAND, WY1 + WAND + 6.0         # Sägehalle innen
H_SAEGE = 4.2
UEBER = si.UEBER                                                   # Außenseite der Wände reicht so weit über die Decke
TUER_Y, TUER_B, TUER_H = 2.5, 1.0, 2.1                             # Tür Werkraum – Lager (rechte Wand)
SAEGETUER_X, SAEGETUER_B, SAEGETUER_H = 2.8, 1.1, 2.2              # Tür Werkraum – Sägehalle (Rückwand)
HOFTOR_Y, HOFTOR_B, HOFTOR_H = -0.6, 2.6, 2.8                      # Hoftor in der linken Wand
HOLZTUER_X, HOLZTUER_B, HOLZTUER_H = -2.0, 0.95, 2.1              # Tür zum Hof in der Vorderwand
SICHTFELD = 50.0                                                   # Grad, senkrecht; Bild 16:9
FPS = 24
TAKT = 24                      # Bilder je Takt der Drehbank und je Umdrehung der Welle (1 s)
ARBEIT_BILDER = 2 * TAKT       # Länge der Animation „Arbeit“ (2 s)

# Arbeitsplätze – die Höhen passen zu figuren.ARBEITSHOEHEN (Hobel, Bock, Drehbank, Stamm) und figuren.PEDAL
HOBELBANK = Vector((0.4, 1.6, 0))          # Mitte, Länge entlang x; gehobelt wird auf der Seite +y
BANK_HOEHE = 0.86                          # Oberkante der Platte; darauf ein Brett (4 cm), darauf der Hobel
SAEGEBOCK = Vector((2.1, 0.2, 0))          # Mitte; der Stamm liegt entlang x
STAMM_Z, STAMM_R = 0.85, 0.17              # Achse und Halbmesser des Stamms auf dem Bock (Oberkante 1,02)
DREHBANK = Vector((-2.55, 1.25, 0))        # Mitte des Betts; die Spindel liegt entlang x
SPINDEL_Z = 1.05                           # Achse der Spindel
SPINDEL_X0, SPINDEL_X1 = -3.0, -2.25       # Werkstück zwischen den Spitzen
SCHNUR_X = -2.59                           # Schnur der Federstange: unter dem rechten Fuß des Drechslers
SCHNUR_R = 0.035                           # Halbmesser des Werkstücks, wo die Schnur läuft
FEDER_DREH = Vector((-0.5, DREHBANK.y + 0.045, 3.05))   # Drehpunkt der Federstange (am Deckenbalken x −0,5)
FEDER_SPITZE_Z = 2.78                      # Achse der Federstange über der Schnur; die Schnur hängt 3 cm darunter
PEDAL_DREH = Vector((SCHNUR_X, 0.6, 0.06)) # Drehpunkt des Pedals am Boden; das Pedal reicht 1 m nach +y
PEDAL_OBEN, PEDAL_UNTEN = 0.28, 0.01       # Spitze des Pedals über dem Drehpunkt: in Ruhe und getreten
GATTER = Vector((-0.3, 7.0, 0))            # Mitte der Gattersäge; der Stamm läuft entlang x
WAGEN_Z = 0.6                              # Oberkante des Wagens
GSTAMM_R = 0.3                             # Stamm im Gatter
WELLE_Z, KURBEL_R, PLEUEL_L = 3.3, 0.2, 1.5    # Welle, Kurbel und Pleuel: Hub 0,4 m, Gatterrahmen oben in Ruhe
KURBEL_X = GATTER.x + 0.35                 # Kurbelscheibe am Ende der Welle
PLEUEL_X = GATTER.x + 0.22

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Werk_0": ((0.2, -3.2, 2.55), (0.0, 1.8, 0.8)),          # Hobelbank, Sägebock, Drechselbank, Tür zur Sägehalle
    "Blick_Werk_L1": ((1.6, -2.6, 2.5), (-4.0, 0.2, 0.9)),         # Hoftor, Personalbuch, Drechselbank
    "Blick_Werk_L2": ((0.6, 1.0, 2.6), (-3.6, -3.2, 0.8)),         # Holztür, Personalbuch, Schürzen
    "Blick_Werk_R1": ((-1.6, -2.6, 2.5), (4.0, 0.6, 1.0)),         # Leimtisch, Werkzeugwand, Tür ins Lager
    "Blick_Werk_R2": ((-2.2, 2.0, 2.6), (3.8, -3.3, 0.8)),         # Leimofen, Löschfass, Sägebock
    "Blick_Lager_0": ((6.8, -1.25, 2.3), (6.9, 2.6, 0.5)),         # Bretterstapel, Stellplätze
    "Blick_Lager_L1": ((8.7, -1.2, 2.3), (4.6, 2.4, 0.8)),         # Tür in den Werkraum, Bretterstapel
    "Blick_Lager_R1": ((5.0, -1.1, 2.3), (9.2, 1.4, 0.7)),         # Holzgestell, Kantholz, Kisten
    "Blick_Saege_0": ((2.6, 4.2, 3.5), (-1.0, 7.8, 0.6)),          # Gattersäge, Arbeitsplatz am Stamm, Rampe, Ladetor
    "Blick_Saege_L1": ((-0.3, 4.1, 3.4), (-3.5, 7.6, 0.8)),        # Arbeitsplatz am Stamm von der Seite, Bretter
    "Blick_Saege_R1": ((3.4, 9.3, 3.5), (2.6, 4.0, 0.8)),          # Tür in den Werkraum, Welle durch die Wand
}

Oe = si.Oe
WERK_OEFFNUNGEN = {
    "vorne": [Oe(0.4, 1.2, 0.9, 1.1), Oe(2.6, 1.2, 0.9, 1.1), Oe(HOLZTUER_X, 0.0, HOLZTUER_B, HOLZTUER_H)],
    "rechts": [Oe(-2.75, 1.2, 0.8, 1.1), Oe(TUER_Y, 0.0, TUER_B, TUER_H)],
    "hinten": [Oe(SAEGETUER_X, 0.0, SAEGETUER_B, SAEGETUER_H)],
    "links": [Oe(HOFTOR_Y, 0.0, HOFTOR_B, HOFTOR_H), Oe(2.4, 1.3, 0.8, 1.1)],
}
LAGER_OEFFNUNGEN = {
    "vorne": [Oe(6.0, 1.3, 0.9, 1.0)],
    "hinten": [Oe(7.1, 1.85, 1.0, 0.75)],
    "links": [Oe(TUER_Y, 0.0, TUER_B, TUER_H)],
    "rechts": [],
}
LADETOR_X, LADETOR_B, LADETOR_H = -1.8, 2.4, 2.7
SAEGE_OEFFNUNGEN = {
    "vorne": [Oe(SAEGETUER_X, 0.0, SAEGETUER_B, SAEGETUER_H)],
    "rechts": [Oe(8.6, 1.6, 0.9, 1.2)],
    "hinten": [Oe(2.6, 2.0, 0.9, 1.0), Oe(LADETOR_X, 0.0, LADETOR_B, LADETOR_H)],
    "links": [Oe(5.2, 1.6, 0.9, 1.2), Oe(8.6, 1.6, 0.9, 1.2)],
}


def ecken(x0, x1, y0, y1):
    """Innenkanten der vier Wände, a → b so, dass der Raum links der Laufrichtung liegt."""
    return {"vorne": ((x0, y0), (x1, y0)), "rechts": ((x1, y0), (x1, y1)), "hinten": ((x1, y1), (x0, y1)),
            "links": ((x0, y1), (x0, y0))}


# ─────────────────────────────────────────────────────────────────────────────
#  Hülle: Wände, Fachwerk, Decke, Fenster
# ─────────────────────────────────────────────────────────────────────────────

def huelle(netz, decke, st, x0, x1, y0, y1, hoehe, waende):
    """Putzwände (waende = {seite: dict(oeffnungen, aussen, laibung, enden, teile)}) mit Außenseite und Laibungen, dazu
    die Decke mit Brettern, Balken und Abdeckung – wie schmied_innen.raum_huelle, nur mit der dünneren Fachwerkwand."""
    kanten = ecken(x0, x1, y0, y1)
    netz.alle = True
    for seite, angaben in waende.items():
        for a, b, art in angaben.get("teile", [kanten[seite] + ({},)]):
            si.wand(netz, st, a, b, hoehe, stoff="Putz", dicke=WAND,
                    **{**{k: v for k, v in angaben.items() if k != "teile"}, **art})
    netz.alle = False
    decke.vieleck_nach([(x0, y0, hoehe), (x1, y0, hoehe), (x1, y1, hoehe), (x0, y1, hoehe)], (0, 0, -1), st["Holz"])
    y = y0 + 0.3
    while y < y1 - 0.1:
        decke.vieleck_nach([(x0, y - 0.012, hoehe - 0.003), (x1, y - 0.012, hoehe - 0.003), (x1, y + 0.012, hoehe - 0.003),
                            (x0, y + 0.012, hoehe - 0.003)], (0, 0, -1), st["Fachwerk"])
        y += 0.3
    anzahl = max(2, round((x1 - x0) / 1.05))
    for k in range(anzahl):
        x = x0 + (x1 - x0) * (k + 0.5) / anzahl
        decke.quader((x, (y0 + y1) / 2, hoehe - 0.13), (0.18, y1 - y0, 0.26), st["Fachwerk"], ohne=("+z", "+y", "-y"))
    d = WAND
    decke.vieleck_nach([(x0 - d, y0 - d, hoehe + UEBER), (x1 + d, y0 - d, hoehe + UEBER), (x1 + d, y1 + d, hoehe + UEBER),
                        (x0 - d, y1 + d, hoehe + UEBER)], (0, 0, 1), st["Holz alt"])


def fachwerk_innen(netz, st, x0, x1, y0, y1, hoehe, oeffnungen):
    """Fachwerk auf der Innenseite der vier Wände (Schwelle, Rähm, Ständer, Riegel, Streben vor dem Putz).
    oeffnungen = {seite: [Oe, …]} in Weltmaßen; eine Öffnung ab dem Boden ist eine Tür. Mit Unterseiten (die Kamera
    steht im Raum und sieht Stürze und Rähm von unten)."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    lage = {"hinten": ((cx, y1, 0), "vorne", x1 - x0, lambda s: s - cx),
            "vorne": ((cx, y0, 0), "hinten", x1 - x0, lambda s: cx - s),
            "rechts": ((x1, cy, 0), "links", y1 - y0, lambda s: cy - s),
            "links": ((x0, cy, 0), "rechts", y1 - y0, lambda s: s - cy)}
    netz.alle = True
    for seite, (punkt, richtung, laenge, u) in lage.items():
        liste = []
        for o in oeffnungen.get(seite, []):
            if o.v < 0.05:
                liste.append(bs.Oeffnung("tuer", u(o.s), 0.0, o.b + 0.04, o.h + 0.02))      # Sturz über der Laibung
            else:
                liste.append(bs.Oeffnung("fenster", u(o.s), o.v - 0.02, o.b + 0.04, o.h + 0.04))
        bs.fachwerk(netz, bs.wandmatrix(punkt, richtung), laenge, hoehe, liste, st, eckstreben=True)
    netz.alle = False


def fenster(netz, st, a, b, o):
    """Scheibe (Material „Fenster“) mitten in der Öffnung o der Wand a → b, mit Fensterkreuz aus Holz und Fensterbrett."""
    a3, b3 = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    d = (b3 - a3).normalized()
    n = Vector((-d.y, d.x, 0))
    entlang_x = abs(d.x) > 0.5
    u = (o.s - (a3.x if entlang_x else a3.y)) * (d.x if entlang_x else d.y)
    t = 0.5 * WAND

    def p(du, v, w):
        return a3 + d * (u + du) + Vector((0, 0, v)) - n * w

    h2 = o.b / 2
    netz.vieleck_nach([p(-h2, o.v, t), p(h2, o.v, t), p(h2, o.v + o.h, t), p(-h2, o.v + o.h, t)], n, st["Fenster"])
    vk = o.v + o.h * 0.6
    for du0, du1, v0, v1 in ((-0.025, 0.025, o.v, o.v + o.h), (-h2, h2, vk - 0.025, vk + 0.025)):
        netz.vieleck_nach([p(du0, v0, t - 0.015), p(du1, v0, t - 0.015), p(du1, v1, t - 0.015), p(du0, v1, t - 0.015)],
                          n, st["Fachwerk"])
    mitte = a3 + d * u + Vector((0, 0, o.v - 0.02)) + n * 0.05                  # Fensterbrett, Oberkante über der Laibung
    netz.quader((0, 0, 0), (1, 1, 1), st["Holz alt"], Matrix.Translation(mitte) @ _richtung_matrix(d, n)
                @ Matrix.Diagonal((o.b + 0.1, 0.16, 0.05, 1)))


def _richtung_matrix(d, n):
    """Drehmatrix: örtlich x entlang d, y entlang n, z oben."""
    return Matrix((d, n, Vector((0, 0, 1)))).transposed().to_4x4()


def fenster_und_licht(netz, st, kanten, oeffnungen, anker, stufe=1):
    """Scheiben aller Fenster eines Raums und je ein Licht „Fenster“ (Tageslicht, Blick −Z in den Raum)."""
    for seite, liste in oeffnungen.items():
        a, b = kanten[seite]
        d = (Vector((b[0], b[1], 0)) - Vector((a[0], a[1], 0))).normalized()
        n = Vector((-d.y, d.x, 0))
        for o in liste:
            if o.v < 0.05:
                continue
            fenster(netz, st, a, b, o)
            p = Vector((o.s, a[1], 0)) if abs(d.x) > 0.5 else Vector((a[0], o.s, 0))
            p += n * 0.1 + Vector((0, 0, o.v + o.h / 2))
            anker.append(licht(p, (n.x, n.y, 0), "Fenster", stufe))


_lichter = [0]


def licht(punkt, richtung, art, stufe=1):
    """Licht_<n> (fortlaufend): „Fenster“ blickt in den Raum, „Lampe“ nach unten."""
    _lichter[0] += 1
    o = si.licht(f"Licht_{_lichter[0]}", punkt, richtung, art, stufe)
    if art == "Lampe":
        o.rotation_euler = (math.radians(-90), 0, 0)          # Blender +Y (Spiel −Z) zeigt nach unten
    return o


# ─────────────────────────────────────────────────────────────────────────────
#  Werkraum
# ─────────────────────────────────────────────────────────────────────────────

def hobelbank(netz, st, mitte):
    """Hobelbank entlang x, gehobelt wird auf der Seite +y: dicke Platte, Beine mit Zargen, Vorderzange links auf der
    Arbeitsseite, Hinterzange rechts, Bankhaken, Beilade auf der Rückseite; darauf ein Brett, ein Putzhobel, Werkzeug."""
    x, y, h = mitte.x, mitte.y, BANK_HOEHE
    holz, alt = st["Holz"], st["Holz alt"]
    netz.quader((x, y, h - 0.05), (2.1, 0.65, 0.1), holz)                                   # Platte
    for sx in (-0.85, 0.85):
        for sy in (-0.24, 0.24):
            netz.quader((x + sx, y + sy, (h - 0.1) / 2), (0.1, 0.1, h - 0.1), alt, ohne=("+z",))
        netz.quader((x + sx, y, 0.2), (0.07, 0.38, 0.08), alt, ohne=("+x", "-x"))         # Zarge quer
    netz.quader((x, y, 0.36), (1.6, 0.07, 0.09), alt, ohne=("+x", "-x"))                   # Längszarge
    # Vorderzange (links, Arbeitsseite): Backe, Spindel mit Knebel
    vz = Vector((x - 0.82, y + 0.36, h - 0.1))
    netz.quader(vz, (0.36, 0.07, 0.2), holz)
    netz.stab(vz + Vector((0, 0.035, 0)), vz + Vector((0, 0.2, 0)), 0.05, 0.05, holz)
    netz.stab(vz + Vector((-0.13, 0.19, 0)), vz + Vector((0.13, 0.19, 0)), 0.025, 0.025, alt)
    # Hinterzange (rechts): Kasten, der die Platte verlängert, mit Spindel
    hz = Vector((x + 1.17, y + 0.12, h - 0.05))
    netz.quader(hz, (0.24, 0.32, 0.1), holz)
    netz.stab(hz + Vector((0.12, 0, -0.02)), hz + Vector((0.3, 0, -0.02)), 0.05, 0.05, holz)
    netz.stab(hz + Vector((0.29, -0.12, -0.02)), hz + Vector((0.29, 0.12, -0.02)), 0.025, 0.025, alt)
    for k in range(6):                                                                       # Bankhaken
        netz.quader((x - 0.6 + 0.3 * k, y + 0.25, h + 0.012), (0.03, 0.03, 0.025), st["Eisen"], ohne=("-z",))
    # Beilade auf der Rückseite (−y): flache Rinne mit Werkzeug
    netz.quader((x - 0.1, y - 0.27, h + 0.015), (1.5, 0.03, 0.03), alt, ohne=("-z",))
    netz.stab((x - 0.6, y - 0.29, h + 0.012), (x - 0.3, y - 0.27, h + 0.012), 0.025, 0.04, holz)         # Klüpfel-Stiel
    netz.quader((x - 0.66, y - 0.29, h + 0.04), (0.1, 0.07, 0.07), holz)                              # Klüpfel-Kopf
    netz.stab((x + 0.05, y - 0.28, h + 0.01), (x + 0.3, y - 0.28, h + 0.01), 0.018, 0.02, st["Eisen"])   # Stemmeisen
    netz.stab((x - 0.08, y - 0.28, h + 0.012), (x + 0.05, y - 0.28, h + 0.012), 0.03, 0.03, holz)
    # Brett, an dem gehobelt wird (Arbeitsseite), Putzhobel am rechten Ende, ein paar Locken auf der Platte
    netz.quader((x + 0.4, y + 0.15, h + 0.02), (1.6, 0.24, 0.04), st["Schnittholz"])
    netz.quader((x + 0.9, y - 0.12, h + 0.035), (0.2, 0.06, 0.07), holz)
    netz.quader((x + 0.84, y - 0.12, h + 0.08), (0.03, 0.04, 0.03), alt)
    zufall = random.Random(5)
    for k in range(5):
        locke(netz, st, Vector((x + zufall.uniform(-0.6, 0.9), y + zufall.uniform(-0.15, 0.3), h + 0.045)), zufall)


def saegebock(netz, st, mitte):
    """Zwei Böcke mit gekreuzten Beinen, darin der Stamm entlang x; abgesägte Scheiben und ein Klotz daneben."""
    alt = st["Holz alt"]
    for dx in (-0.55, 0.55):
        for s in (-1, 1):            # die Beine je Bock leicht versetzt, sonst decken sich ihre Flächen
            netz.stab(mitte + Vector((dx + s * 0.03, s * 0.32, 0)), mitte + Vector((dx + s * 0.03, -s * 0.13, 0.8)), 0.08, 0.08,
                      alt, quer=(1, 0, 0))
        netz.stab(mitte + Vector((dx - 0.03, -0.18, 0.32)), mitte + Vector((dx - 0.03, 0.18, 0.32)), 0.05, 0.05, alt)
    tischler.rundholz(netz, st, mitte + Vector((-1.15, 0, STAMM_Z)), mitte + Vector((1.15, 0, STAMM_Z)), r=STAMM_R)
    klotz = mitte + Vector((1.0, 0.75, 0))                                         # Hackklotz, darauf und daneben Scheiben
    bs.fass(netz, st, klotz, r=0.2, h=0.42, reifen=(), stoff="Schnittholz")
    bs.achteck(netz, st, klotz + Vector((0, 0, 0.42)), STAMM_R * 0.95, 0.0, 0.07, "Schnittholz")
    for k, (dx, dy) in enumerate(((0.05, -0.42), (0.38, -0.22))):
        bs.achteck(netz, st, klotz + Vector((dx, dy, 0)), STAMM_R * 0.95, 0.0, 0.07 + 0.01 * k, "Schnittholz")


def leimtisch(netz, st, M):
    """Leimtisch an der Wand (Länge entlang örtlich x): Platte, Beine, Ablage; darauf ein geleimter Rahmen, ein Leimtopf
    mit Pinsel und Brettchen."""
    holz = st["Holz"]
    si.q(netz, st, M, (0, 0.4, 0.8), (1.8, 0.75, 0.06), "Holz alt")
    for x in (-0.82, 0.82):
        for y in (0.08, 0.72):
            netz.stab(M @ Vector((x, y, 0)), M @ Vector((x, y, 0.77)), 0.07, 0.07, holz)
    si.q(netz, st, M, (0, 0.4, 0.2), (1.6, 0.6, 0.03), "Holz alt")                              # Ablage
    for k in range(3):                                                                            # Brettchen auf der Ablage
        si.q(netz, st, M, (-0.5 + 0.45 * k, 0.4, 0.235 + 0.02 * (k % 2)), (0.4, 0.2 + 0.05 * k, 0.03), "Schnittholz")
    for x, y, bx, by in ((-0.25, 0.2, 0.7, 0.05), (-0.25, 0.6, 0.7, 0.05), (-0.575, 0.4, 0.05, 0.35), (0.075, 0.4, 0.05, 0.35)):
        si.q(netz, st, M, (x, y, 0.86), (bx, by, 0.06), "Schnittholz")                          # Rahmen
    topf = M @ Vector((0.55, 0.3, 0.83))
    bs.achteck(netz, st, topf, 0.08, 0.0, 0.12, "Holz alt")
    bs.rund_oben(netz, st, topf, 0.07, topf.z + 0.101, "Leim")
    netz.stab(topf + Vector((0, 0, 0.07)), M @ Vector((0.62, 0.22, 1.06)), 0.02, 0.02, holz)
    netz.vieleck_nach([M @ Vector(p) for p in ((0.3, 0.55, 0.832), (0.45, 0.6, 0.832), (0.4, 0.68, 0.832))], (0, 0, 1),
                      st["Leim"])                                                                 # Leimfleck


def hoftor(netz, st, M, b, h, r=0.1):
    """Zweiflügeliges Tor in der Öffnung (Mitte unten auf der Innenseite, +y in den Raum), `r` hinter der Innenseite:
    Flügel in der Hausfarbe mit Querleisten und Streben, Bänder, Sperrbalken. Pfosten und Sturz gibt das Fachwerk."""
    for s in (-1, 1):
        u = s * b / 4
        si.q(netz, st, M, (u, -r - 0.035, h / 2), (b / 2 - 0.02, 0.07, h - 0.01), "Hausfarbe", ohne=("-y",))
        for z in (0.35, h / 2, h - 0.35):
            si.q(netz, st, M, (u, -r + 0.02, z), (b / 2 - 0.14, 0.04, 0.14), "Fachwerk", ohne=("-y",))
        netz.stab(M @ Vector((u - s * (b / 4 - 0.12), -r + 0.025, 0.42)), M @ Vector((u + s * (b / 4 - 0.12), -r + 0.025, h / 2 - 0.07)),
                  0.12, 0.035, st["Fachwerk"], quer=M.to_3x3() @ Vector((0, 1, 0)), ohne=("-y",))
        for z in (0.62, h - 0.62):
            si.q(netz, st, M, (s * (b / 2 - 0.3), -r + 0.005, z), (0.5, 0.01, 0.06), "Eisen", ohne=("-y",))
        si.q(netz, st, M, (s * 0.12, -r + 0.06, 1.2), (0.06, 0.04, 0.22), "Eisen", ohne=("-y",))    # Halter des Balkens
    si.q(netz, st, M, (0, -r + 0.13, 1.2), (b - 0.3, 0.1, 0.12), "Holz")                           # Sperrbalken


def schuerzen(netz, st, M):
    """Gekauft: zwei Lederschürzen an Haken (örtlich, +y in den Raum)."""
    si.q(netz, st, M, (0, 0.03, 2.05), (0.9, 0.06, 0.08), "Fachwerk", ohne=("-y",))
    for k, x in enumerate((-0.22, 0.22)):
        netz.stab(M @ Vector((x, 0.03, 2.0)), M @ Vector((x, 0.12, 2.02)), 0.02, 0.02, st["Eisen"])
        oben, unten = 1.97, 1.25 - 0.05 * k
        eck = [(x - 0.16, 0.1, oben), (x + 0.16, 0.1, oben), (x + 0.24, 0.08 + 0.04 * k, unten), (x - 0.24, 0.08 + 0.04 * k, unten)]
        netz.vieleck_nach([M @ Vector(p) for p in eck], M.to_3x3() @ Vector((0, 1, 0)), st["Leder"])
        netz.vieleck_nach([M @ (Vector(p) - Vector((0, 0.01, 0))) for p in eck], M.to_3x3() @ Vector((0, -1, 0)), st["Leder"])


def werkzeug1(netz, st, M):
    """Gekauft: Hobelsatz auf der oberen Leiste der Werkzeugwand, Stemmeisen an den oberen Haken, eine Spannsäge an den
    unteren (linke Hälfte)."""
    for k, (x, laenge) in enumerate(((-0.72, 0.24), (-0.43, 0.32), (-0.16, 0.18))):
        si.q(netz, st, M, (x, 0.08, 2.45), (laenge, 0.07, 0.08), "Holz")
        si.q(netz, st, M, (x - laenge * 0.2, 0.08, 2.51), (0.035, 0.04, 0.05), "Holz alt")       # Nase
        si.q(netz, st, M, (x + laenge * 0.1, 0.08, 2.5), (0.02, 0.05, 0.03), "Eisen")             # Eisen und Keil
    for x, z in si.HAKEN:
        if x < 0 and z > 2:                                                                        # Stemmeisen
            netz.stab(M @ Vector((x, 0.1, z + 0.02)), M @ Vector((x, 0.1, z - 0.2)), 0.035, 0.035, st["Holz alt"])
            netz.stab(M @ Vector((x, 0.1, z - 0.2)), M @ Vector((x, 0.1, z - 0.38)), 0.022, 0.012, st["Eisen"])
    # Spannsäge: zwei Arme, Steg, Blatt unten, Spannschnur oben
    for x in (-0.7, -0.27):
        netz.stab(M @ Vector((x, 0.11, 1.62)), M @ Vector((x, 0.11, 1.18)), 0.035, 0.03, st["Holz"])
    netz.stab(M @ Vector((-0.7, 0.11, 1.4)), M @ Vector((-0.27, 0.11, 1.4)), 0.03, 0.03, st["Holz"])
    netz.stab(M @ Vector((-0.72, 0.115, 1.21)), M @ Vector((-0.25, 0.115, 1.21)), 0.045, 0.006, st["Eisen"])
    netz.stab(M @ Vector((-0.7, 0.105, 1.6)), M @ Vector((-0.27, 0.105, 1.6)), 0.012, 0.012, st["Seil"])


def werkzeug2(netz, st, M):
    """Gekauft (ab Stufe 2): feines Werkzeug – Hohleisen und Schnitzmesser oben, Bohrwinde, Zirkel, Winkel und Raspel
    unten (rechte Hälfte)."""
    for k, (x, z) in enumerate(h for h in si.HAKEN if h[0] > 0.01 and h[1] > 2):
        netz.stab(M @ Vector((x, 0.1, z + 0.02)), M @ Vector((x, 0.1, z - 0.13)), 0.03, 0.03, st["Holz alt"])
        netz.stab(M @ Vector((x, 0.1, z - 0.13)), M @ Vector((x + 0.01 * (k % 2), 0.1, z - 0.3 + 0.03 * (k % 2))), 0.018, 0.014,
                  st["Eisen"])
    e = st["Eisen"]
    # Bohrwinde: gekröpfter Bügel mit Knauf und Bohrer
    p = [M @ Vector(v) for v in ((0.19, 0.1, 1.6), (0.19, 0.1, 1.5), (0.27, 0.1, 1.45), (0.27, 0.1, 1.33), (0.19, 0.1, 1.28),
                                 (0.19, 0.1, 1.2))]
    for a, b in zip(p, p[1:]):
        netz.stab(a, b, 0.018, 0.018, e)
    netz.stab(p[-1], p[-1] + Vector((0, 0, -0.12)), 0.01, 0.01, e)
    bs.achteck(netz, st, M @ Vector((0.19, 0.1, 1.6)), 0.03, 0.0, 0.04, "Holz")
    # Zirkel, Winkel, Raspel
    for s in (-1, 1):
        netz.stab(M @ Vector((0.38, 0.1, 1.6)), M @ Vector((0.38 + s * 0.05, 0.1, 1.35)), 0.012, 0.01, e)
    netz.stab(M @ Vector((0.57, 0.1, 1.6)), M @ Vector((0.57, 0.1, 1.32)), 0.04, 0.012, st["Holz"])
    netz.stab(M @ Vector((0.57, 0.1, 1.34)), M @ Vector((0.72, 0.1, 1.34)), 0.035, 0.008, e)
    netz.stab(M @ Vector((0.76, 0.1, 1.6)), M @ Vector((0.76, 0.1, 1.48)), 0.03, 0.03, st["Holz alt"])
    netz.stab(M @ Vector((0.76, 0.1, 1.48)), M @ Vector((0.76, 0.1, 1.24)), 0.035, 0.012, e)


def leimzwingen(netz, st):
    """Gekauft: Leimofen in der Ecke unter dem Fenster (Feuerloch mit Glut, Topf im Wasserbad, Rohr in die Wand) und
    Schraubzwingen an einer Leiste daneben. Alles liegt vor dem Leimtisch: Die Klickfläche (Umriss des Knotens) soll
    die des Tischs nicht überdecken."""
    ofen = Vector((WX1 - 0.4, -2.85, 0))
    netz.quader(ofen + Vector((0, 0, 0.31)), (0.6, 0.55, 0.62), st["Ziegel"], ohne=("-z",))
    for k in range(1, 6):                                                                         # Fugen
        z = 0.105 * k
        netz.quader(ofen + Vector((0, 0, z)), (0.604, 0.554, 0.012), st["Fuge"], ohne=("-z", "+z"))
    netz.vieleck_nach([ofen + Vector(v) for v in ((-0.302, -0.12, 0.08), (-0.302, 0.12, 0.08), (-0.302, 0.12, 0.28),
                                                   (-0.302, -0.12, 0.28))], (-1, 0, 0), st["Glut"])
    netz.quader(ofen + Vector((-0.31, 0, 0.3)), (0.02, 0.3, 0.04), st["Eisen"], ohne=("+x",))
    netz.quader(ofen + Vector((0, 0, 0.635)), (0.64, 0.59, 0.03), st["Eisen"], ohne=("-z",))
    bs.achteck(netz, st, ofen + Vector((-0.05, 0.02, 0.65)), 0.17, 0.0, 0.2, "Eisen")             # Wasserbad
    bs.rund_oben(netz, st, ofen + Vector((-0.05, 0.02, 0)), 0.155, 0.851, "Wasser")
    bs.achteck(netz, st, ofen + Vector((-0.05, 0.02, 0.7)), 0.1, 0.0, 0.2, "Holz alt")              # Leimtopf darin
    bs.rund_oben(netz, st, ofen + Vector((-0.05, 0.02, 0)), 0.088, 0.902, "Leim")
    netz.stab(ofen + Vector((-0.05, 0.05, 0.86)), ofen + Vector((-0.14, 0.1, 1.07)), 0.02, 0.02, st["Holz"])
    rohr = ofen + Vector((0.18, 0.15, 0.65))
    netz.stab(rohr, rohr + Vector((0, 0, 0.35)), 0.1, 0.1, st["Eisen"])
    netz.stab(rohr + Vector((0, 0, 0.35)), Vector((WX1 + 0.01, rohr.y, rohr.z + 0.35)), 0.1, 0.1, st["Eisen"])
    M = si.rahmen(WX1 - 0.06, -2.12, 90)                                                          # Leiste mit Zwingen
    si.q(netz, st, M, (0, 0.03, 1.8), (0.4, 0.06, 0.06), "Fachwerk", ohne=("-y",))
    for k, x in enumerate((-0.13, 0.0, 0.13)):
        for y in (0.07, 0.07 + 0.1 + 0.03 * k):
            si.q(netz, st, M, (x, y, 1.58 - 0.03 * k), (0.035, 0.035, 0.34), "Holz")
        for z in (1.7, 1.5):
            netz.stab(M @ Vector((x, 0.03, z - 0.03 * k)), M @ Vector((x, 0.3, z - 0.03 * k)), 0.018, 0.018, st["Holz alt"])


def drehbank(netz, st):
    """Drechslerei: Wippdrehbank – zwei Böcke, zwei Bettbalken, Docke und Reitstock mit Spitzen, Auflage, Lager des Pedals
    und die Aufhängung der Federstange am Deckenbalken. Die bewegten Teile baut drehbank_teile."""
    holz, alt, eisen = st["Holz"], st["Holz alt"], st["Eisen"]
    x, y = DREHBANK.x, DREHBANK.y
    for xb in (x - 0.75, x + 0.75):                                                               # Böcke
        for s in (-1, 1):
            netz.stab((xb, y + s * 0.36, 0), (xb, y + s * 0.11, 0.8), 0.08, 0.08, alt, quer=(1, 0, 0))
        netz.stab((xb, y - 0.3, 0.35), (xb, y + 0.3, 0.35), 0.06, 0.06, alt)
    for s in (-1, 1):                                                                             # Bettbalken
        netz.quader((x, y + s * 0.13, 0.84), (1.9, 0.08, 0.12), holz)
    for xd in (SPINDEL_X0 - 0.1, SPINDEL_X1 + 0.1):                                               # Docke, Reitstock
        netz.quader((xd, y, 1.02), (0.1, 0.26, 0.36), holz)
        netz.quader((xd, y, 0.72), (0.06, 0.12, 0.24), alt)                                       # Zapfen unter dem Bett
        netz.quader((xd + 0.06, y, 0.68), (0.04, 0.05, 0.08), alt)                               # Keil
    for x0, x1 in ((SPINDEL_X0 - 0.05, SPINDEL_X0), (SPINDEL_X1, SPINDEL_X1 + 0.05)):            # Spitzen
        netz.stab((x0, y, SPINDEL_Z), (x1, y, SPINDEL_Z), 0.02, 0.02, eisen)
    for xa in (SPINDEL_X0 + 0.1, SPINDEL_X1 - 0.1):                                               # Auflage
        netz.quader((xa, y + 0.13, 0.94), (0.05, 0.05, 0.08), alt)
    netz.quader(((SPINDEL_X0 + SPINDEL_X1) / 2, y + 0.11, 0.99), (SPINDEL_X1 - SPINDEL_X0, 0.04, 0.03), holz)
    netz.quader((PEDAL_DREH.x, PEDAL_DREH.y - 0.04, 0.04), (0.2, 0.1, 0.08), alt)                 # Lager des Pedals
    netz.alle = True                                                                              # Aufhängung: von unten zu sehen
    f = FEDER_DREH
    netz.stab((f.x, f.y, f.z - 0.08), (f.x, f.y, H_WERK - 0.26), 0.1, 0.1, alt)
    for dz in (0.0, 0.08):
        netz.stab((f.x - 0.07, f.y, f.z + dz), (f.x + 0.07, f.y, f.z + dz), 0.03, 0.12, st["Seil"])
    netz.alle = False


def drehbank_teile(st, k):
    """Bewegte Teile der Wippdrehbank für Stufe k (Ursprung im Drehpunkt): Werkstück (Spindel), Federstange, Pedal und die
    beiden Stücke der Schnur. Gibt [(Objekt, Bewegung)] zurück; Bewegung(Bild) → (Ort, Drehung, Maßstab)."""
    y = DREHBANK.y
    # Werkstück: ein Tischbein mit Wulst, Kehle und Perlen; die Schnur liegt bei SCHNUR_X in einer Rille
    spindel = bs.Netz(f"Spindel{k}", alle=True)
    mitte_x = (SPINDEL_X0 + SPINDEL_X1) / 2
    lx = lambda xw: xw - mitte_x  # noqa: E731
    profil = [(lx(SPINDEL_X0), 0.03), (lx(SPINDEL_X0) + 0.03, 0.045), (lx(SPINDEL_X0) + 0.12, 0.045),
              (lx(SPINDEL_X0) + 0.14, 0.06), (lx(SPINDEL_X0) + 0.2, 0.06), (lx(SPINDEL_X0) + 0.22, 0.04),
              (lx(SCHNUR_X) - 0.04, 0.04), (lx(SCHNUR_X) - 0.03, SCHNUR_R), (lx(SCHNUR_X) + 0.03, SCHNUR_R),
              (lx(SCHNUR_X) + 0.04, 0.042), (lx(SCHNUR_X) + 0.14, 0.05), (lx(SCHNUR_X) + 0.18, 0.058),
              (lx(SCHNUR_X) + 0.22, 0.04), (lx(SPINDEL_X1) - 0.08, 0.04), (lx(SPINDEL_X1) - 0.05, 0.052),
              (lx(SPINDEL_X1) - 0.02, 0.04), (lx(SPINDEL_X1), 0.028)]
    drehteil(spindel, st, Matrix.Identity(4), profil)
    for dx in (-0.012, 0.012):                                                                    # Schnur in der Rille
        drehteil(spindel, st, Matrix.Translation((lx(SCHNUR_X) + dx, 0, 0)),
                 [(-0.006, SCHNUR_R + 0.008), (0.006, SCHNUR_R + 0.008)], "Seil", n=8)
    o_sp = spindel.objekt()
    o_sp.location = (mitte_x, y, SPINDEL_Z)

    # Federstange: dicker Fuß hinter dem Drehpunkt, verjüngt bis zur Spitze über der Schnur
    feder = bs.Netz(f"Federstange{k}", alle=True)
    spitze = Vector((SCHNUR_X, y + 0.045, FEDER_SPITZE_Z)) - FEDER_DREH
    fuss = -spitze.normalized() * 0.35
    teile = 4
    for i in range(teile):
        a, b = fuss.lerp(spitze, i / teile), fuss.lerp(spitze, (i + 1) / teile)
        dicke = 0.085 - 0.045 * (i + 0.5) / teile
        feder.stab(a, b, dicke, dicke, st["Holz"], quer=(0, 1, 0), mehr=0.01)
    feder.stab(spitze + Vector((0.03, 0, 0.02)), spitze + Vector((-0.03, 0, 0.02)), 0.06, 0.06, st["Seil"])
    o_fe = feder.objekt()
    o_fe.location = FEDER_DREH

    # Pedal: Brett vom Drehpunkt bis unter den Fuß, vorn ein Klotz als Tritt
    pedal = bs.Netz(f"Pedal{k}", alle=True)
    tritt = Vector((0, 1.0, PEDAL_OBEN))
    pedal.stab((0, -0.02, 0), tritt, 0.12, 0.04, st["Holz alt"], quer=(0, -PEDAL_OBEN, 1.0))
    pedal.quader(tritt * 0.92 + Vector((0, 0, 0.03)), (0.14, 0.12, 0.03), st["Holz"])
    o_pe = pedal.objekt()
    o_pe.location = PEDAL_DREH

    # Schnur: oben vom Werkstück zur Federstange, unten zum Pedal (Länge über den Maßstab z)
    oben_fuss = Vector((SCHNUR_X, y + SCHNUR_R + 0.01, SPINDEL_Z))
    oben_laenge = FEDER_SPITZE_Z - 0.03 - oben_fuss.z
    so = bs.Netz(f"SchnurOben{k}", alle=True)
    so.stab((0, 0, 0), (0, 0, oben_laenge), 0.012, 0.012, st["Seil"])
    o_so = so.objekt()
    o_so.location = oben_fuss
    unten_fuss = Vector((SCHNUR_X, y - SCHNUR_R - 0.01, SPINDEL_Z))
    unten_laenge = unten_fuss.z - pedal_hoehe(0.0, unten_fuss.y)
    su = bs.Netz(f"SchnurUnten{k}", alle=True)
    su.stab((0, 0, 0), (0, 0, -unten_laenge), 0.012, 0.012, st["Seil"])
    o_su = su.objekt()
    o_su.location = unten_fuss

    def zug(bild):
        """Wie weit die Schnur gerade gezogen ist (Meter) und der Winkel des Pedals."""
        d = tritt_anteil((bild % TAKT) / TAKT)
        w0, w1 = math.atan2(PEDAL_OBEN, 1.0), math.atan2(PEDAL_UNTEN, 1.0)
        w = w0 - (w0 - w1) * d
        return pedal_hoehe(0.0, unten_fuss.y) - pedal_hoehe(w0 - w, unten_fuss.y), w0 - w

    def spindel_bewegung(bild):
        weg, _ = zug(bild)
        return None, (weg / SCHNUR_R, 0, 0), None

    def feder_bewegung(bild):
        weg, _ = zug(bild)
        return None, (0, feder_winkel(spitze, weg), 0), None

    def pedal_bewegung(bild):
        _, w = zug(bild)
        return None, (-w, 0, 0), None

    def schnur_oben(bild):
        weg, _ = zug(bild)
        return None, None, (1, 1, (oben_laenge - weg) / oben_laenge)

    def schnur_unten(bild):
        weg, _ = zug(bild)
        return None, None, (1, 1, (unten_laenge + weg) / unten_laenge)

    return [(o_sp, spindel_bewegung), (o_fe, feder_bewegung), (o_pe, pedal_bewegung), (o_so, schnur_oben),
            (o_su, schnur_unten)]


def tritt_anteil(phase):
    """0 = Pedal oben, 1 = ganz getreten: in 40 % des Takts hinunter, danach zieht die Federstange es zurück."""
    if phase < 0.4:
        return 0.5 - 0.5 * math.cos(math.pi * phase / 0.4)
    return 0.5 + 0.5 * math.cos(math.pi * (phase - 0.4) / 0.6)


def pedal_hoehe(drehung, y):
    """Höhe der Oberseite des Pedals bei y (Welt), wenn es um `drehung` (Bogenmaß) aus der Ruhe hinuntergedrückt ist."""
    w = math.atan2(PEDAL_OBEN, 1.0) - drehung
    s = (y - PEDAL_DREH.y) / math.cos(math.atan2(PEDAL_OBEN, 1.0))
    return PEDAL_DREH.z + s * math.sin(w) + 0.02 * math.cos(w)


def feder_winkel(spitze, weg):
    """Drehung der Federstange um y (Bogenmaß), damit ihre Spitze `weg` Meter tiefer liegt (Halbierung)."""
    lo, hi = -0.5, 0.0
    for _ in range(40):
        m = (lo + hi) / 2
        z = -spitze.x * math.sin(m) + spitze.z * math.cos(m)
        if z > spitze.z - weg:
            hi = m
        else:
            lo = m
    return (lo + hi) / 2


def drehteil(netz, st, M, profil, stoff="Schnittholz", n=8):
    """Gedrechseltes Stück um die örtliche x-Achse (M): profil = [(x, r), …] von links nach rechts, n-eckig, Enden zu."""
    def ring(x, r):
        return [M @ Vector((x, r * math.cos(2 * math.pi * (k + 0.5) / n), r * math.sin(2 * math.pi * (k + 0.5) / n)))
                for k in range(n)]
    ringe = [ring(x, r) for x, r in profil]
    m = st[stoff] if isinstance(stoff, str) else stoff
    drehung = M.to_3x3()
    for i in range(len(profil) - 1):
        (xa, ra), (xb, rb) = profil[i], profil[i + 1]
        for k in range(n):
            j = (k + 1) % n
            w = 2 * math.pi * (k + 1) / n
            normale = drehung @ Vector((-(rb - ra), (xb - xa) * math.cos(w), (xb - xa) * math.sin(w)))
            netz.vieleck_nach([ringe[i][k], ringe[i][j], ringe[i + 1][j], ringe[i + 1][k]], normale, m)
    netz.vieleck_nach(ringe[0], drehung @ Vector((-1, 0, 0)), m)
    netz.vieleck_nach(ringe[-1], drehung @ Vector((1, 0, 0)), m)


def drechselware(netz, st):
    """Drechslerei: Wandbord hinten links mit Schalen, Bechern, Spindeln und Kegeln."""
    M = si.rahmen(-3.2, WY1, 180)
    zufall = random.Random(9)
    for z in (1.45, 1.85):
        si.q(netz, st, M, (0, 0.14, z), (1.2, 0.28, 0.03), "Holz", ohne=("-y",))
        for x in (-0.5, 0.5):
            netz.stab(M @ Vector((x, 0.0, z - 0.2)), M @ Vector((x, 0.24, z - 0.02)), 0.04, 0.04, st["Holz alt"])
        x = -0.52
        while x < 0.5:
            art = zufall.choice(("schale", "becher", "spindel", "kegel"))
            fuss = M @ Vector((x, 0.13, z + 0.015))
            if art == "schale":
                bs.achteck(netz, st, fuss, 0.09, 0.0, 0.03, "Schnittholz")
                bs.achteck(netz, st, fuss, 0.12, 0.03, 0.07, "Schnittholz")
                x += 0.27
            elif art == "becher":
                bs.achteck(netz, st, fuss, 0.04, 0.0, 0.03, "Holz")
                bs.achteck(netz, st, fuss, 0.05, 0.03, 0.14, "Holz")
                x += 0.13
            elif art == "spindel":
                drehteil(netz, st, Matrix.Translation(fuss) @ Matrix.Rotation(math.radians(-90), 4, "Y"),
                         [(0.0, 0.02), (0.05, 0.03), (0.1, 0.018), (0.2, 0.026), (0.27, 0.015), (0.3, 0.01)], n=6)
                x += 0.09
            else:
                drehteil(netz, st, Matrix.Translation(fuss) @ Matrix.Rotation(math.radians(-90), 4, "Y"),
                         [(0.0, 0.035), (0.08, 0.05), (0.16, 0.025), (0.2, 0.035), (0.23, 0.0)], n=6)
                x += 0.15


def gefach(netz, st):
    """Stufe 1 und 2: Die Öffnung zur späteren Sägehalle ist ein verputztes Gefach mit Riegel."""
    x0, x1 = SAEGETUER_X - SAEGETUER_B / 2, SAEGETUER_X + SAEGETUER_B / 2
    netz.vieleck_nach([(x0, WY1, 0), (x1, WY1, 0), (x1, WY1, SAEGETUER_H), (x0, WY1, SAEGETUER_H)], (0, -1, 0), st["Putz"])
    M = bs.wandmatrix((SAEGETUER_X, WY1, 0), "vorne")
    bs.balken(netz, M, -SAEGETUER_B / 2 - 0.02, 0.9 - bs.B / 2, SAEGETUER_B / 2 + 0.02, 0.9 - bs.B / 2, st["Fachwerk"],
              vor=bs.VOR_RIEGEL)
    bs.balken(netz, M, -SAEGETUER_B / 2 - 0.02, bs.B, SAEGETUER_B / 2 + 0.02, 0.9 - bs.B, st["Fachwerk"], vor=bs.VOR_STREBE)


def bretter_an_wand(netz, st):
    """Bretter, an die Rückwand gelehnt (zwischen Drechselbank und Hobelbank)."""
    for k in range(5):
        x = -1.35 + 0.13 * k
        netz.stab((x, WY1 - 0.32 + 0.012 * k, 0.0), (x + 0.03, WY1 - 0.06, 2.3 - 0.12 * (k % 3)), 0.24 - 0.02 * (k % 2), 0.03,
                  st["Schnittholz" if k % 2 else "Holz"], quer=(0, 1, 0.12))


def laterne(netz, st, punkt, kette):
    """Laterne an einer Kette: Eisenrahmen, Scheiben aus dem Material „Glut“ (leuchten, solange gearbeitet wird)."""
    alle, netz.alle = netz.alle, True
    e = st["Eisen"]
    netz.stab(punkt + Vector((0, 0, 0.17)), Vector((punkt.x, punkt.y, kette)), 0.015, 0.015, e)
    netz.quader(punkt + Vector((0, 0, -0.16)), (0.2, 0.2, 0.03), e)
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.stab(punkt + Vector((sx * 0.09, sy * 0.09, -0.15)), punkt + Vector((sx * 0.09, sy * 0.09, 0.1)), 0.02, 0.02, e)
    for ecke in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        n = Vector((ecke[0], ecke[1], 0))
        q = Vector((-n.y, n.x, 0)) * 0.08
        mitte = punkt + n * 0.085
        netz.vieleck_nach([mitte - q + Vector((0, 0, -0.14)), mitte + q + Vector((0, 0, -0.14)), mitte + q + Vector((0, 0, 0.09)),
                           mitte - q + Vector((0, 0, 0.09))], n, st["Glut"])
    for k in range(4):                                                                            # Dach: Pyramide
        a = punkt + Vector((0.12 * (1 if k in (0, 1) else -1), 0.12 * (1 if k in (1, 2) else -1), 0.1))
        b = punkt + Vector((0.12 * (1 if k in (1, 2) else -1), 0.12 * (1 if k in (2, 3) else -1), 0.1))
        netz.vieleck_nach([a, b, punkt + Vector((0, 0, 0.19))], (a + b) / 2 - punkt, e)
    netz.vieleck_nach([punkt + Vector(v) for v in ((-0.12, -0.12, 0.1), (0.12, -0.12, 0.1), (0.12, 0.12, 0.1), (-0.12, 0.12, 0.1))],
                      (0, 0, -1), e)
    netz.alle = alle


# ─────────────────────────────────────────────────────────────────────────────
#  Späne
# ─────────────────────────────────────────────────────────────────────────────

def locke(netz, st, fuss, zufall, stoff="Spaene"):
    """Eine Hobellocke: halber Ring aus drei schmalen Streifen, stehend, beidseitig."""
    r = zufall.uniform(0.025, 0.045)
    w = zufall.uniform(0, math.pi)
    d = Vector((math.cos(w), math.sin(w), 0))
    b = Vector((-d.y, d.x, 0)) * zufall.uniform(0.012, 0.02)
    pkt = [fuss + d * (r * math.cos(math.pi * i / 3)) + Vector((0, 0, r * math.sin(math.pi * i / 3))) for i in range(4)]
    for a, c in zip(pkt, pkt[1:]):
        quad = [a - b, c - b, c + b, a + b]
        mitte = (a + c) / 2
        aussen = mitte - (fuss + Vector((0, 0, 0)))
        netz.vieleck_nach(quad, aussen, st[stoff])
        netz.vieleck_nach([p - aussen.normalized() * 0.002 for p in quad], -aussen, st[stoff])


def spaene(netz, st, mitte, rx, ry, menge, saat, stoff="Spaene", locken=True, z0=0.004):
    """Späne um `mitte`: flache Schnipsel, bei mehr Menge Locken und ein flacher Haufen. menge 1–3."""
    zufall = random.Random(saat)
    if menge >= 2:                                                                                # flacher Haufen
        h = 0.03 * menge
        rand = [mitte + Vector((rx * 0.55 * math.cos(2 * math.pi * k / 8) * zufall.uniform(0.8, 1.1),
                                ry * 0.55 * math.sin(2 * math.pi * k / 8) * zufall.uniform(0.8, 1.1), z0 + 0.002)) for k in range(8)]
        spitze = mitte + Vector((zufall.uniform(-0.05, 0.05), 0, z0 + h))
        for i in range(8):
            a, b = rand[i], rand[(i + 1) % 8]
            netz.vieleck_nach([a, b, spitze], (a + b) / 2 - mitte + Vector((0, 0, 0.3)), st[stoff])
    for k in range(int(14 * menge ** 1.4)):                                                       # Schnipsel
        w, f = zufall.uniform(0, 2 * math.pi), math.sqrt(zufall.uniform(0, 1))
        p = mitte + Vector((rx * f * math.cos(w), ry * f * math.sin(w), z0 + 0.003 + 0.001 * (k % 5)))
        if menge >= 2 and f < 0.5:
            p.z += 0.03 * menge * (1 - f / 0.5) * 0.8
        g = zufall.uniform(0.02, 0.045)
        a = zufall.uniform(0, math.pi)
        e = [p + Vector((g * math.cos(a + 2.1 * i), g * 0.7 * math.sin(a + 2.1 * i), 0.006 * (i % 2))) for i in range(3)]
        netz.vieleck_nach(e, (0, 0, 1), st[stoff if k % 4 else "Schnittholz"])
    if locken:
        for k in range(4 * menge):
            w, f = zufall.uniform(0, 2 * math.pi), math.sqrt(zufall.uniform(0, 1))
            locke(netz, st, mitte + Vector((rx * f * math.cos(w), ry * f * math.sin(w), z0 + 0.004)), zufall, stoff)


def spaene_stand(menge, st):
    """Spaene<menge>: Hobelspäne unter der Hobelbank und Sägemehl unter dem Sägebock; Kind „…_Drechselbank“ (ab Stufe 2)."""
    z0 = 0.003 + 0.0012 * menge                                                                   # die drei Stände decken sich nicht
    netz = bs.Netz(f"Spaene{menge}")
    f = (0.55, 0.8, 1.0)[menge - 1]
    spaene(netz, st, HOBELBANK + Vector((-0.05, 0.35, 0)), 0.85 * f, 0.4 * f, menge, 11 + menge, z0=z0)
    spaene(netz, st, Vector((SAEGEBOCK.x - 0.11, SAEGEBOCK.y, 0)), 0.3 * f, 0.22 * f, menge, 21 + menge, "Saegemehl",
           locken=False, z0=z0)
    dreh = bs.Netz(f"Spaene{menge}_Drechselbank")
    spaene(dreh, st, Vector((SCHNUR_X + 0.05, DREHBANK.y + 0.2, 0)), 0.55 * f, 0.3 * f, menge, 31 + menge, z0=z0)
    o, kind = netz.objekt(), dreh.objekt()
    kind.parent = o
    kind.matrix_parent_inverse = Matrix.Identity(4)
    kind["stufe"] = 2
    bs.zustand(o)
    return o


# ─────────────────────────────────────────────────────────────────────────────
#  Holzlager
# ─────────────────────────────────────────────────────────────────────────────

def stapel(netz, st, M, laenge, breite, lagen, brett=0.05, stoff="Schnittholz"):
    """Luftig gestapelte Bretter (örtlich M, Bretter entlang x): Lagen mit Stapelleisten dazwischen, auf drei Unterlagen."""
    for dx in (-laenge * 0.4, 0, laenge * 0.4):
        netz.quader((dx, 0, 0.06), (0.14, breite + 0.08, 0.12), st["Holz alt"], M)
    z = 0.12
    zufall = random.Random(int(M.translation.x * 10 + M.translation.y * 3))
    for k in range(lagen):
        anzahl = max(2, int(breite / 0.24))
        for i in range(anzahl):
            b = breite / anzahl - 0.012
            netz.quader((zufall.uniform(-0.05, 0.05), -breite / 2 + (i + 0.5) * breite / anzahl, z + brett / 2),
                        (laenge - 0.1 * ((k + i) % 2), b, brett), st[stoff], M)
        z += brett
        if k < lagen - 1:
            for dx in (-laenge * 0.4, 0, laenge * 0.4):
                netz.quader((dx, 0, z + 0.0175), (0.04, breite + 0.04, 0.035), st["Holz alt"], M)
            z += 0.035
    return z


def kantholz(netz, st, x0, x1, y, lagen=4):
    """Kanthölzer auf Unterlagen, entlang x gestapelt."""
    for x in (x0 + 0.3, (x0 + x1) / 2, x1 - 0.3):
        netz.quader((x, y, 0.05), (0.12, 0.6, 0.1), st["Holz alt"])
    for k in range(lagen):
        for i in range(4 - k % 2):
            netz.quader(((x0 + x1) / 2 + 0.04 * (k % 2), y - 0.21 + 0.14 * i + 0.07 * (k % 2), 0.1 + 0.13 * k + 0.065),
                        (x1 - x0 - 0.1 * (i % 2), 0.12, 0.12), st["Holz" if (i + k) % 3 else "Schnittholz"])


def kisten(netz, st):
    """Kisten für fertige Ware in der vorderen rechten Ecke, darauf Schalen und ein Stuhl."""
    for (x, y, z, b, t, h) in ((8.85, -1.1, 0, 0.7, 0.6, 0.55), (8.85, -1.05, 0.55, 0.6, 0.5, 0.45), (8.05, -1.1, 0, 0.7, 0.6, 0.6),
                               (8.85, -0.35, 0, 0.6, 0.6, 0.45)):
        c = Vector((x, y, z + h / 2))
        netz.quader(c, (b, t, h), st["Holz alt"])
        for sx in (-1, 1):
            netz.quader(c + Vector((sx * (b / 2 - 0.03), 0, 0)), (0.07, t + 0.012, h + 0.012), st["Fachwerk"], ohne=("-z",))
        netz.quader(c, (b - 0.14, t + 0.008, 0.06), st["Fachwerk"], ohne=("-z", "+z", "+x", "-x"))
    for k in range(3):
        fuss = Vector((7.9 + 0.2 * k, -1.15 + 0.08 * (k % 2), 0.6))
        bs.achteck(netz, st, fuss, 0.08, 0.0, 0.025, "Schnittholz")
        bs.achteck(netz, st, fuss, 0.11, 0.025, 0.06, "Schnittholz")
    # Hocker auf der Kiste vorn
    sitz = Vector((8.85, -0.35, 0.45))
    bs.achteck(netz, st, sitz + Vector((0, 0, 0.32)), 0.16, 0.0, 0.04, "Holz")
    for k in range(3):
        w = 2 * math.pi * k / 3
        netz.stab(sitz + Vector((0.12 * math.cos(w), 0.12 * math.sin(w), 0)), sitz + Vector((0.07 * math.cos(w), 0.07 * math.sin(w), 0.32)),
                  0.035, 0.035, st["Holz"])


def holzgestell(netz, st):
    """Gekauft: Holzgestell an der rechten Wand – Ständer mit Kragarmen, darauf Bretter und Leisten."""
    x = LX1 - 0.05
    for y in (0.25, 1.75, 3.2):
        netz.quader((x - 0.06, y, 1.2), (0.1, 0.1, 2.4), st["Holz"], ohne=("+x",))
        for z in (0.45, 1.05, 1.65, 2.25):
            netz.stab((x - 0.06, y, z - 0.08), (x - 0.48, y, z), 0.06, 0.06, st["Holz alt"])
    for k, z in enumerate((0.45, 1.05, 1.65, 2.25)):
        for i in range(3 - k % 2):
            netz.quader((x - 0.2 - 0.09 * i, 1.72 + 0.05 * ((i + k) % 3), z + 0.04 + 0.03 * (i % 2)), (0.08 + 0.1 * (k % 2), 3.3, 0.03 + 0.02 * (k % 2)),
                        st["Schnittholz" if (i + k) % 2 else "Holz"])


# ─────────────────────────────────────────────────────────────────────────────
#  Sägehalle
# ─────────────────────────────────────────────────────────────────────────────

def gatter_fest(netz, st):
    """Gattersäge: Säulen mit Führungen, Kopfbalken und Streben, Schienen, zwei Wagen (Stamm davor, die Bretter dahinter),
    Lager der Welle und die Platte, durch die sie aus der Wand kommt."""
    gx, gy = GATTER.x, GATTER.y
    holz, alt, eisen = st["Fachwerk"], st["Holz alt"], st["Eisen"]
    for s in (-1, 1):
        netz.quader((gx, gy + s * 0.92, 1.3), (0.24, 0.24, 2.6), holz, ohne=("-z", "+z"))   # Kopf im Kopfbalken
        netz.quader((gx, gy + s * 0.785, 1.25), (0.1, 0.03, 1.9), eisen, ohne=("+y",) if s > 0 else ("-y",))
        for dx in (-1, 1):                                                                         # Streben
            netz.stab((gx + dx * 0.8, gy + s * 0.92, 0), (gx + dx * 0.1, gy + s * 0.92, 1.0), 0.12, 0.12, holz, quer=(0, 1, 0))
    netz.quader((gx, gy, 2.58), (0.26, 2.3, 0.24), holz)
    for s in (-1, 1):                                                                             # Schienen
        netz.quader((0.0, gy + s * 0.43, 0.05), (7.4, 0.12, 0.1), alt)
        netz.quader((0.0, gy + s * 0.43, 0.105), (7.4, 0.05, 0.01), eisen, ohne=("-z",))
    for x0, x1 in ((-2.95, gx - 0.25), (gx + 0.25, gx + 2.8)):                                    # Wagen
        for s in (-1, 1):
            netz.quader(((x0 + x1) / 2, gy + s * 0.3, 0.39), (x1 - x0, 0.14, 0.18), alt)
            for xr in (x0 + 0.3, x1 - 0.3):                                                  # Räder auf den Schienen
                drehteil(netz, st, Matrix.Translation((xr, gy + s * 0.43, 0.21)) @ Matrix.Rotation(math.radians(90), 4, "Z"),
                         [(-0.03, 0.1), (0.03, 0.1)], "Eisen")
        n = max(2, round((x1 - x0) / 1.1))
        for k in range(n + 1):
            netz.quader((x0 + 0.1 + (x1 - x0 - 0.2) * k / n, gy, 0.54), (0.12, 0.8, 0.12), alt)
    # Stamm auf dem vorderen Wagen bis an die Blätter, mit Klammern
    tischler.rundholz(netz, st, (-2.75, gy, WAGEN_Z + GSTAMM_R), (gx, gy, WAGEN_Z + GSTAMM_R), r=GSTAMM_R)
    for xk in (-2.2, gx - 0.9):
        netz.stab((xk, gy - 0.32, WAGEN_Z + 0.02), (xk, gy - 0.22, WAGEN_Z + 0.4), 0.03, 0.05, eisen)
    # gesägte Bretter auf dem hinteren Wagen: der Stamm in fünf Scheiben
    for y0, y1 in ((-0.3, -0.272), (-0.268, -0.092), (-0.088, 0.088), (0.092, 0.268), (0.272, 0.3)):
        ym = (y0 + y1) / 2
        hoehe = 2 * math.sqrt(max(GSTAMM_R ** 2 - (abs(ym) + 0.02) ** 2, 0.004))
        netz.quader((gx + 1.15, gy + ym * 1.08, WAGEN_Z + GSTAMM_R), (2.2, y1 - y0, hoehe),
                    st["Schnittholz" if abs(ym) < 0.25 else "Fachwerk"])
    # Lager der Welle an der Decke und Platte an der Wand
    netz.alle = True
    for xl in (1.5, 2.4):
        netz.stab((xl, gy, H_SAEGE), (xl, gy, WELLE_Z + 0.12), 0.12, 0.12, holz)
        netz.quader((xl, gy, WELLE_Z), (0.16, 0.36, 0.3), alt)
    netz.quader((SX1 - 0.02, gy, WELLE_Z), (0.04, 0.5, 0.5), alt, ohne=("+x",))
    netz.alle = False


def gatterrahmen(st):
    """Bewegt: der Gatterrahmen mit vier Blättern und dem Bügel fürs Pleuel (Ursprung: Mitte des Gatters am Boden,
    in Ruhe ganz oben)."""
    netz = bs.Netz("Gatterrahmen")
    holz, eisen = st["Holz"], st["Eisen"]
    for s in (-1, 1):
        netz.quader((0, s * 0.72, 1.195), (0.1, 0.08, 1.47), holz)
    netz.quader((0, 0, 0.5), (0.12, 1.36, 0.08), holz, ohne=("+y", "-y"))
    netz.quader((0, 0, 1.89), (0.12, 1.36, 0.08), holz, ohne=("+y", "-y"))
    for y in (-0.27, -0.09, 0.09, 0.27):
        netz.quader((0, y, 1.195), (0.1, 0.008, 1.31), eisen, ohne=("+z", "-z"))
    netz.quader((0.11, 0, 1.965), (0.32, 0.08, 0.07), eisen)
    o = netz.objekt()
    o.location = (GATTER.x, GATTER.y, 0)
    return o


def welle(st):
    """Bewegt: die Antriebswelle vom Göpel (kommt durch die rechte Wand), Kurbelscheibe und Kurbelzapfen
    (Ursprung auf der Achse an der Kurbelscheibe)."""
    netz = bs.Netz("Welle", alle=True)
    laenge = SX1 - KURBEL_X
    profil = [(0.11 * math.cos(math.radians(22.5 + 45 * k)), 0.11 * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    netz.prisma(profil, 0.0, laenge, st["Holz"])
    for x in (0.4, 1.0, 1.9, 2.75):                                                              # Eisenringe
        netz.prisma([(y * 1.12, z * 1.12) for y, z in profil], x, x + 0.05, st["Eisen"])
    scheibe = [(0.3 * math.cos(2 * math.pi * k / 12), 0.3 * math.sin(2 * math.pi * k / 12)) for k in range(12)]
    netz.prisma(scheibe, -0.06, 0.0, st["Holz alt"])
    for k in range(4):                                                                            # Speichenleisten
        w = math.pi / 4 + math.pi / 2 * k
        netz.stab((-0.065, 0.05 * math.cos(w), 0.05 * math.sin(w)), (-0.065, 0.27 * math.cos(w), 0.27 * math.sin(w)), 0.04, 0.01,
                  st["Eisen"], quer=(1, 0, 0))
    netz.stab((-0.06, 0, KURBEL_R), (-(KURBEL_X - PLEUEL_X) - 0.06, 0, KURBEL_R), 0.05, 0.05, st["Eisen"])
    o = netz.objekt()
    o.location = (KURBEL_X, GATTER.y, WELLE_Z)
    return o


def pleuel(st):
    """Bewegt: das Pleuel vom Bügel des Gatterrahmens zum Kurbelzapfen (Ursprung am Bügel)."""
    netz = bs.Netz("Pleuel", alle=True)
    netz.quader((0, 0, PLEUEL_L / 2), (0.06, 0.07, PLEUEL_L - 0.1), st["Holz"])
    for z in (0.0, PLEUEL_L):
        netz.quader((0, 0, z), (0.08, 0.12, 0.12), st["Eisen"])
    o = netz.objekt()
    o.location = (PLEUEL_X, GATTER.y, KURBEL_GANZ_OBEN)
    return o


KURBEL_GANZ_OBEN = WELLE_Z + KURBEL_R - PLEUEL_L       # Höhe des Bügels in Ruhe (Kurbelzapfen oben)


def gatter_bewegungen(o_rahmen, o_welle, o_pleuel):
    """Kurbeltrieb: Die Welle dreht sich je Takt einmal, der Bügel folgt dem Kurbelzapfen über das Pleuel."""
    def winkel(bild):
        return 2 * math.pi * bild / TAKT

    def buegel(bild):
        w = winkel(bild)
        neig = math.asin(KURBEL_R * math.sin(w) / PLEUEL_L)
        return WELLE_Z + KURBEL_R * math.cos(w) - PLEUEL_L * math.cos(neig), neig

    def rahmen_bewegung(bild):
        z, _ = buegel(bild)
        return (GATTER.x, GATTER.y, z - KURBEL_GANZ_OBEN), None, None

    def welle_bewegung(bild):
        return None, (winkel(bild), 0, 0), None

    def pleuel_bewegung(bild):
        z, neig = buegel(bild)
        return (PLEUEL_X, GATTER.y, z), (neig, 0, 0), None

    return [(o_rahmen, rahmen_bewegung), (o_welle, welle_bewegung), (o_pleuel, pleuel_bewegung)]


def rampe(netz, st):
    """Rampe hinten links: drei schräge Bohlen mit Stützen und Keilen, darauf Stämme, die zum Wagen hinunterrollen."""
    y0, y1 = GATTER.y + 0.55, SY1 - 0.15

    def oben(y):
        return 0.62 + (y - y0) * (0.98 - 0.62) / (y1 - y0)
    for x in (-2.6, -1.6, -0.8):
        a, b = Vector((x, y0, oben(y0) - 0.07)), Vector((x, y1, oben(y1) - 0.07))
        netz.stab(a, b, 0.16, 0.14, st["Holz alt"], quer=(0, 0, 1))
        netz.stab((x, y1 - 0.15, 0), (x, y1 - 0.15, oben(y1 - 0.15) - 0.12), 0.14, 0.14, st["Fachwerk"])
        netz.stab((x, y0 + 0.75, 0), (x, y0 + 0.75, oben(y0 + 0.75) - 0.12), 0.12, 0.12, st["Fachwerk"])
        netz.quader((x, y0 + 0.12, oben(y0 + 0.12) + 0.05), (0.1, 0.12, 0.1), st["Holz"])
    for y, r, xa, xb in ((y0 + 0.5, 0.28, -3.0, -0.6), (y0 + 1.12, 0.3, -3.1, -0.5)):
        tischler.rundholz(netz, st, (xa, y, oben(y) + r), (xb, y, oben(y) + r), r=r)


def ladetor(netz, st):
    """Ladetor in der Rückwand der Sägehalle (geschlossen): zwei Flügel in der Hausfarbe."""
    hoftor(netz, st, si.rahmen(LADETOR_X, SY1, 180), LADETOR_B, LADETOR_H)


def saegehalle(netz, st):
    """Ausstattung der Sägehalle außer der Säge: Rampe, Ladetor, Bretterstapel, Rundholz, Sägemehl, Werkzeug."""
    rampe(netz, st)
    ladetor(netz, st)
    stapel(netz, st, Matrix.Translation((-2.4, 4.55, 0)), 2.0, 0.85, 5)                         # Bretter vorn links
    for y, r in ((9.0, 0.22), (9.46, 0.22)):                                                       # Rundholz hinten rechts
        tischler.rundholz(netz, st, (1.3, y, r), (3.6, y, r), r=r)
    tischler.rundholz(netz, st, (1.5, 9.23, 0.22 + 0.36), (3.4, 9.23, 0.22 + 0.36), r=0.2)
    for x in (1.12, 3.78):
        netz.quader((x, 8.78, 0.35), (0.08, 0.08, 0.7), st["Holz alt"])
    for x, y, r in ((GATTER.x + 0.45, GATTER.y - 0.62, 0.45), (GATTER.x + 0.5, GATTER.y + 0.62, 0.4), (GATTER.x - 0.4, GATTER.y - 0.55, 0.3)):
        spaene(netz, st, Vector((x, y, 0)), r, r * 0.6, 3, int(x * 10 + y), "Saegemehl", locken=False)
    # Kanthaken und Säge an der linken Wand
    netz.stab((SX0 + 0.08, 6.0, 0.05), (SX0 + 0.12, 6.0, 1.6), 0.05, 0.05, st["Holz"])
    netz.stab((SX0 + 0.12, 6.0, 1.45), (SX0 + 0.3, 6.0, 1.3), 0.03, 0.02, st["Eisen"])
    netz.stab((SX0 + 0.08, 6.4, 0.05), (SX0 + 0.12, 6.4, 1.5), 0.06, 0.04, st["Holz"])


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen
# ─────────────────────────────────────────────────────────────────────────────

def bauen():
    import bpy
    bs.neue_szene()
    _lichter[0] = 0
    st = bs.Stoffe()
    werk, lager = bs.Netz("Raum_Werk"), bs.Netz("Raum_Lager")
    saege = bs.Netz("Raum_Saege")
    decke_w, decke_l = bs.Netz("Decke_Werk", alle=True), bs.Netz("Decke_Lager", alle=True)
    decke_s = bs.Netz("Decke_Saege", alle=True)
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
    huelle(werk, decke_w, st, WX0, WX1, WY0, WY1, H_WERK, {
        "vorne": dict(oeffnungen=W["vorne"]),
        "rechts": dict(teile=[((WX1, WY0), (WX1, LY0), dict(oeffnungen=W["rechts"][:1], enden=(True, False))),
                              ((WX1, LY0), (WX1, WY1), dict(oeffnungen=W["rechts"][1:], aussen=False))]),
        "hinten": dict(oeffnungen=W["hinten"], aussen=False),
        "links": dict(oeffnungen=W["links"]),
    })
    fachwerk_innen(werk, st, WX0, WX1, WY0, WY1, H_WERK, W)
    fenster_liste = []
    fenster_und_licht(werk, st, ecken(WX0, WX1, WY0, WY1), W, fenster_liste)
    for o in fenster_liste:
        setzen(o)
    si.bretterboden(werk, st, WX0, WX1, WY0, WY1, saat=3)
    bretter_an_wand(werk, st)
    hobelbank(teil("Bedienung_Produktion_Hobelbank"), st, HOBELBANK)
    saegebock(werk, st, SAEGEBOCK)
    laterne(werk, st, Vector((1.5, 1.0, 2.55)), H_WERK - 0.26)
    setzen(licht(Vector((1.5, 1.0, 2.4)), (0, 0, -1), "Lampe"))

    # ── Werkraum: Bedienung, Türen, Einrichtung ──────────────────────────────
    si.pult(teil("Bedienung_Personal_Buch"), st, si.rahmen(WX0 + 0.05, -2.7, -90))
    M_tisch = si.rahmen(WX1 - 0.06, -1.0, 90)                 # vor dem Fachwerk
    leimtisch(teil("Bedienung_Produktion_Leimtisch"), st, M_tisch)
    si.werkzeugwand(teil("Bedienung_Einrichten_Werkzeugwand"), st, M_tisch)
    werkzeug1(teil("Einrichtung_Werkzeug1"), st, M_tisch)
    werkzeug2(teil("Einrichtung_Werkzeug2"), st, M_tisch)
    leimzwingen(teil("Einrichtung_Leimzwingen"), st)
    hoftor(teil("Bedienung_Karren_Hoftor"), st, si.rahmen(WX0, HOFTOR_Y, -90), HOFTOR_B, HOFTOR_H)
    M_holz = si.rahmen(HOLZTUER_X, WY0, 0)
    si.bretttuer(teil("Holztuer"), st, M_holz, HOLZTUER_B, HOLZTUER_H)
    si.eisentuer(teil("Einrichtung_Eisentuer"), st, M_holz, HOLZTUER_B, HOLZTUER_H)
    si.loeschfass(teil("Einrichtung_Loeschfass"), st, Vector((2.55, -3.05, 0)))
    schuerzen(teil("Einrichtung_Schuerzen"), st, si.rahmen(-0.95, WY0, 0))
    si.bretttuer(teil("Tuer_Werk_Lager"), st, si.rahmen(WX1, TUER_Y, 90), TUER_B, TUER_H, stoff="Holz alt")
    si.bretttuer(teil("Tuer_Lager_Werk", "Lager"), st, si.rahmen(LX0, TUER_Y, -90), TUER_B, TUER_H, stoff="Holz alt")
    si.bretttuer(teil("Tuer_Werk_Saege"), st, si.rahmen(SAEGETUER_X, WY1, 180), SAEGETUER_B, SAEGETUER_H, stoff="Holz alt")
    si.bretttuer(teil("Tuer_Saege_Werk", "Saege"), st, si.rahmen(SAEGETUER_X, SY0, 0), SAEGETUER_B, SAEGETUER_H, stoff="Holz alt")

    # ── Werkraum: Arbeitsplätze ──────────────────────────────────────────────
    setzen(si.arbeit("Arbeit_1_Hobeln", (HOBELBANK.x, HOBELBANK.y + 0.65), (0, -1), 1))
    setzen(bs.anker("Werkstueck_1", (HOBELBANK.x - 0.15, HOBELBANK.y + 0.15, BANK_HOEHE + 0.08)))
    saeger = Vector((SAEGEBOCK.x + 0.1, SAEGEBOCK.y + 0.55, 0))
    setzen(si.arbeit("Arbeit_2_Saegen", saeger, (0, -1), 1))
    setzen(bs.anker("Werkstueck_2", (SAEGEBOCK.x + 0.45, SAEGEBOCK.y, STAMM_Z + STAMM_R + 0.04)))
    setzen(si.arbeit("Arbeit_3_Drechseln", (SCHNUR_X + 0.09, DREHBANK.y + 0.5), (0, -1), 2))
    setzen(bs.anker("Werkstueck_3", (DREHBANK.x + 0.73, DREHBANK.y, 0.94)), stufe=2)

    # ── Stufen (kumulativ) und bewegte Teile der Drehbank ─────────────────────
    stufen = [bs.Netz(f"Stufe{k}") for k in (1, 2, 3)]
    for k, netz in enumerate(stufen, 1):
        if k <= 2:
            gefach(netz, st)
        if k >= 2:
            drehbank(netz, st)
            drechselware(netz, st)
    bewegt = []
    stufen_obj = [s.objekt() for s in stufen]
    for k in (2, 3):
        for o, bewegung in drehbank_teile(st, k):
            o.parent = stufen_obj[k - 1]
            o.matrix_parent_inverse = Matrix.Identity(4)
            bewegt.append((o, bewegung))
    spaene_obj = [spaene_stand(m, st) for m in (1, 2, 3)]

    # ── Holzlager ─────────────────────────────────────────────────────────────
    L = LAGER_OEFFNUNGEN
    huelle(lager, decke_l, st, LX0, LX1, LY0, LY1, H_LAGER, {
        "vorne": dict(oeffnungen=L["vorne"]),
        "rechts": dict(),
        "hinten": dict(oeffnungen=L["hinten"]),
        "links": dict(oeffnungen=L["links"], aussen=False, laibung=False),
    })
    fachwerk_innen(lager, st, LX0, LX1, LY0, LY1, H_LAGER, L)
    fenster_liste = []
    fenster_und_licht(lager, st, ecken(LX0, LX1, LY0, LY1), L, fenster_liste)
    for o in fenster_liste:
        setzen(o, "Lager")
    si.bretterboden(lager, st, LX0, LX1, LY0, LY1, saat=4)
    stapel(teil("Bedienung_Lager_Bretter", "Lager"), st, Matrix.Translation((7.1, 2.95, 0)), 2.8, 0.9, 6)
    kisten(teil("Bedienung_Lager_Kisten", "Lager"), st)
    holzgestell(teil("Einrichtung_Regale", "Lager"), st)
    kantholz(lager, st, 4.9, 7.4, -1.1)
    nummer = 1
    for y in (1.55, 0.5):
        for x in (5.3, 6.25, 7.2, 8.15):
            setzen(bs.anker(f"Lager_{nummer}", (x, y, 0.0)), "Lager")
            nummer += 1

    # ── Sägehalle (ab Stufe 3) ────────────────────────────────────────────────
    S = SAEGE_OEFFNUNGEN
    huelle(saege, decke_s, st, SX0, SX1, SY0, SY1, H_SAEGE, {
        "vorne": dict(oeffnungen=S["vorne"], aussen=False, laibung=False),
        "rechts": dict(oeffnungen=S["rechts"], enden=(False, True)),
        "hinten": dict(oeffnungen=S["hinten"]),
        "links": dict(oeffnungen=S["links"], enden=(True, False)),
    })
    fachwerk_innen(saege, st, SX0, SX1, SY0, SY1, H_SAEGE, S)
    fenster_liste = []
    fenster_und_licht(saege, st, ecken(SX0, SX1, SY0, SY1), S, fenster_liste, stufe=3)
    for o in fenster_liste:
        setzen(o, "Saege")
    si.bretterboden(saege, st, SX0, SX1, SY0, SY1, saat=5)
    gatter_fest(saege, st)
    saegehalle(saege, st)
    laterne(saege, st, Vector((1.5, 8.4, 3.05)), H_SAEGE - 0.26)
    setzen(licht(Vector((1.5, 8.4, 2.9)), (0, 0, -1), "Lampe", 3), "Saege")
    o_rahmen, o_welle, o_pleuel = gatterrahmen(st), welle(st), pleuel(st)
    setzen(si.arbeit("Arbeit_4_Gatter", (-2.75 - 0.5, GATTER.y), (1, 0), 3), "Saege")
    setzen(bs.anker("Werkstueck_4", (GATTER.x - 0.35, GATTER.y, WAGEN_Z + 2 * GSTAMM_R + 0.04)), "Saege", stufe=3)

    # ── Blickpunkte ──────────────────────────────────────────────────────────
    for name, (auge, ziel) in BLICKE.items():
        raum = name.split("_")[1]
        setzen(si.blick(name, auge, ziel), raum, stufe=3 if raum == "Saege" else None)

    # ── Objekte, Zustände, Hierarchie ─────────────────────────────────────────
    raeume = {"Werk": werk.objekt(), "Lager": lager.objekt(), "Saege": saege.objekt()}
    raeume["Saege"]["stufe"] = 3
    kinder = {r: [] for r in raeume}
    kinder["Werk"] += [decke_w.objekt()] + stufen_obj + spaene_obj
    kinder["Lager"].append(decke_l.objekt())
    kinder["Saege"] += [decke_s.objekt(), o_rahmen, o_welle, o_pleuel]
    for name, (netz, raum) in teile.items():
        o = netz.objekt()
        kinder[raum].append(o)
        if name.startswith("Einrichtung_"):
            bs.zustand(o)
        if name in ("Tuer_Werk_Saege", "Tuer_Saege_Werk"):
            o["stufe"] = 3
    for o, raum in anker:
        kinder[raum].append(o)
    for raum, liste in kinder.items():
        for o in liste:
            o.parent = raeume[raum]
            o.matrix_parent_inverse = Matrix.Identity(4)
    stufen_obj[0]["zustand"] = True
    for o in stufen_obj[1:]:
        bs.zustand(o)
    bewegt += gatter_bewegungen(o_rahmen, o_welle, o_pleuel)
    animieren(bewegt)
    bpy.context.view_layer.update()
    return list(raeume.values()) + [o for liste in kinder.values() for o in liste]


def animieren(bewegt):
    """Animation „Arbeit“: je bewegtem Teil eine Action in einer NLA-Spur „Arbeit“ (der glTF-Export legt gleichnamige
    Spuren zu einer Animation zusammen). Ein Schlüssel je Bild, das letzte Bild gleicht dem ersten (nahtlose Schleife)."""
    import bpy
    for o, bewegung in bewegt:
        ruhe = (o.location.copy(), o.rotation_euler.copy(), o.scale.copy())
        o.animation_data_create()
        act = bpy.data.actions.new("Arbeit_" + o.name)
        o.animation_data.action = act
        for bild in range(ARBEIT_BILDER + 1):
            ort, dreh, mass = bewegung(bild)
            if ort is not None:
                o.location = ort
                o.keyframe_insert("location", frame=bild + 1)
            if dreh is not None:
                o.rotation_euler = dreh
                o.keyframe_insert("rotation_euler", frame=bild + 1)
            if mass is not None:
                o.scale = mass
                o.keyframe_insert("scale", frame=bild + 1)
        spur = o.animation_data.nla_tracks.new()
        spur.name = "Arbeit"
        spur.strips.new("Arbeit", 1, act)
        o.animation_data.action = None
        o.location, o.rotation_euler, o.scale = ruhe


def exportieren(objekte):
    """GLB mit der Animation „Arbeit“ (wie bausteine.exportieren, aber mit Animationen aus den NLA-Spuren)."""
    import bpy
    pfad = os.path.join(bs.MODELLE, NAME + ".glb")
    mit_kindern = set(objekte) | {k for o in objekte for k in o.children_recursive}
    for o in bpy.context.scene.objects:
        o.select_set(o in mit_kindern)
    bpy.context.scene.frame_set(1)
    bpy.context.scene.render.fps = FPS
    bpy.ops.export_scene.gltf(
        filepath=pfad, export_format="GLB", use_selection=True, export_yup=True, export_apply=True,
        export_extras=True, export_normals=True, export_texcoords=False, export_animations=True,
        export_animation_mode="NLA_TRACKS", export_skins=False, export_morph=False, export_lights=False,
        export_cameras=False, export_image_format="NONE", export_vertex_color="ACTIVE", export_all_vertex_colors=False)
    return pfad


# ─────────────────────────────────────────────────────────────────────────────
#  Sichtbarkeit wie im Spiel, Bericht
# ─────────────────────────────────────────────────────────────────────────────

def sichtbar(o, stufe, einrichtung, decke=True, spaene=0):
    """Was das Spiel bei dieser Stufe zeigt (für Vorschau und Prüfung): Stufe{k} genau, Einrichtung nach Kauf, Holztür nur
    ohne Eisentür, Spaene<n> nur der gewählte, Knoten mit extras „stufe“ erst ab dieser Stufe; Kinder folgen den Eltern."""
    while o is not None:
        n = o.name
        if n.startswith("Stufe") and n[5:].isdigit() and int(n[5:]) != stufe:
            return False
        if n.startswith("Einrichtung_") and not einrichtung:
            return False
        if n == "Holztuer" and einrichtung:
            return False
        if n.startswith("Decke_") and not decke:
            return False
        if n.startswith("Spaene") and n[6:].isdigit() and int(n[6:]) != spaene:
            return False
        if o.type == "MESH" and o.get("stufe", 0) > stufe:
            return False
        o = o.parent
    return True


def pruefbericht(objekte):
    """Dreiecke je Knoten, sichtbar je Stufe, und deckungsgleiche Flächen je Stand (Stufe, mit und ohne Einrichtung)."""
    netze = [o for o in objekte if o.type == "MESH"]
    print(f"\n{NAME}: Dreiecke")
    summe = 0
    for o in netze:
        o.data.calc_loop_triangles()
        summe += len(o.data.loop_triangles)
        print(f"  {o.name:34s} {len(o.data.loop_triangles):6d}")
    print(f"  {'gesamt':34s} {summe:6d}")
    funde = []
    for stufe in (1, 2, 3):
        for einrichtung in (False, True):
            auswahl = [o for o in netze if sichtbar(o, stufe, einrichtung, spaene=stufe)]
            dreiecke = sum(len(o.data.loop_triangles) for o in auswahl)
            print(f"  sichtbar Stufe {stufe}{' mit aller Einrichtung' if einrichtung else ''}: {dreiecke} Dreiecke")
            funde += bs.deckungsgleich(auswahl)
    funde = list({(round(f[0].x, 2), round(f[0].y, 2), round(f[0].z, 2), f[1], f[2]): f for f in funde}.values())
    if funde:
        print(f"  WARNUNG: {len(funde)} deckungsgleiche Flächen (flimmern im Spiel), größte:")
        for ort, a, b, flaeche in sorted(funde, key=lambda f: -f[3])[:12]:
            print(f"    bei ({ort.x:.2f}, {ort.y:.2f}, {ort.z:.2f})  {a} / {b}  {flaeche:.3f} m²")
    else:
        print("  keine deckungsgleichen Flächen")


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def _buehne(schnell):
    import bpy
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    bs.cycles_geraet(szene)
    szene.cycles.samples = 24 if schnell else 64
    szene.cycles.use_denoising = True
    szene.view_settings.view_transform = "Standard"
    welt = bpy.data.worlds.new("Himmel")
    try:
        welt.use_nodes = True
    except AttributeError:
        pass
    welt.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.38, 0.45, 1)
    welt.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.3
    szene.world = welt
    kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    kam.data.sensor_fit = "VERTICAL"
    kam.data.angle_y = math.radians(SICHTFELD)
    kam.data.clip_start = 0.05
    szene.collection.objects.link(kam)
    szene.camera = kam
    # Licht für die Bilder: Tageslicht an den Fenstern, Laternen, ein schwaches Füllicht unter jeder Decke
    for o in [o for o in szene.objects if o.get("art")]:
        if o["art"] == "Lampe":
            l = bpy.data.lights.new("_" + o.name, "POINT")
            l.energy, l.color, l.shadow_soft_size = 70, (1.0, 0.72, 0.42), 0.15
        else:
            l = bpy.data.lights.new("_" + o.name, "AREA")
            l.energy, l.color, l.size = 110, (0.85, 0.9, 1.0), 0.9
        lo = bpy.data.objects.new("_" + o.name, l)
        lo["stufe"] = o["stufe"]
        lo.matrix_world = o.matrix_world @ Matrix.Rotation(math.radians(90), 4, "X")      # Flächenlicht leuchtet mit −Z
        szene.collection.objects.link(lo)
    for x, y, h, e, stufe in ((0, 0, H_WERK, 140, 1), ((LX0 + LX1) / 2, (LY0 + LY1) / 2, H_LAGER, 70, 1),
                              (0, (SY0 + SY1) / 2, H_SAEGE, 160, 3)):
        l = bpy.data.lights.new("_Fuell", "AREA")
        l.energy, l.size, l.color = e, 4.0, (1.0, 0.92, 0.8)
        lo = bpy.data.objects.new("_Fuell", l)
        lo.location = (x, y, h - 0.4)
        lo["stufe"] = stufe
        szene.collection.objects.link(lo)
    p = bs.stoff("Fenster").node_tree.nodes["Principled BSDF"]
    p.inputs["Emission Color"].default_value = (0.8, 0.86, 0.95, 1)
    p.inputs["Emission Strength"].default_value = 2.5
    return szene, kam


def _zeigen(szene, stufe, einrichtung, decke=True, spaene=0):
    for o in szene.objects:
        if o.type == "MESH" and not o.name.startswith("_"):
            o.hide_render = not sichtbar(o, stufe, einrichtung, decke, spaene)
        elif o.type == "LIGHT" and "stufe" in o:
            o.hide_render = o["stufe"] > stufe


def vorschau(schnell):
    import bpy
    szene, kam = _buehne(schnell)
    szene.render.resolution_x, szene.render.resolution_y = 960, 540
    szene.render.resolution_percentage = 50 if schnell else 100
    szene.frame_set(1)
    bilder = []
    _zeigen(szene, 3, True, spaene=2)
    for name in BLICKE:
        o = bpy.data.objects[name]
        kam.matrix_world = o.matrix_world @ Matrix.Rotation(math.radians(90), 4, "X")
        bilder.append(bs.rendern_einzeln(f"_{NAME}-{name}", name))
    print(bs.zusammensetzen(NAME + "-blicke", bilder, spalten=2))
    # Grundriss von oben ohne Decke: je Stufe, dazu einer mit allen Ankern
    kam.data.type = "ORTHO"
    kam.data.sensor_fit = "AUTO"
    kam.data.ortho_scale = 14.8
    kam.matrix_world = Matrix.Translation(((WX0 + LX1) / 2, (WY0 + SY1) / 2, 30.0))
    szene.render.resolution_x, szene.render.resolution_y = 900, 900
    sonne = bpy.data.objects.new("_Sonne", bpy.data.lights.new("_Sonne", "SUN"))
    sonne.data.energy = 2.5
    sonne.rotation_euler = (math.radians(25), math.radians(-15), 0)
    szene.collection.objects.link(sonne)
    teile = []
    for k in (1, 2, 3):
        _zeigen(szene, k, False, decke=False, spaene=k)
        teile.append(bs.rendern_einzeln(f"_{NAME}-stufe{k}", f"Stufe {k}: {STUFEN[k - 1]}"))
    print(bs.zusammensetzen(NAME + "-stufen", teile, spalten=3))
    marken = si._marken()
    for o in marken:
        o.location.z += 0.7                       # über die Wände der Sägehalle
    _zeigen(szene, 3, True, decke=False, spaene=3)
    szene.render.resolution_x, szene.render.resolution_y = 1200, 1200
    print(bs.rendern_einzeln(NAME + "-grundriss", "Stufe 3 mit aller Einrichtung: Arbeitsplätze magenta, Stellplätze türkis, "
                                                 "Werkstücke orange, Blickpunkte gelb"))
    for o in marken:
        bpy.data.objects.remove(o)


def main():
    objekte = bauen()
    vorher, nachher = bs.farben_zusammenlegen(objekte)
    print(f"Materialien: {vorher} → {nachher}")
    pruefbericht(objekte)
    print("geschrieben:", exportieren(objekte))
    if "--ohne-bilder" in sys.argv:
        return
    vorschau("--schnell" in sys.argv)


if __name__ == "__main__":
    main()
