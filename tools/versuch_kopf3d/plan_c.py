# Plan C: rohe TRELLIS-Form behalten, nur beleben (Skelett Hals/Kopf/Kiefer, Mundspalt, Blinzel-Textur).
#  ~/werkzeuge/mpfb/blender_mpfb.sh -b --python plan_c.py -- --glb=<trellis.glb> --tl=x,z --tr=x,z --aus=<kopf.glb>
#       [--zu=<front_zu.png> --mitte=0,0.05 --groesse=1.0] [--tris=11000] [--tex=2048]
# --tl/--tr: Augenmitten in der Frontansicht (TRELLIS-Einheiten, tl = Bildseite rechts). Alles andere automatisch.
import sys, os, math
import numpy as np
import bpy, bmesh
from mathutils import Vector, Matrix

a = dict(x[2:].split("=", 1) if "=" in x else (x[2:], "1") for x in sys.argv[sys.argv.index("--") + 1:] if x.startswith("--"))
ordner = os.path.dirname(a["aus"])
TEX = int(a.get("tex", 2048))


def log(*x):
    print("PLANC", *x, flush=True)


def netz_arrays(o):
    me = o.data
    v = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", v); v = v.reshape(-1, 3)
    mw = np.array(o.matrix_world)
    return v @ mw[:3, :3].T + mw[:3, 3]


def basis_bild(o):
    nt = o.data.materials[0].node_tree
    for n in nt.nodes:
        if n.type == "BSDF_PRINCIPLED" and n.inputs["Base Color"].links:
            f = n.inputs["Base Color"].links[0].from_node
            if f.type == "TEX_IMAGE":
                return f.image, f
    n = [n for n in nt.nodes if n.type == "TEX_IMAGE"][0]
    return n.image, n


def punkt_farben(o, img):
    me = o.data
    w, h = img.size
    px = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(px); px = px.reshape(h, w, 4)
    uv = np.empty(len(me.loops) * 2); me.uv_layers.active.data.foreach_get("uv", uv); uv = uv.reshape(-1, 2)
    vi = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", vi)
    x = np.clip((uv[:, 0] % 1.0) * (w - 1), 0, w - 1).astype(int)
    y = np.clip((uv[:, 1] % 1.0) * (h - 1), 0, h - 1).astype(int)
    col = np.zeros((len(me.vertices), 3), np.float32); col[vi] = px[y, x, :3]
    pix = np.zeros((len(me.vertices), 2), np.int64); pix[vi] = np.stack([x, y], 1)
    return col, pix, px


def klassen(c):
    r, g, b = c[:, 0], c[:, 1], c[:, 2]
    v = c.max(axis=1)
    k = np.zeros(len(c), np.int8)
    k[(v > 0.42) & (r > g * 1.04) & (r > b * 1.12)] = 1
    k[(v <= 0.42) & (r >= g * 0.95) & (r >= b)] = 2
    return k


def sm(x, a0, a1):
    t = np.clip((x - a0) / (a1 - a0), 0, 1)
    return t * t * (3 - 2 * t)


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a["glb"])
T = [o for o in bpy.data.objects if o.type == "MESH"][0]
timg, tnode = basis_bild(T)
tv = netz_arrays(T)
farbe, pix, tpx = punkt_farben(T, timg)
kl = klassen(farbe)
log("TRELLIS", len(tv), "Punkte", len(T.data.polygons), "Flächen")

# ---- Landmarken (in TRELLIS-Einheiten) -------------------------------------------------------------
def xz(s):
    return [float(q) for q in s.split(",")]
