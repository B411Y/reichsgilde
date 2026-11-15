"""Neubau Rathaus, Raum 6: Turm-Treppe (Turmschacht vom Erdgeschoss bis unter die Glockenstube) - im Spiel ein Teil von "InteriorRoot".

    blender -b --python tools/modelle/neubau/rathaus_turm_treppe.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/rathaus_turm_treppe_innen.glb:
    Raum_Turm           Bruchsteinschacht 3 x 3 m mit den Lichtschlitzen, Wendeltreppe aus Stein mit Spindel und Seil (42 Stufen bis
                        zur Uhrbuehne), Uhrbuehne aus Bohlen (nur hinter dem Austritt: darunter bleibt die Kopfhoehe)
    Uhrwerk_Turm        Zahnraeder im Holzgestell hinter dem Zifferblatt, Gewichte an Seilen, Leiter zur Luke der Glockenstube
    Anker: Blick_Auge/_Ziel, Tuer_Turm_Halle, Leiter_Turm_Spitze, Licht_*, Feuer_Fackel_*
"""
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rathaus_bib as B                                  # noqa: E402
import rathaus_masse as M                                # noqa: E402
from rathaus_bib import EZ, Teil, feld                   # noqa: E402

BI = B.BAENDER_RATHAUS_INNEN
X0, X1, Y0, Y1, ZB, ZD = M.RAEUME["turm_treppe"][:6]
C = Vector((M.W_MITTE[0], M.W_MITTE[1], 0.0))
TY = M.TUEREN["Tuer_Halle_Turm"][3][1]
TB, TH = M.TUER_LICHT
FACKELN = [(5.70, Y1, (0, -1, 0), ZB + 2.1), (X0, -3.30, (1, 0, 0), 5.6)]


def schlitze(wand):
    return [(a0, a1, z0, z1, False) for w, (a0, a1), (z0, z1), art, raum in M.OEFFNUNGEN if w == wand and raum == "turm_treppe"]


def polar(w, r, z):
    return C + Vector((math.cos(w) * r, math.sin(w) * r, z))


def rand_quadrat(w, halb=1.5):
    """Abstand von der Schachtmitte bis zur Wand in Richtung w (Quadrat 3 x 3 m)."""
    return halb / max(abs(math.cos(w)), abs(math.sin(w)))


def baue_raum():
    r = Teil("Raum_Turm", BI, saat=181)
    B.boden(r, "Innen", "platten", X0, X1, Y0, Y1, ZB, saat=182)
    B.innenwand(r, "Innen", (X0, Y0), (X1, Y0), (0, 1), ZB - 0.02, ZD, "bruchstein", schlitze("turm_vorn"), saat=183)
    B.innenwand(r, "Innen", (X1, Y0), (X1, Y1), (-1, 0), ZB - 0.02, ZD, "bruchstein", schlitze("turm_rechts"), saat=184)
    B.innenwand(r, "Innen", (X0, Y1), (X1, Y1), (0, -1), ZB - 0.02, ZD, "bruchstein", saat=185)
    B.innenwand(r, "Innen", (X0, Y0), (X0, Y1), (1, 0), ZB - 0.02, ZD, "bruchstein", [(TY[0], TY[1], ZB, ZB + TH, False)], saat=186)
    B.tuerrahmen(r, "Innen", "quader", (X0, (TY[0] + TY[1]) / 2, ZB), (0, 1, 0), (1, 0, 0), TB, TH)
    # Wendeltreppe: Keilstufen mit Unterseite, Spindel, Seil an der Wand
    v0, v1, m = BI["platten"]
    for i in range(M.W_STUFEN):
        w0 = M.W_START + i * M.W_WINKEL; w1 = w0 + M.W_WINKEL; wm = (w0 + w1) / 2
        zo = ZB + (i + 1) * M.W_STEIG; zu = zo - 0.24
        ia, ib = polar(w0, M.W_SPINDEL, zo), polar(w1, M.W_SPINDEL, zo)
        oa, om, ob = polar(w0, M.W_AUSSEN, zo), polar(wm, M.W_AUSSEN, zo), polar(w1, M.W_AUSSEN, zo)
        uv = lambda p: ((p - C).x / m, v1 - ((p - C).y + 1.5) / 3.0 * (v1 - v0))
        r.vieleck("Innen", [ia, oa, om, ib], [uv(ia), uv(oa), uv(om), uv(ib)], zu=C + EZ * (zo + 5))
        r.vieleck("Innen", [ib, om, ob], [uv(ib), uv(om), uv(ob)], zu=C + EZ * (zo + 5))
        unten = EZ * (zo - zu)
        r.vieleck("Innen", [ia - unten, oa - unten, oa, ia], [(0, v1), (1.3 / m, v1), (1.3 / m, v1 - 0.3 * (v1 - v0)), (0, v1 - 0.3 * (v1 - v0))],
                  zu=polar(w0 - 0.3, 0.8, zo - 0.1))                                        # Stirn (Setzstufe)
        r.vieleck("Innen", [ia - unten, ib - unten, om - unten, oa - unten], [uv(ia), uv(ib), uv(om), uv(oa)], zu=C + EZ * (zu - 5))
        r.vieleck("Innen", [ob - unten, om - unten, ib - unten], [uv(ob), uv(om), uv(ib)], zu=C + EZ * (zu - 5))
        if i % 2 == 0:                                                                     # Seil an der Wand, auf Pfosten
            p = polar(wm, M.W_AUSSEN + 0.02, zo + 0.90); q = polar(wm + 2 * M.W_WINKEL, M.W_AUSSEN + 0.02, zo + 2 * M.W_STEIG + 0.90)
            r.stab("Innen", p, q, 0.03, "stroh")
    r.dreh("Innen", (C.x, C.y, ZB), [(M.W_SPINDEL, 0.0), (M.W_SPINDEL, M.Z_TURM_UHR - ZB + 1.0)], "quader", seiten=10, gedreht=True)
    # Uhrbuehne: Bohlen ueber den Ecken und hinter dem Austritt (W_ENDE .. W_ENDE + BUEHNE_WINKEL bis an die Spindel)
    z = M.Z_TURM_UHR
    d0, d1, dm = BI["dielen"]
    schritt = math.radians(15)
    ende = M.W_ENDE % (2 * math.pi)
    for k in range(24):
        w0 = k * schritt; w1 = w0 + schritt; wm = (w0 + w1) / 2
        rel = (wm - ende) % (2 * math.pi)
        r_in = M.W_SPINDEL if rel < M.BUEHNE_WINKEL else M.W_AUSSEN
        pts = [polar(w0, r_in, z), polar(w0, rand_quadrat(w0), z), polar(w1, rand_quadrat(w1), z), polar(w1, r_in, z)]
        uvs = [((p - C).x / dm, d1 - ((p - C).y + 1.5) / 3.0 * (d1 - d0)) for p in pts]
        r.vieleck("Innen", pts, uvs, zu=C + EZ * (z + 5))
        r.vieleck("Innen", [p - EZ * M.BODEN_DICKE for p in pts], uvs, zu=C + EZ * (z - 5))
        if r_in > M.W_SPINDEL:
            r.vieleck("Innen", [pts[0], pts[3], pts[3] - EZ * M.BODEN_DICKE, pts[0] - EZ * M.BODEN_DICKE], [(0, d1)] * 4, zu=C + EZ * (z - 0.07))
    return r


