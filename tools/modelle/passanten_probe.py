"""Probe „Weg A“: Passant mit echtem Skelett (MakeHuman/MPFB-Körper, Kleidung um 1400, Bewegungen von Quaternius).

    blender -b --factory-startup --python tools/modelle/passanten_probe.py -- \
        --aus=game/assets/modelle/figuren/probe/passant_probe.glb --bilder=<Ordner> \
        --ual1=<AnimationLibrary_Godot_Standard.glb> --ual2=<UAL2_Standard.glb> [--bericht=<datei.json>]

MPFB 2 (2.0.18) muss als Erweiterung „bl_ext.user_default.mpfb“ in einem eigenen Benutzerordner liegen
(Umgebungsvariable BLENDER_USER_RESOURCES, nie im Blender des Nutzers); das Skript schaltet sie selbst ein.

Quellen (nichts davon liegt im Repository, nur die erzeugte .glb):
  * Körper, Gewichte und Spielskelett „game_engine“: MakeHuman-Grundnetz und Rig aus MPFB 2 – Assets CC0, der GPL-Code
    von MPFB läuft nur als Werkzeug; die Ausgabe gehört uns (MPFB-LICENSE.md, Abschnitt D).
  * Bewegungen: Quaternius „Universal Animation Library“ 1 und 2, Fassung Standard (CC0), über OpenGameArt:
    Walk_Loop → Gehen, Idle_Loop → Stehen, Idle_Talking_Loop → Reden (UAL 1), Walk_Carry_Loop → Tragen (UAL 2).
    Übertragen auf unser Skelett hier im Skript (Richtung jedes Knochens wie in der Quelle, Hüfthöhe angeglichen).
  * Kleidung um 1400 (Cotte mit Gürtel, Gugel mit Schulterkragen und Zipfel, knöchellanger Rock): eigene Formen auf dem
    Körper (aufgedickte Körperflächen, der Rockhelfer des Grundnetzes, ein Zipfel), keine MakeHuman-Kleidung.

Skelett: 23 Knochen mit den Namen von Godots SkeletonProfileHumanoid (Root, Hips, Spine, Chest, UpperChest, Neck, Head,
Left/RightShoulder, …UpperArm, …LowerArm, …Hand, …UpperLeg, …LowerLeg, …Foot, …Toes); die Fingergewichte sind in die Hand
gelegt. Netze: je Variante „Nah_<variante>“ (Ziel 1500–3000 Dreiecke) und „Fern_<variante>“ (rund ein Viertel), dazu „Last“
(Kiste, nur beim Tragen sichtbar). Farben wie bei den Figuren aus figuren.py: Zone in UV.x, feste Farbe als Eckenfarbe
(figur.gdshader), damit das Spiel die Tracht über dieselben fünf Farben setzt.
"""

import json
import math
import os
import sys
import importlib
import addon_utils

import bmesh
import bpy
from mathutils import Matrix, Vector, Quaternion

# ─────────────────────────────────────────────────────────────────────────────
#  Einstellungen
# ─────────────────────────────────────────────────────────────────────────────

FPS = 24
NAH_ZIEL = 2400          # Dreiecke je Variante, nah
FERN_ZIEL = 560          # Dreiecke je Variante, fern
VARIANTEN = ("cotte", "gugel", "rock")

# Zonen wie figur.gdshader: 0 feste Farbe, 1 Gewand, 2 Beinkleid, 3 Haut, 4 Haar, 5 Kopfbedeckung
STOFFE = {  # Name: (Zone, feste Farbe sRGB)
    "Schuh": (0, (0.20, 0.14, 0.09)),
    "Guertel": (0, (0.30, 0.20, 0.11)),
    "Kiste": (0, (0.55, 0.40, 0.24)),
    "Gewand": (1, (1.0, 1.0, 1.0)),
    "Beinkleid": (2, (1.0, 1.0, 1.0)),
    "Haut": (3, (1.0, 1.0, 1.0)),
    "Haar": (4, (1.0, 1.0, 1.0)),
    "Kopf": (5, (1.0, 1.0, 1.0)),
}
STOFF_NAMEN = list(STOFFE)

# Knochen des MPFB-Skeletts „game_engine“ → SkeletonProfileHumanoid (Godot)
KNOCHEN = {
    "Root": "Root", "pelvis": "Hips", "spine_01": "Spine", "spine_02": "Chest", "spine_03": "UpperChest",
    "neck_01": "Neck", "head": "Head",
}
for _s, _S in (("l", "Left"), ("r", "Right")):
    KNOCHEN.update({
        f"clavicle_{_s}": f"{_S}Shoulder", f"upperarm_{_s}": f"{_S}UpperArm", f"lowerarm_{_s}": f"{_S}LowerArm",
        f"hand_{_s}": f"{_S}Hand", f"thigh_{_s}": f"{_S}UpperLeg", f"calf_{_s}": f"{_S}LowerLeg",
        f"foot_{_s}": f"{_S}Foot", f"ball_{_s}": f"{_S}Toes",
    })

# Quaternius-Rig (Rigify-DEF-Knochen) → unser Skelett
UAL_KNOCHEN = {
    "DEF-hips": "Hips", "DEF-spine.001": "Spine", "DEF-spine.002": "Chest", "DEF-spine.003": "UpperChest",
    "DEF-neck": "Neck", "DEF-head": "Head",
}
for _s, _S in (("L", "Left"), ("R", "Right")):
    UAL_KNOCHEN.update({
        f"DEF-shoulder.{_s}": f"{_S}Shoulder", f"DEF-upper_arm.{_s}": f"{_S}UpperArm", f"DEF-forearm.{_s}": f"{_S}LowerArm",
        f"DEF-hand.{_s}": f"{_S}Hand", f"DEF-thigh.{_s}": f"{_S}UpperLeg", f"DEF-shin.{_s}": f"{_S}LowerLeg",
        f"DEF-foot.{_s}": f"{_S}Foot", f"DEF-toe.{_s}": f"{_S}Toes",
    })

