"""Bibliothek des Wohnhauses Mensch (Raum der Stufe 3, Grundform 1).

    blender -b --factory-startup --python tools/modelle/raeume/raum_bibliothek.py -- [--bilder=<ordner>] [--ohne-ao] [--proben=64]

Schreibt game/assets/modelle/gebaeude/neubau/wohnhaus_mensch_s1_g1_bibliothek_innen.glb (im Spiel Knoten „InteriorRoot_Bibliothek“ im
Haus, Stadtansicht.Wohnraeume.cs) und Zelle 1 des AO-Atlas wohnhaus_mensch_raeume_innen_ao_atlas.png.

Vorlage: Artwork des Nutzers „bibliothek.webp“ (nur Stil): Taefelung, Buecherregale bis unter die Decke mit Leiter, Schreibtisch mit
Globus, Lesepult, Sessel am Sandsteinkamin, Rechteckfenster mit Butzenglas (bis 16.11.2026 Spitzbogen), Teppich, Truhe mit Folianten. Masse aus raeume_masse.py
(Soll/Ist dort). Das Lesepult ist der Gegenstand des Raums (Klick: Familienbuch mit den Lehrschriften); alles andere ist Ausstattung.
"""
import math
import os
import random
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import raeume_bib as R                                                    # noqa: E402
import raeume_masse as M                                                  # noqa: E402
from netzbau import Teil                                                  # noqa: E402
from raeume_trimsheet import BAENDER_RAEUME as B, KARTE_U, SCHRIFT_U      # noqa: E402
from zimmertuer import zimmertuer                                         # noqa: E402
from mathutils import Vector                                              # noqa: E402

S = R.S
ZB = M.Z_BODEN
_orte = {}


# ── Wandkoordinaten: a entlang der Wand, t von der Wand in den Raum ──────────────────────────────────────────────────
def W(wand, a, t, z):
    return {"rechts": Vector((M.IX - t, a, z)), "vorn": Vector((a, M.IV + t, z)), "links": Vector((-M.IX + t, a, z))}[wand]


ACHSEN = {"rechts": {"-t": "+x", "+t": "-x", "-a": "-y", "+a": "+y", "a": "y"},
          "vorn": {"-t": "-y", "+t": "+y", "-a": "-x", "+a": "+x", "a": "x"},
          "links": {"-t": "-x", "+t": "+x", "-a": "-y", "+a": "+y", "a": "y"}}


def kasten_w(teil, wand, a0, a1, t0, t1, z0, z1, band, ohne=(), laengs="a"):
    p, q = W(wand, a0, t0, z0), W(wand, a1, t1, z1)
    mn = (min(p.x, q.x), min(p.y, q.y), z0); mx = (max(p.x, q.x), max(p.y, q.y), z1)
    ac = ACHSEN[wand]
    teil.kasten(S, mn, mx, band, ohne=tuple(ac.get(o, o) for o in ohne), laengs=ac["a"] if laengs == "a" else laengs)


