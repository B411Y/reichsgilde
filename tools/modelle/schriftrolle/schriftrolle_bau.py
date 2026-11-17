"""Schriftrolle fuer Meldungen: zwei Holzstaebe mit gedrechselten Knaeufen, Pergament dazwischen, Umriss-Huelle wie beim 3D-Handschuh.
  blender -b --python schriftrolle_bau.py -- <ausordner>
(unter Windows ueber tools/modelle/handschuh/lauf.ps1: Idle-Prioritaet; Pfade absolut.)
Schreibt <ausordner>/schriftrolle.glb (mit Clips), <ausordner>/schriftrolle.blend und <ausordner>/messwerte_schriftrolle.json.

Aufbau (Meter, Blender-Koordinaten; Blick von vorn = -Y, X nach rechts, Z oben; Mittelpunkt des Pergaments = Ursprung):
  - Geruest "Schriftrolle_Geruest": 27 Knochen ohne Eltern: Stab_oben, Stab_unten, Zeile_00..Zeile_24 (eine pro Querlinie des Pergaments).
    Rast-Pose = "zu"; die Clips setzen je Bild die Lage jedes Knochens (Rolle wickelt sich auf einer Archimedes-Spirale, die Staebe drehen sich mit).
  - Netze: "Schriftrolle" (Holz + Pergament), "Schriftrolle_Umriss" (invertierte Huelle, schwarz), "Symbolflaeche" (Quad auf dem Pergament, Material Schriftrolle_Symbol).
  - Form: Die Rolle ist ein S: oberer und unterer Stab wickeln je die halbe Pergamentlaenge, im geschlossenen Zustand beruehren sich die Rollen in der Mitte.
Gleiche Machart wie der Handschuh: Umriss durch invertierte Huelle (Ecken entlang der gemittelten Normalen verschoben, Flaechen gewendet), Knochengewichte 1,0.
Texturen (Pergament, Holz) werden hier selbst gemalt (numpy, feste Saat) und in die .glb eingebettet."""
import bpy, bmesh, math, os, sys, json, struct, zlib
import numpy as np
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
AUS = args[0]
os.makedirs(AUS, exist_ok=True)

# ------------------------------------------------------------------ Masse (Meter)
W = 0.30            # Breite des Pergaments
L = 0.40            # Laenge des Pergaments (offen: Abstand der Stabmitten)
T = 0.006           # Dicke des Pergaments (uebertrieben, damit die Rolle Kontur hat)
R0 = 0.015          # Radius der Staebe
RS = R0 + T / 2     # Spirale beginnt auf dem Stab
K = T / math.pi     # Flaechenzuwachs der Spirale: r^2 = RS^2 + K * Laenge
N = 12              # Zeilen je Haelfte (insgesamt 2N+1 Querlinien)
SEG = 8             # Ecken der Staebe
UMRISS = 0.005      # Abstand der Umriss-Huelle (wie beim Handschuh ca. 2 % der Groesse)
SYM_ZEILEN = (7, 17)  # Zeilen der Symbolflaeche (Mitte = Zeile N)
SYM_B = (SYM_ZEILEN[1] - SYM_ZEILEN[0]) * L / (2 * N)   # Hoehe = Breite der Symbolflaeche
SYM_ABST = 0.0008   # Symbolflaeche schwebt 0,8 mm ueber dem Pergament (kein Z-Fighting)
FPS = 30
CLIP_BILDER = 21    # 0,7 s
NAMEN_ZEILE = [f"Zeile_{i:02d}" for i in range(2 * N + 1)]
STAB = ("Stab_oben", "Stab_unten")

# Drechselprofil (x, r) fuer die rechte Haelfte; gespiegelt ergibt das beide Knaeufe: Schaft, Perle, Hals, Kugelknauf
HALB = [(0.152, 0.015), (0.157, 0.0215), (0.163, 0.0215), (0.167, 0.0135), (0.178, 0.0135), (0.187, 0.027), (0.200, 0.027), (0.212, 0.012), (0.2145, 0.0)]
PROFIL = [(-x, r) for (x, r) in reversed(HALB)] + HALB