# UAL 2 benennt dieselben Knochen wie Unreal (pelvis, spine_01 … wie MPFB, nur „Head“ groß)
UAL_KNOCHEN.update({k: v for k, v in KNOCHEN.items() if k not in ("Root", "head")})
UAL_KNOCHEN["Head"] = "Head"

# Bewegungen: Name im Spiel → (Quelle, Clip)
BEWEGUNGEN = {
    "Gehen": ("ual1", "Walk_Loop"),
    "Stehen": ("ual1", "Idle_Loop"),
    "Reden": ("ual1", "Idle_Talking_Loop"),
    "Tragen": ("ual2", "Walk_Carry_Loop"),
}

# Vorschaufarben (sRGB) je Zone und Variante – nur für die Bilder, das Spiel setzt die Farben selbst
VORSCHAU = {
    "cotte": {1: (0.42, 0.20, 0.14), 2: (0.30, 0.26, 0.22), 3: (0.85, 0.68, 0.55), 4: (0.38, 0.25, 0.14), 5: (0.6, 0.5, 0.3)},
    "gugel": {1: (0.26, 0.32, 0.22), 2: (0.24, 0.24, 0.26), 3: (0.85, 0.68, 0.55), 4: (0.25, 0.17, 0.10), 5: (0.55, 0.16, 0.12)},
    "rock": {1: (0.20, 0.24, 0.40), 2: (0.36, 0.30, 0.20), 3: (0.85, 0.68, 0.55), 4: (0.72, 0.58, 0.32), 5: (0.6, 0.5, 0.3)},
}


def optionen():
    aus = {}
    if "--" in sys.argv:
        for a in sys.argv[sys.argv.index("--") + 1:]:
            if a.startswith("--") and "=" in a:
                k, v = a[2:].split("=", 1)
                aus[k] = v
    return aus


OPT = optionen()
BERICHT = {"quellen": {}, "netze": {}, "bewegungen": {}}


def holen(paket, name):
    for m in list(sys.modules):
        if m.endswith(paket):
            return getattr(importlib.import_module(m), name)
    raise RuntimeError("MPFB nicht gefunden: " + paket)


