"""Uebernimmt die SELBST eingestellten Haltungen an der Tuerklinke (klinke_fertig.blend) in die Clips der Handschuh-glb.
  blender -b handschuh_pixal3d_v6_text.blend --python klinke_aus_blend.py -- <ausordner> <klinke_fertig.blend> <tuerschloss.blend> [bilder|probe]
Liest aus der fertigen Datei die Aktion Klinke_bearbeiten (Bild 1 = Bereit, 2 = Greifen, 3 = Gedrueckt): alle Knochenwinkel (Euler oder Quaternion) und die Handlage
(Wurzel: Ort und Drehung), ausserdem die Lage des Schlosses (Objekt Tuerschloss). Daraus entstehen
  Klinke_bereit (Schleife), Klinke_greifen, Klinke_druecken, Klinke_loslassen, Uebergang_{Ruhe,Zeigen,Text}_Bereit und Uebergang_Bereit_{Ruhe,Zeigen,Text}, Anker_Greifen.
Greifen laeuft ueber einen Zwischenhalt (Hand daneben, halb geschlossen); ist der Weg nicht frei, sucht das Skript ihn im Raster (W1/VOR siehe v6_klinke.py).
Exportiert <ausordner>/handschuh_zeiger_roh.glb (danach glb_faltenfarbe.py), schreibt messwerte_klinke.json (Dreieckspaare Hand/Schloss je Bild, Schlossversatz fuer die Buehne)
und mit "bilder" Bilder fuer v6_klinke_blatt.py ("probe": nur messen). Alte Aktionen, Netz, Gewichte und Anker bleiben unveraendert.
Das Skript braucht die Text-Fassung (handschuh_pixal3d_v6_text.blend aus v6_text_basis.py) als geoeffnete Datei."""
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector, Matrix, Quaternion
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
args = sys.argv[sys.argv.index("--") + 1:]
aus, fertig, schloss_blend = args[0], args[1], args[2]
modus = args[3] if len(args) > 3 else ""
os.makedirs(os.path.join(aus, "bilder"), exist_ok=True)
os.environ["TUERSCHLOSS"] = schloss_blend
import v6_klinke_lib as K
from v6_klinke_lib import rig, gl, P, PZ, PR, L, tr, rz, QPose
sc = bpy.context.scene
sanft = L.ss_
S = K.schloss_laden()
FPS = 24; N_GR, N_DR, N_HALT, N_LOS, N_BER, N_UE = 10, 16, 4, 12, 48, 7
WINKEL = K.WINKEL
DAUER_UE = N_UE / 24.0                 # 7 Bilder bei 24/s = 0,2917 s (die Glb-Abtastung kennt nur ganze Bilder); Dauer der Uebergaenge nach/von Bereit in Sekunden (verbindlich fuer Schloss-Einwachsen und Zeigerpunkt-Gleiten)
DT_UE = 1.0                            # Schluessel auf jedem Bild; WINKEL = K.WINKEL
NAMEN = [pb.name for pb in rig.pose.bones]

# ------------------------------------------------ Text-Pose (Aktion Text, Bild 0) der Basis-Datei
rig.animation_data.action = bpy.data.actions["Text"]; sc.frame_set(0); bpy.context.view_layer.update()
QT = QPose({pb.name: (pb.rotation_quaternion.copy(), Vector(pb.location)) for pb in rig.pose.bones})
rig.animation_data.action = None
QR = K.q_lesen(K.mit_rollachse(PR)); QZ = K.q_lesen(K.mit_rollachse(P["zeigen"]))

# ------------------------------------------------ die drei Haltungen aus der fertigen Datei lesen
rig.name = "_Geruest_Basis"; rig.data.name = "_Geruest_Basis"
ALT_AKT = {a.name for a in bpy.data.actions}
with bpy.data.libraries.load(fertig, link=False) as (src, dst):
    dst.objects = [n for n in src.objects if n in ("Handschuh_Geruest", "Tuerschloss")]
    dst.actions = [n for n in src.actions if n == "Klinke_bearbeiten"]
imp = next((o for o in dst.objects if o.type == "ARMATURE"), None); imp_schloss = next((o for o in dst.objects if o.type == "EMPTY" and o.name.startswith("Tuerschloss")), None)
if imp is None or imp.type != "ARMATURE" or imp.animation_data is None or imp.animation_data.action is None:
    raise SystemExit("Handschuh_Geruest mit Aktion Klinke_bearbeiten nicht in " + fertig)
