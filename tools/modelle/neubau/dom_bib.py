"""Dom-Grundgeruest (Blender): gotische Bausteine fuer alle Dome des Neubaus, auf rathaus_bib.py und netzbau.py aufgebaut.

Spitzbogen- und Rundoeffnungen fuer `rathaus_bib.mauer`, Wandflaechen aussen und innen aus einer Massdatei (dom_masse.py: WAENDE,
OEFFNUNGEN), Scheiben, Gewaende und Masswerk, Strebepfeiler, Fialen, Kreuzgewoelbe und Spitztonnen, Schachbrettboden, Stoffe,
UV2/AO in die Atlanten "dom" und "dom_innen" (ao_atlas.json), Export. Die Skripte dom_<glaube>_aussen.py / _innen.py rufen
diese Funktionen mit ihrer Massdatei auf; ein weiterer Dom braucht nur eine eigene Massdatei und seine zwei Bauskripte.

Koordinaten: X rechts, Y nach hinten (Portal bei -Y), Z oben. Materialnamen wie beim Rathaus (Schiefer, Schieferkante, Bruchstein,
Sandstein, Bretter, Fachwerk, Fenster, Bleinetz; innen: Innen, Innenglas), damit Einkleiden, Brand und Nachtlicht im Spiel greifen.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import rathaus_bib as B                                              # noqa: E402
from rathaus_bib import EZ, Loch, mauer                              # noqa: E402
from dom_trimsheet import BAENDER_DOM, BAENDER_DOM_INNEN, FELDER_DOM, FELDER_DOM_INNEN, SCHIEFER_REIHEN   # noqa: E402

V = Vector


class Teil(B.Teil):
    """Teil eines Doms: beide Blaetter texeltreu (rathaus_bib.Teil teilt breite Flaechen in Streifen von Bandhoehe)."""

    def __init__(self, name, baender, saat=1):
        super().__init__(name, baender, saat)
        self.texeltreu = True
        self.innen = baender is BAENDER_DOM_INNEN


def feld(name, innen=False):
    """u-Bereich eines Farbfelds im Kleinteile-Band."""
    liste = FELDER_DOM_INNEN if innen else FELDER_DOM
    i = liste.index(name); n = len(liste)
    return (i / n + 0.01, (i + 1) / n - 0.01)


def farbflaeche(teil, stoff, punkte, name, **kw):
    """Flaeche in einem Farbfeld (kleiner Fleck aus der Mitte des Feldes: die Farbe ohne Streifen)."""
    ua, ub = feld(name, teil.innen)
    v0, v1, _ = teil.baender["kleinteile"]
    um, vm = (ua + ub) / 2, (v0 + v1) / 2
    uvs = [(um + 0.002 * (i % 2), vm + 0.002 * (i // 2 % 2)) for i in range(len(punkte))]
    teil.vieleck(stoff, punkte, uvs, **kw)


def farbkasten(teil, stoff, mn, mx, name, ohne=()):
    mn, mx = V(mn), V(mx); m = (mn + mx) / 2
    X, Y, Z = V((mx.x - mn.x, 0, 0)), V((0, mx.y - mn.y, 0)), V((0, 0, mx.z - mn.z))
    for seite, o, a, b in (("-x", mn, Y, Z), ("+x", mn + X, Y, Z), ("-y", mn, X, Z), ("+y", mn + Y, X, Z), ("-z", mn, X, Y), ("+z", mn + Z, X, Y)):
        if seite in ohne: continue
        farbflaeche(teil, stoff, [o, o + a, o + a + b, o + b], name, weg_von=m)


def farbkasten_innen(teil, mn, mx, name="nacht", stoff="Sandstein"):
    """Dunkler Hohlraum (Glockenstube): die Flaechen zeigen nach innen."""
    mn, mx = V(mn), V(mx); m = (mn + mx) / 2
    X, Y, Z = V((mx.x - mn.x, 0, 0)), V((0, mx.y - mn.y, 0)), V((0, 0, mx.z - mn.z))
    for o, a, b in ((mn, Y, Z), (mn + X, Y, Z), (mn, X, Z), (mn + Y, X, Z), (mn, X, Y), (mn + Z, X, Y)):
        farbflaeche(teil, stoff, [o, o + a, o + a + b, o + b], name, zu=m)


def farbstab(teil, stoff, a, b, dicke, name, breite=None):
    a, b = V(a), V(b)
    achse = (b - a).normalized()
    hilfe = EZ if abs(achse.z) < 0.9 else V((0, 1, 0))
    e1 = achse.cross(hilfe).normalized(); e2 = e1.cross(achse).normalized()
    w, d = (breite or dicke) / 2, dicke / 2
    m = (a + b) / 2
    ecken = [e1 * w + e2 * d, -e1 * w + e2 * d, -e1 * w - e2 * d, e1 * w - e2 * d]
    for i in range(4):
        c0, c1 = ecken[i], ecken[(i + 1) % 4]
        farbflaeche(teil, stoff, [a + c0, b + c0, b + c1, a + c1], name, weg_von=m)
    for p in (a, b):
        farbflaeche(teil, stoff, [p + c for c in ecken], name, weg_von=m)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Oeffnungen
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def spitz(a, hoehe):
    """Spitzbogen mit halber Breite a und Bogenhoehe `hoehe` (>= a): (Mittelpunktsversatz c, Radius R) der zwei Kreisboegen."""
    c = (hoehe * hoehe - a * a) / (2 * a)
    return c, a + c


def spitz_breite(a, hoehe, h):
    """Halbe Breite des Spitzbogens in der Hoehe h ueber dem Kaempfer."""
    c, r = spitz(a, hoehe)
    return max(math.sqrt(max(r * r - h * h, 0.0)) - c, 0.0)


class Spitzloch(Loch):
    """Spitzbogenoeffnung: Scheitel bei z1, Bogenhoehe = steil x halbe Breite (steil >= 1)."""

    def __init__(self, s0, s1, z0, z1, steil=1.25, teile=5, **kw):
        super().__init__(s0, s1, z0, z1, bogen=True, **kw)
        self.a = (s1 - s0) / 2
        self.h = self.a * steil
        self.zk = z1 - self.h
        self.teile = teile

    def breite_bei(self, z):
        if z < self.z0 - 1e-6 or z > self.z1 + 1e-6: return None
        if z <= self.zk: return (self.s0, self.s1)
        w = spitz_breite(self.a, self.h, z - self.zk)
        m = (self.s0 + self.s1) / 2
        return (m - w, m + w)

    def hoehen(self, teile=None):
        n = self.teile
        return {self.z0, self.z1, self.zk} | {self.zk + self.h * math.sin(math.pi / 2 * i / n) for i in range(n + 1)}

    def umriss(self):
        """(s, z) entlang des Bogens vom rechten Kaempfer ueber den Scheitel zum linken."""
        m = (self.s0 + self.s1) / 2
        zs = [self.zk + self.h * math.sin(math.pi / 2 * i / self.teile) for i in range(self.teile + 1)]
        rechts = [(m + spitz_breite(self.a, self.h, z - self.zk), z) for z in zs]
        return rechts + [(2 * m - s, z) for s, z in reversed(rechts[:-1])]


class Rundloch(Loch):
    """Kreisrunde Oeffnung (Rose)."""

    def __init__(self, sm, zm, radius, teile=8, **kw):
        super().__init__(sm - radius, sm + radius, zm - radius, zm + radius, bogen=True, sohlbank=False, **kw)
        self.sm, self.zm, self.rad, self.teile = sm, zm, radius, teile
        self.zk = zm

    def breite_bei(self, z):
        if z < self.z0 - 1e-6 or z > self.z1 + 1e-6: return None
        w = math.sqrt(max(self.rad ** 2 - (z - self.zm) ** 2, 0.0))
        return (self.sm - w, self.sm + w)

    def hoehen(self, teile=None):
        return {self.zm + self.rad * math.sin(math.pi / 2 * i / self.teile) for i in range(-self.teile, self.teile + 1)}


def loch_von(o):
    _, s0, s1, z0, z1, art, steil = o
    if art == "spitz": return Spitzloch(s0, s1, z0, z1, steil)
    if art == "rose": return Rundloch((s0 + s1) / 2, (z0 + z1) / 2, (s1 - s0) / 2)
    return Loch(s0, s1, z0, z1, sohlbank=False)


def loecher(M, name):
    return [loch_von(o) for o in M.OEFFNUNGEN if o[0] == name]


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Wandflaechen
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def aussenwand(teil, M, name, z0, z1, stoff="Bruchstein", band="mauer", laibung=("Sandstein", "werkstein"), dazu=(), **kw):
    """Aussenflaeche einer Wand der Massdatei mit ihren Oeffnungen; die Laibung reicht bis zur Scheibe in der Wandmitte."""
    o, e, l, n, d = M.wand(name)
    mauer(teil, stoff, o, e, n, l, z0, z1, band, loecher(M, name) + list(dazu), laibung=laibung, dicke=d / 2, **kw)


def innenwand(teil, M, name, z0, z1, band="mauer", dazu=(), ohne_fenster=False, s_bereich=None, **kw):
    """Innenflaeche derselben Wand (um die Wandstaerke nach innen versetzt, gleiche Lage der Oeffnungen)."""
    (ox, oy), e, l, (nx, ny), d = M.wand(name)
    ls = ([] if ohne_fenster else loecher(M, name)) + list(dazu)
    mauer(teil, "Innen", (ox - nx * d, oy - ny * d), e, (-nx, -ny), l, z0, z1, band, ls, laibung=("Innen", "werkstein"), dicke=d / 2,
          zbasis=z0, s_bereich=s_bereich, **kw)


def flaeche(teil, a, b, n, z0, z1, band="mauer", loecher_=(), stoff=None, dicke=0.0, **kw):
    """Freie Wandflaeche von a=(x, y) nach b=(x, y) mit Sichtseite n; Loecher in s ab a."""
    a3, b3 = V((a[0], a[1], 0)), V((b[0], b[1], 0))
    e = (b3 - a3).normalized()
    stoff = stoff or ("Innen" if teil.innen else "Bruchstein")
    lb = ("Innen" if teil.innen else "Sandstein", "werkstein") if dicke else None
    mauer(teil, stoff, a, (e.x, e.y), n, (b3 - a3).length, z0, z1, band, list(loecher_), laibung=lb, dicke=dicke or 0.6, zbasis=kw.pop("zbasis", z0), **kw)


def block(teil, stoff, band, o, eu, ev, b, t, z0, z1, ohne=()):
    """Quader mit waagerechten Lagen: Grundflaeche o + s*eu + q*ev (s bis b, q bis t), von z0 bis z1. `ohne`: "u-", "u+", "v-", "v+", "unten", "oben"."""
    o = V((o[0], o[1], 0.0)); eu = V((eu[0], eu[1], 0.0)).normalized(); ev = V((ev[0], ev[1], 0.0)).normalized()
    m = o + eu * b / 2 + ev * t / 2 + EZ * (z0 + z1) / 2
    h = z1 - z0
    for seite, p, r, lang in (("v-", o, eu, b), ("v+", o + ev * t, eu, b), ("u-", o, ev, t), ("u+", o + eu * b, ev, t)):
        if seite not in ohne: teil.rechteck(stoff, p + EZ * z0, r, EZ, lang, h, band, weg_von=m)
    if "oben" not in ohne: teil.rechteck(stoff, o + EZ * z1, eu, ev, b, t, band, weg_von=m)
    if "unten" not in ohne: teil.rechteck(stoff, o + EZ * z0, eu, ev, b, t, band, weg_von=m)


def strebepfeiler(teil, fuss, raus, breite, tiefe, z0, z1, schraege=0.8, stoff="Sandstein", band="sandstein", fiale=None):
    """Strebepfeiler an einer Wand: `fuss` (x, y) Mitte an der Wand, `raus` Richtung von der Wand weg. Quader bis z1, darauf ein
    Wasserschlag (Schraege zur Wand, `schraege` hoch). `fiale`: (Kantenlaenge, Schafthoehe, Helmhoehe) auf dem aeusseren Ende."""
    f = V((fuss[0], fuss[1], 0.0)); r = V((raus[0], raus[1], 0.0)).normalized(); q = V((-r.y, r.x, 0.0))
    o = f - q * breite / 2
    block(teil, stoff, band, o, q, r, breite, tiefe, z0, z1, ohne=("v-", "unten", "oben"))
    a, b_ = o + EZ * z1, o + q * breite + EZ * z1
    oben = EZ * schraege
    m = f + r * tiefe / 2 + EZ * z1
    if fiale:
        k, schaft, helm = fiale
        tiefe_s = tiefe - k
        teil.rechteck(stoff, a + r * tiefe_s, q, r, breite, k, band, weg_von=m - EZ)        # Absatz unter der Fiale
        mitte = f + r * (tiefe - k / 2)
        fiale_bauen(teil, (mitte.x, mitte.y), k, z1, schaft, helm)
    else:
        tiefe_s = tiefe
    teil.rechteck("Sandstein", a + r * tiefe_s, q, (-r * tiefe_s + oben), breite, (r * tiefe_s - oben).length, "werkstein", zu=m + r + EZ * 3)
    for p in (a, b_):
        v0, v1, mm = teil.baender["werkstein"]
        teil.vieleck("Sandstein", [p, p + r * tiefe_s, p + oben], [(0, v1), (tiefe_s / mm, v1), (0, v0)], weg_von=m)


def fiale_bauen(teil, mitte, kante, z0, schaft, helm, stoff="Sandstein", band="werkstein"):
    """Fiale: quadratischer Schaft und spitzer Helm."""
    x, y = mitte; k = kante / 2
    block(teil, stoff, band, (x - k, y - k), (1, 0), (0, 1), kante, kante, z0, z0 + schaft, ohne=("unten", "oben"))
    v0, v1, mm = teil.baender[band]
    kk = k * 1.25
    ecken = [V((x - kk, y - kk, z0 + schaft)), V((x + kk, y - kk, z0 + schaft)), V((x + kk, y + kk, z0 + schaft)), V((x - kk, y + kk, z0 + schaft))]
    spitze = V((x, y, z0 + schaft + helm))
    for i in range(4):
        a, b_ = ecken[i], ecken[(i + 1) % 4]
        teil.vieleck(stoff, [a, b_, spitze], [(0, v1), (2 * kk / mm, v1), (kk / mm, v0)], weg_von=V((x, y, z0)))
    teil.vieleck(stoff, ecken, [(0, v1), (0.1, v1), (0.1, v0), (0, v0)], zu=V((x, y, z0 - 5)))


GESIMS_KAPPEN = False                                  # die Bauskripte schalten es ein (Dom evangelisch)


def gesimsband(teil, a, b, n, z, hoehe=0.22, vor=0.12, stoff="Sandstein", band="werkstein", ueber=0.0, unten=True):
    """Gesims auf einer Wandflaeche von a nach b (x, y), Sichtseite n: Stirn, Ober- und Unterseite; `ueber`: Ueberstand an den Enden."""
    a3, b3 = V((a[0], a[1], z)), V((b[0], b[1], z)); n3 = V((n[0], n[1], 0)).normalized()
    e = (b3 - a3).normalized()
    a3 -= e * ueber; b3 += e * ueber
    lang = (b3 - a3).length
    m = (a3 + b3) / 2 + EZ * hoehe / 2
    teil.rechteck(stoff, a3 + n3 * vor, e, EZ, lang, hoehe, band, zu=m + n3 * 3)
    teil.rechteck(stoff, a3 + EZ * hoehe, e, n3, lang, vor, band, zu=m + EZ * 3)
    if unten: teil.rechteck(stoff, a3, e, n3, lang, vor, band, zu=m - EZ * 3)
    if GESIMS_KAPPEN:                                     # offene Stirn an beiden Enden schliessen (sonst Blick unter das Gesims, Pruefung 25.11.2026)
        teil.rechteck(stoff, a3, n3, EZ, vor, hoehe, band, zu=a3 - e * 3 + n3 * vor / 2 + EZ * hoehe / 2)
        teil.rechteck(stoff, b3, n3, EZ, vor, hoehe, band, zu=b3 + e * 3 + n3 * vor / 2 + EZ * hoehe / 2)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Fenster: Scheibe, Gewaende, Masswerk
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def _wp(M, name, s, z, raus):
    return V(M.punkt(name, s, z, raus))


def scheibe(teil, stoff, P, loch, zu):
    """Scheibe in der Form der Oeffnung; P(s, z) liefert den Punkt in der Scheibenebene. UV in Metern ab der linken unteren Ecke."""
    zs = sorted(loch.hoehen())
    for za, zb in zip(zs[:-1], zs[1:]):
        if zb - za < 1e-5: continue
        (a0, a1), (b0, b1) = loch.breite_bei(za + 1e-6), loch.breite_bei(zb - 1e-6)
        pts = [P(a0, za), P(a1, za), P(b1, zb), P(b0, zb)]
        uvs = [(a0 - loch.s0, 1 - (za - loch.z0)), (a1 - loch.s0, 1 - (za - loch.z0)), (b1 - loch.s0, 1 - (zb - loch.z0)), (b0 - loch.s0, 1 - (zb - loch.z0))]
        if b1 - b0 < 1e-4: pts, uvs = pts[:3], uvs[:3]
        elif a1 - a0 < 1e-4: pts, uvs = pts[1:], uvs[1:]
        teil.vieleck(stoff, pts, uvs, zu=zu)


def gewaende(teil, P, n, loch, breite=0.16, vor=0.05, stoff="Sandstein", band="werkstein"):
    """Gewaende um eine Spitzbogenoeffnung: flaches Band auf der Wand (steht um `vor` vor), ohne Seitenflaechen."""
    v0, v1, mm = teil.baender[band]
    m = (loch.s0 + loch.s1) / 2
    u = teil.zufall.random()
    def stueck(pa, pb, qa, qb):
        lang = (P(*pb, vor) - P(*pa, vor)).length
        teil.vieleck(stoff, [P(*pa, vor), P(*pb, vor), P(*qb, vor), P(*qa, vor)], [(u, v1), (u + lang / mm, v1), (u + lang / mm, v1 - breite / ((v1 - v0) * mm) * (v1 - v0)), (u, v1 - breite / ((v1 - v0) * mm) * (v1 - v0))],
                     zu=P(m, loch.zk, vor) + n * 5)
    rand = [(loch.s1, loch.z0)] + loch.umriss() + [(loch.s0, loch.z0)]
    mitte = (m, loch.zk)
    def aussen(p):
        if p[1] <= loch.zk + 1e-6: return (p[0] + (breite if p[0] > m else -breite), p[1])
        d = V((p[0] - mitte[0], p[1] - mitte[1])).normalized()
        return (p[0] + d.x * breite, p[1] + d.y * breite)
    for pa, pb in zip(rand[:-1], rand[1:]):
        stueck(pa, pb, aussen(pa), aussen(pb))
    teil.rechteck(stoff, P(loch.s0 - breite, loch.z0 - 0.14, vor + 0.04), P(1, 0, 0) - P(0, 0, 0), EZ, loch.s1 - loch.s0 + 2 * breite, 0.14, band, zu=P(m, loch.z0, 1))   # Sohlbank


def masswerk(teil, P, n, loch, dicke=0.10, stoff="Sandstein", band="werkstein", innen=False):
    """Schlichtes Masswerk: Mittelpfosten bis ueber den Kaempfer, darueber gabelt er sich zu den Bogenschenkeln (Y-Masswerk)."""
    m = (loch.s0 + loch.s1) / 2
    zg = loch.zk + 0.15 * loch.h
    teil.stab(stoff, P(m, loch.z0), P(m, zg), dicke, band, breite=dicke, oben=n)
    zt = loch.zk + 0.62 * loch.h
    w = spitz_breite(loch.a, loch.h, zt - loch.zk)
    for seite in (-1, 1):
        teil.stab(stoff, P(m, zg), P(m + seite * w, zt), dicke, band, breite=dicke, oben=n)


def rose(teil, P, n, loch, stoff="Sandstein", band="werkstein", speichen=8):
    """Rosenfenster: Ring, Nabe und Speichen vor der Scheibe."""
    v0, v1, mm = teil.baender[band]
    c = lambda r, w, vor=0.0: P(loch.sm + r * math.cos(w), loch.zm + r * math.sin(w), vor)
    seg = 16
    for i in range(seg):
        w0, w1 = 2 * math.pi * i / seg, 2 * math.pi * (i + 1) / seg
        for r0, r1, vor in ((loch.rad, loch.rad + 0.22, 0.05), (0.0, 0.32, -0.02)):
            pts = [c(r0, w0, vor), c(r1, w0, vor), c(r1, w1, vor), c(r0, w1, vor)]
            uvs = [(i * 0.1, v1), (i * 0.1, v0), (i * 0.1 + 0.1, v0), (i * 0.1 + 0.1, v1)]
            if r0 == 0.0: pts, uvs = pts[1:], uvs[1:]
            teil.vieleck(stoff, pts, uvs, zu=c(0, 0, 0) + n * 5)
    for i in range(speichen):
        w = 2 * math.pi * (i + 0.5) / speichen
        teil.stab(stoff, c(0.3, w, -0.04), c(loch.rad, w, -0.04), 0.09, band, breite=0.09, oben=n)
    for i in range(seg):                                    # innerer Ring auf halbem Radius
        w0, w1 = 2 * math.pi * i / seg, 2 * math.pi * (i + 1) / seg
        r0, r1 = loch.rad * 0.55, loch.rad * 0.55 + 0.09
        teil.vieleck(stoff, [c(r0, w0, -0.02), c(r1, w0, -0.02), c(r1, w1, -0.02), c(r0, w1, -0.02)],
                     [(i * 0.1, v1), (i * 0.1, v0), (i * 0.1 + 0.1, v0), (i * 0.1 + 0.1, v1)], zu=c(0, 0, 0) + n * 5)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Gewoelbe, Boden
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def _kappe(teil, pts, zu):
    v0, v1, mm = teil.baender["gewoelbe"]
    uvs = [(0, v1), ((pts[1] - pts[0]).length / mm, v1), ((pts[1] - pts[0]).length / mm, v1 - min((pts[2] - pts[1]).length / mm, v1 - v0)), (0, v1 - min((pts[3] - pts[0]).length / mm, v1 - v0))]
    if (pts[2] - pts[3]).length < 1e-5: pts, uvs = pts[:3], uvs[:3]
    elif (pts[0] - pts[1]).length < 1e-5: pts, uvs = pts[1:], uvs[1:]
    teil.vieleck("Innen", pts, uvs, zu=zu)


def rippe(teil, p, q, breite=0.16, tiefe=0.13, band="werkstein"):
    """Rippe unter dem Gewoelbe von p nach q: zwei Flaechen, die sich unten in einem Grat treffen."""
    p, q = V(p), V(q)
    e = (q - p).normalized()
    s = e.cross(EZ)
    if s.length < 1e-5: return
    s = s.normalized() * breite / 2
    ab = (s.cross(e)).normalized() * tiefe
    if ab.z > 0: ab = -ab
    v0, v1, mm = teil.baender[band]
    lang = (q - p).length
    for seite in (1, -1):
        teil.vieleck("Innen", [p + s * seite, q + s * seite, q + ab, p + ab], [(0, v0), (lang / mm, v0), (lang / mm, v0 + breite / mm), (0, v0 + breite / mm)],
                     zu=(p + q) / 2 + s * seite * 4 + ab * 4)


def kreuzgewoelbe(teil, x0, x1, y0, y1, zk, zs, teile=5, rippen=True, gurt=("y0", "y1")):
    """Kreuzgewoelbe ueber einem Joch: zwei Spitztonnen (quer ueber x, laengs ueber y) mit gleichem Kaempfer zk und Scheitel zs;
    die Decke ist die hoehere der beiden. Rippen auf den Graten, Gurtboegen an den Jochgrenzen `gurt`."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    ax, ay = (x1 - x0) / 2, (y1 - y0) / 2
    H = zs - zk
    hs = [H * math.sin(math.pi / 2 * k / teile) for k in range(teile + 1)]
    dx = [spitz_breite(ax, H, h) for h in hs]; dy = [spitz_breite(ay, H, h) for h in hs]
    unten = V((cx, cy, zk - 5))
    for sx in (-1, 1):
        for sy in (-1, 1):
            G = [V((cx + sx * dx[k], cy + sy * dy[k], zk + hs[k])) for k in range(teile + 1)]            # Grat
            for k in range(teile):
                # Kappe der Quertonne (z haengt an x): vom Grat zur Jochgrenze in y
                _kappe(teil, [G[k], V((G[k].x, cy + sy * ay, G[k].z)), V((G[k + 1].x, cy + sy * ay, G[k + 1].z)), G[k + 1]], unten)
                # Kappe der Laengstonne (z haengt an y): vom Grat zur Wand in x (Schildbogen)
                _kappe(teil, [G[k], V((cx + sx * ax, G[k].y, G[k].z)), V((cx + sx * ax, G[k + 1].y, G[k + 1].z)), G[k + 1]], unten)
                if rippen: rippe(teil, G[k], G[k + 1])
            for g in gurt:
                if (g == "y0") != (sy < 0): continue
                for k in range(teile):
                    p = V((cx + sx * dx[k], cy + sy * ay, zk + hs[k])); q = V((cx + sx * dx[k + 1], cy + sy * ay, zk + hs[k + 1]))
                    rippe(teil, p, q, breite=0.30, tiefe=0.22, band="sandstein")


