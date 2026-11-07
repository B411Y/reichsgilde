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

Mit --stufen entstehen sechs Stufen nach Stand (Umfärben über Farbverläufe nach der Helligkeit, Ringe, Goldnaht, Borte,
Schnalle aus Eisen, Silber oder Gold) als hand_<stufe>_normal|klick_<px>.png.

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

    def auf(m, dtyp=bool):
        c = np.zeros((groesse, groesse), dtyp)
        c[1:1 + th, 1:1 + tw] = m
        return c

    metall = e["metall"]
    masken = dict(
        deckend=inh, umriss=auf(umriss), linie=auf(linie), metall=auf(metall), cov_metall=auf(e["cov_metall"], np.float32),
        leder=auf(deckend & ~umriss & ~linie & ~metall),
    )
    return leinwand, pal, masken


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



# ---------------------------------------------------------------- Stufen nach Stand
# Lederfarben als Verlauf (dunkel, mittel, hell, Glanz); die Schattierung der Vorlage bleibt, nur der Farbton wechselt.
VERLAEUFE = {
    "bordeaux": [(34, 10, 15), (92, 25, 36), (156, 62, 68), (206, 134, 128)],
    "schwarz": [(15, 11, 14), (46, 37, 42), (96, 82, 86), (160, 146, 144)],
    "blau": [(16, 24, 56), (38, 64, 126), (96, 132, 196), (170, 198, 234)],
}
GOLD = [(118, 78, 20), (206, 154, 44), (246, 214, 104), (255, 240, 170)]
EISEN = [(46, 50, 54), (92, 96, 100), (150, 154, 156), (200, 204, 204)]
SILBER = [(104, 106, 106), (150, 152, 150), (200, 202, 198), (238, 240, 236)]
ACHSE = np.array([0.64, 0.77])  # Richtung Fingerspitze -> Stulpe (Bildkoordinaten)
QUER = np.array([-0.77, 0.64])
SPITZE80 = np.array([3.0, 1.0])  # Fingerspitze bei 80 px; Ringe sind in 80-px-Einheiten angegeben

# Stufe: (Lederverlauf oder None, Metallton Schnalle, Ringe, Naht, Umrissfarbe, Innenlinie)
# Ringe: ("finger", Abstand von der Fingerspitze in 80-px-Einheiten) oder ("daumen", Abstand von der Daumenspitze)
STUFEN = {
    1: dict(leder=None, schnalle=EISEN, ringe=[], naht=None, umriss=(26, 16, 12), linie=(48, 29, 20), abgenutzt=True),
    2: dict(leder=None, schnalle=SILBER, ringe=[], naht=None, umriss=(26, 16, 12), linie=(48, 29, 20)),
    3: dict(leder="bordeaux", schnalle=SILBER, ringe=[("finger", 13.5)], naht=None, umriss=(28, 10, 13), linie=(54, 20, 24)),
    4: dict(leder="bordeaux", schnalle=GOLD, ringe=[("finger", 13.5)], naht="naht", umriss=(28, 10, 13), linie=(54, 20, 24)),
    5: dict(leder="schwarz", schnalle=GOLD, ringe=[("finger", 10.5), ("finger", 15.5)], naht=None, umriss=(8, 6, 8), linie=(26, 20, 24)),
    6: dict(leder="blau", schnalle=GOLD, ringe=[("finger", 10.5), ("finger", 15.5), ("daumen", 7.5)], naht="borte", umriss=(12, 15, 30), linie=(22, 34, 70)),
}
DAUMEN80 = np.array([8.5, 29.0])  # Mitte der Daumenspitze bei 80 px
DAUMENACHSE = np.array([0.89, 0.45])


def verlauf(t, stuetzen):
    """Farbe an der Stelle t (0 bis 1) eines Verlaufs aus vier Stützfarben bei 0, 0,35, 0,75, 1."""
    st = np.array(stuetzen, np.float32)
    pos = np.array([0.0, 0.35, 0.75, 1.0])
    return np.stack([np.interp(t, pos, st[:, k]) for k in range(3)], axis=-1)


