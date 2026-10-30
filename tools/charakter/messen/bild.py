"""Bilder lesen und rechnen (Charakter Phase 6, S0): OpenImageIO und numpy aus Blenders Python, ohne PIL und scipy.

Alle Bilder als float32-Feld (Höhe, Breite, 3) mit sRGB-Werten 0..1 (so wie Godot sie speichert).
"""
import os

import numpy as np
import OpenImageIO as oiio


def lesen(pfad):
    pfad = os.path.expanduser(pfad)
    b = oiio.ImageBuf(pfad)
    if b.has_error:
        raise IOError(f"{pfad}: {b.geterror()}")
    a = b.get_pixels(oiio.FLOAT)
    if a.ndim == 2:
        a = a[:, :, None]
    if a.shape[2] == 1:
        a = np.repeat(a, 3, axis=2)
    return np.ascontiguousarray(a[:, :, :3], dtype=np.float32)


def schreiben(pfad, a):
    a = np.clip(np.asarray(a, dtype=np.float32), 0.0, 1.0)
    if a.ndim == 2:
        a = a[:, :, None]
    h, w, c = a.shape
    buf = oiio.ImageBuf(oiio.ImageSpec(w, h, c, oiio.UINT8))
    buf.set_pixels(oiio.ROI(0, w, 0, h, 0, 1, 0, c), a)
    if not buf.write(os.path.expanduser(pfad)):
        raise IOError(buf.geterror())


def skalieren(a, faktor):
    """Bild um einen Faktor skalieren (Lanczos über OpenImageIO)."""
    h, w = a.shape[:2]
    nb, nh = max(1, round(w * faktor)), max(1, round(h * faktor))
    buf = oiio.ImageBuf(oiio.ImageSpec(w, h, a.shape[2], oiio.FLOAT))
    buf.set_pixels(oiio.ROI(0, w, 0, h, 0, 1, 0, a.shape[2]), a.astype(np.float32))
    r = oiio.ImageBufAlgo.resize(buf, filtername="lanczos3", roi=oiio.ROI(0, nb, 0, nh, 0, 1, 0, a.shape[2]))
    return r.get_pixels(oiio.FLOAT)


def linear(s):
    s = np.clip(np.asarray(s, dtype=np.float64), 0.0, None)     # Lanczos kann leicht unter 0 schwingen
    return np.where(s <= 0.04045, s / 12.92, ((s + 0.055) / 1.055) ** 2.4)


def srgb(l):
    l = np.asarray(l, dtype=np.float64)
    return np.where(l <= 0.0031308, l * 12.92, 1.055 * np.power(np.maximum(l, 0), 1 / 2.4) - 0.055)


def leuchtdichte(s):
    """Relative Leuchtdichte Y (linear, D65) aus sRGB."""
    l = linear(s)
    return l[..., 0] * 0.2126 + l[..., 1] * 0.7152 + l[..., 2] * 0.0722


def lab(s):
    """CIELAB (D65) aus sRGB (…, 3)."""
    l = linear(s)
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = l @ M.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def gauss(a, sigma):
    """Gauß-Weichzeichner (trennbar, Rand gespiegelt) für 2D-Felder."""
    if sigma <= 0:
        return a.astype(np.float64)
    r = max(1, int(np.ceil(3 * sigma)))
    x = np.arange(-r, r + 1)
    k = np.exp(-0.5 * (x / sigma) ** 2)
    k /= k.sum()
    b = np.pad(a.astype(np.float64), ((r, r), (r, r)), mode="reflect")
    t = np.zeros((b.shape[0], a.shape[1]))
    for i, w in enumerate(k):
        t += w * b[:, i:i + a.shape[1]]
    o = np.zeros(a.shape)
    for i, w in enumerate(k):
        o += w * t[i:i + a.shape[0], :]
    return o


def huelle(punkte):
    """Konvexe Hülle (Andrew) einer Punktmenge (N, 2) -> Ecken gegen den Uhrzeigersinn."""
    p = sorted(set(map(tuple, np.round(punkte, 3))))
    if len(p) < 3:
        return np.array(p)

    def kreuz(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    unten, oben = [], []
    for q in p:
        while len(unten) >= 2 and kreuz(unten[-2], unten[-1], q) <= 0:
            unten.pop()
        unten.append(q)
    for q in reversed(p):
        while len(oben) >= 2 and kreuz(oben[-2], oben[-1], q) <= 0:
            oben.pop()
        oben.append(q)
    return np.array(unten[:-1] + oben[:-1])


def maske_vieleck(form, ecken):
    """Bildpunkte (Höhe, Breite) innerhalb eines konvexen Vielecks (x, y in Bildpunkten)."""
    h, w = form
    m = np.zeros((h, w), bool)
    if len(ecken) < 3:
        return m
    x0, x1 = max(0, int(np.floor(ecken[:, 0].min()))), min(w, int(np.ceil(ecken[:, 0].max())) + 1)
    y0, y1 = max(0, int(np.floor(ecken[:, 1].min()))), min(h, int(np.ceil(ecken[:, 1].max())) + 1)
    if x1 <= x0 or y1 <= y0:
        return m
    yy, xx = np.mgrid[y0:y1, x0:x1]
    px, py = xx + 0.5, yy + 0.5
    innen = np.ones(px.shape, bool)
    for i in range(len(ecken)):
        a, b = ecken[i], ecken[(i + 1) % len(ecken)]
        innen &= (b[0] - a[0]) * (py - a[1]) - (b[1] - a[1]) * (px - a[0]) >= 0
    m[y0:y1, x0:x1] = innen
    return m


def rechteck(form, cx, cy, bw, bh):
    h, w = form
    m = np.zeros((h, w), bool)
    x0, x1 = int(round(cx - bw / 2)), int(round(cx + bw / 2))
    y0, y1 = int(round(cy - bh / 2)), int(round(cy + bh / 2))
    m[max(0, y0):max(0, min(h, y1)), max(0, x0):max(0, min(w, x1))] = True
    return m
