"""Neubau Dom evangelisch, Huelle aussen: dreischiffige Hallenkirche aus Backstein unter einem grossen Schieferdach, Sockel, Ecken,
Gewaende und Vorhalle aus Sandstein, Chor mit 5/10-Schluss, ein runder Turm (16 Seiten) mit offener Glockenstube aus Holz und
Kupferhaube vorn links vor der Westwand.

    blender -b --python tools/modelle/neubau/dom_evangelisch_aussen.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/dom_evangelisch.glb mit den Knoten
    Haus      Mauern, Strebepfeiler, Turm, Vorhalle, Masswerk (keine Turmuhr: um 1400 gibt es sie hier nicht) (Materialien Bruchstein = Backstein, Sandstein, Bretter, Fachwerk, Firstblech = Kupfer)
    Dach      Schiefer in Stufen mit Alpha-Cards (Schieferkante): Halle, Chor, Vorhalle
    Scheiben  Fenster (Fensterlicht) und Bleinetz; die Innenansicht blendet sie aus
    Anker_Tuer, Anker_Laterne
und die gebackene Verdeckung in game/assets/texturen/neubau/dom_ao_atlas.png (Gruppe "dom", Platz dom_evangelisch in ao_atlas.json).
Alle Masse aus dom_evangelisch_masse.py, Bausteine aus dom_bib.py; Blatt dom_evangelisch_trim (gleiche Baender wie dom_trim, im Band
"mauer" Backstein; dom_trimsheet.py). Style-Guide: Monument 8.000-15.000 Dreiecke.
"""
import math
import os
import sys

import bpy
from mathutils import Vector as V

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rathaus_bib as B                                  # noqa: E402
import dom_bib as D                                      # noqa: E402
import dom_evangelisch_masse as M                        # noqa: E402
from rathaus_bib import EZ, mauer                        # noqa: E402
from rathaus_aussen import dachflaeche                   # noqa: E402

BA = D.BAENDER_DOM
MODELL = "dom_evangelisch"
BLATT = "dom_evangelisch_trim"
QUADER = lambda ph: {"links": ("Sandstein", "sandstein", ph), "rechts": ("Sandstein", "sandstein", ph + 1)}
LAIBUNG = ("Sandstein", "werkstein")
ZT, ZS = M.Z_TRAUFE, M.Z_STEIN


def giebel(h, stoff, band, o, eu, n, l, zt, zf, neigung, innen=None):
    """Giebelfeld ueber einer Wand der Laenge l (Ursprung o, Richtung eu, Sichtseite n) von der Traufe zt bis unter den First zf.
    Das Dach faellt beiderseits der Wandmitte mit `neigung`. `innen`: First eines niedrigeren Dachs gleicher Neigung, das mittig
    an den Giebel stoesst (Chor) - die Wand steht nur ausserhalb davon. Die Grenzen laufen je Abschnitt gerade (rathaus_bib.mauer)."""
    halb = lambda z: (zf - z) / neigung
    zk = min(max(zf - l / 2 * neigung, zt), zf)          # bis hier reicht das Dach ueber die Wandenden hinaus: volle Breite
    def stueck(z0, z1, g):
        if z1 - z0 > 1e-4: mauer(h, stoff, o, eu, n, l, z0, z1, band, grenze=g)
    if innen is None:
        stueck(zt, zk, lambda z: (0.0, l))
        stueck(zk, zf - 0.05, lambda z: (l / 2 - halb(z), l / 2 + halb(z)))
        return
    hi = lambda z: (innen - z) / neigung
    stueck(zt, zk, lambda z: (0.0, l / 2 - hi(z)))
    stueck(zt, zk, lambda z: (l / 2 + hi(z), l))
    stueck(zk, innen, lambda z: (l / 2 - halb(z), l / 2 - hi(z)))
    stueck(zk, innen, lambda z: (l / 2 + hi(z), l / 2 + halb(z)))
    stueck(innen, zf - 0.05, lambda z: (l / 2 - halb(z), l / 2 + halb(z)))


