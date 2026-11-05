"""Mauszeiger als Lederhandschuh auf einer echten Handgeometrie (Runde 14): MPFB-Grundnetz mit Fingerknochen, nur posiert.

    ~/werkzeuge/mpfb/blender_mpfb.sh -b --python tools/modelle/handschuh_mpfb.py -- --aus=<Ordner> [--stufen=1,2,3] [--erkunden]

Quelle der Hand: das MakeHuman/MPFB-Grundnetz (MPFB 2.0.18, Basismesh und Rig "default" mit Fingerknochen), Netz und Gewichte CC0
(MakeHuman Community, siehe MPFB-Lizenzhinweis: Assets CC0). Nichts aus „Die Gilde“, nichts aus dem Human Generator.
Das Skript erzeugt den Menschen im Speicher, setzt die Fingerknochen in eine entspannte Haltung (Pose), wertet das Netz aus
und löst die rechte Hand mit kurzem Unterarmstück heraus. Daraus wird der Handschuh: Netz leicht aufgedickt (Verschiebung entlang
der Normalen), Falten an den Fingergelenken und am Handrücken, Nähte (Stiche) an den Fingerseiten und auf dem Rücken, eine
Stulpe als eigenes Teil. Stufen, Stoffe, Schmuck und Nachbearbeitung (Fugen, Kontur, 32/48/64 px) kommen aus handschuh.py
(Runde 13), dort bleiben auch Türschloss und Türring.

Deterministisch: feste Pose, fester Zufallssamen, Cycles CPU.
"""

import math
import os
import random
import sys
import importlib
import importlib.util

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

HIER = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("handschuh_alt", os.path.join(HIER, "handschuh.py"))
H = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(H)

# Haltung auf dem Bild
H.DREHUNG = 38.0        # Finger zeigen etwa 50 Grad (vom Waagrechten) nach links oben
H.NEIGUNG = 12.0        # Kamera nur leicht schräg
MASSSTAB = 10.0         # Meter -> Modelleinheiten (Hand etwa 1,9 lang)
DICKE = 0.030           # Aufdickung des Leders in Modelleinheiten (3 mm)

# Pose: Beugung je Gelenk in Grad (positiv = zur Handfläche), Spreizung in Grad (positiv = vom Mittelfinger weg)
# Gelenke je Finger: Grundgelenk, Mittelgelenk, Endgelenk
POSE = {
    "zeige": dict(beugen=(5, 6, 4), spreizen=6),
    "mittel": dict(beugen=(13, 17, 9), spreizen=1),
    "ring": dict(beugen=(21, 27, 14), spreizen=-3),
    "klein": dict(beugen=(28, 32, 17), spreizen=-8),
    "daumen": dict(spreizen=30, beugen=(6, 10, 10)),
}
FINGER_KNOCHEN = {"daumen": 1, "zeige": 2, "mittel": 3, "ring": 4, "klein": 5}
TEIL_NUMMER = {"platte": 1, "zeige": 2, "mittel": 3, "ring": 4, "klein": 5, "daumen": 6}
TEIL_NAMEN = ["platte", "zeige", "mittel", "ring", "klein", "daumen"]


def holen(paket, name):
    for m in list(sys.modules):
        if m.endswith(paket):
            return getattr(importlib.import_module(m), name)
    raise RuntimeError("MPFB nicht gefunden: " + paket)


# ─────────────────────────────────────────────────────────────────────────────
#  Mensch, Gerüst, Pose
# ─────────────────────────────────────────────────────────────────────────────

def mensch_bauen():
    ts = holen("mpfb.services.targetservice", "TargetService")
    hs = holen("mpfb.services.humanservice", "HumanService")
    d = ts.get_default_macro_info_dict()
    d["gender"] = 1.0
    b = hs.create_human(mask_helpers=False, detailed_helpers=True, extra_vertex_groups=False, macro_detail_dict=d)
    rig = hs.add_builtin_rig(b, "default")
    return b, rig


def achsen(rig):
    """Handachsen im Ruhezustand (Armaturenraum): F Finger, T Daumenseite, N Handrücken."""
    kn = rig.data.bones
    f = (kn["finger3-3.R"].tail_local - kn["finger3-1.R"].head_local).normalized()
    t = kn["finger1-1.R"].head_local - kn["finger5-1.R"].head_local
    t = (t - f * t.dot(f)).normalized()
    n = f.cross(t).normalized()
    return f, t, n


def pose_setzen(rig, f, t, n, pose):
    """Dreht Knochen um Achsen, die im Ruhezustand des Armaturenraums gegeben sind (Drehung im Knochenraum umgerechnet).
    Beugung = Drehung um T; Spreizung = Drehung um N; das Handgelenk wird zuerst so gedreht, dass die Hand in Linie mit dem Unterarm liegt."""
    from mathutils import Quaternion
    kn = rig.data.bones
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)

    def drehen(name, achse, grad):
        r = kn[name].matrix_local.to_3x3()
        lokal = (r.inverted() @ achse).normalized()
        pb = rig.pose.bones[name]
        pb.rotation_quaternion = Quaternion(lokal, math.radians(grad)) @ pb.rotation_quaternion

    # Handgelenk strecken: Hand in Linie mit dem Unterarm (Unterarm weist vom Ellbogen zur Hand)
    arm = (kn["wrist.R"].head_local - kn["lowerarm02.R"].head_local).normalized()
    ach = f.cross(arm)
    if ach.length > 1e-6:
        drehen("wrist.R", ach.normalized(), math.degrees(f.angle(arm)) * pose.get("handgelenk", 1.0))
    for fname, k in FINGER_KNOCHEN.items():
        if fname == "daumen":
            continue
        p = pose[fname]
        drehen(f"finger{k}-1.R", n, p["spreizen"])
        for g, w in enumerate(p["beugen"]):
            drehen(f"finger{k}-{g + 1}.R", t, w)
    d = pose["daumen"]
    drehen("finger1-1.R", n, d["spreizen"])
    if "heben" in d:
        drehen("finger1-1.R", f, d["heben"])
    for g, w in enumerate(d["beugen"]):
        drehen(f"finger1-{g + 1}.R", t, w)
    bpy.context.view_layer.update()


