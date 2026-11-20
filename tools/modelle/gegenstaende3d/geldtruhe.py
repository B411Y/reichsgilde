"""Geldtruhe mit Kastenschloss und Ueberfall (Koeln um 1400): Eichentruhe auf zwei Kufen, mit Eisenbaendern, Eckbeschlaegen, Seitengriffen,
Schlossblech mit Schluesselloch und Krampe; der Ueberfall haengt am Deckel und faellt ueber die Krampe.
Knochen: Koerper (fest), Deckel (dreht um die hintere Oberkante, nur um X), Ueberfall (dreht um seine obere Kante, nur um X).
Fuellstufen: leere Knoten Fuellstufe_viertel, Fuellstufe_halb, Fuellstufe_voll (je ein Muenznetz samt Umriss, ohne Haut); leer = alle aus.
Clips: zu, offen, aufspringen, zuklappen."""
import math

import texturen as tx

NAME = "geldtruhe"
GERUEST = "Geldtruhe_Geruest"
HAUPT = "Geldtruhe"

# Masse (m)
BX, BY = 0.30, 0.19          # halbe Aussenbreite und -tiefe des Kastens
WAND = 0.022
Z_KUFE = 0.03                 # Kastenunterkante (auf den Kufen)
Z_BODEN = 0.055               # Oberkante Innenboden
Z_RAND = 0.30                 # Oberkante Kasten = Unterseite Deckel
Z_DECKEL = 0.36
EB = 0.003                    # Eisen steht 3 mm vor dem Holz
SCHARNIER = (0.0, BY, Z_RAND)
UEB_DREH = (0.0, -BY - 0.005 - 0.024, Z_DECKEL - 0.006)   # Gelenk des Ueberfalls an der Deckelvorderseite
UMR = 0.006                   # Umriss des Kastens (1 % der Breite)
UMR_KLEIN = 0.0015            # Umriss der Muenzen und Beutel
OFFEN_GRAD = 104.0

KNOCHEN = [("Koerper", (0.0, 0.0, 0.0), None), ("Deckel", SCHARNIER, "x"), ("Ueberfall", UEB_DREH, "x")]


def materialien(G, aus):
    G.SKALA.update({"Truhe_Holz": 4.0, "Truhe_Holz_innen": 4.0, "Truhe_Eisen": 12.0, "Truhe_Muenzen": 3.6, "Truhe_Leder": 14.0})
    m = {}
    m["Truhe_Holz"] = G.mat_bild("Truhe_Holz", G.bild_laden(aus, "geldtruhe_holz", tx.holz(7401, (0.45, 0.31, 0.19), ringe=18, kontrast=0.32, fugen=2)))
    m["Truhe_Holz_innen"] = G.mat_bild("Truhe_Holz_innen", G.bild_laden(aus, "geldtruhe_holz_innen", tx.holz(7402, (0.36, 0.25, 0.15), n=128, ringe=10, kontrast=0.22, aeste=1, fugen=2)))
    m["Truhe_Eisen"] = G.mat_bild("Truhe_Eisen", G.bild_laden(aus, "geldtruhe_eisen", tx.eisen(7403)), rau=0.6)
    m["Truhe_Muenzen"] = G.mat_bild("Truhe_Muenzen", G.bild_laden(aus, "geldtruhe_muenzen", tx.muenzen(7404)), rau=0.45)
    m["Truhe_Leder"] = G.mat_bild("Truhe_Leder", G.bild_laden(aus, "geldtruhe_leder", tx.leder(7405)))
    m["Truhe_Schluesselloch"] = G.mat_flaeche("Truhe_Schluesselloch", (0.03, 0.025, 0.02))
    m["Truhe_Gold"] = G.mat_flaeche("Truhe_Gold", (0.86, 0.66, 0.24), rau=0.4)
    m["Truhe_Silber"] = G.mat_flaeche("Truhe_Silber", (0.74, 0.74, 0.70), rau=0.4)
    m["Truhe_Schnur"] = G.mat_flaeche("Truhe_Schnur", (0.62, 0.52, 0.34))
    return m


