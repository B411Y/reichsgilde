"""Marktplatz und Marktstände – zwei Modelle aus einem Skript.

    blender -b --python tools/modelle/markt.py        oder
    python tools/modelle/markt.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/marktplatz.glb und marktstaende.glb, Bilder nach docs/modelle/.

marktplatz.glb (24 × 24 m, Ursprung in der Mitte des Platzes = `Marktplatz` im Spiel):
    Platz               Pflaster mit Fugen, Brunnen mit Säule und Wasserspeiern, Marktkreuz, drei Bänke
    Anker_Stand1–7      Stellplätze der Stände (wie heute im Spiel), Blick zum Stand = Stand schaut mit der Front zur Mitte
    Anker_Brunnen1–4    am Brunnenrand, Blick zum Brunnen (Wasser holen, „Stehen“)
    Anker_Sitz1–6       auf den Bänken, Blick vom Sitz weg („Sitzen“)
    Anker_Plausch<g>_<n> Plauderrunden aus zwei oder drei Plätzen, die sich anschauen („Reden“); g = Gruppe
    Anker_Weg1–12       Rundgang an den Ständen vorbei, in Laufrichtung nummeriert (Schlendern)
marktstaende.glb (Baukasten, Ursprung je in der Mitte des Stands, Kundenseite vorn):
    Stand_Plane         Tisch mit Pfosten und gestreifter Plane („Plane“, das Spiel kann sie je Stand färben)
    Stand_Bock          offener Tisch auf zwei Böcken
    Stand_Karren        Handkarren mit Ladebrett
    Auslage_Eisen       Werkzeug, Hufeisen, Beschläge, ein Schwert am Ständer         (Schmied)
    Auslage_Holz        Schemel, kleine Truhe, Holzzapfen, Gehstöcke                 (Tischler)
    Auslage_Stein       Schleifsteine, kleine Figur; Mühlstein vorn angelehnt         (Steinmetz)
    Auslage_Tinktur     Fläschchen, Salbentöpfe, Kräuterbündel                         (Alchimist)
    Auslage_Bier        Fässchen, Krüge, Brotlaibe                                    (Wirt)
    Auslage_Kerzen      Kerzenbündel, Bücher, Papierrollen                            (Prediger)
    Auslage_Duft        bunte Duftfläschchen, Lavendelbündel                          (Parfumeur)
    Auslage_Rohstoffe   Säcke mit Korn, Körbe mit Erz und Kräutern, Holzscheite       (Umland)
    Anker_Kunde1–3      vor dem Stand, Blick zum Stand;  Anker_Verkaeufer hinter dem Tisch, Blick nach vorn
Jede Auslage liegt auf Tischhöhe 0,85 m in einem Feld von 2,4 × 1,2 m und passt auf jede Standform; das Spiel legt
auf einen Stand, was gerade im Markt liegt (ohne Ware bleibt er leer). Alle Auslagen und Stände sind standardmäßig aus.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Pflaster": (0.55, 0.52, 0.47),
    "Plane": (0.72, 0.62, 0.38),          # Vorgabe ocker; das Spiel kann je Stand umfärben
    "Plane hell": (0.86, 0.82, 0.70),
    "Krug": (0.62, 0.40, 0.26),
    "Brot": (0.70, 0.50, 0.28),
    "Papier": (0.86, 0.82, 0.70),
    "Kerze": (0.90, 0.85, 0.66),
    "Buch": (0.42, 0.18, 0.14),
    "Flasche gruen": (0.26, 0.48, 0.40),
    "Flasche blau": (0.28, 0.36, 0.62),
    "Flasche rot": (0.62, 0.20, 0.24),
    "Sack": (0.70, 0.62, 0.46),
    "Leinen": (0.84, 0.81, 0.72),
    "Geflecht": (0.56, 0.44, 0.27),
})

TISCH = 0.85
# Stellplätze der Stände wie heute im Spiel (Godot x, z) – in Blender ist y = -z
STAENDE = [(-8, -8), (0, -9), (8, -8), (-9, 2), (9, 2), (-4, 8), (4, 8)]


def blender(x, z):
    return Vector((x, -z, 0))


# ─────────────────────────────────────────────────────────────────────────────
#  Marktplatz
# ─────────────────────────────────────────────────────────────────────────────

def platz_bauen(st):
    netz = bs.Netz("Platz")
    halb = 12.0
    netz.quader((0, 0, 0.02), (2 * halb, 2 * halb, 0.04), st["Pflaster"], ohne=("-z",))
    k = -halb + 1.0
    while k < halb - 0.5:                                  # Fugen im Pflaster, ein Meter Raster
        for a, b in (((k, -halb, 0.042), (k, halb, 0.042)), ((-halb, k, 0.043), (halb, k, 0.043))):
            richtung = Vector(b) - Vector(a)
            q = Vector((-richtung.y, richtung.x, 0)).normalized() * 0.02
            netz.vieleck_nach([Vector(a) - q, Vector(b) - q, Vector(b) + q, Vector(a) + q], (0, 0, 1), st["Fuge"])
        k += 1.0
    brunnen(netz, st)
    marktkreuz(netz, st, blender(-10.4, 10.4))
    for (x, z), dreh in (((10.6, 10.0), 0.0), ((-10.6, -3.4), 90.0), ((10.6, -3.4), -90.0)):
        bank(netz, st, blender(x, z), dreh)
    return netz.objekt()


def brunnen(netz, st):
    """Achteckiges Becken aus Sandstein, Wasser, Mittelsäule mit vier Speiern und Kugel, ein Eimer am Rand."""
    bs.achteck(netz, st, (0, 0, 0.04), 1.9, 0.0, 0.75, "Sandstein")
    bs.rund_oben(netz, st, (0, 0), 1.6, 0.62, stoff="Wasser")
    bs.achteck(netz, st, (0, 0, 0.04), 1.98, 0.7, 0.82, "Sandstein")          # Rand oben
    bs.achteck(netz, st, (0, 0, 0.0), 0.28, 0.62, 2.4, "Sandstein", n=8)
    bs.achteck(netz, st, (0, 0, 0.0), 0.42, 2.3, 2.5, "Sandstein", n=8)
    bs.achteck(netz, st, (0, 0, 0.0), 0.16, 2.5, 2.9, "Sandstein", n=8)
    lb.kegel(netz, (0, 0), 0.22, 2.9, 3.25, st["Sandstein"], n=8)
    for k in range(4):
        w = math.pi / 4 + k * math.pi / 2
        a = Vector((math.cos(w) * 0.3, math.sin(w) * 0.3, 1.85))
        b = Vector((math.cos(w) * 0.6, math.sin(w) * 0.6, 1.78))
        nt.ast(netz, st, a, b, 0.05, 0.035, stoff="Kupfer", n=5)
        nt.ast(netz, st, b, b + Vector((math.cos(w) * 0.35, math.sin(w) * 0.35, -1.1)), 0.025, 0.04, stoff="Wasser", n=4)
    bs.fass(netz, st, Vector((1.7, -1.5, 0.04)), r=0.18, h=0.32, reifen=(0.06, 0.25))


def marktkreuz(netz, st, fuss):
    """Steinkreuz auf gestuftem Sockel – Zeichen des Marktrechts."""
    for i, (b, h) in enumerate(((1.4, 0.25), (1.0, 0.25))):
        netz.quader(fuss + Vector((0, 0, 0.04 + 0.25 * i + h / 2)), (b, b, h), st["Sandstein" if i else "Bruchstein"])
    netz.quader(fuss + Vector((0, 0, 0.54 + 1.4)), (0.26, 0.26, 2.8), st["Sandstein"])
    netz.quader(fuss + Vector((0, 0, 2.95)), (1.1, 0.22, 0.22), st["Sandstein"])
    netz.quader(fuss + Vector((0, 0, 3.45)), (0.3, 0.3, 0.12), st["Sandstein"])


def bank(netz, st, fuss, dreh):
    t = Matrix.Translation(fuss) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, 0.45), (2.4, 0.42, 0.08), st["Holz"], t)
    for x in (-0.95, 0.95):
        netz.quader((x, 0, 0.225), (0.1, 0.36, 0.41), st["Holz alt"], t)


def platz_anker():
    """Alle Anker des Platzes. Drehung: Blick ins Ziel (bs.anker: 0 = Blick nach +Y in Blender)."""
    anker = []

    def blick(von, nach):
        d = Vector(nach) - Vector(von)
        return math.degrees(math.atan2(-d.x, d.y))

    mitte = Vector((0, 0, 0))
    for i, (x, z) in enumerate(STAENDE):
        p = blender(x, z)
        anker.append(bs.anker(f"Anker_Stand{i + 1}", (p.x, p.y, 0.04), blick(mitte, p)))      # Front (-Y) zur Mitte
    for k in range(4):
        w = math.pi / 4 + k * math.pi / 2 + 0.3
        p = Vector((math.cos(w) * 2.35, math.sin(w) * 2.35, 0.04))
        anker.append(bs.anker(f"Anker_Brunnen{k + 1}", tuple(p), blick(p, mitte)))
    for (x, z), dreh in (((10.6, 10.0), 0.0), ((-10.6, -3.4), 90.0), ((10.6, -3.4), -90.0)):
        t = Matrix.Translation(blender(x, z)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
        for dx in (-0.6, 0.6):
            p = t @ Vector((dx, 0, 0.45))
            anker.append(bs.anker(f"Anker_Sitz{len([a for a in anker if 'Sitz' in a.name]) + 1}", tuple(p), dreh + 180))
    gruppen = [((-4.6, -3.6), 3), ((4.8, -3.4), 2), ((-6.0, 4.6), 3), ((6.2, 4.8), 2), ((0.0, 12.0 - 1.6), 2)]
    for g, ((x, z), anzahl) in enumerate(gruppen):
        m = blender(x, z)
        for n in range(anzahl):
            w = 2 * math.pi * n / anzahl + 0.4 * g
            p = m + Vector((math.cos(w) * 0.55, math.sin(w) * 0.55, 0.04))
            anker.append(bs.anker(f"Anker_Plausch{g + 1}_{n + 1}", tuple(p), blick(p, m)))
    for k in range(12):                                         # Rundgang gegen den Uhrzeigersinn (von oben)
        w = 2 * math.pi * k / 12 + 0.13
        r = 6.4 + 0.5 * math.sin(3 * w)
        p = Vector((math.cos(w) * r, math.sin(w) * r, 0.04))
        weiter = Vector((-math.sin(w), math.cos(w), 0))
        anker.append(bs.anker(f"Anker_Weg{k + 1}", tuple(p), blick(p, p + weiter)))
    return anker


# ─────────────────────────────────────────────────────────────────────────────
#  Stände
# ─────────────────────────────────────────────────────────────────────────────

def tischplatte(netz, st, mit_beinen=True):
    netz.quader((0, 0, TISCH - 0.03), (2.8, 1.4, 0.06), st["Holz"])
    if mit_beinen:
        for x in (-1.25, 1.25):
            for y in (-0.55, 0.55):
                netz.quader((x, y, (TISCH - 0.06) / 2), (0.08, 0.08, TISCH - 0.06), st["Holz alt"])


def stand_plane(st):
    netz = bs.Netz("Stand_Plane")
    tischplatte(netz, st)
    netz.quader((0, 0.66, 0.42), (2.8, 0.04, 0.8), st["Holz alt"])               # Rückwand unten
    for x in (-1.4, 1.4):
        for y, h in ((-0.2, 2.3), (0.75, 2.05)):
            netz.quader((x, y, h / 2), (0.1, 0.1, h), st["Holz"])
    # Plane über der hinteren Tischhälfte, nach hinten fallend – von vorn und oben sieht man die Ware;
    # Bahnen abwechselnd hell und farbig, vorn ein Volant
    for i in range(6):
        x0, x1 = -1.65 + i * 0.55, -1.65 + (i + 1) * 0.55
        m = st["Plane" if i % 2 == 0 else "Plane hell"]
        ecken = [(x0, -0.35, 2.35), (x1, -0.35, 2.35), (x1, 1.0, 2.0), (x0, 1.0, 2.0)]
        netz.vieleck_nach(ecken, (0, 0.25, 1), m)
        netz.vieleck_nach([(x, y, z - 0.012) for x, y, z in ecken], (0, -0.25, -1), m)
        netz.vieleck_nach([(x0, -0.35, 2.35), (x1, -0.35, 2.35), (x1, -0.35, 2.12), (x0, -0.35, 2.12)], (0, -1, 0), m)
    return netz.objekt()


def stand_bock(st):
    netz = bs.Netz("Stand_Bock")
    netz.quader((0, 0, TISCH - 0.03), (2.8, 1.4, 0.06), st["Holz alt"])
    for x in (-1.0, 1.0):
        for s in (-1, 1):
            netz.stab((x + 0.03 * s, -0.55 * s, 0), (x + 0.03 * s, 0.55 * s, TISCH - 0.06), 0.08, 0.08, st["Holz"],
                      quer=(1, 0, 0))
        netz.quader((x, 0, TISCH - 0.09), (0.1, 1.2, 0.06), st["Holz"])
    return netz.objekt()


def stand_karren(st):
    netz = bs.Netz("Stand_Karren")
    netz.quader((0, 0, TISCH - 0.03), (2.6, 1.4, 0.06), st["Holz alt"])           # Ladebrett
    for s in (-1, 1):
        netz.quader((0, s * 0.72, TISCH + 0.12), (2.6, 0.05, 0.24), st["Holz alt"])
        rad = Matrix.Translation((0.3, s * 0.82, 0.55)) @ Matrix.Rotation(math.radians(90), 4, "X")
        profil = [(0.55 * math.cos(2 * math.pi * k / 10), 0.55 * math.sin(2 * math.pi * k / 10)) for k in range(10)]
        netz.prisma(profil, -0.05, 0.05, st["Holz"], matrix=rad @ Matrix.Rotation(math.radians(90), 4, "Y"),
                    m_enden=st["Holz alt"])
    netz.stab((0.3, -0.85, 0.55), (0.3, 0.85, 0.55), 0.07, 0.07, st["Eisen"])
    for s in (-1, 1):                                                               # Deichsel und Stütze
        netz.stab((-1.3, s * 0.5, TISCH - 0.08), (-2.3, s * 0.35, 0.5), 0.07, 0.07, st["Holz"])
    netz.quader((-1.2, 0, (TISCH - 0.06) / 2), (0.08, 0.08, TISCH - 0.06), st["Holz"])
    return netz.objekt()


def auslage(name, st):
    """Ware auf dem Tisch (z = TISCH), Feld 2,4 × 1,2 m."""
    netz = bs.Netz("Auslage_" + name)
    z = TISCH
    if name == "Eisen":
        for i in range(4):                                                         # Hämmer
            netz.quader((-1.0 + 0.22 * i, -0.3, z + 0.02), (0.05, 0.4, 0.04), st["Holz"])
            netz.quader((-1.0 + 0.22 * i, -0.48, z + 0.05), (0.14, 0.07, 0.1), st["Eisen"])
        for i in range(5):                                                         # Hufeisen
            w = 0.3 * i
            netz.quader((-0.1 + 0.2 * i, 0.1, z + 0.015 + 0.02 * (i % 2)), (0.16, 0.16, 0.03), st["Eisen"])
        for i in range(3):                                                         # Beschläge
            netz.quader((0.5 + 0.18 * i, -0.35, z + 0.01), (0.12, 0.3, 0.02), st["Eisen"])
        netz.quader((1.0, 0.35, z + 0.25), (0.3, 0.2, 0.5), st["Holz"])           # Schwertständer
        netz.stab((1.0, 0.35, z + 0.5), (1.0, 0.35, z + 1.25), 0.05, 0.015, st["Silber"])
        netz.stab((0.88, 0.35, z + 0.52), (1.12, 0.35, z + 0.52), 0.04, 0.04, st["Gold"])
    elif name == "Holz":
        schemel(netz, st, Vector((-0.75, 0.0, z)))
        netz.quader((0.35, 0.15, z + 0.2), (0.7, 0.42, 0.4), st["Holz"])          # Truhe
        netz.quader((0.35, 0.15, z + 0.42), (0.74, 0.46, 0.06), st["Schnittholz"])
        netz.quader((0.35, -0.065, z + 0.22), (0.08, 0.02, 0.1), st["Eisen"])
        for i in range(6):
            lb.saeule(netz, st, (0.9 + 0.08 * (i % 3), -0.35 + 0.09 * (i // 3), z), 0.035, 0, 0.12, "Schnittholz", n=6)
        for i in range(3):
            netz.stab((-1.15 + 0.08 * i, 0.4 + 0.07 * i, z), (-1.1 + 0.08 * i, 0.45 + 0.07 * i, z + 1.0), 0.04, 0.04, st["Holz"])
    elif name == "Stein":
        for i in range(4):
            netz.quader((-0.9 + 0.32 * i, -0.25, z + 0.04), (0.26, 0.12, 0.08), st["Steinstaub"])
        netz.quader((0.5, 0.15, z + 0.06), (0.4, 0.4, 0.12), st["Sandstein"])
        nt.ast(netz, st, (0.5, 0.15, z + 0.12), (0.5, 0.15, z + 0.55), 0.1, 0.07, stoff="Sandstein", n=6)
        lb.kegel(netz, (0.5, 0.15), 0.09, z + 0.55, z + 0.72, st["Sandstein"], n=6)
        mitte = Vector((0, -0.85, 0.42))                                           # Mühlstein vorn angelehnt
        m = Matrix.Translation(mitte) @ Matrix.Rotation(math.radians(80), 4, "X")
        profil = [(0.42 * math.cos(2 * math.pi * k / 12), 0.42 * math.sin(2 * math.pi * k / 12)) for k in range(12)]
        netz.prisma(profil, -0.08, 0.08, st["Bruchstein"], matrix=m @ Matrix.Rotation(math.radians(90), 4, "Y"))
    elif name == "Tinktur":
        flaschen(netz, st, z, ("Flasche gruen", "Elixier", "Flasche gruen", "Farbe"), 9)
        for i in range(4):
            lb.saeule(netz, st, (0.4 + 0.22 * i, -0.35, z), 0.08, 0, 0.12, "Krug", enden="Leinen", n=8)
        for i in range(5):                                                         # Kräuterbündel an einer Leiste
            lb.kegel(netz, (-1.0 + 0.5 * i, 0.55), 0.09, z + 0.8, z + 0.45, st["Heu" if i % 2 else "Kraut"], n=5)
        netz.quader((0, 0.55, z + 0.82), (2.6, 0.05, 0.05), st["Holz"])
    elif name == "Bier":
        for i, (x, y) in enumerate(((-0.8, 0.2), (-0.35, 0.25), (-0.6, -0.25))):
            bs.fass(netz, st, Vector((x, y, z)), r=0.2, h=0.42, reifen=(0.08, 0.32))
        for i in range(5):
            lb.saeule(netz, st, (0.1 + 0.18 * i, -0.3, z), 0.06, 0, 0.18, "Krug", n=7)
        for i in range(4):
            nt.krone(netz, st, (0.35 + 0.28 * i, 0.25, z + 0.07), 0.13, 0.07, saat=i, stoffe=("Brot", "Brot"), n=6,
                     flach_unten=0.0)
    elif name == "Kerzen":
        for b in range(3):
            for i in range(5):
                lb.saeule(netz, st, (-1.0 + 0.05 * i + 0.4 * b, -0.2 + 0.03 * (i % 2), z), 0.022, 0, 0.32, "Kerze", n=5)
        for i in range(4):
            netz.quader((0.3 + 0.07 * (i % 2), 0.1, z + 0.04 + 0.08 * i), (0.45, 0.32, 0.075), st["Buch" if i % 2 else "Leder"])
            netz.quader((0.3 + 0.07 * (i % 2) + 0.004, 0.1, z + 0.04 + 0.08 * i), (0.43, 0.3, 0.06), st["Papier"])
        for i in range(4):
            nt.ast(netz, st, (0.8, -0.3 + 0.12 * i, z + 0.05), (1.15, -0.3 + 0.12 * i, z + 0.05), 0.045, 0.045,
                   stoff="Papier", n=6)
    elif name == "Duft":
        flaschen(netz, st, z, ("Flasche blau", "Violett", "Rose", "Flasche rot"), 12)
        for i in range(4):
            lb.kegel(netz, (0.5 + 0.18 * i, 0.3), 0.08, z, z + 0.32, st["Lavendel"], st["Kraut"], n=5)
    elif name == "Rohstoffe":
        for i, (x, y, stoff) in enumerate(((-0.85, 0.1, "Korn"), (-0.35, 0.15, "Roggen"))):
            sack(netz, st, Vector((x, y, z)), stoff)
        for x, ware in ((0.25, "Erz"), (0.75, "Kraut")):
            lb.saeule(netz, st, (x, -0.15, z), 0.24, 0, 0.22, "Geflecht", n=8)
            lb.haufen(netz, st, (x, -0.15, z + 0.2), 0.2, 0.12, ware, "Fels dunkel" if ware == "Erz" else "Laub hell",
                      saat=3, n=6)
        for i in range(3):
            lb.stamm(netz, st, (0.9 + 0.13 * i, 0.15, z + 0.06), (0.9 + 0.13 * i, 0.55, z + 0.06), 0.06, n=6)
    return netz.objekt()


def schemel(netz, st, fuss):
    netz.quader(fuss + Vector((0, 0, 0.42)), (0.4, 0.4, 0.05), st["Schnittholz"])
    for dx in (-0.14, 0.14):
        for dy in (-0.14, 0.14):
            netz.quader(fuss + Vector((dx, dy, 0.2)), (0.05, 0.05, 0.4), st["Holz"])


def flaschen(netz, st, z, stoffe, anzahl):
    for i in range(anzahl):
        x = -1.05 + (i % 6) * 0.2
        y = -0.3 + (i // 6) * 0.25
        stoff = stoffe[i % len(stoffe)]
        lb.saeule(netz, st, (x, y, z), 0.055, 0, 0.16, stoff, n=6)
        nt.ast(netz, st, (x, y, z + 0.16), (x, y, z + 0.24), 0.025, 0.02, stoff=stoff, n=5)
        netz.quader((x, y, z + 0.255), (0.04, 0.04, 0.03), st["Holz"])


def sack(netz, st, fuss, inhalt):
    from natur import krone
    lb.saeule(netz, st, (fuss.x, fuss.y, fuss.z), 0.2, 0, 0.36, "Sack", n=8)
    krone(netz, st, fuss + Vector((0, 0, 0.42)), 0.18, 0.08, saat=7, stoffe=(inhalt, inhalt), n=7, flach_unten=0.0)


def stand_anker():
    """Anker relativ zum Stand: Kunden vorn (Blick zum Stand, nach +Y), Verkäufer hinten (Blick nach vorn)."""
    anker = [bs.anker(f"Anker_Kunde{i + 1}", (x, -1.45, 0.0), 0.0) for i, x in enumerate((-0.8, 0.15, 0.95))]
    anker.append(bs.anker("Anker_Verkaeufer", (0.0, 1.05, 0.0), 180.0))
    return anker


AUSLAGEN = ["Eisen", "Holz", "Stein", "Tinktur", "Bier", "Kerzen", "Duft", "Rohstoffe"]


def staende_bauen(st):
    objekte = [stand_plane(st), stand_bock(st), stand_karren(st)] + [auslage(n, st) for n in AUSLAGEN]
    for o in objekte:
        o["teil"] = True
        o["sichtbar"] = False
    return objekte + stand_anker()


# ─────────────────────────────────────────────────────────────────────────────
#  Ausgabe und Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def main():
    import bpy
    schnell = "--schnell" in sys.argv
    bs.neue_szene()
    st = bs.Stoffe()
    platz = [platz_bauen(st)] + platz_anker()
    bs.bericht("marktplatz", platz)
    print("geschrieben:", bs.exportieren("marktplatz", platz))
    bs.neue_szene()
    st = bs.Stoffe()
    staende = staende_bauen(st)
    bs.bericht("marktstaende", staende, einzeln=True)
    print("geschrieben:", bs.exportieren("marktstaende", staende))
    if "--ohne-bilder" in sys.argv:
        return
    vorschau_staende(staende, schnell)
    vorschau_platz(schnell)


def vorschau_staende(objekte, schnell):
    """Alle Auslagen je auf einem Stand (Formen im Wechsel), dazu die Anker eines Stands."""
    import bpy
    netze = {o.name: o for o in objekte if o.type == "MESH"}
    formen = ["Stand_Plane", "Stand_Bock", "Stand_Karren"]
    sichtbar = []
    for i, name in enumerate(AUSLAGEN):
        sx, sy = (i % 4 - 1.5) * 3.6, (0.5 - i // 4) * 4.2
        ort = Vector((sx * 0.7071 - sy * 0.7071, sx * 0.7071 + sy * 0.7071, 0))
        for teil in (formen[i % 3], "Auslage_" + name):
            k = netze[teil].copy()
            k.location = ort
            bpy.context.scene.collection.objects.link(k)
            sichtbar.append(k)
    bs.vorschau_buehne(None, ziel_hoehe=1.0, ausschnitt=16.0, schnell=schnell)
    print(bs.rendern("marktstaende", lambda o: o in sichtbar))


def vorschau_platz(schnell):
    """Der ganze Platz mit Ständen auf den Stellplätzen (Auslagen im Wechsel) und den Ankern."""
    import bpy
    bs.neue_szene()
    st = bs.Stoffe()
    platz = [platz_bauen(st)] + platz_anker()
    staende = staende_bauen(st)
    netze = {o.name: o for o in staende if o.type == "MESH"}
    formen = ["Stand_Plane", "Stand_Plane", "Stand_Bock", "Stand_Karren"]
    sichtbar = [platz[0]]
    for i, a in enumerate([a for a in platz if a.name.startswith("Anker_Stand")]):
        for teil in (formen[i % 4], "Auslage_" + AUSLAGEN[i % len(AUSLAGEN)]):
            k = netze[teil].copy()
            k.location, k.rotation_euler = a.location.copy(), a.rotation_euler.copy()
            bpy.context.scene.collection.objects.link(k)
            sichtbar.append(k)
    for o in staende:
        if o.type != "MESH":
            bpy.data.objects.remove(o)
    bs.vorschau_buehne(None, ziel_hoehe=1.0, ausschnitt=30.0, schnell=schnell)
    print(bs.rendern("marktplatz", lambda o: o in sichtbar))
    print(bs.anker_bild("marktplatz", lambda o: o in sichtbar))


if __name__ == "__main__":
    main()
