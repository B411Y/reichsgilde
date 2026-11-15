"""Neubau Rathaus: Soll gegen Ist aus den fertigen GLBs (reines Python mit numpy, ohne Blender).

    python tools/modelle/neubau/rathaus_messen.py [--md=<datei>]

Liest game/assets/modelle/gebaeude/neubau/rathaus.glb und alle rathaus_<raum>_innen.glb und prueft gegen rathaus_masse.py:
  * Dreiecke je Knoten, Huelle im Budget des Style-Guides (Monument 8.000-15.000),
  * belegte Flaeche der Huelle (Katalog 16 x 10 m) und Hoehe,
  * je Raum: Hullkiste der Raumhuelle (Knoten Raum_*) gegen die lichten Masse,
  * Fenster: jede Scheibe innen (Innenglas) liegt in derselben Ebene und Lage wie die Scheibe aussen (Fenster),
  * Tueren: jeder Anker Tuer_* liegt in seiner Tueroeffnung (Mitte, Breite, Hoehe aus den extras gegen TUER_LICHT),
  * Kamera: Blick_Auge auf 1,60 m ueber dem Boden des Raums.
Koordinaten wie in den Bauskripten (Blender: X rechts, Y nach hinten, Z oben); glTF wird zurueckgerechnet.
"""
import json
import os
import struct
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import rathaus_masse as M  # noqa: E402

ORDNER = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(HIER))), "game", "assets", "modelle", "gebaeude", "neubau")


class Glb:
    def __init__(self, pfad):
        b = open(pfad, "rb").read()
        n = struct.unpack("<I", b[12:16])[0]
        self.j = json.loads(b[20:20 + n])
        off = 20 + n
        m = struct.unpack("<I", b[off:off + 4])[0]
        self.bin = b[off + 8:off + 8 + m]
        self.eltern = {c: i for i, nd in enumerate(self.j["nodes"]) for c in nd.get("children", [])}

    def accessor(self, i):
        a = self.j["accessors"][i]
        bv = self.j["bufferViews"][a["bufferView"]]
        typ = {5126: np.float32, 5123: np.uint16, 5121: np.uint8, 5125: np.uint32}[a["componentType"]]
        anz = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
        start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        return np.frombuffer(self.bin, dtype=typ, count=a["count"] * anz, offset=start).reshape(a["count"], anz).astype(np.float64)

    def matrix(self, i):
        nd = self.j["nodes"][i]
        m = np.eye(4)
        if "matrix" in nd: return np.array(nd["matrix"]).reshape(4, 4).T
        t, r, s = nd.get("translation", [0, 0, 0]), nd.get("rotation", [0, 0, 0, 1]), nd.get("scale", [1, 1, 1])
        x, y, z, w = r
        rot = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
        m[:3, :3] = rot @ np.diag(s); m[:3, 3] = t
        return m

    def welt(self, i):
        m = self.matrix(i)
        while i in self.eltern:
            i = self.eltern[i]; m = self.matrix(i) @ m
        return m

    def knoten(self):
        return {nd.get("name", ""): i for i, nd in enumerate(self.j["nodes"])}

    def ecken(self, name, nur_stoff=None):
        """Ecken eines Netzknotens in Blender-Koordinaten, je Flaeche (Stoff) getrennt: {stoff: (ecken, dreiecke)}."""
        i = self.knoten()[name]
        nd = self.j["nodes"][i]
        if "mesh" not in nd: return {}
        w = self.welt(i)
        aus = {}
        for p in self.j["meshes"][nd["mesh"]]["primitives"]:
            stoff = self.j["materials"][p["material"]]["name"] if "material" in p else ""
            if nur_stoff and stoff != nur_stoff: continue
            pos = self.accessor(p["attributes"]["POSITION"])
            pos = (w @ np.c_[pos, np.ones(len(pos))].T).T[:, :3]
            pos = np.c_[pos[:, 0], -pos[:, 2], pos[:, 1]]           # glTF (Y oben) -> Blender (Z oben)
            idx = self.accessor(p["indices"]).astype(int).reshape(-1, 3)
            aus[stoff] = (pos, idx)
        return aus

    def mit_uv(self, name):
        """Wie ecken, aber je Stoff (ecken, dreiecke, uv) mit UV1 (TEXCOORD_0, glTF: v von oben wie im Shader)."""
        i = self.knoten()[name]
        nd = self.j["nodes"][i]
        w = self.welt(i)
        aus = []
        for p in self.j["meshes"][nd["mesh"]]["primitives"]:
            stoff = self.j["materials"][p["material"]]["name"] if "material" in p else ""
            pos = self.accessor(p["attributes"]["POSITION"])
            pos = (w @ np.c_[pos, np.ones(len(pos))].T).T[:, :3]
            idx = self.accessor(p["indices"]).astype(int).reshape(-1, 3)
            aus.append((stoff, pos, idx, self.accessor(p["attributes"]["TEXCOORD_0"])))
        return aus

    def dreiecke(self, name):
        return sum(len(i) for _, i in self.ecken(name).values())

    def anker(self, name):
        i = self.knoten()[name]
        p = self.welt(i)[:3, 3]
        return np.array([p[0], -p[2], p[1]]), self.j["nodes"][i].get("extras", {})


