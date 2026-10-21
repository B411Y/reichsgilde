"""Innenraum des Diebs: die Diebesstube (Hauptraum), darunter der Beutekeller (Lager), ab der Stufe Unterschlupf unter dem Dach der
Schlafboden und ab der Stufe Diebesgilde hinter der Stube die Gildenhalle; Stufen Schmugglerloch, Unterschlupf, Diebesgilde.

    blender -b --python tools/modelle/dieb_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/dieb_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/innen/dieb_innen.glb und Bilder nach docs/modelle/ (dieb_innen-blicke.png: das Bild jedes
Blickpunkts, dieb_innen-stufen.png: die Stube von oben je Stufe, dieb_innen-grundriss.png: Stube und Gildenhalle, Beutekeller und
Schlafboden mit den Ankern).

Koordinaten wie beim Schmied (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte der Stube auf dem
Boden. Außen ist das Haus ein heruntergekommenes Diebesnest (dieb.glb: Bruchstein unten, verwitterte, geflickte Bretter oben);
innen sind die Wände mit alten Brettern verschalt, die Lücken und Flicken zeigen den Bruchstein dahinter:
    Werk        „Diebesstube“ innen x −5,0 … 5,0, y −3,5 … 3,5, nur 2,6 m hoch (die Deckenbalken hängen bis 2,34 m); Lehmboden.
                Hinten links das Übungsbrett mit acht Schlössern, links der Kartentisch mit dem Stadtplan, rechts der Zähltisch mit
                Münzen und Waage, vorn der Wetztisch, vorn links der Schleifbock und die Falltür in den Keller; die Fenster sind
                mit Lappen verhängt. Rechts das Hoftor und die Werkzeugwand, hinten rechts die Leiter zum Schlafboden (ab Stufe 2,
                vorher ist die Luke in der Decke zu) und die Tür zur Gildenhalle (ab Stufe 3, vorher mit Brettern vernagelt).
    Lager       „Beutekeller“ unter der Stube: x −2,4 … 4,0, y −3,75 … 0,95, Boden in −2,7 m, Tonnengewölbe aus Bruchstein (wie der
                Fasskeller des Wirts). Diebesgut an der Rückwand, Hehlerregale vorn, die Falltür zum Fluchttunnel im Boden rechts.
    Schlafboden ab Stufe 2 unter dem Dach über der Stube: Strohsäcke an beiden Kniestöcken, eine Leine mit Verkleidungen, Truhe,
                ein Spiegel mit Schminke.
    Gildenhalle ab Stufe 3 hinter der Stube: x −0,9 … 6,1, y 3,8 … 9,8, 3,6 m hoch, Bruchstein, Backsteinpflaster. Schwurtisch mit
                Dolch und Kerzen, das Wappen der Gilde (Schlüssel und Maske auf Nachtblau), der Thron des Meisterdiebs auf einem
                Podest, Fackeln, Bänke, ein Stuhl mit Stricken für Geiseln.
Alle Räume sind rundum geschlossen, die Kamera steht im Raum.

Knoten im GLB – dieselben Regeln wie in den anderen Innenräumen (Raum_…, Decke_…, Stufe1–Stufe3, Bedienung_…, Tuer_…,
Einrichtung_…, Holztuer, Blick_…, Lager_…, Licht_…), dazu:
    Raum_Schlafboden            extras „stufe“ = 2 (neuer Raumschlüssel „Schlafboden“), ebenso Tuer_Werk_Schlafboden (die Leiter),
                                Tuer_Schlafboden_Werk (die offene Luke) und alle Anker darin
    Raum_Gildenhalle            extras „stufe“ = 3 (neuer Raumschlüssel „Gildenhalle“), ebenso Tuer_Werk_Gildenhalle,
                                Tuer_Gildenhalle_Werk und alle Anker darin
    Dieb_1 … Dieb_40            Plätze der Diebe (Fußpunkt, Blick −Z) mit extras „taetigkeit“ (Ueben, Planen, Zaehlen, Schleifen,
                                Schlafen), „bewegung“ (die Bewegung der Figur), „platz“ (1 … 8: Füllreihenfolge je Tätigkeit) und
                                bei sitzenden Plätzen „sitzhoehe“; je Tätigkeit acht Plätze:
                                1–8   Ueben (Knacken) am Übungsbrett, von der Mitte nach außen
                                9–16  Planen (Predigen: Gesten, die Hände auf dem Tisch) am Kartentisch
                                17–24 Zaehlen (Zaehlen, sitzend) am Zähltisch
                                25–32 Schleifen: 25 am Schleifbock (Schleifen), 26–32 am Wetztisch (Feinarbeit, sitzend)
                                33–40 Schlafen (Schlafen, liegend) auf den Strohsäcken, extras „stufe“ = 2; Fußpunkt unter der Hüfte
                                      auf dem Strohsack, Blick zu den Füßen
    Meister_1                   der Meisterdieb auf dem Thron (stufe 3, Bewegung „Sitzen“, extras „sitzhoehe“ über dem Podest)
    Schleifstein, Waagbalken    bewegte Teile (Animation „Arbeit“)
Keine Arbeit_- und Werkstueck_-Anker: Der Dieb stellt keine Ware her.
Animation „Arbeit“ (6 s, Schleife): Der Schleifstein dreht sich in 1,2 s einmal (wie die Bewegung „Schleifen“), der Balken der
Münzwaage auf dem Zähltisch pendelt zweimal hin und her. Bei Zeit 0 steht beides in Ruhe.
Lichter: extras „art“ = „Fenster“ (die verhängten Fenster), „Lampe“ (Laternen) und „Glut“ (Kerzen, Fackeln).
Materialien: „Farben“ (Eckenfarben, Materialart als UV.y), „Hausfarbe“ (Haustür, Hoftor), „Fenster“, „Glut“, „Eisen“, „Gold“
(Münzen, Schlüssel im Wappen), „Wasser“.
Einrichtung: Werkzeug1 (Dietriche, Brecheisen, Wurfhaken an der Werkzeugwand), Werkzeug2 (Meisterdietriche in einer offenen
Lederrolle auf dem Brett unter der Werkzeugwand), Verkleidung (die Verkleidungstruhe in der Stube – in jeder Stufe dieselbe, den
Schlafboden gibt es erst ab Stufe 2), Regale (Hehlerregale im Keller), Loeschfass, Eisentuer (Haustür), Handschuhe.
"""

import contextlib
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import alchimist_innen as ai  # noqa: E402  (Drehkörper, Scheiben, am Ort, Verkleinern)
import figuren as fg  # noqa: E402  (Übungsbrett, Zähltisch, Kartentisch, Arbeitshöhen)
import innenausbau as ia  # noqa: E402  (Wand, Böden, Decke, Treppe, Dachboden, Prüfbericht, Vorschau)
import parfumeur_innen as pi  # noqa: E402  (Lichter, Kinder, Wandquader)
import schmied_innen as si  # noqa: E402  (Wände, Pult, Türen, Werkzeugwand, Regal, Kisten, Löschfass, Anker, Blickpunkte)
import tischler_innen as ti  # noqa: E402  (Fenster, Laterne, Hoftor, Animation, Export)
import wirt_innen as wi  # noqa: E402  (Netz versetzt bauen, Punkt am Platz, Sitzplätze, Tisch, Bank, Keller, Falltür, Handschuhe)

NAME = "dieb_innen"
STUFEN = ("Schmugglerloch", "Unterschlupf", "Diebesgilde")

bs.FARBEN.update({
    "Wandholz": (0.35, 0.29, 0.22),           # alte Bretter der Verschalung (Maserung aus den Oberflächendetails)
    "Wandholz hell": (0.43, 0.36, 0.27),
    "Wandholz dunkel": (0.26, 0.21, 0.16),
    "Flickholz": (0.52, 0.43, 0.31),          # neuere Bretter, quer über die Lücken genagelt
    "Lappen": (0.47, 0.42, 0.34),             # Lumpen vor den Fenstern
    "Lappen dunkel": (0.31, 0.27, 0.23),
    "Lappen rot": (0.42, 0.19, 0.16),
    "Stadtplan": (0.80, 0.72, 0.55),          # Pergament
    "Plantinte": (0.30, 0.22, 0.16),
    "Planrot": (0.62, 0.14, 0.12),            # Zeichen und Nadeln auf dem Plan
    "Silbermuenze": (0.70, 0.70, 0.67),
    "Wetzstein": (0.42, 0.43, 0.42),
    "Gildenblau": (0.16, 0.19, 0.33),         # Nachtblau der Gilde (matt gemalt; die Spitze außen ist metallisch)
    "Gildenblau dunkel": (0.11, 0.13, 0.23),
    "Maske": (0.86, 0.84, 0.78),
    "Kutte": (0.36, 0.29, 0.22),              # Verkleidungen
    "Bettlerlumpen": (0.52, 0.47, 0.38),
    "Haendlermantel": (0.48, 0.17, 0.14),
    "Pelz": (0.62, 0.55, 0.44),
    "Pilgergrau": (0.44, 0.44, 0.40),
    "Teppich": (0.50, 0.17, 0.14),            # Diebesgut
    "Teppich hell": (0.78, 0.62, 0.36),
    "Rahmengold": (0.70, 0.55, 0.26),         # vergoldeter Bilderrahmen (matt gemalt)
    "Gemaelde": (0.27, 0.31, 0.24),
    "Gemaelde hell": (0.66, 0.56, 0.42),
    "Strohsack": (0.62, 0.55, 0.38),
    "Decke grob": (0.40, 0.34, 0.27),         # Wolldecken auf den Strohsäcken
    "Decke rot": (0.42, 0.20, 0.16),
    "Thronholz": (0.27, 0.18, 0.12),
    "Siegelwachs": (0.62, 0.12, 0.10),
    "Papier": (0.86, 0.82, 0.70),
    "Hutfilz": (0.21, 0.19, 0.17),
})

# ── Maße ─────────────────────────────────────────────────────────────────────
WAND = ti.WAND                                                     # Bruchstein, innen mit Brettern verschalt, 0,3 m
WX0, WX1, WY0, WY1 = -5.0, 5.0, -3.5, 3.5                          # Diebesstube innen
H_WERK = 2.6
Z_SCHLAF = H_WERK + ia.DIELE                                       # Boden des Schlafbodens über der Stube (ab Stufe 2)
DACH = ia.Dachboden(WX0, WX1, WY0, WY1, Z_SCHLAF, 48, kniestock=1.1, kehle=2.6)
CX0, CX1, CY0, CY1 = -2.4, 4.0, -3.75, 0.95                        # Beutekeller innen
Z_KELLER = -2.7
KAEMPFER, STICH = 1.25, 1.2
LUKE = (-3.95, -2.8, -1.75, -1.15)                                 # Falltür in den Keller (x0, x1, y0, y1)
TREPPE_X1 = -1.85                                                  # die Kellertreppe läuft entlang +x hinunter bis hier
KELLERTUER = (LUKE[2] - 0.1, LUKE[3] + 0.1, 1.95)                  # Bogen in der linken Stirnwand des Kellers (y0, y1, Höhe)
TUNNEL = (3.0, 3.6, -0.55, 0.15)                                   # Falltür zum Fluchttunnel im Kellerboden (x0, x1, y0, y1)
LUKE_S = (4.1, WX1, 2.1, 3.2)                                      # Luke zum Schlafboden in der Decke (die Kante x1 an der Wand)
LEITER_X, LEITER_FUSS = 4.55, 3.42                                 # die Leiter steigt von hinten nach vorn zur Luke
GX0, GX1, GY0, GY1 = -0.9, 6.1, WY1 + WAND, WY1 + WAND + 6.0       # Gildenhalle (ab Stufe 3)
H_GILDE = 3.6
GILDENTUER_X, TUER_B, TUER_H = 2.6, 1.0, 2.05                      # Tür Stube – Gildenhalle (Rückwand)
HAUSTUER_X, HAUSTUER_B, HAUSTUER_H = 3.0, 1.0, 2.1                 # Tür zur Gasse in der Vorderwand
HOFTOR_Y, HOFTOR_B, HOFTOR_H = -1.85, 2.2, 2.3                     # Hoftor in der rechten Wand
FPS = 24
TAKT = fg.BEWEGUNGEN["Schleifen"] * FPS                            # Bilder je Umdrehung des Schleifsteins (1,2 s wie „Schleifen“)
ARBEIT_BILDER = round(5 * TAKT)                                    # Länge der Animation „Arbeit“ (6 s: fünf Umdrehungen)
SITZ = 0.43                                                        # Oberkante von Bänken und Sitz des Throns (wie beim Wirt)

