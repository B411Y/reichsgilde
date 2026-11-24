"""Pruefung 40 „offene Kanten“ an den Huellen der Wohnhaeuser (Nutzerbefund 23.11.2026: schwarze Flecken = Luecken und nicht verbundene Polygone an Fenstern und
Giebelverzierungen).

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_kanten_messen.py -- --haus=wohnhaus_zwerg_s3_g1 [--ordner=<glb-ordner>] [--csv=<datei>]

Liest `<haus>.glb`, verschweisst Eckpunkte innerhalb von 1 mm und zaehlt je Netz (Haus, Dach, Scheiben):
  * offene Kanten: eine Kante gehoert nur zu einer Flaeche (Rand). Offene Kanten sind in einer Huelle aus einseitigen Waenden zum Teil gewollt
    (Wandenden an Boden und Dachunterseite), darum wird getrennt: Kanten am Boden (z <= 5 cm) zaehlen nicht; alle anderen sind „frei“;
  * davon „lose“: Kanten, deren beide Endpunkte NUR zu Flaechen dieser Kante gehoeren (nicht verschweisste Einzelteile, z. B. ein Stab ohne Kappen);
  * Nicht-Mannigfaltige Kanten (mehr als zwei Flaechen), Zahl.
Ausgabe: eine Zeile je Netz und die zehn groessten Haeufungen freier Kanten (Raster 0,25 m) mit Lage in Weltkoordinaten (Blender, z oben). Ergebnis nur zur
Kenntnis (kein Fehler, solange die Prueferliste nicht abgearbeitet ist); mit --csv eine Zeile je Haus."""
import collections
import os
import sys

import bpy


def lesen(glb):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)


def kanten(obj):
    me = obj.data
    mw = obj.matrix_world
    # Eckpunkte verschweissen (1 mm)
    schluessel = {}
    ids = []
    pos = []
    for v in me.vertices:
        p = mw @ v.co
        k = (round(p.x * 1000), round(p.y * 1000), round(p.z * 1000))
        if k not in schluessel:
            schluessel[k] = len(pos); pos.append(p)
        ids.append(schluessel[k])
    kante = collections.defaultdict(int)
    flaechen_je_punkt = collections.defaultdict(set)
    for fi, p in enumerate(me.polygons):
        vs = [ids[i] for i in p.vertices]
        for i, a in enumerate(vs):
            b = vs[(i + 1) % len(vs)]
            if a == b: continue
            kante[(min(a, b), max(a, b))] += 1
            flaechen_je_punkt[a].add(fi); flaechen_je_punkt[b].add(fi)
    offen = [k for k, n in kante.items() if n == 1]
    mehr = [k for k, n in kante.items() if n > 2]
    frei = [k for k in offen if max(pos[k[0]].z, pos[k[1]].z) > 0.05]
    # lose Kanten: alle Flaechen an beiden Endpunkten sind dieselben wie an der Kante (Einzelteil ohne Anschluss)
    lose = 0
    for a, b in frei:
        if len(flaechen_je_punkt[a] | flaechen_je_punkt[b]) <= 3: lose += 1
    return pos, offen, frei, mehr, lose


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    haus = opt["haus"]
    HIER = os.path.dirname(os.path.abspath(__file__))
    ordner = opt.get("ordner") or os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(HIER))), "game", "assets", "modelle", "gebaeude", "neubau")
    lesen(os.path.join(ordner, haus + ".glb"))
    summe = 0
    zeile = [haus]
    for name in ("Haus", "Dach", "Scheiben"):
        o = next((x for x in bpy.data.objects if x.type == "MESH" and x.name.split(".")[0] == name), None)
        if o is None: continue
        pos, offen, frei, mehr, lose = kanten(o)
        summe += len(frei)
        print(f"MESSUNG 40. Offene Kanten {name}: {len(offen)} am Rand, davon {len(frei)} frei (nicht am Boden), {lose} lose, {len(mehr)} mit mehr als zwei Flaechen")
        zeile += [name, len(offen), len(frei), lose, len(mehr)]
        if name == "Haus":
            haufen = collections.Counter()
            for a, b in frei:
                m = (pos[a] + pos[b]) / 2
                haufen[(round(m.x / 0.25) * 0.25, round(m.y / 0.25) * 0.25, round(m.z / 0.25) * 0.25)] += 1
            for (x, y, z), n in haufen.most_common(10):
                print(f"MESSUNG    freie Kanten {n:3d} bei x {x:+.2f}, y {y:+.2f}, z {z:+.2f}")
    print(f"MESSUNG 40. Summe freier offener Kanten: {summe}")
    if "csv" in opt:
        with open(opt["csv"], "a", encoding="utf-8") as f: f.write(";".join(str(z) for z in zeile) + "\n")


if __name__ == "__main__":
    main()
