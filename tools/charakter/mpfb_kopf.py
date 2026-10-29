"""Charakter-Versuch Phase 0: MPFB-2-Kopf als Büste nach .glb (eigener Code, MIT; MPFB nur als Werkzeug).

Aufruf:  tools/charakter/blender.sh -b -P tools/charakter/mpfb_kopf.py -- <angabe.json> <ausgabe.glb>

MPFB (GPL) wird nur lokal in Blender aufgerufen; ins Spiel kommt nur die Ausgabe (CC0-Assets + eigene Eingaben,
siehe MPFB LICENSE.md Abschnitt D). Angabe (JSON):
  makros          gender, age_jahre, muscle, weight, proportions, height, race {caucasian, african, asian}
  ziele           {"head/head-scale-horiz-incr": 0.4, ...} Formziele, fest eingebacken (Augen und Zähne passen sich an)
  haut            Name der .mhmat (z. B. "middleage_caucasian_male")
  augen           "high-poly" | "low-poly" (Augenfarbe: Standard der .mhclo, braun)
  zubehoer        Augenbrauen, Wimpern (Namen der .mhclo)
  mimik           "arkit" (52) | Liste von ARKit-Namen | "alle" (ARKit + 15 Meta- + 22 MS-Viseme)
  laufzeit        {"kopf_breit": {"head/head-scale-horiz-incr": 1.0}, ...} Formziele als Blendshapes für das Spiel
  uebertragen     true: Formschlüssel auf Augen, Zähne, Zunge, Brauen, Wimpern übertragen; false: nur Körper;
                  "nur_mimik": Mimik übertragen, Laufzeitformen („form_…“) nur am Körper (Test 3)
  hauer           {"laenge": 0.03, "dicke": 0.007} einfache Hauer an den unteren Eckzähnen (folgen dem Kiefer)
  kopf_uv         0 | 2048: Kopf aus dem Hautatlas ausschneiden, eigene UV und eigenes Bild in dieser Größe
  masken          true: Eckfarbe „Hautmaske“ (Rötung, Glanz, Vertiefung) für den Hautshader
  morph_normalen  true: Normalen der Formen mit exportieren (Datei größer, Licht bei Mimik richtig)
"""
import importlib
import json
import os
import sys
import time

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kopf_gemeinsam as kg  # noqa: E402


def holen(paket, name):
    """MPFB liegt als Erweiterung unter einem unbekannten Paketnamen; das passende Modul suchen."""
    for modul in list(sys.modules):
        if modul.endswith(paket):
            return getattr(importlib.import_module(modul), name)
    raise ValueError("MPFB-Modul fehlt: " + paket)


HumanService = holen("mpfb.services.humanservice", "HumanService")
TargetService = holen("mpfb.services.targetservice", "TargetService")
AssetService = holen("mpfb.services.assetservice", "AssetService")
LocationService = holen("mpfb.services.locationservice", "LocationService")
Mhclo = holen("mpfb.entities.clothes.mhclo", "Mhclo")
ARKIT = holen("mpfb.services.faceservice", "ARKIT_FACEUNITS")
META = holen("mpfb.services.faceservice", "META_VISEMES")
MS = holen("mpfb.services.faceservice", "MICROSOFT_VISEMES")

arg = kg.argumente()
angabe = json.load(open(arg[0], encoding="utf-8"))
ausgabe = os.path.abspath(arg[1])
os.makedirs(os.path.dirname(ausgabe), exist_ok=True)
t0 = time.time()
bericht = {"angabe": os.path.basename(arg[0])}

# Leere Szene
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)

# 1. Mensch mit Makros
makros = TargetService.get_default_macro_info_dict()
for k, v in angabe.get("makros", {}).items():
    if k == "age_jahre":
        makros["age"] = kg.alter_zu_makro(v)
    elif k == "race":
        makros["race"] = v
    else:
        makros[k] = v
koerper = HumanService.create_human(macro_detail_dict=makros)
koerper.name = "Koerper"
bericht["makro_alter"] = makros["age"]

# 2. Feste Formziele
ziele = LocationService.get_mpfb_data("targets")
for name, w in angabe.get("ziele", {}).items():
    TargetService.load_target(koerper, os.path.join(ziele, name + ".target.gz"), weight=float(w))

# 3. Haut und Teile (passen sich beim Laden an die jetzige Form an)
haut = AssetService.find_asset_absolute_path(angabe.get("haut", "middleage_caucasian_male") + ".mhmat",
                                             asset_subdir="skins")
HumanService.set_character_skin(haut, koerper, skin_type="GAMEENGINE")
teile = {}
liste = [("eyes", angabe.get("augen", "high-poly"), "Eyes", "Augen"),
         ("teeth", "teeth_base", "Teeth", "Zaehne"),
         ("tongue", "tongue01", "Tongue", "Zunge")]
