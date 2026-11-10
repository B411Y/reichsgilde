"""Knoechelreihe: vier Hilfsknochen Knoechel2..5.R (Kinder der Mittelhandknochen) an den Grundgelenken, Gewichte auf dem Handruecken (Gauss um das Grundgelenk).
v6: Die Aktion VERSCHIEBT sie beim Beugen (Handruecken-Richtung, bis etwa 7 mm) statt zu skalieren (Skalierung hob die Durchdringungen); vorher wird der Handruecken im Ruhezustand geglaettet (Entknittern) und die Faltenfarbe halbiert.
blender -b hand_v4.blend --python v5_knoechel.py -- <hand_v5.blend> [sigma_m=0.010] [tiefe_m=0.004]"""
import bpy, sys, math, os
import numpy as np
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]; aus = a[0]; GLATT = int(a[3]) if len(a) > 3 else 6; SIG = float(a[1]) if len(a) > 1 else 0.010; TIEFE = float(a[2]) if len(a) > 2 else 0.004
rig = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]; gl = bpy.data.objects["Handschuh"]
for pb in rig.pose.bones: pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion = (1, 0, 0, 0); pb.scale = (1, 1, 1)
bpy.context.view_layer.update()
import bmesh
# --- Handruecken entknittern: Laplace-Glaettung der Ruhe-Ecken, gewichtet mit "Mittelhand-Anteil" und Rueckenseite (nicht an den Fingern/Daumen)
me = gl.data; nV = len(me.vertices); nbr = [set() for _ in range(nV)]
for e in me.edges: nbr[e.vertices[0]].add(e.vertices[1]); nbr[e.vertices[1]].add(e.vertices[0])
P0 = np.array([v.co[:] for v in me.vertices]); NR = np.array([v.normal[:] for v in me.vertices])
vgn = {g.index: g.name for g in gl.vertex_groups}
wm = np.zeros(nV)
for v in me.vertices:
    for g in v.groups:
        if vgn[g.group].startswith(("metacarpal", "wrist")): wm[v.index] += g.weight
mask = np.clip(wm, 0, 1) * np.clip((NR[:, 2] - 0.4) / 0.4, 0, 1)
P = P0.copy()
for it in range(GLATT):
    Q = P.copy()
    for i in range(nV):
        if mask[i] > 0.01 and nbr[i]: Q[i] = P[i] + 0.5 * mask[i] * (np.mean([P[j] for j in nbr[i]], axis=0) - P[i])
    P = Q
dl = P - P0; dn = np.maximum(0.0, (dl * NR).sum(axis=1, keepdims=True)); dl = NR * dn   # nur nach aussen (Taeler fuellen, Spitzen nicht nach innen ziehen: sonst Schichten der Handflaeche durchdringen)
nl = np.linalg.norm(dl, axis=1, keepdims=True); P = P0 + dl * np.minimum(1.0, 0.003 / np.maximum(nl, 1e-9))   # hoechstens 3 mm
for v, q in zip(me.vertices, P): v.co = Vector(q.tolist())
me.update()
print("Handruecken geglaettet, mittlere Verschiebung mm", round(float(np.linalg.norm(P - P0, axis=1).mean() * 1000), 3), "max", round(float(np.linalg.norm(P - P0, axis=1).max() * 1000), 2))
if "Falten" in me.color_attributes:
    ca = me.color_attributes["Falten"]
    for i in range(nV):
        c = ca.data[i].color; ca.data[i].color = (1 - (1 - c[0]) * 0.5, 1 - (1 - c[1]) * 0.5, 1 - (1 - c[2]) * 0.5, 1)

# --- Textur: eingebackene Faltenschatten auf dem Handruecken (Mittelhand) weichzeichnen -> Leder wirkt gespannt statt knittrig (UV-Maske aus der Mittelhand-Ecken, Weichzeichner nur aus abgedeckten Pixeln)
def raster(tris_uv, W, H):
    M = np.zeros((H, W), np.float32)
    for (a, b, c) in tris_uv:
        xs = [a[0] * W, b[0] * W, c[0] * W]; ys = [a[1] * H, b[1] * H, c[1] * H]
        x0, x1 = int(max(0, min(xs))), int(min(W - 1, max(xs) + 1)); y0, y1 = int(max(0, min(ys))), int(min(H - 1, max(ys) + 1))
        if x1 < x0 or y1 < y0: continue
        X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
        if abs(d) < 1e-9: continue
        l1 = ((ys[1] - ys[2]) * (X - xs[2]) + (xs[2] - xs[1]) * (Y - ys[2])) / d; l2 = ((ys[2] - ys[0]) * (X - xs[2]) + (xs[0] - xs[2]) * (Y - ys[2])) / d
        ins = (l1 >= -0.02) & (l2 >= -0.02) & (l1 + l2 <= 1.02); M[y0:y1 + 1, x0:x1 + 1][ins] = 1.0
    return M
def boxblur(A, r, n=3):
    for _ in range(n):
        for ax in (0, 1):
            c = np.cumsum(np.concatenate([np.take(A, [0], axis=ax).repeat(r, axis=ax), A, np.take(A, [-1], axis=ax).repeat(r, axis=ax)], axis=ax), axis=ax)
            hi = np.take(c, np.arange(2 * r, c.shape[ax]), axis=ax); lo = np.concatenate([np.zeros_like(np.take(c, [0], axis=ax)), np.take(c, np.arange(0, c.shape[ax] - 2 * r - 1), axis=ax)], axis=ax)
            A = (hi - lo) / (2 * r + 1)
    return A