def spitztonne(teil, a, b, halb, zk, zs, teile=5, gurt=True):
    """Spitztonne entlang der Achse a -> b (x, y), halbe Spannweite `halb`."""
    a3, b3 = V((a[0], a[1], 0)), V((b[0], b[1], 0))
    e = (b3 - a3).normalized(); q = V((-e.y, e.x, 0))
    H = zs - zk
    hs = [H * math.sin(math.pi / 2 * k / teile) for k in range(teile + 1)]
    d = [spitz_breite(halb, H, h) for h in hs]
    unten = (a3 + b3) / 2 + EZ * (zk - 5)
    for s in (-1, 1):
        for k in range(teile):
            p0, p1 = a3 + q * s * d[k] + EZ * (zk + hs[k]), a3 + q * s * d[k + 1] + EZ * (zk + hs[k + 1])
            _kappe(teil, [p0, p0 + (b3 - a3), p1 + (b3 - a3), p1], unten)
            if gurt:
                for ende in (a3, b3):
                    rippe(teil, ende + q * s * d[k] + EZ * (zk + hs[k]), ende + q * s * d[k + 1] + EZ * (zk + hs[k + 1]), breite=0.30, tiefe=0.22, band="sandstein")


def schachboden(teil, x0, x1, y0, y1, z, raster=(0.0, 0.0)):
    """Schachbrett aus Platten von 0,56 m: Streifen entlang x, jeder zweite um eine Platte versetzt; das Raster liegt fest in der Welt."""
    v0, v1, m = teil.baender["platten"]
    k = (v1 - v0) * m                                  # 0,56 (eine Platte; im Blatt 8 Platten auf 1 UV)
    j = math.floor((y0 - raster[1]) / k)
    while raster[1] + j * k < y1 - 1e-6:
        ya, yb = max(y0, raster[1] + j * k), min(y1, raster[1] + (j + 1) * k)
        if yb - ya > 1e-5:
            t0, t1 = (ya - (raster[1] + j * k)) / k, (yb - (raster[1] + j * k)) / k
            u = lambda x: (x - raster[0]) / m + (0.125 if j % 2 else 0.0)
            pts = [V((x0, ya, z)), V((x1, ya, z)), V((x1, yb, z)), V((x0, yb, z))]
            uvs = [(u(x0), v1 - t0 * (v1 - v0)), (u(x1), v1 - t0 * (v1 - v0)), (u(x1), v1 - t1 * (v1 - v0)), (u(x0), v1 - t1 * (v1 - v0))]
            netz_vieleck(teil, "Innen", pts, uvs, zu=V((x0, ya, z + 5)))
        j += 1


