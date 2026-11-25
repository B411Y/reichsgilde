"""Neubau Dom evangelisch, Huelle aussen nach dem Artwork des Nutzers (24.11.2026): verschachtelte Backsteinkirche mit Sandsteinsockel,
-ecken und -gewaenden. Langhaus mit Sandsteingiebel und Vorhalle vorn, dahinter ein Achteck mit Zeltdach und Gauben, dahinter der Chor
mit 5/8-Schluss, zwei Treppentuermchen mit Kegeldach, ein runder Turm (16 Seiten) mit offener Glockenstube aus Holz und gerippter
Kupferhaube links vor der Westwand. Masse in dom_evangelisch_masse.py, Bausteine in dom_bib.py (wie der Dom katholisch).

    blender -b --python tools/modelle/neubau/dom_evangelisch_aussen.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/dom_evangelisch.glb mit den Knoten
    Haus      Mauern, Strebepfeiler, Turm, Tuermchen, Vorhalle, Gauben, Masswerk (keine Turmuhr)
    Dach      Schiefer in Deckreihen (D.schieferflaeche) mit Alpha-Cards (Schieferkante)
    Scheiben  Fenster (Fensterlicht) und Bleinetz; die Innenansicht blendet sie aus
    Anker_Tuer, Anker_Laterne
und die gebackene Verdeckung in game/assets/texturen/neubau/dom_ao_atlas.png (Gruppe "dom", Platz dom_evangelisch in ao_atlas.json).
Blatt dom_evangelisch_trim (dom_trimsheet.py): Backstein im Band "mauer". Style-Guide: Monument 8.000-15.000 Dreiecke.
"""
import math
import os
import sys

import bpy
from mathutils import Vector as V

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rathaus_bib as B                                  # noqa: E402
import dom_bib as D                                      # noqa: E402
import dom_evangelisch_masse as F                         # noqa: E402
from rathaus_bib import EZ, mauer                        # noqa: E402

BA = D.BAENDER_DOM
MODELL = "dom_evangelisch"
BLATT = "dom_evangelisch_trim"
QUADER = lambda ph: {"links": ("Sandstein", "sandstein", ph), "rechts": ("Sandstein", "sandstein", ph + 1)}
LAIBUNG = ("Sandstein", "werkstein")
ZS = F.Z_STEIN
XA = F.XA


def W(x, y):
    return (XA + x, y)


def wand(p0, p1):
    """Ursprung, Richtung, Laenge, Normale (nach aussen bei Umlauf gegen den Uhrzeigersinn) einer Wand von p0 nach p1."""
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    l = math.hypot(dx, dy)
    return p0, (dx / l, dy / l), l, (dy / l, -dx / l)


def punkt(o, e, n, s, z, raus=0.0):
    return V((o[0] + e[0] * s + n[0] * raus, o[1] + e[1] * s + n[1] * raus, z))


def fenster(h, sc, o, e, n, loecher, d):
    """Scheiben, Gewaende und Masswerk aller Oeffnungen einer Wand (Scheibe mittig in der Wand)."""
    n3 = V((n[0], n[1], 0))
    P = lambda s_, z, raus=0.0: punkt(o, e, n, s_, z, raus)
    for loch in loecher:
        zu = P((loch.s0 + loch.s1) / 2, (loch.z0 + loch.z1) / 2, 5)
        D.scheibe(sc, "Fenster", lambda a, z: P(a, z, -d / 2), loch, zu)
        D.scheibe(sc, "Bleinetz", lambda a, z: P(a, z, -d / 2 + 0.006), loch, zu)
        D.gewaende(h, P, n3, loch)
        if loch.s1 - loch.s0 > 0.9:
            D.masswerk(h, lambda a, z: P(a, z, -d / 2 + 0.07), n3, loch)


def verteilt(l, anzahl, breite, z0, z1, steil=1.5, mitte=None):
    """Spitzbogenfenster gleichmaessig auf einer Wand der Laenge l."""
    mitte = mitte or [l * (i + 0.5) / anzahl for i in range(anzahl)]
    return [D.Spitzloch(m - breite / 2, m + breite / 2, z0, z1, steil) for m in mitte]


def paar(l, breite, z0, z1, steil=1.6, abstand=0.30):
    """Zwei Spitzbogenfenster nebeneinander in der Wandmitte (Achteck)."""
    return [D.Spitzloch(l / 2 + s * (abstand + breite / 2) - breite / 2, l / 2 + s * (abstand + breite / 2) + breite / 2, z0, z1, steil) for s in (-1, 1)]