# ─────────────────────────────────────────────────────────────────────────────
#  Hand herauslösen und zum Netz für den Handschuh machen (einmal, danach nur Zahlen im Speicher)
# ─────────────────────────────────────────────────────────────────────────────

HAND_GRUPPEN = ["lowerarm01.R", "lowerarm02.R", "wrist.R"] + [f"metacarpal{i}.R" for i in (1, 2, 3, 4)] + \
    [f"finger{k}-{g}.R" for k in range(1, 6) for g in (1, 2, 3)]
SCHNITT = 0.060         # Unterarmstück bis so weit hinter dem Handgelenk (Meter)
UNTERTEILUNG = 3


class Hand:
    """Zwischenspeicher: fertig gedicktes Handnetz in Modelleinheiten, Teilenummer je Fläche, Fingerachsen, Unterarm."""


def hand_vorbereiten(pose):
    basis, rig = mensch_bauen()
    f, t, n = achsen(rig)
    pose_setzen(rig, f, t, n, pose)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(basis.evaluated_get(dg))
    co = np.array([v.co[:] for v in me.vertices])
    namen = {g.index: g.name for g in basis.vertex_groups}
    gew = {nm: np.zeros(len(co)) for nm in HAND_GRUPPEN}
    haut = np.zeros(len(co), dtype=bool)         # nur echte Haut, keine Hilfsgeometrie (Gelenkwürfel u. a.)
    for v in me.vertices:
        for ge in v.groups:
            nm = namen.get(ge.group)
            if nm in gew:
                gew[nm][v.index] = ge.weight
            elif nm == "body":
                haut[v.index] = True
    pbs = rig.pose.bones
    wk = np.array(pbs["wrist.R"].head[:])
    arm = np.array((pbs["wrist.R"].head - pbs["lowerarm02.R"].head).normalized()[:])
    summe = sum(gew.values()) * haut
    keep = (summe > 0.5) & ((co - wk) @ arm > -SCHNITT)
    # Handachsen nach der Pose (Armaturenraum), Rest wie im Ruhezustand: Basis X = -T, Y = F, Z = N
    fp = Vector(pbs["finger3-3.R"].tail) - Vector(pbs["finger3-1.R"].head)
    f_, t_, n_ = Vector(f), Vector(t), Vector(n)
    # Handbasis aus dem Unterarm: Y = Unterarmachse, Z = Handrücken (aus N senkrecht dazu), X = Y x Z... (rechtshändig: X = Y.cross(Z))
    y = Vector(arm).normalized()
    z = (n_ - y * n_.dot(y)).normalized()
    x = y.cross(z).normalized()
    R = np.array([x[:], y[:], z[:]])
    ma = MASSSTAB

    def modell(p):
        return (np.array(p, dtype=float) - wk) @ R.T * ma
    faces = [tuple(p.vertices) for p in me.polygons if all(keep[i] for i in p.vertices)]
    benutzt = sorted({i for fc in faces for i in fc})
    neu = {i: k for k, i in enumerate(benutzt)}
    P = modell(co[benutzt])
    # Teilenummer je Ecke: Finger mit größtem Gewicht
    fing = np.zeros((len(benutzt), 6))
    for k in range(1, 6):
        for g in (1, 2, 3):
            fing[:, k] += gew[f"finger{k}-{g}.R"][benutzt]
    teil_e = np.where(fing.max(axis=1) > 0.5, fing.argmax(axis=1), 0)       # 0 Platte, 1 Daumen, 2 Zeige .. 5 Klein
    zu_teil = {0: 1, 1: 6, 2: 2, 3: 3, 4: 4, 5: 5}
    quads = []
    mat = []
    for fc in faces:
        qs = [neu[i] for i in fc]
        ts = [int(teil_e[q]) for q in qs]
        zaehl = {}
        for tt in ts:
            zaehl[tt] = zaehl.get(tt, 0) + 1
        best = max(zaehl.items(), key=lambda kv: (kv[1], kv[0] == 0))[0]
        quads.append(qs)
        mat.append(zu_teil[best] - 1)
    # Fingerknochen im Modellraum
    ketten = {}
    for fname, k in FINGER_KNOCHEN.items():
        ketten[fname] = [(modell(pbs[f"finger{k}-{g}.R"].head), modell(pbs[f"finger{k}-{g}.R"].tail)) for g in (1, 2, 3)]
    meta = {}
    for k in (1, 2, 3, 4):
        meta[k] = (modell(pbs[f"metacarpal{k}.R"].head), modell(pbs[f"metacarpal{k}.R"].tail))
    H_ = Hand()
    H_.P0, H_.quads, H_.mat = P, quads, mat
    H_.ketten, H_.meta = ketten, meta
    H_.arm = Vector((0, 1, 0))
    bpy.data.meshes.remove(me)
    return H_


