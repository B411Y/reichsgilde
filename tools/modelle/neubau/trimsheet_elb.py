"""Trim-Sheets fuer die Wohnhaeuser der Elben, Stufe 1 (Neubau): dieselben Baender wie bei den Menschen (trimsheet.py), anderer Inhalt.

    blender -b --python tools/modelle/neubau/trimsheet_elb.py -- --quellen=<ordner> [--nur=aussen|innen]

Vorbild sind die Kunstwerke â€žWohnhaus Elb, Stufe 1â€œ aussen und innen (wohnhaus_lvl1_elb.jpg, wohnhaus_in_lvl1_elb.jpg): Fachwerk aus
hellem, grau verwittertem Holz mit Kalkputz in blassem Gelbgruen, Dach aus grau-olivgruenen Holzschindeln, Schlot aus hellem Haustein;
innen cremeweisser Putz, Dielenboden, Herd aus hellem Stein. Die Farben sind Proben aus den Kunstwerken (sRGB/255, im Kopf der
Konstanten); die Struktur kommt aus denselben CC0-Texturen von Poly Haven wie bei Menschen und Zwergen (Herkunft in
game/assets/texturen/neubau/HERKUNFT.md); die Schindeln sind die gezeichneten der Zwerge (trimsheet_zwerg.schindel_band), auf die Farbe
des Kunstwerks umgetoent. Keine Bild-KI.
Ergebnis: trim_elb_{albedo,normal,orm}.png und innen_trim_elb_{albedo,normal,orm}.png (2048 x 2048). Im Spiel liegt trim_elb als Ebene
â€želb_s1â€œ im Haeuser-Array (trimsheet_ebenen.py, Bruchsteinband dort K2), innen_trim_elb im Innen-Array.
"""
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import trimsheet_zwerg as tz                                       # noqa: E402
from trimsheet import (BAENDER_AUSSEN, BAENDER_INNEN, FELDER_INNEN, N, Blatt, Quelle, faerbe,  # noqa: E402
                       srgb_zu_linear, linear_zu_srgb)

# Farben aus dem Kunstwerk aussen (Proben 12 x 12 Bildpunkte, sRGB/255): Dach 107/104/88 bis 123/117/99, Putz 199/192/149 bis
# 205/199/157 (Licht), Balken 158/142/119, Ortbrett 162/155/138. Die Werte unten sind die Farben, die in der GESPEICHERTEN PNG stehen
# sollen (sRGB, etwas unter den Proben, weil die Kunstwerke hell ausgeleuchtet sind). Blatt.speichere legt die Farbe von `faerbe`
# linear ab (bei den Menschen wird aus dem Ziel 0,40 im Bild 0,13): `gespeichert` rechnet das Ziel so um, dass im Bild die Probe steht.
DACH = (0.42, 0.41, 0.34)
PUTZ = (0.74, 0.72, 0.57)
BALKEN = ((0.55, 0.50, 0.42), (0.52, 0.47, 0.40), (0.45, 0.41, 0.35))     # balken_a, _b, _c (Schwellen, Sparrenkoepfe dunkler)
PLANKEN = (0.50, 0.46, 0.40)
STEIN = (0.60, 0.57, 0.50)
# Innen (Kunstwerk innen): Putz cremeweiss, Dielen und Balken grau-braun, Herd aus hellem Stein
INNEN_PUTZ = (0.80, 0.77, 0.68)
INNEN_DIELEN = (0.50, 0.43, 0.35)
INNEN_BALKEN = (0.47, 0.42, 0.35)
INNEN_STEIN = (0.60, 0.58, 0.53)


def gespeichert(farbe):
    """Ziel fuer `faerbe`, damit die gespeicherte PNG im Mittel `farbe` (sRGB) zeigt (siehe oben)."""
    return tuple(float(c) for c in linear_zu_srgb(np.array(farbe, dtype=np.float32)))


class Quelle1k(Quelle):
    """Wie trimsheet.Quelle, aber aus den 1k-Dateien (K2 `large_sandstone_blocks_01` liegt nur in 1k vor, wie im Abruf der Steinebenen)."""

    def __init__(self, ordner, name, kante=1024):
        import trimsheet as ts
        o = os.path.join(ordner, name)
        self.farbe = ts.skaliere(ts.lade(os.path.join(o, f"{name}_diff_1k.jpg")), kante, kante)
        self.normale = ts.skaliere(ts.lade(os.path.join(o, f"{name}_nor_gl_1k.jpg"), daten=True), kante, kante)
        self.arm = ts.skaliere(ts.lade(os.path.join(o, f"{name}_arm_1k.jpg"), daten=True), kante, kante)
        self.kante = kante


