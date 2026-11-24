"""Trim-Sheets der Dome (Neubau, Gruppe "dom"): eigenes Blatt aussen und innen, gleiche Technik wie rathaus_trimsheet.py.

    blender -b --python tools/modelle/neubau/dom_trimsheet.py -- --quellen=<ordner> [--nur=aussen|innen]

<ordner> enthaelt je Poly-Haven-Name einen Unterordner mit `<name>_diff_2k.jpg`, `<name>_nor_gl_2k.jpg`, `<name>_arm_2k.jpg`
(CC0, api.polyhaven.com/files/<name>, nie im Repository; Namen und Urheber in game/assets/texturen/neubau/HERKUNFT.md).
Schreibt game/assets/texturen/neubau/dom_trim_{albedo,normal,orm}.png und dom_innen_trim_{...}.png (2048 x 2048).
Die Baender stehen in BAENDER_DOM / BAENDER_DOM_INNEN; die Bauskripte der Dome (dom_bib.py, dom_<glaube>_*.py) lesen sie von hier.
Das Dachband liegt wie bei allen Neubau-Blaettern in v 0-0,25 (der Shader building_trim erkennt daran das Dach fuer Schnee und Regen).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from trimsheet import N, Blatt, Quelle, faerbe, rauschen, srgb_zu_linear, linear_zu_srgb, lade  # noqa: E402

# (v0, v1, Meter je UV.x 0..1). Hoehe eines Bandes in Metern = (v1 - v0) * Meter.
# Reihenfolge nach Farbe: Im Pixel-Look liest der Shader grobe Mip-Stufen, dort mischen sich benachbarte Baender. Neben dem Schiefer
# und der Mauer liegen darum graue Baender, das braune Holz am Ende (Klicktest 23.11.2026: Holz neben dem Dach faerbte Dach und Mauer braun).
BAENDER_DOM = {
    "schiefer":       (0.0000, 0.1875, 4.0),
    "schiefer_kante": (0.1875, 0.2500, 4.0),   # Alpha-Cards an den Stufenkanten der Daecher
    "sockel":         (0.2500, 0.3000, 4.0),   # dunkler Bruchstein: Sockel (0,20 m)
    "eisen":          (0.3000, 0.3250, 2.0),
    "mauer":          (0.3250, 0.6250, 4.0),   # grauer Werkstein in Lagen: Wandflaechen (1,20 m)
    "werkstein":      (0.6250, 0.7000, 4.0),   # glatter heller Stein: Masswerk, Fialen, Gesimse (0,30 m)
    "sandstein":      (0.7000, 0.8500, 4.0),   # gelblicher Sandstein: Strebepfeiler, Ecken, Gewaende (0,60 m)
    "balken":         (0.8500, 0.9000, 4.0),   # Eiche: Glockenstuhl
    "planken":        (0.9000, 0.9500, 4.0),   # Tuerblaetter
    "kleinteile":     (0.9500, 1.0000, 1.0),   # Farbfelder, siehe FELDER_DOM
}
FELDER_DOM = ["gold", "bronze", "zifferblatt", "blei", "holz_dunkel", "nacht", "eisen", "kupfer"]

BAENDER_DOM_INNEN = {
    "platten":     (0.00, 0.14, 4.48),  # Schachbrett aus hellen und dunklen Steinplatten: 8 Platten von 0,56 m auf die Blattbreite
    "mauer":       (0.14, 0.44, 4.0),   # grauer Werkstein (1,20 m)
    "sandstein":   (0.44, 0.59, 4.0),   # gelblicher Sandstein: Boegen, Dienste, Stufen (0,60 m)
    "werkstein":   (0.59, 0.665, 4.0),  # glatter heller Stein: Saeulen, Rippen, Masswerk (0,30 m)
    "gewoelbe":    (0.665, 0.765, 8.0), # heller Putz der Gewoelbekappen (0,80 m)
    "holz":        (0.765, 0.84, 4.0),  # dunkle Eiche: Baenke, Chorgestuehl, Kanzel (0,30 m)
    "stoff":       (0.84, 0.88, 2.0),   # roter Stoff: Fahnen, Laeufer, Altartuch
    "leinen":      (0.88, 0.91, 2.0),   # weisses Leinen
    "gold":        (0.91, 0.95, 2.0),   # vergoldetes Schnitzwerk: Retabel
    "kleinteile":  (0.95, 1.00, 1.0),   # Farbfelder, siehe FELDER_DOM_INNEN
}
FELDER_DOM_INNEN = ["eisen", "messing", "bronze", "pergament", "wachs", "tinte", "glut", "gold", "blau", "haut", "rot", "gruen"]


def _felder(blatt, quellen, band, namen, stoffe):
    putz = Quelle(quellen, "white_plaster_rough_01", kante=512)
    rost = Quelle(quellen, "rusty_metal_02", kante=512)
    a, e = blatt.zeilen(band)
    feld = N // len(namen)
    for i, name in enumerate(namen):
        metallisch, ton, rau = stoffe[name]
        f, n, arm = (rost if metallisch else putz).streifen(e - a, ab=40)
        blatt.setze(band, faerbe(f, ton, kontrast=0.6, eigenfarbe=0.0 if name == "gold" else 0.15), n, arm, rauheit=rau, metall=1.0 if metallisch else 0.0, x0=i * feld, x1=(i + 1) * feld)


def haus_stein_band(quellen, hoehe, meter=4.0):
    """Mauerband mit dem Stein der Menschenhaeuser S2/S3 (Poly Haven castle_wall_varriation, 1K, reale Groesse 2 x 2 m laut
    trimsheet_ebenen.STEINE["k1"]): in seiner eigenen Farbe, waagerecht nahtlos (2^k Kacheln je Band), derselbe Massstab wie am Haus
    (Band von `meter` Metern Breite, hier 4,0 wie im Haus-Layout = 512 Bildpunkte je Meter), Ober- und Unterkante des Bandes auf moeglichst
    dunklen Lagerfugen. Dieselbe Rechnung wie trimsheet_ebenen.stein_band (dort ohne Blender, hier mit den Funktionen von trimsheet.py)."""
    n = "castle_wall_varriation"
    o = os.path.join(quellen, n)
    farbe = lade(os.path.join(o, f"{n}_diff_1k.jpg"))
    normale = lade(os.path.join(o, f"{n}_nor_gl_1k.jpg"), daten=True)
    arm = lade(os.path.join(o, f"{n}_arm_1k.jpg"), daten=True)
    kb, kh = 2.0, 2.0
    k = max(1, int(2 ** round(np.log2(meter / kb))))
    tw = N // k
    massstab = (meter / k) / kb
    th = max(2, int(round(kh * massstab * N / meter)))
    if farbe.shape[0] != th or farbe.shape[1] != tw:
        raise SystemExit(f"castle_wall_varriation: Kachel {farbe.shape[1]}x{farbe.shape[0]} statt {tw}x{th} (Massstab des Hauses nicht 1:1)")
    hell = (farbe @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)).mean(axis=1)
    hell = np.convolve(np.concatenate([hell[-2:], hell, hell[:2]]), np.ones(5) / 5, mode="valid")
    ab = int(np.argmin(hell + np.roll(hell, -(hoehe % th))))
    zeilen = (ab + np.arange(hoehe)) % th
    spalten = np.arange(N) % tw
    hole = lambda f: f[zeilen][:, spalten].copy()
    print(f"Dom-Mauer: {n}: {k} Kachel(n) je {meter:g} m, Massstab {massstab:.2f}, ab Zeile {ab} von {th}")
    return hole(farbe), hole(normale), hole(arm)


def baue_aussen(quellen):
    B = BAENDER_DOM
    blatt = Blatt()
    schiefer = Quelle(quellen, "roof_slates_02")
    a, e = blatt.zeilen(B["schiefer"])
    f, n, arm = schiefer.streifen(e - a, ab=60)
    blatt.setze(B["schiefer"], faerbe(f, (0.50, 0.53, 0.60), kontrast=1.15, eigenfarbe=0.35), n, arm)   # heller als beim Rathaus: im Pixel-Look war das Dach fast schwarz (Klicktest 23.11.2026)
    blatt.verlauf(B["schiefer"], 0.65, 1.0, 90)
    a2, e2 = blatt.zeilen(B["schiefer_kante"])
    h2 = e2 - a2
    f, n, arm = schiefer.streifen(h2, ab=400, versatz=200)
    blatt.setze(B["schiefer_kante"], faerbe(f, (0.46, 0.49, 0.56), kontrast=1.2, eigenfarbe=0.3), n, arm)
    grob = rauschen(N, 21, 40); stufe = (np.floor(np.arange(N) / 38) % 2).astype(np.float32)
    laenge = h2 * (0.55 + 0.20 * grob + 0.18 * stufe)
    yy = np.arange(h2, dtype=np.float32)[:, None]
    blatt.albedo[a2:e2, :, 3] = (yy < laenge[None, :]).astype(np.float32)

    def band(name, q, ton, ab=0, versatz=0, kontrast=1.0, eigenfarbe=0.3, **kw):
        a, e = blatt.zeilen(B[name])
        f, n, arm = q.streifen(e - a, ab=ab, versatz=versatz)
        blatt.setze(B[name], faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe), n, arm, **kw)

    band("balken", Quelle(quellen, "rough_wood", gedreht=True), (0.34, 0.25, 0.18), ab=120, kontrast=1.1, eigenfarbe=0.2)
    blatt.fase(B["balken"])
    band("planken", Quelle(quellen, "old_planks_02", kante=512, gedreht=True), (0.36, 0.26, 0.18), ab=8, kontrast=1.1)
    quader = Quelle(quellen, "sandstone_blocks_08")
    a, e = blatt.zeilen(B["mauer"])
    f, n, arm = haus_stein_band(quellen, e - a, BAENDER_DOM["mauer"][2])          # Mauer: Stein der Menschenhaeuser S3 (24.11.2026), nicht umgefaerbt
    blatt.setze(B["mauer"], f, n, arm)
    band("sandstein", quader, (0.80, 0.74, 0.62), ab=640, versatz=300, kontrast=1.5, eigenfarbe=0.15)
    blatt.verlauf(B["sandstein"], 0.9, 0.9, 12)
    band("werkstein", Quelle(quellen, "white_plaster_rough_01"), (0.82, 0.77, 0.66), ab=200, kontrast=1.3, eigenfarbe=0.1)
    blatt.verlauf(B["werkstein"], 0.85, 0.85, 14)
    band("sockel", Quelle(quellen, "castle_wall_slates"), (0.36, 0.36, 0.36), ab=300, kontrast=1.05, eigenfarbe=0.3)
    a, e = blatt.zeilen(B["eisen"])
    f, n, arm = Quelle(quellen, "rusty_metal_02").streifen(e - a, ab=0)
    blatt.setze(B["eisen"], faerbe(f, (0.16, 0.15, 0.15), kontrast=0.5, eigenfarbe=0.25), n, arm, rauheit=0.62, metall=1.0)
    _felder(blatt, quellen, B["kleinteile"], FELDER_DOM, {
        "gold": (True, (0.86, 0.74, 0.40), 0.3), "bronze": (True, (0.40, 0.32, 0.20), 0.45), "zifferblatt": (False, (0.95, 0.93, 0.86), 0.8),
        "blei": (True, (0.34, 0.36, 0.40), 0.5), "holz_dunkel": (False, (0.16, 0.12, 0.09), 0.9), "nacht": (False, (0.03, 0.03, 0.04), 0.9),
        "eisen": (True, (0.13, 0.13, 0.13), 0.55), "kupfer": (True, (0.30, 0.52, 0.44), 0.6),
    })
    blatt.speichere("dom_trim")


def baue_innen(quellen):
    B = BAENDER_DOM_INNEN
    blatt = Blatt()

    def band(name, q, ton, ab=0, versatz=0, kontrast=1.0, eigenfarbe=0.3, rau_min=None, **kw):
        a, e = blatt.zeilen(B[name])
        f, n, arm = q.streifen(e - a, ab=ab, versatz=versatz)
        if rau_min is not None:
            kw["rauheit"] = rau_min + (1 - rau_min) * arm[..., 1]
        blatt.setze(B[name], faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe), n, arm, **kw)

    # Schachbrett: Platten von 0,56 m (das Band ist eine Platte hoch), abwechselnd hell und dunkel; eine Fuge am Rand jeder Platte.
    # Die Bauskripte versetzen jede zweite Reihe um eine Platte (dom_bib.schachboden).
    a, e = blatt.zeilen(B["platten"])
    h = e - a
    f, n, arm = Quelle(quellen, "large_floor_tiles_02").streifen(h, ab=0)
    hell = faerbe(f, (0.66, 0.63, 0.58), kontrast=0.9, eigenfarbe=0.2)
    dunkel = faerbe(f, (0.42, 0.42, 0.44), kontrast=0.9, eigenfarbe=0.2)
    breite = N / 8.0
    feld_ = (np.floor(np.arange(N) / breite).astype(int) % 2 == 0)
    farbe = np.where(feld_[None, :, None], hell, dunkel)
    x = np.arange(N) % breite; y = np.arange(h)
    fuge = (np.minimum(x, breite - x) < 3)[None, :] | (np.minimum(y, h - 1 - y) < 3)[:, None]
    farbe = np.where(fuge[..., None], farbe * 0.45, farbe)
    blatt.setze(B["platten"], farbe, n, arm, rauheit=0.75 + 0.2 * arm[..., 1])
    blatt.orm[a:e, :, 0] = np.where(fuge, 0.6, blatt.orm[a:e, :, 0])

    quader = Quelle(quellen, "sandstone_blocks_08")
    band("mauer", quader, (0.70, 0.69, 0.67), ab=0, kontrast=1.6, eigenfarbe=0.2, rau_min=0.70)
    band("sandstein", quader, (0.80, 0.74, 0.62), ab=640, versatz=300, kontrast=1.5, eigenfarbe=0.15, rau_min=0.70)
    band("werkstein", Quelle(quellen, "white_plaster_rough_01"), (0.82, 0.77, 0.66), ab=200, kontrast=1.3, eigenfarbe=0.1, rau_min=0.75)
    blatt.verlauf(B["werkstein"], 0.85, 0.85, 14)
    band("gewoelbe", Quelle(quellen, "white_plaster_rough_01"), (0.78, 0.75, 0.68), ab=100, kontrast=0.8, eigenfarbe=0.1, rau_min=0.8)
    band("holz", Quelle(quellen, "dark_wooden_planks", gedreht=True), (0.34, 0.23, 0.15), ab=40, kontrast=1.1)
    band("stoff", Quelle(quellen, "velour_velvet", kante=512), (0.55, 0.10, 0.09), ab=0, kontrast=1.0, eigenfarbe=0.2)
    band("leinen", Quelle(quellen, "velour_velvet", kante=512), (0.88, 0.86, 0.80), ab=120, kontrast=0.6, eigenfarbe=0.05)
    # Gold mit Zeichnung: senkrechte Felder wie Schnitzwerk (dunkle Kehlen alle 0,25 m)
    a, e = blatt.zeilen(B["gold"])
    f, n, arm = Quelle(quellen, "rusty_metal_02", kante=512).streifen(e - a, ab=0)
    gold = faerbe(f, (0.86, 0.74, 0.40), kontrast=0.7, eigenfarbe=0.0)       # helles Blattgold (Probe: war orange)
    kehle = (np.arange(N) % 64 < 5)[None, :, None]
    blatt.setze(B["gold"], np.where(kehle, gold * 0.45, gold), n, arm, rauheit=0.35, metall=1.0)
    _felder(blatt, quellen, B["kleinteile"], FELDER_DOM_INNEN, {
        "eisen": (True, (0.13, 0.13, 0.13), 0.55), "messing": (True, (0.62, 0.48, 0.24), 0.35), "bronze": (True, (0.42, 0.33, 0.20), 0.45),
        "pergament": (False, (0.80, 0.75, 0.64), 0.8), "wachs": (False, (0.92, 0.88, 0.76), 0.6), "tinte": (False, (0.05, 0.05, 0.06), 0.4),
        "glut": (False, (0.95, 0.42, 0.10), 0.9), "gold": (True, (0.86, 0.74, 0.40), 0.3), "blau": (False, (0.16, 0.26, 0.55), 0.8),
        "haut": (False, (0.80, 0.62, 0.50), 0.8),
        "rot": (False, (0.62, 0.13, 0.11), 0.8), "gruen": (False, (0.20, 0.42, 0.26), 0.8),
    })
    blatt.speichere("dom_innen_trim")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict(a[2:].split("=", 1) for a in args if a.startswith("--") and "=" in a)
    if opt.get("nur", "aussen") == "aussen":
        baue_aussen(opt["quellen"])
    if opt.get("nur", "innen") == "innen":
        baue_innen(opt["quellen"])
