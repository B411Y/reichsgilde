"""Bauteile der Elbenhaeuser (Neubau, Stufe 1): gebogene Streben, aufgeschwungene Ortgangbretter, Messfigur, Wappenregel.

Vorbild ist das Kunstwerk „Wohnhaus Elb, Stufe 1“ (wohnhaus_lvl1_elb.jpg, wohnhaus_in_lvl1_elb.jpg): das Fachwerk der Menschen, aber die
Streben in den Eckgefachen und im Giebel sind gebogen; die Ortgangbretter schwingen an der Traufe und am First nach oben aus.
Alles andere (Masse, Waende, Fenster, Moebel) kommt unveraendert aus den Bauskripten der Menschen (wohnhaus_elb_s1.py).
"""
import math

from mathutils import Vector

from netzbau import SCHWARZ

STREBE_BREITEN = (0.12, 0.13)          # Streben der Menschen: Giebel 0,12 m, Eckgefache 0,13 m (wohnhaus_mensch_s1_g1.fachwerk_wand / giebel_fachwerk)
BOGEN_STICH = 0.08                     # Stich der gebogenen Strebe je Meter Sehne (hoechstens BOGEN_STICH_MAX)
BOGEN_STICH_MAX = 0.12
BOGEN_TEILE = 8                        # Abschnitte je Strebe (48 statt 6 Dreiecke; je Abschnitt unter 0,35 m hoch, sonst zaehlt Pruefung 7 ihn als Staender)


def streifen(teil, stoff, mitte, breiten, n, band, tiefe, kappe=False, farbe=SCHWARZ, anfang=None):
    """Gebogenes Brett auf einer Ebene mit der Normalen `n`: Mittellinie `mitte` (Punkte in der Ebene), je Punkt eine Breite;
    Sichtseite um `tiefe` vor der Ebene und die zwei Laengsseiten (wie netzbau.Teil.brett), mit `kappe` auch die Stirn am Ende.
    Die Textur laeuft entlang des Bretts (Band `band`, quer die ganze Bandhoehe, die Seiten 30 % davon). `anfang`: Richtung am ersten
    Punkt (setzt ein gerades Brett ohne Keil fort: der erste Querschnitt steht senkrecht auf dessen Achse)."""
    n = Vector(n).normalized()
    p = [Vector(q) for q in mitte]
    v0, v1, m = teil.baender[band]
    quer = []
    for i in range(len(p)):
        t = Vector(anfang).normalized() if (i == 0 and anfang is not None) else (p[min(i + 1, len(p) - 1)] - p[max(i - 1, 0)]).normalized()
        quer.append(n.cross(t).normalized())
    u0 = teil.zufall.random()
    bogen = [0.0]
    for i in range(1, len(p)): bogen.append(bogen[-1] + (p[i] - p[i - 1]).length)
    hinten = sum(p, Vector()) / len(p) - n
    for i in range(len(p) - 1):
        ua, ub = u0 + bogen[i] / m, u0 + bogen[i + 1] / m
        la, lb = p[i] - quer[i] * breiten[i] / 2, p[i + 1] - quer[i + 1] * breiten[i + 1] / 2
        ra, rb = p[i] + quer[i] * breiten[i] / 2, p[i + 1] + quer[i + 1] * breiten[i + 1] / 2
        teil.vieleck(stoff, [la + n * tiefe, lb + n * tiefe, rb + n * tiefe, ra + n * tiefe], [(ua, v1), (ub, v1), (ub, v0), (ua, v0)], weg_von=hinten, farbe=farbe)
        vs = v1 - 0.3 * (v1 - v0)
        mitte_i = (p[i] + p[i + 1]) / 2 + n * (tiefe / 2)
        for a, b in ((la, lb), (ra, rb)):            # zwei Dreiecke mit fester Diagonale: gespiegelte Streben werden gleich zerlegt (Pruefung 2)
            teil.vieleck(stoff, [a, b, b + n * tiefe], [(ua, v1), (ub, v1), (ub, vs)], weg_von=mitte_i, farbe=farbe)
            teil.vieleck(stoff, [a, b + n * tiefe, a + n * tiefe], [(ua, v1), (ub, vs), (ua, vs)], weg_von=mitte_i, farbe=farbe)
    if kappe:
        e = len(p) - 1
        a, b = p[e] - quer[e] * breiten[e] / 2, p[e] + quer[e] * breiten[e] / 2
        teil.vieleck(stoff, [a, b, b + n * tiefe, a + n * tiefe], [(u0, v1), (u0 + breiten[e] / m, v1), (u0 + breiten[e] / m, vs), (u0, vs)],
                     weg_von=p[e - 1] + n * (tiefe / 2), farbe=farbe)


