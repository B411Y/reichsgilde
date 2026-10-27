# Plan C: rohe TRELLIS-Form behalten, nur beleben (Skelett Hals/Kopf/Kiefer, Mundspalt, Blinzel-Textur).
#  ~/werkzeuge/mpfb/blender_mpfb.sh -b --python plan_c.py -- --glb=<trellis.glb> --tl=x,z --tr=x,z --aus=<kopf.glb>
#       [--zu=<front_zu.png> --mitte=0,0.05 --groesse=1.0] [--tris=11000] [--tex=2048]
# --tl/--tr: Augenmitten in der Frontansicht (TRELLIS-Einheiten, tl = Bildseite rechts). Alles andere automatisch.
import sys, os, math
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, kdtree

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

# ---- scharfes Porträt auf das Gesicht projizieren (Blickwinkel-gewichtet in die TRELLIS-Textur überblenden) ------------------
def kamera_anpassen(P3, P2):
    """Orthografische Kamera: P2 = s * (Rx(phi) Rz(psi) Rz(rho)...) angepasst an Landmarken. Rückgabe Parameter und Funktion."""
    P3 = np.asarray(P3, float); P2 = np.asarray(P2, float)
    c0 = P3.mean(0)
    def rot(psi, phi, rho):
        cz, sz = math.cos(psi), math.sin(psi); cx, sx = math.cos(phi), math.sin(phi); cy, sy = math.cos(rho), math.sin(rho)
        Rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
        Rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
        Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
        return Ry @ Rx @ Rz
    def proj(par, X):
        psi, phi, rho, sc, u0, v0 = par
        Xc = (X - c0) @ rot(psi, phi, rho).T
        return np.stack([sc * Xc[:, 0] + u0, -sc * Xc[:, 2] + v0], 1)
    par = np.array([0.4, 0.0, 0.0, 1000.0, P2[:, 0].mean(), P2[:, 1].mean()])
    def fehler(par):
        return ((proj(par, P3) - P2) ** 2).sum()
    schritt = np.array([0.1, 0.05, 0.05, 50.0, 10.0, 10.0])
    best = fehler(par)
    for it in range(400):
        verbessert = False
        for i in range(6):
            for sg in (1, -1):
                q = par.copy(); q[i] += sg * schritt[i]
                f = fehler(q)
                if f < best:
                    par, best, verbessert = q, f, True
        if not verbessert:
            schritt *= 0.6
    return par, rot, proj, c0, math.sqrt(best / len(P3))


