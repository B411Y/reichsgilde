"""Bilder aus dem SPIEL-GLB selbst (nicht aus dem Werkzeug-Render): liest tier_pferd_*.glb mit eigenem Leser (Knoten, Inverse-Bind-Matrizen, Kanaele, Netz Nah_pferd mit Normalen, UV, Atlas),
haeutet eine Pose des Clips (--klip=<Name> --bild=<Nr>, ohne Angabe die Ruhepose) und rendert in Blender mit dem Stoff des GLB: Atlas als Farbe, Alpha-Maske 0,5, RUECKSEITEN WEGGESCHNITTEN
(--cull=1, Standard) und den Normalen der Datei. Kein Blender-Skelett (der glTF-Import von Blender verzerrt dieses Skelett bekanntermassen).

    blender -b --python tools/modelle/pferd2_spielrender.py -- --glb=<tier_pferd_*.glb> --aus=<Ordner> [--klip=Stehen] [--bild=0] [--ansichten=seite,schraeg,vorn,kopf,huf]
        [--cull=1|0] [--b=1000] [--h=700] [--proben=16] [--tag=<text>]
        Gespann: --karren=<karren_pferd.glb> [--klip=Ziehen --bild=6 --bild2=19] [--ansichten=oben,spiel,vorn,seite,vorn_nah] [--zugtiere=<zugtiere.json>]: zwei Pferde an Anker_Pferd_L/R
        (Platzierung wie im Spiel: Tier-Wurzel = Anker - anker_ziehen_godot aus zugtiere.json)
"""
import json
import os
import struct
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_bilder as pb  # noqa: E402

OPT = pb.OPT
GESPANN_ANSICHTEN = {"oben": ((0.0, -5.3, 0.6), 0, 89.0, 12.0), "spiel": ((0.0, -4.4, 0.8), 40, 38, 12.0), "vorn": ((0.0, -6.0, 0.9), -35, 12, 6.5), "seite": ((0.0, -4.4, 0.9), 0, 0, 11.0),
                     "vorn_nah": ((0.0, -7.0, 1.2), -20, 8, 3.4)}


def lade(pfad):
    b = open(pfad, "rb").read()
    l = struct.unpack("<I", b[12:16])[0]
    return json.loads(b[20:20 + l]), b[20 + l + 8:]


def acc(j, buf, i):
    a = j["accessors"][i]
    v = j["bufferViews"][a["bufferView"]]
    n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]]
    dt = {5126: np.float32, 5123: np.uint16, 5125: np.uint32}[a["componentType"]]
    o = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    return np.frombuffer(buf, dtype=dt, count=a["count"] * n, offset=o).reshape(a["count"], n)


