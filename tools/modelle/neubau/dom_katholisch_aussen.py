"""Neubau Dom katholisch, Huelle aussen: dreischiffige Basilika mit Doppelturmfassade, Querhaus, Chor mit 5/10-Schluss,
Strebepfeilern mit Fialen und Strebeboegen ueber den Seitenschiffen; grauer Werkstein, Gliederung in gelblichem Sandstein, Schiefer.

    blender -b --python tools/modelle/neubau/dom_katholisch_aussen.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/dom_katholisch.glb mit den Knoten
    Haus      Mauern, Strebewerk, Tuerme, Portal, Masswerk, Glocken (keine Turmuhren: um 1400 gibt es sie hier nicht) (Materialien Bruchstein, Sandstein, Bretter, Fachwerk, Firstblech)
    Dach      Schiefer in Deckreihen von 0,25 m (D.schieferflaeche), Alpha-Cards (Schieferkante) an der Traufe: Hochschiff, Querhaus, Chor, Pultdaecher, Turmhelme
    Scheiben  Fenster (Fensterlicht) und Bleinetz; die Innenansicht blendet sie aus
    Anker_Tuer, Anker_Laterne
und die gebackene Verdeckung in game/assets/texturen/neubau/dom_ao_atlas.png (Gruppe "dom", ao_atlas.json).
Alle Masse aus dom_masse.py, Bausteine aus dom_bib.py; Style-Guide: Monument 8.000-15.000 Dreiecke.
"""
import math
import os
import sys

import bpy
from mathutils import Vector as V

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rathaus_bib as B                                  # noqa: E402
import dom_bib as D                                      # noqa: E402
import dom_masse as M                                    # noqa: E402
from rathaus_bib import EZ, mauer                        # noqa: E402

BA = D.BAENDER_DOM
MODELL = "dom_katholisch"
QUADER = lambda ph: {"links": ("Sandstein", "sandstein", ph), "rechts": ("Sandstein", "sandstein", ph + 1)}
LAGE = 0.30                      # Lagenhoehe des Schiefers wie am Wohnhaus und beim Dom evangelisch (Nutzer 25.11.2026: Reihen im Pixel-Look lesbar)
Z_QUERFIRST = M.Z_TRAUFE + ((M.Y_QUER[1] - M.Y_QUER[0]) / 2 + 0.30) * M.NEIGUNG


def giebel(h, name, zf, halb):
    """Giebeldreieck ueber einer Wand: von der Traufe bis zum First zf, das Dach faellt beiderseits der Wandmitte mit NEIGUNG.
    Die Grenze knickt dort, wo das Dreieck die Wandbreite erreicht; innerhalb einer Lage muss sie gerade laufen, darum zwei Teile
    (sonst blieb ein Dreieck neben dem Turm offen, Pruefung 25.11.2026)."""
    l = M.wand(name)[2]
    g = lambda z: (l / 2 - (zf - z) / M.NEIGUNG, l / 2 + (zf - z) / M.NEIGUNG)
    zk = zf - (l / 2) * M.NEIGUNG
    zs = [M.Z_TRAUFE] + ([zk] if M.Z_TRAUFE + 0.01 < zk < zf - 0.01 else []) + [zf]
    for za, zb in zip(zs, zs[1:]):
        D.aussenwand(h, M, name, za, zb, grenze=g, laibung=None)


def fenster(h, s, name):
    """Scheiben, Gewaende und Masswerk aller Oeffnungen einer Wand."""
    _, _, _, n, d = M.wand(name)
    n3 = V((n[0], n[1], 0))
    P = lambda s_, z, raus=0.0: V(M.punkt(name, s_, z, raus))
    for o in M.OEFFNUNGEN:
        if o[0] != name or o[5] == "portal": continue
        loch = D.loch_von(o)
        zu = P((loch.s0 + loch.s1) / 2, (loch.z0 + loch.z1) / 2, 5)
        D.scheibe(s, "Fenster", lambda a, z: P(a, z, -d / 2), loch, zu)
        D.scheibe(s, "Bleinetz", lambda a, z: P(a, z, -d / 2 + 0.006), loch, zu)
        if o[5] == "rose":
            D.rose(h, lambda a, z, vor=0.0: P(a, z, vor - d / 2 + 0.10), n3, loch)
        else:
            D.gewaende(h, P, n3, loch)
            D.masswerk(h, lambda a, z: P(a, z, -d / 2 + 0.07), n3, loch)


