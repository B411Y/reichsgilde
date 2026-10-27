# Versuch HG-Köpfe: Rassenform „Elb“ aus einer TRELLIS-Elbenform gewinnen.
# Aufruf: blender_hg.sh -b <mensch_a.blend> -P hg_elbform.py -- <trellis_d.glb> <ziel.npz> [bildordner]
#
# Ablauf (alles automatisch, gleiche Topologie wie HG, Rig und Mund bleiben unberührt):
#  1. Grundform: HG-Basis + Geschlechtsform (Durchschnittsgesicht, ohne die Vorlage)
#  2. Landmarken auf beiden Netzen aus Formschlüsseln: Lidschluss (Augenmitten), Lippen (Mund), Nasenspitze
#  3. Ähnlichkeitstransformation (Umeyama), dann ICP mit Maßstab auf den Hautflächen
#  4. Nicht-starres Anpassen: HG-Punkt -> nächster TRELLIS-Hautpunkt und TRELLIS-Hautpunkt -> nächster HG-Punkt
#     (zieht die Ohren zu den Spitzen), Augen- und Mundinneres ausgespart, Verschiebungsfeld über das Netz geglättet
#  5. Ergebnis als HG-Livekey (.npz: indices, relative_coordinates) relativ zur Grundform
import bpy, bmesh, sys, os, json
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

args = sys.argv[sys.argv.index("--") + 1:]
trellis, ziel = args[0], args[1]
bilder = args[2] if len(args) > 2 else None
P = json.loads(args[3]) if len(args) > 3 else {}
ctx = bpy.context


def log(*a):
    print("ELB", *a, flush=True)


body = bpy.data.objects["HG_Body"]
kb = body.data.shape_keys.key_blocks
N = len(body.data.vertices)


def co(key):
    a = np.empty(N * 3); key.data.foreach_get("co", a); return a.reshape(N, 3)


basis = co(kb["Basis"])
# HG: Basis ist die Grundform des Geschlechts der Datei, „Male“ (Wert 0/1) schaltet auf männlich
grund = basis.copy()
for g_ in ("Male", "Female"):
    if g_ in kb:
        grund += (co(kb[g_]) - basis) * kb[g_].value
mw = np.array(body.matrix_world)
grund_w = grund @ mw[:3, :3].T + mw[:3, 3]


def schwerpunkt(delta, maske=None):
    g = np.linalg.norm(delta, axis=1)
    if maske is not None:
        g = g * maske
    g = np.where(g > g.max() * 0.25, g, 0)
    return (grund_w * g[:, None]).sum(0) / g.sum()


d_bl = co(kb["eyeBlink_L"]) - basis
d_br = co(kb["eyeBlink_R"]) - basis
d_mu = co(kb["mouthFunnel"]) - basis
hg_aL, hg_aR, hg_mund = schwerpunkt(d_bl), schwerpunkt(d_br), schwerpunkt(d_mu)

# ---------------------------------------------------------------- TRELLIS-Form laden
bilder_vorher = set(bpy.data.images)
bpy.ops.import_scene.gltf(filepath=trellis)
bilder_neu = [i for i in bpy.data.images if i not in bilder_vorher]
tg = bpy.data.objects["gesicht"]
tn = len(tg.data.vertices)
tkb = tg.data.shape_keys.key_blocks


def tco(k):
    a = np.empty(tn * 3); k.data.foreach_get("co", a); return a.reshape(tn, 3)


tm = np.array(tg.matrix_world)
tb = tco(tkb["Basis"])
tw = tb @ tm[:3, :3].T + tm[:3, 3]
d_zu = (tco(tkb["augen_zu"]) - tb) @ tm[:3, :3].T
d_li = (tco(tkb["lippen_auf"]) - tb) @ tm[:3, :3].T


def t_schwer(delta, sel):
    g = np.linalg.norm(delta, axis=1) * sel
    g = np.where(g > g.max() * 0.25, g, 0)
    return (tw * g[:, None]).sum(0) / g.sum()


