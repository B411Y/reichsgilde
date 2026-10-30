"""Bildwerte (Charakter Phase 6, S0): Hautfarbe je Zone, Licht : Schatten, Feinstruktur, Augen, Bildunterschiede.

Zonen (Vorderansicht) über die Pupillen, so wie in der Bestandsaufnahme gemessen (gleiche Definition für Render und
Fotovorlagen; die Pupillen kommen bei Rendern aus den projizierten Netz-Merkpunkten, bei Fotos aus
vorlagen_merkpunkte.json). pl = Pupille links im Bild (rechte Gesichtshälfte der Figur, beim Licht L-V/L-S die Lichtseite).
  wange_licht    Quadrat 0,30·IPD, Mitte (pl.x − 0,05·IPD, p.y + 0,55·IPD)
  wange_schatten wie oben auf der anderen Seite (pr.x + 0,05·IPD)
  stirn          Quadrat 0,30·IPD, Mitte (Augenmitte, p.y − 0,55·IPD)
  oberlid        0,16 × 0,05 IPD, Mitte (pl.x + 0,02·IPD, p.y − 0,10·IPD)  (Lidfalte zwischen Auge und Braue, Lichtseite)
  unterlid       0,18 × 0,06 IPD, Mitte (pl.x, p.y + 0,17·IPD)
  kinn           0,25 × 0,12 IPD, Mitte (Augenmitte, p.y + 1,55·IPD)      (Bartzone unter der Lippe)
  kiefer_licht   0,22 × 0,15 IPD, Mitte (pl.x + 0,05·IPD, p.y + 1,25·IPD) (Bartzone an der Wange, Lichtseite)
Werte je Zone: CIELAB (D65) aus dem Mittel der sRGB-Werte, Chroma, ITA = atan2(L* − 50, b*), Leuchtdichte Y (linear),
R/G und G/B (sRGB, 8 bit). Feinstruktur: Bild auf IPD 89 Bildpunkte skaliert, Hochpass L* − Gauß(L*, 1,5) – Streuung
im Feld; Fleckenband: Gauß(L*, 1) − Gauß(L*, 6). Augen: Iris = Ring 0,03–0,08·IPD um die Pupille, Lederhaut = Felder
0,13–0,20·IPD seitlich der Pupille (Höhe ±0,02·IPD), je Mittel der Leuchtdichte Y.
"""
import math

import numpy as np

from . import bild as B

IPD_NORM = 89.0


def zonen(form, pl, pr):
    ipd = math.dist(pl, pr)
    py = 0.5 * (pl[1] + pr[1])
    mx = 0.5 * (pl[0] + pr[0])
    q = 0.30 * ipd
    return {
        "wange_licht": B.rechteck(form, pl[0] - 0.05 * ipd, py + 0.55 * ipd, q, q),
        "wange_schatten": B.rechteck(form, pr[0] + 0.05 * ipd, py + 0.55 * ipd, q, q),
        "stirn": B.rechteck(form, mx, py - 0.55 * ipd, q, q),
        "oberlid": B.rechteck(form, pl[0] + 0.02 * ipd, py - 0.10 * ipd, 0.16 * ipd, 0.05 * ipd),
        "unterlid": B.rechteck(form, pl[0], py + 0.17 * ipd, 0.18 * ipd, 0.06 * ipd),
        "kinn": B.rechteck(form, mx, py + 1.55 * ipd, 0.25 * ipd, 0.12 * ipd),
        "kiefer_licht": B.rechteck(form, pl[0] + 0.05 * ipd, py + 1.25 * ipd, 0.22 * ipd, 0.15 * ipd),
    }


def farbe(img, maske):
    if not maske.any():
        return None
    s = img[maske].mean(axis=0)
    L, a, b = B.lab(s)
    Y = float(B.leuchtdichte(img[maske]).mean())
    return {"L": float(L), "a": float(a), "b": float(b), "C": float(math.hypot(a, b)),
            "ITA": float(math.degrees(math.atan2(L - 50.0, b))), "Y": Y,
            "RG": float(s[0] / max(s[1], 1e-6)), "GB": float(s[1] / max(s[2], 1e-6)), "n": int(maske.sum()),
            "srgb": [round(float(v) * 255, 1) for v in s]}


def feinstruktur(img, pl, pr):
    """Hochpass- und Fleckenband-Streuung (L*) in Wangen und Stirn bei IPD 89 Bildpunkten."""
    ipd = math.dist(pl, pr)
    f = IPD_NORM / ipd
    klein = B.skalieren(img, f) if abs(f - 1) > 1e-3 else img
    L = B.lab(klein)[..., 0]
    hp = L - B.gauss(L, 1.5)
    band = B.gauss(L, 1.0) - B.gauss(L, 6.0)
    z = zonen(L.shape, (pl[0] * f, pl[1] * f), (pr[0] * f, pr[1] * f))
    aus = {}
    for n in ("wange_licht", "wange_schatten", "stirn"):
        m = z[n]
        aus[n] = {"hp": float(hp[m].std()), "band": float(band[m].std())}
    return aus


