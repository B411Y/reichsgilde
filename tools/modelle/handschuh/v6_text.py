"""Haltung "Text" fuer den v6-Handschuh: flache, gerade Handflaeche hochkant (wie "Stopp"), von der Seite wie ein Strich/Cursor.

Aufruf (Blender -b auf der fertigen v6-Datei, Pfade absolut):
  blender -b handschuh_pixal3d_v6.blend --python v6_text.py -- <ausordner> [probe]
Ohne "probe": baut die Aktionen Text, Uebergang_Ruhe_Text, Uebergang_Text_Ruhe, Uebergang_Zeigen_Text, Uebergang_Text_Zeigen,
setzt den Anker "Anker_Text", exportiert <ausordner>/handschuh_zeiger_roh.glb (danach glb_faltenfarbe.py), schreibt messwerte_text.json
und rendert Bilder (Ansichten oben/seite/schraeg/vorn) nach <ausordner>/bilder. Mit "probe": nur Standbilder der Haltung und Messung.
Gerueste, Netz, Gewichte, die alten Aktionen und die Anker bleiben unveraendert (Quelle ist die gespeicherte v6-Datei).
Umgebung: TEXT = JSON mit Parametern (siehe PARAM).
"""
import bpy, sys, os, math, json
import numpy as np
from mathutils import Vector, Matrix
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
if "TEXT" not in os.environ:   # Parameter der Haltung: text_params.json (Ergebnis von v6_opt_text.py)
    os.environ["TEXT"] = open(os.path.join(HIER, "text_params.json")).read()
import v2_lib, v6_rig_lib as L
import v6_text_lib as T
from v6_text_lib import rig, gl, P, PT, PR, PZ, atmen, messen_pose
from v6_rig_lib import AX_X, AX_Y, AX_Z
args = sys.argv[sys.argv.index("--") + 1:]
aus = args[0]; probe = len(args) > 1 and args[1] == "probe"
os.makedirs(os.path.join(aus, "bilder"), exist_ok=True)

def hand_kante(d):
    """Mittelpunkt der Handkante in der Haltung Text: Kleinfingerseite (oben nach der Rollung), Hoehe = Mitte des Mittelfingers.
    Gibt (Punkt, Richtung zur Kante) in Weltkoordinaten zurueck."""
    L.pose_anwenden(rig, d); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
    me = gl.evaluated_get(dg).to_mesh(); V = np.array([(gl.matrix_world @ v.co)[:] for v in me.vertices])
    pb = rig.pose.bones
    a = np.array((rig.matrix_world @ pb["finger3-1.R"].head)[:]); b = np.array((rig.matrix_world @ pb["finger3-3.R"].tail)[:])
    mitte = (a + b) / 2
    # Richtung zur Kante: +Z des Welt-Raums (Handruecken nach aussen/oben-Rollung) bestimmt aus der Daumen-zu-Klein-Richtung der Hand
    kl = np.array((rig.matrix_world @ pb["finger5-2.R"].head)[:]); dm = np.array((rig.matrix_world @ pb["finger2-2.R"].head)[:])
    r = np.array([0.0, 0.0, 1.0])   # Kante zum Betrachter (Ansicht oben schaut von +Z): hoechster Punkt der Hand in Z
    laengs = b - a; laengs /= np.linalg.norm(laengs)
    band = np.abs((V - mitte) @ laengs) < 0.004
    proj = (V[band] - mitte) @ r
    top = proj.max(); sel = V[band][proj > top - 0.0015]
    punkt = sel.mean(axis=0)
    return punkt, r, laengs

# ------------------------------------------------ Anker_Text (Elternknochen wrist.R, Position in der Haltung Text)
sc = bpy.context.scene
for o in list(bpy.data.objects):
    if o.name == "Anker_Text": bpy.data.objects.remove(o)
