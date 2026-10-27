# Haarkarten (Frisuren, Bart) für den Live-Kopf: eigene Streifen mit Alpha-Textur, am Kopfnetz verankert.
# Alles selbst erzeugt (CC0): Strähnen-Textur wird hier berechnet, die Karten laufen entlang der Kopfhaut.
import sys, math, random, os
import numpy as np
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

E = sys.modules["__main__"]


def strahnen_textur(pfad, breite=512, hoehe=512, locken=6, seed=3):
    """Haarsträhnen als Locken: breite, spitz auslaufende Bänder mit feinen helleren/dunkleren Haarstrichen.
    Alpha = Lockenform (harte Kante, für Alpha-Scissor), Rot = Helligkeit, Wurzel (oben im Bild unten) dunkler."""
    rng = np.random.default_rng(seed)
    alpha = np.zeros((hoehe, breite), np.float32)
    hell = np.zeros((hoehe, breite), np.float32)
    yy = (np.arange(hoehe)[:, None] / hoehe).astype(np.float32)       # 0 = Wurzel, 1 = Spitze
    xx = np.arange(breite)[None, :].astype(np.float32)
    for i in range(locken):
        x0 = (i + 0.5) / locken * breite + rng.uniform(-10, 10)
        laenge = rng.uniform(0.80, 1.0)
        w0 = rng.uniform(0.55, 0.9) * breite / locken * 0.62
        mitte = x0 + rng.uniform(3, 11) * np.sin(yy * rng.uniform(2.0, 4.0) * 6.28 + rng.uniform(0, 6.28)) + yy * rng.uniform(-18, 18)
        u = np.clip(yy / laenge, 0, 1)
        halb = w0 * np.minimum(0.45 + 2.2 * u, 1.0) * (1.0 - 0.92 * np.clip((u - 0.35) / 0.65, 0, 1) ** 1.5)
        a = (np.abs(xx - mitte) < halb) & (yy < laenge)
        a = a.astype(np.float32)
        # feine Haarstriche in der Locke
        dichte = np.zeros_like(alpha)
        for k in range(14):
            off = rng.uniform(-1, 1)
            lin = mitte + off * halb * 0.95 + 2.0 * np.sin(yy * rng.uniform(5, 14) + k)
            dichte += np.clip(1.0 - np.abs(xx - lin) / rng.uniform(0.8, 1.8), 0, 1) * rng.uniform(0.3, 1.0)
        h = 0.55 + 0.35 * np.clip(dichte, 0, 1) - 0.15 * np.clip(np.abs(xx - mitte) / np.maximum(halb, 1), 0, 1)
        neu = a > alpha
        hell = np.where(neu, h, hell)
        alpha = np.maximum(alpha, a)
        # einzelne abstehende Härchen neben der Locke
        for k in range(3):
            lin = mitte + rng.choice([-1, 1]) * (halb + rng.uniform(2, 7)) + yy * rng.uniform(-14, 14)
            hh = (np.abs(xx - lin) < 1.4) & (yy < laenge * rng.uniform(0.4, 0.9)) & (yy > 0.08)
            hell = np.where(hh & (alpha < 0.5), 0.85, hell)
            alpha = np.where(hh, 1.0, alpha)
    bild = np.zeros((hoehe, breite, 4), np.float32)
    bild[:, :, 0] = hell; bild[:, :, 1] = hell; bild[:, :, 2] = hell
    bild[:, :, 3] = alpha
    img = bpy.data.images.new("haarkarte", breite, hoehe, alpha=True)
    img.pixels = bild.ravel().tolist()      # Zeile 0 = unten im Bild = Wurzel (v = 0)
    img.filepath_raw = pfad
    img.file_format = "PNG"
    img.save()
    return img


