"""Vorschaubilder und Videobilder der drei 3D-Gegenstaende (nicht im Spiel, Sichtpruefung), gerendert in Blender (-b, EEVEE) aus der .blend von bau.py.
  blender -b --python bilder.py -- <bauordner> <rohordner> <name|alle> [video]
Umriss wie im Spiel: Material "Umriss" unbeleuchtet schwarz, Rueckseiten weg (Cull Back). Pixel-Look nachgestellt: dieselbe Ansicht in 1/2 und 1/3
der Aufloesung ohne Kantenglaettung gerendert; montage.py zieht sie hart (naechster Nachbar) hoch. Inhaltsflaechen bekommen Platzhalterbilder
(Karte, Wappen), so wie die Oberflaeche spaeter ein eigenes Material setzt."""
import math
import os
import sys

import bpy
from mathutils import Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import texturen as tx  # noqa: E402

BREITE, HOEHE = 720, 540
VIDEO = 540

# Ansichten: Ziel, Abstand, Hoehenwinkel, Drehwinkel (Grad, 0 = von vorn), Bodenhoehe und Bodenfarbe
ANSICHT = {
    "kartentisch": dict(ziel=(0.0, 0.0, 0.52), ab=3.0, hw=32, dw=-28, boden=(0.20, 0.17, 0.14)),
    "kartentisch_nah": dict(ziel=(0.0, 0.0, 0.80), ab=1.55, hw=50, dw=-20, boden=(0.20, 0.17, 0.14)),
    "geldtruhe": dict(ziel=(0.0, 0.06, 0.28), ab=1.95, hw=40, dw=-28, boden=(0.20, 0.17, 0.14)),
    "siegelstempel": dict(ziel=(0.035, 0.0, 0.03), ab=0.56, hw=44, dw=-24, boden=(0.40, 0.29, 0.18)),
}

# Standbilder: (Dateiname, Clip, Bild, Zusatz)
STAND = {
    "kartentisch": [("zu", "zu", 0, {}), ("entrollen_halb", "entrollen", 12, {}), ("glaetten", "entrollen", 26, {}), ("offen", "offen", 0, {}),
                    ("offen_nah", "offen", 0, {"ansicht": "kartentisch_nah"}), ("zu_nah", "zu", 0, {"ansicht": "kartentisch_nah"})],
    "geldtruhe": [("zu", "zu", 0, {}), ("aufspringen", "aufspringen", 8, {"stufe": "halb"}), ("offen_leer", "offen", 0, {"stufe": None}),
                  ("offen_viertel", "offen", 0, {"stufe": "viertel"}), ("offen_halb", "offen", 0, {"stufe": "halb"}), ("offen_voll", "offen", 0, {"stufe": "voll"})],
    "siegelstempel": [("bereit", "bereit", 0, {}), ("ueber_dem_wachs", "siegeln", 12, {}), ("gepresst", "siegeln", 21, {}),
                      ("abgehoben", "siegeln", 27, {}), ("gesiegelt", "gesiegelt", 0, {}), ("gesiegelt_wappen", "gesiegelt", 0, {"wappen": True})],
}

# Clipphasen fuer die Bildleisten: (Clip, Anzahl Bilder); die Bilder liegen gleichmaessig ueber den Clip
LEISTE = {
    "kartentisch": [("entrollen", 7), ("zusammenrollen", 6)],
    "geldtruhe": [("aufspringen", 7), ("zuklappen", 6)],
    "siegelstempel": [("siegeln", 8)],
}

# Videoablauf: Liste von (Clip, Zusatz); jeder Clip erst in Echtzeit, am Ende alles noch einmal dreimal langsamer
FILM = {
    "kartentisch": [("entrollen", {}), ("offen", {"halten": 15}), ("zusammenrollen", {}), ("zu", {"halten": 15})],
    "geldtruhe": [("aufspringen", {"stufe": "halb"}), ("offen", {"halten": 12, "stufe": "leer"}), ("offen", {"halten": 12, "stufe": "viertel"}),
                  ("offen", {"halten": 12, "stufe": "halb"}), ("offen", {"halten": 12, "stufe": "voll"}), ("zuklappen", {"stufe": "voll"}), ("zu", {"halten": 12})],
    "siegelstempel": [("siegeln", {}), ("gesiegelt", {"halten": 12, "wappen": True}), ("bereit", {"halten": 8})],
}


