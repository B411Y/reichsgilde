"""Trim-Sheets fuer den Neubau der Gebaeude (Style-Guide Abschnitt 4 und 6): packt CC0-Texturen von Poly Haven zu Baendern.

    ~/werkzeuge/blender/blender.sh -b --python tools/modelle/neubau/trimsheet.py -- --quellen=<ordner> [--nur=aussen|innen]

<ordner> enthaelt je Poly-Haven-Name einen Unterordner mit `<name>_diff_2k.jpg`, `<name>_nor_gl_2k.jpg`, `<name>_arm_2k.jpg`
(von api.polyhaven.com/files/<name>, nie im Repository; Namen und Urheber in game/assets/texturen/neubau/HERKUNFT.md).
Laeuft in Blender, weil dort numpy und ein Bildlader dabei sind (kein Pillow noetig). Ergebnis je Blatt drei PNG (2048 x 2048):

    <blatt>_albedo.png  sRGB; Alpha nur im Fransenstreifen des Strohs (fuer die Alpha-Cards), sonst 1
    <blatt>_normal.png  Normale (OpenGL, wie Godot)
    <blatt>_orm.png     R Verdeckung, G Rauheit, B Metall

Die Baender laufen waagerecht und kacheln waagerecht (UV.x darf ueber 1 gehen). UV.y = 0 ist die oberste Bildzeile (wie in Godot).
Die Bandgrenzen stehen in BAENDER_AUSSEN / BAENDER_INNEN; das Hausskript (wohnhaus.py) liest sie von hier.
"""
import os
import sys

import bpy
import numpy as np

N = 2048
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
ZIEL = os.path.join(WURZEL, "game", "assets", "texturen", "neubau")

# Aussen wie Style-Guide 4.1. Unterstreifen (v0, v1) als Anteil der Bildhoehe; "breite" = Meter, die UV.x 0..1 bedeckt.
BAENDER_AUSSEN = {
    "stroh":        (0.0000, 0.1875, 4.0),   # Dachflaeche einer Stufe (oben dunkel unter der naechsten Stufe, unten heller)
    "stroh_franse": (0.1875, 0.2500, 4.0),   # Alpha-Cards: Halme mit ausgefranster Unterkante
    "balken_a":     (0.2500, 0.3333, 4.0),
    "balken_b":     (0.3333, 0.4167, 4.0),
    "balken_c":     (0.4167, 0.5000, 4.0),   # dunkler, verwittert (Schwellen, Sparrenkoepfe)
    "putz":         (0.5000, 0.7000, 8.0),
    "planken":      (0.7000, 0.8500, 4.0),   # senkrechte Bretter, Bandhoehe = Brettlaenge
    "bruchstein":   (0.8500, 0.9500, 4.0),
    "eisen":        (0.9500, 1.0000, 2.0),
}
# Innen: eigenes Blatt (der Guide legt nur "gemeinsames Interior-Trim-Sheet" fest, die Aufteilung ist hier gewaehlt).
BAENDER_INNEN = {
    "bohlen":     (0.00, 0.20, 4.0),   # Bretter laufen entlang UV.x (Boden, Tisch, Baenke, Galerie)
    "balken":     (0.20, 0.35, 4.0),
    "lehmwand":   (0.35, 0.55, 8.0),
    "lehmboden":  (0.55, 0.70, 4.0),
    "bruchstein": (0.70, 0.80, 4.0),
    "sackleinen": (0.80, 0.85, 2.0),
    "wolle":      (0.85, 0.90, 2.0),
    "stroh":      (0.90, 0.95, 2.0),
    "kleinteile": (0.95, 1.00, 1.0),   # acht Farbfelder nebeneinander, siehe FELDER_INNEN
}
FELDER_INNEN = ["ton", "eisen", "russ", "rinde", "zwiebel_rot", "zwiebel_gelb", "kraut", "glut"]


