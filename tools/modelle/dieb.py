"""Werkstatt des Diebs – Schmugglerloch (Stufe 1), Unterschlupf (Stufe 2), Diebesgilde (Stufe 3). Fläche 16 × 14 m.

    blender -b --python tools/modelle/dieb.py        oder
    python tools/modelle/dieb.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/dieb.glb und Bilder nach docs/modelle/.

Objekte im GLB (wie bei den anderen Werkstätten):
    Haus        das Diebesnest, heruntergekommen, in jeder Stufe gleich
    Stufe1      Schmugglerloch: Hof hinter einem hohen Bretterzaun mit Tor, Kisten, Fässer und Säcke mit Diebesgut,
                Falltür im Hof, kaputter Karren, Hundehütte
    Stufe2      Unterschlupf: dazu ein Bretterverschlag an der Hauswand, Fluchtleiter zum Obergeschoss,
                Wäscheleine mit Verkleidungen, Taubenschlag, Gaube
    Stufe3      Diebesgilde: Verschlag und Leiter, Gildenhaus im hinteren Hof, hölzerner Ausguck, Gaube,
                Dachreiter mit nachtblauer Spitze
    Wappen, Fahne, Leerstand, Geruest, Anker_Tuer, Anker_Karren
Genau einer der Knoten Stufe1–Stufe3 ist sichtbar; Leerstand und Geruest sind standardmäßig aus.

Aufbau (Blender: Z oben, Gasse bei -Y; Fläche x ±8, y ±7, davon 2 m Vorplatz vorn):
    Diebesnest links (7,2 × 6,4 m), wie bisher im Spiel aus verwittertem, geflicktem Holz: Erdgeschoss aus grobem
    Bruchstein mit vergitterten Fenstern, darüber ein vorkragendes Obergeschoss aus Brettern mit schiefen Läden,
    einzelne Fenster vernagelt (wie bisher im Spiel). Morsches Schindeldach mit Flicken und dem Giebel zur Gasse,
    im Giebel eine Ladeluke mit Kranbalken, schiefer Schornstein. Trübe Laterne über der Tür, Kellerluke zur Gasse.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "dieb"
FLAECHE = (16.0, 14.0)
STUFEN = ("Schmugglerloch", "Unterschlupf", "Diebesgilde")

# ── Maße des Diebesnests ─────────────────────────────────────────────────────
X0, X1, Y0, Y1 = -7.2, 0.0, -5.0, 1.4
CX, CY = (X0 + X1) / 2, (Y0 + Y1) / 2
HW = (X1 - X0) / 2
VOR = 0.3                                   # das Obergeschoss kragt zur Gasse vor
Z_SOCKEL, H_EG, H_OG = 0.3, 2.8, 2.4
Z_OG = Z_SOCKEL + H_EG
Z_TRAUFE = Z_OG + H_OG                      # 5,5
NEIGUNG = 52


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []
    yo = Y0 - VOR

    # ── Sockel, Erdgeschoss aus Bruchstein, Obergeschoss aus Brettern ─────────
    haus.quader((CX, CY, Z_SOCKEL / 2), (X1 - X0 + 0.12, Y1 - Y0 + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    haus.quader((CX, CY, Z_SOCKEL + H_EG / 2), (X1 - X0, Y1 - Y0, H_EG), st["Bruchstein"], ohne=("+z",))
    haus.quader((CX, (yo + Y1) / 2, Z_OG + H_OG / 2), (X1 - X0, Y1 - yo, H_OG), st["Bretter"])
    haus.quader((CX, yo - 0.04, Z_OG + 0.08), (X1 - X0 + 0.1, 0.1, 0.18), st["Fachwerk"], ohne=("+y",))
    for x in (X0 + 0.25, CX - 1.0, CX + 1.6, X1 - 0.25):
        haus.stab((x, Y0 + 0.02, Z_OG - 0.5), (x, yo + 0.02, Z_OG - 0.02), 0.13, 0.13, st["Fachwerk"], quer=(1, 0, 0))

    M_vorn = bs.wandmatrix((CX, Y0, Z_SOCKEL), "vorne")
    M = {
        "vorn": M_vorn, "rechts": bs.wandmatrix((X1, CY, Z_SOCKEL), "rechts"),
        "links": bs.wandmatrix((X0, CY, Z_SOCKEL), "links"), "hinten": bs.wandmatrix((CX, Y1, Z_SOCKEL), "hinten"),
        "vorn og": bs.wandmatrix((CX, yo, Z_OG), "vorne"), "rechts og": bs.wandmatrix((X1, (yo + Y1) / 2, Z_OG), "rechts"),
        "links og": bs.wandmatrix((X0, (yo + Y1) / 2, Z_OG), "links"), "hinten og": bs.wandmatrix((CX, Y1, Z_OG), "hinten"),
    }
    for name, L in (("vorn", 2 * HW), ("rechts", Y1 - Y0), ("links", Y1 - Y0), ("hinten", 2 * HW)):
        bs.fugen(haus, M[name], L - 0.02, 0.0, H_EG, st, abstand=0.36)

    F = "fenster"
    klein = dict(v=1.1, b=0.6, h=0.75)
    og = dict(v=0.6, b=0.7, h=0.95)
    # Erdgeschoss: kleine vergitterte Fenster in grobem Holzrahmen
    for name, liste in (("vorn", [Oeffnung(F, 0.6, **klein)]), ("rechts", [Oeffnung(F, u, **klein) for u in (-1.6, 1.6)]),
                        ("links", [Oeffnung(F, u, **klein) for u in (-1.6, 1.6)]), ("hinten", [Oeffnung(F, -1.4, **klein)])):
        for o in liste:
            bs.gewaende(haus, M[name], o, st, gitter=True, stein="Holz alt")
            oeffnungen.append((M[name], o))
    tuer = Oeffnung("tuer", -1.6, 0.0, 1.0, 2.0)
    for name, o in (("vorn", tuer), ("hinten", Oeffnung("tuer", 1.4, 0.0, 0.9, 1.9))):
        bs.tuer(haus, M[name], o, st)
        haus.quader((o.u, -0.06, o.v + o.h + 0.08), (o.b + 0.4, 0.12, 0.16), st["Holz alt"], M[name], ohne=("+y",))
        oeffnungen.append((M[name], o))
    vernageln(haus, st, M_vorn, Oeffnung(F, 2.3, 1.0, 0.7, 0.85), saat=1)
    # Obergeschoss: Bretterwände mit Leisten, Fenster mit schiefen Läden, eines vernagelt
    og_fenster = {
        "vorn og": ([Oeffnung(F, -2.4, **og), Oeffnung(F, 0.0, **og)], [Oeffnung(F, 2.4, **og)]),
        "rechts og": ([Oeffnung(F, u, **og) for u in (-1.8, 1.4)], []),
        "links og": ([Oeffnung(F, 1.4, **og)], [Oeffnung(F, -1.8, **og)]),
        "hinten og": ([Oeffnung(F, 0.0, **og)], []),
    }
    for name, (offen, zu) in og_fenster.items():
        L = 2 * HW if name in ("vorn og", "hinten og") else Y1 - yo
        bretterwand(haus, st, M[name], L, H_OG, offen + zu, saat=len(name))
        for k, o in enumerate(offen):
            bretterfenster(haus, st, M[name], o)
            laeden(haus, st, M[name], o, (30 + 25 * (k % 2), 70 - 30 * (k % 2)), haengt=k % 2 == 0)
            oeffnungen.append((M[name], o))
        for o in zu:
            bretterfenster(haus, st, M[name], o, glas="Glas")
            vernageln(haus, st, M[name], o, saat=len(name) + 3, glas=False)
    kellerluke(haus, st, CX - 2.9)
    haus.quader((CX + tuer.u, Y0 - 0.2, (Z_SOCKEL + 0.01) / 2), (1.3, 0.4, Z_SOCKEL + 0.01), st["Bruchstein dunkel"])
    laterne(haus, st, M_vorn, tuer.u + 0.8, 2.25)

    # ── Schindeldach mit dem Giebel zur Gasse, Bretter-Giebel, Ladeluke, Schornstein ──
    drehung = Matrix.Translation((CX, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(haus, st, yo, Y1, -HW, HW, Z_TRAUFE, NEIGUNG, traufe=0.35, ortgang=0.3, matrix=drehung,
                         **bs.SCHINDEL)
    hoch = HW * dach["tan"]
    for y, seite in ((yo, "vorne"), (Y1, "hinten")):
        Mg = bs.wandmatrix((CX, y, Z_TRAUFE), seite)
        bs.platte(haus, Mg, [(-HW, 0), (HW, 0), (0, hoch)], 0, 0.3, st["Bretter"])
        luke = Oeffnung("tuer", 0.0, 0.3, 1.0, 1.2) if seite == "vorne" else Oeffnung("fenster", 0.0, 0.7, 0.6, 0.8)
        bretterwand(haus, st, Mg, 2 * HW, hoch, [luke], saat=7, oben=lambda u: hoch * (1 - abs(u) / HW))
        if seite == "vorne":
            ladeluke(haus, st, Mg, luke, hoch)
            oeffnungen.append((Mg, luke))
        else:
            bretterfenster(haus, st, Mg, luke)
            oeffnungen.append((Mg, luke))
    for seite, x, s, r in (("vorn", -0.6, 1.5, 0.75), ("vorn", -3.4, 3.4, 0.55), ("hinten", -2.0, 2.0, 0.7)):
        bs.dachloch(haus, st, dach, seite, x, s, r, stoff="Bretter")                       # geflickte Stellen
    xs, ys = CX - 1.5, Y1 - 1.6                                       # schiefer Schornstein, linke Dachfläche
    z0 = dach["oben_hinten"](-(xs - CX)) - 0.3
    t = Matrix.Translation((xs, ys, z0)) @ Matrix.Rotation(math.radians(7), 4, "Y")
    hoehe = dach["z_first"] + 0.6 - z0
    haus.quader((0, 0, hoehe / 2), (0.55, 0.55, hoehe), st["Bruchstein dunkel"], t)
    haus.quader((0, 0, hoehe + 0.05), (0.7, 0.7, 0.1), st["Bruchstein"], t)

    # ── Wappen, Fahne ─────────────────────────────────────────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, M["vorn og"], -1.2, 1.2, st, groesse=0.55, rahmen="Holz alt")
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (CX, Y1 - 0.3, dach["z_first"] - 0.25), hoehe=2.2, richtung=1)

    # ── Stufen ────────────────────────────────────────────────────────────────
    stufe1 = bs.Netz("Stufe1")
    hof(stufe1, st)
    stufe2 = bs.Netz("Stufe2")
    hof(stufe2, st, karren=False)
    verschlag(stufe2, st)
    fluchtleiter(stufe2, st)
    waescheleine(stufe2, st, (3.8, -0.6), (7.2, 0.2))
    taubenschlag(stufe2, st, (6.4, 4.4, 0))
    bs.gaube(stufe2, st, dach, [], x0=-2.6, x1=-1.2, y_front=-HW + 1.1, koerper="Bretter")
    stufe3 = bs.Netz("Stufe3")
    hof(stufe3, st, karren=False, huette=False)
    verschlag(stufe3, st)
    fluchtleiter(stufe3, st)
    gildenhaus(stufe3, st)
    ausguck(stufe3, st, (6.3, -2.6, 0))
    bs.gaube(stufe3, st, dach, [], x0=-2.6, x1=-1.2, y_front=-HW + 1.1, koerper="Bretter")
    bs.dachreiter(stufe3, st, Vector((CX, -0.6, dach["z_first"])), spitze="Nachtblau", koerper="Bretter", ecken="Holz alt")

    # ── Leerstand, Gerüst, Anker ──────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (Mo, o) in enumerate(oeffnungen):
        bs.bretter(leer, Mo, o, st, i + 11)
    for x, y in ((X0 + 0.3, Y0 - 0.15), (-2.2, Y0 - 1.3), (X1 - 0.2, Y0 - 0.15), (X0 - 0.15, -1.0), (X1 + 0.15, 1.0)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.6)
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=X0 - 0.15, x1=X1 + 0.15, y_innen=yo - 0.35, y_aussen=yo - 1.2,
                  ebenen=(2.3, 4.6), hoehe_innen=6.6, hoehe_aussen=7.2, felder=3)
    anker = [bs.anker("Anker_Tuer", (CX + tuer.u, Y0 - 0.2, Z_SOCKEL + 0.01)),
             bs.anker("Anker_Karren", (2.5, Y0 - 1.6, 0.0))]

    objekte = [haus.objekt(), stufe1.objekt(), stufe2.objekt(), stufe3.objekt(), wappen.objekt(), fahne.objekt(),
               leer.objekt(), geruest.objekt()] + anker
    for o in objekte:
        if o.name in ("Stufe2", "Stufe3", "Leerstand", "Geruest"):
            bs.zustand(o)
        elif o.name == "Stufe1":
            o["zustand"] = True
    return objekte


# ─────────────────────────────────────────────────────────────────────────────
#  Teile des Diebesnests
# ─────────────────────────────────────────────────────────────────────────────

def bretterwand(netz, st, M, L, H, oeffnungen, saat=0, oben=None, abstand=0.34):
    """Deckleisten auf einer Bretterwand (die Wandfläche kommt vom Aufrufer): leicht schief, an Öffnungen unterbrochen.
    `oben(u)`: Höhe der Wand bei u (für Giebel)."""
    zufall = random.Random(saat)
    quer = M.to_3x3() @ Vector((0, 1, 0))
    u = -L / 2 + 0.17
    while u < L / 2 - 0.1:
        top = (H if oben is None else min(H, oben(u))) - 0.06
        stuecke = [(0.04, top)]
        for o in oeffnungen:
            if abs(u - o.u) < o.b / 2 + 0.1:
                stuecke = [(a, min(b, o.v - 0.08)) for a, b in stuecke] + [(max(a, o.v + o.h + 0.08), b) for a, b in stuecke]
        for a, b in stuecke:
            if b - a > 0.15:
                s = zufall.uniform(-0.03, 0.03)
                netz.stab(M @ Vector((u + s, -0.02, a)), M @ Vector((u - s, -0.02, b)), 0.07, 0.03, st["Holz alt"], quer=quer)
        u += abstand * zufall.uniform(0.85, 1.15)


def bretterfenster(netz, st, M, o, glas=None):
    """Fenster in der Bretterwand: Scheibe und ein grober Rahmen aus vier Brettern."""
    netz.vieleck([M @ Vector(p) for p in ((o.u - o.b / 2, -0.006, o.v), (o.u + o.b / 2, -0.006, o.v),
                                          (o.u + o.b / 2, -0.006, o.v + o.h), (o.u - o.b / 2, -0.006, o.v + o.h))],
                 st[glas or bs.scheibe(M, o)])
    quer = M.to_3x3() @ Vector((0, 1, 0))
    for a, b in (((o.u - o.b / 2 - 0.12, o.v - 0.05), (o.u + o.b / 2 + 0.12, o.v - 0.05)),
                 ((o.u - o.b / 2 - 0.1, o.v + o.h + 0.05), (o.u + o.b / 2 + 0.14, o.v + o.h + 0.07)),
                 ((o.u - o.b / 2 - 0.05, o.v - 0.1), (o.u - o.b / 2 - 0.05, o.v + o.h + 0.1)),
                 ((o.u + o.b / 2 + 0.05, o.v - 0.1), (o.u + o.b / 2 + 0.05, o.v + o.h + 0.1))):
        netz.stab(M @ Vector((a[0], -0.045, a[1])), M @ Vector((b[0], -0.045, b[1])), 0.1, 0.04, st["Holz alt"], quer=quer)


def laeden(netz, st, M, o, winkel=(35, 70), haengt=False):
    """Zwei Läden in der Hausfarbe, verschieden weit offen; einer hängt auf Wunsch schief in der Angel."""
    breite = o.b / 2 + 0.05
    for i, s in enumerate((-1, 1)):
        w = winkel[i]
        dreh = -w if s > 0 else 180 + w
        t = M @ Matrix.Translation((o.u + s * (o.b / 2 + 0.1), -0.07, o.v + o.h / 2)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
        if haengt and s > 0:
            t = t @ Matrix.Rotation(math.radians(-9), 4, "Y")
        netz.quader((breite / 2, 0, 0), (breite, 0.035, o.h + 0.06), st["Hausfarbe"], t)


def vernageln(netz, st, M, o, saat=0, glas=True):
    """Vernageltes Fenster (wie bisher im Spiel): dunkles Glas, kreuz und quer genagelte Bretter."""
    if glas:
        netz.vieleck([M @ Vector(p) for p in ((o.u - o.b / 2, -0.006, o.v), (o.u + o.b / 2, -0.006, o.v),
                                              (o.u + o.b / 2, -0.006, o.v + o.h), (o.u - o.b / 2, -0.006, o.v + o.h))],
                     st["Glas"])
    bs.bretter(netz, M, o, st, saat)


def kellerluke(netz, st, x, breite=1.1, tiefe=0.9):
    """Kellerluke vor der Hauswand: schräge Bretterklappe in der Hausfarbe auf einem Bruchsteinkasten."""
    profil = [(Y0, 0.0), (Y0, 0.62), (Y0 - tiefe, 0.12), (Y0 - tiefe, 0.0)]

    def kante(p, q):
        return st["Hausfarbe"] if abs(p.x - q.x) > 0.5 and abs(p.y - q.y) > 0.3 else None

    netz.prisma(profil, x - breite / 2, x + breite / 2, st["Bruchstein dunkel"], kante=kante)
    neig = math.atan2(0.5, tiefe)
    for dx in (-0.25, 0.25):
        netz.stab((x + dx, Y0 - 0.1, 0.62 - 0.1 * math.tan(neig) + 0.02), (x + dx, Y0 - tiefe + 0.1, 0.12 + 0.1 * math.tan(neig) + 0.02),
                  0.06, 0.03, st["Eisen"], quer=(0, math.sin(neig), math.cos(neig)))
    netz.stab((x, Y0 - 0.05, 0.64), (x, Y0 - tiefe, 0.14), 0.04, 0.02, st["Fachwerk"], quer=(0, math.sin(neig), math.cos(neig)))


def laterne(netz, st, M, u, v):
    """Trübe Laterne an einem Wandarm (wie bisher im Spiel über dem Tor); leuchtet nachts mit den Fenstern."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v + 0.35)), M @ Vector((u, -0.55, v + 0.35)), 0.04, 0.04, eisen)
    netz.stab(M @ Vector((u, 0, v)), M @ Vector((u, -0.45, v + 0.33)), 0.03, 0.03, eisen)
    mitte = M @ Vector((u, -0.5, v + 0.05))
    netz.quader(mitte, (0.2, 0.2, 0.28), st["Fenster"])
    netz.quader(mitte + Vector((0, 0, 0.19)), (0.28, 0.28, 0.08), eisen)
    netz.quader(mitte - Vector((0, 0, 0.17)), (0.24, 0.24, 0.05), eisen)


