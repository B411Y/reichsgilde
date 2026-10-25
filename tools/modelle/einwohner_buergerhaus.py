"""Bürgerhaus der Einwohner – drei Varianten in einem Modell. Belegte Fläche 8 × 10 m.

    blender -b --python tools/modelle/einwohner_buergerhaus.py        oder
    python tools/modelle/einwohner_buergerhaus.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/einwohner_buergerhaus.glb und Bilder nach docs/modelle/.

Objekte im GLB:
    Variante1   heller Kalkputz, Fachwerkgiebel, Ziegeldach
    Variante2   rötlicher Putz, verputzter Giebel mit Ladeluke und Kranbalken, Schieferdach
    Variante3   grauer Putz, vorkragender Fachwerkgiebel, Ziegeldach, Laden mit Klappe im Erdgeschoss
    Anker_Tuer  vor der Haustür (in allen Varianten an derselben Stelle)
Das Spiel zeigt genau eine Variante (nach der Hausnummer). Leerstand und Gerüst gibt es nicht. Läden und Tür tragen
„Hausfarbe“ (hier ein gedecktes Grün), das Spiel kann sie je Haus umfärben.

Aufbau wie bisher im Spiel (verputzt, Giebel zur Gasse, zwei Geschosse), Blender: Z oben, Gasse bei -Y:
    Haus 6,6 × 6,8 m (mit Dachüberstand im 7-m-Raster), Front bei y = -3 (2 m Vorplatz), Satteldach mit dem
    Giebel zur Gasse, Fenster mit Putzfaschen und Läden, Haustür rechts.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "einwohner_buergerhaus"
FLAECHE = (8.0, 10.0)

# Farben nur für dieses Modell (jedes Modell läuft in einem eigenen Prozess)
bs.FARBEN.update({
    "Hausfarbe": (0.24, 0.33, 0.24),       # Läden und Tür in gedecktem Grün; das Spiel kann je Haus umfärben
    "Fasche": (0.88, 0.85, 0.78),          # helle Putzfaschen um die Fenster
    "Putz rot": (0.76, 0.58, 0.50),
    "Putz grau": (0.70, 0.70, 0.66),
})

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
HX, VORN, HINTEN = 3.3, -3.0, 3.8
TIEF = HINTEN - VORN
Y_MITTE = (VORN + HINTEN) / 2
Z_SOCKEL = 0.4
GESCHOSSE = (3.0, 2.7)
H_WAND = sum(GESCHOSSE)
Z_TRAUFE = Z_SOCKEL + H_WAND               # 6,1
NEIGUNG = 52
TUER_U = 1.3                               # Haustür rechts der Mitte, in allen Varianten gleich (Anker)

VARIANTEN = {
    "Variante1": dict(putz="Putz", giebel="fachwerk", dach=bs.ZIEGEL, laden=False),
    "Variante2": dict(putz="Putz rot", giebel="luke", dach=bs.SCHIEFER, laden=False),
    "Variante3": dict(putz="Putz grau", giebel="vorkragend", dach=bs.ZIEGEL, laden=True),
}


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    objekte = []
    for name, v in VARIANTEN.items():
        netz = bs.Netz(name)
        haus(netz, st, v)
        objekte.append(netz.objekt())
    objekte.append(bs.anker("Anker_Tuer", (TUER_U, VORN - 0.25, 0.3)))      # auf der oberen Stufe vor der Tür
    return objekte


def haus(netz, st, v):
    putz = st[v["putz"]]
    v1 = GESCHOSSE[0]
    netz.quader((0, Y_MITTE, Z_SOCKEL / 2), (2 * HX + 0.1, TIEF + 0.1, Z_SOCKEL), st["Bruchstein"])
    netz.quader((0, Y_MITTE, Z_SOCKEL + H_WAND / 2), (2 * HX, TIEF, H_WAND), putz, ohne=("+z",))
    # Gurtgesims zwischen den Geschossen und Traufgesims aus Holz
    bs.gesims(netz, st, -HX, HX, VORN, HINTEN, Z_SOCKEL + v1, hoehe=0.12, vor=0.05, stoff="Fasche")
    bs.gesims(netz, st, -HX, HX, VORN, HINTEN, Z_TRAUFE - 0.14, hoehe=0.14, vor=0.08, stoff="Holz")

    M_vorn = bs.wandmatrix((0, VORN, Z_SOCKEL), "vorne")
    M_hinten = bs.wandmatrix((0, HINTEN, Z_SOCKEL), "hinten")
    M_r = bs.wandmatrix((HX, Y_MITTE, Z_SOCKEL), "rechts")
    M_l = bs.wandmatrix((-HX, Y_MITTE, Z_SOCKEL), "links")
    F = "fenster"
    eg = dict(v=0.95, b=0.85, h=1.25)
    og = dict(v=v1 + 0.55, b=0.85, h=1.3)
    leer = []

    def fenster(M, liste, laeden=True):
        for o in liste:
            bs.gewaende(netz, M, o, st, stein="Fasche")
            if laeden:
                fensterlaeden(netz, st, M, o)
            leer.append((M, o))

    # Gassenseite: Haustür rechts, im Erdgeschoss Fenster oder Laden
    if v["laden"]:
        ladenfront(netz, st, M_vorn, -1.3, leer)        # Laden statt der beiden Fenster
    else:
        fenster(M_vorn, [Oeffnung(F, u, **eg) for u in (-2.2, -0.4)])
    fenster(M_vorn, [Oeffnung(F, u, **og) for u in (-2.2, -0.4, 1.3)])
    tuer = Oeffnung("tuer", TUER_U, 0.0, 1.05, 2.25)
    bs.tuer(netz, M_vorn, tuer, st)
    netz.quader((TUER_U, -0.03, 2.35), (1.35, 0.1, 0.2), st["Fasche"], M_vorn, ohne=("+y",))
    for k, (tiefe, hoehe) in enumerate(((0.6, 0.15), (0.3, 0.3))):
        netz.quader((TUER_U, VORN - tiefe / 2, hoehe / 2), (1.5 - 0.15 * k, tiefe, hoehe), st["Bruchstein"], ohne=("+y",))
    # Seiten und Rückseite
    for M in (M_r, M_l):
        fenster(M, [Oeffnung(F, u, **eg) for u in (-1.6, 1.6)])
        fenster(M, [Oeffnung(F, u, **og) for u in (-1.6, 1.6)])
    fenster(M_hinten, [Oeffnung(F, -1.4, **eg)])
    fenster(M_hinten, [Oeffnung(F, u, **og) for u in (-1.4, 1.4)])
    hinten = Oeffnung("tuer", 1.5, 0.0, 0.95, 2.05)
    bs.tuer(netz, M_hinten, hinten, st)

    # Dach mit dem Giebel zur Gasse
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")          # lokal x → Welt y, lokal y → Welt -x
    vor = 0.3 if v["giebel"] == "vorkragend" else 0.0               # so dick wie die Giebelplatte              # vorkragender Giebel: Dach reicht weiter vor
    dach = bs.satteldach(netz, st, VORN - vor, HINTEN, -HX, HX, Z_TRAUFE, NEIGUNG, traufe=0.3, ortgang=0.3,
                         enden="Holz", matrix=drehung, **v["dach"])
    hoch = HX * dach["tan"]
    for y, seite in ((VORN, "vorne"), (HINTEN, "hinten")):
        if seite == "vorne" and v["giebel"] == "vorkragend":
            vorkragender_giebel(netz, st, hoch, vor, leer)
            continue
        M = bs.wandmatrix((0, y, Z_TRAUFE), seite)
        if v["giebel"] == "luke" and seite == "vorne":
            ladeluke(netz, st, M, hoch, v["putz"], leer)
        elif v["giebel"] in ("fachwerk", "vorkragend"):
            bs.fachwerkgiebel(netz, st, M, 2 * HX, hoch, leer, laeden=True)
        else:
            putzgiebel(netz, st, M, hoch, v["putz"], leer)
    xs, ys = 1.4, 1.9                        # Schornstein auf der linken Dachfläche (lokal y = -Welt x)
    z_dach = dach["oben_vorn"](-xs) - 0.3
    hoehe = dach["z_first"] + 0.7 - z_dach
    netz.quader((xs, ys, z_dach + hoehe / 2), (0.6, 0.6, hoehe), st["Bruchstein dunkel"])
    netz.quader((xs, ys, z_dach + hoehe + 0.05), (0.78, 0.78, 0.1), st["Bruchstein"])


def fensterlaeden(netz, st, M, o, winkel=55):
    """Zwei halb geöffnete Läden in der Hausfarbe neben einem Steinfenster."""
    breite = o.b / 2
    for s in (-1, 1):
        dreh = -winkel if s > 0 else 180 + winkel
        t = (M @ Matrix.Translation((o.u + s * (o.b / 2 + 0.16), -0.07, o.v + o.h / 2))
             @ Matrix.Rotation(math.radians(dreh), 4, "Z"))
        netz.quader((breite / 2, 0, 0), (breite, 0.035, o.h), st["Hausfarbe"], t)


def ladenfront(netz, st, M, u, leer):
    """Laden im Erdgeschoss: breites Fenster mit Faschen, davor eine heruntergeklappte Ladenklappe als Auslage
    auf zwei Stützen und darüber ein kleines Vordach aus Brettern."""
    o = Oeffnung("fenster", u, 0.7, 1.9, 1.35)
    bs.gewaende(netz, M, o, st, stein="Fasche")
    leer.append((M, o))
    klappe = M @ Vector((u, -0.35, 0.68))
    netz.quader(klappe, (1.9, 0.62, 0.06), st["Hausfarbe"])
    for s in (-1, 1):
        netz.stab(M @ Vector((u + s * 0.8, -0.6, 0.0)), M @ Vector((u + s * 0.8, -0.6, 0.65)), 0.06, 0.06, st["Holz"])
    for k, ware in enumerate(("Gold", "Leder", "Kohle", "Seil")):
        netz.quader(M @ Vector((u - 0.65 + 0.43 * k, -0.35, 0.78)), (0.24, 0.22, 0.14), st[ware])
    hoch, tief = 2.45, 2.2
    profil = [(-0.02, hoch), (-0.75, tief), (-0.75, tief + 0.06), (-0.02, hoch + 0.06)]
    netz.prisma(profil, u - 1.15, u + 1.15, st["Holz alt"], matrix=M)     # Profil im Wandsystem (y, v), entlang u


def putzgiebel(netz, st, M, hoch, putz, leer, mit_fenster=True):
    """Glatt verputztes Giebeldreieck, auf Wunsch mit einem kleinen Fenster."""
    bs.platte(netz, M, [(-HX, 0), (HX, 0), (0, hoch)], 0, 0.3, st[putz])
    if mit_fenster:
        o = Oeffnung("fenster", 0.0, 0.7, 0.6, 0.85)
        bs.gewaende(netz, M, o, st, stein="Fasche")
        leer.append((M, o))


def ladeluke(netz, st, M, hoch, putz, leer):
    """Verputzter Giebel mit einer Ladeluke für den Dachboden und einem Kranbalken mit Rolle darüber."""
    putzgiebel(netz, st, M, hoch, putz, leer, mit_fenster=False)
    o = Oeffnung("tuer", 0.0, 0.45, 0.9, 1.25)
    netz.quader(M @ Vector((0.0, -0.005, o.v + o.h / 2)), (0.9, 0.02, 1.25), st["Holz alt"])
    for s in (-1, 1):
        netz.quader(M @ Vector((s * 0.52, -0.03, o.v + o.h / 2)), (0.14, 0.06, 1.4), st["Fachwerk"])
    netz.quader(M @ Vector((0.0, -0.03, o.v + o.h + 0.07)), (1.18, 0.06, 0.14), st["Fachwerk"])
    leer.append((M, Oeffnung("fenster", 0.0, o.v, o.b, o.h)))
    kopf = o.v + o.h + 0.55
    netz.stab(M @ Vector((0.0, 0.3, kopf)), M @ Vector((0.0, -0.9, kopf)), 0.16, 0.16, st["Fachwerk"])
    rolle = M @ Vector((0.0, -0.8, kopf - 0.18))
    netz.stab(rolle - M.to_3x3() @ Vector((0.08, 0, 0)), rolle + M.to_3x3() @ Vector((0.08, 0, 0)), 0.2, 0.2, st["Holz"])
    netz.stab(rolle, M @ Vector((0.0, -0.8, 0.2)), 0.02, 0.02, st["Seil"])


def vorkragender_giebel(netz, st, hoch, vor, leer):
    """Fachwerkgiebel, der um `vor` über die Putzfassade vorkragt, auf sichtbaren Balkenköpfen."""
    y = VORN - vor
    for k in range(7):
        u = -HX + 0.25 + k * (2 * HX - 0.5) / 6
        netz.quader((u, VORN - vor / 2, Z_TRAUFE - 0.1), (0.16, vor + 0.02, 0.2), st["Fachwerk"], ohne=("+y",))
    netz.quader((0, (VORN + y) / 2, Z_TRAUFE + 0.06), (2 * HX + 0.1, vor, 0.12), st["Fachwerk"], ohne=("+y",))
    M = bs.wandmatrix((0, y, Z_TRAUFE + 0.12), "vorne")
    bs.fachwerkgiebel(netz, st, M, 2 * HX, hoch, leer, laeden=True)


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv

    def nur(name):
        return lambda o: not o.name.startswith("Variante") or o.name == name

    bs.vorschau_buehne(FLAECHE, ziel_hoehe=5.5, ausschnitt=20.0, schnell=schnell)
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
