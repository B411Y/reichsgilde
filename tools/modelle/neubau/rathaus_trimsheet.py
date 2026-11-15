"""Trim-Sheets des Rathauses (Neubau, Gruppe "rathaus"): eigenes Blatt aussen und innen, gleiche Technik wie trimsheet.py.

    blender -b --python tools/modelle/neubau/rathaus_trimsheet.py -- --quellen=<ordner> [--nur=aussen|innen]

<ordner> enthaelt je Poly-Haven-Name einen Unterordner mit `<name>_diff_2k.jpg`, `<name>_nor_gl_2k.jpg`, `<name>_arm_2k.jpg`
(CC0, api.polyhaven.com/files/<name>, nie im Repository; Namen und Urheber in game/assets/texturen/neubau/HERKUNFT.md).
Schreibt game/assets/texturen/neubau/rathaus_trim_{albedo,normal,orm}.png und rathaus_innen_trim_{...}.png (2048 x 2048).
Die Baender stehen in BAENDER_RATHAUS / BAENDER_RATHAUS_INNEN; die Bauskripte des Rathauses lesen sie von hier.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from trimsheet import N, Blatt, Quelle, faerbe, rauschen, srgb_zu_linear, linear_zu_srgb  # noqa: E402

# Aussen wie Style-Guide 4.1 (Dach 0-0,25; Eichenholz 0,25-0,5; Putz 0,5-0,7; Bretter 0,7-0,85; Stein und Eisen 0,85-1),
# Putz geteilt in Putz und Sandsteinquader. (v0, v1, Meter je UV.x 0..1)
BAENDER_RATHAUS = {
    "schiefer":       (0.0000, 0.1875, 4.0),
    "schiefer_kante": (0.1875, 0.2500, 4.0),   # Alpha-Cards an Traufe und Ortgang: Schieferkante mit unregelmaessigen Spitzen
    "balken_a":       (0.2500, 0.3333, 4.0),
    "balken_b":       (0.3333, 0.4167, 4.0),
    "balken_c":       (0.4167, 0.5000, 4.0),
    "putz":           (0.5000, 0.6000, 8.0),
    "quader":         (0.6000, 0.7000, 4.0),   # Sandsteinquader: Ecken, Boegen, Gesimse, Fensterrahmen
    "planken":        (0.7000, 0.7750, 4.0),
    "farbholz":       (0.7750, 0.8500, 4.0),   # hell, nimmt die Akzentfarbe an (Laeden in Stadtrot)
    "bruchstein":     (0.8500, 0.9500, 4.0),
    "eisen":          (0.9500, 1.0000, 2.0),
}
BAENDER_RATHAUS_INNEN = {
    "dielen":      (0.00, 0.14, 4.0),   # Bretter laufen entlang UV.x
    "taefelung":   (0.14, 0.24, 4.0),   # dunkles Eichenholz: Taefelung, Moebel der Ratshalle und des Gerichts
    "balken":      (0.24, 0.34, 4.0),
    "putz":        (0.34, 0.48, 8.0),
    "quader":      (0.48, 0.58, 4.0),   # Sandstein: Boegen, Kamin, Saeulen
    "bruchstein":  (0.58, 0.68, 4.0),   # graue Mauer der Eingangshalle und des Turms
    "platten":     (0.68, 0.76, 4.0),   # Steinplatten (Halle, Gericht)
    "kerker":      (0.76, 0.83, 4.0),   # dunkle Kerkermauer
    "pflaster":    (0.83, 0.88, 2.0),   # Kopfsteinpflaster des Kerkers
    "stoff":       (0.88, 0.92, 2.0),   # roter Stoff: Behang, Laeufer, Kissen
    "stroh":       (0.92, 0.96, 2.0),
    "kleinteile":  (0.96, 1.00, 1.0),   # Farbfelder, siehe FELDER_RATHAUS_INNEN
}
FELDER_RATHAUS_INNEN = ["eisen", "messing", "bronze", "pergament", "wachs", "tinte", "glut", "gold"]


def baue_aussen(quellen):
    B = BAENDER_RATHAUS
    blatt = Blatt()
    # Schiefer: Deckung mit Schatten unter der naechsten Stufe
    schiefer = Quelle(quellen, "roof_slates_02")
    a, e = blatt.zeilen(B["schiefer"])
    f, n, arm = schiefer.streifen(e - a, ab=60)
    blatt.setze(B["schiefer"], faerbe(f, (0.40, 0.42, 0.46), kontrast=1.15, eigenfarbe=0.35), n, arm)
    blatt.verlauf(B["schiefer"], 0.65, 1.0, 90)
    # Kante: dieselben Platten, unten unregelmaessig ausgezahnt (Alpha)
    a2, e2 = blatt.zeilen(B["schiefer_kante"])
    h2 = e2 - a2
    f, n, arm = schiefer.streifen(h2, ab=400, versatz=200)
    blatt.setze(B["schiefer_kante"], faerbe(f, (0.36, 0.38, 0.42), kontrast=1.2, eigenfarbe=0.3), n, arm)
    grob = rauschen(N, 21, 40); stufe = (np.floor(np.arange(N) / 38) % 2).astype(np.float32)
    laenge = h2 * (0.55 + 0.20 * grob + 0.18 * stufe)
    yy = np.arange(h2, dtype=np.float32)[:, None]
    blatt.albedo[a2:e2, :, 3] = (yy < laenge[None, :]).astype(np.float32)
    holz = Quelle(quellen, "rough_wood", gedreht=True)
    for i, (name, ton, kontrast) in enumerate((("balken_a", (0.40, 0.29, 0.20), 1.0), ("balken_b", (0.37, 0.27, 0.19), 1.1), ("balken_c", (0.30, 0.22, 0.16), 1.2))):
        a, e = blatt.zeilen(B[name])
        f, n, arm = holz.streifen(e - a, ab=120 + 290 * i, versatz=370 * i)
        blatt.setze(B[name], faerbe(f, ton, kontrast=kontrast, eigenfarbe=0.2), n, arm)
        blatt.verlauf(B[name], 0.6, 0.6, 9)
        blatt.fase(B[name])
    a, e = blatt.zeilen(B["putz"])
    f, n, arm = Quelle(quellen, "white_plaster_rough_01").streifen(e - a, ab=200)
    blatt.setze(B["putz"], faerbe(f, (0.88, 0.85, 0.78), kontrast=0.75, eigenfarbe=0.1), n, arm)
    blatt.verlauf(B["putz"], 0.93, 0.82, 50)
    a, e = blatt.zeilen(B["quader"])
    f, n, arm = Quelle(quellen, "sandstone_blocks_08").streifen(e - a, ab=0)
    blatt.setze(B["quader"], faerbe(f, (0.68, 0.62, 0.52), kontrast=1.3, eigenfarbe=0.5), n, arm)
    planken = Quelle(quellen, "old_planks_02", kante=512, gedreht=True)
    a, e = blatt.zeilen(B["planken"])
    f, n, arm = planken.streifen(e - a, ab=8)
    blatt.setze(B["planken"], faerbe(f, (0.40, 0.31, 0.23), kontrast=1.1, eigenfarbe=0.3), n, arm)
    a, e = blatt.zeilen(B["farbholz"])
    f, n, arm = planken.streifen(e - a, ab=154, versatz=190)
    blatt.setze(B["farbholz"], faerbe(f, (0.82, 0.80, 0.75), kontrast=0.55, eigenfarbe=0.05), n, arm)
    a, e = blatt.zeilen(B["bruchstein"])
    f, n, arm = Quelle(quellen, "castle_wall_slates").streifen(e - a, ab=300)
    blatt.setze(B["bruchstein"], faerbe(f, (0.50, 0.50, 0.49), kontrast=1.05, eigenfarbe=0.35), n, arm)
    a, e = blatt.zeilen(B["eisen"])
    f, n, arm = Quelle(quellen, "rusty_metal_02").streifen(e - a, ab=0)
    blatt.setze(B["eisen"], faerbe(f, (0.16, 0.15, 0.15), kontrast=0.5, eigenfarbe=0.25), n, arm, rauheit=0.62, metall=1.0)
    blatt.speichere("rathaus_trim")


def baue_innen(quellen):
    B = BAENDER_RATHAUS_INNEN
    blatt = Blatt()

    def band(name, q, ton, ab=0, versatz=0, kontrast=1.0, eigenfarbe=0.3, rau_min=None, **kw):
        a, e = blatt.zeilen(B[name])
        f, n, arm = q.streifen(e - a, ab=ab, versatz=versatz)
        if rau_min is not None:                     # Mindestrauheit: Rauheit min..1 statt 0..1, das Muster bleibt
            kw["rauheit"] = rau_min + (1 - rau_min) * arm[..., 1]
        blatt.setze(B[name], faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe), n, arm, **kw)

    band("dielen", Quelle(quellen, "old_planks_02", gedreht=True), (0.55, 0.42, 0.30), ab=60)
    band("taefelung", Quelle(quellen, "dark_wooden_planks", gedreht=True), (0.36, 0.25, 0.16), ab=40, kontrast=1.1)
    band("balken", Quelle(quellen, "rough_wood", gedreht=True), (0.42, 0.31, 0.22), ab=300, kontrast=1.1, eigenfarbe=0.2)
    blatt.fase(B["balken"])
    band("putz", Quelle(quellen, "white_plaster_rough_01"), (0.86, 0.83, 0.76), ab=100, kontrast=0.8, eigenfarbe=0.1)
    blatt.verlauf(B["putz"], 0.9, 0.82, 50)
    # Stein matt (Klicktest 2: die Bruchsteinwand der Turmtreppe glaenzte im Fackellicht; Rauheit der Quelle im Mittel 0,50, stellenweise 0,23)
    band("quader", Quelle(quellen, "sandstone_blocks_08"), (0.68, 0.62, 0.52), ab=0, kontrast=1.3, eigenfarbe=0.5, rau_min=0.70)
    band("bruchstein", Quelle(quellen, "castle_wall_slates"), (0.48, 0.47, 0.46), ab=300, eigenfarbe=0.35, rau_min=0.70)
    band("platten", Quelle(quellen, "large_floor_tiles_02"), (0.52, 0.50, 0.47), ab=0, eigenfarbe=0.3)
    band("kerker", Quelle(quellen, "rough_block_wall"), (0.30, 0.29, 0.27), ab=100, kontrast=1.15, eigenfarbe=0.35)
    band("pflaster", Quelle(quellen, "cobblestone_floor_01", kante=512), (0.33, 0.31, 0.29), ab=0, kontrast=1.1)
    band("stoff", Quelle(quellen, "velour_velvet", kante=512), (0.52, 0.10, 0.09), ab=0, kontrast=1.0, eigenfarbe=0.2)
    band("stroh", Quelle(quellen, "reed_roof_03", kante=1024), (0.64, 0.57, 0.40), ab=560, kontrast=1.1)
    putz = Quelle(quellen, "white_plaster_rough_01", kante=512)
    rost = Quelle(quellen, "rusty_metal_02", kante=512)
    a, e = blatt.zeilen(B["kleinteile"])
    feld = N // len(FELDER_RATHAUS_INNEN)
    stoffe = {
        "eisen": (rost, (0.13, 0.13, 0.13), 0.55, 1.0), "messing": (rost, (0.62, 0.48, 0.24), 0.35, 1.0),
        "bronze": (rost, (0.42, 0.33, 0.20), 0.45, 1.0), "pergament": (putz, (0.86, 0.79, 0.62), 0.8, 0.0),
        "wachs": (putz, (0.92, 0.88, 0.76), 0.6, 0.0), "tinte": (putz, (0.05, 0.05, 0.06), 0.4, 0.0),
        "glut": (putz, (0.95, 0.42, 0.10), 0.9, 0.0), "gold": (rost, (0.80, 0.62, 0.22), 0.3, 1.0),
    }
    for i, name in enumerate(FELDER_RATHAUS_INNEN):
        q, ton, rau, metall = stoffe[name]
        f, n, arm = q.streifen(e - a, ab=40)
        # Pergament mit sichtbarer Struktur und gealtert (Klicktest 2: Rollen und Staendetafel wirkten einfarbig gelb)
        kontrast, ton = (1.4, (0.80, 0.75, 0.64)) if name == "pergament" else (0.6, ton)
        blatt.setze(B["kleinteile"], faerbe(f, ton, kontrast=kontrast, eigenfarbe=0.15), n, arm, rauheit=rau, metall=metall, x0=i * feld, x1=(i + 1) * feld)
    blatt.speichere("rathaus_innen_trim")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict(a[2:].split("=", 1) for a in args if a.startswith("--") and "=" in a)
    if opt.get("nur", "aussen") == "aussen":
        baue_aussen(opt["quellen"])
    if opt.get("nur", "innen") == "innen":
        baue_innen(opt["quellen"])
