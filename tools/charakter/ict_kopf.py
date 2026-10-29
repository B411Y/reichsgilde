"""Charakter-Versuch Phase 0, Weg B: ICT-FaceKit-Kopf (MIT) mit ARKit-Formen nach .glb (eigener Code, MIT).

Aufruf:  tools/charakter/blender.sh -b -P tools/charakter/ict_kopf.py -- <ausgabe.glb> [identitaeten=0] [mimik=alle|keine]
         [morph_normalen=1] [masken=1]

Die Daten liegen nur unter ~/werkzeuge/charakter/ict/src/FaceXModel (nicht im Repository).
ICT-Formen heißen „eyeBlink_L“; sie werden in ARKit-Namen umbenannt („eyeBlinkLeft“), damit das Spiel für A und B
dieselben Namen benutzt. Identitäten (Hauptkomponenten aus Lichtbühnen-Scans) kommen optional als „id_000“ … dazu.
"""
import os
import sys
import time

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kopf_gemeinsam as kg  # noqa: E402

ORDNER = os.path.expanduser("~/werkzeuge/charakter/ict/src/FaceXModel")
arg = kg.argumente()
ausgabe = os.path.abspath(arg[0])
optionen = dict(a.split("=") for a in arg[1:])
identitaeten = int(optionen.get("identitaeten", 0))
mimik = optionen.get("mimik", "alle")
t0 = time.time()
MASS = 0.01  # ICT rechnet in Zentimetern

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)

bpy.ops.wm.obj_import(filepath=os.path.join(ORDNER, "generic_neutral_mesh.obj"))
kopf = bpy.context.selected_objects[0]
kopf.name = "Kopf"
bpy.ops.object.shade_smooth()  # OBJ ohne Glättungsgruppen: sonst teilt der glTF-Export jede Ecke je Fläche
kopf.scale = (MASS, MASS, MASS)
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
n = len(kopf.data.vertices)
assert n == 26719, f"Eckenzahl {n} statt 26719 – Reihenfolge der OBJ-Ecken nicht erhalten"


def obj_ecken(datei):
    """Nur die „v“-Zeilen einer OBJ lesen; Achsen wie Blenders Import (x, -z, y) und in Meter."""
    zeilen = [z for z in open(datei, encoding="utf-8") if z.startswith("v ")]
    v = np.array([z.split()[1:4] for z in zeilen], dtype=np.float32)
    return np.stack([v[:, 0], -v[:, 2], v[:, 1]], axis=1) * MASS


basis = obj_ecken(os.path.join(ORDNER, "generic_neutral_mesh.obj"))
# Gegenprobe: Blenders Import entspricht der eigenen Lesung
assert np.abs(kg.ecken_als_feld(kopf) - basis).max() < 1e-4
kopf.shape_key_add(name="Basis", from_mix=False)


def arkit_name(ict):
    if ict.endswith("_L"):
        return ict[:-2] + "Left"
    if ict.endswith("_R"):
        return ict[:-2] + "Right"
    return ict


formen = []
if mimik != "keine":
    for datei in sorted(os.listdir(ORDNER)):
        name = datei[:-4]
        if not datei.endswith(".obj") or name == "generic_neutral_mesh" or name.startswith("identity"):
            continue
        kg.schluessel_setzen(kopf, arkit_name(name), obj_ecken(os.path.join(ORDNER, datei)))
        formen.append(arkit_name(name))
for i in range(identitaeten):
    kg.schluessel_setzen(kopf, f"id_{i:03d}", obj_ecken(os.path.join(ORDNER, f"identity{i:03d}.obj")))
    formen.append(f"id_{i:03d}")

# Merkpunkte: Augapfelmitten aus den Eckbereichen, Nase/Mund/Kinn aus den 68 Landmarken (Multi-PIE-Reihenfolge)
import json  # noqa: E402
ind = json.load(open(os.path.join(ORDNER, "vertex_indices.json"), encoding="utf-8"))
lm = np.array(ind["idx_to_landmark_verts"])
auge_l = basis[21451:23021].mean(axis=0)
auge_r = basis[23021:24591].mean(axis=0)
if auge_l[0] < auge_r[0]:
    auge_l, auge_r = auge_r, auge_l  # „links“ = +x wie bei MPFB
