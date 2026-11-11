"""Schreibt aus den Messdateien von zugtiere_pruefen.py die Tabellen (Markdown): python -I tools/modelle/zugtiere_bericht.py <ordner> <ausgabe.md> <glb> [<glb> …]"""
import json
import os
import sys


def main():
    ordner, aus, dateien = sys.argv[1], sys.argv[2], sys.argv[3:]
    z = ["# Tiere: Bewegungen, Messung\n", "Messung an den GLB-Dateien (Blender-Import, Skelett wie im Spiel) mit `tools/modelle/zugtiere_pruefen.py`; alle Zahlen in Millimetern, wenn nicht anders gesagt.\n"]
    for g in dateien:
        d = json.load(open(os.path.join(ordner, g + "_messung.json"), encoding="utf-8"))
        z.append(f"\n## {g}\n")
        z.append("### Hufe je Clip\n")
        z.append("Stand = Sohle höchstens 6 mm über dem tiefsten Punkt des Clips. Rutschen = Spanne von Y − Rumpfweg über einen zusammenhängenden Stand (0 = fest in der Welt). Hub = höchster Punkt der Sohle über dem Boden.\n")
        z.append("| Clip | Huf | Bilder am Boden | Sohle im Stand min / max | Rutschen | Hub | Abhebungen |")
        z.append("|---|---|---|---|---|---|---|")
        for c, e in d["clips"].items():
            for h, v in e["huf"].items():
                if c.startswith("Stehen") or c == "Fressen":
                    if v["abhebungen"] == 0 and v["hub_mm"] < 5:
                        continue
                r = "-" if v["rutschen_mm"] is None else v["rutschen_mm"]
                z.append(f"| {c} | {h} | {v['am_boden']} | {v['stand_z_min_mm']} / {v['stand_z_max_mm']} | {r} | {v['hub_mm']} | {v['abhebungen']} |")
        z.append("\n### Clips: Boden, Schleife, VAT\n")
        z.append("tiefster = tiefster Punkt des Netzes (negativ: im Boden). Naht = größte Eckenverschiebung zwischen Bild 0 und letztem Bild (Schleifen: 0 = nahtlos); Naht-Beschleunigung gegen die größte Beschleunigung im Clip (zweite Differenz je Bild). VAT12/VAT24 = größte und mittlere Abweichung der gebackenen Bewegung (12 bzw. 24 Bilder/s, linear gemischt) von der Skelettpose, geprüft in der Mitte zwischen den Bildern.\n")
        z.append("| Clip | Bilder | Dauer s | tiefster | Naht | Naht-Beschl. / größte | VAT12 max / Mittel | VAT24 max / Mittel |")
        z.append("|---|---|---|---|---|---|---|---|")
        for c, e in d["clips"].items():
            z.append(f"| {c} | {e['bilder']} | {e['dauer']} | {e['tiefster_mm']} | {e['naht_mm']} | {e['naht_beschl_mm']} / {e['max_beschl_mm']} | "
                     f"{e['vat12_max_mm']} / {e['vat12_mittel_mm']} | {e['vat24_max_mm']} / {e['vat24_mittel_mm']} |")
        z.append("\n### Übergänge (größte Eckenverschiebung zwischen letztem Bild des ersten und erstem Bild des zweiten Clips)\n")
        z.append("| Übergang | mm |")
        z.append("|---|---|")
        for k, v in d["uebergang"].items():
            z.append(f"| {k} | {v} |")
    open(aus, "w", encoding="utf-8", newline="\n").write("\n".join(z) + "\n")


main()