tlx, tlz = xz(a["tl"]); trx, trz = xz(a["tr"])
dE = math.hypot(tlx - trx, tlz - trz)
xm = (tlx + trx) / 2; zE = (tlz + trz) / 2
box = (np.abs(tv[:, 0] - xm) < 0.5 * dE) & (tv[:, 2] < zE - 0.85 * dE) & (tv[:, 2] > zE - 1.45 * dE)
front = tv[:, 1] < np.percentile(tv[box, 1], 45)
cand = np.where(box & front)[0]
rot = farbe[cand, 0] - farbe[cand, 1]
lip = cand[rot > np.percentile(rot, 88)]
mund = tv[lip]
mz = float(np.median(mund[:, 2])); mx0 = float(np.median(mund[:, 0]))
halbW = float((np.percentile(mund[:, 0], 96) - np.percentile(mund[:, 0], 4)) / 2)
halbW = min(max(halbW, 0.16 * dE), 0.34 * dE)
my = float(np.percentile(mund[:, 1], 5))
streifen = np.where((np.abs(tv[:, 0] - xm) < 0.12 * dE) & (tv[:, 2] < mz) & (tv[:, 2] > mz - 1.6 * dE))[0]
bins = np.arange(mz - 1.6 * dE, mz, 0.03 * dE)
fy = {}
for i in range(len(bins) - 1):
    m = streifen[(tv[streifen, 2] >= bins[i]) & (tv[streifen, 2] < bins[i + 1])]
    if len(m):
        fy[i] = tv[m, 1].min()
ref = min(v for k_, v in fy.items() if bins[k_] > mz - 0.9 * dE)
kinn_z = mz - 0.9 * dE
for i in sorted(fy, reverse=True):
    if fy[i] - ref > 0.35 * dE:
        kinn_z = bins[i + 1]
        break
log("Mund z %.3f x %.3f Halbbreite %.3f (%.2f dE) y %.3f | Kinn %.2f dE unter dem Mund | Lippenpunkte %d" % (mz, mx0, halbW, halbW / dE, my, (mz - kinn_z) / dE, len(lip)))

# ---- geschlossene Augen in eine zweite Textur schreiben ---------------------------------------------
timg_zu = None
if a.get("zu"):
    zimg = bpy.data.images.load(a["zu"])
    zw, zh = zimg.size
    zp = np.empty(zw * zh * 4, np.float32); zimg.pixels.foreach_get(zp); zp = zp.reshape(zh, zw, 4)[:, :, :3]
    zmx, zmz = xz(a.get("mitte", "0,0.05")); zos = float(a.get("groesse", "1.0"))
    # Pixel (Bildzeile 0 = oben); bpy-Pixel liegen unten beginnend -> Spiegelung
    px_x = ((tv[:, 0] - zmx) / zos + 0.5) * zw
    px_y = ((tv[:, 2] - zmz) / zos + 0.5) * zh          # in bpy-Zeilenzählung (unten = 0)
    px_y = np.clip(px_y, 0, zh - 1).astype(int); px_x = np.clip(px_x, 0, zw - 1).astype(int)
    h0, w0 = tpx.shape[:2]
    neu = tpx.copy()
    maske = np.zeros((h0, w0), np.float32)
    for cx, cz in ((tlx, tlz), (trx, trz)):
        d = np.hypot((tv[:, 0] - cx) / (0.30 * dE), (tv[:, 2] - cz) / (0.20 * dE))
        sel = np.where((d < 1.15) & (tv[:, 1] < np.percentile(tv[:, 1], 60)))[0]
        for v in sel:
            wgt = 1.0 - sm(d[v], 0.55, 1.15)
            x0, y0 = pix[v]
            ys, xs = slice(max(y0 - 2, 0), y0 + 3), slice(max(x0 - 2, 0), x0 + 3)
            neu[ys, xs, :3] = (1 - wgt) * neu[ys, xs, :3] + wgt * zp[px_y[v], px_x[v]]
    timg_zu = bpy.data.images.new("haut_zu_quelle", w0, h0)
    timg_zu.pixels.foreach_set(neu.ravel()); timg_zu.update()
    log("geschlossene Augen in Textur geschrieben")

# ---- Maßstab/Lage wie der MPFB-Kopf (Augenabstand 0,066 m, Augenmitte x=0, y=-0,146, z=1,64) -------------
def _vy(cx, cz):
    m = np.where(np.hypot(tv[:, 0] - cx, tv[:, 2] - cz) < 0.15 * dE)[0]
    return tv[m, 1].min()
iy = np.mean([_vy(cx, cz) for cx, cz in ((tlx, tlz), (trx, trz))])
s = 0.066 / dE
tgt = np.array([0.0, -0.146, 1.64]); src = np.array([xm, iy, zE])
M = Matrix.Translation(Vector(tgt)) @ Matrix.Scale(s, 4) @ Matrix.Translation(Vector(-src))
T.data.transform(M); T.data.update()
def nrm(p):
    return (np.asarray(p, float) - src) * s + tgt