augenabstand = float(np.linalg.norm(auge_l - auge_r))
nase = basis[lm[30]]
mund = basis[lm[48:68]].mean(axis=0)
mund[1] = basis[lm[48:68], 1].min()
kinn = basis[lm[8]]
haut = np.arange(0, 14062)
augen_z = (auge_l[2] + auge_r[2]) / 2.0
nahe = haut[(np.abs(basis[haut, 0]) < 0.12 * augenabstand) & (np.abs(basis[haut, 2] - augen_z - 0.75 * augenabstand) < 0.08 * augenabstand)]
stirn = basis[nahe[np.argmin(basis[nahe, 1])]]
punkte = {"auge_l": auge_l, "auge_r": auge_r, "nase": nase, "mund": mund, "stirn": stirn, "kinn": kinn}
# Ohren: äußerste Hautecken in Augenhöhe bis Mundhöhe
ohr = np.zeros(n, dtype=np.float32)
band = haut[(basis[haut, 2] < augen_z + 0.3 * augenabstand) & (basis[haut, 2] > mund[2])]
for seite in (1.0, -1.0):
    rand = band[np.argmax(seite * basis[band, 0])]
    d = ((basis - basis[rand]) ** 2).sum(axis=1)
    ohr = np.maximum(ohr, kg.gauss(d, 0.45 * augenabstand).astype(np.float32))
if optionen.get("masken", "1") == "1":
    kg.masken_setzen(kopf, punkte, augenabstand, ohren=ohr)

# Augen: ICT hat zwei Kugeln je Auge (außen „Sclera“ mit Hornhaut, innen „Iris“ mit Iris und Lederhaut, Art der
# Unreal-Digital-Humans). Eckfarbe R = Abstand von der Blickachse: innen R = r / (2 · Irisradius) vorn, sonst 1;
# außen R = 1 im Bereich der Hornhaut (dort durchsichtig). So zeichnet der Augenshader ohne Textur.
R_IRIS = 0.00585  # 11,7 mm Irisdurchmesser
attr = kopf.data.color_attributes.get("Hautmaske")
if attr is not None:
    farbe = np.empty(n * 4, dtype=np.float32)
    attr.data.foreach_get("color", farbe)
    farbe = farbe.reshape(n, 4)
    for lo_s, hi_s, hi_i in ((21451, 22221, 23021), (23021, 23791, 24591)):
        mitte = basis[lo_s:hi_s].mean(axis=0)
        iris = basis[hi_s:hi_i]
        achse = iris.mean(axis=0) - mitte
        achse /= np.linalg.norm(achse)

        def radial(p):
            d = p - iris.mean(axis=0)
            return np.linalg.norm(d - np.outer(d @ achse, achse), axis=1)
        vorn_i = ((iris - mitte) @ achse) > 0
        farbe[hi_s:hi_i, 0] = np.where(vorn_i, np.clip(radial(iris) / (2.0 * R_IRIS), 0.0, 1.0), 1.0)
        # Winkel um die Blickachse als (cos, sin) in G und B, damit die Irisfasern ohne Naht laufen
        e1 = np.cross(achse, [0.0, 0.0, 1.0])
        e1 /= np.linalg.norm(e1)
        e2 = np.cross(achse, e1)
        d = iris - iris.mean(axis=0)
        w = np.arctan2(d @ e2, d @ e1)
        farbe[hi_s:hi_i, 1] = 0.5 + 0.5 * np.cos(w)
        farbe[hi_s:hi_i, 2] = 0.5 + 0.5 * np.sin(w)
        sk = basis[lo_s:hi_s]
        vorn = ((sk - mitte) @ achse) > 0
        farbe[lo_s:hi_s, 0] = np.where(vorn & (radial(sk) < R_IRIS * 1.08), 1.0, 0.0)
        farbe[lo_s:hi_s, 1:3] = 0.0
    attr.data.foreach_set("color", farbe.ravel())

kg.gltf_export([kopf], ausgabe, morph_normalen=optionen.get("morph_normalen", "1") == "1",
               eckfarben=optionen.get("masken", "1") == "1")
bericht = {"ecken": n, "dreiecke": sum(len(p.vertices) - 2 for p in kopf.data.polygons), "formen": len(formen),
           "stoffe": [m.name for m in kopf.data.materials], "augenabstand_m": round(augenabstand, 4),
           "sekunden": round(time.time() - t0, 1), "glb_mb": round(os.path.getsize(ausgabe) / 1e6, 2)}
json.dump(bericht, open(ausgabe[:-4] + ".json", "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print("FERTIG", json.dumps(bericht, ensure_ascii=False))
