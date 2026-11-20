"""Gemeinsame Bauteile der drei 3D-Gegenstaende (Kartentisch, Geldtruhe, Siegelstempel), Machart wie die 3D-Buecher und Briefe:
Geruest mit Gewichten 1,0 (keine Formschluessel), Umriss als invertierte Huelle (Netz "<Name>_Umriss", Material "Umriss"), nur Albedo,
Texturen selbst gemalt (texturen.py) und in der .glb eingebettet, Clips mit 30 Bildern je Sekunde.

Jeder Gegenstand ist ein Modul (kartentisch.py usw.) mit:
  NAME, GERUEST            Dateiname und Name des Geruests
  KNOCHEN                  Liste (Name, Drehpunkt (x, y, z), erlaubte Drehachse 'x' | 'y' | 'z' | None); alle Knochen ohne Eltern
  materialien(G)           legt die Materialien an (G = dieses Modul)
  bauen(G)                 liefert die Netze: Liste von Netz(...)
  bind()                   Verformung je Knochen fuer die Rast-Pose (Bindepose), Standard: keine
  clips()                  Liste (Name, Bilder, fn(bild) -> {Knochen: Matrix 4x4}); die Matrix verformt die flach gebauten Punkte in die Welt
  messen(ctx, mess)        eigene Messwerte (Zeiten, Schwellen, Lage)
Aufbau (Meter, Blender: Z oben, Blick von vorn = -Y; Godot: Y oben, vorn = +Z): Ursprung = Mitte der Standflaeche, Unterseite bei z = 0."""
import bpy
import bmesh
import json
import math
import os
import sys

import numpy as np
from mathutils import Matrix, Quaternion, Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import texturen as tx  # noqa: E402

FPS = 30
HOVER = 0.0008      # Inhaltsflaechen schweben 0,8 mm ueber ihrer Unterlage (kein Z-Fighting)
SKALA = {}          # Material -> Wiederholungen je Meter (Box-UV); setzt jedes Modul in materialien()


# ------------------------------------------------------------------ Kurven
def ease(x):
    x = min(1.0, max(0.0, x))
    return (1.0 - math.cos(math.pi * x)) / 2.0


def verlauf(punkte):
    """Wert ueber die Zeit: Stuetzpunkte (Bild, Wert); zwischen je zwei Punkten Kosinus-Weiche (Steigung 0 an jedem Punkt) = gedaempftes Nachfedern."""
    def f(bild):
        if bild <= punkte[0][0]:
            return punkte[0][1]
        for (b0, u0), (b1, u1) in zip(punkte, punkte[1:]):
            if b0 <= bild <= b1:
                return u0 + (u1 - u0) * ease((bild - b0) / (b1 - b0))
        return punkte[-1][1]
    return f


def fallend(b0, b1, u0, u1):
    """Freier Fall bzw. Beschleunigen: Wert u0 -> u1 zwischen Bild b0 und b1 mit quadratischem Verlauf (Steigung 0 am Anfang)."""
    def f(bild):
        x = min(1.0, max(0.0, (bild - b0) / (b1 - b0)))
        return u0 + (u1 - u0) * x * x
    return f


# ------------------------------------------------------------------ Matrizen
def T(x, y, z):
    return Matrix.Translation((x, y, z))


def RX(grad):
    return Matrix.Rotation(math.radians(grad), 4, "X")


def RY(grad):
    return Matrix.Rotation(math.radians(grad), 4, "Y")


def RZ(grad):
    return Matrix.Rotation(math.radians(grad), 4, "Z")


def S(sx, sy, sz):
    m = Matrix.Identity(4)
    m[0][0], m[1][1], m[2][2] = sx, sy, sz
    return m


def um_punkt(p, M):
    """Verformung M (Drehung/Skala) um den Punkt p."""
    return T(*p) @ M @ T(-p[0], -p[1], -p[2])


