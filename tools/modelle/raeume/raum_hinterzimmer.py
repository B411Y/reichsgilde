"""Hinterzimmer des Wohnhauses Mensch (Raum der Stufe 2, Grundform 1).

    blender -b --factory-startup --python tools/modelle/raeume/raum_hinterzimmer.py -- [--bilder=<ordner>] [--ohne-ao] [--proben=64]

Schreibt game/assets/modelle/gebaeude/neubau/wohnhaus_mensch_s1_g1_hinterzimmer_innen.glb (im Spiel Knoten „InteriorRoot_Hinterzimmer“
im Haus, Stadtansicht.Wohnraeume.cs) und Zelle 0 des AO-Atlas wohnhaus_mensch_raeume_innen_ao_atlas.png.

Vorlage: Artwork des Nutzers „hinterzimmer.webp“ (nur Stil): Fachwerk mit Steinsockel, roter Teppich, runder Tisch mit Karte,
Briefen, Kerze und Dolch; Waffenstaender, Kisten, Faesser und Saecke, eiserne Truhe, kleiner Ofen, Wandborde. Masse aus
raeume_masse.py (Soll/Ist dort). Der Kartentisch ist der Gegenstand des Raums (Klick: Hinterzimmer-Tafel); alles andere ist Ausstattung.
"""
import math
import os
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
_flammen = {}


def baue_raum():
    """Huelle: Dielenboden, Bruchsteinsockel, Kalkputz mit sichtbarem Fachwerk, Bretterdecke mit Balken, Fensterlaibungen, Teppich,
    die Tuer zur Wohnstube."""
    r = Teil("Raum", B, saat=31)
    R.boden(r, "dielen", -M.IX, M.IX, M.IV, M.IH, ZB, laengs="y")
    R.waende(r, "putz", sockel="bruchstein", sockel_hoehe=0.55)
    R.decke(r)
    R.fensterlaibung(r, "balken")
    # Fachwerk: Schwelle auf dem Sockel, Eckstaender, Staender, Rahm unter der Decke, Streben in den freien Feldern
    z_schwelle = ZB + 0.55
    n_innen = {"vorn": (0, 1, 0), "hinten": (0, -1, 0), "links": (1, 0, 0), "rechts": (-1, 0, 0)}
    for wand, ((ox, oy), (ex, ey), laenge) in R.WAENDE.items():
        n = n_innen[wand]
        a0 = ox if ex else oy
        P = lambda a, z, w=wand: R.wandpunkt(w, a, z)
        k = 0.07 if wand in ("vorn", "hinten") else 0.0     # Gassen- und Rueckwand enden vor den Hoelzern der Seitenwaende (sonst deckungsgleich in den Ecken)
        r.brett(S, P(a0 + k, z_schwelle + 0.06), P(a0 + laenge - k, z_schwelle + 0.06), 0.12, n, "balken", tiefe=0.05)
        r.brett(S, P(a0 + k, M.DECKE - 0.08), P(a0 + laenge - k, M.DECKE - 0.08), 0.14, n, "balken", tiefe=0.06)
        offen = [(f[0] - 0.12, f[1] + 0.12) for f in R.fenster_der_wand(wand)]
        if wand == M.RAUM_TUER["Hinterzimmer"][0]:
            y0, y1 = M.RAUM_TUER["Hinterzimmer"][1]; offen.append((y0 - 0.12, y1 + 0.12))
        staender = [a0 + laenge * k / 3 for k in (1, 2)]          # an den Ecken stehen Steinpfeiler
        for s in staender:
            if any(o0 < s < o1 for o0, o1 in offen): continue
            r.brett(S, P(s, z_schwelle + 0.12), P(s, M.DECKE - 0.15), 0.14, n, "balken", tiefe=0.05)
        # Strebe im ersten freien Feld jeder Wand
        for k in range(3):
            s0, s1 = a0 + laenge * k / 3 + 0.1, a0 + laenge * (k + 1) / 3 - 0.1
            if any(not (s1 < o0 or s0 > o1) for o0, o1 in offen): continue
            r.brett(S, P(s0, z_schwelle + 0.14), P(s1, M.DECKE - 0.20), 0.12, n, "balken", tiefe=0.045)
            break
    # Eckpfeiler aus Bruchstein (nicht in der Ecke des Ofens)
    for sx, sy in ((-1, -1), (1, -1), (-1, 1)):
        xa, xb = sorted((sx * M.IX, sx * (M.IX - 0.26)))
        yw = M.IV if sy < 0 else M.IH
        ya, yb = sorted((yw, yw - sy * 0.26))
        wand_x, wand_y = ("-x" if sx < 0 else "+x"), ("-y" if sy < 0 else "+y")
        raum_x, raum_y = ("+x" if sx < 0 else "-x"), ("+y" if sy < 0 else "-y")
        kurs = (B["bruchstein"][1] - B["bruchstein"][0]) * B["bruchstein"][2]           # Lagenhoehe = Hoehe des Bandes (keine Streckung)
        z = ZB
        while z < M.DECKE - 0.16:
            zn = min(z + kurs, M.DECKE - 0.15)
            r.kasten(S, (xa, ya, z), (xb, yb, zn), "bruchstein", ohne=("-z", "+z", wand_x, wand_y, raum_x), laengs="x")
            r.kasten(S, (xa, ya, z), (xb, yb, zn), "bruchstein", ohne=("-z", "+z", wand_x, wand_y, raum_y), laengs="y")
            z = zn
    # Teppich mit Borte (Band "teppich": quer Borte, Feld, Borte), leicht ueber dem Boden
    x0, x1, y0, y1 = M.HZ_TEPPICH
    v0, v1, m = B["teppich"]
    breite = x1 - x0
    r.vieleck(S, [(x0, y0, ZB + 0.008), (x0, y1, ZB + 0.008), (x1, y1, ZB + 0.008), (x1, y0, ZB + 0.008)],
              [(0.1, v1), (0.1 + (y1 - y0) / m, v1), (0.1 + (y1 - y0) / m, v0), (0.1, v0)], zu=(0, 0, 9))
    # Tuer zur Wohnstube (Rueckweg)
    wand, (ty0, ty1) = M.RAUM_TUER["Hinterzimmer"]
    k0, k1, _ = B["kleinteile"]
    wx, innen = (-M.IX, 1) if wand == "links" else (M.IX, -1)
    mitte = zimmertuer(r, S, "dielen", "balken", "kleinteile", R.feld("eisen"), wx, ty0, ty1, ZB, M.TUER_H, innen, klinke_rechts=False)
    _flammen["tuer"] = mitte
    return r


