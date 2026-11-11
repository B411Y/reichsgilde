"""Zieher-Clips für die Passanten (Karren-Auftrag, Teil c): drei neue Bewegungen für die vorhandenen Passanten-Dateien.

    blender -b --factory-startup --python tools/modelle/passanten_clips.py -- \
        [--ordner=game/assets/modelle/figuren/probe] [--aus=<Ordner>] \
        [--bilder=<Ordner>] [--bericht=<datei.json>] [--nur=mann,frau,ork,oger] [--pruefen=1]

Neue Clips (angehängt hinter „Betteln“, Reihenfolge = Zeilen in der VAT-Textur, Liste ProbeKlips im Spiel):
  * Ziehen         – Eigenbau aus „Gehen“: Oberkörper leicht nach vorn, beide Hände hinter dem Körper an der Zugstange des Handkarrens
                     (Handgelenke 0,22 m seitlich, 0,22 m hinter dem Fußpunkt, 0,92 m hoch; Arme von der Zweigelenk-Rechnung). Der Zieher
                     geht vor dem Karren her. (Ein Schieben-Clip wurde erst gebaut und verworfen: Nutzerentscheidung 10.11.2026, der Handkarren
                     wird gezogen; Quaternius „Push_Loop“ taugt nicht für einen Karren.)
  * Nebenhergehen  – Eigenbau aus „Gehen“: die rechte Hand hält den Stecken (Zubehör „Zub_stab“ der Dateien) schräg vor dem Körper,
                     der linke Arm schwingt locker wie beim Gehen.
  * SitzenBock     – Eigenbau aus „Sitzen“: beide Hände vor dem Bauch an den Zügeln (Zweigelenk-Rechnung der Arme auf eine feste Handlage),
                     der Körper atmet wie beim Sitzen; ruhig genug, dass der Fahrer beim Halt auf dem Bock sitzen bleiben kann.
  * (Stehen am Halt ist der vorhandene Clip „Stehen“.)

Eingang sind die fertigen .glb der Passanten (kein MPFB und keine Quaternius-Datei nötig; alles wird aus „Gehen“, „Stehen“ und „Sitzen“ der Dateien abgeleitet): Das Skript importiert sie, rechnet die neuen Clips
auf dem eigenen Skelett, schreibt sie über den glTF-Exporter in eine Hilfsdatei (nur Skelett) und kopiert deren Animationen **binär** in die
Datei, ohne die Netze, Stoffe und die neun bisherigen Clips anzufassen (`animationen_uebertragen`). `--pruefen=1` prüft, dass dieser Umweg
die vorhandenen Clips „Gehen“ und „Stehen“ unverändert zurückgibt (Rundreise).

Die Dateien aus passanten_probe.py (vollständiger Neubau mit MPFB) enthalten diese Clips nicht: nach einem Neubau dieses Skript erneut laufen lassen.
"""

import importlib.util
import json
import math
import os
import struct
import sys
import tempfile

import bpy
from mathutils import Matrix, Vector, Quaternion

HIER = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HIER, "..", ".."))

_spec = importlib.util.spec_from_file_location("passanten_probe", os.path.join(HIER, "passanten_probe.py"))
P = importlib.util.module_from_spec(_spec)
sys.modules["passanten_probe"] = P
_spec.loader.exec_module(P)

FPS = P.FPS
OPT = P.OPT
BERICHT = {}

NEUE_CLIPS = ("Ziehen", "Nebenhergehen", "SitzenBock")