def buecher(teil, wand, a0, a1, t_hinten, z, h_max, saat):
    """Buecher auf einem Fach in Gruppen (Kaesten mit dem Band „buecher“ vorn: jede Gruppe zeigt mehrere Ruecken), unterschiedlich hoch
    und tief, dazwischen Luecken, liegende Stapel und Schriftrollen."""
    z_ = random.Random(saat)
    v0, v1, m = B["buecher"]
    p0, p1, _ = B["papier"]
    a = a0 + 0.01
    while a < a1 - 0.06:
        art = z_.random()
        if art < 0.08:                                   # Luecke
            a += z_.uniform(0.05, 0.12); continue
        if art < 0.18:                                   # liegender Stapel
            b = min(z_.uniform(0.18, 0.26), a1 - a)
            hz = 0
            for k in range(z_.randint(2, 4)):
                hh = z_.uniform(0.03, 0.05)
                kasten_w(teil, wand, a + 0.01 * k, a + b - 0.01 * k, t_hinten, t_hinten + z_.uniform(0.18, 0.24), z + hz, z + hz + hh, "leder", ohne=("-t",))
                hz += hh
            a += b + 0.01; continue
        if art < 0.24:                                   # Schriftrollen
            for k in range(3):
                pa = W(wand, a + 0.05 + 0.07 * k, t_hinten + 0.02, z + 0.03 + 0.05 * (k % 2))
                teil.stab(S, pa, W(wand, a + 0.05 + 0.07 * k, t_hinten + 0.24, z + 0.03 + 0.05 * (k % 2)), 0.05, "papier", kappen=True)
            a += 0.24; continue
        b = min(z_.uniform(0.16, 0.42), a1 - a)
        h = z_.uniform(0.62, 1.0) * h_max
        t = z_.uniform(0.17, 0.25)
        u = z_.random()
        vorn = [W(wand, a, t_hinten + t, z), W(wand, a + b, t_hinten + t, z), W(wand, a + b, t_hinten + t, z + h), W(wand, a, t_hinten + t, z + h)]
        teil.vieleck(S, vorn, [(u, v1), (u + b / m, v1), (u + b / m, v0), (u, v0)], zu=W(wand, a + b / 2, t_hinten + t + 1, z + h / 2))
        oben = [W(wand, a, t_hinten, z + h), W(wand, a + b, t_hinten, z + h), W(wand, a + b, t_hinten + t, z + h), W(wand, a, t_hinten + t, z + h)]
        teil.vieleck(S, oben, [(0.6 + 0.1 * u, p1), (0.64 + 0.1 * u, p1), (0.64 + 0.1 * u, p0 + 0.02), (0.6 + 0.1 * u, p0 + 0.02)], zu=W(wand, a + b / 2, t_hinten + t / 2, z + h + 1))
        for s in (a, a + b):                             # Seiten (Einband)
            seite = [W(wand, s, t_hinten, z), W(wand, s, t_hinten + t, z), W(wand, s, t_hinten + t, z + h), W(wand, s, t_hinten, z + h)]
            l0, l1, _ = B["leder"]
            teil.vieleck(S, seite, [(0.1, l1), (0.2, l1), (0.2, l0), (0.1, l0)], zu=W(wand, s + (1 if s > a else -1), t_hinten + t / 2, z + h / 2))
        a += b + z_.uniform(0.0, 0.02)


def regal(teil, wand, a0, a1, saat):
    """Buecherregal an der Wand: Seiten, Rueckwand, Faecher (BI_BRETTER), Kranz oben, Sockel; auf jedem Fach Buecher."""
    T = M.BI_REGAL_T
    H = M.BI_REGAL_H
    d = 0.035
    kasten_w(teil, wand, a0 + 0.035, a1 - 0.035, 0.0, 0.012, ZB, ZB + H, "moebel", ohne=("-t", "-a", "+a"))   # Rueckwand zwischen den Seiten
    for a in (a0, a1 - d):
        kasten_w(teil, wand, a, a + d, 0.0, T, ZB, ZB + H, "moebel", ohne=("-t", "-z"), laengs="z")    # Seiten
    kasten_w(teil, wand, a0 + d, a1 - d, 0.012, T, ZB, ZB + 0.10, "moebel", ohne=("-t", "-z", "+z"))   # Sockel vorn
    kasten_w(teil, wand, a0 - 0.02, a1 + 0.02, 0.0, T + 0.04, ZB + H, ZB + H + 0.06, "moebel", ohne=("-t",))   # Kranz
    for i, hb in enumerate(M.BI_BRETTER):
        kasten_w(teil, wand, a0 + d, a1 - d, 0.012, T - 0.01, ZB + hb - 0.025, ZB + hb, "moebel", ohne=("-t",))
        if i + 1 < len(M.BI_BRETTER):
            frei = M.BI_BRETTER[i + 1] - hb - 0.025 - 0.03
            buecher(teil, wand, a0 + d, a1 - d, 0.02, ZB + hb, min(frei, 0.34), saat * 31 + i)
    # Mittelpfosten bei breiten Regalen
    if a1 - a0 > 1.0:
        am = (a0 + a1) / 2
        kasten_w(teil, wand, am - d / 2, am + d / 2, 0.012, T, ZB + 0.10, ZB + H, "moebel", ohne=("-t", "-z", "+z"), laengs="z")