def wandbau(h, sc, p0, p1, z_traufe, loecher=(), ph=0, ganz_sandstein=False, d=0.7, z_stein=None, ecken=True, gesims=True, saat=1):
    """Eine Aussenwand: Sandsteinsockel, Backstein (oder Sandstein), Fenster, Sockel- und Traufgesims."""
    zs = ZS if z_stein is None else z_stein
    o, e, l, n = wand(p0, p1)
    stoff, band = ("Sandstein", "sandstein") if ganz_sandstein else ("Bruchstein", "mauer")
    if zs > 0:
        mauer(h, "Sandstein", o, e, n, l, 0.0, zs, "sandstein", saat=saat)
    mauer(h, stoff, o, e, n, l, zs, z_traufe, band, list(loecher), laibung=LAIBUNG, dicke=d / 2, ecken=QUADER(ph) if ecken else None, saat=saat)
    fenster(h, sc, o, e, n, loecher, d)
    a, b = (o[0], o[1]), (o[0] + e[0] * l, o[1] + e[1] * l)
    if gesims:
        if zs > 0: D.gesimsband(h, a, b, n, zs - 0.02, hoehe=0.14, vor=0.06, ueber=0.06)
        D.gesimsband(h, a, b, n, z_traufe - 0.24, ueber=0.10)
    return o, e, l, n


def giebel(h, stoff, band, o, e, n, l, zt, zf, neigung, loecher=(), mitte_halb=None):
    """Giebelfeld ueber einer Wand der Laenge l von der Traufe zt bis unter den First zf; das Dach faellt beiderseits der Mitte."""
    halb = lambda z: (zf - z) / neigung
    zk = min(max(zf - l / 2 * neigung, zt), zf)
    if zk - zt > 1e-4:
        mauer(h, stoff, o, e, n, l, zt, zk, band, list(loecher), laibung=LAIBUNG, dicke=0.3, grenze=lambda z: (0.0, l))
    if zf - 0.05 - zk > 1e-4:
        mauer(h, stoff, o, e, n, l, zk, zf - 0.05, band, list(loecher), laibung=LAIBUNG, dicke=0.3, grenze=lambda z: (l / 2 - halb(z), l / 2 + halb(z)))


def pfeiler(h, fuss, raus, z1, breite=0.70, tiefe=0.90):
    """Strebepfeiler aus Backstein auf dem Sandsteinsockel, Wasserschlag aus Sandstein."""
    f = V((fuss[0], fuss[1], 0.0)); r = V((raus[0], raus[1], 0.0)).normalized(); q = V((-r.y, r.x, 0.0))
    D.block(h, "Sandstein", "sandstein", f - q * breite / 2, q, r, breite, tiefe, 0.0, ZS, ohne=("v-", "unten", "oben"))
    D.strebepfeiler(h, fuss, raus, breite, tiefe, ZS, z1, schraege=1.0, stoff="Bruchstein", band="mauer")


# ══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Baukoerper
# ══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def lochliste(name):
    return [D.loch_von(o) for o in F.OEFFNUNGEN if o[0] == name and o[5] != "portal"]      # das Portal sitzt aussen in der Vorhalle


def langhaus(h, sc):
    x0, x1 = -F.X_AUSSEN, F.X_AUSSEN
    y0, y1 = F.Y_WEST, F.Y_LANG1
    zt = F.Z_TRAUFE_L
    # Giebelwand (Sandstein) mit dem grossen Fenster ueber der Vorhalle und zwei kleinen
    lo = lochliste("west")
    o, e, l, n = wand(W(x0, y0), W(x1, y0))
    mauer(h, "Sandstein", o, e, n, l, 0.0, zt, "sandstein", lo, laibung=LAIBUNG, dicke=0.35, ecken=QUADER(0))
    giebel(h, "Sandstein", "sandstein", o, e, n, l, zt, F.Z_FIRST_L, F.NEIGUNG_L, lo)
    fenster(h, sc, o, e, n, lo, 0.7)
    # Seitenwaende (Backstein): +X und -X
    for (name, p0, p1, ph) in (("seite_r", W(x1, y0), W(x1, y1), 0), ("seite_l", W(x0, y1), W(x0, y0), 1)):
        wandbau(h, sc, p0, p1, zt, lochliste(name), ph=ph, ecken=True)
    # Strebepfeiler an den Seiten (Nahtstellen der Fenster)
    for sx in (-1, 1):
        for y in (y0 + 0.35, -10.55, -6.25, -1.95):
            pfeiler(h, (XA + sx * x1, y), (sx, 0), zt - 0.6)