def fenster(h, s, name):
    """Scheiben, Gewaende und Masswerk aller Fenster einer Wand."""
    _, _, _, n, d = M.wand(name)
    n3 = V((n[0], n[1], 0))
    P = lambda s_, z, raus=0.0: V(M.punkt(name, s_, z, raus))
    for o in M.OEFFNUNGEN:
        if o[0] != name or o[5] == "portal": continue
        loch = D.loch_von(o)
        zu = P((loch.s0 + loch.s1) / 2, (loch.z0 + loch.z1) / 2, 5)
        D.scheibe(s, "Fenster", lambda a, z: P(a, z, -d / 2), loch, zu)
        D.scheibe(s, "Bleinetz", lambda a, z: P(a, z, -d / 2 + 0.006), loch, zu)
        D.gewaende(h, P, n3, loch)
        D.masswerk(h, lambda a, z: P(a, z, -d / 2 + 0.07), n3, loch)


def pfeiler(h, fuss, raus, z1, breite=0.70, tiefe=0.90):
    """Strebepfeiler aus Backstein auf dem Sandsteinsockel, Wasserschlag aus Sandstein."""
    f = V((fuss[0], fuss[1], 0.0)); r = V((raus[0], raus[1], 0.0)).normalized(); q = V((-r.y, r.x, 0.0))
    D.block(h, "Sandstein", "sandstein", f - q * breite / 2, q, r, breite, tiefe, 0.0, ZS, ohne=("v-", "unten", "oben"))
    D.strebepfeiler(h, fuss, raus, breite, tiefe, ZS, z1, schraege=1.0, stoff="Bruchstein", band="mauer")


def baue_haus(s):
    h = D.Teil("Haus", BA, saat=3)
    # ── Mauern: Sandsteinsockel bis Z_STEIN, darueber Backstein ──
    for name in M.WAENDE:
        o, e, l, n, d = M.wand(name)
        ls = [D.loch_von(x) for x in M.OEFFNUNGEN if x[0] == name and x[5] != "portal"]     # das Portal sitzt aussen in der Vorhalle
        ecken = QUADER(0) if name in ("west", "seite_l", "seite_r") else None
        mauer(h, "Sandstein", o, e, n, l, 0.0, ZS, "sandstein")
        mauer(h, "Bruchstein", o, e, n, l, ZS, ZT, "mauer", ls, laibung=LAIBUNG, dicke=d / 2, ecken=ecken)
        fenster(h, s, name)
        # Sockelgesims und Traufgesims (an der Westwand ausserhalb der Vorhalle)
        a, b = V((o[0], o[1], 0)), V((o[0] + e[0] * l, o[1] + e[1] * l, 0))
        e3 = (b - a).normalized()
        stuecke = [(a, b)] if name != "west" else [(a - e3 * 0.06, a + e3 * (l / 2 - M.VORHALLE_X)), (a + e3 * (l / 2 + M.VORHALLE_X), b + e3 * 0.06)]
        for p, q in stuecke:
            D.gesimsband(h, (p.x, p.y), (q.x, q.y), n, ZS - 0.02, hoehe=0.14, vor=0.06, ueber=0.06 if name != "west" else 0.0)
        D.gesimsband(h, (a.x, a.y), (b.x, b.y), n, ZT - 0.24, ueber=0.10)
    # ── Giebel: Westen ueber der ganzen Breite, Osten um das Chordach herum ──
    o, e, l, n, _ = M.wand("west")
    giebel(h, "Bruchstein", "mauer", o, e, n, l, ZT, M.Z_FIRST, M.NEIGUNG)
    giebel(h, "Bruchstein", "mauer", (-M.X_AUSSEN, M.Y_OST), (1, 0), (0, 1), 2 * M.X_AUSSEN, ZT, M.Z_FIRST, M.NEIGUNG, innen=M.Z_CHORFIRST)
    strebewerk(h)
    vorhalle(h)
    turm(h, s)
    return h


