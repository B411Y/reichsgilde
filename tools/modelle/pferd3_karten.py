"""Zugpferd, Folge 3: neue Karten (Albedo, Normal, ORM) je Fell und Einsetzen in das Spiel-GLB (Netz, Haut, Clips bleiben byte-gleich).

Eigenbau mit numpy und Pillow, deterministisch (Saat fest). Fremdes Bildmaterial nur:
  - Normalkarte der Quelle "Horse" von henrysteve973 (CC BY 4.0, Horse_normal.png 4096^2, gleiche UV wie unser Fell): Muskel- und Formdetail,
  - Hufeisen-Farbe der Quelle (Horseshoe_baseColor.png, CC BY 4.0) wie bisher.
Alles Uebrige (Farben je Bereich, Huf, Wirbel, Haarstrich, Glanz, Schmutz, Maehne/Schweif) entsteht hier aus Lage und Normale der Ruhepose (= Stehpose) des Spiel-GLB.

    python -I tools/modelle/pferd3_karten.py <alt.glb> <braun|rappe|fuchs> <Horse_normal.png> <Horseshoe_baseColor.png> <aus.glb>
        [--ao=<ao.npy aus pferd3_ao.py>] [--bilder=<Ordner fuer Einzelkarten>] [--runde=1|2]

Atlas 2048 x 3072 wie bisher (Fell 2048^2 oben, Haar 1024^2 unten links, Hufeisen und Geschirr 512^2, Augenfeld 512^2); Normal 1024 x 1536 (JPEG 95, OpenGL),
ORM 512 x 768 (R Verdeckung, G Rauheit, B Metall, A Hufmaske). Albedo ohne Palette (echte Farbverlaeufe); Godot packt beim Import (embedded_image_handling=2) BC7/BC5 mit Mipmaps.
Koordinaten: Meter, y oben, +z nach vorn (Kopf), Mittellinie x = -0,05.
"""
import io
import json
import os
import struct
import sys
import types

import numpy as np
from PIL import Image

for _n in ("bpy", "bmesh", "mathutils"):                     # pferd2_spielrender liest das GLB mit numpy; Blender-Module hier nur als Attrappe
    if _n not in sys.modules:
        sys.modules[_n] = types.ModuleType(_n)
sys.modules["mathutils"].Vector = object
sys.modules["mathutils"].Matrix = object
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
_argv = sys.argv
sys.argv = sys.argv[:1]
import pferd2_spielrender as R  # noqa: E402
import pferd2_spiel as SP  # noqa: E402
sys.argv = _argv

Image.MAX_IMAGE_PIXELS = None
ARGS = sys.argv[1:]
POS = [a for a in ARGS if not a.startswith("--")]
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS if a.startswith("--"))
AW, AH, N = 2048, 3072, 2048
MX = -0.05                                                     # Mittellinie
SAAT = int(OPT.get("saat", 1480))


# ---------------------------------------------------------------- Hilfen
def glatt(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3 - 2 * t)


def band(x, a, b, w):
    """1 zwischen a und b, weiche Raender der Breite w."""
    return glatt((x - a) / w + 0.5) * glatt((b - x) / w + 0.5)


def srgb_lin(c):
    c = np.asarray(c, np.float32) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055) * 255.0


def unschaerfe(a, r):
    """Kastenunschaerfe (dreimal = fast Gauss), Rand wiederholt; a 2D oder 3D."""
    if r < 1:
        return a
    b = a.astype(np.float32)
    for _ in range(3):
        for ax in (0, 1):
            p = np.concatenate([np.take(b, [0] * r, ax), b, np.take(b, [-1] * r, ax)], ax)
            c = np.cumsum(p, ax, dtype=np.float64)
            z = np.concatenate([np.zeros_like(np.take(c, [0], ax)), c], ax)
            n = b.shape[ax]
            b = ((np.take(z, np.arange(2 * r + 1, 2 * r + 1 + n), ax) - np.take(z, np.arange(0, n), ax)) / (2 * r + 1)).astype(np.float32)
    return b


def rauschen(form, r, rng):
    a = unschaerfe(rng.standard_normal(form).astype(np.float32), r)
    return (a - a.mean()) / max(a.std(), 1e-6)


def fuellen(bild, maske, schritte):
    b = bild.copy()
    m = maske.copy()
    for _ in range(schritte):
        summe = np.zeros_like(b)
        zahl = np.zeros(m.shape, np.float32)
        for oy, ox in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            mm = np.roll(m, (oy, ox), (0, 1))
            bb = np.roll(b, (oy, ox), (0, 1))
            summe += bb * (mm[..., None] if b.ndim == 3 else mm)
            zahl += mm
        neu = (~m) & (zahl > 0)
        if not neu.any():
            break
        b[neu] = summe[neu] / (zahl[neu][:, None] if b.ndim == 3 else zahl[neu])
        m = m | neu
    return b


