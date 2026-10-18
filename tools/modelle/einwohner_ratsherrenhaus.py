"""Ratsherrenhaus der Einwohner – drei Varianten in einem Modell. Belegte Fläche 8 × 10 m.

    blender -b --python tools/modelle/einwohner_ratsherrenhaus.py        oder
    python tools/modelle/einwohner_ratsherrenhaus.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/einwohner_ratsherrenhaus.glb und Bilder nach docs/modelle/.

Objekte im GLB:
    Variante1   grauer Haustein, Treppengiebel, Schieferdach, Kastenerker in der Mitte
    Variante2   ockerfarben verputzt, glatter Giebel mit Sandsteinkanten und Kugeln, Ziegeldach, Eckerker mit Spitzhelm
    Variante3   roter Sandstein, Treppengiebel, Ziegeldach, Erker über der Tür
    Anker_Tuer  vor der Tür (in allen Varianten an derselben Stelle)
Das Spiel zeigt genau eine Variante (nach der Hausnummer). Leerstand und Gerüst gibt es nicht: Einwohnerhäuser
stehen weder leer noch werden sie ausgebaut. Tür und Fensterkreuze tragen „Hausfarbe“ (hier ein gedecktes Braun),
das Spiel kann sie je Haus umfärben.

Aufbau wie bisher im Spiel (Stein, Giebel zur Gasse, Erker), Blender: Z oben, Gasse bei -Y:
    Haus 7,1 × 7,4 m (mit Dachüberstand im 8-m-Raster), drei Geschosse, Front bei y = -3 (2 m Vorplatz), steiles Dach mit dem Giebel
    zur Gasse, Rundbogentür mit Stufen links, Steinfenster mit Kreuzstock.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "einwohner_ratsherrenhaus"
FLAECHE = (8.0, 10.0)

# Farben nur für dieses Modell (jedes Modell läuft in einem eigenen Prozess)
bs.FARBEN.update({
    "Hausfarbe": (0.34, 0.24, 0.17),       # gestrichenes Holz in gedecktem Braun; das Spiel kann je Haus umfärben
    "Ratsputz": (0.80, 0.70, 0.52),        # ockerfarbener Kalkputz
    "Rotsandstein": (0.60, 0.38, 0.31),
    "Rotfuge": (0.46, 0.29, 0.24),
})

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
HX, VORN, HINTEN = 3.55, -3.0, 4.4
TIEF = HINTEN - VORN
Y_MITTE = (VORN + HINTEN) / 2
Z_SOCKEL = 0.45
GESCHOSSE = (3.1, 2.7, 2.5)
H_WAND = sum(GESCHOSSE)
Z_TRAUFE = Z_SOCKEL + H_WAND               # 8,75
Z_GESIMS = 0.22
NEIGUNG = 56
TUER_U = -1.6                              # Tür links der Mitte, in allen Varianten gleich (Anker)

VARIANTEN = {
    "Variante1": dict(stein="Haustein", fuge="Fuge", giebel="treppe", stufen=5, dach=bs.SCHIEFER, erker="mitte"),
    "Variante2": dict(stein="Ratsputz", fuge=None, giebel="glatt", stufen=0, dach=bs.ZIEGEL, erker="ecke"),
    "Variante3": dict(stein="Rotsandstein", fuge="Rotfuge", giebel="treppe", stufen=4, dach=bs.ZIEGEL, erker="tuer"),
}


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    objekte = []
    grau = st["Fuge"]
    for name, v in VARIANTEN.items():
        netz = bs.Netz(name)
        st["Fuge"] = st[v["fuge"]] if v["fuge"] else grau      # Lagerfugen passend zum Stein (auch im Treppengiebel)
        haus(netz, st, v)
        objekte.append(netz.objekt())
    st["Fuge"] = grau
    objekte.append(bs.anker("Anker_Tuer", (TUER_U, VORN - 0.3, 0.3)))      # auf der mittleren Stufe vor der Tür
    return objekte


def haus(netz, st, v):
    stein = st[v["stein"]]
    v1, v2 = GESCHOSSE[0], GESCHOSSE[0] + GESCHOSSE[1]
    netz.quader((0, Y_MITTE, Z_SOCKEL / 2), (2 * HX + 0.12, TIEF + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    netz.quader((0, Y_MITTE, Z_SOCKEL + H_WAND / 2), (2 * HX, TIEF, H_WAND), stein, ohne=("+z",))
    for z, h, vor in ((Z_SOCKEL - 0.05, 0.1, 0.08), (Z_SOCKEL + v1, 0.15, 0.06), (Z_SOCKEL + v2, 0.15, 0.06),
                      (Z_TRAUFE, Z_GESIMS, 0.11)):
        bs.gesims(netz, st, -HX, HX, VORN, HINTEN, z, hoehe=h, vor=vor)
    for sx in (-1, 1):
        for sy, y in ((-1, VORN), (1, HINTEN)):
            if v["erker"] == "ecke" and sx > 0 and sy < 0:
                continue                    # hier sitzt der Eckerker
            bs.eckquader(netz, st, sx * HX, y, sx, sy, Z_SOCKEL, Z_TRAUFE)

    M_vorn = bs.wandmatrix((0, VORN, Z_SOCKEL), "vorne")
    M_hinten = bs.wandmatrix((0, HINTEN, Z_SOCKEL), "hinten")
    M_r = bs.wandmatrix((HX, Y_MITTE, Z_SOCKEL), "rechts")
    M_l = bs.wandmatrix((-HX, Y_MITTE, Z_SOCKEL), "links")
    if v["fuge"]:
        for M, L in ((M_vorn, 2 * HX), (M_hinten, 2 * HX), (M_r, TIEF), (M_l, TIEF)):
            bs.fugen(netz, M, L - 0.02, 0.0, H_WAND, st)
    F = "fenster"
    eg = dict(v=1.0, b=0.9, h=1.35)
    og1 = dict(v=v1 + 0.6, b=0.9, h=1.5)
    og2 = dict(v=v2 + 0.5, b=0.85, h=1.25)
    leer = []

    def fenster(M, liste, verdachung=False):
        for o in liste:
            bs.gewaende(netz, M, o, st, verdachung=verdachung)
            leer.append((M, o))

    # Gassenseite: Tür links, Fenster je nach Erker
    erker = v["erker"]
    fenster(M_vorn, [Oeffnung(F, u, **eg) for u in (0.4, 2.3)])
    if erker == "mitte":
        oben = (-2.4, 2.4)
    elif erker == "ecke":
        oben = (-2.3, -0.6, 1.1)
    else:
        oben = (0.4, 2.3)
    fenster(M_vorn, [Oeffnung(F, u, **og1) for u in oben], verdachung=True)
    fenster(M_vorn, [Oeffnung(F, u, **og2) for u in oben])
    tuer = Oeffnung("tuer", TUER_U, 0.0, 1.25, 2.6)
    bs.portal(netz, M_vorn, tuer, st)
    for k, (tiefe, hoehe) in enumerate(((0.75, 0.15), (0.45, 0.29), (0.2, 0.43))):
        netz.quader((TUER_U, VORN - tiefe / 2, hoehe / 2), (1.9 - 0.2 * k, tiefe, hoehe), st["Sandstein"], ohne=("+y",))
    # Seiten und Rückseite
    for M in (M_r, M_l):
        fenster(M, [Oeffnung(F, u, **eg) for u in (-1.8, 1.8)])
        fenster(M, [Oeffnung(F, u, **og1) for u in (-1.8, 1.8)], verdachung=True)
        fenster(M, [Oeffnung(F, u, **og2) for u in (-1.8, 1.8)])
    fenster(M_hinten, [Oeffnung(F, u, **eg) for u in (-1.8,)])
    fenster(M_hinten, [Oeffnung(F, u, **og1) for u in (-1.8, 1.8)], verdachung=True)
    fenster(M_hinten, [Oeffnung(F, u, **og2) for u in (-1.8, 1.8)])
    hinten = Oeffnung("tuer", 1.6, 0.0, 1.0, 2.1)
    bs.tuer(netz, M_hinten, hinten, st)
    netz.quader((1.6, -0.03, 2.2), (1.35, 0.1, 0.2), st["Sandstein"], M_hinten, ohne=("+y",))

    # Dach mit dem Giebel zur Gasse
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")          # lokal x → Welt y, lokal y → Welt -x
    dach = bs.satteldach(netz, st, VORN, HINTEN, -HX, HX, Z_TRAUFE + Z_GESIMS, NEIGUNG, traufe=0.2,
                         ortgang=-0.22, enden=v["stein"], first_dicke=0.2,
                         matrix=drehung, **v["dach"])
    z_dach0 = Z_GESIMS + dach["dicke"] / math.cos(math.radians(NEIGUNG))
    giebelfenster = [Oeffnung(F, u, 0.8, 0.75, 1.15) for u in (-1.2, 1.2)] + [Oeffnung(F, 0.0, 2.9, 0.6, 0.95)]
    for y, seite in ((VORN, "vorne"), (HINTEN, "hinten")):
        M = bs.wandmatrix((0, y, Z_TRAUFE), seite)
        if v["giebel"] == "treppe":
            bs.treppengiebel(netz, st, M, HX, z_dach0, dach["tan"], leer, giebelfenster, stufen=v["stufen"],
                             oben_halb=0.4, dicke=0.4, stoff=v["stein"])
        else:
            glatter_giebel(netz, st, M, dach, z_dach0, v["stein"], leer, giebelfenster)
    xs, ys = -1.9, 2.4                       # Schornstein auf der linken Dachfläche (lokal y = -Welt x)
    z_dach = dach["oben_hinten"](-xs) - 0.3
    hoehe = dach["z_first"] + 0.8 - z_dach
    netz.quader((xs, ys, z_dach + hoehe / 2), (0.65, 0.65, hoehe), st["Bruchstein"])
    netz.quader((xs, ys, z_dach + hoehe + 0.06), (0.82, 0.82, 0.12), st["Sandstein"])

    if erker == "mitte":
        kastenerker(netz, st, leer, 0.0, 1.0, Z_SOCKEL + v1 + 0.15, Z_SOCKEL + v2 + 2.15, v["stein"], v["dach"])
    elif erker == "tuer":
        kastenerker(netz, st, leer, TUER_U, 0.85, Z_SOCKEL + v1 + 0.15, Z_SOCKEL + v2 + 2.15, v["stein"], v["dach"])
    else:
        eckerker(netz, st, leer, v["stein"])


def kastenerker(netz, st, leer, u, halb, z0, z1, stoff, dach, tief=0.6):
    """Rechteckiger Erker vor der Gassenseite über zwei Geschosse: Steinkörper auf zwei Kragsteinen,
    Fenster vorn und an den Seiten, Gesimse, oben ein kleines Pultdach."""
    y0 = VORN - tief
    netz.quader((u, VORN - tief / 2, (z0 + z1) / 2), (2 * halb, tief, z1 - z0), st[stoff], ohne=("+y",))
    for sx in (-1, 1):
        for k, (b, t, h) in enumerate(((0.4, 0.52, 0.15), (0.32, 0.36, 0.15), (0.24, 0.2, 0.15))):
            netz.quader((u + sx * (halb - 0.24), VORN - t / 2, z0 - 0.095 - 0.15 * k), (b, t, h), st["Sandstein"], ohne=("+y",))
    for z, h, vor in ((z0, 0.12, 0.05), (Z_SOCKEL + GESCHOSSE[0] + GESCHOSSE[1], 0.12, 0.05), (z1 - 0.16, 0.16, 0.08)):
        netz.quader((u, VORN - (tief + vor) / 2, z + h / 2 - 0.02), (2 * halb + 2 * vor, tief + vor, h), st["Sandstein"],
                    ohne=("+y",))
    M_v = bs.wandmatrix((u, y0, Z_SOCKEL), "vorne")
    M_r = bs.wandmatrix((u + halb, VORN - tief / 2, Z_SOCKEL), "rechts")
    M_l = bs.wandmatrix((u - halb, VORN - tief / 2, Z_SOCKEL), "links")
    v1, v2 = GESCHOSSE[0], GESCHOSSE[0] + GESCHOSSE[1]
    for v, h in ((v1 + 0.65, 1.4), (v2 + 0.45, 1.2)):
        for M, o in [(M_v, Oeffnung("fenster", 0.0, v, 2 * halb - 0.65, h))] + [(M, Oeffnung("fenster", 0.0, v, 0.3, h))
                                                                                for M in (M_r, M_l)]:
            bs.gewaende(netz, M, o, st, kreuz=o.b > 0.5)
            leer.append((M, o))
    hoch, tief_z = z1 + 0.7, z1 + 0.05
    profil = [(VORN + 0.01, hoch), (y0 - 0.15, tief_z), (y0 - 0.15, tief_z + 0.11), (VORN + 0.01, hoch + 0.11)]
    netz.prisma(profil, u - halb - 0.18, u + halb + 0.18, st[dach["deckung"]], m_enden=st[dach["kante"]])


def eckerker(netz, st, leer, stoff):
    """Eckerker an der rechten vorderen Hausecke: quadratischer Steinkörper auf einem gestuften Kragstein vom
    ersten Obergeschoss bis über die Traufe, Fenster nach vorn und zur Seite, spitzer Helm."""
    halb = 0.62
    m = Vector((HX - 0.32, VORN + 0.12, 0))
    z0 = Z_SOCKEL + GESCHOSSE[0] + 0.1
    z1 = Z_TRAUFE + 1.3
    netz.quader((m.x, m.y, (z0 + z1) / 2), (2 * halb, 2 * halb, z1 - z0), st[stoff])
    for k in range(3):
        b = 2 * halb - 0.25 * (k + 1)
        netz.quader((m.x - 0.1 * (k + 1), m.y + 0.1 * (k + 1), z0 - 0.08 - 0.16 * k), (b, b, 0.16), st["Sandstein"])
    for z, h, vor in ((z0, 0.12, 0.05), (Z_SOCKEL + GESCHOSSE[0] + GESCHOSSE[1] + 0.03, 0.12, 0.05), (z1 - 0.16, 0.16, 0.08)):
        netz.quader((m.x, m.y, z + h / 2 - 0.02), (2 * halb + 2 * vor, 2 * halb + 2 * vor, h), st["Sandstein"])
    v1, v2 = GESCHOSSE[0], GESCHOSSE[0] + GESCHOSSE[1]
    for M in (bs.wandmatrix((m.x, m.y - halb, Z_SOCKEL), "vorne"), bs.wandmatrix((m.x + halb, m.y, Z_SOCKEL), "rechts")):
        for v, h in ((v1 + 0.6, 1.3), (v2 + 0.4, 1.15), (H_WAND + 0.25, 0.7)):
            o = Oeffnung("fenster", 0.0, v, 0.55, h)
            bs.gewaende(netz, M, o, st, kreuz=False)
            leer.append((M, o))
    bs.turmhelm(netz, st, m, (halb + 0.12) * math.sqrt(2), z1, 2.4, stoff="Schiefer", n=4)


def glatter_giebel(netz, st, M, dach, z_dach0, stoff, leer, fenster):
    """Glatter Giebel: Mauerscheibe etwas über der Dachfläche, Sandsteinkanten entlang der Ortgänge,
    Gesimsband in Höhe der Kehlbalkenlage, Sandsteinkugeln an den Fußpunkten und auf der Spitze."""
    tan = dach["tan"]
    hoch = z_dach0 + HX * tan + 0.15
    rand = [(-HX - 0.05, 0.0), (HX + 0.05, 0.0), (HX + 0.05, z_dach0 + 0.12), (0.0, hoch), (-HX - 0.05, z_dach0 + 0.12)]
    bs.platte(netz, M, rand, 0, 0.4, st[stoff])
    winkel = math.atan(tan)
    laenge = math.hypot(HX + 0.05, hoch - z_dach0 - 0.12)
    for s in (-1, 1):
        mitte = M @ Vector((s * (HX + 0.05) / 2, 0.2, (z_dach0 + 0.12 + hoch) / 2 + 0.06))
        t = (Matrix.Translation(mitte) @ M.to_3x3().to_4x4()
             @ Matrix.Rotation(s * winkel, 4, "Y"))
        netz.quader((0, 0, 0), (laenge - 0.1, 0.52, 0.14), st["Sandstein"], t)      # an der Spitze sitzt die Kugel
        bs.achteck(netz, st, M @ Vector((s * (HX - 0.15), 0.2, z_dach0 + 0.2)), 0.18, 0.0, 0.36, "Sandstein", n=8)
    netz.quader((0, 0.2, 2.2), (2 * (HX - 2.2 / tan) + 0.1, 0.5, 0.14), st["Sandstein"], M)
    bs.achteck(netz, st, M @ Vector((0, 0.2, hoch + 0.05)), 0.2, 0.0, 0.42, "Sandstein", n=8)
    for o in fenster:
        bs.gewaende(netz, M, o, st)
        leer.append((M, o))


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv

    def nur(name):
        return lambda o: not o.name.startswith("Variante") or o.name == name

    bs.vorschau_buehne(FLAECHE, ziel_hoehe=7.5, ausschnitt=24.0, schnell=schnell)
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, nur("Variante1")))
        return
    if "--nur-anker" in sys.argv:
        print(bs.anker_bild(NAME, nur("Variante1")))
        return
    bilder = [bs.rendern(f"{NAME}-{k}", nur(f"Variante{k}")) for k in (1, 2, 3)]
    print(bs.nebeneinander(NAME, bilder, [f"Variante {k}" for k in (1, 2, 3)]))
    if not schnell:
        for k in (1, 2, 3):
            print(bs.rundum(f"{NAME}-{k}", nur(f"Variante{k}")))
    print(bs.anker_bild(NAME, nur("Variante1")))
    print(bs.nachtbild(NAME, nur("Variante1")))


if __name__ == "__main__":
    main()
