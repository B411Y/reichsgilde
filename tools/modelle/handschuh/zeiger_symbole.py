"""Zeiger-Symbole des 3D-Handschuhs: Windrose (ueber verschiebbaren Flaechen wie dem Stammbaum, am Platz des Fragezeichens) und Messing-Textcursor
in Form eines roemischen "I" (ueber Textfeldern, statt der Hand). Komplett selbst gebaut (Blender, kein KI-Inhalt, kein Fremdmodell).

Aufruf (Blender -b, Pfade absolut; tools/modelle/handschuh/lauf.ps1 setzt Idle-Prioritaet):
  blender -b --factory-startup --python zeiger_symbole.py -- <tuerschloss_zeiger.glb> <ausordner>
Schreibt <ausordner>/zeiger_symbole.glb (danach nach game/assets/modelle/zeiger/ kopieren) und gibt die Dreieckszahlen aus (Zeile DREIECKE).

Material: Messing und Umriss werden aus tuerschloss_zeiger.glb uebernommen (dieselben Texturen: Messing mit dunklen Patina-Flecken, Normalkarte),
die Umriss-Huelle ist eine invertierte Huelle wie beim Schloss (Flaechen nach innen, im Spiel ohne Licht mit Rueckseiten-Ausblendung).

Koordinaten (Blender, Meter, Z hoch; der glTF-Export macht daraus +Y hoch):
  Windrose: flach in der XY-Ebene, Dicke in Z (wie die Hilfe_Marke "?" der Hand, deren Transform sie im Spiel uebernimmt; dort kommt MarkeFaktor 1,9 dazu).
            Vier gleiche Spitzen auf +X, +Y, -X, -Y (N/O/S/W), jede mit Grat (zwei Flanken: hell/dunkel im Licht), kleiner Ring um die Mitte.
  Text_I:   aufrecht, Hoehe in Z, Breite in X, Dicke in Y (im Spiel haengt es an der Kamera und steht zum Betrachter). Ursprung = Mitte des Balkens = Zeigerpunkt.
"""
import bpy, bmesh, math, os, sys, json
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
schloss_glb, aus = args[0], args[1]
os.makedirs(aus, exist_ok=True)

# Umriss-Dicke: wie Schloss und Handschuh 3,6 mm im Bild; die Windrose wird im Spiel 1,9-fach gezeigt (MarkeFaktor), darum dort 3,6 / 1,9.
UMRISS = 0.0036
UMRISS_ROSE = UMRISS / 1.9

# Windrose (vor MarkeFaktor): Spitzenradius, Innenecken auf den Diagonalen, Grathoehe in der Mitte, Ring
ROSE_R, ROSE_B, ROSE_H = 0.034, 0.0085, 0.008
RING_R, RING_D, RING_SEG, RING_QUER = 0.0125, 0.0022, 20, 8

# Roemisches I: Hoehe 0,11 m (etwa eine halbe Zeigergroesse: Sichtfeld 0,45 m = zwei Zeigergroessen), schmaler Balken (halbe Breite), waagerechte Serifen mit kleiner Kehle
I_H, I_BALKEN, I_SERIFE, I_SERIFE_AUSSEN, I_SERIFE_INNEN, I_KEHLE, I_DICKE, I_FASE = 0.11, 0.0055, 0.021, 0.0045, 0.0065, 0.0075, 0.004, 0.0013

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

# --------------------------------------------------------------------------- Material aus dem Tuerschloss
bpy.ops.import_scene.gltf(filepath=schloss_glb)
m_messing = bpy.data.materials.get("Messing")
m_umriss = bpy.data.materials.get("Umriss")
if m_messing is None or m_umriss is None:
    raise SystemExit("Messing oder Umriss fehlt in " + schloss_glb)
m_umriss.use_backface_culling = True


