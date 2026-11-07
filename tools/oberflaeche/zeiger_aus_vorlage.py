#!/usr/bin/env python3
"""Mauszeiger aus einer gezeichneten Handschuh-Vorlage: feine Pixel-Art in mehreren Größen.

Aufruf (nutzt nur numpy und OpenImageIO; beides liegt im Python von Blender, kein Blender-Start nötig):

    ~/werkzeuge/blender/blender-5.2.2-linux-x64/5.2/python/bin/python3.13 tools/oberflaeche/zeiger_aus_vorlage.py \
        VORLAGE.jpg AUSGABEORDNER [--groessen 32,48,64,80] [--verfahren linien|flaeche]

Schritte je Größe:
 1. Vorlage freistellen: weißer Grund wird von den Bildrändern aus weggefüllt, der Rand um 2 Pixel zurückgenommen (kein heller Saum).
 2. Auf die Zielgröße verkleinern mit Flächenmittel; dabei getrennt gezählt: wie viel der Blockfläche ist Handschuh,
    wie viel davon dunkle Linie (Umriss, Riemenkanten, Falten) und welche Farbe hat das Leder ohne die Linien.
 3. Pixel setzen: Alpha hart (0 oder 255), Umriss als 1 Pixel dunkle Kontur neu gezogen, Linien im Inneren nur dort,
    wo sie im Block genug Fläche hatten, Leder mit leichtem Schärfen und Kontrast.
 4. Farben auf eine kleine Lederpalette (k-means) begrenzen, optional mit sehr leichtem Raster gegen Banding.
 5. Hellen Außenrand (1 Pixel) setzen. Klickzustand = wärmere, hellere Palette.

Herkunft der Vorlage: Nutzerzeichnung, mit Gemini überarbeitet (Teamleiter, 07.11.2026). Erst nach Freigabe nach game/assets legen,
dann mit Protokollzeile.

Ausgabe: zeiger_<px>.png, zeiger_<px>_klick.png, hotspots.txt (Fingerspitze = deckendes Pixel mit kleinstem x+y).
"""
import argparse
import os
import sys

import numpy as np
import OpenImageIO as oiio

UMRISS = np.array([26, 16, 12], np.float32)
INNENLINIE = np.array([48, 29, 20], np.float32)
RAND = (236, 226, 204, 205)


# ---------------------------------------------------------------- Ein-/Ausgabe
def lies(pfad):
    puffer = oiio.ImageBuf(pfad)
    return puffer.get_pixels(oiio.UINT8)


def schreibe(pfad, a):
    a = np.ascontiguousarray(a, dtype=np.uint8)
    puffer = oiio.ImageBuf(oiio.ImageSpec(a.shape[1], a.shape[0], a.shape[2], oiio.UINT8))
    puffer.set_pixels(oiio.ROI(), a)
    puffer.write(pfad)


# ---------------------------------------------------------------- Freistellen
def verbreitern(m, n=1):
    for _ in range(n):
        p = np.pad(m, 1)
        m = p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    return m


def verengen(m, n=1):
    return ~verbreitern(~m, n)


def freistellen(rgb):
    """Gibt (rgb, maske) zurück, beschnitten auf den Handschuh. Maske True = Handschuh."""
    hell = rgb.min(axis=2) > 215
    nichthell = ~hell
    ys, xs = np.where(nichthell)
    y0, y1, x0, x1 = max(ys.min() - 6, 0), min(ys.max() + 7, rgb.shape[0]), max(xs.min() - 6, 0), min(xs.max() + 7, rgb.shape[1])
    # Bildrandpixel vom Rauschen freihalten: Auffüllen vom Rand der Beschneidung aus
    rgb = rgb[y0:y1, x0:x1]
    hell = hell[y0:y1, x0:x1]
    grund = np.zeros_like(hell)
    grund[0, :] = hell[0, :]
    grund[-1, :] = hell[-1, :]
    grund[:, 0] = hell[:, 0]
    grund[:, -1] = hell[:, -1]
    while True:
        neu = verbreitern(grund) & hell
        if (neu == grund).all():
            break
        grund = neu
    maske = ~grund
    # Löcher im Inneren, die nicht zum Außengrund gehören, bleiben Handschuh (sie sind es schon).
    maske = verengen(maske, 2)
    ys, xs = np.where(maske)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    return rgb[y0:y1, x0:x1].astype(np.float32), maske[y0:y1, x0:x1]


