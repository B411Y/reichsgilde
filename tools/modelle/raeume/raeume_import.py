"""Uebernimmt die Import-Einstellungen der Wohnhaus-Texturen (VRAM-Kompression, Mipmaps, Normalkarte) fuer die Texturen der Raeume.

    python tools/modelle/raeume/raeume_import.py

Godot legt fuer neue Bilder Vorgaben an (ohne Mipmaps, unkomprimiert); das Trim-Sheet der Raeume und ihr AO-Atlas brauchen dieselben
Einstellungen wie die der Wohnstube. Ersetzt in raeume_innen_trim_*.png.import und wohnhaus_mensch_raeume_innen_ao_atlas.png.import den
Abschnitt [params] durch den der Vorlage und loescht die Zielangaben, damit Godot beim naechsten Import neu schreibt. uid bleibt.
(Wie tools/modelle/neubau/rathaus_import.py im Rathaus-Neubau.)
"""
import os
import re

HIER = os.path.dirname(os.path.abspath(__file__))
ORDNER = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(HIER))), "game", "assets", "texturen", "neubau")
VORLAGEN = {
    "raeume_innen_trim_albedo.png": "innen_trim_albedo.png",
    "raeume_innen_trim_normal.png": "innen_trim_normal.png",
    "raeume_innen_trim_orm.png": "innen_trim_orm.png",
    "wohnhaus_mensch_raeume_innen_ao_atlas.png": "wohnhaus_mensch_innen_ao_atlas.png",
}

for bild, vorlage in VORLAGEN.items():
    ziel = os.path.join(ORDNER, bild + ".import")
    if not os.path.exists(ziel):
        print("fehlt (erst in Godot importieren):", bild)
        continue
    alt = open(ziel, encoding="utf-8").read()
    muster = open(os.path.join(ORDNER, vorlage + ".import"), encoding="utf-8").read()
    uid = re.search(r'^uid="[^"]+"', alt, re.M).group(0)
    neu = muster.replace(vorlage, bild)
    neu = re.sub(r'^uid="[^"]+"', uid, neu, flags=re.M)
    neu = re.sub(r'^path(\.\w+)?=.*\n', "", neu, flags=re.M)      # Godot schreibt die Pfade neu (Hash im Namen)
    neu = re.sub(r'^dest_files=.*\n', "", neu, flags=re.M)
    open(ziel, "w", encoding="utf-8", newline="\n").write(neu)
    print("uebernommen", bild, "<-", vorlage)
