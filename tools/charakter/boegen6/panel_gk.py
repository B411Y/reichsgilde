#!/usr/bin/env python3
"""Grundkopf-Panel (Grundkopf 5): Bögen für „vorher“ und „neu“ mit denselben Personen, dazu der Schlüssel für die
blinde Zuordnung (A/B, X/Y, Lidformen). Die Prüfer sehen nur die umbenannten Bilder, der Schlüssel liegt außerhalb.

    python3 tools/charakter/boegen6/panel_gk.py stichprobe <personen.json>
    # Godot headless: --gewichte=<personen.json> --aus=<stichprobe.json> --stand=6 --ende
    python3 tools/charakter/boegen6/panel_gk.py boegen --vorher=<kopf6.glb> --neu=<kopf6.glb> --stichprobe=<stichprobe.json> --aus=<ordner>
    # danach je Seite: schau.sh <bilder> <BxH> --bogen=<aus>/<seite>_*.json --ende
    # Vollständigkeit prüfen (Grundkopf 5b: die Bögen der Runde 1 von Grundkopf 5 waren abgeschnitten): Fenster 2000x3100
    python3 tools/charakter/boegen6/panel_gk.py vollstaendig --boegen=<aus-Ordner mit den .json> --bilder=<Ordner mit den .png> [--praefix=neu|vorher]
    python3 tools/charakter/boegen6/panel_gk.py blind --bilder=<ordner mit vorher/ und neu/> --runde=r1 --aus=<blind> --schluessel=<ordner>
"""
import hashlib
import json
import math
import os
import random
import shutil
import sys

sys.path.insert(0, os.path.dirname(__file__))
import panel_v as pv

SAATEN = {"m": range(30001, 30401), "w": range(40001, 40401)}
FARBEN = pv.FARBEN
K1 = pv.K1
K2 = pv.K2
AUGE = {"ansicht": "augen", "winkel": 0, "hoehe": 0, "spanne_m": 0.17, "ziel_m": 0.0}
LID_PERSONEN = {"m": [30011, 30217, 30050], "w": [40010, 40100, 40200]}
GRUND_AUGEN_MANN = [30011, 30217]


def person(g, kopf, haar=True):
    p = pv.person(g, kopf)
    if not haar:
        p["haar"] = {"modus": "aus"}
        p["warten"] = 10
    return p


def stichprobe(ziel):
    personen = []
    for g, saaten in SAATEN.items():
        for s in saaten:
            personen.append({"name": f"{g}{s}", "saat": s, "geschlecht": g, "alter": 35, "stand": 6,
                             "saat_arten": ["Merkmal"], "gene": dict(FARBEN)})
    json.dump({"kopf": "standard", "personen": personen}, open(ziel, "w", encoding="utf-8"), ensure_ascii=False)


def fratzen_saaten(pfad):
    """Je Geschlecht die 4 extremsten (Augen- und Nasengene, größter z-Ausschlag) und 16 feste Zufallsköpfe."""
    d = json.load(open(pfad, encoding="utf-8"))
    aus = {}
    rnd = random.Random("gk-fratzen")
    for g in ("m", "w"):
        ps = [p for p in d["personen"] if p["name"].startswith(g)]
        def mass(p):
            z = p.get("gene_z", {})
            return max(abs(float(z.get(k, 0.0))) for k in pv.AUGEN_GENE + pv.NASEN_GENE)
        ext = [p["saat"] for p in sorted(ps, key=lambda p: -mass(p))[:4]]
        rest = [p["saat"] for p in ps if p["saat"] not in ext]
        aus[g] = ext + rnd.sample(rest, 16)
    return aus


def schreiben(pfad, name, titel, alle, zellen, spalten, zelle=(600, 720)):
    zeilen = [{"titel": "", "zellen": zellen[i:i + spalten]} for i in range(0, len(zellen), spalten)]
    b = {"name": name, "titel": titel, "zelle": list(zelle), "schrift": 2.2, "rand": 40, "alle": alle,
         "spalten": [{"titel": ""}] * spalten, "zeilen": zeilen}
    json.dump(b, open(pfad, "w", encoding="utf-8"), ensure_ascii=False, indent=1)