# Tempo des Handkarrens bei Spieltempo 1 (KarrenArten): so schnell läuft der Zieher, die Füße dürfen nicht rutschen
ZIEHEN_TEMPO = 1.0
# Ziehen: Neigung des Oberkörpers (Grad nach vorn, zusätzlich zu „Gehen“) und Griff des Handkarrens (Handgelenke, Meter, Fußpunkt der
# Figur als Ursprung: seitlich je Hand von der Mitte, vor dem Körper, Höhe über dem Boden)
ZIEHEN_BEUGE = 14.0
ZIEHEN_GRIFF = {"seitlich": 0.22, "vor": -0.22, "hoch": 0.92}
# Handflächen: (Anteil zum Körper hin, Anteil nach oben) der Flächennormale; Griff: Handflächen nach unten, Zügel: schräg nach unten innen
ZIEHEN_PALME = (0.0, -1.0)
ZUEGEL_PALME = (0.85, -0.5)
# Stecken-Arm beim Nebenhergehen: Oberarm um diesen Winkel nach vorn gehoben (Stecken schräg vor dem Körper aufgesetzt)
STECKEN_VOR_GRAD = 14.0
# Zügel: Handlage beim Sitzen (Meter, vom Hüftknochen: vor dem Körper, seitlich nach innen, darüber)
ZUEGEL = {"vor": 0.38, "seitlich": 0.18, "hoch": 0.18, "schwank": 0.012}

# ─────────────────────────────────────────────────────────────────────────────
#  1. glTF-Datei lesen und Animationen binär umkopieren
# ─────────────────────────────────────────────────────────────────────────────


def glb_lesen(pfad):
    with open(pfad, "rb") as f:
        d = f.read()
    gesamt = struct.unpack("<I", d[8:12])[0]
    pos, j, b = 12, None, bytearray()
    while pos < gesamt:
        n, art = struct.unpack("<II", d[pos:pos + 8])
        teil = d[pos + 8:pos + 8 + n]
        if art == 0x4E4F534A:
            j = json.loads(teil.decode("utf-8"))
        elif art == 0x004E4942:
            b = bytearray(teil)
        pos += 8 + n
    return j, b


def glb_schreiben(pfad, j, b):
    b = bytes(b) + b"\0" * (-len(b) % 4)
    j["buffers"][0]["byteLength"] = len(b)
    text = json.dumps(j, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    text += b" " * (-len(text) % 4)
    gesamt = 12 + 8 + len(text) + 8 + len(b)
    with open(pfad, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, gesamt))
        f.write(struct.pack("<II", len(text), 0x4E4F534A) + text)
        f.write(struct.pack("<II", len(b), 0x004E4942) + b)


_KOMPONENTEN = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}
_BYTES = {5126: 4, 5123: 2, 5121: 1}


def werte_lesen(j, b, idx):
    """Beschreibung und Rohbytes eines Accessors (dicht gepackt, wie der Blender-Exporter Animationen schreibt)."""
    a = j["accessors"][idx]
    bv = j["bufferViews"][a["bufferView"]]
    assert "byteStride" not in bv, "Animationsdaten sind dicht gepackt"
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    laenge = a["count"] * _KOMPONENTEN[a["type"]] * _BYTES[a["componentType"]]
    return a, bytes(b[start:start + laenge])