def messing_aufhellen(mat, ziel):
    """Die Symbole liegen vor dem dunklen Leder der Hand (das Schloss daneben nicht): dieselbe Messing-Textur des Schlosses mit ihren dunklen Patina-Flecken,
    je Farbkanal linear so verstaerkt, dass das Mittel bei `ziel` (linear) liegt; die Flecken bleiben im selben Verhaeltnis dunkler. Normalkarte unveraendert."""
    import numpy as np
    knoten = next(n for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image is not None and n.image.colorspace_settings.name != "Non-Color")
    alt = knoten.image
    w, h = alt.size
    px = np.empty(w * h * 4, dtype=np.float32); alt.pixels.foreach_get(px); px = px.reshape(h, w, 4)
    lin = np.where(px[..., :3] <= 0.04045, px[..., :3] / 12.92, ((px[..., :3] + 0.055) / 1.055) ** 2.4)
    lin = np.clip(lin * (np.array(ziel) / lin.reshape(-1, 3).mean(0)), 0, 1)
    srgb = np.where(lin <= 0.0031308, lin * 12.92, 1.055 * lin ** (1 / 2.4) - 0.055)
    pfad = os.path.join(aus, "Messing_farbe.png")
    neu = bpy.data.images.new("Messing_farbe_roh", w, h, alpha=False)
    out = np.ones((h, w, 4), dtype=np.float32); out[..., :3] = srgb
    neu.pixels.foreach_set(out.reshape(-1)); neu.filepath_raw = pfad; neu.file_format = "PNG"; neu.save(); bpy.data.images.remove(neu)
    neu = bpy.data.images.load(pfad); neu.name = "Messing_farbe"; neu.pack()   # erst als PNG schreiben und laden (sonst exportiert glTF schwarz)
    alt.name = "Messing_farbe_Schloss"
    knoten.image = neu
    print("MESSING Mittel vorher (linear)", [round(float(x), 3) for x in (np.where(px[..., :3] <= 0.04045, px[..., :3] / 12.92, ((px[..., :3] + 0.055) / 1.055) ** 2.4)).reshape(-1, 3).mean(0)], "nachher", ziel, flush=True)


MESSING_ZIEL = (0.50, 0.33, 0.10)   # etwas dunkler als das Fragezeichen der Hand (0,78/0,52/0,17, ohne Textur)
messing_aufhellen(m_messing, MESSING_ZIEL)
for ob in list(bpy.data.objects):
    bpy.data.objects.remove(ob, do_unlink=True)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)


# --------------------------------------------------------------------------- Hilfen (wie tuerschloss_bau.py)
def uv_dreiachsig(bm, dichte=22.0):
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal; a = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            c = l.vert.co
            u, v = ((c.y, c.z), (c.x, c.z), (c.x, c.y))[a]
            l[uv].uv = (u * dichte, v * dichte)


def objekt(name, bm, mat):
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    uv_dreiachsig(bm)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob)
    return ob


def umriss_bauen(quelle, name, abstand):
    """Invertierte Huelle: Ecken entlang der gemittelten Normalen nach aussen, Flaechen umgedreht, Material Umriss."""
    me = quelle.data.copy(); me.name = name
    pos = {}
    for v in me.vertices:
        k = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5)); pos.setdefault(k, Vector((0, 0, 0))); pos[k] += v.normal
    for v in me.vertices:
        k = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5)); d = pos[k].normalized() if pos[k].length > 1e-9 else Vector((0, 0, 0)); v.co = v.co + d * abstand
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.reverse_faces(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    me.materials.clear(); me.materials.append(m_umriss)
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); ob.visible_shadow = False
    return ob


# --------------------------------------------------------------------------- Windrose
bm = bmesh.new()
oben = bm.verts.new((0, 0, ROSE_H)); unten = bm.verts.new((0, 0, -ROSE_H))
spitzen = [bm.verts.new((ROSE_R * math.cos(k * math.pi / 2), ROSE_R * math.sin(k * math.pi / 2), 0)) for k in range(4)]
ecken = [bm.verts.new((ROSE_B * math.cos(k * math.pi / 2 + math.pi / 4), ROSE_B * math.sin(k * math.pi / 2 + math.pi / 4), 0)) for k in range(4)]
for k in range(4):
    t, links, rechts = spitzen[k], ecken[k], ecken[k - 1]   # Ecke k liegt links (gegen den Uhrzeigersinn) der Spitze k
    bm.faces.new((t, links, oben)); bm.faces.new((t, oben, rechts))
    bm.faces.new((t, unten, links)); bm.faces.new((t, rechts, unten))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
ring = bmesh.new()
for i in range(RING_SEG):
    for j in range(RING_QUER):
        a, b = 2 * math.pi * i / RING_SEG, 2 * math.pi * j / RING_QUER
        r = RING_R + RING_D * math.cos(b)
        ring.verts.new((r * math.cos(a), r * math.sin(a), RING_D * math.sin(b)))
