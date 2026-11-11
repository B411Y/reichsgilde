"""Clips fuer die Tuerklinke (Teil 3a): Klinke_bereit, Klinke_greifen, Klinke_druecken, Klinke_loslassen, Uebergaenge nach/von Bereit und Anker_Greifen.

Aufruf (Blender -b auf handschuh_pixal3d_v6_text.blend, dem Ergebnis von Teil 2, Pfade absolut):
  blender -b handschuh_pixal3d_v6_text.blend --python v6_klinke.py -- <ausordner> [probe]
Umgebung: TUERSCHLOSS = tuerschloss.blend (Schloss, Teil 3a), KLINKE = JSON mit der Griffpose (klinke_params.json, Ergebnis von v6_opt_klinke.py),
optional BEREIT = "sx,sy,sz" (Versatz der Bereit-Hand im Schlossraum), LIFT.
Ohne "probe": baut die Aktionen in die Handschuh-Datei, setzt Anker_Greifen, exportiert <aus>/handschuh_zeiger_roh.glb (danach glb_faltenfarbe.py),
schreibt messwerte_klinke.json und Bilder nach <aus>/bilder (Hand und Schloss zusammen; Ansichten spiel, seite, schraeg, vorn, nah).
Lage: Die Hand sitzt im Griff nur um Rz(alpha) gedreht um ihren Ursprung (keine Verschiebung); das Schloss steht fest im Handraum so, dass die Griffstange in der Faust liegt.
Alte Aktionen, Netz, Gewichte und Anker bleiben unveraendert.
"""
import bpy, sys, os, math, json
import numpy as np
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
if "KLINKE" not in os.environ:
    os.environ["KLINKE"] = open(os.path.join(HIER, "klinke_params.json")).read()
import v6_klinke_lib as K
from v6_klinke_lib import rig, gl, P, PZ, PR, L, tr, rz, Vector, Matrix
from v6_rig_lib import AX_X, AX_Y, AX_Z
args = sys.argv[sys.argv.index("--") + 1:]
aus = args[0]; probe = len(args) > 1 and args[1] == "probe"
os.makedirs(os.path.join(aus, "bilder"), exist_ok=True)
sc = bpy.context.scene
par = json.loads(os.environ["KLINKE"])
PG = K.mit_rollachse(K.griff_pose(par))
S = K.schloss_laden()
FPS = 24; N_GR, N_DR, N_HALT, N_LOS, N_BER, N_UE = 10, 16, 4, 12, 48, 14; WINKEL = K.WINKEL
Gp = Vector(par["G"]); psi = par["psi"]
sanft = L.ss_
# Text-Haltung: so wie sie in der Datei steht (Pose des Nutzers, Aktion Text, Bild 0), als Quaternion-Pose
rig.animation_data.action = bpy.data.actions["Text"]; sc.frame_set(0); bpy.context.view_layer.update()
PT_Q = K.QPose({pb.name: (pb.rotation_quaternion.copy(), Vector(pb.location)) for pb in rig.pose.bones})
rig.animation_data.action = None

# ------------------------------------------------ Lage von Hand und Schloss
alpha = K.LOCK_ROT_ZEIGER - psi                     # Hand-Drehung, damit das Schloss im Spielbild ausgerichtet ist (Schloss = alpha + psi = -40 Grad)
M_GRIFF = rz(alpha)                                  # nur drehen, kein Versatz: die Hand bleibt dort, wo sie in allen Clips ist
LAM_REL = K.matrix_aus(Gp, psi)                      # Schloss relativ zur Hand in der Griffpose
LAM = M_GRIFF @ LAM_REL                              # Schloss im Handraum (fest)
PS = LAM @ Vector(K.WELLE_LOKAL)                     # Drehpunkt der Klinke im Handraum
def M_druck(theta): return tr(PS) @ rz(theta) @ tr(-PS) @ M_GRIFF
print("GRIFF alpha", round(alpha, 1), "Schloss", [round(v, 4) for v in LAM.to_translation()], "Pivot", [round(v, 4) for v in PS])

# ------------------------------------------------ Pose Bereit: offene Hand, alle Finger gestreckt, Handflaeche zur Klinke (nach -Z), darueber und etwas zurueck
def bereit_pose():
    d = {k: list(v) for k, v in P["ruhe"].items()}
    k0 = {k: L.kipp(rig, k) for k in (2, 3, 4, 5)}
    for k, fl in ((2, (3, 2, 0)), (3, (2, 2, 0)), (4, (3, 2, 0)), (5, (4, 3, 0))):
        L.finger_pose(rig, d, k, fl, 0.55 * k0[k]); d[f"Knoechel{k}.R"] = [(AX_X, max(0.0, d[f"finger{k}-1.R"][2][1]))]
    L.daumen_pose(rig, d, swing=float(os.environ.get("DAUMEN", "22")), flex=(0, 0, 0), dreh=(0, 0, 0))
    d["wrist.R"] = [(-AX_X, 0.0), (AX_Y, 0.0)]; d["Wurzel"] = [(-AX_X, 0.0), (AX_Z, 0.0), (AX_Y, 0.0)]
    return d
