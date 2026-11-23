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
# Holz aussen und innen: helles Fichten-/Tannenbraun (Entscheidungen des Nutzers 18.11.2026, alle Stufen): Mitte zwischen dem grau
# verwitterten Holz des Kunstwerks (0,55/0,50/0,42) und dem ersten Fichtenbraun (0,50/0,37/0,24)
BALKEN = ((0.525, 0.435, 0.33), (0.495, 0.41, 0.31), (0.425, 0.35, 0.265))   # balken_a, _b, _c (Schwellen, Sparrenkoepfe dunkler)
PLANKEN = (0.48, 0.405, 0.315)
SCHNITZWERK = ("balken_a", "balken_b", "balken_c")   # Baender mit geschnitzter Ranke (elb_schnitzwerk.py; Nutzer 18.11.2026)
STEIN = (0.60, 0.57, 0.50)
# Innen (Kunstwerk innen): Putz cremeweiss, Dielen und Balken grau-braun, Herd aus hellem Stein
INNEN_PUTZ = (0.80, 0.77, 0.68)
# Holz innen wie aussen: Fichte/Tanne (Entscheidung des Nutzers 18.11.2026; vorher grau-braun 0,50/0,43/0,35 und 0,47/0,42/0,35)
INNEN_DIELEN = PLANKEN
INNEN_BALKEN = BALKEN[0]
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


MOOS = (0.27, 0.30, 0.19)            # Moos auf den Schindeln (Kunstwerk: gruene Flecken am Dach), Farbe in der gespeicherten PNG
EFEU = (0.20, 0.30, 0.13)            # Efeublaetter (Kunstwerk: Efeu an Ecken und Giebel)


def feld_rauschen(hoehe, breite, zellen_y, zellen_x, saat):
    """Weiches 2D-Rauschen 0..1, waagerecht nahtlos (bilinear aus einem groben Zufallsgitter)."""
    z = np.random.default_rng(saat).random((zellen_y + 1, zellen_x)).astype(np.float32)
    y = np.linspace(0, zellen_y, hoehe, dtype=np.float32)[:, None]
    x = np.linspace(0, zellen_x, breite, endpoint=False, dtype=np.float32)[None, :]
    y0, x0 = np.floor(y).astype(int), np.floor(x).astype(int)
    fy, fx = y - y0, x - x0
    y1, x1 = np.minimum(y0 + 1, zellen_y), (x0 + 1) % zellen_x
    x0 = x0 % zellen_x
    g = lambda yy, xx: z[yy, xx]
    oben = g(y0, x0) * (1 - fx) + g(y0, x1) * fx
    unten = g(y1, x0) * (1 - fx) + g(y1, x1) * fx
    return oben * (1 - fy) + unten * fy


def moos(blatt, band, anteil=0.28):
    """Sparsame Moosflecken auf den Schindeln (nur Textur, keine Flaechen): wo das grobe Rauschen ueber der Schwelle liegt, wird die
    Farbe zum Moos hin gemischt, rauer und ohne Glanz; `anteil` ~ bedeckte Flaeche."""
    a, e = blatt.zeilen(band)
    r = feld_rauschen(e - a, N, 6, 24, 41) * 0.7 + feld_rauschen(e - a, N, 24, 96, 42) * 0.3
    schwelle = np.quantile(r, 1 - anteil)
    w = 0.75 * np.clip((r - schwelle) / 0.08, 0, 1)[..., None]
    lin = srgb_zu_linear(blatt.albedo[a:e, :, :3])
    moos_lin = srgb_zu_linear(np.array(gespeichert(MOOS), dtype=np.float32)) * (0.75 + 0.5 * feld_rauschen(e - a, N, 48, 192, 43))[..., None]
    blatt.albedo[a:e, :, :3] = linear_zu_srgb(lin * (1 - w) + moos_lin * w)
    blatt.orm[a:e, :, 1] = blatt.orm[a:e, :, 1] * (1 - w[..., 0]) + 0.95 * w[..., 0]