for z in angabe.get("zubehoer", ["eyebrow001", "eyelashes01"]):
    liste.append(("eyebrows" if z.startswith("eyebrow") else "eyelashes", z,
                  "Eyebrows" if z.startswith("eyebrow") else "Eyelashes",
                  "Brauen" if z.startswith("eyebrow") else "Wimpern"))
for ordner, datei, typ, name in liste:
    pfad = AssetService.find_asset_absolute_path(datei + ".mhclo", asset_subdir=ordner)
    if pfad is None:
        print("FEHLT:", datei)
        continue
    obj = HumanService.add_mhclo_asset(pfad, koerper, asset_type=typ, material_type="GAMEENGINE", subdiv_levels=0,
                                       set_up_rigging=False)
    obj.name = name
    teile[name] = (obj, pfad)
# 4. Formziele einbacken (Teile sind schon angepasst)
TargetService.bake_targets(koerper)
for obj, _ in teile.values():
    for m in list(obj.modifiers):
        obj.modifiers.remove(m)

# 5. Mimik und Laufzeitformen als Formschlüssel (Gewicht 0); „Basis“ immer anlegen (auch ganz ohne Formen)
koerper.shape_key_add(name="Basis", from_mix=False)
mimik = angabe.get("mimik", "arkit")
if mimik == "arkit":
    namen = list(ARKIT)
elif mimik == "alle":
    namen = list(ARKIT) + list(META) + list(MS)
else:
    namen = list(mimik)
faceunits = os.path.join(angabe.get("daten", os.path.expanduser("~/werkzeuge/charakter/mpfb_daten")), "data", "targets")
for n in namen:
    unter = "visemes" if n in META or n in MS else "faceunits"
    pfad = os.path.join(faceunits, unter, n + ".target")
    if not os.path.exists(pfad):
        pfad += ".gz"
    TargetService.load_target(koerper, pfad, weight=0.0, name=n)
for form, bestandteile in angabe.get("laufzeit", {}).items():
    # Mehrere Ziele zu einer Form zusammenfassen: nacheinander laden, mischen, als eine Form ablegen
    basis = kg.ecken_als_feld(koerper, "Basis")
    summe = np.zeros_like(basis)
    for ziel, w in bestandteile.items():
        TargetService.load_target(koerper, os.path.join(ziele, ziel + ".target.gz"), weight=0.0, name="_tmp")
        summe += (kg.ecken_als_feld(koerper, "_tmp") - basis) * float(w)
        koerper.shape_key_remove(koerper.data.shape_keys.key_blocks["_tmp"])
    kg.schluessel_setzen(koerper, "form_" + form, basis + summe)
# Namen der MPFB-Schlüssel vereinfachen (ohne Vorsilben)
for kb in koerper.data.shape_keys.key_blocks:
    for vor in ("!ex-", "$md-", "$ex-"):
        if kb.name.startswith(vor):
            kb.name = kb.name[len(vor):]
alle_formen = [kb.name for kb in koerper.data.shape_keys.key_blocks if kb.name != "Basis"]
bericht["formen"] = len(alle_formen)

# 6. Übertragung auf die Teile (vor dem Zuschnitt, solange die Ecknummern der Zuordnung gelten)
koerper_ecken = kg.ecken_als_feld(koerper, "Basis")
uebertragen = angabe.get("uebertragen", True)
bericht["teile"] = {}
for name, (obj, pfad) in teile.items():
    if not uebertragen:
        bericht["teile"][name] = 0
        continue
    zu_uebertragen = alle_formen if uebertragen is True else [f for f in alle_formen if not f.startswith("form_")]
    m = Mhclo()
    m.load(pfad)
    n = len(obj.data.vertices)
    idx = np.zeros((n, 3), dtype=np.int64)
    gew = np.zeros((n, 3), dtype=np.float32)
    for i in range(n):
        eintrag = m.verts[i]
        idx[i] = eintrag["verts"]
        gew[i] = eintrag["weights"]
    bericht["teile"][name] = len(kg.formen_uebertragen(koerper, obj, (idx, gew), zu_uebertragen))


def gruppe_ecken(obj, gruppe):
    gi = obj.vertex_groups[gruppe].index
    return np.array([v.index for v in obj.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)])


def mitte(gruppe):
    return koerper_ecken[gruppe_ecken(koerper, gruppe)].mean(axis=0)


auge_l, auge_r = mitte("joint-l-eye"), mitte("joint-r-eye")
hals, kopf, mund_g = mitte("joint-neck"), mitte("joint-head"), mitte("joint-mouth")
schluessel_bein = (mitte("joint-l-clavicle") + mitte("joint-r-clavicle")) / 2.0
augenabstand = float(np.linalg.norm(auge_l - auge_r))
bericht["augenabstand_m"] = round(augenabstand, 4)