def rot_x(t):
    return Matrix.Rotation(t, 4, 'X')


def trans(y, z):
    return Matrix.Translation((0.0, y, z))


def zustand(u):
    """Lage aller Zeilen und Staebe fuer Offenheit u (0 = zu, 1 = offen): Liste (y, z, winkel) je Zeile, Liste (y, z, winkel) je Stab."""
    w = (1.0 - u) * L / 2
    f = 1.0 - u
    phi = lambda s: 2.0 * (math.sqrt(RS * RS + K * s) - RS) / K
    rw = math.sqrt(RS * RS + K * w)
    ad = -f * math.pi / 2
    l = L / 2 - w
    tau = (math.sin(ad), -math.cos(ad))
    nn = (math.cos(ad), math.sin(ad))
    C = (-l * tau[0] - rw * nn[0], -l * tau[1] - rw * nn[1])
    Pd = (C[0] + rw * nn[0], C[1] + rw * nn[1])
    zeilen = []
    for i in range(2 * N + 1):
        s = i * L / (2 * N)
        sig = s if i <= N else L - s
        if sig <= w + 1e-9:
            r = math.sqrt(RS * RS + K * sig)
            a = ad + phi(w) - phi(sig)
            p = (C[0] + r * math.cos(a), C[1] + r * math.sin(a))
            th = a
        else:
            p = (Pd[0] + (sig - w) * tau[0], Pd[1] + (sig - w) * tau[1])
            th = ad
        if i > N:
            p = (-p[0], -p[1])
        zeilen.append((p[0], p[1], th))
    th0 = ad + phi(w)
    staebe = [(C[0], C[1], th0), (-C[0], -C[1], th0)]
    return zeilen, staebe


def matrix_zeile(z):
    return trans(z[0], z[1]) @ rot_x(z[2])


# ------------------------------------------------------------------ Texturen (selbst gemalt)
def png_schreiben(pfad, rgb):
    h, b, _ = rgb.shape
    roh = b"".join(b"\x00" + rgb[y].tobytes() for y in range(h))

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff)
    with open(pfad, "wb") as fh:
        fh.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", b, h, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(roh, 9)) + chunk(b"IEND", b""))


def vnoise(rng, h, b, gy, gx):
    """Periodischer Wertrauschen-Teppich (kachelbar), glatt interpoliert."""
    g = rng.random((gy, gx))
    ys = np.arange(h) * gy / h
    xs = np.arange(b) * gx / b
    y0 = np.floor(ys).astype(int); x0 = np.floor(xs).astype(int)
    fy = ys - y0; fx = xs - x0
    fy = fy * fy * (3 - 2 * fy); fx = fx * fx * (3 - 2 * fx)
    y1 = (y0 + 1) % gy; x1 = (x0 + 1) % gx
    a = g[np.ix_(y0, x0)]; bb = g[np.ix_(y0, x1)]; c = g[np.ix_(y1, x0)]; d = g[np.ix_(y1, x1)]
    fyy = fy[:, None]; fxx = fx[None, :]
    return (a * (1 - fxx) + bb * fxx) * (1 - fyy) + (c * (1 - fxx) + d * fxx) * fyy


def textur_pergament():
    rng = np.random.default_rng(1480)
    h, b = 256, 192   # 3:4 wie das Pergament (Breite : Laenge = 0,30 : 0,40)
    b, h = 192, 256
    fleck = 0.55 * vnoise(rng, h, b, 4, 3) + 0.30 * vnoise(rng, h, b, 12, 9) + 0.15 * vnoise(rng, h, b, 40, 30)
    faser = vnoise(rng, h, b, 90, 6)
    farbe = np.array([0.86, 0.75, 0.52])
    tief = np.array([0.66, 0.50, 0.29])
    t = np.clip((fleck - 0.25) * 1.5, 0, 1)[..., None]
    bild = farbe * (1 - t) + tief * t * 0.55 + farbe * t * 0.45
    bild = bild * (0.93 + 0.12 * faser[..., None])
    yy, xx = np.mgrid[0:h, 0:b]
    rand = np.minimum.reduce([xx, b - 1 - xx, yy, h - 1 - yy]) / 22.0
    wirk = np.clip(1.0 - rand, 0, 1) ** 2 * (0.55 + 0.45 * vnoise(rng, h, b, 16, 12))
    bild = bild * (1 - 0.55 * wirk[..., None]) + np.array([0.30, 0.16, 0.06]) * 0.55 * wirk[..., None]
    return np.clip(bild * 255 + 0.5, 0, 255).astype(np.uint8)


