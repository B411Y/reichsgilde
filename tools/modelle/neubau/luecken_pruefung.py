"""Erweiterung von Pruefung 12 (Rueckmeldung des Nutzers 15.11.2026: schwarze Spalten am Giebel neben dem Giebelfenster und
zwischen zwei Fenstern). Drei Pruefungen je Huellen-GLB, jede meldet Fundstellen mit Lage in Godot-Koordinaten (x, Hoehe, z;
+z = Gasse) und Groesse:

    blender -b --factory-startup --python tools/modelle/neubau/luecken_pruefung.py -- --glb=<huelle.glb> [--glb=...]
            [--raster=0.01] [--min=0.02] [--bild=<ordner>] [--nur=band]

1. luecken(): Loecher in der Huelle. Strahlen im Raster (Standard 1 cm) von allen vier Seiten, senkrecht auf die Fassade und schraeg
   wie die Spielkamera (40 Grad von oben, 20 Grad seitlich). "Durchblick": Ein Strahl trifft mehr als TIEF hinter der Fassade
   ringsum, und in allen vier Richtungen liegt bis UMKREIS Wand – man sieht durch die Wand ins Innere. "Rueckseite": Ein Strahl trifft
   zuerst die Rueckseite einer Flaeche (offenes Bauteil, gekippte Normale, fehlende Rueckwand; in Godot unsichtbar oder schwarz).
   Gemeldet ab --min (2 cm) in Breite und Hoehe. --bild: Trefferkarte je Blickrichtung (rot Durchblick, orange Rueckseite).
2. band_ueberlauf(): Eine Flaeche reicht mit ihrer UV ueber den Rand ihres Bandes ins Nachbarband (ab UEBERLAUF_MIN).
3. eisen_spalten(): Ein flaches Eisenteil (Band "eisen", fast schwarz) steht senkrecht, schmal und lang direkt auf Mauerwerk – im
   Bild ein schwarzer Spalt. So die Ankereisen am Giebel der Steinhaeuser (Ursache der gemeldeten Spalten neben dem Giebelfenster).
Ausgenommen: Knoten der Zustaende und Anker (Leerstand, Geruest, Bodenfleck, Platz_, Blick_, Anker_, Gegenstand_), Stoffe ohne
geschlossene Huelle (Bleinetz, Strohkante) und in 2./3. die Stoffe ohne Trim-Sheet. Rueckgabe je Pruefung (Zeilen, Fehlerzahl,
Funde); allein: Ausgabe mit Praefix LUECKE, Rueckgabewert 1 bei Funden. --nur=band: nur 2. und 3. (schnell).
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

AUSNAHME_KNOTEN = ("Leerstand", "Geruest", "Gerüst", "Bodenfleck", "Platz_", "Blick_", "Anker_", "Gegenstand_")
AUSNAHME_STOFFE = ("Bleinetz", "Strohkante")
# Seiten in Godot-Richtung (glTF +Z = Gasse = Blender -Y); Lagen werden in Godot-Koordinaten gemeldet (x, Hoehe y, z = -Blender-y)
SEITEN = {"vorn (+Z, Gasse)": Vector((0, -1, 0)), "hinten (-Z)": Vector((0, 1, 0)), "rechts (+X)": Vector((1, 0, 0)), "links (-X)": Vector((-1, 0, 0))}
TIEF = 0.5          # Durchblick: Treffer mehr als 0,5 m hinter der Fassade ringsum (dickste Mauer 0,5 m; Fenster und Tueren liegen flacher)
NAH = 0.15          # "Wand": Treffer hoechstens 15 cm hinter der oertlichen Fassade
UMKREIS = 0.6       # Fassade ringsum = naechster Treffer im Umkreis von 60 cm; eingeschlossen = Wand in allen vier Richtungen bis 60 cm


def _laden(glb):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    teile = []
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        weg = False
        k = o
        while k is not None:
            if k.name.split(".")[0].startswith(AUSNAHME_KNOTEN):
                weg = True
            k = k.parent
        if not weg and o.visible_get() is not False:
            teile.append(o)
    return teile


def _baum(teile):
    punkte, flaechen, herkunft = [], [], []
    for o in teile:
        mw = o.matrix_world
        basis = len(punkte)
        punkte.extend(mw @ v.co for v in o.data.vertices)
        for p in o.data.polygons:
            stoff = o.data.materials[p.material_index].name.split(".")[0] if o.data.materials and o.data.materials[p.material_index] else ""
            if stoff in AUSNAHME_STOFFE:
                continue
            flaechen.append([basis + i for i in p.vertices])
            herkunft.append((o.name.split(".")[0], stoff))
    return BVHTree.FromPolygons(punkte, flaechen, all_triangles=False), herkunft, np.array([list(p) for p in punkte])


def _gruppen(maske):
    """Zusammenhaengende Bereiche (4er-Nachbarschaft) einer Bool-Karte: Liste von (zeilen, spalten)."""
    gesehen = np.zeros_like(maske, dtype=bool)
    gruppen = []
    h, w = maske.shape
    for z0, s0 in zip(*np.nonzero(maske)):
        if gesehen[z0, s0]:
            continue
        stapel, zs, ss = [(z0, s0)], [], []
        gesehen[z0, s0] = True
        while stapel:
            z, s = stapel.pop()
            zs.append(z); ss.append(s)
            for dz, ds in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nz, ns = z + dz, s + ds
                if 0 <= nz < h and 0 <= ns < w and maske[nz, ns] and not gesehen[nz, ns]:
                    gesehen[nz, ns] = True
                    stapel.append((nz, ns))
        gruppen.append((np.array(zs), np.array(ss)))
    return gruppen


def _schieben(feld, dz, ds, fuell):
    """Feld um (dz, ds) Zellen verschoben; frei werdende Zellen = fuell."""
    aus = np.full_like(feld, fuell)
    h, w = feld.shape
    aus[max(dz, 0):h + min(dz, 0), max(ds, 0):w + min(ds, 0)] = feld[max(-dz, 0):h + min(-dz, 0), max(-ds, 0):w + min(-ds, 0)]
    return aus


def _richtungen():
    """Blickrichtungen je Seite: senkrecht auf die Fassade und schraeg wie die Spielkamera (40 Grad von oben, 20 Grad seitlich
    nach beiden Seiten) – manche Spalten (versetzte Wandstuecke) sieht man nur schraeg."""
    aus = []
    for seite, n in SEITEN.items():
        aus.append((seite + " senkrecht", -n))
        for gier in (-20.0, 20.0):
            d = -n.copy()
            d.rotate(Matrix.Rotation(math.radians(gier), 3, "Z"))
            d = (d * math.cos(math.radians(40.0)) + Vector((0, 0, -math.sin(math.radians(40.0))))).normalized()
            aus.append((seite + f" schraeg {gier:+.0f}°", d))
    return aus


def luecken(glb, raster=0.01, min_groesse=0.02, bild=None):
    """Zwei Arten (beide Fehler, je ab min_groesse in Breite und Hoehe der Rasterflaeche quer zum Strahl):
    - Durchblick: Der Strahl trifft mehr als TIEF hinter der Fassade ringsum, und in allen vier Richtungen liegt bis UMKREIS
      Wand (hoechstens NAH hinter der Fassade) – ein Loch in der Wand, durch das man auf die Innenseite des Hauses sieht.
    - Rueckseite: Der Strahl trifft zuerst die Rueckseite einer Flaeche (offenes Bauteil, gekippte Normale, fehlende Rueckwand)."""
    teile = _laden(glb)
    baum, herkunft, punkte = _baum(teile)
    lo, hi = punkte.min(axis=0), punkte.max(axis=0)
    mitte = Vector(((lo + hi) / 2).tolist())
    radius = float(np.linalg.norm(hi - lo)) / 2 + 1.0
    ecken = [Vector((x, y, z)) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]
    funde, zeilen = [], []
    r = max(1, int(round(UMKREIS / raster)))
    for name, d in _richtungen():
        u = d.cross(Vector((0, 0, 1))).normalized()
        v = u.cross(d).normalized()                       # zeigt nach oben
        us = [(e - mitte).dot(u) for e in ecken]; vs = [(e - mitte).dot(v) for e in ecken]
        ua = np.arange(min(us) + raster / 2, max(us), raster)
        va = np.arange(min(vs) + raster / 2, max(vs), raster)
        tiefe = np.full((len(va), len(ua)), np.inf, dtype=np.float32)
        rueck = np.zeros((len(va), len(ua)), dtype=bool)
        wer = {}
        basis = mitte - d * radius
        for i, b in enumerate(va):
            for j, a in enumerate(ua):
                o = basis + u * float(a) + v * float(b)
                ort, normale, idx, dist = baum.ray_cast(o, d, 2 * radius)
                if ort is None:
                    continue
                tiefe[i, j] = dist
                wer[(i, j)] = herkunft[idx]
                if normale.dot(d) > 1e-4:
                    rueck[i, j] = True
        fassade = tiefe.copy()
        for k in range(1, r + 1):
            for dz, ds in ((0, k), (0, -k)):
                fassade = np.minimum(fassade, _schieben(tiefe, dz, ds, np.inf))
        f2 = fassade.copy()
        for k in range(1, r + 1):
            for dz, ds in ((k, 0), (-k, 0)):
                f2 = np.minimum(f2, _schieben(fassade, dz, ds, np.inf))
        fassade = f2
        wand = np.isfinite(tiefe) & (tiefe <= fassade + NAH) & ~rueck
        tief = (tiefe > fassade + TIEF) & np.isfinite(fassade)
        umschlossen = np.ones_like(wand)
        for dz, ds in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            irgendwo = np.zeros_like(wand)
            for k in range(1, r + 1):
                irgendwo |= _schieben(wand, dz * k, ds * k, False)
            umschlossen &= irgendwo
        durchblick = tief & umschlossen
        for art, maske in (("Durchblick", durchblick), ("Rueckseite", rueck)):
            for zs, ss in _gruppen(maske):
                breite = (ss.max() - ss.min() + 1) * raster
                hoehe = (zs.max() - zs.min() + 1) * raster
                if breite < min_groesse or hoehe < min_groesse:
                    continue
                # Eintrittsstelle in die Fassade (Welt, Blender), dann Godot-Koordinaten (x, Hoehe, z = -Blender-y)
                stellen = [basis + u * float(ua[s]) + v * float(va[z]) + d * float(fassade[z, s] if np.isfinite(fassade[z, s]) else tiefe[z, s])
                           for z, s in zip(zs, ss)]
                p = sum(stellen, Vector()) / len(stellen)
                ringsum = set()
                for z, s in zip(zs, ss):
                    for dz, ds in ((0, -2), (0, 2), (-2, 0), (2, 0)):
                        nz, ns = z + dz, s + ds
                        if 0 <= nz < wand.shape[0] and 0 <= ns < wand.shape[1] and wand[nz, ns]:
                            ringsum.add(wer[(nz, ns)])
                innen = sorted({wer[(z, s)] for z, s in zip(zs, ss) if (z, s) in wer})
                funde.append(dict(art=art, blick=name, godot=(p.x, p.z, -p.y), breite=breite, hoehe=hoehe,
                                  flaeche=len(zs) * raster * raster, innen=innen[:3], ringsum=sorted(ringsum)[:6],
                                  tiefe=float(np.median(tiefe[zs, ss] - fassade[zs, ss]))))
        if bild and (durchblick.any() or rueck.any()):
            os.makedirs(bild, exist_ok=True)
            farbe = np.zeros(tiefe.shape + (4,), dtype=np.float32); farbe[..., 3] = 1
            farbe[np.isfinite(tiefe), :3] = 0.6
            farbe[wand, :3] = 0.75
            farbe[rueck, :3] = (1.0, 0.6, 0.0)
            farbe[durchblick, :3] = (1.0, 0.0, 0.0)
            b = bpy.data.images.new("karte", tiefe.shape[1], tiefe.shape[0], alpha=True)
            b.pixels.foreach_set(farbe.ravel())
            kurz = name.replace(" ", "_").replace("°", "").replace("(", "").replace(")", "").replace(",", "")
            b.filepath_raw = os.path.join(bild, f"{os.path.splitext(os.path.basename(glb))[0]}_{kurz}.png")
            b.file_format = "PNG"; b.save(); bpy.data.images.remove(b)
    # dieselbe Stelle aus mehreren Blickrichtungen: zusammenfassen (Abstand der Mitten unter 10 cm, gleiche Art)
    einzeln = []
    for f in sorted(funde, key=lambda f: -f["flaeche"]):
        if any(g["art"] == f["art"] and (Vector(g["godot"]) - Vector(f["godot"])).length < 0.10 for g in einzeln):
            next(g for g in einzeln if g["art"] == f["art"] and (Vector(g["godot"]) - Vector(f["godot"])).length < 0.10)["blicke"].append(f["blick"])
            continue
        f["blicke"] = [f["blick"]]
        einzeln.append(f)
    zeilen.append(f"   {os.path.basename(glb)}: {len(teile)} Teile, Raster {raster * 100:.0f} cm, Mindestgroesse {min_groesse * 100:.0f} cm, "
                  f"{len(_richtungen())} Blickrichtungen")
    for f in sorted(einzeln, key=lambda f: (f["art"], -f["flaeche"])):
        x, y, z = f["godot"]
        zeilen.append(f"   FEHLER {f['art']} bei x {x:+.2f}, Hoehe {y:.2f}, z {z:+.2f} m: {f['breite'] * 100:.0f} x {f['hoehe'] * 100:.0f} cm "
                      f"({f['flaeche'] * 1e4:.0f} cm2, {f['tiefe']:.2f} m hinter der Fassade); dahinter {f['innen']}; ringsum {f['ringsum']}; "
                      f"gesehen {', '.join(f['blicke'][:3])}")
    return zeilen, len(einzeln), einzeln

TRIM_FREI = ("Fenster", "Bleinetz", "Hausfarbe", "Holz", "Eisen", "Innenglas", "Glas")   # Stoffe ohne Trim-Sheet (wie uv2_pruefung.py)
UEBERLAUF_MIN = 0.01   # m: so weit darf eine Flaeche ueber den Rand ihres Bandes im Blatt hinausreichen (Filterrand)


def _layout(datei=None, name="BAENDER_AUSSEN"):
    """Bandgrenzen aus trimsheet.py lesen, ohne es auszufuehren (es braucht Blender-Bildfunktionen)."""
    import ast
    datei = datei or os.path.join(os.path.dirname(os.path.abspath(__file__)), "trimsheet.py")
    with open(datei, encoding="utf-8") as f:
        for k in ast.parse(f.read()).body:
            if isinstance(k, ast.Assign) and any(getattr(z, "id", None) == name for z in k.targets):
                return ast.literal_eval(k.value)
    raise SystemExit(f"{name} nicht in {datei}")


def band_ueberlauf(glb, baender=None):
    """Schwarze Streifen ohne Loch: Eine Flaeche liegt mit ihrer UV ueber den Rand ihres Bandes hinaus und zeigt dort das
    Nachbarband (am Giebel der Steinhaeuser: Haustein bis ins Eisenband, v ueber 0,95 = fast schwarz). Je Flaeche mit Trim-Stoff:
    Band = das Band, in dem die Mitte ihrer UV liegt; Ueberlauf = wie weit v darueber hinausgeht, in Metern (Bandbreite je UV.x,
    die Baender sind im Blatt gleich dicht). Gemeldet ab UEBERLAUF_MIN. Rueckgabe (Zeilen, Fehlerzahl, Funde)."""
    baender = baender or _layout()
    teile = _laden(glb)
    funde = []
    for o in teile:
        me = o.data
        if not me.uv_layers:
            continue
        uv = me.uv_layers[0].data
        mw = o.matrix_world
        for p in me.polygons:
            stoff = me.materials[p.material_index].name.split(".")[0] if me.materials and me.materials[p.material_index] else ""
            if stoff in TRIM_FREI or stoff in AUSNAHME_STOFFE:
                continue
            vs = [1.0 - uv[li].uv.y for li in p.loop_indices]     # Godot-v (oben 0)
            mitte = sum(vs) / len(vs)
            band = next(((n, b) for n, b in baender.items() if b[0] <= mitte < b[1]), None)
            if band is None:
                continue
            n, (v0, v1, meter) = band
            ueber = max(max(vs) - v1, v0 - min(vs), 0.0) * meter
            if ueber < UEBERLAUF_MIN:
                continue
            nach = next((m for m, b in baender.items() if (max(vs) > v1 and b[0] <= max(vs) < b[1]) or (min(vs) < v0 and b[0] <= min(vs) < b[1])), "?")
            c = mw @ p.center
            funde.append(dict(teil=o.name.split(".")[0], stoff=stoff, band=n, nach=nach, ueber=ueber, godot=(c.x, c.z, -c.y),
                              flaeche=p.area * mw.median_scale ** 2, v=(min(vs), max(vs))))
    zeilen = [f"   {os.path.basename(glb)}: Bandueberlauf (Bandgrenzen aus trimsheet.py), Grenze {UEBERLAUF_MIN * 100:.0f} cm"]
    for f in sorted(funde, key=lambda f: -f["ueber"]):
        x, y, z = f["godot"]
        zeilen.append(f"   FEHLER Bandueberlauf {f['teil']}/{f['stoff']} bei x {x:+.2f}, Hoehe {y:.2f}, z {z:+.2f} m: Band {f['band']} "
                      f"um {f['ueber'] * 100:.0f} cm ins Band {f['nach']} (v {f['v'][0]:.3f}..{f['v'][1]:.3f}, Flaeche {f['flaeche'] * 1e4:.0f} cm2)")
    return zeilen, len(funde), funde

SPALT_BREITE, SPALT_LAENGE, SPALT_ABSTAND = 0.08, 0.30, 0.015   # m: schmal, lang, flach vor der Wand
HOLZ_STOFFE = ("Fachwerk", "Holz")                                 # Beschlaege auf Holz (Laeden, Tueren) sind gewollt


def eisen_spalten(glb, baender=None):
    """Was im Bild wie ein schwarzer Spalt aussieht, ohne einer zu sein: ein flaches Eisenteil (Band "eisen" des Blatts, Albedo
    fast schwarz) auf einem Stein- oder Holzstoff, schmal (hoechstens SPALT_BREITE) und lang (mindestens SPALT_LAENGE), senkrecht
    und hoechstens SPALT_ABSTAND vor der Wand dahinter – so die Ankereisen am Giebel der Steinhaeuser (Rueckmeldung 15.11.2026).
    Rueckgabe (Zeilen, Fehlerzahl, Funde)."""
    baender = baender or _layout()
    e0, e1, _m = baender["eisen"]
    teile = _laden(glb)
    baum, _herkunft, _p = _baum(teile)
    funde = []
    for o in teile:
        me = o.data
        if not me.uv_layers:
            continue
        uv = me.uv_layers[0].data
        mw = o.matrix_world
        for p in me.polygons:
            stoff = me.materials[p.material_index].name.split(".")[0] if me.materials and me.materials[p.material_index] else ""
            if stoff in TRIM_FREI or stoff in AUSNAHME_STOFFE:
                continue
            vs = [1.0 - uv[li].uv.y for li in p.loop_indices]
            if not all(e0 - 1e-4 <= v <= e1 + 1e-4 for v in vs):
                continue
            n = (mw.to_3x3() @ p.normal).normalized()
            if abs(n.z) > 0.2:
                continue
            pts = [mw @ me.vertices[i].co for i in p.vertices]
            quer = Vector((-n.y, n.x, 0)).normalized()
            breite = max(q.dot(quer) for q in pts) - min(q.dot(quer) for q in pts)
            hoehe = max(q.z for q in pts) - min(q.z for q in pts)
            if breite > SPALT_BREITE or hoehe < SPALT_LAENGE:      # senkrecht stehende schmale Streifen (waagerechte Baender auf Laeden: nein)
                continue
            c = mw @ p.center
            ort, _nn, idx, d = baum.ray_cast(c - n * 0.001, -n, 0.2)
            if ort is None or d > SPALT_ABSTAND or _herkunft[idx][1] in HOLZ_STOFFE:   # nur auf Mauerwerk, nicht auf Holz
                continue
            if any(math.hypot(g["godot"][0] - c.x, g["godot"][2] + c.y) < 0.05 and abs(g["godot"][1] - c.z) < hoehe for g in funde):
                continue                                                   # zweites Dreieck desselben Eisens
            funde.append(dict(teil=o.name.split(".")[0], stoff=stoff, godot=(c.x, c.z, -c.y), breite=breite, hoehe=hoehe, abstand=d,
                              wand=_herkunft[idx][1]))
    zeilen = [f"   {os.path.basename(glb)}: Eisen flach auf der Wand (liest sich als Spalt): schmal bis {SPALT_BREITE * 100:.0f} cm, "
              f"lang ab {SPALT_LAENGE * 100:.0f} cm, bis {SPALT_ABSTAND * 100:.1f} cm vor der Wand"]
    for f in funde:
        x, y, z = f["godot"]
        zeilen.append(f"   FEHLER Eisenspalt {f['teil']}/{f['stoff']} bei x {x:+.2f}, Hoehe {y:.2f}, z {z:+.2f} m: "
                      f"{f['breite'] * 100:.0f} x {f['hoehe'] * 100:.0f} cm (breit x hoch), {f['abstand'] * 100:.1f} cm vor der Wand ({f['wand']})")
    return zeilen, len(funde), funde

def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    glbs = [a.split("=", 1)[1] for a in args if a.startswith("--glb=")]
    opt = dict(a[2:].split("=", 1) for a in args if a.startswith("--") and not a.startswith("--glb="))
    gesamt = 0
    for glb in glbs:
        for pruefung, was in ((band_ueberlauf, "Flaechen mit Bandueberlauf"), (eisen_spalten, "Eisenteile, die wie Spalten aussehen")):
            z0, f0, _ = pruefung(glb)
            print(f"LUECKE {os.path.basename(glb)}: {f0} {was}")
            for zeile in z0:
                print("LUECKE" + zeile)
            gesamt += f0
        if opt.get("nur") == "band":
            continue
        z, f, _ = luecken(glb, float(opt.get("raster", 0.01)), float(opt.get("min", 0.02)), opt.get("bild"))
        gesamt += f
        print(f"LUECKE {os.path.basename(glb)}: {f} Fundstellen")
        for zeile in z:
            print("LUECKE" + zeile)
    print(f"LUECKE gesamt: {gesamt}")
    return 1 if gesamt else 0


if __name__ == "__main__":
    sys.exit(main())