def _kasten(t):
    H, E = "Truhe_Holz", "Truhe_Eisen"
    innen = {"+y": "Truhe_Holz_innen", "*": H}
    # Waende: vorn, hinten (ganze Breite), Seiten dazwischen; Innenseiten mit hellerem Holz
    t.box(-BX, BX, -BY, -BY + WAND, Z_KUFE, Z_RAND, "Koerper", innen, UMR, ohne=("-z",))
    t.box(-BX, BX, BY - WAND, BY, Z_KUFE, Z_RAND, "Koerper", {"-y": "Truhe_Holz_innen", "*": H}, UMR, ohne=("-z",))
    t.box(-BX, -BX + WAND, -BY + WAND, BY - WAND, Z_KUFE, Z_RAND, "Koerper", {"+x": "Truhe_Holz_innen", "*": H}, UMR, ohne=("-z", "-y", "+y"))
    t.box(BX - WAND, BX, -BY + WAND, BY - WAND, Z_KUFE, Z_RAND, "Koerper", {"-x": "Truhe_Holz_innen", "*": H}, UMR, ohne=("-z", "-y", "+y"))
    t.flaeche([(-BX + WAND, -BY + WAND, Z_BODEN), (BX - WAND, -BY + WAND, Z_BODEN), (BX - WAND, BY - WAND, Z_BODEN), (-BX + WAND, BY - WAND, Z_BODEN)], (0, 0, 1), "Truhe_Holz_innen", "Koerper", UMR)
    t.flaeche([(-BX, -BY, Z_KUFE), (BX, -BY, Z_KUFE), (BX, BY, Z_KUFE), (-BX, BY, Z_KUFE)], (0, 0, -1), H, "Koerper", UMR)
    # Kufen
    for sx in (-1, 1):
        t.box(sx * 0.22 - 0.028, sx * 0.22 + 0.028, -BY - 0.008, BY + 0.008, 0.0, Z_KUFE, "Koerper", H, UMR, ohne=("-z",))
    # senkrechte Baender vorn und hinten
    for xs in (-0.17, 0.17):
        t.box(xs - 0.018, xs + 0.018, -BY - EB, -BY, Z_KUFE, Z_RAND - 0.004, "Koerper", E, UMR, ohne=("+y", "-z"))
        t.box(xs - 0.018, xs + 0.018, BY, BY + EB, Z_KUFE, Z_RAND - 0.004, "Koerper", E, UMR, ohne=("-y", "-z"))
    # Eckbeschlaege (je zwei Platten an den senkrechten Kanten)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x_a = sx * BX
            y_a = sy * BY
            t.box(min(x_a, x_a - sx * 0.04), max(x_a, x_a - sx * 0.04), min(y_a, y_a + sy * EB), max(y_a, y_a + sy * EB), Z_KUFE, Z_RAND - 0.004, "Koerper", E, UMR, ohne=("-z",))
            t.box(min(x_a, x_a + sx * EB), max(x_a, x_a + sx * EB), min(y_a, y_a - sy * 0.04), max(y_a, y_a - sy * 0.04) + (EB if sy > 0 else 0), Z_KUFE, Z_RAND - 0.004, "Koerper", E, UMR, ohne=("-z",))
    # Seitengriffe: Halteplatte und Buegel aus drei Staeben
    for sx in (-1, 1):
        x0 = sx * BX
        t.box(min(x0, x0 + sx * EB), max(x0, x0 + sx * EB), -0.06, 0.06, 0.20, 0.25, "Koerper", E, UMR, ohne=("-x" if sx > 0 else "+x",))
        xa, xb = sorted((x0 + sx * EB, x0 + sx * 0.035))
        t.box(xa, xb, -0.055, -0.043, 0.205, 0.217, "Koerper", E, UMR)
        t.box(xa, xb, 0.043, 0.055, 0.205, 0.217, "Koerper", E, UMR)
        xc, xd = sorted((x0 + sx * 0.025, x0 + sx * 0.037))
        t.box(xc, xd, -0.055, 0.055, 0.193, 0.205, "Koerper", E, UMR)
    # Schlossblech mit Schluesselloch und Krampe
    t.box(-0.065, 0.065, -BY - EB - 0.001, -BY, 0.15, 0.272, "Koerper", E, UMR, ohne=("+y",))
    yv = -BY - EB - 0.001 - 0.0008
    t.flaeche([(-0.006, yv, 0.175), (0.006, yv, 0.175), (0.006, yv, 0.200), (-0.006, yv, 0.200)], (0, -1, 0), "Truhe_Schluesselloch", "Koerper", UMR)
    t.flaeche([(-0.011, yv, 0.200), (0.011, yv, 0.200), (0.011, yv, 0.216), (-0.011, yv, 0.216)], (0, -1, 0), "Truhe_Schluesselloch", "Koerper", UMR)
    t.box(-0.013, 0.013, -BY - 0.026, -BY - EB - 0.001, 0.243, 0.256, "Koerper", E, UMR)