def textur_holz():
    rng = np.random.default_rng(1481)
    h, b = 128, 128   # Maserung laeuft entlang U (Stabachse), kachelbar
    maser = 0.5 * vnoise(rng, h, b, 22, 2) + 0.3 * vnoise(rng, h, b, 56, 3) + 0.2 * vnoise(rng, h, b, 8, 2)
    ring = 0.5 + 0.5 * np.sin(maser * 14.0)
    hell = np.array([0.52, 0.34, 0.18])
    dunkel = np.array([0.31, 0.18, 0.08])
    bild = dunkel + (hell - dunkel) * (0.55 * maser[..., None] + 0.45 * ring[..., None])
    return np.clip(bild * 255 + 0.5, 0, 255).astype(np.uint8)


def bild_laden(name, arr):
    pfad = os.path.join(AUS, name + ".png")
    png_schreiben(pfad, arr)
    img = bpy.data.images.load(pfad)
    img.name = name
    img.colorspace_settings.name = "sRGB"
    img.pack()
    return img


def stoff(name, bild, rau, farbe=None, cull=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for nd in list(nt.nodes):
        nt.nodes.remove(nd)
    ausg = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], ausg.inputs["Surface"])
    bsdf.inputs["Roughness"].default_value = rau
    bsdf.inputs["Metallic"].default_value = 0.0
    if bild is not None:
        tx = nt.nodes.new("ShaderNodeTexImage")
        tx.image = bild
        tx.interpolation = "Closest"
        nt.links.new(tx.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (*farbe, 1)
    m.use_backface_culling = cull
    return m


# ------------------------------------------------------------------ Szene
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = FPS
sc.unit_settings.system = "METRIC"

img_perg = bild_laden("schriftrolle_pergament", textur_pergament())
img_holz = bild_laden("schriftrolle_holz", textur_holz())
m_holz = stoff("Schriftrolle_Holz", img_holz, 0.75)
m_perg = stoff("Schriftrolle_Pergament", img_perg, 0.9)
m_sym = stoff("Schriftrolle_Symbol", None, 0.9, farbe=(0.93, 0.86, 0.68))
m_um = stoff("Umriss", None, 1.0, farbe=(0.02, 0.012, 0.008), cull=True)

zeilen0, staebe0 = zustand(0.0)

# ------------------------------------------------------------------ Netz Schriftrolle (Holz + Pergament) im Zustand "zu"
bm = bmesh.new()
uvl = bm.loops.layers.uv.new("UVMap")
v_gruppe, v_uv = {}, {}


def vert(co, uv, gruppe):
    v = bm.verts.new(co)
    v_gruppe[v] = gruppe
    v_uv[v] = uv
    return v


def flaeche(vs, mat, erwartet):
    co = [v.co for v in vs]
    n = Vector((0, 0, 0))
    for i in range(len(co)):
        a, b = co[i], co[(i + 1) % len(co)]
        n += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
    if n.dot(erwartet) < 0:
        vs = vs[::-1]
    f = bm.faces.new(vs)
    f.material_index = mat
    f.smooth = True
    for lp in f.loops:
        lp[uvl].uv = v_uv[lp.vert]
    return f


# Pergament: je Zeile acht Ecken (vorn, hinten, links, rechts je zwei), damit Kanten hart bleiben und die Flaechen glatt
ZEILE = []
for i, z in enumerate(zeilen0):
    M = matrix_zeile(z)
    R3 = M.to_3x3()
    s = i * L / (2 * N)
    vv = 1.0 - s / L
    g = NAMEN_ZEILE[i]
    d = {}
    for nm, ly, uvx in (("v", -T / 2, 0), ("h", T / 2, 1)):
        for sx in (-1, 1):
            u_ = (sx * 0.5 + 0.5) if uvx == 0 else (0.5 - sx * 0.5)
            d[(nm, sx)] = vert(M @ Vector((sx * W / 2, ly, 0.0)), (u_, vv), g)
    for sx in (-1, 1):
        for ly, yy in ((-T / 2, 0.0), (T / 2, 1.0)):
            d[("k", sx, ly)] = vert(M @ Vector((sx * W / 2, ly, 0.0)), (0.5 + 0.5 * yy * 0.02, vv), g)
    ZEILE.append((d, R3))

for i in range(2 * N):
    d0, R3 = ZEILE[i]
    d1, _ = ZEILE[i + 1]
    vorn = R3 @ Vector((0, -1, 0)); hinten = -vorn
    flaeche([d0[("v", -1)], d0[("v", 1)], d1[("v", 1)], d1[("v", -1)]], 1, vorn)
    flaeche([d0[("h", -1)], d0[("h", 1)], d1[("h", 1)], d1[("h", -1)]], 1, hinten)
    for sx in (-1, 1):
        flaeche([d0[("k", sx, -T / 2)], d0[("k", sx, T / 2)], d1[("k", sx, T / 2)], d1[("k", sx, -T / 2)]], 1, Vector((sx, 0, 0)))

# Staebe (Drechselteile) um die X-Achse, Gewicht 1 auf dem Stabknochen
for si, st in enumerate(staebe0):
    M = matrix_zeile(st)
    R3 = M.to_3x3()
    ringe = []
    for (x, r) in PROFIL:
        ring = []
        for k in range(SEG + 1):
            a = 2 * math.pi * k / SEG
            ring.append(vert(M @ Vector((x, r * math.cos(a), r * math.sin(a))), ((x + 0.2145) / 0.429, k / SEG), STAB[si]))
        ringe.append(ring)
    for j in range(len(PROFIL) - 1):
        (x0, r0), (x1, r1) = PROFIL[j], PROFIL[j + 1]
        dx, dr = x1 - x0, r1 - r0
        ln = math.hypot(dx, dr)
        nx, nr = -dr / ln, dx / ln
        for k in range(SEG):
            am = 2 * math.pi * (k + 0.5) / SEG
            erw = R3 @ Vector((nx, nr * math.cos(am), nr * math.sin(am)))
            a0, a1, b0, b1 = ringe[j][k], ringe[j][k + 1], ringe[j + 1][k], ringe[j + 1][k + 1]
            if r0 == 0.0:
                flaeche([a0, b1, b0], 0, erw)
            elif r1 == 0.0:
                flaeche([a0, a1, b0], 0, erw)
            else:
                flaeche([a0, a1, b1, b0], 0, erw)

bmesh.ops.triangulate(bm, faces=bm.faces[:])
bm.verts.index_update()
gruppen_pro_vert = [v_gruppe[v] for v in bm.verts]
me = bpy.data.meshes.new("Schriftrolle")
bm.to_mesh(me)
bm.free()
me.materials.append(m_holz)
me.materials.append(m_perg)
ob = bpy.data.objects.new("Schriftrolle", me)
sc.collection.objects.link(ob)

# ------------------------------------------------------------------ Symbolflaeche
bm = bmesh.new()
uvl = bm.loops.layers.uv.new("UVMap")
v_gruppe, v_uv = {}, {}
a0, a1 = SYM_ZEILEN
symr = []
for i in range(a0, a1 + 1):
    M = matrix_zeile(zeilen0[i])
    vv = 1.0 - (i - a0) / (a1 - a0)
    ly = -T / 2 - SYM_ABST
    symr.append((vert(M @ Vector((-SYM_B / 2, ly, 0.0)), (0.0, vv), NAMEN_ZEILE[i]), vert(M @ Vector((SYM_B / 2, ly, 0.0)), (1.0, vv), NAMEN_ZEILE[i]), M.to_3x3()))
for i in range(len(symr) - 1):
    l0, r0_, R3 = symr[i]
    l1, r1_, _ = symr[i + 1]
    flaeche([l0, r0_, r1_, l1], 0, R3 @ Vector((0, -1, 0)))
bmesh.ops.triangulate(bm, faces=bm.faces[:])
bm.verts.index_update()
sym_gruppen = [v_gruppe[v] for v in bm.verts]
mes = bpy.data.meshes.new("Symbolflaeche")
bm.to_mesh(mes)
bm.free()
mes.materials.append(m_sym)
obs = bpy.data.objects.new("Symbolflaeche", mes)
sc.collection.objects.link(obs)


# ------------------------------------------------------------------ Umriss-Huelle (wie beim Handschuh: invertierte Huelle, Verschiebung ueber gleiche Orte gemittelt)
def umriss_bauen(quelle, name, abstand):
    me2 = quelle.data.copy()
    me2.name = name
    me2.update()
    nm = [v.normal.copy() for v in me2.vertices]
    pos = {}
    for v, n_ in zip(me2.vertices, nm):
        k = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5))
        pos.setdefault(k, Vector((0, 0, 0)))
        pos[k] += n_
    for v in me2.vertices:
        k = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5))
        d = pos[k].normalized() if pos[k].length > 1e-9 else Vector((0, 0, 0))
        v.co = v.co + d * abstand
    bm2 = bmesh.new()
    bm2.from_mesh(me2)
    bmesh.ops.reverse_faces(bm2, faces=bm2.faces)
    for f in bm2.faces:
        f.material_index = 0
    bm2.to_mesh(me2)
    bm2.free()
    me2.materials.clear()
    me2.materials.append(m_um)
    o = bpy.data.objects.new(name, me2)
    sc.collection.objects.link(o)
    return o


