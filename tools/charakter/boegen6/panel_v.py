#!/usr/bin/env python3
"""Verschiedenheit der Köpfe (Plan 01.11.2026), Etappe E0/E1: festes, wiederholbares Blind-Panel.

Je Geschlecht 16 Zufallspersonen (35 Jahre, nur die Gestalt-Gene aus der Saat; Haut, Haar- und Augenfarbe, Frisur und
Licht fest, damit nur die Form zählt):
  Blatt A  vorn (K1), A1–A16 in fester Reihenfolge
  Blatt B  schräg (K2, 35°), dieselben 16 Personen gemischt, B1–B16
  Blatt C1/C2  15 Paare (10 Fremde, 5 echte Geschwister, gemischt), Urteil „Geschwister?“ 1–10
  Blatt X  8 Extremköpfe (stärkste Augen- bzw. Nasengene aus einer Stichprobe von 300), vorn und schräg (Fratzen)
Die Prüfer sehen nur die Blätter. Der Schlüssel (Saaten, Zuordnung, Paare) liegt außerhalb des Repositorys; sein SHA-256
steht im Bericht, bevor jemand urteilt. Saaten und Paare sind in jeder Runde gleich, die Mischung hängt an --runde.

    # 1. Stichprobe (Gene über den Kern) für die Extremköpfe, Godot ohne Grafik:
    python3 tools/charakter/boegen6/panel_v.py stichprobe <personen.json>
    $G --headless --path game res://versuch/charakter/charakter_schau.tscn -- --glb=<glb-ordner> \
       --gewichte=<personen.json> --aus=<stichprobe.json> --stand=6 --ende
    # 2. Bögen und Schlüssel:
    python3 tools/charakter/boegen6/panel_v.py boegen --kopf=<…/kopf6.glb> --runde=e0 --stichprobe=<stichprobe.json> \
       --aus=<ordner der bögen> --schluessel=<ordner außerhalb des repositorys>
"""
import hashlib
import json
import math
import os
import random
import sys

SAATEN = {"m": list(range(9001, 9017)), "w": list(range(9101, 9117))}
FAMILIEN_BASIS = {"m": 9201, "w": 9401}        # 5 Familien: Eltern basis+2i, basis+2i+1; Kinder basis+20+2i, +1
FREMDE_BASIS = {"m": 9301, "w": 9501}          # 10 Fremdenpaare: basis+2i, basis+2i+1
STICHPROBE = {"m": range(9601, 9901), "w": range(9601, 9901)}
AUGEN_GENE = ["augen_groesse", "lidspalte", "schlupflid", "augen_neigung", "augen_tiefe", "brauen_stand", "brauenwulst"]
NASEN_GENE = ["nase_laenge", "nasenspitze", "nase_hoecker"]
HAAR = {"m": {"modus": "tressfx", "ruhe": True, "vorgabe": {"frisur": "mann_kurz", "bart": ""}},
        "w": {"modus": "tressfx", "ruhe": True, "vorgabe": {"frisur": "frau_knoten", "bart": ""}}}
FARBEN = {"haut_helligkeit": 185, "haut_ton": 105, "haar_helligkeit": 60, "haar_rot": 40}
K1 = {"ansicht": "kopf", "winkel": 0, "hoehe": 0, "spanne_m": 0.30, "ziel_m": -0.015}
K2 = {"ansicht": "kopf", "winkel": 35, "hoehe": 0, "spanne_m": 0.30, "ziel_m": -0.015}


def person(g, kopf):
    gene = dict(FARBEN, **({"bartwuchs": 130} if g == "m" else {}))
    return {"kopf": kopf, "stand": 6, "weg_a": True, "weg_c": True, "weg_d": True, "wetter": 0.25, "licht": "spiel",
            "alter": 35, "geschlecht": g, "haar": HAAR[g], "gene": gene, "warten": 45, "saat_arten": ["Merkmal"]}


def bogen(name, titel, alle, zellen, spalten=4):
    zeilen = [{"titel": "", "zellen": zellen[i:i + spalten]} for i in range(0, len(zellen), spalten)]
    return {"name": name, "titel": titel, "zelle": [600, 720], "schrift": 2.2, "rand": 40, "alle": alle,
            "spalten": [{"titel": ""}] * spalten, "zeilen": zeilen}


def stichprobe(ziel):
    personen = []
    for g, saaten in STICHPROBE.items():
        for s in saaten:
            personen.append({"name": f"{g}{s}", "saat": s, "geschlecht": g, "alter": 35, "stand": 6,
                             "saat_arten": ["Merkmal"], "gene": dict(FARBEN)})
    with open(ziel, "w", encoding="utf-8") as f:
        json.dump({"kopf": "standard", "personen": personen}, f, ensure_ascii=False)
    print("Stichprobe", len(personen), "->", ziel)


