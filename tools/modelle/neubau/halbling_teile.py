"""Bauteile der Halblingshaeuser (Neubau, Stufe 1): dickes Strohdach mit Strohwulst am Ortgang, Zwerchhaus, Rundbogen, Ochsenauge,
Pfettenkoepfe, Messfigur.

Vorbild ist das Kunstwerk „Wohnhaus Halbling, Stufe 1“ (wohnhaus_lvl1_halbling.jpg): ein anderthalbgeschossiges Fachwerkhaus wie das der
Menschen, aber das Strohdach ist dick und rund, es laeuft am Giebel als Wulst ueber die Kante (keine Windbretter, darunter die Koepfe
der Pfetten); an der Traufseite sitzt ein Zwerchhaus mit eigenem kleinen Strohdach und verbretterter Front; die Haustuer hat einen
Rundbogen, im Giebel ueber dem Kehlbalken ein rundes Fenster (Ochsenauge). Alles andere (Masse, Waende, Fenster, Moebel) kommt aus den
Bauskripten der Menschen (wohnhaus_halbling_s1.py).
"""
import math

from mathutils import Vector

from elb_teile import streifen

DICK_UNTEN, DICK_OBEN = 0.50, 0.22      # Dicke der Strohstufen (Menschen 0,30 / 0,15): das Kunstwerk zeigt ein dickes Dach
WULST = 0.16                            # Strohwulst am Ortgang: so weit steht er vor der Giebelkante vor
WULST_TEILE = 7                         # Abschnitte des Wulsts entlang der Dachkante


