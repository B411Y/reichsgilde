"""Bibliothek der Rathaus-Bauskripte (Blender): Mauern mit Rund- und Rechtecköffnungen in Lagen, Quaderecken, Bogenringe,
Fensterstoecke, Tueren, Treppen, Stuehle, Stoffe, UV2 und gebackene Verdeckung, Anker, Export und Kontrollbilder.

Alle Masse kommen aus rathaus_masse.py; die Skripte rathaus_aussen.py und rathaus_<raum>.py rufen diese Funktionen auf.
Mauern laufen in Lagen von der Hoehe ihres Bandes (Bruchstein 0,40 m, Putz 0,80 m): so wird die Textur nicht senkrecht gestreckt
und jede Lage beginnt an einer anderen Stelle des Bandes (keine sichtbare Wiederholung).
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import rathaus_masse as M                                                          # noqa: E402
import netzbau                                                                     # noqa: E402
from netzbau import ROT, SCHWARZ                                                   # noqa: E402,F401
from rathaus_trimsheet import BAENDER_RATHAUS, BAENDER_RATHAUS_INNEN, FELDER_RATHAUS_INNEN   # noqa: E402


class Teil(netzbau.Teil):
    """Teil der Rathausraeume mit texeltreuer UV (Klicktest 2: Holzwand im Kerker, Holz der Turmtreppe gestreckt).

    netzbau.Teil legt jede Flaeche quer zur Maserung ueber das ganze Band, egal wie breit sie ist - bei einer 2,6 m hohen Bretterwand
    auf dem 0,40 m hohen Balkenband sechsfach gestreckt, bei einer 3 cm Leiste stark gestaucht. Hier (nur im Innenblatt) nimmt eine
    Flaeche quer genau so viel vom Band, wie sie in Metern breit ist; ist sie breiter als das Band, wird sie in gleich breite Streifen
    geteilt (jeder mit eigenem Versatz in u, wie einzelne Bretter). Farbfelder (u_bereich) bleiben wie sie sind.
    Das Aussenblatt bleibt unveraendert (die Huelle ist abgenommen).
    """

    def __init__(self, name, baender, saat=1):
        super().__init__(name, baender, saat)
        self.texeltreu = baender is BAENDER_RATHAUS_INNEN

    def rechteck(self, stoff, o, eu, ev, b, h, band, laengs="u", u0=None, anteil=(0.0, 1.0), u_bereich=None, **kw):
        if self.texeltreu and u_bereich and max(b, h) > 4 * max(min(b, h), 1e-4):
            # Lange, schmale Teile im Farbfeld (Gitterstäbe, Bandeisen): Das Feld kachelt nicht, über 2,6 m gezogen wurde seine Struktur zu
            # Längsstreifen (Klicktest 3, Zellengitter). Darum nur ein kleiner Fleck aus der Mitte des Feldes: die Farbe des Stoffs ohne Streifen.
            ua, ub = u_bereich
            u_bereich = (ua + 0.45 * (ub - ua), ua + 0.47 * (ub - ua))
            anteil = (0.45, 0.50)
        if not self.texeltreu or u_bereich:
            return super().rechteck(stoff, o, eu, ev, b, h, band, laengs=laengs, u0=u0, anteil=anteil, u_bereich=u_bereich, **kw)
        v0, v1, m = self.baender[band]
        band_m = (v1 - v0) * m * (anteil[1] - anteil[0])          # so viele Meter fasst das Band quer
        quer = h if laengs == "u" else b
        n = max(1, math.ceil(quer / band_m - 1e-6))
        o, eu, ev = Vector(o), Vector(eu).normalized(), Vector(ev).normalized()
        schritt = quer / n
        a1 = anteil[0] + schritt / ((v1 - v0) * m)
        for i in range(n):
            if laengs == "u":
                super().rechteck(stoff, o + ev * (i * schritt), eu, ev, b, schritt, band, laengs="u",
                                 u0=u0 if i == 0 else None, anteil=(anteil[0], a1), **kw)
            else:
                super().rechteck(stoff, o + eu * (i * schritt), eu, ev, schritt, h, band, laengs="v",
                                 u0=u0 if i == 0 else None, anteil=(anteil[0], a1), **kw)

    def vieleck(self, stoff, punkte, uvs, weg_von=None, zu=None, farbe=SCHWARZ, glatt=False):
        """Wie netzbau, aber im Innenblatt texeltreu: Eine von Hand belegte Flaeche (Treppen, Kamin, Dreh-Koerper, Tueren), deren UV
        mehr als 10 % neben der Dichte des Bandes liegt oder aus dem Band ragt, bekommt neue UV - Maserung in der Richtung, in der u
        bisher lief, Dichte wie das Band, quer in Streifen von Bandhoehe. Farbfelder (Kleinteile), Glas und flache Farbflecken
        (UV ohne Flaeche) bleiben, ebenso jede Flaeche, die schon stimmt (Waende, Boeden: deren UV laeuft ueber Flaechen hinweg durch)."""
        weiter = lambda p, u: super(Teil, self).vieleck(stoff, p, u, weg_von=weg_von, zu=zu, farbe=farbe, glatt=glatt)
        if not self.texeltreu or stoff != "Innen" or len(punkte) < 3:
            return weiter(punkte, uvs)
        P = [Vector(p) for p in punkte]
        U = [tuple(u) for u in uvs]
        vm = sum(u[1] for u in U) / len(U)
        band = next((n for n, (v0, v1, m) in self.baender.items() if v0 <= vm < v1), None)
        if band == "kleinteile":
            return weiter(punkte, _farbfleck(P, U))
        if band is None:
            return weiter(punkte, uvs)
        v0, v1, m = self.baender[band]
        # Ableitungen dP/du, dP/dv aus dem groessten Dreieck des Faechers
        i = max(range(1, len(P) - 1), key=lambda k: (P[k] - P[0]).cross(P[k + 1] - P[0]).length)
        e1, e2 = P[i] - P[0], P[i + 1] - P[0]
        d1 = (U[i][0] - U[0][0], U[i][1] - U[0][1]); d2 = (U[i + 1][0] - U[0][0], U[i + 1][1] - U[0][1])
        det = d1[0] * d2[1] - d2[0] * d1[1]
        if e1.cross(e2).length < 1e-6:
            return weiter(punkte, uvs)
        n = e1.cross(e2).normalized()
        if abs(det) < 1e-9:
            # UV ohne Flaeche (ein einziger Farbpunkt des Bandes): kleine Flecken bleiben, groessere (Stirnseiten der Stufen) bekommen
            # Maserung entlang ihrer laengsten Kante - sonst einfarbig ohne Textur
            flaeche = sum(((P[k] - P[0]).cross(P[k + 1] - P[0]).length for k in range(1, len(P) - 1))) / 2
            if flaeche < 0.005:
                return weiter(punkte, uvs)
            lang = max(((P[(k + 1) % len(P)] - P[k]) for k in range(len(P))), key=lambda e: e.length)
            g = (lang - n * lang.dot(n)).normalized()
            abwaerts, u_off = True, self.zufall.random()
        else:
            dpdu = (e1 * d2[1] - e2 * d1[1]) / det
            dpdv = (e2 * d1[0] - e1 * d2[0]) / det
            im_band = all(v0 - 1e-4 <= u[1] <= v1 + 1e-4 for u in U)
            if im_band and abs(dpdu.length / m - 1) < 0.1 and abs(dpdv.length / m - 1) < 0.1:
                return weiter(punkte, uvs)
            g = (dpdu - n * dpdu.dot(n)).normalized()         # Maserung
            abwaerts = dpdv.dot(n.cross(g)) > 0               # v waechst entlang +a (wie bisher)
            u_off = U[0][0] - P[0].dot(g) / m
        a = n.cross(g)                                        # quer
        t = [p.dot(a) for p in P]
        t0, t1 = min(t), max(t)
        k = max(1, math.ceil((t1 - t0) / ((v1 - v0) * m) - 1e-6))
        schritt = (t1 - t0) / k
        for j in range(k):
            lo, hi = t0 + j * schritt, t0 + (j + 1) * schritt
            stueck = _zwischen(P, a, lo, hi)
            if len(stueck) < 3: continue
            uo = u_off if j == 0 else self.zufall.random()
            neu = []
            for p in stueck:
                s = (p.dot(a) - lo) / m
                neu.append((p.dot(g) / m + uo, v0 + s if abwaerts else v1 - s))
            weiter(stueck, neu)


def _farbfleck(P, U):
    """Farbfeld (Kleinteile): Greift eine Fläche mehr als 10 % der Feldbreite und ist dabei über 4 gestreckt, zeigt sie die Struktur des
    Feldes als Streifen – dann schrumpft ihre UV um die Mitte auf einen Fleck von 0,0025 (die Farbe des Stoffs). Sonst bleibt sie."""
    us = [u[0] for u in U]
    breite = max(us) - min(us)
    if breite <= 0.0125 or len(P) < 3:
        return U
    i = max(range(1, len(P) - 1), key=lambda k: (P[k] - P[0]).cross(P[k + 1] - P[0]).length)
    e1, e2 = P[i] - P[0], P[i + 1] - P[0]
    d1 = (U[i][0] - U[0][0], U[i][1] - U[0][1]); d2 = (U[i + 1][0] - U[0][0], U[i + 1][1] - U[0][1])
    det = d1[0] * d2[1] - d2[0] * d1[1]
    if abs(det) > 1e-12:
        lu = ((e1 * d2[1] - e2 * d1[1]) / det).length
        lv = ((e2 * d1[0] - e1 * d2[0]) / det).length
        if max(lu, lv) <= 4 * max(min(lu, lv), 1e-9):
            return U
    mu, mv = (max(us) + min(us)) / 2, sum(u[1] for u in U) / len(U)
    k = 0.0025 / breite
    return [(mu + (u[0] - mu) * k, mv + (u[1] - mv) * k) for u in U]


def _zwischen(punkte, a, lo, hi):
    """Teil eines konvexen Vielecks zwischen den Ebenen p.a = lo und p.a = hi (Sutherland-Hodgman, Reihenfolge bleibt)."""
    def schneide(pts, innen, wert):
        aus = []
        for i, p in enumerate(pts):
            q = pts[(i + 1) % len(pts)]
            ip, iq = innen(p), innen(q)
            if ip: aus.append(p)
            if ip != iq:
                dp, dq = p.dot(a) - wert, q.dot(a) - wert
                aus.append(p + (q - p) * (dp / (dp - dq)))
        return aus
    pts = schneide(list(punkte), lambda p: p.dot(a) >= lo - 1e-7, lo)
    if len(pts) < 3: return pts
    return schneide(pts, lambda p: p.dot(a) <= hi + 1e-7, hi)

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
ZIEL_MODELL = os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau")
ZIEL_TEXTUR = os.path.join(WURZEL, "game", "assets", "texturen", "neubau")
EZ = Vector((0, 0, 1))


def optionen():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))


def feld(name):
    """u-Bereich eines Farbfelds im Kleinteile-Band des Innenblatts."""
    i = FELDER_RATHAUS_INNEN.index(name)
    n = len(FELDER_RATHAUS_INNEN)
    return (i / n + 0.01, (i + 1) / n - 0.01)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Mauern
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

class Loch:
    """Oeffnung in Wandkoordinaten: s entlang der Wand ab ihrem Ursprung, z absolut. `bogen`: Rundbogen, Scheitel bei z1."""

    def __init__(self, s0, s1, z0, z1, bogen=False, laibung=True, sohlbank=True):
        self.s0, self.s1, self.z0, self.z1, self.bogen = s0, s1, z0, z1, bogen
        self.laibung, self.sohlbank = laibung, sohlbank
        self.r = (s1 - s0) / 2
        self.zk = z1 - self.r if bogen else z1         # Kaempfer

    def breite_bei(self, z):
        if z < self.z0 - 1e-6 or z > self.z1 + 1e-6: return None
        if not self.bogen or z <= self.zk: return (self.s0, self.s1)
        w = math.sqrt(max(self.r * self.r - (z - self.zk) ** 2, 0.0))
        m = (self.s0 + self.s1) / 2
        return (m - w, m + w)

    def hoehen(self, teile=8):
        zs = {self.z0, self.z1, self.zk}
        if self.bogen:
            zs |= {self.zk + self.r * math.sin(math.pi / 2 * i / teile) for i in range(teile + 1)}
        return zs


def _lagen_versatz(k, saat):
    return ((k * 7919 + saat * 104729) % 1000) / 1000.0


def mauer(teil, stoff, o, eu, n, laenge, z0, z1, band, loecher=(), zbasis=0.0, kurs=None, laibung=None, dicke=0.6,
          ecken=None, saat=1, s_bereich=None, grenze=None, farbe=SCHWARZ):
    """Senkrechte Wandflaeche in Lagen. o: Ursprung (x, y) der Flaeche, eu: Richtung entlang der Wand, n: Sichtseite (Normale).
    `loecher`: Liste von Loch. `laibung`: (Stoff, Band) -> Laibungen um `dicke` hinter die Flaeche (nur fuer die Huelle).
    `ecken`: {"links": (Stoff, Band, phase), "rechts": ...} Quaderecken in derselben Lage (wechselnd 0,70 / 0,40 breit).
    `s_bereich`: (sa, sb) nur dieser Teil der Wand. `grenze(z) -> (lo, hi)`: sichtbarer Bereich je Hoehe (Giebel unter dem Dach,
    Turmflaeche ueber dem Dach); die Grenzen muessen innerhalb einer Lage gerade verlaufen (Dachneigung)."""
    o = Vector((o[0], o[1], 0.0)); eu = Vector((eu[0], eu[1], 0.0)).normalized(); n = Vector((n[0], n[1], 0.0)).normalized()
    v0, v1, m = teil.baender[band]
    kurs = kurs or (v1 - v0) * m
    sa, sb = s_bereich or (0.0, laenge)
    loecher = [l for l in loecher if l.s1 > sa and l.s0 < sb and l.z1 > z0 and l.z0 < z1]
    zs = {z0, z1}
    k = math.floor((z0 - zbasis) / kurs) + 1
    while zbasis + k * kurs < z1 - 1e-6:
        zs.add(zbasis + k * kurs); k += 1
    for l in loecher: zs |= {z for z in l.hoehen() if z0 < z < z1}
    zs = sorted(zs)
    P = lambda s, z: o + eu * s + EZ * z

    def uv(s, z, band_, basis, kurs_, versatz):
        a0, a1, mm = teil.baender[band_]
        t = min(max((z - basis) / kurs_, 0.0), 1.0)
        return ((s + versatz) / mm, a1 - t * (a1 - a0))

    for j in range(len(zs) - 1):
        za, zb = zs[j], zs[j + 1]
        if zb - za < 1e-5: continue
        zm = (za + zb) / 2
        lage = math.floor((zm - zbasis) / kurs + 1e-9)
        basis = zbasis + lage * kurs
        versatz = _lagen_versatz(lage, saat) * m
        aktiv = []
        for l in loecher:
            ia, ib = l.breite_bei(za + 1e-6), l.breite_bei(zb - 1e-6)
            if ia and ib and (ia[1] - ia[0] > 1e-5 or ib[1] - ib[0] > 1e-5): aktiv.append((ia, ib, l))
        aktiv.sort(key=lambda x: x[0][0] + x[1][0])
        # Kanten der Wandstuecke (links, rechts je unten und oben)
        lo_a = lo_b = sa; hi_a = hi_b = sb
        if grenze:
            (ga, ha), (gb, hb) = grenze(za), grenze(zb)
            lo_a, lo_b, hi_a, hi_b = max(sa, ga), max(sa, gb), min(sb, ha), min(sb, hb)
            if hi_a - lo_a < 1e-5 and hi_b - lo_b < 1e-5: continue
            if hi_a < lo_a: hi_a = lo_a = (hi_a + lo_a) / 2
            if hi_b < lo_b: hi_b = lo_b = (hi_b + lo_b) / 2
        kanten = [((lo_a, lo_b), None)]
        for ia, ib, l in aktiv:
            kanten.append(((ia[0], ib[0]), l)); kanten.append(((ia[1], ib[1]), l))
        kanten.append(((hi_a, hi_b), None))
        # Quaderecken als eigene Stuecke am Rand
        ecke_l = ecke_r = None
        if ecken:
            if "links" in ecken and sa == 0.0:
                st, bd, ph = ecken["links"]; ecke_l = (st, bd, 0.70 if (lage + ph) % 2 == 0 else 0.40)
            if "rechts" in ecken and sb == laenge:
                st, bd, ph = ecken["rechts"]; ecke_r = (st, bd, 0.70 if (lage + ph) % 2 == 0 else 0.40)
        for i in range(0, len(kanten), 2):
            (la, lb), _ = kanten[i]; (ra, rb), _ = kanten[i + 1]
            if max(ra - la, rb - lb) < 1e-5: continue
            stuecke = []
            l_a, l_b, r_a, r_b = la, lb, ra, rb
            if ecke_l and i == 0 and l_a == sa:
                w = min(ecke_l[2], r_a - l_a, r_b - l_b)
                stuecke.append((ecke_l[0], ecke_l[1], l_a, l_b, l_a + w, l_b + w)); l_a += w; l_b += w
            if ecke_r and i == len(kanten) - 2 and r_a == sb:
                w = min(ecke_r[2], r_a - l_a, r_b - l_b)
                stuecke.append((ecke_r[0], ecke_r[1], r_a - w, r_b - w, r_a, r_b)); r_a -= w; r_b -= w
            if max(r_a - l_a, r_b - l_b) > 1e-5: stuecke.append((stoff, band, l_a, l_b, r_a, r_b))
            for st, bd, pa, pb, qa, qb in stuecke:
                pts = [P(pa, za), P(qa, za), P(qb, zb), P(pb, zb)]
                uvs = [uv(pa, za, bd, basis, kurs, versatz), uv(qa, za, bd, basis, kurs, versatz), uv(qb, zb, bd, basis, kurs, versatz), uv(pb, zb, bd, basis, kurs, versatz)]
                mitte = (pts[0] + pts[2]) / 2
                if (pts[1] - pts[0]).length < 1e-6: pts, uvs = [pts[0], pts[2], pts[3]], [uvs[0], uvs[2], uvs[3]]
                elif (pts[2] - pts[3]).length < 1e-6: pts, uvs = pts[:3], uvs[:3]
                teil.vieleck(st, pts, uvs, zu=mitte + n, farbe=farbe)
        # Laibungen der aktiven Loecher in dieser Lage
        if laibung:
            lst, lbd = laibung
            for ia, ib, l in aktiv:
                if not l.laibung: continue
                for (sa_, sb_), innen in (((ia[0], ib[0]), +1), ((ia[1], ib[1]), -1)):
                    a, b = P(sa_, za), P(sb_, zb)
                    pts = [a, b, b - n * dicke, a - n * dicke]
                    lang = (b - a).length
                    uvs = [(0.0, teil.baender[lbd][1]), (lang / teil.baender[lbd][2], teil.baender[lbd][1]),
                           (lang / teil.baender[lbd][2], teil.baender[lbd][1] - 0.6 * (teil.baender[lbd][1] - teil.baender[lbd][0])),
                           (0.0, teil.baender[lbd][1] - 0.6 * (teil.baender[lbd][1] - teil.baender[lbd][0]))]
                    ziel = P((ia[0] + ia[1]) / 2, zm) - n * dicke / 2
                    teil.vieleck(lst, pts, uvs, zu=ziel)
    if laibung:
        lst, lbd = laibung
        for l in loecher:
            if not l.laibung: continue
            mitte = P((l.s0 + l.s1) / 2, (l.z0 + l.z1) / 2) - n * dicke / 2
            if l.sohlbank and l.z0 > z0 + 1e-4:       # Sohlbank
                teil.rechteck(lst, P(l.s0, l.z0), eu, -n, l.s1 - l.s0, dicke, lbd, zu=mitte)
            if not l.bogen and l.z1 < z1 - 1e-4:      # Sturz
                teil.rechteck(lst, P(l.s0, l.z1), eu, -n, l.s1 - l.s0, dicke, lbd, zu=mitte)


def bogenring(teil, stoff, band, o, eu, n, loch, breite=0.22, vor=0.04, teile=12, kaempfer=True):
    """Gewaende eines Rundbogens aus Quadern: Ring um die Oeffnung, steht `vor` vor der Wand, mit Kaempferplatten."""
    o = Vector((o[0], o[1], 0.0)); eu = Vector((eu[0], eu[1], 0.0)).normalized(); n = Vector((n[0], n[1], 0.0)).normalized()
    m = (loch.s0 + loch.s1) / 2
    P = lambda s, z, d=vor: o + eu * s + EZ * z + n * d
    r0, r1 = loch.r, loch.r + breite
    a0, a1, mm = teil.baender[band]
    for i in range(teile):
        w0, w1 = math.pi * i / teile, math.pi * (i + 1) / teile
        pi0 = P(m + r0 * math.cos(w0), loch.zk + r0 * math.sin(w0)); pi1 = P(m + r0 * math.cos(w1), loch.zk + r0 * math.sin(w1))
        pa0 = P(m + r1 * math.cos(w0), loch.zk + r1 * math.sin(w0)); pa1 = P(m + r1 * math.cos(w1), loch.zk + r1 * math.sin(w1))
        u = i * 0.13
        teil.vieleck(stoff, [pi0, pa0, pa1, pi1], [(u, a1), (u, a0), (u + 0.12, a0), (u + 0.12, a1)], zu=(pi0 + pa1) / 2 + n)
        teil.vieleck(stoff, [pa0, pa1, pa1 - n * vor, pa0 - n * vor], [(u, a1), (u + 0.1, a1), (u + 0.1, a1 - 0.2 * (a1 - a0)), (u, a1 - 0.2 * (a1 - a0))], zu=(pa0 + pa1) / 2 + (pa0 + pa1 - 2 * P(m, loch.zk)).normalized())
    # senkrechte Gewaende bis zur Sohlbank bzw. zum Boden
    for s, aussen in ((loch.s0, -1), (loch.s1, 1)):
        sl = s - breite if aussen < 0 else s
        teil.rechteck(stoff, P(sl, loch.z0), eu, EZ, breite, loch.zk - loch.z0, band, laengs="v", zu=P(sl + breite / 2, (loch.z0 + loch.zk) / 2) + n)
        aussen_s = sl if aussen < 0 else sl + breite
        teil.rechteck(stoff, P(aussen_s, loch.z0, 0.0), EZ, n, loch.zk - loch.z0, vor, band, zu=P(aussen_s, (loch.z0 + loch.zk) / 2) + eu * aussen)
    if kaempfer:
        for s0 in (loch.s0 - breite - 0.03, loch.s1 - 0.01):
            a = o + eu * s0 + EZ * (loch.zk - 0.10)
            b = o + eu * (s0 + breite + 0.04) + EZ * loch.zk + n * (vor + 0.03)
            mn = Vector((min(a.x, b.x), min(a.y, b.y), a.z)); mx = Vector((max(a.x, b.x), max(a.y, b.y), b.z))
            teil.kasten(stoff, mn, mx, band, ohne=("-z",))


def gesims(teil, stoff, band, punkte, z, hoehe=0.18, vor=0.12, mitte=None):
    """Umlaufendes Gesims (geschlossener Linienzug in der Wandebene, im Uhrzeigersinn egal): Vorderseite und Oberseite."""
    mitte = Vector(mitte) if mitte else sum((Vector((p[0], p[1], 0)) for p in punkte), Vector()) / len(punkte)
    for i in range(len(punkte)):
        a = Vector((*punkte[i], z)); b = Vector((*punkte[(i + 1) % len(punkte)], z))
        rand = (b - a).normalized(); n = rand.cross(EZ)
        if n.dot(Vector((a.x, a.y, 0)) - Vector((mitte.x, mitte.y, 0))) < 0: n = -n
        a2, b2 = a - rand * vor + n * vor, b + rand * vor + n * vor
        teil.rechteck(stoff, a2, (b2 - a2), EZ, (b2 - a2).length, hoehe, band, zu=(a2 + b2) / 2 + n + EZ * hoehe / 2)
        teil.vieleck(stoff, [a + EZ * hoehe, b + EZ * hoehe, b2 + EZ * hoehe, a2 + EZ * hoehe],
                     [(0, teil.baender[band][1]), (1, teil.baender[band][1]), (1, teil.baender[band][0]), (0, teil.baender[band][0])], zu=(a + b2) / 2 + EZ * 2)
        teil.vieleck(stoff, [a, b, b2, a2], [(0, teil.baender[band][1]), (1, teil.baender[band][1]), (1, teil.baender[band][0]), (0, teil.baender[band][0])], zu=(a + b2) / 2 - EZ * 2)


def innenwand(teil, stoff, a, b, n, z0, z1, band, loecher=(), zbasis=None, saat=1, **kw):
    """Innenflaeche einer Wand von a=(x, y) nach b=(x, y), Sichtseite n (in den Raum). `loecher`: Liste (a0, a1, z0, z1, bogen)
    mit a0/a1 als Weltkoordinate entlang der Wand (x bei Waenden quer zu Y, y bei Waenden quer zu X)."""
    a3, b3 = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    eu = (b3 - a3).normalized()
    laenge = (b3 - a3).length
    start = a[0] if abs(eu.x) > 0.5 else a[1]
    richtung = eu.x if abs(eu.x) > 0.5 else eu.y
    ls = []
    for (q0, q1, za, zb, bogen) in loecher:
        s0, s1 = sorted(((q0 - start) * richtung, (q1 - start) * richtung))
        ls.append(Loch(s0, s1, za, zb, bogen=bogen))
    mauer(teil, stoff, a, (eu.x, eu.y), n, laenge, z0, z1, band, ls, zbasis=z0 if zbasis is None else zbasis, saat=saat, **kw)


def boden(teil, stoff, band, x0, x1, y0, y1, z, oben=True, loecher=(), laengs="x", saat=3):
    """Waagerechte Flaeche (Boden nach oben, Decke nach unten) mit rechteckigen Loechern (x0, x1, y0, y1). Quer zur Laufrichtung
    der Textur in Streifen von der Hoehe des Bandes (keine Streckung), jeder Streifen beginnt an anderer Stelle des Bandes."""
    v0, v1, m = teil.baender[band]
    kurs = (v1 - v0) * m
    q0, q1 = (y0, y1) if laengs == "x" else (x0, x1)
    streifen = {q0 + k * kurs for k in range(1, int((q1 - q0) / kurs) + 1) if q0 + k * kurs < q1 - 1e-4}
    xs = sorted({x0, x1} | {v for l in loecher for v in (l[0], l[1]) if x0 < v < x1} | (set() if laengs == "x" else streifen))
    ys = sorted({y0, y1} | {v for l in loecher for v in (l[2], l[3]) if y0 < v < y1} | (streifen if laengs == "x" else set()))
    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            xm, ym = (xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2
            if any(l[0] < xm < l[1] and l[2] < ym < l[3] for l in loecher): continue
            q = ym if laengs == "x" else xm
            k = math.floor((q - q0) / kurs + 1e-9)
            basis = q0 + k * kurs
            a = ((ys[j] if laengs == "x" else xs[i]) - basis) / kurs
            b = ((ys[j + 1] if laengs == "x" else xs[i + 1]) - basis) / kurs
            u0 = _lagen_versatz(k, saat) * 3
            if laengs == "x":
                teil.rechteck(stoff, (xs[i], ys[j], z), (1, 0, 0), (0, 1, 0), xs[i + 1] - xs[i], ys[j + 1] - ys[j], band, u0=u0 + xs[i] / m,
                              anteil=(a, b), zu=(xm, ym, z + (1 if oben else -1)))
            else:
                teil.rechteck(stoff, (xs[i], ys[j], z), (0, 1, 0), (1, 0, 0), ys[j + 1] - ys[j], xs[i + 1] - xs[i], band, laengs="u", u0=u0 + ys[j] / m,
                              anteil=(a, b), zu=(xm, ym, z + (1 if oben else -1)))


def tuerrahmen(teil, stoff, band, mitte_unten, eu, n, breite, hoehe, rahmen=0.16, vor=0.03, bogen=False):
    """Gewaende einer Innentuer auf der Wandflaeche: zwei Pfosten und ein Sturz (bzw. flacher Bogen) aus Sandstein."""
    c = Vector(mitte_unten); eu = Vector(eu).normalized(); n = Vector(n).normalized()
    for seite in (-1, 1):
        p = c + eu * seite * (breite / 2 + rahmen / 2)
        mn = p - eu * rahmen / 2; mx = p + eu * rahmen / 2 + n * vor + EZ * hoehe
        teil.kasten(stoff, (min(mn.x, mx.x), min(mn.y, mx.y), c.z), (max(mn.x, mx.x), max(mn.y, mx.y), c.z + hoehe), band, ohne=("-z",))
    p0 = c - eu * (breite / 2 + rahmen) + EZ * hoehe; p1 = c + eu * (breite / 2 + rahmen) + EZ * (hoehe + rahmen * 1.4) + n * vor
    teil.kasten(stoff, (min(p0.x, p1.x), min(p0.y, p1.y), p0.z), (max(p0.x, p1.x), max(p0.y, p1.y), p1.z), band)


def durchgang(teil, stoff, band, mitte_unten, eu, n, breite, hoehe, tiefe):
    """Laibung eines Durchgangs durch eine Wand: zwei Seiten und der Sturz, `tiefe` entgegen n (in die Wand hinein)."""
    c = Vector(mitte_unten); eu = Vector(eu).normalized(); n = Vector(n).normalized()
    for seite in (-1, 1):
        p = c + eu * seite * breite / 2
        teil.rechteck(stoff, p, -n, EZ, tiefe, hoehe, band, zu=c - n * tiefe / 2 + EZ * hoehe / 2)
    teil.rechteck(stoff, c - eu * breite / 2 + EZ * hoehe, eu, -n, breite, tiefe, band, zu=c - n * tiefe / 2)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Fenster, Scheiben, Tueren
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def wandpunkt(wand, s_welt, z, raus=0.0):
    """Punkt auf der Aussenflaeche einer Aussenwand (s in Weltkoordinaten entlang der Wand), um `raus` nach aussen versetzt."""
    (ox, oy), (ex, ey), _, (nx, ny) = M.WAENDE[wand]
    start = ox if ex else oy
    s = s_welt - start
    return Vector((ox + ex * s + nx * raus, oy + ey * s + ny * raus, z))


def fensterstock(teil, stoff, band, wand, a0, a1, z0, z1, bogen=False, kreuz=True, tiefe=None):
    """Holzrahmen 5 x 7 cm in der Oeffnung (Mitte der Wand) mit Kreuz 4,5 x 5,5 cm, wie beim Wohnhaus; beim Rundbogen ein
    Rahmen bis zum Kaempfer, darueber der Bogen aus Stuecken und der Pfosten bis in den Scheitel."""
    (_, _, _, nn) = M.WAENDE[wand]
    d = (tiefe if tiefe is not None else M.WAND_DICKE[wand]) / 2
    n3 = Vector((nn[0], nn[1], 0))
    P = lambda s, z: wandpunkt(wand, s, z, -d)
    r = (a1 - a0) / 2
    zk = z1 - r if bogen else z1
    sm = (a0 + a1) / 2
    stuecke = [(P(a0, z0 + 0.025), P(a1, z0 + 0.025)), (P(a0 + 0.025, z0 + 0.05), P(a0 + 0.025, zk)), (P(a1 - 0.025, z0 + 0.05), P(a1 - 0.025, zk))]
    if bogen:
        teile = 8
        for i in range(teile):
            w0, w1 = math.pi * i / teile, math.pi * (i + 1) / teile
            stuecke.append((P(sm + (r - 0.025) * math.cos(w0), zk + (r - 0.025) * math.sin(w0)), P(sm + (r - 0.025) * math.cos(w1), zk + (r - 0.025) * math.sin(w1))))
        stuecke.append((P(a0 + 0.05, zk), P(a1 - 0.05, zk)))
    else:
        stuecke.append((P(a0, z1 - 0.025), P(a1, z1 - 0.025)))
    for a, b in stuecke:
        teil.stab(stoff, a, b, 0.07, band, breite=0.05, oben=n3)
    if kreuz:
        teil.stab(stoff, P(sm, z0 + 0.05), P(sm, z1 - 0.05), 0.055, band, breite=0.045, oben=n3)
        zm = (z0 + zk) / 2 if bogen else (z0 + z1) / 2
        teil.stab(stoff, P(a0 + 0.05, zm), P(a1 - 0.05, zm), 0.055, band, breite=0.045, oben=n3)


def scheibe(teil, stoff, wand, a0, a1, z0, z1, raus, **kw):
    """Scheibe; UV in Metern ab der linken unteren Ecke (der Butzen-Shader teilt danach; innen und aussen gleich)."""
    b, hoch = a1 - a0, z1 - z0
    pts = [wandpunkt(wand, a0, z0, raus), wandpunkt(wand, a1, z0, raus), wandpunkt(wand, a1, z1, raus), wandpunkt(wand, a0, z1, raus)]
    teil.vieleck(stoff, pts, [(0, 1), (b, 1), (b, 1 - hoch), (0, 1 - hoch)], **kw)


def bretttuer(teil, stoff, band, eisen, eisen_band, mitte_unten, eu, n, breite, hoehe, dicke=0.07, bogen_r=None, bretter=5, beschlag=True, eisen_ub=None):
    """Tuerblatt aus senkrechten Brettern (beide Seiten), mit Langbaendern und Ringgriff aus Eisen. `mitte_unten`: Mitte der
    Unterkante; eu entlang des Blatts, n Sichtseite aussen. `bogen_r`: oben rund (Rundbogentuer)."""
    c = Vector(mitte_unten); eu = Vector(eu).normalized(); n = Vector(n).normalized()
    a0, a1, mm = teil.baender[band]
    b = breite / bretter
    zk = hoehe - (bogen_r or 0)
    for seite in (1, -1):
        for i in range(bretter):
            s0, s1 = -breite / 2 + i * b, -breite / 2 + (i + 1) * b
            def top(s):
                if not bogen_r: return hoehe
                w = s / bogen_r
                return zk + bogen_r * math.sqrt(max(1 - w * w, 0)) if abs(w) <= 1 else zk
            pts = [c + eu * s0 + n * seite * dicke / 2, c + eu * s1 + n * seite * dicke / 2,
                   c + eu * s1 + EZ * top(s1) + n * seite * dicke / 2, c + eu * s0 + EZ * top(s0) + n * seite * dicke / 2]
            u = (i * 0.17) % 1
            uvs = [(u, a1), (u, a0), (u + top(s1) / mm, a0), (u + top(s0) / mm, a1)]
            teil.vieleck(stoff, pts, uvs, zu=c + EZ * 1 + n * seite * 3)
    # Kanten
    teil.rechteck(stoff, c - eu * breite / 2 - n * dicke / 2, EZ, n, zk, dicke, band, zu=c - eu * 3 + EZ)
    teil.rechteck(stoff, c + eu * breite / 2 - n * dicke / 2, EZ, n, zk, dicke, band, zu=c + eu * 3 + EZ)
    if beschlag:
        for z in (0.35, hoehe * 0.55, max(zk - 0.25, 0.8)):
            for seite in (1,):
                p = c + EZ * z + n * (dicke / 2 + 0.006)
                teil.rechteck(eisen, p - eu * breite / 2, eu, EZ, breite * 0.72, 0.05, eisen_band, zu=p + n, u_bereich=eisen_ub)
        # Ringgriff (aussen und innen) und Schlossblech
        for seite in (1, -1):
            g = c + eu * (breite / 2 - 0.16) + EZ * 1.05 + n * seite * (dicke / 2 + 0.008)
            teil.rechteck(eisen, g - eu * 0.05 - EZ * 0.08, eu, EZ, 0.10, 0.16, eisen_band, zu=g + n * seite, u_bereich=eisen_ub)
            teil.stab(eisen, g + n * seite * 0.035 - EZ * 0.02, g + n * seite * 0.035 - EZ * 0.12, 0.014, eisen_band, u_bereich=eisen_ub)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Treppen, Gelaender
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def gerade_treppe(teil, stoff, band, start, richtung, quer, breite, stufen, steig, auftritt, z0, wange=None, block=False, unten_zu=False):
    """Gerade Treppe: `stufen` Steigungen ab z0, die erste Stufe beginnt bei `start` (Mitte der Antrittskante), laeuft in
    `richtung`. Tritt- und Setzstufen; `wange`: (Stoff, Band) fuer die Wangen beiderseits; `block`: massiv bis zum Boden (Stein)."""
    s = Vector((start[0], start[1], 0.0)); r = Vector(richtung).normalized(); q = Vector(quer).normalized()
    for i in range(stufen):
        zu = z0 + i * steig; zo = zu + steig
        p = s + r * (i * auftritt)
        # Setzstufe
        teil.rechteck(stoff, p - q * breite / 2 + EZ * zu, q, EZ, breite, steig, band, zu=p - r + EZ * (zu + steig / 2))
        teil.rechteck(stoff, p - q * breite / 2 + EZ * zo, q, r, breite, auftritt if i < stufen - 1 else 0.02, band, zu=p + EZ * (zo + 1))
        if block:
            for seite in (-1, 1):
                k = p + q * seite * breite / 2
                teil.rechteck(stoff, k + EZ * z0, r, EZ, auftritt, zo - z0, band, zu=k + q * seite + EZ * zo)
    if wange:
        ws, wb = wange
        lang = stufen * auftritt
        for seite in (-1, 1):
            k = s + q * seite * (breite / 2 + 0.03)
            a = k + EZ * (z0 - 0.15); b = k + r * lang + EZ * (z0 + stufen * steig - 0.15)
            n = q * seite
            pts = [a, b, b + EZ * 0.40, a + EZ * 0.40]
            teil.vieleck(ws, pts, [(0, teil.baender[wb][1]), (lang / 2, teil.baender[wb][1]), (lang / 2, teil.baender[wb][0]), (0, teil.baender[wb][0])], zu=(a + b) / 2 + n)
            teil.vieleck(ws, pts, [(0, teil.baender[wb][1]), (lang / 2, teil.baender[wb][1]), (lang / 2, teil.baender[wb][0]), (0, teil.baender[wb][0])], zu=(a + b) / 2 - n)


def gelaender(teil, stoff, band, a, b, hoehe=0.95, abstand=0.14, docke=0.05, handlauf=0.08):
    """Gelaender von a nach b (Fusspunkte, auch schraeg): Handlauf, Fussleiste, gedrechselte Docken (vierkantig)."""
    a, b = Vector(a), Vector(b)
    teil.stab(stoff, a + EZ * hoehe, b + EZ * hoehe, handlauf, band, breite=handlauf * 1.2)
    teil.stab(stoff, a + EZ * 0.05, b + EZ * 0.05, 0.06, band, breite=0.08)
    lang = (b - a).length
    n = max(int(lang / abstand), 1)
    for i in range(1, n):
        p = a + (b - a) * (i / n)
        teil.stab(stoff, p + EZ * 0.08, p + EZ * (hoehe - 0.04), docke, band)
    for p in (a, b):
        teil.stab(stoff, p, p + EZ * (hoehe + 0.12), 0.11, band, kappen=True)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Moebel
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def stuhl(teil, stoff, band, punkt, blick_grad, sitz=0.44, breite=0.62, tiefe=0.50, vorn=0.18, lehne=1.05, polster=None, armlehnen=False):
    """Lehnstuhl um den Sitzpunkt `punkt` (Boden unter der Huefte der Sitzenden, Regel des Figuren-Baukastens), Blick in Grad
    (0 = +Y, 90 = +X). Die Sitzflaeche reicht `vorn` vor den Sitzpunkt und `tiefe - vorn` dahinter; Lehne hinten.
    `polster`: (Stoff, Band) fuer das Sitzkissen."""
    f = Vector(punkt)
    w = math.radians(blick_grad)
    vor = Vector((math.sin(w), math.cos(w), 0)); quer = Vector((math.cos(w), -math.sin(w), 0))
    c = f + vor * (vorn - tiefe / 2)              # Mitte der Sitzflaeche
    def box(mitte, gr_q, gr_v, gr_z, z0, st=stoff, bd=band):
        m = c + quer * mitte[0] + vor * mitte[1] + EZ * z0
        ecken = [m + quer * sx * gr_q / 2 + vor * sy * gr_v / 2 for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        for i in range(4):
            a, b = ecken[i], ecken[(i + 1) % 4]
            teil.vieleck(st, [a, b, b + EZ * gr_z, a + EZ * gr_z], [(0, teil.baender[bd][1]), (gr_z / 2, teil.baender[bd][1]), (gr_z / 2, teil.baender[bd][0]), (0, teil.baender[bd][0])], zu=m + EZ * gr_z / 2 + (a + b - 2 * m) * 3)
        teil.vieleck(st, [e + EZ * gr_z for e in ecken], [(0, teil.baender[bd][1]), (0.2, teil.baender[bd][1]), (0.2, teil.baender[bd][0]), (0, teil.baender[bd][0])], zu=m + EZ * (gr_z + 1))
    brett = sitz - (0.035 if polster else 0.0)       # Oberkante des Sitzbretts: mit Polster liegt das Kissen obenauf, Oberkante = sitz
    # Beine
    for sx in (-1, 1):
        for sy in (-1, 1):
            box((sx * (breite / 2 - 0.04), sy * (tiefe / 2 - 0.04)), 0.046, 0.046, brett - 0.052 if sy > 0 else lehne, 0.0)   # 2 mm Fuge: eigenes Teil
    box((0, 0), breite, tiefe, 0.05, brett - 0.05)
    box((0, -tiefe / 2 + 0.03), breite - 0.06, 0.04, lehne - sitz - 0.10, sitz + 0.05)
    if polster:
        st, bd = polster
        box((0, 0.01), breite - 0.08, tiefe - 0.08, 0.035, brett, st, bd)
    if armlehnen:
        for sx in (-1, 1):
            box((sx * (breite / 2 - 0.03), 0), 0.06, tiefe, 0.05, sitz + 0.24)


def tisch(teil, stoff, band, x0, x1, y0, y1, hoehe=0.76, platte=0.05, zarge=0.10, bein=0.08, z0=0.0, beine=None, knieraum=None):
    """Tisch mit Platte, Zarge und Beinen (achsparallel). Unterkante der Zarge = hoehe - platte - zarge (mit `knieraum`: genau dort).
    Platte, Zarge und Beine beruehren sich nicht (2 mm Fuge): so bleiben es getrennte Teile, und die Sitzpruefung (Huellen je Teil)
    zaehlt den Raum unter der Platte nicht als Moebel."""
    if knieraum is not None: zarge = hoehe - platte - knieraum
    teil.kasten(stoff, (x0, y0, z0 + hoehe - platte), (x1, y1, z0 + hoehe), band)
    zz = z0 + hoehe - platte - zarge
    e = 0.05
    if zarge > 0.02:
        teil.kasten(stoff, (x0 + e, y0 + e, zz), (x1 - e, y1 - e, z0 + hoehe - platte - 0.002), band, ohne=("+z",))
    stellen = beine or [(x0 + e, y0 + e), (x1 - e - bein, y0 + e), (x0 + e, y1 - e - bein), (x1 - e - bein, y1 - e - bein)]
    for bx, by in stellen:
        teil.kasten(stoff, (bx + 0.002, by + 0.002, z0), (bx + bein - 0.002, by + bein - 0.002, zz - 0.002), band, ohne=("+z", "-z"))


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Blender: Stoffe, UV2, AO, Anker, Export, Bilder
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def stoff(name, blatt=None, farbe=(0.5, 0.5, 0.5), alpha=1.0, schere=False, rauheit=0.8, leuchten=0.0):
    """Material fuer Kontrollbild und Export (das Spiel setzt die Trim-Texturen beim Laden, Stadtansicht.Neubau.cs)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(nd for nd in m.node_tree.nodes if nd.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*farbe, 1.0)
    bsdf.inputs["Roughness"].default_value = rauheit
    bsdf.inputs["Alpha"].default_value = alpha
    if leuchten:
        bsdf.inputs["Emission Color"].default_value = (*farbe, 1.0)
        bsdf.inputs["Emission Strength"].default_value = leuchten
    if alpha < 1.0 or schere:
        m.surface_render_method = "DITHERED" if schere else "BLENDED"
    if blatt:
        bild = bpy.data.images.load(os.path.join(ZIEL_TEXTUR, f"{blatt}_albedo.png"), check_existing=True)
        tex = m.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = bild; tex.name = "Trim"
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if schere: m.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        if name == "Bretter":
            # nur fuers Kontrollbild: wo die Eckenfarbe Rot ist (Akzent), faerbt das Spiel mit der Stadtfarbe; hier ein Stadtrot
            col = m.node_tree.nodes.new("ShaderNodeVertexColor"); col.layer_name = "Col"
            sep = m.node_tree.nodes.new("ShaderNodeSeparateColor")
            mix = m.node_tree.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
            m.node_tree.links.new(col.outputs["Color"], sep.inputs[0])
            m.node_tree.links.new(sep.outputs[0], mix.inputs["Factor"])
            m.node_tree.links.new(tex.outputs["Color"], mix.inputs[6])
            mix.inputs[7].default_value = (0.75, 0.16, 0.12, 1)
            m.node_tree.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    return m


