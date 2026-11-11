"""Pixal3D-Handschuh v2: Netz einpassen, Geruest anpassen, Gewichte (Abstand + MPFB-Datenuebertragung), speichern.
blender -b --python v2_bau.py -- <lo.glb> <farbe.png> <aus.blend> (BLENDER_USER_RESOURCES = MPFB-Profil)"""
import bpy, sys, os, math, importlib.util, json
import numpy as np
from mathutils import Vector, Matrix, kdtree
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import v2_lib, v2_rig_lib as L
glb, farbe, aus = sys.argv[sys.argv.index("--") + 1:][:3]
spec = importlib.util.spec_from_file_location("h3", os.path.join(os.path.dirname(os.path.abspath(__file__)), "handschuh_blender_basis.py"))
h3 = importlib.util.module_from_spec(spec); spec.loader.exec_module(h3)
bpy.ops.wm.read_factory_settings(use_empty=True)
basis, rig, _ = h3.hand_holen()
basis.name = "Hand_MPFB"
orig = {b.name: (np.array(b.head_local[:]), np.array(b.tail_local[:])) for b in rig.data.bones}
mp_co = np.array([v.co[:] for v in basis.data.vertices])
gname = {g.index: g.name for g in basis.vertex_groups}
mp_w = [{gname[g.group]: g.weight for g in v.groups if gname[g.group] in orig} for v in basis.data.vertices]
prop = {k: [(orig[f"finger{k}-{g}.R"][1] - orig[f"finger{k}-{g}.R"][0]) for g in (1, 2, 3)] for k in range(1, 6)}
prop = {k: np.array([np.linalg.norm(x) for x in v]) for k, v in prop.items()}
# Handschuh
bpy.ops.import_scene.gltf(filepath=glb)
gl = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name != "Hand_MPFB"][0]; gl.name = "Handschuh"
for o in list(bpy.context.scene.objects):
    if o.type == "EMPTY" or (o.type == "ARMATURE" and o is not rig): bpy.data.objects.remove(o, do_unlink=True)
gl.parent = None; gl.matrix_world = Matrix.Identity(4)
bpy.ops.object.select_all(action="DESELECT"); bpy.context.view_layer.objects.active = gl; gl.select_set(True)
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.mesh.remove_doubles(threshold=1e-6); bpy.ops.object.mode_set(mode="OBJECT")
print("GESCHWEISST", len(gl.data.vertices), len(gl.data.polygons))
me = gl.data
P = np.array([v.co[:] for v in me.vertices])
S = 0.30
def hand(p): return np.array([p[2], -p[0], -p[1]])
def tiefe(x, z):
    d = np.hypot(P[:, 0] - x, P[:, 2] - z); m = d < 0.035
    if m.sum() < 3: m = d < 0.07
    return 0.5 * (P[m, 1].min() + P[m, 1].max())
LM = {"wrist": (0.19, -0.01),
      2: [(-0.05, -0.065), (-0.375, -0.098)], 3: [(-0.06, 0.03), (-0.41, 0.055)],
      4: [(-0.05, 0.108), (-0.385, 0.165)], 5: [(-0.01, 0.155), (-0.285, 0.235)],
      1: [(0.12, -0.06), (0.0, -0.14), (-0.07, -0.185), (-0.135, -0.205)]}
def L3(xz): return np.array([xz[0], tiefe(*xz), xz[1]])
ketten = {}
for k in (2, 3, 4, 5):
    a = L3(LM[k][0]); b = L3(LM[k][1]); pr = prop[k] / prop[k].sum()
    pts = [a]
    for f in pr: pts.append(pts[-1] + (b - a) * f)
    ketten[k] = np.array(pts)
ketten[1] = np.array([L3(p) for p in LM[1]])
# Verfeinern: Gelenke zur Mitte des Querschnitts ziehen (Handschuh-Raum)
def poly_dist(pts, X):
    dm = np.full(len(X), 1e9)
    for a, b in zip(pts[:-1], pts[1:]):
        ab = b - a; t = np.clip(((X - a) @ ab) / (ab @ ab), 0, 1); c = a + t[:, None] * ab
        dm = np.minimum(dm, np.linalg.norm(X - c, axis=1))
    return dm