PB = bereit_pose()
BER = [float(x) for x in os.environ.get("BEREIT", "0.0,-0.02,0.065").split(",")]
M_BER = tr(rz(K.LOCK_ROT_ZEIGER).to_3x3() @ Vector(BER)) @ M_GRIFF

def interp(Ma, Mb, t):
    """Pose-Matrix zwischen zwei Matrizen (Drehung um Z und Verschiebung)."""
    ea = Ma.to_euler("XYZ").z; eb = Mb.to_euler("XYZ").z
    return tr(Ma.to_translation().lerp(Mb.to_translation(), t)) @ Matrix.Rotation(ea + (eb - ea) * t, 4, "Z")
LIFT = float(os.environ.get("LIFT", "0.03"))
def heben(M, s): return tr((0, 0, LIFT * math.sin(math.pi * s))) @ M

def pose_bereit(t):
    d = K.T.atmen(PB, t, 0.1); d["Wurzel"] = [(-AX_X, 0.0), (AX_Z, 0.0), (AX_Y, 0.0)]
    return d, tr((0, 0, 0.004 * math.sin(2 * math.pi * t))) @ M_BER, 0.0
def uebergang_nach(von, t):
    return L.gestaffelt(von, PB, t, False), heben(interp(Matrix.Identity(4), M_BER, sanft(t)), sanft(t)), 0.0
def uebergang_von(nach, t):
    return L.gestaffelt(PB, nach, t, True), heben(interp(M_BER, Matrix.Identity(4), sanft(t)), sanft(t)), 0.0
W1 = [float(x) for x in os.environ.get("W1", "-0.03,-0.06,0.03").split(",")]               # Zwischenhalt (Schlossraum: Versatz zur Griffstellung): erst daneben, dann heranziehen
VOR = float(os.environ.get("VOR", "0.6"))                                                 # Anteil geschlossen am Zwischenhalt (vorgeformte Hand)
U1 = 0.5
def setze_w1(w, vor):
    global M_W1, VOR
    M_W1 = tr(rz(K.LOCK_ROT_ZEIGER).to_3x3() @ Vector(w)) @ M_GRIFF; VOR = vor
setze_w1(W1, VOR)
def zustand(u):
    """Annaeherung Bereit -> Zwischenhalt (Hand halb geschlossen) -> Griff: Pose und Matrix; die Oeffnung der Faust zeigt zur Griffstange."""
    if u < U1: M = interp(M_BER, M_W1, sanft(u / U1)); sf = VOR * sanft(u / U1)
    else: M = interp(M_W1, M_GRIFF, sanft((u - U1) / (1 - U1))); sf = VOR + (1 - VOR) * sanft((u - U1) / (1 - U1))
    return L.gestaffelt(PB, PG, sf, True), M
PB_Q = K.q_lesen(PB)
def uebergang_nach_q(qv, t):
    return K.q_mischen(qv, PB_Q, K.zeitparameter(t, False)), heben(interp(Matrix.Identity(4), M_BER, sanft(t)), sanft(t)), 0.0
def uebergang_von_q(qn, t):
    return K.q_mischen(PB_Q, qn, K.zeitparameter(t, True)), heben(interp(M_BER, Matrix.Identity(4), sanft(t)), sanft(t)), 0.0
def pose_greifen(t):
    d, M = zustand(t); return d, M, 0.0
def pose_druecken(f):
    th = K.VORZ * WINKEL * sanft(min(f, N_DR) / N_DR)
    return PG, M_druck(th), th
T_BACK = 0.34                                       # Anteil von Loslassen, in dem Hand und Klinke gemeinsam zurueckfedern
def pose_loslassen(t):
    if t < T_BACK:
        th = K.VORZ * WINKEL * (1 - sanft(t / T_BACK)); return PG, M_druck(th), th
    d, M = zustand(1 - (t - T_BACK) / (1 - T_BACK)); return d, M, 0.0

def zeigen(d, M, theta):
    K.pose_mit_wurzel(d, M); S["Klinke"].rotation_euler = (0, 0, math.radians(theta))
    S["Tuerschloss"].matrix_world = LAM; bpy.context.view_layer.update()

# ------------------------------------------------ Anker_Greifen (Elternknochen wrist.R, Mitte der Griffstange in der Griffpose)
for o in list(bpy.data.objects):
    if o.name == "Anker_Greifen": bpy.data.objects.remove(o)
