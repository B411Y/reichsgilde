#!/usr/bin/env python3
"""
Prüft den Gebäudekatalog game/daten/katalog.json gegen die Modelle in game/assets/modelle/ (Bereich Leveldesign). Braucht kein Blender.

Aufruf (aus dem Projektordner):
    python3 tools/karten/katalog_pruefen.py            # Fehler und Warnungen
    python3 tools/karten/katalog_pruefen.py --alle     # dazu je Baustein Grundfläche und Ausdehnung des Modells

Fehler (das Werkzeug endet mit Code 1):
  • eine Modelldatei aus `modell` fehlt unter game/assets/modelle/,
  • ein `knoten` fehlt in der Datei, bei Natur auch eine Variante (`Tanne1` … `Tanne3` bei `varianten: 3`).
Warnungen (nur Hinweis): Die Grundfläche passt nicht zur Größe des Modells –
  • das Modell ragt mehr als 0,5 m über die Fläche (gemessen von der Mitte der Fläche, also mit `versatz`),
  • oder es füllt sie in beiden Richtungen nicht einmal zur Hälfte.
Die Ausdehnung zählt alle Netze der Datei (bzw. des Knotens samt Kindern) mit ihrer Lage, auch Zustände wie Leerstand oder Gerüst.

Bekannt und gewollt (Entscheidung des Kerns, PR #163): Die Umlandbetriebe (Grube, Holzschlag … Klosterhof) sind als Modell größer als ihre
Fläche im Katalog (16 × 14 m usw.) – die Fläche bleibt, sie ist die belegte Fläche für das Bauen. Ihre Warnungen sind deshalb erwartet.
"""
import json
import os
import sys

sys.dont_write_bytecode = True   # keinen __pycache__-Ordner im Repo anlegen
HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "modelle"))
from karte import WURZEL, katalog_laden  # noqa: E402
from pruefen import lesen, _welt  # noqa: E402  (GLB lesen wie tools/modelle/pruefen.py)

MODELLORDNER = os.path.join(WURZEL, "game", "assets", "modelle")
UEBERSTAND = 0.5          # Meter, die ein Modell über seine Fläche ragen darf, ohne dass gewarnt wird
MINDESTENS = 0.5          # Anteil der Fläche, den ein Modell in wenigstens einer Richtung füllen soll

_glb = {}


def glb(modell):
    """Die glTF-Beschreibung einer Modelldatei (ohne .glb) und die Weltmatrix je Knoten – oder None, wenn die Datei fehlt."""
    if modell not in _glb:
        pfad = os.path.join(MODELLORDNER, modell + ".glb")
        if not os.path.exists(pfad):
            _glb[modell] = None
        else:
            g = lesen(pfad)
            _glb[modell] = (g, _welt(g))
    return _glb[modell]


def knoten_index(g, name):
    return next((i for i, n in enumerate(g["nodes"]) if n.get("name") == name), None)


def anker(modell, name="Anker_Tuer"):
    """Lage (x, z) des ersten Knotens `name` im Modell, wie die Stadtansicht ihn liest (Eltern eingerechnet) – oder None."""
    daten = glb(modell)
    if daten is None:
        return None
    g, welt = daten
    i = knoten_index(g, name)
    return None if i is None else (welt[i][0][3], welt[i][2][3])


def ausdehnung(modell, knoten=None):
    """(x0, z0, x1, z1) aller Netze der Datei oder des Knotens samt Kindern, in den Koordinaten des Modells – oder None."""
    daten = glb(modell)
    if daten is None:
        return None
    g, welt = daten
    if knoten is None:
        auswahl = range(len(g["nodes"]))
    else:
        start = knoten_index(g, knoten)
        if start is None:
            return None
        auswahl, offen = [], [start]
        while offen:
            i = offen.pop()
            auswahl.append(i)
            offen += g["nodes"][i].get("children", [])
    xs, zs = [], []
    for i in auswahl:
        n = g["nodes"][i]
        if "mesh" not in n:
            continue
        m = welt[i]
        for prim in g["meshes"][n["mesh"]]["primitives"]:
            acc = g["accessors"][prim["attributes"]["POSITION"]]
            for x in (acc["min"][0], acc["max"][0]):
                for y in (acc["min"][1], acc["max"][1]):
                    for z in (acc["min"][2], acc["max"][2]):
                        xs.append(m[0][0] * x + m[0][1] * y + m[0][2] * z + m[0][3])
                        zs.append(m[2][0] * x + m[2][1] * y + m[2][2] * z + m[2][3])
    return (min(xs), min(zs), max(xs), max(zs)) if xs else None


def pruefen(alle=False):
    fehler, warnungen = [], []
    katalog = katalog_laden()
    for kennung, b in katalog.items():
        modell = b.get("modell")
        if modell is None:
            continue
        if glb(modell) is None:
            fehler.append(f"{kennung}: Modelldatei game/assets/modelle/{modell}.glb fehlt")
            continue
        g, _ = glb(modell)
        knoten = b.get("knoten")
        namen = []
        if knoten is not None:
            namen = [knoten + str(v) for v in range(1, b.get("varianten", 1) + 1)] if b["art"] == "natur" else [knoten]
            for n in namen:
                if knoten_index(g, n) is None:
                    fehler.append(f"{kennung}: Knoten „{n}“ fehlt in {modell}.glb")
        if "groesse" not in b:
            continue
        aus = [ausdehnung(modell, n) for n in namen] if namen else [ausdehnung(modell)]
        aus = [a for a in aus if a]
        if not aus:
            continue
        x0, z0 = min(a[0] for a in aus), min(a[1] for a in aus)
        x1, z1 = max(a[2] for a in aus), max(a[3] for a in aus)
        w, t = b["groesse"]
        vx, vz = b.get("versatz", (0, 0))
        mx, mz = -vx, -vz              # Mitte der Fläche in Koordinaten des Modells (der Ursprung liegt `versatz` neben ihr)
        ueber = max(x1 - (mx + w / 2), (mx - w / 2) - x0, z1 - (mz + t / 2), (mz - t / 2) - z0)
        text = (f"{kennung}: Fläche {w:g} × {t:g} m, Modell {modell}{' (' + knoten + ')' if knoten else ''} "
                f"x {x0:.1f} … {x1:.1f}, z {z0:.1f} … {z1:.1f} ({x1 - x0:.1f} × {z1 - z0:.1f} m)")
        if ueber > UEBERSTAND:
            warnungen.append(f"{text} – ragt bis {ueber:.1f} m über die Fläche" + (" (Umland: bekannt, Fläche bleibt)" if b["art"] == "umlandbetrieb" else ""))
        elif (x1 - x0) < MINDESTENS * w and (z1 - z0) < MINDESTENS * t:
            warnungen.append(f"{text} – füllt die Fläche nicht einmal zur Hälfte")
        elif alle:
            print("  ", text)
    return fehler, warnungen


if __name__ == "__main__":
    fehler, warnungen = pruefen("--alle" in sys.argv)
    for w in warnungen:
        print("WARNUNG:", w)
    for f in fehler:
        print("FEHLER:", f)
    print(f"Katalog: {len(fehler)} Fehler, {len(warnungen)} Warnungen")
    sys.exit(1 if fehler else 0)