def scheiben(glb, knoten, stoff):
    """Rechtecke der Scheiben (je zwei Dreiecke): Mitte, Breite (waagerecht), Hoehe."""
    erg = []
    for st, (pos, idx) in glb.ecken(knoten, stoff).items():
        benutzt = set()
        for a in range(0, len(idx), 2):
            ecken = pos[np.unique(idx[a:a + 2].ravel())]
            lo, hi = ecken.min(axis=0), ecken.max(axis=0)
            erg.append(((lo + hi) / 2, max(hi[0] - lo[0], hi[1] - lo[1]), hi[2] - lo[2]))
    return erg


def pruefen():
    zeilen = []
    def z(name, soll, ist, ok): zeilen.append((name, soll, ist, ok))
    f = lambda v: f"{v:.2f}".replace(".", ",")
    huelle = Glb(os.path.join(ORDNER, "rathaus.glb"))
    k = huelle.knoten()
    tri = {n: huelle.dreiecke(n) for n in ("Haus", "Dach", "Scheiben") if n in k}
    summe = sum(tri.values())
    z("Dreiecke Huelle (Haus + Dach + Scheiben)", "8.000-15.000", f"{summe} ({', '.join(f'{n} {v}' for n, v in tri.items())})", 8000 <= summe <= 15000)
    alle = np.vstack([p for n in tri for p, _ in huelle.ecken(n).values()])
    lo, hi = alle.min(axis=0), alle.max(axis=0)
    z("Belegte Flaeche der Huelle", "<= 16,00 x 10,00", f"{f(hi[0] - lo[0])} x {f(hi[1] - lo[1])}", hi[0] - lo[0] <= 16.0 and hi[1] - lo[1] <= 10.0)
    z("Hoehe bis zur Wetterfahne", f"{f(M.Z_FAHNE)}", f(hi[2]), abs(hi[2] - M.Z_FAHNE) < 0.05)
    aussen = scheiben(huelle, "Scheiben", "Fenster")
    for raum, (x0, x1, y0, y1, zb, zd, *_rest) in M.RAEUME.items():
        datei = os.path.join(ORDNER, f"rathaus_{raum}_innen.glb")
        if not os.path.exists(datei):
            z(f"{M.RAUM_TITEL[raum]}", "Modell", "noch nicht gebaut", True)
            continue
        g = Glb(datei)
        kn = g.knoten()
        teile = [n for n in kn if "mesh" in g.j["nodes"][kn[n]]]
        tri_r = {n: g.dreiecke(n) for n in teile}
        z(f"{M.RAUM_TITEL[raum]}: Dreiecke", "<= 8.000 (Ratshalle 10.000)", f"{sum(tri_r.values())} ({', '.join(f'{n} {v}' for n, v in tri_r.items())})",
          sum(tri_r.values()) <= (10000 if raum == "ratshalle" else 8000))
        raumknoten = next(n for n in teile if n.startswith("Raum_"))
        pos = np.vstack([p for p, _ in g.ecken(raumknoten).values()])
        # lichte Masse: Boden und Waende (Ecken innerhalb der Wandflaechen, ohne Laibungen und Gewaende davor)
        rlo, rhi = pos.min(axis=0), pos.max(axis=0)
        z(f"{M.RAUM_TITEL[raum]}: Innenflaechen x", f"{f(x0)}..{f(x1)}", f"{f(rlo[0])}..{f(rhi[0])}", rlo[0] >= x0 - 0.30 and rhi[0] <= x1 + 1.25)
        # Boden: Ecken der waagerechten Dreiecke auf Bodenhoehe (nicht die Unterkanten der Laibungen in den Durchgaengen)
        boden = []
        for p_, i_ in g.ecken(raumknoten).values():
            for d in i_:
                a, b, c = p_[d]
                if max(abs(a[2] - zb), abs(b[2] - zb), abs(c[2] - zb)) < 0.005: boden += [a, b, c]
        boden = np.array(boden)
        z(f"{M.RAUM_TITEL[raum]}: Boden (x, y)", f"{f(x0)}..{f(x1)} x {f(y0)}..{f(y1)} auf {f(zb)}",
          f"{f(boden[:, 0].min())}..{f(boden[:, 0].max())} x {f(boden[:, 1].min())}..{f(boden[:, 1].max())}",
          abs(boden[:, 0].min() - x0) < 0.01 and abs(boden[:, 0].max() - x1) < 0.01 and abs(boden[:, 1].min() - y0) < 0.01 and abs(boden[:, 1].max() - y1) < 0.01)
        if raum == "amtsstube": zd = M.anbau_dach_z(x0) - 0.15                  # schraege Decke unter dem Pultdach: hoechster Punkt an der Hallenwand
        if raum == "turm_spitze": zd = M.Z_HELM_SPITZE - 0.30                    # offen bis in den Helm (Innenseite unter der Spitze)
        if raum == "kerker": zd = M.Z_SOCKEL                                     # die Kellertreppe reicht bis zum Boden der Halle
        z(f"{M.RAUM_TITEL[raum]}: Decke", f(zd), f(rhi[2]), abs(rhi[2] - zd) < 0.15)
        # Fenster innen gegen aussen
        glas = [n for n in teile if n.startswith("Glas_")]
        for n in glas:
            for mitte, b, h in scheiben(g, n, "Innenglas"):
                treffer = [a for a in aussen if np.linalg.norm(a[0][:2] - mitte[:2]) < 0.05 and abs(a[0][2] - mitte[2]) < 0.02]
                ok = bool(treffer) and abs(treffer[0][1] - b) < 0.01 and abs(treffer[0][2] - h) < 0.01
                z(f"{M.RAUM_TITEL[raum]}: Fenster innen = aussen", f"Mitte {f(mitte[0])}/{f(mitte[1])}/{f(mitte[2])}, {f(b)} x {f(h)}",
                  "gleich" if ok else "keine Scheibe aussen an dieser Stelle", ok)
        # Tueren
        for n in kn:
            if not n.startswith("Tuer_"): continue
            p, ex = g.anker(n)
            name = n
            if name in M.TUEREN:
                von, nach, achse, (a, b), ebene = M.TUEREN[name]
                lage_ok = a[0] - 0.05 <= p[0] <= a[1] + 0.05 and b[0] - 0.05 <= p[1] <= b[1] + 0.05
                breite = float(ex.get("breite", 0)); hoehe = float(ex.get("hoehe", 0))
                soll_b = M.TUER_LICHT[0] if nach != "stadt" else M.ARKADE[1][1] - M.ARKADE[1][0]
                z(f"{M.RAUM_TITEL[raum]}: {n}", f"in der Oeffnung, {f(soll_b)} breit, >= {f(M.TUER_LICHT[1])} hoch",
                  f"{f(p[0])}/{f(p[1])}, {f(breite)} x {f(hoehe)}", lage_ok and abs(breite - soll_b) < 0.01 and hoehe >= M.TUER_LICHT[1] - 0.01)
        if "Blick_Auge" in kn:
            p, _ = g.anker("Blick_Auge")
            stand = zb + (23 * M.W_STEIG if raum == "turm_treppe" else 0.0)       # im Turm steht man auf der 23. Stufe der Wendel
            z(f"{M.RAUM_TITEL[raum]}: Kamera auf Augenhoehe", f"{f(stand + 1.60)}", f(p[2]), abs(p[2] - stand - 1.60) < 0.01)
        if raum == "ratshalle":
            # Konsolen der Deckenbalken im Modell gemessen (Unterseiten auf ZD - BALKEN_H - KONSOLE an den Laengswaenden) gegen die Scheiben
            scheiben_r = [s for n in glas for s in scheiben(g, n, "Innenglas")]
            fuesse = konsolen(pos, y0, y1, zd - M.BALKEN_H - M.KONSOLE[1])
            schlecht = [fu for fu in fuesse if not konsole_frei(fu, scheiben_r)]
            z("Ratshalle: Konsolen gemessen, Abstand zur Fensterlaibung", f">= {f(M.AUFLAGER_ABSTAND)} bei {2 * len(M.RATSHALLE_BALKEN)} Konsolen",
              f"{len(fuesse) - len(schlecht)} von {len(fuesse)} frei" + (f" (an x {', '.join(f(fu[1]) for fu in schlecht)})" if schlecht else ""),
              not schlecht and len(fuesse) == 2 * len(M.RATSHALLE_BALKEN))
            m, b_, h_ = scheiben_r[0]
            probe = (m[1] > 0, m[0] - M.KONSOLE[0] + b_ / 2, m[0] + M.KONSOLE[0] + b_ / 2)    # Gegenprobe: Konsole ueber der Ecke des ersten Fensters
            z("Ratshalle: Gegenprobe Konsole an der Fensterecke", "erkannt", "erkannt" if not konsole_frei(probe, scheiben_r) else "NICHT erkannt",
              not konsole_frei(probe, scheiben_r))
        # Texeldichte und Streckung je Band (alle Netze des Raums), Treffer mit Netz
        for band, (flaeche, falsch, dichte, streck, netze) in texel_raum(g, teile).items():
            z(f"{M.RAUM_TITEL[raum]}: Texel {band}", "Farbfelder ohne Streifen (nicht ueber 4 gestreckt ueber 10 % des Feldes)" if band == "kleinteile" else f"Streckung <= {f(STRECKUNG_GRENZE)}, Dichte +-{DICHTE_GRENZE * 100:.0f} %",
              f"{flaeche:.1f} m2, Dichte {f(dichte)}, Streckung {f(streck)}, daneben {falsch * 100:.1f} %" + (f" ({', '.join(netze)})" if netze else ""),
              falsch <= ANTEIL_GRENZE)
        if raum == "kerker":
            # Gegenprobe: die Kerkermauer mit dreifach gestreckter UV muss anschlagen
            fl, fs, fd, d, s, fx = texeldichte(g, raumknoten, baender(), u_faktor=3.0)["kerker"]
            z("Kerker: Gegenprobe Texel (UV dreifach gestreckt)", "schlaegt an", f"daneben {fx * 100:.0f} %", fx > ANTEIL_GRENZE)
    return zeilen