def strohdach(s1, feld=0.8, wulst_teile=None, wulst=None):
    """Strohdach der Halblinge auf den Massen der Grundform (s1.F): die drei ueberlappenden Strohstufen und der First der Menschen
    (wohnhaus_mensch_s1.baue_dach) mit dickeren Stufen; statt der Windbretter laeuft ein Strohwulst ueber die Giebelkante, darunter
    stehen die Pfettenkoepfe aus dem Giebel. Mit Walm (Grundform 4) vorn wie bei den Menschen. `feld` (Laenge eines Felds entlang des
    Firsts), `wulst_teile` und `wulst` (Vorstand des Wulsts): die Stufen 2 und 3 (wohnhaus_halbling_s23.py) sparen damit Dreiecke und
    halten den Fussabdruck; ohne Angabe die Werte der Stufe 1."""
    from netzbau import Teil
    from trimsheet import BAENDER_AUSSEN
    WULST_TEILE_ = WULST_TEILE if wulst_teile is None else wulst_teile
    WULST_ = WULST if wulst is None else wulst
    F = s1.F
    dach_z, NEIGUNG = s1.dach_z, s1.NEIGUNG
    d = Teil("Dach", BAENDER_AUSSEN, saat=23 + F.nr)
    M = F.MITTE
    OX, DV, DH = F.OX, F.DACH_VORN, F.DACH_HINTEN
    xe = OX + s1.UEBER
    laenge = DH - DV
    stufen = 3
    felder = max(5, round(laenge / feld))
    halb = felder // 2
    dick_unten, dick_oben = DICK_UNTEN, DICK_OBEN
    ys = [DV + laenge * j / felder for j in range(felder + 1)]
    v0, v1, m = BAENDER_AUSSEN["stroh"]
    f0, f1, fm = BAENDER_AUSSEN["stroh_franse"]
    walm = F.WALM
    if walm:   # Grundform 4: Am First endet der Walm hinter der ersten Feldgrenze (y -1,92 gegen -2,08); die oberste Stufe des ersten Felds
        #        faltete sich dort (ein Dreieck zeigte nach unten, von oben nur die Rueckseite: Pruefung 12b). Die Grenze liegt darum 5 cm dahinter.
        ys[1] = max(ys[1], DV + max(0.0, dach_z(0.0) - F.Z_WALM) / walm + 0.05)

    def kante(x, y):
        if not walm or y > DV + 1e-6: return y
        return y + max(0.0, dach_z(x) - F.Z_WALM) / walm

    for seite in (-1, 1):
        def P(a, hoch, y=0.0):
            x = seite * xe * (1 - a)
            return Vector((x, kante(x, y), dach_z(x) + hoch))
        d.vieleck("Stroh", [P(0, 0, DV), P(0, 0, DH), P(1, 0, DH), P(1, 0, DV)], [(0, 0.02), (laenge / 4, 0.02), (laenge / 4, 0.07), (0, 0.07)], zu=(0, M.y, 0))
        if walm:
            d.vieleck("Stroh", [P(0, 0, DV), P(2 / 3, 0, DV), P(1, 0, DV)], [(0, 0.02), (0.2, 0.02), (0.3, 0.07)], zu=(0, M.y, 0))
        hang = (P(0, 0) - P(1, 0)).normalized() * 0.75 + Vector((0, 0, -0.25))
        for k in range(stufen):
            a0, a1 = k / stufen, min(1.0, (k + 1) / stufen + 0.02)
            u0 = d.zufall.random()
            wellen = [(d.zufall.uniform(-0.03, 0.03), d.zufall.uniform(-0.008, 0.008)) for _ in ys]
            unten = [P(a0 + da, dick_unten + dz, y) for y, (dz, da) in zip(ys, wellen)]
            oben = [P(a1, dick_oben, y) for y in ys]
            # der Fuss der Stufe ist gerundet: zwei Flaechen (senkrecht hinab, dann schraeg zurueck unter die Stufe)
            mitte_fuss = [P(a0 + da - 0.012, (dick_unten + dz) * 0.45 + (0.0 if k == 0 else dick_oben * 0.5), y) for y, (dz, da) in zip(ys, wellen)]
            fuss = [P(a0 + da + 0.02, 0.0 if k == 0 else dick_oben - 0.01, y) for y, (dz, da) in zip(ys, wellen)]
            for j in range(felder):
                ua, ub = u0 + (ys[j] - DV) / m, u0 + (ys[j + 1] - DV) / m
                d.vieleck("Stroh", [unten[j], unten[j + 1], oben[j + 1], oben[j]], [(ua, v1), (ub, v1), (ub, v0), (ua, v0)], weg_von=M)
                d.vieleck("Stroh", [mitte_fuss[j], mitte_fuss[j + 1], unten[j + 1], unten[j]], [(ua, v0 + 0.01), (ub, v0 + 0.01), (ub, v0 + 0.03), (ua, v0 + 0.03)], weg_von=M + Vector((0, 0, 1)))
                d.vieleck("Stroh", [fuss[j], fuss[j + 1], mitte_fuss[j + 1], mitte_fuss[j]], [(ua, v0 + 0.03), (ub, v0 + 0.03), (ub, v0 + 0.045), (ua, v0 + 0.045)], weg_von=M + Vector((0, 0, 1)))
            for j, aussen in ((0, -1), (felder, 1)):       # Giebelseiten der Stufe
                sl = (oben[j] - unten[j]).length
                d.vieleck("Stroh", [P(a0, 0, ys[j]), P(a1, 0, ys[j]), oben[j], unten[j]],
                          [(u0, v0 + 0.03), (u0 + sl / m, v0 + 0.03), (u0 + sl / m, v0 + 0.06), (u0, v0 + 0.085)], weg_von=(0, ys[j] - aussen * 9, 5))
                # der gerundete Fuss steht vor der Seitenflaeche: sein Querschnitt schliesst die Stufe am Giebel bzw. Walm
                vor = Vector((0, aussen * 0.006, 0))               # 6 mm vor der Seitenflaeche (sonst deckungsgleich mit ihr)
                d.vieleck("Stroh", [fuss[j] + vor, mitte_fuss[j] + vor, unten[j] + vor], [(u0, v0 + 0.03), (u0 + 0.03, v0 + 0.03), (u0 + 0.03, v0 + 0.05)],
                          weg_von=(0, ys[j] - aussen * 9, 5))
            tief = 0.30 if k == 0 else 0.24
            for ja, jb in ((0, halb), (halb, felder)):     # Alpha-Cards unter der Stufenkante
                ka, kb = mitte_fuss[ja] + Vector((0, 0, -0.03)), mitte_fuss[jb] + Vector((0, 0, -0.03))
                ua, ub = u0 + (ys[ja] - DV) / fm, u0 + (ys[jb] - DV) / fm
                d.vieleck("Strohkante", [ka, kb, kb + hang * tief, ka + hang * tief], [(ua, f0), (ub, f0), (ub, f1), (ua, f1)], weg_von=M)
        # Alpha-Cards am Ortgang (unter dem Wulst; mit Walm vorn nur bis zum Fuss der Walmflaeche)
        for y, aussen in ((DV, -1), (DH, 1)):
            oben_a = 2 / 3 if (walm and aussen < 0) else 1.0
            a, b = P(0, dick_unten * 0.5, y), P(oben_a, dick_oben, y)
            ab = Vector((0, aussen * 0.05, -0.24))
            sl = (b - a).length
            d.vieleck("Strohkante", [a, b, b + ab, a + ab], [(0.2, f0), (0.2 + sl / fm, f0), (0.2 + sl / fm, f1), (0.2, f1)], weg_von=(0, y - aussen * 9, 5))
        # Sparrenkoepfe unter der Traufe
        koepfe = max(5, round(laenge / 1.0))
        for j in range(koepfe):
            y = DV + 0.45 + j * (laenge - 0.9) / (koepfe - 1)
            x_innen, x_aussen = seite * (OX + 0.01), seite * (xe - 0.07)
            for dy in (-0.05, 0.05):
                d.vieleck("Fachwerk", [(x_innen, y + dy, dach_z(x_innen) - 0.13), (x_aussen, y + dy, dach_z(x_aussen) - 0.13), (x_aussen, y + dy, dach_z(x_aussen) - 0.005), (x_innen, y + dy, dach_z(x_innen) - 0.005)],
                          [(0.1, 0.43), (0.2, 0.43), (0.2, 0.47), (0.1, 0.47)], weg_von=(x_innen, y, 0))
            d.vieleck("Fachwerk", [(x_innen, y - 0.05, dach_z(x_innen) - 0.13), (x_aussen, y - 0.05, dach_z(x_aussen) - 0.13), (x_aussen, y + 0.05, dach_z(x_aussen) - 0.13), (x_innen, y + 0.05, dach_z(x_innen) - 0.13)],
                      [(0.3, 0.43), (0.4, 0.43), (0.4, 0.47), (0.3, 0.47)], zu=(x_innen, y, 0))
    # First: Wulst aus zwei Flaechen (in Feldern, leicht durchhaengend), mit Fransen nach beiden Seiten (wie bei den Menschen, hoeher)
    zf = F.Z_FIRST + dick_oben
    yv = DV + (F.D_WALM - 0.12 if walm else -0.02)
    kamm_y = [yv] + ys[1:-1] + [DH + 0.02]
    sack = [-0.055 * math.sin(math.pi * j / felder) for j in range(felder + 1)]
    for seite in (-1, 1):
        kamm = [Vector((0, y, zf + 0.22 + sack[j])) for j, y in enumerate(kamm_y)]
        rand = [Vector((seite * 0.46, k.y, zf - 0.46 * NEIGUNG + 0.17 + sack[j])) for j, k in enumerate(kamm)]
        for j in range(felder):
            ua, ub = 0.5 + (kamm_y[j] - DV) / m, 0.5 + (kamm_y[j + 1] - DV) / m
            d.vieleck("Stroh", [rand[j], rand[j + 1], kamm[j + 1], kamm[j]], [(ua, v0 + 0.06), (ub, v0 + 0.06), (ub, v0 + 0.11), (ua, v0 + 0.11)], weg_von=M)
        ab = Vector((seite * 0.10, 0, -0.17))
        d.vieleck("Strohkante", [rand[0], rand[-1], rand[-1] + ab, rand[0] + ab], [(0.6, f0), (0.6 + laenge / fm, f0), (0.6 + laenge / fm, f1), (0.6, f1)], weg_von=M)
        for j, aussen in ((0, -1), (felder, 1)):
            mitte_unten = Vector((0, kamm[j].y, zf - 0.05))
            if walm and aussen < 0:
                d.vieleck("Stroh", [rand[j], kamm[j], mitte_unten], [(0.1, v0 + 0.03), (0.2, v0 + 0.03), (0.15, v0 + 0.06)], weg_von=(0, kamm[j].y - aussen * 9, 5))
                continue
            fuss = Vector((seite * 0.46, kamm[j].y, dach_z(seite * 0.46) + 0.12))
            d.vieleck("Stroh", [fuss, rand[j], kamm[j], mitte_unten], [(0.1, v0 + 0.02), (0.1, v0 + 0.03), (0.2, v0 + 0.03), (0.15, v0 + 0.06)],
                      weg_von=(0, kamm[j].y - aussen * 9, 5))
    if walm:
        # Walmflaeche wie bei den Menschen (wohnhaus_mensch_s1.baue_dach): eine Strohstufe vom First hinab bis zur Giebelkante
        xw = F.X_WALM + 0.16
        fuss_l, fuss_r = Vector((-xw, DV - 0.16, F.Z_WALM + 0.12)), Vector((xw, DV - 0.16, F.Z_WALM + 0.12))
        spitze = Vector((0, DV + F.D_WALM - 0.10, zf + 0.02))   # unter der Firstkappe (sonst deckt der Walm sie zu)
        u0 = d.zufall.random()
        d.vieleck("Stroh", [fuss_l, fuss_r, spitze], [(u0, v1), (u0 + 2 * xw / m, v1), (u0 + xw / m, v0)], weg_von=M)
        unter = Vector((0, 0.05, -0.30))
        d.vieleck("Stroh", [fuss_l + unter, fuss_r + unter, fuss_r, fuss_l], [(u0, v0 + 0.01), (u0 + 2 * xw / m, v0 + 0.01), (u0 + 2 * xw / m, v0 + 0.035), (u0, v0 + 0.035)], weg_von=M)
        for p in (fuss_l, fuss_r):
            d.vieleck("Stroh", [p, p + unter, spitze], [(u0, v0 + 0.03), (u0 + 0.05, v0 + 0.03), (u0 + 0.3, v0 + 0.07)], weg_von=M)
        ab = Vector((0, -0.06, -0.24))
        d.vieleck("Strohkante", [fuss_l + unter, fuss_r + unter, fuss_r + unter + ab, fuss_l + unter + ab], [(0.1, f0), (0.1 + 2 * xw / fm, f0), (0.1 + 2 * xw / fm, f1), (0.1, f1)], weg_von=M)
    # Strohwulst ueber der Giebelkante (statt der Windbretter; nicht am Walm): je Dachseite ein Wulst von der Traufe bis zum First
    for y, aussen in (((DH, 1),) if walm else ((DV, -1), (DH, 1))):
        for seite in (-1, 1):
            ringe, achsen = [], []
            for i in range(WULST_TEILE_ + 1):
                t = i / WULST_TEILE_
                x = seite * (xe - 0.05) * (1 - t) + seite * 0.20 * t
                z = dach_z(x)
                ringe.append([Vector((x, y - aussen * 0.12, z + dick_unten + 0.04)),                     # oben auf dem Stroh
                              Vector((x, y + aussen * WULST_ * 0.70, z + dick_unten * 0.80)),
                              Vector((x, y + aussen * WULST_, z + dick_unten * 0.35)),                  # aussen
                              Vector((x, y + aussen * WULST_ * 0.55, z - 0.06)),
                              Vector((x, y - aussen * 0.04, z - 0.10))])                               # unten, unter der Giebelkante
                achsen.append(Vector((x, y + aussen * WULST_ * 0.15, z + dick_unten * 0.38)))          # Mitte des Querschnitts
            u0 = d.zufall.random()
            for i in range(WULST_TEILE_):
                ua, ub = u0 + i * 0.35 / m * 4, u0 + (i + 1) * 0.35 / m * 4
                for r in range(4):
                    va, vb = v0 + 0.02 + r * 0.03, v0 + 0.05 + r * 0.03
                    d.vieleck("Stroh", [ringe[i][r], ringe[i + 1][r], ringe[i + 1][r + 1], ringe[i][r + 1]], [(ua, va), (ub, va), (ub, vb), (ua, vb)],
                              weg_von=(achsen[i] + achsen[i + 1]) / 2, glatt=True)
            # Abschluss an der Traufe (Stirn des Wulsts)
            r0 = ringe[0]
            mi = sum(r0, Vector()) / len(r0)
            for r in range(4):
                d.vieleck("Stroh", [r0[r], r0[r + 1], mi], [(u0, v0 + 0.02), (u0 + 0.05, v0 + 0.02), (u0 + 0.025, v0 + 0.05)], weg_von=mi + Vector((seite, 0, 0)))
    return d