pt, rr, ll = hand_kante(PT)
print("HANDKANTE", [round(float(x), 4) for x in pt], "Richtung", [round(float(x), 3) for x in rr], "Laengs", [round(float(x), 3) for x in ll])
inter, z = messen_pose(PT)
def faecher(d):
    """Faecherwinkel der Finger zueinander (3D, Grad): Zeigefinger zu Mittelfinger und groesster Winkel unter allen vier; Daumen: Abstand der Spitze zur Zeigefingerachse (mm)."""
    L.pose_anwenden(rig, d); bpy.context.view_layer.update(); pb = rig.pose.bones; W = rig.matrix_world
    P_ = lambda b, a: np.array((W @ getattr(pb[b], a))[:])
    dirs = {k: (P_(f"finger{k}-3.R", "tail") - P_(f"finger{k}-1.R", "head")) for k in (2, 3, 4, 5)}; dirs = {k: v / np.linalg.norm(v) for k, v in dirs.items()}
    ang = lambda a, b: float(np.degrees(np.arccos(np.clip(dirs[a] @ dirs[b], -1, 1))))
    a0 = P_("finger2-1.R", "head"); ax = dirs[2]; tip = P_("finger1-3.R", "tail"); lat = np.linalg.norm((tip - a0) - ((tip - a0) @ ax) * ax) * 1000
    td = P_("finger1-3.R", "tail") - P_("finger1-1.R", "head"); td /= np.linalg.norm(td); dw = float(np.degrees(np.arccos(np.clip(td @ dirs[2], -1, 1))))
    e0, e1, e2, e3 = P_("finger1-1.R", "head"), P_("finger1-1.R", "tail"), P_("finger1-2.R", "tail"), P_("finger1-3.R", "tail")
    u_, v_ = (e2 - e1) / np.linalg.norm(e2 - e1), (e3 - e2) / np.linalg.norm(e3 - e2); k2 = float(np.degrees(np.arccos(np.clip(u_ @ v_, -1, 1))))
    b_, c_ = (e1 - e0) / np.linalg.norm(e1 - e0), (e3 - e1) / np.linalg.norm(e3 - e1); k1 = float(np.degrees(np.arccos(np.clip(b_ @ c_, -1, 1))))
    return {"daumen_knickwinkel_ansatz_zu_spitze_3d_grad": round(k1, 1), "daumen_zweiter_knick_3d_grad": round(k2, 1), "daumen_winkel_zum_zeigefinger_grad": round(dw, 1), "zeige_zu_mittel_grad": round(ang(2, 3), 1), "max_paar_grad": round(max(ang(i, j) for i in (2, 3, 4, 5) for j in (2, 3, 4, 5) if i < j), 1), "daumenspitze_zur_zeigefingerachse_mm": round(float(lat), 1)}
print("FAECHER", faecher(PT))
def breite_oben(d):
    """Silhouettenbreite der Hand (ohne Stulpen) in der Betrachter-Ansicht oben (Welt-X, Blick entlang Z), mm, 0-100 %, und die Fingerreihe allein (Gelenke Zeige..Klein)."""
    V = T.hand_punkte(d); vz = -1.0 if abs(T.PARAM["wurzel"][1]) > 90 else 1.0
    h = V[vz * V[:, 1] > 0.045]
    return round(float(h[:, 0].max() - h[:, 0].min()) * 1000, 1)
BR = breite_oben(PT); print("BREITE_OBEN_MM", BR)
print("DURCHDRINGUNG Text", inter, {f"{a}-{b}": n for (a, b), n in z.items() if a != b})
ankerT = bpy.data.objects.new("Anker_Text", None); ankerT.empty_display_type = "PLAIN_AXES"; ankerT.empty_display_size = 0.01
bpy.context.scene.collection.objects.link(ankerT); ankerT.parent = rig; ankerT.parent_type = "BONE"; ankerT.parent_bone = "wrist.R"
L.pose_anwenden(rig, PT); bpy.context.view_layer.update()
ankerT.matrix_world = Matrix.Translation(Vector(pt.tolist()))
# Umriss-Huelle blendet nicht mit; Marke ausblenden fuer Bilder
for o in bpy.data.objects:
    if o.name.startswith("Hilfe_Marke"): o.hide_render = True