mx = np.median(tw[np.linalg.norm(d_zu, axis=1) > 0, 0]) if (np.linalg.norm(d_zu, axis=1) > 0).any() else 0
# HG: _L ist die linke Seite der Figur (x > 0); TRELLIS ebenso nach x getrennt
t_aL = t_schwer(d_zu, (tw[:, 0] > mx).astype(float))
t_aR = t_schwer(d_zu, (tw[:, 0] <= mx).astype(float))
if np.sign(hg_aL[0] - hg_aR[0]) != np.sign(t_aL[0] - t_aR[0]):
    t_aL, t_aR = t_aR, t_aL
t_mund = t_schwer(d_li, np.ones(tn))


def nasenspitze(pts, aL, aR, mund):
    mitte = (aL + aR) / 2
    achse_x = (aL - aR) / np.linalg.norm(aL - aR)
    sel = (np.abs((pts - mitte) @ achse_x) < 0.012)
    hoch = (pts[:, 2] < mitte[2] - 0.01) & (pts[:, 2] > mund[2] + 0.008)
    kand = pts[sel & hoch]
    return kand[np.argmin(kand[:, 1])]


hg_nase = nasenspitze(grund_w, hg_aL, hg_aR, hg_mund)
t_nase = nasenspitze(tw, t_aL, t_aR, t_mund)
log("HG Landmarken", np.round([hg_aL, hg_aR, hg_mund, hg_nase], 3).tolist())
log("TR Landmarken", np.round([t_aL, t_aR, t_mund, t_nase], 3).tolist())


def umeyama(src, dst):
    ms, md = src.mean(0), dst.mean(0)
    s, d = src - ms, dst - md
    U, S, Vt = np.linalg.svd(d.T @ s / len(src))
    D = np.eye(3)
    if np.linalg.det(U) * np.linalg.det(Vt) < 0:
        D[2, 2] = -1
    R = U @ D @ Vt
    c = np.trace(np.diag(S) @ D) / (s ** 2).sum(1).mean()
    return c, R, md - c * R @ ms


c, R, t = umeyama(np.array([t_aL, t_aR, t_mund, t_nase]), np.array([hg_aL, hg_aR, hg_mund, hg_nase]))
log("Maßstab", round(c, 3))
tw = (c * (R @ tw.T)).T + t

# ---------------------------------------------------------------- Haut der TRELLIS-Form (Haar ausschließen)
me = tg.data
img = None
for n_ in me.materials[0].node_tree.nodes:
    if n_.type == "TEX_IMAGE" and n_.image is not None and n_.image in bilder_neu:
        img = n_.image
if img is None:
    # Farbbild über den Namen (Atlas der Plan-D-Form)
    if bilder_neu:
        img = max((i for i in bilder_neu if i.size[0] > 0), key=lambda i: i.size[0] * i.size[1])
    else:
        # Plan-D-Form: Atlas liegt als <name>_atlas.jpg daneben
        img = bpy.data.images.load(trellis[:-4] + "_atlas.jpg")
log("Farbbild", img.name, tuple(img.size))
w_, h_ = img.size
px = np.empty(w_ * h_ * 4, np.float32); img.pixels.foreach_get(px); px = px.reshape(h_, w_, 4)[:, :, :3]
uv = np.empty(len(me.loops) * 2); me.uv_layers.active.data.foreach_get("uv", uv); uv = uv.reshape(-1, 2)
vi = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", vi)
x = np.clip((uv[:, 0] % 1) * (w_ - 1), 0, w_ - 1).astype(int); y = np.clip((uv[:, 1] % 1) * (h_ - 1), 0, h_ - 1).astype(int)
farbe = np.zeros((tn, 3)); farbe[vi] = px[y, x]
hell = farbe.mean(1)
rotanteil = farbe[:, 0] / (farbe.sum(1) + 1e-6)
haut_t = (hell > P.get("haut_hell", 0.28)) & (rotanteil > 0.36)
# nur Kopf (über dem Kinn bis Stirn, ohne Kleidung): oberhalb Mund - 6 cm
haut_t &= tw[:, 2] > hg_mund[2] - P.get("unter_mund", 0.07)
me.calc_loop_triangles()
ttri = np.array([lt.vertices[:] for lt in me.loop_triangles])
ttri_haut = ttri[haut_t[ttri].all(1)]
log("TRELLIS-Haut", int(haut_t.sum()), "von", tn, "Punkten")

