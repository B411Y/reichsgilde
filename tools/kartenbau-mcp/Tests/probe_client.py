import subprocess, json, sys
p = subprocess.Popen(["dotnet", sys.argv[1]], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, cwd=sys.argv[2])
def send(m): p.stdin.write(json.dumps(m)+"\n"); p.stdin.flush()
def rpc(i, method, params=None):
    send({"jsonrpc":"2.0","id":i,"method":method,"params":params or {}})
    while True:
        r = json.loads(p.stdout.readline())
        if r.get("id")==i: return r
r = rpc(1,"initialize",{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"t","version":"1"}})
print("server:", r["result"]["serverInfo"])
send({"jsonrpc":"2.0","method":"notifications/initialized"})
r = rpc(2,"tools/list"); print("tools:", [t["name"] for t in r["result"]["tools"]])
for i,(n,a) in enumerate([("karten_auflisten",{}),("karte_lesen",{"karte":"greybox"}),("karte_pruefen",{"karte":"greybox"}),("karte_bauen",{"skript":"probestadt"}),("karte_bild",{"karte":"greybox"}),("karte_lesen",{"karte":"../../etc/passwd"})],3):
    r = rpc(i,"tools/call",{"name":n,"arguments":a})
    print("==",n,a)
    for c in r["result"]["content"]: print((c.get("text") or "<"+c["type"]+">")[:600])
p.stdin.close()