# ---------------------------------------------------------------- Verkleinern
def gewichte(quelle, ziel):
    """Matrix ziel x quelle: Flächenmittel (Kastenfilter mit Teilüberdeckung)."""
    w = np.zeros((ziel, quelle), np.float32)
    schritt = quelle / ziel
    for i in range(ziel):
        a, b = i * schritt, (i + 1) * schritt
        j0, j1 = int(np.floor(a)), int(np.ceil(b))
        for j in range(j0, min(j1, quelle)):
            w[i, j] = min(b, j + 1) - max(a, j)
    return w / w.sum(axis=1, keepdims=True)


def mittel(a, wy, wx):
    """Wendet das Flächenmittel auf ein 2D- oder 3D-Feld an."""
    if a.ndim == 2:
        return wy @ a @ wx.T
    return np.stack([wy @ a[..., k] @ wx.T for k in range(a.shape[2])], axis=2)


def kastenweichzeichnen(a, maske):
    """3x3-Mittel nur über Maskenpixel (a: HxWx3)."""
    m = maske.astype(np.float32)
    p = np.pad(a * m[..., None], ((1, 1), (1, 1), (0, 0)))
    pm = np.pad(m, 1)
    s = sum(p[dy:dy + a.shape[0], dx:dx + a.shape[1]] for dy in range(3) for dx in range(3))
    n = sum(pm[dy:dy + a.shape[0], dx:dx + a.shape[1]] for dy in range(3) for dx in range(3))
    return s / np.maximum(n, 1e-6)[..., None]


