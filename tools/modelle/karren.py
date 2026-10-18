"""Karren mit Zugtieren und Bewegungen: Handkarren, Ochsenkarren, Kriegskarren.

    blender -b --python tools/modelle/karren.py        oder
    python tools/modelle/karren.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/karren_hand.glb, karren_ochse.glb, karren_krieg.glb und Bilder nach docs/modelle/.

Inhalt je Datei (Blender: Z oben, Fahrtrichtung -Y; im Spiel Y oben, Fahrtrichtung +Z; Ursprung unter der Radachse
des Wagens auf dem Boden):
    Skelett         Wurzel (Wagenkasten, wippt), je Radachse ein Knochen (dreht um die Achse), bei Zugtieren je Tier
                    Rumpf, Kopf, vier Beine, Schwanz. Jeder Teil hängt starr an einem Knochen.
    Karren          Wagen mit Rädern und Deichsel bzw. Holmen
    Ochse / Pferd1, Pferd2   Zugtiere (Ochsenkarren / Kriegskarren)
    Ladung1–3       Kisten, Säcke, Fässer – so viele sichtbar, wie Sorten geladen sind (Handkarren 1, Ochsenkarren 2,
                    Kriegskarren 3); standardmäßig aus
    Plane           Plane über der Ladung in der Hausfarbe (Besitzer), mit der Ladung an- und auszuschalten
    Anker_Zieher (Handkarren: zwischen den Holmen), Anker_Treiber (neben dem Ochsen), Anker_Kutscher (auf dem Bock)
                    – dort stellt das Spiel die Passanten-Figur hin (Bewegung „Ziehen“, „Gehen“ bzw. „Kutschieren“)
    Bewegungen      Fahren (Räder drehen, Tiere gehen, Wagen wippt) und Stehen (Tiere nicken, Schwanz schlägt)
Die Bewegungen laufen am Ort; das Spiel schiebt den Karren. Wie weit eine Schleife „Fahren“ fährt, steht in WEG.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import figuren as fg  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Fell": (0.46, 0.32, 0.20),            # Ochse
    "Fell hell": (0.78, 0.70, 0.56),
    "Pferd": (0.36, 0.24, 0.15),
    "Pferd dunkel": (0.16, 0.12, 0.10),
    "Horn": (0.86, 0.80, 0.66),
    "Sack": (0.70, 0.62, 0.46),
    "Geflecht": (0.56, 0.44, 0.27),
    "Hausfarbe": (0.18, 0.30, 0.58),
})

FPS = 24
DAUER = {"Fahren": 1.2, "Stehen": 3.0}
ARTEN = {
    # Rad-Halbmesser, Achsen (y), Spur (halbe), Bett (y0, y1, Höhe), Sorten, Tiere
    "hand": dict(r=0.45, achsen=(0.0,), spur=0.55, bett=(-0.75, 0.75, 0.62), breite=0.5, sorten=1, tiere=()),
    "ochse": dict(r=0.72, achsen=(0.3,), spur=0.85, bett=(-0.9, 1.5, 0.95), breite=0.75, sorten=2, tiere=("Ochse",)),
    "krieg": dict(r=0.6, achsen=(-1.1, 1.3), spur=0.85, bett=(-1.6, 1.8, 0.95), breite=0.8, sorten=3,
                  tiere=("Pferd1", "Pferd2")),
}
# Weg je Schleife „Fahren“ (Meter): die Räder drehen genau einmal herum
WEG = {art: 2 * math.pi * d["r"] for art, d in ARTEN.items()}


# ─────────────────────────────────────────────────────────────────────────────
#  Skelett
# ─────────────────────────────────────────────────────────────────────────────

def tier_lage(art, tier):
    """Mitte des Rumpfs eines Tiers (x, y) und Rumpfhöhe."""
    if tier == "Ochse":
        return Vector((0, -3.0, 1.15))
    return Vector((-0.55 if tier == "Pferd1" else 0.55, -3.6, 1.25))


def tier_masse(tier):
    if tier == "Ochse":
        return dict(lang=1.0, r=0.42, bein=1.0, hals=0.35, kopf=0.32)
    return dict(lang=0.9, r=0.34, bein=1.1, hals=0.55, kopf=0.36)


def skelett(art):
    import bpy
    d = ARTEN[art]
    arm = bpy.data.armatures.new("Skelett")
    ob = bpy.data.objects.new("Skelett", arm)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")

    def knochen(name, kopf, schwanz, eltern=None):
        b = arm.edit_bones.new(name)
        b.head, b.tail = kopf, schwanz
        if eltern:
            b.parent = arm.edit_bones[eltern]

    knochen("Wurzel", (0, 0, d["r"]), (0, 0, d["r"] + 0.3))
    for i, y in enumerate(d["achsen"]):
        knochen(f"Achse{i + 1}", (-d["spur"] - 0.2, y, d["r"]), (d["spur"] + 0.2, y, d["r"]), "Wurzel")
    for tier in d["tiere"]:
        m, t = tier_lage(art, tier), tier_masse(tier)
        knochen(f"{tier}_Rumpf", tuple(m + Vector((0, t["lang"], 0))), tuple(m - Vector((0, t["lang"], 0))))
        hals = m - Vector((0, t["lang"] * 0.9, -0.15))
        knochen(f"{tier}_Kopf", tuple(hals), tuple(hals + Vector((0, -t["hals"], t["hals"] * 0.6))), f"{tier}_Rumpf")
        for vorn, sy in (("V", -1), ("H", 1)):
            for seite, sx in (("L", 1), ("R", -1)):
                fuss = m + Vector((sx * t["r"] * 0.6, sy * t["lang"] * 0.78, -t["r"] * 0.5))
                knochen(f"{tier}_Bein{vorn}{seite}", tuple(fuss), (fuss.x, fuss.y, 0.04), f"{tier}_Rumpf")
        schwanz = m + Vector((0, t["lang"], 0.1))
        knochen(f"{tier}_Schwanz", tuple(schwanz), tuple(schwanz + Vector((0, 0.1, -0.7))), f"{tier}_Rumpf")
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in ob.pose.bones:
        pb.rotation_mode = "XYZ"
    return ob


# ─────────────────────────────────────────────────────────────────────────────
#  Teile
# ─────────────────────────────────────────────────────────────────────────────

def rad(n, st, x, y, r, dicke=0.09, speichen=8):
    """Speichenrad in der YZ-Ebene: Felge aus Segmenten, Nabe, Speichen."""
    seg = 12
    for k in range(seg):
        w0, w1 = 2 * math.pi * k / seg, 2 * math.pi * (k + 1) / seg
        a = Vector((x, y + r * 0.92 * math.cos(w0), r + r * 0.92 * math.sin(w0)))
        b = Vector((x, y + r * 0.92 * math.cos(w1), r + r * 0.92 * math.sin(w1)))
        n.stab(a, b, r * 0.14, dicke, st["Holz alt"], quer=(1, 0, 0), mehr=0.02)
    nt.ast(n, st, (x - dicke, y, r), (x + dicke, y, r), r * 0.16, r * 0.16, stoff="Eisen", n=6)
    for k in range(speichen):
        w = 2 * math.pi * (k + 0.5) / speichen
        n.stab((x + 0.012 * (k % 2), y, r), (x + 0.012 * (k % 2), y + r * 0.85 * math.cos(w), r + r * 0.85 * math.sin(w)),
               0.04, 0.035, st["Holz"], quer=(1, 0, 0))


def karren_bauen(art, st, ske):
    d = ARTEN[art]
    t = fg.Teile("Karren")
    y0, y1, h = d["bett"]
    b = d["breite"]
    with t.an("Wurzel") as n:
        n.quader((0, (y0 + y1) / 2, h), ((b + 0.05) * 2, y1 - y0, 0.08), st["Holz alt"])
        hoch = 0.35 if art == "hand" else 0.5
        for sx in (-1, 1):                                          # Seitenbretter
            n.quader((sx * b, (y0 + y1) / 2, h + hoch / 2), (0.06, y1 - y0, hoch), st["Holz"])
        for sy, y in ((-1, y0), (1, y1)):
            n.quader((0, y, h + hoch / 2), (b * 2 - 0.06, 0.06, hoch), st["Holz"])
        if art == "krieg":                                          # Wehrwagen: hohe Bohlenwände mit Eisenbändern
            for sx in (-1, 1):
                n.quader((sx * (b + 0.06), (y0 + y1) / 2, h + 0.75), (0.08, y1 - y0, 0.9), st["Holz alt"])
                for k in range(4):
                    yy = y0 + 0.3 + k * (y1 - y0 - 0.6) / 3
                    n.quader((sx * (b + 0.11), yy, h + 0.75), (0.02, 0.1, 0.92), st["Eisen"])
                    n.quader((sx * (b + 0.105), yy + 0.4, h + 0.95), (0.012, 0.06, 0.22), st["Loch"])   # Scharten
            n.quader((0, y1 + 0.05, h + 0.75), (b * 2 + 0.24, 0.08, 0.9), st["Holz alt"])
            # Bock vorn
            n.quader((0, y0 - 0.25, h + 0.45), (b * 2 - 0.2, 0.5, 0.08), st["Holz"])
            n.quader((0, y0 - 0.48, h + 0.2), (b * 2 - 0.2, 0.06, 0.5), st["Holz"])
        # Deichsel bzw. Holme nach vorn
        if art == "hand":
            for sx in (-1, 1):
                n.stab((sx * (b - 0.1), y0 + 0.1, h + 0.05), (sx * (b - 0.05), y0 - 1.25, 0.88), 0.07, 0.07, st["Holz"])
            n.stab((-(b - 0.05), y0 - 1.15, 0.86), (b - 0.05, y0 - 1.15, 0.86), 0.05, 0.05, st["Holz"])
            n.quader((0, y1 - 0.15, (h - 0.04) / 2), (0.06, 0.06, h - 0.04), st["Holz"])           # Stütze hinten
        else:
            vorn = tier_lage(art, d["tiere"][0]).y - 0.4
            n.stab((0, y0, h - 0.1), (0, vorn, 1.0), 0.1, 0.1, st["Holz"])
            for tier in d["tiere"]:                                  # Joch bzw. Ortscheit
                m = tier_lage(art, tier)
                n.stab((m.x - 0.45, m.y - 0.75, m.z + 0.35), (m.x + 0.45, m.y - 0.75, m.z + 0.35), 0.08, 0.08, st["Holz"])
                for sx in (-1, 1):
                    n.stab((m.x + sx * 0.38, m.y - 0.75, m.z + 0.1), (m.x + sx * 0.4, y0, h), 0.03, 0.02, st["Leder"])
    for i, y in enumerate(d["achsen"]):
        with t.an(f"Achse{i + 1}") as n:
            for sx in (-1, 1):
                rad(n, st, sx * d["spur"], y, d["r"])
            n.stab((-d["spur"], y, d["r"]), (d["spur"], y, d["r"]), 0.07, 0.07, st["Holz"])
    return t.objekt(ske)


def tier_bauen(art, tier, st, ske):
    t = fg.Teile(tier)
    m, ms = tier_lage(art, tier), tier_masse(tier)
    ochse = tier == "Ochse"
    fell, fell2 = ("Fell", "Fell hell") if ochse else ("Pferd", "Pferd dunkel")
    lang, r = ms["lang"], ms["r"]
    with t.an(f"{tier}_Rumpf") as n:
        nt.ast(n, st, m + Vector((0, lang, 0.02)), m - Vector((0, lang * 0.95, -0.05)), r, r * 1.08, stoff=fell, n=8)
        nt.ast(n, st, m + Vector((0, lang, 0.02)), m + Vector((0, lang + 0.1, 0.0)), r * 0.98, r * 0.6, stoff=fell, n=8)
        if ochse:                                                   # Widerrist und Bauch heller
            nt.ast(n, st, m - Vector((0, lang * 0.5, -r * 0.7)), m - Vector((0, lang * 0.9, -r * 0.75)), r * 0.35, r * 0.3,
                   stoff=fell, n=6)
        else:                                                       # Geschirr
            nt.ast(n, st, m - Vector((0, lang * 0.55, 0)), m - Vector((0, lang * 0.45, 0)), r * 1.12, r * 1.12, stoff="Leder", n=8)
    hals = m - Vector((0, lang * 0.9, -0.15))
    with t.an(f"{tier}_Kopf") as n:
        spitze = hals + Vector((0, -ms["hals"], ms["hals"] * 0.6))
        nt.ast(n, st, hals, spitze, r * 0.7, r * 0.5, stoff=fell, n=7)
        kopf_ende = spitze + Vector((0, -ms["kopf"], -ms["kopf"] * (0.7 if ochse else 1.2)))
        nt.ast(n, st, spitze, kopf_ende, r * 0.42, r * 0.3, stoff=fell, n=7)
        nt.ast(n, st, kopf_ende, kopf_ende + Vector((0, -0.04, -0.02)), r * 0.31, r * 0.25, stoff=fell2, n=7)
        for sx in (-1, 1):
            if ochse:                                               # Hörner
                nt.ast(n, st, spitze + Vector((sx * 0.12, 0, 0.1)), spitze + Vector((sx * 0.38, 0.05, 0.25)), 0.05, 0.015,
                       stoff="Horn", n=5)
            else:                                                   # Ohren
                nt.ast(n, st, spitze + Vector((sx * 0.07, 0.05, 0.08)), spitze + Vector((sx * 0.09, 0.08, 0.22)), 0.035,
                       0.01, stoff=fell, n=4)
            n.quader(spitze + Vector((sx * r * 0.36, -ms["kopf"] * 0.3, -0.02)), (0.02, 0.05, 0.05), st["Auge"])
        if not ochse:                                               # Mähne
            nt.ast(n, st, hals + Vector((0, 0.05, r * 0.6)), spitze + Vector((0, 0.08, r * 0.35)), 0.05, 0.04,
                   stoff=fell2, n=4)
    for vorn, sy in (("V", -1), ("H", 1)):
        for seite, sx in (("L", 1), ("R", -1)):
            fuss = m + Vector((sx * r * 0.6, sy * lang * 0.78, -r * 0.5))
            with t.an(f"{tier}_Bein{vorn}{seite}") as n:
                knie = Vector((fuss.x, fuss.y, (fuss.z + 0.04) / 2))
                nt.ast(n, st, fuss + Vector((0, 0, 0.12)), knie, r * 0.38, r * 0.24, stoff=fell, n=6)
                nt.ast(n, st, knie, Vector((fuss.x, fuss.y, 0.12)), r * 0.22, r * 0.2, stoff=fell, n=6)
                nt.ast(n, st, Vector((fuss.x, fuss.y, 0.12)), Vector((fuss.x, fuss.y - 0.02, 0.0)), r * 0.22, r * 0.25,
                       stoff=fell2 if not ochse else "Pferd dunkel", n=6)
    schwanz = m + Vector((0, lang, 0.1))
    with t.an(f"{tier}_Schwanz") as n:
        nt.ast(n, st, schwanz, schwanz + Vector((0, 0.1, -0.6)), 0.04, 0.03, stoff=fell, n=4)
        nt.ast(n, st, schwanz + Vector((0, 0.1, -0.5)), schwanz + Vector((0, 0.12, -0.75)), 0.07, 0.03,
               stoff=fell2 if not ochse else "Pferd dunkel", n=5)
    return t.objekt(ske)


def ladung_bauen(art, i, st, ske):
    """Ladung einer Sorte: ein Abschnitt des Wagenbetts (vorn, Mitte, hinten)."""
    d = ARTEN[art]
    y0, y1, h = d["bett"]
    abschnitt = (y1 - y0) / d["sorten"]
    ya, yb = y0 + abschnitt * i + 0.08, y0 + abschnitt * (i + 1) - 0.08
    ym = (ya + yb) / 2
    b = d["breite"] - 0.1
    t = fg.Teile(f"Ladung{i + 1}")
    with t.an("Wurzel") as n:
        art_ladung = i % 3
        if art_ladung == 0:                                         # Kisten
            for k, (x, dy, hh) in enumerate(((-b / 2, -0.1, 0.42), (b / 2, 0.1, 0.36), (0, 0.0, 0.3))):
                z = h + 0.04 + (0.0 if k < 2 else 0.4)
                n.quader((x * 0.9, ym + dy * abschnitt * 0.4, z + hh / 2), (b * 0.85, (yb - ya) * 0.75, hh), st["Holz"])
                n.quader((x * 0.9, ym + dy * abschnitt * 0.4, z + hh / 2), (b * 0.86, 0.05, hh + 0.01), st["Eisen"])
        elif art_ladung == 1:                                       # Säcke
            for k in range(4):
                x = (-0.5 + (k % 2)) * b * 0.9
                y = ya + (yb - ya) * (0.3 + 0.4 * (k // 2))
                lb.saeule(n, st, (x, y, h + 0.04), 0.22, 0, 0.42, "Sack", n=7)
                nt.krone(n, st, (x, y, h + 0.5), 0.2, 0.08, saat=k, stoffe=("Sack", "Sack"), n=6, flach_unten=0.0)
        else:                                                       # Fässer
            for k in range(2):
                bs.fass(n, st, Vector(((-0.5 + k) * b * 0.9, ym, h + 0.04)), r=min(0.3, b * 0.45), h=0.7)
    return t.objekt(ske)


def plane_bauen(art, st, ske):
    """Plane über der ganzen Ladung, in der Hausfarbe, mit Stricken."""
    d = ARTEN[art]
    y0, y1, h = d["bett"]
    b = d["breite"] + 0.04
    t = fg.Teile("Plane")
    with t.an("Wurzel") as n:
        z0, z1 = h + 0.55, h + 1.0
        profil = [(-b, z0), (b, z0), (b * 0.7, z1 - 0.1), (0, z1), (-b * 0.7, z1 - 0.1)]
        m = Matrix.Rotation(math.radians(90), 4, "Z")
        n.prisma([(x, z) for x, z in profil], y0 + 0.05, y1 - 0.05, st["Hausfarbe"], matrix=m)
        for k in range(3):
            y = y0 + (y1 - y0) * (k + 1) / 4
            n.stab((-b - 0.01, y, z0), (b + 0.01, y, z0), 0.03, 0.03, st["Seil"])
    return t.objekt(ske)


def anker(art):
    d = ARTEN[art]
    y0, y1, h = d["bett"]
    if art == "hand":
        return [bs.anker("Anker_Zieher", (0, y0 - 1.0, 0.0), 180.0)]       # Blick in Fahrtrichtung (-Y)
    if art == "ochse":
        m = tier_lage(art, "Ochse")
        return [bs.anker("Anker_Treiber", (1.05, m.y + 0.2, 0.0), 180.0)]
    return [bs.anker("Anker_Kutscher", (0, y0 - 0.25, h + 0.49), 180.0)]


# ─────────────────────────────────────────────────────────────────────────────
#  Bewegungen
# ─────────────────────────────────────────────────────────────────────────────

def haltung(art, name, phase):
    """{Knochen: (x, y, z) Grad}, Wippen des Wagens (m). Räder drehen um ihre Achse (lokales Y = entlang der Achse)."""
    d = ARTEN[art]
    s = math.sin(2 * math.pi * phase)
    r = {}
    wippen = 0.0
    if name == "Fahren":
        for i in range(len(d["achsen"])):
            r[f"Achse{i + 1}"] = (0, -360 * phase, 0)              # vorwärts (-Y) rollen
        wippen = 0.015 * abs(math.sin(4 * math.pi * phase))
        r["Wurzel"] = (0.6 * math.sin(4 * math.pi * phase), 0, 0.5 * s)
        # Tiere: zwei Schritte je Schleife im Kreuzgang (diagonale Beine gemeinsam)
        for tier in d["tiere"]:
            versatz = 0.25 if tier == "Pferd2" else 0.0
            w = math.sin(2 * math.pi * (2 * phase + versatz))
            for bein, vz in (("VL", 1), ("HR", 1), ("VR", -1), ("HL", -1)):
                r[f"{tier}_Bein{bein}"] = (-22 * w * vz, 0, 0)
            r[f"{tier}_Kopf"] = (6 * math.sin(2 * math.pi * (4 * phase + versatz)), 0, 0)
            r[f"{tier}_Rumpf"] = (0, 2 * w, 0)
            r[f"{tier}_Schwanz"] = (0, 0, 12 * math.sin(2 * math.pi * (2 * phase + versatz)))
    else:                                                           # Stehen
        for tier in d["tiere"]:
            versatz = 0.4 if tier == "Pferd2" else 0.0
            nicken = max(0.0, math.sin(2 * math.pi * (phase + versatz))) ** 3
            r[f"{tier}_Kopf"] = (-18 * nicken, 0, 6 * math.sin(2 * math.pi * (phase + versatz)))
            r[f"{tier}_Schwanz"] = (0, 0, 30 * math.sin(2 * math.pi * (3 * phase + versatz)) * (phase % 0.5 < 0.25))
            r[f"{tier}_BeinHL"] = (4 * nicken, 0, 0)
    return {k: tuple(math.radians(v) for v in w) for k, w in r.items()}, wippen


def pose_setzen(ob, pose, wippen):
    for pb in ob.pose.bones:
        pb.rotation_euler = pose.get(pb.name, (0.0, 0.0, 0.0))
        pb.location = (0.0, 0.0, 0.0)
    ob.pose.bones["Wurzel"].location = (0.0, wippen, 0.0)


def bewegungen_anlegen(ob, art):
    import bpy
    ob.animation_data_create()
    for name, dauer in DAUER.items():
        bilder = int(round(dauer * FPS))
        act = bpy.data.actions.new(name)
        ob.animation_data.action = act
        for f in range(0, bilder + 1, 1 if name == "Fahren" else 2):
            pose_setzen(ob, *haltung(art, name, (f % bilder) / bilder if f < bilder else 1.0 if name == "Fahren" else 0.0))
            for pb in ob.pose.bones:
                pb.keyframe_insert("rotation_euler", frame=f + 1)
            ob.pose.bones["Wurzel"].keyframe_insert("location", frame=f + 1)
        if name == "Fahren":                                         # Räder gleichmäßig (sonst bremst die Kurve)
            for fc in _kurven(act):
                for kp in fc.keyframe_points:
                    kp.interpolation = "LINEAR"
        spur = ob.animation_data.nla_tracks.new()
        spur.name = name
        spur.strips.new(name, 1, act)
        ob.animation_data.action = None
    pose_setzen(ob, *haltung(art, "Stehen", 0.0))


def _kurven(act):
    """F-Kurven einer Aktion (Blender 5: in Ebenen und Streifen)."""
    if hasattr(act, "fcurves"):
        return list(act.fcurves)
    kurven = []
    for ebene in act.layers:
        for streifen in ebene.strips:
            for bag in streifen.channelbags:
                kurven += list(bag.fcurves)
    return kurven


# ─────────────────────────────────────────────────────────────────────────────
#  Ausgabe
# ─────────────────────────────────────────────────────────────────────────────

def bauen(art, st):
    ske = skelett(art)
    objekte = [ske, karren_bauen(art, st, ske)]
    for tier in ARTEN[art]["tiere"]:
        objekte.append(tier_bauen(art, tier, st, ske))
    for i in range(ARTEN[art]["sorten"]):
        o = ladung_bauen(art, i, st, ske)
        o["teil"], o["sichtbar"] = True, False
        objekte.append(o)
    o = plane_bauen(art, st, ske)
    o["teil"], o["sichtbar"] = True, False
    objekte.append(o)
    return ske, objekte + anker(art)


def main():
    schnell = "--schnell" in sys.argv
    for art in ARTEN:
        bs.neue_szene()
        st = bs.Stoffe()
        ske, objekte = bauen(art, st)
        bewegungen_anlegen(ske, art)
        print(f"\nkarren_{art}: Dreiecke")
        for o in objekte:
            if o.type == "MESH":
                o.data.calc_loop_triangles()
                print(f"  {o.name:10s} {len(o.data.loop_triangles):5d}")
        print("geschrieben:", fg.exportieren("karren_" + art, objekte), f"(Weg je Schleife „Fahren“: {WEG[art]:.2f} m)")
    if "--ohne-bilder" not in sys.argv:
        vorschau(schnell)


def vorschau(schnell):
    """Oben: alle drei Karren beladen und mit Plane im Stand. Darunter: je Karren vier Phasen „Fahren“, leer."""
    import bpy
    bs.neue_szene()
    st = bs.Stoffe()
    sichtbar = []
    for zeile, art in enumerate(ARTEN):
        for spalte, (name, phase, voll) in enumerate((("Stehen", 0.0, True), ("Fahren", 0.0, False),
                                                      ("Fahren", 0.25, False), ("Fahren", 0.5, False))):
            ske, objekte = bauen(art, st)
            sx, sy = (spalte - 1.5) * 5.5, (1 - zeile) * 7.0
            ske.location = Vector((sx * 0.7071 - sy * 0.7071, sx * 0.7071 + sy * 0.7071, 0))
            ske.rotation_euler = (0, 0, math.radians(-90))
            pose_setzen(ske, *haltung(art, name, phase))
            for o in objekte:
                if o.type == "MESH" and (voll or not o.get("teil")):
                    sichtbar.append(o)
    bs.vorschau_buehne(None, ziel_hoehe=1.0, ausschnitt=26.0, schnell=schnell)
    print(bs.rendern("karren", lambda o: o in sichtbar))


if __name__ == "__main__":
    main()