def bogen_strebe(teil, stoff, a, b, breite, n, band, tiefe):
    """Gebogene Strebe statt der geraden von a nach b: dieselben Endpunkte, der Bogen woelbt sich in der Wandebene nach oben
    (in den Eckgefachen zur oberen Ecke hin, im Giebel unter den Sparren); Stich 8 % der Sehne, hoechstens 12 cm (Kunstwerk)."""
    a, b, n = Vector(a), Vector(b), Vector(n).normalized()
    sehne = b - a
    d = n.cross(sehne.normalized()).normalized()
    if d.z < 0: d = -d
    stich = min(BOGEN_STICH_MAX, BOGEN_STICH * sehne.length)
    mitte = [a + sehne * (k / BOGEN_TEILE) + d * (stich * 4 * (k / BOGEN_TEILE) * (1 - k / BOGEN_TEILE)) for k in range(BOGEN_TEILE + 1)]
    streifen(teil, stoff, mitte, [breite] * len(mitte), n, band, tiefe)


class BogenTeil:
    """Stellvertreter fuer ein Teil (netzbau.Teil): `brett`-Aufrufe fuer Streben (Band balken_a, Breite 0,12 oder 0,13 m, schraeg)
    werden zu gebogenen Streben; alles andere geht unveraendert an das Teil."""

    def __init__(self, teil):
        self._teil = teil

    def __getattr__(self, name):
        return getattr(self._teil, name)

    def brett(self, stoff, a, b, breite, n, band, tiefe=0.05, seiten=True, farbe=SCHWARZ, laengs="u"):
        av, bv = Vector(a), Vector(b)
        schraeg = abs(av.z - bv.z) > 0.2 and (av.xy - bv.xy).length > 0.2
        if band == "balken_a" and laengs == "u" and seiten and any(abs(breite - s) < 1e-6 for s in STREBE_BREITEN) and schraeg:
            return bogen_strebe(self._teil, stoff, av, bv, breite, n, band, tiefe)
        return self._teil.brett(stoff, a, b, breite, n, band, tiefe=tiefe, seiten=seiten, farbe=farbe, laengs=laengs)


