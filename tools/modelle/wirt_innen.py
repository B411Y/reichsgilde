"""Innenraum des Wirts: Schankstube (Werkraum), daneben die Küche mit Braukessel und Herd (zweiter Werkraum), darunter der
Fasskeller (Lager) und ab der Stufe Gasthaus der Saal im Obergeschoss; Stufen Schänke, Wirtshaus, Gasthaus.

    blender -b --python tools/modelle/wirt_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/wirt_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/innen/wirt_innen.glb und Bilder nach docs/modelle/ (wirt_innen-blicke.png: das Bild jedes
Blickpunkts, wirt_innen-stufen.png: Erdgeschoss von oben je Stufe, wirt_innen-grundriss.png: Erdgeschoss, Fasskeller und Saal
mit den Ankern).

Koordinaten wie beim Schmied (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte der Schankstube
auf dem Boden. Das Haus ist außen Fachwerk (wirt.glb, Schankzeichen grüner Kranz, Wirtshausschild in Ocker mit Bierkrug,
weinrote Akzente), innen wie beim Parfumeur: Fachwerkwände 0,3 m mit hellem Lehmputz, Balkendecken:
    Werkraum    „Schankstube“ innen x −4,5 … 4,5, y −3,5 … 4,0, 3,2 m hoch; Boden in Stufe 1 und 2 gestampfter Lehm, in Stufe 3
                Backsteinpflaster im Fischgrätmuster, hinten ein Dielenstreifen (hinter der Theke die Falltür zum Keller).
                Hinten links die Theke mit dem Zapffass, dahinter Fässer auf dem Lager und ein Bord mit Krügen; links der
                Kamin mit Feuer; vier Tische mit Bänken; vorn rechts in der dunklen Ecke ab Stufe 2 der Würfeltisch; der grüne
                Kranz hängt über der Theke, das Wirtshausschild an der Wand. Rechts die Tür zur Küche, hinten rechts ab Stufe 3
                die Tür zur Treppe in den Saal.
    Kueche      „Küche“ rechts daneben: x 4,8 … 10,3, y −2,0 … 4,0, 3,2 m hoch, Backsteinpflaster. Gemauerter Braukessel aus
                Kupfer mit Maische, offener Herd mit Rauchfang und Kesselhaken, daran der Kochtopf; Hackklotz, Schinken und
                Zwiebelzöpfe an den Balken, Werkzeugwand; ab Stufe 3 die Brennblase für den Schnaps. Das Hoftor liegt in der
                rechten Wand der Küche: Fässer, Malz und Holz kommen über den Hof herein, nicht durch die Gaststube.
    Lager       „Fasskeller“ unter der Theke: x −2,8 … 3,4, y 0,6 … 5,2, Boden in −2,7 m, Tonnengewölbe aus Bruchstein (Scheitel
                2,5 m über dem Boden, entlang x). Durch einen Bogen in der linken Stirnwand kommt die steinerne Treppe von der
                Falltür herunter; Fässer liegend auf Lagern, Säcke, ein Lichtschacht in der rechten Stirnwand.
    Saal        ab Stufe 3 über der Schankstube (gleicher Grundriss, Boden in 3,25 m, 2,9 m hoch): Dielen, Täfelung, Butzenscheiben,
                langer Stammtisch, eine kleine Bühne für den Barden mit Wandbehang, Radleuchter.
Alle Räume sind rundum geschlossen, die Kamera steht im Raum.

Knoten im GLB – dieselben Regeln wie in den anderen Innenräumen (Raum_…, Decke_…, Stufe1–Stufe3, Bedienung_…, Tuer_…,
Einrichtung_…, Holztuer, Blick_…, Arbeit_…, Lager_…, Werkstueck_…, Licht_…, Dampf_…), dazu:
    Raum_Kueche                 zweiter Werkraum (neuer Raumschlüssel „Kueche“), immer da
    Raum_Saal                   extras „stufe“ = 3 (neuer Raumschlüssel „Saal“), ebenso Tuer_Werk_Saal, Tuer_Saal_Werk und alle
                                Anker darin; in Stufe 1 und 2 ist die Tür in der Schankstube ein verputztes Gefach
    Tuer_Werk_Lager             die offene Falltür hinter der Theke mit den obersten Stufen, Tuer_Lager_Werk die Treppe im Keller
    Gast_1 … Gast_12            Sitzplätze der Gäste an den vier Tischen (Fußpunkt auf dem Boden wie bei Arbeit_…, Blick −Z zum
                                Tisch; die Bewegung „Trinken“ setzt die Hüfte auf Sitzhöhe des Menschen – kleine Völker hebt das
                                Spiel wie bei der Feinarbeit), Reihenfolge = Füllreihenfolge (erst an der Theke, dann zum Kamin,
                                zuletzt vorn rechts); extras „sitzhoehe“ (Oberkante der Bank)
    Krug_1 … Krug_12            wo der Krug des Gastes n auf dem Tisch steht (Mitte des Bodens, wie figuren.KRUG_TISCH)
    Gast_Wuerfel_1 … 4          am Würfeltisch (extras „stufe“ = 2), dazu Krug_Wuerfel_1 … 4
    Gast_Stamm_1 … 6            am Stammtisch im Saal (stufe 3), dazu Krug_Stamm_1 … 6
    Barde_1                     auf der Bühne im Saal (stufe 3, stehend, Blick in den Saal; Bewegung „Laute“)
    Wuerfeltisch                Tisch, Hocker, Becher und Würfel (extras „stufe“ = 2)
    Werkstueck_1–4              Netze im Material „Fluessigkeit“ (Ursprung in der Mitte der Oberfläche): 1 Maische im Braukessel,
                                2 Bier in drei vollen Krügen auf der Theke neben dem Hahn, mit dem Kind Strahl_2 (aus der Tülle,
                                läuft in der Animation), 3 Suppe im Kochtopf (stufe 2), 4 Schnaps im Füllstutzen der Brennblase
                                (stufe 3) mit den Kindern Destillat_4 (in der Vorlage) und Tropfen (an der Tülle)
    Dampf_1, Dampf_2            über Braukessel und Kochtopf
    Arbeit_1_Ruehren (Braukessel), Arbeit_2_Zapfen (hinter der Theke), Arbeit_3_Ruehren (Herd, stufe 2),
    Arbeit_4_Destillieren (Brennblase, stufe 3): die Höhen passen zu figuren.ARBEITSHOEHEN („Kessel“, „Ofen“), figuren.VORLAGE
                                und figuren.ZAPFHAHN, HAHN_GRIFF, KRUG_AB, THEKE
    Hahn                        Küken des Zapfhahns (dreht sich in der Animation auf und zu)
Animation „Arbeit“ (4 s, als Schleife, solange gezapft wird; gleich lang wie die Bewegung „Zapfen“): Die Linke dreht das
Küken auf (figuren.zapfen 0,26 … 0,32), Bier läuft aus der Tülle in den Krug (bis 0,6), sie dreht zu (bis 0,66); an der Tülle
der Brennblase bildet sich zweimal ein Tropfen und fällt in die Vorlage. Bei Zeit 0 ist der Hahn zu und nichts zu sehen.
Lichter: extras „art“ = „Fenster“, „Lampe“ (Laternen) und „Glut“ (Kamin, Feuer unter Braukessel und Kochtopf, Feuertür der
Brennblase, Kerze am Würfeltisch, Kerzen des Radleuchters).
Materialien: „Farben“ (Eckenfarben, Materialart als UV.y wie bei den anderen Räumen), „Fluessigkeit“ (Werkstücke),
„Hausfarbe“ (Haustür, Hoftor), „Fenster“, „Glut“, „Eisen“.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import alchimist_innen as ai  # noqa: E402  (Drehkörper, Scheiben, Ofen mit Feuertür, Verkleinern)
import figuren as fg  # noqa: E402  (Kessel, Ofen, Vorlage, Zapfhahn, Krug auf dem Tisch)
import innenausbau as ia  # noqa: E402  (Böden, Decke, Prüfbericht, Vorschau)
import parfumeur_innen as pi  # noqa: E402  (Gefach, Täfelung, liegendes Fass, Butzenfenster, Wandquader)
import schmied_innen as si  # noqa: E402  (Wände, Pult, Türen, Werkzeugwand, Kisten, Löschfass, Anker)
import tischler_innen as ti  # noqa: E402  (Fachwerk innen, Fenster, Lichter, Laterne, Hoftor, Animation, Export)

NAME = "wirt_innen"
STUFEN = ("Schänke", "Wirtshaus", "Gasthaus")

bs.FARBEN.update({
    "Kranzgruen": (0.22, 0.38, 0.17),       # Schankzeichen: Kranz aus Tannengrün
    "Kranzgruen dunkel": (0.14, 0.27, 0.12),
    "Schildocker": (0.78, 0.58, 0.24),      # Wirtshausschild
    "Zinn": (0.62, 0.63, 0.62),             # Zinnteller und -becher
    "Tischplatte": (0.52, 0.38, 0.24),      # abgewetzte Eichenplatte (Holzmaserung aus den Oberflächendetails)
    "Schinken": (0.40, 0.21, 0.13),         # geräuchert
    "Schinken hell": (0.74, 0.50, 0.40),
    "Zwiebel": (0.74, 0.52, 0.28),
    "Malz": (0.68, 0.52, 0.30),
    "Brot": (0.68, 0.46, 0.24),
    "Wuerfel": (0.90, 0.87, 0.78),          # Knochenwürfel
    "Wandbehang": (0.38, 0.12, 0.15),       # weinroter Behang hinter der Bühne
    "Wandbehang hell": (0.74, 0.58, 0.30),
    "Buehne": (0.42, 0.29, 0.18),
})

# ── Maße ─────────────────────────────────────────────────────────────────────
WAND = ti.WAND                                                     # Fachwerkwand mit Putz, 0,3 m
WX0, WX1, WY0, WY1 = -4.5, 4.5, -3.5, 4.0                          # Schankstube innen
H_WERK = 3.2
STREIFEN_Y = 2.45                                                  # ab hier hinten Dielen (hinter der Theke)
KX0, KX1, KY0, KY1 = WX1 + WAND, 10.3, -2.0, 4.0                   # Küche innen
H_KUECHE = 3.2
CX0, CX1, CY0, CY1 = -2.8, 3.4, 0.6, 5.2                           # Fasskeller innen
Z_KELLER = -2.7                                                    # Boden des Kellers
KAEMPFER, STICH = 1.25, 1.25                                       # Gewölbe: Kämpfer über dem Boden, Stich (Scheitel darüber)
Z_SAAL = H_WERK + ia.DIELE                                         # Boden des Saals über der Schankstube (ab Stufe 3)
H_SAAL = 2.9
LUKE = (-4.35, -3.2, 2.75, 3.35)                                   # Falltür hinter der Theke (x0, x1, y0, y1)
KELLERTUER = (2.65, 3.45, 1.95)                                    # Bogen in der linken Stirnwand des Kellers (y0, y1, Höhe)
TREPPE_STUFEN = 11                                                 # Kellertreppe: Stufen von der Falltür (z 0) bis zum Keller
TREPPE_X0, TREPPE_X1 = LUKE[0], -2.25                              # … laufen entlang +x hinunter
KUECHENTUER_Y, TUER_B, TUER_H = 1.4, 1.0, 2.15                     # Tür Schankstube – Küche (rechte Wand)
SAALTUER_X = 3.6                                                   # Tür zur Treppe in den Saal (Rückwand, unten und oben)
HAUSTUER_X, HAUSTUER_B, HAUSTUER_H = -1.4, 1.0, 2.2                # Tür zur Gasse in der Vorderwand
HOFTOR_Y, HOFTOR_B, HOFTOR_H = 1.1, 2.4, 2.6                       # Hoftor in der rechten Wand der Küche
FPS = 24
ARBEIT_BILDER = round(fg.BEWEGUNGEN["Zapfen"] * FPS)               # Länge der Animation „Arbeit“ (4 s)

# Arbeitsplätze (Fußpunkt der Figur) und Geräte – die Höhen kommen aus figuren.py
W1 = Vector((6.1, 2.0, 0))                      # Rühren am Braukessel, Blick +y
BRAUKESSEL = W1 + Vector((0, fg.ARBEITSHOEHEN["Kessel"][0], 0))    # Mitte des Kessels am Boden
W2 = Vector((-1.5, 3.05, 0))                    # Zapfen hinter der Theke, Blick −y (die linke Seite der Figur ist +x)
THEKE_Y0, THEKE_Y1, THEKE_X0, THEKE_X1 = W2.y - 0.28 - 0.65, W2.y - 0.28, WX0, -0.6   # Platte der Theke
W3 = Vector((8.9, 2.6, 0))                      # Rühren am Kochtopf über dem Herd, Blick +y
KOCHTOPF = W3 + Vector((0, fg.ARBEITSHOEHEN["Kessel"][0], 0))
W4 = Vector((8.7, -1.0, 0))                     # Destillieren an der Brennblase (ab Stufe 3), Blick +x (die linke Seite ist +y)
BRENNHERD = W4 + Vector((pi.HERD_VOR + pi.HERD_T / 2, 0.155, 0))   # Herd der Brennblase wie beim Parfumeur (Maße pi.HERD_…)
BLASE = Vector((BRENNHERD.x, BRENNHERD.y - 0.075, pi.HERD_H))      # Brennblase (Mitte unten)
VORLAGE = W4 + Vector((fg.VORLAGE[1], fg.VORLAGE[0], 0))           # Blick +x: vor → +x, rechts → −y
STUTZEN = BLASE + Vector((-0.6 * 0.255, -0.8 * 0.255, 0))          # offener Füllstutzen auf der Schulter der Blase

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Werk_0": ((0.6, -3.2, 2.6), (-1.0, 2.6, 0.9)),          # Theke mit Zapffass, Tische, Kranz
    "Blick_Werk_L1": ((3.6, -2.4, 2.6), (-4.4, 0.0, 0.9)),         # Kamin, Tische
    "Blick_Werk_L2": ((1.8, -0.6, 2.6), (-3.8, 3.6, 0.6)),         # hinter die Theke: Fässer, Falltür
    "Blick_Werk_R1": ((-3.4, -2.8, 2.6), (4.4, 2.2, 0.9)),         # Küchentür, Treppe zum Saal, Pult
    "Blick_Werk_R2": ((-0.6, 1.4, 2.5), (4.0, -3.0, 0.7)),         # die dunkle Ecke mit dem Würfeltisch
    "Blick_Kueche_0": ((7.5, -1.7, 2.6), (7.5, 3.8, 0.9)),         # Braukessel, Herd mit Rauchfang
    "Blick_Kueche_L1": ((9.9, -1.6, 2.6), (5.2, 3.4, 0.9)),        # Braukessel, Werkzeugwand
    "Blick_Kueche_R1": ((5.3, -1.6, 2.6), (10.1, 2.0, 0.9)),       # Herd, Hoftor, Brennblase
    "Blick_Lager_0": ((3.1, 2.9, Z_KELLER + 2.0), (-2.8, 3.1, Z_KELLER + 0.8)),      # das Gewölbe entlang zur Treppe
    "Blick_Lager_L1": ((-2.2, 2.0, Z_KELLER + 1.85), (3.4, 4.8, Z_KELLER + 0.4)),    # Fässer an der Rückwand
    "Blick_Lager_R1": ((-2.2, 3.8, Z_KELLER + 1.85), (3.4, 0.8, Z_KELLER + 0.4)),    # Fassregale, Säcke, Lichtschacht
    "Blick_Saal_0": ((0.6, -3.2, Z_SAAL + 2.3), (-0.4, 3.6, Z_SAAL + 0.9)),          # Stammtisch, Bühne, Radleuchter
    "Blick_Saal_L1": ((3.6, -2.6, Z_SAAL + 2.3), (-4.4, 2.0, Z_SAAL + 1.0)),         # Bühne mit Wandbehang
    "Blick_Saal_R1": ((-3.6, -2.6, Z_SAAL + 2.3), (4.4, 2.4, Z_SAAL + 1.0)),         # Stammtisch, Tür zur Treppe
}

Oe = si.Oe
WERK_OEFFNUNGEN = {
    "vorne": [Oe(-3.4, 0.9, 1.0, 1.05), Oe(0.9, 0.9, 1.0, 1.05), Oe(HAUSTUER_X, 0.0, HAUSTUER_B, HAUSTUER_H)],
    "rechts": [Oe(KUECHENTUER_Y, 0.0, TUER_B, TUER_H)],                                 # vorn rechts bleibt die Ecke dunkel
    "hinten": [Oe(SAALTUER_X, 0.0, TUER_B, TUER_H)],
    "links": [Oe(1.3, 0.9, 0.8, 1.05)],
}
KUECHE_OEFFNUNGEN = {
    "vorne": [Oe(7.0, 1.1, 0.9, 1.1)],
    "rechts": [Oe(HOFTOR_Y, 0.0, HOFTOR_B, HOFTOR_H)],
    "hinten": [Oe(7.4, 1.4, 0.7, 0.9)],
    "links": [Oe(KUECHENTUER_Y, 0.0, TUER_B, TUER_H)],
}
SAAL_FENSTER = {"vorne": [Oe(x, 0.85, 0.75, 1.45) for x in (-2.8, 0.0, 2.8)], "links": [Oe(-1.0, 0.85, 0.75, 1.45)],
                "rechts": [Oe(0.0, 0.85, 0.75, 1.45)]}
SCHILDER = {"Tuer_Werk_Lager": "Treppe in den Fasskeller", "Tuer_Lager_Werk": "Treppe hinauf in die Schankstube",
            "Tuer_Werk_Kueche": "Tür zur Küche", "Tuer_Kueche_Werk": "Tür zur Schankstube",
            "Tuer_Werk_Saal": "Treppe zum Saal", "Tuer_Saal_Werk": "Treppe hinunter in die Schankstube"}

ecken = ia.ecken
drehkoerper, scheibe, am_ort = ai.drehkoerper, ai.scheibe, ai.am_ort
licht, kind, wq = pi.licht, pi.kind, pi.wq


# ─────────────────────────────────────────────────────────────────────────────
#  Hilfen
# ─────────────────────────────────────────────────────────────────────────────

def versetzt(ziel, st, bauen, dz):
    """Baut mit `bauen(netz)` in ein Hilfsnetz (für Bausteine, die nur auf z = 0 bauen: Lehmboden, Wände) und hängt das Ergebnis
    um `dz` nach oben verschoben an `ziel` an."""
    hilf = bs.Netz("_hilf", alle=ziel.alle)
    bauen(hilf)
    for flaeche, k in zip(hilf.flaechen, hilf.fstoff):
        ziel.vieleck([hilf.ecken[i] + Vector((0, 0, dz)) for i in flaeche], hilf.stoffe[k], immer=True)


def dielen(netz, st, x0, x1, y0, y1, z, saat=0, loch=None):
    """Dielenboden in Höhe z (Bretter wie innenausbau.dielen_plan, die Fugen 8 mm darunter), auf Wunsch mit einem Loch."""
    for bx0, bx1, by0, by1, stoff in ia.dielen_plan(x0, x1, y0, y1, saat):
        for r in ia.rechteck_minus((bx0, bx1, by0, by1), loch):
            ia.waagrecht(netz, r, z, True, st[stoff])
    for r in ia.rechteck_minus((x0, x1, y0, y1), loch):
        ia.waagrecht(netz, r, z - 0.008, True, st["Dielenfuge"])


def drehen(punkt, mitte, blick):
    """Punkt im örtlichen System eines Platzes (x seitlich nach links der Figur, y nach vorn) in die Welt: `mitte` ist der
    Fußpunkt, `blick` die Blickrichtung (x, y)."""
    vor = Vector((blick[0], blick[1], 0)).normalized()
    links = Vector((-vor.y, vor.x, 0))
    return Vector(mitte) + links * punkt[0] + vor * punkt[1] + Vector((0, 0, punkt[2] if len(punkt) > 2 else 0.0))


def krug(netz, st, boden, r=0.046, h=0.15, henkel=(1, 0), voll=True):
    """Steinzeugkrug wie in der Hand der Figuren (figuren.KRUG): Bauch mit blauen Bändern, Henkel in Richtung `henkel`."""
    profil = [(0.0, r * 0.82), (0.012, r * 0.9), (h * 0.45, r), (h * 0.85, r * 0.86), (h * 0.9, r * 0.9), (h, r * 0.88)]
    for (u0, r0), (u1, r1) in zip(profil, profil[1:]):
        drehkoerper(netz, st, Vector(boden) + Vector((0, 0, u0)), [(0.0, r0), (u1 - u0, r1)],
                    "Krugblau" if u0 in (0.012, h * 0.85) else "Krugton", n=7)
    if voll:
        scheibe(netz, st, boden, r * 0.84, boden[2] + h - 0.012, "Haar" if voll is True else voll, n=7)
    d = Vector((henkel[0], henkel[1], 0)).normalized()
    b = Vector(boden)
    for a, e in ((b + d * r * 0.9 + Vector((0, 0, h * 0.82)), b + d * (r + 0.03) + Vector((0, 0, h * 0.68))),
                 (b + d * (r + 0.03) + Vector((0, 0, h * 0.68)), b + d * (r + 0.03) + Vector((0, 0, h * 0.3))),
                 (b + d * (r + 0.03) + Vector((0, 0, h * 0.3)), b + d * r * 0.9 + Vector((0, 0, h * 0.2)))):
        netz.stab(a, e, 0.02, 0.012, st["Krugton"], quer=Vector((-d.y, d.x, 0)))


def bank(netz, st, a, b, hoehe=0.43, tiefe=0.32):
    """Wirtshausbank von a nach b (Mitte der Sitzfläche am Boden): dicke Bohle auf zwei Wangen."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    q = Vector((-d.y, d.x, 0))
    netz.stab(a + Vector((0, 0, hoehe - 0.03)), b + Vector((0, 0, hoehe - 0.03)), tiefe, 0.06, st["Holz alt"], quer=(0, 0, 1))
    for t in (0.12, 0.88):
        p = a.lerp(b, t)
        netz.quader((0, 0, 0), (1, 1, 1), st["Holz"], Matrix.Translation(p + Vector((0, 0, (hoehe - 0.07) / 2)))
                    @ Matrix((d, q, Vector((0, 0, 1)))).transposed().to_4x4() @ Matrix.Diagonal((0.06, tiefe - 0.04, hoehe - 0.07, 1)))


