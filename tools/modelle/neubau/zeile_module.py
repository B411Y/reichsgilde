"""Modulares Wohnhaussystem (Variante A, Nutzer 20.11.2026): die Bausteine der Haeuserzeilen je Volk und Stufe.

    blender -b --python tools/modelle/neubau/zeile_module.py -- --satz=mensch_s1 [--bilder=<ordner>] [--ohne-ao]

Schreibt
    game/assets/modelle/gebaeude/zeilen/zeile_<satz>.glb     alle Module des Satzes als eigene Objekte (Name = Modul), dazu Anker
                                                              „Tuer_<modul>“ (Haustuer, Blick zur Gasse)
    game/assets/texturen/neubau/zeile_<satz>_ao_atlas.png    gebackene Verdeckung, eine Zelle je Modul (ao_atlas.json, Gruppe zeile_<satz>)

Das Spiel (game/scripts/Stadtansicht.Zeilen.cs) setzt die Module nach der Nachbarregel zu Zeilen zusammen und verschmilzt jede Zeile
zu einem Netz. Konzept: C:\\Reichsgilde_Austausch\\wohnhaus_modular\\konzept.md (Kapitel 4) und docs/bereiche/darstellung.md.

Masse und Lage (alle Module eines Satzes passen auf das 2-m-Raster):
  * Modulrahmen wie die Neubauten: X entlang der Gasse (von der Gasse aus gesehen nach rechts), Y in die Tiefe (Gasse bei -Y),
    Z oben. Die Vorderkante (Flucht) des Erdgeschosses liegt bei y = 0, die Parzelle reicht von x = 0 bis x = Breite, die Tiefe ist
    fuer alle Koerper TIEFE (8 m). Fachwerk-Obergeschosse kragen KRAG (0,4 m) ueber die Gasse (Nutzer: nur bei Fachwerk).
  * Giebelstaendige Koerper (k_g4, k_g6, k_g8): Waende 0,25 m von der Parzellengrenze eingerueckt, das Dach reicht bis an die
    Grenze. Zwischen zwei Giebelhaeusern bleibt so die Traufgasse (2 x 0,25 m), vorn und hinten mit dem Modul „gasse“ geschlossen.
    Die Seitenwaende sind eigene Module: „seite_blind“ (Nachbar daneben) bzw. „seite_offen“ (freies Zeilenende, mit Fenstern).
  * Traufstaendige Koerper (k_t6, k_t8): Waende bis an die Grenze, keine Seitenwaende. Ist die Giebelseite frei oder hoeher als der
    Nachbar, kommt eine Kappe: „kappe_brand“ (Brandwand, ueber das Dach gezogen) bzw. „kappe_offen“ (Fachwerkgiebel mit Fenster
    und Ortgang, nur am freien Ende eines Fachwerksatzes).
  * Alle Seitenstuecke sind fuer die LINKE Seite gebaut; rechts spiegelt das Spiel sie (x -> Breite - x).
  * Eckhaus (eck): 8 x 8 m, Zeltdach, Schauseiten vorn und rechts (Ende einer Zeile vor dem Knick), links und hinten blinde Waende.
  * Keilstueck fuer schraege Knicke aus Mauerstuecken (mauer 1 m, pfeiler, tor 3 m): das Spiel stellt sie entlang der Sehne.
  * hinterhaus: Schuppen 4 x 4 m mit Pultdach (nur Kulisse, hinter tiefen Parzellen).
"""
import json
import math
import os
import random
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from netzbau import ROT, SCHWARZ                           # noqa: E402
from netzbau import Teil as _Teil                           # noqa: E402
from trimsheet import BAENDER_AUSSEN as BA                 # noqa: E402
import wohnhaus_mensch_s1_g1 as g1                         # noqa: E402  (Stoffe, UV2 und AO, Export, Anker)
import fachwerk_echt                                        # noqa: E402  (echtes Fachwerk, Nutzer 21.11.2026)
from holzlage import Holzlage, kasten as holzkasten        # noqa: E402  (eine Holzlage je Fachwerkwand)

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
ZIEL_MODELL = os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "zeilen")
EZ = Vector((0, 0, 1))


class Teil(_Teil):
    """Teil der Zeilenmodule: Bretter mit Seiten bekommen auch Stirnflaechen an beiden Enden (Pruefung der NAS 21.11.2026: an den
    offenen Enden sah man in die Hoelzer hinein – Rueckseiten)."""

    def vieleck(self, stoff, punkte, uvs, weg_von=None, zu=None, farbe=SCHWARZ, glatt=False):
        """Wie netzbau.Teil.vieleck, aber doppelte Ecken (unter 0,1 mm) fallen weg und die Sichtseite folgt der Flaechennormalen
        nach Newell (bei einer doppelten Ecke unter den ersten dreien drehte netzbau die Flaeche zufaellig – Rueckseiten)."""
        pts, uv = [], []
        for p, u in zip(punkte, uvs):
            p = Vector(p)
            if pts and (p - pts[-1]).length < 1e-4: continue
            pts.append(p); uv.append(u)
        while len(pts) > 1 and (pts[0] - pts[-1]).length < 1e-4:
            pts.pop(); uv.pop()
        if len(pts) < 3: return
        n = Vector()
        for i, a in enumerate(pts):
            b = pts[(i + 1) % len(pts)]
            n += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
        if n.length < 1e-10: return
        mitte = sum(pts, Vector()) / len(pts)
        drehen = False
        if weg_von is not None: drehen = n.dot(mitte - Vector(weg_von)) < 0
        if zu is not None: drehen = n.dot(Vector(zu) - mitte) < 0
        if drehen: pts, uv = pts[::-1], uv[::-1]
        self.flaechen.append((pts, list(uv), stoff, farbe, glatt))

    def brett(self, stoff, a, b, breite, n, band, tiefe=0.05, seiten=True, farbe=SCHWARZ, laengs="u"):
        super().brett(stoff, a, b, breite, n, band, tiefe=tiefe, seiten=seiten, farbe=farbe, laengs=laengs)
        if not seiten: return
        a, b, n = Vector(a), Vector(b), Vector(n).normalized()
        achse = (b - a).normalized()
        quer = n.cross(achse).normalized()
        for ende, nach in ((a, -1), (b, 1)):
            self.rechteck(stoff, ende - quer * breite / 2, quer, n, breite, tiefe, band, anteil=(0.0, 0.3),
                          weg_von=ende - achse * nach * 0.3, farbe=farbe)

# ── Saetze: Volk x Stufe ──────────────────────────────────────────────────────────────────────────────────────────────
# blatt: Trim-Sheet fuer die Kontrollbilder (im Spiel waehlt die Ebene des Haeuser-Arrays, Steinebenen.cs)
# geschosse: Hoehen der Geschosse (EG zuerst); eg/og: Wandart "fachwerk" | "putz" | "mauer" (Mauerwerk im Band mauer_band);
# werk_band: Band des Werksteins fuer Eckquader (None: keine); dach: "stroh" (Stufen 1,55 m, Band „stroh“), "schindel" (Reihen 0,74 m,
# Band „stroh“, duenn), "ziegel" (Lagen 0,25 m, Band „stroh_franse“; Ziegel bzw. Schiefer je Blatt); treppe: Treppengiebel vorn an den
# Giebelhaeusern; erker: Eckerker am Eckhaus (Nutzer: nur Eckhaus, ab Stufe 2); fb: Fensterbreite; putz_lage: Lagenhoehe im Band
# „putz“ (None: eine Lage je Geschoss wie im ersten Stand mensch_s1). Masse der Neubau-Haeuser als Vorbild, Hoehen Annahme (Konzept 4.7).
SAETZE = {
    "mensch_s1": dict(blatt="trim", eg="fachwerk", og="fachwerk", dach="stroh", geschosse=(3.0, 2.6), neig_g=50, neig_t=45,
                      mauer_band="bruchstein", werk_band=None, erker=False, treppe=False, fb=0.75, putz_lage=None),
    "mensch_s2": dict(blatt="trim_s2", eg="mauer", og="fachwerk", dach="ziegel", geschosse=(3.4, 3.0), neig_g=52, neig_t=45,
                      mauer_band="bruchstein", werk_band=None, erker=True, treppe=False, fb=0.8, putz_lage=1.6),
    # mensch_s3: Nutzer 21.11.2026 „S3 B“, Obergeschosse Fachwerk mit Steingefachen (Vorbild Alsfeld); Eintrag endet auf „),“ (ZeilenplanTests)
    "mensch_s3": dict(blatt="trim_s3", eg="mauer", og="fachwerk", dach="ziegel", geschosse=(3.4, 2.5, 2.5), neig_g=52, neig_t=45,
                      mauer_band="bruchstein", werk_band="putz", erker=True, treppe=False, fb=0.8, putz_lage=1.6,
                      gefach="stein"),
    "zwerg_s1": dict(blatt="trim_zwerg", eg="mauer", og="mauer", dach="schindel", geschosse=(2.8, 2.4), neig_g=45, neig_t=40,
                     mauer_band="putz", werk_band="bruchstein", erker=False, treppe=False, fb=0.8, putz_lage=1.6),
    "zwerg_s2": dict(blatt="trim_s2_zwerg", eg="mauer", og="fachwerk", dach="ziegel", geschosse=(3.0, 2.6), neig_g=48, neig_t=42,
                     mauer_band="putz", werk_band="bruchstein", erker=True, treppe=False, fb=0.8, putz_lage=1.6),
    "zwerg_s3": dict(blatt="trim_s3_zwerg", eg="mauer", og="mauer", dach="ziegel", geschosse=(3.0, 2.6, 2.4), neig_g=48, neig_t=42,
                     mauer_band="putz", werk_band="bruchstein", erker=True, treppe=True, fb=0.8, putz_lage=1.6, erker_quader=True),
    "elb_s1": dict(blatt="trim_elb", eg="putz", og="putz", dach="schindel", geschosse=(3.2, 2.8), neig_g=58, neig_t=52,
                   mauer_band="bruchstein", werk_band="bruchstein", erker=False, treppe=False, fb=0.6, putz_lage=1.6),
    "elb_s2": dict(blatt="trim_s2_elb", eg="putz", og="fachwerk", dach="ziegel", geschosse=(3.4, 3.0), neig_g=58, neig_t=52,
                   mauer_band="bruchstein", werk_band="bruchstein", erker=True, treppe=False, fb=0.6, putz_lage=1.6),
    "elb_s3": dict(blatt="trim_s3_elb", eg="mauer", og="mauer", dach="ziegel", geschosse=(3.4, 2.5, 2.5), neig_g=58, neig_t=52,
                   mauer_band="bruchstein", werk_band="putz", erker=True, treppe=False, fb=0.6, putz_lage=1.6, erker_band="putz"),
    "halbling_s1": dict(blatt="trim_halbling", eg="fachwerk", og="fachwerk", dach="stroh", geschosse=(3.0, 1.0), neig_g=48, neig_t=45,
                        mauer_band="bruchstein", werk_band=None, erker=False, treppe=False, fb=0.7, putz_lage=1.6),
    "halbling_s2": dict(blatt="trim_s2_halbling", eg="mauer", og="fachwerk", dach="ziegel", geschosse=(3.0, 2.6), neig_g=50, neig_t=45,
                        mauer_band="bruchstein", werk_band=None, erker=True, treppe=False, fb=0.7, putz_lage=1.6),
    "halbling_s3": dict(blatt="trim_s3_halbling", eg="mauer", og="mauer", dach="ziegel", geschosse=(3.2, 2.8), neig_g=50, neig_t=45,
                        mauer_band="bruchstein", werk_band="putz", erker=True, treppe=True, fb=0.7, putz_lage=1.6,
                        first_v=(0.14, 0.17), grat_band="stroh_franse", erker_quader=True),
}
BANDLAGE = {"bruchstein": 0.4, "putz": 1.6}      # natuerliche Hoehe eines Bandes in Metern (Bandhoehe x Breite des Bands / 2048)
MODULE = ["k_g4", "k_g6", "k_g8", "k_t6", "k_t8", "seite_blind", "seite_offen", "kappe_brand", "kappe_offen", "gasse", "eck",
          "mauer", "pfeiler", "tor", "hinterhaus"]
# Lochbilder (Nutzer 21.11.2026: weniger Fenster – Glas war teuer –, weniger Symmetrie, mehr Abwechslung): je Giebelkoerper drei
# Varianten, Traufseite und Eckhaus je zwei; angehaengt, die bisherigen Module bleiben unveraendert
VARIANTEN = ["k_g4_a", "k_g4_b", "k_g4_c", "k_g6_a", "k_g6_b", "k_g6_c", "k_g8_a", "k_g8_b", "k_g8_c",
             "seite_offen_a", "seite_offen_b", "eck_a", "eck_b"]
MODULE = MODULE + VARIANTEN
FENSTER_EG, FENSTER_OG = 0.50, 0.65   # Wahrscheinlichkeit je Achse fuer ein Fenster
# Echtes Fachwerk (Nutzer 21.11.2026; Recherche C:\Reichsgilde_Austausch\fachwerk\recherche.md): Probe nur am Modul k_g6 der Menschen
# Stufe 2; nach der Abnahme alle Fachwerk-Module von Menschen und Halblingen
# Echtes Fachwerk (Nutzer 21.11.2026): je Satz die Module („alle“ = jedes Modul mit Fachwerkwand); Probe abgenommen 21.11.2026
FACHWERK_ECHT = {"mensch_s1": "alle", "mensch_s2": "alle", "mensch_s3": "alle", "halbling_s1": "alle", "halbling_s2": "alle"}


def gefachflaeche(teil, ebene, poly, putz_lage, loecher, u_saat):
    """Wandflaeche hinter dem Fachwerk: Lehmputz, bei Steingefachen (gefach="stein", Mensch S3) Bruchstein im Mauerband."""
    if S.get("gefach") == "stein":
        band = S["mauer_band"]
        lagenflaeche(teil, "Bruchstein", ebene, poly, band, BANDLAGE[band], loecher, u_saat=u_saat)
    else:
        lagenflaeche(teil, "Lehmputz", ebene, poly, "putz", putz_lage, loecher, u_saat=u_saat)


def echt_fuer(modul):
    v = FACHWERK_ECHT.get(S["satz"])
    return v == "alle" or modul in (v or ())
STAENDER_ABSTAND = 1.3    # groesster Abstand zweier Staender im echten Fachwerk (Annahme, recherche.md)
GEFACH_MINDESTENS, GEFACH_HOECHSTENS = 0.30, 1.30   # lichte Gefachbreite (Pruefliste der Bildpruefung, Nutzer 21.11.2026)
# fensterlose Achse (Nutzer 21.11.2026 „immer im Zufall, mal dies, mal das“): OG 40 % leer, 30 % Luke, 30 % geschlossene Laeden;
# EG 70 % leer, 30 % geschlossene Laeden
LUKE_OG, LADEN_OG, LADEN_EG = 0.30, 0.30, 0.30

TIEFE = 8.0
KRAG = 0.40            # Auskragung der Fachwerk-Obergeschosse (Nutzer: 0,4 m, nur Fachwerk)
EIN = 0.25             # Einrueckung der Giebelhaus-Waende (halbe Traufgasse)
UEBER = 0.38           # Dachueberstand an der Traufe der Traufhaeuser
ORTGANG = 0.30         # Dachueberstand am Giebel der Giebelhaeuser
Z_SOCKEL = 0.45
BW = 0.17              # Staenderbreite
RIEGEL = 0.14
MAUER_HOCH = 2.4       # Hofmauer des Keilstuecks
S = {}                 # Werte des gewaehlten Satzes (main)


