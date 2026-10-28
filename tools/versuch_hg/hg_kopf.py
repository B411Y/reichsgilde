# Versuch HG-Köpfe, Stufe B: aus dem HG-Menschen (Stufe A) ein spielfertiges Brustbild machen.
# Aufruf: blender_hg.sh -b <mensch_a.blend> -P hg_kopf.py -- <kopf.json> <ziel.glb>
#
# Was passiert (alles per Skript, kein Handgriff):
#  1. Regler (HG-Livekeys aus .npz) als Formschlüssel anlegen, Geschlecht und Vorlage fest in die Grundform backen.
#  2. Unterkiefer-Zähne: die Kieferbewegung je Gesichtsform auswerten und als eigene Form speichern
#     (in HG folgt der Kieferknochen den Lippen über eine Bedingung, die Godot nicht kennt).
#  3. Haarkarten, Bart, Brauen, Wimpern an die Haut binden (nächster Punkt, baryzentrisch) und alle Formen übertragen.
#  4. Augen und Zähne folgen den Reglern als Verschiebung (Mittelwert der Haut um sie herum).
#  5. Zuschnitt auf das Brustbild, Rumpf wird zum Leinenkittel, neue UV nur für das Brustbild, Texturen backen.
#  6. Überflüssige Knochen weg, alles zu einem Netz „Kopf“ zusammenfügen, als GLB ausgeben.
# Die Einstellungen (kopf.json) nennen Regler, Formen, Zuschnitt und Texturgrößen; dazu schreibt das Skript
# eine Beschreibung <ziel>.json (Formgruppen, Wertebereiche) für das Porträtfenster.
import bpy, bmesh, sys, json, os, time, math
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

args = sys.argv[sys.argv.index("--") + 1:]
einst = json.load(open(args[0]))
ziel = args[1]
texdir = os.path.splitext(ziel)[0] + "_tex"
os.makedirs(texdir, exist_ok=True)
INHALT = os.path.expanduser("~/werkzeuge/humgen/inhalt")
ctx = bpy.context
t0 = time.time()


def log(*a):
    print("HG-KOPF", f"{time.time()-t0:6.1f}s", *a, flush=True)


rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
body = bpy.data.objects["HG_Body"]
eyes = bpy.data.objects["HG_Eyes"]
zl = bpy.data.objects["HG_TeethLower"]
zo = bpy.data.objects["HG_TeethUpper"]
haare = [o for o in bpy.data.objects if o.type == "MESH" and o.get("hg_haircard")]
for o in list(bpy.data.objects):
    if o.name in ("Cube", "Camera", "Light"):
        bpy.data.objects.remove(o)
for m in [m for m in body.modifiers if m.type == "PARTICLE_SYSTEM"]:
    body.modifiers.remove(m)
rig.data.pose_position = "REST"
log("Haare", [h.name for h in haare])

N = len(body.data.vertices)
kb = body.data.shape_keys.key_blocks


def co_von(key):
    a = np.empty(N * 3)
    key.data.foreach_get("co", a)
    return a.reshape(N, 3)


# ---------------------------------------------------------------- 1. Regler und Grundform
basis_alt = co_von(kb["Basis"])
neu = basis_alt.copy()
for name in ("Male", "Female", "LIVE_KEY_PERMANENT"):
    if name in kb:
        neu += (co_von(kb[name]) - basis_alt) * kb[name].value

regler = dict(einst["regler"])  # Name -> Pfad der .npz (relativ zum HG-Inhalt oder absolut)
regler.update(einst.get("regler_dazu", {}))   # z. B. Rassenformen wie „elb“
for name, pfad in regler.items():
    p = pfad if os.path.isabs(pfad) else os.path.join(INHALT, pfad)
    d = np.load(p)
    delta = np.zeros(N * 3)
    delta[d["indices"]] = d["relative_coordinates"]
    k = body.shape_key_add(name=name, from_mix=False)
    k.data.foreach_set("co", (basis_alt.reshape(-1) + delta))
log("Regler angelegt", len(regler))

body.data.shape_keys.animation_data_clear()
for k in list(kb):
    if k.name in ("Male", "Female", "LIVE_KEY_PERMANENT") or k.name.startswith(("LIVE_KEY_TEMP", "cor_")):
        body.shape_key_remove(k)
kb = body.data.shape_keys.key_blocks
for k in kb:
    if k.name == "Basis":
        continue
    c = co_von(k) - basis_alt + neu
    k.data.foreach_set("co", c.reshape(-1))
    k.value = 0.0
    k.slider_min, k.slider_max = -2.0, 2.0
kb["Basis"].data.foreach_set("co", neu.reshape(-1))
body.data.vertices.foreach_set("co", neu.reshape(-1))
body.data.update()
rest = neu
deltas = {k.name: co_von(k) - rest for k in kb if k.name != "Basis"}
gesicht = [n for n in deltas if n not in regler]
log("Formen am Körper", len(deltas), "davon Gesicht", len(gesicht))

# ---------------------------------------------------------------- 2. Unterkiefer folgt den Gesichtsformen
dg = ctx.evaluated_depsgraph_get()


def eval_co(obj):
    e = obj.evaluated_get(dg)
    m = e.to_mesh()
    a = np.empty(len(m.vertices) * 3)
    m.vertices.foreach_get("co", a)
    e.to_mesh_clear()
    return a.reshape(-1, 3)


# Pose aktiv, damit der Kiefer seiner Bedingung folgt (keine Pose gesetzt -> Ruhelage)
rig.data.pose_position = "POSE"
for pb in rig.pose.bones:
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
for k in kb:
    k.value = 0.0
ctx.view_layer.update(); dg = ctx.evaluated_depsgraph_get()
zl_rest = eval_co(zl)
if not zl.data.shape_keys:
    zl.shape_key_add(name="Basis")
zl.data.shape_keys.animation_data_clear()
zahn_n = 0
for name in gesicht:
    kb[name].value = 1.0
    ctx.view_layer.update(); dg = ctx.evaluated_depsgraph_get()
    d = eval_co(zl) - zl_rest
    kb[name].value = 0.0
    if np.abs(d).max() > 2e-4:
        k = zl.shape_key_add(name=name, from_mix=False)
        base = np.empty(len(zl.data.vertices) * 3); zl.data.shape_keys.key_blocks["Basis"].data.foreach_get("co", base)
        # Auswertung liegt im Objektraum inkl. Armatur (Ruhelage = Grundform), daher Differenz direkt addieren
        k.data.foreach_set("co", base + d.reshape(-1))
        zahn_n += 1
ctx.view_layer.update()
rig.data.pose_position = "REST"
log("Unterkiefer-Formen", zahn_n)

# ---------------------------------------------------------------- 3. Haare an die Haut binden
mw = body.matrix_world
body_w = (np.c_[rest, np.ones(N)] @ np.array(mw).T)[:, :3]
body.data.calc_loop_triangles()
tris = np.array([lt.vertices[:] for lt in body.data.loop_triangles])
bvh = BVHTree.FromPolygons([Vector(v) for v in body_w], tris.tolist())


