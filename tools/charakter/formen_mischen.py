"""Formen eines Kopfs als Linearkombination vorhandener Formen neu mischen (Charakter Phase 6, Etappe B; eigener Code, MIT).

Der Probebau (kopf6.sh proben) legt jedes Kandidatenziel als eigene Form ab („probe_*“, „eigen_*“, „ictid_*“,
„herkunft_*“). Formen sind lineare Versätze; eine Mischform Σ wᵢ·Formᵢ ist also genau das, was Blender aus denselben
Zielen gebaut hätte (Lage; die Normalen werden ebenso linear gemischt, wie Godot sie bei gleichzeitig gesetzten Formen
mischt). So entstehen die Formen des fertigen Kopfs ohne zweiten Blender-Bau, und Ausgleichs- und Eichrechnung können
beliebig oft neu mischen.

Rezept (JSON):
  {"formen": {"<Name>": {"<vorhandene Form>": Gewicht, …}, …},     neue oder ersetzte Formen (in dieser Reihenfolge)
   "entfernen": ["probe_", "ictid_", …]}                         Vorsilben der Formen, die nicht in den Kopf kommen
Eine Form im Rezept darf auf eine früher im selben Rezept gemischte zeigen (z. B. zug_01 auf eine bereinigte Achse).

Aufruf ($BPY = Blenders Python, numpy):
  $BPY -I tools/charakter/formen_mischen.py <ein.glb> <aus.glb> <rezept.json> [beschreibung=<ein.json>] [aus_json=<aus.json>]
Die Beschreibung (kopf6.json) bekommt die Formliste je Teil, die Merkpunktversätze der Mischformen (linear gemischt)
und im Bericht Rezept und Prüfsumme.
"""
import hashlib
import json
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import glb_formnormalen as gf  # noqa: E402
from messen.glb import Glb  # noqa: E402


def _anhaengen(gltf, binaer, werte, mit_grenzen):
    """float32-VEC3-Feld als neuen Accessor hinten an den Puffer hängen; gibt den Index zurück."""
    roh = np.ascontiguousarray(werte, dtype="<f4").tobytes()
    binaer += b"\x00" * ((4 - len(binaer) % 4) % 4)
    gltf["bufferViews"].append({"buffer": 0, "byteOffset": len(binaer), "byteLength": len(roh)})
    binaer += roh
    a = {"bufferView": len(gltf["bufferViews"]) - 1, "componentType": 5126, "count": int(len(werte)), "type": "VEC3"}
    if mit_grenzen:
        a["min"] = [float(v) for v in werte.min(0)]
        a["max"] = [float(v) for v in werte.max(0)]
    gltf["accessors"].append(a)
    return len(gltf["accessors"]) - 1