def teile_bestimmen(h, pos):
    """Teil je Ecke: 0 Handrücken/Handballen/Unterarm, 1 Zeige, 2 Mittel, 3 Ring, 4 Klein, 5 Daumen. Der Finger beginnt an der
    Ebene durch sein Grundgelenk (Daumen: Ende des ersten Gliedes); jede Ecke dahinter gehört zum nächsten Finger. So werden die
    Grenzen gerade und ruhig (die Fingerfugen im Bild sollen nicht zacken)."""
    namen = ["zeige", "mittel", "ring", "klein", "daumen"]
    abst = np.full((len(pos), 5), 1e9)
    for j, fname in enumerate(namen):
        kette = h.ketten[fname]
        dmin = np.full(len(pos), 1e9)
        for (kopf, spitze) in kette:
            ab = spitze - kopf
            t = np.clip(((pos - kopf) @ ab) / (ab @ ab), 0, 1)
            dmin = np.minimum(dmin, np.linalg.norm(pos - (kopf + t[:, None] * ab), axis=1))
        if fname == "daumen":
            ebene, nrm = kette[0][1], kette[0][1] - kette[0][0]
        else:
            ebene, nrm = kette[0][0], kette[0][1] - kette[0][0]
        dahinter = ((pos - ebene) @ nrm) >= 0
        abst[:, j] = np.where(dahinter, dmin, 1e9)
    nah = abst.argmin(axis=1)
    return np.where(abst.min(axis=1) < 0.22, nah + 1, 0)


