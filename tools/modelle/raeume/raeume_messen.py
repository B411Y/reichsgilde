"""Raeume im Wohnhaus: Soll gegen Ist aus den fertigen GLBs (reines Python mit numpy, ohne Blender).

    python tools/modelle/raeume/raeume_messen.py [--md=<datei>]
    blender -b --factory-startup --python tools/modelle/raeume/raeume_messen.py -- [--ordner=<glb-ordner>] [--texturen=<ordner>] [--gegenprobe]
    (in Blender dazu Pruefung 12 „keine schwarze Flaeche“ und die UV2-Inseln der Raum-GLB, tools/modelle/neubau/uv2_pruefung.py)

Liest wohnhaus_mensch_s1_g1_innen.glb (Wohnstube mit den Zimmertueren) und wohnhaus_mensch_s1_g1_<raum>_innen.glb und prueft gegen
raeume_masse.py:
  * Dreiecke je Knoten (Moebel-Cluster nach Style-Guide: Stufe 2 250-700, Stufe 3 500-1.200 – nur Auskunft),
  * Raumhuelle (Knoten „Raum“): lichte Breite, Tiefe und Hoehe wie die Wohnstube,
  * Fenster: das Glas (Innenglas) liegt in den Fensteroeffnungen der Huelle,
  * Tueren: Anker Tuer_* auf der Wand, in der lichten Oeffnung, extras breite/hoehe gegen das Soll; Tuerblatt (Knoten Zimmertuer_*)
    so gross wie die Oeffnung und unter den Galeriebalken,
  * Kamera: Blick_Auge 1,60 m ueber dem Boden; der Gegenstand (Gegenstand_*) ist da.
Koordinaten wie in den Bauskripten (Blender: X rechts, Y nach hinten, Z oben); glTF (Y oben) wird zurueckgerechnet.
"""
import json
import os
import struct
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import raeume_masse as M  # noqa: E402

ORDNER = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(HIER))), "game", "assets", "modelle", "gebaeude", "neubau")
HAUS = "wohnhaus_mensch_s1_g1"
RAEUME = {"Hinterzimmer": ("hinterzimmer", "Kartentisch", (250, 700)), "Bibliothek": ("bibliothek", "Lesepult", (500, 1200))}


class Glb:
    def __init__(self, pfad):
        b = open(pfad, "rb").read()
        n = struct.unpack("<I", b[12:16])[0]
        self.j = json.loads(b[20:20 + n])
        off = 20 + n
        m = struct.unpack("<I", b[off:off + 4])[0]
        self.bin = b[off + 8:off + 8 + m]

    def accessor(self, i):
        a = self.j["accessors"][i]
        bv = self.j["bufferViews"][a["bufferView"]]
        anz = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
        typ = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[a["componentType"]]
        start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        daten = np.frombuffer(self.bin, dtype=typ, count=a["count"] * anz, offset=start)
        return daten.reshape(a["count"], anz) if anz > 1 else daten

    def knoten(self):
        """Name -> (Lage in Blender-Koordinaten, extras, Netz-Ecken in Blender-Koordinaten oder None, Dreiecke, Materialnamen)."""
        aus = {}
        for nd in self.j["nodes"]:
            t = nd.get("translation", [0, 0, 0])
            lage = np.array([t[0], -t[2], t[1]])
            ecken, dreiecke, stoffe = None, 0, []
            if "mesh" in nd:
                teile = []
                for p in self.j["meshes"][nd["mesh"]]["primitives"]:
                    pos = self.accessor(p["attributes"]["POSITION"]).astype(np.float64)
                    teile.append(np.stack([pos[:, 0], -pos[:, 2], pos[:, 1]], axis=1) + lage)
                    dreiecke += (len(self.accessor(p["indices"])) if "indices" in p else len(pos)) // 3
                    if "material" in p: stoffe.append(self.j["materials"][p["material"]].get("name", ""))
                ecken = np.concatenate(teile)
            aus[nd["name"]] = (lage, nd.get("extras", {}), ecken, dreiecke, stoffe)
        return aus