def _deckel(t):
    H, E = "Truhe_Holz", "Truhe_Eisen"
    d0, d1 = -BY - 0.005, BY + 0.005
    t.box(-BX - 0.005, BX + 0.005, d0, d1, Z_RAND, Z_DECKEL, "Deckel", {"-z": "Truhe_Holz_innen", "*": H}, UMR)
    for xs in (-0.17, 0.17):
        t.box(xs - 0.018, xs + 0.018, d0 - EB, d1 + EB, Z_DECKEL, Z_DECKEL + EB, "Deckel", E, UMR, ohne=("-z",))
        t.box(xs - 0.018, xs + 0.018, d0 - EB, d0, Z_RAND + 0.004, Z_DECKEL, "Deckel", E, UMR, ohne=("+y",))
        t.box(xs - 0.018, xs + 0.018, d1, d1 + EB, Z_RAND + 0.004, Z_DECKEL, "Deckel", E, UMR, ohne=("-y",))
    # Band laengs der Deckelvorderkante und Lager des Ueberfalls
    t.box(-BX, BX, d0 - EB, d0, Z_DECKEL - 0.016, Z_DECKEL - 0.002, "Deckel", E, UMR, ohne=("+y",))
    t.box(-0.02, 0.02, d0 - 0.022, d0 - EB, Z_DECKEL - 0.014, Z_DECKEL - 0.002, "Deckel", E, UMR)


def _ueberfall(t):
    E = "Truhe_Eisen"
    y0, y1 = UEB_DREH[1] - 0.003, UEB_DREH[1] + 0.003
    t.box(-0.019, 0.019, y0, y1, 0.232, UEB_DREH[2] + 0.004, "Ueberfall", E, UMR)
    # Oese um die Krampe (dunkles Loch vorn) und Knauf zum Anfassen unten
    t.flaeche([(-0.007, y0 - 0.0008, 0.243), (0.007, y0 - 0.0008, 0.243), (0.007, y0 - 0.0008, 0.256), (-0.007, y0 - 0.0008, 0.256)], (0, -1, 0), "Truhe_Schluesselloch", "Ueberfall", UMR)
    t.box(-0.008, 0.008, y0 - 0.012, y0, 0.228, 0.240, "Ueberfall", E, UMR)


def _stapel(t, x, y, h, mat, n=6, r=0.0125):
    t.dreh([(r, 0.0), (r, h, "h"), (0.0, h)], n, None, mat, UMR_KLEIN, mitte=(x, y, Z_BODEN - 0.001), k_uv=40.0)


