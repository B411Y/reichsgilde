"""Trim-Sheet der Raeume im Elbenhaus: raeume_innen_trim_elb_{albedo,normal,orm}.png aus dem der Menschen.

    blender -b --factory-startup --python tools/modelle/raeume/raeume_trimsheet_elb.py -- --quellen=<ordner>

Ein Aufruf, nachdem raeume_trimsheet.py raeume_innen_trim_* geschrieben hat (Muster: raeume_trimsheet_zwerg.py). Nach den Kunstwerken
innen (wohnhaus_in_lvl2_elb.jpg, wohnhaus_in_lvl3_elb.jpg: cremeweisser Putz, grau-braunes Holz, heller Stein) werden nur umgetoent bzw.
ersetzt: „putz“ (Kalkputz in der Farbe der Elbenstube), „dielen“, „balken“ und „moebel“ (grau-braunes Holz wie innen_trim_elb) und
„bruchstein“ (heller Haustein large_sandstone_blocks_01 statt grauer Bruchstein, wie Herd und Schlot der Elben); Parkett, Teppich,
Buecher, Leder, Papier und Kleinteile bleiben Pixel fuer Pixel. Quellen Poly Haven CC0 (HERKUNFT.md). Keine Bild-KI.
"""
import os
import sys

import bpy

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "neubau"))
sys.path.insert(0, HIER)
from trimsheet import Quelle, faerbe                                      # noqa: E402
import trimsheet_stufen as st                                             # noqa: E402  (lade_blatt)
import trimsheet_elb as te                                                # noqa: E402  (Farben der Elbenstube, gespeichert, Quelle1k)
from raeume_trimsheet import BAENDER_RAEUME as B                          # noqa: E402


def baue(quellen):
    blatt = st.lade_blatt("raeume_innen_trim")

    def band(name, q, ton, ab=0, kontrast=1.0, eigenfarbe=0.2):
        a, e = blatt.zeilen(B[name])
        f, n, arm = q.streifen(e - a, ab=ab)
        blatt.setze(B[name], faerbe(f, te.gespeichert(ton), kontrast=kontrast, eigenfarbe=eigenfarbe), n, arm)

    band("putz", Quelle(quellen, "white_plaster_rough_01"), te.INNEN_PUTZ, ab=100, kontrast=0.7, eigenfarbe=0.1)
    blatt.verlauf(B["putz"], 0.92, 0.82, 40)
    band("dielen", Quelle(quellen, "old_planks_02", gedreht=True), te.INNEN_DIELEN, ab=60, kontrast=1.0, eigenfarbe=0.3)
    band("balken", Quelle(quellen, "rough_wood", gedreht=True), te.INNEN_BALKEN, ab=300, kontrast=1.0, eigenfarbe=0.15)
    band("moebel", Quelle(quellen, "wood_table_001", gedreht=True), tuple(c * 0.95 for c in te.INNEN_BALKEN), ab=200, kontrast=1.05, eigenfarbe=0.2)
    band("bruchstein", te.Quelle1k(quellen, "large_sandstone_blocks_01"), te.INNEN_STEIN, ab=300, eigenfarbe=0.5)
    blatt.speichere("raeume_innen_trim_elb")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict(a[2:].split("=", 1) for a in args if a.startswith("--") and "=" in a)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    baue(opt["quellen"])
