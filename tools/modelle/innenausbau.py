"""Böden, Wände, Decken und Dachboden der Innenräume (schmied_innen, tischler_innen, steinmetz_innen).

Gemeinsame Bausteine in Blender-Koordinaten (Z oben, die Vorderseite eines Raums bei −Y):
    lehmboden       gestampfter Lehm: ruhige Fläche, ein paar hellere und dunklere Flecken, Strohhalme
    fischgraet      Backsteinpflaster im Fischgrätmuster: nur die Deckflächen der Ziegel, die Fugen sind eine 8 mm tiefer
                    liegende Mörtelfläche (Farbe und Vertiefung, keine Seitenflächen)
    dielen_plan     Bretter mit Nut und Feder: dicht gestoßen, die Fugen als dunkle Fläche dahinter
    balkendecke     Bretter von unten, Deckenbalken quer, auf Wunsch mit Treppenloch (Wechsel), den Dielen des Raums darüber
                    (dieselben Bretter von oben) oder einer Abdeckung (Halle ohne Geschoss darüber)
    durchbruch      Bruchstein, der an Ecken und unten durch den Kalkputz bricht: einzelne Steine vor einer Mörtelfläche
    russ            Ruß über dem Rauchfang als Farbverlauf (Eckenfarben; farben_zusammenlegen setzt ihn)
    treppe          steile Holztreppe (Wangen und Trittstufen) durch das Treppenloch
    bodenluke       Klappe und Geländer am Treppenloch im Boden darüber
    Dachboden       Lager unter dem Dach: Kniestock, Giebel, Sparren mit Schalung, Kehlbalken, First, Ladeluke mit Flügeln,
                    Kranbalken und Himmel dahinter
    pruefbericht    Dreiecke je Knoten und je Stufe, deckungsgleiche Flächen; vorschau: Bilder der Blickpunkte und Grundrisse

Materialarten für den Holzschnitt (game/assets/holzschnitt/materialarten.json): Wo die Form die Struktur schon zeigt –
Steine, Ziegel, Bretter mit Fugen –, heißen die Materialien so, dass die Tabelle „keine“ liefert (Kalkputz, Moertel,
Bruch …, Ziegelboden …, Diele …): Der Holzschnitt legt dort nur Schraffur, Papierkorn und Umriss darüber, keine zweite
Struktur. Balken („Fachwerk“, „Holz“) behalten die Holzmaserung, der Lehmboden („Erde hell“) die Bodentupfen.
"""

import math
import random

import bausteine as bs
from bausteine import Matrix, Vector

bs.FARBEN.update({
    "Kalkputz": (0.80, 0.77, 0.69),           # gekalkter Putz der Steinhäuser
    "Lehmputz hell": (0.77, 0.70, 0.56),      # heller Lehmputz in den Gefachen der Fachwerkhäuser
    "Moertel": (0.55, 0.52, 0.46),            # Mörtel, wo der Putz abgefallen ist
    "Bruch": (0.50, 0.48, 0.44),              # Bruchsteine (einzeln modelliert, ohne Struktur im Holzschnitt)
    "Bruch hell": (0.60, 0.57, 0.51),
    "Bruch dunkel": (0.40, 0.39, 0.36),
    "Bruch warm": (0.57, 0.50, 0.40),
    "Russ": (0.24, 0.22, 0.21),
    "Russputz": (0.50, 0.47, 0.43),           # Rußschleier: die Farbe je Ecke setzt farben_zusammenlegen (Verlauf)
    "Schlot": (0.50, 0.46, 0.41),             # verputzter Schlot im Dachboden
    "Diele": (0.46, 0.36, 0.25),              # Bretter von Böden, Decken und Schalung
    "Diele hell": (0.53, 0.42, 0.29),
    "Diele dunkel": (0.39, 0.30, 0.21),
    "Brett": (0.33, 0.27, 0.20),              # Bretter von Türen und Bretterwänden (Fugen modelliert)
    "Dielenfuge": (0.13, 0.10, 0.07),         # dunkle Fläche hinter den Fugen
    "Ziegelboden": (0.56, 0.31, 0.22),        # Backsteinpflaster
    "Ziegelboden hell": (0.62, 0.38, 0.27),
    "Ziegelboden dunkel": (0.47, 0.26, 0.19),
    "Ziegelfuge": (0.58, 0.55, 0.48),
    "Erde hell": (0.47, 0.39, 0.28),          # gestampfter Lehm (wie landbau.py; im Holzschnitt mit Bodentupfen)
    "Lehmfleck hell": (0.51, 0.43, 0.31),
    "Lehmfleck dunkel": (0.44, 0.36, 0.26),
    "Sack": (0.62, 0.55, 0.42),               # Sack am Seil des Kranbalkens
    "Strohhalm": (0.72, 0.63, 0.40),
})

DIELE = 0.05            # Dicke der Dielen: der Boden darüber liegt so viel über der Decke darunter
FUGE = 0.012            # Breite der Fugen zwischen Brettern und Ziegeln
DIELEN = ("Diele", "Diele hell", "Diele", "Diele dunkel")
UEBER = 0.3             # Abdeckung so weit über der Decke (wie schmied_innen.UEBER)


# ─────────────────────────────────────────────────────────────────────────────
#  Hilfen
# ─────────────────────────────────────────────────────────────────────────────

def rechteck_minus(r, loch):
    """Rechteck r = (x0, x1, y0, y1) ohne das Rechteck `loch` (oder None): bis zu vier Rechtecke."""
    x0, x1, y0, y1 = r
    if loch is None:
        return [r]
    lx0, lx1, ly0, ly1 = loch
    if lx0 >= x1 or lx1 <= x0 or ly0 >= y1 or ly1 <= y0:
        return [r]
    teile = []
    if ly0 > y0:
        teile.append((x0, x1, y0, ly0))
    if ly1 < y1:
        teile.append((x0, x1, ly1, y1))
    ym0, ym1 = max(y0, ly0), min(y1, ly1)
    if lx0 > x0:
        teile.append((x0, lx0, ym0, ym1))
    if lx1 < x1:
        teile.append((lx1, x1, ym0, ym1))
    return [t for t in teile if t[1] - t[0] > 1e-4 and t[3] - t[2] > 1e-4]


def waagrecht(netz, r, z, oben, m):
    """Waagrechtes Rechteck r = (x0, x1, y0, y1) in Höhe z, nach oben (`oben`) oder unten."""
    x0, x1, y0, y1 = r
    netz.vieleck_nach([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], (0, 0, 1 if oben else -1), m)


def beschneiden(punkte, x0, x1, y0, y1):
    """Konvexes Vieleck (Liste von (x, y)) auf das Rechteck beschneiden (Sutherland-Hodgman)."""
    def schnitt(liste, innen, kreuz):
        neu = []
        for i, p in enumerate(liste):
            q = liste[i - 1]
            if innen(p):
                if not innen(q):
                    neu.append(kreuz(q, p))
                neu.append(p)
            elif innen(q):
                neu.append(kreuz(q, p))
        return neu

    def bei_x(x):
        return lambda q, p: (x, q[1] + (p[1] - q[1]) * (x - q[0]) / (p[0] - q[0]))

    def bei_y(y):
        return lambda q, p: (q[0] + (p[0] - q[0]) * (y - q[1]) / (p[1] - q[1]), y)
    for innen, kreuz in ((lambda p: p[0] >= x0, bei_x(x0)), (lambda p: p[0] <= x1, bei_x(x1)),
                         (lambda p: p[1] >= y0, bei_y(y0)), (lambda p: p[1] <= y1, bei_y(y1))):
        punkte = schnitt(punkte, innen, kreuz)
        if not punkte:
            return []
    return punkte


def flaeche2d(punkte):
    return abs(sum(punkte[i][0] * punkte[i - 1][1] - punkte[i - 1][0] * punkte[i][1] for i in range(len(punkte)))) / 2


def ecken(x0, x1, y0, y1):
    """Innenkanten der vier Wände eines Raums, a → b so, dass der Raum links der Laufrichtung liegt."""
    return {"vorne": ((x0, y0), (x1, y0)), "rechts": ((x1, y0), (x1, y1)), "hinten": ((x1, y1), (x0, y1)),
            "links": ((x0, y1), (x0, y0))}


