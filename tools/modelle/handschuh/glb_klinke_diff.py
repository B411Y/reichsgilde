"""Vergleicht die Klinken-Clips zweier .glb Kanal fuer Kanal (Drehungen als Winkel zwischen den Quaternionen in Grad, Verschiebungen in mm).
python -I glb_klinke_diff.py <a.glb> <b.glb> [Clip ...]   Ausgabe je Clip: groesste Abweichung Drehung / Verschiebung und Knochen."""
import sys, json, struct
import numpy as np
def laden(p):
    b = open(p, "rb").read(); n = struct.unpack("<I", b[12:16])[0]; j = json.loads(b[20:20 + n]); o = 20 + n
    m = struct.unpack("<I", b[o:o + 4])[0]; return j, b[o + 8:o + 8 + m]
def acc(j, bin_, i):
    a = j["accessors"][i]; v = j["bufferViews"][a["bufferView"]]; s = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    nk = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}[a["type"]]
    return np.frombuffer(bin_[s:s + 4 * nk * a["count"]], dtype="<f4").reshape(a["count"], nk)
def kanaele(p):
    j, b = laden(p); d = {}
    for a in j["animations"]:
        for c in a["channels"]:
            sm = a["samplers"][c["sampler"]]; d[(a["name"], j["nodes"][c["target"]["node"]]["name"], c["target"]["path"])] = (acc(j, b, sm["input"]), acc(j, b, sm["output"]))
    return d
A, B = kanaele(sys.argv[1]), kanaele(sys.argv[2]); clips = sys.argv[3:] or sorted({k[0] for k in A if k[0].startswith(("Klinke", "Uebergang_Bereit", "Uebergang_Ruhe_Bereit", "Uebergang_Zeigen_Bereit", "Uebergang_Text_Bereit"))})
for c in clips:
    rot = (0.0, ""); pos = (0.0, ""); fehlt = 0
    for k, (ta, va) in A.items():
        if k[0] != c: continue
        if k not in B: fehlt += 1; continue
        tb, vb = B[k]
        if va.shape != vb.shape: fehlt += 1; continue
        if k[2] == "rotation":
            w = np.degrees(2 * np.arccos(np.clip(np.abs((va * vb).sum(1)), 0, 1))).max()
            if w > rot[0]: rot = (float(w), k[1])
        elif k[2] == "translation":
            w = np.abs(va - vb).max() * 1000
            if w > pos[0]: pos = (float(w), k[1])
    print(f"{c}: Drehung max {rot[0]:.2f} Grad ({rot[1]}), Verschiebung max {pos[0]:.2f} mm ({pos[1]}), Kanaele ohne Gegenstueck {fehlt}")
