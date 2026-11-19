"""Neubau der Gebaeude: Wohnhaus Halbling, Stufe 2 (Buergerhaus) und Stufe 3 (Steinhaus), Grundformen 1 bis 4 - aussen und innen.

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_halbling_s23.py -- --stufe=<2|3> --form=<1|2|3|4> [--bilder=<ordner>] [--ohne-ao]

Wie bei Elben und Zwergen (wohnhaus_elb_s23.py, wohnhaus_zwerg_s23.py) eine SCHICHT auf den Bauskripten der Menschen
(wohnhaus_mensch_s23.py mit _masse und _formen, unveraendert): gleiche Grundrisse, Geschosse, Waende, Fenster, Treppe, Dach und Raeume
(echtes Obergeschoss ab Stufe 2, Fussabdruck wie das Menschenhaus derselben Grundform, Pruefung 27), gestaltet nach den Vorlagen
wohnhaus_lvl2_halbling / wohnhaus_lvl3_halbling (Nutzer 18.11.2026: „Vorlagen genau umsetzen“):
  * Stoff, Dateinamen, Atlasgruppen: trim_s2_halbling bzw. trim_s3_halbling aussen, innen_trim_s23_halbling innen (trimsheet_halbling.py);
    Gruppen wohnhaus_halbling_s<n> und wohnhaus_halbling_innen; Dateien wohnhaus_halbling_s<stufe>_g<form>[_innen].glb. Im Spiel die
    Steinebenen halbling_s2 und halbling_s3 mit rotem Backstein K6 (medieval_red_brick) im Band „bruchstein“.
  * Stufe 2: Erdgeschoss aus rotem Backstein (Band „bruchstein“ wie bei den Menschen), Obergeschoss und Giebel in Fachwerk mit Lehmputz,
    blaues Schieferdach, an der rechten Traufseite ein Quergiebel in der Flucht der Wand (statt der Gaube, wie bei den Elben).
  * Stufe 3: ganz aus rotem Backstein (Patrizierhaus) mit dem Werkstein der Menschen (Eckquader, Gewaende), vorn der Treppengiebel der
    Menschen, blaues Schieferdach, die Gaube mit Kupferdach (Gruenspan, Band „stroh“). Die Bibliothek bleibt rechteckig im Obergeschoss wie
    im Buergerhaus (raeume_halbling_s23.py). Wandbehang aus dem Bauskript der Menschen.
  * Aus beiden Vorlagen: Rundbogen aussen in der Haustuer (im Steinhaus Werkstein), rundes Fenster im vorderen Giebel (Ochsenauge aus
    halbling_teile; im Steinhaus im Treppengiebel, Ring aus Werkstein). Stufe 1 (Strohdach) bleibt unveraendert.
  * Haustuer, Zimmertueren, Decken und Herdhaube in Menschengroesse, Tisch, Sitz, Blick und Greifhoehe fuer den Halbling wie Stufe 1
    (Tisch 0,56, Sitz 0,30, Blick 1,02 m; halbling_masse.py, wohnhaus_halbling_s23_masse.py).
  * Fixes der Elben: Dach frei vom Schornstein, Vordach als Platte aus Holzschindeln (Band „planken“, nicht dunkel im Schatten), alle
    Dachlagen nach oben (Schuppen).
  * Innen: Feuerstelle ganz aus Stein wie Stufe 1 (Einfassung, Schlot, kein Holz im Schlot; Muster wohnhaus_elb_s23.feuerstelle),
    Hauswappen hoch an der Stirn des Schlots, Standardblick von hinten links auf Herd und Haustuer, Grundlicht auf der Seite der Kamera.
Messung: wohnhaus_halbling_messen.py (Stufen 2 und 3: main_s23); Soll/Ist: wohnhaus_halbling_s23_masse.py.
"""
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "raeume"))
import bpy                                                         # noqa: E402,F401
from mathutils import Vector                                       # noqa: E402
import netzbau                                                     # noqa: E402
from netzbau import Teil                                           # noqa: E402
import wohnhaus_mensch_s1_g1 as g1                                 # noqa: E402
import wohnhaus_mensch_s1 as s1                                    # noqa: E402
import wohnhaus_mensch_s23 as S23                                  # noqa: E402
import halbling_teile as ht                                        # noqa: E402
import wohnhaus_halbling_s1 as HB                                  # noqa: E402  (Konstanten der Stufe 1)
import wohnhaus_halbling_s23_masse as HM                           # noqa: E402

