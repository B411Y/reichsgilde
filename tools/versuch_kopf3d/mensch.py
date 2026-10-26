# Versuch 3D-Köpfe: Person aus einer "DNA" (Formregler) mit MPFB 2 bauen.
# Aufruf nur über Blender mit MPFB-Erweiterung: ~/werkzeuge/mpfb/blender_mpfb.sh -b --python <skript> -- ...
#
# DNA (JSON):
#   makro:  gender (0 Frau .. 1 Mann), age (0,5 = 25 J., 1 = 90 J.), muscle, weight, proportions, height,
#           caucasian/african/asian
#   regler: Name einer MakeHuman-Kategorie (z. B. "chin-width-decr-incr") -> Wert -1..1
#           (negativ = erstes Ziel, positiv = zweites; einseitige Kategorien wie "head-square" nur 0..1)
#   haut, augen, haar, brauen, bart (Haar-Editor: wild_beard / wild_moustache), kleidung, haarfarbe [r,g,b]
import bpy, os, json, math, sys
from importlib import import_module
from mathutils import Vector

MP = "bl_ext.user_default.mpfb"


def _klasse(modul, name):
    return getattr(import_module(f"{MP}.{modul}"), name)


HumanService = _klasse("services.humanservice", "HumanService")
TargetService = _klasse("services.targetservice", "TargetService")
AssetService = _klasse("services.assetservice", "AssetService")
LocationService = _klasse("services.locationservice", "LocationService")
ObjectService = _klasse("services.objectservice", "ObjectService")
ModifierService = _klasse("services.modifierservice", "ModifierService")
HumanObjectProperties = _klasse("entities.objectproperties", "HumanObjectProperties")

ZIELE = LocationService.get_mpfb_data("targets")
NUTZERDATEN = os.path.expanduser("~/werkzeuge/mpfb/daten/data")
with open(os.path.join(ZIELE, "target.json")) as f:
    _KATEGORIEN = {k["name"]: (gruppe, k)
                   for gruppe, inhalt in json.load(f).items() for k in inhalt["categories"]}


def kategorien():
    return _KATEGORIEN