def baue_kartentisch():
    """Gegenstand_Kartentisch: runder Tisch auf Saeule und Kreuzfuss, darauf die Landkarte, Briefe mit Siegel, Kerze, Dolch, Weinflasche."""
    c = Teil("Gegenstand_Kartentisch", B, saat=41)
    tx, ty, tr, th = M.HZ_TISCH
    z = ZB
    c.dreh(S, (tx, ty, z + th - 0.045), [(tr, 0.0), (tr, 0.045)], "moebel", seiten=20, glatt=False)
    c.dreh(S, (tx, ty, z + th - 0.045), [(0.0, 0.0), (tr, 0.0)], "moebel", seiten=20, deckel=False, glatt=False)
    c.dreh(S, (tx, ty, z + 0.06), [(0.075, 0.0), (0.06, 0.25), (0.08, 0.40), (0.06, 0.55), (0.09, th - 0.105)], "moebel", seiten=8, deckel=False)
    for w in (0, 90, 180, 270):
        a = math.radians(w + 45)
        c.stab(S, (tx, ty, z + 0.05), (tx + math.cos(a) * 0.40, ty + math.sin(a) * 0.40, z + 0.025), 0.07, "moebel", breite=0.08, kappen=True)
    zt = z + th + 0.002
    # Landkarte: Pergament, um 18 Grad gedreht, an zwei Ecken leicht aufgerollt
    p0, p1, pm = B["papier"]
    w = math.radians(18)
    ex, ey = Vector((math.cos(w), math.sin(w), 0)), Vector((-math.sin(w), math.cos(w), 0))
    mk = Vector((tx - 0.02, ty + 0.02, zt))
    b, h = 0.66, 0.44
    ka, kb = KARTE_U
    ecken = [mk - ex * b / 2 - ey * h / 2, mk + ex * b / 2 - ey * h / 2, mk + ex * b / 2 + ey * h / 2, mk - ex * b / 2 + ey * h / 2]
    c.vieleck(S, ecken, [(ka, p1), (kb, p1), (kb, p0), (ka, p0)], zu=mk + Vector((0, 0, 1)))
    c.stab(S, ecken[3] + ey * 0.015 + Vector((0, 0, 0.018)), ecken[2] + ey * 0.015 + Vector((0, 0, 0.018)), 0.036, "papier")   # aufgerollter Rand
    # Briefe mit Siegel
    sa, sb = SCHRIFT_U
    for (dx, dy, dw, bb, hh) in ((0.30, -0.28, -30, 0.20, 0.15), (-0.30, -0.26, 25, 0.18, 0.13), (0.18, 0.32, 70, 0.16, 0.12)):
        w2 = math.radians(dw)
        e1, e2 = Vector((math.cos(w2), math.sin(w2), 0)), Vector((-math.sin(w2), math.cos(w2), 0))
        m2 = Vector((tx + dx, ty + dy, zt + 0.003))
        pts = [m2 - e1 * bb / 2 - e2 * hh / 2, m2 + e1 * bb / 2 - e2 * hh / 2, m2 + e1 * bb / 2 + e2 * hh / 2, m2 - e1 * bb / 2 + e2 * hh / 2]
        c.vieleck(S, pts, [(sa, p1), (sa + (sb - sa) * 0.6, p1), (sa + (sb - sa) * 0.6, p0 + (p1 - p0) * 0.3), (sa, p0 + (p1 - p0) * 0.3)], zu=m2 + Vector((0, 0, 1)))
        c.dreh(S, m2 + e2 * 0.02, [(0.018, 0.0), (0.016, 0.006)], "kleinteile", seiten=8, u_bereich=R.feld("siegel"))
    # Kerze im Leuchter
    _flammen["kerze"] = R.kerze(c, tx + 0.22, ty + 0.08, zt, hoch=0.12)
    # Dolch: Klinge (Stahl), Parierstange, Griff
    d0 = Vector((tx - 0.18, ty - 0.10, zt + 0.012)); d1 = d0 + Vector((0.20, -0.07, 0))
    c.stab(S, d0, d1, 0.006, "kleinteile", breite=0.028, oben=(0, 0, 1), u_bereich=R.feld("stahl"))
    c.stab(S, d0 + Vector((0.01, 0.035, 0)), d0 + Vector((-0.01, -0.035, 0)), 0.014, "kleinteile", u_bereich=R.feld("messing"))
    c.stab(S, d0, d0 - Vector((0.10, -0.035, 0)), 0.022, "leder", kappen=True)
    # Weinflasche und Becher
    R.flasche(c, tx - 0.36, ty + 0.18, zt, farbe="wein", hoch=0.28, r=0.045)
    c.dreh(S, (tx - 0.26, ty + 0.30, zt), [(0.03, 0.0), (0.035, 0.08), (0.033, 0.085)], "kleinteile", seiten=8, deckel=False, u_bereich=R.feld("messing"))
    return c