def animationen_uebertragen(quelle, ziel_j, ziel_b, namen):
    """Kopiert die Animationen `namen` aus der glTF-Datei `quelle` in (ziel_j, ziel_b); Knoten werden über ihren Namen zugeordnet.
    Eine gleichnamige Animation im Ziel wird ersetzt."""
    qj, qb = glb_lesen(quelle)
    knoten = {n["name"]: i for i, n in enumerate(ziel_j["nodes"])}
    vorhanden = {a["name"]: i for i, a in enumerate(ziel_j["animations"])}
    for name in namen:
        qa = next((a for a in qj["animations"] if a["name"] == name), None)
        if qa is None:
            raise RuntimeError(f"Animation {name} fehlt in der Hilfsdatei")
        neu = {"name": name, "channels": [], "samplers": []}
        cache = {}

        def acc(idx):
            if idx in cache:
                return cache[idx]
            a, roh = werte_lesen(qj, qb, idx)
            while len(ziel_b) % 4:
                ziel_b.append(0)
            ziel_j["bufferViews"].append({"buffer": 0, "byteOffset": len(ziel_b), "byteLength": len(roh)})
            ziel_b.extend(roh)
            na = {"bufferView": len(ziel_j["bufferViews"]) - 1, "componentType": a["componentType"], "count": a["count"], "type": a["type"]}
            for k in ("min", "max"):
                if k in a:
                    na[k] = a[k]
            ziel_j["accessors"].append(na)
            cache[idx] = len(ziel_j["accessors"]) - 1
            return cache[idx]

        for s in qa["samplers"]:
            neu["samplers"].append({"input": acc(s["input"]), "output": acc(s["output"]), "interpolation": s.get("interpolation", "LINEAR")})
        for c in qa["channels"]:
            ziel_name = qj["nodes"][c["target"]["node"]]["name"]
            if ziel_name not in knoten:
                raise RuntimeError(f"Knoten {ziel_name} fehlt im Ziel")
            neu["channels"].append({"sampler": c["sampler"], "target": {"node": knoten[ziel_name], "path": c["target"]["path"]}})
        if name in vorhanden:
            ziel_j["animations"][vorhanden[name]] = neu
        else:
            vorhanden[name] = len(ziel_j["animations"])
            ziel_j["animations"].append(neu)


def animationen_vergleichen(a_datei, b_datei, namen):
    """Größte Abweichung der Schlüsselwerte gleichnamiger Animationen zweier Dateien (Rundreisetest). Knoten über Namen."""
    aj, ab = glb_lesen(a_datei)
    bj, bb = glb_lesen(b_datei)
    erg = {}
    for name in namen:
        x = next(a for a in aj["animations"] if a["name"] == name)
        y = next(a for a in bj["animations"] if a["name"] == name)
        gx = {(aj["nodes"][c["target"]["node"]]["name"], c["target"]["path"]): c for c in x["channels"]}
        gy = {(bj["nodes"][c["target"]["node"]]["name"], c["target"]["path"]): c for c in y["channels"]}
        groesst_rot, groesst_lage, fehlt, laenge = 0.0, 0.0, 0, None
        for schluessel, cx in gx.items():
            cy = gy.get(schluessel)
            if cy is None:
                fehlt += 1
                continue
            _, ox = werte_lesen(aj, ab, x["samplers"][cx["sampler"]]["output"])
            _, oy = werte_lesen(bj, bb, y["samplers"][cy["sampler"]]["output"])
            vx = struct.unpack(f"<{len(ox) // 4}f", ox)
            vy = struct.unpack(f"<{len(oy) // 4}f", oy)
            if len(vx) != len(vy):
                laenge = f"{schluessel}: {len(vx)} / {len(vy)}"
                break
            if schluessel[1] == "rotation":
                for i in range(0, len(vx), 4):
                    d = sum(p * q for p, q in zip(vx[i:i + 4], vy[i:i + 4]))
                    groesst_rot = max(groesst_rot, 2 * math.degrees(math.acos(min(1.0, abs(d)))))   # Winkel in Grad
            else:
                groesst_lage = max(groesst_lage, max(abs(p - q) for p, q in zip(vx, vy)))
        erg[name] = {"max_drehung_grad": round(groesst_rot, 4), "max_lage_m": round(groesst_lage, 6), "kanaele_ohne_gegenstueck": fehlt,
                     "laengenfehler": laenge, "dauer_a": aj["accessors"][x["samplers"][0]["input"]].get("max"),
                     "dauer_b": bj["accessors"][y["samplers"][0]["input"]].get("max")}
    return erg


# ─────────────────────────────────────────────────────────────────────────────
#  2. Szene: Figur importieren, Haltungen lesen und schreiben
# ─────────────────────────────────────────────────────────────────────────────


def szene_leeren():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)


def figur_laden(pfad):
    vorher = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=pfad)
    neu = [o for o in bpy.data.objects if o not in vorher]
    rig = next(o for o in neu if o.type == "ARMATURE")
    netze = {o.name: o for o in neu if o.type == "MESH"}
    return rig, netze


