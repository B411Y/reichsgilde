# Versuch HG-Köpfe: HG-Haarpartikel (Frisur, Bart) als TressFX-Strähnen (.tfx + .tfxbone) für das Godot-Addon TressFX.
# Aufruf: blender_hg.sh -b <mensch_a.blend> -P hg_tfx.py -- <zielordner> [einstellungen-json]
#
# Weg: HG-Partikelhaar (mit Kindhaaren) per „Modifier umwandeln“ in Linienzüge, je Linienzug die Wurzel am Kopf
# suchen, auf 2^n Punkte umtasten und im TressFX-Format 4 schreiben (wie tressfx_blender_export.py des Addons:
# Z-oben -> Y-oben wie der glTF-Export, 160-Byte-Kopf, die ersten zwei Punkte jeder Strähne fest).
# Knochen: Frisur und Schnurrbart am Kopfknochen „head“, Kinnbart am „jaw“ (den das Porträt mit jawOpen dreht).
import bpy, bmesh, sys, os, json, struct
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from HumGen3D import Human

args = sys.argv[sys.argv.index("--") + 1:]
ziel = args[0]
P = json.loads(args[1]) if len(args) > 1 else {}
os.makedirs(ziel, exist_ok=True)
ctx = bpy.context


def log(*a):
    print("TFX", *a, flush=True)


rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
rig.data.pose_position = "REST"
h = Human.from_existing(rig)
body = h.objects.body
ctx.view_layer.update()
dg = ctx.evaluated_depsgraph_get()
be = body.evaluated_get(dg)
me = be.to_mesh()
me.calc_loop_triangles()
mw = be.matrix_world
bvh = BVHTree.FromPolygons([mw @ v.co for v in me.vertices], [t.vertices[:] for t in me.loop_triangles])
be.to_mesh_clear()
mund_z = (rig.matrix_world @ rig.data.bones["jaw"].tail_local).z if "jaw" in rig.data.bones else None


def linienzuege(obj):
    """Linienzüge eines in ein Netz umgewandelten Partikelsystems (nur Kanten)."""
    m = obj.data
    n = len(m.vertices)
    nb = [[] for _ in range(n)]
    for e in m.edges:
        a, b = e.vertices
        nb[a].append(b); nb[b].append(a)
    co = np.array([obj.matrix_world @ v.co for v in m.vertices])
    gesehen = np.zeros(n, bool)
    zuege = []
    for start in range(n):
        if gesehen[start] or len(nb[start]) != 1:
            continue
        kette = [start]; gesehen[start] = True
        vorher, jetzt = -1, start
        while True:
            weiter = [x for x in nb[jetzt] if x != vorher]
            if not weiter:
                break
            vorher, jetzt = jetzt, weiter[0]
            if gesehen[jetzt]:
                break
            kette.append(jetzt); gesehen[jetzt] = True
        if len(kette) >= 2:
            zuege.append(co[kette])
    return zuege


def umtasten(pts, n):
    d = np.concatenate(([0.0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))))
    if d[-1] <= 1e-6:
        return None
    t = np.linspace(0.0, d[-1], n)
    return np.stack([np.interp(t, d, pts[:, k]) for k in range(3)], axis=1)


def schreibe_tfx(pfad, straehnen):
    s, n, _ = straehnen.shape
    data = np.zeros((s, n, 4), dtype="<f4")
    data[..., 0] = straehnen[..., 0]
    data[..., 1] = straehnen[..., 2]
    data[..., 2] = -straehnen[..., 1]
    data[:, 2:, 3] = 1.0
    with open(pfad, "wb") as f:
        f.write(struct.pack("<fIIIIIII", 4.0, s, n, 160, 0, 0, 0, 0) + bytes(128))
        f.write(data.tobytes())


def schreibe_tfxbone(pfad, knochen_je_straehne):
    namen = sorted(set(knochen_je_straehne))
    idx = {k: i for i, k in enumerate(namen)}
    with open(pfad, "wb") as f:
        f.write(struct.pack("<I", len(namen)))
        for i, k in enumerate(namen):
            raw = k.encode("utf-8")
            f.write(struct.pack("<II", i, len(raw) + 1) + raw + b"\0")
        f.write(struct.pack("<I", len(knochen_je_straehne)))
        for s, k in enumerate(knochen_je_straehne):
            f.write(struct.pack("<I", s))
            for j in range(4):
                f.write(struct.pack("<If", idx[k], 1.0 if j == 0 else 0.0))


def teil_exportieren(name, hair, punkte, kinder, knochen_fn):
    mods = list(hair.modifiers)
    if not mods:
        log(name, "kein Partikelhaar"); return
    zuege = []
    for mod in mods:
        mod.show_viewport = True
        ps = mod.particle_system
        ps.settings.child_percent = kinder           # Kindhaare in der Ansicht (die Umwandlung nimmt die Ansicht)
        ps.settings.display_step = 4
        for o in ctx.selected_objects:
            o.select_set(False)
        with ctx.temp_override(object=body, active_object=body, selected_objects=[body]):
            bpy.ops.object.modifier_convert(modifier=mod.name)
        neu = ctx.view_layer.objects.active
        if neu is None or neu == body:
            continue
        zuege += linienzuege(neu)
        bpy.data.objects.remove(neu)
        mod.show_viewport = False
    straehnen, knochen = [], []
    for z in zuege:
        # Wurzel = Ende näher an der Haut
        d0 = bvh.find_nearest(Vector(z[0]))[3]; d1 = bvh.find_nearest(Vector(z[-1]))[3]
        if d1 < d0:
            z = z[::-1]
        u = umtasten(z, punkte)
        if u is None:
            continue
        straehnen.append(u); knochen.append(knochen_fn(u[0]))
    if not straehnen:
        log(name, "keine Strähnen"); return
    st = np.array(straehnen)
    schreibe_tfx(os.path.join(ziel, name + ".tfx"), st)
    schreibe_tfxbone(os.path.join(ziel, name + ".tfxbone"), knochen)
    laenge = np.linalg.norm(np.diff(st, axis=1), axis=2).sum(1)
    log(name, "Strähnen", len(st), "Punkte je Strähne", punkte, "Länge cm Mittel", round(float(laenge.mean() * 100), 1),
        "Knochen", {k: knochen.count(k) for k in set(knochen)})


teil_exportieren("frisur", h.hair.regular_hair, P.get("punkte_frisur", 16), P.get("kinder_frisur", 100), lambda r: "head")
if h.gender == "male":
    teil_exportieren("bart", h.hair.face_hair, P.get("punkte_bart", 8), P.get("kinder_bart", 100),
                     lambda r: "jaw" if mund_z is not None and r[2] < mund_z - 0.012 else "head")
