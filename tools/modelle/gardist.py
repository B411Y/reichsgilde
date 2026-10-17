"""Werkstatt des Gardisten – Wachhaus (Stufe 1), Stadtwache (Stufe 2), Garnison (Stufe 3). Fläche 16 × 14 m.

    blender -b --python tools/modelle/gardist.py        oder
    python tools/modelle/gardist.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gardist.glb und Bilder nach docs/modelle/.

Objekte im GLB (wie bei den anderen Werkstätten):
    Haus        die Kaserne mit Torhaus und Wehrturm, in jeder Stufe gleich
    Stufe1      Wachhaus: Kasernenhof hinter einer hohen Mauer, Pranger, Feuerkorb, Bank, Trog, Brennholz
    Stufe2      Stadtwache: dazu ein Exerzierplatz mit Strohpuppen, Zielscheiben, Waffen- und Rüstungsständer, Gaube
    Stufe3      Garnison: Mannschaftshaus im Hof, Fahnenmast mit der Stadtfahne, Übungsplatz, Gaube,
                Dachreiter mit stadtroter Spitze
    Wappen, Fahne, Leerstand, Geruest, Anker_Tuer, Anker_Karren
Genau einer der Knoten Stufe1–Stufe3 ist sichtbar; Leerstand und Geruest sind standardmäßig aus.
Das Material „Glut“ (Feuerkorb im Hof) leuchtet – das Spiel regelt die Stärke wie bei der Esse.

Aufbau (Blender: Z oben, Gasse bei -Y; Fläche x ±8, y ±7, davon 2 m Vorplatz vorn):
    Kaserne links (8,4 × 5,6 m), massiv wie bisher im Spiel: zwei Geschosse aus Bruchstein mit Eckquadern und
    Gesims, Ziegeldach mit der Traufe zur Gasse, Tordurchfahrt in den Kasernenhof mit dem Stadtwappen darüber
    (wie bisher im Spiel), vergitterte Fenster der Arrestzellen. Vorn rechts ein Wehrturm mit Schießscharten und
    Zinnen, darauf die Fahne des Hauses; davor Speergestell und Schilderhäuschen in den Stadtfarben (wie bisher im Spiel).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "gardist"
FLAECHE = (16.0, 14.0)
STUFEN = ("Wachhaus", "Stadtwache", "Garnison")

# ── Maße der Kaserne ─────────────────────────────────────────────────────────
X0, X1, Y0, Y1 = -7.4, 1.0, -5.0, 0.6
CX, CY = (X0 + X1) / 2, (Y0 + Y1) / 2
Z_SOCKEL, H_EG, H_OG = 0.3, 3.0, 2.8
H_WAND = H_EG + H_OG
Z_TRAUFE = Z_SOCKEL + H_WAND               # 6,1
NEIGUNG = 48
TX0, TX1, TY0, TY1 = -1.7, 1.3, -5.3, -2.3  # Wehrturm, steht 0,3 m vor der Flucht
H_TURM = 10.2


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []

    # ── Kaserne: Sockel, Mauerkörper, Gesims, Eckquader ──────────────────────
    haus.quader((CX, CY, Z_SOCKEL / 2), (X1 - X0 + 0.12, Y1 - Y0 + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    haus.quader((CX, CY, Z_SOCKEL + H_WAND / 2), (X1 - X0, Y1 - Y0, H_WAND), st["Bruchstein"], ohne=("+z",))
    bs.gesims(haus, st, X0, X1, Y0, Y1, Z_SOCKEL + H_EG, hoehe=0.16, vor=0.07)
    for sx, x, sy, y in ((-1, X0, -1, Y0), (-1, X0, 1, Y1), (1, X1, 1, Y1)):
        bs.eckquader(haus, st, x, y, sx, sy, Z_SOCKEL, Z_TRAUFE)
    M = {"vorn": bs.wandmatrix((CX, Y0, Z_SOCKEL), "vorne"), "links": bs.wandmatrix((X0, CY, Z_SOCKEL), "links"),
         "hinten": bs.wandmatrix((CX, Y1, Z_SOCKEL), "hinten"), "rechts": bs.wandmatrix((X1, CY, Z_SOCKEL), "rechts")}
    for name, L in (("vorn", X1 - X0), ("links", Y1 - Y0), ("hinten", X1 - X0), ("rechts", Y1 - Y0)):
        bs.fugen(haus, M[name], L - 0.02, 0.0, H_WAND, st)

    F = "fenster"
    eg = dict(v=1.0, b=0.6, h=0.9)                      # Erdgeschoss: kleine vergitterte Fenster (Arrest, Wachstube)
    og = dict(v=H_EG + 0.6, b=0.8, h=1.2)

    def fenster(name, liste, gitter=False):
        for o in liste:
            bs.gewaende(haus, M[name], o, st, gitter=gitter)
            oeffnungen.append((M[name], o))

    tor = Oeffnung("tuer", -1.4, 0.0, 2.6, 3.0)
    bs.portal(haus, M["vorn"], tor, st)
    oeffnungen.append((M["vorn"], tor))
    rampe = [(Y0 - 1.0, 0.0), (Y0, 0.0), (Y0, Z_SOCKEL + 0.01), (Y0 - 0.25, Z_SOCKEL + 0.01)]   # 1 cm über dem Sockel
    haus.prisma(rampe, CX + tor.u - 1.5, CX + tor.u + 1.5, st["Bruchstein dunkel"])
    stadtwappen(haus, st, M["vorn"], tor.u, H_EG + 1.25)
    fenster("vorn", [Oeffnung(F, 0.8, **eg)], gitter=True)
    fenster("vorn", [Oeffnung(F, u, **og) for u in (-2.9, -0.35, 0.85)])
    fenster("links", [Oeffnung(F, u, **eg) for u in (-1.4, 1.4)], gitter=True)
    fenster("links", [Oeffnung(F, u, **og) for u in (-1.4, 1.4)])
    fenster("hinten", [Oeffnung(F, -2.6, **eg)], gitter=True)
    fenster("hinten", [Oeffnung(F, u, **og) for u in (-2.8, -0.6, 2.6)])
    fenster("rechts", [Oeffnung(F, 1.6, **og)])
    # Rückseite der Tordurchfahrt: offener Bogen in den Hof
    durchfahrt = Oeffnung("tuer", 1.4, 0.0, 2.6, 3.0)
    hinterbogen(haus, st, M["hinten"], durchfahrt)

    # ── Ziegeldach mit der Traufe zur Gasse, steinerne Giebel, Schornstein ────
    dach = bs.satteldach(haus, st, X0, X1, Y0, Y1, Z_TRAUFE, NEIGUNG, traufe=0.25, ortgang=0.25, **bs.ZIEGEL)
    hoch = (Y1 - Y0) / 2 * dach["tan"]
    for x, seite in ((X0, "links"), (X1, "rechts")):
        bs.steingiebel(haus, st, bs.wandmatrix((x, CY, Z_TRAUFE), seite), Y1 - Y0, hoch, oeffnungen)
    xs, ys = X0 + 1.6, CY + 1.2
    z0 = dach["oben_hinten"](ys) - 0.3
    haus.quader((xs, ys, (z0 + dach["z_first"] + 0.7) / 2), (0.6, 0.6, dach["z_first"] + 0.7 - z0), st["Bruchstein dunkel"])
    haus.quader((xs, ys, dach["z_first"] + 0.76), (0.78, 0.78, 0.12), st["Sandstein"])

    # ── Wehrturm vorn rechts, Speergestell, Schilderhäuschen ──────────────────
    turm_tuer = wehrturm(haus, st, oeffnungen)
    speergestell(haus, st, (0.6, Y0 - 0.85, 0))
    schilderhaus(haus, st, (2.4, Y0 - 0.75, 0))

    # ── Wappen des Hauses am Turm, Fahne auf dem Turm ─────────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, bs.wandmatrix(((TX0 + TX1) / 2, TY0, 0.0), "vorne"), turm_tuer.u, 3.3, st, groesse=0.65, rahmen="Sandstein")
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, ((TX0 + TX1) / 2, (TY0 + TY1) / 2, H_TURM), hoehe=2.6, richtung=1)

    # ── Stufen ────────────────────────────────────────────────────────────────
    stufe1 = bs.Netz("Stufe1")
    wachhaus(stufe1, st)
    stufe2 = bs.Netz("Stufe2")
    stadtwache(stufe2, st)
    bs.gaube(stufe2, st, dach, [], x0=X0 + 0.9, x1=X0 + 2.3, y_front=Y0 + 1.0, art="stein", koerper="Bruchstein")
    stufe3 = bs.Netz("Stufe3")
    garnison(stufe3, st)
    bs.gaube(stufe3, st, dach, [], x0=X0 + 0.9, x1=X0 + 2.3, y_front=Y0 + 1.0, art="stein", koerper="Bruchstein")
    bs.dachreiter(stufe3, st, Vector((CX - 1.0, dach["y_first"], dach["z_first"])), spitze="Spitze rot",
                  koerper="Bruchstein", ecken="Sandstein")

    # ── Leerstand, Gerüst, Anker ──────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (Mo, o) in enumerate(oeffnungen):
        bs.bretter(leer, Mo, o, st, i)
    for x, y in ((X0 + 0.3, Y0 - 0.15), (-2.6, Y0 - 1.3), (TX1 + 0.15, -3.0), (X0 - 0.15, -1.0), (X1 + 0.15, 0.3)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=X0 - 0.15, x1=TX0 - 0.15, y_innen=Y0 - 0.32, y_aussen=Y0 - 1.2,
                  ebenen=(2.4, 4.8), hoehe_innen=5.6, hoehe_aussen=6.6, felder=3)
    anker = [bs.anker("Anker_Tuer", (CX + tor.u, Y0 - 0.2, Z_SOCKEL + 0.01)),
             bs.anker("Anker_Karren", (CX + tor.u, Y0 - 1.6, 0.0))]

    objekte = [haus.objekt(), stufe1.objekt(), stufe2.objekt(), stufe3.objekt(), wappen.objekt(), fahne.objekt(),
               leer.objekt(), geruest.objekt()] + anker
    for o in objekte:
        if o.name in ("Stufe2", "Stufe3", "Leerstand", "Geruest"):
            bs.zustand(o)
        elif o.name == "Stufe1":
            o["zustand"] = True
    return objekte


# ─────────────────────────────────────────────────────────────────────────────
#  Teile der Kaserne
# ─────────────────────────────────────────────────────────────────────────────

def stadtwappen(netz, st, M, u, v, g=0.85):
    """Stadtwappen über dem Tor (wie bisher im Spiel): Schild in Stadtweiß, die obere Hälfte rot, Rahmen aus Sandstein."""
    schild = [(u + x * g, v + y * g) for x, y in bs.SCHILD]
    bs.platte(netz, M, [(u + x * 1.14 * g, v + y * 1.1 * g) for x, y in bs.SCHILD], -0.07, -0.0, st["Sandstein"])
    bs.platte(netz, M, schild, -0.1, -0.07, st["Stadtweiss"])
    bs.platte(netz, M, [(u - 0.34 * g, v + 0.08 * g), (u + 0.34 * g, v + 0.08 * g), (u + 0.34 * g, v + 0.42 * g),
                        (u - 0.34 * g, v + 0.42 * g)], -0.12, -0.1, st["Stadtrot"])


def hinterbogen(netz, st, M, o, g=0.2, teile=8):
    """Rückseite der Tordurchfahrt: dunkler Rundbogen ohne Torflügel, Bogen aus Sandstein."""
    r = o.b / 2
    k = o.v + o.h - r
    bogen = [(o.u + r * math.cos(math.pi * i / teile), k + r * math.sin(math.pi * i / teile)) for i in range(teile + 1)]
    netz.vieleck([M @ Vector((p[0], -0.006, p[1])) for p in [(o.u - r, o.v), (o.u + r, o.v)] + bogen], st["Loch"])
    aussen = [(o.u + (r + g) * math.cos(math.pi * i / teile), k + (r + g) * math.sin(math.pi * i / teile)) for i in range(teile + 1)]
    ring = [(o.u + r + g, o.v)] + aussen + [(o.u - r - g, o.v), (o.u - r, o.v)] + list(reversed(bogen)) + [(o.u + r, o.v)]
    bs.platte(netz, M, ring, -0.06, 0.0, st["Sandstein"])


def schiessscharte(netz, st, M, u, v, h=0.85):
    """Schießscharte: schmaler dunkler Schlitz mit Sandsteinrahmen, unten ein kurzer Querschlitz."""
    for a, b, c, d in ((u - 0.05, v, u + 0.05, v + h), (u - 0.18, v + 0.2, u + 0.18, v + 0.3)):
        netz.vieleck([M @ Vector(p) for p in ((a, -0.006, b), (c, -0.006, b), (c, -0.006, d), (a, -0.006, d))], st["Loch"])
    for du in (-0.15, 0.15):
        netz.quader((u + du, -0.03, v + h / 2), (0.1, 0.06, h + 0.2), st["Sandstein"], M, ohne=("+y",))
    for dv in (-0.05, h + 0.05):
        netz.quader((u, -0.03, v + dv), (0.4, 0.06, 0.1), st["Sandstein"], M, ohne=("+y",))


def zinnen(netz, st, x0, x1, y0, y1, z, dicke=0.35, sockel=0.4, h=0.6, breite=0.55):
    """Zinnenkranz auf einer Plattform: Brüstung rundum, darauf Zinnen mit Lücken (Ecken jeweils mit einer Zinne)."""
    stein = st["Bruchstein"]
    netz.quader(((x0 + x1) / 2, y0 + dicke / 2, z + sockel / 2), (x1 - x0, dicke, sockel), stein)
    netz.quader(((x0 + x1) / 2, y1 - dicke / 2, z + sockel / 2), (x1 - x0, dicke, sockel), stein)
    for x in (x0 + dicke / 2, x1 - dicke / 2):
        netz.quader((x, (y0 + y1) / 2, z + sockel / 2), (dicke, y1 - y0 - 2 * dicke, sockel), stein)
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, z + sockel + 0.03), (x1 - x0 + 0.08, y1 - y0 + 0.08, 0.06), st["Sandstein"],
                ohne=("-z",))
    zs = z + sockel + 0.06

    def reihe(a, b, n):
        return [a + (b - a) * k / (n - 1) for k in range(n)]

    xs = reihe(x0 + breite / 2, x1 - breite / 2, 4)
    ys = reihe(y0 + breite / 2, y1 - breite / 2, 4)
    for x in xs:
        for y in (y0 + dicke / 2, y1 - dicke / 2):
            netz.quader((x, y, zs + h / 2), (breite, dicke, h), stein)
    for y in ys[1:-1]:
        for x in (x0 + dicke / 2, x1 - dicke / 2):
            netz.quader((x, y, zs + h / 2), (dicke, breite, h), stein)


def wehrturm(netz, st, oeffnungen):
    """Wehrturm vorn rechts: Bruchstein mit Eckquadern und Gesimsen, Tür zur Wachstube, Schießscharten,
    oben eine Plattform mit Zinnen. Gibt die Tür zurück (für das Wappen darüber)."""
    cx, cy = (TX0 + TX1) / 2, (TY0 + TY1) / 2
    b = TX1 - TX0
    netz.quader((cx, cy, 0.2), (b + 0.12, b + 0.12, 0.4), st["Bruchstein dunkel"])
    netz.quader((cx, cy, 0.4 + (H_TURM - 0.4) / 2), (b, b, H_TURM - 0.4), st["Bruchstein"])
    for z, hoch in ((Z_TRAUFE, 0.16), (H_TURM - 0.32, 0.22)):      # oberes Gesims endet unter der Plattform
        bs.gesims(netz, st, TX0, TX1, TY0, TY1, z, hoehe=hoch, vor=0.08)
    for sx, x in ((-1, TX0), (1, TX1)):
        for sy, y in ((-1, TY0), (1, TY1)):
            bs.eckquader(netz, st, x, y, sx, sy, 0.4, H_TURM - 0.22)
    M_v = bs.wandmatrix((cx, TY0, 0.0), "vorne")
    M_r = bs.wandmatrix((TX1, cy, 0.0), "rechts")
    M_l = bs.wandmatrix((TX0, cy, 0.0), "links")
    M_h = bs.wandmatrix((cx, TY1, 0.0), "hinten")
    bs.fugen(netz, M_v, b - 0.02, 0.4, H_TURM - 0.22, st)
    bs.fugen(netz, M_r, b - 0.02, 0.4, H_TURM - 0.22, st)
    tuer = Oeffnung("tuer", -0.5, 0.4, 1.0, 2.1)
    bs.tuer(netz, M_v, tuer, st)
    netz.quader((tuer.u, -0.04, tuer.v + tuer.h + 0.12), (1.4, 0.1, 0.24), st["Sandstein"], M_v, ohne=("+y",))
    netz.quader((cx + tuer.u, TY0 - 0.2, 0.205), (1.3, 0.4, 0.41), st["Sandstein"])
    oeffnungen.append((M_v, tuer))
    for Mw, liste in ((M_v, [(0.0, 4.4), (0.0, 7.6)]), (M_r, [(0.0, 2.0), (-0.6, 4.4), (0.6, 7.6)]),
                      (M_l, [(0.0, 7.6)]), (M_h, [(0.0, 7.6)])):
        for u, v in liste:
            schiessscharte(netz, st, Mw, u, v)
    zinnen(netz, st, TX0 - 0.08, TX1 + 0.08, TY0 - 0.08, TY1 + 0.08, H_TURM)
    return tuer


def speergestell(netz, st, mitte, n=5):
    """Speergestell (wie bisher im Spiel vor dem Tor): Querbalken auf zwei Pfosten, Speere mit Eisenspitzen."""
    x, y = mitte[0], mitte[1]
    for dx in (-0.95, 0.95):
        netz.quader((x + dx, y, 0.5), (0.1, 0.1, 1.0), st["Holz alt"])
    netz.quader((x, y, 0.95), (2.1, 0.12, 0.1), st["Holz"])
    netz.quader((x, y - 0.05, 0.08), (2.0, 0.3, 0.06), st["Holz alt"])
    for k in range(n):
        px = x - 0.8 + 1.6 * k / (n - 1)
        netz.stab((px, y - 0.08, 0.1), (px, y + 0.05, 2.7), 0.05, 0.05, st["Holz"])
        netz.prisma([(-0.06, 0.0), (0.06, 0.0), (0.0, 0.35)], -0.02, 0.02, st["Eisen"],
                    matrix=Matrix.Translation((px, y + 0.055, 2.7)) @ Matrix.Rotation(math.radians(90), 4, "Z"))


def schilderhaus(netz, st, fuss):
    """Schilderhäuschen in den Stadtfarben (wie bisher im Spiel): rot-weiß gestreifte Bretter, Schieferhaube,
    dunkler Eingang, daneben ein Mast mit roter Fahne."""
    x, y = fuss[0], fuss[1]
    b, t, h = 1.1, 1.0, 2.3
    netz.quader((x, y, 0.05), (b + 0.1, t + 0.1, 0.1), st["Bruchstein dunkel"])
    for k in range(5):
        z0 = 0.1 + (h - 0.1) * k / 5
        netz.quader((x, y, z0 + (h - 0.1) / 10), (b, t, (h - 0.1) / 5), st["Stadtrot" if k % 2 == 0 else "Stadtweiss"],
                    ohne=("+z", "-z"))
    netz.vieleck_nach([(x - 0.3, y - t / 2 - 0.004, 0.15), (x + 0.3, y - t / 2 - 0.004, 0.15), (x + 0.3, y - t / 2 - 0.004, 1.95),
                       (x - 0.3, y - t / 2 - 0.004, 1.95)], (0, -1, 0), st["Loch"])
    gipfel = Vector((x, y, h + 0.8))
    ecken = [Vector((x + sx * (b / 2 + 0.15), y + sy * (t / 2 + 0.15), h)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        p, q = ecken[i], ecken[(i + 1) % 4]
        netz.vieleck_nach([p, q, gipfel], (p + q) / 2 - Vector((x, y, h)), st["Schiefer"])
    netz.vieleck_nach(ecken, (0, 0, -1), st["Schiefer"])
    mx = x + b / 2 + 0.3
    netz.stab((mx, y, 0), (mx, y, 3.6), 0.07, 0.07, st["Holz"])
    netz.prisma([(0, 0), (0.85, 0.04), (0.85, 0.52), (0, 0.55)], -0.012, 0.012, st["Stadtrot"],
                matrix=Matrix.Translation((mx + 0.04, y, 2.95)) @ Matrix.Rotation(math.radians(-90), 4, "Z"))


def pranger(netz, st, fuss):
    """Pranger auf einem Steinpodest: Pfahl mit Halsgeige (Brett mit drei Löchern), zwei Stufen."""
    x, y = fuss[0], fuss[1]
    netz.quader((x, y, 0.15), (1.6, 1.6, 0.3), st["Bruchstein"])
    netz.quader((x, y - 1.0, 0.075), (1.0, 0.4, 0.15), st["Bruchstein dunkel"])
    netz.quader((x, y, 1.4), (0.18, 0.18, 2.2), st["Holz alt"])
    netz.quader((x, y - 0.12, 1.85), (1.1, 0.08, 0.3), st["Holz"])
    for dx, r in ((-0.35, 0.05), (0.0, 0.09), (0.35, 0.05)):
        netz.vieleck_nach([(x + dx - r, y - 0.162, 1.85 - r), (x + dx + r, y - 0.162, 1.85 - r), (x + dx + r, y - 0.162, 1.85 + r),
                           (x + dx - r, y - 0.162, 1.85 + r)], (0, -1, 0), st["Loch"])
    netz.quader((x, y, 2.55), (0.3, 0.3, 0.1), st["Eisen"])


def feuerkorb(netz, st, fuss):
    """Feuerkorb auf einem Eisenpfahl (Kohlebecken der Wache); die Glut schaltet das Spiel wie die Esse."""
    m = Vector(fuss)
    netz.stab(m, m + Vector((0, 0, 1.0)), 0.08, 0.08, st["Eisen"])
    for w in (0, 120, 240):
        a = m + Vector((0.4 * math.cos(math.radians(w)), 0.4 * math.sin(math.radians(w)), 0))
        netz.stab(a, m + Vector((0, 0, 0.5)), 0.05, 0.05, st["Eisen"])
    bs.achteck(netz, st, m + Vector((0, 0, 1.0)), 0.35, 0.0, 0.32, "Eisen")
    bs.rund_oben(netz, st, m, 0.3, 1.325, "Glut")


def kaefig(netz, st, fuss):
    """Eiserner Käfig auf einem Steinsockel für gefasste Taschendiebe: Gitterstäbe, Deckel, Tür mit Schloss."""
    x, y = fuss[0], fuss[1]
    b, h = 1.5, 2.0
    netz.quader((x, y, 0.15), (b + 0.2, b + 0.2, 0.3), st["Bruchstein dunkel"])
    for i in range(6):
        d = -b / 2 + b * i / 5
        for px, py in ((x + d, y - b / 2), (x + d, y + b / 2)):
            netz.stab((px, py, 0.3), (px, py, 0.3 + h), 0.04, 0.04, st["Eisen"])
        if 0 < i < 5:
            for px, py in ((x - b / 2, y + d), (x + b / 2, y + d)):
                netz.stab((px, py, 0.3), (px, py, 0.3 + h), 0.04, 0.04, st["Eisen"])
    netz.quader((x, y, 0.3 + h + 0.04), (b + 0.08, b + 0.08, 0.08), st["Eisen"])
    for z in (0.35, 0.3 + h * 0.5):
        for yy in (y - b / 2, y + b / 2):
            netz.stab((x - b / 2 - 0.02, yy, z), (x + b / 2 + 0.02, yy, z), 0.06, 0.06, st["Eisen"], quer=(0, 0, 1))
    netz.quader((x + 0.2, y - b / 2 - 0.05, 1.2), (0.12, 0.06, 0.16), st["Eisen"])
    bs.achteck(netz, st, Vector((x - 0.1, y + 0.2, 0.3)), 0.22, 0.0, 0.5, "Lumpen", n=6)     # zusammengekauerter Gefangener
    bs.achteck(netz, st, Vector((x - 0.1, y + 0.2, 0.8)), 0.12, 0.0, 0.2, "Leder", n=6)


def bank_mit_faessern(netz, st, mitte, dreh=0.0):
    """Bank an der Hofmauer, daneben Fässer (Wasser, Bier) – hier würfeln die Wachleute."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, 0.45), (2.0, 0.35, 0.07), st["Holz"], t)
    for dx in (-0.8, 0.8):
        netz.quader((dx, 0, 0.21), (0.1, 0.3, 0.42), st["Holz alt"], t)
    for k, dx in enumerate((1.5, 2.2)):
        bs.fass(netz, st, t @ Vector((dx, 0.05 * k, 0)), r=0.32, h=0.9)


