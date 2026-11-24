"""Neustart Zugpferd, Schritt a: Proportionen eines Pferdenetzes in der Ruhepose in Zahlen (Meter, Pferd schaut nach -Y, Boden z = 0).

    blender -b --python tools/modelle/pferd2_mass.py -- --gltf=<scene.gltf|.glb> --json=<Ausgabe.json> [--s=1.0] [--profil]

Alle Zahlen kommen aus exakten Schnitten durch die Dreiecke des Netzes (Kanten gegen eine Ebene), nicht aus Eckenwolken. Das Netz ist grob (1903 Ecken):
ein Schnitt hat deshalb einige Dutzend Punkte, genug fuer Spannen und Mitten. --s skaliert (Widerrist auf Zielhoehe).
Gewichtsgruppen: Namen wie im Original (Bip01 ...) werden zu Gruppen Vorn/Hinten (je Seite) zusammengefasst.
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS)
S = float(OPT.get("s", 1.0))


def laden(pfad):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=pfad)
    for o in list(bpy.data.objects):
        if o.type == "MESH" and o.name == "Icosphere":
            bpy.data.objects.remove(o)
    ske = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    mesh = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith(("Object", "Nah"))][0]
    ske.animation_data_create()
    ske.animation_data.action = None
    ske.data.pose_position = "REST"
    bpy.context.view_layer.update()
    return ske, mesh


class Netz:
    def __init__(self, ob, s=1.0):
        e = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
        me = e.to_mesh()
        self.P = np.array([tuple(ob.matrix_world @ v.co) for v in me.vertices]) * s
        self.F = np.array([tuple(p.vertices) for p in me.polygons if len(p.vertices) == 3])
        quad = [tuple(p.vertices) for p in me.polygons if len(p.vertices) == 4]
        if quad:
            self.F = np.vstack([self.F, np.array([(q[0], q[1], q[2]) for q in quad]), np.array([(q[0], q[2], q[3]) for q in quad])])
        e.to_mesh_clear()
        self.z0 = 0.0

    def boden(self, z0):
        self.P[:, 2] -= z0
        self.z0 = z0

    def schnitt(self, achse, wert):
        """Punkte, in denen Dreieckskanten die Ebene achse = wert schneiden (achse 0 = x, 1 = y, 2 = z)."""
        P, F = self.P, self.F
        kanten = np.vstack([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
        a, b = P[kanten[:, 0]], P[kanten[:, 1]]
        da, db = a[:, achse] - wert, b[:, achse] - wert
        m = (da * db) < 0
        t = (da[m] / (da[m] - db[m]))[:, None]
        return a[m] + (b[m] - a[m]) * t


def spanne(p, k):
    if not len(p):
        return float("nan"), float("nan")
    return float(p[:, k].min()), float(p[:, k].max())


def messen(ske, ob, S_data, W, bonen=True):
    """Zahlen eines (normalisierten) Pferds in der aktuellen Pose. S_data: Faktor auf die Netzkoordinaten, W: Massstab der Suchfenster (Original-Meter mal W)."""
    global S, SD
    S = S_data
    SD = S
    N = Netz(ob, S_data)
    S = W                                       # ab hier ist S nur noch der Massstab der Suchfenster (Original-Meter mal W)
    N.boden(float(N.P[:, 2].min()))
    z0 = N.z0
    R = {"massstab": S}
    P = N.P
    R["hoehe_gesamt"] = float(P[:, 2].max())
    R["hinweis"] = "Landmarken je Pferd: Brust/Gesaess aus Schnitt z=1,10 m, Widerrist 23 % hinter Brust; Massangaben in Metern ohne Skalierung; bauchlinie_z_je_y = [y, z] in m"
    R["nase_y"] = float(P[:, 1].min())
    # Seitenprofil am Rumpf: Oberkante je y aus Schnitten; Widerrist = Hoechstpunkt der Oberkante im Bereich y -0.45..-0.05 (Original-Meter)
    def oben(y):
        s = N.schnitt(1, y)
        return float(s[:, 2].max()) if len(s) else float("nan")
    # Eigene Landmarken je Pferd (nicht die des Originals): Brustspitze und Gesaess aus dem Schnitt in 1,10 m Hoehe (Gurtlage), Widerrist 23 % der Rumpflaenge hinter der Brustspitze
    sz0 = N.schnitt(2, 1.1)
    sz0 = sz0[np.abs(sz0[:, 0]) > 0.1]
    y_brust0, y_gesaess0 = float(sz0[:, 1].min()), float(sz0[:, 1].max())
    y_w = y_brust0 + 0.23 * (y_gesaess0 - y_brust0)
    ys = np.arange(y_brust0 - 0.2, y_gesaess0, 0.01)
    top = np.array([oben(y) for y in ys])
    # Das Original hat keinen Widerristabsatz (die Oberlinie faellt vom Genick gleichmaessig zum Ruecken). Bezugspunkt: Oberlinie ueber dem Oberarmknochen,
    # y = -0.35 (Original-Meter): dort liegen Schulterblattspitze und Vorderbeinachse.
    iw = int(np.argmin(np.abs(ys - y_w)))
    R["widerrist_y"], R["widerrist_z"] = float(ys[iw]), float(top[iw])
    ik = int(np.nanargmax(np.where((ys > y_gesaess0 - 0.55 * S) & (ys < y_gesaess0 - 0.1 * S), top, -1)))
    R["kruppe_y"], R["kruppe_z"] = float(ys[ik]), float(top[ik])
    R["ruecken_min_z"] = float(np.nanmin(np.where((ys > R["widerrist_y"] + 0.1 * S) & (ys < R["kruppe_y"] - 0.1 * S), top, 9)))
    H = R["widerrist_z"]
    # Gurtschnitt hinter dem Ellbogen: y = Widerrist + 0.10*S (Rumpf ohne Beine: |x| < 0.15*S und z > Ellbogenhoehe)
    def rumpf_unten(y, xmax=0.16):
        s = N.schnitt(1, y)
        s = s[(np.abs(s[:, 0]) < xmax * S) & (s[:, 2] > 0.5 * S)]
        return float(s[:, 2].min()), float(s[:, 2].max())

    gurt_y = R["widerrist_y"] + 0.12 * S
    zu, zo = rumpf_unten(gurt_y)
    R["gurt_y"], R["gurt_unten_z"], R["gurt_oben_z"] = gurt_y, zu, zo
    R["rumpftiefe"] = zo - zu
    R["beinlaenge_brustunterkante_boden"] = zu
    R["tiefe_zu_bein"] = R["rumpftiefe"] / zu
    s = N.schnitt(1, gurt_y)
    s = s[(s[:, 2] > zu + 0.15 * S) & (s[:, 2] < zo - 0.12 * S)]
    R["rumpfbreite_gurt"] = spanne(s, 0)[1] - spanne(s, 0)[0]
    # Brust: vorderster Punkt des Rumpfes unter dem Hals (z zwischen 0.75 H*0.6 und 0.85 H*0.8): Schnitt in Hoehe z = 0.62 H
    sz = N.schnitt(2, 0.62 * H)
    sz = sz[(sz[:, 1] < R["widerrist_y"]) & (np.abs(sz[:, 0]) < 0.35 * S)]
    R["brustspitze_y"] = float(sz[:, 1].min())
    # Brustbreite: Schnitt y = Brustspitze + 0.10, Hoehe 0.5 H..0.75 H
    s = N.schnitt(1, R["brustspitze_y"] + 0.10 * S)
    s = s[(s[:, 2] > 0.55 * H) & (s[:, 2] < 0.78 * H)]
    R["brustbreite_aussen"] = spanne(s, 0)[1] - spanne(s, 0)[0]
    # Gesaess: hinterster Punkt des Hinterteils in Hoehe 0.7 H
    sz = N.schnitt(2, 0.7 * H)
    sz = sz[np.abs(sz[:, 0]) > 0.1 * S]
    R["gesaess_y"] = float(sz[:, 1].max())
    R["rumpflaenge_brustspitze_gesaess"] = R["gesaess_y"] - R["brustspitze_y"]
    # Kruppenbreite (Hueftbreite) y = 0.45 (Kruppe) in Hoehe 0.75 H..0.95 H
    s = N.schnitt(1, R["kruppe_y"])
    s = s[(s[:, 2] > 0.75 * H) & (s[:, 2] < R["kruppe_z"] - 0.04 * S)]
    R["kruppenbreite"] = spanne(s, 0)[1] - spanne(s, 0)[0]
    # Bauchlinie: tiefster Rumpfpunkt (|x| < 0.15) zwischen Ellbogen und Flanke
    bt = []
    for y in np.arange(R["widerrist_y"], y_gesaess0 - 0.2 * S, 0.05 * S):
        s = N.schnitt(1, y)
        s = s[(np.abs(s[:, 0]) < 0.15 * S) & (s[:, 2] > 0.5 * S)]
        if len(s):
            bt.append((round(float(y), 2), round(float(s[:, 2].min()), 3)))
    R["bauchlinie_z_je_y"] = bt
    R["bauch_tiefster_z"] = min(z for _, z in bt)
    # Kopf und Hals. Genick = Kopfknochen-Anfang (Knochen); Nase = vorderster Punkt
    if not bonen:                               # nur Netz: hoechster Punkt des Kopfes (Ohren) als Genickbezug
        kp = P[P[:, 1] < R["nase_y"] + 0.62 * S]
        kp = kp[np.argmax(kp[:, 2])]
        R["genick_y"], R["genick_z"] = float(kp[1]), float(kp[2])
    for pb in (ske.pose.bones if bonen else []):
        b = pb
        if b.name.endswith("Head_06_5") or b.name == "Kopf" or b.name.startswith("BN_Head_00"):
            h = (ske.matrix_world @ pb.head) - Vector((0, 0, z0 * 1.0))
            R["genick_y"], R["genick_z"] = float(h.y), float(h.z)
    pn = P[P[:, 1] < R["nase_y"] + 0.03 * S]
    R["nase_z"] = float(pn[:, 2].mean())
    gn = np.array([0, R["genick_y"], R["genick_z"]])
    R["kopflaenge_genick_nase"] = float(np.linalg.norm(gn - np.array([0, R["nase_y"], R["nase_z"]])))
    # Obere Halslinie: von der Ohrenhinterkante (hoechster Punkt am Kopf) bis Widerrist
    kopf_oben = P[(P[:, 1] < R["genick_y"] + 0.05 * S) & (P[:, 2] > 1.3 * S)]
    ik = np.argmax(kopf_oben[:, 2])
    R["scheitel_y"], R["scheitel_z"] = float(kopf_oben[ik, 1]), float(kopf_oben[ik, 2])
    R["hals_unterseite_oben"] = None
    # Hals: Schnitt senkrecht auf die Verbindung Genick-Widerrist in der Mitte; hier Naeherung: y-Schnitt in der Mitte
    hw = np.array([0, R["widerrist_y"], R["widerrist_z"]])
    R["hals_genick_widerrist"] = float(np.linalg.norm(hw - gn))
    ym = 0.5 * (R["genick_y"] + R["widerrist_y"])
    s = N.schnitt(1, ym)
    s = s[(s[:, 2] > 1.0 * S) & (np.abs(s[:, 0]) < 0.3 * S)]
    R["hals_mitte_y"] = float(ym)
    R["hals_mitte_breite"] = spanne(s, 0)[1] - spanne(s, 0)[0]
    R["hals_mitte_z"] = spanne(s, 2)
    s = N.schnitt(1, R["genick_y"] + 0.05 * S)
    s = s[(s[:, 2] > 1.3 * S) & (np.abs(s[:, 0]) < 0.3 * S)]
    R["genick_breite"] = spanne(s, 0)[1] - spanne(s, 0)[0]
    # Stirnbreite / Kopfhoehe an Ganasche: Schnitt y = Nase + 0.35 * Kopflaenge
    yk = R["nase_y"] + 0.3 * R["kopflaenge_genick_nase"]
    s = N.schnitt(1, yk)
    s = s[s[:, 2] > 1.2 * S]
    R["kopf_y30"] = {"breite": spanne(s, 0)[1] - spanne(s, 0)[0], "hoehe": spanne(s, 2)[1] - spanne(s, 2)[0]}
    # Gelenkhoehen (Knochenkoepfe Links, Original-Namen oder Spiel-Namen)
    kn = {}
    for pb in (ske.pose.bones if bonen else []):
        b = pb
        h = (ske.matrix_world @ pb.head) - Vector((0, 0, z0))
        for k, lab in (("L UpperArm", "schulter_v"), ("L Forearm", "karpus_v"), ("L Hand", "fessel_v"), ("L Thigh", "huefte_h"), ("L Calf", "knie_h"),
                       ("L HorseLink", "sprung_h"), ("L Foot", "fessel_h"), ("BN_L_UpperArm", "schulter_v"), ("BN_l_Forearm", "karpus_v"), ("BN_L_Hand", "fessel_v"),
                       ("BN_L_Thing", "huefte_h"), ("BN_L_Calf", "knie_h"), ("BN_L_HorseLink", "sprung_h"), ("BN_L_Foot", "fessel_h"), ("Vorn_L_Oben", "schulter_v"), ("Vorn_L_Unten", "karpus_v"), ("Vorn_L_Huf", "fessel_v"),
                       ("Hinten_L_Oben", "huefte_h"), ("Hinten_L_Unten", "knie_h"), ("Hinten_L_Roehre", "sprung_h"), ("Hinten_L_Huf", "fessel_h")):
            if k in b.name:
                kn[lab] = [round(float(h.x), 3), round(float(h.y), 3), round(float(h.z), 3)]
    R["gelenke_L"] = kn
    # Beinquerschnitte: z-Schnitte, linkes Vorderbein (y < Rumpfmitte) und linkes Hinterbein
    def bein(vorn, z):
        s = N.schnitt(2, z)
        sel = (s[:, 0] > 0.04 * S) & ((s[:, 1] < 0) if vorn else (s[:, 1] > 0.15 * S)) & (s[:, 1] > (-0.8 * S if vorn else 0))
        s = s[sel]
        if not len(s):
            return None
        # nur das Bein: zusammenhaengende Wolke um den Median
        c = np.median(s, axis=0)
        s = s[np.linalg.norm((s - c)[:, :2], axis=1) < 0.18 * S]
        return {"x": round(spanne(s, 0)[1] - spanne(s, 0)[0], 3), "y": round(spanne(s, 1)[1] - spanne(s, 1)[0], 3),
                "umfang_ellipse": round(math.pi * ((spanne(s, 0)[1] - spanne(s, 0)[0]) + (spanne(s, 1)[1] - spanne(s, 1)[0])) / 2, 3)}
    R["bein_vorn"] = {f"{z:.2f}": bein(True, z * S) for z in (0.06, 0.10, 0.14, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8)}
    R["bein_hinten"] = {f"{z:.2f}": bein(False, z * S) for z in (0.06, 0.10, 0.14, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0)}
    # Huf: x/y-Spanne der Ecken unter 0.04
    for name, vorn in (("vorn", True), ("hinten", False)):
        s = N.schnitt(2, 0.03 * S)
        s = s[(s[:, 0] > 0.02 * S) & ((s[:, 1] < 0) == vorn) & (np.abs(s[:, 1]) > 0.15 * S)]
        R["huf_" + name] = {"x": spanne(s, 0)[1] - spanne(s, 0)[0], "y": spanne(s, 1)[1] - spanne(s, 1)[0]}
    R["dreiecke"] = int(len(N.F))
    R["ecken"] = int(len(P))
    R["verhaeltnis_zu_widerrist"] = {
        "hals(Genick-Widerrist)": R["hals_genick_widerrist"] / H, "kopf(Genick-Nase)": R["kopflaenge_genick_nase"] / H,
        "rumpflaenge(Brust-Gesaess)": R["rumpflaenge_brustspitze_gesaess"] / H, "rumpftiefe(Gurt)": R["rumpftiefe"] / H,
        "beinlaenge(Gurtunterkante-Boden)": R["beinlaenge_brustunterkante_boden"] / H, "rumpftiefe/beinlaenge": R["tiefe_zu_bein"],
        "rumpfbreite(Gurt)": R["rumpfbreite_gurt"] / H, "brustbreite(aussen)": R["brustbreite_aussen"] / H, "kruppenbreite": R["kruppenbreite"] / H,
        "kruppenhoehe": R["kruppe_z"] / H, "scheitel": R["scheitel_z"] / H, "bauch_tiefster": R["bauch_tiefster_z"] / H}
    if "profil" in OPT:
        print("PROFIL y  oben  unten")
        for y in np.arange(-1.3, 1.1, 0.05) * S:
            s = N.schnitt(1, y)
            if len(s):
                r = s[(np.abs(s[:, 0]) < 0.15 * S) & (s[:, 2] > 0.5 * S)]
                print("PROFIL %.2f %.3f %.3f" % (y / S, s[:, 2].max() / S, (r[:, 2].min() / S if len(r) else float("nan"))))
    e_runden = None

    def runden(o):
        if isinstance(o, float):
            return round(o, 4)
        if isinstance(o, dict):
            return {k: runden(v) for k, v in o.items()}
        if isinstance(o, (list, tuple)):
            return [runden(v) for v in o]
        return o
    return runden(R)


def main():
    global S
    ske, ob = laden(OPT["gltf"])
    R = messen(ske, ob, S, float(OPT.get("w", S)))
    os.makedirs(os.path.dirname(OPT["json"]), exist_ok=True)
    json.dump(R, open(OPT["json"], "w"), indent=1)
    print("FERTIG", OPT["json"])


if __name__ == "__main__":
    main()