cam = L.buehne(int(os.environ.get("PX", "384")))
L.ANSICHTEN["spitze"] = ((0, -1, 0.15), 0.30)   # Blick auf die Fingerspitzen (Hand ist um 180 Grad gedreht: Finger zeigen nach -Y)
ANS = ("oben", "seite", "schraeg", "spitze")
MITTE = Vector((0, 0.0, 0))   # beide Lagen (Ruhe: Finger nach +Y, Text: nach -Y) passen bei ORTHO 0.46
def linien(d):
    """Fingerachsen und Daumenkette in der Betrachter-Ansicht oben: Bildpunkte (Pixel bei PX/ORTHO) und Kennzahlen. Schreibt <aus>/linien.json."""
    L.pose_anwenden(rig, d); bpy.context.view_layer.update()
    q = Vector((0, 0.001, -1)).normalized().to_track_quat("-Z", "Y"); R = -np.array((q @ Vector((1, 0, 0)))[:]); O = -np.array((q @ Vector((0, 1, 0)))[:])   # Vorzeichen an gerenderten Bildern geprueft
    px = int(os.environ.get("PX", "384")); orth = float(os.environ.get("ORTHO", "0.30")); pb = rig.pose.bones
    P_ = lambda b, a: np.array((rig.matrix_world @ getattr(pb[b], a))[:]) - np.array(MITTE[:])
    bild_ = lambda p: [round((float(p @ R) / orth + 0.5) * px, 1), round((0.5 - float(p @ O) / orth) * px, 1)]
    mm = lambda p: np.array([float(p @ R), float(p @ O)]) * 1000
    out = {"px": px, "ortho": orth, "finger": {}, "daumen": []}; wink = {}
    for k in (2, 3, 4, 5):
        out["finger"][str(k)] = [bild_(P_(f"finger{k}-1.R", "head")), bild_(P_(f"finger{k}-2.R", "head")), bild_(P_(f"finger{k}-3.R", "head")), bild_(P_(f"finger{k}-3.R", "tail"))]
        v = mm(P_(f"finger{k}-3.R", "tail")) - mm(P_(f"finger{k}-1.R", "head")); wink[k] = float(np.degrees(np.arctan2(v[0], v[1])))
    pts = [P_("finger1-1.R", "head"), P_("finger1-1.R", "tail"), P_("finger1-2.R", "tail"), P_("finger1-3.R", "tail")]
    out["daumen"] = [bild_(p) for p in pts]
    t = [mm(p) for p in pts]; knick = float(t[1][0] - (t[0][0] + t[3][0]) / 2)
    out["kennzahlen"] = {"fingerachsen_winkel_grad": {str(k): round(v, 2) for k, v in wink.items()}, "fingerachsen_max_differenz_grad": round(max(wink.values()) - min(wink.values()), 2),
                         "daumen_knick_mm_rechts_positiv": round(knick, 1), "daumen_knickrichtung": "> (Knick zum Handruecken, rechts)" if knick > 0 else "< (Knick zur Handflaeche, links)"}
    json.dump(out, open(os.path.join(aus, "linien.json"), "w"), indent=1)
    print("LINIEN", json.dumps(out["kennzahlen"]))
    return out["kennzahlen"]
def bild(prefix):
    for an in ANS: L.ansicht_rendern(cam, an, os.path.join(aus, "bilder", f"{prefix}_{an}.png"), MITTE)

if probe:
    L.pose_anwenden(rig, PT); bpy.context.view_layer.update(); bild("probe_text")
    L.pose_anwenden(rig, PR); bpy.context.view_layer.update(); bild("probe_ruhe")
    linien(PT); print("PROBE fertig"); sys.exit(0)