# ------------------------------------------------------------------ Materialien
def _linear(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def _bsdf(name, rau):
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
    return m, nt, bsdf


def mat_flaeche(name, farbe, rau=0.85):
    m, nt, bsdf = _bsdf(name, rau)
    bsdf.inputs["Base Color"].default_value = (*_linear(farbe), 1)
    return m


def mat_bild(name, bild, rau=0.85):
    m, nt, bsdf = _bsdf(name, rau)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bild
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def mat_durchsichtig(name, farbe=(0.88, 0.80, 0.63)):
    """Platzhalter einer Inhaltsflaeche: voellig durchsichtig (Alpha 0), bis die Oberflaeche ein eigenes Material setzt (wie Buch_Inhalt)."""
    m, nt, bsdf = _bsdf(name, 0.9)
    bsdf.inputs["Base Color"].default_value = (*_linear(farbe), 1)
    bsdf.inputs["Alpha"].default_value = 0.0
    try:
        m.surface_render_method = "BLENDED"
    except Exception as e:
        print("RENDERMETHODE", e)
    return m


def bild_laden(ausordner, name, arr):
    pfad = os.path.join(ausordner, name + ".png")
    tx.png_schreiben(pfad, arr)
    img = bpy.data.images.load(pfad)
    img.name = name.split("_", 1)[1]      # Godot nennt das ausgelagerte Bild <glb>_<bildname>.png, z. B. geldtruhe_holz.png
    img.colorspace_settings.name = "sRGB"
    img.pack()
    return img


# ------------------------------------------------------------------ Teil: sammelt Vielecke mit Knochen, Umrissabstand und UV
class Teil:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uvl = self.bm.loops.layers.uv.new("UVMap")
        self.reihenfolge = []
        self.gew = {}
        self.umr = {}

    def _idx(self, m):
        if m not in self.reihenfolge:
            self.reihenfolge.append(m)
        return self.reihenfolge.index(m)

    def v(self, p, knochen, umr):
        vert = self.bm.verts.new(Vector(p))
        self.gew[vert] = knochen
        self.umr[vert] = umr
        return vert

    def f(self, vs, uvs, m, erwartet=None):
        """Flaeche aus vorhandenen Punkten; dreht die Reihenfolge um, wenn die Normale nicht zu 'erwartet' passt."""
        if erwartet is not None:
            n = Vector((0, 0, 0))
            for i in range(len(vs)):
                a, b = vs[i].co, vs[(i + 1) % len(vs)].co
                n += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
            if n.dot(Vector(erwartet)) < 0:
                vs = vs[::-1]
                uvs = uvs[::-1]
        fl = self.bm.faces.new(vs)
        fl.material_index = self._idx(m)
        fl.smooth = True
        for lp, uv in zip(fl.loops, uvs):
            lp[self.uvl].uv = uv
        return fl

    def uv_box(self, p, n, m):
        k = SKALA.get(m, 8.0)
        ax = max(range(3), key=lambda i: abs(n[i]))
        if ax == 2:
            return (p.x * k, p.y * k)
        if ax == 0:
            return (p.y * k, p.z * k)
        return (p.x * k, p.z * k)

    def flaeche(self, pts, erwartet, m, knochen, umr, uvs=None, M=None):
        pts = [Vector(p) for p in pts]
        if M is not None:
            pts = [(M @ p.to_4d()).to_3d() for p in pts]
            erwartet = M.to_3x3() @ Vector(erwartet)
        if uvs is None:
            uvs = [self.uv_box(p, Vector(erwartet), m) for p in pts]
        vs = [self.v(p, knochen, umr) for p in pts]
        return self.f(vs, uvs, m, erwartet)

    def box(self, x0, x1, y0, y1, z0, z1, knochen, mat, umr, ohne=(), M=None):
        """Quader (Flaechen mit eigenen Punkten = harte Kanten). mat: Name oder Dict Seite ('+x' ...) -> Name, Rest '*'."""
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
            self.flaeche([a, b, c, d], n, m, knochen, umr, M=M)

    def prisma(self, profil, a0, a1, achse, knochen, mat, umr, M=None, deckel=True):
        """Profil [(u, z), ...] entlang der Achse ('x' oder 'y') von a0 bis a1 ausgezogen (harte Kanten). achse 'y': u = x; achse 'x': u = y."""
        def P(u, a, z):
            return (u, a, z) if achse == "y" else (a, u, z)
        profil = [tuple(p) for p in profil]
        n = len(profil)
        fl = sum(profil[i][0] * profil[(i + 1) % n][1] - profil[(i + 1) % n][0] * profil[i][1] for i in range(n))
        if fl < 0:
            profil = profil[::-1]
        for i in range(n):
            (ua, za), (ub, zb) = profil[i], profil[(i + 1) % n]
            nu, nz = (zb - za), -(ub - ua)
            ln = math.hypot(nu, nz)
            if ln < 1e-12:
                continue
            self.flaeche([P(ua, a0, za), P(ub, a0, zb), P(ub, a1, zb), P(ua, a1, za)], P(nu / ln, 0, nz / ln), mat, knochen, umr, M=M)
        if deckel:
            for a, s in ((a0, -1), (a1, 1)):
                self.flaeche([P(u, a, z) for u, z in profil], P(0, s, 0), mat, knochen, umr, M=M)
    def dreh(self, profil, n, knochen, mat, umr, mitte=(0.0, 0.0, 0.0), k_uv=None, M=None, tausch=False):
        """Drehkoerper um die Z-Achse durch 'mitte'. profil: [(r, z), ...] oder [(r, z, 'h')] (harte Kante nach diesem Punkt),
        von unten innen nach oben innen (Normale = (dz, -dr) zeigt dann nach aussen). Punkte mit r = 0 werden ein Pol.
        mat: Name oder Liste (je Abschnitt). Benachbarte Abschnitte teilen die Punkte (glatt), ausser nach 'h'.
        tausch: u laeuft entlang des Profils statt rundherum (gedrechseltes Holz: Maserung laengs der Achse)."""
        cx, cy, cz = mitte
        Mx = M if M is not None else Matrix.Identity(4)
        k = k_uv if k_uv is not None else 8.0
        rmax = max(p[0] for p in profil)
        U = max(1, round(2 * math.pi * rmax * k))
        laenge = [0.0]
        for (r0, z0, *_), (r1, z1, *_) in zip(profil, profil[1:]):
            laenge.append(laenge[-1] + math.hypot(r1 - r0, z1 - z0))

        def ring(r, z, i):
            kn = knochen[i] if isinstance(knochen, list) else knochen     # Knochen je Profilpunkt (z. B. Wachs: Mitte und Rand)
            if r < 1e-9:
                vert = self.v((Mx @ Vector((cx, cy, cz + z, 1))).to_3d(), kn, umr)
                return [vert] * n
            return [self.v((Mx @ Vector((cx + r * math.cos(2 * math.pi * j / n), cy + r * math.sin(2 * math.pi * j / n), cz + z, 1))).to_3d(), kn, umr) for j in range(n)]

        vorher = None
        for i in range(len(profil) - 1):
            (r0, z0, *f0), (r1, z1, *f1) = profil[i], profil[i + 1]
            m = mat if isinstance(mat, str) else mat[i]
            A = vorher if (vorher is not None and not (f0 and "h" in f0[0])) else ring(r0, z0, i)
            B = ring(r1, z1, i + 1)
            dr, dz = r1 - r0, z1 - z0
            for j in range(n):
                w = 2 * math.pi * (j + 0.5) / n
                erw = Mx.to_3x3() @ Vector((math.cos(w) * dz, math.sin(w) * dz, -dr))
                quad = [(A[j], (j / n * U, laenge[i] * k)), (A[(j + 1) % n], ((j + 1) / n * U, laenge[i] * k)),
                        (B[(j + 1) % n], ((j + 1) / n * U, laenge[i + 1] * k)), (B[j], (j / n * U, laenge[i + 1] * k))]
                ein = []
                for vert, uv in quad:
                    if all(vert is not e[0] for e in ein):
                        ein.append((vert, uv))
                if tausch:
                    ein = [(vv, (uv[1], uv[0])) for vv, uv in ein]
                if len(ein) >= 3:
                    self.f([e[0] for e in ein], [e[1] for e in ein], m, erw)
            vorher = B

    def kugel(self, mitte, r, nu, nv, knochen, mat, umr):
        """UV-Kugel (glatt): nv Ringe, nu Abschnitte."""
        prof = [(r * math.sin(math.pi * i / nv), -r * math.cos(math.pi * i / nv) + r) for i in range(nv + 1)]
        prof[0] = (0.0, 0.0)
        prof[-1] = (0.0, 2 * r)
        self.dreh(prof, nu, knochen, mat, umr, mitte=(mitte[0], mitte[1], mitte[2] - r), k_uv=1.0 / (2 * r))

    def fertig(self, mats):
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces[:], quad_method="BEAUTY", ngon_method="EAR_CLIP")
        self.bm.verts.index_update()
        gew = [self.gew[v] for v in self.bm.verts]
        umr = [self.umr[v] for v in self.bm.verts]
        flach = np.array([v.co[:] for v in self.bm.verts])
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.reihenfolge:
            me.materials.append(mats[m])
        return me, gew, umr, flach