def knoten_matrix(nd, T=None, R=None):
    if "matrix" in nd:
        return np.array(nd["matrix"]).reshape(4, 4).T
    t = np.array(nd.get("translation", [0, 0, 0])) if T is None else T
    x, y, z, w = nd.get("rotation", [0, 0, 0, 1]) if R is None else R
    s = np.array(nd.get("scale", [1, 1, 1]))
    Rm = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                   [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    M = np.eye(4)
    M[:3, :3] = Rm * s[None, :]
    M[:3, 3] = t
    return M


class Pferd:
    """Nah_pferd aus dem GLB, in einer Pose gehaeutet (Welt in Metern, y oben, +z nach vorn)."""

    def __init__(self, pfad):
        self.j, self.buf = lade(pfad)
        j, buf = self.j, self.buf
        self.eltern = {}
        for i, nd in enumerate(j["nodes"]):
            for c in nd.get("children", []):
                self.eltern[c] = i
        self.skin = j["skins"][0]
        self.ibm = acc(j, buf, self.skin["inverseBindMatrices"]).reshape(-1, 4, 4)
        p = next(m for m in j["meshes"] if m["name"] == "Nah_pferd")["primitives"][0]
        a = p["attributes"]
        self.P = acc(j, buf, a["POSITION"]).astype(float)
        self.N = acc(j, buf, a["NORMAL"]).astype(float)
        self.UV = acc(j, buf, a["TEXCOORD_0"]).astype(float)
        self.JO = acc(j, buf, a["JOINTS_0"]).astype(int)
        self.W = acc(j, buf, a["WEIGHTS_0"]).astype(float)
        self.F = acc(j, buf, p["indices"]).reshape(-1, 3).astype(int)
        self.ani = {x["name"]: x for x in j["animations"]}
        self.material = j["materials"][p.get("material", 0)]
        bv = j["bufferViews"][j["images"][0]["bufferView"]]
        self.bild = buf[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]

    def pose(self, klip=None, bild=0):
        j, buf = self.j, self.buf
        ueber = {}
        if klip:
            c0 = self.ani[klip]
            for c in c0["channels"]:
                s = c0["samplers"][c["sampler"]]
                ueber.setdefault(c["target"]["node"], {})[c["target"]["path"]] = acc(j, buf, s["output"])[bild]
        glob = {}

        def g(i):
            if i not in glob:
                u = ueber.get(i, {})
                m = knoten_matrix(j["nodes"][i], u.get("translation"), u.get("rotation"))
                glob[i] = (g(self.eltern[i]) @ m) if i in self.eltern else m
            return glob[i]
        Jm = np.array([g(ji) @ self.ibm[k].T for k, ji in enumerate(self.skin["joints"])])
        ph = np.hstack([self.P, np.ones((len(self.P), 1))])
        Q = np.zeros((len(self.P), 3))
        Nw = np.zeros((len(self.P), 3))
        for s in range(4):
            M = Jm[self.JO[:, s]]
            Q += self.W[:, s:s + 1] * np.einsum("nij,nj->ni", M, ph)[:, :3]
            Nw += self.W[:, s:s + 1] * np.einsum("nij,nj->ni", M[:, :3, :3], self.N)
        Nw /= np.maximum(np.linalg.norm(Nw, axis=1, keepdims=True), 1e-12)
        return Q, Nw


def material(pferd, cull):
    m = bpy.data.materials.new("Pferd")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = float(pferd.material["pbrMetallicRoughness"].get("roughnessFactor", 0.85))
    pfad = os.path.join(bpy.app.tempdir, "atlas_%d.png" % id(pferd))
    open(pfad, "wb").write(pferd.bild)
    im = bpy.data.images.load(pfad)
    im.pack()
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = im
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    ende = bsdf.outputs["BSDF"]
    transp = nt.nodes.new("ShaderNodeBsdfTransparent")
    gt = nt.nodes.new("ShaderNodeMath")                              # Alpha-Maske (MASK, Schwelle 0,5)
    gt.operation = "GREATER_THAN"
    gt.inputs[1].default_value = float(pferd.material.get("alphaCutoff", 0.5)) - 1e-4
    nt.links.new(tex.outputs["Alpha"], gt.inputs[0])
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(gt.outputs[0], mix.inputs["Fac"])
    nt.links.new(transp.outputs["BSDF"], mix.inputs[1])
    nt.links.new(ende, mix.inputs[2])
    ende = mix.outputs["Shader"]
    if cull:                                                         # Rueckseiten weg: Backfacing -> durchsichtig
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        mix2 = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(geo.outputs["Backfacing"], mix2.inputs["Fac"])
        nt.links.new(ende, mix2.inputs[1])
        nt.links.new(transp.outputs["BSDF"], mix2.inputs[2])
        ende = mix2.outputs["Shader"]
    nt.links.new(ende, out.inputs["Surface"])
    return m


def netz(name, pferd, Q, Nw, mat, versatz=(0, 0, 0)):
    """glTF (y oben, +z vorn) -> Blender (z oben, Blick -y): (x, y, z) -> (x, -z, y)."""
    def B(a):
        return np.stack([a[:, 0], -a[:, 2], a[:, 1]], axis=1)
    V = B(Q) + np.array(versatz)
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in V], [], [tuple(f) for f in pferd.F])
    me.update()
    uvl = me.uv_layers.new(name="UV")
    for lp in me.loops:
        u = pferd.UV[lp.vertex_index]
        uvl.data[lp.index].uv = (u[0], 1.0 - u[1])
    me.shade_smooth()
    me.normals_split_custom_set_from_vertices([tuple(n) for n in B(Nw)])
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(mat)
    return ob


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cull = OPT.get("cull", "1") != "0"
    pferd = Pferd(OPT["glb"])
    kam = pb.buehne()
    os.makedirs(OPT["aus"], exist_ok=True)
    b, h = int(OPT.get("b", 1000)), int(OPT.get("h", 700))
    tag = OPT.get("tag", "")
    if "karren" in OPT:
        bpy.ops.import_scene.gltf(filepath=OPT["karren"])
        for o in bpy.data.objects:
            if o.type == "MESH" and o.name.startswith(("Stuetze", "Ladung", "Plane")):
                o.hide_render = True
        anker = {o.name: o for o in bpy.data.objects if o.type == "EMPTY" and o.name.startswith("Anker_")}
        zt = json.load(open(OPT.get("zugtiere", os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "game", "daten", "zugtiere.json")), encoding="utf-8"))
        az = zt["tiere"]["pferd"]["anker_ziehen_godot"]                # (x, hoch, vor) Godot -> Blender (x, -vor, hoch)
        mat = material(pferd, cull)
        klip = OPT.get("klip", "Ziehen")
        for k, seite in enumerate(("Anker_Pferd_L", "Anker_Pferd_R")):
            a = anker[seite].matrix_world.translation
            Q, Nw = pferd.pose(klip, int(OPT.get("bild", 6)) if k == 0 else int(OPT.get("bild2", 19)))
            netz(f"Pferd{k}", pferd, Q, Nw, mat, (a.x - az[0], a.y + az[2], a.z - az[1]))
        for an in OPT.get("ansichten", "oben,spiel,vorn,seite,vorn_nah").split(","):
            ziel, az_, el, mass = GESPANN_ANSICHTEN[an]
            pb.aufnahme(kam, Vector(ziel), az_, el, mass, os.path.join(OPT["aus"], f"spielglb_gespann_{an}{tag}.png"), 1400, 900)
        return
    Q, Nw = pferd.pose(OPT.get("klip"), int(OPT.get("bild", 0)))
    netz("Pferd", pferd, Q, Nw, material(pferd, cull))
    for an in OPT.get("ansichten", "seite,schraeg,vorn,kopf").split(","):
        ziel, az_, el, mass, lat = pb.ANSICHTEN[an]
        for o in [o for o in bpy.data.objects if o.name.startswith(("_lm", "_lt"))]:
            bpy.data.objects.remove(o)
        pb.latte(az_, lat)
        pb.aufnahme(kam, Vector(ziel), az_, el, mass, os.path.join(OPT["aus"], f"spielglb_{an}_{OPT.get('klip', 'ruhe')}_{int(OPT.get('bild', 0)):03d}{tag}.png"), b, h)


if __name__ == "__main__":
    main()
