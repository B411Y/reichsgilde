"""Werkstatt des Steinmetzen – Maurerei (Stufe 1), Steinschneiderei (Stufe 2), Steinmetzhütte (Stufe 3).
Fläche 16 × 14 m.

    blender -b --python tools/modelle/steinmetz.py        oder
    python tools/modelle/steinmetz.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/steinmetz.glb und Bilder nach docs/modelle/.

Objekte im GLB (wie beim Schmied und Tischler):
    Haus        die Werkstatt, massiv aus Bruchstein, in jeder Stufe gleich
    Stufe1      Maurerei: Vordach mit zwei Bänken und Werkstücken, Quaderstapel, Bruchsteinhaufen,
                Grabsteine und Mühlsteine zum Verkauf an der Gasse, Trockenmauer um den Hof
    Stufe2      Steinschneiderei: steinerner Anbau mit Schleifstein, Steinsäge, Dreibein mit Flaschenzug
                und hängendem Block, Gaube
    Stufe3      Steinmetzhütte: offene Bauhütte mit Steinfigur in Arbeit und Säule, Schaustücke vorn
                (Taufbecken, Grabmal, Säule), Dreibein im Hof, Gaube, Dachreiter mit Schieferspitze
    Wappen, Fahne, Leerstand, Geruest, Anker_Tuer, Anker_Karren
Genau einer der Knoten Stufe1–Stufe3 ist sichtbar; Leerstand und Geruest sind standardmäßig aus.

Aufbau (Blender: Z oben, Gasse bei -Y; Fläche x ±8, y ±7, davon 2 m Vorplatz vorn):
    Werkstatt links (7,8 × 6,8 m): zwei Geschosse aus Bruchstein mit Eckquadern und Gesimsen aus Sandstein –
    der Steinmetz baut massiv und zeigt sein Handwerk an der eigenen Fassade. Ziegeldach mit der Traufe zur
    Gasse, Giebel aus Bruchstein, Rundbogentor mit Rampe, Zunftzeichen mit Zirkel und Winkel.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "steinmetz"
FLAECHE = (16.0, 14.0)
STUFEN = ("Maurerei", "Steinschneiderei", "Steinmetzhütte")

# ── Maße der Werkstatt ───────────────────────────────────────────────────────
X0, X1, Y0, Y1 = -7.2, 0.6, -5.0, 1.8
CX, CY = (X0 + X1) / 2, (Y0 + Y1) / 2
HL, HT = (X1 - X0) / 2, (Y1 - Y0) / 2        # halbe Länge (entlang der Gasse) und halbe Tiefe
Z_SOCKEL, H_EG, H_OG = 0.3, 3.2, 2.6
H_WAND = H_EG + H_OG
Z_TRAUFE = Z_SOCKEL + H_WAND                 # 6,1
NEIGUNG = 48


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []

    # ── Sockel, Mauerkörper, Gesimse, Eckquader ───────────────────────────────
    haus.quader((CX, CY, Z_SOCKEL / 2), (X1 - X0 + 0.12, Y1 - Y0 + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    haus.quader((CX, CY, Z_SOCKEL + H_WAND / 2), (X1 - X0, Y1 - Y0, H_WAND), st["Bruchstein"], ohne=("+z",))
    bs.gesims(haus, st, X0, X1, Y0, Y1, Z_SOCKEL + H_EG, hoehe=0.16, vor=0.07)
    bs.gesims(haus, st, X0, X1, Y0, Y1, Z_TRAUFE - 0.2, hoehe=0.2, vor=0.1)
    for sx, x in ((-1, X0), (1, X1)):
        for sy, y in ((-1, Y0), (1, Y1)):
            bs.eckquader(haus, st, x, y, sx, sy, Z_SOCKEL, Z_TRAUFE - 0.2)

    # ── Wände: Lagerfugen, Steinfenster, Rundbogentor ─────────────────────────
    M_vorn = bs.wandmatrix((CX, Y0, Z_SOCKEL), "vorne")
    M_rechts = bs.wandmatrix((X1, CY, Z_SOCKEL), "rechts")
    M_hinten = bs.wandmatrix((CX, Y1, Z_SOCKEL), "hinten")
    M_links = bs.wandmatrix((X0, CY, Z_SOCKEL), "links")
    for M, L in ((M_vorn, 2 * HL), (M_rechts, 2 * HT), (M_hinten, 2 * HL), (M_links, 2 * HT)):
        bs.fugen(haus, M, L - 0.02, 0.0, H_WAND - 0.2, st)

    F = "fenster"
    eg = dict(v=1.0, b=0.85, h=1.25)
    og = dict(v=H_EG + 0.55, b=0.8, h=1.15)

    def fenster(M, liste):
        for o in liste:
            bs.gewaende(haus, M, o, st)
            oeffnungen.append((M, o))

    tor = Oeffnung("tuer", -1.4, 0.0, 2.4, 2.9)
    bs.portal(haus, M_vorn, tor, st)
    oeffnungen.append((M_vorn, tor))
    fenster(M_vorn, [Oeffnung(F, u, **eg) for u in (1.0, 2.5)])
    fenster(M_vorn, [Oeffnung(F, u, **og) for u in (-2.6, -0.1, 1.2, 2.5)])
    for M in (M_rechts, M_links):
        fenster(M, [Oeffnung(F, u, **eg) for u in (-1.5, 1.5)])
        fenster(M, [Oeffnung(F, u, **og) for u in (-1.5, 1.5)])
    fenster(M_hinten, [Oeffnung(F, 1.6, **eg)])
    fenster(M_hinten, [Oeffnung(F, u, **og) for u in (-1.8, 1.8)])
    hinten = Oeffnung("tuer", -1.6, 0.0, 1.0, 2.1)
    bs.tuer(haus, M_hinten, hinten, st)
    haus.quader((-1.6, -0.03, 2.2), (1.4, 0.1, 0.22), st["Sandstein"], M_hinten, ohne=("+y",))
    oeffnungen.append((M_hinten, hinten))
    # Rampe aus Sandsteinplatten vor dem Tor: Karren rollen hinein
    rampe = [(Y0 - 1.1, 0.0), (Y0, 0.0), (Y0, Z_SOCKEL + 0.01), (Y0 - 0.25, Z_SOCKEL + 0.01)]   # 1 cm über dem Sockel
    haus.prisma(rampe, CX + tor.u - 1.45, CX + tor.u + 1.45, st["Sandstein"])
    zunftzeichen(haus, st, M_vorn, 0.2, 2.75)

    # ── Ziegeldach mit der Traufe zur Gasse, Giebel aus Bruchstein, Schornstein ──
    dach = bs.satteldach(haus, st, X0, X1, Y0, Y1, Z_TRAUFE, NEIGUNG, traufe=0.4, ortgang=0.2, **bs.ZIEGEL)
    hoch = HT * dach["tan"]
    for seite, x in (("rechts", X1), ("links", X0)):
        bs.steingiebel(haus, st, bs.wandmatrix((x, CY, Z_TRAUFE), seite), 2 * HT, hoch, oeffnungen)
    xs, ys = CX - 2.2, Y1 - 1.4
    z0 = dach["oben_hinten"](ys) - 0.3
    haus.quader((xs, ys, (z0 + dach["z_first"] + 0.7) / 2), (0.6, 0.6, dach["z_first"] + 0.7 - z0), st["Bruchstein dunkel"])
    haus.quader((xs, ys, dach["z_first"] + 0.76), (0.78, 0.78, 0.12), st["Sandstein"])

    # ── Wappen über dem Tor, Fahne am rechten Giebel ──────────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, M_vorn, tor.u, H_EG + 1.2, st, groesse=0.75, rahmen="Sandstein")
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (X1 - 0.3, dach["y_first"], dach["z_first"] - 0.25), hoehe=2.4, richtung=1)

    # ── Stufen ────────────────────────────────────────────────────────────────
    stufe1 = bs.Netz("Stufe1")
    maurerei(stufe1, st)
    stufe2 = bs.Netz("Stufe2")
    steinschneiderei(stufe2, st)
    bs.gaube(stufe2, st, dach, [], x0=CX + tor.u - 0.8, x1=CX + tor.u + 0.8, y_front=Y0 + 1.0, art="stein")
    stufe3 = bs.Netz("Stufe3")
    steinmetzhuette(stufe3, st)
    bs.gaube(stufe3, st, dach, [], x0=CX + tor.u - 0.8, x1=CX + tor.u + 0.8, y_front=Y0 + 1.0, art="stein")
    bs.dachreiter(stufe3, st, Vector((CX + 1.2, dach["y_first"], dach["z_first"])), spitze="Schiefer",
                  koerper="Bruchstein", ecken="Sandstein")

    # ── Leerstand, Gerüst, Anker ──────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for x, y in ((X0 + 0.3, Y0 - 0.15), (-2.0, Y0 - 1.3), (X1 - 0.2, Y0 - 0.15), (X0 - 0.15, -1.0), (X1 + 0.15, 1.2)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=X0 - 0.15, x1=X1 + 0.15, y_innen=Y0 - 0.3, y_aussen=Y0 - 1.2,
                  ebenen=(2.4, 4.8), hoehe_innen=5.6, hoehe_aussen=6.6, felder=3)
    anker = [bs.anker("Anker_Tuer", (CX + tor.u, Y0 - 0.2, Z_SOCKEL)),
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
#  Teile der Werkstatt
# ─────────────────────────────────────────────────────────────────────────────

def zunftzeichen(netz, st, M, u, v):
    """Zunftzeichen am Ausleger: eine Sandsteintafel mit Zirkel und Winkel aus Eisen."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v)), M @ Vector((u, -1.1, v)), 0.05, 0.05, eisen)
    netz.stab(M @ Vector((u, 0, v - 0.5)), M @ Vector((u, -0.8, v - 0.02)), 0.04, 0.04, eisen)
    for d in (-0.9, -0.3):
        netz.stab(M @ Vector((u, d, v)), M @ Vector((u, d, v - 0.2)), 0.02, 0.02, eisen)
    dreh = M @ Matrix.Translation((u, -0.6, v - 0.5)) @ Matrix.Rotation(math.radians(-90), 4, "Z")
    netz.prisma([(-0.36, -0.3), (0.36, -0.3), (0.36, 0.3), (-0.36, 0.3)], -0.04, 0.04, st["Sandstein"], matrix=dreh)
    quer = dreh.to_3x3() @ Vector((1, 0, 0))
    for a, b in (((0, 0.2), (-0.17, -0.2)), ((0, 0.2), (0.17, -0.2)),          # Zirkel
                 ((-0.26, -0.06), (-0.26, -0.24)), ((-0.26, -0.24), (0.26, -0.24))):   # Winkel
        netz.stab(dreh @ Vector((0.05, *a)), dreh @ Vector((0.05, *b)), 0.035, 0.02, eisen, quer=quer)
    netz.quader(dreh @ Vector((0.05, 0, 0.2)), (0.06, 0.06, 0.06), eisen)