def engine_setzen(sc):
    for e in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = e
            return
        except TypeError:
            continue


def umriss_material():
    m = bpy.data.materials.get("Umriss")
    nt = m.node_tree
    for nd in list(nt.nodes):
        nt.nodes.remove(nd)
    ausg = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (0.006, 0.004, 0.003, 1)
    nt.links.new(em.outputs["Emission"], ausg.inputs["Surface"])
    m.use_backface_culling = True


def bild_material(name, pfad):
    """Platzhalterbild (RGBA) auf eine Inhaltsflaeche legen."""
    m = bpy.data.materials.get(name)
    if m is None:
        return
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    img = bpy.data.images.load(pfad)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    try:
        m.surface_render_method = "BLENDED"
    except Exception:
        pass


def wappen_setzen(an):
    """So setzt die Oberflaeche das Hauswappen auf den Abdruck: eigenes Material mit Wappenbild (hier ein Platzhalter mit Hausfarben)."""
    m = bpy.data.materials.get("Siegel_Abdruck")
    nt = m.node_tree
    tex = next(n for n in nt.nodes if n.type == "TEX_IMAGE")
    tex.image = bpy.data.images[WAPPEN if an else "abdruck"]


WAPPEN = "wappen_haus"


def kamera(sc, a):
    ziel = Vector(a["ziel"])
    hw, dw = math.radians(a["hw"]), math.radians(a["dw"])
    richtung = Vector((math.sin(dw) * math.cos(hw), -math.cos(dw) * math.cos(hw), math.sin(hw)))
    cam = sc.camera
    cam.location = ziel + richtung * a["ab"]
    cam.rotation_euler = (ziel - cam.location).to_track_quat("-Z", "Y").to_euler()
    next(n for n in bpy.data.materials["Boden"].node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (*[c ** 2.2 for c in a["boden"]], 1)


def stufe_setzen(stufe):
    for o in bpy.data.objects:
        if o.parent is not None and o.parent.name.startswith("Fuellstufe_"):
            an = stufe is not None and o.parent.name == "Fuellstufe_" + stufe
            o.hide_render = not an
            o.hide_viewport = not an


def aufbau(bau, name):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(bau, name + ".blend"))
    sc = bpy.context.scene
    engine_setzen(sc)
    sc.render.film_transparent = False
    try:
        sc.eevee.use_shadows = True
    except Exception:
        pass
    w = bpy.data.worlds.new("Welt")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.074, 0.064, 0.052, 1)
    bg.inputs["Strength"].default_value = 3.2
    sd = bpy.data.lights.new("Sonne", "SUN")
    sd.energy = 3.4
    sd.angle = math.radians(3)
    so = bpy.data.objects.new("Sonne", sd)
    so.rotation_euler = (math.radians(48), 0, math.radians(-38))
    sc.collection.objects.link(so)
    cd = bpy.data.cameras.new("Kamera")
    cd.lens = 50
    co = bpy.data.objects.new("Kamera", cd)
    sc.collection.objects.link(co)
    sc.camera = co
    # Boden
    bm = bpy.data.materials.new("Boden")
    bm.use_nodes = True
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, -0.0005))
    bpy.context.active_object.data.materials.append(bm)
    sc.view_settings.view_transform = "Standard"
    umriss_material()
    roh = sys.argv[sys.argv.index("--") + 2]
    os.makedirs(roh, exist_ok=True)
    pk = os.path.join(roh, "karte_platzhalter.png")
    tx.png_rgba(pk, tx.karte_vorschau(512))
    bild_material("Karte_Inhalt", pk)
    if name == "siegelstempel":
        import numpy as np
        b = np.zeros((64, 64, 4), dtype=np.uint8)
        yy, xx = np.mgrid[0:64, 0:64]
        d = np.sqrt((xx - 31.5) ** 2 + (yy - 31.5) ** 2)
        b[d < 27] = (150, 28, 22, 255)
        b[(d < 27) & (xx > 31)] = (120, 20, 16, 255)
        sch = (np.abs(xx - 31.5) < 13) & (yy > 14) & (yy < 40 + (13 - np.abs(xx - 31.5)) * 0.9)
        b[sch] = (40, 8, 6, 200)
        b[sch & (np.abs(yy - 28) < 3)] = (210, 160, 70, 230)
        b[(d > 24) & (d < 27)] = (60, 10, 8, 230)
        pw = os.path.join(roh, "wappen_platzhalter.png")
        tx.png_rgba(pw, b)
        img = bpy.data.images.load(pw)
        img.name = WAPPEN
    stufe_setzen(None)
    return sc


