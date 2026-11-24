"""Entwurf neue Karren (Phase 1, nur Bilder und Zahlen, nichts im Spiel): Pferdekarren (1 Pferd) und Grosser Pferdekarren (2 Pferde, Drehschemel).

    blender -b --python tools/modelle/karren_neu_entwurf.py -- --aus=<Ordner> [--proben=16] [--nur=bestand_ochse,bestand_pferd,klein,gross,gross_knick]

Bestand: karren.ochse(), karren.pferd() (wie im Spiel) mit Ochse (tier_ochse_*.glb, Blender-Import) bzw. zwei Pferden (tier_pferd_*.glb, Spiel-Leser pferd2_spielrender).
Entwurf: eigene Bauteile aus karren.py (Kasten, Rad, Balken). Blender: Z oben, Fahrtrichtung -Y, Ursprung unter der hinteren Achse.
Gibt Dreiecke je Knoten und die Massrechnung (Knick Wagen/Vorderwagen gegen Abstand Pferd - Fussbrett) als Textzeilen `ENTWURF ...` aus.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import karren as K  # noqa: E402
import pferd2_bilder as pb  # noqa: E402
import pferd2_spielrender as SR  # noqa: E402

OPT = pb.OPT
WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
TIERE = os.path.join(WURZEL, "game", "assets", "modelle", "tiere")
ZT = json.load(open(os.path.join(WURZEL, "game", "daten", "zugtiere.json"), encoding="utf-8"))["tiere"]

KRUPPE_ZU_RING = 1.64       # Pferd B: Kruppe liegt 1,64 m hinter der Ringmitte des Kummets
WAAG_ABSTAND = 0.45         # Waagscheit bzw. naechstes Wagenteil 0,45 m hinter der Kruppe (IVC 8 bis 20 Zoll = 0,20 bis 0,51 m)
SPRITZ_ABSTAND = 0.35       # Spritzbrett 0,35 m hinter dem Waagscheit
RING_Z = 1.279              # Zugpunkt der Pferde im Ziehen


def dreiecke(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons) if o.type == "MESH" else 0


def melde(titel, objekte):
    teile = {o.name: dreiecke(o) for o in objekte if o.type == "MESH"}
    sichtbar = sum(t for n, t in teile.items() if not any(o.name == n and o.get("zustand") for o in objekte))
    print(f"ENTWURF {titel} dreiecke_sichtbar={sichtbar} gesamt={sum(teile.values())} {teile}")


def zustaende_aus(objekte):
    for o in objekte:
        if o.get("zustand"):
            o.hide_render = True


# ─────────────────────────────────────────────────────────────────────────────
#  Entwurf Pferdekarren (1 Pferd): Zweirad, Kasten wie der Ochsenkarren, Gabeldeichsel (Schere) statt Joch
# ─────────────────────────────────────────────────────────────────────────────

def klein(st):
    zf, halb, y0, y1, hw = 0.95, 0.72, -1.2, 1.2, 0.5            # Kasten unveraendert wie karren_ochse (gleiche Ladeflaeche, Werte unveraendert)
    maße = (halb, y0, y1, zf, hw)
    r, spur = 0.72, 0.90
    sx = 0.56                                                     # Schaftbaeume: 0,20 m ausserhalb der Flanke des Pferds (Flanke etwa 0,36 m)
    tip_y, tip_z = y0 - WAAG_ABSTAND - KRUPPE_ZU_RING, RING_Z     # Zugpunkt = Ringmitte des Kummets
    ende_y = tip_y + 0.45                                         # Schaftbaeume enden an der Schulter, kurz hinter dem Ring
    n = bs.Netz("Karren")
    K.kasten(n, st, halb, y0, y1, zf, hw, 3)
    K.balken(n, st, (-spur - 0.1, 0, r), (spur + 0.1, 0, r), 0.1, 0.1, "Holz", quer=(0, 1, 0))
    for s in (-1, 1):
        n.quader((s * (spur + 0.125), 0, r), (0.03, 0.06, 0.06), st["Eisen"])
        n.quader((s * (spur - 0.12), 0, r), (0.06, 0.12, 0.12), st["Eisen"])
        # Schaftbaum: unter dem Boden waagerecht, ab Stirnwand sanft ansteigend bis zur Schulter
        K.tiefe_deichsel(n, st, s * sx, y1 - 0.05, y0, 0.845, 0.845, 0.09, 0.08)
        K.balken(n, st, (s * sx, y0, 0.845), (s * sx, ende_y, 1.02), 0.09, 0.08, "Holz", quer=(1, 0, 0))
        n.quader((s * sx, y0 - 0.2, 0.9), (0.115, 0.05, 0.12), st["Eisen"])                  # Eisenband am Knick
        n.quader((s * sx, 0.0, 0.845), (0.115, 0.06, 0.115), st["Eisen"])
        n.quader((s * sx, ende_y + 0.25, 1.0), (0.08, 0.1, 0.08), st["Eisen"])               # Zugnagel am Schaft
        # Zugstrang: Haken am Kummet (0,347 m seitlich vom Ring) laeuft aussen an der Flanke zum Zugnagel
        pfad = [(s * 0.347, tip_y, tip_z), (s * 0.43, tip_y + 0.35, tip_z - 0.12), (s * 0.5, tip_y + 0.7, tip_z - 0.22), (s * sx, ende_y + 0.25, 1.02)]
        for a_, b_ in zip(pfad[:-1], pfad[1:]):
            K.rohr(n, st, a_, b_, 0.03, 0.03, "Leder", seiten=6)
    K.balken(n, st, (-sx, y0 - 0.15, 0.9), (sx, y0 - 0.15, 0.9), 0.07, 0.07, "Holz", quer=(0, 1, 0))     # Querholz zwischen den Schaftbaeumen
    K.stange(n, st, (halb + 0.045 + 0.012, y0 - 0.045, zf + hw + 0.06), 0.9)
    objekte = [n.objekt()]
    objekte += [K.rad_objekt(st, "Rad_L", r, 0.1, (spur, 0, r), 0), K.rad_objekt(st, "Rad_R", r, 0.1, (-spur, 0, r), 1)]
    for i in range(2):
        objekte.append(K.ladung("ochse", i, st, maße))
    objekte.append(K.plane("ochse", st, maße, 0.98))
    objekte.append(K.wimpel(st, (halb + 0.045 + 0.012, y0 - 0.045, zf + hw + 0.06 + 0.9 + 0.012), 0.0))
    objekte += [bs.anker("Anker_Deichsel", (0, tip_y, tip_z), 180.0), bs.anker("Anker_Fahrer_Seite", (1.1, tip_y + 0.2, 0.0), 180.0)]
    return objekte, {"tip_y": tip_y, "tip_z": tip_z}


# ─────────────────────────────────────────────────────────────────────────────
#  Entwurf Grosser Pferdekarren (2 Pferde): Vierrad, Drehschemel-Vorderwagen, Waagscheit hinter den Pferden (Bauart des Bestands)
# ─────────────────────────────────────────────────────────────────────────────

GROSS = dict(zf=1.04, halb=0.80, y0=-2.85, y1=0.55, hw=0.62, r_h=0.66, r_v=0.46, spur_h=1.02, spur_v=0.98, y_v=-2.20, ws=1.10, pole_z=0.86, bock_halb=0.65, bock_lang=1.175)


def gross_masse():
    g = dict(GROSS)
    g["pole_y"] = g["y0"] - g["bock_lang"] - SPRITZ_ABSTAND
    g["tip_y"] = g["pole_y"] - KRUPPE_ZU_RING - WAAG_ABSTAND
    return g


def gross(st):
    g = gross_masse()
    zf, halb, y0, y1, hw, y_v = g["zf"], g["halb"], g["y0"], g["y1"], g["hw"], g["y_v"]
    maße = (halb, y0, y1, zf, hw)
    pole_y, pole_z, tip_y = g["pole_y"], g["pole_z"], g["tip_y"]
    # Wagen (Kasten, Hinterachse, Bock)
    n = bs.Netz("Karren")
    K.kasten(n, st, halb, y0, y1, zf, hw, 4)
    K.balken(n, st, (-g["spur_h"] - 0.1, 0, g["r_h"]), (g["spur_h"] + 0.1, 0, g["r_h"]), 0.09, 0.09, "Holz", quer=(0, 1, 0))
    for s in (-1, 1):
        n.quader((s * (g["spur_h"] + 0.125), 0, g["r_h"]), (0.03, 0.06, 0.06), st["Eisen"])
        K.balken(n, st, (s * 0.55, y1 - 0.1, zf - 0.105), (s * 0.55, y0 + 0.05, zf - 0.105), 0.09, 0.09, "Holz", quer=(1, 0, 0))     # Langhoelzer
    n.quader((0, 0, (zf - 0.105 + g["r_h"] + 0.045) / 2), (1.3, 0.14, zf - 0.105 - g["r_h"] - 0.045), st["Holz"], ohne=("+z",))
    n.quader((0, y0 - 0.6, zf - 0.02), (2 * g["bock_halb"], g["bock_lang"] - 0.025, 0.04), st["Holz alt"])                                # Fussbrett
    n.quader((0, y0 - g["bock_lang"] + 0.01, zf + 0.16), (2 * g["bock_halb"], 0.04, 0.34), st["Bretter"])                                 # Spritzbrett
    for s in (-1, 1):
        n.quader((s * (g["bock_halb"] + 0.01), y0 - 0.6, zf + 0.16), (0.05, g["bock_lang"] - 0.025, 0.32), st["Holz alt"])
        n.quader((s * 0.4, y0 - 0.55, zf + 0.2), (0.07, 0.07, 0.42), st["Holz"])
    n.quader((0, y0 - 0.55, zf + 0.446), (1.0, 0.38, 0.05), st["Holz"])
    n.quader((0, y0 - 0.34, zf + 0.7), (0.95, 0.06, 0.38), st["Holz"])
    K.stange(n, st, (halb + 0.045 + 0.012, y0 - 0.045, zf + hw + 0.06), 0.9)
    wagen = [n.objekt(), K.rad_objekt(st, "Rad_HL", g["r_h"], 0.11, (g["spur_h"], 0, g["r_h"]), 0), K.rad_objekt(st, "Rad_HR", g["r_h"], 0.11, (-g["spur_h"], 0, g["r_h"]), 1)]
    # Vorderwagen (dreht um den Reibnagel bei y_v): Achsholz, Schemel, Deichsel, Waagscheit, Zugstraenge, Vorderraeder
    v = bs.Netz("Vorderwagen")
    K.balken(v, st, (-g["spur_v"] - 0.1, y_v, g["r_v"]), (g["spur_v"] + 0.1, y_v, g["r_v"]), 0.09, 0.09, "Holz", quer=(0, 1, 0))
    for s in (-1, 1):
        v.quader((s * (g["spur_v"] + 0.125), y_v, g["r_v"]), (0.03, 0.06, 0.06), st["Eisen"])
    v.quader((0, y_v, (zf - 0.06 + g["r_v"] + 0.045) / 2), (1.3, 0.5, zf - 0.06 - g["r_v"] - 0.045), st["Holz"], ohne=("+z",))              # Schemel (Drehscheibe) unter dem Kasten
    v.quader((0, y_v, zf - 0.06), (0.12, 0.12, 0.12), st["Eisen"])                                                                      # Reibnagel
    K.balken(v, st, (0, y_v + 0.1, 0.62), (0, pole_y, pole_z), 0.14, 0.12, "Holz", quer=(1, 0, 0))
    K.rohr(v, st, (-g["ws"], pole_y, pole_z), (g["ws"], pole_y, pole_z), 0.05, 0.05, "Holz", seiten=8)
    v.quader((0, pole_y - 0.02, pole_z), (0.16, 0.18, 0.15), st["Eisen"])
    for s in (-1, 1):
        v.quader((s * g["ws"], pole_y, pole_z), (0.07, 0.11, 0.11), st["Eisen"])
        pfad = [(s * g["ws"], pole_y - 0.04, pole_z), (s * 1.09, pole_y - 1.0, pole_z + 0.14), (s * 1.08, tip_y + 0.95, RING_Z - 0.22), (s * 1.06, tip_y + 0.40, RING_Z - 0.08), (s * (0.62 + 0.347), tip_y, RING_Z)]
        for a_, b_ in zip(pfad[:-1], pfad[1:]):
            K.rohr(v, st, a_, b_, 0.03, 0.03, "Leder", seiten=6)
    vorn = [v.objekt(), K.rad_objekt(st, "Rad_VL", g["r_v"], 0.1, (g["spur_v"], y_v, g["r_v"]), 0), K.rad_objekt(st, "Rad_VR", g["r_v"], 0.1, (-g["spur_v"], y_v, g["r_v"]), 1)]
    sonst = [K.ladung("pferd", i, st, maße) for i in range(3)] + [K.plane("pferd", st, maße, 1.15)]
    ank = [bs.anker("Anker_Pferd_L", (0.62, tip_y, RING_Z), 180.0), bs.anker("Anker_Pferd_R", (-0.62, tip_y, RING_Z), 180.0), bs.anker("Anker_Deichselende", (0, pole_y, pole_z), 180.0),
           bs.anker("Anker_Bock", (0, y0 - 0.55, zf + 0.446), 180.0)]
    return wagen, vorn, sonst + ank, g


# ─────────────────────────────────────────────────────────────────────────────
#  Tiere
# ─────────────────────────────────────────────────────────────────────────────

def pferd_setzen(pferd, mat, name, anker_xyz, bild):
    az = ZT["pferd"]["anker_ziehen_godot"]
    Q, Nw = pferd.pose("Ziehen", bild)
    return SR.netz(name, pferd, Q, Nw, mat, (anker_xyz[0] - az[0], anker_xyz[1] + az[2], anker_xyz[2] - az[1]))


def ochse_setzen(anker_xyz):
    az = ZT["ochse"]["anker_ziehen_godot"]
    vor = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(TIERE, "tier_ochse_rotbraun.glb"))
    namen = [o.name for o in bpy.data.objects if o not in vor]
    for nm in namen:
        if nm.startswith(("Fern_", "Icosphere")):
            bpy.data.objects.remove(bpy.data.objects[nm])
    neu = [bpy.data.objects[nm] for nm in namen if nm in bpy.data.objects]
    halter = bpy.data.objects.new("_OchseHalter", None)
    bpy.context.scene.collection.objects.link(halter)
    halter.location = (anker_xyz[0] - az[0], anker_xyz[1] + az[2], anker_xyz[2] - az[1])
    for o in neu:
        if o.parent is None:
            o.parent = halter


# ─────────────────────────────────────────────────────────────────────────────
#  Bilder
# ─────────────────────────────────────────────────────────────────────────────

ANSICHTEN = {"seite": (0, 0, 640), "oben": (0, 89.0, 760), "spiel": (40, 36, 760)}


def bilder(kam, name, ziel, mass_seite, mass_oben, aus, ansichten=("seite", "oben", "spiel")):
    for an in ansichten:
        az, el, h = ANSICHTEN[an]
        pb.aufnahme(kam, Vector(ziel), az, el, mass_seite if an == "seite" else mass_oben, os.path.join(aus, f"{name}_{an}.png"), 1300, h)


def szene_bestand_ochse(aus):
    bs.neue_szene()
    st = bs.Stoffe()
    o = K.ochse(st)
    zustaende_aus(o)
    melde("bestand_ochse", o)
    a = next(x for x in o if x.name == "Anker_Deichsel").location
    ochse_setzen(a)
    kam = pb.buehne()
    bilder(kam, "bestand_ochsenkarren", (0, -2.2, 1.0), 9.5, 9.5, aus)


def szene_bestand_pferd(aus, pferd):
    bs.neue_szene()
    st = bs.Stoffe()
    o = K.pferd(st)
    zustaende_aus(o)
    melde("bestand_pferd", o)
    mat = SR.material(pferd, True)
    for k, nm in enumerate(("Anker_Pferd_L", "Anker_Pferd_R")):
        a = next(x for x in o if x.name == nm).location
        pferd_setzen(pferd, mat, f"Pferd{k}", a, 6 if k == 0 else 19)
    kam = pb.buehne()
    bilder(kam, "bestand_pferdekarren", (0, -3.1, 1.0), 12.5, 12.5, aus)


def szene_klein(aus, pferd):
    bs.neue_szene()
    st = bs.Stoffe()
    o, m = klein(st)
    zustaende_aus(o)
    melde("klein", o)
    mat = SR.material(pferd, True)
    pferd_setzen(pferd, mat, "Pferd0", (0, m["tip_y"], m["tip_z"]), 6)
    kam = pb.buehne()
    bilder(kam, "entwurf_pferdekarren", (0, -1.7, 1.0), 9.5, 9.5, aus)


def szene_gross(aus, pferd, phi=0.0, name="entwurf_grosser_pferdekarren"):
    bs.neue_szene()
    st = bs.Stoffe()
    wagen, vorn, rest, g = gross(st)
    zustaende_aus(rest)
    melde("gross", wagen + vorn + rest)
    mat = SR.material(pferd, True)
    pf = [pferd_setzen(pferd, mat, f"Pferd{k}", (x, g["tip_y"], RING_Z), 6 if k == 0 else 19) for k, x in enumerate((0.62, -0.62))]
    pivot = bpy.data.objects.new("_Reibnagel", None)
    bpy.context.scene.collection.objects.link(pivot)
    pivot.location = (0, g["y_v"], 0)
    bpy.context.view_layer.update()
    for o in vorn + pf:
        o.parent = pivot
        o.matrix_parent_inverse = pivot.matrix_world.inverted()
    pivot.rotation_euler = (0, 0, math.radians(phi))
    kam = pb.buehne()
    bilder(kam, name, (0, -3.3, 1.0), 12.5, 12.5, aus)


# ─────────────────────────────────────────────────────────────────────────────
#  Massrechnung: Knick Wagen/Vorderwagen gegen Abstand Pferd zum Fussbrett (reine Geometrie von oben)
# ─────────────────────────────────────────────────────────────────────────────

def rechteck(x0, x1, y0, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def drehen(poly, phi, mitte):
    c, s = math.cos(math.radians(phi)), math.sin(math.radians(phi))
    return [(mitte[0] + (x - mitte[0]) * c - (y - mitte[1]) * s, mitte[1] + (x - mitte[0]) * s + (y - mitte[1]) * c) for x, y in poly]


def pkt_seg(p, a, b):
    ax, ay, bx, by = a[0], a[1], b[0], b[1]
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((p[0] - ax) * dx + (p[1] - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(p[0] - ax - t * dx, p[1] - ay - t * dy)


def innen(p, poly):
    z = 0
    for i in range(4):
        a, b = poly[i], poly[(i + 1) % 4]
        if (a[1] > p[1]) != (b[1] > p[1]) and p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]:
            z += 1
    return z % 2 == 1


def abstand(A, B):
    if any(innen(p, B) for p in A) or any(innen(p, A) for p in B):
        return 0.0
    d = 9e9
    for P, Q in ((A, B), (B, A)):
        for p in P:
            for i in range(4):
                d = min(d, pkt_seg(p, Q[i], Q[(i + 1) % 4]))
    return d


def knickrechnung():
    g = gross_masse()
    y_v, y0, tip_y = g["y_v"], g["y0"], g["tip_y"]
    kruppe = g["pole_y"] - WAAG_ABSTAND
    drehend = [rechteck(-1.10, 1.10, g["pole_y"] - 0.05, g["pole_y"] + 0.05)]                                     # Waagscheit (Eisenbeschlaege)
    for x in (0.62, -0.62):
        drehend.append(rechteck(x - 0.34, x + 0.34, kruppe - 2.3, kruppe))                                        # Pferdekoerper von oben (Brust bis Kruppe, Breite 0,68 m)
    for titel, halb, lang in (("Bock breit +-0,65 / 1,175 m (Bestand)", 0.65, 1.175), ("Bock schmal +-0,42 / 0,60 m", 0.42, 0.60)):
        fest = rechteck(-halb, halb, y0 - lang, y0 + 0.0)
        zeile = []
        for phi in (0, 5, 10, 15, 20, 25, 30, 35, 40, 45):
            d = min(abstand(drehen(p, phi, (0, y_v)), fest) for p in drehend)
            zeile.append(f"{phi}deg={d:.2f}m")
        print(f"ENTWURF knick {titel}: " + " ".join(zeile))
    # Radweg im Einschlag: Aussenkante Vorderrad beim Drehen gegen Kastenwand
    print(f"ENTWURF masse deichselabstand_zugpunkt_vorderachse={abs(tip_y - y_v):.3f} deichselabstand_zugpunkt_hinterachse={abs(tip_y):.3f} radstand={abs(y_v):.2f} "
          f"kruppe_bis_fussbrett_vorn={(y0 - g['bock_lang']) - kruppe:.2f}")


def main():
    aus = OPT["aus"]
    os.makedirs(aus, exist_ok=True)
    nur = OPT.get("nur", "bestand_ochse,bestand_pferd,klein,gross,gross_knick").split(",")
    pferd = SR.Pferd(os.path.join(TIERE, "tier_pferd_fuchs.glb"))
    if "bestand_ochse" in nur:
        szene_bestand_ochse(aus)
    if "bestand_pferd" in nur:
        szene_bestand_pferd(aus, pferd)
    if "klein" in nur:
        szene_klein(aus, pferd)
    if "gross" in nur:
        szene_gross(aus, pferd)
    if "gross_knick" in nur:
        szene_gross(aus, pferd, 30.0, "entwurf_grosser_pferdekarren_knick30")
    if "rechnung" in nur or "gross" in nur:
        knickrechnung()


if __name__ == "__main__":
    main()