def extreme(pfad):
    """Je Geschlecht 5 Köpfe mit dem größten Augenmaß √(Mittel z²) über die Augengene und 3 mit der größten Nasennorm."""
    d = json.load(open(pfad, encoding="utf-8"))
    aus = {}
    for g in ("m", "w"):
        ps = [p for p in d["personen"] if p["name"].startswith(g)]
        def mass(p, gene):
            z = p.get("gene_z", {})
            return math.sqrt(sum(float(z.get(k, 0.0)) ** 2 for k in gene) / len(gene))
        augen = sorted(ps, key=lambda p: -mass(p, AUGEN_GENE))[:5]
        rest = [p for p in ps if p not in augen]
        nase = sorted(rest, key=lambda p: -mass(p, NASEN_GENE))[:3]
        aus[g] = [{"saat": p["saat"], "grund": "Augen", "mass": round(mass(p, AUGEN_GENE), 2)} for p in augen] + \
                 [{"saat": p["saat"], "grund": "Nase", "mass": round(mass(p, NASEN_GENE), 2)} for p in nase]
    return aus


def boegen(opt):
    kopf, runde, ordner = opt["kopf"], opt["runde"], opt["aus"]
    os.makedirs(ordner, exist_ok=True)
    ext = extreme(opt["stichprobe"])
    schluessel = {"runde": runde, "kopf": kopf, "geschlechter": {}}
    zz = random.Random("panel_v:" + runde)
    for g in ("m", "w"):
        alle = person(g, kopf)
        wer = "Männer" if g == "m" else "Frauen"
        saaten = SAATEN[g]
        # A vorn, feste Reihenfolge
        zellen = [dict(K1, titel=f"A{i + 1}", saat=s) for i, s in enumerate(saaten)]
        json.dump(bogen(f"v_{g}_a", f"{wer} – Blatt A (vorn)", alle, zellen), open(os.path.join(ordner, f"v_{g}_a.json"), "w"), ensure_ascii=False, indent=1)
        # B schräg, gemischt
        ordnung = list(range(16))
        zz.shuffle(ordnung)
        zellen = [dict(K2, titel=f"B{i + 1}", saat=saaten[j]) for i, j in enumerate(ordnung)]
        b_zu_a = {f"B{i + 1}": f"A{j + 1}" for i, j in enumerate(ordnung)}
        json.dump(bogen(f"v_{g}_b", f"{wer} – Blatt B (schräg)", alle, zellen), open(os.path.join(ordner, f"v_{g}_b.json"), "w"), ensure_ascii=False, indent=1)
        # C Paare: 5 Geschwister (gleiche Eltern), 10 Fremde
        fb, fr = FAMILIEN_BASIS[g], FREMDE_BASIS[g]
        familie = {}
        paare = []
        for i in range(5):
            v, m, k1, k2 = fb + 2 * i, fb + 2 * i + 1, fb + 20 + 2 * i, fb + 20 + 2 * i + 1
            familie[str(k1)] = [v, m]
            familie[str(k2)] = [v, m]
            paare.append({"art": "Geschwister", "saaten": [k1, k2]})
        for i in range(10):
            paare.append({"art": "Fremde", "saaten": [fr + 2 * i, fr + 2 * i + 1]})
        zz.shuffle(paare)
        c_alle = dict(alle, familie=familie)
        for teil, (von, bis) in (("c1", (0, 8)), ("c2", (8, 15))):
            zellen = []
            for n in range(von, bis):
                for seite, s in zip("ab", paare[n]["saaten"]):
                    zellen.append(dict(K1, titel=f"Paar {n + 1} {seite}", saat=s))
            json.dump(bogen(f"v_{g}_{teil}", f"{wer} – Blatt {teil.upper()} (Paare)", c_alle, zellen),
                      open(os.path.join(ordner, f"v_{g}_{teil}.json"), "w"), ensure_ascii=False, indent=1)
        # X Extreme, gemischt; Zeilen vorn / schräg abwechselnd
        x = list(ext[g])
        zz.shuffle(x)
        zellen = []
        for blk in (x[:4], x[4:]):
            zellen += [dict(K1, titel=f"X{x.index(e) + 1} vorn", saat=e["saat"]) for e in blk]
            zellen += [dict(K2, titel=f"X{x.index(e) + 1} schräg", saat=e["saat"]) for e in blk]
        json.dump(bogen(f"v_{g}_x", f"{wer} – Blatt X", alle, zellen), open(os.path.join(ordner, f"v_{g}_x.json"), "w"), ensure_ascii=False, indent=1)
        schluessel["geschlechter"][g] = {"saaten_A": saaten, "B_zu_A": b_zu_a,
                                         "paare": {f"Paar {n + 1}": paare[n] for n in range(15)},
                                         "X": {f"X{i + 1}": e for i, e in enumerate(x)}}
    roh = json.dumps(schluessel, sort_keys=True, ensure_ascii=False, indent=1).encode("utf-8")
    os.makedirs(opt["schluessel"], exist_ok=True)
    pfad = os.path.join(opt["schluessel"], f"schluessel_v_{runde}.json")
    with open(pfad, "wb") as f:
        f.write(roh)
    print("Schlüssel", pfad, "SHA-256", hashlib.sha256(roh).hexdigest())


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)
    if sys.argv[1] == "stichprobe":
        stichprobe(sys.argv[2])
    else:
        boegen(dict(a[2:].split("=", 1) for a in sys.argv[2:] if a.startswith("--")))
