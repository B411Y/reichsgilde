"""Prüfung der Zugtier-Bewegungen an den fertigen GLB-Dateien (nicht am Bauskript): Huf-Kontakt, Schleifen, Übergänge, VAT-Abweichung.

    blender -b --python tools/modelle/zugtiere_pruefen.py -- --glb=tier_pferd_rotbraun [--aus=<ordner>] [--bilder] [--gif]

Liest game/assets/modelle/tiere/<glb>.glb (Import wie im Spiel) und game/daten/zugtiere.json (Wegverlauf je Bild), schreibt
<aus>/<glb>_messung.json (Zahlen) und mit --bilder Bildfolgen je Clip (Seite und Spielkamera) nach <aus>/<glb>/<clip>/…png.
Die Bilder setzt tools/modelle/zugtiere_bogen.py (normales Python mit Pillow) zu Kontaktbögen und GIFs zusammen.
Messungen:
  huf         je Huf und Clip: Bilder am Boden, tiefster und höchster Punkt der Sohle im Stand (mm), Rutschen im Stand (mm, Spanne
              von Y − X(t) über einen zusammenhängenden Stand, X = Weg des Rumpfes), Schwunghöhe (mm)
  tiefster    tiefster Punkt des ganzen Netzes im Clip (mm, negativ = im Boden)
  schleife    Abstand Bild 0 zu letztem Bild (größte Eckenverschiebung, mm), Sprung der Geschwindigkeit an der Naht (mm je Bild)
  uebergang   Abstand der Posen an den Übergängen (größte Eckenverschiebung, mm)
  vat         Abweichung der gebackenen Bewegung (12 Bilder je Sekunde, linear gemischt wie im Shader der Passanten) von der
              Skelettpose, größte und mittlere Eckenabweichung (mm)
"""

import json
import math
import os
import sys

import bpy
from mathutils import Vector

WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS)
VAT_BILDER = 12
FPS = 24
BEINE = {}
FUESSE = {"LH": "BackFoot.L", "LF": "FrontFoot.L", "RH": "BackFoot.R", "RF": "FrontFoot.R"}
UEBERGAENGE = [("Stehen", "Anfahren_Gehen"), ("Anfahren_Gehen", "Gehen"), ("Gehen", "Anhalten_Gehen"), ("Anhalten_Gehen", "Stehen"),
               ("Stehen", "Anfahren_Ziehen"), ("Anfahren_Ziehen", "Ziehen"), ("Ziehen", "Anhalten_Ziehen"), ("Anhalten_Ziehen", "Stehen"),
               ("Gehen", "Ziehen"), ("Ziehen", "Gehen"), ("Stehen", "Stehen_Ruhen"), ("Stehen_Ruhen", "Stehen"), ("Stehen", "Stehen_Umschauen"),
               ("Stehen_Umschauen", "Stehen"), ("Stehen", "Fressen"), ("Fressen", "Stehen")]


