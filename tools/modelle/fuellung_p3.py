"""Füllung Paket 3 – Kleinmodelle für Restflächen (Tiere, Hof, Pferch, Wasserstelle), Teil 1: Modelle.

    blender -b --factory-startup --python tools/modelle/fuellung_p3.py -- [--schnell] [--ohne-bilder] [--nur=tiere,pferch]

Schreibt vier Sammeldateien nach game/assets/modelle/props/ (fuellung_tiere.glb, fuellung_hof2.glb, fuellung_pferch.glb,
fuellung_wasserstelle.glb) und je ein Vorschaubild docs/modelle/fuellung_<name>.png (alle Teile nebeneinander, oben die
Sommerfassung, unten die Winterfassung). Aufbau, Maßstab und Material-Weg wie fuellung_p2.py: Jedes Teil ist ein Knoten mit
Ursprung in der Mitte am Boden, Maße in Metern, Z oben beim Bauen, Blick der Tiere nach -Y. Wo eine Schneehaube oder eine
Eisfassung Sinn hat, gibt es einen zweiten Knoten `<Name>_Winter` am selben Ort.

Wiederverwendet statt neu gebaut (Paket 2, fuellung_hof.glb und fuellung_holzplatz.glb): Trog, Futtertrog, PferchHuerde, Hackklotz.
Hühner und Hähne gibt es als Tiere der Stadt (game/assets/modelle/tiere/tier_huhn_*.glb, tier_hahn.glb) – sie kommen hier nicht noch
einmal vor. Alles deterministisch (Zufall nur aus festen Saaten). Fremdmodelle nur bei den Tieren: Schwein und Ziege kommen aus freien
CC0-Modellen (tiere_cc0.py, Quellen dort); alles andere ist selbst gebaut.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import fuellung_p2 as p2  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
import tiere_cc0 as tc  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Fuellung Schlamm": (0.19, 0.15, 0.105),
    "Pfuetze": (0.14, 0.2, 0.22),             # Wasser der Wasserstelle: wie „Wasser“, aber eigener Name; das Spiel gibt ihm pfuetze.gdshader (Himmelsspiegelung, Regenringe)
})
bs.RAUHEIT["Pfuetze"] = 0.03
rng = p2.rng


# ─────────────────────────────────────────────────────────────────────────────
#  Tiere (Standbilder aus CC0-Fremdmodellen, siehe tiere_cc0.py)
# ─────────────────────────────────────────────────────────────────────────────

TIERE = [
    ("SchweinStehend", tc.schwein, {}),
    ("SchweinWuehlend", tc.schwein, {"wuehlen": True}),
    ("ZiegeStehend", tc.ziege, {}),
    ("ZiegeFressend", tc.ziege, {"fressen": True}),
    ("Huhn", tc.huhn, {}),
]


def tiere_bauen():
    """Die Tiere als Objekte (Sommer; Tiere haben keine Winterfassung). Aufruf nach bs.neue_szene()."""
    bs.neue_szene()
    p2.STOFFE = bs.Stoffe()
    return [bauer(name, **kw) for name, bauer, kw in TIERE], []


# ─────────────────────────────────────────────────────────────────────────────
#  Hof: Hühnerstall, Schuppen
# ─────────────────────────────────────────────────────────────────────────────

def latten(netz, st, x0, x1, y, z0, z1, abstand=0.2, stoff="Holz alt"):
    """Senkrechte Deckleisten über den Fugen einer Bretterwand in der Ebene y von x0 bis x1."""
    n = max(2, int((x1 - x0) / abstand))
    for i in range(n + 1):
        x = x0 + (x1 - x0) * i / n
        netz.quader((x, y, (z0 + z1) / 2), (0.035, 0.018, z1 - z0), st[stoff])


def huehnerstall(netz, st, saat, winter=False):
    """Hühnerstall: Bretterverschlag auf vier Pfosten, Pultdach aus Schindeln, Klappe mit Hühnerleiter, Sitzstange vor der Wand."""
    z = rng(saat)
    B, T, z0, H = 1.5, 1.0, 0.42, 1.0
    for sx in (-1, 1):
        for sy in (-1, 1):
            nt.ast(netz, st, (sx * (B / 2 - 0.08), sy * (T / 2 - 0.08), 0), (sx * (B / 2 - 0.08), sy * (T / 2 - 0.08), z0 + 0.06), 0.06, 0.05, stoff="Holz alt", n=5)
    netz.quader((0, 0, z0 + 0.02), (B, T, 0.06), st["Holz alt"])                                    # Boden
    # Wände aus senkrechten Brettern
    netz.quader((0, T / 2 - 0.02, z0 + 0.05 + H / 2), (B - 0.04, 0.04, H), st["Bretter"], ohne=("-z",))
    netz.quader((-B / 2 + 0.02, 0, z0 + 0.05 + H / 2), (0.04, T - 0.04, H), st["Bretter"], ohne=("-z",))
    netz.quader((B / 2 - 0.02, 0, z0 + 0.05 + H / 2), (0.04, T - 0.04, H), st["Bretter"], ohne=("-z",))
    netz.quader((0, -T / 2 + 0.02, z0 + 0.05 + H / 2), (B - 0.04, 0.04, H), st["Bretter"], ohne=("-z",))
    latten(netz, st, -B / 2 + 0.05, B / 2 - 0.05, -T / 2 - 0.005, z0 + 0.06, z0 + 0.05 + H, 0.17)
    latten(netz, st, -B / 2 + 0.05, B / 2 - 0.05, T / 2 + 0.005, z0 + 0.06, z0 + 0.05 + H, 0.2)
    for sx in (-1, 1):
        for q in (-1, 0, 1):
            netz.quader((sx * (B / 2 + 0.005), q * 0.3, z0 + 0.05 + H / 2), (0.018, 0.035, H), st["Holz alt"])
    for sx in (-1, 1):                                                                                # Eckpfosten
        for sy in (-1, 1):
            netz.quader((sx * (B / 2 - 0.02), sy * (T / 2 - 0.02), z0 + 0.05 + H / 2 + 0.05), (0.07, 0.07, H + 0.1), st["Holz alt"])
    # Dach: fällt nach hinten (+x tief ist rechts), Schindeln
    bs.pultdach(netz, st, -B / 2 - 0.05, B / 2 + 0.05, -T / 2, T / 2, z0 + H + 0.3, z0 + H + 0.12, stoff="Holz alt")
    # Einschlupfklappe vorn rechts und Hühnerleiter
    klappe_x = 0.38
    netz.quader((klappe_x, -T / 2 - 0.012, z0 + 0.3), (0.3, 0.02, 0.34), st["Rinde"])
    netz.quader((klappe_x, -T / 2 - 0.03, z0 + 0.3), (0.38, 0.02, 0.04), st["Holz alt"])
    netz.quader((klappe_x, -T / 2 - 0.03, z0 + 0.47), (0.38, 0.02, 0.04), st["Holz alt"])
    for sx in (-1, 1):
        netz.quader((klappe_x + sx * 0.17, -T / 2 - 0.03, z0 + 0.3), (0.04, 0.02, 0.38), st["Holz alt"])
    an, aus = Vector((klappe_x, -T / 2 - 0.05, z0 + 0.08)), Vector((klappe_x, -T / 2 - 0.62, 0.02))
    for sx in (-0.11, 0.11):
        netz.stab(an + Vector((sx, 0, 0)), aus + Vector((sx, 0, 0)), 0.05, 0.03, st["Holz alt"], quer=(1, 0, 0))
    for k in range(1, 6):
        p = an + (aus - an) * (k / 6)
        netz.quader((p.x, p.y, p.z + 0.03), (0.26, 0.025, 0.025), st["Holz alt"])
    # Sitzstange an der linken Seitenwand, auf zwei Stützen
    s0 = Vector((-B / 2 - 0.58, 0.05, z0 + 0.34))
    nt.ast(netz, st, s0, s0 + Vector((0.56, 0, 0)), 0.03, 0.03, stoff="Holz alt", n=5)
    for y in (-0.15, 0.25):
        nt.ast(netz, st, (-B / 2 - 0.54, y, 0.0), (-B / 2 - 0.54, y, z0 + 0.34), 0.03, 0.025, stoff="Holz alt", n=4)
    nt.ast(netz, st, (-B / 2 - 0.54, -0.15, z0 + 0.3), (-B / 2 - 0.54, 0.25, z0 + 0.3), 0.025, 0.025, stoff="Holz alt", n=4)
    for k in range(9):                                                                                # Stroh und Federn unter dem Stall
        x, y = z.uniform(-0.6, 0.6), z.uniform(-0.4, 0.4)
        w = z.uniform(0, 6.28)
        p2.halm(netz, st, (x, y, 0.01), (x + 0.2 * math.cos(w), y + 0.2 * math.sin(w), 0.02), "Stroh")
    if winter:
        bs.pultdach(netz, st, -B / 2 + 0.02, B / 2 + 0.03, -T / 2 + 0.06, T / 2 - 0.06, z0 + H + 0.37, z0 + H + 0.2, stoff="Schnee")
        for sx in (-1, 1):
            for sy in (-1, 1):
                p2.schnee(netz, st, (sx * (B / 2 - 0.08), sy * (T / 2 - 0.08), z0 + 0.06), 0.05, 0.05, 0.03, saat=int(sx + sy + 7), n=5)
        p2.schneestreifen(netz, st, -B / 2 - 0.58, -B / 2 - 0.02, 0.05, z0 + 0.37, 0.07, 0.04)


def schuppen(netz, st, saat, winter=False):
    """Schuppen: Pfostenbau mit Bretterwänden, Pultdach, Brettertür mit Z-Verstrebung; links offen mit Werkzeug und Reisig."""
    z = rng(saat)
    L, T = 2.5, 1.8
    h_hoch, h_tief = 2.05, 1.72
    ph = lambda x: h_hoch + (h_tief - h_hoch) * (x + L / 2) / L                                     # Wandhöhe an der Stelle x (Dach fällt nach +x)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x = sx * (L / 2 - 0.05)
            nt.ast(netz, st, (x, sy * (T / 2 - 0.05), -0.05), (x, sy * (T / 2 - 0.05), ph(x) - 0.03), 0.07, 0.06, stoff="Holz alt", n=5)
    # Rückwand (Brettverschlag) mit Latten, trapezförmig
    profil_h = [(L / 2 - 0.03, 0), (-L / 2 + 0.03, 0), (-L / 2 + 0.03, h_hoch - 0.08), (L / 2 - 0.03, h_tief - 0.08)]
    netz.prisma([(-x, zz) for x, zz in profil_h], T / 2 - 0.07, T / 2 - 0.03, st["Bretter"], matrix=Matrix.Rotation(math.radians(90), 4, "Z"))
    latten(netz, st, -L / 2 + 0.1, L / 2 - 0.1, T / 2 - 0.01, 0.04, 1.65, 0.2)
    # rechte Seitenwand (x = +L/2) und linke offen mit halber Wand
    netz.quader((L / 2 - 0.02, 0, (h_tief - 0.1) / 2), (0.04, T - 0.12, h_tief - 0.1), st["Bretter"], ohne=("-z",))
    for q in range(-4, 5):
        netz.quader((L / 2 + 0.005, q * 0.19, (h_tief - 0.1) / 2), (0.018, 0.035, h_tief - 0.12), st["Holz alt"])
    netz.quader((-L / 2 + 0.02, 0.47, 0.45), (0.04, 0.74, 0.9), st["Bretter"], ohne=("-z",))      # niedrige Brettwand links hinten
    # Dachbalken und Dach
    for y in (-T / 2 + 0.05, 0, T / 2 - 0.05):
        netz.stab((-L / 2 - 0.1, y, h_hoch - 0.12), (L / 2 + 0.1, y, h_tief - 0.12), 0.1, 0.08, st["Holz alt"])
    bs.pultdach(netz, st, -L / 2 - 0.1, L / 2 + 0.1, -T / 2 - 0.05, T / 2 + 0.05, h_hoch + 0.03, h_tief - 0.12, stoff="Holz alt")
    # Tür vorn rechts
    tb, th = 1.0, 1.58
    tx = L / 2 - 0.62
    netz.quader((tx, -T / 2 - 0.01, th / 2 + 0.05), (tb, 0.04, th), st["Bretter"], ohne=("-z",))
    latten(netz, st, tx - tb / 2 + 0.06, tx + tb / 2 - 0.06, -T / 2 - 0.035, 0.1, th, 0.16)
    for zz in (0.3, th - 0.25):
        netz.quader((tx, -T / 2 - 0.045, zz), (tb - 0.04, 0.025, 0.12), st["Holz alt"])
    netz.stab((tx - tb / 2 + 0.1, -T / 2 - 0.054, 0.3), (tx + tb / 2 - 0.1, -T / 2 - 0.054, th - 0.25), 0.11, 0.02, st["Holz alt"])
    for zz in (0.3, th - 0.25):
        netz.quader((tx + tb / 2 - 0.12, -T / 2 - 0.06, zz), (0.24, 0.015, 0.05), st["Eisen"])         # Bänder
    netz.quader((tx - tb / 2 + 0.12, -T / 2 - 0.06, th / 2), (0.05, 0.03, 0.18), st["Eisen"])            # Griff
    # Gerümpel links: Heugabel, Reisigbündel, ein Eimerreifen
    p2.gabel(netz, st, (-L / 2 + 0.3, -T / 2 + 0.3, 0.05), (0.15, 0.2, 1.0), 1.7)
    for k in range(7):
        w = z.uniform(-0.1, 0.1)
        nt.ast(netz, st, (-L / 2 + 0.55 + z.uniform(-0.08, 0.08), 0.1 + z.uniform(-0.05, 0.05), 0.0),
               (-L / 2 + 0.55 + w * 2, 0.2 + w, 0.8 + z.uniform(0, 0.15)), 0.014, 0.01, stoff="Rinde", n=3)
    bs.fass(netz, st, Vector((-L / 2 + 0.95, 0.5, 0.0)), r=0.28, h=0.6, reifen=(0.12, 0.45))
    if winter:
        bs.pultdach(netz, st, -L / 2 - 0.05, L / 2 + 0.05, -T / 2 + 0.02, T / 2 - 0.02, h_hoch + 0.1, h_tief - 0.05, stoff="Schnee")
        p2.schnee(netz, st, (-L / 2 + 0.95, 0.5, 0.6), 0.28, 0.28, 0.07, saat=saat, n=7)
        for sx in (-1, 1):
            for sy in (-1, 1):
                x = sx * (L / 2 - 0.05)
                p2.schnee(netz, st, (x, sy * (T / 2 - 0.05), ph(x) - 0.03), 0.06, 0.06, 0.03, saat=int(sx * 3 + sy), n=5)


# ─────────────────────────────────────────────────────────────────────────────
#  Pferch: Gatter, Futterraufe, Strohschüttung
# ─────────────────────────────────────────────────────────────────────────────

def gatter(netz, st, saat, winter=False):
    """Tor eines Pferchs: zwei Pfosten, ein Flügel aus drei Riegeln mit Strebe, Bänder am Angelpfosten, Holzriegel mit Seilschlaufe."""
    z = rng(saat)
    X, H = 0.98, 1.3
    for sx in (-1, 1):
        nt.ast(netz, st, (sx * X, 0, -0.05), (sx * X, 0, H), 0.07, 0.06, stoff="Holz alt", n=6)
        lb.kegel(netz, (sx * X, 0), 0.062, H - 0.02, H + 0.09, st["Holz alt"], n=6)
    riegel = (0.22, 0.62, 1.0)
    for zz in riegel:
        netz.stab((-X + 0.07, 0.0, zz), (X - 0.07, 0.0, zz), 0.09, 0.05, st["Holz alt"])
    for sx in (-1, 1):
        netz.stab((sx * (X - 0.14), 0.0, 0.14), (sx * (X - 0.14), 0.0, 1.08), 0.08, 0.05, st["Holz alt"], quer=(1, 0, 0))
    netz.stab((-X + 0.16, -0.035, 0.28), (X - 0.2, -0.035, 0.96), 0.09, 0.03, st["Holz alt"])            # Strebe von unten (Angel) nach oben (Schloss)
    for zz in (0.3, 0.9):                                                                              # Eisenbänder am Angelpfosten
        netz.quader((-X + 0.28, -0.042, zz), (0.5, 0.012, 0.055), st["Eisen"])
        netz.quader((-X, -0.062, zz), (0.1, 0.03, 0.08), st["Eisen"])
    netz.quader((X - 0.17, -0.055, 0.62), (0.2, 0.03, 0.05), st["Holz alt"])                          # Riegel
    netz.quader((X - 0.02, -0.055, 0.62), (0.045, 0.05, 0.1), st["Holz alt"])
    for k in range(5):                                                                                 # Seilschlaufe
        a = 2 * math.pi * k / 5
        netz.stab((X - 0.3 + 0.045 * math.cos(a), -0.08, 0.52 + 0.045 * math.sin(a)), (X - 0.3 + 0.045 * math.cos(a + 1.25), -0.08, 0.52 + 0.045 * math.sin(a + 1.25)),
                  0.014, 0.014, st["Holz alt"])
    for sx in (-1, 1):                                                                                 # Erdanhäufung am Pfosten, Trampelstelle
        p2.kuppel(netz, st, (sx * X, -0.1, 0.0), 0.2, 0.17, 0.05, ("Fuellung Erde", "Fuellung Erde"), n=14, saat=saat + sx, jitter=0.15)
    if winter:
        for zz in riegel:
            p2.schneestreifen(netz, st, -X + 0.08, X - 0.08, 0.0, zz + 0.045, 0.09, 0.035)
        for sx in (-1, 1):
            p2.schnee(netz, st, (sx * X, 0, H + 0.08), 0.07, 0.07, 0.05, saat=sx + 9, n=6)


def futterraufe(netz, st, saat, winter=False):
    """Heuraufe: V-förmiges Gestell aus Latten auf vier Beinen, unten ein Trog; das Heu ist vom Tier zerzupft."""
    z = rng(saat)
    L = 1.7
    fuss = [(sx * 0.75, sy * 0.34) for sx in (-1, 1) for sy in (-1, 1)]
    for fx, fy in fuss:
        nt.ast(netz, st, (fx, fy, 0), (fx * 1.02, fy * 0.82, 0.62), 0.045, 0.04, stoff="Holz alt", n=5)
    for sy in (-1, 1):                                                                                  # Längsholme unten
        netz.stab((-0.78, sy * 0.3, 0.46), (0.78, sy * 0.3, 0.46), 0.08, 0.055, st["Holz alt"])
    # Trog (offene Kiste)
    netz.quader((0, 0, 0.5), (L - 0.1, 0.5, 0.04), st["Holz alt"])
    for sy in (-1, 1):
        netz.quader((0, sy * 0.25, 0.58), (L - 0.1, 0.04, 0.16), st["Holz alt"])
    for sx in (-1, 1):
        netz.quader((sx * (L / 2 - 0.06), 0, 0.58), (0.04, 0.46, 0.16), st["Holz alt"])
    # V-Lattung: Gerüst schräg nach außen
    for sy in (-1, 1):
        for k in range(9):
            x = -0.74 + 0.185 * k
            netz.stab((x, sy * 0.24, 0.62), (x, sy * 0.4, 1.15), 0.035, 0.03, st["Holz alt"], quer=(1, 0, 0))
        netz.stab((-0.78, sy * 0.405, 1.14), (0.78, sy * 0.405, 1.14), 0.05, 0.04, st["Holz alt"])
        netz.stab((-0.78, sy * 0.3, 0.84), (0.78, sy * 0.3, 0.84), 0.04, 0.035, st["Holz alt"])
    for sx in (-1, 1):                                                                                  # Stirnbretter
        netz.stab((sx * 0.8, -0.24, 0.62), (sx * 0.8, -0.4, 1.15), 0.05, 0.04, st["Holz alt"], quer=(1, 0, 0))
        netz.stab((sx * 0.8, 0.24, 0.62), (sx * 0.8, 0.4, 1.15), 0.05, 0.04, st["Holz alt"], quer=(1, 0, 0))
    p2.kuppel(netz, st, (0, 0, 0.55), 0.72, 0.3, 0.5, ("Heu", "Stroh", "Heu", "Heu"), n=14, saat=saat, jitter=0.12,
              profil=((1.0, 0.0), (0.95, 0.4), (0.72, 0.8), (0.3, 1.0)))
    for k in range(16):                                                                                 # Halme, die durch die Latten hängen
        x = z.uniform(-0.65, 0.65)
        sy = z.choice((-1, 1))
        p2.halm(netz, st, (x, sy * 0.3, z.uniform(0.7, 1.0)), (x + z.uniform(-0.05, 0.05), sy * 0.46, z.uniform(0.5, 0.8)), "Heu")
    for k in range(10):
        p2.halm(netz, st, (z.uniform(-0.9, 0.9), z.uniform(-0.7, 0.7), z.uniform(0.01, 0.025)), (z.uniform(-0.9, 0.9), z.uniform(-0.7, 0.7), z.uniform(0.01, 0.025)), "Heu")
    if winter:
        for sy in (-1, 1):
            p2.schneestreifen(netz, st, -0.78, 0.78, sy * 0.405, 1.165, 0.1, 0.045)
        p2.schnee(netz, st, (0, 0, 0.95), 0.62, 0.22, 0.12, saat=saat, n=12)


def strohschuettung(netz, st, saat, winter=False):
    """Strohschütte am Boden: flache, zertretene Lage mit Hügeln, Halmen und einem dunkleren, feuchten Rand."""
    z = rng(saat)
    p2.kuppel(netz, st, (0, 0, 0.0), 1.05, 0.8, 0.17, ("Stroh", "Heu", "Stroh", "Stroh", "Stroh"), n=20, saat=saat, jitter=0.18,
              profil=((1.0, 0.0), (0.96, 0.35), (0.75, 0.75), (0.35, 1.0)))
    p2.kuppel(netz, st, (0.35, 0.2, 0.0), 0.5, 0.4, 0.27, ("Stroh", "Stroh", "Stroh"), n=16, saat=saat + 1, jitter=0.2)
    p2.kuppel(netz, st, (-0.5, -0.25, 0.0), 0.4, 0.35, 0.2, ("Heu", "Stroh", "Stroh"), n=16, saat=saat + 2, jitter=0.2)
    for k in range(34):
        r = z.uniform(0.2, 1.0)
        w = z.uniform(0, 6.28)
        x, y = 1.0 * r * math.cos(w), 0.78 * r * math.sin(w)
        zz = 0.12 * (1 - r * r) + 0.02
        d = z.uniform(0, 6.28)
        p2.halm(netz, st, (x, y, zz), (x + 0.26 * math.cos(d), y + 0.26 * math.sin(d), zz + z.uniform(0.0, 0.1)), "Stroh" if k % 3 else "Stroh")
    for k in range(12):                                                                                 # verstreute Halme außerhalb
        w = z.uniform(0, 6.28)
        x, y = 1.2 * math.cos(w), 0.95 * math.sin(w)
        d = z.uniform(0, 6.28)
        p2.halm(netz, st, (x, y, 0.01), (x + 0.3 * math.cos(d), y + 0.3 * math.sin(d), 0.02), "Stroh")
    if winter:                                                                                          # Schnee liegt in Mulden und auf den Hügeln, nicht als Decke
        prof = ((1.0, 0.0), (0.93, 0.5), (0.62, 0.86), (0.26, 1.0))
        for k, (x, y, rx, ry, h) in enumerate(((-0.45, 0.1, 0.5, 0.36, 0.07), (0.3, -0.25, 0.42, 0.3, 0.06), (0.5, 0.3, 0.38, 0.28, 0.12))):
            p2.kuppel(netz, st, (x, y, 0.1 if k == 2 else 0.04), rx, ry, h, ("Schnee", "Schnee", "Schnee schatten"), n=10, saat=saat + 5 + k, jitter=0.2, profil=prof)


# ─────────────────────────────────────────────────────────────────────────────
#  Wasserstelle: Ziehbrunnen, Steintrog, Pfützen
# ─────────────────────────────────────────────────────────────────────────────

def ziehbrunnen(netz, st, saat, winter=False):
    """Ziehbrunnen: Kranz aus zwei Lagen behauener Steine, Holzgestell mit Haspel (Seil, Kurbel) und Eimer."""
    z = rng(saat)
    n = 11
    R_in, R_aus = 0.54, 0.8
    hoehe = 0.4
    for lage in range(2):
        for k in range(n):
            w = 2 * math.pi * (k + 0.5 * lage) / n + z.uniform(-0.03, 0.03)
            tang = 2 * (R_aus + 0.02) * math.tan(math.pi / n) + 0.02
            stoff = "Sandstein alt" if (k + lage) % 4 else "Sandstein alt"
            tiefe = R_aus - R_in + z.uniform(-0.03, 0.03)
            h = hoehe + z.uniform(-0.015, 0.015)
            mitte = Vector(((R_aus + R_in) / 2 + (0.01 if lage else -0.01), 0, lage * hoehe + h / 2))
            netz.quader(mitte, (tiefe, tang, h), st[stoff], Matrix.Rotation(w, 4, "Z"))
    top = 2 * hoehe
    # dunkle Wasserfläche in der Tiefe
    tiefe = [(R_in * 0.97 * math.cos(2 * math.pi * k / 14), R_in * 0.97 * math.sin(2 * math.pi * k / 14), 0.12) for k in range(14)]
    netz.vieleck_nach(tiefe, (0, 0, 1), st["Eis"] if winter else st["Pfuetze"])
    # Gestell: zwei Pfosten mit Gabel, Haspel, Dachbalken, Streben
    for sx in (-1, 1):
        nt.ast(netz, st, (sx * 0.98, 0, -0.05), (sx * 0.98, 0, 2.15), 0.085, 0.07, stoff="Holz alt", n=6)
        for sy in (-1, 1):
            netz.stab((sx * 0.98, sy * 0.05, 0.9), (sx * 0.66, sy * 0.05, 1.8), 0.07, 0.05, st["Holz alt"], quer=(0, 1, 0))
    netz.stab((-1.1, 0, 2.12), (1.1, 0, 2.12), 0.12, 0.1, st["Holz alt"])
    nt.ast(netz, st, (-1.05, 0, 1.78), (1.2, 0, 1.78), 0.075, 0.075, stoff="Holz alt", n=8)              # Walze der Haspel
    netz.quader((1.2, 0.08, 1.78), (0.05, 0.05, 0.3), st["Holz alt"])                                      # Kurbelarm
    netz.quader((1.2, 0.2, 1.64), (0.05, 0.24, 0.05), st["Holz alt"])                                      # Handgriff
    nt.ast(netz, st, (0, 0, 1.78), (0.0, 0.0, 0.92), 0.012, 0.012, stoff="Holz alt", n=4)                # Seil zum Eimer
    for k in range(4):                                                                                 # Seilwindungen auf der Walze
        netz.prisma([(0.082 * math.cos(2 * math.pi * j / 8), 0.082 * math.sin(2 * math.pi * j / 8)) for j in range(8)], -0.18 + 0.07 * k, -0.18 + 0.07 * k + 0.04,
                    st["Holz alt"], matrix=Matrix.Translation((0, 0, 1.78)) @ Matrix.Rotation(math.radians(0), 4, "Y"))
    bs.fass(netz, st, Vector((0, 0, 0.7)), r=0.15, h=0.26, reifen=(0.06, 0.18))                      # Eimer hängt im Rohr
    netz.stab((-0.13, 0, 1.0), (0.13, 0, 1.0), 0.02, 0.02, st["Eisen"])                              # Bügel
    if winter:
        for k in range(n):
            w = 2 * math.pi * (k + 0.5) / n
            a, b = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
            ring = [(R_in * 0.98 * math.cos(a), R_in * 0.98 * math.sin(a), top + 0.03), (R_in * 0.98 * math.cos(b), R_in * 0.98 * math.sin(b), top + 0.03),
                    (R_aus * math.cos(b), R_aus * math.sin(b), top + 0.015), (R_aus * math.cos(a), R_aus * math.sin(a), top + 0.015)]
            netz.vieleck_nach(ring, (0, 0, 1), st["Schnee" if k % 5 else "Schnee schatten"])
        p2.schneestreifen(netz, st, -1.1, 1.1, 0, 2.18, 0.12, 0.06)
        p2.schnee(netz, st, (0, 0, 0.99), 0.14, 0.14, 0.03, saat=saat, n=6)


def wassertrog_stein(netz, st, saat, winter=False):
    """Steintrog: aus einem Block gehauen, an den Enden verstärkt, leicht bemoost, mit hölzernem Brunnenrohr."""
    z = rng(saat)
    L, B, H = 1.9, 0.75, 0.6
    aussen = [(-B / 2, 0), (B / 2, 0), (B / 2 - 0.01, H * 0.6), (B / 2, H), (B / 2 - 0.1, H), (B / 2 - 0.115, 0.17), (-B / 2 + 0.115, 0.17), (-B / 2 + 0.1, H),
              (-B / 2, H), (-B / 2 + 0.01, H * 0.6)]
    netz.prisma(aussen, -L / 2, L / 2, st["Sandstein alt"])
    for sx in (-1, 1):                                                                                  # Stirnwände
        netz.quader((sx * (L / 2 - 0.115), 0, H / 2 + 0.03), (0.2, B - 0.2, H - 0.06), st["Sandstein alt"], ohne=("-z",))
    wasser = [(-L / 2 + 0.2, -B / 2 + 0.115, H * 0.82), (L / 2 - 0.2, -B / 2 + 0.115, H * 0.82), (L / 2 - 0.2, B / 2 - 0.115, H * 0.82),
              (-L / 2 + 0.2, B / 2 - 0.115, H * 0.82)]
    netz.vieleck_nach(wasser, (0, 0, 1), st["Eis"] if winter else st["Pfuetze"])
    for k in range(5):                                                                                  # Moosflecken und Absplitterungen
        x = z.uniform(-L / 2 + 0.3, L / 2 - 0.3)
        s = z.choice((-1, 1))
        netz.quader((x, s * (B / 2 + 0.003 + 0.003 * k), z.uniform(0.04, 0.2)), (z.uniform(0.2, 0.4), 0.012, z.uniform(0.07, 0.14)), st["Moos"])
    for sx in (-1, 1):                                                                                  # Standfuß: zwei flache Steine
        netz.quader((sx * 0.68, 0, 0.0), (0.5, B + 0.1, 0.06), st["Sandstein alt"], ohne=("-z",))
    # Brunnenrohr aus ausgehöhltem Holz, hängt auf einem Pfosten über dem linken Ende
    nt.ast(netz, st, (-L / 2 - 0.2, 0, -0.03), (-L / 2 - 0.2, 0, 1.0), 0.07, 0.06, stoff="Holz alt", n=6)
    nt.ast(netz, st, (-L / 2 - 0.2, 0, 0.95), (-L / 2 + 0.3, 0, 0.78), 0.04, 0.045, stoff="Holz alt", n=6)
    if winter:
        for s in (-1, 1):
            p2.schneestreifen(netz, st, -L / 2 + 0.02, L / 2 - 0.02, s * (B / 2 - 0.05), H + 0.002, 0.09, 0.04)
        for sx in (-1, 1):
            netz.quader((sx * (L / 2 - 0.1), 0, H + 0.01), (0.2, B - 0.2, 0.02), st["Schnee"])
        p2.schnee(netz, st, (-L / 2 - 0.2, 0, 1.0), 0.07, 0.07, 0.05, saat=saat, n=5)


def pfuetze(netz, st, saat, winter=False, rx=0.8, ry=0.55):
    """Pfütze als flaches Bodennetz: unregelmäßiger Umriss, Schlammrand, Wasser; im Winter Eis mit Schneerand."""
    z = rng(saat)
    n = 28
    p1, p2_, p3 = (z.uniform(0, 6.28) for _ in range(3))
    aussen, innen = [], []
    for k in range(n):
        w = 2 * math.pi * k / n
        f = 1 + 0.22 * math.sin(2 * w + p1) + 0.14 * math.sin(3 * w + p2_) + 0.08 * math.sin(5 * w + p3) + z.uniform(-0.03, 0.03)
        aussen.append((rx * 1.22 * f * math.cos(w), ry * 1.22 * f * math.sin(w)))
        innen.append((rx * f * math.cos(w), ry * f * math.sin(w)))
    for k in range(n):                                                                                  # Rand: Schlamm, im Winter Schnee- und Eisflecken
        j = (k + 1) % n
        stoff = ("Schnee" if k % 3 else "Schnee schatten") if winter else "Fuellung Schlamm"
        zr = 0.006 + 0.001 * (k % 2)                                                                 # versetzt, damit keine Flächen deckungsgleich liegen
        netz.vieleck_nach([(*aussen[k], zr), (*aussen[j], zr), (*innen[j], zr), (*innen[k], zr)], (0, 0, 1), st[stoff])
    netz.vieleck_nach([(*p, 0.014) for p in innen], (0, 0, 1), st["Eis" if winter else "Pfuetze"])
    if winter:                                                                                          # Risse im Eis
        for k in range(3):
            a = z.uniform(0, 6.28)
            c = Vector((rx * 0.2 * math.cos(a), ry * 0.2 * math.sin(a), 0.018))
            netz.stab(c, c + Vector((rx * 0.5 * math.cos(a + z.uniform(-0.6, 0.6)), ry * 0.5 * math.sin(a + z.uniform(-0.6, 0.6)), 0.0)), 0.012, 0.004, st["Schnee schatten"])


def pfuetze_klein(netz, st, saat, winter=False):
    pfuetze(netz, st, saat, winter, rx=0.55, ry=0.4)


def pfuetze_gross(netz, st, saat, winter=False):
    pfuetze(netz, st, saat, winter, rx=1.15, ry=0.8)


# ─────────────────────────────────────────────────────────────────────────────
#  Zusammenstellung
# ─────────────────────────────────────────────────────────────────────────────

GRUPPEN = {
    "tiere": None,                                   # Fremdmodelle, siehe tiere_bauen
    "hof2": [
        ("Huehnerstall", huehnerstall, (111,), {}, True),
        ("Schuppen", schuppen, (112,), {}, True),
    ],
    "pferch": [
        ("Gatter", gatter, (121,), {}, True),
        ("Futterraufe", futterraufe, (122,), {}, True),
        ("Strohschuettung", strohschuettung, (123,), {}, True),
    ],
    "wasserstelle": [
        ("Ziehbrunnen", ziehbrunnen, (131,), {}, True),
        ("WassertrogStein", wassertrog_stein, (132,), {}, True),
        ("PfuetzeKlein", pfuetze_klein, (133,), {}, True),
        ("PfuetzeGross", pfuetze_gross, (134,), {}, True),
    ],
}


def main():
    nur = [a.split("=", 1)[1] for a in sys.argv if a.startswith("--nur=")]
    nur = [g for s in nur for g in s.split(",")]
    for gruppe, teile in GRUPPEN.items():
        if nur and gruppe not in nur:
            continue
        name = "fuellung_" + gruppe
        sommer, winter = tiere_bauen() if teile is None else p2.knoten(teile)
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