class Kopf:
    def __init__(self, b, M, LM):
        self.b = b
        self.M = M
        me = b.data
        verts = [b.matrix_world @ v.co for v in me.vertices]
        polys = [tuple(p.vertices) for p in me.polygons]
        self.bvh = BVHTree.FromPolygons(verts, polys)
        zentral = [v for v in verts if abs(v.x) < 0.004 and v.z > 1.4]
        spitze = min(zentral, key=lambda v: v.y)                 # Nasenspitze
        oben = max(v.z for v in verts)
        hinten = max(v.y for v in verts if abs(v.x) < 0.03 and oben - 0.12 < v.z < oben - 0.02)
        nz, ny = spitze.z, spitze.y
        self.nase = Vector((0, ny, nz))
        self.augen = Vector((0, ny + 0.03, nz + 0.045))
        self.mund = Vector((0, ny + 0.017, nz - 0.026))
        self.kinn_z = nz - 0.076
        self.hals = Vector((0, (ny + hinten) / 2 + 0.01, self.kinn_z))
        self.mitte = Vector((0, (ny + hinten) / 2 + 0.01, nz + 0.07))      # Schädelmitte (Innenpunkt für Strahlen)
        print("KOPF Nase", self.nase, "Oben", oben, "Hinten", hinten)

    def treffer(self, ursprung, richtung):
        return self.bvh.ray_cast(ursprung, richtung.normalized())

    def draussen_halten(self, p, abstand=0.008):
        """Punkt aus dem Körpernetz herausschieben, falls zu nah oder darin."""
        hit = self.bvh.find_nearest(p)
        if hit[0] is None:
            return p
        loc, n, _, d = hit
        innen = (p - loc).dot(n) < 0
        if innen or d < abstand:
            return loc + n * abstand
        return p


