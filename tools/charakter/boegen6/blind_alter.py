"""Blindblatt Altersschätzung (Charakter Phase 6, Plan 5.5): die 24 Köpfe aus P1 (K1, L-S: Mann/Frau × H1–H3 × 20/35/50/70)
gemischt und nummeriert 1–24, als Bogen mit fertigen Bildern („bild“-Zellen, Beschriftung durch die Testszene). Der
Schlüssel kommt in die Prüfmappe (nicht ins Repository); sein SHA-256 wird ausgegeben.

Aufruf: python3 tools/charakter/boegen6/blind_alter.py <ordner p01_alter_ton_k1_ls> <prüfmappe> [startwert]
"""
import hashlib
import json
import os
import random
import sys

ALTER = [20, 35, 50, 70]
ZEILEN = [("m", "H1"), ("m", "H2"), ("m", "H3"), ("w", "H1"), ("w", "H2"), ("w", "H3")]


def main():
    ordner, mappe = sys.argv[1], sys.argv[2]
    zz = random.Random(int(sys.argv[3]) if len(sys.argv) > 3 else 520)
    zellen = [(zi, si) for zi in range(6) for si in range(4)]
    zz.shuffle(zellen)
    schluessel = {}
    eintraege = []
    for n, (zi, si) in enumerate(zellen, 1):
        g, h = ZEILEN[zi]
        schluessel[str(n)] = {"geschlecht": g, "haut": h, "alter": ALTER[si], "datei": f"{zi:02d}_{si:02d}.png"}
        eintraege.append({"titel": str(n), "bild": os.path.join(os.path.abspath(ordner), f"{zi:02d}_{si:02d}.png")})
    roh = json.dumps(schluessel, sort_keys=True, indent=1).encode("utf-8")
    with open(os.path.join(mappe, "schluessel_blind_alter.json"), "wb") as f:
        f.write(roh)
    bogen = {"titel": "Blind: Alter schätzen (Nummern 1–24)", "name": "blind_alter", "zelle": [400, 480], "schrift": 2.0, "rand": 20,
             "spalten": [{"titel": ""}] * 6, "zeilen": [{"titel": "", "zellen": eintraege[i:i + 6]} for i in range(0, 24, 6)]}
    with open(os.path.join(mappe, "blind_alter.json"), "w", encoding="utf-8") as f:
        json.dump(bogen, f, indent=1, ensure_ascii=False)
    print("Schlüssel SHA-256", hashlib.sha256(roh).hexdigest())


if __name__ == "__main__":
    main()