ak = bpy.data.objects.new("Anker_Greifen", None); ak.empty_display_type = "PLAIN_AXES"; ak.empty_display_size = 0.01
sc.collection.objects.link(ak); ak.parent = rig; ak.parent_type = "BONE"; ak.parent_bone = "wrist.R"
L.pose_anwenden(rig, PG); bpy.context.view_layer.update(); ak.matrix_world = Matrix.Translation(Gp)

for o in bpy.data.objects:
    if o.name.startswith("Hilfe_Marke"): o.hide_render = True
cam = L.buehne(384)

def spiel_kamera(cam, ortho=0.45):
    """Spielkamera (HandschuhZeiger.Buehne): Blick von oben, 16 Grad Neigung, Bild um 40 Grad gedreht (Finger links oben)."""
    def gl2bl(v): return Vector((v.x, -v.z, v.y))
    dreh = Matrix.Rotation(math.radians(-40), 3, "Y"); tilt = math.radians(16)
    off = gl2bl(dreh @ Vector((0, math.cos(tilt), math.sin(tilt)))); up = gl2bl(dreh @ Vector((0, 0, -1)))
    mitte = gl2bl(Vector((0, 0, -0.045)))
    z = off.normalized(); y = (up - z * up.dot(z)).normalized(); x = y.cross(z)
    m = Matrix(((x.x, y.x, z.x, 0), (x.y, y.y, z.y, 0), (x.z, y.z, z.z, 0), (0, 0, 0, 1)))
    m.translation = mitte + z * 0.8
    cam.matrix_world = m; cam.data.ortho_scale = ortho; cam.data.clip_start = 0.05; cam.data.clip_end = 3.0
def bild_rendern(name, pfad):
    if name == "spiel": spiel_kamera(cam, 0.45)
    elif name == "nah":    # Nahbild des Griffs: schraeg von vorn-rechts, auf die Griffstange (Anker_Griff) zentriert
        v = (rz(K.LOCK_ROT_ZEIGER).to_3x3() @ Vector((0.35, -0.75, 0.55))).normalized(); mitte = S["Anker_Griff"].matrix_world.translation
        cam.data.ortho_scale = 0.16; cam.data.clip_start = 0.01; cam.data.clip_end = 5
        cam.location = mitte + v; cam.rotation_euler = (-v).to_track_quat("-Z", "Y").to_euler()
    else:
        d = {"seite": (-1, -0.02, 0.2), "schraeg": (-0.5, -0.9, 0.7), "vorn": (0.15, 1, 0.45), "oben": (0.0, -0.001, 1)}[name]
        v = Vector(d).normalized(); cam.data.ortho_scale = 0.40; cam.data.clip_start = 0.01; cam.data.clip_end = 5
        cam.location = Vector((0.0, 0.08, -0.04)) + v; cam.rotation_euler = (-v).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.render.filepath = pfad; bpy.ops.render.render(write_still=True)
ANS = ("spiel", "seite", "schraeg", "vorn", "nah")

if probe:
    for nm, (d, M, th) in {"bereit": pose_bereit(0.0), "griff": (PG, M_GRIFF, 0.0), "druck": pose_druecken(16)}.items():
        zeigen(d, M, th)
        print("PROBE", nm, K.paare_hand_schloss(S), [round(v, 3) for v in S["Anker_Griff"].matrix_world.translation], round(math.degrees(S["Klinke"].rotation_euler.z), 1))
        for an in ANS: bild_rendern(an, os.path.join(aus, "bilder", f"probe_{nm}_{an}.png"))
    sys.exit(0)

# ------------------------------------------------ Clips (Name, Bilder, Funktion Bild -> (Pose, Matrix, Klinkenwinkel), Schritt fuer die Bilder)
CLIPS = [
    ("Klinke_bereit", N_BER, lambda f: pose_bereit(f / N_BER), 8, True),
    ("Klinke_greifen", N_GR, lambda f: pose_greifen(f / N_GR), 1, False),
    ("Klinke_druecken", N_DR + N_HALT, pose_druecken, 2, False),
    ("Klinke_loslassen", N_LOS, lambda f: pose_loslassen(f / N_LOS), 1, False),
]
for quelle, nm in ((PR, "Ruhe"), (P["zeigen"], "Zeigen")):
    q = K.mit_rollachse(quelle)
    CLIPS.append((f"Uebergang_{nm}_Bereit", N_UE, (lambda q_: (lambda f: uebergang_nach(q_, f / N_UE)))(q), 2, False))
    CLIPS.append((f"Uebergang_Bereit_{nm}", N_UE, (lambda q_: (lambda f: uebergang_von(q_, f / N_UE)))(q), 2, False))
