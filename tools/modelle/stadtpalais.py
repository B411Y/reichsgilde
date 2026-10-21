"""Stadtpalais – vierte Stufe des Wohnhauses der Spielhäuser (Stand Edler). Belegte Fläche 12 × 10 m.

    blender -b --python tools/modelle/stadtpalais.py        oder
    python tools/modelle/stadtpalais.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/stadtpalais.glb und Bilder nach docs/modelle/.

Objekte im GLB wie bei den anderen Wohnhäusern: Haus, Wappen, Fahne, Leerstand und Geruest
(die beiden letzten standardmäßig aus), dazu der Anker „Anker_Tuer“.

Ein Palais aus hellem Haustein (Blender: Z oben, Gasse bei -Y), wie bisher im Spiel mit Erker und zwei Ecktürmchen:
    9,8 × 7,6 m, drei Geschosse mit Lagerfugen, Eckquadern und Gesimsen aus Sandstein, Schieferdach mit der
    Traufe zur Gasse und Treppengiebeln an beiden Seiten. In der Mitte springt ein Risalit vor: unten das
    Rundbogenportal mit Freitreppe, darüber ein Erker über zwei Geschosse mit dem Wappen, oben ein Zwerchhaus
    mit Treppengiebel und der Fahne. An den vorderen Ecken zwei achteckige Türmchen mit spitzem Schieferhelm.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "stadtpalais"
FLAECHE = (12.0, 10.0)

# Farben nur für dieses Modell: heller Haustein wie der Platzhalter im Spiel, die Fugen passend heller
# (jedes Modell läuft in einem eigenen Prozess, die anderen Modelle behalten ihre Fuge)
bs.FARBEN.update({
    "Palaststein": (0.70, 0.66, 0.58),
    "Fuge": (0.55, 0.52, 0.46),
})

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
HX, VORN, HINTEN = 4.9, -3.0, 4.4          # Baukörper x ±4,9, y von -3 bis 4,4
TIEF = HINTEN - VORN
Y_MITTE = (VORN + HINTEN) / 2
Z_SOCKEL = 0.5
GESCHOSSE = (3.4, 3.2, 2.7)                # Erdgeschoss, Beletage, 2. Obergeschoss
H_WAND = sum(GESCHOSSE)
Z_TRAUFE = Z_SOCKEL + H_WAND               # 9,8
Z_GESIMS = 0.26                            # Dach sitzt auf dem Traufgesims
NEIGUNG = 50
GIEBEL_DICKE = 0.45
ZW, VOR_RISALIT, H_ZW = 1.8, 0.3, 2.4     # Risalit und Zwerchhaus: halbe Breite, Vorsprung, Wand über der Traufe
Y_RIS = VORN - VOR_RISALIT
ERKER_TIEF, ERKER_HALB = 0.6, 1.3
TURM_R = 1.05                              # Türmchen: Umkreis des Achtecks
TURM_X, TURM_Y = HX - 0.1, VORN + 0.1
TURM_HOCH = Z_TRAUFE + 2.2


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []
    stein = st["Palaststein"]
    v1, v2 = GESCHOSSE[0], GESCHOSSE[0] + GESCHOSSE[1]

    # ── Sockel, Baukörper, Gesimse, Eckquader ─────────────────────────────────
    haus.quader((0, Y_MITTE, Z_SOCKEL / 2), (2 * HX + 0.12, TIEF + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    haus.quader((0, Y_MITTE, Z_SOCKEL + H_WAND / 2), (2 * HX, TIEF, H_WAND), stein, ohne=("+z",))
    for z, h, vor in ((Z_SOCKEL - 0.06, 0.12, 0.09), (Z_SOCKEL + v1, 0.16, 0.07), (Z_SOCKEL + v2, 0.16, 0.07),
                      (Z_TRAUFE, Z_GESIMS, 0.13)):
        bs.gesims(haus, st, -HX, HX, VORN, HINTEN, z, hoehe=h, vor=vor)
    for sx in (-1, 1):
        bs.eckquader(haus, st, sx * HX, HINTEN, sx, 1, Z_SOCKEL, Z_TRAUFE)

    # ── Risalit in der Mitte: vom Sockel bis über die Traufe (dort das Zwerchhaus) ──
    haus.quader((0, (Y_RIS + VORN) / 2 - 0.06, Z_SOCKEL / 2), (2 * ZW + 0.12, VOR_RISALIT, Z_SOCKEL),
                st["Bruchstein dunkel"], ohne=("+y",))
    haus.quader((0, Y_RIS + VOR_RISALIT / 2, Z_SOCKEL + H_WAND / 2), (2 * ZW, VOR_RISALIT, H_WAND), stein, ohne=("+y", "+z"))
    zw_tief = 3.8
    haus.quader((0, Y_RIS + (VOR_RISALIT + zw_tief) / 2, Z_TRAUFE + H_ZW / 2), (2 * ZW, VOR_RISALIT + zw_tief, H_ZW), stein,
                ohne=("+z", "-z", "+y"))
    for z, h, vor in ((Z_SOCKEL + v1, 0.16, 0.07), (Z_SOCKEL + v2, 0.16, 0.07), (Z_TRAUFE, Z_GESIMS, 0.13)):
        bs.gesims(haus, st, -ZW, ZW, Y_RIS, VORN - 0.01, z, hoehe=h + 0.03, vor=vor)   # etwas höher: keine gemeinsame Fläche
    for sx in (-1, 1):
        bs.eckquader(haus, st, sx * ZW, Y_RIS, sx, -1, Z_SOCKEL, Z_TRAUFE + H_ZW)

    # ── Wände: Lagerfugen, Steinfenster, Portal ───────────────────────────────
    M_vorn = bs.wandmatrix((0, VORN, Z_SOCKEL), "vorne")
    M_ris = bs.wandmatrix((0, Y_RIS, Z_SOCKEL), "vorne")
    M_rechts = bs.wandmatrix((HX, Y_MITTE, Z_SOCKEL), "rechts")
    M_hinten = bs.wandmatrix((0, HINTEN, Z_SOCKEL), "hinten")
    M_links = bs.wandmatrix((-HX, Y_MITTE, Z_SOCKEL), "links")
    F = "fenster"
    eg = dict(v=1.0, b=1.0, h=1.55)
    og1 = dict(v=v1 + 0.7, b=1.0, h=1.75)             # Beletage mit hohen Fenstern
    og2 = dict(v=v2 + 0.55, b=0.9, h=1.3)

    def fenster(M, liste, verdachung=False):
        for o in liste:
            bs.gewaende(haus, M, o, st, verdachung=verdachung)
            oeffnungen.append((M, o))

    for M, L in ((M_vorn, 2 * HX), (M_rechts, TIEF), (M_hinten, 2 * HX), (M_links, TIEF)):
        bs.fugen(haus, M, L - 0.02, 0.0, H_WAND, st)
    bs.fugen(haus, M_ris, 2 * ZW - 0.02, 0.0, H_WAND + H_ZW, st)
    seitlich = (-2.75, 2.75)                          # zwischen Risalit und Türmchen
    fenster(M_vorn, [Oeffnung(F, u, **eg) for u in seitlich])
    fenster(M_vorn, [Oeffnung(F, u, **og1) for u in seitlich], verdachung=True)
    fenster(M_vorn, [Oeffnung(F, u, **og2) for u in seitlich])
    for M in (M_rechts, M_links):
        fenster(M, [Oeffnung(F, u, **eg) for u in (-0.4, 1.9)])
        fenster(M, [Oeffnung(F, u, **og1) for u in (-0.4, 1.9)], verdachung=True)
        fenster(M, [Oeffnung(F, u, **og2) for u in (-0.4, 1.9)])
    fenster(M_hinten, [Oeffnung(F, u, **eg) for u in (-3.0, -1.0, 3.0)])
    fenster(M_hinten, [Oeffnung(F, u, **og1) for u in (-3.0, -1.0, 1.0, 3.0)], verdachung=True)
    fenster(M_hinten, [Oeffnung(F, u, **og2) for u in (-3.0, -1.0, 1.0, 3.0)])
    tor = Oeffnung("tuer", 0.0, 0.0, 1.5, 2.9)
    bs.portal(haus, M_ris, tor, st)
    oeffnungen.append((M_ris, tor))
    for k, (tiefe, hoehe) in enumerate(((1.0, 0.16), (0.7, 0.32), (0.4, 0.48))):
        haus.quader((0, Y_RIS - tiefe / 2, hoehe / 2), (2.5 - 0.25 * k, tiefe, hoehe), st["Sandstein"], ohne=("+y",))
    hinten = Oeffnung("tuer", 1.2, 0.0, 1.1, 2.3)
    bs.tuer(haus, M_hinten, hinten, st)
    haus.quader((1.2, -0.03, 2.4), (1.5, 0.1, 0.22), st["Sandstein"], M_hinten, ohne=("+y",))
    oeffnungen.append((M_hinten, hinten))
    haus.quader((1.2, HINTEN + 0.2, 0.24), (1.4, 0.4, 0.48), st["Sandstein"], ohne=("-y",))
    laterne(haus, st, M_ris, -1.35, 2.55)

    # ── Erker über Beletage und 2. Obergeschoss, darauf ein kleines Schieferdach ─
    wappen = bs.Netz("Wappen")
    erker(haus, wappen, st, oeffnungen, v1, v2)

    # ── Schieferdach mit der Traufe zur Gasse, Treppengiebel an den Seiten ─────
    dach = bs.satteldach(haus, st, -HX, HX, VORN, HINTEN, Z_TRAUFE + Z_GESIMS, NEIGUNG, traufe=0.35, ortgang=-0.25,
                         enden="Palaststein", first_dicke=0.2, **bs.SCHIEFER)
    z_dach0 = Z_GESIMS + dach["dicke"] / math.cos(math.radians(NEIGUNG))
    for x, seite in ((HX, "rechts"), (-HX, "links")):
        bs.treppengiebel(haus, st, bs.wandmatrix((x, Y_MITTE, Z_TRAUFE), seite), TIEF / 2, z_dach0, dach["tan"], oeffnungen,
                      [Oeffnung("fenster", u, 0.85, 0.7, 1.1) for u in (-1.3, 1.3)] + [Oeffnung("fenster", 0.0, 2.75, 0.55, 0.85)],
                         stoff="Palaststein")

    # ── Zwerchhaus über dem Risalit: Fenster, eigenes Dach, Treppengiebel mit der Fahne ──
    M_zw = bs.wandmatrix((0, Y_RIS, Z_TRAUFE), "vorne")
    fenster(M_zw, [Oeffnung(F, u, 0.85, 0.75, 1.15) for u in (-0.65, 0.65)])
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")      # lokal x → Welt y, lokal y → Welt -x
    zdach = bs.satteldach(haus, st, Y_RIS, VORN + zw_tief, -ZW, ZW, Z_TRAUFE + H_ZW, NEIGUNG, traufe=0.25, ortgang=-0.25,
                          enden="Palaststein", first_dicke=0.18, matrix=drehung, **bs.SCHIEFER)
    oben = bs.treppengiebel(haus, st, bs.wandmatrix((0, Y_RIS, Z_TRAUFE + H_ZW), "vorne"), ZW,
                         zdach["dicke"] / math.cos(math.radians(NEIGUNG)), zdach["tan"], oeffnungen,
                         [Oeffnung("fenster", 0.0, 0.55, 0.55, 0.85)], stufen=3, oben_halb=0.35, stoff="Palaststein")
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (0.0, Y_RIS + GIEBEL_DICKE / 2, Z_TRAUFE + H_ZW + oben), hoehe=2.4)

    # Gauben auf der hinteren Dachfläche, zwei Schornsteine
    dach_hinten = dict(dach, matrix=dach["matrix"] @ Matrix.Rotation(math.radians(180), 4, "Z"))
    for x0 in (-3.2, 1.6):
        bs.gaube(haus, st, dach_hinten, oeffnungen, x0=x0, x1=x0 + 1.5, y_front=-HINTEN + 1.15, hoehe=1.35, art="stein",
                 koerper="Palaststein")
    for xs in (-2.4, 2.6):
        ys = 2.4
        z_dach = dach["oben_hinten"](ys) - 0.3
        hoehe = dach["z_first"] + 0.9 - z_dach
        haus.quader((xs, ys, z_dach + hoehe / 2), (0.7, 0.7, hoehe), stein)
        haus.quader((xs, ys, z_dach + hoehe + 0.06), (0.9, 0.9, 0.12), st["Sandstein"])

    # ── Zwei achteckige Ecktürmchen mit Schieferhelm ──────────────────────────
    for sx in (-1, 1):
        tuermchen(haus, st, oeffnungen, sx, v1, v2)

    # ── Leerstand ─────────────────────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for seite, x, s, r in (("vorn", -2.9, 2.2, 0.4), ("vorn", 3.0, 4.0, 0.3), ("hinten", 0.4, 3.2, 0.45)):
        bs.dachloch(leer, st, dach, seite, x, s, r)
    for x, y in ((-3.2, VORN - 0.15), (1.9, Y_RIS - 1.1), (HX + 0.15, -0.6), (HX + 0.14, 3.4), (-HX - 0.15, 1.0),
                 (-0.6, HINTEN + 0.15), (-TURM_X - 0.6, TURM_Y - 1.0)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)

    # ── Gerüst vor der Gassenseite über alle drei Geschosse ──────────────────
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=-HX - 0.9, x1=HX + 0.9, y_innen=Y_RIS - ERKER_TIEF - 0.22, y_aussen=-4.55,
                  ebenen=(2.6, 5.2, 7.8), hoehe_innen=10.2, hoehe_aussen=10.8, felder=4)

    # Ankerpunkt: auf der obersten Stufe der Freitreppe vor dem Portal, Blick ins Haus
    tuer = bs.anker("Anker_Tuer", (0.0, Y_RIS - 0.2, 0.48))
    return [haus.objekt(), wappen.objekt(), fahne.objekt(), bs.zustand(leer.objekt()), bs.zustand(geruest.objekt()), tuer]


def erker(netz, wappen, st, oeffnungen, v1, v2):
    """Erker vor dem Risalit über Beletage und 2. Obergeschoss: Steinkörper auf zwei Kragsteinen, Fenster an drei
    Seiten, Gesimse, Wappen zwischen den Geschossen, oben ein Pultdach aus Schiefer an der Zwerchhauswand."""
    stein = st["Palaststein"]
    z0, z1 = Z_SOCKEL + v1 + 0.15, Z_TRAUFE
    y0 = Y_RIS - ERKER_TIEF
    netz.quader((0, Y_RIS - ERKER_TIEF / 2, (z0 + z1) / 2), (2 * ERKER_HALB, ERKER_TIEF, z1 - z0), stein, ohne=("+y",))
    # Kragsteine: drei Lagen, nach unten schmaler
    for sx in (-1, 1):
        for k, (b, t, h) in enumerate(((0.5, 0.62, 0.16), (0.42, 0.45, 0.16), (0.34, 0.28, 0.16))):
            netz.quader((sx * (ERKER_HALB - 0.3), Y_RIS - t / 2, z0 - 0.08 - 0.16 * k), (b, t, h), st["Sandstein"], ohne=("+y",))
    for z, h, vor in ((z0, 0.14, 0.06), (Z_SOCKEL + v2, 0.14, 0.06), (z1 - 0.2, 0.2, 0.1)):
        netz.quader((0, Y_RIS - (ERKER_TIEF + vor) / 2, z + h / 2 - 0.02),
                    (2 * ERKER_HALB + 2 * vor, ERKER_TIEF + vor, h), st["Sandstein"], ohne=("+y",))
    M_vorn = bs.wandmatrix((0, y0, Z_SOCKEL), "vorne")
    M_r = bs.wandmatrix((ERKER_HALB, Y_RIS - ERKER_TIEF / 2, Z_SOCKEL), "rechts")
    M_l = bs.wandmatrix((-ERKER_HALB, Y_RIS - ERKER_TIEF / 2, Z_SOCKEL), "links")
    liste = []
    for v, h in ((v1 + 0.75, 1.55), (v2 + 0.45, 1.3)):
        liste += [(M_vorn, Oeffnung("fenster", u, v, 0.75, h)) for u in (-0.6, 0.6)]
        liste += [(M, Oeffnung("fenster", 0.0, v, 0.36, h)) for M in (M_r, M_l)]
    for M, o in liste:
        bs.gewaende(netz, M, o, st, kreuz=o.b > 0.5)
        oeffnungen.append((M, o))
    bs.wappen(wappen, M_vorn, 0.0, v1 + 2.95, st, groesse=0.62, rahmen="Sandstein")
    # Pultdach: von der Zwerchhauswand zur Erkerkante
    hoch, tief = Z_TRAUFE + 0.95, Z_TRAUFE + 0.12
    profil = [(Y_RIS + 0.01, hoch), (y0 - 0.15, tief), (y0 - 0.15, tief + 0.12), (Y_RIS + 0.01, hoch + 0.12)]
    netz.prisma(profil, -ERKER_HALB - 0.22, ERKER_HALB + 0.22, st["Schiefer"], m_enden=st["Schieferkante"])


def tuermchen(netz, st, oeffnungen, sx, v1, v2):
    """Achteckiges Ecktürmchen an der vorderen Ecke: Sockel, Gesimse in Geschosshöhe, kleine Steinfenster nach vorn
    und zur Seite, spitzer Schieferhelm mit Knauf und Eisenspitze."""
    mitte = Vector((sx * TURM_X, TURM_Y, 0))
    bs.achteck(netz, st, mitte, TURM_R + 0.1, 0.0, Z_SOCKEL + 0.04, "Bruchstein dunkel")   # Ringe etwas höher als die
                                                                                       # Gesimse am Haus: keine gemeinsame Fläche
    bs.achteck(netz, st, mitte, TURM_R, Z_SOCKEL, TURM_HOCH - 0.1, "Palaststein")
    for z, h, mehr in ((Z_SOCKEL + v1, 0.2, 0.08), (Z_SOCKEL + v2, 0.2, 0.08), (Z_TRAUFE, 0.3, 0.1), (TURM_HOCH - 0.22, 0.22, 0.14)):
        bs.achteck(netz, st, mitte, TURM_R + mehr, z, z + h, "Sandstein")
    apo = TURM_R * math.cos(math.radians(22.5))            # Abstand der Achteckseiten von der Mitte
    seite = "rechts" if sx > 0 else "links"
    for M in (bs.wandmatrix((mitte.x, mitte.y - apo, 0), "vorne"), bs.wandmatrix((mitte.x + sx * apo, mitte.y, 0), seite)):
        for v in (Z_SOCKEL + v1 + 0.8, Z_SOCKEL + v2 + 0.6, Z_TRAUFE + 0.7):
            o = Oeffnung("fenster", 0.0, v, 0.42, 0.95)
            bs.gewaende(netz, M, o, st, kreuz=False)
            oeffnungen.append((M, o))
    bs.turmhelm(netz, st, mitte, TURM_R + 0.22, TURM_HOCH, 3.6)


def laterne(netz, st, M, u, v):
    """Schmiedeeiserne Laterne an einem Wandarm neben dem Portal."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v + 0.35)), M @ Vector((u, -0.55, v + 0.35)), 0.04, 0.04, eisen)
    netz.stab(M @ Vector((u, 0, v)), M @ Vector((u, -0.45, v + 0.33)), 0.03, 0.03, eisen)
    mitte = M @ Vector((u, -0.5, v + 0.05))
    netz.quader(mitte, (0.22, 0.22, 0.3), st["Fenster"])          # leuchtet nachts mit den Fenstern
    netz.quader(mitte + Vector((0, 0, 0.2)), (0.3, 0.3, 0.08), eisen)
    netz.quader(mitte - Vector((0, 0, 0.18)), (0.26, 0.26, 0.05), eisen)


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    normal = lambda o: o.name not in ("Leerstand", "Geruest")                     # noqa: E731
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=8.0, ausschnitt=30.0, schnell=schnell)
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, normal))
        return
    if "--nur-anker" in sys.argv:
        print(bs.anker_bild(NAME, normal))
        return
    print(bs.rendern(NAME, normal))
    print(bs.rendern(NAME + "-leerstand", lambda o: o.name not in ("Geruest", "Wappen", "Fahne")))
    print(bs.rendern(NAME + "-geruest", lambda o: o.name != "Leerstand"))
    if not schnell:
        print(bs.rundum(NAME, normal))
    print(bs.anker_bild(NAME, normal))
    print(bs.nachtbild(NAME, normal))


if __name__ == "__main__":
    main()