def pfettenkoepfe(teil, s1, y_wand, aussen, ueberstand):
    """Koepfe der Pfetten unter dem Ortgang (Kunstwerk: Balkenkoepfe unter dem Strohwulst am Giebel): First-, zwei Mittelpfetten,
    12 x 12 cm, sie stehen `ueberstand` aus der Giebelwand (bei y_wand, aussen = -1 vorn, +1 hinten)."""
    F = s1.F
    for x in (-F.OX * 0.55, 0.0, F.OX * 0.55):
        zo = s1.dach_z(x) - 0.012 - (0.10 if x == 0 else 0.0)
        y0, y1 = sorted((y_wand, y_wand + aussen * ueberstand))
        teil.kasten("Fachwerk", (x - 0.06, y0, zo - 0.13), (x + 0.06, y1, zo), "balken_c", ohne=("-y",) if aussen > 0 else ("+y",), laengs="y")


def rundbogen(teil, P, n, s0, s1_, oben, vor=0.01, stuecke=None):
    """Rundbogen NUR AUSSEN in einer rechteckigen Oeffnung (Kunstwerk: Haustuer): ein Halbkreis von Laibung zu Laibung, Scheitel an
    der Oberkante; die Zwickel zwischen Bogen und oberen Ecken mit Putz geschlossen, darauf ein Bogenbrett (wie elb_teile.spitzbogen).
    Die Oeffnung der Huelle bleibt rechteckig, innen aendert sich nichts. P(s, z): Punkt auf der Aussenflaeche der Wand. `stuecke`:
    Abschnitte je Viertelkreis (ohne Angabe die der Stufe 1, elb_teile.BOGEN_STUECKE)."""
    from elb_teile import BOGEN_RAND, BOGEN_STUECKE
    n = Vector(n).normalized()
    r = (s1_ - s0) / 2
    sm, zm = s0 + r, oben - r
    stuecke = stuecke or BOGEN_STUECKE
    links = [(sm + r * math.cos(math.pi - math.pi / 2 * k / stuecke), zm + r * math.sin(math.pi - math.pi / 2 * k / stuecke)) for k in range(stuecke + 1)]
    rechts = [(2 * sm - s, z) for s, z in links]
    Q = lambda s, z, dv=0.0: Vector(P(s, z)) + n * (vor + dv)
    hinten = Q(sm, oben) - n
    for seite, ecke in ((links, (s0, oben)), (rechts, (s1_, oben))):
        for (sa, za), (sb, zb) in zip(seite[:-1], seite[1:]):
            teil.welt("Lehmputz", [Q(*ecke), Q(sa, za), Q(sb, zb)], "putz", Q(s0, 0.0), (Q(s1_, 0.0) - Q(s0, 0.0)).normalized(), 0.0, 2.2, weg_von=hinten)
    bogen = links + rechts[::-1][1:]
    streifen(teil, "Fachwerk", [Q(s, z) for s, z in bogen], [BOGEN_RAND] * len(bogen), n, "balken_c", 0.047, kappe=False)


