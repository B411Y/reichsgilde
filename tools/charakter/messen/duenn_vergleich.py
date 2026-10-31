#!/usr/bin/env python3
"""Charakter Phase 6 (S2): zwei .glb mit denselben Formen vergleichen – dicht gegen dünn gespeichert.

Vergleicht je Netz, Primitiv und Form die Versätze von Lage und Normale (dünne Accessoren aufgefüllt) und meldet die
größte Abweichung. Formen, deren Normalen in einer der Dateien fehlen, werden für die Normale übersprungen.

    $BPY -I tools/charakter/messen/duenn_vergleich.py <dicht.glb> <duenn.glb> [--aus=<json>]
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from messen.glb import Glb  # noqa: E402


def main():
    a, b = Glb(sys.argv[1]), Glb(sys.argv[2])
    opt = dict(x[2:].split("=", 1) for x in sys.argv[3:] if x.startswith("--") and "=" in x)
    erg = {"dicht": a.pfad, "duenn": b.pfad, "groesse_mb": [round(os.path.getsize(a.pfad) / 1e6, 2), round(os.path.getsize(b.pfad) / 1e6, 2)],
           "lage_max": 0.0, "normale_max": 0.0, "formen": 0, "normalen_formen": 0, "duenne_accessoren": 0}
    for ma, mb in zip(a.js["meshes"], b.js["meshes"]):
        assert ma["name"] == mb["name"], (ma["name"], mb["name"])
        for pa, pb in zip(ma["primitives"], mb["primitives"]):
            for za, zb in zip(pa.get("targets", []), pb.get("targets", [])):
                erg["formen"] += 1
                for art in ("POSITION", "NORMAL"):
                    if art not in za or art not in zb:
                        continue
                    if b.ist_duenn(zb[art]):
                        erg["duenne_accessoren"] += 1
                    d = float(np.abs(a.accessor(za[art]).astype(np.float64) - b.accessor(zb[art]).astype(np.float64)).max())
                    if art == "POSITION":
                        erg["lage_max"] = max(erg["lage_max"], d)
                    else:
                        erg["normalen_formen"] += 1
                        erg["normale_max"] = max(erg["normale_max"], d)
    print(json.dumps(erg, ensure_ascii=False))
    if "aus" in opt:
        json.dump(erg, open(opt["aus"], "w", encoding="utf-8"), indent=1, ensure_ascii=False)


if __name__ == "__main__":
    main()
