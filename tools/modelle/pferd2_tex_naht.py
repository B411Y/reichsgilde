"""Texturnaehte heilen (Pferd B): an UV-Inselkanten, wo dieselbe Kante des Netzes auf zwei verschieden gefaerbten Stellen der Fell-Textur liegt, wird die Farbe beider Seiten
auf den Mittelwert gezogen (weicher Streifen, wenige Pixel breit). Das beseitigt helle Linien in der Brustmitte und an der Kehle, ohne die Geometrie zu aendern.

Als Baustein aus pferd2_umfaerben.py benutzt (--naht=<paket.npz>); eigenstaendig:
    python -I tools/modelle/pferd2_tex_naht.py <paket.npz> <textur_in.png> <textur_aus.png> [--breite=5]
"""
import sys

import numpy as np
from PIL import Image


def naht_kanten(paket):
    """Liste von ((uv_a1, uv_b1), (uv_a2, uv_b2)) je Netzkante, die auf zwei verschiedenen UV-Stellen liegt (Pixel-unabhaengig, UV 0..1)."""
    d = np.load(paket, allow_pickle=True)
    k = next(i for i in range(int(d["n"])) if str(d[f"mat{i}"]) == "Horse")
    P, uv, idx = d[f"pos{k}"], d[f"uv{k}"], d[f"idx{k}"]
    key = np.round(P * 1e4).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.reshape(-1)
    kanten = {}
    for tri in idx:
        for i in range(3):
            a, b = int(tri[i]), int(tri[(i + 1) % 3])
            ka, kb = int(inv[a]), int(inv[b])
            if ka == kb:
                continue
            if ka > kb:
                a, b, ka, kb = b, a, kb, ka
            kanten.setdefault((ka, kb), []).append((a, b))
    ergebnis = []
    for l in kanten.values():
        if len(l) != 2:
            continue
        (a1, b1), (a2, b2) = l
        if np.abs(uv[a1] - uv[a2]).max() < 1e-5 and np.abs(uv[b1] - uv[b2]).max() < 1e-5:
            continue
        ergebnis.append(((uv[a1], uv[b1]), (uv[a2], uv[b2])))
    return ergebnis


def naht_heilen(img, paket, breite=5.0):
    """img: float32 (n, n, 3) in 0..1. Gibt das geheilte Bild und die Zahl der Nahtkanten zurueck."""
    n = img.shape[0]
    kanten = naht_kanten(paket)
    acc = np.zeros(img.shape, np.float64)
    wsum = np.zeros(img.shape[:2], np.float64)
    r = int(np.ceil(breite * 1.5))
    yy, xx = np.mgrid[-r:r + 1, -r:r + 1]
    kern = np.exp(-(xx * xx + yy * yy) / (2 * (breite / 2.0) ** 2))
    for (a1, b1), (a2, b2) in kanten:
        la = max(np.hypot(*((a1 - b1) * n)), 1.0)
        m = int(np.ceil(la)) + 1
        for t in np.linspace(0.0, 1.0, m):
            p1 = (a1 * (1 - t) + b1 * t) * n
            p2 = (a2 * (1 - t) + b2 * t) * n
            c1 = img[int(np.clip(p1[1], 0, n - 1)), int(np.clip(p1[0], 0, n - 1))]
            c2 = img[int(np.clip(p2[1], 0, n - 1)), int(np.clip(p2[0], 0, n - 1))]
            ziel = (c1 + c2) / 2.0
            for p in (p1, p2):
                x0, y0 = int(round(p[0])), int(round(p[1]))
                xa, xb, ya, yb = max(x0 - r, 0), min(x0 + r + 1, n), max(y0 - r, 0), min(y0 + r + 1, n)
                if xb <= xa or yb <= ya:
                    continue
                k = kern[ya - (y0 - r):yb - (y0 - r), xa - (x0 - r):xb - (x0 - r)]
                acc[ya:yb, xa:xb] += k[..., None] * ziel[None, None, :]
                wsum[ya:yb, xa:xb] += k
    mix = np.clip(wsum * 0.6, 0.0, 0.85)
    ziel_bild = acc / np.maximum(wsum, 1e-9)[..., None]
    return (img * (1 - mix[..., None]) + ziel_bild * mix[..., None]).astype(np.float32), len(kanten)


if __name__ == "__main__":
    pos = [a for a in sys.argv[1:] if not a.startswith("--")]
    opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    Image.MAX_IMAGE_PIXELS = None
    bild = np.asarray(Image.open(pos[1]).convert("RGB")).astype(np.float32) / 255.0
    neu, nk = naht_heilen(bild, pos[0], float(opt.get("breite", 5)))
    Image.fromarray((np.clip(neu, 0, 1) * 255).astype(np.uint8)).save(pos[2])
    print("Nahtkanten", nk)
