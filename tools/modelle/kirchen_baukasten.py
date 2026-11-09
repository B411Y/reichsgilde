"""Baukasten für die romanischen Kirchen Kölns um 1400 (koelner_kirchen.py). Kein eigenes Modell, nur Bauteile.

Nutzt bausteine.py (Netz, Stoffe, Dächer, Turmhelm, Achteck, Platte) und dom.py (Spitzbogen). Koordinaten wie dort:
Blender Z oben, Westseite (Eingang) bei −Y, Chor im Osten bei +Y, Süden = +X, Ursprung in der Mitte der Fläche.

Stoffe (steinsichtig wie heute, nicht verputzt; Entscheidung des Nutzers, 9.11.2026): „Tuff“ für die Mauerflächen, „Trachyt“ für Lisenen,
Friese, Gesimse und Gewände, „Basalt“ für Sockel und Säulchen, „Blei“ für das Zeltdach von St. Gereon. Tuff, Trachyt und Basalt stehen in
game/daten/materialbibliothek.json (quader bzw. bruchstein) und materialarten.json (mauer); Blei bleibt eine einfache Farbe.

Bauteile:
    mauerzug        Wand entlang eines Vielecks (Apsis, Zehneck)
    konche          halbrunde Konche mit Fenstern, Diensten, Zwerggalerie, Gesims und Halbkegeldach
    basilika        Langhaus (Mittelschiff mit Obergaden, Seitenschiffe mit Pultdach, Lisenen, Rundbogenfries, Westgiebel)
    turm4           Viereckturm mit Geschossen, Ecklisenen, Rundbogenfries, Schallarkaden; Dach Pyramide, Rautendach, Faltdach, Knickhelm
    turm8           Achteckturm (Vierungsturm, Ecktürmchen, Laterne) mit Fenstern oder Schallöffnungen und Helm, Faltdach oder Kegeldach
    zehneck         Zentralbau (St. Gereon) mit zwei Fensterreihen, Strebepfeilern, Zwerggalerie, Falt- oder Zeltdach
    portal_rund     gestuftes Rundbogenportal mit Tür und Stufe
    spitzfenster    gotisches Fenster (Westturm von St. Severin)
    geruest         Baugerüst (Turm im Bau)
Kleinteile: rundfenster, blende, schallarkade, fries, lisene, gesims, sockel, zwerggalerie, giebel, pult, faltdach, rhombendach, knickhelm.

Wandsystem: `wm(p, phi)` legt ein lokales System an den Fuß einer Außenfläche; u läuft entlang der Wand, v nach oben, −y nach außen.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import dom  # noqa: E402  (Spitzbogen, Gewände)
from bausteine import Matrix, Vector  # noqa: E402

# Stoffe der Kirchen (sRGB)
bs.FARBEN.update({
    "Tuff": (0.64, 0.62, 0.56),        # rheinischer Tuffstein, hell gelblich-grau
    "Trachyt": (0.52, 0.51, 0.48),     # Drachenfels-Trachyt: Lisenen, Friese, Gesimse, Gewände
    "Basalt": (0.25, 0.25, 0.27),      # Sockel, Säulchen der Zwerggalerie
    "Blei": (0.50, 0.52, 0.55),        # Zeltdach von St. Gereon (Bleiplatten)
})
bs.BIBLIOTHEK_JE_FLAECHE = True        # Material-Bibliothek je Fläche (Material „Farben“), wie beim Kölner Dom

SCHIEFER = dict(bs.SCHIEFER)


def wm(p, phi):
    """Wandsystem am Punkt p (Fuß der Außenfläche) mit Außennormale in Richtung phi (Grad von +X)."""
    return Matrix.Translation(Vector(p)) @ Matrix.Rotation(math.radians(phi + 90), 4, "Z")


def aussen(M):
    return M.to_3x3() @ Vector((0, -1, 0))


def flach(netz, M, umriss, y, m):
    """Ebenes Vieleck im Wandsystem bei Tiefe y (nur die Schauseite)."""
    netz.vieleck_nach([M @ Vector((u, y, v)) for u, v in umriss], aussen(M), m)


def rundbogen(u, v, b, h, teile=4):
    """Umriss einer Rundbogenöffnung (Breite b, Höhe h bis zum Scheitel) mit dem Fuß bei (u, v)."""
    r = b / 2
    k = v + h - r
    return [(u - r, v), (u + r, v)] + [(u + r * math.cos(math.pi * i / teile), k + r * math.sin(math.pi * i / teile)) for i in range(teile + 1)]


def ring(u, v, b, h, g, teile=4):
    """Rahmen der Breite g um eine Rundbogenöffnung (U-förmig, unten offen)."""
    innen = rundbogen(u, v, b, h, teile)
    aussen_ = rundbogen(u, v, b + 2 * g, h + g, teile)
    return aussen_[1:] + [aussen_[0], innen[0]] + list(reversed(innen[2:])) + [innen[1]]


def rundfenster(netz, st, M, u, v, b, h, glas="Fenster", rahmen="Trachyt", g=0.14):
    """Rundbogenfenster: Scheibe („Fenster“ leuchtet zur Messe, „Glas“ bleibt dunkel), Gewände und Sohlbank."""
    flach(netz, M, rundbogen(u, v, b, h), -0.01, st[glas])
    flach(netz, M, ring(u, v, b, h, g), -0.05, st[rahmen])
    netz.quader((u, -0.05, v - 0.06), (b + 2 * g + 0.1, 0.12, 0.12), st[rahmen], M, ohne=("+y",))


def blende(netz, st, M, u, v, b, h, stoff="Trachyt"):
    """Blendbogen: nur der Rahmen, ohne Öffnung."""
    flach(netz, M, ring(u, v, b, h, 0.12), -0.04, st[stoff])


def schallarkade(netz, st, M, u, v, b, h):
    """Gekuppelte Schallöffnung: zwei dunkle Bögen, Mittelsäulchen, Rahmen."""
    for s in (-1, 1):
        flach(netz, M, rundbogen(u + s * b / 4, v, b / 2 - 0.1, h - 0.15), -0.01, st["Loch"])
    flach(netz, M, ring(u, v, b, h, 0.15), -0.05, st["Trachyt"])
    netz.quader((u, -0.06, v + (h - b / 4) / 2), (0.16, 0.12, h - b / 4), st["Basalt"], M, ohne=("+y",))


def fries(netz, st, M, L, v_oben, hoehe=0.42, bogen=0.9, stoff="Trachyt", y=-0.05):
    """Rundbogenfries unter der Traufe: ein Band mit kleinen Bögen an der Unterkante."""
    n = max(1, round(L / bogen))
    w = L / n
    r = w / 2 * 0.78
    base = v_oben - hoehe
    pts = [(-L / 2, v_oben), (-L / 2, base)]
    for i in range(n):
        c = -L / 2 + w * (i + 0.5)
        for k in range(4):
            a = math.pi * (1 - k / 3)
            pts.append((c + r * math.cos(a), base + 0.07 + r * math.sin(a)))
        pts.append((c + w / 2, base))
    pts.append((L / 2, v_oben))
    flach(netz, M, pts, y, st[stoff])


def lisene(netz, st, M, u, v0, v1, breite=0.45, vor=0.1, stoff="Trachyt"):
    netz.quader((u, -vor / 2, (v0 + v1) / 2), (breite, vor, v1 - v0), st[stoff], M, ohne=("+y", "-z"))


def gesims(netz, st, M, L, v, vor=0.16, hoehe=0.22, stoff="Trachyt"):
    netz.quader((0, 0.0, v), (L + 2 * vor, 2 * vor, hoehe), st[stoff], M, ohne=("+y",))


def sockel(netz, st, M, L, hoehe=0.7, stoff="Basalt"):
    netz.quader((0, 0.0, hoehe / 2), (L + 0.2, 0.2, hoehe), st[stoff], M, ohne=("+y", "-z"))


def zwerggalerie(netz, st, M, L, v0, v1, abstand=0.75):
    """Zwerggalerie: dunkler Laufgang hinter einer Reihe Säulchen mit Bögen, Brüstung darunter."""
    flach(netz, M, [(-L / 2, v0), (L / 2, v0), (L / 2, v1), (-L / 2, v1)], -0.01, st["Loch"])
    n = max(2, round(L / abstand))
    for i in range(1, n):
        u = -L / 2 + L * i / n
        netz.quader((u, -0.12, (v0 + v1) / 2 - 0.15), (0.13, 0.13, v1 - v0 - 0.3), st["Basalt"], M, ohne=("+y", "-z", "+z"))
    fries(netz, st, M, L, v1 + 0.05, hoehe=0.42, bogen=L / n, stoff="Tuff", y=-0.18)
    netz.quader((0, -0.08, v0 - 0.1), (L + 0.05, 0.26, 0.2), st["Trachyt"], M, ohne=("+y",))   # Brüstung


# ─────────────────────────────────────────────────────────────────────────────
#  Mauerzüge
# ─────────────────────────────────────────────────────────────────────────────

def kante(p, q, z=0.0):
    """Wandsystem auf der Kante p→q (Außenseite rechts der Laufrichtung, d. h. Vieleck gegen den Uhrzeigersinn)."""
    p, q = Vector((p[0], p[1])), Vector((q[0], q[1]))
    e = q - p
    phi = math.degrees(math.atan2(-e.x, e.y))
    m = (p + q) / 2
    return wm((m.x, m.y, z), phi), e.length, phi


def mauerzug(netz, st, punkte, z0, z1, d=0.8, stoff="Tuff", geschlossen=False, mit_sockel=True):
    """Wände entlang `punkte` (gegen den Uhrzeigersinn, außen = rechts). Gibt [(M, L, phi)] je Kante zurück."""
    kanten = []
    paare = list(zip(punkte, punkte[1:])) + ([(punkte[-1], punkte[0])] if geschlossen else [])
    for p, q in paare:
        M, L, phi = kante(p, q, z0)
        netz.quader((0, d / 2, (z1 - z0) / 2), (L + 0.04, d, z1 - z0), st[stoff], M, ohne=("-z",))
        if mit_sockel and z0 < 0.5:
            sockel(netz, st, M, L)
        kanten.append((M, L, phi))
    return kanten


def bogenpunkte(mitte, r, w0, w1, n, ry=None):
    ry = r if ry is None else ry
    return [(mitte[0] + r * math.cos(math.radians(w0 + (w1 - w0) * i / n)),
             mitte[1] + ry * math.sin(math.radians(w0 + (w1 - w0) * i / n))) for i in range(n + 1)]


def kegelsegmente(netz, st, punkte, spitze, z_fuss, stoff="Schiefer"):
    """Dach aus Dreiecken von einer Punktreihe (Traufe auf z_fuss) zu einer Spitze."""
    sp = Vector(spitze)
    for p, q in zip(punkte, punkte[1:]):
        a, b = Vector((p[0], p[1], z_fuss)), Vector((q[0], q[1], z_fuss))
        mitte = (a + b) / 2
        n = Vector((mitte.x - sp.x, mitte.y - sp.y, 0)).normalized() + Vector((0, 0, 0.6))
        netz.vieleck_nach([a, b, sp], n, st[stoff])


def knauf_und_kreuz(netz, st, apex, gross=False):
    """Vergoldeter Knauf und eisernes Kreuz auf einer Dachspitze."""
    r, h, d = (0.14, 0.3, 0.06) if gross else (0.12, 0.25, 0.05)
    bs.achteck(netz, st, apex - Vector((0, 0, 0.3)), r, 0.0, h, "Gold")
    netz.stab(apex - Vector((0, 0, 0.1)), apex + Vector((0, 0, 1.2 if gross else 0.9)), d, d, st["Eisen"])
    q = 0.35 if gross else 0.25
    netz.stab(apex + Vector((-q, 0, 0.8 if gross else 0.6)), apex + Vector((q, 0, 0.8 if gross else 0.6)), d, d, st["Eisen"])


# ─────────────────────────────────────────────────────────────────────────────
#  Konche (Apsis) mit Zwerggalerie
# ─────────────────────────────────────────────────────────────────────────────

def konche(netz, st, mitte, r, richt, z_traufe, n=5, fenster=((2.2, 0.95, 2.4),), galerie=True, z_spitze=None,
           dienste=True, stoff="Tuff"):
    """Halbrunde Konche um `mitte` (x, y), Rundung in Richtung `richt` (Grad von +X)."""
    pts = bogenpunkte(mitte, r, richt - 90, richt + 90, n)
    kanten = mauerzug(netz, st, pts, 0.0, z_traufe, 0.8, stoff)
    zg = z_traufe - 1.55
    for M, L, phi in kanten:
        for v, b, h in fenster:
            rundfenster(netz, st, M, 0.0, v, b, h)
        if galerie:
            zwerggalerie(netz, st, M, L - 0.1, zg, z_traufe - 0.45)
        else:
            fries(netz, st, M, L, z_traufe - 0.2)
        gesims(netz, st, M, L, z_traufe - 0.1)
    if dienste:
        for p in pts[1:-1]:
            w = math.degrees(math.atan2(p[1] - mitte[1], p[0] - mitte[0]))
            M = wm((p[0], p[1], 0), w)
            lisene(netz, st, M, 0.0, 0.7, (zg - 0.2) if galerie else z_traufe - 0.6, breite=0.32, vor=0.18, stoff="Trachyt")
    # Halbkegeldach
    ro = r + 0.38
    dach = bogenpunkte(mitte, ro, richt - 90, richt + 90, n)
    zs = z_spitze if z_spitze is not None else z_traufe + r * 0.95
    kegelsegmente(netz, st, dach, (mitte[0], mitte[1], zs), z_traufe + 0.02)
    return pts


# ─────────────────────────────────────────────────────────────────────────────
#  Langhaus
# ─────────────────────────────────────────────────────────────────────────────

def satteldach_y(netz, st, y0, y1, xh, z, neigung, **k):
    """Satteldach mit First entlang Y über der Breite ±xh."""
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")
    opts = dict(SCHIEFER)
    opts.update(dict(traufe=0.35, ortgang=0.25, enden="Trachyt"))
    opts.update(k)
    return bs.satteldach(netz, st, y0, y1, -xh, xh, z, neigung, matrix=drehung, **opts)


def satteldach_x(netz, st, x0, x1, yh0, yh1, z, neigung, **k):
    """Satteldach mit First entlang X (Querhaus) zwischen y = yh0 und yh1."""
    opts = dict(SCHIEFER)
    opts.update(dict(traufe=0.35, ortgang=0.25, enden="Trachyt"))
    opts.update(k)
    return bs.satteldach(netz, st, x0, x1, yh0, yh1, z, neigung, **opts)


def giebel(netz, st, mitte_fuss, halb, hoch, richtung, stoff="Tuff", oculus=True):
    """Giebeldreieck. `richtung`: 'vorne' (−Y), 'hinten' (+Y), 'links' (−X), 'rechts' (+X); mit Rundfenster (Oculus)."""
    M = bs.wandmatrix(mitte_fuss, richtung)
    bs.platte(netz, M, [(-halb, 0), (halb, 0), (0, hoch)], 0, 0.6, st[stoff])
    if oculus and hoch > 2.5:
        kreis = [(0.55 * math.cos(2 * math.pi * k / 8), hoch * 0.38 + 0.55 * math.sin(2 * math.pi * k / 8)) for k in range(8)]
        flach(netz, M, kreis, -0.01, st["Fenster"])
        ringaussen = [(0.8 * math.cos(2 * math.pi * k / 8), hoch * 0.38 + 0.8 * math.sin(2 * math.pi * k / 8)) for k in range(8)]
        for k in range(8):
            a, b = ringaussen[k], ringaussen[(k + 1) % 8]
            c, d = kreis[(k + 1) % 8], kreis[k]
            flach(netz, M, [a, b, c, d], -0.05, st["Trachyt"])
    return M


def dachgiebel(netz, st, mitte, z_traufe, halb_dach, tan, richtung, stoff="Tuff", oculus=True, ueber=0.3):
    """Giebel unter einem Satteldach (Wandoberkante z_traufe, halbe Breite halb_dach, Neigung tan): Das Dreieck reicht `ueber` Meter
    unter den Dachüberstand und liegt mit seinen Schrägen 0,1 m über der Unterseite des Dachs, also immer im Dach (sonst schaut der helle
    Stein zwischen den Schieferreihen hervor). `mitte` = (x, y) der Giebelfläche."""
    return giebel(netz, st, (mitte[0], mitte[1], z_traufe - ueber * tan), halb_dach + ueber, (halb_dach + ueber) * tan + 0.1, richtung,
                  stoff=stoff, oculus=oculus)


def pult(netz, st, x_in, x_aus, y0, y1, z_in, z_aus, stoff="Schiefer"):
    """Pultdach eines Seitenschiffs von der Mittelschiffwand (x_in, z_in) zur Traufe (x_aus, z_aus)."""
    s = 1 if x_aus > x_in else -1
    netz.vieleck_nach([(x_in, y0, z_in), (x_in, y1, z_in), (x_aus, y1, z_aus), (x_aus, y0, z_aus)], (s * 0.5, 0, 1), st[stoff])
    netz.quader((x_aus, (y0 + y1) / 2, z_aus - 0.08), (0.12, y1 - y0, 0.16), st["Schieferkante"], ohne=("-z",))     # Traufkante


def basilika(netz, st, y0, y1, xm, xs, z_ms, z_ss, joche, neigung=45, westgiebel=True, portal=True, ostgiebel=False,
             seiten=(-1, 1), portal_breite=2.2, fenster_ss=True):
    """Dreischiffiges Langhaus von y0 (West) bis y1 (Ost). Mittelschiff ±xm bis z_ms, Seitenschiffe bis ±xs, Traufe z_ss."""
    L = y1 - y0
    ym = (y0 + y1) / 2
    # Mittelschiff
    for s in (-1, 1):
        M = wm((s * xm, ym, 0), 0 if s > 0 else 180)
        netz.quader((0, 0.4, z_ms / 2), (L, 0.8, z_ms), st["Tuff"], M, ohne=("-z",))
        fries(netz, st, M, L, z_ms - 0.15)
        gesims(netz, st, M, L, z_ms - 0.05)
        for i in range(joche):
            u = -L / 2 + L * (i + 0.5) / joche
            uu = u if s > 0 else -u
            rundfenster(netz, st, M, uu, z_ss + 1.9, 0.9, 2.0)
        for i in range(joche + 1):
            u = -L / 2 + L * i / joche
            lisene(netz, st, M, u, z_ss + 1.4, z_ms - 0.55, breite=0.4)
    # Seitenschiffe
    zt = z_ss + (xs - xm) * math.tan(math.radians(24))
    for s in seiten:
        M = wm((s * xs, ym, 0), 0 if s > 0 else 180)
        netz.quader((0, 0.35, z_ss / 2), (L, 0.7, z_ss), st["Tuff"], M, ohne=("-z",))
        sockel(netz, st, M, L)
        fries(netz, st, M, L, z_ss - 0.12)
        for i in range(joche):
            u = -L / 2 + L * (i + 0.5) / joche
            if fenster_ss:
                rundfenster(netz, st, M, u, 2.0, 1.0, 2.3)
        for i in range(joche + 1):
            lisene(netz, st, M, -L / 2 + L * i / joche, 0.7, z_ss - 0.55)
        pult(netz, st, s * xm, s * (xs + 0.35), y0 - 0.1, y1 + 0.1, zt, z_ss - 0.1)
        # Stirnwände der Seitenschiffe
        for y, richtung in ((y0, "vorne"), (y1, "hinten")):
            if (richtung == "hinten") and not ostgiebel:
                continue
            Mw = bs.wandmatrix((s * (xm + xs - 0.7) / 2, y + (0.35 if richtung == "vorne" else -0.35), 0), richtung)
            hb = (xs - 0.7 - xm) / 2
            bs.platte(netz, Mw, [(-hb, 0), (hb, 0), (hb, z_ss if s * (1 if richtung == "vorne" else -1) > 0 else zt),
                                 (-hb, zt if s * (1 if richtung == "vorne" else -1) > 0 else z_ss)], -0.35, 0.35, st["Tuff"])
    # Dach und Giebel
    d = satteldach_y(netz, st, y0, y1, xm, z_ms, neigung)
    if westgiebel:
        dachgiebel(netz, st, (0, y0 + 0.3), z_ms, xm, d["tan"], "vorne")
        Mf = bs.wandmatrix((0, y0, 0), "vorne")
        fries(netz, st, Mf, 2 * xm, z_ms - 0.15)
        if portal:
            portal_rund(netz, st, Mf, 0.0, portal_breite, portal_breite / 2 + 2.3)
            rundfenster(netz, st, Mf, -1.2, 5.6, 0.9, 2.2)
            rundfenster(netz, st, Mf, 1.2, 5.6, 0.9, 2.2)
            rundfenster(netz, st, Mf, 0.0, z_ms - 3.2, 1.0, 2.2)
    if ostgiebel:
        dachgiebel(netz, st, (0, y1 - 0.3), z_ms, xm, d["tan"], "hinten")
    return d


def portal_rund(netz, st, M, u, b, h, stufen=3):
    """Gestuftes Rundbogenportal: Tür aus Holz mit Eisenbändern, Bogenfeld, Gewändestufen, Stufe davor."""
    flach(netz, M, rundbogen(u, 0.0, b, h, 6), -0.02, st["Holz alt"])
    k = h - b / 2
    flach(netz, M, [(u + b / 2 * math.cos(math.pi * i / 6), k + b / 2 * math.sin(math.pi * i / 6)) for i in range(7)], -0.03, st["Trachyt"])
    netz.quader((u, -0.05, k), (b + 0.05, 0.1, 0.18), st["Trachyt"], M, ohne=("+y",))
    for f in (0.25, 0.7):
        netz.quader((u, -0.045, k * f), (b * 0.85, 0.02, 0.08), st["Eisen"], M, ohne=("+y",))
    g = 0.24
    for i in range(stufen):
        bs.platte(netz, M, ring(u, 0.0, b + 2 * g * i, h + g * i, g, 6), -0.12 - 0.12 * i, 0.0, st["Trachyt" if i % 2 == 0 else "Tuff"])
    netz.quader((u, -0.3, 0.08), (b + 2 * g * stufen + 0.8, 0.6, 0.16), st["Trachyt"], M, ohne=("+y", "-z"))


# ─────────────────────────────────────────────────────────────────────────────
#  Türme
# ─────────────────────────────────────────────────────────────────────────────

def seiten4(cx, cy, a, z=0.0):
    """Die vier Wandsysteme eines Viereckturms (halbe Seite a) mit ihrer Richtung."""
    return [(wm((cx + a * math.cos(math.radians(w)), cy + a * math.sin(math.radians(w)), z), w), w) for w in (0, 90, 180, 270)]


def band4(netz, st, cx, cy, a, z, h=0.25, vor=0.12, stoff="Trachyt"):
    """Gesimsband rund um einen Viereckturm."""
    for M, w in seiten4(cx, cy, a):
        lang = 2 * a + 2 * vor if w in (0, 180) else 2 * a - 2 * vor     # keine doppelten Flächen an den Ecken
        netz.quader((0, 0.0, z), (lang, 2 * vor, h), st[stoff], M, ohne=("+y",))


def turm4(netz, st, cx, cy, a, z0, z1, geschosse=(), schall=(), fenster=(), blenden=(), dach="pyramide", dach_h=None,
          ecklisenen=True, stoff="Tuff", fries_oben=True):
    """Viereckturm. `geschosse`: Höhen der Gesimsbänder; `schall`: [(v, b, h, anzahl)] Schallarkaden je Seite;
    `fenster`: [(v, b, h, anzahl)] kleine Rundbogenfenster je Seite; `blenden`: [(v, b, h, anzahl)] Blendbögen.
    `dach`: pyramide, rhomben (Rautendach), falt2 (Faltdach mit zwei Giebeln je Seite), knick (Knickhelm). Gibt die Höhe der Spitze zurück."""
    netz.quader((cx, cy, (z0 + z1) / 2), (2 * a, 2 * a, z1 - z0), st[stoff], ohne=("-z",))
    for z in geschosse:
        band4(netz, st, cx, cy, a, z)
    for M, w in seiten4(cx, cy, a):
        if ecklisenen:
            for s in (-1, 1):
                lisene(netz, st, M, s * (a - 0.25), max(z0, 0.8), z1 - 0.6, breite=0.5, vor=0.12)
        for liste, fn in ((schall, schallarkade), (fenster, rundfenster), (blenden, blende)):
            for v, b, h, anzahl in liste:
                for i in range(anzahl):
                    u = -a + 2 * a * (i + 0.5) / anzahl
                    if fn is rundfenster:
                        rundfenster(netz, st, M, u, v, b, h, glas="Glas")
                    else:
                        fn(netz, st, M, u, v, b, h)
        if fries_oben:
            fries(netz, st, M, 2 * a, z1 - 0.12, bogen=0.8)
            for z in geschosse:
                fries(netz, st, M, 2 * a, z - 0.14, bogen=0.8, hoehe=0.36)
    band4(netz, st, cx, cy, a, z1 - 0.05, h=0.22, vor=0.16)
    hoch = dach_h if dach_h is not None else 2.6 * a
    if dach == "pyramide":
        bs.turmhelm(netz, st, (cx, cy, 0), a * math.sqrt(2) + 0.3, z1, hoch, stoff="Schiefer", n=4)
        return z1 + hoch
    if dach == "rhomben":
        return rhombendach(netz, st, cx, cy, a, z1, hoch / 2)
    if dach == "falt2":
        b = a + 0.25
        pts = [(cx + b, cy - b), (cx + b, cy), (cx + b, cy + b), (cx, cy + b), (cx - b, cy + b), (cx - b, cy), (cx - b, cy - b), (cx, cy - b)]
        return faltdach(netz, st, pts, (cx, cy), z1, b * 0.9, hoch)
    if dach == "knick":
        return knickhelm(netz, st, cx, cy, a, z1, hoch * 0.22, hoch * 0.78)
    raise ValueError(f"turm4: Dach „{dach}“ gibt es nicht")


def knickhelm(netz, st, cx, cy, a, z1, h1, h2, stoff="Schiefer"):
    """Knickhelm: flacher Pyramidenstumpf über dem Viereck, darüber ein steiler Achteckhelm."""
    b = a + 0.3
    r8 = a * 0.62
    ecken = [Vector((cx + sx * b, cy + sy * b, z1)) for sx, sy in ((1, -1), (1, 1), (-1, 1), (-1, -1))]
    oben = [Vector((cx + r8 * math.cos(math.radians(22.5 + 45 * k)), cy + r8 * math.sin(math.radians(22.5 + 45 * k)), z1 + h1)) for k in range(8)]
    c = Vector((cx, cy, z1))
    for i in range(4):
        e0, e1 = ecken[i], ecken[(i + 1) % 4]
        o_links = oben[(2 * i + 7) % 8]
        o_rechts = oben[(2 * i) % 8]
        nrm = ((e0 + e1) / 2 - c) + Vector((0, 0, 1.5))
        netz.vieleck_nach([e0, e1, o_rechts, o_links], nrm, st[stoff])
        netz.vieleck_nach([e1, oben[(2 * i + 1) % 8], o_rechts], (e1 - c) + Vector((0, 0, 1.5)), st[stoff])
    bs.turmhelm(netz, st, (cx, cy, 0), r8, z1 + h1, h2, stoff=stoff, n=8)
    return z1 + h1 + h2


def rhombendach(netz, st, cx, cy, a, z0, hg, stoff="Schiefer"):
    """Rheinischer Rautenhelm: vier Giebel, vier Rautenflächen zur Spitze (eben, wenn die Spitze doppelt so hoch wie die Giebel ist)."""
    a2 = a + 0.25
    ecke = [Vector((cx + sx * a2, cy + sy * a2, z0)) for sx, sy in ((1, -1), (1, 1), (-1, 1), (-1, -1))]
    spitzen = [Vector((cx + a2, cy, z0 + hg)), Vector((cx, cy + a2, z0 + hg)), Vector((cx - a2, cy, z0 + hg)), Vector((cx, cy - a2, z0 + hg))]
    apex = Vector((cx, cy, z0 + 2 * hg))
    for w in (0, 90, 180, 270):                         # Giebelwände (Tuff) mit kleiner Luke
        M = wm((cx + a * math.cos(math.radians(w)), cy + a * math.sin(math.radians(w)), z0), w)
        bs.platte(netz, M, [(-a, 0), (a, 0), (0, hg - 0.05)], 0.0, 0.4, st["Tuff"])
        flach(netz, M, rundbogen(0, hg * 0.25, 0.5, 0.9), -0.01, st["Loch"])
    for i in range(4):
        p = spitzen[i]
        q = spitzen[(i + 1) % 4]
        c = ecke[(i + 1) % 4]
        n = (c - Vector((cx, cy, z0))) + Vector((0, 0, 1.2 * a))
        netz.vieleck_nach([p, c, q, apex], n, st[stoff])
    knauf_und_kreuz(netz, st, apex)
    return z0 + 2 * hg


def turm8(netz, st, cx, cy, r, z0, z1, geschosse=(), schall=None, fenster=None, dach="helm", dach_h=None, stoff="Tuff"):
    """Achteckturm (r = Umkreis). schall/fenster: (v, b, h) je Seite. `dach`: helm, falt (Faltdach), kegel (flaches Kegeldach)."""
    bs.achteck(netz, st, (cx, cy, 0), r, z0, z1, stoff)
    ap = r * math.cos(math.radians(22.5))
    L = 2 * r * math.sin(math.radians(22.5))
    for z in geschosse:
        bs.achteck(netz, st, (cx, cy, 0), r + 0.12, z - 0.11, z + 0.11, "Trachyt")
    bs.achteck(netz, st, (cx, cy, 0), r + 0.16, z1 - 0.2, z1, "Trachyt")
    for k in range(8):
        w = 45 * k
        M = wm((cx + ap * math.cos(math.radians(w)), cy + ap * math.sin(math.radians(w)), 0), w)
        if schall:
            v, b, h = schall
            if b > 0.9:
                schallarkade(netz, st, M, 0, v, min(b, L - 0.4), h)
            else:
                flach(netz, M, rundbogen(0, v, b, h), -0.01, st["Loch"])
                flach(netz, M, ring(0, v, b, h, 0.1), -0.04, st["Trachyt"])
        if fenster:
            v, b, h = fenster
            rundfenster(netz, st, M, 0, v, b, h, glas="Glas", g=0.1)
        if L > 1.2:
            fries(netz, st, M, L, z1 - 0.22, bogen=0.7, hoehe=0.3)
    hoch = dach_h if dach_h is not None else 3.0 * r
    ecken = [(cx + (r + 0.2) * math.cos(math.radians(22.5 + 45 * k)), cy + (r + 0.2) * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    if dach == "helm":
        bs.turmhelm(netz, st, (cx, cy, 0), r + 0.18, z1, hoch, stoff="Schiefer", n=8)
    elif dach == "falt":
        faltdach(netz, st, ecken, (cx, cy), z1, hoch * 0.3, hoch)
    elif dach == "kegel":
        kegelsegmente(netz, st, ecken + [ecken[0]], (cx, cy, z1 + hoch), z1)
    else:
        raise ValueError(f"turm8: Dach „{dach}“ gibt es nicht")
    return z1 + hoch


def faltdach(netz, st, ecken, mitte, z0, hg, hs, giebel_stoff="Tuff", stoff="Schiefer"):
    """Faltdach über einem Vieleck (Ecken gegen den Uhrzeigersinn): über jeder Seite ein Giebel (Höhe hg), dazwischen
    Kehlen zu den Ecken, Spitze in der Mitte auf hs."""
    n = len(ecken)
    c = Vector((mitte[0], mitte[1], z0))
    apex = c + Vector((0, 0, hs))
    peaks = []
    for i in range(n):
        p, q = Vector((*ecken[i], z0)), Vector((*ecken[(i + 1) % n], z0))
        m = (p + q) / 2
        peak = m + Vector((0, 0, hg))
        peaks.append(peak)
        aussen_ = Vector((m.x - c.x, m.y - c.y, 0)).normalized()
        netz.vieleck_nach([p, q, peak], aussen_, st[giebel_stoff])
    for i in range(n):
        e = Vector((*ecken[(i + 1) % n], z0))
        p, q = peaks[i], peaks[(i + 1) % n]
        nrm = Vector((e.x - c.x, e.y - c.y, 0)).normalized() + Vector((0, 0, 0.8))
        netz.vieleck_nach([p, e, q], nrm, st[stoff])
        netz.vieleck_nach([p, q, apex], (p + q) / 2 - c + Vector((0, 0, 1.5)), st[stoff])
    bs.achteck(netz, st, apex - Vector((0, 0, 0.3)), 0.12, 0.0, 0.25, "Gold")
    netz.stab(apex - Vector((0, 0, 0.1)), apex + Vector((0, 0, 0.9)), 0.05, 0.05, st["Eisen"])
    return apex.z


# ─────────────────────────────────────────────────────────────────────────────
#  Zentralbau (Zehneck)
# ─────────────────────────────────────────────────────────────────────────────

def zehneck(netz, st, mitte, rx, ry, z_traufe, fenster_unten=(3.0, 1.2, 3.0), fenster_oben=(8.5, 1.3, 3.4), galerie=True,
            strebe=True, dach="falt", dach_h=None, n=10, dach_stoff="Schiefer"):
    """Ovaler Zentralbau aus n Seiten (Halbachsen rx, ry) mit Strebepfeilern, zwei Fensterreihen, Zwerggalerie; Dach falt oder kegel (Zeltdach)."""
    pts = [(mitte[0] + rx * math.cos(math.radians(90 + 360 * k / n)), mitte[1] + ry * math.sin(math.radians(90 + 360 * k / n))) for k in range(n)]
    kanten = mauerzug(netz, st, pts, 0.0, z_traufe, 0.9, "Tuff", geschlossen=True)
    for M, L, phi in kanten:
        if fenster_unten:
            v, b, h = fenster_unten
            rundfenster(netz, st, M, 0, v, b, h)
        if fenster_oben:
            v, b, h = fenster_oben
            rundfenster(netz, st, M, 0, v, b, h)
        gesims(netz, st, M, L, 6.6, vor=0.12)
        if galerie:
            zwerggalerie(netz, st, M, L - 0.6, z_traufe - 1.65, z_traufe - 0.45)
        gesims(netz, st, M, L, z_traufe - 0.1)
    if strebe:
        for k, p in enumerate(pts):
            w = math.degrees(math.atan2((p[1] - mitte[1]) / ry, (p[0] - mitte[0]) / rx))
            M = wm((p[0], p[1], 0), w)
            netz.quader((0, -0.45, 3.6), (0.9, 0.9, 7.2), st["Tuff"], M, ohne=("+y", "-z"))
            netz.quader((0, -0.3, 7.2 + (z_traufe - 9.5) / 2), (0.7, 0.6, z_traufe - 9.5 + 0.2), st["Tuff"], M, ohne=("+y", "-z"))
            for z, t in ((7.2, 0.9), (z_traufe - 2.2, 0.6)):                  # Pultkappen
                kap = Matrix.Translation(M @ Vector((0, -t / 2 - 0.05, z + 0.25))) @ Matrix.Rotation(math.radians(w + 90), 4, "Z") @ Matrix.Rotation(math.radians(-30), 4, "X")
                netz.quader((0, 0, 0), (0.95 if t > 0.8 else 0.75, t + 0.15, 0.12), st["Trachyt"], kap)
    hoch = dach_h if dach_h is not None else 0.9 * max(rx, ry)
    ecken = [(mitte[0] + (rx + 0.35) * math.cos(math.radians(90 + 360 * k / n)), mitte[1] + (ry + 0.35) * math.sin(math.radians(90 + 360 * k / n))) for k in range(n)]
    if dach == "falt":
        return faltdach(netz, st, ecken, mitte, z_traufe, hoch * 0.35, hoch)
    kegelsegmente(netz, st, ecken + [ecken[0]], (mitte[0], mitte[1], z_traufe + hoch), z_traufe, stoff=dach_stoff)
    knauf_und_kreuz(netz, st, Vector((mitte[0], mitte[1], z_traufe + hoch)), gross=True)
    return z_traufe + hoch


def spitzfenster(netz, st, M, u, v, b, h, glas="Fenster", pfosten=1, stein="Trachyt"):
    """Gotisches Spitzbogenfenster mit Pfosten und Sohlbank (Gewände wie am Dom)."""
    bs.platte(netz, M, dom.spitzbogen(u, v, b, h, 4), -0.012, -0.002, st[glas])
    dom.spitzring(netz, M, u, v, b, h, 0.14, -0.08, -0.004, st[stein])
    k = v + h - b * math.sin(math.radians(60))
    for i in range(1, pfosten + 1):
        uu = u - b / 2 + b * i / (pfosten + 1)
        netz.quader((uu, -0.04, (v + k) / 2 + 0.2), (0.09, 0.08, k - v + 0.4), st[stein], M, ohne=("+y",))
    netz.quader((u, -0.07, v - 0.07), (b + 0.4, 0.14, 0.14), st[stein], M, ohne=("+y",))


# ─────────────────────────────────────────────────────────────────────────────
#  Baustelle
# ─────────────────────────────────────────────────────────────────────────────

def geruest(netz, st, x0, x1, y0, y1, z0, z1, ebene=2.0):
    """Baugerüst um ein Rechteck: Pfosten etwa alle 2,2 m, Riegel je Ebene, Andreaskreuze an der Vorderseite (−Y)."""
    holz = st["Geruestholz"]
    xs = [x0 + (x1 - x0) * i / max(1, round((x1 - x0) / 2.2)) for i in range(round((x1 - x0) / 2.2) + 1)]
    ys = [y0 + (y1 - y0) * i / max(1, round((y1 - y0) / 2.2)) for i in range(round((y1 - y0) / 2.2) + 1)]
    pfosten = [(x, y0) for x in xs] + [(x, y1) for x in xs] + [(x0, y) for y in ys[1:-1]] + [(x1, y) for y in ys[1:-1]]
    for x, y in pfosten:
        netz.stab((x, y, z0), (x, y, z1), 0.12, 0.12, holz)
    z = z0 + ebene
    while z < z1 - 0.3:
        for y in (y0, y1):
            netz.stab((x0 - 0.15, y, z), (x1 + 0.15, y, z), 0.09, 0.09, holz)
        for x in (x0, x1):
            netz.stab((x, y0 - 0.15, z), (x, y1 + 0.15, z), 0.09, 0.09, holz, quer=(1, 0, 0))
        z += ebene
    for k in range(len(xs) - 1):                           # Andreaskreuze an der Vorderseite
        netz.stab((xs[k], y0 - 0.05, z0 + 0.3), (xs[k + 1], y0 - 0.05, z0 + 2 * ebene), 0.07, 0.06, holz, quer=(0, 1, 0))
