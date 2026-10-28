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
haut_pkt = np.array([mw @ v.co for v in me.vertices])
bvh = BVHTree.FromPolygons([Vector(p) for p in haut_pkt], [t.vertices[:] for t in me.loop_triangles])
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


kopf_mitte = np.array(rig.matrix_world @ (rig.data.bones["head"].head_local + Vector((0, 0.0, 0.09))))


def volumen_geben(st, cm):
    """Haar etwas vom Kopf abheben: jeder Punkt wandert vom Kopfmittelpunkt weg, an der Wurzel 0, zur Spitze voll.
    Gibt der Frisur Fülle, statt dass sie wie nass am Schädel klebt."""
    if cm <= 0:
        return st
    bogen = np.concatenate([np.zeros((len(st), 1)), np.cumsum(np.linalg.norm(np.diff(st, axis=1), axis=2), axis=1)], axis=1)
    t = bogen / np.maximum(bogen[:, -1:], 1e-6)
    richt = st - kopf_mitte
    richt /= np.maximum(np.linalg.norm(richt, axis=2, keepdims=True), 1e-6)
    return st + richt * (cm / 100.0) * np.sqrt(t)[:, :, None]


def ansatz_ausduennen(st, knochen, cm):
    """Weicher Haaransatz an Stirn und Schläfen: Strähnen, deren Wurzel nahe an kahler Stirnhaut sitzt, zufällig
    weglassen (an der Kante bleibt ~15 %, nach cm Zentimetern alle) – statt einer harten Haarlinie."""
    from mathutils.kdtree import KDTree
    wurzeln = st[:, 0]
    kd = KDTree(len(wurzeln))
    for i, p in enumerate(wurzeln):
        kd.insert(Vector(p), i)
    kd.balance()
    auge_z = (rig.matrix_world @ rig.data.bones["eyeball.L"].head_local).z
    vorn = (haut_pkt[:, 2] > auge_z + 0.01) & (haut_pkt[:, 1] < kopf_mitte[1] + 0.02)   # Stirn und Schläfen (HG: vorn = -y)
    kahl = [p for p in haut_pkt[vorn] if kd.find(Vector(p))[2] > 0.006]
    if not kahl:
        return st, knochen
    kk = KDTree(len(kahl))
    for i, p in enumerate(kahl):
        kk.insert(Vector(p), i)
    kk.balance()
    d = np.array([kk.find(Vector(p))[2] for p in wurzeln])
    t = np.clip(d / (cm / 100.0), 0, 1)
    behalten = np.random.default_rng(11).random(len(st)) < (0.15 + 0.85 * t * t * (3 - 2 * t))
    log("Haaransatz weich:", int((~behalten).sum()), "Strähnen am Rand weggelassen")
    return st[behalten], [k for k, b in zip(knochen, behalten) if b]


def teil_exportieren(name, hair, punkte, kinder, knochen_fn, anteil=1.0, volumen=0.0, endung=""):
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
    if anteil < 1.0:
        # Leitsträhnen gleichmäßig ausdünnen (Zufall mit festem Startwert); die Lücken füllen in Godot Folgehaare
        wahl = np.sort(np.random.default_rng(7).choice(len(st), int(len(st) * anteil), replace=False))
        st = st[wahl]; knochen = [knochen[i] for i in wahl]
    if name == "frisur" and P.get("ansatz_weich", 2.0) > 0:
        st, knochen = ansatz_ausduennen(st, knochen, P.get("ansatz_weich", 2.0))
    st = volumen_geben(st, volumen)
    schreibe_tfx(os.path.join(ziel, name + endung + ".tfx"), st)
    schreibe_tfxbone(os.path.join(ziel, name + endung + ".tfxbone"), knochen)
    laenge = np.linalg.norm(np.diff(st, axis=1), axis=2).sum(1)
    log(name + endung, "Strähnen", len(st), "Volumen cm", volumen, "Punkte je Strähne", punkte, "Länge cm Mittel", round(float(laenge.mean() * 100), 1),
        "Knochen", {k: knochen.count(k) for k in set(knochen)})


# Varianten: voll (alle Leitsträhnen) und sparsam (Anteil Leitsträhnen, weniger Punkte; Folgehaare in Godot)
# Bart: deutlich weniger Strähnen als HG-Kindhaare (sonst ein dunkler Block), Simulation in Godot ohnehin aus
for endung, v in P.get("varianten", {"": {"anteil": 1.0, "pf": 16, "pb": 8, "ab": 0.35},
                                     "_sparsam": {"anteil": 0.15, "pf": 8, "pb": 4, "ab": 0.25}}).items():
    teil_exportieren("frisur", h.hair.regular_hair, v["pf"], P.get("kinder_frisur", 100), lambda r: "head",
                     v["anteil"], P.get("volumen_frisur", 0.8), endung)
    if h.gender == "male":
        teil_exportieren("bart", h.hair.face_hair, v["pb"], P.get("kinder_bart", 20),
                         lambda r: "jaw" if mund_z is not None and r[2] < mund_z - 0.012 else "head",
                         v["ab"], P.get("volumen_bart", 0.1), endung)
