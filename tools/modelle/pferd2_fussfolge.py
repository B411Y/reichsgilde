"""Fussfolgediagramm (Stand/Schwung je Bein ueber den Zyklus) aus den Protokollen von pferd2_gang.py ("ZS"-Zeilen: tiefste Hufeisenecke je Bild in mm),
dazu die Ablesung der Muybridge-Tafel 563 (je Tafelbild: welches Vorder-/Hinterbein in der Luft).

    python -I tools/modelle/pferd2_fussfolge.py <aus.png> <gang.log>=<Name> [<gang2.log>=<Name2>] [--tafel=<tafel.json>]

Stand: tiefste Ecke unter 6 mm (wie die Messung von pferd2_gang.py). Die Tafel-Ablesung (JSON) nennt je Bild 1-12 "V" und "H" mit 0 (alle am Boden) oder 1
(ein Bein in der Luft) und die Phase des nahen Vorderbeins von Bild 3 (Zyklus 8,5 Tafelbilder).
"""
import json
import sys

from PIL import Image, ImageDraw, ImageFont

ARGS = sys.argv[1:]
POS = [a for a in ARGS if not a.startswith("--")]
OPT = dict(a[2:].split("=", 1) for a in ARGS if a.startswith("--") and "=" in a)
try:
    font = ImageFont.truetype("arial.ttf", 15)
except OSError:
    font = ImageFont.load_default()
reihen = []
for a in POS[1:]:
    pfad, name = a.rsplit("=", 1)
    zs = {}
    for z in open(pfad):
        if z.startswith("ZS "):
            t = z.split()
            zs[t[1]] = [int(v) < 6 for v in t[2:]]
    reihen.append((name, zs))
B, ZH, L = 1000, 26, 150
hoehe = len(reihen) * (4 * ZH + 50) + ((140 + len(reihen) * 2 * ZH) if "tafel" in OPT else 0) + 40
bild = Image.new("RGB", (B + L + 20, hoehe), (255, 255, 255))
d = ImageDraw.Draw(bild)
y = 10
d.text((L, y), "Fussfolge: dunkel = Stand, hell = Schwung; ein Zyklus (1,083 s) von links nach rechts, Beginn Aufsetzen LH", fill=(0, 0, 0), font=font)
y += 28
for name, zs in reihen:
    d.text((10, y), name, fill=(0, 0, 0), font=font)
    y += 22
    for k, bein in enumerate(("LH", "LF", "RH", "RF")):
        kont = zs[bein]
        n = len(kont)
        d.text((10, y + 4), bein, fill=(0, 0, 0), font=font)
        for f in range(n):
            x0, x1 = L + B * f / n, L + B * (f + 1) / n
            farbe = (60, 60, 60) if kont[f] else (225, 225, 225)
            if bein in ("LH", "RF"):
                farbe = (150, 40, 40) if kont[f] else (245, 215, 215)          # diagonales Paar LH+RF rot, RH+LF blau
            else:
                farbe = (40, 70, 150) if kont[f] else (215, 225, 245)
            d.rectangle([x0, y, x1 - 1, y + ZH - 4], fill=farbe)
        y += ZH
    y += 24
if "tafel" in OPT:
    t = json.load(open(OPT["tafel"], encoding="utf-8-sig"))
    d.text((10, y), "Muybridge 563 (Ablesung je Tafelbild, 1 Tafelbild = 1/%.1f Zyklus)" % t["zyklus"], fill=(0, 0, 0), font=font)
    y += 22
    for k, (key, txt) in enumerate((("V", "Vorderbein in der Luft"), ("H", "Hinterbein in der Luft"))):
        d.text((10, y + 4), key, fill=(0, 0, 0), font=font)
        for i, wert in enumerate(t[key]):
            x0 = L + B * i / len(t[key])
            x1 = L + B * (i + 1) / len(t[key])
            d.rectangle([x0, y, x1 - 2, y + ZH - 4], fill=(230, 150, 40) if wert else (225, 225, 225))
            d.text((x0 + 3, y + 3), str(i + 1), fill=(0, 0, 0), font=font)
        y += ZH
    # dieselbe Abtastung fuer jeden Gang: 12 Bilder im Abstand 1/zyklus, Bild 3 = Phase phase_ref des linken Vorderbeins (Viertakt aus dem Protokoll)
    for name, zs in reihen:
        n = len(zs["LF"])
        lf_auf = next(f for f in range(n) if zs["LF"][f] and not zs["LF"][f - 1]) / n
        for key, beine in (("V", ("LF", "RF")), ("H", ("LH", "RH"))):
            d.text((10, y + 4), key + " " + name[:10], fill=(0, 0, 0), font=font)
            for i in range(len(t[key])):
                ph = (lf_auf + t["phase_ref"] + (i + 1 - 3) / t["zyklus"]) % 1.0
                f = int(ph * n) % n
                luft = any(not zs[b][f] for b in beine)
                x0, x1 = L + B * i / len(t[key]), L + B * (i + 1) / len(t[key])
                d.rectangle([x0, y, x1 - 2, y + ZH - 4], fill=(230, 150, 40) if luft else (225, 225, 225))
            y += ZH
    d.text((L, y + 4), t.get("hinweis", ""), fill=(80, 80, 80), font=font)
bild.save(POS[0])
print("geschrieben", POS[0])
