# Plan D: rohe TRELLIS-Form + Porträt direkt als zweite UV (scharf), eigene Mund- und Gesichtsformen, Mundinnenraum aus der Lippenlinie.
#  ~/werkzeuge/mpfb/blender_mpfb.sh -b --python plan_d.py -- --glb=<trellis.glb> --pbild=<real.png> --lm=<landmarken.json>
#       --zu=<front_zu.png> --aus=<ordner>/<name>_d.glb [--tris=12000] [--tex=2048] [--vorschau=1]
# Landmarken (Pixel) im Porträt (P) und in der TRELLIS-Frontansicht (F, render_front.py, Mitte 0,0.05, Größe 1.0) aus landmarken/<name>.json.
# Ausgabe neben der .glb: <name>_atlas.jpg (gebackene Haut für Seiten/Hinterkopf), <name>_portraet.jpg, <name>_portraet_zu.jpg (Augen zu),
# <name>_augen.png (Augenmaske im Porträtraum: R Öffnung, G Blick-Gewicht, B/A Lage im Auge), <name>.json (Porträtachsen für den Shader).
import sys, os, math, json
import numpy as np
import bpy, bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

a = dict(x[2:].split("=", 1) if "=" in x else (x[2:], "1") for x in sys.argv[sys.argv.index("--") + 1:] if x.startswith("--"))
ordner = os.path.dirname(a["aus"])
NAME = os.path.splitext(os.path.basename(a["aus"]))[0]
TEX = int(a.get("tex", 2048))
MM = 0.001


def log(*x):
    print("PLAND", *x, flush=True)


def sm(x, a0, a1):
    t = np.clip((np.asarray(x, float) - a0) / (a1 - a0), 0, 1)
    return t * t * (3 - 2 * t)


def netz_arrays(o):
    me = o.data
    v = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", v); v = v.reshape(-1, 3)
    mw = np.array(o.matrix_world)
    return v @ mw[:3, :3].T + mw[:3, 3]


def bild_laden(pfad):
    img = bpy.data.images.load(pfad)
    w, h = img.size
    p = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(p)
    return img, p.reshape(h, w, 4)[::-1, :, :3].copy()          # Zeile 0 = oben


def bild_speichern(arr, pfad, fmt="JPEG", alpha=None):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(os.path.basename(pfad), w, h, alpha=alpha is not None)
    rgba = np.ones((h, w, 4), np.float32); rgba[:, :, :3] = arr
    if alpha is not None:
        rgba[:, :, 3] = alpha
    img.pixels.foreach_set(rgba[::-1].ravel())
    img.filepath_raw = pfad; img.file_format = fmt
    if fmt == "JPEG":
        bpy.context.scene.render.image_settings.quality = 92
    img.save()
    return img


def bilinear(img, x, y):
    h, w = img.shape[:2]
    x = np.clip(x, 0, w - 1.001); y = np.clip(y, 0, h - 1.001)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int); fx = (x - x0)[..., None]; fy = (y - y0)[..., None]
    return (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x0 + 1] * fx * (1 - fy) + img[y0 + 1, x0] * (1 - fx) * fy + img[y0 + 1, x0 + 1] * fx * fy)


# ---- dünne Platte (TPS) für 2D-Restverzerrungen --------------------------------------------------------------
def tps_anpassen(src, val, lam=1e-3, sk=100.0):
    s = np.asarray(src, float) / sk
    n = len(s)
    r = np.linalg.norm(s[:, None] - s[None], axis=2)
    K = np.where(r > 0, r * r * np.log(r + 1e-12), 0.0) + lam * np.eye(n)
    P = np.hstack([np.ones((n, 1)), s])
    A = np.zeros((n + 3, n + 3)); A[:n, :n] = K; A[:n, n:] = P; A[n:, :n] = P.T
    b = np.zeros((n + 3, val.shape[1])); b[:n] = val
    return s, np.linalg.solve(A, b), sk


def tps_werten(par, q):
    s, c, sk = par
    q = np.asarray(q, float) / sk
    r = np.linalg.norm(q[:, None] - s[None], axis=2)
    U = np.where(r > 0, r * r * np.log(r + 1e-12), 0.0)
    return U @ c[:len(s)] + np.hstack([np.ones((len(q), 1)), q]) @ c[len(s):]


# ---- TRELLIS laden ---------------------------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a["glb"])
T = [o for o in bpy.data.objects if o.type == "MESH"][0]
T.data.transform(T.matrix_world); T.matrix_world = Matrix.Identity(4)
nt = T.data.materials[0].node_tree
tnode = [n for n in nt.nodes if n.type == "TEX_IMAGE"][0]
for n in nt.nodes:
    if n.type == "BSDF_PRINCIPLED" and n.inputs["Base Color"].links and n.inputs["Base Color"].links[0].from_node.type == "TEX_IMAGE":
        tnode = n.inputs["Base Color"].links[0].from_node
timg = tnode.image
tv = netz_arrays(T)
log("TRELLIS", len(tv), "Punkte", len(T.data.polygons), "Flächen")
dg = bpy.context.evaluated_depsgraph_get()
bvh_t = BVHTree.FromObject(T, dg)

LM = json.load(open(a["lm"]))


def f_zu_3d(px, py):
    """F-Pixel -> vorderster Punkt der TRELLIS-Form (TRELLIS-Raum)."""
    x = px / 1024 - 0.5; z = 0.55 - py / 1024
    r = bvh_t.ray_cast(Vector((x, -2, z)), Vector((0, 1, 0)))
    return np.array(r[0]) if r[0] is not None else np.array([x, -0.3, z])


AUGP = ["aussen", "innen", "oben", "unten"]
namen, P2, F2 = [], [], []
for au in ("auge_l", "auge_r"):
    for k in AUGP:
        namen.append(au + "." + k); P2.append(LM["P"][au][k]); F2.append(LM["F"][au][k])
for k in ("ecke_l", "mitte", "ecke_r"):
    namen.append("mund." + k); P2.append(LM["P"]["mund"][k]); F2.append(LM["F"]["mund"][k])
# weitere Landmarken (frei benannt, in P und F vorhanden), z. B. Nasenflügel, Kinn, Brauen: zusätzliche Passpunkte
for k in LM["P"]:
    if k in ("auge_l", "auge_r", "mund", "nase") or k.startswith("_") or k not in LM["F"]:
        continue
    namen.append(k); P2.append(LM["P"][k]); F2.append(LM["F"][k])
P2 = np.array(P2, float); F2 = np.array(F2, float)
X3 = np.array([f_zu_3d(*f) for f in F2])
# Augenmitten (TRELLIS-Raum) = Mitte der Augenwinkel
ey_t = {au: (X3[namen.index(au + ".aussen")] + X3[namen.index(au + ".innen")]) / 2 for au in ("auge_l", "auge_r")}
dE = float(np.linalg.norm(ey_t["auge_l"][[0, 2]] - ey_t["auge_r"][[0, 2]]))
xm = float((ey_t["auge_l"][0] + ey_t["auge_r"][0]) / 2); zE = float((ey_t["auge_l"][2] + ey_t["auge_r"][2]) / 2)
# Nasenspitze automatisch: vorderster Punkt im Mittelstreifen zwischen Augen und Mund
zM3 = X3[namen.index("mund.mitte")][2]
m_ = np.where((np.abs(tv[:, 0] - xm) < 0.25 * dE) & (tv[:, 2] < zE - 0.15 * dE) & (tv[:, 2] > zM3 + 0.1 * dE))[0]
nase3 = f_zu_3d(*LM["F"]["nase_f"]) if "nase_f" in LM["F"] else tv[m_[np.argmin(tv[m_, 1])]]
namen.append("nase"); P2 = np.vstack([P2, LM["P"]["nase"]]); X3 = np.vstack([X3, nase3])
log("Augenabstand %.4f, Nase F-Pixel (%.0f, %.0f)" % (dE, (nase3[0] + 0.5) * 1024, (0.55 - nase3[2]) * 1024))


# ---- Porträt-Kamera (orthografisch) an alle Landmarken anpassen, Rest per TPS -----------------------------------
def rot(psi, phi, rho):
    cz, sz = math.cos(psi), math.sin(psi); cx, sx = math.cos(phi), math.sin(phi); cy, sy = math.cos(rho), math.sin(rho)
    Rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    Rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    return Ry @ Rx @ Rz


c0 = X3.mean(0)


def proj(par, X):
    psi, phi, rho, sc, u0, v0 = par
    Xc = (np.asarray(X) - c0) @ rot(psi, phi, rho).T
    return np.stack([sc * Xc[:, 0] + u0, -sc * Xc[:, 2] + v0], 1)


best = None
for psi0 in (-0.5, -0.25, 0.0, 0.25, 0.5):
    par = np.array([psi0, 0.0, 0.0, 1000.0, P2[:, 0].mean(), P2[:, 1].mean()])
    f0 = ((proj(par, X3) - P2) ** 2).sum()
    schritt = np.array([0.1, 0.05, 0.05, 50.0, 10.0, 10.0])
    for it in range(600):
        verb = False
        for i in range(6):
            for sg in (1, -1):
                q = par.copy(); q[i] += sg * schritt[i]
                f = ((proj(q, X3) - P2) ** 2).sum()
                if f < f0:
                    par, f0, verb = q, f, True
        if not verb:
            schritt *= 0.6
    if best is None or f0 < best[1]:
        best = (par, f0)
par = best[0]
rest = P2 - proj(par, X3)
log("Porträt-Kamera: Gier %.1f, Nick %.1f, Roll %.1f Grad, Maßstab %.0f, Restfehler %.1f px (max %.1f bei %s)" % (
    math.degrees(par[0]), math.degrees(par[1]), math.degrees(par[2]), par[3], math.sqrt(best[1] / len(P2)),
    np.abs(rest).max(), namen[int(np.argmax(np.abs(rest).max(1)))]))
# TPS auf den Resten, mit Ankern (Rest 0) weit außerhalb, damit die Korrektur örtlich bleibt
pp_ = proj(par, X3)
anker = np.array([[pp_[:, 0].mean() + dx, pp_[:, 1].mean() + dy] for dx in (-420, 0, 420) for dy in (-450, 0, 450) if dx or dy])
tps_p = tps_anpassen(np.vstack([pp_, anker]), np.vstack([rest, np.zeros((len(anker), 2))]), lam=float(a.get("tpslam", 0.02)))


def p_von_tr(X):
    q = proj(par, X)
    return q + tps_werten(tps_p, q)


