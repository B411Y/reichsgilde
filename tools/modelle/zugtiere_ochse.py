"""Zugtiere Runde 9: Ochse mit Anatomie statt Tonklumpen (Eigenbau auf dem Quaternius-Netz).

Das glatte Netz der Kuh bekommt Formen, die an der Haut verankert sind (Strahl gegen das Netz, dort eine weiche Beule oder Mulde entlang der
Hautnormalen): Widerrist und Nackenmuskel, Schulterblatt, Brust und Wamme (Hautlappen am Hals), Rippenrundung, eingezogene Flanke, Hüfthöcker,
Oberschenkel. Am Kopf Augenbogen, Backenknochen, Flotzmaul, Nüstern und Augen (dunkle Kugeln mit Lidwulst). Maße in Metern, Skelett in Ruhelage.
"""

import math

import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree


STAERKE = 1.9


def _g(d, r):
    return math.exp(-(d / r) ** 2)


def ochse_form(bm, gw, ske, platz_dunkel=5, platz_fell=0):
    bm.normal_update()
    bvh = BVHTree.FromBMesh(bm)
    b = ske.data.bones
    H = Vector(b["Hips"].head_local)
    T = Vector(b["Torso"].head_local)
    Sh, ShT = Vector(b["Shoulders"].head_local), Vector(b["Shoulders"].tail_local)
    N0, N1 = Vector(b["Neck"].head_local), Vector(b["Neck"].tail_local)
    Hd, Ht = Vector(b["Head"].head_local), Vector(b["Head"].tail_local)
    a = (Ht - Hd).normalized()
    up = Vector((0, a.z, -a.y))
    if up.z < 0:
        up = -up
    up.normalize()

    def treffer(o, r):
        h = bvh.ray_cast(o, r.normalized(), 3.0)
        return h[0] if h[0] is not None else None

    def seitlich(y, z, seite):
        return treffer(Vector((seite * 1.5, y, z)), Vector((-seite, 0, 0)))

    def oben(x, y):
        return treffer(Vector((x, y, 3.0)), Vector((0, 0, -1)))

    def unten(x, y):
        return treffer(Vector((x, y, -0.5)), Vector((0, 0, 1)))

    pl = []        # (Mitte, Radius, Betrag, Stauchung als Vektor)

    def add(p, r, amp, sk=(1.0, 1.0, 1.0)):
        if p is not None:
            pl.append((p, r, amp * STAERKE, Vector(sk)))

    for sd in (1, -1):
        add(oben(sd * 0.20, H.y - 0.02), 0.11, 0.030)                         # Hüfthöcker
        add(oben(sd * 0.10, H.y + 0.34), 0.13, 0.014)                          # Sitzbein, Kruppe
        add(seitlich(H.y + 0.14, H.z - 0.30, sd), 0.24, 0.040, (1, 1.4, 1.1))   # Oberschenkel
        add(seitlich(H.y - 0.30, H.z - 0.55, sd), 0.20, -0.032, (1, 1.3, 1))     # Flanke eingezogen
        add(seitlich(T.y - 0.05, T.z - 0.40, sd), 0.36, 0.024, (1, 1.8, 1))      # Rippenrundung
        add(seitlich(0.5 * (Sh.y + ShT.y), ShT.z - 0.18, sd), 0.22, 0.032, (1, 1.3, 1.3))   # Schulterblatt
    add(oben(0.0, Sh.y - 0.12), 0.17, 0.035, (1.3, 1.0, 1.0))                    # Widerrist
    add(oben(0.0, N0.y - 0.10), 0.20, 0.040, (1.4, 1.2, 1.0))                    # Nackenmuskel
    for f, amp, r in ((0.05, 0.075, 0.24), (0.30, 0.060, 0.17), (0.58, 0.050, 0.15), (0.85, 0.032, 0.12)):    # Wamme: Hautlappen von der Brust zur Kehle
        p = N0 + (N1 - N0) * f
        add(unten(0.0, p.y), r, amp, (1.0, 1.5, 1.0))
    add(unten(0.0, Sh.y + 0.25), 0.30, 0.050, (1.2, 1.4, 1.0))                    # Brust
    # Kopf
    for sd in (1, -1):
        pa = Hd + a * 0.22 + up * 0.03
        augeort = seitlich(pa.y, pa.z, sd)
        if augeort is not None:
            add(augeort + up * 0.045 + a * 0.01, 0.045, 0.016, (1.0, 1.3, 1.0))      # Augenbogen
            add(augeort - up * 0.07 - a * 0.05, 0.07, 0.014, (1.0, 1.4, 1.0))       # Backenknochen
            add(augeort + a * 0.14, 0.07, -0.012, (1.0, 1.4, 1.0))                   # Mulde zum Flotzmaul
    add(oben(0.0, (Hd + a * 0.06).y), 0.11, 0.022)                                  # Stirnwulst zwischen den Hörnern
    # anwenden
    for v in bm.verts:
        n = v.normal
        d = 0.0
        for c, r, amp, sk in pl:
            q = v.co - c
            q = Vector((q.x / sk.x, q.y / sk.y, q.z / sk.z))
            dist = q.length
            if dist < 2.2 * r:
                d += amp * _g(dist, r)
        if d != 0.0:
            w = gw.summe(v, ["Torso", "Hips", "Shoulders", "Neck", "Head", "Back", "Body"])
            v.co = v.co + n * d * min(1.0, w + 0.0)
    bm.normal_update()
    bvh = BVHTree.FromBMesh(bm)
    # Augen und Nüstern als Kugeln
    pa = Hd + a * 0.22 + up * 0.03
    for sd in (1, -1):
        e = seitlich(pa.y, pa.z, sd)
        if e is None:
            continue
        nrm = Vector((sd, 0, 0))
        lid = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.042)
        for v in lid["verts"]:
            v.co = e - nrm * 0.016 + v.co * Vector((0.7, 1.0, 0.8))
            v[gw.dl][gw.idx["Head"]] = 1.0
        for fl in {f for v in lid["verts"] for f in v.link_faces}:
            fl.material_index = platz_fell
            fl.smooth = True
        eye = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.029)
        for v in eye["verts"]:
            v.co = e - nrm * 0.006 + v.co * Vector((0.75, 1.15, 1.0))
            v[gw.dl][gw.idx["Head"]] = 1.0
        for fl in {f for v in eye["verts"] for f in v.link_faces}:
            fl.material_index = platz_dunkel
            fl.smooth = True
    spitze = Ht + a * 0.12
    for sd in (1, -1):
        h = bvh.ray_cast(spitze + Vector((sd * 0.042, 0, 0)) + up * 0.04, -a, 0.5)
        if h[0] is None:
            continue
        geo = bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=0.020)
        for v in geo["verts"]:
            v.co = h[0] + (-a * 0.004) + v.co * Vector((0.5, 0.8, 1.1))
            v[gw.dl][gw.idx["Head"]] = 1.0
        for fl in {f for v in geo["verts"] for f in v.link_faces}:
            fl.material_index = platz_dunkel
            fl.smooth = True
    return f"Ochsenform: {len(pl)} Formen"
