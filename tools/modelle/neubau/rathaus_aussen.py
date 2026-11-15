"""Neubau Rathaus, Huelle aussen: Saalbau (Erdgeschoss Bruchstein mit Arkade, Obergeschoss Fachwerk), Turm mit Uhr, Glockenstube und
Helm, Anbau der Amtsstube mit Pultdach.

    blender -b --python tools/modelle/neubau/rathaus_aussen.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/rathaus.glb mit den Knoten
    Haus      Mauern, Fachwerk, Boegen, Fensterstoecke, Tuer, Uhr (Materialien Bruchstein, Sandstein, Putz, Fachwerk, Bretter, Eisen)
    Dach      Schiefer in drei Stufen, Gauben, Schornstein, Helm, Pultdach; Alpha-Cards an den Stufenkanten (Schieferkante)
    Scheiben  Fenster (Fensterlicht) und Bleinetz; die Innenansicht blendet sie aus
    Anker_Tuer, Anker_Laterne, Anker_Schornstein
und die gebackene Verdeckung in game/assets/texturen/neubau/rathaus_ao_atlas.png (Gruppe "rathaus", ao_atlas.json).
Alle Masse aus rathaus_masse.py; Style-Guide: Monument 8.000-15.000 Dreiecke.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rathaus_bib as B                                  # noqa: E402
import rathaus_masse as M                                # noqa: E402
from rathaus_bib import EZ, ROT, Loch, Teil, mauer, wandpunkt   # noqa: E402

BA = B.BAENDER_RATHAUS
MITTE = Vector((-1.5, 0.0, 4.0))
DACH_D = 0.20            # Dicke der Deckung (senkrecht gemessen)


def loecher(wand):
    (ox, oy), (ex, ey), _, _ = M.WAENDE[wand]
    start = ox if ex else oy
    return [Loch(a0 - start, a1 - start, z0, z1, bogen=art in ("bogenfenster", "portal", "schall"))
            for w, (a0, a1), (z0, z1), art, _ in M.OEFFNUNGEN if w == wand]


def wandmauer(teil, wand, z0, z1, stoff, band, laibung=None, **kw):
    (ox, oy), (ex, ey), laenge, (nx, ny) = M.WAENDE[wand]
    mauer(teil, stoff, (ox, oy), (ex, ey), (nx, ny), laenge, z0, z1, band, loecher(wand), laibung=laibung, dicke=M.WAND_DICKE[wand], **kw)


def s_von(wand, welt):
    (ox, oy), (ex, ey), _, _ = M.WAENDE[wand]
    return welt - (ox if ex else oy)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Dachflaechen
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def dachflaeche(teil, a0, a1, b0, b1, aussen, stufen=3, kurs=0.75, hub=0.05, karten=True, saat=1, band="schiefer"):
    """Schiefer von der Unterkante a0-a1 zur Oberkante b0-b1 (b0 == b1 ergibt ein Dreieck, Helm). Drei Stufen, jede um `hub` hoeher
    als die darunter, mit Stirn und Alpha-Card (Schieferkante) an ihrer Unterkante."""
    a0, a1, b0, b1 = Vector(a0), Vector(a1), Vector(b0), Vector(b1)
    e = (a1 - a0).normalized()
    hoch = ((b0 + b1) / 2 - (a0 + a1) / 2)
    n = e.cross(hoch).normalized()
    if n.dot(Vector(aussen) - a0) < 0: n = -n
    lang = hoch.length
    v0, v1, m = teil.baender[band]
    L = lambda t: a0 + (b0 - a0) * t
    R = lambda t: a1 + (b1 - a1) * t
    u = lambda p, off: ((p - a0).dot(e) + off) / m
    for i in range(stufen):
        ta, tb = i / stufen, (i + 1) / stufen
        hebe = n * (hub * i)
        # Stirn der Stufe (Kante zur Stufe darunter)
        if i > 0:
            p, q = L(ta), R(ta)
            teil.vieleck("Schiefer", [p + n * hub * (i - 1), q + n * hub * (i - 1), q + hebe, p + hebe],
                         [(u(p, 0), v1), (u(q, 0), v1), (u(q, 0), v1 - 0.06 * (v1 - v0)), (u(p, 0), v1 - 0.06 * (v1 - v0))], zu=(p + q) / 2 + n - hoch.normalized())
        t = ta
        k = 0
        while t < tb - 1e-6:
            t2 = min(t + kurs / lang, tb)
            off = ((k * 7919 + saat * 31 + i * 101) % 1000) / 1000.0 * m
            f0 = (t - ta) * lang / kurs; f1 = (t2 - ta) * lang / kurs
            f0 -= math.floor(f0 + 1e-9) if f1 - math.floor(f0 + 1e-9) <= 1.0 + 1e-6 else 0
            f1 = f0 + (t2 - t) * lang / kurs
            vv = lambda f: v1 - min(max(f, 0.0), 1.0) * (v1 - v0)
            pts = [L(t) + hebe, R(t) + hebe, R(t2) + hebe, L(t2) + hebe]
            uvs = [(u(pts[0] - hebe, off), vv(f0)), (u(pts[1] - hebe, off), vv(f0)), (u(pts[2] - hebe, off), vv(f1)), (u(pts[3] - hebe, off), vv(f1))]
            if (pts[2] - pts[3]).length < 1e-5: pts, uvs = pts[:3], uvs[:3]
            teil.vieleck("Schiefer", pts, uvs, zu=(pts[0] + pts[2]) / 2 + n)
            t = t2; k += 1
        if karten:
            p, q = L(ta), R(ta)
            runter = -hoch.normalized()
            c0, c1, _ = teil.baender["schiefer_kante"]
            hk = 0.20
            pts = [p + runter * 0.14 + hebe + n * 0.012, q + runter * 0.14 + hebe + n * 0.012, q - runter * (hk - 0.14) + hebe + n * 0.012, p - runter * (hk - 0.14) + hebe + n * 0.012]
            uvs = [(u(p, 0.3), c1), (u(q, 0.3), c1), (u(q, 0.3), c0), (u(p, 0.3), c0)]
            teil.vieleck("Schieferkante", pts, uvs, zu=(pts[0] + pts[2]) / 2 + n)


def dach_z(y, oben=False):
    """Unterseite der Deckung des Hauptdachs ueber y (mit `oben` die Oberseite)."""
    return M.Z_FIRST - abs(y) * M.NEIGUNG + (DACH_D if oben else 0.0)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Fachwerk
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def fachwerk(teil, wand, z0, z1, s_bereich=None, grenze=None, mann=True):
    """Fachwerk vor der Putzwand: Schwelle, Rähm, Ständer an den Enden, an den Fenstern und etwa alle 1,1 m, Brust- und Sturzriegel,
    in Feldern ohne Fenster eine Strebe (Mann-Figur paarweise)."""
    (ox, oy), (ex, ey), laenge, (nx, ny) = M.WAENDE[wand]
    n = Vector((nx, ny, 0))
    sa, sb = s_bereich or (0.0, laenge)
    P = lambda s, z: Vector((ox + ex * s, oy + ey * s, z))
    fenster = sorted((l for l in loecher(wand) if l.z0 >= z0 - 1e-6 and l.z1 <= z1 + 1e-6 and l.s0 >= sa - 1e-6 and l.s1 <= sb + 1e-6), key=lambda l: l.s0)
    br = 0.18
    teil.brett("Fachwerk", P(sa, z0 + br / 2), P(sb, z0 + br / 2), br, n, "balken_c", tiefe=0.05)          # Schwelle
    teil.brett("Fachwerk", P(sa, z1 - br / 2), P(sb, z1 - br / 2), br, n, "balken_a", tiefe=0.05)          # Rähm
    staender = {sa + 0.09, sb - 0.09}
    for l in fenster:
        staender |= {l.s0 - 0.08, l.s1 + 0.08}
    staender = sorted(staender)
    voll = []
    for a, b in zip(staender[:-1], staender[1:]):
        if b - a > 1.35:
            k = math.ceil((b - a) / 1.15)
            voll += [a + (b - a) * j / k for j in range(1, k)]
    staender = sorted(set(staender) | set(voll))
    for s in staender:
        teil.brett("Fachwerk", P(s, z0 + br), P(s, z1 - br), 0.16, n, "balken_b", tiefe=0.045)
    zb = (z0 + z1) / 2 - 0.35
    for a, b in zip(staender[:-1], staender[1:]):
        f = [l for l in fenster if l.s0 > a - 0.1 and l.s1 < b + 0.1]
        if f:
            l = f[0]
            teil.brett("Fachwerk", P(a + 0.08, l.z0 - 0.07), P(b - 0.08, l.z0 - 0.07), 0.14, n, "balken_a", tiefe=0.04)
            teil.brett("Fachwerk", P(a + 0.08, l.z1 + 0.07), P(b - 0.08, l.z1 + 0.07), 0.14, n, "balken_a", tiefe=0.04)
        else:
            teil.brett("Fachwerk", P(a + 0.08, zb), P(b - 0.08, zb), 0.14, n, "balken_a", tiefe=0.04)
            if mann and b - a > 0.6:
                links = (round((a - sa) / 1.1)) % 2 == 0
                if links: teil.brett("Fachwerk", P(a + 0.08, z0 + br), P(b - 0.08, zb), 0.13, n, "balken_b", tiefe=0.035)
                else: teil.brett("Fachwerk", P(b - 0.08, z0 + br), P(a + 0.08, zb), 0.13, n, "balken_b", tiefe=0.035)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Huelle
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def baue_haus():
    h = Teil("Haus", BA, saat=21)
    stein = ("Sandstein", "quader")
    # ── Erdgeschoss: Bruchstein mit Quaderecken, Laibungen aus Sandstein ──
    wandmauer(h, "vorn_eg", -0.12, M.Z_OG, "Bruchstein", "bruchstein", laibung=stein, ecken={"links": ("Sandstein", "quader", 0)}, saat=3)
    wandmauer(h, "links_eg", -0.12, M.Z_OG, "Bruchstein", "bruchstein", laibung=stein, ecken={"links": ("Sandstein", "quader", 1), "rechts": ("Sandstein", "quader", 1)}, saat=4)
    wandmauer(h, "hinten_eg", -0.12, M.Z_OG, "Bruchstein", "bruchstein", laibung=stein, ecken={"links": ("Sandstein", "quader", 0)}, saat=5)
    for wand in ("vorn_eg", "links_eg"):
        (ox, oy), (ex, ey), _, (nx, ny) = M.WAENDE[wand]
        for l in loecher(wand):
            if l.bogen: B.bogenring(h, "Sandstein", "quader", (ox, oy), (ex, ey), (nx, ny), l, breite=0.24 if l.s1 - l.s0 > 1.1 else 0.18)
            if l.bogen and l.z0 > M.Z_SOCKEL + 0.1:      # Sohlbank, steht vor
                n3 = Vector((nx, ny, 0)); e3 = Vector((ex, ey, 0)); o3 = Vector((ox, oy, 0))
                a = o3 + e3 * (l.s0 - 0.30) + Vector((0, 0, l.z0 - 0.10)); b = o3 + e3 * (l.s1 + 0.30) + n3 * 0.10 + Vector((0, 0, l.z0))
                h.kasten("Sandstein", (min(a.x, b.x), min(a.y, b.y), a.z), (max(a.x, b.x), max(a.y, b.y), b.z), "quader")
    # Sockelgesims am Erdgeschoss
    B.gesims(h, "Sandstein", "quader", [(M.EG_X[0], M.EG_Y[0]), (M.EG_X[1], M.EG_Y[0]), (M.EG_X[1], M.EG_Y[1]), (M.EG_X[0], M.EG_Y[1])], M.Z_SOCKEL - 0.06, hoehe=0.10, vor=0.05)
    # Stufen vor dem Portal
    a0, a1 = M.ARKADE[1]
    for i, z in enumerate((0.15, 0.30)):
        tief = 0.70 - i * 0.35
        h.kasten("Sandstein", (a0 - 0.35 + i * 0.1, M.EG_Y[0] - tief, -0.05), (a1 + 0.35 - i * 0.1, M.EG_Y[0], z), "quader", ohne=("-z", "+y"))
    # ── Obergeschoss: Putz und Fachwerk ──
    putz = ("Fachwerk", "balken_c")
    for wand in ("vorn_og", "hinten_og", "links_og"):
        wandmauer(h, wand, M.Z_OG, M.Z_TRAUFE, "Putz", "putz", laibung=putz, zbasis=M.Z_OG, saat=7)
        fachwerk(h, wand, M.Z_OG, M.Z_TRAUFE)
    s_r = (s_von("rechts_og", M.TURM_Y[1]), M.WAENDE["rechts_og"][2])
    wandmauer(h, "rechts_og", M.Z_OG, M.Z_TRAUFE, "Putz", "putz", laibung=putz, zbasis=M.Z_OG, saat=8, s_bereich=s_r)
    fachwerk(h, "rechts_og", M.Z_OG, M.Z_TRAUFE, s_bereich=s_r)
    # Auskragung: Bretter unter dem Obergeschoss und Balkenkoepfe
    for (a, b) in (((M.OG_X[0], M.OG_Y[0]), (M.OG_X[1], M.EG_Y[0])), ((M.OG_X[0], M.EG_Y[1]), (M.OG_X[1], M.OG_Y[1])), ((M.OG_X[0], M.EG_Y[0]), (M.EG_X[0], M.EG_Y[1]))):
        lx, ly = b[0] - a[0], b[1] - a[1]
        h.rechteck("Fachwerk", (a[0], a[1], M.Z_OG), (1, 0, 0), (0, 1, 0), lx, ly, "balken_c", laengs="u" if lx > ly else "v", zu=(a[0] + lx / 2, a[1] + ly / 2, 0))
    for i in range(12):
        x = M.OG_X[0] + 0.3 + i * (M.OG_X[1] - M.OG_X[0] - 0.6) / 11
        for y0, y1 in ((M.OG_Y[0] - 0.02, M.EG_Y[0]), (M.EG_Y[1], M.OG_Y[1] + 0.02)):
            h.kasten("Fachwerk", (x - 0.09, y0, M.Z_OG - 0.20), (x + 0.09, y1, M.Z_OG), "balken_c", ohne=("+z",), laengs="y")
    for i in range(8):
        y = M.EG_Y[0] + 0.4 + i * (M.EG_Y[1] - M.EG_Y[0] - 0.8) / 7
        h.kasten("Fachwerk", (M.OG_X[0] - 0.02, y - 0.09, M.Z_OG - 0.20), (M.EG_X[0], y + 0.09, M.Z_OG), "balken_c", ohne=("+z",), laengs="x")
    # Giebel links (Fachwerk bis in den First), rechts nur hinter dem Turm
    halb = (M.OG_Y[1] - M.OG_Y[0]) / 2
    giebel = lambda z: (halb - (M.Z_FIRST - z) / M.NEIGUNG, halb + (M.Z_FIRST - z) / M.NEIGUNG)
    (ox, oy), (ex, ey), laenge, (nx, ny) = M.WAENDE["links_og"]
    mauer(h, "Putz", (ox, oy), (ex, ey), (nx, ny), laenge, M.Z_TRAUFE, M.Z_FIRST, "putz", zbasis=M.Z_OG, grenze=giebel, saat=9)
    giebel_fachwerk(h, "links_og")
    (ox, oy), (ex, ey), laenge, (nx, ny) = M.WAENDE["rechts_og"]
    mauer(h, "Putz", (ox, oy), (ex, ey), (nx, ny), laenge, M.Z_TRAUFE, M.Z_FIRST, "putz", zbasis=M.Z_OG, grenze=giebel, saat=10, s_bereich=s_r)
    giebel_fachwerk(h, "rechts_og", ab=s_r[0])
    props(h)
    # ── Turm ──
    turm(h)
    # ── Anbau ──
    anbau(h)
    # ── Fensterstoecke, Laeden, Portal, Wappen ──
    for w, (a0, a1), (z0, z1), art, _ in M.OEFFNUNGEN:
        if art in ("fenster", "bogenfenster"):
            B.fensterstock(h, "Fachwerk", "balken_c", w, a0, a1, z0, z1, bogen=art == "bogenfenster")
        if art == "fenster" and w.endswith("_og"):
            laeden(h, w, a0, a1, z0, z1)
        if art == "schall":
            schalllaeden(h, w, a0, a1, z0, z1)
    portal(h)
    wappen(h)
    return h


def giebel_fachwerk(h, wand, ab=0.0):
    """Fachwerk im Giebeldreieck: Schwelle, Kehlbalken, Staender, Streben, Ortgangbretter. `ab`: nur ab dieser Stelle (rechter
    Giebel hinter dem Turm)."""
    (ox, oy), (ex, ey), laenge, (nx, ny) = M.WAENDE[wand]
    n = Vector((nx, ny, 0))
    P = lambda s, z: Vector((ox + ex * s, oy + ey * s, z))
    halb = laenge / 2
    zk = M.Z_TRAUFE + 2.6                                 # Kehlbalken
    bk = (zk - M.Z_TRAUFE) / M.NEIGUNG
    h.brett("Fachwerk", P(max(0.1, ab), M.Z_TRAUFE + 0.09), P(laenge - 0.1, M.Z_TRAUFE + 0.09), 0.18, n, "balken_c", tiefe=0.05)
    h.brett("Fachwerk", P(max(bk + 0.15, ab), zk), P(laenge - bk - 0.15, zk), 0.16, n, "balken_a", tiefe=0.045)
    for s in (halb - 2.6, halb - 1.3, halb, halb + 1.3, halb + 2.6):
        if s < ab + 0.1: continue
        top = M.Z_FIRST - abs(s - halb) * M.NEIGUNG - 0.15
        z_ende = min(top, zk) if abs(s - halb) > 0.1 else top
        h.brett("Fachwerk", P(s, M.Z_TRAUFE + 0.18), P(s, z_ende), 0.15, n, "balken_b", tiefe=0.045)
    for a, b in ((halb - 2.6, halb - 1.3), (halb + 2.6, halb + 1.3)):          # Streben (Andreaskreuz-Haelften)
        if min(a, b) < ab: continue
        h.brett("Fachwerk", P(a, M.Z_TRAUFE + 0.18), P(b, zk - 0.08), 0.13, n, "balken_b", tiefe=0.035)
    for a, b in ((halb - 1.3, halb), (halb + 1.3, halb)):
        if min(a, b) < ab: continue
        h.brett("Fachwerk", P(a, zk + 0.08), P(b, M.Z_FIRST - 0.6), 0.13, n, "balken_b", tiefe=0.035)
    # Ortgangbretter folgen der Dachkante
    for seite in (-1, 1):
        if seite < 0 and ab > 0: continue
        a = P(halb + seite * halb, M.Z_TRAUFE); b = P(halb, M.Z_FIRST)
        h.brett("Fachwerk", a + Vector((0, 0, -0.1)), b + Vector((0, 0, -0.1)), 0.16, n, "balken_c", tiefe=0.05)


def props(h):
    """Kleinteile vor dem Haus wie im Artwork: Anschlagtafel links vom Portal, zwei Baenke am Turm."""
    x = M.ARKADE[0][0] - 0.75
    y = M.EG_Y[0] - 0.35
    for dx in (-0.55, 0.55):
        h.kasten("Fachwerk", (x + dx - 0.05, y - 0.05, 0.0), (x + dx + 0.05, y + 0.05, 1.95), "balken_b", ohne=("-z",))
    h.kasten("Bretter", (x - 0.62, y - 0.03, 0.85), (x + 0.62, y + 0.03, 1.80), "planken", laengs="x")
    h.kasten("Bretter", (x - 0.72, y - 0.18, 1.80), (x + 0.72, y + 0.12, 1.86), "planken", laengs="x")
    for i, (bx, bz, bw, bh) in enumerate(((-0.35, 1.05, 0.30, 0.38), (0.05, 1.20, 0.26, 0.34), (0.38, 1.0, 0.28, 0.36))):
        h.rechteck("Putz", (x + bx - bw / 2, y - 0.035, bz), (1, 0, 0), (0, 0, 1), bw, bh, "putz", zu=(x, y - 5, 1.3))
    for bx in (M.TURM_X[0] + 0.6, M.TURM_X[0] + 2.4):
        by = M.TURM_Y[0] - 0.20
        h.kasten("Bretter", (bx, by - 0.16, 0.42), (bx + 1.5, by + 0.16, 0.47), "planken", laengs="x")
        for lx in (bx + 0.1, bx + 1.32):
            h.kasten("Fachwerk", (lx, by - 0.13, 0.0), (lx + 0.08, by + 0.13, 0.42), "balken_c", ohne=("-z", "+z"))


def turm(h):
    stein = ("Sandstein", "quader")
    ecke = lambda a, b: {"links": ("Sandstein", "quader", a), "rechts": ("Sandstein", "quader", b)}
    wandmauer(h, "turm_vorn", -0.12, M.Z_TURM_MAUER, "Bruchstein", "bruchstein", laibung=stein, ecken=ecke(1, 0), saat=11)
    wandmauer(h, "turm_rechts", -0.12, M.Z_TURM_MAUER, "Bruchstein", "bruchstein", laibung=stein, ecken=ecke(1, 0), saat=12)
    # hinten: ueber dem Pultdach des Anbaus (und das Stueck rechts daneben, das 0,20 uebersteht)
    hinten_grenze = lambda z: (0.0 if z >= M.Z_ANBAU_FIRST else max(0.0, M.ANBAU_X[1] - M.TURM_X[0] - (z - M.Z_ANBAU_TRAUFE) / M.PULT_NEIGUNG) if z > M.Z_ANBAU_TRAUFE else M.ANBAU_X[1] - M.TURM_X[0], M.TURM_X[1] - M.TURM_X[0])
    wandmauer(h, "turm_hinten", -0.12, M.Z_TURM_MAUER, "Bruchstein", "bruchstein", laibung=stein, ecken={"rechts": ("Sandstein", "quader", 1)}, saat=13, grenze=hinten_grenze)
    # links: das Stueck vor dem Saalbau ganz, darueber nur, was ueber das Dach ragt
    dach_y = lambda z: (z - M.Z_FIRST - DACH_D) / M.NEIGUNG            # y < 0: dort liegt das Dach unter z
    def links_grenze(z):
        vor = M.EG_Y[0] - M.TURM_Y[0] if z < M.Z_OG else M.OG_Y[0] - M.TURM_Y[0]
        if z < M.Z_TRAUFE: return (0.0, vor)
        return (0.0, max(vor, min(M.TURM_Y[1], dach_y(z)) - M.TURM_Y[0]))
    wandmauer(h, "turm_links", -0.12, M.Z_TURM_MAUER, "Bruchstein", "bruchstein", laibung=stein, ecken={"links": ("Sandstein", "quader", 0)}, saat=14, grenze=links_grenze)
    for wand in ("turm_vorn", "turm_rechts", "turm_hinten", "turm_links"):
        (ox, oy), (ex, ey), _, (nx, ny) = M.WAENDE[wand]
        for l in loecher(wand):
            if l.bogen: B.bogenring(h, "Sandstein", "quader", (ox, oy), (ex, ey), (nx, ny), l, breite=0.18)
    ring = [(M.TURM_X[0], M.TURM_Y[0]), (M.TURM_X[1], M.TURM_Y[0]), (M.TURM_X[1], M.TURM_Y[1]), (M.TURM_X[0], M.TURM_Y[1])]
    B.gesims(h, "Sandstein", "quader", ring, M.Z_GLOCKE + 0.55, hoehe=0.16, vor=0.08)
    B.gesims(h, "Sandstein", "quader", ring, M.Z_TURM_MAUER - 0.22, hoehe=0.22, vor=0.10)
    for wand, s in M.ZIFFERBLAETTER:
        uhr(h, wand, s)


def uhr(h, wand, s_welt):
    """Zifferblatt: Scheibe aus Putz, Ring aus Sandstein, zwoelf Marken und zwei Zeiger aus Eisen."""
    (_, _, _, nn) = M.WAENDE[wand]
    n = Vector((nn[0], nn[1], 0))
    c = wandpunkt(wand, s_welt, M.Z_UHR_MITTE, 0.04)
    (_, _), (ex, ey), _, _ = M.WAENDE[wand]
    e = Vector((ex, ey, 0))
    teile = 28
    v0, v1, mm = BA["putz"]
    q0, q1, _ = BA["quader"]
    for i in range(teile):
        w0, w1 = 2 * math.pi * i / teile, 2 * math.pi * (i + 1) / teile
        p0 = c + (e * math.cos(w0) + EZ * math.sin(w0)) * M.UHR_R; p1 = c + (e * math.cos(w1) + EZ * math.sin(w1)) * M.UHR_R
        h.vieleck("Putz", [c, p0, p1], [(0.5, (v0 + v1) / 2), (0.5 + 0.2 * math.cos(w0), (v0 + v1) / 2), (0.5 + 0.2 * math.cos(w1), (v0 + v1) / 2)], zu=c + n)
        r2 = M.UHR_R + 0.13
        a0 = c + (e * math.cos(w0) + EZ * math.sin(w0)) * r2 + n * 0.02; a1 = c + (e * math.cos(w1) + EZ * math.sin(w1)) * r2 + n * 0.02
        h.vieleck("Sandstein", [p0, a0, a1, p1], [(i * 0.1, q1), (i * 0.1, q0), (i * 0.1 + 0.1, q0), (i * 0.1 + 0.1, q1)], zu=(p0 + a1) / 2 + n)
        h.vieleck("Sandstein", [a0, a1, a1 - n * 0.06, a0 - n * 0.06], [(i * 0.1, q1), (i * 0.1 + 0.1, q1), (i * 0.1 + 0.1, q0), (i * 0.1, q0)], zu=(a0 + a1) / 2 * 2 - c)
    for k in range(12):
        w = 2 * math.pi * k / 12
        r = e * math.cos(w) + EZ * math.sin(w); q = e * -math.sin(w) + EZ * math.cos(w)
        p = c + r * (M.UHR_R - 0.16) + n * 0.01
        h.vieleck("Eisen", [p - q * 0.025, p + q * 0.025, p + q * 0.025 + r * 0.12, p - q * 0.025 + r * 0.12], [(0, 0.99)] * 4, zu=p + n)
    for laenge, w, breite in ((0.62, math.radians(60), 0.05), (0.45, math.radians(-30), 0.07)):     # zehn nach zwei
        r = e * math.cos(w) + EZ * math.sin(w); q = e * -math.sin(w) + EZ * math.cos(w)
        p = c + n * 0.025
        h.vieleck("Eisen", [p - q * breite / 2, p + r * laenge, p + q * breite / 2], [(0, 0.99)] * 3, zu=p + n)


def anbau(h):
    """Anbau der Amtsstube: Steinsockel, darueber Fachwerk bis zur Traufe; hinten folgt die Wand dem Pultdach."""
    sockel = M.Z_SOCKEL + 0.60
    for wand, ecken in (("anbau_rechts", {"links": ("Sandstein", "quader", 0)}), ("anbau_hinten", {"rechts": ("Sandstein", "quader", 1)})):
        wandmauer(h, wand, -0.12, sockel, "Bruchstein", "bruchstein", laibung=("Sandstein", "quader"), ecken=ecken, saat=15)
    wandmauer(h, "anbau_rechts", sockel, M.Z_ANBAU_TRAUFE, "Putz", "putz", laibung=("Fachwerk", "balken_c"), zbasis=sockel, saat=16)
    fachwerk(h, "anbau_rechts", sockel, M.Z_ANBAU_TRAUFE)
    oben = lambda z: (0.0, M.ANBAU_X[1] - M.ANBAU_X[0] - max(0.0, (z - M.Z_ANBAU_TRAUFE) / M.PULT_NEIGUNG))
    wandmauer(h, "anbau_hinten", sockel, M.Z_ANBAU_FIRST, "Putz", "putz", laibung=("Fachwerk", "balken_c"), zbasis=sockel, saat=17, grenze=oben)
    fachwerk(h, "anbau_hinten", sockel, M.Z_ANBAU_TRAUFE)


def laeden(h, wand, a0, a1, z0, z1):
    """Zwei offene Fensterlaeden aus gestrichenem Holz (Akzentfarbe: Eckenfarbe Rot, im Spiel Stadtrot) neben dem Fenster."""
    (_, _, _, nn) = M.WAENDE[wand]
    n = Vector((nn[0], nn[1], 0))
    b = (a1 - a0) / 2
    for s0, s1 in ((a0 - b - 0.04, a0 - 0.04), (a1 + 0.04, a1 + b + 0.04)):
        p = wandpunkt(wand, s0, z0, 0.06)
        e = wandpunkt(wand, s1, z0, 0.06) - p
        h.rechteck("Bretter", p, e, EZ, e.length, z1 - z0, "farbholz", laengs="v", zu=p + e / 2 + EZ + n, farbe=ROT)


def schalllaeden(h, wand, a0, a1, z0, z1):
    """Schalllaeden in den Arkaden der Glockenstube: schraege Bretter, dahinter ist nichts zu sehen."""
    (_, _, _, nn) = M.WAENDE[wand]
    n = Vector((nn[0], nn[1], 0))
    r = (a1 - a0) / 2
    zk = z1 - r
    z = z0 + 0.10
    while z < zk + r * 0.6:
        w = r if z <= zk else math.sqrt(max(r * r - (z - zk) ** 2, 0))
        sm = (a0 + a1) / 2
        p = wandpunkt(wand, sm - w, z, -0.30); q = wandpunkt(wand, sm + w, z, -0.30)
        h.vieleck("Bretter", [p, q, q + EZ * 0.16 - n * 0.10, p + EZ * 0.16 - n * 0.10], [(0, BA["planken"][1]), (0.3, BA["planken"][1]), (0.3, BA["planken"][0]), (0, BA["planken"][0])], zu=(p + q) / 2 + n * 2 + EZ)
        z += 0.20


def portal(h):
    """Tuer im mittleren Arkadenbogen: Brettertuer mit Rundbogen, in der Mitte der Mauer."""
    a0, a1 = M.ARKADE[1]
    mitte = wandpunkt("vorn_eg", (a0 + a1) / 2, M.Z_SOCKEL, -M.T_STEIN / 2)
    hoehe = M.Z_KAEMPFER + (a1 - a0) / 2 - M.Z_SOCKEL
    B.bretttuer(h, "Bretter", "planken", "Eisen", "eisen", mitte, (1, 0, 0), (0, -1, 0), a1 - a0, hoehe, bogen_r=(a1 - a0) / 2, bretter=6)


def wappen(h):
    """Stadtwappen am Obergeschoss zwischen dem zweiten und dritten Fenster: Schild in Stadtrot mit Sandsteinrahmen."""
    vorn = sorted(a for w, a, z, art, raum in M.OEFFNUNGEN if w == "vorn_og")
    x = (vorn[1][1] + vorn[2][0]) / 2
    z = (M.OG_FENSTER[0] + M.OG_FENSTER[1]) / 2 + 0.1
    umriss = [(-0.40, 0.48), (0.40, 0.48), (0.40, -0.02), (0.0, -0.52), (-0.40, -0.02)]
    vorn = lambda dx, dz, d: Vector((x + dx, M.OG_Y[0] - d, z + dz))
    for d, stoff, band, farbe, s in ((0.06, "Sandstein", "quader", (0, 0, 0, 1), 1.15), (0.09, "Bretter", "farbholz", ROT, 1.0)):
        pts = [vorn(px * s, pz * s, d) for px, pz in umriss]
        c = vorn(0, 0, d)
        for i in range(len(pts)):
            h.vieleck(stoff, [c, pts[i], pts[(i + 1) % len(pts)]], [(0.5, BA[band][1]), (0.6, BA[band][0]), (0.7, BA[band][0])], zu=c - Vector((0, 1, 0)), farbe=farbe)
        for i in range(len(pts)):
            a, b = pts[i], pts[(i + 1) % len(pts)]
            h.vieleck(stoff, [a, b, b + Vector((0, d - 0.0, 0)) * 0.5, a + Vector((0, d, 0)) * 0.5], [(0, BA[band][1])] * 4, zu=(a + b) / 2 * 2 - c, farbe=farbe)


def baue_dach():
    d = Teil("Dach", BA, saat=31)
    x0, x1 = M.OG_X[0] - M.UEBER_ORTGANG, M.OG_X[1] + M.UEBER_ORTGANG
    ye = M.OG_Y[1] + M.UEBER_TRAUFE
    ze = dach_z(ye)
    x1_hinten = x1
    for seite in (-1, 1):
        # vorn endet das Dach am Turm (sonst ragte die Ecke des Ueberstands vor den Turm)
        x1 = M.OG_X[1] if seite < 0 else x1_hinten
        a0 = (x0, seite * ye, ze + DACH_D); a1 = (x1, seite * ye, ze + DACH_D)
        b0 = (x0, 0.0, M.Z_FIRST + DACH_D); b1 = (x1, 0.0, M.Z_FIRST + DACH_D)
        dachflaeche(d, a0, a1, b0, b1, aussen=(-2, seite * 20, 30), saat=seite + 3)
        # Untersicht, Stirnbrett an der Traufe
        d.vieleck("Fachwerk", [Vector((x0, seite * ye, ze)), Vector((x1, seite * ye, ze)), Vector((x1, seite * M.OG_Y[1], M.Z_TRAUFE)), Vector((x0, seite * M.OG_Y[1], M.Z_TRAUFE))],
                  [(0, BA["planken"][1]), ((x1 - x0) / 4, BA["planken"][1]), ((x1 - x0) / 4, BA["planken"][0]), (0, BA["planken"][0])], zu=(0, seite * 4.8, 0))
        d.rechteck("Fachwerk", (x0, seite * ye, ze), (1, 0, 0), (0, 0, 1), x1 - x0, DACH_D, "balken_c", zu=(0, seite * 30, ze))
        # Ortgang: Stirnbretter und Untersicht links und rechts
        for x, wand_x in ((x0, M.OG_X[0]), (x1, M.OG_X[1])):
            p0 = Vector((x, seite * ye, ze)); p1 = Vector((x, 0, M.Z_FIRST))
            d.vieleck("Fachwerk", [p0, p1, p1 + EZ * DACH_D, p0 + EZ * DACH_D], [(0, BA["balken_c"][1]), (2, BA["balken_c"][1]), (2, BA["balken_c"][0]), (0, BA["balken_c"][0])],
                      zu=(x + (5 if x > 0 else -5), seite * 2, 10))
            q0 = Vector((wand_x, seite * ye, ze)); q1 = Vector((wand_x, 0, M.Z_FIRST))
            d.vieleck("Fachwerk", [p0, p1, q1, q0], [(0, BA["planken"][1]), (2, BA["planken"][1]), (2, BA["planken"][0]), (0, BA["planken"][0])], zu=((x + wand_x) / 2, seite * 2, 0))
    x1 = x1_hinten
    # Sparrenkoepfe unter dem Ueberstand der Traufe
    for seite in (-1, 1):
        xe = M.OG_X[1] - 0.2 if seite < 0 else x1 - 0.2
        k = int((xe - x0 - 0.2) / 0.85)
        for i in range(k + 1):
            x = x0 + 0.25 + i * (xe - x0 - 0.25) / k
            a = Vector((x, seite * (M.OG_Y[1] - 0.05), M.Z_TRAUFE - 0.02)); b = Vector((x, seite * (ye - 0.03), ze + 0.02))
            d.stab("Fachwerk", a, b, 0.12, "balken_c", breite=0.09, kappen=True)
    # First: Abdeckung aus zwei schmalen Streifen
    for seite in (-1, 1):
        a = Vector((x0 - 0.02, 0, M.Z_FIRST + DACH_D + 0.03)); b = Vector((x1 + 0.02, 0, M.Z_FIRST + DACH_D + 0.03))
        q = Vector((0, seite * 0.22, -0.22 * M.NEIGUNG + 0.04))
        d.vieleck("Firstblech", [a, b, b + q, a + q], [(0, BA["schiefer"][1]), ((x1 - x0) / 4, BA["schiefer"][1]), ((x1 - x0) / 4, BA["schiefer"][0] + 0.15), (0, BA["schiefer"][0] + 0.15)], zu=(0, seite, 40))
    for x in M.GAUBEN_X:
        gaube(d, x)
    schornstein(d)
    # Pultdach des Anbaus
    ax0, ax1 = M.ANBAU_X[0], M.ANBAU_X[1] + 0.25
    ya, yb = M.ANBAU_Y[0], M.ANBAU_Y[1] + 0.25
    zr = M.anbau_dach_z(ax1)
    dachflaeche(d, (ax1, ya, zr + 0.16), (ax1, yb, zr + 0.16), (ax0, ya, M.Z_ANBAU_FIRST + 0.16), (ax0, yb, M.Z_ANBAU_FIRST + 0.16), aussen=(20, 2, 30), stufen=2, saat=7)
    d.rechteck("Fachwerk", (ax1, ya, zr), (0, 1, 0), (0, 0, 1), yb - ya, 0.16, "balken_c", zu=(30, 2, zr))
    d.vieleck("Fachwerk", [Vector((ax1, ya, zr)), Vector((ax1, yb, zr)), Vector((M.ANBAU_X[1], yb, M.Z_ANBAU_TRAUFE)), Vector((M.ANBAU_X[1], ya, M.Z_ANBAU_TRAUFE))],
              [(0, BA["planken"][1]), (1, BA["planken"][1]), (1, BA["planken"][0]), (0, BA["planken"][0])], zu=(9, 2, 0))
    p0 = Vector((ax1, yb, zr)); p1 = Vector((ax0, yb, M.Z_ANBAU_FIRST))
    d.vieleck("Fachwerk", [p0, p1, p1 + EZ * 0.16, p0 + EZ * 0.16], [(0, BA["balken_c"][1]), (1, BA["balken_c"][1]), (1, BA["balken_c"][0]), (0, BA["balken_c"][0])], zu=(5, 30, 4))
    d.vieleck("Fachwerk", [p0, p1, Vector((ax0, M.ANBAU_Y[1], M.Z_ANBAU_FIRST)), Vector((ax1, M.ANBAU_Y[1], zr))], [(0, BA["planken"][1]), (1, BA["planken"][1]), (1, BA["planken"][0]), (0, BA["planken"][0])], zu=(5, 4.4, 0))
    helm(d)
    return d


def gaube(d, x):
    """Gaube auf der Marktseite: Putzfront mit Fenster, Wangen, Satteldach mit Schiefer; das hintere Ende steckt im Hauptdach."""
    b = 1.40
    yf = -3.30
    zf = dach_z(yf, oben=True) - 0.05
    hf = 1.45
    zt = zf + hf
    ruecken = lambda z: -(M.Z_FIRST + DACH_D - z) / M.NEIGUNG       # y, an dem das Hauptdach die Hoehe z hat (Vorderseite)
    xa, xb = x - b / 2, x + b / 2
    BAp = BA["putz"]
    # Front (Putz) mit Fensteroeffnung, Fenster 0,60 x 0,80
    fa, fb, fz0, fz1 = x - 0.30, x + 0.30, zf + 0.35, zf + 1.15
    first = zt + (b / 2 + 0.15) * 1.0
    giebel_h = first - 0.05 - zt
    breite_bei = lambda z: b / 2 * (1 - max(0.0, z - zt) / giebel_h)
    mauer(d, "Putz", (xa, yf), (1, 0), (0, -1), b, zf, first - 0.05, "putz", loecher=[Loch(fa - xa, fb - xa, fz0, fz1)], zbasis=zf, laibung=("Fachwerk", "balken_c"), dicke=0.12,
          grenze=lambda z: (b / 2 - breite_bei(z), b / 2 + breite_bei(z)), saat=int(x * 10) % 97)
    d.brett("Fachwerk", Vector((xa, yf, zf + 0.06)), Vector((xb, yf, zf + 0.06)), 0.12, (0, -1, 0), "balken_c", tiefe=0.04)
    for s in (xa + 0.07, xb - 0.07):
        d.brett("Fachwerk", Vector((s, yf, zf + 0.12)), Vector((s, yf, zt)), 0.12, (0, -1, 0), "balken_b", tiefe=0.04)
    for a, bb in (((fa, fz0 + 0.025), (fb, fz0 + 0.025)), ((fa, fz1 - 0.025), (fb, fz1 - 0.025)), ((fa + 0.025, fz0), (fa + 0.025, fz1)), ((fb - 0.025, fz0), (fb - 0.025, fz1)), ((x, fz0), (x, fz1))):
        d.stab("Fachwerk", Vector((a[0], yf + 0.06, a[1])), Vector((bb[0], yf + 0.06, bb[1])), 0.06, "balken_c", breite=0.045, oben=(0, -1, 0))
    # Wangen
    for s in (xa, xb):
        y1 = ruecken(zt)
        d.welt("Putz", [(s, yf, zf), (s, yf, zt), (s, y1, zt)], "putz", (s, yf, 0), (0, 1, 0), zf, hf, zu=(s + (s - x) * 5, yf + 0.3, zf + 0.5))
    # Satteldach der Gaube bis ins Hauptdach
    y_hinten = ruecken(first)
    for seite in (-1, 1):
        a0 = Vector((x + seite * (b / 2 + 0.15), yf - 0.22, zt - 0.06)); a1 = Vector((x + seite * (b / 2 + 0.15), ruecken(zt) + 0.2, zt - 0.06))
        dachflaeche(d, a0, a1, Vector((x, yf - 0.22, first)), Vector((x, y_hinten + 0.2, first)), aussen=(x + seite * 5, yf, first + 5), stufen=1, kurs=0.75, saat=int(x * 3) % 13)


def schornstein(d):
    x0, x1, y0, y1 = M.SCHORNSTEIN
    z0 = dach_z(y1) - 0.2
    z1 = M.Z_FIRST + 0.95
    d.kasten("Sandstein", (x0, y0, z0), (x1, y1, z1), "quader", ohne=("-z", "+z"))
    d.kasten("Sandstein", (x0 - 0.06, y0 - 0.06, z1), (x1 + 0.06, y1 + 0.06, z1 + 0.12), "quader")
    d.kasten("Eisen", (x0 + 0.12, y0 + 0.12, z1 + 0.12), (x1 - 0.12, y1 - 0.12, z1 + 0.13), "eisen", ohne=("-z",))


def helm(d):
    """Pyramidenhelm des Turms: Schiefer, unten flacher ausgestellt; Knauf, Stange und Wetterfahne aus Eisen."""
    u = M.UEBER_HELM
    x0, x1, y0, y1 = M.TURM_X[0] - u, M.TURM_X[1] + u, M.TURM_Y[0] - u, M.TURM_Y[1] + u
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    zf = M.Z_TURM_MAUER - 0.12
    knick = 0.55                      # Aufschiebling: unten flacher
    zk = zf + 0.30
    spitze = Vector((cx, cy, M.Z_HELM_SPITZE))
    ecken = [Vector((x0, y0, zf)), Vector((x1, y0, zf)), Vector((x1, y1, zf)), Vector((x0, y1, zf))]
    innen = [Vector((cx + (e.x - cx) * (1 - knick / (x1 - cx)), cy + (e.y - cy) * (1 - knick / (y1 - cy)), zk)) for e in ecken]
    for i in range(4):
        a0, a1 = ecken[i], ecken[(i + 1) % 4]
        b0, b1 = innen[i], innen[(i + 1) % 4]
        aussen = (a0 + a1) / 2 * 2 - Vector((cx, cy, zf)) + EZ * 2
        dachflaeche(d, a0, a1, b0, b1, aussen=aussen, stufen=1, saat=40 + i)
        dachflaeche(d, b0, b1, spitze, spitze, aussen=aussen + EZ * 3, stufen=3, kurs=0.75, hub=0.03, saat=50 + i)
        d.vieleck("Fachwerk", [a0, a1, a1 - EZ * 0.14, a0 - EZ * 0.14], [(0, BA["balken_c"][1]), (2, BA["balken_c"][1]), (2, BA["balken_c"][0]), (0, BA["balken_c"][0])], zu=aussen)
        w0, w1 = Vector((M.TURM_X[i in (1, 2)], M.TURM_Y[i in (2, 3)], zf - 0.14)), Vector((M.TURM_X[(i + 1) % 4 in (1, 2)], M.TURM_Y[(i + 1) % 4 in (2, 3)], zf - 0.14))
        d.vieleck("Fachwerk", [a0 - EZ * 0.14, a1 - EZ * 0.14, w1, w0], [(0, BA["planken"][1]), (1, BA["planken"][1]), (1, BA["planken"][0]), (0, BA["planken"][0])], zu=Vector((cx, cy, zf - 5)))
    d.dreh("Eisen", (cx, cy, M.Z_HELM_SPITZE - 0.05), [(0.05, 0.0), (0.13, 0.12), (0.13, 0.2), (0.04, 0.32)], "eisen", seiten=8)
    d.stab("Eisen", (cx, cy, M.Z_HELM_SPITZE + 0.3), (cx, cy, M.Z_FAHNE), 0.035, "eisen")
    fz = M.Z_FAHNE - 0.35
    d.vieleck("Eisen", [Vector((cx, cy, fz)), Vector((cx + 0.55, cy, fz + 0.02)), Vector((cx + 0.55, cy, fz + 0.22)), Vector((cx, cy, fz + 0.24))], [(0, 0.99)] * 4, zu=(cx, cy - 1, fz))
    d.vieleck("Eisen", [Vector((cx, cy, fz)), Vector((cx + 0.55, cy, fz + 0.02)), Vector((cx + 0.55, cy, fz + 0.22)), Vector((cx, cy, fz + 0.24))], [(0, 0.99)] * 4, zu=(cx, cy + 1, fz))
    d.stab("Eisen", (cx - 0.45, cy, fz + 0.12), (cx, cy, fz + 0.12), 0.02, "eisen")


def baue_scheiben():
    s = Teil("Scheiben", BA, saat=5)
    for w, (a0, a1), (z0, z1), art, _ in M.OEFFNUNGEN:
        if art in ("fenster", "bogenfenster", "schlitz"):
            mitte = -M.WAND_DICKE[w] / 2
            B.scheibe(s, "Fenster", w, a0, a1, z0, z1, mitte, zu=wandpunkt(w, (a0 + a1) / 2, (z0 + z1) / 2, 5))
            if art != "schlitz":
                B.scheibe(s, "Bleinetz", w, a0, a1, z0, z1, mitte + 0.006, zu=wandpunkt(w, (a0 + a1) / 2, (z0 + z1) / 2, 5))
    for x in M.GAUBEN_X:
        yf = -3.30; zf = dach_z(yf, oben=True) - 0.05
        for stoff, d in (("Fenster", 0.0), ("Bleinetz", -0.006)):
            pts = [Vector((x - 0.30, yf + 0.06 + d, zf + 0.35)), Vector((x + 0.30, yf + 0.06 + d, zf + 0.35)), Vector((x + 0.30, yf + 0.06 + d, zf + 1.15)), Vector((x - 0.30, yf + 0.06 + d, zf + 1.15))]
            s.vieleck(stoff, pts, [(0, 1), (0.6, 1), (0.6, 0.2), (0, 0.2)], zu=(x, yf - 5, zf))
    return s


def main():
    opt = B.optionen()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    stoffe = B.stoffe_aussen()
    haus, dach, scheiben = baue_haus(), baue_dach(), baue_scheiben()
    summe = haus.dreiecke() + dach.dreiecke() + scheiben.dreiecke()
    print(f"DREIECKE aussen: Haus {haus.dreiecke()}, Dach {dach.dreiecke()}, Scheiben {scheiben.dreiecke()}, Summe {summe} (Budget 8000-15000)")
    o_haus, o_dach = haus.objekt(stoffe), dach.objekt(stoffe)
    o_scheiben = scheiben.objekt(stoffe, mit_farbe=False)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0))
    boden = bpy.context.active_object; boden.name = "Kontrollboden"
    boden.data.materials.append(B.stoff("Kontrollboden", farbe=(0.36, 0.33, 0.28)))
    B.uv2_und_ao([([o_haus, o_dach], [boden])], "rathaus", "rathaus", mit_ao="ohne-ao" not in opt)
    a0, a1 = M.ARKADE[1]
    anker = [
        B.anker("Anker_Tuer", ((a0 + a1) / 2, M.EG_Y[0] - 0.9, 0.02), blick="nord"),
        B.anker("Anker_Laterne", ((M.ARKADE[0][1] + M.ARKADE[1][0]) / 2, M.EG_Y[0] - 0.02, 2.55), blick="sued"),
        B.anker("Anker_Schornstein", ((M.SCHORNSTEIN[0] + M.SCHORNSTEIN[1]) / 2, (M.SCHORNSTEIN[2] + M.SCHORNSTEIN[3]) / 2, M.Z_FIRST + 1.1)),
    ]
    B.exportiere([o_haus, o_dach, o_scheiben] + anker, os.path.join(B.ZIEL_MODELL, "rathaus.glb"))
    if "bilder" in opt:
        o = opt["bilder"]; os.makedirs(o, exist_ok=True)
        # Spielkamera: Zoom 25 m von vorn links oben (wie die Strategiekamera, Blick schraeg von oben)
        B.kontrollbild(os.path.join(o, "aussen_spielkamera.jpg"), (-20.0, -24.0, 22.0), (0.8, 0.0, 8.0), brennweite=35)
        B.kontrollbild(os.path.join(o, "aussen_artwork_blick.jpg"), (-26.0, -27.0, 20.0), (0.8, 0.0, 9.5), brennweite=40)
        B.kontrollbild(os.path.join(o, "aussen_hinten_rechts.jpg"), (23.0, 21.0, 17.0), (1.0, 0.0, 8.5), brennweite=35)
        B.kontrollbild(os.path.join(o, "aussen_portal_nah.jpg"), (-2.0, -10.0, 2.4), (0.6, -4.3, 2.4), brennweite=32)


if __name__ == "__main__":
    main()
