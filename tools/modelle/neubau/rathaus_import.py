"""Uebernimmt die Import-Einstellungen der Wohnhaus-Texturen (VRAM-Kompression, Mipmaps, Normalkarte) fuer die Rathaus-Texturen.

    python tools/modelle/neubau/rathaus_import.py

Godot legt fuer neue Bilder Vorgaben an (ohne Mipmaps, unkomprimiert); die Trim-Sheets brauchen dieselben Einstellungen wie die
des Wohnhauses. Ersetzt in jeder rathaus_*.png.import den Abschnitt [params] durch den der Vorlage und loescht die Zielangaben,
damit Godot beim naechsten Import neu schreibt. uid bleibt.
"""
import os
import re

HIER = os.path.dirname(os.path.abspath(__file__))
ORDNER = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(HIER))), "game", "assets", "texturen", "neubau")
VORLAGEN = {"_albedo.png": "trim_albedo.png", "_normal.png": "trim_normal.png", "_orm.png": "trim_orm.png", "_ao_atlas.png": "wohnhaus_mensch_ao_atlas.png"}


def abschnitt(text, name):
    m = re.search(r"^\[" + name + r"\]\n(.*?)(?=^\[|\Z)", text, re.S | re.M)
    return m.group(1) if m else ""


for datei in sorted(os.listdir(ORDNER)):
    if not (datei.startswith("rathaus") and datei.endswith(".png.import")):
        continue
    bild = datei[:-len(".import")]
    vorlage = next((v for k, v in VORLAGEN.items() if bild.endswith(k)), None)
    if vorlage is None:
        continue
    alt = open(os.path.join(ORDNER, datei), encoding="utf-8").read()
    muster = open(os.path.join(ORDNER, vorlage + ".import"), encoding="utf-8").read()
    uid = re.search(r'^uid="[^"]+"', alt, re.M).group(0)
    neu = muster.replace(vorlage, bild)
    neu = re.sub(r'^uid="[^"]+"', uid, neu, flags=re.M)
    # Pfade der importierten Dateien: Godot schreibt sie neu (Hash im Namen); die der Vorlage passen nicht
    neu = re.sub(r'^path(\.\w+)?=.*\n', "", neu, flags=re.M)
    neu = re.sub(r'^dest_files=.*\n', "", neu, flags=re.M)
    open(os.path.join(ORDNER, datei), "w", encoding="utf-8", newline="\n").write(neu)
    print("uebernommen", datei, "<-", vorlage)
