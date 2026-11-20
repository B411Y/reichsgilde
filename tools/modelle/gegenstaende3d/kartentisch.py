"""Kartentisch mit Pergamentkarte (Wohnhaus, Koeln um 1400): schwerer Bocktisch aus Eiche (Platte auf zwei ausgesaegten Wangen, durchgesteckter
Spannriegel mit Keilen), darauf eine Pergamentkarte, die linke Kante unter zwei Bleigewichten, dazu Tintenhorn und Stechzirkel.
Die Karte ist ein Streifen aus starren Linien quer zur Rollrichtung (Knochen Karte_00 .. Karte_NN, je Linie einer, Gewichte 1,0); jede Linie dreht nur
um die Y-Achse (Rollachse). Rast-Pose = zu (aufgerollt). Clips: zu, offen, entrollen (rollt ab und glaettet sich mit Nachfedern), zusammenrollen.
Inhaltsflaeche: Kartenflaeche (Streifen ueber denselben Linien, UV 0..1 ueber die ganze Karte, 0,8 mm ueber der Oberseite), Material Karte_Inhalt (Alpha 0)."""
import math

import texturen as tx

NAME = "kartentisch"
GERUEST = "Kartentisch_Geruest"
HAUPT = "Kartentisch"

Z_T = 0.78                    # Oberkante der Tischplatte
PX, PY = 0.60, 0.40           # halbe Plattenmasse
PD = 0.045                    # Plattendicke
L = 0.70                      # Kartenlaenge (Rollrichtung X)
HW = 0.25                     # halbe Kartentiefe (Y)
DICKE = 0.0012                # Pergament
Z_M = Z_T + 0.0002 + DICKE / 2    # Mittelebene der flachen Karte
X_L = -0.36                   # linke Kartenkante (liegt unter den Gewichten)
R0 = 0.012                    # innerster Rollhalbmesser
STEIG = 0.004                 # Abstand der Lagen in der Rolle
U0 = 0.085                    # so viel bleibt im Zustand zu flach unter den Gewichten
C_END = 0.06                  # Rest, der sich am Ende des Abrollens noch kraeuselt und dann glatt legt
RAND = 0.015                  # Rand der Kartenflaeche zur Pergamentkante
DPHI = 0.70                   # groesster Winkel je Abschnitt in der Rolle (rad)
UMR = 0.007                   # Umriss des Tisches (gut 0,5 % der Breite: der Tisch ist gross)
UMR_KARTE = 0.0015            # Umriss der Karte (dünn, sonst dicker Rand um das Pergament)
UMR_KLEIN = 0.002


def _linien():
    s = [0.0, RAND, 0.035, U0 * 0.6, U0]
    while s[-1] < L - 1e-9:
        r = math.sqrt(R0 * R0 + STEIG * (L - s[-1]) / math.pi)
        s.append(min(L, s[-1] + min(0.03, DPHI * r)))
    s = sorted(set(round(x, 6) for x in s + [L - RAND]))
    return s


LINIEN = _linien()
KARTE = [f"Karte_{i:02d}" for i in range(len(LINIEN))]
KNOCHEN = [("Tisch", (0.0, 0.0, 0.0), None)] + [(nm, (X_L + s, 0.0, Z_M), "y") for nm, s in zip(KARTE, LINIEN)]
R_MAX = math.sqrt(R0 * R0 + STEIG * (L - U0) / math.pi)
U_END = L - C_END
KAPPA0 = 1.0 / math.sqrt(R0 * R0 + STEIG * C_END / math.pi)


def materialien(G, aus):
    G.SKALA.update({"Tisch_Holz": 2.2, "Karte_Pergament": 2.0, "Tisch_Blei": 20.0, "Tisch_Horn": 30.0})
    m = {}
    m["Tisch_Holz"] = G.mat_bild("Tisch_Holz", G.bild_laden(aus, "kartentisch_holz", tx.holz(7501, (0.55, 0.40, 0.25), ringe=26, kontrast=0.28, aeste=3, fugen=3)))
    m["Karte_Pergament"] = G.mat_bild("Karte_Pergament", G.bild_laden(aus, "kartentisch_pergament", tx.pergament(7502)), rau=0.9)
    m["Karte_Inhalt"] = G.mat_durchsichtig("Karte_Inhalt")
    m["Tisch_Blei"] = G.mat_flaeche("Tisch_Blei", (0.33, 0.34, 0.36), rau=0.55)
    m["Tisch_Horn"] = G.mat_flaeche("Tisch_Horn", (0.30, 0.22, 0.13), rau=0.6)
    m["Tisch_Tinte"] = G.mat_flaeche("Tisch_Tinte", (0.03, 0.025, 0.03), rau=0.3)
    m["Tisch_Eisen"] = G.mat_flaeche("Tisch_Eisen", (0.24, 0.24, 0.25), rau=0.5)
    return m


