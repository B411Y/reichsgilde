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
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from netzbau import ROT, SCHWARZ, Teil                     # noqa: E402
from trimsheet import BAENDER_AUSSEN as BA                 # noqa: E402
import wohnhaus_mensch_s1_g1 as g1                         # noqa: E402  (Stoffe, UV2 und AO, Export, Anker)

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
ZIEL_MODELL = os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "zeilen")
EZ = Vector((0, 0, 1))

# ── Saetze: Volk x Stufe ──────────────────────────────────────────────────────────────────────────────────────────────
# blatt: Trim-Sheet fuer die Kontrollbilder (im Spiel waehlt die Ebene des Haeuser-Arrays, Steinebenen.cs)
# eg/og: "fachwerk" | "stein" | "putz"; dach: "stroh" (Stufen zu 1,55 m im Band „stroh“) | "ziegel" (Lagen zu 0,25 m im Band „stroh_franse“)
SAETZE = {
    "mensch_s1": dict(blatt="trim", eg="fachwerk", og="fachwerk", dach="stroh", z_og=3.0, z_traufe=5.6, neig_g=50, neig_t=45,
                      sockel="bruchstein", erker=False),
}
MODULE = ["k_g4", "k_g6", "k_g8", "k_t6", "k_t8", "seite_blind", "seite_offen", "kappe_brand", "kappe_offen", "gasse", "eck",
          "mauer", "pfeiler", "tor", "hinterhaus"]

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
    S["tg"], S["tt"] = math.tan(math.radians(S["neig_g"])), math.tan(math.radians(S["neig_t"]))
    S["dick"] = 0.28 if S["dach"] == "stroh" else 0.12


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
            s_von = l[1]
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
    laibung(teil, ebene, loch, 0.12)
    for a, b in (((s0, t0), (s1, t0)), ((s0, t1), (s1, t1)), ((s0, t0), (s0, t1)), ((s1, t0), (s1, t1))):
        teil.brett("Fachwerk", ebene.p(*a, 0.0), ebene.p(*b, 0.0), 0.07, n, "balken_c", tiefe=0.03)
    teil.brett("Fachwerk", ebene.p((s0 + s1) / 2, t0, -0.06), ebene.p((s0 + s1) / 2, t1, -0.06), 0.05, n, "balken_c", tiefe=0.02, seiten=False)
    pts = [ebene.p(s0, t0, -0.10), ebene.p(s1, t0, -0.10), ebene.p(s1, t1, -0.10), ebene.p(s0, t1, -0.10)]
    teil.vieleck("Fenster", pts, [(0, 0), (1, 0), (1, 1), (0, 1)], weg_von=ebene.innen)
    if laeden and S["og"] == "fachwerk":
        breite = (s1 - s0) / 2
        for sa in (s0 - breite - 0.04, s1 + 0.04):
            teil.rechteck("Fachwerk", ebene.p(sa, t0, 0.025), ebene.eu, ebene.ev, breite, t1 - t0, "farbholz", laengs="v", weg_von=ebene.innen, farbe=ROT)