def vorhalle(h, sc):
    cx = XA + F.VH_X
    hx = F.VH_HALB
    yw = F.Y_WEST
    yv = yw - F.VH_TIEFE
    zt, zf = F.VH_Z
    b, z0, z1 = F.PORTAL
    tief = yw - yv
    mauer(h, "Sandstein", (cx - hx, yw), (0, -1), (-1, 0), tief, 0.0, zt, "sandstein")
    mauer(h, "Sandstein", (cx + hx, yv), (0, 1), (1, 0), tief, 0.0, zt, "sandstein")
    for sx in (-1, 1):
        D.gesimsband(h, (cx + sx * hx, yv), (cx + sx * hx, yw), (sx, 0), zt - 0.20, hoehe=0.20, vor=0.10)
    bogen = D.Spitzloch(hx - b / 2, hx + b / 2, z0, zt, (zt - z1) / (b / 2), sohlbank=False)
    mauer(h, "Sandstein", (cx - hx, yv), (1, 0), (0, -1), 2 * hx, 0.0, zt, "sandstein", [bogen], laibung=LAIBUNG, dicke=0.45)
    giebel(h, "Sandstein", "sandstein", (cx - hx, yv), (1, 0), (0, -1), 2 * hx, zt, zf, 1.0)
    P = lambda s_, z, raus=0.0: V((cx - hx + s_, yv - raus, z))
    D.gewaende(h, P, V((0, -1, 0)), bogen, breite=0.20, vor=0.05)
    for seite in (-1, 1):
        B.bretttuer(h, "Bretter", "planken", "Bretter", "eisen", (cx + seite * b / 4, yv + 0.40, z0), (1, 0, 0), (0, -1, 0), b / 2 - 0.01, z1 - z0, bretter=4)
    v0, v1, mm = BA["werkstein"]
    kurs = (v1 - v0) * mm
    zs = sorted({z for z in bogen.hoehen() if z >= z1 - 1e-6} | {z1 + kurs * i for i in range(1, int((zt - z1) / kurs) + 1)})
    PB = lambda s_, z: V((cx - hx + s_, yv + 0.43, z))
    for za, zb in zip(zs[:-1], zs[1:]):
        if zb - za < 1e-5: continue
        (a0, a1), (b0, b1) = bogen.breite_bei(za + 1e-6), bogen.breite_bei(zb - 1e-6)
        lage = math.floor((za - z1) / kurs + 1e-6)
        vv = lambda z: v1 - (z - z1 - lage * kurs) / kurs * (v1 - v0)
        pts = [PB(a0, za), PB(a1, za), PB(b1, zb), PB(b0, zb)]
        uvs = [(a0 / mm + 0.3 * lage, vv(za)), (a1 / mm + 0.3 * lage, vv(za)), (b1 / mm + 0.3 * lage, vv(zb)), (b0 / mm + 0.3 * lage, vv(zb))]
        if b1 - b0 < 1e-4: pts, uvs = pts[:3], uvs[:3]
        h.vieleck("Sandstein", pts, uvs, zu=PB(hx, z1 + 0.5) - V((0, 5, 0)))
    D.gesimsband(h, (cx + b / 2, yv + 0.43), (cx - b / 2, yv + 0.43), (0, -1), z1 - 0.02, hoehe=0.16, vor=0.10)
    for i in range(3):
        tiefe = 0.6 + 0.35 * (2 - i)
        D.block(h, "Sandstein", "sandstein", (cx - 2.0, yv - tiefe), (1, 0), (0, 1), 4.0, tiefe, 0.0, 0.1 * (i + 1), ohne=("v+", "unten"))
    # kleines Fenster in der Seitenwand der Vorhalle (Artwork: Schlitz im Giebel)
    return yv


def achteck(h, sc):
    mx, my = W(*F.OKT_M)
    zt = F.Z_TRAUFE_O
    for k, (a, b, n) in enumerate(F.vieleck(mx, my, F.OKT_A, 8, 0.0)):
        wandbau(h, sc, a, b, zt, lochliste(f"okt_{k}"), ph=k % 2, d=0.7, saat=3 + k)


def chor(h, sc):
    mx, my = W(*F.CHOR_M)
    zt = F.Z_TRAUFE_C
    blind = {}
    for o in F.blendfenster(): blind.setdefault(o[0], []).append(D.loch_von(o))
    for name in ("chor_r", "chor_l"):
        p0, p1, _ = F.WAENDE[name]
        wandbau(h, sc, p0, p1, zt, blind[name], ph=0 if name == "chor_r" else 1, saat=20 + (name == "chor_l"))
    for k in (1, 2, 3):
        p0, p1, _ = F.WAENDE[f"chor_{k}"]
        wandbau(h, sc, p0, p1, zt, blind[f"chor_{k}"], ph=k % 2, saat=24 + k)
    # Pfeiler an den Ecken des Schlusses und an den Seitenwaenden
    r = F.CHOR_A / math.cos(math.pi / 8)
    for k in range(-1, 5):
        w = math.radians(22.5 + 45 * k)
        px, py = mx + r * math.cos(w), my + r * math.sin(w)
        pfeiler(h, (px - 0.05 * math.cos(w), py - 0.05 * math.sin(w)), (math.cos(w), math.sin(w)), zt - 0.6, breite=0.60)
    for sx in (-1, 1):
        pfeiler(h, (XA + sx * F.CHOR_A, F.CHOR_Y0 + 1.0), (sx, 0), zt - 0.6, breite=0.60)