def block(netz, st, fuss, masse, stoff="Sandstein", dreh=0.0):
    """Quader, der auf `fuss` steht, um die Hochachse gedreht."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, masse[2] / 2), masse, st[stoff], t)


def staub(netz, st, mitte, r, saat=0):
    """Heller Steinstaub und Splitt auf dem Boden: ein unregelmäßiges Vieleck knapp über dem Grund."""
    zufall = random.Random(saat)
    x, y = mitte[0], mitte[1]
    punkte = [(x + r * (0.75 + 0.3 * zufall.random()) * math.cos(math.radians(60 * k + 10 * zufall.random())),
               y + r * (0.75 + 0.3 * zufall.random()) * math.sin(math.radians(60 * k + 10 * zufall.random())), 0.012)
              for k in range(6)]
    netz.vieleck_nach(punkte, (0, 0, 1), st["Steinstaub"])


def muehlstein(netz, st, fuss, r=0.55, dicke=0.26, dreh=0.0, neigung=10.0):
    """Stehender Mühlstein mit dem Loch für die Welle, leicht angelehnt."""
    t = (Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
         @ Matrix.Rotation(math.radians(neigung), 4, "Y"))
    profil = [(r * math.cos(math.radians(15 + 30 * k)), r + r * math.sin(math.radians(15 + 30 * k))) for k in range(12)]
    netz.prisma(profil, -dicke / 2, dicke / 2, st["Haustein"], matrix=t)
    for s in (-1, 1):
        n = t.to_3x3() @ Vector((s, 0, 0))
        netz.vieleck_nach([t @ Vector((s * (dicke / 2 + 0.003), 0.1 * math.cos(math.radians(45 * k)), r + 0.1 * math.sin(math.radians(45 * k))))
                           for k in range(8)], n, st["Loch"])


def muehlsteine_liegend(netz, st, boden, r=0.55, lagen=2):
    """Gestapelte Mühlsteine, oben das Loch."""
    z = 0.0
    for k in range(lagen):
        bs.achteck(netz, st, Vector(boden) + Vector((0.03 * k, -0.02 * k, 0)), r - 0.02 * k, z, z + 0.24, "Haustein", n=12)
        z += 0.24
    bs.rund_oben(netz, st, Vector(boden) + Vector((0.03 * (lagen - 1), -0.02 * (lagen - 1), 0)), 0.1, z + 0.003)


def ziegelstapel(netz, st, fuss, lagen=4):
    """Schleifziegel, kreuzweise gestapelt auf einem Brett."""
    x, y = fuss[0], fuss[1]
    netz.quader((x, y, 0.03), (0.9, 0.9, 0.06), st["Holz alt"])
    for k in range(lagen):
        z = 0.06 + 0.1 * k
        for j in range(3):
            d = (j - 1) * 0.28
            mitte, masse = ((x + d, y, z + 0.045), (0.24, 0.8, 0.09)) if k % 2 == 0 else ((x, y + d, z + 0.045), (0.8, 0.24, 0.09))
            netz.quader(mitte, masse, st["Sandstein"], ohne=("-z",))


def quaderstapel(netz, st, fuss, dreh=0.0, stoff="Sandstein"):
    """Behauene Quader auf Kanthölzern: unten zwei, oben einer quer."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for dx in (-0.45, 0.45):
        netz.quader((dx, 0, 0.05), (0.12, 1.5, 0.1), st["Holz alt"], t)
    for dy in (-0.34, 0.34):
        netz.quader((0, dy, 0.1 + 0.25), (1.3, 0.62, 0.5), st[stoff], t)
    t2 = t @ Matrix.Translation((0.05, 0, 0.6)) @ Matrix.Rotation(math.radians(84), 4, "Z")
    netz.quader((0, 0, 0.22), (1.1, 0.6, 0.44), st[stoff], t2)