def ladeluke(netz, st, M, o, hoch):
    """Ladeluke im Giebel: ein Flügel offen, Kranbalken mit Rolle und Seil, an dem ein Sack hängt."""
    netz.vieleck([M @ Vector(p) for p in ((o.u - o.b / 2, -0.005, o.v), (o.u + o.b / 2, -0.005, o.v),
                                          (o.u + o.b / 2, -0.005, o.v + o.h), (o.u - o.b / 2, -0.005, o.v + o.h))], st["Loch"])
    offen = M @ Matrix.Translation((o.u - o.b / 2, -0.04, o.v + o.h / 2)) @ Matrix.Rotation(math.radians(-110), 4, "Z")
    netz.quader((o.b / 2, 0, 0), (o.b, 0.04, o.h), st["Hausfarbe"], offen)
    bretterfenster_rahmen = Oeffnung("fenster", o.u, o.v, o.b, o.h)
    quer = M.to_3x3() @ Vector((0, 1, 0))
    for a, b in (((o.u - o.b / 2 - 0.1, o.v - 0.05), (o.u + o.b / 2 + 0.1, o.v - 0.05)),
                 ((o.u - o.b / 2 - 0.1, o.v + o.h + 0.05), (o.u + o.b / 2 + 0.1, o.v + o.h + 0.05))):
        netz.stab(M @ Vector((a[0], -0.045, a[1])), M @ Vector((b[0], -0.045, b[1])), 0.1, 0.04, st["Holz alt"], quer=quer)
    del bretterfenster_rahmen
    v_kran = min(hoch - 0.6, o.v + o.h + 0.5)
    netz.stab(M @ Vector((0, 0.2, v_kran)), M @ Vector((0, -1.1, v_kran)), 0.18, 0.18, st["Fachwerk"],
              quer=M.to_3x3() @ Vector((1, 0, 0)))
    netz.quader(M @ Vector((0, -0.95, v_kran - 0.2)), (0.1, 0.22, 0.22), st["Eisen"])
    unten = o.v + 0.2
    netz.stab(M @ Vector((0, -0.95, v_kran - 0.28)), M @ Vector((0, -0.95, unten + 0.45)), 0.03, 0.03, st["Seil"])
    bs.achteck(netz, st, M @ Vector((0, -0.95, unten)), 0.2, 0.0, 0.45, "Lumpen", n=6)


