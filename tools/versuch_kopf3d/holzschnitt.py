# Versuch 3D-Köpfe: Holzschnitt-Darstellung (kolorierter Holzschnitt wie die Oberfläche).
# Zwei Renderdurchgänge mit Eevee und eine Zusammensetzung mit numpy:
#   Linien  – schwarze Umrisse (Freestyle), Schraffur im Schatten (Bildschirm-Schraffur nach Licht),
#             Haar und Bart als einzelne schwarze Striche (dichter im Schatten)
#   Farbe   – flächige, helle Wasserfarben je Material (zwei Töne, etwas Fleckigkeit)
#   Papier  – Farbe leicht versetzt und mit unruhigem Rand auf Papier, Linien darüber multipliziert
import bpy, math, numpy as np

PAPIER = (0.985, 0.975, 0.95)

# Standardfarben (sRGB) je Materialart; die DNA kann sie überschreiben
FARBEN = {
    "haut": (0.98, 0.88, 0.8), "lippen": (0.9, 0.62, 0.55), "haar": (0.62, 0.48, 0.32),
    "brauen": (0.45, 0.33, 0.22), "wimpern": (0.2, 0.15, 0.1), "zaehne": (0.98, 0.96, 0.9),
    "kleid": (0.78, 0.8, 0.72), "hemd": (0.95, 0.93, 0.86), "augen": None,
}


def _srgb_zu_linear(c):
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


def art_von(obj, mat):
    """Materialart aus Objekt- und Materialnamen."""
    n = (obj.name + " " + (mat.name if mat else "")).lower()
    if obj.type == "CURVES":
        return "bart" if ("beard" in n or "moustache" in n) else "haar_kurven"
    for schluessel, art in (("lips", "lippen"), ("eyebrow", "brauen"), ("eyelash", "wimpern"), ("teeth", "zaehne"),
                            ("high-poly", "augen"), ("low-poly", "augen"), ("tongue", "zaehne")):
        if schluessel in n:
            return art
    if obj.get("art"):
        return obj["art"]
    if "Human" == obj.name or ".body" in n or ".ears" in n or "nipple" in n or "nails" in n or "genitals" in n:
        return "haut"
    for kurz in ("short", "long", "bob", "hair", "braid", "ponytail", "afro"):
        if kurz in n:
            return "haar"
    return "kleid"


AUGEN_TEXTUR = {"datei": None}


def _bild_textur(mat):
    """Erste Bildtextur eines Materials (Haar-Alpha, Augen-Textur); prozedurale Augen bekommen die Augen-Textur."""
    if mat and ("high-poly" in mat.name or "low-poly" in mat.name) and AUGEN_TEXTUR["datei"]:
        return bpy.data.images.load(AUGEN_TEXTUR["datei"], check_existing=True)
    if mat and mat.use_nodes:
        bilder = [n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image]
        for b in bilder:
            if not any(w in b.name.lower() for w in ("normal", "bump", "spec", "rough", "ao")):
                return b
        return bilder[0] if bilder else None
    return None


def _mittelfarbe(bild):
    """Durchschnittsfarbe (linear) einer Textur, nur aus jedem 7. Pixel."""
    px = np.array(bild.pixels[:], dtype=np.float32).reshape(-1, 4)[::7]
    px = px[px[:, 3] > 0.5] if (px[:, 3] > 0.5).any() else px
    m = np.median(px[:, :3], axis=0)
    return [max(float(c), 1e-3) for c in m]


def _licht_knoten(nt, staerke=1.0):
    """Helligkeit des Hauptlichts als Wert 0..1 (Diffuse -> Shader to RGB)."""
    d = nt.nodes.new("ShaderNodeBsdfDiffuse")
    d.inputs["Color"].default_value = (1, 1, 1, 1)
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    nt.links.new(d.outputs[0], s2r.inputs[0])
    bw = nt.nodes.new("ShaderNodeRGBToBW")
    nt.links.new(s2r.outputs["Color"], bw.inputs[0])
    return bw.outputs[0]