def haltungen_lesen(rig, act):
    """Alle Knochenhaltungen (Drehung, Lage) je Bild einer Aktion."""
    a, e = int(act.frame_range[0]), int(act.frame_range[1])
    P.aktion_setzen(rig, act)
    erg = []
    for f in range(a, e + 1):
        bpy.context.scene.frame_set(f)
        erg.append({pb.name: (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in rig.pose.bones})
    return erg


def aktion_schreiben(rig, name, bilder_posen):
    """Legt aus einer Liste von Haltungen (Bild 1 an) eine Aktion mit NLA-Spur an (wie passanten_probe.uebertragen); Hüftlage wird mitgeschrieben."""
    neu = bpy.data.actions.new(name)
    rig.animation_data_create()
    rig.animation_data.action = neu
    P.aktion_setzen(rig, neu)
    for i, posen in enumerate(bilder_posen):
        for pb in rig.pose.bones:
            q, loc = posen[pb.name]
            pb.rotation_quaternion = q
            pb.location = loc if pb.name == "Hips" else Vector()
            pb.keyframe_insert("rotation_quaternion", frame=i + 1)
            if pb.name == "Hips":
                pb.keyframe_insert("location", frame=i + 1)
    rig.animation_data.action = None
    spur = rig.animation_data.nla_tracks.new()
    spur.name = name
    spur.strips.new(name, 1, neu)
    return neu, len(bilder_posen) - 1


def aktion_ersetzen(rig, name):
    """Löscht eine vorhandene Aktion samt NLA-Spur gleichen Namens (nur für „Ziehen“ nötig, wenn die Schrittweite neu probiert wird)."""
    for spur in list(rig.animation_data.nla_tracks):
        if spur.name == name:
            rig.animation_data.nla_tracks.remove(spur)
    for a in list(bpy.data.actions):
        if a.name.split(".")[0] == name:
            bpy.data.actions.remove(a)


# ─────────────────────────────────────────────────────────────────────────────
#  3. Die Clips
# ─────────────────────────────────────────────────────────────────────────────


def weltachse_lokal(rig, knochen, achse_welt):
    return (rig.data.bones[knochen].matrix_local.to_3x3().inverted() @ rig.matrix_world.to_3x3().inverted() @ Vector(achse_welt)).normalized()


def nebenhergehen_bauen(rig, gehen, stehen):
    """Nebenhergehen aus „Gehen“: rechter Arm (Schulter bis Hand) aus dem ersten Bild von „Stehen“ (dort steht der Stecken senkrecht in der
    Faust, Zub_stab), dazu der Oberarm um STECKEN_VOR_GRAD nach vorn gehoben; der Körper geht wie beim Gehen."""
    gposen = haltungen_lesen(rig, gehen)
    sposen = haltungen_lesen(rig, stehen)[0]
    arm = ("RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand")
    vor = Quaternion(weltachse_lokal(rig, "RightUpperArm", (1, 0, 0)), -math.radians(STECKEN_VOR_GRAD))
    neu = []
    for posen in gposen:
        p = dict(posen)
        for k in arm:
            p[k] = (sposen[k][0].copy(), Vector())
        q, l = p["RightUpperArm"]
        p["RightUpperArm"] = (q @ vor, l)
        neu.append(p)
    return aktion_schreiben(rig, "Nebenhergehen", neu)


def arm_ziel_setzen(rig, seite, hand_welt, pol_welt, palme=None):
    """Zweigelenk-Rechnung (Oberarm, Unterarm) auf eine Handlage (Handgelenk, Weltraum); Pol = Richtung, in die der Ellbogen ausweicht.
    Die Knochen werden auf die berechneten Richtungen gedreht (kleinste Drehung), die Hand behält ihre Drehung relativ zum Unterarm."""
    ua, la, ha = f"{seite}UpperArm", f"{seite}LowerArm", f"{seite}Hand"
    bpy.context.view_layer.update()
    mw = rig.matrix_world
    pu, pl, ph = rig.pose.bones[ua], rig.pose.bones[la], rig.pose.bones[ha]
    schulter = mw @ pu.head
    l1 = (pl.head - pu.head).length
    l2 = (ph.head - pl.head).length
    ziel = hand_welt - schulter
    d = max(0.05, min(l1 + l2 - 1e-4, ziel.length))
    rz = ziel.normalized()
    cos_s = (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d)
    s = math.acos(max(-1.0, min(1.0, cos_s)))
    pol = (pol_welt - rz * pol_welt.dot(rz)).normalized()
    oberarm_dir = (rz * math.cos(s) + pol * math.sin(s)).normalized()
    ellbogen = schulter + oberarm_dir * l1
    unterarm_dir = (schulter + rz * d - ellbogen).normalized()

    def ausrichten(pb, richtung):
        bpy.context.view_layer.update()
        m = pb.matrix.copy()                                     # Armaturenraum (die Armatur steht unverdreht im Ursprung)
        alt = (m.to_3x3() @ Vector((0, 1, 0))).normalized()
        dreh = alt.rotation_difference(richtung).to_matrix()
        neu = (dreh @ m.to_3x3()).to_4x4()
        neu.translation = m.translation
        pb.matrix = neu
        bpy.context.view_layer.update()

    rel = (pl.matrix.inverted() @ ph.matrix).to_3x3()
    ausrichten(pu, oberarm_dir)
    ausrichten(pl, unterarm_dir)
    neu = (pl.matrix.to_3x3() @ rel).to_4x4()
    neu.translation = ph.matrix.translation
    ph.matrix = neu
    bpy.context.view_layer.update()
    if palme is not None:
        # Handfläche (Knochenachse +Z, in der Ruhelage zum Körper hin) um die Handachse auf die Richtung `palme` drehen (Griff, Zügel)
        m3 = ph.matrix.to_3x3()
        y = (m3 @ Vector((0, 1, 0))).normalized()
        z = m3 @ Vector((0, 0, 1))
        z = (z - y * z.dot(y)).normalized()
        t = (palme - y * palme.dot(y)).normalized()
        winkel = math.atan2(y.dot(z.cross(t)), z.dot(t))
        neu = (Quaternion(y, winkel).to_matrix() @ m3).to_4x4()
        neu.translation = ph.matrix.translation
        ph.matrix = neu
        bpy.context.view_layer.update()


def arme_auf_ziele(rig, quelle, name, ziele, palme, pol_hoch=-0.6):
    """Baut aus der Aktion `quelle` (Körper, Beine) eine neue mit beiden Armen auf Handlagen: `ziele(i, huefte)` gibt je Seite die Lage des
    Handgelenks im Weltraum (Armatur steht unverdreht im Ursprung). Das letzte Bild gleicht dem ersten (Schleife)."""
    a, e = int(quelle.frame_range[0]), int(quelle.frame_range[1])
    n = e - a + 1
    P.aktion_setzen(rig, quelle)
    neu = []
    mw = rig.matrix_world
    for i in range(n - 1):
        bpy.context.scene.frame_set(a + i)
        bpy.context.view_layer.update()
        huefte = mw @ rig.pose.bones["Hips"].head
        lagen = ziele(i, n - 1, huefte)
        for seite, vorzeichen in (("Left", 1.0), ("Right", -1.0)):       # die Figur blickt nach −Y: links ist +X
            arm_ziel_setzen(rig, seite, lagen[seite], Vector((vorzeichen, 0.2, pol_hoch)), Vector((-vorzeichen * palme[0], 0.0, palme[1])))
        neu.append({pb.name: (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in rig.pose.bones})
    neu.append(neu[0])
    return aktion_schreiben(rig, name, neu)


def ziehen_bauen(rig, gehen):
    """Ziehen aus „Gehen“ (Eigenbau): Oberkörper um ZIEHEN_BEUGE Grad nach vorn, beide Hände fest am Griff des Handkarrens (ZIEHEN_GRIFF,
    Handgelenk, Weltraum, Fußpunkt der Figur als Ursprung); Beine, Becken und Boden wie beim Gehen (der Takt im Spiel richtet sich nach dem
    Tempo des Karrens, `tempo_m_s` im Bericht ist das Tempo bei Takt 1)."""
    roh_poses = haltungen_lesen(rig, gehen)
    roh, _ = aktion_schreiben(rig, "ZiehenRoh", roh_poses)
    P.haltung_beugen(rig, roh, ZIEHEN_BEUGE)
    ziele = lambda i, n, huefte: {"Left": Vector((ZIEHEN_GRIFF["seitlich"], -ZIEHEN_GRIFF["vor"], ZIEHEN_GRIFF["hoch"])),
                                  "Right": Vector((-ZIEHEN_GRIFF["seitlich"], -ZIEHEN_GRIFF["vor"], ZIEHEN_GRIFF["hoch"]))}
    erg = arme_auf_ziele(rig, roh, "Ziehen", ziele, ZIEHEN_PALME)
    aktion_ersetzen(rig, "ZiehenRoh")
    return erg


def sitzen_bock_bauen(rig, sitzen):
    """SitzenBock aus „Sitzen“: Körper wie dort (Atmen), beide Hände vor dem Bauch an den Zügeln; die Hände schwanken ein wenig mit
    (Schleife mit der Länge des Clips), als hielten sie leichten Zug."""
    def ziele(i, n, huefte):
        schwank = math.sin(2 * math.pi * i / n) * ZUEGEL["schwank"]
        return {seite: huefte + Vector((v * ZUEGEL["seitlich"], -ZUEGEL["vor"] + schwank, ZUEGEL["hoch"] + schwank))
                for seite, v in (("Left", 1.0), ("Right", -1.0))}
    return arme_auf_ziele(rig, sitzen, "SitzenBock", ziele, ZUEGEL_PALME)


# ─────────────────────────────────────────────────────────────────────────────
#  4. Vorschau (Workbench, Standbilder bei 0 / 25 / 50 / 75 % des Clips)
# ─────────────────────────────────────────────────────────────────────────────


def vorschau_vorbereiten(rig, netze, cfg):
    schuh = "Fuss_barfuss" if cfg["datei"].endswith(("_ork.glb", "_oger.glb")) else "Fuss_halb"
    sicht = {f"Nah_{cfg['varianten'][0]}", schuh, "Zub_stab"}
    if not cfg["datei"].endswith(("_ork.glb", "_oger.glb")):
        sicht.add("Zub_stroh")
    farben = {"Nah": (0.78, 0.62, 0.46, 1), "Fuss": (0.25, 0.17, 0.10, 1), "Zub_stab": (0.40, 0.25, 0.10, 1), "Zub_stroh": (0.85, 0.72, 0.35, 1)}
    for name, o in netze.items():
        o.hide_render = name not in sicht
        o.hide_viewport = name not in sicht
        for k, f in farben.items():
            if name.startswith(k):
                o.color = f
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "OBJECT"
    sh.show_cavity = True
    sh.show_object_outline = True
    sc.render.resolution_x, sc.render.resolution_y = 420, 600
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    if sc.world is None:
        sc.world = bpy.data.worlds.new("w")
    sc.world.color = (0.82, 0.80, 0.74)
    import bmesh
    boden = bpy.data.objects.new("Boden", bpy.data.meshes.new("Boden"))
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=10, y_segments=10, size=2.0)
    bm.to_mesh(boden.data)
    bm.free()
    bpy.context.scene.collection.objects.link(boden)
    boden.color = (0.64, 0.60, 0.52, 1)


def kamera_setzen(sc, ort, ziel, breite):
    cam = sc.camera
    if cam is None:
        cam = bpy.data.objects.new("Kam", bpy.data.cameras.new("Kam"))
        bpy.context.scene.collection.objects.link(cam)
        sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = breite
    cam.location = ort
    cam.rotation_euler = (Vector(ziel) - Vector(ort)).normalized().to_track_quat("-Z", "Y").to_euler()


def reihe_rendern(rig, act, bilder, name, ordner, stab=False):
    """Zwei Reihen (Seite von links der Figur, Vorderansicht) mit je vier Bildern bei 0, 25, 50, 75 % des Clips; Ergebnis ein PNG."""
    import numpy as np
    sc = bpy.context.scene
    for o in bpy.data.objects:
        if o.name == "Zub_stab":
            o.hide_render = not stab
    P.aktion_setzen(rig, act)
    a = int(act.frame_range[0])
    bildliste = [int(round(bilder * p)) for p in (0.0, 0.25, 0.5, 0.75)]
    zeilen = []
    for ort in ((6.0, 0.0, 0.95), (0.0, -6.0, 0.95)):
        kamera_setzen(sc, ort, (0, 0, 0.88), 2.1)
        teile = []
        for b in bildliste:
            sc.frame_set(a + b)
            pfad = os.path.join(tempfile.gettempdir(), "rg_clip_tmp.png")
            sc.render.filepath = pfad
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(pfad, check_existing=False)
            w, h = img.size
            teile.append(np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4))
            bpy.data.images.remove(img)
        zeilen.append(np.concatenate(teile, axis=1))
    gesamt = np.concatenate(zeilen, axis=0)            # Pixelzeile 0 ist unten: erste Reihe (Seite) steht unten, Vorderansicht oben
    h, w = gesamt.shape[:2]
    img = bpy.data.images.new("reihe", w, h, alpha=False)
    img.pixels.foreach_set(gesamt.reshape(-1))
    os.makedirs(ordner, exist_ok=True)
    img.filepath_raw = os.path.join(ordner, f"{name}.png")
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    rig.animation_data.action = None


