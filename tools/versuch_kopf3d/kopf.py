# Person aus DNA bauen und als Brustbild rendern: normal (Eevee) und/oder Holzschnitt.
# ~/werkzeuge/mpfb/blender_mpfb.sh -b --python kopf.py -- --dna=<json> --aus=<ordner/name> [--nur=holz|normal] [--groesse=1024]
import sys, os, json, bpy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mensch as M
import holzschnitt as H

a = M.argumente()
dna = json.load(open(a["dna"]))
groesse = int(a.get("groesse", 1024))
aus = a["aus"]
M.szene_leeren()
b = M.mensch_bauen(dna)
kam = dna.get("kamera", {})
cam, ziel = M.brustbild_kamera(b, drehung=kam.get("drehung", -35), abstand=kam.get("abstand", 0.95),
                               hoehe_versatz=kam.get("hoehe", 0.03), seitlich=kam.get("seitlich", 0.0))
M.licht_portraet(ziel)
M.render_einstellen(groesse, groesse)
if a.get("blend"):
    bpy.ops.wm.save_as_mainfile(filepath=a["blend"])
if a.get("nur") != "holz":
    M.rendern(aus + "_normal.png")
if a.get("nur") != "normal":
    objs = M.objekte(b)
    H.AUGEN_TEXTUR["datei"] = os.path.join(M.NUTZERDATEN, "eyes", "materials", dna.get("augen_textur", "brown") + "_eye.png")
    farben = dict(H.FARBEN)
    farben.update({k: tuple(v) for k, v in dna.get("holzfarben", {}).items()})
    alt = H.durchgang_linien(objs, aus + "_linien.png", (groesse, groesse))
    H.durchgang_farbe(objs, aus + "_farbe.png", farben, alt)
    H.zusammensetzen(aus + "_linien.png", aus + "_farbe.png", aus + "_holz.png",
                     versatz=(int(groesse * 0.005), -int(groesse * 0.004)))