def laden(glb):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    bpy.ops.import_scene.gltf(filepath=os.path.join(WURZEL, "game", "assets", "modelle", "tiere", glb + ".glb"))
    ske = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    nah = next(o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Nah_"))
    return ske, nah


def aktion_setzen(ske, name):
    act = bpy.data.actions[name]
    ske.animation_data_create()
    ske.animation_data.action = act
    slots = getattr(act, "slots", None)
    if slots and len(slots):
        try:
            ske.animation_data.action_slot = slots[0]
        except (AttributeError, TypeError, RuntimeError):
            pass
    return act


def eckenlagen(ske, ob, t):
    """Weltlagen aller Ecken des Netzes zur Zeit t (Sekunden) im Clip."""
    szene = bpy.context.scene
    a = ske.animation_data.action
    x = a.frame_range[0] + t * FPS                                  # Bildnummer (Rundungsfehler nicht als ganzes Bild werten)
    ganz = int(math.floor(x + 1e-6))
    szene.frame_set(ganz, subframe=max(0.0, x - ganz))
    dg = bpy.context.evaluated_depsgraph_get()
    e = ob.evaluated_get(dg)
    me = e.to_mesh()
    m = ob.matrix_world
    lagen = [m @ v.co for v in me.vertices]
    e.to_mesh_clear()
    return lagen


def sohlen(ske, ob):
    """Je Huf die Ecken der Sohle (die tiefsten Ecken der Gruppe des Hufziels) und ihre Ruhehöhe."""
    ske.animation_data_create()
    ske.animation_data.action = None
    ske.data.pose_position = "REST"
    bpy.context.view_layer.update()
    ids = {g.name: g.index for g in ob.vertex_groups}
    aus = {}
    for kurz, bone in FUESSE.items():
        g = ids[bone]
        kand = [(v.co.z, v.index) for v in ob.data.vertices if any(x.group == g and x.weight > 0.5 for x in v.groups)]
        kand.sort()
        aus[kurz] = [i for _, i in kand[:6]]
    for kurz, bone in FUESSE.items():
        unten = ids[bone.replace("Foot", "LowLeg")]
        g = ids[bone]
        bein = sorted([(v.co.z, v.index) for v in ob.data.vertices if any(x.group == unten and x.weight > 0.5 for x in v.groups)])[:8]
        huf = sorted([(-v.co.z, v.index) for v in ob.data.vertices if any(x.group == g and x.weight > 0.5 for x in v.groups)])[:8]
        BEINE[kurz] = ([i for _, i in bein], [i for _, i in huf])
    ske.data.pose_position = "POSE"
    return aus


def gem(lagen, idx):
    v = Vector((0, 0, 0))
    for i in idx:
        v += lagen[i]
    return v / len(idx)


def clip_messen(ske, ob, name, meta, sohle):
    act = aktion_setzen(ske, name)
    n = int(round(act.frame_range[1] - act.frame_range[0]))
    dauer = n / FPS
    lagen = [eckenlagen(ske, ob, i / FPS) for i in range(n + 1)]
    erg = {"bilder": n + 1, "dauer": dauer}
    weg = meta["klips"][name].get("weg")
    erg["tiefster_mm"] = round(min(p.z for l in lagen for p in l) * 1000, 1)
    # Hufe
    huf = {}
    for kurz, idx in sohle.items():
        pts = [gem(l, idx) for l in lagen]
        z = [p.z for p in pts]
        hoehe0 = min(z)                                   # Sohle auf dem Boden im Clip
        stand = [zz - hoehe0 < 0.006 for zz in z]
        runs, k = [], 0
        while k < len(stand):
            if stand[k]:
                j = k
                while j + 1 < len(stand) and stand[j + 1]:
                    j += 1
                runs.append((k, j))
                k = j + 1
            else:
                k += 1
        rutsch = 0.0
        if weg:
            for a, b in runs:
                if b - a >= 2:
                    werte = [pts[i].y - weg[i] for i in range(a, b + 1)]
                    rutsch = max(rutsch, max(werte) - min(werte))
        zs = [z[i] for i in range(len(z)) if stand[i]]
        huf[kurz] = {"am_boden": sum(stand), "stand_z_min_mm": round(min(zs) * 1000, 1) if zs else None,
                     "stand_z_max_mm": round(max(zs) * 1000, 1) if zs else None,
                     "rutschen_mm": round(rutsch * 1000, 1) if weg else None, "hub_mm": round((max(z) - hoehe0) * 1000, 1),
                     "abhebungen": sum(1 for i in range(1, len(stand)) if stand[i - 1] and not stand[i])}
    erg["huf"] = huf
    # Lücke zwischen Bein und Huf: Abstand der Mitte der untersten Beinecken von der Mitte der obersten Hufecken, Abweichung von der Ruhepose (mm), größter Wert
    luecke = {}
    for kurz, (bein, hufoben) in BEINE.items():
        d0 = (gem(lagen[0], bein) - gem(lagen[0], hufoben)).length
        luecke[kurz] = round(max(abs((gem(l, bein) - gem(l, hufoben)).length - d0) for l in lagen) * 1000, 1)
    erg["huf_luecke_mm"] = luecke
    # Naht
    d0 = max((a - b).length for a, b in zip(lagen[0], lagen[-1]))
    erg["naht_mm"] = round(d0 * 1000, 2)
    # Naht: zweite Differenz an der Naht (Beschleunigung) gegen die größte zweite Differenz im Clip
    def beschl(i0, i1, i2):
        return max((lagen[i0][v] - 2 * lagen[i1][v] + lagen[i2][v]).length for v in range(0, len(lagen[0]), 5))
    naht = beschl(n - 1, 0, 1)
    sonst = max(beschl(i - 1, i, i + 1) for i in range(1, n))
    erg["naht_beschl_mm"] = round(naht * 1000, 2)
    erg["max_beschl_mm"] = round(sonst * 1000, 2)
    # VAT: gebackene Bilder (Rate je Sekunde), linear gemischt (Schleife schließt sich), geprüft in der Mitte zwischen den
    # Bildern des Clips (48 Abtastungen je Sekunde): Abweichung von der Skelettpose
    schl = meta["klips"][name]["schleife"]
    namen = {g.index: g.name for g in ob.vertex_groups}
    for rate in (12, 24):
        nb = max(1, int(round(dauer * rate)))
        bak = [eckenlagen(ske, ob, k * dauer / nb) for k in range(nb if schl else nb + 1)]
        groesst, summe, zaehl = 0.0, 0.0, 0
        schlimmste = (0.0, 0, 0)
        for i in range(2 * n):
            t = i / (2.0 * FPS)
            x = t / dauer * nb
            k = int(math.floor(x + 1e-9))
            f = x - k
            if schl:
                a, b = bak[k % nb], bak[(k + 1) % nb]
            else:
                a, b = bak[min(k, nb)], bak[min(k + 1, nb)]
            ist = lagen[i // 2] if i % 2 == 0 else eckenlagen(ske, ob, t)
            for vi in range(0, len(ist), 3):
                d = (a[vi].lerp(b[vi], f) - ist[vi]).length
                if d > schlimmste[0]:
                    schlimmste = (d, i, vi)
                groesst = max(groesst, d)
                summe += d
                zaehl += 1
        erg[f"vat{rate}_max_mm"] = round(groesst * 1000, 1)
        erg[f"vat{rate}_mittel_mm"] = round(summe / max(zaehl, 1) * 1000, 2)
        v = ob.data.vertices[schlimmste[2]]
        gruppe = namen[max(v.groups, key=lambda g: g.weight).group] if len(v.groups) else "-"
        erg[f"vat{rate}_ecke"] = {"bild": schlimmste[1] / 2.0, "ecke": schlimmste[2], "gruppe": gruppe, "mm": round(schlimmste[0] * 1000, 1)}
    return erg, lagen


def main():
    glb = OPT["glb"]
    aus = OPT.get("aus", os.path.join(WURZEL, "docs", "modelle"))
    os.makedirs(aus, exist_ok=True)
    meta = json.load(open(os.path.join(WURZEL, "game", "daten", "zugtiere.json"), encoding="utf-8"))
    art = "pferd" if "pferd" in glb else "ochse"
    meta = meta["tiere"][art]
    ske, ob = laden(glb)
    sohle = sohlen(ske, ob)
    ergebnis = {"datei": glb, "clips": {}, "uebergang": {}}
    lagen_clip = {}
    for name in meta["klips"]:
        erg, lagen = clip_messen(ske, ob, name, meta, sohle)
        ergebnis["clips"][name] = erg
        lagen_clip[name] = lagen
        print(f"{name}: {erg['bilder']} Bilder, tiefster {erg['tiefster_mm']} mm, Naht {erg['naht_mm']} mm, VAT12 max {erg['vat12_max_mm']} mm, VAT24 max {erg['vat24_max_mm']} mm, Naht {erg['naht_beschl_mm']}/{erg['max_beschl_mm']}", flush=True)
    for a, b in UEBERGAENGE:
        if a in lagen_clip and b in lagen_clip:
            p, q = lagen_clip[a][-1], lagen_clip[b][0]
            ergebnis["uebergang"][f"{a} -> {b}"] = round(max((x - y).length for x, y in zip(p, q)) * 1000, 1)
    json.dump(ergebnis, open(os.path.join(aus, glb + "_messung.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print("geschrieben:", os.path.join(aus, glb + "_messung.json"))
    for o in bpy.data.objects:                                        # das Fernnetz liegt im selben Raum wie das Nahnetz: nicht mitzeichnen (sonst flackern die Flächen)
        if o.type == "MESH" and o.name.startswith("Fern_"):
            o.hide_render = True
    if "bilder" in OPT and "nah" not in OPT:
        farbmaterial(ob)
    if "nah" in OPT and "ohnefell" not in OPT:
        fell_material(ob)
    if "nah" in OPT:
        nahbilder(ske, ob, glb, aus)
    if "bilder" in OPT:
        bilder(ske, ob, glb, aus, meta)


# ─────────────────────────────────────────────────────────────────────────────
#  Bildfolgen
# ─────────────────────────────────────────────────────────────────────────────

def buehne():
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    szene.cycles.device = "CPU"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "CUDA"
        prefs.get_devices()
        if any(d.type == "CUDA" for d in prefs.devices):
            for d in prefs.devices:
                d.use = d.type == "CUDA"
            szene.cycles.device = "GPU"
    except (KeyError, TypeError, AttributeError, RuntimeError):
        pass
    szene.cycles.samples = int(OPT.get("proben", 24))
    szene.cycles.use_denoising = False          # der Entrauscher macht aus den Eckenfarben bei wenigen Proben fleckige Flächen
    szene.view_settings.view_transform = "Standard"
    sonne = bpy.data.objects.new("_Sonne", bpy.data.lights.new("_Sonne", "SUN"))
    sonne.data.energy = 3.2
    sonne.data.color = (1.0, 0.94, 0.84)
    sonne.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    szene.collection.objects.link(sonne)
    welt = bpy.data.worlds.new("Himmel")
    welt.use_nodes = True
    hg = welt.node_tree.nodes["Background"]
    hg.inputs["Color"].default_value = (0.40, 0.45, 0.53, 1)
    hg.inputs["Strength"].default_value = 0.85
    szene.world = welt
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=40)
    me = bpy.data.meshes.new("_Boden")
    bm.to_mesh(me)
    bm.free()
    boden = bpy.data.objects.new("_Boden", me)
    m = bpy.data.materials.new("_Boden")
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.09, 0.09, 0.06, 1)
    boden.data.materials.append(m)
    szene.collection.objects.link(boden)
    # Streifen auf dem Boden (alle 0,5 m), damit man das Rutschen und den Weg sieht
    for i in range(-12, 13):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        me2 = bpy.data.meshes.new("_Streifen")
        bm.to_mesh(me2)
        bm.free()
        o = bpy.data.objects.new("_Streifen", me2)
        o.scale = (3.0, 0.02, 0.003)
        o.location = (0, i * 0.5, 0.002)
        mm = bpy.data.materials.new("_Streifen")
        mm.use_nodes = True
        mm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.5, 0.5, 0.35, 1) if i % 2 == 0 else (0.25, 0.25, 0.2, 1)
        o.data.materials.append(mm)
        szene.collection.objects.link(o)
    kam = bpy.data.objects.new("_Kamera", bpy.data.cameras.new("_Kamera"))
    szene.collection.objects.link(kam)
    szene.camera = kam
    return kam


def aufnahme(kam, ziel, az, el, ortho, mass, datei, b=520, h=340):
    szene = bpy.context.scene
    szene.render.resolution_x, szene.render.resolution_y = b, h
    kam.data.type = "ORTHO" if ortho else "PERSP"
    kam.data.ortho_scale = mass
    kam.data.clip_end = 500
    az, el = math.radians(az), math.radians(el)
    kam.location = ziel + Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el))) * 60
    kam.rotation_euler = (ziel - kam.location).normalized().to_track_quat("-Z", "Y").to_euler()
    szene.render.filepath = datei
    bpy.ops.render.render(write_still=True)


