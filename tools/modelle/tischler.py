"""Werkstatt des Tischlers – Tischlerei (Stufe 1), Drechslerei (Stufe 2), Sägewerk (Stufe 3). Fläche 16 × 14 m.

    blender -b --python tools/modelle/tischler.py        oder
    python tools/modelle/tischler.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/tischler.glb und Bilder nach docs/modelle/.

Objekte im GLB (wie beim Schmied):
    Haus        die Werkstatt mit dem Giebel zur Gasse, in jeder Stufe gleich
    Stufe1      Tischlerei: Vordach mit Hobelbank, Bretterstapel, Sägebock, Rundholz, Bretterzaun
    Stufe2      Drechslerei: Anbau mit offener Laube und Wippdrehbank, Gaube, Bretterstapel, Zaun
    Stufe3      Sägewerk: offene Sägehalle mit Gattersäge, Wasserrad mit Gerinne, Dachreiter mit
                kupfergrüner Spitze, Gaube, Holzlager
    Wappen, Fahne, Leerstand, Geruest, Anker_Tuer, Anker_Karren
Genau einer der Knoten Stufe1–Stufe3 ist sichtbar; Leerstand und Geruest sind standardmäßig aus.

Aufbau (Blender: Z oben, Gasse bei -Y; Fläche x ±8, y ±7, davon 2 m Vorplatz vorn):
    Werkstatt links (7,8 × 7,6 m): zwei Fachwerkgeschosse auf einem Bruchsteinsockel, Schieferdach mit dem
    Giebel zur Gasse, im Giebel eine Ladeluke mit Kranbalken für das Holz, unten ein breites Tor mit Rampe.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "tischler"
FLAECHE = (16.0, 14.0)
STUFEN = ("Tischlerei", "Drechslerei", "Sägewerk")

# ── Maße der Werkstatt ───────────────────────────────────────────────────────
X0, X1, Y0, Y1 = -7.2, 0.6, -5.0, 2.6
CX, CY = (X0 + X1) / 2, (Y0 + Y1) / 2
HW = (X1 - X0) / 2
Z_SOCKEL, H_EG, H_OG = 0.4, 3.0, 2.4
Z_OG = Z_SOCKEL + H_EG
Z_TRAUFE = Z_OG + H_OG                      # 5,8
NEIGUNG = 52
UEBER = 0.045


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []

    # ── Sockel und Putzkörper ─────────────────────────────────────────────────
    haus.quader((CX, CY, Z_SOCKEL / 2), (X1 - X0 + 0.12, Y1 - Y0 + 0.12, Z_SOCKEL), st["Bruchstein"])
    haus.quader((CX, CY, (Z_SOCKEL + Z_TRAUFE) / 2), (X1 - X0, Y1 - Y0, Z_TRAUFE - Z_SOCKEL), st["Putz"])
    haus.quader((CX, CY, Z_OG - 0.05), (X1 - X0 + 0.08, Y1 - Y0 + 0.08, 0.16), st["Fachwerk"], ohne=("+z",))

    F = "fenster"
    lang = {"vorne": X1 - X0 + 2 * UEBER, "hinten": X1 - X0 + 2 * UEBER, "rechts": Y1 - Y0, "links": Y1 - Y0}
    mitte = {"vorne": (CX, Y0), "hinten": (CX, Y1), "rechts": (X1, CY), "links": (X0, CY)}

    def geschoss(z, hoehe, liste_je_seite, kreuze):
        for seite, liste in liste_je_seite.items():
            M = bs.wandmatrix((*mitte[seite], z), seite)
            bs.fachwerk(haus, M, lang[seite], hoehe, liste, st, kreuze=kreuze)
            for o in liste:
                if o.art == "fenster":
                    bs.fenster(haus, M, o, st)
                oeffnungen.append((M, o))

    tor = Oeffnung("tuer", -0.9, 0.15, 2.4, 2.5)
    eg = dict(v=0.9, b=0.9, h=1.1)
    geschoss(Z_SOCKEL, H_EG, {
        "vorne": [tor, Oeffnung(F, 2.6, **eg)],
        "rechts": [Oeffnung(F, u, **eg) for u in (-2.2, 1.6)],
        "hinten": [Oeffnung(F, u, **eg) for u in (-2.0, 1.8)],
        "links": [Oeffnung(F, u, **eg) for u in (-2.0, 1.4)],
    }, kreuze=False)
    og = dict(v=0.6, b=0.85, h=1.05)
    geschoss(Z_OG, H_OG, {
        "vorne": [Oeffnung(F, u, **og) for u in (-2.4, 0.0, 2.4)],
        "rechts": [Oeffnung(F, u, **og) for u in (-2.3, 0.0, 2.3)],
        "hinten": [Oeffnung(F, u, **og) for u in (-1.8, 1.8)],
        "links": [Oeffnung(F, u, **og) for u in (-2.3, 0.0, 2.3)],
    }, kreuze=True)
    M_vorn = bs.wandmatrix((CX, Y0, Z_SOCKEL), "vorne")
    tor_fluegel(haus, st, M_vorn, tor)
    # Rampe vor dem Tor: Karren und Schubkarren rollen hinein
    rampe = [(Y0 - 1.1, 0.0), (Y0, 0.0), (Y0, Z_SOCKEL + 0.01), (Y0 - 0.25, Z_SOCKEL + 0.01)]   # 1 cm über dem Sockel
    haus.prisma(rampe, CX + tor.u - 1.4, CX + tor.u + 1.4, st["Bruchstein"])

    # ── Schieferdach mit dem First quer zur Gasse, Ladegiebel vorn, Fachwerkgiebel hinten ──
    drehung = Matrix.Translation((CX, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(haus, st, Y0, Y1, -HW, HW, Z_TRAUFE, NEIGUNG, traufe=0.35, ortgang=0.35,
                         matrix=drehung, **bs.SCHIEFER)
    hoch = HW * dach["tan"]
    ladegiebel(haus, st, bs.wandmatrix((CX, Y0, Z_TRAUFE), "vorne"), 2 * HW, hoch, oeffnungen)
    bs.fachwerkgiebel(haus, st, bs.wandmatrix((CX, Y1, Z_TRAUFE), "hinten"), 2 * HW, hoch, [])
    # Schornstein (Leimofen) auf der linken Dachfläche
    xs, ys = CX - 1.6, 0.9
    z0 = dach["oben_hinten"](-(xs - CX)) - 0.3
    haus.quader((xs, ys, (z0 + dach["z_first"] + 0.7) / 2), (0.6, 0.6, dach["z_first"] + 0.7 - z0), st["Bruchstein dunkel"])
    haus.quader((xs, ys, dach["z_first"] + 0.76), (0.78, 0.78, 0.12), st["Bruchstein"])
    zunftzeichen(haus, st, M_vorn, 2.0 + 1.0, 2.85)

    # ── Wappen, Fahne ─────────────────────────────────────────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, bs.wandmatrix((CX, Y0, Z_OG), "vorne"), 1.2, 1.15, st, groesse=0.75)
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (CX, Y1 - 0.4, dach["z_first"] - 0.3), hoehe=2.5, richtung=1)

    # ── Stufen ────────────────────────────────────────────────────────────────
    stufe1 = bs.Netz("Stufe1")
    tischlerei(stufe1, st)
    stufe2 = bs.Netz("Stufe2")
    drechslerei(stufe2, st)
    bs.gaube(stufe2, st, dach, [], x0=-3.6, x1=-2.0, y_front=-HW + 1.1)
    stufe3 = bs.Netz("Stufe3")
    saegewerk(stufe3, st)
    bs.gaube(stufe3, st, dach, [], x0=-3.6, x1=-2.0, y_front=-HW + 1.1)
    bs.dachreiter(stufe3, st, Vector((CX, -0.2, dach["z_first"])), spitze="Kupfer")

    # ── Leerstand, Gerüst, Anker ──────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for x, y in ((X0 + 0.3, Y0 - 0.15), (-2.0, Y0 - 1.3), (X1 - 0.2, Y0 - 0.15), (X0 - 0.15, -1.0), (X1 + 0.15, 1.8)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=X0 - 0.15, x1=X1 + 0.15, y_innen=Y0 - 0.3, y_aussen=Y0 - 1.2,
                  ebenen=(2.4, 4.8), hoehe_innen=7.0, hoehe_aussen=7.6, felder=3)
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

def tor_fluegel(netz, st, M, o):
    """Zweiflügeliges Brettertor in der Hausfarbe mit Z-Streben und Eisenbändern."""
    for s in (-1, 1):
        u = o.u + s * o.b / 4
        netz.quader((u, -0.01, o.v + o.h / 2), (o.b / 2 - 0.03, 0.04, o.h), st["Hausfarbe"], M, ohne=("+y",))
        for f in (0.15, 0.85):
            netz.quader((u, -0.04, o.v + o.h * f), (o.b / 2 - 0.12, 0.03, 0.12), st["Fachwerk"], M, ohne=("+y",))
        netz.stab(M @ Vector((u - s * (o.b / 4 - 0.12), -0.045, o.v + o.h * 0.15)),
                  M @ Vector((u + s * (o.b / 4 - 0.12), -0.045, o.v + o.h * 0.85)), 0.11, 0.03, st["Fachwerk"],
                  quer=M.to_3x3() @ Vector((0, 1, 0)), ohne=("+y",))
        for f in (0.3, 0.7):
            netz.quader((o.u + s * 0.25, -0.06, o.v + o.h * f), (0.4, 0.02, 0.06), st["Eisen"], M, ohne=("+y",))


def ladegiebel(netz, st, M, L, hoch, oeffnungen):
    """Giebel zur Gasse mit Ladeluke für das Holz auf dem Dachboden, Kranbalken mit Rolle und Seil,
    darüber ein kleines Fenster. Fachwerk: Schwelle, Ständer neben der Luke, Kehlbalken, Streben."""
    holz = st["Fachwerk"]
    halb = L / 2
    bs.platte(netz, M, [(-halb, 0), (halb, 0), (0, hoch)], 0, 0.3, st["Putz"])

    def rand(u):
        return hoch * (1 - abs(u) / halb)

    B = bs.B
    bs.balken(netz, M, -halb + 0.05, B / 2, halb - 0.05, B / 2, holz, vor=bs.VOR_SCHWELLE, enden=True)
    luke = Oeffnung("tuer", 0.0, 0.3, 1.2, 1.45)
    vk = luke.v + luke.h + B
    for s in (-1, 1):
        u = luke.b / 2 + B / 2
        bs.balken(netz, M, s * u, B, s * u, vk + 0.9, holz)
        a = halb * 0.62
        bs.balken(netz, M, s * a, B, s * a, rand(a + B / 2) - 0.08, holz)
        bs.balken(netz, M, s * (u + B / 2), B, s * (a - B / 2), rand(a) - 0.35, holz, vor=bs.VOR_STREBE)
    breite_k = halb * (1 - (vk + B) / hoch) - 0.05
    bs.balken(netz, M, -breite_k, vk + B / 2, breite_k, vk + B / 2, holz, vor=bs.VOR_SCHWELLE)
    bs.balken(netz, M, -luke.b / 2, luke.v - B / 2, luke.b / 2, luke.v - B / 2, holz, vor=bs.VOR_RIEGEL)
    # Ladeluke: zwei Flügel in der Hausfarbe, der rechte halb offen
    netz.quader((-luke.b / 4, -0.01, luke.v + luke.h / 2), (luke.b / 2 - 0.02, 0.04, luke.h), st["Hausfarbe"], M, ohne=("+y",))
    netz.vieleck([M @ Vector(p) for p in ((0, -0.005, luke.v), (luke.b / 2, -0.005, luke.v), (luke.b / 2, -0.005, luke.v + luke.h),
                                          (0, -0.005, luke.v + luke.h))], st["Loch"])
    offen = M @ Matrix.Translation((luke.b / 2, -0.03, luke.v + luke.h / 2)) @ Matrix.Rotation(math.radians(-70), 4, "Z")
    netz.quader((-luke.b / 4, 0, 0), (luke.b / 2 - 0.02, 0.04, luke.h), st["Hausfarbe"], offen)
    oeffnungen.append((M, luke))
    # Kranbalken mit Rolle und Seil
    v_kran = min(hoch - 0.75, vk + 1.4)
    netz.stab(M @ Vector((0, 0.2, v_kran)), M @ Vector((0, -1.25, v_kran)), 0.2, 0.2, holz, quer=M.to_3x3() @ Vector((1, 0, 0)))
    netz.stab(M @ Vector((0, -0.05, v_kran - 0.9)), M @ Vector((0, -0.75, v_kran - 0.08)), 0.12, 0.12, holz,
              quer=M.to_3x3() @ Vector((1, 0, 0)))
    netz.quader(M @ Vector((0, -1.1, v_kran - 0.22)), (0.1, 0.26, 0.26), st["Eisen"], matrix=None)
    netz.stab(M @ Vector((0, -1.1, v_kran - 0.3)), M @ Vector((0, -1.1, luke.v + 0.3)), 0.035, 0.035, st["Seil"])
    if v_kran + 0.35 + 0.6 < hoch - 0.4:
        o = Oeffnung("fenster", 0.0, v_kran + 0.35, 0.5, 0.55, laeden=False)
        bs.fenster(netz, M, o, st)
        oeffnungen.append((M, o))


def zunftzeichen(netz, st, M, u, v):
    """Zunftzeichen am Ausleger: ein Brett mit einer Säge aus Eisen."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v)), M @ Vector((u, -1.1, v)), 0.05, 0.05, eisen)
    netz.stab(M @ Vector((u, 0, v - 0.5)), M @ Vector((u, -0.8, v - 0.02)), 0.04, 0.04, eisen)
    for d in (-0.9, -0.3):
        netz.stab(M @ Vector((u, d, v)), M @ Vector((u, d, v - 0.2)), 0.02, 0.02, eisen)
    dreh = M @ Matrix.Translation((u, -0.6, v - 0.45)) @ Matrix.Rotation(math.radians(-90), 4, "Z")
    netz.prisma([(-0.42, -0.24), (0.42, -0.24), (0.42, 0.24), (-0.42, 0.24)], -0.03, 0.03, st["Holz"], matrix=dreh)
    zaehne = [(-0.34, 0.05), (0.3, 0.05), (0.3, -0.02)]          # Sägeblatt: oben gerade, unten acht Zähne
    for k in range(8):
        zaehne += [(0.3 - k * 0.08 - 0.04, -0.09), (0.3 - (k + 1) * 0.08, -0.02)]
    netz.prisma(zaehne, -0.04, 0.04, eisen, matrix=dreh)
    netz.prisma([(0.3, 0.12), (0.4, 0.12), (0.4, -0.06), (0.3, -0.06)], -0.045, 0.045, st["Fachwerk"], matrix=dreh)