class Wand:
    """Innenkante einer Wand von a nach b (Grundriss), der Raum liegt links: p(u, v, w) ist der Punkt u entlang der Wand,
    v hoch über dem Boden (`boden`) und w vor der Wand im Raum (w < 0: in der Wand). Öffnungen (o.s Mitte entlang Welt-x bzw. -y, o.v, o.b, o.h)
    werden zu (u, o) umgerechnet."""

    def __init__(self, a, b, oeffnungen=(), boden=0.0):
        self.a = Vector((a[0], a[1], boden))
        d = Vector((b[0] - a[0], b[1] - a[1], 0))
        self.laenge = d.length
        self.d = d.normalized()
        self.n = Vector((-self.d.y, self.d.x, 0))
        entlang_x = abs(self.d.x) > 0.5
        self.ops = sorted((((o.s - (self.a.x if entlang_x else self.a.y)) * (self.d.x if entlang_x else self.d.y)), o)
                          for o in oeffnungen)

    def p(self, u, v, w=0.0):
        return self.a + self.d * u + Vector((0, 0, v)) + self.n * w

    def offen(self, u0, u1, v0, v1, rand=0.0):
        """Schneidet das Stück eine Öffnung?"""
        return any(u0 < m + o.b / 2 + rand and u1 > m - o.b / 2 - rand and v0 < o.v + o.h + rand and v1 > o.v - rand
                   for m, o in self.ops)


# ─────────────────────────────────────────────────────────────────────────────
#  Böden
# ─────────────────────────────────────────────────────────────────────────────

def lehmboden(netz, st, x0, x1, y0, y1, saat=0, flecken=10, halme=26):
    """Gestampfter Lehm: eine ruhige Fläche, darauf ein paar unregelmäßige hellere (festgetretene) und dunklere Flecken und
    verstreute Strohhalme – keine Platten. Flecken und Halme liegen 1 bis 3 mm über dem Boden, Späne darüber."""
    waagrecht(netz, (x0, x1, y0, y1), 0.0, True, st["Erde hell"])
    zufall = random.Random(saat)
    mitten = []
    for _ in range(600):
        if len(mitten) >= flecken:
            break
        r = zufall.uniform(0.3, 0.85)
        c = Vector((zufall.uniform(x0 + r, x1 - r), zufall.uniform(y0 + r, y1 - r), 0.0))
        if all((c - m).length > r + rm + 0.15 for m, rm in mitten):
            mitten.append((c, r))
    for k, (c, r) in enumerate(mitten):
        dehnung = zufall.uniform(0.6, 1.0)
        dreh = zufall.uniform(0, math.pi)
        rand = []
        for i in range(12):
            w = 2 * math.pi * i / 12 + zufall.uniform(-0.1, 0.1)
            rr = r * zufall.uniform(0.8, 1.0)
            lx, ly = rr * math.cos(w), rr * dehnung * math.sin(w)
            rand.append(c + Vector((lx * math.cos(dreh) - ly * math.sin(dreh), lx * math.sin(dreh) + ly * math.cos(dreh), 0.0012)))
        netz.vieleck_nach(rand, (0, 0, 1), st["Lehmfleck hell" if k % 3 else "Lehmfleck dunkel"])
    for _ in range(halme):
        c = Vector((zufall.uniform(x0 + 0.2, x1 - 0.2), zufall.uniform(y0 + 0.2, y1 - 0.2), 0.0025))
        w = zufall.uniform(0, math.pi)
        lang = zufall.uniform(0.1, 0.26)
        d = Vector((math.cos(w), math.sin(w), 0)) * lang / 2
        q = Vector((-d.y, d.x, 0)).normalized() * 0.005
        netz.vieleck_nach([c - d - q, c + d - q, c + d + q, c - d + q], (0, 0, 1), st["Strohhalm"])


def fischgraet(netz, st, x0, x1, y0, y1, saat=0, b=0.16):
    """Backsteinpflaster im Fischgrätmuster (Ziegel b × 2b, um 45° gedreht, die Zickzackreihen laufen entlang x): nur die
    Deckfläche jedes Ziegels in z = 0, am Rand auf den Raum beschnitten; darunter (z = −8 mm) eine Mörtelfläche, die in
    den Fugen zu sehen ist. Gibt die Zahl der Ziegel zurück."""
    waagrecht(netz, (x0, x1, y0, y1), -0.008, True, st["Ziegelfuge"])
    zufall = random.Random(saat)
    s = math.sqrt(0.5)

    def welt(u, v):
        return (x0 + (u + v) * s, y0 + (v - u) * s)
    us = [((x - x0) - (y - y0)) * s for x in (x0, x1) for y in (y0, y1)]
    vs = [((x - x0) + (y - y0)) * s for x in (x0, x1) for y in (y0, y1)]
    umin, umax, vmin, vmax = min(us), max(us), min(vs), max(vs)
    f = FUGE / 2
    farben = ("Ziegelboden", "Ziegelboden hell", "Ziegelboden", "Ziegelboden dunkel")
    anzahl = 0
    for m in range(int(math.floor((umin - vmax) / (4 * b))) - 2, int(math.ceil((umax - vmin) / (4 * b))) + 2):
        for k in range(int(math.floor((umin + vmin) / (2 * b))) - 3, int(math.ceil((umax + vmax) / (2 * b))) + 3):
            u0, v0 = b * (k + 2 * m), b * (k - 2 * m)
            for (a0, a1, c0, c1) in ((u0, u0 + 2 * b, v0, v0 + b), (u0, u0 + b, v0 + b, v0 + 3 * b)):   # liegend, stehend
                if a1 < umin or a0 > umax or c1 < vmin or c0 > vmax:
                    continue
                ecken = [welt(a0 + f, c0 + f), welt(a1 - f, c0 + f), welt(a1 - f, c1 - f), welt(a0 + f, c1 - f)]
                stueck = beschneiden(ecken, x0, x1, y0, y1)
                if len(stueck) < 3 or flaeche2d(stueck) < 0.0004:
                    continue
                netz.vieleck_nach([(x, y, 0.0) for x, y in stueck], (0, 0, 1), st[zufall.choice(farben)])
                anzahl += 1
    return anzahl


def dielen_plan(x0, x1, y0, y1, saat=0, breite=0.28):
    """Bretter entlang x in Reihen von etwa `breite`, die Stöße versetzt: [(x0, x1, y0, y1, Material)], zwischen den
    Brettern eine Fuge (FUGE), an den Wänden keine."""
    zufall = random.Random(saat)
    plan = []
    y = y0
    while y < y1 - 0.02:
        yb = y + breite * zufall.uniform(0.9, 1.1)
        if y1 - yb < 0.14:
            yb = y1
        x = x0
        laenge = zufall.uniform(0.8, 3.2)
        while x < x1 - 0.01:
            xe = x + laenge
            if x1 - xe < 0.6:
                xe = x1
            plan.append((x + (FUGE / 2 if x > x0 else 0.0), xe - (FUGE / 2 if xe < x1 else 0.0),
                         y + (FUGE / 2 if y > y0 else 0.0), yb - (FUGE / 2 if yb < y1 else 0.0), zufall.choice(DIELEN)))
            x = xe
            laenge = zufall.uniform(2.2, 4.0)
        y = yb
    return plan


# ─────────────────────────────────────────────────────────────────────────────
#  Decke
# ─────────────────────────────────────────────────────────────────────────────

BALKEN_B, BALKEN_H = 0.18, 0.26       # Deckenbalken: Breite, Höhe