def kegelturm(h, sc, mx, my, saat):
    """Treppentuermchen: Sandsteinfuss, runder Backsteinschaft mit Schlitzfenster; Kegeldach in dach()."""
    n = 12
    r = F.TM_R
    ecken = [(mx + r * math.cos(2 * math.pi * (k + 0.5) / n), my + r * math.sin(2 * math.pi * (k + 0.5) / n)) for k in range(n)]
    sockel = [(mx + (r + 0.10) * math.cos(2 * math.pi * (k + 0.5) / n), my + (r + 0.10) * math.sin(2 * math.pi * (k + 0.5) / n)) for k in range(n)]
    for k in range(n):
        k2 = (k + 1) % n
        w = 2 * math.pi * (k + 1) / n
        nn = (math.cos(w), math.sin(w))
        for ring, z0, z1, stoff, band in ((sockel, 0.0, ZS, "Sandstein", "sandstein"), (ecken, ZS, F.Z_TM_WAND, "Bruchstein", "mauer")):
            a, b = ring[k2], ring[k]
            l = math.hypot(b[0] - a[0], b[1] - a[1]); eu = ((b[0] - a[0]) / l, (b[1] - a[1]) / l)
            ls = []
            if band == "mauer" and k == 8:
                ls = [D.Spitzloch(l / 2 - 0.14, l / 2 + 0.14, 6.0, 7.2, 1.5)]
            if band == "mauer" and k == 10:
                ls = [D.Spitzloch(l / 2 - 0.14, l / 2 + 0.14, 8.6, 9.8, 1.5)]
            mauer(h, stoff, a, eu, nn, l, z0, z1, band, ls, laibung=LAIBUNG, dicke=0.25, saat=saat + k)
            for loch in ls:
                P = lambda s_, z, raus=0.0, a=a, eu=eu, nn=nn: V((a[0] + eu[0] * s_ + nn[0] * raus, a[1] + eu[1] * s_ + nn[1] * raus, z))
                D.gewaende(h, P, V((nn[0], nn[1], 0)), loch, breite=0.08)
                zu = P(l / 2, loch.z0, 5)
                D.scheibe(sc, "Fenster", lambda q, z, P=P: P(q, z, -0.25), loch, zu)
        # Gesims unter dem Dach
        D.gesimsband(h, ecken[k2], ecken[k], nn, F.Z_TM_WAND - 0.2, hoehe=0.2, vor=0.12, ueber=0.12 * math.tan(math.pi / n))


def turm_ecken(r):
    cx, cy = W(*F.TURM_M); n = F.TURM_SEITEN
    return [(cx + r * math.cos(2 * math.pi * (k + 0.5) / n), cy + r * math.sin(2 * math.pi * (k + 0.5) / n)) for k in range(n)]


