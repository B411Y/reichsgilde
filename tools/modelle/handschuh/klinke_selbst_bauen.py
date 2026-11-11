"""Baut die Arbeitsdatei, in der man die Haende an der Tuerklinke SELBST in Blender einstellt (Bereit, Greifen, Gedrueckt).
  blender -b handschuh_pixal3d_v6_text.blend --python klinke_selbst_bauen.py -- <ausordner> <tuerschloss.blend> <klinke_params.json> <vorlage_2d.png>
Ergebnis in <ausordner>: klinke_bearbeiten.blend (alles eingepackt), bild_knochen_klinke.png (Beschriftung, kein GUI-Screenshot).
Die Datei braucht keine Add-ons: Pose-Modus, 23 drehbare Knochen (Knoechel2-5 gesperrt), die Wurzel zusaetzlich verschiebbar, Handschuh deckend;
das Tuerschloss steht fest im Bild (Knoten Klinke bewegt sich in Bild 3 mit); Aktion Klinke_bearbeiten mit Bild 1 = Bereit, 2 = Greifen, 3 = Gedrueckt.
Ansicht links: Zeiger (wie im Spiel, -40 Grad), rechts: Seite; die 2D-Vorlage des Schlosses steht als Bild neben dem Schloss.
Ausgangspose = die Clips von v6_klinke.py (Hand-Wurzelversatz). Danach liest klinke_aus_blend.py die fertige Datei zurueck."""
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector, Matrix, Euler
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
args = sys.argv[sys.argv.index("--") + 1:]
aus, schloss_blend, pjson, vorlage = args[0], args[1], args[2], args[3]
os.makedirs(aus, exist_ok=True)
os.environ["TUERSCHLOSS"] = schloss_blend
import v6_klinke_lib as K
from v6_klinke_lib import rig, gl, L, tr, rz
sc = bpy.context.scene
par = json.load(open(pjson, encoding="utf-8"))
S = K.schloss_laden()
g = K.lage(par)

# ------------------------------------------------ Aufraeumen: alte Aktionen weg (nur in dieser Arbeitsdatei), Hilfsobjekte ausblenden
rig.animation_data_create(); rig.animation_data.action = None
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
for o in bpy.data.objects:
    if o.name.startswith(("Hilfe_Marke", "Anker_")) and o.name not in S or o.name == "Handschuh_Umriss":
        o.hide_viewport = True
        if o.name.startswith("Hilfe_Marke"): o.hide_render = True
for n in ("Umriss_Schloss", "Umriss_Klinke", "Anker_Schloss", "Anker_Griff", "Anker_Welle"): S[n].hide_viewport = True
for o in S.values(): o.hide_select = True
S["Tuerschloss"].matrix_world = g.LAM

# ------------------------------------------------ drei Haltungen als Euler-Schluessel (Bild 1 Bereit, 2 Greifen, 3 Gedrueckt)
def wurzel_werte(M, prev):
    b = rig.data.bones["Wurzel"]; mb = b.matrix_local.inverted() @ M @ b.matrix_local
    return (mb.to_euler("XYZ", prev) if prev else mb.to_euler("XYZ")), mb.to_translation()
HALT = [(1, g.PB, g.M_BER, 0.0), (2, g.PG, g.M_GRIFF, 0.0), (3, g.PG, g.M_DRUCK, K.VORZ * K.WINKEL)]
for pb in rig.pose.bones:
    pb.rotation_mode = "XYZ"; pb.scale = (1, 1, 1)
act = bpy.data.actions.new("Klinke_bearbeiten"); act.use_fake_user = True; rig.animation_data.action = act
prev = None; ausgang = {}
for f, d, M, th in HALT:
    e = K.euler_pose(d, prev); e["Wurzel"] = wurzel_werte(M, prev["Wurzel"][0] if prev else None)
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"; pb.rotation_euler, pb.location = e[pb.name][0], e[pb.name][1]
        pb.keyframe_insert("rotation_euler", frame=f, group=pb.name)
        if pb.name.startswith("Knoechel") or pb.name == "Wurzel": pb.keyframe_insert("location", frame=f, group=pb.name)
    prev = e; ausgang[f] = {n: [round(math.degrees(a), 1) for a in e[n][0]] for n in ("Wurzel", "wrist.R", "finger2-1.R")}
# Schloss: Klinke dreht in Bild 3 mit (Aktion Druecken_Bearbeiten auf dem Knoten Klinke)
kl = S["Klinke"]; kl.rotation_mode = "XYZ"; kl.animation_data_create()
akl = bpy.data.actions.new("Druecken_bearbeiten"); akl.use_fake_user = True; kl.animation_data.action = akl
for f, d, M, th in HALT:
    kl.rotation_euler = (0, 0, math.radians(th)); kl.keyframe_insert("rotation_euler", index=2, frame=f)
sc.frame_start, sc.frame_end = 1, 3; sc.frame_set(1)
try: sc.tool_settings.use_keyframe_insert_auto = True
except Exception as e: print("AUTOKEY-FEHLER", e)
bpy.context.view_layer.update()