def stil(satz):
    S.clear(); S.update(SAETZE[satz]); S["satz"] = satz
    S["krag"] = KRAG if S["og"] == "fachwerk" else 0.0
    S["stufe"] = int(satz.rsplit("_s", 1)[1])
    S["z_og"] = S["geschosse"][0]
    S["z_traufe"] = sum(S["geschosse"])
    S["tg"], S["tt"] = math.tan(math.radians(S["neig_g"])), math.tan(math.radians(S["neig_t"]))
    S["dick"] = {"stroh": 0.28, "schindel": 0.10, "ziegel": 0.12}[S["dach"]]


def stockwerke():
    """Die Geschosse: Liste (z0, z1, Wandart, ist_eg)."""
    aus, z = [], 0.0
    for i, h in enumerate(S["geschosse"]):
        aus.append((z, z + h, S["eg"] if i == 0 else S["og"], i == 0)); z += h
    return aus


def fenster_hoehen(z0, z1, eg):
    """Bruestung und Sturz der Fenster eines Geschosses (wie die Neubauten: EG 1,10/2,10 bei 3 m, OG 0,85/1,95 ueber dem Boden bei 2,6 m;
    ein niedriger Kniestock bekommt kleine Fenster)."""
    h = z1 - z0
    if h < 1.6: return z0 + 0.2, z1 - 0.2
    return (z0 + 0.37 * h, z0 + 0.70 * h) if eg else (z0 + 0.33 * h, z0 + 0.75 * h)


def wandseite(teil, eb_eg, eb_og, b_eg, b_og, anker, modul, n_eg, n_og, tuer=None, blind=False, breite=None, sockel=True, traufe=0.0):
    """Alle Geschosse einer Wand: EG in der Ebene eb_eg (Breite b_eg), die Obergeschosse in eb_og (b_og, auskragend). n_eg/n_og:
    Fensterachsen, tuer: None, "links" oder "rechts" (die Tuer nimmt eine weitere Achse im EG). blind: ohne Oeffnungen, Fachwerk
    wird Putz (die Wand liegt am Nachbarn). traufe: so weit reicht die Wand des obersten Geschosses ueber die Wandkrone in die Deckung
    (Traufwaende; die Dachunterseite geht genau durch die Wandkrone, ohne Uebergriff gingen flache Strahlen durch die Fuge)."""
    fb = breite or S["fb"]
    alle = stockwerke()
    for nr, (z0, z1, art, eg) in enumerate(alle):
        eb, b = (eb_eg, b_eg) if eg else (eb_og, b_og)
        if blind:
            art = "putz" if art == "fachwerk" else art
            oeff = []
            alt_ohne = S.get("ohne_quader"); S["ohne_quader"] = True
        else:
            fa, fz = fenster_hoehen(z0, z1, eg)
            n = n_eg if eg else n_og
            schmal = z1 - z0 < 1.6
            plaetze = achsen(b, n + (1 if (eg and tuer) else 0), 0.5 if schmal else fb) if n + (1 if (eg and tuer) else 0) > 0 else []
            oeff = []
            wuerfel = S.get("variante")
            if wuerfel is not None and not schmal:
                oeff = lochbild(wuerfel, b, plaetze, fa, fz, z1, eg, tuer)
            else:
                for i, (a0, a1) in enumerate(plaetze):
                    if eg and tuer and i == (0 if tuer == "links" else len(plaetze) - 1):
                        m = (a0 + a1) / 2
                        oeff.append((m - 0.48, m + 0.48, 0.0, min(2.15, z1 - 0.45), "tuer"))
                    else:
                        oeff.append((a0, a1, fa, fz, "fenster"))
        geschoss(teil, eb, b, z0, z1, oeff, art, anker, modul, sockel=eg and sockel, hoch=traufe if nr == len(alle) - 1 else 0.0)
        if blind: S["ohne_quader"] = alt_ohne


def lochbild(wuerfel, b, plaetze, fa, fz, z1, eg, tuer):
    """Lochbild einer Variante: je Achse ein Fenster mit FENSTER_EG bzw. FENSTER_OG, sonst im OG mit LUKE_OG eine kleine Luke,
    mindestens ein Fenster je Geschoss; die Tuer bleibt. Kleinere Oeffnungen (80 % breit, 85 % hoch), Achsen bis 0,15 m versetzt."""
    tuer_i = (0 if tuer == "links" else len(plaetze) - 1) if (eg and tuer) else -1
    mitten = []
    for i, (a0, a1) in enumerate(plaetze):
        m = (a0 + a1) / 2 + (wuerfel.uniform(-0.15, 0.15) if i != tuer_i else 0.0)
        mitten.append(m)
    halb = (plaetze[0][1] - plaetze[0][0]) / 2 * 0.8 if plaetze else 0.3
    h = (fz - fa) * 0.85
    fa2 = fa + (fz - fa - h) * 0.4
    oeff, fenster_da = [], []
    for i, m in enumerate(mitten):
        if i == tuer_i:
            oeff.append((m - 0.48, m + 0.48, 0.0, min(2.15, z1 - 0.45), "tuer")); continue
        if wuerfel.random() < (FENSTER_EG if eg else FENSTER_OG):
            fenster_da.append(len(oeff))
            oeff.append((m - halb, m + halb, fa2, fa2 + h, "fenster"))
        else:
            r = wuerfel.random()
            if not eg and r < LUKE_OG:
                oeff.append((m - 0.2, m + 0.2, fz - 0.55, fz - 0.1, "fenster"))       # kleine Luke unter dem Sturz
            elif r >= 1.0 - (LADEN_EG if eg else LADEN_OG):
                oeff.append((m - halb, m + halb, fa2, fa2 + h, "laden"))              # geschlossene Laeden in Fenstergroesse
    if not fenster_da:            # mindestens ein Fenster je Geschoss
        frei = [i for i in range(len(mitten)) if i != tuer_i]
        if frei:
            i = frei[wuerfel.randrange(len(frei))]
            m = mitten[i]
            oeff = [o for o in oeff if not (o[4] != "tuer" and abs((o[0] + o[1]) / 2 - m) < 1e-6)]
            oeff.append((m - halb, m + halb, fa2, fa2 + h, "fenster"))
    # in der Wand bleiben (Abstand zum Rand mindestens ein Staender und 0,1 m)
    rand = BW + 0.1
    aus = []
    for a0, a1, t0, t1, art in sorted(oeff):
        if a0 < rand: a1 += rand - a0; a0 = rand
        if a1 > b - rand: a0 -= a1 - (b - rand); a1 = b - rand
        aus.append((a0, a1, t0, t1, art))
    return aus


def mit_variante(modul, bau):
    """Baut ein Modul mit dem Wuerfel seiner Variante (fester Startwert aus dem Namen)."""
    S["variante"] = random.Random(sum(ord(c) * (i + 1) for i, c in enumerate(modul)))
    try:
        return bau()
    finally:
        S["variante"] = None


# ── Vieleck-Werkzeuge (konvexe Vielecke in einer Ebene: (s, t)-Koordinaten) ───────────────────────────────────────────

def schneide(poly, achse, wert, oben):
    """Sutherland-Hodgman: behaelt die Seite achse >= wert (oben) bzw. <= wert eines konvexen Vielecks aus (s, t)-Paaren."""
    if not poly: return []
    drin = (lambda p: p[achse] >= wert - 1e-9) if oben else (lambda p: p[achse] <= wert + 1e-9)
    aus = []
    for i, p in enumerate(poly):
        q = poly[(i + 1) % len(poly)]
        if drin(p): aus.append(p)
        if drin(p) != drin(q):
            k = (wert - p[achse]) / (q[achse] - p[achse])
            aus.append((p[0] + k * (q[0] - p[0]), p[1] + k * (q[1] - p[1])))
    sauber = []
    for p in aus:
        if not sauber or abs(p[0] - sauber[-1][0]) + abs(p[1] - sauber[-1][1]) > 1e-7: sauber.append(p)
    if len(sauber) > 1 and abs(sauber[0][0] - sauber[-1][0]) + abs(sauber[0][1] - sauber[-1][1]) < 1e-7: sauber.pop()
    return sauber if len(sauber) >= 3 else []


def ausschnitt(poly, s0=None, s1=None, t0=None, t1=None):
    if s0 is not None: poly = schneide(poly, 0, s0, True)
    if s1 is not None: poly = schneide(poly, 0, s1, False)
    if t0 is not None: poly = schneide(poly, 1, t0, True)
    if t1 is not None: poly = schneide(poly, 1, t1, False)
    return poly


def flaeche_teilen(poly):
    """Konvexes Vieleck in Stuecke zu hoechstens vier Ecken (Faecher), die vieleck() nimmt."""
    stuecke, i = [], 1
    while i < len(poly) - 1:
        stueck = [poly[0]] + poly[i:i + 3]
        stuecke.append(stueck); i += 2
    return [s for s in stuecke if len(s) >= 3]


def flaeche_mit_loechern(poly, loecher):
    """Zerlegt ein konvexes Vieleck mit rechteckigen Loechern (s0, s1, t0, t1) in konvexe Stuecke (Streifen)."""
    ts = sorted({p[1] for p in poly} | {l[2] for l in loecher} | {l[3] for l in loecher})
    stuecke = []
    for ta, tb in zip(ts[:-1], ts[1:]):
        if tb - ta < 1e-6: continue
        streifen = ausschnitt(poly, t0=ta, t1=tb)
        if not streifen: continue
        tm = (ta + tb) / 2
        aktiv = sorted((l for l in loecher if l[2] < tm < l[3]), key=lambda l: l[0])
        s_von = None
        for l in aktiv:
            stueck = ausschnitt(streifen, s0=s_von, s1=l[0])
            if stueck: stuecke.append(stueck)
            s_von = l[1] if s_von is None else max(s_von, l[1])
        stueck = ausschnitt(streifen, s0=s_von)
        if stueck: stuecke.append(stueck)
    return stuecke


class Ebene:
    """Senkrechte (oder beliebige) Ebene: Punkt o, Richtung eu (s) und ev (t); innen: Punkt hinter der Sichtseite."""

    def __init__(self, o, eu, ev, innen):
        self.o, self.eu, self.ev, self.innen = Vector(o), Vector(eu).normalized(), Vector(ev).normalized(), Vector(innen)

    def p(self, s, t, raus=0.0):
        n = self.n()
        return self.o + self.eu * s + self.ev * t + n * raus

    def n(self):
        n = self.eu.cross(self.ev).normalized()
        mitte = self.o + self.eu + self.ev
        return n if n.dot(mitte - self.innen) > 0 else -n


def lagenflaeche(teil, stoff, ebene, poly, band, lage, loecher=(), raus=0.0, u_saat=None, farbe=SCHWARZ):
    """Flaeche in Lagen: das Band liegt waagerecht (UV.x entlang s, Bandbreite m), jede Lage von `lage` Metern fuellt das Band quer
    (Bruchstein 0,4 m, Putz bis 1,6 m). So kachelt der Streifen des Trim-Sheets auch ueber hohe Waende."""
    v0, v1, m = BA[band]
    t_min = min(p[1] for p in poly)
    for stueck in flaeche_mit_loechern(poly, list(loecher)):
        ta, tb = min(p[1] for p in stueck), max(p[1] for p in stueck)
        k0, k1 = int(math.floor((ta - t_min) / lage + 1e-6)), int(math.ceil((tb - t_min) / lage - 1e-6))
        for k in range(k0, max(k1, k0 + 1)):
            za, zb = t_min + k * lage, t_min + (k + 1) * lage
            lag = ausschnitt(stueck, t0=za, t1=zb)
            if not lag: continue
            u0 = (u_saat if u_saat is not None else 0.0) + 0.37 * k
            for f in flaeche_teilen(lag):
                pts = [ebene.p(s, t, raus) for s, t in f]
                uvs = [(u0 + s / m, v1 - (t - za) / lage * (v1 - v0)) for s, t in f]
                teil.vieleck(stoff, pts, uvs, weg_von=ebene.innen, farbe=farbe)


def laibung(teil, ebene, loch, tiefe, stoff="Lehmputz", band="putz"):
    """Vier Flaechen in der Oeffnung, um `tiefe` hinter die Wand."""
    s0, s1, t0, t1 = loch
    n = ebene.n()
    mitte = ebene.p((s0 + s1) / 2, (t0 + t1) / 2) - n * tiefe / 2
    for a, b in (((s0, t0), (s1, t0)), ((s0, t1), (s1, t1)), ((s0, t0), (s0, t1)), ((s1, t0), (s1, t1))):
        pa, pb = ebene.p(*a), ebene.p(*b)
        teil.rechteck(stoff, pa, pb - pa, -n, (pb - pa).length, tiefe, band, zu=mitte, anteil=(0.0, 0.5))


# ── Fassaden ──────────────────────────────────────────────────────────────────────────────────────────────────────────

def fenster(teil, ebene, loch, laeden=True):
    """Fensterstock (Rahmen und Mittelpfosten), Scheibe 10 cm zurueck, Laibung, Klappläden in der Akzentfarbe."""
    s0, s1, t0, t1 = loch
    n = ebene.n()
    laibung(teil, ebene, loch, 0.12, *(("Fachwerk", "balken_c") if S.get("holzlage") is not None else ()))
    lage = S.get("holzlage")
    if lage is not None:   # Fensterrahmen in der Holzlage (buendig mit den Staendern)
        for a, b in (((s0 - 0.035, t0), (s1 + 0.035, t0)), ((s0 - 0.035, t1), (s1 + 0.035, t1)), ((s0, t0), (s0, t1)), ((s1, t0), (s1, t1))):
            lage.holz(a, b, 0.07, "balken_c", 2, gerade=True, ende=False)
    else:
        for a, b in (((s0, t0), (s1, t0)), ((s0, t1), (s1, t1)), ((s0, t0), (s0, t1)), ((s1, t0), (s1, t1))):
            teil.brett("Fachwerk", ebene.p(*a, 0.0), ebene.p(*b, 0.0), 0.07, n, "balken_c", tiefe=0.03)
    teil.brett("Fachwerk", ebene.p((s0 + s1) / 2, t0, -0.06), ebene.p((s0 + s1) / 2, t1, -0.06), 0.05, n, "balken_c", tiefe=0.02, seiten=False)
    pts = [ebene.p(s0, t0, -0.10), ebene.p(s1, t0, -0.10), ebene.p(s1, t1, -0.10), ebene.p(s0, t1, -0.10)]
    teil.vieleck("Fenster", pts, [(0, 0), (1, 0), (1, 1), (0, 1)], weg_von=ebene.innen)
    if laeden and S["og"] == "fachwerk" and S["satz"].startswith(("mensch", "halbling")):
        if lage is not None:   # nur auf freiem Gefach, so breit wie Platz ist (Nutzer 21.11.2026)
            for a, e in laeden_frei(lage, s0, s1, t0, t1, S.get("holzlage_b", 99.0)):
                teil.rechteck("Fachwerk", ebene.p(a, t0, 0.015), ebene.eu, ebene.ev, e - a, t1 - t0, "farbholz", laengs="v", weg_von=ebene.innen, farbe=ROT)
        else:
            breite = (s1 - s0) / 2
            for sa in (s0 - breite - 0.04, s1 + 0.04):
                teil.rechteck("Fachwerk", ebene.p(sa, t0, 0.025), ebene.eu, ebene.ev, breite, t1 - t0, "farbholz", laengs="v", weg_von=ebene.innen, farbe=ROT)