def netz_vieleck(teil, stoff, pts, uvs, **kw):
    """Flaeche mit fertiger UV am texeltreuen Umbau vorbei (Boden: das Raster steht schon)."""
    import netzbau
    netzbau.Teil.vieleck(teil, stoff, pts, uvs, **kw)


def saeule(teil, mitte, radius, z0, z1, seiten=8, band="werkstein", kapitell=0.5):
    """Rundpfeiler mit Sockel und Kapitell."""
    x, y = mitte
    r = radius
    profil = [(r * 1.3, z0), (r * 1.3, z0 + 0.25), (r, z0 + 0.45), (r, z1 - kapitell), (r * 1.28, z1 - 0.12), (r * 1.28, z1)]
    v0, v1, m = teil.baender[band]
    um = 2 * math.pi * r
    c = V((x, y, 0))
    ring = lambda rr, z: [c + V((rr * math.cos(2 * math.pi * k / seiten), rr * math.sin(2 * math.pi * k / seiten), z)) for k in range(seiten)]
    for (r0, za), (r1, zb) in zip(profil[:-1], profil[1:]):
        A, Bn = ring(r0, za), ring(r1, zb)
        for k in range(seiten):
            k2 = (k + 1) % seiten
            # Lagen waagerecht: u um den Pfeiler, v die Hoehe in Lagen von Bandhoehe
            n = max(1, math.ceil((zb - za) / ((v1 - v0) * m) - 1e-6))
            for i in range(n):
                ta, tb = i / n, (i + 1) / n
                pa0, pa1 = A[k].lerp(Bn[k], ta), A[k2].lerp(Bn[k2], ta)
                pb0, pb1 = A[k].lerp(Bn[k], tb), A[k2].lerp(Bn[k2], tb)
                u0 = k * um / seiten / m + i * 0.37
                netz_vieleck(teil, "Innen", [pa0, pa1, pb1, pb0], [(u0, v1), (u0 + um / seiten / m, v1), (u0 + um / seiten / m, v1 - (zb - za) / n / m), (u0, v1 - (zb - za) / n / m)],
                             weg_von=c + EZ * (za + zb) / 2, glatt=True)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Stoffe, Bau, Export
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def schieferflaeche(teil, a0, a1, b0, b1, aussen, lage=0.30, stufe=0.09, karte=True, saat=1, band="schiefer", seiten=(True, True)):
    """Schieferdach in Deckreihen (Nutzer, 24.11.2026), nach dem Verfahren von wohnhaus_mensch_s23.ziegelflaeche: Die Flaeche von der
    Unterkante a0-a1 zur Oberkante b0-b1 (b0 == b1: Dreieck, Helm) liegt in Lagen von `lage` Metern, jede am Fuss angehoben (Keil).
    Jede Lage fuellt eine der Reihen des Bandes (dom_trimsheet.SCHIEFER_REIHEN, reihum).
    Anders als am Wohnhaus steht die Stirn jeder Lage SENKRECHT (`stufe` Meter hoch) und nimmt ihre Farbe aus dem dunklen Band
    "schiefer_kante": Am Wohnhaus zeichnet die gebackene Verdeckung (AO-Atlas) die Reihen, die Zelle des Doms ist dafuer zu grob
    (rund 0,25 m je Texel), und eine Stirn quer zur Dachflaeche zeigt nach unten - von der Kamera aus unsichtbar. Die senkrechte
    Stirn sieht die Kamera als dunkle Linie; die Lage darunter endet am Fuss der Stirn.
    `stufe=0`: glatte Flaeche in Streifen von Bandhoehe (Turmhelme: Keile ueberschnitten sich an den Graten), die Reihen kommen dort
    nur aus der Textur. `seiten`: (Rand a0-b0, Rand a1-b1) mit Seitenflaechen der Keile; False an Raendern, die an eine Wand, eine Nachbarflaeche oder ein Gratblech stossen. `karte`: Alpha-Card (Schieferkante) an der Traufe. Lage 0,30 m statt 0,25 m wie am Wohnhaus: mit 0,25 m lag
    die Huelle 163 Dreiecke ueber dem Budget (15.163 von 15.000); die Reihe der Textur (0,25 m) wird dafuer um ein Fuenftel gestreckt."""
    a0, a1, b0, b1 = Vector(a0), Vector(a1), Vector(b0), Vector(b1)
    e = (a1 - a0).normalized()
    hoch = (b0 + b1) / 2 - (a0 + a1) / 2
    n = e.cross(hoch).normalized()
    if n.dot(Vector(aussen) - a0) < 0: n = -n
    lang = hoch.length
    runter = -hoch.normalized()
    v0, v1, m = teil.baender[band]
    c0, c1, _ = teil.baender["schiefer_kante"]
    reihen = SCHIEFER_REIHEN if stufe > 0 else 1
    rh = (v1 - v0) / reihen
    rand = 1.5 / 2048                                       # Abstand zur Nachbarreihe (kein Durchscheinen beim Filtern)
    anz = max(1, round(lang / (lage * (1 if stufe > 0 else SCHIEFER_REIHEN))))
    hub = EZ * stufe        # Fuss der Lage: senkrecht `stufe` ueber der Dachebene. Nur senkrecht: Nachbarflaechen (Grate, First) teilen dann dieselben
                            # Punkte der Gratlinie; ein Hub entlang der Flaechennormalen liess an jedem Grat einen Spalt von 35 mm (Pruefung 25.11.2026)
    for k in range(anz):
        t0, t1 = k / anz, (k + 1) / anz
        p0, q0, p1, q1 = a0.lerp(b0, t0), a1.lerp(b1, t0), a0.lerp(b0, t1), a1.lerp(b1, t1)
        # Oberkante der Lage = Fuss der Stirn der naechsten Lage (liegt in der Dachebene)
        r = (k + saat) % reihen
        va, vb = v0 + r * rh + rand, v0 + (r + 1) * rh - rand            # va oben (Schatten der naechsten Reihe), vb unten (helle Kante)
        off = ((k * 7919 + saat * 31) % 1000) / 1000.0 * m
        u = lambda p: ((p - a0).dot(e) + off) / m
        pts = [p0 + hub, q0 + hub, q1, p1]
        uvs = [(u(p0), vb), (u(q0), vb), (u(q1), va), (u(p1), va)]
        if (q1 - p1).length < 1e-5: pts, uvs = pts[:3], uvs[:3]
        teil.vieleck("Schiefer", pts, uvs, zu=(pts[0] + pts[1] + pts[2]) / 3 + n)
        if stufe > 0:                                       # senkrechte Stirn der Lage, dunkel (Mitte des Bandes "schiefer_kante")
            cm, ch = (c0 + c1) / 2, (c1 - c0) * 0.2
            teil.vieleck("Schiefer", [p0, q0, q0 + hub, p0 + hub],
                         [(u(p0), cm + ch), (u(q0), cm + ch), (u(q0), cm - ch), (u(p0), cm - ch)], zu=(p0 + q0) / 2 + hub / 2 + (n - EZ * n.z).normalized() * 5)
        if stufe > 0 and any(seiten):                          # Seitenflaechen der Keile an den freien Randern (Giebel, Gauben): sonst blickt man unter jede Lage
            cm2 = (c0 + c1) / 2
            mitte_f = (a0 + a1 + b0 + b1) / 4
            for ep, eq, mit in ((p0, p1, seiten[0]), (q0, q1, seiten[1])):
                if not mit: continue
                nh = (eq - ep).cross(EZ)
                if nh.length < 1e-6: continue
                nh.normalize()
                if nh.dot(mitte_f - ep) > 0: nh = -nh            # waagerecht nach aussen, vom Inneren der Flaeche weg
                teil.vieleck("Schiefer", [ep, ep + hub, eq], [(u(ep), cm2), (u(ep) + 0.01, cm2), (u(eq), cm2)], zu=(ep + eq) / 2 + nh * 3)
    if karte:
        hk = 0.20
        hebe = hub + n * 0.012
        pts = [a0 + runter * 0.14 + hebe, a1 + runter * 0.14 + hebe, a1 - runter * (hk - 0.14) + hebe, a0 - runter * (hk - 0.14) + hebe]
        uu = lambda p: ((p - a0).dot(e) + 0.3) / m
        teil.vieleck("Schieferkante", pts, [(uu(a0), c1), (uu(a1), c1), (uu(a1), c0), (uu(a0), c0)], zu=(pts[0] + pts[2]) / 2 + n)


