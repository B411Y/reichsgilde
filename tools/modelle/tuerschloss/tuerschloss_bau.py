"""Tuerschloss mit Klinke (Mauszeiger im 3D-Modus ueber betretbaren Gebaeuden), komplett selbst gebaut (Blender, kein KI-Inhalt).

Aufruf (Blender -b, Pfade absolut):
  blender -b --factory-startup --python tuerschloss_bau.py -- <ausordner> [umriss_dicke]
Schreibt <ausordner>/tuerschloss_zeiger.glb, tuerschloss.blend, tuerschloss_masse.json und Standbilder.

Vorlage: 2D-Zeiger game/assets/oberflaeche/zeiger/tuer_schloss_*.png (Langschild mit Lilienenden, Schluesselloch unten, S-foermige Klinke nach rechts).
Koordinaten (Blender, Meter, Z hoch; in der glb wird daraus glTF +Y hoch):
  Ursprung = Mitte der Unterseite der Beschlagplatte (Anker_Schloss liegt 5 mm darueber), Plattenflaeche +Z (zum Betrachter), Platte in der XY-Ebene, 16 cm hoch.
  Welle (Drehpunkt der Klinke) bei (0, +0.032, 0), Achse +Z; Schluesselloch bei (0, -0.016).
  Klinke in Ruhe nach +X (rechts), S-foermig in der Plattenebene (Griffstange ca. 10 cm, Ende nach oben eingerollt), Achse 4,5 cm ueber der Platte.
  Druecken = Drehung des Knotens "Klinke" um +Z um -28 Grad (im Uhrzeigersinn von vorn gesehen, die Spitze geht nach unten).
Aktionen: Druecken (20 Bilder, 24/s: 16 Bilder druecken, 4 halten), Loslassen (10 Bilder: 4 halten, dann zurueck). Ruhe = Grundstellung des Knotens Klinke (Drehung 0), keine Aktion.
"""
import bpy, bmesh, math, os, sys, json
import numpy as np
from mathutils import Vector, Matrix

HIER = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--") + 1:]
aus = args[0]
UMRISS = float(args[1]) if len(args) > 1 else 0.0036    # gleiche Dicke wie beim Handschuh
os.makedirs(aus, exist_ok=True)
FPS = 24
WINKEL = 28.0
N_DRUECKEN, N_HALTEN, N_LOS = 16, 4, 12
PLATTE_B, PLATTE_H, PLATTE_D, ECKE = 0.056, 0.16, 0.005, 0.014
WELLE = Vector((0.0, 0.032, 0.0))
STAND = 0.045                    # Hoehe der Griffstangenachse ueber der Platte
SCHLUESSEL = Vector((0.0, -0.016, 0.0))

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = FPS

# --------------------------------------------------------------------------- Texturen (Eisen: Hammerschlag/Rost, Messing: matt, gebuerstet)
def fbm(n, rng, grund=4, stufen=5, gew=0.55):
    out = np.zeros((n, n)); amp = 1.0; z = grund
    for _ in range(stufen):
        g = rng.random((z + 1, z + 1)); g[-1, :] = g[0, :]; g[:, -1] = g[:, 0]
        xs = np.linspace(0, z, n, endpoint=False); i = xs.astype(int); f = xs - i; f = f * f * (3 - 2 * f)
        a = g[i][:, i] * (1 - f)[None, :] * (1 - f)[:, None] + g[i + 1][:, i] * f[:, None] * (1 - f)[None, :]
        a += g[i][:, i + 1] * (1 - f)[:, None] * f[None, :] + g[i + 1][:, i + 1] * f[:, None] * f[None, :]
        out += amp * a; amp *= gew; z *= 2
    return out / out.max()

