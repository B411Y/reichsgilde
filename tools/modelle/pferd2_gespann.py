"""Gespannbild und Durchdringung: Pferdekarren (karren_pferd.glb) mit zwei Pferden (Paket mit Geschirr, Ziehen-Bildfolge) an den Ankern Anker_Pferd_L/R des Karrens.

    blender -b --python tools/modelle/pferd2_gespann.py -- --karren=<karren_pferd.glb> --paket=<p5c_g.npz> --framen=<ziehen gehen_J.npz> --info=<geschirr_info.json> --aus=<Ordner>
        [--bild=6] [--tex=<Texturordner>] [--fell=fuchs,braun] [--ansichten=oben,spiel,vorn,seite] [--b=1400] [--h=900] [--proben=16] [--norm=<henry_norm.json>]

Das Pferd steht so, dass sein Zugpunkt (Ringmitte des Kummets, Mittel im Ziehen, `zugpunkt_ziehen_normiert`) auf dem Anker des Karrens liegt (Tier-Wurzel = Anker - anker_ziehen, wie im Spiel).
Das zweite Pferd zeigt das Bild um einen halben Zyklus verschoben (zwei Pferde nie im Gleichtakt). Durchdringung: jede Ecke der Karrenteile (Deichsel, Waage, Zugstraenge) gegen das
Koerpernetz der Pferde (naechster Punkt, Vorzeichen aus der Flaechennormalen): Zahl der Ecken im Koerper und kleinster Abstand, ausgegeben als `GESPANN`-Zeilen.
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_b_render as R  # noqa: E402
import pferd2_bilder as pb  # noqa: E402

OPT = pb.OPT
ANSICHTEN = {"oben": ((0.0, -5.3, 0.6), 0, 89.0, 12.0), "spiel": ((0.0, -4.4, 0.8), 40, 38, 12.0), "vorn": ((0.0, -6.0, 0.9), -35, 12, 6.5), "seite": ((0.0, -4.4, 0.9), 0, 0, 11.0),
             "vorn_nah": ((0.0, -7.0, 1.2), -20, 8, 3.4)}


def mensch(x, y):
    """Vergleichsfigur 1,75 m aus Quadern (nur Massstab)."""
    for name, z, sx, sy, sz in (("_mb", 0.45, 0.16, 0.12, 0.9), ("_mr", 1.25, 0.3, 0.16, 0.7), ("_mk", 1.64, 0.18, 0.18, 0.22)):
        pb.kasten(name + str(x), (x, y, z), (sx, sy, sz), (0.2, 0.35, 0.6, 1))


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=OPT["karren"])
    karren = [o for o in bpy.data.objects if o.type == "MESH"]
    for o in karren:                                             # Zustaende des Spiels: Stuetze, Ladung und Plane standardmaessig aus (Blender kennt die Zustaende nicht)
        if o.name.startswith(("Stuetze", "Ladung", "Plane")):
            o.hide_render = True
            o.hide_viewport = True
    anker = {o.name: o for o in bpy.data.objects if o.type == "EMPTY" and o.name.startswith("Anker_")}
    H = B.Haut()
    mn = B.lade_norm(OPT["norm"])
    d = np.load(OPT["paket"], allow_pickle=True)
    prims = [dict(mat=str(d[f"mat{i}"]), pos=d[f"pos{i}"], uv=d[f"uv{i}"], jo=d[f"jo{i}"], w=d[f"w{i}"], idx=d[f"idx{i}"]) for i in range(int(d["n"]))]
    info = json.load(open(OPT["info"]))
    zug = np.array(info["zugpunkt_ziehen_normiert"])
    Jfr = np.load(OPT["framen"])["J"].astype(float)
    nf = len(Jfr) - 1
    bild = int(OPT.get("bild", 6))
    farben = OPT.get("fell", "fuchs,braun").split(",")
    tex = OPT.get("tex", "C:/Reichsgilde_Austausch/pferd2/schritt_b/form/werk/tex")
    kam = pb.buehne()
    pferde = []
    horse_meshes = []
    for k, side in enumerate(("Anker_Pferd_L", "Anker_Pferd_R")):
        a = anker[side]
        ort = Vector(a.matrix_world.translation)
        f = (bild + (nf // 2 if k else 0)) % nf
        Ps = B.pose(H, Jfr[f], mn, prims)
        versatz = np.array([ort.x, ort.y, ort.z]) - zug
        fell = farben[k % len(farben)]
        mats = {n: R.material(n, os.path.join(tex, f"Horse_{fell}_2048.png"), None) for n in B.KOERPER + ("Geschirr",)}
        for i, p in enumerate(prims):
            P = Ps[i] + versatz
            if p["mat"] == "Hair":
                pass
            ob = R.netz_bauen(f"P{k}_{i}_{p['mat']}", P, p["uv"], p["idx"], mats[p["mat"]])
            if p["mat"] == "Horse":
                horse_meshes.append((P, p["idx"]))
        pferde.append((side, f, versatz))
    mensch(1.9, -5.0)
    # Durchdringung der Karrenteile gegen die Koerper
    baeume = [BVHTree.FromPolygons([tuple(v) for v in P], [tuple(int(i) for i in t) for t in idx]) for P, idx in horse_meshes]
    bpy.context.view_layer.update()
    for o in karren:
        if o.name.startswith(("Rad", "Ladung", "Plane", "Wimpel", "Decke", "Stuetze")):
            continue
        me = o.to_mesh()
        n_in, n_pruef, kleinst = 0, 0, 9.0
        for v in me.vertices:
            p = o.matrix_world @ v.co
            for bt in baeume:
                hit = bt.find_nearest(p, 0.6)
                if hit[0] is None:
                    continue
                n_pruef += 1
                innen = (p - hit[0]).dot(hit[1]) < 0
                if innen:
                    n_in += 1
                kleinst = min(kleinst, hit[3] if not innen else -hit[3])
        print("GESPANN", o.name, "Ecken", len(me.vertices), "gepruefte (naeher als 0,6 m)", n_pruef, "im Koerper", n_in, "kleinster Abstand m", round(kleinst, 3))
        o.to_mesh_clear()
    print("GESPANN Pferde", pferde)
    os.makedirs(OPT["aus"], exist_ok=True)
    b, h = int(OPT.get("b", 1400)), int(OPT.get("h", 900))
    for an in OPT.get("ansichten", "oben,spiel,vorn,seite").split(","):
        ziel, az, el, mass = ANSICHTEN[an]
        pb.aufnahme(kam, Vector(ziel), az, el, mass, os.path.join(OPT["aus"], f"gespann_{an}.png"), b, h)


if __name__ == "__main__":
    main()