def baue_haus(s):
    h = D.Teil("Haus", BA, saat=3)
    # ── Mauern ──
    for name in ("seite_l", "seite_r"):
        D.aussenwand(h, M, name, 0.0, M.Z_SEITE)
    for name in ("ober_l", "ober_r"):
        D.aussenwand(h, M, name, M.Z_PULT - 0.30, M.Z_TRAUFE)
    for seite in "lr":
        D.aussenwand(h, M, f"quer_{seite}_w", 0.0, M.Z_TRAUFE)
        # ueber dem Pultdach (darunter liegt in derselben Ebene die Ostwand des Seitenschiffs): s laeuft von der Hochschiffwand nach aussen
        # in zwei Teilen, geteilt bei Z_PULT - 0,25: Dort knickt die Grenze; innerhalb einer Lage muss sie gerade laufen (sonst bleibt ein Dreieck offen)
        for za, zb in ((M.Z_SEITE - 0.25, M.Z_PULT - 0.25), (M.Z_PULT - 0.25, M.Z_TRAUFE)):
            D.aussenwand(h, M, f"quer_{seite}_w_oben", za, zb,
                         grenze=lambda z, links=(seite == "l"): (lambda g: (M.X_AUSSEN - M.X_ARKADE - g, M.X_AUSSEN - M.X_ARKADE) if links else (0.0, g))(
                             min(max((z + 0.25 - M.Z_SEITE) / (M.Z_PULT - M.Z_SEITE), 0.0), 1.0) * (M.X_AUSSEN - M.X_ARKADE)) if z < M.Z_PULT - 0.25 else (0.0, M.X_AUSSEN - M.X_ARKADE))
        D.aussenwand(h, M, f"quer_{seite}_o", 0.0, M.Z_TRAUFE)
        D.aussenwand(h, M, f"quer_{seite}_g", 0.0, M.Z_TRAUFE, ecken=QUADER(0))
        giebel(h, f"quer_{seite}_g", Z_QUERFIRST, 0)
        D.aussenwand(h, M, f"chor_{seite}", 0.0, M.Z_TRAUFE)
    for k in range(5):
        D.aussenwand(h, M, f"apsis_{k}", 0.0, M.Z_TRAUFE)
    D.aussenwand(h, M, "west", 0.0, M.Z_TRAUFE)
    giebel(h, "west", M.Z_FIRST, 0)
    # Die Tuerme springen je Geschoss um 0,12 zurueck: die Westwand reicht dort bis hinter ihre Flanken (sonst bliebe ein Spalt)
    zt_, N_, xe_ = M.Z_TRAUFE, M.NEIGUNG, M.X_ARKADE + 0.30
    for x0 in (M.X_TURM[0] + 0.45, -M.X_TURM[0]):
        def dach_grenze(z, x0=x0):
            """Streifen s (laeuft nach -x, 0,45 breit): die Oberkante folgt der Dachschraege des Hochschiffs (|x| <= R, 5 cm darunter)."""
            R = xe_ - max(z - zt_, 0.0) / N_
            return (min(max(x0 - R, 0.0), 0.45), 0.45) if x0 > 0 else (0.0, min(max(R - M.X_TURM[0], 0.0), 0.45))
        zk = zt_ + (xe_ - (M.X_TURM[0] + 0.45)) * N_                 # hier beginnt die Schraege den Streifen zu kuerzen
        zo = zt_ + (xe_ - M.X_TURM[0]) * N_                          # hier ist er ganz weg
        for za, zb in ((M.TURM_STUFEN[1], zk), (zk, zo)):
            mauer(h, "Bruchstein", (x0, M.Y_WESTWAND), (-1, 0), (0, -1), 0.45, za, zb, "mauer", grenze=dach_grenze)
    # Die Ostwand der Tuerme springt je Geschoss um 0,12 zurueck, die Hochschiffwand beginnt bei Y_TURM: in der Ebene |x| = X_ARKADE blieb eine
    # lotrechte Fuge (Pruefer 25.11.2026). Verschluss je Geschoss ueber dem Pultdach, Sichtseite nach aussen (|x| > X_ARKADE).
    for sx in (-1, 1):
        for i, (zu_, zo_) in enumerate(((M.Z_PULT - 0.30, M.TURM_STUFEN[2]), (M.TURM_STUFEN[2], M.Z_TRAUFE))):
            e_ = 0.12 * (i + 1)
            h.rechteck("Bruchstein", V((sx * M.X_ARKADE, M.Y_TURM - e_, zu_)), V((0, 1, 0)), EZ, e_, zo_ - zu_, "mauer",
                       zu=V((sx * (M.X_ARKADE + 3), M.Y_TURM - e_ / 2, (zu_ + zo_) / 2)))
    # Tasche zwischen Westwand-Giebel und Turmflanke (0,24 m breit, 0,26 m tief): ausgefuellt bis unter die Dachschraege
    for sx in (-1, 1):
        zt_s = M.Z_TRAUFE + (M.X_ARKADE + 0.30 - M.X_TURM[0]) * M.NEIGUNG
        D.block(h, "Bruchstein", "mauer", (sx * M.X_TURM[0] - (0.24 if sx < 0 else 0.0), M.Y_WESTWAND - 0.26), (1, 0), (0, 1), 0.24, 0.26, M.TURM_STUFEN[2], zt_s,
                ohne=("unten", "u+" if sx > 0 else "u-", "v+"))
    for name in M.WAENDE:
        fenster(h, s, name)
        # Sockel und Traufgesims
        (ox, oy), (ex, ey), l, n, _ = M.wand(name)
        a, b = (ox, oy), (ox + ex * l, oy + ey * l)
        if not name.startswith("ober") and not name.endswith("oben") and name != "west":
            D.gesimsband(h, a, b, n, 0.0, hoehe=0.45, vor=0.08, stoff="Bruchstein", band="sockel", ueber=0.08, unten=False)
        zt = M.Z_SEITE if name.startswith("seite") else M.Z_TRAUFE
        if name != "west" and not name.endswith("_g"):
            D.gesimsband(h, a, b, n, zt - 0.24, ueber=0.10)
            if name in ("quer_l_o", "quer_r_o", "quer_l_w", "quer_r_w"):   # offene Stirn des Gesimses am Querhausgiebel schliessen (Blick von der Seite unter das Dach)
                ende, anfang = (a, b) if abs(a[0]) > abs(b[0]) else (b, a)            # das Ende am Giebel (|x| = X_QUER)
                ed = (V((ende[0], ende[1], 0)) - V((anfang[0], anfang[1], 0))).normalized()
                c = V((ende[0], ende[1], zt - 0.24)) + ed * 0.10
                h.rechteck("Sandstein", c, V((n[0], n[1], 0)), EZ, 0.12, 0.22, "werkstein", zu=c + ed * 3 + EZ * 0.11)
    # Kaffgesims der Westwand ueber dem Portal und unter dem Giebel
    for z in (7.4, M.Z_TRAUFE):
        D.gesimsband(h, (M.X_TURM[0], M.Y_WESTWAND), (-M.X_TURM[0], M.Y_WESTWAND), (0, -1), z - 0.22)
    strebewerk(h)
    portal(h)
    for sx in (-1, 1):
        turm(h, s, sx)
    return h


