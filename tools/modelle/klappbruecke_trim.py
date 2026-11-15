"""Trim-Sheet der Klappbruecke Koeln: Albedo, Normal, ORM (2048 x 2048), wie die Haeuser (building_trim.gdshader: UV1 aufs Blatt, UV2 auf die gebackene Verdeckung).

    ~/werkzeuge/blender/blender.sh -b --python tools/modelle/klappbruecke_trim.py -- --quellen=<ordner>

Quellen (Poly Haven, CC0, je Ordner <name>/<name>_diff_2k.jpg, _nor_gl_2k.jpg, _arm_2k.jpg): sandstone_blocks_08, rough_wood, old_planks_02, rusty_metal_02,
roof_slates_02 (Herkunft: game/assets/texturen/bruecke/HERKUNFT.md). Schreibt game/assets/texturen/bruecke/bruecke_trim_{albedo,normal,orm}.png.

Baender (Anteil der Bildhoehe, v = 0 oben; Meter = so viele Meter deckt UV.x 0..1; 4 m = 512 Bildpunkte je Meter):
    quader_a, quader_b   Quaderverband des zwergischen Steins: eine Lage = 0,5 m hoch, Steine 0,28 bis 1,7 m lang in zwei Schichten oder als ein hoher Stein,
                         vertiefte Fugen (12 px), Fase an jeder Kante, Ton je Stein (Quaderband nach trimsheet_stufen.py, Steinhaus).
    quader_nass          wie quader, dunkler und gruenlich (Wasserlinie am Pfeiler), andere Steinverteilung.
    werkstein            ein einzelner Stein ueber die Bandhoehe (Abdeckplatten, Gesims).
    bohlen               Eichenbohlen: zwei Bohlen je Band (Bohlenbreite 0,25 m), Maserung entlang UV.x, dunkle Fuge, Nagelkoepfe.
    balken               Eichenbalken mit Maserung, abgefaste Kanten.
    eisen_rost           rostiges Eisen (Ketten, Gewichtsbaender): Rostflecken ueber dunklem Metall.
    eisen_fein           elbisches Eisen: dunkles, glattes Metall mit blauem Stich, kaum Rost (Gelaenderstaebe, Ranken, Spitzen).
    dach                 Schiefer (Torhausdaecher).
    eisen_beschlag       Beschlag: dunkles Schmiedeeisen mit Rostspuren und einer Nietenreihe in der Mitte (liegt ab v = 0,95: Beschlaege sind in der
                         Pruefung "keine schwarze Flaeche" ausgenommen, wie bei den Haeusern).
"""
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HIER, "neubau"))
import trimsheet as ts                                                   # noqa: E402
from trimsheet import Blatt, N, Quelle, faerbe, lade, skaliere          # noqa: E402

WURZEL = os.path.dirname(os.path.dirname(HIER))
ZIEL = os.path.join(WURZEL, "game", "assets", "texturen", "bruecke")
BLATT = "bruecke_trim"

# Name -> (v0, v1, Meter); Meter = 4 (512 Bildpunkte je Meter)
BRUECKE_BAENDER = {
    "quader_a":       (0.0000, 0.1250, 4.0),
    "quader_b":       (0.1250, 0.2500, 4.0),
    "werkstein":      (0.2500, 0.3125, 4.0),
    "bohlen":         (0.3125, 0.4375, 4.0),
    "balken":         (0.4375, 0.5000, 4.0),
    "eisen_rost":     (0.5000, 0.5625, 4.0),
    "eisen_fein":     (0.5625, 0.6250, 4.0),
    "dach":           (0.6250, 0.7500, 4.0),
    "quader_nass":    (0.7500, 0.8750, 4.0),
    "eisen_beschlag": (0.9500, 1.0000, 4.0),
}
PX_M = 512


# ── Quaderverband (Steinhaus: tools/modelle/neubau/trimsheet_stufen.py quader_plan/quaderband, hier uebernommen) ──────────

def quader_plan(breite_px, hoehe_px, px_m, saat):
    """Quaderverband einer Lage: Liste der Steine (x0, x1, y0, y1) in Pixeln, waagerecht nahtlos."""
    z = np.random.default_rng(saat)
    steine = []
    x = 0
    while x < breite_px - 1:
        if z.random() < 0.45:
            w = min(int(z.uniform(0.38, 0.62) * px_m), breite_px - x)
            steine.append((x, x + w, 0, hoehe_px))
        else:
            w = min(int(z.uniform(0.9, 1.7) * px_m), breite_px - x)
            teil = int(hoehe_px * z.uniform(0.4, 0.6))
            for y0, y1 in ((0, teil), (teil, hoehe_px)):
                a = x
                while a < x + w - 1:
                    b = min(a + int(z.uniform(0.28, 0.66) * px_m), x + w)
                    if x + w - b < 0.2 * px_m: b = x + w
                    steine.append((a, b, y0, y1)); a = b
        x += w
    return steine