def laden_zu(teil, ebene, loch):
    """Geschlossene Laeden in Fenstergroesse, buendig in der Wand (keine Oeffnung dahinter), mit Rahmen und Mittelleiste. Menschen
    und Halblinge: in der Hausfarbe wie die offenen Laeden (Band „farbholz“, Eckenfarbe Rot); Zwerge und Elben: Holz (Band „planken“)."""
    s0, s1, t0, t1 = loch
    n = ebene.n()
    hausfarbe = S["satz"].startswith(("mensch", "halbling"))
    band, farbe = ("farbholz", ROT) if hausfarbe else ("planken", SCHWARZ)
    mitte = (s0 + s1) / 2
    for a, e in ((s0, mitte), (mitte, s1)):
        teil.rechteck("Fachwerk", ebene.p(a, t0), ebene.eu, ebene.ev, e - a, t1 - t0, band, laengs="v", weg_von=ebene.innen, farbe=farbe)
    lage = S.get("holzlage")
    if lage is not None:
        for a, b in (((s0 - 0.035, t0), (s1 + 0.035, t0)), ((s0 - 0.035, t1), (s1 + 0.035, t1)), ((s0, t0), (s0, t1)), ((s1, t0), (s1, t1))):
            lage.holz(a, b, 0.07, "balken_c", 2, gerade=True, ende=False)
        lage.holz((mitte, t0), (mitte, t1), 0.04, "balken_c", 2, gerade=True, ende=False)
        return
    for a, b in (((s0, t0), (s1, t0)), ((s0, t1), (s1, t1)), ((s0, t0), (s0, t1)), ((s1, t0), (s1, t1))):
        teil.brett("Fachwerk", ebene.p(*a, 0.0), ebene.p(*b, 0.0), 0.07, n, "balken_c", tiefe=0.03)
    teil.brett("Fachwerk", ebene.p(mitte, t0), ebene.p(mitte, t1), 0.04, n, "balken_c", tiefe=0.02)


def tuer(teil, ebene, loch, anker_liste, modul):
    s0, s1, t0, t1 = loch
    n = ebene.n()
    laibung(teil, ebene, loch, 0.14, *(("Fachwerk", "balken_c") if S.get("holzlage") is not None else ()))
    lage = S.get("holzlage")
    if lage is not None:   # Tuerrahmen in der Holzlage (buendig mit den Staendern)
        for a, b in (((s0 - 0.05, t1), (s1 + 0.05, t1)), ((s0, t0), (s0, t1)), ((s1, t0), (s1, t1))):
            lage.holz(a, b, 0.10, "balken_c", 2, gerade=True, ende=False)
    else:
        for a, b in (((s0, t1), (s1, t1)), ((s0, t0), (s0, t1)), ((s1, t0), (s1, t1))):
            teil.brett("Fachwerk", ebene.p(*a, 0.0), ebene.p(*b, 0.0), 0.10, n, "balken_c", tiefe=0.04)
    teil.rechteck("Fachwerk", ebene.p(s0, t0, -0.12), ebene.eu, ebene.ev, s1 - s0, t1 - t0, "planken", laengs="v", weg_von=ebene.innen)
    mitte = ebene.p((s0 + s1) / 2, 0.0, 0.0)
    anker_liste.append(("Tuer_" + modul, Vector((mitte.x, mitte.y, 0.0)), n))


def fachwerk_geschoss(teil, ebene, b, z0, z1, oeffnungen, riegel_mitte=True):
    """Fachwerk eines Geschosses vor der Putzwand: Schwelle, Rähm, Ständer an den Enden und neben jeder Oeffnung, Riegel in Bruestungs-
    und Sturzhoehe der Fenster, Streben in den Endfeldern."""
    n = ebene.n()
    hz = lambda s, t: ebene.p(s, t, 0.0)
    lage = S.get("holzlage")
    if lage is not None:
        return fachwerk_lage(lage, b, z0, z1, oeffnungen, riegel_mitte, ebene)
    teil.brett("Fachwerk", hz(0, z0 + 0.09), hz(b, z0 + 0.09), 0.18, n, "balken_c", tiefe=0.05)
    teil.brett("Fachwerk", hz(0, z1 - 0.08), hz(b, z1 - 0.08), 0.16, n, "balken_a", tiefe=0.05)
    staender = {BW / 2, b - BW / 2}
    for s0, s1, t0, t1, _ in oeffnungen:
        staender |= {s0 - BW / 2, s1 + BW / 2}
    staender = sorted(s for s in staender if BW / 2 - 1e-6 <= s <= b - BW / 2 + 1e-6)
    echt = S.get("fachwerk_echt")
    if echt:   # Zwischenstaender: kein Gefach breiter als STAENDER_ABSTAND (ausser ueber Oeffnungen)
        neu = []
        for a, e in zip(staender[:-1], staender[1:]):
            if any(o[0] >= a - 1e-3 and o[1] <= e + 1e-3 for o in oeffnungen): continue
            k = int((e - a) / STAENDER_ABSTAND)
            neu += [a + (e - a) * (i + 1) / (k + 1) for i in range(k)]
        staender = sorted(staender + neu)
    for s in staender:
        teil.brett("Fachwerk", hz(s, z0 + 0.18), hz(s, z1 - 0.16), BW, n, "balken_a", tiefe=0.045, laengs="u")
    fenster_t = [(t0, t1) for a0, a1, t0, t1, art in oeffnungen if art in ("fenster", "laden") and a1 - a0 > 0.45]   # Luken setzen keine Riegelhoehe
    for sa, sb in zip(staender[:-1], staender[1:]):
        feld_a, feld_b = sa + BW / 2, sb - BW / 2
        if feld_b - feld_a < 0.25: continue
        if any(o[0] >= feld_a - 1e-3 and o[1] <= feld_b + 1e-3 for o in oeffnungen): continue   # die Oeffnung fuellt das Feld
        hoehen = sorted({t for ft in fenster_t for t in ft}) if fenster_t else ([z0 + (z1 - z0) * 0.48] if riegel_mitte else [])
        for t in hoehen:
            teil.brett("Fachwerk", hz(feld_a, t), hz(feld_b, t), RIEGEL, n, "balken_b", tiefe=0.04)
        if not echt and (feld_a < 0.4 or feld_b > b - 0.4):          # Endfeld: Strebe (Mann-Figur angedeutet)
            unten, oben = (feld_a, z0 + 0.18), (feld_b, z1 - 0.16)
            if feld_b > b - 0.4: unten, oben = (feld_b, z0 + 0.18), (feld_a, z1 - 0.16)
            teil.brett("Fachwerk", hz(*unten), hz(*oben), 0.15, n, "balken_b", tiefe=0.035)
    # Bruestungsriegel unter den Fenstern und Sturzriegel darueber (in den Fensterfeldern selbst)
    for s0, s1, t0, t1, art in oeffnungen:
        if art not in ("fenster", "laden"): continue
        teil.brett("Fachwerk", hz(s0, t0 - 0.07), hz(s1, t0 - 0.07), RIEGEL, n, "balken_b", tiefe=0.04)
        teil.brett("Fachwerk", hz(s0, t1 + 0.07), hz(s1, t1 + 0.07), RIEGEL, n, "balken_b", tiefe=0.04)
    if echt:
        brust = min((t0 - 0.14 for _, _, t0, _, art in oeffnungen if art in ("fenster", "laden")), default=z0 + 0.35 * (z1 - z0))
        fachwerk_echt.figuren(teil, hz, n, staender, z0, z1, oeffnungen, S["stufe"], S["fw_wuerfel"], bw=BW, schwelle=0.18, raehm=0.16,
                              z_brust=brust)


def ueber_ecke(ebene, s, richtung, ueber):
    """Ueberstand des Wandendes s (richtung -1 links, +1 rechts) ueber die Ecke – nur, wo er nicht ueber die Parzellengrenze
    (x = 0 bzw. Modulbreite) hinausragen wuerde (Traufhaeuser stossen dort an den Nachbarn)."""
    if ebene is None: return ueber
    q = ebene.p(s + richtung * ueber, 0.0)
    w = S.get("modul_breite")
    if q.x < -1e-4 or (w is not None and q.x > w + 1e-4): return 0.0
    if S.get("modul_eck") and q.y > TIEFE + 1e-4: return 0.0   # Eckhaus: hinten der Nachbar
    return ueber


def fachwerk_lage(lage, b, z0, z1, oeffnungen, riegel_mitte=True, ebene=None):
    """Echtes Fachwerk (Nutzer 21.11.2026 „sauberer bauen“) in die Holzlage der Wand: Schwelle, Raehm, Staender (Gefache hoechstens
    STAENDER_ABSTAND), Riegel, Bruest- und Sturzriegel, Streben und Figuren (fachwerk_echt.figuren); alles buendig in einer Ebene."""
    # Schwelle, Raehm und Eckstaender um die Holztiefe ueber die Ecke (die Traufseite stoesst dagegen, keine Luecke)
    ul, ur = ueber_ecke(ebene, 0.0, -1, 0.05), ueber_ecke(ebene, b, 1, 0.05)
    # Schwelle: an Tueren, die bis unter die Schwelle reichen, ausgeschnitten (die Tuer steht auf dem Sockel bzw. Boden)
    tueren = sorted((o[0], o[1]) for o in oeffnungen if o[4] == "tuer" and o[2] < z0 + 0.18)
    x = -ul
    for t0_, t1_ in tueren + [(b + ur, b + ur)]:
        if t0_ - x > 0.02: lage.holz((x, z0 + 0.09), (t0_, z0 + 0.09), 0.18, "balken_c", 0, gerade=True)    # Schwelle
        x = max(x, t1_)
    lage.holz((-ul, z1 - 0.08), (b + ur, z1 - 0.08), 0.16, "balken_a", 0, gerade=True)    # Raehm
    # Staender: Eckstaender, je Oeffnung links und rechts ein Stiel, dazu Zwischenstaender (lichte Gefachbreite hoechstens 1,3 m)
    art_von = {BW / 2: ("ecke", None), b - BW / 2: ("ecke", None)}
    for s0, s1, t0, t1, art in oeffnungen:
        rahmen = 0.05 if art == "tuer" else 0.035
        art_von.setdefault(s0 - BW / 2, ("links_von", s0 - rahmen))
        art_von.setdefault(s1 + BW / 2, ("rechts_von", s1 + rahmen))
    staender = sorted(s for s in art_von if BW / 2 - 1e-6 <= s <= b - BW / 2 + 1e-6)
    neu = []
    for a, e in zip(staender[:-1], staender[1:]):
        if any(o[0] >= a - 1e-3 and o[1] <= e + 1e-3 for o in oeffnungen): continue
        k = int((e - a - BW) / GEFACH_HOECHSTENS)
        neu += [a + (e - a) * (i + 1) / (k + 1) for i in range(k)]
    for s in neu: art_von[s] = ("zwischen", None)
    # Pfosten (links, rechts, Staendermitten). Bliebe zwischen zwei Staendern weniger als 0,3 m Putz (Nutzer 21.11.2026): der Stiel
    # neben einem Fenster entfaellt, wenn der Putz bis zum Fensterrahmen dann breit genug ist (Fenster zwischen Brust- und Sturzriegel);
    # sonst werden beide Staender ein breiter Pfosten.
    pfosten = [[s - BW / 2, s + BW / 2, [s], art_von[s]] for s in sorted(staender + neu)]
    i = 0
    while i < len(pfosten) - 1:
        p_, q_ = pfosten[i], pfosten[i + 1]
        if q_[0] - p_[1] >= GEFACH_MINDESTENS - 1e-6: i += 1; continue
        fenster_stiel = lambda pf: pf[3][0] in ("links_von", "rechts_von") and any(
            o[4] in ("fenster", "laden") and (abs(o[0] - BW / 2 - pf[2][0]) < 1e-6 or abs(o[1] + BW / 2 - pf[2][0]) < 1e-6) for o in oeffnungen)
        # das neue Feld (vom Pfosten davor bis zum Pfosten danach) darf nicht breiter als 1,3 m werden
        if (fenster_stiel(q_) and q_[3][0] == "links_von" and len(q_[2]) == 1 and q_[3][1] - p_[1] >= GEFACH_MINDESTENS
                and i + 2 < len(pfosten) and pfosten[i + 2][0] - p_[1] <= GEFACH_HOECHSTENS):
            del pfosten[i + 1]; continue
        if (fenster_stiel(p_) and p_[3][0] == "rechts_von" and len(p_[2]) == 1 and q_[0] - p_[3][1] >= GEFACH_MINDESTENS
                and i >= 1 and q_[0] - pfosten[i - 1][1] <= GEFACH_HOECHSTENS):
            del pfosten[i]; i = max(i - 1, 0); continue
        pfosten[i] = [p_[0], q_[1], p_[2] + q_[2], ("breit", None)]
        del pfosten[i + 1]
    for i, (a_, c_, _, _) in enumerate(pfosten):
        a_ -= ul if i == 0 else 0.0
        c_ += ur if i == len(pfosten) - 1 else 0.0
        lage.holz(((a_ + c_) / 2, z0 + 0.18), ((a_ + c_) / 2, z1 - 0.16), c_ - a_, "balken_a", 0, gerade=True)
    staender = sorted(m for pf in pfosten for m in pf[2])
    # Riegel je Feld zwischen zwei Pfosten: Felder mit Fenster oder Laden bekommen Brust- und Sturzriegel ueber die ganze Feldbreite,
    # Felder ohne Oeffnung die Riegelkette in Fensterhoehe; Felder mit Tuer keine (der Tuerrahmen traegt)
    fenster_t = [(t0, t1) for a0, a1, t0, t1, art in oeffnungen if art in ("fenster", "laden") and a1 - a0 > 0.45]
    for (_, g0, _, _), (g1, _, _, _) in zip(pfosten[:-1], pfosten[1:]):
        drin = [o for o in oeffnungen if o[0] >= g0 - 1e-3 and o[1] <= g1 + 1e-3]
        if any(o[4] == "tuer" for o in drin): continue
        if drin:
            bucht = len(drin) == 1 and abs(drin[0][0] - g0) < 1e-3 and abs(drin[0][1] - g1) < 1e-3
            if not bucht and g1 - g0 > GEFACH_HOECHSTENS + 1e-6:
                print("HOLZLAGE Gefach", S["satz"], f"mit Oeffnung {g1 - g0:.2f} m")
            for s0, s1, t0, t1, art in drin:
                lage.holz((g0, t0 - 0.07), (g1, t0 - 0.07), RIEGEL, "balken_b", 2, gerade=True)
                lage.holz((g0, t1 + 0.07), (g1, t1 + 0.07), RIEGEL, "balken_b", 2, gerade=True)
                for k_ in (s0 - 0.035, s1 + 0.035):            # Putz neben dem Fenster
                    rest = (k_ - g0) if k_ < (s0 + s1) / 2 else (g1 - k_)
                    if 1e-3 < rest < GEFACH_MINDESTENS - 1e-6: print("HOLZLAGE Gefach", S["satz"], f"neben Fenster {rest:.2f} m")
            continue
        if not (GEFACH_MINDESTENS - 1e-6 <= g1 - g0 <= GEFACH_HOECHSTENS + 1e-6):
            print("HOLZLAGE Gefach", S["satz"], f"{g1 - g0:.2f} m")
        hoehen = sorted({t for ft in fenster_t for t in ft}) if fenster_t else ([z0 + (z1 - z0) * 0.48] if riegel_mitte else [])
        for t in hoehen:
            lage.holz((g0, t), (g1, t), RIEGEL, "balken_b", 2, gerade=True)
    brust = min((t0 - 0.14 for _, _, t0, _, art in oeffnungen if art in ("fenster", "laden")), default=z0 + 0.35 * (z1 - z0))
    fachwerk_echt.figuren(lage, staender, z0, z1, oeffnungen, S.get("fw_stufe", S["stufe"]), S["fw_wuerfel"], bw=BW, schwelle=0.18,
                          raehm=0.16, z_brust=brust)