def baue_sitzgruppe():
    """Vier Stuehle um den Tisch und ein Schemel."""
    c = Teil("Sitzgruppe", B, saat=43)
    for (x, y, blick) in M.HZ_STUEHLE:
        R.stuhl(c, "moebel", (x, y, ZB), blick, sitz=M.HZ_SITZ, breite=0.44, tiefe=0.42, lehne=1.02)
    for (x, y) in M.HZ_SCHEMEL:
        R.schemel(c, "moebel", x, y, sitz=M.HZ_SITZ)
    return c


def baue_waffenstaender():
    """Waffenstaender an der linken Wand: Gestell mit Kerbleiste, drei Schwerter, zwei Spiesse, Rundschild an der Wand."""
    c = Teil("Waffenstaender", B, saat=45)
    y0, y1 = M.HZ_WAFFEN
    x = -M.IX
    for y in (y0, y1):
        c.kasten(S, (x + 0.03, y - 0.025, ZB), (x + 0.32, y + 0.025, ZB + 0.06), "balken")      # Fuss schmaler als der Pfosten (nicht deckungsgleich)
        c.kasten(S, (x + 0.02, y - 0.03, ZB), (x + 0.08, y + 0.03, ZB + 1.45), "balken")
    for z, tiefe in ((ZB + 0.30, 0.30), (ZB + 1.22, 0.10)):
        c.kasten(S, (x + 0.03, y0 - 0.025, z - 0.03), (x + tiefe, y1 + 0.025, z + 0.03), "balken", laengs="y")
    ue_stahl, ue_messing = R.feld("stahl"), R.feld("messing")
    for i, y in enumerate((y0 + 0.14, y0 + 0.30, y0 + 0.46)):
        fuss = Vector((x + 0.25, y, ZB + 0.33)); kopf = Vector((x + 0.09, y, ZB + 1.30))
        achse = (kopf - fuss).normalized()
        c.stab(S, fuss, fuss + achse * 0.78, 0.008, "kleinteile", breite=0.045, oben=(1, 0, 0), u_bereich=ue_stahl)             # Klinge
        p = fuss + achse * 0.80
        c.stab(S, p + Vector((0, -0.09, 0)), p + Vector((0, 0.09, 0)), 0.02, "kleinteile", u_bereich=ue_messing)                 # Parierstange
        c.stab(S, p, p + achse * 0.18, 0.028, "leder", kappen=True)                                                              # Griff
        c.dreh(S, p + achse * 0.19, [(0.0, 0.0), (0.025, 0.012), (0.0, 0.03)], "kleinteile", seiten=6, u_bereich=ue_messing)     # Knauf
    for y in (y1 - 0.18, y1 - 0.06):
        a = Vector((x + 0.22, y, ZB + 0.05)); b = Vector((x + 0.06, y, ZB + 1.95))
        c.stab(S, a, b, 0.032, "balken")
        e = (b - a).normalized()
        c.stab(S, b, b + e * 0.22, 0.006, "kleinteile", breite=0.05, oben=(1, 0, 0), u_bereich=ue_stahl)
    # Rundschild an der Wand ueber dem Gestell
    m = Vector((x + 0.02, (y0 + y1) / 2, ZB + 1.80))
    for k in range(14):
        w0, w1 = 2 * math.pi * k / 14, 2 * math.pi * (k + 1) / 14
        pa = m + Vector((0, math.cos(w0) * 0.26, math.sin(w0) * 0.26)); pb = m + Vector((0, math.cos(w1) * 0.26, math.sin(w1) * 0.26))
        v0, v1, _ = B["moebel"]
        c.vieleck(S, [m + Vector((0.02, 0, 0)), pa, pb], [(0.5, v0), (0.2 + 0.02 * k, v1), (0.22 + 0.02 * k, v1)], zu=m + Vector((1, 0, 0)))
    return c