# ------------------------------------------------ Knochen: alle ausser Hilfsknochen drehbar, Wurzel zusaetzlich verschiebbar
for b in rig.data.bones: b.hide = False; b.hide_select = False
for pb in rig.pose.bones:
    pb.lock_scale = (True, True, True)
    pb.lock_location = (False, False, False) if pb.name == "Wurzel" else (True, True, True)
    if pb.name.startswith("Knoechel"): pb.lock_rotation = (True, True, True)
    else: pb.lock_rotation = (False, False, False)
rig.pose.bones["Wurzel"].color.palette = "THEME01"
rig.data.show_names = True; rig.data.display_type = "OCTAHEDRAL"; rig.show_in_front = True
gl.show_transparent = False; gl.color = (0.95, 0.75, 0.6, 1.0); gl.hide_select = True; rig.hide_select = False

# ------------------------------------------------ Kameras: Zeiger (wie im Spiel) und Seite; 2D-Vorlage als Bild neben dem Schloss
def gl2bl(v): return Vector((v.x, -v.z, v.y))
dreh = Matrix.Rotation(math.radians(-40), 3, "Y"); tilt = math.radians(16)
off = gl2bl(dreh @ Vector((0, math.cos(tilt), math.sin(tilt)))); up = gl2bl(dreh @ Vector((0, 0, -1))); mitte = gl2bl(Vector((0, 0, -0.045)))
zz = off.normalized(); yy = (up - zz * up.dot(zz)).normalized(); xx = yy.cross(zz)
cz = bpy.data.objects.new("Ansicht_Zeiger", bpy.data.cameras.new("Ansicht_Zeiger")); sc.collection.objects.link(cz)
cz.data.type = "ORTHO"; cz.data.ortho_scale = 0.45; cz.data.clip_start = 0.05; cz.data.clip_end = 5.0
m = Matrix(((xx.x, yy.x, zz.x, 0), (xx.y, yy.y, zz.y, 0), (xx.z, yy.z, zz.z, 0), (0, 0, 0, 1))); m.translation = mitte + zz * 0.8; cz.matrix_world = m
sc.camera = cz
cs = bpy.data.objects.new("Ansicht_Seite", bpy.data.cameras.new("Ansicht_Seite")); sc.collection.objects.link(cs)
cs.data.type = "ORTHO"; cs.data.ortho_scale = 0.45; cs.data.clip_start = 0.05; cs.data.clip_end = 5.0
vs = Vector((-1.0, 0.0, 0.0)).normalized(); cs.location = mitte + vs; cs.rotation_euler = (-vs).to_track_quat("-Z", "Y").to_euler()
sc.render.resolution_x = sc.render.resolution_y = 1000
img = bpy.data.images.load(os.path.abspath(vorlage)); img.pack()
ref = bpy.data.objects.new("Vorlage_2D_Schloss", None); ref.empty_display_type = "IMAGE"; ref.data = img; ref.empty_display_size = 0.17
ref.matrix_world = Matrix(((xx.x, yy.x, zz.x, 0), (xx.y, yy.y, zz.y, 0), (xx.z, yy.z, zz.z, 0), (0, 0, 0, 1)))
ref.location = mitte + xx * (-0.17) + yy * 0.02 - zz * 0.3; ref.hide_select = True
sc.collection.objects.link(ref)
bpy.context.view_layer.update()

# ------------------------------------------------ Bildschirm: zwei 3D-Ansichten (links Zeiger, rechts Seite), Pose-Modus
geteilt = False
try:
    win = bpy.context.window_manager.windows[0]; scr = win.screen
    flaechen = [a for a in scr.areas if a.type == "VIEW_3D"]
    if flaechen:
        with bpy.context.temp_override(window=win, screen=scr, area=flaechen[0]):
            bpy.ops.screen.area_split(direction="VERTICAL", factor=0.5)
        geteilt = True
except Exception as e: print("TEILEN-FEHLER", e)
views = []
fenster = bpy.context.window_manager.windows[0].screen
for scr in bpy.data.screens:
    for ar in scr.areas:
        if ar.type != "VIEW_3D": continue
        for sp in ar.spaces:
            if sp.type != "VIEW_3D": continue
            sp.shading.type = "SOLID"; sp.shading.color_type = "TEXTURE"; sp.shading.show_xray = False; sp.shading.show_object_outline = False
            sp.overlay.show_bones = True; sp.overlay.show_text = True; sp.clip_end = 5.0; sp.show_gizmo = True
            sp.region_3d.view_perspective = "CAMERA"
            if scr == fenster: views.append((ar.x, sp))
views.sort(key=lambda t: t[0])
if len(views) >= 2:
    views[1][1].use_local_camera = True; views[1][1].camera = cs
print("ANSICHTEN", len(views), "geteilt", geteilt)
rig.select_set(True); bpy.context.view_layer.objects.active = rig
try:
    bpy.ops.object.mode_set(mode="POSE"); rig.data.bones.active = rig.data.bones["Wurzel"]; print("MODUS", rig.mode)
except Exception as e: print("MODUS-FEHLER", e)

try: bpy.ops.file.pack_all()
except Exception as e: print("PACK-FEHLER", e)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(aus, "klinke_bearbeiten.blend"), compress=True)
print("GESPEICHERT", json.dumps(ausgang))
print("SCHLOSS im Handraum", [round(v, 4) for v in g.LAM.to_translation()], "Drehung", round(math.degrees(g.LAM.to_euler("XYZ").z), 1))