def stoffe_aussen():
    b = "rathaus_trim"
    return {
        "Schiefer": stoff("Schiefer", b), "Schieferkante": stoff("Schieferkante", b, schere=True), "Fachwerk": stoff("Fachwerk", b),
        "Putz": stoff("Putz", b), "Sandstein": stoff("Sandstein", b), "Bruchstein": stoff("Bruchstein", b), "Bretter": stoff("Bretter", b),
        "Fenster": stoff("Fenster", farbe=(0.09, 0.11, 0.14), rauheit=0.2), "Bleinetz": stoff("Bleinetz", farbe=(0.1, 0.1, 0.11), alpha=0.5),
        "Firstblech": stoff("Firstblech", b), "Eisen": stoff("Eisen", farbe=(0.12, 0.12, 0.13), rauheit=0.5),
        "Holz": stoff("Holz", farbe=(0.30, 0.22, 0.15)), "Hausfarbe": stoff("Hausfarbe", farbe=(0.55, 0.12, 0.10)),
    }


def stoffe_innen():
    b = "rathaus_innen_trim"
    return {"Innen": stoff("Innen", b), "Innenglas": stoff("Innenglas", farbe=(0.55, 0.62, 0.6), alpha=0.16, rauheit=0.15),
            "Stroh": stoff("Stroh", b), "Hausfarbe": stoff("Hausfarbe", farbe=(0.55, 0.12, 0.10))}