def szene_leeren():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for sammlung in (bpy.data.meshes, bpy.data.materials, bpy.data.curves, bpy.data.hair_curves,
                     bpy.data.node_groups, bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for d in list(sammlung):
            if d.users == 0:
                sammlung.remove(d)


def _ziel_pfad(gruppe, datei):
    for wurzel in (os.path.join(ZIELE, gruppe), os.path.join(NUTZERDATEN, "targets", gruppe)):
        for endung in (".target.gz", ".target"):
            p = os.path.join(wurzel, datei + endung)
            if os.path.exists(p):
                return p
    raise FileNotFoundError(f"{gruppe}/{datei}")


def regler_ziele(name, wert):
    """Liste (pfad, gewicht) für einen Regler."""
    gruppe, k = _KATEGORIEN[name]
    if wert == 0:
        return []
    if not k.get("opposites") or not any(k["opposites"].values()):
        # einseitig (z. B. head-square): nur positive Werte
        return [(_ziel_pfad(gruppe, k["targets"][0]), max(0.0, wert))]
    seite = "negative" if wert < 0 else "positive"
    o = k["opposites"]
    if k["has_left_and_right"]:
        return [(_ziel_pfad(gruppe, o[f"{seite}-left"]), abs(wert)),
                (_ziel_pfad(gruppe, o[f"{seite}-right"]), abs(wert))]
    return [(_ziel_pfad(gruppe, o[f"{seite}-unsided"]), abs(wert))]


def _asset(unterordner, name, endung=".mhclo"):
    p = AssetService.find_asset_absolute_path(name + endung, asset_subdir=unterordner)
    if p is None:
        raise FileNotFoundError(f"{unterordner}/{name}{endung}")
    return p


def mensch_bauen(dna, *, unterteilung=1, mit_haareditor=True, extra_ziele=()):
    """Baut die Person. extra_ziele: weitere Ziele als Shape Keys mit Wert 0 (z. B. für Godot)."""
    b = HumanService.create_human()
    m = dna.get("makro", {})
    for schluessel in ("gender", "age", "muscle", "weight", "proportions", "height", "cupsize", "firmness"):
        if schluessel in m:
            HumanObjectProperties.set_value(schluessel, m[schluessel], entity_reference=b)
    for volk in ("caucasian", "african", "asian"):
        if volk in m:
            HumanObjectProperties.set_value(volk, m[volk], entity_reference=b)
    TargetService.reapply_macro_details(b)
    for name, wert in dna.get("regler", {}).items():
        for pfad, gewicht in regler_ziele(name, wert):
            TargetService.load_target(b, pfad, weight=gewicht)
    for gruppe, datei, name in extra_ziele:
        TargetService.load_target(b, _ziel_pfad(gruppe, datei), weight=0.0, name=name)

    haut = dna.get("haut", "young_caucasian_male")
    HumanService.set_character_skin(_asset("skins", haut, ".mhmat"), b, skin_type=dna.get("hauttyp", "ENHANCED_SSS"))

    def anlegen(unterordner, name, typ, material="MAKESKIN"):
        return HumanService.add_mhclo_asset(_asset(unterordner, name), b, asset_type=typ,
                                            subdiv_levels=unterteilung, material_type=material)

    anlegen("eyes", dna.get("augen_netz", "high-poly"), "Eyes", "PROCEDURAL_EYES")
    if dna.get("wimpern", "eyelashes02"):
        anlegen("eyelashes", dna.get("wimpern", "eyelashes02"), "Eyelashes")
    if dna.get("brauen"):
        anlegen("eyebrows", dna["brauen"], "Eyebrows")
    anlegen("teeth", "teeth_base", "Teeth")
    if dna.get("haar"):
        anlegen("hair", dna["haar"], "Hair")
    for kl in dna.get("kleidung", []):
        anlegen("clothes", kl, "Clothes")
    if mit_haareditor:
        # Haar-Editor-Kurven sitzen auf dem Grundnetz; erst mit rest_position folgen sie den Formreglern.
        b.add_rest_position_attribute = True
        for h in ([dna["bart"]] if dna.get("bart") else []) + dna.get("haareditor", []):
            obj = haareditor_anlegen(b, h)
            e = dna.get("haareditor_werte", {}).get(h, {})
            farbe = dna.get("haarfarbe")
            haareditor_einstellen(obj, e.get("werte"),
                                  e.get("farbe1", farbe), e.get("farbe2", [c * 0.35 for c in farbe] if farbe else None),
                                  e.get("dunkle_wurzel"))
    aussehen_anwenden(b, dna)
    if dna.get("kopfhaut"):
        kopfhaut_attribut(b)
    return b


def kopfhaut_attribut(b, name="haarflaeche"):
    """Gewichte der Vertexgruppe 'scalp' als Punkt-Attribut, damit Shader die Kopfhaut in Haarfarbe malen."""
    gi = b.vertex_groups["scalp"].index
    werte = [0.0] * len(b.data.vertices)
    for v in b.data.vertices:
        for g in v.groups:
            if g.group == gi:
                werte[v.index] = g.weight
    attr = b.data.attributes.get(name) or b.data.attributes.new(name, "FLOAT", "POINT")
    attr.data.foreach_set("value", werte)


def aussehen_anwenden(b, dna):
    """Haarfarbe (Netz-Haar und Brauen), Augenfarbe, Kleiderfarben."""
    if dna.get("haarfarbe"):
        for typ in ("Hair", "Eyebrows"):
            o = asset_objekt(b, typ)
            if o:
                # Netz-Haar hat eine hellbraune Textur (~0,25 linear): Faktor so wählen, dass die Haarfarbe herauskommt
                material_toenen(o, [min(1.0, c / 0.25) for c in dna["haarfarbe"]])
    if dna.get("augenfarbe"):
        augenfarbe(b, dna["augenfarbe"], dna.get("augenfarbe2"))
    for name, farbe in dna.get("kleiderfarben", {}).items():
        for o in bpy.data.objects:
            if o.type == "MESH" and o.name.lower().startswith(name.lower()):
                material_toenen(o, farbe)


def haareditor_anlegen(b, name):
    """Haar-Editor-Haar (Geometry-Nodes-Kurven) wie der MPFB-Knopf "Apply hair" anlegen."""
    ObjectService.deselect_and_deactivate_all()
    ObjectService.activate_blender_object(b)
    with bpy.context.temp_override(object=b, active_object=b, selected_objects=[b]):
        ergebnis = bpy.ops.mpfb.apply_hair_operator(hair_asset=name)
    obj = bpy.data.objects.get(name)
    print("HAAREDITOR", name, ergebnis, obj)
    return obj


def objekte(b):
    """Basemesh und alle angehängten Objekte."""
    return [b] + [o for o in bpy.data.objects if o.parent == b or (o.parent and o.parent.parent == b)]


def gelenk(b, gruppe):
    """Mittelpunkt einer Gelenk-Vertexgruppe (Hilfsgeometrie) mit allen Formreglern, in Weltkoordinaten."""
    masken = [m for m in b.modifiers if m.type == "MASK"]
    for m in masken:
        m.show_viewport = False
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = b.evaluated_get(dg)
    me = ev.to_mesh()
    gi = b.vertex_groups[gruppe].index
    summe, n = Vector(), 0
    for v in me.vertices:
        for g in v.groups:
            if g.group == gi and g.weight > 0.5:
                summe += v.co
                n += 1
    ev.to_mesh_clear()
    for m in masken:
        m.show_viewport = True
    bpy.context.view_layer.update()
    return b.matrix_world @ (summe / max(n, 1))


def brustbild_kamera(b, *, drehung=-28.0, brennweite=85.0, abstand=1.1, hoehe_versatz=0.02, seitlich=0.0):
    """Person um Z drehen (Dreiviertelansicht), Kamera von vorn auf Kopf und Schultern."""
    b.rotation_euler[2] = math.radians(drehung)
    bpy.context.view_layer.update()
    ziel = gelenk(b, "joint-head") + Vector((0, 0, hoehe_versatz))
    sc = bpy.context.scene
    cam = bpy.data.objects.get("Kamera")
    if cam is None:
        cam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
        sc.collection.objects.link(cam)
    cam.data.lens = brennweite
    cam.location = ziel + Vector((seitlich, -abstand, 0.03))
    richtung = ziel - cam.location
    cam.rotation_euler = richtung.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    return cam, ziel


def licht_portraet(ziel):
    """Hauptlicht von links oben vorn, schwaches Fülllicht rechts, Welt leicht hell."""
    sc = bpy.context.scene
    for o in [o for o in bpy.data.objects if o.type == "LIGHT"]:
        bpy.data.objects.remove(o, do_unlink=True)

    def licht(name, ort, energie, groesse, farbe=(1, 1, 1)):
        l = bpy.data.lights.new(name, "AREA")
        l.energy, l.size, l.color = energie, groesse, farbe
        o = bpy.data.objects.new(name, l)
        sc.collection.objects.link(o)
        o.location = ziel + Vector(ort)
        o.rotation_euler = (ziel - o.location).to_track_quat("-Z", "Y").to_euler()
        return o

    licht("Haupt", (-1.2, -1.3, 0.9), 220, 1.0, (1.0, 0.95, 0.88))
    licht("Fuell", (1.4, -1.0, 0.1), 45, 2.0, (0.85, 0.9, 1.0))
    licht("Kante", (0.6, 1.2, 0.8), 80, 0.8)
    if sc.world is None:
        sc.world = bpy.data.worlds.new("Welt")
    sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.95, 0.93, 0.88, 1)
    bg.inputs[1].default_value = 0.35