def zahnrad(t, mitte, r, zaehne, dicke=0.05):
    """Zahnrad in der Wandebene (Blick +y): Scheibe mit Zaehnen, Vorder- und Rueckseite, aus Messing."""
    u = feld("messing")
    uv = [((u[0] + u[1]) / 2, BI["kleinteile"][1])] * 4
    n = zaehne * 2
    for k in range(n):
        w0, w1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        rr = r if k % 2 == 0 else r * 0.88
        a = mitte + Vector((math.cos(w0) * rr, 0, math.sin(w0) * rr)); b = mitte + Vector((math.cos(w1) * rr, 0, math.sin(w1) * rr))
        for s in (1, -1):
            off = Vector((0, s * dicke / 2, 0))
            t.vieleck("Innen", [mitte + off, a + off, b + off], uv[:3], zu=mitte + Vector((0, s * 3, 0)))
        t.vieleck("Innen", [a - Vector((0, dicke / 2, 0)), b - Vector((0, dicke / 2, 0)), b + Vector((0, dicke / 2, 0)), a + Vector((0, dicke / 2, 0))], uv, zu=mitte + (a + b - 2 * mitte) * 3)


def baue_uhrwerk():
    t = Teil("Uhrwerk_Turm", BI, saat=191)
    zu = M.Z_UHR_MITTE
    y = Y0 + 0.55
    # Gestell aus Eiche vor der Wand mit dem Zifferblatt
    for x in (5.00, 6.40):
        t.stab("Innen", (x, y, M.Z_TURM_UHR), (x, y, zu + 1.2), 0.12, "balken")
        t.stab("Innen", (x, Y0, zu + 1.2), (x, y + 0.1, zu + 1.2), 0.12, "balken")
    for z in (zu - 0.70, zu + 0.75):
        t.stab("Innen", (4.90, y, z), (6.50, y, z), 0.11, "balken")
    t.stab("Innen", (5.70, Y0, zu), (5.70, y + 0.25, zu), 0.05, "kleinteile", u_bereich=feld("eisen"))     # Welle zum Zifferblatt
    zahnrad(t, Vector((5.70, y + 0.12, zu)), 0.46, 24)
    zahnrad(t, Vector((5.18, y + 0.20, zu - 0.30)), 0.22, 12)
    zahnrad(t, Vector((6.15, y + 0.20, zu + 0.35)), 0.28, 14)
    # Rueckseite des Zifferblatts an der Wand
    for k in range(16):
        w0, w1 = 2 * math.pi * k / 16, 2 * math.pi * (k + 1) / 16
        c = Vector((5.70, Y0 + 0.02, zu))
        t.vieleck("Innen", [c, c + Vector((math.cos(w0), 0, math.sin(w0))) * M.UHR_R, c + Vector((math.cos(w1), 0, math.sin(w1))) * M.UHR_R], [(0.3, BI["balken"][1])] * 3, zu=c + Vector((0, 3, 0)))
    # Gewichte an Seilen
    for x, z_unten in ((4.75, 6.4), (6.65, 7.1)):
        t.stab("Innen", (x, y + 0.05, zu + 0.75), (x, y + 0.05, z_unten + 0.5), 0.015, "stroh")
        t.kasten("Innen", (x - 0.11, y - 0.06, z_unten), (x + 0.11, y + 0.16, z_unten + 0.5), "kerker")
    # Leiter von der Buehne zur Luke der Glockenstube
    fx, fy = M.LEITER_FUSS
    hoehe, neigung, sprosse, breite = M.LEITER
    lauf = hoehe / math.tan(neigung)
    for dx in (-breite / 2, breite / 2):
        t.stab("Innen", (fx + dx, fy, M.Z_TURM_UHR), (fx + dx, fy + lauf, M.Z_GLOCKE + 0.9), 0.06, "balken", breite=0.04)
    n = int(hoehe / sprosse)
    for i in range(1, n + 1):
        t.stab("Innen", (fx - breite / 2, fy + lauf * i * sprosse / hoehe, M.Z_TURM_UHR + i * sprosse), (fx + breite / 2, fy + lauf * i * sprosse / hoehe, M.Z_TURM_UHR + i * sprosse), 0.035, "balken")
    for fx2, fy2, n2, z in FACKELN:
        p = Vector((fx2, fy2, z)); nn = Vector(n2)
        t.stab("Innen", p - nn * 0.02, p + nn * 0.16 + EZ * 0.06, 0.03, "kleinteile", u_bereich=feld("eisen"))
        t.stab("Innen", p + nn * 0.16 - EZ * 0.15, p + nn * 0.16 + EZ * 0.20, 0.045, "balken", kappen=True)
    return t


