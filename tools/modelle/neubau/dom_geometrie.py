"""Neubau Dom: Geometrie-Pruefungen am fertigen GLB mit Strahlen (laeuft in Blender, weil es dessen BVH-Baum nutzt).

    blender -b --python tools/modelle/neubau/dom_geometrie.py -- [--modell=dom_katholisch|dom_evangelisch] [--glb=<datei>] [--schnell]

Ausgabe: je Pruefung eine Zeile `GEOMETRIE|<Name>|<Soll>|<Ist>|ok|FEHLER`, danach Einzelbefunde (`BEFUND|...`). dom_messen.py ruft das Skript
selbst auf und nimmt die Zeilen in seine Tabelle. Hintergrund: Ein unabhaengiger Pruefer fand am 25.11.2026 durch Strahlen und Schnitte
Fehler, die weder Dreiecksbudget noch Masse zeigen (Spalt an jedem Grat, Seitenflaechen mit falscher Normale, offene Oberseiten).

  a  Durchsicht: von oben und aus 8 schraegen Richtungen (50 Grad) darf man durch die Huelle nicht in den Himmel sehen (ein Loch
     innerhalb der Silhouette, dessen Rand auf einer Tiefe liegt), Schritt 2 cm bzw. 3 cm; Ausnahme: die offene Glockenstube
     (durchsicht_erlaubt der Massdatei)
  b  Rueckseiten: aus 64 Richtungen (4 Hoehenwinkel mal 16) darf der erste Treffer keine Rueckseite sein (falsche Normale, offene Kante);
     Dach nur ab 55 Grad, ab 6 Treffern je 0,5 m (flach gesehen streifen die Keile der Deckreihen)
  c  Spalt zwischen losen Schieferteilen: Randkanten verschiedener Teile 5 mm bis 100 mm voneinander entfernt, nicht von einem Blech
     (Gratabdeckung) ueberdeckt
  d  Oberseiten: der Blick senkrecht und mit 70 Grad von oben trifft am Haus nie eine Rueckseite (offene Oberseite von Sockeln und
     Absaetzen, ab 10 Treffern je 0,5 m: Streifschuesse an Mauerecken zaehlen nicht)
  e  Fenster in freier Wand: ein Strahl von 0,3 bis 1,3 m vor jeder Scheibe (nach aussen, Normale der Scheibe) trifft nichts
"""
import math
import os
import sys
from collections import deque

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
OPT = dict((a[2:].split("=", 1) + ["1"])[:2] for a in sys.argv[sys.argv.index("--") + 1:] if a.startswith("--")) if "--" in sys.argv else {}
MODELL = OPT.get("modell", "dom_evangelisch")
GLB = OPT.get("glb") or os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(HIER))), "game", "assets", "modelle", "gebaeude", "neubau", MODELL + ".glb")
SCHNELL = "schnell" in OPT
M = __import__("dom_masse" if MODELL == "dom_katholisch" else MODELL + "_masse")
ERLAUBT = getattr(M, "durchsicht_erlaubt", lambda p: False)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
verts, tris, info = [], [], []                  # info je Dreieck: (Objekt, Stoff)
glas = set()
for name in ("Haus", "Dach", "Scheiben"):
    o = bpy.data.objects.get(name)
    if o is None: continue
    me = o.data; me.calc_loop_triangles()
    base = len(verts)
    verts += [o.matrix_world @ v.co for v in me.vertices]
    for t in me.loop_triangles:
        mat = o.material_slots[me.polygons[t.polygon_index].material_index].material.name if o.material_slots else ""
        if name == "Scheiben": glas.add(len(tris))
        tris.append(tuple(base + i for i in t.vertices)); info.append((name, mat))
bvh = BVHTree.FromPolygons(verts, tris)
allv = np.array([tuple(v) for v in verts])
lo, hi = allv.min(axis=0), allv.max(axis=0)
MITTE = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2))
RADIUS = float(np.linalg.norm(hi - lo)) / 2 + 2.0
zeilen, befunde = [], []
f = lambda v: f"{v:.2f}".replace(".", ",")


