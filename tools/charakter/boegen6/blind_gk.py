#!/usr/bin/env python3
"""Blind-Ordner für das Grundkopf-Panel (Grundkopf 5b): mischt „vorher“ und „neu“ (A/B bei Köpfen und Augen, X/Y bei den Fratzen-Sätzen) und
teilt die langen Bögen in zwei Hälften, damit kein Bild für die Prüfer auf die Hälfte verkleinert wird. Der Schlüssel (SHA-256 im Bericht) liegt
außerhalb des Repositorys unter ~/werkzeuge/charakter/bilder/phase6/GK5B.

    PYTHONPATH=<ordner mit PIL> <Blender-Python> tools/charakter/boegen6/blind_gk.py <arbeitsordner> <runde>
    <arbeitsordner> enthält bv/ (Bilder „vorher“ aus panel_gk.py boegen + schau.sh), bn/ („neu“) und b/ (personen.json); Ausgabe <arbeitsordner>/blind_<runde>/
Die Hälften folgen den Maßen aus panel_gk.py (Kopf 140 px, Zeile 577 px bei Fratzen, 777 px bei Lidformen).
"""
import sys, os, json, random, shutil, hashlib
from PIL import Image
S, runde = sys.argv[1], sys.argv[2]
out = f"{S}/blind_{runde}"
shutil.rmtree(out, ignore_errors=True); os.makedirs(out)
r = random.Random("gk5b-" + runde)
def paar(vorher, neu, ziel):
    a_neu = r.random() < 0.5
    shutil.copy(neu if a_neu else vorher, f"{out}/{ziel}_A.png")
    shutil.copy(vorher if a_neu else neu, f"{out}/{ziel}_B.png")
    return {"A": "neu" if a_neu else "vorher", "B": "vorher" if a_neu else "neu"}
def teilen(quelle, ziel_basis, zeilen_oben, kopf, pitch):
    """Bogen in zwei Hälften schneiden (obere Zeilen / Rest), damit die Bilder nicht auf die Hälfte verkleinert werden."""
    im = Image.open(quelle)
    y = kopf + zeilen_oben * pitch
    im.crop((0, 0, im.width, y)).save(f"{ziel_basis}_1.png")
    im.crop((0, y, im.width, im.height)).save(f"{ziel_basis}_2.png")
    return im.size
sch = {}
sch["kopf_mann"] = paar(f"{S}/bv/vorher_kopf_m.png", f"{S}/bn/neu_kopf_m.png", "kopf_mann")
sch["kopf_frau"] = paar(f"{S}/bv/vorher_kopf_w.png", f"{S}/bn/neu_kopf_w.png", "kopf_frau")
sch["augen_mann"] = paar(f"{S}/bv/vorher_augen_m.png", f"{S}/bn/neu_augen_m.png", "augen_mann")
sch["augen_frau"] = paar(f"{S}/bv/vorher_augen_w.png", f"{S}/bn/neu_augen_w.png", "augen_frau")
x_neu = r.random() < 0.5
tmp = f"{S}/tmp_blind"; shutil.rmtree(tmp, ignore_errors=True); os.makedirs(tmp)
for g in ("m1", "m2", "w1", "w2"):
    xq = f"{S}/bn/neu_fratzen_{g}.png" if x_neu else f"{S}/bv/vorher_fratzen_{g}.png"
    yq = f"{S}/bv/vorher_fratzen_{g}.png" if x_neu else f"{S}/bn/neu_fratzen_{g}.png"
    teilen(xq, f"{out}/fratzen_X_{g}", 3, 140, 577)    # Zeilen 1–3 (6 Köpfe) und 4–5 (4 Köpfe)
    teilen(yq, f"{out}/fratzen_Y_{g}", 3, 140, 577)
sch["fratzen"] = {"X": "neu" if x_neu else "vorher", "Y": "vorher" if x_neu else "neu"}
for g in ("m", "w"):
    teilen(f"{S}/bn/neu_lider_{g}.png", f"{out}/lidformen_{g}", 2, 140, 777)   # Zeilen 1–2 und 3
sch["lider"] = json.load(open(f"{S}/b/personen.json"))["lider"]
shutil.rmtree(tmp, ignore_errors=True)
roh = json.dumps(sch, sort_keys=True, ensure_ascii=False, indent=1).encode()
k = os.path.expanduser("~/werkzeuge/charakter/bilder/phase6/GK5B"); os.makedirs(k, exist_ok=True)
open(f"{k}/schluessel_{runde}.json", "wb").write(roh)
print(sorted(os.listdir(out))); print("SHA-256", hashlib.sha256(roh).hexdigest())