def srgb_zu_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_zu_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def lade(pfad, daten=False):
    """Bild als Feld [Zeile von oben, Spalte, Kanal] in 0..1 (sRGB-Werte bzw. Rohdaten)."""
    bild = bpy.data.images.load(pfad)
    if daten:
        bild.colorspace_settings.name = "Non-Color"
    b, h = bild.size
    feld = np.empty(b * h * 4, dtype=np.float32)
    bild.pixels.foreach_get(feld)
    feld = feld.reshape(h, b, 4)[::-1, :, :3].copy()
    if not daten:
        feld = linear_zu_srgb(feld)          # Blender liefert sRGB-Bilder linear
    bpy.data.images.remove(bild)
    return feld


def skaliere(feld, breite, hoehe):
    """Flaechenmittel beim Verkleinern (ganzzahliger Faktor), sonst naechster Nachbar."""
    h, b = feld.shape[:2]
    if b % breite == 0 and h % hoehe == 0:
        fx, fy = b // breite, h // hoehe
        return feld.reshape(hoehe, fy, breite, fx, -1).mean(axis=(1, 3))
    ys = (np.arange(hoehe) * h // hoehe)
    xs = (np.arange(breite) * b // breite)
    return feld[ys][:, xs]


def drehe(farbe, normale, arm):
    """Dreht um 90 Grad (Maserung senkrecht -> waagerecht); die Normale dreht mit."""
    f = np.rot90(farbe).copy(); a = np.rot90(arm).copy(); n = np.rot90(normale).copy()
    x = n[..., 0] * 2 - 1; y = n[..., 1] * 2 - 1
    n[..., 0] = (-y) * 0.5 + 0.5
    n[..., 1] = x * 0.5 + 0.5
    return f, n, a


class Quelle:
    def __init__(self, ordner, name, kante=1024, gedreht=False):
        o = os.path.join(ordner, name)
        self.farbe = skaliere(lade(os.path.join(o, f"{name}_diff_2k.jpg")), kante, kante)
        self.normale = skaliere(lade(os.path.join(o, f"{name}_nor_gl_2k.jpg"), daten=True), kante, kante)
        self.arm = skaliere(lade(os.path.join(o, f"{name}_arm_2k.jpg"), daten=True), kante, kante)
        if gedreht:
            self.farbe, self.normale, self.arm = drehe(self.farbe, self.normale, self.arm)
        self.kante = kante

    def streifen(self, hoehe, ab=0, versatz=0):
        """Streifen voller Blattbreite: waagerecht gekachelt (nahtlos), senkrecht ein Ausschnitt ab Zeile `ab`."""
        zeilen = (ab + np.arange(hoehe)) % self.kante
        spalten = (versatz + np.arange(N)) % self.kante
        hole = lambda f: f[zeilen][:, spalten].copy()
        return hole(self.farbe), hole(self.normale), hole(self.arm)


def faerbe(farbe, ziel_srgb, kontrast=1.0, eigenfarbe=0.25):
    """Faerbt auf eine Zielfarbe um und behaelt die Zeichnung: Helligkeit relativ zum Mittel, dazu ein Rest der eigenen Farbigkeit."""
    lin = srgb_zu_linear(farbe)
    mittel = lin.reshape(-1, 3).mean(axis=0)
    hell = lin @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    rel = (hell / max(float(hell.mean()), 1e-4)) ** kontrast
    ziel = srgb_zu_linear(np.array(ziel_srgb, dtype=np.float32))
    grau = ziel[None, None, :] * rel[..., None]
    bunt = ziel[None, None, :] * (lin / np.maximum(mittel, 1e-4)[None, None, :]) ** kontrast
    return linear_zu_srgb(grau * (1 - eigenfarbe) + bunt * eigenfarbe)


def rauschen(breite, saat, grob=8):
    """Waagerecht nahtloses, weiches Zufallsprofil 0..1 (fuer Fransen und Flecken)."""
    z = np.random.default_rng(saat)
    stuetzen = z.random(breite // grob)
    x = np.arange(breite) / grob
    i = np.floor(x).astype(int); t = x - i; t = t * t * (3 - 2 * t)
    return stuetzen[i % len(stuetzen)] * (1 - t) + stuetzen[(i + 1) % len(stuetzen)] * t


class Blatt:
    def __init__(self):
        self.albedo = np.zeros((N, N, 4), dtype=np.float32); self.albedo[..., 3] = 1
        self.normal = np.zeros((N, N, 3), dtype=np.float32); self.normal[...] = (0.5, 0.5, 1.0)
        self.orm = np.zeros((N, N, 3), dtype=np.float32); self.orm[...] = (1.0, 0.9, 0.0)

    def zeilen(self, band):
        return int(round(band[0] * N)), int(round(band[1] * N))

    def setze(self, band, farbe, normale, arm, rauheit=None, metall=0.0, ao_staerke=1.0, x0=0, x1=N):
        a, e = self.zeilen(band)
        self.albedo[a:e, x0:x1, :3] = farbe[:, x0:x1]
        self.normal[a:e, x0:x1] = normale[:, x0:x1]
        self.orm[a:e, x0:x1, 0] = 1 - (1 - arm[:, x0:x1, 0]) * ao_staerke
        self.orm[a:e, x0:x1, 1] = arm[:, x0:x1, 1] if rauheit is None else rauheit
        self.orm[a:e, x0:x1, 2] = metall

    def verlauf(self, band, oben, unten, breite_px):
        """Dunkelt die Raender eines Streifens ab (gemalte Kante; gleichzeitig Verdeckung)."""
        a, e = self.zeilen(band)
        h = e - a
        y = np.arange(h, dtype=np.float32)
        f = np.ones(h, dtype=np.float32)
        if oben < 1: f = np.minimum(f, oben + (1 - oben) * np.clip(y / breite_px, 0, 1))
        if unten < 1: f = np.minimum(f, unten + (1 - unten) * np.clip((h - 1 - y) / breite_px, 0, 1))
        lin = srgb_zu_linear(self.albedo[a:e, :, :3]) * f[:, None, None]
        self.albedo[a:e, :, :3] = linear_zu_srgb(lin)
        self.orm[a:e, :, 0] *= (0.5 + 0.5 * f)[:, None]

    def fase(self, band, px=10, staerke=0.55):
        """Abgefaste Balkenkante: Normale kippt am oberen und unteren Rand nach aussen."""
        a, e = self.zeilen(band)
        for i in range(px):
            k = staerke * (1 - i / px)
            self.normal[a + i, :, 1] = np.clip(self.normal[a + i, :, 1] + k * 0.5, 0, 1)
            self.normal[e - 1 - i, :, 1] = np.clip(self.normal[e - 1 - i, :, 1] - k * 0.5, 0, 1)

    def speichere(self, name):
        os.makedirs(ZIEL, exist_ok=True)
        for art, feld, farbraum in (("albedo", self.albedo, "sRGB"), ("normal", self.normal, "Non-Color"), ("orm", self.orm, "Non-Color")):
            kan = feld.shape[2]
            bild = bpy.data.images.new(f"{name}_{art}", N, N, alpha=(kan == 4))
            bild.colorspace_settings.name = farbraum
            voll = np.ones((N, N, 4), dtype=np.float32)
            werte = feld[..., :3]
            voll[..., :3] = srgb_zu_linear(werte) if farbraum == "sRGB" else werte
            if kan == 4: voll[..., 3] = feld[..., 3]
            bild.pixels.foreach_set(voll[::-1].ravel())
            bild.filepath_raw = os.path.join(ZIEL, f"{name}_{art}.png")
            bild.file_format = "PNG"
            szene = bpy.context.scene
            szene.render.image_settings.file_format = "PNG"
            szene.render.image_settings.color_mode = "RGBA" if kan == 4 else "RGB"
            szene.render.image_settings.color_depth = "8"
            szene.render.image_settings.compression = 90
            szene.view_settings.view_transform = "Standard"
            bild.save_render(bild.filepath_raw, scene=szene) if farbraum == "sRGB" else bild.save()
            print("geschrieben", bild.filepath_raw)
            bpy.data.images.remove(bild)


def stroh_band(blatt, q, band_flaeche, band_franse, farbe=(0.74, 0.66, 0.46)):
    # Dachflaeche: Halme senkrecht; oben Schatten der ueberlappenden Stufe, unten helle, frisch geschnittene Kante
    a, e = blatt.zeilen(band_flaeche)
    f, n, arm = q.streifen(e - a, ab=100)
    blatt.setze(band_flaeche, faerbe(f, farbe, kontrast=1.15), n, arm, ao_staerke=1.0)
    blatt.verlauf(band_flaeche, 0.62, 1.0, 110)
    lin = srgb_zu_linear(blatt.albedo[a:e, :, :3])
    y = np.arange(e - a, dtype=np.float32)[:, None, None]
    hell = 1.0 + 0.22 * np.clip((y - (e - a - 90)) / 90, 0, 1)          # Kante unten heller
    # unregelmaessige Lagen: weiche dunkle Querstreifen, waagerecht nahtlos
    for k, (mitte, tiefe) in enumerate(((0.38, 0.14), (0.68, 0.10))):
        welle = (rauschen(N, 11 + k, 64) - 0.5) * 46
        d = np.abs(np.arange(e - a, dtype=np.float32)[:, None] - (mitte * (e - a) + welle[None, :]))
        hell = hell * (1 - tiefe * np.exp(-(d / 7.0) ** 2))[..., None]
    blatt.albedo[a:e, :, :3] = linear_zu_srgb(lin * hell)
    # Fransenstreifen: gleiche Halme, unten ausgefranst (Alpha)
    a2, e2 = blatt.zeilen(band_franse)
    h2 = e2 - a2
    f, n, arm = q.streifen(h2, ab=500, versatz=300)
    blatt.setze(band_franse, faerbe(f, (farbe[0] * 0.92, farbe[1] * 0.9, farbe[2] * 0.85), kontrast=1.25), n, arm)
    grob = rauschen(N, 3, 48); fein = rauschen(N, 4, 6); halm = rauschen(N, 5, 2)
    laenge = h2 * (0.42 + 0.30 * grob + 0.18 * fein + 0.10 * halm)       # Laenge der Halme je Spalte
    yy = np.arange(h2, dtype=np.float32)[:, None]
    blatt.albedo[a2:e2, :, 3] = (yy < laenge[None, :]).astype(np.float32)
    lin = srgb_zu_linear(blatt.albedo[a2:e2, :, :3])
    blatt.albedo[a2:e2, :, :3] = linear_zu_srgb(lin * (1.0 - 0.35 * (yy / h2))[..., None])   # Spitzen dunkler


def baue_aussen(quellen):
    B = BAENDER_AUSSEN
    blatt = Blatt()
    stroh_band(blatt, Quelle(quellen, "thatch_roof_angled"), B["stroh"], B["stroh_franse"])
    holz = Quelle(quellen, "rough_wood", gedreht=True)
    for i, (name, ton, kontrast) in enumerate((("balken_a", (0.40, 0.30, 0.22), 1.0), ("balken_b", (0.37, 0.28, 0.21), 1.1), ("balken_c", (0.30, 0.23, 0.18), 1.2))):
        a, e = blatt.zeilen(B[name])
        f, n, arm = holz.streifen(e - a, ab=120 + 290 * i, versatz=370 * i)
        blatt.setze(B[name], faerbe(f, ton, kontrast=kontrast, eigenfarbe=0.2), n, arm)
        blatt.verlauf(B[name], 0.6, 0.6, 9)
        blatt.fase(B[name])
    putz = Quelle(quellen, "white_plaster_rough_01")
    a, e = blatt.zeilen(B["putz"])
    f, n, arm = putz.streifen(e - a, ab=200)
    blatt.setze(B["putz"], faerbe(f, (0.87, 0.84, 0.77), kontrast=0.75, eigenfarbe=0.1), n, arm)
    blatt.verlauf(B["putz"], 0.93, 0.80, 60)              # unten Spritzwasser, oben leichter Schatten
    planken = Quelle(quellen, "old_planks_02", kante=512)
    a, e = blatt.zeilen(B["planken"])
    f, n, arm = planken.streifen(e - a, ab=512 - (e - a))  # unterer Teil mit den Nagelreihen
    blatt.setze(B["planken"], faerbe(f, (0.50, 0.42, 0.34), kontrast=1.1, eigenfarbe=0.3), n, arm)
    stein = Quelle(quellen, "stone_wall")
    a, e = blatt.zeilen(B["bruchstein"])
    f, n, arm = stein.streifen(e - a, ab=380)
    blatt.setze(B["bruchstein"], faerbe(f, (0.52, 0.50, 0.46), kontrast=1.0, eigenfarbe=0.45), n, arm)
    eisen = Quelle(quellen, "rusty_metal_02")
    a, e = blatt.zeilen(B["eisen"])
    f, n, arm = eisen.streifen(e - a, ab=0)
    blatt.setze(B["eisen"], faerbe(f, (0.16, 0.15, 0.15), kontrast=0.5, eigenfarbe=0.25), n, arm, rauheit=0.62, metall=1.0)
    blatt.speichere("trim")


def baue_innen(quellen):
    B = BAENDER_INNEN
    blatt = Blatt()

    def band(name, q, ton, ab=0, versatz=0, kontrast=1.0, eigenfarbe=0.3, **kw):
        a, e = blatt.zeilen(B[name])
        f, n, arm = q.streifen(e - a, ab=ab, versatz=versatz)
        blatt.setze(B[name], faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe), n, arm, **kw)

    band("bohlen", Quelle(quellen, "old_planks_02", gedreht=True), (0.53, 0.43, 0.32), ab=60)
    holz = Quelle(quellen, "rough_wood", gedreht=True)
    band("balken", holz, (0.43, 0.33, 0.25), ab=300, kontrast=1.1, eigenfarbe=0.2)
    band("lehmwand", Quelle(quellen, "white_plaster_rough_01"), (0.76, 0.70, 0.58), ab=100, kontrast=0.8, eigenfarbe=0.1)
    blatt.verlauf(B["lehmwand"], 0.9, 0.78, 50)
    band("lehmboden", Quelle(quellen, "brown_mud_dry"), (0.50, 0.42, 0.32), ab=0, kontrast=0.7, eigenfarbe=0.3)
    band("bruchstein", Quelle(quellen, "stone_wall"), (0.47, 0.45, 0.42), ab=380, eigenfarbe=0.45)
    band("sackleinen", Quelle(quellen, "hessian_230", kante=512), (0.60, 0.50, 0.33), kontrast=1.0)
    band("wolle", Quelle(quellen, "knitted_fleece", kante=512), (0.40, 0.36, 0.33), kontrast=1.6)
    band("stroh", Quelle(quellen, "reed_roof_03", kante=1024), (0.64, 0.57, 0.40), ab=560, kontrast=1.1)
    # Kleinteile: acht Farbfelder
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
    blatt.speichere("innen_trim")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict(a[2:].split("=", 1) for a in args if a.startswith("--") and "=" in a)
    if opt.get("nur", "aussen") == "aussen" or "nur" not in opt:
        baue_aussen(opt["quellen"])
    if opt.get("nur", "innen") == "innen" or "nur" not in opt:
        baue_innen(opt["quellen"])
