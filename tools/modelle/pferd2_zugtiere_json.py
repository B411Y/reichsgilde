"""Schreibt den Block "pferd" in game/daten/zugtiere.json fuer das Pferd B (Horse von henrysteve973, CC BY 4.0): Zugpunkt aus pferd2_geschirr.py, Gehen/Ziehen aus den Bildfolgen
(26 Bilder + Schlussbild, 1,0833 s, 1,3 m/s). Die uebrigen Clips (Anfahren, Anhalten, Stehen, Fressen) behalten Bilder, Dauer, Weg und Anlauf; der Ochse bleibt unveraendert.

    python -I tools/modelle/pferd2_zugtiere_json.py <geschirr_info.json> [<zugtiere.json>]
"""
import json
import sys

info = json.load(open(sys.argv[1], encoding="utf-8"))
pfad = sys.argv[2] if len(sys.argv) > 2 else "game/daten/zugtiere.json"
z = json.load(open(pfad, encoding="utf-8"))
p = z["tiere"]["pferd"]
FPS, V, ZYKLUS_BILDER = 24, 1.3, 26
anker = [round(info["anker_ziehen_godot"][0], 3), round(info["anker_ziehen_godot"][1], 3), round(info["anker_ziehen_godot"][2], 3)]
p["anker_lokal_godot"] = anker
p["anker_ziehen_godot"] = anker
p["schleife"] = round(ZYKLUS_BILDER / FPS, 4)
p["schrittlaenge"] = round(V * ZYKLUS_BILDER / FPS, 3)
p["tempo"] = V
for name in ("Gehen", "Ziehen"):
    k = p["klips"][name]
    n = ZYKLUS_BILDER + 1
    k["bilder"], k["dauer"], k["schleife"] = n, round(ZYKLUS_BILDER / FPS, 4), True
    k["weg"] = [round(V * i / FPS, 5) for i in range(n)]
    k["anlauf"] = [1.0] * n
json.dump(z, open(pfad, "w", encoding="utf-8", newline="\n"), indent=1, ensure_ascii=False)
open(pfad, "a", encoding="utf-8", newline="\n").write("\n")
print("pferd: Anker", anker, "Gehen/Ziehen", ZYKLUS_BILDER + 1, "Bilder")