mz, kinn_z = nrm([0, 0, mz])[2], nrm([0, 0, kinn_z])[2]
mx0, my = nrm([mx0, my, 0])[0], nrm([0, my, 0])[1]
halbW *= s; dEm = 0.066
tv = netz_arrays(T)
log("normiert: Mund z %.3f, Kinn z %.3f, Halbbreite %.4f" % (mz, kinn_z, halbW))

# ---- verdünnen, entfalten, backen ---------------------------------------------------------------------------
D = T.copy(); D.data = T.data.copy(); D.name = "gesicht"
bpy.context.scene.collection.objects.link(D)
D.data.materials.clear()
# UV-Nähte haben Punkte verdoppelt: zusammenführen, sonst zerreißt das Verdünnen das Netz
bm = bmesh.new(); bm.from_mesh(D.data)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=2e-5)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.to_mesh(D.data); bm.free()
while D.data.uv_layers:
    D.data.uv_layers.remove(D.data.uv_layers[0])
log("zusammengeführt:", len(D.data.vertices), "Punkte", len(D.data.polygons), "Flächen")
gr = D.vertex_groups.new(name="duenn")
dv = netz_arrays(D)
face = (np.abs(dv[:, 0]) < 1.5 * dEm) & (dv[:, 2] > kinn_z - 0.6 * dEm) & (dv[:, 2] < 1.64 + 1.4 * dEm)
for i in range(len(dv)):
    gr.add([i], float(a.get("gesichtw", 0.12)) if face[i] else 1.0, "REPLACE")
dec = D.modifiers.new("d", "DECIMATE"); dec.decimate_type = "COLLAPSE"
dec.vertex_group = "duenn"; dec.vertex_group_factor = float(a.get("fvf", 1.0)); dec.invert_vertex_group = bool(a.get("inv"))
dec.ratio = float(a.get("tris", 12500)) / (len(T.data.polygons))
for x in bpy.context.selected_objects: x.select_set(False)
D.select_set(True); bpy.context.view_layer.objects.active = D
bpy.ops.object.modifier_apply(modifier="d")
D.vertex_groups.remove(D.vertex_groups["duenn"])
bm = bmesh.new(); bm.from_mesh(D.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.to_mesh(D.data); bm.free()
dv = netz_arrays(D)
fz = np.sum((np.abs(dv[:, 0]) < 1.5 * dEm) & (dv[:, 2] > kinn_z - 0.6 * dEm) & (dv[:, 2] < 1.64 + 1.4 * dEm))
log("verdünnt: %d Flächen, davon Punkte im Gesicht %d von %d" % (len(D.data.polygons), fz, len(dv)))
# UV neu
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(70), island_margin=0.002)
bpy.ops.object.mode_set(mode="OBJECT")

sc = bpy.context.scene
sc.render.engine = "CYCLES"
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "CUDA"; prefs.get_devices()
    for d in prefs.devices: d.use = True
    sc.cycles.device = "GPU"
except Exception as ex:
    log("GPU nicht verfügbar", ex)
sc.cycles.samples = 4


def backen(quelle_bild, name):
    tnode.image = quelle_bild
    img = bpy.data.images.new(name, TEX, TEX)
    mat = bpy.data.materials.new(name + "_mat"); mat.use_nodes = True
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = img
    mat.node_tree.nodes.active = tex
    mat.node_tree.links.new(tex.outputs["Color"], mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    D.data.materials.clear(); D.data.materials.append(mat)
    for x in bpy.context.selected_objects: x.select_set(False)
    T.select_set(True); D.select_set(True); bpy.context.view_layer.objects.active = D
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_selected_to_active=True, cage_extrusion=0.012,
                        max_ray_distance=0.03, margin=12, use_clear=True)
    return img, mat

h_offen, mat_offen = backen(timg, "haut_offen")
h_offen.filepath_raw = os.path.join(ordner, "haut_offen.png"); h_offen.file_format = "PNG"; h_offen.save()
if timg_zu is not None:
    h_zu, _ = backen(timg_zu, "haut_zu")
    h_zu.filepath_raw = os.path.join(ordner, "haut_zu.jpg"); h_zu.file_format = "JPEG"; h_zu.save()
