"""GLB-Export von Pferd B nach der Form: neue (reduzierte) Netze, Skelett, Gelenke und ALLE Clips des Autors unveraendert, ohne Sattel/Steigbuegel, Texturen eingebettet.
Reines numpy (normales Python): python -I tools/modelle/pferd2_export.py <paket.npz> <variante braun|fuchs|rappe> <texturordner> <ausgabe.glb> [--nur-clips=Skeleton|1 Ilde,...]

Das Paket kommt aus pferd2_form_b.py / pferd2_reduce.py (je Netz: mat, pos, uv, jo, w, idx in den Rohkoordinaten der glTF).
Materialien: Horse = Fell-Textur (JPEG, 2048), Hair = Haar-Textur RGBA (PNG 1024, Alpha = Strahnenkarte, alphaMode MASK 0,5), Horseshoe = Original 512 (PNG), Cornea unveraendert.
Keine Normalmaps. Namen der Knoten bleiben wie im Original (Umbenennung kommt im Einbau).
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

ARGS = sys.argv[1:]
POS = [a for a in ARGS if not a.startswith("--")]
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS if a.startswith("--"))
Image.MAX_IMAGE_PIXELS = None


class Puffer:
    def __init__(self):
        self.daten = bytearray()
        self.views = []

    def add(self, b, target=None):
        while len(self.daten) % 4:
            self.daten.append(0)
        v = {"buffer": 0, "byteOffset": len(self.daten), "byteLength": len(b)}
        if target:
            v["target"] = target
        self.daten += b
        self.views.append(v)
        return len(self.views) - 1


def normalen(P, idx):
    fn = np.cross(P[idx[:, 1]] - P[idx[:, 0]], P[idx[:, 2]] - P[idx[:, 0]])
    key = np.round(P * 2000).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.reshape(-1)
    acc = np.zeros((inv.max() + 1, 3))
    for k in range(3):
        np.add.at(acc, inv[idx[:, k]], fn)
    n = acc[inv]
    return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)


def main():
    paket, variante, texdir, ausgabe = POS
    H = B.Haut()
    j0, buf0 = H.j, H.buf
    d = np.load(paket, allow_pickle=True)
    pb = Puffer()
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

    # 1. Animationen und Haut: Zahlen (float32) in neue Puffer kopieren
    kop = {}

    def kopiere(i):
        if i in kop:
            return kop[i]
        a0 = j0["accessors"][i]
        arr = bd.accessor(j0, buf0, i).astype(np.float32)
        typ = a0["type"]
        n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[typ]
        arr = arr.reshape(-1, n) if n > 1 else arr.reshape(-1, 1)
        mm = typ == "SCALAR" and "max" in a0
        kop[i] = accessor_neu(arr if n > 1 else arr[:, 0], typ, 5126, None, minmax=mm)
        return kop[i]
    animationen = json.loads(json.dumps(j0["animations"]))
    nur = OPT.get("nur-clips")
    if nur:
        animationen = [a for a in animationen if a["name"] in nur.split(",")]
    if "autor-clips" in OPT:                                          # z. B. "Skeleton|Sleep:Schlafen,Skeleton|Arise:Aufstehen": Clips des Autors umbenannt uebernehmen
        wahl = dict(x.split(":") for x in OPT["autor-clips"].split(",") if x)
        animationen = [dict(a, name=wahl[a["name"]]) for a in animationen if a["name"] in wahl]
    elif "clipdateien" in OPT:
        animationen = []
    for an in animationen:
        for s in an["samplers"]:
            s["input"], s["output"] = kopiere(s["input"]), kopiere(s["output"])
    # Ruhepose des GLB-Skeletts (Knoten-TRS) = Stehen Bild 0 (--ruhepose=<npz>): auch ohne Clip steht die saubere Pose, Kanaele nur gegen diese Ruhepose
    ruhe = None
    if "ruhepose" in OPT:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        import pferd2_gang as GG0
        sk0 = GG0.Skelett()
        d0 = np.load(OPT["ruhepose"])
        ruhe = (d0["R"][0], d0["T"][0], sk0.S, sk0.M)
    # Eigene Clips aus Bildfolgen (npz mit R (Bilder, Knoten, 3, 3), T (Bilder, Knoten, 3), fps): Drehung je Knoten, Verschiebung nur wo sie sich von der Ruhepose unterscheidet
    if "clipdateien" in OPT:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        import pferd2_gang as GG
        for eintrag in OPT["clipdateien"].split(";"):
            cname, pfad = eintrag.split("=", 1)
            dat = np.load(pfad)
            Rr, Tt, fps = dat["R"], dat["T"], float(dat["fps"])
            nf = Rr.shape[0]
            zeiten = (np.arange(nf) / fps).astype(np.float32)
            a_zeit = accessor_neu(zeiten, "SCALAR", 5126, None, True)
            samplers, channels = [], []
            for i in range(Rr.shape[1]):
                if ruhe is not None and ruhe[3][i] is not None:
                    continue                                          # Knoten mit Matrix: nicht animiert
                R_ref = ruhe[0][i] if ruhe is not None else Rr[0, i]
                T_ref = ruhe[1][i] if ruhe is not None else Tt[0, i]
                if np.abs(Rr[:, i] - R_ref[None]).max() > 1e-6:
                    q = np.array([GG.mat_quat(Rr[f, i]) for f in range(nf)])
                    for f in range(1, nf):                            # Vorzeichenstetigkeit
                        if np.dot(q[f], q[f - 1]) < 0:
                            q[f] = -q[f]
                    a_q = accessor_neu(q.astype(np.float32), "VEC4", 5126)
                    samplers.append({"input": a_zeit, "output": a_q, "interpolation": "LINEAR"})
                    channels.append({"sampler": len(samplers) - 1, "target": {"node": i, "path": "rotation"}})
                if np.abs(Tt[:, i] - T_ref[None]).max() > 1e-7:
                    a_t = accessor_neu(Tt[:, i].astype(np.float32), "VEC3", 5126)
                    samplers.append({"input": a_zeit, "output": a_t, "interpolation": "LINEAR"})
                    channels.append({"sampler": len(samplers) - 1, "target": {"node": i, "path": "translation"}})
            animationen.append({"name": cname, "samplers": samplers, "channels": channels})
    skin = json.loads(json.dumps(j0["skins"][0]))
    skin["inverseBindMatrices"] = kopiere(skin["inverseBindMatrices"])
    # 2. Netze
    meshes, matnamen = [], []
    prim_knoten = {}
    for i, p in enumerate(H.prims):
        mat = str(d[f"mat{i}"])
        pos = d[f"pos{i}"].astype(np.float32)
        idx = d[f"idx{i}"].astype(np.int64)
        uv = d[f"uv{i}"].astype(np.float32)
        jo = d[f"jo{i}"].astype(np.uint16)
        w = d[f"w{i}"].astype(np.float32)
        w = w / np.maximum(w.sum(1, keepdims=True), 1e-9)
        nrm = B.weiche_normalen(pos.astype(np.float64), idx).astype(np.float32)
        a_pos = accessor_neu(pos, "VEC3", 5126, 34962, True)
        a_nrm = accessor_neu(nrm, "VEC3", 5126, 34962)
        a_uv = accessor_neu(uv, "VEC2", 5126, 34962)
        a_jo = accessor_neu(jo, "VEC4", 5123, 34962)
        a_w = accessor_neu(w, "VEC4", 5126, 34962)
        a_idx = accessor_neu(idx.reshape(-1).astype(np.uint16 if len(pos) < 65535 else np.uint32), "SCALAR", 5123 if len(pos) < 65535 else 5125, 34963)
        if mat not in matnamen:
            matnamen.append(mat)
        meshes.append({"name": mat, "primitives": [{"attributes": {"POSITION": a_pos, "NORMAL": a_nrm, "TEXCOORD_0": a_uv, "JOINTS_0": a_jo, "WEIGHTS_0": a_w},
                                                    "indices": a_idx, "material": matnamen.index(mat), "mode": 4}]})
        prim_knoten[p["knoten"]] = len(meshes) - 1
    # 3. Texturen
    images, textures = [], []

    def bild(daten, mime):
        vi = pb.add(daten)
        images.append({"bufferView": vi, "mimeType": mime})
        textures.append({"sampler": 0, "source": len(images) - 1})
        return len(textures) - 1
    horse = Image.open(os.path.join(texdir, f"Horse_{variante}_2048.png")).convert("RGB")
    bio = io.BytesIO()
    horse.save(bio, "JPEG", quality=92)
    t_h = bild(bio.getvalue(), "image/jpeg")
    haar = Image.open(os.path.join(texdir, f"Hair_{variante}_1024.png")).convert("RGBA")
    bio = io.BytesIO()
    haar.save(bio, "PNG")
    t_hair = bild(bio.getvalue(), "image/png")
    shoe = Image.open("C:/Reichsgilde_Austausch/pferd3/textures/Horseshoe_baseColor.png").convert("RGB").resize((512, 512))
    bio = io.BytesIO()
    shoe.save(bio, "PNG")
    t_shoe = bild(bio.getvalue(), "image/png")
    mats = []
    for n in matnamen:
        if n == "Horse":
            mats.append({"name": n, "pbrMetallicRoughness": {"baseColorTexture": {"index": t_h}, "metallicFactor": 0.0, "roughnessFactor": 0.85}})
        elif n == "Hair":
            mats.append({"name": n, "alphaMode": "MASK", "alphaCutoff": 0.5, "doubleSided": True,
                         "pbrMetallicRoughness": {"baseColorTexture": {"index": t_hair}, "metallicFactor": 0.0, "roughnessFactor": 0.8}})
        elif n == "Horseshoe":
            mats.append({"name": n, "pbrMetallicRoughness": {"baseColorTexture": {"index": t_shoe}, "metallicFactor": 0.5, "roughnessFactor": 0.6}})
        else:
            m0 = next(m for m in j0["materials"] if m["name"] == n)
            mats.append({"name": n, "pbrMetallicRoughness": dict(m0["pbrMetallicRoughness"])})
    # 4. Knoten: Netzverweise neu, Sattel/Steigbuegel (Knoten mit Netz, die wir nicht uebernehmen) ohne Netz
    nodes = json.loads(json.dumps(j0["nodes"]))
    for i, nd in enumerate(nodes):
        if ruhe is not None and ruhe[3][i] is None:
            nd["rotation"] = [float(x) for x in GG0.mat_quat(ruhe[0][i])]
            nd["translation"] = [float(x) for x in ruhe[1][i]]
            nd["scale"] = [float(x) for x in ruhe[2][i]]
        if "mesh" in nd:
            if i in prim_knoten:
                nd["mesh"] = prim_knoten[i]
            else:
                del nd["mesh"]
                nd.pop("skin", None)
    j = {"asset": {"version": "2.0", "generator": "pferd2_export.py (Reichsgilde)", "copyright": "Basis: Horse by henrysteve973, CC BY 4.0"},
         "scene": j0.get("scene", 0), "scenes": j0["scenes"], "nodes": nodes, "skins": [skin], "meshes": meshes, "materials": mats, "textures": textures, "images": images,
         "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}], "animations": animationen, "accessors": acc,
         "bufferViews": pb.views, "buffers": [{"byteLength": len(pb.daten)}]}
    js = json.dumps(j, separators=(",", ":")).encode("utf8")
    js += b" " * ((4 - len(js) % 4) % 4)
    bn = bytes(pb.daten) + b"\0" * ((4 - len(pb.daten) % 4) % 4)
    glb = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bn)) + struct.pack("<II", len(js), 0x4E4F534A) + js + struct.pack("<II", len(bn), 0x004E4942) + bn
    os.makedirs(os.path.dirname(ausgabe), exist_ok=True)
    open(ausgabe, "wb").write(glb)
    print("GLB", ausgabe, len(glb) // 1024, "KiB", "Netze", [m["name"] for m in meshes], "Clips", len(animationen))


if __name__ == "__main__":
    main()