def aktiv(obj):
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        if o is not None:
            o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def dreiecke(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


# ─────────────────────────────────────────────────────────────────────────────
#  1. Mensch aus MPFB
# ─────────────────────────────────────────────────────────────────────────────

def mensch_bauen():
    addon_utils.enable("bl_ext.user_default.mpfb", default_set=True, persistent=False)
    mpfb = sys.modules["bl_ext.user_default.mpfb"]
    BERICHT["quellen"]["mpfb"] = str(getattr(mpfb, "VERSION", "2.0.18"))
    ts = holen("mpfb.services.targetservice", "TargetService")
    hs = holen("mpfb.services.humanservice", "HumanService")
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    makros = dict(ts.get_default_macro_info_dict())
    # Mann, etwa 25 Jahre, mittlere Kraft und Statur; Herkunft wie bei den Porträtköpfen (charakter-werkzeug)
    makros.update({"gender": 1.0, "age": 0.5, "muscle": 0.5, "weight": 0.5, "height": 0.5, "proportions": 0.5})
    makros["race"] = {"caucasian": 1.0, "asian": 0.0, "african": 0.0}
    BERICHT["makros"] = makros
    mensch = hs.create_human(mask_helpers=False, detailed_helpers=True, extra_vertex_groups=True,
                             feet_on_ground=True, macro_detail_dict=makros)
    rig = hs.add_builtin_rig(mensch, "game_engine")
    ts.bake_targets(mensch)
    for m in list(mensch.modifiers):
        if m.type != "ARMATURE":
            mensch.modifiers.remove(m)
    return mensch, rig


def gruppen_index(obj):
    return {g.index: g.name for g in obj.vertex_groups}


def teil_kopieren(quelle, name, behalten):
    """Kopie von quelle mit nur den Ecken, für die behalten(ecke, gruppen) wahr ist."""
    neu = quelle.copy()
    neu.data = quelle.data.copy()
    neu.name = neu.data.name = name
    bpy.context.collection.objects.link(neu)
    namen = gruppen_index(neu)
    bm = bmesh.new()
    bm.from_mesh(neu.data)
    deform = bm.verts.layers.deform.active
    weg = []
    for v in bm.verts:
        g = {namen[i]: w for i, w in v[deform].items()} if deform else {}
        if not behalten(v, g):
            weg.append(v)
    bmesh.ops.delete(bm, geom=weg, context="VERTS")
    bm.to_mesh(neu.data)
    bm.free()
    return neu


def in_gruppe(name, grenze=0.5):
    return lambda v, g: g.get(name, 0.0) > grenze


# ─────────────────────────────────────────────────────────────────────────────
#  2. Zonen und Kleidung
# ─────────────────────────────────────────────────────────────────────────────

def stoffe_anlegen(obj):
    obj.data.materials.clear()
    for name in STOFF_NAMEN:
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        obj.data.materials.append(m)


def herrschender_knochen(obj, v, namen, deform):
    beste, bw = "", 0.0
    for i, w in v[deform].items():
        n = namen.get(i, "")
        if n in KNOCHEN or n.split("_")[0] in ("index", "middle", "ring", "pinky", "thumb"):
            if w > bw:
                beste, bw = n, w
    return beste


def koerper_zonen(koerper, masse):
    """Ordnet jeder Fläche des Körpers einen Stoff zu (Haut, Gewand, Beinkleid, Schuh, Gürtel) und dickt die Cotte auf."""
    stoffe_anlegen(koerper)
    namen = gruppen_index(koerper)
    bm = bmesh.new()
    bm.from_mesh(koerper.data)
    deform = bm.verts.layers.deform.active
    bm.verts.ensure_lookup_table()
    knochen = [herrschender_knochen(koerper, v, namen, deform) for v in bm.verts]
    huefte = masse["guertel"]
    for f in bm.faces:
        zaehler = {}
        for v in f.verts:
            k = knochen[v.index]
            zaehler[k] = zaehler.get(k, 0) + 1
        k = max(zaehler, key=zaehler.get)
        z = f.calc_center_median().z
        if k.startswith(("head", "neck")) or k.startswith(("hand", "index", "middle", "ring", "pinky", "thumb")):
            stoff = "Haut"
        elif k.startswith(("thigh", "calf")):
            stoff = "Beinkleid"
        elif k.startswith(("foot", "ball")):
            stoff = "Schuh"
        else:
            stoff = "Gewand"
        # Halsausschnitt: der Hals bleibt Haut bis knapp über die Schlüsselbeine
        if stoff == "Gewand" and abs(z - huefte) < 0.022:
            stoff = "Guertel"
        f.material_index = STOFF_NAMEN.index(stoff)
    # Aufdicken: Cotte 12 mm, Gürtel 20 mm, Schuhe 6 mm (Ecken an Grenzen nehmen das Mittel ihrer Flächen)
    dicke = {"Gewand": 0.012, "Guertel": 0.020, "Schuh": 0.006}
    bm.normal_update()
    versatz = []
    for v in bm.verts:
        ds = [dicke.get(STOFF_NAMEN[f.material_index], 0.0) for f in v.link_faces]
        versatz.append(sum(ds) / len(ds) if ds else 0.0)
    for v, d in zip(bm.verts, versatz):
        if d:
            v.co += v.normal * d
    # Glätten: Brust, Bauch und Zehen zeichnen sich sonst durch Cotte und Schuhe ab
    for stoff, schritte in (("Gewand", 8), ("Schuh", 40)):
        i = STOFF_NAMEN.index(stoff)
        innen = [v for v in bm.verts if v.link_faces and all(f.material_index == i for f in v.link_faces)]
        for _ in range(schritte):
            bmesh.ops.smooth_vert(bm, verts=innen, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.to_mesh(koerper.data)
    bm.free()


def koerper_aussparen(koerper, unten, oben, name):
    """Kopie des Körpers ohne Becken und Beine zwischen unten und oben (Meter): Unter dem Rock sieht man sie nie, beim Gehen
    stachen die Oberschenkel sonst durch den Stoff; spart nebenbei Dreiecke."""
    neu = koerper.copy()
    neu.data = koerper.data.copy()
    neu.name = neu.data.name = name
    bpy.context.collection.objects.link(neu)
    namen = gruppen_index(neu)
    bm = bmesh.new()
    bm.from_mesh(neu.data)
    deform = bm.verts.layers.deform.active
    bm.verts.ensure_lookup_table()
    knochen = [herrschender_knochen(neu, v, namen, deform) for v in bm.verts]
    weg = []
    for f in bm.faces:
        zaehler = {}
        for v in f.verts:
            zaehler[knochen[v.index]] = zaehler.get(knochen[v.index], 0) + 1
        k = max(zaehler, key=zaehler.get)
        if k.startswith(("pelvis", "thigh", "calf")) and unten < f.calc_center_median().z < oben:
            weg.append(f)
    bmesh.ops.delete(bm, geom=weg, context="FACES")
    bm.to_mesh(neu.data)
    bm.free()
    return neu


def gugel_bauen(koerper, masse):
    """Gugel: Kapuze mit Schulterkragen aus den Körperflächen von Kopf, Hals und Schultern, aufgedickt, Gesicht ausgespart,
    dazu ein Zipfel am Hinterkopf."""
    namen = gruppen_index(koerper)
    kopfmitte = masse["kopfmitte"]
    gesicht_unten, gesicht_oben = masse["kinn"], masse["stirn"]

    def behalten(v, g):
        kopf = g.get("head", 0) + g.get("neck_01", 0)
        z = v.co.z
        if kopf > 0.3 or z > masse["kragen"]:
            # Gesicht frei: vorn (−Y) zwischen Kinn und Stirn
            if v.co.y < kopfmitte.y - 0.035 and gesicht_unten < z < gesicht_oben and abs(v.co.x) < 0.075:
                return False
            return z > masse["kragen"] - 0.02 or kopf > 0.3
        return False

    gugel = teil_kopieren(koerper, "Gugel", behalten)
    stoffe_anlegen(gugel)
    bm = bmesh.new()
    bm.from_mesh(gugel.data)
    bm.normal_update()
    neu = []
    for v in bm.verts:
        d = 0.022 if v.co.z > masse["hals"] else 0.030
        neu.append(v.co + v.normal * d)
    for v, c in zip(bm.verts, neu):
        v.co = c
    for f in bm.faces:
        f.material_index = STOFF_NAMEN.index("Kopf")
    bm.to_mesh(gugel.data)
    bm.free()
    zipfel = zipfel_bauen(gugel, masse)
    return [gugel, zipfel]


def zipfel_bauen(vorlage, masse):
    """Zipfel der Gugel: ein Kegel vom Hinterkopf den Rücken hinab, am Kopf starr, unten mit Hals und Brustkorb."""
    me = bpy.data.meshes.new("Zipfel")
    obj = bpy.data.objects.new("Zipfel", me)
    bpy.context.collection.objects.link(obj)
    k = masse["kopfmitte"]
    ringe = [  # (Höhe, Abstand nach hinten, Halbmesser, Gewichte)
        (k.z + 0.05, k.y + 0.075, 0.045, {"head": 1.0}),
        (k.z - 0.06, k.y + 0.115, 0.032, {"head": 0.6, "neck_01": 0.4}),
        (k.z - 0.20, k.y + 0.140, 0.022, {"neck_01": 0.3, "spine_03": 0.7}),
        (k.z - 0.36, k.y + 0.155, 0.010, {"spine_03": 1.0}),
    ]
    n = 6
    bm = bmesh.new()
    reihen = []
    for z, y, r, _ in ringe:
        reihen.append([bm.verts.new((r * math.cos(2 * math.pi * i / n), y + 0.6 * r * math.sin(2 * math.pi * i / n), z)) for i in range(n)])
    for a, b in zip(reihen, reihen[1:]):
        for i in range(n):
            bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    spitze = bm.verts.new((0, ringe[-1][1] + 0.01, ringe[-1][0] - 0.05))
    for i in range(n):
        bm.faces.new((reihen[-1][i], reihen[-1][(i + 1) % n], spitze))
    bm.to_mesh(me)
    bm.free()
    stoffe_anlegen(obj)
    for p in me.polygons:
        p.material_index = STOFF_NAMEN.index("Kopf")
    gewichte = [g for _, _, _, g in ringe for _ in range(n)] + [ringe[-1][3]]
    for name in vorlage.vertex_groups.keys():
        obj.vertex_groups.new(name=name)
    for i, g in enumerate(gewichte):
        for name, w in g.items():
            obj.vertex_groups[name].add([i], w, "REPLACE")
    obj.parent = vorlage.parent
    return obj


def rock_bauen(rockhelfer, masse, saum, name):
    """Rockteil der Cotte (Saum am Knie) oder langer Rock (Saum über dem Knöchel) aus dem Rockhelfer des Grundnetzes:
    oben abgeschnitten am Gürtel, unten am Saum (fehlt Länge, wird der untere Rand nach unten verlängert und leicht ausgestellt).
    Gewichte: Becken oben, nach unten immer mehr die Oberschenkel (links/rechts nach der Seite), damit der Rock dem Schritt folgt."""
    obj = rockhelfer.copy()
    obj.data = rockhelfer.data.copy()
    obj.name = obj.data.name = name
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    oben = masse["guertel"] + 0.03
    for f in list(bm.faces):
        if all(v.co.z > oben for v in f.verts):
            bm.faces.remove(f)
    unten_jetzt = min(v.co.z for v in bm.verts)
    if saum > unten_jetzt + 0.01:
        geo = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, saum), plane_no=(0, 0, 1),
                                     clear_inner=True)
    elif saum < unten_jetzt - 0.01:
        rand = [e for e in bm.edges if e.is_boundary and all(v.co.z < unten_jetzt + 0.03 for v in e.verts)]
        schritte = max(1, int(math.ceil((unten_jetzt - saum) / 0.12)))
        mitte = Vector((0, sum(v.co.y for e in rand for v in e.verts) / (2 * len(rand)), 0))
        for s in range(schritte):
            erg = bmesh.ops.extrude_edge_only(bm, edges=rand)
            neue = [g for g in erg["geom"] if isinstance(g, bmesh.types.BMVert)]
            for v in neue:
                waag = Vector((v.co.x - mitte.x, v.co.y - mitte.y, 0))
                v.co.z -= (unten_jetzt - saum) / schritte
                v.co += waag.normalized() * 0.012 if waag.length > 1e-6 else Vector()
            rand = [g for g in erg["geom"] if isinstance(g, bmesh.types.BMEdge) and all(v in neue for v in g.verts)]
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.normal_update()
    huefte = masse["huefte"]
    for v in bm.verts:
        # ausgestellt: unterhalb der Hüfte bis zu 7 cm weiter, damit das vorschwingende Bein im Rock bleibt
        u = max(0.0, min((huefte - v.co.z) / masse["oberschenkel"], 1.6))
        waag = Vector((v.co.x, v.co.y - masse["kopfmitte"].y, 0))
        v.co += v.normal * 0.016
        if waag.length > 1e-6:
            v.co += waag.normalized() * 0.045 * u
    bm.to_mesh(obj.data)
    bm.free()
    stoffe_anlegen(obj)
    for p in obj.data.polygons:
        p.material_index = STOFF_NAMEN.index("Gewand")
    # Gewichte neu
    for g in list(obj.vertex_groups):
        obj.vertex_groups.remove(g)
    for name in ("pelvis", "thigh_l", "thigh_r", "spine_01"):
        obj.vertex_groups.new(name=name)
    huefte = masse["huefte"]
    for v in obj.data.vertices:
        z = v.co.z
        if z >= huefte:
            a = 0.0
        else:
            u = min((huefte - z) / masse["oberschenkel"], 1.0)
            a = 0.85 * u * u * (3 - 2 * u)
        s = max(-1.0, min(1.0, v.co.x / 0.09))
        wl, wr = a * (0.5 + 0.5 * s), a * (0.5 - 0.5 * s)
        if 1 - a > 1e-4:
            obj.vertex_groups["pelvis"].add([v.index], 1 - a, "REPLACE")
        if wl > 1e-4:
            obj.vertex_groups["thigh_l"].add([v.index], wl, "REPLACE")
        if wr > 1e-4:
            obj.vertex_groups["thigh_r"].add([v.index], wr, "REPLACE")
    return obj