def kiste(netz, st, fuss, masse=(0.7, 0.55, 0.5), dreh=0.0):
    """Kiste mit Diebesgut: Holz mit zwei dunklen Bändern."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, masse[2] / 2), masse, st["Holz"], t)
    for dx in (-masse[0] * 0.3, masse[0] * 0.3):
        netz.quader((dx, 0, masse[2] / 2), (0.06, masse[1] + 0.02, masse[2] + 0.02), st["Fachwerk"], t)


def sack(netz, st, fuss, r=0.24):
    """Prall gefüllter Sack, oben zugebunden."""
    bs.achteck(netz, st, Vector(fuss), r, 0.0, r * 1.8, "Lumpen", n=6)
    bs.achteck(netz, st, Vector(fuss), r * 0.4, r * 1.8, r * 2.2, "Seil", n=6)


def diebesgut(netz, st, mitte, dreh=0.0):
    """Ein Haufen Diebesgut: gestapelte Kisten, ein Fass, Säcke (wie bisher im Spiel an der Ostseite)."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    kiste(netz, st, t @ Vector((0, 0, 0)), (0.8, 0.6, 0.55), dreh)
    kiste(netz, st, t @ Vector((0.05, 0.75, 0)), (0.7, 0.6, 0.5), dreh + 8)
    kiste(netz, st, t @ Vector((0.05, 0.3, 0.55)), (0.6, 0.5, 0.45), dreh - 12)
    bs.fass(netz, st, t @ Vector((0.0, -0.75, 0)), r=0.32, h=0.85)
    for d in ((0.7, -0.3, 0), (0.75, 0.35, 0)):
        sack(netz, st, t @ Vector(d))