# ------------------------------------------------ Aktionen
def aktion(name, laenge, schleife):
    a = bpy.data.actions.new(name); a.use_fake_user = True
    rig.animation_data_create(); rig.animation_data.action = a
    a.use_frame_range = True; a.frame_range = (0, laenge); a.use_cyclic = schleife
    return a
neu = {}
a = aktion("Text", 48, True); neu["Text"] = (a, 48, 6)
for f in range(0, 49, 4): L.pose_keyen(rig, atmen(PT, f / 48.0, 0.1), f)
def uebergang(name, von, nach, laenge, schliessen, kurz=False):
    a = aktion(name, laenge, False); neu[name] = (a, laenge, 1)
    for f in range(laenge + 1): L.pose_keyen(rig, L.gestaffelt(von, nach, f / laenge, schliessen, kurz), f)
UE = [("Uebergang_Ruhe_Text", PR, PT, 14, False), ("Uebergang_Text_Ruhe", PT, PR, 14, False),
      ("Uebergang_Zeigen_Text", PZ, PT, 16, False), ("Uebergang_Text_Zeigen", PT, PZ, 16, True)]
for n, v, nch, ln, sl in UE: uebergang(n, v, nch, ln, sl)
rig.animation_data.action = bpy.data.actions["Ruhe"]
sc.frame_set(0); L.pose_anwenden(rig, P["ruhe"]); bpy.context.view_layer.update()
bpy.ops.object.select_all(action="DESELECT")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(aus, "handschuh_pixal3d_v6_text.blend"))
bpy.ops.export_scene.gltf(filepath=os.path.join(aus, "handschuh_zeiger_roh.glb"), export_format="GLB", export_animations=True, export_animation_mode="ACTIONS", export_skins=True, export_def_bones=False)
print("GLB geschrieben")

# ------------------------------------------------ Messung (Dreieckspaare zwischen verschiedenen Gliedern, wie v6)
mess = {"text": messen_pose(PT)[0], "durchdringung_paare": {f"{a}-{b}": n for (a, b), n in messen_pose(PT)[1].items() if a != b and not (a == "H" and b == "H")}, "fan_3d": faecher(PT), "breite_oben_mm": BR, "linien": linien(PT), "kante_punkt": [round(float(x), 4) for x in pt]}
ue = {}
for n, (a_, ln, st) in neu.items():
    if not n.startswith("Uebergang"): continue
    rig.animation_data.action = a_; mx = 0
    for f in range(ln + 1):
        sc.frame_set(f); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
        zz = v2_lib.selbstschnitt_zerlegt(gl, dg); mx = max(mx, sum(c for (aa, bb), c in zz.items() if aa != bb and not (aa == "H" and bb == "H")))
    ue[n] = mx
mess["uebergaenge_max"] = ue
a_ = neu["Text"][0]; rig.animation_data.action = a_; mx = 0
for f in range(0, 49, 4):
    sc.frame_set(f); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
    zz = v2_lib.selbstschnitt_zerlegt(gl, dg); mx = max(mx, sum(c for (aa, bb), c in zz.items() if aa != bb and not (aa == "H" and bb == "H")))
mess["text_clip_max"] = mx
json.dump(mess, open(os.path.join(aus, "messwerte_text.json"), "w"), indent=1)
print("MESSWERTE", json.dumps(mess))

# ------------------------------------------------ Bilder
for n, (a_, ln, st) in neu.items():
    rig.animation_data.action = a_
    for f in range(0, ln + 1, st):
        sc.frame_set(f); bpy.context.view_layer.update()
        for an in ANS: L.ansicht_rendern(cam, an, os.path.join(aus, "bilder", f"{n}_{f:02d}_{an}.png"), MITTE)
    print("CLIP", n, flush=True)
print("FERTIG")