def haar_bauen(haarhelfer, masse):
    """Kurzes Männerhaar aus dem Haarhelfer des Grundnetzes: hinten bis etwa Ohrläppchen, vorn über der Stirn abgeschnitten."""
    obj = haarhelfer.copy()
    obj.data = haarhelfer.data.copy()
    obj.name = obj.data.name = "Haar"
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    k = masse["kopfmitte"]
    weg = [v for v in bm.verts if v.co.z < masse["kinn"] + 0.06 or (v.co.y < k.y - 0.045 and v.co.z < masse["stirn"] + 0.005)]
    bmesh.ops.delete(bm, geom=weg, context="VERTS")
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * 0.004
    bm.to_mesh(obj.data)
    bm.free()
    stoffe_anlegen(obj)
    for p in obj.data.polygons:
        p.material_index = STOFF_NAMEN.index("Haar")
    for g in list(obj.vertex_groups):
        obj.vertex_groups.remove(g)
    obj.vertex_groups.new(name="head").add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    return obj


def zusammenfuegen(teile, name):
    kopien = []
    for t in teile:
        k = t.copy()
        k.data = t.data.copy()
        bpy.context.collection.objects.link(k)
        kopien.append(k)
    aktiv(kopien[0])
    for k in kopien:
        k.select_set(True)
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = obj.data.name = name
    return obj