# Geräte (Blender-Koordinaten)
BRETT_Y = WY1 - 0.08                                               # Vorderseite des Übungsbretts
UEBEN_X = [-4.40 + 0.53 * k for k in range(8)]                     # Plätze am Übungsbrett, von links
KARTE = Vector((-2.4, 0.5, 0))                                     # Kartentisch: Mitte, 1,9 × 1,0 m, Platte auf fg.KANZEL[1]
KARTE_L, KARTE_B = 1.9, 1.0
KARTE_ABSTAND = 0.32                                               # die Füße so weit vor der Tischkante (Hände 0,14 m darauf)
ZAEHL = Vector((2.0, 0.9, 0))                                      # Zähltisch: Mitte, 2,6 × 0,8 m, 0,78 m hoch
ZAEHL_X = (1.1, 1.7, 2.3, 2.9)
WETZ = Vector((0.6, -2.35, 0))                                     # Wetztisch: Mitte, 2,2 × 0,7 m, 0,77 m hoch
SCHLEIF = Vector((-3.9, -2.75, 0))                                 # Schleifbock: der Arbeiter steht bei −x und blickt nach +x
THRON = Vector((2.6, GY1 - 0.75, 0))                               # Thron des Meisterdiebs (Mitte des Sitzes am Boden)
PODEST = 0.3                                                       # Höhe des Podests unter dem Thron

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Werk_0": ((0.2, -3.2, 2.15), (-0.8, 2.9, 0.8)),          # Kartentisch, Übungsbrett, Zähltisch
    "Blick_Werk_L1": ((2.9, -2.9, 2.1), (-4.6, 0.2, 0.7)),          # Kartentisch, Schleifbock, Falltür
    "Blick_Werk_L2": ((0.3, 0.3, 2.15), (-3.2, 3.45, 0.9)),         # das Übungsbrett mit den Schlössern
    "Blick_Werk_R1": ((-3.6, -2.7, 2.1), (4.6, 1.6, 0.8)),          # Zähltisch, Werkzeugwand, Leiter, Gildentür
    "Blick_Werk_R2": ((-1.4, 2.4, 2.1), (2.6, -3.3, 0.7)),          # Wetztisch, Haustür, Hoftor
    "Blick_Lager_0": ((3.7, -0.1, Z_KELLER + 1.9), (-2.3, -1.6, Z_KELLER + 0.6)),    # das Gewölbe entlang zur Treppe
    "Blick_Lager_L1": ((-1.9, -2.9, Z_KELLER + 1.8), (3.8, 0.8, Z_KELLER + 0.4)),    # Diebesgut an der Rückwand
    "Blick_Lager_R1": ((-1.9, 0.3, Z_KELLER + 1.8), (3.8, -3.6, Z_KELLER + 0.3)),    # Hehlerregale, Fluchttunnel
    "Blick_Schlafboden_0": ((3.6, 1.4, Z_SCHLAF + 2.2), (-4.6, -0.4, Z_SCHLAF + 0.3)),       # die Strohsäcke unter dem Dach
    "Blick_Schlafboden_L1": ((0.6, 1.6, Z_SCHLAF + 1.9), (-4.0, -2.8, Z_SCHLAF + 0.2)),      # die vordere Reihe
    "Blick_Schlafboden_R1": ((-1.6, 0.9, Z_SCHLAF + 2.0), (4.6, -0.9, Z_SCHLAF + 0.9)),      # Verkleidungen, Spiegel
    "Blick_Gildenhalle_0": ((2.6, GY0 + 0.4, 2.5), (2.6, GY1, 1.2)),                         # Schwurtisch und Thron
    "Blick_Gildenhalle_L1": ((5.7, GY0 + 0.45, 2.5), (-0.7, 8.2, 0.9)),                      # Bänke, Wappen, Geiselstuhl
    "Blick_Gildenhalle_R1": ((-0.5, GY0 + 0.45, 2.5), (5.9, 8.6, 1.0)),                      # Thron, Fackeln, Fenster
}

Oe = si.Oe
WERK_OEFFNUNGEN = {
    "vorne": [Oe(-3.3, 1.05, 0.8, 0.9), Oe(0.6, 1.15, 0.8, 0.9), Oe(HAUSTUER_X, 0.0, HAUSTUER_B, HAUSTUER_H)],
    "rechts": [Oe(HOFTOR_Y, 0.0, HOFTOR_B, HOFTOR_H)],
    "hinten": [Oe(GILDENTUER_X, 0.0, TUER_B, TUER_H)],
    "links": [Oe(1.3, 1.05, 0.8, 0.9)],
}
GILDE_OEFFNUNGEN = {"vorne": [Oe(GILDENTUER_X, 0.0, TUER_B, TUER_H)], "rechts": [Oe(6.6, 2.2, 0.7, 0.6)]}
SCHILDER = {"Tuer_Werk_Lager": "Falltür in den Beutekeller", "Tuer_Lager_Werk": "Treppe hinauf in die Diebesstube",
            "Tuer_Werk_Schlafboden": "Leiter zum Schlafboden", "Tuer_Schlafboden_Werk": "Leiter hinunter in die Diebesstube",
            "Tuer_Werk_Gildenhalle": "Tür zur Gildenhalle", "Tuer_Gildenhalle_Werk": "Tür zur Diebesstube"}
SITZENDE = ("Zaehlen", "Feinarbeit", "Sitzen")

ecken = ia.ecken
drehkoerper, scheibe, am_ort = ai.drehkoerper, ai.scheibe, ai.am_ort
licht, wq = pi.licht, pi.wq
drehen = wi.drehen


# ─────────────────────────────────────────────────────────────────────────────
#  Hilfen
# ─────────────────────────────────────────────────────────────────────────────

@contextlib.contextmanager
def wirtskeller():
    """Gewölbe, Stirnwände, Kellertreppe und Falltür baut wirt_innen nach seinen Maßen: Für die Dauer des Blocks gelten dort die
    Maße des Beutekellers (wie wirt_innen.brennblase die Destille des Parfumeurs versetzt)."""
    werte = dict(CX0=CX0, CX1=CX1, CY0=CY0, CY1=CY1, Z_KELLER=Z_KELLER, KAEMPFER=KAEMPFER, STICH=STICH, LUKE=LUKE,
                 TREPPE_X0=LUKE[0], TREPPE_X1=TREPPE_X1, KELLERTUER=KELLERTUER)
    alt = {k: getattr(wi, k) for k in werte}
    for k, v in werte.items():
        setattr(wi, k, v)
    try:
        yield
    finally:
        for k, v in alt.items():
            setattr(wi, k, v)


def dieb(liste, punkt, blick, taetigkeit, bewegung, platz, z=0.0, sitz=None, stufe=None):
    """Platz eines Diebs: Dieb_<n> (fortlaufend über alle Tätigkeiten) mit extras „taetigkeit“, „bewegung“, „platz“ (Füllreihenfolge
    je Tätigkeit) und bei sitzenden Plätzen „sitzhoehe“ (Oberkante der Bank über dem Fußpunkt)."""
    o = bs.anker(f"Dieb_{len(liste) + 1}", (punkt[0], punkt[1], z), si.drehung_von(blick))
    o["taetigkeit"], o["bewegung"], o["platz"] = taetigkeit, bewegung, platz
    if sitz is not None:
        o["sitzhoehe"] = sitz
    if stufe:
        o["stufe"] = stufe
    liste.append(o)
    return o


def flach(netz, st, umriss, M, dicke, stoff, rand=None):
    """Flaches Teil (Stoff, Brett): Umriss [(u, v)] in der Ebene x–z des örtlichen Systems M, `dicke` entlang örtlich y; Vorder- und
    Rückseite, Ränder in `rand` (sonst wie die Fläche)."""
    vorn = [M @ Vector((u, -dicke / 2, v)) for u, v in umriss]
    hinten = [M @ Vector((u, dicke / 2, v)) for u, v in umriss]
    n = M.to_3x3() @ Vector((0, -1, 0))
    netz.vieleck_nach(vorn, n, st[stoff])
    netz.vieleck_nach(hinten, -n, st[stoff])
    for i in range(len(umriss)):
        j = (i + 1) % len(umriss)
        quad = [vorn[i], vorn[j], hinten[j], hinten[i]]
        mitte = sum(quad, Vector()) / 4
        aussen = mitte - sum(vorn + hinten, Vector()) / (2 * len(umriss))
        netz.vieleck_nach(quad, aussen, st[rand or stoff])


def kerze(netz, st, fuss, h=0.12, r=0.022, anker=None, stufe=1):
    """Kerzenstumpen mit Flamme („Glut“) und Wachsnase; mit `anker` ein Licht „Glut“ darüber."""
    drehkoerper(netz, st, fuss, [(0.0, r * 1.5), (0.012, r * 1.4), (0.012, r), (h, r * 0.95)], "Kerze", n=6)
    drehkoerper(netz, st, Vector(fuss) + Vector((0, 0, h)), [(0.0, 0.0), (0.012, 0.011), (0.035, 0.008), (0.06, 0.0)], "Glut", n=5)
    if anker is not None:
        anker.append(licht(Vector(fuss) + Vector((0, 0, h + 0.12)), (0, 0, 1), "Glut", stufe))


def muenzen(netz, st, mitte, r=0.07, h=0.035, saat=0):
    """Münzhaufen: ein flacher Kegel aus Gold, darauf und daneben einzelne Gold- und Silbermünzen."""
    m = Vector(mitte)
    drehkoerper(netz, st, m, [(0.0, r), (h * 0.6, r * 0.55), (h, 0.0)], "Gold", n=8)
    zufall = random.Random(saat)
    for k in range(5):
        w = zufall.uniform(0, 2 * math.pi)
        d = zufall.uniform(r * 0.9, r * 1.6)
        scheibe(netz, st, m + Vector((d * math.cos(w), d * math.sin(w), 0)), 0.012, m.z + 0.003 + 0.002 * (k % 2),
                "Silbermuenze" if k % 2 else "Gold", n=6)


def beutel(netz, st, boden, r=0.06, h=0.1, zu=True):
    """Lederbeutel, zugebunden (oder offen) auf dem Tisch."""
    profil = [(0.0, r * 0.6), (0.02, r * 0.92), (h * 0.5, r), (h, r * 0.5), (h + 0.02, r * 0.25 if zu else r * 0.6)]
    drehkoerper(netz, st, boden, profil, "Leder", n=8)
    if zu:
        drehkoerper(netz, st, Vector(boden) + Vector((0, 0, h + 0.02)), [(0.0, r * 0.25), (0.025, r * 0.45), (0.03, 0.0)], "Leder", n=6)
    else:
        scheibe(netz, st, boden, r * 0.55, Vector(boden).z + h + 0.015, "Kohle", n=8)


# ─────────────────────────────────────────────────────────────────────────────
#  Wände: Bruchstein, mit alten Brettern verschalt
# ─────────────────────────────────────────────────────────────────────────────

def bretterwand(netz, st, a, b, hoehe, oeffnungen=(), saat=0, frei=()):
    """Verschalung der Wand a → b (der Raum liegt links): senkrechte Bretter (2,5 cm vor dem Bruchstein, Fugen mit Lücken), einzelne
    fehlen oder sind unten weggefault, quer genagelte Flicken aus helleren Brettern; oben ein Rähm, unten eine Schwelle; um die
    Öffnungen Futterbretter. `frei`: Stücke [(u0, u1)], in denen die Bretter erst oberhalb von 1,62 m beginnen (dort steht etwas vor
    der Wand)."""
    wand = ia.Wand(a, b, oeffnungen)
    zufall = random.Random(saat)
    farben = ("Wandholz", "Wandholz hell", "Wandholz", "Wandholz dunkel")
    u = 0.0
    while u < wand.laenge - 0.05:
        breite = zufall.uniform(0.16, 0.28)
        u1 = min(u + breite, wand.laenge)
        if wand.laenge - u1 < 0.1:
            u1 = wand.laenge
        fuge = zufall.uniform(0.006, 0.016)
        stoff = zufall.choice(farben)
        wurf = zufall.random()
        if wurf > 0.06:                                                          # sonst fehlt das Brett: Bruchstein dahinter
            unten = zufall.uniform(0.25, 0.7) if wurf < 0.16 else 0.0           # unten weggefault
            if any(u < f1 and u1 > f0 for f0, f1 in frei):
                unten = 1.62
            for v0, v1 in _stuecke(wand, u + fuge / 2, u1 - fuge / 2, unten, hoehe - 0.15):
                wq(netz, st, wand, u + fuge / 2, u1 - fuge / 2, v0, v1, 0.0, 0.025, stoff)
        u = u1
    for u0, u1 in pi.freie_stuecke(wand, hoehe - 0.16, hoehe, rand=-0.02):       # Rähm oben
        wq(netz, st, wand, u0, u1, hoehe - 0.15, hoehe, 0.0, 0.06, "Fachwerk", ohne=("-y", "+z"))
    for u0, u1 in pi.freie_stuecke(wand, 0.0, 0.12, rand=0.16):                   # Schwelle unten (bis an den Türstock)
        wq(netz, st, wand, u0, u1, 0.0, 0.1, 0.0, 0.045, "Fachwerk", ohne=("-y", "-z"))
    for m, o in wand.ops:                                                          # Futterbretter um die Fenster (Türen: Türstock)
        if o.v < 0.05:
            continue
        u0, u1, v0, v1 = m - o.b / 2, m + o.b / 2, o.v, o.v + o.h
        wq(netz, st, wand, u0 - 0.1, u0, v0, v1, 0.0, 0.04, "Fachwerk", ohne=("-y",))
        wq(netz, st, wand, u1, u1 + 0.1, v0, v1, 0.0, 0.04, "Fachwerk", ohne=("-y",))
        wq(netz, st, wand, u0 - 0.1, u1 + 0.1, v1, min(v1 + 0.12, hoehe - 0.15), 0.0, 0.045, "Fachwerk", ohne=("-y",))
        if v0 > 0.05:
            wq(netz, st, wand, u0 - 0.12, u1 + 0.12, v0 - 0.06, v0, 0.0, 0.1, "Holz alt", ohne=("-y",))
    for _ in range(max(2, round(wand.laenge / 1.6))):                              # Flicken quer über die Bretter
        lang = zufall.uniform(0.45, 1.1)
        u0 = zufall.uniform(0.1, wand.laenge - lang - 0.1)
        v = zufall.uniform(0.3, hoehe - 0.5)
        h = zufall.uniform(0.11, 0.19)
        if wand.offen(u0 - 0.15, u0 + lang + 0.15, v - 0.15, v + h + 0.15) or any(u0 < f1 and u0 + lang > f0 for f0, f1 in frei):
            continue
        neig = zufall.uniform(-0.07, 0.07)
        M = (Matrix.Translation(wand.p(u0 + lang / 2, v + h / 2, 0.035)) @ Matrix((wand.d, wand.n, Vector((0, 0, 1)))).transposed().to_4x4()
             @ Matrix.Rotation(neig, 4, "Y"))
        netz.quader((0, 0, 0), (lang, 0.022, h), st["Flickholz"], M, ohne=("-y",))
        for s in (-1, 1):                                                          # Nagelköpfe
            for dv in (-0.035, 0.035):
                netz.quader((s * (lang / 2 - 0.05), 0.012, dv), (0.014, 0.004, 0.014), st["Eisen"], M, ohne=("-y",))


def _stuecke(wand, u0, u1, v0, v1):
    """Senkrechte Stücke eines Bretts von v0 bis v1 zwischen u0 und u1, die keine Öffnung schneiden (Futterbretter mit Rand)."""
    stuecke = [(v0, v1)]
    for m, o in wand.ops:
        if u0 < m + o.b / 2 + 0.1 and u1 > m - o.b / 2 - 0.1:
            a, b = o.v - (0.06 if o.v > 0.05 else 0.0), o.v + o.h + 0.12
            stuecke = [s for x0, x1 in stuecke for s in ((x0, min(x1, a)), (max(x0, b), x1)) if s[1] - s[0] > 0.04]
    return stuecke