for it in range(5):
    D = np.stack([poly_dist(ketten[k], P) for k in (1, 2, 3, 4, 5)], 1); zu = np.argmin(D, 1) + 1
    for k in (1, 2, 3, 4, 5):
        pts = ketten[k]; sel = (zu == k) & (D[:, k - 1] < 0.065)
        for j in (1, 2, 3):
            d = pts[min(j + 1, 3)] - pts[j - 1]; d /= np.linalg.norm(d)
            rel = P[sel] - pts[j]; ent = rel @ d
            m = (np.abs(ent) < 0.018) if j < 3 else (ent > -0.05)
            if m.sum() < 5: continue
            c = P[sel][m].mean(0)
            if j < 3:
                dv = c - pts[j]; dv -= d * (dv @ d); pts[j] = pts[j] + 0.7 * dv
            else:
                ende = (rel @ d).max(); pts[3] = pts[3] + d * (ende - 0.022 - 0) * 0.5 + 0  # Spitze: nahe Ende
                dv = c - pts[3]; dv -= d * (dv @ d); pts[3] = pts[3] + 0.5 * dv
W = L3(LM["wrist"])
def zuhand(pg): return hand(pg - W) * S
nP = np.array([zuhand(p) for p in P])
# Handflaeche ausduennen: nur die handflaechenseitige Haut wird zum Handruecken gezogen (Dicke * THIN), so kreuzen sich die Schichten nicht
THIN = float(os.environ.get("THIN", "0.45"))
def ss(a, b, x): t_ = np.clip((x - a) / (b - a), 0, 1); return t_ * t_ * (3 - 2 * t_)
for v, q in zip(me.vertices, nP): v.co = Vector(q.tolist())
me.update()
nrm = np.array([v.normal[:] for v in me.vertices])
kd2 = kdtree.KDTree(len(nP))
for i, p in enumerate(nP): kd2.insert(Vector((p[0], p[1], 0)), i)
kd2.balance()
from mathutils.bvhtree import BVHTree
bpy.context.view_layer.update(); bvh = BVHTree.FromObject(gl, bpy.context.evaluated_depsgraph_get())
dick = np.zeros(len(nP))
for i, p in enumerate(nP):
    o_ = Vector(p.tolist()) + Vector((0, 0, 0.003)); dd_ = 0.0
    for _k in range(4):
        hit = bvh.ray_cast(o_, Vector((0, 0, 1)))
        if hit[0] is None: break
        if hit[1].z > 0.2: dd_ = (hit[0] - Vector(p.tolist())).length; break
        o_ = hit[0] + Vector((0, 0, 0.0005))
    dick[i] = dd_
wy = ss(-0.005, 0.015, nP[:, 1]) * (1 - ss(0.040, 0.075, nP[:, 1])) * (1 - ss(0.025, 0.050, np.abs(nP[:, 0] - float(os.environ.get("THINX", "0.0")))))
wy = wy * (1 - ss(0.1, 0.5, np.array([0.0] * len(nP))))
wn = ss(0.05, 0.45, -nrm[:, 2])
schub = np.minimum(THIN * np.minimum(dick, 0.075), 0.008) * wy * wn
nbr = [[] for _ in nP]
for e_ in me.edges: a_, b_ = e_.vertices; nbr[a_].append(b_); nbr[b_].append(a_)
for _ in range(3):
    sn = schub.copy()
    for i, l in enumerate(nbr):
        if l: sn[i] = 0.5 * schub[i] + 0.5 * np.mean(schub[l])
    schub = np.minimum(sn, THIN * dick)
nP[:, 2] += schub
print("DUENN: max Schub mm", round(float(schub.max()) * 1000, 1), "Ecken", int((schub > 1e-4).sum()))
for v, q in zip(me.vertices, nP): v.co = Vector(q.tolist())
me.update()
CUT = float(os.environ.get("STULPE_BIS", "-0.058"))
import bmesh
bm = bmesh.new(); bm.from_mesh(me)
uvl = bm.loops.layers.uv.active
geo = bm.verts[:] + bm.edges[:] + bm.faces[:]
res = bmesh.ops.bisect_plane(bm, geom=geo, plane_co=(0, CUT, 0), plane_no=(0, 1, 0), clear_outer=False, clear_inner=True)
rand = [e for e in bm.edges if len(e.link_faces) == 1]
if rand:
    kd0 = kdtree.KDTree(len(bm.verts)); inner = [v for v in bm.verts if not any(e in rand for e in v.link_edges)]
    for i, v in enumerate(inner): kd0.insert(v.co, i)
    kd0.balance()
    bmesh.ops.holes_fill(bm, edges=rand, sides=500)
    bm.faces.ensure_lookup_table()
    # UV fuer neue Flaechen: UV der naechsten Randecke
    uvmap = {}
    for f in bm.faces:
        for l in f.loops:
            if l.vert in [v for e in rand for v in e.verts]: pass
    for f in bm.faces:
        if any(abs(l[uvl].uv.x) < 1e-9 and abs(l[uvl].uv.y) < 1e-9 for l in f.loops) or True:
            pass