def _haufen(t, hoehe, rand, mitte_x=0.0):
    """Ausgeschuetteter Muenzhaufen als Hoehenfeld zwischen den Innenwaenden (7 x 5 Felder)."""
    nx, ny = 7, 5
    xa, xb = -BX + WAND, BX - WAND
    ya, yb = -BY + WAND, BY - WAND
    vs = {}

    def h(u, v):
        x = xa + (xb - xa) * u
        bx = math.cos(math.pi * (x - mitte_x) / (2 * 0.30)) if abs(x - mitte_x) < 0.30 else 0.0
        by = math.cos(math.pi * (v - 0.5))
        welle = 0.06 * math.sin(11.0 * u + 3.0 * v) * math.sin(7.0 * v + 1.3)
        return Z_BODEN + 0.0015 + hoehe * max(0.0, rand + (1 - rand) * max(0.0, bx * by) + welle * (1 if 0 < u < 1 and 0 < v < 1 else 0))
    for i in range(nx + 1):
        for j in range(ny + 1):
            u, v = i / nx, j / ny
            vs[i, j] = t.v((xa + (xb - xa) * u, ya + (yb - ya) * v, h(u, v)), None, UMR_KLEIN)
    k = 3.6
    for i in range(nx):
        for j in range(ny):
            q = [vs[i, j], vs[i + 1, j], vs[i + 1, j + 1], vs[i, j + 1]]
            t.f(q, [(p.co.x * k, p.co.y * k) for p in q], "Truhe_Muenzen", (0, 0, 1))


def bauen(G):
    haupt = G.Teil(HAUPT)
    _kasten(haupt)
    _deckel(haupt)
    _ueberfall(haupt)
    netze = [G.Netz(haupt)]
    # Fuellstufen: je ein Muenzhaufen mit Stapeln und Beuteln; nur eine Stufe ist sichtbar (leer = keine)
    stufen = {
        "viertel": dict(hoehe=0.045, rand=0.0, mx=-0.10, stapel=[(0.13, -0.08, 0.030, "Truhe_Silber"), (0.17, 0.05, 0.022, "Truhe_Gold"), (0.10, 0.09, 0.040, "Truhe_Silber"), (0.20, -0.10, 0.016, "Truhe_Gold")],
                        beutel=[(0.18, -0.02, 0.9)]),
        "halb": dict(hoehe=0.10, rand=0.10, mx=-0.06, stapel=[(0.15, -0.10, 0.045, "Truhe_Silber"), (0.20, 0.06, 0.036, "Truhe_Gold"), (0.12, 0.10, 0.052, "Truhe_Silber"), (0.22, -0.06, 0.028, "Truhe_Gold"),
                                                              (0.08, -0.11, 0.030, "Truhe_Gold")], beutel=[(0.19, 0.0, 1.0), (-0.20, 0.10, 0.85)]),
        "voll": dict(hoehe=0.19, rand=0.45, mx=0.0, stapel=[(0.20, -0.10, 0.13, "Truhe_Gold"), (0.23, 0.08, 0.12, "Truhe_Silber"), (-0.22, -0.09, 0.12, "Truhe_Silber"), (0.16, 0.11, 0.11, "Truhe_Gold"),
                                                            (-0.17, 0.11, 0.10, "Truhe_Gold")], beutel=[(-0.06, 0.07, 1.05), (0.08, -0.07, 0.95)]),
    }
    for nm, s in stufen.items():
        # der Haufen liegt zwischen den Innenwaenden und braucht keinen Umriss (die Waende verdecken seinen Rand)
        th = G.Teil("Muenzhaufen_" + nm)
        _haufen(th, s["hoehe"], s["rand"], s["mx"])
        netze.append(G.Netz(th, haut=False, umriss=False, eltern="Fuellstufe_" + nm))
        t = G.Teil("Muenzen_" + nm)
        for x, y, h, mat in s["stapel"]:
            _stapel(t, x, y, h, mat)
        for x, y, sk in s["beutel"]:
            _beutel_auf(t, x, y, sk, s["hoehe"] * 0.75 if nm == "voll" else 0.0)
        netze.append(G.Netz(t, haut=False, umriss=True, eltern="Fuellstufe_" + nm))
    return netze


def _beutel_auf(t, x, y, s, dz):
    p = [(0.0, 0.0), (0.040 * s, 0.003 * s), (0.052 * s, 0.026 * s), (0.044 * s, 0.050 * s), (0.016 * s, 0.066 * s, "h"), (0.013 * s, 0.072 * s, "h"),
         (0.024 * s, 0.084 * s), (0.0, 0.088 * s)]
    mats = ["Truhe_Leder", "Truhe_Leder", "Truhe_Leder", "Truhe_Leder", "Truhe_Schnur", "Truhe_Leder", "Truhe_Leder"]
    t.dreh(p, 7, None, mats, UMR_KLEIN, mitte=(x, y, Z_BODEN - 0.002 + dz), k_uv=14.0)