def glas_ecken(glb):
    """Ecken der Flaechen mit dem Material Innenglas."""
    teile = []
    for nd in glb.j["nodes"]:
        if "mesh" not in nd: continue
        for p in glb.j["meshes"][nd["mesh"]]["primitives"]:
            if "material" in p and glb.j["materials"][p["material"]].get("name") == "Innenglas":
                pos = glb.accessor(p["attributes"]["POSITION"]).astype(np.float64)
                teile.append(np.stack([pos[:, 0], -pos[:, 2], pos[:, 1]], axis=1))
    return np.concatenate(teile) if teile else np.zeros((0, 3))


def pruefen():
    zeilen = []
    def p(name, soll, ist, ok): zeilen.append((name, soll, ist, ok))
    # Wohnstube: Zimmertueren
    w = Glb(os.path.join(ORDNER, f"{HAUS}_innen.glb")).knoten()
    for raum, (wand, (y0, y1)) in M.WOHNSTUBE_TUEREN.items():
        x = -M.IX if wand == "links" else M.IX
        if f"Tuer_Wohnstube_{raum}" not in w or f"Zimmertuer_{raum}" not in w:
            p(f"Wohnstube: Tuer zum {raum}", "Anker und Tuerblatt da", "fehlt", False); continue
        lage, ex, _, _, _ = w[f"Tuer_Wohnstube_{raum}"]
        p(f"Wohnstube: Anker der Tuer zum {raum} auf der Wand", f"x = {x:+.2f}", f"x = {lage[0]:+.3f}", abs(abs(lage[0]) - M.IX) < 0.05)
        p(f"Wohnstube: Anker der Tuer zum {raum} in der Oeffnung", f"y {y0:.2f}..{y1:.2f}", f"y = {lage[1]:.2f}", y0 < lage[1] < y1)
        p(f"Wohnstube: Klickflaeche der Tuer zum {raum}", f">= {M.TUER_B:.2f} x {M.TUER_H:.2f}", f"{ex.get('breite', 0):.2f} x {ex.get('hoehe', 0):.2f}",
          ex.get("breite", 0) >= M.TUER_B - 1e-6 and ex.get("hoehe", 0) >= M.TUER_H - 1e-6)
        e = w[f"Zimmertuer_{raum}"][2]
        breite, oben = e[:, 1].max() - e[:, 1].min(), e[:, 2].max()
        p(f"Wohnstube: Tuerblatt mit Bekleidung zum {raum} breit", f">= {M.TUER_B:.2f} m", f"{breite:.2f} m", breite >= M.TUER_B)
        p(f"Wohnstube: Tuer zum {raum} unter den Galeriebalken", "< 2,35 m", f"{oben:.2f} m", oben < 2.35)
        p(f"Wohnstube: Tuer zum {raum} Dreiecke (Auskunft)", "-", f"{w[f'Zimmertuer_{raum}'][3]}", None)
    # Raeume
    for raum, (datei, gegenstand, budget) in RAEUME.items():
        pfad = os.path.join(ORDNER, f"{HAUS}_{datei}_innen.glb")
        if not os.path.exists(pfad):
            p(f"{raum}: Modell", "da", "fehlt", False); continue
        glb = Glb(pfad)
        k = glb.knoten()
        e = k["Raum"][2]
        zb = e[:, 2].min()
        f = e[np.abs(e[:, 2] - zb) < 0.001]                       # Ecken des Fussbodens: lichte Masse (Fensterbaenke reichen in die Laibung)
        bx, by = f[:, 0].max() - f[:, 0].min(), f[:, 1].max() - f[:, 1].min()
        p(f"{raum}: Raum lichte Breite", f"{2 * M.IX:.2f} m", f"{bx:.2f} m", abs(bx - 2 * M.IX) < 0.02)
        p(f"{raum}: Raum lichte Tiefe", f"{M.IH - M.IV:.2f} m", f"{by:.2f} m", abs(by - (M.IH - M.IV)) < 0.02)
        p(f"{raum}: Fussboden", f"{M.Z_BODEN:.2f} m", f"{zb:.2f} m", abs(zb - M.Z_BODEN) < 0.005)
        p(f"{raum}: Raumhoehe bis zur Decke", f"{M.DECKE - M.Z_BODEN:.2f} m", f"{e[:, 2].max() - zb:.2f} m", abs(e[:, 2].max() - M.DECKE) < 0.02)
        g = glas_ecken(glb)
        for wand, (a0, a1), (z0, z1) in M.FENSTER_EG:
            if wand == "vorn_eg":
                sel = g[np.abs(g[:, 1] - (M.IV - M.T / 2 + 0.004)) < 0.02]; a = sel[:, 0]
            else:
                sel = g[np.abs(g[:, 0] - (-M.IX - M.T / 2 + 0.004)) < 0.02]; a = sel[:, 1]
            ok = len(sel) > 0 and a.min() >= a0 - 0.005 and a.max() <= a1 + 0.005 and sel[:, 2].min() >= z0 - 0.005 and sel[:, 2].max() <= z1 + 0.005
            ist = f"{a.min():.2f}..{a.max():.2f}, z {sel[:, 2].min():.2f}..{sel[:, 2].max():.2f}" if len(sel) else "kein Glas"
            p(f"{raum}: Glas im Fenster {wand}", f"{a0:.2f}..{a1:.2f}, z {z0:.2f}..{z1:.2f}", ist, ok)
        wand, (y0, y1) = M.RAUM_TUER[raum]
        tuer = k.get(f"Tuer_{raum}_Wohnstube")
        if tuer is None:
            p(f"{raum}: Tuer zur Wohnstube", "Anker da", "fehlt", False)
        else:
            lage, ex = tuer[0], tuer[1]
            p(f"{raum}: Tuer zur Wohnstube auf der Wand", f"|x| = {M.IX:.2f}", f"x = {lage[0]:+.3f}", abs(abs(lage[0]) - M.IX) < 0.05)
            p(f"{raum}: Tuer zur Wohnstube in der Oeffnung", f"y {y0:.2f}..{y1:.2f}", f"y = {lage[1]:.2f}", y0 < lage[1] < y1)
            p(f"{raum}: Tuer licht", f">= 0,85 x 1,95 m", f"{ex.get('breite', 0):.2f} x {ex.get('hoehe', 0):.2f} m", ex.get("breite", 0) >= 0.85 and ex.get("hoehe", 0) >= 1.95)
        auge = k.get("Blick_Auge")
        p(f"{raum}: Kamera auf Augenhoehe", "1,60 m", f"{auge[0][2] - M.Z_BODEN:.2f} m" if auge else "fehlt", auge is not None and abs(auge[0][2] - M.Z_BODEN - 1.60) < 0.01)
        p(f"{raum}: Gegenstand {gegenstand}", "da", "da" if f"Gegenstand_{gegenstand}" in k else "fehlt", f"Gegenstand_{gegenstand}" in k)
        summe = 0
        for name, (_, _, ecken, drei, _) in sorted(k.items()):
            if ecken is None: continue
            summe += drei
            if name not in ("Raum", "Glas"):
                p(f"{raum}: Dreiecke {name} (Auskunft, Cluster {budget[0]}-{budget[1]})", f"{budget[0]}-{budget[1]}", f"{drei}", None)
        p(f"{raum}: Dreiecke gesamt (Auskunft)", "-", f"{summe}", None)
    return zeilen