# 7. Hauer (einfache Kegel an den unteren Eckzähnen; folgen den Formen der Unterkiefer-Hilfsecken)
if "hauer" in angabe:
    h = angabe["hauer"]
    unten = koerper_ecken[gruppe_ecken(koerper, "helper-lower-teeth")]
    vorn_y = unten[:, 1].min()
    breite = (unten[:, 0].max() - unten[:, 0].min()) / 2.0
    for seite, sx in (("L", 1.0), ("R", -1.0)):
        # Fuß vorn an der unteren Zahnreihe (Eckzahn), Spitze nach oben, vorn (−y) und etwas nach außen
        fuss = np.array([sx * breite * 0.6, vorn_y + breite * 0.15, unten[:, 2].max() - breite * 0.05])
        laenge = h.get("laenge", 0.03)
        bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=h.get("dicke", 0.007), radius2=0.0008,
                                        depth=laenge, location=(0, 0, 0))
        zahn = bpy.context.active_object
        zahn.name = "Hauer_" + seite
        zahn.rotation_euler = (0.35, sx * 0.2, 0.0)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        richtung = np.array([np.sin(sx * 0.2), -np.sin(0.35), np.cos(0.35)])
        richtung /= np.linalg.norm(richtung)
        zahn.location = tuple(fuss + richtung * laenge * 0.5)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bpy.ops.object.shade_smooth()
        stoff = bpy.data.materials.new("Hauer")
        stoff.use_nodes = True
        stoff.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.78, 0.72, 0.58, 1)
        stoff.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.45
        zahn.data.materials.append(stoff)
        zuo = kg.naechste_zuordnung(unten, kg.ecken_als_feld(zahn), k=4)
        idx = gruppe_ecken(koerper, "helper-lower-teeth")[zuo[0]]
        kg.formen_uebertragen(koerper, zahn, (idx, zuo[1]), alle_formen)
        teile[zahn.name] = (zahn, None)

# 8. Zuschnitt: Hilfsgeometrie weg, Büste bis zur oberen Brust
koerper_menge = set(gruppe_ecken(koerper, "body").tolist())
for m in list(koerper.modifiers):
    koerper.modifiers.remove(m)
schnitt_z = schluessel_bein[2] - 0.5 * ((auge_l[2] + auge_r[2]) / 2.0 - schluessel_bein[2])
geloescht = kg.zuschneiden(koerper, lambda i, co: i in koerper_menge and co.z > schnitt_z)
bericht["schnitt_z"] = round(float(schnitt_z), 3)

# Merkpunkte auf der Gesichtsoberfläche (vorderste Ecke nahe der Mittellinie in passender Höhe)
ecken = kg.ecken_als_feld(koerper)


def vorn(z, breite=0.12):
    nahe = np.where((np.abs(ecken[:, 0]) < breite * augenabstand) & (np.abs(ecken[:, 2] - z) < 0.08 * augenabstand))[0]
    return ecken[nahe[np.argmin(ecken[nahe, 1])]]


augen_z = (auge_l[2] + auge_r[2]) / 2.0
nase = ecken[np.argmin(np.where((np.abs(ecken[:, 0]) < 0.2 * augenabstand) & (ecken[:, 2] < augen_z)
                                 & (ecken[:, 2] > mund_g[2]), ecken[:, 1], 9.0))]
punkte = {"auge_l": auge_l, "auge_r": auge_r, "nase": nase, "mund": vorn(mund_g[2]),
          "stirn": vorn(augen_z + 0.75 * augenabstand), "kinn": vorn(mund_g[2] - 0.6 * augenabstand)}
bericht["merkpunkte"] = {k: [round(float(x), 4) for x in v] for k, v in punkte.items()}

# 9. Hautmasken
if angabe.get("masken", True):
    def gruppe_gewicht(gruppe):
        w = np.zeros(len(koerper.data.vertices), dtype=np.float32)
        if gruppe in koerper.vertex_groups:
            gi = koerper.vertex_groups[gruppe].index
            for v in koerper.data.vertices:
                for g in v.groups:
                    if g.group == gi:
                        w[v.index] = g.weight
        return w
    kg.masken_setzen(koerper, punkte, augenabstand, ohren=gruppe_gewicht("ears"), lippen=gruppe_gewicht("lips"))