def turm(h, s):
    """Runder Turm: Sandsteinsockel, Schaft aus Backstein mit Schlitzfenstern, Gesims, offene Glockenstube aus Holz, Kupferhaube."""
    cx, cy = W(*F.TURM_M); n_ = F.TURM_SEITEN
    zg0, zg1 = F.Z_GLOCKEN
    c = V((cx, cy, 0))
    sockel, schaft = turm_ecken(F.TURM_R + 0.12), turm_ecken(F.TURM_R)
    schlitze = {11: 6.8, 13: 10.6, 9: 14.4, 12: 15.2}      # Seite -> Bruestung eines Schlitzfensters
    wv0, wv1, wm = BA["werkstein"]
    lx, ly0 = XA - F.X_AUSSEN, F.Y_WEST
    for k in range(n_):
        k2 = (k + 1) % n_
        w = 2 * math.pi * (k + 1) / n_
        n = (math.cos(w), math.sin(w)); n3 = V((n[0], n[1], 0))
        mid = ((schaft[k][0] + schaft[k2][0]) / 2, (schaft[k][1] + schaft[k2][1]) / 2)
        drin = mid[0] > lx + 0.1 and mid[1] > ly0 + 0.1      # liegt im Langhaus: nie zu sehen
        if drin: continue
        for ecken, z0, z1, stoff, band in ((sockel, 0.0, F.Z_TURMSOCKEL, "Sandstein", "sandstein"), (schaft, F.Z_TURMSOCKEL, zg0, "Bruchstein", "mauer")):
            a, b = ecken[k2], ecken[k]
            l = math.hypot(b[0] - a[0], b[1] - a[1]); eu = ((b[0] - a[0]) / l, (b[1] - a[1]) / l)
            ls = [D.Spitzloch(l / 2 - 0.24, l / 2 + 0.24, schlitze[k], schlitze[k] + 1.5, 1.3)] if band == "mauer" and k in schlitze else []
            mauer(h, stoff, a, eu, n, l, z0, z1, band, ls, laibung=LAIBUNG, dicke=0.30, saat=k + 1)
            P = lambda s_, z, raus=0.0, a=a, eu=eu, n=n: V((a[0] + eu[0] * s_ + n[0] * raus, a[1] + eu[1] * s_ + n[1] * raus, z))
            for loch in ls:
                D.gewaende(h, P, n3, loch, breite=0.12)
                zu = P(l / 2, loch.z0, 5)
                D.scheibe(s, "Fenster", lambda q, z, P=P: P(q, z, -0.30), loch, zu)
                D.scheibe(s, "Bleinetz", lambda q, z, P=P: P(q, z, -0.294), loch, zu)
        pa, pb, qa, qb = V((*sockel[k2], F.Z_TURMSOCKEL)), V((*sockel[k], F.Z_TURMSOCKEL)), V((*schaft[k2], F.Z_TURMSOCKEL + 0.16)), V((*schaft[k], F.Z_TURMSOCKEL + 0.16))
        lang = (pb - pa).length
        h.vieleck("Sandstein", [pa, pb, qb, qa], [(0, wv1), (lang / wm, wv1), (lang / wm, wv1 - 0.2 / wm), (0, wv1 - 0.2 / wm)], zu=(pa + pb) / 2 + n3 * 3 + EZ * 3)
        D.gesimsband(h, schaft[k2], schaft[k], n, zg0 - 0.30, hoehe=0.30, vor=0.18, ueber=0.18 * math.tan(math.pi / n_))
        # Rundbogenfries unter dem Gesims der Glockenstube (Artwork): je Seite drei blinde Rundbogen, dunkel
        pa, pb = V((*schaft[k2], 0)), V((*schaft[k], 0))
        lf = (pb - pa).length; ef = (pb - pa).normalized()
        Pf = lambda s_, z, raus=0.025: pa + ef * s_ + n3 * raus + EZ * z
        for i in range(3):
            sm, rr = lf * (i + 0.5) / 3, 0.20
            zb, zsp = zg0 - 0.30 - 0.62, zg0 - 0.30 - 0.30
            pts = [Pf(sm - rr, zb), Pf(sm + rr, zb)] + [Pf(sm + rr * math.cos(math.pi * j / 6), zsp + rr * math.sin(math.pi * j / 6)) for j in range(7)]
            D.farbflaeche(h, "Sandstein", pts, "nacht", zu=Pf(lf / 2, zb, 5))
    # Glockenstube: Boden, Pfosten und Rahmen aus Eiche, Bruestung aus Brettern, dahinter der dunkle Raum
    rand, pfosten = turm_ecken(F.TURM_R + 0.18), turm_ecken(F.TURM_R - 0.16)
    zr = zg1 - 0.25
    for k in range(n_):
        k2 = (k + 1) % n_
        w = 2 * math.pi * (k + 1) / n_
        n3 = V((math.cos(w), math.sin(w), 0))
        D.farbflaeche(h, "Sandstein", [c + EZ * (zg0 + 0.001), V((*schaft[k], zg0)), V((*schaft[k2], zg0))], "holz_dunkel", zu=c + EZ * 60)
        x, y = pfosten[k]
        h.stab("Fachwerk", (x, y, zg0), (x, y, zr), 0.22, "balken")
        a, b = V((*pfosten[k2], 0)), V((*pfosten[k], 0))
        h.stab("Fachwerk", a + EZ * (zr + 0.115), b + EZ * (zr + 0.115), 0.23, "balken", breite=0.22)
        e = (b - a).normalized()
        h.rechteck("Bretter", a + e * 0.11 + EZ * zg0, e, EZ, (b - a).length - 0.22, 0.95, "planken", zu=(a + b) / 2 + n3 * 5 + EZ * zg0)
        h.stab("Fachwerk", a + e * 0.11 + EZ * (zg0 + 1.0), b - e * 0.11 + EZ * (zg0 + 1.0), 0.10, "balken", breite=0.14)
        ia, ib = c + (V((*pfosten[k2], 0)) - c) * 0.8, c + (V((*pfosten[k], 0)) - c) * 0.8
        ha, hb = c + (V((*rand[k2], 0)) - c) * 1.09, c + (V((*rand[k], 0)) - c) * 1.09
        D.farbflaeche(h, "Sandstein", [ia + EZ * zg1, ib + EZ * zg1, hb + EZ * zg1, ha + EZ * zg1], "holz_dunkel", zu=c)
    h.dreh("Sandstein", (cx, cy, 0), [(F.TURM_R - 0.55, zg0), (F.TURM_R - 0.55, zg1)], "kleinteile", seiten=n_, u_bereich=D.feld("nacht"), deckel=False, glatt=False)
    # Kupferhaube (welsche Haube): ausschwingende Traufe, Bauch, Einzug, Spitze
    r = F.TURM_R
    zh, zsp = F.Z_HAUBE, F.Z_SPITZE
    t = lambda anteil: zh + (zsp - zh) * anteil
    profil = [(r + 0.60, zh), (r + 0.32, t(0.035)), (r + 0.16, t(0.10)), (r + 0.06, t(0.22)), (r - 0.30, t(0.36)), (r - 0.95, t(0.48)), (r - 1.70, t(0.57)),
              (0.60, t(0.64)), (0.30, t(0.76)), (0.13, t(0.90)), (0.0, zsp)]
    h.dreh("Firstblech", (cx, cy, 0), profil, "kleinteile", seiten=n_, u_bereich=D.feld("kupfer"), deckel=False, glatt=False)
    D.farbstab(h, "Firstblech", (cx, cy, zsp - 0.5), (cx, cy, F.Z_KNAUF - 0.12), 0.07, "gold")
    D.farbkasten(h, "Firstblech", (cx - 0.15, cy - 0.15, F.Z_KNAUF - 0.24), (cx + 0.15, cy + 0.15, F.Z_KNAUF + 0.06), "gold")