def trog(netz, st, mitte, dreh=0.0):
    """Wassertrog aus Stein."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, 0.35), (1.8, 0.7, 0.7), st["Sandstein"], t)
    netz.quader((0, 0, 0.705), (1.6, 0.5, 0.01), st["Wasser"], t, ohne=("-z",))


def brennholz(netz, st, mitte, laenge=1.8, lagen=4, dreh=0.0):
    """Gestapelte Scheite unter einem Brett."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for k in range(lagen):
        for j in range(4 - k % 2):
            netz.quader((-laenge / 2 + laenge * (j + 0.5 + 0.5 * (k % 2)) / 4, 0, 0.09 + 0.17 * k), (0.4, 0.5, 0.16), st["Holz"], t)
    netz.quader((0, 0, 0.09 + 0.17 * lagen + 0.03), (laenge + 0.2, 0.7, 0.04), st["Holz alt"], t)


def strohpuppe(netz, st, fuss, dreh=0.0):
    """Strohpuppe zum Üben: Pfahl mit Querholz, Leib und Kopf aus Stroh, ein roter Schild am Arm."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, 0.9), (0.1, 0.1, 1.8), st["Holz alt"], t)
    netz.quader((0, 0, 1.4), (1.0, 0.08, 0.08), st["Holz alt"], t)
    bs.achteck(netz, st, t @ Vector((0, 0, 0.85)), 0.22, 0.0, 0.7, "Stroh", n=6)
    bs.achteck(netz, st, t @ Vector((0, 0, 1.6)), 0.14, 0.0, 0.28, "Stroh", n=6)
    netz.quader((0.5, -0.06, 1.25), (0.08, 0.4, 0.5), st["Stadtrot"], t)


def zielscheibe(netz, st, fuss, dreh=0.0):
    """Zielscheibe: runde Strohscheibe mit roten und weißen Ringen auf einem schrägen Holzgestell, Pfeile darin."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    neig = t @ Matrix.Translation((0, 0, 1.1)) @ Matrix.Rotation(math.radians(-12), 4, "X")
    for s in (-1, 1):
        netz.stab(t @ Vector((s * 0.45, 0.0, 0)), t @ Vector((s * 0.35, 0.15, 1.6)), 0.07, 0.07, st["Holz alt"])
    netz.stab(t @ Vector((0, 0.7, 0)), t @ Vector((0, 0.12, 1.4)), 0.07, 0.07, st["Holz alt"])
    for r, y, stoff in ((0.42, -0.012, "Stadtrot"), (0.26, -0.016, "Stadtweiss"), (0.1, -0.02, "Stadtrot")):
        n = 12
        netz.vieleck_nach([neig @ Vector((r * math.cos(2 * math.pi * k / n), y, r * math.sin(2 * math.pi * k / n))) for k in range(n)],
                          neig.to_3x3() @ Vector((0, -1, 0)), st[stoff])
    profil = [(0.6 * math.cos(2 * math.pi * k / 12), 0.6 * math.sin(2 * math.pi * k / 12)) for k in range(12)]
    netz.prisma(profil, 0.0, 0.12, st["Stroh"], matrix=neig @ Matrix.Rotation(math.radians(90), 4, "Z"))   # Scheibe, Dicke nach hinten
    for dx, dz in ((0.12, 0.05), (-0.2, 0.18)):
        netz.stab(neig @ Vector((dx, -0.02, dz)), neig @ Vector((dx + 0.05, -0.55, dz + 0.03)), 0.02, 0.02, st["Holz"])