def streifen_bauen(name, streifen, material_name):
    """streifen: Liste von Streifen; jeder Streifen: Liste von (Mitte, Breitenvektor, u0, u1, v)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for s in streifen:
        reihe = []
        for (m, w, u0, u1, v) in s:
            a = bm.verts.new(m - w / 2); c = bm.verts.new(m + w / 2)
            reihe.append((a, c, u0, u1, v))
        for (a0, c0, u00, u10, v0), (a1, c1, u01, u11, v1) in zip(reihe[:-1], reihe[1:]):
            f = bm.faces.new((a0, c0, c1, a1))
            for lp, (uu, vv) in zip(f.loops, [(u00, v0), (u10, v0), (u11, v1), (u01, v1)]):
                lp[uv].uv = (uu, vv)
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    mat = bpy.data.materials.new(material_name)
    mat.use_nodes = True
    o.data.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = True
    return o


def haar_kappe(k, lang=False, seed=1, dichte=1.0):
    """Kurz: Karten über den Schädel von der Krone zum Haaransatz. Lang: zusätzlich Strähnen bis auf die Schultern."""
    rng = random.Random(seed)
    c = k.mitte
    streifen = []

    def theta_max(phi):  # Haaransatz: vorn hoch (Stirn), seitlich über dem Ohr, hinten tief (Nacken)
        f = (1 - math.cos(phi + math.pi / 2)) / 2    # phi=-90° (vorn, -Y) -> 0, hinten -> 1
        return math.radians(54 + 56 * f ** 0.6)

    def punkt(theta, phi, aus):
        d = Vector((math.sin(theta) * math.cos(phi), math.sin(theta) * math.sin(phi), math.cos(theta)))
        h = k.treffer(c, d)
        if h[0] is None:
            return None, None
        return h[0] + h[1] * aus, h[1]

    schichten = [(0.0, 0.50, 0.002, 0.008), (0.25, 0.78, 0.006, 0.014), (0.50, 1.02, 0.010, 0.020), (0.75, 1.05, 0.014, 0.024)]
    breite = 0.034
    for (a, bz, o0, o1) in schichten:
        for ring in range(0, 7):
            th0 = math.radians(4 + ring * 12 * 1.0)
            if ring == 0:
                n = 5
            else:
                n = max(6, int(2 * math.pi * math.sin(th0) * 0.09 / (breite * 0.8) * dichte))
            for i in range(n):
                phi = 2 * math.pi * (i + rng.uniform(-0.3, 0.3)) / n + (a * 3)
                phi = (phi + math.pi) % (2 * math.pi) - math.pi
                tm = theta_max(phi) + math.radians(rng.uniform(-9, 5))
                if th0 > tm:
                    continue
                t_end = tm + math.radians(8) * bz
                t_start = th0 + (t_end - th0) * a * 0.0
                seg = 5
                pts = []
                for s in range(seg + 1):
                    t = s / seg
                    th = t_start + (t_end - t_start) * t
                    aus = o0 + (o1 - o0) * t + (0.006 * t * t if lang else 0.0)
                    p, nrm = punkt(max(th, 0.02), phi, aus)
                    if p is None:
                        break
                    pts.append((p, nrm, t))
                if len(pts) < seg + 1:
                    continue
                if lang and tm > math.radians(75) and (math.sin(phi) > -0.2 or True):
                    pass
                u0 = rng.uniform(0, 0.4)
                bahn = []
                for j, (p, nrm, t) in enumerate(pts):
                    # Breitenrichtung quer zur Laufrichtung, in der Tangentialebene
                    j1 = min(j + 1, len(pts) - 1); j0 = max(j - 1, 0)
                    lauf = (pts[j1][0] - pts[j0][0]).normalized()
                    w = lauf.cross(nrm).normalized() * breite * (1.0 - 0.25 * t)
                    bahn.append((p, w, u0, u0 + 0.6, 0.05 + 0.9 * t))
                streifen.append(bahn)
    if lang:
        streifen += lange_straehnen(k, rng, theta_max, punkt)
    return streifen


def lange_straehnen(k, rng, theta_max, punkt):
    """Strähnen, die am Haaransatz des Hinterkopfes und der Seiten beginnen und auf die Schultern fallen."""
    s_alle = []
    ziel_z = k.hals.z - 0.13
    breite = 0.036
    for phi_grad in range(-150, 151, 12):
        phi = math.radians(phi_grad)
        if -140 < phi_grad < -40:
            continue    # nicht vor dem Gesicht (vorn = -Y = phi -90 Grad)
        for lage in range(2):
            ph = phi + math.radians(rng.uniform(-3, 3))
            th = theta_max(ph) - math.radians(14 - 6 * lage)
            p0, n0 = punkt(th, ph, 0.012 + 0.008 * lage)
            if p0 is None:
                continue
            bahn = []
            seg = 7
            lage_off = 0.012 + 0.010 * lage
            for s in range(seg + 1):
                t = s / seg
                z = p0.z + (ziel_z - p0.z) * t
                # entlang des Rückens nach unten, seitlich vom Hals weg
                r = Vector((p0.x - k.hals.x, p0.y - k.hals.y, 0))
                rl = r.length
                r.normalize()
                p = Vector((k.hals.x, k.hals.y, 0)) + r * (rl + 0.01 * math.sin(t * math.pi) + 0.02 * t) + Vector((0, 0, z))
                p = k.draussen_halten(p, 0.014 + lage_off * 0.5)
                bahn.append([p, t])
            lst = []
            u0 = rng.uniform(0, 0.4)
            for j, (p, t) in enumerate(bahn):
                j1 = min(j + 1, len(bahn) - 1); j0 = max(j - 1, 0)
                lauf = (bahn[j1][0] - bahn[j0][0]).normalized()
                nrm = Vector((p.x - k.hals.x, p.y - k.hals.y, 0)).normalized()
                w = lauf.cross(nrm).normalized() * breite * (1.0 - 0.2 * t)
                lst.append((p, w, u0, u0 + 0.6, 0.15 + 0.85 * t))
            s_alle.append(lst)
    return s_alle


def bart_karten(k, seed=5):
    rng = random.Random(seed)
    m = k.mund
    streifen = []

    def haut(x, z):
        h = k.treffer(Vector((x, m.y - 0.4, z)), Vector((0, 1, 0)))
        return (h[0], h[1]) if h[0] is not None else (None, None)

    def strahne(x, z, laenge, aus_x, drift_y, breite):
        p0, n0 = haut(x, z)
        if p0 is None:
            return
        seg = 5
        bahn = []
        u0 = rng.uniform(0, 0.4)
        for s in range(seg + 1):
            t = s / seg
            wel = 0.004 * math.sin(t * 5.0 + p0.x * 90.0) * t
            p = p0 + n0 * (0.002 + 0.012 * t) + Vector((aus_x * t * laenge + wel, -drift_y * t * t * laenge, -t * laenge))
            p = k.draussen_halten(p, 0.007)
            bahn.append((p, t))
        lst = []
        for j, (p, t) in enumerate(bahn):
            j1 = min(j + 1, len(bahn) - 1); j0 = max(j - 1, 0)
            lauf = (bahn[j1][0] - bahn[j0][0]).normalized()
            nrm = n0 if n0.y < 0 else Vector((0, -1, 0))
            w = lauf.cross(nrm).normalized() * breite * (1 - 0.3 * t)
            lst.append((p, w, u0, u0 + 0.6, 0.1 + 0.8 * t))
        streifen.append(lst)

    # Vollbart: Raster über Wangen, Kinn; Länge wächst zum Kinn hin
    dx, dz = 0.014, 0.012
    x = -0.082
    while x <= 0.0821:
        z = m.z + 0.050
        while z >= m.z - 0.078:
            ax = abs(x)
            innen_mund = ax < 0.030 and (m.z - 0.014) < z < (m.z + 0.006)
            in_wange = ((ax / 0.082) ** 2 + ((z - (m.z - 0.005)) / 0.074) ** 2) < 1.0 and z < m.z + max(0.006, 0.05 - 1.1 * (0.075 - min(ax, 0.075)))
            ueber_wange = z > m.z + 0.01 and ax > 0.052 and z < m.z + 0.05      # Koteletten
            if in_wange and not innen_mund:
                kinn = max(0.0, (m.z - z) / 0.07) * max(0.0, 1.0 - ax / 0.06)
                laenge = 0.03 + 0.06 * kinn + rng.uniform(-0.008, 0.008)
                strahne(x + rng.uniform(-0.004, 0.004), z + rng.uniform(-0.004, 0.004), laenge,
                        (0.2 if x > 0 else -0.2) * (1 - kinn), 0.2 + 0.5 * kinn, 0.03)
            z -= dz
        x += dx
    # Schnurrbart: kurze Karten über der Oberlippe, nach außen unten
    for i in range(-5, 6):
        x = i * 0.0062
        strahne(x, m.z + 0.011, 0.020, 0.7 if x > 0 else -0.7 if x < 0 else 0.0, 0.1, 0.014)
        strahne(x * 0.8, m.z + 0.020, 0.016, 0.9 if x > 0 else -0.9 if x < 0 else 0.0, 0.0, 0.012)
    return streifen


def weiter(b, a, dna, M, S, LM):
    ordner = os.path.dirname(a["aus"])
    tex = os.path.join(ordner, "haarkarte.png")
    strahnen_textur(tex)
    k = Kopf(b, M, LM)
    teile = {"haar_kurz": haar_kappe(k, False, seed=1), "haar_lang": haar_kappe(k, True, seed=2),
             "bart": bart_karten(k)}
    basis, deltas = E.keys_lesen(b)
    objs = []
    for name, st in teile.items():
        o = streifen_bauen(name, st, "mat_" + name)
        o.parent = b
        E.keys_uebertragen(o, basis, deltas)
        o.data.calc_loop_triangles()
        print("KARTEN", name, len(st), "Streifen", len(o.data.loop_triangles), "Dreiecke")
        objs.append(o)
    E.weiter2(b, a, dna)