def netz_dicken(h):
    """Unterteilen, entlang der Normalen aufdicken, Falten an Gelenken und Rücken eindrücken; Ergebnis als Zahlenfelder."""
    me = bpy.data.meshes.new("rohhand")
    me.from_pydata([tuple(p) for p in h.P0], [], [tuple(q) for q in h.quads])
    me.update()
    me.polygons.foreach_set("material_index", h.mat)
    o = bpy.data.objects.new("rohhand", me)
    bpy.context.scene.collection.objects.link(o)
    md = o.modifiers.new("U", "SUBSURF")
    md.levels = md.render_levels = UNTERTEILUNG
    md.boundary_smooth = "ALL"
    bpy.context.view_layer.update()
    ev = bpy.data.meshes.new_from_object(o.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(o)
    bpy.data.meshes.remove(me)
    bm = bmesh.new()
    bm.from_mesh(ev)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    bm.normal_update()
    nv = len(bm.verts)
    pos = np.array([v.co[:] for v in bm.verts])
    nor = np.array([v.normal[:] for v in bm.verts])
    nf = len(bm.faces)
    fl = [tuple(v.index for v in fc.verts) for fc in bm.faces]
    bm.free()
    bpy.data.meshes.remove(ev)
    teil = teile_bestimmen(h, pos)
    fm = []
    for fc in fl:
        z = {}
        for i in fc:
            z[teil[i]] = z.get(teil[i], 0) + 1
        fm.append(max(z.items(), key=lambda kv: (kv[1], kv[0] == teil[fc[0]]))[0])
    dz = np.full(nv, DICKE)
    # ── Falten an den Fingergelenken ──
    zdir = np.array([0.0, 0.0, 1.0])
    ruecken = np.clip(nor @ zdir, -1, 1)
    for fname, kette in h.ketten.items():
        tn = {"daumen": 5, "zeige": 1, "mittel": 2, "ring": 3, "klein": 4}[fname]
        ids = np.where(teil == tn)[0]
        if len(ids) == 0:
            continue
        pp = pos[ids]
        for g in (1, 2):
            kopf, spitze = kette[g]
            ach = spitze - kopf
            ach = ach / np.linalg.norm(ach)
            vor = kette[g - 1][1] - kette[g - 1][0]
            vor = vor / np.linalg.norm(vor)
            m_ach = (ach + vor)
            m_ach = m_ach / np.linalg.norm(m_ach)
            d = (pp - kopf) @ m_ach
            gau = np.exp(-(d / 0.034) ** 2)
            rip = np.cos(2 * math.pi * d / 0.085) * np.exp(-(d / 0.075) ** 2)
            rueck = np.clip(ruecken[ids] + 0.35, 0, 1)
            palm = np.clip(-ruecken[ids] + 0.15, 0, 1)
            dz[ids] += -0.013 * gau * (0.45 + 0.55 * palm) - 0.0032 * rip * rueck
        # Grundgelenk: leichter Knöchel
        kopf0 = kette[0][0]
        a0 = kette[0][1] - kopf0
        d0 = (pp - kopf0) @ a0 / np.linalg.norm(a0)
        dz[ids] += 0.012 * np.exp(-(d0 / 0.075) ** 2) * np.clip(ruecken[ids], 0, 1)
    # ── Handrücken und Handballen: weiche Falten ──
    from mathutils import noise
    pl = np.where(teil == 0)[0]
    for i in pl:
        p = Vector(pos[i])
        r1 = 1.0 - abs(noise.noise(p * 2.6 + Vector((3.1, 1.7, 0.4))))
        r2 = 1.0 - abs(noise.noise(p * 5.5 + Vector((0.5, 9.2, 4.4))))
        f_ = (max(r1 - 0.78, 0) / 0.22) ** 1.6
        f2 = (max(r2 - 0.82, 0) / 0.18) ** 1.6
        dz[i] += -0.012 * f_ * (0.4 + 0.6 * max(ruecken[i], 0)) - 0.005 * f2
    # ── Sehnen auf dem Handrücken: weiche Rippen von den Fingerwurzeln zum Handgelenk ──
    pl = np.where(teil == 0)[0]
    for fname in ("zeige", "mittel", "ring", "klein"):
        mcp = h.ketten[fname][0][0]
        a_ = np.array([mcp[0] * 0.55, 0.05])
        e_ = np.array([mcp[0], mcp[1] - 0.05])
        ab = e_ - a_
        t = np.clip(((pos[pl][:, [0, 1]] - a_) @ ab) / (ab @ ab), 0, 1)
        dist = np.linalg.norm(pos[pl][:, [0, 1]] - (a_ + t[:, None] * ab), axis=1)
        dz[pl] += 0.011 * np.exp(-(dist / 0.05) ** 2) * np.clip(ruecken[pl], 0, 1) * np.sin(math.pi * np.clip(t * 0.9 + 0.08, 0, 1)) ** 0.7
    pos2 = pos + nor * dz[:, None]
    # nach der Verschiebung Normalen neu
    me2 = bpy.data.meshes.new("hand")
    me2.from_pydata([tuple(p) for p in pos2], [], fl)
    me2.update()
    me2.polygons.foreach_set("material_index", fm)
    for pg in me2.polygons:
        pg.use_smooth = True
    bm2 = bmesh.new()
    bm2.from_mesh(me2)
    bm2.normal_update()
    nor2 = np.array([v.normal[:] for v in bm2.verts])
    bm2.free()
    h.pos, h.nor, h.fl, h.fm, h.teil = pos2, nor2, fl, fm, teil
    h.bvh = BVHTree.FromPolygons([Vector(p) for p in pos2], fl)
    return h


# ─────────────────────────────────────────────────────────────────────────────
#  Bauteile auf dem Netz: Stiche, Ringe, Stulpe
# ─────────────────────────────────────────────────────────────────────────────

def auf_netz(h, p, richtung, weit=6.0):
    """Erster Treffer eines Strahls auf dem Handnetz: (Punkt, Normale) oder None."""
    ort, nor, _i, _d = h.bvh.ray_cast(Vector(p), Vector(richtung).normalized(), weit)
    return (ort, nor) if ort is not None else None


def fingerrahmen(kette, g, anteil):
    """Punkt auf dem Fingerknochen g (0..2) bei `anteil`, Achse, seitlich (Richtung -X ist zur Daumenseite), Rückenrichtung."""
    kopf, spitze = kette[g]
    a = Vector(spitze - kopf)
    laenge = a.length
    a.normalize()
    seit = a.cross(Vector((0, 0, 1)))
    if seit.length < 1e-6:
        seit = Vector((1, 0, 0))
    seit.normalize()
    rueck = seit.cross(a).normalized()
    if rueck.z < 0:
        rueck = -rueck
    return Vector(kopf) + a * laenge * anteil, a, seit, rueck, laenge


def stich_pfad(h, kette, seite):
    """Naht entlang der Fingerseite (seite = +1/-1): Punkte (p, n) vom Grundgelenk bis kurz vor die Kuppe."""
    pts = []
    for g in range(3):
        for k in range(8 if g < 2 else 6):
            q, a, seit, rueck, _l = fingerrahmen(kette, g, k / 8)
            tr = auf_netz(h, q - rueck * 0.045, seit * seite)
            if tr:
                pts.append((tr[0], tr[1]))
    return pts


def rueckenpfad(h, x0, y0, x1, y1, n=18):
    pts = []
    for k in range(n + 1):
        t = k / n
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        tr = auf_netz(h, (x, y, 3.0), (0, 0, -1))
        if tr:
            pts.append((tr[0], tr[1]))
    return pts


def stiche(b, name, pts, abstand, groesse, rng=None, hub=0.0, anhub=0.004):
    """Stiche entlang eines Linienzugs auf der Oberfläche: Länge in Pfadrichtung, kleine Erhebung über der Haut."""
    if len(pts) < 2:
        return
    rest = 0.0
    for (p0, n0), (p1, n1) in zip(pts[:-1], pts[1:]):
        d = Vector(p1) - Vector(p0)
        l = d.length
        if l < 1e-6:
            continue
        tv = d / l
        t = rest
        while t < l:
            q = Vector(p0) + tv * t
            nn = (Vector(n0) * (1 - t / l) + Vector(n1) * (t / l)).normalized()
            if rng:
                q = q + tv.cross(nn) * rng.uniform(-hub, hub)
            y = nn.cross(tv).normalized()
            xv = y.cross(nn).normalized()
            b.quader(name, q + nn * anhub, groesse, Matrix((xv, y, nn)).transposed().to_3x3())
            t += abstand
        rest = t - l


def perlen_pfad(b, name, pts, abstand, r):
    if len(pts) < 2:
        return
    rest = 0.0
    for (p0, n0), (p1, n1) in zip(pts[:-1], pts[1:]):
        d = Vector(p1) - Vector(p0)
        l = d.length
        if l < 1e-6:
            continue
        t = rest
        while t < l:
            b.kugel(name, Vector(p0) + d / l * t + Vector(n0) * 0.004, r, seg=8)
            t += abstand
        rest = t - l


def band_um(b, name, c, u, v, a, ru, rv, dicke, seg=48, ring_=7, wellen=0.0):
    """Wulst (Röhre) um eine Ellipse mit Mitte c, Halbachsen ru (Richtung u) und rv (Richtung v), Achse a."""
    verts = []
    bm = b._b(name)
    for i in range(seg):
        w = 2 * math.pi * i / seg
        mitte = c + u * (ru * math.cos(w)) + v * (rv * math.sin(w))
        nach_aussen = (u * (math.cos(w) / ru) + v * (math.sin(w) / rv)).normalized()
        rr = []
        for j in range(ring_):
            bb = 2 * math.pi * j / ring_
            rr.append(bm.verts.new(mitte + nach_aussen * (dicke * math.cos(bb)) + a * (dicke * math.sin(bb))))
        verts.append(rr)
    for i in range(seg):
        for j in range(ring_):
            try:
                bm.faces.new((verts[i][j], verts[(i + 1) % seg][j], verts[(i + 1) % seg][(j + 1) % ring_], verts[i][(j + 1) % ring_]))
            except ValueError:
                pass


def finger_ring(b, h, fname, g, anteil, name, breite, ueber=0.012):
    """Ring um einen Finger: Ellipse aus Strahlen ringsum."""
    kette = h.ketten[fname]
    q, a, seit, rueck, _l = fingerrahmen(kette, g, anteil)
    mass = []
    for r_ in (seit, -seit, rueck, -rueck):
        tr = auf_netz(h, q, r_)
        mass.append((tr[0] - q).length if tr else 0.1)
    ru = (mass[0] + mass[1]) / 2 + ueber
    rv = (mass[2] + mass[3]) / 2 + ueber
    mitte = q + seit * ((mass[0] - mass[1]) / 2) + rueck * ((mass[2] - mass[3]) / 2)
    band_um(b, name, mitte, seit, rueck, a, ru, rv, breite * 0.5, seg=36, ring_=7)
    return mitte, a, seit, rueck, ru, rv


class Stulpe:
    """Stulpe um das Unterarmstück: Ellipsenschnitte aus dem Netz, nach hinten weich ausgestellt."""

    def __init__(self, h, laenge):
        self.h = h
        self.laenge = laenge
        pos = h.pos
        self.stationen = []
        for k in range(15):
            s = 0.0 - laenge * k / 14 - 0.02
            sel = (np.abs(pos[:, 1] - s) < 0.035) & (h.teil == 0)
            pp = pos[sel][:, [0, 2]]
            if len(pp) < 4:
                self.stationen.append(self.stationen[-1] if self.stationen else (0.0, 0.0, 0.3, 0.3))
                continue
            x0, x1 = np.percentile(pp[:, 0], [1, 99])
            z0, z1 = np.percentile(pp[:, 1], [1, 99])
            self.stationen.append(((x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2, (z1 - z0) / 2))
        self.hinten = -laenge - 0.02
        self.vorn = -0.02

    def schnitt(self, y):
        """Mitte (x, z) und Halbachsen (ru, rv) bei y, inklusive Ausstellung zum offenen Ende hin."""
        t = min(max((self.vorn - y) / self.laenge, 0.0), 1.0)
        k = t * 14
        i = min(int(k), 13)
        fr = k - i
        a, b = self.stationen[i], self.stationen[i + 1]
        cx, cz, ru, rv = (a[j] * (1 - fr) + b[j] * fr for j in range(4))
        flare = 0.10 * t * (2 - t) + 0.10 * t ** 2.4
        return cx, cz, (ru + 0.045) * (1 + flare), (rv + 0.045) * (1 + flare * 1.2)

    def punkt(self, y, w, zusatz=0.0):
        cx, cz, ru, rv = self.schnitt(y)
        return Vector((cx + (ru + zusatz) * math.cos(w), y, cz + (rv + zusatz) * math.sin(w)))

    def bauen(self, b, aussen, dicke=0.05, seg=44, stufen=15):
        h = 1.0 / (stufen - 1)
        ys = [self.vorn - self.laenge * k * h for k in range(stufen)]
        bm = b._b(aussen)
        bi = b._b("innen")
        gr, gi = [], []
        for k, y in enumerate(ys):
            t = k * h
            gr.append([])
            gi.append([])
            for j in range(seg):
                w = 2 * math.pi * j / seg
                wel = 1 + 0.045 * t * math.sin(5 * w + 0.6) + 0.02 * t * math.sin(9 * w + 1.3 * t)
                cx, cz, ru, rv = self.schnitt(y)
                gr[-1].append(bm.verts.new(Vector((cx + ru * wel * math.cos(w), y, cz + rv * wel * math.sin(w)))))
                gi[-1].append(bi.verts.new(Vector((cx + (ru * wel - dicke) * math.cos(w), y, cz + (rv * wel - dicke) * math.sin(w)))))
        for k in range(len(ys) - 1):
            for j in range(seg):
                try:
                    bm.faces.new((gr[k][j], gr[k + 1][j], gr[k + 1][(j + 1) % seg], gr[k][(j + 1) % seg]))
                    bi.faces.new((gi[k][j], gi[k + 1][j], gi[k + 1][(j + 1) % seg], gi[k][(j + 1) % seg]))
                except ValueError:
                    pass
        # aufgerollte Lippen an beiden Rändern
        for y, zeichen in ((ys[0], 1), (ys[-1], -1)):
            cx, cz, ru, rv = self.schnitt(y)
            band_um(b, aussen, Vector((cx, y, cz)), Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0)), ru - dicke * 0.5, rv - dicke * 0.5, dicke * 0.62, seg=seg, ring_=7)
        # Boden aus Futter am Ende, damit man nicht in das Unterarmstück sieht
        y = ys[-1] + 0.01
        cx, cz, ru, rv = self.schnitt(ys[-1])
        pts = [bi.verts.new(Vector((cx + (ru - dicke) * math.cos(2 * math.pi * j / seg), y, cz + (rv - dicke) * math.sin(2 * math.pi * j / seg)))) for j in range(seg)]
        try:
            bi.faces.new(pts)
        except ValueError:
            pass