R_cam = rot(par[0], par[1], par[2])
kam_dir_t = R_cam.T @ np.array([0.0, -1.0, 0.0])          # zur Kamera (TRELLIS-Raum)

# ---- Porträt und Farbangleich ------------------------------------------------------------------------------------
pimg, PB = bild_laden(a["pbild"])
PL = PB                 # Linienbild (Landmarken, Lippenlinie)
if a.get("pfarbe"):     # schattenlose Fassung für die Projektion (Schatten macht das Licht im Spiel)
    _pf, PB = bild_laden(a["pfarbe"])
PH, PW = PB.shape[:2]
# TRELLIS-Textur an das Porträt angleichen (Hautton): mittlere Farbe der sichtbaren Gesichtspunkte
tpx = np.empty(timg.size[0] * timg.size[1] * 4, np.float32); timg.pixels.foreach_get(tpx); tpx = tpx.reshape(timg.size[1], timg.size[0], 4)
uvl = T.data.uv_layers.active
uv_ = np.empty(len(T.data.loops) * 2); uvl.data.foreach_get("uv", uv_); uv_ = uv_.reshape(-1, 2)
vi_ = np.empty(len(T.data.loops), np.int64); T.data.loops.foreach_get("vertex_index", vi_)
tcol = np.zeros((len(tv), 3), np.float32)
tcol[vi_] = tpx[np.clip((uv_[:, 1] % 1) * (timg.size[1] - 1), 0, None).astype(int), np.clip((uv_[:, 0] % 1) * (timg.size[0] - 1), 0, None).astype(int), :3]
nvt = np.empty(len(tv) * 3); T.data.vertices.foreach_get("normal", nvt); nvt = nvt.reshape(-1, 3)
pv = p_von_tr(tv)
gesicht_t = (np.hypot((tv[:, 0] - xm) / (1.0 * dE), (tv[:, 2] - (zE - 0.6 * dE)) / (1.2 * dE)) < 1.0) & (nvt @ kam_dir_t > 0.6)
gesicht_t &= (pv[:, 0] > 0) & (pv[:, 0] < PW - 1) & (pv[:, 1] > 0) & (pv[:, 1] < PH - 1)
pcol = bilinear(PB, pv[gesicht_t, 0], pv[gesicht_t, 1])
haut_ = (pcol @ np.array([0.3, 0.55, 0.15]) > 0.45) & (tcol[gesicht_t] @ np.array([0.3, 0.55, 0.15]) > 0.3)
gain = np.clip(np.median(pcol[haut_], 0) / np.maximum(np.median(tcol[gesicht_t][haut_], 0), 1e-3), 0.75, 1.4)
log("Farbangleich TRELLIS -> Porträt", np.round(gain, 3), "aus", int(gesicht_t.sum()), "Punkten")
# nur hautfarbene Texel angleichen (Haar, Bart, Kleidung bleiben)
_c = tpx[:, :, :3]
_lum = _c @ np.array([0.3, 0.55, 0.15])
_haut_w = sm(_lum, 0.25, 0.4) * sm(_c[:, :, 0] - _c[:, :, 2], 0.05, 0.15)
tpx[:, :, :3] = np.clip(_c * (1 + (gain - 1) * _haut_w[:, :, None]), 0, 1)
timg_k = bpy.data.images.new("trellis_korr", timg.size[0], timg.size[1])
timg_k.pixels.foreach_set(tpx.ravel()); timg_k.update()

# ---- Augenmaske und "Augen zu" im Porträtraum ------------------------------------------------------------------
yy, xx = np.mgrid[0:PH, 0:PW].astype(np.float32)


def linse(lm, qx, qy):
    """Vorzeichenabstand (px, innen positiv) zur Augenöffnung aus vier Punkten; dazu Lage t (0 außen .. 1 innen)."""
    A_ = np.array(lm["aussen"], float); B_ = np.array(lm["innen"], float)
    ax = B_ - A_; L = np.linalg.norm(ax); ex = ax / L; ey = np.array([ex[1], -ex[0]])     # ey zeigt nach oben (Bild-y nach unten)
    if ey[1] > 0: ey = -ey
    def tn(p):
        d = np.asarray(p, float) - A_
        return d @ ex / L, d @ ey
    t_o, h_o = tn(lm["oben"]); t_u, h_u = tn(lm["unten"])
    dx = qx - A_[0]; dy = qy - A_[1]
    t = (dx * ex[0] + dy * ex[1]) / L; h = dx * ey[0] + dy * ey[1]
    def bogen(tp, hp):
        w = np.where(t < tp, 1 - ((tp - t) / tp) ** 2, 1 - ((t - tp) / (1 - tp)) ** 2)
        return hp * np.clip(w, -1, 1)
    hob = bogen(t_o, h_o); hun = bogen(t_u, h_u)       # h_u negativ
    d = np.minimum(hob - h, h - hun)
    d = np.where((t < 0) | (t > 1), np.minimum(d, -np.abs(np.where(t < 0, t, t - 1)) * L), d)
    return d, t


AUGEN = {}
R_ = np.zeros((PH, PW), np.float32); G_ = np.zeros_like(R_); Bx = np.full_like(R_, 0.5); By = np.full_like(R_, 0.5)
for au in ("auge_l", "auge_r"):
    lm = LM["P"][au]
    d, t = linse(lm, xx, yy)
    R_ = np.maximum(R_, sm(d, -0.8, 1.2))
    G_ = np.maximum(G_, sm(d, 1.0, 5.0))
    cx_ = (lm["oben"][0] + lm["unten"][0]) / 2; cy_ = (lm["oben"][1] + lm["unten"][1]) / 2
    rr = abs(lm["innen"][0] - lm["aussen"][0]) * 0.42
    inn = d > -3
    Bx = np.where(inn, np.clip((xx - cx_) / rr, -1, 1) * 0.5 + 0.5, Bx)
    By = np.where(inn, np.clip((yy - cy_) / rr, -1, 1) * 0.5 + 0.5, By)
    AUGEN[au] = d
maske_rgba = np.stack([R_, G_, Bx, By], 2)

# geschlossene Augen: KI-Frontbild (F-Raum) je Auge per TPS in den Porträtraum holen, Farbe am Ring um das Auge angleichen
PZ = PB.copy()
if a.get("pzu"):
    # geschlossene Augen liegen schon im Porträtraum (Bild-KI auf dem Porträt selbst)
    _zi, ZB = bild_laden(a["pzu"])
    ident = True
else:
    ident = False
if a.get("zu") or a.get("pzu"):
    if not ident:
        _zi, ZB = bild_laden(a["zu"])
    for au in ("auge_l", "auge_r"):
        ps = np.array([LM["P"][au][k] for k in AUGP], float); fs = np.array([LM["F"][au][k] for k in AUGP], float)
        # affin (kleinste Quadrate) + TPS-Rest
        Am = np.hstack([ps, np.ones((4, 1))])
        aff, *_ = np.linalg.lstsq(Am, fs, rcond=None)
        rest_a = fs - Am @ aff
        tps_a = tps_anpassen(ps, rest_a, lam=0.05)
        d = AUGEN[au]
        ys_, xs_ = np.where(d > -40)
        y0, y1, x0, x1 = ys_.min(), ys_.max() + 1, xs_.min(), xs_.max() + 1
        gy, gx = np.mgrid[y0:y1, x0:x1]
        q = np.stack([gx.ravel(), gy.ravel()], 1).astype(float)
        fq = q if ident else np.hstack([q, np.ones((len(q), 1))]) @ aff + tps_werten(tps_a, q)
        zf = bilinear(ZB, fq[:, 0], fq[:, 1]).reshape(y1 - y0, x1 - x0, 3)
        dq = d[y0:y1, x0:x1]

        def weich(img, r=5, n_=3):
            out = img.copy()
            for _ in range(n_):
                pad = np.pad(out, ((r, r), (r, r), (0, 0)), mode="edge")
                c_ = np.cumsum(np.cumsum(pad, 0), 1); c_ = np.pad(c_, ((1, 0), (1, 0), (0, 0)))
                k_ = 2 * r + 1
                out = (c_[k_:, k_:] - c_[:-k_, k_:] - c_[k_:, :-k_] + c_[:-k_, :-k_]) / (k_ * k_)
            return out
        # Hautfarbe des Porträts von außen in die Augenöffnung hineinfüllen (Diffusion), Einzelheiten (Lid, Wimpern) aus dem KI-Bild
        haut = PB[y0:y1, x0:x1].copy()
        unbek = dq > -14
        haut[unbek] = haut[~unbek].mean(0)
        for _ in range(400):
            gl = (np.roll(haut, 1, 0) + np.roll(haut, -1, 0) + np.roll(haut, 1, 1) + np.roll(haut, -1, 1)) / 4
            haut[unbek] = gl[unbek]
        detail = np.clip(zf / np.maximum(weich(zf), 1e-3), 0.25, 1.6)
        lid = np.clip(haut * detail, 0, 1)
        w = (1 - sm(-dq, 10.0, 18.0))[:, :, None]
        PZ[y0:y1, x0:x1] = PZ[y0:y1, x0:x1] * (1 - w) + lid * w
    log("geschlossene Augen in den Porträtraum übertragen")

os.makedirs(ordner, exist_ok=True)
bild_speichern(PB, os.path.join(ordner, NAME + "_portraet.jpg"))
bild_speichern(PZ, os.path.join(ordner, NAME + "_portraet_zu.jpg"))
bild_speichern(maske_rgba[:, :, :3], os.path.join(ordner, NAME + "_augen.png"), "PNG", alpha=maske_rgba[:, :, 3])

# ---- Lippenlinie im Porträt verfolgen (dunkelste Zeile zwischen den Mundwinkeln) ---------------------------------------
lum = PL @ np.array([0.3, 0.55, 0.15])
lum_g = lum.copy()
lum_g[1:-1] = (lum[:-2] + lum[1:-1] + lum[2:]) / 3
lum_g[:, 1:-1] = (lum_g[:, :-2] + lum_g[:, 1:-1] + lum_g[:, 2:]) / 3
ML = LM["P"]["mund"]
(ul, vl), (uc, vc), (ur, vr) = ML["ecke_l"], ML["mitte"], ML["ecke_r"]
U = np.arange(int(math.ceil(ul)), int(ur) + 1)
quad = np.polyfit([ul, uc, ur], [vl, vc, vr], 2)
V = []
for u in U:
    v0 = np.polyval(quad, u)
    ys = np.arange(int(v0 - 4), int(v0 + 5))
    V.append(ys[np.argmin(lum_g[ys, u])])
