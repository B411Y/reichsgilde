"""Ruhe-Animationen (Idle) der Passanten: je Volk und Geschlecht ein eigener Clip „Stehen“ (Nutzerauftrag 14.11.2026).

    blender -b --factory-startup --python tools/modelle/passanten_idles.py -- \
        [--ordner=game/assets/modelle/figuren/probe] [--aus=<Ordner>] [--alt=<Ordner mit den alten .glb für die Bilder>] \
        [--bilder=<Ordner>] [--bericht=<datei.json>] [--nur=mann,frau,...]

Vorher standen alle Passanten breitbeinig (Quaternius „Idle_Loop“, übertragen: Knöchel rund 0,41 m auseinander, rechter Fuß 0,28 m
zurück). Der neue Stand ist **selbst gebaut** (Haltungen und Atmen aus Formeln, keine fremde Animation): Füße etwa unter den Hüftgelenken,
das Gewicht auf einem Bein (Becken zum Standbein verschoben und zur Spielbeinseite gekippt, das Spielbein ist darum gebeugt), der
Oberkörper gleicht aus, die Arme hängen locker (Zweigelenk-Rechnung der Arme aus passanten_clips.py), dazu Atmen und eine leichte
Gewichtsverlagerung. Alles hängt an Sinus und Kosinus ganzer Vielfacher der Schleifenlänge: das letzte Bild gleicht dem ersten.
Die Füße stehen fest: Die Beine werden je Bild mit der Zweigelenk-Rechnung auf feste Knöchel gelegt, die Hüfthöhe so, dass das
Standbein fast gestreckt ist.

Die Figur blickt nach −Y, links ist +X. Charakter je Figur steht in IDLES.

Eingang sind die fertigen .glb der Passanten (wie passanten_clips.py). Das Skript ersetzt dort nur die Animation „Stehen“ (binär,
die übrigen Clips, Netze und Stoffe bleiben Byte für Byte, siehe tools/modelle/passanten_idles_vergleich.py), und entfernt danach die
Daten, die niemand mehr braucht (die alte Stehen-Animation). Reihenfolge nach einem Neubau: passanten_probe.py → passanten_clips.py →
dieses Skript.
"""

import importlib.util
import json
import math
import os
import sys
import tempfile

import bpy
from mathutils import Matrix, Quaternion, Vector

HIER = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HIER, "..", ".."))

_spec = importlib.util.spec_from_file_location("passanten_clips", os.path.join(HIER, "passanten_clips.py"))
C = importlib.util.module_from_spec(_spec)
sys.modules["passanten_clips"] = C
_spec.loader.exec_module(C)
P = C.P

FPS = P.FPS
OPT = P.OPT
BERICHT = {}

# Länge der Schleife in Sekunden: ein ruhiger Atemzug (12 je Minute); der Oger atmet langsamer
DAUER = 5.0