def texel_raum(g, teile):
    """Je Band: Flaeche, Anteil daneben (gestreckt oder falsche Dichte), Mittel der Dichte und Streckung, Netze mit mehr als ANTEIL_GRENZE daneben."""
    summe = {}
    for n in teile:
        if n.startswith("Glas_"): continue
        for band, (a, fs, fd, d, s, fx) in texeldichte(g, n, baender()).items():
            e = summe.setdefault(band, [0.0, 0.0, 0.0, 0.0, []])
            e[0] += a; e[1] += fx * a; e[2] += d * a; e[3] += s * a
            if fx > ANTEIL_GRENZE: e[4].append(f"{n} {fx * 100:.0f} %")
    return {b: (e[0], e[1] / e[0], e[2] / e[0], e[3] / e[0], e[4]) for b, e in summe.items()}


def baender(name="BAENDER_RATHAUS_INNEN"):
    """Die Baender aus rathaus_trimsheet.py, ohne es zu laden (es braucht Blender): die Zuweisung als Literal gelesen."""
    import ast
    baum = ast.parse(open(os.path.join(HIER, "rathaus_trimsheet.py"), encoding="utf-8").read())
    return next(ast.literal_eval(k.value) for k in baum.body if isinstance(k, ast.Assign) and getattr(k.targets[0], "id", "") == name)


