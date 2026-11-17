"""Trim-Sheet der Raeume im Zwergenhaus: raeume_innen_trim_zwerg_{albedo,normal,orm}.png aus dem der Menschen.

    blender -b --factory-startup --python tools/modelle/raeume/raeume_trimsheet_zwerg.py -- --quellen=<ordner>

Ein Aufruf, nachdem raeume_trimsheet.py raeume_innen_trim_* geschrieben hat: Ersetzt nur das Band „putz“ (Kalkputz) durch behauene
Quader und den Boden „dielen“ (Hinterzimmer) durch dunkles Pflaster; das Parkett der Bibliothek bleibt Parkett (Rueckmeldung zum
Vergleichsbild 15.11.2026: „Parkett statt Pflaster“), ebenso alles andere (Moebelholz, Teppich, Buecher, Karte, Kleinteile) Pixel fuer
Pixel. Quellen Poly Haven CC0: rough_block_wall, cobblestone_floor_01 (HERKUNFT.md). Keine Bild-KI.
"""
import os
import sys

import bpy

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "neubau"))
sys.path.insert(0, HIER)
import trimsheet as ts                                                    # noqa: E402
from trimsheet import Quelle, faerbe                                      # noqa: E402
import trimsheet_stufen as st                                             # noqa: E402  (lade_blatt)
from raeume_trimsheet import BAENDER_RAEUME as B                          # noqa: E402


def baue(quellen):
    blatt = st.lade_blatt("raeume_innen_trim")
    stein = Quelle(quellen, "rough_block_wall", kante=1024)
    a, e = blatt.zeilen(B["putz"])
    f, n, arm = stein.streifen(e - a, ab=140)
    blatt.setze(B["putz"], faerbe(f, (0.58, 0.57, 0.54), kontrast=1.05, eigenfarbe=0.4), n, arm)
    blatt.verlauf(B["putz"], 0.9, 0.82, 40)
    pflaster = Quelle(quellen, "cobblestone_floor_01", kante=1024)
    for name, ab in (("dielen", 0),):
        a, e = blatt.zeilen(B[name])
        f, n, arm = pflaster.streifen(e - a, ab=ab)
        blatt.setze(B[name], faerbe(f, (0.40, 0.38, 0.35), kontrast=0.9, eigenfarbe=0.4), n, arm)
    blatt.speichere("raeume_innen_trim_zwerg")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict(a[2:].split("=", 1) for a in args if a.startswith("--") and "=" in a)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    baue(opt["quellen"])
