"""Bilder der lebendigen Clips (Bereit_atmen, Bereit_zucken, Winken, Uebergang_*_Winken) in der Zeigeransicht, gerendert aus den EXPORTIERTEN Aktionen bei 60 Bildern/s (Zeitpunkte i/60 s).
blender -b <ausordner>/handschuh_pixal3d_v6_leben.blend --python leben_bilder.py -- <tuerschloss.blend> <messwerte_klinke.json> <ausordner>
Schreibt <ausordner>/bilder/<Clip>_<NNN>.png und <Clip>_liste.json; v6_leben_blatt.py setzt GIFs und Blaetter zusammen. Bei Bereit-Clips steht das Schloss im Bild."""
import bpy, sys, os, json, math
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
args = sys.argv[sys.argv.index("--") + 1:]
schloss_blend, mjson, aus = args[0], args[1], args[2]
os.environ["TUERSCHLOSS"] = schloss_blend
import v6_klinke_lib as K
from v6_klinke_lib import rig, L, tr
from mathutils import Vector, Matrix
os.makedirs(os.path.join(aus, "bilder"), exist_ok=True)
sc = bpy.context.scene; sc.render.fps = 24
LAM = Matrix(json.load(open(mjson))["schloss_matrix"]); S = K.schloss_laden(); S["Tuerschloss"].matrix_world = LAM
for o in bpy.data.objects:
    if o.name.startswith("Hilfe_Marke"): o.hide_render = True
cam = L.buehne(384)
def gl2bl(v): return Vector((v.x, -v.z, v.y))
dreh = Matrix.Rotation(math.radians(-40), 3, "Y"); tilt = math.radians(16)
off = gl2bl(dreh @ Vector((0, math.cos(tilt), math.sin(tilt)))); up = gl2bl(dreh @ Vector((0, 0, -1))); mitte = gl2bl(Vector((0, 0, -0.045)))
z = off.normalized(); y = (up - z * up.dot(z)).normalized(); x = y.cross(z)
m = Matrix(((x.x, y.x, z.x, 0), (x.y, y.y, z.y, 0), (x.z, y.z, z.z, 0), (0, 0, 0, 1))); m.translation = mitte + z * 0.8
cam.matrix_world = m; cam.data.ortho_scale = 0.45; cam.data.clip_start = 0.05; cam.data.clip_end = 3.0
CLIPS = ["Bereit_atmen", "Bereit_zucken", "Winken", "Uebergang_Ruhe_Winken", "Uebergang_Winken_Ruhe", "Uebergang_Zeigen_Winken", "Uebergang_Winken_Zeigen",
         "Uebergang_Text_Winken", "Uebergang_Winken_Text", "Uebergang_Greifen_Winken", "Uebergang_Winken_Greifen", "Uebergang_Bereit_Winken", "Uebergang_Winken_Bereit"]
rig.animation_data_create()
for clip in CLIPS:
    a = bpy.data.actions[clip]; rig.animation_data.action = a; dauer = (a.frame_range[1] - a.frame_range[0]) / 24.0
    mit_schloss = "Bereit" in clip
    for o in S.values(): o.hide_render = not mit_schloss
    n = int(round(dauer * 60)); liste = []
    for i in range(n + 1):
        f = i / 60.0 * 24.0; sc.frame_set(int(math.floor(f)), subframe=f - math.floor(f)); bpy.context.view_layer.update()
        p = os.path.join(aus, "bilder", f"{clip}_{i:03d}.png"); sc.render.filepath = p; bpy.ops.render.render(write_still=True); liste.append(p)
    json.dump(liste, open(os.path.join(aus, "bilder", f"{clip}_liste.json"), "w")); print("CLIP", clip, n + 1, "Bilder", flush=True)
print("FERTIG")