def anker_raum():
    fx, fy = M.LEITER_FUSS
    k = [
        # auf der Wendel (Stufe 22, Lauflinie), Blick die Treppe hinab: Stufen, Spindel, Wand und Schlitz im Bild statt der Unterseiten
        B.anker("Blick_Auge", tuple(polar(M.W_START + 22.5 * M.W_WINKEL, 0.95, ZB + 23 * M.W_STEIG + 1.60)), fov=78.0),
        B.anker("Blick_Ziel", tuple(polar(M.W_START + 16.5 * M.W_WINKEL, 0.95, ZB + 17 * M.W_STEIG + 0.70))),
        B.anker("Tuer_Turm_Halle", (X0 + 0.02, (TY[0] + TY[1]) / 2, ZB + TH / 2), ziel="rathaus:halle", breite=TB, hoehe=TH, achse="x"),
        B.anker("Leiter_Turm_Spitze", (fx, fy + 0.4, M.Z_TURM_UHR + 1.6), ziel="rathaus:spitze", groesse="0.9,1.2,3.0"),
        B.anker("Licht_Raum", (5.70, -2.50, M.Z_TURM_UHR + 1.6), art="Raum"),
    ]
    for i, (x, y, n, z) in enumerate(FACKELN):
        p = Vector((x, y, z)) + Vector(n) * 0.16
        k.append(B.anker(f"Licht_Fackel_{i + 1}", tuple(p + EZ * 0.35 + Vector(n) * 0.10), art="Fackel"))
        k.append(B.anker(f"Feuer_Fackel_{i + 1}", tuple(p + EZ * 0.20)))
    return k


def main():
    opt = B.optionen()
    B.raum_bauen("turm_treppe", [baue_raum, baue_uhrwerk], anker_raum, opt)
    if "bilder" in opt:
        B.raum_bilder(opt["bilder"], "turm", "Raum_Turm", 99.0,
                      [("raumkamera", "Blick_Auge", "Blick_Ziel"), ("uhrbuehne", (4.6, -1.3, M.Z_TURM_UHR + 1.6), (6.0, -3.6, M.Z_UHR_MITTE))],
                      ((11.0, -9.0, 9.0), (5.7, -2.5, 5.0)),
                      mass=((X0 + 0.7, -2.2, ZB, 2.50, 0.66),),
                      lichter=[((5.7, -1.3, ZB + 2.4), 150), ((4.5, -3.3, 6.0), 150), ((5.7, -2.5, 9.9), 250)],
                      dazu=("rathaus.glb", "rathaus_eingangshalle_innen.glb"))


if __name__ == "__main__":
    main()
