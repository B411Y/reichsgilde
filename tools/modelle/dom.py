"""Dom – gotische Hauptkirche der Stadt mit einem Westturm (Sonderbau). Belegte Fläche 16 × 40 m.

    blender -b --python tools/modelle/dom.py        oder
    python tools/modelle/dom.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/dom.glb und Bilder nach docs/modelle/.

Objekte im GLB (der Dom hat einen Innenraum, in den man wie bisher hineinsieht):
    Wand_vorne, Wand_hinten,   die vier Wände des Langhauses, jede mit Strebepfeilern und Fenstern innen und außen –
    Wand_links, Wand_rechts    das Spiel blendet beim Hineinsehen die Wände zur Kamera aus (wie bisher die Hülle)
    Dach                       Schieferdach mit beiden Giebeln und dem Kreuz – beim Hineinsehen aus
    Turm                       Westturm mit Portal, Rosette, Glockenstube und Helm – beim Hineinsehen aus
    Anker_Tuer                 auf den Stufen vor dem Portal
Keine Zustände und keine Hausfarbe: Der Dom gehört der Stadt (`pruefen.py … --sonderbau`).

Aufbau (Blender: Z oben, Portal und Turm zur Gasse bei -Y; Ursprung in der Mitte des Langhauses wie beim Platzhalter):
    Langhaus 10 × 22 m, Wände 12 m hoch und 0,5 m dick, innen frei von x −4,5 bis 4,5 und y −10,5 bis 10,5 (dort steht
    die Einrichtung des Spiels: Bänke, Chorstufe, Altar, Kanzel). Fünf Joche mit Strebepfeilern und Fialen, hohe
    Spitzbogenfenster mit Mittelpfosten, im Chor ein großes Fenster. Steiles Schieferdach (60°).
    Westturm 6,5 × 6 m vor dem Portal: Spitzbogenportal mit gestuftem Gewände, Fenster, Rosette, Glockenstube mit
    Schallarkaden, Eckfialen und achteckiger Helm bis 42 m.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

NAME = "dom"
FLAECHE = (16.0, 40.0)

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
HX, HY = 5.0, 11.0                   # Langhaus außen x ±5, y ±11
DICKE = 0.5
H_WAND = 12.0
NEIGUNG = 60
JOCHE = (-8.4, -4.2, 0.0, 4.2, 8.4)   # Fenster der Seitenwände (u entlang der Wand)
PFEILER = (-10.1, -6.3, -2.1, 2.1, 6.3, 10.1)
TX, TY0, TY1, TH = 3.25, -17.0, -HY, 30.0   # Turm x ±3,25, y von −17 bis −11, Mauerwerk bis 30 m


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    waende = {
        "Wand_links": ((-HX + DICKE / 2, 0), (DICKE, 2 * HY - 2 * DICKE), "links", "rechts"),
        "Wand_rechts": ((HX - DICKE / 2, 0), (DICKE, 2 * HY - 2 * DICKE), "rechts", "links"),
        "Wand_vorne": ((0, -HY + DICKE / 2), (2 * HX, DICKE), "vorne", "hinten"),
        "Wand_hinten": ((0, HY - DICKE / 2), (2 * HX, DICKE), "hinten", "vorne"),
    }
    objekte = []
    for name, ((x, y), (bx, by), aussen, innen) in waende.items():
        netz = bs.Netz(name)
        wand(netz, st, name, (x, y), (bx, by), aussen, innen)
        objekte.append(netz.objekt())
    dach = bs.Netz("Dach")
    dach_bauen(dach, st)
    turm = bs.Netz("Turm")
    turm_bauen(turm, st)
    objekte += [dach.objekt(), turm.objekt(), bs.anker("Anker_Tuer", (0.0, TY0 - 0.45, 0.3))]
    return objekte


# ─────────────────────────────────────────────────────────────────────────────
#  Spitzbogen
# ─────────────────────────────────────────────────────────────────────────────

def spitzbogen(u, v, b, h, teile=6):
    """Umriss eines Spitzbogens (gleichseitig) im Wandsystem: unten links, unten rechts, dann rechter Bogen bis zum
    Scheitel und linker Bogen hinunter. h ist die Höhe bis zum Scheitel."""
    k = v + h - b * math.sin(math.radians(60))
    rechts = [(u - b / 2 + b * math.cos(math.radians(60 * i / teile)), k + b * math.sin(math.radians(60 * i / teile)))
              for i in range(teile + 1)]
    links = [(u + b / 2 + b * math.cos(math.radians(120 + 60 * i / teile)), k + b * math.sin(math.radians(120 + 60 * i / teile)))
             for i in range(1, teile + 1)]
    return [(u - b / 2, v), (u + b / 2, v)] + rechts + links


def spitzring(netz, M, u, v, b, h, g, y0, y1, stoff):
    """Gewände um eine Spitzbogenöffnung: Ring der Breite g aus `stoff`, von y0 bis y1 vor der Wand."""
    innen = spitzbogen(u, v, b, h)
    aussen = spitzbogen(u, v, b + 2 * g, h + g)
    ring = aussen[1:] + [aussen[0], innen[0]] + list(reversed(innen[2:])) + [innen[1]]
    bs.platte(netz, M, ring, y0, y1, stoff)


def spitzfenster(netz, st, M, u, v, b, h, pfosten=1, glas="Fenster"):
    """Hohes Spitzbogenfenster: Glas, Gewände aus Sandstein, Sohlbank und Mittelpfosten mit Bogen."""
    bs.platte(netz, M, spitzbogen(u, v, b, h), -0.012, -0.002, st[glas])
    spitzring(netz, M, u, v, b, h, 0.16, -0.09, -0.004, st["Sandstein"])
    netz.quader((u, -0.07, v - 0.07), (b + 0.5, 0.14, 0.14), st["Sandstein"], M, ohne=("+y",))
    k = v + h - b * math.sin(math.radians(60))
    for i in range(1, pfosten + 1):
        uu = u - b / 2 + b * i / (pfosten + 1)
        netz.quader((uu, -0.04, (v + k) / 2 + 0.2), (0.1, 0.08, k - v + 0.4), st["Sandstein"], M, ohne=("+y",))


def innenfenster(netz, st, M, u, v, b, h):
    """Dieselbe Öffnung von innen gesehen: dunkles Glas in einer flachen Laibung."""
    bs.platte(netz, M, spitzbogen(u, v, b, h), -0.012, -0.002, st["Glas"])
    spitzring(netz, M, u, v, b, h, 0.1, -0.03, -0.004, st["Haustein"])


# ─────────────────────────────────────────────────────────────────────────────
#  Langhaus
# ─────────────────────────────────────────────────────────────────────────────

def wand(netz, st, name, mitte, groesse, aussen, innen):
    x, y = mitte
    netz.quader((x, y, H_WAND / 2), (groesse[0], groesse[1], H_WAND), st["Haustein"], ohne=("-z",))
    lang = name in ("Wand_links", "Wand_rechts")
    L = groesse[1] if lang else groesse[0]
    dx = (-1 if aussen == "links" else 1) * DICKE / 2 if lang else 0
    dy = 0 if lang else (-1 if aussen == "vorne" else 1) * DICKE / 2
    M = bs.wandmatrix((x + dx, y + dy, 0), aussen)
    Mi = bs.wandmatrix((x - dx, y - dy, 0), innen)
    # Sockel, Traufgesims, Lagerfugen
    netz.quader((0, -0.04, 0.3), (L + (0.16 if not lang else 0), 0.08, 0.6), st["Bruchstein dunkel"], M, ohne=("+y",))
    netz.quader((0, -0.08, H_WAND - 0.12), (L + (0.32 if not lang else 0), 0.16, 0.24), st["Sandstein"], M, ohne=("+y",))
    bs.fugen(netz, M, L - 0.04, 0.6, H_WAND - 0.25, st, abstand=0.5)
    if lang:
        for u in JOCHE:
            spitzfenster(netz, st, M, u, 3.0, 1.6, 7.6)
            innenfenster(netz, st, Mi, u, 3.0, 1.6, 7.6)
        for u in PFEILER:
            strebepfeiler(netz, st, M, u)
    elif name == "Wand_hinten":                                  # Chor: großes Fenster mit zwei Pfosten
        spitzfenster(netz, st, M, 0.0, 2.6, 3.2, 8.8, pfosten=2)
        innenfenster(netz, st, Mi, 0.0, 2.6, 3.2, 8.8)
        for u in (-3.5, 3.5):
            spitzfenster(netz, st, M, u, 3.6, 0.9, 5.4)
            innenfenster(netz, st, Mi, u, 3.6, 0.9, 5.4)
        for u in (-HX + 0.4, HX - 0.4):
            strebepfeiler(netz, st, M, u)
    else:                                                        # vorne: Flanken neben dem Turm, Tür innen
        for u in (-4.15, 4.15):
            spitzfenster(netz, st, M, u, 4.0, 0.8, 4.2, pfosten=0)
            innenfenster(netz, st, Mi, u, 4.0, 0.8, 4.2)
        netz.quader((0, -0.03, 1.8), (2.4, 0.06, 3.6), st["Holz alt"], Mi, ohne=("+y",))
        for v in (0.9, 2.6):
            netz.quader((0, -0.07, v), (2.2, 0.03, 0.08), st["Eisen"], Mi, ohne=("+y",))


def strebepfeiler(netz, st, M, u):
    """Strebepfeiler in zwei Absätzen mit Abdeckplatten und einer Fiale obenauf."""
    netz.quader((u, -0.55, 3.6), (0.8, 1.1, 7.2), st["Haustein"], M, ohne=("+y", "-z"))
    netz.quader((u, -0.62, 7.25), (0.9, 1.24, 0.1), st["Sandstein"], M, ohne=("+y",))
    netz.quader((u, -0.38, 9.55), (0.7, 0.76, 4.5), st["Haustein"], M, ohne=("+y", "-z"))
    netz.quader((u, -0.42, 11.85), (0.8, 0.84, 0.1), st["Sandstein"], M, ohne=("+y",))
    fuss = M @ Vector((u, -0.38, 0))
    bs.turmhelm(netz, st, Vector((fuss.x, fuss.y, 0)), 0.42, 11.9, 1.9, stoff="Haustein", n=4)


def dach_bauen(netz, st):
    """Steiles Schieferdach mit dem First von vorn nach hinten, Giebel aus Haustein, Kreuz über dem Chor."""
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")         # lokal x → Welt y
    dach = bs.satteldach(netz, st, -HY, HY, -HX, HX, H_WAND + 0.02, NEIGUNG, traufe=0.45, ortgang=0.3,
                         enden="Haustein", first_dicke=0.22, matrix=drehung, **bs.SCHIEFER)
    hoch = HX * dach["tan"]
    for y, seite in ((-HY, "vorne"), (HY, "hinten")):
        M = bs.wandmatrix((0, y, H_WAND), seite)
        bs.platte(netz, M, [(-HX, 0), (HX, 0), (0, hoch)], 0, DICKE, st["Haustein"])
        # Lagerfugen je Höhe so breit wie das Giebeldreieck: über die Schrägen hinaus schweben sie über dem Dach
        v = 0.5
        while v < hoch - 1.25:
            b = min(HX - 0.2, HX - (v + 0.014) / dach["tan"] - 0.03)
            netz.vieleck([M @ Vector(p) for p in ((-b, -0.003, v - 0.014), (b, -0.003, v - 0.014),
                                                   (b, -0.003, v + 0.014), (-b, -0.003, v + 0.014))], st["Fuge"])
            v += 0.5
    M = bs.wandmatrix((0, HY, H_WAND), "hinten")
    rosette(netz, st, M, 0.0, 3.4, 0.85)
    spitze = Vector((0, HY - 0.2, H_WAND + hoch + 0.2))
    netz.quader(spitze + Vector((0, 0, 0.9)), (0.16, 0.16, 1.8), st["Gold"])
    netz.quader(spitze + Vector((0, 0, 1.3)), (0.9, 0.16, 0.16), st["Gold"])


def rosette(netz, st, M, u, v, r, n=16):
    """Rundfenster: Glas, Ring aus Sandstein und sechs Speichen."""
    kreis = [(u + r * math.cos(2 * math.pi * k / n), v + r * math.sin(2 * math.pi * k / n)) for k in range(n)]
    bs.platte(netz, M, kreis, -0.012, -0.002, st["Fenster"])
    aussen = [(u + (r + 0.18) * math.cos(2 * math.pi * k / n), v + (r + 0.18) * math.sin(2 * math.pi * k / n)) for k in range(n)]
    ring = aussen + [aussen[0], kreis[0]] + list(reversed(kreis))
    bs.platte(netz, M, ring, -0.09, -0.004, st["Sandstein"])
    for k in range(3):
        w = math.pi * k / 3
        t = M @ Matrix.Translation((u, -0.045 - 0.006 * k, v)) @ Matrix.Rotation(-w, 4, "Y")   # je Speiche etwas
        netz.quader((0, 0, 0), (2 * r, 0.06, 0.08), st["Sandstein"], t)                        # versetzt: kein Flimmern


# ─────────────────────────────────────────────────────────────────────────────
#  Westturm
# ─────────────────────────────────────────────────────────────────────────────

def turm_bauen(netz, st):
    ym = (TY0 + TY1) / 2
    netz.quader((0, ym, TH / 2), (2 * TX, TY1 - TY0, TH), st["Haustein"])     # Rückseite über dem Dach sichtbar
    # Sockel: an den Seiten durchgehend, vorn nur neben dem Portal (die Tür reicht bis zum Boden)
    netz.quader((0, (TY0 + 0.01 + TY1 - 0.08) / 2, 0.3), (2 * TX + 0.14, TY1 - 0.08 - TY0 - 0.01, 0.6), st["Bruchstein dunkel"],
                ohne=("+y", "-y"))
    for sx in (-1, 1):
        netz.quader((sx * (TX + 2.03) / 2, TY0 - 0.03, 0.3), (TX + 0.07 - 1.96, 0.08, 0.6), st["Bruchstein dunkel"],
                    ohne=("+y",))
    for z in (8.0, 16.0, 23.0, TH - 0.25):
        netz.quader((0, ym - 0.06, z + 0.12), (2 * TX + 0.24, TY1 - TY0 + 0.14, 0.24), st["Sandstein"], ohne=("+y",))
    for sx in (-1, 1):
        bs.eckquader(netz, st, sx * TX, TY0, sx, -1, 0.6, TH - 0.3)
    seiten = {
        "vorne": bs.wandmatrix((0, TY0, 0), "vorne"),
        "links": bs.wandmatrix((-TX, ym, 0), "links"),
        "rechts": bs.wandmatrix((TX, ym, 0), "rechts"),
        "hinten": bs.wandmatrix((0, TY1, 0), "hinten"),
    }
    for seite, M in seiten.items():
        L = 2 * TX if seite in ("vorne", "hinten") else TY1 - TY0
        if seite != "hinten":
            bs.fugen(netz, M, L - 0.1, 0.6, TH - 0.4, st, abstand=0.5)
        # Glockenstube: zwei Schallarkaden je Seite
        for u in (-1.15, 1.15):
            bs.platte(netz, M, spitzbogen(u, 24.0, 1.0, 4.8), -0.012, -0.002, st["Loch"])
            spitzring(netz, M, u, 24.0, 1.0, 4.8, 0.16, -0.09, -0.004, st["Sandstein"])
            for v in (25.4, 26.8):                                  # Schallbretter
                netz.quader((u, -0.05, v), (0.95, 0.06, 0.08), st["Holz alt"], M, ohne=("+y",))
        if seite in ("links", "rechts"):
            for v in (3.0, 10.5, 17.8):                             # schmale Fenster des Treppenaufgangs
                bs.platte(netz, M, spitzbogen(0.0, v, 0.45, 1.6), -0.012, -0.002, st["Loch"])
                spitzring(netz, M, 0.0, v, 0.45, 1.6, 0.12, -0.07, -0.004, st["Sandstein"])
    M = seiten["vorne"]
    portal(netz, st, M)
    spitzfenster(netz, st, M, 0.0, 9.0, 1.5, 5.6)
    rosette(netz, st, M, 0.0, 19.6, 1.35)
    # Eckfialen und Helm
    for sx in (-1, 1):
        for sy, y in ((-1, TY0), (1, TY1)):
            bs.turmhelm(netz, st, Vector((sx * (TX - 0.35), y - sy * 0.35, 0)), 0.55, TH, 3.0, stoff="Haustein", n=4)
    bs.turmhelm(netz, st, Vector((0, ym, 0)), TX + 0.1, TH, 12.0, stoff="Schiefer", n=8)


def portal(netz, st, M):
    """Spitzbogenportal mit dreifach gestuftem Gewände, Tür aus Holz mit Eisenbändern, Bogenfeld aus Sandstein, Stufen."""
    u, b, h = 0.0, 2.6, 5.4
    k = h - b * math.sin(math.radians(60))
    bs.platte(netz, M, spitzbogen(u, 0.0, b, h), -0.02, -0.002, st["Sandstein"])            # Bogenfeld
    netz.quader((u, -0.04, k / 2), (b, 0.06, k), st["Holz alt"], M, ohne=("+y",))           # Tür
    for v in (0.8, 2.0, 3.1):
        netz.quader((u, -0.08, v), (b - 0.2, 0.03, 0.09), st["Eisen"], M, ohne=("+y",))
    netz.quader((u, -0.06, k + 0.1), (b, 0.08, 0.2), st["Sandstein"], M, ohne=("+y",))       # Türsturz
    for i, (g, tiefe, stoff) in enumerate(((0.22, 0.08, "Sandstein"), (0.22, 0.16, "Haustein"), (0.22, 0.24, "Sandstein"))):
        innen_b = b + 2 * 0.22 * i
        spitzring(netz, M, u, 0.0, innen_b, h + 0.22 * i, g, -tiefe, -0.004 - 0.002 * i, st[stoff])
    for i, (tiefe, hoehe) in enumerate(((0.9, 0.15), (0.6, 0.3), (0.3, 0.45))):
        netz.quader((u, -tiefe / 2, hoehe / 2), (4.4 - 0.3 * i, tiefe, hoehe), st["Sandstein"], M, ohne=("+y",))


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    alles = lambda o: True                                                         # noqa: E731
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=13.0, ausschnitt=50.0, schnell=schnell)
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, alles))
        return
    if "--nur-anker" in sys.argv:
        print(bs.anker_bild(NAME, alles))
        return
    print(bs.rendern(NAME, alles))
    # Einblick wie im Spiel: Dach, Turm und die beiden Wände zur Kamera aus
    print(bs.rendern(NAME + "-innen", lambda o: o.name not in ("Dach", "Turm", "Wand_vorne", "Wand_rechts")))
    if not schnell:
        print(bs.rundum(NAME, alles))
    print(bs.anker_bild(NAME, alles))
    print(bs.nachtbild(NAME, alles))


if __name__ == "__main__":
    main()