V = np.array(V, float)
V = np.array([np.median(V[max(0, i - 3):i + 4]) for i in range(len(V))])
V = np.convolve(np.pad(V, 3, mode="edge"), np.ones(7) / 7, "valid")
# an den Ecken genau auf die Landmarken ziehen
V += np.interp(U, [ul, ur], [vl - V[0], vr - V[-1]]) * (1 - sm(np.minimum(U - ul, ur - U), 0, 12))
log("Lippenlinie: %d Spalten, Mitte y %.1f (Landmarke %.1f)" % (len(U), np.interp(uc, U, V), vc))


def lippe_v(u):
    return np.interp(u, U, V, left=np.nan, right=np.nan)


def lippe_v_ext(u):
    """Lippenlinie, über die Ecken hinaus gerade verlängert."""
    v = np.interp(u, U, V)
    v = np.where(u < U[0], V[0] + (u - U[0]) * (V[3] - V[0]) / 3, v)
    v = np.where(u > U[-1], V[-1] + (u - U[-1]) * (V[-1] - V[-4]) / 3, v)
    return v


# ---- normieren wie der MPFB-Kopf (Augenabstand 0,066 m, Augenmitte x=0, y=-0,146, z=1,64) ----------------------------
iy = float(np.mean([ey_t[k][1] for k in ey_t]))
s = 0.066 / dE
tgt = np.array([0.0, -0.146, 1.64]); src = np.array([xm, iy, zE])
Mx = Matrix.Translation(Vector(tgt)) @ Matrix.Scale(s, 4) @ Matrix.Translation(Vector(-src))
T.data.transform(Mx); T.data.update()


def nrm(p):
    return (np.asarray(p, float) - src) * s + tgt


def tr(p):
    return (np.asarray(p, float) - tgt) / s + src


def p_von(Xn):
    return p_von_tr(tr(Xn))


dEm = 0.066
X3n = nrm(X3)
ey_n = {au: nrm(ey_t[au]) for au in ey_t}
mund_c = X3n[namen.index("mund.mitte")]
nase_n = X3n[namen.index("nase")]
kam_dir = kam_dir_t / np.linalg.norm(kam_dir_t)
log("normiert: Mund z %.4f, Nase y %.4f" % (mund_c[2], X3n[-1][1]))

# ---- verdünnen (Augen und Mund dicht), entfalten, backen ------------------------------------------------------------------
D = T.copy(); D.data = T.data.copy(); D.name = "gesicht"
bpy.context.scene.collection.objects.link(D)
D.data.materials.clear()
bm = bmesh.new(); bm.from_mesh(D.data)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=2e-5 * s)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
# lose Teile (Bartfetzen, schwebende Strähnen) entfernen: Zusammenhangskomponenten unter einem Mindestanteil
bm.faces.ensure_lookup_table()
gesehen_f = set(); weg = []
mind = float(a.get("losteil", 0.004)) * len(bm.faces)
for f0 in bm.faces:
    if f0.index in gesehen_f: continue
    komp_f = []; st_ = [f0]; gesehen_f.add(f0.index)
    while st_:
        f_ = st_.pop(); komp_f.append(f_)
        for e_ in f_.edges:
            for g_ in e_.link_faces:
                if g_.index not in gesehen_f:
                    gesehen_f.add(g_.index); st_.append(g_)
    if len(komp_f) < mind: weg.extend(komp_f)
log("lose Teile entfernt: %d Flächen" % len(weg))
bmesh.ops.delete(bm, geom=weg, context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.to_mesh(D.data); bm.free()
while D.data.uv_layers:
    D.data.uv_layers.remove(D.data.uv_layers[0])
# Bereiche getrennt verdünnen (Bearbeitungsmodus, nur Auswahl): Augen und Mund dicht, übriges Gesicht mittel, Rest grob
dv = netz_arrays(D)
face = (np.abs(dv[:, 0]) < 1.5 * dEm) & (dv[:, 2] > mund_c[2] - 1.5 * dEm) & (dv[:, 2] < 1.64 + 1.4 * dEm) & (dv[:, 1] < -0.10)
dicht = np.zeros(len(dv), bool)
for au in ey_n:
    e = ey_n[au]
    dicht |= np.hypot((dv[:, 0] - e[0]) / (0.45 * dEm), (dv[:, 2] - e[2]) / (0.27 * dEm)) < 1.0
dicht |= np.hypot((dv[:, 0] - mund_c[0]) / (0.85 * dEm), (dv[:, 2] - mund_c[2]) / (0.42 * dEm)) < 1.0
dicht &= dv[:, 1] < -0.12
klasse = np.where(dicht, 0, np.where(face, 1, 2))
ziele = [float(a.get("tris_dicht", 7000)), float(a.get("tris_gesicht", 4500)), float(a.get("tris_rest", 7300))]
for x in bpy.context.selected_objects: x.select_set(False)
D.select_set(True); bpy.context.view_layer.objects.active = D
for kl_ in (2, 1, 0):
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(D.data); bm.verts.ensure_lookup_table()
    cov = np.array([v.co for v in bm.verts])
    kv = np.where(dicht[:0].size == 0, 0, 0)
    dch = np.zeros(len(cov), bool)
    for au in ey_n:
        e = ey_n[au]
        dch |= np.hypot((cov[:, 0] - e[0]) / (0.45 * dEm), (cov[:, 2] - e[2]) / (0.27 * dEm)) < 1.0
    dch |= np.hypot((cov[:, 0] - mund_c[0]) / (0.85 * dEm), (cov[:, 2] - mund_c[2]) / (0.42 * dEm)) < 1.0
    dch |= np.hypot((cov[:, 0] - nase_n[0]) / (0.38 * dEm), (cov[:, 2] - (nase_n[2] + 0.25 * dEm)) / (0.55 * dEm)) < 1.0
    dch &= cov[:, 1] < -0.12
    fc = (np.abs(cov[:, 0]) < 1.5 * dEm) & (cov[:, 2] > mund_c[2] - 1.5 * dEm) & (cov[:, 2] < 1.64 + 1.4 * dEm) & (cov[:, 1] < -0.10)
    kl_v = np.where(dch, 0, np.where(fc, 1, 2))
    for v in bm.verts: v.select_set(False)
    nsel = 0
    for f in bm.faces:
        if all(kl_v[v.index] == kl_ for v in f.verts):
            f.select_set(True); nsel += 1
    bm.select_flush_mode(); bmesh.update_edit_mesh(D.data)
    if nsel:
        bpy.ops.mesh.decimate(ratio=min(1.0, ziele[kl_] / nsel))
    bpy.ops.object.mode_set(mode="OBJECT")
    log("Bereich %d: %d -> Ziel %d, jetzt gesamt %d Flächen" % (kl_, nsel, ziele[kl_], len(D.data.polygons)))
bm = bmesh.new(); bm.from_mesh(D.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.to_mesh(D.data); bm.free()
dv = netz_arrays(D)
log("verdünnt: %d Flächen" % len(D.data.polygons))
_ed = np.array([e.vertices[:] for e in D.data.edges]); _l = np.linalg.norm(dv[_ed[:, 0]] - dv[_ed[:, 1]], axis=1)
_mm = np.hypot((dv[:, 0] - mund_c[0]) / (0.9 * dEm), (dv[:, 2] - mund_c[2]) / (0.4 * dEm)) < 1.0
_mm &= dv[:, 1] < -0.12
_em = _mm[_ed[:, 0]] & _mm[_ed[:, 1]]
log("Kantenlänge Mund: Median %.2f mm, max %.2f mm (%d Kanten); gesamt Median %.2f mm" % (np.median(_l[_em]) / MM, _l[_em].max() / MM, _em.sum(), np.median(_l) / MM))
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(70), island_margin=0.002)
bpy.ops.object.mode_set(mode="OBJECT")
D.data.uv_layers[0].name = "atlas"

sc = bpy.context.scene
sc.render.engine = "CYCLES"
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "CUDA"; prefs.get_devices()
    for d_ in prefs.devices: d_.use = True
    sc.cycles.device = "GPU"
except Exception as ex:
    log("GPU nicht verfügbar", ex)
sc.cycles.samples = 4
tnode.image = timg_k
atlas = bpy.data.images.new("atlas", TEX, TEX)
mat = bpy.data.materials.new("haut"); mat.use_nodes = True
tx = mat.node_tree.nodes.new("ShaderNodeTexImage"); tx.image = atlas
mat.node_tree.nodes.active = tx
D.data.materials.append(mat)
for x in bpy.context.selected_objects: x.select_set(False)
T.select_set(True); D.select_set(True); bpy.context.view_layer.objects.active = D
bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_selected_to_active=True, cage_extrusion=0.012,
                    max_ray_distance=0.03, margin=12, use_clear=True)
atlas.filepath_raw = os.path.join(ordner, NAME + "_atlas.jpg"); atlas.file_format = "JPEG"; atlas.save()
mat.node_tree.nodes.remove(tx)
bpy.data.objects.remove(T, do_unlink=True)
log("Atlas gebacken")

# ---- Mund aufschneiden: entlang der Lippenlinie des Porträts (Nullstellen von g = v - Linie(u)) -------------------------------------
bm = bmesh.new(); bm.from_mesh(D.data)
bm.verts.ensure_lookup_table(); bm.normal_update()
co = np.array([v.co for v in bm.verts]); nv_ = np.array([v.normal for v in bm.verts])
pq = p_von(co)
gv = pq[:, 1] - lippe_v_ext(pq[:, 0])
halbP = (ur - ul) / 2
im_band = (np.abs(pq[:, 0] - (ul + ur) / 2) < 1.35 * halbP) & (np.abs(gv) < 0.7 * halbP) & (nv_ @ kam_dir > 0.05) & \
          (np.abs(co[:, 2] - mund_c[2]) < 0.5 * dEm) & (co[:, 1] < mund_c[1] + 0.03)
EPS = 0.02
gwert = {v: gv[v.index] for v in bm.verts}
bandv = {v for v in bm.verts if im_band[v.index]}
null = {v for v in bandv if abs(gwert[v]) < EPS}
zu_teilen = [e for e in bm.edges if e.verts[0] in bandv and e.verts[1] in bandv
             and gwert[e.verts[0]] * gwert[e.verts[1]] < 0 and abs(gwert[e.verts[0]]) >= EPS and abs(gwert[e.verts[1]]) >= EPS]