# ─────────────────────────────────────────────────────────────────────────────
#  5. Ablauf
# ─────────────────────────────────────────────────────────────────────────────


def art_bearbeiten(art):
    cfg = P.FIGUREN[art]
    quelle_glb = os.path.join(OPT.get("ordner", os.path.join(REPO, "game", "assets", "modelle", "figuren", "probe")), cfg["datei"])
    aus_glb = os.path.join(OPT.get("aus", os.path.dirname(quelle_glb)), cfg["datei"])
    szene_leeren()
    rig, netze = figur_laden(quelle_glb)
    alt = {n: next(a for a in bpy.data.actions if a.name == n) for n in ("Stehen", "Gehen", "Sitzen")}
    bericht = {"datei": quelle_glb}
    bilder_ordner = OPT.get("bilder")
    if bilder_ordner:
        vorschau_vorbereiten(rig, netze, cfg)

    clips = {}
    act, bilder = ziehen_bauen(rig, alt["Gehen"])
    clips["Ziehen"] = (act, bilder)
    bericht["Ziehen"] = {"bilder": bilder, "sekunden": round(bilder / FPS, 3)}
    # Handlage beim Ziehen (Figurenraum: Fußpunkt Boden, Blick −Y): Mitte der Hand je Bild, Mittel und Spanne, für Anker_Zug_L/R des Karrens
    P.aktion_setzen(rig, act)
    lagen = {"Left": [], "Right": []}
    for f in range(int(act.frame_range[0]), int(act.frame_range[1]) + 1):
        bpy.context.scene.frame_set(f)
        for s in lagen:
            h = rig.pose.bones[f"{s}Hand"]
            lagen[s].append(rig.matrix_world @ ((h.head + h.tail) / 2))
    bericht["Ziehen"]["hand_mittel_m"] = {s: [round(sum(v[i] for v in l) / len(l), 3) for i in range(3)] for s, l in lagen.items()}
    bericht["Ziehen"]["tempo_m_s"] = round(P.tempo_messen(rig, act, bilder), 3)
    bericht["Ziehen"]["hand_spanne_m"] = {s: [round(max(v[i] for v in l) - min(v[i] for v in l), 3) for i in range(3)] for s, l in lagen.items()}
    rig.animation_data.action = None

    clips["Nebenhergehen"] = nebenhergehen_bauen(rig, alt["Gehen"], alt["Stehen"])
    bericht["Nebenhergehen"] = {"bilder": clips["Nebenhergehen"][1]}
    clips["SitzenBock"] = sitzen_bock_bauen(rig, alt["Sitzen"])
    bericht["SitzenBock"] = {"bilder": clips["SitzenBock"][1]}

    if bilder_ordner:
        griff = bericht["Ziehen"]["hand_mittel_m"]       # Vorschau: Stange des Griffs durch beide Hände (nur bei „Ziehen“ sichtbar)
        import bmesh
        stange = bpy.data.objects.new("Griffstange", bpy.data.meshes.new("Griffstange"))
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.025, radius2=0.025, depth=0.8, matrix=Matrix.Rotation(math.pi / 2, 4, "Y"))
        bm.to_mesh(stange.data)
        bm.free()
        stange.location = (0.0, griff["Left"][1], griff["Left"][2])
        stange.color = (0.4, 0.25, 0.1, 1)
        bpy.context.scene.collection.objects.link(stange)
        for name, (act, bilder) in clips.items():
            stange.hide_render = name != "Ziehen"
            reihe_rendern(rig, act, bilder, f"{art}_{name.lower()}", bilder_ordner, stab=name == "Nebenhergehen")

    # Hilfsdatei: nur Skelett mit den neuen Clips (beim Prüfen auch „Gehen“ und „Stehen“)
    behalten = set(NEUE_CLIPS) | ({"Gehen", "Stehen"} if OPT.get("pruefen") == "1" else set())
    for spur in list(rig.animation_data.nla_tracks):
        if spur.name not in behalten:
            rig.animation_data.nla_tracks.remove(spur)
    rig.animation_data.action = None
    for o in bpy.context.scene.objects:
        o.select_set(o is rig)
    hilfe = os.path.join(tempfile.gettempdir(), f"rg_clips_{art}.glb")
    bpy.ops.export_scene.gltf(
        filepath=hilfe, export_format="GLB", use_selection=True, export_yup=True, export_apply=False,
        export_animations=True, export_animation_mode="NLA_TRACKS", export_anim_slide_to_zero=True, export_morph=False,
        export_skins=True, export_lights=False, export_cameras=False, export_image_format="NONE")
    if OPT.get("pruefen") == "1":
        bericht["rundreise"] = animationen_vergleichen(quelle_glb, hilfe, ["Gehen", "Stehen"])
    j, b = glb_lesen(quelle_glb)
    animationen_uebertragen(hilfe, j, b, NEUE_CLIPS)
    os.makedirs(os.path.dirname(aus_glb), exist_ok=True)
    glb_schreiben(aus_glb, j, b)
    bericht["geschrieben"] = aus_glb
    bericht["animationen"] = [a["name"] for a in j["animations"]]
    bericht["dauer_s"] = {a["name"]: j["accessors"][a["samplers"][0]["input"]].get("max") for a in j["animations"]}
    return bericht


def hauptteil():
    for art in P.FIGUREN:
        if art in OPT.get("nur", ",".join(P.FIGUREN)).split(","):
            BERICHT[art] = art_bearbeiten(art)
    if "bericht" in OPT:
        with open(OPT["bericht"], "w", encoding="utf-8") as f:
            json.dump(BERICHT, f, indent=1, ensure_ascii=False, default=str)
    print("BERICHT", json.dumps(BERICHT, indent=1, ensure_ascii=False, default=str))


if __name__ == "__main__":
    hauptteil()