# ─────────────────────────────────────────────────────────────────────────────
#  Handschuh zusammenbauen
# ─────────────────────────────────────────────────────────────────────────────

class HandBau(H.Bau):
    """Wie Bau, aber mit dem gescannten Handnetz als eigenem Objekt (sechs Stoff-Plätze, Teile für die Fugenlinien)."""

    def __init__(self, stoffe, h):
        super().__init__(stoffe)
        self.h = h
        self.id_objekte = []

    def fertig(self, drehung):
        objs = super().fertig(drehung)
        sz = bpy.context.scene
        rot = Matrix.Rotation(math.radians(drehung), 3, "Z")
        h = self.h
        pos = np.array([(rot @ Vector(p))[:] for p in h.pos])
        self.punkte.extend(Vector(p) for p in pos[::7])
        me = bpy.data.meshes.new("hand")
        me.from_pydata([tuple(p) for p in pos], [], h.fl)
        me.update()
        for _ in range(6):
            me.materials.append(self.st["leder"])
        me.polygons.foreach_set("material_index", h.fm)
        for pg in me.polygons:
            pg.use_smooth = True
        o = bpy.data.objects.new("hand", me)
        o.pass_index = 1
        sz.collection.objects.link(o)
        objs.append(o)
        # Teile für die Fugen: je Teil ein eigenes Objekt, nur in der Nummern-Aufnahme sichtbar
        for tn in range(6):
            sel = [fc for fc, m in zip(h.fl, h.fm) if m == tn]
            if not sel:
                continue
            m2 = bpy.data.meshes.new(f"teil{tn}")
            m2.from_pydata([tuple(p) for p in pos], [], sel)
            m2.update()
            o2 = bpy.data.objects.new(f"teil{tn}", m2)
            o2.pass_index = 1 if tn == 5 else tn + 1      # Daumen gehört zur Platte: keine zackige Linie am Ballen
            o2.hide_render = True
            o2["idobj"] = 1
            sz.collection.objects.link(o2)
            self.id_objekte.append(o2)
        return objs