def strebewerk(h):
    for sx in (-1, 1):
        for y in (M.Y_WEST + 0.35,) + M.PFEILER_Y + (M.Y_OST - 0.35,):
            pfeiler(h, (sx * M.X_AUSSEN, y), (sx, 0), 8.2)
    for k in range(6):                                     # Chor: radial an jeder Ecke des Schlusses
        x, y = M.apsis_ecke(k)
        r = (math.cos(math.radians(36 * k)), math.sin(math.radians(36 * k)))
        pfeiler(h, (x - r[0] * 0.05, y - r[1] * 0.05), r, 7.6, breite=0.60)


def vorhalle(h):
    """Vorhalle aus Sandstein vor dem Portal: Stirnwand mit Rundbogen, darin die zweifluegelige Tuer, Giebel, Stufen."""
    yv, hx = M.VORHALLE_Y, M.VORHALLE_X
    zt, zf = M.VORHALLE_Z
    b, z0, z1 = M.PORTAL
    tief = M.Y_WEST - yv
    mauer(h, "Sandstein", (-hx, M.Y_WEST), (0, -1), (-1, 0), tief, 0.0, zt, "sandstein")
    mauer(h, "Sandstein", (hx, yv), (0, 1), (1, 0), tief, 0.0, zt, "sandstein")
    for sx in (-1, 1):
        D.gesimsband(h, (sx * hx, yv), (sx * hx, M.Y_WEST), (sx, 0), zt - 0.20, hoehe=0.20, vor=0.10)
    bogen = D.Spitzloch(hx - b / 2, hx + b / 2, z0, zt, (zt - z1) / (b / 2), sohlbank=False)      # Kaempfer am Sturz der Tuer, Scheitel an der Traufe
    mauer(h, "Sandstein", (-hx, yv), (1, 0), (0, -1), 2 * hx, 0.0, zt, "sandstein", [bogen], laibung=LAIBUNG, dicke=0.45)
    giebel(h, "Sandstein", "sandstein", (-hx, yv), (1, 0), (0, -1), 2 * hx, zt, zf, 1.0)
    P = lambda s_, z, raus=0.0: V((-hx + s_, yv - raus, z))
    D.gewaende(h, P, V((0, -1, 0)), bogen, breite=0.20, vor=0.05)
    # Tuerblaetter in der Laibung, darueber das Bogenfeld
    for seite in (-1, 1):
        B.bretttuer(h, "Bretter", "planken", "Bretter", "eisen", (seite * b / 4, yv + 0.40, z0), (1, 0, 0), (0, -1, 0), b / 2 - 0.01, z1 - z0, bretter=4)
    v0, v1, mm = BA["werkstein"]
    kurs = (v1 - v0) * mm
    zs = sorted({z for z in bogen.hoehen() if z >= z1 - 1e-6} | {z1 + kurs * i for i in range(1, int((zt - z1) / kurs) + 1)})
    PB = lambda s_, z: V((-hx + s_, yv + 0.43, z))
    for za, zb in zip(zs[:-1], zs[1:]):
        if zb - za < 1e-5: continue
        (a0, a1), (b0, b1) = bogen.breite_bei(za + 1e-6), bogen.breite_bei(zb - 1e-6)
        lage = math.floor((za - z1) / kurs + 1e-6)
        vv = lambda z: v1 - (z - z1 - lage * kurs) / kurs * (v1 - v0)
        pts = [PB(a0, za), PB(a1, za), PB(b1, zb), PB(b0, zb)]
        uvs = [(a0 / mm + 0.3 * lage, vv(za)), (a1 / mm + 0.3 * lage, vv(za)), (b1 / mm + 0.3 * lage, vv(zb)), (b0 / mm + 0.3 * lage, vv(zb))]
        if b1 - b0 < 1e-4: pts, uvs = pts[:3], uvs[:3]
        h.vieleck("Sandstein", pts, uvs, zu=PB(hx, z1 + 0.5) - V((0, 5, 0)))
    D.gesimsband(h, (b / 2, yv + 0.43), (-b / 2, yv + 0.43), (0, -1), z1 - 0.02, hoehe=0.16, vor=0.10)          # Sturz
    for i in range(3):                                     # Stufen: i = 0 unterste, breiteste
        tiefe = 0.6 + 0.35 * (2 - i)
        D.block(h, "Sandstein", "sandstein", (-2.0, yv - tiefe), (1, 0), (0, 1), 4.0, tiefe, 0.0, 0.1 * (i + 1), ohne=("v+", "unten"))