def tuer(teil, ebene, loch, anker_liste, modul):
    s0, s1, t0, t1 = loch
    n = ebene.n()
    laibung(teil, ebene, loch, 0.14)
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
    teil.brett("Fachwerk", hz(0, z0 + 0.09), hz(b, z0 + 0.09), 0.18, n, "balken_c", tiefe=0.05)
    teil.brett("Fachwerk", hz(0, z1 - 0.08), hz(b, z1 - 0.08), 0.16, n, "balken_a", tiefe=0.05)
    staender = {BW / 2, b - BW / 2}
    for s0, s1, t0, t1, _ in oeffnungen:
        staender |= {s0 - BW / 2, s1 + BW / 2}
    staender = sorted(s for s in staender if BW / 2 - 1e-6 <= s <= b - BW / 2 + 1e-6)
    for s in staender:
        teil.brett("Fachwerk", hz(s, z0 + 0.18), hz(s, z1 - 0.16), BW, n, "balken_a", tiefe=0.045, laengs="u")
    fenster_t = [(t0, t1) for _, _, t0, t1, art in oeffnungen if art == "fenster"]
    for sa, sb in zip(staender[:-1], staender[1:]):
        feld_a, feld_b = sa + BW / 2, sb - BW / 2
        if feld_b - feld_a < 0.25: continue
        if any(o[0] >= feld_a - 1e-3 and o[1] <= feld_b + 1e-3 for o in oeffnungen): continue   # die Oeffnung fuellt das Feld
        hoehen = sorted({t for ft in fenster_t for t in ft}) if fenster_t else ([z0 + (z1 - z0) * 0.48] if riegel_mitte else [])
        for t in hoehen:
            teil.brett("Fachwerk", hz(feld_a, t), hz(feld_b, t), RIEGEL, n, "balken_b", tiefe=0.04)
        if feld_a < 0.4 or feld_b > b - 0.4:          # Endfeld: Strebe (Mann-Figur angedeutet)
            unten, oben = (feld_a, z0 + 0.18), (feld_b, z1 - 0.16)
            if feld_b > b - 0.4: unten, oben = (feld_b, z0 + 0.18), (feld_a, z1 - 0.16)
            teil.brett("Fachwerk", hz(*unten), hz(*oben), 0.15, n, "balken_b", tiefe=0.035)
    # Bruestungsriegel unter den Fenstern und Sturzriegel darueber (in den Fensterfeldern selbst)
    for s0, s1, t0, t1, art in oeffnungen:
        if art != "fenster": continue
        teil.brett("Fachwerk", hz(s0, t0 - 0.07), hz(s1, t0 - 0.07), RIEGEL, n, "balken_b", tiefe=0.04)
        teil.brett("Fachwerk", hz(s0, t1 + 0.07), hz(s1, t1 + 0.07), RIEGEL, n, "balken_b", tiefe=0.04)


def geschoss(teil, ebene, b, z0, z1, oeffnungen, art, anker, modul, sockel=False):
    """Eine Geschosswand der Breite b (s von 0 bis b) mit Oeffnungen (s0, s1, t0, t1, "fenster"|"tuer"); art fachwerk|stein|putz."""
    loecher = [(o[0], o[1], o[2], o[3]) for o in oeffnungen]
    poly = [(0, z0), (b, z0), (b, z1), (0, z1)]
    if sockel and z0 < Z_SOCKEL:
        unten = [(0, z0), (b, z0), (b, Z_SOCKEL), (0, Z_SOCKEL)]
        lagenflaeche(teil, "Bruchstein", ebene, unten, "bruchstein", 0.4, [l for l in loecher if l[2] < Z_SOCKEL], raus=0.03, u_saat=teil.zufall.random())
        poly = [(0, Z_SOCKEL), (b, Z_SOCKEL), (b, z1), (0, z1)]
    if art == "stein":
        lagenflaeche(teil, "Bruchstein", ebene, poly, "bruchstein", 0.4, loecher, u_saat=teil.zufall.random())
    else:
        lagenflaeche(teil, "Lehmputz", ebene, poly, "putz", max(z1 - poly[0][1], 0.5), loecher, u_saat=teil.zufall.random())
        if art == "fachwerk": fachwerk_geschoss(teil, ebene, b, poly[0][1], z1, oeffnungen)
    for o in oeffnungen:
        if o[4] == "fenster": fenster(teil, ebene, o[:4])
        else: tuer(teil, ebene, o[:4], anker, modul)


def achsen(b, n, breite):
    """n Fensterachsen gleichmaessig in einer Wand der Breite b: Liste (s0, s1)."""
    teil = b / n
    return [(teil * (i + 0.5) - breite / 2, teil * (i + 0.5) + breite / 2) for i in range(n)]


def fassade(teil, ebene, b, oben_z, krag_ebene, anker, modul, n_eg, n_og, mit_tuer, tuer_links=True, b_og=None):
    """Erdgeschoss und Obergeschoss einer Schauseite (Breite b, oben b_og). `krag_ebene`: Ebene des auskragenden Obergeschosses."""
    z_og, z_t = S["z_og"], oben_z
    b_og = b if b_og is None else b_og
    eg = []
    plaetze = achsen(b, n_eg + (1 if mit_tuer else 0), 0.75)
    for i, (s0, s1) in enumerate(plaetze):
        if mit_tuer and i == (0 if tuer_links else len(plaetze) - 1):
            m = (s0 + s1) / 2
            eg.append((m - 0.48, m + 0.48, 0.0, 2.15, "tuer"))
        else:
            eg.append((s0, s1, 1.10, 2.10, "fenster"))
    geschoss(teil, ebene, b, 0.0, z_og, eg, S["eg"], anker, modul, sockel=True)
    og = [(s0, s1, z_og + 0.85, z_og + 1.95, "fenster") for s0, s1 in achsen(b_og, n_og, 0.75)]
    geschoss(teil, krag_ebene, b_og, z_og, z_t, og, S["og"], anker, modul)


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


