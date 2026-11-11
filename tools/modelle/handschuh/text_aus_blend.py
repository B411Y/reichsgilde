"""Uebernimmt die SELBST eingestellte Text-Haltung (selbst/text_haltung_fertig.blend) in den Text-Clip der Handschuh-glb.
  blender -b handschuh_pixal3d_v6.blend --python text_aus_blend.py -- <ausordner> <text_haltung_fertig.blend> [bilder]
Liest die Pose der Knochen (Rotationen, Euler XYZ oder Quaternion; Verschiebung nur Knoechel-Hilfsknochen) aus dem Objekt
"Handschuh_Geruest" der fertigen Datei (so wie gespeichert, auch ohne Schluesselbild), baut daraus die Aktionen Text,
Uebergang_Ruhe_Text, Uebergang_Text_Ruhe, Uebergang_Zeigen_Text, Uebergang_Text_Zeigen (Uebergaenge: Zeitstaffel wie v6_rig_lib.gestaffelt,
Rotationen als Quaternion-Slerp, damit jede Haltung moeglich ist), setzt Anker_Text, exportiert <ausordner>/handschuh_zeiger_roh.glb
(danach glb_faltenfarbe.py) und schreibt messwerte_text.json (Durchdringung nach Paaren, Daumenzahlen, Uebergaenge).
Mit "bilder" zusaetzlich Bilder fuer v6_text_blatt.py. Der Text-Clip ist eine ruhende Haltung (keine Atembewegung)."""
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector, Quaternion, Matrix
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
args = sys.argv[sys.argv.index("--") + 1:]
aus, fertig = args[0], args[1]; bilder = len(args) > 2 and args[2] == "bilder"; gross = len(args) > 2 and args[2] == "gross"   # gross: nur Vergleichsbild-Zutaten (PX, ORTHO per Umgebung)
os.makedirs(os.path.join(aus, "bilder"), exist_ok=True)
os.environ.setdefault("TEXT", "{}")
import v2_lib, v6_rig_lib as L
import v6_text_lib as T
from v6_rig_lib import AX_X
rig, gl = T.rig, T.gl
sc = bpy.context.scene
NAMEN = [pb.name for pb in rig.pose.bones]
ID = Quaternion((1, 0, 0, 0))

# ------------------------------------------------ Pose aus der fertigen Datei lesen
AKT0 = {a.name for a in bpy.data.actions}
with bpy.data.libraries.load(fertig, link=False) as (src, dst):
    dst.objects = [n for n in src.objects if n == "Handschuh_Geruest"]
imp = dst.objects[0] if dst.objects else None
if imp is None or imp.type != "ARMATURE": raise SystemExit("Objekt Handschuh_Geruest nicht in " + fertig)
def lies(pb):
    if pb.rotation_mode == "QUATERNION": return pb.rotation_quaternion.copy()
    if pb.rotation_mode == "AXIS_ANGLE":
        a = pb.rotation_axis_angle; return Quaternion(Vector(a[1:]), a[0])
    return pb.rotation_euler.to_quaternion()
TEXT_Q = {}
for n in NAMEN:
    pb = imp.pose.bones.get(n)
    if pb is None: raise SystemExit("Knochen fehlt in der fertigen Datei: " + n)
    TEXT_Q[n] = (lies(pb), Vector(pb.location))
bpy.data.objects.remove(imp)
for a_ in list(bpy.data.actions):          # mitgeladene Aktion der fertigen Datei (Text_Haltung) darf nicht in die glb
    if a_.name not in AKT0: bpy.data.actions.remove(a_)

# ------------------------------------------------ Hilfen: Posen als Quaternion
def pose_lesen(d):
    L.pose_anwenden(rig, d); bpy.context.view_layer.update()
    return {pb.name: (pb.rotation_quaternion.copy(), Vector(pb.location)) for pb in rig.pose.bones}
def setze(q):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion, pb.location = q[pb.name][0], q[pb.name][1]; pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()
def keyen(q, frame):
    setze(q)
    for pb in rig.pose.bones:
        pb.keyframe_insert("location" if pb.name.startswith("Knoechel") else "rotation_quaternion", frame=frame, group=pb.name)
def mische(a, b, p):
    out = {}
    for n in NAMEN:
        t = p[n]; qa, la = a[n]; qb, lb = b[n]
        if qa.dot(qb) < 0: qb = Quaternion((-qb.w, -qb.x, -qb.y, -qb.z))
        out[n] = (qa.slerp(qb, t), la.lerp(lb, t))
    return out
def zeitparameter(t, schliessen, kurz):
    von = {n: [(AX_X, 0.0)] for n in NAMEN}; nach = {n: [(AX_X, 1.0)] for n in NAMEN}
    g = L.gestaffelt(von, nach, t, schliessen, kurz)
    return {n: g[n][0][1] for n in NAMEN}
PR_Q = pose_lesen(T.PR); PZ_Q = pose_lesen(T.PZ)

