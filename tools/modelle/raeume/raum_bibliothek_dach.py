"""Bibliothek im Dachgeschoss des Steinhauses (Wohnhaus Mensch, Stufe 3; Entscheidung des Nutzers 14.11.2026).

Wird von raeume_s23.py fuer --haus=wohnhaus_mensch_s3_g<n> --raum=bibliothek aufgerufen. Der Raum liegt unter den Dachschraegen
(kein Kniestock, Masse aus tools/modelle/neubau/wohnhaus_mensch_s23_masse.py): vorn die Innenseite des Treppengiebels mit dem Fenster,
rechts die Gaube mit ihrem Fenster, hinten die Bretterwand zum Treppengang mit der Tuer. Begehbar ist die Mitte (lichte Hoehe ueber
Figur + 0,15: Elb |x| <= 1,16); unter den Schraegen ist Stauraum (niedrige Regale, Truhe). Moebel aus raum_bibliothek.py (Regal,
Schreibtisch, Sessel, Lesepult, Truhe) mit umgesetzten Massen; der Kamin der Bibliothek entfaellt (kein Platz unter dem Dach).
Gebaut im Rahmen der Raumskripte (Fussboden bei Z_BODEN = 0,15); raeume_s23.py verschiebt um (dx, 0, Z_DG - 0,15).
"""
import math

from mathutils import Vector

import raeume_bib as R
import raeume_masse as M
import wohnhaus_mensch_s23_masse as HM
from netzbau import Teil
from raeume_trimsheet import BAENDER_RAEUME as B
from zimmertuer import zimmertuer

S = R.S
ZB = M.Z_BODEN
DZ = HM.Z_DG - ZB                                    # Rahmen der Raumskripte -> Haus
EZ = Vector((0, 0, 1))
_orte = {}

V, H = HM.DG_VORN, HM.DG_HINTEN                       # Innenseite Treppengiebel, Trennwand
XI = HM.DG_IX                                         # Fusspunkt der Schraege
GAUBE = dict(y=(-0.75, 0.45), x=1.75, fenster=(0.55, 0.62))   # wie wohnhaus_mensch_s23.GAUBE


def decke(x):
    """Unterkante der Sparren ueber der Stelle x im Rahmen des Raums (Fussboden ZB)."""
    return ZB + HM.dg_hoehe(x)


def gaube_masse():
    zb = HM.z_dach(GAUBE["x"]) + 0.10 - DZ           # Fuss der Gaubenstirn
    zt = zb + 1.05
    fz0 = zb + 0.22
    return zb, zt, fz0


def fenster_tg():
    (w, (a0, a1), (z0, z1), art), = [f for f in HM.fenster(1, 3) if f[0] == "vorn_tg"]
    return a0, a1, z0 - DZ, z1 - DZ


