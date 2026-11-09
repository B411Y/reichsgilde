"""Einrichtung der Wohnstube des Wohnhauses (Personal und Talentbaum, PR #224; Platzhalter): Vorratskammer, Küche, Wappen über der Tür, Gute Stube,
Bibliothek, eiserne Riegel, Hofhund – je ein Knoten `Einrichtung_<Kennung>` (gekauft erst sichtbar, wie in den Raummodellen der Betriebe).

    blender -b --python tools/modelle/wohnstube_innen.py -- [--ohne-bilder]

Schreibt game/assets/modelle/innen/wohnstube_innen.glb. Die Wohnstube selbst bleibt im Spiel aus Grundkörpern (Stadtansicht.Raeume.cs,
`WohnstubeEinrichten`); der Ursprung des Modells ist ihre Mitte auf dem Boden (Raum 8,4 × 6,6 m; Blender: Y nach hinten, im Spiel −Z). Die Stücke stehen
so, dass sie Herd, Tisch, Bett, Truhe, Wiege und den Schmuck nach der Stufe (`WohnhausSchmuck`: Teppich, Tellerbord an der Ostwand, Wandbehang) nicht berühren.
Die Stücke selbst baut personal_einrichtung.py.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import innenausbau as ia  # noqa: E402
import personal_einrichtung as pe  # noqa: E402

NAME = "wohnstube_innen"
BX, BY = 4.2, 3.3          # halbe Breite und Tiefe der Wohnstube

# Kennung → (Baufunktion, Ursprung x, y, Drehung, Höhe)
STUECKE = (
    ("Kueche", pe.kueche, (-0.3, BY, 180, 0.0)),                 # Rückwand neben dem Herd
    ("Vorratskammer", pe.vorratskammer, (-BX, -0.4, -90, 0.0)),    # linke Wand
    ("Riegel", pe.riegel, (BX, -2.6, 90, 0.0)),                    # rechte Wand, vorn (das Tellerbord hängt weiter hinten)
    ("Hauswappen", pe.hauswappen, (BX, -2.6, 90, 1.9)),            # über der Tür
    ("Hund", pe.hund, (-2.2, 0.9, 180, 0.0)),                      # vor dem Herd
    ("Bibliothek", pe.bibliothek, (-2.4, -BY, 0, 0.0)),            # Vorderwand links
    ("Stube", pe.stube, (1.9, -BY, 0, 0.0)),                       # Vorderwand rechts
)


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    objekte = []
    for kennung, funktion, (x, y, dreh, z) in STUECKE:
        netz = bs.Netz("Einrichtung_" + kennung)
        funktion(netz, st, pe.rahmen(x, y, dreh, z))
        o = netz.objekt()
        bs.zustand(o)
        objekte.append(o)
    return objekte


def main():
    objekte = bauen()
    vorher, nachher = ia.farben_zusammenlegen(objekte)
    print(f"Materialien: {vorher} → {nachher}")
    ia.pruefbericht(objekte, NAME)
    print("geschrieben:", bs.exportieren(NAME, objekte))


if __name__ == "__main__":
    main()
