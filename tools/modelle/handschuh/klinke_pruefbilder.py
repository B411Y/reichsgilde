"""Pruefbilder fuer die Klinken-Clips, gerendert aus den EXPORTIERTEN Aktionen (Blend-Datei von klinke_aus_blend.py):
 1) Vergleichsbilder der drei Haltungen (Bereit, Greifen, Gedrueckt) in der Zeigeransicht und der SICHTBARE ANTEIL DER KLINKE (Zahl der nicht von der Hand verdeckten Klinkenpixel
    durch die Pixel der freien Klinke; Klinke als Markerfarbe gerendert, Hand ein/aus).
 2) Uebergaenge bei 60 Bildern/s (Zeitpunkte i/60 s) in der Zeigeransicht: Ruhe->Bereit->Ruhe, Zeigen->Bereit->Zeigen, Loslassen->Bereit; das Schloss waechst waehrend des
    Uebergangs mit derselben Smoothstep-Kurve (Abbild des Overlay), Dreieckspaare Hand/Schloss je Bild (volles Schloss, also strenger).
blender -b <ausordner>/handschuh_pixal3d_v6_klinke.blend --python klinke_pruefbilder.py -- <tuerschloss.blend> <messwerte_klinke.json> <ausordner>"""
import bpy, sys, os, json, math
import numpy as np
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
args = sys.argv[sys.argv.index("--") + 1:]
schloss_blend, mjson, aus = args[0], args[1], args[2]
os.environ["TUERSCHLOSS"] = schloss_blend
import v6_klinke_lib as K
from v6_klinke_lib import rig, gl, L, tr
from mathutils import Vector, Matrix
os.makedirs(os.path.join(aus, "pruef"), exist_ok=True)
sc = bpy.context.scene; sc.render.fps = 24
mess = json.load(open(mjson)); LAM = Matrix(mess["schloss_matrix"])
S = K.schloss_laden()
for n in ("Umriss_Schloss", "Umriss_Klinke"): pass
A = Vector((0, 0, 0.005))
def sanft(t): t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)
def setze_schloss(skala, winkel):
    S["Klinke"].rotation_euler = (0, 0, math.radians(winkel))
    S["Tuerschloss"].matrix_world = LAM @ tr(A) @ Matrix.Scale(max(skala, 0.001), 4) @ tr(-A); bpy.context.view_layer.update()
def pose(clip, frame):
    rig.animation_data_create(); rig.animation_data.action = bpy.data.actions[clip]
    sc.frame_set(int(math.floor(frame)), subframe=frame - math.floor(frame)); bpy.context.view_layer.update()
for o in bpy.data.objects:
    if o.name.startswith("Hilfe_Marke"): o.hide_render = True
cam = L.buehne(768)
def spiel_kamera(cam, ortho=0.45):
    def gl2bl(v): return Vector((v.x, -v.z, v.y))
    dreh = Matrix.Rotation(math.radians(-40), 3, "Y"); tilt = math.radians(16)
    off = gl2bl(dreh @ Vector((0, math.cos(tilt), math.sin(tilt)))); up = gl2bl(dreh @ Vector((0, 0, -1))); mitte = gl2bl(Vector((0, 0, -0.045)))
    z = off.normalized(); y = (up - z * up.dot(z)).normalized(); x = y.cross(z)
    m = Matrix(((x.x, y.x, z.x, 0), (x.y, y.y, z.y, 0), (x.z, y.z, z.z, 0), (0, 0, 0, 1))); m.translation = mitte + z * 0.8
    cam.matrix_world = m; cam.data.ortho_scale = ortho; cam.data.clip_start = 0.05; cam.data.clip_end = 3.0
spiel_kamera(cam)
HAND = [o for o in bpy.data.objects if o.name in ("Handschuh", "Handschuh_Umriss")]
def rendern(pfad, px=None):
    if px: sc.render.resolution_x = sc.render.resolution_y = px
    sc.render.filepath = pfad; bpy.ops.render.render(write_still=True)
