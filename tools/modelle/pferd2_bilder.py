"""Neustart Zugpferd: Bilder eines Modells (Original oder Ergebnis) in festen Ansichten mit Bodenlinie und Massstab, gleiche Kamera fuer beide.

    blender -b --python tools/modelle/pferd2_bilder.py -- --modell=<scene.gltf|.glb> --aus=<Ordner> --name=<vorher|nachher> [--s=1.0] [--ansichten=seite,schraeg,vorn,kopf]
                                                          [--klip=<Name>] [--bild=<Nr>] [--proben=24] [--b=960] [--h=720]

--s skaliert das Modell nach dem Laden (fuer den Formvergleich: Original mit 1.18 auf dieselbe Hoehe wie das Ergebnis). Die Hoehenlatten stehen neben dem Pferd:
alle 10 cm eine Marke, dunkel alle 50 cm, rot bei 1,0 m und 1,8 m; die Bodenlinie liegt auf z = 0, Bodenstreifen alle 50 cm quer.
Es wird nur gerendert (Cycles, CPU, 2 Threads), nichts ins Spiel geschrieben.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS)
S = float(OPT.get("s", 1.0))

# Ansicht: (Ziel, Azimut Grad, Hoehe Grad, Massstab m (Breite), Latte seitlich in m vom Ziel, Latte in der Tiefe)
ANSICHTEN = {
    "seite": ((0.0, 0.0, 0.98), 0, 0, 4.0, -1.85),
    "schraeg": ((0.0, 0.0, 1.0), 35, 12, 4.4, -1.95),
    "vorn": ((0.0, 0.0, 1.0), -90, 0, 3.0, -1.2),
    "kopf": ((0.0, -1.05, 1.65), 0, 0, 1.4, -0.62),
    "maehne": ((0.0, -0.9, 1.7), 0, 0, 0.9, -0.42),
    "spiel": ((0.0, 0.0, 0.8), 40, 38, 8.0, -3.6),
    "huf": ((0.0, 0.0, 0.1), 35, 12, 0.55, -0.9),
    "mensch": ((0.0, -0.45, 0.95), 0, 0, 4.0, -2.45),
}


def laden(pfad, s):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=pfad)
    for o in list(bpy.data.objects):
        if o.type == "MESH" and o.name == "Icosphere":
            bpy.data.objects.remove(o)
    ske = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    ob = next(o for o in bpy.data.objects if o.type == "MESH")
    ske.animation_data_create()
    if "klip" in OPT:
        act = bpy.data.actions[OPT["klip"]]
        ske.animation_data.action = act
        sl = getattr(act, "slots", None)
        if sl and len(sl):
            ske.animation_data.action_slot = sl[0]
        bpy.context.scene.frame_set(int(OPT.get("bild", 0)))
    else:
        ske.animation_data.action = None
        ske.data.pose_position = "REST"
    # Skalierung ueber das Skelettobjekt (das Netz haengt daran)
    ske.scale = (s, s, s)
    bpy.context.view_layer.update()
    e = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = e.to_mesh()
    zmin = min((e.matrix_world @ v.co).z for v in me.vertices)
    e.to_mesh_clear()
    ske.location.z -= zmin
    bpy.context.view_layer.update()
    return ske, ob


def kasten(name, mitte, groesse, farbe, leuchten=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    o.location = mitte
    o.scale = groesse
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = farbe
    if leuchten:
        b.inputs["Emission Color"].default_value = farbe
        b.inputs["Emission Strength"].default_value = leuchten
    o.data.materials.append(m)
    bpy.context.scene.collection.objects.link(o)
    return o


def buehne():
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    szene.cycles.device = "CPU"
    szene.render.threads_mode = "FIXED"
    szene.render.threads = 2
    szene.cycles.samples = int(OPT.get("proben", 24))
    szene.cycles.use_denoising = False
    szene.view_settings.view_transform = "Standard"
    sonne = bpy.data.objects.new("_Sonne", bpy.data.lights.new("_Sonne", "SUN"))
    sonne.data.energy = 3.2
    sonne.data.color = (1.0, 0.94, 0.84)
    sonne.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    szene.collection.objects.link(sonne)
    welt = bpy.data.worlds.new("Himmel")
    welt.use_nodes = True
    hg = welt.node_tree.nodes["Background"]
    hg.inputs["Color"].default_value = (0.62, 0.66, 0.72, 1)
    hg.inputs["Strength"].default_value = 0.9
    szene.world = welt
    kasten("_Boden", (0, 0, -0.01), (40, 40, 0.02), (0.30, 0.28, 0.22, 1))
    for i in range(-10, 11):                                    # Streifen alle 0,5 m quer und laengs
        kasten(f"_sq{i}", (i * 0.5, 0, 0.001), (0.01, 40, 0.003), (0.12, 0.12, 0.10, 1))
        kasten(f"_sl{i}", (0, i * 0.5, 0.001), (40, 0.01, 0.003), (0.12, 0.12, 0.10, 1))
    kasten("_bodenlinie", (0, 0, 0.0), (40, 0.02, 0.006), (0.0, 0.0, 0.0, 1))
    kam = bpy.data.objects.new("_Kam", bpy.data.cameras.new("_Kam"))
    szene.collection.objects.link(kam)
    szene.camera = kam
    return kam


def latte(az, lateral, tiefe_versatz=0.0):
    """Hoehenlatte (alle 10 cm eine Marke) seitlich vom Pferd in der Bildebene der Ansicht az."""
    a = math.radians(az)
    rechts = Vector((-math.sin(a), math.cos(a), 0))
    zur_kam = Vector((math.cos(a), math.sin(a), 0))
    basis = rechts * lateral + zur_kam * tiefe_versatz
    gruppe = []
    gruppe.append(kasten("_lm", (basis.x, basis.y, 1.0), (0.012 if abs(rechts.x) > 0.5 else 0.012, 0.012, 2.0), (0.9, 0.9, 0.85, 1)))
    for i in range(0, 21):
        z = i * 0.1
        gross = i % 5 == 0
        farbe = (0.9, 0.05, 0.05, 1) if i in (10, 18) else ((0.0, 0.0, 0.0, 1) if gross else (0.15, 0.15, 0.15, 1))
        laenge = 0.20 if gross else 0.10
        # Marke quer zur Latte in der Bildebene
        mitte = basis + rechts * (laenge / 2)
        gr = (abs(rechts.x) * laenge + 0.026 * (1 - abs(rechts.x)), abs(rechts.y) * laenge + 0.026 * (1 - abs(rechts.y)), 0.012)
        kasten(f"_lt{i}", (mitte.x, mitte.y, z), gr, farbe, 1.0)


def aufnahme(kam, ziel, az, el, mass, datei, b, h):
    szene = bpy.context.scene
    szene.render.resolution_x, szene.render.resolution_y = b, h
    kam.data.type = "ORTHO"
    kam.data.ortho_scale = mass
    kam.data.clip_end = 500
    a, e = math.radians(az), math.radians(el)
    kam.location = ziel + Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))) * 40
    kam.rotation_euler = (ziel - kam.location).normalized().to_track_quat("-Z", "Y").to_euler()
    szene.render.filepath = datei
    bpy.ops.render.render(write_still=True)


def main():
    ske, ob = laden(OPT["modell"], S)
    kam = buehne()
    os.makedirs(OPT["aus"], exist_ok=True)
    b, h = int(OPT.get("b", 960)), int(OPT.get("h", 720))
    for an in OPT.get("ansichten", "seite,schraeg,vorn,kopf").split(","):
        ziel, az, el, mass, lat = ANSICHTEN[an]
        for o in [o for o in bpy.data.objects if o.name.startswith(("_lm", "_lt"))]:
            bpy.data.objects.remove(o)
        latte(az, lat)
        z = Vector(ziel)
        if an == "kopf":
            pass
        aufnahme(kam, z, az, el, mass, os.path.join(OPT["aus"], f"{OPT['name']}_{an}" + (f"_{OPT['klip']}_{int(OPT.get('bild', 0)):03d}" if "klip" in OPT else "") + ".png"), b, h)


if __name__ == "__main__":
    main()