def balkendecke(netz, st, x0, x1, y0, y1, hoehe, luke=None, schlote=(), oben=None, abdeckung=None, rand_hinten=0.0, saat=0):
    """Balkendecke: Bretter (Unterseite in `hoehe`, Fugen vor einer dunklen Fläche), Deckenbalken quer (entlang y, etwa alle
    1,05 m). `luke` = (x0, x1, y0, y1): Treppenloch mit Laibung; `schlote`: Löcher für Schlote (ohne Laibung, der Schlot
    füllt sie). Balken, die ein Loch kreuzen, sind ausgewechselt (Wechsel entlang x an den Kanten), die Bretter enden an
    der Kante. `oben`: Netz des Raums darüber, es bekommt die Oberseite derselben Bretter als Boden (in hoehe + DIELE).
    `abdeckung`: Wanddicke – eine Abdeckung über allem für einen Raum ohne Geschoss darüber. `rand_hinten`: dort enden die
    Balken vor der Rückwand. Gibt die Höhe des Bodens darüber zurück."""
    loecher = ([luke] if luke is not None else []) + list(schlote)
    plan = dielen_plan(x0, x1, y0, y1, saat)

    def ohne_loecher(r):
        teile = [r]
        for loch in loecher:
            teile = [t for r_ in teile for t in rechteck_minus(r_, loch)]
        return teile
    for bx0, bx1, by0, by1, stoff in plan:
        for r in ohne_loecher((bx0, bx1, by0, by1)):
            waagrecht(netz, r, hoehe, False, st[stoff])
            if oben is not None:
                waagrecht(oben, r, hoehe + DIELE, True, st[stoff])
    for r in ohne_loecher((x0, x1, y0, y1)):                        # hinter den Fugen, von beiden Seiten zu sehen
        waagrecht(netz, r, hoehe + DIELE / 2, False, st["Dielenfuge"])
    if luke is not None:
        lx0, lx1, ly0, ly1 = luke
        z0, z1 = hoehe, hoehe + DIELE
        for (ax, ay), (bx, by), n in (((lx0, ly0), (lx1, ly0), (0, 1, 0)), ((lx0, ly1), (lx1, ly1), (0, -1, 0)),
                                      ((lx0, ly0), (lx0, ly1), (1, 0, 0)), ((lx1, ly0), (lx1, ly1), (-1, 0, 0))):
            if (ax == bx and (ax <= x0 + 1e-4 or ax >= x1 - 1e-4)) or (ay == by and (ay <= y0 + 1e-4 or ay >= y1 - 1e-4)):
                continue                                                  # Kante an der Wand
            netz.vieleck_nach([(ax, ay, z0), (bx, by, z0), (bx, by, z1), (ax, ay, z1)], n, st["Diele dunkel"])
    anzahl = max(2, round((x1 - x0) / 1.05))
    xs = [x0 + (x1 - x0) * (k + 0.5) / anzahl for k in range(anzahl)]
    zb = hoehe - BALKEN_H / 2
    ende = y1 - rand_hinten

    def kreuzt(x, loch):
        return x - BALKEN_B / 2 < loch[1] and x + BALKEN_B / 2 > loch[0]
    for x in xs:
        stuecke = [(y0, ende)]
        for loch in loecher:
            if kreuzt(x, loch):
                stuecke = [s for a, b in stuecke for s in ((a, min(b, loch[2] - BALKEN_B)), (max(a, loch[3] + BALKEN_B), b))
                           if s[1] - s[0] > 0.05]
        for a, b in stuecke:
            netz.quader((x, (a + b) / 2, zb), (BALKEN_B, b - a, BALKEN_H), st["Fachwerk"], ohne=("+z", "+y", "-y"))
    for loch in loecher:
        if not any(kreuzt(x, loch) for x in xs):
            continue
        lx0, lx1, ly0, ly1 = loch
        links = max([x + BALKEN_B / 2 for x in xs if x + BALKEN_B / 2 <= lx0], default=x0)
        rechts = min([x - BALKEN_B / 2 for x in xs if x - BALKEN_B / 2 >= lx1], default=x1)
        for y, da in ((ly0 - BALKEN_B / 2, ly0 - BALKEN_B > y0 + 0.05), (ly1 + BALKEN_B / 2, ly1 + BALKEN_B < ende - 0.05)):
            if da:                                                       # Wechsel
                netz.quader(((links + rechts) / 2, y, zb), (rechts - links, BALKEN_B, BALKEN_H), st["Fachwerk"],
                            ohne=("+z", "+x", "-x"))
    if abdeckung is not None:
        d = abdeckung
        waagrecht(netz, (x0 - d, x1 + d, y0 - d, y1 + d), hoehe + UEBER, True, st["Holz alt"])
    return hoehe + DIELE


# ─────────────────────────────────────────────────────────────────────────────
#  Wände: Bruchstein bricht durch den Putz, Ruß
# ─────────────────────────────────────────────────────────────────────────────

STEINE = ("Bruch", "Bruch hell", "Bruch dunkel", "Bruch", "Bruch warm")


def durchbruch(netz, st, a, b, hoehe, oeffnungen=(), saat=0, ecken=(True, True), unten=True, frei=(), ecke_hoch=(1.0, 2.3),
               boden=0.0):
    """Der Kalkputz ist an den Ecken (`ecken`: Anfang a, Ende b der Wand) und unten (`unten`) abgefallen, darunter zeigt sich
    das Bruchsteinmauerwerk: je Steinlage eine Mörtelfläche 5 mm vor dem Putz (Stufen wie abgeplatzter Putz), davor die
    einzelnen Steine (unregelmäßige Sechsecke, 1 cm vor dem Putz). `frei`: Stücke [(u0, u1)] entlang der Wand ohne
    Durchbruch (hinter Einbauten), Öffnungen bleiben frei; `boden`: Höhe des Bodens (Dachboden)."""
    wand = Wand(a, b, oeffnungen, boden)
    zufall = random.Random(saat)
    lagen = [0.0]
    while lagen[-1] < hoehe - 0.3:
        lagen.append(lagen[-1] + zufall.uniform(0.2, 0.3))

    def gesperrt(u0, u1, v0, v1):
        return wand.offen(u0, u1, v0, v1, 0.04) or any(u0 < f1 and u1 > f0 for f0, f1 in frei)

    def lage(u0, u1, k, rand0=True, rand1=True):
        """Steine der Lage k zwischen u0 und u1 (Mörtel bis an die Enden, an einer Ecke ohne Rand)."""
        v0, v1 = lagen[k], lagen[k + 1]
        if u1 - u0 < 0.2 or gesperrt(u0, u1, v0, v1):
            return
        m0 = u0 - (zufall.uniform(0.02, 0.07) if rand0 else 0.0)
        m1 = u1 + (zufall.uniform(0.02, 0.07) if rand1 else 0.0)
        netz.vieleck_nach([wand.p(m0, v0, 0.005), wand.p(m1, v0, 0.005), wand.p(m1, v1, 0.005), wand.p(m0, v1, 0.005)],
                          wand.n, st["Moertel"])
        u = u0
        while u < u1 - 0.12:
            ue = min(u + zufall.uniform(0.24, 0.48), u1)
            if u1 - ue < 0.15:
                ue = u1
            j = 0.013
            ecken_ = [(u + j + zufall.uniform(0, 0.02), v0 + j + zufall.uniform(0, 0.015)),
                      ((u + ue) / 2 + zufall.uniform(-0.04, 0.04), v0 + j + zufall.uniform(-0.005, 0.01)),
                      (ue - j - zufall.uniform(0, 0.02), v0 + j + zufall.uniform(0, 0.015)),
                      (ue - j - zufall.uniform(0, 0.02), v1 - j - zufall.uniform(0, 0.015)),
                      ((u + ue) / 2 + zufall.uniform(-0.04, 0.04), v1 - j - zufall.uniform(-0.005, 0.01)),
                      (u + j + zufall.uniform(0, 0.02), v1 - j - zufall.uniform(0, 0.015))]
            netz.vieleck_nach([wand.p(x, y, 0.01) for x, y in ecken_], wand.n, st[zufall.choice(STEINE)])
            u = ue

    L = wand.laenge
    ende0 = ende1 = 0.0
    for seite, an in enumerate(ecken):
        if not an:
            continue
        hoch = zufall.uniform(*ecke_hoch)
        breit = zufall.uniform(0.5, 0.95)
        for k in range(len(lagen) - 1):
            mitte = (lagen[k] + lagen[k + 1]) / 2
            if mitte > hoch:
                break
            e = max(0.25, breit * (1 - mitte / hoch) ** 0.6 + zufall.uniform(-0.08, 0.12))
            if seite == 0:
                lage(0.0, e, k, rand0=False)
                ende0 = max(ende0, e) if k == 0 else ende0
            else:
                lage(L - e, L, k, rand1=False)
                ende1 = max(ende1, e) if k == 0 else ende1
    if not unten:
        return
    u = ende0 + zufall.uniform(0.4, 1.4)
    while u < L - ende1 - 0.8:
        laenge = zufall.uniform(0.6, 2.0)
        ue = min(u + laenge, L - ende1 - 0.3)
        lage(u, ue, 0)
        if zufall.random() < 0.55 and ue - u > 0.9:
            lage(u + zufall.uniform(0.1, 0.4), ue - zufall.uniform(0.1, 0.4), 1)
        u = ue + zufall.uniform(0.7, 2.2)