for e in zu_teilen:
    v0, v1 = e.verts
    f = gwert[v0] / (gwert[v0] - gwert[v1])
    p0, p1 = v0.co.copy(), v1.co.copy()
    ne, nvx = bmesh.utils.edge_split(e, v0, f)
    nvx.co = p0.lerp(p1, f)
    gwert[nvx] = 0.0; null.add(nvx); bandv.add(nvx)
paare = []
for f_ in {f for v in null for f in v.link_faces}:
    zs = [v for v in f_.verts if v in null]
    if len(zs) == 2 and not any(e for e in zs[0].link_edges if e.other_vert(zs[0]) is zs[1]):
        paare.append(zs)
for p_ in paare:
    try:
        bmesh.ops.connect_verts(bm, verts=p_)
    except Exception as ex:
        pass
bm.verts.ensure_lookup_table(); bm.edges.ensure_lookup_table()
nullco = np.array([v.co for v in null]); nullp = p_von(nullco)
nullu = {v: nullp[i, 0] for i, v in enumerate(null)}
spalt = [e for e in bm.edges if e.verts[0] in null and e.verts[1] in null
         and ul + 0.3 < nullu[e.verts[0]] < ur - 0.3 and ul + 0.3 < nullu[e.verts[1]] < ur - 0.3]
# größte zusammenhängende Kette behalten
nachbarn = {}
for e in spalt:
    for v in e.verts:
        nachbarn.setdefault(v, []).append(e)
gesehen = set(); kette = []
for v in nachbarn:
    if v in gesehen: continue
    stapel = [v]; komp = []
    while stapel:
        w = stapel.pop()
        if w in gesehen: continue
        gesehen.add(w); komp.append(w)
        for e in nachbarn[w]:
            stapel.append(e.other_vert(w))
    komps = locals().setdefault("komps", [])
    komps.append(komp)
# alle Kettenstücke mit mindestens 3 Punkten (Lücken an den Ecken sonst: Mund ginge nur teilweise auf)
kset = set(v for k_ in komps if len(k_) >= 3 for v in k_)
log("Kettenstücke:", sorted((len(k_) for k_ in komps), reverse=True)[:8])
spalt = [e for e in spalt if e.verts[0] in kset and e.verts[1] in kset]
uks = sorted(nullu[v] for v in kset)
log("Mundspalt: %d Kanten, Kette von u=%.1f bis %.1f (Ecken %.1f .. %.1f)" % (len(spalt), uks[0], uks[-1], ul, ur))
bmesh.ops.split_edges(bm, edges=spalt)
bm.to_mesh(D.data); bm.free()

# ---- Seiten (Ober-/Unterlippe) und Spaltpunkte ----------------------------------------------------------------------------------
me = D.data
dv = netz_arrays(D)
n = len(dv)
pq = p_von(dv)
gv = pq[:, 1] - lippe_v_ext(pq[:, 0])
gf = np.zeros(n); cnt = np.zeros(n)
for poly in me.polygons:
    vs = list(poly.vertices)
    g_c = float(np.mean(gv[vs]))
    for vi in vs:
        gf[vi] += np.sign(g_c); cnt[vi] += 1
unten = np.where(np.abs(gv) < 1.5, gf > 0, gv > 0)          # P-y wächst nach unten
# Randkanten des Spalts finden
me.calc_loop_triangles()
kz = {}
for poly in me.polygons:
    vs = list(poly.vertices)
    for i in range(len(vs)):
        k = tuple(sorted((vs[i], vs[(i + 1) % len(vs)])))
        kz[k] = kz.get(k, 0) + 1
randk = [k for k, c in kz.items() if c == 1 and abs(gv[k[0]]) < 1.0 and abs(gv[k[1]]) < 1.0 and
         abs(dv[k[0], 2] - mund_c[2]) < 0.3 * dEm and abs(dv[k[0], 0] - mund_c[0]) < 1.2 * dEm]
spaltv = sorted({v for k in randk for v in k}, key=lambda i: pq[i, 0])
ober = [i for i in spaltv if not unten[i]]; unter = [i for i in spaltv if unten[i]]
# Ecken: Punkte, die zu beiden Ketten gehören (gemeinsam), = äußerste Punkte
ecke_l = spaltv[0]; ecke_r = spaltv[-1]
for kette_ in (ober, unter):
    for e_ in (ecke_l, ecke_r):
        if e_ not in kette_: kette_.append(e_)
ober.sort(key=lambda i: pq[i, 0]); unter.sort(key=lambda i: pq[i, 0])
KL, KR = dv[ecke_l].copy(), dv[ecke_r].copy()
halbW = float(np.linalg.norm(KR - KL) / 2)
ex_m = (KR - KL) / np.linalg.norm(KR - KL)
MC = (KL + KR) / 2
MC[1] = float(dv[spaltv, 1].min())
MCz = float(np.median(dv[spaltv, 2]))
log("Spalt: oben %d, unten %d Punkte, Mundbreite %.1f mm" % (len(ober), len(unter), 2 * halbW / MM))

# Lippenlinie in 3D: z der Spaltpunkte über x (für Abstand ober-/unterhalb)
sx_ = dv[spaltv, 0]; sz_ = dv[spaltv, 2]; o_ = np.argsort(sx_)
def linie_z(x):
    return np.interp(x, sx_[o_], sz_[o_])
t_m = ((dv - MC) @ ex_m) / halbW                    # -1 .. 1 zwischen den Ecken
s_m = (dv[:, 2] - linie_z(dv[:, 0])) / halbW         # > 0 oberhalb der Linie
huelle = np.sqrt(np.clip(1 - t_m ** 2, 0, 1))         # Linse: Ecken bleiben zu
vorn = (dv[:, 1] < MC[1] + 0.02).astype(float)

# ---- Mundinnenraum: Lippen-Innenseite und Beutel aus den Spalträndern -----------------------------------------------------
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
bvh_g = BVHTree.FromBMesh(bm)


def hinter_flaeche(p, abstand):
    """Punkt mindestens 'abstand' hinter die Gesichtsfläche (Strahl von vorn) schieben."""
    r = bvh_g.ray_cast(Vector((p[0], p[1] - 0.2, p[2])), Vector((0, 1, 0)))
    if r[0] is not None and p[1] < r[0].y + abstand:
        p = p.copy(); p[1] = r[0].y + abstand
    return p


n_alt = len(bm.verts)
beutel_info = []      # (neuer Index, Quellpunkt, Faktor, Seite: 1 unten / 0 oben / 0.5 Mitte)
def neu(p, quelle, fak, seite):
    v = bm.verts.new(Vector(p))
    beutel_info.append((v, quelle, fak, seite))
    return v
ringe = {}
for name_, kette_, sg in (("o", ober, 1.0), ("u", unter, -1.0)):
    r1, r2 = [], []
    for j, i in enumerate(kette_):
        p = dv[i]; t_ = (p - MC) @ ex_m / halbW; env = math.sqrt(max(0.0, 1 - t_ * t_))
        ist_ecke = i in (ecke_l, ecke_r)
        q1 = np.array([MC[0] + (p[0] - MC[0]) * 0.94, p[1] + 2.5 * MM, p[2] + sg * 1.4 * MM * env])
        q2 = np.array([MC[0] + (p[0] - MC[0]) * 0.80, p[1] + 8.0 * MM, p[2] + sg * (5.0 if sg > 0 else 6.5) * MM * env - (0 if sg > 0 else 1.0 * MM)])
        if ist_ecke and name_ == "u":
            r1.append(ringe["o"][0][kette_.index(i) if False else (0 if i == ecke_l else -1)])
            r2.append(ringe["o"][1][0 if i == ecke_l else -1])
            continue
        r1.append(neu(hinter_flaeche(q1, 1.0 * MM), i, 1.0, 0.0 if sg > 0 else 1.0))
        r2.append(neu(hinter_flaeche(q2, 2.5 * MM), i, 0.7, 0.0 if sg > 0 else 1.0))
    ringe[name_] = (r1, r2)
# Rückwand: gemeinsamer Ring hinten, gleiche Anzahl wie die Oberlippe; untere Kette darauf abbilden
r3 = []
for j, i in enumerate(ober):
    p = dv[i]
    q3 = np.array([MC[0] + (p[0] - MC[0]) * 0.66, p[1] + 13.0 * MM, linie_z(p[0]) - 1.0 * MM])
    r3.append(neu(hinter_flaeche(q3, 4.0 * MM), i, 0.45, 0.5))
bm.verts.ensure_lookup_table()


def streifen(a_, b_):
    """Flächen zwischen zwei Ketten (verschieden lang erlaubt) per Zusammenführen nach Parameter."""
    na, nb = len(a_), len(b_)
    i = j = 0
    while i < na - 1 or j < nb - 1:
        ta = (i + 1) / (na - 1) if i < na - 1 else 9; tb = (j + 1) / (nb - 1) if j < nb - 1 else 9
        try:
            if ta <= tb:
                if len({a_[i], a_[i + 1], b_[j]}) == 3: bm.faces.new((a_[i], a_[i + 1], b_[j]))
                i += 1
            else:
                if len({a_[i], b_[j + 1], b_[j]}) == 3: bm.faces.new((a_[i], b_[j + 1], b_[j]))
                j += 1
        except ValueError:
            if ta <= tb: i += 1
            else: j += 1


vO = [bm.verts[i] for i in ober]; vU = [bm.verts[i] for i in unter]
streifen(vO, ringe["o"][0]); streifen(ringe["o"][0], ringe["o"][1]); streifen(ringe["o"][1], r3)
streifen(vU, ringe["u"][0]); streifen(ringe["u"][0], ringe["u"][1]); streifen(ringe["u"][1], r3)
bm.to_mesh(me); bm.free()
log("Mundbeutel: %d neue Punkte" % len(beutel_info))
bidx = {}
bm_tmp = None
# Indizes der neuen Punkte (Reihenfolge bleibt erhalten: alt zuerst, dann neu in Erzeugungsreihenfolge)
B_IDX = np.arange(n_alt, n_alt + len(beutel_info))
B_Q = np.array([q for (_, q, _, _) in beutel_info]); B_F = np.array([f for (_, _, f, _) in beutel_info]); B_S = np.array([s_ for (_, _, _, s_) in beutel_info])
dv = netz_arrays(D); n = len(dv)
nvn = np.empty(n * 3); me.vertices.foreach_get("normal", nvn); nvn = nvn.reshape(-1, 3)


def erweitern(arr, fuell):
    out = np.full((n,) + arr.shape[1:], fuell, dtype=arr.dtype if hasattr(arr, "dtype") else float)
    out[:len(arr)] = arr
    return out