um = umriss_bauen(ob, "Schriftrolle_Umriss", UMRISS)
um_gruppen = gruppen_pro_vert

# ------------------------------------------------------------------ Geruest (Knochen) und Gewichte
arm_d = bpy.data.armatures.new("Schriftrolle_Geruest")
arm = bpy.data.objects.new("Schriftrolle_Geruest", arm_d)
sc.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bone_namen = list(STAB) + NAMEN_ZEILE
bone_ruhe = {}
for nm, z in zip(bone_namen, list(staebe0) + list(zeilen0)):
    eb = arm_d.edit_bones.new(nm)
    eb.head = (0, 0, 0)
    eb.tail = (0, 0.02, 0)
    eb.matrix = matrix_zeile(z)
    eb.length = 0.02
bpy.ops.object.mode_set(mode="OBJECT")
for nm, z in zip(bone_namen, list(staebe0) + list(zeilen0)):
    abw = (arm_d.bones[nm].matrix_local - matrix_zeile(z))
    m = max(abs(x) for row in abw for x in row)
    if m > 1e-5:
        print("WARNUNG Knochenlage", nm, m)
    bone_ruhe[nm] = arm_d.bones[nm].matrix_local.copy()

for o, gr in ((ob, gruppen_pro_vert), (um, um_gruppen), (obs, sym_gruppen)):
    for nm in bone_namen:
        o.vertex_groups.new(name=nm)
    for vi, nm in enumerate(gr):
        o.vertex_groups[nm].add([vi], 1.0, "REPLACE")
    o.parent = arm
    md = o.modifiers.new("Geruest", "ARMATURE")
    md.object = arm


