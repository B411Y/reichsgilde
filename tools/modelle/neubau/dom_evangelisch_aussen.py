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


def wandbau(h, sc, p0, p1, z_traufe, loecher=(), ph=0, ganz_sandstein=False, d=0.7, z_stein=None, ecken=True, gesims=True, saat=1, ab=None, z_oben=None):
    """Eine Aussenwand: Sandsteinsockel, Backstein (oder Sandstein), Fenster, Sockel- und Traufgesims. `ab`: nur von dieser Hoehe an
    (Wand im Inneren des Langhauses, unten nicht zu sehen und von innen im Weg)."""
    zs = ZS if z_stein is None else z_stein
    if ab is not None: zs = ab
    o, e, l, n = wand(p0, p1)
    stoff, band = ("Sandstein", "sandstein") if ganz_sandstein else ("Bruchstein", "mauer")
    if zs > 0 and ab is None:
        mauer(h, "Sandstein", o, e, n, l, 0.0, zs, "sandstein", saat=saat)
    mauer(h, stoff, o, e, n, l, zs, z_oben or z_traufe, band, list(loecher), laibung=LAIBUNG, dicke=d / 2, ecken=QUADER(ph) if ecken else None, saat=saat)
    fenster(h, sc, o, e, n, loecher, d)
    a, b = (o[0], o[1]), (o[0] + e[0] * l, o[1] + e[1] * l)
    if gesims:
        if zs > 0 and ab is None: D.gesimsband(h, a, b, n, zs - 0.02, hoehe=0.14, vor=0.06, ueber=0.06)
        D.gesimsband(h, a, b, n, z_traufe - 0.24, ueber=0.10)
    return o, e, l, n


def giebel(h, stoff, band, o, e, n, l, zt, zf, neigung, loecher=(), mitte_halb=None):
    """Giebelfeld ueber einer Wand der Laenge l von der Traufe zt bis unter den First zf; das Dach faellt beiderseits der Mitte."""
    halb = lambda z: (zf - z) / neigung
    zk = min(max(zf - l / 2 * neigung, zt), zf)
    if zk - zt > 1e-4:
        mauer(h, stoff, o, e, n, l, zt, zk, band, list(loecher), laibung=LAIBUNG, dicke=0.3, grenze=lambda z: (0.0, l))
    if zf - 0.01 - zk > 1e-4:
        mauer(h, stoff, o, e, n, l, zk, zf - 0.01, band, list(loecher), laibung=LAIBUNG, dicke=0.3, grenze=lambda z: (l / 2 - halb(z), l / 2 + halb(z)))


def pfeiler(h, fuss, raus, zt, breite=0.70, tiefe=0.90):
    """Strebepfeiler aus Backstein auf dem Sandsteinsockel; enden 1,0 m unter der Traufe `zt` mit einem Wasserschlag aus Sandstein
    (Schraege 0,6 hoch), sodass nichts ins Dach ragt (Befund 25.11.2026)."""
    z1 = zt - 1.0
    f = V((fuss[0], fuss[1], 0.0)); r = V((raus[0], raus[1], 0.0)).normalized(); q = V((-r.y, r.x, 0.0))
    D.block(h, "Sandstein", "sandstein", f - q * breite / 2, q, r, breite, tiefe, 0.0, ZS, ohne=("v-", "unten", "oben"))
    D.strebepfeiler(h, fuss, raus, breite, tiefe, ZS, z1, schraege=0.6, stoff="Bruchstein", band="mauer")


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
        wandbau(h, sc, p0, p1, zt, lochliste(name), ph=ph, ecken=True, z_oben=zt + 0.30 * F.NEIGUNG_L - 0.02)     # bis an die Dachflaeche (Traufueberstand 0,30 m): sonst Schlitz unter dem Dach
    # Strebepfeiler an den Seiten (Nahtstellen der Fenster)
    for sx in (-1, 1):
        for y in (y0 + 0.35, -10.55, -6.25, -1.95):
            pfeiler(h, (XA + sx * x1, y), (sx, 0), zt)