if a.get("pbild"):
    pimg = bpy.data.images.load(a["pbild"])
    pw, ph = pimg.size
    pp = np.empty(pw * ph * 4, np.float32); pimg.pixels.foreach_get(pp); pp = pp.reshape(ph, pw, 4)[:, :, :3]   # Zeile 0 = unten
    def pxy(s_):
        q = [float(t) for t in s_.split(",")]
        return q
    # Landmarken im Porträt (Pixel, Bildzeile von oben): linkes Auge (Bildseite links), rechtes, Nasenspitze, Mundmitte
    P2 = np.array([pxy(a["p_el"]), pxy(a["p_er"]), pxy(a["p_nase"]), pxy(a["p_mund"])])
    # entsprechende 3D-Punkte der TRELLIS-Form: Bildseite links = -x
    def vorderster(cx, cz, r):
        m = np.where(np.hypot(tv[:, 0] - cx, tv[:, 2] - cz) < r)[0]
        return tv[m[np.argmin(tv[m, 1])]]
    p_el3 = vorderster(trx, trz, 0.15 * dE); p_er3 = vorderster(tlx, tlz, 0.15 * dE)
    zent = np.where((np.abs(tv[:, 0] - xm) < 0.15 * dE) & (tv[:, 2] < zE - 0.2 * dE) & (tv[:, 2] > zE - 0.8 * dE) & (kl == 1))[0]
    p_nase3 = tv[zent[np.argmin(tv[zent, 1])]]
    p_mund3 = np.array([mx0, my, mz])
    P3 = np.array([p_el3, p_er3, p_nase3, p_mund3])
    par, rotf, projf, c0, rms = kamera_anpassen(P3, P2)
    log("Porträt-Kamera: Gier %.1f Grad, Nick %.1f, Roll %.1f, Maßstab %.1f, Restfehler %.1f px" % (math.degrees(par[0]), math.degrees(par[1]), math.degrees(par[2]), par[3], rms))
    R = rotf(par[0], par[1], par[2])
    # Normalen und Sichtbarkeit
    T.data.calc_normals_split() if hasattr(T.data, "calc_normals_split") else None
    nv = np.empty(len(T.data.vertices) * 3); T.data.vertices.foreach_get("normal", nv); nv = nv.reshape(-1, 3)
    kam_dir = R.T @ np.array([0.0, -1.0, 0.0])          # Richtung vom Objekt zur Kamera (Objektraum)
    cosw = np.clip(nv @ kam_dir, 0, 1)
    uv_ = projf(par, tv)
    # Gesichtsmaske: Nähe zum Gesichtszentrum (Ebene x,z) und nach vorn gerichtet
    zentrum = np.array([xm, zE - 0.7 * dE])
    dd = np.hypot((tv[:, 0] - zentrum[0]) / (1.6 * dE), (tv[:, 2] - zentrum[1]) / (1.9 * dE))
    maske = (1.0 - sm(dd, 0.75, 1.0)) * (cosw ** 1.5) * (tv[:, 1] < np.percentile(tv[:, 1], 70))
    ok = (uv_[:, 0] >= 0) & (uv_[:, 0] < pw - 1) & (uv_[:, 1] >= 0) & (uv_[:, 1] < ph - 1)
    maske *= ok
    ix = np.clip(uv_[:, 0], 0, pw - 1).astype(int); iy = np.clip(ph - 1 - uv_[:, 1], 0, ph - 1).astype(int)
    pfarbe = pp[iy, ix]
    sel = np.where(maske > 0.05)[0]
    gain = (farbe[sel].mean(0) + 1e-3) / (pfarbe[sel].mean(0) + 1e-3)
    gain = np.clip(gain, 0.7, 1.4)
    log("Projektion: Punkte", len(sel), "Farbanpassung", np.round(gain, 3))
    alpha = float(a.get("palpha", 0.92))
    H0, W0 = tpx.shape[:2]
    Wi = np.zeros((H0, W0), np.float32); Ci = np.zeros((H0, W0, 3), np.float32)
    cols = np.clip(pfarbe[sel] * gain, 0, 1)
    wv = (maske[sel]).astype(np.float32)
    for dy in range(-2, 3):
        for dx in range(-2, 3):
            yy = np.clip(pix[sel, 1] + dy, 0, H0 - 1); xx = np.clip(pix[sel, 0] + dx, 0, W0 - 1)
            np.add.at(Wi, (yy, xx), wv)
            np.add.at(Ci, (yy, xx), cols * wv[:, None])
    def box(m):
        out = m.copy()
        out[1:-1, 1:-1] = (m[:-2, :-2] + m[:-2, 1:-1] + m[:-2, 2:] + m[1:-1, :-2] + m[1:-1, 1:-1] + m[1:-1, 2:] + m[2:, :-2] + m[2:, 1:-1] + m[2:, 2:]) / 9.0
        return out
    for it in range(3):
        Wi = box(Wi)
        for c_ in range(3):
            Ci[:, :, c_] = box(Ci[:, :, c_])
    mittel = Ci / np.maximum(Wi, 1e-6)[:, :, None]
    deck = np.clip(Wi / 6.0, 0, 1)[:, :, None] * alpha
    neu = tpx.copy()
    neu[:, :, :3] = (1 - deck) * neu[:, :, :3] + deck * mittel
    tpx = neu
    nimg = bpy.data.images.new("tex_projiziert", tpx.shape[1], tpx.shape[0])
    nimg.pixels.foreach_set(tpx.ravel()); nimg.update()
    timg = nimg
    tnode.image = nimg
    # Texturquelle für das Backen merken
    farbe = farbe

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
dec.ratio = float(a.get("tris", 11000)) / (len(T.data.polygons))
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