def grat(teil, p, q, pa, pb, breite=0.14, hoehe=0.13, lift=0.11):
    """Gratabdeckung (Blei): schmales Dach aus zwei Streifen ueber der Gratlinie p-q zweier Schieferflaechen; pa, pb: je ein Punkt
    der beiden Flaechen abseits des Grats. Deckt den Stoss der Lagen (Hub senkrecht `stufe` = 9 cm) und jede Fuge an der Linie ab."""
    p, q, pa, pb = Vector(p), Vector(q), Vector(pa), Vector(pb)
    e = (q - p).normalized()
    seiten = []
    for x in (pa, pb):
        d = (x - p) - e * (x - p).dot(e)
        d.normalize()
        nn = e.cross(d)
        if nn.z < 0: nn = -nn
        seiten.append((d, nn))
    nb = (seiten[0][1] + seiten[1][1]).normalized()
    for d, nn in seiten:
        pts = [p + d * breite + EZ * lift, q + d * breite + EZ * lift, q + nb * hoehe, p + nb * hoehe]
        farbflaeche(teil, "Firstblech", pts, "blei", zu=(pts[0] + pts[2]) / 2 + nn * 3 + EZ * 3)
    for ende, sg in ((p, -1), (q, 1)):                    # Stirn der Abdeckung an beiden Enden (sonst blickt man in das Dach aus Blei)
        farbflaeche(teil, "Firstblech", [ende + seiten[0][0] * breite + EZ * lift, ende + nb * hoehe, ende + seiten[1][0] * breite + EZ * lift], "blei", zu=ende + e * sg * 3)


def stoffe_aussen():
    b = "dom_trim"
    S = B.stoff
    return {
        "Schiefer": S("Schiefer", b), "Schieferkante": S("Schieferkante", b, schere=True), "Fachwerk": S("Fachwerk", b),
        "Sandstein": S("Sandstein", b), "Bruchstein": S("Bruchstein", b), "Bretter": S("Bretter", b), "Firstblech": S("Firstblech", b),
        "Fenster": S("Fenster", farbe=(0.09, 0.11, 0.14), rauheit=0.2), "Bleinetz": S("Bleinetz", farbe=(0.1, 0.1, 0.11), alpha=0.5),
    }


def stoffe_innen():
    b = "dom_innen_trim"
    return {"Innen": B.stoff("Innen", b), "Innenglas": B.stoff("Innenglas", farbe=(0.55, 0.62, 0.6), alpha=0.16, rauheit=0.15)}


def neu():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")


def knoten(name, kinder):
    """Leerer Knoten als Elternteil (Einrichtung je Glaube: das Spiel schaltet den Knoten sichtbar)."""
    o = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(o)
    for k in kinder: k.parent = o
    return o