def tisch(netz, st, mitte, laenge, breite, hoehe=0.78, quer_x=True, platte="Tischplatte"):
    """Wirtshaustisch: dicke Platte auf zwei Kreuzböcken mit Steg (Länge entlang x, mit `quer_x` False entlang y)."""
    lx, ly = (laenge, breite) if quer_x else (breite, laenge)
    netz.quader(Vector(mitte) + Vector((0, 0, hoehe - 0.03)), (lx, ly, 0.06), st[platte])
    holz = st["Holz"]
    for s in (-1, 1):
        if quer_x:
            fuss = Vector(mitte) + Vector((s * (laenge / 2 - 0.22), 0, 0))
            for t in (-1, 1):                                                              # gekreuzt, versetzt
                netz.stab(fuss + Vector((0.035 * t, t * (breite / 2 - 0.08), 0)), fuss + Vector((0.035 * t, -t * 0.12, hoehe - 0.06)),
                          0.07, 0.06, holz, quer=(1, 0, 0))
        else:
            fuss = Vector(mitte) + Vector((0, s * (laenge / 2 - 0.22), 0))
            for t in (-1, 1):
                netz.stab(fuss + Vector((t * (breite / 2 - 0.08), 0.035 * t, 0)), fuss + Vector((-t * 0.12, 0.035 * t, hoehe - 0.06)),
                          0.07, 0.06, holz, quer=(0, 1, 0))
    a = Vector(mitte) + (Vector((-(laenge / 2 - 0.22), 0, 0.3)) if quer_x else Vector((0, -(laenge / 2 - 0.22), 0.3)))
    netz.stab(a, Vector(mitte) * 2 - a + Vector((0, 0, 0.6)), 0.06, 0.06, holz)


def gast(name, punkt, blick, sitz=0.43, stufe=None):
    """Sitzplatz eines Gastes (Fußpunkt auf dem Boden, Blick zum Tisch) mit extras „sitzhoehe“ (Oberkante der Bank)."""
    o = bs.anker(name, (punkt[0], punkt[1], punkt[2] if len(punkt) > 2 else 0.0), si.drehung_von(blick))
    o["sitzhoehe"] = sitz
    if stufe:
        o["stufe"] = stufe
    return o


def krug_platz(name, punkt, blick, stufe=None):
    """Krug_<n>: Mitte des Bodens des Krugs auf dem Tisch vor dem Gast (figuren.KRUG_TISCH, Mensch)."""
    sitz = fg.VOELKER["mensch"]["knie"] - 0.02
    p = drehen((fg.KRUG_TISCH[0], fg.KRUG_TISCH[1], sitz + fg.KRUG_TISCH[2]), punkt, blick)
    o = bs.anker(name, p, si.drehung_von(blick))
    if stufe:
        o["stufe"] = stufe
    return o


# ─────────────────────────────────────────────────────────────────────────────
#  Schankstube: Theke mit Zapffass, Fässer und Krüge, Kamin, Tische, Würfeltisch
# ─────────────────────────────────────────────────────────────────────────────

def zapf(punkt):
    """Punkt am Zapfplatz (seitlich, vor, Höhe wie in figuren.py) in der Welt: Die Figur steht bei W2 und blickt nach −y."""
    return drehen(punkt, W2, (0, -1))


