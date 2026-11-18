"""Briefe fuer Meldungen: kleiner Faltbrief (Symbol der Meldungsleiste) und grosser Brief zum Auffalten, Umriss-Huelle wie beim 3D-Handschuh und der Schriftrolle.
  blender -b --python brief_bau.py -- <ausordner>
(unter Windows ueber tools/modelle/handschuh/lauf.ps1: Idle-Prioritaet; Pfade absolut.)
Schreibt <ausordner>/brief_klein.glb, brief_gross.glb (mit Clips), die .blend-Dateien und messwerte_brief.json.

Um 1400 gab es fuer Briefe keine Rollen und keine Umschlaege, sondern GEFALTETE BRIEFE AUS HADERNPAPIER MIT WACHSSIEGEL. Der Brief hier ist der klassische Dreierfalz:
das untere Drittel wird nach vorn (zum Betrachter, -Y) hochgeklappt, das obere Drittel darueber heruntergeklappt, das Siegel sitzt auf der Kante des oberen Falzes.

Koordinaten (Meter, Blender; Blick von vorn = -Y, X nach rechts, Z oben; Mitte des offenen Blattes = Ursprung; Godot: +Z vorn, Y oben):
  - Drei starre Zellen: Falz_oben (Oben), Mitte (Streck), Falz_unten (Unten). Die Mitte liegt immer in y = 0 und bewegt sich im Clip nie; nur die beiden Falze klappen um X-parallele Kanten.
  - Rast-Pose = zu (gefaltet). Gewichte 1,0 (starr, kein Formschluessel). Neigung gehoert nicht in den Clip.
  - Hoehe strecken (nur das grosse Blatt): die Knochen Streck, Anker_oben, Anker_unten tragen KEINE Clipspur; der Code setzt sie (siehe docs/bereiche/darstellung.md, "3D-Briefe").
Texturen (Hadernpapier) werden hier selbst gemalt (numpy, feste Saat) und in die .glb eingebettet."""
import bpy, bmesh, math, os, sys, json, struct, zlib
import numpy as np
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
AUS = args[0]
os.makedirs(AUS, exist_ok=True)
FPS = 30
CLIP_BILDER = 27   # 0,9 s


# ------------------------------------------------------------------ Masse
class Mass:
    def __init__(s, W, FZ, HM, T, G, R_siegel, H_siegel, umriss, unterteil, texturname):
        s.W, s.FZ, s.HM, s.T, s.G = W, FZ, HM, T, G
        s.P = T + G                      # Abstand der gefalteten Lagen
        s.H0 = 2 * FZ + HM               # Hoehe des offenen Blattes (kleinste Hoehe)
        s.step = FZ / 4                  # Abstand der Randstuetzpunkte
        s.unterteil = unterteil
        s.R = R_siegel
        s.umriss = umriss
        s.rand = 0.02 * (W / 0.30)       # Rand der Inhaltsflaeche
        s.zfrei = HM / 2 - FZ            # freie Kante des oberen Falzes (gefaltet), dort sitzt das Siegel
        s.y_unten_vorn = -s.P - T / 2    # Vorderflaeche des unteren Falzes (gefaltet)
        s.y_oben_vorn = -2 * s.P - T / 2
        s.y_top = s.y_oben_vorn - H_siegel   # Oberkante des Siegels
        s.h_siegel = s.y_unten_vorn - s.y_top
        s.amp = 0.004 * W                # Unregelmaessigkeit des Randes (Buettenrand)
        s.textur = texturname

    def flap(s, art, c):
        """Matrix des Falzes bei Schliessgrad c (0 = offen/flach, 1 = gefaltet). Drehung um eine X-parallele Kante; 'o' oben, 'u' unten."""
        zc = s.HM / 2 if art == "o" else -s.HM / 2
        yh = -s.P if art == "o" else -s.P / 2
        alpha = math.pi * c if art == "o" else -math.pi * c
        h = Vector((0, yh, zc))
        return Matrix.Translation(h) @ Matrix.Rotation(alpha, 4, "X") @ Matrix.Translation(-h)


GROSS = Mass(W=0.30, FZ=0.12, HM=0.18, T=0.003, G=0.0005, R_siegel=0.028, H_siegel=0.006, umriss=0.006, unterteil=1, texturname="brief_gross_papier")
KLEIN = Mass(W=0.12, FZ=0.048, HM=0.072, T=0.0026, G=0.0005, R_siegel=0.0145, H_siegel=0.0045, umriss=0.0025, unterteil=2, texturname="brief_klein_papier")
S_MAX = 2.0        # groesste Streckung der Mitte (Hoehe 0,42 -> 0,60 m)


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


