"""Rechenvorschau der Zeiger-Symbole (kein Spielbild): rendert in der Zeigeransicht des Spiels (Kamera wie HandschuhZeiger.Buehne: orthogonal 0,45 m,
-40 Grad um die Hochachse, 16 Grad Neigung) die Hand in der Haltung Zeigen mit der Windrose am Platz des Fragezeichens (Hilfe_Marke mit MarkeFaktor 1,9
und MarkeVersatz wie im Spiel), die Hand beim Schrumpfen um die Fingerspitze und das roemische I beim Wachsen (Kurve Smoothstep, 0,2 s, Zeitpunkte i/60 s),
dazu Nahaufnahmen. Bilder mit durchsichtigem Grund; Zusammensetzen auf Hintergruende, Textfeld-Attrappe und GIF macht zeiger_symbole_blatt.py.

Aufruf (Blender -b, Pfade absolut):
  blender -b --factory-startup --python zeiger_symbole_bilder.py -- <handschuh_zeiger.glb> <zeiger_symbole.glb> <ausordner>
Schreibt <ausordner>/roh/*.png und <ausordner>/roh/punkte.json (Zeigerpunkt je Bild in Pixeln: Fingerspitze bzw. Mitte des I-Balkens).
"""
import bpy, os, sys, json, math
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view

args = sys.argv[sys.argv.index("--") + 1:]
hand_glb, sym_glb, aus = args[0], args[1], args[2]
roh = os.path.join(aus, "roh"); os.makedirs(roh, exist_ok=True)
MARKE_FAKTOR = 1.9
MARKE_VERSATZ_GL = Vector((-0.008, 0.0, 0.018))
SYMBOL_DAUER, FPS = 0.2, 60


def sanft(t):
    t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)


bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
vorher = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=hand_glb)
hand_obj = [o for o in bpy.data.objects if o not in vorher]
vorher = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=sym_glb)
sym = {o.name: o for o in bpy.data.objects if o not in vorher}
print("SYMBOLE", sorted(sym), flush=True)

# Haltung Zeigen, Bild 0 (wie HandschuhZeiger.ModellLaden fuer den Zeigerpunkt)
rig = next(o for o in hand_obj if o.type == "ARMATURE")
zeigen = next(a for a in bpy.data.actions if a.name == "Zeigen" or a.name.startswith("Zeigen"))
rig.animation_data_create(); rig.animation_data.action = zeigen
if hasattr(rig.animation_data, "action_slot") and len(getattr(zeigen, "slots", [])) > 0:
    rig.animation_data.action_slot = zeigen.slots[0]
sc.frame_set(0); bpy.context.view_layer.update()
wurzel_hand = next(o for o in hand_obj if o.parent is None)


def gl2bl(v):
    return Vector((v.x, -v.z, v.y))


# Fragezeichen wie im Spiel vergroessern und versetzen (Position im Elternraum, glTF -> Blender), dann die Windrose an genau diese Lage
marken = [bpy.data.objects[n] for n in ("Hilfe_Marke", "Hilfe_Marke_Umriss")]
for m in marken:
    m.scale = m.scale * MARKE_FAKTOR
    m.location = m.location + gl2bl(MARKE_VERSATZ_GL)
bpy.context.view_layer.update()
MARKE_PUNKT = marken[0].matrix_world.translation.copy()

# Kamera wie im Spiel
cam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"
dreh = Matrix.Rotation(math.radians(-40), 3, "Y"); tilt = math.radians(16)
off = gl2bl(dreh @ Vector((0, math.cos(tilt), math.sin(tilt)))); up = gl2bl(dreh @ Vector((0, 0, -1))); MITTE = gl2bl(Vector((0, 0, -0.045)))
z = off.normalized(); y = (up - z * up.dot(z)).normalized(); x = y.cross(z)
KAM = Matrix(((x.x, y.x, z.x, 0), (x.y, y.y, z.y, 0), (x.z, y.z, z.z, 0), (0, 0, 0, 1))); KAM.translation = MITTE + z * 0.8
cam.matrix_world = KAM; cam.data.ortho_scale = 0.45; cam.data.clip_start = 0.05; cam.data.clip_end = 3.0
# Lichter an der Kamera wie HandschuhZeiger.Buehne (Godot-Euler YXZ), Umgebungslicht; Staerken nur angenaehert
for rot, farbe, staerke in (((-42, 32, 0), (1.0, 0.93, 0.82), 1.25), ((-18, -52, 0), (0.82, 0.88, 1.0), 0.55)):
    li = bpy.data.objects.new("Licht", bpy.data.lights.new("Licht", "SUN")); sc.collection.objects.link(li)
    li.data.color = farbe; li.data.energy = staerke * 3.0
    li.parent = cam
    li.matrix_basis = (Matrix.Rotation(math.radians(rot[1]), 4, "Y") @ Matrix.Rotation(math.radians(rot[0]), 4, "X"))