def vorhalle(h, sc):
    cx = XA + F.VH_X
    hx = F.VH_HALB
    yw = F.Y_WEST
    yv = yw - F.VH_TIEFE
    zt, zf = F.VH_Z
    b, z0, z1 = F.PORTAL
    tief = yw - yv
    # Die Seitenwaende reichen bis an die Dachflaeche (Dach faellt von der Traufe bei hx + 0,25 zum First): sonst Schlitz zwischen Wand und Dach
    zw = zt + 0.25 * (zf - zt) / (hx + 0.25) - 0.02
    mauer(h, "Sandstein", (cx - hx, yw), (0, -1), (-1, 0), tief, 0.0, zw, "sandstein")
    mauer(h, "Sandstein", (cx + hx, yv), (0, 1), (1, 0), tief, 0.0, zw, "sandstein")
    for sx in (-1, 1):
        D.gesimsband(h, (cx + sx * hx, yv), (cx + sx * hx, yw), (sx, 0), zt - 0.20, hoehe=0.20, vor=0.10)
    bogen = D.Spitzloch(hx - b / 2, hx + b / 2, z0, zt, (zt - z1) / (b / 2), sohlbank=False)
    mauer(h, "Sandstein", (cx - hx, yv), (1, 0), (0, -1), 2 * hx, 0.0, zt, "sandstein", [bogen], laibung=LAIBUNG, dicke=0.45)
    giebel(h, "Sandstein", "sandstein", (cx - hx, yv), (1, 0), (0, -1), 2 * hx, zt, zf, 1.0)
    P = lambda s_, z, raus=0.0: V((cx - hx + s_, yv - raus, z))
    D.gewaende(h, P, V((0, -1, 0)), bogen, breite=0.20, vor=0.05)
    for seite in (-1, 1):
        B.bretttuer(h, "Bretter", "planken", "Bretter", "eisen", (cx + seite * b / 4, yv + 0.40, z0), (1, 0, 0), (0, -1, 0), b / 2 + 0.01, z1 - z0, bretter=4)
    D.block(h, "Sandstein", "sandstein", (cx - b / 2, yv - 0.30), (1, 0), (0, 1), b, 1.40, 0.0, z0 + 0.01, ohne=("unten",))     # Schwelle unter den Tuerblaettern (sonst Durchsicht unter der Tuer)
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
        # die Vorderseite des Achtecks (Flaechen 5, 6, 7) steht im Langhaus: unten weder zu sehen noch erwuenscht (von innen im Weg)
        if k in (5, 7):
            # die schraegen Flaechen schneiden die Langhauswand: der Teil ausserhalb (rund 1,5 m) steht in voller Hoehe, der Rest im Langhaus erst ab 10,8 m
            xg = XA + (-1 if k == 5 else 1) * (F.X_AUSSEN - 0.15)
            t = (xg - a[0]) / (b[0] - a[0])
            pm = (xg, a[1] + t * (b[1] - a[1]))
            aussen, innen = ((a, pm), (pm, b)) if k == 5 else ((pm, b), (a, pm))
            wandbau(h, sc, *aussen, zt, (), ph=k % 2, d=0.7, saat=3 + k)
            # im Langhaus folgt die Unterkante dem Dach (Stuecke von ~0,5 m), damit unter dem Dach keine Oeffnung bleibt
            (ia, ib) = innen
            roof = lambda x: F.Z_TRAUFE_L + max(0.0, F.X_AUSSEN + 0.30 - abs(x - XA)) * F.NEIGUNG_L
            nst = 5
            for i in range(nst):
                pa_ = (ia[0] + (ib[0] - ia[0]) * i / nst, ia[1] + (ib[1] - ia[1]) * i / nst)
                pb_ = (ia[0] + (ib[0] - ia[0]) * (i + 1) / nst, ia[1] + (ib[1] - ia[1]) * (i + 1) / nst)
                ab_ = min(roof(pa_[0]), roof(pb_[0])) - 0.15
                ln_ = math.hypot(ib[0] - ia[0], ib[1] - ia[1])
                ue_ = ((ib[0] - ia[0]) / ln_ * 0.04, (ib[1] - ia[1]) / ln_ * 0.04)          # Stuecke 4 cm ueberlappen: sonst Fugen zwischen den Stufen der Unterkante
                pa_ = (pa_[0] - ue_[0], pa_[1] - ue_[1]) if i > 0 else pa_
                pb_ = (pb_[0] + ue_[0], pb_[1] + ue_[1]) if i < nst - 1 else pb_
                wandbau(h, sc, pa_, pb_, zt, (), ph=k % 2, d=0.7, saat=30 + k + i, ab=ab_, ecken=False)
            continue
        wandbau(h, sc, a, b, zt, lochliste(f"okt_{k}"), ph=k % 2, d=0.7, saat=3 + k, ab=10.8 if k == 6 else None)


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
        pfeiler(h, (px - 0.05 * math.cos(w), py - 0.05 * math.sin(w)), (math.cos(w), math.sin(w)), zt, breite=0.60)
    for sx in (-1, 1):
        pfeiler(h, (XA + sx * F.CHOR_A, F.CHOR_Y0 + 1.0), (sx, 0), zt, breite=0.60)