def quaderband(blatt, band, q, ton, saat, ab=0, px_m=PX_M, fuge_px=12, fase_px=9, streuung=0.08, kontrast=0.45, eigenfarbe=0.08, rauheit_plus=0.0):
    """Behauene Quader im Band: Quelltextur umgefaerbt, je Stein eigener Ton, vertiefte dunkle Fugen, Fase an jeder Kante (Normale)."""
    a, e = blatt.zeilen(band)
    h = e - a
    f, n, arm = q.streifen(h, ab=ab)
    farbe = faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe)
    z = np.random.default_rng(saat + 1)
    stein_id = np.full((h, N), -1, dtype=np.int32)
    rand = np.full((h, N), 1e9, dtype=np.float32)
    nx = np.zeros((h, N), dtype=np.float32); ny = np.zeros((h, N), dtype=np.float32)
    plan = quader_plan(N, h, px_m, saat)
    halb = fuge_px / 2
    for i, (x0, x1, y0, y1) in enumerate(plan):
        ys = np.arange(y0, y1); xs = np.arange(x0, x1)
        dy0, dy1 = ys - y0 + 0.5, y1 - ys - 0.5
        dx0, dx1 = xs - x0 + 0.5, x1 - xs - 0.5
        innen_y = np.minimum(dy0, dy1) - halb; innen_x = np.minimum(dx0, dx1) - halb
        d = np.minimum(innen_y[:, None], innen_x[None, :])
        cols = xs % N
        stein_id[y0:y1][:, cols] = i
        rand[y0:y1][:, cols] = d
        k = lambda t: np.clip(1 - t / fase_px, 0, 1) * (t >= 0)
        ty0, ty1 = (dy0 - halb)[:, None], (dy1 - halb)[:, None]
        tx0, tx1 = (dx0 - halb)[None, :], (dx1 - halb)[None, :]
        ny[y0:y1][:, cols] = 0.55 * (k(ty0) - k(ty1)) * (np.minimum(ty0, ty1) <= np.minimum(tx0, tx1))
        nx[y0:y1][:, cols] = 0.55 * (k(tx1) - k(tx0)) * (np.minimum(tx0, tx1) < np.minimum(ty0, ty1))
    hell = z.uniform(1 - streuung, 1 + streuung, len(plan)).astype(np.float32)
    warm = z.uniform(-0.025, 0.025, len(plan)).astype(np.float32)
    faktor = np.stack([hell * (1 + warm), hell, hell * (1 - warm)], axis=1)
    farbe = farbe * faktor[np.maximum(stein_id, 0)]
    fuge = rand < 0
    nah = np.clip(rand / fase_px, 0, 1)
    farbe = farbe * (0.86 + 0.14 * nah)[..., None]
    farbe = np.where(fuge[..., None], farbe * 0.55, farbe)
    normale = 0.5 + (n - 0.5) * 0.5
    normale[..., 0] = np.clip(normale[..., 0] + nx * 0.5, 0, 1)
    normale[..., 1] = np.clip(normale[..., 1] + ny * 0.5, 0, 1)
    normale = np.where(fuge[..., None], np.array([0.5, 0.5, 1.0], dtype=np.float32), normale)
    arm = arm.copy()
    arm[..., 0] = np.where(fuge, arm[..., 0] * 0.5, arm[..., 0] * (0.8 + 0.2 * nah))
    arm[..., 1] = np.clip(np.where(fuge, 0.95, arm[..., 1] + rauheit_plus), 0, 1)
    blatt.setze(band, np.clip(farbe, 0, 1), normale, arm)


def stein_flaeche(blatt, band, q, ton, ab=0, kontrast=1.0, eigenfarbe=0.3, fase_px=10):
    """Ein Werkstein ueber die ganze Bandhoehe (Abdeckplatte, Gesims): Quelltextur, Fase oben und unten."""
    a, e = blatt.zeilen(band)
    f, n, arm = q.streifen(e - a, ab=ab)
    blatt.setze(band, faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe), 0.5 + (n - 0.5) * 0.6, arm)
    blatt.fase(band, px=fase_px, staerke=0.5)


# ── Holz und Eisen ─────────────────────────────────────────────────────────────────────────────────────────────────────