def deko(b, h, stufe, S, rng, ribs):
    """Schmuck und Nähte je Stufe auf dem Netz (Handkoordinaten, vor der Drehung)."""
    G = "garn"
    kn = h.ketten
    # Nähte an den Fingerseiten (alle Stufen), feiner bei den hohen Stufen
    for fname in ("zeige", "mittel", "ring", "klein", "daumen"):
        for sg in (-1, 1):
            pf = stich_pfad(h, kn[fname], sg)
            if stufe == 1:
                stiche(b, G, pf, 0.07, (0.040, 0.012, 0.012), rng, 0.006)
            elif stufe in (5, 6):
                perlen_pfad(b, "gold", pf, 0.05, 0.0085) if stufe == 6 else stiche(b, "gold", pf, 0.05, (0.030, 0.009, 0.009))
            else:
                stiche(b, G, pf, 0.055, (0.032, 0.010, 0.010))
    # Rückennähte
    for pf in ribs:
        if stufe == 1:
            stiche(b, G, pf[:9], 0.08, (0.04, 0.013, 0.013), rng, 0.008)
        elif stufe in (5, 6):
            perlen_pfad(b, "gold", pf, 0.05, 0.012)
        else:
            stiche(b, G, pf, 0.06, (0.036, 0.011, 0.011))

    def sband(name, y, dicke, plus=0.0):
        cx, cz, ru, rv = S.schnitt(y)
        band_um(b, name, Vector((cx, y, cz)), Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0)), ru + dicke * 0.3 + plus, rv + dicke * 0.3 + plus, dicke, seg=48, ring_=7)

    def ssaum(name, y, abstand, groesse):
        pf = []
        for w in np.linspace(0.0, math.pi, 30):
            p = S.punkt(y, w, 0.012)
            pf.append((p, S.punkt(y, w, 1.0) - S.punkt(y, w, 0.0)))
        stiche_ring(b, name, pf, abstand, groesse)
    hi, vo = S.hinten, S.vorn
    if stufe == 1:
        # Flicken auf dem Handrücken, Wulst am Rand, kleiner Flicken am Zeigefinger
        tr = auf_netz(h, (0.15, 0.62, 3.0), (0, 0, -1))
        if tr:
            p, nn = tr
            nn = Vector(nn)
            y = nn.cross(Vector((0.2, 1, 0))).normalized()
            xv = y.cross(nn).normalized()
            m3 = Matrix((xv, y, nn)).transposed().to_3x3()
            b.quader("flicken", Vector(p) + nn * 0.012, (0.30, 0.25, 0.034), m3)
            ecken = [(-0.15, -0.125), (0.15, -0.125), (0.15, 0.125), (-0.15, 0.125), (-0.15, -0.125)]
            pf = [(Vector(p) + xv * ex + y * ey + nn * 0.03, nn) for ex, ey in ecken]
            stiche(b, G, pf, 0.05, (0.032, 0.014, 0.014), rng, 0.006)
        q, a, seit, rueck, _l = fingerrahmen(kn["zeige"], 0, 0.55)
        tr = auf_netz(h, q + rueck * 0.0, rueck)
        if tr:
            p, nn = tr
            nn = Vector(nn)
            y = a
            xv = y.cross(nn).normalized()
            b.quader("flicken", Vector(p) + nn * 0.010, (0.15, 0.14, 0.03), Matrix((xv, y, nn)).transposed().to_3x3())
        sband("dunkel", hi + 0.04, 0.034)
        ssaum(G, hi + 0.12, 0.07, (0.04, 0.012, 0.012))
    elif stufe == 2:
        ssaum(G, hi + 0.075, 0.07, (0.04, 0.012, 0.012))
        sband("dunkel", hi + 0.025, 0.02)
        sband("dunkel", vo - 0.02, 0.016)
    elif stufe == 3:
        mitte = vo - S.laenge * 0.45
        sband("gold", mitte + 0.07, 0.011)
        sband("gold", mitte - 0.07, 0.011)
        sband("rot", mitte, 0.042)
        sband("gold", hi + 0.025, 0.016)
        for w in np.linspace(0.12, math.pi - 0.12, 13):
            b.kugel("garn", S.punkt(mitte, w, 0.044), 0.016, seg=8)
    elif stufe == 4:
        sband("gold", hi + 0.03, 0.036)
        sband("gold", hi + 0.13, 0.012)
        finger_ring(b, h, "ring", 0, 0.42, "gold", 0.075)
    else:
        sband("gold", hi + 0.03, 0.036)
        sband("gold", hi + 0.13, 0.013)
        sband("gold", hi + 0.17, 0.009)
        for w in np.linspace(0.15, math.pi - 0.15, 12):
            b.kugel("gold", S.punkt(hi + 0.22, w, 0.016), 0.017, seg=8)
        # Zierbogen auf dem Handrücken
        for sgn in (-1, 1):
            pf = []
            for tt in np.linspace(0, 1, 11):
                x = sgn * (0.07 + 0.26 * tt)
                y = 0.55 + 0.34 * tt * tt
                tr = auf_netz(h, (x, y, 3.0), (0, 0, -1))
                if tr:
                    pf.append((tr[0], tr[1]))
            perlen_pfad(b, "gold", pf, 0.04, 0.011)
        if stufe == 5:
            # Siegelring am Ringfinger
            mitte, a, seit, rueck, ru, rv = finger_ring(b, h, "ring", 0, 0.42, "gold", 0.085)
            xq = a.cross(rueck).normalized()
            b.quader("gold", mitte + rueck * (rv + 0.01), (0.10, 0.12, 0.04), Matrix((seit, a, rueck)).transposed().to_3x3())
            b.kugel("stein", mitte + rueck * (rv + 0.034), 0.036, (1, 1.1, 0.7), Matrix((seit, a, rueck)).transposed().to_3x3(), seg=10)
        else:
            # Hermelinpelz an der Stulpe und ein einziger Siegelring am Zeigefinger
            y0 = hi - 0.01
            for lage, (dy, n_, rad) in enumerate(((0.0, 84, 0.062), (0.085, 78, 0.06), (0.165, 72, 0.058))):
                for k in range(n_):
                    w = 2 * math.pi * (k + 0.5 * lage) / n_ + rng.uniform(-0.03, 0.03)
                    p = S.punkt(y0 + dy + rng.uniform(-0.025, 0.025), w, 0.025 + 0.008 * lage)
                    schwanz = (k % 8 == 3 and lage == 1)
                    nm = "pelz_dunkel" if schwanz else "pelz_hell"
                    b.kugel(nm, p, rad * rng.uniform(0.85, 1.12) * (0.7 if schwanz else 1.0), (1.0, 1.0, 1.35 if schwanz else 1.2), seg=9)
            mitte, a, seit, rueck, ru, rv = finger_ring(b, h, "zeige", 0, 0.5, "gold", 0.085)
            m3 = Matrix((seit, a, rueck)).transposed().to_3x3()
            b.quader("gold", mitte + rueck * (rv + 0.01), (0.10, 0.12, 0.04), m3)
            b.kugel("stein", mitte + rueck * (rv + 0.034), 0.036, (1, 1.1, 0.7), m3, seg=10)