def falltuer(netz, st, mitte):
    """Falltür im Hof (wie bisher im Spiel): dunkle Bohlen bündig im Boden, Eisenbänder und ein Ring."""
    x, y = mitte[0], mitte[1]
    netz.quader((x, y, 0.015), (1.3, 1.1, 0.03), st["Fachwerk"], ohne=("-z",))
    for dy in (-0.3, 0.3):
        netz.quader((x, y + dy, 0.035), (1.2, 0.07, 0.01), st["Eisen"], ohne=("-z",))
    netz.quader((x + 0.45, y, 0.05), (0.14, 0.14, 0.02), st["Eisen"], ohne=("-z",))


def hoher_zaun(netz, st, punkte, h=1.9, saat=0):
    """Hoher, schiefer Bretterzaun als Sichtschutz: Pfosten, Bretterwand, zwei Riegel, hier und da ein fehlendes Brett."""
    zufall = random.Random(saat)
    for i, ((x0, y0), (x1, y1)) in enumerate(zip(punkte, punkte[1:])):
        laenge = math.hypot(x1 - x0, y1 - y0)
        d = Vector((x1 - x0, y1 - y0, 0)) / laenge
        n = max(1, round(laenge / 2.2))
        for k in range(0 if i == 0 else 1, n + 1):
            p = Vector((x0, y0, 0)) + d * (laenge * k / n)
            netz.quader(p + Vector((0, 0, (h + 0.15) / 2)), (0.13, 0.13, h + 0.15), st["Holz alt"])
        a = Vector((x0, y0, 0)) + d * 0.07
        b = Vector((x1, y1, 0)) - d * 0.07
        netz.stab(a + Vector((0, 0, h / 2 + 0.03)), b + Vector((0, 0, h / 2 + 0.03)), h - 0.06, 0.05, st["Bretter"],
                  quer=(-d.y, d.x, 0))
        for z in (0.35, h - 0.3):
            netz.stab(a + Vector((-d.y, d.x, 0)) * 0.05 + Vector((0, 0, z)), b + Vector((-d.y, d.x, 0)) * 0.05 + Vector((0, 0, z)),
                      0.1, 0.05, st["Holz alt"], quer=(-d.y, d.x, 0))
        for k in range(int(laenge / 3)):                            # Lücken
            s = zufall.uniform(0.6, laenge - 0.6)
            p = Vector((x0, y0, 0)) + d * s
            for sgn in (-1, 1):
                nrm = Vector((-d.y, d.x, 0)) * sgn
                q = p + nrm * 0.028
                netz.vieleck_nach([q - d * 0.08 + Vector((0, 0, 0.6)), q + d * 0.08 + Vector((0, 0, 0.6)),
                                   q + d * 0.08 + Vector((0, 0, h - 0.5)), q - d * 0.08 + Vector((0, 0, h - 0.5))], nrm, st["Loch"])


