"""Baut die Arbeitsdatei, in der man die Text-Haltung des Handschuhs SELBST in Blender einstellt.
  blender -b handschuh_pixal3d_v6.blend --python text_selbst_bauen.py -- <ausordner> <params.json> <foto.jpg>
Ergebnis in <ausordner>: text_haltung_bearbeiten.blend (alles eingepackt), bild_knochen.png (Beschriftung), achsen.json (Wirkung der Eulerachsen).
Die Datei braucht keine Add-ons: Pose-Modus, nur Daumenknochen sichtbar und waehlbar, Pose als Aktion "Text_Haltung" auf Bild 1, Euler XYZ,
Kamera "Ansicht_Vergleich" (wie im Vergleichsbild) und das Foto der Nutzerhand als Bild-Empty daneben.
Danach: text_aus_blend.py liest die fertige Datei zurueck."""
import bpy, sys, os, json, math, shutil
import numpy as np
from mathutils import Vector, Matrix, Euler
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
args = sys.argv[sys.argv.index("--") + 1:]
aus, pjson, foto = args[0], args[1], args[2]
os.makedirs(aus, exist_ok=True)
os.environ["TEXT"] = open(pjson, encoding="utf-8").read()
import v6_rig_lib as L
import v6_text_lib as T
from bpy_extras.object_utils import world_to_camera_view
rig, gl = T.rig, T.gl
sc = bpy.context.scene

# ------------------------------------------------ Aufraeumen: alte Aktionen weg (nur in dieser Arbeitsdatei), Hilfsobjekte ausblenden
rig.animation_data_create(); rig.animation_data.action = None
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
for o in bpy.data.objects:
    if o.name.startswith(("Hilfe_Marke", "Anker_")) or o.name == "Handschuh_Umriss":
        o.hide_viewport = True
        if o.name.startswith("Hilfe_Marke"): o.hide_render = True

