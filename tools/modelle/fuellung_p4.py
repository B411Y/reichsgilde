"""Füllung Paket 4 – Kleinmodelle für Restflächen (Steinmetzplatz, Taubenschlag), Teil 1: Modelle.

    blender -b --factory-startup --python tools/modelle/fuellung_p4.py -- [--schnell] [--ohne-bilder] [--nur=steinmetz,taube]

Schreibt zwei Sammeldateien nach game/assets/modelle/props/ (fuellung_steinmetz.glb, fuellung_taube.glb) und je ein Vorschaubild
docs/modelle/fuellung_<name>.png (alle Teile nebeneinander, oben die Sommerfassung, unten die Winterfassung). Aufbau, Maßstab und
Material-Weg wie fuellung_p2.py und fuellung_p3.py: Jedes Teil ist ein Knoten mit Ursprung in der Mitte am Boden, Maße in Metern, Z oben
beim Bauen, zu jedem Teil ein Knoten `<Name>_Winter` mit Schnee. Alles selbst gebaut, nichts aus Fremdquellen; deterministisch.

Stoffe sparen (jeder Stoff ist in der Füllung eine Fläche und kostet zwei Zeichenaufrufe je Zelle): der Steinmetzplatz kommt mit sechs Stoffen aus
(Haustein, Sandstein alt, Holz alt, Eisen, Fels, Seil; Winter dazu Schnee, Schnee schatten), der Taubenschlag mit fünf (Holz alt, Taube, Taube dunkel,
Taube Hals, Taube weiss). Haustein und Sandstein alt tragen die Textur der Material-Bibliothek („quader“).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import fuellung_p2 as p2  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
import tiere as tr  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Taube": (0.47, 0.52, 0.60),                     # blaugrau
    "Taube dunkel": (0.21, 0.23, 0.29),              # Flügelbinden, Schwanzbinde, Schnabel, Füße; auch Dachschiefer, Fluglöcher, Körner
    "Taube Hals": (0.32, 0.44, 0.46),                # Halsschimmer, dezent grün mit einem Hauch Violett
    "Taube weiss": (0.88, 0.88, 0.85),               # weiße und gescheckte Tiere, Wachshaut
})
rng = p2.rng


def lage(x, y, z, rz=0.0, rx=0.0, ry=0.0):
    """Matrix für Quader und Prismen: erst drehen (Z, dann X, dann Y), dann an (x, y, z) setzen."""
    m = Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(rz), 4, "Z")
    if rx:
        m = m @ Matrix.Rotation(math.radians(rx), 4, "X")
    if ry:
        m = m @ Matrix.Rotation(math.radians(ry), 4, "Y")
    return m


def block(netz, st, x, y, z0, lx, ly, hz, stoff, rz=0.0, kante="Sandstein alt"):
    """Behauener Block auf dem Boden (Unterkante z0): Quader mit einer abgeschlagenen Kante oben (kleiner Absatz in anderer Steinart)."""
    netz.quader((0, 0, hz / 2), (lx, ly, hz), st[stoff], lage(x, y, z0, rz))
    netz.quader((0, 0, 0), (lx * 0.42, ly * 0.36, 0.05), st[kante], lage(x, y, z0 + hz + 0.012, rz) @ Matrix.Translation((-lx * 0.26, ly * 0.28, 0)))


def schneehaube(netz, st, x, y, z, rx, ry, h, saat):
    p2.schnee(netz, st, (x, y, z), rx, ry, h, saat=saat, n=6)


# ─────────────────────────────────────────────────────────────────────────────
#  Steinmetzplatz
# ─────────────────────────────────────────────────────────────────────────────

def werkbank(netz, st, saat, winter=False):
    """Steinmetzbank: schwere Holzbank 1,9 × 0,8 m mit unterem Brett; oben ein halb behauener Block, Fäustel, Meißel, Winkel und Steinsplitter."""
    z = rng(saat)
    L, B, H = 1.9, 0.8, 0.82
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader((sx * (L / 2 - 0.1), sy * (B / 2 - 0.1), (H - 0.06) / 2), (0.14, 0.14, H - 0.06), st["Holz alt"])
    for sy in (-1, 1):                                                                    # Zargen
        netz.quader((0, sy * (B / 2 - 0.1), 0.28), (L - 0.3, 0.07, 0.1), st["Holz alt"])
    for sx in (-1, 1):
        netz.quader((sx * (L / 2 - 0.1), 0, 0.28), (0.07, B - 0.3, 0.1), st["Holz alt"])
    netz.quader((0, 0, 0.33), (L - 0.28, B - 0.24, 0.05), st["Holz alt"])               # unteres Brett
    netz.quader((0, 0, H - 0.04), (L, B, 0.1), st["Holz alt"])                            # Platte
    top = H + 0.01
    blockhoehe = 0.34
    netz.quader((0, 0, blockhoehe / 2), (0.62, 0.44, blockhoehe), st["Haustein"], lage(-0.45, 0.02, top, 8))            # Block, links, grob behauen
    netz.quader((0, 0, 0), (0.62, 0.22, 0.06), st["Sandstein alt"], lage(-0.45, -0.1, top + blockhoehe - 0.01, 8))        # Kantenabsatz: vorn abgeschlagen
    netz.quader((0, 0, 0), (0.2, 0.44, 0.08), st["Sandstein alt"], lage(-0.62, 0.02, top + blockhoehe - 0.03, 8))          # rohe Ecke links oben
    netz.quader((0, 0, 0.09), (0.3, 0.26, 0.18), st["Sandstein alt"], lage(0.42, 0.08, top, -14))                           # fertiger kleiner Stein, rechts
    netz.quader((0, 0, 0), (0.32, 0.28, 0.025), st["Haustein"], lage(0.42, 0.08, top + 0.19, -14))                          # sauber glatte Deckfläche
    # Fäustel: Stiel, Kopf
    netz.stab((0.05, -0.2, top + 0.025), (0.46, -0.28, top + 0.03), 0.04, 0.035, st["Holz alt"])
    netz.quader((0, 0, 0), (0.05, 0.16, 0.06), st["Eisen"], lage(0.06, -0.19, top + 0.05, 8))
    # Meißel und Spitzeisen, schräg am Block
    netz.stab((-0.1, 0.12, top + 0.02), (0.08, 0.2, top + 0.05), 0.022, 0.022, st["Eisen"])
    netz.stab((-0.1, 0.28, top + 0.02), (0.12, 0.32, top + 0.03), 0.02, 0.02, st["Eisen"])
    # Winkel: zwei Schenkel
    netz.quader((0, 0, 0), (0.34, 0.035, 0.018), st["Holz alt"], lage(0.22, -0.2, top + 0.012, 4))
    netz.quader((0, 0, 0), (0.035, 0.22, 0.018), st["Holz alt"], lage(0.07, -0.1, top + 0.012, 4))
    for k in range(7):                                                                      # Splitter auf der Platte
        x, y = z.uniform(-0.85, 0.85), z.uniform(-0.3, 0.3)
        netz.quader((0, 0, 0.01), (z.uniform(0.04, 0.09), z.uniform(0.03, 0.07), 0.025), st["Sandstein alt" if k % 2 else "Haustein"], lage(x, y, top, z.uniform(0, 90)))
    # unter der Bank: ein Rohling und ein Sack Sand
    netz.quader((0, 0, 0.1), (0.4, 0.3, 0.2), st["Haustein"], lage(0.35, 0.0, 0.355, 12))
    if winter:
        netz.quader((0.0, 0.0, H + 0.003), (L - 0.04, B - 0.04, 0.04), st["Schnee"])
        netz.quader((0, 0, 0.02), (0.64, 0.46, 0.04), st["Schnee"], lage(-0.45, 0.02, top + blockhoehe + 0.06, 8))
        schneehaube(netz, st, 0.42, 0.08, top + 0.2, 0.17, 0.15, 0.04, saat)


def bloecke_behauen(netz, st, saat, winter=False):
    """Behauene Quader: zwei nebeneinander, einer quer obenauf, dazu zwei Platten an die Seite gelehnt; Sandstein und Haustein gemischt, Kanten bestoßen."""
    z = rng(saat)
    block(netz, st, -0.55, -0.1, 0.0, 0.95, 0.55, 0.42, "Haustein", rz=3)
    block(netz, st, 0.5, 0.0, 0.0, 0.9, 0.52, 0.4, "Sandstein alt", rz=-4, kante="Haustein")
    block(netz, st, -0.1, 0.05, 0.42, 0.8, 0.5, 0.36, "Haustein", rz=82, kante="Sandstein alt")
    # Platten, gegen den Stapel gelehnt (Lehnwinkel um 12°)
    netz.quader((0, 0, 0.45), (0.85, 0.09, 0.9), st["Sandstein alt"], lage(0.0, 0.62, 0.0, -2, rx=-12))
    netz.quader((0, 0, 0.38), (0.6, 0.08, 0.76), st["Haustein"], lage(-0.75, 0.55, 0.0, 6, rx=-10))
    for k in range(5):                                                                           # Splitter am Boden
        netz.quader((0, 0, 0.01), (z.uniform(0.05, 0.12), z.uniform(0.04, 0.09), 0.03), st["Sandstein alt" if k % 2 else "Haustein"],
                    lage(z.uniform(-1.0, 1.0), z.uniform(-0.75, -0.4), 0.002 * k, z.uniform(0, 90)))
    if winter:
        for x, y, zz, lx, ly, rz in ((-0.55, -0.1, 0.42, 0.95, 0.55, 3), (0.5, 0.0, 0.4, 0.9, 0.52, -4)):
            netz.quader((0, 0, 0.02), (lx - 0.04, ly - 0.04, 0.04), st["Schnee"], lage(x, y, zz, rz))
        netz.quader((0, 0, 0.02), (0.8, 0.5, 0.04), st["Schnee"], lage(-0.1, 0.05, 0.78, 82))
        schneehaube(netz, st, -0.1, 0.05, 0.8, 0.3, 0.2, 0.06, saat)


def rohsteine(netz, st, saat, winter=False):
    """Drei rohe Brocken aus dem Bruch, ein paar Bruchstücke dazwischen."""
    z = rng(saat)
    lb.felsbrocken(netz, st, (-0.45, 0.1, 0.0), 0.55, 0.5, saat=saat + 1, n=7, stoff="Fels", stoff2="Sandstein alt")
    lb.felsbrocken(netz, st, (0.5, -0.1, 0.0), 0.62, 0.62, saat=saat + 2, n=8, stoff="Sandstein alt", stoff2="Fels")
    lb.felsbrocken(netz, st, (0.05, 0.62, 0.0), 0.34, 0.3, saat=saat + 3, n=6, stoff="Fels", stoff2="Sandstein alt")
    for k in range(4):
        w = z.uniform(0, 6.28)
        lb.felsbrocken(netz, st, (0.1 + 0.95 * math.cos(w), -0.4 + 0.5 * math.sin(w), 0.0), z.uniform(0.1, 0.18), z.uniform(0.08, 0.14), saat=saat + 10 + k, n=5,
                       stoff="Sandstein alt", stoff2="Fels")
    if winter:
        schneehaube(netz, st, -0.45, 0.1, 0.4, 0.4, 0.38, 0.1, saat)
        schneehaube(netz, st, 0.5, -0.1, 0.52, 0.45, 0.42, 0.11, saat + 1)
        schneehaube(netz, st, 0.05, 0.62, 0.24, 0.22, 0.2, 0.06, saat + 2)


def splitterhaufen(netz, st, saat, winter=False):
    """Haufen aus Steinsplittern und Steinstaub, wo gehauen wird: flach, mit ein paar größeren Bruchstücken obenauf (Stoff Fels: die Block-Textur des Sandsteins sähe auf einem Haufen aus wie ein Panzer)."""
    z = rng(saat)
    p2.kuppel(netz, st, (0, 0, 0), 1.25, 0.95, 0.34, ("Fels", "Fels", "Sandstein alt", "Fels"),
              profil=((1.0, 0.0), (0.9, 0.4), (0.6, 0.82), (0.28, 1.0)), n=20, saat=saat, jitter=0.16)
    for k in range(9):
        w = z.uniform(0, 6.28)
        r = z.uniform(0.05, 0.75)
        zz = max(0.0, 0.34 * (1 - (r / 1.2) ** 2)) - 0.03
        lb.felsbrocken(netz, st, (r * math.cos(w) * 1.0, r * math.sin(w) * 0.8, zz), z.uniform(0.07, 0.15), z.uniform(0.06, 0.11), saat=saat + k, n=5,
                       stoff="Sandstein alt" if k % 3 else "Fels", stoff2="Fels")
    if winter:
        schneehaube(netz, st, 0, 0, 0.18, 1.0, 0.76, 0.2, saat)


def dreibock(netz, st, saat, winter=False):
    """Hebebock: drei Rundhölzer zum Dreibein zusammengebunden, oben eine Seilrolle mit Eisenhaken, daran ein Haustein-Block in der Seilschlinge."""
    z = rng(saat)
    top = Vector((0.0, 0.0, 3.0))
    fuesse = [Vector((1.35 * math.cos(math.radians(a)), 1.35 * math.sin(math.radians(a)), 0.0)) for a in (90, 210, 330)]
    for f in fuesse:
        nt.ast(netz, st, f, top + Vector((f.x * 0.04, f.y * 0.04, -0.05)), 0.075, 0.062, stoff="Holz alt", n=5)
    for i in range(3):                                                                      # Querholz zwischen zwei Beinen, auf 1,2 m
        a, b = fuesse[i].lerp(top, 0.4), fuesse[(i + 1) % 3].lerp(top, 0.4)
        nt.ast(netz, st, a, b, 0.035, 0.035, stoff="Holz alt", n=4)
    netz.quader((0, 0, 3.0), (0.2, 0.2, 0.12), st["Eisen"])                                 # Kopfband
    netz.stab((0, 0, 2.94), (0, 0, 2.7), 0.05, 0.05, st["Eisen"])                            # Aufhängung
    netz.prisma([(0.13 * math.cos(2 * math.pi * k / 8), 0.13 * math.sin(2 * math.pi * k / 8)) for k in range(8)], -0.05, 0.05, st["Holz alt"],
                matrix=Matrix.Translation((0, 0, 2.6)) @ Matrix.Rotation(math.radians(90), 4, "Z"))   # Rolle, Achse quer
    # Seil von der Rolle zum Block (zwei Stränge, Last in der Schlinge)
    blockz = 0.3
    for sx in (-1, 1):
        nt.ast(netz, st, (sx * 0.1, 0, 2.6), (sx * 0.28, 0, blockz + 0.4), 0.014, 0.014, stoff="Seil", n=4)
    netz.stab((-0.34, 0, blockz + 0.4), (0.34, 0, blockz + 0.4), 0.03, 0.03, st["Eisen"])   # Zange
    netz.quader((0, 0, 0), (0.7, 0.5, 0.5), st["Haustein"], lage(0, 0, blockz, 4))             # Last, schwebt knapp über dem Boden
    netz.quader((0, 0, 0), (0.72, 0.52, 0.06), st["Sandstein alt"], lage(0, 0, blockz + 0.5, 4))   # Kantenabsatz
    for sx in (-1, 1):                                                                       # Zangenarme klemmen von der Seite
        netz.quader((sx * 0.36, 0, blockz + 0.42), (0.04, 0.06, 0.16), st["Eisen"])
    # Hunde: Holzkeile unter dem Block
    for sx in (-1, 1):
        netz.prisma([(-0.05, 0.0), (0.05, 0.0), (0.0, 0.2)], sx * 0.2 - 0.05, sx * 0.2 + 0.05, st["Holz alt"], matrix=Matrix.Translation((0.0, 0.0, 0.0)))
    # Seilende am Boden, lose gerollt
    nt.ast(netz, st, (0.9, 0.55, 0.02), (1.2, 0.7, 0.02), 0.016, 0.016, stoff="Seil", n=4)
    nt.ast(netz, st, (1.2, 0.7, 0.02), (1.05, 0.9, 0.04), 0.016, 0.016, stoff="Seil", n=4)
    if winter:
        for i in range(3):
            a, b = fuesse[i].lerp(top, 0.4), fuesse[(i + 1) % 3].lerp(top, 0.4)
            p2.schnee(netz, st, (a + b) / 2 + Vector((0, 0, 0.03)), 0.14, 0.05, 0.03, saat=saat + i, n=5)
        netz.quader((0, 0, 0.02), (0.72, 0.52, 0.04), st["Schnee"], lage(0, 0, blockz + 0.55, 4))


def werkzeugkiste(netz, st, saat, winter=False):
    """Werkzeugkiste des Steinmetzen: Holzkiste mit zurückgeklapptem Deckel, Eisenbändern, Fäustel, Meißeln und Zirkel."""
    B, T, H = 0.85, 0.45, 0.4
    netz.quader((0, 0, 0.03), (B, T, 0.06), st["Holz alt"])                                # Boden
    for sy in (-1, 1):
        netz.quader((0, sy * (T / 2 - 0.02), 0.245), (B, 0.04, H - 0.03), st["Holz alt"])
    for sx in (-1, 1):
        netz.quader((sx * (B / 2 - 0.02), 0, 0.245), (0.04, T - 0.08, H - 0.03), st["Holz alt"])
    for sx in (-0.3, 0.3):                                                                  # Eisenbänder um die Kiste
        netz.quader((sx, 0, 0.235), (0.05, T + 0.01, H + 0.01), st["Eisen"], ohne=("-z",))
    # Deckel zurückgeklappt (hinten, y > 0), steht fast senkrecht
    netz.quader((0, 0, 0), (B, 0.04, T), st["Holz alt"], lage(0, T / 2 + 0.07, H + 0.2, 0, rx=-12))
    for sx in (-0.3, 0.3):
        netz.quader((0, 0, 0), (0.05, 0.07, T + 0.01), st["Eisen"], lage(sx, T / 2 + 0.065, H + 0.2, 0, rx=-12))
    # Inhalt
    netz.stab((-0.3, -0.05, H - 0.12), (0.2, 0.0, H - 0.08), 0.04, 0.035, st["Holz alt"])                        # Fäustelstiel
    netz.quader((0, 0, 0), (0.05, 0.14, 0.06), st["Eisen"], lage(0.2, 0.0, H - 0.08))                           # Fäustelkopf
    for k in range(4):                                                                       # Meißel, Spitzeisen
        netz.stab((-0.34, -0.12 + 0.07 * k, H - 0.1), (0.14 - 0.03 * k, -0.12 + 0.07 * k, H - 0.1), 0.02, 0.02, st["Eisen"])
    netz.stab((0.3, 0.1, H - 0.06), (0.36, 0.18, H + 0.06), 0.015, 0.015, st["Eisen"])                           # Zirkel
    netz.stab((0.36, 0.18, H + 0.06), (0.42, 0.1, H - 0.04), 0.015, 0.015, st["Eisen"])
    if winter:
        netz.quader((0, 0, 0), (B + 0.01, 0.045, T), st["Schnee"], lage(0, T / 2 + 0.11, H + 0.23, 0, rx=-12))
        netz.quader((0, 0, 0.012), (B - 0.12, T - 0.14, 0.03), st["Schnee"], lage(0, 0, H - 0.18))


def saeulenrohling(netz, st, saat, winter=False):
    """Säulenrohling: liegender Schaft von 2,6 m auf zwei Holzkeilen, ein Ende fertig mit Wulstringen, das andere noch roh und dick."""
    n = 8
    r = 0.34
    L = 2.6
    zc = r * 1.05 + 0.1

    def vieleck(radius, dreh):
        return [(radius * math.cos(2 * math.pi * k / n + dreh), radius * math.sin(2 * math.pi * k / n + dreh)) for k in range(n)]
    # Schaft entlang X: Profil (y, z); der Kopf bleibt auf der Mittellinie
    netz.prisma(vieleck(r, math.pi / n), -L / 2 + 0.53, L / 2 - 0.02, st["Haustein"], matrix=Matrix.Translation((0, 0, zc)))
    netz.prisma(vieleck(r * 1.14, 0.1), -L / 2, -L / 2 + 0.55, st["Sandstein alt"], matrix=Matrix.Translation((0, 0, zc + 0.02)))     # roher Teil, dicker
    for x in (L / 2 - 0.18, L / 2 - 0.45, -0.2):                                                 # Wulstringe, Schaftringe
        netz.prisma(vieleck(r * 1.07, math.pi / n), x - 0.05, x + 0.05, st["Sandstein alt"], matrix=Matrix.Translation((0, 0, zc)))
    netz.prisma(vieleck(r * 1.12, math.pi / n), L / 2 - 0.04, L / 2 + 0.06, st["Haustein"], matrix=Matrix.Translation((0, 0, zc)))      # Kopfplatte
    for sx in (-0.7, 0.8):                                                                        # Auflagerkeile aus Holz
        netz.quader((0, 0, 0.1), (0.3, 0.8, 0.2), st["Holz alt"], lage(sx, 0, 0))
        netz.quader((0, 0, 0.2), (0.26, 0.46, 0.04), st["Holz alt"], lage(sx, 0, 0))
    for k in range(5):                                                                            # Steinstaub und Späne
        w = k * 1.3
        netz.quader((0, 0, 0.01), (0.1 + 0.03 * (k % 3), 0.07, 0.025), st["Sandstein alt" if k % 2 else "Haustein"], lage(-1.1 + 0.5 * k, 0.5 * (-1) ** k, 0, w * 30))
    if winter:
        netz.prisma([(-0.17, 0), (-0.14, 0.05), (-0.05, 0.08), (0.05, 0.08), (0.14, 0.05), (0.17, 0)], -L / 2 + 0.1, L / 2, st["Schnee"],
                    matrix=Matrix.Translation((0, 0, zc + r * 0.93)))


# ─────────────────────────────────────────────────────────────────────────────
#  Taubenschlag
# ─────────────────────────────────────────────────────────────────────────────

G_TAUBE = 1.25                                                                               # Taube etwas größer als echt (0,34 m x 1,25), damit sie aus 25 m noch als Taube zu erkennen ist


def _rohr(netz, st, stationen, n, stoff, lat):
    """Körper entlang eines Wegs wie tiere.rohr, aber mit fester Querachse `lat` (Seitenrichtung der Taube): so bleibt die Taube auch gedreht
    breit statt flach. stationen = [(Mitte, Halbachse seitlich, Halbachse hoch)]; stoff(i, k) je Streifen (Ring, Seite)."""
    ringe, mitten, tang = [], [], []
    for i, (c, ru, rv) in enumerate(stationen):
        c = Vector(c)
        t = (Vector(stationen[min(i + 1, len(stationen) - 1)][0]) - Vector(stationen[max(i - 1, 0)][0])).normalized()
        u = (lat - t * lat.dot(t)).normalized()
        v = t.cross(u)
        v = v if v.z >= 0 else -v
        ringe.append([c + u * (ru * math.cos(2 * math.pi * k / n)) + v * (rv * math.sin(2 * math.pi * k / n)) for k in range(n)])
        mitten.append(c)
        tang.append(t)
    for i in range(len(stationen) - 1):
        m = (mitten[i] + mitten[i + 1]) / 2
        for k in range(n):
            j = (k + 1) % n
            quad = [ringe[i][k], ringe[i][j], ringe[i + 1][j], ringe[i + 1][k]]
            netz.vieleck_nach(quad, sum(quad, Vector()) / 4 - m, st[stoff(i, k)])
    netz.vieleck_nach(ringe[0], -tang[0], st[stoff(0, 0)])
    netz.vieleck_nach(ringe[-1], tang[-1], st[stoff(len(stationen) - 2, 0)])


# Haltungen: Stationen (v nach hinten, w hoch, Halbbreite, Halbhöhe) von Brust bis Schwanzansatz, Hals und Kopf, Schnabelspitze (v, w)
_SITZEN = dict(
    rumpf=[(-0.115, 0.150, 0.040, 0.050), (-0.085, 0.155, 0.068, 0.078), (-0.030, 0.155, 0.088, 0.090), (0.030, 0.150, 0.080, 0.080),
           (0.085, 0.145, 0.052, 0.055), (0.120, 0.148, 0.026, 0.030)],
    hals=[(-0.080, 0.185, 0.050, 0.050), (-0.100, 0.220, 0.038, 0.040), (-0.114, 0.250, 0.033, 0.034), (-0.126, 0.270, 0.034, 0.036),
          (-0.146, 0.276, 0.028, 0.028), (-0.160, 0.272, 0.018, 0.018)],
    schnabel=((-0.158, 0.272), (-0.186, 0.264)), schwanz=(0.115, 0.148, -0.03))
_PICKEN = dict(
    rumpf=[(-0.105, 0.125, 0.040, 0.050), (-0.080, 0.140, 0.068, 0.078), (-0.030, 0.150, 0.088, 0.090), (0.030, 0.155, 0.080, 0.080),
           (0.085, 0.160, 0.052, 0.055), (0.120, 0.172, 0.026, 0.030)],
    hals=[(-0.080, 0.150, 0.050, 0.050), (-0.112, 0.130, 0.038, 0.040), (-0.146, 0.103, 0.033, 0.034), (-0.172, 0.078, 0.034, 0.036),
          (-0.192, 0.062, 0.028, 0.028), (-0.204, 0.052, 0.018, 0.018)],
    schnabel=((-0.203, 0.052), (-0.222, 0.026)), schwanz=(0.115, 0.172, 0.035))


def taube(netz, st, x, y, z, dreh=0.0, pose="sitzen", farbe="grau"):
    """Taube als Standbild (rund 0,34 m x 1,25): rundlicher Rumpf mit Brust, kleiner Kopf mit Schnabel, Hals mit grün-violettem Schimmer (Stoff Taube Hals),
    angelegte Flügel mit zwei dunklen Binden und dunkler Spitze, Schwanzfächer mit Endbinde, Füße. Blick nach -Y, gedreht um Z.
    pose: `sitzen` (Kopf hoch), `picken` (Kopf zum Boden), `fluegel` (sitzend, ein Flügel halb geöffnet).
    farbe: `grau` (blaugrau), `weiss` (weiße Taube), `scheckig` (blaugrau mit weißem Rücken und Brustfleck).
    Stoffe: Taube, Taube dunkel, Taube Hals, Taube weiss (die vier Stoffe der Taubenschlag-Datei außer Holz alt)."""
    d = math.radians(dreh)
    c, s = math.cos(d), math.sin(d)
    h = _PICKEN if pose == "picken" else _SITZEN
    lat = Vector((c, s, 0.0))

    def p(u, v, w):
        return Vector((x + (u * c - v * s) * G_TAUBE, y + (u * s + v * c) * G_TAUBE, z + w * G_TAUBE))
    weiss = farbe == "weiss"
    leib = "Taube weiss" if weiss else "Taube"
    binde = "Taube weiss" if weiss else "Taube dunkel"
    hals = "Taube weiss" if weiss else "Taube Hals"

    def rumpf_stoff(i, k):
        if farbe == "scheckig" and i in (1, 2) and k in (0, 1, 2, 3, 4):                     # weiße Brust und Rücken in der Mitte des Rumpfs
            return "Taube weiss"
        return leib
    _rohr(netz, st, [(p(0, v, w), ru * G_TAUBE, rv * G_TAUBE) for v, w, ru, rv in h["rumpf"]], 8, rumpf_stoff, lat)
    # Hals: erste Ringe schillernd, Kopf in der Körperfarbe, Schnabel dunkel
    _rohr(netz, st, [(p(0, v, w), ru * G_TAUBE, rv * G_TAUBE) for v, w, ru, rv in h["hals"]], 6, lambda i, k: hals if i in (1, 2) else leib, lat)
    (v0, w0), (v1, w1) = h["schnabel"]
    netz.stab(p(0, v0, w0), p(0, v1, w1), 0.009 * G_TAUBE, 0.003 * G_TAUBE, st["Taube dunkel"])
    netz.quader((0, 0, 0), (0.016 * G_TAUBE, 0.012 * G_TAUBE, 0.012 * G_TAUBE), st["Taube weiss"], Matrix.Translation(p(0, v0 + 0.004, w0 + 0.004)))      # Wachshaut
    # Schwanz: Fächer nach hinten, Endbinde dunkel
    sv, sw, sg = h["schwanz"]
    wurzel = [p(-0.026, sv - 0.01, sw), p(0.026, sv - 0.01, sw)]
    ende = [p(0.052, sv + 0.145, sw + sg), p(0.0, sv + 0.155, sw + sg + 0.002), p(-0.052, sv + 0.145, sw + sg)]
    mitte = [p(0.040, sv + 0.095, sw + sg * 0.6), p(-0.040, sv + 0.095, sw + sg * 0.6)]
    fl = [wurzel[1], *mitte[:1], ende[0], ende[1], ende[2], *mitte[1:], wurzel[0]]
    netz.vieleck_nach(fl, Vector((0, 0, 1)), st[binde if weiss else "Taube"])
    netz.vieleck_nach(list(reversed(fl)), Vector((0, 0, -1)), st[binde if weiss else "Taube"])
    band = [mitte[0], ende[0], ende[1], ende[2], mitte[1]]
    if not weiss:
        for o in (0.003, -0.012):
            netz.vieleck_nach([b + Vector((0, 0, o)) for b in (band if o > 0 else reversed(band))], Vector((0, 0, 1 if o > 0 else -1)), st["Taube dunkel"])
    # Flügel: je ein Lappen am Körper, zwei dunkle Binden und dunkle Schwungfederspitze
    for sx in (-1, 1):
        offen = pose == "fluegel" and sx == 1
        k0 = 0.0 if not offen else 0.04
        wing = [(-0.058, 0.184, 0.076, 0.014, 0.036), (-0.010, 0.195, 0.082 + k0, 0.022, 0.048), (0.050, 0.186, 0.076 + 2 * k0, 0.022, 0.044),
                (0.110, 0.166, 0.056 + 2.4 * k0, 0.016, 0.030), (0.170, 0.156, 0.036 + 2.4 * k0, 0.007, 0.011)]
        if h is _PICKEN:
            wing = [(v, w - 0.01 + 0.012 * i, u, a, b) for i, (v, w, u, a, b) in enumerate(wing)]
        if offen:                                                                             # halb geöffnet: Spitze hebt sich und schwenkt nach außen
            wing = [(v + 0.01 * i, w + 0.005 * i * i, u, a, b) for i, (v, w, u, a, b) in enumerate(wing)]
        wbinde = ("Taube weiss",) * 3 if weiss else ("Taube", "Taube dunkel", "Taube", "Taube dunkel")
        _rohr(netz, st, [(p(sx * u, v, w), a * G_TAUBE, b * G_TAUBE) for v, w, u, a, b in wing], 6, lambda i, k: wbinde[min(i, len(wbinde) - 1)], lat)
    # Füße
    for sx in (-1, 1):
        netz.stab(p(sx * 0.032, 0.0, 0.0), p(sx * 0.032, 0.0, 0.07), 0.009 * G_TAUBE, 0.009 * G_TAUBE, st["Taube dunkel"])
        netz.quader((0, 0, 0), (0.028 * G_TAUBE, 0.05 * G_TAUBE, 0.012 * G_TAUBE), st["Taube dunkel"], Matrix.Translation(p(sx * 0.032, -0.012, 0.006)))


def taubenschlag(netz, st, saat, winter=False):
    """Taubenschlag auf dem Pfahl: 3 m hoher Pfosten, Kasten mit Fluglöchern auf allen vier Seiten, Anflugbretter, steiles Schindeldach, Leiter; auf Brett und Dach sitzen Tauben."""
    z = rng(saat)
    H = 2.7
    netz.quader((0, 0, 0.04), (0.5, 0.5, 0.08), st["Holz alt"])                          # Fußbrett im Boden
    nt.ast(netz, st, (0, 0, 0.0), (0, 0, H), 0.1, 0.085, stoff="Holz alt", n=6)           # Pfahl
    for sx in (-1, 1):                                                                       # Streben gegen Kippen
        netz.stab((sx * 0.5, 0, 0.0), (sx * 0.06, 0, 0.85), 0.07, 0.06, st["Holz alt"], quer=(0, 1, 0))
    # Kasten 0,85 × 0,85 × 0,6
    K = 0.85
    zk = H + 0.3
    netz.quader((0, 0, zk), (K, K, 0.6), st["Holz alt"])
    netz.quader((0, 0, H - 0.01), (K + 0.12, K + 0.12, 0.05), st["Holz alt"])             # Bodenbrett, steht über
    for w in range(4):                                                                       # Fluglöcher und Anflugbretter auf allen Seiten
        a = math.radians(90 * w)
        ca, sa = round(math.cos(a)), round(math.sin(a))
        for d in (-0.2, 0.2):
            ox, oy = (-sa * d, ca * d)
            netz.quader((0, 0, 0), (0.15 if ca else 0.01, 0.01 if ca else 0.15, 0.17), st["Taube dunkel"], lage(ox + ca * (K / 2 + 0.003), oy + sa * (K / 2 + 0.003), zk + 0.08))
            netz.quader((0, 0, 0), (0.24 if sa else 0.12, 0.24 if ca else 0.12, 0.03), st["Holz alt"], lage(ox + ca * (K / 2 + 0.08), oy + sa * (K / 2 + 0.08), zk - 0.12))
    # Dach: vierseitig, steil, mit Überstand; dunkler Schiefer in „Taube dunkel“ (ein Stoff weniger)
    ecken = [Vector((sx * 0.66, sy * 0.66, zk + 0.32)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    spitze = Vector((0, 0, zk + 1.0))
    for i in range(4):
        pa, pb = ecken[i], ecken[(i + 1) % 4]
        netz.vieleck_nach([pa, pb, spitze], (pa + pb) / 2 - Vector((0, 0, zk + 0.32)), st["Taube dunkel"])
        netz.vieleck_nach([pa, spitze, pb], Vector((0, 0, zk + 0.32)) - (pa + pb) / 2, st["Taube dunkel"])    # Unterseite, damit man vom Kasten aus nicht hindurchsieht
    netz.stab((0, 0, zk + 0.95), (0, 0, zk + 1.22), 0.05, 0.05, st["Holz alt"])             # Firstspitze
    # Leiter an der Rückseite (+Y)
    for sx in (-0.15, 0.15):
        netz.stab((sx, 0.55, 0.0), (sx, 0.12, H - 0.02), 0.045, 0.04, st["Holz alt"], quer=(1, 0, 0))
    for k in range(1, 8):
        t = k / 8
        netz.stab((-0.15, 0.55 - 0.43 * t, (H - 0.02) * t), (0.15, 0.55 - 0.43 * t, (H - 0.02) * t), 0.03, 0.03, st["Holz alt"], quer=(1, 0, 0))
    # Tauben: zwei auf dem Anflugbrett vorn, eine auf dem Dach, zwei am Boden
    taube(netz, st, -0.2, -K / 2 - 0.08, zk - 0.105, 175)
    taube(netz, st, 0.2, -K / 2 - 0.08, zk - 0.105, 188, "fluegel")
    taube(netz, st, K / 2 + 0.08, 0.2, zk - 0.105, 262, "sitzen", "weiss")
    taube(netz, st, 0.55, -0.55, 0.0, 30, "picken")
    taube(netz, st, -0.5, -0.7, 0.0, 190, "picken", "scheckig")
    for k in range(10):                                                                      # Körner und Federn auf dem Boden
        w = z.uniform(0, 6.28)
        r = z.uniform(0.3, 0.9)
        netz.quader((0, 0, 0.004), (0.02, 0.012, 0.008), st["Taube dunkel"], lage(r * math.cos(w), r * math.sin(w), 0.0, z.uniform(0, 90)))
    if winter:
        p2.schneestreifen(netz, st, -0.5, 0.5, 0.0, zk + 0.67, 0.2, 0.05)
        netz.quader((0, 0, H + 0.016), (K + 0.1, K + 0.1, 0.03), st["Schnee"])
        for w in range(4):
            a = math.radians(90 * w)
            ca, sa = round(math.cos(a)), round(math.sin(a))
            netz.quader((0, 0, 0.012), (0.5 if sa else 0.14, 0.5 if ca else 0.14, 0.025), st["Schnee"], lage(ca * (K / 2 + 0.07), sa * (K / 2 + 0.07), zk - 0.075))


def taubenhaus(netz, st, saat, winter=False):
    """Kleines Taubenhaus auf vier Beinen für größere Flächen: Holzhäuschen mit Satteldach, zwei Fluglöchern, Anflugbrett und Kornschüttung, Tauben davor."""
    z = rng(saat)
    L, T, zb = 1.3, 0.9, 1.1
    for sx in (-1, 1):
        for sy in (-1, 1):
            nt.ast(netz, st, (sx * (L / 2 - 0.07), sy * (T / 2 - 0.07), 0.0), (sx * (L / 2 - 0.07), sy * (T / 2 - 0.07), zb), 0.07, 0.06, stoff="Holz alt", n=5)
    for sy in (-1, 1):                                                                       # Zargen
        netz.stab((-L / 2 + 0.07, sy * (T / 2 - 0.07), 0.45), (L / 2 - 0.07, sy * (T / 2 - 0.07), 0.45), 0.04, 0.04, st["Holz alt"])
    for sx in (-1, 1):
        netz.stab((sx * (L / 2 - 0.07), -T / 2 + 0.07, 0.45), (sx * (L / 2 - 0.07), T / 2 - 0.07, 0.45), 0.04, 0.04, st["Holz alt"])
    netz.quader((0, 0, zb + 0.03), (L + 0.1, T + 0.1, 0.06), st["Holz alt"])             # Boden
    H = 0.8
    netz.quader((0, 0, zb + 0.06 + H / 2), (L, T, H), st["Holz alt"])
    for d in (-0.3, 0.3):
        netz.quader((d, -T / 2 - 0.003, zb + 0.5), (0.16, 0.01, 0.18), st["Taube dunkel"])
    netz.quader((0, -T / 2 - 0.1, zb + 0.14), (0.9, 0.2, 0.03), st["Holz alt"])           # Anflugbrett
    xa, xb, ye, zt, zf = -L / 2 - 0.1, L / 2 + 0.1, T / 2 + 0.12, zb + H + 0.07, zb + H + 0.75        # Satteldach: First entlang X, Traufen bei y = ±ye
    for sy in (-1, 1):
        flaeche = [Vector((xa, sy * ye, zt)), Vector((xb, sy * ye, zt)), Vector((xb, 0, zf)), Vector((xa, 0, zf))]
        netz.vieleck_nach(flaeche, (0, sy, 1), st["Taube dunkel"])
        netz.vieleck_nach(list(reversed(flaeche)), (0, -sy, -1), st["Taube dunkel"])
    for sx, x in ((-1, xa), (1, xb)):
        giebel = [Vector((x, -ye, zt)), Vector((x, ye, zt)), Vector((x, 0, zf))]
        netz.vieleck_nach(giebel, (sx, 0, 0), st["Holz alt"])
    # Leiter vorn rechts
    for sx in (0.5, 0.74):
        netz.stab((sx, -0.8, 0.0), (sx, -T / 2 - 0.08, zb + 0.1), 0.045, 0.04, st["Holz alt"], quer=(1, 0, 0))
    for k in range(1, 6):
        t = k / 6
        netz.stab((0.5, -0.8 + (0.8 - T / 2 - 0.08) * t, (zb + 0.1) * t), (0.74, -0.8 + (0.8 - T / 2 - 0.08) * t, (zb + 0.1) * t), 0.03, 0.03, st["Holz alt"], quer=(1, 0, 0))
    taube(netz, st, -0.25, -T / 2 - 0.1, zb + 0.155, 182)
    taube(netz, st, 0.3, -T / 2 - 0.1, zb + 0.155, 168, "fluegel")
    taube(netz, st, -0.7, -0.9, 0.0, 40, "picken")
    taube(netz, st, 0.0, -1.05, 0.0, 120, "picken", "scheckig")
    taube(netz, st, 0.65, -0.95, 0.0, 215, "sitzen", "weiss")
    for k in range(14):                                                                      # Körner
        netz.quader((0, 0, 0.004), (0.02, 0.012, 0.008), st["Taube dunkel"], lage(z.uniform(-0.9, 0.9), z.uniform(-1.2, -0.7), 0.0, z.uniform(0, 90)))
    if winter:
        netz.quader((0, 0, 0.012), (L + 0.08, T + 0.08, 0.03), st["Schnee"], lage(0, 0, zb + H + 0.06))
        netz.quader((0, 0, 0.012), (0.88, 0.2, 0.03), st["Schnee"], lage(0, -T / 2 - 0.1, zb + 0.155))


def taubenfutter(netz, st, saat, winter=False):
    """Futterplatz: flache Holzschale mit Körnern, ein offener Kornsack aus Holz-Rinde-Kiste, vier Tauben picken ringsum."""
    z = rng(saat)
    netz.prisma([(-0.35, 0.0), (0.35, 0.0), (0.33, 0.1), (-0.33, 0.1)], -0.35, 0.35, st["Holz alt"], matrix=Matrix.Translation((0, 0, 0)))
    netz.quader((0, 0, 0.1), (0.62, 0.52, 0.02), st["Holz alt"])
    for k in range(3):
        for j in range(3):
            netz.quader((0, 0, 0.012), (0.03, 0.02, 0.025), st["Taube dunkel"], lage(-0.2 + 0.2 * k, -0.15 + 0.15 * j, 0.1))
    taube(netz, st, -0.5, -0.1, 0.0, 70, "picken")
    taube(netz, st, 0.5, 0.2, 0.0, 250, "picken", "scheckig")
    taube(netz, st, 0.1, 0.55, 0.0, 190, "picken", "weiss")
    taube(netz, st, -0.05, -0.55, 0.0, 10, "fluegel")
    for k in range(10):
        netz.quader((0, 0, 0.004), (0.02, 0.012, 0.008), st["Taube dunkel"], lage(z.uniform(-0.9, 0.9), z.uniform(-0.8, 0.8), 0.0, z.uniform(0, 90)))
    if winter:
        netz.quader((0, 0, 0.012), (0.7, 0.4, 0.03), st["Schnee"], lage(0, 0, 0.1) @ Matrix.Translation((0, 0.0, 0.0)))


# ─────────────────────────────────────────────────────────────────────────────
#  Zusammenstellung
# ─────────────────────────────────────────────────────────────────────────────

GRUPPEN = {
    "steinmetz": [
        ("Werkbank", werkbank, (201,), {}, True),
        ("BloeckeBehauen", bloecke_behauen, (202,), {}, True),
        ("Rohsteine", rohsteine, (203,), {}, True),
        ("Splitterhaufen", splitterhaufen, (204,), {}, True),
        ("Dreibock", dreibock, (205,), {}, True),
        ("Werkzeugkiste", werkzeugkiste, (206,), {}, True),
        ("Saeulenrohling", saeulenrohling, (207,), {}, True),
    ],
    "taube": [
        ("Taubenschlag", taubenschlag, (211,), {}, True),
        ("Taubenhaus", taubenhaus, (212,), {}, True),
        ("Taubenfutter", taubenfutter, (213,), {}, True),
    ],
}


def main():
    nur = [a.split("=", 1)[1] for a in sys.argv if a.startswith("--nur=")]
    nur = [g for s in nur for g in s.split(",")]
    for gruppe, teile in GRUPPEN.items():
        if nur and gruppe not in nur:
            continue
        name = "fuellung_" + gruppe
        sommer, winter = p2.knoten(teile)
        alle = sommer + winter
        bs.bericht(name, alle, einzeln=True)
        for o in alle:
            funde = bs.deckungsgleich([o])
            if funde:
                print(f"  DECKUNG {o.name}: {len(funde)}", [(tuple(round(c, 2) for c in f[0]), round(f[3], 3)) for f in funde[:3]])
        print("geschrieben:", bs.exportieren(name, alle))
        if "--ohne-bilder" in sys.argv:
            continue
        import fuellung_vorschau as fv
        fv.vorschau(name, sommer, winter)


if __name__ == "__main__":
    main()