# ------------------------------------------------ Messen
def messen(q):
    setze(q); dg = bpy.context.evaluated_depsgraph_get()
    z = v2_lib.selbstschnitt_zerlegt(gl, dg)
    return sum(n for (a, b), n in z.items() if a != b and not (a == "H" and b == "H")), z
def zahlen():
    pb = rig.pose.bones; W = rig.matrix_world
    P_ = lambda b, a: np.array((W @ getattr(pb[b], a))[:])
    ang = lambda u, v: float(np.degrees(np.arccos(np.clip((u / np.linalg.norm(u)) @ (v / np.linalg.norm(v)), -1, 1))))
    ach = {k: P_(f"finger{k}-3.R", "tail") - P_(f"finger{k}-1.R", "head") for k in (2, 3, 4, 5)}
    e0, e1, e2, e3 = P_("finger1-1.R", "head"), P_("finger1-1.R", "tail"), P_("finger1-2.R", "tail"), P_("finger1-3.R", "tail")
    a0 = P_("finger2-1.R", "head"); ax = ach[2] / np.linalg.norm(ach[2]); t = e3 - a0
    return {"daumen_winkel_zum_zeigefinger_grad": round(ang(e3 - e0, ach[2]), 1), "daumenspitze_zur_zeigefingerachse_mm": round(float(np.linalg.norm(t - (t @ ax) * ax)) * 1000, 1),
            "daumen_zweiter_knick_3d_grad": round(ang(e2 - e1, e3 - e2), 1), "daumen_knickwinkel_ansatz_zu_spitze_3d_grad": round(ang(e1 - e0, e3 - e1), 1),
            "finger_faecher_max_grad": round(max(ang(ach[i], ach[j]) for i in ach for j in ach if i < j), 1)}
def kante():
    """Mittelpunkt der zum Betrachter (Welt +Z, Ansicht oben) zeigenden Handkante auf Hoehe der Mitte des Mittelfingers."""
    dg = bpy.context.evaluated_depsgraph_get(); me = gl.evaluated_get(dg).to_mesh(); V = np.array([(gl.matrix_world @ v.co)[:] for v in me.vertices])
    pb = rig.pose.bones; a = np.array((rig.matrix_world @ pb["finger3-1.R"].head)[:]); b = np.array((rig.matrix_world @ pb["finger3-3.R"].tail)[:])
    mitte = (a + b) / 2; laengs = (b - a) / np.linalg.norm(b - a); r = np.array([0.0, 0.0, 1.0])
    band = np.abs((V - mitte) @ laengs) < 0.004; proj = (V[band] - mitte) @ r
    return V[band][proj > proj.max() - 0.0015].mean(axis=0)

# ------------------------------------------------ Anker_Text
for o in list(bpy.data.objects):
    if o.name == "Anker_Text": bpy.data.objects.remove(o)
setze(TEXT_Q); pt = kante()
ankerT = bpy.data.objects.new("Anker_Text", None); ankerT.empty_display_type = "PLAIN_AXES"; ankerT.empty_display_size = 0.01
sc.collection.objects.link(ankerT); ankerT.parent = rig; ankerT.parent_type = "BONE"; ankerT.parent_bone = "wrist.R"
ankerT.matrix_world = Matrix.Translation(Vector(pt.tolist()))
print("ANKER_TEXT", [round(float(x), 4) for x in pt])

# ------------------------------------------------ Aktionen
rig.animation_data_create(); neu = {}
def aktion(name, laenge, schleife):
    a = bpy.data.actions.new(name); a.use_fake_user = True; rig.animation_data.action = a
    a.use_frame_range = True; a.frame_range = (0, laenge); a.use_cyclic = schleife; return a
a = aktion("Text", 48, True); neu["Text"] = (a, 48, 6)
for f in range(0, 49, 4): keyen(TEXT_Q, f)
for n, v, nch, ln, sl in (("Uebergang_Ruhe_Text", PR_Q, TEXT_Q, 14, False), ("Uebergang_Text_Ruhe", TEXT_Q, PR_Q, 14, False),
                          ("Uebergang_Zeigen_Text", PZ_Q, TEXT_Q, 16, False), ("Uebergang_Text_Zeigen", TEXT_Q, PZ_Q, 16, True)):
    a = aktion(n, ln, False); neu[n] = (a, ln, 1)
    for f in range(ln + 1): keyen(mische(v, nch, zeitparameter(f / ln, sl, False)), f)
rig.animation_data.action = bpy.data.actions["Ruhe"]
sc.frame_set(0); L.pose_anwenden(rig, T.P["ruhe"]); bpy.context.view_layer.update()
bpy.ops.object.select_all(action="DESELECT")
bpy.ops.export_scene.gltf(filepath=os.path.join(aus, "handschuh_zeiger_roh.glb"), export_format="GLB", export_animations=True, export_animation_mode="ACTIONS", export_skins=True, export_def_bones=False)
print("GLB geschrieben")

