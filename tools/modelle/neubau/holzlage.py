"""Holzlage einer Fachwerkwand (Nutzer 21.11.2026: „sauberer bauen“): alle Hoelzer einer Wand als EIN Netz in EINER Ebene.

Jedes Holz ist ein konvexes Vieleck in Wandkoordinaten (s entlang der Wand, z hoch), Streben und Baender werden auf ihr Gefach
zugeschnitten (schraeger Zuschnitt genau an den Flaechen der Nachbarhoelzer). Die Vielecke werden zu einer ueberlappungsfreien
Vereinigung zerlegt (A minus B in konvexe Stuecke, nach Rang: Staender, Schwelle und Raehm zuerst, dann Streben, Riegel, Figuren);
gebaut wird je Stueck die Sichtflaeche in der Tiefe `tiefe` vor der Wand und nur an den Aussenkanten der Vereinigung eine
Seitenflaeche bis zur Wand. So gibt es keine offenen Enden, keine deckungsgleichen Flaechen und keine Durchdringungen.
Pruefung: pruefen() (offene Kanten nur an der Wand, keine Ueberlappung, eine Ebene).
"""
import math

EPS = 1e-7


def flaeche(poly):
    return 0.5 * sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))


def ccw(poly):
    return poly if flaeche(poly) >= 0 else poly[::-1]


def schneide(poly, p0, nrm):
    """Behaelt den Teil des konvexen Vielecks mit (x - p0) . nrm >= 0."""
    aus = []
    if not poly: return aus
    w = [(p[0] - p0[0]) * nrm[0] + (p[1] - p0[1]) * nrm[1] for p in poly]
    for i, p in enumerate(poly):
        j = (i + 1) % len(poly)
        if w[i] >= -EPS: aus.append(p)
        if (w[i] >= -EPS) != (w[j] >= -EPS):
            k = w[i] / (w[i] - w[j])
            q = poly[j]
            aus.append((p[0] + k * (q[0] - p[0]), p[1] + k * (q[1] - p[1])))
    sauber = []
    for p in aus:
        if not sauber or abs(p[0] - sauber[-1][0]) + abs(p[1] - sauber[-1][1]) > 1e-6: sauber.append(p)
    while len(sauber) > 1 and abs(sauber[0][0] - sauber[-1][0]) + abs(sauber[0][1] - sauber[-1][1]) < 1e-6: sauber.pop()
    return sauber if len(sauber) >= 3 and abs(flaeche(sauber)) > 1e-6 else []


