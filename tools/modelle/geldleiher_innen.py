"""Innenraum des Geldleihers: die Wechselstube (Hauptraum), dahinter das Tresorgewölbe (Lager) und die Pfandkammer, ab der Stufe
Geldleihe links das Kontor, ab der Stufe Bankhaus rechts der Empfang für vornehme Kunden und dahinter der Tresorturm; Stufen
Wechselstube, Geldleihe, Bankhaus.

    blender -b --python tools/modelle/geldleiher_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/geldleiher_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/innen/geldleiher_innen.glb und Bilder nach docs/modelle/ (geldleiher_innen-blicke.png: das Bild
jedes Blickpunkts, geldleiher_innen-stufen.png: die Wechselstube von oben je Stufe, geldleiher_innen-grundriss.png: alle Räume
von oben mit den Ankern).

Koordinaten wie beim Schmied (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte der Wechselstube
auf dem Boden. Außen ist das Haus ein verputztes Kaufmannshaus mit Laubengang aus Sandstein (geldleiher.glb); innen ist alles
wohlhabend und ordentlich – gekalkter Putz mit gemaltem Sockel und Fries, Türgewände aus Sandstein, Rundbogenfenster, Steinplatten,
bemalte Holzdecken, und überall Geld:
    Werk        „Wechselstube“ innen x −6,0 … 6,0, y −4,5 … 4,5, 3,4 m hoch. Vorn drei Rundbogenfenster zum Laubengang, die
                Haustür und das Hoftor; in der Mitte der Wechseltisch mit grünem Tuch (Waagen, Münzsäulen, Schalen), davor die
                Kaufleute, dahinter die Schreiber; links der Rechen- und Zähltisch, hinten links die vier Schreibpulte, hinten
                rechts der Wägetisch mit vier Balkenwaagen; rechts das Pult mit dem Hauptbuch und die Kundenbank, an der linken
                Wand der Wandschrank. Bemalte Balkendecke, warme Steinplatten.
                Türen: hinten die zur Pfandkammer und die eisenbeschlagene zum Tresorgewölbe, links (ab Stufe 2, vorher vermauert)
                die zum Kontor, rechts (ab Stufe 3, vorher vermauert) die zum Empfang.
    Lager       „Tresorgewölbe“ hinter der Wechselstube rechts: x 1,2 … 6,0, y 5,0 … 9,5; zwei Kreuzgratgewölbe (Scheitel 3,0 m)
                mit Gurtbogen, Steinplatten. Geldtruhen mit Eisenbändern, Barren, Münzsäcke; hinten die große Truhe.
    Pfand       „Pfandkammer“ hinter der Wechselstube links: x −6,0 … −2,6, y 5,0 … 9,5, 3,0 m hoch, Dielen. Regale mit Pfandgut
                und Zetteln (Silbergeschirr, Stoffballen, Werkzeug, Leuchter), ein Wappenschild, eine Laute, der Pfandtisch.
    Kontor      ab Stufe 2 links neben der Wechselstube: x −11,0 … −6,5, y −1,5 … 3,5, 3,2 m hoch, Täfelung, Dielen. Schreibtisch
                mit Rechenbrett und Siegel, die Landkarte des Reichs, Bord mit den Hauptbüchern, Aktenregal, Truhe.
    Empfang     ab Stufe 3 rechts neben der Wechselstube: x 6,5 … 12,5, y −4,5 … 2,0, 3,6 m hoch – der vornehmste Raum im Spiel:
                hohe Täfelung, darüber Wandmalerei, Kassettendecke mit goldenen Rosetten, Dielen mit einem großen Teppich;
                Kamin, Wandteppich, drei gepolsterte Lehnsessel um einen Tisch mit Weinkanne und Pokalen, zwei Standleuchter,
                eine Kredenz mit Silbergeschirr.
    Tresor      „Tresorturm“ ab Stufe 3 hinter dem Empfang: x 8,5 … 12,5, y 2,5 … 6,5; Kreuzgratgewölbe (Scheitel 3,8 m),
                Quadermauern, eiserne Tür, Kassenschränke, Truhen mit Eisenbändern, Barren.
Alle Räume sind rundum geschlossen, die Kamera steht im Raum.

Knoten im GLB – dieselben Regeln wie in den anderen Innenräumen (Raum_…, Decke_…, Stufe1–Stufe3, Bedienung_…, Tuer_…,
Einrichtung_…, Holztuer, Blick_…, Lager_…, Licht_…), dazu:
    Raum_Pfand                  neuer Raumschlüssel „Pfand“, immer da
    Raum_Kontor                 extras „stufe“ = 2 (neuer Raumschlüssel „Kontor“), ebenso Tuer_Werk_Kontor, Tuer_Kontor_Werk und
                                alle Anker darin
    Raum_Empfang, Raum_Tresor   extras „stufe“ = 3 (neue Raumschlüssel „Empfang“, „Tresor“), ebenso die Türen dorthin und alle
                                Anker darin; Tuer_Empfang_Tresor ist die eiserne Tür des Tresorturms, Tuer_Tresor_Empfang ihre
                                Innenseite mit dem Riegelwerk
    Schreiber_1 … Schreiber_20  Plätze der Schreiber (Fußpunkt, Blick −Z) mit extras „taetigkeit“, „bewegung“, „platz“ (1 … 4:
                                Füllreihenfolge je Tätigkeit) und bei sitzenden Plätzen „sitzhoehe“:
                                1–4   Bedienen (Predigen, stehend) hinter dem Wechseltisch, Hände auf dem Tisch (figuren.KANZEL)
                                5–8   Wiegen (Wiegen, stehend) am Wägetisch, je eine Balkenwaage (figuren.WIEGEN)
                                9–12  Rechnen (Rechnen, sitzend) am Rechentuch (figuren.RECHNEN)
                                13–16 Zaehlen (Zaehlen, sitzend) am Zähltisch (figuren.ZAEHLEN)
                                17–20 Hauptbuch (Schreiben, sitzend) an den Schreibpulten (figuren.SCHREIBEN)
    Kunde_Kaufmann_1 … 4        stehend vor dem Wechseltisch (Bewegung „Stehen“), Blick zum Tisch
    Kunde_Buerger_1             stehend am Pult mit dem Hauptbuch (Stehen), Kunde_Buerger_2 sitzend auf der Kundenbank (Sitzen)
    Kunde_Vornehm_1 … 3         sitzend in den Lehnsesseln im Empfang (Sitzen, stufe 3)
    Waagbalken, Waagschale_L, Waagschale_R   die Balkenwaage an Platz 1 des Wiegens (Animation „Arbeit“)
    Flammen                     das Feuer im Kamin des Empfangs (Animation „Feuer“)
Keine Arbeit_- und Werkstueck_-Anker: Die Bank stellt keine Ware her.
Animation „Arbeit“ (4 s, so lang wie die Bewegung „Wiegen“): Der Balken der Waage am ersten Platz des Wiegens schwingt nach
figuren.waage_winkel – die Münze kommt bei 0,32 auf die rechte Schale, der Balken pendelt aus, bei 0,76 wird sie abgenommen –,
die Schalen bleiben waagrecht und gehen mit. Bei Zeit 0 steht der Balken waagrecht.
Animation „Feuer“ (3 s, Schleife): Die Flammen im Kamin des Empfangs flackern – sie läuft immer, unabhängig von den Schreibern.
Lichter: extras „art“ = „Fenster“, „Lampe“ (Laternen, Hängeleuchter) und „Glut“ (Kamin, Kerzen).
Materialien: „Farben“ (Eckenfarben, Materialart als UV.y), „Hausfarbe“ (Haustür, Hoftor), „Fenster“, „Glut“, „Eisen“, „Gold“,
„Silber“, „Wasser“.
Einrichtung: Werkzeug1 (Rechenbretter auf dem Wechsel- und dem Wägetisch), Werkzeug2 (Hauptbücher auf dem Bord im Kontor, ab
Stufe 2), Goldwaage (feine Waage unter einem Glaskasten auf dem Wechseltisch), Pfandkammer (zwei Gitterschränke mit
Vorhängeschlössern in der Pfandkammer), Kassenschrank (eiserner Kassenschrank in der Wechselstube), Loeschfass, Eisentuer
(Haustür).
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import alchimist_innen as ai  # noqa: E402  (Drehkörper, Scheiben, am Ort, vermauert, Verkleinern)
import dieb_innen as di  # noqa: E402  (flache Teile, Kerzen, Münzen, Beutel)
import figuren as fg  # noqa: E402  (Kanzel, Wiegen, Rechnen, Zählen, Schreiben)
import innenausbau as ia  # noqa: E402  (Wand, Decke, Prüfbericht, Vorschau)
import parfumeur_innen as pi  # noqa: E402  (Lichter, Wandquader, Täfelung, freie Stücke)
import prediger_innen as pr  # noqa: E402  (Steinplatten, Schreibpult, Laibung, Gewölbe, Truhe, Leuchter)
import schmied_innen as si  # noqa: E402  (Wände, Pult, Türen, Regal, Löschfass, Anker, Blickpunkte)
import tischler_innen as ti  # noqa: E402  (Laterne, Hoftor, Animation, Export)
import wirt_innen as wi  # noqa: E402  (Punkt am Platz, Tisch, Bank, Dielen, Krug)

NAME = "geldleiher_innen"
STUFEN = ("Wechselstube", "Geldleihe", "Bankhaus")

bs.FARBEN.update({
    "Tuch": (0.16, 0.38, 0.22),               # grünes Tuch des Wechseltischs (wie außen)
    "Tuch dunkel": (0.11, 0.27, 0.16),        # Rechentuch, Saum
    "Sockelgruen": (0.24, 0.36, 0.27),        # gemalter Sockel der Wechselstube
    "Deckenrot": (0.56, 0.17, 0.13),          # Malerei der Holzdecke
    "Deckenblau": (0.18, 0.25, 0.42),
    "Deckengruen": (0.25, 0.39, 0.22),
    "Kartengrund": (0.83, 0.75, 0.57),        # Landkarte des Reichs
    "Kartenfluss": (0.26, 0.40, 0.56),
    "Kartenwald": (0.31, 0.43, 0.25),
    "Kartenberg": (0.50, 0.40, 0.28),
    "Kartenrot": (0.62, 0.15, 0.12),
    "Polster": (0.36, 0.11, 0.14),            # Polster der Lehnsessel (wie Damast, ohne Muster)
    "Laute": (0.55, 0.32, 0.16),
    "Lautendecke": (0.84, 0.70, 0.46),
    "Wappenblau": (0.17, 0.28, 0.55),         # verpfändeter Wappenschild
    "Stoff blau": (0.20, 0.30, 0.52),         # Stoffballen in der Pfandkammer
    "Stoff gelb": (0.78, 0.62, 0.26),
    "Stoff rot": (0.58, 0.16, 0.14),
    "Kreide": (0.86, 0.85, 0.80),             # Linien der Rechentücher (wie beim Gardisten)
})

# ── Maße ─────────────────────────────────────────────────────────────────────
WAND = 0.5
WX0, WX1, WY0, WY1 = -6.0, 6.0, -4.5, 4.5                          # Wechselstube innen
H_WERK = 3.4
LX0, LX1, LY0, LY1 = 1.2, WX1, WY1 + WAND, 9.5                      # Tresorgewölbe
L_KAEMPFER, L_STICH = 1.9, 1.1
PX0, PX1, PY0, PY1 = WX0, -2.6, WY1 + WAND, 9.5                     # Pfandkammer
H_PFAND = 3.0
KX0, KX1, KY0, KY1 = -11.0, WX0 - WAND, -1.5, 3.5                   # Kontor (ab Stufe 2)
H_KONTOR = 3.2
EX0, EX1, EY0, EY1 = WX1 + WAND, 12.5, WY0, 2.0                     # Empfang (ab Stufe 3)
H_EMPFANG = 3.6
TX0, TX1, TY0, TY1 = 8.5, EX1, EY1 + WAND, 6.5                      # Tresorturm (ab Stufe 3)
T_KAEMPFER, T_STICH = 2.4, 1.4
HAUSTUER_X, HAUSTUER_B, HAUSTUER_H = -2.0, 1.2, 2.5                # Tür zur Gasse (unter dem Laubengang)
HOFTOR_X, HOFTOR_B, HOFTOR_H = -4.5, 2.2, 2.6                      # Tordurchfahrt in der Vorderwand
FENSTER_X, FENSTER_V, FENSTER_B, FENSTER_H = (0.4, 2.6, 4.8), 0.9, 1.0, 1.8   # Rundbogenfenster zum Laubengang
PFAND_X, LAGER_X, TUER_B, TUER_H = -4.3, 3.6, 1.0, 2.2              # Türen in der Rückwand
KONTOR_Y = 1.0                                                     # Tür zum Kontor in der linken Wand
EMPF_Y, EMPF_B, EMPF_H = -1.0, 1.4, 2.5                            # Flügeltür zum Empfang in der rechten Wand
TRESOR_X = 11.0                                                    # eiserne Tür zum Tresorturm in der Rückwand des Empfangs
FPS = 24
ARBEIT_BILDER = round(fg.BEWEGUNGEN["Wiegen"] * FPS)               # Animation „Arbeit“: eine Schleife des Wiegens (4 s)
FEUER_BILDER = 3 * FPS                                             # Animation „Feuer“ (3 s)
SITZ = 0.43                                                        # Oberkante der Bänke und Hocker (wie beim Wirt)
SESSEL = 0.45                                                      # Oberkante der Polster der Lehnsessel

# Geräte (Blender-Koordinaten)
WECHSEL = Vector((0.3, -1.7, 0))                                   # Wechseltisch: Mitte, 4,0 × 0,9 m, figuren.KANZEL hoch
WECHSEL_L, WECHSEL_B = 4.0, 0.9
BEDIEN_X = tuple(WECHSEL.x + d for d in (-1.35, -0.45, 0.45, 1.35))  # Schreiber hinter dem Tisch, Kaufleute davor
BEDIEN_Y = WECHSEL.y + WECHSEL_B / 2 + (fg.KANZEL[0] - 0.16)        # die Hände liegen KANZEL[0] vor dem Fußpunkt auf dem Tisch
KAUF_Y = WECHSEL.y - WECHSEL_B / 2 - 0.35
WIEGE_X, WIEGE_Y = (1.8, 2.7, 3.6, 4.5), 2.95                      # Fußpunkte am Wägetisch (Blick −y)
WIEGE_RAND = 0.16                                                  # die Tischkante so weit vor dem Fußpunkt
RECHEN = Vector((-3.3, 0.9, 0))                                    # Rechen- und Zähltisch: Mitte, 3,4 × 0,8 m, 0,78 m hoch
RECHEN_X, ZAEHL_X = (-4.55, -3.95), (-2.65, -2.05)
SCHREIB_X, SCHREIB_Y = (-3.2, -2.3, -1.4, -0.5), 3.6               # Schreibpulte: Fußpunkte (Blick −y)
BUCH = Vector((3.3, -1.7, 0))                                      # Pult mit dem Hauptbuch (Bedienung_Bank_Hauptbuch), Blick −y
KUNDENBANK_Y = (-4.1, -2.4)                                        # Kundenbank an der rechten Wand
SCHRANK_Y = -2.0                                                   # Wandschrank an der linken Wand
KASSE_Y = 3.3                                                      # Kassenschrank an der linken Wand
EMPF_TISCH = Vector((9.4, -0.7, 0))                                # Tisch im Empfang, Sessel darum
SESSEL_PLAETZE = (((9.4, 0.25), (0, -1)), ((8.35, -1.05), (1, 0.35)), ((10.45, -1.05), (-1, 0.35)))
KAMIN_Y = -1.0                                                     # Kamin an der rechten Wand des Empfangs

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Werk_0": ((0.3, -4.0, 2.5), (-0.4, 3.6, 0.8)),            # Wechseltisch von den Kunden aus, dahinter die Schreiber
    "Blick_Werk_L1": ((3.6, -3.7, 2.4), (-5.8, 1.6, 0.8)),           # Rechen- und Zähltisch, Wandschrank, Kontortür
    "Blick_Werk_L2": ((0.6, -0.2, 2.35), (-2.8, 4.4, 0.9)),          # die Schreibpulte, Tür zur Pfandkammer
    "Blick_Werk_R1": ((-3.6, -3.4, 2.4), (5.8, 2.3, 0.8)),           # Wägetisch, Tür zum Tresorgewölbe, Kundenbank
    "Blick_Werk_R2": ((-1.2, 3.7, 2.5), (2.6, -4.4, 0.9)),           # von hinten: Wechseltisch, Fenster, Haustür, Hauptbuch
    "Blick_Lager_0": ((3.6, 5.35, 2.0), (3.6, 9.5, 0.7)),            # die große Truhe, Truhen, Barren
    "Blick_Lager_L1": ((5.7, 5.4, 2.0), (1.4, 8.8, 0.7)),            # Truhen links, Münzsäcke
    "Blick_Lager_R1": ((1.5, 5.4, 2.0), (5.9, 8.8, 0.8)),            # Barren und Truhen rechts
    "Blick_Pfand_0": ((-4.3, 5.35, 2.1), (-4.3, 9.5, 0.9)),          # Regale mit Pfandgut, Pfandtisch
    "Blick_Pfand_L1": ((-3.3, 5.35, 2.1), (-5.9, 9.2, 0.9)),         # linke Wand, Fenster, Laute
    "Blick_Pfand_R1": ((-5.3, 5.35, 2.1), (-2.7, 9.2, 0.9)),         # Wappenschild, Gitterschränke
    "Blick_Kontor_0": ((-8.75, -1.2, 2.2), (-8.75, 3.5, 0.8)),       # Landkarte, Schreibtisch
    "Blick_Kontor_L1": ((-6.9, 3.1, 2.2), (-10.9, -1.2, 0.8)),       # Fenster, Truhe, Hauptbücher
    "Blick_Kontor_R1": ((-10.7, -1.1, 2.2), (-6.6, 2.6, 0.8)),       # Aktenregal, Tür zur Wechselstube
    "Blick_Empfang_0": ((9.5, -4.1, 2.4), (9.5, 2.0, 0.9)),          # Sessel, Wandteppich, eiserne Tür
    "Blick_Empfang_L1": ((12.1, -4.0, 2.4), (6.8, 1.6, 0.9)),        # Kredenz, Tür zur Wechselstube, Leuchter
    "Blick_Empfang_R1": ((6.9, -4.0, 2.4), (12.4, 0.4, 0.9)),        # der Kamin
    "Blick_Tresor_0": ((11.0, 2.85, 2.2), (10.4, 6.5, 0.8)),         # Kassenschränke, Truhen
    "Blick_Tresor_L1": ((12.2, 2.9, 2.3), (8.7, 6.2, 0.8)),          # Barren, Fensterschlitz
}

Oe = si.Oe
WERK_OEFFNUNGEN = {
    "vorne": [Oe(HOFTOR_X, 0.0, HOFTOR_B, HOFTOR_H), Oe(HAUSTUER_X, 0.0, HAUSTUER_B, HAUSTUER_H)]
             + [Oe(x, FENSTER_V, FENSTER_B, FENSTER_H) for x in FENSTER_X],
    "rechts": [Oe(EMPF_Y, 0.0, EMPF_B, EMPF_H)],
    "hinten": [Oe(PFAND_X, 0.0, TUER_B, TUER_H), Oe(LAGER_X, 0.0, TUER_B, TUER_H)],
    "links": [Oe(KONTOR_Y, 0.0, TUER_B, TUER_H)],
}
SCHILDER = {"Tuer_Werk_Lager": "Tür zum Tresorgewölbe", "Tuer_Lager_Werk": "Tür zur Wechselstube",
            "Tuer_Werk_Pfand": "Tür zur Pfandkammer", "Tuer_Pfand_Werk": "Tür zur Wechselstube",
            "Tuer_Werk_Kontor": "Tür zum Kontor", "Tuer_Kontor_Werk": "Tür zur Wechselstube",
            "Tuer_Werk_Empfang": "Tür zum Empfang", "Tuer_Empfang_Werk": "Tür zur Wechselstube",
            "Tuer_Empfang_Tresor": "Eiserne Tür zum Tresorturm", "Tuer_Tresor_Empfang": "Tür zum Empfang"}
RAUM_STUFE = {"Kontor": 2, "Empfang": 3, "Tresor": 3}

ecken = ia.ecken
drehkoerper, scheibe, am_ort = ai.drehkoerper, ai.scheibe, ai.am_ort
licht, wq = pi.licht, pi.wq
drehen = wi.drehen


# ─────────────────────────────────────────────────────────────────────────────
#  Hilfen
# ─────────────────────────────────────────────────────────────────────────────

def platz(liste, punkt, blick, taetigkeit, bewegung, nummer, z=0.0, sitz=None, stufe=None):
    """Anker Schreiber_<n> (fortlaufend) mit extras „taetigkeit“, „bewegung“, „platz“ (Füllreihenfolge je Tätigkeit) und bei
    sitzenden Plätzen „sitzhoehe“ (Oberkante der Bank über dem Fußpunkt)."""
    o = bs.anker(f"Schreiber_{len(liste) + 1}", (punkt[0], punkt[1], z), si.drehung_von(blick))
    o["taetigkeit"], o["bewegung"], o["platz"] = taetigkeit, bewegung, nummer
    if sitz is not None:
        o["sitzhoehe"] = sitz
    if stufe:
        o["stufe"] = stufe
    liste.append(o)
    return o


def kunde(liste, gruppe, punkt, blick, bewegung, sitz=None, stufe=None):
    """Anker Kunde_<Gruppe>_<n> (fortlaufend je Gruppe = Füllreihenfolge) mit extras „bewegung“, „platz“ und bei sitzenden
    Plätzen „sitzhoehe“."""
    nummer = sum(1 for a in liste if a.name.startswith(f"Kunde_{gruppe}_")) + 1
    o = bs.anker(f"Kunde_{gruppe}_{nummer}", (punkt[0], punkt[1], 0.0), si.drehung_von(blick))
    o["bewegung"], o["platz"] = bewegung, nummer
    if sitz is not None:
        o["sitzhoehe"] = sitz
    if stufe:
        o["stufe"] = stufe
    liste.append(o)
    return o


def gewaende(netz, st, wand, hoehe):
    """Gewände aus Sandstein um die Türen der Wand (Öffnungen ab dem Boden): Quader im Wechsel an beiden Seiten, oben ein Sturz
    (3 cm vor dem Putz, wie gardist_innen.eckquader)."""
    for m, o in wand.ops:
        if o.v > 0.05:
            continue
        u0, u1 = m - o.b / 2, m + o.b / 2
        stuecke = max(2, round(o.h / 0.36))
        for k in range(stuecke):
            v0, v1 = k * o.h / stuecke + 0.008, (k + 1) * o.h / stuecke - 0.008
            b = 0.3 if k % 2 else 0.18
            wq(netz, st, wand, u0 - b, u0, v0, v1, 0.0, 0.03, "Sandstein", ohne=("-y", "+x"))
            wq(netz, st, wand, u1, u1 + b, v0, v1, 0.0, 0.03, "Sandstein", ohne=("-y", "-x"))
        wq(netz, st, wand, u0 - 0.22, u1 + 0.22, o.h + 0.008, min(o.h + 0.3, hoehe - 0.02), 0.0, 0.03, "Sandstein", ohne=("-y",))


def putzraum(netz, st, x0, x1, y0, y1, hoehe, oeffnungen, ohne_aussen=(), ohne_laibung=(), enden=None, stoff="Kalkputz",
             eigene_laibung=()):
    """Gekalkte Wände (schmied_innen.wand) mit Sandsteingewänden um die Türen; `eigene_laibung`: Seiten, deren Laibungen
    rundbogenfenster bzw. tuerlaibung bauen. Gibt die Wände {Seite: innenausbau.Wand} zurück."""
    kanten = ecken(x0, x1, y0, y1)
    netz.alle = True
    for seite in ("vorne", "rechts", "hinten", "links"):
        si.wand(netz, st, *kanten[seite], hoehe, oeffnungen.get(seite, []), stoff=stoff, dicke=WAND, aussen=seite not in ohne_aussen,
                laibung=seite not in ohne_laibung and seite not in eigene_laibung, enden=(enden or {}).get(seite, (True, True)))
    netz.alle = False
    waende = {s: ia.Wand(*kanten[s], oeffnungen.get(s, [])) for s in kanten}
    for w in waende.values():
        gewaende(netz, st, w, hoehe)
    return waende


def tuer_frei(wand):
    """Stücke [(u0, u1)] um die Türen der Wand samt Gewänden – dort keine Täfelung."""
    return [(m - o.b / 2 - 0.34, m + o.b / 2 + 0.34) for m, o in wand.ops if o.v < 0.05]


def tuerlaibung(netz, st, wand, o):
    """Laibung einer Tür durch die ganze Wand (gekalkt, Schwelle aus Sandstein) – für Wände mit eigene_laibung."""
    u = next(m for m, oo in wand.ops if oo is o)
    pr.laibung(netz, st, wand, u - o.b / 2, u + o.b / 2, 0.0, o.h, WAND, stoff="Kalkputz", unten=True)


def rundbogenfenster(netz, st, anker, wand, o, stufe=1, butzen=False):
    """Rundbogenfenster in der Öffnung o (Rechteck in der Wandfläche): die Zwickel über dem Bogen in der Wandfläche, die
    Laibung im Halbkreis bis zur Scheibe („Fenster“) in der Mitte der Wand, eine Sohlbank aus Sandstein, davor zwei Gitterstäbe
    und ein Querstab; mit `butzen` Butzenscheiben in Bleiruten statt der Stäbe. Licht_ „Fenster“ davor."""
    u = next(m for m, oo in wand.ops if oo is o)
    r = o.b / 2
    vk = o.v + o.h - r
    tief = 0.27
    bogen = [(u + r * math.cos(math.pi * k / 10), vk + r * math.sin(math.pi * k / 10)) for k in range(11)]
    alle, netz.alle = netz.alle, True
    for ecke, teil in (((u + r, o.v + o.h), bogen[:6]), ((u - r, o.v + o.h), bogen[5:])):
        for p, q in zip(teil, teil[1:]):
            netz.vieleck_nach([wand.p(*ecke), wand.p(*p), wand.p(*q)], wand.n, st["Kalkputz"])
    umriss = [(u - r, o.v), (u + r, o.v)] + bogen                                     # gegen den Uhrzeigersinn
    for i in range(len(umriss)):
        a, b = umriss[i], umriss[(i + 1) % len(umriss)]
        quad = [wand.p(a[0], a[1], 0.0), wand.p(b[0], b[1], 0.0), wand.p(b[0], b[1], -tief), wand.p(a[0], a[1], -tief)]
        achse = wand.p(u, max(o.v + 0.3, min(vk, (a[1] + b[1]) / 2)), -tief / 2)
        netz.vieleck_nach(quad, achse - sum(quad, Vector()) / 4, st["Sandstein" if i == 0 else "Kalkputz"])
    netz.alle = alle
    netz.vieleck_nach([wand.p(p[0], p[1], -tief) for p in umriss], wand.n, st["Fenster"])
    wq(netz, st, wand, u - r - 0.06, u + r + 0.06, o.v - 0.05, o.v + 0.005, -0.02, 0.06, "Sandstein", ohne=("-y",))   # Sohlbank
    if butzen:
        d = 0.14
        r0, r1 = d * 0.5, d * 0.36
        v = o.v + d * 0.55
        reihe = 0
        while v < o.v + o.h - d * 0.3:
            x = -r + d * 0.55 + (d / 2 if reihe % 2 else 0.0)
            while x < r - d * 0.45:
                if v < vk or math.hypot(x, v - vk) < r - d * 0.5:
                    for k in range(6):
                        a0, a1 = 2 * math.pi * k / 6, 2 * math.pi * (k + 1) / 6
                        netz.vieleck_nach([wand.p(u + x + rr * math.cos(a), v + rr * math.sin(a), -tief + 0.004)
                                           for rr, a in ((r1, a0), (r0, a0), (r0, a1), (r1, a1))], wand.n, st["Blei"])
                x += d
            v += d * 0.87
            reihe += 1
    else:
        for du in (-r / 2, r / 2):
            netz.stab(wand.p(u + du, o.v, -tief + 0.08), wand.p(u + du, vk + math.sqrt(r * r - du * du) - 0.01, -tief + 0.08), 0.025, 0.025,
                      st["Eisen"], quer=wand.n, ohne=("+x", "-x"))
        netz.stab(wand.p(u - r, o.v + 0.55, -tief + 0.07), wand.p(u + r, o.v + 0.55, -tief + 0.07), 0.025, 0.025, st["Eisen"], quer=wand.n,
                  ohne=("+x", "-x"))
        netz.stab(wand.p(u, o.v, -tief + 0.02), wand.p(u, vk + r - 0.01, -tief + 0.02), 0.05, 0.03, st["Fachwerk"], quer=wand.n,
                  ohne=("+x", "-x"))
    p = wand.p(u, o.v + o.h / 2, 0.1)
    anker.append(licht(p, (wand.n.x, wand.n.y, 0), "Fenster", stufe))


def malerei(netz, st, waende, hoehe, frei=None, sockel="Sockelgruen", fries="Deckenrot"):
    """Gemalter Sockel (bis 0,9 m, oben eine Ockerlinie) und ein Fries unter der Decke (Rauten zwischen Ockerlinien) auf dem
    Putz, wenige Millimeter davor; Öffnungen samt Gewänden und die Stücke `frei` = {Seite: [(u0, u1)]} bleiben frei."""
    for seite, w in waende.items():
        f = (frei or {}).get(seite, [])
        for v0, v1, dw, stoff in ((0.0, 0.88, 0.003, sockel), (0.88, 0.91, 0.004, "Ocker"), (hoehe - 0.42, hoehe - 0.39, 0.004, "Ocker"),
                                  (hoehe - 0.39, hoehe - 0.13, 0.003, fries), (hoehe - 0.13, hoehe - 0.1, 0.004, "Ocker")):
            for u0, u1 in pi.freie_stuecke(w, v0, v1, rand=0.32 if v0 < 2.0 else 0.04, frei=f if v0 < 2.0 else ()):
                netz.vieleck_nach([w.p(u0, v0, dw), w.p(u1, v0, dw), w.p(u1, v1, dw), w.p(u0, v1, dw)], w.n, st[stoff])
        vm = hoehe - 0.26
        for u0, u1 in pi.freie_stuecke(w, hoehe - 0.39, hoehe - 0.13, rand=0.04):
            n = max(1, round((u1 - u0) / 0.3))
            for k in range(n):
                um = u0 + (u1 - u0) * (k + 0.5) / n
                netz.vieleck_nach([w.p(um - 0.11, vm, 0.005), w.p(um, vm - 0.1, 0.005), w.p(um + 0.11, vm, 0.005), w.p(um, vm + 0.1, 0.005)],
                                  w.n, st["Ocker" if k % 2 else "Kalkputz"])


def bemalte_decke(decke, st, x0, x1, y0, y1, hoehe, abdeckung=None, saat=0):
    """Balkendecke (innenausbau.balkendecke), bemalt: die Balken unten in roten und ockerfarbenen Feldern, auf den Brettern
    zwischen ihnen ein Rahmen in Ocker und eine Ranke mit Blüten (rot, blau) entlang der Mitte – 3 mm unter dem Holz."""
    ia.balkendecke(decke, st, x0, x1, y0, y1, hoehe, abdeckung=abdeckung, saat=saat)
    anzahl = max(2, round((x1 - x0) / 1.05))
    xs = [x0 + (x1 - x0) * (k + 0.5) / anzahl for k in range(anzahl)]
    bb, bh = ia.BALKEN_B, ia.BALKEN_H
    alle, decke.alle = decke.alle, True
    z = hoehe - bh - 0.003
    for x in xs:                                                                       # Balken: Felder im Wechsel
        n = max(2, round((y1 - y0) / 0.45))
        for k in range(n):
            ya, yb = y0 + (y1 - y0) * k / n + 0.01, y0 + (y1 - y0) * (k + 1) / n - 0.01
            decke.vieleck_nach([(x - bb / 2 + 0.025, ya, z), (x + bb / 2 - 0.025, ya, z), (x + bb / 2 - 0.025, yb, z), (x - bb / 2 + 0.025, yb, z)],
                               (0, 0, -1), st["Deckenrot" if k % 2 else "Ocker"])
    z = hoehe - 0.003
    grenzen = [x0] + [g for x in xs for g in (x - bb / 2, x + bb / 2)] + [x1]
    zufall = random.Random(saat)
    for fa, fb in zip(grenzen[::2], grenzen[1::2]):                                    # Felder zwischen den Balken
        if fb - fa < 0.3:
            continue
        for xa, xb, ya, yb in ((fa + 0.06, fb - 0.06, y0 + 0.06, y0 + 0.09), (fa + 0.06, fb - 0.06, y1 - 0.09, y1 - 0.06),
                               (fa + 0.06, fa + 0.09, y0 + 0.06, y1 - 0.06), (fb - 0.09, fb - 0.06, y0 + 0.06, y1 - 0.06)):
            decke.vieleck_nach([(xa, ya, z), (xb, ya, z), (xb, yb, z), (xa, yb, z)], (0, 0, -1), st["Ocker"])
        xm = (fa + fb) / 2
        y = y0 + 0.3
        k = 0
        while y < y1 - 0.3:                                                            # Ranke: Stiel, Blätter, Blüten
            yb = min(y + 0.3, y1 - 0.3)
            s0, s1 = 0.06 * math.sin(y * 5.0), 0.06 * math.sin(yb * 5.0)
            decke.vieleck_nach([(xm + s0 - 0.01, y, z - 0.0005), (xm + s0 + 0.01, y, z - 0.0005), (xm + s1 + 0.01, yb, z - 0.0005),
                                (xm + s1 - 0.01, yb, z - 0.0005)], (0, 0, -1), st["Deckengruen"])
            s = 1 if k % 2 else -1
            if k % 3 == 2:
                c = Vector((xm + s0 + s * 0.12, y, z - 0.001))
                farbe = zufall.choice(("Deckenrot", "Deckenblau"))
                decke.vieleck_nach([c + Vector((0.06 * math.cos(2 * math.pi * t / 6), 0.06 * math.sin(2 * math.pi * t / 6), 0)) for t in range(6)],
                                   (0, 0, -1), st[farbe])
                decke.vieleck_nach([c + Vector((0.022 * math.cos(2 * math.pi * t / 6), 0.022 * math.sin(2 * math.pi * t / 6), -0.0005))
                                    for t in range(6)], (0, 0, -1), st["Ocker"])
            else:
                decke.vieleck_nach([(xm + s0, y, z - 0.001), (xm + s0 + s * 0.05, y - 0.03, z - 0.001), (xm + s0 + s * 0.12, y + 0.02, z - 0.001),
                                    (xm + s0 + s * 0.05, y + 0.05, z - 0.001)], (0, 0, -1), st["Deckengruen"])
            y = yb
            k += 1
    decke.alle = alle


def kassettendecke(decke, st, x0, x1, y0, y1, hoehe, abdeckung=WAND):
    """Kassettendecke: Bretter (Unterseite in `hoehe`), darunter ein Gitter aus Rippen (12 cm tief) mit einer Leiste in jedem
    Feld; die Felder blau gemalt, in der Mitte eine goldene Rosette."""
    alle, decke.alle = decke.alle, True
    ia.waagrecht(decke, (x0, x1, y0, y1), hoehe, False, st["Taeferholz"])
    nx, ny = max(2, round((x1 - x0) / 0.9)), max(2, round((y1 - y0) / 0.9))
    xs = [x0 + (x1 - x0) * k / nx for k in range(nx + 1)]
    ys = [y0 + (y1 - y0) * k / ny for k in range(ny + 1)]
    t, b = 0.12, 0.1
    for x in xs[1:-1]:
        decke.quader((x, (y0 + y1) / 2, hoehe - t / 2), (b, y1 - y0, t), st["Taeferholz dunkel"], ohne=("+z",))
    for y in ys[1:-1]:
        for xa, xb in zip(xs, xs[1:]):
            a, e = xa + (b / 2 if xa > x0 else 0.0), xb - (b / 2 if xb < x1 else 0.0)
            decke.quader(((a + e) / 2, y, hoehe - t / 2), (e - a, b, t), st["Taeferholz dunkel"], ohne=("+z", "+x", "-x"))
    for xa, xb in zip(xs, xs[1:]):
        for ya, yb in zip(ys, ys[1:]):
            a0 = xa + (b / 2 if xa > x0 else 0.0) + 0.04
            a1 = xb - (b / 2 if xb < x1 else 0.0) - 0.04
            b0 = ya + (b / 2 if ya > y0 else 0.0) + 0.04
            b1 = yb - (b / 2 if yb < y1 else 0.0) - 0.04
            for p, q, r_, s_ in ((a0, a1, b0, b0 + 0.04), (a0, a1, b1 - 0.04, b1), (a0, a0 + 0.04, b0, b1), (a1 - 0.04, a1, b0, b1)):
                decke.quader(((p + q) / 2, (r_ + s_) / 2, hoehe - 0.02), (q - p, s_ - r_, 0.04), st["Taeferholz dunkel"], ohne=("+z",))
            ia.waagrecht(decke, (a0 + 0.05, a1 - 0.05, b0 + 0.05, b1 - 0.05), hoehe - 0.003, False, st["Deckenblau"])
            c = Vector(((a0 + a1) / 2, (b0 + b1) / 2, hoehe - 0.006))
            decke.vieleck_nach([c + Vector((0.09 * math.cos(2 * math.pi * k / 8), 0.09 * math.sin(2 * math.pi * k / 8), 0)) for k in range(8)],
                               (0, 0, -1), st["Gold"])
            for k in range(4):                                                        # Blätter der Rosette
                w = math.pi / 4 + math.pi / 2 * k
                d = Vector((math.cos(w), math.sin(w), 0))
                q = Vector((-d.y, d.x, 0))
                decke.vieleck_nach([c + d * 0.08, c + d * 0.16 + q * 0.04, c + d * 0.22, c + d * 0.16 - q * 0.04], (0, 0, -1), st["Ocker"])
    if abdeckung is not None:
        ia.waagrecht(decke, (x0 - abdeckung, x1 + abdeckung, y0 - abdeckung, y1 + abdeckung), hoehe + ia.UEBER, True, st["Holz alt"])
    decke.alle = alle


def gewoelberaum(netz, decke, st, x0, x1, y0, y1, joche, z_k, f, loecher, stoff="Kalkputz", sockel=0.6, saat=0):
    """Raum unter `joche` Kreuzgratgewölben nebeneinander entlang x (prediger_innen.gewoelbe_joch), Gurtbögen dazwischen; die
    Wände bis unter die Kappen (Schildbögen), die Löcher {Seite: [(u0, u1, v0, v1)]} bleiben frei; ein Sockel aus Haustein."""
    kanten = ecken(x0, x1, y0, y1)
    w = {s: pr.wand(*kanten[s]) for s in kanten}
    breite = (x1 - x0) / joche
    hx = hy = None
    for j in range(joche):
        a, b = x0 + breite * j, x0 + breite * (j + 1)
        hx, hy = pr.gewoelbe_joch(decke, st, a, b, y0, y1, z_k, f, n=10, saat=saat + j)
        cx = (a + b) / 2
        for seite in ("vorne", "hinten"):
            u0, u1 = sorted((pr.u_von(w[seite], a), pr.u_von(w[seite], b)))
            pr.schildbogen_wand(netz, st, w[seite], u0, u1, z_k, lambda uu, s=seite, h=hx, c=cx: h(pr.welt_s(w[s], uu) - c),
                                loecher.get(seite, []), stoff=stoff, n=10)
        if j > 0:
            pr.gurtbogen(decke, st, (a, y0), (a, y1), z_k, hy, (1, 0))
    cy = (y0 + y1) / 2
    for seite in ("links", "rechts"):
        pr.schildbogen_wand(netz, st, w[seite], 0.0, w[seite].laenge, z_k, lambda uu, s=seite: hy(pr.welt_s(w[s], uu) - cy),
                            loecher.get(seite, []), stoff=stoff, n=12)
    if sockel:
        for seite in w:
            pr.sockel(netz, st, w[seite], loecher.get(seite, []), hoehe=sockel)
    return w


def loch(wand, s, o_v, b, h):
    """Loch (u0, u1, v0, v1) in der Wand bei der Weltkoordinate s."""
    u = pr.u_von(wand, s)
    return (u - b / 2, u + b / 2, o_v, o_v + h)


# ─────────────────────────────────────────────────────────────────────────────
#  Kleinteile: Münzen, Waagen, Leuchter
# ─────────────────────────────────────────────────────────────────────────────

def muenzsaeule(netz, st, fuss, n, r=0.014, stoff="Gold"):
    """Säule aus n Münzen (je 3 mm): ein Zylinder, alle fünf Münzen eine Kerbe, oben ein Deckel."""
    f = Vector(fuss)
    h = 0.003 * n
    profil = [(0.0, r)]
    for k in range(5, n, 5):
        profil += [(0.003 * k - 0.0006, r), (0.003 * k, r * 0.9), (0.003 * k + 0.0006, r)]
    drehkoerper(netz, st, f, profil + [(h, r)], stoff, n=6)
    scheibe(netz, st, f, r, f.z + h, stoff, n=6)


def muenzschale(netz, st, fuss, r=0.09, voll="Gold"):
    """Flache Schale aus Zinn mit Münzen darin."""
    drehkoerper(netz, st, fuss, [(0.0, r * 0.55), (0.012, r * 0.6), (0.04, r), (0.045, r * 0.97)], "Zinn", n=8)
    drehkoerper(netz, st, fuss, [(0.012, r * 0.55), (0.045, r * 0.93)], "Zinn", n=8, innen=True)
    drehkoerper(netz, st, Vector(fuss) + Vector((0, 0, 0.012)), [(0.0, r * 0.75), (0.018, r * 0.55), (0.024, 0.0)], voll, n=8)


def waage(netz, st, fuss, blick, bewegt=None, gewicht=True):
    """Balkenwaage nach figuren.WIEGEN auf dem Tisch: Fuß aus Holz, Säule und Balken aus Messing, die Zunge oben, zwei Schalen an je
    drei Schnüren; auf der rechten Schale (rechts der Figur, die die Waage bedient) eine Münze, auf der linken ein Gewicht. `fuss`
    ist der Fuß der Säule auf dem Tisch, `blick` die Blickrichtung der Figur. Mit `bewegt` (Liste) sind Balken und Schalen eigene
    Netze (Waagbalken, Waagschale_L, Waagschale_R), ihr Ursprung im Drehpunkt bzw. in der Mitte des Schalenbodens."""
    w = fg.WIEGEN
    f = Vector(fuss)
    vor = Vector((blick[0], blick[1], 0)).normalized()
    links = Vector((-vor.y, vor.x, 0))
    netz.quader((0, 0, 0), (0.24, 0.1, 0.03), st["Holz alt"],
                Matrix.Translation(f + Vector((0, 0, 0.015))) @ Matrix((-links, vor, Vector((0, 0, 1)))).transposed().to_4x4())
    drehkoerper(netz, st, f + Vector((0, 0, 0.03)), [(0.0, 0.025), (0.02, 0.012), (w["achse"] - 0.03, 0.008)], "Messing", n=6)
    dreh = f + Vector((0, 0, w["achse"]))
    if bewegt is None:
        balken, schalen = netz, {"L": netz, "R": netz}
    else:
        balken = bs.Netz("Waagbalken", alle=True)
        schalen = {"L": bs.Netz("Waagschale_L", alle=True), "R": bs.Netz("Waagschale_R", alle=True)}
    balken.stab(dreh - links * (w["arm"] + 0.01), dreh + links * (w["arm"] + 0.01), 0.012, 0.012, st["Messing"], quer=vor)
    balken.stab(dreh, dreh + Vector((0, 0, 0.09)), 0.008, 0.004, st["Messing"], quer=vor)                    # Zunge
    balken.stab(dreh + vor * 0.008, dreh - vor * 0.008, 0.016, 0.016, st["Eisen"], quer=Vector((0, 0, 1)))   # Achse
    netz.stab(dreh + Vector((0, 0, -0.02)) + links * 0.0, dreh + Vector((0, 0, 0.11)), 0.03, 0.006, st["Messing"], quer=vor)   # Schere
    mitten = {}
    for seite, s in (("L", 1), ("R", -1)):
        boden = f + links * s * w["arm"] + Vector((0, 0, w["schale"]))
        mitten[seite] = boden
        n_ = schalen[seite]
        aufh = dreh + links * s * w["arm"]
        for k in range(3):
            a = 2 * math.pi * k / 3 + 0.5
            rand = boden + Vector((w["schale_r"] * 0.85 * math.cos(a), w["schale_r"] * 0.85 * math.sin(a), 0.012))
            n_.stab(aufh, rand, 0.003, 0.003, st["Messing"])
        drehkoerper(n_, st, boden, [(0.0, w["schale_r"] * 0.5), (0.008, w["schale_r"] * 0.85), (0.014, w["schale_r"])], "Messing", n=10)
        if seite == "R":
            scheibe(n_, st, boden, w["muenze"], boden.z + 0.006, "Gold", n=8)
            drehkoerper(n_, st, boden + Vector((0, 0, 0.003)), [(0.0, w["muenze"]), (0.003, w["muenze"])], "Gold", n=8)
        elif gewicht:
            drehkoerper(n_, st, boden + Vector((0, 0, 0.004)), [(0.0, 0.012), (0.016, 0.011), (0.02, 0.006), (0.026, 0.006)], "Messing", n=6)
    if bewegt is None:
        return None
    o_b = am_ort(balken, dreh)
    o_l, o_r = am_ort(schalen["L"], mitten["L"]), am_ort(schalen["R"], mitten["R"])
    bewegt += [o_b, o_l, o_r]
    return o_b, o_l, o_r


def waage_bewegung(balken, schale_l, schale_r, links):
    """Animation „Arbeit“: der Balken neigt sich nach figuren.waage_winkel (+: die rechte Schale sinkt), die Schalen hängen an den
    Enden und bleiben waagrecht. `links`: Richtung zur linken Seite der Figur (Achse des Balkens)."""
    w = fg.WIEGEN
    achse = Vector((0, 0, 1)).cross(Vector(links)).normalized()                      # nach vorn: Drehachse des Balkens
    ruhe_b, ruhe_l, ruhe_r = balken.rotation_euler.copy(), schale_l.location.copy(), schale_r.location.copy()

    def winkel(bild):
        return math.radians(fg.waage_winkel((bild % ARBEIT_BILDER) / ARBEIT_BILDER))

    def b_bew(bild):
        m = Matrix.Rotation(winkel(bild), 4, achse) @ ruhe_b.to_matrix().to_4x4()
        return None, m.to_euler("XYZ", ruhe_b), None

    def s_bew(ruhe, s):
        def bew(bild):
            a = winkel(bild)
            return ruhe + Vector(links) * s * w["arm"] * (math.cos(a) - 1) + Vector((0, 0, s * w["arm"] * math.sin(a))), None, None
        return bew
    return [(balken, b_bew), (schale_l, s_bew(ruhe_l, 1)), (schale_r, s_bew(ruhe_r, -1))]


def stehleuchter(netz, st, anker, fuss, stufe=1, h=1.5):
    """Standleuchter aus Messing: Dreifuß, Schaft mit Knäufen, oben drei Arme mit Tropftellern und Kerzen („Glut“)."""
    f = Vector(fuss)
    for k in range(3):
        a = 2 * math.pi * k / 3
        netz.stab(f + Vector((0, 0, 0.12)), f + Vector((0.2 * math.cos(a), 0.2 * math.sin(a), 0.0)), 0.03, 0.03, st["Messing"])
    drehkoerper(netz, st, f + Vector((0, 0, 0.08)), [(0.0, 0.05), (0.06, 0.025), (0.5, 0.018), (0.55, 0.035), (0.6, 0.018), (h - 0.05, 0.016),
                                                     (h, 0.04)], "Messing", n=8)
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.3
        d = Vector((math.cos(a), math.sin(a), 0))
        ende = f + d * 0.2 + Vector((0, 0, h + 0.12))
        netz.stab(f + Vector((0, 0, h)), ende - Vector((0, 0, 0.02)), 0.02, 0.02, st["Messing"])
        drehkoerper(netz, st, ende - Vector((0, 0, 0.02)), [(0.0, 0.045), (0.015, 0.045)], "Messing", n=8)
        drehkoerper(netz, st, ende, [(0.0, 0.02), (0.22, 0.018)], "Kerze", n=6)
        drehkoerper(netz, st, ende + Vector((0, 0, 0.22)), [(0.0, 0.0), (0.012, 0.011), (0.035, 0.008), (0.06, 0.0)], "Glut", n=5)
    anker.append(licht(f + Vector((0, 0, h + 0.45)), (0, 0, 1), "Glut", stufe))


def haengeleuchter(netz, st, anker, mitte, z, decke, stufe=1, arme=6):
    """Hängeleuchter aus Messing an einer Stange: Kugel, geschwungene Arme mit Tropftellern und Kerzen („Glut“)."""
    m = Vector(mitte)
    alle, netz.alle = netz.alle, True
    netz.stab(Vector((m.x, m.y, z + 0.3)), Vector((m.x, m.y, decke)), 0.02, 0.02, st["Eisen"])
    drehkoerper(netz, st, Vector((m.x, m.y, z - 0.25)), [(0.0, 0.0), (0.06, 0.07), (0.16, 0.09), (0.28, 0.05), (0.42, 0.03),
                                                         (0.55, 0.05), (0.6, 0.02)], "Messing", n=10)
    for k in range(arme):
        a = 2 * math.pi * k / arme
        d = Vector((math.cos(a), math.sin(a), 0))
        pts = [Vector((m.x, m.y, z - 0.12)) + d * 0.06, Vector((m.x, m.y, z - 0.18)) + d * 0.25, Vector((m.x, m.y, z - 0.05)) + d * 0.42]
        for p, q in zip(pts, pts[1:]):
            netz.stab(p, q, 0.025, 0.025, st["Messing"])
        drehkoerper(netz, st, pts[-1], [(0.0, 0.045), (0.015, 0.045)], "Messing", n=8)
        drehkoerper(netz, st, pts[-1] + Vector((0, 0, 0.015)), [(0.0, 0.018), (0.18, 0.016)], "Kerze", n=6)
        drehkoerper(netz, st, pts[-1] + Vector((0, 0, 0.195)), [(0.0, 0.0), (0.012, 0.011), (0.035, 0.008), (0.06, 0.0)], "Glut", n=5)
    netz.alle = alle
    anker.append(licht(Vector((m.x, m.y, z + 0.2)), (0, 0, 1), "Lampe", stufe))


def zettel(netz, st, an, vor, saat=0):
    """Pfandzettel: ein Papierschild an einer Schnur, das an `an` hängt und nach `vor` zeigt, darauf zwei Zeilen."""
    zufall = random.Random(saat)
    vor = Vector(vor).normalized()
    quer = Vector((-vor.y, vor.x, 0))
    a = Vector(an)
    unten = a + vor * 0.012 + Vector((0, 0, -0.07))
    netz.stab(a, unten + Vector((0, 0, 0.035)), 0.003, 0.003, st["Seil"])
    w = zufall.uniform(-0.25, 0.25)
    q = (quer * math.cos(w) + Vector((0, 0, math.sin(w)))) * 0.035
    o = Vector((0, 0, 0.025)) - quer * math.sin(w) * 0.0
    ecken_ = [unten - q - o, unten + q - o, unten + q + o, unten - q + o]
    netz.vieleck_nach(ecken_, vor, st["Papier"])
    netz.vieleck_nach(list(reversed(ecken_)), -vor, st["Papier"])
    for k, v in enumerate((0.008, -0.006)):
        netz.vieleck_nach([unten - q * 0.75 + Vector((0, 0, v - 0.002)) + vor * 0.001, unten + q * (0.6 - 0.3 * k) + Vector((0, 0, v - 0.002)) + vor * 0.001,
                           unten + q * (0.6 - 0.3 * k) + Vector((0, 0, v + 0.002)) + vor * 0.001, unten - q * 0.75 + Vector((0, 0, v + 0.002)) + vor * 0.001],
                          vor, st["Tinte"])


def rechenbrett(netz, st, M, b=0.42, t=0.3):
    """Rechenbrett (örtlich, +y zur Figur hin, z oben, Mitte auf der Tischplatte): Holzrahmen, dunkles Feld mit hellen Linien
    quer, Rechenpfennige darauf."""
    si.q(netz, st, M, (0, 0, 0.012), (b, t, 0.024), "Taeferholz dunkel")
    si.q(netz, st, M, (0, 0, 0.0255), (b - 0.05, t - 0.05, 0.003), "Tuch dunkel")
    for k in range(5):
        y = -t / 2 + 0.04 + (t - 0.08) * k / 4
        si.q(netz, st, M, (0, y, 0.0275), (b - 0.07, 0.004, 0.001), "Kreide")
    zufall = random.Random(int(M.translation.x * 100 + M.translation.y * 37))
    for k in range(9):
        y = -t / 2 + 0.04 + (t - 0.08) * zufall.randint(0, 4) / 4
        drehkoerper(netz, st, M @ Vector((zufall.uniform(-b / 2 + 0.05, b / 2 - 0.05), y, 0.027)), [(0.0, 0.011), (0.003, 0.011)], "Messing", n=6)


def hauptbuch(netz, st, M, b=0.5, t=0.36, farbe="Buchrot"):
    """Aufgeschlagenes, dickes Hauptbuch (örtlich: x quer, y das Buch hinauf, z heraus; Mitte unten auf der Unterlage): Einband
    mit Messingecken, zwei Seitenblöcke mit Zeilen und Spalten in Tinte."""
    netz.quader((0, 0, 0.006), (b, t, 0.012), st[farbe], M)
    for s in (-1, 1):
        netz.quader((s * b / 4, 0, 0.03), (b / 2 - 0.02, t - 0.03, 0.036), st["Papier"], M, ohne=("-z",))
        for k in range(10):
            y = t / 2 - 0.04 - 0.028 * k
            netz.vieleck([M @ Vector((s * b / 4 - b / 4 + 0.03, y - 0.002, 0.0485)), M @ Vector((s * b / 4 + b / 4 - 0.07, y - 0.002, 0.0485)),
                          M @ Vector((s * b / 4 + b / 4 - 0.07, y + 0.002, 0.0485)), M @ Vector((s * b / 4 - b / 4 + 0.03, y + 0.002, 0.0485))], st["Tinte"])
        netz.vieleck([M @ Vector((s * b / 4 + b / 4 - 0.065, -t / 2 + 0.03, 0.0485)), M @ Vector((s * b / 4 + b / 4 - 0.06, -t / 2 + 0.03, 0.0485)),
                      M @ Vector((s * b / 4 + b / 4 - 0.06, t / 2 - 0.03, 0.0485)), M @ Vector((s * b / 4 + b / 4 - 0.065, t / 2 - 0.03, 0.0485))], st["Deckenrot"])
        for sy in (-1, 1):
            netz.quader((s * (b / 2 - 0.025), sy * (t / 2 - 0.025), 0.013), (0.05, 0.05, 0.004), st["Messing"], M)


# ─────────────────────────────────────────────────────────────────────────────
#  Wechselstube
# ─────────────────────────────────────────────────────────────────────────────

def stube_huelle(werk, decke, st, anker):
    """Gekalkte Wände mit Sandsteingewänden, gemaltem Sockel und Fries, vorn die Rundbogenfenster, bemalte Balkendecke, warme
    Steinplatten."""
    waende = putzraum(werk, st, WX0, WX1, WY0, WY1, H_WERK, WERK_OEFFNUNGEN, ohne_aussen=("hinten", "links", "rechts"),
                      eigene_laibung=("vorne",))
    for o in WERK_OEFFNUNGEN["vorne"]:
        if o.v < 0.05:
            tuerlaibung(werk, st, waende["vorne"], o)
        else:
            rundbogenfenster(werk, st, anker, waende["vorne"], o)
    frei = {"links": [(pr.u_von(waende["links"], SCHRANK_Y) - 0.8, pr.u_von(waende["links"], SCHRANK_Y) + 0.8)]}
    malerei(werk, st, waende, H_WERK, frei=frei)
    bemalte_decke(decke, st, WX0, WX1, WY0, WY1, H_WERK, abdeckung=WAND, saat=3)
    pr.steinplatten(werk, st, WX0, WX1, WY0, WY1, saat=7, breite=0.6, stoffe=("Platte warm", "Platte hell", "Platte warm", "Platte"))
    return waende


def wechseltisch(netz, st, plaetze, kunden, anker):
    """Der Wechseltisch: ein Tresen (figuren.KANZEL hoch) mit Füllungen zu den Kunden hin, darüber das grüne Tuch, das vorn
    herabhängt; darauf zwei Balkenwaagen, Münzsäulen in Gold und Silber, Schalen mit Münzen, Schuldbriefe, Tintenfass, Siegel und
    zwei Leuchter. Dahinter die vier Plätze zum Bedienen (die Hände auf dem Tisch bei KANZEL), davor die der Kaufleute."""
    m, L, B, H = WECHSEL, WECHSEL_L, WECHSEL_B, fg.KANZEL[1]
    netz.quader(m + Vector((0, 0, H - 0.03)), (L, B, 0.06), st["Tischplatte"])
    netz.quader(m + Vector((0, -B / 2 + 0.05, (H - 0.06) / 2)), (L - 0.04, 0.04, H - 0.06), st["Taeferholz"])          # Füllung vorn
    for k in range(6):                                                                      # Rahmen der Füllungen
        x = m.x - L / 2 + 0.02 + (L - 0.04) * k / 5
        netz.quader((x, m.y - B / 2 + 0.025, (H - 0.06) / 2), (0.08, 0.012, H - 0.06), st["Taeferholz dunkel"])
    for z in (0.08, H - 0.12):
        netz.quader((m.x, m.y - B / 2 + 0.021, z), (L - 0.04, 0.012, 0.08), st["Taeferholz dunkel"])
    for sx in (-1, 1):                                                                      # Wangen
        netz.quader(m + Vector((sx * (L / 2 - 0.04), 0, (H - 0.06) / 2)), (0.06, B - 0.04, H - 0.06), st["Taeferholz dunkel"])
    netz.quader(m + Vector((0, B / 2 - 0.25, 0.3)), (L - 0.1, 0.4, 0.03), st["Holz"])                                     # Ablage hinten
    z = H + 0.003                                                                           # das Tuch
    netz.quader(m + Vector((0, -0.05, z)), (L - 0.06, B - 0.16, 0.006), st["Tuch"])
    netz.quader(m + Vector((0, -B / 2 - 0.004, H - 0.08)), (L - 0.06, 0.006, 0.17), st["Tuch"])
    netz.quader(m + Vector((0, -B / 2 - 0.006, H - 0.155)), (L - 0.06, 0.006, 0.03), st["Tuch dunkel"])
    oben = H + 0.006
    for k, x in enumerate((m.x - 1.75, m.x + 1.75)):                                       # zwei Waagen an den Enden
        waage(netz, st, Vector((x, m.y - 0.12, oben)), (0, 1))
    zufall = random.Random(11)
    for x in BEDIEN_X:                                                                      # vor jedem Platz Münzsäulen und eine Schale
        for j in range(3):
            stoff = "Gold" if j != 1 else "Silber"
            muenzsaeule(netz, st, Vector((x - 0.12 + 0.08 * j, m.y - 0.22, oben)), zufall.randint(5, 14), stoff=stoff)
        muenzschale(netz, st, Vector((x + 0.2, m.y - 0.2, oben)), voll="Gold" if zufall.random() < 0.6 else "Silber")
    for k, x in enumerate((m.x - 0.9, m.x + 0.0, m.x + 0.9)):                               # Schuldbriefe, Tintenfass, Siegel
        M = Matrix.Translation((x, m.y - 0.05, oben)) @ Matrix.Rotation(0.12 * (k - 1), 4, "Z")
        netz.quader((0, 0, 0.001), (0.18, 0.24, 0.002), st["Papier"], M)
        for j in range(5):
            netz.quader((-0.01, 0.08 - 0.03 * j, 0.003), (0.13 - 0.03 * (j % 2), 0.004, 0.001), st["Tinte"], M)
    bs.achteck(netz, st, Vector((m.x + 0.45, m.y + 0.0, oben)), 0.025, 0.0, 0.045, "Kohle", n=6)
    netz.stab(Vector((m.x + 0.45, m.y, oben + 0.04)), Vector((m.x + 0.49, m.y - 0.08, oben + 0.2)), 0.012, 0.03, st["Federweiss"])
    bs.achteck(netz, st, Vector((m.x - 0.45, m.y - 0.02, oben)), 0.02, 0.0, 0.07, "Messing", n=6)     # Petschaft
    bs.achteck(netz, st, Vector((m.x - 0.38, m.y - 0.04, oben)), 0.012, 0.0, 0.03, "Siegelwachs", n=6)
    for x in (m.x - 1.2, m.x + 1.2):                                                       # Leuchter
        pr.leuchter(netz, st, Vector((x, m.y + 0.0, oben)), h=0.22)
        anker.append(licht(Vector((x, m.y, oben + 0.7)), (0, 0, 1), "Glut"))
    for nummer, i in enumerate((1, 2, 0, 3), 1):                                          # Bedienen: erst die Mitte
        platz(plaetze, (BEDIEN_X[i], BEDIEN_Y), (0, -1), "Bedienen", "Predigen", nummer)
    for i in (1, 2, 0, 3):
        kunde(kunden, "Kaufmann", (BEDIEN_X[i], KAUF_Y), (0, 1), "Stehen")


def goldwaage(netz, st):
    """Gekauft: die feine Goldwaage in einem Glaskasten auf dem Wechseltisch (Rahmen aus Messing, das Glas denkt man sich), darin
    eine kleine Balkenwaage und ein Kästchen mit Gewichten."""
    m = WECHSEL + Vector((0.95, -0.12, fg.KANZEL[1] + 0.006))
    b, t, h = 0.36, 0.24, 0.42
    netz.quader(m + Vector((0, 0, 0.02)), (b + 0.04, t + 0.04, 0.04), st["Taeferholz dunkel"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader(m + Vector((sx * b / 2, sy * t / 2, 0.04 + h / 2)), (0.012, 0.012, h), st["Messing"])
        netz.quader(m + Vector((sx * b / 2, 0, 0.04 + h)), (0.012, t, 0.012), st["Messing"])
    for sy in (-1, 1):
        netz.quader(m + Vector((0, sy * t / 2, 0.04 + h)), (b, 0.012, 0.012), st["Messing"])
    netz.quader(m + Vector((0, 0, 0.04 + h + 0.03)), (0.08, 0.04, 0.04), st["Messing"])                       # Griff
    fuss = m + Vector((0, 0.03, 0.04))
    drehkoerper(netz, st, fuss, [(0.0, 0.02), (0.015, 0.006), (0.24, 0.005)], "Messing", n=6)
    dreh = fuss + Vector((0, 0, 0.24))
    netz.stab(dreh - Vector((0.11, 0, 0)), dreh + Vector((0.11, 0, 0)), 0.006, 0.006, st["Gold"])
    for s in (-1, 1):
        for k in range(3):
            a = 2 * math.pi * k / 3
            netz.stab(dreh + Vector((s * 0.1, 0, 0)), fuss + Vector((s * 0.1 + 0.025 * math.cos(a), 0.025 * math.sin(a), 0.05)), 0.002, 0.002,
                      st["Gold"])
        drehkoerper(netz, st, fuss + Vector((s * 0.1, 0, 0.04)), [(0.0, 0.015), (0.01, 0.03)], "Gold", n=8)
    kasten = m + Vector((-0.11, -0.07, 0.04))
    netz.quader(kasten + Vector((0, 0, 0.015)), (0.1, 0.07, 0.03), st["Taeferholz"])
    for k in range(4):
        bs.achteck(netz, st, kasten + Vector((-0.03 + 0.02 * k, 0.0, 0.03)), 0.004 + 0.002 * k, 0.0, 0.008 + 0.003 * k, "Messing", n=6)


def werkzeug_bank(netz, st):
    """Gekauft (Werkzeug1): Rechenbretter mit Pfennigen auf dem Wechseltisch (zwischen den Plätzen) und auf dem Wägetisch."""
    oben = fg.KANZEL[1] + 0.006
    for x in (WECHSEL.x - 0.9, WECHSEL.x + 0.0):
        rechenbrett(netz, st, Matrix.Translation((x, WECHSEL.y + 0.17, oben)), b=0.34, t=0.22)
    t = fg.WIEGEN["tisch"]
    for x in (WIEGE_X[0] + 0.42, WIEGE_X[1] + 0.42):
        rechenbrett(netz, st, Matrix.Translation((x, WIEGE_Y - 0.62, t)), b=0.2, t=0.2)


def waegetisch(netz, st, plaetze, anker, bewegt):
    """Der Wägetisch hinten rechts (figuren.WIEGEN hoch): je Platz eine Balkenwaage (die am ersten Platz bewegt sich in der
    Animation), rechts daneben der Stapel ungeprüfter Münzen und dahinter die geprüften; dazu ein Probierstein, ein Kasten mit
    Gewichten, eine Kerze. Vier Plätze zum Wiegen, erst die Mitte."""
    w = fg.WIEGEN
    t = w["tisch"]
    tief = 0.7
    ym = WIEGE_Y - WIEGE_RAND - tief / 2
    x0, x1 = WIEGE_X[0] - 0.45, WIEGE_X[-1] + 0.45
    netz.quader(((x0 + x1) / 2, ym, t - 0.03), (x1 - x0, tief, 0.06), st["Tischplatte"])
    for x in (x0 + 0.08, x1 - 0.08):
        netz.quader((x, ym, (t - 0.06) / 2), (0.08, tief - 0.1, t - 0.06), st["Taeferholz dunkel"])
    netz.quader(((x0 + x1) / 2, ym - tief / 2 + 0.05, (t - 0.06) / 2 + 0.1), (x1 - x0 - 0.2, 0.03, t - 0.26), st["Taeferholz"])
    netz.quader(((x0 + x1) / 2, ym, 0.12), (x1 - x0 - 0.2, tief - 0.1, 0.03), st["Holz"])
    waagen = None
    blick = (0, -1)
    for i, x in enumerate(WIEGE_X):
        fuss = Vector((x, WIEGE_Y, 0))
        saeule = drehen((0.0, w["vor"], t), fuss, blick)
        if i == 1:                                                                          # am Platz 1 des Wiegens
            waagen = waage(netz, st, saeule, blick, bewegt=bewegt)
        else:
            waage(netz, st, saeule, blick)
        sx, sv = w["stapel"]
        for j, (dx, dv) in enumerate(((0.0, 0.0), (0.035, 0.03), (-0.03, 0.035))):
            muenzsaeule(netz, st, drehen((sx + dx, sv + dv, t), fuss, blick), 8 - 2 * j, r=w["muenze"], stoff="Gold")
        gx, gv = w["geprueft"]
        for j, (dx, dv) in enumerate(((0.0, 0.0), (0.03, -0.03))):
            muenzsaeule(netz, st, drehen((gx + dx, gv + dv, t), fuss, blick), 4 - j, r=w["muenze"], stoff="Gold")
    for nummer, i in enumerate((1, 2, 0, 3), 1):
        platz(plaetze, (WIEGE_X[i], WIEGE_Y), (0, -1), "Wiegen", "Wiegen", nummer)
    stein = Vector((WIEGE_X[2] + 0.45, ym + 0.12, t))                                     # Probierstein mit Strichen
    netz.quader(stein + Vector((0, 0, 0.012)), (0.14, 0.09, 0.024), st["Kohle"])
    for k in range(4):
        netz.quader(stein + Vector((-0.04 + 0.025 * k, 0, 0.0245)), (0.004, 0.06, 0.001), st["Gold"])
    kasten = Vector((x1 - 0.12, ym - 0.05, t))                                            # Kasten mit Gewichten
    netz.quader(kasten + Vector((0, 0, 0.025)), (0.16, 0.12, 0.05), st["Taeferholz"])
    for k in range(5):
        bs.achteck(netz, st, kasten + Vector((-0.055 + 0.028 * k, 0.0, 0.05)), 0.006 + 0.002 * k, 0.0, 0.006 + 0.004 * k, "Messing", n=6)
    di.kerze(netz, st, Vector((WIEGE_X[2] + 0.45, ym - 0.17, t)), 0.12, anker=anker)
    return waagen


def rechentisch(netz, st, plaetze, anker):
    """Der Rechen- und Zähltisch links: Tisch und zwei Bänke wie beim Wirt; links vor jedem Platz ein Rechentuch mit Linien und
    Rechenpfennigen (figuren.RECHNEN), rechts vor jedem Platz ein Münzhaufen (figuren.ZAEHLEN), dazwischen Beutel, eine Kerze.
    Vier Plätze zum Rechnen, vier zum Zählen, je erst die Bank hinten (Gesicht zur Kamera)."""
    m = RECHEN
    wi.tisch(netz, st, m, 3.4, 0.8)
    for s in (-1, 1):
        wi.bank(netz, st, (m.x - 1.7, m.y + s * 0.62, 0), (m.x + 1.7, m.y + s * 0.62, 0))
    r = fg.RECHNEN
    sitz = fg.VOELKER["mensch"]["knie"] - 0.02
    tisch = 0.78
    for (taetigkeit, bewegung, xs) in (("Rechnen", "Rechnen", RECHEN_X), ("Zaehlen", "Zaehlen", ZAEHL_X)):
        nummer = 1
        for sy, blick in ((1, (0, -1)), (-1, (0, 1))):
            for x in (xs[1], xs[0]):
                punkt = Vector((x, m.y + sy * 0.62, 0))
                if taetigkeit == "Rechnen":
                    M = Matrix.Translation(drehen((-0.05, r["vor"] + r["linie"] * 2, tisch), punkt, blick)) \
                        @ Matrix.Rotation(math.radians(si.drehung_von(blick)), 4, "Z")
                    si.q(netz, st, M, (0, 0, 0.002), (0.5, 0.34, 0.004), "Tuch dunkel")
                    for k in range(r["linien"]):
                        si.q(netz, st, M, (0, r["linie"] * (k - 2), 0.0045), (0.46, 0.005, 0.001), "Kreide")
                    for k, (s_, li) in enumerate(((0.12, 0), (0.17, 0), (-0.08, 1), (-0.12, 1), (0.05, 2), (-0.2, 3), (0.1, 4), (0.15, 3))):
                        drehkoerper(netz, st, drehen((s_, r["vor"] + r["linie"] * li, tisch + 0.005), punkt, blick),
                                    [(0.0, r["pfennig"]), (0.003, r["pfennig"])], "Messing", n=6)
                else:
                    hs, hv, hh = fg.ZAEHLEN["haufen"]
                    di.muenzen(netz, st, drehen((hs, hv, sitz + hh), punkt, blick), r=0.05, h=0.025, saat=nummer + 20 * (sy > 0))
                platz(plaetze, punkt, blick, taetigkeit, bewegung, nummer, sitz=SITZ)
                nummer += 1
    for k, x in enumerate((m.x + 0.05, m.x + 1.55)):                                        # Beutel
        di.beutel(netz, st, Vector((x, m.y + 0.05 * (k % 2), tisch)), r=0.045, h=0.07)
    di.kerze(netz, st, m + Vector((-0.35, 0.0, tisch)), 0.12, anker=anker)


def schreibpult(netz, st, pult):
    """Schreibpult wie im Skriptorium des Predigers (prediger_innen.schreibpult: Platte, Tintenhorn und Blatt bei figuren.SCHREIBEN)
    aus dunkler Eiche, mit dem aufgeschlagenen Hauptbuch: Hocker, zwei Wangen, die schräge Platte mit Leiste, unten ein Bord mit
    Büchern."""
    f = pult.platz
    drehkoerper(netz, st, f + Vector((0, 0, SITZ - 0.05)), [(0.0, 0.17), (0.05, 0.17)], "Taeferholz", n=8)
    for k in range(3):
        w = 2 * math.pi * k / 3 + 0.4
        netz.stab(f + Vector((0.14 * math.cos(w), 0.14 * math.sin(w), 0)), f + Vector((0.1 * math.cos(w), 0.1 * math.sin(w), SITZ - 0.05)),
                  0.04, 0.04, st["Taeferholz dunkel"])
    netz.quader((0, 0, -0.015), (0.72, 0.52, 0.03), st["Taeferholz"], pult.matrix(0.0, 0.03, -0.028))
    netz.quader((0, -0.255, 0.01), (0.72, 0.03, 0.04), st["Taeferholz dunkel"], pult.matrix(0.0, 0.03, -0.028))
    oben3 = Vector((0, 0, 1))
    for s_ in (-0.33, 0.33):
        o = pult.p(s_, 0.0, -pult.sitz)
        M = Matrix((pult.vor, pult.links, oben3)).transposed().to_4x4()
        M.translation = o
        umriss = []
        for q in (pult.p(s_, 0.3, -pult.sitz), pult.p(s_, 0.55, -pult.sitz), pult.platte(s_, 0.25, -0.05), pult.platte(s_, -0.2, -0.05)):
            d = q - o
            umriss.append((d.dot(pult.vor), d.z))
        di.flach(netz, st, umriss, M, 0.04, "Taeferholz dunkel")
    netz.quader((0, 0, 0), (0.6, 0.3, 0.03), st["Taeferholz"], pult.lok(0.0, 0.42, -pult.sitz + 0.35))
    for k, f_ in enumerate(("Buchrot", "Leder", "Buchbraun", "Buchgruen")):
        b_ = 0.07 + 0.02 * (k % 2)
        netz.quader((0, 0, 0), (b_, 0.24, 0.26 + 0.02 * k), st[f_], pult.lok(-0.22 + 0.13 * k, 0.42, -pult.sitz + 0.365 + 0.13 + 0.01 * k))
    hs, hv, hh = fg.SCHREIBEN["horn"]
    mund = pult.p(hs, hv, hh)
    drehkoerper(netz, st, mund - Vector((0, 0, 0.13)), [(0.0, 0.0), (0.03, 0.012), (0.1, 0.02), (0.13, 0.024)], "Federweiss", n=6)
    scheibe(netz, st, mund, 0.019, mund.z - 0.008, "Tinte", n=6)
    pr.aufgeschlagen(netz, st, pult, farben=True)


def schreibpulte(netz, st, plaetze, anker):
    """Vier Schreibpulte hinten links mit Blick in die Stube, auf jedem ein aufgeschlagenes Hauptbuch; zwischen ihnen Kerzen auf
    Wandarmen. Vier Plätze fürs Hauptbuch (Schreiben), erst die Mitte."""
    for x in SCHREIB_X:
        schreibpult(netz, st, pr.Pult((x, SCHREIB_Y, 0), (0, -1)))
    for nummer, i in enumerate((1, 2, 0, 3), 1):
        platz(plaetze, (SCHREIB_X[i], SCHREIB_Y), (0, -1), "Hauptbuch", "Schreiben", nummer, sitz=SITZ)
    for x in ((SCHREIB_X[0] + SCHREIB_X[1]) / 2, (SCHREIB_X[2] + SCHREIB_X[3]) / 2):           # Wandarme mit Kerzen
        M = si.rahmen(x, WY1, 180)
        netz.stab(M @ Vector((0, 0.0, 1.85)), M @ Vector((0, 0.22, 1.9)), 0.03, 0.03, st["Eisen"])
        drehkoerper(netz, st, M @ Vector((0, 0.24, 1.9)), [(0.0, 0.05), (0.02, 0.05)], "Messing", n=8)
        di.kerze(netz, st, M @ Vector((0, 0.24, 1.92)), 0.16, anker=anker)


def hauptbuchpult(netz, st):
    """Bedienung_Bank_Hauptbuch: ein Stehpult aus Eiche, darauf das große Hauptbuch der Bank an einer Kette, Tintenfass und Feder;
    unten ein Fach mit weiteren Büchern. Die Kunden stehen davor (Blick +y)."""
    m = BUCH
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.stab(m + Vector((sx * 0.3, sy * 0.2, 0)), m + Vector((sx * 0.3, sy * 0.2, 1.0 + 0.08 * sy)), 0.07, 0.07, st["Taeferholz dunkel"])
    netz.quader(m + Vector((0, 0, 0.3)), (0.66, 0.46, 0.03), st["Taeferholz"])
    for k in range(4):
        netz.quader(m + Vector((-0.2 + 0.12 * k, 0.0, 0.31 + 0.14)), (0.09, 0.3, 0.26 - 0.02 * k), st[("Buchrot", "Leder", "Buchbraun", "Leder")[k]])
    neig = math.atan2(0.16, 0.44)
    platte = Matrix.Translation(m + Vector((0, 0, 1.03))) @ Matrix.Rotation(neig, 4, "X")
    netz.quader((0, 0, 0), (0.74, 0.5, 0.04), st["Taeferholz"], platte)
    netz.quader((0, -0.25, 0.04), (0.74, 0.04, 0.05), st["Taeferholz dunkel"], platte)
    hauptbuch(netz, st, platte @ Matrix.Translation((0, 0.02, 0.02)), b=0.56, t=0.4)
    for k in range(6):                                                                      # Kette zum Pfosten
        a = platte @ Vector((0.28, 0.18, 0.04)) + Vector((0.012 * k, 0.0, -0.06 * k))
        netz.stab(a, a + Vector((0.012, 0, -0.06)), 0.02, 0.01, st["Eisen"], quer=(0, 1, 0) if k % 2 else (1, 0, 0))
    tf = platte @ Vector((-0.31, 0.22, 0.04))
    bs.achteck(netz, st, tf, 0.025, 0.0, 0.05, "Kohle", n=6)
    netz.stab(tf + Vector((0, 0, 0.04)), tf + Vector((0.05, 0.05, 0.2)), 0.012, 0.03, st["Federweiss"])


def kundenbank(netz, st, kunden):
    """Kundenbank an der rechten Wand vorn: Bank mit Lehne und grünem Kissen; Kunde_Buerger_2 sitzt darauf. Darüber ein Bord mit
    Kerze."""
    x = WX1 - 0.25
    y0, y1 = KUNDENBANK_Y
    netz.quader((x, (y0 + y1) / 2, SITZ - 0.03), (0.42, y1 - y0, 0.06), st["Taeferholz"])
    for y in (y0 + 0.1, y1 - 0.1):
        netz.quader((x, y, (SITZ - 0.06) / 2), (0.38, 0.06, SITZ - 0.06), st["Taeferholz dunkel"])
    netz.quader((WX1 - 0.03, (y0 + y1) / 2, SITZ + 0.35), (0.06, y1 - y0, 0.6), st["Taeferholz dunkel"])
    netz.quader((x - 0.02, (y0 + y1) / 2, SITZ + 0.02), (0.36, y1 - y0 - 0.12, 0.04), st["Tuch"])
    kunde(kunden, "Buerger", (BUCH.x, BUCH.y - 0.62), (0, 1), "Stehen")
    kunde(kunden, "Buerger", (x + 0.05, (y0 + y1) / 2), (-1, 0), "Sitzen", sitz=SITZ)


def wandschrank(netz, st):
    """Bedienung_Einrichten_Wandschrank an der linken Wand: hoher Schrank aus Eiche mit offenen Flügeltüren, darin Fächer mit
    Kästchen, Rollen und Büchern; oben ein Gesims."""
    M = si.rahmen(WX0, SCHRANK_Y, -90)
    b, h, t = 1.4, 2.2, 0.5
    for x in (-b / 2, b / 2):
        si.q(netz, st, M, (x, t / 2, h / 2), (0.05, t, h), "Taeferholz dunkel", ohne=("-y",))
    si.q(netz, st, M, (0, 0.015, h / 2), (b, 0.03, h), "Taeferholz", ohne=("-y",))
    si.q(netz, st, M, (0, t / 2, h + 0.05), (b + 0.12, t + 0.06, 0.1), "Taeferholz dunkel")
    zufall = random.Random(5)
    for k, z in enumerate((0.05, 0.6, 1.15, 1.7)):
        si.q(netz, st, M, (0, t / 2, z), (b - 0.05, t - 0.02, 0.03), "Taeferholz")
        x = -b / 2 + 0.1
        while x < b / 2 - 0.2:
            art = zufall.choice(("kasten", "buch", "rolle", "kasten"))
            if art == "kasten":
                si.q(netz, st, M, (x + 0.1, t / 2, z + 0.1), (0.2, 0.28, 0.16), "Taeferholz")
                si.q(netz, st, M, (x + 0.1, t / 2 + 0.141, z + 0.12), (0.04, 0.004, 0.05), "Messing")
                x += 0.26
            elif art == "buch":
                for j in range(3):
                    si.q(netz, st, M, (x + 0.04 + 0.07 * j, t / 2, z + 0.15), (0.06, 0.26, 0.28), zufall.choice(("Buchrot", "Leder", "Buchbraun")))
                x += 0.25
            else:
                for j in range(3):
                    netz.stab(M @ Vector((x + 0.05, 0.12, z + 0.06 + 0.07 * j)), M @ Vector((x + 0.05, t - 0.05, z + 0.06 + 0.07 * j)), 0.07, 0.07,
                              st["Papier"])
                x += 0.14
    for s in (-1, 1):                                                                       # offene Flügel
        scharnier = M @ Vector((s * b / 2, t, 0.05))
        d = (M.to_3x3() @ Vector((s * 0.5, 0.85, 0))).normalized()
        quer = Vector((0, 0, 1)).cross(d)
        netz.quader((0, 0, 0), (1, 1, 1), st["Taeferholz"], Matrix.Translation(scharnier + d * b / 4 + Vector((0, 0, h / 2 - 0.05)))
                    @ Matrix((d, quer, Vector((0, 0, 1)))).transposed().to_4x4() @ Matrix.Diagonal((b / 2 - 0.02, 0.03, h - 0.12, 1)))
        for z in (0.4, h - 0.4):
            netz.stab(scharnier + Vector((0, 0, z)), scharnier + d * 0.3 + Vector((0, 0, z)), 0.04, 0.034, st["Eisen"], quer=quer)


def kassenschrank(netz, st):
    """Gekauft: der eiserne Kassenschrank an der linken Wand hinten – auf kurzen Füßen, Bänder und Nieten, zwei Schlösser und ein
    großer Griff."""
    M = si.rahmen(WX0, KASSE_Y, -90)
    b, h, t = 0.95, 1.25, 0.6
    si.q(netz, st, M, (0, t / 2, 0.08 + h / 2), (b, t, h), "Eisen", ohne=("-y",))
    for x in (-b / 2 + 0.06, b / 2 - 0.06):
        si.q(netz, st, M, (x, t / 2, 0.04), (0.08, t - 0.04, 0.08), "Eisen")
    for z in (0.25, 0.08 + h / 2, h - 0.1):
        si.q(netz, st, M, (0, t + 0.008, z), (b - 0.04, 0.016, 0.06), "Kohle")
    for x in (-b / 2 + 0.05, b / 2 - 0.05):
        si.q(netz, st, M, (x, t + 0.008, 0.08 + h / 2), (0.05, 0.016, h - 0.04), "Kohle")
    for k in range(6):
        for j in range(4):
            si.q(netz, st, M, (-b / 2 + 0.12 + (b - 0.24) * j / 3, t + 0.018, 0.18 + (h - 0.2) * k / 5), (0.025, 0.012, 0.025), "Eisen")
    for x in (-0.18, 0.18):
        si.q(netz, st, M, (x, t + 0.02, 0.85), (0.1, 0.02, 0.14), "Messing")
        si.q(netz, st, M, (x, t + 0.031, 0.85), (0.015, 0.004, 0.05), "Kohle")
    netz.stab(M @ Vector((-0.12, t + 0.04, 0.6)), M @ Vector((0.12, t + 0.04, 0.6)), 0.03, 0.03, st["Eisen"])


def kronleuchter_stufe(netz, st, anker):
    """Stufe 3: ein Hängeleuchter aus Messing über dem Wechseltisch."""
    haengeleuchter(netz, st, anker, (WECHSEL.x, WECHSEL.y + 0.6), 2.9, H_WERK - ia.BALKEN_H, stufe=3)


def hauszeichen(netz, st, M):
    """Ab Stufe 2: das Hauszeichen der Bank an der Wand – ein blauer Schild mit drei goldenen Münzen in einem Rahmen aus Eiche."""
    di.flach(netz, st, [(x * 1.3, 2.35 + z * 1.3) for x, z in bs.SCHILD], M @ Matrix.Translation((0, 0.02, 0)), 0.04, "Taeferholz dunkel")
    di.flach(netz, st, [(x * 1.15, 2.35 + z * 1.15) for x, z in bs.SCHILD], M @ Matrix.Translation((0, 0.045, 0)), 0.012, "Deckenblau")
    for x, z in ((-0.13, 2.55), (0.13, 2.55), (0.0, 2.25)):
        di.flach(netz, st, [(x + 0.075 * math.cos(2 * math.pi * k / 12), z + 0.075 * math.sin(2 * math.pi * k / 12)) for k in range(12)],
                 M @ Matrix.Translation((0, 0.057, 0)), 0.012, "Gold")


def stube_kram(netz, st, anker):
    """Was in der Wechselstube sonst steht: Laternen an der Decke, Geldsäcke neben dem Wägetisch, Mäntel an Haken neben der
    Haustür."""
    for p in (Vector((RECHEN.x, RECHEN.y + 1.2, 2.4)), Vector((3.2, 1.3, 2.45))):
        ti.laterne(netz, st, p, H_WERK - ia.BALKEN_H)
        anker.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"))
    for k, (dx, dy) in enumerate(((0.0, 0.0), (0.22, 0.12), (0.1, -0.2), (0.12, 0.0))):       # Geldsäcke
        di.beutel(netz, st, Vector((WIEGE_X[-1] + 0.8 + dx, 3.9 + dy, 0.0 if k < 3 else 0.2)), r=0.13, h=0.22)
    M = si.rahmen(-0.75, WY0, 0)                                                            # Haken mit Mänteln
    si.q(netz, st, M, (0, 0.03, 2.0), (0.6, 0.05, 0.08), "Taeferholz dunkel", ohne=("-y",))
    for k, x in enumerate((-0.18, 0.18)):
        netz.stab(M @ Vector((x, 0.03, 1.98)), M @ Vector((x, 0.13, 2.01)), 0.025, 0.025, st["Messing"])
        di.flach(netz, st, [(-0.2, 2.0), (0.2, 2.0), (0.26, 1.42), (0.2, 0.98), (-0.22, 0.95), (-0.27, 1.42)],
                 M @ Matrix.Translation((x, 0.1, 0)) @ Matrix.Rotation(0.05 * (k * 2 - 1), 4, "Y"), 0.04, ("Damast", "Kutte")[k])


def stube_stufen(netze, st, anker):
    """Stufen (kumulativ, unter Raum_Werk): in Stufe 1 sind die Türen zum Kontor und zum Empfang vermauert, in Stufe 2 nur noch die
    zum Empfang; ab Stufe 2 hängt das Hauszeichen über der Kundenbank, in Stufe 3 dazu ein Hängeleuchter über dem Wechseltisch und
    ein Läufer hinter dem Tisch."""
    k_ = ecken(WX0, WX1, WY0, WY1)
    for k, netz in enumerate(netze, 1):
        if k == 1:
            ai.vermauert(netz, st, *k_["links"], Oe(KONTOR_Y, 0.0, TUER_B, TUER_H), rueck=0.06)
        if k < 3:
            ai.vermauert(netz, st, *k_["rechts"], Oe(EMPF_Y, 0.0, EMPF_B, EMPF_H), rueck=0.06)
        if k >= 2:
            hauszeichen(netz, st, si.rahmen(WX1, -3.25, 90))
        if k == 3:
            kronleuchter_stufe(netz, st, anker)
            netz.quader((WECHSEL.x, BEDIEN_Y + 0.05, 0.004), (WECHSEL_L - 0.2, 0.8, 0.008), st["Teppich rot"])
            netz.vieleck_nach([(WECHSEL.x + sx * (WECHSEL_L - 0.4) / 2, BEDIEN_Y + 0.05 + sy * 0.3, 0.011) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))],
                              (0, 0, 1), st["Teppich blau"])


# ─────────────────────────────────────────────────────────────────────────────
#  Tresorgewölbe
# ─────────────────────────────────────────────────────────────────────────────

def lager_huelle(lager, decke, st, anker):
    """Zwei Kreuzgratgewölbe mit Gurtbogen, Wände aus Bruchstein mit Sockel, ein Lichtschlitz mit Gitter in der Rückwand,
    Steinplatten."""
    w = {s: pr.wand(*ecken(LX0, LX1, LY0, LY1)[s]) for s in ("vorne", "hinten")}
    loecher = {"vorne": [loch(w["vorne"], LAGER_X, 0.0, TUER_B, TUER_H)], "hinten": [loch(w["hinten"], 4.8, 1.5, 0.3, 0.5)]}
    gewoelberaum(lager, decke, st, LX0, LX1, LY0, LY1, 2, L_KAEMPFER, L_STICH, loecher, stoff="Bruchstein", saat=9)
    pr.steinplatten(lager, st, LX0, LX1, LY0, LY1, saat=9, breite=0.5)
    wh = w["hinten"]
    u0, u1, v0, v1 = loecher["hinten"][0]
    pr.laibung(lager, st, wh, u0, u1, v0, v1, 0.35, stoff="Bruchstein dunkel", unten=True)
    lager.vieleck_nach([wh.p(u0, v0, -0.35), wh.p(u1, v0, -0.35), wh.p(u1, v1, -0.35), wh.p(u0, v1, -0.35)], wh.n, st["Fenster"])
    for u in (u0 + 0.1, u1 - 0.1):
        lager.stab(wh.p(u, v0, -0.1), wh.p(u, v1, -0.1), 0.02, 0.02, st["Eisen"])
    anker.append(licht(wh.p((u0 + u1) / 2, (v0 + v1) / 2, 0.1), (wh.n.x, wh.n.y, 0), "Fenster"))


def grosse_truhe(netz, st):
    """Bedienung_Lager_Tresor: die große Geldtruhe an der Rückwand – ganz mit Eisen beschlagen, drei Schlösser, auf Holzböcken,
    davor zwei Münzsäcke."""
    M = si.rahmen(3.6, LY1, 180)
    b, h, t = 1.6, 0.75, 0.8
    si.q(netz, st, M, (0, t / 2 + 0.05, 0.06), (b + 0.1, t, 0.12), "Holz alt")
    si.q(netz, st, M, (0, t / 2 + 0.05, 0.12 + h / 2), (b, t, h), "Taeferholz dunkel", ohne=("-z",))
    si.q(netz, st, M, (0, t / 2 + 0.05, 0.12 + h + 0.04), (b + 0.04, t + 0.04, 0.08), "Eisen")
    for x in (-0.7, -0.35, 0.0, 0.35, 0.7):
        si.q(netz, st, M, (x, t + 0.056, 0.12 + h / 2), (0.06, 0.012, h), "Eisen")
        si.q(netz, st, M, (x, t / 2 + 0.05, 0.12 + h + 0.085), (0.06, t + 0.05, 0.012), "Eisen")
    for z in (0.25, 0.12 + h - 0.12):
        si.q(netz, st, M, (0, t + 0.058, z), (b, 0.012, 0.05), "Eisen")
    for x in (-0.5, 0.0, 0.5):
        si.q(netz, st, M, (x, t + 0.068, 0.12 + h - 0.22), (0.1, 0.02, 0.14), "Eisen")
        si.q(netz, st, M, (x, t + 0.079, 0.12 + h - 0.22), (0.015, 0.004, 0.05), "Kohle")
    for x in (-0.6, 0.55):
        di.beutel(netz, st, M @ Vector((x, t + 0.35, 0.0)), r=0.15, h=0.26)


def geldtruhen(netz, st):
    """Geldtruhen mit Eisenbändern an den Seitenwänden (zum Teil gestapelt), eine Bank mit Barren aus Gold und Silber, Münzsäcke."""
    for k, (x, y, dreh, z) in enumerate(((LX0, 6.1, -90, 0.0), (LX0, 7.5, -90, 0.0), (LX0, 7.5, -90, 0.68), (LX0, 8.8, -90, 0.0),
                                         (LX1, 6.2, 90, 0.0), (LX1, 6.2, 90, 0.68))):
        pr.truhe(netz, st, si.rahmen(x, y, dreh) @ Matrix.Translation((0, 0, z)), eisen=True)
    M = si.rahmen(LX1, 8.3, 90)                                                            # Bank mit Barren
    si.q(netz, st, M, (0, 0.3, 0.42), (1.5, 0.55, 0.06), "Holz alt")
    for x in (-0.65, 0.65):
        si.q(netz, st, M, (x, 0.3, 0.2), (0.08, 0.5, 0.4), "Holz alt")
    barren(netz, st, M, (0, 0.3, 0.45), 1.3, 0.45)
    for k, (x, y) in enumerate(((2.0, 9.0), (2.3, 8.75), (1.75, 8.6), (5.3, 5.5))):
        di.beutel(netz, st, Vector((x, y, 0)), r=0.14, h=0.24)


def barren(netz, st, M, mitte, laenge, tiefe, lagen=3):
    """Barren aus Gold und Silber in Lagen über Kreuz (örtliches System M, Mitte unten bei `mitte`)."""
    bx, by, bz = 0.18, 0.07, 0.045
    z = mitte[2]
    for lage in range(lagen):
        quer = lage % 2
        n = int((laenge if not quer else tiefe) / (by + 0.02)) - lage
        for k in range(max(1, n)):
            t = (k + 0.5) / max(1, n) - 0.5
            if not quer:
                c = (mitte[0] + t * laenge * 0.9, mitte[1], z + bz / 2)
                masse = (by, bx, bz)
            else:
                c = (mitte[0] + (k % 2 - 0.5) * 0.3 * (lage > 0), mitte[1] + t * tiefe * 0.8, z + bz / 2)
                masse = (bx, by, bz)
            si.q(netz, st, M, c, masse, "Gold" if (k + lage) % 3 else "Silber")
        z += bz


# ─────────────────────────────────────────────────────────────────────────────
#  Pfandkammer
# ─────────────────────────────────────────────────────────────────────────────

def pfand_huelle(netz, decke, st, anker):
    """Gekalkte Wände, ein kleines Fenster in der linken Wand, Dielen, Balkendecke."""
    oe = {"vorne": [Oe(PFAND_X, 0.0, TUER_B, TUER_H)], "links": [Oe(7.4, 1.4, 0.7, 0.9)], "hinten": [], "rechts": []}
    waende = putzraum(netz, st, PX0, PX1, PY0, PY1, H_PFAND, oe, ohne_aussen=("vorne", "rechts"), ohne_laibung=("vorne",),
                      enden={"vorne": (False, False)})
    si.fenster(netz, st, *ecken(PX0, PX1, PY0, PY1)["links"], oe["links"][0], dicke=WAND)
    w = waende["links"]
    u = next(m for m, o in w.ops if o.v > 0.05)
    anker.append(licht(w.p(u, 1.85, 0.1), (w.n.x, w.n.y, 0), "Fenster"))
    ia.balkendecke(decke, st, PX0, PX1, PY0, PY1, H_PFAND, saat=13)
    wi.dielen(netz, st, PX0, PX1, PY0, PY1, 0.0, saat=21)
    return waende


def pfandgut(netz, st, M, x, z, art, zufall, tiefe):
    """Ein Stück Pfandgut auf einem Regalboden (örtlich M, +y in den Raum) bei x, Höhe z; gibt die Breite zurück."""
    mitte = M @ Vector((x, tiefe * 0.5, z))
    vor = M.to_3x3() @ Vector((0, 1, 0))
    if art == "kanne":                                                                      # Silberkanne mit Henkel
        drehkoerper(netz, st, mitte, [(0.0, 0.05), (0.02, 0.06), (0.14, 0.045), (0.2, 0.03), (0.24, 0.035)], "Silber", n=8)
        netz.stab(mitte + Vector((0, 0, 0.2)), mitte + vor * 0.0 + M.to_3x3() @ Vector((0.08, 0, 0.12)), 0.02, 0.01, st["Silber"])
        return 0.16
    if art == "teller":                                                                     # Silberteller hochkant
        T = Matrix.Translation(mitte + Vector((0, 0, 0.12))) @ M.to_3x3().to_4x4() @ Matrix.Rotation(math.radians(80), 4, "X")
        netz.prisma([(0.12 * math.cos(2 * math.pi * k / 12), 0.12 * math.sin(2 * math.pi * k / 12)) for k in range(12)], -0.008, 0.008,
                    st["Silber"], matrix=T @ Matrix.Rotation(math.radians(90), 4, "Y"))
        return 0.18
    if art == "becher":
        for k in range(2):
            drehkoerper(netz, st, mitte + M.to_3x3() @ Vector((-0.04 + 0.08 * k, 0.03 * k, 0)),
                        [(0.0, 0.025), (0.06, 0.01), (0.07, 0.03), (0.13, 0.034)], "Silber", n=6)
        return 0.16
    if art == "stoff":                                                                      # Stoffballen, gerollt
        farbe = zufall.choice(("Stoff blau", "Stoff gelb", "Stoff rot", "Damast", "Leinen"))
        netz.stab(mitte + M.to_3x3() @ Vector((0, -tiefe * 0.35, 0.07)), mitte + M.to_3x3() @ Vector((0, tiefe * 0.35, 0.07)), 0.14, 0.14,
                  st[farbe], quer=(0, 0, 1))
        return 0.17
    if art == "werkzeug":                                                                   # Hammer und Zange
        netz.stab(mitte + M.to_3x3() @ Vector((-0.08, 0, 0.015)), mitte + M.to_3x3() @ Vector((0.1, 0.05, 0.015)), 0.03, 0.03, st["Holz"])
        netz.quader(mitte + M.to_3x3() @ Vector((0.11, 0.05, 0.03)), (0.05, 0.12, 0.05), st["Eisen"])
        netz.stab(mitte + M.to_3x3() @ Vector((-0.1, -0.06, 0.01)), mitte + M.to_3x3() @ Vector((0.08, -0.08, 0.01)), 0.02, 0.02, st["Eisen"])
        return 0.24
    if art == "leuchter":                                                                   # Leuchter aus Messing mit Kerze
        drehkoerper(netz, st, mitte, [(0.0, 0.07), (0.03, 0.03), (0.18, 0.018), (0.2, 0.05), (0.21, 0.05)], "Messing", n=6)
        drehkoerper(netz, st, mitte + Vector((0, 0, 0.21)), [(0.0, 0.022), (0.16, 0.02)], "Kerze", n=6)
        return 0.16
    farbe = zufall.choice(("Taeferholz", "Leder", "Holz alt"))                              # Kästchen
    netz.quader((0, 0, 0), (0.2, tiefe * 0.6, 0.14), st[farbe], Matrix.Translation(mitte + Vector((0, 0, 0.07))) @ M.to_3x3().to_4x4())
    return 0.22


def pfandregal(netz, st, M, breite, hoehe=2.1, tiefe=0.42, boeden=4, saat=0, zettel_an=True):
    """Regal mit Pfandgut (örtlich, +y in den Raum): Silbergeschirr, Stoffballen, Werkzeug, Leuchter, Kästchen; an jedem Stück
    ein Zettel an der Vorderkante des Bodens."""
    for x in (-breite / 2, breite / 2):
        si.q(netz, st, M, (x, tiefe / 2, hoehe / 2), (0.05, tiefe, hoehe), "Holz", ohne=("-y",))
    zufall = random.Random(saat)
    vor = M.to_3x3() @ Vector((0, 1, 0))
    for k in range(boeden):
        z = 0.1 + (hoehe - 0.15) * k / (boeden - 1)
        si.q(netz, st, M, (0, tiefe / 2, z), (breite - 0.05, tiefe, 0.04), "Holz", ohne=("-y",))
        if k == boeden - 1:
            continue
        x = -breite / 2 + 0.12
        while x < breite / 2 - 0.25:
            art = zufall.choice(("kanne", "teller", "becher", "stoff", "werkzeug", "leuchter", "kasten", "stoff"))
            breit = pfandgut(netz, st, M, x + 0.08, z + 0.02, art, zufall, tiefe)
            if zettel_an:
                zettel(netz, st, M @ Vector((x + 0.08, tiefe + 0.005, z - 0.0)), vor, saat=saat * 31 + k * 7 + int(x * 10))
            x += breit + zufall.uniform(0.04, 0.1)


def wappenschild(netz, st, M):
    """Ein verpfändeter Wappenschild an der Wand: blau mit einem goldenen Schrägbalken, Rand aus Eisen, darunter ein Zettel."""
    di.flach(netz, st, [(x * 1.2, 1.9 + z * 1.2) for x, z in bs.SCHILD], M @ Matrix.Translation((0, 0.03, 0)), 0.04, "Wappenblau", "Eisen")
    di.flach(netz, st, [(-0.36, 2.28), (-0.22, 2.38), (0.36, 1.68), (0.22, 1.58)], M @ Matrix.Translation((0, 0.053, 0)), 0.004, "Ocker")
    zettel(netz, st, M @ Vector((0.0, 0.06, 1.36)), M.to_3x3() @ Vector((0, 1, 0)), saat=3)


def laute(netz, st, fuss, dreh):
    """Eine Laute, auf dem Tisch liegend: gewölbter Bauch, Decke mit Rosette, Hals, abgeknickter Wirbelkasten."""
    M = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for k in range(10):                                                                     # Bauch aus Halbringen (liegt auf dem Rücken)
        a0, a1 = math.pi * k / 10, math.pi * (k + 1) / 10
        for j in range(8):
            t0, t1 = j / 8, (j + 1) / 8

            def p(t, a):
                r = 0.17 * math.sin(math.pi * (0.15 + 0.85 * t)) * (1.0 if t > 0.3 else 0.75 + 0.8 * t)
                return M @ Vector((-0.2 + 0.42 * t, r * math.cos(a), 0.13 * math.sin(a) * math.sin(math.pi * (0.15 + 0.85 * t)) * 0.6))
            quad = [p(t0, a0), p(t1, a0), p(t1, a1), p(t0, a1)]
            netz.vieleck_nach(quad, (sum(quad, Vector()) / 4 - M @ Vector((0.0, 0, 0.0))), st["Laute"])
    umriss = [(-0.2 + 0.42 * t, 0.17 * math.sin(math.pi * (0.15 + 0.85 * t)) * (1.0 if t > 0.3 else 0.75 + 0.8 * t)) for t in
              [k / 10 for k in range(11)]]
    decke = [M @ Vector((x, y, 0.001)) for x, y in umriss] + [M @ Vector((x, -y, 0.001)) for x, y in reversed(umriss)]
    netz.vieleck_nach(decke, (0, 0, 1), st["Lautendecke"])
    netz.vieleck_nach([M @ Vector((0.0 + 0.03 * math.cos(2 * math.pi * k / 8), 0.03 * math.sin(2 * math.pi * k / 8), 0.002)) for k in range(8)],
                      (0, 0, 1), st["Kohle"])
    netz.stab(M @ Vector((0.2, 0, 0.01)), M @ Vector((0.52, 0, 0.01)), 0.05, 0.025, st["Thronholz"], quer=(0, 0, 1))
    netz.stab(M @ Vector((0.52, 0, 0.01)), M @ Vector((0.64, 0, -0.04)), 0.05, 0.03, st["Thronholz"], quer=(0, 0, 1))
    for s in (-1, 1):
        netz.stab(M @ Vector((-0.12, s * 0.01, 0.004)), M @ Vector((0.52, s * 0.01, 0.022)), 0.003, 0.003, st["Kreide"])


def pfand_einrichtung(netz, st, anker):
    """Regale an der linken und der hinteren Wand, der Pfandtisch in der Mitte (Pfandbuch, Laute, Silberkanne, Kerze), der
    Wappenschild an der rechten Wand, eine Laterne."""
    pfandregal(netz, st, si.rahmen(PX0, 6.0, -90), 1.4, saat=41)
    pfandregal(netz, st, si.rahmen(PX0, 8.75, -90), 1.3, saat=43)
    pfandregal(netz, st, si.rahmen(-4.45, PY1, 180), 1.9, saat=47)
    wappenschild(netz, st, si.rahmen(PX1, 8.6, 90))
    m = Vector((-4.1, 7.3, 0))
    wi.tisch(netz, st, m, 1.4, 0.75)
    netz.quader(m + Vector((-0.3, 0.05, 0.78 + 0.02)), (0.3, 0.4, 0.04), st["Leder"])
    hauptbuch(netz, st, Matrix.Translation(m + Vector((-0.3, 0.05, 0.82))), b=0.38, t=0.28, farbe="Leder")
    laute(netz, st, m + Vector((0.25, -0.12, 0.78)), 20)
    zettel(netz, st, m + Vector((0.58, -0.3, 0.78)), (0, -1, 0), saat=9)
    di.kerze(netz, st, m + Vector((0.5, 0.25, 0.78)), 0.12, anker=anker)
    drehkoerper(netz, st, Vector((-5.0, 5.55, 0)), [(0.0, 0.2), (0.4, 0.22), (0.4, 0.2)], "Holz alt", n=8)   # Korb mit Stoffen
    for k in range(3):
        netz.stab(Vector((-5.12 + 0.12 * k, 5.45, 0.25)), Vector((-5.0 + 0.1 * k, 5.65, 0.62)), 0.1, 0.1,
                  st[("Stoff blau", "Stoff gelb", "Stoff rot")[k]])
    p = Vector((-3.4, 7.9, 2.4))
    ti.laterne(netz, st, p, H_PFAND - ia.BALKEN_H)
    anker.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"))


def gitterschraenke(netz, st):
    """Gekauft (Einrichtung „Pfandkammer“): zwei Gitterschränke an der rechten Wand, hinter den Eisengittern wertvolles Pfand,
    jedes Fach mit einem Vorhängeschloss."""
    for k, y in enumerate((5.9, 7.25)):
        M = si.rahmen(PX1, y, 90)
        b, h, t = 1.0, 1.9, 0.45
        for x in (-b / 2, b / 2):
            si.q(netz, st, M, (x, t / 2, h / 2), (0.05, t, h), "Taeferholz dunkel", ohne=("-y",))
        for z in (0.04, 0.68, 1.3, h):
            si.q(netz, st, M, (0, t / 2, z), (b, t, 0.04), "Taeferholz dunkel", ohne=("-y",))
        zufall = random.Random(61 + k)
        for j, z in enumerate((0.06, 0.7, 1.32)):
            x = -b / 2 + 0.1
            while x < b / 2 - 0.2:
                art = zufall.choice(("kanne", "becher", "leuchter", "kasten"))
                x += pfandgut(netz, st, M, x + 0.08, z, art, zufall, t * 0.8) + 0.05
            for i in range(5):                                                              # Gitter
                xx = -b / 2 + 0.04 + (b - 0.08) * i / 4
                si.q(netz, st, M, (xx, t - 0.01, z + 0.3), (0.012, 0.012, 0.56), "Eisen")
            for zz in (z + 0.04, z + 0.56):
                si.q(netz, st, M, (0, t - 0.01, zz), (b - 0.06, 0.014, 0.02), "Eisen")
            schloss = M @ Vector((0.12, t + 0.01, z + 0.28))
            netz.quader((0, 0, 0), (0.05, 0.02, 0.06), st["Messing"], Matrix.Translation(schloss) @ M.to_3x3().to_4x4())
            netz.stab(schloss + Vector((0, 0, 0.03)), schloss + Vector((0, 0, 0.07)), 0.03, 0.006, st["Eisen"],
                      quer=M.to_3x3() @ Vector((0, 1, 0)))


# ─────────────────────────────────────────────────────────────────────────────
#  Kontor
# ─────────────────────────────────────────────────────────────────────────────

def kontor_huelle(netz, decke, st, anker):
    """Gekalkte Wände mit Täfelung bis Brusthöhe, ein Rundbogenfenster mit Butzenscheiben in der linken Wand, Dielen,
    Balkendecke."""
    oe = {"rechts": [Oe(KONTOR_Y, 0.0, TUER_B, TUER_H)], "links": [Oe(1.0, 0.9, 1.0, 1.7)], "vorne": [], "hinten": []}
    waende = putzraum(netz, st, KX0, KX1, KY0, KY1, H_KONTOR, oe, ohne_aussen=("rechts",), ohne_laibung=("rechts",),
                      enden={"rechts": (False, False)}, eigene_laibung=("links",))
    rundbogenfenster(netz, st, anker, waende["links"], oe["links"][0], stufe=2, butzen=True)
    for seite, w in waende.items():
        pi.taefelung(netz, st, *ecken(KX0, KX1, KY0, KY1)[seite], oe[seite], frei=tuer_frei(w), hoehe=1.2)
    ia.balkendecke(decke, st, KX0, KX1, KY0, KY1, H_KONTOR, abdeckung=WAND, saat=29)
    wi.dielen(netz, st, KX0, KX1, KY0, KY1, 0.0, saat=31)
    return waende


def landkarte(netz, st, M):
    """Die Landkarte des Reichs an der Rückwand (örtlich +y in den Raum): Pergament in einem Rahmen, Küste und Meer oben, Flüsse,
    Gebirge, Wälder, Städte als rote Punkte, die Grenze des Reichs gestrichelt, eine Windrose."""
    b, h, z0 = 2.0, 1.3, 1.2
    si.q(netz, st, M, (0, 0.02, z0 + h / 2), (b + 0.12, 0.04, h + 0.12), "Taeferholz dunkel", ohne=("-y",))
    P = M @ Matrix.Translation((0, 0.042, z0 + h / 2)) @ Matrix.Rotation(math.radians(-90), 4, "X")   # Ebene x–y, +z in den Raum
    netz.vieleck([P @ Vector((x, y, 0)) for x, y in ((-b / 2, -h / 2), (b / 2, -h / 2), (b / 2, h / 2), (-b / 2, h / 2))], st["Kartengrund"])
    nz = P.to_3x3() @ Vector((0, 0, 1))

    def linie(pkt, breite, stoff, z=0.002):
        for a, e in zip(pkt, pkt[1:]):
            netz.stab(P @ Vector((a[0], a[1], z)), P @ Vector((e[0], e[1], z)), breite, 0.001, st[stoff], quer=nz)
    kueste = [(-1.0, 0.42), (-0.7, 0.5), (-0.45, 0.4), (-0.2, 0.52), (0.1, 0.46), (0.35, 0.56), (0.6, 0.5), (1.0, 0.55)]
    netz.vieleck([P @ Vector((x, y, 0.0015)) for x, y in kueste + [(1.0, 0.65), (-1.0, 0.65)]][::-1], st["Kartenfluss"])
    zufall = random.Random(17)
    for k in range(3):                                                                      # Flüsse von den Bergen zum Meer
        x = -0.6 + 0.55 * k
        pkt = [(x + 0.05 * math.sin(3 * t + k), -0.55 + 1.0 * t) for t in [j / 8 for j in range(9)]]
        pkt = [(px, py) for px, py in pkt if py < 0.45]
        linie(pkt, 0.012, "Kartenfluss")
    for k in range(9):                                                                      # Gebirge
        x, y = -0.85 + 0.2 * k, -0.48 + 0.06 * math.sin(k * 1.7)
        netz.vieleck([P @ Vector((x - 0.06, y, 0.0025)), P @ Vector((x + 0.06, y, 0.0025)), P @ Vector((x, y + 0.1, 0.0025))], st["Kartenberg"])
    for _ in range(26):                                                                     # Wälder
        x, y = zufall.uniform(-0.9, 0.9), zufall.uniform(-0.35, 0.3)
        netz.vieleck([P @ Vector((x + 0.02 * math.cos(2 * math.pi * t / 5), y + 0.02 * math.sin(2 * math.pi * t / 5), 0.0025)) for t in range(5)],
                     st["Kartenwald"])
    for _ in range(14):                                                                     # Städte
        x, y = zufall.uniform(-0.85, 0.85), zufall.uniform(-0.3, 0.38)
        netz.vieleck([P @ Vector((x + 0.018 * math.cos(2 * math.pi * t / 6), y + 0.018 * math.sin(2 * math.pi * t / 6), 0.003)) for t in range(6)],
                     st["Kartenrot"])
    grenze = [(0.9 * math.cos(w) * (1 + 0.08 * math.sin(5 * w)), 0.05 + 0.45 * math.sin(w) * (1 + 0.06 * math.cos(4 * w)))
              for w in [2 * math.pi * k / 40 for k in range(41)]]
    for k in range(0, 40, 2):
        linie(grenze[k:k + 2], 0.008, "Kartenrot", z=0.0028)
    c = (0.78, -0.42)                                                                       # Windrose
    for k in range(8):
        w = math.pi / 4 * k
        r = 0.09 if k % 2 == 0 else 0.05
        netz.vieleck([P @ Vector((c[0], c[1], 0.003)), P @ Vector((c[0] + r * math.cos(w - 0.25), c[1] + r * math.sin(w - 0.25), 0.003)),
                      P @ Vector((c[0] + r * math.cos(w), c[1] + r * math.sin(w), 0.003))], st["Kartenrot" if k % 2 else "Tinte"])


def kontor_einrichtung(netz, st, anker):
    """Der Schreibtisch mit grünem Tuch (Rechenbrett, Siegel und Wachs, Tintenfass, Briefe, ein Leuchter) und dem Lehnstuhl,
    die Landkarte an der Rückwand, ein Aktenregal an der Vorderwand, eine Truhe unter dem Fenster, eine Laterne."""
    landkarte(netz, st, si.rahmen(-8.75, KY1, 180))
    m = Vector((-8.75, 1.6, 0))
    wi.tisch(netz, st, m, 1.8, 0.85)
    netz.quader(m + Vector((0, 0, 0.783)), (1.7, 0.75, 0.006), st["Tuch"])
    oben = 0.786
    rechenbrett(netz, st, Matrix.Translation(m + Vector((-0.45, -0.1, oben))) @ Matrix.Rotation(math.pi, 4, "Z"))
    bs.achteck(netz, st, m + Vector((0.35, 0.0, oben)), 0.022, 0.0, 0.08, "Messing", n=6)                  # Siegel
    for k in range(3):
        bs.achteck(netz, st, m + Vector((0.45 + 0.04 * k, 0.12, oben)), 0.012, 0.0, 0.06, "Siegelwachs", n=6)
    for k in range(3):
        netz.quader(m + Vector((0.15 + 0.06 * k, -0.15 + 0.02 * k, oben + 0.001 + 0.002 * k)), (0.2, 0.26, 0.002), st["Papier"],
                    Matrix.Rotation(0.2 * (k - 1), 4, "Z"))
    bs.achteck(netz, st, m + Vector((0.7, 0.25, oben)), 0.025, 0.0, 0.05, "Kohle", n=6)
    netz.stab(m + Vector((0.7, 0.25, oben + 0.04)), m + Vector((0.75, 0.3, oben + 0.2)), 0.012, 0.03, st["Federweiss"])
    pr.leuchter(netz, st, m + Vector((-0.75, 0.25, oben)), h=0.22)
    anker.append(licht(m + Vector((-0.75, 0.25, oben + 0.7)), (0, 0, 1), "Glut", 2))
    s = m + Vector((0, 0.65, 0))                                                            # Lehnstuhl
    netz.quader(s + Vector((0, 0, SITZ - 0.03)), (0.52, 0.5, 0.06), st["Thronholz"])
    netz.quader(s + Vector((0, 0, SITZ + 0.005)), (0.44, 0.42, 0.02), st["Tuch"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader(s + Vector((sx * 0.23, sy * 0.21, (SITZ - 0.06) / 2)), (0.06, 0.06, SITZ - 0.06), st["Thronholz"])
    netz.quader(s + Vector((0, 0.23, SITZ + 0.5)), (0.52, 0.06, 1.0), st["Thronholz"])
    pr.buecherregal(netz, st, si.rahmen(-9.4, KY0, 0), breite=1.6, hoehe=2.2, saat=7)
    pr.truhe(netz, st, si.rahmen(KX0, 2.6, -90), eisen=True)
    p = Vector((-8.75, 0.3, 2.3))
    ti.laterne(netz, st, p, H_KONTOR - ia.BALKEN_H)
    anker.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe", 2))


def werkzeug_kontor(netz, st):
    """Gekauft (Werkzeug2, ab Stufe 2): die Hauptbücher – ein Bord an der Vorderwand mit sieben dicken Folianten mit
    Messingbeschlägen, davor auf einem Lesepult das aufgeschlagene Hauptbuch des Jahres."""
    M = si.rahmen(-7.5, KY0, 0)
    si.q(netz, st, M, (0, 0.2, 1.4), (1.3, 0.36, 0.04), "Taeferholz dunkel")
    for x in (-0.55, 0.55):
        netz.stab(M @ Vector((x, 0.02, 1.15)), M @ Vector((x, 0.35, 1.38)), 0.04, 0.04, st["Taeferholz dunkel"])
    for k in range(7):
        x = -0.55 + 0.17 * k
        farbe = ("Buchrot", "Leder", "Buchbraun", "Buchgruen", "Leder", "Buchrot", "Buchbraun")[k]
        si.q(netz, st, M, (x, 0.2, 1.42 + 0.19), (0.12, 0.32, 0.38), farbe)
        for z in (1.47, 1.76):
            si.q(netz, st, M, (x, 0.365, z), (0.124, 0.004, 0.03), "Messing")
    f = M @ Vector((0.0, 0.85, 0))
    drehkoerper(netz, st, f, [(0.0, 0.2), (0.05, 0.16), (0.1, 0.05), (1.0, 0.045)], "Taeferholz dunkel", n=8)
    platte = Matrix.Translation(f + Vector((0, -0.02, 1.07))) @ Matrix.Rotation(math.radians(-28), 4, "X")
    netz.quader((0, 0, 0), (0.6, 0.45, 0.03), st["Taeferholz"], platte)
    hauptbuch(netz, st, platte @ Matrix.Translation((0, 0, 0.015)), b=0.5, t=0.36, farbe="Buchgruen")


# ─────────────────────────────────────────────────────────────────────────────
#  Empfang
# ─────────────────────────────────────────────────────────────────────────────

def empfang_huelle(netz, decke, st, anker):
    """Gekalkte Wände mit hoher Täfelung (1,6 m), darüber ein gemalter Fries, zwei Rundbogenfenster mit Butzenscheiben vorn,
    Kassettendecke, Dielen mit einem großen Teppich."""
    oe = {"links": [Oe(EMPF_Y, 0.0, EMPF_B, EMPF_H)], "hinten": [Oe(TRESOR_X, 0.0, TUER_B, TUER_H)],
          "vorne": [Oe(8.0, 0.9, 1.0, 1.9), Oe(11.0, 0.9, 1.0, 1.9)], "rechts": []}
    waende = putzraum(netz, st, EX0, EX1, EY0, EY1, H_EMPFANG, oe, ohne_aussen=("links", "hinten"), ohne_laibung=("links",),
                      enden={"links": (False, False), "vorne": (False, True)}, eigene_laibung=("vorne",))
    for o in oe["vorne"]:
        rundbogenfenster(netz, st, anker, waende["vorne"], o, stufe=3, butzen=True)
    kamin_frei = (pr.u_von(waende["rechts"], KAMIN_Y) - 1.15, pr.u_von(waende["rechts"], KAMIN_Y) + 1.15)
    for seite, w in waende.items():
        pi.taefelung(netz, st, *ecken(EX0, EX1, EY0, EY1)[seite], oe[seite], frei=tuer_frei(w) + ([kamin_frei] if seite == "rechts" else []),
                     hoehe=1.6)
    frei = {"rechts": [kamin_frei]}
    for seite, w in waende.items():
        for v0, v1, dw, stoff in ((H_EMPFANG - 0.5, H_EMPFANG - 0.47, 0.004, "Ocker"), (H_EMPFANG - 0.47, H_EMPFANG - 0.2, 0.003, "Deckenblau"),
                                  (H_EMPFANG - 0.2, H_EMPFANG - 0.17, 0.004, "Ocker")):
            for u0, u1 in pi.freie_stuecke(w, v0, v1, rand=0.04):
                netz.vieleck_nach([w.p(u0, v0, dw), w.p(u1, v0, dw), w.p(u1, v1, dw), w.p(u0, v1, dw)], w.n, st[stoff])
        for u0, u1 in pi.freie_stuecke(w, H_EMPFANG - 0.47, H_EMPFANG - 0.2, rand=0.04):
            n = max(1, round((u1 - u0) / 0.45))
            for k in range(n):
                um = u0 + (u1 - u0) * (k + 0.5) / n
                vm = H_EMPFANG - 0.335
                netz.vieleck_nach([w.p(um + 0.08 * math.cos(2 * math.pi * t / 8), vm + 0.08 * math.sin(2 * math.pi * t / 8), 0.005) for t in range(8)],
                                  w.n, st["Ocker"])
                netz.vieleck_nach([w.p(um + 0.03 * math.cos(2 * math.pi * t / 6), vm + 0.03 * math.sin(2 * math.pi * t / 6), 0.006) for t in range(6)],
                                  w.n, st["Deckenrot"])
        _ = frei
    kassettendecke(decke, st, EX0, EX1, EY0, EY1, H_EMPFANG)
    wi.dielen(netz, st, EX0, EX1, EY0, EY1, 0.0, saat=37)
    teppich(netz, st, EMPF_TISCH + Vector((0, -0.2, 0)), 3.6, 3.0)
    return waende


def teppich(netz, st, mitte, lx, ly):
    """Großer Teppich: roter Rand mit goldenen Linien, blaues Feld, ein Medaillon in der Mitte, Fransen an den Schmalseiten."""
    m = Vector(mitte)
    for (bx, by, z, stoff) in ((lx, ly, 0.004, "Teppich rot"), (lx - 0.36, ly - 0.36, 0.007, "Teppich gold"),
                               (lx - 0.42, ly - 0.42, 0.01, "Teppich blau")):
        netz.vieleck_nach([m + Vector((sx * bx / 2, sy * by / 2, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))], (0, 0, 1), st[stoff])
    netz.quader(m + Vector((0, 0, 0.002)), (lx, ly, 0.004), st["Teppich rot"], ohne=("+z", "-z"))           # Kante
    for k in range(8):                                                                      # Medaillon
        w0, w1 = 2 * math.pi * k / 8, 2 * math.pi * (k + 1) / 8
        netz.vieleck_nach([m + Vector((0, 0, 0.013)), m + Vector((0.55 * math.cos(w0), 0.45 * math.sin(w0), 0.013)),
                           m + Vector((0.55 * math.cos(w1), 0.45 * math.sin(w1), 0.013))], (0, 0, 1), st["Teppich rot"])
    netz.vieleck_nach([m + Vector((0.25 * math.cos(2 * math.pi * k / 8), 0.2 * math.sin(2 * math.pi * k / 8), 0.016)) for k in range(8)],
                      (0, 0, 1), st["Teppich gold"])
    for sx in (-1, 1):                                                                      # Fransen
        for k in range(20):
            y = -ly / 2 + 0.05 + (ly - 0.1) * k / 19
            netz.vieleck_nach([m + Vector((sx * lx / 2 + sx * d, y + e, 0.003)) for d, e in ((0.0, -0.01), (0.08, -0.01), (0.08, 0.01), (0.0, 0.01))],
                              (0, 0, 1), st["Teppich gold"])


def prunkkamin(netz, flammen, st, anker):
    """Der Kamin an der rechten Wand des Empfangs: Herd und Wangen aus Sandstein mit Säulchen, ein Sims mit Profil, darüber der
    Rauchfang mit einem gemalten Wappen (die Münzen der Bank) bis an die Decke; im Feuerraum Scheite auf eisernen Böcken und die
    Flammen („Glut“, eigenes Netz, Animation „Feuer“); daneben Feuerbesteck."""
    M = si.rahmen(EX1, KAMIN_Y, 90)
    b, t = 1.9, 0.7
    si.q(netz, st, M, (0, t / 2 + 0.1, 0.06), (b + 0.3, t + 0.2, 0.12), "Sandstein")              # Herdplatte
    for s in (-1, 1):
        si.q(netz, st, M, (s * (b / 2 - 0.15), t / 2, 0.12 + 0.6), (0.3, t, 1.2), "Sandstein", ohne=("-y",))
        drehkoerper(netz, st, M @ Vector((s * (b / 2 - 0.15), t + 0.05, 0.12)), [(0.0, 0.07), (0.06, 0.06), (0.08, 0.045), (1.05, 0.04),
                                                                               (1.1, 0.06), (1.2, 0.07)], "Sandstein", n=8)
    si.q(netz, st, M, (0, t / 2 + 0.03, 1.32 + 0.12), (b, t + 0.06, 0.24), "Sandstein")             # Sturz
    si.q(netz, st, M, (0, t / 2 + 0.08, 1.62), (b + 0.2, t + 0.16, 0.08), "Sandstein")              # Sims
    si.q(netz, st, M, (0, t / 2 + 0.1, 1.68), (b + 0.28, t + 0.2, 0.04), "Sandstein")
    si.q(netz, st, M, (0, 0.01, 0.12 + 0.6), (b - 0.6, 0.02, 1.2), "Russ", ohne=("-y",))           # verrußte Rückwand
    alle, netz.alle = netz.alle, True                                                       # verrußte Unterseite des Sturzes
    netz.vieleck_nach([M @ Vector((x, y, 1.317)) for x, y in ((-(b - 0.6) / 2, 0.02), ((b - 0.6) / 2, 0.02), ((b - 0.6) / 2, t),
                                                              (-(b - 0.6) / 2, t))], (0, 0, -1), st["Russ"])
    netz.alle = alle
    oben = H_EMPFANG                                                                         # Rauchfang
    si.q(netz, st, M, (0, (t - 0.1) / 2, (1.7 + oben) / 2), (b - 0.3, t - 0.1, oben - 1.7), "Kalkputz", ohne=("-y",))
    schild = M @ Matrix.Translation((0, t - 0.1, 0))
    di.flach(netz, st, [(x * 1.0, 2.55 + z * 1.0) for x, z in bs.SCHILD], schild @ Matrix.Translation((0, 0.01, 0)), 0.012, "Deckenblau")
    for x, z in ((-0.12, 2.73), (0.12, 2.73), (0.0, 2.45)):
        netz.quader((0, 0, 0), (1, 1, 1), st["Gold"], schild @ Matrix.Translation((x, 0.02, z)) @ Matrix.Diagonal((0.13, 0.01, 0.13, 1)))
    feuer = M @ Vector((0, 0.32, 0.12))
    scheibe(netz, st, feuer, 0.4, feuer.z + 0.005, "Glut", n=9)
    vor = M.to_3x3() @ Vector((0, 1, 0))
    quer = M.to_3x3() @ Vector((1, 0, 0))
    for s in (-1, 1):                                                                       # Feuerböcke
        netz.stab(feuer + quer * s * 0.3, feuer + quer * s * 0.3 + Vector((0, 0, 0.32)), 0.03, 0.03, st["Eisen"])
        netz.stab(feuer + quer * s * 0.3 + Vector((0, 0, 0.32)), feuer + quer * s * 0.3 - vor * 0.25 + Vector((0, 0, 0.12)), 0.025, 0.025, st["Eisen"])
    for i, w in enumerate((0.3, 1.2, 2.2, 2.9)):                                            # Scheite über Kreuz
        d = quer * math.cos(w) + vor * math.sin(w) * 0.6
        netz.stab(feuer + d * 0.3 + Vector((0, 0, 0.07 + 0.04 * i)), feuer - d * 0.3 + Vector((0, 0, 0.1 + 0.04 * i)), 0.09, 0.09, st["Scheitholz"])
    zufall = random.Random(5)
    for k in range(6):                                                                      # Flammen (bewegt)
        p = feuer + quer * zufall.uniform(-0.28, 0.28) + vor * zufall.uniform(-0.1, 0.1) + Vector((0, 0, 0.08))
        drehkoerper(flammen, st, p, [(0.0, 0.08), (0.14, 0.065), (0.34 + 0.14 * zufall.random(), 0.0)], "Glut", n=5)
    anker.append(licht(feuer + vor * 0.45 + Vector((0, 0, 0.4)), (vor.x, vor.y, 0), "Glut", 3))
    besteck = M @ Vector((b / 2 + 0.25, 0.3, 0))                                            # Feuerbesteck
    drehkoerper(netz, st, besteck, [(0.0, 0.08), (0.03, 0.06), (0.05, 0.015), (0.85, 0.012), (0.9, 0.04)], "Messing", n=6)
    for k in range(3):
        a = 2 * math.pi * k / 3
        netz.stab(besteck + Vector((0.05 * math.cos(a), 0.05 * math.sin(a), 0.85)), besteck + Vector((0.07 * math.cos(a), 0.07 * math.sin(a), 0.05)),
                  0.018, 0.018, st["Eisen"])
    return am_ort(flammen, feuer)


def wandteppich(netz, st, wand, u0, u1, v0, v1):
    """Wandteppich an der Wand: Stange, rote Bordüre mit Goldlinien, grünes Feld mit Streublumen, in der Mitte ein Baum mit zwei
    Hirschen zu beiden Seiten, unten Fransen."""
    netz.stab(wand.p(u0 - 0.1, v1 + 0.06, 0.06), wand.p(u1 + 0.1, v1 + 0.06, 0.06), 0.04, 0.04, st["Taeferholz dunkel"], quer=wand.n)
    for u in (u0 - 0.05, u1 + 0.05):
        netz.stab(wand.p(u, v1 + 0.06, 0.0), wand.p(u, v1 + 0.06, 0.08), 0.03, 0.03, st["Eisen"], quer=(0, 0, 1))

    def flaeche(a0, a1, b0, b1, w, stoff):
        netz.vieleck_nach([wand.p(a0, b0, w), wand.p(a1, b0, w), wand.p(a1, b1, w), wand.p(a0, b1, w)], wand.n, st[stoff])
    flaeche(u0, u1, v0, v1, 0.04, "Teppich rot")
    flaeche(u0 + 0.12, u1 - 0.12, v0 + 0.12, v1 - 0.12, 0.042, "Deckengruen")
    for a in (0.06, 0.11):
        for b0, b1, c0, c1 in ((u0 + a, u1 - a, v0 + a, v0 + a + 0.012), (u0 + a, u1 - a, v1 - a - 0.012, v1 - a),
                               (u0 + a, u0 + a + 0.012, v0 + a, v1 - a), (u1 - a - 0.012, u1 - a, v0 + a, v1 - a)):
            flaeche(b0, b1, c0, c1, 0.043, "Teppich gold")
    um = (u0 + u1) / 2
    zufall = random.Random(23)
    for _ in range(80):                                                                     # Streublumen
        u, v = zufall.uniform(u0 + 0.17, u1 - 0.17), zufall.uniform(v0 + 0.17, v1 - 0.17)
        if abs(u - um) < 0.7 and v > v0 + 0.25:
            continue
        r = zufall.uniform(0.014, 0.024)
        netz.vieleck_nach([wand.p(u + r * math.cos(2 * math.pi * t / 5), v + r * math.sin(2 * math.pi * t / 5), 0.045) for t in range(5)],
                          wand.n, st[zufall.choice(("Rosenbluete", "Jasmin", "Teppich gold", "Stoff blau"))])
    flaeche(um - 0.03, um + 0.03, v0 + 0.2, v0 + 1.05, 0.0435, "Holz")                      # Baum
    for t in range(14):
        a = 2 * math.pi * t / 14
        netz.vieleck_nach([wand.p(um, v0 + 1.25, 0.0445), wand.p(um + 0.32 * math.cos(a), v0 + 1.25 + 0.3 * math.sin(a), 0.0445),
                           wand.p(um + 0.32 * math.cos(a + math.pi / 7), v0 + 1.25 + 0.3 * math.sin(a + math.pi / 7), 0.0445)], wand.n, st["Laub"])
    for s in (-1, 1):                                                                       # zwei Hirsche
        c = (um + s * 0.48, v0 + 0.5)
        umriss = [(-0.18, 0.0), (0.16, 0.0), (0.2, 0.12), (0.26, 0.28), (0.32, 0.3), (0.3, 0.2), (0.22, 0.1), (0.18, 0.18), (-0.18, 0.18),
                  (-0.24, 0.24)]
        netz.vieleck_nach([wand.p(c[0] - s * x, c[1] + y, 0.046) for x, y in umriss], wand.n, st["Kartenberg"])
        for x in (-0.15, -0.1, 0.1, 0.15):
            flaeche(min(c[0] - s * x - 0.012, c[0] - s * x + 0.012), max(c[0] - s * x - 0.012, c[0] - s * x + 0.012), c[1] - 0.22, c[1], 0.046,
                    "Kartenberg")
        for d in ((0.27, 0.3, 0.2, 0.46), (0.31, 0.3, 0.38, 0.44)):                        # Geweih
            netz.stab(wand.p(c[0] - s * d[0], c[1] + d[1], 0.047), wand.p(c[0] - s * d[2], c[1] + d[3], 0.047), 0.012, 0.002, st["Ocker"], quer=wand.n)
    for k in range(int((u1 - u0) / 0.05)):                                                  # Fransen
        u = u0 + 0.025 + 0.05 * k
        flaeche(u - 0.008, u + 0.008, v0 - 0.07, v0, 0.04, "Teppich gold")


def sessel(netz, st, fuss, blick):
    """Gepolsterter Lehnsessel (Kunde_Vornehm): Gestell aus dunkler Eiche mit gedrechselten Füßen, Polster (Sitz, Lehne,
    Armlehnen) in Weinrot, Ziernägel aus Messing."""
    f = Vector(fuss)
    vor = Vector((blick[0], blick[1], 0)).normalized()
    rechts = Vector((vor.y, -vor.x, 0))
    M = Matrix((rechts, vor, Vector((0, 0, 1)))).transposed().to_4x4()                  # x nach rechts (rechtshändig)
    M.translation = f
    for sx in (-1, 1):
        for sy in (-1, 1):
            drehkoerper(netz, st, M @ Vector((sx * 0.25, sy * 0.22 - 0.03, 0)), [(0.0, 0.03), (0.08, 0.035), (0.12, 0.022), (SESSEL - 0.1, 0.025)],
                        "Thronholz", n=6)
    netz.quader((0, -0.03, SESSEL - 0.1), (0.58, 0.52, 0.06), st["Thronholz"], M)
    netz.quader((0, -0.01, SESSEL - 0.035), (0.5, 0.46, 0.07), st["Polster"], M)
    netz.quader((0, -0.27, SESSEL + 0.4), (0.58, 0.07, 0.9), st["Thronholz"], M)              # Lehne
    netz.quader((0, -0.225, SESSEL + 0.35), (0.48, 0.03, 0.66), st["Polster"], M)
    netz.quader((0, -0.27, SESSEL + 0.88), (0.64, 0.09, 0.07), st["Thronholz"], M)
    for sx in (-1, 1):                                                                      # Armlehnen
        netz.quader((sx * 0.28, -0.03, SESSEL + 0.2), (0.07, 0.48, 0.05), st["Thronholz"], M)
        netz.quader((sx * 0.28, 0.2, SESSEL + 0.07), (0.05, 0.05, 0.2), st["Thronholz"], M)
        netz.quader((sx * 0.28, -0.03, SESSEL + 0.235), (0.06, 0.38, 0.02), st["Polster"], M)
        for k in range(4):
            netz.quader((sx * 0.252, -0.2 + 0.12 * k, SESSEL - 0.01), (0.008, 0.015, 0.015), st["Messing"], M)


def empfang_einrichtung(netz, st, anker, kunden):
    """Der Tisch mit Weinkanne, Pokalen und einer Schale Konfekt, drei Lehnsessel darum (Kunde_Vornehm_1–3), der Wandteppich an
    der Rückwand, zwei Standleuchter, eine Kredenz mit Silbergeschirr an der linken Wand."""
    m = EMPF_TISCH
    netz.quader(m + Vector((0, 0, 0.68)), (0.9, 0.7, 0.05), st["Thronholz"])
    netz.quader(m + Vector((0, 0, 0.709)), (0.82, 0.62, 0.008), st["Damast"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            drehkoerper(netz, st, m + Vector((sx * 0.36, sy * 0.26, 0)), [(0.0, 0.035), (0.1, 0.03), (0.3, 0.045), (0.5, 0.025), (0.655, 0.03)],
                        "Thronholz", n=6)
    oben = 0.713
    kanne = m + Vector((0.1, 0.1, oben))                                                    # Weinkanne aus Zinn
    drehkoerper(netz, st, kanne, [(0.0, 0.06), (0.02, 0.07), (0.12, 0.075), (0.2, 0.05), (0.26, 0.035), (0.29, 0.042)], "Zinn", n=10)
    netz.stab(kanne + Vector((-0.06, 0, 0.25)), kanne + Vector((-0.1, 0, 0.1)), 0.02, 0.012, st["Zinn"])
    netz.stab(kanne + Vector((0.04, 0, 0.27)), kanne + Vector((0.09, 0, 0.31)), 0.02, 0.012, st["Zinn"])
    for k, (dx, dy) in enumerate(((-0.25, -0.1), (0.3, -0.15), (-0.05, -0.22))):            # Pokale aus Silber
        p = m + Vector((dx, dy, oben))
        drehkoerper(netz, st, p, [(0.0, 0.035), (0.008, 0.03), (0.02, 0.008), (0.08, 0.007), (0.09, 0.03), (0.16, 0.04)], "Silber", n=8)
        scheibe(netz, st, p, 0.034, p.z + 0.14, "Weinrot", n=8)
    muenzschale(netz, st, m + Vector((-0.22, 0.17, oben)), r=0.1, voll="Rose")             # Konfekt
    for (punkt, blick) in SESSEL_PLAETZE:
        sessel(netz, st, (punkt[0], punkt[1], 0), blick)
        kunde(kunden, "Vornehm", punkt, blick, "Sitzen", sitz=SESSEL, stufe=3)
    w = ia.Wand(*ecken(EX0, EX1, EY0, EY1)["hinten"])
    u = pr.u_von(w, 8.6)
    wandteppich(netz, st, w, u - 1.0, u + 1.0, 1.65, 3.15)
    for p in ((7.15, 1.4), (11.85, -3.85)):
        stehleuchter(netz, st, anker, (p[0], p[1], 0), stufe=3)
    M = si.rahmen(EX0, 0.6, -90)                                                            # Kredenz
    si.q(netz, st, M, (0, 0.25, 0.45), (1.6, 0.5, 0.9), "Thronholz", ohne=("-y",))
    for x in (-0.4, 0.4):
        si.q(netz, st, M, (x, 0.505, 0.45), (0.7, 0.012, 0.7), "Taeferholz")
        si.q(netz, st, M, (x + (0.25 if x < 0 else -0.25), 0.515, 0.5), (0.03, 0.01, 0.08), "Messing")
    si.q(netz, st, M, (0, 0.25, 0.92), (1.66, 0.54, 0.04), "Thronholz")
    si.q(netz, st, M, (0, 0.04, 1.4), (1.6, 0.08, 0.96), "Taeferholz", ohne=("-y",))          # Rückwand mit Borden
    for z in (1.35, 1.7):
        si.q(netz, st, M, (0, 0.15, z), (1.6, 0.22, 0.03), "Thronholz")
    for k in range(4):                                                                      # Silberteller hochkant
        for z in (1.365, 1.715):
            T = M @ Matrix.Translation((-0.6 + 0.4 * k, 0.12, z + 0.13)) @ Matrix.Rotation(math.radians(-12), 4, "X")
            netz.prisma([(0.12 * math.cos(2 * math.pi * j / 12), 0.12 * math.sin(2 * math.pi * j / 12)) for j in range(12)], -0.006, 0.006,
                        st["Silber"], matrix=T @ Matrix.Rotation(math.radians(90), 4, "Y") @ Matrix.Rotation(math.radians(90), 4, "X"))
    for k in range(3):
        drehkoerper(netz, st, M @ Vector((-0.5 + 0.5 * k, 0.3, 0.94)), [(0.0, 0.05), (0.02, 0.04), (0.12, 0.06), (0.2, 0.04), (0.24, 0.05)],
                    "Silber", n=8)


def eiserne_tuer(netz, st, wand, s, b, h, innen=False):
    """Die eiserne Tür des Tresorturms in der Wand (u bei der Weltkoordinate s): eine Platte mit Bändern und Nieten, ein großes
    Schloss mit Schlüsselschild und ein Ring; `innen`: die Innenseite mit dem Riegelwerk (drei Riegel in Krampen)."""
    u = pr.u_von(wand, s)
    M = Matrix.Translation(wand.p(u, 0.0, 0.0)) @ Matrix((wand.d, wand.n, Vector((0, 0, 1)))).transposed().to_4x4()
    rueck = 0.08
    si.q(netz, st, M, (0, -rueck - 0.03, h / 2), (b - 0.02, 0.06, h - 0.01), "Eisen", ohne=("-y",))
    for z in (0.15, h * 0.5, h - 0.15):
        si.q(netz, st, M, (0, -rueck + 0.008, z), (b - 0.06, 0.016, 0.1), "Kohle", ohne=("-y",))
    for k in range(7):
        for j in range(4):
            si.q(netz, st, M, (-b / 2 + 0.1 + (b - 0.2) * j / 3, -rueck + 0.024, 0.1 + (h - 0.2) * k / 6), (0.035, 0.014, 0.035), "Eisen", ohne=("-y",))
    if not innen:
        si.q(netz, st, M, (b * 0.25, -rueck + 0.03, h * 0.5 + 0.1), (0.16, 0.03, 0.24), "Messing", ohne=("-y",))
        si.q(netz, st, M, (b * 0.25, -rueck + 0.047, h * 0.5 + 0.08), (0.03, 0.006, 0.08), "Kohle", ohne=("-y",))
        netz.stab(M @ Vector((-b * 0.2, -rueck + 0.05, h * 0.5 - 0.05)), M @ Vector((-b * 0.2, -rueck + 0.05, h * 0.5 - 0.2)), 0.12, 0.025,
                  st["Eisen"], quer=wand.n)
    else:
        for z in (0.4, h * 0.5, h - 0.4):
            si.q(netz, st, M, (0.05, -rueck + 0.05, z), (b + 0.3, 0.04, 0.06), "Eisen", ohne=("-y",))
            for x in (-b / 2 - 0.1, b / 2 + 0.1):
                si.q(netz, st, M, (x, 0.04, z), (0.06, 0.08, 0.12), "Kohle", ohne=("-y",))


# ─────────────────────────────────────────────────────────────────────────────
#  Tresorturm
# ─────────────────────────────────────────────────────────────────────────────

def tresor_huelle(netz, decke, st, anker):
    """Kreuzgratgewölbe, Wände aus Quadern (Sandstein mit Fugen gemalt), ein schmaler Lichtschlitz mit Gitter hoch in der rechten
    Wand, Steinplatten."""
    w = {s: pr.wand(*ecken(TX0, TX1, TY0, TY1)[s]) for s in ("vorne", "rechts")}
    loecher = {"vorne": [loch(w["vorne"], TRESOR_X, 0.0, TUER_B, TUER_H)], "rechts": [loch(w["rechts"], 4.8, 2.0, 0.22, 0.8)]}
    waende = gewoelberaum(netz, decke, st, TX0, TX1, TY0, TY1, 1, T_KAEMPFER, T_STICH, loecher, stoff="Haustein", sockel=0.5, saat=19)
    for seite, wand in waende.items():                                                     # Quaderfugen
        for k in range(1, 6):
            v = 0.5 + 0.4 * k
            if v > T_KAEMPFER:
                break
            for u0, u1 in pi.freie_stuecke(ia.Wand(wand.a.to_2d(), (wand.a + wand.d * wand.laenge).to_2d(),
                                                   [Oe(pr.welt_s(wand, (l[0] + l[1]) / 2), l[2], l[1] - l[0], l[3] - l[2])
                                                    for l in loecher.get(seite, [])]), v - 0.01, v + 0.01, rand=0.02):
                netz.vieleck_nach([wand.p(u0, v - 0.006, 0.002), wand.p(u1, v - 0.006, 0.002), wand.p(u1, v + 0.006, 0.002),
                                   wand.p(u0, v + 0.006, 0.002)], wand.n, st["Fuge"])
    pr.steinplatten(netz, st, TX0, TX1, TY0, TY1, saat=19, breite=0.55, stoffe=("Platte dunkel", "Platte", "Platte warm"))
    wr = waende["rechts"]
    u0, u1, v0, v1 = loecher["rechts"][0]
    pr.laibung(netz, st, wr, u0, u1, v0, v1, 0.4, stoff="Haustein", unten=True)
    netz.vieleck_nach([wr.p(u0, v0, -0.4), wr.p(u1, v0, -0.4), wr.p(u1, v1, -0.4), wr.p(u0, v1, -0.4)], wr.n, st["Fenster"])
    netz.stab(wr.p((u0 + u1) / 2, v0, -0.12), wr.p((u0 + u1) / 2, v1, -0.12), 0.02, 0.02, st["Eisen"])
    anker.append(licht(wr.p((u0 + u1) / 2, (v0 + v1) / 2, 0.1), (wr.n.x, wr.n.y, 0), "Fenster", 3))


def tresor_einrichtung(netz, st, anker):
    """Zwei Kassenschränke an der Rückwand, gestapelte Truhen mit Eisenbändern an der linken Wand, ein Regal mit Barren und
    versiegelten Säcken rechts, eine Laterne."""
    for x in (9.5, 11.6):
        M = si.rahmen(x, TY1, 180)
        b, h, t = 0.9, 1.5, 0.6
        si.q(netz, st, M, (0, t / 2, 0.08 + h / 2), (b, t, h), "Eisen", ohne=("-y",))
        for xx in (-b / 2 + 0.06, b / 2 - 0.06):
            si.q(netz, st, M, (xx, t / 2, 0.04), (0.08, t - 0.04, 0.08), "Eisen")
        for z in (0.3, 0.08 + h / 2, h - 0.1):
            si.q(netz, st, M, (0, t + 0.008, z), (b - 0.04, 0.016, 0.06), "Kohle")
        si.q(netz, st, M, (0, t + 0.008, 0.08 + h / 2), (0.02, 0.016, h - 0.06), "Kohle")
        for s in (-1, 1):
            si.q(netz, st, M, (s * 0.15, t + 0.02, 0.95), (0.08, 0.02, 0.12), "Messing")
            netz.stab(M @ Vector((s * 0.08, t + 0.04, 0.7)), M @ Vector((s * 0.08, t + 0.04, 0.5)), 0.03, 0.03, st["Eisen"])
    for k, (y, z) in enumerate(((3.4, 0.0), (4.75, 0.0), (3.4, 0.68), (4.75, 0.68))):
        pr.truhe(netz, st, si.rahmen(TX0, y, -90) @ Matrix.Translation((0, 0, z)), eisen=True)
    M = si.rahmen(TX1, 3.6, 90)
    for x in (-0.6, 0.6):
        si.q(netz, st, M, (x, 0.22, 0.75), (0.05, 0.44, 1.5), "Holz", ohne=("-y",))
    for z in (0.3, 0.8, 1.3):
        si.q(netz, st, M, (0, 0.22, z), (1.2, 0.44, 0.04), "Holz", ohne=("-y",))
    barren(netz, st, M, (0, 0.22, 0.32), 1.0, 0.36, lagen=2)
    barren(netz, st, M, (0, 0.22, 0.82), 1.0, 0.36, lagen=3)
    for k in range(4):
        di.beutel(netz, st, M @ Vector((-0.42 + 0.28 * k, 0.22, 1.32)), r=0.09, h=0.16)
        bs.achteck(netz, st, M @ Vector((-0.42 + 0.28 * k, 0.315, 1.4)), 0.018, 0.0, 0.008, "Siegelwachs", n=6)
    p = Vector((10.5, 4.5, 2.6))
    ti.laterne(netz, st, p, T_KAEMPFER + T_STICH)
    anker.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe", 3))


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen
# ─────────────────────────────────────────────────────────────────────────────

def bauen():
    import bpy
    bs.neue_szene()
    ia.RUSS_QUELLEN.clear()
    ti._lichter[0] = 0
    st = bs.Stoffe()
    raumnetze = {r: bs.Netz(f"Raum_{r}", alle=r != "Werk") for r in ("Werk", "Lager", "Pfand", "Kontor", "Empfang", "Tresor")}
    decken = {r: bs.Netz(f"Decke_{r}", alle=True) for r in raumnetze}
    werk = raumnetze["Werk"]
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

    plaetze, kunden = [], []      # Schreiber_<n>, Kunde_<Gruppe>_<n>
    # ── Wechselstube ──────────────────────────────────────────────────────────
    liste = []
    waende = stube_huelle(werk, decken["Werk"], st, liste)
    wechseltisch(werk, st, plaetze, kunden, liste)
    bewegt = []
    waagen = waegetisch(werk, st, plaetze, liste, bewegt)
    fertig += [(o, "Werk") for o in bewegt]
    rechentisch(werk, st, plaetze, liste)
    schreibpulte(werk, st, plaetze, liste)
    hauptbuchpult(teil("Bedienung_Bank_Hauptbuch"), st)
    kundenbank(werk, st, kunden)
    wandschrank(teil("Bedienung_Einrichten_Wandschrank"), st)
    kassenschrank(teil("Einrichtung_Kassenschrank"), st)
    goldwaage(teil("Einrichtung_Goldwaage"), st)
    werkzeug_bank(teil("Einrichtung_Werkzeug1"), st)
    stube_kram(werk, st, liste)
    alle_setzen(liste)
    si.pult(teil("Bedienung_Personal_Buch"), st, si.rahmen(-0.6, WY0 + 0.05, 0))
    M_haus = si.rahmen(HAUSTUER_X, WY0, 0)
    si.bretttuer(teil("Holztuer"), st, M_haus, HAUSTUER_B, HAUSTUER_H)
    si.eisentuer(teil("Einrichtung_Eisentuer"), st, M_haus, HAUSTUER_B, HAUSTUER_H)
    ti.hoftor(teil("Bedienung_Karren_Hoftor"), st, si.rahmen(HOFTOR_X, WY0, 0), HOFTOR_B, HOFTOR_H)
    si.loeschfass(teil("Einrichtung_Loeschfass"), st, Vector((5.3, 3.85, 0)))
    si.bretttuer(teil("Tuer_Werk_Pfand"), st, si.rahmen(PFAND_X, WY1, 180), TUER_B, TUER_H, stoff="Taeferholz")
    tuer_lager = teil("Tuer_Werk_Lager")
    si.bretttuer(tuer_lager, st, si.rahmen(LAGER_X, WY1, 180), TUER_B, TUER_H, stoff="Taeferholz dunkel", ring=False)
    for z in (0.35, 1.1, 1.85):                                                             # Eisenbänder und Schloss
        si.q(tuer_lager, st, si.rahmen(LAGER_X, WY1, 180), (0, -0.04, z), (TUER_B - 0.06, 0.014, 0.08), "Eisen", ohne=("-y",))
    si.q(tuer_lager, st, si.rahmen(LAGER_X, WY1, 180), (0.3, -0.03, 1.05), (0.1, 0.03, 0.14), "Messing", ohne=("-y",))
    si.bretttuer(teil("Tuer_Werk_Kontor"), st, si.rahmen(WX0, KONTOR_Y, -90), TUER_B, TUER_H, stoff="Taeferholz")
    tuer_empf = teil("Tuer_Werk_Empfang")                                                   # Flügeltür
    for s in (-1, 1):
        si.bretttuer(tuer_empf, st, si.rahmen(WX1, EMPF_Y + s * EMPF_B / 4, 90), EMPF_B / 2, EMPF_H, stoff="Taeferholz", ring=False)
        si.q(tuer_empf, st, si.rahmen(WX1, EMPF_Y + s * 0.08, 90), (0, -0.02, 1.15), (0.04, 0.04, 0.16), "Messing", ohne=("-y",))
    _ = waende

    # ── Stufen (kumulativ) ────────────────────────────────────────────────────
    stufen = [bs.Netz(f"Stufe{k}") for k in (1, 2, 3)]
    liste = []
    stube_stufen(stufen, st, liste)
    alle_setzen(liste)
    stufen_obj = [s.objekt() for s in stufen]

    # ── Tresorgewölbe ─────────────────────────────────────────────────────────
    liste = []
    lager_huelle(raumnetze["Lager"], decken["Lager"], st, liste)
    grosse_truhe(teil("Bedienung_Lager_Tresor", "Lager"), st)
    geldtruhen(raumnetze["Lager"], st)
    tl = teil("Tuer_Lager_Werk", "Lager")
    si.bretttuer(tl, st, si.rahmen(LAGER_X, LY0, 0), TUER_B, TUER_H, stoff="Taeferholz dunkel")
    p = Vector((3.6, 7.3, 2.0))
    ti.laterne(raumnetze["Lager"], st, p, L_KAEMPFER + L_STICH - 0.05)
    liste.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"))
    alle_setzen(liste, "Lager")
    nummer = 1
    for y in (6.3, 7.1, 7.9, 8.7):
        for x in (2.7, 4.5):
            setzen(bs.anker(f"Lager_{nummer}", (x, y, 0.0)), "Lager")
            nummer += 1

    # ── Pfandkammer ───────────────────────────────────────────────────────────
    liste = []
    pfand_huelle(raumnetze["Pfand"], decken["Pfand"], st, liste)
    pfand_einrichtung(raumnetze["Pfand"], st, liste)
    gitterschraenke(teil("Einrichtung_Pfandkammer", "Pfand"), st)
    si.bretttuer(teil("Tuer_Pfand_Werk", "Pfand"), st, si.rahmen(PFAND_X, PY0, 0), TUER_B, TUER_H, stoff="Taeferholz")
    alle_setzen(liste, "Pfand")

    # ── Kontor (ab Stufe 2) ───────────────────────────────────────────────────
    liste = []
    kontor_huelle(raumnetze["Kontor"], decken["Kontor"], st, liste)
    kontor_einrichtung(raumnetze["Kontor"], st, liste)
    werkzeug_kontor(teil("Einrichtung_Werkzeug2", "Kontor"), st)
    si.bretttuer(teil("Tuer_Kontor_Werk", "Kontor"), st, si.rahmen(KX1, KONTOR_Y, 90), TUER_B, TUER_H, stoff="Taeferholz")
    alle_setzen(liste, "Kontor", stufe=2)

    # ── Empfang (ab Stufe 3) ──────────────────────────────────────────────────
    liste = []
    empfang_huelle(raumnetze["Empfang"], decken["Empfang"], st, liste)
    flammen = prunkkamin(raumnetze["Empfang"], bs.Netz("Flammen", alle=True), st, liste)
    fertig.append((flammen, "Empfang"))
    empfang_einrichtung(raumnetze["Empfang"], st, liste, kunden)
    tuer_e = teil("Tuer_Empfang_Werk", "Empfang")
    for s in (-1, 1):
        si.bretttuer(tuer_e, st, si.rahmen(EX0, EMPF_Y - s * EMPF_B / 4, -90), EMPF_B / 2, EMPF_H, stoff="Taeferholz", ring=False)
        si.q(tuer_e, st, si.rahmen(EX0, EMPF_Y - s * 0.08, -90), (0, -0.02, 1.15), (0.04, 0.04, 0.16), "Messing", ohne=("-y",))
    w_e = ia.Wand(*ecken(EX0, EX1, EY0, EY1)["hinten"])
    eiserne_tuer(teil("Tuer_Empfang_Tresor", "Empfang"), st, w_e, TRESOR_X, TUER_B, TUER_H)
    alle_setzen(liste, "Empfang", stufe=3)

    # ── Tresorturm (ab Stufe 3) ───────────────────────────────────────────────
    liste = []
    tresor_huelle(raumnetze["Tresor"], decken["Tresor"], st, liste)
    tresor_einrichtung(raumnetze["Tresor"], st, liste)
    w_t = ia.Wand(*ecken(TX0, TX1, TY0, TY1)["vorne"])
    eiserne_tuer(teil("Tuer_Tresor_Empfang", "Tresor"), st, w_t, TRESOR_X, TUER_B, TUER_H, innen=True)
    alle_setzen(liste, "Tresor", stufe=3)

    for o in plaetze:
        setzen(o, "Werk")
    for o in kunden:
        setzen(o, "Empfang" if o.name.startswith("Kunde_Vornehm_") else "Werk")

    # ── Blickpunkte ──────────────────────────────────────────────────────────
    for name, (auge, ziel) in BLICKE.items():
        raum = name.split("_")[1]
        setzen(si.blick(name, auge, ziel), raum, stufe=RAUM_STUFE.get(raum))

    # ── Objekte, Zustände, Hierarchie ─────────────────────────────────────────
    raeume = {r: n.objekt() for r, n in raumnetze.items()}
    for r, k in RAUM_STUFE.items():
        raeume[r]["stufe"] = k
    kinder = {r: [decken[r].objekt()] for r in raeume}
    kinder["Werk"] += stufen_obj
    for o, raum in fertig:
        kinder[raum].append(o)
    for name, (netz, raum) in teile.items():
        o = netz.objekt()
        kinder[raum].append(o)
        if name.startswith("Einrichtung_"):
            bs.zustand(o)
        if name.startswith("Tuer_"):
            a, b = name.split("_")[1:3]
            k = max(RAUM_STUFE.get(a, 1), RAUM_STUFE.get(b, 1))
            if k > 1:
                o["stufe"] = k
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
    balken, schale_l, schale_r = waagen
    ti.animieren(waage_bewegung(balken, schale_l, schale_r, (-1, 0, 0)), ARBEIT_BILDER)
    animieren(flammen_bewegung(flammen), FEUER_BILDER, "Feuer")
    bpy.context.view_layer.update()
    return list(raeume.values()) + [o for liste in kinder.values() for o in liste]


def animieren(bewegt, bilder, spur):
    """Wie tischler_innen.animieren, aber in eine NLA-Spur mit dem Namen `spur` (eine eigene Animation im GLB)."""
    import bpy
    for o, bewegung in bewegt:
        ruhe = (o.location.copy(), o.rotation_euler.copy(), o.scale.copy())
        o.animation_data_create()
        act = bpy.data.actions.new(f"{spur}_{o.name}")
        o.animation_data.action = act
        for bild in range(bilder + 1):
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
        nla = o.animation_data.nla_tracks.new()
        nla.name = spur
        nla.strips.new(spur, 1, act)
        o.animation_data.action = None
        o.location, o.rotation_euler, o.scale = ruhe


def flammen_bewegung(o):
    """Die Flammen im Kamin flackern: Höhe und Breite schwanken in zwei Wellen (5 und 9 je Schleife)."""
    def bewegung(bild):
        t = 2 * math.pi * bild / FEUER_BILDER
        h = 1 + 0.18 * math.sin(5 * t) + 0.09 * math.sin(9 * t + 0.5)
        b = 1 - 0.07 * math.sin(5 * t + 1.2) + 0.03 * math.sin(9 * t)
        return None, None, (b, b, h)
    return [(o, bewegung)]


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

MARKEN = {"Bedienen": (0.95, 0.25, 0.2), "Wiegen": (0.95, 0.8, 0.15), "Rechnen": (0.2, 0.45, 1.0), "Zaehlen": (0.3, 0.85, 0.3),
          "Hauptbuch": (0.6, 0.3, 1.0), "Kaufmann": (1.0, 0.5, 0.1), "Buerger": (0.55, 0.35, 0.2), "Vornehm": (0.9, 0.9, 0.95)}


def platz_marken(z, raeume):
    """Marken für die Plätze im Grundriss (Pfeil = Blickrichtung; Farbe je Tätigkeit bzw. Kundengruppe)."""
    import bpy
    stoffe = {k: bs._hilfsstoff("_Marke_" + k, f) for k, f in MARKEN.items()}
    liste = []
    for o in list(bpy.context.scene.objects):
        if o.type != "EMPTY" or not o.name.startswith(("Schreiber_", "Kunde_")) or ia.raum_von(o) not in raeume:
            continue
        art = o["taetigkeit"] if o.name.startswith("Schreiber_") else o.name.split("_")[1]
        netz = bs.Netz("_Marke_" + o.name)
        t = o.matrix_world
        p = t.translation.copy()
        p.z = z
        vor = t.to_3x3() @ Vector((0, 1, 0))
        quer = Vector((-vor.y, vor.x, 0))
        netz.vieleck_nach([p - quer * 0.14 - vor * 0.1, p + vor * 0.3, p + quer * 0.14 - vor * 0.1], (0, 0, 1), stoffe[art])
        liste.append(netz.objekt())
    return liste


def vorschau(schnell):
    """Bilder nach docs/modelle/: geldleiher_innen-blicke.png (jeder Blickpunkt in Stufe 3 mit aller Einrichtung),
    geldleiher_innen-stufen.png (die Wechselstube von oben je Stufe) und geldleiher_innen-grundriss.png (alle Räume von oben mit den
    Ankern: Bedienen rot, Wiegen gelb, Rechnen blau, Zählen grün, Hauptbuch lila; Kaufleute orange, Bürger braun, vornehme Kunden
    weiß; Stellplätze türkis, Blickpunkte gelb)."""
    import bpy
    szene, kam = ia.buehne(schnell, lampe=70, glut=200, fuell=[(0, 0, H_WERK - 0.5, 110, 1), ((LX0 + LX1) / 2, (LY0 + LY1) / 2, 2.4, 60, 1),
                                                                  ((PX0 + PX1) / 2, (PY0 + PY1) / 2, H_PFAND - 0.4, 60, 1),
                                                                  ((KX0 + KX1) / 2, (KY0 + KY1) / 2, H_KONTOR - 0.4, 80, 2),
                                                                  ((EX0 + EX1) / 2, (EY0 + EY1) / 2, H_EMPFANG - 0.5, 110, 3),
                                                                  ((TX0 + TX1) / 2, (TY0 + TY1) / 2, 3.0, 50, 3)])
    szene.render.resolution_x, szene.render.resolution_y = 640, 360
    szene.render.resolution_percentage = 50 if schnell else 100
    szene.frame_set(1 + round(0.45 * ARBEIT_BILDER))
    bilder = []
    ia.zeigen(szene, 3, True)
    for blick in BLICKE:
        o = bpy.data.objects[blick]
        kam.matrix_world = o.matrix_world @ Matrix.Rotation(math.radians(90), 4, "X")
        bilder.append(bs.rendern_einzeln(f"_{NAME}-{blick}", blick))
    print(bs.zusammensetzen(NAME + "-blicke", bilder, spalten=3))
    szene.frame_set(1)
    kam.data.type = "ORTHO"
    kam.data.sensor_fit = "AUTO"
    sonne = bpy.data.objects.new("_Sonne", bpy.data.lights.new("_Sonne", "SUN"))
    sonne.data.energy = 2.5
    sonne.rotation_euler = (math.radians(25), math.radians(-15), 0)
    szene.collection.objects.link(sonne)

    def von_oben(r, rand=0.6, oben=1.2, aufloesung=760):
        x0, x1, y0, y1 = r
        kam.data.ortho_scale = max(x1 - x0, y1 - y0 + oben) + 2 * rand
        kam.matrix_world = Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2 + oben / 2, 40.0))
        szene.render.resolution_x = szene.render.resolution_y = aufloesung
    von_oben((WX0 - WAND, WX1 + WAND, WY0 - WAND, WY1 + WAND))
    teile = []
    for k in (1, 2, 3):
        ia.zeigen(szene, k, False, decke=False, raeume={"Werk"})
        teile.append(bs.rendern_einzeln(f"_{NAME}-stufe{k}", f"Stufe {k}: {STUFEN[k - 1]}"))
    print(bs.zusammensetzen(NAME + "-stufen", teile, spalten=3))
    teile = []
    for raeume, r, text in (({"Werk", "Lager", "Pfand"}, (WX0 - WAND, WX1 + WAND, WY0 - WAND, LY1 + WAND),
                             "Wechselstube, Tresorgewölbe und Pfandkammer, Stufe 3"),
                            ({"Kontor", "Empfang", "Tresor"}, (KX0 - WAND, EX1 + WAND, EY0 - WAND, TY1 + WAND),
                             "Kontor (Stufe 2), Empfang und Tresorturm (Stufe 3)")):
        von_oben(r, aufloesung=900)
        liste = ia.marken(4.0, raeume) + platz_marken(4.0, raeume)
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