def strebewerk(h):
    # Seitenschiffe: Pfeiler mit Fiale, Strebebogen zur Hochschiffwand, dort eine Lisene
    for sx in (-1, 1):
        for i in range(1, M.JOCHE):
            y = M.Y_TURM + i * M.JOCH
            D.strebepfeiler(h, (sx * M.X_AUSSEN, y), (sx, 0), 0.60, 0.90, 0.0, 6.4, schraege=0.7, fiale=(0.50, 2.6, 1.5))
            h.stab("Sandstein", (sx * (M.X_AUSSEN + 0.62), y, 8.75), (sx * M.X_ARKADE, y, 12.5), 0.34, "werkstein", breite=0.36)
            D.block(h, "Sandstein", "sandstein", (sx * M.X_ARKADE - (0.22 if sx < 0 else 0.0), y - 0.27), (1, 0), (0, 1), 0.22, 0.54, M.Z_PULT - 0.3, M.Z_TRAUFE - 0.24,
                    ohne=("unten", "oben", "u+" if sx < 0 else "u-"))
        # Querhaus: Pfeiler an den Giebelecken (vor dem Giebel)
        for y in (M.Y_QUER[0] + 0.32, M.Y_QUER[1] - 0.32):
            D.strebepfeiler(h, (sx * M.X_QUER, y), (sx, 0), 0.64, 0.50, 0.0, 10.6, schraege=0.9, fiale=(0.45, 2.4, 1.5))
    # Chor: radial an jeder Ecke des Schlusses
    for k in range(6):
        x, y = M.apsis_ecke(k)
        r = (math.cos(math.radians(36 * k)), math.sin(math.radians(36 * k)))
        D.strebepfeiler(h, (x - r[0] * 0.05, y - r[1] * 0.05), r, 0.60, 0.90, 0.0, 10.4, schraege=1.0, fiale=(0.48, 2.6, 1.6))