def kegelturm(h, sc, mx, my, saat, fenster=(8, 10)):
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
            if band == "mauer" and k == fenster[0]:
                ls = [D.Spitzloch(l / 2 - 0.14, l / 2 + 0.14, 6.0, 7.2, 1.5)]
            if band == "mauer" and k == fenster[1]:
                ls = [D.Spitzloch(l / 2 - 0.14, l / 2 + 0.14, 8.6, 9.8, 1.5)]
            mauer(h, stoff, a, eu, nn, l, z0, z1, band, ls, laibung=LAIBUNG, dicke=0.25, saat=saat + k)
            for loch in ls:
                P = lambda s_, z, raus=0.0, a=a, eu=eu, nn=nn: V((a[0] + eu[0] * s_ + nn[0] * raus, a[1] + eu[1] * s_ + nn[1] * raus, z))
                D.gewaende(h, P, V((nn[0], nn[1], 0)), loch, breite=0.08)
                zu = P(l / 2, loch.z0, 5)
                D.scheibe(sc, "Fenster", lambda q, z, P=P: P(q, z, -0.25), loch, zu)
        # Deckring: Oberseite des Sockels (r + 0,10) bis zum Schaft (r), sonst liegt die Oberseite offen
        h.vieleck("Sandstein", [V((*sockel[k2], ZS)), V((*sockel[k], ZS)), V((*ecken[k], ZS)), V((*ecken[k2], ZS))],
                  [(0, BA["sandstein"][1]), (0.4 / BA["sandstein"][2], BA["sandstein"][1]), (0.4 / BA["sandstein"][2], BA["sandstein"][1] - 0.1 / BA["sandstein"][2]), (0, BA["sandstein"][1] - 0.1 / BA["sandstein"][2])], zu=V((mx, my, ZS + 5)))
        # Gesims unter dem Dach
        D.gesimsband(h, ecken[k2], ecken[k], nn, F.Z_TM_WAND - 0.2, hoehe=0.2, vor=0.12, ueber=0.12 * math.tan(math.pi / n))


def turm_ecken(r):
    cx, cy = W(*F.TURM_M); n = F.TURM_SEITEN
    return [(cx + r * math.cos(2 * math.pi * (k + 0.5) / n), cy + r * math.sin(2 * math.pi * (k + 0.5) / n)) for k in range(n)]


