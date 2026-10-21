"""Innenraum des Parfumeurs: Siederei (Werkraum), darüber der Blütenboden unter dem Dach, ab der Duftwerkstatt der Duftladen
und ab der Parfumerie der achteckige Duftpavillon; Stufen Siederei, Duftwerkstatt, Parfumerie. Ein gehobener Betrieb: Der
Luxus wächst mit der Stufe.

    blender -b --python tools/modelle/parfumeur_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/parfumeur_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/innen/parfumeur_innen.glb und Bilder nach docs/modelle/ (parfumeur_innen-blicke.png: das Bild
jedes Blickpunkts, parfumeur_innen-stufen.png: Erdgeschoss von oben je Stufe, parfumeur_innen-grundriss.png: Erdgeschoss und
Dachboden mit Ankern).

Koordinaten wie beim Schmied (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte des
Werkraums auf dem Boden. Das Haus ist außen Fachwerk (parfumeur.glb), innen wie beim Tischler: Fachwerkwände 0,3 m mit
hellem Lehmputz in den Gefachen, Boden in Stufe 1 und 2 gestampfter Lehm, in Stufe 3 Backsteinpflaster im Fischgrätmuster,
Balkendecke:
    Werkraum    innen x −4 … 4, y −3,5 … 3,5, 3,4 m hoch. Hinten rechts der Destillierherd mit der kupfernen Brennblase,
                Helm und Kühlrohr zur Vorlage, ein gemauerter Schlot dahinter; links der Mischtisch mit Mischglas, Flakons
                und Mörser, darüber Lavendel- und Jasminbündel an Stangen; ab Stufe 2 die Spindelpresse in der Mitte rechts.
                Lila Innenläden an den Fenstern, die Treppe an der rechten Wand führt auf den Blütenboden.
    Lager       der Blütenboden darüber unter dem Dach (Boden in 3,45 m, First entlang x, Kniestock 1,3 m, 50° wie außen):
                Dielen, Fachwerk an Kniestock und Giebeln, Ladeluke mit Kranbalken im linken Giebel (zum Hof), Blütenkörbe
                am rechten Giebel, Trockengestelle, Weingeistfässer, Bündel an Leinen.
    Laden       „Duftladen“ ab Stufe 2 rechts neben dem Werkraum: x 4,3 … 9, y −3,5 … 0,5, 3,0 m hoch; Dielen, Holztäfelung
                bis Brusthöhe, darüber gekalkt, Tresen mit Messingwaage, Regale mit Flakons, Damastvorhang vor dem
                Durchgang, Tür zur Gasse (nur Kulisse).
    Pavillon    „Duftpavillon“ ab Stufe 3 hinter dem Werkraum: achteckig wie außen, innen 5,2 m von Wand zu Wand (Mitte
                x −1,2, y 6,4), 4,0 m hoch; glasierte Fliesen im Schachbrett, Wandmalerei mit Rosenranken, Kachelofen,
                Radleuchter, Butzenscheiben, Wandteppich, Rosenwasser-Brunnen, Abfülltisch, verschlossener Giftschrank.
Alle Räume sind rundum geschlossen, die Kamera steht im Raum.

Knoten im GLB – dieselben Regeln wie in schmied_innen.glb, tischler_innen.glb, steinmetz_innen.glb und alchimist_innen.glb
(Raum_…, Decke_…, Stufe1–Stufe3, Bedienung_…, Tuer_…, Einrichtung_…, Holztuer, Blick_…, Arbeit_…, Lager_…, Werkstueck_…,
Licht_…, Dampf_…, Auslage_…, Kranbalken, Himmel_Lager; Tuer_Werk_Lager ist die Treppe, Tuer_Lager_Werk die Bodenluke, beide mit
extras „schild“), dazu:
    Raum_Laden                  extras „stufe“ = 2, ebenso Tuer_Werk_Laden, Tuer_Laden_Werk (der Damastvorhang) und alle Anker
                                und Blickpunkte darin; in Stufe 1 schließt ein verputztes Gefach die Tür im Werkraum
    Raum_Pavillon               extras „stufe“ = 3, ebenso Tuer_Werk_Pavillon, Tuer_Pavillon_Werk und alles darin; in Stufe 1
                                und 2 ist die Tür ein Gefach
    Auslage_1–8                 Stellplätze der Ware im Laden (leere Knoten auf den Regalbrettern, Mitte der Brettoberkante,
                                Blick −Z in den Laden): ein Platz ist 0,85 m breit, 0,32 m tief, 0,39 m hoch bis zum nächsten
                                Brett – wie beim Alchimisten
    Werkstueck_1–4              Netze im Material „Fluessigkeit“, das das Spiel in der Farbe der Ware färbt (Ursprung in der
                                Mitte der Oberfläche): 1 im Mischglas auf dem Mischtisch, 2 in der Brennblase (zu sehen im
                                offenen Füllstutzen), mit den Kindern Destillat_2 (in der Vorlage) und Tropfen (an der Tülle
                                des Kühlrohrs), 3 in der Auffangschale der Presse (stufe 2) mit dem Kind Strahl (vom Ausguss
                                in die Schale), 4 der Flakon auf dem Abfülltisch (stufe 3, im Pavillon: der Flakon selbst ist
                                die Flüssigkeit, Hals und Lippe aus Glas stehen fest daneben)
    Dampf_1                     über dem offenen Füllstutzen der Brennblase (neben Werkstueck_2)
    Arbeit_1_Mischen, Arbeit_2_Destillieren, Arbeit_3_Pressen (stufe 2), Arbeit_4_Feinarbeit (stufe 3, im Pavillon, sitzend):
                                die Höhen passen zu figuren.MISCHGLAS, figuren.ARBEITSHOEHEN („Ofen“, „Tisch“), figuren.VORLAGE
                                und figuren.PRESSE
    Spindel2, Spindel3          bewegte Spindel der Presse mit dem Querhebel (unter Stufe2 bzw. Stufe3), Ursprung auf der
                                Achse am Boden
Animation „Arbeit“ (3 s, als Schleife zu spielen, solange gearbeitet wird; gleich lang wie die Bewegung „Pressen“): Die
Spindel schwenkt mit dem Querhebel hin und her (figuren.presse_winkel – die Figur am Platz 3 hält den Hebel, im Spiel im
Takt des Raums), an der Tülle des Kühlrohrs bildet sich zweimal ein Tropfen und fällt in die Vorlage. Ruht der Betrieb
(Zeit 0), ist kein Tropfen zu sehen; die Spindel steht in der Datei in der Mitte des Schwenks.
Lichter: extras „art“ = „Fenster“, „Lampe“ (Laternen) und „Glut“ (Feuertür des Herds, Kerzen des Radleuchters ab Stufe 3).
Materialien: „Farben“ (Eckenfarben; die Materialart für die Oberflächendetails je Fläche als UV.y, bausteine.arten_setzen –
„keine“ bei Fliesen, Putz, Malerei, Stoffen und Glas), „Fluessigkeit“ (Werkstücke), „Hausfarbe“ (Hoftor, Holztür, Ladentür
zur Gasse, Flügel der Ladeluke, Ladenschild), „Fenster“ (auch der Himmel hinter der Ladeluke), „Glut“ (Feuer, Kerzen,
Laternen), „Eisen“, „Wasser“ (Kühlfass), „Rosenwasser“ (Brunnen).
Einrichtung_Glasflakons: ein Glasschrank mit venezianischen Flakons an der Rückwand des Werkraums links (in jeder Stufe gleich:
Den Laden gibt es erst ab Stufe 2, der Schrank steht darum in der Siederei).
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import alchimist_innen as ai  # noqa: E402  (Drehkörper, Scheiben, Flaschen, Bündel, Tiegel, Ofen mit Feuertür, Werkbank, Werkzeug)
import figuren as fg  # noqa: E402  (Mischglas, Vorlage, Presse, Arbeitshöhen der Bewegungen)
import innenausbau as ia  # noqa: E402  (Böden, Decke, Treppe, Dachboden, Prüfbericht, Vorschau)
import schmied_innen as si  # noqa: E402  (Wände, Pult, Türen, Werkzeugwand, Kisten, Löschfass, Anker)
import tischler_innen as ti  # noqa: E402  (Fachwerk innen, Fenster, Lichter, Laterne, Hoftor, Animation, Export)

NAME = "parfumeur_innen"
STUFEN = ("Siederei", "Duftwerkstatt", "Parfumerie")

bs.FARBEN.update({
    "Lavendel": (0.55, 0.43, 0.78),         # wie außen (parfumeur.py)
    "Lavendel trocken": (0.49, 0.43, 0.60),
    "Jasmin": (0.96, 0.95, 0.88),
    "Laden lila": (0.42, 0.30, 0.58),       # lila Akzente wie die Fensterläden außen
    "Messing": (0.80, 0.64, 0.30),
    "Flakon blau": (0.18, 0.30, 0.62),      # venezianische Flakons (deckend gemalt)
    "Flakon gruen": (0.24, 0.52, 0.40),
    "Flakon rot": (0.62, 0.16, 0.22),
    "Flakon bernstein": (0.80, 0.52, 0.18),
    "Flakon milch": (0.88, 0.87, 0.84),
    "Flakon lila": (0.56, 0.46, 0.74),
    "Scheibenglas": (0.62, 0.70, 0.70),     # Türen des Glasschranks
    "Blei": (0.28, 0.29, 0.31),             # Bleiruten der Butzenscheiben
    "Fliese gelb": (0.82, 0.68, 0.38),      # glasierte Bodenfliesen im Pavillon
    "Fliese gruen": (0.20, 0.36, 0.27),
    "Fliesenfuge": (0.55, 0.52, 0.46),
    "Wandputz": (0.88, 0.84, 0.74),         # heller Kalk im Pavillon (bemalt)
    "Sockelrot": (0.52, 0.18, 0.16),        # gemalter Sockel
    "Ranke": (0.30, 0.43, 0.22),            # Wandmalerei: Stiele und Fries
    "Rosenblatt": (0.22, 0.38, 0.19),
    "Rosenbluete": (0.78, 0.30, 0.38),
    "Rosenknospe": (0.90, 0.62, 0.66),
    "Ofenkachel": (0.26, 0.45, 0.34),       # grün glasierte Kacheln
    "Ofenkachel dunkel": (0.15, 0.29, 0.21),
    "Damast": (0.46, 0.10, 0.14),           # Vorhang im Laden
    "Damast hell": (0.66, 0.24, 0.26),
    "Teppich blau": (0.15, 0.20, 0.38),     # Wandteppich
    "Teppich rot": (0.55, 0.13, 0.14),
    "Teppich gold": (0.80, 0.63, 0.28),
    "Taeferholz": (0.45, 0.31, 0.19),       # Täfelung im Laden (Holzmaserung aus den Oberflächendetails)
    "Taeferholz dunkel": (0.33, 0.22, 0.13),
    "Kerze": (0.93, 0.89, 0.76),
    "Rosenwasser": (0.86, 0.60, 0.64),      # Wasser im Brunnen (eigenes Material, glänzt)
})
bs.RAUHEIT["Rosenwasser"] = 0.1

# ── Maße ─────────────────────────────────────────────────────────────────────
WAND = ti.WAND                                                     # Fachwerkwand mit Putz, 0,3 m
WX0, WX1, WY0, WY1 = -4.0, 4.0, -3.5, 3.5                          # Werkraum innen
H_WERK = 3.4
Z_LAGER = H_WERK + ia.DIELE                                        # Boden des Blütenbodens unter dem Dach
DACH = ia.Dachboden(WX0, WX1, WY0, WY1, Z_LAGER, 50)
LX0, LX1, LY0, LY1 = WX1 + WAND, 9.0, WY0, 0.5                     # Duftladen innen (ab Stufe 2)
H_LADEN = 3.0
PA = 2.6                                                           # Pavillon: Abstand der Wände von der Mitte (innen)
PR = PA / math.cos(math.radians(22.5))                             # … und der Ecken
PM = Vector((-1.2, WY1 + WAND + PA, 0))                            # Mitte des Pavillons (ab Stufe 3)
H_PAV = 4.0
TREPPE_X, TREPPE_FUSS, TREPPE_OBEN = 3.60, 2.7, 1.45               # steile Treppe an der rechten Wand, steigt nach vorn
LUKE = (3.22, WX1, 1.41, 2.45)                                     # Treppenloch in der Decke (x0, x1, y0, y1)
LADELUKE_Y = -1.1                                                  # Ladeluke im linken Giebel (Mitte), 1,2 × 1,8 m
LADENTUER_Y, LADENTUER_B, LADENTUER_H = -2.3, 1.0, 2.2             # Tür Werkraum – Laden (rechte Wand)
PAVTUER_B, PAVTUER_H = 1.0, 2.2                                    # Tür Werkraum – Pavillon (Rückwand, bei PM.x)
HOFTOR_Y, HOFTOR_B, HOFTOR_H = -0.3, 2.6, 2.8                      # Hoftor in der linken Wand
HOLZTUER_X, HOLZTUER_B, HOLZTUER_H = -1.6, 0.95, 2.1               # Tür zum Hof in der Vorderwand
GASSENTUER_X = 5.3                                                 # Ladentür zur Gasse (nur Kulisse)
FPS = 24
ARBEIT_BILDER = round(fg.BEWEGUNGEN["Pressen"] * FPS)              # Länge der Animation „Arbeit“ (3 s)

# Arbeitsplätze (Fußpunkt der Figur) und Geräte – die Höhen kommen aus figuren.py
W1 = Vector((-2.3, 1.55, 0))                    # Mischen am Mischtisch, Blick −y (die linke Seite der Figur ist +x)
GLAS = W1 + Vector((fg.MISCHGLAS[0], -fg.MISCHGLAS[1], 0))         # Mitte des Mischglases am Boden
W2 = Vector((0.4, 2.42, 0))                     # Destillieren, Blick +x (die linke Seite ist +y)
HERD_VOR, HERD_T, HERD_B, HERD_H = 0.46, 0.8, 0.89, 0.75           # Vorderseite vor der Figur, Tiefe, Breite, Höhe
HERD = W2 + Vector((HERD_VOR + HERD_T / 2, 0.155, 0))              # Mitte des Herds am Boden (y 2,13 … 3,02)
BLASE = Vector((HERD.x, 2.5, HERD_H))           # Brennblase auf dem Herd (Mitte unten)
VORLAGE = W2 + Vector((fg.VORLAGE[1], fg.VORLAGE[0], 0))           # Blick +x: vor → +x, rechts → −y
KSCHLOT = (HERD.x - 0.3, HERD.x + 0.3, 3.0, WY1)                  # gemauerter Schlot hinter dem Herd, durch Decke und Dachboden
W3 = Vector((1.3, 0.6, 0))                      # Pressen, Blick +x
SPINDEL = W3 + Vector((fg.PRESSE["abstand"], 0, 0))                # Achse der Spindelpresse am Boden
SCHALE = SPINDEL + Vector((0.3, -0.62, 0))      # Auffangschale vor dem Presskasten
STUTZEN = BLASE + Vector((-0.6 * 0.255, -0.8 * 0.255, 0))         # offener Füllstutzen auf der Schulter der Blase
GIFTSCHRANK_WAND, OFEN_WAND, TEPPICH_WAND = 5, 3, 4                # Wände des Pavillons (0 vorn, 2 rechts, 4 hinten, 6 links)

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Werk_0": ((0.4, -3.1, 2.7), (-0.7, 1.8, 0.9)),          # Mischtisch, Herd mit Blase, Presse, Tür zum Pavillon
    "Blick_Werk_L1": ((1.8, -2.4, 2.6), (-4.0, 0.2, 0.9)),         # Hoftor, Personalbuch, Mischtisch
    "Blick_Werk_L2": ((3.0, -1.0, 2.7), (-3.6, -3.2, 0.8)),        # Holztür, Personalbuch, Löschfass
    "Blick_Werk_R1": ((-1.0, -3.0, 2.7), (3.4, 2.4, 0.9)),         # schräg: Herd, Presse, Treppe
    "Blick_Werk_R2": ((-2.0, -1.6, 2.6), (4.0, -0.9, 1.0)),        # Werkzeugwand, Tür zum Laden
    "Blick_Lager_0": ((3.3, -1.5, Z_LAGER + 2.1), (-3.2, -0.5, Z_LAGER + 0.7)),     # den First entlang: Ladeluke, Trockengestelle
    "Blick_Lager_L1": ((-3.2, -2.5, Z_LAGER + 2.0), (3.8, 0.6, Z_LAGER + 0.9)),     # zurück: Blütenkörbe am Giebel
    "Blick_Lager_R1": ((2.5, -0.25, Z_LAGER + 2.2), (3.6, 1.95, Z_LAGER - 0.6)),    # hinunter in die Bodenluke mit der Treppe
    "Blick_Laden_0": ((5.0, -3.1, 2.3), (7.2, 0.4, 1.0)),          # Tresen, Regale, Schild
    "Blick_Laden_L1": ((8.6, -3.0, 2.3), (4.7, -0.6, 1.0)),        # Regale, Damastvorhang
    "Blick_Laden_R1": ((5.0, -0.3, 2.3), (8.8, -2.9, 1.0)),        # Tür zur Gasse, Schaufenster, Tresen
    "Blick_Pavillon_0": ((PM.x, 4.25, 2.9), (PM.x + 0.1, 8.0, 1.0)),               # Abfülltisch, Wandteppich, Radleuchter
    "Blick_Pavillon_L1": ((0.4, 4.9, 2.8), (-3.4, 7.6, 1.3)),      # Giftschrank, Fenster, Wandmalerei
    "Blick_Pavillon_R1": ((-3.0, 4.9, 2.8), (1.0, 7.6, 1.0)),      # Kachelofen, Brunnen, Fenster
}

Oe = si.Oe
WERK_OEFFNUNGEN = {
    "vorne": [Oe(0.4, 1.2, 0.9, 1.1), Oe(2.6, 1.2, 0.9, 1.1), Oe(HOLZTUER_X, 0.0, HOLZTUER_B, HOLZTUER_H)],
    "rechts": [Oe(LADENTUER_Y, 0.0, LADENTUER_B, LADENTUER_H)],
    "hinten": [Oe(2.75, 1.3, 0.8, 1.1), Oe(PM.x, 0.0, PAVTUER_B, PAVTUER_H)],
    "links": [Oe(HOFTOR_Y, 0.0, HOFTOR_B, HOFTOR_H), Oe(2.4, 1.3, 0.8, 1.1)],
}
LADEN_OEFFNUNGEN = {
    "vorne": [Oe(GASSENTUER_X, 0.0, 1.0, 2.1), Oe(7.6, 0.85, 1.4, 1.3)],
    "rechts": [Oe(-1.5, 1.1, 0.8, 1.2)],
    "hinten": [],
    "links": [Oe(LADENTUER_Y, 0.0, LADENTUER_B, LADENTUER_H)],
}
PAV_FENSTER = {2: [Oe(PM.y, 0.9, 0.75, 1.6)], 6: [Oe(PM.y, 0.9, 0.75, 1.6)]}   # Wand k liegt bei −90° + 45° · k
PAV_TUER = Oe(PM.x, 0.0, PAVTUER_B, PAVTUER_H)
SCHILDER = {"Tuer_Werk_Lager": "Treppe zum Blütenboden", "Tuer_Lager_Werk": "Treppe hinunter zum Werkraum"}   # extras „schild“

ecken = ia.ecken
drehkoerper, scheibe, am_ort = ai.drehkoerper, ai.scheibe, ai.am_ort


# ─────────────────────────────────────────────────────────────────────────────
#  Hilfen
# ─────────────────────────────────────────────────────────────────────────────

def licht(punkt, richtung, art, stufe=1):
    """Licht_<n> (fortlaufend, über tischler_innen.licht): „Fenster“ blickt in den Raum, „Lampe“ nach unten."""
    return ti.licht(punkt, richtung, art, stufe)


def kind(o, eltern):
    """Hängt o unter `eltern`, ohne dass es sich in der Welt bewegt."""
    o.parent = eltern
    o.matrix_parent_inverse = Matrix.Identity(4)
    o.location -= eltern.location
    return o


def wq(netz, st, wand, u0, u1, v0, v1, w0, w1, stoff, ohne=("-y",)):
    """Quader an einer Wand (innenausbau.Wand): u entlang, v hoch, w in den Raum; `ohne` wie bei Netz.quader in diesem System
    (−y liegt an der Wand)."""
    M = Matrix.Translation(wand.a) @ Matrix((wand.d, wand.n, Vector((0, 0, 1)))).transposed().to_4x4()
    netz.quader(((u0 + u1) / 2, (w0 + w1) / 2, (v0 + v1) / 2), (u1 - u0, w1 - w0, v1 - v0), st[stoff], M, ohne=ohne)


def freie_stuecke(wand, v0, v1, rand=0.02, frei=()):
    """Stücke [(u0, u1)] entlang der Wand zwischen den Höhen v0 und v1, die keine Öffnung (und kein Stück aus `frei`) schneidet."""
    stuecke = [(0.0, wand.laenge)]
    sperren = [(m - o.b / 2 - rand, m + o.b / 2 + rand) for m, o in wand.ops if o.v < v1 and o.v + o.h > v0] + list(frei)
    for a, b in sperren:
        stuecke = [s for x0, x1 in stuecke for s in ((x0, min(x1, a)), (max(x0, b), x1)) if s[1] - s[0] > 0.04]
    return stuecke


def huelle(netz, decke, st, x0, x1, y0, y1, hoehe, waende, luke=None, oben=None, schlote=(), saat=0):
    """Wände aus hellem Lehmputz (wie tischler_innen.huelle) mit Balkendecke, dazu Löcher für Schlote in der Decke."""
    kanten = ecken(x0, x1, y0, y1)
    netz.alle = True
    for seite, angaben in waende.items():
        si.wand(netz, st, *kanten[seite], hoehe, stoff="Lehmputz hell", dicke=WAND, **angaben)
    netz.alle = False
    return ia.balkendecke(decke, st, x0, x1, y0, y1, hoehe, luke=luke, schlote=schlote, oben=oben,
                          abdeckung=None if oben is not None else WAND, saat=saat)


def gefach(netz, st, a, b, o):
    """Die Türöffnung o der Fachwerkwand a → b (Raum links) ist ein verputztes Gefach mit Riegel und Strebe – so lange es den
    Raum dahinter noch nicht gibt."""
    wand = ia.Wand(a, b, [o])
    u = wand.ops[0][0]
    u0, u1 = u - o.b / 2, u + o.b / 2
    netz.vieleck_nach([wand.p(u0, 0, -0.02), wand.p(u1, 0, -0.02), wand.p(u1, o.h, -0.02), wand.p(u0, o.h, -0.02)], wand.n,
                      st["Lehmputz hell"])
    netz.stab(wand.p(u0 - 0.02, 0.9, 0.0), wand.p(u1 + 0.02, 0.9, 0.0), bs.B, 0.08, st["Fachwerk"], quer=wand.n)
    netz.stab(wand.p(u0 + 0.06, bs.B / 2, -0.006), wand.p(u1 - 0.06, 0.9 - bs.B / 2, -0.006), bs.B, 0.08, st["Fachwerk"],
              quer=wand.n)


def flakon(netz, st, fuss, h=0.16, r=0.045, stoff="Flakon blau", form=0, n=6):
    """Venezianischer Flakon: 0 bauchig mit langem Hals, 1 hoch und schlank, 2 kugelig; Stopfen in Messing; `n` Ecken."""
    z = fuss[2]
    if form == 0:
        profil = [(0.0, r * 0.6), (0.02, r), (h * 0.45, r), (h * 0.6, r * 0.3), (h, r * 0.35)]
    elif form == 1:
        profil = [(0.0, r * 0.75), (h * 0.7, r * 0.75), (h * 0.8, r * 0.3), (h, r * 0.3)]
    else:
        profil = [(0.0, r * 0.4), (h * 0.2, r), (h * 0.55, r * 0.7), (h * 0.8, r * 0.2), (h, r * 0.3)]
    drehkoerper(netz, st, fuss, profil, stoff, n=n)
    scheibe(netz, st, fuss, profil[-1][1], z + h, "Messing", n=n)


def buendel_band(netz, st, oben, laenge, stoff, band="Laden lila"):
    """Blütenbündel kopfüber (alchimist_innen.buendel) mit einem lila Band um die Stiele."""
    ai.buendel(netz, st, oben, laenge=laenge, stoff=stoff)
    drehkoerper(netz, st, Vector(oben) - Vector((0, 0, laenge * 0.22 + 0.03)), [(0.0, 0.043), (0.03, 0.043)], band, n=5)


# ─────────────────────────────────────────────────────────────────────────────
#  Werkraum: Destillierherd mit Brennblase, Mischtisch, Presse
# ─────────────────────────────────────────────────────────────────────────────

def destille(netz, st, anker):
    """Destillierherd: gemauerter Herd mit Feuertür und Schieber zur Figur (Platz 2, Bewegung „Destillieren“ des Alchimisten),
    darauf die kupferne Brennblase mit offenem Füllstutzen, der Helm (Mohrenkopf) und das Kühlrohr mit Kühlmantel hinunter zur
    gläsernen Vorlage auf einem Schemel rechts vor der Figur. Gibt Werkstueck_2 (Flüssigkeit im Stutzen, mit Destillat_2 und
    Tropfen) und den Tropfen zurück."""
    hoehe_o = fg.ARBEITSHOEHEN["Ofen"][1]
    tuer_u = (W2.y - 0.12) - HERD.y                               # Feuertür 12 cm rechts der Figur (örtlich x = Welt +y)
    glut, n, M = ai.ofen_mit_tuer(netz, st, HERD, HERD_B, HERD_T, HERD_H, tuer_u, (0.13, 0.43, 0.26), vorn=(-1, 0))
    anker.append(licht(glut, (n.x, n.y, 0), "Glut"))
    e = st["Eisen"]
    netz.quader((tuer_u, HERD_T / 2 + 0.02, hoehe_o), (0.24, 0.04, 0.06), e, M)            # Schieber
    netz.quader((tuer_u + 0.1, HERD_T / 2 + 0.05, hoehe_o), (0.03, 0.04, 0.04), st["Holz alt"], M)
    netz.quader((HERD.x, HERD.y, HERD_H + 0.015), (HERD_T + 0.06, HERD_B + 0.04, 0.03), st["Sandstein"], ohne=("-z",))   # Deckplatte
    b = BLASE + Vector((0, 0, 0.03))
    profil = [(0.0, 0.29), (0.05, 0.36), (0.2, 0.38), (0.4, 0.36), (0.53, 0.27), (0.6, 0.16), (0.64, 0.13), (0.68, 0.13)]
    drehkoerper(netz, st, b, profil, "Kupferblech", n=16)
    for z in (0.12, 0.3):                                                                   # Nietreihen
        drehkoerper(netz, st, b, [(z, 0.372 + (0.008 if z < 0.2 else 0.0)), (z + 0.025, 0.372 + (0.008 if z < 0.2 else 0.0))],
                    "Messing", n=16)
    helm = b + Vector((0, 0, 0.66))
    drehkoerper(netz, st, helm, [(0.0, 0.15), (0.04, 0.3), (0.18, 0.32), (0.3, 0.25), (0.38, 0.12), (0.41, 0.0)], "Kupferblech", n=16)
    drehkoerper(netz, st, helm, [(0.02, 0.152), (0.06, 0.152)], "Messing", n=16)          # Ring an der Fuge
    # Füllstutzen: kurzer Hals mit offenem Deckel, innen ein dunkler Boden unter der Flüssigkeit
    s = STUTZEN + Vector((0, 0, 0.03))
    drehkoerper(netz, st, s, [(0.42, 0.06), (0.58, 0.06), (0.58, 0.052)], "Kupferblech", n=10)
    drehkoerper(netz, st, s, [(0.5, 0.052), (0.58, 0.052)], "Kupferblech", n=10, innen=True)
    scheibe(netz, st, s, 0.052, s.z + 0.5, "Loch", n=10)
    d = Vector((STUTZEN.x - BLASE.x, STUTZEN.y - BLASE.y, 0)).normalized()
    q = Vector((-d.y, d.x, 0))
    angel = s + Vector((0, 0, 0.58)) - d * 0.06
    deckel = [angel + q * (0.065 * math.cos(2 * math.pi * k / 10)) + (Vector((0, 0, 1)) * 0.95 - d * 0.3).normalized() *
              (0.065 + 0.065 * math.sin(2 * math.pi * k / 10)) for k in range(10)]   # hochgeklappt, zum Helm geneigt
    netz.vieleck(deckel, st["Kupferblech"], immer=True)
    netz.vieleck(list(reversed(deckel)), st["Kupferblech"], immer=True)
    # Kühlrohr vom Schnabel des Helms zur Vorlage, mit Kühlmantel; Tülle über dem Trichter
    zum = Vector((VORLAGE.x - BLASE.x, VORLAGE.y - BLASE.y, 0)).normalized()
    anfang = helm + Vector((0, 0, 0.17)) + zum * 0.27
    ende = Vector((VORLAGE.x, VORLAGE.y, 1.2))
    netz.stab(anfang - zum * 0.06, ende, 0.04, 0.04, st["Kupferblech"], quer=(0, 0, 1))
    mitte = anfang.lerp(ende, 0.5)
    r = (ende - anfang).normalized()
    netz.stab(mitte - r * 0.13, mitte + r * 0.13, 0.11, 0.11, st["Kupferblech"], quer=(0, 0, 1))       # Kühlmantel
    for t in (-0.13, 0.13):
        netz.stab(mitte + r * (t - 0.01), mitte + r * (t + 0.01), 0.125, 0.125, st["Messing"], quer=(0, 0, 1))
    netz.stab(ende, ende + Vector((0, 0, -0.045)), 0.022, 0.022, st["Kupferblech"])           # Tülle
    # Schemel mit der Vorlage (bauchige Glasflasche mit Trichter)
    v = VORLAGE
    scheibe(netz, st, v, 0.15, 0.5, "Holz", n=8)
    for i in range(3):
        w = math.radians(30 + 120 * i)
        netz.stab(v + Vector((0.13 * math.cos(w), 0.13 * math.sin(w), 0)), v + Vector((0.09 * math.cos(w), 0.09 * math.sin(w), 0.49)),
                  0.04, 0.04, st["Holz"])
    drehkoerper(netz, st, v + Vector((0, 0, 0.5)), [(0.0, 0.06), (0.025, 0.12), (0.1, 0.14), (0.19, 0.11), (0.25, 0.04), (0.47, 0.03),
                                                    (0.47, 0.03), (0.53, 0.07), (0.53, 0.06)], "Glaskolben", n=10)
    drehkoerper(netz, st, v + Vector((0, 0, 0.5)), [(0.48, 0.03), (0.53, 0.06)], "Glaskolben", n=10, innen=True)
    scheibe(netz, st, v, 0.03, 0.98, "Loch", n=10)
    # Werkstueck_2: die Flüssigkeit im Füllstutzen, dazu Destillat_2 (Band im Bauch der Vorlage) und der Tropfen an der Tülle
    werkstueck = bs.Netz("Werkstueck_2")
    scheibe(werkstueck, st, s, 0.051, s.z + 0.545, "Fluessigkeit", n=10)
    o = am_ort(werkstueck, s + Vector((0, 0, 0.545)))
    destillat = bs.Netz("Destillat_2")
    drehkoerper(destillat, st, v + Vector((0, 0, 0.5)), [(0.004, 0.121), (0.06, 0.141), (0.1, 0.1415)], "Fluessigkeit", n=10)
    kind(am_ort(destillat, v + Vector((0, 0, 0.56))), o)
    tropfen = bs.Netz("Tropfen", alle=True)
    t0 = Vector((v.x, v.y, 1.15))
    drehkoerper(tropfen, st, t0, [(-0.016, 0.0), (-0.008, 0.012), (0.004, 0.009), (0.014, 0.0)], "Fluessigkeit", n=6)
    o_tropfen = kind(am_ort(tropfen, t0), o)
    return o, o_tropfen, s + Vector((0, 0, 0.6))


def tropfen_bewegung(o):
    """Der Tropfen hängt an der Tülle und wächst (1,1 s), fällt in den Trichter der Vorlage (0,2 s) und ist weg; zweimal je
    Schleife, bei Zeit 0 unsichtbar."""
    ruhe = o.location.copy()
    fallen = 1.15 - 0.99

    def bewegung(bild):
        t = bild / FPS
        for anfang in (0.2, 1.7):
            a = t - anfang
            if 0 <= a < 1.1:
                s = 0.25 + 0.75 * fg.weich(a / 1.1)
                return ruhe, None, (s, s, s * (1 + 0.25 * fg.weich(a / 1.1)))
            if 1.1 <= a < 1.3:
                f = (a - 1.1) / 0.2
                return ruhe - Vector((0, 0, fallen * f * f)), None, (0.9, 0.9, 1.35)
        return ruhe, None, (0.01, 0.01, 0.01)
    o.scale = (0.01, 0.01, 0.01)
    return [(o, bewegung)]


def schlot(netz, st, z0, z1):
    """Gemauerter Schlot hinter dem Herd an der Rückwand: drei Seiten aus Ziegeln (die Rückseite ist die Wand), von z0 bis z1;
    oberhalb des Raums bis unter die Dachfläche."""
    x0, x1, y0, y1 = KSCHLOT
    m = st["Ziegelmauer"]
    netz.vieleck_nach([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, -1, 0), m)
    for x, n in ((x0, -1), (x1, 1)):
        netz.vieleck_nach([(x, y0, z0), (x, y1, z0), (x, y1, z1), (x, y0, z1)], (n, 0, 0), m)


def schlot_dachboden(netz, st):
    """Der Schlot im Blütenboden: vom Boden durch die hintere Dachfläche."""
    x0, x1, y0, y1 = KSCHLOT
    m = st["Ziegelmauer"]
    za, ze = H_WERK, DACH.dach_z(y0) + 0.15
    netz.vieleck_nach([(x0, y0, za), (x1, y0, za), (x1, y0, ze), (x0, y0, ze)], (0, -1, 0), m)
    for x, n in ((x0, -1), (x1, 1)):
        netz.vieleck_nach([(x, y0, za), (x, y1, za), (x, y1, DACH.dach_z(y1) + 0.15), (x, y0, ze)], (n, 0, 0), m)


def kuehlfass(netz, st):
    """Offenes Kühlfass mit Wasser neben dem Schlot, eine Schöpfkelle darin; ein Stoß Brennholz davor."""
    ai.offenes_fass(netz, st, (2.15, 3.0, 0), "Wasser", r=0.3, h=0.78)
    netz.stab((2.05, 2.95, 0.72), (1.9, 2.82, 1.1), 0.03, 0.03, st["Holz alt"])
    zufall = random.Random(11)
    for lage in range(3):
        for k in range(4 - lage):
            y = 2.05 + 0.15 * k + 0.075 * lage
            z = 0.075 + 0.13 * lage
            netz.stab((1.85 + zufall.uniform(-0.03, 0.03), y, z), (2.45 + zufall.uniform(-0.03, 0.03), y, z), 0.13, 0.13, st["Holz"])


def mischtisch(netz, st):
    """Mischtisch: Platte auf Böcken mit einem lila Läufer, darauf das Mischglas (vor der Figur, Platz 1), Glasflakons, ein
    Messingmörser, Schalen mit Blüten, ein Trichter und ein Rezept; unten ein Bord mit Blütenkörben. Gibt das Werkstück
    (Flüssigkeit im Mischglas) zurück."""
    holz = st["Holz"]
    tx, ty = W1.x, W1.y - 0.62
    netz.quader((tx, ty, 0.75), (1.8, 0.75, 0.06), holz)
    for x in (tx - 0.8, tx + 0.8):
        for y in (ty - 0.3, ty + 0.3):
            netz.stab((x, y, 0), (x, y, 0.72), 0.07, 0.07, holz)
        netz.quader((x, ty, 0.2), (0.06, 0.62, 0.06), holz)
    netz.quader((tx, ty, 0.24), (1.66, 0.6, 0.03), st["Holz alt"])                          # Bord
    for i, x in enumerate((tx - 0.5, tx + 0.15)):                                           # Körbe mit Blüten
        bs.achteck(netz, st, Vector((x, ty, 0.255)), 0.2, 0.0, 0.2, "Holz alt")
        bs.rund_oben(netz, st, (x, ty), 0.18, 0.458 + 0.004 * i, ("Lavendel", "Jasmin")[i])
    netz.quader((tx, ty, 0.7805), (1.8, 0.3, 0.001), st["Laden lila"], ohne=("-z", "+x", "-x", "+y", "-y"))   # Läufer
    z = 0.781
    # Mischglas: Becher aus Glas, innen ein dunkler Boden unter der Flüssigkeit
    g = Vector((GLAS.x, GLAS.y, 0.78))
    rand = fg.MISCHGLAS[2] - 0.78
    drehkoerper(netz, st, g, [(0.0, 0.058), (0.012, 0.07), (rand - 0.01, 0.07), (rand, 0.076), (rand, 0.068)], "Glaskolben", n=10)
    drehkoerper(netz, st, g, [(rand - 0.08, 0.068), (rand, 0.068)], "Glaskolben", n=10, innen=True)
    scheibe(netz, st, g, 0.068, 0.78 + rand - 0.08, "Loch", n=10)
    # Flakons, Mörser, Schalen, Trichter, Rezept
    for i, (dx, dy, stoff, form) in enumerate(((0.35, -0.2, "Flakon blau", 0), (0.47, -0.12, "Flakon gruen", 1),
                                               (0.6, -0.22, "Flakon bernstein", 2), (-0.7, -0.18, "Flakon rot", 0),
                                               (-0.55, -0.24, "Flakon milch", 1), (0.72, 0.05, "Flakon lila", 0))):
        flakon(netz, st, (tx + dx, ty + dy, z), h=0.15 + 0.03 * (i % 2), r=0.04, stoff=stoff, form=form)
    m = Vector((tx + 0.3, ty + 0.15, z))
    drehkoerper(netz, st, m, [(0.0, 0.07), (0.02, 0.085), (0.1, 0.08), (0.12, 0.085), (0.12, 0.06)], "Messing", n=10)
    scheibe(netz, st, m, 0.06, z + 0.1, "Loch", n=10)
    netz.stab(m + Vector((0, 0, 0.06)), m + Vector((0.06, 0.05, 0.25)), 0.03, 0.03, st["Messing"])
    for dx, dy, inhalt in ((-0.25, -0.2, "Rose"), (-0.05, -0.24, "Lavendel"), (0.12, -0.2, "Jasmin")):
        ai.tiegel(netz, st, (tx + dx, ty + dy, z), r=0.08, h=0.045, stoff="Steingut", inhalt=inhalt)
    t = Vector((tx - 0.35, ty + 0.2, z))                                                   # Trichter auf einem Ständer
    drehkoerper(netz, st, t, [(0.0, 0.05), (0.1, 0.012), (0.1, 0.012)], "Holz", n=6)
    drehkoerper(netz, st, t + Vector((0, 0, 0.1)), [(0.0, 0.008), (0.03, 0.01), (0.12, 0.07), (0.13, 0.07)], "Messing", n=8)
    netz.quader((tx - 0.72, ty + 0.15, z + 0.004), (0.24, 0.3, 0.006), st["Pergament"])
    werkstueck = bs.Netz("Werkstueck_1")
    scheibe(werkstueck, st, g, 0.067, 0.78 + rand - 0.035, "Fluessigkeit", n=10)
    return am_ort(werkstueck, g + Vector((0, 0, rand - 0.035)))


def stangen_mit_buendeln(netz, st):
    """Zwei Stangen über dem Mischtisch, an Seilen von der Decke, voller Lavendel- und Jasminbündel kopfüber."""
    zufall = random.Random(5)
    for y, z in ((W1.y - 0.95, 2.45), (W1.y + 0.55, 2.6)):
        x0, x1 = W1.x - 1.0, W1.x + 1.0
        netz.stab((x0, y, z), (x1, y, z), 0.06, 0.06, st["Holz"])
        for x in (x0 + 0.1, x1 - 0.1):
            netz.stab((x, y, z), (x, y, H_WERK), 0.02, 0.02, st["Seil"])
        for k in range(9):
            x = x0 + 0.14 + (x1 - x0 - 0.28) * k / 8
            buendel_band(netz, st, (x + zufall.uniform(-0.02, 0.02), y + zufall.uniform(-0.02, 0.02), z - 0.03),
                         zufall.uniform(0.3, 0.45), ("Lavendel", "Jasmin", "Lavendel trocken", "Lavendel", "Rose")[k % 5])


def glasschrank(netz, st):
    """Gekauft: Glasschrank mit venezianischen Flakons an der Rückwand links – Gehäuse aus dunkler Eiche mit Kranz, lila
    gefasste Füllungen unten, zwei Glastüren weit geöffnet, vier Böden voller bunter Flakons."""
    M = si.rahmen(-3.2, WY1 - 0.06, 180)                       # vor dem Fachwerk der Rückwand, +y in den Raum
    b, t, h = 1.1, 0.42, 2.05
    q = lambda mitte, masse, stoff, ohne=(): si.q(netz, st, M, mitte, masse, stoff, ohne)   # noqa: E731
    for s in (-1, 1):
        q((s * (b / 2 - 0.025), t / 2, h / 2), (0.05, t, h), "Fachwerk", ("-y",))
    innen = b - 0.104                                                                       # zwischen den Seiten
    q((0, 0.015, h / 2), (innen, 0.03, h), "Laden lila", ("-y", "+x", "-x"))                # Rückwand innen
    q((0, t / 2, 0.05), (innen, t, 0.1), "Fachwerk", ("-y", "+x", "-x"))
    q((0, t / 2 + 0.02, h + 0.04), (b + 0.12, t + 0.08, 0.08), "Fachwerk", ("-y",))         # Kranz
    q((0, t / 2 + 0.02, h + 0.11), (b + 0.04, t + 0.04, 0.06), "Taeferholz dunkel", ("-y",))
    q((0, t / 2, 0.55), (innen, t, 0.9), "Fachwerk", ("-y", "+z"))                          # Unterschrank
    for s in (-1, 1):                                                                       # lila Füllungen der Unterschranktüren
        q((s * 0.25, t + 0.006, 0.55), (0.38, 0.012, 0.7), "Laden lila", ("-y",))
        q((s * 0.08, t + 0.02, 0.6), (0.03, 0.03, 0.12), "Messing", ("-y",))
    zufall = random.Random(23)
    farben = ("Flakon blau", "Flakon gruen", "Flakon rot", "Flakon bernstein", "Flakon milch", "Flakon lila")
    for k, z in enumerate((1.0, 1.36, 1.72)):
        q((0, t / 2, z - 0.015), (innen, t - 0.04, 0.03), "Fachwerk", ("+x", "-x"))
        x = -b / 2 + 0.12
        while x < b / 2 - 0.12:
            r = zufall.uniform(0.03, 0.045)
            flakon(netz, st, M @ Vector((x, t / 2 + zufall.uniform(-0.06, 0.06), z)), h=zufall.uniform(0.13, 0.26), r=r,
                   stoff=zufall.choice(farben), form=zufall.randrange(3))
            x += 2 * r + zufall.uniform(0.03, 0.06)
    for s in (-1, 1):                                                                       # Glastüren, weit geöffnet
        angel = Vector((s * (b / 2 - 0.01), t + 0.01, 0.95))
        richtung = Vector((s * math.sin(math.radians(15)), math.cos(math.radians(15)), 0))
        breit, hoch = b / 2 - 0.03, h - 1.0
        T = M @ Matrix.Translation(angel) @ Matrix((richtung, Vector((-richtung.y, richtung.x, 0)), (0, 0, 1))).transposed().to_4x4()
        netz.alle, alle = True, netz.alle
        netz.quader((breit / 2, 0, hoch / 2), (breit - 0.08, 0.006, hoch - 0.08), st["Scheibenglas"], T)
        for x0, x1, z0, z1 in ((0, 0.045, 0, hoch), (breit - 0.045, breit, 0, hoch), (0, breit, 0, 0.045),
                               (0, breit, hoch - 0.045, hoch), (0, breit, hoch * 0.5 - 0.015, hoch * 0.5 + 0.015)):
            netz.quader(((x0 + x1) / 2, 0, (z0 + z1) / 2), (x1 - x0, 0.03, z1 - z0), st["Fachwerk"], T)
        netz.alle = alle


def handschuhe(netz, st, x):
    """Gekauft: zwei Paar Lederhandschuhe an Haken an der Rückwand (wie beim Alchimisten)."""
    M = si.rahmen(x, WY1 - 0.06, 180)
    si.q(netz, st, M, (0, 0.03, 1.85), (0.62, 0.06, 0.07), "Fachwerk", ohne=("-y",))
    for k, dx0 in enumerate((-0.16, 0.16)):
        netz.stab(M @ Vector((dx0, 0.03, 1.8)), M @ Vector((dx0, 0.12, 1.82)), 0.02, 0.02, st["Eisen"])
        for j, dx in enumerate((-0.05, 0.05)):
            unten = 1.36 - 0.04 * ((k + j) % 2)
            ecken_ = [(dx0 + dx - 0.05, 0.1, 1.78), (dx0 + dx + 0.05, 0.1, 1.78), (dx0 + dx + 0.07, 0.09 + 0.02 * j, unten),
                      (dx0 + dx - 0.06, 0.09 + 0.02 * j, unten)]
            netz.vieleck_nach([M @ Vector(p) for p in ecken_], M.to_3x3() @ Vector((0, 1, 0)), st["Leder"])
            netz.vieleck_nach([M @ (Vector(p) - Vector((0, 0.01, 0))) for p in ecken_], M.to_3x3() @ Vector((0, -1, 0)), st["Leder"])


def innenlaeden(netz, st, kanten, oeffnungen):
    """Lila Innenläden an den Fenstern des Werkraums, aufgeklappt an die Wand (vor dem Fachwerk)."""
    for seite, liste in oeffnungen.items():
        a, b = kanten[seite]
        for o in liste:
            if o.v < 0.05:
                continue
            wand = ia.Wand(a, b, [o])
            u = wand.ops[0][0]
            for s in (-1, 1):
                u0 = u + s * (o.b / 2 + 0.02)
                u1 = u0 + s * o.b / 2
                wq(netz, st, wand, min(u0, u1), max(u0, u1), o.v, o.v + o.h, 0.06, 0.085, "Laden lila")
                for v in (o.v + 0.15, o.v + o.h - 0.15):
                    wq(netz, st, wand, min(u0, u1) + 0.03, max(u0, u1) - 0.03, v - 0.03, v + 0.03, 0.085, 0.095, "Eisen")


def presse(netz, st, k):
    """Spindelpresse (ab Stufe 2): zwei Ständer auf Schwellen, oben der Holm mit der Mutter, dazwischen der Presskasten voller
    Rosenblätter in Tüchern mit dem Pressbrett; vorn ein Ausguss über der Auffangschale. Gibt die bewegte Spindel mit dem
    Querhebel (Spindel<k>) zurück."""
    x, y = SPINDEL.x, SPINDEL.y
    holz, alt = st["Holz"], st["Holz alt"]
    for s in (-1, 1):
        netz.quader((x, y + s * 0.5, 1.025), (0.16, 0.16, 2.05), holz, ohne=("-z",))
        netz.quader((x, y + s * 0.5, 0.06), (1.2, 0.2, 0.12), alt, ohne=("-z",))                 # Schwellen
        netz.stab((x - 0.45, y + s * 0.5, 0.12), (x - 0.08, y + s * 0.5, 0.55), 0.1, 0.1, alt)    # Streben
        netz.stab((x + 0.45, y + s * 0.5, 0.12), (x + 0.08, y + s * 0.5, 0.55), 0.1, 0.1, alt)
    netz.quader((x, y, 2.05), (0.26, 1.32, 0.24), holz)                                         # Holm
    netz.quader((x, y, 1.89), (0.2, 0.2, 0.08), st["Eisen"], ohne=("+z",))                      # Mutter
    # Presskasten auf zwei Klötzen, darin die Maische in Tüchern, darauf das Pressbrett
    for s in (-1, 1):
        netz.quader((x + s * 0.25, y, 0.14), (0.14, 0.6, 0.28), alt, ohne=("-z",))
    kasten = (x - 0.36, x + 0.36, y - 0.33, y + 0.33)
    netz.alle, alle = True, netz.alle
    for (ax, ay), (bx, by) in (((kasten[0], kasten[2]), (kasten[1], kasten[2])), ((kasten[1], kasten[2]), (kasten[1], kasten[3])),
                               ((kasten[1], kasten[3]), (kasten[0], kasten[3])), ((kasten[0], kasten[3]), (kasten[0], kasten[2]))):
        d = Vector((bx - ax, by - ay, 0)).normalized()
        n = Vector((d.y, -d.x, 0))
        netz.stab(Vector((ax, ay, 0.45)) + n * 0.03, Vector((bx, by, 0.45)) + n * 0.03, 0.34, 0.06, alt, quer=n, mehr=0.06)
    netz.alle = alle
    netz.quader(((kasten[0] + kasten[1]) / 2, (kasten[2] + kasten[3]) / 2, 0.29), (0.72, 0.66, 0.02), alt, ohne=("-z",))
    zufall = random.Random(30 + k)
    for i in range(6):                                                                         # Tücher mit Maische
        px, py = x + zufall.uniform(-0.2, 0.2), y + zufall.uniform(-0.18, 0.18)
        bs.rund_oben(netz, st, (px, py), 0.14, 0.5 + 0.01 * i, ("Leinen", "Rose", "Leinen")[i % 3], n=7)
    netz.quader((x, y, 0.62), (0.56, 0.56, 0.06), holz)                                         # Pressbrett
    netz.quader((x, y, 0.67), (0.22, 0.22, 0.04), alt, ohne=("-z",))
    # Ausguss vorn (−y) zur Auffangschale, die Schale auf dem Boden
    netz.stab((SCHALE.x, kasten[2] + 0.02, 0.36), (SCHALE.x, SCHALE.y + 0.12, 0.32), 0.08, 0.02, alt, quer=(0, 0, 1))
    for s in (-1, 1):
        netz.stab((SCHALE.x + s * 0.04, kasten[2] + 0.02, 0.38), (SCHALE.x + s * 0.04, SCHALE.y + 0.12, 0.34), 0.03, 0.01, alt,
                  quer=(s, 0, 0))
    drehkoerper(netz, st, SCHALE, [(0.0, 0.12), (0.03, 0.15), (0.14, 0.18), (0.16, 0.18), (0.16, 0.16)], "Tiegel", n=12)
    drehkoerper(netz, st, SCHALE, [(0.06, 0.155), (0.16, 0.16)], "Tiegel", n=12, innen=True)
    scheibe(netz, st, SCHALE, 0.155, 0.06, "Tiegel dunkel", n=12)
    # Spindel mit Kopf und Querhebel: bewegt, Ursprung auf der Achse am Boden
    sp = bs.Netz(f"Spindel{k}", alle=True)
    drehkoerper(sp, st, Vector((x, y, 0.69)), [(0.0, 0.07), (1.6, 0.07), (1.6, 0.0)], "Holz", n=8)
    for z in [0.76 + 0.08 * i for i in range(2)] + [1.16 + 0.08 * i for i in range(9)]:       # Gewindegänge
        drehkoerper(sp, st, Vector((x, y, z)), [(0.0, 0.078), (0.025, 0.078)], "Holz alt", n=8)
    hz = fg.PRESSE["hoehe"]
    sp.quader((x, y, hz), (0.2, 0.2, 0.16), st["Holz alt"])                                     # Spindelkopf
    for s in (-1, 1):
        sp.quader((x, y + s * 0.101, hz), (0.12, 0.004, 0.12), st["Eisen"], ohne=("+y" if s < 0 else "-y",))
    sp.stab((x - 0.97, y, hz), (x + 0.97, y, hz), 0.07, 0.07, holz)                             # Querhebel
    for s in (-1, 1):
        sp.stab((x + s * 0.93, y, hz), (x + s * 0.975, y, hz), 0.08, 0.08, st["Eisen"])
    o = sp.objekt()
    o.data.transform(Matrix.Translation((-x, -y, 0)))
    o.location = (x, y, 0)
    return o


def presse_werkstueck(st):
    """Werkstueck_3: das Duftöl in der Auffangschale, mit dem Kind „Strahl“ vom Ausguss hinunter."""
    netz = bs.Netz("Werkstueck_3")
    scheibe(netz, st, SCHALE, 0.165, 0.13, "Fluessigkeit", n=12)
    o = am_ort(netz, SCHALE + Vector((0, 0, 0.13)))
    strahl = bs.Netz("Strahl", alle=True)
    oben = Vector((SCHALE.x, SCHALE.y + 0.12, 0.33))
    strahl.stab(oben, Vector((SCHALE.x, SCHALE.y + 0.1, 0.13)), 0.016, 0.016, st["Fluessigkeit"])
    kind(am_ort(strahl, oben), o)
    return o


def spindel_bewegung(o):
    """Die Spindel schwenkt mit dem Querhebel wie die Hände der Figur (figuren.presse_winkel): Die Figur steht links der
    Achse und blickt +x, ihre linke Seite ist +y – ein Schwenk zu ihrer Linken dreht die Spindel um −Winkel."""
    def bewegung(bild):
        return None, (0.0, 0.0, -math.radians(fg.presse_winkel(bild / ARBEIT_BILDER))), None
    return [(o, bewegung)]


def werkzeugwand_ausstattung(netz, st, M):
    """Arbeitstisch unter der Werkzeugwand (alchimist_innen.werkbank), dazu ein lila Tuch und ein Korb mit Blüten."""
    ai.werkbank(netz, st, M)
    si.q(netz, st, M, (0.45, 0.34, 0.851), (0.5, 0.4, 0.002), "Laden lila", ohne=("-z",))
    bs.achteck(netz, st, M @ Vector((-0.35, 0.36, 0.235)), 0.16, 0.0, 0.16, "Holz alt")
    bs.rund_oben(netz, st, M @ Vector((-0.35, 0.36, 0)), 0.14, 0.4, "Lavendel")


# ─────────────────────────────────────────────────────────────────────────────
#  Blütenboden unter dem Dach
# ─────────────────────────────────────────────────────────────────────────────

def dach_fachwerk(netz, st, luke, gfenster):
    """Fachwerk auf der Innenseite von Kniestöcken und Giebeln (wie tischler_innen.dach_fachwerk, die Ladeluke hier im linken,
    das Fenster im rechten Giebel)."""
    z, B, holz = Z_LAGER, bs.B, st["Fachwerk"]
    alle, netz.alle = netz.alle, True
    cx, cy = (WX0 + WX1) / 2, DACH.ym
    L = WX1 - WX0
    for vorn in (True, False):
        M = bs.wandmatrix((cx, WY0 if vorn else WY1, z), "hinten" if vorn else "vorne")
        hoch = DACH.kniestock - 0.18
        bs.balken(netz, M, -L / 2, B / 2, L / 2, B / 2, holz, vor=bs.VOR_SCHWELLE, enden=True)
        n = math.ceil((L - B) / 1.2)
        for k in range(n + 1):
            u = -L / 2 + B / 2 + (L - B) * k / n
            if not vorn and KSCHLOT[0] - 0.12 < (u if not vorn else -u) + cx < KSCHLOT[1] + 0.12:
                continue                                                                      # nicht hinter dem Schlot
            bs.balken(netz, M, u, B, u, hoch, holz)
        bs.balken(netz, M, -L / 2, 0.68, L / 2, 0.68, holz, vor=bs.VOR_RIEGEL)
    T = WY1 - WY0
    for rechts, o in ((True, gfenster), (False, luke)):
        M = bs.wandmatrix((WX1 if rechts else WX0, cy, z), "links" if rechts else "rechts")
        u_o = (cy - o.s) if rechts else (o.s - cy)
        bs.balken(netz, M, -T / 2, B / 2, T / 2, B / 2, holz, vor=bs.VOR_SCHWELLE, enden=True)
        rand = B / 2 + 0.01
        roh = sorted([-T / 2 + B / 2, T / 2 - B / 2, u_o - o.b / 2 - rand, u_o + o.b / 2 + rand])
        us = []
        for a, b in zip(roh, roh[1:]):
            us.append(a)
            n = math.ceil((b - a) / 1.2) - 1
            if not (a < u_o < b):
                us += [a + (b - a) * k / (n + 1) for k in range(1, n + 1)]
        us.append(roh[-1])
        for u in us:
            y = (cy - u) if rechts else (cy + u)
            oben = DACH.hoehe(min(DACH.y1, max(DACH.y0, y + (B / 2 if y > cy else -B / 2)))) - 0.03
            if abs(u - u_o) < o.b / 2:
                continue
            bs.balken(netz, M, u, B, u, oben, holz)
        weit = DACH.halb - (DACH.kehle + 0.1 - DACH.kniestock) / DACH.tan
        bs.balken(netz, M, -weit, DACH.kehle, weit, DACH.kehle, holz, vor=bs.VOR_RIEGEL)
        if o.v < 0.05:
            for a, b in ((-T / 2 + B, u_o - o.b / 2 - B), (u_o + o.b / 2 + B, T / 2 - B)):
                bs.balken(netz, M, a, 0.9, b, 0.9, holz, vor=bs.VOR_RIEGEL)
            bs.balken(netz, M, u_o - o.b / 2 - B, o.h + rand, u_o + o.b / 2 + B, o.h + rand, holz, vor=bs.VOR_RIEGEL)
        else:
            bs.balken(netz, M, -T / 2 + B, 0.9, T / 2 - B, 0.9, holz, vor=bs.VOR_RIEGEL)
            for v in (o.v - rand, o.v + o.h + rand):
                bs.balken(netz, M, u_o - o.b / 2 - B, v, u_o + o.b / 2 + B, v, holz, vor=bs.VOR_RIEGEL)
    netz.alle = alle


def ladeluke_links(lager, kran, himmel, st, luke):
    """Ladeluke mit Flügeln, Kranbalken und Himmel im linken Giebel (innenausbau.Dachboden.ladeluke baut sie rechts, hier an
    x = 0 gespiegelt; ohne Futter, das Fachwerk rahmt sie). Gibt den Punkt für das Licht zurück."""
    teile = [bs.Netz("_Luke", alle=True), bs.Netz("_Kran", alle=True), bs.Netz("_Himmel")]
    punkt = DACH.ladeluke(*teile, st, -luke.s, luke.b, luke.h, WAND, futter=False)
    for quelle, ziel in zip(teile, (lager, kran, himmel)):
        ai.gespiegelt(quelle, ziel)
    return Vector((-punkt.x, punkt.y, punkt.z))


KOERBE_Y = (-2.75, -0.3)                           # Blütenkörbe am rechten Giebel, vorn (vor dem Fenster und der Bodenluke frei)


def bluetenkoerbe(netz, st, z):
    """Blütenkörbe am rechten Giebel: drei Reihen hoher Weidenkörbe, teils gestapelt, voller Lavendel, Rosen und Jasmin."""
    zufall = random.Random(41)
    bluete = ("Lavendel", "Rose", "Jasmin", "Lavendel trocken")
    k = 0
    for x in (WX1 - 0.32, WX1 - 0.85, WX1 - 1.38):
        y = KOERBE_Y[0] + 0.3
        while y < KOERBE_Y[1] - 0.25:
            r = zufall.uniform(0.22, 0.27)
            h = zufall.uniform(0.38, 0.5)
            fuss = Vector((x + zufall.uniform(-0.04, 0.04), y, z))
            bs.achteck(netz, st, fuss, r, 0.0, h, "Holz alt")
            drehkoerper(netz, st, fuss, [(h - 0.04, r + 0.012), (h, r + 0.012)], "Seil", n=8, dreh=math.pi / 8)
            if k % 3 == 1 and x > 3.5:                                                        # gestapelt
                fuss2 = fuss + Vector((0, 0, h + 0.02))
                bs.achteck(netz, st, fuss2, r * 0.9, 0.0, h * 0.8, "Holz alt")
                bs.rund_oben(netz, st, fuss2, r * 0.85, fuss2.z + h * 0.8 + 0.01, bluete[(k + 1) % 4])
            else:
                bs.rund_oben(netz, st, fuss, r * 0.95, z + h + 0.012, bluete[k % 4])
            y += 2 * r + 0.06
            k += 1


def trockengestelle(netz, st, x0, x1):
    """Trockengestelle am vorderen Kniestock, niedrig unter der Schräge: Ständer mit vier Horden voller Blüten."""
    z, y = Z_LAGER, WY0 + 0.38
    for x in (x0, (x0 + x1) / 2, x1):
        netz.quader((x, y, z + 0.65), (0.08, 0.08, 1.1), st["Holz"])
        netz.quader((x, y + 0.04, z + 0.05), (0.1, 0.5, 0.1), st["Holz alt"])
    zufall = random.Random(12)
    for h in (0.25, 0.52, 0.79, 1.06):
        netz.quader(((x0 + x1) / 2, y, z + h), (x1 - x0 + 0.1, 0.5, 0.05), st["Holz alt"], ohne=("+z",))
        netz.quader(((x0 + x1) / 2, y, z + h + 0.025), (x1 - x0 + 0.1, 0.5, 0.0), st["Leinen"], ohne=("-z", "+x", "-x", "+y", "-y"))
        x = x0 + 0.08
        while True:
            b = zufall.uniform(0.25, 0.45)
            if x + b > x1 - 0.03:
                break
            netz.quader((x + b / 2, y, z + h + 0.04), (b, 0.4, 0.03), st[zufall.choice(("Lavendel", "Rose", "Jasmin", "Lavendel trocken"))],
                        ohne=("-z",))
            x += b + 0.05


def flakonregale(netz, st, x0, x1):
    """Gekauft: Flakonregale am vorderen Kniestock (niedrig unter der Schräge), drei Böden voller leerer und gefüllter Flakons
    in Stroh."""
    z, y = Z_LAGER, WY0 + 0.3
    for x in (x0, (x0 + x1) / 2, x1):
        netz.quader((x, y, z + 0.55), (0.07, 0.36, 1.1), st["Holz"])
    zufall = random.Random(19)
    farben = ("Flakon blau", "Flakon gruen", "Flakon milch", "Flakon lila", "Glaskolben")
    for k, h in enumerate((0.1, 0.45, 0.8)):
        netz.quader(((x0 + x1) / 2, y, z + h - 0.015), (x1 - x0, 0.36, 0.03), st["Holz alt"])
        netz.quader(((x0 + x1) / 2, y, z + h + 0.002), (x1 - x0 - 0.1, 0.3, 0.004), st["Heu"], ohne=("-z",))
        x = x0 + 0.1
        while x < x1 - 0.1:
            if abs(x - (x0 + x1) / 2) > 0.06:
                flakon(netz, st, (x, y + zufall.uniform(-0.06, 0.06), z + h + 0.006), h=zufall.uniform(0.12, 0.2), r=0.035,
                       stoff=zufall.choice(farben), form=1, n=5)
            x += zufall.uniform(0.1, 0.16)


def fass_liegend(netz, st, mitte, r=0.3, laenge=0.75, hahn=True):
    """Liegendes Weingeistfass entlang y auf einem Lager (zwei Kanthölzer mit Keilen), vorn ein Zapfhahn aus Messing."""
    M = Matrix.Translation(mitte) @ Matrix.Rotation(math.radians(90), 4, "Z")      # örtlich x = Welt y
    profil = [(r * math.cos(2 * math.pi * k / 10), r * math.sin(2 * math.pi * k / 10)) for k in range(10)]
    profil_e = [(0.9 * y, 0.9 * zz) for y, zz in profil]
    netz.prisma(profil_e, -laenge / 2, -laenge / 2 + 0.08, st["Holz"], matrix=M)
    netz.prisma(profil, -laenge / 2 + 0.08, laenge / 2 - 0.08, st["Holz"], matrix=M)
    netz.prisma(profil_e, laenge / 2 - 0.08, laenge / 2, st["Holz"], matrix=M)
    for x in (-laenge / 2 + 0.12, laenge / 2 - 0.16):
        netz.prisma([(1.04 * y, 1.04 * zz) for y, zz in profil], x, x + 0.04, st["Eisen"], matrix=M)
    if hahn:
        a = M @ Vector((-laenge / 2, 0, -0.08))
        netz.stab(a, a + Vector((0, -0.08, 0)), 0.03, 0.03, st["Messing"])
        netz.stab(a + Vector((0, -0.07, 0)), a + Vector((0, -0.07, -0.06)), 0.025, 0.025, st["Messing"])
    for dx in (-0.2, 0.2):
        netz.quader(mitte + Vector((dx, 0, -r - 0.04)), (0.1, laenge + 0.1, 0.08), st["Holz alt"])


def weingeistfaesser(netz, st, z):
    """Weingeistfässer am hinteren Kniestock: drei liegende auf ihrem Lager, eines stehend, ein Krug."""
    for x in (-3.3, -2.55, -1.8):
        fass_liegend(netz, st, Vector((x, WY1 - 0.6, z + 0.38)))
    bs.fass(netz, st, Vector((-0.9, WY1 - 0.4, z)), r=0.28, h=0.75, reifen=(0.12, 0.58))
    bs.rund_oben(netz, st, Vector((-0.9, WY1 - 0.4, z)), 0.25, z + 0.751, "Holz alt")
    drehkoerper(netz, st, Vector((-0.45, WY1 - 0.35, z)), [(0.0, 0.1), (0.05, 0.13), (0.28, 0.13), (0.36, 0.07), (0.42, 0.06)],
                "Steingut", n=8)


def leinen_mit_buendeln(netz, st, z):
    """Zwei Leinen den First entlang, daran Lavendel- und Jasminbündel hoch über den Stellplätzen."""
    zufall = random.Random(17)
    h = z + DACH.kehle - 0.12
    for y in (-0.75, 0.95):
        netz.stab((WX0 + 1.4, y, h), (WX1 - 1.4, y, h), 0.015, 0.015, st["Seil"])
        x = WX0 + 1.7
        while x < WX1 - 1.6:
            ai.buendel(netz, st, (x, y, h - 0.01), laenge=zufall.uniform(0.26, 0.38),
                       stoff=("Lavendel trocken", "Jasmin", "Lavendel", "Rose", "Lavendel trocken")[int(x * 7) % 5])
            x += zufall.uniform(0.32, 0.5)


# ─────────────────────────────────────────────────────────────────────────────
#  Duftladen
# ─────────────────────────────────────────────────────────────────────────────

REGAL_X = [4.75 + 0.95 * k for k in range(5)]              # Ständer der Regale an der Rückwand des Ladens
REGAL_Y = LY1 - 0.2                                        # Mitte der Bretter (0,38 tief)
BRETTER = (0.35, 0.8, 1.22, 1.64, 2.06)                    # Oberkanten der Bretter
AUSLAGE_BRETTER = (0.8, 1.22)                              # darauf die Stellplätze Auslage_1–8


def dielenboden(netz, st, x0, x1, y0, y1, saat=0):
    """Dielenboden im Erdgeschoss: Bretter wie innenausbau.dielen_plan, die Fugen als dunkle Fläche 8 mm darunter."""
    for bx0, bx1, by0, by1, stoff in ia.dielen_plan(x0, x1, y0, y1, saat):
        ia.waagrecht(netz, (bx0, bx1, by0, by1), 0.0, True, st[stoff])
    ia.waagrecht(netz, (x0, x1, y0, y1), -0.008, True, st["Dielenfuge"])


def taefelung(netz, st, a, b, oeffnungen=(), frei=(), hoehe=1.25):
    """Holztäfelung bis Brusthöhe an der Wand a → b: Sockelleiste, Füllungen zwischen Rahmenhölzern, Abschlussleiste; über ihr
    bleibt der Kalkputz. Öffnungen und die Stücke `frei` (hinter dem Regal) bleiben aus."""
    wand = ia.Wand(a, b, oeffnungen)
    for u0, u1 in freie_stuecke(wand, 0.0, hoehe, frei=frei):
        wq(netz, st, wand, u0, u1, 0.0, 0.12, 0.0, 0.03, "Taeferholz dunkel")                  # Sockelleiste
        wq(netz, st, wand, u0, u1, 0.12, hoehe - 0.06, 0.0, 0.012, "Taeferholz")               # Füllungen (Grund)
        wq(netz, st, wand, u0, u1, 0.12, 0.2, 0.012, 0.028, "Taeferholz dunkel", ohne=("-y", "+x", "-x"))   # Rahmen unten
        wq(netz, st, wand, u0, u1, hoehe - 0.16, hoehe - 0.06, 0.012, 0.028, "Taeferholz dunkel", ohne=("-y", "+x", "-x"))
        wq(netz, st, wand, u0 - 0.01, u1 + 0.01, hoehe - 0.06, hoehe, 0.0, 0.045, "Taeferholz dunkel")    # Abschlussleiste
        n = max(1, round((u1 - u0) / 0.55))
        for k in range(n + 1):
            u = u0 + (u1 - u0) * k / n
            wq(netz, st, wand, max(u0, u - 0.035), min(u1, u + 0.035), 0.2, hoehe - 0.16, 0.012, 0.028, "Taeferholz dunkel",
               ohne=("-y", "+z", "-z"))


def laden_regale(netz, st):
    """Regale an der Rückwand des Ladens mit geschnitztem Kranz und lila Rückwand: Auf den beiden Auslagebrettern bleibt
    Platz für die Ware (das Spiel stellt sie auf), auf den übrigen stehen Flakons, Tiegel und Kästchen."""
    holz = st["Taeferholz dunkel"]
    x0, x1 = REGAL_X[0], REGAL_X[-1]
    netz.quader(((x0 + x1) / 2, LY1 - 0.01, 1.2), (x1 - x0, 0.02, 2.36), st["Laden lila"], ohne=("+y",))
    for x in REGAL_X:
        netz.quader((x, REGAL_Y, 1.2), (0.05, 0.4, 2.4), holz, ohne=("+y",))
    for z in BRETTER:
        netz.quader(((x0 + x1) / 2, REGAL_Y, z - 0.015), (x1 - x0, 0.38, 0.03), holz, ohne=("+y",))
        netz.quader(((x0 + x1) / 2, REGAL_Y - 0.196, z - 0.03), (x1 - x0, 0.012, 0.06), st["Messing" if z in AUSLAGE_BRETTER else
                                                                                         "Taeferholz"], ohne=("+y",))
    netz.quader(((x0 + x1) / 2, REGAL_Y + 0.02, 2.42), (x1 - x0 + 0.12, 0.44, 0.06), holz, ohne=("+y",))
    for k in range(9):                                                                          # Zinnen des Kranzes
        x = x0 + (x1 - x0) * k / 8
        netz.quader((x, REGAL_Y - 0.17, 2.5), (0.1, 0.06, 0.1), holz)
    zufall = random.Random(9)
    farben = ("Flakon blau", "Flakon gruen", "Flakon rot", "Flakon bernstein", "Flakon milch", "Flakon lila")
    for z in BRETTER:
        if z in AUSLAGE_BRETTER:
            continue
        for a, b in zip(REGAL_X, REGAL_X[1:]):
            x = a + 0.09
            while x < b - 0.12:
                art = zufall.choice(("flakon", "flakon", "flakon", "tiegel", "kasten"))
                y = REGAL_Y + zufall.uniform(-0.06, 0.04)
                if art == "flakon":
                    r = zufall.uniform(0.035, 0.05)
                    flakon(netz, st, (x + r, y, z), h=zufall.uniform(0.14, 0.28), r=r, stoff=zufall.choice(farben),
                           form=zufall.randrange(3), n=5)
                    x += 2 * r + 0.04
                elif art == "tiegel":
                    ai.tiegel(netz, st, (x + 0.06, y, z), r=0.06, h=0.08, stoff="Flakon milch", inhalt="Rosenknospe")
                    x += 0.15
                else:
                    netz.quader((x + 0.1, y, z + 0.06), (0.18, 0.2, 0.12), st[zufall.choice(("Laden lila", "Taeferholz"))])
                    netz.quader((x + 0.1, y, z + 0.125), (0.19, 0.21, 0.01), st["Messing"])
                    x += 0.23


def auslage_anker():
    """Auslage_1–8: je Fach eines Auslagebretts ein Stellplatz (Mitte der Brettoberkante, Blick in den Laden, −y)."""
    liste = []
    nummer = 1
    for z in reversed(AUSLAGE_BRETTER):
        for a, b in zip(REGAL_X, REGAL_X[1:]):
            liste.append(bs.anker(f"Auslage_{nummer}", ((a + b) / 2, REGAL_Y, z), 180))
            nummer += 1
    return liste


def tresen(netz, st):
    """Tresen quer im Laden: Kasten mit lila Füllungen und Messingbeschlägen, Platte, darauf die Messingwaage, ein Kontobuch,
    eine Vase mit Rosen und Probierflakons; dahinter ein Schemel."""
    x0, x1, y0, y1 = 5.5, 8.3, -1.65, -1.1
    holz, dunkel = st["Taeferholz"], st["Taeferholz dunkel"]
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, 0.47), (x1 - x0, y1 - y0, 0.94), dunkel)
    for k in range(5):
        x = x0 + (x1 - x0) * (k + 0.5) / 5
        netz.quader((x, y0 - 0.006, 0.47), ((x1 - x0) / 5 - 0.12, 0.012, 0.62), st["Laden lila"], ohne=("+y",))
        netz.quader((x, y0 - 0.014, 0.47), (0.08, 0.004, 0.08), st["Messing"], ohne=("+y",))
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2 - 0.03, 0.965), (x1 - x0 + 0.1, y1 - y0 + 0.12, 0.05), holz)
    z = 0.99
    w = Vector((7.6, -1.35, z))                                                             # Messingwaage
    bs.achteck(netz, st, w, 0.09, 0.0, 0.03, "Messing")
    netz.stab(w, w + Vector((0, 0, 0.42)), 0.02, 0.02, st["Messing"])
    netz.stab(w + Vector((-0.25, 0, 0.4)), w + Vector((0.25, 0, 0.4)), 0.014, 0.016, st["Messing"])
    bs.achteck(netz, st, w + Vector((0, 0, 0.42)), 0.025, 0.0, 0.04, "Messing")
    for s in (-1, 1):
        a = w + Vector((0.24 * s, 0, 0.4))
        netz.stab(a, a - Vector((0, 0, 0.25)), 0.006, 0.006, st["Messing"])
        bs.achteck(netz, st, a - Vector((0, 0, 0.265)), 0.085, 0.0, 0.015, "Messing")
    for k in range(3):                                                                      # Gewichte
        bs.achteck(netz, st, Vector((7.95 + 0.06 * k, -1.45, z)), 0.025 - 0.005 * k, 0.0, 0.04 - 0.008 * k, "Messing", n=6)
    buch = Vector((6.3, -1.3, z))
    for s in (-1, 1):
        netz.quader(buch + Vector((s * 0.12, 0, 0.01)), (0.23, 0.3, 0.02), st["Leder"])
        netz.quader(buch + Vector((s * 0.115, 0, 0.025)), (0.21, 0.28, 0.012), st["Pergament"])
    v = Vector((5.8, -1.3, z))                                                              # Vase mit Rosen
    drehkoerper(netz, st, v, [(0.0, 0.05), (0.06, 0.08), (0.16, 0.06), (0.22, 0.04), (0.25, 0.05)], "Flakon milch", n=8)
    zufall = random.Random(4)
    for k in range(7):
        wk = 2 * math.pi * k / 7
        kopf = v + Vector((0.08 * math.cos(wk), 0.08 * math.sin(wk), 0.42 + zufall.uniform(-0.05, 0.05)))
        netz.stab(v + Vector((0, 0, 0.2)), kopf, 0.012, 0.012, st["Ranke"])
        bs.achteck(netz, st, kopf - Vector((0, 0, 0.02)), 0.035, 0.0, 0.05, "Rose", n=6)
    for i, (x, stoff) in enumerate(((6.85, "Flakon blau"), (7.0, "Flakon rot"), (7.15, "Flakon gruen"))):
        flakon(netz, st, (x, -1.48 + 0.06 * (i % 2), z), h=0.12, r=0.03, stoff=stoff, form=i)
    s = Vector((6.9, -0.6, 0))                                                              # Schemel
    scheibe(netz, st, s, 0.17, 0.62, "Taeferholz", n=8)
    for i in range(3):
        w_ = math.radians(90 + 120 * i)
        netz.stab(s + Vector((0.15 * math.cos(w_), 0.15 * math.sin(w_), 0)), s + Vector((0.1 * math.cos(w_), 0.1 * math.sin(w_), 0.61)),
                  0.04, 0.04, st["Taeferholz dunkel"])


def ladenschild(netz, st):
    """Schild über den Regalen: Brett in der Hausfarbe mit einem lila Flakon und goldenem Stopfen."""
    M = si.rahmen(6.65, LY1, 180)
    si.q(netz, st, M, (0, 0.03, 2.72), (1.3, 0.06, 0.42), "Hausfarbe", ohne=("-y",))
    si.q(netz, st, M, (0, 0.065, 2.72), (1.38, 0.01, 0.48), "Taeferholz dunkel", ohne=("-y", "+y"))
    n = M.to_3x3() @ Vector((0, 1, 0))
    rand = [(0.13 * math.cos(2 * math.pi * k / 12), 2.66 + 0.11 * math.sin(2 * math.pi * k / 12)) for k in range(12)]
    netz.vieleck_nach([M @ Vector((x, 0.063, z)) for x, z in rand], n, st["Laden lila"])
    netz.vieleck_nach([M @ Vector(p) for p in ((-0.035, 0.063, 2.76), (0.035, 0.063, 2.76), (0.035, 0.063, 2.86), (-0.035, 0.063, 2.86))],
                      n, st["Laden lila"])
    netz.vieleck_nach([M @ Vector(p) for p in ((-0.05, 0.064, 2.86), (0.05, 0.064, 2.86), (0.05, 0.064, 2.9), (-0.05, 0.064, 2.9))],
                      n, st["Messing"])
    for s in (-1, 1):
        netz.stab(M @ Vector((s * 0.5, 0.03, 2.93)), M @ Vector((s * 0.5, 0.03, H_LADEN)), 0.02, 0.02, st["Eisen"])


def damastvorhang(netz, st):
    """Damastvorhang vor dem Durchgang zum Werkraum (Tuer_Laden_Werk): in Falten an einer Messingstange, mit hellerem
    Granatapfelmuster und Fransen."""
    x0 = LX0
    breite, oben, unten = LADENTUER_B + 0.36, 2.34, 0.02
    netz.alle, alle = True, netz.alle
    netz.stab((x0 + 0.1, LADENTUER_Y - breite / 2 - 0.08, oben + 0.05), (x0 + 0.1, LADENTUER_Y + breite / 2 + 0.08, oben + 0.05),
              0.035, 0.035, st["Messing"], quer=(1, 0, 0))
    for s in (-1, 1):
        bs.achteck(netz, st, Vector((x0 + 0.1, LADENTUER_Y + s * (breite / 2 + 0.1), oben + 0.03)), 0.03, 0.0, 0.05, "Messing", n=6)
    falten = 12
    pkt = []
    for k in range(falten * 2 + 1):
        y = LADENTUER_Y - breite / 2 + breite * k / (falten * 2)
        pkt.append((y, x0 + (0.07 if k % 2 == 0 else 0.13)))
    for k in range(len(pkt) - 1):
        (ya, xa), (yb, xb) = pkt[k], pkt[k + 1]
        a, b = Vector((xa, ya, 0)), Vector((xb, yb, 0))
        t = (b - a).normalized()
        n = Vector((t.y, -t.x, 0))                                  # zum Laden (+x)
        netz.vieleck_nach([(xa, ya, unten), (xb, yb, unten), (xb, yb, oben), (xa, ya, oben)], n, st["Damast"])
        m = (a + b) / 2 + n * 0.003
        for z in (0.45, 1.05, 1.65):                                                        # Granatäpfel, versetzt
            zz = z + 0.3 * (k % 2)
            if zz + 0.1 < oben - 0.05:
                netz.vieleck_nach([m + Vector((0, 0, zz - 0.09)), m + t * 0.022 + Vector((0, 0, zz)), m + Vector((0, 0, zz + 0.09)),
                                   m - t * 0.022 + Vector((0, 0, zz))], n, st["Damast hell"])
    for k in range(falten * 2):                                                            # Fransen
        (ya, xa) = pkt[k]
        netz.stab((xa + 0.004, ya, unten + 0.05), (xa + 0.004, ya, unten + 0.12), 0.03, 0.006, st["Teppich gold"], quer=(1, 0, 0))
    netz.alle = alle


def ladeneinrichtung(netz, st):
    """Kleinkram im Laden: ein Korb mit Rosenblättern und eine Bank bei der Gassentür, ein Bord mit Lavendelbündeln an der
    rechten Wand."""
    korb = Vector((6.25, -3.05, 0))
    bs.achteck(netz, st, korb, 0.22, 0.0, 0.35, "Holz alt")
    bs.rund_oben(netz, st, korb, 0.2, 0.355, "Rose")
    netz.quader((8.65, -2.6, 0.42), (0.4, 1.1, 0.06), st["Taeferholz"])                     # Bank an der rechten Wand
    for y in (-3.05, -2.15):
        netz.quader((8.65, y, 0.2), (0.36, 0.06, 0.4), st["Taeferholz dunkel"])
    netz.quader((8.62, -2.6, 0.48), (0.34, 1.0, 0.06), st["Laden lila"])                    # Kissen
    M = si.rahmen(LX1, -0.3, 90)
    si.q(netz, st, M, (0, 0.03, 2.25), (1.0, 0.06, 0.07), "Taeferholz dunkel", ohne=("-y",))
    for k in range(5):
        buendel_band(netz, st, M @ Vector((-0.4 + 0.2 * k, 0.08, 2.22)), 0.38, ("Lavendel", "Jasmin")[k % 2])


# ─────────────────────────────────────────────────────────────────────────────
#  Duftpavillon
# ─────────────────────────────────────────────────────────────────────────────

def pav_ecke(k):
    """Innenecke k (0 … 7) des Achtecks, gegen den Uhrzeigersinn ab −112,5° (Ecke 0 und 1 begrenzen die Vorderwand)."""
    w = math.radians(-112.5 + 45 * k)
    return (PM.x + PR * math.cos(w), PM.y + PR * math.sin(w))


def pav_wand(k):
    """innenausbau.Wand der Pavillonwand k (der Raum liegt links)."""
    oeffnungen = [PAV_TUER] if k == 0 else PAV_FENSTER.get(k, [])
    return ia.Wand(pav_ecke(k), pav_ecke(k + 1), oeffnungen)


def pav_huelle(netz, st, anker):
    """Acht Wände in hellem Kalkputz (die Vorderwand ist die Rückwand des Werkraums, von hier ohne Außenseite), Fenster mit
    Butzenscheiben und Licht."""
    netz.alle = True
    for k in range(8):
        oeffnungen = [PAV_TUER] if k == 0 else PAV_FENSTER.get(k, [])
        si.wand(netz, st, pav_ecke(k), pav_ecke(k + 1), H_PAV, oeffnungen, stoff="Wandputz", dicke=WAND, aussen=k != 0,
                laibung=k != 0, enden=(False, False))
    netz.alle = False
    for k, liste in PAV_FENSTER.items():
        for o in liste:
            butzenfenster(netz, st, pav_wand(k), o)
            wand = pav_wand(k)
            anker.append(licht(wand.p(wand.ops[0][0], o.v + o.h / 2, 0.1), (wand.n.x, wand.n.y, 0), "Fenster", 3))


def butzenfenster(netz, st, wand, o):
    """Fenster mit Butzenscheiben: Scheibe („Fenster“) in der Mitte der Laibung, davor Bleiruten als Ringe um runde Butzen
    in versetzten Reihen, Rahmen und Fensterbank aus Eichenholz."""
    u = wand.ops[0][0]
    w = -WAND / 2
    h2 = o.b / 2
    netz.vieleck_nach([wand.p(u - h2, o.v, w), wand.p(u + h2, o.v, w), wand.p(u + h2, o.v + o.h, w), wand.p(u - h2, o.v + o.h, w)],
                      wand.n, st["Fenster"])
    d = 0.15                                                        # Abstand der Butzen
    r0, r1 = d * 0.5, d * 0.36
    reihe = 0
    v = o.v + d * 0.55
    while v < o.v + o.h - d * 0.45:
        versatz = d / 2 if reihe % 2 else 0.0
        x = -h2 + d * 0.55 + versatz
        while x < h2 - d * 0.45:
            for k in range(6):
                a0, a1 = 2 * math.pi * k / 6, 2 * math.pi * (k + 1) / 6
                netz.vieleck_nach([wand.p(u + x + r1 * math.cos(a0), v + r1 * math.sin(a0), w + 0.004),
                                   wand.p(u + x + r0 * math.cos(a0), v + r0 * math.sin(a0), w + 0.004),
                                   wand.p(u + x + r0 * math.cos(a1), v + r0 * math.sin(a1), w + 0.004),
                                   wand.p(u + x + r1 * math.cos(a1), v + r1 * math.sin(a1), w + 0.004)], wand.n, st["Blei"])
            x += d
        v += d * 0.87
        reihe += 1
    for u0, u1, v0, v1 in ((u - h2, u + h2, o.v + o.h - 0.05, o.v + o.h), (u - h2, u + h2, o.v, o.v + 0.05),
                           (u - h2, u - h2 + 0.05, o.v, o.v + o.h), (u + h2 - 0.05, u + h2, o.v, o.v + o.h),
                           (u - 0.02, u + 0.02, o.v, o.v + o.h)):
        wq(netz, st, wand, u0, u1, v0, v1, w, w + 0.03, "Fachwerk", ohne=("-y",))
    wq(netz, st, wand, u - h2 - 0.06, u + h2 + 0.06, o.v - 0.04, o.v + 0.01, w, 0.07, "Taeferholz dunkel", ohne=("-y",))


def wandmalerei(netz, st):
    """Wandmalerei auf allen acht Wänden (Farbe auf dem Putz, wenige Millimeter davor): gemalter roter Sockel mit ockerfarbener
    Linie, oben ein grüner Fries zwischen Ockerlinien, dazwischen Rosenranken – geschwungene Stiele mit Blättern, Blüten und
    Knospen. Öffnungen, Kachelofen, Giftschrank und Wandteppich bleiben frei."""
    zufall = random.Random(77)
    frei_ranken = {OFEN_WAND: [(0.0, 9.0)], GIFTSCHRANK_WAND: [(0.0, 9.0)], TEPPICH_WAND: [(0.0, 9.0)]}
    for k in range(8):
        wand = pav_wand(k)
        L = wand.laenge
        for v0, v1, w, stoff in ((0.0, 0.86, 0.003, "Sockelrot"), (0.86, 0.9, 0.004, "Ocker"), (3.3, 3.33, 0.004, "Ocker"),
                                 (3.33, 3.58, 0.003, "Ranke"), (3.58, 3.61, 0.004, "Ocker")):
            frei = [(0.0, L)] if k == OFEN_WAND and v1 <= 2.4 else []
            for u0, u1 in freie_stuecke(wand, v0, v1, rand=0.0, frei=frei):
                netz.vieleck_nach([wand.p(u0, v0, w), wand.p(u1, v0, w), wand.p(u1, v1, w), wand.p(u0, v1, w)], wand.n, st[stoff])
        for j in range(8):                                                                   # Rosetten im Fries
            u = L * (j + 0.5) / 8
            if not wand.offen(u - 0.05, u + 0.05, 3.33, 3.58):
                netz.vieleck_nach([wand.p(u + 0.06 * math.cos(2 * math.pi * i / 6), 3.455 + 0.06 * math.sin(2 * math.pi * i / 6), 0.005)
                                   for i in range(6)], wand.n, st["Rosenbluete" if j % 2 else "Jasmin"])
        if k in frei_ranken:
            continue
        for uc in (L * 0.25, L * 0.75):
            if wand.offen(uc - 0.2, uc + 0.2, 0.9, 3.3):
                uc = 0.18 if uc < L / 2 else L - 0.18                                          # neben der Öffnung
                if wand.offen(uc - 0.12, uc + 0.12, 0.9, 3.3):
                    continue
            phase = zufall.uniform(0, math.pi)
            vs = [0.95 + 0.1 * i for i in range(24)]

            def stiel(v):
                return uc + 0.07 * math.sin(v * 4.2 + phase)
            for va, vb in zip(vs, vs[1:]):
                ua, ub = stiel(va), stiel(vb)
                netz.vieleck_nach([wand.p(ua - 0.009, va, 0.004), wand.p(ua + 0.009, va, 0.004), wand.p(ub + 0.009, vb, 0.004),
                                   wand.p(ub - 0.009, vb, 0.004)], wand.n, st["Ranke"])
            for i, v in enumerate(vs[1:-1]):
                if i % 2:
                    continue
                s = 1 if i % 4 else -1
                u = stiel(v)
                if i % 6 == 4:                                                                  # Blüte mit Knospe
                    netz.vieleck_nach([wand.p(u + s * 0.08 + 0.045 * math.cos(2 * math.pi * t / 8), v + 0.045 * math.sin(2 * math.pi * t / 8), 0.005)
                                       for t in range(8)], wand.n, st["Rosenbluete"])
                    netz.vieleck_nach([wand.p(u + s * 0.08 + 0.017 * math.cos(2 * math.pi * t / 6), v + 0.017 * math.sin(2 * math.pi * t / 6), 0.0075)
                                       for t in range(6)], wand.n, st["Rosenknospe"])
                    netz.vieleck_nach([wand.p(u, v - 0.004, 0.0045), wand.p(u + s * 0.08, v - 0.004, 0.0045), wand.p(u + s * 0.08, v + 0.004, 0.0045),
                                       wand.p(u, v + 0.004, 0.0045)], wand.n, st["Ranke"])
                else:                                                                           # Blatt
                    spitze = (u + s * 0.1, v + 0.05)
                    netz.vieleck_nach([wand.p(u, v, 0.0045), wand.p(u + s * 0.05, v - 0.012, 0.0045), wand.p(*spitze, 0.0045),
                                       wand.p(u + s * 0.04, v + 0.035, 0.0045)], wand.n, st["Rosenblatt"])


def fliesenboden(netz, st):
    """Glasierte Fliesen im Schachbrett (30 cm, gelb und grün), auf das Achteck beschnitten; die Fugen sind eine Fläche
    6 mm darunter. Gibt die Zahl der Fliesen zurück."""
    umriss = [pav_ecke(k) for k in range(8)]
    netz.vieleck_nach([(x, y, -0.006) for x, y in umriss], (0, 0, 1), st["Fliesenfuge"])
    a, f = 0.3, 0.004
    anzahl = 0
    for i in range(-10, 11):
        for j in range(-10, 11):
            x0, y0 = PM.x + i * a, PM.y + j * a
            stueck = ia.beschneiden_vieleck([(x0 + f, y0 + f), (x0 + a - f, y0 + f), (x0 + a - f, y0 + a - f), (x0 + f, y0 + a - f)],
                                            umriss)
            if len(stueck) < 3 or ia.flaeche2d(stueck) < 0.0004:
                continue
            netz.vieleck_nach([(x, y, 0.0) for x, y in stueck], (0, 0, 1), st["Fliese gelb" if (i + j) % 2 else "Fliese gruen"])
            anzahl += 1
    return anzahl


def kachelofen(netz, st):
    """Kachelofen in der Ecke rechts hinten (vor der Wand OFEN_WAND, zur Mitte gedreht): Sockel aus Sandstein, Feuerkasten
    und darauf ein schmalerer Aufsatz aus grün glasierten Kacheln (jede Kachel ein erhabenes Feld vor den dunklen Fugen),
    Gesimse, Krone."""
    wand = pav_wand(OFEN_WAND)
    mitte = wand.p(wand.laenge / 2, 0, 0.62)
    M = Matrix.Translation(mitte) @ Matrix((wand.d, wand.n, Vector((0, 0, 1)))).transposed().to_4x4()
    si.q(netz, st, M, (0, 0, 0.18), (1.0, 1.0, 0.36), "Sandstein")
    for (b, t, z0, z1, kx, kz) in ((0.92, 0.92, 0.36, 1.3, 4, 4), (0.62, 0.62, 1.42, 2.3, 3, 4)):
        si.q(netz, st, M, (0, 0, (z0 + z1) / 2), (b, t, z1 - z0), "Ofenkachel dunkel")
        kh = (z1 - z0) / kz
        for seite, (d, n, quer) in enumerate((((1, 0, 0), (0, 1, 0), b), ((0, -1, 0), (1, 0, 0), t), ((0, 1, 0), (-1, 0, 0), t))):
            d, n = Vector(d), Vector(n)                            # vorn, rechts, links (die Rückseite steht an der Wand)
            basis = n * (t / 2 if seite == 0 else b / 2)
            kb = quer / kx
            for ix in range(kx):
                for iz in range(kz):
                    c = basis + d * (-quer / 2 + kb * (ix + 0.5)) + Vector((0, 0, z0 + kh * (iz + 0.5)))
                    T = M @ Matrix.Translation(c) @ Matrix((d, n, Vector((0, 0, 1)))).transposed().to_4x4()
                    netz.quader((0, 0.008, 0), (kb - 0.035, 0.016, kh - 0.035), st["Ofenkachel"], T, ohne=("-y",))
        si.q(netz, st, M, (0, 0, z1 + 0.04), (b + 0.08, t + 0.08, 0.08), "Ofenkachel")       # Gesims
    si.q(netz, st, M, (0, 0, 2.42), (0.64, 0.64, 0.16), "Ofenkachel dunkel")
    for k in range(4):                                                                      # Krone
        ecke = M @ Vector((0.3 * (1 if k in (0, 1) else -1), 0.3 * (1 if k in (1, 2) else -1), 2.5))
        bs.achteck(netz, st, ecke, 0.05, 0.0, 0.12, "Ofenkachel", n=6)
    abzug = M @ Vector((0, 0.1, 2.5))
    netz.stab(abzug, Vector((abzug.x, abzug.y, H_PAV)), 0.16, 0.16, st["Ofenkachel dunkel"])  # Abzug in die Decke


def radleuchter(netz, st, anker):
    """Radleuchter über der Mitte: eiserner Ring an vier Ketten, darauf acht Kerzen mit Flammen („Glut“)."""
    z = 2.75
    e = st["Eisen"]
    netz.alle, alle = True, netz.alle
    n = 16
    for k in range(n):
        a, b = (PM + Vector((0.55 * math.cos(2 * math.pi * j / n), 0.55 * math.sin(2 * math.pi * j / n), z)) for j in (k, k + 1))
        netz.stab(a, b, 0.05, 0.03, e, quer=(0, 0, 1))
    for k in range(4):
        w = 2 * math.pi * k / 4 + math.pi / 4
        p = PM + Vector((0.55 * math.cos(w), 0.55 * math.sin(w), z))
        netz.stab(p, PM + Vector((0, 0, z + 0.9)), 0.012, 0.012, e)
        netz.stab(p, PM + Vector((0, 0, z)), 0.02, 0.02, e)
    netz.stab(PM + Vector((0, 0, z + 0.9)), PM + Vector((0, 0, H_PAV)), 0.015, 0.015, e)
    bs.achteck(netz, st, PM + Vector((0, 0, z - 0.12)), 0.06, 0.0, 0.14, "Messing", n=8)
    for k in range(8):
        w = 2 * math.pi * k / 8
        p = PM + Vector((0.55 * math.cos(w), 0.55 * math.sin(w), z + 0.015))
        bs.achteck(netz, st, p, 0.035, 0.0, 0.02, "Messing", n=6)
        bs.achteck(netz, st, p + Vector((0, 0, 0.02)), 0.018, 0.0, 0.14, "Kerze", n=6)
        drehkoerper(netz, st, p + Vector((0, 0, 0.16)), [(0.0, 0.0), (0.01, 0.012), (0.03, 0.01), (0.06, 0.0)], "Glut", n=5)
    netz.alle = alle
    anker.append(licht(PM + Vector((0, 0, z + 0.2)), (0, 0, 1), "Glut", 3))


def wandteppich(netz, st):
    """Wandteppich an der Rückwand: Stange, Feld in Blau mit Streublumen (Tausendblumen), in der Mitte ein Rosenbaum, Bordüre
    in Rot mit Goldlinien, unten Fransen."""
    wand = pav_wand(TEPPICH_WAND)
    L = wand.laenge
    u0, u1, v0, v1 = L / 2 - 0.8, L / 2 + 0.8, 1.05, 3.05
    netz.stab(wand.p(u0 - 0.1, v1 + 0.06, 0.06), wand.p(u1 + 0.1, v1 + 0.06, 0.06), 0.04, 0.04, st["Holz"], quer=wand.n)
    for u in (u0 - 0.05, u1 + 0.05):
        netz.stab(wand.p(u, v1 + 0.06, 0.0), wand.p(u, v1 + 0.06, 0.08), 0.03, 0.03, st["Eisen"], quer=(0, 0, 1))

    def flaeche(a0, a1, b0, b1, w, stoff):
        netz.vieleck_nach([wand.p(a0, b0, w), wand.p(a1, b0, w), wand.p(a1, b1, w), wand.p(a0, b1, w)], wand.n, st[stoff])
    flaeche(u0, u1, v0, v1, 0.04, "Teppich rot")
    flaeche(u0 + 0.12, u1 - 0.12, v0 + 0.12, v1 - 0.12, 0.042, "Teppich blau")
    for a in (0.06, 0.11):
        for b0, b1, c0, c1 in ((u0 + a, u1 - a, v0 + a, v0 + a + 0.012), (u0 + a, u1 - a, v1 - a - 0.012, v1 - a),
                               (u0 + a, u0 + a + 0.012, v0 + a, v1 - a), (u1 - a - 0.012, u1 - a, v0 + a, v1 - a)):
            flaeche(b0, b1, c0, c1, 0.043, "Teppich gold")
    zufall = random.Random(51)
    for _ in range(70):                                                                     # Streublumen
        u, v = zufall.uniform(u0 + 0.17, u1 - 0.17), zufall.uniform(v0 + 0.17, v1 - 0.17)
        if abs(u - L / 2) < 0.32 and v > v0 + 0.3:
            continue
        r = zufall.uniform(0.015, 0.025)
        netz.vieleck_nach([wand.p(u + r * math.cos(2 * math.pi * t / 5), v + r * math.sin(2 * math.pi * t / 5), 0.045) for t in range(5)],
                          wand.n, st[zufall.choice(("Rosenbluete", "Jasmin", "Teppich gold", "Lavendel"))])
    um = L / 2                                                                              # Rosenbaum
    flaeche(um - 0.025, um + 0.025, v0 + 0.25, v0 + 1.2, 0.0435, "Holz")
    for t in range(12):
        a = 2 * math.pi * t / 12
        netz.vieleck_nach([wand.p(um, v0 + 1.38, 0.0445), wand.p(um + 0.3 * math.cos(a), v0 + 1.38 + 0.36 * math.sin(a), 0.0445),
                           wand.p(um + 0.3 * math.cos(a + math.pi / 6), v0 + 1.38 + 0.36 * math.sin(a + math.pi / 6), 0.0445)],
                          wand.n, st["Rosenblatt"])
    for t in range(7):
        a = 2 * math.pi * t / 7 + 0.3
        c = (um + 0.19 * math.cos(a), v0 + 1.38 + 0.23 * math.sin(a))
        netz.vieleck_nach([wand.p(c[0] + 0.035 * math.cos(2 * math.pi * i / 6), c[1] + 0.035 * math.sin(2 * math.pi * i / 6), 0.046)
                           for i in range(6)], wand.n, st["Rosenbluete"])
    for k in range(32):                                                                     # Fransen
        u = u0 + 0.025 + (u1 - u0 - 0.05) * k / 31
        flaeche(u - 0.006, u + 0.006, v0 - 0.07, v0, 0.04, "Teppich gold")


def brunnen(netz, st):
    """Kleiner Brunnen mit Rosenwasser: achteckiges Becken aus Sandstein, Säule mit Schale, darin ein Zapfen; das Rosenwasser
    (eigenes Material) füllt Becken und Schale."""
    m = Vector((0.25, 5.2, 0))
    drehkoerper(netz, st, m, [(0.0, 0.55), (0.08, 0.52), (0.5, 0.52), (0.5, 0.44)], "Sandstein", n=8, dreh=math.pi / 8)
    drehkoerper(netz, st, m, [(0.12, 0.44), (0.5, 0.44)], "Sandstein", n=8, innen=True, dreh=math.pi / 8)
    scheibe(netz, st, m, 0.44, 0.42, "Rosenwasser", n=8, dreh=math.pi / 8)
    drehkoerper(netz, st, m, [(0.42, 0.08), (0.9, 0.06), (0.95, 0.09)], "Sandstein", n=8)
    s = m + Vector((0, 0, 0.95))
    drehkoerper(netz, st, s, [(0.0, 0.09), (0.06, 0.22), (0.1, 0.24), (0.1, 0.21)], "Sandstein", n=10)
    drehkoerper(netz, st, s, [(0.06, 0.21), (0.1, 0.21)], "Sandstein", n=10, innen=True)
    scheibe(netz, st, s, 0.21, s.z + 0.085, "Rosenwasser", n=10)
    drehkoerper(netz, st, s + Vector((0, 0, 0.085)), [(0.0, 0.04), (0.12, 0.03), (0.16, 0.05), (0.2, 0.0)], "Messing", n=6)


def giftschrank(netz, st):
    """Verschlossener Giftschrank (Kulisse) vor der Wand GIFTSCHRANK_WAND: dunkler Eichenschrank mit zwei Türen, breiten
    Eisenbändern, Nieten, Riegel und großem Vorhängeschloss, oben ein Totenkopf-Zeichen als Warnung."""
    wand = pav_wand(GIFTSCHRANK_WAND)
    L = wand.laenge
    b, t, h = 1.0, 0.5, 2.1
    u0, u1 = L / 2 - b / 2, L / 2 + b / 2
    wq(netz, st, wand, u0, u1, 0.0, h, 0.0, t, "Fachwerk")
    wq(netz, st, wand, u0 - 0.04, u1 + 0.04, h, h + 0.08, 0.0, t + 0.04, "Taeferholz dunkel")
    wq(netz, st, wand, u0 - 0.02, u1 + 0.02, 0.0, 0.1, 0.0, t + 0.02, "Taeferholz dunkel")
    wq(netz, st, wand, L / 2 - 0.006, L / 2 + 0.006, 0.12, h - 0.06, t, t + 0.004, "Taeferholz dunkel", ohne=("-y",))
    for v in (0.35, 1.05, 1.75):                                                            # Eisenbänder mit Nieten
        wq(netz, st, wand, u0 + 0.03, u1 - 0.03, v - 0.04, v + 0.04, t, t + 0.012, "Eisen", ohne=("-y",))
        for k in range(6):
            u = u0 + 0.08 + (b - 0.16) * k / 5
            wq(netz, st, wand, u - 0.012, u + 0.012, v - 0.012, v + 0.012, t + 0.012, t + 0.022, "Eisen", ohne=("-y",))
    wq(netz, st, wand, L / 2 - 0.2, L / 2 + 0.2, 1.17, 1.21, t + 0.012, t + 0.03, "Eisen", ohne=("-y",))   # Riegel
    wq(netz, st, wand, L / 2 - 0.06, L / 2 + 0.06, 1.0, 1.14, t + 0.02, t + 0.06, "Eisen", ohne=("-y",))    # Schloss
    netz.stab(wand.p(L / 2 - 0.035, 1.14, t + 0.04), wand.p(L / 2 - 0.035, 1.22, t + 0.04), 0.014, 0.014, st["Eisen"], quer=wand.n)
    netz.stab(wand.p(L / 2 + 0.035, 1.14, t + 0.04), wand.p(L / 2 + 0.035, 1.22, t + 0.04), 0.014, 0.014, st["Eisen"], quer=wand.n)
    k = Vector((L / 2, h - 0.25))                                                           # Totenkopf
    netz.vieleck_nach([wand.p(k.x + 0.07 * math.cos(2 * math.pi * i / 8), k.y + 0.065 * math.sin(2 * math.pi * i / 8), t + 0.004)
                       for i in range(8)], wand.n, st["Stadtweiss"])
    for s in (-1, 1):
        netz.vieleck_nach([wand.p(k.x + s * 0.03 + 0.018 * math.cos(2 * math.pi * i / 6), k.y + 0.005 + 0.016 * math.sin(2 * math.pi * i / 6),
                                  t + 0.006) for i in range(6)], wand.n, st["Kohle"])
        netz.stab(wand.p(k.x - 0.12, k.y - 0.14 + s * 0.04, t + 0.004), wand.p(k.x + 0.12, k.y - 0.14 - s * 0.04, t + 0.004), 0.03,
                  0.002, st["Stadtweiss"], quer=wand.n)


def abfuelltisch(netz, st):
    """Abfülltisch vor dem Wandteppich (Platz 4, Feinarbeit im Sitzen wie beim Schmied): Platte, Hocker, eine Korbflasche mit
    Duftwasser, Trichter, eine Reihe gefüllter Flakons, Korken, Siegelwachs und Petschaft. Vor der Figur steht der Flakon in
    Arbeit: Werkstueck_4 ist sein Bauch aus Flüssigkeit (Farbe der Ware), Hals und Lippe aus Glas stehen fest. Gibt
    (Werkstück, Platz der Figur, örtliches System) zurück."""
    M = si.rahmen(PM.x + 0.1, 6.8, 0)                                  # +y zur Figur (nach hinten)
    holz = st["Taeferholz dunkel"]
    si.q(netz, st, M, (0, 0.32, 0.74), (1.3, 0.62, 0.06), "Taeferholz")
    for x in (-0.58, 0.58):
        for y in (0.06, 0.58):
            netz.stab(M @ Vector((x, y, 0)), M @ Vector((x, y, 0.71)), 0.06, 0.06, holz)
    si.q(netz, st, M, (0, 0.32, 0.771), (1.0, 0.36, 0.002), "Laden lila", ohne=("-z",))   # Tuch
    z = 0.772
    # Hocker
    bs.achteck(netz, st, M @ Vector((0, 0.95, 0.42)), 0.19, 0.0, 0.05, "Taeferholz")
    for k in range(3):
        w = 2 * math.pi * k / 3
        netz.stab(M @ Vector((0.12 * math.cos(w), 0.95 + 0.12 * math.sin(w), 0)), M @ Vector((0.08 * math.cos(w), 0.95 + 0.08 * math.sin(w), 0.42)),
                  0.04, 0.04, holz)
    # Korbflasche links, Trichter, Flakonreihe rechts, Korken, Siegel
    k = M @ Vector((-0.45, 0.25, z))
    drehkoerper(netz, st, k, [(0.0, 0.1), (0.04, 0.15), (0.2, 0.16), (0.26, 0.11), (0.28, 0.05)], "Holz alt", n=8)   # Korb
    drehkoerper(netz, st, k, [(0.27, 0.09), (0.32, 0.04), (0.42, 0.03), (0.44, 0.04)], "Glaskolben", n=8)
    t = M @ Vector((-0.2, 0.42, z))
    drehkoerper(netz, st, t, [(0.0, 0.02), (0.06, 0.006), (0.06, 0.006)], "Messing", n=6)
    drehkoerper(netz, st, t + Vector((0, 0, 0.06)), [(0.0, 0.008), (0.02, 0.012), (0.09, 0.05), (0.1, 0.05)], "Messing", n=8)
    zufall = random.Random(61)
    for i in range(6):
        flakon(netz, st, M @ Vector((0.2 + 0.07 * i, 0.18 + 0.03 * (i % 2), z)), h=0.13, r=0.028,
               stoff=("Flakon lila", "Flakon rot", "Flakon blau")[i % 3], form=1)
    for i in range(5):
        bs.achteck(netz, st, M @ Vector((0.42 + zufall.uniform(-0.08, 0.08), 0.46 + zufall.uniform(-0.05, 0.05), z)), 0.012, 0.0, 0.022,
                   "Holz alt", n=5)
    si.q(netz, st, M, (0.25, 0.5, z + 0.012), (0.1, 0.04, 0.024), "Rosenbluete")              # Siegelwachs
    netz.stab(M @ Vector((0.12, 0.48, z)), M @ Vector((0.12, 0.48, z + 0.09)), 0.02, 0.02, st["Messing"])   # Petschaft
    # Flakon in Arbeit: Hals und Lippe aus Glas, der Bauch ist das Werkstück
    f = M @ Vector((0.0, 0.43, z))
    drehkoerper(netz, st, f, [(0.085, 0.016), (0.12, 0.012), (0.13, 0.017), (0.14, 0.015)], "Glaskolben", n=8)
    scheibe(netz, st, f, 0.015, f.z + 0.14, "Loch", n=8)
    werkstueck = bs.Netz("Werkstueck_4")
    drehkoerper(werkstueck, st, f, [(0.0, 0.032), (0.01, 0.042), (0.06, 0.042), (0.085, 0.016)], "Fluessigkeit", n=8)
    scheibe(werkstueck, st, f, 0.032, f.z, "Fluessigkeit", n=8, unten=True)
    return am_ort(werkstueck, f + Vector((0, 0, 0.06))), M @ Vector((0, 0.95, 0))


def pavillon_bank(netz, st):
    """Gepolsterte Bank vor der Wand links vorn (Wand 7), lila Kissen, darauf ein offenes Kästchen mit Flakons."""
    wand = pav_wand(7)
    L = wand.laenge
    wq(netz, st, wand, L / 2 - 0.7, L / 2 + 0.7, 0.0, 0.42, 0.0, 0.45, "Taeferholz dunkel")
    wq(netz, st, wand, L / 2 - 0.66, L / 2 + 0.66, 0.42, 0.5, 0.06, 0.44, "Laden lila", ohne=("-z",))
    wq(netz, st, wand, L / 2 - 0.7, L / 2 + 0.7, 0.42, 0.9, 0.0, 0.06, "Taeferholz dunkel")
    c = wand.p(L / 2 + 0.35, 0.5, 0.25)
    netz.quader(c + Vector((0, 0, 0.05)), (0.28, 0.2, 0.1), st["Taeferholz"])
    for i in range(3):
        flakon(netz, st, c + wand.d * (-0.08 + 0.08 * i) + Vector((0, 0, 0.1)), h=0.1, r=0.025, stoff=("Flakon gruen", "Flakon milch",
                                                                                                      "Flakon rot")[i])


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen
# ─────────────────────────────────────────────────────────────────────────────

def bauen():
    import bpy
    bs.neue_szene()
    ia.RUSS_QUELLEN.clear()
    ti._lichter[0] = 0
    st = bs.Stoffe()
    werk, lager = bs.Netz("Raum_Werk"), bs.Netz("Raum_Lager", alle=True)
    laden, pav = bs.Netz("Raum_Laden"), bs.Netz("Raum_Pavillon")
    decke_w, decke_l = bs.Netz("Decke_Werk", alle=True), bs.Netz("Decke_Lager", alle=True)
    decke_d, decke_p = bs.Netz("Decke_Laden", alle=True), bs.Netz("Decke_Pavillon", alle=True)
    kran, himmel = bs.Netz("Kranbalken", alle=True), bs.Netz("Himmel_Lager")
    teile = {}                    # Name → (Netz, Raum)

    def teil(name, raum="Werk"):
        if name not in teile:
            teile[name] = (bs.Netz(name), raum)
        return teile[name][0]

    anker = []                    # (Objekt, Raum)
    fertig = []                   # schon gebaute Objekte (Werkstücke): (Objekt, Raum)

    def setzen(o, raum="Werk", stufe=None):
        if stufe is not None:
            o["stufe"] = stufe
        anker.append((o, raum))

    # ── Werkraum: Hülle ───────────────────────────────────────────────────────
    W = WERK_OEFFNUNGEN
    kanten = ecken(WX0, WX1, WY0, WY1)
    huelle(werk, decke_w, st, WX0, WX1, WY0, WY1, H_WERK, {
        "vorne": dict(oeffnungen=W["vorne"]),
        "rechts": dict(oeffnungen=W["rechts"], aussen=False),
        "hinten": dict(oeffnungen=W["hinten"], aussen=False),
        "links": dict(oeffnungen=W["links"]),
    }, luke=LUKE, oben=lager, schlote=[KSCHLOT], saat=3)
    ti.fachwerk_innen(werk, st, WX0, WX1, WY0, WY1, H_WERK, W)
    liste = []
    ti.fenster_und_licht(werk, st, kanten, W, liste)
    for o in liste:
        setzen(o)
    innenlaeden(werk, st, kanten, W)
    schlot(werk, st, 0.0, H_WERK)
    liste = []
    werkstueck2, tropfen, dampf = destille(teil("Bedienung_Produktion_Kessel"), st, liste)
    for o in liste:
        setzen(o)
    fertig.append((werkstueck2, "Werk"))
    setzen(bs.anker("Dampf_1", dampf))
    kuehlfass(werk, st)
    fertig.append((mischtisch(teil("Bedienung_Produktion_Mischtisch"), st), "Werk"))
    stangen_mit_buendeln(werk, st)
    ti.laterne(werk, st, Vector((-1.0, -0.4, 2.95)), H_WERK - 0.26)                       # zwischen zwei Deckenbalken
    setzen(licht(Vector((-1.0, -0.4, 2.8)), (0, 0, -1), "Lampe"))

    # ── Werkraum: Bedienung, Türen, Einrichtung ──────────────────────────────
    si.pult(teil("Bedienung_Personal_Buch"), st, si.rahmen(WX0 + 0.05, -2.7, -90))
    M_wand = si.rahmen(WX1 - 0.06, -0.75, 90)                   # vor dem Fachwerk, vor dem Querhebel der Presse
    werkzeugwand_ausstattung(werk, st, M_wand)
    si.werkzeugwand(teil("Bedienung_Einrichten_Werkzeugwand"), st, M_wand)
    ai.werkzeug1(teil("Einrichtung_Werkzeug1"), st, M_wand)
    ai.werkzeug2(teil("Einrichtung_Werkzeug2"), st, M_wand)
    glasschrank(teil("Einrichtung_Glasflakons"), st)
    ti.hoftor(teil("Bedienung_Karren_Hoftor"), st, si.rahmen(WX0, HOFTOR_Y, -90), HOFTOR_B, HOFTOR_H)
    M_holz = si.rahmen(HOLZTUER_X, WY0, 0)
    si.bretttuer(teil("Holztuer"), st, M_holz, HOLZTUER_B, HOLZTUER_H)
    si.eisentuer(teil("Einrichtung_Eisentuer"), st, M_holz, HOLZTUER_B, HOLZTUER_H)
    si.loeschfass(teil("Einrichtung_Loeschfass"), st, Vector((1.55, -3.05, 0)))
    handschuhe(teil("Einrichtung_Handschuhe"), st, -2.25)
    ia.treppe(teil("Tuer_Werk_Lager"), st, TREPPE_X, TREPPE_FUSS, TREPPE_OBEN, Z_LAGER)       # Treppe hinauf zum Blütenboden
    si.bretttuer(teil("Tuer_Werk_Laden"), st, si.rahmen(WX1, LADENTUER_Y, 90), LADENTUER_B, LADENTUER_H, stoff="Brett")
    damastvorhang(teil("Tuer_Laden_Werk", "Laden"), st)
    si.bretttuer(teil("Tuer_Werk_Pavillon"), st, si.rahmen(PM.x, WY1, 180), PAVTUER_B, PAVTUER_H, stoff="Brett")
    M_pt = si.rahmen(PM.x, WY1 + WAND, 0)
    si.bretttuer(teil("Tuer_Pavillon_Werk", "Pavillon"), st, M_pt, PAVTUER_B, PAVTUER_H, stoff="Brett")
    si.tuerstock(teil("Tuer_Pavillon_Werk", "Pavillon"), st, M_pt, PAVTUER_B, PAVTUER_H)

    # ── Werkraum: Arbeitsplätze ──────────────────────────────────────────────
    setzen(si.arbeit("Arbeit_1_Mischen", W1, (0, -1), 1))
    setzen(si.arbeit("Arbeit_2_Destillieren", W2, (1, 0), 1))
    setzen(si.arbeit("Arbeit_3_Pressen", W3, (1, 0), 2))

    # ── Stufen (kumulativ), Presse, bewegte Teile ─────────────────────────────
    stufen = [bs.Netz(f"Stufe{k}") for k in (1, 2, 3)]
    ziegel = 0
    for k, netz in enumerate(stufen, 1):
        if k < 3:
            ia.lehmboden(netz, st, WX0, WX1, WY0, WY1, saat=6)
        else:
            ziegel = ia.fischgraet(netz, st, WX0, WX1, WY0, WY1, saat=6)
        if k == 1:
            gefach(netz, st, *kanten["rechts"], W["rechts"][0])
        if k <= 2:
            gefach(netz, st, *kanten["hinten"], W["hinten"][1])
    stufen_obj = [s.objekt() for s in stufen]
    bewegt = tropfen_bewegung(tropfen)
    for k in (2, 3):
        netz = bs.Netz(f"Presse{k}")
        spindel = presse(netz, st, k)
        for o in (netz.objekt(), spindel):
            o.parent = stufen_obj[k - 1]
            o.matrix_parent_inverse = Matrix.Identity(4)
        bewegt += spindel_bewegung(spindel)
    werkstueck3 = presse_werkstueck(st)
    werkstueck3["stufe"] = 2
    fertig.append((werkstueck3, "Werk"))
    print(f"Backsteinpflaster: {ziegel} Ziegel")

    # ── Blütenboden unter dem Dach ───────────────────────────────────────────
    z = Z_LAGER
    DACH.kniestoecke(lager, st, "Lehmputz hell")
    luke = Oe(LADELUKE_Y, 0.0, 1.2, 1.8)                      # Höhen über dem Dachboden
    gfenster = Oe(0.0, 2.2, 0.6, 0.55)
    DACH.giebel(lager, st, False, [luke], "Lehmputz hell", WAND)    # Ladeluke im linken Giebel: zum Hof (Hoftor links)
    DACH.giebel(lager, st, True, [gfenster], "Lehmputz hell", WAND)
    dach_fachwerk(lager, st, luke, gfenster)
    DACH.dach(decke_l, st, aussparen=[KSCHLOT[:2]])
    licht_luke = ladeluke_links(lager, kran, himmel, st, luke)
    ti.fenster(lager, st, *kanten["rechts"], Oe(gfenster.s, z + gfenster.v, gfenster.b, gfenster.h))
    lager.alle = True
    schlot_dachboden(lager, st)
    ia.bodenluke(teil("Tuer_Lager_Werk", "Lager"), st, LUKE, z, gelaender_x=LUKE[0] - 0.05)
    setzen(licht(licht_luke, (1, 0, 0), "Fenster"), "Lager")
    setzen(licht(Vector((WX1 - 0.1, gfenster.s, z + gfenster.v + gfenster.h / 2)), (-1, 0, 0), "Fenster"), "Lager")
    bluetenkoerbe(teil("Bedienung_Lager_Koerbe", "Lager"), st, z)
    trockengestelle(lager, st, -3.6, -0.4)
    flakonregale(teil("Einrichtung_Regale", "Lager"), st, 0.4, 3.0)
    si.kisten(lager, st, Vector((-2.6, 1.9, z)))                                              # hinter der Ladeluke
    weingeistfaesser(lager, st, z)
    leinen_mit_buendeln(lager, st, z)
    nummer = 1
    for y in (1.5, 0.55):
        for x in (-1.6, -0.6, 0.4, 1.4):
            setzen(bs.anker(f"Lager_{nummer}", (x, y, z)), "Lager")
            nummer += 1

    # ── Duftladen (ab Stufe 2) ────────────────────────────────────────────────
    L = LADEN_OEFFNUNGEN
    kanten_l = ecken(LX0, LX1, LY0, LY1)
    laden.alle = True
    for seite, angaben in {"vorne": dict(enden=(False, True)), "rechts": {}, "hinten": dict(enden=(True, False)),
                           "links": dict(aussen=False, laibung=False)}.items():
        si.wand(laden, st, *kanten_l[seite], H_LADEN, L[seite], stoff="Kalkputz", dicke=WAND, **angaben)
    laden.alle = False
    ia.balkendecke(decke_d, st, LX0, LX1, LY0, LY1, H_LADEN, abdeckung=WAND, saat=14)
    liste = []
    ti.fenster_und_licht(laden, st, kanten_l, L, liste, stufe=2)
    for o in liste:
        setzen(o, "Laden")
    dielenboden(laden, st, LX0, LX1, LY0, LY1, saat=15)
    for seite in ("vorne", "rechts", "hinten", "links"):
        frei = [(LX1 - REGAL_X[-1] - 0.06, LX1 - REGAL_X[0] + 0.06)] if seite == "hinten" else []
        taefelung(laden, st, *kanten_l[seite], L[seite], frei=frei)
    laden_regale(laden, st)
    tresen(teil("Bedienung_Lager_Tresen", "Laden"), st)
    ladenschild(laden, st)
    ladeneinrichtung(laden, st)
    M_gt = si.rahmen(GASSENTUER_X, LY0, 0)                                                  # Tür zur Gasse (nur Kulisse)
    si.bretttuer(laden, st, M_gt, 1.0, 2.1)
    si.tuerstock(laden, st, M_gt, 1.0, 2.1, stoff="Taeferholz dunkel")
    si.tuerstock(laden, st, si.rahmen(LX0, LADENTUER_Y, -90), LADENTUER_B, LADENTUER_H, stoff="Taeferholz dunkel")
    ti.laterne(laden, st, Vector((6.9, -1.0, 2.4)), H_LADEN - 0.26)
    setzen(licht(Vector((6.9, -1.0, 2.25)), (0, 0, -1), "Lampe", 2), "Laden")
    for o in auslage_anker():
        setzen(o, "Laden", stufe=2)

    # ── Duftpavillon (ab Stufe 3) ─────────────────────────────────────────────
    liste = []
    pav_huelle(pav, st, liste)
    for o in liste:
        setzen(o, "Pavillon")
    wandmalerei(pav, st)
    fliesen = fliesenboden(pav, st)
    umriss = [pav_ecke(k) for k in range(8)]
    xs, ys = [p[0] for p in umriss], [p[1] for p in umriss]
    ia.balkendecke(decke_p, st, min(xs), max(xs), min(ys), max(ys), H_PAV, abdeckung=WAND, saat=19)
    kachelofen(pav, st)
    liste = []
    radleuchter(pav, st, liste)
    for o in liste:
        setzen(o, "Pavillon")
    wandteppich(pav, st)
    brunnen(pav, st)
    giftschrank(pav, st)
    pavillon_bank(pav, st)
    werkstueck4, platz4 = abfuelltisch(pav, st)
    werkstueck4["stufe"] = 3
    fertig.append((werkstueck4, "Pavillon"))
    setzen(si.arbeit("Arbeit_4_Feinarbeit", platz4, (0, -1), 3), "Pavillon")
    print(f"Fliesen im Pavillon: {fliesen}")

    # ── Blickpunkte ──────────────────────────────────────────────────────────
    for name, (auge, ziel) in BLICKE.items():
        raum = name.split("_")[1]
        setzen(si.blick(name, auge, ziel), raum, stufe={"Laden": 2, "Pavillon": 3}.get(raum))

    # ── Objekte, Zustände, Hierarchie ─────────────────────────────────────────
    raeume = {"Werk": werk.objekt(), "Lager": lager.objekt(), "Laden": laden.objekt(), "Pavillon": pav.objekt()}
    raeume["Laden"]["stufe"] = 2
    raeume["Pavillon"]["stufe"] = 3
    kinder = {r: [] for r in raeume}
    kinder["Werk"] += [decke_w.objekt()] + stufen_obj
    kinder["Lager"] += [decke_l.objekt(), kran.objekt(), himmel.objekt()]
    kinder["Laden"].append(decke_d.objekt())
    kinder["Pavillon"].append(decke_p.objekt())
    for o, raum in fertig:
        kinder[raum].append(o)
    for name, (netz, raum) in teile.items():
        o = netz.objekt()
        kinder[raum].append(o)
        if name.startswith("Einrichtung_"):
            bs.zustand(o)
        if name in ("Tuer_Werk_Laden", "Tuer_Laden_Werk"):
            o["stufe"] = 2
        if name in ("Tuer_Werk_Pavillon", "Tuer_Pavillon_Werk"):
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
    ia.vorschau(NAME, list(BLICKE), STUFEN, "--schnell" in sys.argv, (WX0 - WAND, LX1 + WAND, WY0 - WAND, PM.y + PA + WAND),
                (WX0 - WAND, WX1 + WAND, WY0 - WAND, WY1 + WAND), DACH, {"Werk", "Laden", "Pavillon"}, bild=1 + int(0.9 * FPS),
                lampe=80, fuell=[(0, 0, H_WERK - 0.4, 170, 1), (0, 0.3, Z_LAGER + 3.0, 90, 1),
                                 ((LX0 + LX1) / 2, (LY0 + LY1) / 2, H_LADEN - 0.4, 120, 2), (PM.x, PM.y, H_PAV - 0.6, 170, 3)])
    ai.verkleinern(NAME + "-blicke", 0.85)


if __name__ == "__main__":
    main()