def baue_garderobe():
    """Hakenleiste an der rechten Wand mit Mantel und Hut (Ausstattung)."""
    c = Teil("Garderobe", B, saat=55)
    x = M.IX
    y0, y1, z = 0.05, 0.85, ZB + 1.68
    c.kasten(S, (x - 0.04, y0, z - 0.05), (x, y1, z + 0.05), "balken", ohne=("+x",), laengs="y")
    for y in (y0 + 0.15, (y0 + y1) / 2, y1 - 0.15):
        c.stab(S, (x - 0.04, y, z), (x - 0.13, y, z + 0.04), 0.025, "balken", kappen=True)
    # Mantel am mittleren Haken: drei Bahnen, unten weiter, leicht gewellt
    v0, v1, _ = B["leder"]
    ym = (y0 + y1) / 2
    oben = [Vector((x - 0.10, ym + d, z + 0.02)) for d in (-0.10, -0.03, 0.03, 0.10)]
    unten = [Vector((x - 0.05 - 0.07 * (i % 2), ym + d, ZB + 0.55)) for i, d in enumerate((-0.30, -0.10, 0.10, 0.30))]
    for i in range(3):
        c.vieleck(S, [unten[i], unten[i + 1], oben[i + 1], oben[i]], [(0.1 + 0.1 * i, v1), (0.2 + 0.1 * i, v1), (0.2 + 0.1 * i, v0), (0.1 + 0.1 * i, v0)], zu=Vector((x - 1, ym, 1.2)))
    # Hut am vorderen Haken
    c.dreh(S, (x - 0.16, y0 + 0.15, z - 0.02), [(0.0, -0.02), (0.17, 0.0), (0.17, 0.01), (0.09, 0.02), (0.085, 0.12), (0.0, 0.13)], "leder", seiten=12, deckel=False)
    return c


