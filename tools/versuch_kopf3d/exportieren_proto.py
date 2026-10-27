# Prototyp Live-3D-Kopf: Kopf mit Formreglern (Kiefer, Nase, Alter) und Sprech-/Blinzel-Formen als glTF.
# ~/werkzeuge/mpfb/blender_mpfb.sh -b --python exportieren_proto.py -- --dna=proto_dna.json --aus=<datei.glb>
import sys, os, json, gzip, bpy, bmesh
import numpy as np
from importlib import import_module
from mathutils import Vector, kdtree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mensch as M

FaceModul = import_module(M.MP + ".services.faceservice")
ExportService = M._klasse("services.exportservice", "ExportService")
a = M.argumente()
dna = json.load(open(a["dna"]))
M.szene_leeren()
b = M.mensch_bauen(dna, unterteilung=0, mit_haareditor=False)
M.TargetService.bake_targets(b)

def ziel_lesen(pfad):
    f = gzip.open(pfad, "rt") if pfad.endswith(".gz") else open(pfad)
    d = {}
    for z in f:
        t = z.split()
        if len(t) == 4 and t[0].isdigit():
            d[int(t[0])] = Vector((float(t[1]), float(t[2]), float(t[3])))
    return d

# MakeHuman-Ziele liegen in Dezimetern im MPFB-Raum; MPFB skaliert beim Laden (SCALE) -> Faktor übernehmen
def skala():
    k = M.TargetService.load_target(b, M._ziel_pfad("faceunits", "jawOpen"), weight=0.0, name="_t")
    d = max((k.data[i].co - b.data.vertices[i].co).length for i in range(len(b.data.vertices)))
    b.shape_key_remove(k)
    ziel = max(v.length for v in ziel_lesen(M._ziel_pfad("faceunits", "jawOpen")).values())
    return d / ziel if ziel else 1.0
S = skala()
print("SKALA", S)

def key_aus_zielen(name, ziele):
    """Ein Shape Key aus mehreren Zielen: ziele = [(gruppe, datei, gewicht)]."""
    basis = b.data.shape_keys.key_blocks["Basis"] if b.data.shape_keys and "Basis" in b.data.shape_keys.key_blocks else None
    k = b.shape_key_add(name=name, from_mix=False)
    for gruppe, datei, w in ziele:
        for i, d in ziel_lesen(M._ziel_pfad(gruppe, datei)).items():
            k.data[i].co += d * S * w
    return k

def key_aus_alter(name, alter):
    """Shape Key aus dem Unterschied zu einem zweiten Menschen mit anderem Alter."""
    d2 = json.loads(json.dumps(dna)); d2["makro"]["age"] = alter
    h = M.HumanService.create_human()
    for s_, v in d2["makro"].items():
        M.HumanObjectProperties.set_value(s_, v, entity_reference=h)
    M.TargetService.reapply_macro_details(h)
    for n, w in d2["regler"].items():
        for pfad, g in M.regler_ziele(n, w):
            M.TargetService.load_target(h, pfad, weight=g)
    M.TargetService.bake_targets(h)
    k = b.shape_key_add(name=name, from_mix=False)
    for i, v in enumerate(h.data.vertices):
        k.data[i].co = v.co
    bpy.data.objects.remove(h, do_unlink=True)
    # Kopf an Ort und Stelle halten: mittlere Verschiebung der Kopfpunkte von allen Punkten abziehen
    kopf = [i for i, v in enumerate(b.data.vertices) if v.co.z > 1.55 and v.co.y < 0.1 and abs(v.co.x) < 0.12]
    mw = sum(((k.data[i].co - b.data.vertices[i].co) for i in kopf), Vector()) / len(kopf)
    for i in range(len(b.data.vertices)):
        k.data[i].co -= mw

if not b.data.shape_keys:
    b.shape_key_add(name="Basis", from_mix=False)
key_aus_zielen("kiefer_breit", [("chin", "chin-width-incr", 1.0), ("chin", "chin-bones-incr", 0.5)])
key_aus_zielen("kiefer_schmal", [("chin", "chin-width-decr", 1.0), ("chin", "chin-bones-decr", 0.5)])
key_aus_zielen("nase_gross", [("nose", "nose-volume-incr", 1.0), ("nose", "nose-scale-vert-incr", 0.6), ("nose", "nose-hump-incr", 0.6)])
key_aus_zielen("nase_klein", [("nose", "nose-volume-decr", 1.0), ("nose", "nose-scale-vert-decr", 0.6), ("nose", "nose-hump-decr", 0.6)])
key_aus_alter("alt", 1.0)
key_aus_alter("jung", 0.3)
SPRECH = ["jawOpen", "mouthFunnel", "mouthPucker", "mouthSmileLeft", "mouthSmileRight", "mouthClose",
          "mouthStretchLeft", "mouthStretchRight", "mouthLowerDownLeft", "mouthLowerDownRight", "mouthUpperUpLeft",
          "mouthUpperUpRight", "eyeBlinkLeft", "eyeBlinkRight", "browInnerUp", "browDownLeft", "browDownRight",
          "eyeLookDownLeft", "eyeLookDownRight", "eyeLookUpLeft", "eyeLookUpRight",
          "eyeLookInLeft", "eyeLookInRight", "eyeLookOutLeft", "eyeLookOutRight"]