log("gebacken")
D.data.materials.clear(); D.data.materials.append(mat_offen)
bpy.data.objects.remove(T, do_unlink=True)

# ---- Mundspalt aufschneiden ----------------------------------------------------------------------------------
bm = bmesh.new(); bm.from_mesh(D.data)
geom = list(bm.edges) + list(bm.faces)
res = bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), plane_co=(0, 0, mz), plane_no=(0, 0, 1),
                             dist=1e-5)
schnitt = [e for e in bm.edges if abs(e.verts[0].co.z - mz) < 2e-5 and abs(e.verts[1].co.z - mz) < 2e-5
           and abs(e.verts[0].co.x - mx0) < halbW * 0.9 and abs(e.verts[1].co.x - mx0) < halbW * 0.9
           and e.verts[0].co.y < my + 0.02 and e.verts[1].co.y < my + 0.02]
log("Spaltkanten", len(schnitt))
bmesh.ops.split_edges(bm, edges=schnitt)
bm.to_mesh(D.data); bm.free()

# ---- Gewichte --------------------------------------------------------------------------------------------------
dv = netz_arrays(D)
n = len(dv)
x_rel = np.abs(dv[:, 0] - mx0)
beyond = np.maximum(x_rel - halbW, 0)
z = dv[:, 2]
hinge_y = -0.146 + 0.9 * dEm
unten = z <= mz + 1e-6
w_j = np.where(unten, 1.0 - sm(beyond, 0.0, 0.55 * dEm), 0.0)
# oberhalb der Spaltebene nur direkt am Mundwinkel zuziehen
w_j = np.where(~unten, (1.0 - sm(z - mz, 0.0, 0.10 * dEm)) * (1.0 - sm(beyond, 0.0, 0.25 * dEm)) * 0.0, w_j)
w_j *= 1.0 - sm(x_rel, 0.8 * dEm, 1.25 * dEm)                         # Wangenrand
w_j *= sm(z, kinn_z - 0.30 * dEm, kinn_z + 0.05 * dEm)                # unter dem Kinn nichts
w_j *= 1.0 - sm(dv[:, 1], hinge_y - 0.35 * dEm, hinge_y)              # hinter dem Kiefergelenk nichts
# weiche Zone um den Mundwinkel (der Spalt endet dort)
neck_h = sm(z, kinn_z - 1.1 * dEm, kinn_z - 0.2 * dEm)
w_k = (1.0 - w_j) * neck_h
w_h = (1.0 - w_j) * (1.0 - neck_h)
for name, w in (("kiefer", w_j), ("kopf", w_k), ("hals", w_h)):
    g = D.vertex_groups.new(name=name)
    for i in np.where(w > 0.002)[0]:
        g.add([int(i)], float(w[i]), "REPLACE")
log("Gewichte: Kiefer-Punkte", int((w_j > 0.5).sum()))

# ---- Mundhöhle und Zähne ------------------------------------------------------------------------------------------
def material(name, farbe, rauh=0.8):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*farbe, 1); p.inputs["Roughness"].default_value = rauh
    return m
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=8, radius=1.0)
bmesh.ops.scale(bm, vec=(halbW * 0.92, halbW * 0.85, halbW * 0.9), verts=bm.verts)
bmesh.ops.translate(bm, vec=(mx0, my + halbW * 1.35, mz - 0.05 * halbW), verts=bm.verts)
hm = bpy.data.meshes.new("mundhoehle"); bm.to_mesh(hm); bm.free()
H = bpy.data.objects.new("mundhoehle", hm); sc.collection.objects.link(H)
H.data.materials.append(material("hoehle", (0.10, 0.02, 0.02), 0.9))
for p in hm.polygons: p.use_smooth = True


def zaehne(name, z0, z1, tiefe0):
    bm = bmesh.new()
    nz = 9
    for i in range(nz):
        t = (i + 0.5) / nz * 2 - 1                    # -1..1
        x = mx0 + t * halbW * 0.82
        y = my + tiefe0 + 0.55 * halbW * t * t
        bw = halbW * 0.82 * 2 / nz * 0.46
        r = bmesh.ops.create_cube(bm, size=1.0)
        vs = r["verts"]
        bmesh.ops.scale(bm, vec=(bw * 2, halbW * 0.22, z1 - z0), verts=vs)
        bmesh.ops.translate(bm, vec=(x, y, (z0 + z1) / 2), verts=vs)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    o.data.materials.append(material("zahn", (0.88, 0.85, 0.76), 0.5))
    return o