def baue_raum():
    """Huelle: Parkett, Taefelung bis 1,0 m, Kalkputz, Bretterdecke mit Balken, Rechteckfenster mit Gewaende, Teppich, Tuer zur Wohnstube."""
    r = Teil("Raum", B, saat=61)
    R.boden(r, "parkett", -M.IX, M.IX, M.IV, M.IH, ZB, laengs="x")
    R.waende(r, "putz", tafel="moebel", tafel_hoehe=1.0)
    R.decke(r, bretter="moebel", balken="balken")
    # Abschlussleiste der Taefelung
    for wand in ("vorn", "hinten", "links", "rechts"):
        (ox, oy), (ex, ey), laenge = R.WAENDE[wand]
        a0 = ox if ex else oy
        n = R.wandpunkt(wand, 0, 0, 1) - R.wandpunkt(wand, 0, 0, 0)
        stuecke = [(a0, a0 + laenge)]
        for f in R.fenster_der_wand(wand):
            if f[2] - ZB < 1.06: stuecke = [(s0, min(s1, f[0])) for s0, s1 in stuecke] + [(f[1], a0 + laenge)]
        for s0, s1 in stuecke:
            if s1 - s0 > 0.05: r.brett(S, R.wandpunkt(wand, s0, ZB + 1.0), R.wandpunkt(wand, s1, ZB + 1.0), 0.06, n, "moebel", tiefe=0.03)
    # Fenster rechteckig wie die Oeffnung der Huelle (Nutzer 17.11.2026; vorher Spitzbogen)
    for wand in ("vorn", "links"):
        for a0, a1, z0, z1 in R.fenster_der_wand(wand):
            R.bibliotheksfenster(r, "moebel", wand, a0, a1, z0, z1)
            n = R.wandpunkt(wand, 0, 0, 1) - R.wandpunkt(wand, 0, 0, 0)
            p0 = R.wandpunkt(wand, a0 - 0.06, z0 - 0.03, 0.06); p1 = R.wandpunkt(wand, a1 + 0.06, z0, -0.02)
            r.kasten(S, (min(p0.x, p1.x), min(p0.y, p1.y), z0 - 0.05), (max(p0.x, p1.x), max(p0.y, p1.y), z0), "quader")   # Fensterbank aus Sandstein
    # Teppich mit Borte
    x0, x1, y0, y1 = M.BI_TEPPICH
    v0, v1, m = B["teppich"]
    r.vieleck(S, [(x0, y0, ZB + 0.008), (x0, y1, ZB + 0.008), (x1, y1, ZB + 0.008), (x1, y0, ZB + 0.008)],
              [(0.6, v1), (0.6 + (y1 - y0) / m, v1), (0.6 + (y1 - y0) / m, v0), (0.6, v0)], zu=(0, 0, 9))
    # Tuer zur Wohnstube
    wand, (ty0, ty1) = M.RAUM_TUER["Bibliothek"]
    wx, innen = (-M.IX, 1) if wand == "links" else (M.IX, -1)
    _orte["tuer"] = zimmertuer(r, S, "moebel", "moebel", "kleinteile", R.feld("eisen"), wx, ty0, ty1, ZB, M.TUER_H, innen, klinke_rechts=True)
    return r


def baue_regale():
    """Drei Regale: rechte Wand vorn und hinten (um den Kamin), Gassenwand rechts vom Fenster; dazu die Leiter."""
    c = Teil("Regale", B, saat=63)
    for i, (wand, a0, a1) in enumerate(M.BI_REGALE):
        regal(c, wand, a0, a1, 7 + i)
    # Leiter an der rechten Wand: lehnt am Kranz des vorderen Regals
    ym, ztop, grad = M.BI_LEITER
    kopf_t = M.BI_REGAL_T + 0.03
    lauf = (ztop - ZB) / math.tan(math.radians(grad))
    for dy in (-M.BI_LEITER_B / 2, M.BI_LEITER_B / 2):
        c.stab(S, W("rechts", ym + dy, kopf_t + lauf, ZB), W("rechts", ym + dy, kopf_t, ztop + 0.08), 0.05, "moebel", breite=0.06, kappen=True)
    n = int((ztop - ZB - 0.15) / M.BI_SPROSSE)
    for k in range(1, n + 1):
        f = (k * M.BI_SPROSSE) / (ztop - ZB)
        c.stab(S, W("rechts", ym - M.BI_LEITER_B / 2, kopf_t + lauf * (1 - f), ZB + (ztop - ZB) * f), W("rechts", ym + M.BI_LEITER_B / 2, kopf_t + lauf * (1 - f), ZB + (ztop - ZB) * f), 0.035, "moebel")
    return c