# ── Dach ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

def dachflaeche(d, poly_sq, fuss, entlang, hang, unten_punkt, kanten=(), karten=True):
    """Eine Dachseite als konvexes Vieleck (s entlang der Traufe, q hangaufwaerts ab `fuss`) auf der Oberseite der Deckung:
    Stroh in Stufen zu 1,55 m (Band „stroh“), Ziegel/Schiefer in Lagen zu 0,25 m (Band „stroh_franse“), jede Stufe am Fuss etwas
    angehoben (Stirn). `kanten`: Indizes der Randkanten (i, i+1) mit Stirnflaeche der Deckung (Traufe, Ortgang). `unten_punkt`: unter dem Dach."""
    fuss, entlang, hang = Vector(fuss), Vector(entlang).normalized(), Vector(hang).normalized()
    normale = entlang.cross(hang).normalized()
    if normale.z < 0: normale = -normale
    stroh = S["dach"] == "stroh"
    band = "stroh" if stroh else "stroh_franse"
    lage, stufe = (1.55, 0.07) if stroh else (0.25, 0.025)
    v0, v1, m = BA[band]
    P = lambda s, q, h=0.0: fuss + entlang * s + hang * q + normale * h
    q_max = max(q for _, q in poly_sq)
    n = max(1, int(math.ceil(q_max / lage - 1e-6)))
    u_basis = d.zufall.random()
    for k in range(n):
        streifen = ausschnitt(poly_sq, t0=k * lage, t1=(k + 1) * lage)
        if not streifen: continue
        uu = u_basis + 0.37 * k
        for f in flaeche_teilen(streifen):
            pts = [P(s, q, stufe if abs(q - k * lage) < 1e-6 else 0.0) for s, q in f]
            uvs = [(uu + s / m, v1 - (q - k * lage) / lage * (v1 - v0)) for s, q in f]
            d.vieleck("Stroh", pts, uvs, weg_von=unten_punkt)
        # Stirn der Stufe am Fuss
        unten = sorted((p for p in streifen if abs(p[1] - k * lage) < 1e-6), key=lambda p: p[0])
        if len(unten) >= 2:
            a, b = unten[0], unten[-1]
            d.vieleck("Stroh", [P(a[0], a[1]), P(b[0], b[1]), P(b[0], b[1], stufe), P(a[0], a[1], stufe)],
                      [(uu, v1 - 0.002), (uu + (b[0] - a[0]) / m, v1 - 0.002), (uu + (b[0] - a[0]) / m, v1 - 0.012), (uu, v1 - 0.012)],
                      weg_von=P((a[0] + b[0]) / 2, a[1] + 0.3, -0.5))
    # Unterseite (Bohlen) und Stirn der Deckung an den genannten Kanten
    dick = S["dick"]
    pv0, pv1, pm = BA["planken"]
    for f in flaeche_teilen(poly_sq):
        pts = [P(s, q, -dick) for s, q in f]
        d.vieleck("Fachwerk", pts, [(s / pm, pv0 + (pv1 - pv0) * ((q / 3.0) % 1.0)) for s, q in f], zu=unten_punkt)
    for i in kanten:
        a, b = poly_sq[i], poly_sq[(i + 1) % len(poly_sq)]
        laenge = math.hypot(b[0] - a[0], b[1] - a[1])
        mitte = P((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, -dick / 2)
        innen = mitte + (P((a[0] + b[0]) / 2, (a[1] + b[1]) / 2) - P(*_schwerpunkt(poly_sq))).normalized() * -0.5
        d.vieleck("Stroh", [P(*a, -dick), P(*b, -dick), P(*b), P(*a)], [(0.3, 0.23), (0.3 + laenge / m, 0.23), (0.3 + laenge / m, 0.245), (0.3, 0.245)],
                  weg_von=innen)
        if stroh and karten and abs(a[1]) < 1e-6 and abs(b[1]) < 1e-6:        # Traufe: Fransen (Alpha-Cards)
            f0, f1, fm = BA["stroh_franse"]
            haengt = (hang * -0.75 + Vector((0, 0, -0.25))).normalized() * 0.26
            ka, kb = P(*a, -0.02), P(*b, -0.02)
            d.vieleck("Strohkante", [ka, kb, kb + haengt, ka + haengt], [(0.1, f0), (0.1 + laenge / fm, f0), (0.1 + laenge / fm, f1), (0.1, f1)], weg_von=unten_punkt)


def _schwerpunkt(poly):
    return (sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly))


