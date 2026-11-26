"""Kupferhaube des Doms evangelisch als eigenes Modell (zum Texturieren in einem externen Werkzeug): nur Haube, Stange, Turmkugel und Wetterhahn.
    blender -b --python tools/modelle/neubau/dom_evangelisch_haube_export.py -- --ausgabe=<datei.glb>
Echter Massstab in Metern, Ursprung unten in der Mitte (Traufe bei Z = 0), glatt schattiert, ein neutrales hellgraues Material ohne Textur.
UV (Kanal 0), ohne Ueberlappung: Haube u = Winkel (0..1), v = Bogenlaenge (0..0,78); Stange v 0,80..0,85, Kugel u 0..0,5 v 0,86..0,93, kurze Stange v 0,94..0,97, Hahn Vorderseite u 0,55..0,75 und Rueckseite u 0,775..0,975 (v 0,86..0,955), Hahnkante v 0,975..0,995."""
import math
import os
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dom_evangelisch_masse as F  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ziel = next((a.split("=", 1)[1] for a in args if a.startswith("--ausgabe=")), "haube_evangelisch.glb")
SEITEN = 2 * F.TURM_SEITEN
r = F.TURM_R_OBEN
zh, zsp = F.Z_HAUBE, F.Z_SPITZE
t = lambda anteil: zh + (zsp - zh) * anteil
profil = [(r + 0.30, zh), (r + 0.18, t(0.035)), (r + 0.08, t(0.10)), (r + 0.02, t(0.22)), (r - 0.25, t(0.36)), (r - 0.80, t(0.48)), (r - 1.40, t(0.57)),
          (0.50, t(0.64)), (0.25, t(0.76)), (0.11, t(0.90)), (0.0, zsp)]
profil = [(rad, z - zh) for rad, z in profil]
bpy.ops.wm.read_factory_settings(use_empty=True)
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
laenge = [0.0]
for (r0, z0), (r1, z1) in zip(profil[:-1], profil[1:]):
    laenge.append(laenge[-1] + math.hypot(r1 - r0, z1 - z0))
L = laenge[-1]
ringe = []
for rad, z in profil:
    ringe.append([bm.verts.new((rad * math.cos(2 * math.pi * k / SEITEN), rad * math.sin(2 * math.pi * k / SEITEN), z)) for k in range(SEITEN)])
def flaeche(vs, uvs, glatt=True):
    try:
        f = bm.faces.new(vs)
    except ValueError:
        return None
    f.smooth = glatt
    for l, (u, v) in zip(f.loops, uvs): l[uv].uv = (u, v)
    return f
