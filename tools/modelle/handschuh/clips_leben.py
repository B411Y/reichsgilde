"""Lebendige Clips fuer die Handschuh-glb: Bereit_atmen, Bereit_zucken, Winken und die Uebergaenge nach/von Winken.
  blender -b <ausordner>/handschuh_pixal3d_v6_klinke.blend --python clips_leben.py -- <ausordner_neu> <tuerschloss.blend> <messwerte_klinke.json> [probe]
Eingabe ist die Blend-Datei von klinke_aus_blend.py (Text-Fassung + Klinke-Clips; Schloss nicht darin), Ausgabe <ausordner_neu>/handschuh_zeiger_roh.glb (danach glb_faltenfarbe.py),
handschuh_pixal3d_v6_leben.blend (fuer leben_bilder.py) und messwerte_leben.json. Alle bisherigen Aktionen bleiben unveraendert.
  Bereit_atmen   2,5 s Schleife (60 Bilder + gleiches Endbild): Finger/Handgelenk +-1,5 Grad, Handlage-Hub +-3 mm; Bild 0 = Klinke_bereit Bild 0.
  Bereit_zucken  14 Bilder (0,583 s): Finger 2-5 beugen 12/16/8 Grad und kehren zurueck, Daumen unveraendert; Anfangs- und Endbild = Bereit.
  Winken         2,75 s (66 Bilder): Handflaeche zur Kamera (Blick der Zeigeransicht), Finger aufrecht und locker offen, Hand schwenkt dreimal +-17 Grad um den Unterarm; Anfangs- und Endpose gleich.
  Uebergang_{Ruhe,Zeigen,Greifen,Text,Bereit}_Winken und Uebergang_Winken_{...}: 7 Bilder bei 24/s (0,2917 s), Smoothstep fuer alle Knochen und die Handlage (wie die Klinken-Uebergaenge);
  von/nach Bereit auf einem Bogen (Anheben/Ausweichen im Raster gesucht), damit die Hand das Schloss nicht schneidet.
Quellhaltungen werden aus dem ersten Bild der Aktionen Ruhe, Zeigen, Greifen, Text und Klinke_bereit gelesen (Knochenwinkel und Handlage)."""
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector, Matrix, Quaternion
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
args = sys.argv[sys.argv.index("--") + 1:]
aus, schloss_blend, mjson = args[0], args[1], args[2]
probe = len(args) > 3 and args[3] == "probe"
os.makedirs(aus, exist_ok=True)
os.environ["TUERSCHLOSS"] = schloss_blend
import v6_klinke_lib as K
from v6_klinke_lib import rig, gl, P, L, tr, rz, QPose
from v6_rig_lib import AX_X, AX_Y, AX_Z
sc = bpy.context.scene
sanft = L.ss_
NAMEN = [pb.name for pb in rig.pose.bones]
mess_alt = json.load(open(mjson)); LAM = Matrix(mess_alt["schloss_matrix"])
S = K.schloss_laden(); S["Tuerschloss"].matrix_world = LAM; bpy.context.view_layer.update()
try: bpy.context.preferences.edit.keyframe_new_interpolation_type = 'LINEAR'
except Exception as e: print("INTERPOLATION", e)

# ------------------------------------------------ Haltungen aus Aktionen lesen (erstes Bild)
def haltung(clip):
    rig.animation_data_create(); rig.animation_data.action = bpy.data.actions[clip]
    sc.frame_set(0); bpy.context.view_layer.update()
    q = QPose({pb.name: (pb.rotation_quaternion.copy() if pb.rotation_mode == "QUATERNION" else pb.rotation_euler.to_quaternion(), Vector(pb.location)) for pb in rig.pose.bones})
    b = rig.data.bones["Wurzel"]; M = b.matrix_local @ rig.pose.bones["Wurzel"].matrix_basis @ b.matrix_local.inverted()
    rig.animation_data.action = None
    return q, M.copy()
QUELLE = {nm: haltung(cl) for nm, cl in (("Ruhe", "Ruhe"), ("Zeigen", "Zeigen"), ("Greifen", "Greifen"), ("Text", "Text"), ("Bereit", "Klinke_bereit"))}
QB, M_B = QUELLE["Bereit"]
rot_lock = LAM.to_quaternion().to_matrix()

def interp(Ma, Mb, t):
    q = Ma.to_quaternion().slerp(Mb.to_quaternion(), t)
    return tr(Ma.to_translation().lerp(Mb.to_translation(), t)) @ q.to_matrix().to_4x4()
LIFT = 0.0; LAT = [0.0, 0.0]
def heben(M, s, am_schloss):
    w = math.sin(math.pi * s) if am_schloss else 0.0
    return tr((rot_lock @ Vector((LAT[0] * w, LAT[1] * w, 0.0))) + Vector((0, 0, LIFT * w))) @ M
