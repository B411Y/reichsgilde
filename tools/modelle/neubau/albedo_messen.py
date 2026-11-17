"""Helligkeit der Trim-Sheets je Band und der Haeuser aus der Ferne (Mensch-Nachtrag 16.11.2026: Schiefer des Steinhauses zu dunkel).

    blender -b --factory-startup --python tools/modelle/neubau/albedo_messen.py -- --blaetter=trim,trim_s2,trim_s3
            [--haeuser=wohnhaus_mensch_s1_g1:trim,...] [--texturen=<ordner>] [--ordner=<glb-ordner>]

Je Blatt und Band (trimsheet.BAENDER_AUSSEN): sRGB-Mittel (0-255, Mittel der drei Kanaele) und mittlere lineare Leuchtdichte
(Rec. 709 aus der linearisierten Albedo). Je Haus die Albedo „fern“: Mittel der linearen Leuchtdichte ueber alle Flaechen von Haus und
Dach, die auf dem Blatt liegen, gewichtet mit der Flaeche (Bildbereich der UV-Insel, 256 x 256 gemittelt). Naeherung: Im Spiel ersetzen
die Steinebenen (Steinebenen.cs) die Steinbaender; Flaechen mit fester Farbe (Glas, Eisen, Fahne) zaehlen nicht.
"""
import os
import sys

import bpy
import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
BAENDER = {
    "stroh": (0.0000, 0.1875), "stroh_franse": (0.1875, 0.2500), "balken_a": (0.2500, 0.3333), "balken_b": (0.3333, 0.4167),
    "balken_c": (0.4167, 0.5000), "putz": (0.5000, 0.7000), "planken": (0.7000, 0.7750), "farbholz": (0.7750, 0.8500),
    "bruchstein": (0.8500, 0.9500), "eisen": (0.9500, 1.0000)}
KLEIN = 256


def linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lade(pfad):
    """Albedo als Feld (Zeile 0 = oben im Bild), Werte 0..1 wie gespeichert (sRGB-kodiert)."""
    img = bpy.data.images.load(pfad)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    return px.reshape(h, w, 4)[::-1].copy()                       # Blender liefert die unterste Zeile zuerst


def baender(feld):
    h = feld.shape[0]
    lum = linear(feld[:, :, :3]) @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    zeilen = []
    for name, (a, e) in BAENDER.items():
        r0, r1 = int(round(a * h)), int(round(e * h))
        zeilen.append((name, float(feld[r0:r1, :, :3].mean() * 255), float(lum[r0:r1].mean())))
    return zeilen, lum


def haus_fern(glb, lum):
    """Flaechengewichtetes Mittel der Leuchtdichte von Haus und Dach (nur Flaechen mit Material auf dem Blatt)."""
    h = lum.shape[0]
    k = h // KLEIN
    klein = lum.reshape(KLEIN, k, KLEIN, k).mean(axis=(1, 3))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    flach = ("Holz", "Eisen", "Hausfarbe", "Fenster", "Bleinetz")      # Stoffe mit fester Farbe (nicht auf dem Blatt)
    summe, flaeche = 0.0, 0.0
    for o in bpy.data.objects:
        if o.type != "MESH" or o.name.split(".")[0] not in ("Haus", "Dach"): continue
        me = o.data
        uv = me.uv_layers[0].data
        for p in me.polygons:
            mat = me.materials[p.material_index].name.split(".")[0] if me.materials else ""
            if mat in flach: continue
            us = [uv[i].uv.x for i in p.loop_indices]; vs = [1.0 - uv[i].uv.y for i in p.loop_indices]   # Zeile von oben
            c0 = int(np.clip(min(us) % 1.0 * KLEIN, 0, KLEIN - 1)) if max(us) - min(us) < 1 else 0
            c1 = int(np.clip(max(us) % 1.0 * KLEIN, 0, KLEIN - 1)) if max(us) - min(us) < 1 else KLEIN - 1
            r0, r1 = int(np.clip(min(vs) * KLEIN, 0, KLEIN - 1)), int(np.clip(max(vs) * KLEIN, 0, KLEIN - 1))
            if c1 < c0: c0, c1 = 0, KLEIN - 1
            wert = float(klein[r0:r1 + 1, c0:c1 + 1].mean())
            a = p.area * (o.matrix_world.to_scale().x ** 2)
            summe += wert * a; flaeche += a
    return summe / max(flaeche, 1e-9)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    texturen = opt.get("texturen", os.path.join(WURZEL, "game", "assets", "texturen", "neubau"))
    ordner = opt.get("ordner", os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau"))
    lums = {}
    print("ALBEDO Blatt | Band | sRGB-Mittel | linear")
    for blatt in opt.get("blaetter", "trim,trim_s2,trim_s3").split(","):
        zeilen, lums[blatt] = baender(lade(os.path.join(texturen, f"{blatt}_albedo.png")))
        for name, s, l in zeilen:
            print(f"ALBEDO {blatt} | {name} | {s:.0f} | {l:.3f}" + ("  DUNKEL" if l < 0.03 else ""))
    for eintrag in [e for e in opt.get("haeuser", "").split(",") if e]:
        haus, blatt = eintrag.split(":")
        if blatt not in lums:
            _z, lums[blatt] = baender(lade(os.path.join(texturen, f"{blatt}_albedo.png")))
        print(f"ALBEDO Haus {haus} ({blatt}) fern: {haus_fern(os.path.join(ordner, haus + '.glb'), lums[blatt]):.3f}")


if __name__ == "__main__":
    main()