def turm_ecken(r):
    cx, cy = M.TURM_M; n = M.TURM_SEITEN
    return [(cx + r * math.cos(2 * math.pi * (k + 0.5) / n), cy + r * math.sin(2 * math.pi * (k + 0.5) / n)) for k in range(n)]


def turm(h, s):
    """Runder Turm: Sandsteinsockel, Schaft aus Backstein mit Schlitzfenstern, Gesims, offene Glockenstube aus Holz, Kupferhaube."""
    cx, cy = M.TURM_M; n_ = M.TURM_SEITEN
    zg0, zg1 = M.Z_GLOCKEN
    c = V((cx, cy, 0))
    sockel, schaft = turm_ecken(M.TURM_R + 0.12), turm_ecken(M.TURM_R)
    hinten = n_ // 4 - 1                                   # die Seite, die 3 cm hinter der Westwand liegt (Mitte bei 90 Grad)
    z_frei = math.floor((M.Z_FIRST - (abs(cx) + 0.6) * M.NEIGUNG) / 1.2) * 1.2      # darunter verdeckt sie der Westgiebel ganz
    schlitze = {11: 7.2, 9: 11.4, 13: 15.0}                # Seite -> Bruestung eines Schlitzfensters (vorn und schraeg vorn)
    wv0, wv1, wm = BA["werkstein"]
    for k in range(n_):
        k2 = (k + 1) % n_
        w = 2 * math.pi * (k + 1) / n_
        n = (math.cos(w), math.sin(w)); n3 = V((n[0], n[1], 0))
        for ecken, z0, z1, stoff, band in ((sockel, 0.0, M.Z_TURMSOCKEL, "Sandstein", "sandstein"), (schaft, M.Z_TURMSOCKEL, zg0, "Bruchstein", "mauer")):
            a, b = ecken[k2], ecken[k]                     # von aussen gesehen von links nach rechts
            l = math.hypot(b[0] - a[0], b[1] - a[1]); eu = ((b[0] - a[0]) / l, (b[1] - a[1]) / l)
            if k == hinten:
                if band == "sandstein": continue
                z0 = z_frei
            ls = [D.Spitzloch(l / 2 - 0.24, l / 2 + 0.24, schlitze[k], schlitze[k] + 1.5, 1.3)] if band == "mauer" and k in schlitze else []
            mauer(h, stoff, a, eu, n, l, z0, z1, band, ls, laibung=LAIBUNG, dicke=0.30, saat=k + 1)
            P = lambda s_, z, raus=0.0, a=a, eu=eu, n=n: V((a[0] + eu[0] * s_ + n[0] * raus, a[1] + eu[1] * s_ + n[1] * raus, z))
            for loch in ls:
                D.gewaende(h, P, n3, loch, breite=0.12)
                zu = P(l / 2, loch.z0, 5)
                D.scheibe(s, "Fenster", lambda q, z, P=P: P(q, z, -0.30), loch, zu)
                D.scheibe(s, "Bleinetz", lambda q, z, P=P: P(q, z, -0.294), loch, zu)
        # Absatz ueber dem Sockel (Wasserschlag)
        if k != hinten:
            pa, pb, qa, qb = V((*sockel[k2], M.Z_TURMSOCKEL)), V((*sockel[k], M.Z_TURMSOCKEL)), V((*schaft[k2], M.Z_TURMSOCKEL + 0.16)), V((*schaft[k], M.Z_TURMSOCKEL + 0.16))
            lang = (pb - pa).length
            h.vieleck("Sandstein", [pa, pb, qb, qa], [(0, wv1), (lang / wm, wv1), (lang / wm, wv1 - 0.2 / wm), (0, wv1 - 0.2 / wm)], zu=(pa + pb) / 2 + n3 * 3 + EZ * 3)
        # Gesims unter der Glockenstube
        D.gesimsband(h, schaft[k2], schaft[k], n, zg0 - 0.30, hoehe=0.30, vor=0.18, ueber=0.18 * math.tan(math.pi / n_))
    # ── Glockenstube: Boden, Pfosten und Rahmen aus Eiche, Bruestung aus Brettern, dahinter der dunkle Raum ──
    rand, pfosten = turm_ecken(M.TURM_R + 0.18), turm_ecken(M.TURM_R - 0.16)
    zr = zg1 - 0.25                                        # Unterkante des Rahmens
    for k in range(n_):
        k2 = (k + 1) % n_
        w = 2 * math.pi * (k + 1) / n_
        n3 = V((math.cos(w), math.sin(w), 0))
        D.farbflaeche(h, "Sandstein", [c + EZ * (zg0 + 0.001), V((*schaft[k], zg0)), V((*schaft[k2], zg0))], "holz_dunkel", zu=c + EZ * 60)
        x, y = pfosten[k]
        h.stab("Fachwerk", (x, y, zg0), (x, y, zr), 0.22, "balken")
        a, b = V((*pfosten[k2], 0)), V((*pfosten[k], 0))
        h.stab("Fachwerk", a + EZ * (zr + 0.115), b + EZ * (zr + 0.115), 0.23, "balken", breite=0.22)   # 2 cm unter der Haube (keine deckungsgleichen Flaechen)
        e = (b - a).normalized()
        h.rechteck("Bretter", a + e * 0.11 + EZ * zg0, e, EZ, (b - a).length - 0.22, 0.95, "planken", zu=(a + b) / 2 + n3 * 5 + EZ * zg0)
        h.stab("Fachwerk", a + e * 0.11 + EZ * (zg0 + 1.0), b - e * 0.11 + EZ * (zg0 + 1.0), 0.10, "balken", breite=0.14)       # Handlauf
        # Unterseite der Haube ueber dem Umgang
        ia, ib = c + (V((*pfosten[k2], 0)) - c) * 0.8, c + (V((*pfosten[k], 0)) - c) * 0.8
        ha, hb = c + (V((*rand[k2], 0)) - c) * 1.09, c + (V((*rand[k], 0)) - c) * 1.09      # bis unter die Traufe der Haube
        D.farbflaeche(h, "Sandstein", [ia + EZ * zg1, ib + EZ * zg1, hb + EZ * zg1, ha + EZ * zg1], "holz_dunkel", zu=c)
    h.dreh("Sandstein", (cx, cy, 0), [(M.TURM_R - 0.55, zg0), (M.TURM_R - 0.55, zg1)], "kleinteile", seiten=n_, u_bereich=D.feld("nacht"), deckel=False, glatt=False)
    # ── Kupferhaube (welsche Haube): ausschwingende Traufe, Bauch, Einzug, Spitze ──
    r = M.TURM_R
    zh, zsp = M.Z_HAUBE, M.Z_SPITZE
    t = lambda anteil: zh + (zsp - zh) * anteil
    profil = [(r + 0.60, zh), (r + 0.32, t(0.035)), (r + 0.16, t(0.10)), (r + 0.06, t(0.22)), (r - 0.30, t(0.36)), (r - 0.95, t(0.48)), (r - 1.70, t(0.57)),
              (0.60, t(0.64)), (0.30, t(0.76)), (0.13, t(0.90)), (0.0, zsp)]
    h.dreh("Firstblech", (cx, cy, 0), profil, "kleinteile", seiten=n_, u_bereich=D.feld("kupfer"), deckel=False, glatt=False)
    D.farbstab(h, "Firstblech", (cx, cy, zsp - 0.5), (cx, cy, M.Z_KREUZ), 0.07, "gold")
    D.farbkasten(h, "Firstblech", (cx - 0.15, cy - 0.15, zsp - 0.22), (cx + 0.15, cy + 0.15, zsp + 0.08), "gold")


