"""Trim-Sheets fuer die Wohnhaeuser der Zwerge (Neubau): dieselben Baender wie bei den Menschen (trimsheet.py), anderer Inhalt.

    blender -b --python tools/modelle/neubau/trimsheet_zwerg.py -- --quellen=<ordner> [--nur=aussen|innen]

Warum dieselben Baender: Die Bauskripte der Menschenhaeuser lesen die Bandgrenzen aus trimsheet.py; die Zwergenhaeuser entstehen aus
denselben Bauteilen (Waende, Fachwerk, Dach, Moebel), nur mit anderem Stoff darauf. Aus „Putz“ wird behauener Stein, aus „Stroh“
werden Holzschindeln, aus dem Lehmputz innen Stein, aus dem Lehmboden Pflaster. Das Vorbild ist das Kunstwerk „Wohnhaus Zwerg“
(Steinerdgeschoss, Fachwerk mit Steinfuellung, Schindeldach, grosser Steinkamin).

Quellen: Poly Haven (CC0), je `<name>/<name>_diff_2k.jpg`, `_nor_gl_2k.jpg`, `_arm_2k.jpg` (Herkunft in
game/assets/texturen/neubau/HERKUNFT.md). Die Schindeln sind selbst gezeichnet (Zufallsbreiten, Holz aus `old_planks_02`);
Keine Bild-KI.
Ergebnis: trim_zwerg_{albedo,normal,orm}.png und innen_trim_zwerg_{albedo,normal,orm}.png (2048 x 2048).
"""
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import trimsheet as ts                                             # noqa: E402
from trimsheet import (BAENDER_AUSSEN, BAENDER_INNEN, FELDER_INNEN, N, Blatt, Quelle, faerbe, rauschen,  # noqa: E402
                       srgb_zu_linear, linear_zu_srgb)


def schindel_breiten(saat, mittel=150):
    """Breiten der Schindeln einer Reihe (Summe genau N, damit das Band waagerecht nahtlos kachelt)."""
    z = np.random.default_rng(saat)
    b = []
    while sum(b) < N - 2 * mittel:
        b.append(int(z.integers(int(mittel * 0.8), int(mittel * 1.25))))
    rest = N - sum(b)
    b.append(rest)                                                 # letzte Schindel fuellt den Rest (breiter, aber nahtlos)
    return b


