"""Hafenbauten: Flussschiff, Tretradkran, Lagerschuppen und Zollstelle (Greybox mit Fluss und Hafen, PR #199).

    blender -b --python tools/modelle/hafenbauten.py -- [--schnell] [--ohne-bilder] [--nur=wasser_kran]

Schreibt nach game/assets/modelle/wasser/ vier Dateien und je ein Vorschaubild docs/modelle/<name>.png:
    wasser_schiff.glb     Schiff, Segel_Gesetzt, Segel_Gerefft, Laufplanke, Ladung_1–6, Fahne und die Anker
    wasser_kran.glb       Haus, Kranhaus (Kind Tretrad) und die Anker
    wasser_schuppen.glb   Haus und die Anker
    wasser_zoll.glb       Haus und die Anker
Die Teile, Grundformen und die Vorschau-Bühne kommen aus wasser.py; Höhen wie dort: z = 0 ist die Uferkante, das Wasser der
Vorschau liegt bei WASSER = −1,0 m, Kaimauern reichen bis FUSS = −4,5 m.

Flussschiff (Lastkahn, 20 m × 4,2 m) – X ist die Längsachse, der Bug liegt bei +X, die Seite zum Kai bei −Y (im Spiel +Z).
Ursprung auf der Wasserlinie in der Mitte: Das Spiel setzt das Schiff mit y = 0 auf den Wasserspiegel.
    Schiff         Rumpf (geteert, Setzbord, Barkholz), Laderaum, Vor- und Achterdeck, Kajüte achtern, Mast mit Rah und Stagen,
                   Steuerruder mit Pinne.
    Segel_Gesetzt  das Rahsegel, voll (Wind von achtern) – zur Fahrt;   Segel_Gerefft  dasselbe an der Rah aufgetucht – im Hafen.
                   Das Spiel zeigt genau eines von beiden.
    Laufplanke     Ursprung am Dollbord mittschiffs (Kaiseite), führt 2,6 m nach −Y und steigt 0,35 m (auf den Kai); im Spiel nur
                   am Kai einblenden.
    Ladung_1–6     Ladung im Raum von achtern nach vorn: Fässer, Kisten, Säcke unter Plane, Wollballen, Fässer, Kisten. Das Spiel
                   blendet so viele ein, wie das Schiff geladen hat.
    Fahne          Wimpel in der Hausfarbe am Masttopp (Stange „Holz“, Tuch „Hausfarbe“ wie die Fahnen der Häuser; flattert im Wind).
    Anker_Bug (Blick +X), Anker_Heck (Blick −X) auf der Wasserlinie an den Steven; Anker_Kai auf der Wasserlinie 0,3 m vor der
    Bordwand mittschiffs zum Kai (Blick zum Schiff): an diese Stelle gehört die Kaikante; Anker_Planke am Ende der Laufplanke (auf
    Kaihöhe, Blick zum Schiff); Anker_Schiffer an der Pinne (Blick +X).

Tretradkran (Grundfläche 8 × 8 m, am Ufer: Land bei −Y, Uferkante bei y = 4, im Spiel z = −4) – Kai aus Bohlen mit Spundwand
wie beim Hafen, darauf ein achteckiger Sockel aus Bruchstein mit Stufen.
    Haus      alles Feste;   Kranhaus  das drehbare Kranhaus (Bretter, Schieferdach), Ursprung auf dem Sockel in der Drehachse
    (dreht um die Hochachse), Ausleger durch den Giebel zum Wasser, Seil, Haken mit Last;  Tretrad  Kind des Kranhauses, zwei
    Räder links und rechts auf einer Achse, Ursprung in der Achse (dreht um X; das Spiel dreht es wie das des Hafens).
    Anker_Tuer (vor den Stufen), Anker_Ufer, Anker_Anleger (Kai unter dem Ausleger), Anker_Tretrad (im linken Rad, am Kranhaus).

Lagerschuppen (Grundfläche 12 × 10 m, Ursprung in der Mitte, vorn −Y): Fachwerk mit Bretterwänden auf Bruchsteinsockel,
Ziegeldach, Tor, Ladeluke mit Aufzugsbalken, Ware vor dem Tor. Haus, Anker_Tuer, Anker_Karren.

Zollstelle (Grundfläche 8 × 8 m, am Ufer wie der Kran): Zollturm aus Bruchstein mit Eckquadern und Fachwerkgeschoss,
Zeltdach aus Schiefer, Stadtwappen, Schlagbaum über den Weg vorn, an der Kaimauer Haspel und Kettenpfosten, die Kette hängt
ins Wasser. Haus, Anker_Tuer, Anker_Schlagbaum, Anker_Ufer, Anker_Anleger.

Kran, Schuppen und Zoll gehören der Stadt (keine Hausfarbe, keine Zustände). Prüfung mit `pruefen.py <glb> --wasser`
(Schiff, Kran, Zoll) bzw. `pruefen.py <glb> 12 10 --sonderbau` (Schuppen).
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
import wasser as ws  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Wolle": (0.74, 0.70, 0.60),           # Wollballen
})

WASSER, FUSS, KAI = ws.WASSER, ws.FUSS, ws.KAI

# Getrennt bleiben (nicht in „Farben“): die Materialien, auf die die Material-Bibliothek ihre Texturen legt
# (game/daten/materialbibliothek.json, „namen“) – dazu „Holz“, an dem das Spiel den Mast der Fahne erkennt.
with open(os.path.join(bs.WURZEL, "game", "daten", "materialbibliothek.json"), encoding="utf-8") as _f:
    BEHALTEN = set(bs.BENANNTE_STOFFE) | set(json.load(_f)["namen"]) | {"Holz"}

# Schiff
S_LAENGE, S_BREITE = 20.0, 4.2
S_BORD = 0.85           # Dollbord mittschiffs über der Wasserlinie
S_TIEF = 0.7            # Tiefgang
S_SPRUNG = 0.55         # wie weit die Enden höher liegen
S_MAST = 3.6            # Mast (x)
MAST_HOCH = 10.5        # über dem Dollbord
RAH = 8.6               # Höhe der Rah über dem Dollbord
PLANKE = 2.6

# Kran und Zoll: Uferkante bei y = 4
K_UFER = 4.0
K_MITTE = Vector((0.0, 1.0, 0.0))    # Drehachse des Kranhauses
SOCKEL = 0.6


def beide_seiten(netz, punkte, normale, stoff, abstand=0.008):
    """Dünnes Tuch: dieselbe Fläche nach beiden Seiten (Segel, Plane)."""
    n = Vector(normale).normalized()
    netz.vieleck_nach([Vector(p) + n * abstand for p in punkte], n, stoff)
    netz.vieleck_nach([Vector(p) - n * abstand for p in punkte], -n, stoff)


def kind(eltern, o, ort):
    o.parent = eltern
    o.matrix_parent_inverse = Matrix.Identity(4)
    o.location = Vector(ort)
    return o


# ─────────────────────────────────────────────────────────────────────────────
#  Flussschiff
# ─────────────────────────────────────────────────────────────────────────────

def ringe_schiff(stationen=16):
    """Stationen des Rumpfs: (x, halbe Breite am Dollbord, halbe Breite am Boden, Höhe Boden, Höhe Dollbord).
    Flacher Boden, löffelförmig hochgezogene Enden, der Bug etwas schärfer als das Heck."""
    ringe = []
    for i in range(stationen + 1):
        t = -1 + 2 * i / stationen
        a = abs(t)
        voll = 0.4 if t > 0 else 0.5                  # ab hier läuft der Rumpf zum Ende hin zusammen
        f = max(0.0, a - voll) / (1 - voll)
        w = S_BREITE / 2 * (1 - f ** 2 * (0.72 if t > 0 else 0.6))
        zb = -S_TIEF + (S_TIEF + S_BORD * 0.55) * (max(0.0, a - 0.55) / 0.45) ** 2
        ringe.append((t * S_LAENGE / 2, w, w * 0.8, zb, S_BORD + S_SPRUNG * a ** 2.2))
    return ringe


def schiff_rumpf(st):
    netz = bs.Netz("Schiff")
    ringe = ringe_schiff()
    boden = -S_TIEF + 0.18
    mitte = Vector((0, 0, S_BORD))

    def flur(ring):
        _, w, b_, z, g = ring
        fz = max(boden, z + 0.06)
        return fz, b_ + (w - b_) * (fz - z) / (g - z) - 0.08

    n = len(ringe) - 1
    for i, ((xa, wa, ba, za, ga), (xb, wb, bb, zb_, gb)) in enumerate(zip(ringe, ringe[1:])):
        (fza, fa_), (fzb, fb_) = flur(ringe[i]), flur(ringe[i + 1])
        ende = i in (0, 1, n - 2, n - 1)
        for s in (-1, 1):
            seite = [Vector(p) for p in ((xa, s * ba, za), (xb, s * bb, zb_), (xb, s * wb, gb), (xa, s * wa, ga))]
            richtung = sum(seite, Vector()) / 4 - mitte
            # geteerter Rumpf bis 0,3 m unter dem Dollbord, darüber das Setzbord in Holz
            ua, ub = seite[0].lerp(seite[3], 0.78), seite[1].lerp(seite[2], 0.78)
            netz.vieleck_nach([seite[0], seite[1], ub, ua], richtung, st["Teerholz"])
            netz.vieleck_nach([ua, ub, seite[2], seite[3]], richtung, st["Holz"])
            for f in (0.3, 0.55):                                 # Plankengänge im Teer
                ws.linie(netz, st, seite[0].lerp(seite[3], f), seite[1].lerp(seite[2], f), (0, s, 0), "Holz alt", breite=0.03)
            # Barkholz: kräftige Leiste an der Grenze von Teer und Setzbord
            ws.klotz(netz, st["Holz alt"], [ua, ub, ub + Vector((0, s * 0.07, 0)), ua + Vector((0, s * 0.07, 0)),
                                            ua + Vector((0, 0, 0.12)), ub + Vector((0, 0, 0.12)),
                                            ub + Vector((0, s * 0.07, 0.12)), ua + Vector((0, s * 0.07, 0.12))], ohne=("-z",))
            if not ende:                                          # Innenseite und Dollbord
                ia, ib = (xa, s * (wa - 0.08), ga - 0.02), (xb, s * (wb - 0.08), gb - 0.02)
                netz.vieleck_nach([(xa, s * fa_, fza), (xb, s * fb_, fzb), ib, ia], (0, -s, 0.3), st["Holz"])
            netz.vieleck_nach([(xa, s * wa, ga), (xb, s * wb, gb), (xb, s * (wb - 0.08), gb), (xa, s * (wa - 0.08), ga)],
                              (0, 0, 1), st["Holz alt"])
        if ende:                                                  # Vor- und Achterdeck
            netz.vieleck_nach([(xa, -wa + 0.08, ga - 0.03), (xb, -wb + 0.08, gb - 0.03), (xb, wb - 0.08, gb - 0.03),
                               (xa, wa - 0.08, ga - 0.03)], (0, 0, 1), st["Bretter"])
        else:
            netz.vieleck_nach([(xa, -fa_, fza), (xb, -fb_, fzb), (xb, fb_, fzb), (xa, fa_, fza)], (0, 0, 1), st["Bretter"])
    for i in (2, n - 2):                                          # Schotten am Laderaum
        x, w, b_, z, g = ringe[i]
        fz, f = flur(ringe[i])
        netz.vieleck_nach([(x, -f, fz), (x, f, fz), (x, w - 0.08, g - 0.03), (x, -(w - 0.08), g - 0.03)], (-x, 0, 0), st["Holz alt"])
    for x, w, b_, z, g in (ringe[0], ringe[-1]):                  # Steven
        netz.vieleck_nach([(x, -b_, z), (x, b_, z), (x, w, g), (x, -w, g)], (x, 0, 0), st["Teerholz"])
        s = 1 if x > 0 else -1
        netz.stab((x + s * 0.02, 0, z), (x + s * 0.1, 0, g + 0.35), 0.16, 0.18, st["Holz alt"])
    # Spanten im Laderaum (jeder zweite Meter), Ducht am Mast
    for i in range(3, n - 2):
        x, w, b_, z, g = ringe[i]
        fz, f = flur(ringe[i])
        for s in (-1, 1):
            netz.stab((x, s * (f + 0.02), fz), (x, s * (w - 0.12), g - 0.06), 0.12, 0.1, st["Holz alt"], quer=(1, 0, 0))
    netz.stab((S_MAST, -S_BREITE / 2 + 0.1, S_BORD - 0.12), (S_MAST, S_BREITE / 2 - 0.1, S_BORD - 0.12), 0.3, 0.1,
              st["Holz"], quer=(0, 0, 1))
    # Mast, Rah, Stagen und Wanten
    netz.stab((S_MAST, 0, boden), (S_MAST, 0, S_BORD + MAST_HOCH), 0.24, 0.24, st["Holz"])
    netz.quader((S_MAST, 0, S_BORD + MAST_HOCH - 0.6), (0.34, 0.34, 0.4), st["Holz alt"])          # Mastkorb (Eselshaupt)
    netz.stab((S_MAST + 0.2, -3.6, S_BORD + RAH), (S_MAST + 0.2, 3.6, S_BORD + RAH), 0.14, 0.14, st["Holz"], quer=(1, 0, 0))
    bug, heck = ringe[-1], ringe[0]
    netz.stab((S_MAST, 0, S_BORD + MAST_HOCH - 0.4), (bug[0] - 0.3, 0, bug[4] + 0.2), 0.035, 0.035, st["Seil"])
    for s in (-1, 1):
        for dx in (-0.6, 0.5):
            netz.stab((S_MAST + dx, s * (S_BREITE / 2 - 0.1), S_BORD + 0.05), (S_MAST + dx * 0.2, s * 0.12, S_BORD + MAST_HOCH - 0.5 - abs(dx)),
                      0.035, 0.035, st["Seil"], quer=(1, 0, 0))
        netz.stab((S_MAST + 0.2, s * 3.5, S_BORD + RAH), (S_MAST - 3.0, s * (S_BREITE / 2 - 0.15), S_BORD + 0.1), 0.03, 0.03,
                  st["Seil"], quer=(1, 0, 0))                                                    # Brassen
    # Kajüte achtern mit Schindeldach, Tür nach vorn
    xk0, xk1 = -S_LAENGE / 2 + 1.6, -S_LAENGE / 2 + 4.4
    zk = flur(ringe[2])[0]
    ws.kasten(netz, st, xk0, xk1, -1.3, 1.3, zk, S_BORD + 1.0, "Holz alt", ohne=("-z", "+z"))
    bs.satteldach(netz, st, xk0, xk1, -1.3, 1.3, S_BORD + 1.0, 26, traufe=0.18, ortgang=0.15, enden="Holz alt", **bs.SCHINDEL)
    hoch = 1.3 * math.tan(math.radians(26))
    for x, seite in ((xk0, "links"), (xk1, "rechts")):
        bs.platte(netz, bs.wandmatrix((x, 0, S_BORD + 1.0), seite), [(-1.3, 0), (1.3, 0), (0, hoch)], 0, 0.1, st["Holz alt"])
    ws.kasten(netz, st, xk1, xk1 + 0.03, -0.35, 0.35, S_BORD - 0.1, S_BORD + 0.85, "Loch", ohne=("-z",))
    for s in (-1, 1):                                             # Luken an den Seiten
        ws.kasten(netz, st, (xk0 + xk1) / 2 - 0.25, (xk0 + xk1) / 2 + 0.25, s * 1.3 - 0.015, s * 1.3 + 0.015,
                  S_BORD + 0.35, S_BORD + 0.7, "Loch", ohne=("-z",))
    # Ruder am Heck mit Pinne über die Kajüte hinweg
    x_h, _, _, z_h, g_h = heck
    netz.stab((x_h - 0.05, 0, g_h + 0.2), (x_h - 1.3, 0, -0.9), 0.9, 0.1, st["Teerholz"], quer=(0, 1, 0))
    netz.stab((x_h - 0.1, 0, g_h + 0.4), (x_h - 0.2, 0, -0.2), 0.14, 0.14, st["Holz"])
    netz.stab((x_h - 0.15, 0, g_h + 0.35), (x_h + 1.4, 0, g_h + 0.55), 0.08, 0.08, st["Holz"])
    # Anker am Bug, Poller an Bug und Heck
    netz.stab((bug[0] - 0.6, 0.7, bug[4] - 0.2), (bug[0] - 0.6, 0.7, bug[4] + 0.15), 0.1, 0.5, st["Eisen"], quer=(1, 0, 0))
    for x in (bug[0] - 1.2, heck[0] + 1.2):
        r = next(r for r in ringe if abs(r[0] - x) < S_LAENGE / 16 + 0.01)
        for s in (-1, 1):
            lb.saeule(netz, st, (x, s * 0.6, 0), 0.1, r[4] - 0.03, r[4] + 0.35, "Holz", n=6)
    return netz.objekt()


def segel_gesetzt(st):
    """Rahsegel, vom Wind von achtern nach vorn gewölbt (3 × 3 Felder), mit Schoten zum Dollbord."""
    netz = bs.Netz("Segel_Gesetzt", alle=True)
    oben, unten, halb = S_BORD + RAH - 0.1, S_BORD + 2.2, 3.3
    nu, nv = 4, 3

    def p(i, j):
        u, v = -1 + 2 * i / nu, j / nv                            # u quer, v von unten (0) nach oben (1)
        bauch = 0.9 * (1 - u * u) * math.sin(math.pi * (0.15 + 0.85 * (1 - v)))
        breite = halb * (1 - 0.05 * (1 - v))
        return Vector((S_MAST + 0.3 + bauch, u * breite, unten + (oben - unten) * v))
    for i in range(nu):
        for j in range(nv):
            q = [p(i, j), p(i + 1, j), p(i + 1, j + 1), p(i, j + 1)]
            beide_seiten(netz, q, (1, 0, 0), st["Segeltuch"])
    for s in (-1, 1):                                             # Schoten
        netz.stab(p(0 if s < 0 else nu, 0), (S_MAST - 2.2, s * (S_BREITE / 2 - 0.15), S_BORD + 0.1), 0.03, 0.03, st["Seil"],
                  quer=(1, 0, 0))
    for i in range(1, nu):                                        # Nähte der Tuchbahnen
        for j in range(nv):
            ws.linie(netz, st, p(i, j), p(i, j + 1), (1, 0, 0), "Seil", breite=0.035, abstand=0.012)
    return netz.objekt()


def segel_gerefft(st):
    netz = bs.Netz("Segel_Gerefft")
    z = S_BORD + RAH - 0.22
    netz.prisma([(0.2 * math.cos(2 * math.pi * k / 6), z + 0.16 * math.sin(2 * math.pi * k / 6)) for k in range(6)],
                -3.3, 3.3, st["Segeltuch"], matrix=Matrix.Translation((S_MAST + 0.25, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    for y in (-2.4, -0.8, 0.8, 2.4):                              # Zeisinge um das aufgetuchte Segel
        netz.quader((S_MAST + 0.25, y, z), (0.44, 0.06, 0.36), st["Seil"])
    return netz.objekt()


def laufplanke(st):
    """Ursprung am Dollbord mittschiffs auf der Kaiseite; die Planke liegt nach −Y und steigt 0,35 m."""
    netz = bs.Netz("Laufplanke")
    a, b = Vector((0, 0.15, 0.04)), Vector((0, -PLANKE, 0.35 + 0.04))
    netz.stab(a, b, 0.55, 0.06, st["Bretter"], quer=(0, 0, 1))
    k = 1
    while k * 0.4 < PLANKE:                                       # Trittleisten
        p = a.lerp(b, k * 0.4 / (PLANKE + 0.15)) + Vector((0, 0, 0.04))
        netz.quader(p, (0.5, 0.05, 0.03), st["Holz"], ohne=("-z",))
        k += 1
    return netz.objekt()


def ladung(st):
    """Sechs Ladungsplätze im Raum von achtern nach vorn."""
    boden = -S_TIEF + 0.18
    objekte = []
    plaetze = [(-4.4, "fass"), (-2.6, "kiste"), (-0.8, "sack"), (1.0, "ballen"), (5.3, "fass"), (7.0, "kiste")]
    for k, (x, art) in enumerate(plaetze, 1):
        netz = bs.Netz(f"Ladung_{k}")
        zf = nt.zufall(k * 13)
        if art == "fass":
            for dx in (-0.36, 0.36):
                for y in (-0.75, 0.0, 0.75):
                    bs.fass(netz, st, Vector((x + dx, y, boden)), r=0.32, h=0.85, reifen=(0.18 + 0.01 * k, 0.72))
            bs.fass(netz, st, Vector((x, -0.37, boden + 0.85)), r=0.3, h=0.8)
            bs.fass(netz, st, Vector((x, 0.37, boden + 0.85)), r=0.3, h=0.8)
        elif art == "kiste":
            for dx, y, dz, dreh in ((-0.44, -0.6, 0, 0.05), (0.46, -0.55, 0, -0.08), (-0.45, 0.6, 0, 0.1), (0.45, 0.62, 0, 0.0),
                                    (0.0, -0.5, 0.66, 0.2), (0.05, 0.45, 0.67, -0.12)):
                lb.kiste(netz, st, (x + dx, y, boden + dz), (0.78, 0.9, 0.65), dreh=dreh)
        elif art == "sack":
            for dx in (-0.45, 0.0, 0.45):
                for y in (-0.8, -0.27, 0.27, 0.8):
                    lb.saeule(netz, st, (x + dx + zf.uniform(-0.05, 0.05), y, boden), 0.22, 0, 0.42 + 0.03 * ((round(dx / 0.45) + round(y / 0.53)) % 2), "Sack", n=6)
            # Plane darüber, an den Seiten herabhängend
            z = boden + 0.62
            netz.vieleck_nach([(x - 0.8, -1.2, z), (x + 0.8, -1.2, z), (x + 0.8, 1.2, z), (x - 0.8, 1.2, z)], (0, 0, 1), st["Segeltuch"])
            for s in (-1, 1):
                netz.vieleck_nach([(x - 0.8, s * 1.2, z), (x + 0.8, s * 1.2, z), (x + 0.8, s * 1.3, boden + 0.15),
                                   (x - 0.8, s * 1.3, boden + 0.15)], (0, s, 0.4), st["Segeltuch"])
                netz.vieleck_nach([(x + s * 0.8, -1.2, z), (x + s * 0.8, 1.2, z), (x + s * 0.9, 1.25, boden + 0.15),
                                   (x + s * 0.9, -1.25, boden + 0.15)], (s, 0, 0.4), st["Segeltuch"])
        else:                                                     # Wollballen, verschnürt
            for dx, y, dz in ((-0.4, -0.6, 0), (0.4, -0.6, 0), (-0.4, 0.6, 0), (0.4, 0.6, 0), (0.0, 0.0, 0.62)):
                mitte = (x + dx, y, boden + dz)
                lb.kiste(netz, st, mitte, (0.75, 1.05, 0.6), stoff="Wolle", dreh=zf.uniform(-0.1, 0.1))
                netz.quader((mitte[0], mitte[1], mitte[2] + 0.31), (0.06, 1.07, 0.02), st["Seil"])
        objekte.append(netz.objekt())
    return objekte


def wimpel(st):
    """Wimpel am Masttopp: kurze Stange, langes Tuch in der Hausfarbe mit Schwalbenschwanz, weht nach achtern (−X)."""
    netz = bs.Netz("Fahne")
    z0 = S_BORD + MAST_HOCH
    netz.stab((S_MAST, 0, z0 - 0.1), (S_MAST, 0, z0 + 1.3), 0.06, 0.06, st["Holz"])
    netz.quader((S_MAST, 0, z0 + 1.35), (0.12, 0.12, 0.12), st["Eisen"])
    tuch = [(0, 0), (2.4, 0.02), (1.9, 0.24), (2.4, 0.48), (0, 0.5)]
    netz.prisma(tuch, -0.015, 0.015, st["Hausfarbe"],
                matrix=Matrix.Translation((S_MAST - 0.04, 0, z0 + 0.7)) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    return netz.objekt()


def schiff_bauen(st):
    teile = [schiff_rumpf(st), segel_gesetzt(st), segel_gerefft(st), wimpel(st)] + ladung(st)
    ringe = ringe_schiff()
    planke = laufplanke(st)
    planke.location = (0.3, -(ringe[8][1]), ringe[8][4])
    teile.append(planke)
    x_h, g_h = ringe[0][0], ringe[0][4]
    anker = [bs.anker("Anker_Bug", (S_LAENGE / 2 + 0.1, 0, 0), 270), bs.anker("Anker_Heck", (-S_LAENGE / 2 - 0.1, 0, 0), 90),
             bs.anker("Anker_Kai", (0.3, -S_BREITE / 2 - 0.3, 0), 0),
             bs.anker("Anker_Planke", (0.3, -S_BREITE / 2 - PLANKE, S_BORD + 0.39), 0),
             bs.anker("Anker_Schiffer", (x_h + 1.1, 0.35, g_h - 0.03), 270)]
    return teile + anker


# ─────────────────────────────────────────────────────────────────────────────
#  Kai mit Spundwand (Kran) und Kaimauer aus Stein (Zoll)
# ─────────────────────────────────────────────────────────────────────────────

def holzkai(netz, st, x0, x1, y0, saat):
    """Kai aus Bohlen von y0 bis zur Uferkante K_UFER (Oberkante KAI), Spundwand bis FUSS, Pfähle davor, Bordschwelle."""
    ws.bohlen(netz, st, x0, x1, y0 + 0.2, K_UFER - 0.2, lambda x: KAI, saat=saat)
    ws.kasten(netz, st, x0, x1, y0, y0 + 0.2, 0.0, KAI + 0.02, "Holz", ohne=("-z",))
    ws.kasten(netz, st, x0, x1, K_UFER - 0.2, K_UFER, KAI - 0.25, KAI + 0.08, "Holz", ohne=("-z",))
    for xa in (x0, x1 - 0.16):
        ws.kasten(netz, st, xa, xa + 0.16, y0 + 0.2, K_UFER - 0.2, 0.0, KAI, "Holz alt", ohne=("-z",))
    netz.vieleck_nach([(x0, K_UFER, FUSS), (x1, K_UFER, FUSS), (x1, K_UFER, KAI - 0.25), (x0, K_UFER, KAI - 0.25)], (0, 1, 0),
                      st["Holz alt"])
    for x0p in (x0, x1):                                          # Seiten der Spundwand (unter dem Gelände unsichtbar)
        netz.vieleck_nach([(x0p, K_UFER - 0.2, FUSS), (x0p, K_UFER, FUSS), (x0p, K_UFER, KAI - 0.25),
                           (x0p, K_UFER - 0.2, KAI - 0.25)], (x0p, 0, 0), st["Holz alt"])
    x = x0 + 1.0
    while x < x1 - 0.5:
        ws.pfahl(netz, st, x, K_UFER + 0.15, FUSS, KAI + 0.3, r=0.15, n=7, kopf="Scheitholz")
        x += 2.0


def steinkai(netz, st, x0, x1):
    """Kaimauer aus Quadern an der Uferkante mit Abdeckplatten aus Sandstein und Eisenring."""
    netz.vieleck_nach([(x0, K_UFER, FUSS), (x1, K_UFER, FUSS), (x1, K_UFER, -0.02), (x0, K_UFER, -0.02)], (0, 1, 0), st["Haustein"])
    for x0p in (x0, x1):
        netz.vieleck_nach([(x0p, K_UFER - 0.6, FUSS), (x0p, K_UFER, FUSS), (x0p, K_UFER, -0.02), (x0p, K_UFER - 0.6, -0.02)],
                          (x0p, 0, 0), st["Haustein"])
    n = round((x1 - x0) / 1.2)
    for k in range(n):
        a, b = x0 + (x1 - x0) * k / n + 0.01, x0 + (x1 - x0) * (k + 1) / n - 0.01
        ws.kasten(netz, st, a, b, K_UFER - 0.6, K_UFER + 0.06, -0.02, 0.16, "Sandstein", ohne=("-z",))


# ─────────────────────────────────────────────────────────────────────────────
#  Tretradkran
# ─────────────────────────────────────────────────────────────────────────────

KH_X, KH_Y0, KH_Y1 = 1.75, -2.0, 1.9     # Kranhaus lokal: halbe Breite, Rückwand (Land), Vorderwand (Wasser)
KH_WAND = 3.8
RAD_ACHSE = Vector((0, -0.1, 2.15))      # lokal im Kranhaus
RAD_R = 2.0


def kran_haus(st):
    netz = bs.Netz("Haus")
    lb.flaeche(netz, st, lb.rechteck(-4, -4, 4, -3.0), 0.01, "Erde hell")
    holzkai(netz, st, -4.0, 4.0, -3.0, saat=41)
    for x in (-3.2, 3.2):                                         # Poller
        ws.pfahl(netz, st, x, K_UFER - 0.45, KAI, KAI + 0.55, r=0.17, n=8, kopf="Scheitholz")
        ws.kasten(netz, st, x - 0.2, x + 0.2, K_UFER - 0.65, K_UFER - 0.25, KAI + 0.32, KAI + 0.4, "Eisen")
    # Sockel: Achteck aus Bruchstein, Abdeckung aus Sandstein, Stufen zum Land
    m = K_MITTE
    bs.achteck(netz, st, (m.x, m.y, 0), 2.75, KAI, SOCKEL + KAI - 0.12, stoff="Bruchstein")
    bs.achteck(netz, st, (m.x, m.y, 0), 2.9, SOCKEL + KAI - 0.12, SOCKEL + KAI, stoff="Sandstein")
    for y0, z in ((m.y - 3.55, KAI + 0.2), (m.y - 3.2, KAI + 0.41)):
        ws.kasten(netz, st, m.x - 0.8 - 0.01 * z, m.x + 0.8 + 0.01 * z, y0, m.y - 2.5, KAI, z, "Sandstein", ohne=("-z", "+y"))
    # Kranbalken-Stütze am Kai: Prellholz unter dem Ausleger
    ws.kasten(netz, st, -1.2, 1.2, K_UFER - 0.55, K_UFER - 0.3, KAI, KAI + 0.18, "Holz alt", ohne=("-z",))
    # Ware am Kai
    for i, (x, y) in enumerate(((-3.35, 2.35), (-2.6, 2.5), (-3.0, 3.15))):
        bs.fass(netz, st, Vector((x, y, KAI)), r=0.3, h=0.85, reifen=(0.2 + 0.01 * i, 0.74))
    lb.kiste(netz, st, (2.9, 2.6, KAI), (0.8, 0.7, 0.62), dreh=0.15)
    lb.kiste(netz, st, (3.1, 1.8, KAI), (0.7, 0.7, 0.55), dreh=-0.1)
    return netz.objekt()


def kranhaus(st):
    """Drehbares Kranhaus, lokal: Ursprung auf dem Sockel in der Drehachse, Ausleger nach +Y (zum Wasser)."""
    netz = bs.Netz("Kranhaus")
    x, y0, y1, h = KH_X, KH_Y0, KH_Y1, KH_WAND
    ws.kasten(netz, st, -x - 0.1, x + 0.1, y0 - 0.1, y1 + 0.1, 0.0, 0.22, "Holz", ohne=("-z",))       # Schwellenkranz
    for (xa, ya, xb, yb, n) in ((-x, y0, x, y0, (0, -1, 0)), (x, y1, -x, y1, (0, 1, 0)), (x, y0, x, y1, (1, 0, 0)),
                                (-x, y1, -x, y0, (-1, 0, 0))):
        netz.vieleck_nach([(xa, ya, 0.22), (xb, yb, 0.22), (xb, yb, h), (xa, ya, h)], n, st["Bretter"])
    # Ständer an den Ecken und in den Wandmitten, Rähm, Kopfbänder
    for px, py in ((-x, y0), (x, y0), (-x, y1), (x, y1), (-x, (y0 + y1) / 2), (x, (y0 + y1) / 2)):
        ws.kasten(netz, st, px - 0.11, px + 0.11, py - 0.11, py + 0.11, 0.22, h - 0.25, "Fachwerk", ohne=("-z", "+z"))
    for y in (y0, y1):
        ws.kasten(netz, st, -x - 0.11, x + 0.11, y - 0.12, y + 0.12, h - 0.26, h + 0.01, "Fachwerk", ohne=("-z",))
    for xs in (-x, x):
        ws.kasten(netz, st, xs - 0.12, xs + 0.12, y0 - 0.11, y1 + 0.11, h - 0.25, h, "Fachwerk", ohne=("-z",))
    # Tür zum Land (−Y), Luke unter dem Ausleger (+Y)
    ws.kasten(netz, st, -0.5, 0.5, y0 - 0.02, y0, 0.25, 2.2, "Loch", ohne=("-z", "+y"))
    for s in (-1, 1):
        ws.kasten(netz, st, s * 0.5 - 0.08, s * 0.5 + 0.08, y0 - 0.09, y0, 0.22, 2.2, "Holz", ohne=("-z", "+y", "+z"))
    ws.kasten(netz, st, -0.6, 0.6, y0 - 0.09, y0, 2.2, 2.36, "Holz", ohne=("-z",))
    ws.kasten(netz, st, -0.55, 0.55, y1, y1 + 0.02, 2.2, 3.4, "Loch", ohne=("-z", "-y"))
    # Satteldach mit dem First zum Wasser (entlang Y), Giebel aus Brettern
    dreh = Matrix.Rotation(math.radians(90), 4, "Z")              # lokales X des Dachs = Y
    dach = bs.satteldach(netz, st, y0, y1, -x, x, h, 45, traufe=0.3, ortgang=0.25, enden="Holz", matrix=dreh, **bs.SCHIEFER)
    hoch = x * dach["tan"]
    for y, seite in ((y0, "vorne"), (y1, "hinten")):
        bs.platte(netz, bs.wandmatrix((0, y, h), seite), [(-x, 0), (x, 0), (0, hoch)], 0, 0.12, st["Bretter"])
    # Ausleger: liegt auf dem Rähm, ragt durch den Giebel zum Wasser, Strebe vom Rähm, Seil über die Rolle zur Trommel
    fuss, spitze = Vector((0, y0 + 0.6, h + 0.35)), Vector((0, y1 + 3.6, h + 1.7))
    netz.stab(fuss, spitze, 0.3, 0.26, st["Holz"], quer=(1, 0, 0))
    netz.stab((0, y1 + 0.15, h - 0.6), fuss.lerp(spitze, 0.72), 0.2, 0.18, st["Holz"], quer=(1, 0, 0))
    for s in (-1, 1):                                             # Zangen am Kopf
        netz.stab(spitze + Vector((s * 0.17, -0.5, -0.2)), spitze + Vector((s * 0.17, 0.15, 0.1)), 0.24, 0.05, st["Holz alt"],
                  quer=(1, 0, 0))
    ws.walze(netz, st, spitze.y + 0.05, spitze.z - 0.05, 0.18, -0.12, 0.12, "Eisen", n=8)
    netz.stab(spitze + Vector((0, 0.05, 0.13)), (0, y1 - 0.3, h + 0.55), 0.04, 0.04, st["Seil"])
    haken = Vector((0, spitze.y + 0.2, 1.6))
    netz.stab(spitze + Vector((0, 0.22, -0.1)), haken, 0.04, 0.04, st["Seil"])
    netz.quader(haken + Vector((0, 0, -0.08)), (0.08, 0.06, 0.18), st["Eisen"])
    for s in (-1, 1):
        netz.stab(haken + Vector((s * 0.03, 0, -0.15)), haken + Vector((s * 0.35, 0, -0.65)), 0.03, 0.03, st["Seil"])
    lb.kiste(netz, st, haken + Vector((0, 0, -1.25)), (0.75, 0.75, 0.6), dreh=0.3)
    o = netz.objekt()
    o.location = K_MITTE + Vector((0, 0, KAI + SOCKEL))
    return o


def kran_tretrad(st):
    """Zwei Treträder links und rechts außen am Kranhaus auf einer Achse; Ursprung in der Achse (dreht um X)."""
    netz = bs.Netz("Tretrad", alle=True)
    n, r, b = 16, RAD_R, 0.45
    for mx in (-(KH_X + 0.75), KH_X + 0.75):
        for x in (mx - b, mx + b):
            for k in range(n):
                w0, w1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
                d = 0.05 + 0.003 * (k % 2)
                wm = (w0 + w1) / 2
                for rr, normale in ((r, 1), (r - 0.14, -1)):
                    p = [(x - d, rr * math.cos(w0), rr * math.sin(w0)), (x + d, rr * math.cos(w0), rr * math.sin(w0)),
                         (x + d, rr * math.cos(w1), rr * math.sin(w1)), (x - d, rr * math.cos(w1), rr * math.sin(w1))]
                    netz.vieleck_nach(p, (0, normale * math.cos(wm), normale * math.sin(wm)), st["Holz"])
                for sx in (-1, 1):
                    p = [(x + sx * d, r * math.cos(w0), r * math.sin(w0)), (x + sx * d, r * math.cos(w1), r * math.sin(w1)),
                         (x + sx * d, (r - 0.14) * math.cos(w1), (r - 0.14) * math.sin(w1)),
                         (x + sx * d, (r - 0.14) * math.cos(w0), (r - 0.14) * math.sin(w0))]
                    netz.vieleck_nach(p, (sx, 0, 0), st["Holz"])
            for k in range(6):
                w = 2 * math.pi * (k + 0.5) / 6
                netz.stab((x, 0.16 * math.cos(w), 0.16 * math.sin(w)), (x, (r - 0.1) * math.cos(w), (r - 0.1) * math.sin(w)),
                          0.08, 0.08 + 0.012 * (k % 2), st["Holz"], quer=(1, 0, 0))
        for k in range(n):                                        # Trittbretter
            w = 2 * math.pi * (k + 0.5) / n
            t = Matrix.Translation((mx, (r - 0.165) * math.cos(w), (r - 0.165) * math.sin(w))) @ Matrix.Rotation(w, 4, "X")
            netz.quader((0, 0, 0), (2 * b - 0.1, 0.05, 0.3), st["Bretter"], matrix=t)
    ws.walze(netz, st, 0, 0, 0.14, -(KH_X + 1.3), KH_X + 1.3, "Holz", n=8)       # Welle durch das Haus
    ws.walze(netz, st, 0, 0, 0.32, -0.6, 0.6, "Holz", n=10)                      # Seiltrommel im Haus
    ws.walze(netz, st, 0, 0, 0.34, -0.5, 0.5, "Seil", n=10)
    return netz.objekt()


def kran_bauen(st):
    haus = kran_haus(st)
    kh = kranhaus(st)
    rad = kind(kh, kran_tretrad(st), RAD_ACHSE)
    tretrad_anker = ws.anschluss(kh, "Anker_Tretrad", RAD_ACHSE + Vector((-(KH_X + 0.75), 0, -RAD_R + 0.17)), 90)
    anker = [bs.anker("Anker_Tuer", (K_MITTE.x, K_MITTE.y - 3.85, KAI), 0), bs.anker("Anker_Ufer", (0, K_UFER, KAI), 0),
             bs.anker("Anker_Anleger", (2.0, K_UFER - 0.6, KAI), 0)]
    return [haus, kh, rad, tretrad_anker] + anker


# ─────────────────────────────────────────────────────────────────────────────
#  Lagerschuppen
# ─────────────────────────────────────────────────────────────────────────────

SCH_X0, SCH_X1, SCH_Y0, SCH_Y1 = -5.0, 5.0, -2.4, 3.6
SCH_SOCKEL, SCH_WAND = 0.45, 4.3


def fachwerk_wand(netz, st, M, L, H, staender, riegel=(1.9,), streben=True):
    """Bretterwand mit vorgesetztem Fachwerk im Wandsystem: Schwelle, Rähm, Ständer an den u in `staender`, Riegel in Höhe v,
    Fußstreben an den Ecken."""
    netz.vieleck([M @ Vector(p) for p in ((-L / 2, 0, 0), (L / 2, 0, 0), (L / 2, 0, H), (-L / 2, 0, H))], st["Bretter"])
    bs.balken(netz, M, -L / 2, 0.09, L / 2, 0.09, st["Fachwerk"], vor=0.06, breite=0.18, enden=True)
    bs.balken(netz, M, -L / 2, H - 0.09, L / 2, H - 0.09, st["Fachwerk"], vor=0.06, breite=0.18, enden=True)
    for u in staender:
        bs.balken(netz, M, u, 0.18, u, H - 0.18, st["Fachwerk"])
    for v in riegel:
        for a, b in zip(staender, staender[1:]):
            bs.balken(netz, M, a + 0.075, v, b - 0.075, v, st["Fachwerk"], breite=0.13)
    if streben:
        for a, b in ((staender[0], staender[1]), (staender[-1], staender[-2])):
            bs.balken(netz, M, a + (0.1 if b > a else -0.1), 0.25, b - (0.1 if b > a else -0.1), riegel[0] - 0.1, st["Fachwerk"],
                      breite=0.13, vor=bs.VOR_STREBE)


def schuppen_haus(st):
    netz = bs.Netz("Haus")
    lb.flaeche(netz, st, lb.rechteck(-6, -5, 6, SCH_Y0 - 0.05), 0.01, "Erde hell")
    lb.flaeche(netz, st, lb.rechteck(-1.8, -5, 1.8, SCH_Y0 - 0.05), 0.02, "Pflaster")
    x0, x1, y0, y1 = SCH_X0, SCH_X1, SCH_Y0, SCH_Y1
    L, T = x1 - x0, y1 - y0
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, SCH_SOCKEL / 2), (L + 0.12, T + 0.12, SCH_SOCKEL), st["Bruchstein"], ohne=("-z",))
    z = SCH_SOCKEL
    vorne = bs.wandmatrix((0, y0, z), "vorne")
    fachwerk_wand(netz, st, vorne, L, SCH_WAND, (-4.9, -3.0, -1.6, 1.6, 3.0, 4.9), riegel=(2.1,))
    fachwerk_wand(netz, st, bs.wandmatrix((0, y1, z), "hinten"), L, SCH_WAND, (-4.9, -2.5, 0.0, 2.5, 4.9), riegel=(2.1,))
    for x, seite in ((x0, "links"), (x1, "rechts")):
        fachwerk_wand(netz, st, bs.wandmatrix((x, (y0 + y1) / 2, z), seite), T, SCH_WAND, (-2.9, 0.0, 2.9), riegel=(2.1,))
    # Tor (zwei Flügel mit Eisenbändern), darüber die Ladeluke mit Aufzugsbalken
    for s in (-1, 1):
        bs.platte(netz, vorne, [(s * 0.02, 0.0), (s * 1.42, 0.0), (s * 1.42, 2.9), (s * 0.02, 2.9)], -0.07, -0.01, st["Holz alt"])
        for v in (0.5, 2.3):
            bs.platte(netz, vorne, [(s * 0.1, v), (s * 1.3, v), (s * 1.3, v + 0.08), (s * 0.1, v + 0.08)], -0.09, -0.07, st["Eisen"])
    bs.balken(netz, vorne, -1.55, 3.0, 1.55, 3.0, st["Fachwerk"], vor=0.08, breite=0.2, enden=True)
    bs.platte(netz, vorne, [(-0.7, 3.25), (0.7, 3.25), (0.7, 4.15), (-0.7, 4.15)], -0.012, -0.005, st["Loch"])
    for s in (-1, 1):
        bs.platte(netz, vorne, [(s * 0.7, 3.25), (s * 1.3, 3.25), (s * 1.3, 4.15), (s * 0.7, 4.15)], -0.05, -0.01, st["Holz alt"])
    # Dach: Ziegel, Giebel aus Brettern mit Luke, Aufzugsbalken aus dem Dach über der Luke
    zt = z + SCH_WAND
    dach = bs.satteldach(netz, st, x0, x1, y0, y1, zt, 47, traufe=0.4, ortgang=0.3, enden="Holz", **bs.ZIEGEL)
    hoch = T / 2 * dach["tan"]
    for x, seite in ((x0, "links"), (x1, "rechts")):
        M = bs.wandmatrix((x, (y0 + y1) / 2, zt), seite)
        bs.platte(netz, M, [(-T / 2, 0), (T / 2, 0), (0, hoch)], 0, 0.2, st["Bretter"])
        bs.platte(netz, M, [(-0.45, 0.6), (0.45, 0.6), (0.45, 1.5), (-0.45, 1.5)], -0.012, -0.005, st["Loch"])
    netz.stab((0, y0 + 1.2, zt + 0.5), (0, y0 - 1.1, zt + 0.5), 0.24, 0.22, st["Holz"], quer=(1, 0, 0))
    ws.walze(netz, st, y0 - 0.95, zt + 0.32, 0.1, -0.08, 0.08, "Eisen", n=6)
    netz.stab((0, y0 - 0.95, zt + 0.22), (0, y0 - 0.95, z + 2.3), 0.035, 0.035, st["Seil"])
    # Ware vor dem Schuppen, Bank an der Wand
    for i, (x, y) in enumerate(((-4.25, -3.1), (-3.5, -3.15), (-3.9, -3.8))):
        bs.fass(netz, st, Vector((x, y, 0.01)), r=0.32, h=0.9, reifen=(0.2 + 0.01 * i, 0.75))
    bs.fass(netz, st, Vector((-3.88, -3.3, 0.92)), r=0.3, h=0.85)
    for x, y, dreh in ((3.4, -3.0, 0.1), (4.3, -3.1, -0.15), (3.8, -4.1, 0.3)):
        lb.kiste(netz, st, (x, y, 0.01), (0.8, 0.7, 0.6), dreh=dreh)
    lb.kiste(netz, st, (3.85, -3.05, 0.62), (0.7, 0.6, 0.5), dreh=0.2)
    for k, (x, y) in enumerate(((2.2, -3.4), (2.0, -4.0), (2.55, -3.9))):
        lb.saeule(netz, st, (x, y, 0.01), 0.24, 0, 0.45, "Sack", n=7)
        nt.krone(netz, st, (x, y, 0.53), 0.22, 0.09, saat=k, stoffe=("Sack", "Sack"), n=6, flach_unten=0.0)
    return netz.objekt()


def schuppen_bauen(st):
    return [schuppen_haus(st), bs.anker("Anker_Tuer", (0, SCH_Y0 - 0.3, 0), 0), bs.anker("Anker_Karren", (0, -4.3, 0), 0)]


# ─────────────────────────────────────────────────────────────────────────────
#  Zollstelle
# ─────────────────────────────────────────────────────────────────────────────

TURM = Vector((-1.6, 1.0, 0))        # Mitte des Turms
T_HALB = 1.8
T_STEIN, T_FACHWERK = 5.2, 2.4       # Höhe des Steinsockels, des Fachwerkgeschosses
KETTE_X = 2.4


def kettenglieder(netz, st, a, b, laenge=0.2, dicke=0.05):
    """Kette von a nach b: Glieder abwechselnd liegend und stehend."""
    a, b = Vector(a), Vector(b)
    n = max(1, round((b - a).length / laenge))
    for k in range(n):
        p, q = a.lerp(b, k / n), a.lerp(b, (k + 1) / n)
        quer = (0, 0, 1) if k % 2 == 0 else (1, 0, 0) if abs((q - p).normalized().x) < 0.9 else (0, 1, 0)
        netz.stab(p, q, dicke * 1.8, dicke, st["Eisen"], quer=quer, mehr=0.03)


def zoll_haus(st):
    netz = bs.Netz("Haus")
    lb.flaeche(netz, st, lb.rechteck(-4, -4, 4, K_UFER - 0.6), 0.01, "Erde hell")
    lb.flaeche(netz, st, lb.rechteck(-4, -4, 4, -1.6), 0.02, "Pflaster")      # der Weg vor dem Turm
    steinkai(netz, st, -4.0, 4.0)
    # Turm: Bruchstein mit Eckquadern, Gesims, darauf ein vorkragendes Fachwerkgeschoss mit Putz und Zeltdach aus Schiefer
    c, h = TURM, T_HALB
    netz.quader((c.x, c.y, T_STEIN / 2), (2 * h, 2 * h, T_STEIN), st["Bruchstein"], ohne=("-z", "+z"))
    for sx in (-1, 1):
        for sy in (-1, 1):
            bs.eckquader(netz, st, c.x + sx * h, c.y + sy * h, sx, sy, 0.0, T_STEIN)
    bs.gesims(netz, st, c.x - h, c.x + h, c.y - h, c.y + h, T_STEIN - 0.16, vor=0.1)
    k = h + 0.25                                                  # Auskragung des Fachwerkgeschosses
    z0, z1 = T_STEIN, T_STEIN + T_FACHWERK
    netz.quader((c.x, c.y, z0 + 0.08), (2 * k + 0.1, 2 * k + 0.1, 0.16), st["Fachwerk"], ohne=("+z",))
    netz.quader((c.x, c.y, z0 + 0.16 + (T_FACHWERK - 0.16) / 2), (2 * k, 2 * k, T_FACHWERK - 0.16), st["Putz"], ohne=("-z", "+z"))
    for seite, punkt in (("vorne", (c.x, c.y - k, z0)), ("hinten", (c.x, c.y + k, z0)), ("links", (c.x - k, c.y, z0)),
                         ("rechts", (c.x + k, c.y, z0))):
        M = bs.wandmatrix(punkt, seite)
        o = bs.Oeffnung("fenster", 0.0, 0.85, 0.6, 0.8, laeden=False)
        bs.fachwerk(netz, M, 2 * k - 0.06, T_FACHWERK, [o], st, kreuze=True, feld=1.1, saat=len(seite))
        bs.fenster(netz, M, o, st, kreuz="Fachwerk")
    for sx in (-1, 1):                                            # Knaggen unter der Auskragung
        for sy in (-1, 1):
            netz.stab((c.x + sx * (h - 0.05), c.y + sy * (h - 0.05), z0 - 0.9), (c.x + sx * k, c.y + sy * k, z0 - 0.02),
                      0.16, 0.14, st["Fachwerk"])
    # Zeltdach aus Schiefer mit Knauf
    t = k + 0.35
    spitze = Vector((c.x, c.y, z1 + 3.4))
    ecken = [Vector((c.x + sx * t, c.y + sy * t, z1 - 0.25)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for a, b in zip(ecken, ecken[1:] + ecken[:1]):
        netz.vieleck_nach([a, b, spitze], ((a + b) / 2 - c).normalized() + Vector((0, 0, 1)), st["Schiefer"])
        netz.vieleck_nach([a, b, b + Vector((0, 0, -0.1)), a + Vector((0, 0, -0.1))], ((a + b) / 2 - Vector((c.x, c.y, a.z))), st["Holz"])
    netz.stab(spitze - Vector((0, 0, 0.2)), spitze + Vector((0, 0, 0.8)), 0.05, 0.05, st["Eisen"])
    netz.quader(spitze + Vector((0, 0, 0.55)), (0.18, 0.18, 0.18), st["Gold"])
    # Tür zum Weg mit Sandsteingewände, Stadtwappen darüber, Schießscharten
    vorne = bs.wandmatrix((c.x, c.y - h, 0), "vorne")
    tuer = bs.Oeffnung("tuer", 0.0, 0.0, 1.0, 2.1)
    bs.platte(netz, vorne, [(-0.49, 0.0), (0.49, 0.0), (0.49, 2.1), (-0.49, 2.1)], -0.005, 0.0, st["Holz alt"])
    for f in (0.25, 0.75):
        bs.platte(netz, vorne, [(-0.4, 2.1 * f), (0.4, 2.1 * f), (0.4, 2.1 * f + 0.07), (-0.4, 2.1 * f + 0.07)], -0.02, -0.005, st["Eisen"])
    for u0, u1, v0, v1 in ((-0.72, -0.5, 0, 2.1), (0.5, 0.72, 0, 2.1), (-0.72, 0.72, 2.1, 2.42)):
        bs.platte(netz, vorne, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], -0.06, 0.0, st["Sandstein"])
    del tuer
    schild = [(x * 0.9, y * 0.9) for x, y in bs.SCHILD]
    bs.platte(netz, vorne, [(u, 3.0 + v) for u, v in schild], -0.08, 0.0, st["Sandstein"])
    bs.platte(netz, vorne, [(u * 0.85, 3.07 + v * 0.85) for u, v in schild], -0.11, -0.08, st["Stadtrot"])
    for seite, punkt in (("links", (c.x - h, c.y, 0)), ("hinten", (c.x, c.y + h, 0)), ("rechts", (c.x + h, c.y, 0))):
        M = bs.wandmatrix(punkt, seite)
        for v in (1.6, 3.6):
            bs.platte(netz, M, [(-0.08, v), (0.08, v), (0.08, v + 0.7), (-0.08, v + 0.7)], -0.006, 0.0, st["Loch"])
    # Schlagbaum über den Weg: Drehpfosten hinter dem Weg, Gabel vorn, Baum rot-weiß mit Gegengewicht
    xb = 2.4
    lb.saeule(netz, st, (xb, -1.2, 0), 0.16, 0, 1.0, "Holz", n=6)
    lb.saeule(netz, st, (xb, -3.75, 0), 0.12, 0, 0.9, "Holz", n=6)
    for s in (-1, 1):
        netz.stab((xb + s * 0.1, -3.75, 0.85), (xb + s * 0.16, -3.75, 1.15), 0.06, 0.06, st["Holz"])
    teile = 6
    for i in range(teile):
        ya, yb = -1.0 - (3.85 - 1.0) * i / teile, -1.0 - (3.85 - 1.0) * (i + 1) / teile
        netz.stab((xb, ya, 1.08), (xb, yb, 1.08), 0.14, 0.14, st["Stadtrot" if i % 2 == 0 else "Putz"], ohne=("+x", "-x")
                  if 0 < i < teile - 1 else ())
    netz.stab((xb, -0.95, 1.08), (xb, -0.4, 1.05), 0.12, 0.12, st["Holz"])
    netz.quader((xb, -0.45, 0.95), (0.35, 0.35, 0.35), st["Bruchstein"])
    # Haspel und Kettenpfosten an der Kaimauer, die Kette hängt ins Wasser
    for s in (-1, 1):
        ws.kasten(netz, st, KETTE_X + s * 0.6 - 0.1, KETTE_X + s * 0.6 + 0.1, 2.3, 2.5, 0.0, 1.05, "Holz", ohne=("-z",))
    ws.walze(netz, st, 2.4, 0.85, 0.2, KETTE_X - 0.5, KETTE_X + 0.5, "Holz", n=8)
    ws.walze(netz, st, 2.4, 0.85, 0.23, KETTE_X - 0.25, KETTE_X + 0.25, "Eisen", n=8)
    netz.stab((KETTE_X + 0.7, 2.4, 0.85), (KETTE_X + 0.7, 2.4, 1.3), 0.06, 0.06, st["Eisen"])
    netz.stab((KETTE_X + 0.7, 2.4, 1.3), (KETTE_X + 0.7, 2.75, 1.3), 0.06, 0.06, st["Holz"])
    pfosten = Vector((KETTE_X, K_UFER - 0.35, 0))
    lb.saeule(netz, st, pfosten, 0.22, 0.0, 1.6, "Sandstein", n=8)
    netz.quader(pfosten + Vector((0, 0, 1.68)), (0.5, 0.5, 0.16), st["Sandstein"])
    kopf = pfosten + Vector((0, 0.05, 1.45))
    kettenglieder(netz, st, (KETTE_X, 2.62, 0.85), kopf)
    kettenglieder(netz, st, kopf, (KETTE_X, K_UFER + 1.0, 0.2))
    kettenglieder(netz, st, (KETTE_X, K_UFER + 1.0, 0.2), (KETTE_X, K_UFER + 2.6, WASSER - 0.6))
    # Bank und Zollkiste neben der Tür
    ws.kasten(netz, st, c.x + h + 0.1, c.x + h + 1.6, c.y - h - 0.45, c.y - h - 0.1, 0.4, 0.47, "Holz", ohne=("-z",))
    for x in (c.x + h + 0.25, c.x + h + 1.45):
        ws.kasten(netz, st, x - 0.05, x + 0.05, c.y - h - 0.4, c.y - h - 0.15, 0.0, 0.4, "Holz", ohne=("-z",))
    lb.kiste(netz, st, (c.x - 1.15, c.y - h - 0.3, 0.01), (0.7, 0.5, 0.5), stoff="Holz alt")
    netz.quader((c.x - 1.15, c.y - h - 0.56, 0.35), (0.12, 0.02, 0.14), st["Eisen"])
    return netz.objekt()


def zoll_bauen(st):
    return [zoll_haus(st), bs.anker("Anker_Tuer", (TURM.x, TURM.y - T_HALB - 0.3, 0), 0),
            bs.anker("Anker_Schlagbaum", (1.6, -2.6, 0), 270), bs.anker("Anker_Ufer", (0, K_UFER, 0), 0),
            bs.anker("Anker_Anleger", (KETTE_X - 1.2, K_UFER - 0.5, 0), 0)]


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau und Ausgabe
# ─────────────────────────────────────────────────────────────────────────────

def ufer_gelaende(versatz):
    """Ufer entlang X bei y = K_UFER (+ versatz), Wasser dahinter."""
    import bpy
    ws.gelaende([(-60, 0), (K_UFER, 0), (K_UFER + 0.01, -3.2), (60, -3.2)], (K_UFER, 60), achse="y")
    for n in ("_Gelaende", "_Wasser"):
        bpy.data.objects[n].location.y += versatz


def vorschau_schiff(objekte, schnell):
    import bpy
    ws.buehne(24.0, 2.5, schnell)
    ws.gelaende([(-60, -3.0), (60, -3.0)], (-60, 60, 0.0), achse="y")
    zeigen = {o for o in objekte if o.name != "Segel_Gerefft"} | {k for o in objekte for k in o.children_recursive}
    ws.stempel("Flussschiff: Segel gesetzt, Ladung 1–6, Laufplanke, Wimpel")
    a = bs.rendern("_wasser_schiff_a", lambda o: o in zeigen)
    zeigen = {o for o in objekte if o.name not in ("Segel_Gesetzt", "Fahne") and not o.name.startswith("Ladung_")}
    ws.stempel("im Hafen: Segel gerefft, leer")
    b = bs.rendern("_wasser_schiff_b", lambda o: o in zeigen, 2)
    del bpy
    return ws.nebeneinander("wasser_schiff", [a, b])


def vorschau_ufer(name, ausschnitt, ziel):
    def vorschau(objekte, schnell):
        ws.buehne(ausschnitt, ziel, schnell)
        ufer_gelaende(0.0)
        alles = set(objekte) | {k for o in objekte for k in o.children_recursive}
        return bs.rendern(name, lambda o: o in alles)
    return vorschau


def vorschau_land(name, ausschnitt, ziel):
    def vorschau(objekte, schnell):
        ws.buehne(ausschnitt, ziel, schnell)
        ws.gelaende([(-60, 0), (60, 0)], (100, 101))
        alles = set(objekte) | {k for o in objekte for k in o.children_recursive}
        return bs.rendern(name, lambda o: o in alles)
    return vorschau


DATEIEN = [
    ("wasser_schiff", schiff_bauen, vorschau_schiff),
    ("wasser_kran", kran_bauen, vorschau_ufer("wasser_kran", 16.0, 3.0)),
    ("wasser_schuppen", schuppen_bauen, vorschau_land("wasser_schuppen", 17.0, 3.0)),
    ("wasser_zoll", zoll_bauen, vorschau_ufer("wasser_zoll", 15.0, 3.5)),
]


def main():
    schnell = "--schnell" in sys.argv
    nur = [a.split("=", 1)[1] for a in sys.argv if a.startswith("--nur=")]
    for name, bauen, vorschau in DATEIEN:
        if nur and name not in nur:
            continue
        bs.neue_szene()
        st = bs.Stoffe()
        objekte = bauen(st)
        netze = [o for o in objekte if o.type == "MESH"]
        alle = objekte + [k for o in objekte for k in o.children_recursive if k not in objekte]
        bs.bericht(name, [o for o in alle if o.type == "MESH"] + [o for o in alle if o.type != "MESH"], einzeln=True)
        vorher, nachher = bs.farben_zusammenlegen([o for o in alle if o.type == "MESH"], behalten=BEHALTEN)
        print(f"  Materialien: {vorher} → {nachher}")
        print("geschrieben:", bs.exportieren(name, objekte))
        if "--ohne-bilder" not in sys.argv:
            print(vorschau(netze + [o for o in objekte if o.type != "MESH"], schnell))


if __name__ == "__main__":
    main()