def baue_haus(sc):
    h = D.Teil("Haus", BA, saat=3)
    langhaus(h, sc)
    yv = vorhalle(h, sc)
    achteck(h, sc)
    chor(h, sc)
    for i, (x, y) in enumerate(F.TUERMCHEN):
        kegelturm(h, sc, *W(x, y), saat=5 + 3 * i)
    turm(h, sc)
    h.yv = yv
    return h


# ══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Dach
# ══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def gaube(d, h, sc, mx, my, theta, rho_f, saat):
    """Gaube auf einer Dachflaeche des Achtecks (Flaechennormale theta): Backsteinfront mit Spitzbogenfenster, Giebel, zwei
    Schieferflaechen mit waagerechtem First, Wangen aus Schiefer. rho_f: Abstand der Frontwand von der Achse."""
    zt, zsp, a = F.Z_TRAUFE_O, F.Z_SPITZE_O, F.OKT_A + 0.30
    k = (zsp - zt) / a                                           # Dachneigung (Hoehe je Meter)
    zr = lambda rho: zsp - k * rho
    r_ = V((math.cos(theta), math.sin(theta), 0)); t_ = V((-math.sin(theta), math.cos(theta), 0))
    c = V((mx, my, 0))
    w = 1.30
    zf = zr(rho_f)
    hw = 1.9
    ze = zf + hw
    zfi = ze + w / 2 * 1.1
    rho_e, rho_r = (zsp - ze) / k, (zsp - zfi) / k
    p = lambda rho, s_, z: c + r_ * rho + t_ * s_ + EZ * z
    o = (c + r_ * rho_f - t_ * (w / 2))
    eu = (t_.x, t_.y)
    nn = (r_.x, r_.y)
    loch = D.Spitzloch(w / 2 - 0.28, w / 2 + 0.28, 0.55, hw - 0.12, 1.7)
    mauer(h, "Bruchstein", (o.x, o.y), eu, nn, w, zf - 0.02, ze, "mauer", [loch], laibung=LAIBUNG, dicke=0.2, saat=saat)
    giebel(h, "Bruchstein", "mauer", (o.x, o.y), eu, nn, w, ze, zfi, (zfi - ze) / (w / 2))
    P = lambda s_, z, raus=0.0: V((o.x + eu[0] * s_ + nn[0] * raus, o.y + eu[1] * s_ + nn[1] * raus, z + zf))
    # Fenster relativ zur Unterkante der Wand
    loch2 = D.Spitzloch(loch.s0, loch.s1, loch.z0 - zf, loch.z1 - zf, 1.7)
    P0 = lambda s_, z, raus=0.0: V((o.x + eu[0] * s_ + nn[0] * raus, o.y + eu[1] * s_ + nn[1] * raus, z))
    zu = P0(w / 2, zf + 1.0, 5)
    D.scheibe(sc, "Fenster", lambda s_, z: P0(s_, z, -0.1), loch, zu)
    D.scheibe(sc, "Bleinetz", lambda s_, z: P0(s_, z, -0.094), loch, zu)
    D.gewaende(h, P0, V((nn[0], nn[1], 0)), loch, breite=0.09)
    # Dachflaechen der Gaube
    ue = 0.14
    for sg in (-1, 1):
        d_a0 = p(rho_f + ue, sg * (w / 2 + ue), ze - 0.05)
        d_a1 = p(rho_e, sg * (w / 2 + ue), ze - 0.05)
        d_b0 = p(rho_f + ue, 0.0, zfi + 0.05)
        d_b1 = p(rho_r, 0.0, zfi + 0.05)
        D.schieferflaeche(d, d_a0, d_a1, d_b0, d_b1, (d_a0 + d_b0) / 2 + V((sg * t_.x, sg * t_.y, 0)) * 3 + EZ * 3, saat=saat + sg + 2, karte=True)
        # Wange: Dreieck zwischen Wand und Hauptdach
        for sg2 in (sg,):
            pts = [p(rho_f, sg2 * w / 2, zf), p(rho_e, sg2 * w / 2, ze), p(rho_f, sg2 * w / 2, ze)]
            D.schieferflaeche(d, pts[0], pts[1], pts[2], pts[2], p(rho_f, sg2 * w / 2, ze) + t_ * sg2 * 4, stufe=0, karte=False, saat=saat)