def baue_kamin():
    """Sandsteinkamin an der rechten Wand: Herdplatte, Wangen, Sturz mit Sims, Rauchfang bis unter die Decke, Feuerraum, Scheite."""
    c = Teil("Kamin", B, saat=65)
    wand, a0, a1 = M.BI_KAMIN
    T = 0.42
    ob, oh = M.BI_KAMIN_OEFFNUNG
    am = (a0 + a1) / 2
    zf = ZB + 0.08
    kasten_w(c, wand, a0 - 0.06, a1 + 0.06, 0.0, T + 0.22, ZB, zf, "quader", ohne=("-t", "-z"))                          # Herdplatte
    for s0, s1, innen in ((a0, am - ob / 2, "+a"), (am + ob / 2, a1, "-a")):                                                # Wangen (die Seite zum
        kasten_w(c, wand, s0, s1, 0.0, T, zf, zf + oh, "quader", ohne=("-t", "-z", innen))                                # Feuerraum zeichnet der Feuerraum)
    kasten_w(c, wand, a0, a1, 0.0, T, zf + oh, zf + oh + 0.30, "quader", ohne=("-t", "-z"))                               # Sturz (unten: Decke des Feuerraums)
    kasten_w(c, wand, a0 - 0.08, a1 + 0.08, 0.0, T + 0.08, zf + oh + 0.30, zf + oh + 0.37, "quader", ohne=("-t",))        # Sims
    z = zf + oh + 0.37
    kurs = (B["quader"][1] - B["quader"][0]) * B["quader"][2]
    while z < M.DECKE - 0.17:                                                                                               # Rauchfang in Lagen
        zn = min(z + kurs, M.DECKE - 0.16)
        kasten_w(c, wand, a0 + 0.10, a1 - 0.10, 0.0, T - 0.12, z, zn, "quader", ohne=("-t", "-z", "+z"))
        z = zn
    # Feuerraum innen (dunkel) und Glut
    ut, ug = R.feld("tinte"), R.feld("glut")
    k0, k1, _ = B["kleinteile"]
    dunkel = [(ut[0], k1), (ut[1], k1), (ut[1], k0), (ut[0], k0)]
    s0, s1, ti = am - ob / 2, am + ob / 2, 0.05
    mitte = W(wand, am, T / 2, zf + oh / 2)
    for pts in ([W(wand, s0, ti, zf), W(wand, s1, ti, zf), W(wand, s1, ti, zf + oh), W(wand, s0, ti, zf + oh)],
                [W(wand, s0, ti, zf), W(wand, s0, T, zf), W(wand, s0, T, zf + oh), W(wand, s0, ti, zf + oh)],
                [W(wand, s1, ti, zf), W(wand, s1, T, zf), W(wand, s1, T, zf + oh), W(wand, s1, ti, zf + oh)],
                [W(wand, s0, ti, zf + oh), W(wand, s1, ti, zf + oh), W(wand, s1, T, zf + oh), W(wand, s0, T, zf + oh)]):
        c.vieleck(S, pts, dunkel, zu=mitte)
    c.vieleck(S, [W(wand, s0, ti, zf + 0.004), W(wand, s1, ti, zf + 0.004), W(wand, s1, T, zf + 0.004), W(wand, s0, T, zf + 0.004)], [(ug[0], k1), (ug[1], k1), (ug[1], k0), (ug[0], k0)], zu=(0, 0, 9))
    for p, q in ((W(wand, s0 + 0.08, 0.14, zf + 0.05), W(wand, s1 - 0.10, 0.26, zf + 0.05)), (W(wand, s0 + 0.10, 0.28, zf + 0.06), W(wand, s1 - 0.08, 0.16, zf + 0.07)), (W(wand, s0 + 0.18, 0.20, zf + 0.12), W(wand, s1 - 0.20, 0.22, zf + 0.12))):
        c.stab(S, p, q, 0.08, "balken", kappen=True)
    _orte["kamin"] = W(wand, am, 0.22, zf + 0.06)
    # Auf dem Sims: zwei Leuchter und ein Krug
    zs = zf + oh + 0.37
    for a in (a0 + 0.12, a1 - 0.12):
        R.kerze(c, W(wand, a, 0.20, zs).x, W(wand, a, 0.20, zs).y, zs, hoch=0.16)
    p = W(wand, am + 0.12, 0.18, zs)
    c.dreh(S, p, [(0.05, 0.0), (0.07, 0.06), (0.06, 0.14), (0.04, 0.18), (0.045, 0.2)], "kleinteile", seiten=9, deckel=False, u_bereich=R.feld("messing"))
    return c