RUSS_QUELLEN = []       # (Wand, u der Mitte, halbe Breite des Rauchfangs unten, Unterkante des Rauchfangs, Höhe des Raums)


def russ_anteil(quelle, punkt):
    """Wie rußig der Putz an `punkt` ist (0 … 1): ein Schleier über dem Rauchfang, nach oben breiter, an der Decke verlaufend."""
    wand, mitte, breite, unten, hoehe = quelle
    rel = punkt - wand.a
    if abs(rel.dot(wand.n)) > 0.05:
        return 0.0
    du = abs(rel.dot(wand.d) - mitte)
    v = punkt.z
    halb = breite * (0.8 + 0.35 * max(0.0, v - unten))
    seitlich = max(0.0, min(1.0, 1.0 - (du - halb * 0.7) / (halb * 0.9)))
    hoch = max(0.0, min(1.0, (v - unten + 0.5) / 0.9))
    decke = 0.45 * max(0.0, min(1.0, (v - (hoehe - 0.9)) / 0.9)) * max(0.0, 1.0 - du / (halb + 1.6))
    return min(1.0, seitlich * hoch * (0.6 + 0.4 * max(0.0, min(1.0, (v - unten) / (hoehe - unten)))) + decke)


def russ(netz, st, a, b, mitte, breite, unten, hoehe, oeffnungen=(), zelle=0.25):
    """Rußschleier auf der Wand a → b über einem Rauchfang (Mitte `mitte` entlang der Wand, halbe Breite `breite` unten,
    Unterkante `unten`): ein Gitter aus Vierecken 2 mm vor dem Putz im Material „Russputz“. Die Farbe jeder Ecke (Verlauf
    von Ruß zu Kalkputz) setzt farben_zusammenlegen nach russ_anteil; Zellen ohne Ruß entfallen."""
    wand = Wand(a, b, oeffnungen)
    quelle = (wand, mitte, breite, unten, hoehe)
    RUSS_QUELLEN.append(quelle)
    weit = breite * (0.8 + 0.35 * (hoehe - unten)) + 1.6
    u0, u1 = max(0.0, mitte - weit), min(wand.laenge, mitte + weit)
    v0 = max(0.0, unten - 0.5)
    nu, nv = max(1, round((u1 - u0) / zelle)), max(1, round((hoehe - v0) / zelle))
    for i in range(nu):
        for j in range(nv):
            ua, ub = u0 + (u1 - u0) * i / nu, u0 + (u1 - u0) * (i + 1) / nu
            va, vb = v0 + (hoehe - v0) * j / nv, v0 + (hoehe - v0) * (j + 1) / nv
            if wand.offen(ua, ub, va, vb):
                continue
            ecken_ = [wand.p(ua, va, 0.002), wand.p(ub, va, 0.002), wand.p(ub, vb, 0.002), wand.p(ua, vb, 0.002)]
            if max(russ_anteil(quelle, e) for e in ecken_) < 0.02:
                continue
            netz.vieleck_nach(ecken_, wand.n, st["Russputz"])


def farben_zusammenlegen(objekte):
    """bausteine.farben_zusammenlegen, danach bekommen die Flächen „Russputz“ ihre Eckenfarbe aus dem Rußverlauf
    (Mischung aus Kalkputz und Ruß, sRGB-Werte wie die übrigen Eckenfarben). Die Netze liegen ohne Verschiebung im Raum."""
    netze = [o for o in set(objekte) | {k for o in objekte for k in o.children_recursive} if o.type == "MESH"]
    merken = {}
    for o in netze:
        namen = [m.name if m else "" for m in o.data.materials]
        flaechen = [p.index for p in o.data.polygons if p.material_index < len(namen) and namen[p.material_index] == "Russputz"]
        if flaechen:
            merken[o] = flaechen
    ergebnis = bs.farben_zusammenlegen(objekte)
    putz, schwarz = Vector(bs.FARBEN["Kalkputz"]), Vector(bs.FARBEN["Russ"])
    for o, flaechen in merken.items():
        me = o.data
        attr = me.color_attributes["Farbe"]
        werte = [0.0] * (len(me.loops) * 4)
        attr.data.foreach_get("color", werte)
        for i in flaechen:
            for li in me.polygons[i].loop_indices:
                punkt = o.matrix_world @ me.vertices[me.loops[li].vertex_index].co
                f = max((russ_anteil(q, punkt) for q in RUSS_QUELLEN), default=0.0)
                werte[li * 4:li * 4 + 3] = list(putz.lerp(schwarz, 0.95 * f))
        attr.data.foreach_set("color", werte)
        me.update()
    return ergebnis


# ─────────────────────────────────────────────────────────────────────────────
#  Treppe und Bodenluke
# ─────────────────────────────────────────────────────────────────────────────

def treppe(netz, st, x_mitte, y_fuss, y_oben, z_oben, breite=0.66, steigung=0.25, holme=0.9):
    """Steile Holztreppe (Leitertreppe) entlang y: zwei Wangen vom Boden (y_fuss) bis in das Treppenloch (y_oben, Boden
    darüber in z_oben), dazwischen offene Trittstufen; die oberste Stufe ist der Boden darüber. An den Wangen oben zwei
    Holme, die `holme` m über den Boden darüber ragen (zum Festhalten beim Ausstieg; im Raum oben zeigen sie die Treppe an).
    Gibt den Winkel zurück."""
    holz = st["Holz"]
    richtung = 1.0 if y_oben > y_fuss else -1.0
    lauf = abs(y_oben - y_fuss)
    alle, netz.alle = netz.alle, True
    wd, wt = 0.06, 0.22                                             # Wange: Dicke, Tiefe
    for s in (-1, 1):
        x = x_mitte + s * (breite / 2 - wd / 2)
        netz.stab((x, y_fuss, 0.0), (x, y_oben, z_oben - 0.05), wt, wd, holz, quer=(1, 0, 0))
        if holme:
            netz.quader((x, y_oben - richtung * 0.03, z_oben + (holme - 0.4) / 2), (wd + 0.012, 0.08, holme + 0.4), holz)
    n = max(2, round(z_oben / steigung))
    for k in range(1, n):
        z = z_oben * k / n
        y = y_fuss + richtung * lauf * k / n
        netz.quader((x_mitte, y, z - 0.02), (breite - 2 * wd, 0.17, 0.04), holz, ohne=("+x", "-x"))
    netz.alle = alle
    return math.degrees(math.atan2(z_oben, lauf))