# ---------------------------------------------------------------- HG-Netz: Nachbarn, Bereiche
body.data.calc_loop_triangles()
hg_tri = np.array([lt.vertices[:] for lt in body.data.loop_triangles])
kanten = np.array([e.vertices[:] for e in body.data.edges])
nb = [[] for _ in range(N)]
for a_, b_ in kanten:
    nb[a_].append(b_); nb[b_].append(a_)
nb_len = np.array([len(n) for n in nb])
nb_flat = np.concatenate([np.array(n, np.int64) for n in nb])
nb_start = np.concatenate([[0], np.cumsum(nb_len)[:-1]])


def nachbar_mittel(d):
    s = np.add.reduceat(d[nb_flat], nb_start, axis=0)
    return s / nb_len[:, None]


hals_z = hg_mund[2] - 0.075
kopf = grund_w[:, 2] > hals_z
aug_mitte = [hg_aL, hg_aR]
frei = np.zeros(N, bool)  # Augen- und Mundinneres: nur geglättet mitgeführt
for a_ in aug_mitte:
    frei |= np.linalg.norm(grund_w - a_, axis=1) < P.get("auge_r", 0.019)
frei |= np.linalg.norm((grund_w - hg_mund) * [1, 1, 1.6], axis=1) < P.get("mund_r", 0.026)
# Mundraum (HG-Gruppe „mouth_bag_VG_“) und alles hinter den Lippen nicht an die Außenhaut ziehen
beutel = np.zeros(N, bool)
if "mouth_bag_VG_" in body.vertex_groups:
    gi = body.vertex_groups["mouth_bag_VG_"].index
    for v in body.data.vertices:
        if any(g.group == gi and g.weight > 0.01 for g in v.groups):
            beutel[v.index] = True
frei |= beutel
frei |= (grund_w[:, 1] > hg_mund[1] + 0.004) & (np.abs(grund_w[:, 0] - hg_mund[0]) < 0.032) & (np.abs(grund_w[:, 2] - hg_mund[2]) < 0.03)
# Hinterkopf (unter dem Haar der TRELLIS-Form): weich auf null
fest_null = ~kopf

# Datenpunkte nur auf der Vorderseite (Gesicht, Stirn, Wangen) und an den Ohren; Unterkiefer/Hals werden mitgezogen
body.data.update()
nor_hg = np.array([v.normal[:] for v in body.data.vertices]) @ mw[:3, :3].T
vorn = (nor_hg[:, 1] < -0.15) & (grund_w[:, 2] > hg_mund[2] - P.get("kinn_unter_mund", 0.045))
aug_z = (hg_aL[2] + hg_aR[2]) / 2
# Ohr in HG: alle Punkte, die die Ohr-Regler bewegen
ohr_hg = np.zeros(N, bool)
for n_ in ("ear_height", "ear_width", "ear_turn", "ear_antihelix_shape", "ear_lobe_size"):
    d_ = np.load(os.path.expanduser(f"~/werkzeuge/humgen/inhalt/livekeys/face_proportions/ears/{n_}.npz"))
    flat = np.zeros(N * 3); flat[d_["indices"]] = d_["relative_coordinates"]
    ohr_hg |= np.abs(flat.reshape(N, 3)).max(1) > 2e-4
ohr_mitte = [grund_w[ohr_hg & (np.sign(grund_w[:, 0]) == s_)].mean(0) for s_ in (1, -1)]
# Ohr in TRELLIS: Haut im Umkreis der HG-Ohren, seitlich außerhalb des Schädels
ohr_t = np.zeros(tn, bool)
for m_ in ohr_mitte:
    ohr_t |= haut_t & (np.linalg.norm(tw - m_, axis=1) < 0.045) & (np.abs(tw[:, 0]) > np.abs(m_[0]) - 0.012)