# ------------------------------------------------------------------ Clips
def pose_setzen(u, frame=None):
    zl, st = zustand(u)
    for nm, z in zip(bone_namen, list(st) + list(zl)):
        pb = arm.pose.bones[nm]
        B = bone_ruhe[nm].inverted() @ matrix_zeile(z)
        loc, rot, scl = B.decompose()
        pb.rotation_mode = "QUATERNION"
        pb.location = loc
        pb.rotation_quaternion = rot
        pb.scale = scl
        if frame is not None:
            pb.keyframe_insert("location", frame=frame, group=nm)
            pb.keyframe_insert("rotation_quaternion", frame=frame, group=nm)


def sanft(t):
    return t * t * (3 - 2 * t)


try:
    bpy.context.preferences.edit.keyframe_new_interpolation_type = "LINEAR"
except Exception as e:
    print("INTERPOLATION", e)
arm.animation_data_create()
aktionen = {}


def clip(name, bilder, offenheit):
    a = bpy.data.actions.new(name)
    a.use_fake_user = True
    arm.animation_data.action = a
    for f in range(bilder + 1):
        pose_setzen(offenheit(f / bilder), f)
    a.frame_range = (0, bilder)
    a.use_frame_range = True
    a.use_cyclic = False
    aktionen[name] = a