def bodenluke(netz, st, luke, z, gelaender_x=None):
    """Am Treppenloch im Boden (luke = (x0, x1, y0, y1), Boden in z; die Kante x1 liegt an der Wand, die Treppe kommt von
    vorn (−y) herauf): ein Lukenkranz aus dunklen Bohlen an den drei offenen Kanten (zeichnet das Loch im Holzschnitt nach),
    die Klappe aus Brettern, an der Wandseite angeschlagen und senkrecht hochgestellt (mit Leisten, Bändern, Ring), und an
    der Seite `gelaender_x` ein Geländer mit zwei Pfosten."""
    lx0, lx1, ly0, ly1 = luke
    alle, netz.alle = netz.alle, True
    holz = st["Fachwerk"]
    k, h = 0.08, 0.05                                                    # Kranz: Breite, Höhe
    netz.quader(((lx0 + lx1) / 2 - k / 2, ly0 - k / 2, z + h / 2), (lx1 - lx0 + k, k, h), holz, ohne=("-z", "+x"))
    netz.quader(((lx0 + lx1) / 2 - k / 2, ly1 + k / 2, z + h / 2), (lx1 - lx0 + k, k, h), holz, ohne=("-z", "+x"))
    netz.quader((lx0 - k / 2, (ly0 + ly1) / 2, z + h / 2), (k, ly1 - ly0, h), holz, ohne=("-z", "+y", "-y"))
    lang, breit, d = lx1 - lx0 - 0.04, ly1 - ly0 - 0.04, 0.045        # Klappe: Höhe (hochgestellt), Breite, Dicke
    x = lx1 - 0.08
    netz.quader((x, (ly0 + ly1) / 2, z + 0.01 + lang / 2), (d, breit, lang), st["Diele"])
    for t in (0.2, 0.8):                                                 # Leisten auf der Unterseite (jetzt zum Raum)
        netz.quader((x - d / 2 - 0.015, (ly0 + ly1) / 2, z + 0.01 + lang * t), (0.03, breit - 0.1, 0.09), st["Holz"], ohne=("+x",))
    for ys in (ly0 + 0.2, ly1 - 0.2):                                    # Bänder bis zur Angel unten
        netz.quader((x - d / 2 - 0.006, ys, z + 0.01 + lang * 0.3), (0.012, 0.05, lang * 0.6), st["Eisen"], ohne=("+x",))
    netz.quader((x - d / 2 - 0.02, (ly0 + ly1) / 2, z + lang - 0.1), (0.04, 0.07, 0.07), st["Eisen"], ohne=("+x",))   # Ring
    if gelaender_x is not None:
        gx = gelaender_x
        for y in (ly0 + 0.05, ly1 - 0.05):
            netz.quader((gx, y, z + h + 0.475), (0.08, 0.08, 0.95), st["Holz"], ohne=("-z",))
        netz.stab((gx, ly0 + 0.01, z + 1.0), (gx, ly1 - 0.01, z + 1.0), 0.07, 0.09, st["Holz"], quer=(1, 0, 0), ohne=("+x", "-x"))
        netz.stab((gx, ly0 + 0.09, z + 0.55), (gx, ly1 - 0.09, z + 0.55), 0.05, 0.05, st["Holz"], quer=(1, 0, 0), ohne=("+x", "-x"))
    netz.alle = alle


# ─────────────────────────────────────────────────────────────────────────────
#  Dachboden
# ─────────────────────────────────────────────────────────────────────────────