def mischen(ein, aus, rezept, beschreibung=None, aus_json=None):
    gltf, binaer = gf.lesen(ein)
    g = Glb(ein)
    formen = rezept.get("formen", {})
    weg = tuple(rezept.get("entfernen", []))
    neu_namen_alle = None
    for mi, netz in enumerate(gltf["meshes"]):
        namen = list(netz.get("extras", {}).get("targetNames", []))
        if not namen:
            continue
        for prim_i, prim in enumerate(netz["primitives"]):
            ziele = prim.get("targets", [])
            # Felder je Form dieses Primitivs (Lage, Normale), auch für im Rezept gemischte
            felder = {}

            def feld(name, art):
                if (name, art) in felder:
                    return felder[(name, art)]
                if name not in namen:
                    # Blender lässt Formen ohne Versatz in einem Teil weg: dort zählt die Form als null
                    if netz.get("name") == "Haut":
                        raise KeyError(f"Form {name} fehlt im Netz Haut")
                    felder[(name, art)] = None
                    return None
                z = ziele[namen.index(name)]
                f = g.accessor(z[art]).astype(np.float64) if art in z else None
                felder[(name, art)] = f
                return f
            neue = {}
            for name, teile in formen.items():
                for art in ("POSITION", "NORMAL"):
                    summe = None
                    for quelle, w in teile.items():
                        f = neue.get((quelle, art)) if (quelle, art) in neue else feld(quelle, art)
                        if f is None:
                            continue
                        summe = float(w) * f if summe is None else summe + float(w) * f
                    if summe is None:
                        n_ecken = g.accessor(prim["attributes"]["POSITION"]).shape[0]
                        summe = np.zeros((n_ecken, 3)) if art == "POSITION" else None
                    if summe is not None:
                        summe = np.where(np.linalg.norm(summe, axis=1, keepdims=True) < 1.5e-5 if art == "POSITION"
                                         else np.zeros((len(summe), 1), bool), 0.0, summe)
                    neue[(name, art)] = summe
            # neue Zielliste: alte (ohne Vorsilben in „entfernen“, ersetzte an ihrer Stelle), dann neue
            liste, neu_namen = [], []
            for i, n in enumerate(namen):
                if n.startswith(weg) and n not in formen:
                    continue
                if n in formen:
                    z = {"POSITION": _anhaengen(gltf, binaer, neue[(n, "POSITION")], True)}
                    if neue[(n, "NORMAL")] is not None:
                        z["NORMAL"] = _anhaengen(gltf, binaer, neue[(n, "NORMAL")], False)
                    liste.append(z)
                else:
                    liste.append(ziele[i])
                neu_namen.append(n)
            for n in formen:
                if n in namen:
                    continue
                z = {"POSITION": _anhaengen(gltf, binaer, neue[(n, "POSITION")], True)}
                if neue[(n, "NORMAL")] is not None:
                    z["NORMAL"] = _anhaengen(gltf, binaer, neue[(n, "NORMAL")], False)
                liste.append(z)
                neu_namen.append(n)
            prim["targets"] = liste
            if prim_i == 0:
                netz_namen = neu_namen
        netz.setdefault("extras", {})["targetNames"] = netz_namen
        # Standardgewichte der Formen (Blender schreibt sie): Länge anpassen
        if "weights" in netz:
            netz["weights"] = [0.0] * len(netz_namen)
        neu_namen_alle = netz_namen
    gltf, puffer = gf.packen(gltf, binaer)
    gf.schreiben(aus, gltf, puffer)
    print(f"Gemischt: {len(formen)} Formen, entfernt: Vorsilben {', '.join(weg) or '–'}; "
          f"{os.path.getsize(ein) / 1e6:.1f} MB -> {os.path.getsize(aus) / 1e6:.1f} MB")
    if beschreibung and aus_json:
        d = json.load(open(beschreibung, encoding="utf-8"))
        mf = d.get("merkpunkt_formen", {})
        for name, teile in formen.items():
            summe = {}
            for quelle, w in teile.items():
                for k, v in mf.get(quelle, {}).items():
                    summe[k] = [summe.get(k, [0, 0, 0])[i] + float(w) * v[i] for i in range(3)]
            if summe:
                mf[name] = {k: [round(x, 5) for x in v] for k, v in summe.items()}
        d["merkpunkt_formen"] = {k: v for k, v in mf.items() if not (k.startswith(weg) and k not in formen)}
        g2 = Glb(aus)
        d["formen_je_objekt"] = {}
        for netz in g2.js["meshes"]:
            n = netz.get("name", "")
            namen = netz.get("extras", {}).get("targetNames", [])
            if n in ("Haut", "Augen", "Zaehne", "Haare", "Kragen") and namen:
                d["formen_je_objekt"][n] = namen
        roh = json.dumps(rezept, sort_keys=True, ensure_ascii=False).encode("utf-8")
        d.setdefault("bericht", {})["rezept"] = {"formen": len(formen), "entfernt": list(weg),
                                                 "sha256": hashlib.sha256(roh).hexdigest()}
        json.dump(d, open(aus_json, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    return neu_namen_alle


def main():
    ein, aus, rezept_pfad = sys.argv[1], sys.argv[2], sys.argv[3]
    opt = dict(a.split("=", 1) for a in sys.argv[4:])
    rezept = json.load(open(rezept_pfad, encoding="utf-8"))
    mischen(ein, aus, rezept, opt.get("beschreibung"), opt.get("aus_json"))


if __name__ == "__main__":
    main()