def ochsenauge(teil, P, n, sm, zm, r=0.24, stuecke=12):
    """Rundes Giebelfenster (Ochsenauge) ueber dem Kehlbalken, nur aussen: Rahmen als Ring, dunkle Fuellung (verbrettert, keine
    Scheibe: dahinter liegt der Spitzboden ohne Oeffnung innen), Sprossenkreuz. P(s, z): Punkt auf der Aussenflaeche der Wand."""
    n = Vector(n).normalized()
    Q = lambda s, z, dv=0.0: Vector(P(s, z)) + n * dv
    ring = [(sm + r * math.cos(2 * math.pi * k / stuecke), zm + r * math.sin(2 * math.pi * k / stuecke)) for k in range(stuecke + 1)]
    mitte = Q(sm, zm, 0.012)
    hinten = mitte - n
    for (sa, za), (sb, zb) in zip(ring[:-1], ring[1:]):           # Fuellung: dunkles Brett (Band balken_c, im Schatten)
        teil.vieleck("Fachwerk", [mitte, Q(sa, za, 0.012), Q(sb, zb, 0.012)], [(0.3, 0.42), (0.35, 0.42), (0.35, 0.44)], weg_von=hinten)
    streifen(teil, "Fachwerk", [Q(s, z) for s, z in ring], [0.08] * len(ring), n, "balken_b", 0.05, kappe=False)
    for a, b in (((sm - r, zm), (sm + r, zm)), ((sm, zm - r), (sm, zm + r))):
        teil.brett("Fachwerk", Q(*a, 0.012), Q(*b, 0.012), 0.035, n, "balken_c", tiefe=0.03)