def entsaettigen(blatt, band, anteil):
    """Nimmt die bunten Unterschiede der Holzschindeln zurueck (Anteil zur Helligkeit hin): im Kunstwerk ist das Dach gleichmaessig
    grau-olivgruen, die Schindeln unterscheiden sich nur in der Helligkeit."""
    a, e = blatt.zeilen(band)
    lin = srgb_zu_linear(blatt.albedo[a:e, :, :3])
    hell = lin @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    blatt.albedo[a:e, :, :3] = linear_zu_srgb(lin * (1 - anteil) + hell[..., None] * anteil)


def umtoenen(blatt, band, ziel_srgb):
    """Toent ein Band auf die mittlere Farbe `ziel_srgb` (Zeichnung, Helligkeitsverlauf und Alpha bleiben; gemittelt wird nur ueber
    deckende Bildpunkte)."""
    a, e = blatt.zeilen(band)
    lin = srgb_zu_linear(blatt.albedo[a:e, :, :3])
    deckt = blatt.albedo[a:e, :, 3] > 0.5
    mittel = lin[deckt].mean(axis=0)
    ziel = srgb_zu_linear(np.array(ziel_srgb, dtype=np.float32))
    blatt.albedo[a:e, :, :3] = linear_zu_srgb(np.clip(lin * (ziel / np.maximum(mittel, 1e-4)), 0, 1))


def baue_aussen(quellen):
    B = BAENDER_AUSSEN
    blatt = Blatt()
    # Dach: die Holzschindeln der Zwerge (drei Reihen je Stufe, Fuesse als Alpha-Cards), grau-olivgruen wie im Kunstwerk
    tz.schindel_band(blatt, Quelle(quellen, "old_planks_02", kante=512), B["stroh"], B["stroh_franse"])
    for band in ("stroh", "stroh_franse"): entsaettigen(blatt, B[band], 0.8)
    umtoenen(blatt, B["stroh"], gespeichert(DACH))
    umtoenen(blatt, B["stroh_franse"], gespeichert(tuple(c * 0.92 for c in DACH)))
    holz = Quelle(quellen, "rough_wood", gedreht=True)
    for i, (name, ton, kontrast) in enumerate(zip(("balken_a", "balken_b", "balken_c"), BALKEN, (0.9, 1.0, 1.1))):
        a, e = blatt.zeilen(B[name])
        f, n, arm = holz.streifen(e - a, ab=120 + 290 * i, versatz=370 * i)
        blatt.setze(B[name], faerbe(f, gespeichert(ton), kontrast=kontrast, eigenfarbe=0.1), n, arm)
        blatt.verlauf(B[name], 0.7, 0.7, 9)
        blatt.fase(B[name])
    putz = Quelle(quellen, "white_plaster_rough_01")
    a, e = blatt.zeilen(B["putz"])
    f, n, arm = putz.streifen(e - a, ab=200)
    blatt.setze(B["putz"], faerbe(f, gespeichert(PUTZ), kontrast=0.75, eigenfarbe=0.1), n, arm)
    blatt.verlauf(B["putz"], 0.93, 0.80, 60)              # unten Spritzwasser, oben leichter Schatten (wie bei den Menschen)
    planken = Quelle(quellen, "old_planks_02", kante=512, gedreht=True)
    a, e = blatt.zeilen(B["planken"])
    f, n, arm = planken.streifen(e - a, ab=8)
    blatt.setze(B["planken"], faerbe(f, gespeichert(PLANKEN), kontrast=1.0, eigenfarbe=0.2), n, arm)
    a, e = blatt.zeilen(B["farbholz"])
    f, n, arm = planken.streifen(e - a, ab=154, versatz=190)
    blatt.setze(B["farbholz"], faerbe(f, (0.82, 0.80, 0.75), kontrast=0.55, eigenfarbe=0.05), n, arm)   # die Akzentfarbe wird aufmultipliziert
    # Bruchstein: heller Haustein (im Spiel ersetzt die Ebene â€želb_s1â€œ das Band durch K2, dieselbe Textur in eigener Farbe)
    stein = Quelle1k(quellen, "large_sandstone_blocks_01")
    a, e = blatt.zeilen(B["bruchstein"])
    f, n, arm = stein.streifen(e - a, ab=300)
    blatt.setze(B["bruchstein"], faerbe(f, gespeichert(STEIN), kontrast=1.0, eigenfarbe=0.6), n, arm)
    eisen = Quelle(quellen, "rusty_metal_02")
    a, e = blatt.zeilen(B["eisen"])
    f, n, arm = eisen.streifen(e - a, ab=0)
    blatt.setze(B["eisen"], faerbe(f, (0.16, 0.15, 0.15), kontrast=0.5, eigenfarbe=0.25), n, arm, rauheit=0.62, metall=1.0)
    blatt.speichere("trim_elb")