def tor_offen(netz, st, a, b, h=1.9):
    """Zwei offene Torflügel aus Brettern in einer Zaunlücke von a nach b (nach innen geschwenkt)."""
    a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    d = (b - a).normalized()
    breite = (b - a).length / 2
    for p, s in ((a, 1), (b, -1)):
        netz.quader(p + Vector((0, 0, (h + 0.2) / 2)), (0.15, 0.15, h + 0.2), st["Holz alt"])
        w = math.atan2(d.y, d.x) + (math.radians(100) if s > 0 else math.radians(80))
        t = Matrix.Translation(p + Vector((0, 0, h / 2 + 0.05))) @ Matrix.Rotation(w, 4, "Z")
        netz.quader((s * breite / 2 * (1 if s > 0 else -1), 0, 0), (breite, 0.05, h - 0.1), st["Bretter"], t)


def karren_kaputt(netz, st, mitte, dreh=0.0):
    """Kaputter Karren: schiefe Ladefläche auf einem Rad, das andere Rad lehnt daneben."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    neig = Matrix.Rotation(math.radians(9), 4, "Y")
    netz.quader((0, 0, 0.55), (1.8, 1.0, 0.1), st["Holz alt"], t @ neig)
    for s in (-1, 1):
        netz.quader((0, s * 0.5, 0.75), (1.8, 0.06, 0.35), st["Holz alt"], t @ neig)
    profil = [(0.42 * math.cos(math.radians(30 * k)), 0.42 * math.sin(math.radians(30 * k))) for k in range(12)]
    rad = t @ Matrix.Translation((0.3, 0.6, 0.42)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    netz.prisma(profil, -0.04, 0.04, st["Holz"], matrix=rad)
    lehnt = t @ Matrix.Translation((-1.3, -0.2, 0.42)) @ Matrix.Rotation(math.radians(70), 4, "Z") @ Matrix.Rotation(math.radians(12), 4, "X")
    netz.prisma(profil, -0.04, 0.04, st["Holz"], matrix=lehnt)
    netz.stab(t @ Vector((0.9, -0.2, 0.5)), t @ Vector((2.3, -0.3, 0.05)), 0.07, 0.07, st["Holz alt"])


def hundehuette(netz, st, fuss, dreh=0.0):
    """Hundehütte mit dunklem Eingang, Satteldach aus Brettern und einer Kette."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, 0.35), (0.9, 1.1, 0.7), st["Holz alt"], t)
    netz.prisma([(-0.6, 0.7), (0.6, 0.7), (0.0, 1.15)], -0.5, 0.5, st["Schindel"], matrix=t @ Matrix.Rotation(math.radians(90), 4, "Z"))
    netz.vieleck_nach([t @ Vector(p) for p in ((0.452, -0.25, 0.02), (0.452, 0.25, 0.02), (0.452, 0.25, 0.5), (0.452, -0.25, 0.5))],
                      t.to_3x3() @ Vector((1, 0, 0)), st["Loch"])
    netz.stab(t @ Vector((0.46, 0.3, 0.3)), t @ Vector((1.3, 0.5, 0.02)), 0.03, 0.03, st["Eisen"])


