"""Bauteile nur der Zwergenhaeuser (Neubau, Wohnhaus Stufe 1): Schindeldach mit Dachsteinen, hoher Steinschlot, Schmiedegeraet
vor der Tuer, Kohle, Zwergenmoebel. Sie ersetzen oder ergaenzen die Teile der Menschenhaeuser (wohnhaus_zwerg_s1.py).

Alles baut auf `netzbau.Teil` auf (Flaechen mit Trim-Sheet-UV); die Baender sind dieselben wie bei den Menschen (trimsheet_zwerg.py
fuellt sie anders: "stroh" = Schindeln, "putz" = Quader). Vorbild: das Kunstwerk „Wohnhaus Zwerg“ (Stufe 1).
"""
import math

from mathutils import Vector

import netzbau
from netzbau import SCHWARZ, Teil
from trimsheet import BAENDER_AUSSEN, BAENDER_INNEN, FELDER_INNEN

UEBER = 0.38                  # Dachueberstand an der Traufe (wie bei den Menschen)
LETZTES_DACH = None           # das zuletzt gebaute Schindeldach (Teil „Dach“)


def feld(name):
    i = FELDER_INNEN.index(name); n = len(FELDER_INNEN)
    return ((i + 0.15) / n, (i + 0.85) / n)


class DachMass:
    """Was das Dach von der Grundform wissen muss (Baurahmen wie in den Menschenskripten)."""

    def __init__(self, nr, OX, DV, DH, Z_FIRST, MITTE, dach_z, neigung, walm=None, X_WALM=0.0, Z_WALM=0.0, D_WALM=0.0):
        self.nr, self.OX, self.DV, self.DH, self.Z_FIRST, self.M, self.dach_z, self.neigung = nr, OX, DV, DH, Z_FIRST, MITTE, dach_z, neigung
        self.walm, self.X_WALM, self.Z_WALM, self.D_WALM = walm, X_WALM, Z_WALM, D_WALM