# Charakter je Figur (Längen in Metern für einen Mann von 0,91 m Hüfthöhe; das Skript rechnet sie mit der Hüfthöhe der Figur um).
#   stand        Standbein: +1 links, −1 rechts
#   halbbreite   Knöchel seitlich von der Mitte (das Hüftgelenk liegt bei rund 0,11)
#   vor          Spielbein: Knöchel so weit vor dem Standbein (Meter, nach vorn)
#   spreiz       Fußspitzen nach außen (Grad)
#   verlagern    Becken zum Standbein (Meter)
#   kippen       Becken sinkt zur Spielbeinseite (Grad); der Oberkörper gleicht aus (gegenkippen, Anteil)
#   drehen       Becken dreht um die Hochachse zum Spielbein hin (Grad)
#   strecken     Standbein: Anteil der vollen Beinlänge (1 = durchgestreckt), kleiner = Knie leicht gebeugt
#   wiegen       Gewichtsverlagerung über die Schleife (Meter seitlich, Hin und Her einmal je Schleife)
#   atmen        Brustkorb hebt sich (Grad), Schultern heben sich mit
#   beugen       Oberkörper nach vorn (Grad, verteilt wie haltung_beugen), Kopf blickt wieder nach vorn
#   brust        Brust heraus, aufrecht (Grad nach hinten im oberen Rücken)
#   schultern    Schultern sinken (Grad, + = hängend)
#   kopf         (Neigung seitlich in Grad, Blick wandert um die Hochachse in Grad)
#   arm_aus, arm_vor   Hand neben dem Schenkel: seitlich nach außen und nach vorn (Anteil der Armlänge)
#   arm_gerade   Abstand Schulter–Handgelenk als Anteil der Armlänge (kleiner = Ellbogen gebeugter)
#   pendel       Arme schwingen der Gewichtsverlagerung nach (Meter)
IDLES = {
    # Mensch, Mann: natürlich; Gewicht rechts, linkes Knie locker
    "mann": dict(stand=-1, halbbreite=0.115, vor=0.1, spreiz=10, verlagern=0.016, kippen=6.0, gegenkippen=1.1, drehen=6, strecken=0.993,
                 wiegen=0.027, atmen=3.7, beugen=0, brust=0, schultern=0, kopf=(0, 12), arm_aus=0.10, arm_vor=0.04, arm_gerade=0.965, pendel=0.010),
    # Mensch, Frau: Füße enger, Gewicht links, Becken etwas deutlicher gekippt, Arme näher am Körper
    "frau": dict(stand=1, halbbreite=0.075, vor=0.09, spreiz=8, verlagern=0.016, kippen=7.0, gegenkippen=1.15, drehen=8, strecken=0.993,
                 wiegen=0.027, atmen=3.3, beugen=0, brust=1.0, schultern=0, kopf=(2, 12), arm_aus=0.07, arm_vor=0.06, arm_gerade=0.955, pendel=0.009),
    # Elb: aufrecht und ruhig, kaum Gewichtsverlagerung, Brust gehoben, Kopf hoch
    "elb": dict(stand=-1, halbbreite=0.10, vor=0.05, spreiz=7, verlagern=0.009, kippen=3.0, gegenkippen=1.0, drehen=3, strecken=0.996,
                wiegen=0.015, atmen=2.9, beugen=0, brust=3.5, schultern=-1.0, kopf=(0, 8), arm_aus=0.08, arm_vor=0.02, arm_gerade=0.975, pendel=0.004),
    "elbin": dict(stand=1, halbbreite=0.075, vor=0.06, spreiz=6, verlagern=0.011, kippen=3.5, gegenkippen=1.0, drehen=4, strecken=0.996,
                  wiegen=0.018, atmen=2.7, beugen=0, brust=3.5, schultern=-1.0, kopf=(1.5, 9), arm_aus=0.06, arm_vor=0.04, arm_gerade=0.965, pendel=0.004),
    # Zwerg: kräftig, Brust heraus, Füße etwa hüftbreit (nicht breitbeinig), Arme vom Körper weg, Ellbogen etwas gebeugt
    "zwerg": dict(stand=-1, halbbreite=0.13, vor=0.06, spreiz=12, verlagern=0.015, kippen=3.0, gegenkippen=1.0, drehen=4, strecken=0.99,
                  wiegen=0.026, atmen=4.2, beugen=0, brust=3.0, schultern=0, kopf=(0, 10), arm_aus=0.16, arm_vor=0.05, arm_gerade=0.94, pendel=0.008),
    "zwergin": dict(stand=1, halbbreite=0.11, vor=0.06, spreiz=10, verlagern=0.015, kippen=3.5, gegenkippen=1.0, drehen=5, strecken=0.99,
                    wiegen=0.025, atmen=3.7, beugen=0, brust=2.0, schultern=0, kopf=(1, 10), arm_aus=0.12, arm_vor=0.06, arm_gerade=0.945, pendel=0.008),
    # Halbling: locker, deutliches Spielbein, mehr Wiegen, Kopf leicht schief
    "halbling": dict(stand=1, halbbreite=0.10, vor=0.11, spreiz=12, verlagern=0.021, kippen=7.0, gegenkippen=1.2, drehen=9, strecken=0.99,
                     wiegen=0.036, atmen=3.7, beugen=0, brust=0, schultern=0.5, kopf=(4, 18), arm_aus=0.11, arm_vor=0.05, arm_gerade=0.95, pendel=0.014),
    "halblingin": dict(stand=-1, halbbreite=0.085, vor=0.1, spreiz=10, verlagern=0.022, kippen=7.0, gegenkippen=1.2, drehen=8, strecken=0.99,
                       wiegen=0.037, atmen=3.3, beugen=0, brust=0.5, schultern=0.5, kopf=(-4, 16), arm_aus=0.09, arm_vor=0.06, arm_gerade=0.95, pendel=0.013),
    # Ork: leicht gebeugt, Knie etwas gebeugt, Arme hängen vor dem Körper
    "ork": dict(stand=-1, halbbreite=0.13, vor=0.08, spreiz=14, verlagern=0.015, kippen=4.0, gegenkippen=1.0, drehen=5, strecken=0.975,
                wiegen=0.025, atmen=4.6, beugen=14.0, brust=0, schultern=2.0, kopf=(0, 12), arm_aus=0.13, arm_vor=0.12, arm_gerade=0.93, pendel=0.012),
    # Oger: schwer, Schultern hängen, tiefer Atem, Arme vom Bauch weg, träge Gewichtsverlagerung
    "oger": dict(stand=1, halbbreite=0.15, vor=0.06, spreiz=14, verlagern=0.019, kippen=3.5, gegenkippen=1.0, drehen=4, strecken=0.98,
                 wiegen=0.032, atmen=5.8, beugen=7.0, brust=0, schultern=3.0, kopf=(0, 9), arm_aus=0.20, arm_vor=0.07, arm_gerade=0.94, pendel=0.012),
}
BEZUG_HUEFTE = 0.91
# Schultern heben sich beim Einatmen um diesen Anteil von `atmen` (Grad); der Kopf neigt sich mit dem Wiegen um ± so viele Grad
ATEM_SCHULTER = 1.8
KOPF_WIEGEN = 1.3
# Weiche der Hüfthöhe zwischen beiden Beinen (Meter)
WEICH = 0.006