def theke(netz, st):
    """Theke hinten links: Front aus Brettern mit weinroten Füllungen, dicke Platte (Oberkante figuren.THEKE), Fußleiste; auf der
    Schankseite ein Bord mit Krügen. Darauf liegt rechts vor dem Zapfplatz das Zapffass auf einem Bock (Boden mit dem Hahn zur
    Figur), daneben stehen Krüge."""
    x0, x1, y0, y1 = THEKE_X0, THEKE_X1, THEKE_Y0, THEKE_Y1
    h = fg.THEKE
    netz.quader(((x0 + x1) / 2 - 0.02, y0 + 0.02, (h - 0.06) / 2), (x1 - x0 - 0.04, 0.04, h - 0.06), st["Holz"], ohne=("-x",))  # Front
    netz.quader((x1 - 0.02, (y0 + y1) / 2, (h - 0.06) / 2), (0.04, y1 - y0, h - 0.06), st["Holz"], ohne=("+y", "-y"))  # Seite
    netz.quader(((x0 + x1) / 2 + 0.02, (y0 + y1) / 2, h - 0.03), (x1 - x0 + 0.04, y1 - y0 + 0.08, 0.06), st["Tischplatte"],
                ohne=("-x",))
    netz.quader(((x0 + x1) / 2, y0 - 0.005, 0.06), (x1 - x0, 0.03, 0.12), st["Fachwerk"], ohne=("-x", "-z"))      # Fußleiste
    n = round((x1 - x0) / 0.62)
    for k in range(n):                                                                    # Füllungen in Weinrot
        u0 = x0 + (x1 - x0) * k / n + 0.07
        u1 = x0 + (x1 - x0) * (k + 1) / n - 0.07
        netz.vieleck_nach([(u0, y0 - 0.002, 0.24), (u1, y0 - 0.002, 0.24), (u1, y0 - 0.002, h - 0.2), (u0, y0 - 0.002, h - 0.2)],
                          (0, -1, 0), st["Weinrot"])
        netz.quader(((u0 + u1) / 2, y0 - 0.008, h - 0.17), (u1 - u0 + 0.06, 0.012, 0.04), st["Fachwerk"], ohne=("+y",))
        netz.quader(((u0 + u1) / 2, y0 - 0.008, 0.21), (u1 - u0 + 0.06, 0.012, 0.04), st["Fachwerk"], ohne=("+y",))
    netz.quader(((x0 + x1) / 2 - 0.03, y1 - 0.05, 0.5), (x1 - x0 - 0.06, 0.3, 0.03), st["Holz alt"])        # Bord darunter
    for k in range(9):
        x = x0 + 0.5 + k * (x1 - x0 - 0.9) / 8
        if LUKE[0] - 0.05 < x < LUKE[1] + 0.05:
            continue
        krug(netz, st, Vector((x, y1 - 0.05, 0.515)), henkel=(0, 1), voll=False)
    # Zapffass auf dem Bock: liegend entlang x, der Boden mit dem Hahn zur Figur
    r = 0.24
    sx, sv, sh = fg.ZAPFHAHN
    gx, gv, gh = fg.HAHN_GRIFF
    achse_z = sh + 0.07 + 0.12
    a = zapf((sx - 0.08, sv, achse_z))
    e = zapf((sx - 0.78, sv, achse_z))
    M = Matrix.Translation(a) @ Matrix.Rotation(math.radians(180), 4, "Z")                  # örtlich x vom Boden ins Fass
    profil = [(r * math.cos(2 * math.pi * k / 12), r * math.sin(2 * math.pi * k / 12)) for k in range(12)]
    laenge = (e - a).length
    netz.prisma([(0.92 * y, 0.92 * z) for y, z in profil], 0.0, 0.06, st["Holz alt"], matrix=M)
    netz.prisma(profil, 0.06, laenge - 0.06, st["Holz alt"], matrix=M)
    netz.prisma([(0.92 * y, 0.92 * z) for y, z in profil], laenge - 0.06, laenge, st["Holz alt"], matrix=M)
    for x in (0.1, laenge - 0.14):
        netz.prisma([(1.04 * y, 1.04 * z) for y, z in profil], x, x + 0.04, st["Eisen"], matrix=M)
    for t in (0.3, 0.7):                                                                   # Bock: zwei Sättel auf der Platte
        p = a.lerp(e, t)
        netz.quader(Vector((p.x, p.y, (h + achse_z - r) / 2 + 0.02)), (0.08, 0.42, achse_z - r - h + 0.04), st["Holz"])
    rohr = zapf((sx - 0.08, sv, sh + 0.07))
    netz.stab(rohr, zapf((sx + 0.015, sv, sh + 0.07)), 0.035, 0.035, st["Messing"])         # Hahn: Rohr und Tülle
    netz.stab(zapf((sx, sv, sh + 0.085)), zapf((sx, sv, sh)), 0.03, 0.03, st["Messing"])
    netz.stab(zapf((gx, gv, sh + 0.07)), zapf((gx, gv, sh + 0.095)), 0.02, 0.02, st["Messing"])   # Hals des Kükens
    for i, (s, v) in enumerate(((0.36, 0.48), (0.5, 0.42), (0.44, 0.6))):                  # volle Krüge (Bier: Werkstueck_2)
        krug(netz, st, zapf((s, v, h)), henkel=(1, 0.3 * i), voll=False)
    for s, v in ((0.62, 0.36), (0.72, 0.5)):                                              # leere Krüge
        krug(netz, st, zapf((s, v, h)), henkel=(0, 1), voll=False)


def zapf_werkstueck(st):
    """Werkstueck_2: das Bier in den drei vollen Krügen auf der Theke; Kind Strahl_2 läuft aus der Tülle in den Krug der Figur.
    Gibt (Werkstück, Strahl) zurück."""
    h = fg.THEKE
    werkstueck = bs.Netz("Werkstueck_2")
    for s, v in ((0.36, 0.48), (0.5, 0.42), (0.44, 0.6)):
        scheibe(werkstueck, st, zapf((s, v, 0)), fg.KRUG["r"] * 0.84, h + fg.KRUG["h"] - 0.012, "Fluessigkeit", n=7)
    mitte = zapf((0.5, 0.42, h + fg.KRUG["h"] - 0.012))
    o = am_ort(werkstueck, mitte)
    strahl = bs.Netz("Strahl_2", alle=True)
    sx, sv, sh = fg.ZAPFHAHN
    tuelle = zapf((sx, sv, sh))
    drehkoerper(strahl, st, tuelle - Vector((0, 0, 0.11)), [(0.0, 0.007), (0.11, 0.009)], "Fluessigkeit", n=5)
    o_strahl = kind(am_ort(strahl, tuelle), o)
    return o, o_strahl


def hahn(st):
    """Das Küken des Zapfhahns (eigenes Netz „Hahn“, dreht sich um den Hals): ein flacher Griff quer zum Rohr."""
    netz = bs.Netz("Hahn", alle=True)
    gx, gv, gh = fg.HAHN_GRIFF
    mitte = zapf((gx, gv, gh))
    netz.stab(mitte - Vector((0, 0, 0.04)), mitte + Vector((0, 0, 0.015)), 0.016, 0.016, st["Messing"])
    netz.quader(mitte + Vector((0, 0, 0.015)), (0.07, 0.018, 0.03), st["Messing"])
    return am_ort(netz, mitte - Vector((0, 0, 0.04)))


def zapf_bewegung(strahl, kueken):
    """Animation am Zapfplatz im Takt von figuren.zapfen: das Küken dreht auf (0,26–0,32 der Schleife) und zu (0,6–0,66), dazwischen
    läuft das Bier (der Strahl wächst aus der Tülle und schwindet am Ende)."""
    ruhe_s, ruhe_k = strahl.location.copy(), kueken.rotation_euler.copy()

    def auf(phase):
        if phase < 0.26 or phase >= 0.66:
            return 0.0
        if phase < 0.32:
            return fg.weich((phase - 0.26) / 0.06)
        if phase < 0.6:
            return 1.0
        return 1 - fg.weich((phase - 0.6) / 0.06)

    def strahl_bewegung(bild):
        a = auf(bild / ARBEIT_BILDER)
        return ruhe_s, None, (max(0.01, a), max(0.01, a), max(0.01, min(1.0, 1.6 * a)))

    def kueken_bewegung(bild):
        a = auf(bild / ARBEIT_BILDER)
        return None, (ruhe_k.x, ruhe_k.y, ruhe_k.z + math.radians(90) * a), None
    strahl.scale = (0.01, 0.01, 0.01)
    return [(strahl, strahl_bewegung), (kueken, kueken_bewegung)]


def fasslager_hinten(netz, st):
    """Hinter der Theke: liegende Fässer auf einem Lager an der Rückwand, darüber ein Bord mit Krügen und Zinntellern und das
    Wirtshausschild (Ocker mit Bierkrug, weinroter Rand)."""
    for x in (-2.95, -2.2):
        pi.fass_liegend(netz, st, Vector((x, WY1 - 0.34, 0.42)), r=0.3, laenge=0.6)
    M = si.rahmen(-2.0, WY1, 180)
    si.q(netz, st, M, (0, 0.14, 1.72), (2.4, 0.28, 0.04), "Holz alt", ohne=("-y",))
    for x in (-1.0, 1.0):
        netz.stab(M @ Vector((x, 0.02, 1.7)), M @ Vector((x, 0.24, 1.7)), 0.04, 0.06, st["Holz"], quer=M.to_3x3() @ Vector((1, 0, 0)))
    zufall = random.Random(5)
    x = -1.1
    while x < 1.1:
        if zufall.random() < 0.3:                                                          # Zinnteller, an die Wand gelehnt
            mitte = M @ Vector((x + 0.08, 0.06, 1.74 + 0.11))
            netz.vieleck_nach([mitte + M.to_3x3() @ Vector((0.11 * math.cos(w), -0.03 * math.sin(w), 0.11 * math.sin(w)))
                               for w in (2 * math.pi * k / 10 for k in range(10))], M.to_3x3() @ Vector((0, 1, 0.25)), st["Zinn"])
            x += 0.26
        else:
            krug(netz, st, M @ Vector((x + 0.05, 0.14, 1.74)), r=0.04 + 0.008 * zufall.random(), h=0.13 + 0.05 * zufall.random(),
                 henkel=(1, 0), voll=False)
            x += 0.15
    # Wirtshausschild
    si.q(netz, st, M, (0, 0.03, 2.5), (1.1, 0.06, 0.5), "Schildocker", ohne=("-y",))
    si.q(netz, st, M, (0, 0.045, 2.5), (1.18, 0.03, 0.58), "Weinrot", ohne=("-y", "+y"))
    n = M.to_3x3() @ Vector((0, 1, 0))
    umriss = [(-0.12, 2.32), (0.1, 2.32), (0.13, 2.36), (0.13, 2.62), (0.15, 2.66), (-0.14, 2.66), (-0.12, 2.62)]
    netz.vieleck_nach([M @ Vector((x, 0.062, z)) for x, z in umriss], n, st["Holz alt"])               # Bierkrug
    netz.vieleck_nach([M @ Vector((x, 0.062, z)) for x, z in ((0.13, 2.4), (0.24, 2.42), (0.24, 2.56), (0.13, 2.58), (0.13, 2.53),
                                                              (0.2, 2.52), (0.2, 2.46), (0.13, 2.45))], n, st["Holz alt"])
    netz.vieleck_nach([M @ Vector((x, 0.063, z)) for x, z in ((-0.15, 2.64), (0.17, 2.64), (0.12, 2.71), (0.0, 2.73), (-0.11, 2.7))],
                      n, st["Wuerfel"])                                                          # Schaum
    for s in (-1, 1):
        netz.stab(M @ Vector((s * 0.4, 0.03, 2.78)), M @ Vector((s * 0.4, 0.03, H_WERK)), 0.02, 0.02, st["Eisen"])


def kranz(netz, st, mitte):
    """Schankzeichen innen: ein grüner Kranz aus Tannengrün mit weinroten Bändern an drei Schnüren unter einem Deckenbalken."""
    alle, netz.alle = netz.alle, True
    zufall = random.Random(9)
    n, rk = 14, 0.32
    for k in range(n):
        w0, w1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        a = mitte + Vector((rk * math.cos(w0), rk * math.sin(w0), 0))
        b = mitte + Vector((rk * math.cos(w1), rk * math.sin(w1), 0))
        netz.stab(a, b, 0.11, 0.09, st["Kranzgruen" if k % 2 else "Kranzgruen dunkel"], quer=(0, 0, 1))
        for _ in range(2):                                                                 # Zweige stehen ab
            p = a.lerp(b, zufall.random())
            d = (p - mitte).normalized()
            netz.stab(p, p + d * 0.08 + Vector((0, 0, zufall.uniform(-0.05, 0.05))), 0.05, 0.015, st["Kranzgruen"], quer=(0, 0, 1))
    for k in range(3):
        w = 2 * math.pi * k / 3 + 0.3
        p = mitte + Vector((rk * math.cos(w), rk * math.sin(w), 0.03))
        netz.stab(p, mitte + Vector((0, 0, H_WERK - 0.26 - mitte.z)), 0.012, 0.012, st["Seil"])
        q = Vector((-math.sin(w), math.cos(w), 0))
        netz.vieleck_nach([p - q * 0.03, p + q * 0.03, p + q * 0.02 - Vector((0, 0, 0.32)), p - q * 0.04 - Vector((0, 0, 0.28))],
                          Vector((math.cos(w), math.sin(w), 0)), st["Weinrot"])
    netz.alle = alle