def pixelmaske(groesse, mitte, achse, dicke, quer_max):
    """Pixel, deren Mittelpunkt in einem Band senkrecht zur Achse liegt (Dicke in Pixeln, begrenzt in der Quere)."""
    yy, xx = np.mgrid[0:groesse, 0:groesse]
    q = np.stack([xx + 0.5, yy + 0.5], axis=-1) - mitte
    ent = q @ achse
    quer = q @ np.array([-achse[1], achse[0]])
    return (np.abs(ent) <= dicke / 2) & (np.abs(quer) <= quer_max), ent, quer


def stufe_bild(basis, m, stufe, groesse, klick=False, saat=7):
    """Baut aus dem braunen Grundbild die Stufe. basis: RGBA der Größe groesse, m: Masken von zusammensetzen."""
    cfg = STUFEN[stufe]
    b = basis.astype(np.float32)
    rgb = b[..., :3].copy()
    leder = m["leder"]
    l = lumi(rgb)
    lo, hi = np.percentile(l[leder], 1), np.percentile(l[leder], 99)
    t = np.clip((l - lo) / (hi - lo), 0, 1)
    if cfg["leder"]:
        rgb[leder] = verlauf(t[leder], VERLAEUFE[cfg["leder"]])
    if cfg.get("abgenutzt"):
        # stumpferes Leder: weniger Farbe, weniger Kontrast, leichte Scheuerstellen auf den Erhebungen
        grau = lumi(rgb)[..., None]
        stumpf = grau + (rgb - grau) * 0.85
        stumpf = stumpf * 0.9 + 20
        rgb[leder] = stumpf[leder]
        zu = np.random.RandomState(saat)
        helle = leder & (t > 0.55)
        zufall = zu.rand(*leder.shape)
        scheuer = helle & (zufall < 0.13 * groesse / 48)
        rgb[scheuer] = rgb[scheuer] * 0.5 + np.array([176, 150, 118]) * 0.5
        dunkel_fleck = leder & (t < 0.3) & (zu.rand(*leder.shape) < 0.05)
        rgb[dunkel_fleck] *= 0.82
    rgb[m["linie"]] = cfg["linie"]
    rgb[m["umriss"]] = cfg["umriss"]
    # Schnalle
    mt = m["metall"]
    if mt.any():
        tt = np.clip(m["cov_metall"][mt] * 1.8, 0, 1)
        rgb[mt] = verlauf(tt, cfg["schnalle"])

    # Ringe
    mask_hand = m["deckend"]
    sk = groesse / 80.0
    dicke = 1.2 + 0.018 * groesse
    aufgelegt = np.zeros_like(mask_hand)
    for ort, abstand in cfg["ringe"]:
        if ort == "finger":
            mitte = (SPITZE80 + ACHSE * abstand + QUER * 0.5) * sk
            achse, quer_max = ACHSE, 6.5 * sk
        else:
            mitte = (DAUMEN80 + DAUMENACHSE * abstand) * sk
            achse, quer_max = DAUMENACHSE, 4.2 * sk
        band, ent, quer = pixelmaske(groesse, mitte, achse, dicke, quer_max)
        band &= mask_hand
        if not band.any():
            continue
        ys, xs = np.where(band)
        # nur der zusammenhängende Teil beim Mittelpunkt (nicht ein Stück eines anderen Fingers)
        nah = (xs + 0.5 - mitte[0]) ** 2 + (ys + 0.5 - mitte[1]) ** 2 < (quer_max + 1) ** 2
        for y, x in zip(ys[nah], xs[nah]):
            d = ent[y, x] / dicke
            ton = 0.55 if d < -0.2 else (0.15 if d > 0.2 else 0.42)
            if m["umriss"][y, x]:
                ton = 0.02
            rgb[y, x] = verlauf(ton, GOLD)
            aufgelegt[y, x] = True
        # Glanzpixel: das hellste Pixel auf der lichtzugewandten Seite
        kand = [(ent[y, x], y, x) for y, x in zip(ys[nah], xs[nah]) if not m["umriss"][y, x]]
        if kand:
            kand.sort()
            _, gy, gx = kand[0]
            rgb[gy, gx] = GOLD[3]

    # Naht und Borte an der Stulpe: Streifen parallel zum Stulpenrand, 2 bis 3 Pixel hinter dem Umriss
    if cfg["naht"]:
        yy, xx = np.mgrid[0:groesse, 0:groesse]
        sw = (xx + 0.5) * ACHSE[0] + (yy + 0.5) * ACHSE[1]
        smax = sw[mask_hand].max()
        f = groesse / 48.0
        if cfg["naht"] == "naht":
            band = (sw >= smax - 3.4 * f) & (sw <= smax - 2.0 * f)
        else:
            band = (sw >= smax - 5.0 * f) & (sw <= smax - 2.0 * f)
        band &= m["leder"] | m["linie"]
        if cfg["naht"] == "naht":
            rgb[band] = verlauf(0.5, GOLD)
        else:
            qd = (xx + 0.5) * QUER[0] + (yy + 0.5) * QUER[1]
            muster = (np.floor(qd / (1.6 * f)).astype(int) % 2) == 0
            rgb[band & muster] = verlauf(0.45, GOLD)
            rgb[band & ~muster] = verlauf(0.1, GOLD)
            kante = band & (sw >= smax - 2.4 * f)
            rgb[kante & muster] = verlauf(0.8, GOLD)

    out = b.copy()
    out[..., :3] = np.clip(rgb, 0, 255)
    out = out.round().astype(np.uint8)
    if klick:
        out = klickzustand_stufe(out, m, cfg)
    return out