def lumi(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def verkleinern(rgb, maske, groesse, verfahren, farben, schaerfe, kontrast, dunkel_schwelle, linien_flaeche, innen_flaeche, raster, extrem=0.0, metall_flaeche=0.1):
    h, w = maske.shape
    innen = groesse - 2  # 1 Pixel Rand rundum für den hellen Außenrand
    k = innen / max(w, h)
    th, tw = max(1, round(h * k)), max(1, round(w * k))
    wy, wx = gewichte(h, th), gewichte(w, tw)

    fg = maske.astype(np.float32)
    dunkel = (lumi(rgb) < dunkel_schwelle) & maske
    leder = maske & ~dunkel
    cov = mittel(fg, wy, wx)
    cov_dunkel = mittel(dunkel.astype(np.float32), wy, wx)
    cov_leder = mittel(leder.astype(np.float32), wy, wx)
    farbe_leder = mittel(rgb * leder[..., None], wy, wx) / np.maximum(cov_leder, 1e-6)[..., None]
    farbe_alle = mittel(rgb * maske[..., None], wy, wx) / np.maximum(cov, 1e-6)[..., None]

    deckend = cov > 0.5
    if verfahren == "flaeche":
        farbe = farbe_alle.copy()
    else:
        farbe = np.where((cov_leder > 0.08)[..., None], farbe_leder, farbe_alle)

    # Lichter und Tiefen erhalten: auf 3x3 Teilpixel je Zielpixel verkleinern und den Teilpixel nehmen,
    # der am weitesten vom Blockmittel liegt; ein Teil davon fließt in die Farbe ein
    if extrem > 0:
        wy3, wx3 = gewichte(h, 3 * th), gewichte(w, 3 * tw)
        c3 = mittel(leder.astype(np.float32), wy3, wx3)
        f3 = mittel(rgb * leder[..., None], wy3, wx3) / np.maximum(c3, 1e-6)[..., None]
        gut = (c3 > 0.5).reshape(th, 3, tw, 3).transpose(0, 2, 1, 3).reshape(th, tw, 9)
        f3 = f3.reshape(th, 3, tw, 3, 3).transpose(0, 2, 1, 3, 4).reshape(th, tw, 9, 3)
        l3 = lumi(f3)
        ml = lumi(farbe)
        abst = np.abs(l3 - ml[..., None]) + np.where(gut, 0, -1e3)
        i = abst.argmax(axis=2)
        ext = np.take_along_axis(f3, i[..., None, None], axis=2)[:, :, 0, :]
        ok = gut.any(axis=2)[..., None]
        farbe = np.where(ok, farbe + extrem * (ext - farbe), farbe)

    # Schärfen und Kontrast nur im Handschuh
    if schaerfe > 0:
        weich = kastenweichzeichnen(farbe, deckend)
        farbe = farbe + schaerfe * (farbe - weich)
    if kontrast != 1.0:
        mitte = farbe[deckend].mean(axis=0)
        farbe = mitte + (farbe - mitte) * kontrast
    farbe = np.clip(farbe, 0, 255)

    # Linien
    umriss = deckend & ~verengen(deckend, 1)
    linie = np.zeros_like(deckend)
    if verfahren != "flaeche":
        # Innenlinien: Blockfläche mit dunklem Anteil über Schwelle, aber nur lokale Rücken (kein breites Band)
        lok = cov_dunkel
        kandidat = deckend & ~umriss & (lok > innen_flaeche)
        # dünnen: Pixel nur, wenn größer oder gleich beider Nachbarn in x oder y
        p = np.pad(lok, 1)
        gx = (lok >= p[1:-1, :-2]) & (lok >= p[1:-1, 2:])
        gy = (lok >= p[:-2, 1:-1]) & (lok >= p[2:, 1:-1])
        linie = kandidat & (gx | gy)
        # Pixel direkt neben dem Umriss werden nicht zur Linie, sonst wird der Rand 2 Pixel dick
        nah = verbreitern(umriss, 1)
        linie &= ~nah
    # Metall (Schnalle): hell und kaum bunt; schon wenig Fläche reicht für einen silbernen Pixel
    metall_hires = maske & (lumi(rgb) > 110) & ((rgb.max(axis=2) - rgb.min(axis=2)) < 38)
    cov_metall = mittel(metall_hires.astype(np.float32), wy, wx)
    metall = deckend & ~umriss & (cov_metall > metall_flaeche)
    return dict(farbe=farbe, deckend=deckend, umriss=umriss, linie=linie, th=th, tw=tw, cov_dunkel=cov_dunkel, metall=metall, cov_metall=cov_metall)


# ---------------------------------------------------------------- Palette
def kmeans(daten, k, gewicht=None, runden=20):
    """Deterministisches k-means im RGB-Raum; Startpunkte aus Helligkeitsquantilen."""
    reihenfolge = np.argsort(lumi(daten))
    idx = reihenfolge[((np.arange(k) + 0.5) / k * len(daten)).astype(int)]
    mitten = daten[idx].copy()
    for _ in range(runden):
        d = ((daten[:, None, :] - mitten[None]) ** 2 * np.array([0.9, 1.0, 0.8])).sum(axis=2)
        z = d.argmin(axis=1)
        for i in range(k):
            sel = z == i
            if sel.any():
                mitten[i] = daten[sel].mean(axis=0)
    return mitten


def bayer(n):
    m = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], np.float32)
    m = (m + 0.5) / 16 - 0.5
    return np.tile(m, (n // 4 + 1, n // 4 + 1))[:n, :n]


def zusammensetzen(e, groesse, farben, raster):
    farbe, deckend, umriss, linie, th, tw = e["farbe"], e["deckend"], e["umriss"], e["linie"], e["th"], e["tw"]
    pal = kmeans(farbe[deckend & ~umriss & ~linie], farben)
    pal = pal[np.argsort(lumi(pal))]
    f = farbe.copy()
    if raster > 0:
        f = f + (bayer(max(th, tw))[:th, :tw] * raster)[..., None]
    d = ((f[:, :, None, :] - pal[None, None]) ** 2 * np.array([0.9, 1.0, 0.8])).sum(axis=3)
    q = pal[d.argmin(axis=2)]
    # Umriss: Farbton etwas vom Leder übernehmen, damit er nicht tot schwarz wirkt
    rgb = q.copy()
    rgb[linie] = INNENLINIE
    rgb[e["metall"]] = np.clip(np.array([150, 152, 150]) + e["cov_metall"][e["metall"]][:, None] * 160, 0, 235)
    rgb[umriss] = UMRISS
    # auf Leinwand setzen
    leinwand = np.zeros((groesse, groesse, 4), np.uint8)
    inh = np.zeros((groesse, groesse), bool)
    inh[1:1 + th, 1:1 + tw] = deckend
    kol = np.zeros((groesse, groesse, 3), np.float32)
    kol[1:1 + th, 1:1 + tw] = rgb
    leinwand[..., :3] = kol.round().astype(np.uint8)
    leinwand[..., 3] = np.where(inh, 255, 0)
    # heller Außenrand (8 Nachbarn)
    p = np.pad(inh, 1)
    rand = np.zeros_like(inh)
    for dy in range(3):
        for dx in range(3):
            rand |= p[dy:dy + groesse, dx:dx + groesse]
    rand &= ~inh
    leinwand[rand] = RAND
    return leinwand, pal


def klickzustand(bild, pal):
    """Wärmer und heller: Rot rauf, Blau runter, Lichter etwas heller (nur Leder, Umrisse bleiben)."""
    b = bild.astype(np.float32)
    rgb = b[..., :3]
    l = lumi(rgb)[..., None]
    nicht_dunkel = l > 70
    warm = rgb * np.array([1.12, 1.03, 0.86]) + np.array([14, 6, -2])
    hell = warm + np.maximum(l - 90, 0) * 0.12
    rgb2 = np.where(nicht_dunkel, hell, rgb * np.array([1.05, 1.0, 0.95]))
    ist_rand = (b[..., 3] > 0) & (b[..., 3] < 255)
    rgb2 = np.where(ist_rand[..., None], np.array([248, 232, 196]), rgb2)
    b[..., :3] = np.clip(rgb2, 0, 255)
    return b.round().astype(np.uint8)


def hotspot(bild):
    ys, xs = np.where(bild[..., 3] == 255)
    i = (xs + ys).argmin()
    return int(xs[i]), int(ys[i])


# ---------------------------------------------------------------- Hauptprogramm
STANDARD = {
    # groesse: (farben, schaerfe, kontrast, dunkel_schwelle, linien_flaeche, innen_flaeche, raster)
    32: (20, 0.8, 1.15, 45, 0.4, 0.46, 0.0),
    48: (28, 0.8, 1.15, 45, 0.4, 0.40, 0.0),
    64: (32, 0.8, 1.12, 45, 0.4, 0.36, 0.0),
    80: (40, 0.8, 1.10, 45, 0.4, 0.32, 0.0),
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("vorlage")
    ap.add_argument("ausgabe")
    ap.add_argument("--groessen", default="32,48,64,80")
    ap.add_argument("--verfahren", default="linien", choices=["linien", "flaeche"])
    ap.add_argument("--nachsilbe", default="")
    ap.add_argument("--innen", type=float, help="Schwelle für Innenlinien (überschreibt die Standardwerte)")
    ap.add_argument("--schwelle", type=float, help="Helligkeit, unter der ein Vorlagenpixel als Linie zählt")
    ap.add_argument("--schaerfe", type=float)
    ap.add_argument("--kontrast", type=float)
    ap.add_argument("--raster", type=float)
    ap.add_argument("--metall", type=float, default=0.08, help="Mindestfläche Metall je Pixel für die Schnalle")
    ap.add_argument("--extrem", type=float, default=0.0, help="Anteil der Lichter/Tiefen aus Teilpixeln (0 bis 1)")
    args = ap.parse_args()
    os.makedirs(args.ausgabe, exist_ok=True)
    rgb, maske = freistellen(lies(args.vorlage)[..., :3])
    print(f"Vorlage freigestellt: {maske.shape[1]} x {maske.shape[0]} Pixel")
    zeilen = []
    for g in [int(x) for x in args.groessen.split(",")]:
        farben, schaerfe, kontrast, dunkel, lf, inf, raster = STANDARD[g]
        inf = args.innen if args.innen is not None else inf
        dunkel = args.schwelle if args.schwelle is not None else dunkel
        schaerfe = args.schaerfe if args.schaerfe is not None else schaerfe
        kontrast = args.kontrast if args.kontrast is not None else kontrast
        raster = args.raster if args.raster is not None else raster
        e = verkleinern(rgb, maske, g, args.verfahren, farben, schaerfe, kontrast, dunkel, lf, inf, raster, args.extrem, args.metall)
        bild, pal = zusammensetzen(e, g, farben, raster)
        klick = klickzustand(bild, pal)
        schreibe(os.path.join(args.ausgabe, f"zeiger_{g}{args.nachsilbe}.png"), bild)
        schreibe(os.path.join(args.ausgabe, f"zeiger_{g}{args.nachsilbe}_klick.png"), klick)
        hx, hy = hotspot(bild)
        zeilen.append(f"{g}: {hx},{hy}")
        print(f"{g} px: Zeigerpunkt ({hx}, {hy}), Farben {len(np.unique(bild.reshape(-1, 4), axis=0))}")
    with open(os.path.join(args.ausgabe, "hotspots.txt"), "w") as f:
        f.write("\n".join(zeilen) + "\n")


if __name__ == "__main__":
    sys.exit(main())