def waffenstaender(netz, st, mitte, dreh=0.0, n=6):
    """Waffenständer: Gestell mit Hellebarden (Speer mit Beil) und zwei Schilden."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for dx in (-1.0, 1.0):
        netz.quader((dx, 0, 0.75), (0.1, 0.1, 1.5), st["Holz alt"], t)
    for z in (0.3, 1.4):
        netz.quader((0, 0, z), (2.1, 0.1, 0.08), st["Holz"], t)
    for k in range(n):
        x = -0.8 + 1.6 * k / (n - 1)
        netz.stab(t @ Vector((x, -0.08, 0.05)), t @ Vector((x, 0.06, 2.5)), 0.05, 0.05, st["Holz"])
        netz.quader(t @ Vector((x + 0.1, 0.06, 2.25)), (0.22, 0.03, 0.26), st["Eisen"])
        netz.quader(t @ Vector((x, 0.07, 2.55)), (0.04, 0.03, 0.2), st["Eisen"])
    for dx in (-0.55, 0.55):
        netz.quader((dx, -0.12, 0.85), (0.5, 0.05, 0.65), st["Stadtrot"], t)
        netz.quader((dx, -0.15, 0.85), (0.08, 0.02, 0.5), st["Stadtweiss"], t)


def ruestungsstaender(netz, st, fuss, dreh=0.0):
    """Rüstungsständer: Pfahl mit Brustpanzer, Helm und Schild."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, 0.05), (0.6, 0.6, 0.1), st["Holz alt"], t)
    netz.quader((0, 0, 0.85), (0.08, 0.08, 1.5), st["Holz alt"], t)
    netz.prisma([(-0.24, 1.0), (0.24, 1.0), (0.3, 1.5), (-0.3, 1.5)], -0.14, 0.14, st["Eisen"],
                matrix=t @ Matrix.Rotation(math.radians(90), 4, "Z"))
    bs.achteck(netz, st, t @ Vector((0, 0, 1.6)), 0.15, 0.0, 0.2, "Eisen", n=8)
    bs.achteck(netz, st, t @ Vector((0, 0, 1.6)), 0.22, 0.0, 0.03, "Eisen", n=8)
    netz.quader((0.32, -0.05, 0.9), (0.06, 0.45, 0.6), st["Stadtrot"], t)