def verschlag(netz, st):
    """Bretterverschlag an der rechten Hauswand: Pfosten, Bretterwände vorn und rechts zur Hälfte offen,
    Schindel-Pultdach; darin Diebesgut."""
    xa, ya, yb = 3.0, -3.0, 1.4
    for y in (ya + 0.08, (ya + yb) / 2, yb - 0.08):
        netz.stab((xa, y, 0), (xa, y, 2.25), 0.14, 0.14, st["Holz alt"])
    netz.stab((xa, ya, 2.2), (xa, yb, 2.2), 0.14, 0.14, st["Holz alt"])
    netz.quader(((X1 + xa) / 2, ya + 0.04, 1.0), (xa - X1 - 0.1, 0.05, 1.9), st["Bretter"])
    netz.quader((xa + 0.04, (ya + yb) / 2 + 1.2, 1.0), (0.05, (yb - ya) / 2 - 0.2, 1.9), st["Bretter"])
    bs.pultdach(netz, st, X1, xa + 0.1, ya - 0.1, yb, 2.95, 2.25, stoff="Schindel")
    diebesgut(netz, st, (1.3, -1.6, 0))
    kiste(netz, st, (2.2, 0.4, 0), (0.8, 0.6, 0.6), 15)


def fluchtleiter(netz, st):
    """Leiter vom Hof hinauf zum Fenster im Obergeschoss an der rechten Seite."""
    y = -3.75
    fuss, kopf = (X1 + 1.2, y, 0.0), (X1 + 0.12, y, Z_OG + 0.5)
    for dy in (-0.22, 0.22):
        netz.stab((fuss[0], y + dy, fuss[2]), (kopf[0], y + dy, kopf[2]), 0.07, 0.05, st["Holz alt"])
    n = int(kopf[2] / 0.32)
    for k in range(1, n):
        p = Vector(fuss).lerp(Vector(kopf), k / n)
        netz.stab((p.x, y - 0.22, p.z), (p.x, y + 0.22, p.z), 0.04, 0.04, st["Holz alt"])


def waescheleine(netz, st, a, b):
    """Wäscheleine zwischen zwei Pfählen mit Mänteln und Kapuzen zur Verkleidung."""
    a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    for p in (a, b):
        netz.stab(p, p + Vector((0, 0, 2.1)), 0.09, 0.09, st["Holz alt"])
    netz.stab(a + Vector((0, 0, 2.0)), b + Vector((0, 0, 2.0)), 0.025, 0.025, st["Seil"])
    d = (b - a).normalized()
    for k, (f, stoff, laenge) in enumerate(((0.2, "Lumpen", 1.1), (0.42, "Hausfarbe", 0.9), (0.63, "Leder", 1.2), (0.82, "Fachwerk", 0.8))):
        p = a.lerp(b, f) + Vector((0, 0, 2.0))
        netz.stab(p - d * 0.25, p - d * 0.22 - Vector((0, 0, laenge)), 0.04, 0.5, st[stoff], quer=(-d.y, d.x, 0))
        netz.quader(p - Vector((0, 0, laenge / 2)), (0.02, 0.02, 0.02), st[stoff])