def turm_r(z):
    """Umkreis des Schafts in der Hoehe z: durchgehend linear von TURM_R (Sockelkante) bis TURM_R_OBEN (Glockenstube)."""
    z0, z1 = F.Z_TURMSOCKEL, F.Z_GLOCKEN[0]
    return F.TURM_R + (F.TURM_R_OBEN - F.TURM_R) * min(max((z - z0) / (z1 - z0), 0.0), 1.0)


def turm(h, s):
    """Runder Turm nach dem Artwork: Sandsteinsockel, Schaft aus Backstein als Kegelstumpf (unten breit, oben schmal, ohne Absaetze)
    mit Schlitzfenstern, zwei Sandsteinbaendern und 16 Lisenen (je eine auf jeder Ecke, folgen der Schraege, Wasserschlag unter dem
    Fries), Gesims, Rundbogenfries, offene Glockenstube aus Holz, Kupferhaube."""
    cx, cy = W(*F.TURM_M); n_ = F.TURM_SEITEN
    zg0, zg1 = F.Z_GLOCKEN
    zs0 = F.Z_TURMSOCKEL
    c = V((cx, cy, 0))
    sockel = turm_ecken(F.TURM_R + 0.12)
    ap = math.cos(math.pi / n_)                      # Umkreis -> Innenkreis (Abstand der Wandmitte)
    ring = lambda z: turm_ecken(turm_r(z))
    roof_z = lambda x: F.Z_TRAUFE_L + max(0.0, F.X_AUSSEN + 0.30 - abs(x - XA)) * F.NEIGUNG_L      # Hoehe des Langhausdachs an der Stelle x
    schaft = ring(zg0)
    schlitze = {11: 6.8, 13: 10.6, 9: 14.4, 7: 15.2}      # Seite -> Bruestung eines Schlitzfensters
    wv0, wv1, wm = BA["werkstein"]
    mv0, mv1, mm = BA["mauer"]
    kurs = (mv1 - mv0) * mm
    zs = [zs0 + i * kurs for i in range(int((zg0 - zs0) / kurs) + 1)] + [zg0]
    zs = [z for i, z in enumerate(zs) if i == 0 or z - zs[i - 1] > 1e-4]
    lx, ly0 = XA - F.X_AUSSEN, F.Y_WEST
    m_lean = (F.TURM_R_OBEN - F.TURM_R) / (zg0 - zs0)        # Radius je Meter Hoehe (negativ)
    for k in range(n_):
        k2 = (k + 1) % n_
        w = 2 * math.pi * (k + 1) / n_
        n = (math.cos(w), math.sin(w)); n3 = V((n[0], n[1], 0))
        mid = ((schaft[k][0] + schaft[k2][0]) / 2, (schaft[k][1] + schaft[k2][1]) / 2)
        drin = mid[0] > lx + 0.1 and mid[1] > ly0 + 0.1      # liegt im Langhaus: nie zu sehen
        a, b = sockel[k2], sockel[k]
        l = math.hypot(b[0] - a[0], b[1] - a[1]); eu = ((b[0] - a[0]) / l, (b[1] - a[1]) / l)
        # Seiten im Langhaus: erst ab der Dachflaeche (darueber steht der Turm frei, darunter ist er nie zu sehen)
        start = min(roof_z(ring(zs0)[k][0]), roof_z(ring(zs0)[k2][0])) - 0.1 if drin else 0.0
        if not drin:
            mauer(h, "Sandstein", a, eu, n, l, 0.0, zs0, "sandstein", laibung=LAIBUNG, dicke=0.30, saat=k + 1)
        # Schaft: Lagen als Trapeze zwischen den Ringen der Lagenhoehen, Texel wie die Mauer des Doms
        for j in range(len(zs) - 1):
            za, zb = zs[j], zs[j + 1]
            if zb <= start: continue
            ea, eb = ring(za), ring(zb)
            la = math.hypot(ea[k][0] - ea[k2][0], ea[k][1] - ea[k2][1]); lb = math.hypot(eb[k][0] - eb[k2][0], eb[k][1] - eb[k2][1])
            vers = B._lagen_versatz(j, k + 1) * mm
            t = (zb - za) / kurs
            vo = mv1 - t * (mv1 - mv0)
            pts = [V((*ea[k2], za)), V((*ea[k], za)), V((*eb[k], zb)), V((*eb[k2], zb))]
            uvs = [((0 + vers) / mm, mv1), ((la + vers) / mm, mv1), (((la + lb) / 2 + vers) / mm, vo), (((la - lb) / 2 + vers) / mm, vo)]
            h.vieleck("Bruchstein", pts, uvs, zu=(pts[0] + pts[1]) / 2 + n3 * 3 + EZ * 3)
        # Schlitzfenster: flach auf die schraege Wand gesetzt (Gewaende und Scheibe folgen der Schraege)
        if k in schlitze:
            z0w = schlitze[k]; loch = D.Spitzloch(l / 2 - 0.24, l / 2 + 0.24, z0w, z0w + 1.5, 1.3)
            zm = z0w + 0.75
            em = ring(zm); a = em[k2]; bq = em[k]
            lm = math.hypot(bq[0] - a[0], bq[1] - a[1]); eum = ((bq[0] - a[0]) / lm, (bq[1] - a[1]) / lm)
            sh = (lm - l) / 2
            P = lambda s_, z, raus=0.0, a=a, eum=eum, n=n, zm=zm, sh=sh: V((a[0] + eum[0] * (s_ + sh) + n[0] * (raus + (turm_r(z) - turm_r(zm)) * ap),
                                                                          a[1] + eum[1] * (s_ + sh) + n[1] * (raus + (turm_r(z) - turm_r(zm)) * ap), z))
            D.gewaende(h, P, n3, loch, breite=0.12)
            zu = P(l / 2, loch.z0, 5)
            D.scheibe(s, "Fenster", lambda q, z, P=P: P(q, z, 0.02), loch, zu)
            D.scheibe(s, "Bleinetz", lambda q, z, P=P: P(q, z, 0.026), loch, zu)
        # Sandsteinbaender (Gesims ohne Absatz) und Oberkante des Sockels
        for zb_ in F.TURM_BAENDER:
            if zb_ < start: continue
            e_ = ring(zb_)
            D.gesimsband(h, e_[k2], e_[k], n, zb_, hoehe=0.20, vor=0.10, ueber=0.10 * math.tan(math.pi / n_))
        if not drin:
            pa, pb, qa, qb = V((*sockel[k2], zs0)), V((*sockel[k], zs0)), V((*ring(zs0)[k2], zs0 + 0.16)), V((*ring(zs0)[k], zs0 + 0.16))
            lang = (pb - pa).length
            h.vieleck("Sandstein", [pa, pb, qb, qa], [(0, wv1), (lang / wm, wv1), (lang / wm, wv1 - 0.2 / wm), (0, wv1 - 0.2 / wm)], zu=(pa + pb) / 2 + n3 * 3 + EZ * 3)
        D.gesimsband(h, schaft[k2], schaft[k], n, zg0 - 0.30, hoehe=0.30, vor=0.18, ueber=0.18 * math.tan(math.pi / n_))
        # Rundbogenfries unter dem Gesims der Glockenstube (Artwork): je Seite drei blinde Rundbogen, dunkel
        pa, pb = V((*schaft[k2], 0)), V((*schaft[k], 0))
        lf = (pb - pa).length; ef = (pb - pa).normalized()
        Pf = lambda s_, z, raus=0.025: pa + ef * s_ + n3 * raus + EZ * z
        for i in range(3):
            sm, rr = lf * (i + 0.5) / 3, 0.17
            zb, zsp = zg0 - 0.30 - 0.58, zg0 - 0.30 - 0.30
            pts = [Pf(sm - rr, zb), Pf(sm + rr, zb)] + [Pf(sm + rr * math.cos(math.pi * j / 6), zsp + rr * math.sin(math.pi * j / 6)) for j in range(7)]
            D.farbflaeche(h, "Sandstein", pts, "nacht", zu=Pf(lf / 2, zb, 5))
    # Lisenen: 8 Stueck gleichmaessig rund um den Turm (auf Ecken des 16-Ecks, zwischen den Fenstern), dreifach abgetreppt wie Strebepfeiler,
    # folgen der Schraege; Sandsteinabdeckung auf jedem Absatz. Wo der Turm im Langhaus steht, beginnen sie auf dem Dach.
    sl = math.hypot(m_lean, 1.0)
    stufen = F.LISENE_STUFEN
    for m_ in range(8):
        w = 2 * math.pi * (0.5 / n_ + m_ / 8)
        d = V((math.cos(w), math.sin(w), 0)); q = V((-d.y, d.x, 0))
        ex, ey = cx + d.x * turm_r(zs0), cy + d.y * turm_r(zs0)
        clip = roof_z(ex) - 0.2 if (ex > lx + 0.1 and ey > ly0 + 0.1) else 0.0
        neig = (d * m_lean + EZ).normalized()
        for (za, zb, vor, hw) in stufen:
            if zb <= clip: continue
            za = max(za, clip)
            aus = lambda z, vor=vor: c + d * (turm_r(z) + vor) + EZ * z
            zm = V((cx, cy, (za + zb) / 2))
            if za <= zs0 + 1e-6:        # Sandsteinfuss bis zum Sockelrand
                D.block(h, "Sandstein", "sandstein", c + d * (turm_r(zs0) - 0.2) - q * (hw + 0.07), (q.x, q.y), (d.x, d.y), 2 * hw + 0.14, vor + 0.30, 0.0, zs0, ohne=("v-", "unten"))
            h.rechteck("Bruchstein", aus(za) - q * hw, q, neig, 2 * hw, (zb - za) * sl, "mauer", weg_von=zm)
            for sg in (-1, 1):
                h.rechteck("Bruchstein", aus(za) + q * hw * sg, -d, neig, vor + 0.12, (zb - za) * sl, "mauer", laengs="v", weg_von=c + d * (turm_r((za + zb) / 2) + vor / 2) + EZ * (za + zb) / 2)       # Normale zeigt von der Mitte des Streifens weg zur Seite
            # Abdeckung aus Sandstein auf dem Absatz (Platte mit Ueberstand, wie das Sockelband)
            o_ = c + d * (turm_r(zb) - 0.2)
            D.block(h, "Sandstein", "werkstein", o_ + EZ * zb - q * (hw + 0.06), (q.x, q.y), (d.x, d.y), 2 * hw + 0.12, vor + 0.2 + 0.07, zb, zb + 0.26)
    # Glockenstube: Boden, Pfosten und Rahmen aus Eiche, Bruestung aus Brettern, dahinter der dunkle Raum
    rand, pfosten = turm_ecken(F.TURM_R_OBEN + 0.14), turm_ecken(F.TURM_R_OBEN - 0.14)
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
        D.farbflaeche(h, "Sandstein", [ia + EZ * zg1, ib + EZ * zg1, hb + EZ * zg1, ha + EZ * zg1], "holz_dunkel", zu=c + EZ * 60)       # Oberseite des Haubenrings (Normale nach oben)
        D.farbflaeche(h, "Sandstein", [ia + EZ * zg1, ib + EZ * zg1, hb + EZ * zg1, ha + EZ * zg1], "holz_dunkel", zu=c - EZ * 60)       # Unterseite, von der Glockenstube aus gesehen
    h.dreh("Sandstein", (cx, cy, 0), [(F.TURM_R_OBEN - 0.55, zg0), (F.TURM_R_OBEN - 0.55, zg1)], "kleinteile", seiten=n_, u_bereich=D.feld("nacht"), deckel=False, glatt=False)
    # Kupferhaube (welsche Haube): ausschwingende Traufe, Bauch, Einzug, Spitze
    r = F.TURM_R_OBEN
    zh, zsp = F.Z_HAUBE, F.Z_SPITZE
    t = lambda anteil: zh + (zsp - zh) * anteil
    profil = [(r + 0.30, zh), (r + 0.18, t(0.035)), (r + 0.08, t(0.10)), (r + 0.02, t(0.22)), (r - 0.25, t(0.36)), (r - 0.80, t(0.48)), (r - 1.40, t(0.57)),
              (0.50, t(0.64)), (0.25, t(0.76)), (0.11, t(0.90)), (0.0, zsp)]
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
        kegelturm(h, sc, *W(x, y), saat=5 + 3 * i, fenster=(8, 10) if i == 0 else (10, 11))     # Tuermchen 2 steht an Achteck und Chor: Fenster nach Osten, fern vom Wandwinkel
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
    loch = D.Spitzloch(w / 2 - 0.28, w / 2 + 0.28, zf + 0.85, zf + hw - 0.10, 1.9)      # absolute Hoehen (vorher lag das Loch bei z = 0,5: kein Fenster)
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
        D.schieferflaeche(d, d_a0, d_a1, d_b0, d_b1, (d_a0 + d_b0) / 2 + V((sg * t_.x, sg * t_.y, 0)) * 3 + EZ * 3, saat=saat + sg + 2, karte=True, stufe=0)     # glatte Flaeche: Stufenenden haengen hinten sonst heraus (Pruefung 2)
        # Wange: Dreieck zwischen Wand und Hauptdach, aus Backstein wie die Stirnseite (in Lagen, die Oberkante folgt dem Hauptdach)
        mv0, mv1, mmm = BA["mauer"]
        kurs = (mv1 - mv0) * mmm
        nl = max(1, math.ceil((ze - zf) / kurs - 1e-6))
        for i in range(nl):
            za, zb = zf + (ze - zf) * i / nl, zf + (ze - zf) * (i + 1) / nl
            ra, rb = (zsp - za) / k, (zsp - zb) / k
            pts = [p(rho_f, sg * w / 2, za), p(ra, sg * w / 2, za), p(rb, sg * w / 2, zb), p(rho_f, sg * w / 2, zb)]
            vers = B._lagen_versatz(i, saat) * mmm
            t = (zb - za) / kurs
            vo = mv1 - t * (mv1 - mv0)
            uvs = [(vers / mmm, mv1), ((rho_f - ra + vers) / mmm, mv1), ((rho_f - rb + vers) / mmm, vo), (vers / mmm, vo)]
            if abs(ra - rho_f) < 1e-4: pts, uvs = [pts[0], pts[2], pts[3]], [uvs[0], uvs[2], uvs[3]]       # unterste Lage: die Wand beginnt in einer Spitze
            elif abs(rb - rho_f) < 1e-4: pts, uvs = pts[:3], uvs[:3]
            h.vieleck("Bruchstein", pts, uvs, zu=p(rho_f, sg * w / 2, (za + zb) / 2) + t_ * sg * 4)
    # Gratabdeckung auf dem First des Gaubendachs
    D.grat(d, p(rho_f + ue, 0.0, zfi + 0.05), p(rho_r, 0.0, zfi + 0.05), p(rho_f + ue, -(w / 2 + ue), ze - 0.05), p(rho_f + ue, (w / 2 + ue), ze - 0.05))


