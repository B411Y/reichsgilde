"""Mauszeiger als gerenderter Lederhandschuh: eine Hand, sechs Stände, drei Zustände.

    blender -b --python tools/modelle/handschuh.py -- --aus=<Ordner> [--stufen=1,2,3] [--ohne-uebersicht]

Alles ist eigener Entwurf (Skript, deterministisch, kein Zufall außer dem festen Samen für den Pelz); von „Die Gilde 1“ kommt
nur die Idee eines Handschuh-Zeigers. Gebaut wird eine offene rechte Hand von oben (Handrücken sichtbar), Finger parallel und gestaffelt, Daumen nach links abgespreizt; die ganze Hand ist um 38 Grad gedreht (Finger steil nach links oben, etwa 50 Grad auf dem Bild). Kamera nur 14 Grad geneigt,
Licht von oben links, Standard-Farbverwaltung (kein Farbstich), Hintergrund durchsichtig.

Stufen (gleiche Hand, steigende Wertigkeit): 1 Gemeiner (grobes Arbeitsleder, Flicken), 2 Bürger (helles Leder, saubere Naht),
3 Patrizier (feines Leder, verzierte Stulpe), 4 Edler (dunkles Leder, Goldborte, schlichter Ring), 5 Freiherr (Samt mit
Goldstickerei, Siegelring), 6 Reichsgraf (Königsblauer Samt statt Burgunder, dicker Hermelinpelz an der Stulpe, Goldringe an allen Fingern, Goldkappen).
Zustände: normal, klick (wärmerer, hellerer Ton und etwas mehr Glanz, gleiche Form), tuer_schloss (Bronze-Türschild mit Schlüsselloch und Klinke), tuer_schloss_eisen (dasselbe in dunklem Eisen mit Messing) und tuer_ring (Eisen-Türring). Fingerfugen: zweiter Durchgang mit Teile-Nummern, dunkle Linie und Randlicht je Finger.
Haltung: zeigende Hand, Zeigefinger gestreckt und leicht abgespreizt genau zur Spitze (Hotspot), Mittel-, Ring- und kleiner
Finger etwa zu 20 % einer Faust eingerollt, Daumen locker angelegt.

Ausgabe nach <Ordner>/: fein/<größe>/stufeN_<name>_<zustand>.png (32, 48, 64 Pixel, volle Auflösung = Standard), gross/… (384 Pixel), hotspots.txt, uebersicht.png.
Der Hotspot ist die Spitze des Zeigefingers (beim Türring die Ringmitte).
"""

import math
import os
import random
import struct
import sys
import zlib

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

DREHUNG = 38.0          # Grad nach links oben (gegen den Uhrzeigersinn von oben gesehen)
NEIGUNG = 14.0          # Kamera schaut nur leicht schräg: Handrücken fast frontal (Runde 13, vorher 34)
SUPER = 384             # Rendergröße; alle Zielgrößen entstehen daraus durch Mittelung
GROESSEN = (32, 48, 64)
KONTUR = (0.06, 0.04, 0.03)
FUGE = (0.12, 0.065, 0.03)     # Fugen zwischen den Fingern: dunkelbraun statt schwarz
FUGE_DECK = 0.55               # halbe Deckkraft
KNOECHEL_DECK = 0.27           # Knöchel-Fuge Finger/Handrücken: halbe Stärke von Runde 10 (dort 0,55), dünn

# Stufen: Ledertöne direkt als Basisfarbe (linear, gedeckt)
STUFEN = [
    dict(name="gemeiner", leder=(0.26, 0.17, 0.095), rau=0.95, bump=1.0, var=0.30, stulpe=0.34),
    dict(name="buerger", leder=(0.58, 0.38, 0.19), rau=0.62, bump=0.5, var=0.14, stulpe=0.38),
    dict(name="patrizier", leder=(0.40, 0.21, 0.10), rau=0.38, bump=0.28, var=0.08, stulpe=0.40),
    dict(name="edler", leder=(0.085, 0.052, 0.040), rau=0.32, bump=0.28, var=0.05, stulpe=0.40),
    dict(name="freiherr", leder=(0.20, 0.014, 0.038), rau=0.88, bump=0.10, var=0.06, stulpe=0.42, samt=True),
    dict(name="reichsgraf", leder=(0.02, 0.05, 0.40), rau=0.88, bump=0.10, var=0.06, stulpe=0.42, samt=True),
]
ZUSTAENDE = ("normal", "klick")
ZUSAETZE = ("tuer_schloss", "tuer_schloss_eisen", "tuer_ring")      # unabhängig von der Stufe, nur einmal gebaut

GOLD = (0.95, 0.66, 0.16)


# ─────────────────────────────────────────────────────────────────────────────
#  Szene, Stoffe
# ─────────────────────────────────────────────────────────────────────────────

def neue_szene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sz = bpy.context.scene
    sz.render.engine = "CYCLES"
    sz.cycles.device = "CPU"
    sz.cycles.samples = 40
    sz.cycles.use_denoising = False
    sz.render.film_transparent = True
    sz.render.resolution_x = sz.render.resolution_y = SUPER
    sz.render.resolution_percentage = 100
    sz.render.image_settings.file_format = "PNG"
    sz.render.image_settings.color_mode = "RGBA"
    sz.view_settings.view_transform = "Standard"
    sz.view_settings.look = "None"
    sz.display_settings.display_device = "sRGB"
    w = bpy.data.worlds.new("Welt")
    w.use_nodes = True
    hg = w.node_tree.nodes["Background"]
    hg.inputs["Color"].default_value = (0.62, 0.62, 0.62, 1)
    hg.inputs["Strength"].default_value = 0.55
    sz.world = w
    sonne = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN"))
    sonne.data.energy = 3.4
    sonne.data.angle = math.radians(18)
    sz.collection.objects.link(sonne)
    ort = Vector((-5, 4.5, 7))
    sonne.location = ort
    sonne.rotation_euler = (-ort).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    cam.data.type = "ORTHO"
    sz.collection.objects.link(cam)
    sz.camera = cam
    cam.rotation_euler = (math.radians(NEIGUNG), 0, 0)
    return sz


def stoff(name, farbe, rau=0.6, metall=0.0, bump=0.0, korn=70.0, var=0.0, sheen=0.0, glanz=0.0, glut=None, korn_art="voronoi"):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    pb = nt.nodes["Principled BSDF"]
    tk = nt.nodes.new("ShaderNodeTexCoord")
    farb = Vector(farbe[:3])
    if var > 0:
        rs = nt.nodes.new("ShaderNodeTexNoise")
        rs.inputs["Scale"].default_value = 7.0
        rs.inputs["Detail"].default_value = 4.0
        nt.links.new(tk.outputs["Object"], rs.inputs["Vector"])
        mad = nt.nodes.new("ShaderNodeVectorMath")
        mad.operation = "MULTIPLY_ADD"
        mad.inputs[1].default_value = tuple(2 * var * c for c in farb)
        mad.inputs[2].default_value = tuple(c * (1 - var) for c in farb)
        nt.links.new(rs.outputs["Fac"], mad.inputs[0])
        nt.links.new(mad.outputs["Vector"], pb.inputs["Base Color"])
    else:
        pb.inputs["Base Color"].default_value = (*farb, 1)
    pb.inputs["Roughness"].default_value = rau
    pb.inputs["Metallic"].default_value = metall
    if sheen > 0:
        pb.inputs["Sheen Weight"].default_value = sheen
        pb.inputs["Sheen Roughness"].default_value = 0.35
        pb.inputs["Sheen Tint"].default_value = (min(1, farb.x * 2 + 0.15), min(1, farb.y * 2 + 0.05), min(1, farb.z * 2 + 0.08), 1)
    if glanz > 0:
        pb.inputs["Coat Weight"].default_value = glanz
        pb.inputs["Coat Roughness"].default_value = 0.25
    if glut:
        pb.inputs["Emission Color"].default_value = (*glut[:3], 1)
        pb.inputs["Emission Strength"].default_value = glut[3]
    if bump > 0:
        vo = nt.nodes.new("ShaderNodeTexVoronoi")
        vo.feature = "DISTANCE_TO_EDGE"
        vo.inputs["Scale"].default_value = korn
        nt.links.new(tk.outputs["Object"], vo.inputs["Vector"])
        bn = nt.nodes.new("ShaderNodeBump")
        bn.inputs["Strength"].default_value = min(1.0, bump)
        bn.inputs["Distance"].default_value = 0.012
        nt.links.new(vo.outputs["Distance"], bn.inputs["Height"])
        nt.links.new(bn.outputs["Normal"], pb.inputs["Normal"])
    return m