def taubenschlag(netz, st, fuss):
    """Taubenschlag für die Botentauben: Pfahl, Kasten mit Fluglöchern und spitzem Schindeldach."""
    m = Vector(fuss)
    netz.stab(m, m + Vector((0, 0, 3.0)), 0.14, 0.14, st["Holz alt"])
    netz.quader(m + Vector((0, 0, 3.3)), (0.9, 0.9, 0.6), st["Holz alt"])
    for d in (-0.2, 0.2):
        netz.vieleck_nach([m + Vector((d - 0.08, -0.452, 3.2)), m + Vector((d + 0.08, -0.452, 3.2)),
                           m + Vector((d + 0.08, -0.452, 3.38)), m + Vector((d - 0.08, -0.452, 3.38))], (0, -1, 0), st["Loch"])
    netz.quader(m + Vector((0, -0.55, 3.12)), (0.7, 0.2, 0.04), st["Holz alt"])
    gipfel = m + Vector((0, 0, 4.2))
    ecken = [m + Vector((sx * 0.6, sy * 0.6, 3.6)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        p, q = ecken[i], ecken[(i + 1) % 4]
        netz.vieleck_nach([p, q, gipfel], (p + q) / 2 - (m + Vector((0, 0, 3.6))), st["Schindel"])


def ausguck(netz, st, fuss, h=4.8):
    """Hölzerner Ausguck: vier geneigte Pfosten mit Kreuzstreben, Plattform mit Brüstung, Schindeldach, Leiter."""
    m = Vector(fuss)
    holz = st["Holz alt"]
    unten = [m + Vector((sx * 1.0, sy * 1.0, 0)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    oben = [m + Vector((sx * 0.75, sy * 0.75, h)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        netz.stab(unten[i], oben[i] + Vector((0, 0, 1.0)), 0.14, 0.14, holz)
        j = (i + 1) % 4
        netz.stab(unten[i].lerp(oben[i], 0.1), unten[j].lerp(oben[j], 0.55), 0.08, 0.08, holz)
    netz.quader(m + Vector((0, 0, h + 0.05)), (1.8, 1.8, 0.1), st["Bretter"])
    for i in range(4):
        j = (i + 1) % 4
        netz.stab(oben[i] + Vector((0, 0, 0.9)), oben[j] + Vector((0, 0, 0.9)), 0.08, 0.08, holz)
        a, b = oben[i].lerp(oben[j], 0.04), oben[i].lerp(oben[j], 0.96)       # Brüstung, Ecken frei
        netz.stab(a + Vector((0, 0, 0.4)), b + Vector((0, 0, 0.4)), 0.4, 0.04, st["Bretter"], quer=(0, 0, 1))
    gipfel = m + Vector((0, 0, h + 2.4))
    traufe = [m + Vector((sx * 1.05, sy * 1.05, h + 1.6)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        p, q = traufe[i], traufe[(i + 1) % 4]
        netz.vieleck_nach([p, q, gipfel], (p + q) / 2 - (m + Vector((0, 0, h + 1.6))), st["Schindel"])
    for i in range(4):
        netz.stab(oben[i] + Vector((0, 0, 1.0)), traufe[i], 0.08, 0.08, holz)
    fuss_l, kopf_l = m + Vector((-0.4, -1.9, 0)), m + Vector((-0.4, -0.95, h))
    for dx in (-0.2, 0.2):
        netz.stab(fuss_l + Vector((dx, 0, 0)), kopf_l + Vector((dx, 0, 0)), 0.06, 0.05, holz)
    for k in range(1, 14):
        p = fuss_l.lerp(kopf_l, k / 14)
        netz.stab(p - Vector((0.2, 0, 0)), p + Vector((0.2, 0, 0)), 0.04, 0.04, holz)


def gildenhaus(netz, st):
    """Gildenhaus im hinteren Hof: Bruchstein unten, Bretter oben, Schindeldach mit dem Giebel nach rechts,
    Tür mit Laterne, vergitterte und vernagelte Fenster."""
    x0, x1, y0, y1 = 2.6, 7.4, 2.8, 6.4
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    h1, h2 = 2.6, 2.2
    netz.quader((cx, cy, h1 / 2), (x1 - x0, y1 - y0, h1), st["Bruchstein"], ohne=("+z",))
    netz.quader((cx, cy, h1 + h2 / 2), (x1 - x0, y1 - y0, h2), st["Bretter"], ohne=("+z",))
    netz.quader((cx, y0 - 0.04, h1 + 0.06), (x1 - x0 + 0.1, 0.1, 0.16), st["Fachwerk"], ohne=("+y",))
    M_v = bs.wandmatrix((cx, y0, 0.0), "vorne")
    M_r = bs.wandmatrix((x1, cy, 0.0), "rechts")
    M_vo = bs.wandmatrix((cx, y0, h1), "vorne")
    M_ro = bs.wandmatrix((x1, cy, h1), "rechts")
    bs.fugen(netz, M_v, x1 - x0 - 0.02, 0.0, h1, st, abstand=0.36)
    bs.fugen(netz, M_r, y1 - y0 - 0.02, 0.0, h1, st, abstand=0.36)
    tuer = Oeffnung("tuer", -1.2, 0.0, 1.0, 2.0)
    bs.tuer(netz, M_v, tuer, st)
    netz.quader((tuer.u, -0.06, tuer.h + 0.08), (1.4, 0.12, 0.16), st["Holz alt"], M_v, ohne=("+y",))
    laterne(netz, st, M_v, tuer.u + 0.8, 2.1)
    for M, u in ((M_v, 1.0), (M_r, 0.0)):
        bs.gewaende(netz, M, Oeffnung("fenster", u, 1.1, 0.6, 0.75), st, gitter=True, stein="Holz alt")
    fenster_v = [Oeffnung("fenster", u, 0.5, 0.7, 0.9) for u in (-1.4, 0.4, 1.8)]
    fenster_r = [Oeffnung("fenster", u, 0.5, 0.7, 0.9) for u in (-0.9, 0.9)]
    bretterwand(netz, st, M_vo, x1 - x0, h2, fenster_v, saat=31)
    bretterwand(netz, st, M_ro, y1 - y0, h2, fenster_r, saat=32)
    for k, o in enumerate(fenster_v):
        bretterfenster(netz, st, M_vo, o, glas="Glas" if k == 2 else None)
        if k == 2:
            vernageln(netz, st, M_vo, o, saat=33, glas=False)
        else:
            laeden(netz, st, M_vo, o, (40, 60), haengt=k == 1)
    for o in fenster_r:
        bretterfenster(netz, st, M_ro, o)
        laeden(netz, st, M_ro, o, (55, 35))
    dach = bs.satteldach(netz, st, x0, x1, y0, y1, h1 + h2, 50, traufe=0.3, ortgang=0.25, **bs.SCHINDEL)
    hoch = (y1 - y0) / 2 * dach["tan"]
    Mg = bs.wandmatrix((x1, cy, h1 + h2), "rechts")
    bs.platte(netz, Mg, [(-(y1 - y0) / 2, 0), ((y1 - y0) / 2, 0), (0, hoch)], 0, 0.3, st["Bretter"])
    bretterwand(netz, st, Mg, y1 - y0, hoch, [], saat=34, oben=lambda u: hoch * (1 - abs(u) / ((y1 - y0) / 2)))
    bs.dachloch(netz, st, dach, "vorn", cx + 0.8, 1.3, 0.4, stoff="Bretter")


def feuerstelle(netz, st, mitte):
    """Feuerstelle im Hof: Steinring, Glut (das Spiel schaltet sie wie die Esse), Dreibein mit Kessel, zwei Hocker."""
    m = Vector(mitte)
    for k in range(8):
        w = math.radians(45 * k)
        bs.achteck(netz, st, m + Vector((0.55 * math.cos(w), 0.55 * math.sin(w), 0)), 0.14, 0.0, 0.2,
                   "Bruchstein" if k % 2 else "Bruchstein dunkel", n=5)
    bs.rund_oben(netz, st, m, 0.42, 0.03, "Glut")
    for w in (0, 120, 240):
        a = m + Vector((0.6 * math.cos(math.radians(w)), 0.6 * math.sin(math.radians(w)), 0))
        netz.stab(a, m + Vector((0, 0, 1.3)), 0.05, 0.05, st["Holz alt"])
    netz.stab(m + Vector((0, 0, 1.3)), m + Vector((0, 0, 0.75)), 0.02, 0.02, st["Eisen"])
    bs.achteck(netz, st, m + Vector((0, 0, 0.4)), 0.25, 0.0, 0.32, "Kohle", n=8)
    for d in ((1.1, 0.2, 0), (-0.2, 1.1, 0)):
        bs.fass(netz, st, m + Vector(d), r=0.2, h=0.45, reifen=(), stoff="Holz alt")


def hof(netz, st, karren=True, huette=True):
    """Hof hinter dem hohen Bretterzaun mit Tor: Diebesgut an der Hauswand (wie bisher im Spiel an der Ostseite),
    Falltür, Säcke; auf Wunsch kaputter Karren und Hundehütte."""
    diebesgut(netz, st, (X1 + 0.7, -3.6, 0))
    falltuer(netz, st, (4.6, -2.0))
    feuerstelle(netz, st, (4.6, 0.2, 0))
    kiste(netz, st, (6.8, -3.7, 0), (0.8, 0.6, 0.55), 20)
    kiste(netz, st, (6.9, -3.0, 0), (0.6, 0.5, 0.45), -10)
    for p in ((5.6, -2.9, 0), (6.0, -2.3, 0)):
        sack(netz, st, p)
    if karren:
        karren_kaputt(netz, st, (5.2, 2.4, 0), dreh=20)
    if huette:
        hundehuette(netz, st, (2.4, 1.9, 0), dreh=-15)
    hoher_zaun(netz, st, [(X1 + 0.1, -4.7), (1.4, -4.7)], saat=1)
    tor_offen(netz, st, (1.4, -4.7), (3.6, -4.7))
    hoher_zaun(netz, st, [(3.6, -4.7), (7.8, -4.7), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)], saat=2)


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