CLIPS.append(("Uebergang_Text_Bereit", N_UE, lambda f: uebergang_nach_q(PT_Q, f / N_UE), 2, False))
CLIPS.append(("Uebergang_Bereit_Text", N_UE, lambda f: uebergang_von_q(PT_Q, f / N_UE), 2, False))

def aktion(name, laenge, schleife):
    a = bpy.data.actions.new(name); a.use_fake_user = True
    rig.animation_data_create(); rig.animation_data.action = a
    a.use_frame_range = True; a.frame_range = (0, laenge); a.use_cyclic = schleife
    return a
for name, ln, fn, st, schleife in CLIPS:
    aktion(name, ln, schleife)
    for f in range(ln + 1):
        d, M, th = fn(f); K.pose_keyen_wurzel(d, M, f)
rig.animation_data.action = None   # sonst ueberschreibt die zuletzt gesetzte Aktion bei jedem update() die Handpose (Messung und Bilder)
sc.frame_set(0); L.pose_anwenden(rig, P["ruhe"]); bpy.context.view_layer.update()

# ------------------------------------------------ Messung (Dreieckspaare Hand-Klinke/Schloss je Bild, Hand selbst wie v6)
mess = {"anker_greifen": [round(float(v), 4) for v in Gp], "alpha_grad": round(alpha, 2), "schloss": [round(v, 4) for v in LAM.to_translation()],
        "schloss_drehung_grad": round(math.degrees(LAM.to_euler("XYZ").z), 2), "pivot": [round(v, 4) for v in PS], "bereit_versatz": BER}
def folge(fn, laenge):
    mx = {"Schloss": 0, "Klinke_Netz": 0, "selbst": 0}; je = []
    for f in range(laenge + 1):
        d, M, th = fn(f); zeigen(d, M, th)
        p = K.paare_hand_schloss(S); inter, _ = K.messen_pose(d); zeigen(d, M, th)
        je.append(sum(p.values()))
        mx["Schloss"] = max(mx["Schloss"], p["Schloss"]); mx["Klinke_Netz"] = max(mx["Klinke_Netz"], p["Klinke_Netz"]); mx["selbst"] = max(mx["selbst"], inter)
    mx["je_bild"] = je; return mx
for name, ln, fn, st, schleife in CLIPS: mess[name] = folge(fn, ln)
json.dump(mess, open(os.path.join(aus, "messwerte_klinke.json"), "w"), indent=1)
print("MESSWERTE", json.dumps({k: (v if not isinstance(v, dict) else {a: b for a, b in v.items() if a != "je_bild"}) for k, v in mess.items()}))

if os.environ.get("SUCHE"):    # Raster fuer Zwischenhalt und Vorformung: Dreieckspaare Hand-Klinke im Greifen (mehr Bilder fein), kleinste zuerst
    erg = []
    for wy in (-0.06, -0.03, 0.0, 0.03):
        for wx in (-0.03, 0.0, 0.03):
            for wz in (0.0, 0.03, 0.06):
                for vor in (0.35, 0.6):
                    setze_w1((wx, wy, wz), vor); mx = 0
                    for f in range(0, 41):
                        d, M, th = pose_greifen(f / 40); zeigen(d, M, th); p = K.paare_hand_schloss(S); mx = max(mx, sum(p.values()))
                    erg.append((mx, wx, wy, wz, vor)); print("SUCHE", erg[-1], flush=True)
    erg.sort(); print("BESTE", erg[:6]); sys.exit(0)
if os.environ.get("NOBILD"): sys.exit(0)
# ------------------------------------------------ Bilder (vor dem Export, weil Schloss und Licht in der Szene stehen)
for name, ln, fn, st, schleife in ([] if os.environ.get("KEINEBILDER") else CLIPS):
    for f in range(0, ln + 1, st):
        d, M, th = fn(f); zeigen(d, M, th)
        for an in ANS: bild_rendern(an, os.path.join(aus, "bilder", f"{name}_{f:02d}_{an}.png"))
    print("CLIP", name, flush=True)

# ------------------------------------------------ Schloss aus der Szene nehmen, speichern, exportieren
for o in list(S.values()):
    bpy.data.objects.remove(o)
rig.animation_data.action = bpy.data.actions["Ruhe"]
L.pose_anwenden(rig, P["ruhe"]); bpy.context.view_layer.update()
bpy.ops.object.select_all(action="DESELECT")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(aus, "handschuh_pixal3d_v6_klinke.blend"))
bpy.ops.export_scene.gltf(filepath=os.path.join(aus, "handschuh_zeiger_roh.glb"), export_format="GLB", export_animations=True, export_animation_mode="ACTIONS", export_skins=True, export_def_bones=False)
print("GLB geschrieben")
print("FERTIG")