def zeile(name, soll, ist, ok):
    zeilen.append((name, soll, ist, bool(ok)))
    print(f"GEOMETRIE|{name}|{soll}|{ist}|{'ok' if ok else 'FEHLER'}", flush=True)


def basis(d):
    u = d.cross(Vector((0, 0, 1)))
    if u.length < 1e-6: u = Vector((1, 0, 0))
    u.normalize()
    return u, d.cross(u).normalized()


def rastern(d, step):
    """Treffer-Gitter einer Parallelprojektion in Richtung d: (hit, pos, rueck, glas) je Gitterpunkt und die Basis."""
    d = d.normalized(); u, v = basis(d)
    U, V = allv @ np.array(u), allv @ np.array(v)
    u0, v0 = U.min() - 0.3, V.min() - 0.3
    nu, nv = int((U.max() + 0.3 - u0) / step) + 1, int((V.max() + 0.3 - v0) / step) + 1
    C = -d * (RADIUS * 3 + MITTE.length)          # u0, v0 sind vom Ursprung aus gemessen: die Mitte des Gitters darf nicht seitlich verschoben sein (sonst fehlt ein Teil der Huelle)
    hit = np.zeros((nv, nu), bool); pos = np.zeros((nv, nu, 3), np.float32); rueck = np.zeros((nv, nu), bool); gl = np.zeros((nv, nu), bool)
    idx = np.zeros((nv, nu), np.int32); dist = np.zeros((nv, nu), np.float32)
    for j in range(nv):
        bv = v * (v0 + j * step)
        for i in range(nu):
            h = bvh.ray_cast(C + u * (u0 + i * step) + bv, d)
            if h[0] is None: continue
            hit[j, i] = True; pos[j, i] = h[0]
            gl[j, i] = h[2] in glas; idx[j, i] = h[2]; dist[j, i] = h[3]
            rueck[j, i] = h[1].dot(d) > 0
    rastern.idx = idx; rastern.dist = dist; rastern.o = (C, u, v, u0, v0, step, d)
    return hit, pos, rueck, gl


def aussen_fluten(hit):
    nv, nu = hit.shape
    aus = np.zeros_like(hit); q = deque()
    for j in range(nv):
        for i in (0, nu - 1):
            if not hit[j, i] and not aus[j, i]: aus[j, i] = True; q.append((j, i))
    for i in range(nu):
        for j in (0, nv - 1):
            if not hit[j, i] and not aus[j, i]: aus[j, i] = True; q.append((j, i))
    while q:
        j, i = q.popleft()
        for dj in (-1, 0, 1):
            for di in (-1, 0, 1):
                a, b = j + dj, i + di
                if 0 <= a < nv and 0 <= b < nu and not hit[a, b] and not aus[a, b]: aus[a, b] = True; q.append((a, b))
    return aus


