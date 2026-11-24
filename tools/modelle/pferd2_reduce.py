"""Probe-Reduktion von Pferd B auf <= 8.000 Dreiecke (Blender Decimate, Kollaps) in der Bindepose, Gelenk-Gewichte und UV bleiben erhalten (aus Gruppen/Schleifen gelesen).

    blender -b --python tools/modelle/pferd2_reduce.py -- --ein=<paket.npz> --aus=<paket.npz> [--Horse=5200] [--Hair=1500] [--Horseshoe=200] [--Cornea=200]

Das Paket (npz) hat je Netz i: mat, pos, uv, jo, w, idx (siehe pferd2_form_b.py). Rohecken sind gltf-Koordinaten (Einheiten des Autors); Decimate arbeitet unabhaengig von Achsen.
"""
import os
import sys

import bpy
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS)
SCHUTZ = {float(OPT.get("schutzw", 0.7)): ("_Hand_", "_Foot_", "_Toe")}                                         # Hufe/Fessel: weniger reduzieren (Gruppe "schutz", invertiert: Gewicht 0,7 = 30 % Reduktion)
ZIELE = {m: int(OPT.get(m, d)) for m, d in (("Horse", 5300), ("Hair", 2300), ("Horseshoe", 200), ("Cornea", 160), ("Geschirr", 784))}
# Gelenke, deren Umgebung bei der Reduktion mehr Dreiecke behalten soll (Kopf, Maul, Schulter, Brust, Stirnlocke): Gewicht 1 / 0,6
SCHUTZ_ALT = {1.0: ("Head", "Jaw", "Lip", "Nose", "Eye", "Ear", "Hair_06", "Hair_07", "Hair_08", "Hair_09", "Hair_10"), 0.6: ("Clavicle", "UpperArm", "Neck_03", "Neck_02")}


def reduzieren(mat, pos, uv, jo, w, idx, ziel, schutz):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ecken_uv = None
    if "weld" in OPT and mat == "Horse":                          # Nahtecken gleicher Lage zu einer Ecke verschweissen (sonst reissen die UV-Inselkanten beim Kollaps auf), UV je Dreiecksecke behalten
        _, erste, inv = np.unique(np.round(pos * 1e4).astype(np.int64), axis=0, return_index=True, return_inverse=True)
        inv = inv.reshape(-1)
        ecken_uv = uv[idx]
        pos, jo, w, idx = pos[erste], jo[erste], w[erste], inv[idx]
    me = bpy.data.meshes.new("m")
    me.from_pydata([tuple(p) for p in pos], [], [tuple(f) for f in idx])
    me.update()
    uvl = me.uv_layers.new(name="UV")
    if ecken_uv is not None:
        for poly in me.polygons:
            for c_, lp in enumerate(poly.loop_indices):
                uvl.data[lp].uv = tuple(ecken_uv[poly.index, c_])
    else:
        for lp in me.loops:
            uvl.data[lp.index].uv = tuple(uv[lp.vertex_index])
    ob = bpy.data.objects.new("o", me)
    bpy.context.scene.collection.objects.link(ob)
    gruppen = {}
    for v in range(len(pos)):
        for s in range(4):
            if w[v, s] > 1e-6:
                g = int(jo[v, s])
                if g not in gruppen:
                    gruppen[g] = ob.vertex_groups.new(name=f"j{g}")
                gruppen[g].add([v], float(w[v, s]), "ADD")
    if schutz and mat in ("Horse",):
        sg = ob.vertex_groups.new(name="schutz")
        for v in range(len(pos)):
            s = max([schutz.get(int(jo[v, k]), 0.0) * 1.0 for k in range(4) if w[v, k] > 0.3] or [0.0])
            if s > 0:
                sg.add([v], s, "REPLACE")
    ratio = min(1.0, ziel / len(idx))
    md = ob.modifiers.new("d", "DECIMATE")
    md.decimate_type = "COLLAPSE"
    md.ratio = ratio
    md.use_collapse_triangulate = True
    if schutz and mat in ("Horse",):
        md.vertex_group = "schutz"
        md.invert_vertex_group = True
        md.vertex_group_factor = 1.0
    bpy.context.view_layer.update()
    e = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    em = e.to_mesh()
    namen = {g.index: int(g.name[1:]) for g in ob.vertex_groups if g.name != "schutz"}
    n = len(em.vertices)
    P = np.array([tuple(v.co) for v in em.vertices])
    U = np.zeros((n, 2))
    gesehen = np.zeros(n, bool)
    uvd = em.uv_layers[0].data
    for lp in em.loops:
        if not gesehen[lp.vertex_index]:
            U[lp.vertex_index] = uvd[lp.index].uv
            gesehen[lp.vertex_index] = True
    J = np.zeros((n, 4), int)
    W = np.zeros((n, 4))
    for v in em.vertices:
        gs = sorted(((g.weight, namen[g.group]) for g in v.groups if g.group in namen), reverse=True)[:4]
        s = sum(x for x, _ in gs) or 1.0
        for k, (x, g) in enumerate(gs):
            J[v.index, k], W[v.index, k] = g, x / s
    F = np.array([tuple(p.vertices) for p in em.polygons])
    if ecken_uv is not None:                                         # Ergebnis: je (Ecke, UV) ein Eintrag, damit die Texturnaht bleibt, die Geometrie aber dicht ist
        tab, vi_l, uv_l, F2 = {}, [], [], []
        for poly in em.polygons:
            f2 = []
            for lp in poly.loop_indices:
                vi = em.loops[lp].vertex_index
                u_ = tuple(np.round(uvd[lp].uv, 5))
                if (vi, u_) not in tab:
                    tab[(vi, u_)] = len(vi_l)
                    vi_l.append(vi)
                    uv_l.append(u_)
                f2.append(tab[(vi, u_)])
            F2.append(f2)
        vi_a = np.array(vi_l)
        P, U, J, W, F = P[vi_a], np.array(uv_l), J[vi_a], W[vi_a], np.array(F2)
    e.to_mesh_clear()
    return P, U, J, W, F


def main():
    d = np.load(OPT["ein"], allow_pickle=True)
    n = int(d["n"])
    import pferd2_b as B
    H = B.Haut()
    schutz = {}
    for i, nm in enumerate(H.gelenknamen):
        for wert, teile in SCHUTZ.items():
            if any(t in nm for t in teile):
                schutz[i] = max(wert, schutz.get(i, 0.0))
    aus = {"n": n}
    gesamt = 0
    for i in range(n):
        mat = str(d[f"mat{i}"])
        P, U, J, W, F = reduzieren(mat, d[f"pos{i}"], d[f"uv{i}"], d[f"jo{i}"], d[f"w{i}"], d[f"idx{i}"], ZIELE[mat], schutz if "schutz" in OPT else {})
        aus[f"mat{i}"], aus[f"pos{i}"], aus[f"uv{i}"], aus[f"jo{i}"], aus[f"w{i}"], aus[f"idx{i}"] = mat, P, U, J, W, F
        gesamt += len(F)
        print("DREI", mat, len(d[f"idx{i}"]), "->", len(F), "Ecken", len(P))
    print("DREI gesamt", gesamt)
    np.savez_compressed(OPT["aus"], **aus)


if __name__ == "__main__":
    main()