def laeden_frei(lage, s0, s1, t0, t1, b):
    """Offene Laeden nur auf freiem Gefach (Nutzer 21.11.2026): je Seite das freie Stueck Wand neben dem Fenster (zwischen den Hoelzern
    in Fensterhoehe); der Laden ist so breit wie das halbe Fenster, hoechstens so breit wie das freie Stueck (mindestens 0,22 m),
    sonst entfaellt er. Rueckgabe: Liste (a, e) der Ladenflaechen."""
    sperren = []
    for poly, _, _, _, _ in lage.hoelzer:
        zs = [q[1] for q in poly]
        if max(zs) > t0 - 0.02 and min(zs) < t1 + 0.02:
            xs = [q[0] for q in poly]
            sperren.append((min(xs), max(xs)))
    sperren.append((s0 - 0.04, s1 + 0.04))
    for o in S.get("wand_oeffnungen") or []:                  # Tueren, geschlossene Laeden, Nachbarfenster
        if o[2] < t1 + 0.02 and o[3] > t0 - 0.02: sperren.append((o[0] - 0.06, o[1] + 0.06))
    sperren.sort()
    frei, x = [], 0.0
    for a, e in sperren:
        if a > x: frei.append((x, a))
        x = max(x, e)
    if x < b: frei.append((x, b))
    halb = (s1 - s0) / 2
    aus = []
    links = [f for f in frei if f[1] <= s0 + 1e-6]
    rechts = [f for f in frei if f[0] >= s1 - 1e-6]
    if links:
        a, e = links[-1]
        w = min(halb, e - a - 0.04)
        if w >= 0.22: aus.append((e - 0.02 - w, e - 0.02))
    if rechts:
        a, e = rechts[0]
        w = min(halb, e - a - 0.04)
        if w >= 0.22: aus.append((a + 0.02, a + 0.02 + w))
    return aus


def geschoss(teil, ebene, b, z0, z1, oeffnungen, art, anker, modul, sockel=False, hoch=0.0):
    """Eine Geschosswand der Breite b (s von 0 bis b) mit Oeffnungen (s0, s1, t0, t1, "fenster"|"tuer"); art fachwerk|putz|mauer
    (Mauerwerk im Band des Satzes, "stein" = mauer). Bei Mauer und Putz mit Werkstein: Eckquader an beiden Enden."""
    if art == "stein": art = "mauer"
    loecher = [(o[0], o[1], o[2], o[3]) for o in oeffnungen]
    poly = [(0, z0), (b, z0), (b, z1 + hoch), (0, z1 + hoch)]
    z_wand = z0                                   # Unterkante der Fachwerkhoelzer und Eckquader
    if sockel and z0 < Z_SOCKEL and not (art == "mauer" and S["mauer_band"] == "bruchstein"):
        # Bruchsteinsockel 3 cm vor der Wand; die Wand selbst reicht hinter dem Sockel bis zum Boden (Pruefung der NAS 21.11.2026:
        # sonst blieb auf 0,44 m ein Schlitz, durch den flache Strahlen in das Haus gingen), oben deckt eine Flaeche den Absatz
        unten = [(0, z0), (b, z0), (b, Z_SOCKEL), (0, Z_SOCKEL)]
        sockel_loecher = [l for l in loecher if l[2] < Z_SOCKEL]
        lagenflaeche(teil, "Bruchstein", ebene, unten, "bruchstein", 0.4, sockel_loecher, raus=0.03, u_saat=teil.zufall.random())
        sockelabsatz(teil, ebene, b, sockel_loecher)
        z_wand = Z_SOCKEL
    putz_lage = S["putz_lage"] or max(z1 - z_wand, 0.5)
    mit_quader = art in ("mauer", "putz") and S["werk_band"] and b > 1.5 and not S.get("ohne_quader")
    felder = []
    if mit_quader:   # Quader, die eine Oeffnung beruehren, entfallen
        felder = [q for q in quaderfelder(b, z_wand, z1)
                  if not any(q[0] < o[1] + 0.05 and o[0] - 0.05 < q[1] and q[2] < o[3] and o[2] < q[3] for o in oeffnungen)]
        # Hausecke unter dem Erker ohne Eckquader (quader_weg: "anfang" = s 0, "ende" = s b), sonst liefe mit den Quadern des
        # Erkers ein heller Strich vom Dach bis zum Boden (Bildpruefung Runde 3, 22.11.2026)
        weg = S.get("quader_weg")
        felder = [q for q in felder if not (weg == "anfang" and q[0] < 1e-6 or weg == "ende" and q[1] > b - 1e-6)]
        loecher = loecher + felder
    if art == "mauer":
        band = S["mauer_band"]
        lagenflaeche(teil, "Bruchstein", ebene, poly, band, BANDLAGE[band], loecher, u_saat=teil.zufall.random())
    else:
        gefachflaeche(teil, ebene, poly, putz_lage, loecher, teil.zufall.random())
        if art == "fachwerk" and S.get("fachwerk_echt"):
            S["holzlage"] = Holzlage(); S["holzlage_b"] = b
        if art == "fachwerk": fachwerk_geschoss(teil, ebene, b, z_wand, z1, oeffnungen)   # die Schwelle sitzt auf dem Sockel
    if felder:
        eckquader(teil, ebene, felder)
    S["wand_oeffnungen"] = oeffnungen
    for o in oeffnungen:
        if o[4] == "fenster": fenster(teil, ebene, o[:4])
        elif o[4] == "laden": laden_zu(teil, ebene, o[:4])
        else: tuer(teil, ebene, o[:4], anker, modul)
    lage = S.get("holzlage")
    if lage is not None:   # die Holzlage der Wand als ein Netz bauen (buendig, geschlossen, ohne Ueberschneidung)
        befunde = lage.pruefen()
        if befunde: print("HOLZLAGE", modul, befunde[:3])
        lage.bauen(teil, lambda s_, t_, r_: ebene.p(s_, t_, r_), ebene.n(), tiefe=0.05, wand=(0.0, b))
        S["holzlage"] = None


def sockelabsatz(teil, ebene, b, loecher):
    """Waagerechte Flaeche auf dem Sockel (3 cm tief, Oberkante Z_SOCKEL), unterbrochen an den Tueren."""
    v0, v1, m = BA["bruchstein"]
    grenzen, s = [], 0.0
    for l in sorted(loecher, key=lambda l: l[0]):
        if l[0] > s: grenzen.append((s, l[0]))
        s = max(s, l[1])
    if s < b: grenzen.append((s, b))
    for a, e in grenzen:
        if e - a < 1e-3: continue
        # Stirn des Sockels an beiden Enden (sonst blieb an Hausecken ein 3-cm-Spalt zwischen den Sockeln zweier Waende)
        for sk, nach in ((a, 1), (e, -1)):
            stirn = [ebene.p(sk, 0.0, 0.0), ebene.p(sk, 0.0, 0.03), ebene.p(sk, Z_SOCKEL, 0.03), ebene.p(sk, Z_SOCKEL, 0.0)]
            teil.vieleck("Bruchstein", stirn, [(0.1, v1), (0.11, v1), (0.11, v0), (0.1, v0)], weg_von=ebene.p(sk + nach * 0.3, Z_SOCKEL / 2, 0.015))
        pts = [ebene.p(a, Z_SOCKEL, 0.0), ebene.p(e, Z_SOCKEL, 0.0), ebene.p(e, Z_SOCKEL, 0.03), ebene.p(a, Z_SOCKEL, 0.03)]
        teil.vieleck("Bruchstein", pts, [(a / m, v0 + 0.01), (e / m, v0 + 0.01), (e / m, v0 + 0.02), (a / m, v0 + 0.02)],
                     weg_von=ebene.p((a + e) / 2, Z_SOCKEL - 0.5, 0.015))


def quaderfelder(b, z0, z1):
    """Die Felder der Eckquader an beiden Enden einer Wand: (s0, s1, t0, t1), abwechselnd lang (0,55 m) und kurz (0,32 m), je etwa 0,4 m."""
    n = max(1, int(round((z1 - z0) / 0.4)))
    hoch = (z1 - z0) / n
    felder = []
    for k in range(n):
        lang = 0.55 if k % 2 == 0 else 0.32
        for links in (True, False):
            s0, s1 = (0.0, lang) if links else (b - lang, b)
            felder.append((s0, s1, z0 + k * hoch, z0 + (k + 1) * hoch - 0.015))
    return felder


def erker_quaderfelder(b, z0, z1, enden, versatz):
    """Eckquader des Erkers: wie quaderfelder, aber schmaler (lang 0,3 m, kurz 0,2 m; die Fenster beginnen 0,35 m vom Rand).
    `enden`: (am Anfang s = 0, am Ende s = b); `versatz` 1 tauscht lang und kurz (Verzahnung mit der Nachbarwand an derselben Kante)."""
    n = max(1, int(round((z1 - z0) / 0.4)))
    hoch = (z1 - z0) / n
    felder = []
    for k in range(n):
        lang = 0.3 if (k + versatz) % 2 == 0 else 0.2
        for am_anfang, an in zip((True, False), enden):
            if not an: continue
            s0, s1 = (0.0, lang) if am_anfang else (b - lang, b)
            felder.append((s0, s1, z0 + k * hoch, z0 + (k + 1) * hoch - 0.015))
    return felder


def eckquader(teil, ebene, felder):
    """Eckquader aus Werkstein an beiden Enden der Wand, buendig in den Aussparungen der Wand (quaderfelder)."""
    band = S["werk_band"]
    v0, v1, m = BA[band]
    lage = BANDLAGE[band]
    for k, (s0, s1, ta, tb) in enumerate(felder):
        u0 = 0.13 * k
        lang = s1 - s0
        vt = v1 - min(1.0, (tb - ta) / lage) * (v1 - v0)
        pts = [ebene.p(s0, ta), ebene.p(s1, ta), ebene.p(s1, tb), ebene.p(s0, tb)]
        teil.vieleck("Bruchstein", pts, [(u0, v1), (u0 + lang / m, v1), (u0 + lang / m, vt), (u0, vt)], weg_von=ebene.innen)


def achsen(b, n, breite):
    """n Fensterachsen gleichmaessig in einer Wand der Breite b: Liste (s0, s1)."""
    teil = b / n
    return [(teil * (i + 0.5) - breite / 2, teil * (i + 0.5) + breite / 2) for i in range(n)]


def fassade(teil, ebene, b, oben_z, krag_ebene, anker, modul, n_eg, n_og, mit_tuer, tuer_links=True, b_og=None, traufe=0.0):
    """Schauseite: alle Geschosse (Breite b, Obergeschosse b_og in der auskragenden Ebene)."""
    wandseite(teil, ebene, krag_ebene, b, b if b_og is None else b_og, anker, modul, n_eg, n_og,
              tuer=("links" if tuer_links else "rechts") if mit_tuer else None, traufe=traufe)


def krag_boden(teil, x0, x1, y_eg, y_og, z, richtung):
    """Untersicht der Auskragung (Bohlen) und Balkenkoepfe unter dem Obergeschoss."""
    if abs(y_og - y_eg) < 1e-3: return
    v0, v1, m = BA["planken"]
    teil.vieleck("Fachwerk", [Vector((x0, y_og, z)), Vector((x1, y_og, z)), Vector((x1, y_eg, z)), Vector((x0, y_eg, z))],
                 [(0, v1), ((x1 - x0) / m, v1), ((x1 - x0) / m, v0), (0, v0)], weg_von=(0.5 * (x0 + x1), 0.5 * (y_og + y_eg), z + 1))
    n = max(2, int((x1 - x0) / 0.8) + 1)
    for i in range(n):
        x = x0 + 0.12 + i * (x1 - x0 - 0.24) / (n - 1)
        teil.stab("Fachwerk", (x, y_eg + 0.02, z - 0.08), (x, y_og - 0.04 * richtung, z - 0.08), 0.15, "balken_c", breite=0.13, kappen=True)
        if S.get("fachwerk_echt"):   # Knagge unter dem Balkenkopf (Vorbild Rathaus Alsfeld)
            fachwerk_echt.knaggen(teil, [(x, y_eg, z - 0.155 - 0.40)], (0, 1 if y_og > y_eg else -1, 0), hoehe=0.40, krag=abs(y_og - y_eg))


# ── Dach ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