def pose(sc, clip, bild):
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    arm.animation_data.action = bpy.data.actions[clip]
    ganz = int(math.floor(bild))
    sc.frame_set(ganz, subframe=bild - ganz)


def rendern(sc, pfad, b, h, ps=1):
    sc.render.resolution_x = b // ps
    sc.render.resolution_y = h // ps
    sc.render.resolution_percentage = 100
    # wie im Spiel: Texturen mit Mipmaps (gemittelt), dann hart hochgezogen; deshalb auch in kleiner Aufloesung mit Kantenglaettung
    sc.render.filter_size = 1.5
    try:
        sc.eevee.taa_render_samples = 16
    except Exception:
        pass
    sc.render.filepath = pfad
    bpy.ops.render.render(write_still=True)


def zusatz(sc, name, z):
    if "stufe" in z:
        stufe_setzen(None if z["stufe"] in (None, "leer") else z["stufe"])
    if name == "siegelstempel":
        wappen_setzen(bool(z.get("wappen")))
    kamera(sc, ANSICHT[z.get("ansicht", name)])


def lauf(bau, roh, name, video):
    sc = aufbau(bau, name)
    os.makedirs(roh, exist_ok=True)
    for datei, clip, bild, z in STAND[name]:
        zusatz(sc, name, z)
        pose(sc, clip, bild)
        for ps in (1, 2, 3):
            rendern(sc, os.path.join(roh, f"{name}__{datei}__p{ps}.png"), BREITE, HOEHE, ps)
    stufe_setzen("halb" if name == "geldtruhe" else None)
    zusatz(sc, name, {"ansicht": "kartentisch_nah"} if name == "kartentisch" else {})
    for clip, anzahl in LEISTE[name]:
        dauer = bpy.data.actions[clip].frame_range[1]
        for i in range(anzahl):
            fr = dauer * i / (anzahl - 1)
            pose(sc, clip, fr)
            for ps in (1, 3):
                rendern(sc, os.path.join(roh, f"{name}__leiste_{clip}_{i}__p{ps}.png"), 360, 300, ps)
    if not video:
        return
    vdir = os.path.join(roh, "video_" + name)
    os.makedirs(vdir, exist_ok=True)
    nr = 0
    for tempo in (1, 3):
        stufe_setzen("halb" if name == "geldtruhe" else None)
        for clip, z in FILM[name]:
            zusatz(sc, name, z)
            dauer = bpy.data.actions[clip].frame_range[1]
            if "halten" in z:
                bilder = [0.0] * z["halten"]
            else:
                bilder = [i / tempo for i in range(int(dauer * tempo) + 1)]
            for fr in bilder:
                pose(sc, clip, min(fr, dauer))
                rendern(sc, os.path.join(vdir, f"f_{nr:04d}.png"), VIDEO, VIDEO, 1)
                nr += 1
    print("VIDEO", name, nr)


if __name__ == "__main__":
    a = sys.argv[sys.argv.index("--") + 1:]
    namen = ["kartentisch", "geldtruhe", "siegelstempel"] if a[2] == "alle" else a[2].split(",")
    for n in namen:
        lauf(a[0], a[1], n, len(a) > 3 and a[3] == "video")