unten = erweitern(unten, False); unten[B_IDX] = B_S > 0.75
t_m = ((dv - MC) @ ex_m) / halbW
s_m = (dv[:, 2] - linie_z(dv[:, 0])) / halbW
huelle = np.sqrt(np.clip(1 - t_m ** 2, 0, 1))
ist_beutel = np.zeros(n, bool); ist_beutel[B_IDX] = True
vorn = (1.0 - sm(dv[:, 1], MC[1] + 0.012, MC[1] + 0.032)) * ~ist_beutel

# ---- UV2 (Porträt), Farben (Gewicht des Porträts, Dunkelung im Mund) ---------------------------------------------------
pq = p_von(dv)
bvh_s = BVHTree.FromObject(D, bpy.context.evaluated_depsgraph_get())
cosw = np.clip(nvn @ kam_dir, 0, 1)
sicht = np.ones(n)
for i in np.where(cosw > 0.05)[0]:
    st = Vector(dv[i] + nvn[i] * 0.0004 + kam_dir * 0.0006)
    r_ = bvh_s.ray_cast(st, Vector(kam_dir), 0.5)
    an_nase = math.hypot((dv[i, 0] - nase_n[0]) / (0.5 * dEm), (dv[i, 2] - nase_n[2]) / (0.4 * dEm)) < 1.0
    if r_[0] is not None and r_[3] > 0.005:      # Lider/Lippen verdecken sich selbst nur wenige mm: nicht zählen (Nase streng)
        sicht[i] = 0.0
region = 1.0 - sm(np.hypot((dv[:, 0]) / (float(a.get("regx", 1.3)) * dEm), (dv[:, 2] - (1.64 - float(a.get("regm", 1.0)) * dEm)) / (float(a.get("regz", 2.4)) * dEm)), 0.7, 1.0)
# Vordergrund des Porträts (Hintergrund per Flutfüllung vom Rand aus, Farbe wie die Ecken)
_bgc = np.median(np.vstack([PB[:8, :8].reshape(-1, 3), PB[:8, -8:].reshape(-1, 3)]), 0)
_aehnl = np.linalg.norm(PB - _bgc, axis=2) < 0.07
_hg = np.zeros((PH, PW), bool); _hg[0, :] = _aehnl[0, :]; _hg[:, 0] = _aehnl[:, 0]; _hg[:, -1] = _aehnl[:, -1]
for _ in range(2000):
    _n = _hg | np.roll(_hg, 1, 0) | np.roll(_hg, -1, 0) | np.roll(_hg, 1, 1) | np.roll(_hg, -1, 1)
    _n &= _aehnl
    if (_n == _hg).all(): break
    _hg = _n
_vg = (~_hg).astype(float)
for _ in range(6):        # Rand 6 px nach innen
    _vg = np.minimum.reduce([_vg, np.roll(_vg, 1, 0), np.roll(_vg, -1, 0), np.roll(_vg, 1, 1), np.roll(_vg, -1, 1)])
log("Porträt-Hintergrund: %.0f %% der Fläche" % (100 * _hg.mean()))
vg_p = bilinear(_vg[:, :, None], pq[:, 0], pq[:, 1])[:, 0]
rand_p = sm(pq[:, 0], 2, 30) * sm(PW - pq[:, 0], 2, 30) * sm(pq[:, 1], 2, 30) * sm(PH - pq[:, 1], 2, 30)
# außerhalb der Gesichtsmitte nur, wo Porträt und Form-Textur übereinstimmen (Bart der Form vs. Hals im Porträt)
_ap = np.empty(TEX * TEX * 4, np.float32); atlas.pixels.foreach_get(_ap); _ap = _ap.reshape(TEX, TEX, 4)[:, :, :3]
_lv = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", _lv)
_uva = np.empty(len(me.loops) * 2); me.uv_layers["atlas"].data.foreach_get("uv", _uva); _uva = _uva.reshape(-1, 2)
acol = np.zeros((n, 3)); acol[_lv] = _ap[np.clip((_uva[:, 1] * (TEX - 1)).astype(int), 0, TEX - 1), np.clip((_uva[:, 0] * (TEX - 1)).astype(int), 0, TEX - 1)]
pcol_v = bilinear(PB, pq[:, 0], pq[:, 1])
_lum = np.array([0.3, 0.55, 0.15])
_eg = float(a.get("einsg", 1.0))
eins = 1.0 - sm(np.abs(pcol_v @ _lum - acol @ _lum), 0.12 * _eg, 0.28 * _eg)
mitte = 1.0 - sm(np.hypot(dv[:, 0] / dEm, (dv[:, 2] - (1.64 - 0.6 * dEm)) / (1.4 * dEm)), 0.85, 1.15)
# in den Augen (Porträtraum, etwas erweitert) immer das Porträt: dort stehen sonst die gemalten Ränder der Form-Textur
im_auge = np.zeros(n)
for au in ("auge_l", "auge_r"):
    dd = AUGEN[au]
    im_auge = np.maximum(im_auge, sm(dd[np.clip(pq[:, 1].astype(int), 0, PH - 1), np.clip(pq[:, 0].astype(int), 0, PW - 1)], -14.0, -6.0))
im_auge *= (cosw > 0.0)
# Nasenunterseite: lieber das Porträt (gestreckt) als die helleren Ränder der Form-Textur
nah_nase = 1.0 - sm(np.hypot((dv[:, 0] - nase_n[0]) / (0.45 * dEm), (dv[:, 2] - nase_n[2]) / (0.35 * dEm)), 0.7, 1.0)
im_auge = np.maximum(im_auge, float(a.get("nasezwang", 0.0)) * nah_nase * sm(nvn @ kam_dir, -0.35, 0.1) * sm(-nvn[:, 2], 0.3, 0.6) * (dv[:, 1] < nase_n[1] + 0.02))
w_p = np.maximum(im_auge, (sm(cosw, 0.15, 0.55) ** 1.0) * (1.0 - sm(-nvn[:, 2], 0.7, 0.95)) * sicht * region * rand_p * vg_p * np.maximum(mitte, eins)) * float(a.get("palpha", 1.0))
# Beutel: Porträtgewicht und Farbe vom Spaltpunkt
w_p[B_IDX] = w_p[B_Q]
pq[B_IDX] = pq[B_Q]
farbe = np.ones((n, 3))
farbe[B_IDX] = np.where((B_F > 0.9)[:, None], [0.55, 0.30, 0.28], np.where((B_F > 0.6)[:, None], [0.16, 0.07, 0.06], [0.07, 0.03, 0.03]))
# verdeckte Innenflächen der Form im Mundbereich (z. B. unter dem Bart) dunkel wie der Mundraum, sonst schimmert Haut durch den Spalt
mbox = (np.abs(dv[:, 0] - MC[0]) < 1.15 * halbW) & (np.abs(dv[:, 2] - linie_z(dv[:, 0])) < 0.6 * halbW) & ~ist_beutel & (dv[:, 1] > MC[1] + 0.004)
innen_n = 0
for i in np.where(mbox)[0]:
    r_ = bvh_s.ray_cast(Vector(dv[i] + np.array([0, -0.0004, 0])), Vector((0, -1, 0)), 0.05)
    if r_[0] is not None:
        farbe[i] = [0.07, 0.03, 0.03]; innen_n += 1
log("Innenflächen im Mund abgedunkelt:", innen_n)
uv2 = me.uv_layers.new(name="portraet")
lvi = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", lvi)
uv2.data.foreach_set("uv", np.stack([pq[lvi, 0] / PW, 1.0 - pq[lvi, 1] / PH], 1).ravel())
# Atlas-UV der Beutelpunkte: vom Spaltpunkt
uva = me.uv_layers["atlas"]
uvd = np.empty(len(me.loops) * 2); uva.data.foreach_get("uv", uvd); uvd = uvd.reshape(-1, 2)
erst = {}
for li, vi in enumerate(lvi):
    erst.setdefault(vi, uvd[li])
for li, vi in enumerate(lvi):
    if vi >= n_alt:
        uvd[li] = erst.get(int(B_Q[vi - n_alt]), uvd[li])
uva.data.foreach_set("uv", uvd.ravel())
me.uv_layers.active = uva
ca = me.color_attributes.new("farbe", "FLOAT_COLOR", "POINT")
ca.data.foreach_set("color", np.hstack([farbe, w_p[:, None]]).astype(np.float32).ravel())
me.color_attributes.active_color = ca
log("Porträt-UV: %d Punkte mit Gewicht > 0,5, verdeckt %d" % (int((w_p > 0.5).sum()), int((sicht == 0).sum())))

# ---- Gewichte für das Skelett ------------------------------------------------------------------------------------------------
x_rel = np.abs(dv[:, 0] - MC[0])
hinge_y = -0.146 + 0.9 * dEm
# Kinn aus dem Mittelprofil: wo die Vorderkante unterhalb des Mundes deutlich zurückweicht
streifen_m = np.where((x_rel < 0.12 * dEm) & (dv[:, 2] < MCz) & (dv[:, 2] > MCz - 1.6 * dEm) & ~ist_beutel & (dv[:, 1] < MC[1] + 0.04))[0]
kinn_z = MCz - 0.9 * dEm
stufen = np.arange(MCz - 1.6 * dEm, MCz, 0.03 * dEm)
fy = {}
for i in range(len(stufen) - 1):
    m_ = streifen_m[(dv[streifen_m, 2] >= stufen[i]) & (dv[streifen_m, 2] < stufen[i + 1])]
    if len(m_): fy[i] = dv[m_, 1].min()
ref_ = min([v for k_, v in fy.items() if stufen[k_] > MCz - 0.9 * dEm] or [MC[1]])
for i in sorted(fy, reverse=True):
    if fy[i] - ref_ > 0.35 * dEm:
        kinn_z = stufen[i + 1]; break