def kamin(netz, st, anker):
    """Kamin an der linken Wand: Herdplatte aus Sandstein, Wangen und Sturz aus Bruchstein, verrußte Rückwand, Rauchfang bis
    in die Decke; Scheite, Glut und Flammen („Glut“), ein Kesselhaken und Schürhaken."""
    ym, b, t = -1.0, 1.7, 0.62
    x0 = WX0
    y0, y1 = ym - b / 2, ym + b / 2
    netz.quader((x0 + t / 2 + 0.05, ym, 0.12), (t + 0.1, b + 0.2, 0.24), st["Sandstein"], ohne=("-x", "-z"))      # Herdplatte
    for y in (y0 + 0.11, y1 - 0.11):                                                       # Wangen
        netz.quader((x0 + t / 2, y, 0.24 + 0.58), (t, 0.22, 1.16), st["Bruchstein"], ohne=("-x", "-z"))
    netz.quader((x0 + t / 2 + 0.02, ym, 1.6), (t + 0.04, b, 0.4), st["Sandstein"], ohne=("-x",))           # Sturz
    netz.quader((x0 + t / 2 + 0.06, ym, 1.83), (t + 0.12, b + 0.12, 0.06), st["Holz alt"], ohne=("-x",))   # Kaminsims
    netz.vieleck_nach([(x0 + 0.01, y0 + 0.22, 0.24), (x0 + 0.01, y1 - 0.22, 0.24), (x0 + 0.01, y1 - 0.22, 1.4),
                       (x0 + 0.01, y0 + 0.22, 1.4)], (1, 0, 0), st["Russ"])
    alle, netz.alle = netz.alle, True
    netz.vieleck_nach([(x0, y0 + 0.22, 1.4), (x0 + t, y0 + 0.22, 1.4), (x0 + t, y1 - 0.22, 1.4), (x0, y1 - 0.22, 1.4)], (0, 0, -1),
                      st["Russ"])
    netz.alle = alle
    oben = H_WERK                                                                          # Rauchfang: verjüngt bis in die Decke
    for (ya, yb), (xa, xb) in (((y0, y1), (x0 + t + 0.02, x0 + 0.42)),):
        netz.vieleck_nach([(xa, ya, 1.86), (xa, yb, 1.86), (xb, yb - 0.4, oben), (xb, ya + 0.4, oben)], (1, 0, 0.4), st["Lehmputz hell"])
        for y, yo, s in ((ya, ya + 0.4, -1), (yb, yb - 0.4, 1)):
            netz.vieleck_nach([(x0, y, 1.86), (xa, y, 1.86), (xb, yo, oben), (x0, yo, oben)], (0, s, 0.3), st["Lehmputz hell"])
    zufall = random.Random(3)
    feuer = Vector((x0 + 0.3, ym, 0.24))
    scheibe(netz, st, feuer, 0.36, 0.25, "Glut", n=9)
    for i, w in enumerate((0.4, 1.3, 2.4, 3.0)):                                           # Scheite über Kreuz
        d = Vector((math.cos(w), math.sin(w), 0))
        netz.stab(feuer + d * 0.3 + Vector((0, 0, 0.06 + 0.04 * i)), feuer - d * 0.25 + Vector((0, 0, 0.1 + 0.04 * i)), 0.09, 0.09,
                  st["Scheitholz"])
    for k in range(5):                                                                     # Flammen
        p = feuer + Vector((zufall.uniform(-0.12, 0.12), zufall.uniform(-0.28, 0.28), 0.1))
        drehkoerper(netz, st, p, [(0.0, 0.07), (0.12, 0.06), (0.3 + 0.12 * zufall.random(), 0.0)], "Glut", n=5)
    netz.stab((x0 + 0.06, ym, 1.4), (x0 + 0.3, ym, 1.4), 0.03, 0.03, st["Eisen"])            # Kesselhaken
    netz.stab((x0 + 0.3, ym, 1.4), (x0 + 0.3, ym, 0.95), 0.02, 0.02, st["Eisen"])
    netz.stab((x0 + t + 0.08, y1 + 0.02, 0.24), (x0 + t + 0.14, y1 + 0.06, 1.0), 0.025, 0.025, st["Eisen"])   # Schürhaken
    anker.append(licht(feuer + Vector((0.35, 0, 0.35)), (1, 0, 0), "Glut"))
    bs.fass(netz, st, Vector((x0 + 0.25, y1 + 0.42, 0)), r=0.2, h=0.5, reifen=(), stoff="Holz alt")       # Holzkorb
    for k in range(5):
        netz.stab((x0 + 0.12 + 0.05 * k, y1 + 0.3, 0.5 + 0.02 * k), (x0 + 0.17 + 0.05 * k, y1 + 0.55, 0.55), 0.07, 0.07, st["Scheitholz"])


def tische_und_gaeste(netz, st, anker):
    """Vier Tische mit Bänken; je Tisch drei Plätze Gast_<n> (die Nummern füllen erst den Tisch an der Theke, dann den daneben,
    den am Kamin und zuletzt den vorn rechts) mit Krug_<n> davor; auf den Tischen Kerzenstumpen, Teller und Brot."""
    nummer = 1
    tische = ((-2.6, 0.55, (("n", -0.45), ("n", 0.45), ("s", 0.0))), (0.9, 0.6, (("n", 0.45), ("s", -0.45), ("s", 0.45))),
              (-2.35, -2.25, (("n", 0.45), ("n", -0.45), ("s", 0.0))), (1.0, -2.05, (("n", -0.45), ("s", 0.45), ("n", 0.45))))
    zufall = random.Random(21)
    for cx, cy, plaetze in tische:
        tisch(netz, st, Vector((cx, cy, 0)), 1.6, 0.8)
        for s in (-1, 1):
            bank(netz, st, (cx - 0.8, cy + s * 0.62, 0), (cx + 0.8, cy + s * 0.62, 0))
        for seite, dx in plaetze:
            blick = (0, -1) if seite == "n" else (0, 1)
            punkt = Vector((cx + dx, cy + (0.62 if seite == "n" else -0.62), 0))
            anker.append(gast(f"Gast_{nummer}", punkt, blick))
            anker.append(krug_platz(f"Krug_{nummer}", punkt, blick))
            nummer += 1
        scheibe(netz, st, (cx + zufall.uniform(-0.3, 0.3), cy + 0.08, 0), 0.1, 0.781, "Zinn", n=10)
        drehkoerper(netz, st, Vector((cx + 0.6, cy - 0.05, 0.78)), [(0.0, 0.03), (0.07, 0.025), (0.07, 0.0)], "Kerze", n=6)
        if zufall.random() < 0.6:                                                          # ein Laib Brot
            drehkoerper(netz, st, Vector((cx - 0.55, cy - 0.1, 0.78)), [(0.0, 0.08), (0.04, 0.09), (0.08, 0.06), (0.1, 0.0)], "Brot", n=8)
    return nummer - 1


def wuerfeltisch(st, anker):
    """Ab Stufe 2: der Würfeltisch in der dunklen Ecke vorn rechts – runder Tisch, vier Hocker, ein Lederbecher, drei Würfel,
    Münzen, eine Kerze (Licht „Glut“). Vier Plätze Gast_Wuerfel_<n> mit Krug_Wuerfel_<n>. Gibt das Netz zurück."""
    netz = bs.Netz("Wuerfeltisch")
    m = Vector((3.45, -2.55, 0))
    drehkoerper(netz, st, m, [(0.0, 0.25), (0.05, 0.2), (0.06, 0.06), (0.72, 0.06), (0.72, 0.45), (0.78, 0.45)], "Tischplatte", n=12)
    for k in range(4):
        w = math.radians(45 + 90 * k)
        p = m + Vector((0.62 * math.cos(w), 0.62 * math.sin(w), 0))
        drehkoerper(netz, st, p, [(0.0, 0.14), (0.38, 0.12), (0.38, 0.18), (0.43, 0.18)], "Holz alt", n=8)
        blick = (-math.cos(w), -math.sin(w))
        o = gast(f"Gast_Wuerfel_{k + 1}", p, blick, stufe=2)
        anker.append(o)
        anker.append(krug_platz(f"Krug_Wuerfel_{k + 1}", p, blick, stufe=2))
    becher = m + Vector((0.08, 0.1, 0.78))
    drehkoerper(netz, st, becher, [(0.0, 0.04), (0.11, 0.05), (0.11, 0.045), (0.02, 0.04)], "Leder", n=8, innen=False)
    zufall = random.Random(7)
    for k in range(3):
        p = m + Vector((zufall.uniform(-0.2, 0.15), zufall.uniform(-0.2, 0.0), 0.78 + 0.011))
        netz.quader((0, 0, 0), (0.022, 0.022, 0.022), st["Wuerfel"], Matrix.Translation(p) @ Matrix.Rotation(zufall.uniform(0, 1.5), 4, "Z"))
    for k in range(6):
        scheibe(netz, st, m + Vector((zufall.uniform(-0.25, 0.25), zufall.uniform(0.05, 0.3), 0)), 0.013, 0.782 + 0.003 * (k % 2),
                "Gold" if k % 3 else "Zinn", n=6)
    kerze = m + Vector((-0.18, 0.18, 0.78))
    drehkoerper(netz, st, kerze, [(0.0, 0.035), (0.03, 0.03), (0.03, 0.015), (0.12, 0.015)], "Kerze", n=6)
    drehkoerper(netz, st, kerze + Vector((0, 0, 0.12)), [(0.0, 0.0), (0.01, 0.01), (0.04, 0.0)], "Glut", n=5)
    o = licht(kerze + Vector((0, 0, 0.2)), (0, 0, 1), "Glut", 2)
    anker.append(o)
    return netz


def falltuer(netz, st):
    """Tuer_Werk_Lager: das Loch im Dielenstreifen hinter der Theke mit einem Kranz aus Bohlen, die Klappe hochgestellt an der
    Rückseite (Bänder, Ring), darunter der Schacht mit den obersten Stufen der Kellertreppe."""
    lx0, lx1, ly0, ly1 = LUKE
    alle, netz.alle = netz.alle, True
    k, h = 0.07, 0.04
    netz.quader(((lx0 + lx1) / 2, ly0 - k / 2, h / 2), (lx1 - lx0 + 2 * k, k, h), st["Fachwerk"], ohne=("-z",))
    netz.quader(((lx0 + lx1) / 2, ly1 + k / 2, h / 2), (lx1 - lx0 + 2 * k, k, h), st["Fachwerk"], ohne=("-z",))
    for x in (lx0 - k / 2, lx1 + k / 2):
        netz.quader((x, (ly0 + ly1) / 2, h / 2), (k, ly1 - ly0, h), st["Fachwerk"], ohne=("-z", "+y", "-y"))
    lang, d = lx1 - lx0 - 0.02, 0.05                                                        # Klappe, an der Rückseite hochgestellt
    y = ly1 + k + d / 2 + 0.005
    netz.quader(((lx0 + lx1) / 2, y, h + lang / 2 * 0.62), (lang, d, lang * 0.62), st["Diele"])
    for t in (0.25, 0.75):
        netz.quader((lx0 + lang * t, y - d / 2 - 0.006, h + lang * 0.31), (0.05, 0.012, lang * 0.58), st["Eisen"], ohne=("+y",))
    netz.quader(((lx0 + lx1) / 2, y - d / 2 - 0.02, h + lang * 0.5), (0.07, 0.04, 0.07), st["Eisen"], ohne=("+y",))
    treppe_und_schacht(netz, st, oben=True)
    netz.alle = alle


def treppe_und_schacht(netz, st, oben):
    """Kellertreppe aus Sandsteinstufen von der Falltür (z 0) hinunter in den Keller, entlang +x; Schachtwände aus Bruchstein.
    `oben`: die Stufen unter der Falltür bis 1,6 m (Schankstube), sonst der Rest im Keller."""
    lauf = (TREPPE_X1 - TREPPE_X0) / TREPPE_STUFEN
    hub = -Z_KELLER / TREPPE_STUFEN
    y0, y1 = LUKE[2], LUKE[3]
    grenze = -1.6
    for k in range(TREPPE_STUFEN - 1):
        z = -(k + 1) * hub
        if (z > grenze) != oben:
            continue
        x = TREPPE_X0 + k * lauf
        netz.quader((x + lauf / 2, (y0 + y1) / 2, z - hub), (lauf, y1 - y0, 2 * hub), st["Sandstein" if k % 2 else "Haustein"],
                    ohne=("+y", "-y", "-z", "-x"))
    xe = CX0 if oben else CX0
    z0, z1 = (grenze, 0.0) if oben else (Z_KELLER, grenze)
    for y, n in ((y0, (0, 1, 0)), (y1, (0, -1, 0))):
        netz.vieleck_nach([(TREPPE_X0, y, z0), (xe, y, z0), (xe, y, z1), (TREPPE_X0, y, z1)], n, st["Bruchstein"])
    netz.vieleck_nach([(TREPPE_X0, y0, z0), (TREPPE_X0, y1, z0), (TREPPE_X0, y1, z1), (TREPPE_X0, y0, z1)], (1, 0, 0), st["Bruchstein dunkel"])
    if not oben:
        netz.vieleck_nach([(TREPPE_X0, y0, -0.02), (xe, y0, -0.02), (xe, y1, -0.02), (TREPPE_X0, y1, -0.02)], (0, 0, -1), st["Bruch dunkel"])


def treppenhaus(netz, st, z, hinauf):
    """Kulisse hinter der Tür zur Saaltreppe (Rückwand bei SAALTUER_X): ein Gang aus Lehmputz, in dem eine Holztreppe nach rechts
    hinauf (`hinauf`) bzw. hinunter läuft; die Tür steht offen, an die Wand geklappt."""
    alle, netz.alle = netz.alle, True
    x0, x1 = SAALTUER_X - 0.8, SAALTUER_X + 0.8
    y0, y1 = WY1 + WAND, WY1 + WAND + 0.95
    hoch = 2.4
    for a, b, n in (((x0, y1), (x1, y1), (0, -1, 0)), ((x0, y0), (x0, y1), (1, 0, 0)), ((x1, y0), (x1, y1), (-1, 0, 0))):
        netz.vieleck_nach([(a[0], a[1], z), (b[0], b[1], z), (b[0], b[1], z + hoch), (a[0], a[1], z + hoch)], n, st["Lehmputz hell"])
    netz.vieleck_nach([(x0, y0, z + hoch), (x1, y0, z + hoch), (x1, y1, z + hoch), (x0, y1, z + hoch)], (0, 0, -1), st["Diele dunkel"])
    netz.vieleck_nach([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], (0, 0, 1), st["Diele"])
    for k in range(6):                                                                     # Stufen
        hz = 0.2 * (k + 1) if hinauf else -0.2 * (k + 1)
        xs = SAALTUER_X - 0.35 + 0.22 * k if hinauf else SAALTUER_X - 0.35 - 0.22 * k
        if hinauf:
            netz.quader((xs + 0.11, (y0 + y1) / 2 + 0.05, z + hz - 0.02), (0.24, y1 - y0 - 0.1, 0.04), st["Holz"])
        elif k == 0:
            netz.vieleck_nach([(x0, y0, z + 0.004), (SAALTUER_X - 0.35, y0, z + 0.004), (SAALTUER_X - 0.35, y1, z + 0.004),
                               (x0, y1, z + 0.004)], (0, 0, 1), st["Diele dunkel"])                                       # Treppenloch
    netz.stab((x0 + 0.05, y1 - 0.05, z + 0.9), (x1 - 0.05, y1 - 0.05, z + 0.9 + (0.9 if hinauf else -0.9)), 0.05, 0.05, st["Holz"])
    M = si.rahmen(SAALTUER_X, WY1, 180, z)
    if not hinauf:                                                                     # unten umrahmt das Fachwerk die Tür
        si.tuerstock(netz, st, M, TUER_B, TUER_H)
    links = M @ Vector((TUER_B / 2, 0.0, 0))                                               # offene Tür, in den Raum geklappt
    netz.quader(links + Vector((0.03, -TUER_B / 2, TUER_H / 2 - 0.01)), (0.05, TUER_B - 0.04, TUER_H - 0.03), st["Brett"])
    for t in (0.18, 0.82):
        netz.quader(links + Vector((0.06, -TUER_B / 2 + 0.05, TUER_H * t)), (0.012, TUER_B * 0.8, 0.06), st["Eisen"])
    netz.alle = alle