# ---- Augen: Öffnung ausschneiden, Lid-Flicken (geschlossenes Auge) abtrennen, echte Augäpfel einsetzen ------------------------------
AUGEN = float(a.get("augenf", 1.0))
mat_zu = None
if timg_zu is not None:
    mat_zu = bpy.data.materials.new("haut_zu_mat"); mat_zu.use_nodes = True
    tz = mat_zu.node_tree.nodes.new("ShaderNodeTexImage"); tz.image = h_zu
    mat_zu.node_tree.links.new(tz.outputs["Color"], mat_zu.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
ey_n = {"l": np.array([(tlx - xm) * s, -0.146, 1.64]), "r": np.array([(trx - xm) * s, -0.146, 1.64])}
# vorderste Hautfläche an den Augen (Normiert) bestimmen
dvv = netz_arrays(D)
def fy_at(c):
    m = np.where(np.hypot(dvv[:, 0] - c[0], dvv[:, 2] - c[2]) < 0.012)[0]
    return float(dvv[m, 1].min())
ey_n = {k: np.array([v[0], fy_at(v), v[2]]) for k, v in ey_n.items()}
oeff_a, oeff_b = float(a.get("oeff_a", 0.22)) * dEm, float(a.get("oeff_b", 0.1)) * dEm
bm = bmesh.new(); bm.from_mesh(D.data); bm.faces.ensure_lookup_table()
patch_faces = {"l": [], "r": []}
for f in bm.faces:
    cen = f.calc_center_median()
    for k, c in ey_n.items():
        if ((cen.x - c[0]) / oeff_a) ** 2 + ((cen.z - c[2]) / oeff_b) ** 2 < 1.0 and cen.y < c[1] + 0.012:
            patch_faces[k].append(f.index)
log("Lidfläche links", len(patch_faces["l"]), "rechts", len(patch_faces["r"]))
# Flicken als eigenes Netz kopieren
lid_me = bpy.data.meshes.new("lider")
bm_l = bmesh.new()
uv_src = bm.loops.layers.uv.active
uv_dst = bm_l.loops.layers.uv.new("UVMap")
vmap = {}
for k in ("l", "r"):
    for fi in patch_faces[k]:
        f = bm.faces[fi]
        nv = []
        for v in f.verts:
            if v.index not in vmap:
                vmap[v.index] = bm_l.verts.new(v.co)
            nv.append(vmap[v.index])
        nf = bm_l.faces.new(nv)
        for lp_s, lp_d in zip(f.loops, nf.loops):
            lp_d[uv_dst].uv = lp_s[uv_src].uv
bm_l.to_mesh(lid_me); bm_l.free()
LID = bpy.data.objects.new("lider", lid_me); sc.collection.objects.link(LID)
LID.data.materials.append(mat_zu if mat_zu else mat_offen)
for p_ in lid_me.polygons: p_.use_smooth = True
# Löcher in den Kopf schneiden
bmesh.ops.delete(bm, geom=[bm.faces[i] for k in patch_faces for i in patch_faces[k]], context="FACES_ONLY")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
# Lidrand: Öffnungsrand nach innen ziehen (Dicke der Lider), UV vom Randpunkt übernehmen
bm.edges.ensure_lookup_table()
uvl_ = bm.loops.layers.uv.active
rand = [e for e in bm.edges if len(e.link_faces) == 1 and any(((e.verts[0].co.x - c[0]) / (oeff_a * 1.6)) ** 2 + ((e.verts[0].co.z - c[2]) / (oeff_b * 3.0)) ** 2 < 1.0 for c in ey_n.values())]
# Randpunkte auf die Ellipse der Öffnung ziehen (saubere Augenform)
for v in {v_ for e in rand for v_ in e.verts}:
    cc = min(ey_n.values(), key=lambda c: (v.co.x - c[0]) ** 2 + (v.co.z - c[2]) ** 2)
    dx_, dz_ = (v.co.x - cc[0]) / oeff_a, (v.co.z - cc[2]) / oeff_b
    r_ = math.hypot(dx_, dz_)
    if r_ > 1e-6:
        v.co.x = cc[0] + dx_ / r_ * oeff_a; v.co.z = cc[2] + dz_ / r_ * oeff_b
vuv = {}
for e in rand:
    for v in e.verts:
        for lp in v.link_loops:
            vuv[v.index] = lp[uvl_].uv.copy(); break
ext = bmesh.ops.extrude_edge_only(bm, edges=rand)
neue_v = [g for g in ext["geom"] if isinstance(g, bmesh.types.BMVert)]
neue_f = [g for g in ext["geom"] if isinstance(g, bmesh.types.BMFace)]
alt_zu_neu = {}
for e in rand:
    pass
tief = float(a.get("lidtiefe", 0.005))
for v in neue_v:
    cc = min(ey_n.values(), key=lambda c: (v.co.x - c[0]) ** 2 + (v.co.z - c[2]) ** 2)
    v.co.y += tief
    v.co.x = cc[0] + (v.co.x - cc[0]) * 0.92
    v.co.z = cc[2] + (v.co.z - cc[2]) * 0.92
bm.verts.ensure_lookup_table()
# UVs der Randfläche: nächstliegender alter Punkt
for f in neue_f:
    for lp in f.loops:
        best = None; bd = 1e9
        for oi, uvv_ in vuv.items():
            ov = bm.verts[oi] if oi < len(bm.verts) else None
            if ov is None: continue
            d_ = (ov.co.x - lp.vert.co.x) ** 2 + (ov.co.z - lp.vert.co.z) ** 2 + (ov.co.y - lp.vert.co.y + tief) ** 2 * 0.2
            if d_ < bd: bd, best = d_, uvv_
        if best is not None: lp[uvl_].uv = best
bm.to_mesh(D.data); bm.free()
# Augäpfel: MPFB-Netz (CC0) importieren, auf Öffnung abstimmen
eyeobj = os.path.expanduser("~/werkzeuge/mpfb/daten/data/eyes/low-poly/low-poly.obj")
vor = set(bpy.data.objects)
bpy.ops.wm.obj_import(filepath=eyeobj)
EY = [o for o in bpy.data.objects if o not in vor][0]
EY.data.transform(EY.matrix_world); EY.matrix_world = Matrix.Identity(4); EY.parent = None
ev = netz_arrays(EY)
mxe = np.median(ev[:, 0])
tex_name = a.get("iris", "brownlight")
eimg = bpy.data.images.load(os.path.expanduser("~/werkzeuge/mpfb/daten/data/eyes/materials/%s_eye.png" % tex_name))
eimg.scale(512, 512)
emat = bpy.data.materials.new("auge"); emat.use_nodes = True
et = emat.node_tree.nodes.new("ShaderNodeTexImage"); et.image = eimg
pb_ = emat.node_tree.nodes["Principled BSDF"]
emat.node_tree.links.new(et.outputs["Color"], pb_.inputs["Base Color"]); pb_.inputs["Roughness"].default_value = 0.08
augapfel = {}
for k, seite in (("l", ev[:, 0] > mxe), ("r", ev[:, 0] <= mxe)):
    idx = np.where(seite)[0]
    c_old = ev[idx].mean(0)
    # Vorderseite (Iris) über die UV-Mitte bestimmen und nach -Y drehen
    uvl = EY.data.uv_layers.active
    uvv = np.zeros((len(ev), 2)); lvi = np.empty(len(EY.data.loops), np.int64); EY.data.loops.foreach_get("vertex_index", lvi)
    uvd = np.empty(len(EY.data.loops) * 2); uvl.data.foreach_get("uv", uvd); uvv[lvi] = uvd.reshape(-1, 2)
    nahe = idx[np.linalg.norm(uvv[idx] - 0.5, axis=1) < 0.07]
    if len(nahe):
        fvec = (ev[nahe] - c_old).mean(0); fvec /= np.linalg.norm(fvec)
        rot_q = Vector(fvec).rotation_difference(Vector((0, -1, 0)))
        Rm = np.array(rot_q.to_matrix())
        ev[idx] = (ev[idx] - c_old) @ Rm.T + c_old
        log("Auge: Vorderseite war", np.round(fvec, 2))
    # Vorderseite des MPFB-Auges zeigt nach -Y; Maße
    r_old = np.abs(ev[idx] - c_old).max(0)
    c = ey_n[k]
    Rx = oeff_a * float(a.get("augenfaktor", 1.02))
    sx, sy, sz = float(a.get('augx_f', 1.05)) * Rx / r_old[0], float(a.get('augtiefe_f', 0.6)) * Rx / r_old[1], float(a.get('augz_f', 0.72)) * Rx / r_old[2]
    new = (ev[idx] - c_old) * np.array([sx, sy, sz]) + np.array([c[0], c[1] + float(a.get("augentiefe", 0.008)), c[2]])
    augapfel[k] = (idx, new)
allv = ev.copy()
for k, (idx, new) in augapfel.items():
    allv[idx] = new
EY.data.vertices.foreach_set("co", allv.ravel()); EY.data.update()
sub_ = EY.modifiers.new('s', 'SUBSURF'); sub_.levels = 1; sub_.render_levels = 1
for x_ in bpy.context.selected_objects: x_.select_set(False)
EY.select_set(True); bpy.context.view_layer.objects.active = EY
bpy.ops.object.modifier_apply(modifier='s')
EY.name = "augen"; EY.data.materials.clear(); EY.data.materials.append(emat)
for p_ in EY.data.polygons: p_.use_smooth = True
log("Augäpfel eingesetzt, Öffnung a=%.4f b=%.4f" % (oeff_a, oeff_b))

# ---- Mundspalt aufschneiden ----------------------------------------------------------------------------------
bm = bmesh.new(); bm.from_mesh(D.data)
geom = list(bm.edges) + list(bm.faces)
res = bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), plane_co=(0, 0, mz), plane_no=(0, 0, 1),
                             dist=1e-5)
