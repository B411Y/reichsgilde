#!/usr/bin/env python3
"""Charakter Phase 6: Vergleichsblatt Prüfmappe A | Prüfmappe B | Fotovorlage (je Zeile eine Person).

Die Bilder kommen aus dem Bogen P1 (K1, L-V) beider Mappen; die Fotovorlage wird wie in P3 auf den Pupillenabstand des
Kopfs skaliert und an dessen Pupillen gelegt (Pupillen des Kopfs aus der Begleitdatei, der Vorlage aus
vorlagen_merkpunkte.json). Läuft mit Blenders Python (numpy, OpenImageIO), ohne Godot und Blender. Schreibt die
ausgerichteten Vorlagen und einen Bogen <aus>.json (Zellen "bild"), den die Testszene mit Beschriftung zusammensetzt
(OpenImageIO kann hier ohne FreeType keinen Text):

    $BPY -I tools/charakter/messen/vergleichsblatt.py --a=<Mappe A0> --b=<Mappe A1> --aus=<ordner>/vergleich_a0_a1
    tools/charakter/schau.sh <ordner> 2200x3100 --bogen=<ordner>/vergleich_a0_a1.json --ende
         [--zeilen=00_01:h_m35_hell,03_01:h_w35_hell,01_03:a_m70,04_03:a_w70] [--titel_a=A0 --titel_b=A1]

Zeile = <Zelle in p01_alter_ton_k1_lv>:<Vorlage>. Standard: Mann und Frau 35 (hell, H1) und 70 (mittel, H2), weil die
Vorlagen mit 35 Jahren hell und dunkler sind und die mit 70 einen mittleren Hautton haben.
"""
import json
import os
import sys

import numpy as np
import OpenImageIO as oiio

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HIER))
from messen import bild as B  # noqa: E402
from messen.glb import Glb  # noqa: E402
from messen.kamera import Kamera  # noqa: E402
from messen.netz_masse import Netzmesser  # noqa: E402

SCHRIFT = os.path.join(HIER, "..", "..", "..", "game", "assets", "oberflaeche", "schriften", "Alegreya-Variabel.ttf")
NAMEN = {"00_01": "Mann 35 hell (H1)", "03_01": "Frau 35 hell (H1)", "01_03": "Mann 70 mittel (H2)", "04_03": "Frau 70 mittel (H2)",
         "01_01": "Mann 35 mittel (H2)", "04_01": "Frau 35 mittel (H2)"}


def pupillen_kopf(zz_json, nm):
    d = json.load(open(zz_json, encoding="utf-8"))
    P = nm.netz.punkte(d["kopf"]["szene"])
    p, _ = Kamera(d).projizieren(np.array([P["ps_r"], P["ps_l"]]))
    a, b = tuple(p[0]), tuple(p[1])
    return (a, b) if a[0] < b[0] else (b, a)


def vorlage_bild(eintrag, ziel_pl, ziel_pr, form):
    ordner = os.path.expanduser(eintrag.get("ordner", ""))
    if not ordner:
        ordner = os.path.expanduser("~/werkzeuge/charakter/vorlagen-haar/" + eintrag["id"])
    img = B.lesen(os.path.join(ordner, eintrag["vorn"]["bild"]))
    p = eintrag["vorn"]["punkte"]
    vl, vr = sorted([tuple(p["ps_r"]), tuple(p["ps_l"])])
    f = np.hypot(ziel_pr[0] - ziel_pl[0], ziel_pr[1] - ziel_pl[1]) / np.hypot(vr[0] - vl[0], vr[1] - vl[1])
    k = B.skalieren(img, f)
    vm = ((vl[0] + vr[0]) / 2 * f, (vl[1] + vr[1]) / 2 * f)
    zm = ((ziel_pl[0] + ziel_pr[0]) / 2, (ziel_pl[1] + ziel_pr[1]) / 2)
    aus = np.full((form[0], form[1], 3), 0.45, dtype=np.float32)
    dx, dy = int(round(zm[0] - vm[0])), int(round(zm[1] - vm[1]))
    h, w = k.shape[:2]
    y0, x0 = max(0, dy), max(0, dx)
    y1, x1 = min(form[0], dy + h), min(form[1], dx + w)
    if y1 > y0 and x1 > x0:
        aus[y0:y1, x0:x1] = k[y0 - dy:y1 - dy, x0 - dx:x1 - dx, :3]
    return aus