def rundholz(netz, st, a, b, r=0.22):
    """Baumstamm als Achteck von a nach b, Stirnseiten in hellem Hirnholz."""
    a, b = Vector(a), Vector(b)
    t = Matrix.Translation(a) @ bs._richtung(b - a, (0, 0, 1))
    profil = [(r * math.cos(math.radians(22.5 + 45 * k)), r * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    netz.prisma(profil, 0, (b - a).length, st["Fachwerk"], matrix=t, m_enden=st["Schnittholz"])


def bretterstapel(netz, st, mitte, laenge, breite, lagen=6):
    """Luftig gestapelte Bretter zum Trocknen: Lagen mit Stapelleisten dazwischen."""
    x, y = mitte.x, mitte.y
    z = 0.15
    for dx in (-laenge * 0.4, 0, laenge * 0.4):
        netz.quader((x + dx, y, 0.07), (0.16, breite + 0.1, 0.14), st["Fachwerk"])
    for k in range(lagen):
        netz.quader((x, y, z + 0.05), (laenge - 0.06 * (k % 2), breite - 0.05 * (k % 3), 0.1), st["Schnittholz"])
        z += 0.1
        if k < lagen - 1:
            for dx in (-laenge * 0.4, 0, laenge * 0.4):
                netz.quader((x + dx, y, z + 0.025), (0.05, breite + 0.04, 0.05), st["Holz alt"])
            z += 0.05
    # Dachbretter gegen den Regen
    netz.quader((x, y, z + 0.06), (laenge + 0.3, breite + 0.3, 0.05), st["Holz alt"])


def hobelbank(netz, st, mitte, dreh=0.0):
    """Hobelbank mit Vorderzange, darauf ein Brett und ein Hobel."""
    t = Matrix.Translation(mitte) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    holz, alt = st["Holz"], st["Holz alt"]
    netz.quader((0, 0, 0.82), (2.0, 0.6, 0.1), holz, t)
    for sx in (-0.8, 0.8):
        for sy in (-0.22, 0.22):
            netz.quader((sx, sy, 0.39), (0.1, 0.1, 0.78), alt, t)
        netz.quader((sx, 0, 0.2), (0.1, 0.5, 0.08), alt, t)
    netz.quader((-0.95, -0.32, 0.8), (0.16, 0.08, 0.22), alt, t)
    netz.quader((0.1, 0.05, 0.9), (1.4, 0.24, 0.05), st["Schnittholz"], t)
    netz.quader((0.5, 0.05, 0.97), (0.28, 0.08, 0.1), holz, t)


def saegebock(netz, st, mitte, laenge=1.6):
    """Zwei Böcke mit einem Stamm darauf."""
    for dx in (-laenge * 0.35, laenge * 0.35):
        for s in (-1, 1):            # die beiden Beine je Bock leicht versetzt, sonst decken sich ihre Flächen
            netz.stab(mitte + Vector((dx + s * 0.03, s * 0.3, 0)), mitte + Vector((dx + s * 0.03, -s * 0.15, 0.8)), 0.08, 0.08,
                      st["Holz alt"])
    rundholz(netz, st, mitte + Vector((-laenge / 2 - 0.4, 0.07, 0.85)), mitte + Vector((laenge / 2 + 0.4, 0.07, 0.85)), r=0.18)


def holzlager(netz, st, mitte, n=4, laenge=4.0):
    """Liegende Stämme, unten drei, oben einer – zwischen zwei Pflöcken."""
    x, y = mitte.x, mitte.y
    r = 0.24
    for k in range(min(n, 3)):
        yy = y + (k - 1) * 2 * r
        rundholz(netz, st, (x - laenge / 2, yy, r), (x + laenge / 2, yy, r), r)
    if n > 3:
        rundholz(netz, st, (x - laenge / 2 + 0.2, y - r, 3 * r), (x + laenge / 2 - 0.3, y - r, 3 * r), r)
        rundholz(netz, st, (x - laenge / 2 + 0.4, y + r, 3 * r), (x + laenge / 2 - 0.1, y + r, 3 * r), r)
    for s in (-1, 1):
        netz.quader((x + laenge * 0.3, y + s * 3.4 * r, 0.45), (0.12, 0.12, 0.9), st["Holz alt"])


def pultdach(netz, st, x0, x1, y0, y1, z_hoch, z_tief, stoff="Holz alt", entlang="x"):
    """Einfaches Pultdach aus Brettern mit Fugen; fällt entlang x (von x0 hoch nach x1 tief)."""
    neig = math.atan2(z_hoch - z_tief, x1 - x0)
    laenge = (x1 - x0 + 0.3) / math.cos(neig)
    mitte = Vector(((x0 + x1 + 0.3) / 2, (y0 + y1) / 2, (z_hoch + z_tief) / 2 + 0.05 - 0.15 * math.tan(neig)))
    t = Matrix.Translation(mitte) @ Matrix.Rotation(neig, 4, "Y")
    netz.quader((0, 0, 0), (laenge, y1 - y0 + 0.4, 0.08), st[stoff], t)
    y = -(y1 - y0 + 0.4) / 2 + 0.32
    while y < (y1 - y0 + 0.4) / 2 - 0.1:
        netz.vieleck([t @ Vector(p) for p in ((-laenge / 2, y - 0.02, 0.045), (laenge / 2, y - 0.02, 0.045),
                                              (laenge / 2, y + 0.02, 0.045), (-laenge / 2, y + 0.02, 0.045))], st["Fachwerk"])
        y += 0.32


def tischlerei(netz, st):
    """Stufe 1: Vordach an der Werkstatt mit Hobelbank und Spänen, Bretterstapel, Sägebock, Rundholz, Zaun."""
    holz = st["Fachwerk"]
    xa, ya, yb = 3.4, -3.8, 1.6
    for y in (ya + 0.1, (ya + yb) / 2, yb - 0.1):
        netz.stab((xa, y, 0), (xa, y, 2.55), 0.15, 0.15, holz)
    netz.stab((xa, ya, 2.5), (xa, yb, 2.5), 0.15, 0.15, holz)
    pultdach(netz, st, X1, xa + 0.1, ya, yb, 3.3, 2.55)
    hobelbank(netz, st, Vector((2.0, -1.0, 0)), dreh=90)
    for k, (x, y, r) in enumerate(((2.6, 0.6, 0.35), (2.2, -2.6, 0.25))):
        netz.vieleck_nach([(x - r, y - r * 0.7, 0.01), (x + r, y - r * 0.7, 0.01), (x + r * 0.3, y + r * 0.8, 0.01),
                           (x - r * 0.8, y + r * 0.5, 0.01)], (0, 0, 1), st["Schnittholz"])
    for k in range(4):           # Bretter an der Wand
        netz.stab((X1 + 0.12 + 0.07 * k, -3.6 + 0.12 * k, 0), (X1 + 0.05 + 0.07 * k, -3.5 + 0.12 * k, 2.3), 0.22, 0.03,
                  st["Schnittholz"], quer=(1, 0, 0))
    bretterstapel(netz, st, Vector((6.0, -2.6, 0)), 3.0, 1.2)
    bretterstapel(netz, st, Vector((6.0, 0.4, 0)), 3.0, 1.2, lagen=4)
    saegebock(netz, st, Vector((5.2, -5.6, 0)))
    holzlager(netz, st, Vector((4.6, 4.8, 0)), n=4)
    bs.zaun(netz, st, [(7.8, -4.8), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)])


def drechslerei(netz, st):
    """Stufe 2: Anbau mit offener Laube zur Gasse; darin die Wippdrehbank, an der Wand gedrechselte Ware.
    Im Hof ein Bretterstapel und Rundholz, ein Zaun."""
    ax0, ax1, ay0, ay1 = 1.0, 6.6, -4.6, 1.4
    cx = (ax0 + ax1) / 2
    breite = ax1 - ax0
    y_wand = ay0 + 2.0                        # Vorderwand des geschlossenen Teils, davor die Laube
    h = 3.0
    netz.quader((cx, (ay0 + ay1) / 2, Z_SOCKEL / 2 - 0.1), (breite + 0.1, ay1 - ay0 + 0.1, Z_SOCKEL - 0.2), st["Bruchstein"])
    netz.quader((cx, (y_wand + ay1) / 2, (Z_SOCKEL + h) / 2), (breite, ay1 - y_wand, h - Z_SOCKEL), st["Putz"])
    for seite, punkt, L, liste in (("vorne", (cx, y_wand, Z_SOCKEL - 0.2), breite, [Oeffnung("tuer", 1.4, 0.15, 1.0, 2.0),
                                                                                    Oeffnung("fenster", -1.3, 0.9, 1.2, 1.0)]),
                                   ("rechts", (ax1, (y_wand + ay1) / 2, Z_SOCKEL - 0.2), ay1 - y_wand, [Oeffnung("fenster", 0.0, 0.9, 0.9, 1.0)]),
                                   ("hinten", (cx, ay1, Z_SOCKEL - 0.2), breite, [Oeffnung("fenster", 0.0, 0.9, 0.9, 1.0)])):
        M = bs.wandmatrix(punkt, seite)
        bs.fachwerk(netz, M, L + (0.09 if seite != "rechts" else 0), h - Z_SOCKEL + 0.2, liste, st)
        for o in liste:
            (bs.tuer if o.art == "tuer" else bs.fenster)(netz, M, o, st)
    # Laube: Pfosten mit Kopfbändern, Bretterboden
    for x in (ax0 + 0.1, cx, ax1 - 0.1):
        netz.stab((x, ay0 + 0.1, 0), (x, ay0 + 0.1, h), 0.16, 0.16, st["Fachwerk"])
    netz.stab((ax0, ay0 + 0.1, h - 0.08), (ax1, ay0 + 0.1, h - 0.08), 0.18, 0.18, st["Fachwerk"])
    for x, s in ((ax0 + 0.1, 1), (cx, -1), (cx, 1), (ax1 - 0.1, -1)):
        netz.stab((x, ay0 + 0.1, h - 0.7), (x + s * 0.6, ay0 + 0.1, h - 0.12), 0.1, 0.1, st["Fachwerk"])
    netz.quader((cx, (ay0 + y_wand) / 2, 0.12), (breite, y_wand - ay0, 0.24), st["Holz alt"])   # höher als der Sockel
    # Wippdrehbank: Bank, Werkstück, Schnur und federnde Stange
    bank = Vector((cx - 0.6, ay0 + 1.1, 0))
    for dx in (-0.7, 0.7):
        netz.quader(bank + Vector((dx, 0, 0.55)), (0.12, 0.5, 0.9), st["Holz alt"])
    netz.quader(bank + Vector((0, 0, 1.05)), (1.9, 0.18, 0.12), st["Holz"])
    rundholz(netz, st, bank + Vector((-0.35, 0, 1.25)), bank + Vector((0.35, 0, 1.25)), r=0.07)
    stange_fuss = Vector((cx - 0.6, y_wand - 0.05, h - 0.3))
    stange_kopf = bank + Vector((0, -0.3, h - 0.25))
    netz.stab(stange_fuss, stange_kopf, 0.06, 0.06, st["Holz"])
    netz.stab(stange_kopf, bank + Vector((0, 0, 1.25)), 0.015, 0.015, st["Seil"])
    netz.quader(bank + Vector((0, 0.35, 0.12)), (0.5, 0.12, 0.04), st["Holz alt"])
    # Ware an der Wand der Laube: Stöcke und Schalen
    for k in range(5):
        x = cx + 0.8 + 0.25 * k
        netz.stab((x, y_wand - 0.08, 0.2), (x + 0.05, y_wand - 0.12, 1.4 + 0.08 * (k % 2)), 0.05, 0.05, st["Holz"])
    netz.quader((cx + 1.6, y_wand - 0.15, 1.6), (1.4, 0.25, 0.05), st["Holz alt"])
    for k in range(3):
        bs.fass(netz, st, Vector((cx + 1.15 + 0.45 * k, y_wand - 0.15, 1.63)), r=0.1, h=0.12, reifen=(), stoff="Schnittholz")
    # Dach über Laube und Werkstatt, Giebel zur Gasse
    drehung = Matrix.Translation((cx, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(netz, st, ay0, ay1, -breite / 2, breite / 2, h, 45, traufe=0.3, ortgang=0.3, matrix=drehung,
                         **bs.SCHIEFER)
    hoch = breite / 2 * dach["tan"]
    for y, seite in ((ay0, "vorne"), (ay1, "hinten")):
        bs.fachwerkgiebel(netz, st, bs.wandmatrix((cx, y, h), seite), breite, hoch, [])
    bretterstapel(netz, st, Vector((5.0, 3.2, 0)), 3.0, 1.2)
    holzlager(netz, st, Vector((-3.2, 4.9, 0)), n=5, laenge=4.4)
    bs.zaun(netz, st, [(7.8, ay1 + 0.2), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)])


def wasserrad(netz, st, mitte, r=1.7, breite=0.7, speichen=10, achse="y"):
    """Oberschlächtiges Wasserrad: zwei Kränze, Schaufeln, Speichen und Welle. `achse`: "y" oder "x"."""
    holz, alt = st["Holz"], st["Holz alt"]
    B = Matrix.Translation(mitte) @ (Matrix.Rotation(math.radians(90), 4, "Z") if achse == "x" else Matrix.Identity(4))
    quer = B.to_3x3() @ Vector((0, 1, 0))
    for s in (-1, 1):
        y = s * breite / 2
        for k in range(speichen):
            a0, a1 = 2 * math.pi * k / speichen, 2 * math.pi * (k + 1) / speichen
            stueck = [(r * math.cos(a0), r * math.sin(a0)), (r * math.cos(a1), r * math.sin(a1)),
                      ((r - 0.18) * math.cos(a1), (r - 0.18) * math.sin(a1)), ((r - 0.18) * math.cos(a0), (r - 0.18) * math.sin(a0))]
            netz.prisma([(-px, pz) for px, pz in stueck], y - 0.05, y + 0.05, holz,
                        matrix=B @ Matrix.Rotation(math.radians(90), 4, "Z"))
        for k in range(0, speichen, 2):
            a = 2 * math.pi * (k + 0.25 * s) / speichen          # Speichen der beiden Kränze leicht versetzt
            netz.stab(B @ Vector((0, y, 0)), B @ Vector(((r - 0.1) * math.cos(a), y, (r - 0.1) * math.sin(a))), 0.08, 0.06, alt,
                      quer=quer)
    for k in range(speichen):
        a = 2 * math.pi * (k + 0.5) / speichen
        innen = B @ Vector(((r - 0.15) * math.cos(a), 0, (r - 0.15) * math.sin(a)))
        aussen = B @ Vector(((r + 0.12) * math.cos(a), 0, (r + 0.12) * math.sin(a)))
        netz.stab(innen, aussen, 0.04, breite + 0.06, alt, quer=quer)
    netz.stab(B @ Vector((0, -breite / 2 - 0.5, 0)), B @ Vector((0, breite / 2 + 0.3, 0)), 0.16, 0.16, st["Fachwerk"])


def gerinne(netz, st, a, b, breite=0.6, frei=0.0):
    """Hölzernes Gerinne auf Stelzen: Boden, zwei Seitenbretter, Wasser darin. Die letzten `frei` Meter
    vor dem Ende stehen ohne Stelzen (über dem Wasserrad)."""
    a, b = Vector(a), Vector(b)
    t = Matrix.Translation(a) @ bs._richtung(b - a, (0, 0, 1))
    L = (b - a).length
    netz.quader((L / 2, 0, -0.05), (L, breite, 0.08), st["Holz"], t)
    for s in (-1, 1):
        netz.quader((L / 2, s * breite / 2, 0.12), (L, 0.06, 0.32), st["Holz"], t)
    netz.vieleck([t @ Vector(p) for p in ((0, -breite / 2 + 0.04, 0.18), (L, -breite / 2 + 0.04, 0.18),
                                          (L, breite / 2 - 0.04, 0.18), (0, breite / 2 - 0.04, 0.18))], st["Wasser"], immer=True)
    n = max(2, int((L - frei) / 2.5))
    for k in range(n + 1):
        p = a.lerp(b, k / n * (L - frei) / L)
        for s in (-1, 1):
            netz.stab((p.x, p.y + s * breite / 2, 0), (p.x, p.y + s * breite / 2, p.z - 0.1), 0.12, 0.12, st["Holz alt"])


def saegewerk(netz, st):
    """Stufe 3: offene Sägehalle mit Gattersäge und Stamm auf dem Wagen, angetrieben vom Wasserrad im Hof
    (Gerinne auf Stelzen), Holzlager und Bretterstapel."""
    holz = st["Fachwerk"]
    hx0, hx1, hy0, hy1 = 1.2, 6.2, -4.8, 2.8
    cx = (hx0 + hx1) / 2
    breite = hx1 - hx0
    h = 3.4
    netz.quader((cx, (hy0 + hy1) / 2, 0.08), (breite, hy1 - hy0, 0.16), st["Holz alt"])
    ys = [hy0 + (hy1 - hy0) * k / 4 for k in range(5)]
    for y in ys:
        for x in (hx0 + 0.1, hx1 - 0.1):
            netz.stab((x, y, 0), (x, y, h), 0.18, 0.18, holz)
    for x in (hx0 + 0.1, hx1 - 0.1):
        netz.stab((x, hy0, h - 0.1), (x, hy1, h - 0.1), 0.2, 0.2, holz)
        for y in ys[:-1]:
            netz.stab((x, y + 0.1, h - 0.75), (x, y + 0.65, h - 0.15), 0.1, 0.1, holz)
    # Rückwand und halbhohe Bretterwand rechts gegen das Wetter
    netz.quader((cx, hy1 - 0.05, h / 2), (breite, 0.1, h), st["Holz alt"])
    netz.quader((hx1 - 0.08, (hy0 + hy1) / 2, 0.7), (0.08, hy1 - hy0, 1.4), st["Holz alt"])
    drehung = Matrix.Translation((cx, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(netz, st, hy0, hy1, -breite / 2, breite / 2, h, 40, traufe=0.3, ortgang=0.3, matrix=drehung,
                         **bs.SCHIEFER)
    hoch = breite / 2 * dach["tan"]
    for y, seite in ((hy0, "vorne"), (hy1, "hinten")):
        M = bs.wandmatrix((cx, y, h), seite)
        bs.platte(netz, M, [(-breite / 2, 0), (breite / 2, 0), (0, hoch)], 0, 0.08, st["Holz alt"])
    # Gattersäge: Rahmen, Sägeblätter, Stamm auf dem Wagen durch die Halle
    gx, gy = cx, -0.6
    for s in (-1, 1):
        netz.stab((gx + s * 0.7, gy, 0), (gx + s * 0.7, gy, 3.1), 0.22, 0.22, holz)
    for z in (0.5, 2.9):
        netz.stab((gx - 0.9, gy, z), (gx + 0.9, gy, z), 0.18, 0.18, holz)
    netz.quader((gx, gy, 1.85), (1.1, 0.12, 0.9), st["Holz"])           # Gatter mit den Blättern
    for k in range(4):
        netz.quader((gx - 0.36 + 0.24 * k, gy, 1.4), (0.02, 0.05, 1.6), st["Eisen"])
    for s in (-1, 1):
        netz.stab((gx + s * 0.35, hy0 + 0.3, 0.45), (gx + s * 0.35, hy1 - 0.3, 0.45), 0.16, 0.12, holz)
    netz.quader((gx, -2.2, 0.6), (1.0, 2.2, 0.12), st["Holz"])
    rundholz(netz, st, (gx, hy0 - 0.2, 0.92), (gx, gy + 0.2, 0.92), r=0.3)
    for k, y in enumerate((gy + 0.6, gy + 1.2, gy + 1.8)):
        netz.quader((gx, y + 0.3, 0.75 + 0.07 * k), (0.62, 0.55, 0.06), st["Schnittholz"])
    # Wasserrad rechts neben der Halle (quer zur Gasse, gut zu sehen), Gerinne von hinten, Welle in die Halle
    rad = Vector((7.1, 0.0, 1.86))          # die Schaufeln tauchen ins Becken, nicht in den Boden
    wasserrad(netz, st, rad, achse="x")
    netz.stab((hx1, rad.y, rad.z), (rad.x - 0.3, rad.y, rad.z), 0.14, 0.14, holz)
    gerinne(netz, st, (7.1, 6.5, 3.95), (7.1, 0.5, 3.8), frei=2.2)
    netz.quader((7.15, 0.0, 0.14), (1.1, 3.4, 0.24), st["Wasser"])
    netz.quader((7.15, 0.0, 0.05), (1.3, 3.7, 0.1), st["Bruchstein"])
    # Holzlager und Bretter
    holzlager(netz, st, Vector((-3.4, 4.0, 0)), n=5, laenge=4.4)
    bretterstapel(netz, st, Vector((-3.4, 6.0, 0)), 3.2, 1.0, lagen=4)
    holzlager(netz, st, Vector((4.3, -5.9, 0)), n=3, laenge=4.6)
    bs.zaun(netz, st, [(7.8, hy1 + 0.2), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)])


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
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=4.2, ausschnitt=27.0, schnell=schnell)
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
