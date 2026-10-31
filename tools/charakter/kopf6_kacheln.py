"""Detailkacheln der Haut für Etappe C (Charakter Phase 6; eigener Code, MIT; nur numpy und OpenImageIO, Blenders
Python). Alles prozedural mit festem Startwert, nahtlos (periodisch gerechnet), keine Fremddaten.

haut_c_poren.png  (1024², Kachel 25 mm, 24 µm je Bildpunkt)
    RG  Normale der Feinform (Tangentenraum, +x = +u, +y = Bild oben; Gefälle / PORE_GEFAELLE): Poren 0,25–0,5 mm
        (ellipsenförmig, ungleich tief), feine Poren 40–80 µm, Hautfelderung (Furchen 30–40 µm breit, Zellen ≈ 0,65 mm),
        Mikrorauschen 0,1–0,4 mm
    B   Höhle (Porenboden, Furche) 0..1 – dunkelt die Albedo und nimmt Glanz
    A   Bartstoppeln: kurze Striche 0,35–0,8 mm, 60–90 µm breit, Wuchsrichtung nach unten ± 22°, Abstand ≈ 0,8 mm
haut_c_flecken.png (512², Kachel 60 mm, 0,12 mm je Bildpunkt)
    R   Melanin-Unruhe (Wellenlängen 2–14 mm), 0,5 = Mittel
    G   Hämoglobin-Unruhe (3–18 mm), 0,5 = Mittel
    B   Rang des nächsten Flecks (0..1, je Zelle gleich): ein Fleck ist sichtbar, wenn sein Rang unter der Dichte liegt
    A   Fleckform (weiche, unregelmäßige Ellipsen, Radius 0,25–0,75 mm; als Altersfleck 3× größer abgetastet)

Aufruf: $BPY -I tools/charakter/kopf6_kacheln.py <ausgabeordner> [samen=6110]
"""
import json
import os
import sys

import numpy as np
import OpenImageIO as oiio

PORE_GEFAELLE = 1.2


def periodisch_fenster(g, cx, cy, r):
    """Indizes (periodisch) und Abstände eines Fensters um (cx, cy) mit Radius r (Bildpunkte)."""
    x0, x1 = int(np.floor(cx - r)), int(np.ceil(cx + r))
    y0, y1 = int(np.floor(cy - r)), int(np.ceil(cy + r))
    xs = np.arange(x0, x1 + 1)
    ys = np.arange(y0, y1 + 1)
    X, Y = np.meshgrid(xs, ys)
    return (Y % g, X % g), X - cx, Y - cy


def bandrauschen(g, rng, lam_min, lam_max, px):
    """Periodisches Rauschen mit Wellenlängen lam_min..lam_max (mm), ≈ 1/f im Band, Mittel 0, Streuung 1."""
    f = np.fft.fftfreq(g, d=px)
    FX, FY = np.meshgrid(f, f)
    fr = np.hypot(FX, FY)
    lo, hi = 1.0 / lam_max, 1.0 / lam_min
    band = np.clip((fr - lo * 0.7) / (lo * 0.6), 0, 1) * np.clip((hi * 1.3 - fr) / (hi * 0.6), 0, 1)
    amp = band / np.maximum(fr, 1e-6)
    spek = amp * np.exp(2j * np.pi * rng.random((g, g)))
    n = np.real(np.fft.ifft2(spek))
    return (n - n.mean()) / max(n.std(), 1e-12)


def voronoi_f1f2(g, zellen, rng, zitter=0.9):
    """F1, F2 (Bildpunkte) und Index der nächsten Zelle für ein periodisches Zitter-Gitter."""
    cs = g / zellen
    jx = rng.random((zellen, zellen)) * zitter + (1 - zitter) / 2
    jy = rng.random((zellen, zellen)) * zitter + (1 - zitter) / 2
    yy, xx = np.mgrid[0:g, 0:g].astype(np.float64) + 0.5
    ci = np.floor(xx / cs).astype(int)
    cj = np.floor(yy / cs).astype(int)
    f1 = np.full((g, g), np.inf)
    f2 = np.full((g, g), np.inf)
    nah = np.zeros((g, g), int)
    for di in (-1, 0, 1):
        for dj in (-1, 0, 1):
            ii = ci + di
            jj = cj + dj
            pi_ = ii % zellen
            pj = jj % zellen
            px = (ii + jx[pj, pi_]) * cs
            py = (jj + jy[pj, pi_]) * cs
            d = np.hypot(xx - px, yy - py)
            neu1 = d < f1
            f2 = np.where(neu1, f1, np.minimum(f2, d))
            nah = np.where(neu1, pj * zellen + pi_, nah)
            f1 = np.where(neu1, d, f1)
    return f1, f2, nah, (jx, jy, cs)