ZWERCH_VOR = 0.16          # die Front des Zwerchhauses steht so weit vor der Wand (ueber der Traufe)
ZWERCH_HOCH = 1.10         # Hoehe der Front bis zur Traufe des Zwerchdachs
ZWERCH_NEIGUNG = math.tan(math.radians(52))
ZWERCH_UEBER = 0.14        # Ueberstand des Zwerchdachs vorn
ZWERCH_STROH = 0.30        # Dicke des Zwerchdachs
ZWERCH_EINBAU = 0.15       # die Waende des Zwerchhauses stehen so tief im Stroh des Hauptdachs (ueber dessen Unterseite)


def zwerch_masse(s1, seite, yc, w):
    """Masse des Zwerchhauses an der Traufseite `seite` (-1 links, +1 rechts) um y = yc, halbe Breite w (Baurahmen)."""
    F = s1.F
    N = s1.NEIGUNG
    xf = seite * (F.BX + ZWERCH_VOR)
    z0 = s1.dach_z(xf) + ZWERCH_EINBAU
    ze = z0 + ZWERCH_HOCH
    zr = ze + w * ZWERCH_NEIGUNG
    x_schnitt = lambda z: seite * (F.Z_FIRST + 0.10 - z) / N      # dort liegt eine Hoehe z 0,10 m ueber der Unterseite des Hauptdachs
    return dict(xf=xf, z0=z0, ze=ze, zr=zr, x_schnitt=x_schnitt, seite=seite, yc=yc, w=w, z_first=F.Z_FIRST, neigung=N)