if imp_schloss is None: raise SystemExit("Objekt Tuerschloss nicht in " + fertig)
sc.collection.objects.link(imp)
LAM_NEU = imp_schloss.matrix_basis.copy()
def lies(pb):
    if pb.rotation_mode == "QUATERNION": return pb.rotation_quaternion.copy()
    if pb.rotation_mode == "AXIS_ANGLE":
        a = pb.rotation_axis_angle; return Quaternion(Vector(a[1:]), a[0])
    return pb.rotation_euler.to_quaternion()
HALT = {}; HM = {}
for f in (1, 2, 3):
    sc.frame_set(f); bpy.context.view_layer.update()
    q = QPose()
    for n in NAMEN:
        pb = imp.pose.bones.get(n)
        if pb is None: raise SystemExit("Knochen fehlt in der fertigen Datei: " + n)
        q[n] = (lies(pb), Vector(pb.location))
    b = imp.data.bones["Wurzel"]; pbw = imp.pose.bones["Wurzel"]
    HM[f] = b.matrix_local @ pbw.matrix_basis @ b.matrix_local.inverted()      # Handlage (Matrix im Handraum)
    HALT[f] = q
KLINKE_WINKEL = None
imp.animation_data.action = None
bpy.data.objects.remove(imp); bpy.data.objects.remove(imp_schloss)
for a_ in list(bpy.data.actions):
    if a_.name not in ALT_AKT: bpy.data.actions.remove(a_)
rig.name = "Handschuh_Geruest"; rig.data.name = "Handschuh_Geruest"
QB, QG, QD = HALT[1], HALT[2], HALT[3]; M_B, M_G, M_D = HM[1], HM[2], HM[3]
LAM = LAM_NEU
S["Tuerschloss"].matrix_world = LAM
K.schloss_setzen(S, LAM)
print("HALTUNGEN gelesen; Schloss", [round(v, 4) for v in LAM.to_translation()], "Hand Bereit/Greifen/Gedrueckt", [[round(x, 4) for x in M.to_translation()] for M in (M_B, M_G, M_D)])

# ------------------------------------------------ Bewegung
def interp(Ma, Mb, t):
    """Matrix zwischen zwei Handlagen: Ort linear, Drehung Quaternion-Slerp."""
    q = Ma.to_quaternion().slerp(Mb.to_quaternion(), t)
    return tr(Ma.to_translation().lerp(Mb.to_translation(), t)) @ q.to_matrix().to_4x4()
LIFT = float(os.environ.get("LIFT", "0.03")); LAT = [0.0, 0.0]       # Anheben und seitliches Ausweichen (Schlossraum) in der Mitte der Uebergaenge
rot_lock0 = None
def heben(M, s):
    w = math.sin(math.pi * s); return tr((rot_lock @ Vector((LAT[0] * w, LAT[1] * w, 0.0))) + Vector((0, 0, LIFT * w))) @ M
def mix(a, b, t, schliessen): return K.q_mischen(a, b, K.zeitparameter(max(0.0, min(1.0, t)), schliessen))
def gleich(a, b, s): return K.q_mischen(a, b, {n: s for n in NAMEN})      # alle Knochen mit derselben Kurve (Smoothstep) wie die Handlage
def pose_bereit(t):
    q = QPose(QB); w = 0.8 * math.sin(2 * math.pi * t)
    qq, loc = q["wrist.R"]; q["wrist.R"] = (qq @ Quaternion((1, 0, 0), math.radians(w)), loc)
    return q, tr((0, 0, 0.004 * math.sin(2 * math.pi * t))) @ M_B, 0.0
def uebergang_nach(qv, t):
    s = sanft(t); return gleich(qv, QB, s), heben(interp(Matrix.Identity(4), M_B, s), s), 0.0
def uebergang_von(qn, t):
    s = sanft(t); return gleich(QB, qn, s), heben(interp(M_B, Matrix.Identity(4), s), s), 0.0
