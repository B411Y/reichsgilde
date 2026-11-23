"""Prüfung der neuen Karren-GLBs (Pferdekarren, Großer Pferdekarren): Bilder aus dem Spiel-GLB mit Zugpferd aus dem Spiel-GLB, Messungen, Vorher/Nachher.

    blender -b --python tools/modelle/karren_neu_pruefen.py -- --aus=<Ordner> [--vorher=<Ordner mit karren_pferd_alt.glb, karren_ochse.glb>] [--proben=24] [--nur=a,b,...]

Aufträge (--nur): vorher_ochse, vorher_pferd (alte GLBs), pferdekarren, gross (neue GLBs aus game/assets/modelle/karren), gross_knick (Vorderwagen 0/15/30/45 Grad).
Ausgabe: PNG je Ansicht und Textzeilen `PRUEF ...` (Dreiecke, Abstände Pferd zu Wagenteilen, Abstände im Knick, Länge des Gespanns).
Pferd: tier_pferd_fuchs.glb, Pose „Ziehen“ (Spiel-Leser pferd2_spielrender), zwei Pferde mit halbem Zyklus Versatz. Ochse: tier_ochse_rotbraun.glb (Blender-Import, Ruhepose).
Der Vorderwagen und die Pferde drehen im Knick gemeinsam um den Reibnagel (im Spiel gehen die Tiere in Richtung der Deichsel, `TierFolgtDeichsel`).
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_bilder as pb  # noqa: E402
import pferd2_spielrender as SR  # noqa: E402

OPT = pb.OPT
WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
TIERE = os.path.join(WURZEL, "game", "assets", "modelle", "tiere")
KARREN = os.path.join(WURZEL, "game", "assets", "modelle", "karren")
ZT = json.load(open(os.path.join(WURZEL, "game", "daten", "zugtiere.json"), encoding="utf-8"))["tiere"]
ANSICHTEN = {"seite": (0, 0, 640), "oben": (0, 89.0, 760), "spiel": (40, 36, 760)}


def lade_karren(pfad):
    vor = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=pfad)
    neu = [o for o in bpy.data.objects if o not in vor]
    for o in neu:
        if o.type == "MESH" and o.name.startswith(("Stuetze", "Ladung", "Plane")):
            o.hide_render = True
    return neu


def dreiecke(objs):
    sichtbar = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH" and not o.hide_render)
    gesamt = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH")
    return sichtbar, gesamt


def bvh(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    e = obj.evaluated_get(dg)
    me = e.to_mesh()
    me.transform(e.matrix_world)
    punkte = [v.co.copy() for v in me.vertices]
    b = BVHTree.FromPolygons(punkte, [tuple(p.vertices) for p in me.polygons])
    e.to_mesh_clear()
    return b, punkte


def abstand(A, B):
    """Kleinster Abstand zwischen zwei Gruppen von Netzen (Ecken gegen Flächen, beide Richtungen) und Zahl der Ecken näher als 5 mm."""
    ba = [bvh(o) for o in A]
    bb = [bvh(o) for o in B]
    kl, nah = 9e9, 0
    for (ta, pa) in ba:
        for (tb, pb_) in bb:
            for p in pa:
                r = tb.find_nearest(p)
                if r[0] is not None:
                    kl = min(kl, r[3])
                    nah += r[3] < 0.005
            for p in pb_:
                r = ta.find_nearest(p)
                if r[0] is not None:
                    kl = min(kl, r[3])
    return kl, nah


def pferde(pferd, mat, anker, zahl):
    az = ZT["pferd"]["anker_ziehen_godot"]
    res = []
    for k, nm in enumerate(anker[:zahl]):
        a = bpy.data.objects[nm].matrix_world.translation
        Q, Nw = pferd.pose("Ziehen", 6 if k == 0 else 19)
        res.append(SR.netz(f"Pferd{k}", pferd, Q, Nw, mat, (a.x - az[0], a.y + az[2], a.z - az[1])))
    return res


def ochse(anker):
    az = ZT["ochse"]["anker_ziehen_godot"]
    a = bpy.data.objects[anker].matrix_world.translation
    vor = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(TIERE, "tier_ochse_rotbraun.glb"))
    namen = [o.name for o in bpy.data.objects if o not in vor]
    for nm in namen:
        if nm.startswith(("Fern_", "Icosphere")):
            bpy.data.objects.remove(bpy.data.objects[nm])
    halter = bpy.data.objects.new("_OchseHalter", None)
    bpy.context.scene.collection.objects.link(halter)
    halter.location = (a.x - az[0], a.y + az[2], a.z - az[1])
    for nm in namen:
        if nm in bpy.data.objects and bpy.data.objects[nm].parent is None:
            bpy.data.objects[nm].parent = halter


def bilder(kam, name, ziel_y, mass, aus, ansichten=("seite", "oben", "spiel")):
    for an in ansichten:
        az, el, h = ANSICHTEN[an]
        pb.aufnahme(kam, Vector((0, ziel_y, 1.0)), az, el, mass, os.path.join(aus, f"{name}_{an}.png"), 1300, h)


def szene(datei, tiere):
    pb.OPT  # noqa: B018
    bpy.ops.wm.read_factory_settings(use_empty=True)
    neu = lade_karren(datei)
    return neu


def auftrag(nr, aus, pferd):
    cfg = {
        "vorher_ochse": (os.path.join(OPT.get("vorher", ""), "karren_ochse.glb"), "ochse", "vorher_ochsenkarren", -2.2, 9.5),
        "vorher_pferd": (os.path.join(OPT.get("vorher", ""), "karren_pferd_alt.glb"), "zwei", "vorher_pferdekarren", -3.1, 12.5),
        "pferdekarren": (os.path.join(KARREN, "karren_pferd.glb"), "eins", "nachher_pferdekarren", -2.4, 11.0),
        "gross": (os.path.join(KARREN, "karren_pferd_gross.glb"), "zwei", "nachher_grosser_pferdekarren", -3.4, 12.5),
    }[nr]
    datei, tier, name, ziel_y, mass = cfg
    bpy.ops.wm.read_factory_settings(use_empty=True)
    neu = lade_karren(datei)
    mat = SR.material(pferd, True)
    gruppe = []
    if tier == "ochse":
        ochse("Anker_Deichsel")
    elif tier == "eins":
        gruppe = pferde(pferd, mat, ["Anker_Deichsel"], 1)
    else:
        gruppe = pferde(pferd, mat, ["Anker_Pferd_L", "Anker_Pferd_R"], 2)
    s, g = dreiecke(neu)
    print(f"PRUEF {name} dreiecke_sichtbar={s} gesamt={g} knoten={sorted(o.name for o in neu if o.type == 'MESH')}")
    kam = pb.buehne()
    bilder(kam, name, ziel_y, mass, aus)


def baue(art, straenge):
    import bausteine as bs
    import karren as K
    K.STRAENGE = straenge
    bs.neue_szene()
    return K.BAUER[art](bs.Stoffe())


def messung(pferd, art):
    """Abstände aus dem Bauskript (ohne Zugstränge, die enden am Kummet): Pferde zu Wagenteilen gerade; beim Großen im Knick Vorderwagen samt Pferden gegen Kasten und Hinterachse."""
    zwei = art == "pferd_gross"
    phis = (0, 15, 30, 40, 50, 60, 70) if zwei else (0,)
    for phi in phis:
        objekte = baue(art, False)
        bpy.context.view_layer.update()
        mat = SR.material(pferd, True)
        gruppe = pferde(pferd, mat, ["Anker_Pferd_L", "Anker_Pferd_R"] if zwei else ["Anker_Deichsel"], 2 if zwei else 1)
        meshes = [o for o in objekte if o.type == "MESH" and not o.get("zustand") and o.name not in ("Wimpel", "Decke")]
        if zwei:
            vw = next(o for o in objekte if o.name == "Vorderwagen")
            pivot = bpy.data.objects.new("_Reibnagel", None)
            bpy.context.scene.collection.objects.link(pivot)
            pivot.location = vw.location.copy()
            bpy.context.view_layer.update()
            for o in [vw] + gruppe:
                o.parent = pivot
                o.matrix_parent_inverse = pivot.matrix_world.inverted()
            pivot.rotation_euler = (0, 0, math.radians(phi))
            bpy.context.view_layer.update()
            dreh = [o for o in meshes if o.name in ("Vorderwagen", "Rad_VL", "Rad_VR")]
            fest = [o for o in meshes if o not in dreh]
            fb = [bvh(o)[0] for o in fest]

            def nach_fest(punkte):
                kl_, n_ = 9e9, 0
                for pt in punkte:
                    for t in fb:
                        r = t.find_nearest(pt)
                        if r[0] is not None:
                            kl_ = min(kl_, r[3])
                            n_ += r[3] < 0.005
                return kl_, n_
            vwo = bpy.data.objects["Vorderwagen"]
            vorn_pts = [vwo.matrix_world @ v.co for v in vwo.data.vertices if v.co.y < -0.75]                    # Teile vor der Kastenstirn (hinter ihr liegt der Schemel, der den Kasten absichtlich trägt)
            rad_pts = [o.matrix_world @ v.co for o in dreh if o.name.startswith("Rad_V") for v in o.data.vertices]
            pf_pts = [o.matrix_world @ v.co for o in gruppe for v in o.data.vertices]
            a1 = nach_fest(vorn_pts)
            a2 = nach_fest(rad_pts)
            a3 = nach_fest(pf_pts)
            kp, nahp = abstand(gruppe, [vwo])
            print(f"PRUEF knick {phi}deg: Deichsel/Waagscheit vor dem Kasten zu Kasten+Bock {a1[0]:.3f} m ({a1[1]}); Vorderräder zu Kasten+Hinterachse {a2[0]:.3f} m ({a2[1]}); Pferde zu Kasten+Bock {a3[0]:.3f} m ({a3[1]}); Pferde zu Vorderwagen (Mittelbaum, Waagscheit) {kp:.3f} m ({nahp})")
        else:
            for k, p in enumerate(gruppe):
                kl, nah = abstand([p], meshes)
                print(f"PRUEF {art} pferd{k} zu Wagenteilen (ohne Zugstränge) {kl:.3f} m (Ecken unter 5 mm: {nah})")
        if phi == 0:
            ring = bpy.data.objects["Anker_Pferd_L" if zwei else "Anker_Deichsel"].matrix_world.translation
            nase = min(min((p.matrix_world @ v.co).y for v in p.data.vertices) for p in gruppe)
            kruppe = max(max((p.matrix_world @ v.co).y for v in p.data.vertices) for p in gruppe)
            hint = max(max((o.matrix_world @ v.co).y for v in o.data.vertices) for o in meshes)
            front = min(min((o.matrix_world @ v.co).y for v in o.data.vertices) for o in meshes if o.name in ("Karren", "Vorderwagen"))
            print(f"PRUEF {art} zugpunkt_y={ring.y:.3f} nase_y={nase:.3f} (vor dem Ring {ring.y - nase:.3f}) kruppe_y={kruppe:.3f} wagen_vorn_y={front:.3f} wagen_hinten_y={hint:.3f} gespann_vorn={abs(nase):.2f} hinten={hint:.2f}")
        if zwei and phi in (0, 40):
            kam = pb.buehne()
            bilder(kam, f"messmodell_grosser_pferdekarren_knick{phi}", -3.4, 12.5, OPT["aus"], ("oben",))


def knick_bilder(pferd, aus):
    """Bilder aus dem Spiel-GLB im Knick: Vorderwagen und Pferde drehen gemeinsam um den Reibnagel (Knoten Vorderwagen)."""
    for phi in (30, 40, 60):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        lade_karren(os.path.join(KARREN, "karren_pferd_gross.glb"))
        mat = SR.material(pferd, True)
        gruppe = pferde(pferd, mat, ["Anker_Pferd_L", "Anker_Pferd_R"], 2)
        vw = bpy.data.objects["Vorderwagen"]
        pivot = bpy.data.objects.new("_Reibnagel", None)
        bpy.context.scene.collection.objects.link(pivot)
        pivot.location = vw.matrix_world.translation.copy()
        bpy.context.view_layer.update()
        for o in [vw] + gruppe:
            o.parent = pivot
            o.matrix_parent_inverse = pivot.matrix_world.inverted()
        pivot.rotation_euler = (0, 0, math.radians(phi))
        kam = pb.buehne()
        bilder(kam, f"nachher_grosser_pferdekarren_knick{phi}", -3.4, 12.5, aus, ("oben", "spiel"))


def main():
    aus = OPT["aus"]
    os.makedirs(aus, exist_ok=True)
    nur = OPT.get("nur", "vorher_ochse,vorher_pferd,pferdekarren,gross,knick,messung").split(",")
    pferd = SR.Pferd(os.path.join(TIERE, "tier_pferd_fuchs.glb"))
    for nr in nur:
        if nr == "knick":
            knick_bilder(pferd, aus)
        elif nr == "messung":
            messung(pferd, "pferd")
            messung(pferd, "pferd_gross")
        else:
            auftrag(nr, aus, pferd)


if __name__ == "__main__":
    main()