class Dachboden:
    """Lager unter dem Dach über einem Raum mit der Innenfläche x0 … x1, y0 … y1; Boden in z (Oberkante der Dielen).
    Der First läuft entlang x in der Mitte (ym), die Dachflächen steigen vom Kniestock (Höhe `kniestock` über dem Boden,
    an den Wänden y0 und y1) mit `neigung` Grad an; die Giebel stehen bei x0 (links) und x1 (rechts).
    hoehe(y) ist die Unterseite der Schalung über dem Boden."""

    def __init__(self, x0, x1, y0, y1, z, neigung, kniestock=1.3, kehle=3.1):
        self.x0, self.x1, self.y0, self.y1, self.z = x0, x1, y0, y1, z
        self.ym, self.halb = (y0 + y1) / 2, (y1 - y0) / 2
        self.tan = math.tan(math.radians(neigung))
        self.neigung = math.radians(neigung)
        self.kniestock, self.kehle = kniestock, kehle
        self.first = z + kniestock + self.halb * self.tan

    def dach_z(self, y):
        return self.z + self.kniestock + (self.halb - abs(y - self.ym)) * self.tan

    def hoehe(self, y):
        return self.dach_z(y) - self.z

    # ── Wände ────────────────────────────────────────────────────────────────
    def kniestoecke(self, netz, st, stoff):
        """Kniestock vorn (y0) und hinten (y1): von der Decke darunter (Unterseite der Dielen, dort endet die Wand des Raums
        darunter – keine Ritze im Treppenloch) bis zur Dachfläche."""
        z0, z1 = self.z - DIELE, self.z + self.kniestock
        netz.vieleck_nach([(self.x0, self.y0, z0), (self.x1, self.y0, z0), (self.x1, self.y0, z1), (self.x0, self.y0, z1)],
                          (0, 1, 0), st[stoff])
        netz.vieleck_nach([(self.x0, self.y1, z0), (self.x1, self.y1, z0), (self.x1, self.y1, z1), (self.x0, self.y1, z1)],
                          (0, -1, 0), st[stoff])

    def giebel(self, netz, st, rechts, oeffnungen, stoff, dicke, schwelle="Diele dunkel"):
        """Giebel links (x0, blickt nach +x) oder rechts (x1, nach −x) bis unter die Dachflächen, in Streifen zwischen den
        Öffnungen (o.s Mitte entlang y, o.v Unterkante über dem Boden), mit Laibungen durch die Wanddicke."""
        x = self.x1 if rechts else self.x0
        n = Vector((-1, 0, 0) if rechts else (1, 0, 0))
        aussen = -n * dicke
        grenzen = sorted({self.y0, self.y1, self.ym} | {o.s + s * o.b / 2 for o in oeffnungen for s in (-1, 1)})
        z0 = self.z - DIELE                                              # wie kniestoecke
        for ya, yb in zip(grenzen, grenzen[1:]):
            o = next((o for o in oeffnungen if abs((ya + yb) / 2 - o.s) < o.b / 2), None)
            stuecke = [(z0, None)] if o is None else ([(z0, self.z + o.v)] if o.v > 0 else []) + [(self.z + o.v + o.h, None)]
            for unten, oben in stuecke:
                if oben is None:
                    pkt = [(x, ya, unten), (x, yb, unten), (x, yb, self.dach_z(yb)), (x, ya, self.dach_z(ya))]
                else:
                    pkt = [(x, ya, unten), (x, yb, unten), (x, yb, oben), (x, ya, oben)]
                netz.vieleck_nach(pkt, n, st[stoff])
        for o in oeffnungen:
            ya, yb = o.s - o.b / 2, o.s + o.b / 2
            za, zb = self.z + o.v, self.z + o.v + o.h
            ecken_ = [Vector((x, ya, za)), Vector((x, yb, za)), Vector((x, yb, zb)), Vector((x, ya, zb))]
            for i, normale in enumerate(((0, 0, 1), (0, -1, 0), (0, 0, -1), (0, 1, 0))):
                a, b = ecken_[i], ecken_[(i + 1) % 4]
                m = st[schwelle] if i == 0 and o.v < 0.05 else st[stoff]
                netz.vieleck_nach([a, b, b + aussen, a + aussen], normale, m)

    # ── Dach ─────────────────────────────────────────────────────────────────
    def dach(self, netz, st, sparren=0.95, aussparen=(), saat=0):
        """Schalung (Bretter entlang x mit Fugen vor einer dunklen Fläche), Sparren (12 × 16 cm) etwa alle `sparren` m,
        Fußpfetten auf dem Kniestock, Firstbohle und Kehlbalken (an jedem zweiten Sparrenpaar, seitlich angeblattet).
        `aussparen`: Stücke [(x0, x1)], in denen kein Sparren stehen soll (Schlot)."""
        alle, netz.alle = netz.alle, True
        zufall = random.Random(saat)
        ze = self.z + self.kniestock
        lang = self.halb / math.cos(self.neigung)
        anzahl = max(2, round((self.x1 - self.x0 - 0.3) / sparren))
        xs = [self.x0 + 0.15 + (self.x1 - self.x0 - 0.3) * k / anzahl for k in range(anzahl + 1)]
        for i, x in enumerate(xs):                                      # aus dem Schlot schieben
            for a, b in aussparen:
                if a - 0.1 < x < b + 0.1:
                    xs[i] = a - 0.2 if x - a < b - x and a - 0.2 > self.x0 + 0.1 else b + 0.12
        for vorn in (True, False):
            ye = self.y0 if vorn else self.y1
            t = Vector((0, (self.ym - ye) / self.halb * math.cos(self.neigung), math.sin(self.neigung)))   # die Fläche hinauf
            innen = Vector((0, math.sin(self.neigung) * (1 if vorn else -1), -math.cos(self.neigung)))
            fuss = Vector((0, ye, ze))

            def P(x, s, w=0.0):
                return Vector((x, 0, 0)) + fuss + t * s + innen * w
            # Schalung: Bretter quer zu den Sparren, die Stöße versetzt
            s = 0.0
            while s < lang - 0.02:
                se = min(s + 0.24 * zufall.uniform(0.9, 1.1), lang)
                if lang - se < 0.1:
                    se = lang
                x = self.x0
                stueck = zufall.uniform(0.8, 3.0)
                while x < self.x1 - 0.01:
                    xe = min(x + stueck, self.x1)
                    if self.x1 - xe < 0.6:
                        xe = self.x1
                    a, b = x + (FUGE / 2 if x > self.x0 else 0), xe - (FUGE / 2 if xe < self.x1 else 0)
                    c, d = s + (FUGE / 2 if s > 0 else 0), se - (FUGE / 2 if se < lang else 0)
                    netz.vieleck_nach([P(a, c), P(b, c), P(b, d), P(a, d)], innen, st[zufall.choice(DIELEN)])
                    x = xe
                    stueck = zufall.uniform(2.4, 4.0)
                s = se
            netz.vieleck_nach([P(self.x0, 0, -0.006), P(self.x1, 0, -0.006), P(self.x1, lang, -0.006), P(self.x0, lang, -0.006)],
                              innen, st["Dielenfuge"])
            # Sparren: Oberseite an der Schalung, unten auf der Fußpfette, oben an der Firstbohle
            for x in xs:
                x += 0.0 if vorn else 0.012        # das Paar 12 mm versetzt: am First lägen sonst die Seiten beider in einer Ebene
                netz.stab(P(x, 0.02, 0.08), P(x, lang - 0.06 / math.cos(self.neigung), 0.08), 0.16, 0.12, st["Fachwerk"],
                          quer=(1 if vorn else -1, 0, 0), ohne=("-z", "+x", "-x"))
            # Fußpfette auf dem Kniestock
            yp = ye + (0.09 if vorn else -0.09)
            netz.quader(((self.x0 + self.x1) / 2, yp, ze - 0.09), (self.x1 - self.x0, 0.18, 0.18), st["Fachwerk"],
                        ohne=("+x", "-x", "-y" if vorn else "+y"))
        # Firstbohle
        netz.quader(((self.x0 + self.x1) / 2, self.ym, self.first - 0.11), (self.x1 - self.x0, 0.06, 0.22), st["Fachwerk"],
                    ohne=("+x", "-x", "+z"))
        # Kehlbalken neben jedem zweiten Sparrenpaar
        zk = self.z + self.kehle
        weit = self.halb - (self.kehle - self.kniestock) / self.tan - 0.18 / self.tan
        for i, x in enumerate(xs):
            if i % 2 == 1 or i == 0 or i == len(xs) - 1:
                continue
            netz.quader((x + 0.106, self.ym, zk), (0.09, 2 * weit, 0.18), st["Fachwerk"])
        netz.alle = alle
        return xs

    # ── Ladeluke ─────────────────────────────────────────────────────────────
    def ladeluke(self, netz, kran, himmel, st, mitte, breite=1.2, hoch=1.8, dicke=0.5, futter=True):
        """Ladeluke im rechten Giebel (die Öffnung selbst gibt giebel()): Futter mit Sturz, zwei Flügel in der Hausfarbe,
        nach innen fast an die Wand geklappt, Kranbalken (in `kran`) durch den Giebel nach außen mit Stütze, Rolle und Seil,
        und eine Fläche „Fenster“ (in `himmel`) drei Meter vor dem Giebel: das Spiel lässt sie in der Himmelsfarbe leuchten.
        `futter` False: ohne Pfosten und Sturz (im Fachwerkgiebel rahmen Ständer und Riegel die Luke). Der Kranbalken liegt
        innen auf einer Konsole am Giebel; über die Rolle hängen beide Enden des Seils außen hinunter, an einem ein Sack.
        Gibt den Punkt für das Licht zurück."""
        x, z = self.x1, self.z
        ya, yb = mitte - breite / 2, mitte + breite / 2
        holz = st["Fachwerk"]
        if futter:                                                       # Futter: Pfosten und Sturz vor dem Giebel
            for y in (ya - 0.06, yb + 0.06):
                netz.quader((x - 0.04, y, z + hoch / 2), (0.08, 0.12, hoch), holz, ohne=("+x",))
            netz.quader((x - 0.05, mitte, z + hoch + 0.08), (0.1, breite + 0.24, 0.16), holz, ohne=("+x",))
        for s, y in ((-1, ya - 0.12), (1, yb + 0.12)):                  # Flügel um 160° geöffnet
            d = Vector((-math.sin(math.radians(20)), s * math.cos(math.radians(20)), 0))
            q = Vector((-d.y, d.x, 0)) * s                               # zur Raumseite
            b = breite / 2 - 0.02
            M = Matrix((d, q, Vector((0, 0, 1)))).transposed().to_4x4()
            fuss = Vector((x - 0.11, y, z + 0.02))
            netz.quader((0, 0, 0), (1, 1, 1), st["Hausfarbe"], Matrix.Translation(fuss + d * (b / 2) + Vector((0, 0, (hoch - 0.04) / 2)))
                        @ M @ Matrix.Diagonal((b, 0.045, hoch - 0.04, 1)))
            for zz in (0.3, hoch - 0.3):                                 # Querleisten und Bänder auf der Innenseite
                netz.quader((0, 0, 0), (1, 1, 1), holz, Matrix.Translation(fuss + d * (b / 2) + q * 0.035 + Vector((0, 0, zz)))
                            @ M @ Matrix.Diagonal((b - 0.08, 0.03, 0.12, 1)))
                netz.quader((0, 0, 0), (1, 1, 1), st["Eisen"], Matrix.Translation(fuss + d * (b * 0.3) + q * 0.055 + Vector((0, 0, zz)))
                            @ M @ Matrix.Diagonal((b * 0.55, 0.012, 0.05, 1)))
        # Kranbalken: innen auf einer Konsole am Giebel, außen mit Rolle, Seil und Sack
        zk = z + hoch + 0.42
        innen_x, aussen_x = x - 0.75, x + dicke + 1.45
        kran.alle = True
        kran.quader(((innen_x + aussen_x) / 2, mitte, zk), (aussen_x - innen_x, 0.2, 0.22), holz)
        kran.quader((x - 0.11, mitte, zk - 0.24), (0.22, 0.24, 0.26), holz, ohne=("+x", "+z"))            # Konsole
        kran.quader((innen_x + 0.12, mitte, zk), (0.06, 0.24, 0.3), st["Eisen"])                          # Klammer
        rolle = Vector((aussen_x - 0.25, mitte, zk - 0.3))
        for s in (-1, 1):                                                # Bügel
            kran.quader((rolle.x, mitte + s * 0.06, zk - 0.2), (0.06, 0.02, 0.26), st["Eisen"])
        profil = [(0.15 * math.cos(2 * math.pi * k / 10), 0.15 * math.sin(2 * math.pi * k / 10)) for k in range(10)]
        kran.prisma(profil, -0.035, 0.035, st["Holz"], matrix=Matrix.Translation(rolle) @ Matrix.Rotation(math.radians(90), 4, "Z"))
        sack = Vector((rolle.x + 0.15, mitte, z + 0.75))
        kran.stab(rolle + Vector((0.15, 0, 0)), sack + Vector((0, 0, 0.62)), 0.035, 0.035, st["Seil"])                # mit dem Sack
        kran.stab(rolle + Vector((-0.15, 0, 0)), Vector((rolle.x - 0.15, mitte, z - 1.5)), 0.035, 0.035, st["Seil"])  # zum Ziehen
        kran.stab(sack + Vector((0, 0, 0.55)), sack + Vector((0, 0, 0.66)), 0.03, 0.03, st["Eisen"])                 # Haken
        bs.achteck(kran, st, sack, 0.2, 0.0, 0.45, "Sack", n=7)
        bs.achteck(kran, st, sack, 0.12, 0.45, 0.56, "Sack", n=7)
        # Himmel hinter der Luke
        hx = x + dicke + 3.0
        himmel.vieleck_nach([(hx, self.ym - 7, z - 4), (hx, self.ym + 7, z - 4), (hx, self.ym + 7, self.first + 2),
                             (hx, self.ym - 7, self.first + 2)], (-1, 0, 0), st["Fenster"])
        return Vector((x - 0.1, mitte, z + hoch / 2))


# ─────────────────────────────────────────────────────────────────────────────
#  Prüfbericht, Sichtbarkeit wie im Spiel
# ─────────────────────────────────────────────────────────────────────────────