bm.to_mesh(me); bm.free()
# neue (UV-lose) Flaechen auf nahe UV setzen
me.update()
uvd = me.uv_layers.active.data
ploops = {}
for p in me.polygons:
    for li in p.loop_indices:
        ploops.setdefault(p.vertices[li - p.loop_start], []).append(li)
nP = np.array([v.co[:] for v in me.vertices])
# Deckel (Schnittflaeche) auf eine braune Rueckenstelle der Textur legen
fl_ = json.load(open(os.path.join(os.path.dirname(farbe), "lo_flaechen.json")))
rk = [f for f in fl_ if f["n"][1] < -0.5 and f["c"][0] < 0.1][0]; uvz = (sum(u for u, v in rk["uv"]) / 3, sum(v for u, v in rk["uv"]) / 3)
cap = 0
for p in me.polygons:
    if all(abs(me.vertices[i].co.y - CUT) < 1e-4 for i in p.vertices):
        for li in p.loop_indices: me.uv_layers.active.data[li].uv = uvz
        cap += 1
print("DECKEL", cap, "Flaechen")
print("NACH SCHNITT: Ecken", len(me.vertices), "Dreiecke", len(me.polygons))
# Material
mat = bpy.data.materials.new("Leder"); mat.use_nodes = True; nt = mat.node_tree
for n in list(nt.nodes): nt.nodes.remove(n)
tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images.load(farbe); tn.image.pack()
b = nt.nodes.new("ShaderNodeBsdfPrincipled"); o = nt.nodes.new("ShaderNodeOutputMaterial")
b.inputs["Roughness"].default_value = 0.62; b.inputs["Metallic"].default_value = 0.0
lift = nt.nodes.new("ShaderNodeBrightContrast"); lift.inputs["Bright"].default_value = float(os.environ.get("HELL", "0.0")); lift.inputs["Contrast"].default_value = 0.0
nt.links.new(tn.outputs["Color"], lift.inputs["Color"]); nt.links.new(lift.outputs["Color"], b.inputs["Base Color"]); nt.links.new(b.outputs["BSDF"], o.inputs["Surface"])
me.materials.clear(); me.materials.append(mat)
# Geruest anpassen
bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode="EDIT")
eb = rig.data.edit_bones
def setz(name, head, tail, parent=None):
    b = eb[name]; b.use_connect = False; b.head = Vector(head); b.tail = Vector(tail); b.align_roll(Vector((0, 0, 1)))
    if parent: b.parent = eb[parent]
wk_t = np.array([0, 0.02, 0.0])
setz("lowerarm01.R", (0, -0.075, 0), (0, -0.04, 0)); setz("lowerarm02.R", (0, -0.04, 0), (0, 0, 0), "lowerarm01.R"); setz("wrist.R", (0, 0, 0), tuple(wk_t), "lowerarm02.R")
HK = {}
for k in (2, 3, 4, 5):
    pts = [zuhand(p) for p in ketten[k]]; HK[k] = pts
    mc = f"metacarpal{k-1}.R"; setz(mc, tuple(wk_t), tuple(pts[0]), "wrist.R")
    for g in (1, 2, 3): setz(f"finger{k}-{g}.R", tuple(pts[g - 1]), tuple(pts[g]), mc if g == 1 else f"finger{k}-{g-1}.R")
pts = [zuhand(p) for p in ketten[1]]; HK[1] = pts
for g in (1, 2, 3): setz(f"finger1-{g}.R", tuple(pts[g - 1]), tuple(pts[g]), "wrist.R" if g == 1 else f"finger1-{g-1}.R")
bpy.ops.object.mode_set(mode="OBJECT")
neu = {b.name: (np.array(b.head_local[:]), np.array(b.tail_local[:])) for b in rig.data.bones}
# MPFB-Hand auf das neue Geruest verformen (LBS, nur Lage/Laenge je Knochen)
def rot_von_nach(a, b):
    a = a / np.linalg.norm(a); b = b / np.linalg.norm(b); v = np.cross(a, b); c = a @ b
    if c < -0.9999: return -np.eye(3)
    vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]]); return np.eye(3) + vx + vx @ vx / (1 + c)