def bruchsteinhaufen(netz, st, mitte, n=9, r=0.9, saat=3):
    """Haufen grober Bruchsteine in zwei Lagen, zufällig gedreht."""
    zufall = random.Random(saat)
    for k in range(n):
        oben = k >= n * 2 // 3
        w = zufall.uniform(0, 2 * math.pi)
        a = zufall.uniform(0, r * (0.45 if oben else 1.0))
        g = zufall.uniform(0.34, 0.58)
        fuss = Vector(mitte) + Vector((a * math.cos(w), a * math.sin(w), 0.22 if oben else 0.0))
        block(netz, st, fuss, (g, g * 0.8, g * 0.6), "Bruchstein" if k % 2 else "Bruchstein dunkel", zufall.uniform(0, 90))


def moertelplatz(netz, st, fuss, dreh=0.0):
    """Mörtelkasten mit Kalkmörtel und Kelle, daneben ein Sandhaufen und zwei Kalkfässer."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for dy in (-0.45, 0.45):
        netz.quader((0, dy, 0.18), (1.5, 0.08, 0.36), st["Holz alt"], t)
    for dx in (-0.71, 0.71):
        netz.quader((dx, 0, 0.18), (0.08, 0.82, 0.36), st["Holz alt"], t)
    netz.quader((0, 0, 0.15), (1.34, 0.82, 0.3), st["Steinstaub"], t, ohne=("-z",))
    netz.stab(t @ Vector((0.2, 0.1, 0.33)), t @ Vector((0.85, 0.3, 0.6)), 0.05, 0.05, st["Holz"])
    sand = t @ Vector((0.6, 1.5, 0))
    gipfel = sand + Vector((0, 0, 0.55))
    rand = [sand + Vector((0.85 * math.cos(math.radians(45 * k + 10)), 0.7 * math.sin(math.radians(45 * k + 10)), 0))
            for k in range(8)]
    for k in range(8):
        a, b = rand[k], rand[(k + 1) % 8]
        netz.vieleck_nach([a, b, gipfel], (a + b) / 2 - sand, st["Formsand"])
    for d in (-0.4, 0.25):
        bs.fass(netz, st, t @ Vector((-1.25, 1.2 + d, 0)), r=0.28, h=0.7)
        bs.rund_oben(netz, st, t @ Vector((-1.25, 1.2 + d, 0)), 0.24, 0.703, "Steinstaub")


def bankstein(netz, st, fuss, dreh=0.0, werkstueck=(1.0, 0.6, 0.45)):
    """Arbeitsbank des Steinmetzen: zwei Böcke, darauf das Werkstück, Klöpfel und Eisen; Staub darunter."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for dx in (-0.42, 0.42):
        netz.quader((dx, 0, 0.32), (0.22, 0.7, 0.64), st["Holz alt"], t)
    netz.quader((0, 0, 0.64 + werkstueck[2] / 2), werkstueck, st["Sandstein"], t)
    z = 0.64 + werkstueck[2]
    netz.stab(t @ Vector((0.1, 0.1, z + 0.04)), t @ Vector((0.45, 0.2, z + 0.04)), 0.05, 0.05, st["Holz"])      # Klöpfel
    netz.quader(t @ Vector((0.05, 0.08, z + 0.08)), (0.16, 0.16, 0.16), st["Holz"])
    netz.stab(t @ Vector((-0.3, -0.1, z + 0.02)), t @ Vector((-0.05, -0.18, z + 0.02)), 0.025, 0.025, st["Eisen"])
    staub(netz, st, t @ Vector((0, 0, 0)), 0.9, saat=int(fuss[0] * 10 + fuss[1]))