def baue_vorrat():
    """Faesser, Kisten und Saecke an der Rueckwand links (neben der Tuer zur Wohnstube)."""
    c = Teil("Vorrat_Set", B, saat=47)
    ue = R.feld("eisen")
    R.fass(c, "balken", ue, -1.22, M.IH - 0.29, ZB, hoehe=0.80, r=0.26)
    R.fass(c, "balken", ue, -0.74, M.IH - 0.27, ZB, hoehe=0.70, r=0.23)
    R.kiste(c, "dielen", ue, -0.46, M.IH - 0.44, ZB, -0.03, M.IH - 0.02, ZB + 0.44)
    R.kiste(c, "dielen", ue, -0.40, M.IH - 0.38, ZB + 0.44, -0.09, M.IH - 0.08, ZB + 0.72)
    R.kiste(c, "dielen", ue, -1.38, M.IH - 0.36, ZB + 0.80, -1.04, M.IH - 0.06, ZB + 1.06)
    R.sack(c, -1.00, M.IH - 0.72, ZB, r=0.19, hoehe=0.52)
    R.sack(c, -0.52, M.IH - 0.64, ZB, r=0.17, hoehe=0.44)
    return c


def baue_truhe():
    """Eiserne Truhe an der Gassenwand: Kasten mit Nietbaendern, gewoelbter Deckel, Vorhangschloss."""
    c = Teil("Truhe_Eisen", B, saat=49)
    ax0, ax1, _, _, h = M.HZ_TRUHE
    x0, x1, y0, y1 = -ax1, -ax0, M.IV + 0.02, M.IV + (M.HZ_TRUHE[3] - M.HZ_TRUHE[2])   # an der Gassenwand gebaut, dann an die Rueckwand gedreht
    ue, us = R.feld("eisen"), R.feld("stahl")
    k0, k1, _ = B["kleinteile"]
    c.kasten(S, (x0, y0, ZB), (x1, y1, ZB + h - 0.12), "kleinteile", ohne=("-z",))
    for fl in c.flaechen[-5:]: fl[1][:] = [(us[0], k1), (us[1], k1), (us[1], k0), (us[0], k0)]   # Kasten aus Eisenblech (hell), Baender dunkel
    prof = [(y0, ZB + h - 0.12), (y0 + 0.08, ZB + h - 0.02), (y1 - 0.08, ZB + h - 0.02), (y1, ZB + h - 0.12)]
    for (ya, za), (yb, zb) in zip(prof[:-1], prof[1:]):
        c.vieleck(S, [(x0, ya, za), (x1, ya, za), (x1, yb, zb), (x0, yb, zb)], [(us[0], k1), (us[1], k1), (us[1], k0), (us[0], k0)], zu=((x0 + x1) / 2, (y0 + y1) / 2 + (ya + yb - y0 - y1), ZB + h + 1))
    for x in (x0, x1):
        c.vieleck(S, [(x, p[0], p[1]) for p in prof], [(us[0], k1), (us[1], k1), (us[1], k0), (us[0], k0)], zu=(x + (1 if x == x1 else -1), (y0 + y1) / 2, ZB + h))
    # Baender aus Schmiedeeisen (dunkel, vorstehend)
    for x in (x0 + 0.10, (x0 + x1) / 2, x1 - 0.10):
        c.kasten(S, (x - 0.025, y1, ZB + 0.02), (x + 0.025, y1 + 0.008, ZB + h - 0.12), "kleinteile", ohne=("-y",))
        for fl in c.flaechen[-5:]: fl[1][:] = [(ue[0], k1), (ue[1], k1), (ue[1], k0), (ue[0], k0)]
    xm = (x0 + x1) / 2
    c.kasten(S, (xm - 0.05, y1 + 0.008, ZB + h - 0.30), (xm + 0.05, y1 + 0.03, ZB + h - 0.18), "kleinteile")
    for fl in c.flaechen[-6:]: fl[1][:] = [(us[0], k1), (us[1], k1), (us[1], k0), (us[0], k0)]
    c.stab(S, (xm - 0.03, y1 + 0.02, ZB + h - 0.18), (xm + 0.03, y1 + 0.02, ZB + h - 0.12), 0.01, "kleinteile", u_bereich=us)
    R.drehen_180(c)
    return c