def dach(g):
    """Schindeldach: sechs duenne, sich ueberdeckende Reihen (je drei Schindelreihen im Band „stroh“), Schindelfuesse als
    Alpha-Cards, Firstkappe, Ortgangbretter, Sparrenkoepfe – und Dachsteine auf Rundhoelzern, die die Schindeln halten (Kunstwerk).
    Dieselbe Bauweise wie das Strohdach der Menschen (wohnhaus_mensch_s1.py `baue_dach`), nur duenner."""
    d = Teil("Dach", BAENDER_AUSSEN, saat=23 + g.nr)
    M, OX, DV, DH = g.M, g.OX, g.DV, g.DH
    xe = OX + UEBER
    laenge = DH - DV
    hang_laenge = xe / math.cos(math.atan(g.neigung))
    stufen = max(5, round(hang_laenge / 0.74))
    felder = max(5, round(laenge / 0.8))
    halb = felder // 2
    dick_unten, dick_oben = 0.07, 0.015
    ys = [DV + laenge * j / felder for j in range(felder + 1)]
    v0, v1, m = BAENDER_AUSSEN["stroh"]
    f0, f1, fm = BAENDER_AUSSEN["stroh_franse"]
    walm = g.walm

    def kante(x, y):
        if not walm or y > DV + 1e-6: return y
        return y + max(0.0, g.dach_z(x) - g.Z_WALM) / walm

    for seite in (-1, 1):
        def P(a, hoch, y=0.0):
            x = seite * xe * (1 - a)
            return Vector((x, kante(x, y), g.dach_z(x) + hoch))
        d.vieleck("Stroh", [P(0, 0, DV), P(0, 0, DH), P(1, 0, DH), P(1, 0, DV)], [(0, 0.02), (laenge / 4, 0.02), (laenge / 4, 0.07), (0, 0.07)], zu=(0, M.y, 0))
        if walm:
            d.vieleck("Stroh", [P(0, 0, DV), P(2 / 3, 0, DV), P(1, 0, DV)], [(0, 0.02), (0.2, 0.02), (0.3, 0.07)], zu=(0, M.y, 0))
        hang = (P(0, 0) - P(1, 0)).normalized() * 0.55 + Vector((0, 0, -0.35))
        for k in range(stufen):
            a0, a1 = k / stufen, min(1.0, (k + 1) / stufen + 0.03)
            u0 = d.zufall.random()
            wellen = [(d.zufall.uniform(-0.008, 0.008), d.zufall.uniform(-0.003, 0.003)) for _ in ys]
            unten = [P(a0 + da, dick_unten + dz, y) for y, (dz, da) in zip(ys, wellen)]
            oben = [P(a1, dick_oben, y) for y in ys]
            # Fuss der Reihe 1,5 cm hangabwaerts (0,4 % der Hanglaenge): die Stirn der Schindeln steht leicht schraeg statt senkrecht (senkrecht bekam sie in der
            # UV2 eine Insel von unter einem Bildpunkt Breite und backte zu dunkel, uv2_pruefung 12)
            fuss = [P(a0 + da - 0.004, 0.0 if k == 0 else dick_oben - 0.005, y) for y, (dz, da) in zip(ys, wellen)]
            for j in range(felder):
                ua, ub = u0 + (ys[j] - DV) / m, u0 + (ys[j + 1] - DV) / m
                d.vieleck("Stroh", [unten[j], unten[j + 1], oben[j + 1], oben[j]], [(ua, v1), (ub, v1), (ub, v0), (ua, v0)], weg_von=M)
                d.vieleck("Stroh", [fuss[j], fuss[j + 1], unten[j + 1], unten[j]], [(ua, v0 + 0.01), (ub, v0 + 0.01), (ub, v0 + 0.035), (ua, v0 + 0.035)], weg_von=M + Vector((0, 0, 1)))
            for j, aussen in ((0, -1), (felder, 1)):
                sl = (oben[j] - unten[j]).length
                d.vieleck("Stroh", [P(a0, 0, ys[j]), P(a1, 0, ys[j]), oben[j], unten[j]],
                          [(u0, v0 + 0.03), (u0 + sl / m, v0 + 0.03), (u0 + sl / m, v0 + 0.06), (u0, v0 + 0.085)], weg_von=(0, ys[j] - aussen * 9, 5))
            tief = 0.13
            for ja, jb in ((0, halb), (halb, felder)):
                ka, kb = unten[ja] + Vector((0, 0, -0.015)), unten[jb] + Vector((0, 0, -0.015))
                ua, ub = u0 + (ys[ja] - DV) / fm, u0 + (ys[jb] - DV) / fm
                d.vieleck("Strohkante", [ka, kb, kb + hang * tief, ka + hang * tief], [(ua, f0), (ub, f0), (ub, f1), (ua, f1)], weg_von=M)
        # Ortgangbretter: die Schindeln enden an einem Brett, das aussen ueberhaengt. Die Bretter beider Dachseiten treffen sich am
        # First; das der rechten Seite liegt 6 mm weiter aussen (sonst deckungsgleich mit dem linken, backt schwarz: uv2_pruefung 12)
        for y, aussen in ((DV, -1), (DH, 1)):
            oben_a = 2 / 3 if (walm and aussen < 0) else 1.0
            a, b = P(0, dick_unten, y), P(oben_a, dick_oben, y)
            vor = Vector((0, aussen * (0.02 + (0.006 if seite > 0 else 0.0)), -0.05))
            d.brett("Fachwerk", a + vor, b + vor, 0.17, (0, aussen, 0), "balken_c", tiefe=0.05)
        # Sparrenkoepfe unter der Traufe
        koepfe = max(5, round(laenge / 1.0))
        for j in range(koepfe):
            y = DV + 0.45 + j * (laenge - 0.9) / (koepfe - 1)
            x_innen, x_aussen = seite * (OX + 0.01), seite * (xe - 0.07)
            for dy in (-0.05, 0.05):
                d.vieleck("Fachwerk", [(x_innen, y + dy, g.dach_z(x_innen) - 0.13), (x_aussen, y + dy, g.dach_z(x_aussen) - 0.13), (x_aussen, y + dy, g.dach_z(x_aussen) - 0.005), (x_innen, y + dy, g.dach_z(x_innen) - 0.005)],
                          [(0.1, 0.43), (0.2, 0.43), (0.2, 0.47), (0.1, 0.47)], weg_von=(x_innen, y, 0))
            d.vieleck("Fachwerk", [(x_innen, y - 0.05, g.dach_z(x_innen) - 0.13), (x_aussen, y - 0.05, g.dach_z(x_aussen) - 0.13), (x_aussen, y + 0.05, g.dach_z(x_aussen) - 0.13), (x_innen, y + 0.05, g.dach_z(x_innen) - 0.13)],
                      [(0.3, 0.43), (0.4, 0.43), (0.4, 0.47), (0.3, 0.47)], zu=(x_innen, y, 0))
    # Firstkappe: zwei Bretter im Dachwinkel, ueber den First gelegt
    zf = g.Z_FIRST + dick_oben
    yv = DV + (g.D_WALM - 0.12 if walm else -0.03)
    yh = DH + 0.03
    for seite in (-1, 1):
        a0 = Vector((0, yv, zf + 0.075)); a1 = Vector((0, yh, zf + 0.075))
        b0 = Vector((seite * 0.30, yv, zf + 0.075 - 0.30 * g.neigung)); b1 = Vector((seite * 0.30, yh, zf + 0.075 - 0.30 * g.neigung))
        d.vieleck("Fachwerk", [b0, b1, a1, a0], [(0.1, 0.43), (0.1 + (yh - yv) / 4.0, 0.43), (0.1 + (yh - yv) / 4.0, 0.47), (0.1, 0.47)], weg_von=M)
        d.vieleck("Fachwerk", [b0 + Vector((0, 0, -0.05)), b1 + Vector((0, 0, -0.05)), b1, b0], [(0.1, 0.45), (0.5, 0.45), (0.5, 0.48), (0.1, 0.48)], weg_von=M + Vector((0, 0, 3)))
    for y, aussen in ((yv, -1), (yh, 1)):
        d.vieleck("Fachwerk", [Vector((-0.30, y, zf + 0.075 - 0.30 * g.neigung)), Vector((0.30, y, zf + 0.075 - 0.30 * g.neigung)), Vector((0, y, zf + 0.075))], [(0.1, 0.45), (0.2, 0.45), (0.15, 0.47)], weg_von=(0, y - aussen * 9, 0))
    if walm:
        xw = g.X_WALM + 0.16
        fuss_l, fuss_r = Vector((-xw, DV - 0.16, g.Z_WALM + 0.12)), Vector((xw, DV - 0.16, g.Z_WALM + 0.12))
        spitze = Vector((0, DV + g.D_WALM + 0.05, zf + 0.14))
        u0 = d.zufall.random()
        d.vieleck("Stroh", [fuss_l, fuss_r, spitze], [(u0, v1), (u0 + 2 * xw / m, v1), (u0 + xw / m, v0)], weg_von=M)
        unter = Vector((0, 0.05, -0.1))
        d.vieleck("Stroh", [fuss_l + unter, fuss_r + unter, fuss_r, fuss_l], [(u0, v0 + 0.01), (u0 + 2 * xw / m, v0 + 0.01), (u0 + 2 * xw / m, v0 + 0.035), (u0, v0 + 0.035)], weg_von=M)
        for p in (fuss_l, fuss_r):
            d.vieleck("Stroh", [p, p + unter, spitze], [(u0, v0 + 0.03), (u0 + 0.05, v0 + 0.03), (u0 + 0.3, v0 + 0.07)], weg_von=M)
        ab = Vector((0, -0.06, -0.14))
        d.vieleck("Strohkante", [fuss_l + unter, fuss_r + unter, fuss_r + unter + ab, fuss_l + unter + ab], [(0.1, f0), (0.1 + 2 * xw / fm, f0), (0.1 + 2 * xw / fm, f1), (0.1, f1)], weg_von=M)
    dachsteine(d, g, xe, laenge, dick_unten, kante, stufen=stufen, dick_oben=dick_oben)
    global LETZTES_DACH
    LETZTES_DACH = d                                              # fuer fahne_verankern (Stufe 1)
    return d