W1 = [float(x) for x in os.environ.get("W1", "-0.03,-0.06,0.03").split(",")]
VOR = float(os.environ.get("VOR", "0.6")); U1 = 0.5
rot_lock = LAM.to_quaternion().to_matrix()          # Schlossraum -> Handraum (Verschiebungen am Schloss ausrichten)
def setze_w1(w, vor):
    global M_W1, VOR
    M_W1 = tr(rot_lock @ Vector(w)) @ M_G; VOR = vor
setze_w1(W1, VOR)
def zustand(u):
    """Bereit -> Zwischenhalt (Hand daneben, halb geschlossen) -> Griff."""
    if u < U1: M = interp(M_B, M_W1, sanft(u / U1)); sf = VOR * sanft(u / U1)
    else: M = interp(M_W1, M_G, sanft((u - U1) / (1 - U1))); sf = VOR + (1 - VOR) * sanft((u - U1) / (1 - U1))
    return mix(QB, QG, sf, True), M
def pose_greifen(t):
    q, M = zustand(t); return q, M, 0.0
def pose_druecken(f):
    s = sanft(min(f, N_DR) / N_DR); return mix(QG, QD, s, True) if False else K.q_mischen(QG, QD, {n: s for n in NAMEN}), interp(M_G, M_D, s), K.VORZ * WINKEL * s
T_BACK = 0.34
def pose_loslassen(t):
    if t < T_BACK:
        s = 1 - sanft(t / T_BACK); return K.q_mischen(QG, QD, {n: s for n in NAMEN}), interp(M_G, M_D, s), K.VORZ * WINKEL * s
    q, M = zustand(1 - (t - T_BACK) / (1 - T_BACK)); return q, M, 0.0

def zeigen(d, M, theta):
    K.pose_mit_wurzel(d, M); S["Klinke"].rotation_euler = (0, 0, math.radians(theta))
    S["Tuerschloss"].matrix_world = LAM; bpy.context.view_layer.update()
def paare_max(fn, n):
    mx = 0
    for f in range(0, n + 1):
        d, M, th = fn(f / n); zeigen(d, M, th); mx = max(mx, sum(K.paare_hand_schloss(S).values()))
    return mx
# Weg frei? sonst Raster fuer Zwischenhalt und Vorformung
if paare_max(pose_greifen, 40) > 0 and not os.environ.get("KEINE_SUCHE"):
    erg = []
    for wy in (-0.06, -0.03, 0.0, 0.03):
        for wx in (-0.03, 0.0, 0.03):
            for wz in (0.0, 0.03, 0.06):
                for vor in (0.35, 0.6):
                    setze_w1((wx, wy, wz), vor); erg.append((paare_max(pose_greifen, 40), abs(wx) + abs(wy) + abs(wz), wx, wy, wz, vor))
    erg.sort(); b = erg[0]; setze_w1((b[2], b[3], b[4]), b[5]); print("ZWISCHENHALT gesucht", b)

def uebergaenge_max():
    m = 0
    for q in (QR, QZ, QT):
        m = max(m, paare_max(lambda t: uebergang_nach(q, t), 18), paare_max(lambda t: uebergang_von(q, t), 18))
    return m
if uebergaenge_max() > 0 and not os.environ.get("KEINE_SUCHE"):
    erg = []
    for lift in (0.03, 0.06, 0.10):
        for lx in (-0.06, -0.03, 0.0, 0.03, 0.06):
            for ly in (-0.06, -0.03, 0.0, 0.03):
                LIFT = lift; LAT[0], LAT[1] = lx, ly; erg.append((uebergaenge_max(), abs(lx) + abs(ly) + lift, lift, lx, ly))
    erg.sort(); b = erg[0]; LIFT = b[2]; LAT[0], LAT[1] = b[3], b[4]; print("UEBERGANGSWEG gesucht", b)
# ------------------------------------------------ Clips
CLIPS = [("Klinke_bereit", N_BER, lambda f: pose_bereit(f / N_BER), 8, True, 1.0),
         ("Klinke_greifen", N_GR, lambda f: pose_greifen(f / N_GR), 1, False, 1.0),
         ("Klinke_druecken", N_DR + N_HALT, lambda f: pose_druecken(f), 2, False, 1.0),
         ("Klinke_loslassen", N_LOS, lambda f: pose_loslassen(f / N_LOS), 1, False, 1.0)]