def text(buf, x, y, t, groesse=26):
    try:
        oiio.ImageBufAlgo.render_text(buf, x, y, t, groesse, os.path.abspath(SCHRIFT), (0.92, 0.9, 0.86))
    except Exception:  # noqa: BLE001 – ohne Schrift bleibt das Blatt ohne Beschriftung
        pass


def main():
    opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    ma, mb = os.path.expanduser(opt["a"]), os.path.expanduser(opt["b"])
    zeilen = [z.split(":") for z in opt.get("zeilen", "00_01:h_m35_hell,03_01:h_w35_hell,01_03:a_m70,04_03:a_w70").split(",")]
    vm = json.load(open(os.path.join(HIER, "vorlagen_merkpunkte.json"), encoding="utf-8"))["vorlagen"]
    nm = Netzmesser(Glb(opt.get("glb", "~/werkzeuge/charakter/glb5/kopf5.glb")))
    bogen = "p01_alter_ton_k1_lv"
    zellen = []
    for zz, vid in zeilen:
        a = B.lesen(os.path.join(ma, bogen, zz + ".png"))
        b = B.lesen(os.path.join(mb, bogen, zz + ".png"))
        pl, pr = pupillen_kopf(os.path.join(mb, bogen, zz + ".json"), nm)
        e = dict(vm[vid], id=vid)
        zellen.append((zz, vid, a, b, vorlage_bild(e, pl, pr, b.shape[:2])))
    aus = os.path.expanduser(opt["aus"])
    os.makedirs(os.path.dirname(aus), exist_ok=True)
    h, w = zellen[0][2].shape[:2]
    zeilen_j = []
    for zz, vid, a, b, v in zellen:
        pv = f"{aus}_vorlage_{vid}.png"
        B.schreiben(pv, v)
        zeilen_j.append({"titel": f"{NAMEN.get(zz, zz)} · Vorlage {vid}",
                         "zellen": [{"bild": os.path.join(ma, bogen, zz + ".png")}, {"bild": os.path.join(mb, bogen, zz + ".png")}, {"bild": pv}]})
    json.dump({"titel": f"Vergleich {opt.get('titel_a', 'A0 (Stand 5)')} | {opt.get('titel_b', 'A1 (Stand 6)')} | Fotovorlage – P1 K1, L-V",
               "name": os.path.basename(aus), "zelle": [w, h],
               "spalten": [{"titel": opt.get("titel_a", "A0 (Stand 5)")}, {"titel": opt.get("titel_b", "A1 (Stand 6)")},
                           {"titel": "Fotovorlage (auf den IPD skaliert)"}], "zeilen": zeilen_j},
              open(aus + ".json", "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    rand, kopf, fuss = 230, 50, 10
    H, W = kopf + len(zellen) * (h + fuss), rand + 3 * (w + 8)
    blatt = np.full((H, W, 3), 0.11, dtype=np.float32)
    for i, (zz, vid, a, b, v) in enumerate(zellen):
        y = kopf + i * (h + fuss)
        for j, bild in enumerate((a, b, v)):
            x = rand + j * (w + 8)
            blatt[y:y + h, x:x + w] = bild[:h, :w]
    buf = oiio.ImageBuf(oiio.ImageSpec(W, H, 3, oiio.FLOAT))
    buf.set_pixels(oiio.ROI(0, W, 0, H, 0, 1, 0, 3), blatt)
    for j, t in enumerate((opt.get("titel_a", "A0 (Stand 5)"), opt.get("titel_b", "A1 (Stand 6)"), "Fotovorlage (auf den IPD skaliert)")):
        text(buf, rand + j * (w + 8) + 8, 36, t)
    for i, (zz, vid, *_r) in enumerate(zellen):
        y = kopf + i * (h + fuss)
        text(buf, 10, y + h // 2, NAMEN.get(zz, zz), 22)
        text(buf, 10, y + h // 2 + 30, vid, 20)
    buf.write(aus + "_ohne_text.png", "uint8")
    print("Vergleichsblatt", aus + ".json", aus + "_ohne_text.png", W, H)


if __name__ == "__main__":
    main()