def poren_kachel(g, L, rng):
    px = L / g                                    # mm je Bildpunkt
    h = np.zeros((g, g))
    # Hautfelderung: Furchen an den Zellgrenzen (F2 − F1 klein), zwei Lagen verschieden fein
    for zellen, tiefe, breite in ((int(L / 0.65), 0.012, 1.4), (int(L / 0.32), 0.005, 0.9)):
        f1, f2, _, _ = voronoi_f1f2(g, zellen, rng)
        h -= tiefe * np.exp(-((f2 - f1) / breite) ** 2)
    # Poren: Zitter-Gitter ≈ 1 mm, ellipsenförmig, Tiefe nach Größe
    for abstand, r_min, r_max, t_min, t_max in ((1.0, 0.055, 0.13, 0.03, 0.075), (0.5, 0.02, 0.04, 0.006, 0.014)):
        n = int(L / abstand)
        cs = g / n
        for i in range(n):
            for j in range(n):
                cx = (i + rng.uniform(0.1, 0.9)) * cs
                cy = (j + rng.uniform(0.1, 0.9)) * cs
                s = rng.uniform(r_min, r_max) / px
                q = rng.uniform(1.0, 1.5)
                w = rng.uniform(0, np.pi)
                idx, dx, dy = periodisch_fenster(g, cx, cy, 4 * s * q)
                u = dx * np.cos(w) + dy * np.sin(w)
                v = -dx * np.sin(w) + dy * np.cos(w)
                rr = (u / (s * q)) ** 2 + (v / s) ** 2
                t = rng.uniform(t_min, t_max) * (s * px / r_max) ** 0.5
                np.add.at(h, idx, -t * np.exp(-0.5 * rr) + 0.12 * t * np.exp(-0.5 * rr / 2.2))
    # Mikrorauschen
    h += 0.0035 * bandrauschen(g, rng, 0.1, 0.4, px)
    # Normale (periodisches Gefälle, mm/mm)
    dhdu = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) / (2 * px)
    dhdv = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) / (2 * px)
    R = 0.5 + 0.5 * np.clip(-dhdu / PORE_GEFAELLE, -1, 1)
    G = 0.5 + 0.5 * np.clip(dhdv / PORE_GEFAELLE, -1, 1)
    B = np.clip(-h / 0.05, 0, 1) ** 0.8
    # Bartstoppeln
    A = np.zeros((g, g))
    n = int(L / 0.8)
    cs = g / n
    for i in range(n):
        for j in range(n):
            cx = (i + rng.uniform(0.05, 0.95)) * cs
            cy = (j + rng.uniform(0.05, 0.95)) * cs
            w = np.radians(90.0 + rng.normal(0, 22.0))      # nach unten (Bildzeilen wachsen nach unten)
            lg = rng.uniform(0.35, 0.8) / px
            br = rng.uniform(0.03, 0.045) / px
            idx, dx, dy = periodisch_fenster(g, cx + 0.5 * lg * np.cos(w), cy + 0.5 * lg * np.sin(w), lg * 0.6 + 3 * br)
            ux, uy = np.cos(w), np.sin(w)
            t = np.clip(dx * ux + dy * uy, -0.5 * lg, 0.5 * lg)
            d = np.hypot(dx - t * ux, dy - t * uy)
            dicke = br * (1.0 - 0.5 * ((t + 0.5 * lg) / lg))     # zur Spitze dünner
            cov = np.clip((dicke - d) / 1.0 + 0.5, 0, 1) * rng.uniform(0.7, 1.0)
            A[idx] = np.maximum(A[idx], cov)
    return np.stack([R, G, B, A], -1), {"gefaelle_99": float(np.percentile(np.hypot(dhdu, dhdv), 99)),
                                        "stoppel_deckung": float(A.mean())}


def flecken_kachel(g, L, rng):
    px = L / g
    R = 0.5 + 0.17 * bandrauschen(g, rng, 2.0, 14.0, px)
    G = 0.5 + 0.17 * bandrauschen(g, rng, 3.0, 18.0, px)
    zellen = int(L / 1.6)
    f1, _, nah, (jx, jy, cs) = voronoi_f1f2(g, zellen, rng, zitter=0.8)
    rang = rng.random(zellen * zellen)
    B = rang[nah]
    A = np.zeros((g, g))
    rand = bandrauschen(g, rng, 0.15, 0.6, px)
    for k in range(zellen * zellen):
        pj, pi_ = divmod(k, zellen)
        cx = (pi_ + jx[pj, pi_]) * cs
        cy = (pj + jy[pj, pi_]) * cs
        s = rng.uniform(0.25, 0.75) / px
        q = rng.uniform(1.0, 1.4)
        w = rng.uniform(0, np.pi)
        idx, dx, dy = periodisch_fenster(g, cx, cy, 1.6 * s * q)
        u = dx * np.cos(w) + dy * np.sin(w)
        v = -dx * np.sin(w) + dy * np.cos(w)
        r = np.sqrt((u / (s * q)) ** 2 + (v / s) ** 2) + 0.18 * rand[idx]
        A[idx] = np.maximum(A[idx], np.clip((1.0 - r) / 0.35, 0, 1) * rng.uniform(0.6, 1.0))
    return np.stack([np.clip(R, 0, 1), np.clip(G, 0, 1), B, A], -1)


def schreiben(pfad, bild):
    g = bild.shape[0]
    spec = oiio.ImageSpec(g, g, 4, oiio.FLOAT)
    spec.alpha_channel = -1
    spec.attribute("oiio:UnassociatedAlpha", 1)
    buf = oiio.ImageBuf(spec)
    buf.set_pixels(oiio.ROI(0, g, 0, g, 0, 1, 0, 4), np.ascontiguousarray(bild.astype(np.float32)))
    buf.write(pfad, "uint8")


def main():
    aus = sys.argv[1]
    opt = dict(a.split("=", 1) for a in sys.argv[2:])
    samen = int(opt.get("samen", "6110"))
    os.makedirs(aus, exist_ok=True)
    rng = np.random.default_rng(samen)
    poren, info = poren_kachel(1024, 25.0, rng)
    schreiben(os.path.join(aus, "haut_c_poren.png"), poren)
    flecken = flecken_kachel(512, 60.0, rng)
    schreiben(os.path.join(aus, "haut_c_flecken.png"), flecken)
    beschr = {"poren_kachel_mm": 25.0, "flecken_kachel_mm": 60.0, "poren_gefaelle": PORE_GEFAELLE, "samen": samen}
    beschr.update({k: round(v, 4) for k, v in info.items()})
    json.dump(beschr, open(os.path.join(aus, "haut_c_kacheln.json"), "w", encoding="utf-8"), indent=1)
    print("KACHELN", beschr)


if __name__ == "__main__":
    main()