def baue_sessel():
    """Ohrensessel mit Lederpolster schraeg zum Feuer, daneben ein runder Beistelltisch mit Kerze und Buch."""
    c = Teil("Sessel_Set", B, saat=67)
    x, y, blick = M.BI_SESSEL
    R.stuhl(c, "moebel", (x, y, ZB), blick, sitz=M.BI_SESSEL_SITZ, breite=0.66, tiefe=0.60, vorn=0.20, lehne=1.18, polster="leder", armlehnen=True)
    # Polster der Lehne und Ohren
    w = math.radians(blick)
    vor = Vector((math.sin(w), math.cos(w), 0)); quer = Vector((math.cos(w), -math.sin(w), 0))
    mitte = Vector((x, y, ZB)) + vor * (0.20 - 0.60 / 2)
    rueck = mitte - vor * 0.27
    l0, l1, _ = B["leder"]
    for sx in (-1, 1):
        pts = [rueck + quer * sx * 0.30 + Vector((0, 0, M.BI_SESSEL_SITZ + 0.25)), rueck + quer * sx * 0.30 + vor * 0.16 + Vector((0, 0, M.BI_SESSEL_SITZ + 0.32)),
               rueck + quer * sx * 0.30 + vor * 0.10 + Vector((0, 0, ZB + 1.10 - ZB)), rueck + quer * sx * 0.30 + Vector((0, 0, ZB + 1.14 - ZB))]
        c.vieleck(S, pts, [(0.3, l1), (0.4, l1), (0.4, l0), (0.3, l0)], zu=rueck + quer * sx * 2 + Vector((0, 0, 0.8)))
    pl = [rueck + quer * -0.29 + vor * 0.03 + Vector((0, 0, M.BI_SESSEL_SITZ + 0.02)), rueck + quer * 0.29 + vor * 0.03 + Vector((0, 0, M.BI_SESSEL_SITZ + 0.02)),
          rueck + quer * 0.29 + vor * 0.03 + Vector((0, 0, 1.08)), rueck + quer * -0.29 + vor * 0.03 + Vector((0, 0, 1.08))]
    c.vieleck(S, pl, [(0.5, l1), (0.6, l1), (0.6, l0), (0.5, l0)], zu=rueck + vor * 2 + Vector((0, 0, 0.8)))
    # Beistelltisch
    bx, by, bh = M.BI_BEISTELL
    c.dreh(S, (bx, by, ZB + bh - 0.035), [(0.22, 0.0), (0.22, 0.035)], "moebel", seiten=14, glatt=False)
    c.dreh(S, (bx, by, ZB), [(0.16, 0.0), (0.12, 0.03), (0.04, 0.06), (0.035, bh - 0.08), (0.06, bh - 0.035)], "moebel", seiten=8, deckel=False)
    _orte["kerze2"] = R.kerze(c, bx + 0.06, by + 0.05, ZB + bh, hoch=0.12)
    c.kasten(S, (bx - 0.14, by - 0.12, ZB + bh), (bx + 0.02, by + 0.0, ZB + bh + 0.045), "leder")
    return c