def efeu(blatt, band, ranken=5, saat=51):
    """Wenige Efeuranken im Putzband (nur Textur): je Ranke ein Stamm vom unteren Bandrand (Mauerfuss) nach oben, daran Blaetter;
    das Band laeuft waagerecht 8 m, also fuenf Ranken auf 8 m Wand (mehr Efeu: Rueckmeldung des Nutzers 17.11.2026). Farbe, Normale (leicht erhaben) und Rauheit."""
    a, e = blatt.zeilen(band)
    h = e - a
    z = np.random.default_rng(saat)
    maske = np.zeros((h, N), dtype=np.float32)
    for k in range(ranken):
        x = (k + 0.25 + 0.4 * z.random()) * N / ranken
        y = h - 1.0
        oben = h * (0.10 + 0.35 * z.random())
        while y > oben:
            x += z.normal(0, 3.0); y -= 6.0
            for _ in range(2):
                bx, by = x + z.normal(0, 14), y + z.normal(0, 8)
                rx, ry = 7 + 4 * z.random(), 5 + 3 * z.random()
                x0, x1 = int(bx - rx - 1), int(bx + rx + 2); y0, y1 = max(int(by - ry - 1), 0), min(int(by + ry + 2), h)
                xs = np.arange(x0, x1) % N
                if y0 >= y1: continue
                d = ((np.arange(x0, x1)[None, :] - bx) / rx) ** 2 + ((np.arange(y0, y1)[:, None] - by) / ry) ** 2
                maske[y0:y1][:, xs] = np.maximum(maske[y0:y1][:, xs], (d < 1).astype(np.float32))
    w = maske[..., None]
    lin = srgb_zu_linear(blatt.albedo[a:e, :, :3])
    blatt_lin = srgb_zu_linear(np.array(gespeichert(EFEU), dtype=np.float32)) * (0.7 + 0.6 * feld_rauschen(h, N, 40, 400, saat + 1))[..., None]
    blatt.albedo[a:e, :, :3] = linear_zu_srgb(lin * (1 - w) + blatt_lin * w)
    gy, gx = np.gradient(maske * 3.0)
    blatt.normal[a:e, :, 0] = np.clip(blatt.normal[a:e, :, 0] - gx * 0.5, 0, 1)
    blatt.normal[a:e, :, 1] = np.clip(blatt.normal[a:e, :, 1] + gy * 0.5, 0, 1)
    blatt.orm[a:e, :, 0] = blatt.orm[a:e, :, 0] * (1 - 0.25 * maske)
    blatt.orm[a:e, :, 1] = blatt.orm[a:e, :, 1] * (1 - maske) + 0.7 * maske


def baue_aussen(quellen):
    B = BAENDER_AUSSEN
    blatt = Blatt()
    # Dach: die Holzschindeln der Zwerge (drei Reihen je Stufe, Fuesse als Alpha-Cards), grau-olivgruen wie im Kunstwerk
    tz.schindel_band(blatt, Quelle(quellen, "old_planks_02", kante=512), B["stroh"], B["stroh_franse"])
    for band in ("stroh", "stroh_franse"): entsaettigen(blatt, B[band], 0.8)
    umtoenen(blatt, B["stroh"], gespeichert(DACH))
    umtoenen(blatt, B["stroh_franse"], gespeichert(tuple(c * 0.92 for c in DACH)))
    moos(blatt, B["stroh"])                               # Rueckmeldung des Nutzers 17.11.2026: Moos sparsam, als Textur
    holz = Quelle(quellen, "rough_wood", gedreht=True)
    for i, (name, ton, kontrast) in enumerate(zip(("balken_a", "balken_b", "balken_c"), BALKEN, (0.9, 1.0, 1.1))):
        a, e = blatt.zeilen(B[name])
        f, n, arm = holz.streifen(e - a, ab=120 + 290 * i, versatz=370 * i)
        blatt.setze(B[name], faerbe(f, gespeichert(ton), kontrast=kontrast, eigenfarbe=0.1), n, arm)
        blatt.verlauf(B[name], 0.7, 0.7, 9)
        blatt.fase(B[name])
        if name in SCHNITZWERK:
            import elb_schnitzwerk
            elb_schnitzwerk.ranke_auftragen(blatt.albedo, blatt.normal, blatt.orm, a, e, B[name][2], saat=1 + i)
    putz = Quelle(quellen, "white_plaster_rough_01")
    a, e = blatt.zeilen(B["putz"])
    f, n, arm = putz.streifen(e - a, ab=200)
    blatt.setze(B["putz"], faerbe(f, gespeichert(PUTZ), kontrast=0.75, eigenfarbe=0.1), n, arm)
    blatt.verlauf(B["putz"], 0.93, 0.80, 60)              # unten Spritzwasser, oben leichter Schatten (wie bei den Menschen)
    efeu(blatt, B["putz"])                                # Rueckmeldung des Nutzers 17.11.2026: Efeu sparsam, als Textur
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
    import elb_schnitzwerk                                  # geschnitzte Ranke auf Balken, Rahmen und Moebeln (Nutzer 18.11.2026)
    a, e = blatt.zeilen(B["balken"])
    elb_schnitzwerk.ranke_auftragen(blatt.albedo, blatt.normal, blatt.orm, a, e, B["balken"][2], saat=11)
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