class Netz:
    """Ein Netz des Gegenstands. haut: haengt am Geruest (Gewichte); umriss: bekommt eine Umriss-Huelle '<name>_Umriss';
    eltern: Name eines leeren Knotens (z. B. Fuellstufe_halb), unter dem das Netz haengt (nur fuer Netze ohne Haut)."""

    def __init__(self, teil, haut=True, umriss=True, eltern=None):
        self.teil = teil
        self.haut = haut
        self.umriss = umriss
        self.eltern = eltern


# ------------------------------------------------------------------ Bau
def anwenden(D, flach, gew, ident=None):
    """Punkte (flach gebaut) mit der Verformung je Knochen in die Welt."""
    aus = np.empty_like(flach)
    knochen = sorted(set(gew), key=str)
    gw = np.array([str(g) for g in gew])
    for kn in knochen:
        idx = np.nonzero(gw == str(kn))[0]
        M = np.array(D.get(kn, ident if ident is not None else Matrix.Identity(4)))
        p = np.c_[flach[idx], np.ones(len(idx))] @ M.T
        aus[idx] = p[:, :3]
    return aus


def umriss_huelle(me, umr, name, m_um):
    """Invertierte Huelle: jeder Punkt wandert um seinen Umrissabstand entlang der ueber gleiche Orte gemittelten Normale."""
    me2 = me.copy()
    me2.name = name
    me2.update()
    pos = {}
    for v in me2.vertices:
        k = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5))
        pos.setdefault(k, Vector((0, 0, 0)))
        pos[k] += v.normal
    for v in me2.vertices:
        k = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5))
        d = pos[k].normalized() if pos[k].length > 1e-9 else Vector((0, 0, 0))
        v.co = v.co + d * umr[v.index]
    bm2 = bmesh.new()
    bm2.from_mesh(me2)
    bmesh.ops.reverse_faces(bm2, faces=bm2.faces)
    for fl in bm2.faces:
        fl.material_index = 0
    bm2.to_mesh(me2)
    bm2.free()
    me2.materials.clear()
    me2.materials.append(m_um)
    return me2