def zwerchhaus_waende(h, s1, zm):
    """Front (verbrettert, mit Rahmen und Bruestungsriegel wie im Kunstwerk) und Seitenwaende (Putz) des Zwerchhauses. Die Seiten stehen
    mit der Unterkante ZWERCH_EINBAU ueber der Unterseite des Hauptdachs (im Stroh), innen sieht man nichts davon."""
    F = s1.F
    xf, z0, ze, zr, yc, w, s = zm["xf"], zm["z0"], zm["ze"], zm["zr"], zm["yc"], zm["w"], zm["seite"]
    n = Vector((s, 0, 0))
    mitte = Vector((xf - s * 1.0, yc, (z0 + zr) / 2))
    # Schwelle unter der Front, Eckpfosten, Raehm, Bruestungsriegel
    h.kasten("Fachwerk", (min(xf, xf - s * 0.16), yc - w - 0.04, z0 - 0.14), (max(xf, xf - s * 0.16) + 0.0, yc + w + 0.04, z0), "balken_c", laengs="y")
    # Front: Bretter bis zum Giebel
    P = lambda yy, z: Vector((xf, yy, z))
    v0, v1, bm = h.baender["planken"]
    bahnen = 5                                             # ungerade: die mittlere Bahn traegt die Giebelspitze
    oa = lambda yy: ze + (w - abs(yy - yc)) * ZWERCH_NEIGUNG
    for i in range(bahnen):
        ya, yb = yc - w + 2 * w * i / bahnen, yc - w + 2 * w * (i + 1) / bahnen
        u0 = h.zufall.random()
        za, zb = (oa(ya), oa(yb)) if i != bahnen // 2 else (ze + (w - (yc - ya)) * ZWERCH_NEIGUNG,) * 2
        h.vieleck("Fachwerk", [P(ya, z0), P(yb, z0), P(yb, zb), P(ya, za)], [(u0, v1), (u0, v0), (u0 + (zb - z0) / bm, v0), (u0 + (za - z0) / bm, v1)], weg_von=mitte)
        if i == bahnen // 2:                               # Spitze ueber der mittleren Bahn
            h.vieleck("Fachwerk", [P(ya, za), P(yb, zb), P(yc, zr)], [(u0 + (za - z0) / bm, v1), (u0 + (zb - z0) / bm, v0), (u0 + (zr - z0) / bm, (v0 + v1) / 2)], weg_von=mitte)
    import wohnhaus_mensch_s1_g1 as g1
    if g1.FACHWERK_ECHT:   # echtes Fachwerk (Nutzer 21.11.2026): Pfosten, Raehm, Bruestungsriegel und Giebelstreben als eine Holzlage
        import holzlage
        dreieck = [(yc - w, ze), (yc + w, ze), (yc, zr)]
        lage = holzlage.Holzlage(umriss=dreieck)          # Holzenden am Dachrand gelten als angeschlossen (darueber liegt das Zwerchdach)
        lage.holz((yc - w, ze - 0.06), (yc + w, ze - 0.06), 0.12, "balken_b", 0, gerade=True)                    # Raehm
        for yy in (yc - w + 0.06, yc + w - 0.06):
            lage.holz((yy, z0), (yy, ze - 0.12), 0.12, "balken_a", 0, gerade=True)                             # Eckpfosten
        lage.holz((yc - w + 0.12, z0 + 0.45), (yc + w - 0.12, z0 + 0.45), 0.10, "balken_b", 2, gerade=True)   # Bruestungsriegel
        for yy in (yc - w + 0.20, yc + w - 0.20):                                                               # Streben im Giebel
            lage.holz((yy, ze), (yc, zr), 0.10, "balken_c", 1, zuschnitt=dreieck)
        befunde = lage.pruefen()
        if befunde: print("HOLZLAGE zwerchhaus", befunde[:3])
        lage.bauen(h, lambda s_, z_, r_: Vector((xf + s * r_, s_, z_)), n, tiefe=0.05)
    # je Holz eine eigene Tiefe (gleiche Tiefe hiesse deckungsgleiche Flaechen an den Kreuzungen: flackern, backen schwarz)
    for yy in ((yc - w + 0.06, yc + w - 0.06) if not g1.FACHWERK_ECHT else ()):
        h.brett("Fachwerk", P(yy, z0), P(yy, ze - 0.12), 0.12, n, "balken_a", tiefe=0.050)          # Eckpfosten bis unter das Raehm
    if not g1.FACHWERK_ECHT:
        h.brett("Fachwerk", P(yc - w, ze - 0.06), P(yc + w, ze - 0.06), 0.12, n, "balken_b", tiefe=0.056)   # Raehm
        h.brett("Fachwerk", P(yc - w + 0.12, z0 + 0.45), P(yc + w - 0.12, z0 + 0.45), 0.10, n, "balken_b", tiefe=0.062)   # Bruestungsriegel
    for yy in ((yc - w + 0.20, yc + w - 0.20) if not g1.FACHWERK_ECHT else ()):        # Streben im Giebel, ueber dem Raehm
        h.brett("Fachwerk", P(yy, ze + 0.02), P(yc + (0.06 if yy > yc else -0.06), zr - 0.16), 0.10, n, "balken_c", tiefe=0.044 if yy < yc else 0.040)
    # Seitenwaende (Putz): Dreieck zwischen Front, Traufe des Zwerchdachs und Hauptdach
    for yy in (yc - w, yc + w):
        xs = s * (F.Z_FIRST - ze + ZWERCH_EINBAU) / s1.NEIGUNG     # dort erreicht die Unterkante (Dach + EINBAU) die Traufhoehe ze
        h.welt("Lehmputz", [(xf, yy, z0), (xf, yy, ze), (xs, yy, ze)], "putz", (xf, yy, 0), (-s, 0, 0), z0 - 0.3, 2.3, weg_von=(xf - s * 0.5, yc, ze - 0.3))