def loecher(d, step, tag):
    hit, pos, rueck, gl = rastern(d, step)
    loch = (~hit) & (~aussen_fluten(hit))
    nv, nu = hit.shape
    seen = set(); res = []
    for j0, i0 in zip(*np.nonzero(loch)):
        if (j0, i0) in seen: continue
        st = [(j0, i0)]; seen.add((j0, i0)); comp = []
        while st:
            j, i = st.pop(); comp.append((j, i))
            for dj in (-1, 0, 1):
                for di in (-1, 0, 1):
                    a, b = j + dj, i + di
                    if 0 <= a < nv and 0 <= b < nu and loch[a, b] and (a, b) not in seen: seen.add((a, b)); st.append((a, b))
        ja, ia = [c[0] for c in comp], [c[1] for c in comp]
        ps = [pos[a, b] for a in range(max(0, min(ja) - 3), min(nv, max(ja) + 4)) for b in range(max(0, min(ia) - 3), min(nu, max(ia) + 4)) if hit[a, b]]
        p = tuple(float(x) for x in np.mean(ps, axis=0)) if ps else (0.0, 0.0, 0.0)
        # Tiefensprung: Luecke zwischen einem nahen und einem fernen Teil (offene Luft neben einer Turmkante, Stoss zweier getrennter Baukoerper): dort
        # springt die Entfernung schon zwischen benachbarten Bildpunkten am Rand des Lochs. Eine Fuge zwischen zwei Flaechen, die sich beruehren
        # sollen, hat auf beiden Seiten nahezu dieselbe Entfernung (Umgebung von 2 Bildpunkten je Lochpunkt, nicht das ganze Rechteck ringsum).
        ds = []
        for (a_, b_) in comp:
            nb_ = [rastern.dist[x, y] for x in range(max(0, a_ - 2), min(nv, a_ + 3)) for y in range(max(0, b_ - 2), min(nu, b_ + 3)) if hit[x, y]]
            if nb_: ds.append(max(nb_) - min(nb_))
        sprung = bool(ds) and max(ds) > 1.5
        C, u, v, u0, v0, st_, dd = rastern.o
        o_ = C + u * (u0 + float(np.mean(ia)) * step) + v * (v0 + float(np.mean(ja)) * step)
        entlang = any(ERLAUBT(tuple(o_ + dd * ((z - o_.z) / dd.z))) for z in np.arange(0.0, hi[2] + 1.0, 0.4)) if abs(dd.z) > 1e-6 else False
        if entlang: continue                                     # der Blick geht durch die offene Glockenstube
        # Lage des Lochs: der Punkt des leeren Strahls, der dem Mittel der Treffer ringsum am naechsten liegt
        # Lage des Lochs: engste Stelle des leeren Strahls (kleinster Abstand zur Huelle entlang des Strahls innerhalb der Huelle)
        bestm, bestq = 1e9, o_
        for t in np.arange(0.0, 4 * RADIUS, 0.1):
            q = o_ + dd * t
            if not (lo[0] - 1 <= q.x <= hi[0] + 1 and lo[1] - 1 <= q.y <= hi[1] + 1 and lo[2] - 1 <= q.z <= hi[2] + 1): continue
            r = bvh.find_nearest(q)
            if r[0] is not None and r[3] < bestm: bestm, bestq = r[3], q
        schmal = min(max(ia) - min(ia) + 1, max(ja) - min(ja) + 1) * step <= 0.13          # Riss von hoechstens 13 cm Breite: zaehlt auch dann, wenn die Naehe springt
        if sprung and not schmal: continue                        # weite Luecke zwischen einem nahen und einem fernen Teil: kein Loch
        res.append((len(comp) * step * step * 1e4, tuple(float(x) for x in bestq), tag))
    return res


# ── a  Durchsicht ──
alle = []
alle += loecher(Vector((0, 0, -1)), 0.03 if SCHNELL else 0.02, "oben")
for az in range(0, 360, 45):
    c = math.cos(math.radians(50))
    alle += loecher(Vector((c * math.cos(math.radians(az)), c * math.sin(math.radians(az)), -math.sin(math.radians(50)))), 0.05 if SCHNELL else 0.03, f"schraeg {az}")
echt = [x for x in alle if x[0] >= 20 and not ERLAUBT(x[1])]            # ab 20 cm2 (5 x 4 cm); darunter Fugen an Kanten ohne Wirkung
for fl, p, tag in sorted(echt, key=lambda x: -x[0])[:25]: befunde.append(f"Durchsicht {tag}: {fl:.1f} cm2 bei {f(p[0])}/{f(p[1])}/{f(p[2])}")
zeile("Durchsicht (oben + 8 schraege Richtungen)", "keine Loecher in der Silhouette", f"{len(echt)} Loecher, zusammen {sum(x[0] for x in echt):.0f} cm2", not echt)