log("Kinn %.1f mm unter der Lippenlinie" % ((MCz - kinn_z) / MM))
# Hals unter dem Kinn: vom Porträt nicht gesehen, Form-Textur dort fleckig -> als Schatten abdunkeln (weich)
unterkinn = (1.0 - sm(dv[:, 2], kinn_z - 0.2 * dEm, kinn_z + 0.25 * dEm)) * (1.0 - sm(np.abs(dv[:, 0]), 0.9 * dEm, 1.4 * dEm)) * ~ist_beutel
unterkinn *= (1.0 - sm(dv[:, 2], 0, 0) * 0) * sm(kinn_z - dv[:, 2], -0.25 * dEm, 0.2 * dEm) * (1.0 - sm(kinn_z - dv[:, 2], 1.0 * dEm, 1.6 * dEm))
farbe *= (1.0 - float(a.get("halsdunkel", 0.45)) * unterkinn * (1.0 - np.clip(w_p, 0, 1)))[:, None]
# Nasenflügel-Innenseiten, die das Porträt nicht sieht: Form-Textur dort zu hell -> abdunkeln
if a.get("nasedebug"):
    _pc = bilinear(PB, pq[:, 0], pq[:, 1]); _mix = acol * (1 - np.clip(w_p, 0, 1))[:, None] + _pc * np.clip(w_p, 0, 1)[:, None]
    _nn = np.where((nah_nase > 0.3) & ~ist_beutel)[0]
    _hell = _nn[np.argsort(-(_mix[_nn] @ np.array([0.3, 0.55, 0.15])))][:12]
    for _i in _hell:
        log("NASE v%d xyz %s wp %.2f sicht %.0f cosw %.2f n %s acol %s pcol %s" % (_i, np.round((dv[_i] - nase_n) / MM, 1), w_p[_i], sicht[_i], cosw[_i], np.round(nvn[_i], 2), np.round(acol[_i], 2), np.round(_pc[_i], 2)))
# zusätzlich: Flächen an der Nase, die vom Porträt weg zeigen (Flügelränder, Unterseite), wie Schatten abdunkeln
nf = nah_nase * (dv[:, 2] < nase_n[2] + 0.1 * dEm) * np.maximum(1.0 - np.clip(w_p, 0, 1), 1.0 - sm(cosw, 0.15, 0.55))
farbe *= (1.0 - float(a.get("nasedunkel", 0.0)) * nf)[:, None]
# Formschatten aus der Geometrie (Umgebungsverdeckung, kurze Reichweite): Augenhöhlen, Nasenflügel, Lippen, Ohrmuschel.
# Die Projektion ist schattenlos; dieser Faktor gibt die Form zurück, das Licht im Spiel macht den Rest.
if float(a.get("ao", 0.0)) > 0:
    import random as _rnd
    _rnd.seed(3)
    _dirs = []
    while len(_dirs) < int(a.get("ao_strahlen", 48)):
        _v = np.array([_rnd.gauss(0, 1) for _ in range(3)]); _v /= np.linalg.norm(_v); _dirs.append(_v)
    _dirs = np.array(_dirs)
    _reich = float(a.get("ao_weite", 0.012))
    ao = np.ones(n)
    _bvh_ao = BVHTree.FromObject(D, bpy.context.evaluated_depsgraph_get())
    for i_ in np.where(~ist_beutel)[0]:
        _n = nvn[i_]
        _d = _dirs * np.sign(_dirs @ _n)[:, None]
        _w = _d @ _n
        _p = Vector(dv[i_] + _n * 0.0003)
        _tr = 0.0; _ws = 0.0
        for d_, w_ in zip(_d, _w):
            r_ = _bvh_ao.ray_cast(_p, Vector(d_), _reich)
            if r_[0] is not None:
                _tr += w_ * (1.0 - r_[3] / _reich)
            _ws += w_
        ao[i_] = 1.0 - _tr / max(_ws, 1e-6)
    # glätten über Nachbarn (eine Runde)
    _nb = [[] for _ in range(n)]
    for e_ in me.edges:
        a0, a1 = e_.vertices; _nb[a0].append(a1); _nb[a1].append(a0)
    ao = np.array([(ao[i_] + sum(ao[j] for j in _nb[i_])) / (1 + len(_nb[i_])) for i_ in range(n)])
    ao_f = 1.0 - float(a.get("ao", 0.0)) * (1.0 - np.clip(ao, 0, 1))
    ao_f[ist_beutel] = 1.0
    farbe *= ao_f[:, None]
    log("Formschatten: Mittel %.2f, dunkelste 5 %% %.2f" % (ao_f.mean(), np.percentile(ao_f, 5)))
# ---- Mehrere Ansichten (Schräg, Profil) in den Atlas projizieren -------------------------------------------------------------
# Landmarken-Datei: "ansichten": [{"bild": <png>, "punkte": {"auge_l.aussen": [x, y], ...}}] (Namen wie oben, Pixel, Zeile von oben).
# Jede Ansicht bekommt eine eigene orthografische Kamera (angepasst an die sichtbaren Landmarken); der neue Atlas ist eine nach
# Blickwinkel gewichtete Mischung aller Ansichten (Front eingeschlossen) und der TRELLIS-Textur (dort, wo keine Ansicht hinsieht).
if LM.get("ansichten"):
    _lum3 = np.array([0.3, 0.55, 0.15])
    ansichten = [{"bild": a["pbild"], "punkte": None}] + LM["ansichten"]
    gew = []; uvs = []; bilder = []
    dvt = tr(dv)
    for k_, an in enumerate(ansichten):
        if an["punkte"] is None:
            par_k, img_k = par, PB
            q_ = p_von(dv)
        else:
            _i, img_k = bild_laden(an["bild"])
            nm_ = [nm for nm in an["punkte"] if nm in namen]
            P2k = np.array([an["punkte"][nm] for nm in nm_], float); X3k = np.array([X3[namen.index(nm)] for nm in nm_])
            bestk = None
            for psi0 in np.linspace(-1.6, 1.6, 9):
                pk = np.array([psi0, 0.0, 0.0, par[3], P2k[:, 0].mean(), P2k[:, 1].mean()])
                fk = ((proj(pk, X3k) - P2k) ** 2).sum(); st_k = np.array([0.1, 0.05, 0.05, 30.0, 10.0, 10.0])
                for it in range(500):
                    vb = False
                    for j_ in range(6):
                        for sg in (1, -1):
                            qk = pk.copy(); qk[j_] += sg * st_k[j_]
                            ff = ((proj(qk, X3k) - P2k) ** 2).sum()
                            if ff < fk: pk, fk, vb = qk, ff, True
                    if not vb: st_k *= 0.6
                if bestk is None or fk < bestk[1]: bestk = (pk, fk)
            par_k = bestk[0]
            log("Ansicht %s: Gier %.1f Nick %.1f Roll %.1f Maßstab %.0f, Restfehler %.1f px aus %d Punkten" % (os.path.basename(an["bild"]),
                math.degrees(par_k[0]), math.degrees(par_k[1]), math.degrees(par_k[2]), par_k[3], math.sqrt(bestk[1] / len(nm_)), len(nm_)))
            q_ = proj(par_k, dvt)
        Rk = rot(par_k[0], par_k[1], par_k[2])
        kd_t = Rk.T @ np.array([0.0, -1.0, 0.0]); kd_t /= np.linalg.norm(kd_t)
        cos_k = np.clip(nvn @ kd_t, 0, 1)
        sicht_k = np.ones(n)
        for i_ in np.where(cos_k > 0.05)[0]:
            r_ = bvh_s.ray_cast(Vector(dv[i_] + nvn[i_] * 0.0004 + kd_t * 0.0006), Vector(kd_t), 0.5)
            if r_[0] is not None and r_[3] > 0.003:
                sicht_k[i_] = 0.0
        hh, ww = img_k.shape[:2]
        bgc = np.median(np.vstack([img_k[:8, :8].reshape(-1, 3), img_k[:8, -8:].reshape(-1, 3)]), 0)
        aeh = np.linalg.norm(img_k - bgc, axis=2) < 0.07
        hg = np.zeros((hh, ww), bool); hg[0, :] = aeh[0, :]; hg[:, 0] = aeh[:, 0]; hg[:, -1] = aeh[:, -1]
        for _ in range(2000):
            nn_ = (hg | np.roll(hg, 1, 0) | np.roll(hg, -1, 0) | np.roll(hg, 1, 1) | np.roll(hg, -1, 1)) & aeh
            if (nn_ == hg).all(): break
            hg = nn_
        vgk = (~hg).astype(float)
        for _ in range(5):
            vgk = np.minimum.reduce([vgk, np.roll(vgk, 1, 0), np.roll(vgk, -1, 0), np.roll(vgk, 1, 1), np.roll(vgk, -1, 1)])
        im_bild = (q_[:, 0] > 2) & (q_[:, 0] < ww - 3) & (q_[:, 1] > 2) & (q_[:, 1] < hh - 3)
        w_k = sm(cos_k, 0.2, 0.75) ** 2 * sicht_k * bilinear(vgk[:, :, None], q_[:, 0], q_[:, 1])[:, 0] * im_bild
        w_k *= (1.0 - sm(-nvn[:, 2], 0.7, 0.95))
        gew.append(w_k); uvs.append(q_ / np.array([ww, hh])); bilder.append(img_k)
    # Farbabgleich jeder Ansicht an die Front (Median im Überlappungsbereich)
    farb_v = [bilinear(b_, u_[:, 0] * b_.shape[1], u_[:, 1] * b_.shape[0]) for b_, u_ in zip(bilder, uvs)]
    for k_ in range(1, len(bilder)):
        haut_f = (farb_v[0] @ _lum3 > 0.4) & (farb_v[0][:, 0] > farb_v[0][:, 2] * 1.1)
        haut_k = (farb_v[k_] @ _lum3 > 0.4) & (farb_v[k_][:, 0] > farb_v[k_][:, 2] * 1.1)
        ue = (gew[0] > 0.2) & (gew[k_] > 0.2) & ~ist_beutel & haut_f & haut_k & (mitte > 0.5)
        if ue.sum() > 30:
            g_k = np.clip(np.median(farb_v[0][ue], 0) / np.maximum(np.median(farb_v[k_][ue], 0), 1e-3), 0.7, 1.4)
        else:
            g_k = np.ones(3)
        bilder[k_] = np.clip(bilder[k_] * g_k, 0, 1)
        if ue.sum() > 30: log("  Median Front %s, Ansicht %s" % (np.round(np.median(farb_v[0][ue], 0), 3), np.round(np.median(farb_v[k_][ue], 0), 3)))
        log("Ansicht %d: Farbabgleich %s, Überlappung %d Punkte, Anteil > 0,5: %d Punkte" % (k_, np.round(g_k, 3), int(ue.sum()), int((gew[k_] > 0.5).sum())))
    # nur wo die Ansicht zur Form-Textur passt (Helligkeit) und nur am Kopf (Kleidung/Haar der Ansichten sitzt anders)
    kopf_m = sm(dv[:, 2], kinn_z - 0.6 * dEm, kinn_z - 0.1 * dEm) * (1.0 - sm(dv[:, 2], 1.64 + 1.0 * dEm, 1.64 + 1.5 * dEm))
    for k_ in range(len(bilder)):
        fk_ = bilinear(bilder[k_], uvs[k_][:, 0] * bilder[k_].shape[1], uvs[k_][:, 1] * bilder[k_].shape[0])
        pass_k = 1.0 - sm(np.abs(fk_ @ _lum3 - acol @ _lum3), 0.10, 0.22)
        gew[k_] = gew[k_] * pass_k * kopf_m
    W = np.array(gew)                      # (Ansichten, n)
    W = W ** 2 / np.maximum(W.sum(0), 1e-6)  # schärfer: die beste Ansicht dominiert
    w_rest = np.clip(1.0 - W.sum(0), 0, 1) + 0.02
    summe = W.sum(0) + w_rest
    W = W / summe; w_rest = w_rest / summe
    W[:, ist_beutel] = 0.0; w_rest[ist_beutel] = 1.0
    # Atlas neu backen: Emission = Summe w_k * Ansicht_k(uv_k) + w_rest * alter Atlas
    lvi_ = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", lvi_)
    matb = bpy.data.materials.new("mehransicht"); matb.use_nodes = True
    ntb = matb.node_tree; ntb.nodes.clear()
    out_ = ntb.nodes.new("ShaderNodeOutputMaterial"); em_ = ntb.nodes.new("ShaderNodeEmission")
    ntb.links.new(em_.outputs["Emission"], out_.inputs["Surface"])
    def textur(img_arr, name_):
        h_, w_ = img_arr.shape[:2]
        im_ = bpy.data.images.new(name_, w_, h_)
        rgba = np.ones((h_, w_, 4), np.float32); rgba[:, :, :3] = img_arr
        im_.pixels.foreach_set(rgba[::-1].ravel()); im_.update()
        im_.colorspace_settings.name = "sRGB"
        return im_
    summe_node = None
    teile = [(atlas, "atlas", w_rest)] + [(textur(bilder[k_], "ans_%d" % k_), "ans_%d" % k_, W[k_]) for k_ in range(len(bilder))]
    for k_, (b_, uvname, w_) in enumerate(teile):
        if uvname != "atlas":
            ul_ = me.uv_layers.new(name=uvname)
            u_ = uvs[k_ - 1]
            ul_.data.foreach_set("uv", np.stack([u_[lvi_, 0], 1.0 - u_[lvi_, 1]], 1).ravel())
        at_ = me.attributes.new("w_%d" % k_, "FLOAT", "POINT"); at_.data.foreach_set("value", w_.astype(np.float32))
        uvn = ntb.nodes.new("ShaderNodeUVMap"); uvn.uv_map = uvname
        tx_ = ntb.nodes.new("ShaderNodeTexImage"); tx_.image = b_; tx_.extension = "EXTEND"
        ntb.links.new(uvn.outputs["UV"], tx_.inputs["Vector"])
        an_ = ntb.nodes.new("ShaderNodeAttribute"); an_.attribute_name = "w_%d" % k_
        mu_ = ntb.nodes.new("ShaderNodeMix"); mu_.data_type = "RGBA"; mu_.blend_type = "MULTIPLY"; mu_.inputs["Factor"].default_value = 1.0
        ntb.links.new(tx_.outputs["Color"], mu_.inputs[6]); ntb.links.new(an_.outputs["Fac"], mu_.inputs[7])
        if summe_node is None:
            summe_node = mu_.outputs[2]
        else:
            ad_ = ntb.nodes.new("ShaderNodeMix"); ad_.data_type = "RGBA"; ad_.blend_type = "ADD"; ad_.inputs["Factor"].default_value = 1.0
            ntb.links.new(summe_node, ad_.inputs[6]); ntb.links.new(mu_.outputs[2], ad_.inputs[7]); summe_node = ad_.outputs[2]
    ntb.links.new(summe_node, em_.inputs["Color"])
    neu_atlas = bpy.data.images.new("atlas_mehr", TEX, TEX)
    ziel_ = ntb.nodes.new("ShaderNodeTexImage"); ziel_.image = neu_atlas
    ntb.nodes.active = ziel_
    me.uv_layers["atlas"].active = True; me.uv_layers["atlas"].active_render = True
    alte_mats = list(me.materials); me.materials.clear(); me.materials.append(matb)
    for x in bpy.context.selected_objects: x.select_set(False)
    D.select_set(True); bpy.context.view_layer.objects.active = D
    bpy.ops.object.bake(type="EMIT", margin=12, use_clear=True, use_selected_to_active=False)
    neu_atlas.filepath_raw = os.path.join(ordner, NAME + "_atlas.jpg"); neu_atlas.file_format = "JPEG"; neu_atlas.save()
    me.materials.clear()
    for m_ in alte_mats: me.materials.append(m_)
    for k_ in range(len(bilder)):
        me.uv_layers.remove(me.uv_layers["ans_%d" % k_])
    for k_ in range(len(teile)):
        me.attributes.remove(me.attributes["w_%d" % k_])
    me.uv_layers["atlas"].active = True; me.uv_layers["atlas"].active_render = True
    log("Mehransicht-Atlas gebacken aus %d Ansichten" % len(bilder))