def textur(name, basis, var, rauh, seed, n=256):
    rng = np.random.default_rng(seed)
    h = fbm(n, rng, 4, 5) * 0.7 + fbm(n, rng, 16, 3) * 0.3
    fl = fbm(n, rng, 6, 4)
    alb = np.zeros((n, n, 3))
    for c in range(3):
        alb[..., c] = basis[c] * (1.0 - var + 2 * var * h)
    fleck = np.clip((fl - 0.62) * 4, 0, 1)                  # dunkle Flecken (Oxid, Russ)
    alb *= (1 - 0.30 * fleck)[..., None]
    gx = np.roll(h, -1, 1) - np.roll(h, 1, 1); gy = np.roll(h, -1, 0) - np.roll(h, 1, 0)
    nm = np.stack([-gx * rauh, -gy * rauh, np.ones_like(h)], -1); nm /= np.linalg.norm(nm, axis=-1, keepdims=True)
    def bild(nm_, arr, linear):
        # erst als PNG schreiben und wieder laden: der glTF-Export holt die Pixel sonst schwarz aus dem erzeugten Bild
        os.makedirs(os.path.join(aus, "tex"), exist_ok=True)
        pfad = os.path.join(aus, "tex", nm_ + ".png")
        im = bpy.data.images.new(nm_ + "_roh", n, n, alpha=False)
        rgba = np.ones((n, n, 4)); rgba[..., :3] = np.clip(arr, 0, 1)
        im.pixels = rgba[::-1].reshape(-1).tolist()
        im.filepath_raw = pfad; im.file_format = "PNG"; im.save()
        bpy.data.images.remove(im)
        im = bpy.data.images.load(pfad); im.colorspace_settings.name = "Non-Color" if linear else "sRGB"; im.pack(); return im
    return bild(name + "_farbe", alb, False), bild(name + "_normal", nm * 0.5 + 0.5, True)

def stoff(name, basis, var, metall, rau, rauh, seed):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Metallic"].default_value = metall; b.inputs["Roughness"].default_value = rau
    fi, ni = textur(name, basis, var, rauh, seed)
    t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = fi; m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    tn = m.node_tree.nodes.new("ShaderNodeTexImage"); tn.image = ni
    nn = m.node_tree.nodes.new("ShaderNodeNormalMap"); nn.inputs["Strength"].default_value = 0.8
    m.node_tree.links.new(tn.outputs["Color"], nn.inputs["Color"]); m.node_tree.links.new(nn.outputs["Normal"], b.inputs["Normal"])
    return m
m_dunkel = bpy.data.materials.new("Dunkel"); m_dunkel.use_nodes = True
m_dunkel.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.02, 0.015, 0.012, 1); m_dunkel.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
m_messing = stoff("Messing", (0.40, 0.27, 0.065), 0.22, 0.30, 0.45, 0.8, 12)
m_umriss = bpy.data.materials.new("Umriss"); m_umriss.use_nodes = True
bu = m_umriss.node_tree.nodes["Principled BSDF"]; bu.inputs["Base Color"].default_value = (0.02, 0.012, 0.008, 1); bu.inputs["Roughness"].default_value = 1.0
m_umriss.use_backface_culling = True

# --------------------------------------------------------------------------- Hilfen
def objekt(name, me, mat=None, eltern=None):
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob)
    if mat: me.materials.append(mat)
    if eltern: ob.parent = eltern
    return ob

def glatt_schattieren(ob):
    for p in ob.data.polygons: p.use_smooth = True

def uv_dreiachsig(bm, dichte=22.0):
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal; a = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            c = l.vert.co
            l[uv].uv = ((c.y, c.z), (c.x, c.z), (c.x, c.y))[a]
            l[uv].uv = (l[uv].uv[0] * dichte, l[uv].uv[1] * dichte)

def netz_aus_bm(bm, name):
    uv_dreiachsig(bm)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); return me

def kappe(bm, mitte, r, hoehe, seg=12, ring=3):
    """Flache Kuppe (Nagelkopf): Halbkugel gestaucht, Mittelpunkt mitte, Hoehe hoehe nach +Z."""
    res = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=ring * 2, radius=r)
    for v in res["verts"]:
        v.co.z = max(v.co.z, 0.0) * (hoehe / r) if v.co.z > -1e-9 else 0.0
        v.co += mitte

# --------------------------------------------------------------------------- Beschlagplatte (Langschild) mit Lilienenden und Schluesselloch (Vorlage: 2D-Zeiger tuer_schloss_*.png)
def lilie_oben():
    """Blattende (Lilie): Faecher aus fuenf Blaettern (Betrag eines Kosinus: runde Spitzen, Kerben dazwischen), mittleres Blatt am groessten; rechts nach links."""
    pts = []
    for g in range(15, 166, 5):
        r = 0.0185 + (0.0105 * math.exp(-((g - 90) / 45.0) ** 2) + 0.0035) * (0.5 + 0.5 * math.cos(math.pi * (g - 90) / 18.0)) ** 1.2
        pts.append((r * math.cos(math.radians(g)), 0.056 + r * math.sin(math.radians(g))))
    return pts
aussen = ([(0.022, -0.050), (0.022, 0.050)] + lilie_oben() + [(-0.022, 0.050), (-0.022, -0.050)]
          + [(x, -y) for (x, y) in lilie_oben()[::-1]])