def baue_ofen():
    """Kleiner Ofen in der Ecke rechts hinten: gemauerter Koerper mit Feuerloch zum Raum (nach vorn, -Y), Glut und Scheite,
    Rauchfang in Lagen bis unter die Decke, ein Topf obenauf."""
    c = Teil("Ofen", B, saat=51)
    x0, x1, y0, y1, h = M.HZ_OFEN
    zf, zo = ZB + 0.30, ZB + 0.72                      # Feuerloch von 0,30 bis 0,72 m ueber dem Boden
    ox0, ox1 = x0 + 0.15, x1 - 0.15
    tiefe = 0.32
    c.kasten(S, (x0, y0, ZB), (x1, y1, zf), "bruchstein", ohne=("-z", "+x", "+y"), laengs="x")
    c.kasten(S, (x0, y0, zo), (x1, y1, ZB + h), "bruchstein", ohne=("-z", "+x", "+y"), laengs="x")
    c.kasten(S, (x0, y0, zf), (ox0, y1, zo), "bruchstein", ohne=("-z", "+z", "+x", "+y"), laengs="x")
    c.kasten(S, (ox1, y0, zf), (x1, y1, zo), "bruchstein", ohne=("-z", "+z", "+x", "+y", "-x"), laengs="x")
    # Feuerraum innen (dunkel), Glut am Boden, zwei Scheite
    ut, ug = R.feld("tinte"), R.feld("glut")
    k0, k1, _ = B["kleinteile"]
    yi = y0 + tiefe
    dunkel = [(ut[0], k1), (ut[1], k1), (ut[1], k0), (ut[0], k0)]
    mitte = ((ox0 + ox1) / 2, (y0 + yi) / 2, (zf + zo) / 2)
    for pts in ([(ox0, yi, zf), (ox1, yi, zf), (ox1, yi, zo), (ox0, yi, zo)], [(ox0, y0, zf), (ox0, yi, zf), (ox0, yi, zo), (ox0, y0, zo)],
                [(ox1, y0, zf), (ox1, yi, zf), (ox1, yi, zo), (ox1, y0, zo)], [(ox0, y0, zo), (ox1, y0, zo), (ox1, yi, zo), (ox0, yi, zo)]):
        c.vieleck(S, pts, dunkel, zu=mitte)
    c.vieleck(S, [(ox0, y0, zf + 0.005), (ox1, y0, zf + 0.005), (ox1, yi, zf + 0.005), (ox0, yi, zf + 0.005)], [(ug[0], k1), (ug[1], k1), (ug[1], k0), (ug[0], k0)], zu=(0, 0, 9))
    for a, b in (((ox0 + 0.04, y0 + 0.08, zf + 0.04), (ox1 - 0.05, yi - 0.06, zf + 0.05)), ((ox0 + 0.05, yi - 0.07, zf + 0.05), (ox1 - 0.04, y0 + 0.10, zf + 0.06))):
        c.stab(S, a, b, 0.06, "balken", kappen=True)
    _flammen["ofen"] = Vector(((ox0 + ox1) / 2, (y0 + yi) / 2, zf + 0.04))
    # Sims aus Sandstein ueber dem Feuerloch
    c.kasten(S, (x0 - 0.03, y0 - 0.05, ZB + h), (x1, y1, ZB + h + 0.06), "quader", ohne=("+x", "+y"), laengs="x")
    # Rauchfang in Lagen von der Hoehe des Bandes (keine Streckung), schmaler als der Ofen
    kurs = (B["bruchstein"][1] - B["bruchstein"][0]) * B["bruchstein"][2]
    z = ZB + h + 0.06
    while z < M.DECKE - 0.16:
        zn = min(z + kurs, M.DECKE - 0.15)
        c.kasten(S, (x0 + 0.14, y0 + 0.16, z), (x1, y1, zn), "bruchstein", ohne=("-z", "+z", "+x", "+y"), laengs="x")
        z = zn
    # Topf auf dem Sims
    c.dreh(S, (x0 + 0.20, y0 + 0.08 + 0.02, ZB + h + 0.06), [(0.09, 0.0), (0.11, 0.07), (0.10, 0.14), (0.11, 0.15)], "kleinteile", seiten=10, deckel=False, u_bereich=R.feld("eisen"))
    return c