def zwerchdach(d, s1, zm):
    """Kleines Strohdach des Zwerchhauses: zwei Dachflaechen vom First (vorn ueber der Front) bis zur Schnittlinie mit dem Hauptdach
    (0,10 m ueber dessen Unterseite, im Stroh), Dicke ZWERCH_STROH, Stirn vorn als Wulst, Fransen an der Traufe."""
    from trimsheet import BAENDER_AUSSEN
    v0, v1, m = BAENDER_AUSSEN["stroh"]
    f0, f1, fm = BAENDER_AUSSEN["stroh_franse"]
    xf, zr, yc, w, s = zm["xf"], zm["zr"], zm["yc"], zm["w"], zm["seite"]
    N = ZWERCH_NEIGUNG
    xv = xf + s * ZWERCH_UEBER                             # vordere Kante
    we = w + 0.015                                         # seitlich kaum Ueberstand: unter einer Traufe ueber dem Hauptdach sah man durch (Pruefung 16)
    ze2 = zr - we * N                                      # Traufe des Zwerchdachs (Unterseite)
    xs = zm["x_schnitt"]
    oben = Vector((0, 0, ZWERCH_STROH))
    mitte = Vector((xf - s * 0.8, yc, zr - 0.8))
    for sy in (-1, 1):
        rf, rh = Vector((xv, yc, zr)), Vector((xs(zr), yc, zr))
        ef, eh = Vector((xv, yc + sy * we, ze2)), Vector((xs(ze2), yc + sy * we, ze2))
        u0 = d.zufall.random()
        lang = abs(xv - xs(ze2)) / m
        d.vieleck("Stroh", [rf + oben, rh + oben, eh + oben, ef + oben], [(u0, v0), (u0 + lang, v0), (u0 + lang, v1), (u0, v1)], weg_von=mitte)
        d.vieleck("Stroh", [rf, rh, eh, ef], [(0, 0.02), (lang, 0.02), (lang, 0.07), (0, 0.07)], zu=mitte)
        # Traufkante (Stirn) und Fransen
        d.vieleck("Stroh", [ef, eh, eh + oben, ef + oben], [(u0, v0 + 0.01), (u0 + lang, v0 + 0.01), (u0 + lang, v0 + 0.04), (u0, v0 + 0.04)], weg_von=mitte)
        hang = Vector((0, sy * 0.55, -0.45))
        # Fransen nur, wo die Traufe mindestens 0,20 m ueber der Unterseite des Hauptdachs liegt (sie haengen 0,14 m herab)
        x_fr = s * (zm["z_first"] + 0.20 - ze2) / zm["neigung"]
        ka, kb = ef + Vector((0, 0, 0.02)), Vector((x_fr, eh.y, eh.z + 0.02))
        lf = abs(xv - x_fr) / fm
        d.vieleck("Strohkante", [ka, kb, kb + hang * 0.32, ka + hang * 0.32], [(u0, f0), (u0 + lf, f0), (u0 + lf, f1), (u0, f1)], weg_von=mitte)
        # Stirn vorn: Wulst (zwei Flaechen) ueber die Giebelkante
        aus = Vector((s * 0.06, 0, 0))
        d.vieleck("Stroh", [rf + oben, ef + oben, ef + oben * 0.4 + aus, rf + oben * 0.4 + aus], [(u0, v0 + 0.02), (u0 + 0.2, v0 + 0.02), (u0 + 0.2, v0 + 0.05), (u0, v0 + 0.05)], weg_von=mitte, glatt=True)
        d.vieleck("Stroh", [rf + oben * 0.4 + aus, ef + oben * 0.4 + aus, ef, rf], [(u0, v0 + 0.05), (u0 + 0.2, v0 + 0.05), (u0 + 0.2, v0 + 0.08), (u0, v0 + 0.08)], weg_von=mitte, glatt=True)
        d.vieleck("Stroh", [ef, ef + oben * 0.4 + aus, ef + oben], [(u0, v0 + 0.05), (u0 + 0.05, v0 + 0.05), (u0 + 0.03, v0 + 0.08)],
                  weg_von=ef + oben * 0.5 - Vector((0, sy, 0)))   # Ecke des Wulsts an der Traufe geschlossen
    # Firstkappe
    k0, k1 = Vector((xv, yc, zr + ZWERCH_STROH + 0.10)), Vector((xs(zr) - s * 0.05, yc, zr + ZWERCH_STROH + 0.06))
    for sy in (-1, 1):
        r0, r1 = Vector((xv, yc + sy * 0.32, zr - 0.32 * N + ZWERCH_STROH + 0.05)), Vector((xs(zr - 0.32 * N), yc + sy * 0.32, zr - 0.32 * N + ZWERCH_STROH + 0.05))
        u0 = d.zufall.random()
        d.vieleck("Stroh", [r0, r1, k1, k0], [(u0, v0 + 0.06), (u0 + 0.3, v0 + 0.06), (u0 + 0.3, v0 + 0.11), (u0, v0 + 0.11)], weg_von=mitte)
    d.vieleck("Stroh", [k0, Vector((xv, yc - 0.32, zr - 0.32 * N + ZWERCH_STROH + 0.05)), Vector((xv, yc + 0.32, zr - 0.32 * N + ZWERCH_STROH + 0.05))],
              [(0.1, v0 + 0.03), (0.2, v0 + 0.03), (0.15, v0 + 0.06)], weg_von=mitte)


