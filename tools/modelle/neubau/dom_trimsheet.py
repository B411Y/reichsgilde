"""Trim-Sheets der Dome (Neubau, Gruppe "dom"): eigenes Blatt aussen und innen, gleiche Technik wie rathaus_trimsheet.py.

    blender -b --python tools/modelle/neubau/dom_trimsheet.py -- --quellen=<ordner> [--nur=aussen|innen|evangelisch]

<ordner> enthaelt je Poly-Haven-Name einen Unterordner mit `<name>_diff_2k.jpg`, `<name>_nor_gl_2k.jpg`, `<name>_arm_2k.jpg`
(CC0, api.polyhaven.com/files/<name>, nie im Repository; Namen und Urheber in game/assets/texturen/neubau/HERKUNFT.md).
Schreibt game/assets/texturen/neubau/dom_trim_{albedo,normal,orm}.png und dom_innen_trim_{...}.png (2048 x 2048), dazu
dom_evangelisch_trim_{...}.png: das Blatt der Huelle des Doms evangelisch mit denselben Baendern (BAENDER_DOM), im Band "mauer"
aber roter Backstein statt des grauen Werksteins, der Sockel aus Sandstein und das Farbfeld "kupfer" mit Patina-Laeufern.
Die Baender stehen in BAENDER_DOM / BAENDER_DOM_INNEN; die Bauskripte der Dome (dom_bib.py, dom_<glaube>_*.py) lesen sie von hier.
Das Dachband liegt wie bei allen Neubau-Blaettern in v 0-0,25 (der Shader building_trim erkennt daran das Dach fuer Schnee und Regen).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from trimsheet import N, Blatt, Quelle, faerbe, rauschen, srgb_zu_linear, linear_zu_srgb, lade, skaliere  # noqa: E402

# (v0, v1, Meter je UV.x 0..1). Hoehe eines Bandes in Metern = (v1 - v0) * Meter.
# Reihenfolge nach Farbe: Im Pixel-Look liest der Shader grobe Mip-Stufen, dort mischen sich benachbarte Baender. Neben dem Schiefer
# und der Mauer liegen darum graue Baender, das braune Holz am Ende (Klicktest 23.11.2026: Holz neben dem Dach faerbte Dach und Mauer braun).
BAENDER_DOM = {
    "schiefer":       (0.0000, 0.1875, 4.0),
    "schiefer_kante": (0.1875, 0.2500, 4.0),   # Alpha-Cards an den Stufenkanten der Daecher
    "sockel":         (0.2500, 0.3000, 4.0),   # dunkler Bruchstein: Sockel (0,20 m)
    "eisen":          (0.3000, 0.3250, 2.0),
    "mauer":          (0.3250, 0.6250, 4.0),   # Stein der Menschenhaeuser (castle_wall_varriation), grau eingefaerbt: Wandflaechen (1,20 m)
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


MAUER_GRAU = (0.69, 0.70, 0.71)       # helles, leicht kuehles Grau der Dom-Mauer (frueherer grauer Werkstein: 0,68 / 0,67 / 0,65)
SCHIEFER_REIHEN = 3                   # Deckreihen im Band "schiefer": je 0,25 m = 128 Bildpunkte (wie die Ziegellage "stroh_franse" der Wohnhaeuser)
SCHIEFER_TON = (0.50, 0.53, 0.60)     # Schiefergrau-Blau (unveraendert seit 23.11.2026)
SCHIEFER_FUGE = (0.20, 0.22, 0.29)    # Band "schiefer_kante": Fuge zwischen den Deckreihen und Karten an der Traufe
SCHIEFER_REIHE_HELL = (0.93, 1.00, 1.07)   # die drei Reihen leicht verschieden hell: im Pixel-Look ist eine Reihe ein einziger Texel (Mip 7)


def schiefer_reihen(blatt, band, q, hell=2.00, ruhig=False):
    """Schieferdach in Deckreihen (Nutzer, 24.11.2026: Reihen sichtbar wie am Ziegeldach des Wohnhauses). Dasselbe Verfahren wie
    trimsheet_stufen.dach_band: eine Reihe = 0,25 m = 128 Bildpunkte, oben der Schatten der naechsten Reihe, unten die helle Kante; das
    Bauskript (dom_bib.schieferflaeche) legt die Dachflaeche in Lagen von 0,25 m mit kleiner Stufe. Das Band traegt drei solche Reihen
    untereinander (verschiedene Ausschnitte des Schiefers, verschiedene Platten), die Lagen des Dachs nehmen sie reihum. Je Reihe einzelne
    Platten (verschieden hell, dunkle Stossfuge). Die mittlere Helligkeit bleibt die des Bandes vom 23.11.2026 (Faktor 0,96 des Verlaufs)."""
    v0, v1, m = band
    a, e = blatt.zeilen(band)
    h = (e - a) // SCHIEFER_REIHEN
    gewicht = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    y = np.arange(h, dtype=np.float32)
    # Reihenkontrast erhoeht (Nutzer 25.11.2026: im Pixel-Look so lesbar wie die Ziegel der Wohnhaeuser): tieferer Schatten unter der naechsten
    # Reihe, breitere und hellere Kante der Platten, insgesamt heller (Schiefergrau-Blau bleibt)
    fy = 0.42 + 0.58 * np.clip(y / 56.0, 0, 1) ** 0.8                 # oben: Schatten der naechsten Reihe
    fy = fy * (1.0 + 0.90 * np.clip((y - (h - 26)) / 8.0, 0, 1))      # unten: helle Kante der Platten
    ziel = None
    for r in range(SCHIEFER_REIHEN):
        f, n, arm = q.streifen(h, ab=60 + 311 * r, versatz=397 * r)
        lin = srgb_zu_linear(faerbe(f, SCHIEFER_TON, kontrast=0.85 if ruhig else 1.15, eigenfarbe=0.25 if ruhig else 0.35))
        if ziel is None: ziel = hell * float((lin @ gewicht).mean())
        z = np.random.default_rng(140 + r)
        fx = np.ones(N, dtype=np.float32)
        x = int(z.integers(0, 120))
        while x < N + 120:                                            # Platten 0,27 bis 0,43 m breit, waagerecht nahtlos
            w = int(z.integers(140, 221))
            hell_p = float(z.uniform(0.93, 1.07) if ruhig else z.uniform(0.86, 1.14))
            sp = np.arange(x, x + w)
            fuge = 1.0 - 0.45 * np.clip(1.0 - np.abs(np.arange(w) - 2.5) / 3.5, 0, 1)   # Stossfuge am linken Rand der Platte
            fx[sp % N] = hell_p * fuge
            x += w
        lin = lin * fx[None, :, None] * fy[:, None, None]
        lin = lin * (ziel * SCHIEFER_REIHE_HELL[r] / max(float((lin @ gewicht).mean()), 1e-4))
        teil = (v0 + (v1 - v0) * r / SCHIEFER_REIHEN, v0 + (v1 - v0) * (r + 1) / SCHIEFER_REIHEN, m)
        blatt.setze(teil, linear_zu_srgb(np.clip(lin, 0, 1)), n, arm)
        ta, te = blatt.zeilen(teil)
        blatt.orm[ta:te, :, 0] *= (0.5 + 0.5 * np.clip(fy, 0, 1))[:, None]
        print(f"Dom-Schiefer Reihe {r}: Zeilen {ta}-{te}, Helligkeit {ziel * SCHIEFER_REIHE_HELL[r]:.4f}")


def baue_aussen(quellen, evangelisch=False):
    B = BAENDER_DOM
    blatt = Blatt()
    schiefer = Quelle(quellen, "roof_slates_02")
    # Dom katholisch (Pruefer 25.11.2026: Schattenseite fast schwarz, im Pixel-Look koernig): heller und ruhigeres Korn; der evangelische bleibt
    schiefer_reihen(blatt, B["schiefer"], schiefer, hell=2.0 if evangelisch else 2.6, ruhig=not evangelisch)
    a2, e2 = blatt.zeilen(B["schiefer_kante"])
    h2 = e2 - a2
    f, n, arm = schiefer.streifen(h2, ab=400, versatz=200)
    # dunkel (vorher 0,46 / 0,49 / 0,56): Aus diesem Band nimmt auch die senkrechte Stirn jeder Deckreihe ihre Farbe (dom_bib.schieferflaeche) -
    # die dunkle Fuge zwischen den Reihen; die Karten an der Traufe lesen sich damit als Schattenkante.
    blatt.setze(B["schiefer_kante"], faerbe(f, SCHIEFER_FUGE, kontrast=1.2, eigenfarbe=0.3), n, arm)
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
    if evangelisch:
        # Backstein im Klosterformat: 14 Lagen auf die Bandhoehe von 1,20 m (die Quelle zeigt 32 Lagen auf ihre Kante), damit die
        # Lagen der Mauer (rathaus_bib.mauer stapelt Bandhoehen) ohne halbe Steine aufeinander stehen
        a, e = blatt.zeilen(B["mauer"])
        f, n, arm = Quelle(quellen, "medieval_red_brick", kante=round((e - a) / 14 * 32)).streifen(e - a, ab=0)
        blatt.setze(B["mauer"], f, n, arm)                    # Originalfarben der Quelle: rote Steine, sandfarbene Fugen
    else:
        a, e = blatt.zeilen(B["mauer"])
        f, n, arm = haus_stein_band(quellen, e - a, BAENDER_DOM["mauer"][2])          # Mauer: Stein der Menschenhaeuser S3 (24.11.2026), gleiche Lagen und Fugen
        # ... aber kuehl grau eingefaerbt (Nutzer, 24.11.2026): in der eigenen Farbe (braun-ocker) hob sich die Wand im Pixel-Look kaum von den
        # gelben Sandstein-Gliedern ab. Wenig Eigenfarbe (sonst bleibt der Braunstich), Kontrast angehoben, damit die Fugen dunkel bleiben.
        # Danach auf die Helligkeit des Zieltons gehoben: der Stein ist dunkel mit hellen Fugen, mit Kontrast fiel das Mittel sonst weit unter
        # den Ton (erster Stand 0,62 / 0,64 / 0,67 las sich im Spiel schieferblau wie das Dach).
        lin = srgb_zu_linear(faerbe(f, MAUER_GRAU, kontrast=1.2, eigenfarbe=0.05))
        gewicht = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
        soll = float(srgb_zu_linear(np.array(MAUER_GRAU, dtype=np.float32)) @ gewicht)
        ist = float((lin @ gewicht).mean())
        print(f"Dom-Mauer: Helligkeit {ist:.4f} -> {soll:.4f}")
        blatt.setze(B["mauer"], linear_zu_srgb(np.clip(lin * (soll / ist), 0, 1)), n, arm)
    band("sandstein", quader, (0.80, 0.74, 0.62), ab=640, versatz=300, kontrast=1.5, eigenfarbe=0.15)
    blatt.verlauf(B["sandstein"], 0.9, 0.9, 12)
    band("werkstein", Quelle(quellen, "white_plaster_rough_01"), (0.82, 0.77, 0.66), ab=200, kontrast=1.3, eigenfarbe=0.1)
    blatt.verlauf(B["werkstein"], 0.85, 0.85, 14)
    band("sockel", Quelle(quellen, "castle_wall_slates"), (0.58, 0.52, 0.42) if evangelisch else (0.36, 0.36, 0.36), ab=300, kontrast=1.05, eigenfarbe=0.3)
    a, e = blatt.zeilen(B["eisen"])
    f, n, arm = Quelle(quellen, "rusty_metal_02").streifen(e - a, ab=0)
    blatt.setze(B["eisen"], faerbe(f, (0.16, 0.15, 0.15), kontrast=0.5, eigenfarbe=0.25), n, arm, rauheit=0.62, metall=1.0)
    _felder(blatt, quellen, B["kleinteile"], FELDER_DOM, {
        "gold": (True, (0.86, 0.74, 0.40), 0.3), "bronze": (True, (0.40, 0.32, 0.20), 0.45), "zifferblatt": (False, (0.95, 0.93, 0.86), 0.8),
        "blei": (True, (0.34, 0.36, 0.40), 0.5), "holz_dunkel": (False, (0.16, 0.12, 0.09), 0.9), "nacht": (False, (0.03, 0.03, 0.04), 0.9),
        "eisen": (True, (0.13, 0.13, 0.13), 0.55), "kupfer": (True, (0.30, 0.52, 0.44), 0.6),
    })
    if evangelisch:
        # Kupferhaube mit Gruenspan-Patina (Nutzer 26.11.2026: wie alte Kupferdaecher, heller, tuerkis bis blaugruen, fleckig, in Fugen und Rippen
        # dunkleres Kupferbraun). Textur: ambientCG "Metal 058 C" (CC0, Kupfer mit Oxidation, https://ambientcg.com/view?id=Metal058C), Farbe,
        # Normale, Rauheit und Metallwert in das Farbfeld "kupfer"; Rippenfugen und die Traufkante in Kupferbraun darueber gelegt.
        a, e = blatt.zeilen(B["kleinteile"])
        feld = N // len(FELDER_DOM); i = FELDER_DOM.index("kupfer")
        h_ = e - a
        o = os.path.join(quellen, "Metal058C")
        farbe = skaliere(lade(os.path.join(o, "Metal058C_1K-JPG_Color.jpg")), 1024, 1024)
        norm = skaliere(lade(os.path.join(o, "Metal058C_1K-JPG_NormalGL.jpg"), daten=True), 1024, 1024)
        rau = skaliere(lade(os.path.join(o, "Metal058C_1K-JPG_Roughness.jpg"), daten=True), 1024, 1024)
        met = skaliere(lade(os.path.join(o, "Metal058C_1K-JPG_Metalness.jpg"), daten=True), 1024, 1024)
        zl, sp = (150 + np.arange(h_)) % 1024, (300 + np.arange(feld)) % 1024
        c3 = farbe[zl][:, sp][..., :3]
        col = np.clip(c3 * 0.95, 0, 1)                                   # hell, aber nicht ausgebleicht: im Pixel-Look klar als Patina lesbar
        u = np.arange(feld, dtype=np.float32)
        rippe = ((u % (feld / 16.0)) < 4.0).astype(np.float32)[None, :] * np.ones((h_, 1), np.float32)       # Rippenfuge der 16 Seiten
        kante = (np.arange(h_)[:, None] > h_ - 6).astype(np.float32) * np.ones((1, feld), np.float32)        # Traufkante unten
        m = np.clip(rippe * 0.8 + kante * 0.9, 0, 1)[..., None]
        braun = np.array([0.36, 0.22, 0.13], dtype=np.float32)
        col = col * (1 - m) + braun[None, None, :] * m
        x0, x1 = i * feld, (i + 1) * feld
        blatt.albedo[a:e, x0:x1, :3] = np.clip(col, 0, 1)
        blatt.normal[a:e, x0:x1] = norm[zl][:, sp]
        blatt.orm[a:e, x0:x1, 0] = 1.0
        blatt.orm[a:e, x0:x1, 1] = np.clip(rau[zl][:, sp][..., 0] * 0.9 + 0.1, 0, 1)
        blatt.orm[a:e, x0:x1, 2] = 0.25 * met[zl][:, sp][..., 0]
    blatt.speichere("dom_evangelisch_trim" if evangelisch else "dom_trim")


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
    if opt.get("nur", "evangelisch") == "evangelisch":
        baue_aussen(opt["quellen"], evangelisch=True)
    if opt.get("nur", "innen") == "innen":
        baue_innen(opt["quellen"])