def portal(h):
    y = M.Y_WESTWAND
    b, z0, z1 = M.PORTAL
    # Tuerblaetter (zweifluegelig) in der Laibung
    for seite in (-1, 1):
        B.bretttuer(h, "Bretter", "planken", "Bretter", "eisen", (seite * b / 4, y + 0.30, z0), (1, 0, 0), (0, -1, 0), b / 2 + 0.01, z1 - z0, bretter=4)
    # Schwelle unter den Tuerblaettern: sonst blickt man unter der Tuer durch (kein Boden im Modell)
    D.block(h, "Sandstein", "sandstein", (-b / 2, y - 0.30), (1, 0), (0, 1), b, 1.40, 0.0, z0 + 0.01, ohne=("unten",))
    # Gewaende: Pfosten mit Fialen, Spitzbogen mit Bogenfeld ueber dem Sturz, darueber der Wimperg (Giebel)
    halb = b / 2 + 0.36
    vorn = y - 0.30
    for seite in (-1, 1):
        x0 = -halb if seite < 0 else b / 2
        D.block(h, "Sandstein", "sandstein", (x0, vorn), (1, 0), (0, 1), 0.36, 0.30, 0.0, z1, ohne=("v+", "unten"))
        D.fiale_bauen(h, (seite * (halb - 0.18), y - 0.15), 0.36, z1, 2.3, 1.4)
    zw0, zw1 = z1, 7.15
    breit = b / 2
    # Giebelfeld (heller Werkstein) zwischen den Fialen, davor das Bogenfeld in grauem Stein im Sandsteinbogen
    mauer(h, "Sandstein", (halb, y - 0.12), (-1, 0), (0, -1), 2 * halb, zw0, zw1, "sandstein", zbasis=zw0,
          grenze=lambda z: (halb - halb * (zw1 - z) / (zw1 - zw0), halb + halb * (zw1 - z) / (zw1 - zw0)))
    for seite in (-1, 1):                                  # Seiten des Giebelfelds zur Wand
        a = V((seite * halb, y - 0.12, zw0)); sp = V((0, y - 0.12, zw1))
        h.rechteck("Sandstein", a, sp - a, V((0, 1, 0)), (sp - a).length, 0.12, "werkstein", zu=(a + sp) / 2 + V((seite * 3, 0, 3)))
    bogen = D.Spitzloch(breit - b / 2 + 0.10, breit + b / 2 - 0.10, zw0, zw0 + 2.15, 1.45)
    PB = lambda s_, z, raus=0.0: V((breit - s_, y - 0.13 - raus, z))
    mv0, mv1, mm = BA["mauer"]
    kurs = (mv1 - mv0) * mm
    zs = sorted(bogen.hoehen() | {zw0 + kurs})
    for za, zb in zip(zs[:-1], zs[1:]):                    # Bogenfeld in Streifen, Lagen wie die Mauer
        (a0, a1), (b0, b1) = bogen.breite_bei(za + 1e-6), bogen.breite_bei(zb - 1e-6)
        lage = math.floor((za - zw0) / kurs + 1e-6)
        vv = lambda z: mv1 - (z - zw0 - lage * kurs) / kurs * (mv1 - mv0)
        pts = [PB(a0, za), PB(a1, za), PB(b1, zb), PB(b0, zb)]
        uvs = [(a0 / mm + 0.3 * lage, vv(za)), (a1 / mm + 0.3 * lage, vv(za)), (b1 / mm + 0.3 * lage, vv(zb)), (b0 / mm + 0.3 * lage, vv(zb))]
        if b1 - b0 < 1e-4: pts, uvs = pts[:3], uvs[:3]
        h.vieleck("Bruchstein", pts, uvs, zu=PB(breit, zw0 + 1, 5))
    D.gewaende(h, PB, V((0, -1, 0)), bogen, breite=0.20, vor=0.05)
    D.gesimsband(h, (breit, y - 0.12), (-breit, y - 0.12), (0, -1), zw0 - 0.02, hoehe=0.16, vor=0.10)       # Sturz
    D.fiale_bauen(h, (0.0, y - 0.07), 0.22, zw1 - 0.15, 0.5, 0.9)
    # Stufen vor dem Portal
    for i in range(3):                                     # i = 0 unterste, breiteste Stufe
        tiefe = 0.6 + 0.35 * (2 - i)
        D.block(h, "Sandstein", "sandstein", (-M.X_TURM[0], y - tiefe), (1, 0), (0, 1), 2 * M.X_TURM[0], tiefe, 0.0, 0.1 * (i + 1), ohne=("v+", "unten"))   # bis an die Turmflanken (sonst ein offener Schacht daneben)


def wimperg_dicke(h, a, eu, n, hw, zh, d=0.20, hoch=3.0):
    """Rueckwand des Wimpergs: ein Dreieck um `d` hinter der Aussenwand (vorher eine zweite Wand in derselben Ebene: die Pruefung sah
    ihre Rueckseite) und die beiden Schraegen als Deckstreifen dazwischen, damit man von oben nicht in das Giebeldreieck blickt."""
    P = lambda s_, z, raus=0.0: V((a[0] + eu[0] * s_ + n[0] * raus, a[1] + eu[1] * s_ + n[1] * raus, z))
    mitte = P(hw, zh + 1.0, -2.0)
    ecken = [P(0, zh, -d), P(2 * hw, zh, -d), P(hw, zh + hoch, -d)]
    v0, v1, m = h.baender["sandstein"]
    h.vieleck("Sandstein", ecken, [(q / m, (v0 + v1) / 2) for q in (0.0, 2 * hw, hw)], zu=mitte)
    for links in (True, False):
        s0 = 0.0 if links else 2 * hw
        richt = (P(hw, zh + hoch) - P(s0, zh)).normalized()
        laenge = (P(hw, zh + hoch) - P(s0, zh)).length
        o = P(s0, zh)
        tief = V((-n[0], -n[1], 0))
        aus = (richt.cross(tief)).normalized()
        if aus.z < 0: aus = -aus
        h.rechteck("Sandstein", o, richt, tief, laenge, d, "werkstein", zu=(o + richt * laenge / 2 + tief * d / 2) + aus * 3)