welt = bpy.data.worlds.new("Welt"); sc.world = welt; welt.use_nodes = True
hg = next(n for n in welt.node_tree.nodes if n.type == "BACKGROUND"); hg.inputs[0].default_value = (0.62, 0.60, 0.58, 1); hg.inputs[1].default_value = 0.7
try:
    sc.render.engine = "BLENDER_EEVEE"
except TypeError as e:
    print("Engine:", e)
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"

# Das I haengt an der Kamera, 0,8 m vor ihr (Mitte der Buehne), zum Betrachter gedreht (glTF +Y = Bild oben)
I_LAGE = KAM @ Matrix.Translation((0, 0, -0.8)) @ Matrix.Rotation(math.radians(-90), 4, "X")
for n in ("Text_I", "Text_I_Umriss"):
    sym[n].matrix_world = I_LAGE
# Die Windrose steht am Platz des Fragezeichens (Mitte der Hilfe_Marke), aber zum Betrachter gedreht (Spitzen genau oben/rechts/unten/links)
# und ROSE_VOR Meter zur Kamera hin geschoben, damit die Hand sie nicht verdeckt (orthogonal: Lage und Groesse im Bild bleiben gleich).
ROSE_VOR = 0.12
ROSE_LAGE = Matrix.Translation(MARKE_PUNKT + z * ROSE_VOR) @ KAM.to_3x3().to_4x4() @ Matrix.Scale(MARKE_FAKTOR, 4)
for n in ("Windrose", "Windrose_Umriss"):
    sym[n].matrix_world = ROSE_LAGE

anker = bpy.data.objects["Anker_Spitze"]
HAND0 = wurzel_hand.matrix_world.copy()
SPITZE = anker.matrix_world.translation.copy()
punkte = {}


def zeige(hand=1.0, rose=False, frage=False, text_i=0.0):
    s = max(hand, 0.001)
    wurzel_hand.matrix_world = Matrix.Translation(SPITZE) @ Matrix.Scale(s, 4) @ Matrix.Translation(-SPITZE) @ HAND0
    for o in hand_obj:
        if o.type == "MESH": o.hide_render = hand < 0.001
    for m in marken: m.hide_render = not frage or hand < 0.001
    for n in ("Windrose", "Windrose_Umriss"): sym[n].hide_render = not rose or hand < 0.001
    for n in ("Text_I", "Text_I_Umriss"):
        sym[n].hide_render = text_i < 0.001
        sym[n].matrix_world = I_LAGE @ Matrix.Scale(max(text_i, 0.001), 4)
    bpy.context.view_layer.update()


def rendern(name, px, punkt_welt):
    sc.render.resolution_x = sc.render.resolution_y = px
    sc.render.filepath = os.path.join(roh, name + ".png")
    bpy.ops.render.render(write_still=True)
    p = world_to_camera_view(sc, cam, punkt_welt)
    punkte[name] = [p.x * px, (1 - p.y) * px]


I_MITTE = I_LAGE.translation.copy()
# Kacheln: Zeigergroesse 64 und 96 -> Bild 2,0 Zeigergroessen, gerendert mit Superabtastung 2
for g in (64, 96):
    px = int(g * 2.0 * 2)
    zeige(rose=True); rendern(f"windrose_{g}", px, SPITZE)
    zeige(frage=True); rendern(f"fragezeichen_{g}", px, SPITZE)
    zeige(); rendern(f"hand_{g}", px, SPITZE)
    zeige(hand=0.0, text_i=1.0); rendern(f"text_i_{g}", px, I_MITTE)
# Uebergang (Zeigergroesse 64): Hand schrumpft 0,2 s, dann waechst das I 0,2 s; Zeitpunkte i/60 s. Rueckweg = dieselben Bilder rueckwaerts.
n = int(round(SYMBOL_DAUER * FPS))
for i in range(n + 1):
    zeige(hand=1 - sanft(i / n)); rendern(f"folge_hand_{i:02d}", 256, SPITZE)
    zeige(hand=0.0, text_i=sanft(i / n)); rendern(f"folge_i_{i:02d}", 256, I_MITTE)
# Nahaufnahmen: Kamera auf das Symbol verschoben, Sichtfeld klein
for name, obj, feld, setz in (("nah_windrose", sym["Windrose"], 0.16, dict(rose=True)), ("nah_text_i", sym["Text_I"], 0.16, dict(hand=0.0, text_i=1.0))):
    zeige(**setz)
    for o in hand_obj:
        if o.type == "MESH": o.hide_render = True
    mitte = obj.matrix_world.translation
    cam.matrix_world = Matrix.Translation(mitte - MITTE) @ KAM; cam.data.ortho_scale = feld
    rendern(name, 512, mitte)
    cam.matrix_world = KAM; cam.data.ortho_scale = 0.45
json.dump({"punkte": punkte, "folge_bilder": n + 1, "fps": FPS, "dauer_s": SYMBOL_DAUER}, open(os.path.join(roh, "punkte.json"), "w"), indent=1)
print("FERTIG", len(punkte), "Bilder", flush=True)