def _tisch(t):
    H = "Tisch_Holz"
    t.box(-PX, PX, -PY, PY, Z_T - PD, Z_T, "Tisch", H, UMR)
    # Wangen (ausgesaegt, 5 cm stark), Profil in (y, z)
    prof = [(-0.30, 0.0), (0.30, 0.0), (0.30, 0.05), (0.13, 0.085), (0.095, 0.30), (0.095, 0.60), (0.22, 0.69), (0.22, Z_T - PD),
            (-0.22, Z_T - PD), (-0.22, 0.69), (-0.095, 0.60), (-0.095, 0.30), (-0.13, 0.085), (-0.30, 0.05)]
    for sx in (-1, 1):
        xa = sx * 0.44
        t.prisma(prof, xa - 0.025, xa + 0.025, "x", "Tisch", H, UMR)
    # Spannriegel, durch die Wangen gesteckt, aussen mit Keilen
    t.box(-0.53, 0.53, -0.028, 0.028, 0.22, 0.29, "Tisch", H, UMR)
    for sx in (-1, 1):
        x0 = sx * 0.495
        t.box(x0 - 0.012, x0 + 0.012, -0.040, 0.040, 0.20, 0.31, "Tisch", H, UMR, M=None)


def _beiwerk(t):
    zt = Z_T + 0.0002 + DICKE
    # zwei Bleigewichte auf der linken Kante
    for y in (-0.205, 0.205):
        t.box(X_L + 0.005, X_L + 0.045, y - 0.022, y + 0.022, zt, zt + 0.024, "Tisch", "Tisch_Blei", UMR_KLEIN, ohne=("-z",))
        t.box(X_L + 0.017, X_L + 0.033, y - 0.006, y + 0.006, zt + 0.024, zt + 0.036, "Tisch", "Tisch_Blei", UMR_KLEIN, ohne=("-z",))
    # Tintenhorn (rechts hinten) und Stechzirkel (rechts vorn), stehen auf der Platte neben der Karte
    t.dreh([(0.026, 0.0), (0.030, 0.040), (0.022, 0.075, "h"), (0.017, 0.072), (0.0, 0.066)], 8, "Tisch", ["Tisch_Horn", "Tisch_Horn", "Tisch_Horn", "Tisch_Tinte"],
           UMR_KLEIN, mitte=(0.47, 0.25, Z_T), k_uv=30.0)
    import gemeinsam as G
    for w in (-9.0, 9.0):
        M = G.T(0.47, -0.22, Z_T) @ G.RZ(25.0 + w)
        t.box(-0.004, 0.004, -0.085, 0.0, 0.0, 0.004, "Tisch", "Tisch_Eisen", UMR_KLEIN, ohne=("-z",), M=M)
    t.dreh([(0.007, 0.0), (0.007, 0.007, "h"), (0.0, 0.007)], 6, "Tisch", "Tisch_Eisen", UMR_KLEIN, mitte=(0.47, -0.22, Z_T), k_uv=30.0)