# ─────────────────────────────────────────────────────────────────────────────
#  Küche: Braukessel, Herd mit Kochtopf, Brennblase, Vorräte
# ─────────────────────────────────────────────────────────────────────────────

def braukessel(netz, st, anker):
    """Gemauerter Braukessel: runder Ofen aus Ziegeln mit Feuerloch zur linken Wand, darin der Kupferkessel (Rand 0,95 m, die
    Maische 10 cm darunter wie bei figuren.ARBEITSHOEHEN „Kessel“), darüber eine hölzerne Dunsthaube mit Rohr in die Decke;
    ein Maischholz lehnt am Rand. Gibt Werkstueck_1 (die Maische) und den Ort für Dampf_1 zurück."""
    k = BRAUKESSEL
    drehkoerper(netz, st, k, [(0.0, 0.62), (0.6, 0.6)], "Ziegelmauer", n=12)
    drehkoerper(netz, st, k, [(0.6, 0.6), (0.66, 0.6), (0.66, 0.52)], "Sandstein", n=12)
    profil = [(0.6, 0.5), (0.95, 0.47), (0.98, 0.5), (0.98, 0.46)]                        # Kupferkessel mit Wulst
    drehkoerper(netz, st, k, profil, "Kupferblech", n=16)
    drehkoerper(netz, st, k, [(0.8, 0.455), (0.98, 0.46)], "Kupferblech", n=16, innen=True)
    scheibe(netz, st, k, 0.455, 0.8, "Kupfer", n=16)
    # Feuerloch nach −x mit Glut
    for z0, z1, w in ((0.0, 0.32, 0.18),):
        tuer = [k + Vector((-0.605, -w, z0 + 0.06)), k + Vector((-0.605, w, z0 + 0.06)), k + Vector((-0.605, w, z1)),
                k + Vector((-0.605, -w * 0.6, z1 + 0.06)), k + Vector((-0.605, -w, z1))]
        netz.vieleck_nach(tuer, (-1, 0, 0), st["Glut"])
        netz.stab(k + Vector((-0.62, -w - 0.03, z0 + 0.05)), k + Vector((-0.62, w + 0.03, z0 + 0.05)), 0.04, 0.04, st["Eisen"])
    anker.append(licht(k + Vector((-0.9, 0, 0.3)), (-1, 0, 0), "Glut"))
    # Dunsthaube
    haube = k + Vector((0, 0, 2.05))
    drehkoerper(netz, st, haube, [(0.0, 0.62), (0.06, 0.62), (0.55, 0.2), (H_KUECHE - 2.05, 0.17)], "Holz alt", n=10)
    for w in (0.4, 2.5, 4.6):
        netz.stab(haube + Vector((0.6 * math.cos(w), 0.6 * math.sin(w), 0.03)), haube + Vector((0.2 * math.cos(w), 0.2 * math.sin(w),
                  H_KUECHE - 2.05)), 0.02, 0.02, st["Eisen"])
    netz.stab(k + Vector((0.3, 0.2, 0.7)), k + Vector((0.62, 0.45, 1.8)), 0.05, 0.05, st["Holz"])               # Maischholz
    netz.quader(k + Vector((0.62, 0.45, 1.82)), (0.18, 0.04, 0.08), st["Holz"], Matrix.Rotation(0.7, 4, "Z"))
    werkstueck = bs.Netz("Werkstueck_1")
    scheibe(werkstueck, st, k, 0.45, 0.85, "Fluessigkeit", n=16)
    return am_ort(werkstueck, k + Vector((0, 0, 0.85))), k + Vector((0, 0, 0.95))


def herd(netz, st, anker):
    """Offener Herd an der Rückwand: gemauerte Herdbank, Feuer aus Scheiten, darüber an einem Kesselhaken der Kochtopf (Rand
    0,95 m vor der Figur am Platz 3), Rauchfang aus verrußtem Lehm bis in die Decke, Pfannen und Kellen am Sims. Gibt
    Werkstueck_3 (die Suppe) und den Ort für Dampf_2 zurück."""
    x0, x1, y0, y1, h = 7.9, 9.9, 3.0, KY1, 0.45
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, h / 2), (x1 - x0, y1 - y0, h), st["Ziegelmauer"], ohne=("+y", "-z"))
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2 - 0.02, h + 0.02), (x1 - x0 + 0.04, y1 - y0 + 0.04, 0.04), st["Sandstein"], ohne=("+y", "-z"))
    p = KOCHTOPF
    feuer = Vector((p.x, p.y + 0.05, h + 0.04))
    scheibe(netz, st, feuer, 0.32, h + 0.045, "Glut", n=9)
    for i, w in enumerate((0.3, 1.5, 2.7)):
        d = Vector((math.cos(w), math.sin(w), 0))
        netz.stab(feuer + d * 0.28 + Vector((0, 0, 0.05 + 0.03 * i)), feuer - d * 0.24 + Vector((0, 0, 0.08 + 0.03 * i)), 0.08, 0.08,
                  st["Scheitholz"])
    anker.append(licht(feuer + Vector((0, -0.45, 0.25)), (0, -1, 0), "Glut"))
    # Rauchfang: Mantel auf zwei Konsolen, verjüngt bis in die Decke
    za = 1.85
    netz.quader(((x0 + x1) / 2, y0 + 0.02, za - 0.05), (x1 - x0 + 0.2, 0.1, 0.12), st["Holz alt"])
    for x in (x0 - 0.03, x1 + 0.03):
        netz.quader((x, (y0 + y1) / 2 - 0.04, za - 0.15), (0.1, y1 - y0 - 0.08, 0.2), st["Holz alt"])
    netz.vieleck_nach([(x0 - 0.1, y0 - 0.03, za), (x1 + 0.1, y0 - 0.03, za), (x1 - 0.35, y1 - 0.35, H_KUECHE),
                       (x0 + 0.35, y1 - 0.35, H_KUECHE)], (0, -1, 0.6), st["Russputz"])
    for x, xo, s in ((x0 - 0.1, x0 + 0.35, -1), (x1 + 0.1, x1 - 0.35, 1)):
        netz.vieleck_nach([(x, y0 - 0.03, za), (x, y1, za), (xo, y1, H_KUECHE), (xo, y1 - 0.35, H_KUECHE)], (s, -0.2, 0.3), st["Russputz"])
    alle, netz.alle = netz.alle, True
    netz.vieleck_nach([(x0 - 0.1, y0 - 0.03, za), (x1 + 0.1, y0 - 0.03, za), (x1 + 0.1, y1, za), (x0 - 0.1, y1, za)], (0, 0, -1), st["Russ"])
    netz.alle = alle
    # Kesselhaken an einer Stange quer unter dem Rauchfang
    netz.stab((x0 + 0.1, p.y, za - 0.08), (x1 - 0.1, p.y, za - 0.08), 0.04, 0.04, st["Eisen"])
    for z in (za - 0.1, 1.55, 1.35):
        netz.stab((p.x, p.y, z), (p.x, p.y, z - 0.18), 0.03, 0.015, st["Eisen"], quer=(1, 0, 0) if z > 1.5 else (0, 1, 0))
    # Kochtopf (Kupfer, Rand in 0,95 m) mit Bügel
    drehkoerper(netz, st, p, [(0.52, 0.18), (0.58, 0.28), (0.8, 0.31), (0.95, 0.3), (0.97, 0.32), (0.97, 0.29)], "Kupferblech", n=12)
    drehkoerper(netz, st, p, [(0.8, 0.29), (0.97, 0.29)], "Kupferblech", n=12, innen=True)
    scheibe(netz, st, p, 0.29, 0.8, "Kohle", n=12)
    for s in (-1, 1):
        netz.stab(p + Vector((s * 0.32, 0, 0.93)), p + Vector((s * 0.12, 0, 1.2)), 0.025, 0.015, st["Eisen"], quer=(0, 1, 0))
    netz.stab(p + Vector((-0.13, 0, 1.2)), p + Vector((0.13, 0, 1.2)), 0.025, 0.015, st["Eisen"], quer=(0, 1, 0))
    for k, x in enumerate((8.05, 8.3, 9.55, 9.75)):                                       # Kellen und Pfannen am Mantel
        netz.stab((x, y0 - 0.05, za - 0.12), (x, y0 - 0.05, za - 0.55 - 0.08 * (k % 2)), 0.02, 0.02, st["Eisen"])
        if k % 2:
            drehkoerper(netz, st, Vector((x, y0 - 0.06, za - 0.75)), [(0.0, 0.0), (0.0, 0.12), (0.04, 0.13)], "Eisen", n=8)
    werkstueck = bs.Netz("Werkstueck_3")
    scheibe(werkstueck, st, p, 0.3, 0.85, "Fluessigkeit", n=12)
    return am_ort(werkstueck, p + Vector((0, 0, 0.85))), p + Vector((0, 0, 0.97))


def brennblase(st, anker):
    """Ab Stufe 3: die Brennblase für den Schnaps, gebaut wie beim Parfumeur (parfumeur_innen.destille: Herd mit Feuertür, Blase,
    Helm, Kühlrohr, Vorlage auf dem Schemel) – hier vorn rechts in der Küche, die Figur blickt nach +x. Gibt (Netz, Werkstueck_4,
    Tropfen) zurück."""
    alt = {k: getattr(pi, k) for k in ("W2", "HERD", "BLASE", "VORLAGE", "STUTZEN")}
    pi.W2, pi.HERD, pi.BLASE, pi.VORLAGE, pi.STUTZEN = W4, BRENNHERD, BLASE, VORLAGE, STUTZEN
    try:
        netz = bs.Netz("Brennblase")
        werkstueck, tropfen, _ = pi.destille(netz, st, anker)
    finally:
        for k, v in alt.items():
            setattr(pi, k, v)
    werkstueck.name = "Werkstueck_4"
    werkstueck.data.name = "Werkstueck_4"
    for c in werkstueck.children:
        if c.name.startswith("Destillat"):
            c.name = c.data.name = "Destillat_4"
    return netz, werkstueck, tropfen