def boegen(opt):
    ordner = opt["aus"]
    os.makedirs(ordner, exist_ok=True)
    fr = fratzen_saaten(opt["stichprobe"])
    reihenfolge = random.Random("gk-lid").sample([(g, s, f) for g in ("m", "w") for s in LID_PERSONEN[g]
                                                  for f in ("rund", "mandel", "schlupf")], 18)
    for seite in ("vorher", "neu"):
        kopf = os.path.expanduser(opt[seite])
        # Köpfe: Grundköpfe (keine Saat) vorn und schräg, Mann und Frau
        for g in ("m", "w"):
            zellen = [dict(K1, titel=f"{g} vorn"), dict(K2, titel=f"{g} schräg")]
            schreiben(os.path.join(ordner, f"{seite}_kopf_{g}.json"), f"{seite}_kopf_{g}", "", person(g, kopf), zellen, 2)
        # Augen nah: Grundköpfe und die beiden Mandel-Männer
        for g, saaten in (("m", [None] + GRUND_AUGEN_MANN), ("w", [None])):
            zellen = []
            for s in saaten:
                z = dict(AUGE, titel=f"{g} {s or 'grund'}")
                if s:
                    z["saat"] = s
                zellen.append(z)
            schreiben(os.path.join(ordner, f"{seite}_augen_{g}.json"), f"{seite}_augen_{g}", "", person(g, kopf, False), zellen, len(zellen))
        # Fratzen: 40 Köpfe, vorn und schräg, in vier Blättern zu je 10 Köpfen
        for g in ("m", "w"):
            for blatt in range(2):
                zellen = []
                for s in fr[g][blatt * 10:(blatt + 1) * 10]:
                    zellen += [dict(K1, titel=f"{s} vorn", saat=s), dict(K2, titel=f"{s} schräg", saat=s)]
                schreiben(os.path.join(ordner, f"{seite}_fratzen_{g}{blatt + 1}.json"), f"{seite}_fratzen_{g}{blatt + 1}", "",
                          person(g, kopf, False), zellen, 4, (420, 520))
    # Lidformen (nur neu): 18 Augenpaare gemischt
    zellen = []
    for i, (g, s, f) in enumerate(reihenfolge):
        zellen.append(dict(AUGE, titel=f"L{i + 1}", saat=s, geschlecht=g, lidform=f))
    alle = person("m", os.path.expanduser(opt["neu"]), False)
    for g in ("m", "w"):
        zz = [z for z, (gg, _, _) in zip(zellen, reihenfolge) if gg == g]
        schreiben(os.path.join(ordner, f"neu_lider_{g}.json"), f"neu_lider_{g}", "", person(g, os.path.expanduser(opt["neu"]), False), zz, 3)
    json.dump({"fratzen": fr, "lider": [{"titel": f"L{i + 1}", "geschlecht": g, "saat": s, "form": f}
                                       for i, (g, s, f) in enumerate(reihenfolge)]},
              open(os.path.join(ordner, "personen.json"), "w"), ensure_ascii=False, indent=1)


def vollstaendig(opt):
    """Prüft jeden Bogen: Bild vorhanden, Größe wie berechnet (Zellen × Zellgröße + Rand und Beschriftung), alle Zellen belegt.
    Die Größe berechnet charakter_schau.gd so: Breite = rand + Spalten × (Zelle.x + 6), Höhe = Kopfzeile + Spaltenkopf +
    Zeilen × (Zelle.y + Unterzeile), jeweils mit der Schriftvergrößerung „schrift“. Ist das Bild kleiner (Fenster zu klein),
    wurde es abgeschnitten."""
    from struct import unpack
    ok = True
    summe = {}
    for datei in sorted(os.listdir(opt["boegen"])):
        if not datei.endswith(".json") or datei == "personen.json" or not datei.startswith(opt.get("praefix", "")):
            continue
        b = json.load(open(os.path.join(opt["boegen"], datei), encoding="utf-8"))
        sf = float(b.get("schrift", 1.0))
        ges_x = int(b.get("rand", 150)) + len(b["spalten"]) * (b["zelle"][0] + 6)
        ges_y = int(34 * sf) + int(30 * sf) + len(b["zeilen"]) * (b["zelle"][1] + int(26 * sf))
        zellen = sum(len(z.get("zellen", [])) for z in b["zeilen"])
        png = os.path.join(opt["bilder"], b["name"] + ".png")
        if not os.path.exists(png):
            print("FEHLT", b["name"]); ok = False; continue
        with open(png, "rb") as f:
            kopf = f.read(24)
        breite, hoehe = unpack(">II", kopf[16:24])
        gut = (breite, hoehe) == (ges_x, ges_y)
        ok &= gut
        gruppe = b["name"].split("_")[0] + "_" + b["name"].split("_")[1]
        summe[gruppe] = summe.get(gruppe, 0) + zellen
        print("%-24s %2d Zellen  Bild %dx%d  erwartet %dx%d  %s" % (b["name"], zellen, breite, hoehe, ges_x, ges_y, "ok" if gut else "ABGESCHNITTEN"))
    for g, n in sorted(summe.items()):
        print("Summe", g, n, "Zellen")
    print("VOLLSTAENDIG" if ok else "UNVOLLSTAENDIG")
    return ok


if __name__ == "__main__":
    opt = dict(a[2:].split("=", 1) for a in sys.argv[2:] if a.startswith("--"))
    {"stichprobe": lambda: stichprobe(sys.argv[2]), "boegen": lambda: boegen(opt), "vollstaendig": lambda: sys.exit(0 if vollstaendig(opt) else 1)}[sys.argv[1]]()
