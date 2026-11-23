"""Spiel-GLB des Zugpferds (Pferd B, Basis "Horse" von henrysteve973, CC BY 4.0): ein Netz "Nah_pferd" (Koerper, Maehne, Hufeisen, Augen, Geschirr) und ein Netz "Fern_pferd" mit EINEM Stoff
(Farbfeld-Atlas mit Alpha: Maehne als Alpha-Maske), Skelett des Autors, die zehn Clips aus Bildfolgen, Anker "Anker_Geschirr". In Meter und auf den Boden normiert.

    python -I tools/modelle/pferd2_spiel.py <nah.npz> <fern.npz> <braun|fuchs|rappe> <texturordner> <ausgabe.glb> --clipdateien=<Name>=<J.npz>;... --ruhepose=<Stehen_J.npz> --info=<geschirr_info.json>
        [--hufeisen=<Horseshoe_baseColor.png>]

Normierung (wie pferd2_b.py "Blender-Koordinaten", ein fester Matrix mn): Die Welt des Autors ist rund 10,5-fach zu gross. Alle Knotenverschiebungen, Kanaele, Inverse-Bind-Matrizen
(Verschiebungsspalte) und Netzecken werden mit s = mn-Massstab multipliziert, der oberste Knoten bekommt Drehung und Verschiebung von mn: so entsteht aus der Haut genau die Welt von
pferd2_b.py (Fuesse auf z = 0, Blick nach +Z, 1,6 m Widerrist), ohne dass der Importer einen Massstab am Wurzelknoten verstehen muss.
Atlas 2048 x 3072 (PNG, RGBA): Fell 2048^2 oben, darunter Haar 1024^2 (links), Hufeisen 512^2, Geschirrfelder 512^2, Augenfeld 512^2.
"""
import io
import json
import os
import struct
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_bind as bd  # noqa: E402
import pferd2_export as EX  # noqa: E402
import pferd2_gang as GG  # noqa: E402

ARGS = sys.argv[1:]
POS = [a for a in ARGS if not a.startswith("--")]
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS if a.startswith("--"))
Image.MAX_IMAGE_PIXELS = None
AW, AH = 2048, 3072
LEDER, HOLZ, EISEN, GURT = (150, 104, 62), (214, 176, 118), (72, 74, 82), (112, 76, 46)


def atlas(variante, texdir, schuh):
    a = Image.new("RGBA", (AW, AH), (0, 0, 0, 255))
    a.paste(Image.open(os.path.join(texdir, f"Horse_{variante}_2048.png")).convert("RGBA"), (0, 0))
    a.paste(Image.open(os.path.join(texdir, f"Hair_{variante}_1024.png")).convert("RGBA"), (0, 2048))
    a.paste(Image.open(schuh).convert("RGB").resize((512, 512)).convert("RGBA"), (1024, 2048))
    a.paste(Image.new("RGBA", (512, 512), (4, 4, 4, 255)), (1536, 2048))
    g = Image.new("RGBA", (512, 512), LEDER + (255,))
    g.paste(Image.new("RGBA", (256, 256), HOLZ + (255,)), (256, 0))
    g.paste(Image.new("RGBA", (256, 256), EISEN + (255,)), (0, 256))
    g.paste(Image.new("RGBA", (256, 256), GURT + (255,)), (256, 256))
    a.paste(g, (1024, 2560))
    return a


def uv_atlas(mat, uv):
    """UV eines Teilnetzes in den Atlas umrechnen (v nach unten wie im glTF)."""
    u, v = uv[:, 0].astype(np.float64), uv[:, 1].astype(np.float64)
    if mat == "Horse":
        return np.stack([u, v * 2048 / AH], 1)
    if mat == "Hair":
        return np.stack([u * 1024 / AW, (2048 + v * 1024) / AH], 1)
    if mat == "Horseshoe":
        return np.stack([(1024 + u * 512) / AW, (2048 + v * 512) / AH], 1)
    if mat == "Cornea":
        return np.stack([np.full_like(u, (1536 + 256) / AW), np.full_like(u, (2048 + 256) / AH)], 1)
    if mat == "Geschirr":
        return np.stack([(1024 + u * 512) / AW, (2560 + v * 512) / AH], 1)
    raise ValueError(mat)


def zusammenfassen(paket):
    """Alle Teilnetze eines Pakets zu einem Netz (Lage, Normale, UV im Atlas, Knochen, Gewichte, Index)."""
    d = np.load(paket, allow_pickle=True)
    P, N, U, JO, W, F, o = [], [], [], [], [], [], 0
    zahl = {}
    for i in range(int(d["n"])):
        mat = str(d[f"mat{i}"])
        pos = d[f"pos{i}"].astype(np.float64)
        idx = d[f"idx{i}"].astype(np.int64)
        n = B.weiche_normalen(pos, idx)
        P.append(pos)
        N.append(n)
        U.append(uv_atlas(mat, d[f"uv{i}"]))
        JO.append(d[f"jo{i}"].astype(np.uint16))
        w = d[f"w{i}"].astype(np.float32)
        W.append(w / np.maximum(w.sum(1, keepdims=True), 1e-9))
        F.append(idx + o)
        o += len(pos)
        zahl[mat] = len(idx)
    return np.vstack(P), np.vstack(N), np.vstack(U), np.vstack(JO), np.vstack(W), np.vstack(F), zahl