def stoffe_fuer(stufe, warm):
    """Alle Stoffe einer Stufe. `warm` = Zustand „klick“: wärmerer, hellerer Ton und etwas mehr Glanz."""
    s = STUFEN[stufe - 1]
    f = Vector(s["leder"])
    g = Vector(GOLD)
    if warm:
        f = Vector((f.x * 1.16 + 0.02, f.y * 1.11 + 0.01, f.z * 1.0 + 0.002))
        g = Vector((min(1, g.x * 1.04), min(1, g.y * 1.14), min(1, g.z * 1.4)))
    glut = (1.0, 0.55, 0.15, 0.05) if warm else None
    glanz = (0.55 if warm else 0.0) + (0.35 if stufe in (3, 4) else 0.0)
    st = {}
    st["leder"] = stoff("Leder", f, s["rau"] - (0.15 if warm else 0), 0, s["bump"], 75, s["var"], sheen=1.0 if s.get("samt") else 0.0,
                        glanz=min(1.0, glanz), glut=glut)
    dk = f * 0.45
    st["dunkel"] = stoff("Falte", dk, 0.9)
    st["innen"] = stoff("Futter", (0.05, 0.035, 0.03), 1.0)
    heller = Vector((min(1, f.x * 1.45 + 0.08), min(1, f.y * 1.45 + 0.06), min(1, f.z * 1.4 + 0.04)))
    st["flicken"] = stoff("Flicken", heller if stufe == 1 else f, 1.0, 0, 1.0, 55, 0.2)
    st["garn"] = stoff("Garn", (0.80, 0.70, 0.50) if stufe < 4 else (0.62, 0.45, 0.16), 0.9, 0.0)
    st["gold"] = stoff("Gold", g, 0.30, 1.0, 0.0, glut=glut and (1.0, 0.7, 0.2, 0.06))
    st["rot"] = stoff("Rotband", (0.30, 0.04, 0.04), 0.75, 0, 0.3, 80)
    st["stein"] = stoff("Stein", (0.55, 0.02, 0.04), 0.12, 0.0, glut=(0.7, 0.0, 0.03, 0.5))
    st["pelz_hell"] = stoff("PelzHell", (0.95, 0.93, 0.88) if stufe == 6 else (0.82, 0.76, 0.62), 1.0, 0, 0, 0, 0.08 if stufe == 6 else 0.18)
    st["pelz_dunkel"] = stoff("PelzDunkel", (0.03, 0.025, 0.03) if stufe == 6 else (0.40, 0.31, 0.22), 1.0, 0, 0, 0, 0.18)
    st["eisen"] = stoff("Eisen", (0.34, 0.34, 0.37), 0.42, 0.9, 0.4, 30, 0.1)
    st["eisen_dunkel"] = stoff("EisenDunkel", (0.17, 0.17, 0.19), 0.50, 0.85, 0.5, 26, 0.12)
    st["messing"] = stoff("Messing", (0.80, 0.55, 0.14), 0.32, 1.0, 0.0)
    st["eisen_schmied"] = stoff("EisenSchmied", (0.075, 0.075, 0.085), 0.55, 0.85, 0.5, 30, 0.12)
    st["bronze"] = stoff("Bronze", (0.46, 0.26, 0.085), 0.42, 1.0, 0.45, 26, 0.14)
    st["bronze_dunkel"] = stoff("BronzeDunkel", (0.17, 0.09, 0.035), 0.60, 0.9, 0.5, 30, 0.12)
    st["bronze_hell"] = stoff("BronzeHell", (0.66, 0.40, 0.13), 0.34, 1.0, 0.0)
    st["loch"] = stoff("Loch", (0.004, 0.004, 0.005), 1.0, 0.0)
    return st


# ─────────────────────────────────────────────────────────────────────────────
#  Geometrie: je Stoff ein bmesh, am Ende je ein Objekt
# ─────────────────────────────────────────────────────────────────────────────

# Teile für die Fugenlinien (Objekt-Index): Handrücken/Stulpe = 1, je Finger eigene Nummer, alles andere (Nähte, Ringe, Zierrat) = 99
TEILE = {"zeige": 2, "mittel": 3, "ring": 4, "klein": 5, "daumen": 6, "platte": 1, "griff": 2, "rose": 3}
ZIERRAT = 99


def teil_id(name):
    if "@" in name:
        return TEILE.get(name.split("@")[1], ZIERRAT)
    return 1 if name == "leder" else ZIERRAT