# ── Stufe 2 (Buergerhaus) und Stufe 3 (Steinhaus) der Elben (Auftrag 17.11.2026; Kunstwerke wohnhaus_lvl2_elb, wohnhaus_lvl3_elb,
#    wohnhaus_in_lvl2_elb, wohnhaus_in_lvl3_elb): abgeleitet aus trim_elb / innen_trim_elb wie trimsheet_stufen.py bei den Menschen.
#    trim_s2_elb:        Schiefer wie Mensch S3 (roof_slates_02, gleiche Helligkeit), mit Moos; Putz, Holz, Efeu, Haustein der Stufe 1
#    trim_s3_elb:        wie trim_s2_elb, dazu Mauer im Band "bruchstein" aus K2 (large_sandstone_blocks_01) mit Efeu und Werkstein im
#                        Band "putz" aus K5 (Bricks100) – im Spiel setzt die Steinebene elb_s3 K2 und K5 noch einmal in eigener Farbe ein
#                        und traegt den Efeu wieder auf (elb_bewuchs.py, gleiche Ranken)
#    innen_trim_s23_elb: innen_trim_elb (Kalkputz, Dielen, heller Stein), Bodenfeld "lehmboden" links helle Steinfliesen, rechts
#                        Schachbrett aus hellem und graugruenem Stein (Kunstwerke innen: heller Steinboden)
#    blender -b --python tools/modelle/neubau/trimsheet_elb.py -- --quellen=<ordner> --steine=<ordner mit Bricks100> --nur=s23
EFEU_S2 = dict(ranken=2, saat=71, blatt_px=1.0)      # Efeu im Putz des Buergerhauses (2 Ranken auf 8 m Band)
EFEU_S3 = __import__("elb_bewuchs").EFEU_S3            # Efeu auf der Mauer des Steinhauses (dieselben Ranken in trimsheet_ebenen.py)
WERKSTEIN_K5 = (0.66, 0.60, 0.48)                     # Ton des Werksteins (K5) im Blatt, nur fuer die Vorschau in Blender


class QuelleAcg(Quelle):
    """Wie trimsheet.Quelle, aber aus den 1K-Dateien von ambientCG (K5 Bricks100: Color, NormalGL, AmbientOcclusion, Roughness); die
    Kachel (Breite, Hoehe in Metern) wird auf die Bildpunkte des Bands umgerechnet (`px_m` Bildpunkte je Meter)."""

    def __init__(self, ordner, name, kachel, px_m):
        import trimsheet as ts
        o = os.path.join(ordner, name)
        b, h = int(round(kachel[0] * px_m)), int(round(kachel[1] * px_m))
        self.farbe = ts.skaliere(ts.lade(os.path.join(o, f"{name}_1K-JPG_Color.jpg")), b, h)
        self.normale = ts.skaliere(ts.lade(os.path.join(o, f"{name}_1K-JPG_NormalGL.jpg"), daten=True), b, h)
        ao = ts.skaliere(ts.lade(os.path.join(o, f"{name}_1K-JPG_AmbientOcclusion.jpg"), daten=True), b, h)
        rau = ts.skaliere(ts.lade(os.path.join(o, f"{name}_1K-JPG_Roughness.jpg"), daten=True), b, h)
        self.arm = np.stack([ao[..., 0], rau[..., 0], np.zeros_like(ao[..., 0])], axis=-1)
        self.kante = b
        self.hoehe = h

    def streifen(self, hoehe, ab=0, versatz=0):
        zeilen = (ab + np.arange(hoehe)) % self.hoehe
        spalten = (versatz + np.arange(N)) % self.kante
        hole = lambda f: f[zeilen][:, spalten].copy()
        return hole(self.farbe), hole(self.normale), hole(self.arm)


