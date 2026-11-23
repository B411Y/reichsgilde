"""Fell, Normal- und ORM-Karten fuer das Zugpferd (Eigenbau, nur numpy und Pillow; kein fremdes Bildmaterial).

    python -I tools/modelle/pferd2_fell.py <paket.npz> <Stehen_J.npz> <texturordner_ein> <ausgabeordner> [--varianten=braun,fuchs,rappe] [--vorschau]

Ablauf je Variante (Albedo 2048^2 aus <texturordner_ein>/Horse_<v>_2048.png, Farben bleiben):
1. Das Netz "Horse" wird in der Stehpose (Bild 0) gehaeutet und im UV-Raum gerastert: je Texel Lage (normierte Meter: x seitlich, y vorn negativ, z oben),
   Normale und die Ableitungen dP/du, dP/dv des Dreiecks.
2. Haarstrich: Richtung je Texel aus der Lage (Rumpf und Hals nach hinten unten, Beine nach unten, Kopf zur Nase), in die Tangentialebene gelegt und in
   UV-Richtung umgerechnet; Hoehenbild durch Faltung von Rauschen entlang dieser Richtung (Line Integral Convolution, zwei Laengen).
3. Albedo: Haarstrich leicht in die Farbe, natuerliche Verlaeufe (Oberlinie etwas dunkler, Bauch und untere Flanke etwas heller, beim Fuchs die
   Unterbeine dunkler); keine Abzeichen, nichts dazuerfunden.
4. Normalkarte (OpenGL: Gruen = oben im Bild) aus dem Hoehenbild; Rauheit (ORM: R Verdeckung = 1, G Rauheit, B Metall) je Bereich.
Ausgabe: Horse_<v>_2048.png (neue Albedo), Normal_2048.png, Rauheit_<v>_2048.png (nur Fell) im Ausgabeordner; den Atlas setzt pferd2_spiel.py zusammen.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402

ARGS = sys.argv[1:]
POS = [a for a in ARGS if not a.startswith("--")]
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS if a.startswith("--"))
N = 2048
NORM = "C:/Reichsgilde_Austausch/pferd2/schritt_a2/roh/henry_norm.json"


def glatt(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def rastern(Q, NQ, UV, F):
    """Je Texel: Lage, Normale, dP/du, dP/dv (Dreieck), Maske."""
    lage = np.zeros((N, N, 3), np.float32)
    nrm = np.zeros((N, N, 3), np.float32)
    du = np.zeros((N, N, 3), np.float32)
    dv = np.zeros((N, N, 3), np.float32)
    maske = np.zeros((N, N), bool)
    px = UV * N                                                           # Pixel (x = u * N, y = v * N; v nach unten wie im glTF)
    for tri in F:
        a, b, c = px[tri]
        x0, x1 = int(np.floor(min(a[0], b[0], c[0]))), int(np.ceil(max(a[0], b[0], c[0])))
        y0, y1 = int(np.floor(min(a[1], b[1], c[1]))), int(np.ceil(max(a[1], b[1], c[1])))
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, N - 1), min(y1, N - 1)
        if x1 < x0 or y1 < y0:
            continue
        det = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(det) < 1e-12:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        l1 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / det
        l2 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / det
        l3 = 1 - l1 - l2
        innen = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        if not innen.any():
            continue
        yy, xx = (ys[innen] - 0.5).astype(int), (xs[innen] - 0.5).astype(int)
        w = np.stack([l1[innen], l2[innen], l3[innen]], 1)
        lage[yy, xx] = w @ Q[tri]
        nrm[yy, xx] = w @ NQ[tri]
        # Ableitungen der Lage nach u und v (je Dreieck konstant)
        e1, e2 = Q[tri[1]] - Q[tri[0]], Q[tri[2]] - Q[tri[0]]
        d1, d2 = UV[tri[1]] - UV[tri[0]], UV[tri[2]] - UV[tri[0]]
        r = d1[0] * d2[1] - d2[0] * d1[1]
        if abs(r) > 1e-12:
            du[yy, xx] = (e1 * d2[1] - e2 * d1[1]) / r
            dv[yy, xx] = (e2 * d1[0] - e1 * d2[0]) / r
        maske[yy, xx] = True
    return lage, nrm, du, dv, maske


def strich_richtung(lage, nrm, du, dv, maske):
    """Haarstrich als Richtung in Pixeln (dx, dy) je Texel."""
    x, y, z = lage[..., 0], lage[..., 1], lage[..., 2]
    hinten = np.zeros_like(lage)
    hinten[..., 1] = 1.0
    unten = np.zeros_like(lage)
    unten[..., 2] = -1.0
    bein = glatt((0.85 - z) / 0.25) * (np.abs(x) > 0.0)                  # unterhalb des Rumpfs: Beine
    kopf = glatt((-1.05 - y) / 0.15) * glatt((z - 1.1) / 0.2)            # vor dem Hals, hoch: Kopf (Haar zur Nase)
    nase = np.zeros_like(lage)
    nase[..., 1], nase[..., 2] = -0.4, -1.0
    D = (hinten * 1.0 + unten * 0.45) * (1 - bein)[..., None] + (unten + hinten * 0.1) * bein[..., None]
    D = D * (1 - kopf)[..., None] + nase * kopf[..., None]
    n_ = nrm / np.maximum(np.linalg.norm(nrm, axis=-1, keepdims=True), 1e-9)
    D = D - (D * n_).sum(-1, keepdims=True) * n_
    # D = a * du + b * dv (kleinste Quadrate), a, b in UV-Einheiten -> Pixel
    g11, g12, g22 = (du * du).sum(-1), (du * dv).sum(-1), (dv * dv).sum(-1)
    h1, h2 = (D * du).sum(-1), (D * dv).sum(-1)
    det = g11 * g22 - g12 * g12
    det = np.where(np.abs(det) < 1e-12, 1e-12, det)
    a = (g22 * h1 - g12 * h2) / det
    b = (g11 * h2 - g12 * h1) / det
    l = np.maximum(np.hypot(a, b), 1e-9)
    dx, dy = np.where(maske, a / l, 1.0), np.where(maske, b / l, 0.0)
    return dx.astype(np.float32), dy.astype(np.float32)


def lic(rauschen, dx, dy, laenge):
    """Line Integral Convolution (gerade Strecke je Texel, Gauss-gewichtet, wiederholend am Rand)."""
    acc = np.zeros_like(rauschen)
    gew = 0.0
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    for k in range(-laenge, laenge + 1):
        w = float(np.exp(-0.5 * (k / (laenge * 0.6)) ** 2))
        sx = np.mod(np.rint(xx + k * dx).astype(np.int32), N)
        sy = np.mod(np.rint(yy + k * dy).astype(np.int32), N)
        acc += w * rauschen[sy, sx]
        gew += w
    return acc / gew


def fuellen(bild, maske, schritte=12):
    """Werte ueber die Inselgrenzen hinaus ausdehnen (gegen Naehte beim Filtern und in Mip-Stufen)."""
    b = bild.copy()
    m = maske.copy()
    for _ in range(schritte):
        summe = np.zeros_like(b)
        zahl = np.zeros(m.shape, np.float32)
        for oy, ox in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            mm = np.roll(m, (oy, ox), (0, 1))
            bb = np.roll(b, (oy, ox), (0, 1))
            summe += bb * mm[..., None] if b.ndim == 3 else bb * mm
            zahl += mm
        neu = (~m) & (zahl > 0)
        if b.ndim == 3:
            b[neu] = summe[neu] / zahl[neu][:, None]
        else:
            b[neu] = summe[neu] / zahl[neu]
        m = m | neu
    return b


def main():
    paket, stehen, ein, aus = POS
    os.makedirs(aus, exist_ok=True)
    varianten = OPT.get("varianten", "braun,fuchs,rappe").split(",")
    d = np.load(paket, allow_pickle=True)
    i = next(k for k in range(int(d["n"])) if str(d[f"mat{k}"]) == "Horse")
    p = dict(pos=d[f"pos{i}"].astype(float), jo=d[f"jo{i}"].astype(int), w=d[f"w{i}"].astype(float))
    F = d[f"idx{i}"].astype(int)
    UV = d[f"uv{i}"].astype(float)
    H = B.Haut()
    J = np.load(stehen)["J"][0].astype(float)
    w, _ = H.haut(p, J)
    mn = B.lade_norm(NORM)
    Q = B.anwenden(mn, B.blender_koord(w))
    NQ = B.weiche_normalen(Q, F)
    print("Rastern ...", flush=True)
    lage, nrm, du, dv, maske = rastern(Q, NQ, UV, F)
    dx, dy = strich_richtung(lage, nrm, du, dv, maske)
    rng = np.random.default_rng(int(OPT.get("saat", 1480)))                  # fester Zufall: gleiches Ergebnis bei jedem Lauf
    print("Haarstrich ...", flush=True)
    h = 0.65 * lic(rng.random((N, N)).astype(np.float32), dx, dy, int(OPT.get("strich", 10))) \
        + 0.35 * lic(rng.random((N, N)).astype(np.float32), dx, dy, int(OPT.get("strich_fein", 4)))
    h = (h - h[maske].mean()) / max(h[maske].std(), 1e-6)                      # Mittel 0, Streuung 1
    h = np.clip(h, -3, 3)
    # Normalkarte (Gruen = oben im Bild, OpenGL wie glTF)
    s = float(OPT.get("staerke", 0.9))
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) / 2
    gy_oben = (np.roll(h, 1, 0) - np.roll(h, -1, 0)) / 2
    n_ = np.stack([-s * gx, -s * gy_oben, np.ones_like(h)], -1)
    n_ /= np.linalg.norm(n_, axis=-1, keepdims=True)
    n_ = np.where(maske[..., None], n_, np.array([0, 0, 1.0], np.float32))
    n_ = fuellen(n_.astype(np.float32), maske)
    Image.fromarray(np.clip((n_ * 0.5 + 0.5) * 255 + 0.5, 0, 255).astype(np.uint8)).save(os.path.join(aus, "Normal_2048.png"))
    z, y = lage[..., 2], lage[..., 1]
    rumpf = glatt((z - 0.75) / 0.15) * glatt((-y + 0.95) / 0.1)             # Rumpf, Hals (ohne Beine)
    oberlinie = glatt((z - 1.30) / 0.25) * rumpf
    bauch = glatt((1.10 - z) / 0.20) * glatt((z - 0.80) / 0.10) * glatt((0.80 - np.abs(y)) / 0.15)
    unterbein = glatt((0.50 - z) / 0.25)
    for v in varianten:
        alb = np.asarray(Image.open(os.path.join(ein, f"Horse_{v}_2048.png")).convert("RGB")).astype(np.float32)
        r_, g_, b_ = alb[..., 0], alb[..., 1], alb[..., 2]
        rosa = (b_ > g_ * 1.05) & (r_ > 120)                                 # Nuestern, Lider, Ohrinnenseite: ohne Haarstrich
        fell = maske & ~rosa
        f = np.ones((N, N), np.float32)
        f *= 1 - float(OPT.get("oberlinie", 0.10)) * oberlinie
        f *= 1 + float(OPT.get("bauch", 0.08)) * bauch
        if v == "fuchs":
            f *= 1 - 0.15 * unterbein
        f *= 1 + float(OPT.get("strich_farbe", 0.07)) * h
        f = np.where(fell, f, 1.0)
        neu = np.clip(alb * f[..., None], 0, 255)
        Image.fromarray((neu + 0.5).astype(np.uint8)).save(os.path.join(aus, f"Horse_{v}_2048.png"))
        rau = np.full((N, N), 0.55 if v == "rappe" else 0.72, np.float32)   # Rappe: glaenzenderes Fell
        rau = rau - 0.04 * np.clip(h, -1.5, 1.5)
        rau = np.where(rosa, 0.45, rau)
        Image.fromarray((np.clip(rau, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(aus, f"Rauheit_{v}_2048.png"))
        print("geschrieben", v, flush=True)
    if "vorschau" in OPT:
        Image.fromarray(((np.clip(h, -2, 2) + 2) / 4 * 255).astype(np.uint8)).save(os.path.join(aus, "strich_hoehe.png"))


if __name__ == "__main__":
    main()