def baue_dach():
    d = D.Teil("Dach", BA, saat=11)
    sch = D.schieferflaeche
    # ── Langhaus ──
    zt = F.Z_TRAUFE_L
    xe = F.X_AUSSEN + 0.30
    y0, y1 = (F.Y_WEST, F.Y_LANG1)
    for sx in (-1, 1):
        sch(d, (XA + sx * xe, y0, zt), (XA + sx * xe, y1 + 0.6, zt), (XA, y0, F.Z_FIRST_L), (XA, y1 + 0.6, F.Z_FIRST_L), (XA + sx * 40, 0, 60), saat=1 + sx)
    # ── Vorhalle (45 Grad) ──
    cx = XA + F.VH_X
    vx = F.VH_HALB + 0.25
    yv = F.Y_WEST - F.VH_TIEFE
    for sx in (-1, 1):
        sch(d, (cx + sx * vx, yv, F.VH_Z[0]), (cx + sx * vx, F.Y_WEST, F.VH_Z[0]), (cx, yv, F.VH_Z[1]), (cx, F.Y_WEST, F.VH_Z[1]), (cx + sx * 40, 0, 60), saat=8 + sx)
    # ── Achteck: Zeltdach ──
    mx, my = W(*F.OKT_M)
    spitze = (mx, my, F.Z_SPITZE_O)
    flaechen = F.vieleck(mx, my, F.OKT_A + 0.30, 8, 0.0)
    for k, (a, b, n) in enumerate(flaechen):
        sch(d, (*a, F.Z_TRAUFE_O), (*b, F.Z_TRAUFE_O), spitze, spitze, (mx + n[0] * 40, my + n[1] * 40, 60), saat=20 + k)
    D.farbstab(d, "Firstblech", (mx, my, F.Z_SPITZE_O - 0.3), (mx, my, F.Z_SPITZE_O + 1.6), 0.08, "bronze")
    # ── Chor ──
    cmx, cmy = W(*F.CHOR_M)
    zc = F.Z_TRAUFE_C
    xc = F.CHOR_A + 0.30
    ysp = cmy
    ye = cmy + xc * math.tan(math.pi / 8)
    ya = F.CHOR_Y0 - 0.2
    top = (XA, cmy, F.Z_FIRST_C)
    for sx in (-1, 1):
        sch(d, (XA + sx * xc, ya, zc), (XA + sx * xc, ye, zc), (XA, ya, F.Z_FIRST_C), (XA, cmy, F.Z_FIRST_C), (XA + sx * 40, cmy, 60), saat=40 + sx) if sx == -1 else \
            sch(d, (XA + sx * xc, ye, zc), (XA + sx * xc, ya, zc), (XA, cmy, F.Z_FIRST_C), (XA, ya, F.Z_FIRST_C), (XA + sx * 40, cmy, 60), saat=40 + sx)
    sf = F.vieleck(cmx, cmy, xc, 8, 0.0)
    for k in (1, 2, 3):
        a, b, n = sf[k]
        sch(d, (*a, zc), (*b, zc), top, top, (cmx + n[0] * 40, cmy + n[1] * 40, 60), saat=44 + k)
    return d