def schluessel_umriss(mitte):
    """Schluesselloch: Kreis oben, Schaft unten."""
    r = 0.0060; ps = []
    a0, a1 = math.radians(-60), math.radians(240)
    for i in range(15):
        a = a0 + (a1 - a0) * i / 14; ps.append((r * math.cos(a), r * math.sin(a) + 0.0040))
    ps.append((-0.0042, -0.0125)); ps.append((0.0042, -0.0125))
    return [(mitte.x + p[0], mitte.y + p[1]) for p in ps]
loch = schluessel_umriss(SCHLUESSEL)
bm = bmesh.new()
def schleife(pts, z):
    vs = [bm.verts.new((x, y, z)) for (x, y) in pts]
    es = [bm.edges.new((vs[i], vs[(i + 1) % len(vs)])) for i in range(len(vs))]
    return vs, es
vo, eo = schleife(aussen, 0.0); vl, el = schleife(loch, 0.0)
tf = bmesh.ops.triangle_fill(bm, edges=eo + el, use_beauty=True)
if not tf["geom"]: raise SystemExit("triangle_fill ohne Ergebnis")
fl = [g for g in tf["geom"] if isinstance(g, bmesh.types.BMFace)]
ext = bmesh.ops.extrude_face_region(bm, geom=fl)
bmesh.ops.translate(bm, verts=[g for g in ext["geom"] if isinstance(g, bmesh.types.BMVert)], vec=(0, 0, PLATTE_D))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
kanten = [e for e in bm.edges if all(abs(v.co.z - PLATTE_D) < 1e-6 for v in e.verts) and len({f.normal.z > 0.5 for f in e.link_faces}) == 2]
bmesh.ops.bevel(bm, geom=kanten, offset=0.0013, segments=1, affect="EDGES")
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
for (x, y) in ((0.0150, 0.044), (-0.0150, 0.044), (0.0140, -0.038), (-0.0140, -0.038)):
    kappe(bm, Vector((x, y, PLATTE_D)), 0.0040, 0.0030, 12, 2)
me_platte = netz_aus_bm(bm, "Schloss")
ob_platte = objekt("Schloss", me_platte, m_messing); glatt_schattieren(ob_platte)

# dunkler Grund im Schluesselloch (sonst sieht man durch die Platte)
bm = bmesh.new()
vs_ = [bm.verts.new((x, y, 0.0010)) for (x, y) in loch]
bm.faces.new(vs_); bmesh.ops.triangulate(bm, faces=bm.faces[:])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
for f in bm.faces:
    if f.normal.z < 0: f.normal_flip()
ob_dunkel = objekt("Schluesselgrund", netz_aus_bm(bm, "Schluesselgrund"), m_dunkel)

# Rosette (runder Wulst mit Stufen) an der Welle
bm = bmesh.new()
profil = [(0.0, 0.0), (0.0225, 0.0), (0.0225, 0.0035), (0.0200, 0.0048), (0.0200, 0.0062), (0.0165, 0.0080), (0.0125, 0.0094), (0.0108, 0.0104), (0.0098, 0.0104), (0.0, 0.0104)]
vs_ = []; SEG = 20
for (r, z) in profil:
    vs_.append([bm.verts.new((WELLE.x + r * math.cos(2 * math.pi * i / SEG), WELLE.y + r * math.sin(2 * math.pi * i / SEG), PLATTE_D + z)) for i in range(SEG)])
for j in range(len(profil) - 1):
    for i in range(SEG):
        try: bm.faces.new((vs_[j][i], vs_[j][(i + 1) % SEG], vs_[j + 1][(i + 1) % SEG], vs_[j + 1][i]))
        except ValueError: pass
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
ob_rose = objekt("Rosette", netz_aus_bm(bm, "Rosette"), m_messing); glatt_schattieren(ob_rose)

