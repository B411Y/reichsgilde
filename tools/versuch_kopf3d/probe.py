# Erste Probe: DNA laden, Person bauen, Brustbild normal rendern.
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mensch as M
a = M.argumente()
dna = json.load(open(a["dna"]))
M.szene_leeren()
b = M.mensch_bauen(dna)
cam, ziel = M.brustbild_kamera(b, drehung=float(a.get("drehung", -28)))
M.licht_portraet(ziel)
M.render_einstellen(int(a.get("groesse", 768)), int(a.get("groesse", 768)))
M.rendern(a["aus"])
if a.get("blend"):
    import bpy; bpy.ops.wm.save_as_mainfile(filepath=a["blend"])