def punkt_nieten(blatt, band, xs, ys, r, tiefe=0.35):
    """Runde Koepfe (Nieten, Naegel) an den Stellen (xs, ys) im Band: Rand dunkel, Kuppe hell, Normale gewoelbt, Verdeckung am Rand."""
    a, e = blatt.zeilen(band)
    gy, gx = np.mgrid[0:e - a, 0:N].astype(np.float32)
    for x in xs:
        for y in ys:
            dx = (gx - x + N / 2) % N - N / 2
            dy = gy - y
            d = np.sqrt(dx * dx + dy * dy)
            innen = d <= r
            if not innen.any(): continue
            t = np.clip(d / r, 0, 1)
            kuppe = np.sqrt(np.clip(1 - t * t, 0, 1))
            licht = (0.55 + 0.45 * kuppe - 0.25 * (dx + dy) / (2 * r))
            lin = ts.srgb_zu_linear(blatt.albedo[a:e, :, :3])
            neu = lin * np.where(innen, np.clip(licht, 0.3, 1.6), 1.0)[..., None]
            ring = (d > r * 0.8) & (d <= r + 1.5)
            neu = neu * np.where(ring, 0.5, 1.0)[..., None]
            blatt.albedo[a:e, :, :3] = ts.linear_zu_srgb(neu)
            blatt.normal[a:e, :, 0] = np.where(innen, np.clip(0.5 - tiefe * dx / r * 0.5, 0, 1), blatt.normal[a:e, :, 0])
            blatt.normal[a:e, :, 1] = np.where(innen, np.clip(0.5 + tiefe * dy / r * 0.5, 0, 1), blatt.normal[a:e, :, 1])
            blatt.orm[a:e, :, 0] = np.where(ring, blatt.orm[a:e, :, 0] * 0.55, blatt.orm[a:e, :, 0])


def rost_maske(h, saat, sigma_x=14.0, sigma_y=None, deckung=0.5):
    """Weiche Rostflecken 0..1, in beiden Richtungen nahtlos: Zufallsfeld im Frequenzraum geglaettet, dann durch eine Schwelle (deckung = Anteil Rost)
    geschoben; Feinkorn darueber."""
    sigma_y = sigma_y or max(h / 9.0, 2.0)
    z = np.random.default_rng(saat)
    f = np.fft.fft2(z.normal(size=(h, N)))
    ky = np.fft.fftfreq(h)[:, None] * 2 * np.pi
    kx = np.fft.fftfreq(N)[None, :] * 2 * np.pi
    f = f * np.exp(-0.5 * ((kx * sigma_x) ** 2 + (ky * sigma_y) ** 2))
    g = np.real(np.fft.ifft2(f))
    g = (g - g.mean()) / (g.std() + 1e-6)
    schwelle = np.quantile(g, 1 - deckung)
    m = np.clip((g - schwelle) * 2.0 + 0.5, 0, 1)
    korn = z.random((h, N)) * 0.25
    return np.clip(m * (0.75 + korn), 0, 1).astype(np.float32)