# Marker-Material fuer die Klinke
mk = bpy.data.materials.new("Marker"); mk.use_nodes = True
nt = mk.node_tree; nt.nodes.clear(); em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (1, 0, 1, 1); ou = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(em.outputs[0], ou.inputs[0])
klinke_mat = S["Klinke_Netz"].data.materials[0]
def anteil(pfad_basis):
    """Sichtbarer Anteil der Klinke in der Zeigeransicht (Marker-Pixel mit Hand / ohne Hand)."""
    S["Klinke_Netz"].data.materials[0] = mk
    for n in ("Schloss", "Umriss_Schloss", "Umriss_Klinke"): S[n].hide_render = True
    zaehl = {}
    for hand in (False, True):
        for o in HAND: o.hide_render = not hand
        rendern(pfad_basis + ("_mit.png" if hand else "_ohne.png"), 768)
        im = bpy.data.images.load(pfad_basis + ("_mit.png" if hand else "_ohne.png"))
        w, h = im.size; arr = np.empty(w * h * 4, dtype=np.float32); im.pixels.foreach_get(arr); arr = arr.reshape(h, w, 4); bpy.data.images.remove(im)
        zaehl[hand] = int(((arr[..., 0] > 0.78) & (arr[..., 2] > 0.78) & (arr[..., 1] < 0.35) & (arr[..., 3] > 0.78)).sum())
    S["Klinke_Netz"].data.materials[0] = klinke_mat
    for n in ("Schloss", "Umriss_Schloss", "Umriss_Klinke"): S[n].hide_render = False
    for o in HAND: o.hide_render = False
    return zaehl[True], zaehl[False]
erg = {}
HALT = [("Bereit", "Klinke_bereit", 0.0, 0.0), ("Greifen", "Klinke_greifen", 10.0, 0.0), ("Gedrueckt", "Klinke_druecken", 16.0, -K.WINKEL)]
bilder = []
for nm, clip, fr, th in HALT:
    pose(clip, fr); setze_schloss(1.0, th)
    paare = K.paare_hand_schloss(S)
    sicht = anteil(os.path.join(aus, "pruef", f"sicht_{nm}"))
    rendern(os.path.join(aus, "pruef", f"haltung_{nm}.png"), 768)
    erg[nm] = {"klinke_sichtbar_px": sicht[0], "klinke_frei_px": sicht[1], "sichtbarer_anteil": round(sicht[0] / max(1, sicht[1]), 3), "paare_hand_schloss": paare}
    print("SICHT", nm, erg[nm], flush=True)
# Uebergaenge bei 60 Bildern/s
def sequenz(teile, name):
    """teile: Liste (clip, dauer_s, skalenverlauf, winkelverlauf); skalenverlauf/winkelverlauf: Funktionen von t in 0..1."""
    nr = 0; pa = []; frames = []
    for clip, dauer, skala, wink in teile:
        n = int(round(dauer * 60))
        for i in range(n + 1):
            t = i / n; pose(clip, t * dauer * 24.0); setze_schloss(skala(t), wink(t))
            pa.append(sum(K.paare_hand_schloss(S).values()))
            p = os.path.join(aus, "pruef", f"{name}_{nr:03d}.png"); rendern(p, 384); frames.append(p); nr += 1
    return frames, pa
UE = mess["uebergang_dauer_s"]
eins = lambda t: 1.0; null = lambda t: 0.0
hold = [("Klinke_bereit", 0.0, eins, null)]
folgen = {
    "ruhe_bereit_ruhe": [("Uebergang_Ruhe_Bereit", UE, sanft, null), ("Klinke_bereit", 0.2, eins, null), ("Uebergang_Bereit_Ruhe", UE, lambda t: 1 - sanft(t), null)],
    "zeigen_bereit_zeigen": [("Uebergang_Zeigen_Bereit", UE, sanft, null), ("Klinke_bereit", 0.2, eins, null), ("Uebergang_Bereit_Zeigen", UE, lambda t: 1 - sanft(t), null)],
    "text_bereit_text": [("Uebergang_Text_Bereit", UE, sanft, null), ("Klinke_bereit", 0.2, eins, null), ("Uebergang_Bereit_Text", UE, lambda t: 1 - sanft(t), null)],
    "loslassen_bereit": [("Klinke_loslassen", 0.5, eins, lambda t: -K.WINKEL * (1 - sanft(t / 0.34)) if t < 0.34 else 0.0)],
}
for name, teile in folgen.items():
    fr, pa = sequenz(teile, name)
    erg["folge_" + name] = {"bilder": len(fr), "fps": 60, "max_paare": max(pa), "je_bild": pa}
    print("FOLGE", name, len(fr), "Bilder, max Paare", max(pa), flush=True)
    json.dump({n: p for n, p in zip(range(len(fr)), fr)}, open(os.path.join(aus, "pruef", f"{name}_liste.json"), "w"))
json.dump(erg, open(os.path.join(aus, "pruef", "pruefwerte.json"), "w"), indent=1)
print("FERTIG")