def uv2_pruefen(ordner, texturen=None):
    """Gemeinsame Pruefungen der gebackenen Modelle (tools/modelle/neubau/uv2_pruefung.py; nur in Blender) fuer beide Raum-GLB und den
    Innenraum der Probe (Wohnstube mit den Zimmertueren): 12 keine schwarze Flaeche, 13 UV2-Inseln, 14 UV2-Dichte und typischer
    Inselrand (innen: Dichte mindestens 300 Bildpunkte je m2), 15 Normalmap und ORM des Blatts."""
    sys.path.insert(0, os.path.join(os.path.dirname(HIER), "neubau"))
    import uv2_pruefung as U
    modelle = [("Wohnstube (Probe)", os.path.join(ordner, f"{HAUS}_innen.glb"), None, None)]
    for raum, (datei, _, _) in RAEUME.items():
        modelle.append((raum, os.path.join(ordner, f"{HAUS}_{datei}_innen.glb"), "raeume_innen_trim", datei))
    zeilen = []
    kurz = lambda z: "; ".join(str(x).strip() for x in z if "FEHLER" in str(x))[:600]
    for name, glb, blatt, datei in modelle:
        if not os.path.exists(glb): continue
        atlas = (U.atlas_von(glb, atlas_json=os.path.join(HIER, "ao_atlas.json"), gruppe="wohnhaus_mensch_raeume_innen", modell=datei)
                 if datei else U.atlas_von(glb))
        z12, f12 = U.schwarze_flaechen(glb, texturen=texturen, blatt=blatt, atlas=atlas)
        zeilen.append((f"{name}: 12. keine schwarze Flaeche", "0 Fehler", f"{f12} Fehler " + kurz(z12), f12 == 0))
        zu, fu, w = U.uv2_inseln(glb, atlas=atlas)
        werte = ", ".join(f"{k} {v:.2f}" if isinstance(v, float) else f"{k} {v}" for k, v in w.items())
        zeilen.append((f"{name}: 13. UV2-Inseln (kleinste Insel, Dichte, Rand)", "0 Fehler", f"{fu} Fehler ({werte}) " + kurz(zu), fu == 0))
        zd, fd, wd = U.uv2_dichte(glb, atlas=atlas)
        werte = ", ".join(f"{k} {v:.1f}" if isinstance(v, float) else f"{k} {v}" for k, v in wd.items())
        zeilen.append((f"{name}: 14. UV2-Dichte und Inselrand", "0 Fehler", f"{fd} Fehler ({werte}) " + kurz(zd), fd == 0))
        zn, fn = U.normal_orm(glb, texturen=texturen, blatt=blatt, atlas=atlas)
        zeilen.append((f"{name}: 15. Normalmap und ORM", "0 Fehler", f"{fn} Fehler " + kurz(zn), fn == 0))
    return zeilen

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    opt = dict((a[2:].split("=", 1) + [""])[:2] for a in args if a.startswith("--"))
    if opt.get("ordner"): ORDNER = opt["ordner"]
    zeilen = pruefen()
    try:
        import bpy  # noqa: F401  (in Blender: dazu die UV2-Pruefungen)
        zeilen += uv2_pruefen(ORDNER, opt.get("texturen") or None)
    except ImportError:
        print("Hinweis: Pruefung 12 und UV2-Inseln nur in Blender (blender -b --factory-startup --python tools/modelle/raeume/raeume_messen.py -- ...)")
    fehler = sum(1 for z in zeilen if z[3] is not None and not z[3])
    text = ["| Prüfung | Soll | Ist | |", "|---|---|---|---|"] + [f"| {n} | {s} | {i} | {'Auskunft' if ok is None else ('ok' if ok else 'FEHLER')} |" for n, s, i, ok in zeilen]
    for n, s, i, ok in zeilen:
        print(f"MESSUNG {'Auskunft' if ok is None else ('ok' if ok else 'FEHLER'):8} {n}: Soll {s}, Ist {i}")
    print("MESSUNG Ergebnis:", "ok" if fehler == 0 else f"{fehler} Fehler")
    if opt.get("md"): open(opt["md"], "w", encoding="utf-8").write("\n".join(text) + "\n")
    if "gegenprobe" not in opt: sys.exit(1 if fehler else 0)