# ─────────────────────────────────────────────────────────────────────────────
#  1. Haltung setzen (Armaturenraum = Weltraum: die Armatur steht unverdreht im Ursprung)
# ─────────────────────────────────────────────────────────────────────────────


def aktualisieren():
    bpy.context.view_layer.update()


def welt_drehen(pb, dreh, um=None):
    """Dreht den Knochen (samt Kindern) im Weltraum um `dreh` (Matrix 3×3) um seinen Kopf (oder den Punkt `um`)."""
    aktualisieren()
    m = pb.matrix.copy()
    p = m.translation.copy() if um is None else um
    r = dreh.to_4x4()
    neu = Matrix.Translation(p) @ r @ Matrix.Translation(-p) @ m
    pb.matrix = neu
    aktualisieren()


def achse(x, y, z, grad):
    return Matrix.Rotation(math.radians(grad), 3, Vector((x, y, z)))


def bein_setzen(rig, seite, knoechel, pol, fuss_dreh):
    """Zweigelenk-Rechnung Oberschenkel–Unterschenkel auf eine Knöchellage; der Fuß bekommt seine Ruhe-Ausrichtung, gedreht um fuss_dreh."""
    ob, ub, fb = (rig.pose.bones[f"{seite}{k}"] for k in ("UpperLeg", "LowerLeg", "Foot"))
    aktualisieren()
    huefte = ob.head.copy()
    l1 = (ub.head - ob.head).length
    l2 = (fb.head - ub.head).length
    ziel = knoechel - huefte
    d = max(0.05, min(l1 + l2 - 1e-4, ziel.length))
    rz = ziel.normalized()
    s = math.acos(max(-1.0, min(1.0, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
    pv = (pol - rz * pol.dot(rz)).normalized()
    ober_dir = (rz * math.cos(s) + pv * math.sin(s)).normalized()
    knie = huefte + ober_dir * l1
    unter_dir = (huefte + rz * d - knie).normalized()
    for pb, richtung in ((ob, ober_dir), (ub, unter_dir)):
        aktualisieren()
        m = pb.matrix.copy()
        alt = (m.to_3x3() @ Vector((0, 1, 0))).normalized()
        welt_drehen(pb, alt.rotation_difference(richtung).to_matrix())
    aktualisieren()
    ruhe = rig.data.bones[f"{seite}Foot"].matrix_local.to_3x3()
    neu = (fuss_dreh @ ruhe).to_4x4()
    neu.translation = fb.matrix.translation
    fb.matrix = neu
    aktualisieren()
    return (fb.head - knoechel).length, d / (l1 + l2)


def ausrichten_teilweise(pb, ziel3, anteil):
    """Dreht den Knochen um den Anteil `anteil` von seiner jetzigen Weltausrichtung zur Ausrichtung ziel3."""
    aktualisieren()
    jetzt = pb.matrix.to_3x3().to_quaternion()
    delta = ziel3.to_quaternion() @ jetzt.inverted()
    welt_drehen(pb, Quaternion().slerp(delta, anteil).to_matrix())


def haltung(rig, cfg, t, mass, ruhe):
    """Setzt die Haltung zur Phase t (0..1) der Schleife."""
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
    aktualisieren()
    w = 2 * math.pi * t
    s_stand = cfg["stand"]
    s_spiel = -s_stand
    mb = rig.pose.bones

    # Knöchel (fest über die ganze Schleife): Standbein unter der Hüfte, Spielbein etwas vor und gespreizt
    knoechel = {}
    for seite, sx in (("Left", 1.0), ("Right", -1.0)):
        z = ruhe[f"{seite}Foot"].z - ruhe["boden"]       # Sohle auf dem Boden (die Ruhelage steht nicht bei allen Völkern auf z = 0)
        y = ruhe[f"{seite}Foot"].y
        knoechel[seite] = Vector((sx * cfg["halbbreite"] * mass, y - (cfg["vor"] * mass if sx == s_spiel else 0.0), z))

    # Becken: Gewichtsverlagerung als langsames Hin und Her von einem Bein aufs andere (Mitte zum Standbein verschoben); das Becken
    # sinkt zur entlasteten Seite und dreht sich zu ihr, beides folgt der Last (14.11.2026: Bewegung stärker, Nutzer)
    wiegen = math.sin(w)
    seit = s_stand * cfg["verlagern"] * mass + cfg["wiegen"] * mass * wiegen
    last = seit / ((cfg["verlagern"] + cfg["wiegen"]) * mass)                  # −1 … 1, + = Gewicht links
    kipp_winkel = -cfg["kippen"] * last                                         # +Y-Achse: +X sinkt bei positivem Winkel
    gier_becken = last * cfg["drehen"] * 0.5
    kipp_matrix = achse(0, 1, 0, kipp_winkel)
    dreh_matrix = achse(0, 0, 1, gier_becken)
    hips = mb["Hips"]
    becken = dreh_matrix @ kipp_matrix
    welt_drehen(hips, becken)
    # Hüfthöhe: kein Bein über den Anteil `strecken` seiner Länge (das belastete ist dann fast gestreckt, das andere gebeugt);
    # Atmen hebt das Becken nicht
    aktualisieren()
    hoehen = []
    for seite in ("Left", "Right"):
        gelenk = mb[f"{seite}UpperLeg"].head.copy() + Vector((seit, 0.0, 0.0))
        l = (mb[f"{seite}LowerLeg"].head - mb[f"{seite}UpperLeg"].head).length + \
            (mb[f"{seite}Foot"].head - mb[f"{seite}LowerLeg"].head).length
        ziel_d = l * cfg["strecken"]
        a = knoechel[seite]
        dx, dy = gelenk.x - a.x, gelenk.y - a.y
        d = a.z + math.sqrt(max(1e-6, ziel_d * ziel_d - dx * dx - dy * dy)) - gelenk.z
        hoehen.append(d)
    # weiches Minimum: beim Wechsel des belasteten Beins kein Knick in der Hüfthöhe (der Knick ließ die gebackene VAT zwischen
    # zwei Bildern bis 5 mm danebenliegen); liegt höchstens WEICH · ln 2 unter dem Minimum, die Beine sind dann eine Spur mehr gebeugt
    dz = -WEICH * math.log(sum(math.exp(-(h - min(hoehen)) / WEICH) for h in hoehen)) + min(hoehen)
    m = hips.matrix.copy()
    m.translation += Vector((seit, 0.0, dz))
    hips.matrix = m
    aktualisieren()

    # Rücken: gegen das Becken kippen (Schultern schräg gegen die Hüfte), nach vorn beugen, Brust heraus, Atmen
    atem = 0.5 - 0.5 * math.cos(w)          # 0 ausgeatmet … 1 eingeatmet
    gegen = -kipp_winkel * cfg["gegenkippen"]
    for k, anteil in (("Spine", 0.4), ("Chest", 0.3), ("UpperChest", 0.3)):
        welt_drehen(mb[k], achse(0, 1, 0, gegen * anteil))
        welt_drehen(mb[k], achse(0, 0, 1, -gier_becken * anteil))                     # Schultern wieder nach vorn
    for k, anteil in (("Spine", 0.3), ("Chest", 0.35), ("UpperChest", 0.35)):
        welt_drehen(mb[k], achse(1, 0, 0, cfg["beugen"] * anteil))
    welt_drehen(mb["UpperChest"], achse(1, 0, 0, -cfg["brust"] - cfg["atmen"] * atem))
    welt_drehen(mb["Chest"], achse(1, 0, 0, -0.3 * cfg["atmen"] * atem))
    for seite, sx in (("Left", 1.0), ("Right", -1.0)):
        # Schultern: + sinkt (Drehung um die Vorwärtsachse), Atmen hebt
        heben = -cfg["schultern"] + ATEM_SCHULTER * cfg["atmen"] * atem
        welt_drehen(mb[f"{seite}Shoulder"], achse(0, 1, 0, sx * heben))

    # Hals und Kopf: Blick wieder geradeaus, Kopf leicht geneigt, Blick wandert langsam
    neig, gier = cfg["kopf"]
    s = math.sin(w + 1.3)
    blick = gier * s * (1.5 - 0.5 * s * s)        # Blickwechsel: weich zur Seite, kurz verweilen, zurück (abgeflachter Sinus, ohne Ruck)
    neigung = -neig + KOPF_WIEGEN * math.sin(w + 0.4) + 0.4 * math.sin(2 * w)     # der Kopf neigt sich mit der Gewichtsverlagerung
    kopf_ziel = achse(0, 0, 1, blick) @ achse(0, 1, 0, neigung) @ ruhe["Head_rot"]
    hals_ziel = achse(0, 0, 1, 0.5 * blick) @ ruhe["Neck_rot"]
    ausrichten_teilweise(mb["Neck"], hals_ziel, 0.55 if cfg["beugen"] else 0.75)
    ausrichten_teilweise(mb["Head"], kopf_ziel, 1.0)

    # Beine auf die festen Knöchel
    fehler = {}
    for seite, sx in (("Left", 1.0), ("Right", -1.0)):
        pol = Vector((sx * 0.12, -1.0, 0.0))
        spreiz = achse(0, 0, 1, sx * cfg["spreiz"])
        fehler[seite] = bein_setzen(rig, seite, knoechel[seite], pol, spreiz)

    # Arme: Hand neben dem Schenkel, locker; sie pendeln der Gewichtsverlagerung leicht nach
    for seite, sx in (("Left", 1.0), ("Right", -1.0)):
        aktualisieren()
        ua, la, ha = mb[f"{seite}UpperArm"], mb[f"{seite}LowerArm"], mb[f"{seite}Hand"]
        lang = (la.head - ua.head).length + (ha.head - la.head).length
        richtung = Vector((sx * cfg["arm_aus"], -cfg["arm_vor"], -1.0)).normalized()
        pendel = Vector((-cfg["pendel"] * mass * math.sin(w - 0.6), cfg["pendel"] * mass * 0.5 * math.sin(2 * w + sx), 0.0))
        ziel = ua.head + richtung * lang * cfg["arm_gerade"] + pendel
        if seite == "Right":
            # Die rechte Hand hält bei Hirte und Wache den Stecken (Zub_stab, in der Faust der ersten Haltung des alten „Stehen“ gebaut):
            # Handhöhe und Ausrichtung wie dort, so steht der Stecken weiter senkrecht und auf dem Boden
            ziel.z = ruhe["Stab_hand"].z + pendel.z
            # Mit stärkerem Wiegen und Atmen kommt der Arm hier fast an seine volle Länge; dort springt der Ellbogen (Zweigelenk-Rechnung
            # ohne Spielraum). Abstand Schulter–Hand darum weich unter 97 % der Armlänge halten (die Hand hebt sich dann um Millimeter).
            d = (ziel - ua.head).length
            frei, rand = 0.90 * lang, 0.07 * lang
            if d > frei:
                ziel = ua.head + (ziel - ua.head) * ((frei + rand * math.tanh((d - frei) / rand)) / d)
        C.arm_ziel_setzen(rig, seite, ziel, Vector((sx * 0.35, 1.0, 0.0)), Vector((-sx * 0.85, 0.45, 0.0)))
        if seite == "Right":
            aktualisieren()
            m = ruhe["Stab_rot"].to_4x4()
            m.translation = ha.matrix.translation
            ha.matrix = m
            aktualisieren()
    return fehler


def ruhe_lesen(rig):
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
    aktualisieren()
    r = {pb.name: pb.head.copy() for pb in rig.pose.bones}
    r["Head_rot"] = rig.pose.bones["Head"].matrix.to_3x3()
    r["Neck_rot"] = rig.pose.bones["Neck"].matrix.to_3x3()
    return r


def stab_hand_lesen(rig):
    """Rechte Hand im ersten Bild des alten „Stehen“ (der Datei): Lage und Ausrichtung, in der Zub_stab senkrecht steht."""
    alt = next(a for a in bpy.data.actions if a.name.split(".")[0] == "Stehen")
    P.aktion_setzen(rig, alt)
    bpy.context.scene.frame_set(int(alt.frame_range[0]))
    aktualisieren()
    hand = rig.pose.bones["RightHand"]
    erg = hand.head.copy(), hand.matrix.to_3x3()
    rig.animation_data.action = None
    return erg


def sohle_in_ruhe(netze):
    """Tiefster Punkt des Halbschuhs in der Ruhelage (die übrigen Clips setzen die Sohle über die Hüftlage auf z = 0, siehe
    passanten_probe.bodenkontakt; die Ruhelage selbst steht bei Elben 7 cm zu tief, bei Zwergen und Halblingen 11 bis 13 cm zu hoch)."""
    obj = netze["Fuss_halb"]
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    z = min((obj.matrix_world @ v.co).z for v in me.vertices)
    ev.to_mesh_clear()
    return z


def idle_bauen(rig, art, netze):
    cfg = IDLES[art]
    for spur in rig.animation_data.nla_tracks:
        spur.mute = True
    stab_hand, stab_rot = stab_hand_lesen(rig)
    ruhe = ruhe_lesen(rig)
    ruhe["boden"] = sohle_in_ruhe(netze)
    ruhe["Stab_hand"], ruhe["Stab_rot"] = stab_hand, stab_rot
    mass = (rig.data.bones["LeftUpperLeg"].head_local.z - ruhe["boden"]) / BEZUG_HUEFTE
    n = int(round(DAUER * FPS))
    posen, fehler_max, streck = [], 0.0, []
    for i in range(n):
        f = haltung(rig, cfg, i / n, mass, ruhe)
        fehler_max = max(fehler_max, *(v[0] for v in f.values()))
        streck.append({k: round(v[1], 4) for k, v in f.items()})
        posen.append({pb.name: (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in rig.pose.bones})
    posen.append(posen[0])
    for spur in rig.animation_data.nla_tracks:
        spur.mute = False
    C.aktion_ersetzen(rig, "Stehen")
    act, bilder = C.aktion_schreiben(rig, "Stehen", posen)
    bericht = {"bilder": bilder, "sekunden": round(bilder / FPS, 3), "knoechel_fehler_max_m": round(fehler_max, 5),
               "beinstreckung_bild0": streck[0], "massstab": round(mass, 3),
               "knoechel_abstand_m": round(2 * cfg["halbbreite"] * mass, 3), "sohle_ruhelage_m": round(ruhe["boden"], 4)}
    return act, bilder, bericht


# ─────────────────────────────────────────────────────────────────────────────
#  2. glTF: Stehen ersetzen, Unbenutztes entfernen
# ─────────────────────────────────────────────────────────────────────────────


def unbenutztes_entfernen(j, b):
    """Entfernt Accessoren und BufferViews, auf die nichts mehr zeigt (die alte Stehen-Animation), und packt den Puffer neu.
    Die Bytes jedes übrigen BufferViews bleiben gleich, nur ihre Lage im Puffer und die Nummern ändern sich."""
    acc_benutzt = set()
    for m in j.get("meshes", []):
        for p in m["primitives"]:
            acc_benutzt.update(p["attributes"].values())
            if "indices" in p:
                acc_benutzt.add(p["indices"])
            for t in p.get("targets", []):
                acc_benutzt.update(t.values())
    for s in j.get("skins", []):
        if "inverseBindMatrices" in s:
            acc_benutzt.add(s["inverseBindMatrices"])
    for a in j.get("animations", []):
        for s in a["samplers"]:
            acc_benutzt.update((s["input"], s["output"]))
    acc_neu = {alt: neu for neu, alt in enumerate(sorted(acc_benutzt))}
    bv_benutzt = {j["accessors"][i]["bufferView"] for i in acc_benutzt if "bufferView" in j["accessors"][i]}
    bv_benutzt |= {img["bufferView"] for img in j.get("images", []) if "bufferView" in img}
    bv_neu = {alt: neu for neu, alt in enumerate(sorted(bv_benutzt))}
    neuer_puffer = bytearray()
    views = []
    for alt in sorted(bv_benutzt):
        v = dict(j["bufferViews"][alt])
        start = v.get("byteOffset", 0)
        roh = b[start:start + v["byteLength"]]
        while len(neuer_puffer) % 4:
            neuer_puffer.append(0)
        v["byteOffset"] = len(neuer_puffer)
        neuer_puffer.extend(roh)
        views.append(v)
    accs = []
    for alt in sorted(acc_benutzt):
        a = dict(j["accessors"][alt])
        if "bufferView" in a:
            a["bufferView"] = bv_neu[a["bufferView"]]
        accs.append(a)
    j["accessors"], j["bufferViews"] = accs, views
    for m in j.get("meshes", []):
        for p in m["primitives"]:
            p["attributes"] = {k: acc_neu[v] for k, v in p["attributes"].items()}
            if "indices" in p:
                p["indices"] = acc_neu[p["indices"]]
            if "targets" in p:
                p["targets"] = [{k: acc_neu[v] for k, v in t.items()} for t in p["targets"]]
    for s in j.get("skins", []):
        if "inverseBindMatrices" in s:
            s["inverseBindMatrices"] = acc_neu[s["inverseBindMatrices"]]
    for a in j.get("animations", []):
        for s in a["samplers"]:
            s["input"], s["output"] = acc_neu[s["input"]], acc_neu[s["output"]]
    for img in j.get("images", []):
        if "bufferView" in img:
            img["bufferView"] = bv_neu[img["bufferView"]]
    return j, neuer_puffer


# ─────────────────────────────────────────────────────────────────────────────
#  3. Bilder: Spielkamera (ortho, Gier −45°, Neigung 40°), Standbild und Mitte der Schleife
# ─────────────────────────────────────────────────────────────────────────────

GIER, NEIGUNG = -45.0, 40.0


def spielkamera(sc, mitte_z, breite):
    """Ortho-Kamera wie im Spiel: um die Hochachse −45° gedreht, 40° nach unten geneigt; Blick auf die Figur von vorn rechts oben."""
    g, n = math.radians(GIER), math.radians(NEIGUNG)
    # Blickrichtung: ohne Gier nach +Y (auf die Vorderseite der Figur, die nach −Y blickt), dann um −45° um die Hochachse
    blick = Vector((-math.sin(g) * math.cos(n), math.cos(g) * math.cos(n), -math.sin(n)))
    ziel = Vector((0, 0, mitte_z))
    C.kamera_setzen(sc, tuple(ziel - blick * 8.0), tuple(ziel), breite)


def bild_rendern(rig, act, bild):
    import numpy as np
    sc = bpy.context.scene
    P.aktion_setzen(rig, act)
    sc.frame_set(int(act.frame_range[0]) + bild)
    pfad = os.path.join(tempfile.gettempdir(), "rg_idle_tmp.png")
    sc.render.filepath = pfad
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(pfad, check_existing=False)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return a


def figur_bilder(art, glb, cfg):
    """Rendert Stehen aus `glb`: Standbild (Bild 0) und Mitte der Schleife; gibt zwei Bildfelder zurück."""
    C.szene_leeren()
    rig, netze = C.figur_laden(glb)
    C.vorschau_vorbereiten(rig, netze, cfg)
    for name in ("Zub_stab", "Zub_stroh"):
        if name in bpy.data.objects:
            bpy.data.objects[name].hide_render = True
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = 300, 460
    act = next(a for a in bpy.data.actions if a.name.split(".")[0] == "Stehen")
    for spur in rig.animation_data.nla_tracks:
        spur.mute = True
    spielkamera(sc, 0.85, 2.0)
    bilder = int(act.frame_range[1] - act.frame_range[0])
    return bild_rendern(rig, act, 0), bild_rendern(rig, act, bilder // 2)


def tafel_rendern(arten, alt_ordner, neu_ordner, ziel):
    """Alle Figuren nebeneinander: Zeilen ALT Standbild, ALT Mitte, NEU Standbild, NEU Mitte."""
    import numpy as np
    zeilen = {"alt0": [], "altm": [], "neu0": [], "neum": []}
    for art in arten:
        cfg = P.FIGUREN[art]
        a0, am = figur_bilder(art, os.path.join(alt_ordner, cfg["datei"]), cfg)
        n0, nm = figur_bilder(art, os.path.join(neu_ordner, cfg["datei"]), cfg)
        for k, v in (("alt0", a0), ("altm", am), ("neu0", n0), ("neum", nm)):
            zeilen[k].append(v)
    # Pixelzeile 0 ist unten: von unten nach oben NEU Mitte, NEU Standbild, ALT Mitte, ALT Standbild
    trenn = lambda v: np.concatenate([v, np.full((v.shape[0], 4, 4), (0.2, 0.2, 0.2, 1), dtype=np.float32)], axis=1)
    reihe = lambda k: np.concatenate([trenn(v) for v in zeilen[k]], axis=1)
    linie = lambda r: np.full((6, r.shape[1], 4), (0.1, 0.1, 0.1, 1), dtype=np.float32)
    r = [reihe(k) for k in ("neum", "neu0", "altm", "alt0")]
    gesamt = np.concatenate([r[0], r[1], linie(r[1]) * 3, r[2], r[3]], axis=0)
    h, w = gesamt.shape[:2]
    img = bpy.data.images.new("tafel", w, h, alpha=False)
    img.pixels.foreach_set(gesamt.reshape(-1))
    img.filepath_raw = ziel
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


# ─────────────────────────────────────────────────────────────────────────────
#  4. Ablauf
# ─────────────────────────────────────────────────────────────────────────────


def art_bearbeiten(art):
    cfg = P.FIGUREN[art]
    quelle_glb = os.path.join(OPT.get("ordner", os.path.join(REPO, "game", "assets", "modelle", "figuren", "probe")), cfg["datei"])
    aus_glb = os.path.join(OPT.get("aus", os.path.dirname(quelle_glb)), cfg["datei"])
    C.szene_leeren()
    rig, netze = C.figur_laden(quelle_glb)
    act, bilder, bericht = idle_bauen(rig, art, netze)
    bericht["datei"] = quelle_glb
    for spur in list(rig.animation_data.nla_tracks):
        if spur.name != "Stehen":
            rig.animation_data.nla_tracks.remove(spur)
    rig.animation_data.action = None
    for o in bpy.context.scene.objects:
        o.select_set(o is rig)
    hilfe = os.path.join(tempfile.gettempdir(), f"rg_idle_{art}.glb")
    bpy.ops.export_scene.gltf(
        filepath=hilfe, export_format="GLB", use_selection=True, export_yup=True, export_apply=False,
        export_animations=True, export_animation_mode="NLA_TRACKS", export_anim_slide_to_zero=True, export_morph=False,
        export_skins=True, export_lights=False, export_cameras=False, export_image_format="NONE")
    j, b = C.glb_lesen(quelle_glb)
    C.animationen_uebertragen(hilfe, j, b, ["Stehen"])
    j, b = unbenutztes_entfernen(j, b)
    os.makedirs(os.path.dirname(aus_glb), exist_ok=True)
    C.glb_schreiben(aus_glb, j, b)
    bericht["geschrieben"] = aus_glb
    bericht["groesse_vorher_b"] = os.path.getsize(quelle_glb) if quelle_glb != aus_glb else None
    bericht["groesse_b"] = os.path.getsize(aus_glb)
    bericht["dauer_s"] = {a["name"]: j["accessors"][a["samplers"][0]["input"]].get("max") for a in j["animations"]}
    return bericht


def hauptteil():
    arten = [a for a in P.FIGUREN if a in OPT.get("nur", ",".join(P.FIGUREN)).split(",")]
    if OPT.get("nur_bilder") != "1":
        for art in arten:
            BERICHT[art] = art_bearbeiten(art)
    if "bilder" in OPT:
        os.makedirs(OPT["bilder"], exist_ok=True)
        neu = OPT.get("aus", OPT.get("ordner", os.path.join(REPO, "game", "assets", "modelle", "figuren", "probe")))
        tafel_rendern(arten, OPT["alt"], neu, os.path.join(OPT["bilder"], "idles_alt_neu.png"))
    if "bericht" in OPT:
        with open(OPT["bericht"], "w", encoding="utf-8") as f:
            json.dump(BERICHT, f, indent=1, ensure_ascii=False, default=str)
    print("BERICHT", json.dumps(BERICHT, indent=1, ensure_ascii=False, default=str))


if __name__ == "__main__":
    hauptteil()
