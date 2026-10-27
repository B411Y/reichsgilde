# Versuch HG-Köpfe, Stufe A: Mensch mit Human Generator 3D erzeugen, Haar zu Haarkarten.
# Aufruf: blender_hg.sh -b -P hg_erzeugen.py -- <einstellungen.json> <ziel.blend>
# Gebacken wird erst in Stufe B nach dem Zuschnitt (Texturen nur für das Brustbild).
# Die Einstellungen nennen Vorlage, Frisur, Bart, Alter und einzelne Regler (Livekeys).
# Human Generator (Personal-Lizenz) liegt nur auf dem Rechner, nie im Repository.
import bpy, sys, json, os, time
from HumGen3D import Human

args = sys.argv[sys.argv.index("--") + 1:]
einst = json.load(open(args[0]))
ziel_blend = args[1]
ctx = bpy.context
t0 = time.time()

def log(*a):
    print("HG-ERZ", f"{time.time()-t0:6.1f}s", *a, flush=True)

h = Human.from_preset(einst["vorlage"], context=ctx)
log("Vorlage", einst["vorlage"])

# Regler: Werte aus der Vorlage überschreiben (Namen wie in den Livekey-Dateien)
if einst.get("regler"):
    for name, wert in einst["regler"].items():
        h.keys[name].set_without_update(wert)
    h.keys.update_human_from_key_change(ctx)
if "alter" in einst:
    h.age.set(einst["alter"])
if "haut" in einst:
    h.skin.texture.set(einst["haut"])
log("Regler gesetzt")

if einst.get("frisur"):
    h.hair.regular_hair.set(einst["frisur"], context=ctx)
if einst.get("bart"):
    h.hair.face_hair.set(einst["bart"], context=ctx)
for teil, farbe in einst.get("haarfarbe", {}).items():
    # teil: regular_hair/face_hair/eyebrows; farbe: {lightness, redness, …} wie die HG-Haarregler
    for name, wert in farbe.items():
        getattr(getattr(h.hair, teil), name).value = wert
log("Haar gesetzt")

# Gesichtsrig mit den FACS-Formen (jawOpen, mouthFunnel …, Zunge an den Zähnen)
h.expression.load_facial_rig(context=ctx)
log("Gesichtsrig geladen")

# Haarkarten (Kopfhaar, Brauen, Wimpern, Bart)
q = einst.get("haarkarten", "high")
if h.hair.regular_hair.modifiers:
    h.hair.regular_hair.convert_to_haircards(q, ctx)
h.hair.eyebrows.convert_to_haircards(q, ctx)
h.hair.eyelashes.convert_to_haircards(q, ctx)
if h.gender == "male" and h.hair.face_hair.modifiers:
    # HG macht aus Barthaar sonst nur eine Kappe (Textur auf der Haut); als „Scalp“ entstehen echte Haarkarten
    fh = type(h.hair.face_hair)
    alt_typ = fh._haircap_type
    fh._haircap_type = "Scalp"
    try:
        h.hair.face_hair.convert_to_haircards(einst.get("bart_karten", q), ctx)
    finally:
        fh._haircap_type = alt_typ
h.objects.rig["haircards"] = True
log("Haarkarten", [(o.name, len(o.data.polygons)) for o in h.objects.haircards])

bpy.ops.wm.save_as_mainfile(filepath=ziel_blend)
log("gespeichert", ziel_blend)
