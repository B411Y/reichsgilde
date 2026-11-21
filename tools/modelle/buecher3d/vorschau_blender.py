"""Zwei Vorschaubilder eines Buchs aus seiner .blend (Sichtpruefung vor dem Einbau, nicht im Spiel):
  blender -b <ausordner>/buch_<art>.blend --python vorschau_blender.py -- <zielordner> <art>
Schreibt vorschau_<art>_zu.png (liegt zu auf dem Tisch, schraeg von vorn) und vorschau_<art>_offen.png (aufgeschlagen, von oben vorn).
Cycles (GPU, wenn CUDA da ist). Der Umriss ist im Spiel eine invertierte Huelle mit Rueckseiten-Culling; Cycles kennt kein Culling,
darum wird das Material "Umriss" hier nur fuer das Bild so umgebaut, dass die dem Blick zugewandte Seite durchsichtig ist."""
import math
import os
import sys

import bpy
from mathutils import Vector

ziel, art = sys.argv[sys.argv.index("--") + 1:][:2]
sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.samples = 64
sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 1280, 800
sc.render.film_transparent = False
sc.view_settings.view_transform = "Standard"   # Farben wie im Spiel, keine Filmkurve
try:
    pr = bpy.context.preferences.addons["cycles"].preferences
    for art_gpu in ("OPTIX", "CUDA"):
        pr.compute_device_type = art_gpu
        pr.refresh_devices()
        karten = [d for d in pr.devices if d.type == art_gpu]
        if karten:
            for d in pr.devices:
                d.use = d.type == art_gpu
            sc.cycles.device = "GPU"
            print("VORSCHAU GPU:", art_gpu, [d.name for d in karten])
            break
    else:
        print("VORSCHAU ohne GPU (CPU rechnet)")
except Exception as fehler:  # ohne GPU rechnet die CPU
    print("VORSCHAU ohne GPU:", fehler)

# Umriss: zugewandte Seite der Huelle (ihre Rueckseite) durchsichtig, abgewandte schwarz und unbeleuchtet
um = bpy.data.materials["Umriss"]
um.use_nodes = True
nt = um.node_tree
nt.nodes.clear()
aus = nt.nodes.new("ShaderNodeOutputMaterial")
misch = nt.nodes.new("ShaderNodeMixShader")
geo = nt.nodes.new("ShaderNodeNewGeometry")
schwarz = nt.nodes.new("ShaderNodeEmission")
schwarz.inputs["Color"].default_value = (0.02, 0.012, 0.008, 1)
klar = nt.nodes.new("ShaderNodeBsdfTransparent")
weg = nt.nodes.new("ShaderNodeLightPath")
groesser = nt.nodes.new("ShaderNodeMath")
groesser.operation = "MAXIMUM"
nt.links.new(geo.outputs["Backfacing"], groesser.inputs[0])
nt.links.new(weg.outputs["Is Shadow Ray"], groesser.inputs[1])   # die Huelle wirft keinen Schatten auf die Seiten
nt.links.new(groesser.outputs[0], misch.inputs[0])
nt.links.new(schwarz.outputs[0], misch.inputs[1])
nt.links.new(klar.outputs[0], misch.inputs[2])
nt.links.new(misch.outputs[0], aus.inputs["Surface"])
for o in sc.objects:
    if o.name.startswith("Inhaltsflaeche"):
        o.hide_render = True
    if "Umriss" in o.name:
        o.visible_shadow = False

# Tischplatte (Eiche, schlicht), Kerzenlicht von links, kuehles Fuelllicht
me = bpy.data.meshes.new("Tisch")
me.from_pydata([(-0.9, -0.6, 0), (0.9, -0.6, 0), (0.9, 0.6, 0), (-0.9, 0.6, 0)], [], [(0, 1, 2, 3)])
tisch = bpy.data.objects.new("Tisch", me)
sc.collection.objects.link(tisch)
holz = bpy.data.materials.new("Tischholz")
holz.use_nodes = True
bsdf = holz.node_tree.nodes["Principled BSDF"]
rau = holz.node_tree.nodes.new("ShaderNodeTexNoise")
rau.inputs["Scale"].default_value = 3.0
strecke = holz.node_tree.nodes.new("ShaderNodeMapping")
strecke.inputs["Scale"].default_value = (1.0, 14.0, 1.0)
koord = holz.node_tree.nodes.new("ShaderNodeTexCoord")
rampe = holz.node_tree.nodes.new("ShaderNodeValToRGB")
rampe.color_ramp.elements[0].color = (0.16, 0.09, 0.045, 1)
rampe.color_ramp.elements[1].color = (0.34, 0.21, 0.11, 1)
holz.node_tree.links.new(koord.outputs["Object"], strecke.inputs["Vector"])
holz.node_tree.links.new(strecke.outputs["Vector"], rau.inputs["Vector"])
holz.node_tree.links.new(rau.outputs[0], rampe.inputs[0])
holz.node_tree.links.new(rampe.outputs[0], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.8
me.materials.append(holz)


def licht(name, ort, farbe, watt, groesse):
    d = bpy.data.lights.new(name, "AREA")
    d.energy, d.color, d.size = watt, farbe, groesse
    o = bpy.data.objects.new(name, d)
    o.location = ort
    o.rotation_euler = (Vector((0, 0, 0)) - Vector(ort)).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(o)


licht("Kerze", (-0.55, -0.35, 0.55), (1.0, 0.80, 0.58), 9, 0.25)
licht("Fenster", (0.7, 0.5, 0.9), (0.80, 0.86, 1.0), 7, 0.8)
welt = bpy.data.worlds.new("Welt")
welt.use_nodes = True
welt.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.045, 0.04, 1)
welt.node_tree.nodes["Background"].inputs[1].default_value = 0.6
sc.world = welt

kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
kam.data.lens = 50
sc.collection.objects.link(kam)
sc.camera = kam
arm = bpy.data.objects["Buch_Geruest"]


def bild(name, clip, auge, zielpunkt):
    arm.animation_data.action = bpy.data.actions[clip]
    sc.frame_set(0)
    kam.location = auge
    kam.rotation_euler = (Vector(zielpunkt) - Vector(auge)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(ziel, f"vorschau_{art}_{name}.png")
    bpy.ops.render.render(write_still=True)
    print("VORSCHAU", sc.render.filepath)


bild("zu", "zu", (0.34, -0.62, 0.40), (0.0, -0.03, 0.02))
# offen: das Buch wirft im Bild keinen Schatten (die geschlossenen Huellflaechen ueber den Seiten verschatten sie in Cycles sonst; im Spiel wirft das Buch ohnehin keinen)
for o in sc.objects:
    if o.type == "MESH" and o.name != "Tisch":
        o.visible_shadow = False
bild("offen", "offen", (0.0, -0.50, 0.62), (0.0, -0.02, 0.02))
