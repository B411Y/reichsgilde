"""Innenraum des Gardisten: die Wachstube (Hauptraum), dahinter die Waffenkammer (Lager), die Arrestzelle und ab der Stufe
Garnison die Hauptmannsstube, ab der Stufe Stadtwache rechts daneben die Übungshalle; Stufen Wachhaus, Stadtwache, Garnison.

    blender -b --python tools/modelle/gardist_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/gardist_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/innen/gardist_innen.glb und Bilder nach docs/modelle/ (gardist_innen-blicke.png: das Bild jedes
Blickpunkts, gardist_innen-stufen.png: die Wachstube von oben je Stufe, gardist_innen-grundriss.png: alle Räume von oben mit den
Ankern).

Koordinaten wie beim Schmied (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte der Wachstube auf
dem Boden. Außen ist das Haus eine massive Kaserne (gardist.glb: Bruchstein mit Eckquadern, Tordurchfahrt mit dem Stadtwappen,
Wehrturm); innen ist alles wehrhaft und ordentlich – Bruchstein, Eckquader und Türgewände aus Sandstein,
Steinplatten, Balkendecken, vergitterte Fenster:
    Werk        „Wachstube“ innen x −6,0 … 6,0, y −4,0 … 4,0, 3,2 m hoch. Links der Kamin, davor die Hunde (gekauft), der
                Würfeltisch, hinten links vier Etagenpritschen; hinten der Waffenständer unter dem Stadtwappen, davor zwei Bänke
                zum Rüstungputzen; rechts der Wetztisch, hinten rechts der Schleifbock, an der rechten Wand der Wachplan; vorn
                die Haustür, das Hoftor (Tordurchfahrt), das Pult mit dem Personalbuch und zwei Schreibpulte mit dem Wachbuch.
                Türen: hinten die Gittertür der Arrestzelle, die Tür zur Waffenkammer und (ab Stufe 3, vorher vermauert) die
                zur Hauptmannsstube, rechts (ab Stufe 2, vorher vermauert) die zur Übungshalle.
    Lager       „Waffenkammer“ hinter der Wachstube: x −1,7 … 2,0, y 4,5 … 10,0, 3,0 m hoch. Hinten das große Gestell mit
                Hellebarden und Spießen, links Rüstungen auf Ständern, rechts Schilde an der Wand, Pfeilkisten.
    Zelle       „Arrestzelle“ hinter der Wachstube links: x −6,0 … −2,2, y 4,5 … 9,0, 2,8 m hoch, dunkler Bruchstein. Steinbänke
                mit Stroh an der Rück- und der linken Wand, Stroh am Boden, Eimer, Ketten mit Schellen an der Wand, ein kleines
                vergittertes Fenster hoch oben, Kreidestriche der Tage.
    Uebung      „Übungshalle“ ab Stufe 2 rechts neben der Wachstube: x 6,5 … 13,5, y −3,5 … 3,5, 4,2 m hoch. Auf dem Fechtboden
                vier Strohpuppen in einer Reihe (von beiden Seiten wird gefochten), Zielscheibe, Waffenrechen, Bänke.
    Hauptmann   „Hauptmannsstube“ ab Stufe 3 hinter der Wachstube rechts: x 2,5 … 6,0, y 4,5 … 9,0, 3,2 m hoch, gekalkt mit
                Täfelung, Dielen. Schreibtisch mit dem Stadtplan, Lehnstuhl, Truhe, Wappenbanner, Bord mit Akten.
Alle Räume sind rundum geschlossen, die Kamera steht im Raum.

Knoten im GLB – dieselben Regeln wie in den anderen Innenräumen (Raum_…, Decke_…, Stufe1–Stufe3, Bedienung_…, Tuer_…,
Einrichtung_…, Holztuer, Blick_…, Lager_…, Licht_…), dazu:
    Raum_Zelle                  neuer Raumschlüssel „Zelle“, immer da; Tuer_Werk_Zelle ist die Gittertür (von der Wachstube aus
                                sieht man durch die Stäbe in die Zelle), Tuer_Zelle_Werk ihr Rahmen mit dem Riegel auf der Zellenseite
    Raum_Uebung                 extras „stufe“ = 2 (neuer Raumschlüssel „Uebung“), ebenso Tuer_Werk_Uebung, Tuer_Uebung_Werk und
                                alle Anker darin
    Raum_Hauptmann              extras „stufe“ = 3 (neuer Raumschlüssel „Hauptmann“), ebenso Tuer_Werk_Hauptmann,
                                Tuer_Hauptmann_Werk und alle Anker darin
    Wache_1 … Wache_42          Plätze der Wachen (Fußpunkt, Blick −Z) mit extras „taetigkeit“, „bewegung“, „platz“ (1 … 8:
                                Füllreihenfolge je Tätigkeit) und bei sitzenden Plätzen „sitzhoehe“:
                                1–8   Wuerfeln (Zaehlen, sitzend) am Würfeltisch, erst die Bank hinten (Gesicht zur Kamera)
                                9–16  Putzen (Putzen, sitzend) auf den Bänken vor dem Waffenständer, erst die hintere Bank
                                17–24 Schaerfen: 17 am Schleifbock (Schleifen), 18–24 am Wetztisch (Feinarbeit, sitzend)
                                25–26 Wachbuch (Schreiben, sitzend) an den beiden Schreibpulten
                                27–34 Schlafen (Schlafen, liegend) auf den Etagenpritschen, erst unten; Fußpunkt unter der Hüfte auf
                                      dem Strohsack, Blick zu den Füßen (in den Raum)
                                35–42 Fechten (Fechten, stehend, extras „stufe“ = 2) vor den Strohpuppen der Übungshalle, erst die
                                      hintere Reihe (Gesicht zur Kamera); die Puppe steht figuren.FECHTEN „puppe“ vor dem Platz
    Gefangen_1 … Gefangen_6     Plätze der Gefangenen auf den Steinbänken der Zelle (Bewegung „Sitzen“, extras „sitzhoehe“,
                                „taetigkeit“ = „Gefangen“, „platz“ = Füllreihenfolge)
    Hauptmann_1                 der Hauptmann am Schreibtisch (stufe 3, extras „taetigkeit“ = „Befehligen“ wie im Kern, Bewegung
                                „Feinarbeit“, sitzend)
    Schleifstein, Flammen       bewegte Teile (Animation „Arbeit“)
Keine Arbeit_- und Werkstueck_-Anker: Die Wache stellt keine Ware her.
Animation „Arbeit“ (6 s, Schleife): Der Schleifstein dreht sich in 1,2 s einmal (wie die Bewegung „Schleifen“), die Flammen im
Kamin flackern. Bei Zeit 0 steht der Stein in Ruhe.
Lichter: extras „art“ = „Fenster“, „Lampe“ (Laternen) und „Glut“ (Kamin, Kerzen).
Materialien: „Farben“ (Eckenfarben, Materialart als UV.y), „Hausfarbe“ (Haustür, Hoftor), „Fenster“, „Glut“, „Eisen“, „Gold“,
„Wasser“.
Einrichtung: Werkzeug1 (Hellebarden im Waffenständer), Werkzeug2 (Armbrüste mit Köchern auf seinem Bord), Hunde (zwei Hunde auf
Stroh neben dem Kamin, ruhend), Kettenhemd (Kettenhemden auf Ständern in der Waffenkammer), Regale (Gestelle mit Helmen und
Bolzen in der Waffenkammer), Loeschfass, Eisentuer (Haustür).
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import alchimist_innen as ai  # noqa: E402  (Drehkörper, Scheiben, am Ort, vermauert, Verkleinern)
import dieb_innen as di  # noqa: E402  (flache Teile, Kerzen, Münzen, Plan auf Pergament)
import figuren as fg  # noqa: E402  (Arbeitshöhen, Fechten, Zählen)
import gardist as ga  # noqa: E402  (Zielscheibe, Rüstungsständer)
import innenausbau as ia  # noqa: E402  (Wand, Böden, Decke, Prüfbericht, Vorschau)
import parfumeur_innen as pi  # noqa: E402  (Lichter, Wandquader, Täfelung)
import prediger_innen as pr  # noqa: E402  (Steinplatten, Schreibpult, Gittertür, Truhe)
import schmied_innen as si  # noqa: E402  (Wände, Pult, Türen, Regal, Kisten, Löschfass, Schleifbock, Anker, Blickpunkte)
import tischler_innen as ti  # noqa: E402  (Fenster, Laterne, Hoftor, Animation, Export)
import wirt_innen as wi  # noqa: E402  (Punkt am Platz, Tisch, Bank, Dielen)

NAME = "gardist_innen"
STUFEN = ("Wachhaus", "Stadtwache", "Garnison")

bs.FARBEN.update({
    "Puppenstroh": (0.66, 0.57, 0.34),        # Leib der Strohpuppen
    "Puppensack": (0.52, 0.45, 0.33),         # Sackleinen um den Leib
    "Hundfell": (0.42, 0.30, 0.19),
    "Hundfell hell": (0.62, 0.50, 0.34),
    "Hundfell grau": (0.36, 0.34, 0.31),
    "Hundnase": (0.08, 0.07, 0.07),
    "Kettenhemd": (0.33, 0.34, 0.36),         # Ringgeflecht (matt gemalt)
    "Wappenrock": (0.62, 0.13, 0.12),         # Stadtrot der Wache, etwas gedeckter
    "Kreide": (0.86, 0.85, 0.80),
    "Schiefertafel": (0.17, 0.18, 0.19),
    "Fechtboden": (0.56, 0.45, 0.31),
    "Horn": (0.78, 0.70, 0.55),               # Signalhorn
    "Sanduhrglas": (0.74, 0.77, 0.73),        # Stundenglas (deckend gemalt)
})

# ── Maße ─────────────────────────────────────────────────────────────────────
WAND = 0.5                                                         # Bruchstein
WX0, WX1, WY0, WY1 = -6.0, 6.0, -4.0, 4.0                          # Wachstube innen
H_WERK = 3.2
LX0, LX1, LY0, LY1 = -1.7, 2.0, WY1 + WAND, 10.0                   # Waffenkammer
H_LAGER = 3.2
ZX0, ZX1, ZY0, ZY1 = -6.0, -2.2, WY1 + WAND, 9.0                   # Arrestzelle
H_ZELLE = 2.8
UX0, UX1, UY0, UY1 = WX1 + WAND, 13.5, -3.5, 3.5                   # Übungshalle (ab Stufe 2)
H_UEBUNG = 4.2
HX0, HX1, HY0, HY1 = 2.5, WX1, WY1 + WAND, 9.0                     # Hauptmannsstube (ab Stufe 3)
H_HAUPT = 3.2
HAUSTUER_X, HAUSTUER_B, HAUSTUER_H = -2.2, 1.1, 2.2                # Tür zur Gasse in der Vorderwand
HOFTOR_X, HOFTOR_B, HOFTOR_H = 3.6, 2.2, 2.5                       # Tordurchfahrt in der Vorderwand
ZELLE_X, LAGER_X, HAUPT_X, TUER_B, TUER_H = -3.4, 0.4, 4.3, 1.0, 2.1   # Türen in der Rückwand
UEBUNG_Y, UEBUNG_B, UEBUNG_H = 1.6, 1.2, 2.3                       # Tür zur Übungshalle in der rechten Wand
FPS = 24
TAKT = fg.BEWEGUNGEN["Schleifen"] * FPS                            # Bilder je Umdrehung des Schleifsteins (1,2 s wie „Schleifen“)
ARBEIT_BILDER = round(5 * TAKT)                                    # Länge der Animation „Arbeit“ (6 s: fünf Umdrehungen)
SITZ = 0.43                                                        # Oberkante der Bänke (wie beim Wirt)

# Geräte (Blender-Koordinaten)
KAMIN_Y, KAMIN_B, KAMIN_T = -1.8, 1.7, 0.62                        # Kamin an der linken Wand (Mitte, Breite, Tiefe)
PRITSCHE_Y = (0.5, 1.5, 2.5, 3.5)                                  # Etagenpritschen an der linken Wand (Mitte y)
PRITSCHE_L, PRITSCHE_B = 1.95, 0.85
PRITSCHE_Z = (0.42, 1.48)                                          # Oberkante der Strohsäcke unten und oben
WUERFEL = Vector((-2.6, -0.7, 0))                                  # Würfeltisch: Mitte, 2,6 × 0,8 m, 0,78 m hoch
WUERFEL_X = (-3.5, -2.9, -2.3, -1.7)
STAENDER_X = -1.5                                                  # Waffenständer an der Rückwand (Mitte), 2,1 m breit
PUTZ_Y = (3.0, 1.5)                                                # Bänke zum Rüstungputzen: hinten (Blick −y), vorn (Blick +y)
PUTZ_X = (-2.35, -1.75, -1.05, -0.45)
WETZ = Vector((2.6, 0.3, 0))                                       # Wetztisch: Mitte, 2,2 × 0,7 m, 0,77 m hoch
SCHLEIF = Vector((2.6, 3.0, 0))                                    # Schleifbock: der Arbeiter steht bei −x und blickt nach +x
SCHREIB_X, SCHREIB_Y = (0.2, 1.3), -1.7                            # Schreibpulte: Fußpunkte der Schreiber (Blick −y zur Haustür)
WACHPLAN_Y = -1.8                                                  # Wachplan an der rechten Wand (Mitte)
PUPPE_X = (8.4, 9.8, 11.2, 12.6)                                   # Strohpuppen in der Übungshalle (y 0)
HAUPT_TISCH = Vector((4.25, 7.35, 0))                              # Schreibtisch des Hauptmanns: Mitte, 1,6 × 0,8 m
HAUPT_PLATZ = Vector((4.25, 8.05, 0))                              # Fußpunkt des Hauptmanns (Blick −y)

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Werk_0": ((0.0, -3.6, 2.3), (-0.8, 3.6, 0.8)),            # Putzbänke, Waffenständer, Wappen, Würfeltisch, Gittertür
    "Blick_Werk_L1": ((3.4, -3.3, 2.2), (-5.8, 1.0, 0.7)),           # Kamin, Hunde, Würfeltisch, Pritschen
    "Blick_Werk_L2": ((-0.6, -0.9, 2.25), (-5.6, 2.6, 0.9)),         # die Etagenpritschen
    "Blick_Werk_R1": ((-4.2, -3.2, 2.2), (5.8, 1.6, 0.8)),           # Wetztisch, Schleifbock, Wachplan, Türen rechts
    "Blick_Werk_R2": ((-1.6, 3.2, 2.2), (2.6, -3.9, 0.8)),           # Schreibpulte, Haustür, Hoftor, Personalbuch
    "Blick_Lager_0": ((0.4, 4.85, 2.2), (0.2, 10.0, 0.8)),           # das Gestell mit den Hellebarden
    "Blick_Lager_L1": ((1.7, 4.9, 2.1), (-1.6, 8.6, 0.8)),           # Rüstungen und Kettenhemden
    "Blick_Lager_R1": ((-1.4, 4.9, 2.1), (2.0, 8.6, 1.0)),           # Schilde, Regale, Pfeilkisten
    "Blick_Zelle_0": ((-2.6, 4.85, 2.05), (-5.4, 8.6, 0.55)),        # die Bänke mit den Gefangenen
    "Blick_Zelle_R1": ((-5.6, 4.85, 2.05), (-2.5, 8.8, 0.7)),        # Eimer, Ketten, Fenster
    "Blick_Uebung_0": ((7.0, -3.1, 2.7), (11.4, 1.8, 0.9)),          # Strohpuppen, Fechtboden
    "Blick_Uebung_L1": ((13.1, -3.1, 2.7), (7.4, 2.4, 0.9)),         # Puppen von rechts, Bänke, Tür
    "Blick_Uebung_R1": ((7.0, 3.1, 2.7), (13.0, -2.6, 0.9)),         # Zielscheibe, Waffenrechen
    "Blick_Hauptmann_0": ((4.25, 4.85, 2.1), (4.25, 9.0, 0.9)),      # Schreibtisch, Banner
    "Blick_Hauptmann_L1": ((5.7, 4.9, 2.1), (2.6, 8.6, 0.8)),        # Truhe, Bord, Schreibtisch
}

Oe = si.Oe
WERK_OEFFNUNGEN = {
    "vorne": [Oe(-4.3, 1.1, 0.9, 1.1), Oe(HAUSTUER_X, 0.0, HAUSTUER_B, HAUSTUER_H), Oe(0.8, 1.1, 0.9, 1.1),
              Oe(HOFTOR_X, 0.0, HOFTOR_B, HOFTOR_H)],
    "rechts": [Oe(UEBUNG_Y, 0.0, UEBUNG_B, UEBUNG_H)],
    "hinten": [Oe(ZELLE_X, 0.0, TUER_B, TUER_H), Oe(LAGER_X, 0.0, TUER_B, TUER_H), Oe(HAUPT_X, 0.0, TUER_B, TUER_H)],
    "links": [],
}
SCHILDER = {"Tuer_Werk_Lager": "Tür zur Waffenkammer", "Tuer_Lager_Werk": "Tür zur Wachstube",
            "Tuer_Werk_Zelle": "Gittertür zur Arrestzelle", "Tuer_Zelle_Werk": "Gittertür zur Wachstube",
            "Tuer_Werk_Uebung": "Tür zur Übungshalle", "Tuer_Uebung_Werk": "Tür zur Wachstube",
            "Tuer_Werk_Hauptmann": "Tür zur Hauptmannsstube", "Tuer_Hauptmann_Werk": "Tür zur Wachstube"}
RAUM_STUFE = {"Uebung": 2, "Hauptmann": 3}

ecken = ia.ecken
drehkoerper, scheibe, am_ort = ai.drehkoerper, ai.scheibe, ai.am_ort
licht, wq = pi.licht, pi.wq
drehen = wi.drehen


# ─────────────────────────────────────────────────────────────────────────────
#  Hilfen
# ─────────────────────────────────────────────────────────────────────────────

def platz(liste, art, punkt, blick, taetigkeit, bewegung, nummer, z=0.0, sitz=None, stufe=None):
    """Anker <art>_<n> (fortlaufend je Art: Wache, Gefangen, Hauptmann) mit extras „taetigkeit“, „bewegung“, „platz“
    (Füllreihenfolge je Tätigkeit) und bei sitzenden Plätzen „sitzhoehe“ (Oberkante der Bank über dem Fußpunkt)."""
    o = bs.anker(f"{art}_{sum(1 for a in liste if a.name.startswith(art + '_')) + 1}", (punkt[0], punkt[1], z),
                 si.drehung_von(blick))
    o["taetigkeit"], o["bewegung"], o["platz"] = taetigkeit, bewegung, nummer
    if sitz is not None:
        o["sitzhoehe"] = sitz
    if stufe:
        o["stufe"] = stufe
    liste.append(o)
    return o


def mauerraum(netz, decke, st, x0, x1, y0, y1, hoehe, oeffnungen, ohne_aussen=(), ohne_laibung=(), stoff="Bruchstein", saat=0,
              enden=None, abdeckung=None):
    """Raum aus Bruchstein (schmied_innen.wand), an den inneren Ecken Eckquader aus Sandstein, um die Türen Gewände;
    Balkendecke (mit `abdeckung` über den Mauern, nur wo kein Nachbarraum gleich hoch ist). `ohne_aussen`: Seiten, deren
    Außenseite der Nachbarraum zeigt (geteilte Wand), `ohne_laibung`: Seiten, deren Laibungen der Nachbarraum baut, `enden`:
    {Seite: (Anfang, Ende)} – ob die Außenseite dort um die Wanddicke verlängert wird."""
    kanten = ecken(x0, x1, y0, y1)
    netz.alle = True
    for seite in ("vorne", "rechts", "hinten", "links"):
        si.wand(netz, st, *kanten[seite], hoehe, oeffnungen.get(seite, []), stoff=stoff, dicke=WAND, aussen=seite not in ohne_aussen,
                laibung=seite not in ohne_laibung, enden=(enden or {}).get(seite, (True, True)))
    netz.alle = False
    for seite in ("vorne", "rechts", "hinten", "links"):
        eckquader(netz, st, ia.Wand(*kanten[seite], oeffnungen.get(seite, [])), hoehe)
    ia.balkendecke(decke, st, x0, x1, y0, y1, hoehe, abdeckung=abdeckung, saat=saat)


def eckquader(netz, st, wand, hoehe):
    """Eckquader aus Sandstein an beiden Enden der Wand (im Wechsel lang und kurz, 3 cm vor dem Mauerwerk) und Gewände um die
    Türen (Öffnungen ab dem Boden): Quader im Wechsel an beiden Seiten, oben ein Sturz."""
    lagen = max(2, round(hoehe / 0.34))
    h = hoehe / lagen
    for k in range(lagen):
        lang = 0.46 if k % 2 else 0.26
        v0, v1 = k * h + 0.008, (k + 1) * h - 0.008
        wq(netz, st, wand, 0.0, lang, v0, v1, 0.0, 0.03, "Sandstein", ohne=("-y", "-x"))
        lang2 = 0.26 if k % 2 else 0.46
        wq(netz, st, wand, wand.laenge - lang2, wand.laenge, v0, v1, 0.0, 0.03, "Sandstein", ohne=("-y", "+x"))
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


def fenster_und_licht(netz, st, kanten, oeffnungen, anker, stufe=1):
    """Scheiben der Fenster eines Raums mit Kreuz und Gittern (schmied_innen.fenster) und je ein Licht „Fenster“."""
    for seite, liste in oeffnungen.items():
        a, b = kanten[seite]
        for o in liste:
            if o.v < 0.05:
                continue
            si.fenster(netz, st, a, b, o, dicke=WAND)
            wand = ia.Wand(a, b, [o])
            p = wand.p(wand.ops[0][0], o.v + o.h / 2, 0.1)
            anker.append(licht(p, (wand.n.x, wand.n.y, 0), "Fenster", stufe))


def stroh(netz, st, x0, x1, y0, y1, menge, saat, z=0.0):
    """Lose Strohhalme auf dem Boden (flache Streifen 2 mm über z)."""
    zufall = random.Random(saat)
    for _ in range(menge):
        c = Vector((zufall.uniform(x0, x1), zufall.uniform(y0, y1), z + 0.002 + 0.001 * zufall.random()))
        w = zufall.uniform(0, math.pi)
        d = Vector((math.cos(w), math.sin(w), 0)) * zufall.uniform(0.05, 0.11)
        q = Vector((-d.y, d.x, 0)).normalized() * 0.006
        netz.vieleck_nach([c - d - q, c + d - q, c + d + q, c - d + q], (0, 0, 1), st["Strohhalm"])


def strohhaufen(netz, st, mitte, rx, ry, h, saat):
    """Flacher Haufen Stroh: ein gedrückter Kegel, darauf und daneben Halme."""
    m = Vector(mitte)
    profil = [(0.0, 1.0), (h * 0.5, 0.75), (h, 0.0)]
    hilf = bs.Netz("_hilf", alle=netz.alle)
    drehkoerper(hilf, st, Vector((0, 0, m.z)), [(z, r) for z, r in profil], "Puppenstroh", n=9)
    for flaeche, k in zip(hilf.flaechen, hilf.fstoff):
        netz.vieleck([Vector((m.x + hilf.ecken[i].x * rx, m.y + hilf.ecken[i].y * ry, hilf.ecken[i].z)) for i in flaeche],
                     hilf.stoffe[k], immer=True)
    stroh(netz, st, m.x - rx * 1.3, m.x + rx * 1.3, m.y - ry * 1.3, m.y + ry * 1.3, 18, saat, z=m.z)


def schild(netz, st, M, g=1.0, rand="Sandstein", tief=0.0):
    """Stadtwappen (örtlich: Ebene x–z, +y in den Raum, Mitte bei 0): Schild in Stadtweiß, die obere Hälfte rot, mit Rand."""
    umriss = [(x * g, z * g) for x, z in bs.SCHILD]
    rahmen = [(x * 1.14 * g, z * 1.1 * g) for x, z in bs.SCHILD]
    di.flach(netz, st, rahmen, M @ Matrix.Translation((0, tief + 0.02, 0)), 0.04, rand)
    di.flach(netz, st, umriss, M @ Matrix.Translation((0, tief + 0.047, 0)), 0.015, "Stadtweiss")
    di.flach(netz, st, [(-0.34 * g, 0.42 * g), (0.34 * g, 0.42 * g), (0.34 * g, 0.08 * g), (-0.34 * g, 0.08 * g)],
             M @ Matrix.Translation((0, tief + 0.056, 0)), 0.004, "Stadtrot")


# ─────────────────────────────────────────────────────────────────────────────
#  Wachstube
# ─────────────────────────────────────────────────────────────────────────────

def stube_huelle(werk, decke, st, anker):
    """Wände aus Bruchstein mit Eckquadern und Gewänden, vergitterte Fenster, Balkendecke, Steinplatten."""
    mauerraum(werk, decke, st, WX0, WX1, WY0, WY1, H_WERK, WERK_OEFFNUNGEN, ohne_aussen=("hinten", "rechts"), saat=11, abdeckung=WAND)
    fenster_und_licht(werk, st, ecken(WX0, WX1, WY0, WY1), WERK_OEFFNUNGEN, anker)
    pr.steinplatten(werk, st, WX0, WX1, WY0, WY1, saat=5, breite=0.55)


def kamin(netz, flammen, st, anker):
    """Kamin an der linken Wand: Herdplatte aus Sandstein, Wangen und Sturz aus Bruchstein, verrußte Rückwand, verputzter
    Rauchfang bis in die Decke; Scheite und Glut, die Flammen („Glut“) sind ein eigenes Netz (flackern in der Animation);
    Feuerbock, Schürhaken, Scheite auf dem Sims."""
    ym, b, t = KAMIN_Y, KAMIN_B, KAMIN_T
    x0 = WX0
    y0, y1 = ym - b / 2, ym + b / 2
    netz.quader((x0 + t / 2 + 0.05, ym, 0.1), (t + 0.1, b + 0.2, 0.2), st["Sandstein"], ohne=("-x", "-z"))      # Herdplatte
    for y in (y0 + 0.12, y1 - 0.12):                                                       # Wangen
        netz.quader((x0 + t / 2, y, 0.2 + 0.6), (t, 0.24, 1.2), st["Bruchstein"], ohne=("-x", "-z"))
    netz.quader((x0 + t / 2 + 0.02, ym, 1.6), (t + 0.04, b, 0.4), st["Sandstein"], ohne=("-x",))           # Sturz
    netz.quader((x0 + t / 2 + 0.06, ym, 1.83), (t + 0.12, b + 0.12, 0.06), st["Sandstein"], ohne=("-x",))  # Sims
    netz.vieleck_nach([(x0 + 0.01, y0 + 0.24, 0.2), (x0 + 0.01, y1 - 0.24, 0.2), (x0 + 0.01, y1 - 0.24, 1.4),
                       (x0 + 0.01, y0 + 0.24, 1.4)], (1, 0, 0), st["Russ"])
    alle, netz.alle = netz.alle, True
    netz.vieleck_nach([(x0, y0 + 0.24, 1.4), (x0 + t, y0 + 0.24, 1.4), (x0 + t, y1 - 0.24, 1.4), (x0, y1 - 0.24, 1.4)], (0, 0, -1),
                      st["Russ"])
    netz.alle = alle
    oben = H_WERK                                                                          # Rauchfang: verjüngt bis in die Decke
    xa, xb = x0 + t + 0.02, x0 + 0.42
    netz.vieleck_nach([(xa, y0, 1.86), (xa, y1, 1.86), (xb, y1 - 0.4, oben), (xb, y0 + 0.4, oben)], (1, 0, 0.4), st["Moertel"])
    for y, yo, s in ((y0, y0 + 0.4, -1), (y1, y1 - 0.4, 1)):
        netz.vieleck_nach([(x0, y, 1.86), (xa, y, 1.86), (xb, yo, oben), (x0, yo, oben)], (0, s, 0.3), st["Moertel"])
    feuer = Vector((x0 + 0.3, ym, 0.2))
    scheibe(netz, st, feuer, 0.36, 0.205, "Glut", n=9)
    for i, w in enumerate((0.4, 1.3, 2.4, 3.0)):                                           # Scheite über Kreuz
        d = Vector((math.cos(w), math.sin(w), 0))
        netz.stab(feuer + d * 0.3 + Vector((0, 0, 0.06 + 0.04 * i)), feuer - d * 0.25 + Vector((0, 0, 0.1 + 0.04 * i)), 0.09, 0.09,
                  st["Scheitholz"])
    for s in (-1, 1):                                                                      # Feuerbock
        netz.stab(feuer + Vector((0.25, s * 0.3, 0)), feuer + Vector((0.25, s * 0.3, 0.3)), 0.03, 0.03, st["Eisen"])
        netz.stab(feuer + Vector((0.25, s * 0.3, 0.3)), feuer + Vector((-0.2, s * 0.3, 0.12)), 0.025, 0.025, st["Eisen"])
    zufall = random.Random(3)
    for k in range(5):                                                                     # Flammen (bewegt)
        p = feuer + Vector((zufall.uniform(-0.1, 0.1), zufall.uniform(-0.26, 0.26), 0.08))
        drehkoerper(flammen, st, p, [(0.0, 0.07), (0.12, 0.06), (0.3 + 0.12 * zufall.random(), 0.0)], "Glut", n=5)
    netz.stab((x0 + t + 0.08, y1 + 0.02, 0.2), (x0 + t + 0.14, y1 + 0.06, 1.0), 0.025, 0.025, st["Eisen"])   # Schürhaken
    anker.append(licht(feuer + Vector((0.35, 0, 0.35)), (1, 0, 0), "Glut"))
    for k in range(4):                                                                     # Scheite auf dem Sims
        netz.stab((x0 + 0.12, y1 - 0.15 - 0.12 * k, 1.9), (x0 + 0.5, y1 - 0.15 - 0.12 * k, 1.9 + 0.01 * k), 0.09, 0.09, st["Scheitholz"])
    return am_ort(flammen, feuer)


def hund(netz, st, fuss, dreh, fell="Hundfell"):
    """Ein ruhender Hund aus Grundkörpern wie die Figuren (Ringe): auf dem Bauch liegend, die Vorderläufe vorgestreckt, der Kopf
    auf den Pfoten, Hängeohren, der Schwanz liegt um die Hinterläufe; ledernes Halsband. `dreh` in Grad (0: Blick nach −y)."""
    M = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    hell = "Hundfell hell" if fell == "Hundfell" else "Hundfell grau"

    def ring(liste, stoff, n=8):
        """Ringe entlang −y (vom Schwanz zum Kopf): (y, rx, rz, z)."""
        rs = [[M @ Vector((rx * math.cos(2 * math.pi * k / n), y, z + rz * math.sin(2 * math.pi * k / n))) for k in range(n)]
              for y, rx, rz, z in liste]
        for i in range(len(rs) - 1):
            mitte = sum(rs[i] + rs[i + 1], Vector()) / (2 * n)
            for k in range(n):
                j = (k + 1) % n
                quad = [rs[i][k], rs[i][j], rs[i + 1][j], rs[i + 1][k]]
                netz.vieleck_nach(quad, sum(quad, Vector()) / 4 - mitte, st[stoff])
        netz.vieleck_nach(rs[0], M.to_3x3() @ Vector((0, 1, 0)), st[stoff])
        netz.vieleck_nach(rs[-1], M.to_3x3() @ Vector((0, -1, 0)), st[stoff])
    ring([(0.36, 0.1, 0.08, 0.13), (0.28, 0.16, 0.13, 0.15), (0.08, 0.15, 0.14, 0.17), (-0.14, 0.14, 0.13, 0.17),
          (-0.26, 0.11, 0.11, 0.18)], fell)                                                # Rumpf
    ring([(-0.24, 0.07, 0.07, 0.2), (-0.32, 0.075, 0.075, 0.18)], fell, n=6)              # Hals
    ring([(-0.3, 0.085, 0.08, 0.12), (-0.44, 0.09, 0.085, 0.12), (-0.5, 0.065, 0.06, 0.1)], fell)   # Kopf, auf den Pfoten
    ring([(-0.5, 0.045, 0.04, 0.085), (-0.6, 0.04, 0.035, 0.08)], hell, n=6)              # Schnauze
    netz.quader((0, 0, 0), (0.04, 0.02, 0.03), st["Hundnase"], M @ Matrix.Translation((0, -0.605, 0.085)))
    for s in (-1, 1):                                                                       # Augen, Hängeohren, Läufe
        netz.quader((0, 0, 0), (0.018, 0.01, 0.012), st["Hundnase"], M @ Matrix.Translation((s * 0.045, -0.515, 0.14)))
        netz.vieleck_nach([M @ Vector(p) for p in ((s * 0.075, -0.36, 0.17), (s * 0.09, -0.44, 0.18), (s * 0.11, -0.43, 0.06),
                                                   (s * 0.1, -0.35, 0.07))], M.to_3x3() @ Vector((s, 0, 0)), st[fell])
        netz.stab(M @ Vector((s * 0.07, -0.2, 0.05)), M @ Vector((s * 0.06, -0.55, 0.03)), 0.06, 0.05, st[fell])   # Vorderlauf
        netz.quader((0, 0, 0), (0.07, 0.08, 0.04), st[hell], M @ Matrix.Translation((s * 0.06, -0.58, 0.02)))     # Pfote
        netz.stab(M @ Vector((s * 0.14, 0.32, 0.08)), M @ Vector((s * 0.16, 0.12, 0.04)), 0.09, 0.07, st[fell])   # Hinterlauf
        netz.quader((0, 0, 0), (0.07, 0.12, 0.04), st[hell], M @ Matrix.Translation((s * 0.17, 0.08, 0.02)))
    schwanz = [(0.0, 0.4, 0.12), (0.12, 0.44, 0.04), (0.24, 0.36, 0.03), (0.28, 0.2, 0.03)]
    for (x0, y0, z0), (x1, y1, z1) in zip(schwanz, schwanz[1:]):
        netz.stab(M @ Vector((x0, y0, z0)), M @ Vector((x1, y1, z1)), 0.04, 0.035, st[fell])
    for k in range(8):                                                                      # Halsband mit Ring
        w0, w1 = 2 * math.pi * k / 8, 2 * math.pi * (k + 1) / 8
        netz.stab(M @ Vector((0.085 * math.cos(w0), -0.3, 0.2 + 0.08 * math.sin(w0))), M @ Vector((0.085 * math.cos(w1), -0.3, 0.2 + 0.08 * math.sin(w1))),
                  0.03, 0.012, st["Leder"], quer=M.to_3x3() @ Vector((0, 1, 0)))
    bs.achteck(netz, st, M @ Vector((0, -0.31, 0.105)), 0.02, 0.0, 0.01, "Eisen", n=6)


def hunde(netz, st):
    """Gekauft: zwei Wachhunde liegen auf Stroh in der Ecke vorn links neben dem Kamin, ein Napf mit Wasser, eine Kette zum Ring in
    der Wand."""
    strohhaufen(netz, st, (-5.15, -3.35, 0.0), 0.75, 0.5, 0.05, saat=17)
    hund(netz, st, (-5.3, -3.25, 0.03), 60, "Hundfell")
    hund(netz, st, (-4.65, -3.45, 0.03), 110, "Hundfell grau")
    napf = Vector((-4.3, -2.85, 0))
    drehkoerper(netz, st, napf, [(0.0, 0.1), (0.06, 0.12), (0.06, 0.1), (0.01, 0.09)], "Holz alt", n=8)
    scheibe(netz, st, napf, 0.1, 0.045, "Wasser", n=8)
    ring = Vector((WX0 + 0.02, -3.3, 0.6))
    netz.stab(ring, ring + Vector((0.08, 0, 0)), 0.04, 0.04, st["Eisen"])
    for k in range(6):                                                                      # Kette zum ersten Hund
        a = ring.lerp(Vector((-5.45, -3.0, 0.2)), k / 6) + Vector((0.08, 0, -0.08 * math.sin(math.pi * k / 6)))
        b = ring.lerp(Vector((-5.45, -3.0, 0.2)), (k + 1) / 6) + Vector((0.08, 0, -0.08 * math.sin(math.pi * (k + 1) / 6)))
        netz.stab(a, b, 0.02, 0.012, st["Eisen"], quer=(0, 0, 1) if k % 2 else (0, 1, 0))


def pritschen(netz, st, plaetze):
    """Vier Etagenpritschen an der linken Wand (Kopfende an der Wand): Pfosten, Rahmen, Bretter, darauf Strohsäcke mit Kissen und
    Wolldecken, eine Leiter am Fußende. Acht Plätze zum Schlafen: erst die unteren (von vorn), dann die oberen."""
    x0, x1 = WX0 + 0.05, WX0 + 0.05 + PRITSCHE_L
    holz = st["Holz"]
    hals = fg.VOELKER["mensch"]["schulter"] - fg.VOELKER["mensch"]["huefte"] + 0.31     # Hüfte → Scheitel (Mensch)
    nummer = 1
    for etage, z in enumerate(PRITSCHE_Z):
        for i, ym in enumerate(PRITSCHE_Y):
            mitte = Vector(((x0 + x1) / 2, ym, 0))
            boden = z - 0.12
            if etage == 0:
                for sx in (x0 + 0.04, x1 - 0.04):                                          # Pfosten bis über die obere Pritsche
                    for sy in (-1, 1):
                        netz.quader((sx, ym + sy * (PRITSCHE_B / 2 - 0.04), 1.0), (0.08, 0.08, 2.0), holz)
            for sy in (-1, 1):                                                              # Wangen
                netz.quader((mitte.x, ym + sy * (PRITSCHE_B / 2 - 0.03), boden - 0.06), (PRITSCHE_L - 0.1, 0.05, 0.14), holz)
            for sx in (x0 + 0.04, x1 - 0.04):
                netz.quader((sx, ym, boden - 0.06), (0.05, PRITSCHE_B - 0.1, 0.12), holz)
            netz.quader((mitte.x, ym, boden - 0.01), (PRITSCHE_L - 0.12, PRITSCHE_B - 0.12, 0.02), st["Holz alt"])
            netz.quader((mitte.x, ym, boden + 0.06), (PRITSCHE_L - 0.16, PRITSCHE_B - 0.14, 0.12), st["Strohsack"], ohne=("-z",))
            netz.quader((x0 + 0.24, ym, z + 0.03), (0.28, 0.5, 0.06), st["Strohsack"])     # Kissen
            decke = ("Decke grob", "Decke rot", "Kutte")[(i + etage) % 3]
            netz.quader((x1 - 0.5, ym, z + 0.01), (0.78, PRITSCHE_B - 0.08, 0.02), st[decke])
            netz.quader((x1 - 0.12, ym, z - 0.05), (0.02, PRITSCHE_B - 0.08, 0.12), st[decke])
            if etage == 1:                                                                  # Leiter am Fußende
                for sy in (-0.18, 0.18):
                    netz.stab((x1 + 0.03, ym + sy, 0.0), (x1 + 0.03, ym + sy, z), 0.04, 0.04, holz)
                for k in range(3):
                    netz.stab((x1 + 0.03, ym - 0.18, 0.4 + 0.35 * k), (x1 + 0.03, ym + 0.18, 0.4 + 0.35 * k), 0.03, 0.03, holz)
            huefte = Vector((x0 + PRITSCHE_L / 2 - (PRITSCHE_L / 2 - 0.12 - hals), ym, 0))
            platz(plaetze, "Wache", huefte, (1, 0), "Schlafen", "Schlafen", nummer, z=z)
            nummer += 1
    for k, ym in enumerate(PRITSCHE_Y):                                                    # Stiefel und ein Helm vor den Pritschen
        if k % 2:
            continue
        for dy in (-0.08, 0.08):
            netz.quader((x1 + 0.3, ym + dy, 0.17), (0.11, 0.09, 0.34), st["Leder"])
            netz.quader((x1 + 0.37, ym + dy, 0.05), (0.14, 0.085, 0.1), st["Leder"])


def wuerfeltisch(netz, st, plaetze, anker):
    """Der Würfeltisch: Tisch und zwei Bänke wie beim Wirt; vor jedem Platz ein kleiner Haufen Münzen (figuren.ZAEHLEN „haufen“,
    links davon bleibt Platz für den Beutel in der Hand), in der Mitte der Würfelbecher, Würfel, Krüge und eine Kerze. Acht
    Plätze zum Würfeln (Bewegung „Zaehlen“), erst die Bank hinten (Gesicht zur Kamera)."""
    m = WUERFEL
    wi.tisch(netz, st, m, 2.6, 0.8)
    for s in (-1, 1):
        wi.bank(netz, st, (m.x - 1.3, m.y + s * 0.62, 0), (m.x + 1.3, m.y + s * 0.62, 0))
    hs, hv, hh = fg.ZAEHLEN["haufen"]
    sitz = fg.VOELKER["mensch"]["knie"] - 0.02
    nummer = 1
    for sy, blick in ((1, (0, -1)), (-1, (0, 1))):
        for x in WUERFEL_X:
            punkt = Vector((x, m.y + sy * 0.62, 0))
            di.muenzen(netz, st, drehen((hs, hv, sitz + hh), punkt, blick), r=0.045, h=0.022, saat=nummer)
            platz(plaetze, "Wache", punkt, blick, "Wuerfeln", "Zaehlen", nummer, sitz=SITZ)
            nummer += 1
    becher = m + Vector((0.15, 0.05, 0.78))
    drehkoerper(netz, st, becher, [(0.0, 0.04), (0.11, 0.05), (0.11, 0.045), (0.02, 0.04)], "Leder", n=8, innen=False)
    zufall = random.Random(7)
    for k in range(5):
        p = m + Vector((zufall.uniform(-0.5, 0.4), zufall.uniform(-0.12, 0.12), 0.78 + 0.011))
        netz.quader((0, 0, 0), (0.022, 0.022, 0.022), st["Wuerfel"], Matrix.Translation(p) @ Matrix.Rotation(zufall.uniform(0, 1.5), 4, "Z"))
    for k, (dx, dy) in enumerate(((-1.05, 0.1), (0.75, -0.08), (1.1, 0.12))):
        wi.krug(netz, st, (m.x + dx, m.y + dy, 0.78), henkel=(1 if k % 2 else -1, 0))
    di.kerze(netz, st, m + Vector((-0.3, 0.0, 0.78)), 0.12, anker=anker)


def waffenstaender(netz, st, M):
    """Bedienung_Einrichten_Waffenstaender an der Rückwand (örtlich +y in den Raum): Sockel mit Mulden für die Schäfte, drei
    Pfosten, oben eine Leiste mit Kerben (links die Hellebarden), rechts ein Brett mit Haken (die Armbrüste); zwei Spieße und zwei
    Schilde sind immer da."""
    holz, alt = "Holz", "Holz alt"
    si.q(netz, st, M, (-0.3, 0.2, 0.08), (1.5, 0.32, 0.16), alt)                           # Sockel unter den Hellebarden
    for x in (-1.0, 0.2, 1.0):
        si.q(netz, st, M, (x, 0.12, 1.05), (0.09, 0.09, 2.1), holz, ohne=("-y",))
    si.q(netz, st, M, (-0.4, 0.12, 1.85), (1.3, 0.12, 0.08), holz)                         # Leiste mit Kerben
    for k in range(5):
        si.q(netz, st, M, (-0.75 + 0.2 * k, 0.18, 1.85), (0.05, 0.02, 0.085), "Kohle")
    si.q(netz, st, M, (0.6, 0.03, 1.15), (0.75, 0.04, 1.5), alt, ohne=("-y",))             # Brett für die Armbrüste
    for z in (1.82, 1.12):                                                                  # Haken
        for x in (0.42, 0.78):
            netz.stab(M @ Vector((x, 0.05, z)), M @ Vector((x, 0.14, z + 0.03)), 0.025, 0.025, st[holz])
    for x in (-0.9, 0.88):                                                                  # zwei Spieße
        netz.stab(M @ Vector((x, 0.22 if x < 0 else 0.2, 0.0 if x > 0 else 0.12)), M @ Vector((x, 0.15, 2.3)), 0.035, 0.035, st[holz])
        netz.stab(M @ Vector((x, 0.15, 2.3)), M @ Vector((x, 0.145, 2.52)), 0.05, 0.012, st["Eisen"], quer=M.to_3x3() @ Vector((0, 1, 0)))
    for x in (-1.25, 1.25):                                                                 # Schilde an den Seiten
        si.q(netz, st, M, (x, 0.05, 1.25), (0.42, 0.04, 0.55), "Wappenrock")
        si.q(netz, st, M, (x, 0.072, 1.25), (0.08, 0.006, 0.48), "Stadtweiss")
        si.q(netz, st, M, (x, 0.08, 1.33), (0.36, 0.006, 0.07), "Stadtweiss")


def hellebarde(netz, st, M, x, unten=0.14, oben=2.25, neig=0.0):
    """Hellebarde im Ständer (örtlich wie waffenstaender): Schaft, oben Beil, Spitze und Haken."""
    e = st["Eisen"]
    a, b = M @ Vector((x, 0.22, unten)), M @ Vector((x + neig, 0.16, oben))
    netz.stab(a, b, 0.035, 0.035, st["Holz"])
    d = (b - a).normalized()
    q = M.to_3x3() @ Vector((1, 0, 0))
    vor = M.to_3x3() @ Vector((0, 1, 0))
    netz.stab(b, b + d * 0.24, 0.035, 0.012, e, quer=vor)                                   # Spitze
    netz.vieleck([b - d * 0.18 + q * 0.02, b - d * 0.02 + q * 0.02, b + d * 0.06 + q * 0.2, b - d * 0.24 + q * 0.17],
                 e, immer=True)                                                              # Beil
    netz.vieleck([b - d * 0.24 + q * 0.17, b + d * 0.06 + q * 0.2, b - d * 0.02 + q * 0.02, b - d * 0.18 + q * 0.02], e, immer=True)
    netz.stab(b - d * 0.1 - q * 0.02, b - d * 0.04 - q * 0.13, 0.03, 0.012, e, quer=vor)    # Haken
    netz.stab(b - d * 0.32, b - d * 0.02, 0.045, 0.045, e)                                  # Federn am Schaft


def werkzeug_wache(netz, st, M, stufe):
    """Gekauft am Waffenständer: Werkzeug1 – fünf Hellebarden in den Kerben; Werkzeug2 – zwei Armbrüste an den Haken des Bretts
    (Säule senkrecht, der Bogen oben quer, die Sehne gespannt), darunter ein Köcher mit Bolzen."""
    if stufe == 1:
        for k in range(5):
            hellebarde(netz, st, M, -0.75 + 0.2 * k, neig=0.015 * (k % 3 - 1))
        return
    for z in (1.86, 1.16):
        S = M @ Matrix.Translation((0.6, 0.13, z))
        netz.quader((0, 0, 0), (0.06, 0.05, 0.6), st["Holz"], S @ Matrix.Translation((0, 0, -0.32)))        # Säule
        netz.quader((0, 0, 0), (0.46, 0.03, 0.03), st["Eisen"], S @ Matrix.Translation((0, 0.01, -0.06)))    # Bogen (Stahl)
        for s_ in (-1, 1):                                                                 # Sehne, gespannt zur Nuss
            netz.stab(S @ Vector((s_ * 0.22, 0.03, -0.06)), S @ Vector((0.0, 0.035, -0.3)), 0.006, 0.006, st["Seil"])
        netz.quader((0, 0, 0), (0.08, 0.02, 0.06), st["Eisen"], S @ Matrix.Translation((0, 0.01, 0.0)))      # Steigbügel
        netz.quader((0, 0, 0), (0.03, 0.04, 0.12), st["Eisen"], S @ Matrix.Translation((0, 0.04, -0.5)))     # Abzug
    k = M @ Vector((0.6, 0.14, 0.0))                                                       # Köcher mit Bolzen
    drehkoerper(netz, st, k, [(0.0, 0.07), (0.42, 0.08)], "Leder", n=6, innen=False)
    for j in range(6):
        w = 2 * math.pi * j / 6
        p = k + Vector((0.035 * math.cos(w), 0.035 * math.sin(w), 0.42))
        netz.stab(p, p + Vector((0, 0, 0.08)), 0.012, 0.012, st["Holz alt"])
        netz.quader(p + Vector((0, 0, 0.09)), (0.02, 0.004, 0.03), st["Leinen"])


def putzbaenke(netz, st, plaetze):
    """Zwei Bänke vor dem Waffenständer, zwischen ihnen ein niedriger Bock mit Rüstungsteilen, Ölkännchen, Sand im Topf und Lappen.
    Acht Plätze zum Putzen (der Helm liegt auf dem Schoß: Teil „Lappen“ der Figur), erst die hintere Bank (Gesicht zur Kamera),
    von der Mitte nach außen."""
    x0, x1 = PUTZ_X[0] - 0.35, PUTZ_X[-1] + 0.35
    for y in PUTZ_Y:
        wi.bank(netz, st, (x0, y, 0), (x1, y, 0))
    ym = (PUTZ_Y[0] + PUTZ_Y[1]) / 2
    netz.quader(((x0 + x1) / 2, ym, 0.43), (x1 - x0 - 0.3, 0.3, 0.04), st["Holz alt"])     # Bock
    for x in (x0 + 0.3, x1 - 0.3):
        for s in (-1, 1):
            netz.stab((x, ym + s * 0.13, 0.0), (x, ym + s * 0.06, 0.41), 0.05, 0.05, st["Holz"])
    zufall = random.Random(4)
    for k in range(4):                                                                       # Helme, Armschienen, Kännchen
        x = x0 + 0.4 + 0.55 * k
        if k % 2 == 0:
            p = Vector((x, ym, 0.45))
            r, h = fg.PUTZEN["r"], fg.PUTZEN["h"]
            for (u0, r0), (u1, r1) in zip([(0.0, r), (0.6 * h, 0.85 * r), (h, 0.1 * r)], [(0.6 * h, 0.85 * r), (h, 0.1 * r), (h, 0.0)]):
                drehkoerper(netz, st, p + Vector((0, 0, u0)), [(0.0, r0), (u1 - u0, r1)], "Eisen", n=10)
            scheibe(netz, st, p, fg.PUTZEN["krempe"], 0.452, "Eisen", n=12)
        else:
            netz.stab((x - 0.12, ym + 0.05, 0.48), (x + 0.12, ym + 0.02, 0.48), 0.09, 0.06, st["Eisen"])
            drehkoerper(netz, st, Vector((x + 0.1, ym - 0.08, 0.45)), [(0.0, 0.035), (0.06, 0.04), (0.09, 0.015), (0.12, 0.012)], "Messing", n=6)
        netz.quader((x + 0.2, ym + zufall.uniform(-0.06, 0.06), 0.455), (0.14, 0.1, 0.01), st["Putzlappen"],
                    Matrix.Rotation(zufall.uniform(0, 1), 4, "Z"))
    drehkoerper(netz, st, Vector((x1 - 0.25, ym, 0.45)), [(0.0, 0.07), (0.12, 0.08), (0.12, 0.07), (0.02, 0.065)], "Krugton", n=8,
                innen=False)                                                                  # Topf mit Sand
    scheibe(netz, st, Vector((x1 - 0.25, ym, 0)), 0.07, 0.56, "Steinstaub", n=8)
    nummer = 1
    for y, blick in ((PUTZ_Y[0], (0, -1)), (PUTZ_Y[1], (0, 1))):
        for i in (1, 2, 0, 3):
            platz(plaetze, "Wache", (PUTZ_X[i], y), blick, "Putzen", "Putzen", nummer, sitz=SITZ)
            nummer += 1


def schleifbock(netz, st):
    """Der Tretschleifbock wie beim Dieb (der Stein ist ein eigenes Netz „Schleifstein“). Gibt (Schleifstein, Fußpunkt) zurück."""
    alt = di.SCHLEIF
    di.SCHLEIF = SCHLEIF
    try:
        return di.schleifbock(netz, st)
    finally:
        di.SCHLEIF = alt


def wetztisch(netz, st, plaetze, schleifbock_platz):
    """Der Wetztisch: niedriger Tisch mit Bänken (wie beim Dieb), vor jedem Platz ein Wetzstein auf einem Brettchen und eine Klinge
    (Schwert, Dolch, Spießeisen), dazu Ölkännchen und Lappen. Platz 1 des Schärfens ist der Schleifbock, 2–8 am Tisch."""
    m = WETZ
    wi.tisch(netz, st, m, 2.2, 0.7, hoehe=0.77)
    for s in (-1, 1):
        wi.bank(netz, st, (m.x - 1.1, m.y + s * 0.57, 0), (m.x + 1.1, m.y + s * 0.57, 0))
    liste = [((x, m.y + 0.57), (0, -1)) for x in (m.x - 0.85, m.x - 0.3, m.x + 0.25, m.x + 0.8)] + \
            [((x, m.y - 0.57), (0, 1)) for x in (m.x - 0.575, m.x - 0.025, m.x + 0.525)]
    vor = fg.ARBEITSHOEHEN["Tisch"][0]
    platz(plaetze, "Wache", schleifbock_platz, (1, 0), "Schaerfen", "Schleifen", 1)
    for nummer, (punkt, blick) in enumerate(liste, 2):
        stein = drehen((0.0, vor - 0.02, 0.77), (punkt[0], punkt[1], 0.0), blick)
        d = Vector((blick[0], blick[1], 0))
        q = Vector((-d.y, d.x, 0))
        netz.quader(stein + Vector((0, 0, 0.008)), (0.2, 0.2, 0.016), st["Holz alt"], Matrix.Rotation(math.atan2(d.y, d.x), 4, "Z"))
        netz.quader((0, 0, 0), (0.17, 0.05, 0.03), st["Wetzstein"],
                    Matrix.Translation(stein + Vector((0, 0, 0.031))) @ Matrix.Rotation(math.atan2(q.y, q.x), 4, "Z"))
        klinge = stein + q * 0.18 + d * 0.05 + Vector((0, 0, 0.003))
        lang = (0.5, 0.25, 0.32)[nummer % 3]
        netz.stab(klinge, klinge + q * 0.12, 0.03, 0.025, st["Leder"], quer=(0, 0, 1))
        netz.stab(klinge - q * 0.01 + d * 0.0, klinge - q * 0.01 + d * 0.0 + Vector((0, 0, 0.02)), 0.12 if lang > 0.3 else 0.06, 0.02,
                  st["Eisen"], quer=q)
        netz.stab(klinge - q * 0.02, klinge - q * (0.02 + lang) + d * 0.02, 0.035, 0.004, st["Eisen"], quer=(0, 0, 1))
        platz(plaetze, "Wache", punkt, blick, "Schaerfen", "Feinarbeit", nummer, sitz=SITZ)
    drehkoerper(netz, st, m + Vector((0.0, 0.0, 0.77)), [(0.0, 0.035), (0.06, 0.04), (0.09, 0.015), (0.12, 0.012)], "Messing", n=6)
    for k in range(2):
        netz.quader(m + Vector((-0.6 + 1.1 * k, 0.02, 0.775)), (0.18, 0.13, 0.01), st["Putzlappen"], Matrix.Rotation(0.4 + k, 4, "Z"))


def schreibpult(netz, st, pult):
    """Schreibpult wie im Skriptorium des Predigers (prediger_innen.schreibpult: Platte, Tintenhorn und Blatt bei
    figuren.SCHREIBEN), aus Eiche der Wache: Hocker, zwei Wangen als Bretter (4 cm), die schräge Platte mit Leiste, unten ein Bord
    mit Akten."""
    f = pult.platz
    drehkoerper(netz, st, f + Vector((0, 0, SITZ - 0.05)), [(0.0, 0.17), (0.05, 0.17)], "Holz", n=8)       # Hocker
    for k in range(3):
        w = 2 * math.pi * k / 3 + 0.4
        netz.stab(f + Vector((0.14 * math.cos(w), 0.14 * math.sin(w), 0)), f + Vector((0.1 * math.cos(w), 0.1 * math.sin(w), SITZ - 0.05)),
                  0.04, 0.04, st["Holz alt"])
    netz.quader((0, 0, -0.015), (0.72, 0.52, 0.03), st["Holz"], pult.matrix(0.0, 0.03, -0.028))
    netz.quader((0, -0.255, 0.01), (0.72, 0.03, 0.04), st["Holz alt"], pult.matrix(0.0, 0.03, -0.028))   # Leiste unten
    oben3 = Vector((0, 0, 1))
    for s_ in (-0.33, 0.33):                                                                 # Wangen
        o = pult.p(s_, 0.0, -pult.sitz)
        M = Matrix((pult.vor, pult.links, oben3)).transposed().to_4x4()
        M.translation = o
        umriss = []
        for q in (pult.p(s_, 0.3, -pult.sitz), pult.p(s_, 0.55, -pult.sitz), pult.platte(s_, 0.25, -0.05), pult.platte(s_, -0.2, -0.05)):
            d = q - o
            umriss.append((d.dot(pult.vor), d.z))
        di.flach(netz, st, umriss, M, 0.04, "Holz alt")
    netz.quader((0, 0, 0), (0.6, 0.3, 0.03), st["Holz"], pult.lok(0.0, 0.42, -pult.sitz + 0.35))           # Bord mit Akten
    for k, f_ in enumerate(("Leder", "Wappenrock", "Leder", "Kutte")):
        b_ = 0.06 + 0.02 * (k % 2)
        netz.quader((0, 0, 0), (b_, 0.22, 0.22 + 0.03 * k), st[f_], pult.lok(-0.22 + 0.12 * k, 0.42, -pult.sitz + 0.365 + 0.11 + 0.015 * k))
    hs, hv, hh = fg.SCHREIBEN["horn"]                                                         # Tintenhorn
    mund = pult.p(hs, hv, hh)
    drehkoerper(netz, st, mund - Vector((0, 0, 0.13)), [(0.0, 0.0), (0.03, 0.012), (0.1, 0.02), (0.13, 0.024)], "Horn", n=6)
    scheibe(netz, st, mund, 0.019, mund.z - 0.008, "Kohle", n=6)


def schreibpulte(netz, st, plaetze, anker):
    """Zwei Schreibpulte mit Blick zur Haustür: auf dem ersten das aufgeschlagene Wachbuch (prediger_innen.aufgeschlagen), auf dem
    zweiten Meldungen und ein Siegel. Zwei Plätze für das Wachbuch (Schreiben)."""
    for nummer, x in enumerate(SCHREIB_X, 1):
        pult = pr.Pult((x, SCHREIB_Y, 0), (0, -1))
        schreibpult(netz, st, pult)
        if nummer == 1:
            pr.aufgeschlagen(netz, st, pult, farben=True)
        if nummer == 2:
            M = pult.matrix(0.08, 0.04, -0.026)
            for k in range(3):
                netz.quader((0.02 * k - 0.02, 0.01 * k, 0.003 + 0.002 * k), (0.2, 0.27, 0.002), st["Papier"],
                            M @ Matrix.Rotation(0.15 * (k - 1), 4, "Z"))
            bs.achteck(netz, st, M @ Vector((0.16, 0.12, 0.004)), 0.02, 0.0, 0.012, "Siegelwachs", n=6)
        platz(plaetze, "Wache", (x, SCHREIB_Y), (0, -1), "Wachbuch", "Schreiben", nummer, sitz=SITZ)
    _ = anker


def wachplan(netz, st, M, anker):
    """Bedienung_Wache_Karte an der rechten Wand (örtlich +y in den Raum): eine Tafel mit dem Stadtplan (Pergament, Mauerring,
    Gassen, die Posten der Wache als rote Nadeln, die Rundgänge gestrichelt), daneben eine Schiefertafel mit den Schichten in
    Kreide und ein Schlüsselbrett; darunter ein Bord mit Stundenglas, Horn und Laterne."""
    si.q(netz, st, M, (0, 0.025, 1.6), (2.0, 0.05, 1.2), "Holz alt", ohne=("-y",))
    for z in (1.0, 2.2):
        si.q(netz, st, M, (0, 0.06, z), (2.08, 0.04, 0.06), "Holz", ohne=("-y",))
    P = M @ Matrix.Translation((-0.28, 0.052, 1.62)) @ Matrix.Rotation(math.radians(-90), 4, "X")    # Ebene x–y des Plans, +z in den Raum
    rand = [(-0.6, -0.48), (-0.1, -0.5), (0.45, -0.47), (0.62, -0.5), (0.6, 0.05), (0.63, 0.48), (0.1, 0.5), (-0.5, 0.46),
            (-0.62, 0.49), (-0.6, 0.0)]
    netz.vieleck([P @ Vector((x, y, 0)) for x, y in rand], st["Stadtplan"])
    for k in range(18):                                                                       # Mauerring
        w0, w1 = 2 * math.pi * k / 18, 2 * math.pi * (k + 1) / 18
        netz.stab(P @ Vector((0.42 * math.cos(w0), 0.36 * math.sin(w0), 0.002)), P @ Vector((0.42 * math.cos(w1), 0.36 * math.sin(w1), 0.002)),
                  0.014, 0.001, st["Plantinte"], quer=P.to_3x3() @ Vector((0, 0, 1)))
    zufall = random.Random(23)
    for k in range(10):                                                                       # Gassen
        a = Vector((zufall.uniform(-0.34, 0.34), zufall.uniform(-0.28, 0.28), 0.0025))
        e = a + Vector((zufall.uniform(-0.25, 0.25), zufall.uniform(-0.2, 0.2), 0))
        netz.stab(P @ a, P @ e, 0.008, 0.001, st["Plantinte"], quer=P.to_3x3() @ Vector((0, 0, 1)))
    posten = [(0.42 * math.cos(w), 0.36 * math.sin(w)) for w in (0.3, 1.5, 2.6, 3.9, 5.2)] + [(0.0, 0.0), (0.12, -0.15)]
    for k in range(len(posten) - 2):                                                          # Rundgang, gestrichelt
        a, b = Vector(posten[k] + (0.003,)), Vector(posten[k + 1] + (0.003,))
        for j in range(4):
            netz.stab(P @ a.lerp(b, j / 4 + 0.03), P @ a.lerp(b, j / 4 + 0.15), 0.01, 0.001, st["Planrot"],
                      quer=P.to_3x3() @ Vector((0, 0, 1)))
    vor = M.to_3x3() @ Vector((0, 1, 0))
    for x, y in posten:                                                                        # Nadeln mit roten Köpfen
        p = P @ Vector((x, y, 0.003))
        netz.stab(p, p + vor * 0.04, 0.005, 0.005, st["Eisen"])
        bs.achteck(netz, st, p + vor * 0.04 - Vector((0, 0, 0.008)), 0.01, 0.0, 0.016, "Planrot", n=5)
    si.q(netz, st, M, (0.6, 0.058, 1.75), (0.5, 0.012, 0.65), "Schiefertafel")                # Schichten in Kreide
    for zeile in range(5):
        for k in range(1 + (zeile * 3) % 5):
            si.q(netz, st, M, (0.42 + 0.035 * k, 0.065, 1.95 - 0.1 * zeile), (0.008, 0.002, 0.07), "Kreide")
        si.q(netz, st, M, (0.72, 0.065, 1.95 - 0.1 * zeile), (0.16, 0.002, 0.012), "Kreide")
    for k in range(5):                                                                         # Schlüsselbrett
        netz.stab(M @ Vector((0.42 + 0.09 * k, 0.05, 1.28)), M @ Vector((0.42 + 0.09 * k, 0.11, 1.3)), 0.015, 0.015, st["Holz"])
        if k != 2:
            netz.stab(M @ Vector((0.42 + 0.09 * k, 0.1, 1.28)), M @ Vector((0.42 + 0.09 * k, 0.1, 1.15)), 0.012, 0.004, st["Eisen"])
            bs.achteck(netz, st, M @ Vector((0.42 + 0.09 * k, 0.1, 1.29)), 0.018, -0.005, 0.005, "Eisen", n=6)
    si.q(netz, st, M, (0, 0.16, 0.95), (1.6, 0.26, 0.04), "Holz")                             # Bord
    for x in (-0.7, 0.7):
        netz.stab(M @ Vector((x, 0.02, 0.7)), M @ Vector((x, 0.26, 0.93)), 0.04, 0.04, st["Holz"])
    glas = M @ Vector((-0.4, 0.16, 0.97))                                                      # Stundenglas
    for z in (0.0, 0.2):
        scheibe(netz, st, glas + Vector((0, 0, z)), 0.06, glas.z + z + 0.015, "Holz", n=6)
    drehkoerper(netz, st, glas + Vector((0, 0, 0.015)), [(0.0, 0.045), (0.08, 0.012), (0.09, 0.012), (0.17, 0.045)], "Sanduhrglas", n=6)
    horn = M @ Vector((0.2, 0.16, 0.99))                                                       # Signalhorn
    netz.stab(horn, horn + M.to_3x3() @ Vector((0.3, 0.02, 0.05)), 0.07, 0.03, st["Horn"])
    p = M @ Vector((0.6, 0.16, 1.13))                                                          # Laterne auf dem Bord
    ti.laterne(netz, st, p, p.z + 0.19)
    anker.append(licht(p, (0, 0, -1), "Lampe"))


def stube_kram(netz, st, anker):
    """Was in der Wachstube sonst steht: ein Rüstungsständer in der Ecke hinten rechts, Mäntel an Haken neben der Haustür, ein
    Wasserfass mit Schöpfkelle."""
    ga.ruestungsstaender(netz, st, (WX1 - 0.55, WY1 - 0.55, 0), dreh=-45)
    M = si.rahmen(-3.4, WY0, 0)                                                                 # Haken mit Mänteln (Wappenrock)
    si.q(netz, st, M, (0, 0.03, 1.78), (0.6, 0.05, 0.08), "Holz", ohne=("-y",))
    for k, x in enumerate((-0.2, 0.2)):
        netz.stab(M @ Vector((x, 0.03, 1.76)), M @ Vector((x, 0.13, 1.79)), 0.025, 0.025, st["Holz"])
        di.flach(netz, st, [(-0.2, 1.78), (0.2, 1.78), (0.26, 1.2), (0.2, 0.75), (-0.22, 0.72), (-0.27, 1.2)],
                 M @ Matrix.Translation((x, 0.1, 0)) @ Matrix.Rotation(0.06 * (k * 2 - 1), 4, "Y"), 0.04,
                 ("Wappenrock", "Kutte")[k])
        if k == 0:
            di.flach(netz, st, [(-0.06, 1.7), (0.06, 1.7), (0.06, 0.85), (-0.06, 0.85)],
                     M @ Matrix.Translation((x, 0.125, 0)), 0.006, "Stadtweiss")
    fass = Vector((WX1 - 0.45, -0.4, 0))                                                        # Wasserfass
    bs.fass(netz, st, fass, r=0.3, h=0.85, reifen=(0.12, 0.66))
    bs.rund_oben(netz, st, fass, 0.28, 0.8, "Wasser")
    netz.stab(fass + Vector((0.0, 0.1, 0.82)), fass + Vector((-0.1, 0.42, 0.95)), 0.03, 0.03, st["Holz"])


def stube_stufen(netze, st):
    """Stufen (kumulativ, unter Raum_Werk): in Stufe 1 sind die Türen zur Übungshalle und zur Hauptmannsstube vermauert, in
    Stufe 2 nur noch die zur Hauptmannsstube; ab Stufe 2 hängt die Fahne der Stadtwache an der Wand, in Stufe 3 dazu ein zweiter
    Rüstungsständer."""
    k_ = ecken(WX0, WX1, WY0, WY1)
    for k, netz in enumerate(netze, 1):
        if k == 1:
            ai.vermauert(netz, st, *k_["rechts"], Oe(UEBUNG_Y, 0.0, UEBUNG_B, UEBUNG_H), rueck=0.06)
        if k < 3:
            ai.vermauert(netz, st, *k_["hinten"], Oe(HAUPT_X, 0.0, TUER_B, TUER_H), rueck=0.06)
        if k >= 2:
            fahne(netz, st, si.rahmen(WX0, -0.35, -90))
        if k == 3:
            ga.ruestungsstaender(netz, st, (WX1 - 0.55, 2.75, 0), dreh=-90)


def fahne(netz, st, M, z=2.85):
    """Die Fahne der Stadtwache an einer Stange schräg an der Wand: stadtrot mit weißem Balken und Schwalbenschwanz."""
    lang, hoch = 0.9, 0.6
    stange_a, stange_b = M @ Vector((0, 0.06, z - 0.6)), M @ Vector((0, 0.55, z + 0.05))
    netz.stab(stange_a, stange_b, 0.035, 0.035, st["Holz"])
    di.flach(netz, st, [(-hoch / 2, z), (hoch / 2, z), (hoch / 2, z - lang), (0.0, z - lang + 0.18), (-hoch / 2, z - lang)],
             M @ Matrix.Translation((0, 0.42, -0.02)), 0.01, "Stadtrot")
    di.flach(netz, st, [(-0.06, z - 0.02), (0.06, z - 0.02), (0.06, z - lang + 0.12), (-0.06, z - lang + 0.12)],
             M @ Matrix.Translation((0, 0.428, -0.02)), 0.004, "Stadtweiss")


# ─────────────────────────────────────────────────────────────────────────────
#  Waffenkammer
# ─────────────────────────────────────────────────────────────────────────────

def lager_huelle(lager, decke, st, anker):
    """Bruchstein mit Eckquadern, ein kleines vergittertes Fenster in der rechten Wand, Steinplatten, Balkendecke."""
    oe = {"vorne": [Oe(LAGER_X, 0.0, TUER_B, TUER_H)], "rechts": [], "hinten": [Oe(0.15, 2.5, 0.8, 0.42)], "links": []}
    mauerraum(lager, decke, st, LX0, LX1, LY0, LY1, H_LAGER, oe, ohne_aussen=("vorne", "links", "rechts"), ohne_laibung=("vorne",),
              saat=31, enden={"vorne": (False, False)})
    fenster_und_licht(lager, st, ecken(LX0, LX1, LY0, LY1), {"hinten": oe["hinten"]}, anker)
    pr.steinplatten(lager, st, LX0, LX1, LY0, LY1, saat=9, breite=0.5)


def waffengestell(netz, st):
    """Bedienung_Lager_Waffen: das große Gestell an der Rückwand – zwei Reihen Hellebarden und Spieße in Kerben, unten ein Fach mit
    Streitkolben und Äxten, oben auf einem Bord Helme."""
    M = si.rahmen((LX0 + LX1) / 2 - 0.1, LY1, 180)
    breite = 3.2
    for x in (-breite / 2, 0.0, breite / 2):
        si.q(netz, st, M, (x, 0.2, 1.2), (0.09, 0.4, 2.4), "Holz", ohne=("-y",))
    for z, tief in ((0.15, 0.36), (1.9, 0.14), (2.35, 0.4)):
        si.q(netz, st, M, (0, tief / 2 + 0.02, z), (breite, tief, 0.05), "Holz")
    for k in range(11):                                                                      # Hellebarden und Spieße
        x = -1.45 + 0.29 * k
        if k % 3 == 1:
            netz.stab(M @ Vector((x, 0.22, 0.18)), M @ Vector((x, 0.12, 2.25)), 0.035, 0.035, st["Holz"])
            netz.stab(M @ Vector((x, 0.12, 2.25)), M @ Vector((x, 0.115, 2.5)), 0.05, 0.012, st["Eisen"], quer=M.to_3x3() @ Vector((0, 1, 0)))
        else:
            hellebarde(netz, st, M, x, unten=0.18, oben=2.05)
    for k in range(4):                                                                       # Helme oben
        p = M @ Vector((-1.2 + 0.8 * k, 0.22, 2.375))
        r = 0.11
        drehkoerper(netz, st, p, [(0.0, r), (0.08, 0.88 * r), (0.13, 0.15 * r), (0.135, 0.0)], "Eisen", n=10)
        scheibe(netz, st, p, 0.16 if k % 2 else 0.12, p.z + 0.005, "Eisen", n=12)
    for k in range(3):                                                                       # Streitkolben und Äxte unten
        p = M @ Vector((-1.0 + 0.9 * k, 0.24, 0.18))
        netz.stab(p, p + M.to_3x3() @ Vector((0.35, 0.0, 0.0)) + Vector((0, 0, 0.02)), 0.03, 0.03, st["Holz"])
        bs.achteck(netz, st, p + M.to_3x3() @ Vector((0.38, 0.0, -0.04)), 0.05, 0.0, 0.1, "Eisen", n=6)


def ruestungen(netz, st):
    """Rüstungen auf Ständern an der linken Wand (Brustpanzer, Helm, Schild, wie im Hof der Kaserne), dazu Schilde an der rechten
    Wand und Pfeilkisten vorn rechts."""
    for y in (6.75, 7.85, 8.95):
        ga.ruestungsstaender(netz, st, (LX0 + 0.45, y, 0), dreh=90)
    for k, y in enumerate((5.4, 6.3, 7.2, 8.1, 9.0)):                                         # Schilde in Stadtfarben
        M = si.rahmen(LX1, y, 90)
        di.flach(netz, st, [(x * 1.1, 1.95 + z * 1.1) for x, z in bs.SCHILD], M @ Matrix.Translation((0, 0.03, 0)), 0.04,
                 "Wappenrock" if k % 2 == 0 else "Stadtweiss")
        di.flach(netz, st, [(-0.05, 2.38), (0.05, 2.38), (0.05, 1.55), (-0.05, 1.55)], M @ Matrix.Translation((0, 0.052, 0)), 0.004,
                 "Stadtweiss" if k % 2 == 0 else "Wappenrock")
        bs.achteck(netz, st, M @ Vector((0, 0.06, 1.98)), 0.05, 0.0, 0.01, "Eisen", n=8)
    for k, (x, y) in enumerate(((1.5, 4.95), (1.5, 5.45))):                                  # Pfeilkisten
        mitte = Vector((x, y, 0))
        netz.quader(mitte + Vector((0, 0, 0.18)), (0.5, 0.36, 0.36), st["Holz alt"])
        for sx in (-1, 1):
            netz.quader(mitte + Vector((sx * 0.22, 0, 0.18)), (0.07, 0.37, 0.37), st["Fachwerk"], ohne=("-z",))
        zufall = random.Random(k)
        for j in range(14):
            p = mitte + Vector((zufall.uniform(-0.17, 0.17), zufall.uniform(-0.12, 0.12), 0.3))
            kopf = p + Vector((zufall.uniform(-0.04, 0.04), zufall.uniform(-0.04, 0.04), 0.22))
            netz.stab(p, kopf, 0.012, 0.012, st["Holz alt"])
            netz.quader(kopf + Vector((0, 0, 0.01)), (0.025, 0.004, 0.04), st["Leinen"])


def kettenhemden(netz, st):
    """Gekauft: zwei Kettenhemden auf Ständern links vorn (Pfahl, Schulterholz, das Hemd als Tunika aus Ringgeflecht mit kurzen
    Ärmeln, darüber eine Kettenhaube)."""
    for y in (4.98, 5.9):
        M = Matrix.Translation((LX0 + 0.45, y, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
        netz.quader((0, 0, 0.04), (0.5, 0.5, 0.08), st["Holz alt"], M)
        netz.quader((0, 0, 0.75), (0.07, 0.07, 1.5), st["Holz alt"], M)
        netz.quader((0, 0, 1.42), (0.5, 0.06, 0.06), st["Holz"], M)
        di.flach(netz, st, [(-0.22, 1.42), (-0.26, 1.14), (-0.25, 0.68), (0.25, 0.68), (0.26, 1.14), (0.22, 1.42)], M, 0.18,
                 "Kettenhemd")                                                                # Leib
        for s in (-1, 1):                                                                     # kurze Ärmel
            di.flach(netz, st, [(s * 0.21, 1.4), (s * 0.4, 1.34), (s * 0.42, 1.12), (s * 0.25, 1.16)], M, 0.14, "Kettenhemd")
        drehkoerper(netz, st, M @ Vector((0, 0, 1.45)), [(0.0, 0.16), (0.1, 0.13), (0.22, 0.1), (0.3, 0.0)], "Kettenhemd", n=8)


def lager_regale(netz, st):
    """Gekauft: zwei Gestelle unter den Schilden an der rechten Wand – Fächer mit Helmen, Bündeln von Bolzen und Spießeisen."""
    for k, y in enumerate((6.5, 8.4)):
        si.regal(netz, st, si.rahmen(LX1, y, 90), breite=1.6, hoehe=1.3, tiefe=0.4, boeden=3, saat=81 + k)
        M = si.rahmen(LX1, y, 90)
        for j in range(3):
            p = M @ Vector((-0.5 + 0.5 * j, 0.2, 1.3))
            drehkoerper(netz, st, p, [(0.0, 0.1), (0.07, 0.09), (0.12, 0.02), (0.125, 0.0)], "Eisen", n=8)


# ─────────────────────────────────────────────────────────────────────────────
#  Arrestzelle
# ─────────────────────────────────────────────────────────────────────────────

def zelle_huelle(zelle, decke, st, anker):
    """Dunkler Bruchstein mit Eckquadern, ein kleines vergittertes Fenster hoch in der Rückwand, Steinplatten mit Stroh."""
    oe = {"vorne": [Oe(ZELLE_X, 0.0, TUER_B, TUER_H)], "rechts": [], "hinten": [Oe(-4.6, 2.0, 0.45, 0.35)], "links": []}
    mauerraum(zelle, decke, st, ZX0, ZX1, ZY0, ZY1, H_ZELLE, oe, ohne_aussen=("vorne", "rechts"), ohne_laibung=("vorne",),
              stoff="Bruchstein dunkel", saat=41, enden={"vorne": (False, False), "links": (True, False)})
    fenster_und_licht(zelle, st, ecken(ZX0, ZX1, ZY0, ZY1), {"hinten": oe["hinten"]}, anker)
    pr.steinplatten(zelle, st, ZX0, ZX1, ZY0, ZY1, saat=13, breite=0.45, stoffe=("Platte dunkel", "Platte", "Platte dunkel"))
    stroh(zelle, st, ZX0 + 0.2, ZX1 - 0.2, ZY0 + 0.2, ZY1 - 0.2, 70, saat=3)
    strohhaufen(zelle, st, (-2.65, 7.4, 0.0), 0.35, 0.45, 0.1, saat=5)


def steinbaenke(netz, st, plaetze):
    """Steinbänke an der Rück- und der linken Wand, darauf Stroh; sechs Plätze für Gefangene (Bewegung „Sitzen“), erst hinten."""
    tief = 0.42
    netz.quader(((-5.6 + -2.4) / 2 - 0.0, ZY1 - tief / 2, SITZ / 2), (3.2, tief, SITZ), st["Sandstein"], ohne=("+y", "-z"))
    netz.quader((ZX0 + tief / 2, (5.2 + ZY1 - tief) / 2, SITZ / 2), (tief, ZY1 - tief - 5.2, SITZ), st["Sandstein"], ohne=("-x", "-z"))
    for x0, x1, y0, y1 in ((-5.55, -2.45, ZY1 - tief + 0.03, ZY1 - 0.02), (ZX0 + 0.02, ZX0 + tief - 0.03, 5.25, ZY1 - tief)):
        netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, SITZ + 0.01), (x1 - x0, y1 - y0, 0.02), st["Puppenstroh"], ohne=("-z",))
        stroh(netz, st, x0, x1, y0, y1, 10, saat=int(x0 * 10), z=SITZ + 0.02)
    nummer = 1
    for x in (-3.7, -4.6, -2.8):
        platz(plaetze, "Gefangen", (x, ZY1 - 0.24), (0, -1), "Gefangen", "Sitzen", nummer, sitz=SITZ)
        nummer += 1
    for y in (7.75, 6.85, 5.95):
        platz(plaetze, "Gefangen", (ZX0 + 0.24, y), (1, 0), "Gefangen", "Sitzen", nummer, sitz=SITZ)
        nummer += 1


def zelle_kram(netz, st, anker):
    """Eimer in der Ecke vorn links, Ketten mit Schellen an der Rück- und der rechten Wand, ein Wasserkrug, Kreidestriche (die
    Tage) an der Wand, eine Laterne in einer vergitterten Nische über der Tür."""
    eimer = Vector((ZX0 + 0.35, ZY0 + 0.35, 0))
    drehkoerper(netz, st, eimer, [(0.0, 0.13), (0.32, 0.16), (0.32, 0.15), (0.02, 0.12)], "Holz alt", n=8, innen=False)
    for z in (0.06, 0.26):
        drehkoerper(netz, st, eimer + Vector((0, 0, z)), [(0.0, 0.13 + z * 0.1 + 0.004), (0.025, 0.135 + z * 0.1 + 0.004)], "Eisen", n=8)
    netz.stab(eimer + Vector((-0.15, 0, 0.32)), eimer + Vector((0.15, 0, 0.32)), 0.015, 0.015, st["Eisen"])
    for k, (M, x) in enumerate(((si.rahmen(-3.25, ZY1, 180), 0.0), (si.rahmen(ZX1, 7.4, 90), 0.0), (si.rahmen(-5.0, ZY1, 180), 0.0))):
        ring = M @ Vector((x, 0.05, 1.5 - 0.3 * (k % 2)))
        bs.achteck(netz, st, M @ Vector((x, 0.0, 1.5 - 0.3 * (k % 2) - 0.04)), 0.05, 0.0, 0.08, "Eisen", n=6)
        unten = M @ Vector((x + 0.15, 0.14, 0.5 + 0.2 * k))
        for j in range(7):                                                                   # Kette
            a = ring.lerp(unten, j / 7) + Vector((0, 0, -0.12 * math.sin(math.pi * j / 7)))
            b = ring.lerp(unten, (j + 1) / 7) + Vector((0, 0, -0.12 * math.sin(math.pi * (j + 1) / 7)))
            netz.stab(a, b, 0.025, 0.012, st["Eisen"], quer=(0, 0, 1) if j % 2 else M.to_3x3() @ Vector((0, 1, 0)))
        for s in (-1, 1):                                                                     # Schellen
            drehkoerper(netz, st, unten + Vector((0, 0, -0.03)) + M.to_3x3() @ Vector((s * 0.05, 0, 0)), [(0.0, 0.045), (0.04, 0.045)],
                        "Eisen", n=8)
    M = si.rahmen(-3.0, ZY1, 180)                                                              # Kreidestriche
    for gruppe in range(4):
        for k in range(5 if gruppe < 3 else 3):
            si.q(netz, st, M, (-0.25 + 0.16 * gruppe + 0.022 * k, 0.003, 1.75), (0.007, 0.004, 0.12), "Kreide")
        if gruppe < 3:
            netz.stab(M @ Vector((-0.26 + 0.16 * gruppe, 0.006, 1.7)), M @ Vector((-0.15 + 0.16 * gruppe, 0.006, 1.8)), 0.008, 0.003,
                      st["Kreide"])
    wi.krug(netz, st, (ZX0 + 0.2, 8.35, SITZ + 0.02), henkel=(1, 0), voll="Wasser")
    p = Vector((ZX1 - 0.6, ZY0 + 0.2, 2.3))                                                   # Laterne an der Wand neben der Tür
    netz.stab(Vector((ZX1 - 0.6, ZY0, 2.55)), Vector((ZX1 - 0.6, ZY0 + 0.25, 2.55)), 0.03, 0.03, st["Eisen"])
    ti.laterne(netz, st, p, 2.55)
    anker.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"))


def gittertuer_werk(netz, st):
    """Tuer_Werk_Zelle: die Gittertür in der Rückwand der Wachstube (in der Mitte der Mauer, prediger_innen.gittertuer)."""
    w = ia.Wand(*ecken(WX0, WX1, WY0, WY1)["hinten"])
    pr.gittertuer(netz, st, w, pr.u_von(w, ZELLE_X), TUER_B, TUER_H, 0.25)


def gittertuer_zelle(netz, st):
    """Tuer_Zelle_Werk: von der Zelle aus der Rahmen der Gittertür – Flacheisen um die Öffnung, ein Querriegel mit Schloss."""
    w = ia.Wand(*ecken(ZX0, ZX1, ZY0, ZY1)["vorne"])
    u = pr.u_von(w, ZELLE_X)
    for uu in (u - TUER_B / 2 + 0.03, u + TUER_B / 2 - 0.03):
        pr.balken_wand(netz, st, w, (uu, 0.0), (uu, TUER_H), 0.06, 0.08, "Eisen")
    for v in (0.04, TUER_H - 0.04):
        pr.balken_wand(netz, st, w, (u - TUER_B / 2, v), (u + TUER_B / 2, v), 0.06, 0.08, "Eisen")
    pr.balken_wand(netz, st, w, (u - TUER_B / 2 - 0.12, 1.05), (u + TUER_B / 2 + 0.12, 1.05), 0.08, 0.03, "Eisen", dicke=0.05)
    wq(netz, st, w, u - TUER_B / 2 - 0.2, u - TUER_B / 2 - 0.08, 0.95, 1.15, 0.0, 0.035, "Eisen")
    wq(netz, st, w, u + TUER_B / 2 + 0.08, u + TUER_B / 2 + 0.2, 0.95, 1.15, 0.0, 0.035, "Eisen")


# ─────────────────────────────────────────────────────────────────────────────
#  Übungshalle
# ─────────────────────────────────────────────────────────────────────────────

def uebung_huelle(netz, decke, st, anker):
    """Bruchstein mit Eckquadern, hohe vergitterte Fenster in Rück- und rechter Wand, Lehmboden, in der Mitte der Fechtboden aus
    Bohlen mit Kalkstrichen."""
    oe = {"links": [Oe(UEBUNG_Y, 0.0, UEBUNG_B, UEBUNG_H)], "hinten": [Oe(8.6, 2.2, 0.9, 1.2), Oe(11.6, 2.2, 0.9, 1.2)],
          "rechts": [Oe(0.0, 2.2, 0.9, 1.2)], "vorne": [Oe(11.0, 2.2, 0.9, 1.2)]}
    mauerraum(netz, decke, st, UX0, UX1, UY0, UY1, H_UEBUNG, oe, ohne_aussen=("links",), ohne_laibung=("links",), saat=51,
              enden={"links": (False, False)}, abdeckung=WAND)
    fenster_und_licht(netz, st, ecken(UX0, UX1, UY0, UY1), {k: v for k, v in oe.items() if k != "links"}, anker, stufe=2)
    fx0, fx1, fy0, fy1 = 7.6, UX1 - 0.4, -2.3, 2.3
    for r in ia.rechteck_minus((UX0, UX1, UY0, UY1), (fx0, fx1, fy0, fy1)):
        ia.lehmboden(netz, st, *r, saat=int(r[0] * 10 + r[2] * 3), flecken=3, halme=6)
    for k in range(round((fx1 - fx0) / 0.32)):                                                # Fechtboden: Bohlen entlang y
        x0 = fx0 + (fx1 - fx0) * k / round((fx1 - fx0) / 0.32)
        x1 = fx0 + (fx1 - fx0) * (k + 1) / round((fx1 - fx0) / 0.32)
        netz.quader(((x0 + x1) / 2, 0.0, 0.03), (x1 - x0 - 0.008, fy1 - fy0, 0.06), st[("Fechtboden", "Diele", "Fechtboden", "Diele hell")[k % 4]],
                    ohne=("-z",))
    netz.vieleck_nach([(fx0, fy0, 0.055), (fx1, fy0, 0.055), (fx1, fy1, 0.055), (fx0, fy1, 0.055)], (0, 0, 1), st["Dielenfuge"])
    for y in (-fg.FECHTEN["puppe"], fg.FECHTEN["puppe"]):                                      # Kalkstriche: die Fechtlinien
        netz.vieleck_nach([(fx0 + 0.15, y - 0.02, 0.065), (fx1 - 0.15, y - 0.02, 0.065), (fx1 - 0.15, y + 0.02, 0.065),
                           (fx0 + 0.15, y + 0.02, 0.065)], (0, 0, 1), st["Kreide"])


def strohpuppe(netz, st, fuss):
    """Strohpuppe zum Fechten (figuren.FECHTEN): Pfahl in einem Kreuzfuß, Querholz als Arme, Leib aus Stroh in Sackleinen mit
    Schnüren, Kopf aus Stroh, ein roter Lappen als Schild am linken Arm."""
    f = fg.FECHTEN
    p = Vector(fuss)
    netz.quader(p + Vector((0, 0, 0.06)), (0.8, 0.1, 0.12), st["Holz alt"], ohne=("-z",))      # Kreuzfuß
    netz.quader(p + Vector((0, 0, 0.055)), (0.1, 0.8, 0.11), st["Holz alt"], ohne=("-z",))
    netz.quader(p + Vector((0, 0, 0.95)), (0.09, 0.09, 1.9), st["Holz alt"], ohne=("-z",))
    netz.quader(p + Vector((0, 0, 1.42)), (1.0, 0.08, 0.08), st["Holz alt"])
    bs.achteck(netz, st, p, f["leib"], f["unten"], f["oben"], "Puppensack", n=8)
    for z in (f["unten"] + 0.12, (f["unten"] + f["oben"]) / 2, f["oben"] - 0.1):
        bs.achteck(netz, st, p, f["leib"] + 0.008, z - 0.012, z + 0.012, "Seil", n=8)
    bs.achteck(netz, st, p, 0.14, f["oben"] + 0.04, f["oben"] + 0.32, "Puppenstroh", n=8)
    for k in range(8):                                                                         # Halme stehen oben heraus
        w = 2 * math.pi * k / 8
        a = p + Vector((0.1 * math.cos(w), 0.1 * math.sin(w), f["oben"] + 0.3))
        netz.stab(a, a + Vector((0.05 * math.cos(w), 0.05 * math.sin(w), 0.08)), 0.02, 0.005, st["Puppenstroh"])
    netz.quader(p + Vector((0.47, 0.0, 1.22)), (0.07, 0.42, 0.5), st["Wappenrock"])


def puppen_und_plaetze(netz, st, plaetze):
    """Vier Strohpuppen in einer Reihe über den Fechtboden; je Puppe zwei Plätze (von vorn und von hinten), Fechten ab Stufe 2,
    erst die hintere Reihe (Gesicht zur Kamera), von der Mitte nach außen."""
    for x in PUPPE_X:
        strohpuppe(netz, st, (x, 0.0, 0.06))
    d = fg.FECHTEN["puppe"]
    nummer = 1
    for y, blick in ((d, (0, -1)), (-d, (0, 1))):
        for i in (1, 2, 0, 3):
            platz(plaetze, "Wache", (PUPPE_X[i], y), blick, "Fechten", "Fechten", nummer, z=0.06, stufe=2)
            nummer += 1


def zielscheibe(netz, st, fuss):
    """Zielscheibe wie im Hof der Kaserne (gardist.zielscheibe), aber mit dem Stroh der Puppen: runde Scheibe mit roten und weißen
    Ringen auf einem schrägen Gestell, Pfeile darin (Blick −y)."""
    t = Matrix.Translation(Vector(fuss))
    neig = t @ Matrix.Translation((0, 0, 1.1)) @ Matrix.Rotation(math.radians(-12), 4, "X")
    for s in (-1, 1):
        netz.stab(t @ Vector((s * 0.45, 0.0, 0)), t @ Vector((s * 0.35, 0.15, 1.6)), 0.07, 0.07, st["Holz alt"])
    netz.stab(t @ Vector((0, 0.7, 0)), t @ Vector((0, 0.12, 1.4)), 0.07, 0.07, st["Holz alt"])
    for r, y, stoff in ((0.42, -0.012, "Stadtrot"), (0.26, -0.016, "Stadtweiss"), (0.1, -0.02, "Stadtrot")):
        netz.vieleck_nach([neig @ Vector((r * math.cos(2 * math.pi * k / 12), y, r * math.sin(2 * math.pi * k / 12))) for k in range(12)],
                          neig.to_3x3() @ Vector((0, -1, 0)), st[stoff])
    profil = [(0.6 * math.cos(2 * math.pi * k / 12), 0.6 * math.sin(2 * math.pi * k / 12)) for k in range(12)]
    netz.prisma(profil, 0.0, 0.12, st["Puppenstroh"], matrix=neig @ Matrix.Rotation(math.radians(90), 4, "Z"))
    for dx, dz in ((0.12, 0.05), (-0.2, 0.18), (0.3, -0.25)):
        netz.stab(neig @ Vector((dx, -0.02, dz)), neig @ Vector((dx + 0.05, -0.55, dz + 0.03)), 0.02, 0.02, st["Holz"])


def uebung_kram(netz, st, anker):
    """Zielscheibe an der Rückwand, Waffenrechen mit Übungsschwertern und Stangen an der Vorderwand, Bänke an der linken Wand,
    Strohballen, zwei Laternen."""
    zielscheibe(netz, st, (12.4, UY1 - 0.8, 0))
    M = si.rahmen(10.3, UY0, 0)                                                                # Waffenrechen
    si.q(netz, st, M, (0, 0.12, 0.12), (2.0, 0.24, 0.08), "Holz alt")
    for x in (-0.95, 0.95):
        si.q(netz, st, M, (x, 0.08, 0.8), (0.08, 0.08, 1.6), "Holz", ohne=("-y",))
    si.q(netz, st, M, (0, 0.1, 1.45), (2.0, 0.08, 0.06), "Holz")
    for k in range(9):
        x = -0.8 + 0.2 * k
        if k % 3 == 2:                                                                          # Stangen
            netz.stab(M @ Vector((x, 0.14, 0.16)), M @ Vector((x, 0.07, 2.1)), 0.035, 0.035, st["Holz"])
        else:                                                                                    # Übungsschwerter, Spitze nach oben
            netz.stab(M @ Vector((x, 0.14, 0.16)), M @ Vector((x, 0.12, 0.35)), 0.032, 0.034, st["Leder"])
            netz.stab(M @ Vector((x, 0.12, 0.36)), M @ Vector((x, 0.1, 1.05)), 0.045, 0.018, st["Fechtboden"], quer=M.to_3x3() @ Vector((1, 0, 0)))
            netz.quader(M @ Vector((x, 0.125, 0.355)), (0.2, 0.035, 0.03), st["Holz alt"])
    for y0, y1 in ((-3.1, -1.0), (2.4, 3.2)):                                                   # Bänke an der linken Wand
        wi.bank(netz, st, (UX0 + 0.3, y0, 0), (UX0 + 0.3, y1, 0), tiefe=0.34)
    for k, (x, y) in enumerate(((13.0, -3.0), (12.35, -3.05), (13.05, -2.35))):                 # Strohballen
        netz.quader((x, y, 0.22), (0.6, 0.45, 0.44), st["Puppenstroh"])
        for s in (-0.12, 0.12):
            netz.quader((x + s, y, 0.22), (0.02, 0.46, 0.45), st["Seil"])
    for x in (9.1, 11.9):
        p = Vector((x, 0.0, 3.0))
        ti.laterne(netz, st, p, H_UEBUNG - 0.2)
        anker.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe", 2))


# ─────────────────────────────────────────────────────────────────────────────
#  Hauptmannsstube
# ─────────────────────────────────────────────────────────────────────────────

def haupt_huelle(netz, decke, st, anker):
    """Gekalkt, an den Ecken bricht der Bruchstein durch, unten eine Täfelung; ein vergittertes Fenster in der rechten Wand,
    Dielen, Balkendecke."""
    oe = {"vorne": [Oe(HAUPT_X, 0.0, TUER_B, TUER_H)], "rechts": [Oe(6.4, 1.0, 0.9, 1.2)], "hinten": [], "links": []}
    kanten = ecken(HX0, HX1, HY0, HY1)
    netz.alle = True
    for seite in ("vorne", "rechts", "hinten", "links"):
        si.wand(netz, st, *kanten[seite], H_HAUPT, oe[seite], stoff="Kalkputz", dicke=WAND, aussen=seite in ("rechts", "hinten"),
                laibung=seite != "vorne", enden=(False, True) if seite == "rechts" else (True, True))
    netz.alle = False
    for i, seite in enumerate(("vorne", "rechts", "hinten", "links")):
        ia.durchbruch(netz, st, *kanten[seite], H_HAUPT, oe[seite], saat=61 + 13 * i)
        pi.taefelung(netz, st, *kanten[seite], oe[seite], hoehe=1.1)
    fenster_und_licht(netz, st, kanten, {"rechts": oe["rechts"]}, anker, stufe=3)
    ia.balkendecke(decke, st, HX0, HX1, HY0, HY1, H_HAUPT, saat=67)
    wi.dielen(netz, st, HX0, HX1, HY0, HY1, 0.0, saat=17)


def schreibtisch(netz, st, plaetze, anker):
    """Der Schreibtisch des Hauptmanns: schwere Platte auf Wangen, darauf der Stadtplan auf Pergament (Mauerring, Gassen, die Posten
    als rote Nadeln), ein Lederbuch, Tintenfass mit Feder, Siegel, eine Kerze; dahinter der Lehnstuhl, Hauptmann_1 sitzt darauf
    (Bewegung „Feinarbeit“: die Hände auf dem Plan)."""
    m, l, b, h = HAUPT_TISCH, 1.6, 0.8, 0.77
    netz.quader(m + Vector((0, 0, h - 0.03)), (l, b, 0.06), st["Tischplatte"])
    for sx in (-1, 1):
        netz.quader(m + Vector((sx * (l / 2 - 0.1), 0, (h - 0.06) / 2)), (0.08, b - 0.1, h - 0.06), st["Thronholz"])
        netz.quader(m + Vector((sx * (l / 2 - 0.1), 0, 0.04)), (0.14, b, 0.08), st["Thronholz"])
    netz.quader(m + Vector((0, -b / 2 + 0.06, (h - 0.06) / 2 + 0.15)), (l - 0.25, 0.03, h - 0.36), st["Thronholz"])   # Blende vorn
    z = h + 0.002
    P = Matrix.Translation(m + Vector((-0.05, 0.02, z))) @ Matrix.Rotation(0.06, 4, "Z")
    rand = [(-0.45, -0.3), (0.0, -0.32), (0.44, -0.29), (0.47, 0.0), (0.44, 0.3), (-0.1, 0.31), (-0.46, 0.29), (-0.47, 0.0)]
    netz.vieleck_nach([P @ Vector((x, y, 0)) for x, y in rand], (0, 0, 1), st["Stadtplan"])
    for k in range(16):
        w0, w1 = 2 * math.pi * k / 16, 2 * math.pi * (k + 1) / 16
        netz.stab(P @ Vector((0.34 * math.cos(w0), 0.22 * math.sin(w0), 0.001)), P @ Vector((0.34 * math.cos(w1), 0.22 * math.sin(w1), 0.001)),
                  0.01, 0.001, st["Plantinte"], quer=(0, 0, 1))
    zufall = random.Random(29)
    for k in range(8):
        a = Vector((zufall.uniform(-0.28, 0.28), zufall.uniform(-0.18, 0.18), 0.0012))
        netz.stab(P @ a, P @ (a + Vector((zufall.uniform(-0.2, 0.2), zufall.uniform(-0.12, 0.12), 0))), 0.006, 0.001, st["Plantinte"],
                  quer=(0, 0, 1))
    for w in (0.4, 1.7, 3.0, 4.4, 5.6):
        p = P @ Vector((0.34 * math.cos(w), 0.22 * math.sin(w), 0.0))
        netz.stab(p, p + Vector((0, 0, 0.04)), 0.004, 0.004, st["Eisen"])
        bs.achteck(netz, st, p + Vector((0, 0, 0.04)), 0.008, 0.0, 0.012, "Planrot", n=5)
    buch = m + Vector((0.6, 0.12, z))
    netz.quader(buch + Vector((0, 0, 0.025)), (0.24, 0.32, 0.05), st["Leder"], Matrix.Rotation(0.2, 4, "Z"))
    fass = m + Vector((-0.62, 0.22, z))
    bs.achteck(netz, st, fass, 0.03, 0.0, 0.05, "Kohle")
    netz.stab(fass + Vector((0, 0, 0.04)), fass + Vector((0.04, -0.1, 0.22)), 0.012, 0.03, st["Leinen"])
    bs.achteck(netz, st, m + Vector((0.66, -0.25, z)), 0.025, 0.0, 0.06, "Messing", n=6)                  # Siegel
    di.kerze(netz, st, m + Vector((-0.68, -0.2, z)), 0.15, anker=anker, stufe=3)
    s = HAUPT_PLATZ                                                                                       # Lehnstuhl
    netz.quader(s + Vector((0, 0.04, SITZ - 0.03)), (0.52, 0.5, 0.06), st["Thronholz"])
    netz.quader(s + Vector((0, 0.04, SITZ + 0.005)), (0.44, 0.42, 0.02), st["Wappenrock"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader(s + Vector((sx * 0.23, 0.04 + sy * 0.21, (SITZ - 0.06) / 2)), (0.06, 0.06, SITZ - 0.06), st["Thronholz"])
        netz.quader(s + Vector((sx * 0.25, 0.04, SITZ + 0.22)), (0.05, 0.46, 0.05), st["Thronholz"])
        netz.quader(s + Vector((sx * 0.25, -0.17, SITZ + 0.1)), (0.05, 0.05, 0.2), st["Thronholz"])
    netz.quader(s + Vector((0, 0.27, SITZ + 0.5)), (0.52, 0.06, 1.0), st["Thronholz"])
    netz.quader(s + Vector((0, 0.235, SITZ + 0.55)), (0.4, 0.01, 0.6), st["Wappenrock"])
    o = bs.anker("Hauptmann_1", (s.x, s.y, 0.0), si.drehung_von((0, -1)))
    o["taetigkeit"], o["bewegung"], o["platz"], o["sitzhoehe"], o["stufe"] = "Befehligen", "Feinarbeit", 1, SITZ, 3
    plaetze.append(o)


def haupt_kram(netz, st, anker):
    """Wappenbanner hinter dem Stuhl, eine beschlagene Truhe an der linken Wand, ein Bord mit Akten und Rollen, Schwert und Schild an
    der Wand, ein Teppich unter dem Tisch, eine Laterne."""
    M = si.rahmen(HAUPT_TISCH.x, HY1, 180)                                                     # Wappenbanner
    breite, hoehe, z = 1.0, 1.9, 2.9
    netz.stab(M @ Vector((-breite / 2 - 0.1, 0.06, z + 0.04)), M @ Vector((breite / 2 + 0.1, 0.06, z + 0.04)), 0.04, 0.04, st["Holz"])
    di.flach(netz, st, [(-breite / 2, z), (breite / 2, z), (breite / 2, z - hoehe), (0.0, z - hoehe + 0.25), (-breite / 2, z - hoehe)],
             M @ Matrix.Translation((0, 0.06, 0)), 0.01, "Stadtrot", "Wappenrock")
    schild(netz, st, M @ Matrix.Translation((0, 0.03, z - 0.75)), g=0.75, rand="Gold", tief=0.0)
    pr.truhe(netz, st, si.rahmen(HX0, 7.0, -90), eisen=True)
    M = si.rahmen(HX0, 5.6, -90)                                                                # Bord mit Akten
    si.q(netz, st, M, (0, 0.17, 1.7), (1.0, 0.3, 0.03), "Holz")
    zufall = random.Random(31)
    x = -0.45
    while x < 0.42:
        b = zufall.uniform(0.05, 0.09)
        if zufall.random() < 0.3:
            netz.stab(M @ Vector((x + 0.05, 0.05, 1.75)), M @ Vector((x + 0.05, 0.3, 1.75)), 0.08, 0.08, st["Papier"])
            x += 0.12
            continue
        si.q(netz, st, M, (x + b / 2, 0.16, 1.715 + 0.13), (b, 0.24, 0.26), zufall.choice(("Leder", "Wappenrock", "Kutte")))
        x += b + 0.01
    M = si.rahmen(HX1, 8.1, 90)                                                                  # Schwert und Schild an der Wand
    schild(netz, st, M @ Matrix.Translation((0, 0.0, 1.8)), g=0.7, rand="Eisen", tief=0.0)
    netz.stab(M @ Vector((-0.5, 0.08, 1.35)), M @ Vector((0.5, 0.08, 2.25)), 0.045, 0.012, st["Eisen"], quer=M.to_3x3() @ Vector((0, 1, 0)))
    netz.stab(M @ Vector((-0.55, 0.09, 1.31)), M @ Vector((-0.46, 0.09, 1.39)), 0.03, 0.03, st["Leder"])
    netz.quader(HAUPT_TISCH + Vector((0, 0.15, 0.004)), (2.2, 1.7, 0.008), st["Teppich"])         # Teppich
    netz.quader(HAUPT_TISCH + Vector((0, 0.15, 0.0045)), (1.9, 1.4, 0.008), st["Teppich hell"], ohne=("-z",))
    p = Vector((HAUPT_TISCH.x, 6.0, 2.4))
    ti.laterne(netz, st, p, H_HAUPT - 0.26)
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
    raumnetze = {r: bs.Netz(f"Raum_{r}", alle=r != "Werk") for r in ("Werk", "Lager", "Zelle", "Uebung", "Hauptmann")}
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

    plaetze = []                  # Wache_<n>, Gefangen_<n>, Hauptmann_1
    # ── Wachstube ─────────────────────────────────────────────────────────────
    liste = []
    stube_huelle(werk, decken["Werk"], st, liste)
    flammen = kamin(werk, bs.Netz("Flammen", alle=True), st, liste)
    wuerfeltisch(werk, st, plaetze, liste)
    putzbaenke(werk, st, plaetze)
    stein, schleifer = schleifbock(werk, st)
    wetztisch(werk, st, plaetze, schleifer)
    schreibpulte(werk, st, plaetze, liste)
    pritschen(werk, st, plaetze)
    fertig += [(stein, "Werk"), (flammen, "Werk")]
    for p in (Vector((WUERFEL.x, WUERFEL.y, 2.2)), Vector((WETZ.x - 0.5, WETZ.y + 0.6, 2.25))):   # Laternen
        ti.laterne(werk, st, p, H_WERK - 0.26)
        liste.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"))
    M_st = si.rahmen(STAENDER_X, WY1, 180)
    waffenstaender(teil("Bedienung_Einrichten_Waffenstaender"), st, M_st)
    werkzeug_wache(teil("Einrichtung_Werkzeug1"), st, M_st, 1)
    werkzeug_wache(teil("Einrichtung_Werkzeug2"), st, M_st, 2)
    schild(werk, st, M_st @ Matrix.Translation((0, 0.0, 2.6)), g=0.62)                     # Stadtwappen über dem Ständer
    wachplan(teil("Bedienung_Wache_Karte"), st, si.rahmen(WX1, WACHPLAN_Y, 90), liste)
    stube_kram(werk, st, liste)
    alle_setzen(liste)
    si.pult(teil("Bedienung_Personal_Buch"), st, si.rahmen(-0.9, WY0 + 0.05, 0))
    M_haus = si.rahmen(HAUSTUER_X, WY0, 0)
    si.bretttuer(teil("Holztuer"), st, M_haus, HAUSTUER_B, HAUSTUER_H)
    si.eisentuer(teil("Einrichtung_Eisentuer"), st, M_haus, HAUSTUER_B, HAUSTUER_H)
    ti.hoftor(teil("Bedienung_Karren_Hoftor"), st, si.rahmen(HOFTOR_X, WY0, 0), HOFTOR_B, HOFTOR_H)
    hunde(teil("Einrichtung_Hunde"), st)
    si.loeschfass(teil("Einrichtung_Loeschfass"), st, Vector((WX0 + 0.45, -0.35, 0)))
    si.bretttuer(teil("Tuer_Werk_Lager"), st, si.rahmen(LAGER_X, WY1, 180), TUER_B, TUER_H, stoff="Holz")
    gittertuer_werk(teil("Tuer_Werk_Zelle"), st)
    si.bretttuer(teil("Tuer_Werk_Uebung"), st, si.rahmen(WX1, UEBUNG_Y, 90), UEBUNG_B, UEBUNG_H, stoff="Holz")
    si.bretttuer(teil("Tuer_Werk_Hauptmann"), st, si.rahmen(HAUPT_X, WY1, 180), TUER_B, TUER_H, stoff="Holz")

    # ── Stufen (kumulativ) ────────────────────────────────────────────────────
    stufen = [bs.Netz(f"Stufe{k}") for k in (1, 2, 3)]
    stube_stufen(stufen, st)
    stufen_obj = [s.objekt() for s in stufen]

    # ── Waffenkammer ──────────────────────────────────────────────────────────
    liste = []
    lager_huelle(raumnetze["Lager"], decken["Lager"], st, liste)
    waffengestell(teil("Bedienung_Lager_Waffen", "Lager"), st)
    ruestungen(raumnetze["Lager"], st)
    kettenhemden(teil("Einrichtung_Kettenhemd", "Lager"), st)
    lager_regale(teil("Einrichtung_Regale", "Lager"), st)
    si.bretttuer(teil("Tuer_Lager_Werk", "Lager"), st, si.rahmen(LAGER_X, LY0, 0), TUER_B, TUER_H, stoff="Holz")
    p = Vector(((LX0 + LX1) / 2, 7.2, 2.2))
    ti.laterne(raumnetze["Lager"], st, p, H_LAGER - 0.26)
    liste.append(licht(p - Vector((0, 0, 0.15)), (0, 0, -1), "Lampe"))
    alle_setzen(liste, "Lager")
    nummer = 1
    for y in (6.3, 7.1, 7.9, 8.7):
        for x in (-0.5, 0.85):
            setzen(bs.anker(f"Lager_{nummer}", (x, y, 0.0)), "Lager")
            nummer += 1

    # ── Arrestzelle ───────────────────────────────────────────────────────────
    liste = []
    zelle_huelle(raumnetze["Zelle"], decken["Zelle"], st, liste)
    steinbaenke(raumnetze["Zelle"], st, plaetze)
    zelle_kram(raumnetze["Zelle"], st, liste)
    gittertuer_zelle(teil("Tuer_Zelle_Werk", "Zelle"), st)
    alle_setzen(liste, "Zelle")

    # ── Übungshalle (ab Stufe 2) ──────────────────────────────────────────────
    liste = []
    uebung_huelle(raumnetze["Uebung"], decken["Uebung"], st, liste)
    puppen_und_plaetze(raumnetze["Uebung"], st, plaetze)
    uebung_kram(raumnetze["Uebung"], st, liste)
    si.bretttuer(teil("Tuer_Uebung_Werk", "Uebung"), st, si.rahmen(UX0, UEBUNG_Y, -90), UEBUNG_B, UEBUNG_H, stoff="Holz")
    alle_setzen(liste, "Uebung", stufe=2)

    # ── Hauptmannsstube (ab Stufe 3) ──────────────────────────────────────────
    liste = []
    haupt_huelle(raumnetze["Hauptmann"], decken["Hauptmann"], st, liste)
    schreibtisch(raumnetze["Hauptmann"], st, plaetze, liste)
    haupt_kram(raumnetze["Hauptmann"], st, liste)
    si.bretttuer(teil("Tuer_Hauptmann_Werk", "Hauptmann"), st, si.rahmen(HAUPT_X, HY0, 0), TUER_B, TUER_H, stoff="Holz")
    alle_setzen(liste, "Hauptmann", stufe=3)
    for o in plaetze:
        raum = "Zelle" if o.name.startswith("Gefangen_") else "Hauptmann" if o.name.startswith("Hauptmann_") else \
            "Uebung" if o["taetigkeit"] == "Fechten" else "Werk"
        setzen(o, raum)

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
        for r, k in RAUM_STUFE.items():
            if name in (f"Tuer_Werk_{r}", f"Tuer_{r}_Werk"):
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
    ti.animieren(di.stein_bewegung(stein) + flammen_bewegung(flammen), ARBEIT_BILDER)
    bpy.context.view_layer.update()
    return list(raeume.values()) + [o for liste in kinder.values() for o in liste]


def flammen_bewegung(o):
    """Die Flammen im Kamin flackern: Höhe und Breite schwanken in zwei Wellen (7 und 11 je Schleife), Ruhe bei Zeit 0."""
    def bewegung(bild):
        t = 2 * math.pi * bild / ARBEIT_BILDER
        h = 1 + 0.16 * math.sin(7 * t) + 0.08 * math.sin(11 * t + 0.5) - 0.08 * math.sin(0.5)
        b = 1 - 0.06 * math.sin(7 * t + 1.2) + 0.06 * math.sin(1.2)
        return None, None, (b, b, h)
    return [(o, bewegung)]


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

MARKEN = {"Wuerfeln": (0.95, 0.8, 0.15), "Putzen": (0.2, 0.45, 1.0), "Schaerfen": (0.3, 0.85, 0.3), "Wachbuch": (0.95, 0.25, 0.2),
          "Schlafen": (0.75, 0.75, 0.9), "Fechten": (1.0, 0.5, 0.1), "Gefangen": (0.55, 0.35, 0.2), "Befehligen": (0.6, 0.3, 1.0)}


def wache_marken(z, raeume):
    """Marken für die Plätze im Grundriss (Pfeil = Blickrichtung; Farbe je Tätigkeit)."""
    import bpy
    stoffe = {k: bs._hilfsstoff("_Marke_" + k, f) for k, f in MARKEN.items()}
    liste = []
    for o in list(bpy.context.scene.objects):
        if o.type != "EMPTY" or not o.name.startswith(("Wache_", "Gefangen_", "Hauptmann_")) or ia.raum_von(o) not in raeume:
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
    """Bilder nach docs/modelle/: gardist_innen-blicke.png (jeder Blickpunkt in Stufe 3 mit aller Einrichtung),
    gardist_innen-stufen.png (die Wachstube von oben je Stufe) und gardist_innen-grundriss.png (alle Räume von oben mit den
    Ankern: Würfeln gelb, Putzen blau, Schärfen grün, Wachbuch rot, Schlafen hellgrau, Fechten orange, Gefangene braun, Hauptmann
    lila, Stellplätze türkis, Blickpunkte gelb)."""
    import bpy
    szene, kam = ia.buehne(schnell, lampe=70, glut=220, fuell=[(0, 0, H_WERK - 0.4, 160, 1), ((LX0 + LX1) / 2, (LY0 + LY1) / 2, H_LAGER - 0.4, 60, 1),
                                                                  ((ZX0 + ZX1) / 2, (ZY0 + ZY1) / 2, H_ZELLE - 0.4, 40, 1),
                                                                  ((UX0 + UX1) / 2, 0, H_UEBUNG - 0.4, 160, 2),
                                                                  ((HX0 + HX1) / 2, (HY0 + HY1) / 2, H_HAUPT - 0.4, 70, 3)])
    szene.render.resolution_x, szene.render.resolution_y = 640, 360
    szene.render.resolution_percentage = 50 if schnell else 100
    szene.frame_set(1 + round(0.15 * ARBEIT_BILDER))
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
    for raeume, r, text in (({"Werk", "Lager", "Zelle", "Hauptmann"}, (WX0 - WAND, WX1 + WAND, WY0 - WAND, LY1 + WAND),
                             "Wachstube und die Räume dahinter, Stufe 3"),
                            ({"Uebung"}, (UX0 - WAND, UX1 + WAND, UY0 - WAND, UY1 + WAND), "Übungshalle (Stufe 2): Fechten orange")):
        von_oben(r, aufloesung=900 if len(raeume) > 1 else 760)
        liste = ia.marken(3.5, raeume) + wache_marken(3.5, raeume)
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
