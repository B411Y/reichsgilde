"""Bilder und Zahlen von Pferd B (Original oder bearbeitetes Paket), selbst geskinnt (pferd2_b.py), keine Armatur in Blender.

    blender -b --python tools/modelle/pferd2_b_render.py -- --aus=<Ordner> [--modell=orig|<paket.npz>] [--posen="Skeleton|1 Ilde:0;Skeleton|6 Eat:0.5"] [--ansichten=seite,schraeg,vorn,kopf]
        [--tag=_x] [--ziel=1.6] [--norm=<json>] [--horse-tex=<png>] [--haarfix] [--haar-farbe=r,g,b] [--ohne-haar] [--mensch] [--mass] [--b=1000] [--h=700] [--proben=24]
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_bilder as pb  # noqa: E402
import pferd2_mass as pm  # noqa: E402

OPT = pb.OPT
TEX = "C:/Reichsgilde_Austausch/pferd3/textures/"


def prims_laden(H, modell):
    if modell == "orig":
        return H.prims
    d = np.load(modell, allow_pickle=True)
    n = int(d["n"])
    return [dict(mat=str(d[f"mat{i}"]), pos=d[f"pos{i}"], uv=d[f"uv{i}"], jo=d[f"jo{i}"], w=d[f"w{i}"], idx=d[f"idx{i}"]) for i in range(n)]


def material(name, horse_tex=None, haar_farbe=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.85

    def bild(datei, nicht_farbe=False):
        im = bpy.data.images.load(datei)
        if nicht_farbe:
            im.colorspace_settings.name = "Non-Color"
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = im
        return t
    if name == "Horse":
        t = bild(horse_tex or OPT.get("horse-tex", TEX + "Horse_baseColor.png"))
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    elif name == "Horseshoe":
        t = bild(TEX + "Horseshoe_baseColor.png")
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Metallic"].default_value = 0.6
    elif name == "Cornea":
        bsdf.inputs["Base Color"].default_value = (0.002, 0.002, 0.002, 1)
        bsdf.inputs["Roughness"].default_value = 0.1
    elif name == "Geschirr":                                    # vier Farbfelder aus dem UV (Leder, Holz, Eisen, Gurt) wie im Atlas des Spiel-GLB (pferd2_spiel.py): Bild mit 2 x 2 Pixeln, Naechster
        im = bpy.data.images.new("_geschirr_felder", 2, 2)
        farben = [(110, 72, 44), (176, 132, 84), (88, 90, 98), (96, 64, 40)]            # Leder, Holz, Eisen, Gurt
        def lin(c):
            return [((x / 255.0 + 0.055) / 1.055) ** 2.4 if x / 255.0 > 0.04045 else x / 255.0 / 12.92 for x in c]
        px = [0.0] * 16
        # Pixel (x, y) von unten: UV-Feld leder (0,25 / 0,25) liegt nach dem Umkehren von v oben links, siehe netz_bauen (v -> 1 - v)
        plaetze = {(0, 1): farben[0], (1, 1): farben[1], (0, 0): farben[2], (1, 0): farben[3]}
        for (x, y), f in plaetze.items():
            o = (y * 2 + x) * 4
            px[o:o + 4] = [x / 255.0 for x in f] + [1.0]                   # Bild ist sRGB: Werte unveraendert
        im.pixels = px
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = im
        t.interpolation = "Closest"
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.7
    elif name == "Hair":
        t = bild(OPT.get("haar-tex", TEX + "Hair_baseColor.png"))
        if "haar-rgba" in OPT:                                   # fertige RGBA-Haartextur wie im GLB: Farbe aus RGB, Alphamaske bei 0,5 (MASK)
            nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
            kr = nt.nodes.new("ShaderNodeMapRange")
            kr.inputs["From Min"].default_value = 0.45
            kr.inputs["From Max"].default_value = 0.55
            nt.links.new(t.outputs["Alpha"], kr.inputs["Value"])
            nt.links.new(kr.outputs["Result"], bsdf.inputs["Alpha"])
            try:
                m.surface_render_method = "DITHERED"
            except (AttributeError, TypeError):
                pass
        elif "haarfix" in OPT:
            fb = [float(x) for x in (haar_farbe or OPT.get("haar-farbe", "0.30,0.18,0.12")).split(",")]
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.blend_type = "MULTIPLY"
            mix.inputs["Factor"].default_value = 1.0
            mix.inputs["A"].default_value = (fb[0], fb[1], fb[2], 1)
            nt.links.new(t.outputs["Color"], mix.inputs["B"])
            nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
            kr = nt.nodes.new("ShaderNodeMapRange")
            kr.inputs["From Min"].default_value = 0.18
            kr.inputs["From Max"].default_value = 0.26
            nt.links.new(t.outputs["Color"], kr.inputs["Value"])
            nt.links.new(kr.outputs["Result"], bsdf.inputs["Alpha"])
            try:
                m.surface_render_method = "DITHERED"
            except (AttributeError, TypeError):
                pass
        else:                                                    # Original: Graustufenkarte als Farbe, undurchsichtig
            nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def vertex_normalen(P, idx):
    """Weiche Normalen: Flaechennormalen (flaechengewichtet) ueber alle Ecken gleicher Lage (UV-Nahtecken) gemittelt."""
    idx = np.asarray(idx)
    fn = np.cross(P[idx[:, 1]] - P[idx[:, 0]], P[idx[:, 2]] - P[idx[:, 0]])
    key = np.round(P * 2000).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.reshape(-1)
    acc = np.zeros((inv.max() + 1, 3))
    for k in range(3):
        np.add.at(acc, inv[idx[:, k]], fn)
    n = acc[inv]
    l = np.linalg.norm(n, axis=1, keepdims=True)
    return n / np.maximum(l, 1e-12)


def netz_bauen(name, P, uv, idx, mat):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(p) for p in P], [], [tuple(f) for f in idx])
    me.update()
    uvl = me.uv_layers.new(name="UV")
    for lp in me.loops:
        u = uv[lp.vertex_index]
        uvl.data[lp.index].uv = (u[0], 1.0 - u[1])
    if "glatt" in OPT:
        N = B.weiche_normalen(P, idx) if "weich2" in OPT else vertex_normalen(P, idx)
        me.shade_smooth()
        me.normals_split_custom_set_from_vertices([tuple(n) for n in N])
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(mat)
    return ob


def mensch(y):
    """Vergleichsfigur 1,75 m (Zylinder und Kugel), nur Massstab."""
    farbe = (0.75, 0.45, 0.2, 1)
    m = bpy.data.materials.new("_Mensch")
    m.use_nodes = True
    next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = farbe
    for name, z, r, h in (("_m1", 0.45, 0.13, 0.9), ("_m2", 1.25, 0.20, 0.7), ("_m3", 1.66, 0.09, 0.18)):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=r, radius2=r, depth=h)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        o = bpy.data.objects.new(name, me)
        o.location = (0, y, z)
        o.data.materials.append(m)
        bpy.context.scene.collection.objects.link(o)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    H = B.Haut()
    ziel = float(OPT.get("ziel", 1.6))
    normpfad = OPT.get("norm", os.path.join(OPT["aus"], "henry_norm.json"))
    os.makedirs(OPT["aus"], exist_ok=True)
    kopf_h = next(i for i, p in enumerate(H.prims) if p["mat"] == "Horse")
    if os.path.exists(normpfad):
        mn = B.lade_norm(normpfad)
    else:
        # Ausrichtung immer aus dem ORIGINAL in Idle 1, Bild 0 (Anteil 0)
        J0 = H.gelenkmatrizen("Skeleton|1 Ilde", 0.0)
        w0, _ = H.haut(H.prims[kopf_h], J0)
        P0 = blender_p = B.blender_koord(w0)
        mn = B.ausrichtung(H, P0, H.prims[kopf_h]["idx"], ziel)
        B.speichere_norm(normpfad, mn)
    pb.buehne_kam = None
    kam = pb.buehne()
    modelle = OPT.get("modell", "orig").split("+")
    tags = OPT.get("tag", "").split(",")
    for mi, modell in enumerate(modelle):
        modell, _, rest_ = modell.partition("@")
        htex, _, hfarbe = rest_.partition("@")
        prims = prims_laden(H, modell)
        mats = {n: material(n, htex or None, hfarbe or None) for n in B.KOERPER + ("Geschirr",)}
        kopf = next(i for i, p in enumerate(prims) if p["mat"] == "Horse")
        tag = tags[mi] if mi < len(tags) else tags[-1]
        posen = OPT.get("posen", "Skeleton|1 Ilde:0").split(";")
        if "framen" in OPT:
            Jfr = np.load(OPT["framen"])["J"]
            posen = ["gehen:%d" % int(i) for i in OPT.get("bilder", ",".join(str(i) for i in range(len(Jfr)))).split(",")]
        b, h = int(OPT.get("b", 1000)), int(OPT.get("h", 700))
        gemessen = False
        for pz in posen:
            clip, anteil = pz.rsplit(":", 1)
            J = Jfr[int(anteil)].astype(float) if "framen" in OPT else H.gelenkmatrizen(clip, float(anteil))
            Ps = B.pose(H, J, mn, prims)
            if "framen" in OPT:                                   # Weg des Rumpfes: Pferd laeuft durchs Bild (Boden steht), Mitte des Zyklus bei y = 0
                dat = np.load(OPT["framen"])
                dy = -(float(dat["V"]) * int(anteil) / 24.0 - float(dat["V"]) * float(dat["Tz"]) / 2)
                Ps = [P + np.array([0.0, dy, 0.0]) for P in Ps]
            for o in [o for o in bpy.data.objects if o.name.startswith("P_")]:
                bpy.data.objects.remove(o)
            obs = {}
            for i, (p, P) in enumerate(zip(prims, Ps)):
                if "ohne-haar" in OPT and p["mat"] == "Hair":
                    continue
                obs[i] = netz_bauen(f"P_{i}_{p['mat']}", P, p["uv"], p["idx"], mats[p["mat"]])
            for o in bpy.data.objects:
                d = o.dimensions
                if max(d) > 0.8 and not o.name.startswith(("_Boden", "_sq", "_sl", "_bodenlinie", "_lm")):
                    print("DREI GROSS", o.name, tuple(round(x, 2) for x in d), tuple(round(x, 2) for x in o.location))
            suffix = "_" + clip.split("|")[-1].replace(" ", "") + "_" + (("%03d" % int(anteil)) if "framen" in OPT else str(int(float(anteil) * 100)))
            if "mass" in OPT and not gemessen:
                gemessen = True
                R = pm.messen(None, obs[kopf], 1.0, 1.17 * ziel / 1.806, bonen=False)
                R["dreiecke_je_netz"] = {p["mat"]: int(len(p["idx"])) for p in prims}
                R["dreiecke_gesamt"] = int(sum(len(p["idx"]) for p in prims))
                json.dump(R, open(os.path.join(OPT["aus"], f"henry{tag}_mass.json"), "w"), indent=1)
                print("MASS", R.get("widerrist_z"), R.get("tiefe_zu_bein"), R["dreiecke_gesamt"], R["dreiecke_je_netz"])
            for an in OPT.get("ansichten", "seite").split(","):
                ziel_p, az, el, mass, lat = pb.ANSICHTEN[an]
                for o in [o for o in bpy.data.objects if o.name.startswith(("_lm", "_lt", "_m"))]:
                    bpy.data.objects.remove(o)
                pb.latte(az, lat)
                if "mensch" in OPT:
                    mensch(float(OPT.get("mensch-y", -1.2)))
                Pk = Ps[kopf]
                if an == "huf":
                    sh = Ps[next(i for i, p in enumerate(prims) if p["mat"] == "Horseshoe")]
                    sel = sh[(sh[:, 1] < 0) & (sh[:, 0] > 0)]
                    ziel_p = (float(sel[:, 0].mean()), float(sel[:, 1].mean()), 0.08)
                if an in ("kopf", "maehne"):
                    sel = Pk[Pk[:, 1] < Pk[:, 1].min() + (0.55 if an == "kopf" else 0.9) * ziel / 1.6]
                    ziel_p = (0, float(sel[:, 1].mean()), float(sel[:, 2].mean()) + (0.0 if an == "kopf" else 0.05))
                pb.aufnahme(kam, Vector(ziel_p), az, el, mass * (ziel / 1.806 if an not in ("kopf", "maehne") else ziel / 1.806), os.path.join(OPT["aus"], f"henry{tag}_{an}{suffix}.png"), b, h)





if __name__ == "__main__":
    main()
