for q, nm in ((QR, "Ruhe"), (QZ, "Zeigen"), (QT, "Text")):
    CLIPS.append((f"Uebergang_{nm}_Bereit", N_UE, (lambda q_: (lambda f: uebergang_nach(q_, f / N_UE)))(q), 2, False, DT_UE))
    CLIPS.append((f"Uebergang_Bereit_{nm}", N_UE, (lambda q_: (lambda f: uebergang_von(q_, f / N_UE)))(q), 2, False, DT_UE))
try: bpy.context.preferences.edit.keyframe_new_interpolation_type = 'LINEAR'   # wie Godot zwischen den Abtastwerten
except Exception as e: print('INTERPOLATION', e)
def aktion(name, laenge, schleife, dt=1.0):
    a = bpy.data.actions.new(name); a.use_fake_user = True
    rig.animation_data_create(); rig.animation_data.action = a
    a.use_frame_range = True; a.frame_range = (0, laenge * dt); a.use_cyclic = schleife
for name, ln, fn, st, schleife, dt in CLIPS:
    aktion(name, ln, schleife, dt)
    for f in range(ln + 1):
        d, M, th = fn(f); K.pose_keyen_wurzel(d, M, f * dt)
rig.animation_data.action = None
sc.frame_set(0); L.pose_anwenden(rig, P["ruhe"]); bpy.context.view_layer.update()

# ------------------------------------------------ Anker_Greifen: Mitte der Griffstange in der Griffpose, im Raum der Hand (Wurzel Identitaet)
for o in list(bpy.data.objects):
    if o.name == "Anker_Greifen": bpy.data.objects.remove(o)
ak = bpy.data.objects.new("Anker_Greifen", None); ak.empty_display_type = "PLAIN_AXES"; ak.empty_display_size = 0.01
sc.collection.objects.link(ak); ak.parent = rig; ak.parent_type = "BONE"; ak.parent_bone = "wrist.R"
K.pose_mit_wurzel(QG, Matrix.Identity(4)); S["Klinke"].rotation_euler = (0, 0, 0); bpy.context.view_layer.update()
griff_welt = LAM @ Vector(K.GRIFF_LOKAL); griff_hand = M_G.inverted() @ griff_welt
ak.matrix_world = Matrix.Translation(griff_hand)

# ------------------------------------------------ Messung
mess = {"anker_greifen": [round(float(v), 4) for v in griff_hand],
        "schloss_versatz_handraum_blender": [round(float(v), 4) for v in LAM.to_translation()],
        "schloss_drehung_grad_um_Z": round(math.degrees(LAM.to_euler("XYZ").z), 2),
        "schloss_versatz_gltf": [round(float(LAM.to_translation().x), 4), round(float(LAM.to_translation().z), 4), round(-float(LAM.to_translation().y), 4)],
        "pivot_blender": [round(float(v), 4) for v in (LAM @ Vector(K.WELLE_LOKAL))], "zwischenhalt": [list(W1), VOR], "uebergang_dauer_s": DAUER_UE, "uebergang_weg": {"anheben_m": LIFT, "ausweichen_schlossraum_xy_m": LAT}, "uebergang_kurve": "Smoothstep t*t*(3-2t) fuer alle Knochen und die Handlage", "schloss_matrix": [list(r) for r in LAM],
        "hand_wurzel_blender": {n: [round(float(v), 4) for v in M.to_translation()] + [round(math.degrees(M.to_euler("XYZ").z), 1)] for n, M in (("bereit", M_B), ("greifen", M_G), ("gedrueckt", M_D))}}
def folge(fn, laenge, sk):
    mx = {"Schloss": 0, "Klinke_Netz": 0, "selbst": 0}; je = []
    for f in range(laenge + 1):
        d, M, th = fn(f); zeigen(d, M, th)
        p = K.paare_hand_schloss(S); inter, _ = K.messen_pose(d); zeigen(d, M, th)
        je.append(sum(p.values())); mx["Schloss"] = max(mx["Schloss"], p["Schloss"]); mx["Klinke_Netz"] = max(mx["Klinke_Netz"], p["Klinke_Netz"]); mx["selbst"] = max(mx["selbst"], inter)
    mx["je_bild"] = je; return mx