def stube_huelle(werk, decke, schlaf, st, anker):
    """Wände der Stube (Bruchstein mit Verschalung), Fenster mit Lappen davor, Balkendecke mit der Luke zum Schlafboden, Lehmboden
    mit dem Loch der Falltür."""
    W = WERK_OEFFNUNGEN
    kanten = ecken(WX0, WX1, WY0, WY1)
    werk.alle = True
    for seite in ("vorne", "rechts", "hinten", "links"):
        si.wand(werk, st, *kanten[seite], H_WERK, W[seite], stoff="Bruchstein dunkel", dicke=WAND, aussen=seite != "hinten",
                fugen=True, saat=11 + len(seite))
    werk.alle = False
    frei = {"hinten": [(WX1 - UEBEN_X[-1] - 0.2, WX1 - UEBEN_X[0] + 0.2)]}         # hinter dem Übungsbrett keine Bretter (u ab x1)
    for i, seite in enumerate(("vorne", "rechts", "hinten", "links")):
        bretterwand(werk, st, *kanten[seite], H_WERK, W[seite], saat=21 + 7 * i, frei=frei.get(seite, ()))
    for seite, liste in W.items():
        a, b = kanten[seite]
        for o in liste:
            if o.v < 0.05:
                continue
            ti.fenster(werk, st, a, b, o)
            lappen(werk, st, ia.Wand(a, b, [o]), o, saat=int(o.s * 10) + len(seite))
            wand = ia.Wand(a, b, [o])
            p = wand.p(wand.ops[0][0], o.v + o.h / 2, 0.1)
            anker.append(licht(p, (wand.n.x, wand.n.y, 0), "Fenster"))
    ia.balkendecke(decke, st, WX0, WX1, WY0, WY1, H_WERK, luke=LUKE_S, oben=schlaf, saat=7)
    for r in ia.rechteck_minus((WX0, WX1, WY0, WY1), LUKE):
        ia.lehmboden(werk, st, *r, saat=int(r[0] * 10 + r[2] * 3), flecken=max(2, round((r[1] - r[0]) * (r[3] - r[2]) / 6)),
                     halme=max(2, round((r[1] - r[0]) * (r[3] - r[2]) * 0.6)))


def lappen(netz, st, wand, o, saat=0):
    """Lumpen vor einem Fenster: eine Stange über der Öffnung, daran zwei bis drei Lappen mit ausgefranstem Saum, die das Glas zum
    größten Teil verdecken; einer ist zur Seite geschlagen und lässt einen Spalt Licht herein."""
    zufall = random.Random(saat)
    u = wand.ops[0][0]
    u0, u1 = u - o.b / 2 - 0.08, u + o.b / 2 + 0.08
    oben = o.v + o.h + 0.08
    netz.stab(wand.p(u0 - 0.05, oben, 0.06), wand.p(u1 + 0.05, oben, 0.06), 0.03, 0.03, st["Holz alt"])
    teile = [(u0, u - 0.05, "Lappen"), (u + 0.12, u1, zufall.choice(("Lappen dunkel", "Lappen rot")))]
    for k, (a, b, stoff) in enumerate(teile):
        n = 6
        saum = []
        for j in range(n + 1):
            x = a + (b - a) * j / n
            saum.append((x, o.v - 0.06 + zufall.uniform(-0.05, 0.12)))
        umriss = [(a, oben - 0.02), (b, oben - 0.02)] + [(x, v) for x, v in reversed(saum)]
        pkt = [wand.p(x, v, 0.05 + 0.012 * k + 0.01 * math.sin(7 * x)) for x, v in umriss]
        netz.vieleck_nach(pkt, wand.n, st[stoff])
        netz.vieleck_nach(list(reversed(pkt)), -wand.n, st[stoff])
    netz.stab(wand.p(u + 0.02, o.v + o.h * 0.55, 0.07), wand.p(u + 0.1, o.v + o.h * 0.4, 0.08), 0.05, 0.02, st["Lappen dunkel"])   # Spalt


# ─────────────────────────────────────────────────────────────────────────────
#  Stube: Übungsbrett, Kartentisch, Zähltisch, Wetztisch, Schleifbock
# ─────────────────────────────────────────────────────────────────────────────

def uebungsbrett(netz, st, plaetze):
    """Das Übungsbrett an der Rückwand: eine breite Bohle auf zwei Latten, acht verschiedene Schlösser (Kastenschloss, Vorhängeschloss
    am Überwurf, Truhenschloss, Riegelschloss), das Schlüsselloch je bei figuren.KNACKEN vor dem Platz. Darüber ein Bord mit
    Schlüsseln und Ketten, Kreidestriche. Gibt die Plätze (Fußpunkte) in Füllreihenfolge zurück."""
    sx, sv, sh = fg.KNACKEN["schloss"]
    x0, x1 = UEBEN_X[0] - 0.35, UEBEN_X[-1] + 0.35
    netz.quader(((x0 + x1) / 2, BRETT_Y + 0.02, 1.05), (x1 - x0, 0.04, 1.0), st["Holz alt"], ohne=("+y",))
    for z in (0.65, 1.45):
        netz.quader(((x0 + x1) / 2, BRETT_Y + 0.06, z), (x1 - x0 + 0.1, 0.04, 0.08), st["Fachwerk"], ohne=("+y",))
    for x in (x0 + 0.05, (x0 + x1) / 2, x1 - 0.05):
        netz.quader((x, BRETT_Y + 0.065, 0.95), (0.08, 0.03, 1.9), st["Fachwerk"], ohne=("+y",))
    e, k_ = st["Eisen"], st["Kohle"]
    for i, xa in enumerate(UEBEN_X):
        x = xa - sx                                                          # Blick +y: rechts der Figur ist +x
        art = i % 4
        y = BRETT_Y
        loch = [(x - 0.006, y - 0.0005, sh - 0.016), (x + 0.006, y - 0.0005, sh - 0.016), (x + 0.006, y - 0.0005, sh + 0.004),
                (x - 0.006, y - 0.0005, sh + 0.004)]
        if art == 0:                                                         # Kastenschloss mit Drücker
            netz.quader((x + 0.03, y - 0.025, sh + 0.0), (0.2, 0.05, 0.15), e, ohne=("+y",))
            netz.quader((x + 0.13, y - 0.07, sh + 0.04), (0.02, 0.06, 0.02), e)
            netz.quader((x + 0.13, y - 0.1, sh + 0.04), (0.1, 0.02, 0.02), e)
            loch = [(px, y - 0.0505, pz) for px, _, pz in loch]
        elif art == 1:                                                       # Überwurf mit Vorhängeschloss
            netz.quader((x + 0.0, y - 0.006, sh + 0.12), (0.06, 0.012, 0.16), e, ohne=("+y",))
            netz.quader((x, y - 0.03, sh + 0.035), (0.09, 0.04, 0.08), e)
            for s in (-1, 1):
                netz.stab((x + s * 0.03, y - 0.03, sh + 0.075), (x + s * 0.025, y - 0.03, sh + 0.12), 0.012, 0.012, e)
            loch = [(px, y - 0.0505, pz) for px, _, pz in loch]
        elif art == 2:                                                       # Truhe auf einer Konsole, das Schloss vorn
            netz.quader((x, y - 0.13, sh - 0.12), (0.4, 0.26, 0.04), st["Holz"])
            netz.quader((x, y - 0.14, sh + 0.0), (0.36, 0.22, 0.2), st["Holz alt"])
            netz.quader((x, y - 0.14, sh + 0.115), (0.37, 0.23, 0.03), st["Holz"])
            for s in (-1, 1):
                netz.quader((x + s * 0.13, y - 0.14, sh), (0.03, 0.225, 0.205), e)
            netz.quader((x, y - 0.251, sh - 0.004), (0.07, 0.006, 0.09), e)
            loch = [(px, y - 0.2545, pz) for px, _, pz in loch]
        else:                                                                # Riegelschloss mit Schlossblech
            netz.quader((x, y - 0.004, sh), (0.12, 0.008, 0.18), e, ohne=("+y",))
            netz.stab((x - 0.2, y - 0.02, sh + 0.06), (x + 0.04, y - 0.02, sh + 0.06), 0.025, 0.025, e)
            netz.quader((x - 0.06, y - 0.03, sh + 0.06), (0.02, 0.03, 0.05), e)
            loch = [(px, y - 0.0085, pz) for px, _, pz in loch]
        netz.vieleck_nach(loch, (0, -1, 0), k_)
    # Bord mit Schlüsseln, Ketten und Kreidestriche
    netz.quader(((x0 + x1) / 2, BRETT_Y + 0.0 - 0.1, 1.66), (x1 - x0 - 0.2, 0.2, 0.03), st["Holz alt"])
    zufall = random.Random(3)
    x = x0 + 0.2
    while x < x1 - 0.2:
        h = zufall.uniform(0.08, 0.14)
        netz.stab((x, BRETT_Y - 0.01, 1.62), (x, BRETT_Y - 0.01, 1.62 - h), 0.012, 0.004, e)
        bs.achteck(netz, st, Vector((x, BRETT_Y - 0.01, 1.62 - h - 0.02)), 0.018, 0.0, 0.004, "Eisen", n=6)
        x += zufall.uniform(0.12, 0.3)
    for k in range(9):
        xs = x0 + 0.3 + 0.045 * k
        netz.vieleck_nach([(xs, BRETT_Y - 0.001, 1.25), (xs + 0.012, BRETT_Y - 0.001, 1.25), (xs + 0.012, BRETT_Y - 0.001, 1.4),
                           (xs, BRETT_Y - 0.001, 1.4)], (0, -1, 0), st["Papier"])
    reihenfolge = (3, 4, 2, 5, 1, 6, 0, 7)
    for platz, i in enumerate(reihenfolge, 1):
        dieb(plaetze, (UEBEN_X[i], BRETT_Y - sv), (0, 1), "Ueben", "Knacken", platz)