STRECKUNG_GRENZE = 1.5       # Verhaeltnis der Meter je UV-Einheit in u und v (1 = quadratische Texel)
DICHTE_GRENZE = 0.35          # Texeldichte hoechstens 35 % neben dem Soll des Bandes
FLAECHE_MIN = 0.02            # Dreiecke unter 0,02 m2 (Fasen, Kanten duenner Bretter) zaehlen nicht
ANTEIL_GRENZE = 0.02          # ein Band eines Knotens schlaegt an, wenn mehr als 2 % seiner Flaeche danebenliegen


def texeldichte(glb, knoten, baender, u_faktor=1.0):
    """Je Band des Trim-Sheets: Flaeche, Anteil gestreckt, Anteil mit falscher Dichte, Mittel der Dichte (Soll 1) und der Streckung.

    Fuer jedes Dreieck die Ableitungen dP/du und dP/dv (Meter je UV-Einheit). Soll: beide gleich den Metern je UV.x des Bandes
    (das Blatt ist quadratisch, die Baender sind so hoch wie ihr Streifen in Metern). Streckung = groessere / kleinere Ableitung,
    ein Dreieck ohne UV-Flaeche (auf eine Linie gelegt) zaehlt als unendlich gestreckt. Band nach der Mitte von v (glTF, von oben).
    Das Band "kleinteile" (Farbfelder) zaehlt nicht: dort liegen Kleinteile frei im Feld.
    """
    erg = {}
    for stoff, pos, idx, uv in glb.mit_uv(knoten):
        if stoff != "Innen" and stoff != "Haus": continue
        p0, p1, p2 = pos[idx[:, 0]], pos[idx[:, 1]], pos[idx[:, 2]]
        uv = uv * np.array([u_faktor, 1.0])                                     # Gegenprobe: u kuenstlich gestreckt
        t0, t1, t2 = uv[idx[:, 0]], uv[idx[:, 1]], uv[idx[:, 2]]
        e1, e2 = p1 - p0, p2 - p0
        d1, d2 = t1 - t0, t2 - t0
        flaeche = np.linalg.norm(np.cross(e1, e2), axis=1) / 2
        det = d1[:, 0] * d2[:, 1] - d2[:, 0] * d1[:, 1]
        sicher = np.where(np.abs(det) < 1e-12, 1e-12, det)
        dpdu = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) / sicher[:, None]
        dpdv = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) / sicher[:, None]
        lu, lv = np.linalg.norm(dpdu, axis=1), np.linalg.norm(dpdv, axis=1)
        streck = np.where(np.abs(det) < 1e-12, np.inf, np.maximum(lu, lv) / np.maximum(np.minimum(lu, lv), 1e-9))
        vm = (t0[:, 1] + t1[:, 1] + t2[:, 1]) / 3
        for name, (v0, v1, meter) in baender.items():
            if name == "kleinteile":
                # Farbfelder kacheln nicht: Ein Dreieck, das mehr als 10 % der Feldbreite (1/8 des Blatts) greift und dabei über 4 gestreckt ist,
                # zeigt die Struktur des Feldes als Streifen (Zellengitter, Klicktest 3). Kleine Flecken (eine Farbe) zählen nicht.
                s = (vm >= v0) & (flaeche >= 0.005)
                if not s.any(): continue
                tu = np.maximum(np.maximum(t0[:, 0], t1[:, 0]), t2[:, 0]) - np.minimum(np.minimum(t0[:, 0], t1[:, 0]), t2[:, 0])
                streifen = s & (tu > 0.0125) & (streck > 4)
                a = flaeche[s]
                anteil = flaeche[streifen].sum() / a.sum()
                erg["kleinteile"] = (a.sum(), anteil, 0.0, 1.0, float(np.average(np.clip(streck[s], 1, 10), weights=a)), anteil)
                continue
            s = (vm >= v0) & (vm < v1) & (flaeche >= FLAECHE_MIN)
            if not s.any(): continue
            a = flaeche[s]
            dichte = meter / np.maximum(np.sqrt(lu[s] * lv[s]), 1e-9)            # 1 = Soll
            falsch_s = streck[s] > STRECKUNG_GRENZE
            falsch_d = np.abs(dichte - 1) > DICHTE_GRENZE
            erg[name] = (a.sum(), a[falsch_s].sum() / a.sum(), a[falsch_d].sum() / a.sum(),
                         float(np.average(np.clip(dichte, 0, 10), weights=a)), float(np.average(np.clip(streck[s], 1, 10), weights=a)),
                         a[falsch_s | falsch_d].sum() / a.sum())
    return erg