def gleich(a, b, s): return K.q_mischen(a, b, {n: s for n in NAMEN})
def klein(q, achsen):
    """q (QPose) mit kleinen Zusatzdrehungen: {Knochen: Grad} um die Beugeachse (Finger) bzw. -X (Handgelenk), im Knochenraum nachgeschaltet."""
    out = QPose(q)
    for n, deg in achsen.items():
        if n.startswith("finger") or n.startswith("metacarpal"): ax = L.flexachse(rig, n)
        else: ax = -AX_X
        dq = K.L.drehung(rig, n, ax, deg); out[n] = (out[n][0] @ dq, out[n][1])
    return out

# ------------------------------------------------ Bereit_atmen: Schleife um die Nutzer-Pose Bereit
def pose_atmen(t):
    a = math.sin(2 * math.pi * t); b = math.sin(4 * math.pi * t)
    d = {"wrist.R": 1.2 * a + 0.5 * b}
    for k in (2, 3, 4, 5):
        d[f"finger{k}-1.R"] = 1.2 * a * (1 + 0.1 * k) ; d[f"finger{k}-2.R"] = 1.5 * b + 0.8 * a
    return klein(QB, d), tr((0, 0, 0.003 * a + 0.001 * b)) @ M_B, 0.0
# ------------------------------------------------ Bereit_zucken: Finger beugen kurz, Daumen bleibt
def bump(t): return math.sin(math.pi * min(1.0, max(0.0, t)) ** 0.75) ** 2
def pose_zucken(t):
    d = {}
    for k in (2, 3, 4, 5):
        tk = (t - 0.04 * (k - 2)) / 0.88; b = bump(tk)
        d[f"finger{k}-1.R"] = 12.0 * b; d[f"finger{k}-2.R"] = 16.0 * b; d[f"finger{k}-3.R"] = 8.0 * b
    return klein(QB, d), M_B, 0.0
# ------------------------------------------------ Winken: Handflaeche zur Kamera, Hand schwenkt um den Unterarm
def winken_basis():
    d = {k: list(v) for k, v in P["ruhe"].items()}
    k0 = {k: L.kipp(rig, k) for k in (2, 3, 4, 5)}
    for k, fl in ((2, (6, 8, 4)), (3, (8, 10, 5)), (4, (10, 12, 6)), (5, (12, 14, 7))):      # locker offen, leicht gefaechert
        L.finger_pose(rig, d, k, fl, 0.7 * k0[k]); d[f"Knoechel{k}.R"] = [(AX_X, max(0.0, d[f"finger{k}-1.R"][2][1]))]
    L.daumen_pose(rig, d, swing=26.0, flex=(4, 6, 4), dreh=(0, 0, 0))
    d["wrist.R"] = [(-AX_X, 0.0), (AX_Y, 0.0)]; d["Wurzel"] = [(-AX_X, 0.0), (AX_Z, 0.0), (AX_Y, 0.0)]
    return K.q_lesen(K.mit_rollachse(d))
QW0 = winken_basis()
M_W = rz(K.LOCK_ROT_ZEIGER) @ Matrix.Rotation(math.pi, 4, "Y")      # Handflaeche zur Kamera (Drehung um die Fingerachse), Finger im Bild nach oben
PIV = M_W @ Vector((0.0, -0.10, 0.0))                                  # Drehpunkt im Unterarm
WINKEL_W = float(os.environ.get("WINKEL_W", "17.0"))
def pose_winken(t):
    u = (t - 0.10) / 0.70
    env = sanft(u * 4) * sanft((1 - u) * 4) if 0 < u < 1 else 0.0           # weich ein und aus
    ph = 2 * math.pi * 3 * u if 0 < u < 1 else 0.0
    th = WINKEL_W * env * math.sin(ph)
    d = {}
    for k in (2, 3, 4, 5):
        d[f"finger{k}-1.R"] = 4.0 * env * math.sin(ph - 0.45 * (k - 2)); d[f"finger{k}-2.R"] = 6.0 * env * math.sin(ph - 0.45 * (k - 2) - 0.5)
    M = tr(PIV) @ rz(th) @ tr(-PIV) @ M_W
    return klein(QW0, d), M, 0.0

# ------------------------------------------------ Uebergaenge nach/von Winken
def ue_nach(nm, t):
    q0, M0 = QUELLE[nm]; s = sanft(t)
    return gleich(q0, QW0, s), heben(interp(M0, M_W, s), s, nm == "Bereit"), 0.0
def ue_von(nm, t):
    q0, M0 = QUELLE[nm]; s = sanft(t)
    return gleich(QW0, q0, s), heben(interp(M_W, M0, s), s, nm == "Bereit"), 0.0

def zeigen(d, M):
    K.pose_mit_wurzel(d, M); bpy.context.view_layer.update()
def paare(fn, n, mit_schloss):
    pa = 0; selbst = 0
    for i in range(n + 1):
        d, M, th = fn(i / n); zeigen(d, M)
        if mit_schloss: pa = max(pa, sum(K.paare_hand_schloss(S).values()))
        selbst = max(selbst, K.messen_pose(d)[0])
    return pa, selbst
