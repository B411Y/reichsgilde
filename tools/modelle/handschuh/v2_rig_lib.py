"""Gemeinsame Hilfen: Posen (Achsen aus dem Ruhezustand), Buehne, Rendern. Handraum: Finger +Y, Handruecken +Z, Daumen -X."""
import bpy, math, os
import numpy as np
from mathutils import Vector, Quaternion, Matrix

AX_X = Vector((1, 0, 0)); AX_Y = Vector((0, 1, 0)); AX_Z = Vector((0, 0, 1))

def drehung(rig, knochen, achse, grad):
    r = rig.data.bones[knochen].matrix_local.to_3x3()
    lokal = (r.inverted() @ Vector(achse)).normalized()
    return Quaternion(lokal, math.radians(grad))

def pose_anwenden(rig, d):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion = (1, 0, 0, 0)
    for k, liste in d.items():
        q = Quaternion((1, 0, 0, 0))
        for (ax, w) in liste:
            q = drehung(rig, k, ax, w) @ q
        rig.pose.bones[k].rotation_quaternion = q

def pose_keyen(rig, d, frame):
    pose_anwenden(rig, d)
    for k in d:
        rig.pose.bones[k].keyframe_insert("rotation_quaternion", frame=frame, group=k)

def mischen(a, b, t):
    out = {}
    for k in a:
        out[k] = [(ax, wa + (wb - wa) * t) for (ax, wa), (_, wb) in zip(a[k], b[k])]
    return out

def knochenrichtung(rig, name):
    b = rig.data.bones[name]
    return (b.tail_local - b.head_local).normalized()

def flexachse(rig, name):
    """Achse, um die ein Glied zur Handflaeche (-Z) beugt: Richtung x (-Z)."""
    d = knochenrichtung(rig, name)
    return d.cross(Vector((0, 0, -1))).normalized()

def kipp(rig, k):
    """Neigung des Fingers k (Grundglied) gegen +Y in Grad, positiv zur Daumenseite (-X)."""
    d = knochenrichtung(rig, f"finger{k}-1.R")
    return math.degrees(math.atan2(-d.x, d.y))

def mc_kipp(rig, k):
    d = knochenrichtung(rig, f"metacarpal{k-1}.R")
    return math.degrees(math.atan2(-d.x, d.y))

def finger_pose(rig, d, k, flex, ziel_kipp=None):
    """Finger k=2..5: flex = Grad je Glied (Grund, Mittel, End); ziel_kipp = gewuenschte Neigung nach dem Spreizen."""
    theta = 0.0 if ziel_kipp is None else (ziel_kipp - kipp(rig, k))
    # Spreizen: Drehung um +Z dreht +Y zur Daumenseite (-X) -> positiver Winkel = positive Kipp
    d[f"finger{k}-1.R"] = [(flexachse(rig, f"finger{k}-1.R"), flex[0]), (AX_Z, theta)]
    d[f"finger{k}-2.R"] = [(flexachse(rig, f"finger{k}-2.R"), flex[1])]
    d[f"finger{k}-3.R"] = [(flexachse(rig, f"finger{k}-3.R"), flex[2])]

def daumen_pose(rig, d, swing=0.0, flex=(0, 0, 0), dreh=(0, 0, 0)):
    """Daumen: swing um +Z am Grundglied; flex je Glied; dreh = Drehung um die eigene Achse je Glied. Immer gleich viele Eintraege."""
    for g in (1, 2, 3):
        n = f"finger1-{g}.R"
        lst = [(flexachse(rig, n), flex[g - 1]), (knochenrichtung(rig, n), dreh[g - 1]), (AX_Z, swing if g == 1 else 0.0)]
        d[n] = lst

def leere_pose(rig):
    d = {}
    for b in rig.data.bones:
        d[b.name] = [(AX_X, 0.0)]
    return d

# ---------------------------------------------------------------- Buehne
ANSICHTEN = {"oben": ((0, 0.001, 1), 0.30), "seite": ((1, 0.0, 0.25), 0.30), "schraeg": ((-0.45, -0.30, 1.0), 0.30), "unten": ((0, 0.001, -1), 0.30), "vorn": ((0, 1, 0.15), 0.30), "vorn_unten": ((-0.35, 1, -0.55), 0.30), "knoechel": ((0.2, 1, 0.5), 0.30)}

def buehne(px=384, hintergrund=False):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = sc.render.resolution_y = px
    sc.render.film_transparent = not hintergrund
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("W"); w.use_nodes = True
    bg = w.node_tree.nodes["Background"]; bg.inputs["Color"].default_value = (0.62, 0.60, 0.58, 1); bg.inputs["Strength"].default_value = float(os.environ.get("WELT","1.5"))
    sc.world = w
    sun = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 2.6; sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    fill = bpy.data.objects.new("Fuell", bpy.data.lights.new("Fuell", "SUN")); sc.collection.objects.link(fill)
    fill.data.energy = float(os.environ.get("FUELL","2.2")); fill.rotation_euler = (math.radians(135), math.radians(-10), math.radians(40))
    cam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera")); sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"; sc.camera = cam
    return cam

def ansicht_rendern(cam, name, pfad, mitte=Vector((0, 0.03, 0))):
    d, ortho = ANSICHTEN[name]
    v = Vector(d).normalized()
    cam.data.ortho_scale = float(os.environ.get("ORTHO", ortho))
    cam.location = mitte + v * 1.0
    cam.rotation_euler = (-v).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.render.filepath = pfad
    bpy.ops.render.render(write_still=True)



def ss_(x):
    x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)

def gestaffelt(von, nach, t, schliessen=True):
    """Pose zwischen von und nach, Finger zeitversetzt (Zeiten aus den Videos: MediaPipe-Kurven + Bildanalyse).
    Schliessen: Mittel und Klein beginnen zuerst, Ring knapp danach, Zeige etwas spaeter, Klein und Ring brauchen am laengsten, Daumen zuletzt.
    Oeffnen: Daumen zuerst, dann Zeige, Mittel, Ring, Klein zuletzt."""
    d = {}
    SC = {"2": (0.10, 0.62), "3": (0.00, 0.60), "4": (0.02, 0.72), "5": (0.02, 0.80)}
    SO = {"2": (0.05, 0.60), "3": (0.12, 0.60), "4": (0.18, 0.62), "5": (0.24, 0.66)}
    for n in von:
        if n.startswith("finger1"):
            s, dur = (0.25, 0.60) if schliessen else (0.0, 0.50)
        elif n[:6] == "finger" and n[6] in "2345":
            s, dur = (SC if schliessen else SO)[n[6]]
            if schliessen and n[8] == "1": s, dur = s + 0.30, 0.65     # Grundgelenk folgt den Mittel-/Endgliedern (Video)
            if (not schliessen) and n[8] == "1": dur = dur * 0.8
        else:
            s, dur = 0.05, 0.75
        p = ss_((t - s) / dur)
        d[n] = [(ax, wa + (wb - wa) * p) for (ax, wa), (_, wb) in zip(von[n], nach[n])]
    return d