def _karte(t, inhalt=False):
    """Pergament (Ober-, Unterseite, Kanten) bzw. Kartenflaeche; jede Linie ein Knochen, Punkte flach gebaut (offen)."""
    k = 2.0
    if inhalt:
        idx = [i for i, s in enumerate(LINIEN) if RAND - 1e-9 <= s <= L - RAND + 1e-9]
        z = Z_M + DICKE / 2 + 0.0008
        ya, yb = -HW + RAND, HW - RAND
        vs = [(t.v((X_L + LINIEN[i], ya, z), KARTE[i], UMR_KARTE), t.v((X_L + LINIEN[i], yb, z), KARTE[i], UMR_KARTE)) for i in idx]
        for a in range(len(idx) - 1):
            u0 = (LINIEN[idx[a]] - RAND) / (L - 2 * RAND)
            u1 = (LINIEN[idx[a + 1]] - RAND) / (L - 2 * RAND)
            q = [vs[a][0], vs[a + 1][0], vs[a + 1][1], vs[a][1]]
            t.f(q, [(u0, 0.0), (u1, 0.0), (u1, 1.0), (u0, 1.0)], "Karte_Inhalt", (0, 0, 1))
        return
    n = len(LINIEN)
    for seite, zz, nn in (("oben", Z_M + DICKE / 2, 1), ("unten", Z_M - DICKE / 2, -1)):
        vs = [(t.v((X_L + s, -HW, zz), KARTE[i], UMR_KARTE), t.v((X_L + s, HW, zz), KARTE[i], UMR_KARTE)) for i, s in enumerate(LINIEN)]
        for a in range(n - 1):
            q = [vs[a][0], vs[a + 1][0], vs[a + 1][1], vs[a][1]]
            t.f(q, [(p.co.x * k, p.co.y * k) for p in q], "Karte_Pergament", (0, 0, nn))
    for y, nn in ((-HW, -1), (HW, 1)):
        vs = [(t.v((X_L + s, y, Z_M - DICKE / 2), KARTE[i], UMR_KARTE), t.v((X_L + s, y, Z_M + DICKE / 2), KARTE[i], UMR_KARTE)) for i, s in enumerate(LINIEN)]
        for a in range(n - 1):
            q = [vs[a][0], vs[a + 1][0], vs[a + 1][1], vs[a][1]]
            t.f(q, [(p.co.x * k, p.co.z * k) for p in q], "Karte_Pergament", (0, nn, 0))
    for i, nn in ((0, -1), (n - 1, 1)):
        x = X_L + LINIEN[i]
        pts = [(x, -HW, Z_M - DICKE / 2), (x, HW, Z_M - DICKE / 2), (x, HW, Z_M + DICKE / 2), (x, -HW, Z_M + DICKE / 2)]
        q = [t.v(p, KARTE[i], UMR_KARTE) for p in pts]
        t.f(q, [(p[1] * k, p[2] * k) for p in pts], "Karte_Pergament", (nn, 0, 0))


def bauen(G):
    haupt = G.Teil(HAUPT)
    _tisch(haupt)
    _beiwerk(haupt)
    _karte(haupt)
    fl = G.Teil("Kartenflaeche")
    _karte(fl, inhalt=True)
    return [G.Netz(haupt), G.Netz(fl, umriss=False)]


# ------------------------------------------------------------------ Lage jeder Linie
def _linie(s, U, kappa=None):
    """Mittelpunkt (x, z) und Neigung theta (rad, um Y) der Linie bei Bogenlaenge s. U = flach abgerollte Laenge;
    kappa None: der Rest liegt in der Rolle (archimedische Spirale), sonst ein Kreisbogen mit der Kruemmung kappa (Glaetten)."""
    if s <= U + 1e-12:
        return X_L + s, Z_M, 0.0
    l = s - U
    if kappa is not None:
        if kappa < 1e-6:
            return X_L + s, Z_M, 0.0
        w = kappa * l
        return X_L + U + math.sin(w) / kappa, Z_M + (1 - math.cos(w)) / kappa, w
    Rc = math.sqrt(R0 * R0 + STEIG * (L - U) / math.pi)
    r = math.sqrt(max(R0 * R0 * 0.5, Rc * Rc - STEIG * l / math.pi))
    dphi = (2 * math.pi / STEIG) * (Rc - r)
    a = -math.pi / 2 + dphi
    cx, cz = X_L + U, Z_M + Rc
    return cx + r * math.cos(a), cz + r * math.sin(a), dphi


def _D(G, U, kappa=None):
    D = {"Tisch": G.Matrix.Identity(4)}
    for nm, s in zip(KARTE, LINIEN):
        x, z, th = _linie(s, U, kappa)
        D[nm] = G.T(x, 0.0, z) @ G.RY(-math.degrees(th)) @ G.T(-(X_L + s), 0.0, -Z_M)
    return D


def bind():
    import gemeinsam as G
    return _D(G, U0)


def _kurven(G):
    U_ab = G.verlauf([(0, U0), (24, U_END)])
    k_ab = G.verlauf([(24, KAPPA0), (30, 0.0), (33, 0.12 * KAPPA0), (37, 0.0)])
    k_zu = G.verlauf([(0, 0.0), (8, KAPPA0)])
    U_zu = G.verlauf([(8, U_END), (30, U0)])
    return U_ab, k_ab, k_zu, U_zu