def kanten(poly):
    """Kanten eines CCW-Vielecks mit Normale nach innen: (p0, n_innen)."""
    aus = []
    for i, a in enumerate(poly):
        b = poly[(i + 1) % len(poly)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        l = math.hypot(dx, dy)
        if l < 1e-9: continue
        aus.append((a, (-dy / l, dx / l)))
    return aus


def schnitt(a, b):
    """Schnitt zweier konvexer Vielecke."""
    for p0, n in kanten(ccw(b)):
        a = schneide(a, p0, n)
        if not a: return []
    return a


def minus(a, b):
    """A minus B (beide konvex) als Liste konvexer Stuecke."""
    if not schnitt(a, b): return [a]
    stuecke, rest = [], a
    for p0, n in kanten(ccw(b)):
        aussen = schneide(rest, p0, (-n[0], -n[1]))
        if aussen: stuecke.append(aussen)
        rest = schneide(rest, p0, n)
        if not rest: break
    return stuecke


def innen(p, poly, rand=1e-7):
    poly = ccw(poly)
    for p0, n in kanten(poly):
        if (p[0] - p0[0]) * n[0] + (p[1] - p0[1]) * n[1] < rand: return False
    return True


def rechteck(a, b, breite):
    """Kantholz von a nach b (Mittellinie) als Vieleck, an beiden Enden um die Breite verlaengert (wird danach zugeschnitten)."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    l = math.hypot(dx, dy)
    ex, ey = dx / l, dy / l
    qx, qy = -ey * breite / 2, ex * breite / 2
    # so weit verlaengert, dass auch ein flach angeschnittenes Ende mit ganzer Breite am Holz anliegt (keine Spitze im Gefach)
    ueber = breite / max(min(abs(ex), abs(ey)), 0.15) if min(abs(ex), abs(ey)) > 1e-6 else breite
    a2 = (a[0] - ex * ueber, a[1] - ey * ueber); b2 = (b[0] + ex * ueber, b[1] + ey * ueber)
    return ccw([(a2[0] - qx, a2[1] - qy), (b2[0] - qx, b2[1] - qy), (b2[0] + qx, b2[1] + qy), (a2[0] + qx, a2[1] + qy)])


def kasten(s0, s1, z0, z1):
    return [(s0, z0), (s1, z0), (s1, z1), (s0, z1)]


class Holzlage:
    """Sammelt die Hoelzer einer Wand und baut sie als ein Netz."""

    def __init__(self, achse=None, umriss=None):
        """achse: s der Spiegelachse – die Lage wird links davon zerlegt und gespiegelt (spiegelgleiche Giebelseiten, Pruefung 2)."""
        self.hoelzer = []    # (Vieleck, Rang, Band, Achse (a, e), Breite)
        self.stuecke = None
        self.achse = achse
        self.ohne_ende = set()   # Hoelzer ohne Endpruefung (Rahmen von Fenster, Laden, Tuer: geschlossene Rechtecke)
        self.umriss = umriss   # konvexes Vieleck der Wandflaeche (z. B. Giebeldreieck): Holzenden am Rand gelten als angeschlossen

    def holz(self, a, b, breite, band="balken_a", rang=1, zuschnitt=None, gerade=False, ende=True):
        """Holz von a nach b (s, z). gerade: genau von a bis b (Schwelle, Raehm, Staender, Riegel), sonst verlaengert und auf
        `zuschnitt` (Vieleck, meist das Gefach) zugeschnitten."""
        if gerade:
            dx, dy = b[0] - a[0], b[1] - a[1]
            l = math.hypot(dx, dy); ex, ey = dx / l, dy / l
            qx, qy = -ey * breite / 2, ex * breite / 2
            poly = ccw([(a[0] - qx, a[1] - qy), (b[0] - qx, b[1] - qy), (b[0] + qx, b[1] + qy), (a[0] + qx, a[1] + qy)])
        else:
            poly = rechteck(a, b, breite)
        if zuschnitt is not None:
            poly = schnitt(poly, ccw(zuschnitt))
        if not poly: return
        l = math.hypot(b[0] - a[0], b[1] - a[1])
        self.hoelzer.append((ccw(poly), rang, band, (a, ((b[0] - a[0]) / l, (b[1] - a[1]) / l)), breite))
        if not ende: self.ohne_ende.add(len(self.hoelzer) - 1)

    def zerlegen(self):
        """Ueberlappungsfreie Stuecke (Vieleck, Holz) nach Rang."""
        if self.stuecke is not None: return self.stuecke
        gesetzt = []
        for holz in sorted(self.hoelzer, key=lambda h: h[1]):
            teile = [holz[0]]
            if self.achse is not None:
                teile = [t for t in [schneide(holz[0], (self.achse, 0.0), (-1.0, 0.0))] if t]
            for vorhanden, _ in gesetzt:
                neu = []
                for t in teile: neu += minus(t, vorhanden)
                teile = [t for t in neu if abs(flaeche(t)) > 2e-6]
                if not teile: break
            gesetzt += [(ccw(t), holz) for t in teile]
        if self.achse is not None:
            ax = self.achse
            gespiegelt = []
            for t, holz in gesetzt:
                poly, rang, band, (a, e), breite = holz
                gespiegelt.append((ccw([(2 * ax - x, z) for x, z in t]), (poly, rang, band, ((2 * ax - a[0], a[1]), (-e[0], e[1])), breite)))
            gesetzt = gesetzt + gespiegelt
        self.stuecke = gesetzt
        return gesetzt

    def frei(self, poly, abstand=0.0):
        """Liegt das Vieleck (z. B. ein Laden) frei von allen Hoelzern (mit Abstand)?"""
        p = ccw(poly)
        if abstand:
            xs, ys = [q[0] for q in p], [q[1] for q in p]
            p = kasten(min(xs) - abstand, max(xs) + abstand, min(ys) - abstand, max(ys) + abstand)
        return not any(schnitt(p, h[0]) for h in self.hoelzer)

    def aussenkanten(self):
        """Kanten der Stuecke, die auf dem Rand der Vereinigung liegen (an T-Stoessen geteilt): (a, b, Stueck-Holz)."""
        stuecke = self.zerlegen()
        punkte = [q for t, _ in stuecke for q in t]
        aus = []
        for i, (t, holz) in enumerate(stuecke):
            for k, a in enumerate(t):
                b = t[(k + 1) % len(t)]
                dx, dy = b[0] - a[0], b[1] - a[1]
                l2 = dx * dx + dy * dy
                if l2 < 1e-12: continue
                ts = {0.0, 1.0}
                for q in punkte:
                    u = ((q[0] - a[0]) * dx + (q[1] - a[1]) * dy) / l2
                    if 1e-6 < u < 1 - 1e-6:
                        px, py = a[0] + u * dx, a[1] + u * dy
                        if abs(px - q[0]) + abs(py - q[1]) < 1e-5: ts.add(u)
                ts = sorted(ts)
                l = math.sqrt(l2)
                ox, oy = dy / l, -dx / l              # nach aussen (CCW)
                for u0, u1 in zip(ts[:-1], ts[1:]):
                    if (u1 - u0) * l < 1e-5: continue
                    m = (a[0] + (u0 + u1) / 2 * dx + ox * 2e-4, a[1] + (u0 + u1) / 2 * dy + oy * 2e-4)
                    if any(j != i and innen(m, s) for j, (s, _) in enumerate(stuecke)): continue
                    aus.append(((a[0] + u0 * dx, a[1] + u0 * dy), (a[0] + u1 * dx, a[1] + u1 * dy), holz))
        return self.zusammenfassen(aus)

    @staticmethod
    def zusammenfassen(kanten):
        """Fasst aneinanderstossende Aussenkanten auf einer Geraden (gleiches Band) zu einer zusammen: eine Seitenflaeche statt vieler
        (spart Dreiecke; die Seitenflaeche bleibt geschlossen, an den Stossstellen liegt nur ein Eckpunkt der Vorderflaeche)."""
        kanten = list(kanten)
        geaendert = True
        while geaendert:
            geaendert = False
            ende = {}
            for i, (a, b, h) in enumerate(kanten):
                ende.setdefault((round(a[0], 5), round(a[1], 5)), []).append(i)
            weg = set()
            for i, (a, b, h) in enumerate(kanten):
                if i in weg: continue
                for j in ende.get((round(b[0], 5), round(b[1], 5)), []):
                    if j == i or j in weg: continue
                    a2, b2, h2 = kanten[j]
                    if h2[2] != h[2]: continue
                    d1 = (b[0] - a[0], b[1] - a[1]); d2 = (b2[0] - a2[0], b2[1] - a2[1])
                    l1, l2 = math.hypot(*d1), math.hypot(*d2)
                    if abs(d1[0] * d2[1] - d1[1] * d2[0]) > 1e-6 * l1 * l2 or d1[0] * d2[0] + d1[1] * d2[1] <= 0: continue
                    kanten[i] = (a, b2, h); weg.add(j); geaendert = True
                    break
            kanten = [k for i, k in enumerate(kanten) if i not in weg]
        return kanten

    def bauen(self, teil, P, n, tiefe=0.05, baender=None, stoff="Fachwerk", farbe=None, wand=None):
        """Baut die Holzlage in `teil`: P(s, z, raus) -> Punkt, n: Normale nach aussen (Vector). wand: (s0, s1) – wo die Hoelzer ueber
        die Wand hinausragen (an Ecken), bekommen sie auch eine Rueckseite (sonst sah man von der Seite in sie hinein)."""
        from mathutils import Vector
        baender = baender or teil.baender
        kw = {} if farbe is None else {"farbe": farbe}
        innen_pkt = lambda s, z: P(s, z, -1.0)
        for poly, holz in self.zerlegen():
            _, _, band, (a, e), breite = holz
            v0, v1, m = baender[band]
            q = (-e[1], e[0])
            pts = [P(x, z, tiefe) for x, z in poly]
            uvs = []
            for x, z in poly:
                lang = (x - a[0]) * e[0] + (z - a[1]) * e[1]
                quer = (x - a[0]) * q[0] + (z - a[1]) * q[1]
                t = min(max(quer / breite + 0.5, 0.0), 1.0)
                uvs.append((0.13 * hash_holz(holz) + lang / m, v1 - t * (v1 - v0)))
            sx = sum(x for x, _ in poly) / len(poly); sz = sum(z for _, z in poly) / len(poly)
            teil.vieleck(stoff, pts, uvs, weg_von=innen_pkt(sx, sz), **kw)
        for a, b, holz in self.aussenkanten():
            band = holz[2]
            v0, v1, m = baender[band]
            l = math.hypot(b[0] - a[0], b[1] - a[1])
            if l < 1e-5: continue
            pts = [P(a[0], a[1], 0.0), P(b[0], b[1], 0.0), P(b[0], b[1], tiefe), P(a[0], a[1], tiefe)]
            mitte = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
            # Sichtseite weg vom Holz: Punkt etwas innerhalb des Stuecks
            dx, dy = b[0] - a[0], b[1] - a[1]
            ix, iy = -dy / l, dx / l
            ref = P(mitte[0] + ix * 0.01, mitte[1] + iy * 0.01, tiefe / 2)
            uvs = [(0.0, v1), (l / m, v1), (l / m, v1 - 0.3 * (v1 - v0)), (0.0, v1 - 0.3 * (v1 - v0))]
            teil.vieleck(stoff, pts, uvs, weg_von=ref, **kw)
        if wand is not None:
            for poly, holz in self.zerlegen():
                v0, v1, m = baender[holz[2]]
                for stueck in (schneide(poly, (wand[0], 0.0), (-1.0, 0.0)), schneide(poly, (wand[1], 0.0), (1.0, 0.0))):
                    if not stueck: continue
                    sx = sum(x for x, _ in stueck) / len(stueck); sz = sum(z for _, z in stueck) / len(stueck)
                    teil.vieleck(stoff, [P(x, z, 0.0) for x, z in stueck], [(x / m, v0 + 0.5 * (v1 - v0)) for x, z in stueck],
                                 weg_von=P(sx, sz, 1.0), **kw)

    def feld_z(self, x, z, raenge=(0, 2)):
        """Lichte Hoehe des Felds an der Stelle x um z: (Oberkante des naechsten Holzes darunter, Unterkante des naechsten darueber),
        aus den tragenden Hoelzern (Schwelle, Raehm, Riegel). Figuren werden auf diese Grenzen zugeschnitten (Strebenende = Holzkante)."""
        unten, oben = -1e9, 1e9
        streifen = kasten(x - 1e-4, x + 1e-4, -1e3, 1e3)
        for poly, rang, *_ in self.hoelzer:
            if rang not in raenge: continue
            t = schnitt(poly, streifen)
            if not t: continue
            z0, z1 = min(q[1] for q in t), max(q[1] for q in t)
            if z1 <= z + 1e-6: unten = max(unten, z1)
            elif z0 >= z - 1e-6: oben = min(oben, z0)
        return unten, oben

    def spalten(self, schritt=0.003):
        """Strebenende beruehrt Nachbarholz, Spalt 0 (Nutzer 21.11.2026): jede Endkante eines Holzes ab Rang 1 (Kante quer zur Achse)
        muss an ein anderes Holz stossen – der Punkt 3 mm vor der Kantenmitte liegt in einem anderen Holz oder ausserhalb der Wand."""
        xs = [q[0] for poly, *_ in self.hoelzer for q in poly]; zs = [q[1] for poly, *_ in self.hoelzer for q in poly]
        rand = (min(xs) + 1e-3, max(xs) - 1e-3, min(zs) + 1e-3, max(zs) - 1e-3) if xs else None
        befunde = []
        for i, (poly, rang, band, (a, e), breite) in enumerate(self.hoelzer):
            if rang < 1 or i in self.ohne_ende: continue
            for k, p in enumerate(poly):
                q = poly[(k + 1) % len(poly)]
                dx, dz = q[0] - p[0], q[1] - p[1]
                l = math.hypot(dx, dz)
                if l < 0.01 or abs(dx / l * e[1] - dz / l * e[0]) < 0.2: continue      # Seitenkante (laengs) oder Splitter
                m = ((p[0] + q[0]) / 2 + dz / l * schritt, (p[1] + q[1]) / 2 - dx / l * schritt)
                if not (rand[0] < m[0] < rand[1] and rand[2] < m[1] < rand[3]): continue
                if self.umriss is not None and not innen(m, self.umriss, rand=1e-4): continue
                if any(innen(m, h[0], rand=0.0) for j, h in enumerate(self.hoelzer) if j != i): continue
                befunde.append(f"Spalt am Holzende ({m[0]:.2f}, {m[1]:.2f}) Rang {rang}")
        return befunde

    def pruefen(self):
        """Geometrische Pruefung: keine Ueberlappung der Stuecke, alle Stuecke mit Flaeche, jedes Strebenende beruehrt ein Nachbarholz
        (Spalt 0). Rueckgabe: Liste der Befunde."""
        befunde = []
        stuecke = self.zerlegen()
        for i, (a, _) in enumerate(stuecke):
            for j in range(i + 1, len(stuecke)):
                s = schnitt(a, stuecke[j][0])
                if s and abs(flaeche(s)) > 1e-5: befunde.append(f"Ueberlappung {abs(flaeche(s)) * 1e4:.1f} cm2")
        return befunde + self.spalten()


def hash_holz(holz):
    a = holz[3][0]
    return int(abs(a[0] * 7.3 + a[1] * 3.1) * 10) % 7