def baue_schreibtisch():
    """Schreibtisch mit Schubladenblende, Globus, aufgeschlagenem Buch, Tintenfass mit Feder, Kerze, Buecherstapel; Stuhl dahinter."""
    c = Teil("Schreibtisch_Set", B, saat=69)
    x0, x1, y0, y1, h = M.BI_SCHREIBTISCH
    R.tisch(c, "moebel", x0, x1, y0, y1, hoehe=h, platte=0.05, zarge=0.14, bein=0.08, z0=ZB)
    c.kasten(S, (x0 + 0.06, y0 + 0.02, ZB + 0.12), (x1 - 0.06, y0 + 0.05, ZB + h - 0.20), "moebel", ohne=("+y",))     # Blende zur Gasse
    zt = ZB + h
    ue, um = R.feld("eisen"), R.feld("messing")
    # Globus auf Fuss (Messing), Kugel mit der Landkarte
    gx, gy = x1 - 0.18, y0 + 0.20
    c.dreh(S, (gx, gy, zt), [(0.10, 0.0), (0.08, 0.02), (0.02, 0.05), (0.02, 0.16)], "kleinteile", seiten=8, deckel=False, u_bereich=um)
    rk = 0.13
    kugel = [(rk * math.sin(math.pi * i / 8), rk * (1 - math.cos(math.pi * i / 8))) for i in range(9)]
    c.dreh(S, (gx, gy, zt + 0.16), kugel, "papier", seiten=14, deckel=False, u_bereich=KARTE_U)
    for k in range(13):                                                                                                   # Meridianring
        w0, w1 = math.pi * k / 12 - math.pi / 2, math.pi * (k + 1) / 12 - math.pi / 2
        p0 = Vector((gx + (rk + 0.015) * math.cos(w0), gy, zt + 0.16 + rk + (rk + 0.015) * math.sin(w0)))
        p1 = Vector((gx + (rk + 0.015) * math.cos(w1), gy, zt + 0.16 + rk + (rk + 0.015) * math.sin(w1)))
        if k < 12: c.stab(S, p0, p1, 0.012, "kleinteile", u_bereich=um)
    # aufgeschlagenes Buch (zwei Seiten im Knick), Tintenfass, Feder, Kerze
    p0, p1, _ = B["papier"]
    sa, sb = SCHRIFT_U
    bm = Vector((x0 + 0.42, (y0 + y1) / 2 + 0.02, zt + 0.02))
    for seite in (-1, 1):
        pts = [bm + Vector((0, -0.14, 0)), bm + Vector((seite * 0.20, -0.14, 0.03)), bm + Vector((seite * 0.20, 0.14, 0.03)), bm + Vector((0, 0.14, 0))]
        uu = (sa, sa + 0.08) if seite < 0 else (sa + 0.1, sa + 0.18)
        c.vieleck(S, pts, [(uu[0], p1), (uu[1], p1), (uu[1], p0), (uu[0], p0)], zu=bm + Vector((0, 0, 1)))
    c.kasten(S, bm + Vector((-0.22, -0.155, -0.02)), bm + Vector((0.22, 0.155, 0.0)), "leder", ohne=("-z",))
    c.dreh(S, (x0 + 0.78, y0 + 0.50, zt), [(0.035, 0.0), (0.04, 0.03), (0.02, 0.06), (0.022, 0.07)], "kleinteile", seiten=8, deckel=True, u_bereich=R.feld("tinte"))
    c.stab(S, Vector((x0 + 0.78, y0 + 0.50, zt + 0.05)), Vector((x0 + 0.70, y0 + 0.58, zt + 0.26)), 0.012, "papier", breite=0.03)
    _orte["kerze"] = R.kerze(c, x0 + 0.12, y0 + 0.55, zt, hoch=0.15)
    # Buecherstapel auf dem Tisch und am Boden neben dem Tisch
    z = zt
    for k, (b, t, hh) in enumerate(((0.24, 0.17, 0.05), (0.22, 0.16, 0.04), (0.20, 0.15, 0.05))):
        c.kasten(S, (x0 + 0.06 + 0.01 * k, y0 + 0.08, z), (x0 + 0.06 + b, y0 + 0.08 + t, z + hh), "leder", ohne=("-z",)); z += hh
    z = ZB
    for k, (b, t, hh) in enumerate(((0.32, 0.24, 0.07), (0.30, 0.22, 0.06), (0.28, 0.21, 0.06), (0.26, 0.20, 0.05))):
        c.kasten(S, (x0 - 0.42 + 0.01 * k, y0 + 0.10, z), (x0 - 0.42 + b, y0 + 0.10 + t, z + hh), "leder", ohne=("-z",)); z += hh
    # Stuhl hinter dem Tisch
    sx, sy, sb = M.BI_STUHL
    R.stuhl(c, "moebel", (sx, sy, ZB), sb, sitz=0.45, breite=0.50, tiefe=0.46, lehne=1.10, polster="leder")
    return c