def folge60(fn, dauer):
    """Dreieckspaare je Bild bei 60 Bildern/s (Zeitpunkte i/60 s, auch zwischen den Schluesseln)."""
    n = int(round(dauer * 60)); mx = 0; je = []
    for i in range(n + 1):
        d, M, th = fn(i / n); zeigen(d, M, th); p = sum(K.paare_hand_schloss(S).values()); je.append(p); mx = max(mx, p)
    return {"bilder_60fps": n + 1, "max": mx, "je_bild": je}
for name, ln, fn, st, schleife, dt in CLIPS:
    mess[name] = folge(fn, ln, st)
    if name.startswith("Uebergang"): mess[name]["bei_60fps"] = folge60(lambda t, fn=fn, ln=ln: fn(t * ln), DAUER_UE)
json.dump(mess, open(os.path.join(aus, "messwerte_klinke.json"), "w"), indent=1)
print("MESSWERTE", json.dumps({k: (v if not isinstance(v, dict) or "je_bild" not in v else {a: b for a, b in v.items() if a != "je_bild"}) for k, v in mess.items()}))
if modus == "probe": sys.exit(0)

# ------------------------------------------------ Bilder (wie v6_klinke.py)
if modus == "bilder":
    for o in bpy.data.objects:
        if o.name.startswith("Hilfe_Marke"): o.hide_render = True
    cam = L.buehne(384)
    def spiel_kamera(cam, ortho=0.45):
        def gl2bl(v): return Vector((v.x, -v.z, v.y))
        dreh = Matrix.Rotation(math.radians(-40), 3, "Y"); tilt = math.radians(16)
        off = gl2bl(dreh @ Vector((0, math.cos(tilt), math.sin(tilt)))); up = gl2bl(dreh @ Vector((0, 0, -1))); mitte = gl2bl(Vector((0, 0, -0.045)))
        z = off.normalized(); y = (up - z * up.dot(z)).normalized(); x = y.cross(z)
        m = Matrix(((x.x, y.x, z.x, 0), (x.y, y.y, z.y, 0), (x.z, y.z, z.z, 0), (0, 0, 0, 1))); m.translation = mitte + z * 0.8
        cam.matrix_world = m; cam.data.ortho_scale = ortho; cam.data.clip_start = 0.05; cam.data.clip_end = 3.0
    def bild(name, pfad):
        if name == "spiel": spiel_kamera(cam, 0.45)
        elif name == "nah":
            v = (rot_lock @ Vector((0.35, -0.75, 0.55))).normalized(); mitte = S["Anker_Griff"].matrix_world.translation
            cam.data.ortho_scale = 0.16; cam.data.clip_start = 0.01; cam.data.clip_end = 5; cam.location = mitte + v; cam.rotation_euler = (-v).to_track_quat("-Z", "Y").to_euler()
        else:
            d = {"seite": (-1, -0.02, 0.2), "schraeg": (-0.5, -0.9, 0.7), "vorn": (0.15, 1, 0.45)}[name]
            v = Vector(d).normalized(); cam.data.ortho_scale = 0.40; cam.data.clip_start = 0.01; cam.data.clip_end = 5
            cam.location = Vector((0.0, 0.08, -0.04)) + v; cam.rotation_euler = (-v).to_track_quat("-Z", "Y").to_euler()
        bpy.context.scene.render.filepath = pfad; bpy.ops.render.render(write_still=True)
    for name, ln, fn, st, schleife, dt in CLIPS:
        for f in range(0, ln + 1, st):
            d, M, th = fn(f); zeigen(d, M, th)
            for an in ("spiel", "seite", "schraeg", "vorn", "nah"): bild(an, os.path.join(aus, "bilder", f"{name}_{f:02d}_{an}.png"))
        print("CLIP", name, flush=True)

# ------------------------------------------------ Schloss entfernen, speichern, exportieren
for o in list(S.values()): bpy.data.objects.remove(o)
rig.animation_data.action = bpy.data.actions["Ruhe"]
L.pose_anwenden(rig, P["ruhe"]); bpy.context.view_layer.update()
bpy.ops.object.select_all(action="DESELECT")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(aus, "handschuh_pixal3d_v6_klinke.blend"))
bpy.ops.export_scene.gltf(filepath=os.path.join(aus, "handschuh_zeiger_roh.glb"), export_format="GLB", export_animations=True, export_animation_mode="ACTIONS", export_skins=True, export_def_bones=False)
print("GLB geschrieben"); print("FERTIG")