def baue_dach():
    d = D.Teil("Dach", BA, saat=11)
    sch = D.schieferflaeche
    # ── Langhaus ──
    zt = F.Z_TRAUFE_L
    xe = F.X_AUSSEN + 0.30
    y0, y1 = (F.Y_WEST, F.Y_LANG1)
    for sx in (-1, 1):
        sch(d, (XA + sx * xe, y0, zt), (XA + sx * xe, y1 + 0.6, zt), (XA, y0, F.Z_FIRST_L), (XA, y1 + 0.6, F.Z_FIRST_L), (XA + sx * 40, 0, 60), saat=1 + sx)
    D.grat(d, (XA, y0, F.Z_FIRST_L), (XA, y1 + 0.6, F.Z_FIRST_L), (XA - xe, y0, zt), (XA + xe, y0, zt))
    # ── Vorhalle (45 Grad) ──
    cx = XA + F.VH_X
    vx = F.VH_HALB + 0.25
    yv = F.Y_WEST - F.VH_TIEFE
    for sx in (-1, 1):
        sch(d, (cx + sx * vx, yv, F.VH_Z[0]), (cx + sx * vx, F.Y_WEST, F.VH_Z[0]), (cx, yv, F.VH_Z[1]), (cx, F.Y_WEST, F.VH_Z[1]), (cx + sx * 40, 0, 60), saat=8 + sx)
    D.grat(d, (cx, yv, F.VH_Z[1]), (cx, F.Y_WEST, F.VH_Z[1]), (cx - vx, yv, F.VH_Z[0]), (cx + vx, yv, F.VH_Z[0]))
    # ── Achteck: Zeltdach ──
    mx, my = W(*F.OKT_M)
    spitze = (mx, my, F.Z_SPITZE_O)
    flaechen = F.vieleck(mx, my, F.OKT_A + 0.30, 8, 0.0)
    for k, (a, b, n) in enumerate(flaechen):
        sch(d, (*a, F.Z_TRAUFE_O), (*b, F.Z_TRAUFE_O), spitze, spitze, (mx + n[0] * 40, my + n[1] * 40, 60), saat=20 + k)
    for k, (a, b, n) in enumerate(flaechen):
        a2, b2, _ = flaechen[(k + 1) % 8]
        D.grat(d, (*b, F.Z_TRAUFE_O), spitze, (*a, F.Z_TRAUFE_O), (*b2, F.Z_TRAUFE_O))
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
    # Grate des Chordachs: First der Schraegen, Grat zwischen Schraege und Schluss, Grate des Schlusses
    D.grat(d, (XA, ya, F.Z_FIRST_C), top, (XA - xc, ya, zc), (XA + xc, ya, zc))
    D.grat(d, (*sf[1][0], zc), top, (XA + xc, ya, zc), (*sf[1][1], zc))
    D.grat(d, (*sf[1][1], zc), top, (*sf[1][0], zc), (*sf[2][1], zc))
    D.grat(d, (*sf[2][1], zc), top, (*sf[2][0], zc), (*sf[3][1], zc))
    D.grat(d, (*sf[3][1], zc), top, (*sf[3][0], zc), (XA - xc, ya, zc))
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
    D.GESIMS_KAPPEN = True
    opt = B.optionen()
    D.neu()
    st = stoffe()
    scheiben = D.Teil("Scheiben", BA, saat=5)
    haus = baue_haus(scheiben)
    dach = baue_dach()
    baue_kegel(dach, 60)
    mx, my = W(*F.OKT_M)
    for i, (theta, rho) in enumerate(((0, 4.9), (90, 4.9), (180, 4.9), (270, 4.9))):
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
        B.kontrollbild(os.path.join(o, "domev_blick_turmkopf.jpg"), (XA + 4.0, -26.0, 26.5), (XA - 5.6, -15.5, 22.0), brennweite=42)
        B.kontrollbild(os.path.join(o, "domev_blick_gauben.jpg"), (XA + 10.5, -4.0, 21.0), (XA + 4.9, 3.2, 17.3), brennweite=40)


if __name__ == "__main__":
    main()
