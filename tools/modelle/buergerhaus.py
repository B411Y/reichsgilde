"""Bürgerhaus – zweite Stufe des Wohnhauses der Spielhäuser (Stand Bürger). Belegte Fläche 8 × 10 m.

    blender -b --python tools/modelle/buergerhaus.py        oder
    python tools/modelle/buergerhaus.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/buergerhaus.glb und Bilder nach docs/modelle/.

Objekte im GLB:
    Haus        das Haus selbst
    Wappen      Schild in der Hausfarbe an der Gassenseite     (im Leerstand ausblenden)
    Fahne       Fahne in der Hausfarbe auf dem First           (im Leerstand ausblenden)
    Leerstand   Bretter vor Türen und Fenstern, Löcher im Dach, Unkraut   – standardmäßig aus
    Geruest     Baugerüst vor der Gassenseite mit Leiter                  – standardmäßig aus

Das Haus (Blender: Z oben, Gasse bei -Y):
    Bruchsteinsockel, Erdgeschoss 6,4 × 7 m, Obergeschoss kragt vorn und seitlich 35 cm vor
    (Balkenköpfe sichtbar), Satteldach 50° mit Traufe zur Gasse, Schleppgaube, Schornstein,
    Fachwerk mit Eckstreben und Andreaskreuzen unter den Fenstern im Obergeschoss.
    Vorne 2 m Vorplatz (Gasse), hinten 1 m Hof.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Oeffnung, Vector  # noqa: E402

NAME = "buergerhaus"
FLAECHE = (8.0, 10.0)

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
EG_X, EG_VORN, EG_HINTEN = 3.2, -3.0, 4.0         # Erdgeschoss: x ±3,2, y von -3 bis 4
KRAG = 0.35                                        # Vorkragung des Obergeschosses
OG_X, OG_VORN, OG_HINTEN = EG_X + KRAG, EG_VORN - KRAG, EG_HINTEN
Z_SOCKEL = 0.45
H_EG = 2.6                                         # Fachwerk des Erdgeschosses
Z_BALKENLAGE = Z_SOCKEL + H_EG                     # 3,05: hier liegen die Balkenköpfe
Z_OG = Z_BALKENLAGE + 0.2                          # 3,25
H_OG = 2.6
Z_TRAUFE = Z_OG + H_OG                             # 5,85
NEIGUNG = 50
UEBER = 0.045                                      # Fachwerk der Giebel- und Rückwand läuft um die Ecke


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []      # (Wandmatrix, Oeffnung) – für die Bretter im Leerstand

    def wand(M, L, H, liste, **art):
        bs.fachwerk(haus, M, L, H, liste, st, **art)
        for o in liste:
            (bs.tuer if o.art == "tuer" else bs.fenster)(haus, M, o, st)
            oeffnungen.append((M, o))

    y_eg = (EG_VORN + EG_HINTEN) / 2
    y_og = (OG_VORN + OG_HINTEN) / 2

    # ── Sockel mit einzelnen vorstehenden Steinen ─────────────────────────────
    haus.quader((0, y_eg, Z_SOCKEL / 2), (2 * EG_X + 0.1, EG_HINTEN - EG_VORN + 0.1, Z_SOCKEL), st["Bruchstein"])
    steine = [(-2.6, 0.12, 0.5), (-0.9, 0.3, 0.4), (2.4, 0.1, 0.55), (-3.0, 0.28, 0.35), (0.2, 0.08, 0.45)]
    for i, (u, v, b) in enumerate(steine):
        haus.quader((u, EG_VORN - 0.065, v + 0.07), (b, 0.04, 0.16), st["Bruchstein dunkel"], ohne=("+y",))
        haus.quader((EG_X + 0.065, -u * 1.1, v + 0.06), (0.04, b, 0.15), st["Bruchstein dunkel"], ohne=("-x",))
        haus.quader((-EG_X - 0.065, u * 1.1 + 0.5, v + 0.08), (0.04, b, 0.14), st["Bruchstein dunkel"], ohne=("+x",))
        haus.quader((-u, EG_HINTEN + 0.065, v + 0.07), (b, 0.04, 0.15), st["Bruchstein dunkel"], ohne=("-y",))

    # ── Putzkörper der beiden Geschosse ───────────────────────────────────────
    haus.quader((0, y_eg, (Z_SOCKEL + Z_OG) / 2), (2 * EG_X, EG_HINTEN - EG_VORN, Z_OG - Z_SOCKEL), st["Putz"], ohne=("+z",))
    haus.quader((0, y_og, Z_OG + H_OG / 2), (2 * OG_X, OG_HINTEN - OG_VORN, H_OG), st["Putz"])

    # ── Balkenlage: Stockschwelle, Balkenköpfe unter der Vorkragung, Gratstichbalken ──
    haus.quader((0, y_eg, Z_BALKENLAGE + 0.1), (2 * EG_X + 0.06, EG_HINTEN - EG_VORN + 0.06, 0.2), st["Fachwerk"], ohne=("+z",))
    z = Z_BALKENLAGE + 0.1
    for i in range(8):
        x = -EG_X + 0.45 + i * (2 * EG_X - 0.9) / 7
        haus.quader((x, (EG_VORN + OG_VORN - 0.04) / 2, z), (0.17, EG_VORN - OG_VORN + 0.04, 0.2), st["Fachwerk"], ohne=("+z", "+y"))
    for i in range(8):
        y = EG_VORN + 0.45 + i * (EG_HINTEN - EG_VORN - 0.8) / 7
        for s in (-1, 1):
            haus.quader((s * (EG_X + OG_X + 0.04) / 2, y, z), (OG_X - EG_X + 0.04, 0.17, 0.2), st["Fachwerk"],
                        ohne=("+z", "-x" if s > 0 else "+x"))
    for s in (-1, 1):
        haus.stab((s * EG_X, EG_VORN, z), (s * (OG_X + 0.05), OG_VORN - 0.05, z), 0.2, 0.2, st["Fachwerk"], quer=(0, 0, 1))

    # ── Erdgeschoss ───────────────────────────────────────────────────────────
    F, T = "fenster", "tuer"
    L_quer, L_tief = 2 * EG_X + 2 * UEBER, EG_HINTEN - EG_VORN
    wand(bs.wandmatrix((0, EG_VORN, Z_SOCKEL), "vorne"), L_quer, H_EG,
         [Oeffnung(F, -1.55, 0.9, 0.95, 1.15), Oeffnung(T, 1.2, 0.15, 1.0, 1.9)])
    wand(bs.wandmatrix((EG_X, y_eg, Z_SOCKEL), "rechts"), L_tief, H_EG,
         [Oeffnung(F, -1.8, 0.9, 0.95, 1.15), Oeffnung(F, 1.4, 0.9, 0.95, 1.15)])
    wand(bs.wandmatrix((0, EG_HINTEN, Z_SOCKEL), "hinten"), L_quer, H_EG,
         [Oeffnung(F, -1.4, 0.9, 0.95, 1.15), Oeffnung(T, 1.6, 0.15, 0.9, 1.9)])
    wand(bs.wandmatrix((-EG_X, y_eg, Z_SOCKEL), "links"), L_tief, H_EG,
         [Oeffnung(F, 1.6, 0.9, 0.95, 1.15), Oeffnung(F, -1.7, 0.9, 0.95, 1.15)])
    # Stufen vor der Haustür und vor der Hintertür
    for x, y, r in ((1.2, EG_VORN, -1), (-1.6, EG_HINTEN, 1)):
        haus.quader((x, y + r * 0.3, 0.13), (1.4, 0.6, 0.26), st["Bruchstein"])
        haus.quader((x, y + r * 0.12, 0.36), (1.25, 0.3, 0.2), st["Bruchstein"])

    # ── Obergeschoss: Andreaskreuze in der Brüstung ───────────────────────────
    L_quer, L_tief = 2 * OG_X + 2 * UEBER, OG_HINTEN - OG_VORN
    fo = dict(v=0.8, b=0.9, h=1.15)
    wand(bs.wandmatrix((0, OG_VORN, Z_OG), "vorne"), L_quer, H_OG,
         [Oeffnung(F, u, **fo) for u in (-2.45, -0.85, 2.15)], kreuze=True)
    wand(bs.wandmatrix((OG_X, y_og, Z_OG), "rechts"), L_tief, H_OG,
         [Oeffnung(F, u, **fo) for u in (-1.9, 1.7)], kreuze=True)
    wand(bs.wandmatrix((0, OG_HINTEN, Z_OG), "hinten"), L_quer, H_OG,
         [Oeffnung(F, u, **fo) for u in (-1.6, 1.6)], kreuze=True)
    wand(bs.wandmatrix((-OG_X, y_og, Z_OG), "links"), L_tief, H_OG,
         [Oeffnung(F, u, **fo) for u in (-1.7, 1.9)], kreuze=True)

    # ── Dach ──────────────────────────────────────────────────────────────────
    dach = bs.satteldach(haus, st, -OG_X, OG_X, OG_VORN, OG_HINTEN, Z_TRAUFE, NEIGUNG)
    hoch = (OG_HINTEN - OG_VORN) / 2 * dach["tan"]          # Höhe des Giebeldreiecks
    for s, seite in ((1, "rechts"), (-1, "links")):
        giebel(haus, st, bs.wandmatrix((s * OG_X, y_og, Z_TRAUFE), seite), OG_HINTEN - OG_VORN, hoch, oeffnungen)
    bs.gaube(haus, st, dach, oeffnungen, x0=-2.5, x1=-0.8, y_front=OG_VORN + 1.3)
    # Schornstein auf der Hofseite
    xs, ys = -1.5, 1.7
    z_dach = dach["oben_hinten"](ys + 0.3)
    haus.quader((xs, ys, (z_dach - 0.3 + dach["z_first"] + 0.75) / 2), (0.62, 0.62, dach["z_first"] + 0.75 - z_dach + 0.3),
                st["Bruchstein dunkel"])
    haus.quader((xs, ys, dach["z_first"] + 0.8), (0.82, 0.82, 0.1), st["Bruchstein"])

    # ── Bank vor dem Fenster und Regentonne an der Seite ──────────────────────
    holz = st["Holz"]
    haus.quader((-1.55, EG_VORN - 0.33, 0.46), (1.5, 0.36, 0.07), holz)
    for x in (-2.15, -0.95):
        haus.quader((x, EG_VORN - 0.33, 0.21), (0.08, 0.3, 0.42), holz)
    bs.fass(haus, st, Vector((EG_X + 0.42, 2.9, 0)))

    # ── Wappen über der Tür (Obergeschoss) und Fahne auf dem First ───────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, bs.wandmatrix((0, OG_VORN, Z_OG), "vorne"), 0.65, 0.95, st)
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (2.9, dach["y_first"], dach["z_first"] - 0.3))

    # ── Leerstand ─────────────────────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for seite, x, s, r in (("vorn", 0.8, 2.6, 0.42), ("vorn", 2.4, 4.4, 0.22), ("hinten", -2.0, 3.2, 0.38)):
        bs.dachloch(leer, st, dach, seite, x, s, r)
    for x, y in ((-3.1, EG_VORN - 0.15), (2.6, EG_VORN - 0.12), (EG_X + 0.12, -1.0), (-EG_X - 0.12, 2.6), (0.4, EG_HINTEN + 0.15)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.5)

    # ── Gerüst vor der Gassenseite ────────────────────────────────────────────
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=-OG_X - 0.1, x1=OG_X + 0.1, y_innen=OG_VORN - 0.25, y_aussen=OG_VORN - 1.15)

    objekte = [haus.objekt(), wappen.objekt(), fahne.objekt(), bs.zustand(leer.objekt()), bs.zustand(geruest.objekt())]
    return objekte


def giebel(netz, st, M, L, hoch, oeffnungen):
    """Giebeldreieck mit Fachwerk: Schwelle, Firstsäule, Kehlbalken, Ständer, Streben, zwei Fenster."""
    holz = st["Fachwerk"]
    halb = L / 2
    bs.platte(netz, M, [(-halb, 0), (halb, 0), (0, hoch)], 0, 0.3, st["Putz"])

    def rand(u):                     # Oberkante des Dreiecks bei u
        return hoch * (1 - abs(u) / halb)

    bs.balken(netz, M, -halb + 0.05, bs.B / 2, halb - 0.05, bs.B / 2, holz, vor=bs.VOR_SCHWELLE)
    bs.balken(netz, M, 0, bs.B, 0, hoch - 0.35, holz)
    vk = 2.2
    breite_k = halb * (1 - (vk + bs.B) / hoch) - 0.05
    bs.balken(netz, M, -breite_k, vk + bs.B / 2, breite_k, vk + bs.B / 2, holz, vor=bs.VOR_SCHWELLE)
    fb, fh, fv, fu = 0.7, 0.95, 0.55, 1.05
    for s in (-1, 1):
        for u in (fu - fb / 2 - bs.B / 2, fu + fb / 2 + bs.B / 2):
            bs.balken(netz, M, s * u, bs.B, s * u, vk, holz)
        aussen = 2.4
        bs.balken(netz, M, s * aussen, bs.B, s * aussen, rand(aussen + bs.B / 2) - 0.08, holz)
        for v in (fv - bs.B / 2, fv + fh + bs.B / 2):
            bs.balken(netz, M, s * 0.07, v, s * (fu + fb / 2 + bs.B), v, holz, vor=bs.VOR_RIEGEL)
        # Strebe im Außenfeld, Kopfstreben über dem Kehlbalken
        bs.balken(netz, M, s * (fu + fb / 2 + bs.B), bs.B, s * (aussen - bs.B / 2), rand(aussen) - 0.25, holz, vor=bs.VOR_STREBE)
        bs.balken(netz, M, s * 0.95, vk + bs.B, s * 0.08, hoch - 1.0, holz, vor=bs.VOR_STREBE)
        o = Oeffnung("fenster", s * fu, fv, fb, fh)
        bs.fenster(netz, M, o, st)
        oeffnungen.append((M, o))


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=5.0, ausschnitt=20.0, schnell="--schnell" in sys.argv)
    normal = lambda o: o.name not in ("Leerstand", "Geruest")                     # noqa: E731
    print(bs.rendern(NAME, normal))
    print(bs.rendern(NAME + "-leerstand", lambda o: o.name not in ("Geruest", "Wappen", "Fahne")))
    print(bs.rendern(NAME + "-geruest", lambda o: o.name != "Leerstand"))
    if "--schnell" not in sys.argv:
        print(bs.rundum(NAME, normal))


if __name__ == "__main__":
    main()