def _math(nt, op, a, b=None, c=None, clamp=False):
    m = nt.nodes.new("ShaderNodeMath")
    m.operation = op
    m.use_clamp = clamp
    for i, v in enumerate((a, b, c)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            m.inputs[i].default_value = v
        else:
            nt.links.new(v, m.inputs[i])
    return m.outputs[0]


def _schraffur(nt, licht, winkel, abstand_px, schwelle, breite_max, aufloesung, rausch=None, objekt=None):
    """Parallele Linien; Linienbreite wächst, je dunkler es ist. Ergebnis 1 = Linie.
    Ohne objekt im Bildraum (Winkel), mit objekt=(richtung, abstand_m) als Ringe um die Form
    (Objektkoordinaten), so folgen die Striche der Rundung wie beim Schneiden mit dem Messer."""
    tc = nt.nodes.new("ShaderNodeTexCoord")
    if objekt is None:
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(tc.outputs["Window"], sep.inputs[0])
        w, h = aufloesung
        x = _math(nt, "MULTIPLY", sep.outputs[0], w * math.cos(winkel))
        y = _math(nt, "MULTIPLY", sep.outputs[1], h * math.sin(winkel))
        t = _math(nt, "ADD", x, y)
        if rausch is not None:
            t = _math(nt, "ADD", t, _math(nt, "MULTIPLY", rausch, abstand_px * 0.6))
        t = _math(nt, "DIVIDE", t, abstand_px)
    else:
        richtung, abstand_m = objekt
        dot = nt.nodes.new("ShaderNodeVectorMath")
        dot.operation = "DOT_PRODUCT"
        nt.links.new(tc.outputs["Object"], dot.inputs[0])
        laenge = math.sqrt(sum(c * c for c in richtung))
        dot.inputs[1].default_value = [c / laenge for c in richtung]
        t = _math(nt, "DIVIDE", dot.outputs["Value"], abstand_m)
        if rausch is not None:
            t = _math(nt, "ADD", t, _math(nt, "MULTIPLY", rausch, 0.5))
    fr = _math(nt, "FRACT", t)
    # Abstand zur Linienmitte (Linienmitte bei fr = 0 bzw. 1), in Streifenbreiten 0..0,5
    d = _math(nt, "SUBTRACT", 0.5, _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", fr, 0.5)))
    # Dunkelheit unter der Schwelle -> halbe Linienbreite; im Licht keine Linie
    dunkel = _math(nt, "DIVIDE", _math(nt, "SUBTRACT", schwelle, licht), schwelle, clamp=True)
    halb = _math(nt, "MULTIPLY", dunkel, breite_max * 0.5)
    # Kante etwa ein Pixel weich
    return _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", halb, d), abstand_px, clamp=True)


def _rauschen(nt, skala=6.0):
    tc = nt.nodes.new("ShaderNodeTexCoord")
    no = nt.nodes.new("ShaderNodeTexNoise")
    no.inputs["Scale"].default_value = skala
    no.inputs["Detail"].default_value = 2.0
    nt.links.new(tc.outputs["Object"], no.inputs["Vector"])
    return _math(nt, "SUBTRACT", no.outputs["Fac"], 0.5)


def material_linien(obj, mat, art, aufloesung):
    """Weiß mit schwarzer Schraffur im Schatten; Kurvenhaar als Striche."""
    m = bpy.data.materials.new(f"Linien.{obj.name}.{art}")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    aus = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(em.outputs[0], aus.inputs["Surface"])
    licht = _licht_knoten(nt)
    rausch = _rauschen(nt, 4.0)
    if art in ("bart", "haar_kurven"):
        # Je Haar ein Zufallswert: im Schatten mehr Haare schwarz, im Licht wenige
        info = nt.nodes.new("ShaderNodeHairInfo")
        schwarz = _math(nt, "LESS_THAN", info.outputs["Random"],
                        _math(nt, "ADD", 0.4, _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", 1.0, licht), 0.6)))
        dunkel = schwarz
    elif art == "haar":
        # Netz-Haar: Strähnen aus der Haartextur (dunkle Stellen) und Schraffur im Schatten
        dunkel = _math(nt, "MAXIMUM",
                       _schraffur(nt, licht, math.radians(60), 6.0, 0.6, 0.7, aufloesung, rausch),
                       _schraffur(nt, licht, math.radians(-30), 7.0, 0.25, 0.6, aufloesung, rausch))
        bild = _bild_textur(mat)
        if bild:
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = bild
            bw = nt.nodes.new("ShaderNodeRGBToBW")
            nt.links.new(tex.outputs["Color"], bw.inputs[0])
            straehne = _math(nt, "LESS_THAN", bw.outputs[0], 0.2)
            dunkel = _math(nt, "MAXIMUM", dunkel, _math(nt, "MULTIPLY", straehne, 0.85))
            alpha = tex.outputs["Alpha"]
            _alpha_schnitt(nt, m, em, aus, alpha)
    elif art in ("brauen", "wimpern"):
        bild = _bild_textur(mat)
        dunkel = _math(nt, "ADD", 0.0, 1.0)  # durchgehend schwarz
        if bild:
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = bild
            _alpha_schnitt(nt, m, em, aus, _math(nt, "GREATER_THAN", tex.outputs["Alpha"], 0.45 if art == "brauen" else 0.3))
            if art == "brauen":
                # Brauen als Strichbündel: Textur-Helligkeit unter der Schwelle wird Strich
                bw = nt.nodes.new("ShaderNodeRGBToBW")
                nt.links.new(tex.outputs["Color"], bw.inputs[0])
                dunkel = _math(nt, "LESS_THAN", bw.outputs[0], 0.35)
    elif art == "augen":
        bild = _bild_textur(mat)
        dunkel = _math(nt, "ADD", 0.0, 0.0)
        if bild:
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = bild
            bw = nt.nodes.new("ShaderNodeRGBToBW")
            nt.links.new(tex.outputs["Color"], bw.inputs[0])
            # Pupille schwarz, Rand der Iris als Ring, Iris locker schraffiert
            lum = bw.outputs[0]
            # Werte linear (Augen-Textur: Lederhaut ~0,35, Iris ~0,015, Pupille 0, Übergang 0,05–0,3)
            pupille = _math(nt, "LESS_THAN", lum, 0.004)
            ring = _math(nt, "MULTIPLY", _math(nt, "GREATER_THAN", lum, 0.04), _math(nt, "LESS_THAN", lum, 0.26))
            iris = _math(nt, "LESS_THAN", lum, 0.04)
            locker = _schraffur(nt, _math(nt, "ADD", 0.0, 0.0), math.radians(70), 4.0, 0.5, 0.35, aufloesung)
            dunkel = _math(nt, "MAXIMUM", _math(nt, "MAXIMUM", pupille, ring), _math(nt, "MULTIPLY", iris, locker))
    else:
        # erste Lage folgt der Form (leicht schräge Ringe), zweite Lage kreuzt im Bildraum
        s1 = _schraffur(nt, licht, 0, 7.0, 0.45, 0.65, aufloesung, rausch, objekt=((0.15, 0.25, 1.0), 0.0026))
        s2 = _schraffur(nt, licht, math.radians(-35), 8.0, 0.2, 0.55, aufloesung, rausch)
        dunkel = _math(nt, "MAXIMUM", s1, s2)
        if art == "haut":
            # Kopfhaut unter dem Kurvenhaar: dichter schraffiert, damit Lücken wie Haar wirken
            at = nt.nodes.new("ShaderNodeAttribute")
            at.attribute_name = "haarflaeche"
            hs = _schraffur(nt, _math(nt, "MULTIPLY", licht, 0.8), math.radians(70), 5.0, 0.9, 0.6, aufloesung, rausch)
            dunkel = _math(nt, "MAXIMUM", dunkel, _math(nt, "MULTIPLY", hs, _math(nt, "GREATER_THAN", at.outputs["Fac"], 0.3)))
    weiss = _math(nt, "SUBTRACT", 1.0, dunkel, clamp=True)
    cmb = nt.nodes.new("ShaderNodeCombineColor")
    for i in range(3):
        nt.links.new(weiss, cmb.inputs[i])
    nt.links.new(cmb.outputs[0], em.inputs["Color"])
    return m


def _alpha_schnitt(nt, m, em, aus, alpha):
    """Transparente Stellen (Haarkarten, Brauen) durchsichtig machen."""
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(alpha, mix.inputs["Fac"])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], aus.inputs["Surface"])
    m.surface_render_method = "DITHERED"


def material_farbe(obj, mat, art, farben):
    """Flächige Wasserfarbe: Grundton im Licht, etwas dunklerer Ton im Schatten, leicht fleckig."""
    m = bpy.data.materials.new(f"Farbe.{obj.name}.{art}")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    aus = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(em.outputs[0], aus.inputs["Surface"])
    schluessel = {"bart": "haar", "haar_kurven": "haar"}.get(art, art)
    grund = farben.get(obj.get("farbe_schluessel", schluessel)) or farben.get(schluessel)
    licht = _licht_knoten(nt)
    rausch = _rauschen(nt, 3.0)
    if art == "augen" and _bild_textur(mat):
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = _bild_textur(mat)
        farbe = tex.outputs["Color"]
    else:
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = (*_srgb_zu_linear(grund), 1)
        farbe = rgb.outputs[0]
        if art == "haut":
            at = nt.nodes.new("ShaderNodeAttribute")
            at.attribute_name = "haarflaeche"
            haar = nt.nodes.new("ShaderNodeRGB")
            haar.outputs[0].default_value = (*_srgb_zu_linear(farben["haar"]), 1)
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            nt.links.new(_math(nt, "GREATER_THAN", at.outputs["Fac"], 0.3), mix.inputs["Factor"])
            nt.links.new(farbe, mix.inputs["A"])
            nt.links.new(haar.outputs[0], mix.inputs["B"])
            farbe = mix.outputs["Result"]
            hb = _bild_textur(mat)
            if hb:
                # natürliche Rötungen (Wangen, Nase, Lippen) aus der Hauttextur: Abweichung vom Mittelwert
                tex = nt.nodes.new("ShaderNodeTexImage")
                tex.image = hb
                mittel = _mittelfarbe(hb)
                teil = nt.nodes.new("ShaderNodeVectorMath")
                teil.operation = "DIVIDE"
                nt.links.new(tex.outputs["Color"], teil.inputs[0])
                teil.inputs[1].default_value = mittel
                tint = nt.nodes.new("ShaderNodeMix")
                tint.data_type = "RGBA"
                tint.blend_type = "MULTIPLY"
                tint.inputs["Factor"].default_value = 0.55
                nt.links.new(farbe, tint.inputs["A"])
                nt.links.new(teil.outputs[0], tint.inputs["B"])
                farbe = tint.outputs["Result"]
    # zwei Töne: Schatten 18 % dunkler, Übergang weich; Flecken ±6 %
    schatten = _math(nt, "SMOOTH_MIN", _math(nt, "MULTIPLY", licht, 2.2), 1.0, 0.3)
    ton = _math(nt, "ADD", _math(nt, "ADD", 0.80, _math(nt, "MULTIPLY", schatten, 0.2)), _math(nt, "MULTIPLY", rausch, 0.12))
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    nt.links.new(farbe, mul.inputs["A"])
    cmb = nt.nodes.new("ShaderNodeCombineColor")
    for i in range(3):
        nt.links.new(ton, cmb.inputs[i])
    nt.links.new(cmb.outputs[0], mul.inputs["B"])
    nt.links.new(mul.outputs["Result"], em.inputs["Color"])
    bild = _bild_textur(mat)
    if art in ("haar", "brauen", "wimpern") and bild:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bild
        _alpha_schnitt(nt, m, em, aus, _math(nt, "GREATER_THAN", tex.outputs["Alpha"], 0.3))
    if art == "wimpern":
        # Wimpern nur als Linie, keine Farbe
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        nt.links.new(tr.outputs[0], aus.inputs["Surface"])
    return m


def materialien_tauschen(objekte, macher):
    """Materialien aller Objekte durch macher(obj, mat, art) ersetzen; gibt Rücksicherung zurück."""
    alt = {}
    for o in objekte:
        if o.type not in ("MESH", "CURVES"):
            continue
        alt[o.name] = [s.material for s in o.material_slots]
        for i, s in enumerate(o.material_slots):
            art = art_von(o, s.material)
            neu = macher(o, s.material, art)
            if s.link == "OBJECT":
                s.material = neu
            else:
                o.data.materials[i] = neu
    return alt


def freestyle_einrichten(dicke=2.2):
    sc = bpy.context.scene
    sc.render.use_freestyle = True
    sc.render.line_thickness_mode = "ABSOLUTE"
    sc.render.line_thickness = 1.0
    vl = bpy.context.view_layer
    vl.use_freestyle = True
    fs = vl.freestyle_settings
    fs.crease_angle = math.radians(118)
    fs.use_smoothness = True
    for ls in list(fs.linesets):
        fs.linesets.remove(ls)
    ls = fs.linesets.new("Umriss")
    ls.select_by_visibility = True
    ls.visibility = "VISIBLE"
    ls.select_silhouette = True
    ls.select_border = True
    ls.select_crease = True
    ls.select_contour = True
    ls.select_external_contour = True
    st = ls.linestyle
    st.color = (0, 0, 0)
    st.thickness = dicke
    st.thickness_position = "CENTER"
    st.caps = "ROUND"
    st.chaining = "PLAIN"
    # Strichstärke schwankt wie mit der Feder/dem Messer
    mod = st.thickness_modifiers.new("Rauschen", "NOISE")
    mod.amplitude = dicke * 0.6
    mod.period = 30
    mod.influence = 1.0
    ge = st.geometry_modifiers.new("Zittern", "PERLIN_NOISE_1D")
    ge.frequency = 6
    ge.amplitude = 1.2
    # Außenkontur kräftiger
    ls2 = fs.linesets.new("Aussen")
    ls2.select_by_visibility = True
    ls2.select_by_edge_types = True
    for k in ("silhouette", "border", "crease", "contour"):
        setattr(ls2, "select_" + k, False)
    ls2.select_external_contour = True
    ls2.linestyle.color = (0, 0, 0)
    ls2.linestyle.thickness = dicke * 1.6
    ls2.linestyle.caps = "ROUND"


def holzlicht(an=True):
    """Für beide Holzschnitt-Durchgänge: ein Sonnenlicht von links oben vorn statt der Porträtlichter."""
    for o in bpy.data.objects:
        if o.type == "LIGHT" and o.name != "HolzSonne":
            o.hide_render = an
    sonne = bpy.data.objects.get("HolzSonne")
    if sonne is None:
        l = bpy.data.lights.new("HolzSonne", "SUN")
        l.energy = 3.2
        l.angle = math.radians(8)
        sonne = bpy.data.objects.new("HolzSonne", l)
        bpy.context.scene.collection.objects.link(sonne)
        # Licht kommt von links (Bild), oben, vorn
        sonne.rotation_euler = (math.radians(50), 0, math.radians(-45))
    sonne.hide_render = not an


def durchgang_linien(objekte, aus, aufloesung):
    sc = bpy.context.scene
    holzlicht(True)
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.render.film_transparent = False
    welt_weiss()
    freestyle_einrichten(dicke=aufloesung[0] / 380)
    alt = materialien_tauschen(objekte, lambda o, m, art: material_linien(o, m, art, aufloesung))
    sc.render.filepath = aus
    bpy.ops.render.render(write_still=True)
    return alt


def durchgang_farbe(objekte, aus, farben, alt):
    sc = bpy.context.scene
    sc.render.use_freestyle = False
    sc.render.film_transparent = True
    sc.view_settings.view_transform = "Standard"
    # zurück auf die Originalmaterialien (Texturen), dann Farbmaterialien
    zuruecksetzen(objekte, alt)
    materialien_tauschen(objekte, lambda o, m, art: material_farbe(o, m, art, farben))
    sc.render.filepath = aus
    bpy.ops.render.render(write_still=True)


def zuruecksetzen(objekte, alt):
    for o in objekte:
        if o.name in alt:
            for i, mat in enumerate(alt[o.name]):
                if o.material_slots[i].link == "OBJECT":
                    o.material_slots[i].material = mat
                else:
                    o.data.materials[i] = mat


def welt_weiss(umgebung=0.15):
    """Weißer Hintergrund für die Kamera, aber nur schwaches Umgebungslicht (sonst keine Schatten)."""
    sc = bpy.context.scene
    welt = bpy.data.worlds.new("Papierwelt")
    welt.use_nodes = True
    nt = welt.node_tree
    nt.nodes.clear()
    aus = nt.nodes.new("ShaderNodeOutputWorld")
    lp = nt.nodes.new("ShaderNodeLightPath")
    mix = nt.nodes.new("ShaderNodeMixShader")
    licht = nt.nodes.new("ShaderNodeBackground")
    licht.inputs[1].default_value = umgebung
    weiss = nt.nodes.new("ShaderNodeBackground")
    weiss.inputs[1].default_value = 1.0
    nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs["Fac"])
    nt.links.new(licht.outputs[0], mix.inputs[1])
    nt.links.new(weiss.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], aus.inputs["Surface"])
    sc.world = welt