def _D(G, deckel_grad, ueb_rel_grad):
    a = -deckel_grad
    Dd = G.um_punkt(SCHARNIER, G.RX(a))
    Du = Dd @ G.um_punkt(UEB_DREH, G.RX(-ueb_rel_grad))
    return {"Koerper": G.Matrix.Identity(4), "Deckel": Dd, "Ueberfall": Du}


def _kurven(G):
    auf = G.verlauf([(0, 0.0), (3, 4.0), (5, 2.5), (14, 112.0), (19, 98.0), (24, OFFEN_GRAD + 3.0), (28, OFFEN_GRAD - 1.0), (31, OFFEN_GRAD)])
    ueb_auf = G.verlauf([(0, 0.0), (5, 0.0), (12, 32.0), (18, 4.0), (24, 14.0), (31, 8.0)])
    zu_x = G.fallend(0, 18, OFFEN_GRAD, 0.0)
    ueb_zu = G.verlauf([(0, 8.0), (12, 0.0), (21, 0.0)])
    return auf, ueb_auf, zu_x, ueb_zu


def clips():
    import gemeinsam as G
    auf, ueb_auf, zu_x, ueb_zu = _kurven(G)
    return [
        ("zu", 1, lambda f: _D(G, 0.0, 0.0)),
        ("offen", 1, lambda f: _D(G, OFFEN_GRAD, 8.0)),
        ("aufspringen", 31, lambda f: _D(G, auf(f), ueb_auf(f))),
        ("zuklappen", 21, lambda f: _D(G, max(0.0, zu_x(f)) if f <= 18 else 0.0, ueb_zu(f))),
    ]


def messen(ctx, mess):
    import gemeinsam as G
    auf, ueb_auf, zu_x, ueb_zu = _kurven(G)
    zu_l = [zu_x(f) if f <= 18 else 0.0 for f in range(22)]
    mess["zuklappen_monoton"] = all(zu_l[i] >= zu_l[i + 1] - 1e-9 for i in range(len(zu_l) - 1))
    auf_l = [auf(f) for f in range(32)]
    mess["aufspringen_groesster_winkel_grad"] = round(max(auf_l), 2)
    mess["offen_winkel_grad"] = OFFEN_GRAD
    # Ueberfall darf nie in den Deckel drehen (relativer Winkel >= 0 = nach aussen)
    mess["ueberfall_nie_in_den_deckel"] = all(ueb_auf(f) >= -1e-9 for f in range(32)) and all(ueb_zu(f) >= -1e-9 for f in range(22))
    mess["zeiten_s"] = {
        "aufspringen_dauer": round(31 / G.FPS, 3), "zuklappen_dauer": round(21 / G.FPS, 3),
        "aufspringen_schloss_springt": round(3 / G.FPS, 3), "aufspringen_erster_anschlag": round(14 / G.FPS, 3),
        "zuklappen_deckel_liegt_auf": round(18 / G.FPS, 3),
    }
    mess["fuellstufen"] = ["leer", "viertel", "halb", "voll"]
    mess["masse"] = {"kasten_b_t_h": [2 * BX, 2 * BY, Z_RAND], "hoehe_zu": Z_DECKEL + EB, "innen_b_t": [round(2 * (BX - WAND), 3), round(2 * (BY - WAND), 3)],
                     "innenboden_z": Z_BODEN, "scharnier": list(SCHARNIER), "umriss": UMR, "umriss_muenzen": UMR_KLEIN}
    # Muenzen bleiben unter dem geschlossenen Deckel (hoechster Punkt jeder Stufe)
    hoechst = {}
    for ob, gew, flach, haut, ist_um in ctx["objekte"]:
        if ob.name.startswith("Muenz") and not ist_um:
            hoechst[ob.name] = round(float(flach[:, 2].max()), 4)
    mess["muenzen_hoechster_punkt_m"] = hoechst