# Zierring um das Schluesselloch
bm = bmesh.new()
cx = float(np.mean([p[0] for p in loch])); cy = float(np.mean([p[1] for p in loch]))
rg_in = [(cx + (x - cx) * 1.05, cy + (y - cy) * 1.05) for (x, y) in loch]
rg_out = [(cx + (x - cx) * 1.55, cy + (y - cy) * 1.38) for (x, y) in loch]
vi = [bm.verts.new((x, y, PLATTE_D + 0.0018)) for (x, y) in rg_in]; vo_ = [bm.verts.new((x, y, PLATTE_D + 0.0018)) for (x, y) in rg_out]
vb = [bm.verts.new((x, y, PLATTE_D)) for (x, y) in rg_out]
for i in range(len(vi)):
    j = (i + 1) % len(vi)
    bm.faces.new((vi[i], vi[j], vo_[j], vo_[i])); bm.faces.new((vo_[i], vo_[j], vb[j], vb[i]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
ob_ring = objekt("Schluesselring", netz_aus_bm(bm, "Schluesselring"), m_messing)

# --------------------------------------------------------------------------- Klinke (Messing): Welle, Kragen, geschwungene Griffstange in der Plattenebene, Ende eingerollt
def sweep(bm, pts, radien, seg=12, ref=Vector((0, 0, 1))):
    """Rohr entlang pts mit Radius je Punkt, Kappen an beiden Enden."""
    ringe = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        s = t.cross(ref)
        if s.length < 1e-4: s = t.cross(Vector((1, 0, 0)))
        s.normalize(); u = s.cross(t).normalized()
        ringe.append([bm.verts.new(p + (math.cos(2 * math.pi * k / seg) * s + math.sin(2 * math.pi * k / seg) * u) * radien[i]) for k in range(seg)])
    for i in range(len(ringe) - 1):
        for k in range(seg):
            bm.faces.new((ringe[i][k], ringe[i][(k + 1) % seg], ringe[i + 1][(k + 1) % seg], ringe[i + 1][k]))
    bm.faces.new(ringe[0][::-1]); bm.faces.new(ringe[-1])

def catmull(P0, n=6):
    out = []
    P = [P0[0]] + P0 + [P0[-1]]
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(n):
            t = k / n
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(P0[-1]); return out

bm = bmesh.new()
H = STAND
# Pfad in der Plattenebene (X rechts, Y hoch), ab der Welle; Vorlage: steigt nach rechts, taucht ab, Ende leicht nach oben eingerollt
STUETZ = [(0.0, 0.0), (0.020, 0.0100), (0.040, 0.0160), (0.062, 0.0120), (0.080, 0.0030), (0.098, -0.0040), (0.112, -0.0060), (0.122, -0.0020), (0.126, 0.0040)]
pfad = catmull([Vector((0.82 * x, 0.82 * y, H)) for (x, y) in STUETZ], 3)
rad = [0.0115 - (0.0115 - 0.0088) * min(1.0, i / 6) for i in range(len(pfad))]
rad[-3:] = [0.0091, 0.0096, 0.0100]
sweep(bm, pfad, rad, 12)
ende = pfad[-1]; t_ = (pfad[-1] - pfad[-2]).normalized()
res = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=6, radius=0.0105)
for v in res["verts"]: v.co += ende + t_ * 0.002
res2 = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=6, radius=0.0118)
for v in res2["verts"]: v.co += Vector((0, 0, H))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
ob_klinke_me = netz_aus_bm(bm, "Klinke_Netz")
klinke = bpy.data.objects.new("Klinke", None); klinke.empty_display_type = "ARROWS"; klinke.empty_display_size = 0.02
sc.collection.objects.link(klinke); klinke.location = WELLE; klinke.rotation_mode = "XYZ"
ob_klinke = objekt("Klinke_Netz", ob_klinke_me, m_messing, klinke); glatt_schattieren(ob_klinke)
GRIFF_ANKER = min(pfad, key=lambda p: abs(p.x - 0.054))      # Mitte der Griffstange (dort sitzt die Faust)
# Welle (senkrecht) von der Rosette bis zur Stange
bm = bmesh.new()
sweep(bm, [Vector((0, 0, PLATTE_D + 0.008)), Vector((0, 0, STAND - 0.004))], [0.0080, 0.0080], 14)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
ob_welle = objekt("Welle", netz_aus_bm(bm, "Welle"), m_messing); ob_welle.location = WELLE; glatt_schattieren(ob_welle)


# --------------------------------------------------------------------------- Umriss-Huellen (invertierte Huelle, wie beim Handschuh)
def umriss_bauen(quelle, name, abstand):
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

# Platte, Rosette, Ring, Welle zu einem festen Netz "Schloss" vereinigen (je ein Material-Slot), Umriss darueber
fest = [ob_platte, ob_rose, ob_ring, ob_welle, ob_dunkel]
bpy.context.view_layer.update()
for o in fest:
    o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)
bpy.ops.object.select_all(action="DESELECT")
for o in fest: o.select_set(True)
bpy.context.view_layer.objects.active = ob_platte
for o in fest:
    o.data.materials.clear(); o.data.materials.append(m_messing); o.data.materials.append(m_dunkel)
    for p in o.data.polygons: p.material_index = 1 if o is ob_dunkel else 0
