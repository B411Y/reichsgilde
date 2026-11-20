"""Modulares Wohnhaussystem: packt die AO-Atlanten der Zeilen-Saetze zu einem Texture2DArray, eine Ebene je Satz.

    python -I tools/modelle/neubau/zeile_ao_ebenen.py

Braucht Python 3 mit numpy und Pillow (kein Blender). Liest die Namen der Saetze aus zeile_module.py (SAETZE, per ast gelesen,
nicht abgeschrieben) und schreibt game/assets/texturen/neubau/zeilen_ao_ebenen.png: die Atlanten zeile_<satz>_ao_atlas.png
senkrecht untereinander (Ebene i = Bildzeilen i*2048 bis (i+1)*2048; Import als Texture2DArray, slices/vertical = Zahl der
Saetze, verlustfrei wie haeuser_ao_ebenen.png). Die Reihenfolge ist die von Zeilenplan.Saetze (game/scripts/Zeilenplan.cs,
AoEbene); ZeilenplanTests prueft, dass beide Listen und die Ebenenzahl der Datei zusammenpassen. Nach jedem neuen oder neu
gebackenen Satz einmal laufen lassen; die .import-Datei wird bei geaenderter Ebenenzahl nachgezogen.
"""
import ast
import os
import re

import numpy as np
from PIL import Image

HIER = os.path.dirname(os.path.abspath(__file__))
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
NEUBAU = os.path.join(WURZEL, "game", "assets", "texturen", "neubau")
N = 2048

IMPORT_VORLAGE = """[remap]

importer="2d_array_texture"
type="CompressedTexture2DArray"

[deps]

source_file="res://assets/texturen/neubau/zeilen_ao_ebenen.png"

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/hdr_compression=1
compress/channel_pack=0
mipmaps/generate=true
mipmaps/limit=-1
slices/horizontal=1
slices/vertical={ebenen}
"""


def saetze():
    """Die Schluessel von SAETZE in zeile_module.py, in der Reihenfolge der Datei."""
    baum = ast.parse(open(os.path.join(HIER, "zeile_module.py"), encoding="utf-8").read())
    for knoten in baum.body:
        if isinstance(knoten, ast.Assign) and any(getattr(z, "id", "") == "SAETZE" for z in knoten.targets):
            return [k.value for k in knoten.value.keys]
    raise SystemExit("SAETZE nicht gefunden")


def main():
    namen = saetze()
    ebenen = []
    for name in namen:
        pfad = os.path.join(NEUBAU, f"zeile_{name}_ao_atlas.png")
        if not os.path.exists(pfad):
            raise SystemExit(f"AO-Atlas fehlt: {pfad}")
        bild = Image.open(pfad).convert("RGB")
        if bild.size != (N, N):
            raise SystemExit(f"{pfad}: nicht {N} x {N}")
        ebenen.append(np.asarray(bild))
        print(f"AO-Ebene {len(ebenen) - 1} {name}: {pfad}")
    ziel = os.path.join(NEUBAU, "zeilen_ao_ebenen.png")
    Image.fromarray(np.concatenate(ebenen, axis=0), "RGB").save(ziel, optimize=True)
    imp = ziel + ".import"
    if not os.path.exists(imp):
        open(imp, "w", encoding="utf-8", newline="\n").write(IMPORT_VORLAGE.format(ebenen=len(ebenen)))
    else:
        text = open(imp, encoding="utf-8").read()
        neu = re.sub(r"slices/vertical=\d+", f"slices/vertical={len(ebenen)}", text)
        if neu != text:
            open(imp, "w", encoding="utf-8", newline="\n").write(neu)
    print(f"geschrieben: {ziel} ({len(ebenen)} Ebenen)")


if __name__ == "__main__":
    main()