def stein(t, mitte, r, h, seiten=6, band="putz", stoff="Lehmputz"):
    """Ein Findling: flacher, grob behauener Drehkoerper (Gewicht auf dem Dach)."""
    t.dreh(stoff, mitte, [(0.0, 0.0), (r * 0.92, 0.0), (r, h * 0.45), (r * 0.72, h * 0.9), (0.0, h)], band, seiten=seiten, deckel=False, glatt=False)


STANGEN_A = (0.24, 0.52, 0.80)      # Lage der Schwerstangen am Hang (Anteil von der Traufe zum First)
STANGE = 0.11                        # Kantholz 11 x 11 cm


def dachsteine(d, g, xe, laenge, dick, kante, stufen=None, dick_oben=0.015):
    """Legschindeldach (Rueckmeldung des Nutzers 16.11.2026): je Dachseite drei lange SCHWERSTANGEN waagerecht, parallel zum First, ueber
    die ganze Dachlaenge auf den Schindeln; die Findlinge liegen in Abstaenden AUF den Stangen und beschweren sie, damit der Wind die
    Schindeln nicht abhebt. Die Stange liegt mit ihrer Breitseite auf der Schindelflaeche (5 mm eingesenkt, Normale der Dachflaeche);
    die Hoehe der Schindelflaeche am Hang: Reihe k von a0 = k/stufen (Fuss, Dicke `dick`) bis a1 (oben, Dicke dick_oben)."""
    walm_tief = (g.D_WALM + 0.5) if g.walm else 0.0
    ya, yb = g.DV + walm_tief + 0.05, g.DH - 0.05          # bis in den Ueberstand (Stirn nicht in der Ebene des Giebels)
    for seite in (-1, 1):
        n = Vector((seite * g.neigung, 0.0, 1.0)).normalized()          # Normale der Dachflaeche (nach aussen oben)
        for i, a in enumerate(STANGEN_A):
            x = seite * xe * (1 - a)
            # hoechste Stelle der Schindeln unter der Stange (gemessen an den gebauten Flaechen, alle 0,25 m): dort liegt sie auf
            hoehen = [dach_hoehe(d, x, ya + (yb - ya) * s / 24, stoffe=("Stroh",)) for s in range(25)]
            zs = max(h for h in hoehen if h is not None)
            mitte = lambda y: Vector((x, y, zs)) + n * (STANGE / 2 - 0.005)
            d.stab("Fachwerk", mitte(ya), mitte(yb), STANGE, "balken_a", oben=n, kappen=True)
            oben = n * (STANGE / 2 - 0.01)                               # Findlinge 1 cm in die Stange gesetzt (liegen auf)
            for j, f in enumerate((0.18, 0.5, 0.82)):
                y = ya + (yb - ya) * f + 0.25 * ((i + j + (seite > 0)) % 2 - 0.5)
                stein(d, mitte(y) + oben, 0.15 + 0.02 * ((i + j) % 3), 0.18, seiten=5)


def dach_hoehe(dach, x, y, stoffe=None):
    """Oberste Hoehe der Dachflaechen (Teil „Dach“, optional nur bestimmter Stoffe) ueber der Stelle (x, y); None ohne Flaeche."""
    beste = None
    for pts, uv, st, *_ in dach.flaechen:
        if stoffe and st not in stoffe: continue
        for i in range(1, len(pts) - 1):
            a, b, c = pts[0], pts[i], pts[i + 1]
            det = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)
            if abs(det) < 1e-9: continue
            u = ((x - a.x) * (c.y - a.y) - (c.x - a.x) * (y - a.y)) / det
            v = ((b.x - a.x) * (y - a.y) - (x - a.x) * (b.y - a.y)) / det
            if u >= -1e-6 and v >= -1e-6 and u + v <= 1 + 1e-6:
                z = a.z + u * (b.z - a.z) + v * (c.z - a.z)
                beste = z if beste is None else max(beste, z)
    return beste


def fahne_verankern(f, dach):
    """Der Fahnenmast steht auf dem First (Rueckmeldung des Nutzers 16.11.2026: er schwebte ueber den Schindeln): der Mast wird bis
    10 cm unter die Dachoberflaeche verlaengert und bekommt dort eine eiserne Schelle als Fuss. `f` = Teil „Fahne“ (erste Flaeche
    gehoert zum Mast, Stoff „Holz“)."""
    holz = [p for pts, uv, st, *_ in f.flaechen if st == "Holz" for p in pts]
    unten = min(p.z for p in holz)
    fuss = [p for p in holz if p.z < unten + 1e-4]
    x, y = sum(p.x for p in fuss) / len(fuss), sum(p.y for p in fuss) / len(fuss)
    dz = dach_hoehe(dach, x, y)
    if dz is None:
        raise SystemExit(f"Fahne: keine Dachflaeche unter dem Mast bei ({x:.2f}, {y:.2f})")
    if unten > dz - 0.10:
        f.stab("Holz", (x, y, dz - 0.10), (x, y, unten), 0.08, "balken_a")
    f.kasten("Eisen", (x - 0.07, y - 0.07, dz - 0.03), (x + 0.07, y + 0.07, dz + 0.09), "eisen")      # Schelle am Fuss
    print(f"FAHNE Mast von {unten:.3f} bis auf {dz - 0.10:.3f} verlaengert, Dach bei {dz:.3f}")
    return f