ca.data.foreach_set("color", np.hstack([farbe, w_p[:, None]]).astype(np.float32).ravel())
unter_tief = -s_m          # > 0 unterhalb der Lippenlinie (in Halbbreiten)
f_nah = 1.0 - sm(np.abs(t_m), 0.45, 1.05)                    # nahe der Linie: Linse, Ecken bleiben
f_fern = 1.0 - sm(x_rel, 0.8 * dEm, 1.25 * dEm)               # weiter unten: ganzer Unterkiefer
w_j = np.where(unten, f_nah + (f_fern - f_nah) * sm(unter_tief, 0.15, 1.0), 0.0)
w_j *= 1.0 - sm(dv[:, 1], hinge_y - 0.35 * dEm, hinge_y)      # hinter dem Kiefergelenk nichts
w_j *= sm(dv[:, 2], kinn_z - 0.30 * dEm, kinn_z + 0.05 * dEm)  # unter dem Kinn nichts
# Beutel: unten wie der Spaltpunkt, Rückwand halb
unten_naechst = np.array([unter[int(np.argmin(np.abs(pq[unter, 0] - pq[q, 0])))] for q in B_Q])
w_j[B_IDX] = np.where(B_S > 0.75, w_j[B_Q], np.where(B_S > 0.25, 0.5 * w_j[unten_naechst], 0.0))
neck_h = sm(dv[:, 2], kinn_z - 1.1 * dEm, kinn_z - 0.2 * dEm)
w_k = (1.0 - w_j) * neck_h
w_h = (1.0 - w_j) * (1.0 - neck_h)
for name_, w in (("kiefer", w_j), ("kopf", w_k), ("hals", w_h)):
    g = D.vertex_groups.new(name=name_)
    for i in np.where(w > 0.002)[0]:
        g.add([int(i)], float(w[i]), "REPLACE")
log("Gewichte: Kiefer-Punkte", int((w_j > 0.5).sum()))

# ---- eigene Formen ------------------------------------------------------------------------------------------------------------
formen = {}
lip_o = 1.0 - sm(s_m, 0.12, 0.75)                              # Oberlippe bis unter die Nase
lip_u = 1.0 - sm(-s_m, 0.20, 1.10)                             # Unterlippe bis zum Kinn
breit_lat = 1.0 - sm(np.abs(t_m), 1.0, 1.6)
nah_mund = (np.abs(dv[:, 2] - MCz) < 1.2 * dEm) * vorn
# Lippen öffnen sich (Linse über die volle Breite, Ecken zu)
d_ = np.zeros((n, 3))
d_[:, 2] = np.where(unten, -2.4 * MM * huelle * lip_u, 0.9 * MM * huelle * lip_o)
d_[:, 1] = np.where(unten, 0.6 * MM * huelle * lip_u, 0.0)
formen["lippen_auf"] = d_ * nah_mund[:, None]
# rund (o/u): zur Mitte ziehen und vorschieben
r_ = np.hypot((dv[:, 0] - MC[0]) / (1.35 * halbW), (dv[:, 2] - MCz) / (1.0 * halbW))
w_r = (1.0 - sm(r_, 0.55, 1.35)) * vorn
d_ = np.zeros((n, 3))
d_[:, 0] = -(dv[:, 0] - MC[0]) * 0.30 * w_r
d_[:, 1] = -2.6 * MM * w_r
d_[:, 2] = -(dv[:, 2] - MCz) * 0.12 * w_r
formen["mund_rund"] = d_
# breit (e/i): Mundwinkel nach außen und etwas zurück
d_ = np.zeros((n, 3))
w_b = (1.0 - sm(r_, 0.6, 1.6)) * vorn
d_[:, 0] = (dv[:, 0] - MC[0]) * 0.14 * w_b
d_[:, 1] = 0.8 * MM * w_b * np.clip(np.abs(t_m), 0, 1)
formen["mund_breit"] = d_
# gepresst (m/b/p): Lippen nach innen, etwas schmaler
d_ = np.zeros((n, 3))
w_pr = huelle * np.where(unten, lip_u, lip_o) * nah_mund
d_[:, 1] = 0.7 * MM * w_pr
d_[:, 2] = np.where(unten, 0.25 * MM, -0.25 * MM) * w_pr
formen["mund_press"] = d_
# f/v: Unterlippe hoch und zurück unter die oberen Zähne
d_ = np.zeros((n, 3))
w_f = huelle * lip_u * unten * nah_mund
d_[:, 1] = 1.6 * MM * w_f; d_[:, 2] = 0.6 * MM * w_f
d_[:, 2] += np.where(~unten, 0.6 * MM * huelle * lip_o * nah_mund, 0.0)
formen["lippe_f"] = d_
# Lächeln je Seite: Mundwinkel hoch, außen, zurück; Wange hebt sich; unteres Lid etwas hoch (cheekSquint)
for seite, K, sgn in (("l", KL, -1.0), ("r", KR, 1.0)):
    d_ = np.zeros((n, 3))
    dk = np.hypot(dv[:, 0] - K[0], dv[:, 2] - K[2]) / halbW
    w_c = np.exp(-(dk / 0.8) ** 2) * vorn
    d_[:, 0] += sgn * 2.3 * MM * w_c
    d_[:, 1] += 1.7 * MM * w_c
    d_[:, 2] += 3.0 * MM * w_c
    # Wange
    Q = K + np.array([sgn * 0.55 * halbW, 0, 1.45 * halbW])
    dq = np.hypot((dv[:, 0] - Q[0]) / (1.0 * halbW), (dv[:, 2] - Q[2]) / (0.9 * halbW))
    w_q = np.exp(-dq ** 2) * vorn
    d_[:, 2] += 2.0 * MM * w_q; d_[:, 1] -= 1.2 * MM * w_q
    # unteres Lid
    E = ey_n["auge_r" if sgn > 0 else "auge_l"]
    w_s = np.exp(-(((dv[:, 0] - E[0]) / (0.33 * dEm)) ** 2 + ((dv[:, 2] - (E[2] - 0.16 * dEm)) / (0.09 * dEm)) ** 2)) * vorn
    d_[:, 2] += 1.0 * MM * w_s; d_[:, 1] -= 0.3 * MM * w_s
    d_[ist_beutel] = 0.0
    formen["laecheln_" + seite] = d_