class Bau:
    def __init__(self, stoffe):
        self.st = stoffe
        self.bm = {}
        self.tipp = None
        self.punkte = []                        # alle Eckpunkte für den Bildausschnitt

    def _b(self, name):
        if name not in self.bm:
            self.bm[name] = bmesh.new()
        return self.bm[name]

    def kegel(self, name, p0, p1, r0, r1, seg=16, kappen=True, plattung=1.0):
        d = Vector(p1) - Vector(p0)
        q = Vector((0, 0, 1)).rotation_difference(d)
        m = Matrix.Translation((Vector(p0) + Vector(p1)) / 2) @ q.to_matrix().to_4x4() @ Matrix.Diagonal((1, plattung, 1, 1))
        bmesh.ops.create_cone(self._b(name), cap_ends=kappen, cap_tris=False, segments=seg, radius1=r0, radius2=r1, depth=d.length, matrix=m)

    def kugel(self, name, p, r, skal=(1, 1, 1), dreh=None, seg=14):
        m = Matrix.Translation(p)
        if dreh is not None:
            m = m @ dreh.to_4x4()
        m = m @ Matrix.Diagonal((skal[0], skal[1], skal[2], 1))
        bmesh.ops.create_uvsphere(self._b(name), u_segments=seg, v_segments=max(6, seg // 2 + 1), radius=r, matrix=m)

    def quader(self, name, p, groesse, dreh=None):
        m = Matrix.Translation(p)
        if dreh is not None:
            m = m @ dreh.to_4x4()
        m = m @ Matrix.Diagonal((groesse[0], groesse[1], groesse[2], 1))
        bmesh.ops.create_cube(self._b(name), size=1.0, matrix=m)

    def torus(self, name, p, gross, klein, skal=(1, 1, 1), dreh=None, seg=40, ring=8):
        """Torus in der XY-Ebene (Z = Achse); `dreh` kippt ihn."""
        b = self._b(name)
        m = Matrix.Translation(p)
        if dreh is not None:
            m = m @ dreh.to_4x4()
        m = m @ Matrix.Diagonal((skal[0], skal[1], skal[2], 1))
        verts = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ring_v = []
            for j in range(ring):
                bb = 2 * math.pi * j / ring
                x = (gross + klein * math.cos(bb)) * math.cos(a)
                y = (gross + klein * math.cos(bb)) * math.sin(a)
                z = klein * math.sin(bb)
                ring_v.append(b.verts.new(m @ Vector((x, y, z))))
            verts.append(ring_v)
        for i in range(seg):
            for j in range(ring):
                try:
                    b.faces.new((verts[i][j], verts[(i + 1) % seg][j], verts[(i + 1) % seg][(j + 1) % ring], verts[i][(j + 1) % ring]))
                except ValueError:
                    pass

    def naht(self, name, pfad, abstand=0.06, groesse=(0.034, 0.011, 0.011), hub=0.0, rng=None):
        """Stiche (kleine Quader) entlang eines Linienzugs."""
        rest = 0.0
        for a, b in zip(pfad[:-1], pfad[1:]):
            a, b = Vector(a), Vector(b)
            d = b - a
            laenge = d.length
            if laenge < 1e-6:
                continue
            u = d / laenge
            q = Vector((1, 0, 0)).rotation_difference(u)
            t = rest
            while t < laenge:
                p = a + u * t
                if rng:
                    p = p + Vector((rng.uniform(-hub, hub), rng.uniform(-hub, hub), 0))
                self.quader(name, p, groesse, q.to_matrix())
                t += abstand
            rest = t - laenge

    def perlen(self, name, pfad, abstand, r):
        rest = 0.0
        for a, b in zip(pfad[:-1], pfad[1:]):
            a, b = Vector(a), Vector(b)
            d = b - a
            laenge = d.length
            if laenge < 1e-6:
                continue
            t = rest
            while t < laenge:
                self.kugel(name, a + d / laenge * t, r, seg=8)
                t += abstand
            rest = t - laenge

    def fertig(self, drehung):
        sz = bpy.context.scene
        rot = Matrix.Rotation(math.radians(drehung), 4, "Z")
        objs = []
        for name, bm in self.bm.items():
            stoff_name = name.split("@")[0]
            bm.transform(rot)
            for f in bm.faces:
                f.smooth = True
            me = bpy.data.meshes.new(name)
            bm.to_mesh(me)
            self.punkte.extend(v.co.copy() for v in bm.verts)
            bm.free()
            o = bpy.data.objects.new(name, me)
            o.data.materials.append(self.st[stoff_name])
            o.pass_index = teil_id(name)
            sz.collection.objects.link(o)
            objs.append(o)
        self.tipp = rot @ self.tipp if self.tipp is not None else None
        return objs


# ─────────────────────────────────────────────────────────────────────────────
#  Die Hand
# ─────────────────────────────────────────────────────────────────────────────

PALME = (0.345, 0.46, 0.15)       # Halbachsen, Mittelpunkt bei y = 0,5


def palmenhoehe(x, y):
    t = 1 - (x / PALME[0]) ** 2 - ((y - 0.5) / PALME[1]) ** 2
    return PALME[2] * math.sqrt(max(t, 0.0))


# Finger: (Wurzel, Längen der Glieder, Radius von/bis, Spreizung in Grad, Neigung je Glied normal, Neigung je Glied beim Greifen, Seitenknick je Glied)
# Mittelfinger am längsten, kleiner Finger deutlich kürzer; die drei hinteren Finger fächern gestaffelt auf und sind nur locker gekrümmt.
FINGER = {
    "zeige": ((-0.250, 0.74, 0.03), (0.60, 0.36, 0.27), (0.108, 0.058), -3, (2, -2, -4), (-28, -62, -48), (0, 0, 0)),
    "mittel": ((-0.083, 0.82, 0.02), (0.46, 0.27, 0.17), (0.080, 0.058), -1, (-2, -8, -10), (-32, -66, -50), (0, 0, 0)),
    "ring": ((0.083, 0.80, 0.03), (0.34, 0.22, 0.14), (0.077, 0.055), 1, (-3, -10, -12), (-30, -64, -48), (0, 0, 0)),
    "klein": ((0.245, 0.72, 0.03), (0.24, 0.16, 0.11), (0.068, 0.049), 3, (-4, -10, -12), (-28, -60, -46), (0, 0, 0)),
}
# Daumen in drei Gliedern: vom Handballen weg abgespreizt, dann Endglied leicht nach vorn und oben (Zeigerichtung, überstreckt), nicht unter die Hand
DAUMEN = ((-0.30, 0.24, -0.01), (0.30, 0.26, 0.20), (0.125, 0.085), -24, (0, 3, 6), (-8, -22, -22), (0, -3, -4))


def glieder(wurzel, laengen, radien, spreiz, neigung, knick=None):
    """[(p0, p1, r0, r1, Richtung)] für eine Kette aus Gliedern."""
    p = Vector(wurzel)
    neig = 0.0
    sp = spreiz
    out = []
    n = len(laengen)
    for i, (L, nk) in enumerate(zip(laengen, neigung)):
        neig += nk
        sp += knick[i] if knick else 0.0
        ya, pa = math.radians(sp), math.radians(neig)
        d = Vector((math.sin(ya) * math.cos(pa), math.cos(ya) * math.cos(pa), math.sin(pa)))
        r0 = radien[0] + (radien[1] - radien[0]) * i / n
        r1 = radien[0] + (radien[1] - radien[0]) * (i + 1) / n
        out.append((p, p + d * L, r0, r1, d))
        p = p + d * L
    return out


def catmull(pts, n=6):
    """Glatte Kurve durch die Gelenkpunkte; liefert (Punkt, Gelenknummer-Bruchteil)."""
    P = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(n):
            t = k / n
            q = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)
            out.append((q, (i - 1) + t))
    out.append((P[-2], len(P) - 3))
    return out


def sweep(b, name, kette, von=0, bis=None, skal=1.0, flach=0.80, spitze=True, wulst=0.10, seg=14):
    """Finger als gezogener, abgeflachter Querschnitt: schmaler zur Spitze, Knöchelwülste an den Gelenken,
    runde Kuppe. Gibt die Mittellinie als [(p, side, up, w, h)] zurück."""
    bis = len(kette) if bis is None else bis
    pts = [kette[von][0]] + [g[1] for g in kette[von:bis]]
    rad = [g[2] for g in kette[von:bis]] + [kette[bis - 1][3]]
    kurve = catmull(pts)
    z = Vector((0, 0, 1))
    rahmen = []
    for i, (p, u) in enumerate(kurve):
        q = kurve[min(i + 1, len(kurve) - 1)][0] - kurve[max(i - 1, 0)][0]
        d = q.normalized()
        side = d.cross(z)
        side = side.normalized() if side.length > 1e-4 else Vector((1, 0, 0))
        up = side.cross(d)
        k = int(u)
        r = rad[min(k, len(rad) - 1)] if k >= len(rad) - 1 else rad[k] + (rad[k + 1] - rad[k]) * (u - k)
        # Knöchelwulst an den Gelenken (nicht am Ansatz)
        for j in range(1, len(rad) - 1):
            r *= 1 + wulst * math.exp(-((u - j) / 0.22) ** 2)
        # leicht eingezogen zwischen den Gelenken
        r *= 1 - 0.03 * math.sin(math.pi * ((u % 1.0)))
        rahmen.append((p, side, up, r * skal, r * skal * flach, d))
    ringe = [list(x) for x in rahmen]
    if spitze:
        p, side, up, w, h, d = rahmen[-1]
        for j in range(1, 5):
            a = j / 4 * math.pi / 2
            f = max(math.cos(a), 0.12)
            ringe.append([p + d * (w * 0.95 * math.sin(a)), side, up, w * f, h * f, d])
    B = b._b(name)
    gr = []
    for (p, side, up, w, h, d) in ringe:
        gr.append([B.verts.new(p + side * math.cos(2 * math.pi * j / seg) * w + up * math.sin(2 * math.pi * j / seg) * h) for j in range(seg)])
    for i in range(len(gr) - 1):
        for j in range(seg):
            try:
                B.faces.new((gr[i][j], gr[i + 1][j], gr[i + 1][(j + 1) % seg], gr[i][(j + 1) % seg]))
            except ValueError:
                pass
    for ring_ in (gr[0][::-1], gr[-1]):
        try:
            B.faces.new(ring_)
        except ValueError:
            pass
    return [(r[0], r[1], r[2], r[3], r[4]) for r in rahmen]


def stulpe_r(ly, r_a, r_e, y):
    """Halbachse der Stulpe in der Tiefe y: am Handgelenk eng, nach hinten weich ausgestellt (kein Trichter)."""
    t = min(max((0.08 - y) / (ly + 0.08), 0.0), 1.0)
    return r_a + (r_e - r_a) * (0.60 * t * (2 - t) + 0.40 * t ** 2.2)


def stulpe_bauen(b, ly, r_a, r_e):
    """Weicher Lederkragen: Ringe mit leicht welligem Rand, aufgerollte Lippe, Futter am Ende."""
    B = b._b("leder")
    ys = [0.08 - (ly + 0.08) * k / 14 for k in range(15)]
    seg = 40
    gr = []
    for k, y in enumerate(ys):
        t = k / 14
        gr.append([])
        for j in range(seg):
            w = 2 * math.pi * j / seg
            wel = 1 + 0.06 * t * math.sin(5 * w + 0.6) + 0.025 * t * math.sin(9 * w + 1.3 * t)
            r = stulpe_r(ly, r_a, r_e, y) * wel
            gr[-1].append(B.verts.new(Vector((r * math.cos(w), y, r * math.sin(w) * 0.70))))
    for k in range(len(gr) - 1):
        for j in range(seg):
            try:
                B.faces.new((gr[k][j], gr[k + 1][j], gr[k + 1][(j + 1) % seg], gr[k][(j + 1) % seg]))
            except ValueError:
                pass
    stulpenband(b, "leder", ly, r_a, r_e, -ly + 0.012, dicke=0.05)       # aufgerollte Lippe
    b.kegel("innen", (0, -ly + 0.015, 0), (0, -ly + 0.03, 0), stulpe_r(ly, r_a, r_e, -ly) - 0.03, stulpe_r(ly, r_a, r_e, -ly) - 0.03, seg=32)


def hand_bauen(stufe, griff, st):
    b = Bau(st)
    rng = random.Random(1480 + stufe)
    s = STUFEN[stufe - 1]
    # Palme (Handrücken leicht gewölbt) und Daumenballen
    b.kugel("leder", (0, 0.5, 0), 1.0, (PALME[0], PALME[1], PALME[2]), seg=24)
    b.kugel("leder", (-0.24, 0.27, 0.02), 1.0, (0.18, 0.26, 0.11), Matrix.Rotation(math.radians(-25), 3, "Z"), seg=16)
    ly = s["stulpe"]
    r_a, r_e = 0.27, 0.33
    stulpe_bauen(b, ly, r_a, r_e)
    # Finger
    fin = {}
    for name, (w, la, ra, sp, nn, ng, kn) in FINGER.items():
        fin[name] = glieder(w, la, ra, sp, ng if griff else nn, kn)
        b.kugel("leder", Vector(w) + Vector((0, 0.0, 0.03)), ra[0] * 1.02, (1.0, 0.85, 0.8), seg=12)       # Knöchel am Ansatz
    dau = glieder(DAUMEN[0], DAUMEN[1], DAUMEN[2], DAUMEN[3] + (25 if griff else 0), DAUMEN[5] if griff else DAUMEN[4], DAUMEN[6])
    alle = list(fin.values()) + [dau]
    mitte = {}
    namen = {id(k): n for n, k in fin.items()}
    namen[id(dau)] = "daumen"
    for kette in alle:
        flach = 0.9 if kette is dau else 0.80
        mitte[id(kette)] = sweep(b, "leder@" + namen[id(kette)], kette, flach=flach)
    z_ = fin["zeige"][-1]
    b.tipp = z_[1] + z_[4] * z_[3] * 0.9
    # Falten an den Knöcheln: je Gelenk zwei dünne dunkle Wülste quer über dem Rücken
    for kette in alle:
        for i, (p0, p1, r0, r1, d) in enumerate(kette[:-1]):
            q = Vector((0, 1, 0)).rotation_difference(d)
            for dd in (-0.03, 0.0, 0.03):
                c = p1 + d * dd + Vector((0, 0, r1 * 0.80 * 0.95 * 1.08))
                b.kugel("dunkel", c, r1 * 0.85, (1.0, 0.07, 0.10), q.to_matrix(), seg=10)
    # Nähte an den Fingerseiten
    for kette in alle:
        m = mitte[id(kette)]
        for sg in (-1, 1):
            pf = [p + side * (w * 0.97 * sg) + up * (h * 0.0) for (p, side, up, w, h) in m[2:-1]]
            b.naht("garn", pf, 0.05, (0.026, 0.009, 0.009))
    # Rückennähte: drei Rippen vom Handgelenk zu den Fingerwurzeln
    rippen = []
    for x0, x1 in ((-0.20, -0.15), (0.0, 0.0), (0.2, 0.17)):
        pf = []
        for k in range(9):
            t = k / 8
            y = 0.1 + t * 0.66
            x = x0 + (x1 - x0) * t
            pf.append(Vector((x, y, palmenhoehe(x, y) + 0.004)))
        rippen.append(pf)
    if stufe != 2:
        stulpen_stiche(b, "garn", ly, r_a, r_e, -ly + 0.075, 0.07, (0.04, 0.012, 0.012))      # Saumnaht (Stufe 2 hat eigene)
    stufen_deko(b, stufe, griff, rng, rippen, fin, dau, ly, r_a, r_e)
    return b


def stulpenpunkt(ly, r_a, r_e, y, w):
    """Punkt auf der Stulpe bei der Tiefe y (negativ = nach hinten), Winkel w; obere Hälfte w in 0..pi."""
    r = stulpe_r(ly, r_a, r_e, y)
    return Vector((r * math.cos(w), y, r * math.sin(w) * 0.70 + (0.0)))


def stulpenband(b, name, ly, r_a, r_e, y, hoch=True, dicke=0.022, seg=44, dz=0.0):
    r = stulpe_r(ly, r_a, r_e, y)
    b.torus(name, (0, y, dz), r + dicke * 0.3, dicke, skal=(1, 0.70, 1), dreh=Matrix.Rotation(math.pi / 2, 3, "X"), seg=seg, ring=7)


def stulpen_stiche(b, name, ly, r_a, r_e, y, abstand, groesse, rng=None, hub=0.0):
    r = stulpe_r(ly, r_a, r_e, y)
    pfad = [Vector((r * math.cos(w), y, r * math.sin(w) * 0.70 + 0.012)) for w in np.linspace(0.0, math.pi, 26)]
    b.naht(name, pfad, abstand, groesse, hub, rng)


def ring(b, p, d, r, name="gold", breite=0.05):
    """Ring um einen Finger an Punkt p, Richtung d, Fingerradius r."""
    q = Vector((0, 0, 1)).rotation_difference(d)
    b.torus(name, p, r * 1.02, breite * 0.5, skal=(1, 1, 1), dreh=q.to_matrix(), seg=26, ring=7)


def stufen_deko(b, stufe, griff, rng, rippen, fin, dau, ly, r_a, r_e):
    G = "garn"
    ring_gl = fin["ring"][0]
    if stufe == 1:
        # Flicken mit krummen Stichen auf dem Handrücken, Wulst am Stulpenrand, grobe Rippennähte
        c = Vector((0.12, 0.52, 0))
        c.z = palmenhoehe(c.x, c.y) + 0.0
        dreh = Matrix.Rotation(math.radians(-14), 3, "Z")
        b.quader("flicken", c + Vector((0, 0, 0.0)), (0.27, 0.23, 0.04), dreh)
        ecken = [Vector((-0.135, -0.115, 0)), Vector((0.135, -0.115, 0)), Vector((0.135, 0.115, 0)), Vector((-0.135, 0.115, 0)),
                 Vector((-0.135, -0.115, 0))]
        pf = [c + dreh @ e + Vector((0, 0, 0.022)) for e in ecken]
        b.naht(G, pf, 0.05, (0.03, 0.014, 0.014), 0.006, rng)
        for r_ in rippen[:1]:
            b.naht(G, r_[:4], 0.07, (0.03, 0.013, 0.013), 0.008, rng)
        stulpenband(b, "dunkel", ly, r_a, r_e, -ly + 0.03, dicke=0.032)
        # kleiner Flicken am Zeigefingerglied
        p0, p1, r0, r1, d = fin["zeige"][0]
        b.quader("flicken", (p0 + p1) / 2 + Vector((0, 0, r0 * 0.9)), (0.11, 0.12, 0.03), Matrix.Rotation(math.radians(-15 - 8), 3, "Z"))
    elif stufe == 2:
        for r_ in rippen:
            b.naht(G, r_, 0.065, (0.036, 0.011, 0.011))
        stulpen_stiche(b, G, ly, r_a, r_e, -ly + 0.06, 0.07, (0.04, 0.012, 0.012))
        stulpenband(b, "dunkel", ly, r_a, r_e, -ly + 0.025, dicke=0.018)
    elif stufe == 3:
        for r_ in rippen:
            b.naht(G, r_, 0.06, (0.034, 0.011, 0.011))
        stulpenband(b, "rot", ly, r_a, r_e, -ly * 0.42, dicke=0.05)
        stulpenband(b, "gold", ly, r_a, r_e, -ly * 0.42 + 0.06, dicke=0.011)
        stulpenband(b, "gold", ly, r_a, r_e, -ly * 0.42 - 0.06, dicke=0.011)
        stulpenband(b, "dunkel", ly, r_a, r_e, -ly + 0.02, dicke=0.022)
        # Stickerei: Punkte im Band
        for w in np.linspace(0.12, math.pi - 0.12, 13):
            b.kugel("garn", stulpenpunkt(ly, r_a, r_e, -ly * 0.42, w) + Vector((0, 0, 0.044)), 0.016, seg=8)
    elif stufe == 4:
        for r_ in rippen:
            b.naht(G, r_, 0.06, (0.034, 0.011, 0.011))
        stulpenband(b, "gold", ly, r_a, r_e, -ly + 0.025, dicke=0.034)
        stulpenband(b, "gold", ly, r_a, r_e, -ly + 0.12, dicke=0.011)
        # schlichter Ring am Ringfinger
        p0, p1, r0, r1, d = ring_gl
        ring(b, p0 + d * 0.15, d, r0 * 0.97, "gold", 0.055)
    else:
        # 5 und 6: Samt mit Goldstickerei
        for r_ in rippen:
            b.perlen("gold", r_, 0.05, 0.012)
        stulpenband(b, "gold", ly, r_a, r_e, -ly + 0.025, dicke=0.034)
        stulpenband(b, "gold", ly, r_a, r_e, -ly + 0.12, dicke=0.012)
        stulpenband(b, "gold", ly, r_a, r_e, -ly + 0.16, dicke=0.008)
        for w in np.linspace(0.15, math.pi - 0.15, 12):
            b.kugel("gold", stulpenpunkt(ly, r_a, r_e, -ly + 0.2, w) + Vector((0, 0, 0.02)), 0.017, seg=8)
        # Zierbogen auf dem Handrücken
        for sgn in (-1, 1):
            pf = [Vector((sgn * (0.06 + 0.19 * t), 0.46 + 0.35 * t * t + 0.0, 0)) for t in np.linspace(0, 1, 9)]
            for v in pf:
                v.z = palmenhoehe(v.x, v.y) + 0.006
            b.perlen("gold", pf, 0.04, 0.011)
        # Goldlinie über den Fingern
        for kette in (fin["zeige"], fin["mittel"], fin["ring"], fin["klein"]):
            pf = [p + Vector((0, 0, rr * 0.80 * 1.0)) for (p, q_, rr, r1, d) in kette] + [kette[-1][1] + Vector((0, 0, kette[-1][3] * 0.72))]
            b.perlen("gold", pf, 0.045, 0.009)
        # Siegelring am Ringfinger
        p0, p1, r0, r1, d = ring_gl
        ring(b, p0 + d * 0.15, d, r0 * 0.97, "gold", 0.06)
        q = Vector((0, 1, 0)).rotation_difference(d).to_matrix()
        b.quader("gold", p0 + d * 0.15 + Vector((0, 0, r0 * 1.0)), (0.10, 0.12, 0.04), q)
        b.kugel("stein", p0 + d * 0.15 + Vector((0, 0, r0 * 1.0 + 0.022)), 0.034, (1, 1.1, 0.7), q, seg=10)
        if stufe == 6:
            # Hermelinpelz an der Stulpe: dick, hell, mit dunklen Schwänzchen; steht über den Rand
            y0 = -ly - 0.02
            rr = stulpe_r(ly, r_a, r_e, y0)
            for lage, (dy, n, rad) in enumerate(((0.0, 56, 0.095), (0.12, 50, 0.09))):
                for k in range(n):
                    w = 2 * math.pi * (k + 0.5 * lage) / n + rng.uniform(-0.04, 0.04)
                    r2 = rr + 0.03 + 0.01 * lage
                    p = Vector((r2 * math.cos(w), y0 + dy + rng.uniform(-0.03, 0.03), r2 * math.sin(w) * 0.70))
                    nm = "pelz_dunkel" if (k % 9 == 4 and lage == 1) else "pelz_hell"
                    b.kugel(nm, p, rad * rng.uniform(0.9, 1.1), (1.0, 1.25, 1.0), seg=9)
            # Gold: Ringe an jedem Fingerglied, Goldkappen auf den Fingerspitzen, Steinring am kleinen Finger
            for kette in (fin["zeige"], fin["mittel"], fin["ring"], fin["klein"]):
                for gl in (kette[1:2] if kette is fin["zeige"] else kette[:2]):
                    p0, p1, r0, r1, d = gl
                    ring(b, p0 + d * (p1 - p0).length * 0.5, d, r1 * 0.93, "gold", 0.07)
                sweep(b, "gold", kette, von=len(kette) - 1, skal=1.07, wulst=0.0)
            p0, p1, r0, r1, d = fin["klein"][0]
            b.kugel("stein", p0 + d * 0.11 + Vector((0, 0, r0 * 1.1)), 0.04, (1, 1, 0.8), seg=10)
            p0, p1, r0, r1, d = dau[0]
            ring(b, p0 + d * 0.18, d, r0 * 1.04, "gold", 0.07)
            stulpenband(b, "gold", ly, r_a, r_e, -ly + 0.24, dicke=0.01)


# ─────────────────────────────────────────────────────────────────────────────
#  Türring (Alternative zur greifenden Hand)
# ─────────────────────────────────────────────────────────────────────────────

def ring_bauen(st):
    b = Bau(st)
    # Platte klein und oben, damit das Ringloch frei bleibt (auch bei 32 px kein Fleck)
    b.kegel("eisen", (0, 0.50, -0.02), (0, 0.50, 0.05), 0.22, 0.20, seg=28)                # Befestigungsplatte
    b.torus("eisen", (0, 0.50, 0.05), 0.18, 0.02, seg=28, ring=6)                          # Plattenrand
    for k in range(3):
        w = math.pi / 2 + k * 2 * math.pi / 3
        b.kugel("eisen", (0.12 * math.cos(w), 0.50 + 0.12 * math.sin(w), 0.075), 0.035, seg=8)
    b.kugel("eisen", (0, 0.42, 0.12), 0.09, seg=12)                                        # Zapfen, an dem der Ring hängt
    b.torus("eisen", (0, 0.02, 0.21), 0.40, 0.07, dreh=Matrix.Rotation(math.radians(-8), 3, "X"), seg=48, ring=10)
    b.tipp = Vector((0, 0.02, 0.21))
    return b


# ─────────────────────────────────────────────────────────────────────────────
#  Türschloss mit Klinke (zweite Alternative für „Haus betreten“)
# ─────────────────────────────────────────────────────────────────────────────

def prisma(b, name, pts, z0, z1):
    """Gerade Platte über einem Umriss (Punkte gegen den Uhrzeigersinn)."""
    B = b._b(name)
    u = [B.verts.new(Vector((x, y, z0))) for x, y in pts]
    o = [B.verts.new(Vector((x, y, z1))) for x, y in pts]
    n = len(pts)
    B.faces.new(o)
    B.faces.new(u[::-1])
    for i in range(n):
        B.faces.new((u[i], u[(i + 1) % n], o[(i + 1) % n], o[i]))


def spitzbogen_umriss(halb=0.31, unten=0.04, schulter=0.74, spitze=1.04, fase=0.07, n=9):
    """Umriss einer Beschlagplatte: unten abgefast, oben als Spitzbogen."""
    pts = [(-halb + fase, unten), (halb - fase, unten), (halb, unten + fase), (halb, schulter)]
    for k in range(1, n):
        t = k / n
        pts.append((halb * (1 - t) ** 1.0, schulter + (spitze - schulter) * (1 - (1 - t) ** 2.3)))
    pts.append((0.0, spitze))
    for k in range(n - 1, 0, -1):
        t = k / n
        pts.append((-halb * (1 - t) ** 1.0, schulter + (spitze - schulter) * (1 - (1 - t) ** 2.3)))
    pts += [(-halb, schulter), (-halb, unten + fase)]
    return pts


def lilie(b, rolle, x, y, nach_oben, flaeche):
    """Lilienende (flach auf der Platte): mittleres Blatt, zwei nach außen gerollte Seitenblätter, Band am Fuß.
    nach_oben = +1 am oberen, -1 am unteren Ende; (x, y) = Ansatz der Lilie."""
    s = nach_oben
    z = 0.085
    b.kugel(rolle, (x, y + s * 0.11, z), 1.0, (0.062, 0.135, 0.040), seg=12)
    for sg in (-1, 1):
        dreh = Matrix.Rotation(math.radians(-sg * s * 38), 3, "Z")
        b.kugel(rolle, (x + sg * 0.085, y + s * 0.085, z - 0.005), 1.0, (0.045, 0.105, 0.036), dreh, seg=12)
        b.kugel(rolle, (x + sg * 0.135, y + s * 0.15, z - 0.01), 0.034, (1, 1, 0.9), seg=10)        # eingerolltes Ende
    b.quader(rolle, (x, y, 0.09), (0.25, 0.034, 0.05))                                              # Band
    b.kugel(rolle, (x, y + s * 0.025, 0.12), 0.026, seg=8)


def schloss_bauen(st, eisen=False):
    """Türschild mit Schlüsselloch und geschmiedeter Klinke, hoch und schmal. Bronze (Platte dunkel, Fläche gehämmert,
    Rose und Nieten heller) oder dunkles Eisen mit Messing. Hotspot = Mitte des Schlüssellochs (Bartschlüssel-Form)."""
    b = Bau(st)
    grund, flaeche, griff, zier = ("eisen_schmied", "eisen_dunkel", "eisen_dunkel", "messing") if eisen else ("bronze_dunkel", "bronze", "bronze", "bronze_hell")
    hb, y0, y1 = 0.15, 0.10, 1.26                    # halbe Schaftbreite, Unter- und Oberkante
    # Umriss: schmaler Schaft, an beiden Enden ausgestellt wie ein Lilienfuß
    umriss = [(-hb, y0 + 0.17), (-0.20, y0 + 0.08), (-0.13, y0 + 0.01), (0.13, y0 + 0.01), (0.20, y0 + 0.08), (hb, y0 + 0.17),
              (hb, y1 - 0.17), (0.20, y1 - 0.08), (0.13, y1 - 0.01), (-0.13, y1 - 0.01), (-0.20, y1 - 0.08), (-hb, y1 - 0.17)]
    prisma(b, grund + "@platte", umriss, -0.02, 0.07)
    # aufgesetzte Fläche (gehämmert), darum bleibt ein dunkler Rand
    prisma(b, flaeche + "@platte", [(-0.092, 0.34), (0.092, 0.34), (0.092, 1.02), (-0.092, 1.02)], 0.07, 0.095)
    lilie(b, zier + "@platte", 0.0, y1 - 0.10, 1, flaeche)
    lilie(b, zier + "@platte", 0.0, y0 + 0.10, -1, flaeche)
    # Nieten
    for x, y in ((-0.125, 0.30), (0.125, 0.30), (-0.125, 0.78), (0.125, 0.78), (-0.125, 1.07), (0.125, 1.07)):
        b.kugel(zier + "@platte", (x, y, 0.085), 0.026, (1, 1, 0.8), seg=8)
    # Schlüsselloch: Bartschlüssel-Form (Kreis oben, Schlitz unten) in einer Messing-/Bronzeumrandung
    kx, ky = 0.0, 0.62
    b.kegel(zier + "@platte", (kx, ky, 0.093), (kx, ky, 0.112), 0.105, 0.095, seg=24)
    b.kegel("loch", (kx, ky + 0.012, 0.110), (kx, ky + 0.015, 0.128), 0.064, 0.064, seg=20)
    b.quader("loch", (kx, ky - 0.075, 0.119), (0.05, 0.15, 0.022))
    b.tipp = Vector((kx, ky, 0.125))
    # Klinke: Rose am oberen Ende, kräftig geschmiedeter Arm waagerecht zur Seite, fast gerade, Ende nur leicht geneigt, kleiner Endknauf
    rx, ry = 0.0, 0.915
    b.kegel(zier + "@rose", (rx, ry, 0.095), (rx, ry, 0.165), 0.125, 0.100, seg=24)
    b.kugel(griff + "@rose", (rx, ry, 0.175), 0.075, (1, 1, 0.75), seg=12)
    # S-förmig: von der Rose waagerecht, erst sanft nach oben, dann nach unten, am Ende eingerollt mit kleinem Knauf
    # Länge auf zwei Drittel gekürzt (x mal K), Höhenschwung etwas weniger, damit die S-Form bleibt
    K, KY = 2 / 3, 0.85
    roh = ((0.02, 0.0, 0.21), (0.22, 0.065, 0.235), (0.44, 0.080, 0.255), (0.62, -0.020, 0.27),
           (0.78, -0.125, 0.28), (0.92, -0.150, 0.285), (1.02, -0.105, 0.285))
    stuetz = [Vector((x * K if x > 0.05 else x, ry + dy * KY, z)) for x, dy, z in roh]
    n = len(stuetz) - 1
    radien = [0.088 - 0.022 * i / n for i in range(n + 1)]
    kette = [(stuetz[i], stuetz[i + 1], radien[i], radien[i + 1], (stuetz[i + 1] - stuetz[i]).normalized()) for i in range(n)]
    sweep(b, griff + "@griff", kette, flach=0.95, spitze=False, wulst=0.0, seg=12)
    kurve = [q for q, _ in catmull(stuetz, 5)]
    p = kurve[-1]
    b.kugel(zier + "@griff", p, 0.066, (1.0, 1.0, 0.95), seg=14)                                    # Endknauf
    for t in (4, 17):                                                                                # Schmiedebünde am Arm
        i = min(t, n - 2)
        d = (kurve[i + 1] - kurve[i]).normalized()
        b.torus(zier + "@griff", kurve[i], 0.080, 0.014, dreh=d.to_track_quat("Z", "Y").to_matrix().to_4x4(), seg=16, ring=6)
    return b


# ─────────────────────────────────────────────────────────────────────────────
#  Rendern und Nachbearbeitung
# ─────────────────────────────────────────────────────────────────────────────

def ausschnitt_aus(punkte, rand_px=3, ziel=16):
    th = math.radians(NEIGUNG)
    us = [p.x for p in punkte]
    vs = [p.y * math.cos(th) + p.z * math.sin(th) for p in punkte]
    return (min(us), max(us), min(vs), max(vs))


def kamera_setzen(rahmen, rand_px=3, ziel=16):
    cam = bpy.context.scene.camera
    th = math.radians(NEIGUNG)
    u0, u1, v0, v1 = rahmen
    ext = max(u1 - u0, v1 - v0)
    cam.data.ortho_scale = ext * ziel / (ziel - 2 * rand_px)
    uc, vc = (u0 + u1) / 2, (v0 + v1) / 2
    c = Vector((uc, vc * math.cos(th), vc * math.sin(th)))
    f = Vector((0, math.sin(th), -math.cos(th)))
    cam.location = c - f * 20
    cam.data.clip_end = 100
    bpy.context.scene.frame_current = 1


def hotspot_norm(punkt):
    th = math.radians(NEIGUNG)
    cam = bpy.context.scene.camera
    ext = cam.data.ortho_scale
    cl = cam.location
    f = Vector((0, math.sin(th), -math.cos(th)))
    up = Vector((0, math.cos(th), math.sin(th)))
    rel = punkt - cl
    u = rel.x / ext + 0.5
    v = rel.dot(up) / ext + 0.5
    return u, 1 - v


def lesen(pfad):
    img = bpy.data.images.load(pfad)
    img.colorspace_settings.name = "Non-Color"
    img.alpha_mode = "STRAIGHT"
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]      # oben zuerst
    bpy.data.images.remove(img)
    return a