def raum_von(o):
    while o is not None:
        if o.name.startswith("Raum_"):
            return o.name[5:]
        o = o.parent
    return None


def sichtbar(o, stufe, einrichtung, decke=True, spaene=0, raeume=None):
    """Was das Spiel bei dieser Stufe zeigt (für Vorschau und Prüfung): Stufe{k} genau, Einrichtung nach Kauf, Holztür nur
    ohne Eisentür, Spaene<n> nur der gewählte, Knoten mit extras „stufe“ erst ab dieser Stufe; Kinder folgen den Eltern.
    `decke` False: Decke_… aus (Bilder von oben); `raeume`: nur diese Räume (Namen ohne „Raum_“)."""
    if raeume is not None and raum_von(o) not in raeume:
        return False
    while o is not None:
        n = o.name
        if n.startswith("Stufe") and n[5:].isdigit() and int(n[5:]) != stufe:
            return False
        if n.startswith("Einrichtung_") and not einrichtung:
            return False
        if n == "Holztuer" and einrichtung:
            return False
        if n.startswith("Decke_") and not decke:
            return False
        if n.startswith("Spaene") and n[6:].isdigit() and int(n[6:]) != spaene:
            return False
        if o.type == "MESH" and o.get("stufe", 0) > stufe:
            return False
        o = o.parent
    return True


def _konvex(pkt):
    """Ist das Vieleck (Liste von (u, v)) konvex?"""
    vorzeichen = 0
    for i in range(len(pkt)):
        (ax, ay), (bx, by), (cx, cy) = pkt[i - 2], pkt[i - 1], pkt[i]
        k = (bx - ax) * (cy - by) - (by - ay) * (cx - bx)
        if abs(k) > 1e-9:
            if vorzeichen and (k > 0) != (vorzeichen > 0):
                return False
            vorzeichen = k
    return True


def _schnittflaeche(a, b):
    """Fläche, in der sich zwei konvexe Vielecke (Listen von (u, v), gegen den Uhrzeigersinn) überdecken."""
    def links(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]) >= -1e-12
    stueck = list(a)
    for i in range(len(b)):
        p, q = b[i - 1], b[i]
        neu = []
        for j in range(len(stueck)):
            s, e = stueck[j - 1], stueck[j]
            if links(p, q, e):
                if not links(p, q, s):
                    neu.append(_kreuz(s, e, p, q))
                neu.append(e)
            elif links(p, q, s):
                neu.append(_kreuz(s, e, p, q))
        stueck = neu
        if len(stueck) < 3:
            return 0.0
    return flaeche2d(stueck)


def _kreuz(s, e, p, q):
    dx, dy = e[0] - s[0], e[1] - s[1]
    ex, ey = q[0] - p[0], q[1] - p[1]
    nenner = dx * ey - dy * ex
    t = ((p[0] - s[0]) * ey - (p[1] - s[1]) * ex) / nenner if abs(nenner) > 1e-15 else 0.0
    return (s[0] + t * dx, s[1] + t * dy)


def deckungsgleich(objekte, grenze=0.004):
    """Wie bausteine.deckungsgleich (Flächen in derselben Ebene, gleiche Richtung, die sich überdecken), aber genau: Sind
    beide Flächen konvex, zählt die wirkliche Überdeckung, nicht die der umschließenden Rechtecke – sonst meldete das
    Fischgrätpflaster jeden Ziegel neben einem schrägen Nachbarn. Gibt [(Ort, Objekt A, Objekt B, Fläche)] zurück."""
    import collections
    eimer = collections.defaultdict(list)
    for o in objekte:
        if o.type != "MESH":
            continue
        for p in o.data.polygons:
            if p.area < grenze:
                continue
            n = p.normal
            u = n.cross(Vector((0, 0, 1))).normalized() if abs(n.z) < 0.9 else Vector((1, 0, 0))
            v = n.cross(u)
            pkt = [(q.dot(u), q.dot(v)) for q in (o.data.vertices[i].co for i in p.vertices)]
            if sum(pkt[i - 1][0] * pkt[i][1] - pkt[i][0] * pkt[i - 1][1] for i in range(len(pkt))) < 0:
                pkt.reverse()
            us, vs = [q[0] for q in pkt], [q[1] for q in pkt]
            schluessel = (round(n.x, 2), round(n.y, 2), round(n.z, 2), round(n.dot(p.center), 3))
            eimer[schluessel].append((o.name, p.center.copy(), (min(us), max(us), min(vs), max(vs)), pkt, _konvex(pkt)))
    funde = []
    for liste in eimer.values():
        liste.sort(key=lambda e: e[2][0])
        for i in range(len(liste)):
            a = liste[i]
            for j in range(i + 1, len(liste)):
                b = liste[j]
                if b[2][0] >= a[2][1] - 0.01:
                    break
                du = min(a[2][1], b[2][1]) - max(a[2][0], b[2][0])
                dv = min(a[2][3], b[2][3]) - max(a[2][2], b[2][2])
                if du <= 0.01 or dv <= 0.01 or du * dv <= grenze:
                    continue
                flaeche = _schnittflaeche(a[3], b[3]) if a[4] and b[4] else du * dv
                if flaeche > grenze:
                    funde.append((a[1], a[0], b[0], flaeche))
    return funde


def pruefbericht(objekte, name):
    """Dreiecke je Knoten, sichtbar je Stufe, und deckungsgleiche Flächen je Stand (Stufe, mit und ohne Einrichtung) – mit
    allen Kindern (bewegte Teile unter den Stufen, Späne)."""
    import bpy
    alle = list(dict.fromkeys(objekte + [k for o in objekte for k in o.children_recursive]))
    netze = [o for o in alle if o.type == "MESH"]
    print(f"\n{name}: Dreiecke")
    summe = 0
    for o in netze:
        o.data.calc_loop_triangles()
        summe += len(o.data.loop_triangles)
        print(f"  {o.name:34s} {len(o.data.loop_triangles):6d}")
    print(f"  {'gesamt':34s} {summe:6d}")
    welt = {}
    for o in netze:
        if o.matrix_world != Matrix.Identity(4):
            me = o.data.copy()
            me.transform(o.matrix_world)
            welt[o] = bpy.data.objects.new(o.name, me)
    funde = []
    for stufe in (1, 2, 3):
        for einrichtung in (False, True):
            auswahl = [o for o in netze if sichtbar(o, stufe, einrichtung, spaene=stufe)]
            dreiecke = sum(len(o.data.loop_triangles) for o in auswahl)
            je_raum = {}
            for o in auswahl:
                je_raum[raum_von(o)] = je_raum.get(raum_von(o), 0) + len(o.data.loop_triangles)
            print(f"  sichtbar Stufe {stufe}{' mit aller Einrichtung' if einrichtung else ''}: {dreiecke} Dreiecke  "
                  + ", ".join(f"{r} {n}" for r, n in sorted(je_raum.items(), key=lambda e: str(e[0]))))
            funde += deckungsgleich([welt.get(o, o) for o in auswahl])
    for k in welt.values():
        me = k.data
        bpy.data.objects.remove(k)
        bpy.data.meshes.remove(me)
    funde = list({(round(f[0].x, 2), round(f[0].y, 2), round(f[0].z, 2), f[1], f[2]): f for f in funde}.values())
    if funde:
        print(f"  WARNUNG: {len(funde)} deckungsgleiche Flächen (flimmern im Spiel), größte:")
        for ort, a, b, flaeche in sorted(funde, key=lambda f: -f[3])[:12]:
            print(f"    bei ({ort.x:.2f}, {ort.y:.2f}, {ort.z:.2f})  {a} / {b}  {flaeche:.3f} m²")
    else:
        print("  keine deckungsgleichen Flächen")


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def buehne(schnell, sichtfeld=50.0, lampe=75, glut=260, fuell=()):
    """Cycles, Himmel, Kamera (Sichtfeld senkrecht), ein Licht je Anker mit extras „art“ und Füllichter
    `fuell` = [(x, y, z, Energie, Stufe)] unter den Decken; die Fenster leuchten hell."""
    import bpy
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    bs.cycles_geraet(szene)
    szene.cycles.samples = 24 if schnell else 64
    szene.cycles.use_denoising = True
    szene.view_settings.view_transform = "Standard"
    szene.render.image_settings.color_mode = "RGB"
    szene.render.image_settings.compression = 100
    welt = bpy.data.worlds.new("Himmel")
    try:
        welt.use_nodes = True
    except AttributeError:
        pass
    welt.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.38, 0.45, 1)
    welt.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.3
    szene.world = welt
    kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    kam.data.sensor_fit = "VERTICAL"
    kam.data.angle_y = math.radians(sichtfeld)
    kam.data.clip_start = 0.05
    szene.collection.objects.link(kam)
    szene.camera = kam
    for o in [o for o in szene.objects if o.get("art")]:
        if o["art"] == "Glut":
            l = bpy.data.lights.new("_" + o.name, "POINT")
            l.energy, l.color, l.shadow_soft_size = glut, (1.0, 0.5, 0.22), 0.3
        elif o["art"] == "Lampe":
            l = bpy.data.lights.new("_" + o.name, "POINT")
            l.energy, l.color, l.shadow_soft_size = lampe, (1.0, 0.72, 0.42), 0.15
        else:
            l = bpy.data.lights.new("_" + o.name, "AREA")
            l.energy, l.color, l.size = 110, (0.85, 0.9, 1.0), 0.9
        lo = bpy.data.objects.new("_" + o.name, l)
        lo["stufe"] = o["stufe"]
        lo.matrix_world = o.matrix_world @ Matrix.Rotation(math.radians(90), 4, "X")      # Flächenlicht leuchtet mit −Z
        szene.collection.objects.link(lo)
    for x, y, z, e, stufe in fuell:
        l = bpy.data.lights.new("_Fuell", "AREA")
        l.energy, l.size, l.color = e, 4.0, (1.0, 0.92, 0.8)
        lo = bpy.data.objects.new("_Fuell", l)
        lo.location = (x, y, z)
        lo["stufe"] = stufe
        szene.collection.objects.link(lo)
    p = bs.stoff("Fenster").node_tree.nodes["Principled BSDF"]
    p.inputs["Emission Color"].default_value = (0.8, 0.86, 0.95, 1)
    p.inputs["Emission Strength"].default_value = 2.5
    return szene, kam