def turm(h, s, sx):
    xa, xb = (M.X_TURM if sx > 0 else (-M.X_TURM[1], -M.X_TURM[0]))
    cx, cy = (xa + xb) / 2, (M.Y_WEST + M.Y_TURM) / 2
    aussen_x = xb if sx > 0 else xa
    stufen = list(M.TURM_STUFEN) + [M.Z_GLOCKEN[1]]
    for i in range(len(stufen) - 1):
        e = 0.12 * i
        z0, z1 = stufen[i], stufen[i + 1]
        x0, x1, y0, y1 = xa + e, xb - e, M.Y_WEST + e, M.Y_TURM - e
        seiten = (((x0, y0), (x0, y1), "x0"), ((x0, y1), (x1, y1), "y1"), ((x1, y1), (x1, y0), "x1"), ((x1, y0), (x0, y0), "y0"))
        for a, b, wo in seiten:
            l = math.hypot(b[0] - a[0], b[1] - a[1])
            eu = ((b[0] - a[0]) / l, (b[1] - a[1]) / l)
            n = (-eu[1], eu[0])
            sichtbar = wo == "y0" or (wo == "x1" and sx > 0) or (wo == "x0" and sx < 0)      # West- und Aussenseite
            ls = []
            if i == 3:
                ls = [D.Spitzloch(l / 2 - 1.45 + k * 1.70, l / 2 - 1.45 + k * 1.70 + 1.20, z0 + 1.0, z0 + 4.4, 1.3, sohlbank=True) for k in (0, 1)]
            elif i in (1, 2) and sichtbar:
                ls = [D.Spitzloch(l / 2 - 0.35, l / 2 + 0.35, z0 + 1.0, z0 + 3.4, 1.3)]
            # Seiten zur Kirche hin: nur, was von aussen zu sehen ist - sonst laegen sie in der Ebene der Innenwaende (Vorhalle, Seitenschiff)
            kw = {}
            innenseite = (wo == "x0" and sx > 0) or (wo == "x1" and sx < 0)
            if innenseite and i < 2:
                vor = M.Y_WESTWAND - M.Y_WEST - e                     # der Streifen, um den der Turm vor der Westwand steht
                kw["s_bereich"] = (0.0, vor) if sx > 0 else (l - vor, l)
            if wo == "y1" and i < 2:
                def ueber_pult(z, a=a, eu=eu, l=l):
                    """Ostseite: sichtbar nur ueber dem Pultdach des Seitenschiffs (s laeuft entlang x)."""
                    if z < M.Z_SEITE - 0.25: return (0.0, 0.0)
                    xg = M.X_AUSSEN - (z + 0.25 - M.Z_SEITE) / (M.Z_PULT - M.Z_SEITE) * (M.X_AUSSEN - M.X_ARKADE)
                    xg = min(max(xg, M.X_ARKADE), M.X_AUSSEN)
                    return (xg - a[0], l) if a[0] > 0 else (0.0, -xg - a[0])
                kw["grenze"] = ueber_pult
            zk = [z0] + [zz for zz in (M.Z_SEITE - 0.25, M.Z_PULT - 0.25) if "grenze" in kw and z0 < zz < z1] + [z1]     # Knicke der Grenze: je Teil gerade
            for za, zb in zip(zk, zk[1:]):
                mauer(h, "Bruchstein", a, eu, n, l, za, zb, "mauer", ls, laibung=("Sandstein", "werkstein"), dicke=0.45 if i == 3 else 0.30, ecken=QUADER(i), **kw)
            P = lambda s_, z, raus=0.0, a=a, eu=eu, n=n: V((a[0] + eu[0] * s_ + n[0] * raus, a[1] + eu[1] * s_ + n[1] * raus, z))
            n3 = V((n[0], n[1], 0))
            for loch in ls:
                D.gewaende(h, P, n3, loch, breite=0.14)
                if i != 3:
                    zu = P(l / 2, z0 + 2, 5)
                    D.scheibe(s, "Fenster", lambda q, z, P=P: P(q, z, -0.30), loch, zu)
                    D.scheibe(s, "Bleinetz", lambda q, z, P=P: P(q, z, -0.294), loch, zu)
            if i >= 2 or not (innenseite or wo == "y1"):
                D.gesimsband(h, a, b, n, z1 - 0.26, hoehe=0.26, vor=0.14, ueber=0.14)
            if i < 2 and ((wo == "x1" and sx > 0) or (wo == "x0" and sx < 0)):
                # Das Gesims der Aussenseite endet an der Ostkante des Turms offen (die Ostseite hat dort keins): Stirn schliessen
                c, ed = ((a[0] - eu[0] * 0.14, a[1] - eu[1] * 0.14), (-eu[0], -eu[1])) if wo == "x1" else ((b[0] + eu[0] * 0.14, b[1] + eu[1] * 0.14), eu)
                h.rechteck("Sandstein", V((c[0], c[1], z1 - 0.26)), V((n[0], n[1], 0)), EZ, 0.14, 0.26, "werkstein", zu=V((c[0] + ed[0] * 3, c[1] + ed[1] * 3, z1 - 0.13)))
            if wo == "y0":
                # Das Gesims der Westseite endet am inneren Rand (zur Vorhalle hin) offen: Stirn schliessen
                c, ed = ((b[0] + eu[0] * 0.14, b[1] + eu[1] * 0.14), eu) if sx > 0 else ((a[0] - eu[0] * 0.14, a[1] - eu[1] * 0.14), (-eu[0], -eu[1]))
                h.rechteck("Sandstein", V((c[0], c[1], z1 - 0.26)), V((n[0], n[1], 0)), EZ, 0.14, 0.26, "werkstein", zu=V((c[0] + ed[0] * 3, c[1] + ed[1] * 3, z1 - 0.13)))
            if i < 3:
                # Absatz: Oberseite der Wand, wo das naechste Geschoss um 0,12 zurueckspringt (sonst sieht man von oben die Rueckseite der Wand)
                sa_, sb_ = kw.get("s_bereich", (0.0, l))
                if "grenze" in kw:
                    sa_, sb_ = kw["grenze"](z1)         # Ostseite: nur der Teil ueber dem Pultdach
                if sb_ - sa_ > 1e-3:
                    h.rechteck("Sandstein", V((a[0] + eu[0] * sa_, a[1] + eu[1] * sa_, z1)), V((eu[0], eu[1], 0)), V((-n[0], -n[1], 0)), sb_ - sa_, 0.12, "werkstein",
                               zu=V((a[0] + eu[0] * (sa_ + sb_) / 2, a[1] + eu[1] * (sa_ + sb_) / 2, z1 + 5)))
        if i == 3:
            # Glockenstube: dunkler Innenraum hinter den Schalloeffnungen, darin die Glocke
            t = 0.45
            D.farbkasten_innen(h, (x0 + t, y0 + t, z0 + 0.6), (x1 - t, y1 - t, z1 - 0.3))
            h.dreh("Sandstein", (cx, cy, 0), [(0.78, z0 + 1.5), (0.70, z0 + 1.75), (0.52, z0 + 2.4), (0.40, z0 + 2.75), (0.0, z0 + 2.85)], "kleinteile", seiten=8,
                   u_bereich=D.feld("bronze"), deckel=False)
            h.stab("Fachwerk", (x0 + t, cy, z0 + 3.05), (x1 - t, cy, z0 + 3.05), 0.22, "balken")
    # Wimperge ueber der Glockenstube, Eckfialen, Kreuz
    e = 0.12 * 3
    hw = (xb - xa) / 2 - e
    zh = M.Z_HELM
    for a, b in (((cx - hw, cy - hw), (cx - hw, cy + hw)), ((cx - hw, cy + hw), (cx + hw, cy + hw)), ((cx + hw, cy + hw), (cx + hw, cy - hw)), ((cx + hw, cy - hw), (cx - hw, cy - hw))):
        eu = ((b[0] - a[0]) / (2 * hw), (b[1] - a[1]) / (2 * hw)); n = (-eu[1], eu[0])
        g = lambda z: (hw - hw * (zh + 3.0 - z) / 3.0, hw + hw * (zh + 3.0 - z) / 3.0)
        mauer(h, "Sandstein", a, eu, n, 2 * hw, zh, zh + 3.0, "sandstein", zbasis=zh, grenze=g)
        wimperg_dicke(h, a, eu, n, hw, zh)
    # Deckplatte der Glockenstube: schliesst die Ecken zwischen Wimpergen und achtseitigem Helm (sonst sieht man in den Turm)
    h.rechteck("Sandstein", (cx - hw, cy - hw, zh + 0.02), (1, 0, 0), (0, 1, 0), 2 * hw, 2 * hw, "werkstein", zu=(cx, cy, zh + 5))
    for fx in (-1, 1):
        for fy in (-1, 1):
            D.fiale_bauen(h, (cx + fx * (hw - 0.28), cy + fy * (hw - 0.28)), 0.56, zh, 2.3, 2.1)
    D.farbstab(h, "Firstblech", (cx, cy, M.Z_SPITZE - 0.3), (cx, cy, M.Z_KREUZ), 0.10, "gold")
    for seite in (-1, 1):                                  # Arme einzeln: ein durchgehender Balken laege deckungsgleich im Stamm
        D.farbstab(h, "Firstblech", (cx + seite * 0.05, cy, M.Z_KREUZ - 0.75), (cx + seite * 0.65, cy, M.Z_KREUZ - 0.75), 0.10, "gold")
    D.farbkasten(h, "Firstblech", (cx - 0.17, cy - 0.17, M.Z_SPITZE - 0.1), (cx + 0.17, cy + 0.17, M.Z_SPITZE + 0.24), "gold")