def schlot_aufsatz(h, k, zo):
    """Der Steinschlot der Zwergenhaeuser ragt hoeher heraus und traegt eine vorkragende Haube (Kunstwerk: grosser Steinschlot).
    `k` = (x0, x1, y0, y1) des Schlots, `zo` = Oberkante des Schlots der Menschen (er traegt dort die Platte)."""
    x0, x1, y0, y1 = k
    stein_ = "Bruchstein"
    z = zo + 0.12
    ende = zo + 0.12 + 0.80
    while z < ende - 1e-3:
        zn = min(z + 0.4, ende)
        h.kasten(stein_, (x0, y0, z), (x1, y1, zn), "bruchstein", ohne=("-z", "+z"), laengs="x")
        z = zn
    for i, (u, hz) in enumerate(((0.05, 0.16), (0.10, 0.12))):          # zwei Stufen der Haube
        z0 = ende + sum(x[1] for x in ((0.05, 0.16), (0.10, 0.12))[:i])
        h.kasten(stein_, (x0 - u, y0 - u, z0), (x1 + u, y1 + u, z0 + hz), "bruchstein", ohne=("-z",), laengs="x")
    zt = ende + 0.28
    # Rauchloch 6 mm ueber der Deckplatte (in ihrer Ebene waere es deckungsgleich mit ihr und backte schwarz)
    h.rechteck(stein_, (x0 + 0.08, y0 + 0.08, zt + 0.006), (1, 0, 0), (0, 1, 0), x1 - x0 - 0.16, y1 - y0 - 0.16, "eisen", zu=(0, 0, 99), anteil=(0.0, 0.2))
    return zt


def schmiedegeraet(h, ort, wand_n, wand_e, schlank=False, nur_amboss=False):
    """Amboss auf einem Holzklotz vor dem Haus (Kunstwerk: Amboss, Hammerstiel, Kette) mit einer Kette am Boden.
    `ort` = (x, y) Fusspunkt, `wand_e` = Richtung entlang der Wand (Horn zeigt dorthin), `wand_n` = Richtung von der Wand weg."""
    holz, eisen = "Fachwerk", "Bruchstein"
    x, y = ort
    e = Vector((wand_e[0], wand_e[1], 0)); n = Vector((wand_n[0], wand_n[1], 0))
    p = lambda a, b, z: Vector((x, y, z)) + e * a + n * b
    h.dreh(holz, (x, y, 0.0), [(0.22, 0.0), (0.24, 0.05), (0.21, 0.30), (0.24, 0.34)], "balken_c", seiten=8, deckel=True, glatt=False)
    zb = 0.34
    kante = lambda a0, a1, b0, b1, z0, z1: h.kasten(eisen, tuple(min(p(a, b, z)[i] for a, b, z in ((a0, b0, z0), (a1, b1, z1))) for i in range(3)),
                                                      tuple(max(p(a, b, z)[i] for a, b, z in ((a0, b0, z0), (a1, b1, z1))) for i in range(3)), "eisen", ohne=("-z",))
    kante(-0.17, 0.17, -0.09, 0.09, zb, zb + 0.06)                      # Fuss
    kante(-0.10, 0.10, -0.06, 0.06, zb + 0.06, zb + 0.17)               # Taille
    kante(-0.27, 0.22, -0.085, 0.085, zb + 0.17, zb + 0.27)             # Bahn
    h.stab(eisen, p(0.22, 0, zb + 0.22), p(0.50, 0, zb + 0.21), 0.09, "eisen", breite=0.09, oben=(0, 0, 1), kappen=True)   # Horn
    if nur_amboss: return
    # Hammer am Klotz lehnend: Stiel und Kopf
    h.stab(holz, p(-0.05, 0.28, 0.02), p(-0.02, 0.52, 0.72), 0.04, "balken_a", kappen=True)
    h.kasten(eisen, tuple(p(-0.10, 0.50, 0.66)), tuple(p(0.06, 0.58, 0.76)), "eisen")
    # Kette: sieben Glieder, vor dem Klotz auf dem Boden entlang der Wand zur Tuer hin
    for i in range(4 if schlank else 7):
        a = p(-0.30 - 0.115 * i, 0.34, 0.03); b = p(-0.30 - 0.115 * (i + 1) + 0.03, 0.34, 0.03)
        h.stab(eisen, a, b, 0.035, "eisen", breite=0.05 if i % 2 else 0.035, oben=(0, 0, 1), kappen=True)


def kohlensack(h, ort, drehung=0.0, hoehe=0.50, schlank=False):
    """Ein gefuellter Kohlensack (dunkel, oben zugebunden)."""
    x, y = ort
    h.dreh("Fachwerk", (x, y, 0.0), [(0.0, 0.0), (0.26, 0.0), (0.30, 0.12), (0.28, 0.28), (0.20, hoehe - 0.10), (0.09, hoehe), (0.0, hoehe + 0.03)], "balken_c", seiten=6 if schlank else 8, deckel=False, glatt=False)