def dachflaeche(d, poly_sq, fuss, entlang, hang, unten_punkt, kanten=(), karten=True, stirn=True, fuss_stufe=True, einzug=0.0):
    """Eine Dachseite als konvexes Vieleck (s entlang der Traufe, q hangaufwaerts ab `fuss`) auf der Oberseite der Deckung:
    Stroh in Stufen zu 1,55 m (Band „stroh“), Ziegel/Schiefer in Lagen zu 0,25 m (Band „stroh_franse“), jede Stufe am Fuss etwas
    angehoben (Stirn). `kanten`: Indizes der Randkanten (i, i+1) mit Stirnflaeche der Deckung (Traufe, Ortgang). `unten_punkt`: unter dem Dach."""
    fuss, entlang, hang = Vector(fuss), Vector(entlang).normalized(), Vector(hang).normalized()
    normale = entlang.cross(hang).normalized()
    if normale.z < 0: normale = -normale
    stroh = S["dach"] == "stroh"
    band = "stroh_franse" if S["dach"] == "ziegel" else "stroh"
    lage, stufe = {"stroh": (1.55, 0.07), "schindel": (0.74, 0.03), "ziegel": (0.25, 0.025)}[S["dach"]]
    if not stirn: stufe = 0.0                      # ohne Stirnflaechen keine Stufen (sie blieben offen)
    v0, v1, m = BA[band]
    P = lambda s, q, h=0.0: fuss + entlang * s + hang * q + normale * h
    # Unterseite senkrecht unter der Oberseite (nicht entlang der Normalen: sonst schoben sich die Unterseiten zweier Dachseiten am
    # First uebereinander und stachen durch die Deckung, Pruefung 21.11.2026); dieselbe Ebene wie bisher
    U = lambda s, q: P(s, q) - EZ * (S["dick"] / max(normale.z, 0.2))
    q_max = max(q for _, q in poly_sq)
    n = max(1, int(math.ceil(q_max / lage - 1e-6)))
    u_basis = d.zufall.random()
    for k in range(n):
        streifen = ausschnitt(poly_sq, t0=k * lage, t1=(k + 1) * lage)
        if not streifen: continue
        uu = u_basis + 0.37 * k
        for f in flaeche_teilen(streifen):
            pts = [P(s, q, stufe if abs(q - k * lage) < 1e-6 and (k > 0 or fuss_stufe) else 0.0) for s, q in f]
            uvs = [(uu + s / m, v1 - (q - k * lage) / lage * (v1 - v0)) for s, q in f]
            d.vieleck("Stroh", pts, uvs, weg_von=unten_punkt)
        # Stirn der Stufe am Fuss
        unten = sorted((p for p in streifen if abs(p[1] - k * lage) < 1e-6), key=lambda p: p[0])
        if stirn and (k > 0 or fuss_stufe) and len(unten) >= 2:
            a, b = unten[0], unten[-1]
            d.vieleck("Stroh", [P(a[0], a[1]), P(b[0], b[1]), P(b[0], b[1], stufe), P(a[0], a[1], stufe)],
                      [(uu, v1 - 0.002), (uu + (b[0] - a[0]) / m, v1 - 0.002), (uu + (b[0] - a[0]) / m, v1 - 0.012), (uu, v1 - 0.012)],
                      weg_von=P((a[0] + b[0]) / 2, a[1] + 0.3, -0.5))
    # Unterseite (Bohlen) und Stirn der Deckung an den genannten Kanten
    dick = S["dick"]
    pv0, pv1, pm = BA["planken"]
    for f in flaeche_teilen(poly_sq):
        pts = [U(s, q) for s, q in f]
        d.vieleck("Fachwerk", pts, [(s / pm, pv0 + (pv1 - pv0) * ((q / 3.0) % 1.0)) for s, q in f], zu=unten_punkt)
    for i in kanten:
        a, b = poly_sq[i], poly_sq[(i + 1) % len(poly_sq)]
        laenge = math.hypot(b[0] - a[0], b[1] - a[1])
        mitte = (P((a[0] + b[0]) / 2, (a[1] + b[1]) / 2) + U((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)) / 2
        innen = mitte + (P((a[0] + b[0]) / 2, (a[1] + b[1]) / 2) - P(*_schwerpunkt(poly_sq))).normalized() * -0.5
        # Die Stirn folgt der Oberseite: am Fuss jeder Lage um die Stufe angehoben (sonst blieb am Ortgang ein Keil offen, durch den man
        # unter die Deckung sah – Rueckseiten an den Naehten, Pruefung der NAS 21.11.2026)
        qa, qb = a[1], b[1]
        teile = [0.0, 1.0]
        if abs(qb - qa) > 1e-6 and stufe > 0:      # ohne Stufen genuegt eine Flaeche je Kante
            for kk in range(n + 1):
                t = (kk * lage - qa) / (qb - qa)
                if 1e-6 < t < 1 - 1e-6: teile.append(t)
        teile.sort()
        def hebung(q, oben):
            """Anhebung der Oberseite bei q; oben: Grenze von der oberen Lage aus gesehen (dort ist die Lage am Fuss angehoben)."""
            k = int(math.floor(q / lage + (1e-6 if oben else -1e-6)))
            k = max(0, min(k, n - 1))
            if not (k > 0 or fuss_stufe): return 0.0
            return stufe * max(0.0, 1.0 - (q - k * lage) / lage)
        # Ende einer Dachflaeche an der Parzellengrenze (Traufhaeuser): 1 mm nach innen, damit die Stirnen zweier Nachbarn nicht
        # deckungsgleich liegen; sie schliessen die Stufen der Deckung, wo der Nachbar niedriger ist
        ds = 0.0
        if einzug and abs(a[0] - b[0]) < 1e-6:
            s_min, s_max = min(p[0] for p in poly_sq), max(p[0] for p in poly_sq)
            ds = einzug if abs(a[0] - s_min) < 1e-6 else (-einzug if abs(a[0] - s_max) < 1e-6 else 0.0)
        for t0, t1 in zip(teile[:-1], teile[1:]):
            pa = (a[0] + (b[0] - a[0]) * t0 + ds, qa + (qb - qa) * t0)
            pb = (a[0] + (b[0] - a[0]) * t1 + ds, qa + (qb - qa) * t1)
            steigt = qb >= qa
            ha = hebung(pa[1], steigt); hb = hebung(pb[1], not steigt)
            if abs(qb - qa) < 1e-6: ha = hb = 0.0          # Traufe: die Stufe deckt dort die Stirn der Lage
            l0, l1 = laenge * t0 / m, laenge * t1 / m
            # Stirn bis zur Oberseite ohne Stufe, dazu der Keil der angehobenen Lage als Dreieck (die Kontur der Deckung)
            d.vieleck("Stroh", [U(*pa), U(*pb), P(*pb), P(*pa)], [(0.3 + l0, 0.23), (0.3 + l1, 0.23), (0.3 + l1, 0.245), (0.3 + l0, 0.245)],
                      weg_von=innen)
            if ha > 1e-6:
                d.vieleck("Stroh", [P(*pa), P(*pb), P(*pa, ha)], [(0.3 + l0, 0.24), (0.3 + l1, 0.24), (0.3 + l0, 0.245)], weg_von=innen)
            if hb > 1e-6:
                d.vieleck("Stroh", [P(*pa), P(*pb), P(*pb, hb)], [(0.3 + l0, 0.24), (0.3 + l1, 0.24), (0.3 + l1, 0.245)], weg_von=innen)
        if stroh and karten and abs(a[1]) < 1e-6 and abs(b[1]) < 1e-6:        # Traufe: Fransen (Alpha-Cards)
            f0, f1, fm = BA["stroh_franse"]
            haengt = (hang * -0.75 + Vector((0, 0, -0.25))).normalized() * 0.26
            ka, kb = P(*a, -0.02), P(*b, -0.02)
            d.vieleck("Strohkante", [ka, kb, kb + haengt, ka + haengt], [(0.1, f0), (0.1 + laenge / fm, f0), (0.1 + laenge / fm, f1), (0.1, f1)], weg_von=unten_punkt)


def _schwerpunkt(poly):
    return (sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly))


def first(d, a, b, seite_a, seite_b, unten_punkt, kappen=(True, True)):
    """Firstwulst bzw. Firstziegel: zwei schmale Flaechen ueber dem First von a nach b."""
    a, b = Vector(a), Vector(b)
    band = "stroh"
    v0, v1, m = BA[band]
    fa, fb = S.get("first_v", (v0 + 0.06, v0 + 0.11))   # Halbling S3: oben im Band liegt Efeu (STROH_TEIL), der First nimmt das Kupfer
    kamm_a, kamm_b = a + EZ * (0.14 if S["dach"] == "stroh" else 0.07), b + EZ * (0.14 if S["dach"] == "stroh" else 0.07)
    raender = []
    for seite in (seite_a, seite_b):
        seite = Vector(seite) - EZ * 0.12          # Rand 2 cm in der Deckung (Pruefung 21.11.2026: darunter sah man ins Dach)
        rand_a, rand_b = a + seite, b + seite
        raender.append((rand_a, rand_b))
        laenge = (b - a).length
        d.vieleck("Stroh", [rand_a, rand_b, kamm_b, kamm_a], [(0.5, fa), (0.5 + laenge / m, fa), (0.5 + laenge / m, fb), (0.5, fb)], weg_von=unten_punkt)
    richtung = (b - a).normalized()
    for ende, kamm, i, aussen in ((a, kamm_a, 0, -1), (b, kamm_b, 1, 1)):    # Stirnseiten der Firstkappe (nur an freien Enden)
        if not kappen[i]: continue
        d.vieleck("Stroh", [raender[0][i], kamm, raender[1][i]], [(0.5, fa), (0.55, fb), (0.6, fa)], weg_von=ende - richtung * aussen)


def schornstein(h, x, y, z_unten, z_oben, b=0.55):
    """Bruchsteinschlot durchs Dach."""
    for k, (o, eu, breite) in enumerate((((x - b / 2, y - b / 2, 0), (1, 0, 0), b), ((x + b / 2, y - b / 2, 0), (0, 1, 0), b),
                                          ((x + b / 2, y + b / 2, 0), (-1, 0, 0), b), ((x - b / 2, y + b / 2, 0), (0, -1, 0), b))):
        eb = Ebene(o, eu, (0, 0, 1), (x, y, z_unten))
        lagenflaeche(h, "Bruchstein", eb, [(0, z_unten), (breite, z_unten), (breite, z_oben), (0, z_oben)], "bruchstein", 0.4, u_saat=0.21 * k)
    h.kasten("Bruchstein", (x - b / 2 - 0.05, y - b / 2 - 0.05, z_oben), (x + b / 2 + 0.05, y + b / 2 + 0.05, z_oben + 0.10), "bruchstein")   # mit Unterseite (Rand steht 5 cm ueber)


# ── Koerper ───────────────────────────────────────────────────────────────────────────────────────────────────────────

def koerper_giebel(w, modul):
    """Giebelstaendiger Koerper der Breite w (4/6/8 m): Waende x von EIN bis w - EIN, Satteldach mit First entlang y bis an die Grenzen.
    Echtes Fachwerk (FACHWERK_ECHT) mit festem Wuerfel je Modul."""
    alt = (S.get("fachwerk_echt"), S.get("fw_wuerfel"))
    S["fachwerk_echt"] = echt_fuer(modul)
    S["fw_wuerfel"] = random.Random(sum(ord(c) * (i + 3) for i, c in enumerate(S["satz"] + modul)))
    try:
        return _koerper_giebel(w, modul)
    finally:
        S["fachwerk_echt"], S["fw_wuerfel"] = alt


def mit_fachwerk(bauer):
    """Setzt fuer den Bau eines Moduls das echte Fachwerk (FACHWERK_ECHT) und den festen Wuerfel je Modul."""
    def bauen(m):
        alt = (S.get("fachwerk_echt"), S.get("fw_wuerfel"))
        S["fachwerk_echt"] = echt_fuer(m)
        S["fw_wuerfel"] = random.Random(sum(ord(c) * (i + 3) for i, c in enumerate(S["satz"] + m)))
        S["modul_breite"] = BREITE.get(m); S["modul_eck"] = m.startswith("eck")
        # Seitenstuecke (Traufseiten) der reichen Stufe 3 schlicht wie Stufe 1: Schmuck an der Schauseite, Hoechstgrenze 8.000 je Haus
        S["fw_stufe"] = 1 if S["stufe"] >= 3 and m.startswith(("seite", "kappe")) else S["stufe"]
        try:
            return bauer(m)
        finally:
            S["fachwerk_echt"], S["fw_wuerfel"] = alt
    return bauen


def giebel_holzlage(h, eb, b, z_t, first_z, s_first, loch):
    """Giebelfachwerk (echtes Fachwerk) als eine Holzlage, auf das Giebeldreieck unter der Dachflaeche zugeschnitten: Staender neben
    der Luke (sonst in der Mitte), Kehlbalken ueber der Luke; Fensterrahmen der Luke in derselben Lage."""
    dreieck = [(0, z_t), (b, z_t), (s_first, first_z - 0.06)]
    lage = Holzlage(umriss=dreieck); S["holzlage"] = lage; S["holzlage_b"] = b
    mitte = [loch[0] - BW / 2, loch[1] + BW / 2] if loch else [s_first]
    links, rechts = min(mitte), max(mitte)
    hoehe = lambda x: (first_z - z_t) * (x / s_first if x <= s_first else (b - x) / (b - s_first))
    pfosten = list(mitte)
    for rand, innen_ in ((0.0, links), (b, rechts)):     # nach aussen weitere Staender, solange das Dreieck dort 0,6 m hoch ist
        k = int((abs(innen_ - rand) - BW) / GEFACH_HOECHSTENS)
        for i in range(1, k + 1):
            x = innen_ + (rand - innen_) * i / (k + 1)
            if hoehe(x) >= 0.6: pfosten.append(x)
    for s_ in sorted(pfosten):
        lage.holz((s_, z_t), (s_, first_z), BW, "balken_a", 0, zuschnitt=dreieck)
    kz = z_t + (first_z - z_t) * 0.55
    kz_l = max(kz, loch[3] + 0.15) if loch else kz          # Kehlbalken ueber der Luke
    if kz_l < first_z - 0.45: lage.holz((0, kz_l), (b, kz_l), RIEGEL, "balken_b", 1, zuschnitt=dreieck)
    if loch: fenster(h, eb, loch, laeden=False)
    befunde = lage.pruefen()
    if befunde: print("HOLZLAGE Giebel", befunde[:3])
    lage.bauen(h, lambda s_, t_, r_: eb.p(s_, t_, r_), eb.n(), tiefe=0.05)
    S["holzlage"] = None


def _koerper_giebel(w, modul):
    h = Teil(modul, BA, saat=sum(map(ord, modul)))
    anker = []
    b = w - 2 * EIN
    z_t = S["z_traufe"]
    krag = S["krag"]
    innen = (w / 2, TIEFE / 2, 2.0)
    vorn = Ebene((EIN, 0, 0), (1, 0, 0), (0, 0, 1), innen)
    vorn_og = Ebene((EIN, -krag, 0), (1, 0, 0), (0, 0, 1), innen)
    n_og = {4: 2, 6: 3, 8: 4}[w]
    fassade(h, vorn, b, z_t, vorn_og, anker, modul, n_og - 1, n_og, True, tuer_links=(w != 6))
    krag_boden(h, EIN, w - EIN, 0.0, -krag, S["z_og"], -1)
    hinten = Ebene((w - EIN, TIEFE, 0), (-1, 0, 0), (0, 0, 1), innen)
    wandseite(h, hinten, hinten, b, b, anker, modul, max(1, n_og - 1), max(1, n_og - 1))
    # Giebeldreiecke vorn und hinten bis unter die Dachflaeche
    tg = S["tg"]
    first_z = z_t + (w / 2 - EIN) * tg
    treppe = S["treppe"]
    if treppe:
        treppengiebel(h, vorn_og, b, z_t, first_z, -krag)
    for eb, y in ((vorn_og, -krag), (hinten, TIEFE)):
        if treppe and eb is vorn_og: continue
        poly = [(0, z_t), (b, z_t), (b, z_t + 0.1), (b / 2, first_z + 0.1), (0, z_t + 0.1)]   # bis in die Deckung
        loch = (b / 2 - 0.30, b / 2 + 0.30, z_t + 0.55, z_t + 1.35) if first_z - z_t > 2.1 else None
        art = S["og"]
        if art in ("mauer", "putz"):
            band = S["mauer_band"] if art == "mauer" else "putz"
            lagenflaeche(h, "Bruchstein" if art == "mauer" else "Lehmputz", eb, poly, band, BANDLAGE[band], [loch] if loch else [], u_saat=h.zufall.random())
        else:
            gefachflaeche(h, eb, poly, first_z - z_t, [loch] if loch else [], h.zufall.random())
            n = eb.n()
            kz = z_t + (first_z - z_t) * 0.55
            ks = (first_z - kz) / tg
            if S.get("fachwerk_echt"):
                giebel_holzlage(h, eb, b, z_t, first_z, b / 2, loch)
                continue
            # Fachwerk im Giebel: Rähm, Ständer neben der Luke und in der Mitte, Kehlbalken
            for s in ([loch[0] - BW / 2, loch[1] + BW / 2] if loch else [b / 2]):
                oben = first_z - abs(s - b / 2) * tg - 0.12
                if oben > z_t + 0.3: h.brett("Fachwerk", eb.p(s, z_t), eb.p(s, oben), BW, n, "balken_a", tiefe=0.04)
            h.brett("Fachwerk", eb.p(b / 2 - ks + 0.08, kz), eb.p(b / 2 + ks - 0.08, kz), RIEGEL, n, "balken_b", tiefe=0.04)
        if loch: fenster(h, eb, loch, laeden=False)
    # Dach: zwei Seiten, Traufe an der Parzellengrenze (x = 0 bzw. w)
    dick = S["dick"]
    y0, y1 = (-krag + 0.15 if treppe else -krag - ORTGANG), TIEFE + ORTGANG
    # Rahmen (Pruefung der NAS 21.11.2026): die Traufe liegt so weit innerhalb der Parzelle, dass auch die angehobene Stufe der Deckung
    # (Stirn, Normale) nicht ueber die Grenze ragt; keine Fransen an diesen Traufen (sie hingen nach aussen)
    stufe = {"stroh": 0.07, "schindel": 0.03, "ziegel": 0.025}[S["dach"]]
    rand = stufe * math.sin(math.radians(S["neig_g"])) + 0.01
    z_fuss = z_t - (EIN - rand) * tg + dick / math.cos(math.radians(S["neig_g"]))
    breite_hang = (w / 2 - rand) / math.cos(math.radians(S["neig_g"]))
    unten_punkt = (w / 2, TIEFE / 2, z_t)
    for seite in (-1, 1):
        x_tr = rand if seite < 0 else w - rand
        fuss = Vector((x_tr, y1 if seite < 0 else y0, z_fuss))
        entlang = Vector((0, -1, 0)) if seite < 0 else Vector((0, 1, 0))
        hang = Vector((-seite * (w / 2), 0, (w / 2) * tg)).normalized()
        laenge = y1 - y0
        poly = [(0, 0), (laenge, 0), (laenge, breite_hang), (0, breite_hang)]
        dachflaeche(h, poly, fuss, entlang, hang, unten_punkt, kanten=(0, 1, 3), karten=False)   # auch vorn am Treppengiebel (neben der Wand sichtbar)
    zf = z_t + (w / 2 - EIN) * tg + dick / math.cos(math.radians(S["neig_g"]))
    first(h, (w / 2, y0, zf), (w / 2, y1, zf), (-0.35, 0, -0.35 * tg + 0.1), (0.35, 0, -0.35 * tg + 0.1), unten_punkt)
    # Windbretter am Ortgang
    for y, aussen in ((y0, -1), (y1, 1)):
        if treppe and aussen < 0: continue   # vorn steckt der Ortgang im Treppengiebel
        for seite in (-1, 1):
            a = Vector((w / 2 + seite * (w / 2 - rand - 0.11), y, z_fuss + 0.11 * tg - dick - 0.02)); bb = Vector((w / 2, y, zf - 0.04))
            h.stab("Fachwerk", a + Vector((0, aussen * 0.015, 0)), bb + Vector((0, aussen * 0.015, 0)), 0.03, "balken_c", breite=0.20, oben=(0, aussen, 0), kappen=True)
    if w >= 6:
        schornstein(h, w / 2 + 0.9, TIEFE * 0.62, z_t, zf + 0.7)
    return h, anker