def binden(obj):
    """Je Haar-Eckpunkt: Dreieck der Haut, baryzentrische Gewichte, Abstand."""
    m = obj.matrix_world
    pts = [m @ v.co for v in obj.data.vertices]
    idx = np.zeros((len(pts), 3), int); w = np.zeros((len(pts), 3)); dist = np.zeros(len(pts))
    for i, p in enumerate(pts):
        loc, nor, fi, dd = bvh.find_nearest(p)
        a, b, c = (Vector(body_w[j]) for j in tris[fi])
        # baryzentrische Koordinaten des Fußpunkts
        v0, v1, v2 = b - a, c - a, loc - a
        d00, d01, d11 = v0.dot(v0), v0.dot(v1), v1.dot(v1)
        d20, d21 = v2.dot(v0), v2.dot(v1)
        den = d00 * d11 - d01 * d01 or 1e-12
        bv = (d11 * d20 - d01 * d21) / den; cv = (d00 * d21 - d01 * d20) / den
        idx[i] = tris[fi]; w[i] = (1 - bv - cv, bv, cv); dist[i] = dd
    return idx, w, dist


def formen_uebertragen(obj, idx, w, dist, weich_ab=0.025, weich_bis=0.06):
    """Formen der Haut auf ein gebundenes Objekt übertragen (Weltraum -> Objektraum)."""
    inv = np.array(obj.matrix_world.inverted().to_3x3())
    if not obj.data.shape_keys:
        obj.shape_key_add(name="Basis")
    base = np.empty(len(obj.data.vertices) * 3); obj.data.vertices.foreach_get("co", base)
    # Gesichtsformen klingen mit dem Abstand zur Haut ab (lange Haare sollen beim Lächeln nicht wackeln)
    abkl = np.clip(1 - (dist - weich_ab) / (weich_bis - weich_ab), 0, 1)[:, None]
    mw3 = np.array(mw.to_3x3())
    for name, d in deltas.items():
        dw = d @ mw3.T
        hd = (dw[idx] * w[:, :, None]).sum(1)
        if name not in regler:
            hd = hd * abkl
        if np.abs(hd).max() < 1e-5:
            continue
        k = obj.shape_key_add(name=name, from_mix=False)
        k.data.foreach_set("co", base + (hd @ inv.T).reshape(-1))


kappen = []
for o in list(haare):
    if "Eyelashes" in o.name:
        continue
    k_idx = [i for i, m in enumerate(o.data.materials) if "Haircap" in m.name]
    if not k_idx:
        continue
    if len(k_idx) == len(o.data.materials):
        haare.remove(o); kappen.append(o)
        continue
    # gemischtes Objekt (Bart: Kappe + Karten) teilen
    kp = o.copy(); kp.data = o.data.copy(); kp.name = o.name + "_kappe"; ctx.collection.objects.link(kp)
    for ob, behalte in ((kp, True), (o, False)):
        bm = bmesh.new(); bm.from_mesh(ob.data)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if (f.material_index in k_idx) != behalte], context="FACES")
        bm.to_mesh(ob.data); bm.free()
    kappen.append(kp)
log("Kappen auf die Haut", [k.name for k in kappen], "Haare", [h.name for h in haare])

# Wimpern ausdünnen (HG legt 768 Karten an; im Porträt reicht gut die Hälfte)
for o in haare:
    if "Eyelashes" in o.name and einst.get("wimpern_anteil", 1.0) < 1.0:
        bm_ = bmesh.new(); bm_.from_mesh(o.data)
        inseln, gesehen = [], set()
        for f_ in bm_.faces:
            if f_ in gesehen:
                continue
            stapel, insel = [f_], []
            while stapel:
                x = stapel.pop()
                if x in gesehen:
                    continue
                gesehen.add(x); insel.append(x)
                for e in x.edges:
                    stapel.extend(g for g in e.link_faces if g not in gesehen)
            inseln.append(insel)
        schritt = 1.0 / max(1e-3, 1.0 - einst["wimpern_anteil"])
        weg = [f_ for k_, insel in enumerate(inseln) if (k_ % round(schritt)) == 0 for f_ in insel]
        bmesh.ops.delete(bm_, geom=weg, context="FACES")
        bm_.to_mesh(o.data); bm_.free()
        log("Wimpern ausgedünnt", len(inseln), "Karten,", len(weg), "Flächen weg")

# Ohrpunkte (für das Ausdünnen des Haars an Elbenohren): alles, was die HG-Ohrregler bewegen
ohr_pkt = np.zeros(N, bool)
for n_ in ("ear_height", "ear_width", "ear_turn"):
    d_ = np.load(os.path.join(INHALT, f"livekeys/face_proportions/ears/{n_}.npz"))
    flat = np.zeros(N * 3); flat[d_["indices"]] = d_["relative_coordinates"]
    ohr_pkt |= np.abs(flat.reshape(N, 3)).max(1) > 2e-4


def haar_an_ohren_ausduennen(obj, dist, form="elb", abstand=0.014):
    """In der Form „elb“ Haarkarten, die das Elbenohr durchstechen würde, auf ihre Wurzel zusammenziehen."""
    if form not in deltas or not obj.data.shape_keys or form not in obj.data.shape_keys.key_blocks:
        return
    n = len(obj.data.vertices)
    base = np.empty(n * 3); obj.data.vertices.foreach_get("co", base); base = base.reshape(n, 3)
    k = obj.data.shape_keys.key_blocks[form]
    c = np.empty(n * 3); k.data.foreach_get("co", c); c = c.reshape(n, 3)
    M = np.array(obj.matrix_world)
    welt = (np.c_[c, np.ones(n)] @ M.T)[:, :3]
    ohr_w = ((rest + deltas[form]) @ np.array(mw)[:3, :3].T + np.array(mw)[:3, 3])[ohr_pkt]
    # nur der Teil des Ohrs, der sich in der Form bewegt (das Elbenohr selbst)
    ohr_w = ohr_w[np.linalg.norm(deltas[form][ohr_pkt], axis=1) > 0.004]
    if len(ohr_w) == 0:
        return
    from mathutils.kdtree import KDTree as _KD
    kd_ = _KD(len(ohr_w))
    for j, p_ in enumerate(ohr_w):
        kd_.insert(Vector(p_), j)
    kd_.balance()
    nah = np.array([kd_.find(Vector(p_))[2] < abstand for p_ in welt])
    if not nah.any():
        return
    # Karten = zusammenhängende Teile; eine berührte Karte fällt ganz auf ihren hautnächsten Punkt zusammen
    bm_ = bmesh.new(); bm_.from_mesh(obj.data); bm_.verts.ensure_lookup_table()
    teil = -np.ones(n, int); t_ = 0
    for v in bm_.verts:
        if teil[v.index] >= 0:
            continue
        stapel = [v]
        while stapel:
            x = stapel.pop()
            if teil[x.index] >= 0:
                continue
            teil[x.index] = t_
            stapel.extend(e.other_vert(x) for e in x.link_edges if teil[e.other_vert(x).index] < 0)
        t_ += 1
    bm_.free()
    karten = t_
    # Karten, die das Elbenohr berührt, hinter das Ohr streichen (wie hinters Ohr geklemmtes Haar):
    # ganze Karte nach hinten, je weiter vom Ansatz desto mehr; Wurzel bleibt, damit keine kahle Stelle entsteht
    weg = np.unique(teil[nah])
    inv3 = np.linalg.inv(M[:3, :3])
    for t_ in weg:
        ids = np.nonzero(teil == t_)[0]
        f_ = np.clip(dist[ids] / 0.02, 0, 1)[:, None]
        schub = np.array([0.0, einst.get("ohr_haar_nach_hinten", 0.028), -0.004])   # Blender: +y = hinten
        c[ids] += f_ * (schub @ inv3.T)
    k.data.foreach_set("co", c.reshape(-1))
    log("Elbenohr: Haarkarten hinters Ohr gestrichen", obj.name, len(weg), "von", karten)