img = [n for n in gl.data.materials[0].node_tree.nodes if n.type == "TEX_IMAGE"][0].image; W, H = img.size
px = np.array(img.pixels[:], np.float32).reshape(H, W, 4); uvl = me.uv_layers.active.data
me.calc_loop_triangles(); alle = []; maske = []
for t in me.loop_triangles:
    uv = [tuple(uvl[i].uv) for i in t.loops]; alle.append(uv)
    if np.mean([mask[v] for v in t.vertices]) > 0.45: maske.append(uv)
C = raster(alle, W, H); Mk = raster(maske, W, H); R = int(os.environ.get("TEXR", "9"))
Cb = boxblur(C, R); wgt = np.maximum(Cb, 1e-4)
bl = np.stack([boxblur(px[..., c] * C, R) / wgt for c in range(3)], axis=-1)
Mf = np.clip(boxblur(Mk, 3), 0, 1)[..., None] * float(os.environ.get("TEXSTAERKE", "0.85"))
px[..., :3] = px[..., :3] * (1 - Mf) + bl * Mf
# graue/glasige Stellen an den Fingerspitzen (Pixal3D-Textur) auf Lederfarbe umfaerben: Ecken mit Hauptgewicht auf den Endgliedern (finger*-3)
wt = np.zeros(nV)
for v in me.vertices:
    for g in v.groups:
        if vgn[g.group].endswith("-3.R") and vgn[g.group].startswith("finger"): wt[v.index] += g.weight
tips = [[tuple(uvl[i].uv) for i in t.loops] for t in me.loop_triangles if np.mean([wt[v] for v in t.vertices]) > 0.35]
Mt = raster(tips, W, H); mx = px[..., :3].max(axis=2); mn = px[..., :3].min(axis=2); sat = (mx - mn) / (mx + 1e-3); lum = px[..., :3].mean(axis=2)
braun = (Mt > 0) & (sat >= 0.3); ziel = px[braun][:, :3].mean(axis=0); lm = float(lum[braun].mean())
grau = (Mt > 0) & ((sat < 0.25) | (lum > 1.35 * lm)); G = np.clip(boxblur(grau.astype(np.float32), 2), 0, 1)[..., None]
neu = ziel[None, None, :] * np.clip(lum / lm, 0.6, 1.1)[..., None]
px[..., :3] = px[..., :3] * (1 - G) + neu * G
print("Fingerspitzen: graue Pixel umgefaerbt", int(grau.sum()))
img.pixels = px.ravel().tolist(); img.pack()
print("Textur weichgezeichnet, Maskenanteil", round(float(Mk.sum() / max(C.sum(), 1)), 3))
bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode="EDIT"); eb = rig.data.edit_bones
mcp = {k: Vector(eb[f"finger{k}-1.R"].head) for k in (2, 3, 4, 5)}
print("Knochen", [b.name for b in eb if b.name.startswith(("meta", "wrist", "Wurzel"))])
for k in (2, 3, 4, 5):
    b = eb.new(f"Knoechel{k}.R"); b.head = mcp[k] + Vector((0, 0, -TIEFE)); b.tail = b.head + Vector((0, 0.008, 0)); b.parent = eb[f"metacarpal{k-1}.R"]; b.use_connect = False; b.use_deform = True
bpy.ops.object.mode_set(mode="OBJECT")
M = gl.matrix_world; Mi = rig.matrix_world.inverted()
for k in (2, 3, 4, 5): gl.vertex_groups.new(name=f"Knoechel{k}.R")
n = 0
for v in gl.data.vertices:
    p = Mi @ (M @ v.co)
    ws = {}
    for k in (2, 3, 4, 5):
        c = mcp[k]; d = p - c
        if d.z < -0.004: continue                       # nur Handruecken-Seite
        r2 = d.x * d.x + d.y * d.y * 0.7 + d.z * d.z
        w = 0.9 * math.exp(-r2 / (2 * SIG * SIG))
        if w > 0.02: ws[k] = w
    if not ws: continue
    tot = min(0.9, sum(ws.values())); alt = {g.group: g.weight for g in v.groups}
    for g, wv in alt.items(): gl.vertex_groups[g].add([v.index], wv * (1 - tot), "REPLACE")
    for k, w in ws.items(): gl.vertex_groups[f"Knoechel{k}.R"].add([v.index], w * tot / sum(ws.values()), "REPLACE")
    n += 1
# hoechstens 4 Einfluesse, normiert
for v in gl.data.vertices:
    gr = sorted([(g.weight, g.group) for g in v.groups], reverse=True)
    for w, g in gr[4:]: gl.vertex_groups[g].remove([v.index])
    gr = gr[:4]; s = sum(w for w, g in gr)
    if s > 0:
        for w, g in gr: gl.vertex_groups[g].add([v.index], w / s, "REPLACE")
print("Knoechel-Gewichte auf", n, "Ecken")
bpy.ops.wm.save_as_mainfile(filepath=aus)