schnitt = [e for e in bm.edges if abs(e.verts[0].co.z - mz) < 2e-5 and abs(e.verts[1].co.z - mz) < 2e-5
           and abs(e.verts[0].co.x - mx0) < halbW * float(a.get("spaltb", 1.15)) and abs(e.verts[1].co.x - mx0) < halbW * float(a.get("spaltb", 1.15))
           and e.verts[0].co.y < my + 0.02 and e.verts[1].co.y < my + 0.02]
alle_p = [e for e in bm.edges if abs(e.verts[0].co.z - mz) < 2e-5 and abs(e.verts[1].co.z - mz) < 2e-5]
log("Spaltkanten", len(schnitt), "Kanten in der Ebene gesamt", len(alle_p), "x-Bereich", [round(min(e.verts[0].co.x for e in alle_p),4), round(max(e.verts[0].co.x for e in alle_p),4)], "Mund", round(mx0,4), round(halbW,4), "y", sorted(set(round(e.verts[0].co.y,3) for e in alle_p))[:6], my)
bmesh.ops.split_edges(bm, edges=schnitt)
bm.to_mesh(D.data); bm.free()

# ---- Gewichte --------------------------------------------------------------------------------------------------
dv = netz_arrays(D)
n = len(dv)
x_rel = np.abs(dv[:, 0] - mx0)
beyond = np.maximum(x_rel - halbW, 0)
z = dv[:, 2]
hinge_y = -0.146 + 0.9 * dEm
# Punkte auf der Spaltebene gehören je nach angrenzenden Flächen zur Ober- oder Unterlippe (Spalt ist aufgeschnitten)
D.data.calc_loop_triangles()
cz = np.array([p_.center.z for p_ in D.data.polygons])
n_oben = np.zeros(n); n_unten = np.zeros(n)
for p_, c_ in zip(D.data.polygons, cz):
    for vi_ in p_.vertices:
        if c_ > mz: n_oben[vi_] += 1
        else: n_unten[vi_] += 1