def kartentisch(netz, st, plaetze, anker):
    """Bedienung_Auftraege_Karte: ein hoher Kartentisch (Platte auf der Höhe der Brüstung von figuren.KANZEL – die Gesten beim Planen
    sind die des Predigers), darauf der Stadtplan auf Pergament mit Gassen, Mauerring und roten Zeichen, Nadeln, ein Dolch steckt im
    Plan, Münzen beschweren die Ecken, zwei Kerzen. Acht Plätze zum Planen rundum."""
    m, l, b = KARTE, KARTE_L, KARTE_B
    h = fg.KANZEL[1]
    netz.quader(m + Vector((0, 0, h - 0.03)), (l, b, 0.06), st["Tischplatte"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader(m + Vector((sx * (l / 2 - 0.12), sy * (b / 2 - 0.1), (h - 0.06) / 2)), (0.09, 0.09, h - 0.06), st["Holz"])
        netz.quader(m + Vector((sx * (l / 2 - 0.12), 0, 0.2)), (0.06, b - 0.2, 0.07), st["Holz"])
    netz.quader(m + Vector((0, 0, 0.2)), (l - 0.24, 0.06, 0.07), st["Holz"])
    z = h + 0.002
    zufall = random.Random(17)
    rand = [(-0.72, -0.4), (-0.2, -0.43), (0.35, -0.38), (0.74, -0.42), (0.7, 0.05), (0.76, 0.4), (0.1, 0.43), (-0.5, 0.38),
            (-0.75, 0.42), (-0.7, 0.0)]
    netz.vieleck_nach([m + Vector((x, y, z)) for x, y in rand], (0, 0, 1), st["Stadtplan"])
    for k in range(16):                                                    # Mauerring
        w0, w1 = 2 * math.pi * k / 16, 2 * math.pi * (k + 1) / 16
        a = m + Vector((0.5 * math.cos(w0), 0.3 * math.sin(w0), z + 0.001))
        e = m + Vector((0.5 * math.cos(w1), 0.3 * math.sin(w1), z + 0.001))
        netz.stab(a, e, 0.012, 0.001, st["Plantinte"], quer=(0, 0, 1))
    for k in range(9):                                                     # Gassen
        a = m + Vector((zufall.uniform(-0.45, 0.45), zufall.uniform(-0.26, 0.26), z + 0.0012))
        e = a + Vector((zufall.uniform(-0.3, 0.3), zufall.uniform(-0.2, 0.2), 0))
        netz.stab(a, e, 0.006, 0.001, st["Plantinte"], quer=(0, 0, 1))
    for k, (x, y) in enumerate(((0.18, 0.08), (-0.25, -0.12), (0.35, -0.18))):   # rote Zeichen und Nadeln
        p = m + Vector((x, y, z + 0.0014))
        netz.stab(p - Vector((0.03, 0.03, 0)), p + Vector((0.03, 0.03, 0)), 0.008, 0.001, st["Planrot"], quer=(0, 0, 1))
        netz.stab(p - Vector((0.03, -0.03, 0)), p + Vector((0.03, -0.03, 0)), 0.008, 0.001, st["Planrot"], quer=(0, 0, 1))
        netz.stab(p + Vector((0.04, 0.0, 0)), p + Vector((0.04, 0.0, 0.05)), 0.004, 0.004, st["Eisen"])
        bs.achteck(netz, st, p + Vector((0.04, 0.0, 0.05)), 0.008, 0.0, 0.012, "Planrot", n=5)
    dolch = m + Vector((-0.05, 0.05, z))                                   # Dolch steckt im Plan
    netz.stab(dolch, dolch + Vector((0.0, 0.0, 0.14)), 0.02, 0.005, st["Eisen"], quer=(1, 0, 0))
    netz.quader(dolch + Vector((0, 0, 0.145)), (0.08, 0.016, 0.014), st["Eisen"])
    netz.stab(dolch + Vector((0, 0, 0.15)), dolch + Vector((0, 0, 0.24)), 0.022, 0.022, st["Leder"])
    bs.achteck(netz, st, dolch + Vector((0, 0, 0.24)), 0.016, 0.0, 0.02, "Eisen", n=6)
    for x, y in ((-0.7, -0.38), (0.7, 0.38), (0.72, -0.4)):
        muenzen(netz, st, m + Vector((x, y, h)), r=0.03, h=0.012, saat=int(x * 10 + y * 7))
    kerze(netz, st, m + Vector((-0.82, 0.38, h)), 0.14, anker=anker)
    kerze(netz, st, m + Vector((0.84, -0.32, h)), 0.09, anker=anker)
    # Plätze rundum: erst die Seite hinten (Gesicht zur Kamera), dann die Stirnseiten, zuletzt vorn
    a = KARTE_ABSTAND
    nord, sued = m.y + b / 2 + a, m.y - b / 2 - a
    liste = [((m.x, nord), (0, -1)), ((m.x - 0.6, nord), (0, -1)), ((m.x + 0.6, nord), (0, -1)),
             ((m.x + l / 2 + a, m.y), (-1, 0)), ((m.x - l / 2 - a, m.y), (1, 0)),
             ((m.x, sued), (0, 1)), ((m.x - 0.6, sued), (0, 1)), ((m.x + 0.6, sued), (0, 1))]
    for platz, (punkt, blick) in enumerate(liste, 1):
        dieb(plaetze, punkt, blick, "Planen", "Predigen", platz)


def zaehltisch(netz, st, plaetze, anker):
    """Der Zähltisch rechts: Tisch und zwei Bänke wie beim Wirt, vor jedem Platz ein Münzhaufen (figuren.ZAEHLEN), dazwischen zugebundene
    Beutel, ein Rechenbrett mit Kreidestrichen, eine Kerze; am Ende die Münzwaage (ihr Balken pendelt in der Animation). Acht Plätze,
    erst die Bank hinten (Gesicht zur Kamera). Gibt den Waagbalken zurück."""
    m = ZAEHL
    wi.tisch(netz, st, m, 2.6, 0.8)
    for s in (-1, 1):
        wi.bank(netz, st, (m.x - 1.3, m.y + s * 0.62, 0), (m.x + 1.3, m.y + s * 0.62, 0))
    hs, hv, hh = fg.ZAEHLEN["haufen"]
    platz = 1
    for sy, blick in ((1, (0, -1)), (-1, (0, 1))):
        for x in ZAEHL_X:
            punkt = Vector((x, m.y + sy * 0.62, 0))
            sitz = fg.VOELKER["mensch"]["knie"] - 0.02
            muenzen(netz, st, drehen((hs, hv, sitz + hh), punkt, blick), saat=platz)
            o = dieb(plaetze, punkt, blick, "Zaehlen", "Zaehlen", platz, sitz=SITZ)
            platz += 1
    zufall = random.Random(5)
    for k in range(5):
        beutel(netz, st, Vector((zufall.uniform(m.x - 1.1, m.x + 0.9), m.y + zufall.uniform(-0.06, 0.06), 0.78)), r=0.045, h=0.07)
    netz.quader(m + Vector((-1.05, 0.02, 0.785)), (0.3, 0.22, 0.01), st["Kohle"])                 # Rechenbrett mit Kreide
    for k in range(5):
        netz.quader(m + Vector((-1.15 + 0.045 * k, 0.02, 0.791)), (0.008, 0.16, 0.002), st["Papier"])
    kerze(netz, st, m + Vector((0.15, -0.02, 0.78)), 0.11, anker=anker)
    # Münzwaage am Ende des Tischs: Säule, Balken (bewegt), zwei Schalen
    w = m + Vector((1.15, 0.0, 0.78))
    drehkoerper(netz, st, w, [(0.0, 0.07), (0.02, 0.06), (0.02, 0.012), (0.3, 0.01)], "Messing", n=8)
    balken = bs.Netz("Waagbalken", alle=True)
    dreh = w + Vector((0, 0, 0.3))
    balken.stab(dreh - Vector((0, 0.13, 0)), dreh + Vector((0, 0.13, 0)), 0.012, 0.012, st["Messing"])
    for s in (-1, 1):
        haken = dreh + Vector((0, s * 0.12, 0))
        for d in (-1, 1):
            balken.stab(haken, haken + Vector((d * 0.035, 0, -0.16)), 0.003, 0.003, st["Messing"])
        drehkoerper(balken, st, haken - Vector((0, 0, 0.18)), [(0.0, 0.0), (0.0, 0.03), (0.02, 0.05)], "Messing", n=8)
    scheibe(balken, st, dreh + Vector((0, -0.12, 0)), 0.006, w.z - 0.0, "Gold", n=5)  # ein Körnchen Gold auf der Schale
    return am_ort(balken, dreh)


def wetztisch(netz, st, plaetze, schleifbock_platz):
    """Der Wetztisch vorn: niedriger Tisch (Feinwerktisch, figuren.ARBEITSHOEHEN „Tisch“) mit Bänken, vor jedem Platz ein Wetzstein
    auf einem Brettchen, Messer, Dolche, ein Ölkännchen und Lappen. Die Plätze 2–8 des Schleifens (sitzend, Bewegung
    „Feinarbeit“); Platz 1 ist der Schleifbock."""
    m = WETZ
    wi.tisch(netz, st, m, 2.2, 0.7, hoehe=0.77)
    for s in (-1, 1):
        wi.bank(netz, st, (m.x - 1.1, m.y + s * 0.57, 0), (m.x + 1.1, m.y + s * 0.57, 0))
    plaetze_liste = [((x, m.y + 0.57), (0, -1)) for x in (-0.2, 0.35, 0.9, 1.45)] + [((x, m.y - 0.57), (0, 1)) for x in (0.075, 0.625, 1.175)]
    vor = fg.ARBEITSHOEHEN["Tisch"][0]
    zufall = random.Random(9)
    dieb(plaetze, schleifbock_platz, (1, 0), "Schleifen", "Schleifen", 1)
    for platz, (punkt, blick) in enumerate(plaetze_liste, 2):
        stein = drehen((0.0, vor - 0.02, 0.77), (punkt[0], punkt[1], 0.0), blick)
        d = Vector((blick[0], blick[1], 0))
        q = Vector((-d.y, d.x, 0))
        netz.quader(stein + Vector((0, 0, 0.008)), (0.2, 0.2, 0.016), st["Holz alt"], Matrix.Rotation(math.atan2(d.y, d.x), 4, "Z"))
        netz.quader((0, 0, 0), (0.17, 0.05, 0.03), st["Wetzstein"],
                    Matrix.Translation(stein + Vector((0, 0, 0.031))) @ Matrix.Rotation(math.atan2(q.y, q.x), 4, "Z"))
        messer = stein + q * 0.16 + d * 0.02 + Vector((0, 0, 0.003))
        netz.stab(messer, messer + q * 0.1, 0.02, 0.004, st["Holz alt"], quer=(0, 0, 1))
        netz.stab(messer - q * 0.0, messer - q * 0.14 + d * 0.01, 0.022, 0.003, st["Eisen"], quer=(0, 0, 1))
        dieb(plaetze, punkt, blick, "Schleifen", "Feinarbeit", platz, sitz=SITZ)
    drehkoerper(netz, st, m + Vector((0.95, 0.0, 0.77)), [(0.0, 0.035), (0.06, 0.04), (0.09, 0.015), (0.12, 0.012)], "Messing", n=6)
    for k in range(3):
        netz.quader(m + Vector((-0.7 + 0.5 * k, zufall.uniform(-0.05, 0.05), 0.775)), (0.2, 0.14, 0.01), st["Lappen"],
                    Matrix.Rotation(zufall.uniform(0, 1), 4, "Z"))


def schleifbock(netz, st):
    """Der Tretschleifbock wie beim Schmied (schmied_innen.schleifbock), aber der Schleifstein ist ein eigenes Netz „Schleifstein“
    (er dreht sich in der Animation um seine Achse entlang y). Gibt (Schleifstein, Fußpunkt des Arbeiters) zurück."""
    hilf = bs.Netz("_hilf", alle=netz.alle)
    si.schleifbock(hilf, st, SCHLEIF)
    r, z = 0.38, 0.72
    stein_m = st["Fels Sandstein"]
    staub_m = st["Steinstaub"]
    for flaeche, k in zip(hilf.flaechen, hilf.fstoff):                     # alles außer dem Stein übernehmen
        if hilf.stoffe[k] in (stein_m, staub_m):
            continue
        netz.vieleck([hilf.ecken[i] for i in flaeche], hilf.stoffe[k], immer=True)
    stein = bs.Netz("Schleifstein", alle=True)
    teile = 12
    ring = [(r * math.cos(2 * math.pi * k / teile), z + r * math.sin(2 * math.pi * k / teile)) for k in range(teile)]
    stein.prisma(ring, -0.07, 0.07, stein_m, matrix=Matrix.Translation(SCHLEIF) @ Matrix.Rotation(math.radians(90), 4, "Z"),
                 m_enden=staub_m)
    for k in range(teile):                                                 # Rillen auf der Seite: man sieht ihn drehen
        if k % 3:
            continue
        w = 2 * math.pi * k / teile
        for s in (-1, 1):
            a = SCHLEIF + Vector((0.12 * math.cos(w), s * 0.0705, z + 0.12 * math.sin(w)))
            e = SCHLEIF + Vector((0.34 * math.cos(w), s * 0.0705, z + 0.34 * math.sin(w)))
            stein.stab(a, e, 0.02, 0.002, st["Bruchstein dunkel"], quer=(0, 1, 0))
    return am_ort(stein, SCHLEIF + Vector((0, 0, z))), SCHLEIF + Vector((-0.6, 0, 0))


def stein_bewegung(o):
    """Der Schleifstein dreht sich in TAKT Bildern einmal um seine Achse (y), gleichmäßig."""
    ruhe = o.rotation_euler.copy()

    def bewegung(bild):
        return None, (ruhe.x, ruhe.y - 2 * math.pi * bild / TAKT, ruhe.z), None
    return [(o, bewegung)]


def waage_bewegung(o):
    """Der Waagbalken pendelt zweimal je Schleife um ±5° um die Achse x und kommt zur Ruhe zurück."""
    ruhe = o.rotation_euler.copy()

    def bewegung(bild):
        return None, (ruhe.x + math.radians(5) * math.sin(4 * math.pi * bild / ARBEIT_BILDER), ruhe.y, ruhe.z), None
    return [(o, bewegung)]


def werkzeug_dieb(netz, st, M, stufe):
    """Gekauft an der Werkzeugwand: Werkzeug1 – Dietriche an den Haken der linken Hälfte, ein Brecheisen, ein Seil mit Wurfhaken;
    Werkzeug2 – Meisterdietriche in einer offenen Lederrolle auf dem Brett unter der Wand (rechte Hälfte)."""
    e = st["Eisen"]
    unten = M.to_3x3() @ Vector((0, 0, -1))
    vorn = M.to_3x3() @ Vector((0, 1, 0))
    seite = M.to_3x3() @ Vector((1, 0, 0))
    if stufe == 1:
        for k, (x, z) in enumerate(si.HAKEN):
            if x >= 0:
                continue
            a = M @ Vector((x, 0.12, z + 0.02))
            if k % 9 == 0:                                                 # Brecheisen
                netz.stab(a, a + unten * 0.55, 0.025, 0.025, e)
                netz.stab(a + unten * 0.55, a + unten * 0.58 + vorn * 0.05, 0.03, 0.012, e, quer=seite)
            elif k % 9 == 2:                                               # Seil mit Wurfhaken
                for j in range(8):
                    w0, w1 = 2 * math.pi * j / 8, 2 * math.pi * (j + 1) / 8
                    c = a + unten * 0.18
                    netz.stab(c + (seite * math.cos(w0) - unten * math.sin(w0)) * 0.13 + vorn * 0.01 * (j % 2),
                              c + (seite * math.cos(w1) - unten * math.sin(w1)) * 0.13, 0.03, 0.03, st["Seil"])
                haken = a + unten * 0.4
                netz.stab(a + unten * 0.3, haken, 0.02, 0.02, e)
                for s in (-1, 1):
                    netz.stab(haken, haken + seite * s * 0.07 - unten * 0.06, 0.012, 0.012, e)
            else:                                                          # Dietrich mit Ring
                netz.stab(a, a + unten * 0.04, 0.012, 0.012, e)
                netz.stab(a + unten * 0.04, a + unten * 0.2, 0.005, 0.004, e)
                netz.stab(a + unten * 0.2, a + unten * 0.2 + seite * 0.015, 0.005, 0.004, e)
    else:
        rolle = M @ Vector((0.42, 0.18, 1.02))
        netz.quader((0, 0, 0), (0.62, 0.3, 0.008), st["Leder"], Matrix.Translation(rolle) @ M.to_3x3().to_4x4())
        netz.stab(rolle - seite * 0.34 - vorn * 0.15 + Vector((0, 0, 0.03)), rolle - seite * 0.34 + vorn * 0.15 + Vector((0, 0, 0.03)),
                  0.06, 0.06, st["Leder"])                                  # der aufgerollte Rest
        for j in range(9):
            p = rolle - seite * 0.26 + seite * 0.065 * j + Vector((0, 0, 0.006))
            netz.stab(p - vorn * 0.1, p + vorn * 0.09, 0.006, 0.003, e, quer=(0, 0, 1))
            netz.stab(p + vorn * 0.09, p + vorn * 0.09 + seite * 0.012 * (1 if j % 2 else -1), 0.006, 0.003, e, quer=(0, 0, 1))
            bs.achteck(netz, st, p - vorn * 0.12, 0.012, 0.0, 0.006, "Messing", n=6)


def verkleidungstruhe(netz, st, M):
    """Gekauft: die Verkleidungstruhe an der Rückwand – Truhe mit hochgeklapptem Deckel, aus ihr hängt eine Kutte, darauf ein Hut
    mit breiter Krempe, eine weiße Maske und ein falscher Bart; daneben ein Stab und eine Bettlerschale."""
    si.q(netz, st, M, (0, 0.3, 0.25), (0.9, 0.5, 0.5), "Holz alt", ohne=("-y",))
    for x in (-0.38, 0.38):
        si.q(netz, st, M, (x, 0.3, 0.25), (0.05, 0.52, 0.52), "Eisen", ohne=("-y",))
    si.q(netz, st, M, (0, 0.07, 0.75), (0.92, 0.04, 0.5), "Holz", ohne=("-y",))                  # Deckel hochgestellt
    n = M.to_3x3() @ Vector((0, 1, 0))
    flach(netz, st, [(-0.3, 0.53), (0.1, 0.53), (0.18, 0.2), (0.22, 0.02), (-0.36, 0.02), (-0.34, 0.25)],
          M @ Matrix.Translation((0, 0.55, 0)), 0.03, "Kutte")
    hut = M @ Vector((0.18, 0.28, 0.52))
    drehkoerper(netz, st, hut, [(0.0, 0.2), (0.015, 0.2), (0.015, 0.1), (0.13, 0.08), (0.14, 0.0)], "Hutfilz", n=10)
    maske = M @ Vector((-0.18, 0.35, 0.51))
    netz.quader(maske, (0.16, 0.2, 0.02), st["Maske"], Matrix.Rotation(0.4, 4, "Z"))
    for s in (-1, 1):
        netz.quader(maske + Vector((s * 0.04, 0, 0.011)), (0.035, 0.02, 0.002), st["Kohle"])
    netz.quader(M @ Vector((0.0, 0.38, 0.505)), (0.12, 0.06, 0.012), st["Haar"])                    # falscher Bart
    netz.stab(M @ Vector((0.52, 0.12, 0.0)), M @ Vector((0.56, 0.12, 1.5)), 0.04, 0.04, st["Holz"])  # Pilgerstab
    drehkoerper(netz, st, M @ Vector((-0.6, 0.25, 0)), [(0.0, 0.05), (0.06, 0.09), (0.06, 0.08), (0.01, 0.045)], "Holz alt", n=8,
                innen=False)


def faelscherpult(netz, st, M):
    """Ab Stufe 2: ein kleines Fälscherpult – schräge Platte mit Briefen, Siegeln, Siegelwachs und Feder, ein Hocker."""
    for x in (-0.28, 0.28):
        for y in (0.1, 0.5):
            netz.stab(M @ Vector((x, y, 0)), M @ Vector((x, y, 0.89 + 0.1 * (0.5 - y))), 0.05, 0.05, st["Holz"])
    platte = M @ Matrix.Translation((0, 0.32, 0.97)) @ Matrix.Rotation(math.radians(-14), 4, "X")
    netz.quader((0, 0, 0), (0.66, 0.5, 0.03), st["Holz alt"], platte)
    for k, (x, y) in enumerate(((-0.15, 0.02), (0.12, -0.06), (0.0, 0.1))):
        netz.quader((x, y, 0.017 + 0.003 * k), (0.2, 0.26, 0.002), st["Papier"], platte @ Matrix.Rotation(0.2 * (k - 1), 4, "Z"))
    for x in (0.22, 0.27):
        bs.achteck(netz, st, platte @ Vector((x, 0.15, 0.015)), 0.016, 0.0, 0.03, "Siegelwachs", n=6)
    netz.stab(platte @ Vector((-0.24, 0.12, 0.016)), platte @ Vector((-0.06, 0.18, 0.016)), 0.012, 0.012, st["Siegelwachs"])
    netz.stab(platte @ Vector((0.02, -0.1, 0.02)), platte @ Vector((0.18, 0.08, 0.16)), 0.012, 0.03, st["Leinen"])
    drehkoerper(netz, st, M @ Vector((0.1, 0.85, 0)), [(0.0, 0.16), (0.4, 0.14), (0.4, 0.17), (0.45, 0.17)], "Holz alt", n=8)


def verbrettert(netz, st):
    """Stufe 1 und 2: Die Öffnung zur Gildenhalle ist mit Brettern vernagelt – dunkle Rückseite, senkrechte Bretter, zwei quer."""
    o = Oe(GILDENTUER_X, 0.0, TUER_B, TUER_H)
    wand = ia.Wand(*ecken(WX0, WX1, WY0, WY1)["hinten"], [o])
    u = wand.ops[0][0]
    u0, u1 = u - o.b / 2, u + o.b / 2
    netz.vieleck_nach([wand.p(u0, 0, -0.12), wand.p(u1, 0, -0.12), wand.p(u1, o.h, -0.12), wand.p(u0, o.h, -0.12)], wand.n,
                      st["Bruchstein dunkel"])
    zufall = random.Random(4)
    x = u0
    while x < u1 - 0.02:
        b = min(zufall.uniform(0.17, 0.25), u1 - x)
        wq(netz, st, wand, x + 0.006, x + b - 0.006, 0.0, o.h - zufall.uniform(0.0, 0.1), -0.06, -0.035, zufall.choice(("Wandholz", "Wandholz dunkel")))
        x += b
    for v, neig in ((0.5, 0.25), (1.45, -0.2)):
        M = (Matrix.Translation(wand.p(u, v, -0.02)) @ Matrix((wand.d, wand.n, Vector((0, 0, 1)))).transposed().to_4x4()
             @ Matrix.Rotation(neig, 4, "Y"))
        netz.quader((0, 0, 0), (o.b + 0.25, 0.024, 0.14), st["Flickholz"], M, ohne=("-y",))


def luke_zu(netz, st):
    """Stufe 1: Die Luke zum Dachboden ist mit einer Klappe aus Brettern verschlossen (von unten: Leisten, Bänder, Ring)."""
    lx0, lx1, ly0, ly1 = LUKE_S
    z = H_WERK + 0.006
    netz.vieleck_nach([(lx0, ly0, z), (lx1, ly0, z), (lx1, ly1, z), (lx0, ly1, z)], (0, 0, -1), st["Diele dunkel"])
    for t in (0.25, 0.75):
        netz.quader((lx0 + (lx1 - lx0) * t, (ly0 + ly1) / 2, z - 0.015), (0.06, ly1 - ly0 - 0.1, 0.03), st["Holz"], ohne=("+z",))
    for y in (ly0 + 0.15, ly1 - 0.15):
        netz.quader(((lx0 + lx1) / 2 + 0.1, y, z - 0.004), (lx1 - lx0 - 0.3, 0.05, 0.008), st["Eisen"], ohne=("+z",))
    netz.stab(((lx0 + lx1) / 2 - 0.2, (ly0 + ly1) / 2, z - 0.02), ((lx0 + lx1) / 2 - 0.2, (ly0 + ly1) / 2, z - 0.09), 0.02, 0.02,
              st["Eisen"])


def banner(netz, st, M, breite=0.9, hoehe=1.5, z=2.3):
    """Banner der Gilde an einer Wand (örtlich: +y in den Raum): Nachtblau mit Zacken unten, darauf ein goldener Schlüssel und eine
    weiße Maske; an einer Stange."""
    umriss = [(-breite / 2, z), (breite / 2, z), (breite / 2, z - hoehe), (breite / 4, z - hoehe + 0.18), (0, z - hoehe),
              (-breite / 4, z - hoehe + 0.18), (-breite / 2, z - hoehe)]
    flach(netz, st, umriss, M @ Matrix.Translation((0, 0.03, 0)), 0.01, "Gildenblau", "Gildenblau dunkel")
    netz.stab(M @ Vector((-breite / 2 - 0.08, 0.04, z + 0.03)), M @ Vector((breite / 2 + 0.08, 0.04, z + 0.03)), 0.035, 0.035, st["Holz"])
    zeichen(netz, st, M @ Matrix.Translation((0, 0.04, z - hoehe * 0.42)) @ Matrix.Rotation(math.pi, 4, "Z"), 0.5)


def zeichen(netz, st, M, k=1.0):
    """Zeichen der Gilde (örtlich: Ebene x–z, nach −y gesehen): ein goldener Schlüssel schräg hinter einer weißen Maske."""
    n = M.to_3x3() @ Vector((0, -1, 0))
    gold = st["Gold"]
    d = Vector((0.6, 0, 0.8)).normalized()
    a, e = -d * 0.42 * k, d * 0.36 * k
    netz.stab(M @ (a + Vector((0, -0.006, 0))), M @ (e + Vector((0, -0.006, 0))), 0.05 * k, 0.012, gold,
              quer=M.to_3x3() @ Vector((0, 1, 0)))
    for j in range(10):                                                    # Ring des Schlüssels
        w0, w1 = 2 * math.pi * j / 10, 2 * math.pi * (j + 1) / 10
        c = e + d * 0.1 * k
        netz.stab(M @ (c + Vector((math.cos(w0), 0, math.sin(w0))) * 0.1 * k + Vector((0, -0.006, 0))),
                  M @ (c + Vector((math.cos(w1), 0, math.sin(w1))) * 0.1 * k + Vector((0, -0.006, 0))), 0.035 * k, 0.012, gold,
                  quer=M.to_3x3() @ Vector((0, 1, 0)))
    q = Vector((-d.z, 0, d.x))
    for t, l in ((0.0, 0.12), (0.08, 0.08)):                               # Bart
        p = a + d * t * k
        netz.stab(M @ (p + Vector((0, -0.006, 0))), M @ (p + q * l * k + Vector((0, -0.006, 0))), 0.04 * k, 0.012, gold,
                  quer=M.to_3x3() @ Vector((0, 1, 0)))
    maske = [(-0.2, 0.1), (-0.08, 0.14), (0.0, 0.1), (0.08, 0.14), (0.2, 0.1), (0.16, -0.06), (0.04, -0.1), (0.0, -0.04), (-0.04, -0.1),
             (-0.16, -0.06)]
    netz.vieleck_nach([M @ Vector((x * k, -0.02, z * k - 0.02 * k)) for x, z in maske], n, st["Maske"])
    for s in (-1, 1):
        netz.vieleck_nach([M @ Vector(((s * 0.09 + dx) * k, -0.022, (dz + 0.02) * k)) for dx, dz in
                           ((-0.04, -0.02), (0.04, -0.02), (0.04, 0.025), (-0.04, 0.025))], n, st["Kohle"])


# ─────────────────────────────────────────────────────────────────────────────
#  Beutekeller
# ─────────────────────────────────────────────────────────────────────────────

def keller_huelle(lager, decke, st):
    """Längswände aus Bruchstein bis zum Kämpfer, Lehmboden mit dem Loch der Falltür zum Fluchttunnel, Stirnwände und
    Tonnengewölbe wie im Fasskeller des Wirts (der Bogen der Treppe in der linken Stirnwand)."""
    kanten = ecken(CX0, CX1, CY0, CY1)

    def waende(hilf):
        for seite in ("vorne", "hinten"):
            si.wand(hilf, st, *kanten[seite], KAEMPFER, stoff="Bruchstein", dicke=WAND, aussen=False, fugen=True,
                    saat=61 if seite == "vorne" else 62)
        for r in ia.rechteck_minus((CX0, CX1, CY0, CY1), TUNNEL):
            ia.lehmboden(hilf, st, *r, saat=int(r[0] * 10 + 40), flecken=3, halme=3)
    wi.versetzt(lager, st, waende, Z_KELLER)
    with wirtskeller():
        wi.stirnwand(lager, st, CX0, 1, loch=KELLERTUER)
        wi.stirnwand(lager, st, CX1, -1)
        wi.gewoelbe(decke, st, saat=43)


def fluchttunnel(netz, st):
    """Die Falltür zum Fluchttunnel im Kellerboden: ein Kranz aus Bohlen, die Klappe hochgestellt, darunter ein Schacht aus
    Bruchstein mit einer Leiter, unten wird es schwarz – dort führt der Gang unter der Gasse weg (Kulisse)."""
    x0, x1, y0, y1 = TUNNEL
    z = Z_KELLER
    tief = 2.2
    alle, netz.alle = netz.alle, True
    for (a, b, n) in (((x0, y0), (x1, y0), (0, 1, 0)), ((x1, y0), (x1, y1), (-1, 0, 0)), ((x1, y1), (x0, y1), (0, -1, 0)),
                      ((x0, y1), (x0, y0), (1, 0, 0))):
        netz.vieleck_nach([(a[0], a[1], z - tief), (b[0], b[1], z - tief), (b[0], b[1], z), (a[0], a[1], z)], n,
                          st["Bruchstein dunkel"])
    netz.vieleck_nach([(x0, y0, z - tief), (x1, y0, z - tief), (x1, y1, z - tief), (x0, y1, z - tief)], (0, 0, 1), st["Loch"])
    netz.vieleck_nach([(x1 - 0.002, y0 + 0.1, z - tief), (x1 - 0.002, y1 - 0.1, z - tief), (x1 - 0.002, y1 - 0.1, z - tief + 1.1),
                       (x1 - 0.002, y0 + 0.1, z - tief + 1.1)], (-1, 0, 0), st["Loch"])           # der Gang unter der Gasse
    k, h = 0.07, 0.04
    netz.quader(((x0 + x1) / 2, y0 - k / 2, z + h / 2), (x1 - x0 + 2 * k, k, h), st["Fachwerk"], ohne=("-z",))
    netz.quader(((x0 + x1) / 2, y1 + k / 2, z + h / 2), (x1 - x0 + 2 * k, k, h), st["Fachwerk"], ohne=("-z",))
    for x in (x0 - k / 2, x1 + k / 2):
        netz.quader((x, (y0 + y1) / 2, z + h / 2), (k, y1 - y0, h), st["Fachwerk"], ohne=("-z", "+y", "-y"))
    lang, d = y1 - y0 - 0.02, 0.05                                         # Klappe an der linken Kante hochgestellt
    x = x0 - k - d / 2 - 0.005
    netz.quader((x, (y0 + y1) / 2, z + h + lang * 0.31), (d, lang, lang * 0.62), st["Diele"])
    for t in (0.25, 0.75):
        netz.quader((x - d / 2 - 0.006, y0 + lang * t, z + h + lang * 0.31), (0.012, 0.05, lang * 0.58), st["Eisen"], ohne=("+x",))
    netz.quader((x - d / 2 - 0.02, (y0 + y1) / 2, z + h + lang * 0.5), (0.04, 0.07, 0.07), st["Eisen"], ohne=("+x",))
    lx = (x0 + x1) / 2                                                     # Leiter an der Rückseite des Schachts
    for s in (-1, 1):
        netz.stab((lx + s * 0.18, y1 - 0.06, z - tief), (lx + s * 0.18, y1 - 0.04, z + 0.45), 0.06, 0.05, st["Holz alt"])
    for k_ in range(8):
        zz = z - tief + 0.3 + 0.3 * k_
        netz.stab((lx - 0.18, y1 - 0.06, zz), (lx + 0.18, y1 - 0.06, zz), 0.035, 0.035, st["Holz alt"])
    netz.alle = alle


def diebesgut(netz, st):
    """Bedienung_Lager_Beute: das Diebesgut an der Rückwand – Kisten, eine offene Truhe mit Silbergeschirr, Kerzenleuchter, eine
    Teppichrolle, Bilderrahmen an die Wand gelehnt, Säcke, ein Fass; dazu Hehlerware in Kisten mit Kreidezeichen."""
    z = Z_KELLER
    y = CY1 - 0.35
    si.kisten(netz, st, Vector((-1.2, y - 0.05, z)))
    for k, x in enumerate((0.0, 0.62)):                                    # Bilderrahmen, an die Wand gelehnt
        M = Matrix.Translation((x, CY1 - 0.12 - 0.06 * k, z)) @ Matrix.Rotation(math.radians(-12 + 3 * k), 4, "X")
        b, h = (0.7, 0.9) if k == 0 else (0.5, 0.62)
        for (u0, u1, v0, v1) in ((-b / 2, b / 2, 0.0, 0.07), (-b / 2, b / 2, h - 0.07, h), (-b / 2, -b / 2 + 0.07, 0.07, h - 0.07),
                                 (b / 2 - 0.07, b / 2, 0.07, h - 0.07)):
            netz.quader(((u0 + u1) / 2, 0, (v0 + v1) / 2), (u1 - u0, 0.04, v1 - v0), st["Rahmengold"], M)
        netz.quader((0, 0.01, h / 2), (b - 0.14, 0.01, h - 0.14), st["Gemaelde" if k == 0 else "Gemaelde hell"], M)
        if k == 0:
            netz.quader((0.05, -0.002, h * 0.55), (0.2, 0.004, 0.26), st["Gemaelde hell"], M)
    rolle = Vector((1.35, y + 0.05, z + 0.17))                             # Teppichrolle
    profil = [(0.17 * math.cos(2 * math.pi * k / 10), 0.17 * math.sin(2 * math.pi * k / 10)) for k in range(10)]
    netz.prisma(profil, -0.9, 0.9, st["Teppich"], matrix=Matrix.Translation(rolle) @ Matrix.Rotation(0.15, 4, "Z"),
                m_enden=st["Teppich hell"])
    truhe = Vector((2.55, y - 0.02, z))                                    # offene Truhe mit Silber
    netz.quader(truhe + Vector((0, 0, 0.24)), (0.8, 0.46, 0.48), st["Holz alt"])
    netz.quader(truhe + Vector((0, 0.24, 0.62)), (0.82, 0.04, 0.46), st["Holz"], Matrix.Rotation(0, 4, "Z"))
    for x in (-0.33, 0.33):
        netz.quader(truhe + Vector((x, 0, 0.24)), (0.05, 0.47, 0.49), st["Eisen"])
    netz.quader(truhe + Vector((0, 0, 0.485)), (0.68, 0.36, 0.01), st["Silbermuenze"], ohne=("-z",))
    for k, (dx, dy) in enumerate(((-0.2, 0.05), (0.05, -0.08), (0.22, 0.1))):
        drehkoerper(netz, st, truhe + Vector((dx, dy, 0.48)), [(0.0, 0.04), (0.06, 0.03), (0.1, 0.05), (0.14, 0.06)], "Silbermuenze", n=8)
    for k, x in enumerate((3.35, 3.6)):                                    # Kerzenleuchter aus Messing
        p = Vector((x, CY1 - 0.25, z))
        drehkoerper(netz, st, p, [(0.0, 0.1), (0.03, 0.08), (0.05, 0.025), (0.5 + 0.1 * k, 0.02), (0.52 + 0.1 * k, 0.05),
                                  (0.55 + 0.1 * k, 0.05)], "Messing", n=8)
        for s in (-1, 1):
            arm = p + Vector((s * 0.16, 0, 0.45 + 0.1 * k))
            netz.stab(p + Vector((0, 0, 0.35 + 0.1 * k)), arm, 0.015, 0.015, st["Messing"])
            drehkoerper(netz, st, arm, [(0.0, 0.035), (0.03, 0.03)], "Messing", n=6)
    for k, (x, dy) in enumerate(((-0.35, 0.0), (-0.05, -0.12), (0.85, 0.02))):   # Säcke
        drehkoerper(netz, st, Vector((x, y + dy, z)), [(0.0, 0.2), (0.25, 0.26), (0.5, 0.22), (0.6, 0.1), (0.68, 0.05)], "Sack", n=8)
    bs.fass(netz, st, Vector((-1.95, CY1 - 0.45, z)), r=0.3, h=0.85, reifen=(0.12, 0.66))
    bs.rund_oben(netz, st, Vector((-1.95, CY1 - 0.45)), 0.28, z + 0.851, "Holz alt")


def hehlerregale(netz, st):
    """Gekauft: die Hehlerregale an der vorderen Längswand – drei niedrige Regale (unter dem Gewölbe) mit Ware, Bündeln und Fässchen."""
    for k, x in enumerate((-0.6, 1.1, 2.8)):
        si.regal(netz, st, si.rahmen(x, CY0, 0, Z_KELLER), breite=1.5, hoehe=1.15, tiefe=0.4, boeden=3, saat=71 + k)


def keller_kram(netz, st, anker):
    """Der Tisch des Hehlers an der rechten Stirnwand (längs, vor den Regalen frei) mit Kassenbuch, Waage, Beuteln, Münzen und Kerze,
    davor ein Schemel."""
    z = Z_KELLER
    m = Vector((3.5, -2.3, z))
    wi.tisch(netz, st, m, 1.2, 0.6, hoehe=0.76, quer_x=False)
    for s in (-1, 1):
        netz.quader(m + Vector((0.0, s * 0.11, 0.77)), (0.28, 0.2, 0.02), st["Leder"] if s < 0 else st["Papier"])
    for k in range(6):                                                     # Zeilen im Kassenbuch
        netz.quader(m + Vector((-0.1 + 0.04 * k, 0.11, 0.781)), (0.006, 0.14, 0.002), st["Plantinte"])
    w = m + Vector((0.05, -0.42, 0.76))
    drehkoerper(netz, st, w, [(0.0, 0.05), (0.02, 0.04), (0.02, 0.01), (0.22, 0.008)], "Messing", n=6)
    netz.stab(w + Vector((-0.12, 0, 0.22)), w + Vector((0.12, 0, 0.22)), 0.01, 0.01, st["Messing"])
    for s in (-1, 1):
        drehkoerper(netz, st, w + Vector((s * 0.11, 0, 0.1)), [(0.0, 0.0), (0.0, 0.035), (0.015, 0.045)], "Messing", n=6)
        netz.stab(w + Vector((s * 0.11, 0, 0.115)), w + Vector((s * 0.11, 0, 0.22)), 0.004, 0.004, st["Messing"])
    beutel(netz, st, m + Vector((0.15, -0.1, 0.76)), r=0.05, h=0.08)
    beutel(netz, st, m + Vector((0.2, 0.0, 0.76)), r=0.04, h=0.06, zu=False)
    muenzen(netz, st, m + Vector((-0.12, -0.15, 0.76)), r=0.05, h=0.02, saat=8)
    kerze(netz, st, m + Vector((0.18, 0.5, 0.76)), 0.1, anker=anker)
    drehkoerper(netz, st, m + Vector((-0.6, 0.0, 0)), [(0.0, 0.14), (0.42, 0.12), (0.42, 0.17), (0.46, 0.17)], "Holz alt", n=8)
    scheibe(netz, st, m + Vector((-0.6, 0.0, 0)), 0.17, z + 0.46, "Holz alt", n=8)


# ─────────────────────────────────────────────────────────────────────────────
#  Schlafboden
# ─────────────────────────────────────────────────────────────────────────────

SCHLAF_X = (-4.15, -2.95, -1.75, -0.55)                                    # Strohsäcke an beiden Kniestöcken (Mitte x)
SCHLAF_HOEHE = 0.14                                                        # Oberkante des Strohsacks


def strohsack(netz, st, mitte, kopf, saat=0):
    """Ein Strohlager: Strohsack (0,85 × 1,95 m) mit dem Kopfende zum Kniestock (`kopf` = ±1 in y), ein Strohkissen, eine Decke über
    dem Fußende, ein paar Halme daneben."""
    zufall = random.Random(saat)
    z = Z_SCHLAF
    l, b, h = 1.95, 0.85, SCHLAF_HOEHE
    netz.quader(mitte + Vector((0, 0, z + h / 2)), (b, l, h), st["Strohsack"], ohne=("-z",))
    for s in (-1, 1):                                                      # Wülste an den Längsseiten
        netz.stab(mitte + Vector((s * (b / 2 - 0.03), -l / 2 + 0.05, z + h - 0.01)), mitte + Vector((s * (b / 2 - 0.03), l / 2 - 0.05, z + h - 0.01)),
                  0.06, 0.04, st["Strohsack"], quer=(1, 0, 0))
    kissen = mitte + Vector((0, kopf * (l / 2 - 0.2), z + h))
    netz.quader(kissen + Vector((0, 0, 0.03)), (0.5, 0.28, 0.06), st["Strohsack"])
    decke = zufall.choice(("Decke grob", "Decke rot"))
    netz.quader(mitte + Vector((0.02, -kopf * 0.45, z + h + 0.008)), (b + 0.06, 0.9, 0.016), st[decke])
    netz.quader(mitte + Vector((0.02, -kopf * 0.92, z + h - 0.03)), (b + 0.06, 0.016, 0.08), st[decke])
    for _ in range(8):
        c = mitte + Vector((zufall.uniform(-0.7, 0.7), zufall.uniform(-1.0, 1.0), z + 0.003))
        if abs(c.x - mitte.x) < b / 2 + 0.05 and abs(c.y - mitte.y) < l / 2 + 0.05:
            continue
        w = zufall.uniform(0, math.pi)
        d = Vector((math.cos(w), math.sin(w), 0)) * 0.08
        q = Vector((-d.y, d.x, 0)).normalized() * 0.005
        netz.vieleck_nach([c - d - q, c + d - q, c + d + q, c - d + q], (0, 0, 1), st["Strohhalm"])


def verkleidungen(netz, st):
    """Eine Leine am rechten Giebel entlang (von vorn nach hinten) mit Verkleidungen auf Bügeln: Mönchskutte mit Kapuze und Strick,
    Bettlerlumpen, Händlermantel mit Pelzkragen, Waffenrock der Stadtwache, Pilgermantel mit Hut; darunter ein Paar Stiefel."""
    z = Z_SCHLAF + 1.95
    x, y0, y1 = 4.25, -2.3, 1.3
    netz.stab((x, y0, z), (x, y1, z), 0.015, 0.015, st["Seil"])
    for y in (y0, y1):
        netz.stab((x, y, z), (x, y, min(DACH.dach_z(y) - 0.08, Z_SCHLAF + DACH.kehle)), 0.012, 0.012, st["Seil"])
    stuecke = (("Kutte", "Kutte", 1.2, 0.5), ("Bettlerlumpen", "Lappen dunkel", 0.95, 0.44), ("Haendlermantel", "Pelz", 1.1, 0.52),
               ("Stadtrot", "Stadtweiss", 0.85, 0.46), ("Pilgergrau", "Leder", 1.15, 0.5))
    y = y0 + 0.4
    for k, (stoff, rand, lang, breit) in enumerate(stuecke):
        M = Matrix.Translation((x, y, z - 0.06)) @ Matrix.Rotation(math.radians(90) + 0.12 * (k % 2 * 2 - 1), 4, "Z")
        netz.stab(M @ Vector((-breit / 2 - 0.04, 0, 0.0)), M @ Vector((breit / 2 + 0.04, 0, 0.0)), 0.025, 0.02, st["Holz"])
        netz.stab(M @ Vector((0, 0, 0.0)), M @ Vector((0, 0, 0.07)), 0.008, 0.008, st["Eisen"])
        s = breit / 2
        if stoff == "Bettlerlumpen":
            umriss = [(-s, 0), (s, 0), (s + 0.05, -0.3), (s - 0.02, -lang + 0.1), (s * 0.4, -lang), (0, -lang + 0.12), (-s * 0.5, -lang + 0.02),
                      (-s + 0.03, -lang + 0.16), (-s - 0.05, -0.3)]
        else:
            umriss = [(-s, 0), (s, 0), (s + 0.18, -0.12), (s + 0.12, -0.5), (s, -0.36), (s + 0.06, -lang), (-s - 0.06, -lang), (-s, -0.36),
                      (-s - 0.12, -0.5), (-s - 0.18, -0.12)]
        flach(netz, st, umriss, M, 0.05, stoff)
        if stoff in ("Haendlermantel", "Pilgergrau"):                       # Kragen
            netz.quader((0, 0, 0), (breit + 0.06, 0.07, 0.1), st[rand], M @ Matrix.Translation((0, 0, -0.03)))
        if stoff == "Stadtrot":                                             # weißer Balken des Wappenrocks
            netz.quader((0, 0, 0), (0.16, 0.054, lang * 0.75), st[rand], M @ Matrix.Translation((0, 0, -lang * 0.45)))
        if stoff == "Kutte":                                                # Kapuze und Strick
            drehkoerper(netz, st, M @ Vector((0, 0.03, -0.22)), [(0.0, 0.1), (0.18, 0.08), (0.24, 0.0)], "Kutte", n=6)
            netz.stab(M @ Vector((-s - 0.02, -0.03, -0.55)), M @ Vector((s + 0.02, -0.03, -0.55)), 0.02, 0.02, st["Seil"])
        if stoff == "Pilgergrau":                                           # Pilgerhut
            drehkoerper(netz, st, M @ Vector((0, 0.0, 0.08)), [(0.0, 0.2), (0.015, 0.2), (0.015, 0.09), (0.1, 0.08), (0.11, 0.0)],
                        "Pilgergrau", n=8)
        y += breit + 0.22
    for dy in (0.0, 0.16):                                                  # Stiefel
        p = Vector((x - 0.35, -0.5 + dy, Z_SCHLAF))
        netz.quader(p + Vector((0.04, 0, 0.2)), (0.12, 0.1, 0.4), st["Leder"])
        netz.quader(p + Vector((-0.06, 0, 0.05)), (0.14, 0.1, 0.1), st["Leder"])


def schminktisch(netz, st, anker):
    """Ein Tischchen mit Spiegel (Zinn, blank), Schminktiegeln, einer Perücke auf einem Holzkopf und einer Kerze."""
    m = Vector((3.4, -2.4, Z_SCHLAF))
    netz.quader(m + Vector((0, 0, 0.7)), (0.8, 0.45, 0.04), st["Holz alt"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader(m + Vector((sx * 0.35, sy * 0.18, 0.34)), (0.05, 0.05, 0.68), st["Holz"])
    spiegel = m + Vector((0, -0.18, 0.72))
    netz.quader(spiegel + Vector((0, 0, 0.26)), (0.42, 0.04, 0.52), st["Holz"])
    netz.quader(spiegel + Vector((0, 0.022, 0.26)), (0.34, 0.004, 0.44), st["Zinn"])
    for k, x in enumerate((-0.28, -0.18, 0.2)):
        drehkoerper(netz, st, m + Vector((x, 0.08, 0.72)), [(0.0, 0.035), (0.04, 0.035), (0.04, 0.0)], ("Papier", "Lappen rot", "Kohle")[k],
                    n=6)
    kopf = m + Vector((0.08, 0.1, 0.72))
    drehkoerper(netz, st, kopf, [(0.0, 0.05), (0.08, 0.04), (0.1, 0.08), (0.2, 0.09), (0.27, 0.06), (0.3, 0.0)], "Holz alt", n=8)
    drehkoerper(netz, st, kopf + Vector((0, 0.01, 0.17)), [(0.0, 0.11), (0.08, 0.1), (0.15, 0.0)], "Haar", n=8)
    kerze(netz, st, m + Vector((-0.3, -0.1, 0.72)), 0.1, anker=anker, stufe=2)


def schlafboden(netz, decke, st, anker, plaetze):
    """Raum_Schlafboden (ab Stufe 2): Kniestöcke und Giebel aus Brettern, Dach mit Sparren und Kehlbalken (innenausbau.Dachboden),
    ein verhängtes Fenster im linken Giebel; acht Strohsäcke, Verkleidungen an der Leine, Truhe, Schminktisch, Laterne."""
    z = Z_SCHLAF
    DACH.kniestoecke(netz, st, "Bretter")
    gfenster = Oe(0.0, 1.3, 0.6, 0.6)
    DACH.giebel(netz, st, False, [gfenster], "Bretter", WAND)
    DACH.giebel(netz, st, True, [], "Bretter", WAND)
    DACH.dach(decke, st, saat=5)
    kanten = ecken(WX0, WX1, WY0, WY1)
    o = Oe(gfenster.s, z + gfenster.v, gfenster.b, gfenster.h)
    ti.fenster(netz, st, *kanten["links"], o)
    lappen(netz, st, ia.Wand(*kanten["links"], [o]), o, saat=8)
    anker.append(licht(Vector((WX0 + 0.1, gfenster.s, z + gfenster.v + gfenster.h / 2)), (1, 0, 0), "Fenster", 2))
    reihen = ((WY0 + 0.1 + 1.95 / 2, -1), (WY1 - 0.1 - 1.95 / 2, 1))       # vorn: Kopf zur Vorderwand; hinten: zur Rückwand
    hals = fg.VOELKER["mensch"]["schulter"] - fg.VOELKER["mensch"]["huefte"] + 0.31   # Hüfte → Scheitel (Mensch)
    platz = 1
    for ym, kopf in reihen:
        for i in (2, 3, 1, 0):
            mitte = Vector((SCHLAF_X[i], ym, 0))
            strohsack(netz, st, mitte, kopf, saat=platz)
            huefte = Vector((mitte.x, mitte.y + kopf * (1.95 / 2 - 0.12 - hals), 0))
            dieb(plaetze, huefte, (0, -kopf), "Schlafen", "Schlafen", platz, z=z + SCHLAF_HOEHE, stufe=2)
            platz += 1
    truhe = Vector((-4.55, 0.0, z))                                         # Truhe am linken Giebel
    netz.quader(truhe + Vector((0, 0, 0.25)), (0.5, 0.9, 0.5), st["Holz alt"])
    netz.quader(truhe + Vector((0, 0, 0.52)), (0.52, 0.92, 0.05), st["Holz"])
    for y in (-0.38, 0.38):
        netz.quader(truhe + Vector((0, y, 0.27)), (0.52, 0.05, 0.52), st["Eisen"])
    verkleidungen(netz, st)
    schminktisch(netz, st, anker)
    p = Vector((-1.2, 0.0, z + 2.1))
    ti.laterne(netz, st, p, z + DACH.kehle)
    anker.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe", 2))


# ─────────────────────────────────────────────────────────────────────────────
#  Gildenhalle
# ─────────────────────────────────────────────────────────────────────────────

def gilde_huelle(netz, decke, st, anker):
    """Wände aus Bruchstein mit Fugen (die Vorderwand ist die Rückwand der Stube), ein vergittertes Fenster hoch in der rechten Wand,
    Balkendecke, Backsteinpflaster im Fischgrätmuster."""
    G = GILDE_OEFFNUNGEN
    kanten = ecken(GX0, GX1, GY0, GY1)
    netz.alle = True
    for seite in ("vorne", "rechts", "hinten", "links"):
        si.wand(netz, st, *kanten[seite], H_GILDE, G.get(seite, []), stoff="Bruchstein dunkel", dicke=0.5, fugen=True, saat=81 + len(seite),
                aussen=seite != "vorne", laibung=seite != "vorne", enden=(seite != "links", seite != "rechts"))
    netz.alle = False
    for o in G["rechts"]:
        si.fenster(netz, st, *kanten["rechts"], o, dicke=0.5)
        anker.append(licht(Vector((GX1 - 0.1, o.s, o.v + o.h / 2)), (-1, 0, 0), "Fenster", 3))
    ia.balkendecke(decke, st, GX0, GX1, GY0, GY1, H_GILDE, abdeckung=0.5, saat=19)
    ia.fischgraet(netz, st, GX0, GX1, GY0, GY1, saat=23)


def thron(netz, st, anker):
    """Der Thron des Meisterdiebs auf einem Podest mit zwei Stufen an der Rückwand: hohe Lehne mit Schnitzwerk und nachtblauem
    Polster, Armlehnen mit Knäufen; Meister_1 sitzt darauf (Fußpunkt auf dem Podest, Blick in die Halle). Darüber das Wappen der
    Gilde, links und rechts Fackeln."""
    t = THRON
    netz.quader(Vector((t.x, GY1 - 0.9, PODEST / 2)), (2.4, 1.8, PODEST), st["Holz alt"], ohne=("-z",))
    netz.quader(Vector((t.x, GY1 - 1.95, PODEST / 4)), (1.6, 0.3, PODEST / 2), st["Holz"], ohne=("-z",))
    z = PODEST
    sitz = t + Vector((0, -0.02, z))
    netz.quader(sitz + Vector((0, 0, SITZ - 0.04)), (0.62, 0.55, 0.08), st["Thronholz"])
    netz.quader(sitz + Vector((0, -0.02, SITZ + 0.015)), (0.54, 0.47, 0.03), st["Gildenblau"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader(sitz + Vector((sx * 0.28, sy * 0.24, (SITZ - 0.08) / 2)), (0.07, 0.07, SITZ - 0.08), st["Thronholz"])
        netz.quader(sitz + Vector((sx * 0.31, 0.0, SITZ + 0.2)), (0.06, 0.52, 0.05), st["Thronholz"])           # Armlehne
        netz.quader(sitz + Vector((sx * 0.31, -0.24, SITZ + 0.1)), (0.06, 0.06, 0.2), st["Thronholz"])
        bs.achteck(netz, st, sitz + Vector((sx * 0.31, -0.25, SITZ + 0.225)), 0.045, 0.0, 0.06, "Gold", n=6)
    lehne = sitz + Vector((0, 0.27, 0))
    netz.quader(lehne + Vector((0, 0, 0.95)), (0.66, 0.07, 1.9), st["Thronholz"])
    netz.quader(lehne + Vector((0, -0.04, 0.85)), (0.5, 0.01, 0.75), st["Gildenblau"])
    for k in range(5):                                                     # Zinnen oben
        netz.quader(lehne + Vector((-0.28 + 0.14 * k, 0, 1.95 + 0.05 * (k % 2))), (0.08, 0.07, 0.12 + 0.1 * (k % 2)), st["Thronholz"])
    zeichen(netz, st, Matrix.Translation(lehne + Vector((0, -0.05, 1.45))) @ Matrix.Rotation(0, 4, "Z"), 0.35)
    meister = bs.anker("Meister_1", (sitz.x, sitz.y - 0.24, z), si.drehung_von((0, -1)))
    meister["taetigkeit"], meister["bewegung"], meister["sitzhoehe"], meister["stufe"] = "Thronen", "Sitzen", SITZ, 3
    anker.append(meister)
    # Wappen der Gilde über dem Thron
    M = si.rahmen(t.x, GY1, 180)
    flach(netz, st, [(-0.55, 3.25), (0.55, 3.25), (0.55, 2.6), (0.3, 2.3), (0.0, 2.18), (-0.3, 2.3), (-0.55, 2.6)],
          M @ Matrix.Translation((0, 0.04, 0)), 0.06, "Gildenblau", "Gold")
    zeichen(netz, st, M @ Matrix.Translation((0, 0.075, 2.72)) @ Matrix.Rotation(math.pi, 4, "Z"), 0.75)
    for sx in (-1, 1):
        fackel(netz, st, si.rahmen(t.x + sx * 1.55, GY1, 180), anker)


def fackel(netz, st, M, anker, z=1.85):
    """Fackel in einem eisernen Halter an der Wand (örtlich: +y in den Raum), Flamme „Glut“ mit Licht."""
    e = st["Eisen"]
    netz.stab(M @ Vector((0, 0.0, z - 0.1)), M @ Vector((0, 0.16, z - 0.05)), 0.03, 0.02, e)
    ring = M @ Vector((0, 0.16, z))
    drehkoerper(netz, st, ring - Vector((0, 0, 0.05)), [(0.0, 0.035), (0.08, 0.045)], "Eisen", n=6)
    fuss = M @ Vector((0, 0.13, z - 0.3))
    oben = M @ Vector((0, 0.19, z + 0.12))
    netz.stab(fuss, oben, 0.04, 0.04, st["Holz alt"])
    drehkoerper(netz, st, oben, [(0.0, 0.035), (0.06, 0.04)], "Kohle", n=6)
    drehkoerper(netz, st, oben + Vector((0, 0, 0.06)), [(0.0, 0.045), (0.08, 0.035), (0.2, 0.0)], "Glut", n=6)
    anker.append(licht(oben + Vector((0, 0, 0.2)) + (M.to_3x3() @ Vector((0, 0.1, 0))), (0, 0, 1), "Glut", 3))


def schwurtisch(netz, st, anker):
    """Der Schwurtisch in der Mitte: ein schwerer runder Tisch, ein Dolch steckt senkrecht darin, ein aufgeschlagenes Schwurbuch,
    drei Kerzen, ein Totenschädel als Kerzenhalter, Münzen; rundum Hocker."""
    m = Vector((GILDENTUER_X, GY0 + 2.6, 0))
    drehkoerper(netz, st, m, [(0.0, 0.35), (0.06, 0.3), (0.08, 0.1), (0.74, 0.1), (0.74, 0.62), (0.8, 0.62)], "Thronholz", n=14)
    z = 0.8
    scheibe(netz, st, m, 0.62, z, "Thronholz", n=14)
    dolch = m + Vector((0.12, 0.05, z))
    netz.stab(dolch, dolch + Vector((0, 0, 0.18)), 0.03, 0.006, st["Eisen"], quer=(1, 0, 0))
    netz.quader(dolch + Vector((0, 0, 0.185)), (0.12, 0.02, 0.018), st["Gold"])
    netz.stab(dolch + Vector((0, 0, 0.19)), dolch + Vector((0, 0, 0.3)), 0.028, 0.028, st["Leder"])
    bs.achteck(netz, st, dolch + Vector((0, 0, 0.3)), 0.02, 0.0, 0.025, "Gold", n=6)
    buch = m + Vector((-0.18, -0.12, z))
    for s in (-1, 1):
        netz.quader(buch + Vector((s * 0.12, 0, 0.015)), (0.23, 0.32, 0.03), st["Leder"])
        netz.quader(buch + Vector((s * 0.115, 0, 0.034)), (0.21, 0.29, 0.01), st["Papier"])
    for k in range(3):
        w = 2 * math.pi * k / 3 + 0.5
        kerze(netz, st, m + Vector((0.45 * math.cos(w), 0.45 * math.sin(w), z)), 0.16 + 0.04 * k, anker=anker if k == 0 else None, stufe=3)
    schaedel = m + Vector((0.32, -0.32, z))
    drehkoerper(netz, st, schaedel, [(0.0, 0.05), (0.04, 0.07), (0.1, 0.075), (0.14, 0.05), (0.16, 0.0)], "Maske", n=8)
    for s in (-1, 1):
        netz.quader(schaedel + Vector((s * 0.028, -0.065, 0.08)), (0.028, 0.012, 0.03), st["Kohle"])
    kerze(netz, st, schaedel + Vector((0, 0, 0.16)), 0.06, r=0.015)
    muenzen(netz, st, m + Vector((-0.25, 0.3, z)), r=0.05, h=0.02, saat=3)
    for k in range(6):
        w = 2 * math.pi * k / 6 + 0.25
        hocker = m + Vector((1.05 * math.cos(w), 1.05 * math.sin(w), 0))
        drehkoerper(netz, st, hocker, [(0.0, 0.14), (0.42, 0.12), (0.42, 0.17), (0.46, 0.17)], "Holz alt", n=8)
        scheibe(netz, st, hocker, 0.17, 0.46, "Holz alt", n=8)


def gilde_kram(netz, st, anker):
    """Bänke an den Seitenwänden, Banner der Gilde, zwei Fackeln vorn, ein Stuhl mit Stricken für Geiseln in der Ecke vorn links,
    ein Fass und eine Kiste."""
    for x, d in ((GX0 + 0.35, 1), (GX1 - 0.35, -1)):
        wi.bank(netz, st, (x, GY0 + 1.6, 0), (x, GY0 + 4.4, 0), tiefe=0.36)
        banner(netz, st, si.rahmen(GX0 if d > 0 else GX1, GY0 + 3.0, -90 if d > 0 else 90), z=2.9)
        fackel(netz, st, si.rahmen(GX0 if d > 0 else GX1, GY0 + 1.2, -90 if d > 0 else 90), anker)
    stuhl = Vector((GX0 + 0.7, GY0 + 0.75, 0))                             # Geiselstuhl
    netz.quader(stuhl + Vector((0, 0, 0.44)), (0.46, 0.44, 0.05), st["Holz alt"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader(stuhl + Vector((sx * 0.2, sy * 0.19, 0.21)), (0.05, 0.05, 0.42), st["Holz"])
        netz.quader(stuhl + Vector((sx * 0.2, 0.19, 0.75)), (0.05, 0.05, 0.6), st["Holz"])
    netz.quader(stuhl + Vector((0, 0.19, 0.95)), (0.46, 0.04, 0.12), st["Holz"])
    for z in (0.3, 0.6):
        for sx in (-1, 1):
            drehkoerper(netz, st, stuhl + Vector((sx * 0.2, 0.19, z)), [(0.0, 0.035), (0.05, 0.035)], "Seil", n=6)
    netz.stab(stuhl + Vector((-0.18, 0.0, 0.47)), stuhl + Vector((0.3, -0.4, 0.0)), 0.02, 0.02, st["Seil"])
    bs.fass(netz, st, Vector((GX1 - 0.5, GY0 + 0.6, 0)), r=0.3, h=0.85, reifen=(0.12, 0.66))
    si.kisten(netz, st, Vector((GX1 - 0.6, GY1 - 0.7, 0)))


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
    schlaf, gilde = bs.Netz("Raum_Schlafboden", alle=True), bs.Netz("Raum_Gildenhalle")
    decke_w, decke_l = bs.Netz("Decke_Werk", alle=True), bs.Netz("Decke_Lager", alle=True)
    decke_s, decke_g = bs.Netz("Decke_Schlafboden", alle=True), bs.Netz("Decke_Gildenhalle", alle=True)
    teile = {}                    # Name → (Netz, Raum)

    def teil(name, raum="Werk"):
        if name not in teile:
            teile[name] = (bs.Netz(name), raum)
        return teile[name][0]

    anker = []                    # (Objekt, Raum)
    fertig = []                   # schon gebaute Objekte (bewegte Teile): (Objekt, Raum)

    def setzen(o, raum="Werk", stufe=None):
        if stufe is not None:
            o["stufe"] = stufe
        anker.append((o, raum))

    def alle_setzen(liste, raum="Werk", stufe=None):
        for o in liste:
            setzen(o, raum, stufe)

    plaetze = []                  # Dieb_<n>, fortlaufend
    # ── Stube ─────────────────────────────────────────────────────────────────
    liste = []
    stube_huelle(werk, decke_w, schlaf, st, liste)
    uebungsbrett(werk, st, plaetze)
    kartentisch(teil("Bedienung_Auftraege_Karte"), st, plaetze, liste)
    waagbalken = zaehltisch(werk, st, plaetze, liste)
    stein, schleifer = schleifbock(werk, st)
    wetztisch(werk, st, plaetze, schleifer)
    fertig += [(stein, "Werk"), (waagbalken, "Werk")]
    alle_setzen(liste)
    p = Vector((ZAEHL.x - 0.4, ZAEHL.y, 2.05))                              # Laterne über dem Zähltisch
    ti.laterne(werk, st, p, H_WERK - 0.26)
    setzen(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"))
    si.pult(teil("Bedienung_Personal_Buch"), st, si.rahmen(-1.4, WY0 + 0.05, 0))
    M_haus = si.rahmen(HAUSTUER_X, WY0, 0)
    si.bretttuer(teil("Holztuer"), st, M_haus, HAUSTUER_B, HAUSTUER_H)
    si.eisentuer(teil("Einrichtung_Eisentuer"), st, M_haus, HAUSTUER_B, HAUSTUER_H)
    M_hof = si.rahmen(WX1, HOFTOR_Y, 90)
    ti.hoftor(teil("Bedienung_Karren_Hoftor"), st, M_hof, HOFTOR_B, HOFTOR_H)
    si.tuerstock(werk, st, M_hof, HOFTOR_B, HOFTOR_H)
    M_wz = si.rahmen(WX1 - 0.06, 0.55, 90)
    si.werkzeugwand(teil("Bedienung_Einrichten_Werkzeugwand"), st, M_wz)
    werkzeug_dieb(teil("Einrichtung_Werkzeug1"), st, M_wz, 1)
    werkzeug_dieb(teil("Einrichtung_Werkzeug2"), st, M_wz, 2)
    si.q(werk, st, M_wz, (0.42, 0.18, 1.0), (0.8, 0.36, 0.03), "Holz alt")       # Brett unter der Werkzeugwand
    verkleidungstruhe(teil("Einrichtung_Verkleidung"), st, si.rahmen(0.45, WY1, 180))
    si.loeschfass(teil("Einrichtung_Loeschfass"), st, Vector((-4.55, 1.55, 0)))
    wi.handschuhe(teil("Einrichtung_Handschuhe"), st, si.rahmen(-0.45, WY0, 0))
    with wirtskeller():
        wi.falltuer(teil("Tuer_Werk_Lager"), st)
    ia.treppe(teil("Tuer_Werk_Schlafboden"), st, LEITER_X, LEITER_FUSS, LUKE_S[2] + 0.05, Z_SCHLAF, breite=0.55, holme=0.0)
    si.bretttuer(teil("Tuer_Werk_Gildenhalle"), st, si.rahmen(GILDENTUER_X, WY1, 180), TUER_B, TUER_H, stoff="Brett")
    si.tuerstock(werk, st, si.rahmen(GILDENTUER_X, WY1, 180), TUER_B, TUER_H)
    si.tuerstock(werk, st, M_haus, HAUSTUER_B, HAUSTUER_H)

    # ── Stufen (kumulativ) ────────────────────────────────────────────────────
    stufen = [bs.Netz(f"Stufe{k}") for k in (1, 2, 3)]
    for k, netz in enumerate(stufen, 1):
        if k == 1:
            luke_zu(netz, st)
        if k < 3:
            verbrettert(netz, st)
        if k >= 2:
            faelscherpult(netz, st, si.rahmen(1.45, WY1, 180))
        if k == 3:
            banner(netz, st, si.rahmen(WX0, -0.9, -90), breite=0.8, hoehe=1.3, z=2.35)
    stufen_obj = [s.objekt() for s in stufen]

    # ── Beutekeller ───────────────────────────────────────────────────────────
    keller_huelle(lager, decke_l, st)
    with wirtskeller():
        wi.treppe_und_schacht(teil("Tuer_Lager_Werk", "Lager"), st, oben=False)
    diebesgut(teil("Bedienung_Lager_Beute", "Lager"), st)
    hehlerregale(teil("Einrichtung_Regale", "Lager"), st)
    fluchttunnel(lager, st)
    liste = []
    keller_kram(lager, st, liste)
    alle_setzen(liste, "Lager")
    with wirtskeller():
        for x in (-0.4, 2.0):
            p = Vector((x, (CY0 + CY1) / 2, Z_KELLER + 2.05))
            ti.laterne(lager, st, p, Z_KELLER + wi.gewoelbe_z(p.y) + 0.02)
            setzen(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"), "Lager")
    nummer = 1
    for y in (-1.1, -2.2):
        for x in (-1.1, -0.1, 0.9, 1.9):
            setzen(bs.anker(f"Lager_{nummer}", (x, y, Z_KELLER)), "Lager")
            nummer += 1

    # ── Schlafboden (ab Stufe 2) ──────────────────────────────────────────────
    liste = []
    schlafboden(schlaf, decke_s, st, liste, plaetze)
    alle_setzen(liste, "Schlafboden", stufe=2)
    ia.bodenluke(teil("Tuer_Schlafboden_Werk", "Schlafboden"), st, LUKE_S, Z_SCHLAF, gelaender_x=LUKE_S[0] - 0.05)

    # ── Gildenhalle (ab Stufe 3) ──────────────────────────────────────────────
    liste = []
    gilde_huelle(gilde, decke_g, st, liste)
    thron(gilde, st, liste)
    schwurtisch(gilde, st, liste)
    gilde_kram(gilde, st, liste)
    alle_setzen(liste, "Gildenhalle", stufe=3)
    si.bretttuer(teil("Tuer_Gildenhalle_Werk", "Gildenhalle"), st, si.rahmen(GILDENTUER_X, GY0, 0), TUER_B, TUER_H, stoff="Brett")
    si.tuerstock(teil("Tuer_Gildenhalle_Werk", "Gildenhalle"), st, si.rahmen(GILDENTUER_X, GY0, 0), TUER_B, TUER_H)
    for o in plaetze:
        raum = "Schlafboden" if o["taetigkeit"] == "Schlafen" else "Werk"
        setzen(o, raum)

    # ── Blickpunkte ──────────────────────────────────────────────────────────
    for name, (auge, ziel) in BLICKE.items():
        raum = name.split("_")[1]
        setzen(si.blick(name, auge, ziel), raum, stufe={"Schlafboden": 2, "Gildenhalle": 3}.get(raum))

    # ── Objekte, Zustände, Hierarchie ─────────────────────────────────────────
    raeume = {"Werk": werk.objekt(), "Lager": lager.objekt(), "Schlafboden": schlaf.objekt(), "Gildenhalle": gilde.objekt()}
    raeume["Schlafboden"]["stufe"] = 2
    raeume["Gildenhalle"]["stufe"] = 3
    kinder = {r: [] for r in raeume}
    kinder["Werk"] += [decke_w.objekt()] + stufen_obj
    kinder["Lager"].append(decke_l.objekt())
    kinder["Schlafboden"].append(decke_s.objekt())
    kinder["Gildenhalle"].append(decke_g.objekt())
    for o, raum in fertig:
        kinder[raum].append(o)
    for name, (netz, raum) in teile.items():
        o = netz.objekt()
        kinder[raum].append(o)
        if name.startswith("Einrichtung_"):
            bs.zustand(o)
        if name in ("Tuer_Werk_Schlafboden", "Tuer_Schlafboden_Werk"):
            o["stufe"] = 2
        if name in ("Tuer_Werk_Gildenhalle", "Tuer_Gildenhalle_Werk"):
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
    ti.animieren(stein_bewegung(stein) + waage_bewegung(waagbalken), ARBEIT_BILDER)
    bpy.context.view_layer.update()
    return list(raeume.values()) + [o for liste in kinder.values() for o in liste]


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def dieb_marken(z, raeume):
    """Marken für die Plätze der Diebe im Grundriss (Pfeil = Blickrichtung; Farbe je Tätigkeit) und den Meisterdieb (lila)."""
    import bpy
    farben = {"Ueben": (0.95, 0.25, 0.2), "Planen": (0.2, 0.45, 1.0), "Zaehlen": (0.95, 0.8, 0.15), "Schleifen": (0.3, 0.85, 0.3),
              "Schlafen": (0.75, 0.75, 0.9), "Thronen": (0.6, 0.3, 1.0)}
    stoffe = {k: bs._hilfsstoff("_Marke_" + k, f) for k, f in farben.items()}
    liste = []
    for o in list(bpy.context.scene.objects):
        if o.type != "EMPTY" or not o.name.startswith(("Dieb_", "Meister_")) or ia.raum_von(o) not in raeume:
            continue
        netz = bs.Netz("_Marke_" + o.name)
        t = o.matrix_world
        p = t.translation.copy()
        p.z = z
        vor = t.to_3x3() @ Vector((0, 1, 0))
        quer = Vector((-vor.y, vor.x, 0))
        netz.vieleck_nach([p - quer * 0.14 - vor * 0.1, p + vor * 0.3, p + quer * 0.14 - vor * 0.1], (0, 0, 1), stoffe[o["taetigkeit"]])
        liste.append(netz.objekt())
    return liste


def vorschau(schnell):
    """Bilder nach docs/modelle/: dieb_innen-blicke.png (jeder Blickpunkt in Stufe 3 mit aller Einrichtung), dieb_innen-stufen.png
    (die Stube von oben je Stufe) und dieb_innen-grundriss.png (Stube und Gildenhalle, Beutekeller, Schlafboden mit den Ankern:
    Plätze der Diebe je Tätigkeit – Üben rot, Planen blau, Zählen gelb, Schleifen grün, Schlafen hellgrau, Meisterdieb lila –,
    Stellplätze türkis, Blickpunkte gelb)."""
    import bpy
    szene, kam = ia.buehne(schnell, lampe=70, glut=200, fuell=[(0, 0, H_WERK - 0.4, 120, 1), ((CX0 + CX1) / 2, (CY0 + CY1) / 2,
                                                                  Z_KELLER + 2.0, 50, 1), (0, 0, Z_SCHLAF + 2.4, 140, 2),
                                                                 ((GX0 + GX1) / 2, (GY0 + GY1) / 2, H_GILDE - 0.4, 120, 3)])
    szene.render.resolution_x, szene.render.resolution_y = 640, 360
    szene.render.resolution_percentage = 50 if schnell else 100
    szene.frame_set(1 + round(0.15 * ARBEIT_BILDER))
    bilder = []
    ia.zeigen(szene, 3, True)
    for blick in BLICKE:
        o = bpy.data.objects[blick]
        kam.matrix_world = o.matrix_world @ Matrix.Rotation(math.radians(90), 4, "X")
        bilder.append(bs.rendern_einzeln(f"_{NAME}-{blick}", blick))
    print(bs.zusammensetzen(NAME + "-blicke", bilder, spalten=2))
    szene.frame_set(1)
    kam.data.type = "ORTHO"
    kam.data.sensor_fit = "AUTO"
    sonne = bpy.data.objects.new("_Sonne", bpy.data.lights.new("_Sonne", "SUN"))
    sonne.data.energy = 2.5
    sonne.rotation_euler = (math.radians(25), math.radians(-15), 0)
    szene.collection.objects.link(sonne)

    def von_oben(r, rand=0.8, oben=1.2):
        x0, x1, y0, y1 = r
        kam.data.ortho_scale = max(x1 - x0, y1 - y0 + oben) + 2 * rand
        kam.matrix_world = Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2 + oben / 2, 40.0))
        szene.render.resolution_x = szene.render.resolution_y = 760
    von_oben((WX0 - WAND, WX1 + WAND, WY0 - WAND, WY1 + WAND))
    teile = []
    for k in (1, 2, 3):
        ia.zeigen(szene, k, False, decke=False, raeume={"Werk"})
        teile.append(bs.rendern_einzeln(f"_{NAME}-stufe{k}", f"Stufe {k}: {STUFEN[k - 1]}"))
    print(bs.zusammensetzen(NAME + "-stufen", teile, spalten=3))
    teile = []
    for raeume, r, z, text in (({"Werk", "Gildenhalle"}, (WX0 - WAND, WX1 + WAND, WY0 - WAND, GY1 + 0.5), 5.0,
                                "Stube und Gildenhalle, Stufe 3: Üben rot, Planen blau, Zählen gelb, Schleifen grün, Meister lila"),
                               ({"Lager"}, (CX0 - 1.8, CX1 + WAND, CY0 - WAND, CY1 + WAND), Z_KELLER + 2.0,
                                "Beutekeller (ohne Gewölbe): Stellplätze türkis, Blickpunkte gelb"),
                               ({"Schlafboden"}, (WX0 - WAND, WX1 + WAND, WY0 - WAND, WY1 + WAND), Z_SCHLAF + 2.6,
                                "Schlafboden (Stufe 2, ohne Dach): Schlafplätze hellgrau")):
        von_oben(r)
        liste = ia.marken(z, raeume) + dieb_marken(z, raeume)
        ia.zeigen(szene, 3, True, decke=False, raeume=raeume)
        teile.append(bs.rendern_einzeln(f"_{NAME}-{'-'.join(sorted(raeume))}", text))
        for o in liste:
            bpy.data.objects.remove(o)
    print(bs.zusammensetzen(NAME + "-grundriss", teile, spalten=3))
    ai.verkleinern(NAME + "-blicke", 0.85)


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
