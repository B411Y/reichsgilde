"""Füllung Paket 2 – Kleinmodelle für Restflächen (Garten, Holzplatz, Heuschober, Hof, Kirchhof, Bildstock).

    blender -b --python tools/modelle/fuellung_p2.py -- [--schnell] [--ohne-bilder] [--nur=garten]

Schreibt sechs Sammeldateien nach game/assets/modelle/props/ (fuellung_garten.glb, fuellung_holzplatz.glb,
fuellung_heuschober.glb, fuellung_hof.glb, fuellung_kirchhof.glb, fuellung_bildstock.glb) und je ein Vorschaubild
docs/modelle/fuellung_<name>.png (alle Teile nebeneinander, oben die Sommerfassung, unten die Winterfassung).

Jedes Teil ist ein Knoten mit Ursprung in der Mitte am Boden, Maße in Metern, Z oben beim Bauen. Wo eine Schneehaube
sinnvoll ist, gibt es einen zweiten Knoten `<Name>_Winter` (derselbe Ort, das Spiel zeigt je Jahreszeit einen von beiden).
Keine Hausfarbe, keine Zustände, keine Anker. Materialien, die die Material-Bibliothek kennt (Holz alt, Rinde, Stroh, Heu,
Sandstein, Bruchstein, Eisen, Fels …), bekommen im Spiel ihre Oberflächentextur; neue Namen (Erde, Kohl, Schnee …) bleiben
einfarbig, ihre Form trägt die Struktur.

Alles deterministisch (Zufall nur aus festen Saaten). Nichts stammt aus Fremdmodellen.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Matrix, Vector, bpy  # noqa: E402

bs.FARBEN.update({
    "Weide": (0.50, 0.40, 0.25),            # Flechtruten
    "Salbei": (0.50, 0.57, 0.44),
    "Fenchel": (0.40, 0.55, 0.30),
    "Lauch": (0.33, 0.48, 0.34),
    "Lauch weiss": (0.84, 0.86, 0.76),
    "Bohne": (0.29, 0.43, 0.19),
    "Bohnenhuelse": (0.46, 0.52, 0.24),
    "Schnee": (0.91, 0.93, 0.96),
    "Schnee schatten": (0.78, 0.82, 0.89),
    "Eis": (0.66, 0.77, 0.82),
    "Mist": (0.25, 0.18, 0.12),
    "Meilererde": (0.20, 0.15, 0.12),
    "Meilererde hell": (0.26, 0.20, 0.15),
    "Schlacke": (0.19, 0.19, 0.20),
    "Schlacke rot": (0.34, 0.21, 0.17),
    "Leinen": (0.78, 0.74, 0.62),
    "Leinen blau": (0.30, 0.36, 0.52),
    "Leinen braun": (0.46, 0.35, 0.25),
    "Scherbe": (0.56, 0.32, 0.22),
    "Knochen": (0.80, 0.77, 0.67),
    "Spaene": (0.72, 0.60, 0.42),
})
PROFIL_RUND = ((1.0, 0.0), (0.92, 0.45), (0.62, 0.82), (0.26, 1.0))


def rng(s):
    return random.Random(s * 7919 + 13)


# ─────────────────────────────────────────────────────────────────────────────
#  Grundformen
# ─────────────────────────────────────────────────────────────────────────────

def fein(profil):
    """Profil verfeinern: zwischen je zwei Ringen ein weiterer (rundere Silhouette, mehr Dreiecke)."""
    neu = [profil[0]]
    for a, b in zip(profil, profil[1:]):
        neu.append(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2))
        neu.append(b)
    return tuple(neu)


def kuppel(netz, st, mitte, rx, ry, h, stoffe, profil=PROFIL_RUND, n=12, saat=0, jitter=0.1, krater=None, muster="zufall", verfeinert=True):
    """Unregelmäßiger Haufen: Ringe aus n Ecken, Flächen wechseln durch `stoffe`. Gibt die Ringe zurück.
    `krater` = (Anteil, Tiefe, Stoff): oben ein Loch statt der Kappe."""
    z = rng(saat)
    zs = rng(saat + 99)
    x, y, z0 = mitte
    if verfeinert:
        profil = fein(profil)
    ringe = []
    for fr, fz in profil:
        ring = []
        for k in range(n):
            w = 2 * math.pi * (k + z.uniform(-0.15, 0.15)) / n
            f = fr * (1 + z.uniform(-jitter, jitter))
            hz = h * fz * (1 + z.uniform(-jitter, jitter) * 0.5) if fz else 0.0
            ring.append(Vector((x + rx * f * math.cos(w), y + ry * f * math.sin(w), z0 + hz)))
        ringe.append(ring)
    innen = Vector((x, y, z0 + h * 0.3))
    for i in range(len(ringe) - 1):
        for k in range(n):
            q = [ringe[i][k], ringe[i][(k + 1) % n], ringe[i + 1][(k + 1) % n], ringe[i + 1][k]]
            netz.vieleck_nach(q, sum(q, Vector()) / 4 - innen, st[stoffe[k % len(stoffe)] if muster == "streifen" else zs.choice(stoffe)])
    if krater:
        anteil, tiefe, stoff = krater
        rand = ringe[-1]
        mi = sum(rand, Vector()) / n
        tief = [mi + (p - mi) * anteil - Vector((0, 0, tiefe)) for p in rand]
        for k in range(n):
            q = [rand[k], rand[(k + 1) % n], tief[(k + 1) % n], tief[k]]
            netz.vieleck_nach(q, mi - sum(q, Vector()) / 4 + Vector((0, 0, 0.3)), st[stoff])
        netz.vieleck_nach(tief, (0, 0, 1), st[stoff])
    else:
        netz.vieleck_nach(ringe[-1], (0, 0, 1), st[stoffe[0]])
    return ringe


def schnee(netz, st, mitte, rx, ry, h, saat=0, n=11):
    """Schneehaube: flache Kuppel, unten leicht bläulich."""
    kuppel(netz, st, mitte, rx, ry, h, ("Schnee", "Schnee", "Schnee schatten"), profil=((1.0, 0.0), (0.93, 0.5), (0.62, 0.86), (0.26, 1.0)),
           n=n, saat=saat, jitter=0.08)


def schneestreifen(netz, st, x0, x1, y, z, breite, h):
    """Schneewulst entlang X (auf einem Zaun, Dachfirst, Brett)."""
    b = breite / 2
    netz.prisma([(-b, 0), (-b * 0.8, h * 0.7), (-b * 0.35, h), (b * 0.35, h), (b * 0.8, h * 0.7), (b, 0)], x0, x1, st["Schnee"],
                matrix=Matrix.Translation((0, y, z)))


def strich(netz, st, a, b, d, stoff):
    netz.stab(a, b, d, d, st[stoff])


def halm(netz, st, a, b, stoff="Stroh", d=0.012):
    nt.ast(netz, st, a, b, d, d * 0.5, stoff=stoff, n=3)


def scheit(netz, st, x, zc, y0, y1, r, z, rinde):
    """Liegendes Scheit entlang Y von y0 bis y1, Mitte bei (x, zc); Stirnholz hell."""
    n = 5
    w0 = z.uniform(0, 6.28)
    profil = [(r * math.cos(w0 + 2 * math.pi * k / n), r * math.sin(w0 + 2 * math.pi * k / n)) for k in range(n)]
    t = Matrix.Translation((x, y0, zc)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    netz.prisma(profil, 0, y1 - y0, st["Rinde" if rinde else "Holz"], matrix=t, m_enden=st["Scheitholz"])


def pfahl(netz, st, x, y, h, r=0.04, stoff="Holz alt"):
    nt.ast(netz, st, (x, y, 0), (x, y, h), r, r * 0.85, stoff=stoff, n=5)


def gabel(netz, st, spitze, richtung, laenge=1.5):
    """Heugabel in einem Haufen: Stiel von der Spitze (im Haufen) schräg nach oben, Zinken am Fuß."""
    p = Vector(spitze)
    d = Vector(richtung).normalized()
    netz.stab(p + d * 0.25, p + d * laenge, 0.035, 0.035, st["Holz"])
    quer = d.cross(Vector((0, 0, 1))).normalized()
    for s in (-1, 0, 1):
        netz.stab(p + quer * s * 0.06 + d * 0.3, p + quer * s * 0.06 - d * 0.1, 0.014, 0.014, st["Eisen"])
    netz.stab(p + quer * -0.08 + d * 0.3, p + quer * 0.08 + d * 0.3, 0.02, 0.02, st["Eisen"])


# ─────────────────────────────────────────────────────────────────────────────
#  Garten
# ─────────────────────────────────────────────────────────────────────────────

def flechtzaun(netz, st, laenge, saat, winter=False):
    """Flechtzaun: Pfähle alle 0,5 m, Weidenruten abwechselnd vor und hinter den Pfählen."""
    z = rng(saat)
    anzahl = int(round(laenge / 0.5)) + 1
    xs = [-laenge / 2 + laenge * i / (anzahl - 1) for i in range(anzahl)]
    for x in xs:
        h = z.uniform(0.95, 1.08)
        nt.ast(netz, st, (x + z.uniform(-0.012, 0.012), z.uniform(-0.012, 0.012), 0), (x, 0, h), 0.034, 0.026, stoff="Holz alt", n=5)
    for lage in range(7):
        zz = 0.1 + lage * 0.125 + z.uniform(-0.01, 0.01)
        for i in range(anzahl - 1):
            seite = 1 if (i + lage) % 2 == 0 else -1
            dz = z.uniform(-0.012, 0.012)
            netz.stab((xs[i] - 0.03, seite * 0.036, zz), (xs[i + 1] + 0.03, seite * 0.036, zz + dz), 0.05, 0.036, st["Weide"])
    if winter:
        schneestreifen(netz, st, -laenge / 2 + 0.02, laenge / 2 - 0.02, 0, 0.88, 0.14, 0.07)
        for x in xs[::2]:
            schnee(netz, st, (x, 0, 1.0), 0.05, 0.05, 0.05, saat=int(x * 10) + 40, n=5)
        for s in (-1, 1):
            schneestreifen(netz, st, -laenge / 2, laenge / 2, s * 0.1, 0.0, 0.14, 0.1)


def beet_grund(netz, st, laenge=2.0, breite=1.1, furchen=True):
    netz.quader((0, 0, 0.07), (laenge - 0.06, breite - 0.06, 0.14), st["Erde"], ohne=("-z",))
    for sy in (-1, 1):
        netz.quader((0, sy * (breite / 2 - 0.015), 0.1), (laenge, 0.03, 0.2), st["Holz alt"], ohne=("-z",))
    for sx in (-1, 1):
        netz.quader((sx * (laenge / 2 - 0.015), 0, 0.1), (0.03, breite - 0.06, 0.2), st["Holz alt"], ohne=("-z",))
        for sy in (-1, 1):
            nt.ast(netz, st, (sx * laenge / 2, sy * breite / 2, 0), (sx * laenge / 2, sy * breite / 2, 0.29), 0.035, 0.03, stoff="Holz alt", n=4)
    if furchen:
        for y in (-0.3, 0.0, 0.3):
            netz.prisma([(y - 0.12, 0), (y - 0.07, 0.07), (y + 0.07, 0.07), (y + 0.12, 0)], -laenge / 2 + 0.05, laenge / 2 - 0.05,
                        st["Erde hell"], matrix=Matrix.Translation((0, 0, 0.14)))


def beet_kraut(netz, st, saat):
    z = rng(saat)
    beet_grund(netz, st)
    for ri, y in enumerate((-0.3, 0.0, 0.3)):
        for k in range(5):
            p = Vector((-0.8 + 0.4 * k + z.uniform(-0.04, 0.04), y + z.uniform(-0.03, 0.03), 0.2))
            if ri == 0:                                                  # Salbei: graugrüne Blätter im Kreis
                for a in range(6):
                    w = 2 * math.pi * a / 6 + z.uniform(0, 0.8)
                    nt.blatt(netz, st, p, (math.cos(w), math.sin(w)), z.uniform(0.2, 0.3), 0.1, "Salbei", hoch=0.4)
            elif ri == 1:                                                # Petersilie: dichter Busch
                w0 = z.uniform(0, 3)
                lb.kegel(netz, (p.x, p.y), 0.17, 0.2, 0.36, st["Kraut"], st["Laub hell"], n=7, dreh=w0)
                lb.kegel(netz, (p.x, p.y), 0.1, 0.3, 0.46, st["Laub hell"], st["Kraut"], n=6, dreh=w0 + 1)
            else:                                                        # Dill: dünn und hoch mit gelber Dolde
                nt.ast(netz, st, p, p + Vector((z.uniform(-0.03, 0.03), 0, 0.55)), 0.012, 0.008, stoff="Fenchel", n=3)
                lb.kegel(netz, (p.x, p.y), 0.14, 0.28, 0.55, st["Fenchel"], st["Kraut"], n=6, dreh=z.uniform(0, 3))
                nt.tupfen(netz, st, p + Vector((0, 0, 0.58)), 0.07, "Ginster", n=5)


def beet_kohl(netz, st, saat):
    z = rng(saat)
    beet_grund(netz, st)
    for y in (-0.3, 0.3):
        for k in range(5):
            x = -0.9 + 0.45 * k + z.uniform(-0.04, 0.04)
            gross = 0.95 + z.uniform(0, 0.15)
            nt.krone(netz, st, (x, y, 0.2 + 0.13 * gross), 0.17 * gross, 0.14 * gross, saat=saat + k * 3 + int(y * 10), stoffe=("Kohl", "Laub hell"),
                     n=7, flach_unten=0.2)
            for a in range(5):
                w = 2 * math.pi * a / 5 + z.uniform(0, 1)
                nt.blatt(netz, st, Vector((x, y, 0.2)), (math.cos(w), math.sin(w)), 0.32 * gross, 0.2, "Laub", hoch=0.3)
    for k in range(4):                                                   # Jungpflanzen in der Mitte
        lb.kegel(netz, (-0.65 + 0.43 * k, 0.0), 0.1, 0.2, 0.3, st["Kohl"], st["Laub"], n=5, dreh=k)


def beet_lauch(netz, st, saat, winter=False, nur_pflanzen=False):
    z = rng(saat)
    for y in (-0.3, 0.0, 0.3):
        for k in range(8):
            p = Vector((-0.87 + 0.25 * k + z.uniform(-0.03, 0.03), y + z.uniform(-0.03, 0.03), 0.2 if not winter else 0.24))
            hohe = z.uniform(0.18, 0.26)
            nt.ast(netz, st, p, p + Vector((0, 0, hohe)), 0.022, 0.026, stoff="Lauch weiss", n=4)
            for a in range(4 if not winter else 3):
                w = 2 * math.pi * a / 4 + z.uniform(0, 1.2)
                nt.blatt(netz, st, p + Vector((0, 0, hohe)), (math.cos(w), math.sin(w)), z.uniform(0.3, 0.4), 0.055, "Lauch", hoch=1.7)


def beet_winter(netz, st, saat, art):
    z = rng(saat)
    beet_grund(netz, st, furchen=False)
    L = 2.0
    netz.prisma([(-0.49, 0), (-0.44, 0.05), (-0.3, 0.095), (0.3, 0.095), (0.44, 0.05), (0.49, 0)], -L / 2 + 0.04, L / 2 - 0.04,
                st["Schnee"], matrix=Matrix.Translation((0, 0, 0.14)))
    for sy in (-1, 1):
        schneestreifen(netz, st, -L / 2, L / 2, sy * (0.55 - 0.015), 0.2, 0.06, 0.03)
    if art == "lauch":
        beet_lauch(netz, st, saat, winter=True)
    elif art == "kraut":
        for k in range(4):                                               # trockene Dillstängel
            x, y = -0.7 + 0.45 * k, z.uniform(-0.25, 0.25)
            nt.ast(netz, st, (x, y, 0.2), (x + 0.03, y, 0.55), 0.011, 0.008, stoff="Stoppel", n=3)
            schnee(netz, st, (x + 0.03, y, 0.53), 0.06, 0.06, 0.03, saat=k + 8, n=5)
    else:
        for k in range(3):                                               # Kohlstrünke mit Schneekappe
            x, y = -0.6 + 0.6 * k, z.uniform(-0.2, 0.2)
            nt.ast(netz, st, (x, y, 0.2), (x, y, 0.3), 0.05, 0.04, stoff="Laub", n=5)
            schnee(netz, st, (x, y, 0.3), 0.11, 0.11, 0.07, saat=k + 5, n=6)


def bohnenstange(netz, st, saat, winter=False):
    z = rng(saat)
    spitze = Vector((0, 0, 2.05))
    kuppel(netz, st, (0, 0, 0), 0.6, 0.6, 0.1, ("Erde", "Erde hell"), n=8, saat=saat, jitter=0.12)
    for k in range(4):
        w = 2 * math.pi * k / 4 + 0.4
        fuss = Vector((0.5 * math.cos(w), 0.5 * math.sin(w), 0.0))
        nt.ast(netz, st, fuss, spitze + Vector((z.uniform(-0.02, 0.02), z.uniform(-0.02, 0.02), 0)), 0.03, 0.017, stoff="Holz alt", n=4)
    nt.ast(netz, st, (0, 0, 1.95), (0, 0, 2.12), 0.06, 0.05, stoff="Seil", n=5)          # Bund oben
    nt.ast(netz, st, (0, 0, 2.05), (0.0, 0.03, 2.3), 0.018, 0.012, stoff="Holz alt", n=3)
    for k in range(4):
        w = 2 * math.pi * k / 4 + 0.4
        for lage in range(7):
            f = 0.12 + lage * 0.125                                       # Anteil der Stangenhöhe
            rad = 0.5 * (1 - f)
            hz = 2.05 * f
            p = Vector((rad * math.cos(w), rad * math.sin(w), hz))
            if winter:
                if lage % 3 == 1:
                    nt.blatt(netz, st, p, (math.cos(w + 1.2), math.sin(w + 1.2)), 0.16, 0.07, "Stoppel", hoch=-0.5)
                continue
            for s in (-1, 1):
                a = w + s * (1.1 + z.uniform(-0.2, 0.2))
                nt.blatt(netz, st, p, (math.cos(a), math.sin(a)), z.uniform(0.17, 0.24), 0.11, "Bohne", hoch=0.1)
            if lage % 2 == 0 and lage > 0:
                q = p + Vector((0.05 * math.cos(w + 0.6), 0.05 * math.sin(w + 0.6), -0.02))
                nt.ast(netz, st, q, q + Vector((0, 0, -0.15)), 0.014, 0.01, stoff="Bohnenhuelse", n=3)
    if winter:
        schnee(netz, st, (0, 0, 0.03), 0.62, 0.62, 0.13, saat=saat, n=9)
        schnee(netz, st, (0, 0, 2.28), 0.05, 0.05, 0.05, saat=saat + 1, n=5)


def waescheleine(netz, st, saat):
    z = rng(saat)
    L = 3.6
    for sx in (-1, 1):
        x = sx * L / 2
        nt.ast(netz, st, (x, 0, 0), (x, 0, 1.95), 0.052, 0.04, stoff="Holz alt", n=6)
        nt.ast(netz, st, (x, 0, 1.78), (x - sx * 0.1, 0, 2.05), 0.025, 0.02, stoff="Holz alt", n=4)       # Astgabel
        kuppel(netz, st, (x, 0, 0), 0.2, 0.2, 0.06, ("Erde", "Erde hell"), n=6, saat=sx + 9)

    def zl(x):
        return 1.92 - 0.26 * (1 - (2 * x / L) ** 2)

    anz = 10
    for i in range(anz):
        x0, x1 = -L / 2 + L * i / anz, -L / 2 + L * (i + 1) / anz
        strich(netz, st, (x0, 0, zl(x0)), (x1, 0, zl(x1)), 0.016, "Seil")
    stuecke = [
        (-1.3, "Leinen", [(-0.17, 0), (0.17, 0), (0.46, 0.1), (0.48, 0.5), (0.3, 0.5), (0.2, 0.28), (0.2, 0.82), (-0.2, 0.82), (-0.2, 0.28),
                          (-0.3, 0.5), (-0.48, 0.5), (-0.46, 0.1)], 0.12),
        (-0.55, "Leinen braun", [(-0.2, 0), (0.2, 0), (0.22, 0.85), (0.04, 0.85), (0.0, 0.4), (-0.04, 0.85), (-0.22, 0.85)], -0.08),
        (0.25, "Leinen", [(-0.45, 0), (0.45, 0), (0.46, 0.78), (0.3, 0.86), (0.15, 0.78), (0.0, 0.88), (-0.15, 0.78), (-0.3, 0.86), (-0.46, 0.78)], 0.05),
        (1.05, "Leinen blau", [(-0.22, 0), (0.22, 0), (0.27, 0.3), (0.36, 0.98), (-0.36, 0.98), (-0.27, 0.3)], -0.1),
        (1.5, "Lumpen", [(-0.14, 0), (0.14, 0), (0.15, 0.38), (-0.15, 0.4)], 0.2),
    ]
    for x, stoff, umriss, yaw in stuecke:
        t = Matrix.Translation((x, 0, zl(x) + 0.02)) @ Matrix.Rotation(yaw, 4, "Z")
        pts = [t @ Vector((u, 0, -v)) for u, v in umriss]
        off = t.to_3x3() @ Vector((0, 0.007, 0))
        netz.vieleck_nach([p - off for p in pts], t.to_3x3() @ Vector((0, -1, 0)), st[stoff])
        netz.vieleck_nach([p + off for p in pts], t.to_3x3() @ Vector((0, 1, 0)), st[stoff])
        for u in (umriss[0][0] * 0.8, umriss[1][0] * 0.8):                # Klammern
            netz.quader((u, 0, -0.02), (0.022, 0.04, 0.08), st["Holz"], t)


# ─────────────────────────────────────────────────────────────────────────────
#  Holzplatz und Werkhof
# ─────────────────────────────────────────────────────────────────────────────

def holzstapel(netz, st, spalten, reihen, tiefe, saat, dach=False, winter=False):
    z = rng(saat)
    d = 0.19
    breite = spalten * d
    for ri in range(reihen):
        for c in range(spalten - (ri % 2)):
            x = -breite / 2 + d / 2 + c * d + (d / 2 if ri % 2 else 0)
            zc = 0.095 + ri * d * 0.9
            r = z.uniform(0.072, 0.092)
            scheit(netz, st, x + z.uniform(-0.01, 0.01), zc, -tiefe / 2 + z.uniform(-0.04, 0.0), tiefe / 2 + z.uniform(0.0, 0.04), r, z,
                   z.random() < 0.45)
    hoehe = 0.095 + (reihen - 1) * d * 0.9 + 0.09
    for sx in (-1, 1):
        for sy in (-1, 1):
            pfahl(netz, st, sx * (breite / 2 + 0.07), sy * (tiefe / 2 - 0.02), hoehe + 0.1, 0.045)
    netz.stab((-breite / 2 - 0.12, tiefe / 2 + 0.03, 0.02), (breite / 2 + 0.12, tiefe / 2 + 0.03, 0.02), 0.04, 0.04, st["Holz alt"])
    if dach:
        t = Matrix.Translation((0, 0, hoehe + 0.3)) @ Matrix.Rotation(math.radians(9), 4, "X")
        netz.quader((0, 0, 0), (breite + 0.5, tiefe + 0.5, 0.05), st["Bretter"], t)
        for sx in (-1, 1):
            for sy in (-1, 1):
                pfahl(netz, st, sx * (breite / 2 + 0.07), sy * (tiefe / 2 - 0.02), hoehe + 0.3 + (0.05 if sy > 0 else 0.0), 0.045)
    if winter:
        if dach:
            t = Matrix.Translation((0, 0, hoehe + 0.345)) @ Matrix.Rotation(math.radians(9), 4, "X")
            netz.quader((0, 0, 0), (breite + 0.4, tiefe + 0.4, 0.07), st["Schnee"], t)
        else:
            schneestreifen(netz, st, -breite / 2, breite / 2, 0, hoehe - 0.05, tiefe * 0.95, 0.09)
    return hoehe


def hackklotz(netz, st, saat, winter=False):
    z = rng(saat)
    lb.saeule(netz, st, (0, 0, 0), 0.27, 0, 0.55, "Rinde", enden="Scheitholz", n=9)
    for k in range(5):                                               # Wurzelanläufe
        w = 2 * math.pi * k / 5 + 0.3
        nt.ast(netz, st, (0.2 * math.cos(w), 0.2 * math.sin(w), 0.15), (0.34 * math.cos(w), 0.34 * math.sin(w), 0.02), 0.05, 0.02, stoff="Rinde", n=3)
    netz.quader((-0.1, -0.04, 0.5535), (0.34, 0.016, 0.004), st["Loch"])                   # Risse in der Hiebfläche
    netz.quader((-0.14, 0.12, 0.5535), (0.012, 0.22, 0.004), st["Loch"])
    # Beil: Kopf im Holz, Stiel schräg nach oben
    a = Vector((0.13, 0, 0.74))
    d = Vector((math.sin(math.radians(28)), 0, math.cos(math.radians(28))))
    netz.stab(a, a + d * 0.7, 0.04, 0.035, st["Holz"])
    netz.quader((0.1, 0, 0.64), (0.17, 0.035, 0.22), st["Eisen"])
    netz.quader((0.19, 0, 0.7), (0.07, 0.05, 0.08), st["Eisen"])
    for k, (x, y0) in enumerate(((0.55, -0.6), (0.72, -0.1), (-0.6, -0.7))):                 # Scheite am Boden
        scheit(netz, st, x, 0.07, y0, y0 + 0.42, 0.07, z, k % 2 == 0)
    for k in range(14):                                              # Späne
        x, y = z.uniform(-0.8, 0.8), z.uniform(-0.8, 0.8)
        if math.hypot(x, y) < 0.3:
            continue
        w = z.uniform(0, 6.28)
        l, b = 0.09, 0.03
        p = [Vector((x + math.cos(w) * l * sx + math.sin(w) * b * sy, y + math.sin(w) * l * sx - math.cos(w) * b * sy, 0.004))
             for sx, sy in ((-1, -1), (1, -0.3), (1, 0.3), (-1, 1))]
        netz.vieleck_nach(p, (0, 0, 1), st["Spaene"])
    if winter:
        schnee(netz, st, (-0.06, 0.04, 0.55), 0.22, 0.2, 0.07, saat=saat, n=7)
        schnee(netz, st, (0, 0, 0.0), 0.95, 0.95, 0.03, saat=saat + 2, n=10)


def scheitwand(netz, st, saat, winter=False):
    z = rng(saat)
    spalten, reihen, tiefe = 12, 8, 0.42
    d = 0.19
    breite = spalten * d
    for ri in range(reihen):
        for c in range(spalten - (ri % 2)):
            x = -breite / 2 + d / 2 + c * d + (d / 2 if ri % 2 else 0)
            zc = 0.095 + ri * d * 0.9
            scheit(netz, st, x, zc, -tiefe / 2 + z.uniform(-0.03, 0), tiefe / 2 + z.uniform(0, 0.03), z.uniform(0.07, 0.092), z, z.random() < 0.4)
    hoehe = 0.095 + (reihen - 1) * d * 0.9 + 0.09
    for sx in (-1, 0, 1):
        for sy in (-1, 1):
            if sx == 0 and sy == 1:
                continue
            pfahl(netz, st, sx * (breite / 2 + 0.07) if sx else 0.0, sy * (tiefe / 2 + 0.02), hoehe + 0.2, 0.05)
    for sy in (-1, 1):                                               # Rindenstücke als Abdeckung
        for k in range(3):
            netz.stab((-breite / 2 - 0.1 + k * breite / 3, sy * 0.12, hoehe + 0.015), (-breite / 2 + 0.1 + (k + 1) * breite / 3, sy * 0.12, hoehe + 0.03),
                      0.24, 0.03, st["Rinde"])
    if winter:
        schneestreifen(netz, st, -breite / 2 - 0.1, breite / 2 + 0.1, 0, hoehe + 0.04, 0.5, 0.1)


def kohlenmeiler(netz, st, saat):
    z = rng(saat)
    rx = 2.0
    ringe = kuppel(netz, st, (0, 0, 0), rx, rx, 1.45, ("Meilererde", "Meilererde", "Meilererde", "Meilererde", "Meilererde hell", "Kohle"),
                   profil=((1.0, 0.0), (0.97, 0.2), (0.84, 0.5), (0.58, 0.8), (0.24, 0.96)), n=18, saat=saat, jitter=0.06, krater=(0.55, 0.2, "Kohle"))
    n = 18
    rund = rx  # Zuglöcher am Fuß: dunkle Soden
    for k in range(0, n, 4):
        p = ringe[2][k]
        lb.kegel(netz, (p.x, p.y), 0.15, p.z - 0.04, p.z + 0.11, st["Loch"], n=4, dreh=z.uniform(0, 1))
    for k in range(1, n, 3):                                         # Grasbüschel an den Rändern der Rasensoden
        p = ringe[0][k]
        bs.buschel(netz, st, Vector((p.x * 1.02, p.y * 1.02, 0)), groesse=0.3, stoff="Gras")
    bs.rund_oben(netz, st, (0, 0, ringe[-1][0].z - 0.2), 0.12, ringe[-1][0].z - 0.205, "Glut", n=6)
    # Leiter an der Schauseite
    w = math.radians(-60)
    fuss = Vector((math.cos(w) * 2.7, math.sin(w) * 2.7, 0))
    kopf = Vector((math.cos(w) * 1.05, math.sin(w) * 1.05, 1.4))
    quer = Vector((-math.sin(w), math.cos(w), 0)) * 0.2
    for s in (-1, 1):
        netz.stab(fuss + quer * s, kopf + quer * s, 0.05, 0.05, st["Holz alt"])
    for k in range(1, 6):
        t = k / 6
        p = fuss.lerp(kopf, t)
        netz.stab(p - quer, p + quer, 0.04, 0.04, st["Holz alt"])
    # Schürhaken und Reisigbündel
    netz.stab((2.7, 1.1, 0.05), (1.6, 2.2, 0.05), 0.04, 0.04, st["Holz alt"])
    netz.stab((2.7, 1.1, 0.07), (2.55, 0.95, 0.07), 0.03, 0.03, st["Eisen"])
    for k in range(5):
        halm(netz, st, (-2.9 - z.uniform(0, 0.3), 1.6 + 0.05 * k, 0.1 + 0.05 * (k % 2)), (-1.9 + z.uniform(-0.1, 0.1), 1.8 + 0.04 * k, 0.12), "Totholz", 0.035)


def schlackehaufen(netz, st, saat, winter=False):
    z = rng(saat)
    ringe = kuppel(netz, st, (0, 0, 0), 1.15, 0.95, 0.62, ("Schlacke", "Schlacke rot", "Schlacke", "Fels dunkel"),
                   profil=((1.0, 0.0), (0.9, 0.38), (0.6, 0.78), (0.28, 1.0)), n=13, saat=saat, jitter=0.18)
    for k in range(8):
        w = z.uniform(0, 6.28)
        r = z.uniform(0.15, 0.95)
        zz = max(0.0, 0.62 * (1 - (r / 1.1) ** 2)) - 0.04
        lb.felsbrocken(netz, st, (r * math.cos(w) * 0.9, r * math.sin(w) * 0.8, zz), z.uniform(0.12, 0.2), z.uniform(0.1, 0.17), saat=saat + k, n=5,
                       stoff="Schlacke", stoff2="Schlacke rot")
    for k in range(3):                                               # Schlackekuchen aus dem Herd
        w = z.uniform(0, 6.28)
        netz.stab((1.2 * math.cos(w) - 0.2, 1.0 * math.sin(w), 0.05), (1.2 * math.cos(w) + 0.2, 1.0 * math.sin(w) + 0.05, 0.07), 0.3, 0.1, st["Schlacke"])
    if winter:
        schnee(netz, st, (0, 0, 0.3), 0.95, 0.8, 0.38, saat=saat, n=9)


def abfallhaufen(netz, st, saat, winter=False):
    z = rng(saat)
    kuppel(netz, st, (0, 0, 0), 0.95, 0.8, 0.38, ("Erde", "Mist", "Erde hell", "Stroh"), profil=((1.0, 0.0), (0.88, 0.5), (0.5, 0.92), (0.2, 1.0)),
           n=13, saat=saat, jitter=0.15)

    def hoehe(x, y):
        d = math.hypot(x / 0.95, y / 0.8)
        return max(0.0, 0.36 * (1 - d * d * 0.9))
    for k in range(7):                                               # Scherben
        x, y = z.uniform(-0.6, 0.6), z.uniform(-0.5, 0.5)
        zz = hoehe(x, y) + 0.02
        w = z.uniform(0, 6.28)
        pts = [Vector((x + 0.07 * math.cos(w + a), y + 0.05 * math.sin(w + a), zz + 0.03 * math.sin(a * 2))) for a in (0, 2.0, 3.6)]
        netz.vieleck_nach(pts, (0, 0, 1), st["Scherbe"])
    for k in range(5):                                               # Knochen
        x, y = z.uniform(-0.55, 0.55), z.uniform(-0.45, 0.45)
        zz = hoehe(x, y) + 0.04
        w = z.uniform(0, 6.28)
        halm(netz, st, (x, y, zz), (x + 0.16 * math.cos(w), y + 0.16 * math.sin(w), zz + 0.03), "Knochen", 0.016)
    for k in range(2):                                               # Lumpen
        x, y = z.uniform(-0.4, 0.4), z.uniform(-0.35, 0.35)
        zz = hoehe(x, y) + 0.025
        netz.vieleck_nach([(x - 0.14, y - 0.06, zz), (x + 0.12, y - 0.1, zz + 0.02), (x + 0.15, y + 0.09, zz), (x - 0.06, y + 0.12, zz + 0.03)],
                          (0, 0, 1), st["Lumpen"])
    nt.blatt(netz, st, Vector((0.2, -0.3, hoehe(0.2, -0.3))), (1, 0.4), 0.22, 0.14, "Kohl", hoch=0.2)
    for k in range(6):
        x, y = z.uniform(-0.7, 0.7), z.uniform(-0.55, 0.55)
        if hoehe(x, y) > 0.02:
            w = z.uniform(0, 6.28)
            halm(netz, st, (x, y, hoehe(x, y)), (x + 0.3 * math.cos(w), y + 0.3 * math.sin(w), hoehe(x, y) + 0.1), "Stroh")
    if winter:
        schnee(netz, st, (-0.1, 0.05, 0.12), 0.72, 0.6, 0.27, saat=saat, n=8)


def geruempel(netz, st, saat, winter=False):
    z = rng(saat)
    # Fass, kaputt, liegend
    R = 0.36
    fass_mitte = Vector((0.0, 0.0, R + 0.01))
    stellen = (-0.45, 0.0, 0.45)
    wolbung = (0.0, 0.1, 0.0)
    fehlend = {2, 3, 4}
    for i in range(12):
        phi = 2 * math.pi * i / 12
        if i in fehlend:
            continue
        rad = Vector((0, math.cos(phi), math.sin(phi)))
        kurz = i in (1, 5)
        for j in range(2):
            x0, x1 = stellen[j], stellen[j + 1]
            if kurz and j == 1:
                x1 = 0.25
            r0, r1 = R * (1 + wolbung[j]), R * (1 + wolbung[j + 1])
            a = fass_mitte + Vector((x0, 0, 0)) + rad * r0
            b = fass_mitte + Vector((x1, 0, 0)) + rad * r1
            netz.stab(a, b, 0.135, 0.028, st["Holz"], quer=tuple(rad))
    kopf = [fass_mitte + Vector((-0.43, 0.33 * math.cos(2 * math.pi * k / 12), 0.33 * math.sin(2 * math.pi * k / 12))) for k in range(12)]
    netz.vieleck_nach(kopf, (-1, 0, 0), st["Holz alt"])                                      # Boden (Deckel) an einem Ende
    for xr, voll in ((-0.4, True), (0.38, False)):                                           # Eisenreifen, einer halb weg
        rr = R * (1.02 + (0.0 if abs(xr) < 0.2 else 0.0))
        for k in range(12):
            if not voll and k in (2, 3, 4, 5):
                continue
            p0 = 2 * math.pi * k / 12
            p1 = 2 * math.pi * (k + 1) / 12
            a = fass_mitte + Vector((xr, rr * math.cos(p0), rr * math.sin(p0)))
            b = fass_mitte + Vector((xr, rr * math.cos(p1), rr * math.sin(p1)))
            netz.stab(a, b, 0.045, 0.02, st["Eisen"], quer=(0, math.cos((p0 + p1) / 2), math.sin((p0 + p1) / 2)))
    # Wagenrad, schräg angelehnt
    Rr = 0.52
    T = Matrix.Translation((-0.5, 0.75, Rr * math.cos(math.radians(18)) + 0.02)) @ Matrix.Rotation(math.radians(-18), 4, "X") @ Matrix.Rotation(math.radians(20), 4, "Z")
    for k in range(8):
        a0, a1 = 2 * math.pi * k / 8, 2 * math.pi * (k + 1) / 8
        pa = Vector((Rr * math.cos(a0), 0, Rr * math.sin(a0)))
        pb = Vector((Rr * math.cos(a1), 0, Rr * math.sin(a1)))
        netz.stab(pa, pb, 0.09, 0.055, st["Holz alt"], matrix=T, quer=(math.cos((a0 + a1) / 2), 0, math.sin((a0 + a1) / 2)), mehr=0.01)
        netz.stab(pa * 1.04, pb * 1.04, 0.095, 0.012, st["Eisen"], matrix=T, quer=(math.cos((a0 + a1) / 2), 0, math.sin((a0 + a1) / 2)), mehr=0.012) if k != 5 else None
    for k in range(8):
        if k in (2, 6):
            continue                                                      # zwei Speichen gebrochen
        a = 2 * math.pi * k / 8 + 0.2
        netz.stab(Vector((0.08 * math.cos(a), 0, 0.08 * math.sin(a))), Vector((Rr * 0.97 * math.cos(a), 0, Rr * 0.97 * math.sin(a))), 0.04, 0.04,
                  st["Holz alt"], matrix=T)
    for k in (2, 6):
        a = 2 * math.pi * k / 8 + 0.2
        netz.stab(Vector((0.08 * math.cos(a), 0, 0.08 * math.sin(a))), Vector((0.2 * math.cos(a), 0, 0.2 * math.sin(a))), 0.04, 0.04, st["Holz alt"], matrix=T)
    netz.prisma([(0.11 * math.cos(2 * math.pi * k / 8), 0.11 * math.sin(2 * math.pi * k / 8)) for k in range(8)], -0.07, 0.07, st["Holz"],
                matrix=T @ Matrix.Rotation(math.radians(90), 4, "Z") @ Matrix.Rotation(math.radians(90), 4, "X"))
    # Bretter
    for (x, y, l, dreh, h) in ((0.9, -0.5, 1.3, 0.35, 0.0), (0.7, -0.7, 1.1, -0.2, 0.04), (-0.9, -0.6, 0.9, 1.2, 0.0)):
        t = Matrix.Translation((x, y, 0.02 + h)) @ Matrix.Rotation(dreh, 4, "Z")
        netz.quader((0, 0, 0.0), (l, 0.2, 0.035), st["Bretter"], t)
    netz.stab((0.85, 0.2, 0.0), (0.5, 0.45, 0.62), 0.2, 0.035, st["Bretter"])                  # Brett an das Fass gelehnt
    if winter:
        schnee(netz, st, (0, 0, R * 2 - 0.1), 0.4, 0.3, 0.07, saat=saat, n=7)
        schneestreifen(netz, st, 0.1, 1.0, -0.5, 0.04, 0.2, 0.05)
        schnee(netz, st, (-0.5, 0.75, 1.0), 0.1, 0.06, 0.05, saat=saat + 1, n=5)


# ─────────────────────────────────────────────────────────────────────────────
#  Heuschober
# ─────────────────────────────────────────────────────────────────────────────

def heuhaufen(netz, st, saat, winter=False):
    z = rng(saat)
    ringe = kuppel(netz, st, (0, 0, 0), 1.3, 1.15, 1.45, ("Heu", "Heu", "Stroh"),
                   profil=((0.9, 0.0), (1.0, 0.25), (0.85, 0.58), (0.52, 0.86), (0.2, 1.0)), n=18, saat=saat, jitter=0.05, muster="streifen")
    for k in range(20):                                              # Halmbüschel, die herausstehen
        p = ringe[z.randrange(2, len(ringe) - 2)][z.randrange(18)]
        w = math.atan2(p.y, p.x) + z.uniform(-0.4, 0.4)
        halm(netz, st, p, p + Vector((0.13 * math.cos(w), 0.13 * math.sin(w), z.uniform(-0.04, 0.08))), "Heu" if k % 2 else "Stroh")
    gabel(netz, st, (0.55, -0.55, 0.95), (0.55, -0.4, 1.0), 1.7)
    if winter:
        schnee(netz, st, (0, 0, 0.85), 0.78, 0.7, 0.62, saat=saat, n=10)


def strohballen(netz, st, saat, winter=False):
    """Gebundene Strohbündel (Quaderballen gab es um 1400 nicht): zwei liegen, eines obenauf."""
    z = rng(saat)

    def buendel(mitte, laenge, r, dreh, kopf_stroh=True):
        t = Matrix.Translation(mitte) @ Matrix.Rotation(dreh, 4, "Z")
        profil = [(r * math.cos(2 * math.pi * k / 9 + 0.2), r * math.sin(2 * math.pi * k / 9 + 0.2)) for k in range(9)]
        netz.prisma(profil, -laenge / 2, laenge / 2, st["Stroh"], matrix=t, m_enden=st["Strohkante"])
        for xr in (-laenge * 0.25, laenge * 0.25):
            netz.prisma([(p[0] * 1.05, p[1] * 1.05) for p in profil], xr - 0.035, xr + 0.035, st["Seil"], matrix=t)
        for s in (-1, 1):                                            # Halme am Ende
            for k in range(6):
                a = z.uniform(0, 6.28)
                e = t @ Vector((s * laenge / 2, r * 0.7 * math.cos(a), r * 0.7 * math.sin(a)))
                f = t @ Vector((s * (laenge / 2 + z.uniform(0.07, 0.16)), r * 0.85 * math.cos(a), r * 0.85 * math.sin(a)))
                halm(netz, st, e, f)
    r = 0.27
    buendel(Vector((0, -0.28, r + 0.01)), 1.0, r, 0.03)
    buendel(Vector((0.05, 0.3, r + 0.01)), 1.0, r, -0.06)
    buendel(Vector((-0.02, 0.0, 2 * r * 0.88 + 0.28)), 1.0, r, 0.1)
    for k in range(7):
        x, y = z.uniform(-0.9, 0.9), z.uniform(-0.8, 0.8)
        w = z.uniform(0, 6.28)
        halm(netz, st, (x, y, 0.01), (x + 0.3 * math.cos(w), y + 0.3 * math.sin(w), 0.02), "Stroh")
    if winter:
        schnee(netz, st, (0, 0, 0.76), 0.45, 0.45, 0.07, saat=saat, n=7)
        for s in (-1, 1):
            schneestreifen(netz, st, -0.45, 0.45, -0.28 if s < 0 else 0.3, 0.5, 0.2, 0.06)


def strohmiete(netz, st, saat, winter=False):
    z = rng(saat)
    for k in range(8):                                               # Unterlage aus Schwellen
        w = math.pi * k / 8
        netz.stab((1.3 * math.cos(w), 1.3 * math.sin(w), 0.065 + 0.006 * k), (-1.3 * math.cos(w), -1.3 * math.sin(w), 0.065 + 0.006 * k), 0.14, 0.11, st["Holz alt"])
    ringe = kuppel(netz, st, (0, 0, 0.17), 1.25, 1.25, 2.5, ("Stroh", "Heu", "Stroh", "Strohkante"),
                   profil=((0.92, 0.0), (1.0, 0.1), (1.0, 0.28), (0.9, 0.44), (0.62, 0.68), (0.3, 0.9), (0.12, 0.98)), n=16, saat=saat, jitter=0.03,
                   muster="streifen")
    n = 16
    for k in range(0, n, 4):                                         # Seile über den Haufen
        for i in range(5, len(ringe) - 1):
            a, b = ringe[i][k] * 1.03, ringe[i + 1][k] * 1.03
            strich(netz, st, a + Vector((0, 0, 0.0)), b, 0.03, "Seil")
    for k in range(40):                                              # Halmenden
        i = z.randrange(2, 10)
        p = ringe[i][z.randrange(n)]
        w = math.atan2(p.y, p.x)
        halm(netz, st, p, p + Vector((0.16 * math.cos(w), 0.16 * math.sin(w), -0.12)), "Strohkante")
    top = ringe[-1][0].z
    nt.ast(netz, st, (0, 0, top - 0.7), (0, 0, top + 0.85), 0.045, 0.03, stoff="Holz alt", n=5)             # Mittelstange ragt heraus
    lb.kegel(netz, (0, 0), 0.22, top - 0.1, top + 0.18, st["Stroh"], st["Strohkante"], n=6)                 # Strohkrone um die Stange
    if winter:
        schnee(netz, st, (0, 0, 1.45), 0.9, 0.9, 1.25, saat=saat, n=14)
        schnee(netz, st, (0, 0, top + 0.82), 0.06, 0.06, 0.05, saat=saat + 1, n=5)


# ─────────────────────────────────────────────────────────────────────────────
#  Hof und Vieh
# ─────────────────────────────────────────────────────────────────────────────

def trog(netz, st, saat, winter=False):
    L = 1.9
    aussen = [(-0.3, 0.46), (-0.31, 0.2), (-0.22, 0.04), (0.22, 0.04), (0.31, 0.2), (0.3, 0.46), (0.23, 0.46), (0.23, 0.2), (0.15, 0.12), (-0.15, 0.12),
              (-0.23, 0.2), (-0.23, 0.46)]
    netz.prisma(aussen, -L / 2, L / 2, st["Holz"], m_enden=st["Scheitholz"])
    for x in (-0.6, 0.6):                                           # Eisenbänder
        for s in (-1, 1):
            netz.quader((x, s * 0.312, 0.28), (0.06, 0.012, 0.36), st["Eisen"])
    for s in (-1, 1):                                               # Griffe
        netz.quader((s * (L / 2 + 0.02), 0, 0.4), (0.06, 0.18, 0.05), st["Holz"])
    if winter:
        netz.vieleck_nach([(-0.9, -0.15, 0.36), (0.9, -0.15, 0.36), (0.9, 0.15, 0.36), (-0.9, 0.15, 0.36)], (0, 0, 1), st["Eis"])
        schneestreifen(netz, st, -L / 2, L / 2, -0.265, 0.46, 0.09, 0.05)
        schneestreifen(netz, st, -L / 2, L / 2, 0.265, 0.46, 0.09, 0.05)
    else:
        netz.vieleck_nach([(-0.9, -0.16, 0.38), (0.9, -0.16, 0.38), (0.9, 0.16, 0.38), (-0.9, 0.16, 0.38)], (0, 0, 1), st["Wasser"])


def futtertrog(netz, st, saat):
    L = 1.7
    netz.prisma([(-0.3, 0.7), (-0.2, 0.36), (0.2, 0.36), (0.3, 0.7), (0.25, 0.7), (0.17, 0.41), (-0.17, 0.41), (-0.25, 0.7)], -L / 2, L / 2, st["Holz alt"],
                m_enden=st["Scheitholz"])
    for sx in (-1, 1):
        netz.quader((sx * (L / 2 - 0.03), 0, 0.57), (0.05, 0.46, 0.34), st["Holz alt"], ohne=("-z",))
        for sy in (-1, 1):                                          # Böcke
            nt.ast(netz, st, (sx * 0.65, sy * 0.4, 0), (sx * 0.65, sy * 0.15, 0.4), 0.04, 0.035, stoff="Holz alt", n=4)
        netz.stab((sx * 0.65, -0.15, 0.36), (sx * 0.65, 0.15, 0.36), 0.06, 0.06, st["Holz alt"])
    kuppel(netz, st, (0, 0, 0.38), 0.7, 0.2, 0.3, ("Heu", "Stroh", "Heu"), n=8, saat=saat, jitter=0.12, profil=((1.0, 0.0), (0.85, 0.55), (0.45, 0.95), (0.15, 1.0)))
    z = rng(saat)
    for k in range(10):
        x = z.uniform(-0.7, 0.7)
        halm(netz, st, (x, z.uniform(-0.1, 0.1), 0.62), (x + z.uniform(-0.1, 0.1), z.uniform(-0.25, 0.25), 0.55), "Heu")
    for k in range(6):
        halm(netz, st, (z.uniform(-1, 1), z.uniform(-0.7, 0.7), 0.01), (z.uniform(-1, 1), z.uniform(-0.7, 0.7), 0.02), "Heu")


def pferch_huerde(netz, st, saat, winter=False):
    z = rng(saat)
    L, H = 2.0, 1.0
    for sx in (-1, 1):
        nt.ast(netz, st, (sx * (L / 2), 0, 0), (sx * (L / 2), 0, H + 0.12), 0.045, 0.03, stoff="Holz alt", n=5)
        lb.kegel(netz, (sx * L / 2, 0), 0.032, H + 0.12, H + 0.2, st["Holz alt"], n=5)
    for zz in (0.18, 0.55, 0.95):
        netz.stab((-L / 2, 0.04, zz), (L / 2, 0.04, zz), 0.055, 0.04, st["Holz"])
    netz.stab((-L / 2 + 0.05, -0.035, 0.15), (L / 2 - 0.05, -0.035, 0.9), 0.05, 0.035, st["Holz alt"])
    for k in range(8):                                              # senkrechte Weidenruten, abwechselnd davor und dahinter
        x = -L / 2 + 0.2 + k * (L - 0.4) / 7
        strich(netz, st, (x, -0.02 if k % 2 else 0.065, 0.2), (x + z.uniform(-0.01, 0.01), -0.02 if k % 2 else 0.065, 0.97), 0.03, "Weide")
    for sx in (-1, 1):                                              # Fußstützen
        netz.stab((sx * (L / 2 - 0.05), 0, 0.012), (sx * (L / 2 - 0.05), 0.45, 0.012), 0.06, 0.05, st["Holz alt"])
        nt.ast(netz, st, (sx * (L / 2 - 0.05), 0.4, 0.02), (sx * (L / 2 - 0.02), 0.0, 0.35), 0.03, 0.025, stoff="Holz alt", n=4)
    if winter:
        schneestreifen(netz, st, -L / 2 + 0.05, L / 2 - 0.05, 0.04, 0.975, 0.1, 0.06)
        for sx in (-1, 1):
            schnee(netz, st, (sx * L / 2, 0, H + 0.14), 0.04, 0.04, 0.04, saat=sx + 5, n=5)


def misthaufen(netz, st, saat, winter=False):
    z = rng(saat)
    ringe = kuppel(netz, st, (0, -0.1, 0), 1.25, 0.95, 0.78, ("Mist", "Mist", "Mist", "Mist", "Erde", "Stroh"), profil=((1.0, 0.0), (0.9, 0.35), (0.62, 0.78), (0.3, 1.0)),
                   n=15, saat=saat, jitter=0.07)
    for k in range(22):
        w = z.uniform(0, 6.28)
        r = z.uniform(0.0, 1.0)
        x, y = 1.15 * r * math.cos(w), -0.1 + 0.85 * r * math.sin(w)
        zz = max(0.0, 0.75 * (1 - r * r)) - 0.03
        halm(netz, st, (x, y, zz), (x + 0.3 * math.cos(w), y + 0.25 * math.sin(w), zz + 0.12), "Stroh")
    for sx in (-1, 0, 1):                                            # Brettwand dahinter
        pfahl(netz, st, sx * 1.3, 1.0, 0.8, 0.05)
    for zz in (0.3, 0.6):
        netz.stab((-1.3, 1.05, zz), (1.3, 1.05, zz), 0.2, 0.035, st["Bretter"])
    gabel(netz, st, (0.35, -0.4, 0.55), (0.35, 0.5, 1.0), 1.7)
    if winter:
        schnee(netz, st, (0.2, 0.2, 0.28), 0.75, 0.5, 0.18, saat=saat, n=8)
        schneestreifen(netz, st, -1.35, 1.35, 1.05, 0.8, 0.14, 0.06)


# ─────────────────────────────────────────────────────────────────────────────
#  Kirchhof
# ─────────────────────────────────────────────────────────────────────────────

def huegel(netz, st, rx, ry, h, saat, z0=0.0, dx=0.0, dy=0.0):
    kuppel(netz, st, (dx, dy, z0), rx, ry, h, ("Gras", "Gras", "Gras", "Gras", "Gras", "Moos"), profil=((1.0, 0.0), (0.9, 0.5), (0.5, 0.92), (0.2, 1.0)), n=12, saat=saat,
           jitter=0.07)


def grabkreuz_holz(netz, st, saat, winter=False):
    z = rng(saat)
    huegel(netz, st, 0.5, 1.0, 0.22, saat)
    y = 0.75
    t = Matrix.Translation((0, y, 0)) @ Matrix.Rotation(math.radians(2.0), 4, "X")
    netz.quader((0, 0, 0.7), (0.075, 0.075, 1.4), st["Holz alt"], t)
    netz.quader((0, 0, 1.08), (0.6, 0.065, 0.075), st["Holz alt"], t)
    netz.prisma([(-0.17, 0), (0.17, 0), (0, 0.15)], -0.26, 0.26, st["Schindel"], matrix=Matrix.Translation((0, y, 1.4)) @ Matrix.Rotation(math.radians(2.0), 4, "X"),
                m_enden=st["Holz alt"])
    netz.quader((0, -0.004, 1.28), (0.14, 0.012, 0.1), st["Fuge"], t)                              # Namenstäfelchen
    for k in range(3):
        bs.buschel(netz, st, Vector((z.uniform(-0.3, 0.3), z.uniform(-0.9, 0.2), 0.08)), groesse=0.16, stoff="Gras")
    if winter:
        schnee(netz, st, (0, 0, 0.02), 0.52, 1.02, 0.17, saat=saat, n=10)
        netz.prisma([(-0.11, 0), (0.0, 0.1), (0.11, 0)], -0.24, 0.24, st["Schnee"], matrix=Matrix.Translation((0, y, 1.5)) @ Matrix.Rotation(math.radians(2.0), 4, "X"))
        schneestreifen(netz, st, -0.3, 0.3, y, 1.1, 0.05, 0.03)


def grabkreuz_stein(netz, st, saat, winter=False):
    z = rng(saat)
    huegel(netz, st, 0.5, 1.0, 0.16, saat)
    y = 0.7
    t = Matrix.Translation((0, y, 0)) @ Matrix.Rotation(math.radians(-1.8), 4, "X")
    netz.quader((0, 0, 0.1), (0.62, 0.34, 0.2), st["Sandstein"], t)
    netz.quader((0, 0, 0.3), (0.5, 0.26, 0.2), st["Sandstein"], t)
    netz.quader((0, 0, 0.78), (0.17, 0.12, 0.76), st["Sandstein"], t)
    netz.quader((0, 0, 1.0), (0.62, 0.12, 0.17), st["Sandstein"], t)
    netz.quader((0, 0, 1.12), (0.24, 0.12, 0.06), st["Sandstein"], t)                               # Kopfstück
    netz.quader((0, -0.062, 0.8), (0.05, 0.012, 0.35), st["Fuge"], t)                              # eingeritztes Kreuz
    netz.quader((0, -0.062, 0.88), (0.2, 0.012, 0.04), st["Fuge"], t)
    for xm, zm, w in ((-0.2, 0.06, 0.22), (0.22, 0.12, 0.14), (0.0, 0.58, 0.12)):                       # Moos
        netz.quader((xm, -0.175 if zm < 0.3 else -0.065, zm), (w, 0.012, 0.09), st["Moos"], t)
    for k in range(3):
        bs.buschel(netz, st, Vector((z.uniform(-0.35, 0.35), z.uniform(-0.9, 0.2), 0.08)), groesse=0.16, stoff="Gras")
    if winter:
        schnee(netz, st, (0, 0, 0.02), 0.52, 1.02, 0.12, saat=saat, n=10)
        netz.quader((0, 0, 0.405), (0.54, 0.28, 0.03), st["Schnee"], t)
        netz.quader((0, 0, 1.095), (0.64, 0.14, 0.035), st["Schnee"], t)
        netz.quader((0, 0, 1.16), (0.26, 0.13, 0.03), st["Schnee"], t)


def grabplatte(netz, st, saat, winter=False):
    z = rng(saat)
    t = Matrix.Translation((0, 0, 0.0)) @ Matrix.Rotation(math.radians(1.5), 4, "Y") @ Matrix.Rotation(math.radians(1.0), 4, "X")
    netz.prisma([(-0.43, 0), (0.43, 0), (0.43, 0.1), (0.38, 0.14), (-0.38, 0.14), (-0.43, 0.1)], -0.95, 0.95, st["Sandstein"], matrix=t)
    h = 0.1415
    def streifen(xm, ym, sx, sy):
        netz.quader((xm, ym, h + 0.002), (sx, sy, 0.004), st["Fuge"], t)
    for ym in (-0.34, 0.34):                                        # Umschrift: Rahmen
        streifen(0, ym, 1.7, 0.03)
    for xm in (-0.8, 0.8):
        streifen(xm, 0, 0.03, 0.7)
    streifen(0, 0.0, 0.05, 0.4)                                     # Kreuz
    streifen(0, 0.08, 0.26, 0.05)
    for k in range(3):                                              # Schriftzeilen (angedeutet)
        streifen(0.4, -0.15 + 0.15 * k, 0.4 - 0.08 * k, 0.03)
    for k in range(3):
        streifen(-0.4, -0.15 + 0.15 * k, 0.4 - 0.1 * (k % 2), 0.03)
    for xm, ym, a, b in ((-0.7, -0.3, 0.3, 0.2), (0.75, 0.3, 0.2, 0.15)):                            # Moos
        netz.vieleck_nach([t @ Vector((xm - a / 2, ym - b / 2, h + 0.006)), t @ Vector((xm + a / 2, ym - b / 2, h + 0.006)),
                           t @ Vector((xm + a / 3, ym + b / 2, h + 0.006))], (0, 0, 1), st["Moos"])
    for k in range(5):
        a = z.uniform(0, 6.28)
        bs.buschel(netz, st, Vector((1.1 * math.cos(a), 0.6 * math.sin(a), 0)), groesse=0.16, stoff="Gras")
    if winter:
        netz.prisma([(-0.4, 0), (-0.36, 0.035), (0.36, 0.035), (0.4, 0)], -0.9, 0.9, st["Schnee"], matrix=Matrix.Translation((0, 0, 0.14)) @ t.to_3x3().to_4x4())


# ─────────────────────────────────────────────────────────────────────────────
#  Bildstock
# ─────────────────────────────────────────────────────────────────────────────

def bildstock(netz, st, saat, winter=False):
    z = rng(saat)
    netz.quader((0, 0, 0.07), (1.2, 1.2, 0.14), st["Bruchstein"], ohne=("-z",))
    netz.quader((0, 0, 0.21), (0.95, 0.95, 0.14), st["Bruchstein"])
    netz.quader((0, 0, 0.4), (0.62, 0.62, 0.24), st["Sandstein"])                                     # Sockel
    netz.quader((0, 0, 0.55), (0.7, 0.7, 0.06), st["Sandstein"])                                      # Sockelgesims
    netz.quader((0, 0, 1.08), (0.34, 0.34, 1.0), st["Sandstein"])                                     # Schaft
    for sx in (-1, 1):                                                                              # Fasen am Schaft
        for sy in (-1, 1):
            netz.stab((sx * 0.175, sy * 0.175, 0.6), (sx * 0.175, sy * 0.175, 1.58), 0.03, 0.03, st["Haustein"])
    netz.quader((0, 0, 1.62), (0.5, 0.5, 0.08), st["Sandstein"])                                      # Auflager des Gehäuses
    # Gehäuse mit Nische in der Vorderseite (−Y)
    g0, g1 = 1.66, 2.06
    netz.quader((-0.19, 0, (g0 + g1) / 2), (0.12, 0.44, g1 - g0), st["Sandstein"])
    netz.quader((0.19, 0, (g0 + g1) / 2), (0.12, 0.44, g1 - g0), st["Sandstein"])
    netz.quader((0, 0.19, (g0 + g1) / 2), (0.26, 0.06, g1 - g0), st["Sandstein"])
    netz.quader((0, -0.19, g0 + 0.04), (0.26, 0.06, 0.08), st["Sandstein"])
    netz.quader((0, -0.19, g1 - 0.05), (0.26, 0.06, 0.1), st["Sandstein"])
    netz.quader((0, 0.162, (g0 + g1) / 2), (0.26, 0.012, g1 - g0 - 0.1), st["Loch"])                  # dunkle Rückwand der Nische
    lb.kegel(netz, (0, -0.02), 0.07, g0 + 0.09, g0 + 0.25, st["Stadtweiss"], st["Leinen blau"], n=6)  # Marienfigur: Gewand
    lb.saeule(netz, st, (0, -0.02, 0), 0.04, g0 + 0.25, g0 + 0.31, "Stadtweiss", n=6)                  # Kopf
    netz.quader((0, 0, g1 + 0.025), (0.62, 0.62, 0.05), st["Sandstein"])                              # Dachplatte
    lb.kegel(netz, (0, 0), 0.43, g1 + 0.05, g1 + 0.4, st["Sandstein"], st["Haustein"], n=4, dreh=math.radians(45))   # Pyramidendach
    netz.stab((0, 0, g1 + 0.38), (0, 0, g1 + 0.62), 0.04, 0.04, st["Eisen"])                          # Kreuz
    netz.stab((-0.07, 0, g1 + 0.54), (0.07, 0, g1 + 0.54), 0.035, 0.035, st["Eisen"])
    for xm, zm, w in ((0.13, 0.7, 0.12), (-0.1, 0.62, 0.08), (0.05, 0.3, 0.2)):                         # Moos vorn
        netz.quader((xm, -0.176 if zm > 0.55 else -0.31, zm if zm > 0.55 else 0.42), (w, 0.012, 0.1), st["Moos"])
    for k in range(5):
        a = z.uniform(0, 6.28)
        bs.buschel(netz, st, Vector((0.8 * math.cos(a), 0.8 * math.sin(a), 0)), groesse=0.18, stoff="Gras")
    if winter:
        netz.quader((0, 0, 0.145), (1.1, 1.1, 0.025), st["Schnee"])
        netz.quader((0, 0, 0.292), (0.85, 0.85, 0.025), st["Schnee"])
        netz.quader((0, 0, 0.59), (0.72, 0.72, 0.03), st["Schnee"])
        netz.quader((0, 0, 1.675), (0.52, 0.52, 0.03), st["Schnee"])
        schnee(netz, st, (0, 0, g1 + 0.2), 0.34, 0.34, 0.15, saat=saat, n=8)
        schnee(netz, st, (0, 0, g1 + 0.58), 0.05, 0.05, 0.04, saat=saat + 1, n=5)
        netz.quader((0, -0.19, g0 + 0.095), (0.25, 0.05, 0.02), st["Schnee"])


def wegkreuz_holz(netz, st, saat, winter=False):
    z = rng(saat)
    for i, (dx, dz, s) in enumerate(((0, 0.0, 1.0), (0, 0.18, 0.78))):                                # Steinsockel: zwei Stufen
        netz.quader((0, 0, dz + 0.09), (0.9 * s, 0.9 * s, 0.18), st["Bruchstein"], ohne=("-z",))
    netz.quader((0, 0, 0.42), (0.42, 0.42, 0.12), st["Haustein"])
    netz.quader((0, 0, 1.45), (0.17, 0.17, 2.1), st["Holz alt"])                                      # Pfosten
    netz.quader((0, 0, 1.95), (0.9, 0.13, 0.13), st["Holz alt"])                                      # Querbalken
    netz.quader((0, 0.09, 1.8), (0.62, 0.025, 0.85), st["Bretter"])                                   # Rückbrett
    # Christusfigur
    netz.quader((0, -0.09, 1.78), (0.11, 0.05, 0.5), st["Leder"])
    netz.stab((-0.34, -0.09, 1.93), (0.34, -0.09, 1.93), 0.05, 0.045, st["Leder"])
    netz.stab((-0.03, -0.09, 1.55), (0.0, -0.09, 1.9), 0.035, 0.035, st["Leder"])
    lb.saeule(netz, st, (0, -0.09, 0), 0.07, 1.98, 2.1, "Leder", n=6)
    netz.quader((0, -0.095, 1.62), (0.2, 0.05, 0.14), st["Leinen"])                                   # Tuch
    # Dach: Satteldach aus Schindeln über dem Balken
    t = Matrix.Translation((0, 0, 2.5))
    netz.prisma([(-0.38, 0), (0.38, 0), (0.0, 0.3)], -0.58, 0.58, st["Schindel"], matrix=t, m_enden=st["Holz alt"])
    netz.quader((0, 0, 2.26), (0.9, 0.05, 0.28), st["Bretter"])
    for sx in (-1, 1):                                                                              # Konsolen unter dem Dach
        netz.stab((sx * 0.4, -0.02, 2.12), (sx * 0.15, -0.02, 2.5), 0.05, 0.05, st["Holz alt"])
    for k in range(4):
        bs.buschel(netz, st, Vector((z.uniform(-0.8, 0.8), z.uniform(-0.8, 0.8), 0)), groesse=0.18, stoff="Gras")
    if winter:
        netz.quader((0, 0, 0.19), (0.95, 0.95, 0.03), st["Schnee"])
        netz.quader((0, 0, 0.375), (0.8, 0.8, 0.03), st["Schnee"])
        netz.prisma([(-0.4, 0), (-0.28, 0.08), (0.28, 0.08), (0.4, 0)], -0.6, 0.6, st["Schnee"], matrix=Matrix.Translation((0, 0, 2.77)) @ Matrix.Rotation(0, 4, "Z"))
        schneestreifen(netz, st, -0.6, 0.6, 0.0, 2.84, 0.1, 0.06)


# ─────────────────────────────────────────────────────────────────────────────
#  Zusammenstellung
# ─────────────────────────────────────────────────────────────────────────────

STOFFE = None


def knoten(teile):
    """Baut aus [(Name, Bauer, Argumente, hat_winter)] die Objekte. Sommer zuerst, dann die Winterfassungen."""
    global STOFFE
    bs.neue_szene()
    STOFFE = bs.Stoffe()
    sommer, winter = [], []
    for name, bauer, args, kw, hat_winter in teile:
        n = bs.Netz(name)
        bauer(n, STOFFE, *args, **kw)
        sommer.append(n.objekt())
        if hat_winter:
            w = bs.Netz(name + "_Winter")
            kw2 = dict(kw)
            kw2["winter"] = True
            bauer(w, STOFFE, *args, **kw2)
            winter.append(w.objekt())
    return sommer, winter


GRUPPEN = {
    "garten": [
        ("Flechtzaun2", flechtzaun, (2.0, 11), {}, True),
        ("Flechtzaun4", flechtzaun, (4.0, 12), {}, True),
        ("BeetKraut", beet_kraut, (21,), {}, False),
        ("BeetKohl", beet_kohl, (22,), {}, False),
        ("BeetLauch", None, (23,), {}, True),
        ("Bohnenstange", bohnenstange, (24,), {}, True),
        ("Waescheleine", waescheleine, (25,), {}, False),
    ],
    "holzplatz": [
        ("HolzstapelKlein", holzstapel, (6, 4, 0.9, 31), {}, True),
        ("HolzstapelGross", holzstapel, (12, 7, 1.1, 32), {"dach": True}, True),
        ("Hackklotz", hackklotz, (33,), {}, True),
        ("Scheitwand", scheitwand, (34,), {}, True),
        ("Kohlenmeiler", kohlenmeiler, (35,), {}, False),
        ("Schlackehaufen", schlackehaufen, (36,), {}, True),
        ("Abfallhaufen", abfallhaufen, (37,), {}, True),
        ("Geruempel", geruempel, (38,), {}, True),
    ],
    "heuschober": [
        ("Heuhaufen", heuhaufen, (41,), {}, True),
        ("Strohballen", strohballen, (42,), {}, True),
        ("Strohmiete", strohmiete, (43,), {}, True),
    ],
    "hof": [
        ("Trog", trog, (51,), {}, True),
        ("Futtertrog", futtertrog, (52,), {}, False),
        ("PferchHuerde", pferch_huerde, (53,), {}, True),
        ("Misthaufen", misthaufen, (54,), {}, True),
    ],
    "kirchhof": [
        ("GrabkreuzHolz", grabkreuz_holz, (61,), {}, True),
        ("GrabkreuzStein", grabkreuz_stein, (62,), {}, True),
        ("Grabplatte", grabplatte, (63,), {}, True),
    ],
    "bildstock": [
        ("Bildstock", bildstock, (71,), {}, True),
        ("WegkreuzHolz", wegkreuz_holz, (72,), {}, True),
    ],
}


def beet_lauch_knoten(netz, st, saat, winter=False):
    if winter:
        beet_winter(netz, st, saat, "lauch")
    else:
        beet_grund(netz, st)
        beet_lauch(netz, st, saat)


def beet_kohl_knoten(netz, st, saat, winter=False):
    beet_winter(netz, st, saat, "kohl") if winter else beet_kohl(netz, st, saat)


def beet_kraut_knoten(netz, st, saat, winter=False):
    beet_winter(netz, st, saat, "kraut") if winter else beet_kraut(netz, st, saat)


GRUPPEN["garten"][2] = ("BeetKraut", beet_kraut_knoten, (21,), {}, True)
GRUPPEN["garten"][3] = ("BeetKohl", beet_kohl_knoten, (22,), {}, True)
GRUPPEN["garten"][4] = ("BeetLauch", beet_lauch_knoten, (23,), {}, True)


def main():
    nur = [a.split("=", 1)[1] for a in sys.argv if a.startswith("--nur=")]
    for gruppe, teile in GRUPPEN.items():
        if nur and gruppe not in nur:
            continue
        name = "fuellung_" + gruppe
        sommer, winter = knoten(teile)
        alle = sommer + winter
        nt.bericht(name, alle)
        print("geschrieben:", bs.exportieren(name, alle))
        if "--ohne-bilder" in sys.argv:
            continue
        import fuellung_vorschau as fv
        fv.vorschau(name, sommer, winter)


if __name__ == "__main__":
    main()