for n in SPRECH:
    M.TargetService.load_target(b, M._ziel_pfad("faceunits", n), weight=0.0, name=n)
EIGENE = ["kiefer_breit", "kiefer_schmal", "nase_gross", "nase_klein", "alt", "jung"]
for n in EIGENE:
    if n not in FaceModul.ARKIT_FACEUNITS:
        FaceModul.ARKIT_FACEUNITS.append(n)
FaceModul.FaceService.interpolate_targets(b)

LM = {n: M.gelenk(b, n) for n in ('joint-l-eye','joint-r-eye','joint-mouth','joint-jaw','joint-neck','joint-head')}
ExportService.bake_modifiers_remove_helpers(b, bake_masks=True, bake_subdiv=False, remove_helpers=True)

schnitt = LM["joint-neck"].z - 0.22 if "joint-neck" in b.vertex_groups else 1.3
for o in M.objekte(b):
    if o.type != "MESH":
        continue
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if (o.matrix_world @ v.co).z < schnitt], context="VERTS")
    bm.to_mesh(o.data); bm.free(); o.data.update()

def keys_lesen(o):
    kb = o.data.shape_keys.key_blocks
    n = len(o.data.vertices)
    basis = np.empty(n * 3); kb[0].data.foreach_get("co", basis); basis = basis.reshape(-1, 3)
    d = {}
    for k in kb[1:]:
        c = np.empty(n * 3); k.data.foreach_get("co", c); d[k.name] = c.reshape(-1, 3) - basis
    return basis, d

def keys_uebertragen(ziel, basis, deltas, nachbarn=3):
    """Shape Keys eines Quellnetzes (Basis + Deltas) per Nachbarpunkt-Mischung auf ein anderes Netz legen."""
    kd = kdtree.KDTree(len(basis))
    for i, p in enumerate(basis):
        kd.insert(Vector(p), i)
    kd.balance()
    idx, gew = [], []
    for v in ziel.data.vertices:
        res = kd.find_n(v.co, nachbarn)
        w = np.array([1.0 / (r[2] + 1e-5) for r in res]); w /= w.sum()
        idx.append([r[1] for r in res]); gew.append(w)
    if not idx:
        return
    idx = np.array(idx); gew = np.array(gew)
    if ziel.data.shape_keys is None:
        ziel.shape_key_add(name="Basis", from_mix=False)
    base = np.empty(len(ziel.data.vertices) * 3); ziel.data.vertices.foreach_get("co", base); base = base.reshape(-1, 3)
    for name, dl in deltas.items():
        dz = (dl[idx] * gew[:, :, None]).sum(axis=1)
        k = ziel.shape_key_add(name=name, from_mix=False)
        k.data.foreach_set("co", (base + dz).ravel())

def ausduennen(o, ziel_tris, schuetz_z=None):
    """Netz mit Shape Keys auf etwa ziel_tris Dreiecke verringern (Keys werden danach übertragen).
    Punkte oberhalb schuetz_z (Gesicht) bekommen in der Decimate-Gruppe fast kein Gewicht."""
    basis, d = keys_lesen(o)
    for k in list(o.data.shape_keys.key_blocks)[::-1]:
        o.shape_key_remove(k)
    n0 = tris(o)
    m = o.modifiers.new("Duenn", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.use_collapse_triangulate = True
    if schuetz_z is not None:
        g = o.vertex_groups.new(name="duenn")
        for v in o.data.vertices:
            g.add([v.index], 1.0 if (o.matrix_world @ v.co).z < schuetz_z else 0.02, "REPLACE")
        m.vertex_group = "duenn"; m.vertex_group_factor = 1.0
    m.ratio = min(1.0, ziel_tris / n0)
    bpy.context.view_layer.objects.active = o
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True)
    bpy.ops.object.modifier_apply(modifier=m.name)
    keys_uebertragen(o, basis, d)
    print("DUENN", o.name, n0, "->", tris(o))

def tris(o):
    o.data.calc_loop_triangles(); return len(o.data.loop_triangles)
