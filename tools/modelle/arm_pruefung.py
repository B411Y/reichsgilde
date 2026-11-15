"""Arme im Stehen und Gehen: Abspreizwinkel des Oberarms (ohne Blender; CPU-Pose aus tools/modelle/sitzpruefung.py)

    python tools/modelle/arm_pruefung.py [--ordner=game/assets/modelle/figuren/probe] [--clips=Stehen,Gehen,Reden,Sitzen] [--grenze=35]

Je Figurdatei und Clip, über 13 Zeitpunkte der Schleife: Winkel zwischen Oberarm (UpperArm → LowerArm) und der Senkrechten nach unten,
im Mittel und höchstens, je Seite. Grenzwert für das Mittel: GRENZE (Grad; Stehen, Gehen, Reden und Sitzen der Dateien liegen bei
5–28°, das alte Rig im Stehen bei 6°). Der Clipname „Ruhelage“ misst die Bindepose der Datei ohne Clip (42°: so stand eine Figur, deren Skelett
das Spiel nicht stellte – Gegenprobe). Im Spiel misst dasselbe die Prüfhilfe --armprobe (Stadtansicht.Pruefhilfe.cs).
Rückgabewert 1, wenn ein Mittel darüber liegt.
"""
import math
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import sitzpruefung as sp  # noqa: E402

GRENZE = 35.0
CLIPS = ("Stehen", "Gehen", "Reden", "Sitzen")   # Tragen hält die Kiste vor dem Bauch (44°), Arbeitsclips führen Werkzeug


def winkel(g, clip, zeit):
    welt = g.welt() if clip == "Ruhelage" else g.welt(g.pose(clip, zeit))
    erg = {}
    for seite in ("Left", "Right"):
        a = welt[g.knoten(f"{seite}UpperArm")][:3, 3]
        b = welt[g.knoten(f"{seite}LowerArm")][:3, 3]
        v = (b - a) / np.linalg.norm(b - a)
        erg[seite] = math.degrees(math.acos(float(np.clip(-v[1], -1, 1))))
    return erg


def main():
    opt = {a.split("=")[0][2:]: a.split("=", 1)[1] for a in sys.argv[1:] if a.startswith("--") and "=" in a}
    ordner = opt.get("ordner", sp.FIGUREN)
    clips = opt["clips"].split(",") if "clips" in opt else CLIPS
    grenze = float(opt.get("grenze", GRENZE))
    fehler = 0
    print(f"Armprüfung (Oberarm gegen die Senkrechte, Grenzwert Mittel {grenze:.0f}°), Ordner {ordner}")
    print(f"{'Datei':28} {'Clip':8} {'links Mittel/max':>16} {'rechts Mittel/max':>17}  Urteil")
    for datei in sorted(f for f in os.listdir(ordner) if f.startswith("passant_probe") and f.endswith(".glb")):
        g = sp.Glb(os.path.join(ordner, datei))
        namen = [a["name"] for a in g.j.get("animations", [])]
        for clip in clips:
            if clip not in namen and clip != "Ruhelage":
                continue
            dauer = 1.0 if clip == "Ruhelage" else g.clipdauer(clip)
            werte = [winkel(g, clip, float(z)) for z in np.linspace(0, dauer, 13, endpoint=False)]
            l = [w["Left"] for w in werte]
            r = [w["Right"] for w in werte]
            gut = np.mean(l) <= grenze and np.mean(r) <= grenze
            fehler += not gut
            print(f"{datei:28} {clip:8} {np.mean(l):7.0f}/{max(l):4.0f}° {np.mean(r):8.0f}/{max(r):4.0f}°  {'gut' if gut else 'FEHLER'}", flush=True)
    print(f"Fehler: {fehler}")
    return 1 if fehler else 0


if __name__ == "__main__":
    sys.exit(main())
