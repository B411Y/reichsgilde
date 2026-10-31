"""Charakter Phase 2: Quelldaten laden (läuft in Blender; eigener Code, MIT).

- ICT-FaceKit Light (MIT): Netz, Teile, 53 ARKit-Formen und 2 Pupillenformen aus ~/werkzeuge/charakter/ict/src/FaceXModel
- MakeHuman-Grundnetz und Formziele (CC0) über MPFB 2 (GPL, nur als Werkzeug in Blender aufgerufen)
Nichts davon liegt im Repository; die Pfade stehen nur hier.
"""
import importlib
import json
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kopf2_werkzeug as kw  # noqa: E402

CH = os.environ.get("CHARAKTER", os.path.expanduser("~/werkzeuge/charakter"))
ICT = os.path.join(CH, "ict", "src", "FaceXModel")
MPFB_DATEN = os.path.join(CH, "mpfb_daten", "data")

# Teile des ICT-Netzes (Eckbereiche laut README des ICT-FaceKit)
ICT_TEILE = {
    "gesicht": (0, 9409), "hinterkopf": (9409, 11248), "mundhoehle": (11248, 13294),
    "augenhoehle_l": (13294, 13678), "augenhoehle_r": (13678, 14062), "zahnfleisch": (14062, 17039),
    "zaehne": (17039, 21451), "auge_l": (21451, 23021), "auge_r": (23021, 24591),
    "traene_l": (24591, 24795), "traene_r": (24795, 24999), "augrand_l": (24999, 25023), "augrand_r": (25023, 25047),
    "lidschatten_l": (25047, 25199), "lidschatten_r": (25199, 25351), "wimpern_l": (25351, 26035),
    "wimpern_r": (26035, 26719),
}
ICT_LEDERHAUT = {"l": (21451, 22221), "r": (23021, 23791)}
ICT_IRIS = {"l": (22221, 23021), "r": (23791, 24591)}
ICT_OBERE_ZAEHNE = (17039, 19247)


def ict_name_zu_arkit(n):
    """ICT „eyeBlink_L“ -> ARKit „eyeBlinkLeft“; „PupilDilate_L“ -> „pupilDilateLeft“."""
    if n.startswith("PupilDilate"):
        n = "p" + n[1:]
    if n.endswith("_L"):
        return n[:-2] + "Left"
    if n.endswith("_R"):
        return n[:-2] + "Right"
    return n


def ict_laden():
    """ICT-Grundnetz (Meter, Blender-Achsen) und alle Ausdrucksformen als Versätze."""
    v, fl, fl_uv, uv, stoffe = kw.obj_lesen(os.path.join(ICT, "generic_neutral_mesh.obj"), 0.01)
    assert len(v) == 26719, "ICT-Netz hat nicht 26719 Ecken"
    ind = json.load(open(os.path.join(ICT, "vertex_indices.json"), encoding="utf-8"))
    formen = {}
    for name in ind["expressions"] + ["PupilDilate_L", "PupilDilate_R"]:
        d = kw.obj_ecken(os.path.join(ICT, name + ".obj"), 0.01) - v
        d[np.linalg.norm(d, axis=1) < 2e-6] = 0.0     # Rauschen unter 0,002 mm weg (spart Platz, Formen werden dünn)
        formen[ict_name_zu_arkit(name)] = d
    return {"ecken": v, "flaechen": fl, "flaechen_uv": fl_uv, "uv": uv, "stoffe": stoffe, "formen": formen,
            "merkpunkte68": np.array(ind["idx_to_landmark_verts"])}


def ict_identitaeten(nummern):
    """Phase 6 (S6): ICT-Identitätskomponenten identityNNN (je 1 σ der Scan-Population) als Versätze wie ict_laden."""
    v = kw.obj_ecken(os.path.join(ICT, "generic_neutral_mesh.obj"), 0.01)
    aus = {}
    for k in nummern:
        d = kw.obj_ecken(os.path.join(ICT, f"identity{k:03d}.obj"), 0.01) - v
        d[np.linalg.norm(d, axis=1) < 2e-6] = 0.0
        aus[f"ictid_{k:03d}"] = d
    return aus


def _holen(paket, name):
    for modul in list(sys.modules):
        if modul.endswith(paket):
            return getattr(importlib.import_module(modul), name)
    raise ValueError("MPFB-Modul fehlt: " + paket)