def first(d, a, b, seite_a, seite_b, unten_punkt):
    """Firstwulst bzw. Firstziegel: zwei schmale Flaechen ueber dem First von a nach b."""
    a, b = Vector(a), Vector(b)
    band = "stroh"
    v0, v1, m = BA[band]
    for seite in (seite_a, seite_b):
        seite = Vector(seite)
        kamm_a, kamm_b = a + EZ * (0.14 if S["dach"] == "stroh" else 0.07), b + EZ * (0.14 if S["dach"] == "stroh" else 0.07)
        rand_a, rand_b = a + seite, b + seite
        laenge = (b - a).length
        d.vieleck("Stroh", [rand_a, rand_b, kamm_b, kamm_a], [(0.5, v0 + 0.06), (0.5 + laenge / m, v0 + 0.06), (0.5 + laenge / m, v0 + 0.11), (0.5, v0 + 0.11)], weg_von=unten_punkt)


def schornstein(h, x, y, z_unten, z_oben, b=0.55):
    """Bruchsteinschlot durchs Dach."""
    for k, (o, eu, breite) in enumerate((((x - b / 2, y - b / 2, 0), (1, 0, 0), b), ((x + b / 2, y - b / 2, 0), (0, 1, 0), b),
                                          ((x + b / 2, y + b / 2, 0), (-1, 0, 0), b), ((x - b / 2, y + b / 2, 0), (0, -1, 0), b))):
        eb = Ebene(o, eu, (0, 0, 1), (x, y, z_unten))
        lagenflaeche(h, "Bruchstein", eb, [(0, z_unten), (breite, z_unten), (breite, z_oben), (0, z_oben)], "bruchstein", 0.4, u_saat=0.21 * k)
    h.kasten("Bruchstein", (x - b / 2 - 0.05, y - b / 2 - 0.05, z_oben), (x + b / 2 + 0.05, y + b / 2 + 0.05, z_oben + 0.10), "bruchstein", ohne=("-z",))


# ── Koerper ───────────────────────────────────────────────────────────────────────────────────────────────────────────