def baue_raum():
    """Huelle: Parkett, Giebelwand (Putz) mit Fenster und Hausteinlaibung, Schraegen aus Brettern mit Sparren, Gaubennische, Trennwand,
    Teppich, Tuer zum Treppengang."""
    r = Teil("Raum", B, saat=71)
    R.boden(r, "parkett", -XI, XI, V, H, ZB, laengs="x")
    mitte = Vector((0, (V + H) / 2, ZB + 1.2))
    a0, a1, z0, z1 = fenster_tg()
    # Giebelwand innen: Putz bis unter die Sparren, mit dem Fenster
    rand = lambda z: max(XI - (z - decke(XI)) / HM.NEIGUNG, 0.0)          # halbe Breite der Wand in Hoehe z
    zs = [ZB, decke(XI), z0, z1, decke(0.0)]
    for za, zb_ in zip(zs[:-1], zs[1:]):
        if zb_ - za < 1e-4: continue
        xa, xb = (XI, XI) if zb_ <= decke(XI) + 1e-6 else (rand(za), rand(zb_))
        if z0 <= za + 1e-4 and z1 >= zb_ - 1e-4:
            for s in (-1, 1):
                pts = [(s * a1, V, za), (s * xa, V, za), (s * xb, V, zb_), (s * a1, V, zb_)]
                r.welt(S, pts if s > 0 else pts[::-1], "putz", (-XI, V, 0), (1, 0, 0), za, 2.4, zu=mitte)
        else:
            pts = [(-xa, V, za), (xa, V, za), (xb, V, zb_), (-xb, V, zb_)] if xb > 1e-3 else [(-xa, V, za), (xa, V, za), (0, V, zb_)]
            r.welt(S, pts, "putz", (-XI, V, 0), (1, 0, 0), za, 2.4, zu=mitte)
    # Laibung aus Haustein durch die Giebelmauer, Fensterbank
    tiefe = HM.TG_DICKE
    for p, richtung, laenge in (((a0, V, z0), Vector((1, 0, 0)), a1 - a0), ((a0, V, z1), Vector((1, 0, 0)), a1 - a0), ((a0, V, z0), EZ, z1 - z0), ((a1, V, z0), EZ, z1 - z0)):
        r.rechteck(S, p, richtung, (0, -1, 0), laenge, tiefe, "quader", zu=Vector(((a0 + a1) / 2, V - tiefe / 2, (z0 + z1) / 2)), anteil=(0.0, 0.6))
    # Fensterbank 4 mm ueber der unteren Laibung: ihre Oberseite lag sonst in deren Ebene und deckte sie auf 2 cm Tiefe
    # (deckungsgleich, uv2_pruefung 12; Mensch-Nachtrag 16.11.2026, wie bei den Zwergen)
    r.kasten(S, (a0 - 0.06, V - 0.02, z0 - 0.046), (a1 + 0.06, V + 0.06, z0 + 0.004), "quader", laengs="x")
    # Schraegen: Bretter auf Sparren (rechts die Oeffnung der Gaube)
    gy0, gy1 = GAUBE["y"]
    zb, zt, fz0 = gaube_masse()
    x_n = HM.OX - (zt - 0.10 + DZ + HM.SPARREN - HM.Z_TRAUFE) / HM.NEIGUNG    # wo die Decke der Nische die Schraege trifft
    for s in (-1, 1):
        stuecke = [(V, H)] if s < 0 else [(V, gy0), (gy1, H)]
        for y0, y1 in stuecke:
            pts = [(s * XI, y0, decke(XI)), (s * XI, y1, decke(XI)), (0.0, y1, decke(0.0)), (0.0, y0, decke(0.0))]
            r.welt(S, pts, "moebel", (0, y0, 0), (0, 1, 0), ZB, 3.5, zu=Vector((0, (y0 + y1) / 2, ZB + 0.5)))
        if s > 0:   # ueber und unter der Gaubennische
            for xa_, xb_ in ((0.0, x_n), (GAUBE["x"], XI)):
                pts = [(xb_, gy0, decke(xb_)), (xb_, gy1, decke(xb_)), (xa_, gy1, decke(xa_)), (xa_, gy0, decke(xa_))]
                r.welt(S, pts, "moebel", (0, gy0, 0), (0, 1, 0), ZB, 3.5, zu=Vector((1.0, (gy0 + gy1) / 2, ZB + 0.5)))
        # Sparren (sichtbar), nicht in der Nische
        n = 6
        for k in range(n + 1):
            y = V + 0.08 + k * (H - V - 0.16) / n
            if s > 0 and gy0 - 0.05 < y < gy1 + 0.05: continue
            r.stab(S, (s * (XI - 0.04), y, decke(XI - 0.04) - 0.06), (s * 0.06, y, decke(0.06) - 0.06), 0.12, "balken", breite=0.08)
        r.kasten(S, (min(s * XI, s * (XI - 0.10)), V, ZB), (max(s * XI, s * (XI - 0.10)), H, decke(XI)), "balken", ohne=("-z",), laengs="y")  # Fussschwelle
    # Gaubennische: Decke, Wangen, Stirn mit Fenster
    zn = zt - 0.10
    r.rechteck(S, (x_n, gy0, zn), (1, 0, 0), (0, 1, 0), GAUBE["x"] - x_n, gy1 - gy0, "putz", zu=(1.2, -0.15, ZB))
    for y in (gy0, gy1):
        r.welt(S, [(x_n, y, zn), (GAUBE["x"], y, zn), (GAUBE["x"], y, decke(GAUBE["x"]))], "putz", (x_n, y, 0), (1, 0, 0), ZB, 2.4, zu=(1.2, -0.15, zn - 0.3))
    fb, fh = GAUBE["fenster"]
    ym = (gy0 + gy1) / 2
    r.wand(S, (GAUBE["x"], gy0, 0), (0, 1, 0), gy1 - gy0, decke(GAUBE["x"]), zn, "putz", loecher=[(ym - fb / 2 - gy0, ym + fb / 2 - gy0, fz0, fz0 + fh)],
           zbasis=decke(GAUBE["x"]), hoehe=2.4, zu=(0.8, ym, fz0))
    # Trennwand zum Treppengang (Bretter, Sicht in den Raum): unten bis zum Fuss der Schraege, darueber das Dreieck bis unter den First
    r.welt(S, [(-XI, H, ZB), (XI, H, ZB), (XI, H, decke(XI)), (-XI, H, decke(XI))], "moebel", (-XI, H, 0), (1, 0, 0), ZB, 3.5, zu=mitte)
    r.welt(S, [(-XI, H, decke(XI)), (XI, H, decke(XI)), (0.0, H, decke(0.0))], "moebel", (-XI, H, 0), (1, 0, 0), ZB, 3.5, zu=mitte)
    # Teppich
    v0, v1, m = B["teppich"]
    x0, x1, y0, y1 = -0.95, 0.95, V + 0.55, H - 0.95
    r.vieleck(S, [(x0, y0, ZB + 0.008), (x0, y1, ZB + 0.008), (x1, y1, ZB + 0.008), (x1, y0, ZB + 0.008)],
              [(0.6, v1), (0.6 + (y1 - y0) / m, v1), (0.6 + (y1 - y0) / m, v0), (0.6, v0)], zu=(0, 0, 9))
    # Tuer zum Treppengang: in einer Wand quer zur X-Achse gebaut und um 90 Grad in die Trennwand gedreht (Sichtseite -Y)
    tmp = Teil("_", B, saat=5)
    t0, t1 = HM.DG_TUER
    mitte_t = zimmertuer(tmp, S, "moebel", "moebel", "kleinteile", R.feld("eisen"), 0.0, t0, t1, ZB, M.TUER_H, 1, klinke_rechts=True)
    neu = []
    for pts, uvs, st, fa, gl in tmp.flaechen:
        neu.append(([Vector((p.y, H - 0.001 - p.x, p.z)) for p in pts], uvs, st, fa, gl))
    r.flaechen.extend(neu)
    _orte["tuer"] = Vector((mitte_t.y, H - 0.001 - mitte_t.x, mitte_t.z))
    return r