def baue(quellen):
    B = BRUECKE_BAENDER
    blatt = Blatt()
    sand = Quelle(quellen, "sandstone_blocks_08")
    quaderband(blatt, B["quader_a"], sand, (0.56, 0.54, 0.51), saat=5, ab=0, eigenfarbe=0.2, kontrast=0.85)
    quaderband(blatt, B["quader_b"], sand, (0.52, 0.52, 0.50), saat=9, ab=256, streuung=0.10, eigenfarbe=0.2, kontrast=0.85)
    quaderband(blatt, B["quader_nass"], sand, (0.40, 0.44, 0.41), saat=13, ab=512, streuung=0.12, eigenfarbe=0.12, rauheit_plus=-0.15, kontrast=0.6)
    stein_flaeche(blatt, B["werkstein"], sand, (0.58, 0.56, 0.53), ab=768)

    # Eichenbohlen: zwei Bohlen je Band, Fuge dazwischen und am Rand, Maserung entlang UV.x, Nagelkoepfe
    planken = Quelle(quellen, "old_planks_02", kante=512, gedreht=True)
    a, e = blatt.zeilen(B["bohlen"]); h = e - a
    f, n, arm = planken.streifen(h, ab=8)
    farbe = faerbe(f, (0.54, 0.39, 0.26), kontrast=1.15, eigenfarbe=0.3)
    z = np.random.default_rng(21)
    zeile = np.arange(h)
    bohle = (zeile >= h // 2).astype(np.int32)
    ton = z.uniform(0.9, 1.1, 2)[bohle]
    farbe = farbe * ton[:, None, None]
    fuge = np.minimum(np.minimum(zeile % (h // 2), (h // 2) - 1 - zeile % (h // 2)), 99) < 4
    farbe = np.where(fuge[:, None, None], farbe * 0.45, farbe)
    n = n.copy(); n[fuge] = (0.5, 0.5, 1.0)
    arm = arm.copy(); arm[fuge, :, 0] *= 0.4
    blatt.setze(B["bohlen"], np.clip(farbe, 0, 1), n, arm)
    blatt.fase(B["bohlen"], px=5, staerke=0.3)
    punkt_nieten(blatt, B["bohlen"], [64 + 512 * i for i in range(4)] + [320 + 512 * i for i in range(4)], [h // 4, 3 * h // 4], 9)

    holz = Quelle(quellen, "rough_wood", gedreht=True)
    a, e = blatt.zeilen(B["balken"])
    f, n, arm = holz.streifen(e - a, ab=410, versatz=370)
    blatt.setze(B["balken"], faerbe(f, (0.48, 0.35, 0.24), kontrast=1.1, eigenfarbe=0.2), n, arm)
    blatt.verlauf(B["balken"], 0.6, 0.6, 9)
    blatt.fase(B["balken"])

    metall = Quelle(quellen, "rusty_metal_02")
    # rostiges Eisen: dunkles Metall, Rost (orange-braun) in Flecken
    for name, ton_eisen, ton_rost, saat, deckung, rauh, ab in (("eisen_rost", (0.20, 0.18, 0.17), (0.46, 0.22, 0.10), 31, 0.45, 0.7, 40),
                                                                 ("eisen_fein", (0.30, 0.31, 0.35), (0.34, 0.19, 0.10), 33, 0.06, 0.38, 300)):
        a, e = blatt.zeilen(B[name]); h = e - a
        f, n, arm = metall.streifen(h, ab=ab)
        fe = faerbe(f, ton_eisen, kontrast=0.5 if name == "eisen_rost" else 0.3, eigenfarbe=0.2)
        rost = faerbe(f, ton_rost, kontrast=0.8, eigenfarbe=0.5)
        m = rost_maske(h, saat, deckung=deckung)[..., None] * 0.85
        farbe = fe * (1 - m) + rost * m
        blatt.setze(B[name], np.clip(farbe, 0, 1), n * 0.7 + 0.15, arm, metall=1.0)
        rau = np.clip(rauh + 0.25 * m[..., 0], 0, 1)
        blatt.orm[a:e, :, 1] = rau
        blatt.orm[a:e, :, 2] = np.clip(1.0 - 0.75 * m[..., 0], 0, 1)         # Rost ist kein Metall
    # Beschlag: dunkles Schmiedeeisen, Rostspuren, Nietenreihe in der Mitte
    a, e = blatt.zeilen(B["eisen_beschlag"]); h = e - a
    f, n, arm = metall.streifen(h, ab=700)
    fe = faerbe(f, (0.19, 0.18, 0.18), kontrast=0.5, eigenfarbe=0.2)
    rost = faerbe(f, (0.40, 0.19, 0.09), kontrast=0.8, eigenfarbe=0.5)
    m = rost_maske(h, 35, deckung=0.25)[..., None]
    blatt.setze(B["eisen_beschlag"], np.clip(fe * (1 - m) + rost * m, 0, 1), n, arm, rauheit=0.6, metall=1.0)
    blatt.orm[a:e, :, 2] = np.clip(1.0 - 0.7 * m[..., 0], 0, 1)
    blatt.fase(B["eisen_beschlag"], px=4, staerke=0.35)
    punkt_nieten(blatt, B["eisen_beschlag"], [30 + 61 * i for i in range(N // 61 + 1)], [h // 2], 7)

    slate = Quelle(quellen, "roof_slates_02")
    a, e = blatt.zeilen(B["dach"])
    f, n, arm = slate.streifen(e - a, ab=100)
    blatt.setze(B["dach"], faerbe(f, (0.36, 0.39, 0.44), kontrast=1.1, eigenfarbe=0.25), n, arm)
    blatt.verlauf(B["dach"], 0.8, 0.9, 10)
    ts.ZIEL = ZIEL
    blatt.speichere(BLATT)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    if "quellen" not in opt: raise SystemExit("--quellen=<ordner> fehlt")
    baue(opt["quellen"])


if any(a.startswith("--quellen") for a in sys.argv):      # nur als Skript (blender --python), nicht beim Import durch klappbruecke.py
    main()