def main():
    nah, fern, variante, texdir, ausgabe = POS
    H = B.Haut()
    j0, buf0 = H.j, H.buf
    sk0 = GG.Skelett()
    mn = sk0.mn
    s = float(np.linalg.norm(mn[:3, 0]))                                   # Massstab
    Rl = mn[:3, :3] / s
    Cb = np.array([[1.0, 0, 0], [0, 0, -1.0], [0, 1.0, 0]])               # glTF-Welt -> Blender-Welt (blender_koord)
    Rg = Cb.T @ Rl @ Cb
    tg = Cb.T @ mn[:3, 3]
    Mt = np.eye(4)
    Mt[:3, :3], Mt[:3, 3] = Rg, tg
    pb = EX.Puffer()
    acc = []

    def accessor_neu(arr, typ, comp, target=None, minmax=False):
        arr = np.ascontiguousarray(arr)
        vi = pb.add(arr.tobytes(), target)
        a = {"bufferView": vi, "componentType": comp, "count": int(len(arr)), "type": typ}
        if minmax:
            a["min"] = [float(x) for x in np.atleast_1d(arr.min(0))]
            a["max"] = [float(x) for x in np.atleast_1d(arr.max(0))]
        acc.append(a)
        return len(acc) - 1

    # 1. Clips (Verschiebungen mal s), Ruhepose
    ruhe = np.load(OPT["ruhepose"])
    R0, T0 = ruhe["R"][0], ruhe["T"][0]
    animationen = []
    for eintrag in OPT["clipdateien"].split(";"):
        cname, pfad = eintrag.split("=", 1)
        dat = np.load(pfad)
        Rr, Tt, fps = dat["R"], dat["T"], float(dat["fps"])
        nf = Rr.shape[0]
        a_zeit = accessor_neu((np.arange(nf) / fps).astype(np.float32), "SCALAR", 5126, None, True)
        samplers, channels = [], []
        for i in range(Rr.shape[1]):
            if sk0.M[i] is not None:
                continue
            if np.abs(Rr[:, i] - R0[i][None]).max() > 1e-6:
                q = np.array([GG.mat_quat(Rr[f, i]) for f in range(nf)])
                for f in range(1, nf):
                    if np.dot(q[f], q[f - 1]) < 0:
                        q[f] = -q[f]
                samplers.append({"input": a_zeit, "output": accessor_neu(q.astype(np.float32), "VEC4", 5126), "interpolation": "LINEAR"})
                channels.append({"sampler": len(samplers) - 1, "target": {"node": i, "path": "rotation"}})
            if np.abs(Tt[:, i] - T0[i][None]).max() > 1e-7:
                samplers.append({"input": a_zeit, "output": accessor_neu((Tt[:, i] * s).astype(np.float32), "VEC3", 5126), "interpolation": "LINEAR"})
                channels.append({"sampler": len(samplers) - 1, "target": {"node": i, "path": "translation"}})
        animationen.append({"name": cname, "samplers": samplers, "channels": channels})
    # 2. Haut: Inverse-Bind-Matrizen (Verschiebungsspalte mal s)
    skin = json.loads(json.dumps(j0["skins"][0]))
    ibm = bd.accessor(j0, buf0, skin["inverseBindMatrices"]).reshape(-1, 4, 4).astype(np.float64)     # zeilenweise gelesen: Verschiebung steht in der letzten Zeile? siehe unten
    # bd.accessor liefert die Matrizen so, wie sie im glTF stehen (spaltenweise gespeichert): Verschiebung = Elemente [3][0..2] des gelesenen Felds
    ibm2 = ibm.copy()
    ibm2[:, 3, :3] *= s
    skin["inverseBindMatrices"] = accessor_neu(ibm2.astype(np.float32).reshape(-1, 16), "MAT4", 5126)
    # 3. Netze
    meshes = []
    infos = {}
    for name, paket in (("Nah_pferd", nah), ("Fern_pferd", fern)):
        P, N, U, JO, W, F, zahl = zusammenfassen(paket)
        infos[name] = {"dreiecke": int(len(F)), "ecken": int(len(P)), "teile": zahl}
        a_pos = accessor_neu((P * s).astype(np.float32), "VEC3", 5126, 34962, True)
        a_nrm = accessor_neu(N.astype(np.float32), "VEC3", 5126, 34962)
        a_uv = accessor_neu(U.astype(np.float32), "VEC2", 5126, 34962)
        a_jo = accessor_neu(JO, "VEC4", 5123, 34962)
        a_w = accessor_neu(W, "VEC4", 5126, 34962)
        idx = F.reshape(-1).astype(np.uint16 if len(P) < 65535 else np.uint32)
        a_idx = accessor_neu(idx, "SCALAR", 5123 if len(P) < 65535 else 5125, 34963)
        meshes.append({"name": name, "primitives": [{"attributes": {"POSITION": a_pos, "NORMAL": a_nrm, "TEXCOORD_0": a_uv, "JOINTS_0": a_jo, "WEIGHTS_0": a_w}, "indices": a_idx, "material": 0, "mode": 4}]})
    # 4. Atlas und Stoff
    bio = io.BytesIO()
    at = atlas(variante, texdir, OPT.get("hufeisen", "C:/Reichsgilde_Austausch/pferd3/textures/Horseshoe_baseColor.png"))
    if "png-voll" not in OPT:                                          # 256 Farben mit Alpha (mittlere Abweichung 1,5 von 255): 0,4 statt 4,5 MB je Datei
        at = at.quantize(256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE)
    at.save(bio, "PNG", optimize=True)
    vi = pb.add(bio.getvalue())
    images = [{"bufferView": vi, "mimeType": "image/png"}]
    textures = [{"sampler": 0, "source": 0}]
    mats = [{"name": "Pferd", "alphaMode": "MASK", "alphaCutoff": 0.5, "doubleSided": True,
             "pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicFactor": 0.0, "roughnessFactor": 0.85}}]
    # 5. Knoten: Verschiebungen mal s, Ruhepose, oberster Knoten mit mn, Netze an zwei Knoten, Anker
    nodes = json.loads(json.dumps(j0["nodes"]))
    prim_knoten = [p["knoten"] for p in H.prims]
    for i, nd in enumerate(nodes):
        if "matrix" in nd:
            m = list(nd["matrix"])
            m[12], m[13], m[14] = m[12] * s, m[13] * s, m[14] * s
            nd["matrix"] = m
        else:
            nd["rotation"] = [float(x) for x in GG.mat_quat(R0[i])]
            nd["translation"] = [float(x) * s for x in T0[i]]
            nd["scale"] = [float(x) for x in sk0.S[i]]
        if "mesh" in nd:
            del nd["mesh"]
            nd.pop("skin", None)
    # oberster Knoten
    wurzel_i = j0["scenes"][j0.get("scene", 0)]["nodes"][0]
    m0 = np.array(nodes[wurzel_i]["matrix"]).reshape(4, 4).T                 # spaltenweise gespeichert -> normale Matrix
    m0 = Mt @ m0
    nodes[wurzel_i]["matrix"] = [float(x) for x in m0.T.reshape(-1)]
    for k, (name, mi) in enumerate((("Nah_pferd", 0), ("Fern_pferd", 1))):
        nodes[prim_knoten[k]]["mesh"] = mi
        nodes[prim_knoten[k]]["skin"] = 0
        nodes[prim_knoten[k]]["name"] = name
    info = json.load(open(OPT["info"]))
    ax, ay, az = info["anker_ziehen_godot"][0], info["anker_ziehen_godot"][1], info["anker_ziehen_godot"][2]
    nodes.append({"name": "Anker_Geschirr", "translation": [ax, ay, az]})
    nodes.append({"name": "Pferd", "children": [wurzel_i, len(nodes) - 1]})
    scenes = [{"name": "Pferd", "nodes": [len(nodes) - 1]}]
    j = {"asset": {"version": "2.0", "generator": "pferd2_spiel.py (Reichsgilde)", "copyright": "Basis: Horse by henrysteve973, CC BY 4.0"},
         "scene": 0, "scenes": scenes, "nodes": nodes, "skins": [skin], "meshes": meshes, "materials": mats, "textures": textures, "images": images,
         "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 33071, "wrapT": 33071}], "animations": animationen, "accessors": acc,
         "bufferViews": pb.views, "buffers": [{"byteLength": len(pb.daten)}]}
    js = json.dumps(j, separators=(",", ":")).encode("utf8")
    js += b" " * ((4 - len(js) % 4) % 4)
    bn = bytes(pb.daten) + b"\0" * ((4 - len(pb.daten) % 4) % 4)
    glb = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bn)) + struct.pack("<II", len(js), 0x4E4F534A) + js + struct.pack("<II", len(bn), 0x004E4942) + bn
    os.makedirs(os.path.dirname(ausgabe), exist_ok=True)
    open(ausgabe, "wb").write(glb)
    print("GLB", ausgabe, len(glb) // 1024, "KiB", json.dumps(infos), "Clips", len(animationen))


if __name__ == "__main__":
    main()
