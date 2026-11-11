"""Bibliothek fuer die Klinken-Clips (Teil 3a): Griffpose, Tuerschloss-Einbindung, Kosten, Wurzel-Bewegung.
Aufgerufen von v6_opt_klinke.py und v6_klinke.py (innerhalb Blenders, auf handschuh_pixal3d_v6_text.blend).
Koordinaten: Handraum (Finger +Y, Handruecken +Z, Daumen -X), Meter. Das Schloss liegt mit Plattennormale +Z, Klinke in Ruhe nach +X.
Umgebung: TEXT (Parameter Haltung Text, noetig fuer v6_text_lib), TUERSCHLOSS = Pfad der tuerschloss.blend.
"""
import bpy, sys, os, math, json
import numpy as np
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
if "TEXT" not in os.environ:
    os.environ["TEXT"] = open(os.path.join(HIER, "text_params.json")).read()
import v6_text_lib as T
import v6_rig_lib as L
from v6_rig_lib import AX_X, AX_Y, AX_Z
rig, gl, P, PZ, PR = T.rig, T.gl, T.P, T.PZ, T.PR
mit_rollachse = T.mit_rollachse

LOCK_ROT_ZEIGER = -40.0       # Drehung des Schlosses um +Z im Handraum: Bildlinks des Spiels (Kamera 40 Grad, HandschuhZeiger.Buehne)
GRIFF_LOKAL = Vector((0.065, 0.035, 0.045))   # Anker_Griff im Schlossraum (Ursprung = Mitte Plattenunterseite)
WELLE_LOKAL = Vector((0.0, 0.035, 0.0))        # Drehpunkt (Achse +Z)
R_GRIFF = 0.0090              # mittlerer Radius der Griffstange
WINKEL = 28.0
VORZ = -1                     # Druecken dreht um -28 Grad um +Z (Klinke zeigt nach rechts, die Spitze geht nach unten)

def rz(grad): return Matrix.Rotation(math.radians(grad), 4, "Z")
def tr(v): return Matrix.Translation(Vector(v))

# ------------------------------------------------------------------ Schloss einbinden
def schloss_laden(pfad=None):
    pfad = pfad or os.environ["TUERSCHLOSS"]
    with bpy.data.libraries.load(pfad, link=False) as (von, nach):
        nach.objects = list(von.objects)
    sc = bpy.context.scene; d = {}
    for o in nach.objects:
        sc.collection.objects.link(o); d[o.name] = o; o.animation_data_clear()
    bpy.context.view_layer.update()
    global GRIFF_LOKAL, WELLE_LOKAL      # Anker aus dem Schloss lesen (Ruhestellung, Wurzel im Ursprung)
    inv = d["Tuerschloss"].matrix_world.inverted()
    GRIFF_LOKAL = (inv @ d["Anker_Griff"].matrix_world).to_translation(); WELLE_LOKAL = (inv @ d["Anker_Welle"].matrix_world).to_translation()
    return d
def schloss_setzen(S, matrix):
    """Wurzel des Schlosses auf matrix (Welt); Klinke steht in Ruhe."""
    S["Tuerschloss"].matrix_world = matrix; S["Klinke"].rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
def matrix_aus(G, psi):
    """Schlossmatrix, sodass Anker_Griff bei G liegt und das Schloss um psi (Grad, um +Z) gedreht ist."""
    R = rz(psi); O = Vector(G) - (R @ GRIFF_LOKAL)
    return tr(O) @ R

# ------------------------------------------------------------------ Griffpose
def griff_pose(par):
    """par: f {k: [flex1, flex2, flex3, kipp, roll]}, daumen {swing, flex[3], dreh[3]}, hand (Handgelenk -X)."""
    d = {k: list(v) for k, v in P["ruhe"].items()}
    for k in (2, 3, 4, 5):
        a = par["f"][str(k)]
        L.finger_pose(rig, d, k, a[:3], a[3], a[4] if len(a) > 4 else 0.0)
        d[f"Knoechel{k}.R"] = [(AX_X, max(0.0, d[f"finger{k}-1.R"][2][1]))]
    dm = par["daumen"]
    L.daumen_pose(rig, d, swing=dm["swing"], flex=dm["flex"], dreh=dm["dreh"])
    d["wrist.R"] = [(-AX_X, par["hand"]), (AX_Y, 0.0)]
    d["Wurzel"] = [(-AX_X, 0.0), (AX_Z, 0.0), (AX_Y, 0.0)]
    return d

# ------------------------------------------------------------------ Wurzel (Verschiebung + Drehung des ganzen Handschuhs)
def wurzel_matrix(M):
    """Setzt die Wurzel so, dass der ganze Handschuh um M (Matrix im Handraum) bewegt wird (Wurzel ist die Wurzel des Skeletts, Ursprung 0)."""
    pb = rig.pose.bones["Wurzel"]; b = rig.data.bones["Wurzel"]
    pb.rotation_mode = "QUATERNION"
    pb.matrix_basis = b.matrix_local.inverted() @ M @ b.matrix_local
class QPose(dict):
    """Pose als Quaternion/Verschiebung je Knochen (wie text_aus_blend.py), erlaubt jede Haltung, auch die vom Nutzer eingestellte Text-Haltung."""
def q_lesen(d):
    L.pose_anwenden(rig, d); bpy.context.view_layer.update()
    return QPose({pb.name: (pb.rotation_quaternion.copy(), Vector(pb.location)) for pb in rig.pose.bones})
def q_setzen(q):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion, pb.location = q[pb.name][0], q[pb.name][1]; pb.scale = (1, 1, 1)
def q_mischen(a, b, p):
    out = QPose()
    for n in a:
        t = p[n]; qa, la = a[n]; qb, lb = b[n]
        if qa.dot(qb) < 0: qb = Quaternion((-qb.w, -qb.x, -qb.y, -qb.z))
        out[n] = (qa.slerp(qb, t), la.lerp(lb, t))
    return out
