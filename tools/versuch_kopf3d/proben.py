# Proben: dieselbe Person mit verschiedenen Frisuren oder Kleidern (--art=clothes) nacheinander rendern.
import sys, os, json, bpy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mensch as M
a = M.argumente()
dna = json.load(open(a["dna"]))
M.szene_leeren()
haare = a["haare"].split(",")
art = a.get("art", "hair")
if art == "hair":
    dna["haar"] = None
else:
    dna["kleidung"] = []
b = M.mensch_bauen(dna)
cam, ziel = M.brustbild_kamera(b, drehung=float(a.get("drehung", -35)))
M.licht_portraet(ziel)
M.render_einstellen(512, 512)
for h in haare:
    vorher = set(bpy.data.objects)
    if h.startswith("he:"):
        M.haareditor_anlegen(b, h[3:])
    else:
        M.HumanService.add_mhclo_asset(M._asset(art, h), b, asset_type="Hair" if art == "hair" else "Clothes", subdiv_levels=1, material_type="MAKESKIN")
    M.rendern(os.path.join(a["aus"], f"{art}_{h.replace(':','_')}.png"))
    for o in set(bpy.data.objects) - vorher:
        bpy.data.objects.remove(o, do_unlink=True)