def render_einstellen(breite=1024, hoehe=1024, engine="BLENDER_EEVEE"):
    sc = bpy.context.scene
    sc.render.engine = engine
    sc.render.resolution_x, sc.render.resolution_y = breite, hoehe
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "AgX"
    sc.render.image_settings.file_format = "PNG"
    if engine == "BLENDER_EEVEE":
        sc.eevee.taa_render_samples = 32


def rendern(pfad):
    sc = bpy.context.scene
    sc.render.filepath = pfad
    bpy.ops.render.render(write_still=True)


def argumente():
    """Argumente nach "--" als dict (--name=wert)."""
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    d = {}
    for x in a:
        if x.startswith("--"):
            k, _, v = x[2:].partition("=")
            d[k] = v if v else True
    return d


HAAR_REGLER = {
    "length": ("Set Hair Curve Profile", "Socket_1"), "density": ("Set Hair Curve Profile", "Socket_0"),
    "thickness": ("Set Hair Curve Profile", "Input_3"), "frizz": ("Frizz Hair Curves", "Input_3"),
    "clump": ("Clump Hair Curves", "Input_7"), "noise": ("Hair Curves Noise", "Input_3"),
    "curl": ("Curl Hair Curves", "Input_2"), "curl_radius": ("Curl Hair Curves", "Input_7"),
}