def massfigur_halbling(fusspunkte, hoehe=1.10):
    """Messfigur eines Halblings (nur fuer die Kontrollbilder): gedrungen, Beine bis 0,40 h, Rumpf bis 0,80 h, grosser Kopf."""
    from netzbau import Teil
    from trimsheet import BAENDER_INNEN
    f = Teil("MassfigurHalbling", BAENDER_INNEN, saat=98)
    s = hoehe / 1.10
    for x, y, z in fusspunkte:
        f.kasten("MassfigurHalbling", (x - 0.11 * s, y - 0.09 * s, z), (x + 0.11 * s, y + 0.09 * s, z + 0.44 * s), "balken")
        f.kasten("MassfigurHalbling", (x - 0.17 * s, y - 0.12 * s, z + 0.44 * s), (x + 0.17 * s, y + 0.12 * s, z + 0.86 * s), "balken")
        f.dreh("MassfigurHalbling", (x, y, z + 0.86 * s), [(0.0, 0.0), (0.09 * s, 0.04 * s), (0.11 * s, 0.12 * s), (0.08 * s, 0.21 * s), (0.0, 0.24 * s)], "balken", seiten=8, deckel=False)
    return f


def scheitholz(s1, h, wand, a_start):
    """Scheitholzstapel wie bei den Menschen (wohnhaus_mensch_s1.scheitholz), aber dichter gelegt: Achsabstand 0,175 m (5 mm Fuge statt
    1 cm; durch die 1-cm-Fuge sah Pruefung 16 bei Grundform 4 bis an die Hauswand)."""
    from mathutils import Vector as V
    F = s1.F
    (ox, oy), (ex, ey), _, _ = F.WAENDE[wand]
    start = ox if ex else oy
    a = 0.175
    for i, (ds, z) in enumerate([(a * k, 0.09) for k in range(5)] + [(a / 2 + a * k, 0.26) for k in range(4)] + [(a + a * k, 0.43) for k in range(3)]):
        sp = a_start - start + ds
        p0, p1 = s1.punkt(wand, sp, z, 0.03), s1.punkt(wand, sp, z, 0.62)
        h.stab("Fachwerk", p0, p1, 0.17, "balken_a", kappen=False)
        quer = V((ex, ey, 0))
        h.rechteck("Fachwerk", p1 - quer * 0.085 - s1.EZ * 0.085, quer, s1.EZ, 0.17, 0.17, "farbholz", weg_von=p0, anteil=(0.1, 0.9), u_bereich=(0.1 + 0.07 * i, 0.14 + 0.07 * i))