def baue_glas():
    g = Teil("Glas", B, saat=9)
    a0, a1, z0, z1 = fenster_tg()
    yg = HM.OVORN - 0.04 + 0.18                       # wie die Scheibe aussen: in der Mitte der Giebelmauer
    g.vieleck("Innenglas", [(a0, yg, z0), (a1, yg, z0), (a1, yg, z1), (a0, yg, z1)], [(0, 1), (a1 - a0, 1), (a1 - a0, 1 - (z1 - z0)), (0, 1 - (z1 - z0))], zu=(0, 0, z0))
    zb, zt, fz0 = gaube_masse()
    fb, fh = GAUBE["fenster"]
    ym = sum(GAUBE["y"]) / 2
    x = GAUBE["x"] - 0.08                             # wie die Scheibe der Gaube aussen
    g.vieleck("Innenglas", [(x, ym - fb / 2, fz0), (x, ym + fb / 2, fz0), (x, ym + fb / 2, fz0 + fh), (x, ym - fb / 2, fz0 + fh)],
              [(0, 1), (fb, 1), (fb, 1 - fh), (0, 1 - fh)], zu=(0, ym, fz0))
    return g


def masse_umsetzen():
    """Die Masse der Bibliothek (raeume_masse.py) fuer den Raum unter dem Dach: Wand vorn = Giebel, niedrige Regale unter den Schraegen."""
    M.IV = V
    M.BI_REGAL_H, M.BI_BRETTER = 1.80, (0.10, 0.50, 0.90, 1.30, 1.62)
    M.BI_SCHREIBTISCH = (0.55, 1.65, -0.55, 0.18, 0.76)         # unter der Gaube
    M.BI_STUHL = (1.10, 0.50, 180)
    M.BI_SESSEL = (-0.55, -1.30, 180)                           # mit Blick zum Giebelfenster
    M.BI_BEISTELL = (-1.05, -1.55, 0.62)
    M.BI_LESEPULT = (-0.70, 0.60, 1.12, 90)
    M.BI_TRUHE = (-2.30, -1.45, V, V + 0.46, 0.50)               # Stauraum unter der Schraege links vorn