# ------------------------------------------------ Text-Pose als Aktion, Euler XYZ, Bild 1
L.pose_anwenden(rig, T.PT); bpy.context.view_layer.update()
werte = {pb.name: (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in rig.pose.bones}
for pb in rig.pose.bones:
    q, loc = werte[pb.name]
    pb.rotation_mode = "XYZ"; pb.rotation_euler = q.to_euler("XYZ"); pb.location = loc; pb.scale = (1, 1, 1)
act = bpy.data.actions.new("Text_Haltung"); act.use_fake_user = True; rig.animation_data.action = act
for pb in rig.pose.bones:
    pb.keyframe_insert("rotation_euler", frame=1, group=pb.name)
    if pb.name.startswith("Knoechel"): pb.keyframe_insert("location", frame=1, group=pb.name)
sc.frame_start = sc.frame_end = 1; sc.frame_set(1)
try: sc.tool_settings.use_keyframe_insert_auto = True   # Automatisch Schluesselbilder: jede Drehung bleibt beim Speichern/Neuoeffnen erhalten
except Exception as e: print("AUTOKEY-FEHLER", e)
bpy.context.view_layer.update()

# ------------------------------------------------ Knochen: nur Daumen sichtbar/waehlbar, Rest versteckt und gesperrt
DAUMEN = ("finger1-1.R", "finger1-2.R", "finger1-3.R")
ALLE = os.environ.get("ALLE") == "1"   # zweite Datei: alle Knochen sichtbar/waehlbar (nur Drehen), Netz deckend
for b in rig.data.bones:
    ist = ALLE or b.name in DAUMEN
    b.hide = not ist; b.hide_select = not ist
for pb in rig.pose.bones:
    if pb.name in DAUMEN or ALLE:
        pb.lock_location = (True, True, True); pb.lock_scale = (True, True, True)
        if pb.name.startswith("Knoechel"): pb.lock_rotation = (True, True, True)   # Hilfsknochen der Knoechelreihe: nur per Verschiebung gesteuert, nicht drehen
    if pb.name in DAUMEN:
        pb.color.palette = {"finger1-1.R": "THEME01", "finger1-2.R": "THEME04", "finger1-3.R": "THEME09"}[pb.name]
rig.data.show_names = True; rig.data.display_type = "OCTAHEDRAL"; rig.show_in_front = True
gl.show_transparent = not ALLE; gl.color = (0.95, 0.75, 0.6, 1.0 if ALLE else 0.35)
gl.hide_select = True; rig.hide_select = False

# ------------------------------------------------ Kamera wie im Vergleichsbild (Ansicht oben) und Foto daneben
cam = bpy.data.objects.new("Ansicht_Vergleich", bpy.data.cameras.new("Ansicht_Vergleich")); sc.collection.objects.link(cam)
cam.data.type = "ORTHO"; sc.camera = cam; sc.render.resolution_x = sc.render.resolution_y = 1000   # quadratisches Bild: Kamerarahmen = Ansicht
v = Vector((0, 0.001, 1)).normalized()
cam.rotation_euler = (-v).to_track_quat("-Z", "Y").to_euler(); cam.location = v * 1.0
bpy.context.view_layer.update()
M = cam.matrix_world.to_3x3(); RE, UP, TO = M @ Vector((1, 0, 0)), M @ Vector((0, 1, 0)), M @ Vector((0, 0, 1))   # rechts, oben, zum Betrachter
dg = bpy.context.evaluated_depsgraph_get(); me = gl.evaluated_get(dg).to_mesh()
Vw = np.array([(gl.matrix_world @ p.co)[:] for p in me.vertices]); R_ = np.array(RE[:]); U_ = np.array(UP[:])
hand = Vw[np.abs(Vw[:, 1]) > 0.045]
x0, x1 = float((hand @ R_).min()), float((hand @ R_).max()); y0, y1 = float((hand @ U_).min()), float((hand @ U_).max())
tipp = np.array((rig.matrix_world @ rig.pose.bones["finger3-3.R"].tail)[:]); wr = np.array((rig.matrix_world @ rig.pose.bones["wrist.R"].head)[:])
laenge = float(np.linalg.norm(tipp - wr))
img = bpy.data.images.load(os.path.abspath(foto)); img.pack()
S = laenge / 0.73                                   # Bildgroesse (Hoehe) so, dass die Hand im Foto etwa so lang ist wie die Handschuhhand
tipy = float(tipp @ U_)
cx = x0 - 0.03 - 0.375 * S; cy = tipy - S / 2 + 0.06 * S
ref = bpy.data.objects.new("Referenz_Foto", None); ref.empty_display_type = "IMAGE"; ref.data = img; ref.empty_display_size = S
ref.rotation_euler = cam.rotation_euler; ref.location = RE * cx + UP * cy - TO * 0.2
ref.hide_select = True; ref.show_in_front = False
sc.collection.objects.link(ref)
# Kamera mittig ueber Foto und Hand
bx0 = cx - 0.375 * S; bx1 = x1; by0 = min(y0, cy - S / 2); by1 = max(y1, cy + S / 2)
mitte = RE * ((bx0 + bx1) / 2) + UP * ((by0 + by1) / 2)
cam.location = mitte + v * 1.0; cam.data.ortho_scale = max(bx1 - bx0, by1 - by0) * 1.08
bpy.context.view_layer.update()
cam.data.clip_end = 5.0
ecken = [world_to_camera_view(sc, cam, ref.matrix_world @ Vector(c)) for c in ((-S * 0.375, -S / 2, 0), (S * 0.375, S / 2, 0))]
print("FOTO im Bild (0..1):", [(round(e.x, 2), round(e.y, 2)) for e in ecken])

# ------------------------------------------------ Ansichten im Viewport (nur Voreinstellung, der Nutzer darf alles drehen)
for scr in bpy.data.screens:
    for ar in scr.areas:
        if ar.type != "VIEW_3D": continue
        for sp in ar.spaces:
            if sp.type != "VIEW_3D": continue
            sp.shading.type = "SOLID"; sp.shading.color_type = "TEXTURE" if ALLE else "OBJECT"; sp.shading.show_xray = False; sp.shading.show_object_outline = False
            sp.overlay.show_bones = True; sp.overlay.show_text = True
            sp.region_3d.view_perspective = "CAMERA"; sp.clip_end = 5.0; sp.show_gizmo = True
rig.select_set(True); bpy.context.view_layer.objects.active = rig
try:
    bpy.ops.object.mode_set(mode="POSE")
    rig.data.bones.active = rig.data.bones["finger1-1.R"]
    print("MODUS", rig.mode)
except Exception as e:
    print("MODUS-FEHLER", e)

# ------------------------------------------------ Wirkung der Eulerachsen (fuer die Anleitung): +20 Grad je Achse, Spitze in Bildkoordinaten
def spitze():
    bpy.context.view_layer.update(); return np.array((rig.matrix_world @ rig.pose.bones["finger1-3.R"].tail)[:])
basis = spitze(); wirk = {}
for n in DAUMEN:
    pb = rig.pose.bones[n]; e0 = pb.rotation_euler.copy()
    for i, ax in enumerate("XYZ"):
        e = e0.copy(); e[i] += math.radians(20); pb.rotation_euler = e
        d = (spitze() - basis) * 1000
        wirk[f"{n} +20 um {ax}"] = {"rechts_mm": round(float(d @ R_), 1), "oben_mm": round(float(d @ U_), 1), "zum_betrachter_mm": round(float(d @ np.array(TO[:])), 1)}
    pb.rotation_euler = e0
bpy.context.view_layer.update()
json.dump(wirk, open(os.path.join(aus, "achsen.json"), "w"), indent=1, ensure_ascii=False)
print("ACHSEN", json.dumps(wirk, ensure_ascii=False))
euler = {n: [round(math.degrees(a), 1) for a in rig.pose.bones[n].rotation_euler] for n in DAUMEN}
print("EULER Daumen", euler)

# ------------------------------------------------ Speichern (alles einpacken)
try: bpy.ops.file.pack_all()
except Exception as e: print("PACK-FEHLER", e)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(aus, ("text_haltung_bearbeiten_alle.blend" if ALLE else "text_haltung_bearbeiten.blend")), compress=True)
print("GESPEICHERT")

# ------------------------------------------------ Beschriftungsbild (nur -b gerendert, KEIN GUI-Screenshot)
sc.render.engine = "BLENDER_EEVEE"
r = L.buehne(900); r.data.ortho_scale = cam.data.ortho_scale; r.location = cam.location.copy(); r.rotation_euler = cam.rotation_euler.copy()
sc.camera = r
for o in bpy.data.objects:
    if o.name.startswith("Hilfe_Marke"): o.hide_render = True
sc.render.filepath = os.path.join(aus, "_render.png"); bpy.ops.render.render(write_still=True)
dat = {n: [list(world_to_camera_view(sc, r, rig.matrix_world @ getattr(rig.pose.bones[n], a))[:2]) for a in ("head", "tail")] for n in DAUMEN}
json.dump(dat, open(os.path.join(aus, "_knochen_bild.json"), "w"))
print("BILD fertig")