def baue_lesepult():
    """Gegenstand_Lesepult: Pfosten auf Kreuzfuss, schraege Platte mit Leiste, aufgeschlagenes Buch, Lesezeichen."""
    c = Teil("Gegenstand_Lesepult", B, saat=71)
    px, py, hv, blick = M.BI_LESEPULT
    w = math.radians(blick)
    zum_leser = Vector((-math.sin(w), -math.cos(w), 0))          # vom Pult zum Lesenden (Blick 270: der Lesende steht bei +X)
    quer = Vector((zum_leser.y, -zum_leser.x, 0))
    fuss = Vector((px, py, ZB))
    for d in (quer, -quer, zum_leser, -zum_leser):
        c.stab(S, fuss + Vector((0, 0, 0.04)), fuss + d * 0.30 + Vector((0, 0, 0.02)), 0.05, "moebel", breite=0.06, kappen=True)
    c.dreh(S, fuss, [(0.06, 0.0), (0.045, 0.08), (0.035, 0.20), (0.04, 0.5), (0.03, hv - 0.10), (0.05, hv - 0.02)], "moebel", seiten=8, deckel=True)
    # Platte: vorn (zum Leser) Kante hv, hinten 0,22 hoeher, 0,52 breit, 0,40 tief
    vk = fuss + zum_leser * 0.20 + Vector((0, 0, hv))
    hk = fuss - zum_leser * 0.20 + Vector((0, 0, hv + 0.22))
    ecken = [vk - quer * 0.26, vk + quer * 0.26, hk + quer * 0.26, hk - quer * 0.26]
    m0, m1, mm = B["moebel"]
    c.vieleck(S, ecken, [(0.2, m1), (0.46, m1), (0.46, m0), (0.2, m0)], zu=(vk + hk) / 2 + zum_leser * 0.3 + Vector((0, 0, 1)))
    unten = [e - Vector((0, 0, 0.03)) for e in ecken]
    c.vieleck(S, unten, [(0.2, m1), (0.46, m1), (0.46, m0), (0.2, m0)], zu=(vk + hk) / 2 - Vector((0, 0, 1)))
    for i in range(4):
        a, b = ecken[i], ecken[(i + 1) % 4]
        c.vieleck(S, [a, b, b - Vector((0, 0, 0.03)), a - Vector((0, 0, 0.03))], [(0.1, m1), (0.2, m1), (0.2, m0 + 0.02), (0.1, m0 + 0.02)], zu=(a + b) * 0.5 + ((a + b) * 0.5 - (vk + hk) * 0.5) * 3)
    c.stab(S, vk - quer * 0.26 + zum_leser * 0.015 + Vector((0, 0, 0.02)), vk + quer * 0.26 + zum_leser * 0.015 + Vector((0, 0, 0.02)), 0.03, "moebel")   # Leiste
    # Buch: zwei Seiten, leicht gewoelbt
    p0, p1, _ = B["papier"]
    sa, sb = SCHRIFT_U
    neig = (hk - vk).normalized()
    bm = (vk + hk) / 2 + Vector((0, 0, 0.012)) + zum_leser * 0.02
    for seite in (-1, 1):
        pts = [bm - neig * 0.14, bm - neig * 0.14 + quer * seite * 0.19 + Vector((0, 0, 0.02)), bm + neig * 0.14 + quer * seite * 0.19 + Vector((0, 0, 0.02)), bm + neig * 0.14]
        uu = (sa + 0.02, sa + 0.10) if seite < 0 else (sa + 0.12, sa + 0.20)
        c.vieleck(S, pts, [(uu[0], p1), (uu[1], p1), (uu[1], p0), (uu[0], p0)], zu=bm + Vector((0, 0, 1)) + zum_leser * 0.5)
    c.vieleck(S, [bm - neig * 0.15 - quer * 0.21 - Vector((0, 0, 0.006)), bm - neig * 0.15 + quer * 0.21 - Vector((0, 0, 0.006)), bm + neig * 0.15 + quer * 0.21 - Vector((0, 0, 0.006)), bm + neig * 0.15 - quer * 0.21 - Vector((0, 0, 0.006))],
              [(0.1, B["leder"][1]), (0.2, B["leder"][1]), (0.2, B["leder"][0]), (0.1, B["leder"][0])], zu=bm + Vector((0, 0, 1)))
    c.stab(S, bm + Vector((0, 0, 0.015)), bm - neig * 0.30 + Vector((0, 0, -0.10)), 0.008, "kleinteile", breite=0.02, u_bereich=R.feld("siegel"))   # Lesezeichen
    return c


