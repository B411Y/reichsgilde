"""Umriss, Marke, Anker, Clips, Export, Bilder, Messwerte. blender -b hand_v2.blend --python v2_final.py -- <ausordner>"""
import bpy, sys, os, math, json, importlib.util
import numpy as np
from mathutils import Vector, Matrix
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import v2_lib, v2_rig_lib as L, v2_posen
from v2_rig_lib import AX_X, AX_Y, AX_Z
aus = sys.argv[sys.argv.index("--") + 1]
os.makedirs(aus, exist_ok=True)
spec = importlib.util.spec_from_file_location("h3", os.path.join(os.path.dirname(os.path.abspath(__file__)), "handschuh_blender_basis.py"))
h3 = importlib.util.module_from_spec(spec); spec.loader.exec_module(h3)
rig = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]; rig.name = "Handschuh_Geruest"
gl = bpy.data.objects["Handschuh"]
info = json.load(open(os.path.join(aus, "..", "hand_v4.blend.json")))
bpy.context.view_layer.update()
def mat(name, farbe, rau, metall=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (*farbe, 1); b.inputs["Roughness"].default_value = rau; b.inputs["Metallic"].default_value = metall
    return m
m_um = mat("Umriss", (0.02, 0.012, 0.008), 1.0); m_um.use_backface_culling = True
m_ms = mat("Messing", (0.78, 0.52, 0.17), 0.38, 1.0)
# Umriss-Huelle (invertierte Huelle wie beim Blender-Weg)
um = h3.umriss_bauen(gl, "Handschuh_Umriss", float(os.environ.get("UMRISS","0.0040")))
sm_ = um.modifiers.new("Glatt", "SMOOTH"); sm_.factor = 0.5; sm_.iterations = 2
bpy.context.view_layer.objects.active = um; bpy.ops.object.modifier_apply(modifier="Glatt")
um.data.materials.clear(); um.data.materials.append(m_um); um.parent = rig; um.visible_shadow = False
um.modifiers.new("Armature", "ARMATURE").object = rig
# Hilfe-Marke
marke = h3.marke_bauen(); marke.data.materials.clear(); marke.data.materials.append(m_ms)
from mathutils.bvhtree import BVHTree
bpy.context.view_layer.update(); bvh_ = BVHTree.FromObject(gl, bpy.context.evaluated_depsgraph_get())
MX, MY, MZ = [float(x) for x in os.environ.get("MARKE", "0.05,0.02,0.045").split(",")]
for _ in range(60):
    Mt = Matrix.Translation((MX, MY, MZ)) @ Matrix.Rotation(math.radians(-40), 4, "Z")
    dmin = min(bvh_.find_nearest(Mt @ v.co)[3] for v in marke.data.vertices)
    inside = False
    if dmin >= 0.008: break
    MX += 0.002
print("MARKE Position", round(MX, 3), MY, MZ, "Abstand zum Handschuh mm", round(dmin * 1000, 1))
marke.parent = rig; marke.parent_type = "BONE"; marke.parent_bone = "Wurzel"
marke.matrix_world = Matrix.Translation((MX, MY, MZ)) @ Matrix.Rotation(math.radians(-40), 4, "Z")
mum = h3.umriss_bauen(marke, "Hilfe_Marke_Umriss", 0.0016)
mum.data.materials.clear(); mum.data.materials.append(m_um); mum.parent = rig; mum.parent_type = "BONE"; mum.parent_bone = "Wurzel"; mum.matrix_world = marke.matrix_world.copy(); mum.visible_shadow = False
# Anker an der Zeigefingerspitze
anker = bpy.data.objects.new("Anker_Spitze", None); anker.empty_display_type = "PLAIN_AXES"; anker.empty_display_size = 0.01
bpy.context.scene.collection.objects.link(anker); anker.parent = rig; anker.parent_type = "BONE"; anker.parent_bone = "finger2-3.R"
sp = np.array(info["spitze"]); d2 = rig.data.bones["finger2-3.R"]; dirv = (d2.tail_local - d2.head_local).normalized()
anker.matrix_world = Matrix.Translation(Vector(sp.tolist()) - dirv * 0.004)
print("ANKER", [round(x, 4) for x in anker.matrix_world.to_translation()])
# ----------------------------------------------------------- Posen und Clips
P = v2_posen.posen(rig)
alle = [b.name for b in rig.data.bones]
def voll(d):
    out = {}
    for n in alle:
        out[n] = d.get(n, [(AX_X, 0.0)] * 1)
    return out
for k in P: P[k] = voll(P[k])
# gleiche Listenlaenge je Knochen sicherstellen
for n in alle:
    ln = max(len(P[k][n]) for k in P)
    for k in P:
        while len(P[k][n]) < ln: P[k][n].append((P[k][n][0][0], 0.0))
for n in ("wrist.R", "Wurzel"):
    for k in P:
        if n == "Wurzel": P[k][n] = [(-AX_X, 0.0), (AX_Z, 0.0), (AX_Y, 0.0)]
def atmen(basis, t, amp=1.0):
    w = 2 * math.pi * t
    d = {k: [(ax, a) for (ax, a) in v] for k, v in basis.items()}
    d["Wurzel"] = [(-AX_X, 1.6 * amp * math.sin(w)), (AX_Z, 2.2 * amp * math.sin(w + 1.2)), (AX_Y, 1.4 * amp * math.sin(w + 2.4))]
    wr = d["wrist.R"]; d["wrist.R"] = [(wr[0][0], wr[0][1] + 1.5 * amp * math.sin(w + 0.8))] + wr[1:]
    for k in range(2, 6):
        f1 = d[f"finger{k}-1.R"]; d[f"finger{k}-1.R"] = [(f1[0][0], f1[0][1] + 2.0 * amp * math.sin(w + 0.35 * k))] + f1[1:]
        f2 = d[f"finger{k}-2.R"]; d[f"finger{k}-2.R"] = [(f2[0][0], f2[0][1] + 2.5 * amp * math.sin(w + 0.35 * k + 0.6))] + f2[1:]
    return d
def aktion(name, laenge, schleife):
    a = bpy.data.actions.new(name); a.use_fake_user = True
    rig.animation_data_create(); rig.animation_data.action = a
    a.use_frame_range = True; a.frame_range = (0, laenge); a.use_cyclic = schleife
    return a
clips = {}
a = aktion("Ruhe", 48, True); clips["Ruhe"] = (a, 48, 4)
for f in range(0, 49, 4): L.pose_keyen(rig, atmen(P["ruhe"], f / 48.0), f)
a = aktion("Zeigen", 48, True); clips["Zeigen"] = (a, 48, 4)
for f in range(0, 49, 4): L.pose_keyen(rig, atmen(P["zeigen"], f / 48.0, 0.45), f)
a = aktion("Greifen", 36, True); clips["Greifen"] = (a, 36, 3)
for f in range(0, 37, 3):
    t = f / 36.0; dd = atmen(P["faust"], t, 0.3); pr = 0.5 + 0.5 * math.sin(2 * math.pi * t)
    for k in range(2, 6):
        f1 = dd[f"finger{k}-1.R"]; dd[f"finger{k}-1.R"] = [(f1[0][0], f1[0][1] + 3 * pr)] + f1[1:]
    L.pose_keyen(rig, dd, f)
a = aktion("Tippen", 24, False); clips["Tippen"] = (a, 24, 2)
z = P["zeigen"]
def mit(d, **kw):
    q = {k: [(ax, w) for (ax, w) in v] for k, v in d.items()}
    for n, (idx, wert) in kw.items():
        nm = n.replace("_", "-", 1).replace("_R", ".R") if False else n
    return q
def setz(d, n, i, wert): d[n] = [(ax, (wert if j == i else w)) for j, (ax, w) in enumerate(d[n])]
def gib(d): return {k: [(ax, w) for (ax, w) in v] for k, v in d.items()}
f20 = z["finger2-1.R"][0][1]; f21 = z["finger2-2.R"][0][1]
kontakt = gib(z); setz(kontakt, "finger2-1.R", 0, 22); setz(kontakt, "finger2-2.R", 0, 14); setz(kontakt, "finger2-3.R", 0, 8); kontakt["wrist.R"] = [(-AX_X, 11)]
ausholen = gib(z); setz(ausholen, "finger2-1.R", 0, -10); setz(ausholen, "finger2-2.R", 0, -4); ausholen["wrist.R"] = [(-AX_X, -5)]
for f, d in ((0, z), (4, ausholen), (7, kontakt), (10, L.mischen(kontakt, z, 0.35)), (14, L.mischen(z, ausholen, 0.35)), (24, z)): L.pose_keyen(rig, d, f)
def ss_(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def uebergang(name, von, nach, laenge=8, daumen_spaet=True):
    a = aktion(name, laenge, False); clips[name] = (a, laenge, 1)
    for f in range(laenge + 1):
        t = f / laenge
        tf = ss_(t / 0.75) if daumen_spaet else ss_((t - 0.25) / 0.75)
        td = ss_((t - 0.25) / 0.75) if daumen_spaet else ss_(t / 0.75)
        d = {}
        for n in von:
            tt = td if n.startswith("finger1") else tf
            d[n] = [(ax, wa + (wb - wa) * tt) for (ax, wa), (_, wb) in zip(von[n], nach[n])]
        L.pose_keyen(rig, d, f)
def uebergang_g(name, von, nach, laenge, schliessen):
    a = aktion(name, laenge, False); clips[name] = (a, laenge, 1)
    for f in range(laenge + 1): L.pose_keyen(rig, L.gestaffelt(von, nach, f / laenge, schliessen), f)
uebergang("Uebergang_Ruhe_Zeigen", P["ruhe"], P["zeigen"]); uebergang("Uebergang_Zeigen_Ruhe", P["zeigen"], P["ruhe"], daumen_spaet=False)
uebergang_g("Uebergang_Ruhe_Greifen", P["ruhe"], P["faust"], 24, True); uebergang_g("Uebergang_Greifen_Ruhe", P["faust"], P["ruhe"], 24, False)
uebergang_g("Uebergang_Zeigen_Greifen", P["zeigen"], P["faust"], 12, True); uebergang_g("Uebergang_Greifen_Zeigen", P["faust"], P["zeigen"], 12, False)
rig.animation_data.action = clips["Ruhe"][0]
# Ruhe-Schleife nahtlos: erstes und letztes Bild gleich
sc = bpy.context.scene; sc.frame_set(0); L.pose_anwenden(rig, P["ruhe"])
# ----------------------------------------------------------- Export
bpy.ops.object.select_all(action="DESELECT")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(aus, "..", "handschuh_pixal3d_v4.blend"))
try:
    bpy.ops.export_scene.gltf(filepath=os.path.join(aus, "..", "handschuh_pixal3d_v4.glb"), export_format="GLB", export_animations=True, export_animation_mode="ACTIONS", export_skins=True, export_def_bones=False)
    print("GLB geschrieben")
except Exception as e: print("GLBFEHLER", e)
# ----------------------------------------------------------- Messwerte
mess = {"netz": v2_lib.netz_messen(gl), "dreiecke": {"Handschuh": sum(len(p.vertices) - 2 for p in gl.data.polygons), "Umriss": sum(len(p.vertices) - 2 for p in um.data.polygons), "Marke": sum(len(p.vertices) - 2 for p in marke.data.polygons)}}
vg = {g.name for g in gl.vertex_groups}
w = np.zeros(len(gl.data.vertices)); ne = np.zeros(len(gl.data.vertices), int)
for v in gl.data.vertices:
    for g in v.groups: w[v.index] += g.weight; ne[v.index] += 1
mess["gewicht"] = {"summe_min": float(w.min()), "summe_max": float(w.max()), "max_einfluesse": int(ne.max()), "ohne_gewicht": int((w < 0.99).sum())}
pm = {}
def messen(name, d):
    L.pose_anwenden(rig, d); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
    z = v2_lib.selbstschnitt_zerlegt(gl, dg)
    inter = sum(n for (a, b), n in z.items() if a != b and not (a == "H" and b == "H")); innen = sum(n for (a, b), n in z.items() if a == b); hh = z.get(("H", "H"), 0)
    me = gl.evaluated_get(dg).to_mesh(); V = np.array([v.co[:] for v in me.vertices])
    tips = {}
    for k in (2, 3, 4, 5):
        tip = np.array((rig.matrix_world @ rig.pose.bones[f"finger{k}-3.R"].tail)[:]); tips[k] = [round(float(x), 4) for x in tip]
    pm[name] = dict(zwischen_gruppen=inter, gleiche_gruppe=innen, hand_hand=hh, aufgeschluesselt={f"{a}-{b}": n for (a, b), n in z.items()}, spitzen=tips)
    return z
for n in ("ruhe", "zeigen", "faust"): messen(n, P[n])
messen("tippen_kontakt", kontakt)
# Fingerspitze zur Handflaeche in der Faust (Abstand zur naechsten Ecke der Handflaeche = Gruppe H) in mm bei Handlaenge 190 mm
L.pose_anwenden(rig, P["faust"]); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
me = gl.evaluated_get(dg).to_mesh(); V = np.array([v.co[:] for v in me.vertices])
H = [i for i, v in enumerate(gl.data.vertices) if v.groups and max(v.groups, key=lambda g: g.weight) and gl.vertex_groups[max(v.groups, key=lambda g: g.weight).group].name.startswith(("wrist", "metacarpal"))]
Hv = V[H]
pm["faust_spitze_zu_handflaeche_mm"] = {str(k): round(float(np.linalg.norm(Hv - np.array((rig.matrix_world @ rig.pose.bones[f"finger{k}-3.R"].tail)[:]), axis=1).min()) * 1000, 1) for k in (2, 3, 4, 5)}
# Uebergaenge: Maximum ueber alle Bilder
ue = {}
for n, (a, ln, st) in clips.items():
    if not n.startswith("Uebergang"): continue
    rig.animation_data.action = a; mx = 0
    for f in range(ln + 1):
        sc.frame_set(f); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
        z = v2_lib.selbstschnitt_zerlegt(gl, dg); mx = max(mx, sum(c for (aa, bb), c in z.items() if aa != bb and not (aa == "H" and bb == "H")))
    ue[n] = mx
pm["uebergaenge_max_zwischen_gruppen"] = ue
mess["posen"] = pm
json.dump(mess, open(os.path.join(aus, "..", "messwerte.json"), "w"), indent=1)
print("MESSWERTE", json.dumps(mess)[:1500])
# ----------------------------------------------------------- Bilder
sc.render.film_transparent = True
cam = L.buehne(384)
marke.hide_render = True; mum.hide_render = True
for n, (a, ln, st) in clips.items():
    rig.animation_data.action = a
    for f in range(0, ln + 1, st):
        sc.frame_set(f); bpy.context.view_layer.update()
        for an in ("oben", "seite", "schraeg"):
            L.ansicht_rendern(cam, an, os.path.join(aus, f"{n}_{f:02d}_{an}.png"))
    print("CLIP", n, ln, st, flush=True)

marke.hide_render = False; mum.hide_render = False
rig.animation_data.action = clips["Ruhe"][0]; sc.frame_set(0); bpy.context.view_layer.update()
for an in ("oben", "seite", "schraeg"): L.ansicht_rendern(cam, an, os.path.join(aus, f"Hilfe_Marke_{an}.png"))
print("FERTIG")
