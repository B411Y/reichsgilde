"""Werkstatt des Predigers – Kirche (Stufe 1), Dom (Stufe 2), Kathedrale (Stufe 3). Fläche 16 × 14 m.

    blender -b --python tools/modelle/prediger.py        oder
    python tools/modelle/prediger.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/prediger.glb und Bilder nach docs/modelle/.

Objekte im GLB (wie bei den anderen Werkstätten):
    Haus        die Kirche mit Glockenturm, in jeder Stufe gleich
    Stufe1      Kirche: Kirchhof mit Gräbern und Linde, Kirchhofmauer
    Stufe2      Dom: dazu Chor mit Apsis, Skriptorium für Papier und Schriften, Gaube
    Stufe3      Kathedrale: Chor, Skriptorium, Vorhalle mit Wimperg vor dem Portal, Fialen auf allen Strebepfeilern,
                Strebebogen, Dachreiter mit silberner Spitze, Gaube
    Wappen, Fahne, Leerstand, Geruest, Anker_Tuer, Anker_Karren
Genau einer der Knoten Stufe1–Stufe3 ist sichtbar; Leerstand und Geruest sind standardmäßig aus.

Aufbau (Blender: Z oben, Gasse bei -Y; Fläche x ±8, y ±7, davon 2 m Vorplatz vorn):
    Saalkirche links (7,8 × 8,6 m) aus Haustein mit Strebepfeilern, Spitzbogenfenstern und steilem Schieferdach,
    der Giebel mit Spitzbogenportal und Rosette zur Gasse, Opferstock vor dem Tor (wie bisher im Spiel).
    Vorn rechts der Glockenturm (wie bisher im Spiel an der Ostseite): vier Geschosse, Turmuhr mit Zifferblatt
    in der Hausfarbe, Schallöffnungen, Schieferhelm mit goldenem Kreuz.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "prediger"
FLAECHE = (16.0, 14.0)
STUFEN = ("Kirche", "Dom", "Kathedrale")

# ── Maße der Kirche ──────────────────────────────────────────────────────────
X0, X1, Y0, Y1 = -7.0, 0.8, -5.0, 3.6
CX, CY = (X0 + X1) / 2, (Y0 + Y1) / 2
HW = (X1 - X0) / 2
Z_SOCKEL = 0.4
H_WAND = 6.2
Z_TRAUFE = Z_SOCKEL + H_WAND               # 6,6
NEIGUNG = 56
TX0, TX1, TY0, TY1 = X1, 3.6, Y0, -2.2      # Glockenturm
H_TURM = 13.0


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []
    pfeiler = []                            # Köpfe der Strebepfeiler (für die Fialen der Kathedrale)

    # ── Schiff: Sockel, Mauerkörper, Gesims, Eckquader ────────────────────────
    haus.quader((CX, CY, Z_SOCKEL / 2), (X1 - X0 + 0.14, Y1 - Y0 + 0.14, Z_SOCKEL), st["Bruchstein dunkel"])
    haus.quader((CX, CY, Z_SOCKEL + H_WAND / 2), (X1 - X0, Y1 - Y0, H_WAND), st["Haustein"], ohne=("+z",))
    bs.gesims(haus, st, X0, X1, Y0, Y1, Z_TRAUFE - 0.22, hoehe=0.22, vor=0.1)
    M_vorn = bs.wandmatrix((CX, Y0, Z_SOCKEL), "vorne")
    M_rechts = bs.wandmatrix((X1, CY, Z_SOCKEL), "rechts")
    M_links = bs.wandmatrix((X0, CY, Z_SOCKEL), "links")
    M_hinten = bs.wandmatrix((CX, Y1, Z_SOCKEL), "hinten")
    for M, L in ((M_vorn, 2 * HW), (M_rechts, Y1 - Y0), (M_links, Y1 - Y0), (M_hinten, 2 * HW)):
        bs.fugen(haus, M, L - 0.02, 0.0, H_WAND - 0.22, st)

    # ── Fassade zur Gasse: Portal, Rosette, Stufen, Opferstock ───────────────
    portal = Oeffnung("tuer", 0.0, 0.0, 1.9, 3.7)
    spitzbogen(haus, st, M_vorn, portal, tuer=True)
    oeffnungen.append((M_vorn, portal))
    rosette(haus, st, M_vorn, 0.0, 5.0, 0.8)
    for k, (tiefe, hoehe) in enumerate(((1.0, 0.14), (0.6, 0.28), (0.3, Z_SOCKEL + 0.01))):
        haus.quader((CX, Y0 - tiefe / 2, hoehe / 2), (2.8 - 0.3 * k, tiefe, hoehe), st["Sandstein"])
    opferstock(haus, st, (CX + 2.6, Y0 - 0.9, 0))
    for u in (-HW + 0.3, HW - 0.3):
        pfeiler.append(strebepfeiler(haus, st, M_vorn, u))

    # ── Langseiten: Spitzbogenfenster zwischen Strebepfeilern ────────────────
    fenster = dict(v=1.5, b=1.0, h=3.3)
    for u in (-0.3, 1.9):                                             # rechts (u entlang +y), vorn steht der Turm
        o = Oeffnung("fenster", u, **fenster)
        spitzbogen(haus, st, M_rechts, o)
        oeffnungen.append((M_rechts, o))
    for u in (2.9, 0.6, -1.7):                                        # links (u entlang -y)
        o = Oeffnung("fenster", u, **fenster)
        spitzbogen(haus, st, M_links, o)
        oeffnungen.append((M_links, o))
    for u in (0.8, Y1 - CY - 0.35):
        pfeiler.append(strebepfeiler(haus, st, M_rechts, u))
    for u in (-(Y1 - CY) + 0.35, -0.55, 1.75, (Y1 - CY) - 0.35):
        pfeiler.append(strebepfeiler(haus, st, M_links, u))
    for u in (-2.0, 2.0):
        o = Oeffnung("fenster", u, **fenster)
        spitzbogen(haus, st, M_hinten, o)
        oeffnungen.append((M_hinten, o))

    # ── Schieferdach mit dem Giebel zur Gasse, steinerne Giebel ──────────────
    drehung = Matrix.Translation((CX, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(haus, st, Y0, Y1, -HW, HW, Z_TRAUFE, NEIGUNG, traufe=0.35, ortgang=0.2,
                         enden="Haustein", matrix=drehung, **bs.SCHIEFER)
    hoch = HW * dach["tan"]
    for y, seite in ((Y0, "vorne"), (Y1, "hinten")):
        M = bs.wandmatrix((CX, y, Z_TRAUFE), seite)
        bs.steingiebel(haus, st, M, 2 * HW, hoch, oeffnungen, stoff="Haustein", mit_fenster=False)
        o = Oeffnung("fenster", 0.0, 1.0, 0.6, 1.6)
        spitzbogen(haus, st, M, o)
        oeffnungen.append((M, o))
    haus.quader((CX, Y0 + 0.1, Z_TRAUFE + hoch + 0.35), (0.12, 0.12, 0.9), st["Gold"])        # Kreuz auf dem Giebel
    haus.quader((CX, Y0 + 0.1, Z_TRAUFE + hoch + 0.5), (0.5, 0.12, 0.12), st["Gold"])

    # ── Glockenturm vorn rechts ───────────────────────────────────────────────
    turm_oeffnungen = glockenturm(haus, st)
    oeffnungen += turm_oeffnungen

    # ── Wappen über dem Portal, Fahne am Turm ─────────────────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, M_vorn, -2.2, 4.4, st, groesse=0.7, rahmen="Sandstein")
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (CX, Y1 - 0.3, dach["z_first"] - 0.25), hoehe=2.2, richtung=1)

    # ── Stufen ────────────────────────────────────────────────────────────────
    stufe1 = bs.Netz("Stufe1")
    kirchhof(stufe1, st, reihen=4)
    stufe2 = bs.Netz("Stufe2")
    chor(stufe2, st)
    skriptorium(stufe2, st)
    kirchhof(stufe2, st, reihen=2)
    bs.gaube(stufe2, st, dach, [], x0=-0.9, x1=0.5, y_front=-HW + 1.2, art="stein")
    stufe3 = bs.Netz("Stufe3")
    chor(stufe3, st, fialen=True)
    skriptorium(stufe3, st)
    kirchhof(stufe3, st, reihen=1)
    vorhalle(stufe3, st)
    strebebogen(stufe3, st, CY + 0.8)
    for p in pfeiler:
        fiale(stufe3, st, p)
    bs.gaube(stufe3, st, dach, [], x0=-0.9, x1=0.5, y_front=-HW + 1.2, art="stein")
    bs.dachreiter(stufe3, st, Vector((CX, 1.9, dach["z_first"])), spitze="Silber", koerper="Haustein", ecken="Sandstein")

    # ── Leerstand, Gerüst, Anker ──────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for x, y in ((X0 + 0.3, Y0 - 0.15), (-2.2, Y0 - 1.3), (TX1 - 0.2, Y0 - 0.15), (X0 - 0.15, -1.0), (X1 + 0.15, 1.2)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=X0 - 0.15, x1=X1 + 0.15, y_innen=Y0 - 1.0, y_aussen=Y0 - 1.5,
                  ebenen=(2.4, 4.8), hoehe_innen=7.6, hoehe_aussen=8.2, felder=3)
    anker = [bs.anker("Anker_Tuer", (CX, Y0 - 0.15, Z_SOCKEL + 0.01)),
             bs.anker("Anker_Karren", ((TX0 + TX1) / 2, Y0 - 1.5, 0.0))]

    objekte = [haus.objekt(), stufe1.objekt(), stufe2.objekt(), stufe3.objekt(), wappen.objekt(), fahne.objekt(),
               leer.objekt(), geruest.objekt()] + anker
    for o in objekte:
        if o.name in ("Stufe2", "Stufe3", "Leerstand", "Geruest"):
            bs.zustand(o)
        elif o.name == "Stufe1":
            o["zustand"] = True
    return objekte


# ─────────────────────────────────────────────────────────────────────────────
#  Gotische Teile
# ─────────────────────────────────────────────────────────────────────────────

def bogen_punkte(u, v, b, h, teile=4):
    """Umriss eines Spitzbogens (gleichseitig) von unten links gegen den Uhrzeigersinn; h ist die Scheitelhöhe."""
    k = v + h - b * math.sin(math.radians(60))
    rechts = [(u - b / 2 + b * math.cos(math.radians(60 * i / teile)), k + b * math.sin(math.radians(60 * i / teile)))
              for i in range(teile + 1)]
    links = [(u + b / 2 + b * math.cos(math.radians(120 + 60 * i / teile)), k + b * math.sin(math.radians(120 + 60 * i / teile)))
             for i in range(1, teile + 1)]
    return [(u - b / 2, v), (u + b / 2, v)] + rechts + links


def spitzbogen(netz, st, M, o, tuer=False, fuellung=None, g=0.17):
    """Spitzbogenfenster oder -portal: Füllung (Glas mit Licht, Tür in der Hausfarbe oder `fuellung`),
    Gewände aus Sandstein als U um die Öffnung, beim Fenster Mittelpfosten und Sohlbank, bei der Tür Eisenbänder."""
    innen = bogen_punkte(o.u, o.v, o.b, o.h)
    stoff = st[fuellung] if fuellung else st["Hausfarbe" if tuer else bs.scheibe(M, o)]
    netz.vieleck([M @ Vector((p[0], -0.006, p[1])) for p in innen], stoff)
    aussen = bogen_punkte(o.u, o.v, o.b + 2 * g, o.h + g)
    ring = [aussen[1]] + aussen[2:] + [aussen[0], innen[0]] + list(reversed(innen[2:])) + [innen[1]]
    bs.platte(netz, M, ring, -0.07, 0.0, st["Sandstein"])
    if tuer:
        for f in (0.25, 0.55):
            bs.balken(netz, M, o.u - o.b * 0.42, o.v + o.h * f, o.u + o.b * 0.42, o.v + o.h * f, st["Eisen"], vor=0.03, breite=0.07)
        bs.balken(netz, M, o.u, o.v, o.u, o.v + o.h - 0.05, st["Fachwerk"], vor=0.025, breite=0.05)
    elif not fuellung:
        bs.balken(netz, M, o.u, o.v, o.u, o.v + o.h - o.b * 0.45, st["Sandstein"], vor=0.035, breite=0.07)
        netz.quader((o.u, -0.06, o.v - 0.06), (o.b + 2 * g + 0.1, 0.14, 0.12), st["Sandstein"], M, ohne=("+y",))


def rosette(netz, st, M, u, v, r, n=12):
    """Rosette: rundes Glas mit Speichen, Ring aus Sandstein."""
    punkte = [(u + r * math.cos(2 * math.pi * k / n), v + r * math.sin(2 * math.pi * k / n)) for k in range(n)]
    netz.vieleck([M @ Vector((p[0], -0.006, p[1])) for p in punkte], st["Fenster"])
    ring = [(u + (r + 0.16) * math.cos(2 * math.pi * k / n), v + (r + 0.16) * math.sin(2 * math.pi * k / n)) for k in range(n)]
    for k in range(n):
        a, b = ring[k], ring[(k + 1) % n]
        netz.stab(M @ Vector((a[0], -0.05, a[1])), M @ Vector((b[0], -0.05, b[1])), 0.2, 0.1, st["Sandstein"],
                  quer=M.to_3x3() @ Vector((0, 1, 0)), mehr=0.06)
    for k in range(0, n, 2):
        p = punkte[k]
        netz.stab(M @ Vector((u, -0.03, v)), M @ Vector((p[0], -0.03, p[1])), 0.05, 0.04, st["Sandstein"],
                  quer=M.to_3x3() @ Vector((0, 1, 0)))
    bs.platte(netz, M, [(u + 0.16 * math.cos(2 * math.pi * k / 8), v + 0.16 * math.sin(2 * math.pi * k / 8)) for k in range(8)],
              -0.06, -0.02, st["Sandstein"])


def strebepfeiler(netz, st, M, u, h=5.4, tief=0.85, breit=0.6):
    """Strebepfeiler vor einer Wand (Wandsystem M): unten breit, ein Absatz mit Wasserschlag, oben schmaler mit
    Pultabdeckung. Gibt den Punkt über dem Kopf zurück (dort sitzt in Stufe 3 eine Fiale)."""
    haustein, sand = st["Haustein"], st["Sandstein"]
    h1 = h * 0.55
    netz.quader((u, -tief / 2, h1 / 2), (breit, tief, h1), haustein, M, ohne=("+y",))
    t = 0.65 * tief
    netz.quader((u, -t / 2, (h1 + h) / 2), (breit * 0.9, t, h - h1), haustein, M, ohne=("+y",))
    dreh = M @ Matrix.Rotation(math.pi, 4, "Z")                    # Prisma-y → nach außen, Prisma-x → Wand -u
    for z, d0, b in ((h1, tief, breit), (h, t, breit * 0.9)):      # Wasserschlag: Abdeckung fällt nach außen ab
        profil = [(0.0, z), (d0 + 0.04, z), (d0 + 0.04, z + 0.08), (0.0, z + 0.08 + d0 * 0.75)]
        netz.prisma(profil, -u - b / 2 - 0.02, -u + b / 2 + 0.02, sand, matrix=dreh)
    return M @ Vector((u, -t / 2, h + 0.08 + t * 0.75 * 0.5))


def fiale(netz, st, fuss, h=1.1):
    """Fiale: kleiner Schaft mit vier Giebelchen angedeutet und spitzer Pyramide, oben eine Kreuzblume."""
    fuss = Vector(fuss)
    netz.quader(fuss + Vector((0, 0, 0.25)), (0.26, 0.26, 0.5), st["Sandstein"])
    gipfel = fuss + Vector((0, 0, 0.5 + h))
    ecken = [fuss + Vector((sx * 0.18, sy * 0.18, 0.5)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        a, b = ecken[i], ecken[(i + 1) % 4]
        netz.vieleck_nach([a, b, gipfel], (a + b) / 2 - (fuss + Vector((0, 0, 0.5))), st["Sandstein"])
    netz.quader(gipfel + Vector((0, 0, 0.04)), (0.1, 0.1, 0.1), st["Sandstein"])


def opferstock(netz, st, fuss):
    """Opferstock: Holzblock mit Eisenbändern und Schlitz, auf einer Steinplatte (wie bisher im Spiel vor dem Tor)."""
    x, y = fuss[0], fuss[1]
    netz.quader((x, y, 0.05), (0.7, 0.7, 0.1), st["Sandstein"])
    netz.quader((x, y, 0.55), (0.45, 0.45, 0.9), st["Holz"])
    for z in (0.35, 0.85):
        netz.quader((x, y, z), (0.48, 0.48, 0.06), st["Eisen"])
    netz.quader((x, y - 0.1, 1.003), (0.18, 0.04, 0.01), st["Loch"], ohne=("-z",))


def glockenturm(netz, st):
    """Glockenturm vorn rechts: Haustein mit Eckquadern und Gesimsen, kleine Spitzbogenfenster, Turmuhr mit
    Zifferblatt in der Hausfarbe, Schallöffnungen mit Lamellen, Schieferhelm mit goldenem Kreuz."""
    cx, cy = (TX0 + TX1) / 2, (TY0 + TY1) / 2
    b = TX1 - TX0
    oeffnungen = []
    netz.quader((cx + 0.07, cy, Z_SOCKEL / 2), (b, b + 0.14, Z_SOCKEL), st["Bruchstein dunkel"], ohne=("-x",))  # neben dem Sockel des Schiffs
    netz.quader((cx, cy, Z_SOCKEL + (H_TURM - 0.25 - Z_SOCKEL) / 2), (b, b, H_TURM - 0.25 - Z_SOCKEL), st["Haustein"],
                ohne=("-z", "+z"))                         # oben schließt das Gesims ab
    for z, hoch, vor in ((4.6, 0.18, 0.08), (9.0, 0.18, 0.08), (H_TURM - 0.25, 0.25, 0.12)):
        bs.gesims(netz, st, TX0, TX1, TY0, TY1, z, hoehe=hoch, vor=vor)
    for sx, x in ((-1, TX0), (1, TX1)):
        for sy, y in ((-1, TY0), (1, TY1)):
            if sx > 0:                                             # links stößt der Turm an das Schiff
                bs.eckquader(netz, st, x, y, sx, sy, Z_SOCKEL, H_TURM - 0.25)
    seiten = {"vorne": ((cx, TY0, Z_SOCKEL), True), "rechts": ((TX1, cy, Z_SOCKEL), True),
              "hinten": ((cx, TY1, Z_SOCKEL), False), "links": ((TX0, cy, Z_SOCKEL), False)}
    for seite, (punkt, unten) in seiten.items():
        M = bs.wandmatrix(punkt, seite)
        if unten:
            bs.fugen(netz, M, b - 0.02, 0.0, H_TURM - Z_SOCKEL - 0.3, st)
            o = Oeffnung("fenster", 0.0, 2.6 if seite == "vorne" else 5.4, 0.5, 1.3)
            spitzbogen(netz, st, M, o, g=0.12)
            oeffnungen.append((M, o))
        schall = Oeffnung("fenster", 0.0, 9.6, 0.9, 2.2)
        spitzbogen(netz, st, M, schall, fuellung="Loch", g=0.15)
        for k in range(4):
            v = schall.v + 0.3 + 0.38 * k
            netz.stab(M @ Vector((-0.42, -0.02, v)), M @ Vector((0.42, -0.02, v)), 0.1, 0.03, st["Holz alt"],
                      quer=M.to_3x3() @ Vector((0, 1, 0)))
        if seite in ("vorne", "rechts"):
            turmuhr(netz, st, M, 0.0, 7.4)
    # Schieferhelm mit Kreuz
    z0 = H_TURM
    gipfel = Vector((cx, cy, z0 + 5.6))
    ecken = [Vector((cx + sx * (b / 2 + 0.25), cy + sy * (b / 2 + 0.25), z0)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        a, c = ecken[i], ecken[(i + 1) % 4]
        netz.vieleck_nach([a, c, gipfel], (a + c) / 2 - Vector((cx, cy, z0 + 1.0)), st["Schiefer"])
        netz.vieleck_nach([a, c, c + Vector((0, 0, -0.14)), a + Vector((0, 0, -0.14))], (a + c) / 2 - Vector((cx, cy, z0)),
                          st["Schieferkante"])
    netz.quader(gipfel + Vector((0, 0, 0.15)), (0.22, 0.22, 0.22), st["Gold"])
    netz.quader(gipfel + Vector((0, 0, 0.85)), (0.12, 0.12, 1.3), st["Gold"])
    netz.quader(gipfel + Vector((0, 0, 1.1)), (0.7, 0.12, 0.12), st["Gold"])
    return oeffnungen


def turmuhr(netz, st, M, u, v, r=0.55):
    """Turmuhr: Zifferblatt in der Hausfarbe, goldener Ring mit Stundenmarken, zwei Zeiger."""
    n = 12
    bs.platte(netz, M, [(u + r * math.cos(2 * math.pi * k / n), v + r * math.sin(2 * math.pi * k / n)) for k in range(n)],
              -0.05, 0.0, st["Hausfarbe"])
    quer = M.to_3x3() @ Vector((0, 1, 0))
    for k in range(n):
        a = (u + (r + 0.04) * math.cos(2 * math.pi * k / n), v + (r + 0.04) * math.sin(2 * math.pi * k / n))
        c = (u + (r + 0.04) * math.cos(2 * math.pi * (k + 1) / n), v + (r + 0.04) * math.sin(2 * math.pi * (k + 1) / n))
        netz.stab(M @ Vector((a[0], -0.06, a[1])), M @ Vector((c[0], -0.06, c[1])), 0.08, 0.04, st["Gold"], quer=quer, mehr=0.03)
    netz.stab(M @ Vector((u, -0.07, v)), M @ Vector((u + 0.12, -0.07, v + 0.38)), 0.05, 0.02, st["Gold"], quer=quer)
    netz.stab(M @ Vector((u, -0.08, v)), M @ Vector((u + 0.28, -0.08, v - 0.1)), 0.06, 0.02, st["Gold"], quer=quer)


def grab(netz, st, fuss, art=0):
    """Grab: flacher Hügel mit Gras, am Kopfende ein Holzkreuz oder ein kleiner Grabstein."""
    x, y = fuss[0], fuss[1]
    netz.quader((x, y, 0.06), (0.7, 1.5, 0.12), st["Gras"], ohne=("-z",))
    if art % 3 == 2:
        bs.grabstein(netz, st, (x, y + 0.85, 0), breite=0.5, hoehe=0.75, form="rund" if art % 2 else "giebel")
    else:
        netz.quader((x, y + 0.85, 0.5), (0.08, 0.08, 1.0), st["Holz alt"])
        netz.quader((x, y + 0.85, 0.75), (0.45, 0.07, 0.07), st["Holz alt"])


def kirchhof(netz, st, reihen=4):
    """Kirchhof rechts der Kirche: Reihen von Gräbern, eine Linde mit Bank, Kirchhofmauer."""
    n = 0
    for i in range(reihen):
        y = 5.4 - 2.0 * i
        for x in (5.0, 6.0, 7.0):
            grab(netz, st, (x, y, 0), n)
            n += 1
    bs.linde(netz, st, (5.6, -3.9, 0), hoehe=6.0, r=1.7, saat=21)
    netz.quader((4.3, -3.9, 0.45), (0.3, 1.6, 0.06), st["Holz"])
    for y in (-4.5, -3.3):
        netz.quader((4.3, y, 0.21), (0.25, 0.08, 0.42), st["Holz alt"])
    bs.trockenmauer(netz, st, [(7.76, -4.8), (7.76, 6.76), (-7.76, 6.76), (-7.76, Y1 + 0.3)])


def chor(netz, st, fialen=False, r=2.6, h=5.4):
    """Chor mit polygonaler Apsis (halbes Achteck) hinten am Schiff: Haustein, schmale Spitzbogenfenster,
    Strebepfeiler an den Ecken, Schieferdach als halbe Pyramide. In der Kathedrale mit Fialen."""
    c = Vector((CX, Y1, 0))
    winkel = [0, 45, 90, 135, 180]
    ecken = [c + Vector((r * math.cos(math.radians(w)), r * math.sin(math.radians(w)), 0)) for w in winkel]
    profil = [(p.y, -p.x) for p in ecken]                          # Prisma-x → Welt-z: (py, pz) = (y, -x)
    dreh = Matrix.Rotation(math.radians(-90), 4, "Y")
    netz.prisma(profil, 0.0, Z_SOCKEL, st["Bruchstein dunkel"], matrix=Matrix.Translation((0, 0.0, 0)) @ dreh)
    netz.prisma(profil, Z_SOCKEL, Z_SOCKEL + h, st["Haustein"], matrix=dreh)
    for i in range(4):
        a, b = ecken[i], ecken[i + 1]
        phi = math.radians(22.5 + 45 * i)
        M = Matrix.Translation(((a + b) / 2).to_3d() + Vector((0, 0, Z_SOCKEL))) @ Matrix.Rotation(phi + math.pi / 2, 4, "Z")
        o = Oeffnung("fenster", 0.0, 1.4, 0.75, 3.0)
        spitzbogen(netz, st, M, o, g=0.13)
    for i in range(1, 4):
        p = ecken[i]
        w = math.radians(winkel[i])
        M = Matrix.Translation(p + Vector((0, 0, Z_SOCKEL))) @ Matrix.Rotation(w + math.pi / 2, 4, "Z")
        kopf = strebepfeiler(netz, st, M, 0.0, h=h - 0.6, tief=0.7, breit=0.5)
        if fialen:
            fiale(netz, st, kopf)
    # Dach: halbe Pyramide aus Schiefer
    z = Z_SOCKEL + h
    traufe = [c + Vector(((r + 0.35) * math.cos(math.radians(w)), (r + 0.35) * math.sin(math.radians(w)), z - 0.2))
              for w in winkel]
    gipfel = c + Vector((0, 0, z + (r + 0.35) * 1.3))
    for i in range(4):
        a, b = traufe[i], traufe[i + 1]
        netz.vieleck_nach([a, b, gipfel], (a + b) / 2 - (c + Vector((0, 0, z))), st["Schiefer"])
        netz.vieleck_nach([a, b, b + Vector((0, 0, -0.12)), a + Vector((0, 0, -0.12))], (a + b) / 2 - (c + Vector((0, 0, z))),
                          st["Schieferkante"])


def skriptorium(netz, st):
    """Skriptorium (Schreibstube für Papier, Kerzen und Schriften) rechts hinten am Schiff: Haustein, Schieferdach mit
    dem Giebel zum Hof, Spitzbogenfenster, Tür mit Stufe."""
    x0, x1, y0, y1 = X1, 4.4, 1.8, 5.4
    h = 4.0
    cy = (y0 + y1) / 2
    netz.quader(((x0 + x1) / 2 + 0.05, cy, Z_SOCKEL / 2), (x1 - x0 + 0.04, y1 - y0 + 0.12, Z_SOCKEL), st["Bruchstein dunkel"],
                ohne=("-x",))
    netz.quader(((x0 + x1) / 2, cy, Z_SOCKEL + h / 2), (x1 - x0, y1 - y0, h), st["Haustein"], ohne=("+z", "-x"))
    M_v = bs.wandmatrix(((x0 + x1) / 2, y0, Z_SOCKEL), "vorne")
    M_r = bs.wandmatrix((x1, cy, Z_SOCKEL), "rechts")
    bs.fugen(netz, M_v, x1 - x0 - 0.02, 0.0, h, st)
    bs.fugen(netz, M_r, y1 - y0 - 0.02, 0.0, h, st)
    tuer = Oeffnung("tuer", 0.6, 0.0, 1.0, 2.3)
    spitzbogen(netz, st, M_v, tuer, tuer=True, g=0.13)
    netz.quader(((x0 + x1) / 2 + tuer.u, y0 - 0.2, (Z_SOCKEL + 0.01) / 2), (1.3, 0.4, Z_SOCKEL + 0.01), st["Sandstein"])
    spitzbogen(netz, st, M_v, Oeffnung("fenster", -0.9, 1.3, 0.7, 2.0), g=0.13)
    for u in (-0.9, 0.9):
        spitzbogen(netz, st, M_r, Oeffnung("fenster", u, 1.3, 0.7, 2.0), g=0.13)
    dach = bs.satteldach(netz, st, x0, x1, y0, y1, Z_SOCKEL + h, 50, traufe=0.3, ortgang=0.2, enden="Haustein", **bs.SCHIEFER)
    bs.steingiebel(netz, st, bs.wandmatrix((x1, cy, Z_SOCKEL + h), "rechts"), y1 - y0, (y1 - y0) / 2 * dach["tan"], [],
                   stoff="Haustein")
    # Papier zum Trocknen und Lumpenballen vor der Tür
    for k in range(3):
        netz.quader((1.6 + 0.6 * k, y0 - 0.6, 0.25), (0.5, 0.45, 0.5), st["Leder" if k % 2 else "Seil"])


def vorhalle(netz, st):
    """Vorhalle vor dem Portal: offener Spitzbogen unter einem Wimperg (steiler Ziergiebel mit Krabben), Seitenwände,
    Schieferdach; davor zwei Fialen."""
    w, tief = 1.7, 1.5
    y_front = Y0 - tief
    M = bs.wandmatrix((CX, y_front, 0.0), "vorne")
    H = 4.4
    innen = bogen_punkte(0.0, 0.0, 2.0, 3.8)
    umriss = [(-w, 0.0)] + [innen[0]] + list(reversed(innen[2:])) + [innen[1], (w, 0.0), (w, H), (0.0, H + 2.4), (-w, H)]
    bs.platte(netz, M, umriss, -0.0, 0.45, st["Sandstein"])
    for s in (-1, 1):
        netz.quader((CX + s * (w - 0.25), y_front + tief / 2 + 0.2, H / 2), (0.36, tief - 0.4 + 0.45, H), st["Haustein"])
        fiale(netz, st, Vector((CX + s * (w + 0.05), y_front + 0.2, H + 0.02)), h=1.2)
        for k in range(4):                                         # Krabben auf dem Wimperg
            t = (k + 0.5) / 4
            p = M @ Vector((s * w * (1 - t), -0.1, H + 2.4 * t + 0.1))
            netz.quader(p, (0.14, 0.14, 0.14), st["Sandstein"])
    netz.quader(M @ Vector((0, -0.05, H + 2.55)), (0.14, 0.14, 0.4), st["Sandstein"])
    drehung = Matrix.Translation((CX, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    bs.satteldach(netz, st, y_front + 0.3, Y0, -w - 0.05, w + 0.05, H, 55, traufe=0.2, ortgang=0.0, enden="Haustein",
                  matrix=drehung, **bs.SCHIEFER)


def strebebogen(netz, st, y):
    """Strebebogen an der rechten Langseite: freistehender Pfeiler mit Fiale, ein Bogen stützt die Wand unter der Traufe."""
    xp = 3.4
    netz.quader((xp, y, Z_SOCKEL / 2), (0.9, 0.9, Z_SOCKEL), st["Bruchstein dunkel"])
    netz.quader((xp, y, (Z_SOCKEL + 6.4) / 2), (0.7, 0.7, 6.4 - Z_SOCKEL), st["Haustein"])
    netz.quader((xp, y, 6.45), (0.8, 0.8, 0.1), st["Sandstein"])
    fiale(netz, st, Vector((xp, y, 6.5)), h=1.3)
    oben = [(xp - 0.35, 6.2), (X1, Z_TRAUFE - 0.3)]
    unten = [(X1 + (xp - 0.35 - X1) * (1 - math.cos(math.radians(90 * k / 5))), Z_TRAUFE - 1.0 - 1.3 * math.sin(math.radians(90 * k / 5)))
             for k in range(6)]
    profil = [oben[1], oben[0]] + list(reversed(unten))
    dreh = Matrix.Translation((0, y, 0)) @ Matrix.Rotation(math.radians(-90), 4, "Z")   # Prisma-y → Welt x
    netz.prisma([(px, pz) for px, pz in profil], -0.2, 0.2, st["Sandstein"], matrix=dreh)


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
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=6.5, ausschnitt=30.0, schnell=schnell)
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
