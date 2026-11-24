"""Schecke -> einfarbig (Braun, Fuchs, Rappe) fuer die Fell-Textur von Pferd B (Horse_baseColor.png, 4096 -> 2048) und Haar-Texturen mit Alpha. Pillow und numpy.

    python -I tools/modelle/pferd2_umfaerben.py <Horse_baseColor.png> <Hair_baseColor.png> <Ausgabeordner>

Verfahren (Version 2): Die grossflaechige Helligkeit der Fell-Pixel wird aus den BRAUNEN Pixeln der Umgebung geschaetzt (normierte Unschaerfe), Hellflecken tragen dazu nichts bei;
dadurch verschwinden die Scheckenflecken. Die feine Zeichnung (Hochpass) bleibt. Rosa Stellen (Nase, Ohrinnen) und sehr dunkle Stellen (Hufe, Maul, Auge) bleiben.
Braun: Zielfarbe 18 % heller als Version 1, mit sanftem Verlauf; Rappe: weiche Glanzzonen aus der sehr grossraeumigen Helligkeit; Fuchs wie bisher.
Haar: Karte (Graustufen) wird Alpha, RGB = Haarfarbe der Variante mal Karte.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

Image.MAX_IMAGE_PIXELS = None
NAHT = next((a[7:] for a in sys.argv if a.startswith("--naht=")), None)          # Paket (npz), dessen UV-Naehte in der Fell-Textur geheilt werden
FARBEN = {"braun": (0.283, 0.153, 0.088), "fuchs": (0.42, 0.20, 0.085), "rappe": (0.050, 0.046, 0.043)}
HAARFARBEN = {"braun": (0.045, 0.032, 0.026), "fuchs": (0.52, 0.20, 0.05), "rappe": (0.028, 0.028, 0.030)}


def blur(a, sigma):
    """Gauss-Unschaerfe (separabel, bei grossem Sigma auf kleinerem Raster gerechnet und hochgezogen)."""
    f = 2 ** int(np.log2(max(1, sigma // 4)))
    n = a.shape[0]
    if f > 1:
        a = a.reshape(n // f, f, n // f, f).mean(axis=(1, 3))
        sigma = sigma / f
    r = int(max(1, round(sigma * 3)))
    x = np.arange(-r, r + 1)
    k = np.exp(-(x * x) / (2 * sigma * sigma))
    k /= k.sum()
    pad = np.pad(a, r, mode="reflect")
    t = np.zeros_like(pad)
    for i, kv in enumerate(k):
        t[:, r:-r] += kv * pad[:, i:i + a.shape[1]]
    o = np.zeros_like(a)
    for i, kv in enumerate(k):
        o += kv * t[i:i + a.shape[0], r:-r]
    if f > 1:
        im = Image.fromarray(o.astype(np.float32), mode="F").resize((n, n), Image.BILINEAR)
        o = np.asarray(im, dtype=np.float32)
    return o

def behang_maske(n=2048):
    """Gemalter Fesselbehang: Maske (0..1) im UV-Raum der Fell-Textur ueber den Fesselkoepfen (Hoehe 0,05 bis 0,27 m in der Idle-Pose), Strichrichtung entlang des Beins
    (Gradient von z im UV), je Strich ein Zufallswert. Gibt (Staerke, Strich) zurueck."""
    import pferd2_b as B
    H = B.Haut()
    k = next(i for i, p in enumerate(H.prims) if p["mat"] == "Horse")
    p = H.prims[k]
    J0 = H.gelenkmatrizen("Skeleton|1 Ilde", 0.0)
    mn = B.lade_norm("C:/Reichsgilde_Austausch/pferd2/schritt_a2/roh/henry_norm.json")
    w, _ = H.haut(p, J0)
    Q = B.anwenden(mn, B.blender_koord(w))
    uv = p["uv"] * n
    staerke = np.zeros((n, n), np.float32)
    strich = np.zeros((n, n), np.float32)
    rng = np.random.default_rng(7)
    for f in p["idx"]:
        zz = Q[f, 2]
        if zz.max() > 0.30 or zz.min() < 0.02:
            continue
        a, b, c = uv[f[0]], uv[f[1]], uv[f[2]]
        x0, x1 = int(max(min(a[0], b[0], c[0]) - 1, 0)), int(min(max(a[0], b[0], c[0]) + 2, n))
        y0, y1 = int(max(min(a[1], b[1], c[1]) - 1, 0)), int(min(max(a[1], b[1], c[1]) + 2, n))
        if x1 <= x0 or y1 <= y0:
            continue
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-9:
            continue
        # Gradient von z im UV (a*u + b*v + c)
        M = np.array([[a[0], a[1], 1], [b[0], b[1], 1], [c[0], c[1], 1]])
        try:
            ga, gb, gc = np.linalg.solve(M, zz)
        except np.linalg.LinAlgError:
            continue
        gl = np.hypot(ga, gb)
        if gl < 1e-9:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
        l1 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
        l2 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
        l3 = 1 - l1 - l2
        im = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        z = ga * xs + gb * ys + gc
        perp = (-gb * xs + ga * ys) / gl                                   # Koordinate quer zur Beinrichtung (Pixel)
        s = np.clip((z - 0.05) / 0.06, 0, 1) ** 1.0 * np.clip((0.27 - z) / 0.07, 0, 1)
        # Strichmuster: Summe zweier Sinus mit Zufallsphase je Dreieck-Insel (stetig ueber Dreiecke, weil nur von perp abhaengig)
        st = 0.5 + 0.25 * np.sin(perp * 0.9 + 1.3) + 0.25 * np.sin(perp * 2.3 + 0.4)
        sub = staerke[y0:y1, x0:x1]
        sub[im] = np.maximum(sub[im], s[im])
        strich[y0:y1, x0:x1][im] = st[im]
    return staerke, strich


def main():
    horse, hair, aus = [a for a in sys.argv[1:] if not a.startswith("--")][:3]
    os.makedirs(aus, exist_ok=True)
    a = np.asarray(Image.open(horse).convert("RGB").resize((2048, 2048), Image.LANCZOS)).astype(np.float32) / 255.0
    mx, mn = a.max(-1), a.min(-1)
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    lum = 0.3 * a[..., 0] + 0.59 * a[..., 1] + 0.11 * a[..., 2]
    rosa = (a[..., 0] > 0.55) & (a[..., 0] - a[..., 1] > 0.18) & (a[..., 2] > 0.35)
    braun = (s >= 0.20) & (mx >= 0.10) & ~rosa
    dunkel = mx < 0.16
    dunkel_gross = np.asarray(Image.fromarray((dunkel * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(15)).filter(ImageFilter.MaxFilter(15))) > 0     # nur grosse dunkle Flaechen (Beine, Maul, Hufe); duenne alte Fleckenraender zaehlen als Fell
    fell = ~dunkel_gross & ~rosa
    m_fell = blur(fell.astype(np.float32), 4) * (1.0 - blur(rosa.astype(np.float32), 3))
    mb = braun.astype(np.float32)
    nenner = blur(mb, 70)
    nenner = blur(mb, 110)
    low_b = blur(lum * mb, 110) / np.maximum(nenner, 1e-3)
    med = float(np.median(lum[braun]))
    low_b = np.where(nenner < 0.02, med, low_b)
    verlauf = np.clip(low_b / med, 0.88, 1.12) ** 0.6
    r5 = lum / np.maximum(blur(lum, 5), 1e-3)
    kante = blur((np.abs(r5 - 1.0) > 0.10).astype(np.float32), 9)                  # Flecken-Raender: keine Zeichnung uebernehmen
    hf = (1.0 + (np.clip(r5, 0.8, 1.25) - 1.0) * (1.0 - np.clip(kante * 4.0, 0, 1))) ** 1.0
    low_alle = blur(lum * fell, 160) / np.maximum(blur(fell.astype(np.float32), 160), 1e-3)
    glanz = np.clip(low_alle / max(float(np.median(lum[fell])), 1e-3), 0.6, 1.6)
    behang_s, behang_st = behang_maske(2048)
    behang_s = blur(behang_s, 7)                                 # weicher Verlauf statt Strichmuster (Form-P3)
    behang_farbe = {"braun": (0.19, 0.115, 0.075), "fuchs": (0.70, 0.42, 0.20), "rappe": (0.15, 0.14, 0.13)}
    for name, z in FARBEN.items():
        z = np.array(z, np.float32)
        if name == "rappe":
            k = (0.7 + 0.9 * (glanz - 0.6)) * hf
            neu = z[None, None, :] * k[..., None] * np.array([1.0, 1.05, 1.18], np.float32)[None, None, :]
        else:
            neu = z[None, None, :] * (verlauf * hf)[..., None]
        rest = (1.0 - m_fell)[..., None] * (1.0 - blur(rosa.astype(np.float32), 3))[..., None]
        if name == "rappe":                                    # dunkle Reste (Beine, Maul) neutral dunkel statt braun
            grau = np.dstack([lum * 0.85, lum * 0.87, lum * 0.95])
            a_rest = a * (1.0 - 0.9 * rest) + grau * 0.9 * rest
        elif name == "fuchs":                                  # dunkle Reste (Beine, Strumpf-Flecken) als dunkles Kastanienbraun, nicht schwarzbraun
            braunlich = ((s > 0.25) & (mx > 0.07)).astype(np.float32)               # Beine/Reste: braeunlich, nicht Hufe (grau), Auge/Maul (fast schwarz)
            braunlich = blur(braunlich, 3)
            dkl = z[None, None, :] * 0.97 * np.clip(lum / 0.11, 0.9, 1.08)[..., None]
            w_ = (rest * braunlich[..., None]) * 0.97
            a_rest = a * (1.0 - w_) + dkl * w_
        else:
            a_rest = a
        ausb = a_rest * (1.0 - m_fell[..., None]) + neu * m_fell[..., None]
        # Reste heller Pixel (weisser Strich am Ohr u. a.): auf Fellfarbe
        hell_rest = (ausb.max(-1) > 0.62) & (s < 0.30) & ~rosa
        ausb = np.where(hell_rest[..., None], z[None, None, :] * 1.1, ausb)
        # gemalter Fesselbehang
        m_b = (behang_s * 0.62)[..., None]
        ausb = ausb * (1 - m_b) + np.array(behang_farbe[name], np.float32)[None, None, :] * m_b
        # dunkler Fleck unten an der Brust (Zitzenbild und Streifen der Schecken-Textur, Pixel x 118..224, y 440..680): mit Fell von rechts daneben ueberdecken, weiche Raender
        x0_, x1_, y0_, y1_, dx_ = 118, 224, 440, 680, 118
        wx = np.minimum(np.minimum(np.arange(x0_, x1_) - x0_, x1_ - 1 - np.arange(x0_, x1_)) / 10.0, 1.0)
        wy = np.minimum(np.minimum(np.arange(y0_, y1_) - y0_, y1_ - 1 - np.arange(y0_, y1_)) / 10.0, 1.0)
        wf = np.clip(wy[:, None] * wx[None, :], 0.0, 1.0)[..., None]
        ausb[y0_:y1_, x0_:x1_] = ausb[y0_:y1_, x0_:x1_] * (1 - wf) + ausb[y0_:y1_, x0_ + dx_:x1_ + dx_] * wf
        if NAHT:                                               # Texturnaehte an UV-Inselkanten heilen (Brustmitte, Kehle)
            import pferd2_tex_naht as TN
            ausb, nk = TN.naht_heilen(ausb.astype(np.float32), NAHT)
            print("Nahtkanten", nk)
        Image.fromarray((np.clip(ausb, 0, 1) * 255).astype(np.uint8)).save(os.path.join(aus, f"Horse_{name}_2048.png"))
        print("Fell", name)
    k = np.asarray(Image.open(hair).convert("L").resize((1024, 1024), Image.LANCZOS)).astype(np.float32) / 255.0
    alpha = np.clip((k - 0.08) / 0.27, 0, 1)
    for name, hz in HAARFARBEN.items():
        rgb = np.array(hz, np.float32)[None, None, :] * (0.55 + 0.75 * np.clip(k * 2.0, 0, 1))[..., None]      # Untergrenze: Schweif und Maehne gleich hell
        rgba = np.dstack([np.clip(rgb, 0, 1), alpha])
        Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").save(os.path.join(aus, f"Hair_{name}_1024.png"))
        print("Haar", name)


if __name__ == "__main__":
    main()