def augen(img, pl, pr):
    ipd = math.dist(pl, pr)
    h, w = img.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    Y = B.leuchtdichte(img)
    aus = {}
    for name, p, seite in (("links", pl, -1), ("rechts", pr, 1)):
        r = np.hypot(xx + 0.5 - p[0], yy + 0.5 - p[1])
        iris = (r >= 0.03 * ipd) & (r <= 0.08 * ipd)
        dx = (xx + 0.5 - p[0]) * seite
        leder = (np.abs(yy + 0.5 - p[1]) <= 0.02 * ipd) & (np.abs(xx + 0.5 - p[0]) >= 0.13 * ipd) & (np.abs(xx + 0.5 - p[0]) <= 0.20 * ipd)
        aus[name] = {"iris_Y": float(Y[iris].mean()) if iris.any() else None,
                     "lederhaut_Y": float(Y[leder].mean()) if leder.any() else None}
    return aus


def werte_vorn(img, pl, pr):
    """Alle Bildwerte einer Vorderansicht; pl, pr = Pupillen (Bildpunkte), pl links im Bild."""
    if pl[0] > pr[0]:
        pl, pr = pr, pl
    z = zonen(img.shape[:2], pl, pr)
    aus = {"ipd_px": math.dist(pl, pr), "zonen": {n: farbe(img, m) for n, m in z.items()}}
    zl, zs = aus["zonen"]["wange_licht"], aus["zonen"]["wange_schatten"]
    aus["licht_schatten"] = zl["Y"] / max(zs["Y"], 1e-6)
    aus["oberlid_dL"] = aus["zonen"]["oberlid"]["L"] - zl["L"]
    aus["unterlid_dL"] = aus["zonen"]["unterlid"]["L"] - zl["L"]
    aus["kiefer_gegen_wange"] = aus["zonen"]["kiefer_licht"]["Y"] / max(zl["Y"], 1e-6) - 1.0
    aus["feinstruktur"] = feinstruktur(img, pl, pr)
    aus["augen"] = augen(img, pl, pr)
    return aus


# ------------------------------------------------------------------ Umriss, Pupillen (Abgleich Bild gegen Netz)
def grundfarbe(img):
    """Farbe des Hintergrunds aus den oberen Ecken (je 8 × 8 Bildpunkte, Median; unten liegt meist das Hemd)."""
    e = np.concatenate([img[:8, :8].reshape(-1, 3), img[:8, -8:].reshape(-1, 3)])
    return np.median(e, axis=0)


def vordergrund(img, schwelle=0.06):
    g = grundfarbe(img)
    return np.abs(img - g).max(axis=2) > schwelle


def umriss_breite(maske, y):
    """Breite des Vordergrunds in Zeile y (Bildpunkte, Teilpunkte über die Nachbarzeilen gemittelt)."""
    zeilen = []
    for yy in (int(np.floor(y)), int(np.ceil(y))):
        r = np.nonzero(maske[min(max(yy, 0), maske.shape[0] - 1)])[0]
        if len(r):
            zeilen.append(r.max() + 1 - r.min())
    return float(np.mean(zeilen)) if zeilen else float("nan")


def profil_vorn(maske, blick_rechts=True):
    """Vorderer Umriss eines Profilbilds: je Zeile die vorderste Spalte des Vordergrunds (Bildpunkte)."""
    h = maske.shape[0]
    z = np.full(h, np.nan)
    for y in range(h):
        r = np.nonzero(maske[y])[0]
        if len(r):
            z[y] = (r.max() + 1) if blick_rechts else r.min()
    return z


def pupille_finden(img, p, ipd_px):
    """Pupillenmitte im Bild: Schwerpunkt der dunkelsten 6 % im Kreis 0,08·IPD um die Schätzung p."""
    h, w = img.shape[:2]
    r = 0.08 * ipd_px
    x0, x1 = int(max(0, p[0] - r)), int(min(w, p[0] + r + 1))
    y0, y1 = int(max(0, p[1] - r)), int(min(h, p[1] + r + 1))
    Y = B.leuchtdichte(img[y0:y1, x0:x1])
    yy, xx = np.mgrid[y0:y1, x0:x1]
    im_kreis = np.hypot(xx + 0.5 - p[0], yy + 0.5 - p[1]) <= r
    grenze = np.percentile(Y[im_kreis], 6)
    sel = im_kreis & (Y <= grenze)
    return float((xx[sel] + 0.5).mean()), float((yy[sel] + 0.5).mean())


# ------------------------------------------------------------------ Vergleiche
def bildunterschied(a, b, maske=None):
    """Mittlere Abweichung je Kanal in 1/255 (und größte Kanalabweichung im Mittel)."""
    d = np.abs(a.astype(np.float64) - b.astype(np.float64)) * 255.0
    if maske is not None:
        d = d[maske]
    else:
        d = d.reshape(-1, 3)
    return {"mittel": float(d.mean()), "kanal_max": float(d.mean(axis=0).max()), "p99": float(np.percentile(d, 99))}


def flimmern(bilder, maske=None):
    """Mittlere Bildänderung (1/255) zwischen aufeinanderfolgenden Bildern."""
    return float(np.mean([bildunterschied(a, b, maske)["mittel"] for a, b in zip(bilder, bilder[1:])]))


def autokorrelation(feld, dx, dy):
    """Normierte Autokorrelation eines Felds bei Verschiebung (dx, dy) Bildpunkte."""
    f = feld - feld.mean()
    a = f[max(0, dy):f.shape[0] + min(0, dy), max(0, dx):f.shape[1] + min(0, dx)]
    b = f[max(0, -dy):f.shape[0] + min(0, -dy), max(0, -dx):f.shape[1] + min(0, -dx)]
    return float((a * b).mean() / max(f.var(), 1e-12))