def baue_borde():
    """Zwei Wandborde ueber der Truhe: Flaschen, Krug, Buecher (an der Gassenwand gebaut, dann an die Rueckwand gedreht)."""
    c = Teil("Wandborde", B, saat=53)
    x0, x1, hoehen = -M.HZ_BORDE[1], -M.HZ_BORDE[0], M.HZ_BORDE[2]
    y = M.IV
    for h in hoehen:
        c.kasten(S, (x0, y, ZB + h - 0.03), (x1, y + 0.22, ZB + h), "dielen", ohne=("-y",), laengs="x")
        for x in (x0 + 0.10, x1 - 0.10):
            c.vieleck(S, [(x - 0.015, y, ZB + h - 0.03), (x + 0.015, y, ZB + h - 0.03), (x + 0.015, y + 0.16, ZB + h - 0.03), (x - 0.015, y, ZB + h - 0.20)],
                      [(0.1, B["balken"][1]), (0.12, B["balken"][1]), (0.12, B["balken"][0]), (0.1, B["balken"][0])], zu=(x, y + 1, ZB + h - 0.1))
    z1 = ZB + hoehen[0]
    for i, (dx, farbe, hoch) in enumerate(((0.12, "glas", 0.24), (0.24, "wein", 0.20), (0.34, "glas", 0.18))):
        R.flasche(c, x0 + dx, y + 0.11, z1, farbe=farbe, hoch=hoch, r=0.04)
    R.buchreihe(c, x0 + 0.55, x0 + 0.95, y + 0.03, 0.17, z1, 0.18, 0.24, saat=3, achse="x", innen=1)
    c.dreh(S, (x1 - 0.18, y + 0.11, z1), [(0.06, 0.0), (0.08, 0.06), (0.07, 0.14), (0.05, 0.18), (0.055, 0.2)], "kleinteile", seiten=9, deckel=False, u_bereich=R.feld("messing"))
    z2 = ZB + hoehen[1]
    R.buchreihe(c, x0 + 0.08, x0 + 0.62, y + 0.03, 0.17, z2, 0.16, 0.26, saat=5, achse="x", innen=1)
    for i, dx in enumerate((0.78, 0.92)):
        R.flasche(c, x0 + dx, y + 0.10, z2, farbe="glas" if i else "wein", hoch=0.22, r=0.038)
    R.drehen_180(c)
    return c


def baue_glas():
    g = Teil("Glas", B, saat=7)
    for wand in ("vorn", "links"):
        for a0, a1, z0, z1 in R.fenster_der_wand(wand):
            R.rechteckglas(g, wand, a0, a1, z0, z1)
    return g


def anker():
    eye, ziel = M.HZ_KAMERA
    tx, ty, _, th = M.HZ_TISCH
    wand, (ty0, ty1) = M.RAUM_TUER["Hinterzimmer"]
    return [
        R.anker("Blick_Auge", eye), R.anker("Blick_Ziel", ziel),
        R.anker("Licht_Raum", (0.0, -0.4, M.DECKE - 0.35), art="Raum"),
        R.anker("Licht_Kerze", tuple(_flammen["kerze"] + Vector((0, 0, 0.12))), art="Kerze"),
        R.anker("Feuer_Kerze_1", tuple(_flammen["kerze"])),
        R.anker("Licht_Ofen", tuple(_flammen["ofen"] + Vector((0, -0.35, 0.25))), art="Kamin"),
        R.anker("Feuer_Ofen_1", tuple(_flammen["ofen"])),
        R.anker("Tuer_Hinterzimmer_Wohnstube", tuple(_flammen["tuer"]), ziel="Wohnstube", breite=M.TUER_B, hoehe=M.TUER_H, achse="x"),
    ]


def main():
    opt = R.optionen()
    teile = [baue_raum, baue_kartentisch, baue_sitzgruppe, baue_waffenstaender, baue_garderobe, baue_vorrat, baue_truhe, baue_ofen, baue_borde, baue_glas]
    R.raum_bauen("hinterzimmer", teile, anker, opt)
    if "bilder" in opt:
        eye, ziel = M.HZ_KAMERA
        tx, ty, _, _ = M.HZ_TISCH
        R.raum_bilder(opt["bilder"], "hinterzimmer",
                      [("raumkamera", eye, ziel), ("ofen", eye, (1.55, 1.80, 0.9)), ("tuer", eye, (-1.98, 1.30, 1.0))],
                      ((4.2, -5.0, 5.4), (0.0, 0.1, 0.5)),
                      mass=[(-1.55, 1.30, ZB, 1.75, 0.44), (0.95, -1.20, ZB, 1.75, 0.44), (1.05, 0.95, ZB, 2.50, 0.62)],
                      lichter=[(tuple(_flammen["kerze"] + Vector((0, 0, 0.1))), 40, (1.0, 0.75, 0.45)), (tuple(_flammen["ofen"] + Vector((0, -0.35, 0.2))), 120, (1.0, 0.55, 0.25)),
                               ((0.0, -0.4, 2.1), 160, (1.0, 0.9, 0.78))])


if __name__ == "__main__":
    main()
