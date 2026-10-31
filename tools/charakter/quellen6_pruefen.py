#!/usr/bin/env python3
"""Prüft das Quellenmanifest quellen6.json (Charakter Phase 6, S0): Git-Stände, Prüfsummen und Versionen.

    python3 -I tools/charakter/quellen6_pruefen.py        (oder: tools/charakter/einrichten.sh pruefen)

Gibt je Quelle OK / FEHLT / ABWEICHUNG aus; Rückgabe 1, wenn etwas nicht stimmt. Nur Standardbibliothek.
"""
import hashlib
import json
import os
import subprocess
import sys

HIER = os.path.dirname(os.path.abspath(__file__))


def pfad(p):
    return os.path.expanduser(p)


def sha256(datei):
    h = hashlib.sha256()
    with open(datei, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def baum(ordner):
    """SHA-256 über die Zeilen „<sha256>  ./<pfad>“ aller Dateien (ohne __pycache__), nach Pfad sortiert – wie
    `find . -type f -not -path '*/__pycache__/*' -print0 | sort -z | xargs -0 sha256sum | sha256sum`."""
    dateien = []
    for wurzel, ordn, namen in os.walk(ordner):
        ordn[:] = [o for o in ordn if o != "__pycache__"]
        for n in namen:
            voll = os.path.join(wurzel, n)
            if os.path.isfile(voll) and not os.path.islink(voll):
                dateien.append("./" + os.path.relpath(voll, ordner))
    dateien.sort(key=lambda s: s.encode())
    zeilen = "".join(f"{sha256(os.path.join(ordner, d[2:]))}  {d}\n" for d in dateien)
    return hashlib.sha256(zeilen.encode()).hexdigest(), len(dateien)


def main():
    q = json.load(open(os.path.join(HIER, "quellen6.json"), encoding="utf-8"))
    fehler = 0
    for e in q["quellen"]:
        art, name = e["art"], e["name"]
        try:
            if art == "git":
                ort = pfad(e["ort"])
                kopf = subprocess.run(["git", "-C", ort, "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
                sauber = subprocess.run(["git", "-C", ort, "status", "--porcelain"], capture_output=True, text=True).stdout.strip() == ""
                ok = kopf == e["commit"] and sauber
                print(f"{'OK ' if ok else 'ABWEICHUNG'} {name}: {kopf[:12]}{'' if sauber else ' (geändert)'}")
            elif art == "ordner":
                h, n = baum(pfad(e["ort"]))
                ok = h == e["baum_sha256"]
                print(f"{'OK ' if ok else 'ABWEICHUNG'} {name} {e.get('version', '')}: {n} Dateien, {h[:12]}")
            elif art == "zips":
                ok = True
                for datei, soll in e["sha256"].items():
                    p = os.path.join(pfad(e["ort"]), datei)
                    ist = sha256(p) if os.path.exists(p) else "fehlt"
                    if ist != soll:
                        ok = False
                        print(f"ABWEICHUNG {name}: {datei} {ist[:12]}")
                print(f"{'OK ' if ok else 'ABWEICHUNG'} {name}: {len(e['sha256'])} Zips")
            elif art == "programm":
                aus = subprocess.run([pfad(e["ort"])] + e["aufruf"], capture_output=True, text=True, timeout=120)
                ok = e["erwartet"] in (aus.stdout + aus.stderr)
                zeile = next((z for z in (aus.stdout + aus.stderr).splitlines() if e["erwartet"].split()[0] in z), "")
                print(f"{'OK ' if ok else 'ABWEICHUNG'} {name}: {zeile.strip()[:60]}")
            elif art == "datei":
                p = pfad(e["ort"])
                ist = sha256(p) if os.path.exists(p) else "fehlt"
                ok = ist == e["sha256"]
                print(f"{'OK ' if ok else 'ABWEICHUNG'} {name}: {ist[:12]}")
            else:
                ok = True
                print(f"-- {name}: {art} ({e.get('sha256', e.get('hinweis', ''))[:70]})")
        except (OSError, subprocess.SubprocessError) as ex:
            ok = False
            print(f"FEHLT {name}: {ex}")
        fehler += 0 if ok else 1
    print("Manifest", "grün" if fehler == 0 else f"{fehler} Abweichung(en)")
    return 1 if fehler else 0


if __name__ == "__main__":
    sys.exit(main())