def _zustand_entrollen(G, f):
    U_ab, k_ab, _, _ = _kurven(G)
    if f <= 24:
        return _D(G, U_ab(f))
    return _D(G, U_END, k_ab(f))


def _zustand_zusammen(G, f):
    _, _, k_zu, U_zu = _kurven(G)
    if f < 8:
        return _D(G, U_END, k_zu(f))
    return _D(G, U_zu(f))


def clips():
    import gemeinsam as G
    return [
        ("zu", 1, lambda f: _D(G, U0)),
        ("offen", 1, lambda f: _D(G, L)),
        ("entrollen", 37, lambda f: _zustand_entrollen(G, f)),
        ("zusammenrollen", 30, lambda f: _zustand_zusammen(G, f)),
    ]


def messen(ctx, mess):
    import gemeinsam as G
    import numpy as np
    objekte = {o.name: (o, gew, flach) for o, gew, flach, haut, _ in ctx["objekte"]}
    _, gk, fk = objekte[HAUPT]
    _, gi, fi = objekte["Kartenflaeche"]
    gw = np.array([str(g) for g in gk])
    karte = np.array([g.startswith("Karte_") for g in gw])
    # Karte nie unter der Tischplatte, Kartenflaeche nie unter dem Pergament (in jedem Bild jedes Clips)
    tiefste = 1e9
    for name, (bilder, fn) in ctx["clipfn"].items():
        for fr in range(bilder + 1):
            p = ctx["anwenden"](fn(fr), fk[karte], [g for g, k in zip(gk, karte) if k])
            tiefste = min(tiefste, float(p[:, 2].min()))
    mess["karte_tiefster_punkt_ueber_platte_m"] = round(tiefste - Z_T, 5)
    po = ctx["anwenden"](_D(G, L), fi, gi)
    mess["offen_kartenflaeche"] = {"min": [round(float(x), 5) for x in po.min(axis=0)], "max": [round(float(x), 5) for x in po.max(axis=0)],
                                   "hoehe_ueber_platte_m": round(float(po[:, 2].mean()) - Z_T, 5), "ebenheit_m": round(float(po[:, 2].max() - po[:, 2].min()), 6)}
    pk = ctx["anwenden"](_D(G, L), fk[karte], [g for g, k in zip(gk, karte) if k])
    mess["offen_karte_dicke_m"] = round(float(pk[:, 2].max() - pk[:, 2].min()), 6)
    pz = ctx["anwenden"](_D(G, U0), fk[karte], [g for g, k in zip(gk, karte) if k])
    mess["zu_rolle"] = {"durchmesser_m": round(float(pz[:, 2].max() - Z_M), 4), "x_von_bis": [round(float(pz[:, 0].min()), 4), round(float(pz[:, 0].max()), 4)]}
    # Lagen der Rolle duerfen sich nicht schneiden: Abstand der Mittelebenen minus Dicke, Kartenflaeche und Umriss
    mess["rolle_luft_zwischen_lagen_m"] = round(STEIG - DICKE - 0.0008, 4)
    mess["zeiten_s"] = {"entrollen_dauer": round(37 / G.FPS, 3), "entrollen_abgerollt": round(24 / G.FPS, 3), "entrollen_glatt": round(30 / G.FPS, 3),
                        "entrollen_nachfedern_hoechst": round(33 / G.FPS, 3), "zusammenrollen_dauer": round(30 / G.FPS, 3),
                        "zusammenrollen_rest_kraeuselt": round(8 / G.FPS, 3)}
    mess["kartenflaeche_sichtbar"] = "immer (die Bildseite liegt in der Rolle innen; die Flaeche ist 15 mm schmaler als das Pergament und bleibt so verdeckt)"
    mess["masse"] = {"platte_b_t": [2 * PX, 2 * PY], "platte_oberkante_m": Z_T, "karte_b_t": [L, 2 * HW], "kartenflaeche_m": [round(L - 2 * RAND, 4), round(2 * HW - 2 * RAND, 4)],
                     "pergament_dicke": DICKE, "rolle_r_innen_aussen": [R0, round(R_MAX, 4)], "linien": len(LINIEN), "umriss": UMR, "umriss_karte": UMR_KARTE}