def koerper_giebel(w, modul):
    """Giebelstaendiger Koerper der Breite w (4/6/8 m): Waende x von EIN bis w - EIN, Satteldach mit First entlang y bis an die Grenzen."""
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
    geschoss(h, hinten, b, 0.0, S["z_og"], [(s0, s1, 1.1, 2.1, "fenster") for s0, s1 in achsen(b, max(1, n_og - 1), 0.7)], S["eg"], anker, modul, sockel=True)
    geschoss(h, hinten, b, S["z_og"], z_t, [(s0, s1, S["z_og"] + 0.85, S["z_og"] + 1.9, "fenster") for s0, s1 in achsen(b, max(1, n_og - 1), 0.7)], S["og"], anker, modul)
    # Giebeldreiecke vorn und hinten bis unter die Dachflaeche
    tg = S["tg"]
    first_z = z_t + (w / 2 - EIN) * tg
    for eb, y in ((vorn_og, -krag), (hinten, TIEFE)):
        poly = [(0, z_t), (b, z_t), (b / 2, first_z - 0.02)]
        loch = (b / 2 - 0.30, b / 2 + 0.30, z_t + 0.55, z_t + 1.35) if first_z - z_t > 2.1 else None
        art = S["og"]
        if art == "stein":
            lagenflaeche(h, "Bruchstein", eb, poly, "bruchstein", 0.4, [loch] if loch else [], u_saat=h.zufall.random())
        else:
            lagenflaeche(h, "Lehmputz", eb, poly, "putz", first_z - z_t, [loch] if loch else [], u_saat=h.zufall.random())
            n = eb.n()
            # Fachwerk im Giebel: Rähm, Ständer neben der Luke und in der Mitte, Kehlbalken
            for s in ([loch[0] - BW / 2, loch[1] + BW / 2] if loch else [b / 2]):
                oben = first_z - abs(s - b / 2) * tg - 0.12
                if oben > z_t + 0.3: h.brett("Fachwerk", eb.p(s, z_t), eb.p(s, oben), BW, n, "balken_a", tiefe=0.04)
            kz = z_t + (first_z - z_t) * 0.55
            ks = (first_z - kz) / tg
            h.brett("Fachwerk", eb.p(b / 2 - ks + 0.08, kz), eb.p(b / 2 + ks - 0.08, kz), RIEGEL, n, "balken_b", tiefe=0.04)
        if loch: fenster(h, eb, loch, laeden=False)
    # Dach: zwei Seiten, Traufe an der Parzellengrenze (x = 0 bzw. w)
    dick = S["dick"]
    y0, y1 = -krag - ORTGANG, TIEFE + ORTGANG
    z_fuss = z_t - EIN * tg + dick / math.cos(math.radians(S["neig_g"]))
    breite_hang = (w / 2) / math.cos(math.radians(S["neig_g"]))
    unten_punkt = (w / 2, TIEFE / 2, z_t)
    for seite in (-1, 1):
        x_tr = 0.0 if seite < 0 else w
        fuss = Vector((x_tr, y1 if seite < 0 else y0, z_fuss))
        entlang = Vector((0, -1, 0)) if seite < 0 else Vector((0, 1, 0))
        hang = Vector((-seite * (w / 2), 0, (w / 2) * tg)).normalized()
        laenge = y1 - y0
        poly = [(0, 0), (laenge, 0), (laenge, breite_hang), (0, breite_hang)]
        dachflaeche(h, poly, fuss, entlang, hang, unten_punkt, kanten=(0, 1, 3))
    zf = z_t + (w / 2 - EIN) * tg + dick / math.cos(math.radians(S["neig_g"]))
    first(h, (w / 2, y0, zf), (w / 2, y1, zf), (-0.35, 0, -0.35 * tg + 0.1), (0.35, 0, -0.35 * tg + 0.1), unten_punkt)
    # Windbretter am Ortgang
    for y, aussen in ((y0, -1), (y1, 1)):
        for seite in (-1, 1):
            a = Vector((w / 2 + seite * (w / 2), y, z_fuss - dick - 0.02)); bb = Vector((w / 2, y, zf - 0.04))
            h.brett("Fachwerk", a, bb, 0.20, (0, aussen, 0), "balken_c", tiefe=0.03)
    if w >= 6:
        schornstein(h, w / 2 + 0.9, TIEFE * 0.62, z_t, zf + 0.7)
    return h, anker


def koerper_trauf(w, modul):
    """Traufstaendiger Koerper der Breite w (6/8 m): Waende bis an die Grenzen, First entlang x, keine Seitenwaende."""
    h = Teil(modul, BA, saat=sum(map(ord, modul)))
    anker = []
    z_t = S["z_traufe"]
    krag = S["krag"]
    innen = (w / 2, TIEFE / 2, 2.0)
    vorn = Ebene((0, 0, 0), (1, 0, 0), (0, 0, 1), innen)
    vorn_og = Ebene((0, -krag, 0), (1, 0, 0), (0, 0, 1), innen)
    n_og = {6: 3, 8: 4}[w]
    fassade(h, vorn, w, z_t, vorn_og, anker, modul, n_og - 1, n_og, True, tuer_links=False)
    krag_boden(h, 0.0, w, 0.0, -krag, S["z_og"], -1)
    hinten = Ebene((w, TIEFE, 0), (-1, 0, 0), (0, 0, 1), innen)
    geschoss(h, hinten, w, 0.0, S["z_og"], [(s0, s1, 1.1, 2.1, "fenster") for s0, s1 in achsen(w, n_og - 1, 0.7)], S["eg"], anker, modul, sockel=True)
    geschoss(h, hinten, w, S["z_og"], z_t, [(s0, s1, S["z_og"] + 0.85, S["z_og"] + 1.9, "fenster") for s0, s1 in achsen(w, n_og - 1, 0.7)], S["og"], anker, modul)
    trauf_dach(h, 0.0, w, z_t, krag, kanten_enden=False)
    zf = trauf_first_z(z_t, krag)
    schornstein(h, w * 0.68, (TIEFE - krag) / 2 + 0.8, z_t, zf + 0.6)
    return h, anker