def baue_innen(quellen):
    B = BAENDER_INNEN
    blatt = Blatt()

    def band(name, q, ton, ab=0, versatz=0, kontrast=1.0, eigenfarbe=0.3, **kw):
        a, e = blatt.zeilen(B[name])
        f, n, arm = q.streifen(e - a, ab=ab, versatz=versatz)
        blatt.setze(B[name], faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe), n, arm, **kw)

    dielen = Quelle(quellen, "old_planks_02", gedreht=True)
    band("bohlen", dielen, gespeichert(INNEN_DIELEN), ab=60)
    band("balken", Quelle(quellen, "rough_wood", gedreht=True), gespeichert(INNEN_BALKEN), ab=300, kontrast=1.0, eigenfarbe=0.15)
    band("lehmwand", Quelle(quellen, "white_plaster_rough_01"), gespeichert(INNEN_PUTZ), ab=200, kontrast=0.7, eigenfarbe=0.1)   # Kalkputz statt Lehm
    blatt.verlauf(B["lehmwand"], 0.92, 0.82, 50)
    band("lehmboden", dielen, gespeichert(tuple(c * 0.95 for c in INNEN_DIELEN)), ab=700, versatz=400)        # Dielenboden statt Lehm (Kunstwerk)
    band("bruchstein", Quelle1k(quellen, "large_sandstone_blocks_01"), gespeichert(INNEN_STEIN), ab=300, eigenfarbe=0.5)
    band("sackleinen", Quelle(quellen, "hessian_230", kante=512), (0.55, 0.50, 0.40), kontrast=1.0)
    band("wolle", Quelle(quellen, "knitted_fleece", kante=512), (0.46, 0.50, 0.40), kontrast=1.6)   # Wolldecke graugruen wie im Kunstwerk
    band("stroh", Quelle(quellen, "reed_roof_03", kante=1024), (0.64, 0.57, 0.40), ab=560, kontrast=1.1)
    ton_q = Quelle(quellen, "clay_plaster", kante=512)
    rinde = Quelle(quellen, "bark_brown_02", kante=512, gedreht=True)
    rost = Quelle(quellen, "rusty_metal_02", kante=512)
    a, e = blatt.zeilen(B["kleinteile"])
    feld = N // len(FELDER_INNEN)
    stoffe = {
        "ton": (ton_q, (0.60, 0.33, 0.20), 0.45, 0.0), "eisen": (rost, (0.13, 0.13, 0.13), 0.55, 1.0),
        "russ": (ton_q, (0.05, 0.045, 0.04), 0.95, 0.0), "rinde": (rinde, (0.33, 0.26, 0.19), None, 0.0),
        "zwiebel_rot": (ton_q, (0.50, 0.17, 0.20), 0.4, 0.0), "zwiebel_gelb": (ton_q, (0.74, 0.50, 0.20), 0.4, 0.0),
        "kraut": (rinde, (0.30, 0.38, 0.18), 0.85, 0.0), "glut": (ton_q, (0.95, 0.42, 0.10), 0.9, 0.0),
    }
    for i, name in enumerate(FELDER_INNEN):
        q, ton, rau, metall = stoffe[name]
        f, n, arm = q.streifen(e - a, ab=40)
        blatt.setze(B["kleinteile"], faerbe(f, ton, kontrast=0.8, eigenfarbe=0.2), n, arm, rauheit=rau, metall=metall, x0=i * feld, x1=(i + 1) * feld)
    blatt.speichere("innen_trim_elb")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict(a[2:].split("=", 1) for a in args if a.startswith("--") and "=" in a)
    nur = opt.get("nur", "")
    if nur in ("", "aussen"): baue_aussen(opt["quellen"])
    if nur in ("", "innen"): baue_innen(opt["quellen"])
