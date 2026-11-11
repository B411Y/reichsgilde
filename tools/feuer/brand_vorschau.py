"""Vorschau des Gebäudebrands ohne Spiel: ein Haus mit Flammen aus Fenstern und Dach, gerechnet mit Blender (Cycles, nur Prozessor, ohne Fenster).

    blender -b --factory-startup --python tools/feuer/brand_vorschau.py -- --ziel <Ordner> [--bild phasen|alt|nah] [--aufloesung 1280]

Die Flammen sind dieselben Flipbooks wie im Spiel (game/assets/texturen/feuer/flamme_fenster.png und flamme_dach.png, vormultipliziert: Leuchten + Hintergrund · (1 − Deckung)),
Größen und Lagen nach Gebaeudebrand.cs und FeuerArt.cs: Fensterflamme wandparallel in der Öffnung, Dachflamme zur Kamera gedreht mit dem Fuß im Dach, Brandfleck
flach auf dem Dach, glühende Öffnung im Fenster. Rauch (Shader im Spiel) und die Pixel-Welt des Spiels fehlen; es ist eine Annäherung zum Ansehen der Anordnung,
nicht des fertigen Bildes. Bilder: phasen (vier Häuser: Anfang, Vollbrand, Ausklingen, Glut), alt (altes Feuer gegen neues), nah (ein Haus im Vollbrand nah).
"""
import json
import math
import os
import sys

import bpy
import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.normpath(os.path.join(HIER, "..", "..", "game", "assets", "texturen", "feuer"))


def optionen():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = {"ziel": ".", "bild": "phasen", "aufloesung": 1280}
    for i in range(0, len(argv) - 1, 2):
        o[argv[i][2:]] = argv[i + 1]
    o["aufloesung"] = int(o["aufloesung"])
    return o


# ── Brandverlauf (wie BrandVerlauf.Rechnen in Gebaeudebrand.cs) ──────────────
def weich(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


def verlauf(t, b=120.0, g=180.0):
    anfang = min(max(b * 0.28, 8), 30)
    voll = max(b * 0.45, anfang + 4)
    klingt = max(b * 0.75, voll + 4)
    if t < b:
        if t < anfang:
            f = 0.38 * weich(t / anfang)
        elif t < voll:
            f = 0.38 + 0.62 * weich((t - anfang) / (voll - anfang))
        elif t < klingt:
            f = 1.0
        else:
            f = 1.0 - 0.88 * weich((t - klingt) / (b - klingt))
        f *= weich((b - t) / 2.5)
        gl = 0.55 * weich(t / anfang) if t < anfang else 0.55 + 0.3 * weich((t - voll) / max(b - voll, 1))
        return f, gl
    u = min(max((t - b) / g, 0), 1)
    return 0.0, 0.85 * (1 - u) ** 2 * (0.6 + 0.4 * (1 - u))


# ── Flipbooks ────────────────────────────────────────────────────────────────
_bilder = {}


def bild_laden(name):
    if name in _bilder:
        return _bilder[name]
    kenn = json.load(open(os.path.join(TEX, f"flamme_{name}.json")))
    img = bpy.data.images.load(os.path.join(TEX, f"flamme_{name}.png"))
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]   # Zeile 0 oben
    _bilder[name] = (kenn, px)
    return _bilder[name]


def bild_ausschneiden(name, nr, tag):
    kenn, px = bild_laden(name)
    sp, bw, bh = kenn["spalten"], kenn["bild_pixel"][0], kenn["bild_pixel"][1]
    z, s = divmod(nr % kenn["bilder"], sp)
    crop = px[z * bh:(z + 1) * bh, s * bw:(s + 1) * bw][::-1]             # für Blender: Zeile 0 unten
    img = bpy.data.images.new(f"{tag}_{name}_{nr}", bw, bh, alpha=True)
    img.pixels = crop.reshape(-1).tolist()
    img.alpha_mode = "CHANNEL_PACKED"
    return img