# ── Innen ───────────────────────────────────────────────────────────────────────────────────────────────────────────────

Z_BODEN = 0.15


def werkzeug(IX, ya=-1.95, yb=-1.25, z=1.45):
    """Werkzeugleiste (Gegenstand: Handwerk): an der linken Wand ueber dem Tisch, auf Zwergenreichweite (1,45 m): Schmiedehammer,
    Zange und Spitzhacke (Kunstwerk: Schaufel, Spitzhacke und Hammer an der Wand). Wie bei den Menschen ein Knoten „Gegenstand_Werkzeug“."""
    c = Teil("Gegenstand_Werkzeug", BAENDER_INNEN, saat=95)
    S = "Innen"
    x = -IX
    c.kasten(S, (x, ya, z - 0.035), (x + 0.03, yb, z + 0.035), "bohlen", ohne=("-x",), laengs="y")
    ue = feld("eisen"); k0, k1, _ = BAENDER_INNEN["kleinteile"]
    eisen = lambda n: [fl[1].__setitem__(slice(None), [(ue[0], k1), (ue[1], k1), (ue[1], k0), (ue[0], k0)][:len(fl[1])]) for fl in c.flaechen[-n:]]
    # Schmiedehammer: kurzer Stiel, schwerer Kopf
    c.stab(S, (x + 0.05, ya + 0.12, z - 0.02), (x + 0.05, ya + 0.12, z - 0.40), 0.03, "balken")
    c.kasten(S, (x + 0.02, ya + 0.04, z - 0.50), (x + 0.08, ya + 0.20, z - 0.38), "kleinteile", laengs="y"); eisen(6)
    # Zange
    for d in (-1, 1):
        c.stab(S, (x + 0.045, ya + 0.36 + d * 0.035, z - 0.02), (x + 0.045, ya + 0.36 - d * 0.03, z - 0.36), 0.018, "kleinteile", u_bereich=ue)
        c.stab(S, (x + 0.045, ya + 0.36 - d * 0.03, z - 0.36), (x + 0.045, ya + 0.36 - d * 0.012, z - 0.47), 0.02, "kleinteile", u_bereich=ue)
    # Spitzhacke: Stiel senkrecht, Kopf quer mit zwei Spitzen
    c.stab(S, (x + 0.05, ya + 0.58, z - 0.02), (x + 0.05, ya + 0.58, z - 0.62), 0.03, "balken")
    c.stab(S, (x + 0.05, ya + 0.42, z - 0.08), (x + 0.05, ya + 0.70, z - 0.08), 0.03, "kleinteile", breite=0.035, u_bereich=ue)
    return c


def vorrat(IX, KAMIN, zimmertuer_frei=False, IH=2.2):
    """Vorrat-Set der Zwerge: Geraeuchertes (zwei Schinken) und Kraeuter an der Stange ueber dem Herd, eine Kohlenkiste mit Kohle,
    Hackklotz mit Scheiten hinter dem Herd (Kunstwerk: Schinken, Kraeuter, Kohlenkiste, Holzstapel)."""
    c = Teil("Vorrat_Set", BAENDER_INNEN, saat=59)
    S = "Innen"
    x0, x1, y0, y1 = KAMIN
    hy0, hy1 = y0 - 0.3, y1 + 0.3
    ys_ = hy0 - 0.45
    c.stab(S, (IX - 0.13, ys_ - 0.55, 1.80), (IX - 0.13, ys_ + 0.55, 1.80), 0.035, "balken", kappen=True)            # Stange an der Wand
    # haengend: das Profil von unten nach oben (Hoehen >= 0), sonst laeuft die UV aus dem Band „kleinteile“ hinaus (uv2_pruefung 12)
    for dy in (-0.38, 0.0):
        c.dreh(S, (IX - 0.13, ys_ + dy, 1.80 - 0.05 - 0.43), [(0.0, 0.0), (0.06, 0.03), (0.10, 0.13), (0.085, 0.28), (0.05, 0.41), (0.0, 0.43)], "kleinteile", seiten=7, deckel=False, u_bereich=feld("ton"))
    for dy, farbe in ((0.22, "kraut"), (0.40, "kraut")):
        c.dreh(S, (IX - 0.13, ys_ + dy, 1.80 - 0.03 - 0.33), [(0.015, 0.0), (0.07, 0.13), (0.04, 0.29), (0.0, 0.33)], "kleinteile", seiten=5, deckel=False, u_bereich=feld(farbe))
    # Kohlenkiste mit Kohle (offen, Kohlebrocken oben)
    kx0, kx1, ky0, ky1 = 0.84, 1.30, 0.86, 1.30
    c.kasten(S, (kx0, ky0, Z_BODEN), (kx1, ky1, Z_BODEN + 0.36), "bohlen", ohne=("+z",), laengs="x")
    c.rechteck(S, (kx0 + 0.02, ky0 + 0.02, Z_BODEN + 0.30), (1, 0, 0), (0, 1, 0), kx1 - kx0 - 0.04, ky1 - ky0 - 0.04, "kleinteile", u_bereich=feld("russ"), zu=(0, 0, 99))
    z_ = c.zufall
    for i in range(7):
        px, py = z_.uniform(kx0 + 0.07, kx1 - 0.07), z_.uniform(ky0 + 0.07, ky1 - 0.07)
        c.dreh(S, (px, py, Z_BODEN + 0.30), [(0.0, 0.0), (0.07, 0.0), (0.06, 0.07), (0.0, 0.1)], "kleinteile", seiten=5, deckel=False, u_bereich=feld("russ"), glatt=False)
    # Hackklotz mit Scheiten (zimmertuer_frei: an der Rueckwand, die rechte Wand bleibt fuer die Zimmertuer frei, wie bei den Menschen)
    if zimmertuer_frei:
        c.dreh(S, (1.05, IH - 0.25, Z_BODEN), [(0.23, 0.0), (0.22, 0.42)], "kleinteile", seiten=8, u_bereich=feld("rinde"), glatt=False)
        scheite = (((1.32, IH - 0.08, Z_BODEN + 0.05), (1.92, IH - 0.10, Z_BODEN + 0.05)), ((1.36, IH - 0.20, Z_BODEN + 0.05), (1.93, IH - 0.21, Z_BODEN + 0.05)), ((1.34, IH - 0.14, Z_BODEN + 0.14), (1.90, IH - 0.15, Z_BODEN + 0.14)))
    else:
        c.dreh(S, (1.6, hy1 + 0.42, Z_BODEN), [(0.23, 0.0), (0.22, 0.42)], "kleinteile", seiten=8, u_bereich=feld("rinde"), glatt=False)
        scheite = (((1.25, hy1 + 0.82, Z_BODEN + 0.05), (1.85, hy1 + 0.84, Z_BODEN + 0.05)), ((1.3, hy1 + 0.94, Z_BODEN + 0.05), (1.86, hy1 + 0.97, Z_BODEN + 0.05)), ((1.28, hy1 + 0.88, Z_BODEN + 0.14), (1.84, hy1 + 0.9, Z_BODEN + 0.14)))
    for a, b in scheite:
        c.stab(S, a, b, 0.1, "balken", kappen=True)
    return c