VOLK = "halbling"
BLATT = {}                        # Blaetter trim_s<n>_halbling, innen_trim_s23_halbling (trimsheet_halbling.py)
QUERGIEBEL = dict(y=(0.55, 2.15), fenster=(0.66, 0.66))   # Stufe 2: Quergiebel in der Flucht der rechten Wand hinter dem Schornstein (wie Elben; Vorlage)
BOGEN_STUECKE = 3                 # Rundbogen: Abschnitte je Viertelkreis (Stufe 1: 6; Dreiecksgrenze 4.000 im Steinhaus G3)
OCHSENAUGE_STUECKE = 8            # Ochsenauge: Abschnitte des Rings (Stufe 1: 12)
OCHSENAUGE_R = 0.24
OCHSENAUGE_RAND = 0.10            # so weit reicht der Rahmen ueber den Radius hinaus (Holz des Giebels haelt Abstand)
VORDACH_DICKE = 0.025             # Vordach als Platte: Untersicht so weit unter der Dachflaeche (wohnhaus_elb_s23.vordach_platte)
WAPPEN_INNEN_Y = -0.45            # Hauswappen innen an der Stirn des Schlots ueber der Herdhaube (y, Mitte; Unterkante 5 cm ueber der Haube)
# Standardblick der Wohnstube (Pruefungen 34, 35): von hinten links ueber den Herd auf die Haustuer (wie Stufe 1, Grundform 3), in Augenhoehe
# des Halblings; der Blick der Menschen (vorn links nach hinten rechts) hatte die Haustuer 58 Grad neben der Bildmitte, und das Hauswappen
# ueber dem Schrank der Guten Stube lag aus 1,02 m Augenhoehe hinter dessen Oberkante
BLICK = ((-0.25, 1.45), (1.90, -1.20, 1.30))
STUFE = [2]
FORM = [1]
LETZT = {}
# Stufe 3 belebt (Nutzer 19.11.2026: „zu langweilig“): Eckquader auch im Obergeschoss, eine Hausecke dicht mit Efeu bewachsen.
# Efeu an der Ecke vorn rechts (im Stadtblick und in der Vorschau sichtbar): je Geschoss eine Flaeche vor der Gassenwand und eine vor der
# rechten Wand, 4,5 cm vor der Mauer (vor den Eckquadern), Umriss (Abstand von der Ecke, Hoehe ueber dem Fuss) sternfoermig um den Fuss
# an der Ecke; EG vorn bis vor die Haustuer (0,62 m Abstand bleiben), OG rechts bis vor das Fenster; im EG bis 2,34 m (darueber stehen die
# Balkenkoepfe unter der Auskragung durch die Ebene des Efeus, Pruefung 12).
EFEU_ECKE = (1, -1)
EFEU_VOR = 0.045
EFEU_UMRISS = {
    "eg_vorn": [(0, 0), (0, 2.34), (0.30, 2.32), (0.45, 2.18), (0.62, 2.25), (0.78, 1.95), (0.72, 1.55), (0.80, 1.15), (0.55, 0.70), (0.42, 0.35), (0.22, 0)],
    "eg_seite": [(0, 0), (0, 2.34), (0.40, 2.31), (0.65, 2.14), (0.90, 2.27), (1.20, 1.90), (1.35, 1.55), (1.15, 1.05), (1.25, 0.70), (0.80, 0.40), (0.45, 0.12), (0.30, 0)],
    "og_vorn": [(0, 0), (0, 1.95), (0.25, 1.80), (0.40, 2.05), (0.65, 1.60), (0.85, 1.45), (1.05, 1.00), (1.25, 0.75), (1.10, 0.30), (1.30, 0)],
    "og_seite": [(0, 0), (0, 2.10), (0.20, 1.90), (0.38, 2.00), (0.55, 1.55), (0.75, 1.30), (0.95, 0.85), (0.85, 0.40), (1.00, 0)],
}
EFEU_V, KUPFER_V = (0.004, 0.121), (0.125, 0.1875)     # trimsheet_halbling.EFEU_STUFE3 / KUPFER_STUFE3 (Band „stroh“ geteilt)
OG_QUADER_AB = 0.12                                     # Eckquader des OG ueber dem Gesims (Z_EG_DECKE - 0,04 bis + 0,12) ...
OG_QUADER_BIS = 0.10                                    # ... bis unter das Traufgesims (Z_TRAUFE - 0,10)


def umbenennen(text):
    return text.replace("mensch", VOLK)


def blatt_halbling(blatt):
    return BLATT.get(blatt, blatt + "_" + VOLK)


# ── Hilfen fuer Flaechen (wie wohnhaus_elb_s23.py; dort in der Schicht der Elben, hier eine eigene Kopie) ───────────────────

def _lerp(a, b, t):
    return a + (b - a) * t


def _uv_lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def schnitt(pts, uv, f, werte):
    """Teilt ein Viereck entlang eines Kantenpaars an den Stellen, an denen die Groesse f(p) die `werte` annimmt (beide Kanten des Paars
    muessen den Wert kreuzen). Liefert die Teilvierecke (Punkte, UV) in gleicher Drehrichtung oder None."""
    if len(pts) != 4: return None
    for dreh in (0, 1):
        P = [Vector(p) for p in pts[dreh:] + pts[:dreh]]
        U = list(uv[dreh:] + uv[:dreh])
        a0, a1, b0, b1 = f(P[0]), f(P[1]), f(P[3]), f(P[2])
        ts = []
        for w in werte:
            if min(a0, a1) + 1e-4 < w < max(a0, a1) - 1e-4 and min(b0, b1) + 1e-4 < w < max(b0, b1) - 1e-4:
                ts.append(((w - a0) / (a1 - a0), (w - b0) / (b1 - b0)))
        if not ts: continue
        ts = [(0.0, 0.0)] + sorted(ts) + [(1.0, 1.0)]
        teile = []
        for (ta, tb), (tc, td) in zip(ts[:-1], ts[1:]):
            q = [_lerp(P[0], P[1], ta), _lerp(P[0], P[1], tc), _lerp(P[3], P[2], td), _lerp(P[3], P[2], tb)]
            u = [_uv_lerp(U[0], U[1], ta), _uv_lerp(U[0], U[1], tc), _uv_lerp(U[3], U[2], td), _uv_lerp(U[3], U[2], tb)]
            teile.append((q, u))
        return teile
    return None


def flaechen_ersetzen(teil, neu_je_flaeche):
    """Ersetzt jede Flaeche i durch die Liste neu_je_flaeche[i] und verschiebt die Bereiche der Holzakzente mit (versteinern liest sie)."""
    start, neu = [], []
    for liste in neu_je_flaeche:
        start.append(len(neu)); neu += liste
    start.append(len(neu))
    teil.flaechen = neu
    if hasattr(teil, "holz_akzente"):
        teil.holz_akzente = [(start[a], start[e]) for a, e in teil.holz_akzente]


def band_umlegen(teil, ab, band, stoff=None, bis=None):
    """Legt die UV der Flaechen ab Index `ab` (bis `bis`) auf das Band `band` (gleiche relative Lage im Band) und setzt den Stoff."""
    v0n, v1n, _ = teil.baender[band]
    for i in range(ab, len(teil.flaechen) if bis is None else bis):
        pts, uv, st, fa, gl = teil.flaechen[i]
        alt = S23._band_von(teil, uv)
        v0, v1, _ = teil.baender[alt] if alt else (0.0, 1.0, 1)
        uv = [(u, v0n + (v - v0) / max(v1 - v0, 1e-6) * (v1n - v0n)) for u, v in uv]
        teil.flaechen[i] = (pts, uv, stoff or st, fa, gl)


def akzent(teil, ab):
    """Flaechen ab `ab` bleiben im Steinhaus, wie sie sind (versteinern() liest holz_akzente)."""
    if not hasattr(teil, "holz_akzente"): teil.holz_akzente = []
    teil.holz_akzente.append((ab, len(teil.flaechen)))