def treppengiebel(h, eb, b, z_t, first_z, y_wand, stufen=4, dicke=0.32):
    """Treppengiebel vorn (Stufe 3 der Menschen, Zwerge, Halblinge; Vorbild Overstolzenhaus): die Giebelwand steigt in Stufen 0,45 m ueber
    die Deckung, mit Abdeckplatten aus Werkstein; das Dach beginnt hinter der Wand. Mauer im Band des Satzes, Luke in der Mitte."""
    tg, dick = S["tg"], S["dick"]
    cos = math.cos(math.radians(S["neig_g"]))
    mitte = 0.35
    oben = lambda s: z_t + min(s, b - s) * tg + dick / cos + 0.45      # Oberkante ueber der Deckung bei s
    grenzen = [i * (b / 2 - mitte) / stufen for i in range(stufen + 1)]
    streifen = []
    for i in range(stufen):
        a0, a1 = grenzen[i], grenzen[i + 1]
        streifen.append((a0, a1, oben(a1)))
        streifen.append((b - a1, b - a0, oben(a1)))
    streifen.append((b / 2 - mitte, b / 2 + mitte, z_t + (b / 2) * tg + dick / cos + 0.75))
    streifen.sort()
    band = S["mauer_band"]
    stoff = "Bruchstein"
    loch = (b / 2 - 0.30, b / 2 + 0.30, z_t + 0.55, z_t + 1.35) if first_z - z_t > 2.1 else None
    werk = S["werk_band"] or band
    wv0, wv1, wm = BA[werk]
    n = eb.n()
    hinten = Ebene(eb.o - n * dicke, eb.eu, eb.ev, eb.o + n * 1.0)      # Rueckseite, sichtbar ueber dem Dach
    for k, (a0, a1, top) in enumerate(streifen):
        poly = [(a0, z_t), (a1, z_t), (a1, top), (a0, top)]
        lagenflaeche(h, stoff, eb, poly, band, BANDLAGE[band], [loch] if loch else [], u_saat=0.11 * k)
        unten_hinten = z_t + min(a0, b - a1) * tg
        lagenflaeche(h, stoff, hinten, [(a0, unten_hinten), (a1, unten_hinten), (a1, top), (a0, top)], band, BANDLAGE[band], u_saat=0.23 * k)
        # Abdeckplatte
        p0 = eb.p(a0, top, 0.0)          # buendig (ohne Unterseite stand die Platte vorher ueber)
        h.rechteck(stoff, p0, eb.eu, -n, a1 - a0, dicke, werk, weg_von=eb.p((a0 + a1) / 2, top - 1.0, -dicke / 2))
    # Stossflaechen zwischen den Stufen und an den Enden (zur niedrigeren Seite)
    for i, (a0, a1, top) in enumerate(streifen):
        for s_kante, nachbar in ((a0, streifen[i - 1][2] if i > 0 else z_t), (a1, streifen[i + 1][2] if i + 1 < len(streifen) else z_t)):
            if top - nachbar < 0.01: continue
            p = eb.p(s_kante, nachbar)
            richtung = -1 if s_kante == a0 else 1
            h.rechteck(stoff, p, -n, (0, 0, 1), dicke, top - nachbar, band, laengs="u",
                       weg_von=eb.p(s_kante - richtung * 0.5, (top + nachbar) / 2, -dicke / 2))
    if loch: fenster(h, eb, loch, laeden=False)


def koerper_trauf(w, modul):
    """Traufstaendiger Koerper der Breite w (6/8 m): Waende bis an die Grenzen, First entlang x, keine Seitenwaende (ohne Eckquader:
    das Haus stoesst an die Nachbarn)."""
    S["ohne_quader"] = True
    try:
        return _koerper_trauf(w, modul)
    finally:
        S["ohne_quader"] = False


def _koerper_trauf(w, modul):
    h = Teil(modul, BA, saat=sum(map(ord, modul)))
    anker = []
    z_t = S["z_traufe"]
    krag = S["krag"]
    innen = (w / 2, TIEFE / 2, 2.0)
    vorn = Ebene((0, 0, 0), (1, 0, 0), (0, 0, 1), innen)
    vorn_og = Ebene((0, -krag, 0), (1, 0, 0), (0, 0, 1), innen)
    n_og = {6: 3, 8: 4}[w]
    fassade(h, vorn, w, z_t, vorn_og, anker, modul, n_og - 1, n_og, True, tuer_links=False, traufe=0.1)
    krag_boden(h, 0.0, w, 0.0, -krag, S["z_og"], -1)
    hinten = Ebene((w, TIEFE, 0), (-1, 0, 0), (0, 0, 1), innen)
    wandseite(h, hinten, hinten, w, w, anker, modul, n_og - 1, n_og - 1, traufe=0.1)
    trauf_dach(h, 0.0, w, z_t, krag, kanten_enden=False, einzug=0.001)   # Stirnen an den Enden (Pruefung 21.11.2026)
    zf = trauf_first_z(z_t, krag)
    schornstein(h, w * 0.68, (TIEFE - krag) / 2 + 0.8, z_t, zf + 0.6)
    return h, anker


def trauf_first_z(z_t, krag):
    return z_t + ((TIEFE + krag) / 2) * S["tt"] + S["dick"] / math.cos(math.radians(S["neig_t"]))


def trauf_dach(h, x0, x1, z_t, krag, kanten_enden, stirn=True, einzug=0.0):
    """Satteldach mit First entlang x zwischen x0 und x1 (Traufe vorn vor dem Obergeschoss, hinten vor der Rueckwand)."""
    tt, dick = S["tt"], S["dick"]
    cos = math.cos(math.radians(S["neig_t"]))
    y_first = (TIEFE - krag) / 2
    halb = (TIEFE + krag) / 2                            # waagerecht von der Wand bis unter den First
    z_fuss = z_t - UEBER * tt + dick / cos
    hang_l = (halb + UEBER) / cos
    unten_punkt = ((x0 + x1) / 2, y_first, z_t)
    for seite in (-1, 1):
        y_tr = -krag - UEBER if seite < 0 else TIEFE + UEBER
        if seite < 0:
            fuss, entlang = Vector((x0, y_tr, z_fuss)), Vector((1, 0, 0))
        else:
            fuss, entlang = Vector((x1, y_tr, z_fuss)), Vector((-1, 0, 0))
        hang = Vector((0, -seite * halb, halb * tt)).normalized()
        L = x1 - x0
        poly = [(0, 0), (L, 0), (L, hang_l), (0, hang_l)]
        dachflaeche(h, poly, fuss, entlang, hang, unten_punkt, kanten=(0, 1, 3) if (kanten_enden or einzug) else (0,), stirn=stirn, einzug=einzug)
    zf = trauf_first_z(z_t, krag)
    # Traufhaus: die Enden stossen an den Nachbarn (oder an die Kappe); nur der Ortgang der offenen Kappe (x0 < 0) ist frei
    first(h, (x0, y_first, zf), (x1, y_first, zf), (0, -0.35, -0.35 * tt + 0.1), (0, 0.35, -0.35 * tt + 0.1), unten_punkt, kappen=(x0 < 0, False))


def trauf_profil(z_t, krag):
    """Seitenumriss eines Traufhauses (y, z) unter der Dachunterseite: fuer Kappen."""
    tt = S["tt"]
    y_first = (TIEFE - krag) / 2
    return y_first, z_t + ((TIEFE + krag) / 2) * tt


# ── Seitenstuecke (linke Seite; rechts spiegelt das Spiel) ────────────────────────────────────────────────────────────

def seite_giebelhaus(offen, modul):
    """Traufseite eines Giebelhauses bei x = EIN (Wand mit Sichtseite nach -x): blind (Putz) oder offen (Fachwerk mit Fenstern)."""
    h = Teil(modul, BA, saat=31 if offen else 37)
    anker = []
    krag, z_og, z_t = S["krag"], S["z_og"], S["z_traufe"]
    innen = (EIN + 2.0, TIEFE / 2, 2.0)
    eg = Ebene((EIN, TIEFE, 0), (0, -1, 0), (0, 0, 1), innen)           # s von hinten (y = TIEFE) nach vorn
    # offen: wenige Fensterachsen (Dreiecksgrenze der Kappen, Konzept 4.9: 300)
    n = 3 if S.get("variante") is not None else 1
    wandseite(h, eg, eg, TIEFE, TIEFE + krag, anker, modul, n, n, blind=not offen, traufe=0.1)
    return h, anker


def kappe(brand, modul):
    """Giebelseite eines Traufhauses bei x = 0 (Sichtseite nach -x). Brandwand: 0,25 m Bruchstein bis 0,35 m ueber die Deckung, mit
    Abdeckplatten; offen: Fachwerkwand und -giebel mit Fenstern, Ortgang mit 0,3 m Dachueberstand und Windbrettern."""
    h = Teil(modul, BA, saat=41 if brand else 43)
    anker = []
    krag, z_og, z_t = S["krag"], S["z_og"], S["z_traufe"]
    tt, dick = S["tt"], S["dick"]
    cos = math.cos(math.radians(S["neig_t"]))
    y_first, z_first = trauf_profil(z_t, krag)
    innen = (3.0, TIEFE / 2, 2.0)
    eb = Ebene((0, TIEFE, 0), (0, -1, 0), (0, 0, 1), innen)            # s = TIEFE - y
    s_vorn_og = TIEFE + krag
    s_first = TIEFE - y_first
    if brand:
        d = 0.25
        ueber = 0.35 + dick / cos
        s_tr_h, s_tr_v = -UEBER - 0.05, TIEFE + krag + UEBER + 0.05     # ueber die Traufen hinaus
        z_tr = z_t - (UEBER + 0.05) * tt
        # Grenze zwischen Wand und Giebelstueck: hoechstens die Oberkante an den Traufenden (bei steilem Dach liegt sie unter der
        # Traufhoehe, das Giebelstueck waere sonst kein Vieleck mehr)
        z_m = min(z_t, z_tr + ueber - 0.01)
        stuecke = [[(0, 0), (TIEFE, 0), (TIEFE, z_og), (0, z_og)],
                   [(0, z_og), (s_vorn_og, z_og), (s_vorn_og, z_m), (0, z_m)],
                   [(s_tr_h, z_m), (s_tr_v, z_m), (s_tr_v, z_tr + ueber), (s_first, z_first + ueber), (s_tr_h, z_tr + ueber)],
                   [(s_tr_h, z_tr - 0.15), (0, z_tr - 0.15), (0, z_m), (s_tr_h, z_m)],
                   [(s_vorn_og, z_tr - 0.15), (s_tr_v, z_tr - 0.15), (s_tr_v, z_m), (s_vorn_og, z_m)]]
        innen_eb = Ebene((d, TIEFE, 0), (0, -1, 0), (0, 0, 1), (-1.0, TIEFE / 2, 2.0))
        for k, poly in enumerate(stuecke):
            lagenflaeche(h, "Bruchstein", eb, poly, S["mauer_band"], BANDLAGE[S["mauer_band"]], u_saat=0.13 * k)
            if k >= 2:   # innen nur ueber dem eigenen Dach und unter den Traufen sichtbar
                lagenflaeche(h, "Bruchstein", innen_eb, poly, S["mauer_band"], BANDLAGE[S["mauer_band"]], u_saat=0.29 + 0.1 * k)
        for sa, sb in ((s_tr_h, 0.0), (s_vorn_og, s_tr_v)):     # Unterseite der Stuecke unter den Traufen
            pa, pb = eb.p(sa, z_tr - 0.15), eb.p(sb, z_tr - 0.15)
            h.rechteck("Bruchstein", pa, pb - pa, (1, 0, 0), (pb - pa).length, d, "bruchstein", weg_von=(d / 2, (pa.y + pb.y) / 2, z_tr + 1))
        # Stirnseiten und Abdeckung
        for s, z0, z1 in ((s_tr_h, z_tr - 0.15, z_tr + ueber), (s_tr_v, z_tr - 0.15, z_tr + ueber)):
            y = TIEFE - s
            h.rechteck("Bruchstein", (0, y, z0), (1, 0, 0), (0, 0, 1), d, z1 - z0, "bruchstein", laengs="v",
                       weg_von=(d / 2, TIEFE / 2, (z0 + z1) / 2))
        for (sa, za), (sb, zb) in (((s_tr_h, z_tr + ueber), (s_first, z_first + ueber)), ((s_first, z_first + ueber), (s_tr_v, z_tr + ueber))):
            pa, pb = eb.p(sa, za), eb.p(sb, zb)
            o = pa
            h.rechteck("Bruchstein", o, pb - pa, (1, 0, 0), (pb - pa).length, d, "bruchstein", laengs="u",
                       weg_von=(d / 2, (pa.y + pb.y) / 2, (pa.z + pb.z) / 2 - 1))
    else:
        wandseite(h, eb, eb, TIEFE, TIEFE + krag, anker, modul, 0, 1)
        b = TIEFE + krag
        poly = [(0, z_t), (b, z_t), (b, z_t + 0.1), (s_first, z_first + 0.1), (0, z_t + 0.1)]   # bis in die Deckung
        loch = (s_first - 0.3, s_first + 0.3, z_t + 0.6, z_t + 1.4)
        if S["og"] in ("mauer", "putz"):
            band = S["mauer_band"] if S["og"] == "mauer" else "putz"
            lagenflaeche(h, "Bruchstein" if S["og"] == "mauer" else "Lehmputz", eb, poly, band, BANDLAGE[band], [loch], u_saat=0.5)
        else:
            gefachflaeche(h, eb, poly, z_first - z_t, [loch], 0.5)
            n = eb.n()
            if S.get("fachwerk_echt"):   # Giebelhoelzer und Lukenrahmen als eine Holzlage
                giebel_holzlage(h, eb, b, z_t, z_first, s_first, loch)
            else:
                for s in (loch[0] - BW / 2, loch[1] + BW / 2):
                    oben = z_first - abs(s - s_first) * tt - 0.12
                    h.brett("Fachwerk", eb.p(s, z_t), eb.p(s, oben), BW, n, "balken_a", tiefe=0.04)
                kz = z_t + (z_first - z_t) * 0.55
                ks = (z_first - kz) / tt
                h.brett("Fachwerk", eb.p(s_first - ks + 0.08, kz), eb.p(s_first + ks - 0.08, kz), RIEGEL, n, "balken_b", tiefe=0.04)
        if not (S["og"] == "fachwerk" and S.get("fachwerk_echt")):
            fenster(h, eb, loch, laeden=False)
        # Ortgang: Dach 0,3 m ueber die Giebelwand hinaus, Windbretter
        trauf_dach(h, -ORTGANG, 0.0, z_t, krag, kanten_enden=True, stirn=False)   # Ortgang ohne Stirn der Lagen (Dreiecksgrenze 300)
        zf = trauf_first_z(z_t, krag)
        z_fuss = z_t - UEBER * tt + dick / cos
        for y_tr in (-krag - UEBER, TIEFE + UEBER):
            a = Vector((-ORTGANG - 0.03, y_tr, z_fuss - dick - 0.02)); bb = Vector((-ORTGANG - 0.03, y_first, zf - 0.04))
            h.stab("Fachwerk", a + Vector((-0.015, 0, 0)), bb + Vector((-0.015, 0, 0)), 0.03, "balken_c", breite=0.20, oben=(-1, 0, 0), kappen=True)
    return h, anker