def baue_regale(Q):
    """Regale: zwei hohe an der Giebelwand beiderseits des Fensters (in der begehbaren Zone), niedrige unter den Schraegen (Stauraum)."""
    c = Teil("Regale", B, saat=63)
    a0, a1, _, _ = fenster_tg()
    for i, (u0, u1) in enumerate(((-1.20, a0 - 0.10), (a1 + 0.10, 1.20))):
        Q.regal(c, "vorn", u0, u1, 7 + i)
    hoch, bretter, ix = M.BI_REGAL_H, M.BI_BRETTER, M.IX
    M.BI_REGAL_H, M.BI_BRETTER, M.IX = 0.85, (0.10, 0.45, 0.80), 1.95
    gy0, gy1 = GAUBE["y"]
    # rechts: nur vor der Gaube (y < gy0 - 0,20), der Schreibtisch steht bei y -0,55..0,18; hinter der Gaube bleibt kein Platz
    regale = [("links", -1.95, -0.35), ("links", -0.15, 1.70), ("rechts", -2.30, min(gy0 - 0.20, -0.95)), ("rechts", gy1 + 0.20, 1.70)]
    for i, (wand, u0, u1) in enumerate(regale):
        if u1 - u0 >= 0.30: Q.regal(c, wand, u0, u1, 11 + i)
    M.BI_REGAL_H, M.BI_BRETTER, M.IX = hoch, bretter, ix
    return c


def anker():
    a0, a1, z0, z1 = fenster_tg()
    return [
        R.anker("Blick_Auge", (0.0, H - 0.45, ZB + M.AUGE)), R.anker("Blick_Ziel", (0.15, V, ZB + 0.95)),
        R.anker("Licht_Raum", (0.0, -0.3, ZB + 2.3), art="Raum"),
        R.anker("Licht_Kerze", tuple(_orte["kerze"] + Vector((0, 0, 0.12))), art="Kerze"),
        R.anker("Feuer_Kerze_1", tuple(_orte["kerze"])), R.anker("Feuer_Kerze_2", tuple(_orte["kerze2"])),
        R.anker("Tuer_Bibliothek_Wohnstube", tuple(_orte["tuer"]), ziel="Wohnstube", breite=M.TUER_B, hoehe=M.TUER_H, achse="z"),
    ]


def teile(Q):
    """Die Teile des Raums (Q: raum_bibliothek, dessen Moebel benutzt werden; ihre Kerzen landen in Q._orte)."""
    masse_umsetzen()
    t = [baue_raum(), baue_regale(Q), Q.baue_sessel(), Q.baue_schreibtisch(), Q.baue_lesepult(), Q.baue_truhe(), baue_glas()]
    _orte["kerze"], _orte["kerze2"] = Q._orte["kerze"], Q._orte["kerze2"]
    return t