def dreibein(netz, st, mitte, hoehe=4.4, last=(0.9, 0.6, 0.5), z_last=0.7):
    """Dreibein aus Stangen mit Flaschenzug, Seil, Steinzange und einem hängenden Block; Haspel an zwei Beinen."""
    holz = st["Fachwerk"]
    x, y = mitte[0], mitte[1]
    gipfel = Vector((x, y, hoehe))
    fuesse = [Vector((x + 1.5 * math.cos(math.radians(w)), y + 1.5 * math.sin(math.radians(w)), 0)) for w in (100, 220, 340)]
    for f in fuesse:
        netz.stab(f, gipfel + (gipfel - f).normalized() * 0.25, 0.14, 0.14, holz)
    netz.quader(gipfel, (0.24, 0.24, 0.2), st["Eisen"])
    netz.quader((x, y, hoehe - 0.55), (0.14, 0.24, 0.42), st["Holz"])                 # Flasche mit Rollen
    netz.stab((x, y, hoehe - 0.78), (x, y, z_last + last[2] + 0.35), 0.035, 0.035, st["Seil"])
    for s in (-1, 1):                                                                  # Steinzange
        netz.stab((x, y + s * 0.03, z_last + last[2] + 0.38), (x + s * last[0] * 0.45, y + s * 0.03, z_last + last[2] * 0.6), 0.04, 0.04,
                  st["Eisen"], quer=(1, 0, 0))
    block(netz, st, (x, y, z_last), last)
    # Haspel zwischen zwei Beinen, das Seil läuft vom Gipfel hinab
    a, b = (fuesse[1].lerp(gipfel, 0.25), fuesse[2].lerp(gipfel, 0.25))
    netz.stab(a, b, 0.12, 0.12, st["Holz"], quer=(0, 0, 1), mehr=0.2)
    m = (a + b) / 2
    netz.stab(m, (x, y, hoehe - 0.6), 0.03, 0.03, st["Seil"])
    netz.stab(a + (a - b).normalized() * 0.1, a + (a - b).normalized() * 0.1 + Vector((0, 0, -0.35)), 0.04, 0.04, st["Eisen"])