# Unterkiefer seitlich nicht an die (schmalere, weiche) TRELLIS-Kieferlinie ziehen: dort läge die Haut sonst
# innerhalb des Mundraums, der bei HG darunter liegt
kiefer = (grund_w[:, 2] < hg_mund[2] + P.get("kiefer_ueber_mund", 0.01)) & (np.abs(grund_w[:, 0]) > P.get("kiefer_seitlich", 0.022))
daten = (vorn | ohr_hg) & kopf & ~frei
kiefer_gew = np.where(kiefer, P.get("kiefer_gewicht", 0.3), 1.0)   # Kieferlinie nur halbherzig anpassen
for seite in (1, -1):
    oh = ohr_hg & (np.sign(grund_w[:, 0]) == seite); ot = ohr_t & (np.sign(tw[:, 0]) == seite)
    log("Ohr", seite, "HG oben/außen/hinten", np.round([grund_w[oh, 2].max(), np.abs(grund_w[oh, 0]).max(), grund_w[oh, 1].max()], 3).tolist(),
        "TR", np.round([tw[ot, 2].max(), np.abs(tw[ot, 0]).max(), tw[ot, 1].max()], 3).tolist() if ot.any() else None)
log("Datenpunkte HG", int(daten.sum()), "Ohr HG", int(ohr_hg.sum()), "Ohr TRELLIS", int(ohr_t.sum()))
cur = grund_w.copy()
iters = P.get("runden", 12)
for it in range(iters):
    bvh_t = BVHTree.FromPolygons([Vector(p) for p in tw], ttri_haut.tolist())
    ziel_d = np.zeros((N, 3)); gew = np.zeros(N)
    grenze = 0.02 if it < 3 else 0.012
    # A: HG -> TRELLIS
    for i in np.nonzero(daten)[0]:
        loc, nor, fi, dd = bvh_t.find_nearest(Vector(cur[i]), grenze)
        if loc is not None:
            ziel_d[i] += (np.array(loc) - cur[i]); gew[i] += 1.0
    # B: TRELLIS -> HG (zieht z. B. Ohrspitzen heran)
    kd = KDTree(N)
    for i in np.nonzero(daten)[0]:
        kd.insert(Vector(cur[i]), int(i))
    kd.balance()
    kd_ohr = KDTree(int(ohr_hg.sum()))
    for i in np.nonzero(ohr_hg)[0]:
        kd_ohr.insert(Vector(cur[i]), int(i))
    kd_ohr.balance()
    for j in np.nonzero(ohr_t)[0]:
        # Ohren: ohne Abstandsgrenze, damit die Spitzen das HG-Ohr nach oben ziehen
        p_, i, dd = kd_ohr.find(Vector(tw[j]))
        ziel_d[i] += (tw[j] - cur[i]) * P.get("zug_ohr", 1.5); gew[i] += P.get("zug_ohr", 1.5)
    for j in np.nonzero(haut_t & ~ohr_t)[0][::2]:
        p_, i, dd = kd.find(Vector(tw[j]))
        if i is not None and dd < grenze * 1.6:
            ziel_d[i] += (tw[j] - cur[i]) * P.get("zug_b", 0.8); gew[i] += P.get("zug_b", 0.8)
    datum = np.zeros((N, 3)); hat = gew > 0
    datum[hat] = ziel_d[hat] / gew[hat][:, None]
    # Glätten: Daten-Term + Nachbarmittel (Jacobi), Hals/Rumpf bleibt fest
    lam = np.where(ohr_hg, P.get("glatt_ohr", 0.6), P.get("glatt", 3.0))[:, None]
    d = datum.copy()
    w_d = np.where(hat, 1.0, 0.0) * kiefer_gew
    for _ in range(P.get("glatt_schritte", 60)):
        m = nachbar_mittel(d)
        d = (w_d[:, None] * datum + lam * m) / (w_d[:, None] + lam)
        d[fest_null] = 0
    schritt = 0.6 if it < iters - 2 else 1.0
    cur = cur + d * schritt
    log("Runde", it, "mittlere Bewegung mm", round(float(np.linalg.norm(d[kopf], axis=1).mean() * 1000), 2),
        "Datenpunkte", int(hat.sum()))