def boden_pflastern(raum, z_boden=Z_BODEN):
    """Legt den Boden des Raums als Steinpflaster in Streifen: das Band „lehmboden“ ist nur 0,60 m hoch; ein Rechteck, das es in
    einem Stueck fuellt, haette die Steine zu langen Strichen gezogen (beim Menschen ist es gestampfter Lehm und faellt nicht auf)."""
    v0, v1, m = BAENDER_INNEN["lehmboden"]
    streifen = (v1 - v0) * 2048.0 / (2048.0 / m)                   # Meter, die das Band in der Hoehe deckt
    neu, boeden = [], []
    for fl in raum.flaechen:
        pts, uvs = fl[0], fl[1]
        if len(pts) == 4 and all(abs(q.z - z_boden) < 1e-4 for q in pts) and all(v0 - 1e-4 <= uv[1] <= v1 + 1e-4 for uv in uvs): boeden.append(fl)
        else: neu.append(fl)
    raum.flaechen = neu
    for fl in boeden:
        xs = [q.x for q in fl[0]]; ys = [q.y for q in fl[0]]
        x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
        n = max(1, math.ceil((y1 - y0) / streifen))
        for i in range(n):
            ya, yb = y0 + (y1 - y0) * i / n, y0 + (y1 - y0) * (i + 1) / n
            raum.rechteck("Innen", (x0, ya, z_boden), (1, 0, 0), (0, 1, 0), x1 - x0, yb - ya, "lehmboden", zu=(0, 0, 99), anteil=(0.0, 1.0))
    return raum


def massfigur_zwerg(fusspunkte, hoehe=1.35):
    """Messfigur eines Zwergs (nur fuer die Kontrollbilder, wie die Messfigur der Menschen): Beine bis 0,49 h, Rumpf bis 0,82 h
    (breiter und gedrungener als beim Menschen), Kopf mit Bart. `fusspunkte`: Liste von (x, y, z)."""
    f = Teil("MassfigurZwerg", BAENDER_INNEN, saat=98)
    s = hoehe / 1.35
    for x, y, z in fusspunkte:
        f.kasten("MassfigurZwerg", (x - 0.14 * s, y - 0.12 * s, z), (x + 0.14 * s, y + 0.12 * s, z + 0.66 * s), "balken")
        f.kasten("MassfigurZwerg", (x - 0.22 * s, y - 0.16 * s, z + 0.66 * s), (x + 0.22 * s, y + 0.16 * s, z + 1.12 * s), "balken")
        f.dreh("MassfigurZwerg", (x, y, z + 1.14 * s), [(0.0, 0.0), (0.11 * s, 0.04 * s), (0.125 * s, 0.13 * s), (0.09 * s, 0.21 * s), (0.0, 0.21 * s)], "balken", seiten=8, deckel=False)
    return f


# ── Dunkler Quader in Lagen (Nutzer 22.11.2026: Texturen ohne Streckung) ────────────────────────────────────────────────────────────────
# Bei den Menschen ist das Band „putz“ Lehmputz: die Flaechen der Gefache, Giebel, Gauben und Erker ziehen es ueber ihre ganze Hoehe
# (netzbau.Teil.welt, `hoehe`). Bei den Zwergen ist dasselbe Band Mauerwerk aus dunklem Quader – ueber ein Geschoss von 2,8 m gezogen
# standen die Steine 1,75-fach gestreckt und verwaschen. Darum teilt die Zwergenschicht jede solche Flaeche in Lagen von der Hoehe des
# Bandes (wie `steinwand` der Menschen im Erdgeschoss): jede Lage zeigt das Band in voller Hoehe, die Fugen behalten ihr Mass.
# Die Lagen haengen an festen Hoehen (Vielfache der Bandhoehe ab z = 0), damit Wand, Giebel und Nachbarwand auf einer Fuge liegen.