def textur_papier(b, h, seed, v_falze):
    """Hadernpapier: warmes Weiss, leichte Flecken (Stockflecken), Faserung, feine Rippung (Siebmarken), Hadernreste, dunkler Faserrand, Falzlinien bei den v-Werten v_falze."""
    rng = np.random.default_rng(seed)
    fleck = 0.5 * vnoise(rng, h, b, 6, 4) + 0.3 * vnoise(rng, h, b, 18, 12) + 0.2 * vnoise(rng, h, b, 64, 40)
    faser = vnoise(rng, h, b, h // 3, b // 6) * 0.6 + vnoise(rng, h, b, h // 8, b // 2) * 0.4
    hell = np.array([0.885, 0.835, 0.705])
    tief = np.array([0.74, 0.65, 0.48])
    t = np.clip((fleck - 0.35) * 1.8, 0, 1)[..., None]
    bild = hell * (1 - t) + tief * t
    bild = bild * (0.955 + 0.075 * faser[..., None])
    yy, xx = np.mgrid[0:h, 0:b]
    # Rippung: feine waagerechte Linien und senkrechte Kettlinien, nur ganz leicht
    ripp = 0.5 + 0.5 * np.cos(2 * np.pi * yy / (h / 134.0))
    kette = (np.cos(2 * np.pi * xx / (b / 9.5)) > 0.985).astype(float)
    bild = bild * (1 - 0.028 * ripp[..., None] * (0.5 + 0.5 * vnoise(rng, h, b, 8, 6)[..., None])) * (1 - 0.05 * kette[..., None])
    # Stockflecken: weiche braune Flecken
    for _ in range(int(60 * b * h / (1024 * 1536))):
        cy, cx = rng.integers(0, h), rng.integers(0, b)
        r = rng.integers(max(4, b // 130), max(8, b // 40))
        y0, y1, x0, x1 = max(0, cy - 3 * r), min(h, cy + 3 * r), max(0, cx - 3 * r), min(b, cx + 3 * r)
        gy, gx = np.mgrid[y0:y1, x0:x1]
        a = np.exp(-((gy - cy) ** 2 + (gx - cx) ** 2) / (2.0 * r * r)) * rng.uniform(0.10, 0.34)
        bild[y0:y1, x0:x1] = bild[y0:y1, x0:x1] * (1 - a[..., None]) + np.array([0.58, 0.42, 0.22]) * a[..., None]
    # Hadernreste: kurze dunkle und helle Faeden und Punkte
    for _ in range(int(130 * b * h / (1024 * 1536)) + 20):
        cy, cx = rng.integers(2, h - 2), rng.integers(2, b - 2)
        ang = rng.uniform(0, np.pi)
        ln = rng.integers(2, max(4, b // 38))
        dunkel = rng.random() < 0.65
        farbe = np.array([0.36, 0.27, 0.18]) if dunkel else np.array([0.97, 0.94, 0.85])
        for k in range(ln):
            py = int(cy + np.sin(ang) * k); px = int(cx + np.cos(ang) * k)
            if 0 <= py < h and 0 <= px < b:
                bild[py, px] = bild[py, px] * 0.45 + farbe * 0.55
    # Faserrand (Buettenrand): dunkler, ausgefranst
    rand = np.minimum.reduce([xx, b - 1 - xx, yy, h - 1 - yy]) / (b / 22.0)
    wirk = np.clip(1.0 - rand, 0, 1) ** 2 * (0.5 + 0.5 * vnoise(rng, h, b, 40, 28))
    bild = bild * (1 - 0.5 * wirk[..., None]) + np.array([0.36, 0.24, 0.12]) * 0.5 * wirk[..., None]
    # Falzlinien
    for v in v_falze:
        r = int(round(v * h))
        for dy, f in ((-3, 1.02), (-2, 1.035), (-1, 1.02), (0, 0.80), (1, 0.90), (2, 0.97), (3, 0.99)):
            ry = r + dy
            if 0 <= ry < h:
                bild[ry, :, :] = bild[ry, :, :] * f
    return np.clip(bild * 255 + 0.5, 0, 255).astype(np.uint8)


def bild_laden(name, arr):
    pfad = os.path.join(AUS, name + ".png")
    png_schreiben(pfad, arr)
    img = bpy.data.images.load(pfad)
    img.name = name
    img.colorspace_settings.name = "sRGB"
    img.pack()
    return img


def stoff(name, bild, rau, farbe=None, cull=False, nah=False):
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
        tx.interpolation = "Closest" if nah else "Linear"
        nt.links.new(tx.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (*farbe, 1)
    m.use_backface_culling = cull
    return m


# ------------------------------------------------------------------ Flaechenlisten (reine Rechnung) und Netzbau
def flaeche_rec(cos, uvs, mat, g, erw):
    return {"cos": cos, "uvs": uvs, "mat": mat, "g": g, "erw": erw}


class Netzbau:
    def __init__(self):
        self.bm = bmesh.new()
        self.uvl = self.bm.loops.layers.uv.new("UVMap")
        self.gr = {}

    def emit(self, recs):
        for r in recs:
            vs = [self.bm.verts.new(c) for c in r["cos"]]
            uvs = list(r["uvs"])
            n = Vector((0, 0, 0))
            co = r["cos"]
            for i in range(len(co)):
                a, b = co[i], co[(i + 1) % len(co)]
                n += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
            if n.dot(r["erw"]) < 0:
                vs = vs[::-1]
                uvs = uvs[::-1]
            f = self.bm.faces.new(vs)
            f.material_index = r["mat"]
            f.smooth = True
            for lp, uv in zip(f.loops, uvs):
                lp[self.uvl].uv = uv
            for v in vs:
                self.gr[v] = r["g"]

    def fertig(self, name, materialien, sc):
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces[:])
        self.bm.verts.index_update()
        gruppen = [self.gr[v] for v in self.bm.verts]
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in materialien:
            me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        sc.collection.objects.link(ob)
        return ob, gruppen


def papier_recs(ms):
    """Die drei Zellen des Briefes im Zustand 'zu' (gefaltet). Ränder unregelmaessig (Buettenrand), Falzkanten gerade."""
    rng = np.random.default_rng(1400)
    jl = rng.uniform(-1, 1, 400) * ms.amp
    jr = rng.uniform(-1, 1, 400) * ms.amp
    jo = rng.uniform(-1, 1, 80) * ms.amp
    ju = rng.uniform(-1, 1, 80) * ms.amp
    sub = ms.step / ms.unterteil
    nx = 6 * ms.unterteil
    W, T = ms.W, ms.T

    def idx(z):
        return int(round(z / sub)) + 200

    def umfang(za, zb, unten_rand, oben_rand):
        nz = int(round((zb - za) / sub))
        pts = []
        if unten_rand:
            for i in range(nx + 1):
                x = -W / 2 + W * i / nx
                x += jl[idx(za)] if i == 0 else (jr[idx(za)] if i == nx else 0)
                pts.append((x, za + ju[i]))
        else:
            pts += [(-W / 2 + jl[idx(za)], za), (W / 2 + jr[idx(za)], za)]
        for k in range(1, nz):
            z = za + k * sub
            pts.append((W / 2 + jr[idx(z)], z))
        if oben_rand:
            for i in range(nx, -1, -1):
                x = -W / 2 + W * i / nx
                x += jr[idx(zb)] if i == nx else (jl[idx(zb)] if i == 0 else 0)
                pts.append((x, zb + jo[i]))
        else:
            pts += [(W / 2 + jr[idx(zb)], zb), (-W / 2 + jl[idx(zb)], zb)]
        for k in range(nz - 1, 0, -1):
            z = za + k * sub
            pts.append((-W / 2 + jl[idx(z)], z))
        return pts

    zk = ms.HM / 2
    zellen = (("Falz_oben", zk, zk + ms.FZ, False, True, ms.flap("o", 1.0)),
              ("Streck", -zk, zk, False, False, Matrix.Identity(4)),
              ("Falz_unten", -zk - ms.FZ, -zk, True, False, ms.flap("u", 1.0)))
    recs = []
    for g, za, zb, ur, orr, M in zellen:
        pts = umfang(za, zb, ur, orr)
        R3 = M.to_3x3()
        cx = sum(p[0] for p in pts) / len(pts)
        cz = sum(p[1] for p in pts) / len(pts)

        def uv_vorn(x, z):
            return (min(1.0, max(0.0, (x + W / 2) / W)), min(1.0, max(0.0, 1.0 - (z + ms.H0 / 2) / ms.H0)))

        def uv_hinten(x, z):
            u, v = uv_vorn(x, z)
            return (1.0 - u, v)
        recs.append(flaeche_rec([M @ Vector((x, -T / 2, z)) for x, z in pts], [uv_vorn(x, z) for x, z in pts], 0, g, R3 @ Vector((0, -1, 0))))
        recs.append(flaeche_rec([M @ Vector((x, T / 2, z)) for x, z in pts], [uv_hinten(x, z) for x, z in pts], 0, g, R3 @ Vector((0, 1, 0))))
        for i in range(len(pts)):
            (x0, z0), (x1, z1) = pts[i], pts[(i + 1) % len(pts)]
            mx, mz = (x0 + x1) / 2, (z0 + z1) / 2
            erw = R3 @ Vector((mx - cx, 0, mz - cz))
            cos = [M @ Vector((x0, -T / 2, z0)), M @ Vector((x1, -T / 2, z1)), M @ Vector((x1, T / 2, z1)), M @ Vector((x0, T / 2, z0))]
            uvs = [uv_vorn(x0, z0), uv_vorn(x1, z1), uv_vorn(x1, z1), uv_vorn(x0, z0)]
            recs.append(flaeche_rec(cos, uvs, 0, g, erw))
    return recs


SIEGEL_PROFIL = [(1.0, 0.0), (1.07, 0.35), (1.02, 0.75), (0.88, 0.97), (0.78, 1.0)]   # (Radius / R, Hoehe / h_siegel): Wulst, flache Oberseite


def siegel_recs(ms, modus):
    """Wachssiegel auf der freien Kante des oberen Falzes. modus 'ganz' (kleiner Brief, ein Stueck) oder 'halb' (grosser Brief: Siegel_oben / Siegel_unten, an der Kante geteilt)."""
    cz = ms.zfrei
    R = ms.R
    seg_halb = 12 if modus == "halb" else 24
    recs = []

    def pos(r, a, h):
        return Vector((r * math.cos(a), ms.y_unten_vorn - h * ms.h_siegel, cz + r * math.sin(a)))

    innen = Vector((0, ms.y_unten_vorn - ms.h_siegel / 2, cz))
    haelften = (("Siegel_oben", 0.0, math.pi), ("Siegel_unten", math.pi, 2 * math.pi)) if modus == "halb" else ((None, 0.0, 2 * math.pi),)
    for g, a0, a1 in haelften:
        seg = seg_halb
        for k in range(seg):
            ka, kb = a0 + (a1 - a0) * k / seg, a0 + (a1 - a0) * (k + 1) / seg
            for j in range(len(SIEGEL_PROFIL) - 1):
                (r0, h0), (r1, h1) = SIEGEL_PROFIL[j], SIEGEL_PROFIL[j + 1]
                cos = [pos(r0 * R, ka, h0), pos(r0 * R, kb, h0), pos(r1 * R, kb, h1), pos(r1 * R, ka, h1)]
                mid = sum(cos, Vector((0, 0, 0))) / 4
                recs.append(flaeche_rec(cos, [(0, 0)] * 4, 0, g, mid - innen))
            rl, hl = SIEGEL_PROFIL[-1]
            cos = [pos(rl * R, ka, hl), pos(rl * R, kb, hl), pos(0, 0, hl)]
            recs.append(flaeche_rec(cos, [(0, 0)] * 3, 0, g, Vector((0, -1, 0))))
        if modus == "halb":
            # Schnittflaeche (liegt in der Ebene z = cz): Umriss des Profils von -R bis +R
            pol = [(-p[0] * R, p[1]) for p in SIEGEL_PROFIL] + [(p[0] * R, p[1]) for p in reversed(SIEGEL_PROFIL)]
            cos = [Vector((x, ms.y_unten_vorn - h * ms.h_siegel, cz)) for x, h in pol]
            erw = Vector((0, 0, -1 if g == "Siegel_oben" else 1))
            recs.append(flaeche_rec(cos, [(0, 0)] * len(cos), 0, g, erw))
    return recs


def symbol_recs(ms):
    """Symbolflaeche: Quad auf der flachen Oberseite des Siegels, UV 0..1 (u nach rechts, v nach unten), 0,8 mm darueber (kein Z-Fighting)."""
    s = ms.R * 1.06 / 2
    y = ms.y_top - 0.0008 * (ms.W / 0.30) ** 0.5
    cz = ms.zfrei
    cos = [Vector((-s, y, cz + s)), Vector((s, y, cz + s)), Vector((s, y, cz - s)), Vector((-s, y, cz - s))]
    return [flaeche_rec(cos, [(0, 0), (1, 0), (1, 1), (0, 1)], 0, "Siegel_Symbol", Vector((0, -1, 0)))], s * 2


def inhalt_recs(ms):
    """Inhaltsflaeche des offenen Blattes: Quad in der Ebene y = -T/2 - 1 mm, mittig, UV 0..1; Hoehe fuer die kleinste Blatthoehe."""
    b = ms.W - 2 * ms.rand
    h = ms.H0 - 2 * ms.rand
    y = -ms.T / 2 - 0.001
    cos = [Vector((-b / 2, y, h / 2)), Vector((b / 2, y, h / 2)), Vector((b / 2, y, -h / 2)), Vector((-b / 2, y, -h / 2))]
    return [flaeche_rec(cos, [(0, 0), (1, 0), (1, 1), (0, 1)], 0, None, Vector((0, -1, 0)))], (b, h)


def umriss_bauen(quelle, name, abstand, m_um, sc):
    """Wie Handschuh und Schriftrolle: Kopie, Ecken entlang der (an gleichen Orten gemittelten) Normalen verschoben, Flaechen gewendet."""
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


def dreiecke(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def neue_szene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    sc.unit_settings.system = "METRIC"
    return sc


def v_falze(ms):
    zk = ms.HM / 2
    return [1.0 - (zk + ms.H0 / 2) / ms.H0, 1.0 - (-zk + ms.H0 / 2) / ms.H0]


def materialien(ms, klein):
    px = (512, 768) if klein else (1024, 1536)
    img = bild_laden(ms.textur, textur_papier(px[0], px[1], 1481 if klein else 1480, v_falze(ms)))
    m_pap = stoff("Brief_Papier", img, 0.9)
    m_sieg = stoff("Brief_Siegel", None, 0.42, farbe=(0.60, 0.09, 0.07))
    m_sym = stoff("Brief_Siegel_Symbol", None, 0.5, farbe=(0.86, 0.66, 0.58))
    m_inh = stoff("Brief_Inhalt", None, 0.9, farbe=(0.90, 0.86, 0.74))
    m_um = stoff("Umriss", None, 1.0, farbe=(0.02, 0.012, 0.008), cull=True)
    return m_pap, m_sieg, m_sym, m_inh, m_um


def exportieren(objs, arm, pfad, animiert):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    if arm is not None:
        arm.select_set(True)
        bpy.context.view_layer.objects.active = arm
    else:
        bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.gltf(filepath=pfad, export_format="GLB", use_selection=True, export_animations=animiert,
                              export_animation_mode="ACTIONS", export_skins=arm is not None, export_yup=True, export_apply=False,
                              export_image_format="AUTO", export_force_sampling=True, export_frame_range=False,
                              export_optimize_animation_size=True)
    print("GLB", pfad, os.path.getsize(pfad))


mess = {}

# ================================================================== KLEINER FALTBRIEF
sc = neue_szene()
ms = KLEIN
m_pap, m_sieg, m_sym, m_inh, m_um = materialien(ms, True)
nb_p, nb_s, nb_y, nb_a = Netzbau(), Netzbau(), Netzbau(), Netzbau()
pr = papier_recs(ms)
sr = siegel_recs(ms, "ganz")
yr, sym_seite = symbol_recs(ms)
nb_p.emit(pr); nb_s.emit(sr); nb_y.emit(yr); nb_a.emit(pr); nb_a.emit(sr)
ob_p, _ = nb_p.fertig("Brief_klein", [m_pap], sc)
ob_s, _ = nb_s.fertig("Siegel", [m_sieg], sc)
ob_y, _ = nb_y.fertig("Siegel_Symbol", [m_sym], sc)
ob_a, _ = nb_a.fertig("Brief_klein_alle", [m_pap], sc)
ob_u = umriss_bauen(ob_a, "Brief_klein_Umriss", ms.umriss, m_um, sc)
bpy.data.objects.remove(ob_a)
d_k = {o.name: dreiecke(o) for o in (ob_p, ob_s, ob_y, ob_u)}
d_k["gesamt"] = sum(d_k.values())
pts = np.array([v.co[:] for o in (ob_p, ob_s) for v in o.data.vertices])
umri = np.array([v.co[:] for v in ob_u.data.vertices])
mess["klein"] = {"dreiecke": d_k, "papier_min": [round(float(x), 4) for x in pts.min(axis=0)], "papier_max": [round(float(x), 4) for x in pts.max(axis=0)],
                 "umriss_min": [round(float(x), 4) for x in umri.min(axis=0)], "umriss_max": [round(float(x), 4) for x in umri.max(axis=0)],
                 "symbolflaeche_m": [round(sym_seite, 4)] * 2, "siegel_radius": ms.R, "siegel_mitte": [0.0, round(ms.y_top, 5), round(ms.zfrei, 5)]}
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(AUS, "brief_klein.blend"))
exportieren([ob_p, ob_s, ob_y, ob_u], None, os.path.join(AUS, "brief_klein.glb"), False)

# ================================================================== GROSSER BRIEF
sc = neue_szene()
ms = GROSS
m_pap, m_sieg, m_sym, m_inh, m_um = materialien(ms, False)
pr = papier_recs(ms)
sr = siegel_recs(ms, "halb")
yr, sym_seite = symbol_recs(ms)
yr[0]["g"] = "Symbolknochen"
ir, (ib, ih) = inhalt_recs(ms)
nb_p, nb_s, nb_y, nb_a, nb_i = Netzbau(), Netzbau(), Netzbau(), Netzbau(), Netzbau()
nb_p.emit(pr); nb_s.emit(sr); nb_y.emit(yr); nb_a.emit(pr); nb_a.emit(sr); nb_i.emit(ir)
ob_p, gr_p = nb_p.fertig("Brief_gross", [m_pap], sc)
ob_s, gr_s = nb_s.fertig("Siegel", [m_sieg], sc)
ob_y, gr_y = nb_y.fertig("Siegel_Symbol", [m_sym], sc)
ob_a, gr_a = nb_a.fertig("Brief_gross_alle", [m_pap], sc)
ob_i, _ = nb_i.fertig("Inhaltsflaeche", [m_inh], sc)
ob_u = umriss_bauen(ob_a, "Brief_gross_Umriss", ms.umriss, m_um, sc)
bpy.data.objects.remove(ob_a)

# Geruest: Streck, Anker_oben, Anker_unten ohne Clipspur (der Code setzt sie), darunter die Falze und Siegelteile
BONES = ["Streck", "Anker_oben", "Anker_unten", "Falz_oben", "Falz_unten", "Siegel_oben", "Siegel_unten", "Symbolknochen"]
PARENT = {"Falz_oben": "Anker_oben", "Siegel_oben": "Anker_oben", "Symbolknochen": "Anker_oben", "Falz_unten": "Anker_unten", "Siegel_unten": "Anker_oben"}
cz_s = ms.zfrei
# Streck und Anker: Knochen-Y zeigt nach Blender-Z (oben); nach dem glTF-Export ist die Ruhe in Godot die Einheitsmatrix, die Skala (1, s, 1) wirkt also auf Godot-Y (oben)
ACHSE = Matrix.Rotation(math.radians(90.0), 4, "X")
REST = {"Streck": ACHSE, "Anker_oben": ACHSE, "Anker_unten": ACHSE,
        "Falz_oben": ms.flap("o", 1.0), "Falz_unten": ms.flap("u", 1.0),
        "Siegel_oben": Matrix.Translation((0, ms.y_top, cz_s)), "Siegel_unten": Matrix.Translation((0, ms.y_top, cz_s)),
        "Symbolknochen": Matrix.Translation((0, ms.y_top, cz_s))}
arm_d = bpy.data.armatures.new("Brief_Geruest")
arm = bpy.data.objects.new("Brief_Geruest", arm_d)
sc.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
for nm in BONES:
    eb = arm_d.edit_bones.new(nm)
    eb.head = (0, 0, 0)
    eb.tail = (0, 0.02, 0)
    eb.matrix = REST[nm]
    eb.length = 0.02
for nm, p in PARENT.items():
    arm_d.edit_bones[nm].parent = arm_d.edit_bones[p]
    arm_d.edit_bones[nm].use_connect = False
bpy.ops.object.mode_set(mode="OBJECT")
for nm in BONES:
    abw = arm_d.bones[nm].matrix_local - REST[nm]
    m = max(abs(x) for row in abw for x in row)
    if m > 1e-5:
        print("WARNUNG Knochenlage", nm, m)

for o, gr in ((ob_p, gr_p), (ob_s, gr_s), (ob_y, gr_y), (ob_u, gr_a)):
    for nm in BONES:
        o.vertex_groups.new(name=nm)
    for vi, nm in enumerate(gr):
        o.vertex_groups[nm].add([vi], 1.0, "REPLACE")
    o.parent = arm
    md = o.modifiers.new("Geruest", "ARMATURE")
    md.object = arm


# ------------------------------------------------------------------ Clips
def verlauf(punkte):
    """Schliessgrad c ueber die Zeit: Stuetzpunkte (Bild, c); zwischen je zwei Punkten Kosinus-Weiche (Steigung 0 an jedem Punkt) = gedaempftes Nachfedern."""
    def f(bild):
        for (b0, u0), (b1, u1) in zip(punkte, punkte[1:]):
            if b0 <= bild <= b1:
                x = (bild - b0) / (b1 - b0)
                return u0 + (u1 - u0) * (1.0 - math.cos(math.pi * x)) / 2.0
        return punkte[-1][1]
    return f


# auffalten: erst bricht das Siegel (Bild 0..9), der obere Falz hebt sich ab Bild 3 und schwingt kaum ueber flach (-1,5 %), der untere ab Bild 8 mit dem letzten, kleinen Nachfedern (-3 %, +1 %, -0,2 %)
AUF_O = [(0, 1.0), (3, 1.0), (14, -0.015), (19, 0.006), (23, -0.002), (27, 0.0)]
AUF_U = [(0, 1.0), (8, 1.0), (20, -0.030), (24, 0.010), (26, -0.002), (27, 0.0)]
# zufalten: kein Ueberschwingen; erst der untere Falz hoch, dann der obere darueber, zuletzt das Siegel
ZU_U = [(0, 0.0), (2, 0.0), (13, 1.0), (27, 1.0)]
ZU_O = [(0, 0.0), (8, 0.0), (19, 1.0), (27, 1.0)]
SIEGEL_FLUG = {"Siegel_oben": Vector((0.020, -0.045, 0.030)), "Siegel_unten": Vector((-0.020, -0.040, -0.030))}
SIEGEL_DREH = {"Siegel_oben": 40.0, "Siegel_unten": -40.0}   # Grad um Y


def kos(x):
    x = min(1.0, max(0.0, x))
    return (1.0 - math.cos(math.pi * x)) / 2.0


def siegel_zustand(clip, f):
    """(Lauf 0..1 weg vom Sitz, Groesse). auffalten: Bild 0..9 springt weg und schrumpft ab 40 %; zufalten: Bild 20..26 kommt zurueck und waechst."""
    if clip == "auffalten":
        t = min(1.0, f / 9.0)
        lauf = math.sin(math.pi * t / 2)
        gr = 1.0 if t < 0.4 else 1.0 - (1.0 - 0.001) * kos((t - 0.4) / 0.6)
        return lauf, gr
    if clip == "zufalten":
        t = min(1.0, max(0.0, (f - 20) / 6.0))
        k = kos(t)
        return 1.0 - math.sin(math.pi * k / 2), 0.001 + (1.0 - 0.001) * k
    return 0.0, 1.0


def symbol_groesse(clip, f):
    if clip == "auffalten":
        return 1.0 - (1.0 - 0.001) * kos(f / 2.0)
    if clip == "zufalten":
        return 0.001 + (1.0 - 0.001) * kos((f - 25) / 2.0)
    return 1.0


KURVEN = {"auffalten": (verlauf(AUF_O), verlauf(AUF_U)), "zufalten": (verlauf(ZU_O), verlauf(ZU_U)), "zu": (lambda f: 1.0, lambda f: 1.0)}


def pose_setzen(clip, f, key=True):
    co, cu = KURVEN[clip][0](f), KURVEN[clip][1](f)
    for nm, art, c in (("Falz_oben", "o", co), ("Falz_unten", "u", cu)):
        pb = arm.pose.bones[nm]
        B = REST[nm].inverted() @ ms.flap(art, c)
        loc, rot, scl = B.decompose()
        pb.rotation_mode = "QUATERNION"
        pb.location, pb.rotation_quaternion, pb.scale = loc, rot, scl
        if key:
            pb.keyframe_insert("location", frame=f, group=nm)
            pb.keyframe_insert("rotation_quaternion", frame=f, group=nm)
            pb.keyframe_insert("scale", frame=f, group=nm)
    lauf, gr = siegel_zustand(clip, f)
    for nm in ("Siegel_oben", "Siegel_unten"):
        pb = arm.pose.bones[nm]
        pb.rotation_mode = "QUATERNION"
        pb.location = SIEGEL_FLUG[nm] * lauf
        pb.rotation_quaternion = Matrix.Rotation(math.radians(SIEGEL_DREH[nm]) * lauf, 4, "Y").to_quaternion()
        pb.scale = (gr, gr, gr)
        if key:
            pb.keyframe_insert("location", frame=f, group=nm)
            pb.keyframe_insert("rotation_quaternion", frame=f, group=nm)
            pb.keyframe_insert("scale", frame=f, group=nm)
    pb = arm.pose.bones["Symbolknochen"]
    g = symbol_groesse(clip, f)
    pb.rotation_mode = "QUATERNION"
    pb.scale = (g, g, g)
    if key:
        pb.keyframe_insert("scale", frame=f, group="Symbolknochen")


try:
    bpy.context.preferences.edit.keyframe_new_interpolation_type = "LINEAR"
except Exception as e:
    print("INTERPOLATION", e)
arm.animation_data_create()
aktionen = {}


def clip(name, bilder):
    a = bpy.data.actions.new(name)
    a.use_fake_user = True
    arm.animation_data.action = a
    for f in range(bilder + 1):
        pose_setzen(name, f)
    a.frame_range = (0, bilder)
    a.use_frame_range = True
    a.use_cyclic = False
    aktionen[name] = a


clip("auffalten", CLIP_BILDER)
clip("zufalten", CLIP_BILDER)
clip("zu", 1)
arm.animation_data.action = aktionen["zu"]
sc.frame_set(0)


# ------------------------------------------------------------------ Messwerte
def netz_punkte(o):
    dg = bpy.context.evaluated_depsgraph_get()
    e = o.evaluated_get(dg)
    m = e.to_mesh()
    pts = np.array([(o.matrix_world @ v.co)[:] for v in m.vertices])
    e.to_mesh_clear()
    return pts


def hoehe_netz(o, frame, action):
    arm.animation_data.action = aktionen[action]
    sc.frame_set(frame)
    bpy.context.view_layer.update()
    return netz_punkte(o)


def streck_setzen(s):
    """Wie der Code im Spiel: Streck skaliert in Z, die Anker verschieben die Falze um +-(s-1)*HM/2."""
    d = (s - 1.0) * ms.HM / 2
    arm.pose.bones["Streck"].scale = (1, s, 1)
    arm.pose.bones["Anker_oben"].location = (0, d, 0)
    arm.pose.bones["Anker_unten"].location = (0, -d, 0)


mess["gross"] = {"dreiecke": {o.name: dreiecke(o) for o in (ob_p, ob_s, ob_y, ob_u, ob_i)}}
mess["gross"]["dreiecke"]["gesamt"] = sum(mess["gross"]["dreiecke"].values())
for zst, (fr, act) in (("zu", (0, "zu")), ("halb", (11, "auffalten")), ("offen", (CLIP_BILDER, "auffalten"))):
    pts = hoehe_netz(ob_p, fr, act)
    mess["gross"][zst] = {"papier_min": [round(float(x), 4) for x in pts.min(axis=0)], "papier_max": [round(float(x), 4) for x in pts.max(axis=0)]}
mess["gross"]["inhaltsflaeche_m"] = [round(ib, 4), round(ih, 4)]
ipts = netz_punkte(ob_i)
mess["gross"]["inhaltsflaeche_mitte"] = [round(float((a + b) / 2), 5) for a, b in zip(ipts.min(axis=0), ipts.max(axis=0))]
mess["gross"]["inhaltsflaeche_y"] = round(float(ipts[:, 1].mean()), 5)
mess["gross"]["siegel_radius"] = ms.R
mess["gross"]["symbolflaeche_m"] = [round(sym_seite, 4)] * 2

# Streck-Probe im offenen Zustand: Hoehe, Falze unverformt, Naht ohne Spalt
arm.animation_data.action = aktionen["auffalten"]
sc.frame_set(CLIP_BILDER)
streck = {}
ref = None
for s in (1.0, S_MAX):
    streck_setzen(s)
    bpy.context.view_layer.update()
    pts = netz_punkte(ob_p)
    gr_ = gr_p
    zonen = {}
    for nm in ("Falz_oben", "Streck", "Falz_unten"):
        sel = np.array([i for i, g in enumerate(gr_) if g == nm])
        zonen[nm] = pts[sel]
    streck[s] = zonen
    mess["gross"][f"streck_{s:g}"] = {"hoehe": round(float(pts[:, 2].max() - pts[:, 2].min()), 4),
                                      "breite": round(float(pts[:, 0].max() - pts[:, 0].min()), 4),
                                      "tiefe": round(float(pts[:, 1].max() - pts[:, 1].min()), 4),
                                      "z_mitte_oben": round(float(zonen["Streck"][:, 2].max()), 5), "z_mitte_unten": round(float(zonen["Streck"][:, 2].min()), 5),
                                      "z_falz_oben_unten": round(float(zonen["Falz_oben"][:, 2].min()), 5), "z_falz_unten_oben": round(float(zonen["Falz_unten"][:, 2].max()), 5)}
a, b = streck[1.0], streck[S_MAX]
verz = {}
for nm in ("Falz_oben", "Falz_unten"):
    # Falz nur verschoben: Abstaende der Ecken zueinander gleich, X und Y unveraendert
    d0 = a[nm] - a[nm].mean(axis=0)
    d1 = b[nm] - b[nm].mean(axis=0)
    verz[nm] = round(float(np.abs(d0 - d1).max()), 7)
mess["gross"]["streck_falz_verzerrung_m"] = verz
mess["gross"]["streck_mitte_faktor"] = round(float((b["Streck"][:, 2].max() - b["Streck"][:, 2].min()) / (a["Streck"][:, 2].max() - a["Streck"][:, 2].min())), 5)
mess["gross"]["streck_mitte_xy_aenderung_m"] = round(float(np.abs(b["Streck"][:, :2] - a["Streck"][:, :2]).max()), 7)
for nm in ("Streck", "Anker_oben", "Anker_unten"):
    arm.pose.bones[nm].scale = (1, 1, 1)
    arm.pose.bones[nm].location = (0, 0, 0)
arm.animation_data.action = aktionen["zu"]
sc.frame_set(0)


# Neigung: in jedem Bild jedes Clips muessen die Mitte (Streck) und die X-Achsen der Falze ungedreht bleiben (Falze drehen nur um X-parallele Kanten)
def winkel(a_, b_):
    return math.degrees(math.acos(max(-1.0, min(1.0, a_.dot(b_)))))


schlimm_mitte = 0.0
schlimm_achse = 0.0
flach = {}
for name in ("auffalten", "zufalten", "zu"):
    arm.animation_data.action = aktionen[name]
    flach[name] = []
    for f in range(int(aktionen[name].frame_range[1]) + 1):
        sc.frame_set(f)
        bpy.context.view_layer.update()
        m3 = arm.pose.bones["Streck"].matrix.to_3x3()
        schlimm_mitte = max(schlimm_mitte, winkel(m3 @ Vector((0, 1, 0)), Vector((0, 0, 1))))
        w = 0.0
        for nm in ("Falz_oben", "Falz_unten"):
            bm3 = arm.pose.bones[nm].matrix.to_3x3()
            schlimm_achse = max(schlimm_achse, winkel(bm3 @ Vector((1, 0, 0)), Vector((1, 0, 0))))
            # Winkel der Papiernormalen zu -Y: aus der Matrix der Zelle im Raum (Rest-Normale * Drehung)
        co, cu = KURVEN[name][0](f), KURVEN[name][1](f)
        w = max(abs(co), abs(cu)) * 180.0   # Grad Abweichung von flach (Schliessgrad * 180)
        flach[name].append(w)
mess["gross"]["neigung_mitte_max_grad"] = round(schlimm_mitte, 4)
mess["gross"]["falz_achse_max_grad"] = round(schlimm_achse, 4)
mess["gross"]["ueberschwinger_grad"] = {"auffalten_oben": round(abs(min(c for _, c in AUF_O)) * 180, 1), "auffalten_unten": round(abs(min(c for _, c in AUF_U)) * 180, 1), "zufalten": 0.0}
mess["gross"]["flach_toleranz_grad"] = 2.0
fl = [w <= 2.0 for w in flach["auffalten"]]
ab = next(f for f in range(CLIP_BILDER + 1) if all(fl[f:]))
fz = [w <= 2.0 for w in flach["zufalten"]]
bis = max(f for f in range(CLIP_BILDER + 1) if all(fz[:f + 1]))
sich_auf = max(f for f in range(CLIP_BILDER + 1) if siegel_zustand("auffalten", f)[1] > 0.05)
sich_zu = min(f for f in range(CLIP_BILDER + 1) if siegel_zustand("zufalten", f)[1] > 0.05)
sym_auf = max(f for f in range(CLIP_BILDER + 1) if symbol_groesse("auffalten", f) > 0.05)
sym_zu = min(f for f in range(CLIP_BILDER + 1) if symbol_groesse("zufalten", f) > 0.05)
mess["gross"]["zeiten_s"] = {"clip_dauer": round(CLIP_BILDER / FPS, 3),
                             "auffalten_inhaltsflaeche_sichtbar_ab": round(ab / FPS, 3), "zufalten_inhaltsflaeche_sichtbar_bis": round(bis / FPS, 3),
                             "auffalten_siegel_sichtbar_bis": round(sich_auf / FPS, 3), "zufalten_siegel_sichtbar_ab": round(sich_zu / FPS, 3),
                             "auffalten_symbol_sichtbar_bis": round(sym_auf / FPS, 3), "zufalten_symbol_sichtbar_ab": round(sym_zu / FPS, 3)}
mess["gross"]["clips"] = {"auffalten_oben": AUF_O, "auffalten_unten": AUF_U, "zufalten_oben": ZU_O, "zufalten_unten": ZU_U}
mess["gross"]["streck"] = {"s_min": 1.0, "s_max": S_MAX, "hoehe_min": round(ms.H0, 4), "hoehe_max": round(2 * ms.FZ + ms.HM * S_MAX, 4), "mitte_min": ms.HM, "mitte_max": round(ms.HM * S_MAX, 4), "falz": ms.FZ}
mess["gross"]["masse"] = {"breite": ms.W, "dicke_lage": ms.T, "lagenabstand": ms.P, "falz_hoehe": ms.FZ, "mitte_hoehe": ms.HM}
arm.animation_data.action = aktionen["zu"]
sc.frame_set(0)
print("MESSWERTE", json.dumps(mess))
with open(os.path.join(AUS, "messwerte_brief.json"), "w", encoding="utf-8") as fh:
    json.dump(mess, fh, indent=1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(AUS, "brief_gross.blend"))
exportieren([ob_p, ob_s, ob_y, ob_u, ob_i], arm, os.path.join(AUS, "brief_gross.glb"), True)