def vorraete(netz, st):
    """Küchenkram: Hackklotz mit Beil, Anrichte mit Brot, Zwiebeln und Messer, Malzsäcke, Brennholz; an Haken unter einem
    Deckenbalken Schinken und Zwiebelzöpfe."""
    hk = Vector((7.2, 0.5, 0))
    bs.fass(netz, st, hk, r=0.3, h=0.72, reifen=(), stoff="Holz alt")
    bs.rund_oben(netz, st, hk, 0.27, 0.721, "Schnittholz", n=8)
    netz.stab(hk + Vector((0.05, -0.05, 0.71)), hk + Vector((0.2, -0.15, 1.05)), 0.035, 0.035, st["Holz"])        # Beil
    netz.quader(hk + Vector((0.04, -0.04, 0.73)), (0.14, 0.02, 0.1), st["Eisen"], Matrix.Rotation(0.6, 4, "Z"))
    M = si.rahmen(5.6, KY0, 0)                                                            # Anrichte an der Vorderwand
    si.q(netz, st, M, (0, 0.33, 0.86), (1.4, 0.62, 0.06), "Tischplatte")
    for x in (-0.62, 0.62):
        si.q(netz, st, M, (x, 0.33, 0.42), (0.07, 0.55, 0.83), "Holz")
    si.q(netz, st, M, (0, 0.33, 0.2), (1.24, 0.55, 0.03), "Holz alt")
    for k in range(3):
        drehkoerper(netz, st, M @ Vector((-0.45 + 0.2 * k, 0.35, 0.89)), [(0.0, 0.08), (0.04, 0.09), (0.08, 0.06), (0.1, 0.0)], "Brot", n=8)
    zufall = random.Random(4)
    for k in range(6):
        p = M @ Vector((0.15 + zufall.uniform(0, 0.4), 0.25 + zufall.uniform(0, 0.2), 0.89))
        drehkoerper(netz, st, p, [(0.0, 0.02), (0.03, 0.035), (0.06, 0.02), (0.08, 0.0)], "Zwiebel", n=6)
    netz.quader(M @ Vector((0.3, 0.5, 0.895)), (0.3, 0.03, 0.01), st["Eisen"])
    for k, (x, y) in enumerate(((5.25, 3.55), (5.25, 2.95), (5.75, 3.6))):               # Malzsäcke an der Rückwand
        drehkoerper(netz, st, Vector((x, y, 0)), [(0.0, 0.2), (0.25, 0.26), (0.5, 0.22), (0.62, 0.1), (0.7, 0.05)], "Sack", n=8)
    for lage in range(4):                                                                 # Brennholz neben dem Herd
        for k in range(3 - lage // 2):
            y = 3.25 + 0.14 * k + 0.07 * (lage % 2)
            netz.stab((9.95, y, 0.07 + 0.13 * lage), (10.25, y, 0.07 + 0.13 * lage), 0.13, 0.13, st["Scheitholz"])
    alle, netz.alle = netz.alle, True
    y = 1.25
    netz.stab((5.4, y, 2.6), (9.6, y, 2.6), 0.05, 0.05, st["Holz alt"])
    for k, x in enumerate((5.8, 6.4, 7.0, 7.7, 8.3, 9.0)):
        netz.stab((x, y, 2.62), (x, y, H_KUECHE - 0.26), 0.012, 0.012, st["Seil"])
        if k % 2 == 0:                                                                    # Schinken
            drehkoerper(netz, st, Vector((x, y, 2.16)), [(0.0, 0.045), (0.05, 0.1), (0.22, 0.11), (0.34, 0.06), (0.42, 0.025)], "Schinken",
                        n=8)
            scheibe(netz, st, (x, y), 0.045, 2.16, "Schinken hell", n=8, unten=True)
            netz.stab((x, y, 2.58), (x, y, 2.6), 0.03, 0.03, st["Seil"])
        else:                                                                             # Zwiebelzopf
            for j in range(7):
                p = Vector((x + 0.03 * (j % 2) - 0.015, y + 0.02 * ((j + 1) % 2), 2.5 - 0.07 * j))
                drehkoerper(netz, st, p - Vector((0, 0, 0.04)), [(0.0, 0.01), (0.03, 0.038), (0.06, 0.02), (0.08, 0.0)], "Zwiebel", n=6)
            netz.stab((x, y, 2.55), (x, y, 2.04), 0.03, 0.02, st["Stroh"])
    netz.alle = alle


def braupfanne(netz, st):
    """Gekauft: die kupferne Braupfanne auf einem gemauerten Sockel neben dem Braukessel, darin ein Rest Würze, ein Schöpfer."""
    m = Vector((7.3, 3.45, 0))
    drehkoerper(netz, st, m, [(0.0, 0.42), (0.42, 0.42)], "Ziegelmauer", n=10)
    drehkoerper(netz, st, m, [(0.42, 0.38), (0.5, 0.46), (0.62, 0.5), (0.64, 0.53), (0.64, 0.5)], "Kupferblech", n=14)
    drehkoerper(netz, st, m, [(0.5, 0.49), (0.64, 0.5)], "Kupferblech", n=14, innen=True)
    scheibe(netz, st, m, 0.47, 0.52, "Malz", n=14)
    netz.stab(m + Vector((0.2, -0.1, 0.55)), m + Vector((0.55, -0.3, 0.95)), 0.03, 0.03, st["Holz"])
    drehkoerper(netz, st, m + Vector((0.18, -0.08, 0.5)), [(0.0, 0.0), (0.0, 0.07), (0.05, 0.08)], "Kupferblech", n=8)


def werkzeug_kueche(netz, st, M, stufe):
    """Gekauft an der Werkzeugwand: Gutes Werkzeug (Stufe 1: Kellen, Siebe, Trichter, Messer) bzw. Meisterwerkzeug (Stufe 2:
    Zapfhähne aus Messing, Fassbohrer, Spundhammer, Hopfensieb)."""
    for k, (x, z) in enumerate(si.HAKEN):
        if (x < 0) != (stufe == 1):
            continue
        a = M @ Vector((x, 0.12, z + 0.02))
        unten = M.to_3x3() @ Vector((0, 0, -1))
        vorn = M.to_3x3() @ Vector((0, 1, 0))
        art = k % 4
        if stufe == 1 and art in (0, 2):                                                  # Kelle bzw. Schaumkelle
            netz.stab(a, a + unten * 0.34, 0.02, 0.012, st["Holz" if art else "Eisen"], quer=vorn)
            drehkoerper(netz, st, a + unten * 0.42, [(0.0, 0.0), (0.0, 0.07), (0.05, 0.07)], "Eisen" if art else "Kupferblech", n=7)
        elif stufe == 1:                                                                  # Messer
            netz.stab(a, a + unten * 0.12, 0.03, 0.02, st["Holz alt"], quer=vorn)
            netz.stab(a + unten * 0.12, a + unten * 0.32, 0.035, 0.004, st["Eisen"], quer=vorn)
        elif art in (0, 2):                                                               # Zapfhahn aus Messing
            netz.stab(a, a + unten * 0.18, 0.035, 0.035, st["Messing"])
            netz.stab(a + unten * 0.1, a + unten * 0.1 + vorn * 0.06, 0.04, 0.012, st["Messing"], quer=unten)
        else:                                                                             # Spundhammer bzw. Fassbohrer
            netz.stab(a, a + unten * 0.32, 0.025, 0.025, st["Holz"])
            netz.stab(a + unten * 0.32 - vorn * 0.05, a + unten * 0.32 + vorn * 0.05, 0.07, 0.07, st["Holz alt" if art == 1 else "Eisen"])


def handschuhe(netz, st, M):
    """Gekauft: zwei Paar Lederhandschuhe an Haken (wie beim Parfumeur, an der Wand bei M)."""
    si.q(netz, st, M, (0, 0.03, 1.85), (0.62, 0.06, 0.07), "Fachwerk", ohne=("-y",))
    for k, dx0 in enumerate((-0.16, 0.16)):
        netz.stab(M @ Vector((dx0, 0.03, 1.8)), M @ Vector((dx0, 0.12, 1.82)), 0.02, 0.02, st["Eisen"])
        for j, dx in enumerate((-0.05, 0.05)):
            unten = 1.36 - 0.04 * ((k + j) % 2)
            ecken_ = [(dx0 + dx - 0.05, 0.1, 1.78), (dx0 + dx + 0.05, 0.1, 1.78), (dx0 + dx + 0.07, 0.09 + 0.02 * j, unten),
                      (dx0 + dx - 0.06, 0.09 + 0.02 * j, unten)]
            netz.vieleck_nach([M @ Vector(p) for p in ecken_], M.to_3x3() @ Vector((0, 1, 0)), st["Leder"])
            netz.vieleck_nach([M @ (Vector(p) - Vector((0, 0.01, 0))) for p in ecken_], M.to_3x3() @ Vector((0, -1, 0)), st["Leder"])


# ─────────────────────────────────────────────────────────────────────────────
#  Fasskeller
# ─────────────────────────────────────────────────────────────────────────────

def gewoelbe_z(y):
    """Höhe des Tonnengewölbes (Stichbogen über die Breite des Kellers) über dem Kellerboden an der Stelle y."""
    w = (CY1 - CY0) / 2
    r = (w * w + STICH * STICH) / (2 * STICH)
    ym = (CY0 + CY1) / 2
    return KAEMPFER + STICH - r + math.sqrt(max(0.0, r * r - (y - ym) ** 2))


def gewoelbe(decke, st, saat=0):
    """Decke_Lager: das Tonnengewölbe aus Bruchstein, Streifen entlang x, je Stein eine eigene Farbe."""
    zufall = random.Random(saat)
    n = 14
    ys = [CY0 + (CY1 - CY0) * k / n for k in range(n + 1)]
    for i in range(n):
        y0, y1 = ys[i], ys[i + 1]
        z0, z1 = Z_KELLER + gewoelbe_z(y0), Z_KELLER + gewoelbe_z(y1)
        x = CX0
        versatz = 0.25 * (i % 2)
        while x < CX1 - 1e-4:
            xe = min(CX1, x + zufall.uniform(0.35, 0.6) + (versatz if x == CX0 else 0.0))
            if CX1 - xe < 0.15:
                xe = CX1
            normale = Vector((0, -(z1 - z0), y1 - y0)).normalized() * -1
            decke.vieleck_nach([(x, y0, z0), (xe, y0, z0), (xe, y1, z1), (x, y1, z1)], normale, st[zufall.choice(ia.STEINE)])
            x = xe
        decke.vieleck_nach([(CX0, y0, z0 + 0.012), (CX1, y0, z0 + 0.012), (CX1, y1, z1 + 0.012), (CX0, y1, z1 + 0.012)],
                           Vector((0, -(z1 - z0), y1 - y0)).normalized() * -1, st["Moertel"])


def stirnwand(netz, st, x, innen, loch=None, schacht=None):
    """Stirnwand des Kellers bei x (Innenseite in Richtung `innen` = ±1) bis unter das Gewölbe, mit einer Türöffnung `loch` =
    (y0, y1, Höhe) samt Sturz aus Sandstein bzw. einem Lichtschacht `schacht` = (y0, y1, z0, z1) oben."""
    n = 14
    ys = sorted(set([CY0 + (CY1 - CY0) * k / n for k in range(n + 1)] + ([loch[0], loch[1]] if loch else [])
                    + ([schacht[0], schacht[1]] if schacht else [])))
    m = st["Bruchstein"]
    for y0, y1 in zip(ys, ys[1:]):
        z0, z1 = gewoelbe_z(y0), gewoelbe_z(y1)
        stuecke = [(0.0, 0.0, z0, z1)]                                                    # (unten links, unten rechts, oben links, oben rechts)
        if loch and loch[0] - 1e-4 <= y0 and y1 <= loch[1] + 1e-4:
            stuecke = [(loch[2], loch[2], z0, z1)]
        if schacht and schacht[0] - 1e-4 <= y0 and y1 <= schacht[1] + 1e-4:
            stuecke = [(0.0, 0.0, schacht[2], schacht[2]), (schacht[3], schacht[3], z0, z1)]
        for u0, u1, o0, o1 in stuecke:
            if o0 - u0 > 1e-3 or o1 - u1 > 1e-3:
                netz.vieleck_nach([(x, y0, Z_KELLER + u0), (x, y1, Z_KELLER + u1), (x, y1, Z_KELLER + o1), (x, y0, Z_KELLER + o0)],
                                  (innen, 0, 0), m)
    if loch:
        y0, y1, h = loch
        netz.quader((x - innen * 0.06, (y0 + y1) / 2, Z_KELLER + h + 0.1), (0.5, y1 - y0 + 0.3, 0.2), st["Sandstein"], ohne=("-z",))
        for y in (y0 - 0.08, y1 + 0.08):
            netz.quader((x + innen * 0.02, y, Z_KELLER + h / 2), (0.05, 0.16, h), st["Sandstein"])
        for y, s in ((y0, 1), (y1, -1)):                                                  # Laibung
            netz.vieleck_nach([(x, y, Z_KELLER), (x - innen * 0.5, y, Z_KELLER), (x - innen * 0.5, y, Z_KELLER + h), (x, y, Z_KELLER + h)],
                              (0, s, 0), st["Bruchstein dunkel"])
    if schacht:
        y0, y1, z0, z1 = schacht                                                          # schräger Schacht nach oben zum Licht
        tief = 0.7
        netz.vieleck_nach([(x, y0, Z_KELLER + z0), (x, y1, Z_KELLER + z0), (x - innen * tief, y1, Z_KELLER + z1 + 0.3),
                           (x - innen * tief, y0, Z_KELLER + z1 + 0.3)], (innen, 0, 1), st["Bruch hell"])
        for y, s in ((y0, 1), (y1, -1)):
            netz.vieleck_nach([(x, y, Z_KELLER + z0), (x - innen * tief, y, Z_KELLER + z1 + 0.3), (x, y, Z_KELLER + z1)], (0, s, 0),
                              st["Bruch"])
        netz.vieleck_nach([(x - innen * tief, y0, Z_KELLER + z1 + 0.3), (x - innen * tief, y1, Z_KELLER + z1 + 0.3),
                           (x - innen * tief * 0.3, y1, Z_KELLER + z1 + 0.08), (x - innen * tief * 0.3, y0, Z_KELLER + z1 + 0.08)],
                          (innen, 0, -0.5), st["Fenster"])
        for k in (0.33, 0.66):
            y = y0 + (y1 - y0) * k
            netz.stab((x - innen * 0.05, y, Z_KELLER + z0 + 0.05), (x - innen * 0.05, y, Z_KELLER + z1 - 0.02), 0.025, 0.025, st["Eisen"])


def kellerfaesser(netz, st):
    """Bedienung_Lager_Faesser: große Fässer liegend auf ihren Lagern an der hinteren Längswand, die Hähne zum Gang; ein
    stehendes Fass mit Kreidestrichen, Schläuche und ein Trichter."""
    for k, x in enumerate((-1.75, -0.75, 0.25, 1.25, 2.25)):
        pi.fass_liegend(netz, st, Vector((x, CY1 - 0.55, Z_KELLER + 0.5)), r=0.4 - 0.04 * (k % 2), laenge=0.9)
    bs.fass(netz, st, Vector((3.0, 3.9, Z_KELLER)), r=0.3, h=0.85, reifen=(0.12, 0.66))
    bs.rund_oben(netz, st, Vector((3.0, 3.9)), 0.28, Z_KELLER + 0.851, "Holz alt")
    for k in range(4):                                                                    # Kreidestriche am Fass
        netz.vieleck_nach([(2.69, 3.84 + 0.04 * k, Z_KELLER + 0.4), (2.69, 3.86 + 0.04 * k, Z_KELLER + 0.4),
                           (2.69, 3.86 + 0.04 * k, Z_KELLER + 0.55), (2.69, 3.84 + 0.04 * k, Z_KELLER + 0.55)], (-1, 0, 0), st["Wuerfel"])


def fassregale(netz, st):
    """Gekauft: Fassregale an der vorderen Längswand – zwei Reihen kleiner Fässchen auf Kanthölzern übereinander."""
    x0, x1 = -1.9, 2.9
    y = CY0 + 0.32
    for z in (0.0, 0.62):
        for dy in (-0.18, 0.18):
            netz.quader(((x0 + x1) / 2, y + dy, Z_KELLER + z + 0.05), (x1 - x0, 0.08, 0.1), st["Holz alt"])
        for k in range(7):
            x = x0 + 0.35 + k * (x1 - x0 - 0.7) / 6
            pi.fass_liegend(netz, st, Vector((x, y, Z_KELLER + z + 0.1 + 0.22)), r=0.21, laenge=0.48, hahn=k % 2 == 0)
    for x in (x0 + 0.05, (x0 + x1) / 2, x1 - 0.05):
        for dy in (-0.18, 0.18):
            netz.quader((x, y + dy, Z_KELLER + 0.6), (0.08, 0.1, 1.2), st["Holz"])


def keller_kram(netz, st):
    """Säcke, Kisten und ein Laternenhaken im Fasskeller."""
    for k, (x, y) in enumerate(((3.0, 1.0), (2.6, 1.05), (3.05, 1.45))):
        drehkoerper(netz, st, Vector((x, y, Z_KELLER)), [(0.0, 0.2), (0.25, 0.26), (0.5, 0.22), (0.6, 0.1), (0.68, 0.05)], "Sack", n=8)
    si.kisten(netz, st, Vector((2.95, 2.65, Z_KELLER)))


# ─────────────────────────────────────────────────────────────────────────────
#  Saal
# ─────────────────────────────────────────────────────────────────────────────

def saal_huelle(netz, decke, st, anker):
    """Wände des Saals (Kalkputz über einer Holztäfelung, ohne Außenseite: darunter liegt die Schankstube), Butzenscheiben mit
    Lichtern, Balkendecke; die Tür zur Treppe in der Rückwand."""
    kanten = ecken(WX0, WX1, WY0, WY1)
    oeffnungen = {s: list(SAAL_FENSTER.get(s, [])) for s in ("vorne", "rechts", "hinten", "links")}
    oeffnungen["hinten"].append(Oe(SAALTUER_X, 0.0, TUER_B, TUER_H))

    def waende(hilf):
        hilf.alle = True
        for seite in ("vorne", "rechts", "hinten", "links"):
            si.wand(hilf, st, *kanten[seite], H_SAAL, oeffnungen[seite], stoff="Kalkputz", dicke=WAND, aussen=False)
            pi.taefelung(hilf, st, *kanten[seite], oeffnungen[seite], hoehe=1.1)
    versetzt(netz, st, waende, Z_SAAL)
    for seite, liste in SAAL_FENSTER.items():
        for o in liste:
            wand = ia.Wand(*kanten[seite], [o], boden=Z_SAAL)
            pi.butzenfenster(netz, st, wand, o)
            p = wand.p(wand.ops[0][0], o.v + o.h / 2, 0.1)
            anker.append(licht(p, (wand.n.x, wand.n.y, 0), "Fenster", 3))
    ia.balkendecke(decke, st, WX0, WX1, WY0, WY1, Z_SAAL + H_SAAL, abdeckung=WAND, saat=31)


def stammtisch(netz, st, anker):
    """Der lange Stammtisch mit zwei Bänken; Gast_Stamm_1 … 6 (je drei an einer Seite) mit Krug_Stamm_<n>; darauf ein Zinnkrug,
    Teller und ein Tischzeichen (geschnitzter Krug auf einem Stab)."""
    cx, cy, laenge = 0.4, 0.2, 3.6
    z = Z_SAAL
    tisch(netz, st, Vector((cx, cy, z)), laenge, 0.9, hoehe=0.78)
    for s in (-1, 1):
        bank(netz, st, (cx - laenge / 2, cy + s * 0.62, z), (cx + laenge / 2, cy + s * 0.62, z))
    nummer = 1
    for s, blick in ((1, (0, -1)), (-1, (0, 1))):
        for dx in (-1.2, 0.0, 1.2):
            punkt = Vector((cx + dx * s, cy + s * 0.62, z))
            anker.append(gast(f"Gast_Stamm_{nummer}", punkt, blick, stufe=3))
            anker.append(krug_platz(f"Krug_Stamm_{nummer}", punkt, blick, stufe=3))
            nummer += 1
    for dx in (-0.6, 0.6):
        scheibe(netz, st, (cx + dx, cy), 0.12, z + 0.781, "Zinn", n=10)
    zeichen = Vector((cx, cy, z + 0.78))
    netz.stab(zeichen, zeichen + Vector((0, 0, 0.45)), 0.02, 0.02, st["Eisen"])
    krug(netz, st, zeichen + Vector((0, 0, 0.45)), r=0.06, h=0.18, henkel=(1, 0), voll=False)


def buehne(netz, st, anker):
    """Kleine Bühne für den Barden hinten links: Podest aus Dielen mit zwei Stufen, dahinter der weinrote Wandbehang mit
    goldener Borte und einem Krug; Barde_1 steht darauf und blickt in den Saal."""
    x0, x1, y0, y1, h = -4.4, -1.9, 2.5, WY1 - 0.03, 0.35
    z = Z_SAAL
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, z + h / 2), (x1 - x0, y1 - y0, h), st["Buehne"], ohne=("-z", "+y"))
    for k in range(int((x1 - x0) / 0.3)):                                                 # Bretterfugen
        x = x0 + 0.3 * (k + 1)
        netz.vieleck_nach([(x - 0.006, y0, z + h + 0.002), (x + 0.006, y0, z + h + 0.002), (x + 0.006, y1, z + h + 0.002),
                           (x - 0.006, y1, z + h + 0.002)], (0, 0, 1), st["Dielenfuge"])
    for hh, tief, b in ((0.12, 0.6, 1.0), (0.24, 0.3, 0.96)):                              # Stufen vorn
        netz.quader(((x0 + x1) / 2 + 0.3, y0 - tief / 2, z + hh / 2), (b, tief, hh), st["Holz alt"], ohne=("-z", "+y"))
    M = si.rahmen((x0 + x1) / 2, WY1, 180, z)
    si.q(netz, st, M, (0, 0.025, 1.6), (1.9, 0.02, 1.9), "Wandbehang", ohne=("-y",))
    for v in (0.68, 2.52):
        si.q(netz, st, M, (0, 0.04, v), (1.9, 0.012, 0.08), "Wandbehang hell", ohne=("-y",))
    netz.stab(M @ Vector((-1.0, 0.04, 2.58)), M @ Vector((1.0, 0.04, 2.58)), 0.035, 0.035, st["Holz"])
    n = M.to_3x3() @ Vector((0, 1, 0))
    umriss = [(-0.2, 1.2), (0.16, 1.2), (0.2, 1.26), (0.2, 1.86), (0.24, 1.92), (-0.22, 1.92), (-0.18, 1.86)]
    netz.vieleck_nach([M @ Vector((x, 0.041, zz)) for x, zz in umriss], n, st["Wandbehang hell"])
    netz.vieleck_nach([M @ Vector((x, 0.046, zz)) for x, zz in ((0.2, 1.36), (0.38, 1.4), (0.38, 1.7), (0.2, 1.74), (0.2, 1.66),
                                                                 (0.3, 1.64), (0.3, 1.46), (0.2, 1.44))], n, st["Wandbehang hell"])
    barde = bs.anker("Barde_1", ((x0 + x1) / 2 - 0.1, (y0 + y1) / 2 + 0.05, z + h), si.drehung_von((0.35, -1)))
    barde["stufe"] = 3
    anker.append(barde)
    drehkoerper(netz, st, Vector((x1 - 0.35, y1 - 0.3, z + h)), [(0.0, 0.17), (0.4, 0.15), (0.4, 0.2), (0.45, 0.2)], "Holz alt", n=8)
    krug(netz, st, Vector((x1 - 0.35, y1 - 0.3, z + h + 0.45)), henkel=(-1, 0), voll=False)


