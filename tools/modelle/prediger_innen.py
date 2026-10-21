"""Innenraum des Predigers: das Kirchenschiff (Hauptraum mit Bänken, Kanzel, Altar, Beichtstuhl), links die Sakristei
(Werkraum: Kerzen ziehen, Papier schöpfen), unter dem Chor die Krypta (Lager), ab der Stufe Dom rechts das Skriptorium und ab
der Stufe Kathedrale hinter der Sakristei die Schatzkammer; Stufen Kirche, Dom, Kathedrale.

    blender -b --python tools/modelle/prediger_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/prediger_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/innen/prediger_innen.glb und Bilder nach docs/modelle/ (prediger_innen-blicke.png: das Bild jedes
Blickpunkts, prediger_innen-stufen.png: Kirchenschiff und Sakristei von oben je Stufe, prediger_innen-grundriss.png:
Erdgeschoss, Krypta und die Räume ab Stufe 2 und 3 mit den Ankern).

Koordinaten wie beim Schmied (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte des
Kirchenschiffs auf dem Boden, das Portal vorn (−y), der Altar hinten. Außen ist die Kirche eine Saalkirche aus Haustein
(prediger.glb, Spitzbogenfenster, Rosette über dem Portal, Schiefer); innen gekalkter Putz über einem Sockel aus Haustein,
Steinplatten, Holzbalkendecken:
    Kirche      „Kirchenschiff“ innen x −3,6 … 3,6, y −4,6 … 5,6, 7,0 m hoch; Wände 0,6 m. Vorn das Portal mit Bogenfeld, darüber
                die Rosette, daneben Weihwasserbecken, Opferstock und Taufstein; vier Reihen Bänke links und rechts des
                Mittelgangs; links die Kanzel mit Treppe und Schalldeckel, rechts vorn der Beichtstuhl, rechts die Tür zum
                Skriptorium (ab Stufe 2) und die Treppe in die Krypta; hinten der Chor zwei Stufen hoch mit dem Altar, dem
                Kruzifix und dem Ewigen Licht, links die Tür zur Sakristei. Drei Spitzbogenfenster je Seite, zwei hinten, mit
                bunten Feldern; Dienste auf Konsolen tragen die Deckenbalken; Weihekreuze an den Wänden.
    Werk        „Sakristei“ links neben dem Chor: x −10,2 … −4,2, y −0,6 … 5,6, 3,8 m hoch. Hinten der Wachsherd mit dem Trog
                (Kerzen ziehen) und dem Rauchfang, links das Ziehgestell mit frischen Kerzen, in der Mitte die Papierbütte
                (schöpfen) mit Schöpfformen, Gautschstuhl und Lumpenkorb, unter der Decke Leinen mit Bögen; rechts Pult,
                Werkzeugwand und die Tür ins Kirchenschiff, vorn das Hoftor, hinten die vermauerte Tür zur Schatzkammer.
    Lager       „Krypta“ unter dem Chor: x −2,9 … 2,2, y 0,2 … 5,4, Boden in −2,9 m, Kreuzgratgewölbe auf zwei Säulen
                (Kämpfer 1,75 m, Scheitel 2,6 m über dem Boden) mit Gurtbögen; Grabplatten im Boden, eine Tumba, Kisten mit
                Kerzen und Papier; die Treppe kommt im Schacht rechts herunter und mündet hinten rechts durch einen Bogen.
    Skriptorium ab Stufe 2 rechts neben dem Schiff: x 4,2 … 9,2, y −1,4 … 4,0, 3,8 m hoch; Dielen, zwei Schreibpulte mit dem
                Licht von links (Fenster hinten), Bücherregale, Lesepult, Truhe.
    Schatzkammer ab Stufe 3 hinter der Sakristei: x −8,6 … −4,6, y 6,2 … 9,6; Kreuzgratgewölbe (Scheitel 3,3 m), Eisentruhe,
                Nischen mit Kelchen und Monstranz, der Arbeitstisch fürs Reliquiar, die vergitterte Tür.
Alle Räume sind rundum geschlossen, die Kamera steht im Raum.

Knoten im GLB – dieselben Regeln wie in den anderen Innenräumen (Raum_…, Decke_…, Stufe1–Stufe3, Bedienung_…, Tuer_…,
Einrichtung_…, Holztuer, Blick_…, Arbeit_…, Lager_…, Werkstueck_…, Licht_…, Dampf_…), dazu:
    Raum_Kirche                 Hauptraum (neuer Raumschlüssel „Kirche“), immer da; darin die Haustür (Holztuer bzw.
                                Einrichtung_Eisentuer im Portal) und die Stufen (Stufe1–3: in Stufe 1 die vermauerte Tür zum
                                Skriptorium, ab Stufe 2 ein Flügelaltar, in Stufe 3 vergoldet und dazu ein Radleuchter)
    Raum_Skriptorium            extras „stufe“ = 2 (neuer Raumschlüssel „Skriptorium“), ebenso Tuer_Kirche_Skriptorium,
                                Tuer_Skriptorium_Kirche und alle Anker darin
    Raum_Schatzkammer           extras „stufe“ = 3 (neuer Raumschlüssel „Schatzkammer“), ebenso Tuer_Werk_Schatzkammer (eine
                                eisenbeschlagene Tür vor der vermauerten Öffnung in der Sakristei), Tuer_Schatzkammer_Werk (die
                                vergitterte Tür) und alle Anker darin
    Tuer_Kirche_Lager           die Treppe im Schiff (Brüstung, obere Stufen), Tuer_Lager_Kirche die unteren Stufen im Schacht und
                                der Bogen in der Krypta; Tuer_Kirche_Werk, Tuer_Werk_Kirche die Tür zwischen Chor und Sakristei
    Glaeubig_1 … Glaeubig_16    Sitzplätze der Gläubigen auf den Bänken (Fußpunkt auf dem Boden unter der Hüfte, Blick −Z zum Altar,
                                extras „sitzhoehe“ = Oberkante der Sitzbank); Reihenfolge = Füllreihenfolge: vorn → hinten, in
                                jeder Reihe erst die beiden Plätze am Mittelgang, dann die außen
    Predigt_1                   Standort des Predigers auf der Kanzel (Fußpunkt auf ihrem Boden, Blick in das Schiff; Bewegung
                                „Predigen“, die Hände auf der Brüstung: figuren.KANZEL)
    Beichte_1                   kniender Platz am Beichtstuhl (Fußpunkt auf der Kniebank, Blick zum Sprechgitter; Bewegung
                                „Knien“)
    Werkstueck_1–4              Netze mit dem Material „Fluessigkeit“, das das Spiel in der Farbe der Ware färbt (Ursprung in der
                                Mitte): 1 das Wachs im Trog des Wachsherds, 2 der Brei in der Bütte, 3 das aufgeschlagene Buch auf
                                dem Schreibpult (stufe 2; gefärbt sind Einband, Schnitt und die Initiale), 4 das Reliquiar auf dem
                                Arbeitstisch (stufe 3; gefärbt sind die Emailfelder, das Gehäuse ist vergoldet)
    Dampf_1                     über dem Wachstrog
    Arbeit_1_Kerzenziehen, Arbeit_2_Schoepfen (Sakristei), Arbeit_3_Schreiben (stufe 2, Skriptorium, sitzend; extras
    „sitzhoehe“), Arbeit_4_Feinarbeit (stufe 3, Schatzkammer, sitzend): die Höhen passen zu figuren.KERZEN, BUETTE, SCHREIBEN
                                und ARBEITSHOEHEN („Tisch“)
    Kerzenstange_1 … 5          die Stäbe mit frischen Kerzen auf dem Ziehgestell (pendeln in der Animation)
    Tropfen_Kerzen_1 … 3, Tropfen_Sieb_1 … 4   Tropfen, die in der Animation fallen
Animation „Arbeit“ (4 s, als Schleife, solange gearbeitet wird; gleich lang wie die Bewegungen „Kerzenziehen“ und
„Schoepfen“): Die Stäbe auf dem Ziehgestell pendeln leicht, von ihren Kerzen fallen Wachstropfen auf das Tropfbrett; über der
Bütte fallen von 0,66 bis 0,98 der Schleife Tropfen aus dem Sieb, das die Figur am Platz 2 dann zum Abtropfen hält (im Spiel
im Takt des Raums: „Schoepfen“ 4,0 s). Bei Zeit 0 ist kein Tropfen zu sehen.
Lichter: extras „art“ = „Fenster“, „Lampe“ (Laternen) und „Glut“ (Altarkerzen, Ewiges Licht, Feuertür des Wachsherds, ab Stufe 3
der Radleuchter im Schiff).
Materialien: „Farben“ (Eckenfarben, Materialart als UV.y), „Fluessigkeit“ (Werkstücke), „Hausfarbe“ (Portal, Hoftor),
„Fenster“, „Glut“, „Eisen“, „Gold“, „Silber“, „Wasser“ (Taufstein, Weihwasser, Löschfass, Tropfen aus dem Sieb). Die bunten
Felder der Kirchenfenster sind Farben wenige Millimeter vor dem Fensterglas (sie leuchten nicht selbst).
Einrichtung_Gaensekiele: Gänsekiele und Lapislazuli stehen auf einem Farbtisch an der Rückwand der Sakristei (in jeder Stufe
gleich – das Skriptorium gibt es erst ab Stufe 2); Einrichtung_Regale: Bücherregale in der Krypta.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import alchimist_innen as ai  # noqa: E402  (Drehkörper, Scheiben, Ofen mit Feuertür, vermauerte Tür, Verkleinern)
import figuren as fg  # noqa: E402  (Wachstrog, Bütte, Schreibpult, Kanzel, Arbeitshöhen)
import innenausbau as ia  # noqa: E402  (Wand, Böden, Decke, Prüfbericht, Vorschau)
import parfumeur_innen as pi  # noqa: E402  (Lichter, Kinder, Wandquader, Täfelung, Butzenfenster, Handschuhe)
import prediger as pr  # noqa: E402  (Umriss des Spitzbogens wie außen)
import schmied_innen as si  # noqa: E402  (Pult, Türen, Werkzeugwand, Kisten, Löschfass, Anker, Blickpunkte)
import tischler_innen as ti  # noqa: E402  (Laterne, Hoftor, Animation, Export)
import wirt_innen as wi  # noqa: E402  (Netz versetzt bauen, Punkt am Platz, Sitzplätze mit Sitzhöhe, Radleuchter)

NAME = "prediger_innen"
STUFEN = ("Kirche", "Dom", "Kathedrale")

bs.FARBEN.update({
    "Platte": (0.60, 0.57, 0.51),             # Steinplatten (einzeln gelegt, ohne Struktur in den Oberflächendetails)
    "Platte hell": (0.66, 0.63, 0.56),
    "Platte dunkel": (0.52, 0.49, 0.44),
    "Platte warm": (0.63, 0.56, 0.46),
    "Plattenfuge": (0.38, 0.36, 0.33),
    "Gewoelbeputz": (0.74, 0.71, 0.64),       # gekalkte Kappen des Gewölbes
    "Weihrot": (0.60, 0.14, 0.12),            # Weihekreuze, Antependium
    "Scheibe rot": (0.66, 0.12, 0.10),        # bunte Felder der Kirchenfenster (vor dem Glas gemalt)
    "Scheibe blau": (0.16, 0.26, 0.62),
    "Scheibe gelb": (0.88, 0.68, 0.20),
    "Scheibe gruen": (0.22, 0.50, 0.26),
    "Scheibe violett": (0.42, 0.20, 0.52),
    "Altartuch": (0.90, 0.88, 0.82),
    "Corpus": (0.80, 0.68, 0.54),             # geschnitzter Leib am Kruzifix
    "Kirchenholz": (0.36, 0.24, 0.15),        # dunkles Eichenholz von Bänken, Kanzel, Beichtstuhl
    "Kirchenholz hell": (0.46, 0.32, 0.20),
    "Vorhang": (0.46, 0.12, 0.14),
    "Grabschrift": (0.32, 0.29, 0.25),        # eingetiefte Linien auf den Grabplatten
    "Tafelbild blau": (0.20, 0.30, 0.56),     # Flügelaltar
    "Tafelbild rot": (0.58, 0.16, 0.14),
    "Tafelbild gruen": (0.26, 0.42, 0.26),
    "Buchrot": (0.48, 0.16, 0.12),            # Einbände
    "Buchbraun": (0.40, 0.26, 0.15),
    "Buchgruen": (0.22, 0.34, 0.22),
    "Lapis": (0.12, 0.22, 0.58),              # Lapislazuli und Ultramarin
    "Papierbogen": (0.88, 0.85, 0.76),
    "Wachsblock": (0.86, 0.72, 0.40),
    "Federweiss": (0.92, 0.90, 0.84),
    "Tinte": (0.08, 0.07, 0.08),
})

# ── Maße ─────────────────────────────────────────────────────────────────────
WAND = 0.6                                                         # Haustein, innen gekalkt
WAND_S = 0.5                                                       # Außenwände der Anbauten
KX0, KX1, KY0, KY1 = -3.6, 3.6, -4.6, 5.6                          # Kirchenschiff innen
H_KIRCHE = 7.0
CHOR_X, CHOR_Y, CHOR_H = 2.5, 3.0, 0.30                            # Chor: x ±2,5 ab y 3,0, zwei Stufen zu 15 cm
SX0, SX1, SY0, SY1 = -10.2, KX0 - WAND, -0.6, KY1                  # Sakristei (Werkraum)
H_SAK = 3.8
CX0, CX1, CY0, CY1 = -2.9, 2.2, 0.2, 5.4                           # Krypta (Lager)
Z_KRYPTA = -2.9                                                    # Boden der Krypta
KAEMPFER, STICH = 1.75, 0.85                                       # Gewölbe: Kämpfer über dem Boden, Stich darüber
SAEULE_X, SAEULE_Y = -0.35, (CY0 + (CY1 - CY0) / 3, CY0 + 2 * (CY1 - CY0) / 3)
TR_X0, TR_X1, TR_Y0, TR_Y1 = 2.6, KX1, 0.9, 4.6                     # Treppe in die Krypta: im Schiff rechts, abwärts nach +y
TR_STUFEN = 15
PODEST_Y1 = 5.4                                                    # unten das Podest bis hier, von dort der Bogen in die Krypta
KRYPTATUER = (4.6, 5.25, KAEMPFER)                                 # Bogen in der rechten Wand der Krypta (y0, y1, Höhe)
RX0, RX1, RY0, RY1 = KX1 + WAND, 9.2, -1.4, 4.0                    # Skriptorium (ab Stufe 2)
H_SKRIPT = 3.8
TX0, TX1, TY0, TY1 = -8.6, -4.6, SY1 + WAND, 9.6                   # Schatzkammer (ab Stufe 3)
T_KAEMPFER, T_STICH = 2.3, 1.0
PORTAL_B, PORTAL_H = 1.6, 3.7                                      # Portal: Türflügel bis 2,7 m, darüber das Bogenfeld
ROSETTE = (5.4, 1.0)                                               # Mitte über dem Boden, Halbmesser des Glases
SAKTUER_Y, SKRIPTTUER_Y, SCHATZTUER_X = 4.4, -0.3, -6.0
TUER_B, TUER_H = 1.0, 2.2
HOFTOR_X, HOFTOR_B, HOFTOR_H = -8.6, 2.2, 2.5
KANZEL_M, KANZEL_Z = Vector((KX0 + 0.62, 1.2, 0)), 1.6             # Mitte des Kanzelkorbs, Höhe seines Bodens
KANZEL_BLICK = (1.0, -0.25)
FENSTER_Y = (-3.0, 0.0, 3.0)                                       # Spitzbogenfenster in beiden Langseiten
FENSTER_X = (-1.95, 1.95)                                          # … und hinten
FENSTER_V, FENSTER_B, FENSTER_H = 3.4, 1.0, 3.0                    # Glas: Unterkante, Breite, Scheitel darüber
DIENSTE_Y = (-1.5, 1.5, 4.5)                                       # Dienste auf Konsolen unter den Hauptbalken
FPS = 24
ARBEIT_BILDER = round(fg.BEWEGUNGEN["Schoepfen"] * FPS)            # Länge der Animation „Arbeit“ (4 s)

# Arbeitsplätze (Fußpunkt der Figur) und Geräte – die Höhen kommen aus figuren.py
W1 = Vector((-8.6, 4.95, 0))                    # Kerzen ziehen am Wachstrog, Blick −y
TROG = W1 + Vector((0, -fg.KERZEN["vor"], 0))   # Mitte des Trogs am Boden (lang entlang x)
W2 = Vector((-6.6, 3.12, 0))                    # Schöpfen an der Bütte, Blick −y
BUETTE = W2 + Vector((0, -fg.BUETTE["vor"], 0)) # Mitte der Bütte am Boden
W3 = Vector((5.8, 2.2, 0))                      # Schreiben am Pult im Skriptorium, Blick +x (das Licht kommt von links)
W4 = Vector((-6.8, 8.6, 0))                     # Feinarbeit am Reliquiar in der Schatzkammer, Blick −y
SITZ = 0.43                                     # Oberkante von Bänken und Hockern (wie beim Wirt)

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Kirche_0": ((0.0, -4.1, 2.7), (0.0, 4.8, 1.6)),            # den Mittelgang entlang zum Altar
    "Blick_Kirche_L1": ((2.3, -3.8, 2.9), (-3.4, 1.6, 1.8)),          # Bänke, Kanzel
    "Blick_Kirche_L2": ((1.8, 0.2, 2.7), (-3.4, 4.6, 1.3)),           # Chor links, Kanzeltreppe, Tür zur Sakristei
    "Blick_Kirche_R1": ((-2.4, -3.4, 2.7), (3.4, -0.6, 1.0)),         # Beichtstuhl, Tür zum Skriptorium, Treppe
    "Blick_Kirche_R2": ((0.6, 4.2, 3.0), (-0.4, -4.6, 2.2)),          # vom Chor zurück: Portal, Rosette, Taufstein
    "Blick_Werk_0": ((-5.0, -0.25, 2.5), (-8.0, 4.4, 0.9)),           # Bütte, Wachsherd
    "Blick_Werk_L1": ((-4.8, 1.6, 2.4), (-10.0, 4.4, 0.9)),           # Ziehgestell, Wachsherd
    "Blick_Werk_R1": ((-9.7, -0.1, 2.5), (-4.4, 3.4, 1.0)),           # Bütte, Werkzeugwand, Tür ins Schiff
    "Blick_Lager_0": ((1.7, 0.6, Z_KRYPTA + 1.75), (-2.4, 4.6, Z_KRYPTA + 0.6)),    # über die Stellplätze zur Tumba
    "Blick_Lager_L1": ((1.6, 4.9, Z_KRYPTA + 1.7), (-2.6, 0.8, Z_KRYPTA + 0.6)),    # Regale, Kisten
    "Blick_Lager_R1": ((-2.4, 1.0, Z_KRYPTA + 1.7), (2.1, 4.9, Z_KRYPTA + 0.6)),    # zum Bogen der Treppe
    "Blick_Skriptorium_0": ((8.3, -0.4, 2.3), (5.3, 2.7, 0.9)),       # Schreibpulte
    "Blick_Skriptorium_L1": ((8.7, 3.5, 2.4), (4.6, -1.0, 0.9)),      # Pult vorn, Tür ins Schiff
    "Blick_Skriptorium_R1": ((4.8, -1.0, 2.4), (9.1, 2.8, 1.0)),      # Bücherregale
    "Blick_Schatzkammer_0": ((-5.0, 6.5, 2.3), (-7.2, 9.4, 0.9)),     # Arbeitstisch, Nischen
    "Blick_Schatzkammer_L1": ((-4.9, 9.2, 2.3), (-8.4, 6.6, 0.8)),    # Eisentruhe, vergitterte Tür
}

Oe = si.Oe
SCHILDER = {"Tuer_Kirche_Lager": "Treppe in die Krypta", "Tuer_Lager_Kirche": "Treppe hinauf ins Kirchenschiff",
            "Tuer_Kirche_Werk": "Tür zur Sakristei", "Tuer_Werk_Kirche": "Tür ins Kirchenschiff",
            "Tuer_Kirche_Skriptorium": "Tür zum Skriptorium", "Tuer_Skriptorium_Kirche": "Tür ins Kirchenschiff",
            "Tuer_Werk_Schatzkammer": "Tür zur Schatzkammer", "Tuer_Schatzkammer_Werk": "Gittertür zur Sakristei"}

ecken = ia.ecken
drehkoerper, scheibe, am_ort = ai.drehkoerper, ai.scheibe, ai.am_ort
licht, kind, wq = pi.licht, pi.kind, pi.wq


# ─────────────────────────────────────────────────────────────────────────────
#  Hilfen: Wände mit Löchern, Laibungen, Spitzbogenfenster, Böden, Gewölbe
# ─────────────────────────────────────────────────────────────────────────────

def wand(a, b, boden=0.0):
    """Innenkante einer Wand von a nach b (der Raum liegt links), als innenausbau.Wand ohne Öffnungen."""
    return ia.Wand(a, b, (), boden)


def u_von(w, s):
    """Lage entlang der Wand w zu einer Weltkoordinate s (x bei Wänden entlang x, sonst y) – wie innenausbau.Wand."""
    entlang_x = abs(w.d.x) > 0.5
    return (s - (w.a.x if entlang_x else w.a.y)) * (w.d.x if entlang_x else w.d.y)


def flaeche_mit_loechern(netz, st, w, hoehe, loecher=(), stoff="Kalkputz", v0=0.0):
    """Innenseite einer Wand von v0 bis `hoehe` ohne die Löcher [(u0, u1, v0, v1)] – beliebig viele, auch übereinander (Portal
    und Rosette): je Streifen zwischen den Kanten der Löcher die freien Stücke der Höhe."""
    grenzen = sorted({0.0, w.laenge} | {min(max(g, 0.0), w.laenge) for l in loecher for g in l[:2]})
    for ua, ub in zip(grenzen, grenzen[1:]):
        if ub - ua < 1e-4:
            continue
        stuecke = [(v0, hoehe)]
        for l in loecher:
            if l[0] < ub - 1e-4 and l[1] > ua + 1e-4:
                stuecke = [s for x0, x1 in stuecke for s in ((x0, min(x1, l[2])), (max(x0, l[3]), x1)) if s[1] - s[0] > 1e-4]
        for va, vb in stuecke:
            netz.vieleck_nach([w.p(ua, va), w.p(ub, va), w.p(ub, vb), w.p(ua, vb)], w.n, st[stoff])


def laibung(netz, st, w, u0, u1, v0, v1, tief, stoff="Haustein", unten=False):
    """Gerade Laibung einer rechteckigen Öffnung (Tür) von der Innenseite `tief` Meter in die Wand: Seiten, Sturz, auf Wunsch
    die Schwelle."""
    alle, netz.alle = netz.alle, True
    for u, s in ((u0, 1), (u1, -1)):
        netz.vieleck_nach([w.p(u, v0, 0), w.p(u, v1, 0), w.p(u, v1, -tief), w.p(u, v0, -tief)], w.d * s, st[stoff])
    netz.vieleck_nach([w.p(u0, v1, 0), w.p(u1, v1, 0), w.p(u1, v1, -tief), w.p(u0, v1, -tief)], (0, 0, -1), st[stoff])
    if unten:
        netz.vieleck_nach([w.p(u0, v0, 0), w.p(u1, v0, 0), w.p(u1, v0, -tief), w.p(u0, v0, -tief)], (0, 0, 1), st["Sandstein"])
    netz.alle = alle


def spitz_loch(netz, st, w, u, v, b, h, tief, innen, teile=5, stoff="Haustein", zwickel="Kalkputz"):
    """Spitzbogige Öffnung in der Wand w: das Loch (Rechteck u ± b/2, v … v + h) muss in der Wandfläche frei bleiben. Gefüllt
    werden die Zwickel neben dem Bogen (Putz in der Wandfläche) und die schräge Laibung bis `tief` in die Wand, wo der kleinere
    Umriss `innen` = (v, b, h) sitzt (die Sohlbank aus Sandstein). Gibt den inneren Umriss als Punkte (u, v) zurück."""
    aussen_pkt = pr.bogen_punkte(u, v, b, h, teile)
    innen_pkt = pr.bogen_punkte(u, innen[0], innen[1], innen[2], teile)
    alle, netz.alle = netz.alle, True
    oben = v + h
    for ecke, bogen in (((u + b / 2, oben), aussen_pkt[2:3 + teile]), ((u - b / 2, oben), aussen_pkt[2 + teile:])):
        for p, q in zip(bogen, bogen[1:]):
            netz.vieleck_nach([w.p(*ecke), w.p(*p), w.p(*q)], w.n, st[zwickel])
    n = len(aussen_pkt)
    for i in range(n):
        a0, a1 = aussen_pkt[i], aussen_pkt[(i + 1) % n]
        b0, b1 = innen_pkt[i], innen_pkt[(i + 1) % n]
        quad = [w.p(a0[0], a0[1], 0), w.p(a1[0], a1[1], 0), w.p(b1[0], b1[1], -tief), w.p(b0[0], b0[1], -tief)]
        mitte = sum(quad, Vector()) / 4
        achse = w.p(u, max(v + 0.3, min(oben - 0.3, (a0[1] + a1[1]) / 2)), -tief / 2)
        netz.vieleck_nach(quad, achse - mitte, st["Sandstein" if i == 0 else stoff])
    netz.alle = alle
    return innen_pkt


def scheibe_wand(netz, st, w, punkte, tief, stoff):
    """Ebene Fläche (Glas, Bogenfeld) mit dem Umriss `punkte` (u, v) `tief` Meter in der Wand, zum Raum hin."""
    netz.vieleck_nach([w.p(p[0], p[1], -tief) for p in punkte], w.n, st[stoff])


def ring_wand(netz, st, w, mitte, r0, r1, tief, stoff, n=12, dreh=0.0):
    """Kreisring in der Wandfläche (u, v um `mitte`, Halbmesser r0 … r1) `tief` Meter in der Wand."""
    for k in range(n):
        a0, a1 = dreh + 2 * math.pi * k / n, dreh + 2 * math.pi * (k + 1) / n
        quad = [(mitte[0] + r * math.cos(a), mitte[1] + r * math.sin(a)) for r, a in ((r0, a0), (r1, a0), (r1, a1), (r0, a1))]
        netz.vieleck_nach([w.p(u, v, -tief) for u, v in quad], w.n, st[stoff])


def kreis_wand(netz, st, w, mitte, r, tief, stoff, n=12, dreh=0.0):
    netz.vieleck_nach([w.p(mitte[0] + r * math.cos(dreh + 2 * math.pi * k / n), mitte[1] + r * math.sin(dreh + 2 * math.pi * k / n),
                           -tief) for k in range(n)], w.n, st[stoff])


def balken_wand(netz, st, w, a, b, breite, tief, stoff, dicke=0.04):
    """Leiste in der Wand von a nach b (je (u, v)) mit `breite`, `tief` Meter in der Wand (Vorderseite), `dicke` nach hinten."""
    netz.stab(w.p(a[0], a[1], -tief - dicke / 2), w.p(b[0], b[1], -tief - dicke / 2), breite, dicke, st[stoff], quer=w.n)


FARBEN_GLAS = ("Scheibe rot", "Scheibe blau", "Scheibe gelb", "Scheibe gruen", "Scheibe violett")


def kirchenfenster(netz, st, anker, w, s, v=FENSTER_V, b=FENSTER_B, h=FENSTER_H, dicke=WAND, bunt=True, stufe=1, saat=0):
    """Spitzbogenfenster in der Wand w bei s (Weltkoordinate entlang der Wand): außen in der Wandfläche um 0,16 m breiter, die
    schräge Laibung aus Haustein bis zum Glas („Fenster“) in der Mitte der Wand; ein Mittelpfosten teilt es in zwei Lanzetten
    mit Bögen, darüber ein Ring (Maßwerk aus Sandstein). Mit `bunt` liegen davor bunte Felder (Medaillons, Ränder, ein
    Vierpass im Ring) mit Bleiruten. Licht_ „Fenster“ davor. Gibt das Loch (u0, u1, v0, v1) für die Wandfläche zurück."""
    u = u_von(w, s)
    sb, sv = 0.16, 0.1
    bi, hi = b + 2 * sb, h + sv + sb * 0.9
    vi = v - sv
    t = dicke / 2
    glas = spitz_loch(netz, st, w, u, vi, bi, hi, t, (v, b, h))
    scheibe_wand(netz, st, w, glas, t, "Fenster")
    gv, oben = v, v + h
    gk = oben - b * math.sin(math.radians(60))                              # Kämpfer des Glasbogens
    zufall = random.Random(saat)
    # Maßwerk: Mittelpfosten, zwei Lanzettbögen, Ring
    vor = 0.035
    balken_wand(netz, st, w, (u, gv), (u, gk + 0.12), 0.08, t - vor, "Sandstein", dicke=vor)
    lb = (b - 0.08) / 2
    for su in (-1, 1):
        mitte_l = u + su * (lb / 2 + 0.04)
        bogen = pr.bogen_punkte(mitte_l, gk - lb * 0.4, lb, lb * 0.4 + lb * math.sin(math.radians(60)) * 0.9, 3)[2:]
        for p, q in zip(bogen, bogen[1:]):
            balken_wand(netz, st, w, p, q, 0.06, t - vor, "Sandstein", dicke=vor)
    ring_m = (u, gk + b * 0.5)
    r_ring = b * 0.2
    ring_wand(netz, st, w, ring_m, r_ring, r_ring + 0.045, t - vor - 0.006, "Sandstein", n=12)
    if bunt:
        d = 0.004
        for su in (-1, 1):                                                   # Lanzetten: Medaillons und Ränder
            mu = u + su * (lb / 2 + 0.04)
            for k, mv in enumerate((gv + 0.45, gv + 1.05, gv + 1.65)):
                if mv + 0.25 > gk:
                    continue
                f = FARBEN_GLAS[(k + (su > 0) * 2 + zufall.randrange(2)) % len(FARBEN_GLAS)]
                kreis_wand(netz, st, w, (mu, mv), lb * 0.36, t - d, f, n=10)
                kreis_wand(netz, st, w, (mu, mv), lb * 0.17, t - 2 * d, FARBEN_GLAS[(k + 3) % len(FARBEN_GLAS)], n=8)
                ring_wand(netz, st, w, (mu, mv), lb * 0.36, lb * 0.36 + 0.014, t - 3 * d, "Blei", n=10)
            for k in range(int((gk - gv) / 0.3)):                                # Ränder: kleine Felder, abwechselnd
                for seite in (-1, 1):
                    x = mu + seite * (lb / 2 - 0.035)
                    va = gv + 0.03 + k * 0.3
                    netz.vieleck_nach([w.p(x - 0.025, va, -(t - d)), w.p(x + 0.025, va, -(t - d)), w.p(x + 0.025, va + 0.26, -(t - d)),
                                       w.p(x - 0.025, va + 0.26, -(t - d))], w.n, st["Scheibe rot" if (k + seite) % 2 else "Scheibe blau"])
            for k in range(1, int((gk - gv) / 0.3) + 1):                         # Bleiruten quer
                va = gv + k * 0.3
                balken_wand(netz, st, w, (mu - lb / 2, va), (mu + lb / 2, va), 0.012, t - 3 * d, "Blei", dicke=0.004)
        for k in range(4):                                                   # Vierpass im Ring
            a = math.pi / 2 * k + math.pi / 4
            kreis_wand(netz, st, w, (ring_m[0] + r_ring * 0.45 * math.cos(a), ring_m[1] + r_ring * 0.45 * math.sin(a)),
                       r_ring * 0.42, t - d, FARBEN_GLAS[(k + 1) % 4], n=8)
        kreis_wand(netz, st, w, ring_m, r_ring * 0.22, t - 2 * d, "Scheibe gelb", n=8)
    p = w.p(u, v + h * 0.5, 0.12)
    anker.append(licht(p, (w.n.x, w.n.y, 0), "Fenster", stufe))
    return (u - bi / 2, u + bi / 2, vi, vi + hi)


def rosette(netz, st, anker, w, s, mitte_v, r, dicke=WAND):
    """Rosette über dem Portal: rundes Loch (Quadrat in der Wandfläche, die Ecken verputzt), schräge Laibung, Glas, sechzehn
    Speichen und ein innerer Ring aus Sandstein, bunte Felder zwischen den Speichen. Gibt das Loch zurück."""
    u = u_von(w, s)
    ra, n = r + 0.18, 16
    t = dicke / 2
    aussen = [(u + ra * math.cos(2 * math.pi * k / n), mitte_v + ra * math.sin(2 * math.pi * k / n)) for k in range(n)]
    innen = [(u + r * math.cos(2 * math.pi * k / n), mitte_v + r * math.sin(2 * math.pi * k / n)) for k in range(n)]
    alle, netz.alle = netz.alle, True
    for k in range(n):                                                  # Zwickel zu den Ecken des Quadrats
        a0, a1 = aussen[k], aussen[(k + 1) % n]
        mid = ((a0[0] + a1[0]) / 2 - u, (a0[1] + a1[1]) / 2 - mitte_v)
        ecke = (u + math.copysign(ra, mid[0]), mitte_v + math.copysign(ra, mid[1]))
        netz.vieleck_nach([w.p(*ecke), w.p(*a0), w.p(*a1)], w.n, st["Kalkputz"])
        quad = [w.p(a0[0], a0[1], 0), w.p(a1[0], a1[1], 0), w.p(innen[(k + 1) % n][0], innen[(k + 1) % n][1], -t),
                w.p(innen[k][0], innen[k][1], -t)]
        netz.vieleck_nach(quad, w.p(u, mitte_v, -t) - sum(quad, Vector()) / 4, st["Haustein"])
    netz.alle = alle
    scheibe_wand(netz, st, w, innen, t, "Fenster")
    vor = 0.035
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n
        if k % 2 == 0:
            balken_wand(netz, st, w, (u + r * 0.32 * math.cos(a), mitte_v + r * 0.32 * math.sin(a)),
                        (u + r * math.cos(a), mitte_v + r * math.sin(a)), 0.05, t - vor, "Sandstein", dicke=vor)
        f = FARBEN_GLAS[k % 4] if k % 2 else FARBEN_GLAS[(k // 2) % 5]
        a0, a1 = 2 * math.pi * k / n + 0.03, 2 * math.pi * (k + 1) / n - 0.03
        netz.vieleck_nach([w.p(u + rr * math.cos(aa), mitte_v + rr * math.sin(aa), -(t - 0.004))
                           for rr, aa in ((r * 0.42, a0), (r * 0.9, a0), (r * 0.9, a1), (r * 0.42, a1))], w.n, st[f])
    ring_wand(netz, st, w, (u, mitte_v), r * 0.28, r * 0.34, t - vor, "Sandstein", n=12)
    kreis_wand(netz, st, w, (u, mitte_v), r * 0.28, t - 0.004, "Scheibe gelb", n=12)
    anker.append(licht(w.p(u, mitte_v, 0.15), (w.n.x, w.n.y, 0), "Fenster"))
    return (u - ra, u + ra, mitte_v - ra, mitte_v + ra)


PLATTEN = ("Platte", "Platte hell", "Platte", "Platte dunkel", "Platte warm")


def ohne_loecher(r, loecher):
    teile = [r]
    for loch in loecher:
        teile = [t for r_ in teile for t in ia.rechteck_minus(r_, loch)]
    return teile


def steinplatten(netz, st, x0, x1, y0, y1, z=0.0, saat=0, breite=0.5, laenge=(0.55, 1.0), loecher=(), stoffe=PLATTEN):
    """Boden aus Steinplatten in Reihen entlang x (Reihen etwa `breite`, die Platten zufällig lang, die Stöße versetzt): je
    Platte die Deckfläche, die Fugen (1 cm) sind eine Mörtelfläche 6 mm tiefer; die `loecher` (x0, x1, y0, y1) bleiben frei."""
    for r in ohne_loecher((x0, x1, y0, y1), loecher):
        ia.waagrecht(netz, r, z - 0.006, True, st["Plattenfuge"])
    zufall = random.Random(saat)
    f = 0.005
    y = y0
    while y < y1 - 1e-4:
        yb = y + breite * zufall.uniform(0.85, 1.15)
        if y1 - yb < 0.25:
            yb = y1
        x = x0
        erste = True
        while x < x1 - 1e-4:
            xe = x + zufall.uniform(*laenge) * (zufall.uniform(0.3, 0.9) if erste else 1.0)
            if x1 - xe < 0.25:
                xe = x1
            platte = (x + (f if x > x0 else 0.0), xe - (f if xe < x1 else 0.0), y + (f if y > y0 else 0.0), yb - (f if yb < y1 else 0.0))
            for r in ohne_loecher(platte, loecher):
                ia.waagrecht(netz, r, z, True, st[zufall.choice(stoffe)])
            x, erste = xe, False
        y = yb


def sockel(netz, st, w, loecher=(), hoehe=0.95, frei=()):
    """Sockel aus Haustein vor dem Putz (3 cm), oben ein Gesims aus Sandstein; Lager- und Stoßfugen gemalt."""
    stuecke = [(0.0, w.laenge)]
    for l in list(loecher) + [(a, b, 0.0, hoehe) for a, b in frei]:
        if l[2] < hoehe:
            stuecke = [s for x0, x1 in stuecke for s in ((x0, min(x1, l[0])), (max(x0, l[1]), x1)) if s[1] - s[0] > 0.05]
    zufall = random.Random(int(abs(w.a.x * 13 + w.a.y * 7)))
    for u0, u1 in stuecke:
        wq(netz, st, w, u0, u1, 0.0, hoehe, 0.0, 0.03, "Haustein", ohne=("-y", "-z"))
        wq(netz, st, w, u0, u1, hoehe, hoehe + 0.07, 0.0, 0.06, "Sandstein")
        for k, v in enumerate((0.32, 0.64)):
            netz.vieleck_nach([w.p(u0, v - 0.006, 0.031), w.p(u1, v - 0.006, 0.031), w.p(u1, v + 0.006, 0.031), w.p(u0, v + 0.006, 0.031)],
                              w.n, st["Fuge"])
        for k in range(3):
            v0, v1 = (0.0, 0.32, 0.64, hoehe)[k:k + 2]
            u = u0 + zufall.uniform(0.15, 0.5)
            while u < u1 - 0.15:
                netz.vieleck_nach([w.p(u - 0.006, v0, 0.031), w.p(u + 0.006, v0, 0.031), w.p(u + 0.006, v1, 0.031), w.p(u - 0.006, v1, 0.031)],
                                  w.n, st["Fuge"])
                u += zufall.uniform(0.5, 0.75)


def weihekreuz(netz, st, w, u, v):
    """Weihekreuz: roter Ring mit einem Kreuz darin, auf den Putz gemalt."""
    ring_wand(netz, st, w, (u, v), 0.17, 0.21, -0.003, "Weihrot", n=14)
    for a, b in (((u - 0.12, v), (u + 0.12, v)), ((u, v - 0.12), (u, v + 0.12))):
        balken_wand(netz, st, w, a, b, 0.05, -0.004, "Weihrot", dicke=0.001)
    for k in range(4):
        aa = math.pi / 2 * k
        kreis_wand(netz, st, w, (u + 0.12 * math.cos(aa), v + 0.12 * math.sin(aa)), 0.035, -0.005, "Weihrot", n=6)


def stich(halb, f):
    """Höhe eines Stichbogens (Spannweite 2·halb, Stich f) über dem Kämpfer an der Stelle x (von der Mitte)."""
    r = (halb * halb + f * f) / (2 * f)
    return lambda x: f - r + math.sqrt(max(0.0, r * r - x * x))


def gewoelbe_joch(decke, st, x0, x1, y0, y1, z_k, f, n=8, stoff="Gewoelbeputz", saat=0):
    """Kreuzgratgewölbe über einem Joch (x0 … x1, y0 … y1), Kämpfer in z_k, Stich f: die vier Kappen als Streifen; an den
    Wänden entlang x folgt die Kappe dem Bogen über x, an denen entlang y dem über y; die Grate liegen, wo beide gleich hoch
    sind. Die Flächen zeigen nach unten."""
    a, b = (x1 - x0) / 2, (y1 - y0) / 2
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hx, hy = stich(a, f), stich(b, f)
    rb = (b * b + f * f) / (2 * f)
    ra = (a * a + f * f) / (2 * f)

    def grat_y(x):                                              # wo die Kappe über x (Höhe hx) auf die über y trifft
        h = hx(x)
        return math.sqrt(max(0.0, rb * rb - (h - f + rb) ** 2))

    def grat_x(y):
        h = hy(y)
        return math.sqrt(max(0.0, ra * ra - (h - f + ra) ** 2))
    zufall = random.Random(saat)
    alle, decke.alle = decke.alle, True
    for s in (-1, 1):
        for i in range(n):                                        # Kappen an den Wänden y = cy ± b
            xa, xb = -a + 2 * a * i / n, -a + 2 * a * (i + 1) / n
            quad = [(xa, s * b, hx(xa)), (xb, s * b, hx(xb)), (xb, s * grat_y(xb), hx(xb)), (xa, s * grat_y(xa), hx(xa))]
            pkt = [Vector((cx + p[0], cy + p[1], z_k + p[2])) for p in quad]
            if (pkt[0] - pkt[3]).length + (pkt[1] - pkt[2]).length > 1e-4:
                decke.vieleck_nach(pkt, (0, 0, -1), st[stoff if zufall.random() < 0.8 else "Kalkputz"])
        for i in range(n):                                        # Kappen an den Wänden x = cx ± a
            ya, yb = -b + 2 * b * i / n, -b + 2 * b * (i + 1) / n
            quad = [(s * a, ya, hy(ya)), (s * a, yb, hy(yb)), (s * grat_x(yb), yb, hy(yb)), (s * grat_x(ya), ya, hy(ya))]
            pkt = [Vector((cx + p[0], cy + p[1], z_k + p[2])) for p in quad]
            if (pkt[0] - pkt[3]).length + (pkt[1] - pkt[2]).length > 1e-4:
                decke.vieleck_nach(pkt, (0, 0, -1), st[stoff if zufall.random() < 0.8 else "Kalkputz"])
    decke.alle = alle
    return hx, hy


def schildbogen_wand(netz, st, w, u0, u1, z_k, h_von_u, loecher=(), stoff="Bruchstein", n=8):
    """Wand unter dem Gewölbe zwischen u0 und u1: vom Boden der Wand w bis zur Kappe (Kämpfer z_k plus h_von_u), in Streifen
    (an den Kanten der Löcher geteilt); die Löcher (u0, u1, v0, v1) bleiben frei."""
    grenzen = sorted({u0 + (u1 - u0) * i / n for i in range(n + 1)} | {g for l in loecher for g in l[:2] if u0 < g < u1})
    for ua, ub in zip(grenzen, grenzen[1:]):
        if ub - ua < 1e-4:
            continue
        oa, ob = z_k + h_von_u(ua), z_k + h_von_u(ub)
        stuecke = [(0.0, None)]
        for l in loecher:
            if l[0] < ub - 1e-4 and l[1] > ua + 1e-4:
                stuecke = [(0.0, l[2]), (l[3], None)]
        for va, vb in stuecke:
            if vb is None:
                netz.vieleck_nach([w.p(ua, va), w.p(ub, va), w.p(ub, ob), w.p(ua, oa)], w.n, st[stoff])
            elif vb - va > 1e-4:
                netz.vieleck_nach([w.p(ua, va), w.p(ub, va), w.p(ub, vb), w.p(ua, vb)], w.n, st[stoff])


def gurtbogen(netz, st, a, b, z_k, h, quer, breite=0.3, tiefe=0.07, n=8):
    """Gurtbogen aus Sandstein unter dem Gewölbe von a nach b (Grundriss, Kämpfer z_k, Höhe h(t) über dem Kämpfer für t von −L/2
    bis L/2), `quer` ist die Richtung quer zum Bogen; Unterseite und die beiden Seiten."""
    a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    L = (b - a).length
    d = (b - a).normalized()
    q = Vector((quer[0], quer[1], 0)).normalized() * breite / 2
    alle, netz.alle = netz.alle, True
    for i in range(n):
        ta, tb = -L / 2 + L * i / n, -L / 2 + L * (i + 1) / n
        pa, pb = (a + b) / 2 + d * ta, (a + b) / 2 + d * tb
        za, zb = z_k + h(ta) - tiefe, z_k + h(tb) - tiefe
        netz.vieleck_nach([pa - q + Vector((0, 0, za)), pb - q + Vector((0, 0, zb)), pb + q + Vector((0, 0, zb)), pa + q + Vector((0, 0, za))],
                          (0, 0, -1), st["Sandstein"])
        for s in (-1, 1):
            netz.vieleck_nach([pa + q * s + Vector((0, 0, za)), pb + q * s + Vector((0, 0, zb)), pb + q * s + Vector((0, 0, zb + tiefe + 0.03)),
                               pa + q * s + Vector((0, 0, za + tiefe + 0.03))], q * s, st["Sandstein"])
    netz.alle = alle


def saeule(netz, st, fuss, hoehe, r=0.16):
    """Säule der Krypta: Plinthe, Wulst, Schaft, Würfelkapitell mit Deckplatte; `hoehe` bis zur Oberkante der Deckplatte."""
    f = Vector(fuss)
    netz.quader(f + Vector((0, 0, 0.07)), (2.6 * r, 2.6 * r, 0.14), st["Sandstein"], ohne=("-z",))
    drehkoerper(netz, st, f + Vector((0, 0, 0.14)), [(0.0, 1.22 * r), (0.05, 1.18 * r), (0.09, 1.02 * r), (0.11, r)], "Sandstein", n=10)
    drehkoerper(netz, st, f, [(0.25, r), (hoehe - 0.36, r * 0.94)], "Haustein", n=10)
    k = f + Vector((0, 0, hoehe - 0.36))
    for s in range(4):                                                     # Würfelkapitell: unten rund, oben quadratisch
        w0 = math.pi / 2 * s
        for j in range(3):
            a0, a1 = w0 + math.pi / 2 * j / 3, w0 + math.pi / 2 * (j + 1) / 3
            unten = [k + Vector((0.94 * r * math.cos(a), 0.94 * r * math.sin(a), 0)) for a in (a0, a1)]
            oben = [k + Vector((1.3 * r * max(-1, min(1, math.cos(a) * 1.42)), 1.3 * r * max(-1, min(1, math.sin(a) * 1.42)), 0.3))
                    for a in (a0, a1)]
            netz.vieleck_nach([unten[0], unten[1], oben[1], oben[0]], (math.cos((a0 + a1) / 2), math.sin((a0 + a1) / 2), -0.4),
                              st["Sandstein"])
    netz.quader(k + Vector((0, 0, 0.33)), (2.8 * r, 2.8 * r, 0.06), st["Sandstein"], ohne=("+z",))


# ─────────────────────────────────────────────────────────────────────────────
#  Kirchenschiff: Decke, Dienste, Portal, Bänke, Kanzel, Chor, Altar, Beichtstuhl, Taufstein, Opferstock, Treppe
# ─────────────────────────────────────────────────────────────────────────────

BALKEN_Y = (-3.0, -1.5, 0.0, 1.5, 3.0, 4.5)


def kirchendecke(decke, st):
    """Holzbalkendecke des Schiffs: Bretter entlang y (die Fugen als dunkle Fläche dahinter), Balken quer über die ganze Breite,
    unter den Hauptbalken (bei den Diensten) Sattelhölzer; darüber eine Abdeckung."""
    h = H_KIRCHE
    for by0, by1, bx0, bx1, stoff in ia.dielen_plan(KY0, KY1, KX0, KX1, 71, breite=0.3):
        ia.waagrecht(decke, (bx0, bx1, by0, by1), h, False, st[stoff])
    ia.waagrecht(decke, (KX0, KX1, KY0, KY1), h + 0.025, False, st["Dielenfuge"])
    ia.waagrecht(decke, (KX0 - WAND, KX1 + WAND, KY0 - WAND, KY1 + WAND), h + 0.3, True, st["Holz alt"])
    for y in BALKEN_Y:
        decke.quader((0, y, h - 0.15), (KX1 - KX0, 0.24, 0.3), st["Fachwerk"], ohne=("+z", "+x", "-x"))
        if y in DIENSTE_Y:
            for x, s in ((KX0, 1), (KX1, -1)):
                decke.quader((x + s * 0.35, y, h - 0.37), (0.7, 0.28, 0.14), st["Fachwerk"], ohne=("+z",))


def dienste(netz, st, w):
    """Dienste an einer Langseite: auf einer Konsole in 4 m Höhe ein Wandpfeiler aus Haustein bis unter das Sattelholz."""
    for y in DIENSTE_Y:
        u = u_von(w, y)
        wq(netz, st, w, u - 0.13, u + 0.13, 4.05, H_KIRCHE - 0.51, 0.0, 0.14, "Haustein")
        wq(netz, st, w, u - 0.17, u + 0.17, H_KIRCHE - 0.51, H_KIRCHE - 0.44, 0.0, 0.2, "Sandstein")
        wq(netz, st, w, u - 0.17, u + 0.17, 3.9, 4.05, 0.0, 0.22, "Sandstein")
        alle, netz.alle = netz.alle, True                                     # Konsole: nach unten verjüngt
        netz.vieleck_nach([w.p(u - 0.17, 3.9, 0.22), w.p(u + 0.17, 3.9, 0.22), w.p(u + 0.08, 3.55, 0.0), w.p(u - 0.08, 3.55, 0.0)],
                          (w.n.x, w.n.y, -1.2), st["Sandstein"])
        for s in (-1, 1):
            netz.vieleck_nach([w.p(u + s * 0.17, 3.9, 0.22), w.p(u + s * 0.17, 3.9, 0.0), w.p(u + s * 0.08, 3.55, 0.0)], w.d * s, st["Sandstein"])
        netz.alle = alle


def eisentuer(netz, st, M, b, h, rueck):
    """Gekauft: eisenbeschlagene Tür statt der Holztür (wie schmied_innen.eisentuer, aber `rueck` hinter der Innenseite)."""
    si.q(netz, st, M, (0, -rueck - 0.03, (h - 0.03) / 2), (b - 0.05, 0.06, h - 0.03), "Eisen", ohne=("-y",))
    for f in (0.12, 0.38, 0.62, 0.88):
        si.q(netz, st, M, (0, -rueck + 0.008, h * f), (b - 0.06, 0.016, 0.08), "Kohle", ohne=("-y",))
    for s in (-1, 1):
        si.q(netz, st, M, (s * (b / 2 - 0.07), -rueck + 0.01, h / 2), (0.08, 0.02, h - 0.1), "Kohle", ohne=("-y",))
    for k in range(5):
        for j in range(4):
            si.q(netz, st, M, (-b / 2 + 0.2 + (b - 0.4) * j / 3, -rueck + 0.026, h * (0.12 + 0.19 * k)), (0.035, 0.012, 0.035), "Eisen",
                 ohne=("-y",))
    si.q(netz, st, M, (b * 0.3, -rueck + 0.03, h * 0.5), (0.09, 0.05, 0.12), "Eisen", ohne=("-y",))


def portal(netz, st, w, holztuer, eisen):
    """Portal in der Vorderwand: spitzbogige Öffnung mit schräger Laibung, auf halber Tiefe der Türflügel (Holztuer bzw. die
    gekaufte Eisentür), darüber Sturz und Bogenfeld aus Sandstein. Gibt das Loch für die Wandfläche zurück."""
    u = u_von(w, 0.0)
    b, h = PORTAL_B + 0.4, PORTAL_H + 0.3
    tief = 0.2
    innen = spitz_loch(netz, st, w, u, 0.0, b, h, tief, (0.0, PORTAL_B, PORTAL_H), teile=6)
    tuer_h = 2.7
    feld = ia.beschneiden([(p[0], p[1]) for p in innen], u - 5, u + 5, tuer_h, 20)
    scheibe_wand(netz, st, w, feld, tief, "Sandstein")
    wq(netz, st, w, u - PORTAL_B / 2, u + PORTAL_B / 2, tuer_h - 0.12, tuer_h, -tief, -tief + 0.08, "Sandstein")
    for k in range(3):                                                  # Bogenfeld: Kreuz und zwei Rosen in Relief
        a = (u + (k - 1) * 0.42, tuer_h + 0.35 + (0.12 if k == 1 else 0.0))
        if k == 1:
            balken_wand(netz, st, w, (a[0], a[1] - 0.2), (a[0], a[1] + 0.25), 0.07, tief - 0.03, "Haustein", dicke=0.03)
            balken_wand(netz, st, w, (a[0] - 0.15, a[1] + 0.08), (a[0] + 0.15, a[1] + 0.08), 0.07, tief - 0.036, "Haustein", dicke=0.03)
        else:
            kreis_wand(netz, st, w, a, 0.1, tief - 0.02, "Haustein", n=8)
    M = si.rahmen(0.0, KY0, 0)
    si.bretttuer(holztuer, st, M, PORTAL_B, tuer_h, rueck=tief)
    eisentuer(eisen, st, M, PORTAL_B, tuer_h, tief - 0.005)
    return (u - b / 2, u + b / 2, 0.0, h)


def kirchenbank(netz, st, x0, x1, y):
    """Bank (Sitz in SITZ, Mitte des Sitzes bei y, Blick +y): Sitzbrett, Lehne mit Leiste, hinten das schräge Buchbrett für die
    Reihe dahinter, an den Enden Wangen mit spitzem Abschluss."""
    L, xm = x1 - x0, (x0 + x1) / 2
    netz.quader((xm, y, SITZ - 0.025), (L - 0.08, 0.38, 0.05), st["Kirchenholz hell"])
    netz.quader((xm, y - 0.17, SITZ + 0.24), (L - 0.08, 0.04, 0.44), st["Kirchenholz"])
    netz.quader((xm, y - 0.17, SITZ + 0.48), (L - 0.06, 0.08, 0.04), st["Kirchenholz"])
    netz.quader((0, 0, 0), (L - 0.08, 0.16, 0.025), st["Kirchenholz hell"],
                Matrix.Translation((xm, y - 0.27, 0.8)) @ Matrix.Rotation(math.radians(-14), 4, "X"))
    for x in (x0 + 0.03, x1 - 0.03):
        profil = [(y - 0.33, 0.0), (y + 0.21, 0.0), (y + 0.21, SITZ + 0.06), (y + 0.06, SITZ + 0.1), (y - 0.13, 0.98), (y - 0.22, 1.06),
                  (y - 0.33, 0.98)]
        netz.prisma(profil, x - 0.03, x + 0.03, st["Kirchenholz"])


BANK_Y = (1.6, 0.6, -0.4, -1.4)                                        # Reihen von vorn nach hinten
BANK_X = ((-2.3, -0.6), (0.6, 2.3))


def baenke(netz, st, anker):
    """Vier Reihen Bänke links und rechts des Mittelgangs; je Bank zwei Plätze Glaeubig_<n> (Füllreihenfolge: vorn → hinten, in
    jeder Reihe erst die beiden am Mittelgang, dann die außen)."""
    nummer = 1
    for y in BANK_Y:
        for x0, x1 in BANK_X:
            kirchenbank(netz, st, x0, x1, y)
        for x in (-1.03, 1.03, -1.87, 1.87):
            anker.append(wi.gast(f"Glaeubig_{nummer}", (x, y), (0, 1), sitz=SITZ))
            nummer += 1


def achteck_punkte(m, apothem, z, n=8, dreh=0.0):
    r = apothem / math.cos(math.pi / n)
    return [Vector((m.x + r * math.cos(dreh + math.pi / n + 2 * math.pi * k / n), m.y + r * math.sin(dreh + math.pi / n + 2 * math.pi * k / n), z))
            for k in range(n)]


def kanzel(netz, st, anker):
    """Kanzel an der linken Wand: achteckiger Korb aus Eichenholz auf einer Säule (unten ein Kegel), die Brüstung (Oberkante
    figuren.KANZEL über dem Boden) mit Füllungen, vorn ein Pult mit der Bibel; an der Wand die Rückwand bis zum Schalldeckel mit
    Taube und Knauf; die Treppe mit Wange und Handlauf läuft an der Wand vom Chor herauf. Predigt_1 steht in der Mitte."""
    m, z = KANZEL_M, KANZEL_Z
    holz, hell = st["Kirchenholz"], st["Kirchenholz hell"]
    a_aus, a_in = 0.52, 0.47
    rand_h = fg.KANZEL[1]
    boden = achteck_punkte(m, a_aus, z)
    netz.vieleck_nach(boden, (0, 0, 1), hell)
    unten = achteck_punkte(m, a_aus, z - 0.14)
    kegel = achteck_punkte(m, 0.16, z - 0.6)
    alle, netz.alle = netz.alle, True
    for k in range(8):
        j = (k + 1) % 8
        mitte = (boden[k] + boden[j]) / 2
        aus = Vector((mitte.x - m.x, mitte.y - m.y, 0))
        netz.vieleck_nach([boden[k], boden[j], unten[j], unten[k]], aus, holz)
        netz.vieleck_nach([unten[k], unten[j], kegel[j], kegel[k]], aus - Vector((0, 0, 0.6)), holz)
        eingang = aus.y > 0.4 and abs(aus.x) < 0.3                      # die Seite zur Treppe bleibt offen
        if eingang:
            continue
        o_k, o_j = boden[k] + Vector((0, 0, rand_h)), boden[j] + Vector((0, 0, rand_h))
        netz.vieleck_nach([boden[k], boden[j], o_j, o_k], aus, holz)
        i_k, i_j = m.lerp(boden[k], a_in / a_aus), m.lerp(boden[j], a_in / a_aus)
        i_k.z = i_j.z = z
        netz.vieleck_nach([i_k, i_j, i_j + Vector((0, 0, rand_h)), i_k + Vector((0, 0, rand_h))], -aus, holz)
        netz.vieleck_nach([o_k, o_j, i_j + Vector((0, 0, rand_h)), i_k + Vector((0, 0, rand_h))], (0, 0, 1), hell)
        n_ = aus.normalized()
        for f0, f1 in ((0.15, 0.55), (0.62, 0.86)):                     # Füllungen
            p = [boden[k].lerp(boden[j], 0.18), boden[k].lerp(boden[j], 0.82)]
            netz.vieleck_nach([p[0] + n_ * 0.006 + Vector((0, 0, rand_h * f0)), p[1] + n_ * 0.006 + Vector((0, 0, rand_h * f0)),
                               p[1] + n_ * 0.006 + Vector((0, 0, rand_h * f1)), p[0] + n_ * 0.006 + Vector((0, 0, rand_h * f1))], n_, hell)
    netz.alle = alle
    drehkoerper(netz, st, Vector((m.x, m.y, 0)), [(0.0, 0.3), (0.16, 0.3), (0.16, 0.2), (0.3, 0.16), (z - 0.6, 0.15)], "Haustein", n=10)
    # Pult mit Bibel auf der Brüstung, in Blickrichtung
    vor = Vector((KANZEL_BLICK[0], KANZEL_BLICK[1], 0)).normalized()
    pult = m + vor * 0.44 + Vector((0, 0, z + rand_h + 0.02))
    neig = Matrix.Translation(pult) @ Matrix.Rotation(math.radians(si.drehung_von(vor)), 4, "Z") @ Matrix.Rotation(math.radians(20), 4, "X")
    netz.quader((0, 0, 0), (0.42, 0.3, 0.025), holz, neig)
    netz.quader((0, 0.0, 0.025), (0.38, 0.26, 0.03), st["Buchrot"], neig)
    for s in (-1, 1):
        netz.quader((s * 0.09, 0.0, 0.045), (0.17, 0.24, 0.012), st["Leinen"], neig)
    # Rückwand und Schalldeckel
    w = wand((KX0, KY1), (KX0, KY0))
    u = u_von(w, m.y)
    wq(netz, st, w, u - 0.42, u + 0.42, z + 0.2, z + 2.3, 0.0, 0.04, "Kirchenholz")
    wq(netz, st, w, u - 0.32, u + 0.32, z + 0.6, z + 2.0, 0.04, 0.055, "Kirchenholz hell")
    deckel = achteck_punkte(m, 0.72, z + 2.35)
    deckel_u = achteck_punkte(m, 0.72, z + 2.2)
    netz.vieleck_nach(deckel, (0, 0, 1), holz)
    alle, netz.alle = netz.alle, True
    netz.vieleck_nach(deckel_u, (0, 0, -1), hell)
    for k in range(8):
        j = (k + 1) % 8
        aus = (deckel[k] + deckel[j]) / 2 - Vector((m.x, m.y, deckel[k].z))
        netz.vieleck_nach([deckel_u[k], deckel_u[j], deckel[j], deckel[k]], aus, holz)
        netz.vieleck_nach([deckel_u[k], deckel_u[j], deckel_u[j] - Vector((0, 0, 0.12)) + aus.normalized() * -0.03,
                           deckel_u[k] - Vector((0, 0, 0.12)) + aus.normalized() * -0.03], aus, hell)     # Lambrequin
    netz.alle = alle
    drehkoerper(netz, st, Vector((m.x, m.y, z + 2.35)), [(0.0, 0.3), (0.22, 0.12), (0.4, 0.04), (0.44, 0.07), (0.5, 0.0)], "Gold", n=8)
    taube = Vector((m.x, m.y, z + 2.1))                                 # Taube unter dem Deckel
    netz.quader(taube, (0.06, 0.16, 0.05), st["Federweiss"])
    netz.quader(taube + Vector((0, 0, 0.0)), (0.28, 0.06, 0.012), st["Federweiss"])
    # Treppe an der Wand: vom Chor (y 3,75) hinauf zum Korb
    stufen = 9
    y_unten, y_oben = 3.75, m.y + a_aus
    lauf = (y_unten - y_oben) / stufen
    hub = z / stufen
    x0, x1 = KX0 + 0.07, KX0 + 0.6
    for k in range(stufen):
        y = y_unten - (k + 1) * lauf
        if k < stufen - 1:
            netz.quader(((x0 + x1) / 2, y + lauf / 2, (k + 1) * hub - 0.025), (x1 - x0, lauf + 0.03, 0.05), st["Kirchenholz hell"],
                        ohne=("+x", "-x", "-z"))
        netz.quader(((x0 + x1) / 2, y + lauf - 0.01, (k + 0.5) * hub - 0.025), (x1 - x0, 0.02, hub - 0.05), holz, ohne=("+y", "+x", "-x", "+z"))
    wange = [(y_unten, 0.0), (y_unten, 0.18), (y_oben, z + 0.18), (y_oben, z - 0.3)]
    netz.prisma(wange, x1, x1 + 0.05, holz)
    for t in (0.0, 0.5, 1.0):
        p = Vector((x1 + 0.025, y_unten + (y_oben - y_unten) * t, t * z + 0.15))
        netz.stab(p, p + Vector((0, 0, 0.85)), 0.05, 0.05, holz)
    netz.stab(Vector((x1 + 0.025, y_unten, 1.0)), Vector((x1 + 0.025, y_oben, z + 1.0)), 0.05, 0.05, hell)
    o = bs.anker("Predigt_1", (m.x, m.y, z), si.drehung_von(KANZEL_BLICK))
    anker.append(o)


def chor(netz, st):
    """Chor: zwei Stufen aus Sandstein über die Breite x ±CHOR_X, darauf Steinplatten bis zur Rückwand."""
    x0, x1 = -CHOR_X, CHOR_X
    for z, y0, y1 in ((0.15, CHOR_Y, CHOR_Y + 0.32), (CHOR_H, CHOR_Y + 0.32, KY1)):
        steinplatten(netz, st, x0, x1, y0, y1, z=z, saat=int(z * 100), breite=0.45, stoffe=("Platte hell", "Platte", "Platte hell", "Platte warm"))
        netz.vieleck_nach([(x0, y0, z - 0.15), (x1, y0, z - 0.15), (x1, y0, z), (x0, y0, z)], (0, -1, 0), st["Sandstein"])
        for x, s in ((x0, -1), (x1, 1)):
            netz.vieleck_nach([(x, y0, 0.0), (x, y1, 0.0), (x, y1, z), (x, y0, z)], (s, 0, 0), st["Sandstein"])


def kerze(netz, st, fuss, h=0.32, r=0.025, anker=None, stufe=1):
    """Kerze mit Flamme („Glut“); mit `anker` ein Licht darüber."""
    drehkoerper(netz, st, fuss, [(0.0, r), (h, r), (h, r * 0.6)], "Kerze", n=6)
    drehkoerper(netz, st, Vector(fuss) + Vector((0, 0, h)), [(0.0, 0.0), (0.012, r * 0.5), (0.04, r * 0.4), (0.075, 0.0)], "Glut", n=5)


def leuchter(netz, st, fuss, h=0.32):
    """Altarleuchter aus Messing: Fuß, Schaft mit Knauf, Tropfteller, Dorn; darauf eine Kerze."""
    f = Vector(fuss)
    drehkoerper(netz, st, f, [(0.0, 0.1), (0.03, 0.09), (0.05, 0.03), (0.16, 0.022), (0.18, 0.04), (0.2, 0.022), (h, 0.02), (h, 0.07),
                              (h + 0.02, 0.07)], "Messing", n=8)
    kerze(netz, st, f + Vector((0, 0, h + 0.02)), h=0.36, r=0.028)


def altar(netz, st, anker):
    """Hochaltar auf dem Chor: Stipes aus Haustein, Mensa aus Sandstein, darauf das Altartuch, vorn das rote Antependium mit
    goldener Borte und Kreuz; Altarkreuz, zwei Leuchter, das Messbuch auf seinem Pult. Licht „Glut“ über den Kerzen."""
    m = Vector((0, 4.75, CHOR_H))
    oben = m.z + 1.0
    netz.quader(m + Vector((0, 0, 0.5)), (1.9, 0.9, 1.0), st["Haustein"], ohne=("-z",))
    netz.quader(m + Vector((0, 0, 1.04)), (2.04, 1.02, 0.08), st["Sandstein"])
    o = oben + 0.08
    netz.quader(m + Vector((0, 0, 1.08 + 0.003)), (1.98, 0.98, 0.006), st["Altartuch"], ohne=("-z",))
    for s in (-1, 1):                                                     # Tuch hängt an den Seiten über
        netz.vieleck_nach([(s * 0.993, m.y - 0.49, o - 0.3), (s * 0.993, m.y + 0.49, o - 0.3), (s * 0.993, m.y + 0.49, o + 0.006),
                           (s * 0.993, m.y - 0.49, o + 0.006)], (s, 0, 0), st["Altartuch"])
    yv = m.y - 0.452
    netz.vieleck_nach([(-0.92, yv, m.z + 0.06), (0.92, yv, m.z + 0.06), (0.92, yv, m.z + 0.96), (-0.92, yv, m.z + 0.96)], (0, -1, 0), st["Weihrot"])
    for (x0, z0), (x1, z1) in (((-0.92, m.z + 0.06), (0.92, m.z + 0.12)), ((-0.92, m.z + 0.9), (0.92, m.z + 0.96)),
                               ((-0.92, m.z + 0.12), (-0.86, m.z + 0.9)), ((0.86, m.z + 0.12), (0.92, m.z + 0.9)),
                               ((-0.04, m.z + 0.25), (0.04, m.z + 0.58)), ((-0.04, m.z + 0.66), (0.04, m.z + 0.8)),
                               ((-0.2, m.z + 0.58), (0.2, m.z + 0.66))):
        netz.vieleck_nach([(x0, yv - 0.003, z0), (x1, yv - 0.003, z0), (x1, yv - 0.003, z1), (x0, yv - 0.003, z1)], (0, -1, 0), st["Gold"])
    netz.vieleck_nach([(-1.0, m.y - 0.513, o - 0.22), (1.0, m.y - 0.513, o - 0.22), (1.0, m.y - 0.513, o + 0.006), (-1.0, m.y - 0.513, o + 0.006)],
                      (0, -1, 0), st["Altartuch"])
    k = Vector((0, m.y + 0.25, o))                                        # Altarkreuz
    drehkoerper(netz, st, k, [(0.0, 0.11), (0.04, 0.09), (0.06, 0.03), (0.1, 0.025)], "Gold", n=8)
    netz.quader(k + Vector((0, 0, 0.36)), (0.035, 0.035, 0.52), st["Gold"])
    netz.quader(k + Vector((0, 0, 0.47)), (0.3, 0.035, 0.035), st["Gold"])
    for x in (-0.62, 0.62):
        leuchter(netz, st, (x, m.y + 0.22, o))
    b = Matrix.Translation((-0.35, m.y - 0.12, o + 0.05)) @ Matrix.Rotation(math.radians(20), 4, "X")      # Messbuch
    netz.quader((0, 0, -0.03), (0.3, 0.24, 0.05), st["Holz"], b)
    netz.quader((0, 0, 0.0), (0.36, 0.28, 0.03), st["Buchrot"], b)
    for s in (-1, 1):
        netz.quader((s * 0.085, 0, 0.02), (0.16, 0.25, 0.012), st["Leinen"], b)
    anker.append(licht(Vector((0, m.y + 0.1, o + 0.75)), (0, 0, 1), "Glut"))


def kruzifix(netz, st):
    """Großes Kruzifix an der Rückwand über dem Altar: Kreuz aus dunklem Holz mit Titulus, geschnitzter Leib mit Lendentuch,
    goldener Nimbus."""
    y = KY1 - 0.1
    holz = st["Kirchenholz"]
    netz.quader((0, y, 4.4), (0.16, 0.12, 2.8), holz)
    netz.quader((0, y, 5.05), (1.7, 0.1, 0.16), holz)
    netz.quader((0, y - 0.065, 5.62), (0.36, 0.02, 0.14), st["Altartuch"])
    v = y - 0.12
    c = st["Corpus"]
    netz.quader((0, v, 4.62), (0.24, 0.12, 0.5), c)                       # Rumpf
    netz.quader((0, v - 0.005, 4.3), (0.27, 0.13, 0.17), st["Altartuch"])   # Lendentuch
    for s in (-1, 1):
        netz.stab((s * 0.11, v, 4.86), (s * 0.74, v, 5.06), 0.075, 0.075, c)          # Arme, leicht hängend
        netz.stab((s * 0.06, v - 0.006 * s, 4.25), (s * 0.02, v - 0.006 * s, 3.55), 0.085, 0.085, c)          # Beine
    drehkoerper(netz, st, Vector((0.03, v, 4.9)), [(0.0, 0.06), (0.05, 0.1), (0.15, 0.09), (0.22, 0.0)], "Corpus", n=8)
    ring_wand(netz, st, wand((KX1, KY1), (KX0, KY1)), (u_von(wand((KX1, KY1), (KX0, KY1)), 0.03), 5.0), 0.13, 0.17, -0.17, "Gold", n=12)


def ewiges_licht(netz, st, anker, mitte):
    """Ewiges Licht: rotes Glas in einer Messingschale an drei Ketten von der Decke, darin die Flamme („Glut“)."""
    m = Vector(mitte)
    drehkoerper(netz, st, m, [(0.0, 0.0), (0.0, 0.03), (0.06, 0.12), (0.1, 0.14), (0.12, 0.14)], "Messing", n=8)
    drehkoerper(netz, st, m + Vector((0, 0, 0.07)), [(0.0, 0.05), (0.1, 0.07), (0.16, 0.075)], "Scheibe rot", n=8)
    drehkoerper(netz, st, m + Vector((0, 0, 0.18)), [(0.0, 0.0), (0.01, 0.015), (0.04, 0.0)], "Glut", n=5)
    alle, netz.alle = netz.alle, True
    for k in range(3):
        w = 2 * math.pi * k / 3
        netz.stab(m + Vector((0.13 * math.cos(w), 0.13 * math.sin(w), 0.12)), m + Vector((0, 0, 0.7)), 0.012, 0.012, st["Messing"])
    netz.stab(m + Vector((0, 0, 0.7)), Vector((m.x, m.y, H_KIRCHE)), 0.012, 0.012, st["Eisen"])
    netz.alle = alle
    anker.append(licht(m + Vector((0, 0, 0.25)), (0, 0, 1), "Glut"))


def fluegelaltar(netz, st, gold):
    """Ab Stufe 2: geschnitzter Flügelaltar hinten auf der Mensa – Schrein mit drei bemalten Tafeln, die Flügel offen, oben ein
    Gesprenge mit Fialen; `gold`: Stufe 3, Rahmen und Gesprenge vergoldet, ein Strahlenkranz in der Mitte."""
    y = 4.75 + 0.42
    z = CHOR_H + 1.08
    rahmen = st["Gold" if gold else "Kirchenholz"]
    netz.quader((0, y, z + 0.06), (2.0, 0.12, 0.12), rahmen)                                  # Predella
    netz.quader((0, y + 0.02, z + 0.12 + 0.6), (0.96, 0.1, 1.2), rahmen)                     # Schrein
    for x, f in ((-0.28, "Tafelbild blau"), (0.0, "Tafelbild rot"), (0.28, "Tafelbild gruen")):
        netz.vieleck_nach([(x - 0.12, y - 0.032, z + 0.22), (x + 0.12, y - 0.032, z + 0.22), (x + 0.12, y - 0.032, z + 1.18), (x - 0.12, y - 0.032, z + 1.18)],
                          (0, -1, 0), st[f])
        netz.vieleck_nach([(x - 0.06, y - 0.034, z + 0.7), (x + 0.06, y - 0.034, z + 0.7), (x + 0.06, y - 0.034, z + 0.98), (x - 0.06, y - 0.034, z + 0.98)],
                          (0, -1, 0), st["Corpus"])                                           # Heiligenfigur, gemalt
        kreis_wand(netz, st, wand((KX1, y - 0.036), (KX0, y - 0.036)), (KX1 - x, z + 1.03), 0.06, 0.0, "Gold", n=8)
    for s in (-1, 1):                                                                         # offene Flügel, schräg
        a = Vector((s * 0.48, y - 0.03, z + 0.14))
        e = a + Vector((s * 0.46, -0.16, 0))
        netz.vieleck_nach([a, e, e + Vector((0, 0, 1.0)), a + Vector((0, 0, 1.0))], (-0.16 * s, -0.46, 0), rahmen)
        mitte_f = a.lerp(e, 0.5)
        n_ = Vector((-0.16 * s, -0.46, 0)).normalized()
        q = (e - a).normalized()
        netz.vieleck_nach([mitte_f - q * 0.18 + n_ * 0.004 + Vector((0, 0, 0.08)), mitte_f + q * 0.18 + n_ * 0.004 + Vector((0, 0, 0.08)),
                           mitte_f + q * 0.18 + n_ * 0.004 + Vector((0, 0, 0.92)), mitte_f - q * 0.18 + n_ * 0.004 + Vector((0, 0, 0.92))], n_,
                          st["Tafelbild blau" if s < 0 else "Tafelbild rot"])
    for x in (-0.36, 0.0, 0.36):                                                              # Gesprenge
        fuss = Vector((x, y + 0.02, z + 1.32))
        netz.quader(fuss + Vector((0, 0, 0.12)), (0.08, 0.08, 0.24), rahmen)
        for k in range(4):
            a, b = (fuss + Vector((0.06 * math.cos(w), 0.06 * math.sin(w), 0.24)) for w in (math.pi / 2 * k, math.pi / 2 * (k + 1)))
            netz.vieleck_nach([a, b, fuss + Vector((0, 0, 0.62 if x == 0 else 0.48))], (a + b) / 2 - fuss - Vector((0, 0, 0.24)), rahmen)
    if gold:
        mitte = Vector((0, y - 0.04, z + 0.82))
        for k in range(12):
            w = 2 * math.pi * k / 12
            netz.vieleck_nach([mitte + Vector((0.1 * math.cos(w), 0, 0.1 * math.sin(w))), mitte + Vector((0.3 * math.cos(w + 0.08), 0, 0.3 * math.sin(w + 0.08))),
                               mitte + Vector((0.3 * math.cos(w - 0.08), 0, 0.3 * math.sin(w - 0.08)))], (0, -1, 0), st["Gold"])


def beichtstuhl(netz, st, anker):
    """Beichtstuhl an der rechten Wand vorn: drei Zellen – in der Mitte der Priester hinter einer halbhohen Tür und einem roten
    Vorhang, links und rechts offene Kniezellen mit Kniebank, Armbrett und Sprechgitter in der Trennwand; Gesims und Kreuz oben.
    Beichte_1 kniet in der vorderen Zelle mit Blick zum Gitter."""
    xw, t = KX1, 0.85
    xf = xw - t
    y0, y1, h = -3.95, -1.45, 2.45
    ya, yb = y0 + 0.8, y0 + 1.7
    holz, hell = st["Kirchenholz"], st["Kirchenholz hell"]
    netz.quader((xw - 0.03, (y0 + y1) / 2, h / 2), (0.04, y1 - y0, h), holz, ohne=("+x",))
    for y in (y0, y1):
        netz.quader((xw - t / 2, y, h / 2), (t, 0.05, h), holz)
    for y in (ya, yb):                                                       # Trennwände mit Sprechgitter
        for z0, z1 in ((0.0, 0.88), (1.26, h)):
            netz.quader((xw - t / 2 - 0.02, y, (z0 + z1) / 2), (t - 0.08, 0.05, z1 - z0), holz)
        for x0, x1 in ((xf + 0.04, xw - 0.62), (xw - 0.28, xw - 0.05)):
            netz.quader(((x0 + x1) / 2, y, 1.07), (x1 - x0, 0.05, 0.38), holz)
        netz.vieleck_nach([(xw - 0.62, y, 0.88), (xw - 0.28, y, 0.88), (xw - 0.28, y, 1.26), (xw - 0.62, y, 1.26)], (0, 1, 0), st["Kohle"])
        netz.vieleck_nach([(xw - 0.62, y, 0.88), (xw - 0.28, y, 0.88), (xw - 0.28, y, 1.26), (xw - 0.62, y, 1.26)], (0, -1, 0), st["Kohle"])
        for s in (-1, 1):
            for k in range(4):
                netz.stab((xw - 0.62 + 0.085 * (k + 0.5), y + s * 0.03, 0.88), (xw - 0.62 + 0.085 * (k + 0.5), y + s * 0.03, 1.26), 0.016, 0.01, hell,
                          quer=(0, 1, 0))
                netz.stab((xw - 0.62, y + s * 0.03, 0.88 + 0.095 * (k + 0.5)), (xw - 0.28, y + s * 0.03, 0.88 + 0.095 * (k + 0.5)), 0.016, 0.01,
                          hell, quer=(0, 1, 0))
            netz.quader((xw - 0.45, y + s * 0.09, 0.86), (0.5, 0.14, 0.03), hell)               # Armbrett unter dem Gitter
    netz.quader((xw - t / 2 + 0.03, (y0 + y1) / 2, h + 0.04), (t + 0.1, y1 - y0 + 0.12, 0.08), holz)
    netz.quader((xf - 0.02, (y0 + y1) / 2, h + 0.14), (0.08, y1 - y0 + 0.16, 0.12), hell)
    m = Vector((xf - 0.02, (ya + yb) / 2, h + 0.2))
    netz.quader(m + Vector((0, 0, 0.25)), (0.05, 0.05, 0.5), st["Gold"])
    netz.quader(m + Vector((0, 0, 0.33)), (0.05, 0.28, 0.05), st["Gold"])
    netz.quader((xf + 0.02, (ya + yb) / 2, 0.55), (0.04, yb - ya - 0.06, 1.1), holz)        # halbhohe Tür
    for z0, z1 in ((0.12, 0.5), (0.6, 0.98)):
        netz.vieleck_nach([(xf - 0.002, ya + 0.12, z0), (xf - 0.002, yb - 0.12, z0), (xf - 0.002, yb - 0.12, z1), (xf - 0.002, ya + 0.12, z1)],
                          (-1, 0, 0), hell)
    falten = 9                                                              # Vorhang über der Tür
    for k in range(falten):
        y_a = ya + 0.03 + (yb - ya - 0.06) * k / falten
        y_b = ya + 0.03 + (yb - ya - 0.06) * (k + 1) / falten
        xa = xf + 0.03 + (0.025 if k % 2 else 0.0)
        xb = xf + 0.03 + (0.0 if k % 2 else 0.025)
        netz.vieleck_nach([(xa, y_a, 1.12), (xb, y_b, 1.12), (xb, y_b, h - 0.05), (xa, y_a, h - 0.05)], (-1, 0, 0), st["Vorhang"])
    netz.stab((xf + 0.03, ya, h - 0.03), (xf + 0.03, yb, h - 0.03), 0.025, 0.025, st["Messing"])
    netz.quader((xw - 0.3, (ya + yb) / 2, 0.45), (0.5, yb - ya - 0.1, 0.05), hell)            # Sitz des Priesters
    for yc in ((y0 + ya) / 2, (yb + y1) / 2):                                                 # Kniebänke
        netz.quader((xw - 0.43, yc - 0.06, 0.04), (0.62, 0.62, 0.08), hell, ohne=("-z",))
    o = bs.anker("Beichte_1", (xw - 0.43, y0 + 0.5, 0.08), si.drehung_von((0, 1)))
    anker.append(o)


def taufstein(netz, st):
    """Taufstein vorn links: achteckiges Becken aus Sandstein auf Schaft und Fuß, Wasser darin; der hölzerne Deckel hängt an
    einem Seil darüber."""
    m = Vector((-2.35, -3.3, 0))
    drehkoerper(netz, st, m, [(0.0, 0.5), (0.12, 0.5), (0.12, 0.34), (0.2, 0.22), (0.55, 0.19), (0.6, 0.3), (0.66, 0.46), (0.95, 0.5),
                              (0.98, 0.5), (0.98, 0.43)], "Sandstein", n=8, dreh=math.pi / 8)
    drehkoerper(netz, st, m, [(0.82, 0.38), (0.98, 0.43)], "Sandstein", n=8, innen=True, dreh=math.pi / 8)
    scheibe(netz, st, m, 0.38, 0.82, "Sandstein", n=8, dreh=math.pi / 8)
    scheibe(netz, st, m, 0.4, 0.88, "Wasser", n=8, dreh=math.pi / 8)
    d = m + Vector((0, 0, 1.55))
    drehkoerper(netz, st, d, [(0.0, 0.0), (0.0, 0.52), (0.1, 0.5), (0.35, 0.18), (0.45, 0.1), (0.5, 0.14), (0.58, 0.0)], "Kirchenholz", n=8,
                dreh=math.pi / 8)
    alle, netz.alle = netz.alle, True
    netz.stab(d + Vector((0, 0, 0.58)), Vector((m.x, m.y, H_KIRCHE)), 0.02, 0.02, st["Seil"])
    netz.alle = alle


def weihwasser(netz, st):
    """Weihwasserbecken rechts neben dem Portal: Schale aus Sandstein auf einer Konsole an der Wand."""
    m = Vector((1.35, KY0 + 0.2, 0.86))
    netz.quader((m.x, KY0 + 0.08, m.z - 0.18), (0.22, 0.16, 0.36), st["Sandstein"], ohne=("-y",))
    drehkoerper(netz, st, m, [(0.0, 0.06), (0.06, 0.16), (0.12, 0.2), (0.14, 0.2), (0.14, 0.17)], "Sandstein", n=10)
    drehkoerper(netz, st, m, [(0.06, 0.12), (0.14, 0.17)], "Sandstein", n=10, innen=True)
    scheibe(netz, st, m, 0.14, m.z + 0.11, "Wasser", n=10)


def opferstock(netz, st):
    """Bedienung_Lager_Opferstock: Holzblock mit Eisenbändern und Schloss auf einer Steinplatte, oben der Schlitz – hier liegt,
    was die Gläubigen spenden."""
    x, y = 2.0, -4.0
    netz.quader((x, y, 0.05), (0.66, 0.66, 0.1), st["Sandstein"], ohne=("-z",))
    netz.quader((x, y, 0.55), (0.42, 0.42, 0.9), st["Kirchenholz"], ohne=("-z",))
    for z in (0.3, 0.62, 0.92):
        netz.quader((x, y, z), (0.45, 0.45, 0.06), st["Eisen"])
    netz.quader((x, y - 0.1, 1.003), (0.16, 0.035, 0.008), st["Kohle"], ohne=("-z",))
    netz.quader((x, y - 0.235, 0.78), (0.08, 0.03, 0.1), st["Eisen"])
    netz.stab((x - 0.03, y - 0.25, 0.83), (x + 0.03, y - 0.25, 0.83), 0.04, 0.012, st["Eisen"], quer=(0, 0, 1))


# ─────────────────────────────────────────────────────────────────────────────
#  Treppe in die Krypta
# ─────────────────────────────────────────────────────────────────────────────

TR_GRENZE = -1.6                     # bis hier gehören Stufen und Schacht zum Schiff, darunter zur Krypta
TR_LOCH_Y = 3.6                      # so weit ist der Boden des Schiffs über der Treppe offen


def treppe_krypta(netz, st, oben):
    """Steinerne Treppe im Schacht rechts im Schiff, abwärts nach +y bis zum Podest vor dem Bogen in die Krypta. `oben`: was
    vom Schiff aus zu sehen ist (Stufen und Schacht bis TR_GRENZE, Brüstung, die Unterseite des Bodens hinter dem Loch), sonst
    der untere Teil mit Podest, Bogen und Decke."""
    lauf = (TR_Y1 - TR_Y0) / (TR_STUFEN - 1)
    hub = -Z_KRYPTA / TR_STUFEN
    alle, netz.alle = netz.alle, True
    for k in range(TR_STUFEN):
        z = -(k + 1) * hub
        y = TR_Y0 + k * lauf
        if (z + hub > TR_GRENZE) != oben:
            continue
        netz.vieleck_nach([(TR_X0, y, z + hub), (TR_X1, y, z + hub), (TR_X1, y, z), (TR_X0, y, z)], (0, -1, 0),
                          st["Sandstein" if k % 2 else "Haustein"])
        if k < TR_STUFEN - 1:
            ia.waagrecht(netz, (TR_X0, TR_X1, y, y + lauf), z, True, st["Platte" if k % 2 else "Platte hell"])
    zb = TR_GRENZE if oben else Z_KRYPTA
    zt = 0.0 if oben else TR_GRENZE
    y_end = PODEST_Y1
    for x, n in ((TR_X0, (1, 0, 0)), (TR_X1, (-1, 0, 0))):
        if x == TR_X0 and not oben:                                      # unten die Öffnung zum Bogen in die Krypta
            y0_, y1_, hb = KRYPTATUER
            for ya, yb_, za, zb_ in ((TR_Y0, y0_, zb, zt), (y1_, y_end, zb, zt), (y0_, y1_, Z_KRYPTA + hb, zt)):
                if zb_ - za < 1e-3:
                    continue
                netz.vieleck_nach([(x, ya, za), (x, yb_, za), (x, yb_, zb_), (x, ya, zb_)], n, st["Bruchstein"])
            continue
        netz.vieleck_nach([(x, TR_Y0, zb), (x, y_end, zb), (x, y_end, zt), (x, TR_Y0, zt)], n, st["Bruchstein"])
    netz.vieleck_nach([(TR_X0, y_end, zb), (TR_X1, y_end, zb), (TR_X1, y_end, zt), (TR_X0, y_end, zt)], (0, -1, 0), st["Bruchstein"])
    if oben:
        netz.vieleck_nach([(TR_X0, TR_LOCH_Y, -0.22), (TR_X1, TR_LOCH_Y, -0.22), (TR_X1, TR_LOCH_Y, 0.0), (TR_X0, TR_LOCH_Y, 0.0)], (0, -1, 0),
                          st["Sandstein"])
        ia.waagrecht(netz, (TR_X0, TR_X1, TR_LOCH_Y, y_end), -0.22, False, st["Bruch dunkel"])
        netz.vieleck_nach([(TR_X0, TR_Y0, -hub), (TR_X1, TR_Y0, -hub), (TR_X1, TR_Y0, -1.0), (TR_X0, TR_Y0, -1.0)], (0, 1, 0), st["Bruchstein"])
        # Brüstung aus Haustein an der offenen Seite und am Ende, vorn ein Pfosten am Eingang
        d = 0.16
        for a, b, deckel in (((TR_X0 - d / 2, TR_Y0 + 0.39), (TR_X0 - d / 2, TR_LOCH_Y), (TR_LOCH_Y - 0.03,)),
                             ((TR_X0 - d, TR_LOCH_Y + d / 2), (TR_X1, TR_LOCH_Y + d / 2), (TR_X0 - d - 0.03,))):
            entlang_y = a[0] == b[0]
            netz.stab(Vector((a[0], a[1], 0.45)), Vector((b[0], b[1], 0.45)), 0.9, d, st["Haustein"], quer=(1, 0, 0) if entlang_y else (0, 1, 0))
            a2 = Vector((a[0], a[1], 0.93))
            b2 = Vector((b[0], deckel[0], 0.93)) if entlang_y else Vector((b[0], b[1], 0.93))
            if not entlang_y:
                a2.x = deckel[0]
            netz.stab(a2, b2, 0.06, d + 0.06, st["Sandstein"], quer=(1, 0, 0) if entlang_y else (0, 1, 0))
        netz.quader((TR_X0 - d / 2, TR_Y0 + 0.27, 0.55), (0.24, 0.24, 1.1), st["Sandstein"], ohne=("-z",))
        drehkoerper(netz, st, Vector((TR_X0 - d / 2, TR_Y0 + 0.27, 1.1)), [(0.0, 0.1), (0.1, 0.09), (0.16, 0.0)], "Sandstein", n=8)
    else:
        ia.waagrecht(netz, (TR_X0, TR_X1, TR_LOCH_Y, y_end), -0.226, False, st["Bruch dunkel"])
        ia.waagrecht(netz, (TR_X0, TR_X1, TR_Y0 + (TR_STUFEN - 1) * lauf, y_end), Z_KRYPTA, True, st["Platte dunkel"])
        y0_, y1_, hb = KRYPTATUER                                        # Laibung durch die Wand der Krypta, Sturz aus Sandstein
        for y, s in ((y0_, 1), (y1_, -1)):
            netz.vieleck_nach([(CX1, y, Z_KRYPTA), (TR_X0, y, Z_KRYPTA), (TR_X0, y, Z_KRYPTA + hb), (CX1, y, Z_KRYPTA + hb)], (0, s, 0),
                              st["Bruchstein dunkel"])
        netz.vieleck_nach([(CX1, y0_, Z_KRYPTA + hb), (TR_X0, y0_, Z_KRYPTA + hb), (TR_X0, y1_, Z_KRYPTA + hb), (CX1, y1_, Z_KRYPTA + hb)], (0, 0, -1),
                          st["Sandstein"])
        ia.waagrecht(netz, (CX1, TR_X0, y0_, y1_), Z_KRYPTA + 0.004, True, st["Platte"])
        netz.quader((CX1 + 0.03, (y0_ + y1_) / 2, Z_KRYPTA + hb + 0.1), (0.1, y1_ - y0_ + 0.3, 0.2), st["Sandstein"], ohne=("-z",))
    netz.alle = alle


# ─────────────────────────────────────────────────────────────────────────────
#  Krypta
# ─────────────────────────────────────────────────────────────────────────────

def joche_krypta():
    """Die sechs Joche der Krypta (x0, x1, y0, y1): zwei quer, drei längs, getrennt an den Säulen."""
    xs = (CX0, SAEULE_X, CX1)
    ys = (CY0,) + SAEULE_Y + (CY1,)
    return [(xs[i], xs[i + 1], ys[j], ys[j + 1]) for i in range(2) for j in range(3)]


def welt_s(w, u):
    """Weltkoordinate entlang der Wand (x bei Wänden entlang x, sonst y) zur Lage u."""
    p = w.a + w.d * u
    return p.x if abs(w.d.x) > 0.5 else p.y


def krypta(netz, decke, st, anker):
    """Krypta: Steinplatten, Wände aus Bruchstein bis unter die Kappen (Schildbögen), sechs Kreuzgratgewölbe aus gekalktem Putz,
    Gurtbögen und zwei Säulen; in der linken Wand ein Lichtschacht, in der rechten hinten der Bogen zur Treppe. Grabplatten im
    Boden, eine Tumba hinten links, zwei Laternen. Stellplätze Lager_1–8 in zwei Reihen zwischen den Säulen."""
    z0 = Z_KRYPTA
    steinplatten(netz, st, CX0, CX1, CY0, CY1, z=z0, saat=81, breite=0.55, stoffe=("Platte dunkel", "Platte", "Platte dunkel", "Platte warm"))
    kanten = ecken(CX0, CX1, CY0, CY1)
    hoehen = {}
    for x0, x1, y0, y1 in joche_krypta():
        hx, hy = gewoelbe_joch(decke, st, x0, x1, y0, y1, z0 + KAEMPFER, STICH, saat=int(x0 * 10 + y0 * 7))
        hoehen[(x0, x1, y0, y1)] = (hx, hy)
    schacht = (CY0 + (CY1 - CY0) / 2 - 0.22, CY0 + (CY1 - CY0) / 2 + 0.22, KAEMPFER + 0.15, KAEMPFER + 0.62)
    for seite in ("vorne", "hinten", "links", "rechts"):
        w = wand(*kanten[seite], boden=z0)
        entlang_x = seite in ("vorne", "hinten")
        grenzen = (CX0, SAEULE_X, CX1) if entlang_x else (CY0,) + SAEULE_Y + (CY1,)
        for g0, g1 in zip(grenzen, grenzen[1:]):
            halb, mitte = (g1 - g0) / 2, (g0 + g1) / 2
            h = stich(halb, STICH)
            ua, ub = sorted((u_von(w, g0), u_von(w, g1)))
            loecher = []
            if seite == "links" and g0 < schacht[0] < g1:
                loecher.append(tuple(sorted((u_von(w, schacht[0]), u_von(w, schacht[1])))) + schacht[2:])
            if seite == "rechts" and g0 < KRYPTATUER[0] < g1:
                loecher.append(tuple(sorted((u_von(w, KRYPTATUER[0]), u_von(w, KRYPTATUER[1])))) + (0.0, KRYPTATUER[2]))
            schildbogen_wand(netz, st, w, ua, ub, KAEMPFER, lambda u, w=w, h=h, mitte=mitte: h(welt_s(w, u) - mitte), loecher, n=10)
    # Lichtschacht in der linken Wand: schräge Laibung nach oben, dahinter das Licht
    ya, yb, va, vb = schacht
    x = CX0
    t = 0.7
    alle, netz.alle = netz.alle, True
    netz.vieleck_nach([(x, ya, z0 + va), (x, yb, z0 + va), (x - t, yb, z0 + vb + 0.25), (x - t, ya, z0 + vb + 0.25)], (1, 0, 1), st["Bruch hell"])
    netz.vieleck_nach([(x, ya, z0 + vb), (x, yb, z0 + vb), (x - t, yb, z0 + vb + 0.25), (x - t, ya, z0 + vb + 0.25)], (1, 0, -1), st["Bruch"])
    for y, s in ((ya, 1), (yb, -1)):
        netz.vieleck_nach([(x, y, z0 + va), (x - t, y, z0 + vb + 0.25), (x, y, z0 + vb)], (0, s, 0), st["Bruch"])
    netz.vieleck_nach([(x - t, ya, z0 + vb + 0.25), (x - t, yb, z0 + vb + 0.25), (x - t * 0.45, yb, z0 + (va + vb) / 2 + 0.2),
                       (x - t * 0.45, ya, z0 + (va + vb) / 2 + 0.2)], (1, 0, 0), st["Fenster"])
    for k in (0.33, 0.66):
        y = ya + (yb - ya) * k
        netz.stab((x - 0.06, y, z0 + va + 0.03), (x - 0.06, y, z0 + vb - 0.02), 0.025, 0.025, st["Eisen"])
    netz.alle = alle
    anker.append(licht(Vector((x + 0.15, (ya + yb) / 2, z0 + (va + vb) / 2)), (1, 0, 0), "Fenster"))
    # Gurtbögen und Säulen
    for (x0, x1, y0, y1), (hx, hy) in hoehen.items():
        if x1 == SAEULE_X:
            gurtbogen(netz, st, (SAEULE_X, y0), (SAEULE_X, y1), z0 + KAEMPFER, hy, (1, 0))
        if y1 in SAEULE_Y:
            gurtbogen(netz, st, (x0, y1), (x1, y1), z0 + KAEMPFER, hx, (0, 1))
    for y in SAEULE_Y:
        saeule(netz, st, (SAEULE_X, y, z0), KAEMPFER)
    # Grabplatten im Boden (wenige Millimeter über den Platten)
    for k, (x, y, dreh) in enumerate(((0.95, 4.35, 0), (-1.55, 2.05, 90), (0.95, 2.05, 90))):
        grabplatte(netz, st, Vector((x, y, z0 + 0.004)), dreh, k)
    tumba(netz, st, Vector((-1.6, 4.55, z0)))
    for p in (Vector((-1.6, 2.8, z0 + 1.95)), Vector((0.85, 3.6, z0 + 1.95))):
        ti.laterne(netz, st, p, z0 + KAEMPFER + STICH - 0.05)
        anker.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"))
    nummer = 1
    for y in (1.3, 2.85):
        for x in (-1.95, -1.15, 0.45, 1.25):
            anker.append(bs.anker(f"Lager_{nummer}", (x, y, z0)))
            nummer += 1


def grabplatte(netz, st, mitte, dreh, art):
    """Grabplatte im Boden: Sandstein mit eingetieftem Rand, Kreuz und Schriftzeilen (dunkle Linien)."""
    M = Matrix.Translation(mitte) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    L, B = 1.7, 0.8
    netz.quader((0, 0, 0.0), (B, L, 0.008), st["Sandstein"], M, ohne=("-z",))
    s = st["Grabschrift"]

    def linie(a, b, breite=0.02):
        a, b = Vector((a[0], a[1], 0.0065)), Vector((b[0], b[1], 0.0065))
        q = (b - a).normalized().cross(Vector((0, 0, 1))) * breite / 2
        netz.vieleck_nach([M @ (a - q), M @ (b - q), M @ (b + q), M @ (a + q)], (0, 0, 1), s)
    for a, b in (((-B / 2 + 0.06, -L / 2 + 0.06), (B / 2 - 0.06, -L / 2 + 0.06)), ((-B / 2 + 0.06, L / 2 - 0.06), (B / 2 - 0.06, L / 2 - 0.06))):
        linie(a, b)
    for a, b in (((-B / 2 + 0.06, -L / 2 + 0.07), (-B / 2 + 0.06, L / 2 - 0.07)), ((B / 2 - 0.06, -L / 2 + 0.07), (B / 2 - 0.06, L / 2 - 0.07))):
        linie(a, b)
    linie((0, -0.35), (0, 0.185), 0.03)
    linie((0, 0.215), (0, 0.45), 0.03)
    linie((-0.17, 0.2), (0.17, 0.2), 0.03)
    zufall = random.Random(art)
    for k in range(3 + art % 2):
        y = -L / 2 + 0.16 + 0.07 * k
        laenge = zufall.uniform(0.3, 0.55)
        linie((-laenge / 2, y), (laenge / 2, y), 0.012)


def tumba(netz, st, fuss):
    """Tumba: steinerner Sarkophag mit Blendbögen an den Seiten, darauf die liegende Gestalt eines Stifters (Kopfkissen,
    gefaltete Hände, ein Hund zu Füßen)."""
    f = Vector(fuss)
    L, B, H = 2.0, 0.9, 0.85
    netz.quader(f + Vector((0, 0, 0.05)), (B + 0.12, L + 0.12, 0.1), st["Sandstein"], ohne=("-z",))
    netz.quader(f + Vector((0, 0, 0.1 + (H - 0.2) / 2)), (B, L, H - 0.2), st["Haustein"], ohne=("-z", "+z"))
    netz.quader(f + Vector((0, 0, H - 0.05)), (B + 0.1, L + 0.1, 0.1), st["Sandstein"])
    for k in range(4):                                                      # Blendbögen an der Langseite (zum Raum)
        y = f.y - L / 2 + L * (k + 0.5) / 4
        for x, s in ((f.x + B / 2 + 0.003, 1), (f.x - B / 2 - 0.003, -1)):
            pts = pr.bogen_punkte(0.0, 0.18, 0.3, 0.42, 3)
            netz.vieleck_nach([Vector((x, y + p[0] * s, f.z + p[1])) for p in pts], (s, 0, 0), st["Bruchstein dunkel"])
    o = f + Vector((0, 0, H))
    netz.quader(o + Vector((0, L / 2 - 0.22, 0.05)), (0.36, 0.22, 0.1), st["Sandstein"])           # Kissen
    drehkoerper(netz, st, o + Vector((0, L / 2 - 0.3, 0.1)), [(0.0, 0.09), (0.08, 0.1), (0.16, 0.06), (0.2, 0.0)], "Haustein", n=8)
    netz.quader(o + Vector((0, 0.05, 0.08)), (0.42, 1.2, 0.16), st["Haustein"])                    # Gewand
    netz.quader(o + Vector((0, 0.32, 0.22)), (0.08, 0.06, 0.16), st["Haustein"])                   # gefaltete Hände
    netz.quader(o + Vector((0, -L / 2 + 0.3, 0.09)), (0.24, 0.32, 0.18), st["Haustein"])            # Hund


# ─────────────────────────────────────────────────────────────────────────────
#  Sakristei: Wachsherd, Ziehgestell, Bütte, Leinen, Lumpen, Farbtisch
# ─────────────────────────────────────────────────────────────────────────────

def wachsherd(netz, st, anker):
    """Wachsherd an der Rückwand: gemauerter Herd mit Feuertür zur Kamera, darauf der kupferne Wachstrog (lang entlang x, Rand
    figuren.KERZEN „rand“), darin das Wachs (Werkstueck_1); darüber ein Rauchfang aus verrußtem Lehm bis in die Decke, an seiner
    Kante hängen fertige Kerzenbündel. Gibt (Werkstueck_1, Ort für Dampf_1) zurück."""
    k = fg.KERZEN
    t = TROG
    hb, ht = 0.62, 0.3                                                       # Trog innen: halbe Länge, Tiefe (y)
    herd_h = k["rand"] - 0.3
    glut, n, M = ai.ofen_mit_tuer(netz, st, Vector((t.x, t.y, 0)), 1.5, 0.5, herd_h, -0.35, (0.1, 0.34, 0.3), vorn=(0, -1))
    anker.append(licht(glut, (n.x, n.y, 0), "Glut"))
    netz.quader((t.x, t.y, herd_h + 0.015), (1.56, 0.56, 0.03), st["Sandstein"], ohne=("-z",))
    boden = herd_h + 0.03
    aussen = [(hb + 0.03, ht / 2 + 0.03), (-hb - 0.03, ht / 2 + 0.03), (-hb - 0.03, -ht / 2 - 0.03), (hb + 0.03, -ht / 2 - 0.03)]
    innen = [(hb, ht / 2), (-hb, ht / 2), (-hb, -ht / 2), (hb, -ht / 2)]
    for i in range(4):                                                      # Trog: außen, Rand, innen
        a, b = aussen[i], aussen[(i + 1) % 4]
        ia_, ib = innen[i], innen[(i + 1) % 4]
        nn = Vector(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, 0))
        netz.vieleck_nach([(t.x + a[0], t.y + a[1], boden), (t.x + b[0], t.y + b[1], boden), (t.x + b[0], t.y + b[1], k["rand"]),
                           (t.x + a[0], t.y + a[1], k["rand"])], nn, st["Kupferblech"])
        netz.vieleck_nach([(t.x + a[0], t.y + a[1], k["rand"]), (t.x + b[0], t.y + b[1], k["rand"]), (t.x + ib[0], t.y + ib[1], k["rand"]),
                           (t.x + ia_[0], t.y + ia_[1], k["rand"])], (0, 0, 1), st["Kupferblech"])
        netz.vieleck_nach([(t.x + ia_[0], t.y + ia_[1], k["wachs"] - 0.05), (t.x + ib[0], t.y + ib[1], k["wachs"] - 0.05),
                           (t.x + ib[0], t.y + ib[1], k["rand"]), (t.x + ia_[0], t.y + ia_[1], k["rand"])], -nn, st["Kupferblech"])
    for s in (-1, 1):                                                       # Griffe an den Enden
        netz.stab((t.x + s * (hb + 0.03), t.y - 0.08, k["rand"] - 0.05), (t.x + s * (hb + 0.12), t.y, k["rand"] - 0.03), 0.02, 0.02, st["Eisen"])
        netz.stab((t.x + s * (hb + 0.12), t.y, k["rand"] - 0.03), (t.x + s * (hb + 0.03), t.y + 0.08, k["rand"] - 0.05), 0.02, 0.02, st["Eisen"])
    # Rauchfang: von 2,2 m bis in die Decke, verjüngt, an der Rückwand
    za, y0 = 2.2, t.y - 0.42
    x0, x1 = t.x - 0.95, t.x + 0.95
    netz.quader(((x0 + x1) / 2, y0, za - 0.06), (x1 - x0 + 0.1, 0.1, 0.12), st["Holz alt"])
    netz.vieleck_nach([(x0, y0, za), (x1, y0, za), (x1 - 0.4, SY1 - 0.3, H_SAK), (x0 + 0.4, SY1 - 0.3, H_SAK)], (0, -1, 0.7), st["Rauchfang"])
    for x, xo, s in ((x0, x0 + 0.4, -1), (x1, x1 - 0.4, 1)):
        netz.vieleck_nach([(x, y0, za), (x, SY1, za), (xo, SY1, H_SAK), (xo, SY1 - 0.3, H_SAK)], (s, -0.2, 0.3), st["Rauchfang"])
    alle, netz.alle = netz.alle, True
    netz.vieleck_nach([(x0, y0, za), (x1, y0, za), (x1, SY1, za), (x0, SY1, za)], (0, 0, -1), st["Russ"])
    for j, x in enumerate((x0 + 0.25, x0 + 0.55, x1 - 0.55, x1 - 0.25)):    # Kerzenbündel am Haken
        netz.stab((x, y0 - 0.05, za - 0.12), (x, y0 - 0.05, za - 0.3), 0.012, 0.012, st["Seil"])
        for i in range(5):
            dx = (i - 2) * 0.022
            drehkoerper(netz, st, Vector((x + dx, y0 - 0.05 + 0.01 * (i % 2), za - 0.3 - 0.3 - 0.02 * (i % 3))),
                        [(0.0, 0.006), (0.02, 0.012), (0.3, 0.01)], "Kerze", n=5)
    netz.alle = alle
    werkstueck = bs.Netz("Werkstueck_1")
    ia.waagrecht(werkstueck, (t.x - hb, t.x + hb, t.y - ht / 2, t.y + ht / 2), k["wachs"], True, st["Fluessigkeit"])
    return am_ort(werkstueck, Vector((t.x, t.y, k["wachs"]))), Vector((t.x, t.y, k["rand"] + 0.02))


def kerzen_reihe(netz, st, mitte, laenge, anzahl, lang, zufall):
    """Kerzen an einem Stab entlang x um `mitte`: Docht, Kerze (unten dicker, mit Tropfnase)."""
    for i in range(anzahl):
        x = mitte.x - laenge / 2 + laenge * (i + 0.5) / anzahl
        l = lang * zufall.uniform(0.85, 1.0)
        oben = Vector((x, mitte.y, mitte.z - 0.012))
        netz.stab(oben, oben - Vector((0, 0, 0.03)), 0.005, 0.005, st["Seil"])
        f = oben - Vector((0, 0, 0.03 + l))
        drehkoerper(netz, st, f, [(0.0, 0.0), (0.015, 0.014), (l - 0.01, 0.011), (l, 0.006)], "Kerze", n=5)


def ziehgestell(netz, st):
    """Ziehgestell an der linken Wand: zwei Böcke mit Holmen entlang y, darauf fünf Stäbe mit frischen Kerzen (eigene Knoten
    Kerzenstange_<n>, sie pendeln), darunter ein Tropfbrett mit Wachstropfen; dazu drei Tropfen (Tropfen_Kerzen_<n>), die in
    der Animation von den Kerzen fallen. Gibt (Stäbe, Tropfen) zurück."""
    xa, xb = SX0 + 0.25, SX0 + 0.85
    ya, yb = 1.35, 3.5
    z = 1.85
    holz = st["Holz"]
    for y in (ya, yb):
        for x in (xa, xb):
            netz.stab((x, y - 0.25, 0.0), (x, y, z), 0.07, 0.07, holz, quer=(1, 0, 0))
            netz.stab((x + 0.004, y + 0.25, 0.0), (x + 0.004, y, z), 0.07, 0.056, holz, quer=(1, 0, 0))
        netz.stab((xa - 0.05, y, 0.9), (xb + 0.05, y, 0.9), 0.06, 0.05, holz)
    for x in (xa, xb):
        netz.quader((x, (ya + yb) / 2, z + 0.03), (0.07, yb - ya + 0.2, 0.07), st["Holz alt"])
    netz.quader(((xa + xb) / 2, (ya + yb) / 2, 0.03), (xb - xa + 0.25, yb - ya - 0.1, 0.03), st["Holz alt"], ohne=("-z",))
    zufall = random.Random(17)
    for k in range(18):                                                     # Wachstropfen auf dem Brett
        p = Vector((zufall.uniform(xa - 0.05, xb + 0.05), zufall.uniform(ya + 0.1, yb - 0.1), 0.046))
        scheibe(netz, st, p, zufall.uniform(0.01, 0.025), 0.046, "Kerze", n=5)
    staebe, tropfen = [], []
    for k in range(5):
        y = ya + 0.25 + (yb - ya - 0.5) * k / 4
        m = Vector(((xa + xb) / 2, y, z + 0.08))
        stab = bs.Netz(f"Kerzenstange_{k + 1}", alle=True)
        stab.stab(m - Vector((0.42, 0, 0)), m + Vector((0.42, 0, 0)), 0.024, 0.024, st["Holz"], quer=(0, 0, 1))
        kerzen_reihe(stab, st, m, 0.48, 6, 0.32 + 0.04 * (k % 2), zufall)
        staebe.append(am_ort(stab, m))
    for k, (i, j) in enumerate(((0, 1), (2, 4), (4, 2))):
        y = ya + 0.25 + (yb - ya - 0.5) * i / 4
        x = (xa + xb) / 2 - 0.24 + 0.48 * (j + 0.5) / 6
        t = bs.Netz(f"Tropfen_Kerzen_{k + 1}", alle=True)
        t0 = Vector((x, y, z + 0.08 - 0.012 - 0.03 - 0.3))
        drehkoerper(t, st, t0, [(-0.012, 0.0), (-0.006, 0.008), (0.003, 0.006), (0.01, 0.0)], "Kerze", n=5)
        tropfen.append(am_ort(t, t0))
    return staebe, tropfen


def kerzen_bewegung(staebe, tropfen):
    """Die Stäbe pendeln um ihre Achse (±2,5°, je Stab versetzt, eine Schwingung je Schleife); die Tropfen wachsen an der
    Kerzenspitze und fallen aufs Brett (je einmal, zu verschiedenen Zeiten). Bei Zeit 0 sind die Tropfen nicht zu sehen."""
    bewegt = []
    for k, o in enumerate(staebe):
        ruhe = o.rotation_euler.copy()

        def pendel(bild, ruhe=ruhe, k=k):
            w = math.radians(2.5) * math.sin(2 * math.pi * bild / ARBEIT_BILDER + 1.3 * k)
            return None, (ruhe.x + w, ruhe.y, ruhe.z), None
        bewegt.append((o, pendel))
    for k, o in enumerate(tropfen):
        ruhe = o.location.copy()
        fallen = ruhe.z - 0.05
        anfang = 0.4 + 1.1 * k

        def tropf(bild, ruhe=ruhe, fallen=fallen, anfang=anfang):
            a = bild / FPS - anfang
            if 0 <= a < 0.9:
                s = 0.3 + 0.7 * fg.weich(a / 0.9)
                return ruhe, None, (s, s, s * (1 + 0.3 * fg.weich(a / 0.9)))
            if 0.9 <= a < 1.25:
                f = (a - 0.9) / 0.35
                return ruhe - Vector((0, 0, fallen * f * f)), None, (0.9, 0.9, 1.3)
            return ruhe, None, (0.01, 0.01, 0.01)
        o.scale = (0.01, 0.01, 0.01)
        bewegt.append((o, tropf))
    return bewegt


def buette(netz, st):
    """Papierbütte: Bottich aus Dauben mit Eisenreifen auf einem Sockel (Rand figuren.BUETTE „rand“), darin der Faserbrei
    (Werkstueck_2), auf dem Rand das Brett mit dem Gautschbock, an der Seite zwei Schöpfformen. Vier Tropfen (Tropfen_Sieb_<n>)
    fallen in der Animation aus dem Sieb, das die Figur über der Bütte hält. Gibt (Werkstueck_2, Tropfen) zurück."""
    b = fg.BUETTE
    m = BUETTE
    hx, hy = 0.5, 0.33                                                      # innen: halbe Breite (x), halbe Tiefe (y)
    n = 16
    holz, eisen = st["Holz alt"], st["Eisen"]

    def rand(r, z, k):
        w = 2 * math.pi * k / n
        return Vector((m.x + (hx + r) * math.cos(w), m.y + (hy + r) * math.sin(w), z))
    alle, netz.alle = netz.alle, True
    for k in range(n):
        j = (k + 1) % n
        aus = Vector((math.cos(2 * math.pi * (k + 0.5) / n), math.sin(2 * math.pi * (k + 0.5) / n), 0))
        netz.vieleck_nach([rand(0.05, 0.0, k), rand(0.05, 0.0, j), rand(0.06, b["rand"], j), rand(0.06, b["rand"], k)], aus,
                          st["Holz alt" if k % 2 else "Holz"])
        netz.vieleck_nach([rand(0.06, b["rand"], k), rand(0.06, b["rand"], j), rand(0.0, b["rand"], j), rand(0.0, b["rand"], k)], (0, 0, 1), holz)
        netz.vieleck_nach([rand(0.0, b["brei"] - 0.1, k), rand(0.0, b["brei"] - 0.1, j), rand(0.0, b["rand"], j), rand(0.0, b["rand"], k)], -aus, holz)
        for z in (0.15, b["rand"] - 0.14):
            netz.vieleck_nach([rand(0.058, z, k), rand(0.058, z, j), rand(0.062, z + 0.05, j), rand(0.062, z + 0.05, k)], aus, eisen)
    netz.alle = alle
    werkstueck = bs.Netz("Werkstueck_2")
    werkstueck.vieleck_nach([rand(0.0, b["brei"], k) for k in range(n)], (0, 0, 1), st["Fluessigkeit"])
    ws = am_ort(werkstueck, Vector((m.x, m.y, b["brei"])))
    # zwei Schöpfformen lehnen an der linken Seite, rechts der Gautschbock mit Filzen und Bögen
    for k in range(2):
        x = m.x - hx - 0.18 - 0.05 * k
        a = Vector((x, m.y - 0.25 + 0.1 * k, 0.0))
        e = a + Vector((0.12, 0, 0.62))
        q = Vector((0, 0.4, 0))
        netz.vieleck_nach([a, a + q, e + q, e], (-1, 0, 0.2), st["Holz"])
        netz.vieleck_nach([a + Vector((0.01, 0.03, 0.05)), a + q + Vector((0.01, -0.03, 0.05)), e + q + Vector((0.01, -0.03, -0.05)),
                           e + Vector((0.01, 0.03, -0.05))], (-1, 0, 0.2), st["Siebgitter"])
    g = Vector((m.x + 1.15, m.y + 0.05, 0))
    netz.quader(g + Vector((0, 0, 0.3)), (0.62, 0.48, 0.6), holz)
    for i in range(9):
        netz.quader(g + Vector((0, 0, 0.62 + 0.024 * i)), (0.5 - 0.004 * i, 0.36, 0.012), st["Leinen" if i % 2 else "Papierbogen"])
    for s in (-1, 1):
        netz.stab(g + Vector((s * 0.36, 0, 0.0)), g + Vector((s * 0.36, 0, 1.42)), 0.08, 0.08, st["Holz"])
    netz.stab(g + Vector((-0.42, 0, 1.4)), g + Vector((0.42, 0, 1.4)), 0.1, 0.1, st["Holz"])
    netz.stab(g + Vector((0, 0, 1.35)), g + Vector((0, 0, 0.95)), 0.06, 0.06, st["Holz alt"])           # Spindel des Gautschbocks
    netz.quader(g + Vector((0, 0, 0.92)), (0.54, 0.4, 0.05), st["Holz"])
    tropfen = []
    hoch = b["rand"] + b["ueber"] - 0.005
    zufall = random.Random(23)
    for k in range(4):
        t0 = Vector((W2.x + zufall.uniform(-0.15, 0.15), W2.y - b["tropf"] + zufall.uniform(-0.08, 0.08), hoch))
        t = bs.Netz(f"Tropfen_Sieb_{k + 1}", alle=True)
        drehkoerper(t, st, t0, [(-0.01, 0.0), (-0.005, 0.006), (0.003, 0.005), (0.008, 0.0)], "Wasser", n=5)
        tropfen.append(am_ort(t, t0))
    return ws, tropfen


def sieb_tropfen(tropfen):
    """Tropfen aus dem Sieb: von 0,66 bis 0,98 der Schleife – solange die Figur am Platz 2 das Sieb über der Bütte abtropfen
    lässt (figuren.schoepf_ziel) – fallen sie versetzt in den Brei; sonst unsichtbar."""
    bewegt = []
    tief = fg.BUETTE["ueber"] + fg.BUETTE["rand"] - fg.BUETTE["brei"]
    for k, o in enumerate(tropfen):
        ruhe = o.location.copy()
        starts = (0.66 + 0.06 * k, 0.66 + 0.06 * k + 0.14)

        def fall(bild, ruhe=ruhe, starts=starts):
            ph = bild / ARBEIT_BILDER
            for s in starts:
                a = (ph - s) / 0.08
                if 0 <= a < 1 and s + 0.08 <= 0.99:
                    return ruhe - Vector((0, 0, tief * a * a)), None, (1.0, 1.0, 1.0 + 0.6 * a)
            return ruhe, None, (0.01, 0.01, 0.01)
        o.scale = (0.01, 0.01, 0.01)
        bewegt.append((o, fall))
    return bewegt


def lumpenkorb(netz, st, fuss):
    """Weidenkorb voller Lumpen (für den Faserbrei), daneben ein Bündel."""
    f = Vector(fuss)
    drehkoerper(netz, st, f, [(0.0, 0.26), (0.5, 0.33), (0.5, 0.31)], "Geflecht", n=10)
    drehkoerper(netz, st, f, [(0.3, 0.29), (0.5, 0.31)], "Geflecht", n=10, innen=True)
    zufall = random.Random(5)
    for k in range(7):
        p = f + Vector((zufall.uniform(-0.18, 0.18), zufall.uniform(-0.18, 0.18), 0.46))
        drehkoerper(netz, st, p, [(0.0, 0.1), (0.07, 0.09), (0.12, 0.0)], zufall.choice(("Lumpen", "Leinen", "Sack", "Lumpen")), n=6,
                    dreh=zufall.uniform(0, 1))
    drehkoerper(netz, st, f + Vector((0.45, 0.1, 0)), [(0.0, 0.18), (0.22, 0.2), (0.32, 0.12), (0.38, 0.0)], "Lumpen", n=7)


def trockenleinen(netz, st):
    """Leinen unter der Decke über dem vorderen Teil der Sakristei, darüber gehängte Bögen Papier (beidseitig)."""
    alle, netz.alle = netz.alle, True
    z = H_SAK - 0.55
    zufall = random.Random(29)
    for y in (0.45, 1.05):
        netz.stab((SX0 + 0.6, y, z), (SX1 - 1.2, y, z), 0.012, 0.012, st["Seil"])
        x = SX0 + 0.8
        while x < SX1 - 1.6:
            b = zufall.uniform(0.3, 0.36)
            if zufall.random() < 0.85:
                h = zufall.uniform(0.22, 0.28)
                for s in (-1, 1):
                    netz.vieleck_nach([(x, y, z), (x + b, y, z), (x + b, y + s * 0.012, z - h), (x, y + s * 0.012, z - h)], (0, s, 0.05),
                                      st["Papierbogen"])
            x += b + zufall.uniform(0.04, 0.1)
    netz.alle = alle


def paramentenschrank(netz, st, M):
    """Schrank für die Gewänder (örtlich +y in den Raum): Korpus, zwei Türen mit Eisenbändern, oben ein Gesims."""
    si.q(netz, st, M, (0, 0.3, 1.0), (1.2, 0.6, 2.0), "Kirchenholz", ohne=("-y",))
    si.q(netz, st, M, (0, 0.32, 2.05), (1.3, 0.66, 0.1), "Kirchenholz hell", ohne=("-y",))
    for s in (-1, 1):
        si.q(netz, st, M, (s * 0.29, 0.605, 1.05), (0.54, 0.01, 1.8), "Kirchenholz hell", ohne=("-y",))
        for z in (0.4, 1.7):
            si.q(netz, st, M, (s * 0.25, 0.612, z), (0.46, 0.008, 0.05), "Eisen", ohne=("-y",))
        si.q(netz, st, M, (s * 0.04, 0.62, 1.05), (0.03, 0.02, 0.12), "Eisen", ohne=("-y",))


def farbtisch(netz, st, M):
    """Gekauft (Einrichtung_Gaensekiele): Farbtisch an der Rückwand – ein Glas voller Gänsekiele, Federmesser, Lapislazuli in
    einer Schale, der Reibstein mit dem Läufer und blauem Pigment, Muscheln mit Farben, ein Bündel frischer Federn."""
    holz = st["Holz"]
    si.q(netz, st, M, (0, 0.32, 0.86), (1.0, 0.56, 0.05), "Holz")
    for x in (-0.44, 0.44):
        for y in (0.08, 0.56):
            netz.stab(M @ Vector((x, y, 0)), M @ Vector((x, y, 0.84)), 0.05, 0.05, holz)
    o = 0.885
    g = M @ Vector((-0.3, 0.3, o))
    drehkoerper(netz, st, g, [(0.0, 0.05), (0.14, 0.055), (0.14, 0.048)], "Krugton", n=8)
    zufall = random.Random(3)
    for k in range(7):                                                       # Gänsekiele im Glas
        w = 2 * math.pi * k / 7
        a = g + Vector((0.02 * math.cos(w), 0.02 * math.sin(w), 0.1))
        e = a + Vector((0.07 * math.cos(w), 0.07 * math.sin(w), 0.28 + 0.04 * zufall.random()))
        netz.stab(a, e, 0.006, 0.006, st["Federkiel"])
        d = (e - a).normalized()
        q = d.cross(Vector((0, 0, 1))).normalized() * 0.02
        netz.vieleck(([e - d * 0.2, e - d * 0.2 + q, e + q * 0.4, e]), st["Federweiss"], immer=True)
        netz.vieleck(list(reversed([e - d * 0.2, e - d * 0.2 + q, e + q * 0.4, e])), st["Federweiss"], immer=True)
    s = M @ Vector((0.05, 0.25, o))                                          # Schale mit Lapislazuli
    drehkoerper(netz, st, s, [(0.0, 0.05), (0.04, 0.09), (0.05, 0.09)], "Krugton", n=8)
    for k in range(5):
        p = s + Vector((zufall.uniform(-0.04, 0.04), zufall.uniform(-0.04, 0.04), 0.04))
        netz.quader((0, 0, 0), (0.03, 0.025, 0.025), st["Lapis"], Matrix.Translation(p) @ Matrix.Rotation(zufall.uniform(0, 1.5), 4, "Z"))
    r = M @ Vector((0.3, 0.32, o))                                           # Reibstein mit Läufer und Ultramarin
    si.q(netz, st, M, (0.3, 0.32, o + 0.015), (0.28, 0.24, 0.03), "Sandstein")
    scheibe(netz, st, r, 0.07, o + 0.032, "Lapis", n=8)
    drehkoerper(netz, st, r + Vector((0.05, 0.02, 0.032)), [(0.0, 0.035), (0.05, 0.03), (0.08, 0.015)], "Sandstein", n=6)
    for k, f in enumerate(("Lapis", "Weihrot", "Gold", "Scheibe gruen")):     # Muscheln mit Farben
        p = M @ Vector((-0.05 + 0.08 * k, 0.48, o))
        drehkoerper(netz, st, p, [(0.0, 0.0), (0.0, 0.03), (0.012, 0.035)], "Federweiss", n=6)
        scheibe(netz, st, p, 0.025, o + 0.008, f, n=6)
    netz.stab(M @ Vector((0.1, 0.12, o + 0.005)), M @ Vector((0.24, 0.1, o + 0.005)), 0.012, 0.006, st["Eisen"])     # Federmesser


def werkzeug_sakristei(netz, st, M, stufe):
    """Gekauft an der Werkzeugwand: Gutes Werkzeug (Stufe 1: Dochtscheren, Gießkellen für Wachs, Messer, Dochtgarn) bzw.
    Meisterwerkzeug (Stufe 2: Glättsteine und Falzbeine für das Papier, ein Satz Messingformen, Pinsel)."""
    for k, (x, z) in enumerate(si.HAKEN):
        if (x < 0) != (stufe == 1):
            continue
        a = M @ Vector((x, 0.12, z + 0.02))
        unten = M.to_3x3() @ Vector((0, 0, -1))
        vorn = M.to_3x3() @ Vector((0, 1, 0))
        art = k % 4
        if stufe == 1 and art == 0:                                           # Gießkelle
            netz.stab(a, a + unten * 0.3, 0.02, 0.014, st["Holz"], quer=vorn)
            drehkoerper(netz, st, a + unten * 0.4, [(0.0, 0.0), (0.0, 0.06), (0.05, 0.065)], "Kupferblech", n=7)
        elif stufe == 1 and art == 1:                                         # Dochtschere
            for s in (-1, 1):
                netz.stab(a + M.to_3x3() @ Vector((s * 0.02, 0, 0)), a + unten * 0.2, 0.012, 0.008, st["Eisen"], quer=vorn)
        elif stufe == 1 and art == 2:                                         # Dochtgarn auf einer Spule
            drehkoerper(netz, st, a + unten * 0.16, [(0.0, 0.03), (0.02, 0.04), (0.1, 0.04), (0.12, 0.03)], "Leinen", n=6)
        elif stufe == 1:                                                      # Messer
            netz.stab(a, a + unten * 0.12, 0.03, 0.02, st["Holz alt"], quer=vorn)
            netz.stab(a + unten * 0.12, a + unten * 0.3, 0.032, 0.004, st["Eisen"], quer=vorn)
        elif art in (0, 2):                                                   # Glättstein bzw. Falzbein
            netz.stab(a, a + unten * 0.2, 0.04, 0.02, st["Federweiss" if art else "Sandstein"], quer=vorn)
        elif art == 1:                                                        # Pinsel
            netz.stab(a, a + unten * 0.24, 0.015, 0.015, st["Holz"])
            netz.stab(a + unten * 0.24, a + unten * 0.3, 0.025, 0.02, st["Haar"], quer=vorn)
        else:                                                                 # kleine Form aus Messing
            si.q(netz, st, M, (x, 0.13, z - 0.13), (0.18, 0.02, 0.14), "Messing")


# ─────────────────────────────────────────────────────────────────────────────
#  Skriptorium und Schatzkammer
# ─────────────────────────────────────────────────────────────────────────────

def fenster_loch(s, v, b, h):
    """Das Loch in der Wandfläche zu kirchenfenster(…, s, v, b, h) als Öffnung (Oe) für schmied_innen.wand."""
    return Oe(s, v - 0.1, b + 0.32, h + 0.1 + 0.144)


class Pult:
    """Örtliches System eines Schreibpults am Platz `platz` (Fußpunkt, Blick `blick`): Punkte (seitlich, vor, Höhe) wie in
    figuren.SCHREIBEN – die Höhe über der Sitzhöhe des Menschen –, die Platte ist um SCHREIBEN „neigung“ zur Figur geneigt."""

    def __init__(self, platz, blick):
        self.platz, self.blick = Vector(platz), blick
        self.vor = Vector((blick[0], blick[1], 0)).normalized()
        self.links = Vector((-self.vor.y, self.vor.x, 0))
        n = math.radians(fg.SCHREIBEN["neigung"])
        self.auf = self.vor * math.cos(n) + Vector((0, 0, 1)) * math.sin(n)      # auf der Platte nach oben (weg von der Figur)
        self.ab = -self.vor * math.sin(n) + Vector((0, 0, 1)) * math.cos(n)      # aus der Platte heraus
        self.sitz = fg.VOELKER["mensch"]["knie"] - 0.02

    def p(self, s, v, h):
        return self.platz + self.links * s + self.vor * v + Vector((0, 0, h + self.sitz))

    def lok(self, s, v, h):
        """System an einem Punkt (s, v, h): x seitlich (nach links der Figur), y nach vorn, z oben."""
        M = Matrix((self.links, self.vor, Vector((0, 0, 1)))).transposed().to_4x4()
        M.translation = self.p(s, v, h)
        return M

    def platte(self, s, u, n=0.0):
        """Punkt auf der Ebene des Blatts: `s` seitlich, `u` die Platte hinauf ab der Zeile (figuren.SCHREIBEN „blatt“), `n` darüber."""
        _, v0, h0 = fg.SCHREIBEN["blatt"]
        return self.p(s, v0, h0) + self.auf * u + self.ab * n

    def matrix(self, s, u, n):
        """System auf der Platte: x seitlich (nach links der Figur), y die Platte hinauf, z heraus."""
        M = Matrix((self.links, self.auf, self.ab)).transposed().to_4x4()
        M.translation = self.platte(s, u, n)
        return M


BUCH = (0.10, 0.04)                 # Mitte des aufgeschlagenen Buchs auf der Platte (seitlich, hinauf)


def schreibpult(netz, st, pult, buch=None, werkstueck=None):
    """Schreibpult (Pult): Hocker unter der Hüfte (Oberkante SITZ), zwei Wangen, die schräge Platte unter dem Blatt, eine
    Ablage mit Büchern, das Tintenhorn rechts in einem Loch der Platte. Das aufgeschlagene Buch kommt in `buch` (Farben) bzw.
    als Werkstueck_3 in `werkstueck` (Einband, Schnitt und Initiale im Material „Fluessigkeit“)."""
    holz, hell = st["Kirchenholz"], st["Kirchenholz hell"]
    f = pult.platz
    drehkoerper(netz, st, f + Vector((0, 0, SITZ - 0.05)), [(0.0, 0.17), (0.05, 0.17)], "Kirchenholz hell", n=8)
    for k in range(3):
        w = 2 * math.pi * k / 3 + 0.4
        netz.stab(f + Vector((0.14 * math.cos(w), 0.14 * math.sin(w), 0)), f + Vector((0.1 * math.cos(w), 0.1 * math.sin(w), SITZ - 0.05)),
                  0.04, 0.04, holz)
    netz.quader((0, 0, -0.015), (0.72, 0.52, 0.03), hell, pult.matrix(0.0, 0.03, -0.028))
    netz.quader((0, -0.255, 0.01), (0.72, 0.03, 0.04), holz, pult.matrix(0.0, 0.03, -0.028))     # Leiste unten
    for s in (-0.33, 0.33):                                                   # Wangen
        unten_v, oben = 0.3, pult.platte(s, -0.2, -0.05)
        a = pult.p(s, unten_v, -pult.sitz)
        b = pult.p(s, unten_v + 0.25, -pult.sitz)
        netz.vieleck_nach([a, b, pult.platte(s, 0.25, -0.05), oben], pult.links * (1 if s > 0 else -1), holz)
        netz.vieleck_nach([a, b, pult.platte(s, 0.25, -0.05), oben], pult.links * (-1 if s > 0 else 1), holz)
    netz.quader((0, 0, 0), (0.62, 0.3, 0.03), hell, pult.lok(0.0, 0.42, -pult.sitz + 0.35))           # Ablage mit Büchern
    for k, f_ in enumerate(("Buchrot", "Buchbraun", "Buchgruen", "Leder")):
        b_ = 0.06 + 0.02 * (k % 2)
        netz.quader((0, 0, 0), (b_, 0.22, 0.22 + 0.03 * k), st[f_], pult.lok(-0.22 + 0.12 * k, 0.42, -pult.sitz + 0.365 + 0.11 + 0.015 * k))
    hs, hv, hh = fg.SCHREIBEN["horn"]                                         # Tintenhorn
    mund = pult.p(hs, hv, hh)
    drehkoerper(netz, st, mund - Vector((0, 0, 0.13)), [(0.0, 0.0), (0.03, 0.012), (0.1, 0.02), (0.13, 0.024)], "Federweiss", n=6)
    scheibe(netz, st, mund, 0.019, mund.z - 0.008, "Tinte", n=6)
    if buch is not None:
        aufgeschlagen(buch, st, pult, farben=True)
    if werkstueck is not None:
        aufgeschlagen(werkstueck, st, pult, farben=False)


def aufgeschlagen(netz, st, pult, farben):
    """Aufgeschlagenes Buch auf der Platte (Mitte BUCH): Einband, zwei Seitenblöcke, deren Oberseite genau in der Ebene des
    Blatts liegt (dort setzt die Feder auf), Zeilen in Tinte, eine Initiale; ohne `farben` sind Einband, Schnitt und Initiale
    im Material „Fluessigkeit“ (Werkstück: die Farbe der Ware)."""
    s0, u0 = BUCH
    einband = st["Buchrot" if farben else "Fluessigkeit"]
    M = pult.matrix(s0, u0, -0.026)
    netz.quader((0, 0, 0.004), (0.4, 0.28, 0.008), einband, M)
    for s in (-1, 1):
        netz.quader((s * 0.095, 0, 0.014), (0.18, 0.26, 0.012), st["Buchrot" if farben else "Fluessigkeit"], M, ohne=("+z",))
        netz.quader((s * 0.095, 0, 0.0225), (0.18, 0.26, 0.005), st["Leinen"], M, ohne=("-z",))
        for k in range(9):
            if (s > 0 and k < 3) or (s < 0 and k > 5):
                continue
            u = 0.1 - 0.024 * k
            laenge = 0.13 - 0.03 * ((k * 7) % 3) / 2
            netz.vieleck([M @ Vector(p) for p in ((s * 0.095 - 0.065, u - 0.003, 0.0255), (s * 0.095 - 0.065 + laenge, u - 0.003, 0.0255),
                                                  (s * 0.095 - 0.065 + laenge, u + 0.003, 0.0255), (s * 0.095 - 0.065, u + 0.003, 0.0255))],
                         st["Tinte"])
    netz.vieleck([M @ Vector(p) for p in ((0.04, 0.045, 0.0256), (0.1, 0.045, 0.0256), (0.1, 0.105, 0.0256), (0.04, 0.105, 0.0256))],
                 st["Weihrot" if farben else "Fluessigkeit"])


def buecherregal(netz, st, M, breite=2.0, hoehe=2.3, tiefe=0.36, boeden=5, saat=0):
    """Bücherregal an der Wand (örtlich +y in den Raum): Seiten, Böden, Bücher in Reihen (stehend, manche liegend gestapelt),
    dazwischen Rollen."""
    for x in (-breite / 2, breite / 2):
        si.q(netz, st, M, (x, tiefe / 2, hoehe / 2), (0.05, tiefe, hoehe), "Kirchenholz", ohne=("-y",))
    si.q(netz, st, M, (0, 0.012, hoehe / 2), (breite, 0.024, hoehe), "Kirchenholz", ohne=("-y",))
    zufall = random.Random(saat)
    farben = ("Buchrot", "Buchbraun", "Buchgruen", "Leder", "Buchbraun", "Tafelbild blau")
    for k in range(boeden):
        z = 0.1 + (hoehe - 0.18) * k / (boeden - 1)
        si.q(netz, st, M, (0, tiefe / 2, z), (breite - 0.05, tiefe, 0.035), "Kirchenholz hell", ohne=("-y",))
        if k == boeden - 1:
            continue
        x = -breite / 2 + 0.05
        while x < breite / 2 - 0.12:
            art = zufall.random()
            if art < 0.12:                                                   # liegender Stapel
                for j in range(zufall.randint(2, 4)):
                    si.q(netz, st, M, (x + 0.13, tiefe * 0.5, z + 0.03 + 0.045 * j), (0.24, 0.3, 0.04), zufall.choice(farben))
                x += 0.3
            elif art < 0.2:                                                  # Rollen
                for j in range(3):
                    netz.stab(M @ Vector((x + 0.05 + 0.06 * j, 0.04, z + 0.05)), M @ Vector((x + 0.05 + 0.06 * j, tiefe - 0.02, z + 0.05)),
                              0.05, 0.05, st["Papierbogen"])
                x += 0.22
            else:
                d = zufall.uniform(0.04, 0.08)
                h = zufall.uniform(0.22, 0.34)
                si.q(netz, st, M, (x + d / 2, tiefe * 0.5 + zufall.uniform(-0.02, 0.02), z + 0.018 + h / 2), (d - 0.004, 0.26, h), zufall.choice(farben))
                x += d


def wandbord(netz, st, M):
    """Wandbord (örtlich +y in den Raum) mit Tintenfässern, Federn in einem Becher und Pergamentrollen."""
    si.q(netz, st, M, (0, 0.13, 1.45), (1.0, 0.26, 0.03), "Kirchenholz hell", ohne=("-y",))
    for x in (-0.42, 0.42):
        si.q(netz, st, M, (x, 0.1, 1.35), (0.04, 0.2, 0.18), "Kirchenholz", ohne=("-y",))
    zufall = random.Random(11)
    for k in range(4):
        netz.stab(M @ Vector((-0.35 + 0.06 * k, 0.05, 1.5)), M @ Vector((-0.35 + 0.06 * k, 0.22, 1.5)), 0.05, 0.05, st["Papierbogen"])
    for k, x in enumerate((0.0, 0.1)):
        drehkoerper(netz, st, M @ Vector((x, 0.12, 1.465)), [(0.0, 0.03), (0.05, 0.03), (0.06, 0.015), (0.075, 0.015)], "Krugton" if k else "Tinte", n=6)
    b = M @ Vector((0.3, 0.12, 1.465))
    drehkoerper(netz, st, b, [(0.0, 0.035), (0.1, 0.035), (0.1, 0.03)], "Krugton", n=6)
    for k in range(4):
        w = 2 * math.pi * k / 4 + zufall.uniform(0, 0.4)
        netz.stab(b + Vector((0.01 * math.cos(w), 0.01 * math.sin(w), 0.05)), b + Vector((0.05 * math.cos(w), 0.05 * math.sin(w), 0.3)),
                  0.008, 0.008, st["Federweiss"])


def lesepult(netz, st, fuss, blick):
    """Stehendes Lesepult mit einem großen Buch an einer Kette."""
    f = Vector(fuss)
    vor = Vector((blick[0], blick[1], 0)).normalized()
    drehkoerper(netz, st, f, [(0.0, 0.25), (0.06, 0.2), (0.1, 0.06), (1.05, 0.05)], "Kirchenholz", n=8)
    M = Matrix.Translation(f + Vector((0, 0, 1.12)) - vor * 0.02) @ Matrix.Rotation(math.radians(si.drehung_von((-vor.x, -vor.y))), 4, "Z") \
        @ Matrix.Rotation(math.radians(-32), 4, "X")
    netz.quader((0, 0, 0), (0.6, 0.45, 0.03), st["Kirchenholz hell"], M)
    netz.quader((0, 0, 0.025), (0.54, 0.4, 0.03), st["Leder"], M)
    for s in (-1, 1):
        netz.quader((s * 0.13, 0, 0.05), (0.25, 0.36, 0.02), st["Leinen"], M)
    netz.stab(M @ Vector((0.29, 0.1, 0.03)), f + Vector((0.12, 0.05, 0.6)), 0.012, 0.012, st["Eisen"])


def truhe(netz, st, M, eisen=False):
    """Truhe (örtlich +y in den Raum); mit `eisen` ganz mit Eisenbändern beschlagen, drei Schlösser."""
    si.q(netz, st, M, (0, 0.3, 0.3), (1.2, 0.56, 0.6), "Kirchenholz", ohne=("-y", "+z", "-z"))
    si.q(netz, st, M, (0, 0.3, 0.62), (1.24, 0.6, 0.06), "Kirchenholz hell" if not eisen else "Eisen")
    for x in ((-0.5, -0.25, 0.0, 0.25, 0.5) if eisen else (-0.45, 0.45)):
        si.q(netz, st, M, (x, 0.585, 0.3), (0.05, 0.012, 0.6), "Eisen")
        si.q(netz, st, M, (x, 0.3, 0.652), (0.05, 0.6, 0.01), "Eisen")
    for x in ((-0.3, 0.0, 0.3) if eisen else (0.0,)):
        si.q(netz, st, M, (x, 0.595, 0.48), (0.08, 0.02, 0.1), "Eisen")


def reliquiar(st):
    """Werkstueck_4: Reliquiar in Form einer kleinen Kirche – vergoldetes Gehäuse auf Füßen, Satteldach mit Kamm, die Felder
    an Wänden und Dach als Email im Material „Fluessigkeit“ (die Farbe der Ware), Bergkristalle an den Ecken."""
    netz = bs.Netz("Werkstueck_4", alle=True)
    L, B, H = 0.26, 0.13, 0.11
    gold = st["Gold"]
    netz.quader((0, 0, 0.012), (L + 0.03, B + 0.03, 0.024), gold)
    netz.quader((0, 0, 0.024 + H / 2), (L, B, H), gold)
    for s in (-1, 1):                                                          # Emailfelder an den Langseiten
        for k in range(3):
            x = -L / 2 + L * (k + 0.5) / 3
            netz.vieleck_nach([(x - 0.035, s * (B / 2 + 0.002), 0.04), (x + 0.035, s * (B / 2 + 0.002), 0.04), (x + 0.035, s * (B / 2 + 0.002), 0.12),
                               (x - 0.035, s * (B / 2 + 0.002), 0.12)], (0, s, 0), st["Fluessigkeit"])
    z = 0.024 + H
    for s in (-1, 1):                                                          # Dach
        netz.vieleck_nach([(-L / 2 - 0.01, s * (B / 2 + 0.01), z), (L / 2 + 0.01, s * (B / 2 + 0.01), z), (L / 2 + 0.01, 0, z + 0.07),
                           (-L / 2 - 0.01, 0, z + 0.07)], (0, s, 1), gold)
        netz.vieleck_nach([(-L / 2 + 0.02, s * (B / 2 - 0.005), z + 0.012), (L / 2 - 0.02, s * (B / 2 - 0.005), z + 0.012),
                           (L / 2 - 0.02, s * 0.012, z + 0.062), (-L / 2 + 0.02, s * 0.012, z + 0.062)], (0, s * 0.8, 1), st["Fluessigkeit"])
        netz.vieleck_nach([(s * (L / 2 + 0.01), -B / 2 - 0.01, z), (s * (L / 2 + 0.01), B / 2 + 0.01, z), (s * (L / 2 + 0.01), 0, z + 0.07)], (s, 0, 0), gold)
    for k in range(5):                                                         # Kamm
        x = -L / 2 + L * k / 4
        netz.quader((x, 0, z + 0.085), (0.018, 0.012, 0.03), gold)
    for sx in (-1, 1):
        for sy in (-1, 1):
            drehkoerper(netz, st, Vector((sx * L / 2, sy * B / 2, z)), [(0.0, 0.0), (0.0, 0.012), (0.015, 0.0)], "Federweiss", n=4)
    return netz


def vorraete_krypta(netz, st):
    """Bedienung_Lager_Krypta: an der Vorderwand Kisten mit Kerzen (oben offen, Bündel darin), Ballen Papier mit Schnur, ein
    Korb mit Wachsblöcken."""
    z = Z_KRYPTA
    zufall = random.Random(41)
    for k, x in enumerate((0.45, 1.1)):
        m = Vector((x, CY0 + 0.32, z))
        netz.quader(m + Vector((0, 0, 0.22)), (0.6, 0.5, 0.44), st["Holz alt"], ohne=("+z",))
        netz.quader(m + Vector((0, 0, 0.36)), (0.56, 0.46, 0.01), st["Holz"], ohne=("-z",))
        for i in range(6):                                                   # Kerzenbündel
            p = m + Vector((-0.2 + 0.08 * i, zufall.uniform(-0.12, 0.12), 0.37))
            netz.stab(p, p + Vector((0, 0.32, 0.02)), 0.07, 0.07, st["Kerze"], quer=(0, 0, 1))
            netz.stab(p + Vector((0, 0.15, 0.0)), p + Vector((0, 0.17, 0.0)), 0.08, 0.08, st["Seil"], quer=(0, 0, 1))
    m = Vector((1.75, CY0 + 0.3, z))
    for i in range(4):                                                       # Papierballen
        netz.quader(m + Vector((0.0, 0.0, 0.07 + 0.14 * i)), (0.44, 0.32, 0.13), st["Papierbogen"])
        netz.quader(m + Vector((0.0, 0.0, 0.07 + 0.14 * i)), (0.45, 0.03, 0.135), st["Seil"])
    drehkoerper(netz, st, Vector((-0.25, CY0 + 0.35, z)), [(0.0, 0.2), (0.35, 0.26), (0.35, 0.24)], "Geflecht", n=10)
    for i in range(4):
        netz.quader(Vector((-0.25 + 0.15 * (i % 2) - 0.075, CY0 + 0.3 + 0.11 * (i // 2) - 0.055, z + 0.38)), (0.13, 0.09, 0.07), st["Wachsblock"])


def kelch(netz, st, fuss, h=0.24):
    drehkoerper(netz, st, fuss, [(0.0, 0.07), (0.015, 0.065), (0.04, 0.02), (h * 0.45, 0.016), (h * 0.5, 0.03), (h * 0.55, 0.016),
                                 (h * 0.65, 0.02), (h * 0.8, 0.055), (h, 0.06)], "Gold", n=8)


def monstranz(netz, st, fuss):
    """Monstranz: Fuß und Schaft aus Gold, darüber ein Strahlenkranz um das Schaugefäß."""
    f = Vector(fuss)
    drehkoerper(netz, st, f, [(0.0, 0.09), (0.02, 0.08), (0.05, 0.025), (0.2, 0.02), (0.23, 0.035), (0.26, 0.02), (0.3, 0.02)], "Gold", n=8)
    m = f + Vector((0, 0, 0.42))
    for k in range(16):
        w = 2 * math.pi * k / 16
        lang = 0.17 if k % 2 else 0.12
        netz.vieleck_nach([m + Vector((0.06 * math.cos(w), 0, 0.06 * math.sin(w))), m + Vector((lang * math.cos(w + 0.06), 0, lang * math.sin(w + 0.06))),
                           m + Vector((lang * math.cos(w - 0.06), 0, lang * math.sin(w - 0.06)))], (0, -1, 0), st["Gold"])
    drehkoerper(netz, st, m - Vector((0, 0, 0.065)), [(0.0, 0.0), (0.0, 0.065), (0.13, 0.065), (0.13, 0.0)], "Gold", n=10)
    kreis_wand(netz, st, wand((f.x + 1, f.y - 0.066), (f.x - 1, f.y - 0.066)), (1.0, m.z), 0.045, 0.0, "Federweiss", n=10)


def nische(netz, st, w, u, v0, v1, b, tief=0.3):
    """Nische in der Wand w (Loch u ± b/2, v0 … v1 muss frei sein): Rückwand, Seiten, Decke aus Putz, eine Sohlbank aus Sandstein,
    ein Rahmen aus Sandstein in der Wandfläche."""
    alle, netz.alle = netz.alle, True
    u0, u1 = u - b / 2, u + b / 2
    netz.vieleck_nach([w.p(u0, v0, -tief), w.p(u1, v0, -tief), w.p(u1, v1, -tief), w.p(u0, v1, -tief)], w.n, st["Weihrot"])
    for uu, s in ((u0, 1), (u1, -1)):
        netz.vieleck_nach([w.p(uu, v0, 0), w.p(uu, v1, 0), w.p(uu, v1, -tief), w.p(uu, v0, -tief)], w.d * s, st["Kalkputz"])
    netz.vieleck_nach([w.p(u0, v1, 0), w.p(u1, v1, 0), w.p(u1, v1, -tief), w.p(u0, v1, -tief)], (0, 0, -1), st["Kalkputz"])
    netz.vieleck_nach([w.p(u0, v0, 0), w.p(u1, v0, 0), w.p(u1, v0, -tief), w.p(u0, v0, -tief)], (0, 0, 1), st["Sandstein"])
    netz.alle = alle
    for a, b_ in (((u0 - 0.06, v0 - 0.06), (u1 + 0.06, v0)), ((u0 - 0.06, v1), (u1 + 0.06, v1 + 0.06)), ((u0 - 0.06, v0), (u0, v1)), ((u1, v0), (u1 + 0.06, v1))):
        wq(netz, st, w, a[0], b_[0], a[1], b_[1], 0.0, 0.025, "Sandstein")


def gittertuer(netz, st, w, u, b, h, tief):
    """Vergitterte Tür (Rahmen und Gitter aus Eisen) `tief` Meter in der Wand w, mit Schloss."""
    e = st["Eisen"]
    for uu in (u - b / 2 + 0.03, u + b / 2 - 0.03):
        balken_wand(netz, st, w, (uu, 0.0), (uu, h), 0.05, tief, "Eisen")
    for v in (0.03, h * 0.5, h - 0.03):
        balken_wand(netz, st, w, (u - b / 2, v), (u + b / 2, v), 0.05, tief, "Eisen")
    for k in range(1, 6):
        uu = u - b / 2 + b * k / 6
        netz.stab(w.p(uu, 0.0, -tief - 0.02), w.p(uu, h, -tief - 0.02), 0.022, 0.022, e, quer=w.n)
    for k in range(1, 8):
        v = h * k / 8
        netz.stab(w.p(u - b / 2, v, -tief - 0.045), w.p(u + b / 2, v, -tief - 0.045), 0.018, 0.018, e, quer=w.n)
    wq(netz, st, w, u + b / 2 - 0.2, u + b / 2 - 0.06, h * 0.5 - 0.08, h * 0.5 + 0.08, -tief + 0.0, -tief + 0.04, "Eisen")


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen
# ─────────────────────────────────────────────────────────────────────────────

def bauen():
    import bpy
    bs.neue_szene()
    ia.RUSS_QUELLEN.clear()
    ti._lichter[0] = 0
    st = bs.Stoffe()
    kirche, werk = bs.Netz("Raum_Kirche", alle=True), bs.Netz("Raum_Werk")
    lager, skript, schatz = bs.Netz("Raum_Lager", alle=True), bs.Netz("Raum_Skriptorium"), bs.Netz("Raum_Schatzkammer", alle=True)
    decken = {r: bs.Netz(f"Decke_{r}", alle=True) for r in ("Kirche", "Werk", "Lager", "Skriptorium", "Schatzkammer")}
    teile = {}                    # Name → (Netz, Raum)

    def teil(name, raum="Kirche"):
        if name not in teile:
            teile[name] = (bs.Netz(name), raum)
        return teile[name][0]

    anker = []                    # (Objekt, Raum)
    fertig = []                   # schon gebaute Objekte (Werkstücke, bewegte Teile): (Objekt, Raum)

    def setzen(o, raum="Kirche", stufe=None):
        if stufe is not None:
            o["stufe"] = stufe
        anker.append((o, raum))

    def alle_setzen(liste, raum="Kirche", stufe=None):
        for o in liste:
            setzen(o, raum, stufe)

    # ── Kirchenschiff: Wände mit Fenstern, Rosette, Portal und Türen ──────────
    kanten = ecken(KX0, KX1, KY0, KY1)
    w = {s: wand(*kanten[s]) for s in kanten}
    loecher = {s: [] for s in kanten}
    liste = []
    for y in FENSTER_Y:
        loecher["links"].append(kirchenfenster(kirche, st, liste, w["links"], y, saat=int(13 + 5 * y)))
        loecher["rechts"].append(kirchenfenster(kirche, st, liste, w["rechts"], y, saat=int(31 + 5 * y)))
    for x in FENSTER_X:
        loecher["hinten"].append(kirchenfenster(kirche, st, liste, w["hinten"], x, saat=int(47 + 5 * x)))
    loecher["vorne"].append(rosette(kirche, st, liste, w["vorne"], 0.0, *ROSETTE))
    loecher["vorne"].append(portal(kirche, st, w["vorne"], teil("Holztuer"), teil("Einrichtung_Eisentuer")))
    for seite, s in (("links", SAKTUER_Y), ("rechts", SKRIPTTUER_Y)):
        u = u_von(w[seite], s)
        loch = (u - TUER_B / 2, u + TUER_B / 2, 0.0, TUER_H)
        laibung(kirche, st, w[seite], *loch, 0.25)
        loecher[seite].append(loch)
    for seite in kanten:
        flaeche_mit_loechern(kirche, st, w[seite], H_KIRCHE, loecher[seite])
        frei = [(u_von(w["rechts"], -3.95), u_von(w["rechts"], -1.45))] if seite == "rechts" else []
        sockel(kirche, st, w[seite], loecher[seite], frei=frei)
    dienste(kirche, st, w["links"])
    dienste(kirche, st, w["rechts"])
    for seite, y in (("links", -1.5), ("links", 2.6), ("rechts", -1.5), ("rechts", 2.2)):
        weihekreuz(kirche, st, w[seite], u_von(w[seite], y), 3.0)
    alle_setzen(liste)
    kirchendecke(decken["Kirche"], st)
    steinplatten(kirche, st, KX0, KX1, KY0, KY1, saat=61, loecher=[(TR_X0, TR_X1, TR_Y0, TR_LOCH_Y), (-CHOR_X, CHOR_X, CHOR_Y, KY1)])

    # ── Kirchenschiff: Chor, Altar, Bänke, Kanzel, Beichtstuhl, Taufstein, Opferstock, Treppe ──
    liste = []
    chor(kirche, st)
    altar(kirche, st, liste)
    kruzifix(kirche, st)
    ewiges_licht(kirche, st, liste, (-1.9, 3.9, 2.45))
    baenke(kirche, st, liste)
    kanzel(kirche, st, liste)
    beichtstuhl(kirche, st, liste)
    taufstein(kirche, st)
    weihwasser(kirche, st)
    alle_setzen(liste)
    opferstock(teil("Bedienung_Lager_Opferstock"), st)
    treppe_krypta(teil("Tuer_Kirche_Lager"), st, True)
    si.bretttuer(teil("Tuer_Kirche_Werk"), st, si.rahmen(KX0, SAKTUER_Y, -90), TUER_B, TUER_H, stoff="Brett", rueck=0.25)
    si.bretttuer(teil("Tuer_Kirche_Skriptorium"), st, si.rahmen(KX1, SKRIPTTUER_Y, 90), TUER_B, TUER_H, stoff="Brett", rueck=0.25)

    # ── Stufen (je eine sichtbar, im Schiff) ──────────────────────────────────
    stufen = [bs.Netz(f"Stufe{k}", alle=True) for k in (1, 2, 3)]
    ai.vermauert(stufen[0], st, *kanten["rechts"], Oe(SKRIPTTUER_Y, 0.0, TUER_B, TUER_H), rueck=0.05)
    fluegelaltar(stufen[1], st, False)
    fluegelaltar(stufen[2], st, True)
    liste = []
    wi.radleuchter(stufen[2], st, liste, Vector((0.0, -0.4, 4.3)), H_KIRCHE)
    alle_setzen(liste, stufe=3)
    stufen_obj = [s.objekt() for s in stufen]

    # ── Sakristei (Werkraum) ──────────────────────────────────────────────────
    kanten_s = ecken(SX0, SX1, SY0, SY1)
    vorn_fenster = (-5.6, 1.2, 0.7, 1.7)
    oeff = {"vorne": [Oe(HOFTOR_X, 0.0, HOFTOR_B, HOFTOR_H), fenster_loch(*vorn_fenster)], "rechts": [Oe(SAKTUER_Y, 0.0, TUER_B, TUER_H)]}
    w_s = {s: wand(*kanten_s[s]) for s in kanten_s}
    frei = {"hinten": [(u_von(w_s["hinten"], x + 0.6), u_von(w_s["hinten"], x - 0.6)) for x in (SCHATZTUER_X,)]
            + [(u_von(w_s["hinten"], TROG.x + 0.85), u_von(w_s["hinten"], TROG.x - 0.85))],
            "links": [(u_von(w_s["links"], 3.7), u_von(w_s["links"], 1.1))]}
    si.steinraum(werk, decken["Werk"], None, st, SX0, SX1, SY0, SY1, H_SAK, {
        "vorne": dict(oeffnungen=oeff["vorne"], laibung=False, dicke=WAND_S),
        "rechts": dict(oeffnungen=oeff["rechts"], aussen=False, laibung=False, dicke=WAND),
        "hinten": dict(dicke=WAND_S),
        "links": dict(dicke=WAND_S),
    }, saat=7, frei=frei)
    liste = []
    kirchenfenster(werk, st, liste, w_s["vorne"], vorn_fenster[0], *vorn_fenster[1:], dicke=WAND_S, bunt=False)
    u = u_von(w_s["vorne"], HOFTOR_X)
    laibung(werk, st, w_s["vorne"], u - HOFTOR_B / 2, u + HOFTOR_B / 2, 0.0, HOFTOR_H, 0.1)
    u = u_von(w_s["rechts"], SAKTUER_Y)
    laibung(werk, st, w_s["rechts"], u - TUER_B / 2, u + TUER_B / 2, 0.0, TUER_H, 0.25)
    steinplatten(werk, st, SX0, SX1, SY0, SY1, saat=62, breite=0.45)
    werkstueck1, dampf1 = wachsherd(teil("Bedienung_Produktion_Kerzen", "Werk"), st, liste)
    staebe, tropfen_k = ziehgestell(werk, st)
    werkstueck2, tropfen_s = buette(teil("Bedienung_Produktion_Buette", "Werk"), st)
    fertig += [(o, "Werk") for o in [werkstueck1, werkstueck2] + staebe + tropfen_k + tropfen_s]
    setzen(bs.anker("Dampf_1", dampf1), "Werk")
    lumpenkorb(werk, st, (-7.9, 1.55, 0))
    trockenleinen(werk, st)
    paramentenschrank(werk, st, si.rahmen(SX0, 0.55, -90))
    farbtisch(teil("Einrichtung_Gaensekiele", "Werk"), st, si.rahmen(-7.25, SY1, 180))
    si.pult(teil("Bedienung_Personal_Buch", "Werk"), st, si.rahmen(SX1 - 0.05, 0.5, 90))
    M_wz = si.rahmen(SX1, 2.3, 90)
    si.werkzeugwand(teil("Bedienung_Einrichten_Werkzeugwand", "Werk"), st, M_wz)
    werkzeug_sakristei(teil("Einrichtung_Werkzeug1", "Werk"), st, M_wz, 1)
    werkzeug_sakristei(teil("Einrichtung_Werkzeug2", "Werk"), st, M_wz, 2)
    wi.handschuhe(teil("Einrichtung_Handschuhe", "Werk"), st, si.rahmen(SX1, 3.55, 90))
    si.loeschfass(teil("Einrichtung_Loeschfass", "Werk"), st, Vector((-5.0, 5.15, 0)))
    ti.hoftor(teil("Bedienung_Karren_Hoftor", "Werk"), st, si.rahmen(HOFTOR_X, SY0, 0), HOFTOR_B, HOFTOR_H)
    si.bretttuer(teil("Tuer_Werk_Kirche", "Werk"), st, si.rahmen(SX1, SAKTUER_Y, 90), TUER_B, TUER_H, stoff="Brett", rueck=0.25)
    o_schatz = Oe(SCHATZTUER_X, 0.0, TUER_B, TUER_H)                      # vermauert, ab Stufe 3 davor die Eisentür
    ai.vermauert(werk, st, *kanten_s["hinten"], o_schatz, rueck=-0.012)
    u = u_von(w_s["hinten"], SCHATZTUER_X)
    for a, b in (((u - TUER_B / 2 - 0.12, 0.0), (u - TUER_B / 2, TUER_H)), ((u + TUER_B / 2, 0.0), (u + TUER_B / 2 + 0.12, TUER_H)),
                 ((u - TUER_B / 2 - 0.12, TUER_H), (u + TUER_B / 2 + 0.12, TUER_H + 0.16))):
        wq(werk, st, w_s["hinten"], a[0], b[0], a[1], b[1], 0.0, 0.03, "Sandstein")
    tuer_schatz = teil("Tuer_Werk_Schatzkammer", "Werk")                    # schwere Bohlentür mit Eisenbändern und Schloss
    M_t = si.rahmen(SCHATZTUER_X, SY1, 180)
    si.bretttuer(tuer_schatz, st, M_t, TUER_B + 0.04, TUER_H + 0.04, stoff="Kirchenholz", rueck=-0.08)
    for f in (0.12, 0.36, 0.62, 0.86):
        si.q(tuer_schatz, st, M_t, (0, 0.092, (TUER_H + 0.04) * f), (TUER_B, 0.012, 0.08), "Eisen", ohne=("-y",))
        for x in (-0.38, -0.12, 0.12, 0.38):
            si.q(tuer_schatz, st, M_t, (x, 0.1, (TUER_H + 0.04) * f), (0.03, 0.01, 0.03), "Eisen", ohne=("-y",))
    si.q(tuer_schatz, st, M_t, (0.3, 0.1, 1.05), (0.12, 0.03, 0.18), "Eisen", ohne=("-y",))
    for p in (Vector((-8.0, 2.0, 2.95)), Vector((-5.6, 3.7, 2.95))):
        ti.laterne(werk, st, p, H_SAK - 0.26)
        liste.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"))
    alle_setzen(liste, "Werk")
    setzen(si.arbeit("Arbeit_1_Kerzenziehen", W1, (0, -1), 1), "Werk")
    setzen(si.arbeit("Arbeit_2_Schoepfen", W2, (0, -1), 1), "Werk")

    # ── Krypta (Lager) ────────────────────────────────────────────────────────
    liste = []
    krypta(lager, decken["Lager"], st, liste)
    alle_setzen(liste, "Lager")
    vorraete_krypta(teil("Bedienung_Lager_Krypta", "Lager"), st)
    for y, b in ((1.15, 1.3), (2.6, 0.9)):
        buecherregal(teil("Einrichtung_Regale", "Lager"), st, si.rahmen(CX0, y, -90, Z_KRYPTA), breite=b, hoehe=1.6, boeden=4, saat=int(y * 10))
    treppe_krypta(teil("Tuer_Lager_Kirche", "Lager"), st, False)

    # ── Skriptorium (ab Stufe 2) ──────────────────────────────────────────────
    kanten_r = ecken(RX0, RX1, RY0, RY1)
    w_r = {s: wand(*kanten_r[s]) for s in kanten_r}
    fenster_r = [(x, 1.1, 0.8, 2.0) for x in (5.4, 7.6)]
    si.steinraum(skript, decken["Skriptorium"], None, st, RX0, RX1, RY0, RY1, H_SKRIPT, {
        "vorne": dict(dicke=WAND_S),
        "rechts": dict(dicke=WAND_S),
        "hinten": dict(oeffnungen=[fenster_loch(*f) for f in fenster_r], laibung=False, dicke=WAND_S),
        "links": dict(oeffnungen=[Oe(SKRIPTTUER_Y, 0.0, TUER_B, TUER_H)], aussen=False, laibung=False, dicke=WAND),
    }, saat=9, frei={"rechts": [(0.0, w_r["rechts"].laenge)]})
    liste = []
    for f in fenster_r:
        kirchenfenster(skript, st, liste, w_r["hinten"], f[0], *f[1:], dicke=WAND_S, bunt=False, stufe=2)
    u = u_von(w_r["links"], SKRIPTTUER_Y)
    laibung(skript, st, w_r["links"], u - TUER_B / 2, u + TUER_B / 2, 0.0, TUER_H, 0.25)
    wi.dielen(skript, st, RX0, RX1, RY0, RY1, 0.0, saat=8)
    si.bretttuer(teil("Tuer_Skriptorium_Kirche", "Skriptorium"), st, si.rahmen(RX0, SKRIPTTUER_Y, -90), TUER_B, TUER_H, stoff="Brett", rueck=0.25)
    pult3 = Pult(W3, (1, 0))
    buch3 = bs.Netz("Werkstueck_3", alle=True)
    schreibpult(skript, st, pult3, werkstueck=buch3)
    werkstueck3 = am_ort(buch3, pult3.platte(*BUCH, 0.0))
    werkstueck3["stufe"] = 2
    fertig.append((werkstueck3, "Skriptorium"))
    pult_b = Pult(Vector((5.8, 0.25, 0)), (1, 0))
    schreibpult(skript, st, pult_b, buch=skript)
    p = pult_b.platte(-0.12, 0.08, 0.004)                                   # Feder auf dem zweiten Pult
    skript.stab(p, p + pult_b.auf * 0.05 + pult_b.links * 0.2, 0.006, 0.006, st["Federkiel"])
    buecherregal(skript, st, si.rahmen(RX1, 1.2, 90), breite=2.2, hoehe=2.6, boeden=6, saat=3)
    buecherregal(skript, st, si.rahmen(RX1, 3.2, 90), breite=1.2, hoehe=2.6, boeden=6, saat=4)
    buecherregal(skript, st, si.rahmen(7.6, RY0, 0), breite=1.8, hoehe=2.2, boeden=5, saat=5)
    lesepult(skript, st, (7.9, 0.1, 0), (-1, 0))
    truhe(skript, st, si.rahmen(8.4, RY1, 180))
    kerze(skript, st, pult_b.platte(0.27, 0.2, 0.0) + Vector((0, 0, 0.02)), h=0.16, r=0.02)                 # Kerze auf dem zweiten Pult
    skript.quader(pult_b.platte(0.27, 0.2, 0.0) + Vector((0, 0, 0.01)), (0.09, 0.09, 0.02), st["Messing"])
    wandbord(skript, st, si.rahmen(5.2, RY0, 0))
    w_l = w_r["links"]                                                     # kleines Kruzifix an der linken Wand
    u = u_von(w_l, 2.4)
    wq(skript, st, w_l, u - 0.03, u + 0.03, 1.75, 2.45, 0.0, 0.04, "Kirchenholz")
    wq(skript, st, w_l, u - 0.17, u + 0.17, 2.2, 2.26, 0.0, 0.04, "Kirchenholz")
    wq(skript, st, w_l, u - 0.025, u + 0.025, 1.92, 2.22, 0.04, 0.07, "Corpus")
    p = Vector((6.6, 1.3, 2.95))
    ti.laterne(skript, st, p, H_SKRIPT - 0.26)
    liste.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe", 2))
    alle_setzen(liste, "Skriptorium", stufe=2)
    o = si.arbeit("Arbeit_3_Schreiben", W3, (1, 0), 2)
    o["sitzhoehe"] = SITZ
    setzen(o, "Skriptorium")

    # ── Schatzkammer (ab Stufe 3) ─────────────────────────────────────────────
    kanten_t = ecken(TX0, TX1, TY0, TY1)
    w_t = {s: wand(*kanten_t[s]) for s in kanten_t}
    hx, hy = gewoelbe_joch(decken["Schatzkammer"], st, TX0, TX1, TY0, TY1, T_KAEMPFER, T_STICH, n=10, saat=5)
    cx, cy = (TX0 + TX1) / 2, (TY0 + TY1) / 2
    u_tuer = u_von(w_t["vorne"], SCHATZTUER_X)
    fenster_t = Oe(8.1, 1.2, 0.5, 0.9)
    u_fenster = u_von(w_t["links"], fenster_t.s)
    nischen = [(x, 1.15, 1.75, 0.42) for x in (-8.0, -6.8, -5.6)]
    loecher_t = {"vorne": [(u_tuer - TUER_B / 2, u_tuer + TUER_B / 2, 0.0, TUER_H)],
                 "links": [(u_fenster - fenster_t.b / 2, u_fenster + fenster_t.b / 2, fenster_t.v, fenster_t.v + fenster_t.h)],
                 "hinten": [(u_von(w_t["hinten"], x) - b / 2, u_von(w_t["hinten"], x) + b / 2, v0, v1) for x, v0, v1, b in nischen],
                 "rechts": []}
    for seite in kanten_t:
        entlang_x = seite in ("vorne", "hinten")
        h = hx if entlang_x else hy
        mitte = cx if entlang_x else cy
        schildbogen_wand(schatz, st, w_t[seite], 0.0, w_t[seite].laenge, T_KAEMPFER,
                         lambda uu, s=seite, h=h, mitte=mitte: h(welt_s(w_t[s], uu) - mitte), loecher_t[seite], stoff="Kalkputz", n=12)
        sockel(schatz, st, w_t[seite], loecher_t[seite], hoehe=0.6)
    steinplatten(schatz, st, TX0, TX1, TY0, TY1, saat=63, stoffe=("Platte warm", "Platte", "Platte hell"))
    laibung(schatz, st, w_t["vorne"], u_tuer - TUER_B / 2, u_tuer + TUER_B / 2, 0.0, TUER_H, 0.12)
    gang = (SCHATZTUER_X - TUER_B / 2, SCHATZTUER_X + TUER_B / 2, TY0 - WAND + 0.1, TY0 - 0.12)       # dunkler Gang hinter dem Gitter
    for x, s in ((gang[0], 1), (gang[1], -1)):
        schatz.vieleck_nach([(x, gang[2], 0.0), (x, gang[3], 0.0), (x, gang[3], TUER_H), (x, gang[2], TUER_H)], (s, 0, 0), st["Bruch dunkel"])
    ia.waagrecht(schatz, gang, TUER_H, False, st["Bruch dunkel"])
    ia.waagrecht(schatz, gang, 0.0, True, st["Platte dunkel"])
    schatz.vieleck_nach([(gang[0], gang[2], 0.0), (gang[1], gang[2], 0.0), (gang[1], gang[2], TUER_H), (gang[0], gang[2], TUER_H)], (0, 1, 0),
                        st["Kirchenholz"])                                    # Rückseite der Tür zur Sakristei
    gittertuer(teil("Tuer_Schatzkammer_Werk", "Schatzkammer"), st, w_t["vorne"], u_tuer, TUER_B, TUER_H, 0.12)
    liste = []
    laibung(schatz, st, w_t["links"], u_fenster - fenster_t.b / 2, u_fenster + fenster_t.b / 2, fenster_t.v, fenster_t.v + fenster_t.h, WAND_S,
            unten=True)
    si.fenster(schatz, st, *kanten_t["links"], fenster_t, WAND_S)
    liste.append(licht(w_t["links"].p(u_fenster, fenster_t.v + fenster_t.h / 2, 0.12), (1, 0, 0), "Fenster", 3))
    for k, (x, v0, v1, b) in enumerate(nischen):
        uu = u_von(w_t["hinten"], x)
        nische(schatz, st, w_t["hinten"], uu, v0, v1, b)
        fuss = Vector((x, TY1 + 0.15, v0))
        if k == 1:
            monstranz(schatz, st, fuss)
        else:
            kelch(schatz, st, fuss + Vector((-0.08, 0, 0)))
            kelch(schatz, st, fuss + Vector((0.1, 0.02, 0)), h=0.2)
    truhe(schatz, st, si.rahmen(TX0, 7.3, -90), eisen=True)
    M_tisch = si.rahmen(W4.x, W4.y - 0.95, 0)
    si.feinwerktisch(schatz, st, M_tisch)
    rel = reliquiar(st).objekt()
    rel.location = M_tisch @ Vector((0.0, 0.5, 0.775))
    rel["stufe"] = 3
    fertig.append((rel, "Schatzkammer"))
    p = Vector((-6.4, 7.6, 2.55))
    ti.laterne(schatz, st, p, T_KAEMPFER + T_STICH - 0.03)
    liste.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe", 3))
    alle_setzen(liste, "Schatzkammer", stufe=3)
    setzen(si.arbeit("Arbeit_4_Feinarbeit", W4, (0, -1), 3), "Schatzkammer")

    # ── Blickpunkte ──────────────────────────────────────────────────────────
    for name, (auge, ziel) in BLICKE.items():
        raum = name.split("_")[1]
        setzen(si.blick(name, auge, ziel), raum, stufe={"Skriptorium": 2, "Schatzkammer": 3}.get(raum))

    # ── Objekte, Zustände, Hierarchie ─────────────────────────────────────────
    raeume = {"Kirche": kirche.objekt(), "Werk": werk.objekt(), "Lager": lager.objekt(), "Skriptorium": skript.objekt(),
              "Schatzkammer": schatz.objekt()}
    raeume["Skriptorium"]["stufe"] = 2
    raeume["Schatzkammer"]["stufe"] = 3
    kinder = {r: [decken[r].objekt()] for r in raeume}
    kinder["Kirche"] += stufen_obj
    for o, raum in fertig:
        kinder[raum].append(o)
    for name, (netz, raum) in teile.items():
        o = netz.objekt()
        kinder[raum].append(o)
        if name.startswith("Einrichtung_"):
            bs.zustand(o)
        if name in ("Tuer_Kirche_Skriptorium", "Tuer_Skriptorium_Kirche"):
            o["stufe"] = 2
        if name in ("Tuer_Werk_Schatzkammer", "Tuer_Schatzkammer_Werk"):
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
    ti.animieren(kerzen_bewegung(staebe, tropfen_k) + sieb_tropfen(tropfen_s), ARBEIT_BILDER)
    bpy.context.view_layer.update()
    return list(raeume.values()) + [o for liste in kinder.values() for o in liste]


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def platz_marken(z, raeume):
    """Marken für die Plätze der Gläubigen (blau), des Predigers (lila) und der Beichte (weiß) im Grundriss (Pfeil = Blick)."""
    import bpy
    stoffe = {"Glaeubig": bs._hilfsstoff("_Marke_Glaeubig", (0.2, 0.45, 1.0)), "Predigt": bs._hilfsstoff("_Marke_Predigt", (0.6, 0.3, 1.0)),
              "Beichte": bs._hilfsstoff("_Marke_Beichte", (0.95, 0.95, 0.95))}
    liste = []
    for o in list(bpy.context.scene.objects):
        art = o.name.split("_")[0]
        if o.type != "EMPTY" or art not in stoffe or ia.raum_von(o) not in raeume:
            continue
        netz = bs.Netz("_Marke_" + o.name)
        t = o.matrix_world
        p = t.translation.copy()
        p.z = z
        vor = t.to_3x3() @ Vector((0, 1, 0))
        quer = Vector((-vor.y, vor.x, 0))
        netz.vieleck_nach([p - quer * 0.17 - vor * 0.12, p + vor * 0.36, p + quer * 0.17 - vor * 0.12], (0, 0, 1), stoffe[art])
        liste.append(netz.objekt())
    return liste


def vorschau(schnell):
    """Bilder nach docs/modelle/: prediger_innen-blicke.png (jeder Blickpunkt in Stufe 3 mit aller Einrichtung, Tropfen aus dem
    Sieb), prediger_innen-stufen.png (Kirchenschiff und Sakristei von oben je Stufe) und prediger_innen-grundriss.png
    (Kirchenschiff und Sakristei, Krypta, Skriptorium, Schatzkammer mit den Ankern: Arbeitsplätze magenta, Stellplätze türkis, Werkstücke
    orange, Blickpunkte gelb, Gläubige blau, Prediger lila, Beichte weiß)."""
    import bpy
    szene, kam = ia.buehne(schnell, lampe=80, fuell=[(0, 0, H_KIRCHE - 0.6, 420, 1), (0, 3.5, 3.0, 160, 1),
                                                       ((SX0 + SX1) / 2, (SY0 + SY1) / 2, H_SAK - 0.4, 150, 1),
                                                       ((CX0 + CX1) / 2, (CY0 + CY1) / 2, Z_KRYPTA + 2.2, 60, 1),
                                                       ((RX0 + RX1) / 2, (RY0 + RY1) / 2, H_SKRIPT - 0.4, 130, 2),
                                                       ((TX0 + TX1) / 2, (TY0 + TY1) / 2, 2.8, 70, 3)])
    szene.render.resolution_x, szene.render.resolution_y = 640, 360
    szene.render.resolution_percentage = 50 if schnell else 100
    szene.frame_set(1 + round(0.8 * ARBEIT_BILDER))                      # Tropfen fallen aus dem Sieb
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

    def von_oben(r, rand=0.6, oben=1.0):
        x0, x1, y0, y1 = r
        kam.data.ortho_scale = max(x1 - x0, y1 - y0 + oben) + 2 * rand
        kam.matrix_world = Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2 + oben / 2, 40.0))
        szene.render.resolution_x = szene.render.resolution_y = 760
    eg = {"Kirche", "Werk"}
    bereich_eg = (SX0 - WAND_S, KX1 + WAND, KY0 - WAND, KY1 + WAND)
    von_oben(bereich_eg)
    teile = []
    for k in (1, 2, 3):
        ia.zeigen(szene, k, False, decke=False, raeume=eg)
        teile.append(bs.rendern_einzeln(f"_{NAME}-stufe{k}", f"Stufe {k}: {STUFEN[k - 1]}"))
    print(bs.zusammensetzen(NAME + "-stufen", teile, spalten=3))
    teile = []
    for raeume, r, z, text in ((eg, bereich_eg, 8.0, "Kirchenschiff und Sakristei, Stufe 3: Arbeit magenta, Werkstücke orange, Blick gelb, "
                                                        "Gläubige blau, Prediger lila, Beichte weiß"),
                               ({"Lager"}, (CX0 - WAND, TR_X1 + 0.2, CY0 - 0.6, PODEST_Y1 + 0.3), Z_KRYPTA + 2.4,
                                "Krypta (ohne Gewölbe): Stellplätze türkis, Blickpunkte gelb"),
                               ({"Skriptorium"}, (RX0 - WAND, RX1 + WAND_S, RY0 - WAND_S, RY1 + WAND_S), 4.0,
                                "Skriptorium (ab Stufe 2, ohne Decke)"),
                               ({"Schatzkammer"}, (TX0 - 0.8, TX1 + 0.8, TY0 - WAND, TY1 + WAND_S), 4.0,
                                "Schatzkammer (ab Stufe 3, ohne Gewölbe)")):
        von_oben(r)
        liste = ia.marken(z, raeume) + platz_marken(z, raeume)
        ia.zeigen(szene, 3, True, decke=False, raeume=raeume)
        teile.append(bs.rendern_einzeln(f"_{NAME}-{'-'.join(sorted(raeume))}", text))
        for o in liste:
            bpy.data.objects.remove(o)
    print(bs.zusammensetzen(NAME + "-grundriss", teile, spalten=2))
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