def trauf_first_z(z_t, krag):
    return z_t + ((TIEFE + krag) / 2) * S["tt"] + S["dick"] / math.cos(math.radians(S["neig_t"]))


def trauf_dach(h, x0, x1, z_t, krag, kanten_enden):
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
        dachflaeche(h, poly, fuss, entlang, hang, unten_punkt, kanten=(0, 1, 3) if kanten_enden else (0,))
    zf = trauf_first_z(z_t, krag)
    first(h, (x0, y_first, zf), (x1, y_first, zf), (0, -0.35, -0.35 * tt + 0.1), (0, 0.35, -0.35 * tt + 0.1), unten_punkt)


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
    if offen:
        eg_l = [(s0, s1, 1.1, 2.1, "fenster") for s0, s1 in achsen(TIEFE, 3, 0.7)]
        og_l = [(s0, s1, z_og + 0.85, z_og + 1.9, "fenster") for s0, s1 in achsen(TIEFE + krag, 3, 0.7)]
    else:
        eg_l, og_l = [], []
    geschoss(h, eg, TIEFE, 0.0, z_og, eg_l, S["eg"] if offen else ("stein" if S["eg"] == "stein" else "putz"), anker, modul, sockel=True)
    geschoss(h, eg, TIEFE + krag, z_og, z_t, og_l, S["og"] if offen else ("stein" if S["og"] == "stein" else "putz"), anker, modul)
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
        stuecke = [[(0, 0), (TIEFE, 0), (TIEFE, z_og), (0, z_og)],
                   [(0, z_og), (s_vorn_og, z_og), (s_vorn_og, z_t), (0, z_t)],
                   [(s_tr_h, z_t), (s_tr_v, z_t), (s_tr_v, z_tr + ueber), (s_first, z_first + ueber), (s_tr_h, z_tr + ueber)],
                   [(s_tr_h, z_tr - 0.15), (0, z_tr - 0.15), (0, z_t), (s_tr_h, z_t)],
                   [(s_vorn_og, z_tr - 0.15), (s_tr_v, z_tr - 0.15), (s_tr_v, z_t), (s_vorn_og, z_t)]]
        innen_eb = Ebene((d, TIEFE, 0), (0, -1, 0), (0, 0, 1), (-1.0, TIEFE / 2, 2.0))
        for k, poly in enumerate(stuecke):
            lagenflaeche(h, "Bruchstein", eb, poly, "bruchstein", 0.4, u_saat=0.13 * k)
            if k == 2:   # innen nur ueber dem eigenen Dach sichtbar
                lagenflaeche(h, "Bruchstein", innen_eb, poly, "bruchstein", 0.4, u_saat=0.29)
        # Stirnseiten und Abdeckung
        for s, z0, z1 in ((s_tr_h, z_tr - 0.15, z_tr + ueber), (s_tr_v, z_tr - 0.15, z_tr + ueber)):
            y = TIEFE - s
            h.rechteck("Bruchstein", (0, y, z0), (1, 0, 0), (0, 0, 1), d, z1 - z0, "bruchstein", laengs="v",
                       weg_von=(d / 2, TIEFE / 2, (z0 + z1) / 2))
        for (sa, za), (sb, zb) in (((s_tr_h, z_tr + ueber), (s_first, z_first + ueber)), ((s_first, z_first + ueber), (s_tr_v, z_tr + ueber))):
            pa, pb = eb.p(sa, za), eb.p(sb, zb)
            o = pa + Vector((-0.04, 0, 0))
            h.rechteck("Bruchstein", o, pb - pa, (1, 0, 0), (pb - pa).length, d + 0.08, "bruchstein", laengs="u",
                       weg_von=(d / 2, (pa.y + pb.y) / 2, (pa.z + pb.z) / 2 - 1))
    else:
        eg_l = [(s0, s1, 1.1, 2.1, "fenster") for s0, s1 in achsen(TIEFE, 2, 0.7)]
        og_l = [(s0, s1, z_og + 0.85, z_og + 1.9, "fenster") for s0, s1 in achsen(TIEFE + krag, 3, 0.7)]
        geschoss(h, eb, TIEFE, 0.0, z_og, eg_l, S["eg"], anker, modul, sockel=True)
        geschoss(h, eb, TIEFE + krag, z_og, z_t, og_l, S["og"], anker, modul)
        b = TIEFE + krag
        poly = [(0, z_t), (b, z_t), (s_first, z_first - 0.02)]
        loch = (s_first - 0.3, s_first + 0.3, z_t + 0.6, z_t + 1.4)
        if S["og"] == "stein":
            lagenflaeche(h, "Bruchstein", eb, poly, "bruchstein", 0.4, [loch], u_saat=0.5)
        else:
            lagenflaeche(h, "Lehmputz", eb, poly, "putz", z_first - z_t, [loch], u_saat=0.5)
            n = eb.n()
            for s in (loch[0] - BW / 2, loch[1] + BW / 2):
                oben = z_first - abs(s - s_first) * tt - 0.12
                h.brett("Fachwerk", eb.p(s, z_t), eb.p(s, oben), BW, n, "balken_a", tiefe=0.04)
            kz = z_t + (z_first - z_t) * 0.55
            ks = (z_first - kz) / tt
            h.brett("Fachwerk", eb.p(s_first - ks + 0.08, kz), eb.p(s_first + ks - 0.08, kz), RIEGEL, n, "balken_b", tiefe=0.04)
        fenster(h, eb, loch, laeden=False)
        # Ortgang: Dach 0,3 m ueber die Giebelwand hinaus, Windbretter
        trauf_dach(h, -ORTGANG, 0.0, z_t, krag, kanten_enden=True)
        zf = trauf_first_z(z_t, krag)
        z_fuss = z_t - UEBER * tt + dick / cos
        for y_tr in (-krag - UEBER, TIEFE + UEBER):
            a = Vector((-ORTGANG - 0.03, y_tr, z_fuss - dick - 0.02)); bb = Vector((-ORTGANG - 0.03, y_first, zf - 0.04))
            h.brett("Fachwerk", a, bb, 0.20, (-1, 0, 0), "balken_c", tiefe=0.03)
    return h, anker