for o in haare:
    i, w, d = binden(o)
    formen_uebertragen(o, i, w, d)
    if "Eyelashes" not in o.name:
        haar_an_ohren_ausduennen(o, d)
    # Zweite UV „Wurzel“: Abstand zur Haut (0 = Haaransatz, 1 = ab 3 cm) für den dunkleren Ansatz im Spiel-Material
    o["wurzel"] = np.clip(d / einst.get("wurzel_laenge", 0.03), 0, 1).tolist()
    log("gebunden", o.name, len(o.data.vertices), "Abstand max", round(float(d.max()), 3))

# ---------------------------------------------------------------- 4. Augen und Zähne folgen den Reglern


def verschieben_mit(obj, gruppen, radius):
    """Regler als starre Verschiebung: Mittel der Hautbewegung im Umkreis der Gruppe."""
    if not obj.data.shape_keys:
        obj.shape_key_add(name="Basis")
    n = len(obj.data.vertices)
    base = np.empty(n * 3); obj.data.vertices.foreach_get("co", base); base = base.reshape(n, 3)
    welt = (np.c_[base, np.ones(n)] @ np.array(obj.matrix_world).T)[:, :3]
    teile = []
    for g in gruppen:
        if g is None:
            sel = np.ones(n, bool)
        else:
            gi = obj.vertex_groups[g].index
            sel = np.array([any(x.group == gi and x.weight > 0.5 for x in v.groups) for v in obj.data.vertices])
        mitte = welt[sel].mean(0)
        nah = np.linalg.norm(body_w - mitte, axis=1) < radius
        teile.append((sel, nah))
    inv = np.array(obj.matrix_world.inverted().to_3x3()); mw3 = np.array(mw.to_3x3())
    for name in regler:
        dw = deltas[name] @ mw3.T
        neu_co = base.copy()
        for sel, nah in teile:
            neu_co[sel] += (dw[nah].mean(0) @ inv.T)
        k = obj.shape_key_add(name=name, from_mix=False)
        k.data.foreach_set("co", neu_co.reshape(-1))


eyes.data.shape_keys and [eyes.shape_key_remove(k) for k in list(eyes.data.shape_keys.key_blocks)[::-1]]
# Augapfel genau wie die Augenhöhle (Haut innerhalb 1,3 cm um die Augenmitte), sonst sticht sie durch die Pupille
verschieben_mit(eyes, ["eyeball.L", "eyeball.R"], einst.get("augen_radius", 0.013))
# Zähne und Zunge: Regler über den nächsten Hautpunkt (Mundraum) – starr verschoben stäche die Zunge bei
# schmaleren Kiefern (Elb) unter dem Kinn durch
for o in (zo, zl):
    i_, w_, d_ = binden(o)
    inv = np.array(o.matrix_world.inverted().to_3x3()); mw3 = np.array(mw.to_3x3())
    if not o.data.shape_keys:
        o.shape_key_add(name="Basis")
    nb_ = len(o.data.vertices); base = np.empty(nb_ * 3); o.data.vertices.foreach_get("co", base)
    for name in regler:
        dw = deltas[name] @ mw3.T
        hd = (dw[i_] * w_[:, :, None]).sum(1)
        k = o.shape_key_add(name=name, from_mix=False)
        k.data.foreach_set("co", base + (hd @ inv.T).reshape(-1))
log("Augen und Zähne folgen den Reglern")

# ---------------------------------------------------------------- 5. Zuschnitt, Kittel, UV, Backen
kopf_b = rig.data.bones["head"].head_local
hals_b = rig.data.bones["neck"].head_local
z_schnitt = hals_b.z - einst.get("schnitt_unter_hals", 0.20)
x_schnitt = einst.get("schnitt_breite", 0.26)
z_kragen = hals_b.z - einst.get("kragen_unter_hals", 0.035)

bm = bmesh.new(); bm.from_mesh(body.data)
weg = [v for v in bm.verts if v.co.z < z_schnitt or abs(v.co.x) > x_schnitt]
bmesh.ops.delete(bm, geom=weg, context="VERTS")
bm.to_mesh(body.data); bm.free()
log("zugeschnitten auf", len(body.data.vertices), "Punkte")

# Kittel: Flächen unter dem Kragen bekommen das Leinen und rücken etwas nach außen
mat_k = bpy.data.materials.new("Kittel"); mat_k.use_nodes = True
nt = mat_k.node_tree; pr = nt.nodes["Principled BSDF"]
tc = nt.nodes.new("ShaderNodeTexCoord")
wave1 = nt.nodes.new("ShaderNodeTexWave"); wave1.wave_type = "BANDS"; wave1.bands_direction = "X"; wave1.inputs["Scale"].default_value = 260; wave1.inputs["Distortion"].default_value = 3
wave2 = nt.nodes.new("ShaderNodeTexWave"); wave2.wave_type = "BANDS"; wave2.bands_direction = "Z"; wave2.inputs["Scale"].default_value = 260; wave2.inputs["Distortion"].default_value = 3
noise = nt.nodes.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 18; noise.inputs["Detail"].default_value = 8
for n_ in (wave1, wave2, noise):
    nt.links.new(tc.outputs["Object"], n_.inputs["Vector"])
mix = nt.nodes.new("ShaderNodeMath"); mix.operation = "MULTIPLY"
nt.links.new(wave1.outputs["Fac"], mix.inputs[0]); nt.links.new(wave2.outputs["Fac"], mix.inputs[1])
ramp = nt.nodes.new("ShaderNodeValToRGB")
farbe = einst.get("kittel_farbe", [0.10, 0.08, 0.055])  # linear
ramp.color_ramp.elements[0].color = (farbe[0] * 0.55, farbe[1] * 0.55, farbe[2] * 0.55, 1)
ramp.color_ramp.elements[1].color = (farbe[0], farbe[1], farbe[2], 1)
add = nt.nodes.new("ShaderNodeMath"); add.operation = "ADD"
nt.links.new(noise.outputs["Fac"], add.inputs[0]); nt.links.new(mix.outputs[0], add.inputs[1])
mul = nt.nodes.new("ShaderNodeMath"); mul.operation = "MULTIPLY"; mul.inputs[1].default_value = 0.7
nt.links.new(add.outputs[0], mul.inputs[0]); nt.links.new(mul.outputs[0], ramp.inputs[0])
nt.links.new(ramp.outputs[0], pr.inputs["Base Color"])
rv = nt.nodes.new("ShaderNodeValue"); rv.outputs[0].default_value = 0.92
nt.links.new(rv.outputs[0], pr.inputs["Roughness"])
bump = nt.nodes.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = 0.35; bump.inputs["Distance"].default_value = 0.002
nt.links.new(mix.outputs[0], bump.inputs["Height"]); nt.links.new(bump.outputs[0], pr.inputs["Normal"])
# Kittel als eigene Hülle: Kopie des Rumpfs, sauber an einer schrägen Ebene (Halsausschnitt, vorn tiefer) geschnitten,
# etwas nach außen gerückt, mit schmalem Saum. Die Haut darunter fällt weg (spart Dreiecke, nichts sticht durch).
neig = einst.get("ausschnitt_neigung", 0.6)        # z steigt nach hinten (HG: vorn = -y)
ebene_n = Vector((0, -neig, 1)).normalized()
ebene_p = Vector((0, 0, z_kragen))


