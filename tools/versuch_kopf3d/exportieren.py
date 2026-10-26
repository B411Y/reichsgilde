# Kopf (Brustbild) als glTF mit Formreglern (Shape Keys) für den Godot-Test exportieren.
# ~/werkzeuge/mpfb/blender_mpfb.sh -b --python exportieren.py -- --dna=<json> --aus=<datei.glb>
# Shape Keys: kiefer_breit, kiefer_schmal, nase_gross (Formregler zum Verändern im Spiel),
#             eyeBlinkLeft, eyeBlinkRight (Blinzeln, ARKit-Gesichtseinheit aus faceunits01).
# Das Kurvenhaar des Haar-Editors (Bart) lässt sich nicht als glTF ausgeben; hier nur Netz-Haar.
import sys, os, json, bpy, bmesh
from importlib import import_module
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mensch as M

FaceModul = import_module(M.MP + ".services.faceservice")
ExportService = M._klasse("services.exportservice", "ExportService")

a = M.argumente()
dna = json.load(open(a["dna"]))
dna["hauttyp"] = "GAMEENGINE"
M.szene_leeren()
b = M.mensch_bauen(dna, unterteilung=0, mit_haareditor=False)

# 1. Aussehen aus der DNA fest einbacken, danach nur die Regler fürs Spiel als Shape Keys
M.TargetService.bake_targets(b)
SPIEL_REGLER = [("chin", "chin-width-incr", "kiefer_breit"), ("chin", "chin-width-decr", "kiefer_schmal"),
                ("nose", "nose-volume-incr", "nase_gross"),
                ("faceunits", "eyeBlinkLeft", "eyeBlinkLeft"), ("faceunits", "eyeBlinkRight", "eyeBlinkRight")]
for gruppe, datei, name in SPIEL_REGLER:
    M.TargetService.load_target(b, M._ziel_pfad(gruppe, datei), weight=0.0, name=name)

# 2. Shape Keys auf Brauen, Wimpern, Haar, Kleidung übertragen (MPFB übernimmt nur bekannte Namen)
for _, _, name in SPIEL_REGLER:
    if name not in FaceModul.ARKIT_FACEUNITS:
        FaceModul.ARKIT_FACEUNITS.append(name)
FaceModul.FaceService.interpolate_targets(b)

# 3. Hilfsgeometrie entfernen, Masken einbacken
ExportService.bake_modifiers_remove_helpers(b, bake_masks=True, bake_subdiv=False, remove_helpers=True)

# 4. Nur Brustbild: alles unterhalb der Brust weg (Netz und Kleidung)
schnitt = M.gelenk(b, "joint-neck").z - 0.32 if "joint-neck" in b.vertex_groups else 1.2
for o in M.objekte(b):
    if o.type != "MESH":
        continue
    bm = bmesh.new()
    bm.from_mesh(o.data)
    weg = [v for v in bm.verts if (o.matrix_world @ v.co).z < schnitt]
    bmesh.ops.delete(bm, geom=weg, context="VERTS")
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()

# Augen: prozedurales Material lässt sich nicht als glTF ausgeben -> einfaches Material mit Augen-Textur
augen = M.asset_objekt(b, "Eyes")
if augen:
    mat = bpy.data.materials.new("Augen")
    mat.use_nodes = True
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(M.NUTZERDATEN, "eyes", "materials", dna.get("augen_textur", "brown") + "_eye.png"))
    p = mat.node_tree.nodes["Principled BSDF"]
    mat.node_tree.links.new(tex.outputs["Color"], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.15
    augen.data.materials.clear()
    augen.data.materials.append(mat)

# Texturen auf höchstens 1024 px verkleinern (Spiel-Größe statt 4K)
groesste = int(a.get("textur", 1024))
for img in bpy.data.images:
    if img.size[0] > groesste:
        f = groesste / img.size[0]
        img.scale(groesste, max(1, int(img.size[1] * f)))

# Zahlen fürs Protokoll
gesamt = 0
for o in M.objekte(b):
    if o.type == "MESH":
        o.data.calc_loop_triangles()
        n = len(o.data.loop_triangles)
        gesamt += n
        keys = [k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else []
        print(f"NETZ {o.name}: {len(o.data.vertices)} Punkte, {n} Dreiecke, Shape Keys {keys}")
print(f"NETZ gesamt {gesamt} Dreiecke")

# 5. Export
bpy.ops.object.select_all(action="DESELECT")
for o in M.objekte(b):
    if o.type == "MESH":
        o.select_set(True)
bpy.context.view_layer.objects.active = b
bpy.ops.export_scene.gltf(filepath=a["aus"], export_format="GLB", use_selection=True,
                          export_morph=True, export_morph_normal=True, export_apply=False,
                          export_skins=False, export_animations=False, export_yup=True)
print("EXPORT", a["aus"], os.path.getsize(a["aus"]))