ring.verts.ensure_lookup_table()
for i in range(RING_SEG):
    for j in range(RING_QUER):
        v = lambda ii, jj: ring.verts[(ii % RING_SEG) * RING_QUER + (jj % RING_QUER)]
        ring.faces.new((v(i, j), v(i + 1, j), v(i + 1, j + 1), v(i, j + 1)))
bmesh.ops.recalc_face_normals(ring, faces=ring.faces)
rose_stern = objekt("Windrose_Stern", bm, m_messing)
rose_ring = objekt("Windrose_Ring", ring, m_messing)
um_stern = umriss_bauen(rose_stern, "Windrose_Umriss_Stern", UMRISS_ROSE)
um_ring = umriss_bauen(rose_ring, "Windrose_Umriss_Ring", UMRISS_ROSE)


def vereinigen(teile, name):
    bpy.ops.object.select_all(action="DESELECT")
    for t in teile: t.select_set(True)
    bpy.context.view_layer.objects.active = teile[0]
    bpy.ops.object.join()
    teile[0].name = name; teile[0].data.name = name
    return teile[0]


rose = vereinigen([rose_stern, rose_ring], "Windrose")
rose_umriss = vereinigen([um_stern, um_ring], "Windrose_Umriss")

# --------------------------------------------------------------------------- Roemisches I
h2 = I_H / 2
rechts = [  # rechte Haelfte des Umrisses (X, Z), unten nach oben; Kehle als Viertelbogen zwischen Serife und Balken
    (I_SERIFE, -h2), (I_SERIFE, -h2 + I_SERIFE_AUSSEN), (I_BALKEN + I_KEHLE, -h2 + I_SERIFE_INNEN)]
for s in range(1, 4):
    w = s / 4 * math.pi / 2
    rechts.append((I_BALKEN + I_KEHLE - I_KEHLE * math.sin(w), -h2 + I_SERIFE_INNEN + I_KEHLE * (1 - math.cos(w))))
rechts.append((I_BALKEN, -h2 + I_SERIFE_INNEN + I_KEHLE))
oben_r = [(x, -z) for (x, z) in reversed(rechts)]
umriss = rechts + oben_r + [(-x, z) for (x, z) in reversed(rechts + oben_r)]
bm = bmesh.new()
vorn = bm.faces.new([bm.verts.new((x, -I_DICKE, z)) for (x, z) in umriss])
erg = bmesh.ops.extrude_face_region(bm, geom=[vorn])
neu = [g for g in erg["geom"] if isinstance(g, bmesh.types.BMVert)]
bmesh.ops.translate(bm, verts=neu, vec=(0, 2 * I_DICKE, 0))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
for kappe in [f for f in bm.faces if abs(f.normal.y) > 0.99]:
    bmesh.ops.inset_region(bm, faces=[kappe], thickness=I_FASE, depth=I_FASE)   # angefaste Kante: das Messing glaenzt am Rand
text_i = objekt("Text_I", bm, m_messing)
text_i_umriss = umriss_bauen(text_i, "Text_I_Umriss", UMRISS)

# --------------------------------------------------------------------------- Ausgabe
dreiecke = {o.name: len(o.data.polygons) for o in (rose, rose_umriss, text_i, text_i_umriss)}
print("DREIECKE", json.dumps(dreiecke), "Windrose", dreiecke["Windrose"] + dreiecke["Windrose_Umriss"], "Text_I", dreiecke["Text_I"] + dreiecke["Text_I_Umriss"], flush=True)
for o in (rose, text_i):
    lo = [min(v.co[i] for v in o.data.vertices) for i in range(3)]; hi = [max(v.co[i] for v in o.data.vertices) for i in range(3)]
    print("MASSE", o.name, [round(x, 4) for x in lo], [round(x, 4) for x in hi], flush=True)
bpy.ops.object.select_all(action="DESELECT")
for o in (rose, rose_umriss, text_i, text_i_umriss): o.select_set(True)
bpy.ops.export_scene.gltf(filepath=os.path.join(aus, "zeiger_symbole.glb"), export_format="GLB", use_selection=True, export_animations=False,
                          export_apply=False, export_cameras=False, export_lights=False)
print("GLB geschrieben", os.path.join(aus, "zeiger_symbole.glb"), flush=True)