def radleuchter(netz, st, anker, mitte, hoehe):
    """Radleuchter: eiserner Ring an vier Ketten, darauf acht Kerzen mit Flammen („Glut“) – wie im Pavillon des Parfumeurs."""
    e = st["Eisen"]
    alle, netz.alle = netz.alle, True
    n = 16
    for k in range(n):
        a, b = (mitte + Vector((0.55 * math.cos(2 * math.pi * j / n), 0.55 * math.sin(2 * math.pi * j / n), 0)) for j in (k, k + 1))
        netz.stab(a, b, 0.05, 0.03, e, quer=(0, 0, 1))
    for k in range(4):
        w = 2 * math.pi * k / 4 + math.pi / 4
        p = mitte + Vector((0.55 * math.cos(w), 0.55 * math.sin(w), 0))
        netz.stab(p, mitte + Vector((0, 0, 0.7)), 0.012, 0.012, e)
    netz.stab(mitte + Vector((0, 0, 0.7)), Vector((mitte.x, mitte.y, hoehe)), 0.015, 0.015, e)
    for k in range(8):
        w = 2 * math.pi * k / 8
        p = mitte + Vector((0.55 * math.cos(w), 0.55 * math.sin(w), 0.015))
        drehkoerper(netz, st, p, [(0.0, 0.035), (0.02, 0.035), (0.02, 0.018), (0.16, 0.018)], "Kerze", n=6)
        drehkoerper(netz, st, p + Vector((0, 0, 0.16)), [(0.0, 0.0), (0.01, 0.012), (0.03, 0.01), (0.06, 0.0)], "Glut", n=5)
    netz.alle = alle
    anker.append(licht(mitte + Vector((0, 0, 0.2)), (0, 0, 1), "Glut", 3))


def saal_kram(netz, st):
    """Anrichte an der rechten Wand mit Zinnkrügen und Tellern, eine Truhe."""
    M = si.rahmen(WX1, -1.2, 90, Z_SAAL)
    si.q(netz, st, M, (0, 0.25, 0.45), (1.6, 0.5, 0.9), "Taeferholz dunkel", ohne=("-y",))
    si.q(netz, st, M, (0, 0.27, 0.92), (1.7, 0.56, 0.05), "Tischplatte", ohne=("-y",))
    for k in range(4):
        krug(netz, st, M @ Vector((-0.6 + 0.4 * k, 0.28, 0.945)), r=0.05, h=0.17, henkel=(0, -1), voll=False)
    M = si.rahmen(-3.6, WY0, 0, Z_SAAL)
    si.q(netz, st, M, (0, 0.3, 0.3), (0.9, 0.5, 0.6), "Holz alt", ohne=("-y",))
    for x in (-0.35, 0.35):
        si.q(netz, st, M, (x, 0.3, 0.3), (0.06, 0.52, 0.62), "Eisen", ohne=("-y",))


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen
# ─────────────────────────────────────────────────────────────────────────────