auf_ebene = np.abs(z - mz) < 3e-5
unten = np.where(auf_ebene, (n_unten > 0) & (n_oben == 0), z < mz)
w_j = np.where(unten, 1.0 - sm(beyond, 0.0, float(a.get("eckfade", 0.26)) * dEm), 0.0)
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

# ---- ARKit-Lippenformen: vom MPFB-Kopf (Träger) per nächstem Punkt auf das TRELLIS-Netz übertragen ----------------------------------
FORMEN = ["mouthFunnel", "mouthPucker", "mouthSmileLeft", "mouthSmileRight", "mouthStretchLeft", "mouthStretchRight",
          "mouthLowerDownLeft", "mouthLowerDownRight", "mouthUpperUpLeft", "mouthUpperUpRight", "mouthRollLower", "mouthRollUpper",
          "mouthPressLeft", "mouthPressRight", "mouthFrownLeft", "mouthFrownRight", "mouthClose", "browInnerUp", "browDownLeft",
          "browDownRight", "cheekPuff", "noseSneerLeft", "noseSneerRight"]
if not a.get("ohneformen"):
    import json as _json
    import mensch as M
    dna_t = _json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "proto_dna.json")))
    vor_h = set(bpy.data.objects)
    bt = M.mensch_bauen(dna_t, unterteilung=0, mit_haareditor=False)
    M.TargetService.bake_targets(bt)
    for n_ in FORMEN:
        M.TargetService.load_target(bt, M._ziel_pfad("faceunits", n_), weight=0.0, name=n_)
    kb = bt.data.shape_keys.key_blocks
    nb_ = len(bt.data.vertices)
    base_ = np.empty(nb_ * 3); kb["Basis"].data.foreach_get("co", base_); base_ = base_.reshape(-1, 3)
    deltas_t = {}
    for n_ in FORMEN:
        c_ = np.empty(nb_ * 3); kb[n_].data.foreach_get("co", c_); deltas_t[n_] = c_.reshape(-1, 3) - base_
    sel_t = np.where((base_[:, 2] > 1.50) & (base_[:, 1] < 0.02) & (np.abs(base_[:, 0]) < 0.2))[0]
    for o_ in [x_ for x_ in bpy.data.objects if x_ not in vor_h]:
        bpy.data.objects.remove(o_, do_unlink=True)
    # Landmarken Träger -> Netz (Augen, Nase, Mund)
    dvn = netz_arrays(D)
    zn = np.where((np.abs(dvn[:, 0]) < 0.004) & (dvn[:, 2] > 1.4))[0]
    nase_c = dvn[zn[np.argmin(dvn[zn, 1])]]
    src_t = np.array([[0.0331, -0.1454, 1.6374], [-0.0331, -0.1454, 1.6374], [0.0, -0.182, 1.596], [0.0, -0.162, 1.572]])
    dst_t = np.array([ey_n["l"], ey_n["r"], nase_c, [mx0, my, mz]])
    st_, Rt_, tt_ = umeyama_c(src_t, dst_t) if False else (None, None, None)
    # Ähnlichkeit (Umeyama) selbst gerechnet
    ms, md = src_t.mean(0), dst_t.mean(0)
    cov = (dst_t - md).T @ (src_t - ms) / 4
    U, Dd, Vt = np.linalg.svd(cov)
    Sg = np.eye(3)
    if np.linalg.det(U) * np.linalg.det(Vt) < 0: Sg[2, 2] = -1
    Rt_ = U @ Sg @ Vt
    st_ = np.trace(np.diag(Dd) @ Sg) / (((src_t - ms) ** 2).sum() / 4)
    tt_ = md - st_ * Rt_ @ ms
    pos_t = (st_ * (Rt_ @ base_[sel_t].T)).T + tt_
    kdT = kdtree.KDTree(len(sel_t))
    for i_, p_ in enumerate(pos_t): kdT.insert(Vector(p_), i_)
    kdT.balance()
    fz_ = np.where(dvn[:, 2] > 1.45)[0]
    idxs = np.zeros((len(dvn), 3), np.int64); gws = np.zeros((len(dvn), 3))
    for i_ in fz_:
        res = kdT.find_n(Vector(dvn[i_]), 3)
        w_ = np.array([1.0 / (r[2] + 2e-3) for r in res])
        fade_ = 1.0 - sm(res[0][2], 0.012, 0.03)
        gws[i_] = w_ / w_.sum() * fade_
        idxs[i_] = [r[1] for r in res]
    # Formen als Shape Keys anlegen
    if D.data.shape_keys is None:
        D.shape_key_add(name="Basis", from_mix=False)
    for n_ in FORMEN:
        dl_ = deltas_t[n_][sel_t]
        dd_ = (dl_[idxs] * gws[:, :, None]).sum(1)
        dd_ = (st_ * (Rt_ @ dd_.T)).T
        # Wirkung auf Gesicht und Bartbereich begrenzen (nicht auf Haar oberhalb der Brauen / Hals)
        k_ = D.shape_key_add(name=n_, from_mix=False)
        k_.data.foreach_set("co", (dvn + dd_).ravel())
    log("ARKit-Formen übertragen:", len(FORMEN), "Skala", round(float(st_), 3))

