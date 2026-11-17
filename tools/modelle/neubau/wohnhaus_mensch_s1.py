"""Neubau der Gebaeude: Wohnhaus Mensch, Stufe 1 (Gemeiner), Grundformen 2, 3 und 4 - aussen und innen.

    ~/werkzeuge/blender/blender.sh -b --python tools/modelle/neubau/wohnhaus_mensch_s1.py -- --form=<2|3|4> [--bilder=<ordner>] [--ohne-ao]

Schreibt je Grundform
    game/assets/modelle/gebaeude/neubau/wohnhaus_mensch_s1_g<n>.glb         Huelle: Haus, Dach, Scheiben, Wappen, Fahne, Leerstand, Geruest, Anker
    game/assets/modelle/gebaeude/neubau/wohnhaus_mensch_s1_g<n>_innen.glb   Innenraum (im Spiel „InteriorRoot“)
    und seine Zelle in den beiden AO-Atlanten der Gruppe (ao_atlas.json).

Grundformen (Entscheidung des Nutzers, 14.11.2026): 1 schmal und hoch (wohnhaus_mensch_s1_g1.py, die Probe), 2 breit,
3 mit Anbau bzw. Schuppen, 4 mit Hof bzw. Laube. Alles Weitere wie bei der Probe (docs/bereiche/darstellung.md):
Budget 1.500-3.500 Dreiecke, gemeinsames Trim-Sheet, Strohdach in drei Stufen, Fensterstoecke in 3D mit Butzenglas,
Innenraum massgleich, echte Haustuer, Leiter, Gegenstaende statt Reiter, Plaetze der Familie als Anker.

Aufbau:
  * Grundform 3 ist das Haus der Probe (alle Teile aus wohnhaus_mensch_s1_g1.py), gespiegelt, mit einem Bretterschuppen
    unter einem Pultdach an der Seite; das Scheitholz liegt dafuer an der anderen Seitenwand.
  * Grundform 2 und 4 sind anderthalb Geschosse hoch (Erdgeschoss in Fachwerk, Kniestock, Schlafgalerie unter dem Dach):
    Grundform 2 steht mit der Traufe zur Gasse (7,0 x 4,8 m, Tuer in der Traufwand, Herd am Giebel mit Schornstein am First),
    Grundform 4 mit dem Giebel (5,0 x 4,8 m), das Dach ist als Krueppelwalm 1,6 m ueber eine Laube auf drei Pfosten vorgezogen.
    Beide entstehen im „Baurahmen“ der Probe (First entlang y, Giebel bei VORN und HINTEN, Traufen bei +-x) und werden
    am Ende in die Lage im Spiel gedreht (Vorderseite bei -Y, Front bei y = -3).
  * Die Moebel, Gegenstaende und Einrichtungsstuecke sind dieselben wie in der Probe: Sie werden von den Funktionen der
    Probe in deren Massen gebaut und als starre Gruppen an ihre Wand im neuen Raum versetzt (`versetze`).
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import netzbau                                                   # noqa: E402,F401
from netzbau import ROT, Teil                                      # noqa: E402
from trimsheet import BAENDER_AUSSEN, BAENDER_INNEN                # noqa: E402
import wohnhaus_mensch_s1_g1 as g1                                 # noqa: E402  (die Probe: Moebel, Gegenstaende, Blender-Helfer)

ZIEL_MODELL = g1.ZIEL_MODELL
feld = g1.feld
EZ = Vector((0, 0, 1))

# ── Feste Masse (wie die Probe) ──────────────────────────────────────────────────────────────────────────────────────
T = 0.22                    # Wandstaerke
Z_BODEN = 0.15              # Fussboden innen
Z_SOCKEL = 0.45             # Oberkante des Bruchsteinsockels
Z_OG = 2.55                 # Oberkante Erdgeschoss (Schwelle des Kniestocks)
KNIE = 0.80                 # Hoehe des Kniestocks
Z_TRAUFE = Z_OG + KNIE
NEIGUNG = math.tan(math.radians(50))
UEBER = 0.38                # Dachueberstand an der Traufe
STROH = 0.28
Z_GALERIE = Z_OG + 0.12     # Oberkante der Galeriebohlen
Z_HAUBE = 1.95              # Unterkante der Herdhaube (1,80 m ueber dem Fussboden)
TUER_HOCH = 2.00
KEHL = 1.45                 # Hoehe des Kehlbalkens ueber der Traufe (innen 2,13 m ueber dem Galerieboden)
GB_FENSTER = (3.50, Z_TRAUFE + KEHL - 0.08)   # Giebelfenster der Galerie: Bruestung 0,83 m ueber dem Galerieboden, der Kehlbalken ist sein Sturz
EG_FENSTER = (1.05, 2.00)   # Bruestung und Sturz aller Fenster des Erdgeschosses (Fachwerk: Riegel in einer Hoehe durchs Geschoss)
LEITER_LAUF = 0.77
BW = g1.BW                  # Breite der Fachwerkstaender

# Masse der Probe, auf die sich die Moebelgruppen beziehen (Innenwaende der Probe)
G1_IX, G1_IV, G1_IH, G1_JV = g1.IX, g1.IV, g1.IH, g1.JV


# ── Grundform: wird in form_setzen gefuellt ──────────────────────────────────────────────────────────────────────────
class F:
    """Masse und Lage der gewaehlten Grundform (Baurahmen)."""


def form_setzen(n):
    F.nr = n
    F.name = f"wohnhaus_mensch_s1_g{n}"
    if n == 2:
        F.BX, F.VORN, F.HINTEN = 2.4, -3.5, 3.5
        _q = lambda k: g1.gefach(-F.BX, 2 * F.BX, k)               # Giebelseiten 4,8 m: fuenf Gefache (Achsabstand 0,93)
        _l = lambda k: g1.gefach(F.VORN, F.HINTEN - F.VORN, k)     # Traufseiten 7,0 m: sieben Gefache (0,98)
        F.DACH_VORN, F.DACH_HINTEN = -3.85, 3.85
        F.STRASSE = "links_eg"                         # die Traufwand zur Gasse
        F.WELT = (((0, -1), (1, 0)), (0.0, -0.2))      # Baurahmen -> Spiel: Vierteldrehung, Strassenwand bei y = -2,6
        F.FENSTER = [                                  # je ein Gefach; zur Gasse die Tuer im mittleren, die Fenster spiegelgleich im 2. und 6.
            ("links_eg", _l(3), (Z_BODEN, Z_BODEN + TUER_HOCH), "tuer"),
            ("links_eg", _l(1), EG_FENSTER, "fenster"),
            ("links_eg", _l(5), EG_FENSTER, "fenster"),
            ("rechts_eg", _l(4), EG_FENSTER, "fenster"),
            ("hinten_eg", _q(2), EG_FENSTER, "fenster"),
            ("hinten_gb", _q(2), GB_FENSTER, "fenster"),
        ]
        F.WALM = None
        F.LAUBE = False
        F.GALERIE_AB, F.LEITER_X = 1.70, 0.0
        F.KAMIN = (-0.50, 0.20, -3.5 + T, -3.5 + T + 0.60)       # Schlot innen an der Giebelwand, aussen am First
        F.MATTEN = ((-0.6, -1.3, 1.5, 1.6), (0.3, 2.0, 1.5, 1.0))
        F.FAHNE = (0.0, F.DACH_HINTEN - 0.55)
        F.WAPPEN = ((-F.BX, 0.845, 1.53), (0, -1, 0), (-1, 0, 0))  # Ort, Rechts des Betrachters, Normale: Gefach neben der Tuer, frei zwischen Tuer und Laden
        F.BANK = ("links_eg", 1.75, 2.95)
        F.FASS = (F.BX + 0.36, F.HINTEN - 0.45)
        F.SCHEIT = ("rechts_eg", -2.9)
        F.GERUEST = dict(y=-2.6, x0=-3.6, x1=3.6, ebenen=(1.9,), innen=3.3, aussen=3.9, leiter_x=2.9)
    elif n == 4:
        F.BX, F.VORN, F.HINTEN = 2.5, -1.4, 3.4
        _q = lambda k: g1.gefach(-F.BX, 2 * F.BX, k)               # Giebelseiten 5,0 m: fuenf Gefache (Achsabstand 0,97)
        _l = lambda k: g1.gefach(F.VORN, F.HINTEN - F.VORN, k)     # Traufseiten 4,8 m: fuenf Gefache (0,93)
        F.DACH_VORN, F.DACH_HINTEN = -2.88, 3.62
        F.STRASSE = "vorn_eg"
        F.WELT = (((1, 0), (0, 1)), (0.0, 0.0))
        F.FENSTER = [
            ("vorn_eg", _q(3), (Z_BODEN, Z_BODEN + TUER_HOCH), "tuer"),       # je ein Gefach, Tuer und Fenster spiegelgleich
            ("vorn_eg", _q(1), EG_FENSTER, "fenster"),
            ("links_eg", _l(3), EG_FENSTER, "fenster"),                 # im vierten Gefach: im dritten laege der Bauplan am Tisch davor
            ("hinten_eg", _q(2), EG_FENSTER, "fenster"),
            ("hinten_gb", _q(2), GB_FENSTER, "fenster"),
        ]
        F.WALM = math.tan(math.radians(55))            # Krueppelwalm ueber der Laube: Neigung der Walmflaeche
        F.LAUBE = True
        F.GALERIE_AB, F.LEITER_X = 1.80, -1.0
        F.KAMIN = (F.BX - T - 0.60, F.BX - T, 0.90, 1.60)
        F.MATTEN = ((-0.6, -0.5, 1.5, 1.6), (-0.4, 2.15, 1.5, 1.0))
        F.FAHNE = None                                 # ergibt sich aus dem Walm (siehe main)
        F.WAPPEN = ((0.0, F.DACH_VORN + 0.18, 4.10), (1, 0, 0), (0, -1, 0))   # auf der Verbretterung des Laubengiebels
        F.BANK = ("vorn_eg", -1.55, -0.35)
        F.FASS = (-F.BX - 0.36, F.VORN + 0.45)
        F.SCHEIT = ("rechts_eg", 2.0)
        F.GERUEST = dict(y=F.DACH_VORN + 0.08, x0=-2.6, x1=2.6, ebenen=(1.9, 3.9), innen=4.5, aussen=5.3, leiter_x=1.75)
    elif n == 3:
        F.WELT = (((-1, 0), (0, 1)), (1.05, 0.0))      # gespiegelt, damit der Schuppen links steht; in die Mitte des Platzes geschoben
        return
    else:
        raise SystemExit("--form=2, 3 oder 4 (Grundform 1 baut wohnhaus_mensch_s1_g1.py)")
    F.OX = F.BX
    F.Z_FIRST = Z_TRAUFE + F.OX * NEIGUNG
    F.IX, F.IV, F.IH = F.BX - T, F.VORN + T, F.HINTEN - T
    F.XK = F.OX - KEHL / NEIGUNG                       # halbe Breite des Giebels in Hoehe des Kehlbalkens
    F.ZK = Z_TRAUFE + KEHL
    F.MITTE = Vector((0, (F.VORN + F.HINTEN) / 2, 3.0))
    F.RAUM = Vector((0, (F.VORN + F.HINTEN) / 2, 1.6))
    bx, v, h, xk = F.BX, F.VORN, F.HINTEN, F.XK
    # Waende: Name -> (Ursprung aussen, Richtung, Breite, Normale nach aussen); „_gb“ ist das Mittelfeld des Giebels
    F.WAENDE = {
        "vorn_eg": ((-bx, v), (1, 0), 2 * bx, (0, -1)), "hinten_eg": ((-bx, h), (1, 0), 2 * bx, (0, 1)),
        "links_eg": ((-bx, v), (0, 1), h - v, (-1, 0)), "rechts_eg": ((bx, v), (0, 1), h - v, (1, 0)),
        "vorn_og": ((-bx, v), (1, 0), 2 * bx, (0, -1)), "hinten_og": ((-bx, h), (1, 0), 2 * bx, (0, 1)),
        "links_og": ((-bx, v), (0, 1), h - v, (-1, 0)), "rechts_og": ((bx, v), (0, 1), h - v, (1, 0)),
        "vorn_gb": ((-xk, v), (1, 0), 2 * xk, (0, -1)), "hinten_gb": ((-xk, h), (1, 0), 2 * xk, (0, 1)),
    }
    if F.WALM:
        xe = F.OX + UEBER
        F.X_WALM = xe / 3                              # die oberste Dachstufe ist abgewalmt
        F.Z_WALM = F.Z_FIRST - F.X_WALM * NEIGUNG
        F.D_WALM = (F.Z_FIRST - F.Z_WALM) / F.WALM     # um so viel beginnt der First hinter der Giebelkante


def loecher(wand):
    (ox, oy), (ex, ey), _, _ = F.WAENDE[wand]
    start = ox if ex else oy
    return [(a0 - start, a1 - start, z0, z1, art) for w, (a0, a1), (z0, z1), art in F.FENSTER if w == wand]


def punkt(wand, s, z, raus=0.0):
    (ox, oy), (ex, ey), _, (nx, ny) = F.WAENDE[wand]
    return Vector((ox + ex * s + nx * raus, oy + ey * s + ny * raus, z))


def dach_z(x, oben=False):
    return F.Z_FIRST - abs(x) * NEIGUNG + (STROH if oben else 0.0)


def wandkasten(teil, stoff, wand, s0, s1, r0, r1, z0, z1, band, **kw):
    """Kasten an einer Wand: von s0 bis s1 entlang der Wand, von r0 bis r1 nach aussen (negativ: nach innen)."""
    a, b = punkt(wand, s0, z0, r0), punkt(wand, s1, z1, r1)
    teil.kasten(stoff, (min(a.x, b.x), min(a.y, b.y), min(a.z, b.z)), (max(a.x, b.x), max(a.y, b.y), max(a.z, b.z)), band, **kw)


def faerbe(teil, ab, feldname):
    """Legt die seit `ab` gebauten Flaechen auf ein Farbfeld des Bandes „kleinteile“ (Eisen, Russ ...)."""
    ua, ub = feld(feldname); k0, k1, _ = BAENDER_INNEN["kleinteile"]
    for fl in teil.flaechen[ab:]:
        fl[1][:] = [(ua, k1), (ub, k1), (ub, k0), (ua, k0)][:len(fl[1])]


def bretterwand(teil, stoff, a, b, z_unten, z_oben, band, breite=0.46, kanten=(), **kw):
    """Stehende Bretter von a nach b (Punkte am Boden der Wand): Bahnen von rund `breite`, jede fuellt quer das Band; die
    Oberkante folgt z_oben(Punkt). `kanten`: Anteile 0..1 entlang der Wand, an denen eine Bahn enden muss (Knicke der Oberkante)."""
    a, b = Vector(a), Vector(b)
    laenge = (b - a).length
    n = max(1, round(laenge / breite))
    ts = sorted({i / n for i in range(n + 1)} | {k for k in kanten if 0.02 < k < 0.98})
    v0, v1, m = teil.baender[band]
    for t0, t1 in zip(ts[:-1], ts[1:]):
        p0, p1 = a.lerp(b, t0), a.lerp(b, t1)
        h0, h1 = z_oben(p0) - z_unten, z_oben(p1) - z_unten
        if max(h0, h1) < 0.03: continue
        u0 = teil.zufall.random()
        pts = [Vector((p0.x, p0.y, z_unten)), Vector((p1.x, p1.y, z_unten)), Vector((p1.x, p1.y, z_unten + h1)), Vector((p0.x, p0.y, z_unten + h0))]
        teil.vieleck(stoff, pts, [(u0, v1), (u0, v0), (u0 + h1 / m, v0), (u0 + h0 / m, v1)], **kw)


def versetze(teile, m=((1, 0), (0, 1)), t=(0.0, 0.0), dz=0.0):
    """Versetzt Teile starr: (x, y) -> m * (x, y) + t, z + dz. Bei einer Spiegelung dreht sich der Umlauf der Flaechen mit."""
    (a, b), (c, d) = m
    spiegel = a * d - b * c < 0
    for teil in teile if isinstance(teile, (list, tuple)) else [teile]:
        neu = []
        for pts, uvs, stoff, farbe, glatt in teil.flaechen:
            pts = [Vector((a * p.x + b * p.y + t[0], c * p.x + d * p.y + t[1], p.z + dz)) for p in pts]
            if spiegel: pts, uvs = pts[::-1], uvs[::-1]
            neu.append((pts, uvs, stoff, farbe, glatt))
        teil.flaechen = neu
    return teile


class Anker:
    """Leerer Knoten, erst am Ende angelegt (nach dem Drehen ins Spiel). `blick`: Richtung im Baurahmen, in die eine Figur schaut."""
    RICHTUNG = {"nord": (0, 1), "west": (-1, 0), "sued": (0, -1), "ost": (1, 0)}

    def __init__(self, name, ort, blick=None, **extras):
        self.name, self.ort, self.extras = name, Vector(ort), extras
        self.blick = Vector((*self.RICHTUNG[blick], 0)) if isinstance(blick, str) else (Vector(blick) if blick is not None else None)

    def versetze(self, m=((1, 0), (0, 1)), t=(0.0, 0.0), dz=0.0):
        (a, b), (c, d) = m
        self.ort = Vector((a * self.ort.x + b * self.ort.y + t[0], c * self.ort.x + d * self.ort.y + t[1], self.ort.z + dz))
        if self.blick is not None: self.blick = Vector((a * self.blick.x + b * self.blick.y, c * self.blick.x + d * self.blick.y, 0))
        return self

    def objekt(self):
        o = bpy.data.objects.new(self.name, None)
        o.location = self.ort
        if self.blick is not None: o.rotation_euler = (0, 0, math.atan2(-self.blick.x, self.blick.y))   # wie in der Probe: Nord = 0, West = 90 Grad
        for k, v in self.extras.items(): o[k] = v
        bpy.context.scene.collection.objects.link(o)
        return o


def versetze_alle(dinge, m, t, dz=0.0):
    for d in dinge:
        if isinstance(d, Anker): d.versetze(m, t, dz)
        else: versetze(d, m, t, dz)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Aussen: anderthalb Geschosse (Grundform 2 und 4)
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def fensterstock(teil, stoff, wand, s0, s1, z0, z1, band):
    """Echter Fensterstock wie in der Probe: Rahmen 5 x 7 cm rundum und Holzkreuz 4,5 x 5,5 cm, mittig in der Wand."""
    _, _, _, n = F.WAENDE[wand]
    n3 = (n[0], n[1], 0)
    P = lambda s, z: punkt(wand, s, z, -T / 2)
    sm, zm = (s0 + s1) / 2, (z0 + z1) / 2
    for a, b in ((P(s0, z0 + 0.025), P(s1, z0 + 0.025)), (P(s0, z1 - 0.025), P(s1, z1 - 0.025)), (P(s0 + 0.025, z0 + 0.05), P(s0 + 0.025, z1 - 0.05)), (P(s1 - 0.025, z0 + 0.05), P(s1 - 0.025, z1 - 0.05))):
        teil.stab(stoff, a, b, 0.07, band, breite=0.05, oben=n3)
    teil.stab(stoff, P(sm, z0 + 0.05), P(sm, z1 - 0.05), 0.055, band, breite=0.045, oben=n3)
    teil.stab(stoff, P(s0 + 0.05, zm), P(s1 - 0.05, zm), 0.055, band, breite=0.045, oben=n3)


def fachwerk(h, wand, z0, z1, ganz=True, raehm=True):
    """Fachwerk einer Wand zwischen z0 und z1 nach der Konstruktionsregel (g1.fachwerk_wand): Schwelle (an Tueren unterbrochen),
    Raehm, Staender im regelmaessigen Raster, jede Oeffnung genau ein Gefach; mit `ganz` dazu Brust- und Sturzriegel in einer
    Hoehe und Streben in den Eckgefachen (ohne: Kniestock, nur Staender)."""
    _, _, breite, n = F.WAENDE[wand]
    g1.fachwerk_wand(h, lambda s, z: punkt(wand, s, z), breite, (n[0], n[1], 0), z0, z1, loecher(wand), raehm=raehm, riegel=ganz, z_fenster=EG_FENSTER,
                     stoss="kappe" if wand.startswith(("vorn", "hinten")) else "buendig")   # Ecken ohne deckungsgleiche Balken


def scheitholz(h, wand, a_start):
    """Scheitholzstapel an einer Wand unter dem Dachueberstand (zwoelf Scheite in drei Lagen, Stirnseiten hell). Achsabstand 0,18 m =
    1 cm Fuge und gerade wie im Steinhaus (vorher 2 cm und schraeg: an der Stirn bis 1,8 cm, Durchblick bis an die Hauswand,
    luecken_pruefung; Mensch-Nachtrag 16.11.2026)."""
    (ox, oy), (ex, ey), _, _ = F.WAENDE[wand]
    start = ox if ex else oy
    for i, (ds, z) in enumerate([(0.18 * k, 0.09) for k in range(5)] + [(0.09 + 0.18 * k, 0.26) for k in range(4)] + [(0.18 + 0.18 * k, 0.43) for k in range(3)]):
        s = a_start - start + ds
        a, b = punkt(wand, s, z, 0.03), punkt(wand, s, z, 0.62)
        h.stab("Fachwerk", a, b, 0.17, "balken_a", kappen=False)
        quer = Vector((ex, ey, 0))
        h.rechteck("Fachwerk", b - quer * 0.085 - EZ * 0.085, quer, EZ, 0.17, 0.17, "farbholz", weg_von=a, anteil=(0.1, 0.9), u_bereich=(0.1 + 0.07 * i, 0.14 + 0.07 * i))


def baue_haus():
    h = Teil("Haus", BAENDER_AUSSEN, saat=11 + F.nr)
    lehm, holz, stein = "Lehmputz", "Fachwerk", "Bruchstein"
    M = F.MITTE
    umfang = 0.0
    for wand in ("vorn_eg", "rechts_eg", "hinten_eg", "links_eg"):
        (ox, oy), (ex, ey), breite, n = F.WAENDE[wand]
        o, eu = (ox, oy, 0), (ex, ey, 0)
        l = [x[:4] for x in loecher(wand)]
        tuer = [x for x in l if x[2] < Z_SOCKEL]
        h.wand(stein, o, eu, breite, -0.12, Z_SOCKEL, "bruchstein", loecher=[(a, b, c, min(d, Z_SOCKEL + 1)) for a, b, c, d in tuer], hoehe=Z_SOCKEL + 0.12, zbasis=-0.12, weg_von=M, u_start=umfang)
        h.wand(lehm, o, eu, breite, Z_SOCKEL, Z_OG, "putz", loecher=l, zbasis=Z_SOCKEL, laibung=(holz, "balken_c"), laibung_tiefe=T, weg_von=M, u_start=umfang)
        umfang += breite
    for wand in ("vorn_og", "rechts_og", "hinten_og", "links_og"):                # Kniestock
        (ox, oy), (ex, ey), breite, n = F.WAENDE[wand]
        h.wand(lehm, (ox, oy, 0), (ex, ey, 0), breite, Z_OG, Z_TRAUFE, "putz", zbasis=Z_OG, hoehe=2.3, weg_von=M, u_start=umfang)
        umfang += breite
    # Giebel: links und rechts ein Dreieck, in der Mitte das Feld mit dem Fenster, darueber das Dreieck bis zum First
    OX, xk, zk = F.OX, F.XK, F.ZK
    for gb, y in (("vorn_gb", F.VORN), ("hinten_gb", F.HINTEN)):
        o, eu = (-OX, y, 0), (1, 0, 0)
        for s in (-1, 1):
            h.welt(lehm, [(s * OX, y, Z_TRAUFE), (s * xk, y, Z_TRAUFE), (s * xk, y, zk)], "putz", o, eu, Z_OG, 2.3 + KEHL, weg_von=M)
        h.wand(lehm, (-xk, y, 0), eu, 2 * xk, Z_TRAUFE, zk, "putz", loecher=[x[:4] for x in loecher(gb)], zbasis=Z_OG, hoehe=2.3 + KEHL, laibung=(holz, "balken_c"), laibung_tiefe=T, weg_von=M, u_start=OX - xk)
        h.welt(lehm, [(-xk, y, zk), (xk, y, zk), (0, y, F.Z_FIRST)], "putz", o, eu, zk, F.Z_FIRST - zk, weg_von=M)

    # ── Fachwerk: Erdgeschoss mit Riegeln und Streben, Kniestock, Giebel ─────────────────────────────────────────────
    for wand in ("vorn_eg", "rechts_eg", "hinten_eg", "links_eg"): fachwerk(h, wand, Z_SOCKEL, Z_OG, raehm=False)   # die Schwelle des Kniestocks ist zugleich das Raehm
    for wand in ("vorn_og", "rechts_og", "hinten_og", "links_og"): fachwerk(h, wand, Z_OG, Z_TRAUFE, ganz=False)
    for gb, y, aussen in (("vorn_gb", F.VORN, -1), ("hinten_gb", F.HINTEN, 1)):              # Giebel bis zum First
        fenster = [(a0, a1) for w, (a0, a1), _, _ in F.FENSTER if w == gb]
        g1.giebel_fachwerk(h, y, aussen, OX, Z_TRAUFE, F.Z_FIRST, NEIGUNG, fenster[0] if fenster else None, GB_FENSTER)

    # ── Tuer, Fensterstoecke, Laeden ─────────────────────────────────────────────────────────────────────────────────
    strasse = F.STRASSE[:-3]
    for wand, (a0, a1), (z0, z1), art in F.FENSTER:
        (ox, oy), (ex, ey), breite, n = F.WAENDE[wand]
        n3 = Vector((n[0], n[1], 0)); e3 = (ex, ey, 0)
        start = ox if ex else oy
        s0, s1 = a0 - start, a1 - start
        P = lambda s, z, raus=0.0: punkt(wand, s, z, raus)
        if art == "tuer":
            # Tuerblatt 8 cm zurueckgesetzt (drei Bahnen, Maserung senkrecht), zwei Eisenbaender, Steinstufe davor; den Rahmen bildet das Fachwerk
            for i in range(3):
                h.rechteck(holz, P(s0 + i * (s1 - s0) / 3, z0, -0.08), e3, EZ, (s1 - s0) / 3, z1 - z0, "planken", laengs="v", weg_von=M)
            for z in (z0 + 0.45, z1 - 0.4):
                h.rechteck(stein, P(s0 + 0.03, z - 0.035, -0.07), e3, EZ, 0.62, 0.07, "eisen", weg_von=M)
            wandkasten(h, stein, wand, s0 - 0.15, s1 + 0.15, 0.0, 0.36, -0.12, Z_BODEN, "bruchstein", ohne=("-z",))
            h.rechteck(stein, P(s0 + 0.12, 1.05, -0.072), e3, EZ, 0.1, 0.14, "eisen", weg_von=M)                    # Tuerring
            continue
        # Fensterbank 5 mm unter der Oeffnung: sonst liegt ihre Oberseite in einer Ebene mit der des Brustriegels (deckungsgleich)
        h.brett(holz, P(s0 - 0.12, z0 - 0.055), P(s1 + 0.12, z0 - 0.055), 0.10, n3, "balken_c", tiefe=0.08)          # Fensterbank
        fensterstock(h, holz, wand, s0, s1, z0, z1, "balken_c")
        # Laeden: zur Gasse zwei Fluegel, sonst einer; offen an der Wand, Eckenfarbe Rot = Akzentfarbe je Haus
        lb = (s1 - s0) / 2
        fluegel = [(s0 - lb - 0.03, s0 - 0.03), (s1 + 0.03, s1 + lb + 0.03)] if wand.startswith(strasse) else [(s1 + 0.03, s1 + 2 * lb * 0.55 + 0.03)]
        for f0, f1 in fluegel:
            if f0 < 0.02 or f1 > breite - 0.02: continue
            h.rechteck(holz, P(f0, z0 - 0.02, 0.085), e3, EZ, f1 - f0, z1 - z0 + 0.04, "farbholz", laengs="v", weg_von=M, farbe=ROT)
            for z in (z0 + 0.12, z1 - 0.12):
                h.rechteck(stein, P(f0, z - 0.03, 0.091), e3, EZ, f1 - f0, 0.06, "eisen", weg_von=M)

    # ── Bank unter einem Fenster zur Gasse, Regenfass, Scheitholz ────────────────────────────────────────────────────
    wand, a0, a1 = F.BANK
    (ox, oy), (ex, ey), _, _ = F.WAENDE[wand]
    b0, b1 = a0 - (ox if ex else oy), a1 - (ox if ex else oy)
    wandkasten(h, holz, wand, b0, b1, 0.06, 0.38, 0.42, 0.48, "balken_b")
    for s in (b0 + 0.14, b1 - 0.14):
        wandkasten(h, holz, wand, s - 0.05, s + 0.05, 0.10, 0.34, 0.0, 0.42, "balken_c", ohne=("-z", "+z"), laengs="z")
    fx, fy = F.FASS
    h.dreh(holz, (fx, fy, 0.0), [(0.25, 0.0), (0.31, 0.3), (0.31, 0.5), (0.26, 0.8)], "planken", seiten=10, deckel=False, gedreht=True)
    h.dreh(stein, (fx, fy, 0.72), [(0.255, 0.0), (0.0, 0.0)], "eisen", seiten=8, deckel=False, anteil=(0.0, 0.3))
    for z in (0.18, 0.62):
        h.dreh(stein, (fx, fy, z), [(0.305 if z < 0.5 else 0.30, 0.0), (0.305 if z < 0.5 else 0.295, 0.05)], "eisen", seiten=8, deckel=False)
    scheitholz(h, *F.SCHEIT)

    # ── Schornstein ueber dem Dach (innen laeuft der Schlot in derselben Lage) ───────────────────────────────────────
    x0, x1, y0, y1 = F.KAMIN
    nah = 0.0 if x0 < 0 < x1 else min(abs(x0), abs(x1))
    zu = dach_z(max(abs(x0), abs(x1)), True) - 0.25
    zo = max(F.Z_FIRST - 0.35, dach_z(nah, True) + 0.85)
    z = zu
    while z < zo - 1e-3:
        zn = min(z + 0.5, zo)
        h.kasten(stein, (x0, y0, z), (x1, y1, zn), "bruchstein", ohne=("-z", "+z"), laengs="x")
        z = zn
    h.kasten(stein, (x0 - 0.05, y0 - 0.05, zo), (x1 + 0.05, y1 + 0.05, zo + 0.12), "bruchstein", laengs="x")
    h.rechteck(stein, (x0 + 0.1, y0 + 0.1, zo + 0.125), (1, 0, 0), (0, 1, 0), x1 - x0 - 0.2, y1 - y0 - 0.2, "eisen", zu=(0, 0, 99), anteil=(0.0, 0.2))
    F.Z_KAMIN = zo

    if F.LAUBE: baue_laube(h)
    return h


def baue_laube(h):
    """Laube vor dem Giebel (Grundform 4): drei Pfosten auf Steinen tragen das vorgezogene Dach - Rahmholz vorn, zwei Pfetten zur
    Hauswand, Kopfbaender; der Giebel darueber ist bis unter den Walm verbrettert."""
    holz, stein = "Fachwerk", "Bruchstein"
    yl = F.DACH_VORN + 0.18
    zr = Z_TRAUFE - 0.06                                   # Mitte des Rahmholzes
    xs = (-F.BX + 0.09, 0.0, F.BX - 0.09)
    for x in xs:
        h.kasten(stein, (x - 0.17, yl - 0.17, -0.12), (x + 0.17, yl + 0.17, 0.10), "bruchstein", ohne=("-z",))
        h.stab(holz, (x, yl, 0.10), (x, yl, zr - 0.09), 0.16, "balken_a")
    h.stab(holz, (-F.OX - 0.12, yl, zr), (F.OX + 0.12, yl, zr), 0.18, "balken_c", kappen=True)
    for x in (xs[0], xs[2]):
        h.stab(holz, (x, yl + 0.09, zr), (x, F.VORN, zr), 0.18, "balken_c")
        h.stab(holz, (x, yl + 0.08, zr - 0.72), (x, yl + 0.70, zr - 0.09), 0.10, "balken_a")       # Kopfband zur Pfette
    for x, richtungen in ((xs[0], (1,)), (xs[1], (-1, 1)), (xs[2], (-1,))):
        for r in richtungen:
            h.stab(holz, (x + r * 0.08, yl, zr - 0.72), (x + r * 0.70, yl, zr - 0.09), 0.10, "balken_a")
            # das Dreieck zwischen Pfosten, Rahmholz und Kopfband mit stehenden Brettern gefuellt (durch die Felder sah man 0,6 m tief
            # bis an die Hauswand, luecken_pruefung „Durchblick“; die Laube bleibt darunter offen; Mensch-Nachtrag 16.11.2026, wie bei
            # den Zwergen). Bretter von beiden Seiten sichtbar; u laeuft die Hoehe hinauf, v quer ueber die Bretter im Band „planken“
            a, b, c = Vector((x + r * 0.08, yl, zr - 0.72)), Vector((x + r * 0.08, yl, zr - 0.09)), Vector((x + r * 0.70, yl, zr - 0.09))
            for dy, seite in ((-0.012, -1), (0.012, 1)):
                v = Vector((0, dy, 0))
                h.vieleck(holz, [a + v, b + v, c + v], [(0.10, 0.705), (0.2575, 0.705), (0.2575, 0.77)], weg_von=(x, yl - seite * 5, zr - 0.3))
    # Verbretterung des Giebels ueber dem Rahmholz: stehende Bretter bis unter die Dachflaechen und den Walm
    oben = lambda p: min(dach_z(p.x) - 0.02, F.Z_WALM + 0.10)
    bretterwand(h, holz, (-F.OX, yl, 0), (F.OX, yl, 0), zr + 0.09, oben, "planken",
                kanten=((F.OX - F.X_WALM) / (2 * F.OX), (F.OX + F.X_WALM) / (2 * F.OX)), weg_von=F.MITTE)


def baue_dach():
    """Strohdach in drei ueberlappenden Stufen wie in der Probe; die Zahl der Felder entlang des Firsts folgt der Laenge.
    Mit Walm (Grundform 4) laeuft die Giebelkante der obersten Stufe schraeg zum First zurueck, davor liegt die Walmflaeche."""
    d = Teil("Dach", BAENDER_AUSSEN, saat=23 + F.nr)
    M = F.MITTE
    OX, DV, DH = F.OX, F.DACH_VORN, F.DACH_HINTEN
    xe = OX + UEBER
    laenge = DH - DV
    stufen = 3
    felder = max(5, round(laenge / 0.8))
    halb = felder // 2
    dick_unten, dick_oben = 0.30, 0.15
    ys = [DV + laenge * j / felder for j in range(felder + 1)]
    v0, v1, m = BAENDER_AUSSEN["stroh"]
    f0, f1, fm = BAENDER_AUSSEN["stroh_franse"]
    walm = F.WALM

    def kante(x, y):
        """Lage der vorderen Giebelkante: unter dem Walm senkrecht, darueber laeuft sie zum First zurueck."""
        if not walm or y > DV + 1e-6: return y
        return y + max(0.0, dach_z(x) - F.Z_WALM) / walm

    for seite in (-1, 1):
        def P(a, hoch, y=0.0):
            x = seite * xe * (1 - a)
            return Vector((x, kante(x, y), dach_z(x) + hoch))
        # Untersicht (von innen die Decke)
        d.vieleck("Stroh", [P(0, 0, DV), P(0, 0, DH), P(1, 0, DH), P(1, 0, DV)], [(0, 0.02), (laenge / 4, 0.02), (laenge / 4, 0.07), (0, 0.07)], zu=(0, M.y, 0))
        if walm:
            d.vieleck("Stroh", [P(0, 0, DV), P(2 / 3, 0, DV), P(1, 0, DV)], [(0, 0.02), (0.2, 0.02), (0.3, 0.07)], zu=(0, M.y, 0))
        hang = (P(0, 0) - P(1, 0)).normalized() * 0.75 + Vector((0, 0, -0.25))
        for k in range(stufen):
            a0, a1 = k / stufen, min(1.0, (k + 1) / stufen + 0.02)
            u0 = d.zufall.random()
            wellen = [(d.zufall.uniform(-0.022, 0.022), d.zufall.uniform(-0.006, 0.006)) for _ in ys]
            unten = [P(a0 + da, dick_unten + dz, y) for y, (dz, da) in zip(ys, wellen)]
            oben = [P(a1, dick_oben, y) for y in ys]
            fuss = [P(a0 + da, 0.0 if k == 0 else dick_oben - 0.01, y) for y, (dz, da) in zip(ys, wellen)]
            for j in range(felder):
                ua, ub = u0 + (ys[j] - DV) / m, u0 + (ys[j + 1] - DV) / m
                d.vieleck("Stroh", [unten[j], unten[j + 1], oben[j + 1], oben[j]], [(ua, v1), (ub, v1), (ub, v0), (ua, v0)], weg_von=M)
                d.vieleck("Stroh", [fuss[j], fuss[j + 1], unten[j + 1], unten[j]], [(ua, v0 + 0.01), (ub, v0 + 0.01), (ub, v0 + 0.035), (ua, v0 + 0.035)], weg_von=M + Vector((0, 0, 1)))
            for j, aussen in ((0, -1), (felder, 1)):       # Giebelseiten
                sl = (oben[j] - unten[j]).length
                d.vieleck("Stroh", [P(a0, 0, ys[j]), P(a1, 0, ys[j]), oben[j], unten[j]],
                          [(u0, v0 + 0.03), (u0 + sl / m, v0 + 0.03), (u0 + sl / m, v0 + 0.06), (u0, v0 + 0.085)], weg_von=(0, ys[j] - aussen * 9, 5))
            tief = 0.27 if k == 0 else 0.21
            for ja, jb in ((0, halb), (halb, felder)):     # Alpha-Cards unter der Stufenkante
                ka, kb = unten[ja] + Vector((0, 0, -0.03)), unten[jb] + Vector((0, 0, -0.03))
                ua, ub = u0 + (ys[ja] - DV) / fm, u0 + (ys[jb] - DV) / fm
                d.vieleck("Strohkante", [ka, kb, kb + hang * tief, ka + hang * tief], [(ua, f0), (ub, f0), (ub, f1), (ua, f1)], weg_von=M)
            if k == 0:
                k2a, k2b = P(0.035, dick_unten * 0.5, DV), P(0.035, dick_unten * 0.5, DH)
                d.vieleck("Strohkante", [k2a, k2b, k2b + hang * 0.3, k2a + hang * 0.3],
                          [(u0 + 0.37, f0), (u0 + 0.37 + laenge / fm, f0), (u0 + 0.37 + laenge / fm, f1), (u0 + 0.37, f1)], weg_von=M)
        # Alpha-Cards am Ortgang (mit Walm vorn nur bis zum Fuss der Walmflaeche)
        for y, aussen in ((DV, -1), (DH, 1)):
            oben_a = 2 / 3 if (walm and aussen < 0) else 1.0
            a, b = P(0, dick_unten * 0.8, y), P(oben_a, dick_oben, y)
            ab = Vector((0, aussen * 0.05, -0.22))
            sl = (b - a).length
            d.vieleck("Strohkante", [a, b, b + ab, a + ab], [(0.2, f0), (0.2 + sl / fm, f0), (0.2 + sl / fm, f1), (0.2, f1)], weg_von=(0, y - aussen * 9, 5))
        # Sparrenkoepfe unter der Traufe
        koepfe = max(5, round(laenge / 1.0))
        for j in range(koepfe):
            y = DV + 0.45 + j * (laenge - 0.9) / (koepfe - 1)
            x_innen, x_aussen = seite * (OX + 0.01), seite * (xe - 0.07)
            for dy in (-0.05, 0.05):
                d.vieleck("Fachwerk", [(x_innen, y + dy, dach_z(x_innen) - 0.13), (x_aussen, y + dy, dach_z(x_aussen) - 0.13), (x_aussen, y + dy, dach_z(x_aussen) - 0.005), (x_innen, y + dy, dach_z(x_innen) - 0.005)],
                          [(0.1, 0.43), (0.2, 0.43), (0.2, 0.47), (0.1, 0.47)], weg_von=(x_innen, y, 0))
            d.vieleck("Fachwerk", [(x_innen, y - 0.05, dach_z(x_innen) - 0.13), (x_aussen, y - 0.05, dach_z(x_aussen) - 0.13), (x_aussen, y + 0.05, dach_z(x_aussen) - 0.13), (x_innen, y + 0.05, dach_z(x_innen) - 0.13)],
                      [(0.3, 0.43), (0.4, 0.43), (0.4, 0.47), (0.3, 0.47)], zu=(x_innen, y, 0))
    # First: Wulst aus zwei Flaechen (in Feldern, leicht durchhaengend), mit Fransen nach beiden Seiten
    zf = F.Z_FIRST + dick_oben
    yv = DV + (F.D_WALM - 0.12 if walm else -0.02)
    kamm_y = [yv] + ys[1:-1] + [DH + 0.02]
    sack = [-0.055 * math.sin(math.pi * j / felder) for j in range(felder + 1)]
    for seite in (-1, 1):
        kamm = [Vector((0, y, zf + 0.20 + sack[j])) for j, y in enumerate(kamm_y)]
        rand = [Vector((seite * 0.42, k.y, zf - 0.42 * NEIGUNG + 0.16 + sack[j])) for j, k in enumerate(kamm)]
        for j in range(felder):
            ua, ub = 0.5 + (kamm_y[j] - DV) / m, 0.5 + (kamm_y[j + 1] - DV) / m
            d.vieleck("Stroh", [rand[j], rand[j + 1], kamm[j + 1], kamm[j]], [(ua, v0 + 0.06), (ub, v0 + 0.06), (ub, v0 + 0.11), (ua, v0 + 0.11)], weg_von=M)
        ab = Vector((seite * 0.10, 0, -0.17))
        d.vieleck("Strohkante", [rand[0], rand[-1], rand[-1] + ab, rand[0] + ab], [(0.6, f0), (0.6 + laenge / fm, f0), (0.6 + laenge / fm, f1), (0.6, f1)], weg_von=M)
        for j, aussen in ((0, -1), (felder, 1)):
            mitte_unten = Vector((0, kamm[j].y, zf - 0.05))
            if walm and aussen < 0:                    # vorn auf dem Walm: wie bisher (dort fand die Pruefung kein Loch)
                d.vieleck("Stroh", [rand[j], kamm[j], mitte_unten], [(0.1, v0 + 0.03), (0.2, v0 + 0.03), (0.15, v0 + 0.06)], weg_von=(0, kamm[j].y - aussen * 9, 5))
                continue
            # Firstkappe am Ortgang bis ins Stroh der obersten Stufe hinab (Feinschliff 17.11.2026): zwischen Wulst und Dachflaeche blieben
            # 2-3 cm Loch (luecken_pruefung, Pruefung 16)
            fuss = Vector((seite * 0.42, kamm[j].y, dach_z(seite * 0.42) + 0.10))
            d.vieleck("Stroh", [fuss, rand[j], kamm[j], mitte_unten], [(0.1, v0 + 0.02), (0.1, v0 + 0.03), (0.2, v0 + 0.03), (0.15, v0 + 0.06)],
                      weg_von=(0, kamm[j].y - aussen * 9, 5))
    # Windbretter am Giebel (nicht am Walm)
    for y in ((DH + 0.03,) if walm else (DV - 0.03, DH + 0.03)):
        for seite in (-1, 1):
            # das zweite Brett liegt vor dem ersten (sonst decken sich beide in der Kreuzung: flackert, backt schwarz)
            vor = Vector((0, (-1 if y < M.y else 1) * (0.025 if seite == 1 else 0.0), 0))
            a = Vector((seite * 0.45, y, dach_z(0.45) + dick_oben + 0.06)) + vor; b = Vector((-seite * 0.16, y, zf + 0.42)) + vor
            d.brett("Fachwerk", a, b, 0.09, (0, -1 if y < M.y else 1, 0), "balken_c", tiefe=0.025)
            d.brett("Fachwerk", a, b, 0.09, (0, 1 if y < M.y else -1, 0), "balken_c", tiefe=0.0, seiten=False)
    if walm:
        # Walmflaeche: eine Strohstufe vom First hinab bis zur Giebelkante, Schnittkante und Fransen an ihrem Fuss
        xw = F.X_WALM + 0.16
        fuss_l, fuss_r = Vector((-xw, DV - 0.16, F.Z_WALM + 0.12)), Vector((xw, DV - 0.16, F.Z_WALM + 0.12))
        spitze = Vector((0, DV + F.D_WALM + 0.05, zf + 0.14))
        u0 = d.zufall.random()
        d.vieleck("Stroh", [fuss_l, fuss_r, spitze], [(u0, v1), (u0 + 2 * xw / m, v1), (u0 + xw / m, v0)], weg_von=M)
        unter = Vector((0, 0.05, -0.24))
        d.vieleck("Stroh", [fuss_l + unter, fuss_r + unter, fuss_r, fuss_l], [(u0, v0 + 0.01), (u0 + 2 * xw / m, v0 + 0.01), (u0 + 2 * xw / m, v0 + 0.035), (u0, v0 + 0.035)], weg_von=M)
        for s, p in ((-1, fuss_l), (1, fuss_r)):          # Seiten der Walmstufe schliessen
            d.vieleck("Stroh", [p, p + unter, spitze], [(u0, v0 + 0.03), (u0 + 0.05, v0 + 0.03), (u0 + 0.3, v0 + 0.07)], weg_von=M)
        ab = Vector((0, -0.06, -0.24))
        d.vieleck("Strohkante", [fuss_l + unter, fuss_r + unter, fuss_r + unter + ab, fuss_l + unter + ab], [(0.1, f0), (0.1 + 2 * xw / fm, f0), (0.1 + 2 * xw / fm, f1), (0.1, f1)], weg_von=M)
    return d


def scheibe(teil, stoff, wand, a0, a1, z0, z1, raus, **kw):
    """Scheibe in der Oeffnung; UV in Metern wie in der Probe (danach teilt der Butzen-Shader innen und aussen gleich)."""
    (ox, oy), (ex, ey), _, _ = F.WAENDE[wand]
    start = ox if ex else oy
    b, hoch = a1 - a0, z1 - z0
    pts = [punkt(wand, a0 - start, z0, raus), punkt(wand, a1 - start, z0, raus), punkt(wand, a1 - start, z1, raus), punkt(wand, a0 - start, z1, raus)]
    teil.vieleck(stoff, pts, [(0, 1), (b, 1), (b, 1 - hoch), (0, 1 - hoch)], **kw)


def baue_scheiben():
    s = Teil("Scheiben", BAENDER_AUSSEN, saat=5)
    for wand, (a0, a1), (z0, z1), art in F.FENSTER:
        if art != "fenster": continue
        scheibe(s, "Fenster", wand, a0, a1, z0, z1, -T / 2, weg_von=F.MITTE)
        scheibe(s, "Bleinetz", wand, a0, a1, z0, z1, -T / 2 + 0.006, weg_von=F.MITTE)
    return s


def baue_glas():
    g = Teil("Glas", BAENDER_INNEN, saat=7)
    for wand, (a0, a1), (z0, z1), art in F.FENSTER:
        if art == "fenster": scheibe(g, "Innenglas", wand, a0, a1, z0, z1, -T / 2 - 0.004, zu=F.RAUM + Vector((0, 0, z0 - 1.0)))
    return g


def baue_wappen():
    """Wappenschild des Besitzers auf freier Wandflaeche zur Gasse (wie in der Probe: Rahmen Holz, Schild „Hausfarbe“, Eisenhaken)."""
    w = Teil("Wappen", BAENDER_AUSSEN, saat=61)
    g = 0.62
    o, eu, n = F.WAPPEN
    g1.prisma(w, "Fachwerk", [(x * 1.16 * g, y * 1.12 * g) for x, y in g1.SCHILD], o, eu, (0, 0, 1), n, 0.0, 0.045, "balken_c")
    g1.prisma(w, "Hausfarbe", [(x * g, y * g) for x, y in g1.SCHILD], o, eu, (0, 0, 1), n, 0.045, 0.075, "farbholz", farbe=(1.0, 1.0, 1.0, 1.0))
    o, n = Vector(o), Vector(n)
    a, b = o + EZ * 0.47 * g - Vector(eu) * 0.02, o + EZ * 0.58 * g + Vector(eu) * 0.02 + n * 0.05
    w.kasten("Bruchstein", (min(a.x, b.x), min(a.y, b.y), a.z), (max(a.x, b.x), max(a.y, b.y), b.z), "eisen")
    return w


def baue_leerstand():
    l = Teil("Leerstand", BAENDER_AUSSEN, saat=71)
    for wand, (a0, a1), (z0, z1), art in F.FENSTER:
        (ox, oy), (ex, ey), _, n = F.WAENDE[wand]
        start = ox if ex else oy
        s0, s1 = a0 - start, a1 - start
        n3 = (n[0], n[1], 0)
        if art == "tuer":
            for z in (z0 + 0.55, z0 + 1.05, z0 + 1.5):
                l.brett("Fachwerk", punkt(wand, s0 - 0.16, z - 0.06), punkt(wand, s1 + 0.16, z + 0.07), 0.17, n3, "planken", tiefe=0.10)
        else:
            l.brett("Fachwerk", punkt(wand, s0 - 0.1, z0 + 0.05), punkt(wand, s1 + 0.1, z1 - 0.08), 0.15, n3, "planken", tiefe=0.11)
            l.brett("Fachwerk", punkt(wand, s0 - 0.1, z1 - 0.02), punkt(wand, s1 + 0.1, z0 + 0.12), 0.15, n3, "planken", tiefe=0.135)
    return l


# ── Teile, die in der Lage des Spiels gebaut werden (nach dem Drehen): Fahne, Geruest, Massfiguren ───────────────────

def baue_fahne(x, y, z):
    """Fahne des Besitzers auf dem First – Aufbau wie in der Probe (Stadtansicht.Fahnen.cs erwartet ihn so): das Tuch zeigt nach +x."""
    f = Teil("Fahne", BAENDER_AUSSEN, saat=67)
    hoehe = 2.3
    f.stab("Holz", (x, y, z), (x, y, z + hoehe), 0.08, "balken_a")
    f.kasten("Eisen", (x - 0.07, y - 0.07, z + hoehe - 0.02), (x + 0.07, y + 0.07, z + hoehe + 0.12), "eisen")
    tuch = [(0, 0), (1.25, 0.05), (0.98, 0.38), (1.25, 0.72), (0, 0.72)]
    o = Vector((x + 0.04, y, z + hoehe - 0.85))
    for t, sicht in ((-0.015, -1), (0.015, 1)):
        f.vieleck("Hausfarbe", [o + Vector((u, t, v)) for u, v in tuch], [(0.1, 0.8)] * 5, weg_von=o + Vector((0.6, -t, 0.36)))
    for (au, av), (bu, bv) in zip(tuch, tuch[1:] + tuch[:1]):
        f.vieleck("Hausfarbe", [o + Vector((au, -0.015, av)), o + Vector((bu, -0.015, bv)), o + Vector((bu, 0.015, bv)), o + Vector((au, 0.015, av))],
                  [(0.1, 0.8)] * 4, weg_von=o + Vector((0.6, 0, 0.36)))
    return f


def baue_geruest(y, x0, x1, ebenen, innen, aussen, leiter_x):
    """Baugeruest vor der Gassenseite (Knoten „Geruest“): Stangen, Laufebenen, Leiter – wie in der Probe, in der Breite des Hauses."""
    g = Teil("Geruest", BAENDER_AUSSEN, saat=73)
    yi, ya = y - 0.22, y - 1.05
    n = max(3, round((x1 - x0) / 2.6) + 1)
    xs = [x0 + (x1 - x0) * i / (n - 1) for i in range(n)]
    for x in xs:
        g.stab("Fachwerk", (x, yi, 0), (x, yi, innen), 0.09, "balken_a")
        g.stab("Fachwerk", (x, ya, 0), (x, ya, aussen), 0.09, "balken_a")
    for z in ebenen:
        for x in xs:
            g.stab("Fachwerk", (x, yi - 0.05, z - 0.07), (x, ya + 0.05, z - 0.07), 0.08, "balken_b")
        g.kasten("Fachwerk", (xs[0] - 0.15, ya + 0.08, z), (xs[-1] + 0.15, yi - 0.08, z + 0.05), "planken", laengs="x")
        g.stab("Fachwerk", (xs[0], ya, z + 0.95), (xs[-1], ya, z + 0.95), 0.06, "balken_a")
    g.stab("Fachwerk", (xs[0], ya, 0.1), (xs[1], ya, ebenen[0] - 0.05), 0.07, "balken_c")
    if len(ebenen) > 1: g.stab("Fachwerk", (xs[-2], ya, ebenen[0] + 0.1), (xs[-1], ya, ebenen[1] - 0.05), 0.07, "balken_c")
    fuss, kopf = Vector((leiter_x, ya - 0.55, 0)), Vector((leiter_x, ya + 0.02, ebenen[0] + 0.6))
    for dx in (-0.2, 0.2):
        g.stab("Fachwerk", fuss + Vector((dx, 0, 0)), kopf + Vector((dx, 0, 0)), 0.05, "balken_a")
    for i in range(7):
        p = fuss.lerp(kopf, (i + 0.7) / 7.6)
        g.stab("Fachwerk", p + Vector((-0.2, 0, 0)), p + Vector((0.2, 0, 0)), 0.04, "balken_b")
    return g


def baue_massfiguren(stehend, sitzend):
    """Massfiguren von 1,75 m nur fuers Kontrollbild: `stehend` Fusspunkte, `sitzend` (Fusspunkt, Blick, Sitzhoehe)."""
    f = Teil("Massfigur", BAENDER_INNEN, saat=99)
    for p in stehend:
        x, y, z = p
        f.kasten("Massfigur", (x - 0.15, y - 0.15, z), (x + 0.15, y + 0.15, z + 0.88), "balken")
        f.kasten("Massfigur", (x - 0.2, y - 0.2, z + 0.88), (x + 0.2, y + 0.2, z + 1.46), "balken")
        f.dreh("Massfigur", (x, y, z + 1.49), [(0.0, 0.0), (0.1, 0.05), (0.11, 0.14), (0.08, 0.23), (0.0, 0.26)], "balken", seiten=8, deckel=False)
    for p, blick, sitz in sitzend:
        x, y, z = p.x, p.y, p.z + sitz
        bx, by = blick.x, blick.y
        f.kasten("Massfigur", (x - 0.18, y - 0.18, z), (x + 0.18, y + 0.18, z + 0.60), "balken")
        f.kasten("Massfigur", (min(x, x + bx * 0.42) - 0.17 * abs(by), min(y, y + by * 0.42) - 0.17 * abs(bx), z - 0.14), (max(x, x + bx * 0.42) + 0.17 * abs(by), max(y, y + by * 0.42) + 0.17 * abs(bx), z), "balken")
        f.dreh("Massfigur", (x, y, z + 0.63), [(0.0, 0.0), (0.1, 0.05), (0.11, 0.14), (0.08, 0.23), (0.0, 0.26)], "balken", seiten=8, deckel=False)
    return f


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Innen: anderthalb Geschosse (Grundform 2 und 4)
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def baue_raum():
    r = Teil("Raum", BAENDER_INNEN, saat=31 + F.nr)
    S = "Innen"
    IX, IV, IH, RAUM = F.IX, F.IV, F.IH, F.RAUM
    xk, zk = F.XK, F.ZK
    zw = dach_z(IX)                                    # die Traufwaende reichen innen bis unter das Dach
    for wand, ((ax, ay), (ex, ey), breite, (nx, ny)) in F.WAENDE.items():
        gb = wand.endswith("_gb")
        versatz = 0.0 if gb else T
        o = (ax + ex * versatz - nx * T, ay + ey * versatz - ny * T, 0)
        l = [(a - versatz, b - versatz, c, d) for a, b, c, d, _ in loecher(wand)]
        seite = "links" in wand or "rechts" in wand
        z0, z1 = (Z_BODEN, Z_OG) if wand.endswith("_eg") else (Z_TRAUFE, zk) if gb else (Z_OG, zw if seite else Z_TRAUFE)
        r.wand(S, o, (ex, ey, 0), breite - 2 * versatz, z0, z1, "lehmwand", loecher=l, zbasis=Z_BODEN if z0 < Z_OG else Z_OG, hoehe=2.4, zu=RAUM + Vector((0, 0, 0 if z0 < Z_OG else 2.2)))
    # Giebel innen neben und ueber dem Mittelfeld
    for y in (IV, IH):
        hin = RAUM + Vector((0, 0, 3))
        for s in (-1, 1):
            r.welt(S, [(s * IX, y, Z_TRAUFE), (s * xk, y, Z_TRAUFE), (s * xk, y, zk), (s * IX, y, zw)], "lehmwand", (-IX, y, 0), (1, 0, 0), Z_OG, 2.4, zu=hin)
        r.welt(S, [(-xk, y, zk), (xk, y, zk), (0, y, F.Z_FIRST)], "lehmwand", (-IX, y, 0), (1, 0, 0), zk, 2.4, zu=hin)
    # Boden: gestampfter Lehm, zwei Binsenmatten
    r.rechteck(S, (-IX, IV, Z_BODEN), (1, 0, 0), (0, 1, 0), 2 * IX, IH - IV, "lehmboden", zu=(0, 0, 99), u0=0.0)
    for (x0, y0, lx, ly) in F.MATTEN:
        r.rechteck(S, (x0, y0, Z_BODEN + 0.012), (1, 0, 0), (0, 1, 0), lx, ly, "stroh", zu=(0, 0, 99))
    for wand, (a0, a1), (z0, z1), art in F.FENSTER:
        (ox, oy), (ex, ey), _, (nx, ny) = F.WAENDE[wand]
        start = ox if ex else oy
        s0, s1 = a0 - start, a1 - start
        ni = (-nx, -ny, 0); e3 = (ex, ey, 0)
        P = lambda s, z, raus: punkt(wand, s, z, raus)
        if art == "fenster":
            # Fenster von innen: Bank, Sturz und Rahmen auf der Innenwand (der Fensterstock der Huelle ist von innen derselbe)
            for a, b, breite in ((P(s0 - 0.10, z0 - 0.04, -T), P(s1 + 0.10, z0 - 0.04, -T), 0.08), (P(s0 - 0.10, z1 + 0.04, -T), P(s1 + 0.10, z1 + 0.04, -T), 0.08),
                                 (P(s0 - 0.05, z0, -T), P(s0 - 0.05, z1, -T), 0.10), (P(s1 + 0.05, z0, -T), P(s1 + 0.05, z1, -T), 0.10)):
                r.brett(S, a, b, breite, ni, "balken", tiefe=0.035)
            continue
        # Haustuer von innen, genau in der Oeffnung der Huelle: Blatt aus sechs Brettern, Querriegel und Strebe, Langbaender,
        # Riegel mit Griff, Rahmen auf der Innenwand (wie in der Probe)
        for i in range(3):
            r.rechteck(S, P(s0 + i * (s1 - s0) / 3, z0, -0.10), e3, EZ, (s1 - s0) / 3, z1 - z0, "bohlen", laengs="v", zu=RAUM)
        for z in (z0 + 0.35, z1 - 0.35):
            r.brett(S, P(s0 + 0.04, z, -0.10), P(s1 - 0.04, z, -0.10), 0.14, ni, "balken", tiefe=0.035)
        r.brett(S, P(s0 + 0.12, z0 + 0.44, -0.10), P(s1 - 0.12, z1 - 0.44, -0.10), 0.12, ni, "balken", tiefe=0.03)
        ue = feld("eisen")
        for z in (z0 + 0.35, z1 - 0.35):
            r.rechteck(S, P(s0 + 0.01, z - 0.03, -0.14), e3, EZ, 0.5, 0.06, "kleinteile", u_bereich=ue, zu=RAUM)
        ab = len(r.flaechen)
        wandkasten(r, S, wand, s1 - 0.30, s1 - 0.02, -0.10, -0.15, z0 + 1.02, z0 + 1.08, "kleinteile")           # Riegel
        faerbe(r, ab, "eisen")
        r.stab(S, P(s1 - 0.2, z0 + 1.05, -0.15), P(s1 - 0.2, z0 + 1.05, -0.22), 0.03, "kleinteile", u_bereich=ue, kappen=True)   # Griff
        for s in (s0 - 0.06, s1 + 0.06):
            r.brett(S, P(s, z0, -T), P(s, z1 + 0.12, -T), 0.12, ni, "balken", tiefe=0.04)
        r.brett(S, P(s0 - 0.12, z1 + 0.06, -T), P(s1 + 0.12, z1 + 0.06, -T), 0.12, ni, "balken", tiefe=0.05)
    # Sichtbares Fachwerk innen: Eckstaender, Staender an den Traufwaenden (nicht in Oeffnungen, nicht am Herd), Raehm
    bw = 0.16
    kx0, kx1, ky0, ky1 = F.KAMIN
    for sx in (-1, 1):
        wand = "links_eg" if sx < 0 else "rechts_eg"
        offen = [(a0, a1) for w, (a0, a1), _, _ in F.FENSTER if w == wand]
        for y in (IV, IH):
            sy = 1 if y == IV else -1
            r.kasten(S, (min(sx * IX, sx * (IX - bw)), min(y, y + sy * bw), Z_BODEN), (max(sx * IX, sx * (IX - bw)), max(y, y + sy * bw), Z_OG), "balken", ohne=("-z", "+z"), laengs="z")
        for y in [F.VORN + s for s in g1.raster(F.HINTEN - F.VORN)[0]]:   # auf den Achsen des Fachwerks aussen
            if not IV + 0.2 < y < IH - 0.2: continue
            if any(a0 - 0.3 < y < a1 + 0.3 for a0, a1 in offen): continue
            if abs(sx * IX - (kx0 + kx1) / 2) < 0.9 and ky0 - 0.5 < y < ky1 + 0.5: continue
            r.kasten(S, (min(sx * IX, sx * (IX - 0.07)), y - bw / 2, Z_BODEN), (max(sx * IX, sx * (IX - 0.07)), y + bw / 2, Z_OG), "balken", ohne=("-z", "+z", "+x" if sx > 0 else "-x"), laengs="z")
            r.kasten(S, (min(sx * IX, sx * (IX - 0.06)), y - bw / 2, Z_OG), (max(sx * IX, sx * (IX - 0.06)), y + bw / 2, zw - 0.05), "balken", ohne=("-z", "+z", "+x" if sx > 0 else "-x"), laengs="z")
        # zwischen den Raehmen vorn und hinten (nicht unter ihnen: sonst decken sich die Unterseiten an den Ecken)
        r.kasten(S, (min(sx * IX, sx * (IX - 0.09)), IV + 0.09, Z_OG - 0.18), (max(sx * IX, sx * (IX - 0.09)), IH - 0.09, Z_OG), "balken", ohne=("+z", "+x" if sx > 0 else "-x", "-y", "+y"), laengs="y")
        for y in (IV + 0.08, IH - 0.08):                # Eckstaender des Kniestocks
            r.kasten(S, (min(sx * IX, sx * (IX - 0.06)), y - bw / 2, Z_OG), (max(sx * IX, sx * (IX - 0.06)), y + bw / 2, zw - 0.05), "balken", ohne=("-z", "+z", "+x" if sx > 0 else "-x"), laengs="z")
    for y, n in ((IV, 1), (IH, -1)):
        r.kasten(S, (-IX, min(y, y + n * 0.09), Z_OG - 0.18), (IX, max(y, y + n * 0.09), Z_OG), "balken", ohne=("+z", "-x", "+x", "-y" if n > 0 else "+y"), laengs="x")
    # Schlafgalerie: Bohlen auf drei Deckenbalken, Brustholz mit Luecke fuer den Austritt der Leiter
    GA, LX = F.GALERIE_AB, F.LEITER_X
    r.kasten(S, (-IX, GA, Z_OG), (IX, IH, Z_GALERIE), "bohlen", ohne=("+y", "-x", "+x"), laengs="x")
    for y in (GA + 0.09, (GA + IH) / 2, IH - 0.2):
        r.kasten(S, (-IX, y - 0.09, Z_OG - 0.2), (IX, y + 0.09, Z_OG), "balken", ohne=("+z", "-x", "+x"), laengs="x")
    pfosten = [-IX + 0.05, LX - 0.35, LX + 0.35, IX - 0.05]
    for xa, xb in ((-IX, LX - 0.35), (LX + 0.35, IX)):
        r.stab(S, (xa, GA + 0.06, Z_GALERIE + 0.85), (xb, GA + 0.06, Z_GALERIE + 0.85), 0.08, "balken")
        if xb - xa > 1.7: pfosten.append((xa + xb) / 2)
    for x in pfosten:
        r.stab(S, (x, GA + 0.06, Z_GALERIE), (x, GA + 0.06, Z_GALERIE + 0.85), 0.09, "balken")      # 5 mm vor dem Brustholz
    # Leiter zur Galerie wie in der Probe: 73 Grad, 0,48 m breit, neun Sprossen im Abstand von 26,5 cm, Holme 0,95 m ueber den Galerieboden
    fuss = Vector((LX, GA - 0.03 - LEITER_LAUF, Z_BODEN))
    kante = Vector((LX, GA - 0.03, Z_GALERIE))
    kopf = fuss + (kante - fuss) * ((Z_GALERIE + 0.95 - Z_BODEN) / (Z_GALERIE - Z_BODEN))
    for dx in (-0.24, 0.24):
        r.stab(S, fuss + Vector((dx, 0, 0)), kopf + Vector((dx, 0, 0)), 0.06, "balken", breite=0.09, kappen=True)
    for i in range(1, 10):
        p = fuss + (kante - fuss) * (0.265 * i / (Z_GALERIE - Z_BODEN))
        r.stab(S, p + Vector((-0.24, 0, 0)), p + Vector((0.24, 0, 0)), 0.045, "bohlen")
    # Dachstuhl: Sparrenpaare, Firstpfette, zwei Kehlbalken
    n_sp = max(4, round((IH - IV) / 1.15) + 1)
    sparren = [IV + 0.3 + (IH - IV - 0.6) * i / (n_sp - 1) for i in range(n_sp)]
    for y in sparren:
        for sx in (-1, 1):
            r.stab(S, (sx * IX, y, zw - 0.07), (0, y, F.Z_FIRST - 0.07), 0.12, "balken", breite=0.10 if sx < 0 else 0.11)   # am First nicht deckungsgleich
    r.stab(S, (0, IV, F.Z_FIRST - 0.12), (0, IH, F.Z_FIRST - 0.12), 0.14, "balken")
    for y in (sparren[1], sparren[-2]):
        r.stab(S, (-xk + 0.1, y, zk), (xk - 0.1, y, zk), 0.12, "balken")
    # Schlot: Bruchstein von der Herdhaube bis durchs Dach (gleiche Lage wie der Schornstein aussen)
    ohne = ["-z", "+z"] + (["+x"] if kx1 > IX - 0.01 else []) + (["-x"] if kx0 < -IX + 0.01 else []) + (["-y"] if ky0 < IV + 0.01 else []) + (["+y"] if ky1 > IH - 0.01 else [])
    z = Z_HAUBE + 0.2
    zo = (F.Z_FIRST if kx0 < 0 < kx1 else dach_z(min(abs(kx0), abs(kx1)))) + 0.05
    while z < zo - 1e-3:
        zn = min(z + 0.6, zo)
        r.kasten(S, (kx0, ky0, z), (kx1, ky1, zn), "bruchstein", ohne=tuple(ohne), laengs="y")
        z = zn
    return r


def baue_hausrat(kruege, besen):
    """Hausrat-Set wie in der Probe: zwei Tonkruege in einer Ecke, Reisigbesen an der Wand (`besen`: Fusspunkt und Wandrichtung)."""
    c = Teil("Hausrat_Set", BAENDER_INNEN, saat=57)
    S = "Innen"
    (ax, ay), (bx, by) = kruege
    c.dreh(S, (ax, ay, Z_BODEN), [(0.09, 0.0), (0.14, 0.06), (0.17, 0.17), (0.16, 0.28), (0.09, 0.39), (0.08, 0.44), (0.097, 0.48)], "kleinteile", seiten=10, deckel=False, u_bereich=feld("ton"))
    c.dreh(S, (bx, by, Z_BODEN), [(0.07, 0.0), (0.105, 0.06), (0.115, 0.15), (0.07, 0.25), (0.062, 0.28), (0.074, 0.31)], "kleinteile", seiten=9, deckel=False, u_bereich=feld("ton"))
    (fx, fy), (wx, wy) = besen
    c.stab(S, (fx, fy, Z_BODEN + 0.3), (fx - 0.05 + wx * 0.19, fy + wy * 0.19, Z_BODEN + 1.55), 0.03, "balken")
    c.dreh(S, (fx - wx * 0.03, fy - wy * 0.03, Z_BODEN), [(0.13, 0.0), (0.09, 0.2), (0.03, 0.34)], "stroh", seiten=6, deckel=False, u_bereich=(0.0, 0.3))
    return c


def teile_auf(teil, zuordnung):
    """Zerlegt ein Teil nach dem Schwerpunkt seiner Flaechen: zuordnung(Schwerpunkt) -> Schluessel. Liefert {Schluessel: Teil}."""
    stuecke = {}
    for fl in teil.flaechen:
        mitte = sum(fl[0], Vector()) / len(fl[0])
        k = zuordnung(mitte)
        if k not in stuecke: stuecke[k] = Teil(teil.name, teil.baender)
        stuecke[k].flaechen.append(fl)
    return stuecke


def moebel_der_probe():
    """Die Einrichtung der Probe in deren Massen, als Gruppen je Wand: (Teile, Anker). Wird danach je Grundform versetzt."""
    tisch = [g1.baue_tisch(), g1.baue_familienbuch(), g1.baue_bauplan(), g1.baue_werkzeug(), g1.baue_geldkassette()]
    t_anker = [
        Anker("Platz_Oberhaupt", (g1.BANK_X, g1.TISCH[2] + 0.52, Z_BODEN), "west", haltung="sitzt", rolle="oberhaupt", sitzhoehe=g1.SITZ_HOEHE),
        Anker("Platz_Kind_1", (g1.BANK_X, g1.TISCH[2] + 0.90, Z_BODEN), "west", haltung="sitzt", rolle="kind", sitzhoehe=g1.SITZ_HOEHE),
        Anker("Platz_Kind_2", (g1.SCHEMEL[0], g1.SCHEMEL[1], Z_BODEN), "sued", haltung="sitzt", rolle="kind", sitzhoehe=g1.SITZ_HOEHE),
    ]
    x0, x1, y0, y1 = g1.KAMIN
    herd = [g1.baue_herd(), g1.baue_vorrat()]
    h_anker = [
        Anker("Licht_Herd", ((x0 + x1) / 2 - 0.35, (y0 + y1) / 2, 0.95)),
        Anker("Feuer_Herd", ((x0 + x1) / 2 - 0.18, (y0 + y1) / 2, 0.53)),
        Anker("Platz_Partner", (1.3, y0 - 0.3 - 0.42, Z_BODEN), "nord", haltung="ruehrt", rolle="partner"),
        Anker("Platz_Kind_3", (g1.HERDSCHEMEL[0], g1.HERDSCHEMEL[1], Z_BODEN), "ost", haltung="sitzt", rolle="kind", sitzhoehe=g1.SITZ_HOEHE),
    ]
    waechter = teile_auf(g1.baue_hauswaechter(), lambda m: "schemel" if m.x > -0.6 else "laterne" if math.hypot(m.x + 1.66, m.y - (G1_IV + 0.11)) < 0.12 else "hellebarde")
    return dict(tisch=(tisch, t_anker), herd=(herd, h_anker), truhe=g1.baue_truhe(), apotheke=g1.baue_hausapotheke(), waechter=waechter,
                hauswappen=g1.baue_hauswappen(), schlafplatz=g1.baue_schlafplatz())


def zusammen(name, stuecke, baender=BAENDER_INNEN):
    t = Teil(name, baender)
    for s in stuecke: t.dazu(s)
    return t


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Grundform 3: das Haus der Probe mit Schuppen
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

# Rueckwand 1 cm hinter der Flucht des Hauses: in der Flucht laege sie in einer Ebene mit dem Eckstaender des Obergeschosses
SCHUPPEN = dict(x1=g1.BX + 2.40, y0=-1.40, y1=g1.HINTEN + 0.01, z_oben=3.30, z_aussen=2.15)


def schuppen_z(x):
    """Unterseite des Pultdachs ueber der Stelle x (am Obergeschoss der Probe angelehnt)."""
    s = SCHUPPEN
    return s["z_oben"] - (x - g1.OX) * (s["z_oben"] - s["z_aussen"]) / (s["x1"] - g1.OX)


def baue_schuppen(h):
    """Bretterschuppen an der Seitenwand: Eckpfosten, stehende Bretter, Brettertuer mit Strebe zur Gasse, Pultdach aus Brettern
    mit Deckleisten, an das Obergeschoss gelehnt."""
    holz, stein = "Fachwerk", "Bruchstein"
    s = SCHUPPEN
    x0, x1, y0, y1 = g1.BX, s["x1"], s["y0"], s["y1"]
    M = Vector(((x0 + x1) / 2, (y0 + y1) / 2, 1.0))
    oben = lambda p: min(schuppen_z(p.x), g1.Z_OG if p.x < g1.OX - 1e-4 else 99) - 0.02
    knick = ((g1.OX - x0) / (x1 - x0),)
    bretterwand(h, holz, (x0, y0, 0), (x1, y0, 0), 0.0, oben, "planken", kanten=knick, weg_von=M)
    bretterwand(h, holz, (x0, y1, 0), (x1, y1, 0), 0.0, oben, "planken", kanten=knick, weg_von=M)
    bretterwand(h, holz, (x1, y0, 0), (x1, y1, 0), 0.0, oben, "planken", weg_von=M)
    for y in (y0, y1):
        h.kasten(holz, (x1 - 0.08, y - 0.08, 0.0), (x1 + 0.08, y + 0.08, s["z_aussen"]), "balken_a", ohne=("-z", "+z"), laengs="z")
    h.stab(holz, (x1, y0 - 0.12, s["z_aussen"] - 0.07), (x1, y1 + 0.12, s["z_aussen"] - 0.07), 0.14, "balken_c", kappen=True)
    # Brettertuer zur Gasse: 0,90 x 1,95 m licht (ab 4 cm ueber dem Boden), 3 cm vor der Wand, mit Rahmen, zwei Riegeln, Strebe und Eisenriegel
    tx0, tx1, tz = x0 + 0.62, x0 + 1.52, 1.99
    for i in range(2):
        h.rechteck(holz, (tx0 + i * 0.45, y0 - 0.03, 0.04), (1, 0, 0), (0, 0, 1), 0.45, tz - 0.04, "planken", laengs="v", weg_von=M)
    n = (0, -1, 0)
    for x in (tx0 - 0.06, tx1 + 0.06):
        h.brett(holz, (x, y0, 0), (x, y0, tz + 0.12), 0.12, n, "balken_c", tiefe=0.06, laengs="v")
    h.brett(holz, (tx0 - 0.12, y0, tz + 0.06), (tx1 + 0.12, y0, tz + 0.06), 0.12, n, "balken_c", tiefe=0.065)   # Sturz 5 mm vor den Pfosten
    for z in (0.35, tz - 0.35):
        h.brett(holz, (tx0 + 0.03, y0 - 0.03, z), (tx1 - 0.03, y0 - 0.03, z), 0.12, n, "balken_b", tiefe=0.03)
    h.brett(holz, (tx0 + 0.08, y0 - 0.03, 0.43), (tx1 - 0.08, y0 - 0.03, tz - 0.43), 0.11, n, "balken_b", tiefe=0.025)
    h.rechteck(stein, (tx1 - 0.3, y0 - 0.065, 1.0), (1, 0, 0), (0, 0, 1), 0.34, 0.06, "eisen", weg_von=M)
    # Pultdach: Bretter in Fallrichtung (Bahnen von 0,46 m), Unterseite, Stirnkanten, fuenf Deckleisten
    ya, yb = y0 - 0.22, y1 + 0.12
    xa, xb = g1.OX, x1 + 0.30
    fall = Vector((xb - xa, 0, schuppen_z(xb) - schuppen_z(xa)))
    lang = fall.length; fall.normalize()
    n_bahn = round((yb - ya) / 0.46)
    breite = (yb - ya) / n_bahn
    for i in range(n_bahn):
        o = Vector((xa, ya + i * breite, schuppen_z(xa) + 0.05))
        h.rechteck(holz, o, fall, (0, 1, 0), lang, breite, "planken", zu=o + Vector((0, 0, 9)))
    h.rechteck(holz, (xa, ya, schuppen_z(xa)), fall, (0, 1, 0), lang, yb - ya, "balken_c", zu=(xa, 0, -9))
    for y, aussen in ((ya, -1), (yb, 1)):
        h.rechteck(holz, (xa, y, schuppen_z(xa)), fall, (0, 0, 1), lang, 0.05, "balken_c", weg_von=(xa, y - aussen, 2), anteil=(0.0, 0.3))
    h.rechteck(holz, (xb, ya, schuppen_z(xb)), (0, 1, 0), (0, 0, 1), yb - ya, 0.05, "balken_c", weg_von=M, anteil=(0.0, 0.3))
    for i in range(1, n_bahn, 2):
        y = ya + i * breite
        h.stab(holz, Vector((xa, y, schuppen_z(xa) + 0.07)), Vector((xb, y, schuppen_z(xb) + 0.07)), 0.03, "balken_b", breite=0.07)
    for y in (y0 + 0.5, (y0 + y1) / 2, y1 - 0.5):                                   # Sparren, an der Stirn zu sehen
        h.stab(holz, Vector((xa, y, schuppen_z(xa) - 0.06)), Vector((xb - 0.04, y, schuppen_z(xb - 0.04) - 0.06)), 0.12, "balken_a", breite=0.09)
    # Windbrett an Vorder- und Rueckwand unter dem Pultdach: zwischen Wandkrone und Dach blieb ein Spalt von 2 cm, durch den man in den
    # Schuppen sah (luecken_pruefung „Durchblick“; Mensch-Nachtrag 16.11.2026, wie bei den Zwergen)
    for y, aussen in ((y0, -1), (y1, 1)):
        a = Vector((g1.OX, y + aussen * 0.002, schuppen_z(g1.OX) - 0.04))
        b = Vector((x1, y + aussen * 0.002, schuppen_z(x1) - 0.04))
        h.brett(holz, a, b, 0.11, (0, aussen, 0), "balken_c", tiefe=0.03)


def scheitholz_links(h):
    """Das Scheitholz der Probe lag an der rechten Seitenwand; dort steht jetzt der Schuppen – es liegt an der linken."""
    h.flaechen = [fl for fl in h.flaechen if not (min(p.x for p in fl[0]) > g1.BX + 0.02 and max(p.z for p in fl[0]) < 0.6 and 0.3 < sum(p.y for p in fl[0]) / len(fl[0]) < 1.6)]
    for i, (y, z) in enumerate([(0.75 + 0.18 * k, 0.09) for k in range(5)] + [(0.84 + 0.18 * k, 0.26) for k in range(4)] + [(0.93 + 0.18 * k, 0.43) for k in range(3)]):   # 1 cm Fuge
        h.stab("Fachwerk", (-g1.BX - 0.03, y, z), (-g1.BX - 0.62, y + 0.008 * (i % 3 - 1), z), 0.17, "balken_a", kappen=False)   # schraeg, ohne sich zu durchdringen
        h.rechteck("Fachwerk", (-g1.BX - 0.62, y - 0.085, z - 0.085), (0, 1, 0), (0, 0, 1), 0.17, 0.17, "farbholz", weg_von=(0, y, z), anteil=(0.1, 0.9), u_bereich=(0.1 + 0.07 * i, 0.14 + 0.07 * i))


def anker_der_probe():
    """Die Anker der Probe (wohnhaus_mensch_s1_g1.main) als versetzbare Liste: aussen, innen."""
    tuer = next(f for f in g1.FENSTER if f[3] == "tuer")
    x0, x1, y0, y1 = g1.KAMIN
    tm = (tuer[1][0] + tuer[1][1]) / 2
    aussen = [Anker("Anker_Tuer", (tm, g1.VORN - 0.2, Z_BODEN + 0.02)), Anker("Anker_Schornstein", ((x0 + x1) / 2, (y0 + y1) / 2, g1.Z_FIRST - 0.15))]
    innen = [
        Anker("Blick_Auge", (0.3, g1.IH - 0.3, Z_BODEN + 1.60)), Anker("Blick_Ziel", (0.3, g1.IV, Z_BODEN + 1.50)),
        Anker("Licht_Herd", ((x0 + x1) / 2 - 0.35, (y0 + y1) / 2, 0.95)), Anker("Licht_Raum", (-0.3, -1.2, 3.6)),
        Anker("Feuer_Herd", ((x0 + x1) / 2 - 0.18, (y0 + y1) / 2, 0.53)),
        Anker("Tuer_Innen", (tm, g1.IV, Z_BODEN + TUER_HOCH / 2)),
        Anker("Platz_Oberhaupt", (g1.BANK_X, g1.TISCH[2] + 0.52, Z_BODEN), "west", haltung="sitzt", rolle="oberhaupt", sitzhoehe=g1.SITZ_HOEHE),
        Anker("Platz_Partner", (1.3, y0 - 0.3 - 0.42, Z_BODEN), "nord", haltung="ruehrt", rolle="partner"),
        Anker("Platz_Kind_1", (g1.BANK_X, g1.TISCH[2] + 0.90, Z_BODEN), "west", haltung="sitzt", rolle="kind", sitzhoehe=g1.SITZ_HOEHE),
        Anker("Platz_Kind_2", (g1.SCHEMEL[0], g1.SCHEMEL[1], Z_BODEN), "sued", haltung="sitzt", rolle="kind", sitzhoehe=g1.SITZ_HOEHE),
        Anker("Platz_Kind_3", (g1.HERDSCHEMEL[0], g1.HERDSCHEMEL[1], Z_BODEN), "ost", haltung="sitzt", rolle="kind", sitzhoehe=g1.SITZ_HOEHE),
        Anker("Platz_Kind_4", (0.2, 1.4, Z_BODEN), "nord", haltung="spielt", rolle="kind"),
    ]
    return aussen, innen


def baue_form3():
    haus = g1.baue_haus()
    scheitholz_links(haus)
    baue_schuppen(haus)
    teile = dict(
        huelle=[haus, g1.baue_dach()], scheiben=g1.baue_scheiben(), zusatz=[g1.baue_wappen(), g1.baue_leerstand(), g1.baue_geruest()],
        raum=[g1.baue_raum(), g1.baue_herd(), g1.baue_vorrat(), g1.baue_tisch(), g1.baue_schlafplatz(), g1.baue_hausrat()],
        gegenstaende=[g1.baue_truhe(), g1.baue_familienbuch(), g1.baue_bauplan(), g1.baue_werkzeug(), g1.baue_geldkassette()],
        einrichtung=[g1.baue_hausapotheke(), g1.baue_hauswaechter(), g1.baue_hauswappen()], glas=g1.baue_glas())
    aussen, innen = anker_der_probe()
    teile.update(anker_aussen=aussen, anker_innen=innen, fahne_ort=Anker("f", (0.0, g1.DACH_VORN + 0.55, g1.Z_FIRST + 0.25)),
                 steht=[Anker("m", (0.3, g1.GALERIE_AB - 0.3 - 1.2, Z_BODEN)), Anker("m", (g1.LEITER_X, g1.GALERIE_AB + 0.3, g1.Z_GALERIE)),
                        Anker("m", (g1.BX + 1.07, SCHUPPEN["y0"] - 0.6, 0.0))])
    return teile


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Grundform 2 und 4: Zusammenbau
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def baue_form24():
    IX, IV, IH = F.IX, F.IV, F.IH
    m = moebel_der_probe()
    eins = ((1, 0), (0, 1))
    if F.nr == 2:
        # Baurahmen: Strassenwand links (x = -IX), Herd am vorderen Giebel (y = IV), Galerie am hinteren (y ab 1,70)
        lage = dict(
            tisch=(eins, (-(IX - G1_IX), 2.05)),                                  # an der Strassenwand rechts neben der Tuer
            herd=(((0, -1), (-1, 0)), (-0.10, IV + G1_IX)),                       # von der rechten Wand an die Giebelwand, offen zum Raum
            truhe=(((0, 1), (-1, 0)), (-IX - G1_IV, -2.92)),                      # unter dem Fenster an der Strassenwand, Schloss zum Raum
            apotheke=(((0, -1), (1, 0)), (IX + G1_IV, -2.55)),                    # an der Rueckwand, in der Ecke neben dem Herd
            hellebarde=(eins, (-(IX - G1_IX), IV - G1_IV)),
            laterne=(((0, 1), (-1, 0)), (-IX - G1_IV, -1.79)),                    # an der Strassenwand zwischen Tuer und Tisch
            schemel=(eins, (IX - 0.27 - 0.14, -2.35 - (G1_IV + 0.27))),
            hauswappen=(eins, (1.15, IV - G1_JV), -0.40),                         # an der Giebelwand neben dem Schlot
            schlafplatz=(eins, (0.0, IH - G1_IH)),
        )
        m["herd"][0][1].flaechen = m["herd"][0][1].flaechen[:-18]                 # ohne die drei Scheite: dort steht die Truhe
        hausrat = baue_hausrat(((IX - 0.24, IV + 0.26), (IX - 0.62, IV + 0.22)), ((-1.0, IH - 0.24), (0, 1)))
        frei = [Anker("Blick_Auge", (IX - 0.50, -0.30, Z_BODEN + 1.60)), Anker("Blick_Ziel", (-IX, -1.15, Z_BODEN + 1.45)),
                Anker("Licht_Raum", (0.0, -0.8, 3.6)), Anker("Platz_Kind_4", (0.9, 2.3, Z_BODEN), "west", haltung="spielt", rolle="kind")]
    else:
        yo = IV - G1_IV
        lage = dict(
            tisch=(eins, (-(IX - G1_IX), yo)), herd=(eins, (IX - G1_IX, yo)), truhe=(eins, (-0.23, yo)), apotheke=(eins, (0.0, yo)),
            hellebarde=(eins, (-(IX - G1_IX), yo)), laterne=(eins, (-0.06, yo)), schemel=(eins, (0.0, yo)),
            hauswappen=(eins, (0.0, IV - G1_JV), -0.40), schlafplatz=(eins, (0.0, IH - G1_IH)),
        )
        hausrat = baue_hausrat(((IX - 0.24, IV + 0.26), (IX - 0.20, IV + 0.62)), ((-1.0, IH - 0.24), (0, 1)))
        frei = [Anker("Blick_Auge", (0.3, IH - 0.3, Z_BODEN + 1.60)), Anker("Blick_Ziel", (0.3, IV, Z_BODEN + 1.50)),
                Anker("Licht_Raum", (-0.3, -1.2 + yo, 3.4)), Anker("Platz_Kind_4", (0.2, 1.4 + yo, Z_BODEN), "nord", haltung="spielt", rolle="kind")]
    for name in ("tisch", "herd"):
        versetze_alle(m[name][0] + m[name][1], *lage[name])
    for name in ("truhe", "apotheke", "schlafplatz"):
        versetze(m[name], *lage[name])
    versetze(m["hauswappen"], lage["hauswappen"][0], lage["hauswappen"][1], lage["hauswappen"][2])
    for name in ("hellebarde", "laterne", "schemel"):
        versetze(m["waechter"][name], *lage[name])
    waechter = zusammen("Einrichtung_Hauswaechter", m["waechter"].values())

    tuer = next(f for f in F.FENSTER if f[3] == "tuer")
    (ox, oy), (ex, ey), _, (nx, ny) = F.WAENDE[tuer[0]]
    sm = (tuer[1][0] + tuer[1][1]) / 2 - (ox if ex else oy)
    kx0, kx1, ky0, ky1 = F.KAMIN
    haus = baue_haus()
    aussen = [Anker("Anker_Tuer", punkt(tuer[0], sm, Z_BODEN + 0.02, 0.2)), Anker("Anker_Schornstein", ((kx0 + kx1) / 2, (ky0 + ky1) / 2, F.Z_KAMIN + 0.2))]
    innen = frei + [Anker("Tuer_Innen", punkt(tuer[0], sm, Z_BODEN + TUER_HOCH / 2, -T))] + m["tisch"][1] + m["herd"][1]
    if F.FAHNE: fahne_ort = Anker("f", (F.FAHNE[0], F.FAHNE[1], F.Z_FIRST + 0.25))
    else: fahne_ort = Anker("f", (0.0, F.DACH_VORN + F.D_WALM + 0.45, F.Z_FIRST + 0.25))
    return dict(
        huelle=[haus, baue_dach()], scheiben=baue_scheiben(), zusatz=[baue_wappen(), baue_leerstand()],
        raum=[baue_raum(), m["herd"][0][0], m["herd"][0][1], m["tisch"][0][0], m["schlafplatz"], hausrat],
        gegenstaende=[m["truhe"]] + m["tisch"][0][1:], einrichtung=[m["apotheke"], waechter, m["hauswappen"]], glas=baue_glas(),
        anker_aussen=aussen, anker_innen=innen, fahne_ort=fahne_ort,
        steht=[Anker("m", punkt(tuer[0], sm, Z_BODEN, -T - 0.55)), Anker("m", (F.LEITER_X, F.GALERIE_AB - LEITER_LAUF - 0.42, Z_BODEN)),
               Anker("m", (F.LEITER_X, F.GALERIE_AB + 0.3, Z_GALERIE))])


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    form_setzen(int(opt.get("form", "0")))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    stoff = g1.stoff
    aussen_stoffe = {
        "Stroh": stoff("Stroh", "trim"), "Strohkante": stoff("Strohkante", "trim", schere=True), "Fachwerk": stoff("Fachwerk", "trim"),
        "Lehmputz": stoff("Lehmputz", "trim"), "Bruchstein": stoff("Bruchstein", "trim"),
        "Fenster": stoff("Fenster", farbe=(0.09, 0.11, 0.14), rauheit=0.2), "Bleinetz": stoff("Bleinetz", farbe=(0.1, 0.1, 0.11), alpha=0.5),
        "Hausfarbe": stoff("Hausfarbe", farbe=(0.55, 0.12, 0.10)), "Holz": stoff("Holz", farbe=(0.30, 0.22, 0.15)), "Eisen": stoff("Eisen", farbe=(0.12, 0.12, 0.13), rauheit=0.5),
    }
    innen_stoffe = {"Innen": stoff("Innen", "innen_trim"), "Innenglas": stoff("Innenglas", farbe=(0.55, 0.62, 0.6), alpha=0.16, rauheit=0.15),
                    "Hausfarbe": aussen_stoffe["Hausfarbe"]}

    t = baue_form3() if F.nr == 3 else baue_form24()
    # alles aus dem Baurahmen in die Lage im Spiel (Vorderseite bei -Y)
    m_welt, t_welt = F.WELT
    alle = t["huelle"] + [t["scheiben"]] + t["zusatz"] + t["raum"] + t["gegenstaende"] + t["einrichtung"] + [t["glas"]]
    versetze_alle(alle + t["anker_aussen"] + t["anker_innen"] + t["steht"] + [t["fahne_ort"]], m_welt, t_welt)
    g1.wappen_frei_ruecken(t["zusatz"][0], t["huelle"] + [t["scheiben"]])        # Regel wappen_ort: 5 cm frei (Feinschliff 17.11.2026)
    fahne = g1.fahne_verankern(baue_fahne(*t["fahne_ort"].ort), t["huelle"][1])     # Mast im Dach verankert (Mensch-Nachtrag 16.11.2026)
    if F.nr != 3: t["zusatz"].append(baue_geruest(**F.GERUEST))

    zaehle = lambda teile: {x.name: x.dreiecke() for x in teile}
    huelle = t["huelle"] + [t["scheiben"]]
    print("DREIECKE aussen:", zaehle(huelle), "Summe", sum(x.dreiecke() for x in huelle))
    print("DREIECKE Zustand und Besitz:", zaehle(t["zusatz"] + [fahne]))
    print("DREIECKE innen:", zaehle(t["raum"] + [t["glas"]]), "Summe", sum(x.dreiecke() for x in t["raum"] + [t["glas"]]))
    print("DREIECKE Einrichtung:", zaehle(t["einrichtung"]))
    print("DREIECKE Gegenstaende:", zaehle(t["gegenstaende"]))
    xs = [p.x for teil in t["huelle"] for fl in teil.flaechen for p in fl[0]]; ys_ = [p.y for teil in t["huelle"] for fl in teil.flaechen for p in fl[0]]; zs = [p.z for teil in t["huelle"] for fl in teil.flaechen for p in fl[0]]
    print("UMRISS Huelle: x %.2f..%.2f, y %.2f..%.2f, z bis %.2f" % (min(xs), max(xs), min(ys_), max(ys_), max(zs)))

    o_huelle = [x.objekt(aussen_stoffe) for x in t["huelle"]]
    o_zusatz = [x.objekt(aussen_stoffe) for x in t["zusatz"]]
    o_scheiben = t["scheiben"].objekt(aussen_stoffe, mit_farbe=False)
    o_fahne = fahne.objekt(aussen_stoffe, mit_farbe=False)
    o_raum = [x.objekt(innen_stoffe, mit_farbe=False) for x in t["raum"]]
    o_einrichtung = [x.objekt(innen_stoffe, mit_farbe=False) for x in t["einrichtung"] + t["gegenstaende"]]
    o_glas = t["glas"].objekt(innen_stoffe, mit_farbe=False)
    for o in o_zusatz[1:]:                              # Leerstand und Geruest: Zustandsknoten, im Spiel nach Bedarf sichtbar
        o["zustand"] = True; o["sichtbar"] = False
    aussen = o_huelle + [o_scheiben] + o_zusatz + [o_fahne]
    innen = o_raum + o_einrichtung + [o_glas]
    a_innen = {a.name: a for a in t["anker_innen"]}
    o_mass = None
    if "bilder" in opt:
        tuer_aussen = next(a for a in t["anker_aussen"] if a.name == "Anker_Tuer")
        sitz = a_innen["Platz_Oberhaupt"]
        figuren = baue_massfiguren([a.ort for a in t["steht"]] + [Vector((tuer_aussen.ort.x, tuer_aussen.ort.y - 0.75, 0.0))], [(sitz.ort, sitz.blick, sitz.extras["sitzhoehe"])])
        o_mass = figuren.objekt({"Massfigur": stoff("Massfigur", farbe=(0.85, 0.35, 0.1))}, mit_farbe=False)
        o_mass.hide_render = True

    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    boden = bpy.context.active_object; boden.name = "Kontrollboden"
    boden.data.materials.append(stoff("Kontrollboden", farbe=(0.33, 0.3, 0.24)))

    mit_ao = "ohne-ao" not in opt
    g1.uv2_und_ao([(o_huelle, [boden]), (o_zusatz, o_huelle + [boden])], "wohnhaus_mensch", F.name, mit_ao=mit_ao)
    g1.uv2_und_ao([(o_raum, []), (o_einrichtung, o_raum)], "wohnhaus_mensch_innen", F.name, mit_ao=mit_ao)

    g1.exportiere(aussen + [a.objekt() for a in t["anker_aussen"]], os.path.join(ZIEL_MODELL, f"{F.name}.glb"))
    g1.exportiere(innen + [a.objekt() for a in t["anker_innen"]], os.path.join(ZIEL_MODELL, f"{F.name}_innen.glb"))

    if "bilder" in opt:
        o = opt["bilder"]; os.makedirs(o, exist_ok=True)
        fest = [x.name for x in innen] + ["Leerstand", "Geruest"]
        vor = f"g{F.nr}_blender_"
        g1.kontrollbild(os.path.join(o, vor + "aussen_vorn.jpg"), (-8.5, -14.0, 8.0), (0.0, 0.3, 2.8), verbergen=fest)
        g1.kontrollbild(os.path.join(o, vor + "aussen_rechts.jpg"), (10.5, -11.5, 6.5), (0.0, 0.3, 2.6), verbergen=fest)
        g1.kontrollbild(os.path.join(o, vor + "aussen_hinten.jpg"), (7.5, 14.0, 8.0), (0.0, 0.3, 2.8), verbergen=fest)
        ta = next(a for a in t["anker_aussen"] if a.name == "Anker_Tuer").ort
        g1.kontrollbild(os.path.join(o, vor + "aussen_tuer.jpg"), (ta.x + 2.4, ta.y - 5.8, 2.2), (ta.x - 0.4, ta.y + 0.2, 1.5), brennweite=40, verbergen=fest)
        auge, ziel = a_innen["Blick_Auge"].ort, a_innen["Blick_Ziel"].ort
        lampe = bpy.data.objects.new("Kontrolllampe", bpy.data.lights.new("Kontrolllampe", "POINT"))
        lampe.data.energy = 260; lampe.location = a_innen["Licht_Raum"].ort - Vector((0, 0, 1.2))
        bpy.context.scene.collection.objects.link(lampe)
        huelle_weg = ["Dach", "Scheiben", "Wappen", "Fahne", "Leerstand", "Geruest"]
        g1.kontrollbild(os.path.join(o, vor + "innen_blick.jpg"), tuple(auge), tuple(ziel), brennweite=14, verbergen=huelle_weg)
        mitte = (auge + ziel) / 2
        g1.kontrollbild(os.path.join(o, vor + "innen_oben.jpg"), (mitte.x + 1.0, mitte.y - 1.6, 7.6), (mitte.x, mitte.y + 0.2, 1.0), brennweite=16, verbergen=huelle_weg)
        g1.kontrollbild(os.path.join(o, vor + "innen_gegen.jpg"), (ziel.x * 0.8 + auge.x * 0.2, ziel.y * 0.8 + auge.y * 0.2, Z_BODEN + 1.6), (auge.x, auge.y, Z_BODEN + 1.9), brennweite=14, verbergen=huelle_weg)


if __name__ == "__main__":
    main()