def baue_truhe():
    """Truhe mit Folianten unter dem Fenster der Gassenwand; obenauf Schriftrollen."""
    c = Teil("Truhe_Folianten", B, saat=73)
    x0, x1, y0, y1, h = M.BI_TRUHE
    c.kasten(S, (x0, y0 + 0.02, ZB), (x1, y1, ZB + h), "moebel", ohne=("-z", "-y"), laengs="x")
    ue = R.feld("messing")
    k0, k1, _ = B["kleinteile"]
    for x in (x0 + 0.08, x1 - 0.08):
        c.kasten(S, (x - 0.02, y1, ZB + 0.03), (x + 0.02, y1 + 0.006, ZB + h - 0.02), "kleinteile", ohne=("-y",))
        for fl in c.flaechen[-5:]: fl[1][:] = [(ue[0], k1), (ue[1], k1), (ue[1], k0), (ue[0], k0)]
    for k in range(4):
        y = y0 + 0.10 + 0.08 * (k % 3)
        c.stab(S, (x0 + 0.08 + 0.18 * k, y, ZB + h + 0.035), (x0 + 0.30 + 0.18 * k, y + 0.04, ZB + h + 0.035), 0.06, "papier", kappen=True)
    # Folianten gelehnt an die Truhe
    for k, (b, hh) in enumerate(((0.05, 0.42), (0.06, 0.38))):
        xa = x1 + 0.03 + 0.065 * k
        c.kasten(S, (xa, y1 - 0.30, ZB), (xa + b, y1, ZB + hh), "leder", ohne=("-z",))
    return c


def baue_glas():
    g = Teil("Glas", B, saat=9)
    for wand in ("vorn", "links"):
        for a0, a1, z0, z1 in R.fenster_der_wand(wand):
            dummy = Teil("_", B)
            R.bibliotheksfenster(dummy, "moebel", wand, a0, a1, z0, z1, glas_teil=g)
    return g


def anker():
    eye, ziel = M.BI_KAMERA
    return [
        R.anker("Blick_Auge", eye), R.anker("Blick_Ziel", ziel),
        R.anker("Licht_Raum", (0.1, -0.3, M.DECKE - 0.35), art="Raum"),
        R.anker("Licht_Kamin", tuple(_orte["kamin"] + Vector((-0.45, 0, 0.35))), art="Kamin"),
        R.anker("Feuer_Kamin_1", tuple(_orte["kamin"])),
        R.anker("Licht_Kerze", tuple(_orte["kerze"] + Vector((0, 0, 0.12))), art="Kerze"),
        R.anker("Feuer_Kerze_1", tuple(_orte["kerze"])),
        R.anker("Feuer_Kerze_2", tuple(_orte["kerze2"])),
        R.anker("Tuer_Bibliothek_Wohnstube", tuple(_orte["tuer"]), ziel="Wohnstube", breite=M.TUER_B, hoehe=M.TUER_H, achse="x"),
    ]


def main():
    opt = R.optionen()
    teile = [baue_raum, baue_regale, baue_kamin, baue_sessel, baue_schreibtisch, baue_lesepult, baue_truhe, baue_glas]
    R.raum_bauen("bibliothek", teile, anker, opt)
    if "bilder" in opt:
        eye, ziel = M.BI_KAMERA
        R.raum_bilder(opt["bilder"], "bibliothek",
                      [("raumkamera", eye, ziel), ("kamin", eye, (1.7, -0.2, 1.0)), ("lesepult", eye, (-1.3, 0.2, 0.9))],
                      ((-4.4, 5.0, 5.4), (0.2, -0.3, 0.5)),
                      mass=[(-0.95, 0.50, ZB, 1.75, 0.44), (-1.45, -1.35, ZB, 1.75, 0.44), (0.95, 0.85, ZB, 2.50, 0.62)],
                      lichter=[(tuple(_orte["kamin"] + Vector((-0.4, 0, 0.3))), 160, (1.0, 0.55, 0.25)), (tuple(_orte["kerze"] + Vector((0, 0, 0.1))), 40, (1.0, 0.75, 0.45)),
                               (tuple(_orte["kerze2"] + Vector((0, 0, 0.1))), 30, (1.0, 0.75, 0.45)), ((0.1, -0.3, 2.1), 160, (1.0, 0.9, 0.78))],
                      schnitt=("hinten", "links"))


if __name__ == "__main__":
    main()