def nach_oben(teil, ab, stoff=("Stroh",), grenze=-0.85):
    """Flaechen der Deckung ab `ab`, die nach unten zeigen, werden umgedreht (die Lagen der Menschen zeigen weg von MITTE, z 3,0: unter
    dieser Hoehe nach unten – von oben sah man nur ihre Rueckseiten, im Spiel durchsichtig; Elben: Pruefung 12b). Nur fast waagerecht
    nach unten (z der Normale unter `grenze`): die Stirnen der Lagen steiler Daecher zeigen gewollt schraeg nach unten und bleiben."""
    n = 0
    for i in range(ab, len(teil.flaechen)):
        pts, uv, st, fa, gl = teil.flaechen[i]
        if st in stoff and len(pts) >= 3 and (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized().z < grenze:
            teil.flaechen[i] = (pts[::-1], list(uv)[::-1], st, fa, gl); n += 1
    return n


# ── Dach der Menschen (Vorlagen: blaues Schiefer-/Ziegeldach), frei vom Schornstein ──────────────────────────────────────────────────────────────

def schlot_frei(d):
    """Dachflaechen im Grundriss des Schornsteins entfallen (Elben, Klicktest 18.11.2026: das Dach lief durch den Schornstein, das Stueck
    darin backte schwarz und fleckte auf das Dach daneben). Die Flaechen werden an den Fluchten des Schornsteins geteilt, die Stuecke
    darin entfallen. Der Schornstein der Menschen steht aussen an der rechten Wand im Ueberstand des Strohs."""
    x0, x1, y0, y1 = S23.M.SCHORNSTEIN_RECHTS
    z_oben = S23.M.Z_FIRST - 0.6
    drin = lambda p: x0 - 1e-3 <= p.x <= x1 + 1e-3 and y0 - 1e-3 <= p.y <= y1 + 1e-3 and p.z <= z_oben + 1e-3
    neu_je, weg, geteilt = [], 0, 0
    for pts, uv, st, fa, gl in d.flaechen:
        stuecke = [(pts, uv)]
        if (min(p.x for p in pts) < x1 and max(p.x for p in pts) > x0 and min(p.y for p in pts) < y1 and max(p.y for p in pts) > y0
                and min(p.z for p in pts) < z_oben):
            for f, werte in ((lambda p: p.y, (y0, y1)), (lambda p: p.x, (x0, x1))):
                naechste = []
                for q, u in stuecke:
                    r = schnitt(q, u, f, werte)
                    naechste += r if r else [(q, u)]
                stuecke = naechste
            geteilt += 1
        liste = []
        for q, u in stuecke:
            if all(drin(Vector(p)) for p in q):
                weg += 1; continue
            liste.append(([Vector(p) for p in q], list(u), st, fa, gl))
        neu_je.append(liste)
    flaechen_ersetzen(d, neu_je)
    print(f"DACH Schornstein: {geteilt} Flaechen geteilt, {weg} Stuecke im Schornstein weg")


def baue_dach_halbling(stoff="Dachziegel", first="Firstziegel"):
    """Dach der Menschen nach den Vorlagen (Nutzer 18.11.2026: blaues Dach, kein Stroh): Lagen im Band „stroh_franse“ (im Blatt der
    Halblinge blauer Schiefer), Bürgerhaus mit dem Quergiebel in der Flucht der rechten Wand, Steinhaus mit der Gaube (Dach im Band
    „stroh“: Kupfer mit Gruenspan). Frei vom Schornstein (Elben, Klicktest 18.11.2026)."""
    d = _ALT["baue_dach"](stoff, first)
    schlot_frei(d)
    if S23.ist_stein():
        print(f"BELEBT Kupfer im Dach umgelegt: {kupfer_umlegen(d)} Flaechen")
    LETZT["dach"] = d
    return d


def ziegelflaeche_oben(d, stoff, unten, oben, ys, saat, **kw):
    """Ziegellagen der Menschen (Fluegel, Schuppen, Erker), alle Lagen nach oben (Schuppen: Pruefung 12b, wie bei den Elben)."""
    ab = len(d.flaechen)
    _ALT["ziegelflaeche"](d, stoff, unten, oben, ys, saat, **kw)
    n = nach_oben(d, ab, stoff=(stoff,))
    if n: print(f"DACHLAGEN nach oben gedreht: {n}")


_ALT = {}


# ── Haus: Rundbogen, Ochsenauge ────────────────────────────────────────────────────

def ochsenauge_hoehe(wand):
    """Mitte des Ochsenauges im Giebel: zwischen Kehlbalken und Hahnenbalken des Giebelfachwerks der Menschen (giebel(): Kehlbalken
    0,08 ueber dem Sturz der Giebelfenster, Hahnenbalken bei 55 % bis zum First)."""
    M = S23.M
    fen = [f for f in M.FENSTER if f[0] == wand]
    zk = max([f[2][1] for f in fen] or [M.Z_TRAUFE + 1.50]) + 0.08
    zh = zk + 0.55 * (M.Z_FIRST - zk)
    return ((zk + 0.08) + (zh - 0.075)) / 2


class OhneMitte:
    """Stellvertreter fuer das Haus-Teil beim Giebelfachwerk des Giebels mit dem Ochsenauge: der Mittelstaender und die Spitzsaeule
    (senkrechtes Holz in der Mitte) setzen zwischen z0 und z1 aus, dort sitzt das runde Fenster (Kunstwerk); Kehl- und Hahnenbalken,
    Staender und Streben bleiben (Pruefung 2: spiegelgleich, bis unter den First)."""

    def __init__(self, teil, z0, z1):
        self._teil, self._z0, self._z1 = teil, z0, z1

    def brett(self, stoff, a, b, *args, **kw):
        a, b = Vector(a), Vector(b)
        if abs(a.x) < 0.02 and abs(b.x) < 0.02 and abs(a.y - b.y) < 1e-6:
            lo, hi = (a, b) if a.z <= b.z else (b, a)
            if lo.z < self._z1 and hi.z > self._z0:
                if self._z0 - lo.z > 0.05: self._teil.brett(stoff, lo, Vector((lo.x, lo.y, self._z0)), *args, **kw)
                if hi.z - self._z1 > 0.05: self._teil.brett(stoff, Vector((hi.x, hi.y, self._z1)), hi, *args, **kw)
                return None
        return self._teil.brett(stoff, a, b, *args, **kw)

    def __getattr__(self, name):
        return getattr(self._teil, name)


def _im_umriss(umriss, d, z):
    """Liegt der Punkt (Abstand d von der Ecke, Hoehe z ueber dem Fuss) in der Efeuflaeche (Strahltest)?"""
    drin = False
    for (d0, z0), (d1, z1) in zip(umriss, umriss[1:] + umriss[:1]):
        if (z0 > z) != (z1 > z) and d < d0 + (z - z0) * (d1 - d0) / (z1 - z0): drin = not drin
    return drin


def _bedeckt(umriss, breite, z_oben, z_fuss, z_unten):
    """Liegt eine Seite eines Eckquaders (Breite ab der Ecke, z_unten bis z_oben) ganz hinter der Efeuflaeche (Raster 5 x 5, 2 cm innen)?"""
    return all(_im_umriss(umriss, 0.02 + (breite - 0.02) * i / 4, z_unten - z_fuss + 0.02 + (z_oben - z_unten) * k / 4) for i in range(5) for k in range(5))


def eckquader(h, X, Y0, Y1, z0, z1, geschoss):
    """Eckquader aus Werkstein wie im Erdgeschoss der Menschen (wohnhaus_mensch_s23.baue_haus: Laeufer 0,48 / Binder 0,22 m, Hoehen 0,34 /
    0,26 m, 3 cm vor der Mauer), aber als ein Winkel aus vier Flaechen: vorn, Seite (Stirn des Laeufers und Seite des Binders liegen in
    einer Ebene), die zwei Stirnen zur Wand. Die Quader stossen ohne offene Fuge aneinander (die Fuge zeichnet der Wechsel der Textur;
    durch eine offene Fuge sah man in den Schacht hinter der Ecke, Pruefung 16); einen Deckel bekommt nur der oberste Quader eines Stapels.
    8 statt 18 Dreiecke je Quader: so passen die Quader des Obergeschosses unter die Grenze von 4.000 Dreiecken. Hinter dem Efeu liegende
    Quader entfallen."""
    band, stein = S23.WERKSTEIN, "Bruchstein"
    n = 0
    for sx in (-1, 1):
        for y, sy in ((Y0, -1), (Y1, 1)):
            efeu = (sx, sy) == EFEU_ECKE
            stapel, z, i = [], z0, 0
            oben = z1 if (geschoss == "eg" or sy < 0) else z1 + OG_QUADER_BIS   # OG hinten: bis unter das Ortgangbrett (kein Traufgesims)
            while z < oben - (0.3 if geschoss == "eg" else 0.05):             # OG: der oberste Quader reicht bis oben (sonst Schlitz unter dem Dach, 16)
                hq = 0.34 if i % 2 == 0 else 0.26
                lx, ly = (0.48, 0.22) if i % 2 == 0 else (0.22, 0.48)
                zb = min(z + hq, oben - 0.02)
                weg = efeu and _bedeckt(EFEU_UMRISS[f"{geschoss}_vorn"], lx + 0.03, zb + 0.02, z0, z) and _bedeckt(EFEU_UMRISS[f"{geschoss}_seite"], ly + 0.03, zb + 0.02, z0, z)
                stapel.append((z, zb, lx, ly, not weg))
                z, i = zb, i + 1
            xa, ya = sx * (X + 0.03), y + sy * 0.03
            for k, (z, zb, lx, ly, da) in enumerate(stapel):
                if not da: continue
                innen = Vector((sx * (X - 0.3), y - sy * 0.3, (z + zb) / 2))
                h.rechteck(stein, (min(xa, sx * (X - lx)), ya, z), (1, 0, 0), (0, 0, 1), lx + 0.03, zb - z, band, weg_von=innen)
                h.rechteck(stein, (xa, min(ya, y - sy * ly), z), (0, 1, 0), (0, 0, 1), ly + 0.03, zb - z, band, weg_von=innen)
                h.rechteck(stein, (sx * (X - lx), min(y - sy * 0.01, ya), z), (0, 1, 0), (0, 0, 1), 0.04, zb - z, band, weg_von=(sx * (X + 1), y, z))
                h.rechteck(stein, (min(sx * (X - 0.01), xa), y - sy * ly, z), (1, 0, 0), (0, 0, 1), 0.04, zb - z, band, weg_von=(sx * X, y + sy, z))
                if k == len(stapel) - 1 or not stapel[k + 1][4]:      # Deckel (Laeufer und Binder) auf dem obersten Quader
                    h.rechteck(stein, (min(xa, sx * (X - lx)), min(ya, y - sy * 0.01), zb), (1, 0, 0), (0, 1, 0), lx + 0.03, 0.04, band, zu=(0, 0, zb + 1))
                    h.rechteck(stein, (min(xa, sx * (X - 0.01)), min(y - sy * 0.01, y - sy * ly), zb), (1, 0, 0), (0, 1, 0), 0.04, ly - 0.01, band, zu=(0, 0, zb + 1))
                n += 1
    return n


def efeu_ecke(h):
    """Efeu an der Ecke EFEU_ECKE (Erd- und Obergeschoss, vorn und Seite), Stoff „Efeu“ im oberen Teil des Bands „stroh“ (gedreht: UV.x die
    Wand hinauf, UV.y von der Ecke weg). Jede Flaeche ein Faecher von Dreiecken um den Fuss an der Ecke."""
    M = S23.M
    sx, sy = EFEU_ECKE
    ecken = {"eg": (M.BX, M.VORN if sy < 0 else M.HINTEN, 0.0), "og": (M.OX, M.OVORN if sy < 0 else M.HINTEN, M.Z_EG_DECKE + OG_QUADER_AB)}
    v0, v1 = EFEU_V
    dreiecke, u0 = 0, 0.0
    for geschoss, (X, Y, zf) in ecken.items():
        ex, ey = sx * (X + EFEU_VOR), Y + sy * EFEU_VOR
        for seite, richtung, innen in (("vorn", Vector((-sx, 0, 0)), Vector((ex - sx, ey - sy, zf + 1))), ("seite", Vector((0, -sy, 0)), Vector((ex - sx, ey - sy, zf + 1)))):
            umriss = EFEU_UMRISS[f"{geschoss}_{seite}"]
            breite = max(d for d, _ in umriss)
            p = [Vector((ex, ey, zf)) + richtung * d + Vector((0, 0, hz)) for d, hz in umriss]
            uv = [(u0 + hz / 4.0, v0 + (v1 - v0) * d / breite) for d, hz in umriss]
            for k in range(1, len(p) - 1):
                h.vieleck("Efeu", [p[0], p[k], p[k + 1]], [uv[0], uv[k], uv[k + 1]], weg_von=innen)
                dreiecke += 1
            u0 += 0.27
    return dreiecke


def kupfer_umlegen(teil):
    """Stufe 3: Flaechen im Band „stroh“ (Kupfer der Gauben und des Firsts) in dessen unteren Teil KUPFER_V; oben liegt der Efeu."""
    k0, k1 = KUPFER_V
    n = 0
    for i, (pts, uv, st, fa, gl) in enumerate(teil.flaechen):
        if st == "Efeu" or not all(v <= 0.1875 + 1e-6 for _u, v in uv): continue
        teil.flaechen[i] = (pts, [(u, k0 + (k1 - k0) * v / 0.1875) for u, v in uv], st, fa, gl)
        n += 1
    return n


def haus_halbling():
    M = S23.M
    stein = S23.ist_stein()
    alt_kasten = netzbau.Teil.kasten
    weg = []

    def kasten(self, stoff, mn, mx, band, *a, **kw):           # Steinhaus: die Eckquader der Menschen entfallen (eckquader() ersetzt sie)
        if stein and band == S23.WERKSTEIN and max(abs(mn[0]), abs(mx[0])) > M.BX + 0.029 and max(abs(mn[0]), abs(mx[0])) < M.BX + 0.031 and mx[2] <= M.Z_EG_DECKE:
            weg.append(1)
            return None
        return alt_kasten(self, stoff, mn, mx, band, *a, **kw)
    netzbau.Teil.kasten = kasten
    try:
        h = _ALT["baue_haus"]()
    finally:
        netzbau.Teil.kasten = alt_kasten
    if stein:
        vorher = h.dreiecke()
        n_eg = eckquader(h, M.BX, M.VORN, M.HINTEN, 0.0, M.Z_EG_DECKE, "eg")
        n_og = eckquader(h, M.OX, M.OVORN, M.HINTEN, M.Z_EG_DECKE + OG_QUADER_AB, M.Z_TRAUFE - OG_QUADER_BIS, "og")
        n_k = kupfer_umlegen(h)
        n_e = efeu_ecke(h)
        print(f"BELEBT Eckquader der Menschen weg: {len(weg)} Kaesten; neu EG {n_eg}, OG {n_og} Quader; Kupfer umgelegt {n_k}; Efeu {n_e} Dreiecke; "
              f"Dreiecke {vorher} -> {h.dreiecke()}")
    vorher = h.dreiecke()
    for wand, a, z, art in M.FENSTER:
        if art != "tuer": continue
        (ox, oy), (ex, ey), _, n = S23.WAENDE[wand]
        start = ox if ex else oy
        ab = len(h.flaechen)
        ht.rundbogen(h, lambda s, zz, w=wand: S23.punkt(w, s, zz), (n[0], n[1], 0), a[0] - start, a[1] - start, z[1], stuecke=BOGEN_STUECKE)
        if stein:                                             # Steinhaus: Zwickel und Bogen aus Werkstein (wie der Spitzbogen der Elben)
            band_umlegen(h, ab, S23.WERKSTEIN, "Bruchstein")
            akzent(h, ab)
    print("DREIECKE Rundbogen:", h.dreiecke() - vorher)
    vorher = h.dreiecke()
    wand = "vorn_tg" if stein else "vorn_gb"
    (ox, oy), (ex, ey), breite, n = S23.WAENDE[wand]
    ab = len(h.flaechen)
    ht.ochsenauge(h, lambda s, zz, w=wand: S23.punkt(w, s, zz), (n[0], n[1], 0), breite / 2, ochsenauge_hoehe(wand), r=OCHSENAUGE_R, stuecke=OCHSENAUGE_STUECKE)
    if stein:                                                # Steinhaus (Vorlage: Rosette im Treppengiebel): Ring aus Werkstein, Fuellung und Kreuz Holz
        ring = [i for i in range(ab, len(h.flaechen)) if S23._band_von(h, h.flaechen[i][1]) == "balken_b"]
        for i in ring: band_umlegen(h, i, S23.WERKSTEIN, "Bruchstein", bis=i + 1)
        akzent(h, ab)
    print("DREIECKE Ochsenauge:", h.dreiecke() - vorher)
    LETZT["haus"] = h
    return h


def giebel_halbling(h, wand, y, aussen):
    """Fachwerkgiebel der Menschen; im Giebel mit dem Ochsenauge setzt das senkrechte Holz der Mitte um das Fenster aus."""
    if wand == "vorn_gb":
        zc = ochsenauge_hoehe(wand)
        r = OCHSENAUGE_R + OCHSENAUGE_RAND
        h = OhneMitte(h, zc - r, zc + r)
    return _ALT["giebel"](h, wand, y, aussen)


# ── Vordach als Platte aus Holzschindeln (wie wohnhaus_elb_s23.vordach_platte) ───────────────────────────────────────────

def vordach_platte(h, ab):
    """Vordach der Menschen (Dachflaeche und Untersicht auf denselben Punkten, ohne Dicke) als Platte: Beim Verschmelzen der Punkte vor
    dem Backen loeschte Blender je deckungsgleiches Paar eine Flaeche (Elben, Klicktest 18.11.2026). Die Untersicht liegt VORDACH_DICKE
    unter der Dachflaeche, Traufe und Stirn sind geschlossen (Pruefung 12b). Die Deckung sind Holzschindeln im Band „planken“ (der dunkle
    Schiefer bzw. das Stroh wirkte unter der Auskragung fast schwarz; Entscheidung des Nutzers 18.11.2026 bei den Elben)."""
    neu = []
    for i in range(ab, len(h.flaechen)):
        pts, uv, st, fa, gl = h.flaechen[i]
        if st != "Dachziegel" or len(pts) != 4: continue
        n = (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized()
        if n.z < 0: n = -n
        schluessel = sorted((round(p.x, 4), round(p.y, 4), round(p.z, 4)) for p in pts)
        for k in range(ab, len(h.flaechen)):
            q, u, st2, fa2, gl2 = h.flaechen[k]
            if st2 == "Fachwerk" and len(q) == 4 and sorted((round(p.x, 4), round(p.y, 4), round(p.z, 4)) for p in q) == schluessel:
                h.flaechen[k] = ([p - n * VORDACH_DICKE for p in q], u, st2, fa2, gl2)
                break
        else:
            continue
        vs0, vs1, ms = h.baender["stroh"]
        vp0, vp1, mp = h.baender["planken"]
        vs_min = min(v for _, v in uv)
        uv = [(u * ms / mp, vp0 + (v - vs_min) / max(vs1 - vs_min, 1e-6) * (vp1 - 0.003 - vp0)) for u, v in uv]
        h.flaechen[i] = (pts, uv, "Fachwerk", fa, gl)
        mitte = sum(pts, Vector()) / 4 - n * (VORDACH_DICKE / 2)
        zmin, ymin = min(p.z for p in pts), min(p.y for p in pts)
        for a, b in zip(pts, pts[1:] + pts[:1]):
            traufe = abs(a.z - zmin) < 1e-3 and abs(b.z - zmin) < 1e-3
            stirn = abs(a.y - ymin) < 1e-3 and abs(b.y - ymin) < 1e-3
            if traufe or stirn:
                neu.append(([a, b, b - n * VORDACH_DICKE, a - n * VORDACH_DICKE], mitte))
    for q, mitte in neu:
        h.vieleck("Fachwerk", q, [(0.1, 0.772), (0.3, 0.772), (0.3, 0.766), (0.1, 0.766)], weg_von=mitte)   # im Band „planken“ (Pruefung 17)
    print(f"VORDACH Platte: {len(neu)} Stirnflaechen, Untersicht {VORDACH_DICKE * 100:.1f} cm unter der Dachflaeche")


def vordach_halbling(h, x, z):
    ab = len(h.flaechen)
    _ALT["vordach"](h, x, z)
    # die Untersicht der Menschen reicht mit v 0,78 eine Spur ueber das Band „planken“ (bis 0,775): Pruefung 17
    v0, v1, _ = h.baender["planken"]
    for i in range(ab, len(h.flaechen)):
        pts, uv, st, fa, gl = h.flaechen[i]
        if st == "Fachwerk" and all(v0 - 1e-4 <= v <= 0.78 + 1e-4 for _, v in uv) and max(v for _, v in uv) > v1:
            h.flaechen[i] = (pts, [(u, min(v, v1 - 0.002)) for u, v in uv], st, fa, gl)
    vordach_platte(h, ab)


# ── Innen: Feuerstelle, Decke, Hauswappen, Licht (wie wohnhaus_elb_s23.py) ────────────────────────────────────────────────

def licht_vor_den_herd(licht, auge):
    """Grundlicht der Wohnstube (Licht_Raum, ohne Schatten) auf der Seite der Kamera (Elben, Klicktest 18.11.2026; Pruefung 12c): in der
    Raummitte (y -0,3 wie bei den Menschen) stand es hinter den Flaechen, die zur Kamera zeigen. Wie in den Raeumen der Elben: in x
    in der Raummitte (zwischen den Deckenbalken), in y auf der Hoehe des Blickpunkts (die Kamera der Halblinge steht hinten, BLICK); an
    der linken Wand wie in der Wohnstube der Elben blieb die Seite eines Deckenbalkens zur Kamera ohne Licht."""
    return Vector((licht.x, auge.y, licht.z))


def schlot_grundriss():
    kx0, kx1, ky0, ky1 = g1.KAMIN
    hy0, hy1 = ky0 - 0.3 + S23.DY_RECHTS, ky1 + 0.3 + S23.DY_RECHTS
    return kx0 - 0.16 + S23.DX_RECHTS, S23.IX, hy0 + 0.06, hy1 - 0.06


def baue_decke():
    """Decke der Wohnstube wie bei den Menschen, aber kein Holz im Schlot: Balken und Unterzug enden an ihm."""
    M, IX, IV, IH, S = S23.M, S23.IX, S23.IV, S23.IH, S23.S
    sx0, sx1, sy0, sy1 = schlot_grundriss()
    r = Teil("Decke", S23.BI, saat=33)
    r.rechteck(S, (-IX, IV, M.Z_EG_DECKE), (0, 1, 0), (1, 0, 0), IH - IV, 2 * IX, "bohlen", zu=(0, 0, 0), anteil=(0.0, 1.0))
    for i in range(6):
        x = -IX + 0.40 + i * (2 * IX - 0.80) / 5
        stuecke = [(IV, sy0), (sy1, IH)] if x + 0.08 > sx0 else [(IV, IH)]
        for ya, yb in stuecke:
            r.kasten(S, (x - 0.08, ya, M.Z_EG_DECKE - 0.16), (x + 0.08, yb, M.Z_EG_DECKE), "balken", ohne=("+z", "-y", "+y"), laengs="y")
    xe = sx0 if sy0 < 0.10 and sy1 > -0.10 else IX
    r.kasten(S, (-IX, -0.10, M.Z_EG_DECKE - 0.24), (xe, 0.10, M.Z_EG_DECKE - 0.16), "balken", ohne=("-x", "+x"), laengs="x")   # Unterzug
    return r


def feuerstelle(raum):
    """Wie die Stufe 1 (Rueckmeldung des Nutzers): Schlot aus Stein, Holz des Herds im Schlot entfaellt, alle Holzflaechen des Herds und
    der Taefelung im Grundriss des Herdsteins (bis unter die Haube, mit Sturz und Haube) werden Stein (ganze Einfassung); Leisten der
    Taefelung in der Feueroeffnung entfallen (Elben, Klicktest 18.11.2026: helle Leiste quer durch die Feueroeffnung)."""
    BI = S23.BI
    holz = [BI[b] for b in ("bohlen", "balken")]
    stein = BI["bruchstein"]
    sx0, sx1, sy0, sy1 = schlot_grundriss()
    im_schlot = lambda p, r=0.01: sx0 - r < p.x < sx1 + r and sy0 - r < p.y < sy1 + r
    ist_holz = lambda uv: any(all(v0 - 1e-4 <= v <= v1 + 1e-4 for _, v in uv) for v0, v1, _ in holz)
    herd = [t for t in raum if t.name.startswith("Herd")]
    herdstein = [p for t in herd for pts, uv, *_ in t.flaechen if all(stein[0] - 1e-4 <= v <= stein[1] + 1e-4 for _, v in uv) for p in pts]
    hx0, hx1 = min(p.x for p in herdstein) - 0.03, max(p.x for p in herdstein) + 0.03
    hy0, hy1 = min(p.y for p in herdstein) - 0.03, max(p.y for p in herdstein) + 0.03
    z_haube = g1.Z_HAUBE
    in_herd = lambda p: hx0 <= p.x <= hx1 and hy0 <= p.y <= hy1 and p.z <= z_haube + 0.35
    in_haube = lambda p: hx0 - 0.15 <= p.x <= hx1 + 0.15 and hy0 - 0.15 <= p.y <= hy1 + 0.15 and z_haube - 0.40 <= p.z

    def zu_stein(pts, uv):
        v0, v1 = next((a, b) for a, b, _ in holz if all(a - 1e-4 <= v <= b + 1e-4 for _, v in uv))
        return [(u, stein[0] + (v - v0) / (v1 - v0) * (stein[1] - stein[0])) for u, v in uv]
    weg = neu_stein = leiste = 0
    for t in raum:
        if t.name.startswith("Herd"):
            bleib = []
            for pts, uv, st, fa, gl in t.flaechen:
                if ist_holz(uv) and max(p.z for p in pts) > z_haube and any(im_schlot(p) for p in pts):
                    weg += 1; continue
                if ist_holz(uv) and (all(in_herd(p) for p in pts) or all(im_schlot(p, 0.06) for p in pts) or all(in_haube(p) for p in pts)):
                    uv = zu_stein(pts, uv); neu_stein += 1
                bleib.append((pts, uv, st, fa, gl))
            t.flaechen = bleib
        elif t.name == "Raum":
            neu_je = []
            for pts, uv, st, fa, gl in t.flaechen:
                stuecke = [(pts, uv)]
                if ist_holz(uv) and all(abs(p.x - S23.IX) < 0.05 for p in pts):
                    r = schnitt(pts, uv, lambda p: p.y, [hy0, hy1])
                    if r: stuecke = r
                liste = []
                for q, u in stuecke:
                    if (ist_holz(u) and hy0 < sum(p.y for p in q) / len(q) < hy1 and all(abs(p.x - S23.IX) < 0.05 and p.z <= z_haube for p in q)
                            and any(S23.IX - p.x > 0.004 for p in q)):
                        leiste += 1; continue
                    if ist_holz(u) and all(in_herd(p) or (abs(p.x - S23.IX) < 0.05 and hy0 - 1e-3 <= p.y <= hy1 + 1e-3 and p.z <= z_haube) for p in q):
                        u = zu_stein(q, u); neu_stein += 1
                    liste.append((q, list(u), st, fa, gl))
                neu_je.append(liste)
            flaechen_ersetzen(t, neu_je)
        elif t.name == "Fensterseiten":
            for i, (pts, uv, st, fa, gl) in enumerate(t.flaechen):
                if min(p.z for p in pts) >= z_haube + 0.2 - 1e-3 and all(im_schlot(p, 0.17) for p in pts):
                    v0, v1, _ = BI["lehmwand"]
                    if all(v0 - 1e-4 <= v <= v1 + 1e-4 for _, v in uv):
                        t.flaechen[i] = (pts, [(u, stein[0] + (v - v0) / (v1 - v0) * (stein[1] - stein[0])) for u, v in uv], st, fa, gl); neu_stein += 1
    print(f"INNEN Feuerstelle: {weg} Holzflaechen im Schlot weg, {neu_stein} Flaechen zu Stein (Einfassung, Taefelung, Schlot), "
          f"{leiste} Leistenflaechen in der Feueroeffnung weg")


def baue_hauswappen_klein():
    """Hauswappen innen nach der Regel der Stufe 1: hoch (Unterkante ueber dem Kopf eines Menschen), nicht an der Haustuerwand – an der
    Stirn des steinernen Schlots ueber der Herdhaube, neben dem Unterzug (im Blick der Raumkamera; ueber dem Schrank der Guten Stube wie
    bei den Elben verdeckte ihn dessen Oberkante aus der Augenhoehe des Halblings, Pruefung 34)."""
    M, BI = S23.M, S23.BI
    c = Teil("Einrichtung_Hauswappen", BI, saat=87)
    g = 0.34
    o = (schlot_grundriss()[0], WAPPEN_INNEN_Y, M.Z_EG_DECKE - 0.18)
    eu, ev, n = (0, -1, 0), (0, 0, 1), (-1, 0, 0)
    g1.prisma(c, "Innen", [(x * 1.2 * g, y * 1.14 * g) for x, y in g1.SCHILD], o, eu, ev, n, 0.0, 0.05, "balken")
    g1.prisma(c, "Hausfarbe", [(x * g, y * g) for x, y in g1.SCHILD], o, eu, ev, n, 0.05, 0.08, "balken", farbe=(1.0, 1.0, 1.0, 1.0))
    return c


# ── Schicht ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

_MASSE = []


def masse_anlegen(stufe):
    """Halblingsmasse in den Modulen der Menschen (auch fuer die Messung; einmal): Tisch und Sitz fuer den Halbling; Haustuer, Zimmertueren,
    Decken und Herdhaube bleiben die der Menschen (Nutzer 18.11.2026)."""
    if _MASSE: return
    _MASSE.append(stufe)
    STUFE[0] = stufe
    g1.TISCH_HOEHE, g1.SITZ_HOEHE = HB.HALBLING_TISCH, HB.HALBLING_SITZ
    if stufe == 2:                                          # Vorlage: Quergiebel an der rechten Traufseite statt der Gaube (wie Elben)
        S23.GAUBE.update(y=QUERGIEBEL["y"], x_front=S23.M.OX, fenster=QUERGIEBEL["fenster"])


def schicht_anlegen(stufe):
    """Stoff, Dateinamen, Atlasgruppen, Halblingsmasse und die Bauteile der Halblinge; die Skripte der Menschen bleiben unveraendert."""
    STUFE[0] = stufe
    alt_stoff, alt_uv, alt_exp = g1.stoff, g1.uv2_und_ao, g1.exportiere
    g1.stoff = lambda name, blatt=None, **kw: alt_stoff(name, blatt_halbling(blatt) if blatt else None, **kw)
    g1.uv2_und_ao = lambda gruppen, gruppe, modell, mit_ao=True: alt_uv(gruppen, umbenennen(gruppe), umbenennen(modell), mit_ao)
    g1.exportiere = lambda objekte, datei: alt_exp(objekte, umbenennen(datei))
    masse_anlegen(stufe)
    # Stoffe, die die Menschen nicht haben: beim Bau des Objekts aus dem Blatt der Stufe
    alt_objekt = netzbau.Teil.objekt

    def objekt(self, stoffe, *a, **kw):
        fehlt = sorted({fl[2] for fl in self.flaechen} - set(stoffe))
        if fehlt:
            stoffe = dict(stoffe)
            for n in fehlt: stoffe[n] = g1.stoff(n, f"trim_s{STUFE[0]}", schere=n == "Strohkante")
        return alt_objekt(self, stoffe, *a, **kw)
    netzbau.Teil.objekt = objekt
    # Blick und Licht
    alt_anker, alt_init = g1.anker, s1.Anker.__init__

    def ort(name, o):
        o = Vector(o)
        if name == "Blick_Auge": o.x, o.y = BLICK[0]
        if name == "Blick_Ziel": o.x, o.y, o.z = BLICK[1][0], BLICK[1][1], S23.M.Z_BODEN + BLICK[1][2]
        if name in ("Blick_Auge", "Blick_Ziel"): o.z -= HB.BLICK_TIEFER
        if name == "Blick_Auge": LETZT["auge"] = o.copy()
        if name == "Licht_Raum" and "auge" in LETZT: o = licht_vor_den_herd(o, LETZT["auge"])
        return o
    g1.anker = lambda name, o, blick=None, **kw: alt_anker(name, ort(name, o), blick, **kw)
    s1.Anker.__init__ = lambda self, name, o, blick=None, **ex: alt_init(self, name, ort(name, o), blick, **ex)
    # Greifhoehe wie Stufe 1 (die Apotheke 1,5 cm zur Seite wie bei den Zwergen: tiefer gehaengt lag ihre Seitenwand in der Ebene der
    # Riegelstuetze an der Haustuer)
    a_apo, a_plan = g1.baue_hausapotheke, g1.baue_bauplan
    g1.baue_hausapotheke = lambda: s1.versetze(a_apo(), t=(0.015, 0.0), dz=-HB.APOTHEKE_TIEFER)
    g1.baue_bauplan = lambda: s1.versetze(a_plan(), dz=-HB.BAUPLAN_TIEFER)
    # Huelle
    for name in ("baue_haus", "giebel", "vordach", "ziegelflaeche", "baue_scheiben", "baue_wappen"):
        _ALT[name] = getattr(S23, name)
    S23.baue_haus = haus_halbling
    S23.giebel = giebel_halbling
    S23.vordach = vordach_halbling
    _ALT["baue_dach"] = S23.baue_dach
    S23.ziegelflaeche = ziegelflaeche_oben                  # Fluegel, Schuppen und Erker (main reicht sie an wohnhaus_mensch_s23_formen)
    S23.baue_dach = baue_dach_halbling

    def scheiben():
        s = _ALT["baue_scheiben"]()
        LETZT["scheiben"] = s
        return s
    S23.baue_scheiben = scheiben

    def wappen():
        w = _ALT["baue_wappen"]()
        g1.wappen_frei_ruecken(w, [LETZT["haus"], LETZT["dach"], LETZT["scheiben"]])   # Wappenregel (Rundbogen, Vordach)
        return w
    S23.baue_wappen = wappen
    # Innen: main() baut erst die Moebel (Herd), dann Raum, Fensterseiten (Schlot) und Decke: die Feuerstelle entsteht mit der Decke
    S23.baue_hauswappen_klein = baue_hauswappen_klein
    alt_moebel, alt_fs, alt_raum = S23.moebel, S23.baue_fensterseiten, S23.baue_raum

    def moebel():
        m_raum, gegen, einr = alt_moebel()
        LETZT["m_raum"] = m_raum
        return m_raum, gegen, einr
    S23.moebel = moebel
    S23.baue_fensterseiten = lambda: LETZT.__setitem__("fs", alt_fs()) or LETZT["fs"]
    S23.baue_raum = lambda: LETZT.__setitem__("raum", alt_raum()) or LETZT["raum"]

    def decke():
        feuerstelle(LETZT["m_raum"] + [LETZT["raum"], LETZT["fs"]])
        return baue_decke()
    S23.baue_decke = decke


def messen(stufe, form, ausgabe_ordner=None):
    """Ist-Masse aus der gebauten Szene und den Massen der Mensch-Skripte; Soll gegen Ist: wohnhaus_halbling_s23_masse.pruefe."""
    M = S23.M
    objs = bpy.data.objects
    z0 = M.Z_BODEN
    anker = {o.name: o.location.copy() for o in objs if o.type == "EMPTY"}
    tuer = next(f for f in M.FENSTER if f[3] == "tuer")
    loch = M.TREPPE_LOCH_X[1]
    stufe_hoch = (M.TREPPE_FUSS_X - loch) / M.AUFTRITT * M.STEIGUNG
    ist = dict(tuer_h=tuer[2][1] - tuer[2][0], tuer_b=tuer[1][1] - tuer[1][0], raum_h=M.Z_EG_DECKE - z0, haube_h=g1.Z_HAUBE - z0, sitz=g1.SITZ_HOEHE,
               tisch=g1.TISCH_HOEHE, auge=anker["Blick_Auge"].z - z0, stufe=M.STUFE_VOR_TUER, zimmertuer=M.ZIMMERTUER, treppe_steigung=M.STEIGUNG,
               treppe_kopfhoehe=M.Z_EG_DECKE - (z0 + stufe_hoch), og_raum_h=M.Z_OG_DECKE - M.Z_OG)
    zeilen = []
    _z, fehler = HM.pruefe(stufe, form, ist, ausgabe=zeilen.append)
    for z in zeilen: print(z)
    print(f"SOLLIST Fehler: {fehler}")
    if ausgabe_ordner:
        os.makedirs(ausgabe_ordner, exist_ok=True)
        open(os.path.join(ausgabe_ordner, f"soll_ist_s{stufe}_g{form}.txt"), "w", encoding="utf-8").write("\n".join(z.replace("SOLLIST ", "", 1) for z in zeilen) + "\n")


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    stufe = int(opt.get("stufe", "2"))
    FORM[0] = int(opt.get("form", "1"))
    schicht_anlegen(stufe)
    S23.main()
    messen(stufe, FORM[0], opt.get("bilder"))


if __name__ == "__main__":
    main()