def fahnenmast(netz, st, fuss, hoehe=7.0):
    """Fahnenmast mit großer Stadtfahne (rot-weiß geteilt) auf einem Steinsockel."""
    m = Vector(fuss)
    netz.quader(m + Vector((0, 0, 0.25)), (0.8, 0.8, 0.5), st["Sandstein"])
    netz.stab(m + Vector((0, 0, 0.5)), m + Vector((0, 0, hoehe)), 0.12, 0.12, st["Holz"])
    netz.quader(m + Vector((0, 0, hoehe + 0.08)), (0.2, 0.2, 0.2), st["Gold"])
    dreh = Matrix.Translation(m + Vector((-0.07, 0, hoehe - 1.3))) @ Matrix.Rotation(math.radians(90), 4, "Z")   # weht nach -x
    netz.prisma([(0, 0.6), (1.7, 0.62), (1.7, 1.2), (0, 1.2)], -0.015, 0.015, st["Stadtrot"], matrix=dreh)
    netz.prisma([(0, 0.0), (1.7, 0.02), (1.7, 0.59), (0, 0.59)], -0.015, 0.015, st["Stadtweiss"], matrix=dreh)


def mannschaftshaus(netz, st, x0=1.6, x1=7.5, y0=3.2, y1=6.4):
    """Mannschaftshaus der Garnison: eingeschossig mit Kniestock, Bruchstein, Ziegeldach (First entlang x),
    Tür und vergitterte Fenster zum Hof, steinerner Giebel nach rechts."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    h = 3.6
    netz.quader((cx, cy, 0.15), (x1 - x0 + 0.1, y1 - y0 + 0.1, 0.3), st["Bruchstein dunkel"])
    netz.quader((cx, cy, 0.3 + h / 2), (x1 - x0, y1 - y0, h), st["Bruchstein"], ohne=("+z",))
    M_v = bs.wandmatrix((cx, y0, 0.3), "vorne")
    M_r = bs.wandmatrix((x1, cy, 0.3), "rechts")
    bs.fugen(netz, M_v, x1 - x0 - 0.02, 0.0, h, st)
    bs.fugen(netz, M_r, y1 - y0 - 0.02, 0.0, h, st)
    for sx, x in ((-1, x0), (1, x1)):
        bs.eckquader(netz, st, x, y0, sx, -1, 0.3, 0.3 + h)
    bs.eckquader(netz, st, x1, y1, 1, 1, 0.3, 0.3 + h)
    tuer = Oeffnung("tuer", -0.4, 0.0, 1.1, 2.2)
    bs.tuer(netz, M_v, tuer, st)
    netz.quader((tuer.u, -0.04, tuer.h + 0.12), (1.5, 0.1, 0.24), st["Sandstein"], M_v, ohne=("+y",))
    netz.quader((cx + tuer.u, y0 - 0.2, 0.155), (1.4, 0.4, 0.31), st["Sandstein"])
    for u in (-2.2, 1.2, 2.3):
        bs.gewaende(netz, M_v, Oeffnung("fenster", u, 1.0, 0.7, 1.1), st)
    bs.gewaende(netz, M_r, Oeffnung("fenster", 0.0, 1.0, 0.7, 1.1), st)
    dach = bs.satteldach(netz, st, x0, x1, y0, y1, 0.3 + h, 45, traufe=0.25, ortgang=0.2, **bs.ZIEGEL)
    bs.steingiebel(netz, st, bs.wandmatrix((x1, cy, 0.3 + h), "rechts"), y1 - y0, (y1 - y0) / 2 * dach["tan"], [])
    bs.gaube(netz, st, dict(dach), [], x0=cx - 1.2, x1=cx + 0.2, y_front=y0 + 0.6, hoehe=1.2, art="stein", koerper="Bruchstein")


def hofmauer(netz, st, bis=6.7):
    """Hohe Hofmauer aus Bruchstein mit Sandsteinabdeckung rund um den Kasernenhof."""
    bs.trockenmauer(netz, st, [(TX1, -4.75), (7.7, -4.75), (7.7, bis), (-7.7, bis), (-7.7, Y1 + 0.3)], hoehe=1.9, dicke=0.45)


# ─────────────────────────────────────────────────────────────────────────────
#  Stufen
# ─────────────────────────────────────────────────────────────────────────────

def wachhaus(netz, st):
    """Stufe 1: Kasernenhof hinter der Mauer mit Pranger, Feuerkorb, Bank mit Fässern, Trog und Brennholz."""
    pranger(netz, st, (3.2, -1.8))
    feuerkorb(netz, st, (5.4, -2.2, 0))
    bank_mit_faessern(netz, st, (3.6, 6.2, 0))
    trog(netz, st, (7.0, 1.6, 0), dreh=90)
    kaefig(netz, st, (6.4, 4.2))
    brennholz(netz, st, (-6.6, 4.4, 0), dreh=90)
    hofmauer(netz, st)


def stadtwache(netz, st):
    """Stufe 2: dazu ein Exerzierplatz mit Strohpuppen, Zielscheiben, Waffen- und Rüstungsständer."""
    pranger(netz, st, (3.2, -1.8))
    feuerkorb(netz, st, (5.4, -2.2, 0))
    for x in (3.4, 5.0):
        strohpuppe(netz, st, (x, 1.6, 0), dreh=10 * x)
    for x in (3.2, 5.4):
        zielscheibe(netz, st, (x, 5.6, 0))
    waffenstaender(netz, st, (7.0, -1.4, 0), dreh=90)
    ruestungsstaender(netz, st, (6.9, -3.6, 0), dreh=90)
    trog(netz, st, (7.0, 2.2, 0), dreh=90)
    kaefig(netz, st, (6.6, 4.4))
    brennholz(netz, st, (-6.6, 4.4, 0), dreh=90)
    hofmauer(netz, st)


def garnison(netz, st):
    """Stufe 3: Mannschaftshaus im hinteren Hof, Fahnenmast mit Stadtfahne, Übungsplatz, Pranger, Feuerkorb."""
    mannschaftshaus(netz, st)
    fahnenmast(netz, st, (6.4, -3.2, 0))
    pranger(netz, st, (3.2, -1.8))
    feuerkorb(netz, st, (4.8, -1.0, 0))
    strohpuppe(netz, st, (3.4, 1.6, 0), dreh=20)
    zielscheibe(netz, st, (5.6, 2.0, 0), dreh=-20)
    waffenstaender(netz, st, (7.1, 0.6, 0), dreh=90)
    ruestungsstaender(netz, st, (7.0, -1.4, 0), dreh=90)
    brennholz(netz, st, (-6.6, 4.4, 0), dreh=90)
    hofmauer(netz, st)


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    zustaende = ("Stufe1", "Stufe2", "Stufe3", "Leerstand", "Geruest")

    def nur(*an):
        return lambda o: o.name not in zustaende or o.name in an

    schnell = "--schnell" in sys.argv
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=5.0, ausschnitt=28.0, schnell=schnell)
    if "--nur-anker" in sys.argv:
        print(bs.anker_bild(NAME, nur("Stufe1")))
        return
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, nur("Stufe3")))
        return
    bilder = [bs.rendern(f"{NAME}-stufe{k}", nur(f"Stufe{k}")) for k in (1, 2, 3)]
    print(bilder)
    print(bs.rendern(NAME + "-leerstand", lambda o: nur("Stufe1", "Leerstand")(o) and o.name not in ("Wappen", "Fahne")))
    print(bs.rendern(NAME + "-geruest", nur("Stufe2", "Geruest")))
    if not schnell:
        print(bs.rundum(NAME + "-stufe3", nur("Stufe3")))
    print(bs.nebeneinander(NAME, bilder, [f"Stufe {k + 1}: {n}" for k, n in enumerate(STUFEN)]))
    print(bs.anker_bild(NAME, nur("Stufe1")))
    print(bs.nachtbild(NAME, nur("Stufe3")))


if __name__ == "__main__":
    main()