def farbmaterial_fuer(o):
    """Material nur mit der Eckenfarbe für ein einzelnes Netz (mehrere Tiere in einer Szene)."""
    ca = o.data.color_attributes
    m = bpy.data.materials.new("_Farbe")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    aus = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    b.inputs["Roughness"].default_value = 0.9
    e = nt.nodes.new("ShaderNodeVertexColor")
    e.layer_name = "Farbe" if "Farbe" in ca else (ca[0].name if len(ca) else "Farbe")
    g = nt.nodes.new("ShaderNodeGamma")
    g.inputs["Gamma"].default_value = 2.2
    nt.links.new(e.outputs["Color"], g.inputs["Color"])
    nt.links.new(g.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(b.outputs["BSDF"], aus.inputs["Surface"])
    o.data.materials.clear()
    o.data.materials.append(m)


def farbmaterial(ob):
    """Material nur mit der Eckenfarbe der GLB (wie das Spiel ohne Struktur) für die Bildfolgen."""
    ca = ob.data.color_attributes
    m = bpy.data.materials.new("_Farbe")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    aus = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    b.inputs["Roughness"].default_value = 0.9
    e = nt.nodes.new("ShaderNodeVertexColor")
    e.layer_name = "Farbe" if "Farbe" in ca else (ca[0].name if len(ca) else "Farbe")
    g = nt.nodes.new("ShaderNodeGamma")
    g.inputs["Gamma"].default_value = 2.2
    nt.links.new(e.outputs["Color"], g.inputs["Color"])
    nt.links.new(g.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(b.outputs["BSDF"], aus.inputs["Surface"])
    for o in bpy.data.objects:
        if o.type == "MESH" and o.name in ("Nah_pferd", "Nah_ochse"):
            o.data.materials.clear()
            o.data.materials.append(m)


def fell_material(ob):
    """Nachbau des Shaders im Spiel (oberflaeche.gdshader, Schicht „fell“ der Material-Bibliothek) in Blender, nur für die Bilder: Eckenfarbe
    mal Fell-Textur durch deren mittlere Farbe, dreiseitig projiziert (Kachel 0,5 m), die Normale der Schicht fehlt im Bild (mit --bump als Bump, das auf den flachen Flächen der Vorlage Artefakte macht). Das ist kein Bild aus Godot."""
    import numpy as np
    sys.path.insert(0, os.path.join(WURZEL, "tools", "oberflaeche"))
    import fell_schicht as fs
    farbe, h, (nx, ny, nz), rau = fs.bauen()
    n = farbe.shape[0]

    def bild(name, rgb, farbraum):
        im = bpy.data.images.new(name, n, n, alpha=False, float_buffer=True)
        a = np.dstack([rgb, np.ones((n, n))])[::-1]
        im.colorspace_settings.name = farbraum
        im.pixels.foreach_set(a.astype(np.float32).ravel())
        im.update()
        print("Fell-Bild:", name, "Mittel", float(np.mean(a[..., :3])), "Pixel", float(im.pixels[4 * 1000 + 1]))
        return im
    im_f = bild("fell_farbe", np.clip(farbe, 0, 1), "sRGB")
    im_n = bild("fell_hoehe", np.dstack([h, h, h]), "Non-Color")
    mittel = np.mean(np.where(farbe <= 0.04045, farbe / 12.92, ((farbe + 0.055) / 1.055) ** 2.4).reshape(-1, 3), axis=0)
    m = bpy.data.materials.new("_Fell")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    ausg = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.88
    koord = nt.nodes.new("ShaderNodeTexCoord")
    abb = nt.nodes.new("ShaderNodeMapping")
    abb.inputs["Scale"].default_value = (2.0, 2.0, 2.0)
    t1 = nt.nodes.new("ShaderNodeTexImage")
    t1.image = im_f
    t1.projection = "BOX"
    t1.projection_blend = 0.25
    t2 = nt.nodes.new("ShaderNodeTexImage")
    t2.image = im_n
    t2.projection = "BOX"
    t2.projection_blend = 0.25
    nm = nt.nodes.new("ShaderNodeBump")
    nm.inputs["Strength"].default_value = 0.55
    nm.inputs["Distance"].default_value = 0.004
    ecke = nt.nodes.new("ShaderNodeVertexColor")
    ca = ob.data.color_attributes
    ecke.layer_name = "Farbe" if "Farbe" in ca else (ca[0].name if len(ca) else "Farbe")
    print("Fell-Bild: Farbschicht", ecke.layer_name, [c.name for c in ca])
    gam = nt.nodes.new("ShaderNodeGamma")
    gam.inputs["Gamma"].default_value = 2.2
    teil = nt.nodes.new("ShaderNodeMix")
    teil.data_type = "RGBA"
    teil.blend_type = "MULTIPLY"
    teil.inputs["Factor"].default_value = 1.0
    skal = nt.nodes.new("ShaderNodeMix")
    skal.data_type = "RGBA"
    skal.blend_type = "MULTIPLY"
    skal.inputs["Factor"].default_value = 1.0
    skal.inputs["B"].default_value = (1 / mittel[0], 1 / mittel[1], 1 / mittel[2], 1)
    nt.links.new(koord.outputs["Object"], abb.inputs["Vector"])
    nt.links.new(abb.outputs["Vector"], t1.inputs["Vector"])
    nt.links.new(abb.outputs["Vector"], t2.inputs["Vector"])
    nt.links.new(t1.outputs["Color"], skal.inputs["A"])
    nt.links.new(ecke.outputs["Color"], gam.inputs["Color"])
    nt.links.new(gam.outputs["Color"], teil.inputs["A"])
    nt.links.new(skal.outputs["Result"], teil.inputs["B"])
    if os.environ.get("ZUGTIERE_NURFARBE"):
        nt.links.new(gam.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        nt.links.new(teil.outputs["Result"], bsdf.inputs["Base Color"])
    nt.links.new(t2.outputs["Color"], nm.inputs["Height"])
    if "bump" in OPT:
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    nt.links.new(bsdf.outputs["BSDF"], ausg.inputs["Surface"])
    for o in bpy.data.objects:
        if o.type == "MESH" and o.name in ("Nah_pferd", "Nah_ochse"):
            o.data.materials.clear()
            o.data.materials.append(m)


def nahbilder(ske, ob, glb, aus):
    nahbilder_ohne_buehne(ske, glb, aus, buehne())


def nahbilder_ohne_buehne(ske, glb, aus, kam):
    """Nahbilder (Stehen, Bild 0): Flanke mit Fell, Kopf mit Mähne, Schweif, Fesseln, Spielkamera nah."""
    os.makedirs(aus, exist_ok=True)
    aktion_setzen(ske, "Stehen")
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    pferd = "pferd" in glb
    h = 1.5 if pferd else 1.25
    ziele = (("nah_flanke", Vector((0, -0.1, h * 0.6)), 0, 8, True, 1.4 if pferd else 1.2, 1100, 760),
             ("nah_kopf_mahne", Vector((0, -0.75 if pferd else -0.9, h * 1.15)), -50, 12, True, 1.7 if pferd else 1.4, 1100, 760),
             ("nah_schweif", Vector((0, 1.25 if pferd else 1.2, h * 0.55)), 125, 10, True, 1.5 if pferd else 1.3, 1100, 760),
             ("nah_fesseln", Vector((0, -0.5, 0.2)), -30, 14, True, 1.2, 1100, 760),
             ("nah_bauch", Vector((0, 0.1, h * 0.33)), 40, -28, True, 2.4, 1100, 760),
             ("nah_spielkamera", Vector((0, 0.0, h * 0.55)), -135, 38, True, 4.2 if pferd else 3.6, 1100, 760),
             ("nah_mahne_seite", Vector((0, -1.0 if pferd else -0.9, 1.75 if pferd else h * 1.1)), 0, 0, True, 2.2, 1100, 760),
             ("nah_klauen_vorn", Vector((0, -0.5, 0.12)), -75, 18, True, 0.6, 1100, 760),
             ("nah_mahne_rechts", Vector((0, -1.0 if pferd else -0.9, 1.75 if pferd else h * 1.1)), 180, 10, True, 2.2, 1100, 760),
             ("nah_mahne_oben", Vector((0, -1.0 if pferd else -0.9, 1.75 if pferd else h * 1.1)), -20, 62, True, 2.2, 1100, 760),
             ("nah_schweif_oben", Vector((0, 1.3 if pferd else 1.2, 1.45 if pferd else h * 0.7)), 150, 55, True, 1.3, 1100, 760),
             ("nah_schweif_seite", Vector((0, 1.3 if pferd else 1.2, 1.1 if pferd else h * 0.55)), 0, 0, True, 1.9, 1100, 760),
             ("nah_spielkamera_klein", Vector((0, 0.0, h * 0.55)), -135, 38, True, 8.0, 800, 560))
    for name, ziel, az, el, ortho, mass, b, hh in ziele:
        unten = el < 0                                                  # Blick von unten: Boden und Streifen ausblenden
        for o in bpy.data.objects:
            if o.name.startswith(("_Boden", "_Streifen")):
                o.hide_render = unten
        aufnahme(kam, ziel, az, el, ortho, mass, os.path.join(aus, f"{glb}_{name}.png"), b, hh)
    for o in bpy.data.objects:
        if o.name.startswith(("_Boden", "_Streifen")):
            o.hide_render = False


def bilder(ske, ob, glb, aus, meta):
    kam = buehne()
    szene = bpy.context.scene
    schritt = int(OPT.get("schritt", 3))
    for name, info in meta["klips"].items():
        act = aktion_setzen(ske, name)
        n = int(round(act.frame_range[1] - act.frame_range[0]))
        ordner = os.path.join(aus, glb, name)
        os.makedirs(ordner, exist_ok=True)
        gif = "gif" in OPT
        folge = list(range(0, n + 1, max(1, n // 12 if not gif else schritt)))
        for i in folge:
            szene.frame_set(int(act.frame_range[0]) + i)
            bpy.context.view_layer.update()
            # Das Weltmaß mitlaufen lassen: der Boden mit den Streifen wandert gegen die Laufrichtung (Tier steht still)
            weg = (info.get("weg") or [0.0] * (n + 1))[min(i, n)]
            for o in bpy.data.objects:
                if o.name.startswith("_Streifen"):
                    o["y0"] = o.get("y0", o.location.y)
                    o.location.y = ((o["y0"] + weg + 6.0) % 12.0) - 6.0
            aufnahme(kam, Vector((0, 0.0, 0.9)), 0, 0, True, 5.2 if "pferd" in glb else 4.4, os.path.join(ordner, f"seite_{i:03d}.png"))
            aufnahme(kam, Vector((0, -0.3, 0.7)), -135, 38, True, 6.0 if "pferd" in glb else 5.2, os.path.join(ordner, f"spiel_{i:03d}.png"))
    print("Bilder:", os.path.join(aus, glb))


if __name__ == "__main__":
    main()