def schindel_band(blatt, holz, band_flaeche, band_franse):
    """Dachflaeche: drei Reihen Holzschindeln, jede Reihe ueberdeckt die obere; dunkle Fugen, Fuss dicker (Normale).
    Fransenstreifen: die Schindelfuesse einer Reihe mit Alpha (ungleich lang, zwischen den Schindeln Luft)."""
    a, e = blatt.zeilen(band_flaeche)
    h = e - a
    kurse = 3
    kh = h // kurse
    farbe = np.zeros((h, N, 3), dtype=np.float32)
    dicke = np.zeros((h, N), dtype=np.float32)
    ao = np.ones((h, N), dtype=np.float32)
    zufall = np.random.default_rng(21)
    yy = np.arange(h)[:, None]
    for c in range(kurse):
        y0, y1 = c * kh, (c + 1) * kh
        sicht0 = max(0, y0 - int(kh * 0.55))                          # die Schindel reicht unter die Reihe darueber
        x = int(zufall.integers(0, 80)) if c % 2 else 0
        for b in schindel_breiten(100 + c):
            zeilen = np.arange(sicht0, y1)
            spalten = (x + np.arange(b)) % N
            ton = np.array([0.62, 0.54, 0.45]) * float(zufall.uniform(0.84, 1.12)) * np.array([1.0, 1.0, float(zufall.uniform(0.9, 1.1))])
            r0, c0 = int(zufall.integers(0, holz.kante)), int(zufall.integers(0, holz.kante))
            teil = holz.farbe[(r0 + np.arange(len(zeilen))) % holz.kante][:, (c0 + np.arange(b)) % holz.kante]
            teil = faerbe(teil, tuple(np.clip(ton, 0, 1)), kontrast=1.2, eigenfarbe=0.25)
            # Form der Schindel: Fugen links und rechts, unten dicker, oben duenn; ganz unten ein heller Schnittrand
            rel = (zeilen - y0) / max(kh, 1)
            hoehe = (0.35 + 0.65 * np.clip((rel + 0.55) / 1.55, 0, 1))[:, None] * np.ones((1, b))
            sx = np.arange(b)[None, :]
            rand = np.minimum(sx, b - 1 - sx)
            fuge = np.clip(rand / 5.0, 0, 1)
            fuss = np.clip((y1 - 1 - zeilen)[:, None] / 4.0, 0, 1)
            hoehe = hoehe * fuge * (0.55 + 0.45 * fuss)
            lin = srgb_zu_linear(teil) * (0.35 + 0.65 * np.minimum(fuge, 1.0))[..., None] * (0.70 + 0.30 * fuss)[..., None]
            lin = lin * (0.78 + 0.22 * np.clip((rel[:, None] + 0.55) / 1.55, 0, 1))[..., None]   # unter der naechsten Reihe dunkler
            farbe[np.ix_(zeilen, spalten)] = linear_zu_srgb(lin)
            dicke[np.ix_(zeilen, spalten)] = hoehe
            ao[np.ix_(zeilen, spalten)] = 0.55 + 0.45 * fuge * (0.6 + 0.4 * np.clip((rel[:, None] + 0.55) / 1.55, 0, 1))
            x += b
    # Normale aus der Dicke (OpenGL: y zeigt nach oben im Bild, Zeilen laufen nach unten)
    gy, gx = np.gradient(dicke)
    staerke = 3.2
    n = np.zeros((h, N, 3), dtype=np.float32)
    nx, ny = -gx * staerke, gy * staerke
    laenge = np.sqrt(nx * nx + ny * ny + 1.0)
    n[..., 0], n[..., 1], n[..., 2] = (nx / laenge) * 0.5 + 0.5, (ny / laenge) * 0.5 + 0.5, (1.0 / laenge) * 0.5 + 0.5
    arm = np.zeros((h, N, 3), dtype=np.float32); arm[..., 0] = ao; arm[..., 1] = 0.82
    blatt.setze(band_flaeche, farbe, n, arm)
    blatt.verlauf(band_flaeche, 0.70, 1.0, 90)                        # oben Schatten der naechsten Stufe
    # Fransen
    a2, e2 = blatt.zeilen(band_franse)
    h2 = e2 - a2
    f2 = np.zeros((h2, N, 3), dtype=np.float32)
    alpha = np.zeros((h2, N), dtype=np.float32)
    n2 = np.zeros((h2, N, 3), dtype=np.float32); n2[...] = (0.5, 0.5, 1.0)
    z2 = np.random.default_rng(33)
    x = 0
    yy2 = np.arange(h2)[:, None]
    for b in schindel_breiten(140):
        ton = np.array([0.58, 0.50, 0.41]) * float(z2.uniform(0.84, 1.08))
        r0, c0 = int(z2.integers(0, holz.kante)), int(z2.integers(0, holz.kante))
        teil = holz.farbe[(r0 + np.arange(h2)) % holz.kante][:, (c0 + np.arange(b)) % holz.kante]
        teil = faerbe(teil, tuple(np.clip(ton, 0, 1)), kontrast=1.2, eigenfarbe=0.25)
        laenge = h2 * float(z2.uniform(0.62, 1.0))
        sx = np.arange(b)[None, :]
        rand = np.minimum(sx, b - 1 - sx)
        ecke = np.clip((rand - 3) / 9.0, 0, 1) ** 0.5                  # leicht gerundete untere Ecken
        al = ((yy2 < laenge * (0.82 + 0.18 * ecke)) & (rand >= 3)).astype(np.float32)
        lin = srgb_zu_linear(teil) * (1.0 - 0.28 * (yy2 / h2))[..., None]
        spalten = (x + np.arange(b)) % N
        f2[:, spalten] = linear_zu_srgb(lin)
        alpha[:, spalten] = al
        x += b
    arm2 = np.zeros((h2, N, 3), dtype=np.float32); arm2[..., 0] = 0.85; arm2[..., 1] = 0.85
    blatt.setze(band_franse, f2, n2, arm2)
    blatt.albedo[a2:e2, :, 3] = alpha