# ---- Mundhöhle und Zähne ------------------------------------------------------------------------------------------
def material(name, farbe, rauh=0.8):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*farbe, 1); p.inputs["Roughness"].default_value = rauh
    return m
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=8, radius=1.0)
bmesh.ops.scale(bm, vec=(halbW * 0.92, halbW * 0.8, halbW * 0.9), verts=bm.verts)
bmesh.ops.translate(bm, vec=(mx0, my + halbW * 1.45, mz - 0.3 * halbW), verts=bm.verts)
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
# MPFB-Zähne und Zunge (CC0) statt Kästchen: importieren, auf Mundbreite skalieren, hinter die Lippen setzen
def mpfb_obj(pfad, ziel_tris, name):
    vor_ = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=os.path.expanduser(pfad))
    o = [x for x in bpy.data.objects if x not in vor_][0]
    o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4); o.parent = None
    o.name = name
    if ziel_tris:
        n_ = sum(len(p.vertices) - 2 for p in o.data.polygons)
        md_ = o.modifiers.new("d", "DECIMATE"); md_.ratio = min(1.0, ziel_tris / n_)
        for x_ in bpy.context.selected_objects: x_.select_set(False)
        o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier="d")
    return o
ZAHN = mpfb_obj("~/werkzeuge/mpfb/daten/data/teeth/teeth_base/teeth_base.obj", 900, "zaehne")
zv = netz_arrays(ZAHN)
zmin, zmax = zv.min(0), zv.max(0)
s_t = (2 * halbW * 0.95) / (zmax[0] - zmin[0])
zc = np.array([(zmin[0] + zmax[0]) / 2, zmin[1], (zmin[2] + zmax[2]) / 2])
okk = mz - float(a.get('zahnhoch', 0.40)) * halbW
zneu = (zv - zc) * s_t + np.array([mx0, my + float(a.get("zahntiefe", 0.006)), okk])
ZAHN.data.vertices.foreach_set("co", zneu.ravel()); ZAHN.data.update()
zimg = bpy.data.images.load(os.path.expanduser("~/werkzeuge/mpfb/daten/data/teeth/teeth_base/teeth.png")); zimg.scale(256, 256)
zm = bpy.data.materials.new("zahn"); zm.use_nodes = True
zt = zm.node_tree.nodes.new("ShaderNodeTexImage"); zt.image = zimg
zm.node_tree.links.new(zt.outputs["Color"], zm.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
ZAHN.data.materials.clear(); ZAHN.data.materials.append(zm)
ZUNGE = mpfb_obj("~/werkzeuge/mpfb/daten/data/tongue/tongue01/tongue01.obj", 350, "zunge")
tv_ = netz_arrays(ZUNGE)
tmin, tmax = tv_.min(0), tv_.max(0)
tc = np.array([(tmin[0] + tmax[0]) / 2, tmin[1], tmax[2]])
tneu = (tv_ - tc) * s_t * float(a.get('zungef', 0.38)) * np.array([1.0, 1.0, float(a.get('zungez', 0.35))]) + np.array([mx0, my + float(a.get("zahntiefe", 0.006)) + 0.012, mz - float(a.get('zahnhoch', 0.40)) * halbW - 0.05 * halbW])
ZUNGE.data.vertices.foreach_set("co", tneu.ravel()); ZUNGE.data.update()
timg_t = bpy.data.images.load(os.path.expanduser("~/werkzeuge/mpfb/daten/data/tongue/tongue01/tongue01_diffuse.png")); timg_t.scale(256, 256)
tm_ = bpy.data.materials.new("zunge"); tm_.use_nodes = True
tt_ = tm_.node_tree.nodes.new("ShaderNodeTexImage"); tt_.image = timg_t
tm_.node_tree.links.new(tt_.outputs["Color"], tm_.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
ZUNGE.data.materials.clear(); ZUNGE.data.materials.append(tm_)
for o_ in (ZAHN, ZUNGE):
    for p_ in o_.data.polygons: p_.use_smooth = True
log("Zähne", len(ZAHN.data.polygons), "Zunge", len(ZUNGE.data.polygons), "Maßstab", s_t)
zo = ZAHN; zun = ZUNGE   # (Namen für Weiterverwendung)

# ---- Skelett ----------------------------------------------------------------------------------------------------------
arm = bpy.data.armatures.new("gerippe"); A = bpy.data.objects.new("gerippe", arm); sc.collection.objects.link(A)
for x in bpy.context.selected_objects: x.select_set(False)
bpy.context.view_layer.objects.active = A
bpy.ops.object.mode_set(mode="EDIT")
yN = hinge_y
eb = arm.edit_bones
b_hals = eb.new("hals"); b_hals.head = (0, yN, kinn_z - 1.3 * dEm); b_hals.tail = (0, yN, kinn_z - 0.3 * dEm)
b_kopf = eb.new("kopf"); b_kopf.head = b_hals.tail; b_kopf.tail = (0, yN, 1.64 + 2.0 * dEm); b_kopf.parent = b_hals
b_al = eb.new("auge_l"); b_al.head = tuple(ey_n["l"] + np.array([0, 0.012, 0])); b_al.tail = tuple(ey_n["l"] + np.array([0, -0.004, 0])); b_al.parent = b_kopf
b_ar = eb.new("auge_r"); b_ar.head = tuple(ey_n["r"] + np.array([0, 0.012, 0])); b_ar.tail = tuple(ey_n["r"] + np.array([0, -0.004, 0])); b_ar.parent = b_kopf
b_kief = eb.new("kiefer"); b_kief.head = (0, yN, mz + 0.55 * dEm); b_kief.tail = (0, my, kinn_z + 0.1 * dEm); b_kief.parent = b_kopf
for b_ in eb: b_.roll = 0.0
bpy.ops.object.mode_set(mode="OBJECT")
for o in (D, H, zo, zun, LID, EY):
    o.parent = A
    md = o.modifiers.new("Skelett", "ARMATURE"); md.object = A
# Höhle und obere Zähne am Kopf-Knochen, untere Zähne am Kiefer
for o, gname in ((H, "kopf"), (zun, "kiefer"), (LID, "kopf")):
    g = o.vertex_groups.new(name=gname); g.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
zvv = netz_arrays(zo)
g_o = zo.vertex_groups.new(name="kopf"); g_u = zo.vertex_groups.new(name="kiefer")
oben_ = np.where(zvv[:, 2] >= okk)[0]; unten_ = np.where(zvv[:, 2] < okk)[0]
g_o.add([int(i) for i in oben_], 1.0, "REPLACE"); g_u.add([int(i) for i in unten_], 1.0, "REPLACE")

gl = EY.vertex_groups.new(name="auge_l"); gr_ = EY.vertex_groups.new(name="auge_r")
evv = netz_arrays(EY)
gl.add([int(i) for i in np.where(evv[:, 0] > 0)[0]], 1.0, "REPLACE")
gr_.add([int(i) for i in np.where(evv[:, 0] <= 0)[0]], 1.0, "REPLACE")
# ---- Export ------------------------------------------------------------------------------------------------------------
for img in bpy.data.images:
    if img.name in ("haut_offen", "haut_zu"):
        img.pack()
bpy.ops.object.select_all(action="DESELECT")
for o in (D, H, zo, zun, LID, EY, A): o.select_set(True)
bpy.context.view_layer.objects.active = A
bpy.ops.export_scene.gltf(filepath=a["aus"], export_format="GLB", use_selection=True, export_skins=True, export_animations=False,
                          export_yup=True, export_image_format="JPEG", export_jpeg_quality=90)
tris = sum(len(p.vertices) - 2 for o in (D, H, zo, zun, LID, EY) for p in o.data.polygons)
log("EXPORT", a["aus"], os.path.getsize(a["aus"]), "Bytes", tris, "Dreiecke")
if a.get("vorschau"):
    # Kontrollbilder: Pose des Kiefers (Grad um X) rendern, Workbench mit Textur
    import math as _m
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "FLAT"; sc.display.shading.color_type = "TEXTURE"
    if a.get("wire"): sc.display.shading.type = "WIREFRAME"
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x = sc.render.resolution_y = 700
    sc.world = bpy.data.worlds.new("w2"); sc.world.color = (0.85, 0.85, 0.9)
    cam = bpy.data.objects.new("c2", bpy.data.cameras.new("c2")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = float(a.get("vos", 0.42))
    pb = A.pose.bones
    for name, ang_k, ang_h in (("ruhe", 0, 0), ("auf", float(a.get("kw", "-9")), 0), ("nicken", 0, 12)):
        pb["kiefer"].rotation_mode = "XYZ"; pb["kopf"].rotation_mode = "XYZ"
        pb["kiefer"].rotation_euler = (_m.radians(ang_k), 0, 0)
        pb["kopf"].rotation_euler = (_m.radians(ang_h), 0, 0)
        bpy.context.view_layer.update()
        for ans, az in (("vorn", 0), ("seite", 90)):
            cam.location = (_m.sin(_m.radians(az)) * 3, -_m.cos(_m.radians(az)) * 3, float(a.get("vz", 1.58)))
            cam.rotation_euler = (_m.radians(90), 0, _m.radians(az))
            sc.render.filepath = os.path.join(ordner, "vorschau_%s_%s.png" % (name, ans))
            bpy.ops.render.render(write_still=True)