def flammen_material(img, hell=1.8):
    m = bpy.data.materials.new("Flamme")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Linear"
    tex.extension = "EXTEND"
    emi = nt.nodes.new("ShaderNodeEmission")
    emi.inputs["Strength"].default_value = hell
    nt.links.new(tex.outputs["Color"], emi.inputs["Color"])
    # Deckung: Transparent mit der Farbe (1 − Alpha) dämpft den Hintergrund
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    nt.links.new(tex.outputs["Alpha"], inv.inputs[1])
    mix = nt.nodes.new("ShaderNodeCombineColor")
    for k in ("Red", "Green", "Blue"):
        nt.links.new(inv.outputs[0], mix.inputs[k])
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    nt.links.new(mix.outputs[0], tr.inputs["Color"])
    add = nt.nodes.new("ShaderNodeAddShader")
    nt.links.new(emi.outputs[0], add.inputs[0])
    nt.links.new(tr.outputs[0], add.inputs[1])
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(add.outputs[0], out.inputs["Surface"])
    return m


def flaeche(name, breite, hoehe, mat, fuss_unten=0.0):
    """Eine Fläche, deren Fuß im Ursprung liegt (steht in der XZ-Ebene, Vorderseite −Y)."""
    bpy.ops.mesh.primitive_plane_add(size=1)
    o = bpy.context.active_object
    o.name = name
    o.rotation_euler = (math.radians(90), 0, 0)
    o.scale = (breite, hoehe, 1)
    bpy.ops.object.transform_apply(rotation=True, scale=True)
    for v in o.data.vertices:
        v.co.z += hoehe / 2 - fuss_unten
    o.data.materials.append(mat)
    return o


def stoff(name, farbe, rauh=0.9, emission=None, staerke=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*farbe, 1)
    b.inputs["Roughness"].default_value = rauh
    if emission:
        b.inputs["Emission Color"].default_value = (*emission, 1)
        b.inputs["Emission Strength"].default_value = staerke
    return m


def oeffnung_material(staerke):
    """Glühende Fensteröffnung: Verlauf von dunkelrot unten über gelb zur Mitte, mit Rauschen."""
    m = bpy.data.materials.new("Oeffnung")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["UV"], sep.inputs[0])
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 5
    nt.links.new(tc.outputs["UV"], noise.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.9, 0.12, 0.02, 1)
    ramp.color_ramp.elements[1].color = (1.0, 0.75, 0.3, 1)
    mixv = nt.nodes.new("ShaderNodeMath")
    mixv.operation = "ADD"
    mixv.inputs[0].default_value = 0.0
    nt.links.new(sep.outputs["Y"], mixv.inputs[0])
    nt.links.new(noise.outputs["Fac"], mixv.inputs[1])
    nm = nt.nodes.new("ShaderNodeMath")
    nm.operation = "MULTIPLY"
    nm.inputs[1].default_value = 0.6
    nt.links.new(mixv.outputs[0], nm.inputs[0])
    nt.links.new(nm.outputs[0], ramp.inputs["Fac"])
    emi = nt.nodes.new("ShaderNodeEmission")
    emi.inputs["Strength"].default_value = 1.1 * staerke
    nt.links.new(ramp.outputs["Color"], emi.inputs["Color"])
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(emi.outputs[0], out.inputs["Surface"])
    return m