huelle = body.copy(); huelle.data = body.data.copy(); huelle.name = "Kittel"
ctx.collection.objects.link(huelle)
huelle.shape_key_clear()
bm = bmesh.new(); bm.from_mesh(huelle.data)
r = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=ebene_p, plane_no=ebene_n, clear_outer=True)
bm.normal_update()
abst = einst.get("kittel_abstand", 0.007)
for v in bm.verts:
    v.co += v.normal * abst
bm.to_mesh(huelle.data); bm.free()
huelle.data.materials.clear(); huelle.data.materials.append(mat_k)
for l in huelle.data.uv_layers[0].data:
    l.uv = (l.uv[0] + 2.0, l.uv[1])
# Kittel ist fast eben: eigenständig grob vereinfachen (hat noch keine Formen, kein Saum zur Haut)
if einst.get("kittel_dezimieren", 1.0) < 1.0:
    m_ = huelle.modifiers.new("dez", "DECIMATE"); m_.ratio = einst["kittel_dezimieren"]
    with ctx.temp_override(object=huelle, active_object=huelle):
        bpy.ops.object.modifier_apply(modifier=m_.name)
bm = bmesh.new(); bm.from_mesh(body.data)
unter = [v for v in bm.verts if (v.co - ebene_p).dot(ebene_n) < -0.012]
bmesh.ops.delete(bm, geom=unter, context="VERTS")
bm.to_mesh(body.data); bm.free()
body.data.materials.append(mat_k)
with ctx.temp_override(active_object=body, selected_editable_objects=[body, huelle], selected_objects=[body, huelle]):
    bpy.ops.object.join()
log("Kittel", len(body.data.vertices), "Punkte")
# Kittelflächen merken (für UV2.y: das Spiel-Material tönt nur die Haut, nicht das Leinen)
kittel_mat = len(body.data.materials) - 1
body.data.attributes.new("kittel", "INT", "FACE")
body.data.attributes["kittel"].data.foreach_set("value", [1 if p_.material_index == kittel_mat else 0 for p_ in body.data.polygons])

# Verschlanken: Rumpf, Kittel, Hinterkopf (unter dem Haar) und Ohren dürfen gröber werden, das Gesicht bleibt genau.
# Die Formen werden danach über den nächsten alten Punkt zurückgeholt (im Gesicht sind das dieselben Punkte).
gesicht_m = Vector((0, rest[:, 1].min() + 0.06, (rig.data.bones["eyeball.L"].head_local.z + kopf_b.z) * 0.5))
# Ohren auch genau lassen (Elbenohr wird sonst kantig): Punkte, die die HG-Ohrregler bewegen
ohr_schutz = np.zeros(len(body.data.vertices), bool)
for n_ in ("ear_height", "ear_width", "ear_turn", "ear_antihelix_shape"):
    d_ = np.load(os.path.join(INHALT, f"livekeys/face_proportions/ears/{n_}.npz"))
    flat = np.zeros(N * 3); flat[d_["indices"]] = d_["relative_coordinates"]
    bewegt = np.abs(flat.reshape(N, 3)).max(1) > 2e-4
    # Zuschnitt hat die Punktnummern verändert: über die Lage zuordnen
    from mathutils.kdtree import KDTree as _KD
    kd_o = _KD(int(bewegt.sum()))
    for k_, p_ in enumerate(rest[bewegt]):
        kd_o.insert(Vector(p_), k_)
    kd_o.balance()
    for v in body.data.vertices:
        if kd_o.find(v.co)[2] < 1e-5:
            ohr_schutz[v.index] = True
# Kittel ist schon für sich vereinfacht: nicht noch einmal
if "kittel" in body.data.attributes:
    kf_ = np.empty(len(body.data.polygons), np.int64); body.data.attributes["kittel"].data.foreach_get("value", kf_)
    for p_, k_ in zip(body.data.polygons, kf_):
        if k_:
            for vi_ in p_.vertices:
                ohr_schutz[vi_] = True
grp = body.vertex_groups.new(name="Sparen")
for v in body.data.vertices:
    d = (v.co - gesicht_m).length
    w = 0.0 if ohr_schutz[v.index] else min(1.0, max(0.0, (d - einst.get("gesicht_radius", 0.10)) / 0.03))
    if w > 0:
        grp.add([v.index], w, "REPLACE")


def dezimieren_mit_formen_gruppe(obj, verh, gruppe):
    n = len(obj.data.vertices)
    base = np.empty(n * 3); obj.data.vertices.foreach_get("co", base); base = base.reshape(n, 3)
    alt = {}
    for k in obj.data.shape_keys.key_blocks[1:]:
        c = np.empty(n * 3); k.data.foreach_get("co", c); alt[k.name] = c.reshape(n, 3) - base
    obj.shape_key_clear()
    m = obj.modifiers.new("dez", "DECIMATE"); m.ratio = verh; m.vertex_group = gruppe; m.vertex_group_factor = 1.0
    m.use_symmetry = True; m.symmetry_axis = "X"
    with ctx.temp_override(object=obj, active_object=obj):
        bpy.ops.object.modifier_apply(modifier=m.name)
    from mathutils.kdtree import KDTree
    kd = KDTree(n)
    for i, v in enumerate(base):
        kd.insert(Vector(v), i)
    kd.balance()
    nn = len(obj.data.vertices)
    nc = np.empty(nn * 3); obj.data.vertices.foreach_get("co", nc); nc = nc.reshape(nn, 3)
    nachbar = np.array([kd.find(Vector(p))[1] for p in nc])
    obj.shape_key_add(name="Basis")
    for name, d in alt.items():
        k = obj.shape_key_add(name=name, from_mix=False)
        k.data.foreach_set("co", (nc + d[nachbar]).reshape(-1))


v_vor = len(body.data.vertices)
dezimieren_mit_formen_gruppe(body, einst.get("koerper_dezimieren", 0.35), "Sparen")
body.vertex_groups.remove(body.vertex_groups["Sparen"])
log("Körper verschlankt", v_vor, "->", len(body.data.vertices))

# Neue UV nur für das Brustbild: Inseln der HG-UV übernehmen und auf die volle Fläche packen
uv_alt = body.data.uv_layers[0]
uv_neu = body.data.uv_layers.new(name="Spiel")
for i, l in enumerate(uv_neu.data):
    l.uv = uv_alt.data[i].uv