def ortgang_schwung(d, g, xe, dick_unten, dick_oben, walm):
    """Aufgeschwungene Ortgangbretter (Kunstwerk): an jeder Traufecke biegt das Brett nach aussen oben aus, am First laufen die Bretter
    beider Seiten ueber Kreuz weiter und schwingen nach oben. Gleiche Ebene, Breite und Tiefe wie die Ortgangbretter von
    zwerg_teile.dach (0,17 x 0,05 m, Band balken_c); das Brett der rechten Seite liegt dort 6 mm weiter aussen, hier ebenso.
    Am Walm (Grundform 4 vorn) nur die Traufecken (der Walm hat keinen Giebel)."""
    for y, aussen in ((g.DV, -1), (g.DH, 1)):
        for seite in (-1, 1):
            vor = Vector((0, aussen * (0.02 + (0.006 if seite > 0 else 0.0)), -0.05))
            x = seite * xe
            traufe = Vector((x, y, g.dach_z(x) + dick_unten)) + vor
            first = Vector((0, y, g.Z_FIRST + dick_oben)) + vor
            n = Vector((0, aussen, 0))
            # Traufe: Richtung des Bretts hangabwaerts, dann in 3 Abschnitten nach oben gebogen (insgesamt 70 Grad), 0,42 m lang
            ab = (traufe - first).normalized()
            punkte, breiten = [traufe], [0.17]
            for k in range(1, 4):
                winkel = math.radians(70 * k / 3) * seite                 # in der x-z-Ebene nach oben drehen (rechts gegen den Uhrzeigersinn)
                r = Vector((ab.x * math.cos(winkel) - ab.z * math.sin(winkel), 0, ab.x * math.sin(winkel) + ab.z * math.cos(winkel)))
                punkte.append(punkte[-1] + r * 0.14)
                breiten.append(0.17 - 0.025 * k)
            streifen(d, "Fachwerk", punkte, breiten, n, "balken_c", 0.05, kappe=True, anfang=ab)
            if walm and aussen < 0: continue
            # First: ueber Kreuz weiter (Richtung des Bretts), dann nach oben, 0,40 m lang
            auf = (first - traufe).normalized()
            punkte, breiten = [first], [0.17]
            for k in range(1, 4):
                winkel = math.radians(25 * k / 3) * (-seite)
                r = Vector((auf.x * math.cos(winkel) - auf.z * math.sin(winkel), 0, auf.x * math.sin(winkel) + auf.z * math.cos(winkel)))
                punkte.append(punkte[-1] + r * 0.13)
                breiten.append(0.17 - 0.025 * k)
            streifen(d, "Fachwerk", punkte, breiten, n, "balken_c", 0.05, kappe=True, anfang=auf)


def massfigur_elb(fusspunkte, hoehe=1.85):
    """Messfigur eines Elben (nur fuer die Kontrollbilder): schlanker als die der Menschen, Beine bis 0,50 h, Rumpf bis 0,83 h."""
    from netzbau import Teil
    from trimsheet import BAENDER_INNEN
    f = Teil("MassfigurElb", BAENDER_INNEN, saat=97)
    s = hoehe / 1.85
    for x, y, z in fusspunkte:
        f.kasten("MassfigurElb", (x - 0.12 * s, y - 0.09 * s, z), (x + 0.12 * s, y + 0.09 * s, z + 0.93 * s), "balken")
        f.kasten("MassfigurElb", (x - 0.20 * s, y - 0.11 * s, z + 0.93 * s), (x + 0.20 * s, y + 0.11 * s, z + 1.54 * s), "balken")
        f.dreh("MassfigurElb", (x, y, z + 1.56 * s), [(0.0, 0.0), (0.09 * s, 0.05 * s), (0.10 * s, 0.15 * s), (0.07 * s, 0.25 * s), (0.0, 0.29 * s)], "balken", seiten=8, deckel=False)
    return f


# ── Wappenregel wappen_ort fuer Stufe 1 (dieselbe Rechnung wie im Feinschliff der Menschen, Branch phase5/haeuser-feinschliff-main
#    25f10843, dort wohnhaus_mensch_s1_g1.wappen_frei_ruecken; hier in der Elben-Schicht, weil der Feinschliff noch nicht in main ist) ──
WAPPEN_ABSTAND = 0.05     # Mindestabstand des Wappens zu Laden, Fenster, Gewaende, Holz und Vordach (Pruefung 19)


def _vieleck_trifft(pts2, u0, u1, z0, z1):
    """Schneidet das ebene, konvexe Vieleck (u, z) das Rechteck? Trennende Achsen wie Pruefung 19."""
    if min(p[0] for p in pts2) >= u1 or max(p[0] for p in pts2) <= u0 or min(p[1] for p in pts2) >= z1 or max(p[1] for p in pts2) <= z0:
        return False
    ecken = ((u0, z0), (u1, z0), (u1, z1), (u0, z1))
    for (ax, ay), (bx, by) in zip(pts2, pts2[1:] + pts2[:1]):
        nx, ny = by - ay, ax - bx
        if abs(nx) + abs(ny) < 1e-9: continue
        a = [nx * x + ny * y for x, y in pts2]; b = [nx * x + ny * y for x, y in ecken]
        if max(a) <= min(b) + 1e-9 or max(b) <= min(a) + 1e-9:
            return False
    return True