def stiche_ring(b, name, pf, abstand, groesse):
    """Stiche entlang einer Kurve um die Stulpe (Punkt, nach außen)."""
    pts = [(p, n) for p, n in pf]
    for (p0, n0), (p1, n1) in zip(pts[:-1], pts[1:]):
        d = Vector(p1) - Vector(p0)
        l = d.length
        tv = d / l
        nn = Vector(n0).normalized()
        y = nn.cross(tv).normalized()
        b.quader(name, p0, groesse, Matrix((tv, y, nn)).transposed().to_3x3())


def hand_bauen(stufe, h, st):
    b = HandBau(st, h)
    rng = random.Random(1480 + stufe)
    S = Stulpe(h, 0.62 if stufe in (3, 6) else 0.52)
    aussen = "rot@platte" if stufe == 3 else "leder@platte"
    S.bauen(b, aussen)
    # Rückennähte: drei Linien zwischen den Fingerwurzeln
    mcp = [h.ketten[f][0][0] for f in ("zeige", "mittel", "ring", "klein")]
    ribs = []
    for i in range(3):
        xm = (mcp[i][0] + mcp[i + 1][0]) / 2
        ym = (mcp[i][1] + mcp[i + 1][1]) / 2 - 0.12
        ribs.append(rueckenpfad(h, xm * 0.55, 0.2, xm, ym))
    deko(b, h, stufe, S, rng, ribs)
    # Spitze des Zeigefingers = Klickpunkt
    kopf, spitze = h.ketten["zeige"][2]
    a = (spitze - kopf) / np.linalg.norm(spitze - kopf)
    sel = np.where(h.teil == 1)[0]
    best = sel[np.argmax((h.pos[sel] - kopf) @ a)]
    b.tipp = Vector(h.pos[best])
    return b