# Winken-Pose so hoch ueber dem Schloss, dass die flache Hand es auch bei Bereit nicht trifft (kleinster Abstand aus dem Raster, Schwenk eingeschlossen)
def winken_max(dz):
    global M_W, PIV
    M_W = tr((0, 0, dz)) @ rz(K.LOCK_ROT_ZEIGER) @ Matrix.Rotation(math.pi, 4, "Y"); PIV = M_W @ Vector((0.0, -0.10, 0.0))
    return paare(pose_winken, 33, True)[0]
WZ = 0.0
for dz in (0.0, 0.03, 0.06, 0.09, 0.12, 0.15):
    if winken_max(dz) == 0: WZ = dz; break
else: WZ = 0.15; winken_max(WZ)
print("WINKEN Hoehe ueber dem Schloss", WZ, flush=True)
# freien Bogen fuer Bereit <-> Winken suchen
def bogen_max():
    return max(paares[0] for paares in (paare(lambda t: ue_nach("Bereit", t), 18, True), paare(lambda t: ue_von("Bereit", t), 18, True)))
if bogen_max() > 0:
    erg = []
    for lift in (0.03, 0.06, 0.10, 0.15):
        for lx in (-0.06, -0.03, 0.0, 0.03, 0.06):
            for ly in (-0.06, -0.03, 0.0, 0.03):
                LIFT = lift; LAT[0], LAT[1] = lx, ly; erg.append((bogen_max(), abs(lx) + abs(ly) + lift, lift, lx, ly))
    erg.sort(); b = erg[0]; LIFT = b[2]; LAT[0], LAT[1] = b[3], b[4]; print("BOGEN gesucht", b)

# ------------------------------------------------ Clips: (Name, Bilder, Funktion t->(Pose, Matrix, 0), Schleife, am_schloss, Dauer s)
CLIPS = [("Bereit_atmen", 60, pose_atmen, True, True), ("Bereit_zucken", 14, pose_zucken, False, True), ("Winken", 66, pose_winken, False, True)]
for nm in ("Ruhe", "Zeigen", "Greifen", "Text", "Bereit"):
    CLIPS.append((f"Uebergang_{nm}_Winken", 7, (lambda n_: (lambda t: ue_nach(n_, t)))(nm), False, nm == "Bereit"))
    CLIPS.append((f"Uebergang_Winken_{nm}", 7, (lambda n_: (lambda t: ue_von(n_, t)))(nm), False, nm == "Bereit"))
mess = {"winken_hub_z_m": WZ, "winken_wurzel_matrix": [list(r) for r in M_W], "winken_winkel_grad": WINKEL_W, "bogen_bereit_winken": {"anheben_m": LIFT, "ausweichen_schlossraum_xy_m": LAT}}
for name, n, fn, schleife, am_s in CLIPS:
    pa, selbst = paare(fn, n if not name.startswith("Uebergang") else 18, am_s)
    mess[name] = {"bilder": n + 1, "dauer_s": round(n / 24.0, 4), "schleife": schleife, "max_paare_hand_schloss": pa if am_s else None, "max_selbst_hand": selbst}
    print("MESS", name, mess[name], flush=True)
# Anfangs-/Endbild
def gleich_pose(f1, f2):
    a, Ma, _ = f1; b, Mb, _ = f2
    return max((a[n][0].rotation_difference(b[n][0]).angle for n in NAMEN)) , (Ma.to_translation() - Mb.to_translation()).length
mess["schleife_atmen_ende_gleich_anfang"] = gleich_pose(pose_atmen(0.0), pose_atmen(1.0))
mess["atmen_bild0_gleich_Klinke_bereit_bild0"] = gleich_pose(pose_atmen(0.0), (QB, M_B, 0))
mess["zucken_ende_gleich_bereit"] = gleich_pose(pose_zucken(1.0), (QB, M_B, 0))
mess["winken_ende_gleich_anfang"] = gleich_pose(pose_winken(1.0), pose_winken(0.0))
json.dump(mess, open(os.path.join(aus, "messwerte_leben.json"), "w"), indent=1)
if probe: sys.exit(0)

# ------------------------------------------------ Aktionen bauen und exportieren
def aktion(name, laenge, schleife):
    a = bpy.data.actions.new(name); a.use_fake_user = True
    rig.animation_data_create(); rig.animation_data.action = a
    a.use_frame_range = True; a.frame_range = (0, laenge); a.use_cyclic = schleife
for name, n, fn, schleife, am_s in CLIPS:
    aktion(name, n, schleife)
    for f in range(n + 1):
        d, M, th = fn(f / n); K.pose_keyen_wurzel(d, M, f)
rig.animation_data.action = None
for o in list(S.values()): bpy.data.objects.remove(o)
rig.animation_data.action = bpy.data.actions["Ruhe"]
L.pose_anwenden(rig, P["ruhe"]); bpy.context.view_layer.update()
bpy.ops.object.select_all(action="DESELECT")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(aus, "handschuh_pixal3d_v6_leben.blend"))
bpy.ops.export_scene.gltf(filepath=os.path.join(aus, "handschuh_zeiger_roh.glb"), export_format="GLB", export_animations=True, export_animation_mode="ACTIONS", export_skins=True, export_def_bones=False)
print("GLB geschrieben"); print("FERTIG")