def wappen_frei_ruecken(wappen, teile, abstand=WAPPEN_ABSTAND + 0.005):
    """Haelt das Wappen in der Ansicht seiner Wand nicht `abstand` zu allem, was vor der Mauerflucht steht (Laden, Gewaende,
    Fachwerk, Vordach, Tuer) und zu den Scheiben, wird es so wenig wie moeglich verschoben (bis 0,6 m in der Wand) und nur wenn
    noetig kleiner (bis zur Haelfte). Liegt es frei, bleibt alles, wie es ist. `teile`: Haus, Dach, Scheiben."""
    import numpy as np
    pw = [p for fl in wappen.flaechen for p in fl[0]]
    ph = [p for t in teile if t.name == "Haus" for fl in t.flaechen for p in fl[0]]
    ausd = [max(p[i] for p in pw) - min(p[i] for p in pw) for i in (0, 1)]
    ax = 0 if ausd[0] < ausd[1] else 1
    au = 1 - ax
    mitte_h = (max(p[ax] for p in ph) + min(p[ax] for p in ph)) / 2
    s = 1.0 if sum(p[ax] for p in pw) / len(pw) > mitte_h else -1.0
    wand = min(s * p[ax] for p in pw)
    hindernisse = []
    for t in teile:
        for pts, _uv, stoff_, *_ in t.flaechen:
            d = [s * q[ax] for q in pts]
            glas = stoff_ in ("Fenster", "Bleinetz") and abs(sum(d) / len(d) - wand) < 0.4
            vor = max(d) > wand + 0.004 and min(d) < wand + 0.40
            if glas or vor:
                hindernisse.append([(q[au], q.z) for q in pts])
    u0, u1 = min(p[au] for p in pw), max(p[au] for p in pw)
    z0, z1 = min(p.z for p in pw), max(p.z for p in pw)
    uc, zc = (u0 + u1) / 2, (z0 + z1) / 2

    def frei(k, du, dz):
        hu, hz = (u1 - u0) / 2 * k + abstand, (z1 - z0) / 2 * k + abstand
        return not any(_vieleck_trifft(h, uc + du - hu, uc + du + hu, zc + dz - hz, zc + dz + hz) for h in hindernisse)
    if frei(1.0, 0.0, 0.0):
        print("WAPPEN frei, bleibt an seinem Ort")
        return
    boxen = np.array([(min(p[0] for p in h), max(p[0] for p in h), min(p[1] for p in h), max(p[1] for p in h)) for h in hindernisse])
    schritte = np.arange(-0.60, 0.6001, 0.01)
    du_, dz_ = np.meshgrid(schritte, schritte)
    du_, dz_ = du_.ravel(), dz_.ravel()
    reihe = np.argsort(du_ * du_ + dz_ * dz_, kind="stable")
    for k in np.arange(1.0, 0.499, -0.02):
        hu, hz = (u1 - u0) / 2 * k + abstand, (z1 - z0) / 2 * k + abstand
        for i in reihe:
            du, dz = float(du_[i]), float(dz_[i])
            a0, a1, b0, b1 = uc + du - hu, uc + du + hu, zc + dz - hz, zc + dz + hz
            nah = (boxen[:, 0] < a1) & (boxen[:, 1] > a0) & (boxen[:, 2] < b1) & (boxen[:, 3] > b0)
            if any(_vieleck_trifft(hindernisse[j], a0, a1, b0, b1) for j in np.nonzero(nah)[0]):
                continue

            def neu(p):                                   # in der Wandebene verkleinern und schieben; die Tiefe (Achse ax) bleibt
                q = Vector(p)
                q[au] = uc + (p[au] - uc) * k + du
                q.z = zc + (p.z - zc) * k + dz
                return q
            wappen.flaechen = [([neu(p) for p in pts], uv, st, fa, gl) for pts, uv, st, fa, gl in wappen.flaechen]
            print(f"WAPPEN gerueckt: {du * 100:+.0f} cm entlang der Wand, {dz * 100:+.0f} cm in der Hoehe, Groesse x {k:.2f}")
            return
    print("WAPPEN FEHLER: kein freier Ort gefunden")