for j in range(len(profil) - 1):
    v0, v1 = 0.78 * laenge[j] / L, 0.78 * laenge[j + 1] / L
    for k in range(SEITEN):
        k2 = (k + 1) % SEITEN
        u0, u1 = k / SEITEN, (k + 1) / SEITEN
        a, b = ringe[j][k], ringe[j][k2]
        c, d = ringe[j + 1][k2], ringe[j + 1][k]
        flaeche([a, b, c, d], [(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
# Stange (Zylinder, 8 Seiten) bis zur Kugelmitte, Turmkugel (UV-Kugel, 12 Seiten), kurze Stange und Wetterhahn (zwei Blechflaechen und Kante)
import dom_haubenspitze as HS  # noqa: E402
zk = F.Z_KNAUF - zh
n = 8; rs = 0.035
def zylinder(zu, zo, rad, v0, v1, n=8):
    for k in range(n):
        k2 = (k + 1) % n
        p = lambda kk, z: bm.verts.new((rad * math.cos(2 * math.pi * kk / n), rad * math.sin(2 * math.pi * kk / n), z))
        flaeche([p(k, zu), p(k2, zu), p(k2, zo), p(k, zo)], [(k / n, v0), ((k + 1) / n, v0), ((k + 1) / n, v1), (k / n, v1)])
zylinder(zsp - zh - 0.5, zk, rs, 0.80, 0.85)
# Kugel: u = Winkel (0..0,5 der Breite), v 0,86..0,93 nach der Hoehe
kp = HS.kugel_profil(zk)
for j in range(len(kp) - 1):
    va, vb = 0.86 + 0.07 * j / (len(kp) - 1), 0.86 + 0.07 * (j + 1) / (len(kp) - 1)
    for k in range(12):
        k2 = (k + 1) % 12
        q = lambda kk, rz: bm.verts.new((rz[0] * math.cos(2 * math.pi * kk / 12), rz[0] * math.sin(2 * math.pi * kk / 12), rz[1]))
        flaeche([q(k, kp[j]), q(k2, kp[j]), q(k2, kp[j + 1]), q(k, kp[j + 1])], [(0.5 * k / 12, va), (0.5 * (k + 1) / 12, va), (0.5 * (k + 1) / 12, vb), (0.5 * k / 12, vb)])
zylinder(zk + HS.KUGEL_R - 0.05, zk + HS.HAHN_FUSS, 0.025, 0.94, 0.97, 6)
# Hahn: Umriss auf die UV-Zelle u 0,55..1, v 0,86..1 abbilden (beide Seiten dieselbe Zelle liegen uebereinander: Vorder- und Rueckseite spiegeln sich, das Blech ist symmetrisch genug fuer eine Textur; die Kante bekommt eine eigene Zeile v 0,97..1)
z0 = zk + HS.HAHN_FUSS; dk = HS.HAHN_DICKE / 2
xs_ = [p[0] for p in HS.HAHN]; zs_ = [p[1] for p in HS.HAHN]
hu = lambda x: 0.55 + 0.20 * (x - min(xs_)) / (max(xs_) - min(xs_))
hv = lambda z: 0.86 + 0.095 * (z - min(zs_)) / (max(zs_) - min(zs_))
for sd, uo in ((-1, 0.0), (1, 0.225)):                      # Vorderseite links, Rueckseite rechts davon: keine Ueberlappung
    for tri in HS.dreiecke():
        vs = [bm.verts.new((HS.HAHN[i][0], sd * dk, z0 + HS.HAHN[i][1])) for i in tri]
        uvs = [(hu(HS.HAHN[i][0]) + uo, hv(HS.HAHN[i][1])) for i in tri]
        if sd > 0: vs, uvs = vs[::-1], uvs[::-1]
        flaeche(vs, uvs, glatt=False)
nh = len(HS.HAHN)
for i in range(nh):
    j = (i + 1) % nh
    a_, b_ = HS.HAHN[i], HS.HAHN[j]
    vs = [bm.verts.new((a_[0], -dk, z0 + a_[1])), bm.verts.new((b_[0], -dk, z0 + b_[1])), bm.verts.new((b_[0], dk, z0 + b_[1])), bm.verts.new((a_[0], dk, z0 + a_[1]))]
    flaeche(vs, [(0.55 + 0.45 * i / nh, 0.975), (0.55 + 0.45 * (i + 1) / nh, 0.975), (0.55 + 0.45 * (i + 1) / nh, 0.995), (0.55 + 0.45 * i / nh, 0.995)], glatt=False)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me = bpy.data.meshes.new("Haube")
bm.to_mesh(me); bm.free()
ob = bpy.data.objects.new("Haube", me)
bpy.context.scene.collection.objects.link(ob)
m = bpy.data.materials.new("Neutral"); m.use_nodes = True
b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (0.75, 0.75, 0.75, 1); b.inputs["Roughness"].default_value = 0.6; b.inputs["Metallic"].default_value = 0.0
me.materials.append(m)
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
tris = sum(len(p.vertices) - 2 for p in me.polygons)
xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=ziel, export_format="GLB", use_selection=True, export_apply=True, export_yup=True, export_texcoords=True, export_normals=True)
print(f"HAUBE Dreiecke {tris} Breite {max(xs)-min(xs):.2f} Tiefe {max(ys)-min(ys):.2f} Hoehe {max(zs)-min(zs):.2f} zmin {min(zs):.2f} geschrieben {ziel}")
