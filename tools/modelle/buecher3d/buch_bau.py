"""Gemeinsame Bauteile der drei 3D-Buecher fuer die Innenansichten (Personalbuch, Familienbuch, Kontorbuch).
  blender -b --python buch_bau.py -- <ausordner> <personal|familie|kontor|schwarz|alle>
(unter Windows ueber tools/modelle/handschuh/lauf.ps1: Idle-Prioritaet; Pfade absolut.)
Schreibt <ausordner>/buch_<art>.glb (mit Clips), buch_<art>.blend und messwerte_buch_<art>.json.

Machart wie Schriftrolle und Handschuh: Gewichte 1,0 auf Knochen (Ausnahme: das Blatt, siehe unten), Umriss als invertierte Huelle (Netz "Buch_Umriss",
Material "Umriss"), nur Albedo-Texturen (selbst gemalt, numpy), die Texturen sind in der .glb eingebettet. Die Formen stehen in buch_stile.py.

Aufbau (Meter, Blender: Z oben, Blick von vorn = -Y, Seitenkopf nach +Y; Ursprung = Mitte des Buchs auf der Auflage, Unterseite bei z = 0):
  - Das Buch liegt flach, Ruecken links (-X). Es hat keine eigene Neigung; die Schraege des Pults steckt im Knoten Buch_Auflage des Moebels.
  - Zustand "zu" (Rast-Pose): Block liegt mittig auf dem Ursprung. Beim Aufklappen dreht sich der Vorderdeckel um die Ruecken-Achse um 180 Grad nach links;
    die ganze Buchmasse rutscht dabei um die halbe Breite nach rechts (Wurzelversatz in allen Knochen), so dass der Falz (Mitte des aufgeschlagenen Buchs)
    im Ursprung liegt. Die Neigung aller Teile bleibt in jedem Bild gleich, nur die Drehung des Vorderdeckels (und des Blattes) um die Ruecken-Achse kommt dazu.
  - Knochen ohne Eltern: Deckel_hinten (Rueckdeckel, rechter Block, unterer Rueckenhaelfte, rechte Inhaltsflaeche), Deckel_vorn (Vorderdeckel, linker Block,
    obere Rueckenhaelfte, linke Inhaltsflaeche, Wappenfeld), Blatt_1..Blatt_4 (das umgeblaetterte Blatt in vier Abschnitten; Gelenkzeilen mit 0,5/0,5 auf
    beiden Nachbarn, damit sich das Blatt biegt), optional Riemen bzw. Schliesse (haengt am Vorderdeckel und faellt beim Aufklappen nach aussen).
  - Netze: Buch, Buch_Umriss, Inhaltsflaeche_links, Inhaltsflaeche_rechts, optional Wappenfeld. Gerueest: Buch_Geruest.
Clips: zu, offen, aufklappen, zuklappen, umblaettern, umblaettern_zurueck (siehe docs/bereiche/darstellung.md)."""
import bpy
import bmesh
import math
import os
import sys
import json
import numpy as np
from mathutils import Vector, Matrix, Quaternion

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import buch_texturen as tx  # noqa: E402
import buch_stile as stile  # noqa: E402

FPS = 30
UMRISS = 0.004       # Abstand der Umriss-Huelle (wie bei Handschuh und Schriftrolle ca. 1 bis 2 % der Groesse)
TL = 0.0012          # Dicke des Blatts
HOVER = 0.0008       # Inhaltsflaechen schweben 0,8 mm ueber dem Papier (kein Z-Fighting)
SEGMENTE = stile.SEGMENTE   # Abschnitte des Blatts


def ease(x):
    return (1.0 - math.cos(math.pi * x)) / 2.0


def verlauf(punkte):
    """Wert ueber die Zeit: Stuetzpunkte (Bild, Wert); zwischen je zwei Punkten Kosinus-Weiche (Steigung 0 an jedem Punkt) = gedaempftes Nachfedern."""
    def f(bild):
        for (b0, u0), (b1, u1) in zip(punkte, punkte[1:]):
            if b0 <= bild <= b1:
                return u0 + (u1 - u0) * ease((bild - b0) / (b1 - b0))
        return punkte[-1][1]
    return f