def _schnitt_z(pts, za, zb):
    """Schneidet das Vieleck `pts` auf den Streifen za <= z <= zb (Sutherland-Hodgman, zwei Halbebenen)."""
    for grenze, oben in ((za, False), (zb, True)):
        innen = (lambda p: p.z <= grenze + 1e-9) if oben else (lambda p: p.z >= grenze - 1e-9)
        neu = []
        for i, p in enumerate(pts):
            q = pts[(i + 1) % len(pts)]
            if innen(p): neu.append(p)
            if innen(p) != innen(q) and abs(q.z - p.z) > 1e-12:
                neu.append(p + (q - p) * ((grenze - p.z) / (q.z - p.z)))
        pts = neu
        if len(pts) < 3: return []
    aus = []                                    # doppelte Ecken weg (Ecke genau auf der Grenze)
    for p in pts:
        if not aus or (p - aus[-1]).length > 1e-6: aus.append(p)
    if len(aus) > 1 and (aus[0] - aus[-1]).length <= 1e-6: aus.pop()
    return aus if len(aus) >= 3 else []


def quader_in_lagen(stoff="Lehmputz", band="putz", ziel=None):
    """Stellt netzbau.Teil.welt fuer die Zwergenhaeuser um: Flaechen aus `stoff` auf dem Band `band` entstehen in Lagen der Bandhoehe.
    `ziel` (Stoff, Band): die Lagen entstehen gleich aus diesem Stein (Steinhaus: die Gaube aus Werkstein – versteinern() legte den
    Putz sonst gestreckt auf das Steinband). Gibt eine Funktion zurueck, die die Zahl der geteilten Flaechen nennt."""
    if getattr(Teil, "_quader_lagen", None): return Teil._quader_lagen
    alt = Teil.welt
    zahl = [0, 0]

    def welt(self, st, punkte, bd, o, eu, zbasis, hoehe, m=None, **kw):
        if st != stoff or bd != band:
            return alt(self, st, punkte, bd, o, eu, zbasis, hoehe, m, **kw)
        if ziel: st, bd = ziel
        v0, v1, bm = self.baender[bd]
        kurs = (v1 - v0) * bm
        pts = [Vector(p) for p in punkte]
        za, zb = min(p.z for p in pts), max(p.z for p in pts)
        # Sichtseite einmal fuer die ganze Flaeche bestimmen (die Teilstuecke koennten sonst einzeln kippen)
        n = (pts[1] - pts[0]).cross(pts[2] - pts[0])
        mitte = sum(pts, Vector()) / len(pts)
        drehen = False
        if kw.get("weg_von") is not None: drehen = n.dot(mitte - Vector(kw["weg_von"])) < 0
        if kw.get("zu") is not None: drehen = n.dot(Vector(kw["zu"]) - mitte) < 0
        if drehen: pts = pts[::-1]
        rest = {k: v for k, v in kw.items() if k not in ("weg_von", "zu")}
        o, eu = Vector(o), Vector(eu).normalized()
        zahl[0] += 1
        k = math.floor((za + 1e-6) / kurs)
        while k * kurs < zb - 1e-6:
            teil = _schnitt_z(pts, k * kurs, (k + 1) * kurs)
            ok = o - eu * ((k * 0.37) % 1.0) * bm            # jede Lage um ein festes Stueck versetzt (kein Fugenkreuz ueber die Lagen)
            for i in range(1, len(teil) - 1, 2):             # Vieleck als Faecher aus Vier- und Dreiecken
                stueck = [teil[0]] + teil[i:i + 3]
                if len(stueck) == 3 and (stueck[1] - stueck[0]).cross(stueck[2] - stueck[0]).length < 1e-8: continue
                alt(self, st, stueck, bd, ok, eu, k * kurs, kurs, m, **rest)
                zahl[1] += 1
            k += 1

    Teil.welt = welt
    Teil._quader_lagen = lambda: tuple(zahl)
    return Teil._quader_lagen


# ── Geschlossene Laeden im festen Zufall (Nutzer 21./22.11.2026 „immer im Zufall, mal dies, mal das“) ───────────────────────────────────
# Wie in den Zeilenmodulen (zeile_module.py): ein fensterloses Feld bekommt mit dem Anteil LADEN_OG bzw. LADEN_EG geschlossene Laeden in
# Fenstergroesse – buendig vor der Wand, ohne Oeffnung dahinter (die Innenraeume bleiben unveraendert). Gebaut wird mit derselben Funktion
# (zeile_module.laden_zu: zwei Fluegel aus Planken, Rahmen und Mittelleiste), gewuerfelt mit derselben festen Saat je Haus und Wand.
# Erst wenn die ganze Huelle steht (Anbau, Schlot, Vordach, offene Laeden), faellt weg, was verstellt waere (laeden_zu_pruefen).

LAEDEN_ZU = []                # (Teil, Marke) der gebauten geschlossenen Laeden


class _Marke(tuple):
    """Eckenfarbe Schwarz, als eigenes Objekt: daran sind die Flaechen eines Ladens auch nach dem Versetzen der Teile zu erkennen."""


def _saat(schluessel):
    import random
    return random.Random(sum(ord(c) * (i + 1) for i, c in enumerate(schluessel)))