for o in M.objekte(b):
    if o.type == "MESH":
        print("NETZ", o.name, tris(o), [k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [])
kopfz = LM["joint-neck"].z
for o in M.objekte(b):
    if o.type != "MESH": continue
    if o.name == "Human": ausduennen(o, int(a.get("koerper", 6200)), schuetz_z=kopfz)
    elif "casualsuit" in o.name: ausduennen(o, int(a.get("kleid", 1800)))
    elif "teeth" in o.name: ausduennen(o, int(a.get("zaehne", 700)))
import gesicht as G
_koerper = [o for o in M.objekte(b) if o.name == "Human"][0]
if not a.get("trellis"):
    _zentral = [v.co.copy() for v in _koerper.data.vertices if abs(v.co.x) < 0.004 and v.co.z > 1.4]
    _nase = min(_zentral, key=lambda v: v.y)
    G.anwenden(_koerper, [o for o in M.objekte(b) if o.type == "MESH" and ("eyebrow" in o.name or "eyelash" in o.name)],
               _nase, float(a.get("formen", 1.0)))
import hilfe_karten as H


def profil():
    for v in sorted([v.co for v in b.data.vertices if abs(v.co.x)<0.004 and v.co.y<-0.03 and v.co.z>1.1], key=lambda c:-c.z)[::2]:
        print('PROFIL %.3f %.3f'%(v.z,v.y))
    print('BOX', [max(v.co[i] for v in b.data.vertices) for i in range(3)], [min(v.co[i] for v in b.data.vertices) for i in range(3)])

def weiter2(b, a, dna):
    ordner = os.path.dirname(a["aus"])
    tr = bool(a.get("trellis"))

    # Augen: einfaches Material mit Augen-Textur
    augen = M.asset_objekt(b, "Eyes")
    if augen:
        mat = bpy.data.materials.new("Augen")
        mat.use_nodes = True
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(os.path.join(M.NUTZERDATEN, "eyes", "materials", dna.get("augen_textur", "brown") + "_eye.png"))
        p = mat.node_tree.nodes["Principled BSDF"]
        mat.node_tree.links.new(tex.outputs["Color"], p.inputs["Base Color"])
        p.inputs["Roughness"].default_value = 0.15
        augen.data.materials.clear(); augen.data.materials.append(mat)
    ordner = os.path.dirname(a["aus"])
    # Haut und Kleid bekommen in Godot eigene Shader/Materialien: Texturen nicht in die .glb packen
    for o in M.objekte(b):
        if o.type == "MESH" and ((o.name == "Human" and not tr) or "casualsuit" in o.name):
            mat = bpy.data.materials.new("einfach_" + o.name)
            o.data.materials.clear(); o.data.materials.append(mat)
    # Zähne: kleine Textur genügt
    for img in bpy.data.images:
        if "teeth" in img.name.lower() and img.size[0] > 256:
            img.scale(256, 256)
    # Augen-Blickformen müssen nicht an der Haut hängen (nur an den Augäpfeln)
    for o in M.objekte(b):
        if o.name == "Human" and o.data.shape_keys:
            for k in [k for k in o.data.shape_keys.key_blocks if k.name.startswith("eyeLook")]:
                o.shape_key_remove(k)
    # Haut alt: Textur der alten Haut verkleinert neben die .glb legen
    alt = os.path.join(M.NUTZERDATEN, "skins", "old_caucasian_male", "old_lightskinned_male_diffuse.png")
    import glob
    jung = (glob.glob(os.path.join(M.NUTZERDATEN, "skins", "young_caucasian_male", "*diffuse*.png")) or [None])[0]
    if jung and not tr:
        im = bpy.data.images.load(jung); im.scale(1024, 1024); im.filepath_raw = os.path.join(ordner, "haut_jung.png")
        im.file_format = "PNG"; im.save()
    alt = (glob.glob(os.path.join(M.NUTZERDATEN, "skins", "old_caucasian_male", "*diffuse*.png")) or [None])[0]
    if alt and not tr:
        im = bpy.data.images.load(alt); im.scale(1024, 1024); im.filepath_raw = os.path.join(ordner, "haut_alt.png")
        im.file_format = "PNG"; im.save()
    groesste = int(a.get("textur", 1024))
    for img in bpy.data.images:
        if img.size[0] > groesste:
            f = groesste / img.size[0]; img.scale(groesste, max(1, int(img.size[1] * f)))
    gesamt = 0
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.data.calc_loop_triangles(); gesamt += len(o.data.loop_triangles)
    print("NETZ gesamt", gesamt, "Dreiecke (alle Frisuren)")
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.select_set(True)
    bpy.context.view_layer.objects.active = b
    bpy.ops.export_scene.gltf(filepath=a["aus"], export_format="GLB", use_selection=True, export_morph=True,
                              export_morph_normal=True, export_apply=False, export_skins=False,
                              export_animations=False, export_yup=True,
                              export_image_format="JPEG", export_jpeg_quality=88)
    print("EXPORT", a["aus"], os.path.getsize(a["aus"]))

if a.get("trellis"):
    import anpassen as AP
    AP.weiter(b, a, dna, M, S, LM)
else:
    H.weiter(b, a, dna, M, S, LM)