bpy.ops.object.join()
ob_schloss = bpy.context.view_layer.objects.active; ob_schloss.name = "Schloss"; ob_schloss.data.name = "Schloss"
um_schloss = umriss_bauen(ob_schloss, "Umriss_Schloss", UMRISS)
um_klinke = umriss_bauen(ob_klinke, "Umriss_Klinke", UMRISS)
um_klinke.parent = klinke; um_klinke.matrix_parent_inverse = Matrix.Identity(4)
# Umriss ueber dem Schluesselring/Welle soll das Messing nicht verdecken: reicht, da nur Rueckseiten gezeichnet werden

# --------------------------------------------------------------------------- Wurzel und Anker
wurzel = bpy.data.objects.new("Tuerschloss", None); wurzel.empty_display_type = "PLAIN_AXES"; wurzel.empty_display_size = 0.02
sc.collection.objects.link(wurzel)
for o in (ob_schloss, um_schloss, klinke): o.parent = wurzel
def anker(name, eltern, pos):
    a = bpy.data.objects.new(name, None); a.empty_display_type = "PLAIN_AXES"; a.empty_display_size = 0.012
    sc.collection.objects.link(a); a.parent = eltern; a.location = pos; return a
anker("Anker_Schloss", wurzel, Vector((0, 0, PLATTE_D)))                  # Plattenmitte auf der Oberflaeche
anker("Anker_Griff", klinke, Vector((GRIFF_ANKER.x, GRIFF_ANKER.y, GRIFF_ANKER.z)))                 # Mitte der Griffstange (dort sitzt die Faust), dreht mit der Klinke
anker("Anker_Welle", wurzel, Vector((WELLE.x, WELLE.y, PLATTE_D)))         # Drehpunkt (Achse +Z)
bpy.context.view_layer.update()

# --------------------------------------------------------------------------- Aktionen
def sanft(t): t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)
def aktion(name, werte):
    a = bpy.data.actions.new(name); a.use_fake_user = True
    klinke.animation_data_create(); klinke.animation_data.action = a
    for f, w in enumerate(werte):
        klinke.rotation_euler = (0, 0, math.radians(-w)); klinke.keyframe_insert("rotation_euler", index=2, frame=f)
    a.use_frame_range = True; a.frame_range = (0, max(1, len(werte) - 1))
    t = klinke.animation_data.nla_tracks.new(); t.name = name; t.strips.new(name, 0, a)   # NLA-Spur je Aktion: so exportiert Blender alle drei
    return a
aktion("Druecken", [WINKEL * sanft(f / N_DRUECKEN) for f in range(N_DRUECKEN + 1)] + [WINKEL] * N_HALTEN)
aktion("Loslassen", [WINKEL * (1 - sanft(f / (0.34 * N_LOS))) for f in range(N_LOS + 1)])   # Klinke federt in den ersten 34 % zurueck (Hand liegt noch auf), dann nichts mehr
klinke.animation_data.action = None; klinke.rotation_euler = (0, 0, 0); sc.frame_set(0)

# --------------------------------------------------------------------------- Kennzahlen
dg = bpy.context.evaluated_depsgraph_get()
def dreiecke(ob): return sum(len(p.vertices) - 2 for p in ob.data.polygons)
masse = {"dreiecke": {o.name: dreiecke(o) for o in (ob_schloss, ob_klinke, um_schloss, um_klinke)}, "fps": FPS,
         "platte_m": [PLATTE_B, PLATTE_H, PLATTE_D], "welle_xy": [WELLE.x, WELLE.y], "stand_m": STAND, "griff_laenge_m": 0.82 * 0.1235 + 0.0105 + 0.002,
         "winkel_grad": WINKEL, "druecken_bilder": N_DRUECKEN + N_HALTEN, "loslassen_bilder": N_LOS, "umriss_m": UMRISS,
         "anker_griff": [GRIFF_ANKER.x + WELLE.x, GRIFF_ANKER.y + WELLE.y, GRIFF_ANKER.z]}
masse["dreiecke_summe"] = sum(masse["dreiecke"].values())
json.dump(masse, open(os.path.join(aus, "tuerschloss_masse.json"), "w"), indent=1)
print("MASSE", json.dumps(masse))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(aus, "tuerschloss.blend"))
bpy.ops.object.select_all(action="DESELECT")
bpy.ops.export_scene.gltf(filepath=os.path.join(aus, "tuerschloss_zeiger.glb"), export_format="GLB", export_animations=True,
                          export_animation_mode="NLA_TRACKS", export_optimize_animation_size=False, export_apply=False, export_cameras=False, export_lights=False)
print("GLB geschrieben")