# ------------------------------------------------ Messwerte
setze(TEXT_Q); inter, z = messen(TEXT_Q)
mess = {"durchdringung_gesamt": inter, "durchdringung_paare": {f"{a}-{b}": n for (a, b), n in z.items() if a != b and not (a == "H" and b == "H")}, "zahlen": zahlen(), "kante_punkt": [round(float(x), 4) for x in pt]}
ue = {}
for n, (a_, ln, st) in neu.items():
    rig.animation_data.action = a_; mx = 0; zz_ = None
    for f in range(0, ln + 1, 1 if n != "Text" else 4):
        sc.frame_set(f); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
        zz = v2_lib.selbstschnitt_zerlegt(gl, dg); c = sum(c for (aa, bb), c in zz.items() if aa != bb and not (aa == "H" and bb == "H"))
        if c > mx: mx = c; zz_ = {f"{aa}-{bb}": cc for (aa, bb), cc in zz.items() if aa != bb and not (aa == "H" and bb == "H")}
    ue[n] = {"max": mx, "paare_beim_maximum": zz_}
mess["clips_max"] = ue
json.dump(mess, open(os.path.join(aus, "messwerte_text.json"), "w"), indent=1)
print("MESSWERTE", json.dumps({k: v for k, v in mess.items() if k != "clips_max"}), {n: v["max"] for n, v in ue.items()})

# ------------------------------------------------ Bilder (wie v6_text.py: oben, seite, schraeg, spitze)
if bilder:
    L.ANSICHTEN["spitze"] = ((0, -1, 0.15), 0.30)
    cam = L.buehne(int(os.environ.get("PX", "384"))); sc.render.film_transparent = True
    for o in bpy.data.objects:
        if o.name.startswith("Hilfe_Marke"): o.hide_render = True
    for n, (a_, ln, st) in neu.items():
        rig.animation_data.action = a_
        for f in range(0, ln + 1, st):
            sc.frame_set(f); bpy.context.view_layer.update()
            for an in ("oben", "seite", "schraeg", "spitze"): L.ansicht_rendern(cam, an, os.path.join(aus, "bilder", f"{n}_{f:02d}_{an}.png"), Vector((0, 0, 0)))
if gross:   # Zutaten fuer Vergleichsbild Foto | Handschuh: probe_text_oben.png und linien.json (Linien aus den Knochen in die Ansicht oben projiziert)
    sc.render.film_transparent = True; cam = L.buehne(int(os.environ.get("PX", "1000")))
    for o in bpy.data.objects:
        if o.name.startswith("Hilfe_Marke"): o.hide_render = True
    rig.animation_data.action = None; setze(TEXT_Q); L.ansicht_rendern(cam, "oben", os.path.join(aus, "probe_text_oben.png"), Vector((0, 0, 0)))
    q = Vector((0, 0.001, -1)).normalized().to_track_quat("-Z", "Y"); R_ = -np.array((q @ Vector((1, 0, 0)))[:]); O_ = -np.array((q @ Vector((0, 1, 0)))[:])
    px = int(os.environ.get("PX", "1000")); orth = float(os.environ.get("ORTHO", "0.40")); pb = rig.pose.bones
    P_ = lambda b, a: np.array((rig.matrix_world @ getattr(pb[b], a))[:])
    bl = lambda p: [round((float(p @ R_) / orth + 0.5) * px, 1), round((0.5 - float(p @ O_) / orth) * px, 1)]
    mm = lambda p: np.array([float(p @ R_), float(p @ O_)]) * 1000
    out = {"px": px, "ortho": orth, "finger": {}, "daumen": []}; wink = {}
    for k in (2, 3, 4, 5):
        out["finger"][str(k)] = [bl(P_(f"finger{k}-1.R", "head")), bl(P_(f"finger{k}-2.R", "head")), bl(P_(f"finger{k}-3.R", "head")), bl(P_(f"finger{k}-3.R", "tail"))]
        v = mm(P_(f"finger{k}-3.R", "tail")) - mm(P_(f"finger{k}-1.R", "head")); wink[k] = float(np.degrees(np.arctan2(v[0], v[1])))
    pts = [P_("finger1-1.R", "head"), P_("finger1-1.R", "tail"), P_("finger1-2.R", "tail"), P_("finger1-3.R", "tail")]
    out["daumen"] = [bl(p) for p in pts]; t = [mm(p) for p in pts]; knick = float(t[1][0] - (t[0][0] + t[3][0]) / 2)
    z_ = zahlen(); out["kennzahlen"] = {"fingerachsen_max_differenz_grad": round(max(wink.values()) - min(wink.values()), 2), "daumen_knick_mm_rechts_positiv": round(knick, 1),
        "daumen_knickrichtung": "> (Knick zum Handruecken, rechts)" if knick > 0 else "< (Knick zur Handflaeche, links)", "daumen_zweiter_knick_3d_grad": z_["daumen_zweiter_knick_3d_grad"]}
    json.dump(out, open(os.path.join(aus, "linien.json"), "w"), indent=1); print("LINIEN", out["kennzahlen"])
print("FERTIG")