def steinsaege(netz, st, fuss, dreh=0.0):
    """Steinsäge: Galgen aus zwei Pfosten, daran hängt der Rahmen mit dem Blatt, das sich in einen großen Block frisst."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    holz = st["Fachwerk"]
    for dx in (-1.3, 1.3):
        netz.quader((dx, 0, 1.25), (0.16, 0.16, 2.5), holz, t)
        netz.quader((dx, 0, 0.06), (0.22, 0.8, 0.12), holz, t)
    netz.quader((0, 0, 2.55), (2.9, 0.18, 0.18), holz, t)
    netz.quader((0, 0, 0.4), (1.5, 1.0, 0.8), st["Sandstein"], t)                     # Block
    netz.quader((0.1, 0, 0.805), (1.3, 0.04, 0.01), st["Fuge"], t)                     # Schnitt
    for dx in (-0.85, 0.85):
        netz.quader((dx, 0, 1.75), (0.05, 0.05, 1.5), st["Seil"], t)
        netz.quader((dx, 0, 0.98), (0.1, 0.1, 0.36), st["Holz"], t)
    netz.quader((0, 0, 1.12), (1.9, 0.08, 0.08), st["Holz"], t)
    netz.quader((0, 0, 0.8), (1.8, 0.015, 0.14), st["Eisen"], t, ohne=("-z",))       # Blatt, steckt im Schnitt
    bs.fass(netz, st, t @ Vector((1.0, 0.75, 0)), r=0.24, h=0.5, reifen=(0.1, 0.38))
    bs.rund_oben(netz, st, t @ Vector((1.0, 0.75, 0)), 0.2, 0.45, "Wasser")
    staub(netz, st, t @ Vector((0.2, -0.2, 0)), 1.2, saat=7)


def schleifstein(netz, st, fuss, dreh=0.0):
    """Großer Schleifstein im Holzgestell mit Kurbel und Wassertrog darunter."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for dy in (-0.28, 0.28):
        netz.quader((0, dy, 0.5), (1.1, 0.1, 0.12), st["Holz alt"], t)
        for dx in (-0.45, 0.45):
            netz.quader((dx, dy, 0.28), (0.1, 0.1, 0.56), st["Holz alt"], t)
    netz.quader((0, 0, 0.42), (0.8, 0.4, 0.16), st["Holz"], t)
    netz.quader((0, 0, 0.505), (0.7, 0.3, 0.01), st["Wasser"], t, ohne=("-z",))
    profil = [(0.48 * math.cos(math.radians(15 + 30 * k)), 0.98 + 0.48 * math.sin(math.radians(15 + 30 * k))) for k in range(12)]
    netz.prisma(profil, -0.11, 0.11, st["Haustein"], matrix=t @ Matrix.Rotation(math.radians(90), 4, "Z"))
    netz.stab(t @ Vector((0, -0.4, 0.98)), t @ Vector((0, 0.45, 0.98)), 0.06, 0.06, st["Eisen"], quer=(0, 0, 1))
    netz.stab(t @ Vector((0, 0.45, 0.98)), t @ Vector((0, 0.45, 0.7)), 0.04, 0.04, st["Eisen"])