# ── b  Rueckseiten ──
dirs = []
for el in (10, 30, 55, 80):
    for i in range(16):
        az = 2 * math.pi * (i + 0.5 * (el // 30 % 2)) / 16
        c = math.cos(math.radians(el))
        dirs.append(Vector((c * math.cos(az), c * math.sin(az), -math.sin(math.radians(el)))))
step = 0.4 if SCHNELL else 0.25
bad = {}
art = {}
nstr = 0
tri_rueck = {}                  # Dreieck -> Richtungen, aus denen seine Rueckseite zuerst getroffen wurde
for dkey, d in enumerate(dirs):
    hit, pos, rueck, gl = rastern(d, step)
    nstr += int(hit.sum())
    steil = d.z < -0.8                          # ab 55 Grad Hoehenwinkel
    for j, i in zip(*np.nonzero(hit & rueck & ~gl)):
        p = tuple(float(x) for x in pos[j, i])
        if ERLAUBT(p): continue
        # Dach: nur steile Strahlen (flach gesehen streifen Keile der Deckreihen und Gratbleche die Rueckseite des Nachbarn: kein Fehler)
        if info[int(rastern.idx[j, i])][0] != "Haus" and not steil: continue
        key = tuple(round(x / 0.5) for x in p)
        bad[key] = bad.get(key, 0) + 1
        art[key] = info[int(rastern.idx[j, i])] + (tuple(round(x, 2) for x in d),)
        tri_rueck.setdefault(int(rastern.idx[j, i]), set()).add(dkey)
bad = {k: n for k, n in bad.items() if n >= 6}                  # wenige Treffer an einer Kante oder durch eine Fensteroeffnung sind Streifschuesse
for key, n in sorted(bad.items(), key=lambda kv: -kv[1])[:25]: befunde.append(f"Rueckseite bei {f(key[0] * 0.5)}/{f(key[1] * 0.5)}/{f(key[2] * 0.5)}: {n} Treffer, {art[key][0]}/{art[key][1]}, Strahl {art[key][2]}")
zeile("Rueckseiten (64 Richtungen)", "erster Treffer nie eine Rueckseite", f"{len(bad)} Stellen ({nstr} Strahlen)", not bad)

# ── b2  kleine Fehlstellen ──
# Eine Flaeche mit falscher Normale zeigt ihre Rueckseite aus vielen Richtungen; ein Streifschuss an einer Kante trifft sie nur aus einer
# oder zwei. Darum: Dreiecke, deren Rueckseite aus mindestens 3 verschiedenen der 64 Richtungen als erstes getroffen wird (Haus, ausser
# Glockenstube; Dach nur ab 55 Grad ist hier ohne Wirkung, es zaehlen alle Treffer ausser Blech und Schieferkante).
klein = []
for t, ds in tri_rueck.items():
    if len(ds) < 3 or info[t][1] in ("Firstblech", "Schieferkante"): continue
    pa = [verts[k] for k in tris[t]]
    c = (pa[0] + pa[1] + pa[2]) / 3
    if ERLAUBT((c.x, c.y, c.z)): continue
    klein.append((len(ds), c, info[t]))
klein.sort(key=lambda x: -x[0])
for n, c, inf in klein[:15]: befunde.append(f"Rueckseite einer Flaeche aus {n} Richtungen bei {f(c.x)}/{f(c.y)}/{f(c.z)}, {inf[0]}/{inf[1]}")
zeile("Rueckseiten einzelner Flaechen (aus >= 3 Richtungen)", "keine", f"{len(klein)} Dreiecke", not klein)

# ── d  Oberseiten ──
cl = {}
for az in range(-1, 360, 45):                    # -1: senkrecht von oben; sonst 8 Richtungen mit 70 Grad (Absaetze und Sockel von der Seite gesehen)
    d = Vector((0, 0, -1)) if az < 0 else Vector((math.cos(math.radians(70)) * math.cos(math.radians(az)), math.cos(math.radians(70)) * math.sin(math.radians(az)), -math.sin(math.radians(70))))
    hit, pos, rueck, gl = rastern(d, 0.05)
    for j, i in zip(*np.nonzero(hit & rueck & ~gl)):
        if info[int(rastern.idx[j, i])][0] != "Haus": continue
        p = tuple(float(x) for x in pos[j, i])
        if ERLAUBT(p): continue
        key = tuple(round(x / 0.5) for x in p)
        cl[key] = cl.get(key, 0) + 1
cl = {k: n for k, n in cl.items() if n >= 10}
for key, n in sorted(cl.items(), key=lambda kv: -kv[1])[:15]: befunde.append(f"Oberseite offen bei {f(key[0] * 0.5)}/{f(key[1] * 0.5)}/{f(key[2] * 0.5)}: {n} Treffer")
zeile("Oberseiten geschlossen (von oben und 8 x 70 Grad)", "keine Rueckseite des Hauses", f"{len(cl)} Stellen", not cl)

# ── c  Spalt zwischen losen Schieferteilen ──
o = bpy.data.objects.get("Dach")
spalt = []
if o is not None:
    bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.001)
    bm.faces.ensure_lookup_table()
    schiefer = {i for i, s in enumerate(o.material_slots) if s.material and s.material.name == "Schiefer"}
    teil = {}; pid = 0
    for fc in bm.faces:
        if fc.index in teil or fc.material_index not in schiefer: continue
        st = [fc]; teil[fc.index] = pid
        while st:
            a = st.pop()
            for e in a.edges:
                for b in e.link_faces:
                    if b.index not in teil and b.material_index in schiefer: teil[b.index] = pid; st.append(b)
        pid += 1
    proben = []                  # (Punkt, Teil)
    for e in bm.edges:
        fs = [x for x in e.link_faces if x.index in teil]
        if len(e.link_faces) != 1 or len(fs) != 1: continue
        a, b = e.verts[0].co, e.verts[1].co
        n = max(1, int((b - a).length / 0.02))
        for k in range(n + 1): proben.append(((a.lerp(b, k / n)), teil[fs[0].index]))
    from mathutils.kdtree import KDTree
    kd = KDTree(len(proben))
    for i, (p, _) in enumerate(proben): kd.insert(p, i)
    kd.balance()
    for i, (p, t) in enumerate(proben):
        for q, idx, dist in kd.find_range(p, 0.10):
            if proben[idx][1] == t or dist < 0.005: continue
            h = bvh.ray_cast(Vector(((p.x + q.x) / 2, (p.y + q.y) / 2, (p.z + q.z) / 2 + 0.5)), Vector((0, 0, -1)), 1.0)
            if h[0] is not None and info[h[2]][1] == "Firstblech": continue           # von einer Gratabdeckung ueberdeckt
            spalt.append((dist, tuple((p + q) / 2))); break
    bm.free()
cl = {}
for dist, p in spalt: cl.setdefault(tuple(round(x / 0.5) for x in p), []).append(dist)
for key, ds in sorted(cl.items(), key=lambda kv: -len(kv[1]))[:15]: befunde.append(f"Spalt {f(min(ds) * 1000)} bis {f(max(ds) * 1000)} mm bei {f(key[0] * 0.5)}/{f(key[1] * 0.5)}/{f(key[2] * 0.5)}")
zeile("Spalt zwischen Schieferteilen", "kein offener Spalt 5 bis 100 mm", f"{len(cl)} Stellen", not cl)

# ── e  Fenster in freier Wand ──
blind = []
so = bpy.data.objects.get("Scheiben")
if so is not None:
    me = so.data; me.calc_loop_triangles()
    gesehen = set()
    for t in me.loop_triangles:
        mat = so.material_slots[me.polygons[t.polygon_index].material_index].material.name
        if mat != "Fenster": continue
        vs = [so.matrix_world @ me.vertices[i].co for i in t.vertices]
        c = (vs[0] + vs[1] + vs[2]) / 3
        key = tuple(round(x, 1) for x in c)
        if key in gesehen: continue
        gesehen.add(key)
        n = (vs[1] - vs[0]).cross(vs[2] - vs[0])
        if n.length < 1e-9: continue
        n.normalize()
        n.z = 0
        if n.length < 0.5: continue                                      # liegende Scheibe: nicht gemeint
        n.normalize()
        # Die Scheiben sind so gewunden, dass ihre Normale nach aussen zeigt (D.scheibe mit `zu` aussen): kein Raten der Aussenseite
        h = bvh.ray_cast(c + n * 0.3, n, 1.0)
        if h[0] is not None and h[2] not in glas: blind.append(c)
for c in blind[:15]: befunde.append(f"Fenster ohne freie Seite bei {f(c.x)}/{f(c.y)}/{f(c.z)}")
zeile("Fenster in freier Wand", "1,0 m freie Strecke vor jeder Scheibe", f"{len(blind)} Scheiben ohne", not blind)
for b in befunde: print("BEFUND|" + b, flush=True)