def baue_dach():
    d = D.Teil("Dach", BA, saat=11)
    zt = ZT
    xe, xc = M.X_AUSSEN + 0.30, M.X_ARKADE + 0.30
    for sx in (-1, 1):
        # Halle: ein Dach ueber alle drei Schiffe
        dachflaeche(d, (sx * xe, M.Y_WEST, zt), (sx * xe, M.Y_OST, zt), (0, M.Y_WEST, M.Z_FIRST), (0, M.Y_OST, M.Z_FIRST), (sx * 40, 0, 60), saat=1 + sx)
        # Chor
        dachflaeche(d, (sx * xc, M.Y_OST, zt), (sx * xc, M.Y_APSIS, zt), (0, M.Y_OST, M.Z_CHORFIRST), (0, M.Y_APSIS, M.Z_CHORFIRST), (sx * 40, 0, 60), saat=5 + sx)
        # Vorhalle (45 Grad)
        vx = M.VORHALLE_X + 0.25
        dachflaeche(d, (sx * vx, M.VORHALLE_Y, M.VORHALLE_Z[0]), (sx * vx, M.Y_WEST, M.VORHALLE_Z[0]), (0, M.VORHALLE_Y, M.VORHALLE_Z[1]), (0, M.Y_WEST, M.VORHALLE_Z[1]),
                    (sx * 40, 0, 60), stufen=2, saat=8 + sx)
    spitze = (0, M.Y_APSIS, M.Z_CHORFIRST)
    for k in range(5):                                     # Chorschluss: fuenf Flaechen zum Firstpunkt
        a, b = M.apsis_ecke(k, M.R_APSIS + 0.30), M.apsis_ecke(k + 1, M.R_APSIS + 0.30)
        m = ((a[0] + b[0]) * 2, M.Y_APSIS + (a[1] + b[1] - 2 * M.Y_APSIS) * 2, 60)
        dachflaeche(d, (*a, zt), (*b, zt), spitze, spitze, m, saat=20 + k)
    return d


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
    summe = haus.dreiecke() + dach.dreiecke() + scheiben.dreiecke()
    print(f"DREIECKE aussen: Haus {haus.dreiecke()}, Dach {dach.dreiecke()}, Scheiben {scheiben.dreiecke()}, Summe {summe} (Budget {M.BUDGET[0]}-{M.BUDGET[1]})")
    o_haus, o_dach = haus.objekt(st), dach.objekt(st)
    o_scheiben = scheiben.objekt(st, mit_farbe=False)
    bpy.ops.mesh.primitive_plane_add(size=120, location=(0, 0, 0))
    boden = bpy.context.active_object; boden.name = "Kontrollboden"
    boden.data.materials.append(B.stoff("Kontrollboden", farbe=(0.36, 0.33, 0.28)))
    B.uv2_und_ao([([o_haus, o_dach], [boden])], "dom", MODELL, mit_ao="ohne-ao" not in opt)
    anker = [
        B.anker("Anker_Tuer", (0.0, M.VORHALLE_Y - 2.2, 0.02), blick="nord"),
        B.anker("Anker_Laterne", (M.PORTAL[0] / 2 + 0.28, M.VORHALLE_Y - 0.05, 2.6), blick="sued"),
    ]
    B.exportiere([o_haus, o_dach, o_scheiben] + anker, os.path.join(B.ZIEL_MODELL, MODELL + ".glb"))
    if "bilder" in opt:
        o = opt["bilder"]; os.makedirs(o, exist_ok=True)
        B.kontrollbild(os.path.join(o, "domev_aussen_artwork_blick.jpg"), (-36.0, -48.0, 40.0), (-0.5, -1.0, 10.0), brennweite=40)
        B.kontrollbild(os.path.join(o, "domev_aussen_portal_nah.jpg"), (4.0, -30.0, 2.4), (-2.0, -12.0, 7.0), brennweite=24)
        B.kontrollbild(os.path.join(o, "domev_aussen_chor.jpg"), (30.0, 40.0, 30.0), (0.0, 4.0, 9.0), brennweite=40)
        B.kontrollbild(os.path.join(o, "domev_aussen_haube.jpg"), (-16.0, -34.0, 30.0), (M.TURM_M[0], M.TURM_M[1], 24.0), brennweite=50)


if __name__ == "__main__":
    main()