def gasse(modul):
    """Halbe Traufgasse (Streifen x 0 bis EIN an der linken Grenze eines Giebelhauses): vorn und hinten eine zurueckgesetzte Bretter-
    wand (Tuerchen), darueber ein dunkler Putzspalt bis unter die Traufe, oben ein Brett, unten die Rinne."""
    h = Teil(modul, BA, saat=53)
    z_t = S["z_traufe"]
    z_top = z_t - EIN * S["tg"]
    for y, aussen in ((0.35, -1), (TIEFE - 0.35, 1)):
        innen = (EIN / 2, y - aussen, 2.0)
        y_putz = y + (0.6 if aussen < 0 else -0.6)
        for gegen in (False, True):        # beidseitig: aus dem Spalt sieht man auch die Rueckseiten (5 mm dahinter)
            dy = -aussen * 0.005 if gegen else 0.0
            h.rechteck("Fachwerk", (0, y + dy, 0), (1, 0, 0), (0, 0, 1), EIN, 2.1, "planken", laengs="v",
                       weg_von=(EIN / 2, y + aussen * (1 if gegen else -1), 2.0))
            h.rechteck("Lehmputz", (0, y_putz + dy, 2.1), (1, 0, 0), (0, 0, 1), EIN, z_top - 2.1 - 0.05, "putz",
                       weg_von=(EIN / 2, y_putz + aussen * (1 if gegen else -1), 3))
        dy = -aussen * 0.005
        for yy, zz, oben in ((y, 2.1, True), (y_putz, 2.1, False), (y_putz, z_top - 0.05, True)):   # Kanten der doppelten Flaechen
            h.rechteck("Fachwerk" if yy == y else "Lehmputz", (0, min(yy, yy + dy), zz), (1, 0, 0), (0, 1, 0), EIN, abs(dy),
                       "planken" if yy == y else "putz", weg_von=(EIN / 2, yy, zz + (-1 if oben else 1)))
        y_kante = (-0.035 if aussen < 0 else TIEFE + 0.035)
        h.stab("Fachwerk", (0, y_kante, z_top - 0.25), (EIN, y_kante, z_top - 0.25), 0.03, "balken_c", breite=0.18, oben=(0, aussen, 0), kappen=True)
    h.rechteck("Bruchstein", (0, -0.25, 0.02), (1, 0, 0), (0, 1, 0), EIN, 0.6, "bruchstein", weg_von=(EIN / 2, 0, -1))
    return h, []


# ── Eckhaus ──────────────────────────────────────────────────────────────────────────────────────────────────────────

def eckhaus(modul):
    """Eckhaus 8 x 8 m: Schauseiten vorn (y = 0) und rechts (x = 8) mit Tuer und Fenstern, links und hinten blinde Waende
    (dort stehen die Nachbarn). Fachwerk kragt vorn und rechts aus (das Obergeschoss ist ein Quadrat Q = 8 + KRAG), Zeltdach mit
    Ueberstand nur ueber den Schauseiten. Ab Stufe 2 ein Eckerker (Nutzer)."""
    h = Teil(modul, BA, saat=61)
    anker = []
    B = TIEFE
    k = S["krag"]
    Q, oy = B + k, -k
    z_og, z_t = S["z_og"], S["z_traufe"]
    innen = (B / 2, B / 2, 2.0)
    vorn, vorn_og = Ebene((0, 0, 0), (1, 0, 0), (0, 0, 1), innen), Ebene((0, oy, 0), (1, 0, 0), (0, 0, 1), innen)
    rechts, rechts_og = Ebene((B, 0, 0), (0, 1, 0), (0, 0, 1), innen), Ebene((Q, oy, 0), (0, 1, 0), (0, 0, 1), innen)
    links = Ebene((0, B, 0), (0, -1, 0), (0, 0, 1), innen)
    hinten, hinten_og = Ebene((B, B, 0), (-1, 0, 0), (0, 0, 1), innen), Ebene((Q, B, 0), (-1, 0, 0), (0, 0, 1), innen)
    erker_quader = S["erker"] and S.get("erker_quader")
    S["quader_weg"] = "ende" if erker_quader else None        # vorn: die Ecke unter dem Erker liegt bei s = B
    fassade(h, vorn, B, z_t, vorn_og, anker, modul, 3, 4, True, tuer_links=False, b_og=Q, traufe=0.1)
    S["quader_weg"] = "anfang" if erker_quader else None      # rechts: bei s = 0
    fassade(h, rechts, B, z_t, rechts_og, [], modul, 3, 4, False, b_og=Q, traufe=0.1)
    S["quader_weg"] = None
    krag_boden(h, 0.0, Q, 0.0, oy, z_og, -1)
    if k > 0:   # Untersicht der Auskragung rechts
        v0, v1, m = BA["planken"]
        h.vieleck("Fachwerk", [Vector((B, oy, z_og)), Vector((Q, oy, z_og)), Vector((Q, B, z_og)), Vector((B, B, z_og))],
                  [(0, v1), (0, v0), ((B - oy) / m, v0), ((B - oy) / m, v1)], weg_von=(B + k / 2, B / 2, z_og + 1))
    # blinde Waende an der Parzellengrenze: ohne vorstehenden Sockel (Rahmen), bis unter die Deckung geschlossen (siehe unten)
    # Ohne Ueberstand liegt die Traufe links und hinten auf der Wandkrone; dort schliesst die senkrechte Stirn der Deckung (Unterseite
    # senkrecht unter der Oberseite) genau an die Wandkrone an (Pruefung der NAS 21.11.2026: vorher Oeffnung 5,66-6,01 m).
    S["ohne_quader"] = True
    wandseite(h, links, links, B, Q, anker, modul, 0, 0, blind=True, sockel=False)
    wandseite(h, hinten, hinten_og, B, Q, anker, modul, 0, 0, blind=True, sockel=False)
    S["ohne_quader"] = False
    # Zeltdach: z = z_t + tg * min(x, Q - x, y - oy, Q - (y - oy)), Ueberstand vorn und rechts
    tg, dick = S["tg"], S["dick"]
    cos = math.cos(math.radians(S["neig_g"]))
    u = UEBER
    spitze = Vector((Q / 2, oy + Q / 2, z_t + Q / 2 * tg + dick / cos))
    def dz(x, y): return z_t + tg * min(x, Q - x, y - oy, Q - (y - oy)) + dick / cos
    unten_punkt = (Q / 2, oy + Q / 2, z_t)
    ap = (Q / 2, oy + Q / 2)
    seiten = [  # (Grundriss-Vieleck, Traufe-Fuss, entlang, hang)
        ([(0, oy - u), (Q + u, oy - u), ap, (0, oy)], (0, oy - u), (1, 0, 0), (0, 1, tg)),
        ([(Q + u, oy - u), (Q + u, B), (Q, B), ap], (Q + u, oy - u), (0, 1, 0), (-1, 0, tg)),
        ([(Q, B), (0, B), ap], (Q, B), (-1, 0, 0), (0, -1, tg)),
        ([(0, B), (0, oy), ap], (0, B), (0, -1, 0), (1, 0, tg)),
    ]
    for nr, (grund, fuss_xy, entlang, hang) in enumerate(seiten):
        fuss = Vector((fuss_xy[0], fuss_xy[1], dz(*fuss_xy)))
        e, hg = Vector(entlang), Vector(hang).normalized()
        poly = []
        for x, y in grund:
            p = Vector((x, y, dz(x, y))) - fuss
            poly.append((p.dot(e), p.dot(hg)))
        kanten = tuple(i for i in range(len(grund)) if _eck_kante(grund[i], grund[(i + 1) % len(grund)], ap))
        dachflaeche(h, poly, fuss, e, hg, unten_punkt, kanten=kanten, karten=nr < 2, stirn=False)   # ohne Stufen: an den Graten klafften sie
    # Grate: schmale Kappen von den Ecken zur Spitze
    gv0, gv1, gm = BA[S.get("grat_band", "stroh")]
    for ex, ey in ((0, oy), (Q + u, oy - u), (Q, B), (0, B)):
        p0 = Vector((ex, ey))
        if (ex, ey) != (Q + u, oy - u):     # an der Grenze (links, hinten) beginnt der Grat 0,2 m innen (Rahmen)
            p0 = p0 + (Vector(ap) - p0).normalized() * 0.25
        p1 = Vector(ap)
        quer = Vector((-(p1 - p0).y, (p1 - p0).x)).normalized() * 0.14
        kamm = [Vector((p.x, p.y, dz(p.x, p.y) + 0.06)) for p in (p0, p1)]
        raender = []
        for sg in (1, -1):
            r = [Vector((p.x + quer.x * sg, p.y + quer.y * sg, dz(p.x + quer.x * sg, p.y + quer.y * sg) - 0.02)) for p in (p0, p1)]
            raender.append(r)
            laenge = (kamm[1] - kamm[0]).length
            h.vieleck("Stroh", [r[0], r[1], kamm[1], kamm[0]], [(0.2, gv0 + 0.02), (0.2 + laenge / gm, gv0 + 0.02), (0.2 + laenge / gm, gv0 + 0.06), (0.2, gv0 + 0.06)],
                      weg_von=unten_punkt)
        h.vieleck("Stroh", [raender[0][0], kamm[0], raender[1][0]], [(0.2, gv0 + 0.02), (0.25, gv0 + 0.06), (0.3, gv0 + 0.02)],
                  weg_von=Vector((p1.x, p1.y, kamm[0].z)))
    schornstein(h, B * 0.30, B * 0.70, z_t, spitze.z - 0.6)
    if S["erker"]:
        erker(h, Q, oy)
    return h, anker


def _eck_kante(a, b, ap):
    """Ist die Grundriss-Kante a-b eine Traufe oder ein Ortgang (aussen)? Grate zur Spitze nicht."""
    return not (abs(a[0] - ap[0]) < 1e-6 and abs(a[1] - ap[1]) < 1e-6) and not (abs(b[0] - ap[0]) < 1e-6 and abs(b[1] - ap[1]) < 1e-6)


def erker(h, Q, oy):
    """Eckerker ab dem 1. Obergeschoss an der Ecke vorn rechts (Stufe 2 und 3)."""
    z0, z1 = S["z_og"] - 0.3, S["z_traufe"] - 0.2
    x0, x1, y0, y1 = Q - 1.2, Q + 0.6, oy - 0.6, oy + 1.2
    mitte = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
    # erker_band (nur elb_s3): Erker in Werkstein K5 statt im Mauerband, damit er sich absetzt. Nicht bei den anderen Steinsaetzen: Das
    # Putzband von mensch_s3 und halbling_s3 ist im Spiel einfarbig (Streuung 2 von 255, ohne Fugen), der helle Haustein der Zwerge S3
    # liegt auch in den Eckquadern darunter – der Erker verschmolze mit ihnen zu einem Pfeiler (Bildpruefung 22.11.2026).
    stein = S["og"] == "mauer" or S.get("gefach") == "stein"
    wand_band = S.get("erker_band") or (S["mauer_band"] if stein else "putz")
    # erker_quader (zwerg_s3, halbling_s3; Teamleiter 22.11.2026 nach Bildpruefung Runde 2): Im Mauerband las sich der Erker wie eine
    # Hausecke. Darum Eckquader aus Werkstein (werk_band, wie im Erdgeschoss) an den Erkerkanten; die Waende bleiben im Mauerband.
    quader = S.get("erker_quader")
    for nr, (o, eu, breite) in enumerate((((x0, y0, 0), (1, 0, 0), x1 - x0), ((x1, y0, 0), (0, 1, 0), y1 - y0))):
        eb = Ebene(o, eu, (0, 0, 1), mitte)
        rand = 0.4 if quader else 0.35     # mit Eckquadern (bis 0,3 m) ein Stueck Wand zwischen Quader und Fenster
        loecher = [(rand, breite - rand, fa, fz) for g0, g1, _, eg in stockwerke() if not eg for fa, fz in [fenster_hoehen(g0, g1, False)]]
        felder = erker_quaderfelder(breite, z0, z1, (True, True), nr) if quader else []
        band = wand_band
        lagenflaeche(h, "Bruchstein" if stein else "Lehmputz", eb, [(0, z0), (breite, z0), (breite, z1), (0, z1)],
                     band, BANDLAGE[band], loecher + felder)
        if felder: eckquader(h, eb, felder)
        if S["og"] == "fachwerk" and S.get("fachwerk_echt"):   # Eckpfosten und Fensterrahmen als eine Holzlage
            lage = Holzlage(); S["holzlage"] = lage; S["holzlage_b"] = breite
            for sx in (0.06, breite - 0.06):
                lage.holz((sx, z0), (sx, z1), 0.12, "balken_a", 0, gerade=True)
            for loch in loecher: fenster(h, eb, loch, laeden=False)
            befunde = lage.pruefen()
            if befunde: print("HOLZLAGE Erker", befunde[:3])
            lage.bauen(h, lambda s_, t_, r_: eb.p(s_, t_, r_), eb.n(), tiefe=0.05)
            S["holzlage"] = None
            continue
        if S["og"] == "fachwerk":
            n = eb.n()
            for sx in (0.06, breite - 0.06):
                h.brett("Fachwerk", eb.p(sx, z0), eb.p(sx, z1), 0.12, n, "balken_a", tiefe=0.04)
        for loch in loecher: fenster(h, eb, loch, laeden=False)
    # die zwei kurzen Seiten zwischen Erker und Hauswand (0,6 m, ohne Fenster)
    for o, eu, breite, versatz in (((x0, oy, 0), (0, -1, 0), oy - y0, 1), ((Q, y1, 0), (1, 0, 0), x1 - Q, 0)):
        eb = Ebene(o, eu, (0, 0, 1), mitte)
        # nur an der Aussenkante, gegen die anstossende Erkerwand versetzt (lang trifft kurz)
        felder = erker_quaderfelder(breite, z0, z1, (False, True), versatz) if quader else []
        lagenflaeche(h, "Bruchstein" if stein else "Lehmputz", eb, [(0, z0), (breite, z0), (breite, z1), (0, z1)], wand_band, BANDLAGE[wand_band],
                     felder)
        if felder: eckquader(h, eb, felder)
    # Konsole: drei Lagen Kragstein, jede 0,2 m hoch und zur Hauswand hin um 0,2 m zurueckgesetzt (vorher eine 0,25 m dicke Platte, der
    # die Stirn zur Gasse fehlte: ohne=("-x", "-y") liess die Seite y = y0 weg, die vorn aussen liegt). Nur die Oberseite entfaellt
    # (unter dem Erker bzw. der Lage darueber); was in der Hauswand steckt, sieht man nicht.
    # Band der Konsole: mit Erker-Eckquadern der Werkstein (Zwerge S3: heller Haustein mit Fugen; Halblinge S3: Werkstein der Eckquader,
    # im Spiel einfarbig), sonst wie die Erkerwand bei den Steinsaetzen (Mauerwerk mit Fugen), sonst Bruchstein wie vorher
    band_k = S["werk_band"] if quader else wand_band if stein else "bruchstein"
    for lage_nr in range(3):
        r = 0.2 * lage_nr
        kragstein(h, Vector((x0, y0 + r, z0 - 0.2 * (lage_nr + 1))), Vector((x1 - r, y1, z0 - 0.2 * lage_nr)), band_k)
    spitze = Vector(((x0 + x1) / 2, (y0 + y1) / 2, z1 + 1.1))
    ecken = [Vector((x0 - 0.08, y0 - 0.08, z1)), Vector((x1 + 0.08, y0 - 0.08, z1)), Vector((x1 + 0.08, y1 + 0.08, z1)), Vector((x0 - 0.08, y1 + 0.08, z1))]
    v0, v1, m = BA["stroh_franse"]
    for a, b in zip(ecken, ecken[1:] + ecken[:1]):
        h.vieleck("Stroh", [a, b, spitze], [(0, v1), ((b - a).length / m, v1), ((b - a).length / m / 2, v0)], weg_von=mitte)


