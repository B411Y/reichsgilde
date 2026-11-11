"""Vergleicht zwei Handschuh-.glb: alles aus <alt> (Netz, Gewichte, Gerueste, Knoten/Anker, alle Aktionen) muss in <neu> bitgleich vorkommen.
python -I glb_vergleich.py <alt.glb> <neu.glb>   (Ausgabe: Zahl der Abweichungen, Namen der zusaetzlichen Aktionen/Knoten)"""
import sys, json, struct
def laden(p):
    b = open(p, "rb").read(); n = struct.unpack("<I", b[12:16])[0]; j = json.loads(b[20:20 + n]); o = 20 + n
    m = struct.unpack("<I", b[o:o + 4])[0]; return j, b[o + 8:o + 8 + m]
def acc(j, bin_, i):
    a = j["accessors"][i]; v = j["bufferViews"][a["bufferView"]]; s = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]] * {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}[a["componentType"]] * a["count"]
    return bin_[s:s + n]
def zusammen(p):
    j, b = laden(p); d = {}; nm = lambda i: j["nodes"][i].get("name", str(i))
    for i, n in enumerate(j["nodes"]):
        d["knoten:" + nm(i)] = json.dumps({k: n[k] for k in ("translation", "rotation", "scale") if k in n}) + "|" + ",".join(sorted(nm(c) for c in n.get("children", [])))
    for me in j["meshes"]:
        for k, pr in enumerate(me["primitives"]):
            for at, ai in pr["attributes"].items(): d[f"netz:{me['name']}:{k}:{at}"] = acc(j, b, ai)
            if "indices" in pr: d[f"netz:{me['name']}:{k}:idx"] = acc(j, b, pr["indices"])
    for s in j.get("skins", []):
        d["skin:" + s.get("name", "")] = ",".join(nm(x) for x in s["joints"]); 
        if "inverseBindMatrices" in s: d["skin:ibm"] = acc(j, b, s["inverseBindMatrices"])
    for a in j.get("animations", []):
        for c in a["channels"]:
            sm = a["samplers"][c["sampler"]]; k = f"aktion:{a['name']}:{nm(c['target']['node'])}:{c['target']['path']}"
            d[k] = acc(j, b, sm["input"]) + b"|" + acc(j, b, sm["output"])
    return d, [a["name"] for a in j.get("animations", [])], len(j["skins"][0]["joints"])
A, na, ja = zusammen(sys.argv[1]); N, nn, jn = zusammen(sys.argv[2])
fehl = [k for k, v in A.items() if k not in N or N[k] != v]
neu = sorted({k.split(":")[1] for k in N if k not in A and k.startswith("aktion:")}); nk = [k for k in N if k not in A and k.startswith("knoten:")]
print("alt", len(na), "Aktionen", ja, "Knochen | neu", len(nn), "Aktionen", jn, "Knochen")
print("ABWEICHUNGEN", len(fehl), fehl[:10]); print("NEUE Aktionen", neu); print("NEUE Knoten", nk)
