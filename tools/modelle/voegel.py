"""Vögel der Stadt: Taube und Spatz mit Skelett und Bewegungen.

    blender -b --python tools/modelle/voegel.py        oder
    python tools/modelle/voegel.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/vogel_taube.glb und vogel_spatz.glb und docs/modelle/voegel.png.

Inhalt je Datei (Blender: Z oben, Blick nach -Y; im Spiel Y oben, Blick nach +Z; Ursprung zwischen den Füßen):
    Skelett         Koerper, Kopf, Fluegel_L, Fluegel_R, Schwanz – jeder Teil starr an einem Knochen
    Vogel           Körper, Kopf mit Schnabel und Augen, Flügel mit Schwungfedern und Binden, Fächerschwanz, Füße
    Bewegungen      Fliegen (Flügelschlag), Gleiten (Flügel ausgebreitet), Sitzen (Kopf dreht sich),
                    Picken (Körper kippt, Kopf nickt zum Boden), Putzen (Kopf ins Gefieder),
                    Gehen (Taube: Kopfnicken im Schritt; Spatz: Hüpfen)
                    – Schleifen am Ort; das Spiel bewegt den Vogel und hebt ihn beim Fliegen vom Boden.
Größe: etwa 1,6-fach (Taube 0,5 m, Spatz 0,26 m lang), damit man sie aus der Spielkamera noch erkennt.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import figuren as fg  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Vector  # noqa: E402

bs.FARBEN.update({
    "Taube": (0.56, 0.58, 0.62),
    "Taube dunkel": (0.36, 0.38, 0.43),
    "Taube Hals": (0.30, 0.46, 0.40),
    "Spatz": (0.52, 0.38, 0.24),
    "Spatz hell": (0.74, 0.68, 0.58),
    "Spatz dunkel": (0.20, 0.15, 0.12),
    "Spatz Kappe": (0.47, 0.47, 0.46),
    "Taube Auge": (0.86, 0.46, 0.18),
    "Wachshaut": (0.88, 0.87, 0.84),
    "Schnabel": (0.82, 0.62, 0.30),
    "Fuss": (0.78, 0.42, 0.40),
})

FPS = 24
ARTEN = {
    "taube": dict(g=1.6, laenge=0.31, r=0.075, kopf=0.04, fluegel=0.3, farbe="Taube", dunkel="Taube dunkel",
                  hals="Taube Hals", bauch="Taube", bein=0.05),
    "spatz": dict(g=1.6, laenge=0.15, r=0.042, kopf=0.026, fluegel=0.12, farbe="Spatz", dunkel="Spatz dunkel",
                  hals="Spatz dunkel", bauch="Spatz hell", bein=0.03),
}
BEWEGUNGEN = {"Fliegen": 0.35, "Gleiten": 1.5, "Sitzen": 3.0, "Picken": 1.0, "Putzen": 2.0, "Gehen": 0.6}


def masse(art):
    d = dict(ARTEN[art])
    g = d["g"]
    for k in ("laenge", "r", "kopf", "fluegel", "bein"):
        d[k] *= g
    d["z"] = d["bein"] + d["r"]                    # Höhe der Körpermitte
    return d


def skelett(art):
    import bpy
    d = masse(art)
    arm = bpy.data.armatures.new("Skelett")
    ob = bpy.data.objects.new("Skelett", arm)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    z, l = d["z"], d["laenge"]

    def knochen(name, kopf, schwanz, eltern=None):
        b = arm.edit_bones.new(name)
        b.head, b.tail = kopf, schwanz
        b.roll = 0.0
        if eltern:
            b.parent = arm.edit_bones[eltern]

    knochen("Koerper", (0, 0, z), (0, -l * 0.3, z))
    knochen("Kopf", (0, -l * 0.3, z + d["r"] * 0.6), (0, -l * 0.3, z + d["r"] * 0.6 + d["kopf"] * 2), "Koerper")
    for s, seite in ((1, "L"), (-1, "R")):
        knochen(f"Fluegel_{seite}", (s * d["r"] * 0.7, -l * 0.1, z + d["r"] * 0.4),
                (s * (d["r"] * 0.7 + d["fluegel"]), -l * 0.1, z + d["r"] * 0.4), "Koerper")
    knochen("Schwanz", (0, l * 0.3, z + d["r"] * 0.1), (0, l * 0.6, z + d["r"] * 0.2), "Koerper")
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in ob.pose.bones:
        pb.rotation_mode = "XYZ"
    return ob


def vogel(art, st, ske):
    d = masse(art)
    t = fg.Teile("Vogel")
    z, l, r = d["z"], d["laenge"], d["r"]
    with t.an("Koerper") as n:
        # Rumpf: Tropfen von der Brust (vorn, -Y) zum Schwanzansatz
        nt.ast(n, st, (0, -l * 0.32, z + r * 0.15), (0, -l * 0.18, z + r * 0.1), r * 0.55, r * 0.95, stoff=d["bauch"], n=7)
        nt.ast(n, st, (0, -l * 0.18, z + r * 0.1), (0, l * 0.15, z), r * 0.95, r * 0.75, stoff=d["farbe"], n=7)
        nt.ast(n, st, (0, l * 0.15, z), (0, l * 0.32, z + r * 0.1), r * 0.75, r * 0.3, stoff=d["dunkel"], n=7)
        nt.ast(n, st, (0, -l * 0.28, z + r * 0.35), (0, -l * 0.3, z + r * 0.75), r * 0.5, r * 0.4, stoff=d["hals"], n=7)
        for s in (-1, 1):                                              # Füße
            n.stab((s * r * 0.35, -l * 0.02, z - r * 0.6), (s * r * 0.35, -l * 0.04, 0.01), r * 0.12, r * 0.12, st["Fuss"])
            n.quader((s * r * 0.35, -l * 0.08, 0.008), (r * 0.25, r * 0.6, 0.016), st["Fuss"])
    with t.an("Kopf") as n:
        km = Vector((0, -l * 0.31, z + r * 0.75 + d["kopf"]))
        nt.krone(n, st, km, d["kopf"], d["kopf"], saat=1,
                 stoffe=("Spatz Kappe", d["farbe"]) if art == "spatz" else (d["farbe"], d["farbe"]), n=6, flach_unten=0.6)
        nt.ast(n, st, km + Vector((0, -d["kopf"] * 0.85, -d["kopf"] * 0.1)),
               km + Vector((0, -d["kopf"] * 1.7, -d["kopf"] * 0.3)), d["kopf"] * 0.3, d["kopf"] * 0.05,
               stoff="Schnabel" if art == "spatz" else "Taube dunkel", n=4)
        for s in (-1, 1):
            # Auge: Taube mit orangem Ring um die dunkle Pupille, Spatz nur dunkel
            if art == "taube":
                n.quader(km + Vector((s * d["kopf"] * 0.72, -d["kopf"] * 0.35, d["kopf"] * 0.2)),
                         (d["kopf"] * 0.2, d["kopf"] * 0.34, d["kopf"] * 0.34), st["Taube Auge"])
                n.quader(km + Vector((s * d["kopf"] * 0.8, -d["kopf"] * 0.35, d["kopf"] * 0.2)),
                         (d["kopf"] * 0.2, d["kopf"] * 0.16, d["kopf"] * 0.16), st["Auge"])
            else:
                n.quader(km + Vector((s * d["kopf"] * 0.75, -d["kopf"] * 0.35, d["kopf"] * 0.2)),
                         (d["kopf"] * 0.2, d["kopf"] * 0.25, d["kopf"] * 0.25), st["Auge"])
                # helle Wange unter dem Auge
                n.quader(km + Vector((s * d["kopf"] * 0.74, -d["kopf"] * 0.2, -d["kopf"] * 0.35)),
                         (d["kopf"] * 0.2, d["kopf"] * 0.5, d["kopf"] * 0.35), st["Spatz hell"])
        if art == "spatz":                                             # Kehlfleck
            n.quader(km + Vector((0, -d["kopf"] * 0.6, -d["kopf"] * 0.95)), (d["kopf"] * 0.8, d["kopf"] * 0.3, d["kopf"] * 0.6),
                     st["Spatz dunkel"])
        else:                                                          # Wachshaut am Schnabelansatz
            n.quader(km + Vector((0, -d["kopf"] * 1.0, 0)), (d["kopf"] * 0.4, d["kopf"] * 0.3, d["kopf"] * 0.25), st["Wachshaut"])
    for s, seite in ((1, "L"), (-1, "R")):
        with t.an(f"Fluegel_{seite}") as n:
            x0 = s * r * 0.7
            x1 = s * (r * 0.7 + d["fluegel"])
            y0, zf = -l * 0.1, z + r * 0.4

            def f(a, b, h=0.0):                                        # a: 0 Schulter … 1 Spitze, b: längs (× Länge)
                return Vector((x0 + (x1 - x0) * a, y0 + l * b, zf + h))
            ecken = [f(0, -0.12), f(0.6, -0.08), f(0.85, 0.08), f(0.7, 0.26), f(0, 0.28)]
            n.vieleck_nach(ecken, (0, 0, 1), st[d["farbe"]])
            n.vieleck_nach([e - Vector((0, 0, 0.006)) for e in ecken], (0, 0, -1), st[d["dunkel"]])
            # Schwungfedern: drei dunkle Federn ragen über den Rand hinaus und geben dem Flügel die gefingerte Kante
            for k in range(3):
                b0 = 0.02 + 0.07 * k
                spitze, tb = 1.0 - 0.06 * k, 0.07 + 0.09 * k
                feder = [f(0.55, b0 - 0.05), f(spitze, tb - 0.035), f(spitze - 0.05, tb + 0.04), f(0.55, b0 + 0.05)]
                n.vieleck_nach([e + Vector((0, 0, 0.002 + 0.001 * k)) for e in feder], (0, 0, 1), st[d["dunkel"]])
                n.vieleck_nach([e - Vector((0, 0, 0.008 + 0.001 * k)) for e in feder], (0, 0, -1), st[d["dunkel"]])
            # Binden quer über den inneren Flügel: Taube zwei dunkle, Spatz eine helle
            binden = ((0.08, 0.14), (0.18, 0.24)) if art == "taube" else ((0.02, 0.06),)
            for b0, b1 in binden:
                band = [f(0.05, b0, 0.0015), f(0.45, b0 - 0.02, 0.0015), f(0.45, b1 - 0.02, 0.0015), f(0.05, b1, 0.0015)]
                n.vieleck_nach(band, (0, 0, 1), st["Taube dunkel" if art == "taube" else "Spatz hell"])
    with t.an("Schwanz") as n:
        # Fächer mit dunkler Endbinde
        def q(x, y, h=0.0):
            return Vector((x * r, y * l, z + r * (0.12 + (y - 0.28) * 0.3) + h))
        ecken = [q(-0.35, 0.28), q(0.35, 0.28), q(0.6, 0.6), q(0, 0.66), q(-0.6, 0.6)]
        schwanz = d["farbe"] if art == "taube" else "Spatz dunkel"
        n.vieleck_nach(ecken, (0, 0, 1), st[schwanz])
        n.vieleck_nach([e - Vector((0, 0, 0.006)) for e in ecken], (0, 0, -1), st[schwanz])
        if art == "taube":
            n.vieleck_nach([q(-0.56, 0.54, 0.002), q(0.56, 0.54, 0.002), q(0.6, 0.6, 0.002), q(0, 0.66, 0.002), q(-0.6, 0.6, 0.002)],
                           (0, 0, 1), st[d["dunkel"]])
    return t.objekt(ske)


def haltung(art, name, phase):
    """{Knochen: (x, y, z) Grad}, Höhe des Körpers über der Ruhe (m). Flügel (Knochen zeigen nach außen): positives X
    hebt die Spitze des linken Flügels, negatives die des rechten (Flügelschlag), Z schwenkt sie waagrecht – links positiv, rechts negativ nach hinten."""
    s = math.sin(2 * math.pi * phase)
    c = math.cos(2 * math.pi * phase)
    r = {}
    hoch = 0.0
    eingeklappt = 78                                                   # Flügel am Körper
    if name == "Fliegen":
        schlag = 55 * s
        r["Fluegel_L"] = (schlag, 0, 8)
        r["Fluegel_R"] = (-schlag, 0, -8)
        r["Koerper"] = (-4 * c, 0, 0)
        r["Schwanz"] = (6 * s, 0, 0)
        hoch = 0.0
    elif name == "Gleiten":
        r["Fluegel_L"] = (8 + 4 * s, 0, 4)
        r["Fluegel_R"] = (-8 - 4 * s, 0, -4)
        r["Koerper"] = (0, 0, 0)
        r["Kopf"] = (0, 0, 6 * s)
    else:
        r["Fluegel_L"] = (-6, 0, eingeklappt)
        r["Fluegel_R"] = (6, 0, -eingeklappt)
        if name == "Sitzen":
            dreh = 35 * math.sin(2 * math.pi * phase) * (abs(math.sin(2 * math.pi * phase * 2)) > 0.4)
            r["Kopf"] = (0, 0, dreh)
            r["Schwanz"] = (3 * s, 0, 0)
        elif name == "Picken":
            nick = max(0.0, math.sin(2 * math.pi * phase * 2)) ** 2
            r["Koerper"] = (-28 * nick - 8, 0, 0)
            r["Kopf"] = (-45 * nick, 0, 0)
            r["Schwanz"] = (14 * nick, 0, 0)
        elif name == "Putzen":
            p = max(0.0, s)
            r["Kopf"] = (-30 * p, 0, 110 * p)
            r["Fluegel_L"] = (10 * p, 0, eingeklappt - 18 * p)
        elif name == "Gehen":
            if art == "taube":
                r["Kopf"] = (12 * s, 0, 0)
                r["Koerper"] = (0, 2 * c, 0)
            else:
                hoch = 0.035 * max(0.0, s)
                r["Schwanz"] = (-10 * max(0.0, s), 0, 0)
    return {k: tuple(math.radians(v) for v in w) for k, w in r.items()}, hoch


def pose_setzen(ob, pose, hoch):
    for pb in ob.pose.bones:
        pb.rotation_euler = pose.get(pb.name, (0.0, 0.0, 0.0))
        pb.location = (0.0, 0.0, 0.0)
    ob.pose.bones["Koerper"].location = (0.0, 0.0, -hoch)        # Knochen zeigt nach -Y: lokales -Z ist oben
    return ob


def bewegungen_anlegen(ob, art):
    import bpy
    ob.animation_data_create()
    for name, dauer in BEWEGUNGEN.items():
        bilder = max(4, int(round(dauer * FPS)))
        act = bpy.data.actions.new(name)
        ob.animation_data.action = act
        for f in range(0, bilder + 1):
            pose_setzen(ob, *haltung(art, name, (f % bilder) / bilder))
            for pb in ob.pose.bones:
                pb.keyframe_insert("rotation_euler", frame=f + 1)
            ob.pose.bones["Koerper"].keyframe_insert("location", frame=f + 1)
        spur = ob.animation_data.nla_tracks.new()
        spur.name = name
        spur.strips.new(name, 1, act)
        ob.animation_data.action = None
    pose_setzen(ob, *haltung(art, "Sitzen", 0.0))


def main():
    schnell = "--schnell" in sys.argv
    for art in ARTEN:
        bs.neue_szene()
        st = bs.Stoffe()
        ske = skelett(art)
        objekte = [ske, vogel(art, st, ske)]
        bewegungen_anlegen(ske, art)
        objekte[1].data.calc_loop_triangles()
        print(f"vogel_{art}: {len(objekte[1].data.loop_triangles)} Dreiecke")
        print("geschrieben:", fg.exportieren("vogel_" + art, objekte))
    if "--ohne-bilder" not in sys.argv:
        vorschau(schnell)


def vorschau(schnell):
    """Je Vogel eine Reihe: Sitzen, Picken, Putzen, Gehen, Fliegen (oben, unten), Gleiten."""
    bs.neue_szene()
    st = bs.Stoffe()
    sichtbar = []
    reihe = [("Sitzen", 0.25), ("Picken", 0.25), ("Putzen", 0.25), ("Gehen", 0.25), ("Fliegen", 0.25), ("Fliegen", 0.75),
             ("Gleiten", 0.0)]
    for z, art in enumerate(ARTEN):
        for k, (name, phase) in enumerate(reihe):
            ske = skelett(art)
            o = vogel(art, st, ske)
            sx, sy = (k - 3) * 0.75, (0.5 - z) * 1.0
            ske.location = Vector((sx * 0.7071 - sy * 0.7071, sx * 0.7071 + sy * 0.7071, 0.3 if name in ("Fliegen", "Gleiten") else 0))
            ske.rotation_euler = (0, 0, math.radians(-90))
            pose_setzen(ske, *haltung(art, name, phase))
            sichtbar.append(o)
    bs.vorschau_buehne(None, ziel_hoehe=0.15, ausschnitt=4.6, schnell=schnell)
    print(bs.rendern("voegel", lambda o: o in sichtbar))


if __name__ == "__main__":
    main()