# ---------- Zusammensetzen mit numpy ----------

def _laden(pfad):
    img = bpy.data.images.load(pfad)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return px


def _speichern(px, pfad):
    h, w = px.shape[:2]
    img = bpy.data.images.new("aus", w, h, alpha=True)
    img.pixels[:] = px.astype(np.float32).ravel()
    img.filepath_raw = pfad
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def _weich(a, r):
    """Kastenunschärfe mit Radius r (mehrfach = fast Gauß)."""
    for _ in range(2):
        k = 2 * r + 1
        c = np.cumsum(np.pad(a, ((r + 1, r), (0, 0)), mode="edge"), axis=0)
        a = (c[k:] - c[:-k]) / k
        c = np.cumsum(np.pad(a, ((0, 0), (r + 1, r)), mode="edge"), axis=1)
        a = (c[:, k:] - c[:, :-k]) / k
    return a


def _rausch_feld(h, w, skala, rng):
    """Glattes Rauschen 0..1 (zufälliges Gitter, hochskaliert und geglättet)."""
    gh, gw = max(2, h // skala), max(2, w // skala)
    g = rng.random((gh, gw)).astype(np.float32)
    ys = (np.arange(h) * gh / h).astype(int)
    xs = (np.arange(w) * gw / w).astype(int)
    return _weich(g[ys][:, xs], max(1, skala // 2))


def _schieben(a, dx, dy):
    """Bild verschieben, frei werdender Rand leer (kein Umlauf)."""
    out = np.zeros_like(a)
    h, w = a.shape[:2]
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = a[ys, xs]
    return out


def zusammensetzen(linien_pfad, farbe_pfad, aus_pfad, versatz=(5, -4), seed=3, staerke_farbe=0.75):
    rng = np.random.default_rng(seed)
    L = _laden(linien_pfad)[..., :3].mean(axis=2)
    L = np.clip(L / max(np.percentile(L, 99.5), 1e-3), 0, 1)
    F = _laden(farbe_pfad)
    h, w = L.shape
    # Papier: warmes Weiß mit feiner Körnung und leichten Wolken
    papier = np.empty((h, w, 3), np.float32)
    papier[:] = PAPIER
    korn = rng.normal(0, 0.012, (h, w)).astype(np.float32)
    wolke = (_rausch_feld(h, w, 64, rng) - 0.5) * 0.04
    papier *= (1 + korn + wolke)[..., None]
    # Farbe: versetzt (Blender-Bilder beginnen unten: y nach oben), Rand unruhig, leicht verlaufen
    dx, dy = versatz
    F = _schieben(F, dx, dy)
    a = F[..., 3]
    rand = _rausch_feld(h, w, 12, rng)
    a = np.clip((_weich(a, 2) - 0.25 - (rand - 0.5) * 0.5) * 2.5, 0, 1)
    farbe = F[..., :3] / np.maximum(F[..., 3:4], 1e-3)
    farbe = np.clip(farbe, 0, 1)
    # Wasserfarbe: hell, Pigment am Rand etwas dichter
    kante = np.clip(a - _weich(a, 4), 0, 1) * 1.6
    deck = np.clip(a * staerke_farbe * (0.85 + 0.3 * _rausch_feld(h, w, 40, rng)) + kante * 0.25, 0, 1)
    bild = papier * (1 - deck[..., None] + deck[..., None] * farbe)
    # Linien: Druckfarbe nicht ganz schwarz, leicht ausgefranst
    tinte = np.clip(L + rng.normal(0, 0.04, (h, w)).astype(np.float32), 0, 1)
    bild *= (0.1 + 0.9 * tinte)[..., None]
    out = np.ones((h, w, 4), np.float32)
    out[..., :3] = np.clip(bild, 0, 1)
    _speichern(out, aus_pfad)