def mat_flaeche(name, farbe, rau=0.9):
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
    bsdf.inputs["Base Color"].default_value = (*_linear(farbe), 1)
    return m


def _linear(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def mat_bild(name, bild, rau=0.9):
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
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bild
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def mat_durchsichtig(name):
    """Platzhalter der Inhaltsflaechen: voellig durchsichtig (die Oberflaeche setzt ein eigenes Material mit der 2D-Tafel darauf)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for nd in list(nt.nodes):
        nt.nodes.remove(nd)
    ausg = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], ausg.inputs["Surface"])
    bsdf.inputs["Base Color"].default_value = (*_linear((0.92, 0.88, 0.76)), 1)
    bsdf.inputs["Alpha"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.9
    try:
        m.surface_render_method = "BLENDED"
    except Exception as e:
        print("RENDERMETHODE", e)
    try:
        m.blend_method = "BLEND"
    except Exception:
        pass
    return m


def bild_laden(ausordner, name, arr, datei=None):
    pfad = os.path.join(ausordner, (datei or name) + ".png")
    tx.png_schreiben(pfad, arr)
    img = bpy.data.images.load(pfad)
    img.name = name
    img.colorspace_settings.name = "sRGB"
    img.pack()
    return img


class Teil:
    """Sammelt Vielecke mit Knochengewichten und UV; wird ein Mesh."""

    def __init__(self, name, mats, x0=0.0):
        self.name = name
        self.mats = mats                 # Name -> bpy Material
        self.x0 = x0                     # Versatz der Ruhelage (zu): Mitte des geschlossenen Buchs auf den Ursprung
        self.bm = bmesh.new()
        self.uvl = self.bm.loops.layers.uv.new("UVMap")
        self.reihenfolge = []
        self.gew = {}

    def _idx(self, m):
        if m not in self.reihenfolge:
            self.reihenfolge.append(m)
        return self.reihenfolge.index(m)

    def flaeche(self, pts, erwartet, m, uvs, gew):
        """pts: Ecken in Offenkoordinaten (Ruecken-Achse x = 0) der Anordnung 'zu'; gew: Knochenname oder Liste von {Knochen: Gewicht} je Ecke."""
        pts = [Vector(p) for p in pts]
        n = Vector((0, 0, 0))
        for i in range(len(pts)):
            a, b = pts[i], pts[(i + 1) % len(pts)]
            n += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
        if n.dot(Vector(erwartet)) < 0:
            pts = pts[::-1]
            uvs = uvs[::-1]
            if isinstance(gew, list):
                gew = gew[::-1]
        vs = [self.bm.verts.new(Vector((p.x + self.x0, p.y, p.z))) for p in pts]
        for i, v in enumerate(vs):
            self.gew[v] = {gew: 1.0} if isinstance(gew, str) else gew[i]
        f = self.bm.faces.new(vs)
        f.material_index = self._idx(m)
        f.smooth = True
        for lp, uv in zip(f.loops, uvs):
            lp[self.uvl].uv = uv
        return f

    def uv_von(self, p, n, spec, m):
        if spec is not None and spec[0] == "box":
            _, xa, xb, ya, yb, spiegel = spec
            u = (p.x - xa) / (xb - xa)
            v = (p.y - ya) / (yb - ya)
            return (1.0 - u if spiegel else u, v)
        k = stile.SKALA.get(m, 8.0)
        ax = max(range(3), key=lambda i: abs(n[i]))
        if ax == 2:
            return (p.x * k, p.y * k)
        if ax == 0:
            return (p.y * k, p.z * k)
        return (p.x * k, p.z * k)

    def box(self, x0, x1, y0, y1, z0, z1, bone, mat, spec=None, ohne=()):
        """Quader. mat: Name fuer alle Seiten oder Dict Seite ('+x' ...) -> Name (Rest: Eintrag '*'). spec: Dict Seite -> ('box', xa, xb, ya, yb, spiegel)."""
        seiten = {
            "+x": ((x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1), (1, 0, 0)),
            "-x": ((x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0), (-1, 0, 0)),
            "+y": ((x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0), (0, 1, 0)),
            "-y": ((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1), (0, -1, 0)),
            "+z": ((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1), (0, 0, 1)),
            "-z": ((x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (x1, y0, z0), (0, 0, -1)),
        }
        for k, (a, b, c, d, n) in seiten.items():
            if k in ohne:
                continue
            m = mat if isinstance(mat, str) else mat.get(k, mat.get("*"))
            if m is None:
                continue
            pts = [Vector(a), Vector(b), Vector(c), Vector(d)]
            sp = spec.get(k) if spec else None
            uvs = [self.uv_von(p, Vector(n), sp, m) for p in pts]
            self.flaeche(pts, n, m, uvs, bone)

    def prisma(self, profil, y0, y1, bone, mat_fn, spec=None):
        """Profil [(x, z), ...] entlang Y von y0 bis y1 ausgezogen. mat_fn(normale) -> Materialname oder None (Seite weglassen)."""
        profil = [tuple(p) for p in profil]
        area = sum(profil[i][0] * profil[(i + 1) % len(profil)][1] - profil[(i + 1) % len(profil)][0] * profil[i][1] for i in range(len(profil)))
        if area < 0:
            profil = profil[::-1]
        n = len(profil)
        for i in range(n):
            (xa, za), (xb, zb) = profil[i], profil[(i + 1) % n]
            nx, nz = (zb - za), -(xb - xa)
            ln = math.hypot(nx, nz)
            if ln < 1e-12:
                continue
            nv = Vector((nx / ln, 0, nz / ln))
            m = mat_fn(tuple(nv))
            if m is None:
                continue
            pts = [Vector((xa, y0, za)), Vector((xb, y0, zb)), Vector((xb, y1, zb)), Vector((xa, y1, za))]
            sp = spec if (spec and nv.z > 0.3) else None
            self.flaeche(pts, nv, m, [self.uv_von(p, nv, sp, m) for p in pts], bone)
        for y, nn in ((y0, (0, -1, 0)), (y1, (0, 1, 0))):
            m = mat_fn(nn)
            if m is None:
                continue
            pts = [Vector((x, y, z)) for x, z in profil]
            self.flaeche(pts, nn, m, [self.uv_von(p, Vector(nn), None, m) for p in pts], bone)

    def zylinder(self, cx, cy, r, z0, z1, bone, mat_seite, mat_deckel=None, n=8):
        """Stehender Zylinder (Niete, Buckel): n Seiten, Deckel oben, unten offen."""
        ring = [(cx + r * math.cos(2 * math.pi * k / n), cy + r * math.sin(2 * math.pi * k / n)) for k in range(n)]
        for k in range(n):
            (xa, ya), (xb, yb) = ring[k], ring[(k + 1) % n]
            nv = Vector(((ya + yb) / 2 - cy, -((xa + xb) / 2 - cx), 0)).normalized()
            nv = Vector((((xa + xb) / 2 - cx), ((ya + yb) / 2 - cy), 0)).normalized()
            pts = [Vector((xa, ya, z0)), Vector((xb, yb, z0)), Vector((xb, yb, z1)), Vector((xa, ya, z1))]
            self.flaeche(pts, nv, mat_seite, [self.uv_von(p, nv, None, mat_seite) for p in pts], bone)
        pts = [Vector((x, y, z1)) for x, y in ring]
        mt = mat_deckel or mat_seite
        self.flaeche(pts, (0, 0, 1), mt, [self.uv_von(p, Vector((0, 0, 1)), None, mt) for p in pts], bone)

    def fertig(self):
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces[:])
        self.bm.verts.index_update()
        gruppen = [self.gew[v] for v in self.bm.verts]
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.reihenfolge:
            me.materials.append(self.mats[m])
        return me, gruppen


class Buch:
    def __init__(self, art, ausordner):
        self.art = art
        self.aus = ausordner
        self.P = stile.STILE[art]
        P = self.P
        self.W, self.D, self.c, self.a, self.s, self.g, self.o = (P[k] for k in ("W", "D", "c", "a", "s", "g", "o"))
        self.z_t = self.c + self.a                    # Oberkante der Bloecke (offen)
        self.z_h = self.z_t + TL / 2                  # Mittelebene des Blatts = Drehachse
        self.H = 2 * self.z_h                         # Gesamthoehe zu
        self.Wc = self.g + self.W + self.o            # Deckelbreite (von x = s an)
        self.Dc = self.D + 2 * self.o                 # Deckeltiefe
        self.xb = self.s + self.g                     # Blockanfang
        self.X0 = -(self.s + self.Wc) / 2.0           # Versatz der Ruhelage
        self.z_blatt = self.z_t + TL + HOVER          # Hoehe der Inhaltsflaechen
        self.knochen = ["Deckel_hinten", "Deckel_vorn"] + [f"Blatt_{i + 1}" for i in range(SEGMENTE)] + list(P.get("anhang", ()))
        self.ruhe_o = {"Deckel_hinten": (0, 0, 0), "Deckel_vorn": (0, 0, self.z_h)}
        for i in range(SEGMENTE):
            self.ruhe_o[f"Blatt_{i + 1}"] = (self.xb + i * self.W / SEGMENTE, 0, self.z_h)
        if P.get("anhang"):
            self.ruhe_o[P["anhang"][0]] = (self.s + self.Wc, 0, self.H - self.c / 2)

    # ------------------------------------------------------------- Lage der Knochen
    def ruhe(self, name):
        r = self.ruhe_o[name]
        return Matrix.Translation((self.X0 + r[0], r[1], r[2]))

    def pose(self, ang, theta, richtung):
        """Matrizen aller Knochen: ang = Oeffnungswinkel des Vorderdeckels in Grad (0 zu, 180 offen), theta = Winkel des Blatts in rad (0 rechts, pi links),
        richtung = +1 (vorwaerts: freier Rand haengt hinterher) oder -1 (rueckwaerts)."""
        f = ang / 180.0
        shift = Matrix.Translation((self.X0 * (1.0 - f), 0, 0))
        r = math.radians(ang)
        a_deckel = Matrix.Translation((0, 0, self.z_h)) @ Matrix.Rotation(-r, 4, "Y") @ Matrix.Translation((0, 0, -self.z_h))
        m = {}
        m["Deckel_hinten"] = shift @ Matrix.Translation(self.ruhe_o["Deckel_hinten"])
        m["Deckel_vorn"] = shift @ a_deckel @ Matrix.Translation(self.ruhe_o["Deckel_vorn"])
        # Blatt: Kette in der x-z-Ebene um die Achse (0, z_h); Winkel a_i wird in Richtung der Bewegung nachgezogen
        L = self.W / SEGMENTE
        k = self.P.get("blatt_biegung", 0.16)
        px = self.xb * math.cos(theta)
        pz = self.z_h + self.xb * math.sin(theta)
        for i in range(SEGMENTE):
            ai = theta - richtung * k * math.sin(theta) * i
            m[f"Blatt_{i + 1}"] = shift @ Matrix.Translation((px, 0, pz)) @ Matrix.Rotation(-ai, 4, "Y")
            px += L * math.cos(ai)
            pz += L * math.sin(ai)
        if self.P.get("anhang"):
            # Riemen / Schliesse: haengt nach unten, solange der Deckel steht; faellt am Ende nach aussen auf die Auflage (0 -> 90 Grad zwischen 140 und 180 Grad)
            x = min(1.0, max(0.0, (ang - 140.0) / 40.0))
            tw = 90.0 * (3 * x * x - 2 * x * x * x)
            phi = math.radians(ang + tw)
            h = Vector(self.ruhe_o[self.P["anhang"][0]])
            m[self.P["anhang"][0]] = shift @ a_deckel @ Matrix.Translation(h) @ Matrix.Rotation(phi, 4, "Y")
        return m


def erzeuge(art, ausordner):
    os.makedirs(ausordner, exist_ok=True)
    B = Buch(art, ausordner)
    P = B.P
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    sc.unit_settings.system = "METRIC"
    stile.lade_texturen(B, bild_laden, mat_bild, mat_flaeche, mat_durchsichtig)
    m_um = mat_flaeche("Umriss", (0.02, 0.012, 0.008))
    m_um.use_backface_culling = True
    B.mats["Umriss"] = m_um

    haupt = Teil("Buch", B.mats, B.X0)
    stile.teile_gemeinsam(B, haupt)
    stile.teile_besonders(B, haupt)
    me, gruppen = haupt.fertig()
    ob = bpy.data.objects.new("Buch", me)
    sc.collection.objects.link(ob)

    # Inhaltsflaechen (Quads UV 0..1, knapp ueber den Seiten) und Wappenfeld
    flaechen = []
    ins = P["inhalt_rand"]
    xa, xb_ = B.xb + ins, B.xb + B.W - ins
    ya, yb = -B.D / 2 + ins, B.D / 2 - ins
    for seite in ("rechts", "links"):
        t = Teil("Inhaltsflaeche_" + seite, B.mats, B.X0)
        if seite == "rechts":
            z = B.z_blatt
            pts = [(xa, ya, z), (xb_, ya, z), (xb_, yb, z), (xa, yb, z)]
            uvs = [(0, 0), (1, 0), (1, 1), (0, 1)]
            t.flaeche(pts, (0, 0, 1), "Buch_Inhalt", uvs, "Deckel_hinten")
        else:
            # Ruhelage "zu": gespiegelt um die Drehachse (z -> 2 z_h - z); nach der Drehung liegt sie offen auf gleicher Hoehe wie die rechte
            z = 2 * B.z_h - B.z_blatt
            pts = [(xa, ya, z), (xb_, ya, z), (xb_, yb, z), (xa, yb, z)]
            uvs = [(1, 0), (0, 0), (0, 1), (1, 1)]
            t.flaeche(pts, (0, 0, -1), "Buch_Inhalt", uvs, "Deckel_vorn")
        me_i, gr_i = t.fertig()
        o_i = bpy.data.objects.new("Inhaltsflaeche_" + seite, me_i)
        sc.collection.objects.link(o_i)
        flaechen.append((o_i, gr_i))
    wappen = stile.wappenfeld(B)
    if wappen is not None:
        t = Teil("Wappenfeld", B.mats, B.X0)
        (xa_, xb2, ya_, yb2, z_, ) = wappen
        t.flaeche([(xa_, ya_, z_), (xb2, ya_, z_), (xb2, yb2, z_), (xa_, yb2, z_)], (0, 0, 1), "Buch_Wappenfeld", [(0, 0), (1, 0), (1, 1), (0, 1)], "Deckel_vorn")
        me_w, gr_w = t.fertig()
        o_w = bpy.data.objects.new("Wappenfeld", me_w)
        sc.collection.objects.link(o_w)
        flaechen.append((o_w, gr_w))

    # Umriss-Huelle (wie Handschuh/Schriftrolle: invertierte Huelle, Verschiebung ueber gleiche Orte gemittelt)
    me2 = ob.data.copy()
    me2.name = "Buch_Umriss"
    me2.update()
    pos = {}
    for v in me2.vertices:
        k = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5))
        pos.setdefault(k, Vector((0, 0, 0)))
        pos[k] += v.normal
    for v in me2.vertices:
        k = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5))
        d = pos[k].normalized() if pos[k].length > 1e-9 else Vector((0, 0, 0))
        v.co = v.co + d * UMRISS
    bm2 = bmesh.new()
    bm2.from_mesh(me2)
    bmesh.ops.reverse_faces(bm2, faces=bm2.faces)
    for f in bm2.faces:
        f.material_index = 0
    bm2.to_mesh(me2)
    bm2.free()
    me2.materials.clear()
    me2.materials.append(m_um)
    um = bpy.data.objects.new("Buch_Umriss", me2)
    sc.collection.objects.link(um)

    # Geruest
    arm_d = bpy.data.armatures.new("Buch_Geruest")
    arm = bpy.data.objects.new("Buch_Geruest", arm_d)
    sc.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for nm in B.knochen:
        eb = arm_d.edit_bones.new(nm)
        eb.head = (0, 0, 0)
        eb.tail = (0, 0.02, 0)
        eb.matrix = B.ruhe(nm)
        eb.length = 0.02
    bpy.ops.object.mode_set(mode="OBJECT")
    ruhe_m = {nm: arm_d.bones[nm].matrix_local.copy() for nm in B.knochen}
    for nm in B.knochen:
        abw = ruhe_m[nm] - B.ruhe(nm)
        if max(abs(x) for row in abw for x in row) > 1e-5:
            print("WARNUNG Knochenlage", nm)

    for o, gr in [(ob, gruppen), (um, gruppen)] + flaechen:
        for nm in B.knochen:
            o.vertex_groups.new(name=nm)
        for vi, g in enumerate(gr):
            for nm, w in g.items():
                o.vertex_groups[nm].add([vi], w, "REPLACE")
        o.parent = arm
        md = o.modifiers.new("Geruest", "ARMATURE")
        md.object = arm

    # Clips
    try:
        bpy.context.preferences.edit.keyframe_new_interpolation_type = "LINEAR"
    except Exception as e:
        print("INTERPOLATION", e)
    arm.animation_data_create()
    aktionen = {}
    vorher = {}

    def pose_setzen(ang, theta, richtung, frame=None):
        zl = B.pose(ang, theta, richtung)
        for nm in B.knochen:
            pb = arm.pose.bones[nm]
            Bm = ruhe_m[nm].inverted() @ zl[nm]
            loc, rot, scl = Bm.decompose()
            if nm in vorher and rot.dot(vorher[nm]) < 0:
                rot = Quaternion((-rot.w, -rot.x, -rot.y, -rot.z))
            vorher[nm] = rot.copy()
            pb.rotation_mode = "QUATERNION"
            pb.location = loc
            pb.rotation_quaternion = rot
            pb.scale = scl
            if frame is not None:
                pb.keyframe_insert("location", frame=frame, group=nm)
                pb.keyframe_insert("rotation_quaternion", frame=frame, group=nm)

    def clip(name, bilder, fn):
        a = bpy.data.actions.new(name)
        a.use_fake_user = True
        arm.animation_data.action = a
        vorher.clear()
        for f in range(bilder + 1):
            ang, theta, rt = fn(f)
            pose_setzen(ang, theta, rt, f)
        a.frame_range = (0, bilder)
        a.use_frame_range = True
        a.use_cyclic = False
        aktionen[name] = a

    ang_auf = verlauf(P["auf_ang"])
    ang_zu = verlauf(P["zu_ang"])
    th_vor = verlauf([(0, 0.0), (P["blatt_bilder"], math.pi)])
    th_rueck = verlauf([(0, math.pi), (P["blatt_bilder"], 0.0)])
    clip("aufklappen", P["auf_ang"][-1][0], lambda f: (ang_auf(f), 0.0, 1))
    clip("zuklappen", P["zu_ang"][-1][0], lambda f: (ang_zu(f), 0.0, 1))
    clip("umblaettern", P["blatt_bilder"], lambda f: (180.0, th_vor(f), 1))
    clip("umblaettern_zurueck", P["blatt_bilder"], lambda f: (180.0, th_rueck(f), -1))
    clip("zu", 1, lambda f: (0.0, 0.0, 1))
    clip("offen", 1, lambda f: (180.0, 0.0, 1))
    arm.animation_data.action = aktionen["zu"]
    sc.frame_set(0)

    # ------------------------------------------------------------- Messwerte
    def welt_punkte(o, frame, aktion):
        arm.animation_data.action = aktionen[aktion]
        sc.frame_set(frame)
        dg = bpy.context.evaluated_depsgraph_get()
        e = o.evaluated_get(dg)
        m = e.to_mesh()
        pts = np.array([(o.matrix_world @ v.co)[:] for v in m.vertices])
        e.to_mesh_clear()
        return pts

    def dreiecke(o):
        return sum(len(p.vertices) - 2 for p in o.data.polygons)

    mess = {"art": art, "dreiecke": {"Buch": dreiecke(ob), "Buch_Umriss": dreiecke(um)}}
    for o_i, _ in flaechen:
        mess["dreiecke"][o_i.name] = dreiecke(o_i)
    mess["dreiecke"]["gesamt"] = sum(mess["dreiecke"].values())
    for zst, (fr, akt) in (("zu", (0, "zu")), ("halb", (min(range(P["auf_ang"][-1][0] + 1), key=lambda f: abs(verlauf(P["auf_ang"])(f) - 90.0)), "aufklappen")), ("offen", (0, "offen"))):
        pts = welt_punkte(ob, fr, akt)
        mess[zst] = {"min": [round(float(x), 4) for x in pts.min(axis=0)], "max": [round(float(x), 4) for x in pts.max(axis=0)]}
    # Inhaltsflaechen im Zustand offen: Ecken, Hoehe, Ausdehnung
    for o_i, _ in flaechen:
        if o_i.name.startswith("Inhaltsflaeche"):
            pts = welt_punkte(o_i, 0, "offen")
            mess["offen"][o_i.name] = {"min": [round(float(x), 5) for x in pts.min(axis=0)], "max": [round(float(x), 5) for x in pts.max(axis=0)]}
    # Neigung: Pose des Rueckdeckels in jedem Bild jedes Clips gegen die Ruhe (Drehung) und Hoehe der Aufstandsflaeche
    schlimm_neig = 0.0
    schlimm_quer = 0.0
    z_min_hint, z_max_hint = 1e9, -1e9
    for name in aktionen:
        arm.animation_data.action = aktionen[name]
        for f in range(int(aktionen[name].frame_range[1]) + 1):
            sc.frame_set(f)
            bpy.context.view_layer.update()
            R = arm.pose.bones["Deckel_hinten"].matrix.to_3x3()
            R0 = ruhe_m["Deckel_hinten"].to_3x3()
            d = R @ R0.inverted()
            q = d.to_quaternion()
            schlimm_neig = max(schlimm_neig, math.degrees(2 * math.acos(min(1.0, abs(q.w)))))
            for nm in B.knochen:   # jede Drehung eines Knochens geht nur um die Y-Achse (Rueckenachse): keine Neigung zur Seite oder nach vorn
                Rb = arm.pose.bones[nm].matrix.to_3x3()
                schlimm_quer = max(schlimm_quer, math.degrees(math.asin(min(1.0, abs((Rb @ Vector((0, 0, 1))).y)))), math.degrees(math.asin(min(1.0, abs((Rb @ Vector((1, 0, 0))).y)))))
            z = arm.pose.bones["Deckel_hinten"].matrix.translation.z
            z_min_hint, z_max_hint = min(z_min_hint, z), max(z_max_hint, z)
    mess["neigung_max_grad"] = round(max(schlimm_neig, schlimm_quer), 4)
    mess["neigung_rueckdeckel_grad"] = round(schlimm_neig, 4)
    mess["hoehe_rueckdeckel_schwankung_m"] = round(z_max_hint - z_min_hint, 6)
    # tiefster Punkt des Netzes in allen Bildern (nichts darf unter die Auflage)
    tiefste = 1e9
    for name in ("aufklappen", "zuklappen", "umblaettern", "umblaettern_zurueck"):
        for f in range(0, int(aktionen[name].frame_range[1]) + 1, 2):
            pts = welt_punkte(ob, f, name)
            tiefste = min(tiefste, float(pts[:, 2].min()))
    mess["tiefster_punkt_m"] = round(tiefste, 5)

    # Winkel und Zeiten der Inhaltsflaechen
    sw = P.get("schwelle_grad", 172.0)
    b_auf = P["auf_ang"][-1][0]
    b_zu = P["zu_ang"][-1][0]
    auf = [ang_auf(f) for f in range(b_auf + 1)]
    zu_l = [ang_zu(f) for f in range(b_zu + 1)]
    ab = next(f for f in range(b_auf + 1) if all(a >= sw for a in auf[f:]))
    bis = max(f for f in range(b_zu + 1) if all(a >= sw for a in zu_l[:f + 1]))
    th_s = [th_vor(f) for f in range(P["blatt_bilder"] + 1)]
    f_senkrecht = min(range(P["blatt_bilder"] + 1), key=lambda f: abs(th_s[f] - math.pi / 2))
    f_flach = next(f for f in range(P["blatt_bilder"] + 1) if all(t >= math.pi - math.radians(3) for t in th_s[f:]))
    mess["zeiten_s"] = {
        "aufklappen_dauer": round(b_auf / FPS, 3), "zuklappen_dauer": round(b_zu / FPS, 3),
        "umblaettern_dauer": round(P["blatt_bilder"] / FPS, 3),
        "schwelle_grad": sw,
        "aufklappen_halb": round(min(range(b_auf + 1), key=lambda f: abs(auf[f] - 90.0)) / FPS, 3),
        "aufklappen_inhaltsflaeche_sichtbar_ab": round(ab / FPS, 3),
        "zuklappen_inhaltsflaeche_sichtbar_bis": round(bis / FPS, 3),
        "umblaettern_inhaltsflaeche_aus_von_bis": [0.0, round(f_flach / FPS, 3)],
        "umblaettern_blatt_senkrecht": round(f_senkrecht / FPS, 3),
    }
    zu_monoton = all(zu_l[i] >= zu_l[i + 1] - 1e-9 for i in range(len(zu_l) - 1))
    mess["zuklappen_monoton"] = bool(zu_monoton)
    mess["auf_ang_kurve"] = P["auf_ang"]
    mess["masse"] = {"seite_breite": B.W, "seite_tiefe": B.D, "deckel_breite": round(B.Wc, 4), "deckel_tiefe": round(B.Dc, 4), "deckel_dicke": B.c,
                     "block_dicke_je_haelfte": B.a, "ruecken_dicke": B.s, "hoehe_zu": round(B.H, 4), "blatt_dicke": TL,
                     "inhaltsflaeche_m": [round(xb_ - xa, 4), round(yb - ya, 4)], "inhaltsflaeche_hoehe_ueber_auflage_m": round(B.z_blatt, 4),
                     "versatz_zu_x": round(B.X0, 4), "umriss": UMRISS}
    arm.animation_data.action = aktionen["zu"]
    sc.frame_set(0)
    print("MESSWERTE", json.dumps(mess))
    with open(os.path.join(ausordner, f"messwerte_buch_{art}.json"), "w", encoding="utf-8") as fh:
        json.dump(mess, fh, indent=1)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ausordner, f"buch_{art}.blend"))
    for o in sc.objects:
        o.select_set(False)
    sel = [arm, ob, um] + [o for o, _ in flaechen]
    for o in sel:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    pfad = os.path.join(ausordner, f"buch_{art}.glb")
    bpy.ops.export_scene.gltf(filepath=pfad, export_format="GLB", use_selection=True, export_animations=True,
                              export_animation_mode="ACTIONS", export_skins=True, export_yup=True, export_apply=False,
                              export_image_format="AUTO", export_force_sampling=True, export_frame_range=False,
                              export_optimize_animation_size=True)
    print("GLB", pfad, os.path.getsize(pfad))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    aus = args[0]
    wahl = args[1] if len(args) > 1 else "alle"
    for art in (stile.STILE if wahl == "alle" else [wahl]):
        erzeuge(art, aus)