# ─────────────────────────────────────────────────────────────────────────────
#  Rendern
# ─────────────────────────────────────────────────────────────────────────────

def szene_aufbauen(h, stufe, warm):
    H.neue_szene()
    st = H.stoffe_fuer(stufe, warm)
    # das Handnetz muss nach neue_szene (liest Werkzeug-Daten zurück) neu als Mesh bestehen
    b = hand_bauen(stufe, h, st)
    objs = b.fertig(H.DREHUNG)
    return b, objs


def id_aufnehmen(b, tmp):
    for o in b.id_objekte:
        o.hide_render = False
    for o in bpy.context.scene.objects:
        if o.name == "hand":
            o.hide_render = True
    a = H.id_aufnehmen(tmp)
    for o in b.id_objekte:
        o.hide_render = True
    for o in bpy.context.scene.objects:
        if o.name == "hand":
            o.hide_render = False
    return a


def alles(aus, stufen, gross_stufen, h):
    tmp = os.path.join(aus, "_tmp.png")
    os.makedirs(aus, exist_ok=True)
    b, _ = szene_aufbauen(h, 6, False)
    rahmen = H.ausschnitt_aus(b.punkte)
    ergebnis, hot = {}, {}
    for stufe in stufen:
        s = H.STUFEN[stufe - 1]
        for zustand in ("normal", "klick"):
            b, _ = szene_aufbauen(h, stufe, zustand == "klick")
            H.kamera_setzen(rahmen)
            hot_ = H.hotspot_norm(b.tipp)
            name = f"stufe{stufe}_{s['name']}_{zustand}"
            gross = H.aufnehmen(tmp)
            ids = id_aufnehmen(b, tmp)
            ergebnis[name] = (gross, ids)
            hot[name] = hot_
            print("fertig", name, flush=True)
            if zustand == "normal" and stufe in gross_stufen:
                sz = bpy.context.scene
                alt = (H.SUPER, sz.cycles.samples, sz.cycles.use_denoising)
                H.SUPER = GROSS
                sz.render.resolution_x = sz.render.resolution_y = GROSS
                sz.cycles.samples = 200
                sz.cycles.use_denoising = True
                g2 = H.aufnehmen(tmp)
                i2 = id_aufnehmen(b, tmp)
                H.png_schreiben(os.path.join(aus, "gross", name + ".png"), H.srgb8(H.fugen(g2, i2, 110, True)))
                H.SUPER, sz.cycles.samples, sz.cycles.use_denoising = alt
                sz.render.resolution_x = sz.render.resolution_y = H.SUPER
    if os.path.exists(tmp):
        os.remove(tmp)
    for name, (gross, ids) in ergebnis.items():
        for g in H.GROESSEN:
            n = H.SUPER // g
            a = H.kontur(H.fugen_klein(H.mitteln(H.fugen(gross, ids, g, True), n), ids, n, True))
            H.png_schreiben(os.path.join(aus, "fein", str(g), name + ".png"), H.srgb8(a))
    zeilen = [name + ": " + ", ".join(f"{g}px=({round(hs[0] * g)},{round(hs[1] * g)})" for g in H.GROESSEN) for name, hs in hot.items()]
    open(os.path.join(aus, "hotspots.txt"), "w").write("\n".join(zeilen) + "\n")


GROSS = 512


def main():
    a = H.args()
    aus = a.get("aus", os.path.join(os.getcwd(), "mauszeiger3d_mpfb"))
    stufen = [int(x) for x in a.get("stufen", "1,2,3,4,5,6").split(",")]
    gross_stufen = [int(x) for x in a.get("gross", "2,6").split(",")]
    h = hand_vorbereiten(POSE)
    netz_dicken(h)
    print("Hand:", len(h.pos), "Ecken,", len(h.fl), "Flächen", flush=True)
    alles(aus, stufen, gross_stufen, h)


if __name__ == "__main__":
    main()