def zeigen(szene, stufe, einrichtung, decke=True, spaene=0, raeume=None):
    for o in szene.objects:
        if o.type == "MESH" and not o.name.startswith("_"):
            o.hide_render = not sichtbar(o, stufe, einrichtung, decke, spaene, raeume)
        elif o.type == "LIGHT" and "stufe" in o:
            o.hide_render = o["stufe"] > stufe


def marken(z, raeume):
    """Markierungen für den Grundriss in Höhe z (nur Anker der `raeume`): Arbeitsplätze magenta (Pfeil = Blickrichtung),
    Stellplätze türkis, Werkstücke orange, Blickpunkte gelb (Keil = Sichtfeld waagrecht)."""
    import bpy
    farben = {"Arbeit": (0.95, 0.15, 0.75), "Lager": (0.1, 0.85, 0.85), "Werkstueck": (1.0, 0.55, 0.1), "Blick": (1.0, 0.9, 0.2)}
    stoffe = {k: bs._hilfsstoff("_Marke_" + k, f) for k, f in farben.items()}
    liste = []
    breite = math.atan(math.tan(math.radians(25)) * 16 / 9)
    for o in list(bpy.context.scene.objects):
        art = o.name.split("_")[0]
        if o.type != "EMPTY" or art not in stoffe or raum_von(o) not in raeume:
            continue
        netz = bs.Netz("_Marke_" + o.name)
        t = o.matrix_world
        p = t.translation.copy()
        p.z = z
        m = stoffe[art]
        if art == "Blick":
            vor = (t.to_3x3() @ Vector((0, 1, 0)))
            vor.z = 0
            vor.normalize()
            for s in (-1, 1):
                netz.stab(p, p + Matrix.Rotation(s * breite, 3, "Z") @ vor * 1.4, 0.05, 0.05, m)
            netz.stab(p, p + vor * 0.7, 0.09, 0.09, m)
            netz.quader(p, (0.22, 0.22, 0.05), m)
        elif art == "Lager":
            netz.vieleck_nach([p + Vector((x, y, 0)) for x, y in ((-0.38, -0.38), (0.38, -0.38), (0.38, 0.38), (-0.38, 0.38))], (0, 0, 1), m)
        elif art == "Werkstueck":
            netz.quader(p, (0.16, 0.16, 0.05), m)
        else:
            vor = t.to_3x3() @ Vector((0, 1, 0))
            quer = Vector((-vor.y, vor.x, 0))
            netz.vieleck_nach([p - quer * 0.2 - vor * 0.15, p + vor * 0.45, p + quer * 0.2 - vor * 0.15], (0, 0, 1), m)
        liste.append(netz.objekt())
    return liste


def vorschau(name, blicke, stufen, schnell, erdgeschoss, dachboden, lager, raeume_eg, bild=1, **licht):
    """Bilder nach docs/modelle/: <name>-blicke.png (jeder Blickpunkt in Stufe 3 mit aller Einrichtung und mittleren
    Spänen, 640 × 360), <name>-stufen.png (Erdgeschoss von oben je Stufe, ohne Decken und Dachboden) und
    <name>-grundriss.png (Erdgeschoss und Dachboden mit den Ankern). `erdgeschoss`, `dachboden`: (x0, x1, y0, y1) der
    Ausschnitte von oben, `lager`: der Dachboden (für die Höhe der Marken), `bild`: Bild der Animation „Arbeit“."""
    import bpy
    szene, kam = buehne(schnell, **licht)
    szene.render.resolution_x, szene.render.resolution_y = 640, 360
    szene.render.resolution_percentage = 50 if schnell else 100
    szene.frame_set(bild)
    bilder = []
    zeigen(szene, 3, True, spaene=2)
    for blick in blicke:
        o = bpy.data.objects[blick]
        kam.matrix_world = o.matrix_world @ Matrix.Rotation(math.radians(90), 4, "X")
        bilder.append(bs.rendern_einzeln(f"_{name}-{blick}", blick))
    print(bs.zusammensetzen(name + "-blicke", bilder, spalten=2))
    szene.frame_set(1)
    kam.data.type = "ORTHO"
    kam.data.sensor_fit = "AUTO"
    sonne = bpy.data.objects.new("_Sonne", bpy.data.lights.new("_Sonne", "SUN"))
    sonne.data.energy = 2.5
    sonne.rotation_euler = (math.radians(25), math.radians(-15), 0)
    szene.collection.objects.link(sonne)

    def von_oben(r, rand=1.0, oben=1.6):
        x0, x1, y0, y1 = r
        kam.data.ortho_scale = max(x1 - x0, y1 - y0 + oben) + 2 * rand
        kam.matrix_world = Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2 + oben / 2, 40.0))
        szene.render.resolution_x = szene.render.resolution_y = 760
    von_oben(erdgeschoss)
    teile = []
    for k in (1, 2, 3):
        zeigen(szene, k, False, decke=False, spaene=k, raeume=raeume_eg)
        teile.append(bs.rendern_einzeln(f"_{name}-stufe{k}", f"Stufe {k}: {stufen[k - 1]}"))
    print(bs.zusammensetzen(name + "-stufen", teile, spalten=3))
    teile = []
    liste = marken(6.0, raeume_eg)
    zeigen(szene, 3, True, decke=False, spaene=3, raeume=raeume_eg)
    teile.append(bs.rendern_einzeln(f"_{name}-eg", "Erdgeschoss, Stufe 3 mit aller Einrichtung: Arbeitsplätze magenta, "
                                                   "Werkstücke orange, Blickpunkte gelb"))
    for o in liste:
        bpy.data.objects.remove(o)
    von_oben(dachboden)
    liste = marken(lager.z + 2.6, {"Lager"})
    zeigen(szene, 3, True, decke=False, raeume={"Lager"})
    teile.append(bs.rendern_einzeln(f"_{name}-dg", "Dachboden (Lager, ohne Dach): Stellplätze türkis, Blickpunkte gelb"))
    for o in liste:
        bpy.data.objects.remove(o)
    print(bs.zusammensetzen(name + "-grundriss", teile, spalten=2))