# ─────────────────────────────────────────────────────────────────────────────
#  3. Gewichte, Verkleinern, Zonen-Attribute
# ─────────────────────────────────────────────────────────────────────────────

def gewichte_aufraeumen(obj):
    """Finger → Hand, alle übrigen Gruppen weg, höchstens 4 Knochen je Ecke, normiert, Namen nach SkeletonProfileHumanoid."""
    namen = gruppen_index(obj)
    werte = []
    for v in obj.data.vertices:
        g = {}
        for e in v.groups:
            n = namen[e.group]
            if n.split("_")[0] in ("index", "middle", "ring", "pinky", "thumb"):
                n = "hand_" + n[-1]
            if n in KNOCHEN and n != "Root":
                g[KNOCHEN[n]] = g.get(KNOCHEN[n], 0.0) + e.weight
        beste = sorted(g.items(), key=lambda x: -x[1])[:4]
        summe = sum(w for _, w in beste)
        if summe <= 1e-6:
            beste, summe = [("Hips", 1.0)], 1.0
        werte.append([(n, w / summe) for n, w in beste])
    for g in list(obj.vertex_groups):
        obj.vertex_groups.remove(g)
    for n in set(KNOCHEN.values()):
        obj.vertex_groups.new(name=n)
    for i, liste in enumerate(werte):
        for n, w in liste:
            obj.vertex_groups[n].add([i], w, "REPLACE")


def verkleinern(obj, ziel):
    jetzt = dreiecke(obj)
    if jetzt > ziel:
        aktiv(obj)
        m = obj.modifiers.new("Verkleinern", "DECIMATE")
        m.decimate_type = "COLLAPSE"
        m.ratio = ziel / jetzt
        m.use_collapse_triangulate = True
        while obj.modifiers[0] != m:
            bpy.ops.object.modifier_move_up(modifier=m.name)
        bpy.ops.object.modifier_apply(modifier=m.name)
    return dreiecke(obj)


def figur_material():
    m = bpy.data.materials.get("Figur") or bpy.data.materials.new("Figur")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    baum = m.node_tree
    p = baum.nodes.get("Principled BSDF")
    p.inputs["Roughness"].default_value = 0.9
    ecke = baum.nodes.new("ShaderNodeVertexColor")
    ecke.layer_name = "Farbe"
    gamma = baum.nodes.new("ShaderNodeGamma")
    gamma.inputs["Gamma"].default_value = 2.2
    baum.links.new(ecke.outputs["Color"], gamma.inputs["Color"])
    baum.links.new(gamma.outputs["Color"], p.inputs["Base Color"])
    return m


def zonen_setzen(obj):
    """Stoff je Fläche → Zone in UV „Zone“ (x) und feste Farbe als Eckenfarbe „Farbe“ (wie figuren.py), ein Material „Figur“."""
    me = obj.data
    alte = [m.name.split(".")[0] if m else "" for m in me.materials]
    for uv in list(me.uv_layers):
        me.uv_layers.remove(uv)
    for c in list(me.color_attributes):
        me.color_attributes.remove(c)
    farbe = me.color_attributes.new("Farbe", "BYTE_COLOR", "CORNER")
    me.color_attributes.active_color = farbe
    me.color_attributes.render_color_index = me.color_attributes.find("Farbe")
    uv = me.uv_layers.new(name="Zone")
    werte, zonen = [1.0] * (len(me.loops) * 4), [0.0] * (len(me.loops) * 2)
    for poly in me.polygons:
        zone, f = STOFFE.get(alte[poly.material_index], (0, (1, 0, 1)))
        for li in poly.loop_indices:
            werte[li * 4:li * 4 + 4] = [f[0], f[1], f[2], 1.0]
            zonen[li * 2] = float(zone)
    farbe.data.foreach_set("color", werte)
    uv.data.foreach_set("uv", zonen)
    me.materials.clear()
    me.materials.append(figur_material())
    me.polygons.foreach_set("material_index", [0] * len(me.polygons))
    # Weiche Schattierung (die Figuren sind 9–25 Pixel hoch, harte Kanten würden flimmern)
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.update()


# ─────────────────────────────────────────────────────────────────────────────
#  4. Skelett umbenennen
# ─────────────────────────────────────────────────────────────────────────────

def skelett_umbauen(rig):
    aktiv(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones
    for b in list(eb):
        if b.name not in KNOCHEN:
            eb.remove(b)
    for b in eb:
        b.name = KNOCHEN[b.name]
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.name = "Skelett"
    rig.data.name = "Skelett"
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return rig


# ─────────────────────────────────────────────────────────────────────────────
#  5. Bewegungen übertragen
# ─────────────────────────────────────────────────────────────────────────────

def ual_laden(pfad):
    vorher = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=pfad)
    neu = [o for o in bpy.data.objects if o not in vorher]
    rig = next(o for o in neu if o.type == "ARMATURE")
    for o in neu:
        if o.type == "MESH" and o.name.startswith("Mannequin"):
            o.hide_render = True
            QUELLNETZE[rig.name] = o      # bleibt für die Vergleichsbilder
        elif o.type == "MESH":
            bpy.data.objects.remove(o)
    rig.animation_data_create()
    # Der Import legt weitere Clips als NLA-Spuren ab; sie würden sich in die Haltung mischen
    for spur in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(spur)
    return rig


QUELLNETZE = {}


def aktion_setzen(obj, act):
    obj.animation_data.action = act
    if hasattr(obj.animation_data, "action_slot") and getattr(act, "slots", None) and len(act.slots) and obj.animation_data.action_slot is None:
        obj.animation_data.action_slot = act.slots[0]


def richtung(rig, b):
    m = rig.matrix_world
    return ((m @ b.tail_local) - (m @ b.head_local)).normalized()


