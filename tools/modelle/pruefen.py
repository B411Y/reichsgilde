"""Prüft eine GLB-Datei gegen die Konventionen der Gebäudemodelle. Braucht kein Blender.

    python tools/modelle/pruefen.py game/assets/modelle/fachwerkhaus.glb 8 10

Gibt Knoten, Materialien, Dreiecke je Knoten und die Ausdehnung (Y oben, Vorderseite +Z) aus
und meldet Verstöße: Material „Hausfarbe“ fehlt, Modell ragt über die belegte Fläche oder
unter den Boden, ein Zustandsknoten fehlt. Hat das Modell Stufen (Werkstatt), müssen es genau
Stufe1–Stufe3 sein; dann steht auch da, wie viele Dreiecke je Stufe sichtbar sind (Haus + Stufe).
"""

import json
import struct
import sys

ZUSTAENDE = ("Leerstand", "Geruest")


def lesen(pfad):
    with open(pfad, "rb") as f:
        daten = f.read()
    laenge = struct.unpack_from("<I", daten, 12)[0]
    return json.loads(daten[20:20 + laenge])


def main():
    pfad = sys.argv[1]
    breite, tiefe = (float(a) for a in sys.argv[2:4]) if len(sys.argv) >= 4 else (None, None)
    g = lesen(pfad)
    fehler = []
    materialien = [m["name"] for m in g.get("materials", [])]
    print("Materialien:", ", ".join(materialien))
    if "Hausfarbe" not in materialien:
        fehler.append("Material „Hausfarbe“ fehlt")

    mins, maxs = [1e9] * 3, [-1e9] * 3
    print(f"{'Knoten':12s} {'Dreiecke':>8s}  Extras")
    namen = []
    dreiecke_je = {}
    summe = 0
    for knoten in g["nodes"]:
        if "mesh" not in knoten:
            continue
        namen.append(knoten["name"])
        dreiecke = 0
        for prim in g["meshes"][knoten["mesh"]]["primitives"]:
            if "indices" in prim:
                dreiecke += g["accessors"][prim["indices"]]["count"] // 3
            pos = g["accessors"][prim["attributes"]["POSITION"]]
            versatz = knoten.get("translation", [0, 0, 0])
            for i in range(3):
                mins[i] = min(mins[i], pos["min"][i] + versatz[i])
                maxs[i] = max(maxs[i], pos["max"][i] + versatz[i])
        summe += dreiecke
        dreiecke_je[knoten["name"]] = dreiecke
        print(f"{knoten['name']:12s} {dreiecke:8d}  {knoten.get('extras', '')}")
    print(f"{'gesamt':12s} {summe:8d}")
    print("Ausdehnung X {:.2f} … {:.2f}   Y {:.2f} … {:.2f}   Z {:.2f} … {:.2f}".format(
        mins[0], maxs[0], mins[1], maxs[1], mins[2], maxs[2]))

    stufen = sorted(n for n in namen if n.startswith("Stufe"))
    if stufen:
        if stufen != ["Stufe1", "Stufe2", "Stufe3"]:
            fehler.append("Stufen müssen genau Stufe1, Stufe2, Stufe3 heißen, gefunden: " + ", ".join(stufen))
        fest = sum(n for k, n in dreiecke_je.items() if k in ("Haus", "Wappen", "Fahne"))
        print("sichtbar je Stufe (Haus, Wappen, Fahne + Stufe): " +
              ", ".join(f"{k} {fest + dreiecke_je[k]}" for k in stufen))
    for z in ZUSTAENDE:
        if z not in namen:
            fehler.append(f"Zustandsknoten „{z}“ fehlt")
    if mins[1] < -0.06:
        fehler.append(f"ragt {-mins[1]:.2f} m unter den Boden")
    if breite is not None:
        if max(-mins[0], maxs[0]) > breite / 2 + 1e-3:
            fehler.append(f"breiter als {breite} m (X bis ±{max(-mins[0], maxs[0]):.2f})")
        if max(-mins[2], maxs[2]) > tiefe / 2 + 1e-3:
            fehler.append(f"tiefer als {tiefe} m (Z bis ±{max(-mins[2], maxs[2]):.2f})")
    print("in Ordnung" if not fehler else "FEHLER:\n  " + "\n  ".join(fehler))
    sys.exit(1 if fehler else 0)


if __name__ == "__main__":
    main()