def gasse(modul):
    """Halbe Traufgasse (Streifen x 0 bis EIN an der linken Grenze eines Giebelhauses): vorn und hinten eine zurueckgesetzte Bretter-
    wand (Tuerchen), darueber ein dunkler Putzspalt bis unter die Traufe, oben ein Brett, unten die Rinne."""
    h = Teil(modul, BA, saat=53)
    z_t = S["z_traufe"]
    z_top = z_t - EIN * S["tg"]
    for y, aussen in ((0.35, -1), (TIEFE - 0.35, 1)):
        innen = (EIN / 2, y - aussen, 2.0)
        h.rechteck("Fachwerk", (0, y, 0), (1, 0, 0), (0, 0, 1), EIN, 2.1, "planken", laengs="v", weg_von=innen)
        h.rechteck("Lehmputz", (0, y + aussen * -0.0 + (0.6 if aussen < 0 else -0.6), 2.1), (1, 0, 0), (0, 0, 1), EIN, z_top - 2.1 - 0.05, "putz", weg_von=(EIN / 2, y + (1.6 if aussen < 0 else -1.6), 3))
        y_kante = (-0.02 if aussen < 0 else TIEFE + 0.02)
        h.brett("Fachwerk", (0, y_kante, z_top - 0.25), (EIN, y_kante, z_top - 0.25), 0.18, (0, aussen, 0), "balken_c", tiefe=0.03)
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
    fassade(h, vorn, B, z_t, vorn_og, anker, modul, 3, 4, True, tuer_links=False, b_og=Q)
    fassade(h, rechts, B, z_t, rechts_og, [], modul, 3, 4, False, b_og=Q)
    krag_boden(h, 0.0, Q, 0.0, oy, z_og, -1)
    if k > 0:   # Untersicht der Auskragung rechts
        v0, v1, m = BA["planken"]
        h.vieleck("Fachwerk", [Vector((B, oy, z_og)), Vector((Q, oy, z_og)), Vector((Q, B, z_og)), Vector((B, B, z_og))],
                  [(0, v1), (0, v0), ((B - oy) / m, v0), ((B - oy) / m, v1)], weg_von=(B + k / 2, B / 2, z_og + 1))
    blind_eg = "stein" if S["eg"] == "stein" else "putz"
    blind_og = "stein" if S["og"] == "stein" else "putz"
    geschoss(h, links, B, 0.0, z_og, [], blind_eg, anker, modul, sockel=True)
    geschoss(h, links, Q, z_og, z_t, [], blind_og, anker, modul)
    geschoss(h, hinten, B, 0.0, z_og, [], blind_eg, anker, modul, sockel=True)
    geschoss(h, hinten_og, Q, z_og, z_t, [], blind_og, anker, modul)
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
        dachflaeche(h, poly, fuss, e, hg, unten_punkt, kanten=kanten, karten=nr < 2)
    # Grate: schmale Kappen von den Ecken zur Spitze
    for ex, ey in ((0, oy), (Q + u, oy - u), (Q, B), (0, B)):
        a = Vector((ex, ey, dz(ex, ey))) + EZ * 0.02
        h.brett("Stroh", a, spitze + EZ * 0.05, 0.22, (0, 0, 1), "stroh", tiefe=0.06, seiten=False)
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
    for o, eu, breite in (((x0, y0, 0), (1, 0, 0), x1 - x0), ((x1, y0, 0), (0, 1, 0), y1 - y0)):
        eb = Ebene(o, eu, (0, 0, 1), mitte)
        loch = (0.35, breite - 0.35, z0 + 0.7, z1 - 0.4)
        lagenflaeche(h, "Bruchstein" if S["og"] == "stein" else "Lehmputz", eb, [(0, z0), (breite, z0), (breite, z1), (0, z1)],
                     "bruchstein" if S["og"] == "stein" else "putz", 0.4 if S["og"] == "stein" else z1 - z0, [loch])
        fenster(h, eb, loch, laeden=False)
    h.kasten("Bruchstein", (x0, y0, z0 - 0.25), (x1, y1, z0), "bruchstein", ohne=("-x", "-y"))
    spitze = Vector(((x0 + x1) / 2, (y0 + y1) / 2, z1 + 1.1))
    ecken = [Vector((x0 - 0.08, y0 - 0.08, z1)), Vector((x1 + 0.08, y0 - 0.08, z1)), Vector((x1 + 0.08, y1 + 0.08, z1)), Vector((x0 - 0.08, y1 + 0.08, z1))]
    v0, v1, m = BA["stroh_franse"]
    for a, b in zip(ecken, ecken[1:] + ecken[:1]):
        h.vieleck("Stroh", [a, b, spitze], [(0, v1), ((b - a).length / m, v1), ((b - a).length / m / 2, v0)], weg_von=mitte)


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

# Breite je Modul (fuer Blindnachbarn beim Backen, Kontrollbilder)
BREITE = {"k_g4": 4, "k_g6": 6, "k_g8": 8, "k_t6": 6, "k_t8": 8, "eck": 8}


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
    eintrag = {"datei": f"{gruppe}_ao_atlas.png", "kante": 2048, "zellen": 4, "plaetze": {m: i for i, m in enumerate(MODULE)}}
    if daten.get(gruppe) != eintrag:
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
        elif m in ("seite_blind", "seite_offen"):
            nachbarn = [kopie("k_g6", 0.0)] if "k_g6" in objekte else []
        elif m in ("kappe_brand", "kappe_offen"):
            nachbarn = [kopie("k_t6", 0.0)] if "k_t6" in objekte else []
        elif m == "gasse":
            nachbarn = [kopie("k_g6", 0.0), kopie("k_g6", -6.0)] if "k_g6" in objekte else []
        elif m == "eck":
            nachbarn = [kopie("k_t6", -6.0)] if "k_t6" in objekte else []
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