def uebertragen(quelle, ziel, act, name):
    """Überträgt einen Clip: Jeder Knochen bekommt im Weltraum dieselbe Drehung gegenüber einer Bezugshaltung, in der seine
    Richtung der Ruhehaltung der Quelle gleicht (unser Grundnetz steht in A-Haltung, Quaternius in T-Haltung). Die Hüfte
    bekommt zusätzlich die Verschiebung der Quellhüfte, mit dem Verhältnis der Hüfthöhen."""
    aktion_setzen(quelle, act)
    a, e = int(act.frame_range[0]), int(act.frame_range[1])
    mq, mz = quelle.matrix_world, ziel.matrix_world
    paare = [(q, z) for q, z in UAL_KNOCHEN.items() if q in quelle.data.bones and z in ziel.data.bones]
    bezug = {}
    for q, z in paare:
        bq, bz = quelle.data.bones[q], ziel.data.bones[z]
        rq = (mq @ bq.matrix_local).to_quaternion()
        rz = (mz @ bz.matrix_local).to_quaternion()
        # Nur Arme und Beine nach der Richtung ausrichten (A- gegen T-Haltung); Rumpf, Hals, Kopf, Schultern und Füße
        # stehen in beiden Ruhehaltungen gleich, ihre Knochenachsen sagen dort nichts über die Haltung des Körpers
        if z.endswith(("UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg")):
            dreh = richtung(ziel, bz).rotation_difference(richtung(quelle, bq))
        else:
            dreh = Quaternion()
        bezug[z] = (rq, dreh @ rz)
    qhuefte = next(q for q, z in paare if z == "Hips")
    hq = quelle.data.bones[qhuefte].head_local.z
    hz = ziel.data.bones["Hips"].head_local.z
    hq_ruhe = mq @ quelle.data.bones[qhuefte].head_local
    hz_ruhe = mz @ ziel.data.bones["Hips"].head_local
    massstab = hz / hq

    neu = bpy.data.actions.new(name)
    ziel.animation_data_create()
    ziel.animation_data.action = neu
    reihenfolge = [b.name for b in ziel.data.bones]  # Eltern stehen vor Kindern
    q_von_z = {z: q for q, z in paare}
    for bild in range(a, e + 1):
        bpy.context.scene.frame_set(bild)
        posen = {}  # Armaturenraum des Ziels
        for zn in reihenfolge:
            bz = ziel.data.bones[zn]
            pb = ziel.pose.bones[zn]
            eltern = bz.parent
            ruhe_rel = (eltern.matrix_local.inverted() @ bz.matrix_local) if eltern else bz.matrix_local
            elter_pose = posen[eltern.name] if eltern else Matrix.Identity(4)
            if zn in q_von_z:
                qp = quelle.pose.bones[q_von_z[zn]]
                rq_pose = (mq @ qp.matrix).to_quaternion()
                rq_ruhe, rz_bezug = bezug[zn]
                welt = rq_pose @ rq_ruhe.inverted() @ rz_bezug
                arm = mz.to_quaternion().inverted() @ welt
                ort = (elter_pose @ ruhe_rel).to_translation()
                if zn == "Hips":
                    qh = mq @ qp.matrix.to_translation()
                    ort = mz.inverted() @ (hz_ruhe + (qh - hq_ruhe) * massstab)
                gewollt = Matrix.Translation(ort) @ arm.to_matrix().to_4x4()
            else:
                gewollt = elter_pose @ ruhe_rel
            basis = ruhe_rel.inverted() @ elter_pose.inverted() @ gewollt
            loc, rot, _ = basis.decompose()
            pb.rotation_quaternion = rot
            pb.location = loc if zn == "Hips" else Vector()
            pb.keyframe_insert("rotation_quaternion", frame=bild - a + 1)
            if zn == "Hips":
                pb.keyframe_insert("location", frame=bild - a + 1)
            posen[zn] = elter_pose @ ruhe_rel @ Matrix.LocRotScale(pb.location, rot, None)
    ziel.animation_data.action = None
    spur = ziel.animation_data.nla_tracks.new()
    spur.name = name
    spur.strips.new(name, 1, neu)
    return neu, e - a