def drehabweichung(D, achse):
    """Winkel (Grad), um den die Drehung in D von einer reinen Drehung um 'achse' abweicht (achse None: jede Drehung zaehlt)."""
    R = D.to_3x3()
    R = Matrix([R.col[i].normalized() for i in range(3)]).transposed()
    if achse is None:
        q = R.to_quaternion()
        return math.degrees(2 * math.acos(min(1.0, abs(q.w))))
    a = Vector({"x": (1, 0, 0), "y": (0, 1, 0), "z": (0, 0, 1)}[achse])
    return math.degrees(math.acos(max(-1.0, min(1.0, (R @ a).dot(a)))))


def erzeuge(modul, ausordner):
    os.makedirs(ausordner, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    sc.unit_settings.system = "METRIC"
    SKALA.clear()
    mats = modul.materialien(sys.modules[__name__], ausordner)
    m_um = mat_flaeche("Umriss", (0.02, 0.012, 0.008))
    m_um.use_backface_culling = True
    mats["Umriss"] = m_um

    kn_namen = [k[0] for k in modul.KNOCHEN]
    dreh_punkt = {k[0]: Vector(k[1]) for k in modul.KNOCHEN}
    achse = {k[0]: k[2] for k in modul.KNOCHEN}
    D_bind = {nm: Matrix.Identity(4) for nm in kn_namen}
    D_bind.update(modul.bind() if hasattr(modul, "bind") else {})

    arm_d = bpy.data.armatures.new(modul.GERUEST)
    arm = bpy.data.objects.new(modul.GERUEST, arm_d)
    sc.collection.objects.link(arm)

    netze = modul.bauen(sys.modules[__name__])
    leere = {}
    umriss_werte = {}
    objekte = []     # (Objekt, gew, flach, haut, ist_umriss)
    for nz in netze:
        me, gew, umr, flach = nz.teil.fertig(mats)
        if nz.umriss:
            umriss_werte[nz.teil.name] = [round(min(umr), 4), round(max(umr), 4)]
        if nz.haut:
            welt = anwenden(D_bind, flach, gew)
            for v, p in zip(me.vertices, welt):
                v.co = Vector(p)
            me.update()
        ob = bpy.data.objects.new(nz.teil.name, me)
        sc.collection.objects.link(ob)
        objekte.append((ob, gew, flach, nz.haut, False))
        if nz.umriss:
            me2 = umriss_huelle(me, umr, nz.teil.name + "_Umriss", m_um)
            um = bpy.data.objects.new(nz.teil.name + "_Umriss", me2)
            sc.collection.objects.link(um)
            # flache Koordinaten der Huelle fuer Messungen: Huelle in der Bindepose zurueck in die Bauform
            welt2 = np.array([v.co[:] for v in me2.vertices])
            flach2 = anwenden({k: D_bind[k].inverted() for k in D_bind}, welt2, gew) if nz.haut else welt2
            objekte.append((um, gew, flach2, nz.haut, True))
        if nz.eltern:
            if nz.eltern not in leere:
                e = bpy.data.objects.new(nz.eltern, None)
                e.empty_display_size = 0.05
                sc.collection.objects.link(e)
                e.parent = arm
                leere[nz.eltern] = e
            for o in [ob] + ([um] if nz.umriss else []):
                o.parent = leere[nz.eltern]

    # Geruest: Ruhelage der Knochen = Bindepose (Verformung D_bind am Drehpunkt)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    ruhe_flach = {nm: T(*dreh_punkt[nm]) for nm in kn_namen}
    for nm in kn_namen:
        eb = arm_d.edit_bones.new(nm)
        eb.head = (0, 0, 0)
        eb.tail = (0, 0.02, 0)
        loc, rot, _ = (D_bind[nm] @ ruhe_flach[nm]).decompose()
        eb.matrix = Matrix.LocRotScale(loc, rot, None)
        eb.length = 0.02
    bpy.ops.object.mode_set(mode="OBJECT")
    ruhe_m = {nm: arm_d.bones[nm].matrix_local.copy() for nm in kn_namen}   # ohne Skala (eine Skala der Bindepose steckt in D_bind)
    bind_inv = {nm: D_bind[nm].inverted() for nm in kn_namen}

    for ob, gew, _, haut, _ in objekte:
        if not haut:
            continue
        for nm in kn_namen:
            ob.vertex_groups.new(name=nm)
        for vi, g in enumerate(gew):
            ob.vertex_groups[g].add([vi], 1.0, "REPLACE")
        ob.parent = arm
        md = ob.modifiers.new("Geruest", "ARMATURE")
        md.object = arm

    # ------------------------------------------------------------- Clips
    try:
        bpy.context.preferences.edit.keyframe_new_interpolation_type = "LINEAR"
    except Exception as e:
        print("INTERPOLATION", e)
    arm.animation_data_create()
    aktionen = {}
    clipliste = modul.clips()
    clipfn = {}
    vorher = {}

    def pose_setzen(D, frame):
        for nm in kn_namen:
            pb = arm.pose.bones[nm]
            # Punkt der Bindepose -> flach (D_bind^-1) -> Welt (D); als Knochenpose relativ zur Ruhelage des Knochens
            Bm = ruhe_m[nm].inverted() @ D.get(nm, Matrix.Identity(4)) @ bind_inv[nm] @ ruhe_m[nm]
            loc, rot, scl = Bm.decompose()
            if nm in vorher and rot.dot(vorher[nm]) < 0:
                rot = Quaternion((-rot.w, -rot.x, -rot.y, -rot.z))
            vorher[nm] = rot.copy()
            pb.rotation_mode = "QUATERNION"
            pb.location = loc
            pb.rotation_quaternion = rot
            pb.scale = scl
            pb.keyframe_insert("location", frame=frame, group=nm)
            pb.keyframe_insert("rotation_quaternion", frame=frame, group=nm)
            pb.keyframe_insert("scale", frame=frame, group=nm)

    for name, bilder, fn in clipliste:
        a = bpy.data.actions.new(name)
        a.use_fake_user = True
        arm.animation_data.action = a
        vorher.clear()
        for fr in range(bilder + 1):
            pose_setzen(fn(fr), fr)
        a.frame_range = (0, bilder)
        a.use_frame_range = True
        a.use_cyclic = False
        aktionen[name] = a
        clipfn[name] = (bilder, fn)
    erster = clipliste[0][0]
    arm.animation_data.action = aktionen[erster]
    sc.frame_set(0)

    # ------------------------------------------------------------- Messwerte
    def dreiecke(o):
        return sum(len(p.vertices) - 2 for p in o.data.polygons)

    mess = {"gegenstand": modul.NAME, "dreiecke": {}, "knochen": kn_namen, "clips_s": {}}
    for ob, *_ in objekte:
        mess["dreiecke"][ob.name] = dreiecke(ob)
    mess["dreiecke"]["gesamt"] = sum(mess["dreiecke"].values())
    for name, (bilder, _) in clipfn.items():
        mess["clips_s"][name] = round(bilder / FPS, 3)

    # Neigung: jede Drehung eines Knochens nur um seine erlaubte Achse, in jedem Bild jedes Clips; Huelle aller Netze ueber alle Bilder
    schlimm = 0.0
    lo = np.array([1e9] * 3)
    hi = np.array([-1e9] * 3)
    for name, (bilder, fn) in clipfn.items():
        for fr in range(bilder + 1):
            D = fn(fr)
            for nm in kn_namen:
                schlimm = max(schlimm, drehabweichung(D.get(nm, Matrix.Identity(4)), achse[nm]))
            for ob, gew, flach, haut, _ in objekte:
                if not haut:
                    continue
                p = anwenden(D, flach, gew)
                lo = np.minimum(lo, p.min(axis=0))
                hi = np.maximum(hi, p.max(axis=0))
    bpy.context.view_layer.update()
    for ob, gew, flach, haut, _ in objekte:
        if not haut:
            p = np.array([(ob.matrix_world @ v.co)[:] for v in ob.data.vertices])
            lo = np.minimum(lo, p.min(axis=0))
            hi = np.maximum(hi, p.max(axis=0))
    mess["neigung_max_grad"] = round(schlimm, 4)
    mess["huelle_alle_bilder_blender"] = {"min": [round(float(x), 4) for x in lo], "max": [round(float(x), 4) for x in hi]}
    # CustomAabb fuer Godot (Y oben, Z = -Blender-Y): 1 cm Rand, auf cm gerundet
    gmin = [math.floor((lo[0] - 0.01) * 100) / 100, math.floor((lo[2] - 0.01) * 100) / 100, math.floor((-hi[1] - 0.01) * 100) / 100]
    gmax = [math.ceil((hi[0] + 0.01) * 100) / 100, math.ceil((hi[2] + 0.01) * 100) / 100, math.ceil((-lo[1] + 0.01) * 100) / 100]
    mess["custom_aabb_godot"] = {"position": gmin, "groesse": [round(b - a, 2) for a, b in zip(gmin, gmax)]}

    # Abgleich: ausgewertete Blender-Pose (Geruest, Gewichte, Schluesselbilder) gegen die Formel des Moduls
    abgleich = 0.0
    for name, (bilder, fn) in clipfn.items():
        for fr in sorted({0, bilder // 2, bilder}):
            arm.animation_data.action = aktionen[name]
            sc.frame_set(fr)
            dg = bpy.context.evaluated_depsgraph_get()
            for ob, gew, flach, haut, _ in objekte:
                if not haut:
                    continue
                e = ob.evaluated_get(dg)
                m = e.to_mesh()
                p = np.array([(ob.matrix_world @ v.co)[:] for v in m.vertices])
                e.to_mesh_clear()
                abgleich = max(abgleich, float(np.abs(p - anwenden(fn(fr), flach, gew)).max()))
    mess["abgleich_blender_formel_m"] = round(abgleich, 7)

    mess["umriss_m"] = umriss_werte
    ctx = {"objekte": objekte, "clipfn": clipfn, "anwenden": anwenden, "mess": mess, "D_bind": D_bind}
    modul.messen(ctx, mess)
    arm.animation_data.action = aktionen[erster]
    sc.frame_set(0)
    print("MESSWERTE", json.dumps(mess))
    with open(os.path.join(ausordner, f"messwerte_{modul.NAME}.json"), "w", encoding="utf-8") as fh:
        json.dump(mess, fh, indent=1)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ausordner, f"{modul.NAME}.blend"))
    for o in sc.objects:
        o.select_set(False)
    for o in [arm] + [o for o, *_ in objekte] + list(leere.values()):
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    pfad = os.path.join(ausordner, f"{modul.NAME}.glb")
    bpy.ops.export_scene.gltf(filepath=pfad, export_format="GLB", use_selection=True, export_animations=True,
                              export_animation_mode="ACTIONS", export_skins=True, export_yup=True, export_apply=False,
                              export_image_format="AUTO", export_force_sampling=True, export_frame_range=False,
                              export_optimize_animation_size=True)
    print("GLB", pfad, os.path.getsize(pfad))