# Augen zu: Lid wölbt sich etwas vor (Augapfel darunter)
d_ = np.zeros((n, 3))
for au in ("auge_l", "auge_r"):
    dd = AUGEN[au]
    dq = dd[np.clip(pq[:, 1].astype(int), 0, PH - 1), np.clip(pq[:, 0].astype(int), 0, PW - 1)]
    w_a = sm(dq, -3.0, 4.0) * (cosw > 0.2) * (w_p > 0.3)
    d_[:, 1] -= 0.9 * MM * w_a
formen["augen_zu"] = d_
# Brauen hoch
d_ = np.zeros((n, 3))
for au in ("auge_l", "auge_r"):
    E = ey_n[au]
    w_br = np.exp(-(((dv[:, 0] - E[0]) / (0.5 * dEm)) ** 2 + ((dv[:, 2] - (E[2] + 0.32 * dEm)) / (0.2 * dEm)) ** 2)) * vorn
    d_[:, 2] += 1.6 * MM * w_br
formen["brauen_hoch"] = d_
# Beutelpunkte folgen ihrem Spaltpunkt (außer beim Lächeln: dort gleiche Verschiebung, Ecken geschlossen)
for k_, d_ in formen.items():
    if k_.startswith("laecheln"):
        d_[B_IDX] = d_[B_Q] * B_F[:, None]
    elif k_ in ("mund_rund", "mund_breit"):
        d_[B_IDX] = d_[B_Q]                     # seitlich voll mitgehen, sonst ragt der Beutel an den Ecken heraus
    elif k_ not in ("augen_zu", "brauen_hoch"):
        d_[B_IDX] = d_[B_Q] * B_F[:, None]
D.shape_key_add(name="Basis", from_mix=False)
for k_, d_ in formen.items():
    sk_ = D.shape_key_add(name=k_, from_mix=False)
    sk_.data.foreach_set("co", (dv + d_).ravel())
log("Formen:", ", ".join(formen))

# ---- Zähne (ohne Zahnfleisch) und Zunge -------------------------------------------------------------------------------------
def material(name, farbe, rauh=0.6):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*farbe, 1); p.inputs["Roughness"].default_value = rauh
    return m


def zahnreihe(name, oben):
    bm = bmesh.new()
    seg = 14
    reihen = []
    for j in range(seg + 1):
        t_ = -0.78 + 1.56 * j / seg
        x = MC[0] + t_ * halbW
        zl = float(linie_z(x))
        yl = float(np.interp(x, sx_[o_], dv[spaltv, 1][o_])) if len(spaltv) else MC[1]
        tief = (3.2 if oben else 5.5) * MM + 0.55 * halbW * t_ * t_
        if oben:
            zs = (zl + 1.2 * MM, zl + 8.0 * MM)
        else:
            zs = (zl - 4.0 * MM, zl - 8.5 * MM)
        ring = []
        for z_ in zs:
            for dy_ in (0.0, 1.8 * MM):
                ring.append(bm.verts.new((x, yl + tief + dy_, z_)))
        reihen.append(ring)
    for j in range(seg):
        a_, b_ = reihen[j], reihen[j + 1]
        bm.faces.new((a_[0], b_[0], b_[2], a_[2]))        # Vorderseite
        bm.faces.new((a_[1], a_[3], b_[3], b_[1]))        # Rückseite
        bm.faces.new((a_[0], a_[1], b_[1], b_[0]))        # Schneidekante
    meh = bpy.data.meshes.new(name); bm.to_mesh(meh); bm.free()
    o = bpy.data.objects.new(name, meh); sc.collection.objects.link(o)
    zv = netz_arrays(o)
    zv = np.array([hinter_flaeche(p, (1.8 if oben else 3.5) * MM) for p in zv])
    meh.vertices.foreach_set("co", zv.ravel())
    t_ = (zv[:, 0] - MC[0]) / halbW
    hell = (1.0 - 0.75 * np.clip(np.abs(t_) / 0.78, 0, 1) ** 1.5) * (0.72 if oben else 0.42)
    hell *= np.where(np.abs(zv[:, 2] - linie_z(zv[:, 0])) > 3.5 * MM, 0.55, 1.0)
    c_ = np.stack([0.86 * hell, 0.82 * hell, 0.72 * hell, np.ones_like(hell)], 1).astype(np.float32)
    ca_ = meh.color_attributes.new("farbe", "FLOAT_COLOR", "POINT"); ca_.data.foreach_set("color", c_.ravel())
    o.data.materials.append(material("zahn", (1, 1, 1), 0.45))
    for p_ in meh.polygons: p_.use_smooth = True
    return o


ZO = zahnreihe("zaehne_oben", True)
ZU = zahnreihe("zaehne_unten", False)
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=6, radius=1.0)
bmesh.ops.scale(bm, vec=(0.42 * halbW, 8 * MM, 2.8 * MM), verts=bm.verts)
bmesh.ops.translate(bm, vec=(MC[0], MC[1] + 13.0 * MM, MCz - 5.5 * MM), verts=bm.verts)
zm = bpy.data.meshes.new("zunge"); bm.to_mesh(zm); bm.free()
ZG = bpy.data.objects.new("zunge", zm); sc.collection.objects.link(ZG)
zgv = np.array([hinter_flaeche(p, 3.0 * MM) for p in netz_arrays(ZG)])
zm.vertices.foreach_set("co", zgv.ravel())
ZG.data.materials.append(material("zunge", (0.36, 0.11, 0.10), 0.5))
for p_ in zm.polygons: p_.use_smooth = True

# ---- Skelett ----------------------------------------------------------------------------------------------------------------
arm = bpy.data.armatures.new("gerippe"); A = bpy.data.objects.new("gerippe", arm); sc.collection.objects.link(A)
for x in bpy.context.selected_objects: x.select_set(False)
bpy.context.view_layer.objects.active = A
bpy.ops.object.mode_set(mode="EDIT")
eb = arm.edit_bones
b_hals = eb.new("hals"); b_hals.head = (0, hinge_y, kinn_z - 1.3 * dEm); b_hals.tail = (0, hinge_y, kinn_z - 0.3 * dEm)
b_kopf = eb.new("kopf"); b_kopf.head = b_hals.tail; b_kopf.tail = (0, hinge_y, 1.64 + 2.0 * dEm); b_kopf.parent = b_hals
b_kief = eb.new("kiefer"); b_kief.head = (0, hinge_y, MCz + 0.55 * dEm); b_kief.tail = (0, MC[1], kinn_z + 0.1 * dEm); b_kief.parent = b_kopf
for b_ in eb: b_.roll = 0.0
bpy.ops.object.mode_set(mode="OBJECT")
for o in (D, ZO, ZU, ZG):
    o.parent = A
    md = o.modifiers.new("Skelett", "ARMATURE"); md.object = A
for o, gname in ((ZO, "kopf"), (ZU, "kiefer"), (ZG, "kiefer")):
    g = o.vertex_groups.new(name=gname); g.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")

# ---- Export ------------------------------------------------------------------------------------------------------------------
bpy.ops.object.select_all(action="DESELECT")
for o in (D, ZO, ZU, ZG, A): o.select_set(True)
bpy.context.view_layer.objects.active = A
bpy.ops.export_scene.gltf(filepath=a["aus"], export_format="GLB", use_selection=True, export_skins=True, export_animations=False,
                          export_yup=True, export_vertex_color="ACTIVE", export_all_vertex_colors=True, export_texcoords=True,
                          export_morph=True, export_morph_normal=True)
tris = sum(len(p.vertices) - 2 for o in (D, ZO, ZU, ZG) for p in o.data.polygons)
# Achsen der Porträtkamera für den Shader (Godot: x, z, -y)
ax_u = R_cam.T @ np.array([1.0, 0, 0]); ax_v = R_cam.T @ np.array([0, 0, -1.0])
g_ = lambda v: [float(v[0]), float(v[2]), float(-v[1])]
json.dump({"achse_u": g_(ax_u), "achse_v": g_(ax_v), "zur_kamera": g_(kam_dir), "dreiecke": tris}, open(os.path.join(ordner, NAME + ".json"), "w"))
log("EXPORT", a["aus"], os.path.getsize(a["aus"]), "Bytes", tris, "Dreiecke")

if a.get("vorschau"):
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "VERTEX" if a.get("vfarbe") else "OBJECT"
    if a.get("wire"): sc.display.shading.show_xray = False; sc.display.shading.type = "WIREFRAME"
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x = sc.render.resolution_y = 700
    sc.world = bpy.data.worlds.new("w2"); sc.world.color = (0.85, 0.85, 0.9)
    cam = bpy.data.objects.new("c2", bpy.data.cameras.new("c2")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = float(a.get("vos", 0.12))
    kb = D.data.shape_keys.key_blocks
    for name_, werte in (("ruhe", {}), ("auf", {"lippen_auf": 1.0}), ("rund", {"mund_rund": 1.0, "lippen_auf": 0.5}), ("laecheln", {"laecheln_l": 1.0, "laecheln_r": 1.0})):
        for k_ in kb: k_.value = 0.0
        for k_, v_ in werte.items(): kb[k_].value = v_
        for ans, az in (("vorn", 0), ("seite", 70)):
            cam.location = (math.sin(math.radians(az)) * 3, -math.cos(math.radians(az)) * 3, MCz)
            cam.rotation_euler = (math.radians(90), 0, math.radians(az))
            sc.render.filepath = os.path.join(ordner, "vorschau_%s_%s_%s.png" % (NAME, name_, ans))
            bpy.ops.render.render(write_still=True)