def kehle(teil, p, q, pa, pb, breite=0.30, lift=0.20):
    """Kehlblech: Bleistreifen beiderseits der Kehllinie p-q, je auf der Dachflaeche, zu der pa bzw. pb gehoert."""
    p, q = V(p), V(q)
    e = (q - p).normalized()
    p = p - e * 0.45                                       # ueber die Traufecke hinaus: dort liegen die Kanten der Lagen beider Daecher
    for x in (pa, pb):
        x = V(x)
        dd = (x - p) - e * (x - p).dot(e)
        dd.normalize()
        pts = [p + EZ * lift, q + EZ * lift, q + dd * breite + EZ * lift, p + dd * breite + EZ * lift]
        D.farbflaeche(teil, "Firstblech", pts, "blei", zu=(p + q) / 2 + EZ * 5)


def baue_dach():
    d = D.Teil("Dach", BA, saat=11)
    zt, zf = M.Z_TRAUFE, M.Z_FIRST
    xe = M.X_ARKADE + 0.30
    oben = (0, 0, 60)
    for sx in (-1, 1):
        # Hochschiff
        D.schieferflaeche(d, (sx * xe, M.Y_WESTWAND, zt), (sx * xe, M.Y_APSIS, zt), (0, M.Y_WESTWAND, zf), (0, M.Y_APSIS, zf), (sx * 40, 0, 60), saat=1 + sx, lage=LAGE, seiten=(True, True))
        # Pultdach des Seitenschiffs
        yp = (M.Y_TURM - 0.25, M.Y_QUER[0] + 0.25)        # 0,25 m in Turm und Querhauswand hinein: dort schliesst das Dach ohne Luecke an
        D.schieferflaeche(d, (sx * (M.X_AUSSEN + 0.30), yp[0], M.Z_SEITE - 0.19), (sx * (M.X_AUSSEN + 0.30), yp[1], M.Z_SEITE - 0.19),
                    (sx * M.X_ARKADE, yp[0], M.Z_PULT), (sx * M.X_ARKADE, yp[1], M.Z_PULT), (sx * 40, 0, 60), saat=5 + sx, lage=LAGE, seiten=(False, False))
    # Gratabdeckung am First des Hochschiffs (Reparatur 25.11.2026: Stoss der beiden Flaechen)
    D.grat(d, (0, M.Y_WESTWAND, zf), (0, M.Y_APSIS, zf), (-xe, M.Y_WESTWAND, zt), (xe, M.Y_WESTWAND, zt))
    # Querhaus: First quer zum Hochschiff, niedriger als dessen First
    ym = (M.Y_QUER[0] + M.Y_QUER[1]) / 2
    xq = M.X_QUER + 0.25
    for sy in (-1, 1):
        ye = ym + sy * ((M.Y_QUER[1] - M.Y_QUER[0]) / 2 + 0.30)
        D.schieferflaeche(d, (-xq, ye, zt), (xq, ye, zt), (-xq, ym, Z_QUERFIRST), (xq, ym, Z_QUERFIRST), (0, ym + sy * 40, 60), saat=8 + sy, lage=LAGE)
    for sx in (-1, 1):                                     # Kehlen: wo die Querhausdaecher auf das Hochschiff treffen (Bleistreifen auf beiden Flaechen)
        for sy in (-1, 1):
            ye = ym + sy * ((M.Y_QUER[1] - M.Y_QUER[0]) / 2 + 0.30)
            kehle(d, (sx * xe, ye, zt), (sx * (xe - (Z_QUERFIRST - zt) / M.NEIGUNG), ym, Z_QUERFIRST), (sx * (xe - 1.0), ye, zt + M.NEIGUNG), (sx * xe, ye - sy * 1.0, zt + M.NEIGUNG))
    D.grat(d, (-xq, ym, Z_QUERFIRST), (xq, ym, Z_QUERFIRST), (-xq, ym - (M.Y_QUER[1] - M.Y_QUER[0]) / 2 - 0.30, zt), (-xq, ym + (M.Y_QUER[1] - M.Y_QUER[0]) / 2 + 0.30, zt))
    # Chorschluss: fuenf Flaechen zum Firstpunkt
    spitze = (0, M.Y_APSIS, zf)
    for k in range(5):
        a, b = M.apsis_ecke(k, M.R_APSIS + 0.30), M.apsis_ecke(k + 1, M.R_APSIS + 0.30)
        m = ((a[0] + b[0]) * 2, M.Y_APSIS + (a[1] + b[1] - 2 * M.Y_APSIS) * 2, 60)
        D.schieferflaeche(d, (*a, zt), (*b, zt), spitze, spitze, m, saat=20 + k, lage=LAGE, seiten=(False, False))
    for k in range(1, 5):                                  # Grate zwischen den fuenf Flaechen des Chorschlusses
        e0, e1, e2 = (M.apsis_ecke(i, M.R_APSIS + 0.30) for i in (k - 1, k, k + 1))
        D.grat(d, (*e1, zt), spitze, (*e0, zt), (*e2, zt))
    # Turmhelme: achtseitig
    for sx in (-1, 1):
        xa, xb = (M.X_TURM if sx > 0 else (-M.X_TURM[1], -M.X_TURM[0]))
        cx, cy = (xa + xb) / 2, (M.Y_WEST + M.Y_TURM) / 2
        hw = (xb - xa) / 2 - 0.36 - 0.12                  # der Helm steht hinter den Wimpergen (keine Durchdringung mit ihren Flaechen)
        t = hw * math.tan(math.radians(22.5))
        ecken = [(hw, -t), (hw, t), (t, hw), (-t, hw), (-hw, t), (-hw, -t), (-t, -hw), (t, -hw)]
        sp = (cx, cy, M.Z_SPITZE)
        for k in range(8):
            a, b = ecken[k], ecken[(k + 1) % 8]
            D.schieferflaeche(d, (cx + a[0], cy + a[1], M.Z_HELM), (cx + b[0], cy + b[1], M.Z_HELM), sp, sp, (cx + (a[0] + b[0]) * 4, cy + (a[1] + b[1]) * 4, 60),
                        stufe=0.0, karte=False, saat=30 + k + 4 * sx)   # glatte Flaechen: Keile und Karten ueberschnitten sich an den Graten; Reihen aus der Textur
        for k in range(8):                                 # Gratabdeckung der acht Grate des Helms
            e0, e1, e2 = ecken[k - 1], ecken[k], ecken[(k + 1) % 8]
            D.grat(d, (cx + e1[0], cy + e1[1], M.Z_HELM), sp, (cx + e0[0], cy + e0[1], M.Z_HELM), (cx + e2[0], cy + e2[1], M.Z_HELM))
    return d