def kragstein(h, mn, mx, band):
    """Eine Lage der Erkerkonsole: achsparalleler Kasten ohne Oberseite. Anders als Teil.kasten liest jede Seite nur den Teil des Bands,
    der ihrer Hoehe entspricht (BANDLAGE), sonst waere das Putzband (1,6 m) auf 0,2 m zusammengedrueckt."""
    mitte = (mn + mx) / 2
    lage = BANDLAGE[band]
    dx, dy, dz = mx.x - mn.x, mx.y - mn.y, mx.z - mn.z
    ex, ey, ez = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    seiten = [(Vector((mn.x, mn.y, mn.z)), ex, ez, dx, dz), (Vector((mn.x, mx.y, mn.z)), ex, ez, dx, dz),
              (Vector((mn.x, mn.y, mn.z)), ey, ez, dy, dz), (Vector((mx.x, mn.y, mn.z)), ey, ez, dy, dz),
              (Vector((mn.x, mn.y, mn.z)), ex, ey, dx, dy)]
    for o, eu, ev, b, hh in seiten:
        h.rechteck("Bruchstein", o, eu, ev, b, hh, band, anteil=(0.0, min(1.0, hh / lage)), weg_von=mitte)


# ── Keilstueck und Hinterhaus ────────────────────────────────────────────────────────────────────────────────────────

def mauer(modul, laenge=1.0):
    """Hofmauer: 1 m langes Stueck (x 0 bis 1), 0,45 m dick (y 0 bis 0,45), mit Abdeckung; das Spiel reiht es entlang der Sehne."""
    h = Teil(modul, BA, saat=71)
    d = 0.45
    for y, n in ((0.0, -1), (d, 1)):
        eb = Ebene((0, y, 0), (1, 0, 0), (0, 0, 1), (laenge / 2, d / 2, 1))
        lagenflaeche(h, "Bruchstein", eb, [(0, 0), (laenge, 0), (laenge, MAUER_HOCH), (0, MAUER_HOCH)], "bruchstein", 0.4, u_saat=0.31 if n < 0 else 0.77)
    h.kasten("Bruchstein", (-0.01, -0.05, MAUER_HOCH), (laenge + 0.01, d + 0.05, MAUER_HOCH + 0.12), "bruchstein")
    return h, []


def pfeiler(modul):
    """Mauerpfeiler (Anfang, Ende, Knick des Keilstuecks): 0,6 x 0,6 m, 0,3 m hoeher als die Mauer."""
    h = Teil(modul, BA, saat=73)
    b, hoch = 0.6, MAUER_HOCH + 0.3
    for k, (o, eu) in enumerate((((-b / 2, -b / 2, 0), (1, 0, 0)), ((b / 2, -b / 2, 0), (0, 1, 0)), ((b / 2, b / 2, 0), (-1, 0, 0)), ((-b / 2, b / 2, 0), (0, -1, 0)))):
        eb = Ebene(o, eu, (0, 0, 1), (0, 0, 1))
        lagenflaeche(h, "Bruchstein", eb, [(0, 0), (b, 0), (b, hoch), (0, hoch)], "bruchstein", 0.4, u_saat=0.17 * k)
    h.kasten("Bruchstein", (-b / 2 - 0.05, -b / 2 - 0.05, hoch), (b / 2 + 0.05, b / 2 + 0.05, hoch + 0.15), "bruchstein")
    return h, []


def tor(modul):
    """Hoftor (3 m, x 0 bis 3): zwei Torfluegel aus Bohlen zwischen Mauerstuecken, Sturzbalken mit kleinem Dach."""
    h = Teil(modul, BA, saat=79)
    d = 0.45
    for y, n in ((0.0, -1), (d, 1)):
        innen = (1.5, d / 2, 1)
        h.rechteck("Fachwerk", (0.1, y + n * -0.1, 0), (1, 0, 0), (0, 0, 1), 2.8, 2.3, "planken", laengs="v", weg_von=innen)
    h.stab("Fachwerk", (-0.1, d / 2, 2.45), (3.1, d / 2, 2.45), 0.22, "balken_c", breite=0.30, kappen=True)
    v0, v1, m = BA["stroh_franse"]
    for seite in (-1, 1):
        a, b = Vector((-0.2, d / 2 + seite * 0.55, 2.55)), Vector((3.2, d / 2 + seite * 0.55, 2.55))
        c, e = Vector((3.2, d / 2, 2.95)), Vector((-0.2, d / 2, 2.95))
        h.vieleck("Stroh", [a, b, c, e], [(0, v1), (3.4 / m, v1), (3.4 / m, v0), (0, v0)], weg_von=(1.5, d / 2, 2.0))
    return h, []


def hinterhaus(modul):
    """Schuppen 4 x 4 m mit Pultdach (nur Kulisse): vorn 3,0 m, hinten 2,2 m hoch, Bretterwaende auf Bruchsteinsockel."""
    h = Teil(modul, BA, saat=83)
    B, z_v, z_h = 4.0, 3.0, 2.2
    innen = (B / 2, B / 2, 1.0)
    z_at = lambda y: z_v + (z_h - z_v) * y / B
    for o, eu, art in (((0, 0, 0), (1, 0, 0), "vorn"), ((B, 0, 0), (0, 1, 0), "rechts"), ((B, B, 0), (-1, 0, 0), "hinten"), ((0, B, 0), (0, -1, 0), "links")):
        eb = Ebene(o, eu, (0, 0, 1), innen)
        if art in ("vorn", "hinten"):
            z = z_v if art == "vorn" else z_h
            poly = [(0, 0.3), (B, 0.3), (B, z), (0, z)]
        else:
            za, zb = (z_at(0), z_at(B)) if art == "rechts" else (z_at(B), z_at(0))
            poly = [(0, 0.3), (B, 0.3), (B, zb), (0, za)]
        loecher = [(1.4, 2.6, 0.3, 2.2)] if art == "vorn" else []
        lagenflaeche(h, "Fachwerk", eb, poly, "planken", 0.5, loecher, u_saat=h.zufall.random())
        lagenflaeche(h, "Bruchstein", eb, [(0, 0), (B, 0), (B, 0.3), (0, 0.3)], "bruchstein", 0.4, u_saat=h.zufall.random())
    h.rechteck("Fachwerk", (1.4, 0.12, 0.3), (1, 0, 0), (0, 0, 1), 1.2, 1.9, "planken", laengs="v", weg_von=innen)
    v0, v1, m = BA["stroh_franse"]
    ecken = [Vector((-0.3, -0.4, z_v + 0.1)), Vector((B + 0.3, -0.4, z_v + 0.1)), Vector((B + 0.3, B + 0.3, z_h + 0.04)), Vector((-0.3, B + 0.3, z_h + 0.04))]
    neig = math.atan2(z_v - z_h, B)
    poly = [(0, 0), (B + 0.6, 0), (B + 0.6, (B + 0.7) / math.cos(neig)), (0, (B + 0.7) / math.cos(neig))]
    dachflaeche(h, poly, ecken[3], (1, 0, 0), (ecken[0] - ecken[3]).normalized(), (B / 2, B / 2, 1.0), kanten=(0, 1, 2, 3), karten=False)
    return h, []


# ── Bau, Backen, Export ──────────────────────────────────────────────────────────────────────────────────────────────

BAUER = {
    "k_g4": lambda m: koerper_giebel(4, m), "k_g6": lambda m: koerper_giebel(6, m), "k_g8": lambda m: koerper_giebel(8, m),
    "k_t6": lambda m: koerper_trauf(6, m), "k_t8": lambda m: koerper_trauf(8, m),
    "seite_blind": lambda m: seite_giebelhaus(False, m), "seite_offen": lambda m: seite_giebelhaus(True, m),
    "kappe_brand": lambda m: kappe(True, m), "kappe_offen": lambda m: kappe(False, m), "gasse": gasse, "eck": eckhaus,
    "mauer": mauer, "pfeiler": pfeiler, "tor": tor, "hinterhaus": hinterhaus,
}
BAUER = {_m: mit_fachwerk(_f) for _m, _f in BAUER.items()}
for _v in VARIANTEN:
    _grund = _v.rsplit("_", 1)[0]
    BAUER[_v] = (lambda grund: lambda m: mit_variante(m, lambda: BAUER[grund](m)))(_grund)

# Breite je Modul (fuer Blindnachbarn beim Backen, Kontrollbilder)
BREITE = {"k_g4": 4, "k_g6": 6, "k_g8": 8, "k_t6": 6, "k_t8": 8, "eck": 8}
BREITE.update({v: BREITE[v.rsplit("_", 1)[0]] for v in VARIANTEN if v.rsplit("_", 1)[0] in BREITE})


def stoffe_anlegen(blatt):
    s = g1.stoff
    return {
        "Stroh": s("Stroh", blatt), "Strohkante": s("Strohkante", blatt, schere=True), "Fachwerk": s("Fachwerk", blatt),
        "Lehmputz": s("Lehmputz", blatt), "Bruchstein": s("Bruchstein", blatt),
        "Fenster": s("Fenster", farbe=(0.09, 0.11, 0.14), rauheit=0.2),
    }


def ao_gruppe_eintragen(gruppe):
    """Traegt die Gruppe zeile_<satz> mit einem Platz je Modul in ao_atlas.json ein (4 x 4 Zellen zu 512 Bildpunkten)."""
    pfad = os.path.join(HIER, "ao_atlas.json")
    daten = json.load(open(pfad, encoding="utf-8"))
    zellen = 4 if len(MODULE) <= 16 else 8
    eintrag = {"datei": f"{gruppe}_ao_atlas.png", "kante": 2048, "zellen": zellen, "plaetze": {m: i for i, m in enumerate(MODULE)}}
    if daten.get(gruppe) != eintrag:
        if (daten.get(gruppe) or {}).get("zellen") != zellen:
            # neue Aufteilung: der Atlas beginnt weiss (sonst blieben Reste der alten Zellen stehen)
            pfad_bild = os.path.join(g1.ZIEL_TEXTUR, eintrag["datei"])
            if os.path.exists(pfad_bild):
                weiss = bpy.data.images.new("AO-Neu", 2048, 2048, alpha=False)
                weiss.colorspace_settings.name = "Non-Color"
                weiss.pixels.foreach_set([1.0] * (2048 * 2048 * 4))
                weiss.filepath_raw = pfad_bild; weiss.file_format = "PNG"; weiss.save()
                bpy.data.images.remove(weiss)
        daten[gruppe] = eintrag
        text = json.dumps(daten, ensure_ascii=False, indent=2)
        open(pfad, "w", encoding="utf-8", newline="\n").write(text + "\n")


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    satz = opt.get("satz", "mensch_s1")
    stil(satz)
    gruppe = f"zeile_{satz}"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    stoffe = stoffe_anlegen(S["blatt"])
    nur = opt.get("nur", "").split(",") if opt.get("nur") else MODULE
    teile, anker = {}, []
    for m in nur:
        t, a = BAUER[m](m)
        teile[m] = t; anker += a
        print(f"DREIECKE {m}: {t.dreiecke()}")
    objekte = {m: t.objekt(stoffe) for m, t in teile.items()}
    ao_gruppe_eintragen(gruppe)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0))
    boden = bpy.context.active_object; boden.name = "Kontrollboden"
    boden.data.materials.append(g1.stoff("Kontrollboden", farbe=(0.33, 0.3, 0.24)))
    mit_ao = "ohne-ao" not in opt
    def kopie(quelle, dx):
        """Verknuepfte Kopie eines Moduls als Blindnachbar (wirft nur Schatten, wird nicht exportiert)."""
        k = objekte[quelle].copy(); k.name = "Blind_" + quelle
        k.location = (dx, 0, 0); bpy.context.scene.collection.objects.link(k)
        return k
    for m, o in objekte.items():
        # Kontext-Backen (Konzept 4.8): echte Nachbarmodule werfen Schatten. Koerper: gleichartige Nachbarn links und rechts; Seitenstuecke
        # und Kappen: der eigene Koerper dahinter; Traufgasse: Giebelhaeuser auf beiden Seiten. Die blinde Traufseite wird ohne Nachbarn
        # gebacken (sichtbar ist sie nur ueber einem niedrigeren Nachbarn).
        w = BREITE.get(m, 1.0)
        if m.startswith("k_g"):
            nachbarn = [kopie("k_g6", -6.0), kopie("k_g6", w)] if "k_g6" in objekte else []
        elif m.startswith("k_t"):
            nachbarn = [kopie("k_t6", -6.0), kopie("k_t6", w)] if "k_t6" in objekte else []
        elif m.startswith("seite_"):
            nachbarn = [kopie("k_g6", 0.0)] if "k_g6" in objekte else []
        elif m in ("kappe_brand", "kappe_offen"):
            nachbarn = [kopie("k_t6", 0.0)] if "k_t6" in objekte else []
        elif m == "gasse":
            nachbarn = [kopie("k_g6", 0.0), kopie("k_g6", -6.0)] if "k_g6" in objekte else []
        elif m.startswith("eck"):
            nachbarn = [kopie("k_g6", -6.0)] if "k_g6" in objekte else []
        else:
            nachbarn = []
        g1.uv2_und_ao([([o], [boden] + nachbarn)], gruppe, m, mit_ao=mit_ao)
        for n in nachbarn: bpy.data.objects.remove(n, do_unlink=True)
    anker_obj = []
    for name, ort, n in anker:
        e = bpy.data.objects.new(name, None)
        e.location = ort
        e.rotation_euler = (0, 0, math.atan2(-n.x, n.y))     # -Z im Spiel zeigt zur Gasse (aus dem Haus heraus), wie g1.anker
        bpy.context.scene.collection.objects.link(e)
        anker_obj.append(e)
    os.makedirs(ZIEL_MODELL, exist_ok=True)
    if "nur" not in opt:
        g1.exportiere(list(objekte.values()) + anker_obj, os.path.join(ZIEL_MODELL, f"zeile_{satz}.glb"))
    print("SUMME", {m: t.dreiecke() for m, t in teile.items()})


if __name__ == "__main__":
    main()