def tempo_messen(ziel, act, bilder):
    """Geschwindigkeit (m/s) des Standfußes (Gehen auf der Stelle): so schnell muss die Figur vorwärts, damit nichts rutscht."""
    ziel.animation_data.action = act
    werte = {"LeftFoot": [], "RightFoot": []}
    for f in range(1, bilder + 2):
        bpy.context.scene.frame_set(f)
        for n in werte:
            werte[n].append((ziel.matrix_world @ ziel.pose.bones[n].head).copy())
    ziel.animation_data.action = None
    v = []
    for n, lagen in werte.items():
        tiefste = min(p.z for p in lagen)
        for p0, p1 in zip(lagen, lagen[1:]):
            if p0.z < tiefste + 0.02 and p1.z < tiefste + 0.02:
                v.append((p1.y - p0.y) * FPS)
    v.sort()
    return v[len(v) // 2] if v else 0.0


# ─────────────────────────────────────────────────────────────────────────────
#  6. Last (Kiste beim Tragen)
# ─────────────────────────────────────────────────────────────────────────────

def last_bauen(rig, tragen, vorlage):
    """Kiste zwischen den Händen in der ersten Haltung von „Tragen“; je zur Hälfte an beiden Händen."""
    rig.animation_data.action = tragen
    bpy.context.scene.frame_set(1)
    mw = rig.matrix_world
    hl = mw @ rig.pose.bones["LeftHand"].matrix
    hr = mw @ rig.pose.bones["RightHand"].matrix
    ruhe_l = mw @ rig.data.bones["LeftHand"].matrix_local
    ruhe_r = mw @ rig.data.bones["RightHand"].matrix_local
    mitte = (hl.to_translation() + hr.to_translation()) / 2
    breite = max(0.30, (hl.to_translation() - hr.to_translation()).length - 0.04)
    rig.animation_data.action = None
    bpy.context.scene.frame_set(1)
    # Kiste vor dem Bauch: Mitte etwas vor und unter den Händen
    groesse = Vector((breite, 0.30, 0.26))
    zentrum = mitte + Vector((0, -0.05, -0.06))
    me = bpy.data.meshes.new("Last")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = zentrum + Vector((v.co.x * groesse.x, v.co.y * groesse.y, v.co.z * groesse.z))
    # in die Ruhelage zurück: inverse der gemittelten Handbewegung
    misch = ((hl @ ruhe_l.inverted()) + (hr @ ruhe_r.inverted())) * 0.5
    inv = misch.inverted()
    for v in bm.verts:
        v.co = inv @ v.co
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Last", me)
    bpy.context.collection.objects.link(obj)
    stoffe_anlegen(obj)
    for p in me.polygons:
        p.material_index = STOFF_NAMEN.index("Kiste")
    for n in ("LeftHand", "RightHand"):
        obj.vertex_groups.new(name=n).add(list(range(len(me.vertices))), 0.5, "REPLACE")
    zonen_setzen(obj)
    me.polygons.foreach_set("use_smooth", [False] * len(me.polygons))
    obj.parent = rig
    m = obj.modifiers.new("Skelett", "ARMATURE")
    m.object = rig
    return obj


# ─────────────────────────────────────────────────────────────────────────────
#  7. Ablauf
# ─────────────────────────────────────────────────────────────────────────────

def masse_messen(rig):
    b = rig.data.bones
    kopf = b["head"]
    return {
        "huefte": b["thigh_l"].head_local.z,
        "oberschenkel": (b["thigh_l"].head_local - b["calf_l"].head_local).length,
        "knie": b["calf_l"].head_local.z,
        "knoechel": b["foot_l"].head_local.z,
        "guertel": b["pelvis"].head_local.z + 0.035,
        "kopfmitte": (kopf.head_local + kopf.tail_local) / 2,
        "kinn": kopf.head_local.z - 0.035,
        "stirn": kopf.head_local.z + 0.115,
        "hals": b["neck_01"].head_local.z + 0.02,
        "kragen": b["clavicle_l"].head_local.z - 0.10,
    }


def hauptteil():
    mensch, rig = mensch_bauen()
    bpy.context.scene.render.fps = FPS
    masse = masse_messen(rig)
    BERICHT["masse"] = {k: (list(v) if isinstance(v, Vector) else v) for k, v in masse.items()}

    koerper = teil_kopieren(mensch, "Koerper", in_gruppe("body"))
    rockhelfer = teil_kopieren(mensch, "Rockhelfer", in_gruppe("helper-skirt"))
    haarhelfer = teil_kopieren(mensch, "Haarhelfer", in_gruppe("helper-hair"))
    BERICHT["rockhelfer_z"] = [min(v.co.z for v in rockhelfer.data.vertices), max(v.co.z for v in rockhelfer.data.vertices)]
    BERICHT["grundnetz"] = {"ecken": len(mensch.data.vertices), "koerper_dreiecke": dreiecke(koerper)}
    bpy.data.objects.remove(mensch)
    koerper_zonen(koerper, masse)
    gugel = gugel_bauen(koerper, masse)
    rock_knie = rock_bauen(rockhelfer, masse, masse["knie"] - 0.03, "RockKnie")
    rock_lang = rock_bauen(rockhelfer, masse, masse["knoechel"] + 0.05, "RockLang")
    haar = haar_bauen(haarhelfer, masse)
    bpy.data.objects.remove(rockhelfer)
    bpy.data.objects.remove(haarhelfer)

    unter_guertel = masse["guertel"] - 0.03
    koerper_knie = koerper_aussparen(koerper, masse["knie"] + 0.02, unter_guertel, "KoerperKnie")
    koerper_lang = koerper_aussparen(koerper, masse["knoechel"] + 0.12, unter_guertel, "KoerperLang")
    teile = {
        "cotte": [koerper_knie, rock_knie, haar],
        "gugel": [koerper_knie, rock_knie] + gugel,
        "rock": [koerper_lang, rock_lang, haar],
    }
    netze = []
    for variante, liste in teile.items():
        for stufe, ziel in (("Nah", NAH_ZIEL), ("Fern", FERN_ZIEL)):
            obj = zusammenfuegen(liste, f"{stufe}_{variante}")
            vorher = dreiecke(obj)
            gewichte_aufraeumen(obj)
            nachher = verkleinern(obj, ziel)
            zonen_setzen(obj)
            BERICHT["netze"][obj.name] = {"dreiecke_vorher": vorher, "dreiecke": nachher, "ecken": len(obj.data.vertices)}
            netze.append(obj)
    for t in [koerper, koerper_knie, koerper_lang, rock_knie, rock_lang, haar] + gugel:
        bpy.data.objects.remove(t)

    skelett_umbauen(rig)
    for obj in netze:
        obj.parent = rig
        for m in list(obj.modifiers):
            obj.modifiers.remove(m)
        m = obj.modifiers.new("Skelett", "ARMATURE")
        m.object = rig
    BERICHT["knochen"] = [b.name for b in rig.data.bones]

    quellen = {"ual1": ual_laden(OPT["ual1"]), "ual2": ual_laden(OPT["ual2"])}
    aktionen = {}
    for name, (q, clip) in BEWEGUNGEN.items():
        act = bpy.data.actions.get(clip)
        # beide Bibliotheken bringen eigene Aktionen; die richtige ist die, die zur Quelle passt
        kandidaten = [a for a in bpy.data.actions if a.name.split(".")[0] == clip]
        act = kandidaten[0] if kandidaten else None
        if act is None:
            raise RuntimeError(f"Clip {clip} fehlt in {q}")
        neu, bilder = uebertragen(quellen[q], rig, act, name)
        aktionen[name] = (neu, bilder)
        BERICHT["bewegungen"][name] = {"quelle": q, "clip": clip, "bilder": bilder, "sekunden": bilder / FPS}
    for name in ("Gehen", "Tragen"):
        v = tempo_messen(rig, aktionen[name][0], aktionen[name][1])
        BERICHT["bewegungen"][name]["tempo_m_s"] = v
    last = last_bauen(rig, aktionen["Tragen"][0], netze[0])
    BERICHT["netze"]["Last"] = {"dreiecke": dreiecke(last), "ecken": len(last.data.vertices)}
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()

    pfad = os.path.abspath(OPT["aus"])
    os.makedirs(os.path.dirname(pfad), exist_ok=True)
    for o in bpy.context.scene.objects:
        o.select_set(o in netze or o is rig or o is last)
    bpy.ops.export_scene.gltf(
        filepath=pfad, export_format="GLB", use_selection=True, export_yup=True, export_apply=False,
        export_extras=True, export_normals=True, export_texcoords=True, export_skins=True, export_animations=True,
        export_animation_mode="NLA_TRACKS", export_anim_slide_to_zero=True, export_morph=False, export_lights=False,
        export_cameras=False, export_image_format="NONE", export_vertex_color="ACTIVE", export_all_vertex_colors=False)
    print("Geschrieben:", pfad)
    if "bilder" in OPT:
        vorschau(rig, netze, last, aktionen, OPT["bilder"], quellen)
    if "bericht" in OPT:
        with open(OPT["bericht"], "w", encoding="utf-8") as f:
            json.dump(BERICHT, f, indent=1, ensure_ascii=False, default=str)
    print(json.dumps(BERICHT, indent=1, ensure_ascii=False, default=str))


# ─────────────────────────────────────────────────────────────────────────────
#  8. Vorschaubilder (Workbench, nur Bilder)
# ─────────────────────────────────────────────────────────────────────────────

def vorschau(rig, netze, last, aktionen, ordner, quellen):
    os.makedirs(ordner, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "VERTEX"
    sc.display.shading.show_shadows = True
    sc.display.shading.show_cavity = False
    sc.render.resolution_x, sc.render.resolution_y = 900, 1000
    sc.render.film_transparent = False
    sc.world = sc.world or bpy.data.worlds.new("Welt")
    sc.render.image_settings.file_format = "PNG"
    cam_d = bpy.data.cameras.new("Kamera")
    cam_d.type = "ORTHO"
    cam_d.ortho_scale = 2.3
    cam = bpy.data.objects.new("Kamera", cam_d)
    sc.collection.objects.link(cam)
    sc.camera = cam
    # Blick von vorn rechts oben (die Figur schaut nach −Y)
    cam.location = (2.6, -4.2, 2.4)
    richtung_ = Vector((0, 0, 0.92)) - cam.location
    cam.rotation_euler = richtung_.to_track_quat("-Z", "Y").to_euler()
    # Vorschaufarben: zweite Eckenfarbe, Zonenfarben eingesetzt
    for obj in netze + [last]:
        me = obj.data
        zone = me.uv_layers["Zone"].data
        basis = me.color_attributes["Farbe"].data
        v = me.color_attributes.new("Vorschau", "BYTE_COLOR", "CORNER")
        variante = obj.name.split("_")[-1]
        pal = VORSCHAU.get(variante, VORSCHAU["cotte"])
        werte = []
        for li in range(len(me.loops)):
            z = int(round(zone[li].uv[0]))
            c = pal.get(z) if z else tuple(basis[li].color[:3])
            werte += [c[0], c[1], c[2], 1.0]
        v.data.foreach_set("color", werte)
        me.color_attributes.active_color = v
    posen = [("Stehen", 20), ("Gehen", 8), ("Tragen", 12), ("Reden", 30)]
    alle = netze + [last]
    for obj in netze:
        stufe, variante = obj.name.split("_")
        for bewegung, bild in posen:
            if stufe == "Fern" and bewegung not in ("Gehen",):
                continue
            rig.animation_data.action = aktionen[bewegung][0]
            sc.frame_set(bild)
            for o in alle:
                o.hide_render = not (o is obj or (o is last and bewegung == "Tragen"))
            sc.render.filepath = os.path.join(ordner, f"probe_{variante}_{stufe.lower()}_{bewegung.lower()}.png")
            bpy.ops.render.render(write_still=True)
    # Vergleich: die Quaternius-Puppe im selben Bild ihres Clips (zeigt, ob die Übertragung die Haltung trifft)
    for o in alle:
        o.hide_render = True
    for bewegung, bild in posen:
        q, clip = BEWEGUNGEN[bewegung]
        qrig = quellen[q]
        netz = QUELLNETZE.get(qrig.name)
        if netz is None:
            continue
        aktion_setzen(qrig, next(a for a in bpy.data.actions if a.name.split(".")[0] == clip))
        sc.frame_set(bild - 1)
        netz.hide_render = False
        sc.render.filepath = os.path.join(ordner, f"quelle_{bewegung.lower()}.png")
        bpy.ops.render.render(write_still=True)
        netz.hide_render = True
    # Reihe: drei Varianten nebeneinander im Gehen (nah) – Kopien mit eigenem Skelett
    rig.animation_data.action = aktionen["Gehen"][0]
    sc.frame_set(8)
    for o in alle:
        o.hide_render = True
    kopien = []
    for i, variante in enumerate(VARIANTEN):
        quelle = bpy.data.objects[f"Nah_{variante}"]
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(quelle.evaluated_get(dg))
        k = bpy.data.objects.new(f"Reihe_{variante}", me)
        k.matrix_world = rig.matrix_world
        k.location.x += (i - 1) * 0.85
        sc.collection.objects.link(k)
        me.color_attributes.active_color = me.color_attributes["Vorschau"]
        kopien.append(k)
    cam_d.ortho_scale = 3.4
    sc.render.resolution_x, sc.render.resolution_y = 1400, 1000
    sc.render.filepath = os.path.join(ordner, "probe_reihe_gehen.png")
    bpy.ops.render.render(write_still=True)


hauptteil()