def png_schreiben(pfad, a):
    """a: H×W×4 uint8."""
    h, w, _ = a.shape
    roh = b"".join(b"\x00" + a[y].tobytes() for y in range(h))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(roh, 9)) + chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(pfad), exist_ok=True)
    open(pfad, "wb").write(data)


def mitteln(a, n):
    """Blockmittel n×n (Alpha-gewichtet)."""
    h, w, _ = a.shape
    al = a[..., 3]
    pm = a[..., :3] * al[..., None]
    pm = pm.reshape(h // n, n, w // n, n, 3).mean(axis=(1, 3))
    al = al.reshape(h // n, n, w // n, n).mean(axis=(1, 3))
    rgb = np.where(al[..., None] > 1e-4, pm / np.maximum(al[..., None], 1e-4), 0)
    return np.concatenate([np.clip(rgb, 0, 1), al[..., None]], axis=-1)


def kontur(a):
    """Alpha hart (>= 0,5), 1 Pixel dunkle Kontur außen."""
    m = a[..., 3] >= 0.5
    d = m.copy()
    d[1:] |= m[:-1]
    d[:-1] |= m[1:]
    d[:, 1:] |= m[:, :-1]
    d[:, :-1] |= m[:, 1:]
    out = np.zeros(a.shape, dtype=np.float32)
    out[m, :3] = a[m, :3]
    out[m, 3] = 1
    rand = d & ~m
    out[rand, :3] = KONTUR
    out[rand, 3] = 1
    h = d.copy()
    h[1:] |= d[:-1]
    h[:-1] |= d[1:]
    h[:, 1:] |= d[:, :-1]
    h[:, :-1] |= d[:, 1:]
    halo = h & ~d
    out[halo, :3] = (0.96, 0.92, 0.82)
    out[halo, 3] = 0.55
    return out


def srgb8(a):
    return (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)


def aufnehmen(tmp):
    sz = bpy.context.scene
    sz.render.filepath = tmp
    bpy.ops.render.render(write_still=True)
    return lesen(tmp)


def szene_aufbauen(art, stufe, warm):
    """Neue Szene mit Hand (art 'normal' / 'griff') oder Ring; liefert (Bau, Objekte)."""
    neue_szene()
    st = stoffe_fuer(stufe, warm)
    if art == "ring":
        b = ring_bauen(st)
    elif art in ("schloss", "schloss_eisen"):
        b = schloss_bauen(st, art == "schloss_eisen")
    else:
        b = hand_bauen(stufe, art == "griff", st)
    objs = b.fertig(DREHUNG if art not in ("ring", "schloss", "schloss_eisen") else 0.0)
    return b, objs


def id_aufnehmen(tmp):
    """Zweite Aufnahme: jeder Teil (Finger, Handrücken, Beschlagteil) als eigene Zahl, ohne Kantenglättung."""
    sz = bpy.context.scene
    m = bpy.data.materials.new("IdFarbe")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    oi = nt.nodes.new("ShaderNodeObjectInfo")
    teil = nt.nodes.new("ShaderNodeMath")
    teil.operation = "DIVIDE"
    teil.inputs[1].default_value = 100.0
    nt.links.new(oi.outputs["Object Index"], teil.inputs[0])
    kom = nt.nodes.new("ShaderNodeCombineXYZ")
    for k in ("X", "Y", "Z"):
        nt.links.new(teil.outputs["Value"], kom.inputs[k])
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(kom.outputs["Vector"], em.inputs["Color"])
    ausg = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(em.outputs["Emission"], ausg.inputs["Surface"])
    vl = bpy.context.view_layer
    vl.material_override = m
    zier = [o for o in sz.objects if o.pass_index == ZIERRAT]
    for o in zier:
        o.hide_render = True                    # Nähte, Ringe, Pelz usw. zählen nicht als eigener Teil
    alt = (sz.cycles.samples, sz.cycles.filter_width)
    sz.cycles.samples = 1
    sz.cycles.filter_width = 0.01
    a = aufnehmen(tmp)
    sz.cycles.samples, sz.cycles.filter_width = alt
    vl.material_override = None
    for o in zier:
        o.hide_render = False
    v = a[..., 0]
    lin = np.where(v <= 0.04045, v / 12.92, ((v + 0.055) / 1.055) ** 2.4)
    ids = np.rint(lin * 100).astype(np.int32)
    ids[a[..., 3] < 0.5] = 0
    return ids


def verschieben(m, dy, dx):
    """Bild um (dy, dx) verschieben, Rand mit 0 (bzw. False) füllen."""
    out = np.zeros_like(m)
    H, W = m.shape[:2]
    ys, yd = (slice(0, H - dy), slice(dy, H)) if dy >= 0 else (slice(-dy, H), slice(0, H + dy))
    xs, xd = (slice(0, W - dx), slice(dx, W)) if dx >= 0 else (slice(-dx, W), slice(0, W + dx))
    out[yd, xd] = m[ys, xs]
    return out


def aufweiten(m, r):
    """Maske um r Pixel verbreitern (Quadrat)."""
    out = m.copy()
    for k in range(1, r + 1):
        for dy, dx in ((k, 0), (-k, 0), (0, k), (0, -k)):
            out |= verschieben(m, dy, dx)
    # Rest in Diagonalen, damit die Linie auch schräg gleichmäßig dick wird
    if r >= 2:
        h = out.copy()
        for dy, dx in ((1, 1), (-1, -1), (1, -1), (-1, 1)):
            out |= verschieben(h, dy, dx)
    return out


def fugen(gross, ids, ziel, hand=True):
    """Dunkle Fuge zwischen den einzelnen Fingern (und Handrücken) samt Randlicht an der beleuchteten Seite (oben links).
    `ziel` = Breite des Zielbildes in Pixeln; die Linie ist etwa ein drittel Zielpixel breit."""
    f = ids
    rand = np.zeros(f.shape, dtype=bool)        # Finger gegen Finger
    rand_hr = np.zeros(f.shape, dtype=bool)     # Finger gegen Handrücken: dünner
    for dy, dx in ((0, 1), (1, 0)):
        nb = verschieben(f, -dy, -dx)
        diff = (f > 0) & (nb > 0) & (f != nb)
        hr = diff & ((f == 1) | (nb == 1))
        for m, ziel_m in ((diff & ~hr, rand), (hr, rand_hr)):
            ziel_m |= m
            ziel_m |= verschieben(m, dy, dx)
    k = SUPER / ziel
    r = max(1, int(round(k * 0.42)))
    # Hand: keine Linie zwischen Finger und Handrücken (Knöchel), nur zwischen den Fingern; Beschläge behalten ihre Fugen
    linie = aufweiten(rand, max(1, int(r * 0.6)))
    if not hand:
        linie |= aufweiten(rand_hr, 1)
    knoechel = aufweiten(rand_hr, 1) & ~linie if hand else None      # Runde 12: Knöchel nur angedeutet
    licht = verschieben(linie, r + 1, r + 1) & ~linie & (f > 0)
    out = gross.copy()
    al = out[..., 3] > 0.5
    weich = np.array(FUGE, dtype=np.float32)
    deck = FUGE_DECK * (0.6 if hand else 1.0)       # an der Hand dezenter
    out[linie & al, :3] = out[linie & al, :3] * (1 - deck) + weich * deck
    l2 = licht & al
    out[l2, :3] = np.clip(out[l2, :3] * 1.32 + 0.05, 0, 1)
    if hand:
        kn = knoechel & al
        out[kn, :3] = out[kn, :3] * (1 - KNOECHEL_DECK) + weich * KNOECHEL_DECK
    return out


def fugen_klein(a, ids, n, hand=True):
    """Im Zielbild selbst: Pixel, die gegen einen Nachbarn oben oder links einen anderen Teil berühren, werden abgedunkelt
    (Schattenseite unten rechts). So bleibt jede Fingerfuge auch bei zwei Pixeln Fingerbreite als Linie sichtbar."""
    low = ids[n // 2::n, n // 2::n]
    h, w = a.shape[:2]
    low = low[:h, :w]
    m = np.zeros(low.shape, dtype=bool)
    kn = np.zeros(low.shape, dtype=bool)
    for dy, dx in ((0, 1), (1, 0)):
        nb = verschieben(low, dy, dx)
        d = (low > 0) & (nb > 0) & (low != nb)
        if hand:
            kn |= d & ((low == 1) | (nb == 1))       # Finger gegen Handrücken: nur leicht abdunkeln
            d &= (low != 1) & (nb != 1)
        m |= d
    out = a.copy()
    sel = m & (out[..., 3] > 0.5)
    out[sel, :3] *= 0.82 if hand else 0.74
    if hand:
        out[kn & ~m & (out[..., 3] > 0.5), :3] *= 0.87
    return out


def alles(aus, stufen, mit_uebersicht=True):
    tmp = os.path.join(aus, "_tmp.png")
    os.makedirs(aus, exist_ok=True)
    # gemeinsamer Bildausschnitt für alle Hand-Bilder (größte Hand = Stufe 6)
    b, _ = szene_aufbauen("normal", 6, False)
    rahmen_hand = ausschnitt_aus(b.punkte)
    b, _ = szene_aufbauen("ring", 1, False)
    rahmen_ring = ausschnitt_aus(b.punkte)
    b, _ = szene_aufbauen("schloss", 1, False)
    rahmen_schloss = ausschnitt_aus(b.punkte)
    ergebnis = {}
    hot = {}
    for stufe in stufen:
        s = STUFEN[stufe - 1]
        for zustand in ZUSTAENDE + ZUSAETZE:
            if zustand in ZUSAETZE and stufe != stufen[0]:
                continue
            art = {"normal": "normal", "klick": "normal", "tuer_schloss": "schloss", "tuer_schloss_eisen": "schloss_eisen", "tuer_ring": "ring"}[zustand]
            b, _ = szene_aufbauen(art, stufe, zustand == "klick")
            kamera_setzen({"ring": rahmen_ring, "schloss": rahmen_schloss, "schloss_eisen": rahmen_schloss}.get(art, rahmen_hand))
            hs = hotspot_norm(b.tipp)
            gross = aufnehmen(tmp)
            ids = id_aufnehmen(tmp)
            name = f"stufe{stufe}_{s['name']}_{zustand}" if zustand not in ZUSAETZE else zustand
            ergebnis[name] = (gross, ids)
            hot[name] = hs
            hand = art == "normal"
            ergebnis[name] = (gross, ids, hand)
            png_schreiben(os.path.join(aus, "gross", name + ".png"), srgb8(fugen(gross, ids, 96, hand)))
    if os.path.exists(tmp):
        os.remove(tmp)
    # Zielgrößen
    klein = {}
    for name, (gross, ids, hand) in ergebnis.items():
        for g in GROESSEN:
            for chunk_, ordner in ((1, "fein"),):
                low = g // chunk_
                a = kontur(fugen_klein(mitteln(fugen(gross, ids, low, hand), SUPER // low), ids, SUPER // low, hand))
                fin = np.repeat(np.repeat(a, chunk_, 0), chunk_, 1)
                klein[(name, g, ordner)] = fin
                png_schreiben(os.path.join(aus, ordner, str(g), name + ".png"), srgb8(fin))
    zeilen = []
    for name, hs in hot.items():
        zeilen.append(name + ": " + ", ".join(f"{g}px=({round(hs[0] * g)},{round(hs[1] * g)})" for g in GROESSEN))
    open(os.path.join(aus, "hotspots.txt"), "w").write("\n".join(zeilen) + "\n")
    if mit_uebersicht:
        from_scratch_uebersicht(aus, stufen, klein)


# ─────────────────────────────────────────────────────────────────────────────
#  Übersicht
# ─────────────────────────────────────────────────────────────────────────────

ZIFFERN = {
    "1": ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
    "2": [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
    "3": [".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###."],
    "4": ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
    "5": ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
    "6": [".###.", "#....", "####.", "#...#", "#...#", "#...#", ".###."],
}


def ziffer(c, ziffer_, x, y, farbe, z=3):
    for j, zeile in enumerate(ZIFFERN[ziffer_]):
        for i, ch in enumerate(zeile):
            if ch == "#":
                c[y + j * z:y + (j + 1) * z, x + i * z:x + (i + 1) * z, :3] = farbe


def einsetzen(c, sprite, x, y, skal=1):
    if skal > 1:
        sprite = np.repeat(np.repeat(sprite, skal, 0), skal, 1)
    h, w, _ = sprite.shape
    H, W, _ = c.shape
    x0, y0 = max(x, 0), max(y, 0)
    x1, y1 = min(x + w, W), min(y + h, H)
    if x1 <= x0 or y1 <= y0:
        return
    sp = sprite[y0 - y:y1 - y, x0 - x:x1 - x]
    al = sp[..., 3:4]
    c[y0:y1, x0:x1, :3] = c[y0:y1, x0:x1, :3] * (1 - al) + sp[..., :3] * al


def pergament(h, w):
    rng = np.random.default_rng(3)
    base = np.array([0.84, 0.75, 0.57])
    n = rng.normal(0, 0.012, (h, w, 1))
    yy = np.linspace(0, 1, h)[:, None, None]
    return np.clip(base + n - 0.05 * yy, 0, 1)


def nacht(h, w):
    rng = np.random.default_rng(5)
    base = np.array([0.045, 0.06, 0.11])
    return np.clip(base + rng.normal(0, 0.008, (h, w, 1)), 0, 1)


def spielbild(h, w, pfad):
    if pfad and os.path.exists(pfad):
        img = lesen_srgb(pfad)
        H, W, _ = img.shape
        y0, x0 = max(0, H // 2 - h // 2 - 120), max(0, W // 2 - w // 2 - 80)
        crop = img[y0:y0 + h, x0:x0 + w, :3]
        if crop.shape[0] == h and crop.shape[1] == w:
            return crop
    return np.full((h, w, 3), 0.35)


def lesen_srgb(pfad):
    img = bpy.data.images.load(pfad)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    ch = img.channels
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, ch)[::-1]
    bpy.data.images.remove(img)
    return a


def from_scratch_uebersicht(aus, stufen, klein, spielbild_pfad=None):
    spielbild_pfad = spielbild_pfad or os.environ.get("SPIELBILD", "")
    ns = len(stufen)

    def sp(st, z, g, ordner="fein"):
        if z in ZUSAETZE:
            return klein[(z, g, ordner)]
        return klein[(f"stufe{st}_{STUFEN[st - 1]['name']}_{z}", g, ordner)]
    # Teil A: alle Stufen groß (4-fach von 64 px = 256), normal
    ca = 4 * 64 + 16
    breite = ns * ca + 16
    hoehe_a = 4 * 64 + 40
    # Teil B: je Stufe die Zustände (3-fach von 48 px) in zwei Blöcken
    zb = 3 * 48 + 6
    zus = ("normal", "klick") + ZUSAETZE
    bloecke = [stufen[:(ns + 1) // 2], stufen[(ns + 1) // 2:]]
    hoehe_b = max(len(x) for x in bloecke) * zb + 24
    # Teil C: Originalgröße auf drei Hintergründen
    zeilen_c = [(32, "normal", "fein"), (48, "normal", "fein"), (64, "normal", "fein"),
                (48, "klick", "fein"), (48, "tuer_schloss", "fein"), (48, "tuer_schloss_eisen", "fein"), (48, "tuer_ring", "fein")]
    hoehe_c = len(zeilen_c) * 56 + 20
    H = hoehe_a + hoehe_b + hoehe_c
    c = np.zeros((H, breite, 4), dtype=np.float32)
    c[..., 3] = 1
    c[:hoehe_a, :, :3] = pergament(hoehe_a, breite)
    for k, st in enumerate(stufen):
        einsetzen(c, sp(st, "normal", 64), 8 + k * ca + 8, 34, 4)
        ziffer(c, str(st), 8 + k * ca + 8 + 128 - 7, 8, (0.2, 0.1, 0.05))
    y = hoehe_a
    c[y:y + hoehe_b, :, :3] = pergament(hoehe_b, breite) * 0.92
    for bi, bl in enumerate(bloecke):
        for r, st in enumerate(bl):
            for zi, z in enumerate(zus):
                einsetzen(c, sp(st, z, 48), 40 + bi * (breite // 2) + zi * (zb + 2), y + 12 + r * zb, 3)
            ziffer(c, str(st), 12 + bi * (breite // 2), y + 12 + r * zb + 60, (0.2, 0.1, 0.05))
    y = hoehe_a + hoehe_b
    kachel = breite // 3
    hintergruende = [pergament(hoehe_c, kachel), nacht(hoehe_c, kachel), spielbild(hoehe_c, kachel, spielbild_pfad)]
    for k, bg in enumerate(hintergruende):
        c[y:y + hoehe_c, k * kachel:(k + 1) * kachel, :3] = bg
        for r, (g, z, o) in enumerate(zeilen_c):
            for i, st in enumerate(stufen if z not in ZUSAETZE else stufen[:1]):
                einsetzen(c, sp(st, z, g, o), k * kachel + 14 + i * 60, y + 10 + r * 56, 1)
    png_schreiben(os.path.join(aus, "uebersicht.png"), srgb8(c))


def args():
    a = {}
    for s in sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []:
        if s.startswith("--") and "=" in s:
            k, v = s[2:].split("=", 1)
            a[k] = v
        elif s.startswith("--"):
            a[s[2:]] = True
    return a


if __name__ == "__main__":
    a = args()
    aus = a.get("aus", os.path.join(os.getcwd(), "mauszeiger3d"))
    stufen = [int(x) for x in a.get("stufen", "1,2,3,4,5,6").split(",")]
    alles(aus, stufen, not a.get("ohne-uebersicht"))