def rastern(Q, NQ, UV, F, b, h, u0=0.0, v0=0.0, su=1.0, sv=1.0, extra=None):
    """Dreiecke in ein Feld b x h rastern: je Texel Lage, Normale, dP/du, dP/dv, Maske (UV -> Feld: x = (u - u0) * su * b, y = (v - v0) * sv * h)."""
    lage = np.zeros((h, b, 3), np.float32)
    nrm = np.zeros((h, b, 3), np.float32)
    du = np.zeros((h, b, 3), np.float32)
    dv = np.zeros((h, b, 3), np.float32)
    ex = np.zeros((h, b), np.float32) if extra is not None else None
    maske = np.zeros((h, b), bool)
    px = np.stack([(UV[:, 0] - u0) * su * b, (UV[:, 1] - v0) * sv * h], 1)
    for tri in F:
        a, bb, c = px[tri]
        x0, x1 = int(np.floor(min(a[0], bb[0], c[0]))), int(np.ceil(max(a[0], bb[0], c[0])))
        y0, y1 = int(np.floor(min(a[1], bb[1], c[1]))), int(np.ceil(max(a[1], bb[1], c[1])))
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, b - 1), min(y1, h - 1)
        if x1 < x0 or y1 < y0:
            continue
        det = (bb[1] - c[1]) * (a[0] - c[0]) + (c[0] - bb[0]) * (a[1] - c[1])
        if abs(det) < 1e-12:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        l1 = ((bb[1] - c[1]) * (xs - c[0]) + (c[0] - bb[0]) * (ys - c[1])) / det
        l2 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / det
        l3 = 1 - l1 - l2
        innen = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        if not innen.any():
            continue
        yy, xx = (ys[innen] - 0.5).astype(int), (xs[innen] - 0.5).astype(int)
        w = np.stack([l1[innen], l2[innen], l3[innen]], 1)
        lage[yy, xx] = w @ Q[tri]
        nrm[yy, xx] = w @ NQ[tri]
        if ex is not None:
            ex[yy, xx] = w @ extra[tri]
        e1, e2 = Q[tri[1]] - Q[tri[0]], Q[tri[2]] - Q[tri[0]]
        d1, d2 = UV[tri[1]] - UV[tri[0]], UV[tri[2]] - UV[tri[0]]
        r = d1[0] * d2[1] - d2[0] * d1[1]
        if abs(r) > 1e-12:
            du[yy, xx] = (e1 * d2[1] - e2 * d1[1]) / r
            dv[yy, xx] = (e2 * d1[0] - e1 * d2[0]) / r
        maske[yy, xx] = True
    nrm /= np.maximum(np.linalg.norm(nrm, axis=-1, keepdims=True), 1e-9)
    return lage, nrm, du, dv, maske, ex


def richtung_pixel(D, du, dv, maske):
    """Weltrichtung D je Texel in Bildrichtung (dx, dy) umrechnen (kleinste Quadrate in dP/du, dP/dv)."""
    g11, g12, g22 = (du * du).sum(-1), (du * dv).sum(-1), (dv * dv).sum(-1)
    h1, h2 = (D * du).sum(-1), (D * dv).sum(-1)
    det = g11 * g22 - g12 * g12
    det = np.where(np.abs(det) < 1e-12, 1e-12, det)
    a = (g22 * h1 - g12 * h2) / det
    b = (g11 * h2 - g12 * h1) / det
    l = np.maximum(np.hypot(a, b), 1e-9)
    return np.where(maske, a / l, 1.0).astype(np.float32), np.where(maske, b / l, 0.0).astype(np.float32)


def lic(feld, dx, dy, laenge):
    h, b = feld.shape
    acc = np.zeros_like(feld)
    gew = 0.0
    yy, xx = np.mgrid[0:h, 0:b].astype(np.float32)
    for k in range(-laenge, laenge + 1):
        w = float(np.exp(-0.5 * (k / (laenge * 0.6)) ** 2))
        sx = np.clip(np.rint(xx + k * dx).astype(np.int32), 0, b - 1)
        sy = np.clip(np.rint(yy + k * dy).astype(np.int32), 0, h - 1)
        acc += w * feld[sy, sx]
        gew += w
    return acc / gew


