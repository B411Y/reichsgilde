#!/usr/bin/env python3
"""TRELLIS.2 auf der zweiten ComfyUI (Port 8189): trellis.py <porträt.png> <ziel.glb>
Lädt das Bild hoch, startet den Workflow reichsgilde_3d_api, wartet und holt die GLB aus Knoten 37 (SaveGLB).
Regeln: nie zugleich mit der Bild-KI (8188) rechnen; vorher /free auf 8188. PAUSE-Datei beachten."""
import json, os, sys, time, urllib.parse, urllib.request, uuid
URL = os.environ.get("COMFY3D", "http://192.168.2.23:8189")
bild, ziel = sys.argv[1], sys.argv[2]
if os.path.exists(os.path.expanduser("~/werkzeuge/comfy/PAUSE")):
    print("PAUSE"); sys.exit(75)

def holen(pfad, daten=None, kopf=None, zeit=120):
    with urllib.request.urlopen(urllib.request.Request(URL + pfad, data=daten, headers=kopf or {}), timeout=zeit) as a:
        return a.read()

g = "----" + uuid.uuid4().hex
name = "kopf_" + os.path.basename(bild)
body = (f"--{g}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{name}\"\r\nContent-Type: application/octet-stream\r\n\r\n").encode() \
    + open(bild, "rb").read() + f"\r\n--{g}\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n--{g}--\r\n".encode()
hoch = json.loads(holen("/upload/image", body, {"Content-Type": f"multipart/form-data; boundary={g}"}))
wf = json.loads(holen("/api/userdata/workflows%2Freichsgilde_3d_api.json"))
wf["1"]["inputs"]["image"] = hoch["name"]
if os.environ.get("SEED"):
    for k in ("14", "19", "21", "24"):
        wf[k]["inputs"]["seed"] = int(os.environ["SEED"]) + int(k)
cid = uuid.uuid4().hex
r = json.loads(holen("/prompt", json.dumps({"prompt": wf, "client_id": cid}).encode(), {"Content-Type": "application/json"}))
pid = r["prompt_id"]
print("gestartet", pid, flush=True)
t0 = time.time()
while True:
    time.sleep(10)
    h = json.loads(holen("/history/" + pid))
    if pid in h:
        e = h[pid]
        if e["status"]["status_str"] != "success":
            print("FEHLER", json.dumps(e["status"])[:2000]); sys.exit(1)
        break
    if time.time() - t0 > 1500:
        print("ZEIT"); sys.exit(2)
out = e["outputs"]["37"]
print("ausgabe", json.dumps(out), flush=True)
dat = out.get("3d") or out.get("result") or out.get("images") or next(iter(out.values()))
d = dat[0] if isinstance(dat, list) else dat
if isinstance(d, str):
    d = {"filename": d, "subfolder": "", "type": "output"}
q = urllib.parse.urlencode({"filename": d["filename"], "subfolder": d.get("subfolder", ""), "type": d.get("type", "output")})
open(ziel, "wb").write(holen("/view?" + q, zeit=300))
print("fertig", ziel, os.path.getsize(ziel), "Bytes", round(time.time() - t0), "s")