clip("aufrollen", CLIP_BILDER, lambda t: sanft(t))
clip("zurollen", CLIP_BILDER, lambda t: sanft(1.0 - t))
clip("zu", 1, lambda t: 0.0)
arm.animation_data.action = aktionen["zu"]
sc.frame_set(0)


# ------------------------------------------------------------------ Messwerte (Mitte, Masse zu/offen), Dreiecke
def hoehe_netz(o, frame, action):
    arm.animation_data.action = aktionen[action]
    sc.frame_set(frame)
    dg = bpy.context.evaluated_depsgraph_get()
    e = o.evaluated_get(dg)
    m = e.to_mesh()
    pts = np.array([(o.matrix_world @ v.co)[:] for v in m.vertices])
    e.to_mesh_clear()
    return pts.min(axis=0), pts.max(axis=0)


def dreiecke(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


mess = {"dreiecke": {"Schriftrolle": dreiecke(ob), "Schriftrolle_Umriss": dreiecke(um), "Symbolflaeche": dreiecke(obs)}}
mess["dreiecke"]["gesamt"] = sum(mess["dreiecke"].values())
for zst, (fr, act) in (("zu", (0, "zu")), ("offen", (CLIP_BILDER, "aufrollen"))):
    mn, mx = hoehe_netz(ob, fr, act)
    smn, smx = hoehe_netz(obs, fr, act)
    mess[zst] = {"schriftrolle_min": [round(float(x), 4) for x in mn], "schriftrolle_max": [round(float(x), 4) for x in mx],
                 "symbol_min": [round(float(x), 4) for x in smn], "symbol_max": [round(float(x), 4) for x in smx],
                 "symbol_mitte": [round(float((a + b) / 2), 4) for a, b in zip(smn, smx)]}
mess["symbolflaeche_m"] = [round(SYM_B, 4), round(SYM_B, 4)]
mess["masse"] = {"breite": W, "laenge": L, "dicke": T, "stabradius": R0, "knauf_radius": 0.027, "stablaenge_mit_knaeufen": 2 * 0.2145}
arm.animation_data.action = aktionen["zu"]
sc.frame_set(0)
print("MESSWERTE", json.dumps(mess))
with open(os.path.join(AUS, "messwerte_schriftrolle.json"), "w", encoding="utf-8") as fh:
    json.dump(mess, fh, indent=1)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(AUS, "schriftrolle.blend"))

# ------------------------------------------------------------------ Export
for o in sc.objects:
    o.select_set(False)
for o in (arm, ob, um, obs):
    o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.gltf(filepath=os.path.join(AUS, "schriftrolle.glb"), export_format="GLB", use_selection=True, export_animations=True,
                          export_animation_mode="ACTIONS", export_skins=True, export_yup=True, export_apply=False,
                          export_image_format="AUTO", export_force_sampling=True, export_frame_range=False,
                          export_optimize_animation_size=True)
print("GLB", os.path.join(AUS, "schriftrolle.glb"), os.path.getsize(os.path.join(AUS, "schriftrolle.glb")))