def baue_aussen(quellen):
    B = BAENDER_AUSSEN
    blatt = Blatt()
    schindel_band(blatt, Quelle(quellen, "old_planks_02", kante=512), B["stroh"], B["stroh_franse"])
    holz = Quelle(quellen, "rough_wood", gedreht=True)
    for i, (name, ton, kontrast) in enumerate((("balken_a", (0.40, 0.31, 0.23), 1.0), ("balken_b", (0.37, 0.29, 0.22), 1.1), ("balken_c", (0.31, 0.24, 0.19), 1.2))):
        a, e = blatt.zeilen(B[name])
        f, n, arm = holz.streifen(e - a, ab=120 + 290 * i, versatz=370 * i)
        blatt.setze(B[name], faerbe(f, ton, kontrast=kontrast, eigenfarbe=0.2), n, arm)
        blatt.verlauf(B[name], 0.6, 0.6, 9)
        blatt.fase(B[name])
    # „putz“ ist hier behauener Stein: grosse, saubere Quader (Erdgeschoss, Fuellung der Fachwerkfelder)
    quader = Quelle(quellen, "rough_block_wall", kante=1024)
    a, e = blatt.zeilen(B["putz"])
    f, n, arm = quader.streifen(e - a, ab=140)
    blatt.setze(B["putz"], faerbe(f, (0.56, 0.55, 0.52), kontrast=1.05, eigenfarbe=0.45), n, arm)
    blatt.verlauf(B["putz"], 0.95, 0.82, 60)
    planken = Quelle(quellen, "old_planks_02", kante=512, gedreht=True)
    a, e = blatt.zeilen(B["planken"])
    f, n, arm = planken.streifen(e - a, ab=8)
    blatt.setze(B["planken"], faerbe(f, (0.37, 0.30, 0.24), kontrast=1.1, eigenfarbe=0.3), n, arm)
    a, e = blatt.zeilen(B["farbholz"])
    f, n, arm = planken.streifen(e - a, ab=154, versatz=190)
    blatt.setze(B["farbholz"], faerbe(f, (0.82, 0.80, 0.75), kontrast=0.55, eigenfarbe=0.05), n, arm)
    # „bruchstein“: dunkler, kleinteiliger Bruch (Sockel, Schlot)
    bruch = Quelle(quellen, "castle_wall_slates", kante=1024)
    a, e = blatt.zeilen(B["bruchstein"])
    f, n, arm = bruch.streifen(e - a, ab=300)
    blatt.setze(B["bruchstein"], faerbe(f, (0.42, 0.41, 0.39), kontrast=1.0, eigenfarbe=0.5), n, arm)
    eisen = Quelle(quellen, "rusty_metal_02")
    a, e = blatt.zeilen(B["eisen"])
    f, n, arm = eisen.streifen(e - a, ab=0)
    blatt.setze(B["eisen"], faerbe(f, (0.15, 0.14, 0.14), kontrast=0.5, eigenfarbe=0.25), n, arm, rauheit=0.62, metall=1.0)
    blatt.speichere("trim_zwerg")


def baue_innen(quellen):
    B = BAENDER_INNEN
    blatt = Blatt()

    def band(name, q, ton, ab=0, versatz=0, kontrast=1.0, eigenfarbe=0.3, **kw):
        a, e = blatt.zeilen(B[name])
        f, n, arm = q.streifen(e - a, ab=ab, versatz=versatz)
        blatt.setze(B[name], faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe), n, arm, **kw)

    band("bohlen", Quelle(quellen, "old_planks_02", gedreht=True), (0.45, 0.36, 0.27), ab=60)
    holz = Quelle(quellen, "rough_wood", gedreht=True)
    band("balken", holz, (0.36, 0.28, 0.21), ab=300, kontrast=1.1, eigenfarbe=0.2)
    band("lehmwand", Quelle(quellen, "rough_block_wall"), (0.58, 0.57, 0.54), ab=140, kontrast=1.05, eigenfarbe=0.4)   # Steinwand statt Lehmputz
    blatt.verlauf(B["lehmwand"], 0.9, 0.78, 50)
    band("lehmboden", Quelle(quellen, "cobblestone_floor_01"), (0.45, 0.43, 0.40), ab=0, kontrast=0.9, eigenfarbe=0.4)  # Steinpflaster statt Lehm
    band("bruchstein", Quelle(quellen, "castle_wall_slates"), (0.45, 0.44, 0.42), ab=300, eigenfarbe=0.5)
    band("sackleinen", Quelle(quellen, "hessian_230", kante=512), (0.50, 0.42, 0.30), kontrast=1.0)
    band("wolle", Quelle(quellen, "knitted_fleece", kante=512), (0.40, 0.36, 0.33), kontrast=1.6)
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
    blatt.speichere("innen_trim_zwerg")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict(a[2:].split("=", 1) for a in args if a.startswith("--") and "=" in a)
    nur = opt.get("nur", "")
    if nur in ("", "aussen"): baue_aussen(opt["quellen"])
    if nur in ("", "innen"): baue_innen(opt["quellen"])