def verschmelzen(obj):
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bm.to_mesh(obj.data); bm.free()


def atlas_platz(gruppe, modell):
    daten = json.load(open(os.path.join(HIER, "ao_atlas.json"), encoding="utf-8"))[gruppe]
    nummer = daten["plaetze"][modell]
    n = daten["zellen"]
    return daten["datei"], daten["kante"], n, nummer % n, nummer // n


def uv2_und_ao(gruppen, gruppe, modell, mit_ao=True, proben=64):
    """UV2 (Smart UV Project, Rand 0,02) aller Objekte in eine Zelle des AO-Atlas der Gruppe und die Verdeckung dorthin backen."""
    import numpy as np
    datei, kante, n, spalte, zeile = atlas_platz(gruppe, modell)
    zelle = kante // n
    objekte = [o for objs, _ in gruppen for o in objs]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objekte:
        o.hide_set(False); o.hide_viewport = False
        verschmelzen(o)
        uv2 = o.data.uv_layers.new(name="UV2")
        o.data.uv_layers.active = uv2
        o.select_set(True)
    bpy.context.view_layer.objects.active = objekte[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    if mit_ao:
        bild = bpy.data.images.new("AO-Zelle", zelle, zelle, alpha=False)
        bild.colorspace_settings.name = "Non-Color"
        bild.generated_color = (1, 1, 1, 1)
        szene = bpy.context.scene
        szene.render.engine = "CYCLES"
        szene.cycles.device = "CPU"
        szene.cycles.samples = proben
        szene.render.bake.margin = 6
        szene.render.bake.use_clear = False
        if szene.world is None: szene.world = bpy.data.worlds.new("Welt")
        szene.world.light_settings.distance = 1.1
        for objs, verdecker in gruppen:
            for o in bpy.data.objects:
                o.hide_render = o not in objs and o not in verdecker
            for o in objs:
                for mt in o.data.materials:
                    knoten = mt.node_tree.nodes
                    ziel = knoten.get("AO-Ziel") or knoten.new("ShaderNodeTexImage")
                    ziel.name = "AO-Ziel"; ziel.image = bild
                    knoten.active = ziel
            for o in objs:
                bpy.ops.object.select_all(action="DESELECT")
                o.select_set(True); bpy.context.view_layer.objects.active = o
                bpy.ops.object.bake(type="AO", uv_layer="UV2")
        feld_ = np.empty(zelle * zelle * 4, dtype=np.float32); bild.pixels.foreach_get(feld_)
        grau = feld_.reshape(zelle, zelle, 4)[..., 0]
        grau = np.clip((grau + np.roll(grau, 1, 0) + np.roll(grau, -1, 0) + np.roll(grau, 1, 1) + np.roll(grau, -1, 1)) / 5, 0, 1)
        pfad = os.path.join(ZIEL_TEXTUR, datei)
        if os.path.exists(pfad):
            alt = bpy.data.images.load(pfad); alt.colorspace_settings.name = "Non-Color"
            ganz = np.empty(alt.size[0] * alt.size[1] * 4, dtype=np.float32); alt.pixels.foreach_get(ganz)
            ganz = ganz.reshape(alt.size[1], alt.size[0], 4).copy(); bpy.data.images.remove(alt)
            if ganz.shape[0] != kante: ganz = np.ones((kante, kante, 4), dtype=np.float32)
        else:
            ganz = np.ones((kante, kante, 4), dtype=np.float32)
        ganz[zeile * zelle:(zeile + 1) * zelle, spalte * zelle:(spalte + 1) * zelle, :3] = grau[..., None]
        ganz[..., 3] = 1
        atlas = bpy.data.images.new("AO-Atlas", kante, kante, alpha=False)
        atlas.colorspace_settings.name = "Non-Color"
        atlas.pixels.foreach_set(ganz.ravel())
        atlas.filepath_raw = pfad; atlas.file_format = "PNG"; atlas.save()
        print("geschrieben", pfad, "Zelle", spalte, zeile)
    rand = 3.0 / zelle
    for o in objekte:
        kanal = o.data.uv_layers["UV2"]
        for d in kanal.data:
            d.uv = ((spalte + rand + d.uv[0] * (1 - 2 * rand)) / n, (zeile + rand + d.uv[1] * (1 - 2 * rand)) / n)
        for mt in o.data.materials:
            knoten = mt.node_tree.nodes
            if knoten.get("AO-Ziel"): knoten.remove(knoten["AO-Ziel"])
            if knoten.get("Trim"): knoten.active = knoten["Trim"]
        o.data.uv_layers.active = o.data.uv_layers["UVMap"]
    for o in bpy.data.objects: o.hide_render = False


BLICK = {"nord": 0.0, "west": math.pi / 2, "sued": math.pi, "ost": -math.pi / 2}


def anker(name, ort, blick=None, grad=None, eltern=None, **extras):
    """Leerer Knoten. `blick`: Himmelsrichtung (nord = +Y) oder `grad` (0 = +Y, 90 = +X): im Spiel schaut -Z dorthin.
    `extras` landen als glTF-extras am Knoten."""
    o = bpy.data.objects.new(name, None)
    o.location = ort
    if blick: o.rotation_euler = (0, 0, BLICK[blick])
    if grad is not None: o.rotation_euler = (0, 0, -math.radians(grad))
    for k, v in extras.items(): o[k] = v
    bpy.context.scene.collection.objects.link(o)
    if eltern is not None: o.parent = eltern
    return o


def exportiere(objekte, datei):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objekte: o.select_set(True)
    os.makedirs(os.path.dirname(datei), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=datei, export_format="GLB", use_selection=True, export_yup=True, export_apply=True,
                              export_texcoords=True, export_normals=True, export_vertex_color="ACTIVE", export_all_vertex_colors=False,
                              export_image_format="NONE", export_materials="EXPORT", export_extras=True, export_cameras=False, export_lights=False)
    print("geschrieben", datei, os.path.getsize(datei), "Bytes")


def kontrollbild(datei, auge, ziel, brennweite=35, verbergen=(), proben=32, aufloesung=(1280, 960), sonne=True, welt=0.9):
    szene = bpy.context.scene
    kam = bpy.data.objects.get("Kontrollkamera")
    if kam is None:
        kam = bpy.data.objects.new("Kontrollkamera", bpy.data.cameras.new("Kontrollkamera"))
        szene.collection.objects.link(kam)
        s = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN"))
        s.data.energy = 3.2; s.rotation_euler = (math.radians(50), 0, math.radians(-35))
        szene.collection.objects.link(s)
        if szene.world is None: szene.world = bpy.data.worlds.new("Welt")
        szene.world.use_nodes = True
    hg = next(nd for nd in szene.world.node_tree.nodes if nd.type == "BACKGROUND")
    hg.inputs[0].default_value = (0.62, 0.66, 0.72, 1); hg.inputs[1].default_value = welt
    bpy.data.objects["Sonne"].hide_render = not sonne
    kam.location = auge
    kam.rotation_euler = (Vector(ziel) - Vector(auge)).to_track_quat("-Z", "Y").to_euler()
    kam.data.lens = brennweite
    kam.data.clip_start = 0.05
    szene.camera = kam
    szene.render.engine = "CYCLES"; szene.cycles.device = "CPU"; szene.cycles.samples = proben
    szene.render.resolution_x, szene.render.resolution_y = aufloesung
    szene.render.filepath = datei
    szene.render.image_settings.file_format = "JPEG"
    szene.view_settings.view_transform = "Standard"
    vorher = {o.name: o.hide_render for o in bpy.data.objects}
    for o in bpy.data.objects:
        if o.name in verbergen: o.hide_render = True
    bpy.ops.render.render(write_still=True)
    for o in bpy.data.objects: o.hide_render = vorher.get(o.name, False)


def raum_bauen(raum, teile, anker_fn, opt):
    """Gemeinsamer Ablauf der Raumskripte: Teile zu Objekten, UV2 und Verdeckung (die Raumhuelle teile[0] fuer sich, die Moebel mit der
    Huelle als Verdecker) in die Zelle des Raums, Anker dazu, Export nach rathaus_<raum>_innen.glb. Gibt die Objekte zurueck."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    stoffe = stoffe_innen()
    teile = [t() if callable(t) else t for t in teile]
    print(f"DREIECKE {raum}:", {t.name: t.dreiecke() for t in teile}, "Summe", sum(t.dreiecke() for t in teile))
    objekte = [t.objekt(stoffe, mit_farbe=False) for t in teile]
    rest = [o for o in objekte[1:] if not o.name.startswith("Glas_")]
    uv2_und_ao([([objekte[0]], []), (rest, [objekte[0]])], "rathaus_innen", raum, mit_ao="ohne-ao" not in opt)
    anker = anker_fn()
    exportiere(objekte + anker, os.path.join(ZIEL_MODELL, f"rathaus_{raum}_innen.glb"))
    return objekte


def raum_bilder(o, praefix, raumknoten, decke_z, sichten, uebersicht, mass=(), lichter=(), dazu=("rathaus.glb",)):
    """Kontrollbilder (Blender, kein Spielbild): je Sicht (Name, Auge, Ziel; Auge/Ziel auch als Ankername) mit Brennweite fuer 66 Grad,
    die erste auch mit Massfiguren (`mass`: (x, y, z, hoehe, breite)), dazu die Uebersicht ohne Decke."""
    os.makedirs(o, exist_ok=True)
    for datei in dazu:
        pfad = os.path.join(ZIEL_MODELL, datei)
        if os.path.exists(pfad): bpy.ops.import_scene.gltf(filepath=pfad)
    for ob in bpy.data.objects:
        if ob.name.startswith("Scheiben"): ob.hide_render = True
    if mass:
        mt = Teil("Massfiguren", BAENDER_RATHAUS_INNEN)
        for (x, y, z, h, b) in mass: massfigur(mt, "Mass", (x, y, z), h, b)
        mt.objekt({"Mass": stoff("Mass", farbe=(0.85, 0.35, 0.1))}, mit_farbe=False)
    for i, (p, energie) in enumerate(lichter):
        l = bpy.data.objects.new(f"Kontrolllicht{i}", bpy.data.lights.new(f"Kontrolllicht{i}", "POINT"))
        l.data.energy = energie; l.data.color = (1.0, 0.75, 0.48); l.location = p
        bpy.context.scene.collection.objects.link(l)
    brenn = 18.0 / math.tan(math.radians(66) / 2)
    L = lambda v: tuple(bpy.data.objects[v].location) if isinstance(v, str) else tuple(v)
    for i, (name, auge, ziel) in enumerate(sichten):
        if isinstance(auge, str) and "fov" in bpy.data.objects[auge]:          # Oeffnungswinkel des Ankers wie im Spiel (Godot Fov ist senkrecht)
            fov = float(bpy.data.objects[auge]["fov"])
            brenn = 13.5 / math.tan(math.radians(fov) / 2)                    # 4:3-Bild, 36-mm-Sensor: senkrecht 27 mm
        else:
            brenn = 18.0 / math.tan(math.radians(66) / 2)
        kontrollbild(os.path.join(o, f"{praefix}_{name}.jpg"), L(auge), L(ziel), brennweite=brenn, verbergen=("Massfiguren",), proben=48)
        if i == 0 and mass:
            kontrollbild(os.path.join(o, f"{praefix}_{name}_mass.jpg"), L(auge), L(ziel), brennweite=brenn, proben=32)
    import bmesh
    r = bpy.data.objects[raumknoten]
    bm = bmesh.new(); bm.from_mesh(r.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().z > decke_z - 0.3], context="FACES"); bm.to_mesh(r.data); bm.free()
    weg = tuple(ob.name for ob in bpy.data.objects if ob.name in ("Dach", "Haus", "Scheiben"))
    kontrollbild(os.path.join(o, f"{praefix}_uebersicht.jpg"), uebersicht[0], uebersicht[1], brennweite=22, verbergen=weg, proben=48)


def massfigur(teil, stoff, fuss, hoehe, breite):
    """Grobe Massfigur (Quader mit Kopf) fuer die Kontrollbilder: Mensch 1,75, Oger 2,50."""
    x, y, z = fuss
    teil.kasten(stoff, (x - breite / 2, y - 0.14, z), (x + breite / 2, y + 0.14, z + hoehe * 0.86), "putz" if "putz" in teil.baender else "dielen")
    k = hoehe * 0.07
    teil.kasten(stoff, (x - k, y - k, z + hoehe * 0.86), (x + k, y + k, z + hoehe), "putz" if "putz" in teil.baender else "dielen")