def felder_der_lage(lage, breite, oeffnungen, t0, t1, abstand=0.25):
    """Freie Gefache einer Holzlage in Fensterhoehe (t0..t1): zwischen zwei Staendern, ohne Strebe, mit Brust- und Sturzriegel, nicht
    neben einem Fenster oder einer Tuer (dort haengen die offenen Laeden)."""
    sperren = sorted((min(q[0] for q in poly), max(q[0] for q in poly)) for poly, *_ in lage.hoelzer
                     if max(q[1] for q in poly) > t0 + 0.03 and min(q[1] for q in poly) < t1 - 0.03)
    frei, x = [], 0.0
    for a, e in sperren:
        if a > x + 1e-6: frei.append((x, a))
        x = max(x, e)
    if x < breite: frei.append((x, breite))

    def riegel(a, e, z):
        return any(min(q[0] for q in poly) <= a + 0.02 and max(q[0] for q in poly) >= e - 0.02
                   and min(q[1] for q in poly) <= z <= max(q[1] for q in poly)
                   and max(q[1] for q in poly) - min(q[1] for q in poly) < 0.30 for poly, *_ in lage.hoelzer)
    aus = []
    for a, e in frei:
        if not (0.45 <= e - a <= 1.30) or a < 0.05 or e > breite - 0.05: continue
        if any(a < o[1] + abstand and e > o[0] - abstand for o in oeffnungen): continue
        if riegel(a, e, t0 - 0.05) and riegel(a, e, t1 + 0.05): aus.append((a + 0.04, e - 0.04))
    return aus


def felder_der_mauer(achsen, bw, breite, oeffnungen, abstand=0.60, ecke=0.55):
    """Fensterlose Achsfelder einer Steinwand: nicht neben Fenster oder Tuer (Gewaende, offene Laeden), nicht an den Eckquadern."""
    aus = []
    for k in range(len(achsen) - 1):
        a, e = achsen[k] + bw / 2, achsen[k + 1] - bw / 2
        if not (0.45 <= e - a <= 1.30) or a < ecke or e > breite - ecke: continue
        if any(a < o[1] + abstand and e > o[0] - abstand for o in oeffnungen): continue
        aus.append((a, e))
    return aus


def laeden_zu(h, p0, eu, n, felder, t0, t1, schluessel, anteil, einbau=None):
    """Wuerfelt je Feld (s0, s1) der Wand (Punkt p0 bei s = 0, Richtung eu, Normale n nach aussen) und baut geschlossene Laeden von t0
    bis t1. `einbau(bau)`: fuehrt den Bau aus (Steinhaus: als Holzakzent). Gibt die Zahl der gebauten Laeden zurueck."""
    import zeile_module as Z
    wuerfel = _saat(schluessel)
    p0, eu, n = Vector(p0), Vector(eu).normalized(), Vector(n).normalized()
    o = Vector((p0.x, p0.y, 0.0)) + n * 0.02                         # die Fluegel 2 cm vor der Wand (nicht deckungsgleich mit ihr)
    zahl = 0
    for s0, s1 in felder:
        if not wuerfel.random() >= 1.0 - anteil: continue            # derselbe Wurf wie in zeile_module (fensterlose Achse)
        ebene = Z.Ebene(o, eu, (0, 0, 1), o + eu * (s0 + s1) / 2 - n + Vector((0, 0, (t0 + t1) / 2)))
        a = len(h.flaechen)
        alt = dict(Z.S)
        Z.S.clear(); Z.S.update(satz="zwerg_wohnhaus")              # Zwerge: Holz (Band „planken“), kein Anstrich in der Hausfarbe
        try:
            bau = lambda: Z.laden_zu(h, ebene, (s0, s1, t0, t1))
            einbau(bau) if einbau else bau()
        finally:
            Z.S.clear(); Z.S.update(alt)
        marke = _Marke(SCHWARZ)
        h.flaechen[a:] = [(pts, uvs, st, marke, gl) for pts, uvs, st, _f, gl in h.flaechen[a:]]
        LAEDEN_ZU.append((h, marke))
        zahl += 1
    return zahl


def laeden_zu_pruefen(teile):
    """Nimmt geschlossene Laeden wieder weg, vor denen etwas steht oder haengt (Schlot, Anbau, Schuppendach, Vordach, offener Laden,
    Scheitholz): irgendeine Flaeche der Huelle `teile` liegt mehr als 6 cm vor der Wand und ueberdeckt den Laden. Gibt (gebaut, entfernt)."""
    weg = 0
    for h, marke in LAEDEN_ZU:
        eigene = [f for f in h.flaechen if f[3] is marke]
        if not eigene: continue
        pts = eigene[0][0]                                           # erster Fluegel: liegt 2 cm vor der Wand, Normale nach aussen
        n = (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized()
        eu = Vector((0, 0, 1)).cross(n).normalized()
        alle = [p for f in eigene for p in f[0]]
        sa, se = min((p - pts[0]).dot(eu) for p in alle) + 0.01, max((p - pts[0]).dot(eu) for p in alle) - 0.01
        za, ze = min(p.z for p in alle) + 0.01, max(p.z for p in alle) - 0.01
        verstellt = False
        for t in teile:
            for f in t.flaechen:
                if f[3] is marke: continue
                d = [(p - pts[0]).dot(n) for p in f[0]]
                if max(d) < 0.045 or min(d) > 3.0: continue                       # Wand und Holzlage (bis 3 cm davor) zaehlen nicht
                s = [(p - pts[0]).dot(eu) for p in f[0]]
                if max(s) > sa and min(s) < se and max(p.z for p in f[0]) > za and min(p.z for p in f[0]) < ze:
                    verstellt = True; break
            if verstellt: break
        if verstellt:
            weg += 1
            if hasattr(h, "holz_akzente"):                           # Bereiche der Holzakzente ruecken mit (wie flaechen_entfernen)
                idx = [i for i, f in enumerate(h.flaechen) if f[3] is marke]
                h.holz_akzente = [(a - sum(1 for i in idx if i < a), e - sum(1 for i in idx if i < e)) for a, e in h.holz_akzente]
            h.flaechen = [f for f in h.flaechen if f[3] is not marke]
    zahl = len(LAEDEN_ZU)
    print(f"LAEDEN ZU: {zahl} gewuerfelt, {weg} verstellt und entfernt, {zahl - weg} gebaut")
    return zahl, weg