class Mpfb:
    """MakeHuman-Grundnetz (Maßstab 0,1 = Meter, Blender-Achsen) mit Formzielen als Versatz-Feldern.

    lage(makros) setzt die Makroziele wie MPFB zusammen (TargetService.calculate_target_stack_from_macro_info_dict)."""

    def __init__(self):
        self.TS = _holen("mpfb.services.targetservice", "TargetService")
        self.OS = _holen("mpfb.services.objectservice", "ObjectService")
        self.LS = _holen("mpfb.services.locationservice", "LocationService")
        self.HS = _holen("mpfb.services.humanservice", "HumanService")
        self.AS = _holen("mpfb.services.assetservice", "AssetService")
        self.extra = _holen("mpfb.entities.socketobject", "BASEMESH_EXTRA_GROUPS")
        self.roh = self.OS.load_base_mesh(context=bpy.context, scale_factor=0.1, load_vertex_groups=True)
        self.roh.name = "MPFB_roh"
        self.n = len(self.roh.data.vertices)
        b = np.empty(self.n * 3, dtype=np.float32)
        self.roh.data.vertices.foreach_get("co", b)
        self.grund = b.reshape(self.n, 3).astype(np.float64)
        self.gruppen = {}
        for v in self.roh.data.vertices:
            for g in v.groups:
                if g.weight > 0.5:
                    self.gruppen.setdefault(self.roh.vertex_groups[g.group].name, []).append(v.index)
        for name, liste in self.extra.items():
            if liste:
                self.gruppen[name] = list(liste)
        self.gruppen = {k: np.array(v, dtype=np.int64) for k, v in self.gruppen.items()}
        self.flaechen = [list(p.vertices) for p in self.roh.data.polygons]
        koerper = set(self.gruppen["body"].tolist())
        self.koerper_flaechen = [f for f in self.flaechen if all(i in koerper for i in f)]
        self.ziele_ordner = self.LS.get_mpfb_data("targets")
        self._cache = {}

    def versatz(self, ziel):
        """Versatz (n,3) eines Formziels („head/head-scale-vert-incr“ oder „macrodetails/…“)."""
        if ziel in self._cache:
            return self._cache[ziel]
        pfad = os.path.join(self.ziele_ordner, ziel + ".target.gz")
        if not os.path.exists(pfad):
            pfad = pfad[:-3]
        self.TS.load_target(self.roh, pfad, weight=0.0, name="_t")
        kb = self.roh.data.shape_keys.key_blocks["_t"]
        d = np.empty(self.n * 3, dtype=np.float32)
        kb.data.foreach_get("co", d)
        d = d.reshape(self.n, 3).astype(np.float64) - self.grund
        self.roh.shape_key_remove(kb)
        self._cache[ziel] = d
        return d

    def makros(self, **werte):
        m = dict(self.TS.get_default_macro_info_dict())
        m["race"] = {"caucasian": 1.0, "asian": 0.0, "african": 0.0}
        m.update(werte)
        return m

    def lage(self, makros):
        p = self.grund.copy()
        for name, w in self.TS.calculate_target_stack_from_macro_info_dict(makros):
            p += w * self.versatz(name)
        return p

    def zubehoer(self, makros, dateien):
        """MPFB-Mensch mit Zubehör (.mhclo nach Name) erzeugen und die angepassten Teile als Ecken/Flächen/UV holen.
        Rückgabe je Name: (ecken (Meter, gleiche Lage wie lage(makros)), flächen, uv je Schleife, Bildpfad)."""
        mensch = self.HS.create_human(macro_detail_dict=makros, feet_on_ground=False)
        teile = {}
        for ordner, datei in dateien:
            pfad = self.AS.find_asset_absolute_path(datei + ".mhclo", asset_subdir=ordner)
            obj = self.HS.add_mhclo_asset(pfad, mensch, asset_type="Eyebrows" if ordner == "eyebrows" else "Eyelashes",
                                          material_type="GAMEENGINE", subdiv_levels=0, set_up_rigging=False)
            teile[datei] = obj
        self.TS.bake_targets(mensch)
        n = len(mensch.data.vertices)
        b = np.empty(n * 3, dtype=np.float32)
        mensch.data.vertices.foreach_get("co", b)
        aus = {"_mensch": b.reshape(n, 3).astype(np.float64)}
        for datei, obj in teile.items():
            for m in list(obj.modifiers):
                obj.modifiers.remove(m)
            dg = bpy.context.evaluated_depsgraph_get()
            ev = obj.evaluated_get(dg)
            me = ev.to_mesh()
            mw = np.array(obj.matrix_world)
            e = np.array([tuple(mw[:3, :3] @ np.array(v.co) + mw[:3, 3]) for v in me.vertices])
            fl = [list(p.vertices) for p in me.polygons]
            uv = [[tuple(me.uv_layers.active.data[li].uv) for li in p.loop_indices] for p in me.polygons]
            bild = os.path.join(MPFB_DATEN, "eyebrows" if datei.startswith("eyebrow") else "eyelashes", datei, datei + ".png")
            aus[datei] = (e, fl, uv, bild)
            ev.to_mesh_clear()
        bpy.data.objects.remove(mensch, do_unlink=True)
        for obj in teile.values():
            bpy.data.objects.remove(obj, do_unlink=True)
        return aus