# Abschluss: noch einmal glätten (nur leicht), Rumpf weich auf null
delta_w = cur - grund_w
# Ohrspitzen: oberster Hautpunkt der TRELLIS-Ohren -> oberster Punkt des HG-Ohrs, weich über das Ohr verteilt
# (nächster Punkt zieht die Spitze nicht weit genug heraus; das hier ist eine gezielte Landmarke)
for s_, m_ in zip((1, -1), ohr_mitte):
    kand = haut_t & (np.linalg.norm(tw - m_, axis=1) < P.get("ohr_such", 0.08)) & (np.abs(tw[:, 0]) > np.abs(m_[0]) - 0.005) & (tw[:, 2] > m_[2])
    if not kand.any():
        log("keine TRELLIS-Ohrspitze", s_); continue
    spitze_t = tw[kand][np.argmax(tw[kand][:, 2])]
    oh = np.nonzero(ohr_hg & (np.sign(grund_w[:, 0]) == s_))[0]
    jetzt = grund_w[oh] + delta_w[oh]
    oben = oh[np.argmax(jetzt[:, 2])]
    zug = spitze_t - (grund_w[oben] + delta_w[oben])
    # Spitze etwas weiter in Richtung Ohrmitte -> TRELLIS-Spitze verlängern (die TRELLIS-Form ist dort weich und rund)
    richt = (spitze_t - m_) / np.linalg.norm(spitze_t - m_)
    zug = zug + richt * P.get("ohr_verlaengern", 0.02)
    r = np.linalg.norm(grund_w[oh] - grund_w[oben], axis=1)
    f = np.clip(1 - r / P.get("ohr_radius", 0.026), 0, 1) ** 2
    delta_w[oh] += f[:, None] * zug * P.get("ohr_staerke", 1.0)
    log("Ohrspitze", s_, "TRELLIS", np.round(spitze_t, 3).tolist(), "Zug mm", np.round(zug * 1000, 1).tolist())

# Augen und Mund als Ganzes verschieben (Mittel des Rings drumherum): Lider und Lippen behalten ihre HG-Form,
# Augapfel (in hg_kopf.py ebenso als Mittel der Haut im Umkreis) und Lider bleiben zueinander passend
for mitte, r_in, r_aus, streck in [(hg_aL, 0.019, 0.03, [1, 1, 1]), (hg_aR, 0.019, 0.03, [1, 1, 1]), (hg_mund, 0.026, 0.038, [1, 1, 1.6])]:
    dist = np.linalg.norm((grund_w - mitte) * streck, axis=1)
    innen = dist < r_in
    # (Mundraum dahinter folgt dem geglätteten Feld wie Kinn und Hals, sonst sticht er unter dem Kinn durch)
    ring = (dist >= r_in) & (dist < r_aus)
    verschiebung = delta_w[ring].mean(0)
    # weicher Übergang am Rand des Inneren
    f = np.clip((dist - r_in * 0.7) / (r_in * 0.3), 0, 1)[:, None]
    delta_w[innen] = verschiebung * (1 - f[innen]) + delta_w[innen] * f[innen]
for _ in range(10):
    delta_w = 0.5 * delta_w + 0.5 * nachbar_mittel(delta_w)
    delta_w[fest_null] = 0
inv = np.linalg.inv(mw[:3, :3])
delta = delta_w @ inv.T
idx = np.nonzero(np.abs(delta.reshape(-1)) > 1e-6)[0]
np.savez(ziel, indices=idx, relative_coordinates=delta.reshape(-1)[idx])
log("gespeichert", ziel, "Punkte bewegt", int((np.abs(delta).max(1) > 1e-4).sum()), "max mm", round(float(np.abs(delta).max() * 1000), 1))