def baue_kegel(d, saat):
    n = 12
    for i, (x, y) in enumerate(F.TUERMCHEN):
        mx, my = W(x, y)
        r = F.TM_R + 0.25
        sp = (mx, my, F.Z_TM_SPITZE)
        for k in range(n):
            a = (mx + r * math.cos(2 * math.pi * k / n), my + r * math.sin(2 * math.pi * k / n))
            b = (mx + r * math.cos(2 * math.pi * (k + 1) / n), my + r * math.sin(2 * math.pi * (k + 1) / n))
            m = (mx + (a[0] + b[0] - 2 * mx) * 8, my + (a[1] + b[1] - 2 * my) * 8, 40)
            D.schieferflaeche(d, (*a, F.Z_TM_WAND - 0.2), (*b, F.Z_TM_WAND - 0.2), sp, sp, m, stufe=0, karte=False, saat=saat + k)
        D.farbstab(d, "Firstblech", (mx, my, F.Z_TM_SPITZE - 0.3), (mx, my, F.Z_TM_SPITZE + 1.1), 0.06, "eisen")


def stoffe():
    b = BLATT
    S = B.stoff
    return {
        "Schiefer": S("Schiefer", b), "Schieferkante": S("Schieferkante", b, schere=True), "Fachwerk": S("Fachwerk", b),
        "Sandstein": S("Sandstein", b), "Bruchstein": S("Bruchstein", b), "Bretter": S("Bretter", b), "Firstblech": S("Firstblech", b),
        "Fenster": S("Fenster", farbe=(0.09, 0.11, 0.14), rauheit=0.2), "Bleinetz": S("Bleinetz", farbe=(0.1, 0.1, 0.11), alpha=0.5),
    }


def main():
    opt = B.optionen()
    D.neu()
    st = stoffe()
    scheiben = D.Teil("Scheiben", BA, saat=5)
    haus = baue_haus(scheiben)
    dach = baue_dach()
    baue_kegel(dach, 60)
    mx, my = W(*F.OKT_M)
    for i, (theta, rho) in enumerate(((270, 4.9), (315, 4.9), (0, 4.9), (225, 4.9), (45, 4.9))):
        gaube(dach, haus, scheiben, mx, my, math.radians(theta), rho, 70 + 3 * i)
    summe = haus.dreiecke() + dach.dreiecke() + scheiben.dreiecke()
    print(f"DREIECKE aussen: Haus {haus.dreiecke()}, Dach {dach.dreiecke()}, Scheiben {scheiben.dreiecke()}, Summe {summe} (Budget {F.BUDGET[0]}-{F.BUDGET[1]})")
    o_haus, o_dach = haus.objekt(st), dach.objekt(st)
    o_scheiben = scheiben.objekt(st, mit_farbe=False)
    bpy.ops.mesh.primitive_plane_add(size=120, location=(0, 0, 0))
    boden = bpy.context.active_object; boden.name = "Kontrollboden"
    boden.data.materials.append(B.stoff("Kontrollboden", farbe=(0.36, 0.33, 0.28)))
    B.uv2_und_ao([([o_haus, o_dach], [boden])], "dom", MODELL, mit_ao="ohne-ao" not in opt)
    yv = haus.yv
    anker = [
        B.anker("Anker_Tuer", (XA + F.VH_X, yv - 2.2, 0.02), blick="nord"),
        B.anker("Anker_Laterne", (XA + F.VH_X + F.PORTAL[0] / 2 + 0.28, yv - 0.05, 2.6), blick="sued"),
    ]
    B.exportiere([o_haus, o_dach, o_scheiben] + anker, os.path.join(B.ZIEL_MODELL, MODELL + ".glb"))
    if "bilder" in opt:
        o = opt["bilder"]; os.makedirs(o, exist_ok=True)
        B.kontrollbild(os.path.join(o, "domev_blick_artwork.jpg"), (XA + 34.0, -46.0, 38.0), (XA - 1.0, 0.0, 9.0), brennweite=42)
        B.kontrollbild(os.path.join(o, "domev_blick_portal.jpg"), (XA - 10.0, -36.0, 6.0), (XA - 3.0, -14.0, 7.0), brennweite=26)
        B.kontrollbild(os.path.join(o, "domev_blick_chor.jpg"), (XA + 30.0, 40.0, 28.0), (XA, 4.0, 9.0), brennweite=40)


if __name__ == "__main__":
    main()