def fleck_material(hitze):
    """Brandfleck: Kohle mit glühenden Rissen und weichem Rand."""
    m = bpy.data.materials.new("Fleck")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    tc = nt.nodes.new("ShaderNodeTexCoord")
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 7
    noise.inputs["Detail"].default_value = 4
    nt.links.new(tc.outputs["UV"], noise.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.62
    ramp.color_ramp.elements[1].position = 0.72
    nt.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    emi = nt.nodes.new("ShaderNodeEmission")
    emi.inputs["Color"].default_value = (1.0, 0.3, 0.05, 1)
    emi.inputs["Strength"].default_value = 1.2 * hitze
    dunkel = nt.nodes.new("ShaderNodeBsdfDiffuse")
    dunkel.inputs["Color"].default_value = (0.02, 0.016, 0.014, 1)
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(ramp.outputs["Color"], mix.inputs["Fac"])
    nt.links.new(dunkel.outputs[0], mix.inputs[1])
    nt.links.new(emi.outputs[0], mix.inputs[2])
    # runder Rand
    grad = nt.nodes.new("ShaderNodeTexGradient")
    grad.gradient_type = "SPHERICAL"
    nt.links.new(tc.outputs["UV"], grad.inputs["Vector"])
    # UV 0..1: Mitte verschieben auf (0.5, 0.5)
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Location"].default_value = (-0.5, -0.5, 0)
    nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
    nt.links.new(mp.outputs[0], grad.inputs["Vector"])
    kante = nt.nodes.new("ShaderNodeMapRange")
    kante.inputs["From Min"].default_value = 0.2
    kante.inputs["From Max"].default_value = 0.5
    kante.inputs["To Min"].default_value = 1.0
    kante.inputs["To Max"].default_value = 0.0
    nt.links.new(grad.outputs["Fac"], kante.inputs["Value"])
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix2 = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(kante.outputs["Result"], mix2.inputs["Fac"])
    nt.links.new(tr.outputs[0], mix2.inputs[1])
    nt.links.new(mix.outputs[0], mix2.inputs[2])
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(mix2.outputs[0], out.inputs["Surface"])
    return m


# ── Haus (wie BrandHausBauen in Feuertest.cs; Spielkoordinaten (x, y, z) → Blender (x, −z, y)) ──
def kasten(name, gx, gy, gz, bx, by, bz, mat):
    """Kasten mit Fuß bei (gx, gy, gz) in Spielkoordinaten, Größe (bx, by, bz)."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(gx, -gz, gy + by / 2))
    o = bpy.context.active_object
    o.name = name
    o.scale = (bx, bz, by)
    o.data.materials.append(mat)
    return o


def haus(x0, stand, mats, flip, nacht=True):
    breite, hoehe, tiefe = 8.0, 6.2, 6.4
    kasten("Wand", x0, 0, 0, breite, hoehe, tiefe, mats["putz"])
    dh = breite * 0.3
    hd = (breite + 0.6) / 2
    # Satteldach, First entlang der Tiefe (Blender-Y)
    me = bpy.data.meshes.new("Dach")
    v = [(-hd, -(tiefe + 0.6) / 2, 0), (hd, -(tiefe + 0.6) / 2, 0), (0, -(tiefe + 0.6) / 2, dh),
         (-hd, (tiefe + 0.6) / 2, 0), (hd, (tiefe + 0.6) / 2, 0), (0, (tiefe + 0.6) / 2, dh)]
    f = [(0, 1, 2), (3, 5, 4), (0, 2, 5, 3), (1, 4, 5, 2), (0, 3, 4, 1)]
    me.from_pydata(v, [], f)
    d = bpy.data.objects.new("Dach", me)
    bpy.context.collection.objects.link(d)
    d.location = (x0, 0, hoehe)
    me.materials.append(mats["dach"])
    flamme, glut = stand
    # Fenster und Anker (wie die Testszene)
    fenster = []
    for fy in (0.9, hoehe * 0.58):
        for fx in (-breite * 0.3, 0.0, breite * 0.3):
            if fy < 1.5 and abs(fx) < 0.1:
                continue
            fenster.append((fx, fy + 0.6, tiefe / 2, (0, 0, 1)))
        for gz in (-tiefe * 0.22, tiefe * 0.22):
            for s in (-1, 1):
                fenster.append((s * breite / 2, fy + 0.6, gz, (s, 0, 0)))
    for fx, fymitte, fz, n in fenster:
        kasten("Fenster", x0 + fx + n[0] * -0.01, fymitte - 0.6, fz + n[2] * -0.01, 0.9 if n[2] else 0.06, 1.2, 0.06 if n[2] else 0.9, mats["glas"])
    kasten("Tuer", x0, 0, tiefe / 2 - 0.02, 1.0, 1.9, 0.08, mats["holz"])
    # Fenster in Reihenfolge der Auswahl: Brandherd unten vorn, dann weit verteilt (hier fest gewählt wie im Spiel: Reihenfolge der Liste nach Entfernung)
    wahl = [fenster[1], fenster[4 if len(fenster) > 4 else 2], fenster[len(fenster) - 3], fenster[7 if len(fenster) > 7 else 3], fenster[len(fenster) - 6], fenster[5], fenster[9 if len(fenster) > 9 else 1]]
    hell_hinter = []
    for k, (fx, fymitte, fz, n) in enumerate(wahl):
        schwelle = 0.04 + 0.09 * k
        f = weich((flamme - schwelle) / 0.24)
        glimm = min(max(f, glut * 0.8), 1.0)
        if glimm > 0.02:
            ow = 0.9 * 0.92 if n[2] else 0.9 * 0.92
            o = flaeche("Oeffnung", ow, 1.2 * 0.92, mats["oeffnung"])
            o.location = (x0 + fx + n[0] * 0.035, -(fz + n[2] * 0.035), fymitte - 0.6 * 0.92)
            o.rotation_euler = (0, 0, 0) if n[2] else (0, 0, math.radians(-90 * n[0]))
            o.scale = (1, 1, 1)
        if f > 0.02:
            kenn, _ = bild_laden("fenster")
            art_h, art_b = 3.0, 1.8
            g = min(max(0.9 / 0.9, 0.75), 1.5)
            hh = art_h * g / kenn["flamme_hoehe"] * f
            seiten = kenn["bild_pixel"][0] / kenn["bild_pixel"][1]
            nat = kenn["flamme_breite"] * seiten / kenn["flamme_hoehe"]
            ww = hh * seiten * min(max(art_b / art_h / max(nat, 0.05), 0.8), 1.4)
            img = bild_ausschneiden("fenster", flip + k * 23, f"fe{x0}_{k}")
            o = flaeche("Fensterflamme", ww, hh, flammen_material(img), fuss_unten=kenn["fuss"] * hh)
            o.location = (x0 + fx + n[0] * 0.09, -(fz + n[2] * 0.09), fymitte - 1.2 * 0.38)
            o.rotation_euler = (0, 0, 0) if n[2] else (0, 0, math.radians(-90 * n[0]))
            hell_hinter.append((x0 + fx, -(fz), fymitte, f))
    # Dach: Punkte wie in der Testszene
    dach = [(0.7, 0.1, 1), (0.6, -0.28, -1), (0.75, 0.3, -1), (0.55, -0.3, 1), (0.8, -0.05, 1)]
    for k, (u, gz, s) in enumerate(dach):
        schwelle = 0.30 + 0.07 * k
        f = weich((flamme - schwelle) / 0.2) * (0.72 + 0.5 * weich((flamme - 0.55) / 0.4))
        glimm = min(max(f, glut * (1.0 if k == 0 else 0.75)), 1.0)
        px, py, pz = x0 + s * hd * (1 - u), -(gz * tiefe), hoehe + dh * u
        if glimm > 0.02:
            bpy.ops.mesh.primitive_plane_add(size=1, location=(px, py, pz + 0.05))
            fl = bpy.context.active_object
            fl.name = "Brandfleck"
            fl.scale = (2.8 * min(glimm, 1), 2.1 * min(glimm, 1), 1)
            fl.rotation_euler = (0, s * math.atan2(dh, hd), 0)   # flach auf die Dachfläche (Normale s·dh, hd)
            fl.data.materials.append(fleck_material(max(flamme * 1.15 + glut, 0.3)))
        if f > 0.02:
            kenn, _ = bild_laden("dach")
            art_h, art_b = (4.4, 2.2) if k == 0 else (3.8, 1.9)
            hh = art_h * 1.0 / kenn["flamme_hoehe"] * f
            seiten = kenn["bild_pixel"][0] / kenn["bild_pixel"][1]
            nat = kenn["flamme_breite"] * seiten / kenn["flamme_hoehe"]
            ww = hh * seiten * min(max(art_b / art_h / max(nat, 0.05), 0.8), 1.4)
            img = bild_ausschneiden("dach", flip + k * 31 + 7, f"da{x0}_{k}")
            o = flaeche("Dachflamme", ww, hh, flammen_material(img), fuss_unten=kenn["fuss"] * hh)
            o.location = (px, py, pz - 0.12)
            hell_hinter.append((px, py, pz + 1.5, f))
    # Licht: ein Schein über dem Dach nach der Flammenstärke
    if flamme > 0.05 or glut > 0.1:
        li = bpy.data.lights.new("Brand", "POINT")
        li.energy = 2200 * flamme ** 1.2 + 150 * glut
        li.color = (1.0, 0.5, 0.2)
        li.shadow_soft_size = 1.5
        lo = bpy.data.objects.new("Brandlicht", li)
        bpy.context.collection.objects.link(lo)
        lo.location = (x0, -4.0, hoehe + dh + 5.0)


def flammen_alt(x0, mats):
    """Das alte Feuer (vor „Feuer überall“): zehn Zungen des wilden Flipbooks schweben über dem Dach (Radius 2,2 m), zwei Fensterbrände stehen als Fläche vor den Fenstern."""
    kenn, _ = bild_laden("wild")
    seiten = kenn["bild_pixel"][0] / kenn["bild_pixel"][1]
    nat = kenn["flamme_breite"] * seiten / kenn["flamme_hoehe"]
    rng = np.random.default_rng(5)
    for i in range(10):
        w = i * 2.39996
        r = 2.2 * math.sqrt((i + 0.5) / 10) if i else 0.0
        gr = (1 - 0.35 * (r / 2.2)) * (1.0 if i == 0 else rng.uniform(0.72, 1.05))
        hh = 3.0 * gr / kenn["flamme_hoehe"]
        ww = hh * seiten * min(max(1.9 / 3.0 / max(nat, 0.05), 0.8), 1.4)
        o = flaeche("alt_dach", ww, hh, flammen_material(bild_ausschneiden("wild", 10 + i * 13, f"alt{x0}_{i}")), fuss_unten=kenn["fuss"] * hh)
        o.location = (x0 + math.cos(w) * r, -math.sin(w) * r, 6.7 + 1.0)
    for k, fx in enumerate((-1.8, 1.8)):
        hh = 1.1 / kenn["flamme_hoehe"]
        ww = hh * seiten * min(max(0.85 / 1.1 / max(nat, 0.05), 0.8), 1.4)
        for z in range(3):
            o = flaeche("alt_fenster", ww, hh, flammen_material(bild_ausschneiden("wild", 40 + k * 17 + z * 9, f"altf{x0}_{k}_{z}")), fuss_unten=kenn["fuss"] * hh)
            o.location = (x0 + fx + (z - 1) * 0.3, -(2.85 + 0.1 * z), 1.3)


def szene(o):
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 40
    sc.cycles.use_denoising = False
    sc.render.resolution_x = o["aufloesung"]
    sc.render.resolution_y = int(o["aufloesung"] * 9 / 16)
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.exposure = 0.6
    w = bpy.data.worlds.new("Nacht")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.016, 0.034, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    sc.world = w
    mats = {
        "putz": stoff("putz", (0.42, 0.34, 0.26)), "dach": stoff("dach", (0.16, 0.08, 0.06)), "glas": stoff("glas", (0.05, 0.06, 0.08), 0.2),
        "holz": stoff("holz", (0.18, 0.1, 0.05)), "boden": stoff("boden", (0.1, 0.1, 0.08)), "oeffnung": oeffnung_material(1.0),
    }
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, 0))
    bpy.context.active_object.data.materials.append(mats["boden"])
    mond = bpy.data.lights.new("Mond", "SUN")
    mond.energy = 0.25
    mond.color = (0.6, 0.7, 1.0)
    mo = bpy.data.objects.new("Mond", mond)
    bpy.context.collection.objects.link(mo)
    mo.rotation_euler = (math.radians(55), 0, math.radians(30))
    return mats


def kamera(ziel, abstand, orthos):
    cam = bpy.data.cameras.new("Kamera")
    cam.type = "ORTHO"
    cam.ortho_scale = orthos
    co = bpy.data.objects.new("Kamera", cam)
    bpy.context.collection.objects.link(co)
    n = math.radians(40)
    co.location = (ziel[0], ziel[1] - math.cos(n) * abstand, ziel[2] + math.sin(n) * abstand)
    co.rotation_euler = (math.radians(90) - n, 0, 0)
    bpy.context.scene.camera = co
    return co


def main():
    o = optionen()
    os.makedirs(o["ziel"], exist_ok=True)
    mats = szene(o)
    if o["bild"] == "phasen":
        for x0, t in ((-15, 14), (-5, 60), (5, 105), (15, 150)):
            haus(x0, verlauf(t), mats, flip=int(t))
        kamera((0, 0, 5), 120, 44)
        name = "haeuser_phasen.png"
    elif o["bild"] == "nah":
        haus(0, verlauf(60), mats, flip=17)
        kamera((0, 0, 6), 120, 15)
        name = "haus_vollbrand_nah.png"
    else:
        haus(-8, (0.0, 0.0), mats, flip=0)
        flammen_alt(-8, mats)
        haus(8, verlauf(60), mats, flip=17)
        kamera((0, 0, 5), 120, 30)
        name = "alt_gegen_neu.png"
    bpy.context.scene.render.filepath = os.path.join(o["ziel"], name)
    bpy.ops.render.render(write_still=True)
    print("[vorschau] geschrieben", bpy.context.scene.render.filepath)


main()