zo = zaehne("zaehne_oben", mz - 0.40 * halbW, mz + 0.02 * halbW, halbW * 1.0)
zun = zaehne("zaehne_unten", mz - 0.80 * halbW, mz - 0.40 * halbW + 0.02 * halbW, halbW * 1.0)

# ---- Skelett ----------------------------------------------------------------------------------------------------------
arm = bpy.data.armatures.new("gerippe"); A = bpy.data.objects.new("gerippe", arm); sc.collection.objects.link(A)
for x in bpy.context.selected_objects: x.select_set(False)
bpy.context.view_layer.objects.active = A
bpy.ops.object.mode_set(mode="EDIT")
yN = hinge_y
eb = arm.edit_bones
b_hals = eb.new("hals"); b_hals.head = (0, yN, kinn_z - 1.3 * dEm); b_hals.tail = (0, yN, kinn_z - 0.3 * dEm)
b_kopf = eb.new("kopf"); b_kopf.head = b_hals.tail; b_kopf.tail = (0, yN, 1.64 + 2.0 * dEm); b_kopf.parent = b_hals
b_kief = eb.new("kiefer"); b_kief.head = (0, yN, mz + 0.55 * dEm); b_kief.tail = (0, my, kinn_z + 0.1 * dEm); b_kief.parent = b_kopf
for b_ in eb: b_.roll = 0.0
bpy.ops.object.mode_set(mode="OBJECT")
for o in (D, H, zo, zun):
    o.parent = A
    md = o.modifiers.new("Skelett", "ARMATURE"); md.object = A
# Höhle und obere Zähne am Kopf-Knochen, untere Zähne am Kiefer
for o, gname in ((H, "kopf"), (zo, "kopf"), (zun, "kiefer")):
    g = o.vertex_groups.new(name=gname); g.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")

# ---- Export ------------------------------------------------------------------------------------------------------------
for img in bpy.data.images:
    if img.name in ("haut_offen", "haut_zu"):
        img.pack()
bpy.ops.object.select_all(action="DESELECT")
for o in (D, H, zo, zun, A): o.select_set(True)
bpy.context.view_layer.objects.active = A
bpy.ops.export_scene.gltf(filepath=a["aus"], export_format="GLB", use_selection=True, export_skins=True, export_animations=False,
                          export_yup=True, export_image_format="JPEG", export_jpeg_quality=90)
tris = sum(len(p.vertices) - 2 for o in (D, H, zo, zun) for p in o.data.polygons)
log("EXPORT", a["aus"], os.path.getsize(a["aus"]), "Bytes", tris, "Dreiecke")
if a.get("vorschau"):
    # Kontrollbilder: Pose des Kiefers (Grad um X) rendern, Workbench mit Textur
    import math as _m
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "FLAT"; sc.display.shading.color_type = "TEXTURE"
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x = sc.render.resolution_y = 700
    sc.world = bpy.data.worlds.new("w2"); sc.world.color = (0.85, 0.85, 0.9)
    cam = bpy.data.objects.new("c2", bpy.data.cameras.new("c2")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = 0.42
    pb = A.pose.bones
    for name, ang_k, ang_h in (("ruhe", 0, 0), ("auf", float(a.get("kw", "-9")), 0), ("nicken", 0, 12)):
        pb["kiefer"].rotation_mode = "XYZ"; pb["kopf"].rotation_mode = "XYZ"
        pb["kiefer"].rotation_euler = (_m.radians(ang_k), 0, 0)
        pb["kopf"].rotation_euler = (_m.radians(ang_h), 0, 0)
        bpy.context.view_layer.update()
        for ans, az in (("vorn", 0), ("seite", 90)):
            cam.location = (_m.sin(_m.radians(az)) * 3, -_m.cos(_m.radians(az)) * 3, 1.58)
            cam.rotation_euler = (_m.radians(90), 0, _m.radians(az))
            sc.render.filepath = os.path.join(ordner, "vorschau_%s_%s.png" % (name, ans))
            bpy.ops.render.render(write_still=True)