# 10. Kopf-UV: Kopf aus dem Hautatlas schneiden und auf ein eigenes Bild (kopf_uv × kopf_uv) legen
groesse = int(angabe.get("kopf_uv", 0))
if groesse:
    uv = koerper.data.uv_layers.active.data
    oben_flaechen = [p for p in koerper.data.polygons
                     if min(koerper.data.vertices[i].co.z for i in p.vertices) > hals[2] - 0.02]
    # Größte zusammenhängende UV-Insel der Kopfflächen (Gesicht); Ohren, Mundinneres usw. bleiben im Atlas
    eltern = {p.index: p.index for p in oben_flaechen}

    def wurzel(i):
        while eltern[i] != i:
            eltern[i] = eltern[eltern[i]]
            i = eltern[i]
        return i
    kanten = {}
    for p in oben_flaechen:
        vs, ls = list(p.vertices), list(p.loop_indices)
        for k in range(len(vs)):
            a, b = vs[k], vs[(k + 1) % len(vs)]
            ua = tuple(round(x, 5) for x in uv[ls[k]].uv)
            ub = tuple(round(x, 5) for x in uv[ls[(k + 1) % len(vs)]].uv)
            schl = (min(a, b), max(a, b), frozenset([(a, ua), (b, ub)]))
            if schl in kanten:
                eltern[wurzel(p.index)] = wurzel(kanten[schl])
            else:
                kanten[schl] = p.index
    inseln = {}
    for p in oben_flaechen:
        inseln.setdefault(wurzel(p.index), []).append(p)
    kopf_flaechen = max(inseln.values(), key=len)
    bericht["kopf_insel_flaechen"] = [len(kopf_flaechen), len(oben_flaechen)]
    schleifen = [li for p in kopf_flaechen for li in p.loop_indices]
    us = np.array([uv[li].uv[0] for li in schleifen])
    vs = np.array([uv[li].uv[1] for li in schleifen])
    u0, u1, v0, v1 = us.min(), us.max(), vs.min(), vs.max()
    stoff = koerper.data.materials[0]
    bild = next(n.image for n in stoff.node_tree.nodes if n.type == "TEX_IMAGE" and n.image)
    bw, bh = bild.size
    px = np.array(bild.pixels[:], dtype=np.float32).reshape(bh, bw, 4)
    x0, x1 = int(np.floor(u0 * bw)), int(np.ceil(u1 * bw))
    y0, y1 = int(np.floor(v0 * bh)), int(np.ceil(v1 * bh))
    bericht["kopf_im_atlas_px"] = [x1 - x0, y1 - y0, bw, bh]
    aus = px[y0:y1, x0:x1].copy()
    neu = bpy.data.images.new("Kopfhaut", x1 - x0, y1 - y0, alpha=True)
    neu.pixels.foreach_set(aus.ravel())
    # Seitenverhältnis halten, längste Seite = kopf_uv
    faktor = groesse / max(x1 - x0, y1 - y0)
    nb, nh = int(round((x1 - x0) * faktor)), int(round((y1 - y0) * faktor))
    neu.scale(nb, nh)
    bericht["kopf_bild_px"] = [nb, nh]
    # leicht nachschärfen (Unschärfemaske 3×3), damit das Hochskalieren nicht weicher wirkt als das Original
    p2 = np.array(neu.pixels[:], dtype=np.float32).reshape(nh, nb, 4)
    weich = (np.roll(p2, 1, 0) + np.roll(p2, -1, 0) + np.roll(p2, 1, 1) + np.roll(p2, -1, 1) + p2) / 5.0
    p2[..., :3] = np.clip(p2[..., :3] + 0.6 * (p2[..., :3] - weich[..., :3]), 0.0, 1.0)
    neu.pixels.foreach_set(p2.ravel())
    neu.filepath_raw = os.path.join(os.path.dirname(ausgabe), "kopfhaut_tmp.png")
    neu.file_format = "PNG"
    neu.save()
    neu.pack()
    kopf_stoff = stoff.copy()
    kopf_stoff.name = "Kopfhaut"
    for n in kopf_stoff.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image == bild:
            n.image = neu
    koerper.data.materials.append(kopf_stoff)
    nr = len(koerper.data.materials) - 1
    for p in kopf_flaechen:
        p.material_index = nr
        for li in p.loop_indices:
            u, v = uv[li].uv
            uv[li].uv = ((u * bw - x0) / (x1 - x0), (v * bh - y0) / (y1 - y0))
    os.remove(neu.filepath_raw)

# 11. Export
objekte = [koerper] + [o for o, _ in teile.values()]
for o in objekte:
    for m in list(o.modifiers):
        o.modifiers.remove(m)
kg.gltf_export(objekte, ausgabe, morph_normalen=angabe.get("morph_normalen", True),
               eckfarben=angabe.get("masken", True))
bericht["ecken"] = {o.name: len(o.data.vertices) for o in objekte}
bericht["dreiecke"] = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objekte}
bericht["stoffe"] = {o.name: [m.name for m in o.data.materials if m] for o in objekte}
bericht["sekunden"] = round(time.time() - t0, 1)
bericht["glb_mb"] = round(os.path.getsize(ausgabe) / 1e6, 2)
json.dump(bericht, open(ausgabe[:-4] + ".json", "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print("FERTIG", json.dumps(bericht, ensure_ascii=False))