body.data.uv_layers.active = uv_neu
uv_alt.active_render = True
ctx.view_layer.objects.active = body
for o in ctx.selected_objects:
    o.select_set(False)
body.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.select_all(action="SELECT")
bpy.ops.uv.pack_islands(rotate=False, scale=True, margin=0.004)
bpy.ops.object.mode_set(mode="OBJECT")
log("UV gepackt")

# Cycles für das Backen
sc = ctx.scene
sc.render.engine = "CYCLES"
try:
    cp = ctx.preferences.addons["cycles"].preferences
    cp.compute_device_type = "CUDA"; cp.get_devices()
    for d in cp.devices:
        d.use = d.type == "CUDA"
    sc.cycles.device = "GPU"
except Exception as e:
    log("CPU-Backen", e)
sc.cycles.samples = 4
sc.render.bake.margin = 8


def quelle(mat, eingang):
    pr = next(n for n in mat.node_tree.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled")
    inp = pr.inputs[eingang]
    if inp.is_linked:
        return inp.links[0].from_socket
    return None


def backen(obj, mats, art, groesse, name, format_="JPEG", alpha=False, nach=None):
    """Ein Kanal (Base Color/Roughness/Alpha/Normal) aller genannten Materialien in ein Bild."""
    datei = os.path.join(texdir, name + (".jpg" if format_ == "JPEG" else ".png"))
    if os.path.exists(datei) and not einst.get("neu_backen", True):
        # Zum schnellen Ausprobieren: schon gebackenes Bild wiederverwenden (gleiche UV vorausgesetzt)
        img = bpy.data.images.load(datei)
        if art in ("Normal", "Roughness", "Alpha"):
            img.colorspace_settings.name = "Non-Color"
        if alpha:
            img.alpha_mode = "STRAIGHT"
        return img
    img = bpy.data.images.new(name, groesse, groesse, alpha=alpha)
    if art == "Normal":
        img.colorspace_settings.name = "Non-Color"
    if art in ("Roughness", "Alpha"):
        img.colorspace_settings.name = "Non-Color"
    alt = []
    for mat in mats:
        nt = mat.node_tree
        out = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeOutputMaterial" and n.is_active_output)
        vorher = out.inputs[0].links[0].from_socket if out.inputs[0].is_linked else None
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img
        for n_ in nt.nodes:
            n_.select = False
        tn.select = True; nt.nodes.active = tn
        em = None
        if art != "Normal":
            pr = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled")
            em = nt.nodes.new("ShaderNodeEmission")
            src = quelle(mat, art)
            if src is not None:
                nt.links.new(src, em.inputs[0])
            else:
                v = pr.inputs[art].default_value
                em.inputs[0].default_value = (v[0], v[1], v[2], 1) if hasattr(v, "__len__") else (v, v, v, 1)
            nt.links.new(em.outputs[0], out.inputs[0])
        else:
            pr = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled")
            nt.links.new(pr.outputs[0], out.inputs[0])
        alt.append((mat, tn, em, out, vorher))
    for o in ctx.selected_objects:
        o.select_set(False)
    objs = obj if isinstance(obj, list) else [obj]
    for o in objs:
        o.select_set(True)
    ctx.view_layer.objects.active = objs[0]
    bpy.ops.object.bake(type="NORMAL" if art == "Normal" else "EMIT")
    for mat, tn, em, out, vorher in alt:
        nt = mat.node_tree
        nt.nodes.remove(tn)
        if em:
            nt.nodes.remove(em)
        if vorher is not None:
            nt.links.new(vorher, out.inputs[0])
    if nach:
        px = np.empty(groesse * groesse * 4, np.float32); img.pixels.foreach_get(px)
        img.pixels.foreach_set(nach(px))
    img.filepath_raw = os.path.join(texdir, name + (".jpg" if format_ == "JPEG" else ".png"))
    img.file_format = format_
    img.save()
    return img


def kappen_backen(ziel, kappen, ziel_mats, groesse, art):
    """Haarkappen (Bartschatten, Brauen) von außen auf die Haut backen (ausgewählt -> aktiv), Kanal Farbe oder Alpha."""
    img = bpy.data.images.new("kappe_" + art, groesse, groesse, alpha=False)
    img.colorspace_settings.name = "Non-Color" if art == "Alpha" else "sRGB"
    weg = []
    for mat in ziel_mats:
        nt = mat.node_tree
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img
        for n_ in nt.nodes:
            n_.select = False
        tn.select = True; nt.nodes.active = tn
        weg.append((nt, tn, None, None))
    for o in kappen:
        for mat in o.data.materials:
            nt = mat.node_tree
            out = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeOutputMaterial" and n.is_active_output)
            vorher = out.inputs[0].links[0].from_socket if out.inputs[0].is_linked else None
            em = nt.nodes.new("ShaderNodeEmission")
            src = quelle(mat, art)
            if src is not None:
                nt.links.new(src, em.inputs[0])
            nt.links.new(em.outputs[0], out.inputs[0])
            weg.append((nt, em, out, vorher))
    for o in ctx.selected_objects:
        o.select_set(False)
    for o in kappen:
        o.select_set(True)
    ziel.select_set(True); ctx.view_layer.objects.active = ziel
    bk = sc.render.bake
    bk.use_selected_to_active = True; bk.cage_extrusion = 0.006; bk.max_ray_distance = 0.02
    bpy.ops.object.bake(type="EMIT")
    bk.use_selected_to_active = False
    for nt, n_, out, vorher in weg:
        nt.nodes.remove(n_)
        if out is not None and vorher is not None:
            nt.links.new(vorher, out.inputs[0])
    px = np.empty(groesse * groesse * 4, np.float32); img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(-1, 4)


def spielmaterial(name, farbe=None, normal=None, rauh=None, alpha=None, rauh_wert=0.6, blend=False):
    """Schlichtes Principled-Material aus gebackenen Bildern (so versteht es der glTF-Export)."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; pr = nt.nodes["Principled BSDF"]
    def bild(img, x, y):
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = img; t.location = (x, y); return t
    if farbe:
        t = bild(farbe, -600, 300); nt.links.new(t.outputs[0], pr.inputs["Base Color"])
        if alpha is not None and alpha is farbe:
            nt.links.new(t.outputs[1], pr.inputs["Alpha"])
    if rauh:
        t = bild(rauh, -600, 0); nt.links.new(t.outputs[0], pr.inputs["Roughness"])
    else:
        pr.inputs["Roughness"].default_value = rauh_wert
    if normal:
        t = bild(normal, -900, -300); nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(t.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], pr.inputs["Normal"])
    pr.inputs["Specular IOR Level"].default_value = 0.4
    if blend:
        m.surface_render_method = "DITHERED"
        m.blend_method = "HASHED" if hasattr(m, "blend_method") else None
    return m


R = einst.get("texturen", {})
haut_mats = [body.data.materials[0], mat_k]
r_haut = int(R.get("haut", 2048))

# Haaransatz: Haut unter Haar- und Bartwurzeln in Haarfarbe tönen (sonst schimmert zwischen den Karten nackte Kopfhaut)
wurzeln = []
for o in haare + kappen:
    if "Eyelashes" in o.name:
        continue
    for v in o.data.vertices:
        p_ = o.matrix_world @ v.co
        if o in kappen or bvh.find_nearest(p_)[3] < 0.008:
            wurzeln.append(p_)
from mathutils.kdtree import KDTree
kd = KDTree(len(wurzeln))
for i, p_ in enumerate(wurzeln):
    kd.insert(p_, i)
kd.balance()
reich = einst.get("ansatz_weite", 0.012)
maske = body.data.color_attributes.new("Maske", "FLOAT_COLOR", "POINT")
for v in body.data.vertices:
    _, _, d = kd.find(body.matrix_world @ v.co)
    m_ = max(0.0, 1.0 - d / reich) ** 0.7
    maske.data[v.index].color = (m_, m_, m_, 1)
hm = body.data.materials[0]; nt = hm.node_tree
prh = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled")
src = prh.inputs["Base Color"].links[0].from_socket
ca = nt.nodes.new("ShaderNodeVertexColor"); ca.layer_name = "Maske"
fk = nt.nodes.new("ShaderNodeMath"); fk.operation = "MULTIPLY"; fk.inputs[1].default_value = einst.get("ansatz_staerke", 0.8)
nt.links.new(ca.outputs["Color"], fk.inputs[0])
mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = "RGBA"
nt.links.new(fk.outputs[0], mx.inputs["Factor"]); nt.links.new(src, mx.inputs["A"])
hf = einst.get("ansatz_farbe", [0.035, 0.022, 0.013])
mx.inputs["B"].default_value = (hf[0], hf[1], hf[2], 1)
nt.links.new(mx.outputs["Result"], prh.inputs["Base Color"])

def kappen_einmischen(px):
    if not kappen:
        return px
    far = kappen_backen(body, kappen, haut_mats, r_haut, "Base Color")
    alp = kappen_backen(body, kappen, haut_mats, r_haut, "Alpha")[:, :1]
    alp = np.clip(alp * einst.get("kappen_deckung", 0.85), 0, 1)
    px = px.reshape(-1, 4).copy()
    px[:, :3] = px[:, :3] * (1 - alp) + far[:, :3] * alp
    return px.reshape(-1)

h_far = backen(body, haut_mats, "Base Color", r_haut, "haut_farbe", nach=kappen_einmischen)
# Hautvarianten (anderes HG-Hautbild auf dieselbe UV, z. B. mittel/dunkel): nur die Farbe, als eigene Bilder neben dem GLB
farb_knoten = next((n for n in body.data.materials[0].node_tree.nodes
                    if n.bl_idname == "ShaderNodeTexImage" and n.name == "Color" and n.image), None)
for var_name, var_pfad in einst.get("haut_varianten", {}).items():
    if farb_knoten is None:
        log("kein Hautbild-Knoten für Varianten"); break
    alt_bild = farb_knoten.image
    farb_knoten.image = bpy.data.images.load(os.path.join(INHALT, var_pfad), check_existing=True)
    backen(body, haut_mats, "Base Color", r_haut, "haut_farbe_" + var_name, nach=kappen_einmischen)
    farb_knoten.image = alt_bild
    log("Hautvariante", var_name, var_pfad)
# HG-Haut ist für Cycles abgestimmt; in Godot wirkt sie sonst ölig -> Rauheit anheben
rmin = einst.get("haut_rauheit_min", 0.42)
h_rau = backen(body, haut_mats, "Roughness", r_haut // 2, "haut_rauheit", nach=lambda px: rmin + (1 - rmin) * px)
h_nor = backen(body, haut_mats, "Normal", r_haut, "haut_normal")
m_haut = spielmaterial("Haut", h_far, h_nor, h_rau)
body.data.materials.clear(); body.data.materials.append(m_haut)
for p in body.data.polygons:
    p.material_index = 0
body.data.uv_layers.remove(body.data.uv_layers[0])
log("Haut gebacken")

# Haare: Farbe+Alpha (PNG) und Normal je Material, auf der vorhandenen Karten-UV (Atlas von HG, 4K).
# Alle Haarobjekte in einem Backvorgang je Kanal (jeder Vorgang kostet sonst ~25 s Szenenaufbau).
r_haar = int(R.get("haar", 2048))
hf = einst.get("haarfarbe", {})
haar_mats = []
for o in haare:
    benutzt = {p.material_index for p in o.data.polygons}
    for si, mat in enumerate(o.data.materials):
        if si in benutzt and mat not in [m for m, _ in haar_mats]:
            for n_ in mat.node_tree.nodes:
                if n_.bl_idname == "ShaderNodeGroup" and "Lightness" in n_.inputs:
                    for k_, v_ in hf.items():
                        if k_ in n_.inputs:
                            n_.inputs[k_].default_value = v_
            haar_mats.append((mat, f"haar{len(haar_mats)}"))


def backen_zusammen(objs, paare, art, groesse, endung, format_, alpha=False):
    """Wie backen(), aber mehrere Materialien mit je eigenem Zielbild in einem Vorgang."""
    bilder, weg = {}, []
    for mat, nm in paare:
        datei = os.path.join(texdir, nm + endung + (".jpg" if format_ == "JPEG" else ".png"))
        if os.path.exists(datei) and not einst.get("neu_backen", True):
            bilder[nm] = bpy.data.images.load(datei)
            continue
        img = bpy.data.images.new(nm + endung, groesse, groesse, alpha=alpha)
        if art != "Base Color":
            img.colorspace_settings.name = "Non-Color"
        img.filepath_raw = datei; img.file_format = format_
        bilder[nm] = img
        nt = mat.node_tree
        out = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeOutputMaterial" and n.is_active_output)
        vorher = out.inputs[0].links[0].from_socket if out.inputs[0].is_linked else None
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img
        for n_ in nt.nodes:
            n_.select = False
        tn.select = True; nt.nodes.active = tn
        pr = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled")
        em = None
        if art == "Normal":
            nt.links.new(pr.outputs[0], out.inputs[0])
        else:
            em = nt.nodes.new("ShaderNodeEmission")
            nt.links.new(quelle(mat, art), em.inputs[0]); nt.links.new(em.outputs[0], out.inputs[0])
        weg.append((nt, tn, em, out, vorher))
    if not weg:
        return bilder
    for o in ctx.selected_objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    ctx.view_layer.objects.active = objs[0]
    alt_s = sc.cycles.samples; sc.cycles.samples = einst.get("haar_samples", 16)
    bpy.ops.object.bake(type="NORMAL" if art == "Normal" else "EMIT")
    sc.cycles.samples = alt_s
    for nt, tn, em, out, vorher in weg:
        nt.nodes.remove(tn)
        if em:
            nt.nodes.remove(em)
        if vorher is not None:
            nt.links.new(vorher, out.inputs[0])
    for img in bilder.values():
        if img.filepath_raw and not img.packed_file and img.is_dirty:
            img.save()
    return bilder


for o in haare:
    o.data.uv_layers.active = o.data.uv_layers[0]
b_far = backen_zusammen(haare, haar_mats, "Base Color", r_haar, "_farbe", "PNG", alpha=True)
# Haarsträhnen sind im 4K-Atlas oft dünner als ein Pixel der 2K-Textur: Deckung über eine Kurve anheben
# (a' = 1-(1-a)^k), sonst verschwinden sie im Spiel hinter dem Alpha-Test.
b_alp = backen_zusammen(haare, haar_mats, "Alpha", r_haar, "_alpha", "PNG")
k_dicht = einst.get("haar_dichte", 4.0)
for _, nm in haar_mats:
    far, alp = b_far[nm], b_alp[nm]
    w_, h_ = far.size
    px = np.empty(w_ * h_ * 4, np.float32); far.pixels.foreach_get(px)
    pa = np.empty(w_ * h_ * 4, np.float32); alp.pixels.foreach_get(pa)
    px[3::4] = 1 - (1 - np.clip(pa[0::4], 0, 1)) ** k_dicht
    far.pixels.foreach_set(px)
    far.filepath_raw = os.path.join(texdir, nm + "_farbe.png"); far.file_format = "PNG"; far.save()
    bpy.data.images.remove(alp)
b_nor = backen_zusammen(haare, haar_mats, "Normal", r_haar // 2, "_normal", "JPEG")
ersatz = {mat.name: spielmaterial("Haar_" + nm, b_far[nm], b_nor[nm], None, alpha=b_far[nm], rauh_wert=0.55, blend=True)
          for mat, nm in haar_mats}
for o in haare:
    idx = [p.material_index for p in o.data.polygons]   # clear() setzt die Indizes zurück
    namen = [m.name for m in o.data.materials]
    o.data.materials.clear()
    neu_liste = []
    for nm_ in namen:
        neu_liste.append(ersatz.get(nm_) or list(ersatz.values())[0])
    for m in neu_liste:
        o.data.materials.append(m)
    for p, i in zip(o.data.polygons, idx):
        p.material_index = i
for k in kappen:
    bpy.data.objects.remove(k)
log("Haare gebacken")

# Zähne: nur Farbe; Augen: Iris gebacken, Hornhaut als klares Material
r_kl = int(R.get("klein", 512))
z_far = backen([zo, zl], [zo.data.materials[0]], "Base Color", r_kl, "zaehne_farbe")
m_z = spielmaterial("Zaehne", z_far, rauh_wert=0.35)
for o in (zo, zl):
    o.data.materials.clear(); o.data.materials.append(m_z)
iris = eyes.data.materials[1]
# Pupille: HG dunkelt sie über Brechung im Glas; im Spiel fehlt das -> als dunkler Fleck in die Farbe backen
pup = eyes.data.color_attributes.new("Maske", "FLOAT_COLOR", "POINT")   # gleicher Name wie an der Haut -> eine Farbebene nach dem Zusammenfügen
for seite in ("eyeball.L", "eyeball.R"):
    b_ = rig.data.bones[seite]
    mitte = eyes.matrix_world.inverted() @ (rig.matrix_world @ b_.head_local)
    achse = (eyes.matrix_world.inverted().to_3x3() @ Vector((0, -1, 0))).normalized()   # Blick nach vorn (HG: -y)
    gi = eyes.vertex_groups[seite].index
    for v in eyes.data.vertices:
        if any(g.group == gi and g.weight > 0.5 for g in v.groups):
            w_ = math.degrees((v.co - mitte).angle(achse, 0.0))
            m_ = min(1.0, max(0.0, (einst.get("pupille_grad", 11.0) - w_) / 2.5))
            pup.data[v.index].color = (m_, m_, m_, 1)
log("Pupille: Punkte", sum(1 for d in pup.data if d.color[0] > 0.5), "Gruppen", [g.name for g in eyes.vertex_groups])
ntp = iris.node_tree
prp = next(n for n in ntp.nodes if n.bl_idname == "ShaderNodeBsdfPrincipled")
srcp = prp.inputs["Base Color"].links[0].from_socket
cap = ntp.nodes.new("ShaderNodeVertexColor"); cap.layer_name = "Maske"
mxp = ntp.nodes.new("ShaderNodeMix"); mxp.data_type = "RGBA"
ntp.links.new(cap.outputs["Color"], mxp.inputs["Factor"]); ntp.links.new(srcp, mxp.inputs["A"])
mxp.inputs["B"].default_value = (0.01, 0.008, 0.007, 1)
ntp.links.new(mxp.outputs["Result"], prp.inputs["Base Color"])
a_far = backen(eyes, [iris], "Base Color", int(R.get("augen", 1024)), "augen_farbe")
m_iris = spielmaterial("Augen", a_far, rauh_wert=0.3)
m_horn = bpy.data.materials.new("Hornhaut"); m_horn.use_nodes = True
pr = m_horn.node_tree.nodes["Principled BSDF"]
pr.inputs["Base Color"].default_value = (1, 1, 1, 1); pr.inputs["Roughness"].default_value = 0.02
pr.inputs["Alpha"].default_value = 0.08; m_horn.surface_render_method = "BLENDED"
alt_namen = [m.name for m in eyes.data.materials]
alt_idx = [p.material_index for p in eyes.data.polygons]
eyes.data.materials.clear()
eyes.data.materials.append(m_horn); eyes.data.materials.append(m_iris)
for p, i in zip(eyes.data.polygons, alt_idx):
    p.material_index = 1 if alt_namen[i].endswith("Inner") else 0
log("Zähne und Augen gebacken")

# ---------------------------------------------------------------- 6. Netz verschlanken, Knochen, zusammenfügen
def dezimieren_mit_formen(obj, verh):
    if verh >= 1:
        return
    n = len(obj.data.vertices)
    base = np.empty(n * 3); obj.data.vertices.foreach_get("co", base); base = base.reshape(n, 3)
    alt = {}
    if obj.data.shape_keys:
        for k in obj.data.shape_keys.key_blocks[1:]:
            c = np.empty(n * 3); k.data.foreach_get("co", c); alt[k.name] = c.reshape(n, 3) - base
        obj.shape_key_clear()
    m = obj.modifiers.new("dez", "DECIMATE"); m.ratio = verh
    with ctx.temp_override(object=obj, active_object=obj):
        bpy.ops.object.modifier_apply(modifier=m.name)
    if not alt:
        return
    # Formen über den nächsten alten Punkt zurückholen (Collapse behält Punkte nahe an alten Lagen)
    from mathutils.kdtree import KDTree
    kd = KDTree(n)
    for i, v in enumerate(base):
        kd.insert(Vector(v), i)
    kd.balance()
    nn = len(obj.data.vertices)
    neu_co = np.empty(nn * 3); obj.data.vertices.foreach_get("co", neu_co); neu_co = neu_co.reshape(nn, 3)
    nachbar = np.array([kd.find(Vector(p))[1] for p in neu_co])
    obj.shape_key_add(name="Basis")
    for name, d in alt.items():
        k = obj.shape_key_add(name=name, from_mix=False)
        k.data.foreach_set("co", (neu_co + d[nachbar]).reshape(-1))


D = einst.get("dezimieren", {})
dezimieren_mit_formen(eyes, D.get("augen", 0.35))
dezimieren_mit_formen(zo, D.get("zaehne", 0.3))
dezimieren_mit_formen(zl, D.get("zaehne", 0.3))
log("dezimiert")

# Haarkarten-Normalen: vom Kopf weg (Mischung aus Richtung vom Kopfmittelpunkt und Hautnormale am Fußpunkt).
# So schattiert das Haar weich wie eine Haube statt fleckig je Karte; das Spiel-Material kehrt sie auf der Rückseite nicht um.
kopf_mitte = rig.matrix_world @ (kopf_b + Vector((0, 0.01, 0.07)))
for o in haare:
    mwo = o.matrix_world; inv3 = mwo.inverted().to_3x3()
    nn = []
    for v in o.data.vertices:
        p_ = mwo @ v.co
        loc, nor, fi, d = bvh.find_nearest(p_)
        r = (p_ - kopf_mitte).normalized()
        nn.append((inv3 @ (r * 0.6 + nor * 0.4)).normalized())
    o.data.normals_split_custom_set_from_vertices(nn)

# Haare: Elternknochen lösen, fest an „head“ wiegen
for o in haare:
    mwo = o.matrix_world.copy()
    o.parent = None; o.matrix_world = mwo
    # Transformation in die Punkte backen (Formen bleiben relativ korrekt, da linear)
    me = o.data
    M = np.array(mwo)
    n = len(me.vertices)
    for k in me.shape_keys.key_blocks:
        c = np.empty(n * 3); k.data.foreach_get("co", c); c = c.reshape(n, 3)
        k.data.foreach_set("co", ((np.c_[c, np.ones(n)] @ M.T)[:, :3]).reshape(-1))
    c = np.empty(n * 3); me.shape_keys.key_blocks["Basis"].data.foreach_get("co", c); me.vertices.foreach_set("co", c)
    o.matrix_world = Matrix.Identity(4)
    g = o.vertex_groups.new(name="head"); g.add(list(range(n)), 1.0, "REPLACE")
    o.parent = rig
    am = o.modifiers.new("Armature", "ARMATURE"); am.object = rig

# Knochen: nur was das Brustbild braucht
behalten = set(einst.get("knochen", ["spine", "spine.001", "spine.002", "spine.003", "neck", "head", "jaw",
                                        "eyeball.L", "eyeball.R", "shoulder.L", "shoulder.R",
                                        "upper_arm.L", "upper_arm.R", "breast.L", "breast.R"]))
for pb in rig.pose.bones:
    for c in list(pb.constraints):
        pb.constraints.remove(c)
ctx.view_layer.objects.active = rig
for o in ctx.selected_objects:
    o.select_set(False)
rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
eb = rig.data.edit_bones
for b in list(eb):
    if b.name in behalten:
        p = b.parent
        while p is not None and p.name not in behalten:
            p = p.parent
        b.parent = p
for b in list(eb):
    if b.name not in behalten:
        eb.remove(b)
bpy.ops.object.mode_set(mode="OBJECT")
for o in [body, eyes, zo, zl]:
    for g in list(o.vertex_groups):
        if g.name not in behalten:
            o.vertex_groups.remove(g)
log("Knochen", len(rig.data.bones))

# Zusammenfügen: alles in ein Netz „Kopf“ (gleiche Formen über alle Teile)
teile = [body, eyes, zo, zl] + haare
for o in teile:
    while len(o.data.uv_layers) > 1:
        o.data.uv_layers.remove(o.data.uv_layers[1])
    o.data.uv_layers[0].name = "UVMap"
    wl = o.data.uv_layers.new(name="Wurzel")
    werte = o.get("wurzel")
    vi = np.empty(len(o.data.loops), np.int64); o.data.loops.foreach_get("vertex_index", vi)
    u = np.array(werte)[vi] if werte is not None and len(werte) == len(o.data.vertices) else np.ones(len(vi))
    v = np.zeros(len(vi))
    if "kittel" in o.data.attributes:
        kf = np.empty(len(o.data.polygons), np.int64); o.data.attributes["kittel"].data.foreach_get("value", kf)
        li = np.empty(len(o.data.loops), np.int64)
        for p_ in o.data.polygons:
            li[p_.loop_start:p_.loop_start + p_.loop_total] = p_.index
        v = kf[li].astype(float)
    wl.data.foreach_set("uv", np.c_[u, v].reshape(-1))
for o in teile:
    o.select_set(True)
    o.data.shape_keys.animation_data_clear() if o.data.shape_keys else None
    for k in (o.data.shape_keys.key_blocks if o.data.shape_keys else []):
        k.value = 0.0
rig.select_set(False)
ctx.view_layer.objects.active = body
with ctx.temp_override(active_object=body, selected_editable_objects=teile, selected_objects=teile):
    bpy.ops.object.join()
kopf = body; kopf.name = "Kopf"; kopf.data.name = "Kopf"
# Normalen der Gewichte (max. 4 Einflüsse für Godot)
with ctx.temp_override(object=kopf, active_object=kopf):
    bpy.ops.object.vertex_group_limit_total(limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
tris_n = sum(len(p.vertices) - 2 for p in kopf.data.polygons)
log("Kopf", len(kopf.data.vertices), "Punkte", tris_n, "Dreiecke", len(kopf.data.shape_keys.key_blocks) - 1, "Formen",
    "Materialien", [m.name for m in kopf.data.materials])

# ---------------------------------------------------------------- Ausgabe
# Winzige Formanteile (< 0,1 mm) auf null, damit der Export sie als dünn besetzte Daten schreibt
n = len(kopf.data.vertices)
base = np.empty(n * 3); kopf.data.shape_keys.key_blocks["Basis"].data.foreach_get("co", base); base = base.reshape(n, 3)
for k in kopf.data.shape_keys.key_blocks[1:]:
    c = np.empty(n * 3); k.data.foreach_get("co", c); d = c.reshape(n, 3) - base
    d[np.abs(d).max(1) < einst.get("form_schwelle", 1e-4)] = 0
    k.data.foreach_set("co", (base + d).reshape(-1))
for o in bpy.data.objects:
    o.select_set(o in (kopf, rig))
bpy.ops.export_scene.gltf(
    filepath=ziel, export_format="GLB", use_selection=True,
    export_skins=True, export_morph=True, export_morph_normal=einst.get("form_normalen", True), export_morph_tangent=False,
    export_try_sparse_sk=True, export_animations=False, export_yup=True, export_apply=False,
    export_image_format=einst.get("bildformat", "WEBP"), export_image_quality=einst.get("bildqualitaet", 85), export_def_bones=False,
    export_vertex_color="NONE",
)
groesse = os.path.getsize(ziel) / 1e6
beschr = {
    "dreiecke": tris_n, "punkte": len(kopf.data.vertices), "mb": round(groesse, 2),
    "gesicht": gesicht, "regler": list(regler.keys()),
    "zunge": [k.name for k in kopf.data.shape_keys.key_blocks if k.name.startswith("tongue")],
    "knochen": sorted(behalten),
}
json.dump(beschr, open(os.path.splitext(ziel)[0] + ".json", "w"), indent=1, ensure_ascii=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.splitext(ziel)[0] + "_b.blend")
log("fertig", ziel, f"{groesse:.1f} MB", tris_n, "Dreiecke")