def haareditor_einstellen(obj, werte=None, farbe1=None, farbe2=None, dunkle_wurzel=None):
    """Regler (Länge, Dichte …) und Farben eines Haar-Editor-Objekts setzen."""
    for k, v in (werte or {}).items():
        mod, ident = HAAR_REGLER[k]
        ModifierService.set_node_group_input(obj.modifiers[mod], ident, v)
        obj.modifiers[mod].node_group.interface_update(bpy.context)
    mat = obj.active_material
    for n in mat.node_tree.nodes:
        if getattr(n, "node_tree", None) and n.node_tree.name.startswith("Hair shader EEVEE"):
            if farbe1:
                n.inputs["Color 1"].default_value = (*farbe1, 1)
            if farbe2:
                n.inputs["Color 2"].default_value = (*farbe2, 1)
            if dunkle_wurzel is not None:
                n.inputs["Darken root"].default_value = dunkle_wurzel
    obj.update_tag()


def material_toenen(obj, farbe, staerke=1.0):
    """Grundfarbe aller Principled-Shader eines Objekts mit einer Farbe multiplizieren (Haar umfärben)."""
    for slot in obj.material_slots:
        mat = slot.material
        if mat is None or not mat.use_nodes:
            continue
        nt = mat.node_tree
        for p in [n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"]:
            eingang = p.inputs["Base Color"]
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.blend_type = "MULTIPLY"
            mix.inputs["Factor"].default_value = staerke
            mix.inputs["B"].default_value = (*farbe, 1)
            if eingang.is_linked:
                nt.links.new(eingang.links[0].from_socket, mix.inputs["A"])
            else:
                mix.inputs["A"].default_value = eingang.default_value
            nt.links.new(mix.outputs["Result"], eingang)


def augenfarbe(b, gross, klein=None):
    """Prozedurale Augen: Irisfarben setzen."""
    for o in objekte(b):
        if o.type != "MESH" or not o.material_slots:
            continue
        for slot in o.material_slots:
            if slot.material is None or not slot.material.use_nodes:
                continue
            for n in slot.material.node_tree.nodes:
                if n.type == "GROUP" and "IrisMinorColor" in n.inputs:
                    n.inputs["IrisMajorColor"].default_value = (*gross, 1)
                    n.inputs["IrisMinorColor"].default_value = (*(klein or gross), 1)
                    n.inputs["IrisSection4Color"].default_value = (*[c * 0.5 for c in gross], 1)


def asset_objekt(b, typ):
    return ObjectService.find_object_of_type_amongst_nearest_relatives(b, typ)