def konsolen(pos, y0, y1, z_unten):
    """Grundrisse der Konsolen: Ecken auf Hoehe z_unten nahe einer Laengswand, nach x zusammengefasst -> (Wand hinten?, x0, x1)."""
    erg = []
    for hinten, yw in ((False, y0), (True, y1)):
        e = pos[(np.abs(pos[:, 2] - z_unten) < 0.005) & (np.abs(pos[:, 1] - yw) < 0.15)]
        xs = np.unique(np.round(e[:, 0], 3))
        gruppe = []
        for x in xs:
            if gruppe and x - gruppe[-1] > 0.30:      # Konsolen stehen mindestens 1,5 m auseinander
                erg.append((hinten, gruppe[0], gruppe[-1])); gruppe = []
            gruppe.append(x)
        if gruppe: erg.append((hinten, gruppe[0], gruppe[-1]))
    return erg


def konsole_frei(fuss, scheiben_r):
    """Liegt die Konsole mit AUFLAGER_ABSTAND neben jeder Scheibe derselben Wand? (Die Konsole reicht ueber die Fensteroberkante hinab.)"""
    hinten, a0, a1 = fuss
    for m, b, h in scheiben_r:
        if abs(m[1]) < 3.5 or (m[1] > 0) != hinten: continue      # Fenster der Giebelwand oder der anderen Laengswand
        if a1 > m[0] - b / 2 - M.AUFLAGER_ABSTAND and a0 < m[0] + b / 2 + M.AUFLAGER_ABSTAND:
            return False
    return True


def main():
    zeilen = pruefen()
    md = ["| Pruefung (gemessen in den GLBs) | Soll | Ist | |", "|---|---|---|---|"]
    md += [f"| {n} | {s} | {i} | {'ok' if ok else 'NEIN'} |" for n, s, i, ok in zeilen]
    text = "\n".join(md)
    print(text)
    for a in sys.argv[1:]:
        if a.startswith("--md="):
            open(a[5:], "w", encoding="utf-8").write("# Rathaus: Soll gegen Ist\n\n## Rechnung (rathaus_masse.py)\n\n" + M.tabelle() + "\n\n## Messung (rathaus_messen.py)\n\n" + text + "\n")
    sys.exit(0 if all(ok for *_, ok in zeilen) else 1)


if __name__ == "__main__":
    main()