M = {}
for n, (h0, t0) in orig.items():
    if n == "Wurzel" or n not in neu: continue
    h1, t1 = neu[n]; R = rot_von_nach(t0 - h0, t1 - h1); s = np.linalg.norm(t1 - h1) / max(1e-9, np.linalg.norm(t0 - h0))
    M[n] = (R, s, h0, h1)
mp_neu = np.zeros_like(mp_co)
for i, (x, wd) in enumerate(zip(mp_co, mp_w)):
    tot = sum(wd.values()) or 1.0; acc = np.zeros(3)
    for n, w in wd.items():
        R, s, h0, h1 = M[n]; acc += (w / tot) * (h1 + s * (R @ (x - h0)))
    mp_neu[i] = acc
# Gewichte: Abstand
bones = [bb for bb in rig.data.bones if bb.name != "Wurzel"]; bn = [bb.name for bb in bones]
A = np.array([bb.head_local[:] for bb in bones]); B = np.array([bb.tail_local[:] for bb in bones])
D = np.zeros((len(nP), len(bones)))
for i, (a, bq) in enumerate(zip(A, B)):
    ab = bq - a; t = np.clip(((nP - a) @ ab) / (ab @ ab), 0, 1); c = a + t[:, None] * ab; D[:, i] = np.linalg.norm(nP - c, axis=1)
BAND = 0.016
dmin = D.min(1, keepdims=True); Wd = np.clip(1 - (D - dmin) / BAND, 0, 1) ** 2; Wd /= Wd.sum(1, keepdims=True)
# Datenuebertragung von der verformten MPFB-Hand
kd = kdtree.KDTree(len(mp_neu))
for i, p in enumerate(mp_neu): kd.insert(Vector(p.tolist()), i)
kd.balance()
Wt = np.zeros_like(Wd); mitDaten = np.zeros(len(nP), bool)
for vi, p in enumerate(nP):
    nn = kd.find_n(Vector(p.tolist()), 5)
    if nn[0][2] > 0.02: continue
    mitDaten[vi] = True; ws = np.array([1.0 / (n[2] + 0.002) for n in nn]); ws /= ws.sum()
    for (co, idx, dist), wgt in zip(nn, ws):
        for n, w in mp_w[idx].items():
            if n in bn: Wt[vi, bn.index(n)] += wgt * w
    s = Wt[vi].sum()
    if s > 0: Wt[vi] /= s
    else: mitDaten[vi] = False
ALPHA = float(os.environ.get("ALPHA", "0.5"))
Wg = Wd.copy(); Wg[mitDaten] = (1 - ALPHA) * Wd[mitDaten] + ALPHA * Wt[mitDaten]
print("DATEN: Ecken mit MPFB-Gewichten", int(mitDaten.sum()), "von", len(nP))
# Nachbarschaft glaetten (zwei Durchgaenge), nur 4 staerkste, normieren
nb = [[] for _ in nP]
for e in me.edges: a, bq = e.vertices; nb[a].append(bq); nb[bq].append(a)
for _ in range(2):
    N = Wg.copy()
    for i, l in enumerate(nb):
        if l: N[i] = 0.6 * Wg[i] + 0.4 * Wg[l].mean(0)
    Wg = N / N.sum(1, keepdims=True)
for i in range(len(Wg)):
    idx = np.argsort(-Wg[i])[4:]; Wg[i, idx] = 0; Wg[i] /= Wg[i].sum()
for g in list(gl.vertex_groups): gl.vertex_groups.remove(g)
for i, nm in enumerate(bn):
    vg = gl.vertex_groups.new(name=nm)
    for vi in np.nonzero(Wg[:, i] > 0.005)[0]: vg.add([int(vi)], float(Wg[vi, i]), "REPLACE")