def main():
    opt = B.optionen()
    D.neu()
    stoffe = D.stoffe_aussen()
    scheiben = D.Teil("Scheiben", BA, saat=5)
    haus = baue_haus(scheiben)
    dach = baue_dach()
    summe = haus.dreiecke() + dach.dreiecke() + scheiben.dreiecke()
    print(f"DREIECKE aussen: Haus {haus.dreiecke()}, Dach {dach.dreiecke()}, Scheiben {scheiben.dreiecke()}, Summe {summe} (Budget {M.BUDGET[0]}-{M.BUDGET[1]})")
    o_haus, o_dach = haus.objekt(stoffe), dach.objekt(stoffe)
    o_scheiben = scheiben.objekt(stoffe, mit_farbe=False)
    bpy.ops.mesh.primitive_plane_add(size=120, location=(0, 0, 0))
    boden = bpy.context.active_object; boden.name = "Kontrollboden"
    boden.data.materials.append(B.stoff("Kontrollboden", farbe=(0.36, 0.33, 0.28)))
    B.uv2_und_ao([([o_haus, o_dach], [boden])], "dom", MODELL, mit_ao="ohne-ao" not in opt)
    anker = [
        B.anker("Anker_Tuer", (0.0, M.Y_WESTWAND - 2.2, 0.02), blick="nord"),
        B.anker("Anker_Laterne", (M.PORTAL[0] / 2 + 0.9, M.Y_WESTWAND - 0.05, 2.9), blick="sued"),
    ]
    B.exportiere([o_haus, o_dach, o_scheiben] + anker, os.path.join(B.ZIEL_MODELL, MODELL + ".glb"))
    if "bilder" in opt:
        o = opt["bilder"]; os.makedirs(o, exist_ok=True)
        B.kontrollbild(os.path.join(o, "dom_aussen_artwork_blick.jpg"), (-42.0, -52.0, 46.0), (0.0, -1.0, 12.0), brennweite=40)
        B.kontrollbild(os.path.join(o, "dom_aussen_portal_nah.jpg"), (-5.0, -27.0, 2.4), (0.0, -17.0, 5.5), brennweite=28)
        B.kontrollbild(os.path.join(o, "dom_aussen_helme.jpg"), (-16.0, -40.0, 44.0), (0.0, -14.7, 32.0), brennweite=50)
        if "alle" in opt:                                   # 8 Ansichten rundum und Nahbilder (Reparatur 25.11.2026)
            for i in range(8):
                w = math.radians(i * 45 - 90)
                B.kontrollbild(os.path.join(o, f"dom_rund{i}.jpg"), (math.cos(w) * 66, 1.0 + math.sin(w) * 66, 36.0), (0.0, 0.0, 14.0), brennweite=40, proben=24)
            for name, auge, ziel, f in (("grate_hochschiff", (-12, -6, 24), (0, 4, 17), 45), ("helm", (-14, -26, 36), (5.1, -14.7, 31), 50),
                                        ("helm_wimperge", (12, -24, 31), (5.1, -14.7, 27), 50), ("strebebogen", (-17, -4, 11), (-7, 0, 8), 40),
                                        ("chor", (10, 33, 20), (0, 13.5, 14), 40), ("kehle", (14, 2, 20), (4.5, 7.5, 14.5), 45),
                                        ("turm_pultdach", (14, -20, 11), (7.7, -11.9, 7), 40), ("querhaus", (22, 2, 22), (6, 6, 12), 40)):
                B.kontrollbild(os.path.join(o, f"dom_nah_{name}.jpg"), auge, ziel, brennweite=f, proben=24)


if __name__ == "__main__":
    main()