def baue_s23(quellen, steine, stufen=(2, 3), innen=True):
    import elb_bewuchs
    import trimsheet_stufen as st
    B = BAENDER_AUSSEN
    schiefer = Quelle(quellen, "roof_slates_02", kante=1024)
    for stufe in stufen:
        blatt = st.lade_blatt("trim_elb")
        st.dach_band(blatt, schiefer, (0.38, 0.39, 0.42), kontrast=1.2, eigenfarbe=0.08)
        st.dach_aufhellen(blatt, st.SCHIEFER_HELL)
        for band in ("stroh", "stroh_franse"):
            moos(blatt, B[band], anteil=0.20)             # Moos sparsam auf dem Schiefer (wie die Schindeln der Stufe 1)
        if stufe in (2, 3):     # Stufe 3 (Patrizier, Nutzer 23.11.2026): ganze Waende in Kalkputz, Werkstein im Band „bruchstein“ (K2)
            # Putz neu und mit weniger Efeu: am Buergerhaus liegt das Band in vielen schmalen Gefachen und in zwei Lagen im Putzsockel –
            # die fuenf Ranken der Stufe 1 standen dort wie Streifen in jedem zweiten Gefach
            putz = Quelle(quellen, "white_plaster_rough_01")
            a, e = blatt.zeilen(B["putz"])
            f, n, arm = putz.streifen(e - a, ab=200)
            blatt.setze(B["putz"], faerbe(f, gespeichert(PUTZ), kontrast=0.75, eigenfarbe=0.1), n, arm)
            blatt.verlauf(B["putz"], 0.93, 0.80, 60)
            anteil = elb_bewuchs.efeu_auftragen(blatt.albedo, blatt.normal, blatt.orm, a, e, **EFEU_S2)
            print(f"Efeu im Putz: {anteil:.3f} der Flaeche")
        if stufe == 3:
            a, e = blatt.zeilen(B["bruchstein"])
            k2 = Quelle1k(quellen, "large_sandstone_blocks_01", kante=int(round(3.0 * N / B["bruchstein"][2])))   # Kachel 3 x 3 m
            f, n, arm = k2.streifen(e - a, ab=300)
            blatt.setze(B["bruchstein"], f, n, arm)
        blatt.speichere(f"trim_s{stufe}_elb")
    if not innen: return
    # Innen
    I = BAENDER_INNEN
    i = st.lade_blatt("innen_trim_elb")
    fliesen = Quelle(quellen, "large_floor_tiles_02", kante=1024)
    a, e = i.zeilen(I["lehmboden"])
    h = e - a
    f, n, arm = fliesen.streifen(h, ab=40)
    kachel_x, kachel_y = int(round(st.FLIESEN_U * N)) / 6, h / 2
    xx = np.arange(N)[None, :] % (N // 2)
    yy = np.arange(h)[:, None]
    kx = np.minimum(xx // kachel_x, 5).astype(int); ky = np.minimum(yy // kachel_y, 1).astype(int)
    fuge = (np.minimum(xx % kachel_x, kachel_x - xx % kachel_x) < 3) | (np.minimum(yy % kachel_y, kachel_y - yy % kachel_y) < 3)
    rechts = np.arange(N)[None, :] >= N // 2
    hell = faerbe(f, gespeichert((0.80, 0.78, 0.71)), kontrast=0.9, eigenfarbe=0.15)
    gruen = faerbe(f, gespeichert((0.60, 0.64, 0.58)), kontrast=0.9, eigenfarbe=0.15)
    links = faerbe(f, gespeichert((0.76, 0.73, 0.64)), kontrast=0.9, eigenfarbe=0.2)
    schach = np.where(((kx + ky) % 2 == 0)[..., None], hell, gruen)
    farbe = np.where(rechts[..., None], schach, links)
    zufall = np.random.default_rng(7).uniform(0.94, 1.05, (2, 6, 2))
    farbe = farbe * zufall[ky, kx, rechts.astype(int)][..., None]
    farbe = np.where(fuge[..., None], farbe * 0.55, farbe)
    arm = arm.copy(); arm[..., 0] = np.where(fuge, arm[..., 0] * 0.6, arm[..., 0])
    i.setze(I["lehmboden"], np.clip(farbe, 0, 1), n, arm)
    i.speichere("innen_trim_s23_elb")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict(a[2:].split("=", 1) for a in args if a.startswith("--") and "=" in a)
    nur = opt.get("nur", "")
    if nur in ("", "aussen"): baue_aussen(opt["quellen"])
    if nur in ("", "innen"): baue_innen(opt["quellen"])
    if nur in ("", "s23"): baue_s23(opt["quellen"], opt.get("steine"))
    if nur == "s3": baue_s23(opt["quellen"], None, stufen=(3,), innen=False)