def hoehe_zu_normal(hh, staerke):
    gx = (np.roll(hh, -1, 1) - np.roll(hh, 1, 1)) * 0.5
    gy = (np.roll(hh, 1, 0) - np.roll(hh, -1, 0)) * 0.5           # OpenGL: Gruen nach oben
    n = np.stack([-staerke * gx, -staerke * gy, np.ones_like(hh)], -1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def normal_mischen(basis, detail):
    """Detail auf Basis (beide Tangentenraum, z > 0): Whiteout-Mischung."""
    n = np.stack([basis[..., 0] + detail[..., 0], basis[..., 1] + detail[..., 1], basis[..., 2] * detail[..., 2]], -1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


# ---------------------------------------------------------------- Felle (sRGB, Ziel-Albedo; Phase-1-Tabelle)
FELLE = {
    "braun": dict(grund=(100, 55, 33), ober=0.80, bauch=(128, 86, 56), punkte=(30, 24, 22), punkte_an=1.0, ohr=(32, 25, 22), maul=(40, 31, 28),
                  haar_wurzel=(22, 18, 17), haar_spitze=(52, 38, 30), huf=(48, 47, 50), rau=0.66, glanz=0.56),
    "rappe": dict(grund=(28, 25, 26), ober=0.86, bauch=(44, 37, 33), punkte=(27, 25, 26), punkte_an=0.6, ohr=(25, 23, 24), maul=(34, 30, 30),
                  haar_wurzel=(17, 16, 18), haar_spitze=(38, 33, 33), huf=(44, 43, 46), rau=0.58, glanz=0.47),
    "fuchs": dict(grund=(124, 68, 40), ober=0.84, bauch=(150, 98, 64), punkte=(98, 54, 33), punkte_an=0.5, ohr=(104, 57, 34), maul=(62, 50, 46),
                  haar_wurzel=(78, 40, 24), haar_spitze=(108, 60, 36), huf=(52, 44, 38), rau=0.66, glanz=0.56),
}
GLB_NAME = {"braun": "rotbraun", "rappe": "schwarzbraun", "fuchs": "fuchs"}
BEINE = {  # Bodenmitte des Hufs (x, z), Knie/Sprung, Fesselgelenk (Ruhepose, gemessen)
    "LF": dict(x=0.052, z=0.509, knie=0.486, fessel=0.154, vorn=True),
    "RF": dict(x=-0.147, z=0.528, knie=0.486, fessel=0.152, vorn=True),
    "LH": dict(x=0.085, z=-0.658, knie=0.59, fessel=0.17, vorn=False),
    "RH": dict(x=-0.183, z=-0.654, knie=0.59, fessel=0.17, vorn=False),
}
AUGEN = [np.array([0.03, 1.712, 1.27]), np.array([-0.126, 1.712, 1.271])]
NUESTERN = [np.array([0.003, 1.433, 1.47]), np.array([-0.098, 1.433, 1.47])]
OHREN = [(np.array([0.017, 1.837, 1.187]), np.array([0.049, 1.945, 1.265])), (np.array([-0.114, 1.837, 1.187]), np.array([-0.145, 1.945, 1.265]))]


def bein_zuordnung(P):
    """Naechstes Bein je Texel (nach x und z) und Abstand in der Waagerechten zur Hufmitte."""
    best = np.full(P.shape[:-1], 9.0, np.float32)
    welches = np.zeros(P.shape[:-1], np.int8)
    for i, b in enumerate(BEINE.values()):
        d = np.hypot(P[..., 0] - b["x"], P[..., 2] - b["z"])
        neu = d < best
        best[neu], welches[neu] = d[neu], i
    return welches, best


def punkt_abstand(P, p):
    return np.linalg.norm(P - p, axis=-1)


def strecke_abstand(P, a, b):
    ab = b - a
    t = np.clip(((P - a) * ab).sum(-1) / (ab @ ab), 0, 1)
    return np.linalg.norm(P - (a + t[..., None] * ab), axis=-1), t


def bereiche(P, Nn, m):
    """Weiche Masken je Bereich (0..1) im Fellfeld."""
    x, y, z = P[..., 0], P[..., 1], P[..., 2]
    welches, dist = bein_zuordnung(P)
    B = {}
    beinbereich = (y < 1.0) & ((z > 0.25) | (z < -0.35)) & m
    # Huf: unter dem Kronrand (vorn 10,5 cm, Trachten 5,5 cm), Kronrand 1,2 cm hell-wachsig, Sohle (Normale nach unten)
    kron = np.zeros_like(y)
    for i, b in enumerate(BEINE.values()):
        f = np.clip((z - b["z"]) / 0.07, -1, 1)
        kron = np.where(welches == i, 0.080 + 0.025 * f, kron)
    B["huf"] = glatt((kron - y) / 0.006 + 0.5) * beinbereich
    B["kronrand"] = band(kron - y, 0.0, 0.007, 0.003) * beinbereich
    B["sohle"] = B["huf"] * glatt((-Nn[..., 1] - 0.55) / 0.2)
    B["haar_kron"] = band(y - kron, 0.0, 0.025, 0.01) * beinbereich       # Haarkranz ueber dem Kronrand (Saum)
    # Unterbein (Abzeichen "points" beim Braunen): vorn bis ueber das Knie, hinten bis zum Sprung; Rueckseite etwas hoeher
    grenze = np.zeros_like(y)
    for i, b in enumerate(BEINE.values()):
        hinten = glatt(((b["z"] - z) if b["vorn"] else (b["z"] - z)) / 0.1)
        grenze = np.where(welches == i, b["knie"] + (0.06 if b["vorn"] else 0.03) + 0.05 * hinten, grenze)
    grenze = grenze + 0.025 * np.sin(z * 37.0 + x * 23.0) * np.sin(x * 51.0 - z * 13.0)   # Rand leicht unregelmaessig
    t = np.clip((grenze - y) / 0.30 + 0.5, 0, 1)                         # 15 cm unter bis 15 cm ueber der Grenze
    B["unterbein"] = (t * t * (3 - 2 * t)) ** 1.6 * beinbereich             # unten voll, nach oben lang und weich auslaufend (keine Kante)
    B["fessel"] = np.zeros_like(y)
    for i, b in enumerate(BEINE.values()):
        B["fessel"] = np.where(welches == i, band(y, 0.06, b["fessel"] + 0.05, 0.03), B["fessel"])
    B["fessel"] *= beinbereich
    # Kastanien: innen am Bein, vorn 8 cm ueber dem Knie, hinten 9 cm unter dem Sprung (hinten kleiner)
    # Lage aus dem Netz: Beinmitte in dieser Hoehe, Punkt auf der Innenseite (der Mitte am naechsten); Oval auf der Oberflaeche, weicher Rand
    kast = np.zeros_like(y)
    for i, b in enumerate(BEINE.values()):
        mitte_y = b["knie"] + 0.08 if b["vorn"] else b["knie"] - 0.09
        ry, rh = (0.034, 0.020) if b["vorn"] else (0.022, 0.013)
        ring = m & (welches == i) & (np.abs(y - mitte_y) < 0.012) & beinbereich
        if not ring.any():
            continue
        cz = float(z[ring].mean())
        richtung = np.sign(MX - b["x"])
        xi = float((x[ring] * richtung).max() * richtung)                # innerster Punkt (zur Mittellinie)
        dh = np.hypot(x - xi, z - cz)
        e = ((y - mitte_y) / ry) ** 2 + (dh / rh) ** 2
        kast = np.where(welches == i, glatt((1.0 - e) / 0.7), kast)
    B["kastanie"] = kast * beinbereich
    # Rumpf: Oberlinie (Ruecken, Mähnenkamm), Bauch (unten zwischen den Beinen), Glanz Kruppe/Schulter/Hals
    B["ober"] = glatt((Nn[..., 1] - 0.55) / 0.3) * glatt((y - 1.15) / 0.15) * m
    B["bauch"] = glatt((-Nn[..., 1] - 0.25) / 0.35) * band(y, 0.62, 1.12, 0.12) * band(z, -0.5, 0.42, 0.12) * m
    seite = glatt((np.abs(Nn[..., 0]) - 0.2) / 0.4)
    kruppe = band(z, -0.95, -0.25, 0.15) * glatt((y - 1.0) / 0.15) * glatt((Nn[..., 1] + 0.1) / 0.4)
    schulter = band(z, 0.25, 0.65, 0.12) * band(y, 0.85, 1.38, 0.12) * seite
    hals = band(z, 0.55, 1.05, 0.15) * band(y, 1.2, 1.75, 0.15) * seite
    B["glanz"] = np.clip(np.maximum(np.maximum(kruppe, schulter), 0.7 * hals), 0, 1) * m
    # Kopf: Maul/Nuestern fast haarlos (dunkle Haut), Augenumgebung (Lid), Ohren (innen, Rand)
    kopf = glatt((z - 1.05) / 0.08) * glatt((y - 1.2) / 0.08) * m
    maul = glatt((z - 1.30) / 0.07) * glatt((1.53 - y) / 0.06) * kopf
    nues = np.zeros_like(y)
    for p in NUESTERN:
        nues = np.maximum(nues, glatt((0.030 - punkt_abstand(P, p)) / 0.012))
        B.setdefault("nues_innen", np.zeros_like(y))
        B["nues_innen"] = np.maximum(B["nues_innen"], glatt((0.016 - punkt_abstand(P, p)) / 0.008))
    B["maul"] = np.clip(maul, 0, 1)
    B["nuestern"] = nues * kopf
    B["nues_innen"] = B["nues_innen"] * kopf
    lid = np.zeros_like(y)
    for p in AUGEN:
        lid = np.maximum(lid, glatt((0.042 - punkt_abstand(P, p)) / 0.012))
    B["lid"] = lid * kopf
    ohr_innen = np.zeros_like(y)
    ohr_rand = np.zeros_like(y)
    for a, b in OHREN:
        d, t = strecke_abstand(P, a, b)
        im_ohr = glatt((0.05 - d) / 0.015) * glatt((y - 1.84) / 0.02)
        vorn = glatt((Nn[..., 2] - 0.1) / 0.3)
        ohr_innen = np.maximum(ohr_innen, im_ohr * vorn * glatt((t - 0.15) / 0.2))
        ohr_rand = np.maximum(ohr_rand, im_ohr * (1 - vorn) * glatt((t - 0.55) / 0.25))
    B["ohr_innen"] = ohr_innen * m
    B["ohr_rand"] = ohr_rand * m
    B["kopf"] = kopf
    # Schmutz (fest schwach): Unterbeine von unten, Bauch und Brust unten, Kniegelenk innen
    B["schmutz"] = np.clip(glatt((0.32 - y) / 0.25) * beinbereich + 0.5 * B["bauch"] * glatt((-Nn[..., 1] - 0.5) / 0.3), 0, 1) * (1 - B["huf"] * 0.6) * m
    return B


def wuchsrichtung(P, Nn):
    """Haarstrich (Weltrichtung): Rumpf nach hinten unten, Beine nach unten, Kopf zur Nase; Wirbel an Stirn, Hals oben (paarig), Flanke (Flankenstrom) und Brust."""
    x, y, z = P[..., 0], P[..., 1], P[..., 2]
    D = np.zeros_like(P)
    D[..., 2], D[..., 1] = -1.0, -0.45
    bein = glatt((0.95 - y) / 0.25) * glatt((np.abs(z) - 0.2) / 0.15)
    D = D * (1 - bein)[..., None] + np.array([0.0, -1.0, -0.1], np.float32) * bein[..., None]
    kopf = glatt((z - 1.05) / 0.12) * glatt((y - 1.25) / 0.1)
    D = D * (1 - kopf)[..., None] + np.array([0.0, -0.45, 1.0], np.float32) * kopf[..., None]
    wirbel = [((MX, 1.78, 1.32), 0.06, 1.0),                         # Stirn (zwischen den Augen)
              ((0.09, 1.0, -0.28), 0.12, -1.0), ((MX - 0.14, 1.0, -0.28), 0.12, 1.0),   # Flanke
              ((0.05, 1.62, 0.95), 0.06, 1.0), ((MX - 0.10, 1.62, 0.95), 0.06, -1.0),     # Hals oben, paarig
              ((MX, 1.12, 0.78), 0.07, 1.0)]                                              # Brust
    for c, r, s in wirbel:
        c = np.array(c, np.float32)
        d = P - c
        w = np.exp(-(np.linalg.norm(d, axis=-1) / r) ** 2)
        rot = s * np.cross(Nn, d)                                      # Drehung um die Flaechennormale
        rot /= np.maximum(np.linalg.norm(rot, axis=-1, keepdims=True), 1e-6)
        D = D * (1 - w)[..., None] + rot * w[..., None]
    D = D - (D * Nn).sum(-1, keepdims=True) * Nn
    return D / np.maximum(np.linalg.norm(D, axis=-1, keepdims=True), 1e-9)


# ---------------------------------------------------------------- Fellfeld
def fell_feld(p, Q, NQ, fell_tri, fell_name, quelle_normal, ao):
    rng = np.random.default_rng(SAAT)
    F = p.F[fell_tri]
    P, Nn, du, dv, m, _ = rastern(Q, NQ, p.UV, F, N, N, 0, 0, 1, AH / AW)
    B = bereiche(P, Nn, m)
    f = FELLE[fell_name]
    lin = lambda c: srgb_lin(c)[None, None, :]
    # Haarstrich
    D = wuchsrichtung(P, Nn)
    dx, dy = richtung_pixel(D, du, dv, m)
    grob = lic(rng.standard_normal((N, N)).astype(np.float32), dx, dy, 14)
    fein = lic(rng.standard_normal((N, N)).astype(np.float32), dx, dy, 5)
    strich = (grob / max(grob.std(), 1e-6)) * 0.6 + (fein / max(fein.std(), 1e-6)) * 0.4
    flecken = rauschen((N, N), 40, rng)                               # grosse, weiche Farbschwankung
    # Albedo (linear)
    A = np.broadcast_to(lin(f["grund"]), (N, N, 3)).copy()
    A *= (1 + 0.05 * flecken)[..., None]
    A = A * (1 - (1 - f["ober"]) * B["ober"])[..., None]
    A = A * (1 - 0.6 * B["bauch"])[..., None] + lin(f["bauch"]) * (0.6 * B["bauch"])[..., None]
    A = A * (1 - f["punkte_an"] * B["unterbein"])[..., None] + lin(f["punkte"]) * (f["punkte_an"] * B["unterbein"])[..., None]
    if fell_name == "braun":
        A = A * (1 - B["ohr_rand"])[..., None] + lin(f["ohr"]) * B["ohr_rand"][..., None]
    A = A * (1 - B["ohr_innen"] * 0.55)[..., None] + lin((150, 132, 112)) * (B["ohr_innen"] * 0.55)[..., None]
    haut = np.clip(B["maul"] * 0.95 + B["nuestern"] + B["lid"] * 0.75, 0, 1)
    A = A * (1 - haut)[..., None] + lin(f["maul"]) * haut[..., None]
    A *= (1 - 0.65 * B["nues_innen"])[..., None]
    poren = rauschen((N, N), 1, rng)
    A *= (1 + 0.08 * poren * haut)[..., None]
    bueschel = lic(rng.standard_normal((N, N)).astype(np.float32), dx, dy, 9)
    A *= (1 + 0.25 * B["ohr_innen"] * bueschel / max(bueschel.std(), 1e-6))[..., None]
    # Haarstrich in der Farbe (sichtbar nah, im Spiel als ruhige Struktur)
    A *= (1 + 0.06 * strich * (1 - haut) * (1 - B["huf"]))[..., None]
    # Kastanien
    kast_farbe = lin((72, 64, 56)) * (1 + 0.18 * rauschen((N, N), 1, rng))[..., None]   # grau-braunes Horn, rau
    A = A * (1 - 0.9 * B["kastanie"])[..., None] + kast_farbe * (0.9 * B["kastanie"])[..., None]
    # Huf: Horn mit Laengsstreifen, Wachstumsringen; Kronrand wachsig heller; Sohle grau-gelblich
    welches, _ = bein_zuordnung(P)
    winkel = np.zeros_like(P[..., 0])
    for i, b in enumerate(BEINE.values()):
        winkel = np.where(welches == i, np.arctan2(P[..., 0] - b["x"], P[..., 2] - b["z"]), winkel)
    faser_tab = rng.standard_normal(4096).astype(np.float32)                # Laengsfasern: 1D-Rauschen ueber den Umfang, senkrecht durchlaufend
    faser_tab = np.convolve(np.concatenate([faser_tab[-3:], faser_tab, faser_tab[:3]]), np.ones(3) / 3, "same")[3:-3]
    faser_tab /= faser_tab.std()
    k = ((winkel + np.pi) / (2 * np.pi) * 4096 + 9.0 * np.sin(P[..., 1] * 31.0 + winkel * 3)).astype(np.int64) % 4096
    streifen = 0.7 * faser_tab[k] + 0.3 * faser_tab[(k * 7 + 1234) % 4096]
    ringe = 0.5 * np.sin(P[..., 1] * 2 * np.pi / 0.021 + 1.5 * np.sin(winkel * 2)) + 0.5 * np.sin(P[..., 1] * 2 * np.pi / 0.013)   # schwache Querwellen
    horn = lin(f["huf"]) * (1 + 0.16 * streifen + 0.04 * ringe)[..., None]
    A = A * (1 - B["huf"])[..., None] + horn * B["huf"][..., None]
    A = A * (1 - 0.5 * B["kronrand"])[..., None] + lin((72, 66, 60)) * (0.5 * B["kronrand"])[..., None]
    A = A * (1 - 0.5 * B["haar_kron"])[..., None] + A * (0.8 + 0.4 * glatt(strich * 0.5 + 0.5))[..., None] * (0.5 * B["haar_kron"])[..., None]
    A = A * (1 - B["sohle"])[..., None] + lin((118, 104, 84)) * B["sohle"][..., None]
    # Verdeckung (gebacken) leicht in die Albedo (Falten, Achsel), Schmutz fest schwach
    if ao is not None:
        A *= (0.72 + 0.28 * ao)[..., None]
    dichte = glatt((0.30 - P[..., 1]) / 0.25)                               # unten dichter
    sp = rauschen((N, N), 1, rng) + 0.6 * rauschen((N, N), 2, rng)
    spritzer = glatt((sp - (2.6 - 1.2 * dichte)) / 0.35) * dichte
    staub = np.clip(B["schmutz"] * 0.35 * (0.7 + 0.3 * glatt(flecken * 0.5 + 0.5)) + 0.45 * spritzer * B["schmutz"], 0, 0.8)
    A = A * (1 - staub)[..., None] + lin((96, 86, 72)) * staub[..., None]
    A = fuellen(A, m, 40)
    # Normal: Quelle (Form) + Haarstrich + Hufringe
    qn = quelle_normal
    hoehe = strich * 0.5 * (1 - haut * 0.7) * (1 - B["huf"]) + (ringe * 0.25 + streifen * 0.6) * B["huf"]
    hoehe += B["kastanie"] * 3.0 + rauschen((N, N), 2, rng) * B["kastanie"] * 0.8
    hoehe += poren * haut * 0.6 - B["nues_innen"] * 4.0 + spritzer * B["schmutz"] * 1.5
    det = hoehe_zu_normal(hoehe, float(OPT.get("strich_staerke", 0.28)))
    Nm = normal_mischen(qn, det)
    Nm = fuellen(Nm, m, 40)
    # Rauheit je Bereich
    r = np.full((N, N), f["rau"], np.float32)
    r += 0.08 * B["bauch"]
    r = r * (1 - B["glanz"]) + f["glanz"] * B["glanz"]
    r = r * (1 - B["huf"]) + 0.42 * B["huf"]
    r = r * (1 - B["sohle"]) + 0.7 * B["sohle"]
    r = r * (1 - B["maul"]) + 0.42 * B["maul"]
    r = r * (1 - B["nuestern"]) + 0.28 * B["nuestern"]
    r = r * (1 - B["nues_innen"]) + 0.18 * B["nues_innen"]                # feucht
    r = r * (1 - B["lid"] * 0.6) + 0.35 * B["lid"] * 0.6
    r = r * (1 - B["kastanie"]) + 0.75 * B["kastanie"]
    r = np.clip(r + 0.2 * staub - 0.07 * strich * (1 - B["huf"]) * (1 - haut), 0.05, 1)   # Rauheit schwankt mit dem Haarstrich: Glanz zerfaellt in Haarlinien
    r = fuellen(r, m, 40)
    ao_f = fuellen(ao, m, 40) if ao is not None else np.ones((N, N), np.float32)
    huf_m = fuellen(B["huf"].astype(np.float32), m, 40)
    return A, Nm, r, ao_f, m, B, huf_m


def quellnormal_laden(pfad):
    q = Image.open(pfad).convert("RGB").resize((N, N), Image.BOX)
    n = np.asarray(q).astype(np.float32) / 127.5 - 1.0
    st = float(OPT.get("quelle_staerke", 1.0))
    n[..., :2] *= st
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


# ---------------------------------------------------------------- Haarfeld (Maehne, Schweif)
def haar_feld(p, Q, NQ, haar_tri, fell_ecken, fell_name, alpha_alt):
    """Maehne/Schweif: Farbe von der Wurzel (dunkel, dicht) zur Spitze (heller, duenner), Straehnen aus der Alpha-Karte (Form bleibt)."""
    rng = np.random.default_rng(SAAT + 7)
    F = p.F[haar_tri]
    ecken = np.unique(F)
    # Wurzelabstand je Ecke = Abstand zur naechsten Fellecke (Maehnenkamm, Schweifruebe)
    FQ = Q[fell_ecken]
    d = np.zeros(len(Q), np.float32)
    for s in range(0, len(ecken), 256):
        e = ecken[s:s + 256]
        d[e] = np.sqrt(((Q[e][:, None, :] - FQ[None, :, :]) ** 2).sum(-1)).min(1)
    schweif = Q[:, 2] < -0.5
    t = np.where(schweif, d / 0.75, d / 0.30).clip(0, 1)
    P, Nn, du, dv, m, T = rastern(Q, NQ, p.UV, F, 1024, 1024, 0, 2048 / AH, AW / 1024, AH / 1024, extra=t)
    T = fuellen(T[..., None], m, 30)[..., 0]
    f = FELLE[fell_name]
    a = alpha_alt.astype(np.float32) / 255.0
    # Straehnen-Hoehe aus der Alpha (weichgezeichnet) und feinem Rauschen entlang der Straehne
    weich = unschaerfe(a, 2)
    var = rauschen((1024, 1024), 3, rng)
    roh = rng.standard_normal((1024, 1024)).astype(np.float32)
    zug = np.cumsum(np.pad(roh, ((0, 0), (40, 0)), mode="wrap"), 1)
    laengs = (zug[:, 40:] - zug[:, :-40]) / 40.0                             # 40 Texel laengs gemittelt (Straehnen liegen waagerecht im Haarfeld)
    laengs = unschaerfe(laengs, 1)
    laengs = (laengs - laengs.mean()) / max(laengs.std(), 1e-6)
    farbe = srgb_lin(f["haar_wurzel"])[None, None, :] * (1 - T[..., None]) + srgb_lin(f["haar_spitze"])[None, None, :] * T[..., None]
    farbe *= (1 + 0.16 * laengs + 0.05 * var + 0.10 * (weich - 0.5))[..., None]
    hoehe = weich * 1.2 + laengs * 0.45 + var * 0.1
    nrm = hoehe_zu_normal(hoehe, 1.2)
    rau = (0.52 + 0.16 * T + 0.05 * var).astype(np.float32)
    return farbe, nrm, rau, a


# ---------------------------------------------------------------- GLB schreiben (nur Bilder tauschen)
def glb_bilder_tauschen(alt, neu, bilder):
    """bilder: Liste (bytes, mime) fuer images[0..]; alle uebrigen Puffer bleiben byte-gleich (Lage neu berechnet)."""
    raw = open(alt, "rb").read()
    jl = struct.unpack("<I", raw[12:16])[0]
    j = json.loads(raw[20:20 + jl])
    binl = struct.unpack("<I", raw[20 + jl:24 + jl])[0]
    b = raw[28 + jl:28 + jl + binl]
    ersatz = {j["images"][i]["bufferView"]: (dat, mime) for i, (dat, mime) in enumerate(bilder)}
    out = bytearray()
    for k, bv in enumerate(j["bufferViews"]):
        if k in ersatz:
            dat = ersatz[k][0]
        else:
            o = bv.get("byteOffset", 0)
            dat = b[o:o + bv["byteLength"]]
        while len(out) % 4:
            out += b"\0"
        bv["byteOffset"] = len(out)
        bv["byteLength"] = len(dat)
        out += dat
    for i, (_, mime) in enumerate(bilder):
        j["images"][i]["mimeType"] = mime
    while len(out) % 4:
        out += b"\0"
    j["buffers"][0]["byteLength"] = len(out)
    js = json.dumps(j, separators=(",", ":")).encode()
    while len(js) % 4:
        js += b" "
    ges = 12 + 8 + len(js) + 8 + len(out)
    with open(neu, "wb") as fh:
        fh.write(struct.pack("<III", 0x46546C67, 2, ges))
        fh.write(struct.pack("<I", len(js)) + b"JSON" + js)
        fh.write(struct.pack("<I", len(out)) + b"BIN\0" + bytes(out))


def png(bild):
    bio = io.BytesIO()
    bild.save(bio, "PNG", optimize=True)
    return bio.getvalue()


def main():
    alt, fell_name, qnormal, schuh, aus = POS[:5]
    p = R.Pferd(alt)
    Q, NQ = p.pose(None, 0)
    UV = p.UV
    tv = UV[p.F].mean(1)                                               # Dreiecksmitte im UV
    fell_tri = tv[:, 1] < 2048 / AH
    haar_tri = (tv[:, 1] >= 2048 / AH) & (tv[:, 0] < 1024 / AW)
    fell_ecken = np.unique(p.F[fell_tri])
    ao = None
    if "ao" in OPT:
        ao = np.load(OPT["ao"]).astype(np.float32)
        ao = np.asarray(Image.fromarray(ao, "F").resize((N, N), Image.BILINEAR)).astype(np.float32)
    A, Nm, r, ao_f, m, B, huf_m = fell_feld(p, Q, NQ, fell_tri, fell_name, quellnormal_laden(qnormal), ao)
    # alter Atlas: Alpha der Maehne, Augenfeld, Geschirr-UV-Lage bleiben
    altatlas = Image.open(io.BytesIO(p.bild)).convert("RGBA")
    alt_a = np.asarray(altatlas)
    hf, hn, hr, ha = haar_feld(p, Q, NQ, haar_tri, fell_ecken, fell_name, alt_a[2048:3072, 0:1024, 3])
    # Albedo-Atlas
    atl = np.zeros((AH, AW, 4), np.float32)
    atl[:N, :N, :3] = A
    atl[:N, :N, 3] = 1.0
    atl[2048:3072, 0:1024, :3] = hf
    atl[2048:3072, 0:1024, 3] = ha
    s_bild = Image.open(schuh).convert("RGB").resize((512, 512), Image.LANCZOS)
    atl[2048:2560, 1024:1536, :3] = srgb_lin(np.asarray(s_bild))
    atl[2048:2560, 1024:1536, 3] = 1.0
    atl[2048:2560, 1536:2048, :3] = srgb_lin((16, 11, 9))                # Auge: sehr dunkles Braun, Glanz ueber die Rauheit
    atl[2048:2560, 1536:2048, 3] = 1.0
    gf, gh, gr, gm = SP.geschirr_felder()
    atl[2560:3072, 1024:1536, :3] = srgb_lin(np.asarray(gf)[..., :3])
    atl[2560:3072, 1024:1536, 3] = 1.0
    atl[2560:3072, 1536:2048] = atl[2048:2560, 1536:2048]
    rgb = lin_srgb(atl[..., :3])
    alb = np.dstack([rgb, atl[..., 3:] * 255.0])
    alb_bild = Image.fromarray(np.clip(alb + 0.5, 0, 255).astype(np.uint8), "RGBA")
    # Normal-Atlas (voll, dann halbiert)
    nrm = np.zeros((AH, AW, 3), np.float32)
    nrm[..., 2] = 1.0
    nrm[:N, :N] = Nm
    nrm[2048:3072, 0:1024] = hn
    st = 0.35
    gx = (np.roll(gh, -1, 1) - np.roll(gh, 1, 1)) / 2
    gy = (np.roll(gh, 1, 0) - np.roll(gh, -1, 0)) / 2
    n_ = np.stack([-st * gx, -st * gy, np.ones_like(gh)], -1)
    nrm[2560:3072, 1024:1536] = n_ / np.linalg.norm(n_, axis=-1, keepdims=True)
    n_bild = Image.fromarray(np.clip((nrm * 0.5 + 0.5) * 255 + 0.5, 0, 255).astype(np.uint8)).resize((AW // 2, AH // 2), Image.BOX)
    # ORM
    orm = np.zeros((AH, AW, 4), np.float32)
    orm[..., 0], orm[..., 1] = 1.0, 0.85
    orm[:N, :N, 3] = huf_m                                               # A: Hufmaske (tier_abzeichen.gdshaderinc: Huf unter weissem Bein hell statt weiss)
    orm[:N, :N, 0] = 0.55 + 0.45 * ao_f
    orm[:N, :N, 1] = r
    orm[2048:3072, 0:1024, 1] = hr
    orm[2048:2560, 1024:1536, 1], orm[2048:2560, 1024:1536, 2] = 0.42, 0.75   # Hufeisen
    orm[2048:3072, 1536:2048, 1] = 0.05                                  # Auge nass
    orm[2560:3072, 1024:1536, 1], orm[2560:3072, 1024:1536, 2] = gr, gm
    o8 = np.clip(orm * 255 + 0.5, 0, 255).astype(np.uint8)              # Kanaele einzeln verkleinern (Pillow wuerde RGBA mit Alpha vormultiplizieren)
    o_bild = Image.merge("RGBA", [Image.fromarray(o8[..., k], "L").resize((AW // 4, AH // 4), Image.BOX) for k in range(4)])
    if "bilder" in OPT:
        os.makedirs(OPT["bilder"], exist_ok=True)
        alb_bild.save(os.path.join(OPT["bilder"], f"albedo_{fell_name}.png"))
        n_bild.save(os.path.join(OPT["bilder"], f"normal_{fell_name}.png"))
        o_bild.save(os.path.join(OPT["bilder"], f"orm_{fell_name}.png"))
        mk = np.zeros((N, N, 3), np.float32)
        for k, c in (("huf", (1, 0.8, 0)), ("unterbein", (0.3, 0.3, 1)), ("kastanie", (1, 0, 0)), ("bauch", (0, 1, 0)), ("glanz", (1, 1, 1)),
                     ("maul", (1, 0, 1)), ("lid", (0, 1, 1)), ("ohr_innen", (1, 0.5, 0.5)), ("kronrand", (0.5, 1, 0.5)), ("schmutz", (0.6, 0.4, 0.2))):
            mk = np.maximum(mk, B[k][..., None] * np.array(c, np.float32))
        Image.fromarray((mk * 255).astype(np.uint8)).resize((1024, 1024)).save(os.path.join(OPT["bilder"], f"bereiche_{fell_name}.png"))
    nb = io.BytesIO()
    n_bild.save(nb, "JPEG", quality=95)                                # Normalkarte als JPEG wie bisher (Godot packt sie ohnehin neu: BC5)
    glb_bilder_tauschen(alt, aus, [(png(alb_bild), "image/png"), (nb.getvalue(), "image/jpeg"), (png(o_bild), "image/png")])
    print(aus, os.path.getsize(aus))


if __name__ == "__main__":
    main()