def klickzustand_stufe(bild, m, cfg):
    """Klick: wärmer und etwas heller; Umriss und Innenlinien bleiben, der Rand wird cremefarben."""
    b = bild.astype(np.float32)
    rgb = b[..., :3]
    neutral = ~(m["umriss"] | m["linie"])
    warm = rgb * np.array([1.10, 1.03, 0.90]) + np.array([12, 6, 0])
    warm = warm + np.maximum(lumi(rgb)[..., None] - 90, 0) * 0.10
    rgb = np.where((m["deckend"] & neutral)[..., None], warm, rgb)
    rand = (b[..., 3] > 0) & (b[..., 3] < 255)
    rgb = np.where(rand[..., None], np.array([248, 232, 196]), rgb)
    b[..., :3] = np.clip(rgb, 0, 255)
    return b.round().astype(np.uint8)


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
    ap.add_argument("--stufen", action="store_true", help="sechs Stufen nach Stand als hand_<stufe>_normal|klick_<px>.png schreiben")
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
        bild, pal, masken = zusammensetzen(e, g, farben, raster)
        if args.stufen:
            for st in range(1, 7):
                for kl in (False, True):
                    erg = stufe_bild(bild, masken, st, g, kl)
                    schreibe(os.path.join(args.ausgabe, f"hand_{st}_{'klick' if kl else 'normal'}_{g}.png"), erg)
        klick = klickzustand(bild, pal)
        if not args.stufen:
            schreibe(os.path.join(args.ausgabe, f"zeiger_{g}{args.nachsilbe}.png"), bild)
            schreibe(os.path.join(args.ausgabe, f"zeiger_{g}{args.nachsilbe}_klick.png"), klick)
        hx, hy = hotspot(bild)
        zeilen.append(f"{g}: {hx},{hy}")
        print(f"{g} px: Zeigerpunkt ({hx}, {hy}), Farben {len(np.unique(bild.reshape(-1, 4), axis=0))}")
    with open(os.path.join(args.ausgabe, "hotspots.txt"), "w") as f:
        f.write("\n".join(zeilen) + "\n")


if __name__ == "__main__":
    sys.exit(main())