def bauen():
    import bpy
    bs.neue_szene()
    ia.RUSS_QUELLEN.clear()
    ti._lichter[0] = 0
    st = bs.Stoffe()
    werk, kueche = bs.Netz("Raum_Werk"), bs.Netz("Raum_Kueche")
    lager, saal = bs.Netz("Raum_Lager", alle=True), bs.Netz("Raum_Saal")
    decke_w, decke_k = bs.Netz("Decke_Werk", alle=True), bs.Netz("Decke_Kueche", alle=True)
    decke_l, decke_s = bs.Netz("Decke_Lager", alle=True), bs.Netz("Decke_Saal", alle=True)
    teile = {}                    # Name → (Netz, Raum)

    def teil(name, raum="Werk"):
        if name not in teile:
            teile[name] = (bs.Netz(name), raum)
        return teile[name][0]

    anker = []                    # (Objekt, Raum)
    fertig = []                   # schon gebaute Objekte (Werkstücke, bewegte Teile): (Objekt, Raum)

    def setzen(o, raum="Werk", stufe=None):
        if stufe is not None:
            o["stufe"] = stufe
        anker.append((o, raum))

    def alle_setzen(liste, raum="Werk", stufe=None):
        for o in liste:
            setzen(o, raum, stufe)

    # ── Schankstube: Hülle, Boden hinten, Fenster ────────────────────────────
    W = WERK_OEFFNUNGEN
    kanten = ecken(WX0, WX1, WY0, WY1)
    pi.huelle(werk, decke_w, st, WX0, WX1, WY0, WY1, H_WERK, {
        "vorne": dict(oeffnungen=W["vorne"]),
        "rechts": dict(oeffnungen=W["rechts"], aussen=False),
        "hinten": dict(oeffnungen=W["hinten"]),
        "links": dict(oeffnungen=W["links"]),
    }, oben=saal, saat=3)
    ti.fachwerk_innen(werk, st, WX0, WX1, WY0, WY1, H_WERK, W)
    liste = []
    ti.fenster_und_licht(werk, st, kanten, W, liste)
    alle_setzen(liste)
    dielen(werk, st, WX0, WX1, STREIFEN_Y, WY1, 0.0, saat=5, loch=LUKE)

    # ── Schankstube: Theke, Kamin, Tische, Würfeltisch ────────────────────────
    theke(teil("Bedienung_Lager_Theke"), st)
    kueken = hahn(st)
    werkstueck2, strahl = zapf_werkstueck(st)
    fertig += [(kueken, "Werk"), (werkstueck2, "Werk")]
    fasslager_hinten(werk, st)
    kranz(werk, st, Vector((-2.2, 1.55, 2.35)))
    liste = []
    kamin(werk, st, liste)
    tische_und_gaeste(werk, st, liste)
    alle_setzen(liste)
    liste = []
    wuerfel = wuerfeltisch(st, liste).objekt()
    wuerfel["stufe"] = 2
    fertig.append((wuerfel, "Werk"))
    alle_setzen(liste)
    for p in (Vector((-0.5, 0.6, 2.75)), Vector((-0.5, -2.15, 2.75))):                    # Laternen zwischen den Deckenbalken
        ti.laterne(werk, st, p, H_WERK - 0.26)
        setzen(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"))
    si.pult(teil("Bedienung_Personal_Buch"), st, si.rahmen(WX1 - 0.05, 2.95, 90))
    M_haus = si.rahmen(HAUSTUER_X, WY0, 0)
    si.bretttuer(teil("Holztuer"), st, M_haus, HAUSTUER_B, HAUSTUER_H)
    si.eisentuer(teil("Einrichtung_Eisentuer"), st, M_haus, HAUSTUER_B, HAUSTUER_H)
    si.bretttuer(teil("Tuer_Werk_Kueche"), st, si.rahmen(WX1, KUECHENTUER_Y, 90), TUER_B, TUER_H, stoff="Brett")
    si.bretttuer(teil("Tuer_Kueche_Werk", "Kueche"), st, si.rahmen(KX0, KUECHENTUER_Y, -90), TUER_B, TUER_H, stoff="Brett")
    si.tuerstock(teil("Tuer_Kueche_Werk", "Kueche"), st, si.rahmen(KX0, KUECHENTUER_Y, -90), TUER_B, TUER_H)
    falltuer(teil("Tuer_Werk_Lager"), st)
    treppenhaus(teil("Tuer_Werk_Saal"), st, 0.0, True)
    setzen(si.arbeit("Arbeit_2_Zapfen", W2, (0, -1), 1))

    # ── Stufen (kumulativ) ────────────────────────────────────────────────────
    stufen = [bs.Netz(f"Stufe{k}") for k in (1, 2, 3)]
    ziegel = 0
    for k, netz in enumerate(stufen, 1):
        if k < 3:
            ia.lehmboden(netz, st, WX0, WX1, WY0, STREIFEN_Y, saat=6)
            pi.gefach(netz, st, *kanten["hinten"], W["hinten"][0])                      # Tür zur Saaltreppe erst ab Stufe 3
        else:
            ziegel = ia.fischgraet(netz, st, WX0, WX1, WY0, STREIFEN_Y, saat=6)
    stufen_obj = [s.objekt() for s in stufen]
    print(f"Backsteinpflaster: {ziegel} Ziegel")

    # ── Küche ─────────────────────────────────────────────────────────────────
    K = KUECHE_OEFFNUNGEN
    kanten_k = ecken(KX0, KX1, KY0, KY1)
    pi.huelle(kueche, decke_k, st, KX0, KX1, KY0, KY1, H_KUECHE, {
        "vorne": dict(oeffnungen=K["vorne"], enden=(False, True)),
        "rechts": dict(oeffnungen=K["rechts"]),
        "hinten": dict(oeffnungen=K["hinten"], enden=(True, False)),
        "links": dict(oeffnungen=K["links"], aussen=False, laibung=False),
    }, saat=8)
    ti.fachwerk_innen(kueche, st, KX0, KX1, KY0, KY1, H_KUECHE, K)
    liste = []
    ti.fenster_und_licht(kueche, st, kanten_k, K, liste)
    alle_setzen(liste, "Kueche")
    ziegel_k = ia.fischgraet(kueche, st, KX0, KX1, KY0, KY1, saat=12)
    liste = []
    werkstueck1, dampf1 = braukessel(teil("Bedienung_Produktion_Braukessel", "Kueche"), st, liste)
    werkstueck3, dampf2 = herd(teil("Bedienung_Produktion_Herd", "Kueche"), st, liste)
    alle_setzen(liste, "Kueche")
    werkstueck3["stufe"] = 2
    liste = []
    brenn, werkstueck4, tropfen = brennblase(st, liste)
    alle_setzen(liste, "Kueche", stufe=3)
    brenn_obj = brenn.objekt()
    brenn_obj["stufe"] = 3
    werkstueck4["stufe"] = 3
    fertig += [(werkstueck1, "Kueche"), (werkstueck3, "Kueche"), (werkstueck4, "Kueche"), (brenn_obj, "Kueche")]
    setzen(bs.anker("Dampf_1", dampf1), "Kueche")
    setzen(bs.anker("Dampf_2", dampf2), "Kueche", stufe=2)
    vorraete(kueche, st)
    M_wz = si.rahmen(KX0 + 0.06, -0.95, -90)
    si.werkzeugwand(teil("Bedienung_Einrichten_Werkzeugwand", "Kueche"), st, M_wz)
    werkzeug_kueche(teil("Einrichtung_Werkzeug1", "Kueche"), st, M_wz, 1)
    werkzeug_kueche(teil("Einrichtung_Werkzeug2", "Kueche"), st, M_wz, 2)
    braupfanne(teil("Einrichtung_Braupfanne", "Kueche"), st)
    handschuhe(teil("Einrichtung_Handschuhe", "Kueche"), st, si.rahmen(5.2, KY1 - 0.06, 180))
    si.loeschfass(teil("Einrichtung_Loeschfass", "Kueche"), st, Vector((7.6, -1.55, 0)))
    ti.hoftor(teil("Bedienung_Karren_Hoftor", "Kueche"), st, si.rahmen(KX1, HOFTOR_Y, 90), HOFTOR_B, HOFTOR_H)
    ti.laterne(kueche, st, Vector((7.0, 0.3, 2.75)), H_KUECHE - 0.26)
    setzen(licht(Vector((7.0, 0.3, 2.6)), (0, 0, -1), "Lampe"), "Kueche")
    setzen(si.arbeit("Arbeit_1_Ruehren", W1, (0, 1), 1), "Kueche")
    setzen(si.arbeit("Arbeit_3_Ruehren", W3, (0, 1), 2), "Kueche")
    setzen(si.arbeit("Arbeit_4_Destillieren", W4, (1, 0), 3), "Kueche")
    print(f"Backsteinpflaster Küche: {ziegel_k} Ziegel")

    # ── Fasskeller ────────────────────────────────────────────────────────────
    kanten_c = ecken(CX0, CX1, CY0, CY1)

    def kellerwaende(hilf):
        for seite in ("vorne", "hinten"):
            si.wand(hilf, st, *kanten_c[seite], KAEMPFER, stoff="Bruchstein", dicke=WAND, aussen=False, fugen=True,
                    saat=51 if seite == "vorne" else 52)
        ia.lehmboden(hilf, st, CX0, CX1, CY0, CY1, saat=41, flecken=8, halme=6)
    versetzt(lager, st, kellerwaende, Z_KELLER)
    stirnwand(lager, st, CX0, 1, loch=KELLERTUER)
    stirnwand(lager, st, CX1, -1, schacht=(2.6, 3.2, 1.55, 2.15))
    gewoelbe(decke_l, st, saat=43)
    treppe_und_schacht(teil("Tuer_Lager_Werk", "Lager"), st, oben=False)
    kellerfaesser(teil("Bedienung_Lager_Faesser", "Lager"), st)
    fassregale(teil("Einrichtung_Regale", "Lager"), st)
    keller_kram(lager, st)
    for x in (-1.5, 1.6):
        p = Vector((x, 2.9, Z_KELLER + 1.95))
        ti.laterne(lager, st, p, Z_KELLER + gewoelbe_z(2.9) + 0.02)
        setzen(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"), "Lager")
    setzen(licht(Vector((CX1 - 0.15, 2.9, Z_KELLER + 1.85)), (-1, 0, 0), "Fenster"), "Lager")
    nummer = 1
    for y in (3.5, 2.3):
        for x in (-1.7, -0.7, 0.3, 1.3):
            setzen(bs.anker(f"Lager_{nummer}", (x, y, Z_KELLER)), "Lager")
            nummer += 1

    # ── Saal (ab Stufe 3) ─────────────────────────────────────────────────────
    liste = []
    saal_huelle(saal, decke_s, st, liste)
    stammtisch(saal, st, liste)
    buehne(saal, st, liste)
    radleuchter(saal, st, liste, Vector((0.4, 0.2, Z_SAAL + 2.15)), Z_SAAL + H_SAAL)
    alle_setzen(liste, "Saal", stufe=3)
    saal_kram(saal, st)
    treppenhaus(teil("Tuer_Saal_Werk", "Saal"), st, Z_SAAL, False)

    # ── Blickpunkte ──────────────────────────────────────────────────────────
    for name, (auge, ziel) in BLICKE.items():
        raum = name.split("_")[1]
        setzen(si.blick(name, auge, ziel), raum, stufe=3 if raum == "Saal" else None)

    # ── Objekte, Zustände, Hierarchie ─────────────────────────────────────────
    raeume = {"Werk": werk.objekt(), "Kueche": kueche.objekt(), "Lager": lager.objekt(), "Saal": saal.objekt()}
    raeume["Saal"]["stufe"] = 3
    kinder = {r: [] for r in raeume}
    kinder["Werk"] += [decke_w.objekt()] + stufen_obj
    kinder["Kueche"].append(decke_k.objekt())
    kinder["Lager"].append(decke_l.objekt())
    kinder["Saal"].append(decke_s.objekt())
    for o, raum in fertig:
        kinder[raum].append(o)
    for name, (netz, raum) in teile.items():
        o = netz.objekt()
        kinder[raum].append(o)
        if name.startswith("Einrichtung_"):
            bs.zustand(o)
        if name in ("Tuer_Werk_Saal", "Tuer_Saal_Werk"):
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
    ti.animieren(zapf_bewegung(strahl, kueken) + pi.tropfen_bewegung(tropfen), ARBEIT_BILDER)
    bpy.context.view_layer.update()
    return list(raeume.values()) + [o for liste in kinder.values() for o in liste]


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def gast_marken(z, raeume):
    """Marken für die Plätze der Gäste und des Barden (blau, Pfeil = Blickrichtung) und die Krüge (weiß) im Grundriss."""
    import bpy
    stoffe = {"Gast": bs._hilfsstoff("_Marke_Gast", (0.2, 0.45, 1.0)), "Barde": bs._hilfsstoff("_Marke_Barde", (0.6, 0.3, 1.0)),
              "Krug": bs._hilfsstoff("_Marke_Krug", (0.95, 0.95, 0.95))}
    liste = []
    for o in list(bpy.context.scene.objects):
        art = o.name.split("_")[0]
        if o.type != "EMPTY" or art not in stoffe or ia.raum_von(o) not in raeume:
            continue
        netz = bs.Netz("_Marke_" + o.name)
        t = o.matrix_world
        p = t.translation.copy()
        p.z = z
        if art == "Krug":
            netz.quader(p, (0.1, 0.1, 0.05), stoffe[art])
        else:
            vor = t.to_3x3() @ Vector((0, 1, 0))
            quer = Vector((-vor.y, vor.x, 0))
            netz.vieleck_nach([p - quer * 0.17 - vor * 0.12, p + vor * 0.36, p + quer * 0.17 - vor * 0.12], (0, 0, 1), stoffe[art])
        liste.append(netz.objekt())
    return liste


def vorschau(schnell):
    """Bilder nach docs/modelle/: wirt_innen-blicke.png (jeder Blickpunkt in Stufe 3 mit aller Einrichtung, der Hahn offen),
    wirt_innen-stufen.png (Schankstube und Küche von oben je Stufe) und wirt_innen-grundriss.png (Erdgeschoss, Fasskeller und
    Saal mit den Ankern: Arbeitsplätze magenta, Stellplätze türkis, Werkstücke orange, Blickpunkte gelb, Gäste blau, Barde
    lila, Krüge weiß)."""
    import bpy
    szene, kam = ia.buehne(schnell, lampe=80, fuell=[(0, 0, H_WERK - 0.4, 170, 1), ((KX0 + KX1) / 2, (KY0 + KY1) / 2, H_KUECHE - 0.4, 130, 1),
                                                       ((CX0 + CX1) / 2, (CY0 + CY1) / 2, Z_KELLER + 2.0, 50, 1),
                                                       (0, 0.3, Z_SAAL + H_SAAL - 0.4, 160, 3)])
    szene.render.resolution_x, szene.render.resolution_y = 640, 360
    szene.render.resolution_percentage = 50 if schnell else 100
    szene.frame_set(1 + round(0.4 * ARBEIT_BILDER))                                     # Bier läuft
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
    eg = {"Werk", "Kueche"}
    von_oben((WX0 - WAND, KX1 + WAND, WY0 - WAND, WY1 + WAND))
    teile = []
    for k in (1, 2, 3):
        ia.zeigen(szene, k, False, decke=False, raeume=eg)
        teile.append(bs.rendern_einzeln(f"_{NAME}-stufe{k}", f"Stufe {k}: {STUFEN[k - 1]}"))
    print(bs.zusammensetzen(NAME + "-stufen", teile, spalten=3))
    teile = []
    for raeume, r, z, text in ((eg, (WX0 - WAND, KX1 + WAND, WY0 - WAND, WY1 + WAND), 6.0,
                                "Erdgeschoss (Schankstube, Küche), Stufe 3: Arbeit magenta, Werkstücke orange, Blick gelb, Gäste blau"),
                               ({"Lager"}, (CX0 - 1.8, CX1 + WAND, CY0 - WAND, CY1 + WAND), Z_KELLER + 2.0,
                                "Fasskeller (ohne Gewölbe): Stellplätze türkis, Blickpunkte gelb"),
                               ({"Saal"}, (WX0 - WAND, WX1 + WAND, WY0 - WAND, WY1 + WAND), Z_SAAL + 2.6,
                                "Saal (Stufe 3, ohne Decke): Stammtisch blau, Barde lila, Krüge weiß")):
        von_oben(r)
        liste = ia.marken(z, raeume) + gast_marken(z, raeume)
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