def saeule(netz, st, a, b, r=0.2, stoff="Sandstein"):
    """Säule von a nach b: achteckiger Schaft, quadratischer Fuß und Kapitell."""
    a, b = Vector(a), Vector(b)
    laenge = (b - a).length
    t = Matrix.Translation(a) @ bs._richtung(b - a, (0, 0, 1))
    netz.quader((0.1, 0, 0), (0.2, 2.8 * r, 2.8 * r), st[stoff], t)
    netz.quader((laenge - 0.13, 0, 0), (0.26, 3.0 * r, 3.0 * r), st[stoff], t)
    profil = [(r * math.cos(math.radians(22.5 + 45 * k)), r * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    netz.prisma(profil, 0.2, laenge - 0.26, st[stoff], matrix=t)


def taufbecken(netz, st, boden):
    """Achteckiges Taufbecken: Stufe, Fuß, Schaft und Becken mit dunkler Mulde."""
    bs.achteck(netz, st, boden, 0.5, 0.0, 0.14, "Bruchstein dunkel")
    bs.achteck(netz, st, boden, 0.3, 0.14, 0.3)
    bs.achteck(netz, st, boden, 0.2, 0.3, 0.7)
    bs.achteck(netz, st, boden, 0.58, 0.7, 1.02)
    bs.rund_oben(netz, st, boden, 0.46, 1.023, "Fuge")


def figur(netz, st, boden, dreh=0.0):
    """Steinfigur in Arbeit: unten noch der rohe Block, oben schon Gewand, Schultern und Kopf."""
    t = Matrix.Translation(Vector(boden)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    sand = st["Sandstein"]
    netz.quader((0, 0, 0.35), (0.7, 0.8, 0.7), sand, t)
    netz.prisma([(-0.3, 0.7), (0.3, 0.7), (0.22, 1.5), (-0.22, 1.5)], -0.17, 0.17, sand, matrix=t)   # Gewand
    netz.quader((0, 0, 1.6), (0.3, 0.52, 0.2), sand, t)                                            # Schultern
    netz.quader((0, 0, 1.73), (0.1, 0.1, 0.06), sand, t)
    netz.quader((0.01, 0, 1.88), (0.2, 0.18, 0.24), sand, t)                                       # Kopf
    netz.stab(t @ Vector((0.05, 0.22, 1.58)), t @ Vector((0.16, 0.12, 1.15)), 0.1, 0.1, sand)       # Arm vor der Brust
    staub(netz, st, t @ Vector((0, 0, 0)), 1.0, saat=11)


def grabmal(netz, st, fuss, dreh=0.0):
    """Hohes Grabmal (Epitaph) mit Giebel auf einem breiten Sockel."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, 0.15), (1.1, 0.55, 0.3), st["Haustein"], t)
    bs.grabstein(netz, st, t @ Vector((0, 0, 0.3)), dreh=dreh, breite=0.8, hoehe=1.6, dicke=0.24, form="giebel")


def ware_an_der_gasse(netz, st, mit_muehlstein=True):
    """Grabsteine und Mühlsteine zum Verkauf auf dem Vorplatz rechts vom Tor."""
    bs.grabstein(netz, st, (1.5, -5.7, 0), breite=0.6, hoehe=0.95)
    bs.grabstein(netz, st, (2.4, -5.75, 0), dreh=-8, breite=0.55, hoehe=1.1, form="giebel")
    if mit_muehlstein:
        muehlstein(netz, st, (3.5, -5.6, 0), dreh=90)
        muehlsteine_liegend(netz, st, (4.7, -5.8, 0))


# ─────────────────────────────────────────────────────────────────────────────
#  Stufen
# ─────────────────────────────────────────────────────────────────────────────

def maurerei(netz, st):
    """Stufe 1: Vordach an der Werkstatt mit zwei Bänken, Quaderstapel, Bruchsteine und Rohblöcke im Hof,
    Grabsteine, Mühlsteine und Schleifziegel an der Gasse, Trockenmauer."""
    holz = st["Fachwerk"]
    xa, ya, yb = 3.6, -4.0, 0.8
    for y in (ya + 0.1, (ya + yb) / 2, yb - 0.1):
        netz.stab((xa, y, 0), (xa, y, 2.55), 0.15, 0.15, holz)
    netz.stab((xa, ya, 2.5), (xa, yb, 2.5), 0.15, 0.15, holz)
    bs.pultdach(netz, st, X1, xa + 0.1, ya, yb, 3.3, 2.55)
    bankstein(netz, st, (2.1, -2.7, 0), dreh=90)
    bankstein(netz, st, (2.2, -0.2, 0), dreh=90, werkstueck=(1.2, 0.5, 0.18))
    quaderstapel(netz, st, (5.9, -2.4, 0))
    quaderstapel(netz, st, (5.9, 0.6, 0), dreh=90)
    block(netz, st, (2.6, 4.4, 0), (1.6, 1.0, 0.9), "Haustein", 12)           # Rohblöcke aus dem Steinbruch
    block(netz, st, (2.9, 2.9, 0), (1.2, 0.9, 0.7), "Sandstein", -20)
    bruchsteinhaufen(netz, st, (5.6, 4.6, 0), n=12, r=1.1)
    moertelplatz(netz, st, (5.0, 2.2, 0), dreh=90)
    ware_an_der_gasse(netz, st)
    ziegelstapel(netz, st, (6.1, -5.7, 0))
    bs.trockenmauer(netz, st, [(7.76, -4.8), (7.76, 6.76), (-7.76, 6.76), (-7.76, Y1 + 0.3)])


def steinschneiderei(netz, st):
    """Stufe 2: steinerner Anbau mit dem Giebel zur Gasse (darin Schleifstein und Säge), Steinsäge im Hof,
    Dreibein mit Flaschenzug und hängendem Block, Quader, Ware an der Gasse, Trockenmauer."""
    ax0, ax1, ay0, ay1 = X1, 4.8, 0.6, 4.4
    cx, breite = (ax0 + ax1) / 2, ax1 - ax0
    h = 3.0
    netz.quader((cx, (ay0 + ay1) / 2, h / 2), (breite, ay1 - ay0, h), st["Bruchstein"], ohne=("+z", "-x"))
    bs.gesims(netz, st, ax0 + 0.08, ax1, ay0, ay1, h - 0.18, hoehe=0.18, vor=0.08)
    for x, sx in ((ax1, 1),):
        for y, sy in ((ay0, -1), (ay1, 1)):
            bs.eckquader(netz, st, x, y, sx, sy, 0.0, h - 0.18)
    M_vorn = bs.wandmatrix((cx, ay0, 0), "vorne")
    M_rechts = bs.wandmatrix((ax1, (ay0 + ay1) / 2, 0), "rechts")
    bs.fugen(netz, M_vorn, breite - 0.02, 0.0, h - 0.18, st)
    bs.fugen(netz, M_rechts, ay1 - ay0 - 0.02, 0.0, h - 0.18, st)
    tor = Oeffnung("tuer", -0.7, 0.0, 1.3, 2.2)
    bs.tuer(netz, M_vorn, tor, st)
    netz.quader((tor.u, -0.03, 2.31), (1.7, 0.1, 0.22), st["Sandstein"], M_vorn, ohne=("+y",))
    bs.gewaende(netz, M_vorn, Oeffnung("fenster", 1.2, 0.95, 0.8, 1.1), st)
    for u in (-1.0, 1.0):
        bs.gewaende(netz, M_rechts, Oeffnung("fenster", u, 0.95, 0.8, 1.1), st)
    drehung = Matrix.Translation((cx, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(netz, st, ay0, ay1, -breite / 2, breite / 2, h, 42, traufe=0.3, ortgang=0.15, matrix=drehung,
                         **bs.ZIEGEL)
    bs.steingiebel(netz, st, bs.wandmatrix((cx, ay0, h), "vorne"), breite, breite / 2 * dach["tan"], [])
    # Hof: Steinsäge, Dreibein, Quader, Bruchsteine, Ware
    steinsaege(netz, st, (2.6, -2.4, 0))
    dreibein(netz, st, (5.8, -1.6, 0))
    schleifstein(netz, st, (1.5, -0.4, 0), dreh=90)
    quaderstapel(netz, st, (6.4, 2.2, 0), dreh=90)
    block(netz, st, (6.3, 4.6, 0), (1.5, 1.0, 0.85), "Haustein", 8)
    bruchsteinhaufen(netz, st, (2.4, 5.6, 0), n=7, r=0.8, saat=5)
    ware_an_der_gasse(netz, st)
    ziegelstapel(netz, st, (6.1, -5.7, 0))
    bs.trockenmauer(netz, st, [(7.76, -4.8), (7.76, 6.76), (-7.76, 6.76), (-7.76, Y1 + 0.3)])


def steinmetzhuette(netz, st):
    """Stufe 3: offene Bauhütte aus Holz mit Ziegeldach (Giebel zur Gasse), darin die Steinfigur in Arbeit,
    eine Säule auf Kanthölzern und eine Bank. Vorn Schaustücke: Taufbecken, Grabmal, Säule, Grabstein.
    Im Hof das Dreibein mit hängendem Block, Rohblöcke und Bruchsteine, Trockenmauer."""
    holz = st["Fachwerk"]
    hx0, hx1, hy0, hy1 = 1.2, 7.3, -4.6, 1.6
    cx, breite = (hx0 + hx1) / 2, hx1 - hx0
    h = 3.4
    netz.quader((cx, (hy0 + hy1) / 2, 0.02), (breite - 0.1, hy1 - hy0 - 0.1, 0.04), st["Steinstaub"], ohne=("-z",))
    ys = [hy0 + (hy1 - hy0) * k / 3 for k in range(4)]
    for y in ys:
        for x in (hx0 + 0.1, hx1 - 0.1):
            netz.stab((x, y, 0), (x, y, h), 0.2, 0.2, holz)
    for x in (hx0 + 0.1, hx1 - 0.1):
        netz.stab((x, hy0, h - 0.1), (x, hy1, h - 0.1), 0.22, 0.22, holz)
        for y in ys[:-1]:
            netz.stab((x, y + 0.1, h - 0.8), (x, y + 0.7, h - 0.15), 0.12, 0.12, holz)
    netz.quader((cx, hy1, h / 2), (breite - 0.2, 0.08, h), st["Holz alt"])        # Rückwand, endet in den Eckpfosten
    drehung = Matrix.Translation((cx, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(netz, st, hy0, hy1, -breite / 2, breite / 2, h, 40, traufe=0.3, ortgang=0.3, matrix=drehung,
                         **bs.ZIEGEL)
    hoch = breite / 2 * dach["tan"]
    for y, seite in ((hy0, "vorne"), (hy1, "hinten")):
        M = bs.wandmatrix((cx, y, h), seite)
        bs.platte(netz, M, [(-breite / 2, 0), (breite / 2, 0), (0, hoch)], 0, 0.08, st["Holz alt"])
    netz.stab((cx - breite / 2, hy0, h + 0.1), (cx + breite / 2, hy0, h + 0.1), 0.2, 0.2, holz)    # Bundbalken vorn
    # in der Hütte
    figur(netz, st, (3.0, -3.3, 0.04), dreh=-30)
    bankstein(netz, st, (5.7, -2.6, 0.04), dreh=90, werkstueck=(0.9, 0.7, 0.55))
    for x in (3.4, 6.2):
        netz.quader((x, -0.1, 0.12), (0.2, 0.9, 0.16), st["Holz alt"])
    saeule(netz, st, (2.9, -0.1, 0.48), (6.7, -0.1, 0.48), r=0.22)
    # Schaustücke vorn an der Gasse
    taufbecken(netz, st, Vector((1.8, -5.8, 0)))
    grabmal(netz, st, (3.3, -5.85, 0))
    saeule(netz, st, (4.7, -5.75, 0), (4.7, -5.75, 2.5), r=0.2)
    bs.grabstein(netz, st, (5.8, -5.75, 0), breite=0.6, hoehe=1.0)
    muehlsteine_liegend(netz, st, (6.9, -5.75, 0), r=0.5)
    # Hof hinter der Hütte
    dreibein(netz, st, (3.6, 4.4, 0))
    block(netz, st, (6.2, 3.6, 0), (1.6, 1.1, 0.9), "Haustein", -10)
    block(netz, st, (6.4, 5.5, 0), (1.2, 0.9, 0.8), "Sandstein", 15)
    bruchsteinhaufen(netz, st, (-3.6, 4.6, 0), n=8, r=0.9, saat=9)
    bs.trockenmauer(netz, st, [(7.76, hy1 + 0.25), (7.76, 6.76), (-7.76, 6.76), (-7.76, Y1 + 0.3)])


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
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=4.4, ausschnitt=27.0, schnell=schnell)
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
