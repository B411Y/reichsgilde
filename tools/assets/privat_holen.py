"""Kopiert die privaten GameDev.tv-Texturen aus dem privaten Speicher nach game/assets/privat/ (von Git ignoriert).

    python -I tools/assets/privat_holen.py [<Ordner>]        (oder Umgebungsvariable REICHSGILDE_PRIVAT)
    python -I tools/assets/privat_holen.py --pruefen         (nur prüfen, was in game/assets/privat/ liegt)

Der Ordner enthält die Dateien so, wie sie in docs/assets/privat_manifest.json stehen (boden/…, fels/…, schnee/…, LIZENZ.txt); liegt dort ein
Unterordner „spiel“, gilt der. Jede Datei wird gegen die SHA-256 des Manifests geprüft, bevor sie kopiert wird. Fehlt der Ordner, passiert nichts
(Rückgabe 0, ein Hinweis): Das Spiel nimmt dann die eigenen Texturen, auch die CI (kein Fehler).
Die Texturen stehen unter der GameDev.tv General Asset License 2.0 – sie dürfen nicht ins öffentliche Repository (siehe docs/assets/README.md).
Danach einmal den Godot-Editor öffnen oder `godot --headless --import --path game` laufen lassen, damit Godot sie importiert.
"""
import argparse
import hashlib
import json
import os
import shutil
import sys

WURZEL = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
MANIFEST = os.path.join(WURZEL, "docs", "assets", "privat_manifest.json")
ZIEL = os.path.join(WURZEL, "game", "assets", "privat")


def sha256(pfad):
    h = hashlib.sha256()
    with open(pfad, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("quelle", nargs="?", default=os.environ.get("REICHSGILDE_PRIVAT"))
    ap.add_argument("--pruefen", action="store_true", help="nur game/assets/privat/ gegen das Manifest prüfen")
    a = ap.parse_args()
    with open(MANIFEST, encoding="utf-8") as f:
        manifest = json.load(f)
    if a.pruefen:
        quelle = ZIEL
    else:
        quelle = a.quelle
        if not quelle:
            print("Kein privater Speicher angegeben (Argument oder REICHSGILDE_PRIVAT): nichts zu holen, das Spiel nimmt die eigenen Texturen.")
            return 0
        if os.path.isdir(os.path.join(quelle, "spiel")):
            quelle = os.path.join(quelle, "spiel")
        if not os.path.isdir(quelle):
            print(f"Ordner {quelle} fehlt: nichts zu holen, das Spiel nimmt die eigenen Texturen.")
            return 0
    fehler = fehlt = ok = 0
    for e in manifest["dateien"]:
        q = os.path.join(quelle, e["datei"])
        if not os.path.isfile(q):
            print(f"fehlt: {e['datei']}")
            fehlt += 1
            continue
        if os.path.getsize(q) != e["groesse"] or sha256(q) != e["sha256"]:
            print(f"FALSCH (Größe oder SHA-256 passt nicht zum Manifest): {e['datei']}")
            fehler += 1
            continue
        if not a.pruefen:
            z = os.path.join(ZIEL, e["datei"])
            os.makedirs(os.path.dirname(z), exist_ok=True)
            shutil.copyfile(q, z)
        ok += 1
    print(f"{ok} Dateien {'in Ordnung' if a.pruefen else 'nach game/assets/privat/ kopiert'}, {fehlt} fehlen, {fehler} falsch.")
    return 1 if fehler else 0


if __name__ == "__main__":
    sys.exit(main())