def zeitparameter(t, schliessen):
    von = {n: [(AX_X, 0.0)] for n in a_namen()}; nach = {n: [(AX_X, 1.0)] for n in a_namen()}
    g = L.gestaffelt(von, nach, t, schliessen, False); return {n: g[n][0][1] for n in a_namen()}
def a_namen(): return [pb.name for pb in rig.pose.bones]
def messen_pose(d):
    """Selbstdurchdringung der Hand (v6-Messung) fuer Winkel- und Quaternion-Posen."""
    if isinstance(d, QPose):
        q_setzen(d); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
        z = T.v2_lib.selbstschnitt_zerlegt(gl, dg); return sum(n for (a, b), n in z.items() if a != b and not (a == "H" and b == "H")), z
    return T.messen_pose(d)
def pose_mit_wurzel(d, M):
    if isinstance(d, QPose): q_setzen(d)
    else: L.pose_anwenden(rig, d)
    wurzel_matrix(M); bpy.context.view_layer.update()
def pose_keyen_wurzel(d, M, frame):
    if isinstance(d, QPose): q_setzen(d)
    else: L.pose_anwenden(rig, d)
    wurzel_matrix(M)
    for k in (list(d) if not isinstance(d, QPose) else a_namen()):
        if k == "Wurzel": continue
        rig.pose.bones[k].keyframe_insert("location" if k.startswith("Knoechel") else "rotation_quaternion", frame=frame, group=k)
    pb = rig.pose.bones["Wurzel"]
    pb.keyframe_insert("rotation_quaternion", frame=frame, group="Wurzel"); pb.keyframe_insert("location", frame=frame, group="Wurzel")

# ------------------------------------------------------------------ Messung
def bvh(ob):
    """BVH in Weltkoordinaten (BVHTree.FromObject ignoriert die Objektmatrix)."""
    e = ob.evaluated_get(bpy.context.evaluated_depsgraph_get()); me = e.to_mesh(); M = e.matrix_world
    verts = [M @ v.co for v in me.vertices]; polys = [tuple(p.vertices) for p in me.polygons]
    t = BVHTree.FromPolygons(verts, polys); e.to_mesh_clear(); return t
def paare_hand_schloss(S, nur_klinke=False):
    """Dreieckspaare Handschuh <-> Schloss (Platte/Rosette/Welle und Klinke)."""
    bpy.context.view_layer.update(); h = bvh(gl)
    n = {}
    for nm in (("Klinke_Netz",) if nur_klinke else ("Schloss", "Klinke_Netz")):
        n[nm] = len(h.overlap(bvh(S[nm])))
    return n
def achse_abstand(p, G, u):
    v = np.array(p) - np.array(G[:]); return float(np.linalg.norm(v - np.dot(v, u) * u))
def welt(b, a="tail"):
    return np.array((rig.matrix_world @ getattr(rig.pose.bones[b], a))[:])

# ------------------------------------------------------------------ Posen und Lage fuer die Klinken-Haltungen (gemeinsam fuer v6_klinke.py, klinke_selbst_bauen.py, klinke_aus_blend.py)
class Lage: pass
def bereit_pose(daumen=22.0):
    """Offene Hand, alle Finger gestreckt (leicht gefaechert), Daumen abgespreizt, Handflaeche nach -Z."""
    d = {k: list(v) for k, v in P["ruhe"].items()}
    k0 = {k: L.kipp(rig, k) for k in (2, 3, 4, 5)}
    for k, fl in ((2, (3, 2, 0)), (3, (2, 2, 0)), (4, (3, 2, 0)), (5, (4, 3, 0))):
        L.finger_pose(rig, d, k, fl, 0.55 * k0[k]); d[f"Knoechel{k}.R"] = [(AX_X, max(0.0, d[f"finger{k}-1.R"][2][1]))]
    L.daumen_pose(rig, d, swing=daumen, flex=(0, 0, 0), dreh=(0, 0, 0))
    d["wrist.R"] = [(-AX_X, 0.0), (AX_Y, 0.0)]; d["Wurzel"] = [(-AX_X, 0.0), (AX_Z, 0.0), (AX_Y, 0.0)]
    return d
def lage(par, ber=(0.07, -0.04, 0.06)):
    """Hand-Matrizen und Schlossort aus der Griffpose par (klinke_params.json); schloss_laden() muss vorher gelaufen sein."""
    g = Lage(); g.PG = mit_rollachse(griff_pose(par)); g.PB = mit_rollachse(bereit_pose()); g.psi = par["psi"]; g.Gp = Vector(par["G"])
    g.alpha = LOCK_ROT_ZEIGER - g.psi; g.M_GRIFF = rz(g.alpha)
    g.LAM = g.M_GRIFF @ matrix_aus(g.Gp, g.psi); g.PS = g.LAM @ Vector(WELLE_LOKAL)
    g.M_BER = tr(rz(LOCK_ROT_ZEIGER).to_3x3() @ Vector(ber)) @ g.M_GRIFF
    g.M_DRUCK = tr(g.PS) @ rz(VORZ * WINKEL) @ tr(-g.PS) @ g.M_GRIFF
    return g
def euler_pose(d, prev=None):
    """Winkel-Pose d als {Knochen: (Euler XYZ, Verschiebung)}; prev = Ergebnis des Bildes davor (fuer kontinuierliche Winkel)."""
    q = q_lesen(d); out = {}
    for n, (qq, loc) in q.items():
        e = qq.to_euler("XYZ", prev[n][0]) if prev else qq.to_euler("XYZ")
        out[n] = (e, loc)
    return out