# Gelenke herausarbeiten: Knoechelwuelste (Rueckseite), Faltenlinien als Eckenfarbe, Daumen dicker
nrm = np.array([v.normal[:] for v in me.vertices])
fal = np.ones(len(nP))
def fingerw(k): return sum(Wg[:, bn.index(f"finger{k}-{g}.R")] for g in (1, 2, 3))
FALT = float(os.environ.get("FALT", "1.0")); BUMP = float(os.environ.get("BUMP", "0.0035")); DAU = float(os.environ.get("DAUMEN", "1.14"))
for k in (2, 3, 4, 5):
    wf = fingerw(k)
    for g, (amp, bump, br) in zip((1, 2, 3), ((0.22, BUMP * 1.0, 0.009), (0.34, BUMP * 0.9, 0.008), (0.26, BUMP * 0.4, 0.006))):
        bb = rig.data.bones[f"finger{k}-{g}.R"]; J = np.array(bb.head_local[:]); u = np.array((bb.tail_local - bb.head_local).normalized()[:])
        rel = nP - J; d = rel @ u; perp = np.linalg.norm(rel - np.outer(d, u), axis=1)
        m = (wf > 0.5) & (perp < 0.03)
        gz = np.exp(-(d / 0.0035) ** 2); fal[m] = np.minimum(fal[m], 1 - min(0.8, amp * FALT) * gz[m])
        rueck = m & (nrm[:, 2] > 0.2)
        nP[rueck] += nrm[rueck] * (bump * np.exp(-(d[rueck] / 0.007) ** 2))[:, None]
# Daumen: radial um die Knochenkette aufdicken
wd = sum(Wg[:, bn.index(f"finger1-{g}.R")] for g in (1, 2, 3))
for vi in np.nonzero(wd > 0.6)[0]:
    best = None
    for g in (1, 2, 3):
        bb = rig.data.bones[f"finger1-{g}.R"]; a_ = np.array(bb.head_local[:]); b_ = np.array(bb.tail_local[:]); ab = b_ - a_
        t = np.clip(((nP[vi] - a_) @ ab) / (ab @ ab), 0, 1); c = a_ + t * ab; dd = np.linalg.norm(nP[vi] - c)
        if best is None or dd < best[0]: best = (dd, c)
    nP[vi] = best[1] + (nP[vi] - best[1]) * DAU
for v, q in zip(me.vertices, nP): v.co = Vector(q.tolist())
me.update()
ca = me.color_attributes.new("Falten", "FLOAT_COLOR", "POINT")
for i, c in enumerate(fal): ca.data[i].color = (c, c, c, 1)
me.color_attributes.active_color = ca
nt = gl.data.materials[0].node_tree
tex = [n for n in nt.nodes if n.type == "TEX_IMAGE"][0]; lift = [n for n in nt.nodes if n.type == "BRIGHTCONTRAST"][0]
cn = nt.nodes.new("ShaderNodeVertexColor"); cn.layer_name = "Falten"
mx_ = nt.nodes.new("ShaderNodeMix"); mx_.data_type = "RGBA"; mx_.blend_type = "MULTIPLY"; mx_.inputs[0].default_value = 1.0
nt.links.new(tex.outputs["Color"], mx_.inputs[6]); nt.links.new(cn.outputs["Color"], mx_.inputs[7]); nt.links.new(mx_.outputs[2], lift.inputs["Color"])
mod = gl.modifiers.new("Armature", "ARMATURE"); mod.object = rig
gl.parent = rig
bpy.ops.object.select_all(action="DESELECT"); bpy.context.view_layer.objects.active = gl; gl.select_set(True)
bpy.ops.object.shade_smooth()
# Fingerspitze fuer Anker (Zeigefinger): Ecke mit groesster Projektion der Zeigefingerwerte
i2 = bn.index("finger2-3.R"); sel = np.nonzero(Wg[:, i2] > 0.6)[0]
dir2 = (B[i2] - A[i2]) / np.linalg.norm(B[i2] - A[i2]); spitze = nP[sel][np.argmax(nP[sel] @ dir2)]
# Hilfsdaten fuer Folgeschritte
json.dump(dict(spitze=spitze.tolist(), kipp={k: L.kipp(rig, k) for k in (2, 3, 4, 5)}, mc_kipp={k: L.mc_kipp(rig, k) for k in (2, 3, 4, 5)}), open(aus + ".json", "w"))
print("KIPP", {k: round(L.kipp(rig, k), 1) for k in (2, 3, 4, 5)}, "MC", {k: round(L.mc_kipp(rig, k), 1) for k in (2, 3, 4, 5)})
print("GEWICHT Summe min/max", Wg.sum(1).min(), Wg.sum(1).max(), "max Einfluesse", int((Wg > 0).sum(1).max()))
basis.hide_set(True); basis.hide_render = True
bpy.data.objects.remove(basis, do_unlink=True)
# Mpfb-Ueberbleibsel (Menschenmesh) entfernen: nur Handschuh + Geruest behalten
for o in list(bpy.context.scene.objects):
    if o is not gl and o is not rig: bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=aus)
print("MESS", v2_lib.netz_messen(gl))









