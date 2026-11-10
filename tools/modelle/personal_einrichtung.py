"""Neue Einrichtung aus „Personal und Talentbaum“ (PR #224) – die Stücke, die im Kern schon kaufbar sind, aber noch kein Modell haben.

Werkstätten (Stufe 3, Knoten `Einrichtung_<Kennung>` im Raummodell des Berufs, gekauft erst sichtbar):
    Schleifstein       Tretrad-Schleifstein        Schmied      (Goldkette, Prunkschwert)
    Drechselbank       Drechselbank                Tischler     (Schrank, Prunktruhe)
    Steinsaege         Steinsäge                   Steinmetz    (Steinfigur, Grabmal)
    Athanor            Athanor                     Alchimist    (Theriak, Zinnober)
    Enfleurage         Enfleurage-Kammer           Parfumeur    (Heiligenduft, Schattendolch)
    Gewoelbekeller     Gewölbekeller               Wirt         (Festbier, Schädelbrand)
    Skriptorium        Skriptorium                 Prediger     (Stundenbuch, Reliquiar)
    Ladentheke         Ladentheke                  Schmied, Tischler, Steinmetz, Alchimist, Parfumeur, Wirt, Prediger
    Lehrlingsstube     Lehrlingsstube              alle zehn Berufe
Wohnhaus (Wohnstube des Hauses, Stadtansicht.Raeume.cs; Knoten wie die Kennung des Kerns):
    Vorratskammer, Kueche, Hauswappen, Stube, Bibliothek, Riegel, Hund

Jedes Stück ist eine Funktion `(netz, st, M, …)` im örtlichen System `M` wie `rahmen(x, y, drehung)` der Raumskripte: Ursprung auf dem
Boden an der Wand (Mitte des Stücks), `+y` zeigt in den Raum, `z` oben, `+x` entlang der Wand. Die Raumskripte (`*_innen.py`) rufen sie
mit ihrem `teil("Einrichtung_<Kennung>")` auf und stellen `M` an die Stelle, die frei ist; `personal_vorschau.py` zeigt sie.
Gebaut wie die übrigen Innenräume: wenige Vielecke, Materialien aus `bausteine.FARBEN` (die Oberflächenstruktur kommt im Spiel aus der
Material-Bibliothek nach dem Namen des Materials).
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Wolle": (0.52, 0.47, 0.40),           # Decken, Teppichgrund
    "Decke rot": (0.50, 0.17, 0.15),
    "Decke blau": (0.20, 0.27, 0.45),
    "Strohsack": (0.62, 0.55, 0.38),
    "Ziegelstein": (0.55, 0.30, 0.22),     # Athanor, Gewölbe
    "Ton": (0.58, 0.38, 0.26),             # Töpfe, Tiegel
    "Glasur grün": (0.24, 0.38, 0.28),     # Kacheln der guten Stube
    "Pergament": (0.86, 0.80, 0.64),
    "Lapis": (0.14, 0.22, 0.52),           # Lapislazuli am Farbtisch
    "Fett": (0.88, 0.84, 0.70),            # Pomade im Rahmen der Enfleurage
    "Marmor": (0.82, 0.80, 0.76),          # Block auf der Steinsäge
    "Sandbrei": (0.52, 0.44, 0.32),
    "Schiefer tafel": (0.16, 0.17, 0.20),
    "Hundfell": (0.50, 0.38, 0.26),
    "Hundfell grau": (0.42, 0.40, 0.38),
    "Buch rot": (0.45, 0.12, 0.10),
    "Buch grün": (0.16, 0.30, 0.20),
    "Buch blau": (0.17, 0.22, 0.42),
    "Buch braun": (0.30, 0.20, 0.12),
    "Leinen": (0.84, 0.81, 0.72),
    "Zinn": (0.62, 0.63, 0.66),
    "Fels Sandstein": (0.66, 0.57, 0.43),  # Schleifstein (roher Sandstein, wie beim Steinmetz)
})
bs.METALL.update({"Zinn": 0.6})


def rahmen(x, y, drehung, z=0.0):
    """Wie `rahmen` der Raumskripte: Ursprung (x, y, z), +y zeigt in den Raum (drehung 0 = Vorderwand, 180 Rückwand, −90 linke, 90 rechte Wand)."""
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(drehung), 4, "Z")


def q(netz, st, M, mitte, masse, stoff, ohne=()):
    netz.quader(mitte, masse, st[stoff], M, ohne=ohne)


def v(M, p):
    return M @ Vector(p)


def balken(netz, st, M, a, b, breite, dicke, stoff, quer=(0, 1, 0)):
    """Balken von a nach b (örtlich); `dicke` liegt entlang `quer` (örtlich)."""
    netz.stab(v(M, a), v(M, b), breite, dicke, st[stoff], quer=M.to_3x3() @ Vector(quer))


def walze(netz, st, M, mitte, r, y0, y1, stoff, n=12, m_enden=None, achse="y"):
    """Walze (Rad, Stein, Fass liegend) mit Achse entlang örtlich y (oder x), von y0 bis y1 um `mitte` = (x, z)."""
    profil = [(r * math.cos(2 * math.pi * k / n), r * math.sin(2 * math.pi * k / n)) for k in range(n)]
    if achse == "y":
        t = M @ Matrix.Translation((mitte[0], 0, mitte[1])) @ Matrix.Rotation(math.radians(90), 4, "Z")   # Prisma-x → örtlich y
    else:
        t = M @ Matrix.Translation((0, mitte[0], mitte[1]))                                                  # Achse entlang x, mitte = (y, z)
    netz.prisma(profil, y0, y1, st[stoff], matrix=t, m_enden=st[m_enden] if m_enden else None)


def speichenrad(netz, st, M, c, r, y0, y1, n=14, speichen=6, stoff="Holz", tritte=True, felge=0.07):
    """Rad mit Achse entlang örtlich y: zwei Felgenringe bei y0 und y1, Speichen, auf Wunsch Trittbretter dazwischen (Tretrad)."""
    cx, cz = c
    pt = [(cx + r * math.sin(2 * math.pi * k / n), cz + r * math.cos(2 * math.pi * k / n)) for k in range(n)]
    for y in (y0, y1):
        for k in range(n):
            a, b = pt[k], pt[(k + 1) % n]
            balken(netz, st, M, (a[0], y, a[1]), (b[0], y, b[1]), felge, 0.05, stoff, quer=(0, 1, 0))
        for k in range(speichen):
            w = 2 * math.pi * k / speichen + 0.2
            balken(netz, st, M, (cx, y, cz), (cx + (r - felge) * math.sin(w), y, cz + (r - felge) * math.cos(w)), 0.06, 0.05, stoff, quer=(0, 1, 0))
    if tritte:
        for k in range(n):
            a, b = pt[k], pt[(k + 1) % n]
            mx, mz = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            radial = (mx - cx, 0, mz - cz)
            l = math.hypot(*radial[::2])
            radial = (radial[0] / l, 0, radial[2] / l)
            balken(netz, st, M, (mx, y0, mz), (mx, y1, mz), 0.4 * r * math.sin(math.pi / n) * 2, 0.035, "Holz alt", quer=radial)


def hocker(netz, st, M, x, y, h=0.45, r=0.17, stoff="Holz"):
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        netz.stab(v(M, (x + dx * r * 0.7, y + dy * r * 0.7, 0)), v(M, (x + dx * r * 0.85, y + dy * r * 0.85, h - 0.03)), 0.04, 0.04, st[stoff])
    q(netz, st, M, (x, y, h - 0.015), (2 * r, 2 * r, 0.04), stoff)


def buch(netz, st, M, mitte, masse, stoff="Buch rot", dreh=0.0):
    """Liegendes Buch: Deckel in `stoff`, Seiten hell (Block mitte, masse = x, y, z)."""
    T = M @ Matrix.Translation(mitte) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, 0), masse, st[stoff], T)
    netz.quader((0.01, 0, 0), (masse[0] - 0.02, masse[1] - 0.02, masse[2] - 0.012), st["Pergament"], T, ohne=("-x",))


def krug(netz, st, M, x, y, z=0.0, r=0.045, h=0.13, stoff="Ton"):
    bs.achteck(netz, st, v(M, (x, y, z)), r, 0.0, h, stoff, n=8)


def regal_buecher(netz, st, M, breite, hoehe, tiefe, boeden, saat=0, voll=0.92):
    """Bücherregal an der Wand: Seiten, Böden, Buchrücken in bunten Farben, stehend, mit Lücken und ein paar liegenden."""
    z = random.Random(saat)
    for x in (-breite / 2, breite / 2):
        q(netz, st, M, (x, tiefe / 2, hoehe / 2), (0.05, tiefe, hoehe), "Holz", ohne=("-y",))
    q(netz, st, M, (0, 0.01, hoehe / 2), (breite, 0.02, hoehe), "Holz alt", ohne=("-y",))
    farben = ("Buch rot", "Buch grün", "Buch blau", "Buch braun", "Leder")
    for k in range(boeden + 1):
        zb = 0.04 + (hoehe - 0.08) * k / boeden
        q(netz, st, M, (0, tiefe / 2, zb), (breite - 0.05, tiefe, 0.035), "Holz", ohne=("-y",))
        if k == boeden:
            break
        hoch = (hoehe - 0.08) / boeden - 0.07
        x = -breite / 2 + 0.06
        while x < breite / 2 - 0.1:
            d = z.uniform(0.035, 0.075)
            if z.random() > voll:
                x += d
                continue
            h = hoch * z.uniform(0.72, 0.98)
            if x + d > breite / 2 - 0.06:
                break
            q(netz, st, M, (x + d / 2, tiefe * 0.45, zb + 0.0175 + h / 2), (d - 0.004, tiefe * 0.62, h), z.choice(farben))
            x += d + 0.004


# ═════════════════════════════════════════════════════════════════════════════
#  Werkstätten (Stufe 3)
# ═════════════════════════════════════════════════════════════════════════════

def schleifstein(netz, st, M, r=0.78):
    """Tretrad-Schleifstein (Schmied): Ein großes Tretrad (Trittbretter zwischen zwei Felgen, Speichen) läuft auf einer Achse mit dem Schleifstein davor;
    der Stein dreht sich im Wassertrog, daneben ein Wetzstein-Brett. Etwa 1,7 m breit, 1,1 m tief, 1,75 m hoch."""
    cx, cz = -0.2, r + 0.1
    y0, y1 = 0.12, 0.62
    speichenrad(netz, st, M, (cx, cz), r, y0, y1, n=14, speichen=6)
    walze(netz, st, M, (cx, cz), 0.13, 0.04, 0.98, "Holz", n=8)                                 # Nabe und Achse
    balken(netz, st, M, (cx, 0.04, cz), (cx, 0.98, cz), 0.05, 0.05, "Eisen", quer=(1, 0, 0))
    for y in (0.05, 0.88):                                                                       # Achsböcke (Pfosten schräg zum Boden)
        for s in (-1, 1):
            balken(netz, st, M, (cx + s * 0.62, y, 0.0), (cx, y, cz), 0.09, 0.07, "Holz", quer=(0, 1, 0))
        q(netz, st, M, (cx, y, 0.04), (1.3, 0.09, 0.07), "Holz")
    walze(netz, st, M, (cx, cz), 0.43, 0.70, 0.86, "Fels Sandstein", n=14, m_enden="Steinstaub")  # der Schleifstein vor dem Rad
    # Wassertrog unter dem Stein
    q(netz, st, M, (cx, 0.78, 0.2), (0.95, 0.3, 0.26), "Holz alt", ohne=("+z",))
    netz.vieleck_nach([v(M, (cx - 0.44, 0.65, 0.31)), v(M, (cx + 0.44, 0.65, 0.31)), v(M, (cx + 0.44, 0.91, 0.31)), v(M, (cx - 0.44, 0.91, 0.31))],
                      (0, 0, 1), st["Wasser"])
    for s in (-1, 1):
        q(netz, st, M, (cx + s * 0.485, 0.78, 0.2), (0.04, 0.3, 0.26), "Holz")
    # Trittbrett-Aufgang und Wetzbrett
    q(netz, st, M, (cx, 0.5, 0.07), (0.7, 0.4, 0.05), "Holz alt")
    q(netz, st, M, (cx + 1.05, 0.25, 0.45), (0.36, 0.3, 0.04), "Holz")
    for s in (-1, 1):
        balken(netz, st, M, (cx + 1.05 + s * 0.14, 0.25, 0.0), (cx + 1.05 + s * 0.14, 0.25, 0.43), 0.05, 0.05, "Holz")
    q(netz, st, M, (cx + 1.0, 0.25, 0.49), (0.16, 0.06, 0.03), "Steinstaub")
    for k in range(3):                                                                           # fertig geschliffene Klingen
        balken(netz, st, M, (cx + 0.9, 0.18 + 0.05 * k, 0.485), (cx + 1.2, 0.2 + 0.05 * k, 0.485), 0.025, 0.012, "Silber")


def drechselbank(netz, st, M):
    """Drechselbank des Tischlers: Wangen mit Spindelstock, Reitstock und Auflage, ein halb gedrechselter Pfosten eingespannt, darunter das Schwungrad
    mit Tritt, an der Wand das Drechseleisen-Brett; Späne am Boden. Etwa 1,9 m breit, 0,8 m tief, 1,3 m hoch."""
    hoehe, y = 0.98, 0.45
    for x in (-0.78, 0.78):                                                                       # Böcke
        for sy in (-1, 1):
            balken(netz, st, M, (x, y + sy * 0.18, 0.0), (x, y + sy * 0.14, hoehe - 0.05), 0.09, 0.09, "Holz")
        q(netz, st, M, (x, y, 0.15), (0.08, 0.4, 0.07), "Holz")
    for sy in (-1, 1):                                                                            # zwei Wangen
        q(netz, st, M, (0, y + sy * 0.07, hoehe), (1.8, 0.07, 0.1), "Schnittholz")
    q(netz, st, M, (-0.55, y, hoehe + 0.14), (0.28, 0.24, 0.2), "Holz alt")                        # Spindelstock
    q(netz, st, M, (0.6, y, hoehe + 0.13), (0.14, 0.22, 0.18), "Holz alt")                         # Reitstock
    balken(netz, st, M, (-0.4, y, hoehe + 0.14), (-0.3, y, hoehe + 0.14), 0.04, 0.04, "Eisen", quer=(0, 1, 0))
    balken(netz, st, M, (0.5, y, hoehe + 0.14), (0.54, y, hoehe + 0.14), 0.035, 0.035, "Eisen", quer=(0, 1, 0))
    balken(netz, st, M, (0.6, y, hoehe + 0.14), (0.76, y, hoehe + 0.14), 0.025, 0.025, "Eisen")   # Spindel des Reitstocks
    q(netz, st, M, (0.78, y, hoehe + 0.14), (0.03, 0.12, 0.12), "Holz")
    # Auflage (Support) vorn
    balken(netz, st, M, (-0.2, y + 0.2, hoehe + 0.03), (-0.2, y + 0.2, hoehe + 0.13), 0.04, 0.04, "Eisen")
    q(netz, st, M, (0.0, y + 0.19, hoehe + 0.14), (0.6, 0.035, 0.03), "Eisen")
    # Werkstück: Baluster – Wulst, Hals, Scheibe, gedrehte Walzen
    spindel = [(-0.3, 0.08), (-0.22, 0.05), (-0.14, 0.085), (-0.06, 0.045), (0.0, 0.08), (0.08, 0.045), (0.16, 0.08), (0.26, 0.055), (0.34, 0.09), (0.5, 0.09)]
    for (xa, ra), (xb, rb) in zip(spindel, spindel[1:]):
        walze(netz, st, M, (y, hoehe + 0.14), (ra + rb) / 2, xa, xb, "Schnittholz", n=8, achse="x")
    # Schwungrad unter der Bank, Kurbel, Tritt
    walze(netz, st, M, (y - 0.17, 0.42), 0.3, -0.62, -0.5, "Holz alt", n=14, achse="x")
    for k in range(4):
        w = math.pi * k / 4
        balken(netz, st, M, (-0.5, y - 0.17 + 0.3 * math.sin(w), 0.42 + 0.3 * math.cos(w)),
               (-0.5, y - 0.17 - 0.3 * math.sin(w), 0.42 - 0.3 * math.cos(w)), 0.04, 0.04, "Holz", quer=(1, 0, 0))
    q(netz, st, M, (-0.25, y - 0.18, 0.1), (0.9, 0.18, 0.035), "Holz")                              # Trittbrett
    balken(netz, st, M, (-0.55, y - 0.17, 0.12), (-0.5, y - 0.17, 0.52), 0.025, 0.025, "Eisen")     # Zugstange
    q(netz, st, M, (0.55, y - 0.18, 0.1), (0.1, 0.22, 0.1), "Holz")                                  # Drehpunkt des Tritts
    # Drechseleisen-Brett an der Wand und Spanhaufen
    q(netz, st, M, (0.0, 0.03, 1.55), (1.4, 0.05, 0.28), "Holz alt", ohne=("-y",))
    for k in range(7):
        x = -0.6 + 0.2 * k
        balken(netz, st, M, (x, 0.07, 1.46), (x, 0.07, 1.64), 0.025, 0.02, "Eisen")
        balken(netz, st, M, (x, 0.07, 1.64), (x, 0.07, 1.72), 0.03, 0.03, "Holz")
    rnd = random.Random(4)
    for k in range(18):
        a = rnd.uniform(0, math.tau)
        d = rnd.uniform(0.0, 0.55)
        q(netz, st, M, (0.0 + d * math.cos(a), y + 0.1 + 0.4 * d * math.sin(a), 0.012), (rnd.uniform(0.08, 0.16), rnd.uniform(0.03, 0.06), 0.012), "Schnittholz")


def steinsaege(netz, st, M):
    """Steinsäge des Steinmetzen: ein Gestell aus zwei Pfosten und Querbalken, daran hängt das Sägeblatt im Rahmen über einem Marmorblock auf
    Unterlagshölzern; Sandtrichter, Wassereimer, zwei fertige Platten an der Wand. Etwa 2,1 m breit, 1,0 m tief, 2,0 m hoch."""
    y = 0.55
    for x in (-0.95, 0.95):
        for sy in (-1, 1):
            balken(netz, st, M, (x, y + sy * 0.3, 0.0), (x, y + sy * 0.3, 1.95), 0.11, 0.11, "Holz")
        balken(netz, st, M, (x, y - 0.3, 1.85), (x, y + 0.3, 1.85), 0.1, 0.1, "Holz", quer=(1, 0, 0))
        balken(netz, st, M, (x, y - 0.3, 0.15), (x, y + 0.3, 0.15), 0.1, 0.08, "Holz", quer=(1, 0, 0))
    balken(netz, st, M, (-0.95, y + 0.3, 1.85), (0.95, y + 0.3, 1.85), 0.12, 0.1, "Holz", quer=(0, 1, 0))
    balken(netz, st, M, (-0.95, y - 0.3, 1.85), (0.95, y - 0.3, 1.85), 0.12, 0.1, "Holz", quer=(0, 1, 0))
    # Blockwagen: zwei Kufen und der Marmorblock darauf
    for sy in (-1, 1):
        q(netz, st, M, (0.0, y + sy * 0.22, 0.09), (1.9, 0.1, 0.12), "Holz alt")
    q(netz, st, M, (0.0, y, 0.45), (1.2, 0.46, 0.5), "Marmor")
    q(netz, st, M, (0.0, y, 0.2), (1.3, 0.5, 0.06), "Holz alt")
    # Sägerahmen: zwei Seitenholme, Querholme oben und unten, Blatt (Eisen) in der Mitte, im Schnitt
    for x in (-0.22, 0.22):
        balken(netz, st, M, (x, y, 0.62), (x, y, 1.55), 0.07, 0.05, "Holz", quer=(0, 1, 0))
    q(netz, st, M, (0.0, y, 1.6), (0.56, 0.06, 0.07), "Holz")
    q(netz, st, M, (0.0, y, 0.72), (0.56, 0.05, 0.06), "Holz")
    q(netz, st, M, (0.0, y, 1.1), (0.012, 0.03, 0.84), "Eisen")                                         # Blatt (dünner Streifen)
    balken(netz, st, M, (0.0, y, 1.6), (0.0, y, 1.85), 0.03, 0.03, "Seil")                              # an der Schwinge
    # Trichter mit Sandbrei oben auf dem Gestell
    q(netz, st, M, (0.45, y, 1.98), (0.3, 0.3, 0.18), "Holz alt", ohne=("+z",))
    netz.vieleck_nach([v(M, (0.32, y - 0.12, 2.06)), v(M, (0.58, y - 0.12, 2.06)), v(M, (0.58, y + 0.12, 2.06)), v(M, (0.32, y + 0.12, 2.06))],
                      (0, 0, 1), st["Sandbrei"])
    balken(netz, st, M, (0.45, y, 1.9), (0.1, y, 1.22), 0.025, 0.025, "Eisen")                         # Rinne zum Schnitt
    # Wassereimer, fertige Platten
    bs.achteck(netz, st, v(M, (1.18, 0.35, 0)), 0.14, 0.0, 0.3, "Holz alt", n=8)
    bs.rund_oben(netz, st, v(M, (1.18, 0.35, 0)), 0.125, 0.29, "Wasser", n=8)
    for k in range(2):
        T = M @ Matrix.Translation((-1.28 + 0.1 * k, 0.14, 0.7)) @ Matrix.Rotation(math.radians(-8 + 4 * k), 4, "Y")
        netz.quader((0, 0, 0), (0.08, 0.5, 1.4), st["Marmor"], T)


def athanor(netz, st, M):
    """Athanor des Alchimisten: der „bequeme“ Ofen. Gemauerter Sockel mit Feuerloch und Aschentür, darauf der Turm mit Haube und dem Kupferhelm,
    seitlich der Kohlenschacht mit Trichter, im Turm ein Fenster mit dem Kolben im Sandbad. Etwa 1,1 m breit, 1,0 m tief, 2,0 m hoch."""
    y = 0.55
    q(netz, st, M, (0.0, y, 0.38), (0.95, 0.8, 0.76), "Ziegelstein", ohne=("-y",))               # Sockel
    q(netz, st, M, (0.0, y, 0.78), (1.02, 0.86, 0.06), "Bruchstein dunkel")                          # Deckplatte
    q(netz, st, M, (0.0, y + 0.401, 0.52), (0.46, 0.012, 0.34), "Glut")                              # Feuerloch (glüht)
    for dz in (0.17, -0.17):
        q(netz, st, M, (0.0, y + 0.41, 0.52 + dz), (0.52, 0.02, 0.04), "Eisen")
    for dx in (-0.25, 0.25):
        q(netz, st, M, (dx, y + 0.41, 0.52), (0.04, 0.02, 0.38), "Eisen")
    q(netz, st, M, (0.0, y + 0.401, 0.15), (0.34, 0.012, 0.18), "Eisen")                             # Aschentür
    q(netz, st, M, (0.0, y + 0.414, 0.15), (0.08, 0.02, 0.03), "Kohle")
    # Turm: gemauerter Zylinder, zwei Eisenbänder, Fenster, Haube
    bs.achteck(netz, st, v(M, (0.0, y, 0.81)), 0.36, 0.0, 0.62, "Ziegelstein", n=10)
    for z in (0.12, 0.45):
        bs.achteck(netz, st, v(M, (0.0, y, 0.81)), 0.375, z, z + 0.04, "Eisen", n=10)
    q(netz, st, M, (0.0, y + 0.37, 1.2), (0.2, 0.025, 0.26), "Glas")                                 # Sichtfenster
    q(netz, st, M, (0.0, y + 0.378, 1.2), (0.22, 0.012, 0.28), "Eisen", ohne=("+y",))
    bs.achteck(netz, st, v(M, (0.0, y, 1.43)), 0.34, 0.0, 0.08, "Bruchstein dunkel", n=10)
    bs.turmhelm(netz, st, v(M, (0.0, y, 1.51)), 0.33, 0.0, 0.34, stoff="Kupfer", n=10)             # Haube
    bs.achteck(netz, st, v(M, (0.0, y, 1.85)), 0.06, 0.0, 0.2, "Kupfer", n=8)                         # Ofenrohr
    bs.achteck(netz, st, v(M, (0.0, y, 2.05)), 0.09, 0.0, 0.04, "Kupfer", n=8)
    # Kohlenschacht mit Trichter rechts
    q(netz, st, M, (0.5, y - 0.12, 1.1), (0.22, 0.24, 0.66), "Holz alt")
    q(netz, st, M, (0.5, y - 0.12, 1.5), (0.4, 0.38, 0.12), "Holz alt", ohne=("+z", "-z"))
    netz.vieleck_nach([v(M, (0.3, y - 0.31, 1.56)), v(M, (0.7, y - 0.31, 1.56)), v(M, (0.7, y + 0.07, 1.56)), v(M, (0.3, y + 0.07, 1.56))],
                      (0, 0, 1), st["Kohle"])
    balken(netz, st, M, (0.38, y + 0.0, 0.8), (0.34, y + 0.15, 0.8), 0.05, 0.05, "Eisen")           # Schieber
    # Kolben im Sandbad auf der Deckplatte vorn, Zangen und Kohleeimer
    bs.achteck(netz, st, v(M, (-0.28, y + 0.22, 0.81)), 0.12, 0.0, 0.07, "Eisen", n=8)
    bs.rund_oben(netz, st, v(M, (-0.28, y + 0.22, 0.81)), 0.1, 0.07, "Formsand", n=8)
    bs.achteck(netz, st, v(M, (-0.28, y + 0.22, 0.87)), 0.07, 0.0, 0.1, "Elixier", n=8)
    bs.achteck(netz, st, v(M, (-0.28, y + 0.22, 0.97)), 0.025, 0.0, 0.14, "Elixier", n=6)
    bs.achteck(netz, st, v(M, (-0.72, 0.38, 0.0)), 0.16, 0.0, 0.3, "Holz alt", n=8)
    bs.rund_oben(netz, st, v(M, (-0.72, 0.38, 0.0)), 0.14, 0.29, "Kohle", n=8)


def enfleurage(netz, st, M):
    """Enfleurage-Kammer des Parfumeurs: ein hoher Schrank aus Holz mit Glastüren, drin die Rahmen mit Glasplatten und Fett, die die Blüten
    aufnehmen; drei Rahmen sind herausgezogen (Rosenblätter auf dem Fett), davor ein Tisch mit Pomadentiegeln und Spatel. Etwa 1,7 m breit,
    0,9 m tief, 2,0 m hoch."""
    b, t, h = 1.45, 0.62, 1.95
    for x in (-b / 2, b / 2):                                                                     # Seiten und Pfosten
        q(netz, st, M, (x, t / 2, h / 2), (0.06, t, h), "Holz", ohne=("-y",))
    q(netz, st, M, (0.0, 0.015, h / 2), (b, 0.03, h), "Holz alt", ohne=("-y",))
    q(netz, st, M, (0.0, t / 2, h + 0.02), (b + 0.1, t + 0.06, 0.05), "Fachwerk")                    # Gesims
    q(netz, st, M, (0.0, t / 2, 0.06), (b, t, 0.1), "Holz", ohne=("-y",))
    q(netz, st, M, (0.0, t - 0.01, 0.12), (b, 0.03, 0.12), "Fachwerk")                               # Sockelleiste
    rahmenzahl = 7
    z0, dz = 0.3, 0.23
    rnd = random.Random(11)
    for k in range(rahmenzahl):
        z = z0 + dz * k
        q(netz, st, M, (0.0, t / 2, z - 0.035), (b - 0.05, t - 0.04, 0.025), "Holz alt", ohne=("-y",))     # Leiste (Auflager)
        raus = 0.0
        if k in (1, 3, 5):                                                                          # herausgezogen
            raus = 0.38
        w = b - 0.2
        y = t / 2 + raus - 0.02
        q(netz, st, M, (0.0, y, z), (w, 0.5, 0.03), "Holz")                                           # Holzrahmen
        q(netz, st, M, (0.0, y, z + 0.0155), (w - 0.07, 0.43, 0.004), "Fett" if k % 2 else "Putz")      # Glasplatte mit Fett
        if raus:
            for j in range(26):
                px = rnd.uniform(-w / 2 + 0.07, w / 2 - 0.07)
                py = y + rnd.uniform(-0.19, 0.19)
                q(netz, st, M, (px, py, z + 0.024), (0.04, 0.035, 0.008), "Rose" if j % 4 else "Heu")
            q(netz, st, M, (0.0, y + 0.25, z + 0.015), (0.16, 0.025, 0.035), "Eisen")                # Griff
    for x in (-0.36, 0.36):                                                                         # Glastüren zu, nur oben und unten
        q(netz, st, M, (x, t + 0.0, 1.78), (0.68, 0.02, 0.3), "Glas")
        q(netz, st, M, (x, t + 0.012, 1.78), (0.72, 0.012, 0.34), "Fachwerk", ohne=("+y",))
    # Tisch davor mit Tiegeln, Spatel, Schale mit Blüten
    tx = 1.18
    q(netz, st, M, (tx, 0.42, 0.78), (0.6, 0.5, 0.05), "Holz")
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        balken(netz, st, M, (tx + dx * 0.25, 0.42 + dy * 0.2, 0.0), (tx + dx * 0.25, 0.42 + dy * 0.2, 0.76), 0.05, 0.05, "Holz")
    for k, farbe in enumerate(("Ton", "Putz", "Ton", "Zinn")):
        bs.achteck(netz, st, v(M, (tx - 0.2 + 0.13 * k, 0.5, 0.805)), 0.045, 0.0, 0.05, farbe, n=8)
    balken(netz, st, M, (tx - 0.2, 0.28, 0.83), (tx + 0.1, 0.34, 0.83), 0.02, 0.01, "Eisen")
    bs.achteck(netz, st, v(M, (tx + 0.18, 0.32, 0.805)), 0.1, 0.0, 0.04, "Holz alt", n=10)
    bs.rund_oben(netz, st, v(M, (tx + 0.18, 0.32, 0.805)), 0.085, 0.04, "Rose", n=10)


def gewoelbekeller(netz, st, M, fass_davor=True):
    """Gewölbekeller des Wirts: ein gemauerter Tonnengewölbe-Anbau an der Wand (Ziegel, vorn ein Rundbogen aus Sandstein mit Schlussstein und Gewänden,
    innen das Gewölbe), darin Fässer liegend auf Lagerhölzern (zwei Reihen, ein Fass oben auf), eine Laterne am Bogen, ein Fass und ein Fässchen davor.
    Etwa 2,3 m breit, 1,4 m tief, 2,4 m hoch."""
    r = 0.8                  # Halbkreis des Gewölbes (innen)
    dick = 0.3               # Mauerdicke
    R = r + dick
    hy = 1.25                # Höhe, ab der der Bogen beginnt
    D = 1.4                  # Tiefe vom Wand bis zum Bogen vorn
    n = 12
    Mn = M.to_3x3()

    def bogen(rr, a):
        return (rr * math.cos(a), hy + rr * math.sin(a))

    def flaeche(punkte, normale, stoff):
        netz.vieleck_nach([v(M, p) for p in punkte], Mn @ Vector(normale), st[stoff])

    for k in range(n):                                                                                   # Gewölbe außen und innen
        a0, a1 = math.pi * k / n, math.pi * (k + 1) / n
        am = (a0 + a1) / 2
        (xo0, zo0), (xo1, zo1) = bogen(R, a0), bogen(R, a1)
        (xi0, zi0), (xi1, zi1) = bogen(r, a0), bogen(r, a1)
        flaeche([(xo0, 0.0, zo0), (xo1, 0.0, zo1), (xo1, D, zo1), (xo0, D, zo0)], (math.cos(am), 0, math.sin(am)), "Ziegelstein")
        flaeche([(xi0, 0.0, zi0), (xi1, 0.0, zi1), (xi1, D, zi1), (xi0, D, zi0)], (-math.cos(am), 0, -math.sin(am)), "Ziegelstein")
        # Vorderseite: Keilstein des Bogens (zwischen innerem und äußerem Bogen), im Wechsel hell und dunkel
        stein = "Sandstein" if k % 2 == 0 else "Haustein"
        flaeche([(xi0, D, zi0), (xi1, D, zi1), (xo1 * 1.0, D, zo1), (xo0, D, zo0)], (0, 1, 0), stein)
        # Laibung (Gewölbe-Innenkante) ist die Innenfläche oben
    for s in (-1, 1):                                                                                    # Seiten: außen, innen, Vorderseite (Gewände)
        flaeche([(s * R, 0.0, 0.0), (s * R, D, 0.0), (s * R, D, hy), (s * R, 0.0, hy)], (s, 0, 0), "Ziegelstein")
        flaeche([(s * r, 0.0, 0.0), (s * r, D, 0.0), (s * r, D, hy), (s * r, 0.0, hy)], (-s, 0, 0), "Ziegelstein")
        for z0, z1, stein in ((0.0, 0.42, "Sandstein"), (0.42, 0.84, "Haustein"), (0.84, hy, "Sandstein")):
            flaeche([(s * r, D, z0), (s * R, D, z0), (s * R, D, z1), (s * r, D, z1)], (0, 1, 0), stein)
    flaeche([(-r, 0.0, 0.0), (r, 0.0, 0.0)] + [(bogen(r, math.pi * k / n)[0], 0.0, bogen(r, math.pi * k / n)[1]) for k in range(n + 1)],
            (0, 1, 0), "Bruchstein dunkel")                                                               # Rückwand (die Mauer)
    flaeche([(-r, 0.0, 0.015), (r, 0.0, 0.015), (r, D, 0.015), (-r, D, 0.015)], (0, 0, 1), "Bruchstein dunkel")   # Boden
    # Schlussstein und Sockelsteine
    q(netz, st, M, (0.0, D + 0.03, hy + R + 0.02), (0.3, 0.07, 0.3), "Sandstein")
    q(netz, st, M, (0.0, D + 0.06, hy + R + 0.02), (0.1, 0.03, 0.14), "Gold")
    # Fässer im Gewölbe: liegend, Achse entlang y, zwei Reihen hintereinander und eines oben auf
    for x, y0, z0, rr in ((-0.36, 0.4, 0.33, 0.32), (0.36, 0.4, 0.33, 0.32), (-0.36, 1.0, 0.33, 0.32), (0.36, 1.0, 0.33, 0.32), (0.0, 0.7, 0.82, 0.29)):
        T = M @ Matrix.Translation((x, y0, z0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
        profil = [(rr * math.cos(2 * math.pi * k / 10 + 0.31), rr * math.sin(2 * math.pi * k / 10 + 0.31)) for k in range(10)]
        netz.prisma(profil, -0.29, 0.29, st["Holz"], matrix=T, m_enden=st["Holz alt"])
        profil2 = [((rr + 0.015) * math.cos(2 * math.pi * k / 10 + 0.31), (rr + 0.015) * math.sin(2 * math.pi * k / 10 + 0.31)) for k in range(10)]
        for dy in (-0.2, 0.2):
            netz.prisma(profil2, dy - 0.03, dy + 0.03, st["Eisen"], matrix=T)
    for x in (-0.36, 0.36):                                                                              # Lagerhölzer
        q(netz, st, M, (x, 0.7, 0.04), (0.5, 1.4, 0.07), "Holz alt")
    # Laterne am rechten Gewände, Fass und Fässchen davor
    bs.achteck(netz, st, v(M, (R + 0.22, D, 1.7)), 0.06, 0.0, 0.16, "Eisen", n=6)
    bs.achteck(netz, st, v(M, (R + 0.22, D, 1.72)), 0.04, 0.0, 0.12, "Ocker", n=6)
    balken(netz, st, M, (R + 0.22, D - 0.2, 1.9), (R + 0.22, D, 1.85), 0.02, 0.02, "Eisen")
    if fass_davor:
        bs.fass(netz, st, v(M, (-R - 0.4, D - 0.1, 0.0)), r=0.34, h=0.85, reifen=(0.12, 0.62))
        bs.fass(netz, st, v(M, (-R - 0.3, D + 0.55, 0.0)), r=0.26, h=0.65, reifen=(0.1, 0.48))


def skriptorium(netz, st, M):
    """Skriptorium des Predigers: ein Schreibpult mit zwei schrägen Flächen für zwei Schreiber (je Pergament, Tintenhorn, Feder), ein hoher
    Kerzenständer, ein Farbtisch-Brett mit Lapislazuli und Blattgold an der Wand, Hocker, Buchstapel auf dem Boden. Etwa 2,6 m breit, 1,2 m tief, 1,6 m hoch."""
    y = 0.7
    for x in (-0.82, 0.82):
        for sy in (-1, 1):
            balken(netz, st, M, (x, y + sy * 0.3, 0.0), (x, y + sy * 0.25, 0.92), 0.08, 0.08, "Holz")
        q(netz, st, M, (x, y, 0.2), (0.07, 0.6, 0.06), "Holz")
    q(netz, st, M, (0.0, y, 0.22), (1.65, 0.07, 0.05), "Holz")
    q(netz, st, M, (0.0, y, 0.32), (1.6, 0.5, 0.03), "Holz alt")                                       # Ablage mit Büchern
    for k, f in enumerate(("Buch rot", "Buch braun", "Buch blau", "Buch grün")):
        q(netz, st, M, (-0.6 + 0.3 * k, y + 0.02, 0.37 + 0.02 * (k % 2)), (0.24, 0.3, 0.06 + 0.02 * (k % 2)), f)
    neig = math.radians(26)
    for hdx in (-1, 1):                                                                              # zwei schräge Schreibflächen, die Schreiber sitzen vorn
        P = M @ Matrix.Translation((hdx * 0.42, y, 1.0)) @ Matrix.Rotation(-neig, 4, "X")
        netz.quader((0, 0, 0), (0.8, 0.6, 0.04), st["Holz"], P)
        netz.quader((0, 0.28, 0.04), (0.8, 0.04, 0.05), st["Holz"], P)                                # Leiste unten (hält das Blatt)
        netz.quader((0, -0.03, 0.026), (0.5, 0.36, 0.008), st["Pergament"], P)                         # Pergament
        for j in range(6):
            netz.quader((-0.02, -0.15 + 0.045 * j, 0.032), (0.42 - 0.03 * (j % 3), 0.01, 0.002), st["Kohle"], P)
        netz.quader((0.3, -0.19, 0.03), (0.07, 0.07, 0.02), st["Ton"], P)                              # Tintenhorn/-fass
        netz.quader((-0.32, -0.2, 0.03), (0.1, 0.05, 0.015), st["Lapis"], P)
    q(netz, st, M, (0.0, y - 0.28, 1.17), (0.07, 0.04, 0.3), "Holz alt")                              # Mittelsteg
    for dx in (-0.5, 0.5):                                                                            # Hocker
        hocker(netz, st, M, dx, y + 0.78, 0.46, 0.17)
    # Kerzenständer (hoch, Eisen) mit brennender Kerze
    bs.achteck(netz, st, v(M, (1.05, 0.55, 0.0)), 0.12, 0.0, 0.03, "Eisen", n=8)
    balken(netz, st, M, (1.05, 0.55, 0.0), (1.05, 0.55, 1.3), 0.03, 0.03, "Eisen")
    bs.achteck(netz, st, v(M, (1.05, 0.55, 1.3)), 0.07, 0.0, 0.02, "Eisen", n=8)
    bs.achteck(netz, st, v(M, (1.05, 0.55, 1.32)), 0.022, 0.0, 0.14, "Stadtweiss", n=6)
    bs.achteck(netz, st, v(M, (1.05, 0.55, 1.46)), 0.012, 0.0, 0.05, "Glut", n=6)
    # Farbbrett an der Wand
    q(netz, st, M, (-1.15, 0.1, 0.95), (0.6, 0.18, 0.04), "Holz", ohne=("-y",))
    for dx in (-0.25, 0.25):
        q(netz, st, M, (-1.15 + dx, 0.07, 0.86), (0.04, 0.12, 0.18), "Holz", ohne=("-y",))
    for k, farbe in enumerate(("Lapis", "Rose", "Gold", "Kraut", "Lapis")):
        bs.achteck(netz, st, v(M, (-1.37 + 0.11 * k, 0.12, 0.97)), 0.04, 0.0, 0.05, farbe, n=6)
    for k in range(4):                                                                                # Pergamentrollen, im Regal darüber
        T = M @ Matrix.Translation((-1.3 + 0.12 * k, 0.12, 1.45)) @ Matrix.Rotation(math.radians(90), 4, "Z")
        netz.prisma([(0.03 * math.cos(2 * math.pi * j / 6), 0.03 * math.sin(2 * math.pi * j / 6)) for j in range(6)], -0.25, 0.25, st["Pergament"], matrix=T)
    q(netz, st, M, (-1.15, 0.1, 1.38), (0.6, 0.18, 0.03), "Holz", ohne=("-y",))
    for k in range(3):                                                                                # Bücher auf dem Boden
        q(netz, st, M, (-0.95, 0.45, 0.04 + 0.08 * k), (0.34 - 0.03 * k, 0.24, 0.08), ("Buch braun", "Buch rot", "Buch grün")[k])


# ═════════════════════════════════════════════════════════════════════════════
#  Alle Betriebe
# ═════════════════════════════════════════════════════════════════════════════

def ladentheke(netz, st, M, ware="Schmied", breite=2.3):
    """Ladentheke: Verkaufstresen aus Brettern mit Rahmen und Deckplatte, Klappe am rechten Ende, darauf Waage, Hauptbuch und Rechenbrett, dahinter ein
    Auslageregal. `ware` bestimmt, was auf der Theke liegt (Schmied, Tischler, Steinmetz, Alchimist, Parfumeur, Wirt, Prediger). Etwa 2,3 m breit,
    0,8 m tief, 1,05 m hoch (mit Auslage 1,7 m)."""
    t = 0.7
    q(netz, st, M, (0.0, t / 2, 0.45), (breite, t - 0.1, 0.9), "Holz alt")
    for k in range(5):                                                                                # Füllungen mit Rahmen an der Vorderseite
        x = -breite / 2 + (k + 0.5) * breite / 5
        q(netz, st, M, (x, t - 0.04, 0.47), (breite / 5 - 0.08, 0.04, 0.72), "Holz")
        q(netz, st, M, (x, t - 0.065, 0.47), (breite / 5 - 0.16, 0.012, 0.58), "Holz alt")
    for x in (-breite / 2 + 0.03, breite / 2 - 0.03):
        q(netz, st, M, (x, t - 0.04, 0.47), (0.07, 0.05, 0.86), "Fachwerk")
    q(netz, st, M, (0.0, t - 0.04, 0.05), (breite, 0.06, 0.1), "Fachwerk")
    q(netz, st, M, (0.0, t / 2 + 0.04, 0.93), (breite + 0.1, t + 0.12, 0.07), "Schnittholz")          # Deckplatte
    q(netz, st, M, (0.0, t + 0.08, 0.93), (breite + 0.1, 0.03, 0.09), "Holz")                          # Kante vorn
    # Waage rechts, Hauptbuch und Rechenbrett links, Ware dazwischen
    wx = breite / 2 - 0.45
    balken(netz, st, M, (wx, 0.3, 0.97), (wx, 0.3, 1.38), 0.035, 0.035, "Eisen")
    balken(netz, st, M, (wx - 0.26, 0.3, 1.36), (wx + 0.26, 0.3, 1.36), 0.02, 0.02, "Eisen", quer=(0, 1, 0))
    for s in (-1, 1):
        bs.achteck(netz, st, v(M, (wx + s * 0.26, 0.3, 1.1)), 0.1, 0.0, 0.02, "Zinn", n=8)
        for dx in (-0.06, 0.06):
            balken(netz, st, M, (wx + s * 0.26, 0.3, 1.36), (wx + s * 0.26 + dx, 0.3, 1.12), 0.004, 0.004, "Seil")
    q(netz, st, M, (wx, 0.3, 0.97), (0.14, 0.14, 0.03), "Eisen")
    buch(netz, st, M, (-breite / 2 + 0.5, 0.42, 0.99), (0.34, 0.26, 0.04), "Buch braun", dreh=-8)
    q(netz, st, M, (-breite / 2 + 0.95, 0.46, 0.975), (0.3, 0.18, 0.02), "Holz")                      # Rechenbrett
    for j in range(4):
        q(netz, st, M, (-breite / 2 + 0.95, 0.4 + 0.045 * j, 0.992), (0.24, 0.004, 0.008), "Fachwerk")
        for i in range(3):
            q(netz, st, M, (-breite / 2 + 0.86 + 0.06 * i + 0.01 * j, 0.4 + 0.045 * j, 1.0), (0.025, 0.02, 0.02), "Gold")
    ladenware(netz, st, M, ware, wx - 0.65, breite)
    # Auslageregal an der Wand dahinter
    q(netz, st, M, (0.0, 0.03, 1.45), (breite - 0.3, 0.04, 0.5), "Holz alt", ohne=("-y",))
    for z in (1.2, 1.72):
        q(netz, st, M, (0.0, 0.14, z), (breite - 0.3, 0.2, 0.035), "Holz", ohne=("-y",))
    for s in (-1, 1):
        q(netz, st, M, (s * (breite / 2 - 0.15), 0.12, 1.46), (0.035, 0.18, 0.55), "Holz", ohne=("-y",))
    ladenware(netz, st, M, ware, 0.0, breite - 0.4, z=1.22, y=0.14, regal=True)


def ladenware(netz, st, M, ware, x, breite, z=0.96, y=0.38, regal=False):
    """Kleine Auslage nach dem Beruf: Reihen von Dolchen (Schmied), Holzfiguren (Tischler), Steinfiguren (Steinmetz), Flaschen (Alchimist),
    Flakons (Parfumeur), Krüge (Wirt), Kerzen und Bücher (Prediger)."""
    n = 4 if not regal else 7
    schritt = 0.2 if not regal else (breite - 0.3) / 7
    x0 = x - (n - 1) * schritt / 2 if not regal else -(n - 1) * schritt / 2
    for k in range(n):
        px = x0 + k * schritt
        if ware == "Schmied":
            q(netz, st, M, (px, y, z + 0.005), (0.02, 0.3, 0.012), "Silber")
            q(netz, st, M, (px, y - 0.18, z + 0.008), (0.05, 0.08, 0.018), "Leder")
        elif ware == "Tischler":
            bs.achteck(netz, st, v(M, (px, y, z)), 0.045, 0.0, 0.14 + 0.03 * (k % 3), "Schnittholz", n=6)
            bs.achteck(netz, st, v(M, (px, y, z + 0.14 + 0.03 * (k % 3))), 0.03, 0.0, 0.05, "Schnittholz", n=6)
        elif ware == "Steinmetz":
            q(netz, st, M, (px, y, z + 0.08), (0.09, 0.07, 0.16), "Marmor")
            bs.achteck(netz, st, v(M, (px, y, z + 0.16)), 0.04, 0.0, 0.07, "Marmor", n=6)
        elif ware == "Alchimist":
            bs.achteck(netz, st, v(M, (px, y, z)), 0.045, 0.0, 0.11, ("Elixier", "Farbe", "Kraut", "Violett")[k % 4], n=6)
            bs.achteck(netz, st, v(M, (px, y, z + 0.11)), 0.018, 0.0, 0.05, ("Elixier", "Farbe", "Kraut", "Violett")[k % 4], n=6)
        elif ware == "Parfumeur":
            bs.achteck(netz, st, v(M, (px, y, z)), 0.04, 0.0, 0.08, ("Glas", "Rose", "Gold", "Elixier")[k % 4], n=6)
            bs.achteck(netz, st, v(M, (px, y, z + 0.08)), 0.015, 0.0, 0.05, "Gold", n=6)
        elif ware == "Wirt":
            bs.achteck(netz, st, v(M, (px, y, z)), 0.05, 0.0, 0.13, "Ton" if k % 2 else "Zinn", n=8)
        else:
            bs.achteck(netz, st, v(M, (px, y, z)), 0.025, 0.0, 0.12 + 0.02 * (k % 2), "Stadtweiss", n=6)
            if k % 2:
                q(netz, st, M, (px + 0.08, y + 0.02, z + 0.03), (0.14, 0.1, 0.05), ("Buch rot", "Buch blau")[(k // 2) % 2])


def lehrlingsstube(netz, st, M, variante="Werkstatt", bett=True):
    """Lehrlingsstube: ein schlichtes Doppelstockbett aus rohen Balken mit Strohsäcken und groben Decken, eine kleine Truhe am Fußende, ein Hocker, an der
    Wand eine Schiefertafel mit Kreide und ein Haken mit dem Kittel. `bett=False`: nur Zusatz (Truhe, Hocker, Tafel; für Räume, die schon Pritschen haben). `variante` setzt eine Kleinigkeit (Dieb: Übungsschloss, Wache: Übungsstock und
    Helm, Bank: Rechenbrett, sonst Hobel/Hammer). Etwa 2,2 m breit, 1,0 m tief, 1,85 m hoch."""
    L, B = 1.9, 0.82
    x0 = -1.05
    for etage, z in enumerate((0.42, 1.2) if bett else ()):
        for sy in (0, 1):                                                                           # Wangen
            q(netz, st, M, (x0 + L / 2, 0.06 + sy * (B - 0.08), z), (L, 0.06, 0.14), "Holz")
        for sx in (0, 1):
            q(netz, st, M, (x0 + 0.03 + sx * (L - 0.06), B / 2, z), (0.06, B - 0.2, 0.14), "Holz")
        q(netz, st, M, (x0 + L / 2, B / 2, z + 0.05), (L - 0.08, B - 0.1, 0.02), "Holz alt")
        q(netz, st, M, (x0 + L / 2 - 0.05, B / 2, z + 0.13), (L - 0.2, B - 0.14, 0.16), "Strohsack", ohne=("-z",))
        q(netz, st, M, (x0 + 0.3, B / 2, z + 0.26), (0.38, 0.5, 0.08), "Leinen")                      # Kissen
        decke = "Decke rot" if etage == 0 else "Decke blau"
        q(netz, st, M, (x0 + L - 0.55, B / 2, z + 0.225), (0.95, B - 0.1, 0.03), decke)
        q(netz, st, M, (x0 + L - 0.06, B / 2, z + 0.1), (0.03, B - 0.1, 0.22), decke)                   # hängt am Fußende herab
    for sx in ((x0 + 0.03, x0 + L - 0.03) if bett else ()):                                           # Pfosten
        for sy in (0.06, B - 0.06):
            balken(netz, st, M, (sx, sy, 0.0), (sx, sy, 1.85), 0.08, 0.08, "Holz")
    for sy in ((0.28, 0.54) if bett else ()):
        balken(netz, st, M, (x0 + L + 0.08, sy, 0.0), (x0 + L + 0.08, sy, 1.15), 0.04, 0.04, "Holz")
    for k in range(4 if bett else 0):
        q(netz, st, M, (x0 + L + 0.08, 0.41, 0.25 + 0.27 * k), (0.04, 0.3, 0.035), "Holz")
    # Truhe am Fuß, Hocker, Tafel, Haken
    q(netz, st, M, (x0 + L + 0.55, 0.3, 0.2), (0.6, 0.42, 0.4), "Holz alt")
    q(netz, st, M, (x0 + L + 0.55, 0.3, 0.405), (0.64, 0.46, 0.04), "Holz")
    for s in (-1, 1):
        q(netz, st, M, (x0 + L + 0.55 + s * 0.2, 0.52, 0.2), (0.05, 0.02, 0.4), "Eisen")
    hocker(netz, st, M, x0 + 0.55 if bett else x0 + L + 0.55, B + 0.35 if bett else 0.85, 0.38, 0.15)
    q(netz, st, M, (x0 + L + 0.55, 0.035, 1.35), (0.5, 0.03, 0.4), "Holz", ohne=("-y",))               # Tafel
    q(netz, st, M, (x0 + L + 0.55, 0.055, 1.35), (0.42, 0.012, 0.32), "Schiefer tafel", ohne=("-y",))
    for j in range(3):
        q(netz, st, M, (x0 + L + 0.5, 0.062, 1.43 - 0.08 * j), (0.28 - 0.06 * j, 0.004, 0.012), "Stadtweiss", ohne=("-y",))
    q(netz, st, M, (x0 + L + 0.55, 0.03, 1.74), (0.5, 0.04, 0.03), "Holz", ohne=("-y",))
    for s in (-0.12, 0.12):
        balken(netz, st, M, (x0 + L + 0.55 + s, 0.06, 1.74), (x0 + L + 0.55 + s, 0.12, 1.6), 0.02, 0.02, "Eisen")
    q(netz, st, M, (x0 + L + 0.55 + 0.12, 0.1, 1.5), (0.22, 0.04, 0.3), "Lumpen")                     # Kittel am Haken
    if variante == "Dieb":
        q(netz, st, M, (x0 + L + 1.0, 0.04, 1.2), (0.3, 0.03, 0.22), "Holz alt", ohne=("-y",))        # Übungsschloss
        q(netz, st, M, (x0 + L + 1.0, 0.065, 1.2), (0.12, 0.03, 0.14), "Eisen", ohne=("-y",))
    elif variante == "Wache":
        balken(netz, st, M, (x0 + L + 1.0, 0.1, 0.0), (x0 + L + 1.0, 0.1, 1.3), 0.04, 0.04, "Holz")      # Übungsstock
        bs.achteck(netz, st, v(M, (x0 + L + 1.2, 0.3, 0.4)), 0.12, 0.0, 0.1, "Eisen", n=8)                # Helm auf der Truhe
    elif variante == "Bank":
        q(netz, st, M, (x0 + L + 1.0, 0.04, 1.2), (0.3, 0.03, 0.2), "Holz", ohne=("-y",))


# ═════════════════════════════════════════════════════════════════════════════
#  Wohnhaus (Wohnstube)
# ═════════════════════════════════════════════════════════════════════════════

def vorratskammer(netz, st, M):
    """Vorratskammer: zwei hohe Regale mit Töpfen, Säcken, Käselaiben und Krügen, darüber ein Balken mit Schinken und Würsten, ein Fässchen und ein Korb.
    Etwa 2,0 m breit, 0,6 m tief, 2,3 m hoch (an der Wand, im Haus mit Vorhang- oder Bretterwand abgetrennt gedacht)."""
    rnd = random.Random(3)
    for ix, x in enumerate((-0.55, 0.55)):
        b, tiefe, h = 0.95, 0.5, 1.9
        for dx in (-b / 2, b / 2):
            q(netz, st, M, (x + dx, tiefe / 2, h / 2), (0.05, tiefe, h), "Holz", ohne=("-y",))
        q(netz, st, M, (x, 0.01, h / 2), (b, 0.02, h), "Holz alt", ohne=("-y",))
        for k in range(5):
            z = 0.05 + 0.44 * k
            q(netz, st, M, (x, tiefe / 2, z), (b - 0.05, tiefe, 0.035), "Holz", ohne=("-y",))
            if k == 4:
                break
            px = x - b / 2 + 0.12
            while px < x + b / 2 - 0.12:
                art = rnd.choice(("topf", "sack", "kaese", "krug", "topf"))
                if art == "topf":
                    bs.achteck(netz, st, v(M, (px, tiefe * 0.5, z + 0.0175)), 0.09, 0.0, 0.16, "Ton", n=8)
                    bs.achteck(netz, st, v(M, (px, tiefe * 0.5, z + 0.1775)), 0.07, 0.0, 0.02, "Holz alt", n=8)
                    px += 0.24
                elif art == "sack":
                    bs.achteck(netz, st, v(M, (px, tiefe * 0.5, z + 0.0175)), 0.11, 0.0, 0.3, "Leinen", n=6)
                    bs.achteck(netz, st, v(M, (px, tiefe * 0.5, z + 0.3175)), 0.045, 0.0, 0.05, "Seil", n=6)
                    px += 0.27
                elif art == "kaese":
                    bs.achteck(netz, st, v(M, (px, tiefe * 0.5, z + 0.0175)), 0.13, 0.0, 0.1, "Heu", n=10)
                    px += 0.3
                else:
                    krug(netz, st, M, px, tiefe * 0.5, z + 0.0175, 0.05, 0.22, "Ton")
                    px += 0.16
    # Balken mit Schinken und Würsten
    balken(netz, st, M, (-1.0, 0.3, 2.15), (1.0, 0.3, 2.15), 0.1, 0.1, "Holz", quer=(0, 1, 0))
    for x in (-0.6, -0.15, 0.4, 0.85):
        balken(netz, st, M, (x, 0.3, 2.15), (x, 0.3, 1.95), 0.012, 0.012, "Seil")
    for x, f in ((-0.6, "Rose"), (-0.15, "Leder"), (0.4, "Rose")):
        bs.achteck(netz, st, v(M, (x, 0.3, 1.62)), 0.09, 0.0, 0.34, f, n=6)
    for k in range(5):
        bs.achteck(netz, st, v(M, (0.78 + 0.04 * k - 0.08, 0.3, 1.7)), 0.02, 0.0, 0.28, "Leder", n=6)
    bs.fass(netz, st, v(M, (1.25, 0.35, 0.0)), r=0.24, h=0.55, reifen=(0.1, 0.4))
    bs.achteck(netz, st, v(M, (-1.2, 0.35, 0.0)), 0.2, 0.0, 0.3, "Stroh", n=10)                        # Korb


def kueche(netz, st, M):
    """Küche: Arbeitstisch mit Hackklotz, Schneidbrett und Brotlaib, ein Wandbrett mit Kupfertöpfen, Pfannen und Kellen an Haken, ein Gewürzregal
    mit Tiegeln, ein Wassereimer und eine Bank. Etwa 2,2 m breit, 0,8 m tief, 1,9 m hoch (neben dem Herd der Wohnstube)."""
    q(netz, st, M, (0.0, 0.45, 0.88), (1.5, 0.7, 0.06), "Schnittholz")
    for dx in (-0.68, 0.68):
        for dy in (0.2, 0.7):
            balken(netz, st, M, (dx, dy, 0.0), (dx, dy, 0.85), 0.07, 0.07, "Holz")
    q(netz, st, M, (0.0, 0.45, 0.2), (1.35, 0.55, 0.03), "Holz alt")
    for k in range(3):
        krug(netz, st, M, -0.5 + 0.2 * k, 0.5, 0.215, 0.07, 0.2, "Ton")
    bs.achteck(netz, st, v(M, (-0.35, 0.4, 0.91)), 0.16, 0.0, 0.2, "Holz alt", n=8)                    # Hackklotz
    q(netz, st, M, (0.1, 0.4, 0.925), (0.4, 0.26, 0.025), "Holz")                                      # Brett
    bs.achteck(netz, st, v(M, (0.12, 0.4, 0.94)), 0.1, 0.0, 0.06, "Heu", n=8)                          # Brotlaib
    balken(netz, st, M, (0.28, 0.45, 0.96), (0.5, 0.5, 0.96), 0.02, 0.012, "Eisen")                    # Messer
    bs.achteck(netz, st, v(M, (0.55, 0.42, 0.91)), 0.09, 0.0, 0.14, "Kupfer", n=10)                    # Kessel
    # Wandbrett mit Haken: Töpfe und Pfannen
    q(netz, st, M, (0.0, 0.03, 1.55), (1.9, 0.04, 0.06), "Holz", ohne=("-y",))
    for k, x in enumerate((-0.8, -0.5, -0.2, 0.15, 0.5, 0.8)):
        balken(netz, st, M, (x, 0.06, 1.55), (x, 0.06, 1.4), 0.012, 0.012, "Eisen")
        if k % 3 == 0:
            bs.achteck(netz, st, v(M, (x, 0.1, 1.15)), 0.09, 0.0, 0.2, "Kupfer", n=8)
        elif k % 3 == 1:
            walze(netz, st, M, (x, 1.25), 0.11, 0.05, 0.08, "Eisen", n=10)                              # Pfanne (Scheibe, Achse y)
            balken(netz, st, M, (x, 0.07, 1.14), (x, 0.07, 0.95), 0.025, 0.012, "Eisen")
        else:
            balken(netz, st, M, (x, 0.07, 1.4), (x, 0.07, 1.0), 0.02, 0.012, "Eisen")                    # Kelle
            bs.achteck(netz, st, v(M, (x, 0.07, 0.96)), 0.05, 0.0, 0.04, "Eisen", n=8)
    # Gewürzbrett
    q(netz, st, M, (-1.35, 0.1, 1.2), (0.55, 0.18, 0.03), "Holz", ohne=("-y",))
    for k in range(4):
        bs.achteck(netz, st, v(M, (-1.55 + 0.1 * k, 0.12, 1.215)), 0.035, 0.0, 0.06, ("Ton", "Zinn", "Ton", "Heu")[k], n=6)
    for dx in (-0.2, 0.2):
        q(netz, st, M, (-1.35 + dx, 0.08, 1.1), (0.04, 0.12, 0.15), "Holz", ohne=("-y",))
    bs.achteck(netz, st, v(M, (1.2, 0.35, 0.0)), 0.16, 0.0, 0.32, "Holz alt", n=8)                    # Wassereimer
    bs.rund_oben(netz, st, v(M, (1.2, 0.35, 0.0)), 0.14, 0.31, "Wasser", n=8)
    hocker(netz, st, M, 0.95, 0.9, 0.42, 0.18)


def hauswappen(netz, st, M, hoehe=1.0):
    """Wappen über der Tür: ein geschnitzter Holzschild mit Rahmen und Lorbeer, darin das Wappenschild in der Hausfarbe (das Spiel färbt „Hausfarbe“ je Haus
    um) mit einer goldenen Figur (Sparren und Stern), darüber ein Helm mit Helmzier. Etwa 0,9 m breit, 0,1 m tief, 1,2 m hoch (hängt an der Wand,
    Mittelpunkt auf `hoehe`)."""
    z = hoehe
    q(netz, st, M, (0.0, 0.03, z), (0.82, 0.05, 1.0), "Fachwerk", ohne=("-y",))                       # Holztafel
    q(netz, st, M, (0.0, 0.06, z), (0.72, 0.03, 0.9), "Holz", ohne=("-y",))
    # Schild: Fünfeck (spanische Form) in der Hausfarbe
    umriss = [(-0.27, 0.16), (0.27, 0.16), (0.27, -0.1), (0.0, -0.4), (-0.27, -0.1)]
    netz.vieleck_nach([v(M, (x, 0.085, z + dz)) for x, dz in umriss], v(M, (0, 1, 0)) - v(M, (0, 0, 0)), st["Hausfarbe"])
    for i, ((xa, za), (xb, zb)) in enumerate(zip(umriss, umriss[1:] + umriss[:1])):                    # goldener Rand (Lagen um Millimeter versetzt: keine deckungsgleichen Flächen)
        balken(netz, st, M, (xa, 0.09 + 0.003 * i, z + za), (xb, 0.09 + 0.003 * i, z + zb), 0.03, 0.02, "Gold", quer=(0, 1, 0))
    balken(netz, st, M, (-0.25, 0.1, z - 0.0), (0.0, 0.1, z + 0.14), 0.05, 0.02, "Gold", quer=(0, 1, 0))      # Sparren
    balken(netz, st, M, (0.25, 0.103, z - 0.0), (0.0, 0.103, z + 0.14), 0.05, 0.02, "Gold", quer=(0, 1, 0))
    for k in range(5):                                                                                 # Stern
        w = math.pi * 2 * k / 5 + math.pi / 2
        balken(netz, st, M, (0.0, 0.1 + 0.003 * k, z - 0.2), (0.07 * math.cos(w), 0.1 + 0.003 * k, z - 0.2 + 0.07 * math.sin(w)), 0.03, 0.02, "Gold", quer=(0, 1, 0))
    # Helm und Helmzier
    q(netz, st, M, (0.0, 0.09, z + 0.28), (0.24, 0.07, 0.17), "Silber")
    q(netz, st, M, (0.0, 0.1, z + 0.28), (0.18, 0.02, 0.02), "Kohle")
    for k in range(4):
        w = math.radians(30 + 40 * k)
        balken(netz, st, M, (0.0, 0.09 + 0.004 * k, z + 0.38), (0.24 * math.cos(w), 0.09 + 0.004 * k, z + 0.38 + 0.18 * math.sin(w)), 0.045, 0.03, "Hausfarbe", quer=(0, 1, 0))
        balken(netz, st, M, (0.0, 0.092 + 0.004 * k, z + 0.38), (-0.24 * math.cos(w), 0.092 + 0.004 * k, z + 0.38 + 0.18 * math.sin(w)), 0.045, 0.03, "Hausfarbe", quer=(0, 1, 0))
    # Lorbeer seitlich
    for s in (-1, 1):
        for k in range(5):
            bs.achteck(netz, st, v(M, (s * (0.36 - 0.015 * k), 0.07, z - 0.35 + 0.15 * k)), 0.04, 0.0, 0.03, "Laub", n=5)


def stube(netz, st, M):
    """Gute Stube: ein gewebter Teppich in Rot und Blau mit Rand, ein geschnitzter Schrank (Kredenz) mit Zinnkannen und Tellern, ein Kachelofen mit grünen
    Kacheln, eine gepolsterte Bank, eine Wandtäfelung aus Holz. Der Teppich liegt auf dem Boden vor `M` (3 × 2 m), die übrigen Stücke an der Wand.
    Etwa 3,6 m breit, 2,2 m tief, 2,1 m hoch."""
    # Teppich mit Rand
    cx, cy, w, t = 0.0, 1.35, 2.8, 1.8
    q(netz, st, M, (cx, cy, 0.006), (w, t, 0.012), "Decke rot", ohne=("-z",))
    q(netz, st, M, (cx, cy, 0.0125), (w - 0.2, t - 0.2, 0.004), "Decke blau", ohne=("-z",))
    q(netz, st, M, (cx, cy, 0.0145), (w - 0.5, t - 0.5, 0.004), "Decke rot", ohne=("-z",))
    for k in range(4):
        for j in range(3):
            q(netz, st, M, (cx - w / 2 + 0.5 + 0.55 * k, cy - t / 2 + 0.45 + 0.45 * j, 0.0165), (0.14, 0.14, 0.004), "Gold", ohne=("-z",))
    for s in (-1, 1):                                                                                  # Fransen
        for k in range(14):
            q(netz, st, M, (cx + s * (w / 2 + 0.03), cy - t / 2 + 0.07 + 0.125 * k, 0.005), (0.06, 0.02, 0.006), "Leinen", ohne=("-z",))
    # Kredenz an der Wand (rechts)
    x = 1.25
    q(netz, st, M, (x, 0.28, 0.45), (1.3, 0.5, 0.9), "Holz")
    for s in (-1, 1):
        q(netz, st, M, (x + s * 0.32, 0.54, 0.45), (0.58, 0.03, 0.76), "Holz alt")
        q(netz, st, M, (x + s * 0.32, 0.56, 0.45), (0.46, 0.012, 0.64), "Holz")
        q(netz, st, M, (x + s * 0.06, 0.575, 0.45), (0.025, 0.02, 0.1), "Eisen")
    q(netz, st, M, (x, 0.3, 0.92), (1.4, 0.56, 0.05), "Schnittholz")
    q(netz, st, M, (x, 0.07, 1.55), (1.3, 0.04, 1.3), "Holz alt", ohne=("-y",))                        # Rückbrett
    for z in (1.15, 1.6):
        q(netz, st, M, (x, 0.17, z), (1.3, 0.22, 0.035), "Holz", ohne=("-y",))
    for k in range(5):
        walze(netz, st, M, (x - 0.5 + 0.25 * k, 1.28), 0.1, 0.15, 0.17, "Zinn", n=12)                  # Teller (stehend)
    for k, hh in enumerate((0.26, 0.2, 0.24)):
        bs.achteck(netz, st, v(M, (x - 0.4 + 0.4 * k, 0.17, 0.935)), 0.06, 0.0, hh, "Zinn", n=8)       # Zinnkannen
    for k in range(3):
        bs.achteck(netz, st, v(M, (x - 0.35 + 0.35 * k, 0.17, 1.17)), 0.045, 0.0, 0.14, ("Ton", "Zinn", "Ton")[k], n=8)
    # Kachelofen links an der Wand
    ox = -1.45
    q(netz, st, M, (ox, 0.45, 0.35), (0.8, 0.7, 0.7), "Glasur grün")
    q(netz, st, M, (ox, 0.45, 0.72), (0.86, 0.76, 0.05), "Bruchstein dunkel")
    q(netz, st, M, (ox, 0.45, 1.25), (0.64, 0.56, 1.0), "Glasur grün")
    for k in range(4):                                                                                 # Kachelfugen als Linien am Oberteil
        q(netz, st, M, (ox, 0.45 + 0.281, 0.9 + 0.2 * k), (0.62, 0.003, 0.012), "Fuge", ohne=("-y",))
    q(netz, st, M, (ox, 0.45, 1.78), (0.7, 0.62, 0.06), "Bruchstein dunkel")
    q(netz, st, M, (ox, 0.45 + 0.405, 0.35), (0.3, 0.02, 0.26), "Eisen")                                # Ofentür
    q(netz, st, M, (ox, 0.45 + 0.415, 0.35), (0.06, 0.02, 0.03), "Gold")
    # gepolsterte Bank unter dem Fenster in der Mitte
    bx = 0.0
    q(netz, st, M, (bx, 0.27, 0.22), (1.4, 0.44, 0.44), "Holz")
    q(netz, st, M, (bx, 0.27, 0.47), (1.38, 0.42, 0.07), "Decke blau")
    q(netz, st, M, (bx, 0.07, 0.8), (1.4, 0.06, 0.7), "Holz", ohne=("-y",))                            # Lehne
    q(netz, st, M, (bx, 0.12, 0.74), (1.3, 0.1, 0.5), "Decke blau")
    for dx in (-0.4, 0.4):
        q(netz, st, M, (bx + dx, 0.22, 0.56), (0.3, 0.26, 0.08), "Decke rot")                           # Kissen
    # Täfelung entlang der Wand hinter dem Schrank (Streifen)


def bibliothek(netz, st, M):
    """Bibliothek: eine Wand voller Bücher (zwei hohe Regale mit Leiter), ein Lesepult mit aufgeschlagenem Buch und Kerze, ein Globus, ein Lesesessel.
    Etwa 2,8 m breit, 1,2 m tief, 2,4 m hoch."""
    regal_buecher(netz, st, M @ Matrix.Translation((-0.8, 0, 0)), 1.2, 2.3, 0.35, 6, saat=1)
    regal_buecher(netz, st, M @ Matrix.Translation((0.5, 0, 0)), 1.2, 2.3, 0.35, 6, saat=2)
    q(netz, st, M, (0.0, 0.21, 2.4), (2.6, 0.45, 0.06), "Fachwerk")                                     # Gesims
    for x in (-1.45, 1.15):                                                                             # Leiter an Schiene
        for sy in (-0.07, 0.07):
            balken(netz, st, M, (x + sy, 0.46, 0.0), (x + sy * 0.6, 0.38, 2.05), 0.045, 0.04, "Holz")
        for k in range(9):
            q(netz, st, M, (x, 0.45 - 0.003 * k, 0.2 + 0.22 * k), (0.2, 0.04, 0.03), "Holz")
    # Lesepult mit Buch und Kerze
    px = 0.0
    for dx in (-0.25, 0.25):
        for dy in (0.62, 0.9):
            balken(netz, st, M, (px + dx, dy, 0.0), (px + dx, dy, 0.9), 0.06, 0.06, "Holz")
    q(netz, st, M, (px, 0.76, 0.5), (0.58, 0.4, 0.03), "Holz alt")
    P = M @ Matrix.Translation((px, 0.76, 1.0)) @ Matrix.Rotation(math.radians(14), 4, "X")
    netz.quader((0, 0, 0), (0.7, 0.5, 0.04), st["Holz"], P)
    netz.quader((0, 0.23, 0.04), (0.7, 0.04, 0.05), st["Holz"], P)
    for s in (-1, 1):
        netz.quader((s * 0.15, 0.0, 0.035), (0.28, 0.38, 0.02), st["Buch rot"], P)
        netz.quader((s * 0.14, 0.0, 0.048), (0.25, 0.35, 0.012), st["Pergament"], P)
    for j in range(5):
        netz.quader((-0.14, -0.14 + 0.06 * j, 0.056), (0.2, 0.01, 0.002), st["Kohle"], P)
    bs.achteck(netz, st, v(M, (0.52, 0.76, 0.0)), 0.07, 0.0, 0.03, "Eisen", n=6)                      # Leuchter
    balken(netz, st, M, (0.52, 0.76, 0.0), (0.52, 0.76, 1.15), 0.03, 0.03, "Eisen")
    bs.achteck(netz, st, v(M, (0.52, 0.76, 1.15)), 0.02, 0.0, 0.14, "Stadtweiss", n=6)
    bs.achteck(netz, st, v(M, (0.52, 0.76, 1.29)), 0.011, 0.0, 0.04, "Glut", n=6)
    # Sessel links
    q(netz, st, M, (-1.0, 0.85, 0.22), (0.7, 0.7, 0.44), "Holz")
    q(netz, st, M, (-1.0, 0.85, 0.48), (0.64, 0.62, 0.1), "Decke rot")
    q(netz, st, M, (-1.0, 1.16, 0.8), (0.7, 0.1, 0.8), "Holz")
    q(netz, st, M, (-1.0, 1.1, 0.8), (0.6, 0.1, 0.66), "Decke rot")


def riegel(netz, st, M, breite=1.05, hoehe=2.1):
    """Eiserne Riegel: eine Tür in Mauerstock aus Holz, mit Eisenbändern und Nieten, zwei eisernen Querriegeln in Haltern, einem Kastenschloss und einem
    Schiebebolzen am Boden; seitlich ein Fensterladen mit Riegel. Etwa 1,05 m breit (Tür) plus 0,7 m (Laden), 0,15 m tief, 2,1 m hoch."""
    # Mauerstock
    for s in (-1, 1):
        q(netz, st, M, (s * (breite / 2 + 0.06), 0.06, hoehe / 2), (0.14, 0.12, hoehe), "Fachwerk")
    q(netz, st, M, (0.0, 0.06, hoehe + 0.06), (breite + 0.4, 0.12, 0.14), "Fachwerk")
    q(netz, st, M, (0.0, 0.04, hoehe / 2), (breite, 0.06, hoehe), "Holz alt")
    for k in range(5):                                                                                 # Bretter (Fugen)
        q(netz, st, M, (-breite / 2 + breite * (k + 1) / 6, 0.075, hoehe / 2), (0.01, 0.01, hoehe - 0.04), "Kohle")
    for z in (0.35, 1.0, 1.7):                                                                          # Bänder mit Nieten
        q(netz, st, M, (0.0, 0.085, z), (breite - 0.04, 0.025, 0.09), "Eisen")
        for k in range(6):
            q(netz, st, M, (-breite / 2 + 0.1 + (breite - 0.2) * k / 5, 0.1, z), (0.03, 0.012, 0.03), "Kohle")
        q(netz, st, M, (-breite / 2 + 0.02, 0.085, z), (0.1, 0.03, 0.14), "Eisen")                     # Angel
    for z in (0.7, 1.35):                                                                               # Querriegel (eiserne Stange) mit Haltern
        balken(netz, st, M, (-breite / 2 - 0.1, 0.17, z), (breite / 2 + 0.1, 0.17, z), 0.05, 0.05, "Eisen", quer=(0, 1, 0))
        for s in (-1, 0, 1):
            q(netz, st, M, (s * (breite / 2 - 0.05), 0.125, z), (0.06, 0.07, 0.12), "Eisen")
        q(netz, st, M, (breite / 2 - 0.15, 0.2, z), (0.06, 0.04, 0.12), "Eisen")                        # Griff
    q(netz, st, M, (0.28, 0.115, 1.0), (0.2, 0.05, 0.14), "Eisen")                                       # Kastenschloss
    q(netz, st, M, (0.28, 0.145, 1.0), (0.05, 0.012, 0.05), "Kohle")
    q(netz, st, M, (-0.2, 0.12, 0.08), (0.18, 0.04, 0.04), "Eisen")                                      # Fußriegel
    # Fensterladen daneben mit Riegel
    lx = breite / 2 + 0.7
    q(netz, st, M, (lx, 0.04, 1.45), (0.62, 0.05, 0.9), "Hausfarbe")
    balken(netz, st, M, (lx - 0.36, 0.1, 1.45), (lx + 0.36, 0.1, 1.45), 0.05, 0.04, "Eisen", quer=(0, 1, 0))
    q(netz, st, M, (lx, 0.075, 1.45), (0.02, 0.02, 0.86), "Fachwerk")


def hund(netz, st, M):
    """Hofhund: Ein Hund liegt auf einem Strohlager (vor dem Herd), daneben Wassernapf und Knochen. Etwa 1,4 m breit, 1,0 m tief, 0,4 m hoch."""
    import gardist_innen as gi
    gi.strohhaufen(netz, st, v(M, (0.0, 0.6, 0.0)), 0.7, 0.5, 0.05, saat=7)
    gi.hund(netz, st, v(M, (-0.1, 0.55, 0.04)), math.degrees(math.atan2(M.to_3x3()[1][0], M.to_3x3()[0][0])) + 100, "Hundfell")
    napf = v(M, (0.7, 0.8, 0.0))
    gi.drehkoerper(netz, st, napf, [(0.0, 0.1), (0.06, 0.12), (0.06, 0.1), (0.01, 0.09)], "Holz alt", n=8)
    gi.scheibe(netz, st, napf, 0.1, 0.045, "Wasser", n=8)
    q(netz, st, M, (0.5, 0.35, 0.02), (0.14, 0.03, 0.03), "Leinen")                                       # Knochen


ALLE = {
    "Schleifstein": (schleifstein, 2.0), "Drechselbank": (drechselbank, 2.0), "Steinsaege": (steinsaege, 2.4), "Athanor": (athanor, 2.0),
    "Enfleurage": (enfleurage, 2.3), "Gewoelbekeller": (gewoelbekeller, 3.2), "Skriptorium": (skriptorium, 2.6),
    "Ladentheke": (ladentheke, 2.5), "Lehrlingsstube": (lehrlingsstube, 3.4),
    "Vorratskammer": (vorratskammer, 2.6), "Kueche": (kueche, 2.4), "Hauswappen": (hauswappen, 1.4), "Stube": (stube, 3.6),
    "Bibliothek": (bibliothek, 3.0), "Riegel": (riegel, 2.4), "Hund": (hund, 1.6),
}
