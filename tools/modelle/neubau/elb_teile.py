"""Bauteile der Elbenhaeuser (Neubau, Stufe 1): gebogene Streben, aufgeschwungene Ortgangbretter, Messfigur, Dachschwung, Spitzbogen, Unterstand.

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


# ── Ueberarbeitung nach der Rueckmeldung des Nutzers zur Probe (17.11.2026): geschwungenes Dach, Spitzbogen, Holzunterstand ──
SCHWUNG = 0.32             # so viel heben sich die Dachecken (Traufe am Giebel) im Kunstwerk etwa, in Metern
SCHWUNG_LAENGE = 1.6       # ueber diese Laenge der Traufe (vom Giebel her) laeuft der Schwung aus


def _glatt(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def dach_heben(x, y, g, yw0, yw1, xe, vorn_walm):
    """Hebung eines Dachpunkts (Kunstwerk: hochgezogene Ecken). Nur im Dachueberstand, ausserhalb der Wandflucht – ueber den Waenden
    bleibt das Dach, wie es ist (keine Luecke zu Wand und Giebel, Mast und Schlot unberuehrt):
      Traufe:  SCHWUNG * (Anteil im Ueberstand)^1,5 * glatt(Naehe zum Giebelende ueber SCHWUNG_LAENGE)
      Ortgang: SCHWUNG * (Anteil im Giebelueberstand) * |x| / xe  (linear in x: das Ortgangbrett bleibt gerade auf der Dachkante)
    Am Krueppelwalm (Grundform 4 vorn, ueber der Laube) hebt sich nichts."""
    ax = abs(x)
    tx = min(max((ax - g.OX) / max(xe - g.OX, 1e-6), 0.0), 1.0)
    naehe_v = 0.0 if vorn_walm else 1.0 - min(max((y - g.DV) / SCHWUNG_LAENGE, 0.0), 1.0)
    naehe_h = 1.0 - min(max((g.DH - y) / SCHWUNG_LAENGE, 0.0), 1.0)
    traufe = SCHWUNG * tx ** 1.5 * _glatt(max(naehe_v, naehe_h))
    if y < yw0 and not vorn_walm: uy = (yw0 - y) / max(yw0 - g.DV, 1e-6)
    elif y > yw1: uy = (y - yw1) / max(g.DH - yw1, 1e-6)
    else: uy = 0.0
    ortgang = SCHWUNG * min(max(uy, 0.0), 1.0) * min(ax / xe, 1.0)
    return max(traufe, ortgang)


def dach_schwingen(d, g, yw0, yw1, xe):
    """Hebt alle Punkte des Dachs (Teil „Dach“: Schindeln, Fuesse, Ortgang, Sparrenkoepfe, Schwuenge) nach dach_heben. UV bleibt (die
    Schindeln laufen mit), die Normalen rechnet der Export neu."""
    walm = bool(g.walm)
    d.flaechen = [([Vector((p.x, p.y, p.z + dach_heben(p.x, p.y, g, yw0, yw1, xe, walm and p.y < yw0))) for p in pts], uv, st, fa, gl)
                  for pts, uv, st, fa, gl in d.flaechen]


BOGEN_RAND = 0.08          # Breite des Bogenbretts (mittig auf der Bogenlinie: hoechstens 4 cm ueber die Oeffnung, Pruefung 1)
BOGEN_STUECKE = 6


def spitzbogen(teil, P, n, s0, s1, oben, vor=0.01):
    """Spitzbogen NUR AUSSEN in einer rechteckigen Oeffnung (Kunstwerk: Haustuer und Giebelfenster): zwei Kreisboegen vom Kaempfer an
    den Laibungen bis zum Scheitel in der Mitte der Oberkante; die Zwickel zwischen Bogen und oberen Ecken sind mit Putz geschlossen,
    darauf ein Bogenbrett. Die Oeffnung der Huelle bleibt rechteckig, innen aendert sich nichts (innen = aussen, Pruefungen 3 und 24).
    Stich 1,3 x halbe Weite (spitz: Radius groesser als die halbe Weite). P(s, z): Punkt auf der Aussenflaeche der Wand; `vor`: Abstand
    der Zwickel vor der Wand (vor Laeden der Luke weiter)."""
    n = Vector(n).normalized()
    a = (s1 - s0) / 2
    h = 1.3 * a
    r = (a * a + h * h) / (2 * a)
    zs = oben - h
    winkel = math.atan2(h, a - r)
    links = []
    for k in range(BOGEN_STUECKE + 1):
        t = math.pi + (winkel - math.pi) * k / BOGEN_STUECKE
        links.append((s0 + r + r * math.cos(t), zs + r * math.sin(t)))
    rechts = [(s0 + s1 - s, z) for s, z in links]
    Q = lambda s, z, dv=0.0: Vector(P(s, z)) + n * (vor + dv)
    hinten = Q((s0 + s1) / 2, oben) - n
    for seite, ecke in ((links, (s0, oben)), (rechts, (s1, oben))):
        for (sa, za), (sb, zb) in zip(seite[:-1], seite[1:]):
            pts = [Q(*ecke), Q(sa, za), Q(sb, zb)]
            teil.welt("Lehmputz", pts, "putz", Q(s0, 0.0), (Q(s1, 0.0) - Q(s0, 0.0)).normalized(), 0.0, 2.2, weg_von=hinten)
        streifen(teil, "Fachwerk", [Q(s, z) for s, z in seite], [BOGEN_RAND] * len(seite), n, "balken_c", 0.047, kappe=False)   # Sichtseite 5 mm vor Staendern und Pfosten (0,05): nicht deckungsgleich


def unterstand(teil, x_wand, y0, y1, z_oben, tiefe=1.0, fall=0.35):
    """Holzunterstand am Haus (Kunstwerk: offener Unterstand mit Pultdach an der Seite, darunter Brennholz): Wandbalken, zwei Pfosten,
    Pfette, Pultdach aus Brettern. An der Seitenwand bei x = x_wand (> 0), von y0 bis y1; Dach von z_oben an der Wand bis z_oben - fall."""
    xa = x_wand + tiefe
    dz = lambda x: z_oben - fall * (x - x_wand) / tiefe          # Unterkante des Dachs
    teil.kasten("Fachwerk", (x_wand, y0, z_oben - 0.14), (x_wand + 0.10, y1, z_oben), "balken_c", ohne=("-x",), laengs="y")   # Wandbalken
    xp = xa - 0.12
    teil.kasten("Fachwerk", (xp - 0.06, y0, dz(xp) - 0.14), (xp + 0.06, y1, dz(xp + 0.06) - 0.005), "balken_c", laengs="y")    # Pfette
    for y in (y0 + 0.08, y1 - 0.08):
        teil.kasten("Fachwerk", (xp - 0.05, y - 0.05, 0.0), (xp + 0.05, y + 0.05, dz(xp) - 0.14), "balken_a", ohne=("-z",), laengs="z")  # Pfosten
    d = 0.04
    ecken = [Vector((x_wand, y0 - 0.10, z_oben)), Vector((xa + 0.10, y0 - 0.10, dz(xa + 0.10))), Vector((xa + 0.10, y1 + 0.10, dz(xa + 0.10))), Vector((x_wand, y1 + 0.10, z_oben))]
    oben_ = [p + Vector((0, 0, d)) for p in ecken]
    v0, v1, m = teil.baender["planken"]
    lang, quer = (y1 - y0 + 0.2) / m, (xa + 0.10 - x_wand) / m
    mitte = sum(ecken, Vector()) / 4 + Vector((0, 0, d / 2))
    teil.vieleck("Fachwerk", oben_, [(0, v1), (0, v0), (lang, v0), (lang, v1)], zu=mitte + Vector((0, 0, 9)))
    teil.vieleck("Fachwerk", ecken, [(0, v1), (0, v0), (lang, v0), (lang, v1)], zu=mitte - Vector((0, 0, 9)))
    for i in range(3):                                           # Stirnseiten (zur Wand hin keine: dort liegt das Dach an)
        a, b = ecken[i], ecken[i + 1]
        teil.vieleck("Fachwerk", [a, b, b + Vector((0, 0, d)), a + Vector((0, 0, d))], [(0, v1), (quer, v1), (quer, v1 - 0.1 * (v1 - v0)), (0, v1 - 0.1 * (v1 - v0))], weg_von=mitte)
