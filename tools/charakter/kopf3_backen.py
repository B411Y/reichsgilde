"""Charakter Phase 3: Haut des Basiskopfs backen (eigener Code, MIT; läuft in Blender wegen BVH, ohne MPFB).

Aufruf:  tools/charakter/blender.sh -b --factory-startup -P tools/charakter/kopf3_backen.py -- <backdaten.npz> <ausgabe> [groesse=2048]
         (oder tools/charakter/kopf3.sh)

Eingang: die Backdaten des Baus (kopf2_bauen.py backdaten=…): Hautnetz mit Spiel-UV, Lage jeder Ecke auf der
MakeHuman-Haut, MakeHuman-Netz mit dessen UV, Merkpunkte. Ausgang (je groesse², linear, Zeile 0 = oben):
  haut_farbe.png   RGB = Hautbild geteilt durch die mittlere Hautfarbe, × 0,5 (der Ton kommt im Shader aus den
                   Genen), A = Brauenschatten (Wurzeldichte der Braue auf der Haut)
  haut_normal.png  RG = Normale (Tangentenraum, OpenGL: +x = +u, +y = +v), B = Porenstärke für die feine Kachel
  haut_masken.png  R = Rötung, G = Glanz, B = Verdeckung (Höhlen), A = Bartschatten (Bereich)
  haut_falten.png  Höhen der Falten (0,5 = keine, kleiner = Furche): R = Stirn und Zornesfalten, G = Augen
                   (Krähenfüße, Unterlid), B = Nasolabial- und Marionettenfalten, A = Hals und Lippenfältchen
  iris_muster.png  RoboPoets „iris_color_mask“ R/G (CC0): R = Fasern, G = innerer Kranz
  lederhaut.png    RoboPoets „sclera_color“ (CC0), Äderchen
Hautbild: MakeHuman „young_caucasian_female“ (CC0, Teil von makehuman_system_assets), über die MakeHuman-UV auf die
Spiel-UV übertragen; Kopfhaut (Stoppeln) und Lidränder (gemalte Wimpern) werden durch Haut ersetzt. Falten, Poren,
Lippenlinien, Masken: eigene Rechenregeln aus den Merkpunkten (keine Fremddaten).
Achsen: Blender (z oben, Gesicht nach -y, +x = linke Seite der Figur), Meter.
"""
import math
import os
import sys
import time

import numpy as np
import OpenImageIO as oiio
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import kopf3_haartexturen as kht  # noqa: E402

arg = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
NPZ, AUS = arg[0], arg[1]
OPT = dict(a.split("=", 1) for a in arg[2:])
G = int(OPT.get("groesse", "2048"))
CH = os.environ.get("CHARAKTER", os.path.expanduser("~/werkzeuge/charakter"))
HAUTBILD = os.path.join(CH, "mpfb_daten", "data", "skins", "young_caucasian_female", "young_lightskinned_female_diffuse.png")
ROBO = os.path.join(CH, "robopoets", "src", "textures")
t0 = time.time()
rng = np.random.default_rng(1480)


def zeit(text):
    print(f"[{time.time() - t0:6.1f} s] {text}", flush=True)


def glatt(x, a, b):
    t = np.clip((np.asarray(x, dtype=np.float64) - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


d = np.load(NPZ)
HE = d["h_ecken"]
TRI = d["h_tri"]
TRI_UV = d["h_tri_uv"]
TRI_ST = d["h_tri_stoff"]
HERK = d["herkunft"]
P = d["mpfb"]
MH_TRI = d["mh_tri"]
MH_UV = d["mh_tri_uv"]
N_HAUT = int(d["n_haut"])
MH0, MH1 = (int(x) for x in d["mundhoehle"])
AH = d["augenhoehlen"]
M68 = d["merk68"]
AUGE = {"l": d["auge_l"], "r": d["auge_r"]}
LID = {"l": d["lidnaht_l"], "r": d["lidnaht_r"]}
LIPPE = d["lippennaht"]
AA = float(np.linalg.norm(AUGE["l"][:3] - AUGE["r"][:3]))        # Augenabstand (≈ 60 mm)
MASS = AA / 0.060                                                 # Maßstab gegen einen Erwachsenen
os.makedirs(AUS, exist_ok=True)

# ================================================================= 1. Spiel-UV rastern
tid = np.full((G, G), -1, np.int32)
bc = np.zeros((G, G, 3), np.float32)
for k in range(len(TRI_UV)):
    q = TRI_UV[k] * G
    x0, y0 = np.floor(q.min(0) - 0.5).astype(int)
    x1, y1 = np.ceil(q.max(0) - 0.5).astype(int)
    x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, G - 1), min(y1, G - 1)
    if x1 < x0 or y1 < y0:
        continue
    X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    (ax, ay), (bx, by), (cx, cy) = q
    den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(den) < 1e-12:
        continue
    w0 = ((by - cy) * (X - cx) + (cx - bx) * (Y - cy)) / den
    w1 = ((cy - ay) * (X - cx) + (ax - cx) * (Y - cy)) / den
    w2 = 1.0 - w0 - w1
    innen = (w0 >= -1e-4) & (w1 >= -1e-4) & (w2 >= -1e-4)
    if not innen.any():
        continue
    yy = (Y[innen] - 0.5).astype(int)
    xx = (X[innen] - 0.5).astype(int)
    tid[yy, xx] = k
    bc[yy, xx] = np.stack([w0[innen], w1[innen], w2[innen]], 1)
J, I = np.nonzero(tid >= 0)
T_ = tid[J, I]
W = bc[J, I].astype(np.float64)
W /= W.sum(1, keepdims=True)
N = len(T_)
zeit(f"Rastern: {N} Bildpunkte ({N / G / G * 100:.0f} %)")


def je_bildpunkt(feld):
    """Eckwerte (n, k) baryzentrisch auf die Bildpunkte."""
    return (feld[TRI[T_, 0]] * W[:, 0:1] + feld[TRI[T_, 1]] * W[:, 1:2] + feld[TRI[T_, 2]] * W[:, 2:3])


# Eckennormalen der Haut
fn = np.cross(HE[TRI[:, 1]] - HE[TRI[:, 0]], HE[TRI[:, 2]] - HE[TRI[:, 0]])
vn = np.zeros_like(HE)
for k in range(3):
    np.add.at(vn, TRI[:, k], fn)
vn /= np.maximum(np.linalg.norm(vn, axis=1), 1e-12)[:, None]
POS = je_bildpunkt(HE)
NRM = je_bildpunkt(vn)
NRM /= np.maximum(np.linalg.norm(NRM, axis=1), 1e-12)[:, None]
v0 = TRI[T_, 0]
IST_MUND = (v0 >= MH0) & (v0 < MH1)
IST_AUGENHOEHLE = ((v0 >= AH[0, 0]) & (v0 < AH[0, 1])) | ((v0 >= AH[1, 0]) & (v0 < AH[1, 1]))
IST_BUESTE = v0 >= N_HAUT
STOFF = TRI_ST[T_]

# Meter je Bildpunkt entlang u und v (aus den Dreiecken), für Normalen aus Höhen
uv_t = TRI_UV
d1, d2 = uv_t[:, 1] - uv_t[:, 0], uv_t[:, 2] - uv_t[:, 0]
p1, p2 = HE[TRI[:, 1]] - HE[TRI[:, 0]], HE[TRI[:, 2]] - HE[TRI[:, 0]]
det = d1[:, 0] * d2[:, 1] - d1[:, 1] * d2[:, 0]
det = np.where(np.abs(det) < 1e-14, 1e-14, det)
dpdu = (p1 * d2[:, 1:2] - p2 * d1[:, 1:2]) / det[:, None]
dpdv = (p2 * d1[:, 0:1] - p1 * d2[:, 0:1]) / det[:, None]
M_U = np.linalg.norm(dpdu, axis=1)[T_] / G
M_V = np.linalg.norm(dpdv, axis=1)[T_] / G

# ================================================================= 2. MakeHuman-UV je Bildpunkt
bvh_mh = BVHTree.FromPolygons([Vector(p) for p in P], [tuple(int(i) for i in t) for t in MH_TRI])


def mh_uv(punkte):
    loc = np.zeros((len(punkte), 3))
    idx = np.zeros(len(punkte), np.int64)
    for k, p in enumerate(punkte):
        r = bvh_mh.find_nearest(Vector(p), 0.2)
        if r[0] is not None:
            loc[k] = r[0]
            idx[k] = r[2]
    t = MH_TRI[idx]
    a, b, c = P[t[:, 0]], P[t[:, 1]], P[t[:, 2]]
    v0_, v1_, v2_ = b - a, c - a, loc - a
    d00, d01, d11 = (v0_ * v0_).sum(1), (v0_ * v1_).sum(1), (v1_ * v1_).sum(1)
    d20, d21 = (v2_ * v0_).sum(1), (v2_ * v1_).sum(1)
    nn = np.maximum(d00 * d11 - d01 * d01, 1e-20)
    wv = (d11 * d20 - d01 * d21) / nn
    ww = (d00 * d21 - d01 * d20) / nn
    g = np.clip(np.stack([1 - wv - ww, wv, ww], 1), 0, 1)
    g /= g.sum(1, keepdims=True)
    return (MH_UV[idx] * g[..., None]).sum(1)


uv_ecke = mh_uv(HERK)
uv_drei = uv_ecke[TRI[T_]]                                          # (N, 3, 2)
spanne = np.max(np.linalg.norm(uv_drei - uv_drei[:, [1, 2, 0]], axis=2), axis=1)
UVMH = (uv_drei * W[..., None]).sum(1)
naht = spanne > 0.015
if naht.any():
    UVMH[naht] = mh_uv(je_bildpunkt(HERK)[naht])
zeit(f"MakeHuman-UV: {int(naht.sum())} Bildpunkte an Nähten einzeln")


def bild_lesen(pfad, kanaele=3):
    b = oiio.ImageBuf(pfad)
    return b.get_pixels(oiio.FLOAT)[..., :kanaele].astype(np.float32)


def abtasten(bild, uv):
    """Bilinear, uv mit v nach oben (Blender), Bildzeile 0 = oben."""
    h, w = bild.shape[:2]
    x = np.clip(uv[:, 0] * w - 0.5, 0, w - 1.001)
    y = np.clip((1.0 - uv[:, 1]) * h - 0.5, 0, h - 1.001)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = (x - x0)[:, None], (y - y0)[:, None]
    return ((bild[y0, x0] * (1 - fx) + bild[y0, x0 + 1] * fx) * (1 - fy)
            + (bild[y0 + 1, x0] * (1 - fx) + bild[y0 + 1, x0 + 1] * fx) * fy)


def lin(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


ALB = lin(abtasten(bild_lesen(HAUTBILD), UVMH))
zeit("Hautbild übertragen")

# ================================================================= 3. Lage im Gesicht
X, Y, Z = POS[:, 0], POS[:, 1], POS[:, 2]
SX = np.where(X >= 0, 1.0, -1.0)
AUGE_Z = (AUGE["l"][2] + AUGE["r"][2]) / 2
VORN = np.clip(-NRM[:, 1], -1, 1)                    # Normale zeigt nach vorn


def gauss(c, s):
    return np.exp(-((POS - c) ** 2).sum(1) / (2 * s * s))


bvh_haut = BVHTree.FromPolygons([Vector(p) for p in HE], [tuple(int(i) for i in t) for t in TRI])


def auf_haut(punkte):
    """Punkte auf die Haut legen (nächster Punkt)."""
    aus = np.array(punkte, dtype=np.float64)
    for k, p in enumerate(aus):
        r = bvh_haut.find_nearest(Vector(p), 0.05)
        if r[0] is not None:
            aus[k] = r[0]
    return aus


def auf_haut_vorn(punkte):
    """Punkte von vorn (entlang +y) auf die Haut legen (für Linien über der Oberlippe u. ä.)."""
    aus = np.array(punkte, dtype=np.float64)
    for k, p in enumerate(aus):
        r = bvh_haut.ray_cast(Vector((p[0], p[1] - 0.05, p[2])), Vector((0, 1, 0)), 0.1)
        if r[0] is not None:
            aus[k] = r[0]
    return aus


def kurve(pkte, n=60):
    """Glatte Kurve (quadratische Bézier-Stücke bzw. Polylinie) durch Stützpunkte, auf die Haut gelegt."""
    pk = np.asarray(pkte, dtype=np.float64)
    if len(pk) == 3:
        t = np.linspace(0, 1, n)[:, None]
        c = (1 - t) ** 2 * pk[0] + 2 * (1 - t) * t * pk[1] + t ** 2 * pk[2]
    else:
        seg = np.linalg.norm(np.diff(pk, axis=0), axis=1)
        s = np.concatenate([[0], np.cumsum(seg)])
        z = np.linspace(0, s[-1], n)
        c = np.stack([np.interp(z, s, pk[:, k]) for k in range(3)], 1)
    return auf_haut(c)


def abstand_kurve(c, auswahl=None, weit=0.03):
    """Abstand jedes (ausgewählten) Bildpunkts zur Kurve c, Lage t (0..1) entlang der Kurve und Fußpunkt."""
    idx = np.arange(N) if auswahl is None else np.nonzero(auswahl)[0]
    q = POS[idx]
    best = np.full(len(idx), np.inf)
    best_t = np.zeros(len(idx))
    fuss = np.zeros((len(idx), 3))
    n = len(c)
    for k in range(n - 1):
        a, b = c[k], c[k + 1]
        ab = b - a
        t = np.clip(((q - a) @ ab) / max(ab @ ab, 1e-12), 0, 1)
        f = a + t[:, None] * ab
        dd = np.linalg.norm(q - f, axis=1)
        besser = dd < best
        best[besser] = dd[besser]
        best_t[besser] = (k + t[besser]) / (n - 1)
        fuss[besser] = f[besser]
    aus_d = np.full(N, np.inf)
    aus_t = np.zeros(N)
    aus_f = np.zeros((N, 3))
    aus_d[idx], aus_t[idx], aus_f[idx] = best, best_t, fuss
    return aus_d, aus_t, aus_f


def nahe(c, r):
    """Bildpunkte, die näher als r an einem Punkt der Kurve liegen (Vorauswahl)."""
    lo, hi = c.min(0) - r, c.max(0) + r
    return np.all((POS >= lo) & (POS <= hi), axis=1)


def rauschen1(x, frequenz, start):
    """Glattes 1D-Rauschen (Summe weniger Sinus) im Bereich -1..1."""
    return (np.sin(x * frequenz * 2 * math.pi + start) * 0.6 + np.sin(x * frequenz * 4.7 + start * 1.7) * 0.3
            + np.sin(x * frequenz * 11.3 + start * 2.3) * 0.1)


def furche(dd, breite):
    return -np.exp(-0.5 * (dd / breite) ** 2)


# Lidrand: Abstand zur Lidnaht (für gemalte Wimpern des Hautbilds und Rötung der Lider)
lid_d = np.full(N, np.inf)
for s in ("l", "r"):
    lid_d = np.minimum(lid_d, abstand_kurve(np.vstack([LID[s], LID[s][:1]]), nahe(LID[s], 0.012))[0])
lid_d = np.where(np.isfinite(lid_d), lid_d, 1.0)
# Lippen: Abstand zur Lippennaht
lip_d = abstand_kurve(LIPPE, nahe(LIPPE, 0.02))[0]
lip_d = np.where(np.isfinite(lip_d), lip_d, 1.0)
# Lippenrot: Ober- und Unterlippe als Bereich zwischen Lippennaht und Außenkontur (Merkpunkte 48–59)
ober_kontur = M68[48:55]
unter_kontur = np.vstack([M68[54:60], M68[48:49]])


def kontur_hoehe(kontur, x):
    o = np.argsort(kontur[:, 0])
    return np.interp(x, kontur[o, 0], kontur[o, 2], left=np.nan, right=np.nan)


mund_z = LIPPE[:, 2].mean()
z_ober = kontur_hoehe(ober_kontur, X)
z_unter = kontur_hoehe(unter_kontur, X)
mundbreite = abs(M68[54, 0] - M68[48, 0]) / 2
im_mundbereich = (np.abs(X) < mundbreite + 0.001) & (Y < M68[51, 1] + 0.012) & ~IST_MUND & ~IST_BUESTE
LIPPENROT = np.zeros(N)
with np.errstate(invalid="ignore"):
    ober = im_mundbereich & (Z >= mund_z - 0.002) & (Z <= np.nan_to_num(z_ober, nan=-9) + 0.0006)
    unter = im_mundbereich & (Z < mund_z + 0.002) & (Z >= np.nan_to_num(z_unter, nan=9) - 0.0008)
    rand_o = np.nan_to_num(z_ober, nan=-9) + 0.0006 - Z
    rand_u = Z - (np.nan_to_num(z_unter, nan=9) - 0.0008)
LIPPENROT[ober] = glatt(rand_o[ober], 0.0, 0.0012)
LIPPENROT[unter] = glatt(rand_u[unter], 0.0, 0.0015)
LIPPENROT *= glatt(mundbreite + 0.001 - np.abs(X), 0.0, 0.003)
LIPPENROT = np.maximum(LIPPENROT, (1 - glatt(lip_d, 0.001, 0.003)) * (~IST_MUND))
zeit("Merkpunkte, Lider, Lippen")

# ================================================================= 4. Kopfhaut und Lidrand säubern, Farbe normieren
# Kopfhaut: über dem Haaransatz liegen im Hautbild Stoppeln. Haaransatz als Höhe über der Augenlinie je Richtung um
# den Kopf (vorn Stirn, Schläfen tiefer, hinten bis zum Nacken)
ohr_z = AUGE_Z - 0.012 * MASS
kopfmitte = np.array([0.0, (AUGE["l"][1] + AUGE["r"][1]) / 2 + 0.085 * MASS, AUGE_Z])
rel = POS - kopfmitte
richtung = np.degrees(np.arctan2(np.abs(rel[:, 0]), -rel[:, 1]))          # 0 vorn, 90 seitlich, 180 hinten
stirn_oben = M68[[19, 24], 2].mean() + 0.052 * MASS
ansatz = np.interp(richtung, [0, 30, 55, 75, 95, 125, 155, 180],
                   [stirn_oben, stirn_oben - 0.005 * MASS, AUGE_Z + 0.034 * MASS, AUGE_Z + 0.026 * MASS, AUGE_Z + 0.02 * MASS,
                    AUGE_Z - 0.012 * MASS, ohr_z - 0.04 * MASS, ohr_z - 0.055 * MASS])
KOPFHAUT = glatt(Z - ansatz, -0.006, 0.008) * (~IST_BUESTE) * (~IST_MUND)
# Gesichtshaut als Bezug: Wangen, Stirn, Kinn ohne Lippen, Augen, Kopfhaut
bezug = (~IST_MUND) & (~IST_BUESTE) & (~IST_AUGENHOEHLE) & (KOPFHAUT < 0.01) & (lid_d > 0.006) & (LIPPENROT < 0.01) \
    & (VORN > 0.4) & (np.abs(X) < 0.05 * MASS) & (Z > M68[8, 2]) & (Z < stirn_oben)
MITTEL = np.median(ALB[bezug], axis=0)
zeit(f"mittlere Hautfarbe (linear) {np.round(MITTEL, 3)}")


def glatt_bild(werte, gueltig, sigma_px):
    """Gauß-Weichzeichnen im Bildraum (nur gültige Punkte zählen)."""
    feld = np.zeros((G, G, werte.shape[1]), np.float32)
    gew = np.zeros((G, G), np.float32)
    feld[J, I] = werte * gueltig[:, None]
    gew[J, I] = gueltig
    # drei Kastenfilter hintereinander (Summentabelle) ≈ Gauß; Aufwand unabhängig vom Halbmesser
    r = max(1, int(round(sigma_px * 0.9)))

    def kasten(a, achse):
        pad = [(0, 0)] * a.ndim
        pad[achse] = (r + 1, r)
        c = np.cumsum(np.pad(a, pad), axis=achse, dtype=np.float64)
        hi = np.take(c, np.arange(2 * r + 1, c.shape[achse]), axis=achse)
        lo = np.take(c, np.arange(0, c.shape[achse] - 2 * r - 1), axis=achse)
        return ((hi - lo) / (2 * r + 1)).astype(np.float32)
    for _ in range(3):
        for achse in (0, 1):
            feld = kasten(feld, achse)
            gew = kasten(gew, achse)
    return (feld / np.maximum(gew, 1e-6)[..., None])[J, I]


# Lidrand: Im Hautbild sind Augen, Wimpern und Lidspalt gemalt (weiß, grau, dunkel); dort die Haut der Umgebung nehmen.
# „Hautähnlich“ = Verhältnis Rot/Grün und Grün/Blau wie die mittlere Haut und nicht zu dunkel
rg = ALB[:, 0] / np.maximum(ALB[:, 1], 1e-4)
gb = ALB[:, 1] / np.maximum(ALB[:, 2], 1e-4)
hautnah = np.exp(-((rg - MITTEL[0] / MITTEL[1]) / 0.18) ** 2 - ((gb - MITTEL[1] / MITTEL[2]) / 0.2) ** 2) \
    * (ALB.mean(1) > MITTEL.mean() * 0.6)
umgebung = glatt_bild(ALB, (hautnah * (lid_d > 0.0035) * (~IST_MUND)).astype(np.float32), 14.0)
lidrand = (1 - glatt(lid_d, 0.0025, 0.006)) * (~IST_MUND)
ALB = ALB * (1 - lidrand[:, None]) + umgebung * np.array([0.96, 0.9, 0.9]) * lidrand[:, None]
# Kopfhaut: mittlere Gesichtsfarbe, etwas blasser, mit leichtem Fleckenrauschen
flecken = 1.0 + 0.03 * np.sin(POS @ np.array([310.0, 170, 240])) * np.sin(POS @ np.array([-150.0, 260, 90]))
kopf_farbe = MITTEL * np.array([1.0, 0.985, 0.98]) * flecken[:, None]
ALB = ALB * (1 - KOPFHAUT[:, None]) + kopf_farbe * KOPFHAUT[:, None]
# Augenhöhlen (Innenseite der Lider): Bindehaut
ALB[IST_AUGENHOEHLE] = MITTEL * np.array([1.05, 0.62, 0.6])
ALB[IST_MUND] = MITTEL * np.array([0.9, 0.45, 0.45])
# Feine Unruhe der Haut (eigene Rechenregel): Flecken um 2–4 mm (Helligkeit ±4 %, etwas Rötung) und Sprenkel um
# 0,6 mm (±2,5 %); nicht in Mund und Augenhöhlen
def wert_rauschen(p, zelle, start):
    """Glattes 3D-Rauschen (Wert-Rauschen, trilinear mit Glättung) im Bereich -1..1."""
    q = p / zelle + start
    i0 = np.floor(q).astype(np.int64)
    f = q - i0
    f = f * f * (3 - 2 * f)
    aus = np.zeros(len(p))
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                z_ = i0 + np.array([dx, dy, dz])
                h = (z_[:, 0] * 73856093) ^ (z_[:, 1] * 19349663) ^ (z_[:, 2] * 83492791)
                h = ((h * 2654435761) & 0xFFFFFF) / float(0xFFFFFF) * 2 - 1
                w = (f[:, 0] if dx else 1 - f[:, 0]) * (f[:, 1] if dy else 1 - f[:, 1]) * (f[:, 2] if dz else 1 - f[:, 2])
                aus += w * h
    return aus


flecken_g = 0.6 * wert_rauschen(POS, 0.0035 * MASS, 3.1) + 0.4 * wert_rauschen(POS, 0.0016 * MASS, 7.7)
sprenkel = wert_rauschen(POS, 0.0006 * MASS, 1.3)
unruhe = (1 + 0.04 * flecken_g + 0.025 * sprenkel)[:, None] * (1 + 0.03 * np.clip(flecken_g, 0, 1)[:, None] * np.array([0.6, -0.4, -0.5]))
ALB = np.where((IST_MUND | IST_AUGENHOEHLE)[:, None], ALB, ALB * unruhe)
# Lidkante: am Oberlid ein dunkler Saum (Wimpernwurzeln), am Unterlid schwächer
for s in ("l", "r"):
    for oben_, staerke in ((True, 0.38), (False, 0.15)):
        sel_ = (LID[s][:, 2] > AUGE[s][2] - 0.0004) if oben_ else (LID[s][:, 2] < AUGE[s][2] + 0.0004)
        lk = LID[s][sel_]
        lk = lk[np.argsort(lk[:, 0])]
        if len(lk) < 3:
            continue
        dd_, _, _ = abstand_kurve(lk, nahe(lk, 0.004))
        saum = (1 - glatt(np.nan_to_num(dd_, posinf=1), 0.0004, 0.0016)) * (~IST_AUGENHOEHLE) * (~IST_MUND)
        ALB = ALB * (1 - staerke * saum)[:, None]
FARBE = np.clip(ALB / MITTEL * 0.5, 0, 1)
zeit("Kopfhaut, Lidrand, Normierung")

# ================================================================= 5. Brauenschatten aus den Brauenkarten
be, bf, buv, blage = d["brauen_ecken"], d["brauen_flaechen"], d["brauen_uv"], d["brauen_lage"]
untere = [k for k, f in enumerate(bf) if blage[f[0]] == 0]
b_tri, b_tri_uv = [], []
for k in untere:
    f = bf[k]
    for j in range(1, len(f) - 1):
        b_tri.append((f[0], f[j], f[j + 1]))
        b_tri_uv.append((buv[k][0], buv[k][j], buv[k][j + 1]))
b_tri, b_tri_uv = np.array(b_tri), np.array(b_tri_uv)
bvh_b = BVHTree.FromPolygons([Vector(p) for p in be], [tuple(int(i) for i in t) for t in b_tri])
BRAUE = np.zeros(N)
nah_b = np.nonzero(np.all((POS > be.min(0) - 0.004) & (POS < be.max(0) + 0.004), axis=1))[0]
for k in nah_b:
    r = bvh_b.find_nearest(Vector(POS[k]), 0.003)
    if r[0] is None:
        continue
    t = b_tri[r[2]]
    a, b_, c = be[t[0]], be[t[1]], be[t[2]]
    loc = np.array(r[0])
    m = np.array([b_ - a, c - a]).T
    w12 = np.linalg.lstsq(m, loc - a, rcond=None)[0]
    w = np.array([1 - w12.sum(), w12[0], w12[1]])
    u, v = w @ b_tri_uv[r[2]]
    dichte, _ = kht.brauen_form(np.clip(u, 0, 1), np.clip(v * 2.0, 0, 1))
    BRAUE[k] = float(dichte) * (1 - glatt(r[3], 0.0008, 0.003))
zeit(f"Brauenschatten: {len(nah_b)} Bildpunkte geprüft")

# ================================================================= 6. Masken: Rötung, Glanz, Verdeckung, Bart
nase = M68[30]
wange = {s: AUGE[s][:3] + np.array([sx * 0.012 * MASS, -0.006, -0.024 * MASS]) for s, sx in (("l", 1.0), ("r", -1.0))}
band = (~IST_BUESTE) & (Z < AUGE_Z + 0.01) & (Z > mund_z)
ohr = {}
for s, sx in (("l", 1.0), ("r", -1.0)):
    seite_ = band & (SX == sx)
    xmax = np.max(np.abs(X[seite_]))
    rand_ = seite_ & (np.abs(X) > xmax - 0.012 * MASS)
    ohr[s] = POS[rand_].mean(0)
OHR = np.maximum(gauss(ohr["l"], 0.014 * MASS), gauss(ohr["r"], 0.014 * MASS)) * glatt(np.abs(X), np.abs(ohr["l"][0]) - 0.012, np.abs(ohr["l"][0]) - 0.004)
ROT = (0.75 * gauss(wange["l"], 0.016 * MASS) + 0.75 * gauss(wange["r"], 0.016 * MASS) + 0.8 * gauss(nase, 0.009 * MASS)
       + 0.7 * gauss(M68[31], 0.006 * MASS) + 0.7 * gauss(M68[35], 0.006 * MASS) + 0.35 * gauss(M68[8], 0.012 * MASS)
       + 0.9 * OHR + 0.28 * (1 - glatt(lid_d, 0.002, 0.008)) + 0.35 * LIPPENROT)
ROT = np.clip(ROT, 0, 1) * (~IST_MUND)
stirn = np.array([0.0, M68[27, 1] - 0.004, M68[[19, 24], 2].mean() + 0.025 * MASS])
GLANZ = (0.7 * gauss(stirn, 0.022 * MASS) + 0.9 * gauss(nase, 0.01 * MASS) + 0.6 * gauss((M68[27] + M68[30]) / 2, 0.006 * MASS)
         + 0.45 * gauss(M68[8], 0.012 * MASS) + 0.9 * LIPPENROT + 0.12)
GLANZ = np.clip(GLANZ, 0, 1)
# Verdeckung je Ecke über Strahlen (2 cm), dann auf die Bildpunkte
richt = rng.normal(size=(24, 3))
richt /= np.linalg.norm(richt, axis=1, keepdims=True)
ao_ecke = np.zeros(len(HE))
for i, (p, n_) in enumerate(zip(HE, vn)):
    if i >= MH0 and i < MH1:
        continue
    treffer = 0.0
    zahl = 0
    for r_ in richt:
        if r_ @ n_ < 0:
            r_ = -r_
        w_ = r_ @ n_
        h = bvh_haut.ray_cast(Vector(p + n_ * 0.0003), Vector(r_), 0.02 * MASS)
        zahl += w_
        if h[0] is not None:
            treffer += w_ * (1 - h[3] / (0.02 * MASS))
    ao_ecke[i] = treffer / max(zahl, 1e-6)
HOEHLE = np.clip(je_bildpunkt(ao_ecke[:, None])[:, 0] * 1.6, 0, 1)
HOEHLE[IST_MUND] = 1.0
zeit("Verdeckung")
# Bartschatten: unter der Linie Ohr–Nasenflügel, Oberlippe, Kinn, Kiefer bis unter das Kinn; nicht auf den Lippen
BART = np.zeros(N)
for s, sx, ohr_m, nasen_m in (("l", 1.0, 16, 35), ("r", -1.0, 0, 31)):
    a = M68[ohr_m] + np.array([0, 0, -0.004 * MASS])
    b = M68[nasen_m] + np.array([sx * 0.008 * MASS, 0, -0.004 * MASS])
    t = np.clip((X * sx - a[0] * sx) / (b[0] * sx - a[0] * sx), 0, 1)
    linie_z = a[2] + (b[2] - a[2]) * t
    seite_ = (SX == sx)
    BART[seite_] = glatt(linie_z[seite_] - Z[seite_], -0.003, 0.006)
# Oberlippe zwischen Nase und Lippe gehört dazu, Kinn bis Hals; hinten nicht über den Kieferwinkel
BART = np.maximum(BART, (np.abs(X) < M68[54, 0] + 0.004) * glatt(M68[33, 2] - 0.002 - Z, 0.0, 0.004))
BART *= glatt(-Y + M68[0, 1] - 0.004, 0.0, 0.012) * (1 - LIPPENROT) * (~IST_MUND)
hals_a = (POS - d["kragen_mitte"]) @ d["hals_achse"]
BART *= glatt(hals_a, 0.035 * MASS, 0.05 * MASS)
BART *= glatt(lip_d, 0.002, 0.004)
BART *= 0.85 + 0.15 * np.sin(POS @ np.array([900.0, 400, 650])) * np.sin(POS @ np.array([-500.0, 820, 300]))
zeit("Rötung, Glanz, Bart")

# ================================================================= 7. Falten (Höhen, -1 = tiefe Furche)
F_STIRN = np.zeros(N)
F_AUGE = np.zeros(N)
F_MUND = np.zeros(N)
F_HALS = np.zeros(N)
breite_f = 0.0008 * MASS
# Stirn: vier Querfurchen, gewellt und unterbrochen, nur vorn
braue_z = M68[[19, 24], 2].mean()
stirnbereich = (~IST_BUESTE) & (VORN > 0.25) & (Z > braue_z) & (Z < braue_z + 0.05 * MASS) & (np.abs(X) < 0.06 * MASS)
xs = X / MASS
for k, (hoehe, staerke) in enumerate(((0.011, 0.75), (0.0185, 1.0), (0.026, 0.9), (0.0335, 0.6))):
    st = rng.uniform(0, 6.3, 3)
    linie = braue_z + hoehe * MASS + 0.0025 * MASS * (xs / 0.04) ** 2 * (1 if k % 2 else 0.6) \
        + 0.0011 * MASS * rauschen1(xs, 1 / 0.05, st[0])
    dz = Z - linie
    abschnitt = np.clip(0.55 + 1.4 * rauschen1(xs, 1 / 0.035, st[1]), 0, 1)
    laenge = glatt(0.047 * MASS - np.abs(X) + rng.uniform(-0.006, 0.006), 0.0, 0.012)
    F_STIRN += np.where(stirnbereich, staerke * furche(dz, breite_f * (1.0 + 0.3 * k / 3)) * abschnitt * laenge, 0)
# Zornesfalten: zwei senkrechte Furchen über der Nasenwurzel
innen_z = M68[[21, 22], 2].mean()
for sx in (1.0, -1.0):
    zz = (Z - innen_z) / MASS
    xl = sx * (0.0052 + 0.12 * zz) * MASS
    dx = X - xl
    entlang = glatt(zz, -0.005, -0.001) * glatt(0.011 - zz, 0.0, 0.005)
    F_STIRN += np.where((VORN > 0.3) & ~IST_BUESTE, 0.8 * furche(dx, breite_f * 0.8) * entlang, 0)
# Krähenfüße: fünf Furchen fächerförmig vom äußeren Lidwinkel
for s, sx, m in (("l", 1.0, 45), ("r", -1.0, 36)):
    c = M68[m]
    e_lat = np.array([sx, 0.55, 0.0])
    e_lat /= np.linalg.norm(e_lat)
    rel_ = POS - c
    s_ = rel_ @ e_lat
    t_ = rel_[:, 2]
    r_ = np.hypot(s_, t_) / MASS
    phi = np.degrees(np.arctan2(t_, s_))
    bereich = (s_ > 0) & (r_ < 0.03) & (SX == sx) & ~IST_BUESTE
    for k, w0 in enumerate((-36, -18, -2, 14, 30)):
        w0 = w0 + rng.normal(0, 4)
        anfang, ende = rng.uniform(0.005, 0.008), rng.uniform(0.016, 0.024)
        bogen = w0 - 10.0 * (r_ - anfang) / 0.015 * (1 if w0 < 0 else 0.4)
        dd = np.radians(phi - bogen) * r_ * MASS
        entlang = glatt(r_, anfang, anfang + 0.003) * glatt(ende - r_, 0.0, 0.005)
        F_AUGE += np.where(bereich, (0.65 + 0.25 * (k % 2)) * furche(dd, breite_f * 0.75) * entlang, 0)
# Unterlid: zwei Bögen unter dem Unterlid (Tränenrinne, Lidfalte)
for s in ("l", "r"):
    mitte_ = AUGE[s][:3]
    unten = LID[s][LID[s][:, 2] < mitte_[2] - 0.001]
    unten = unten[np.argsort(unten[:, 0])]
    for tiefe, staerke in ((0.0045, 0.7), (0.009, 0.55)):
        c = kurve(unten + np.array([0, -0.001, -tiefe * MASS]), 40)
        dd, tt, _ = abstand_kurve(c, nahe(c, 0.004))
        F_AUGE += np.where(np.isfinite(dd), staerke * furche(np.nan_to_num(dd, posinf=1), breite_f) * glatt(tt, 0.05, 0.25)
                           * glatt(1 - tt, 0.05, 0.3), 0)
# Nasolabialfalte: vom Nasenflügel bogenförmig am Mundwinkel vorbei; seitlich davon ein Wulst (Wange)
for sx, nf, mw in ((1.0, 35, 54), (-1.0, 31, 48)):
    a = M68[nf] + np.array([sx * 0.0045, 0.0, 0.002]) * MASS
    b = M68[mw] + np.array([sx * 0.010, 0.0, 0.006]) * MASS
    e = M68[mw] + np.array([sx * 0.012, 0.0, -0.010]) * MASS
    c = kurve([a, b, e], 60)
    dd, tt, fuss = abstand_kurve(c, nahe(c, 0.01))
    ok = np.isfinite(dd)
    seite_ = np.sign((POS[:, 0] - fuss[:, 0]) * sx)
    wulst = np.where(seite_ > 0, 0.55 * np.exp(-0.5 * ((dd - 0.004 * MASS) / (0.0028 * MASS)) ** 2), 0)
    form = furche(dd, 0.0012 * MASS) + wulst
    F_MUND += np.where(ok, form * glatt(tt, 0.0, 0.15) * glatt(1 - tt, 0.0, 0.25), 0)
    # Marionettenfalte: vom Mundwinkel abwärts
    a2 = M68[mw] + np.array([sx * 0.002, 0.0, -0.002]) * MASS
    e2 = M68[mw] + np.array([sx * 0.008, 0.0, -0.022]) * MASS
    c2 = kurve([a2, (a2 + e2) / 2 + np.array([sx * 0.002, 0, 0]), e2], 40)
    dd2, tt2, _ = abstand_kurve(c2, nahe(c2, 0.008))
    F_MUND += np.where(np.isfinite(dd2), 0.7 * furche(np.nan_to_num(dd2, posinf=1), 0.0009 * MASS) * glatt(tt2, 0.05, 0.3)
                       * glatt(1 - tt2, 0.0, 0.35), 0)
# Lippenfältchen: kurze senkrechte Furchen über der Oberlippe und unter der Unterlippe
for lage, kontur, richtung_z, anzahl in (("oben", ober_kontur, 1.0, 14), ("unten", unter_kontur, -1.0, 8)):
    for k in range(anzahl):
        x0 = rng.uniform(-1, 1) * (mundbreite - 0.002)
        zk = kontur_hoehe(kontur, np.array([x0]))[0]
        if np.isnan(zk):
            continue
        L = rng.uniform(0.003, 0.007) * MASS
        a = np.array([x0, 0, zk + richtung_z * 0.0008])
        e = a + np.array([x0 * 0.08, 0, richtung_z * L])
        t_ = np.linspace(0, 1, 12)[:, None]
        c = auf_haut_vorn(a + t_ * (e - a))
        dd, tt, _ = abstand_kurve(c, nahe(c, 0.002))
        F_HALS += np.where(np.isfinite(dd), rng.uniform(0.5, 0.9) * furche(np.nan_to_num(dd, posinf=1), 0.00028 * MASS)
                           * glatt(1 - tt, 0.0, 0.4), 0)
# Hals: drei Querfurchen vorn
hals_vorn = (-((POS - d["kragen_mitte"]) @ np.array([0, -1.0, 0])) < 0.02) & (hals_a > 0.005) & (hals_a < 0.07 * MASS)
for hoehe, staerke in ((0.022, 0.8), (0.034, 1.0), (0.046, 0.7)):
    st = rng.uniform(0, 6.3)
    dz = hals_a - hoehe * MASS - 0.0015 * rauschen1(X / MASS, 1 / 0.04, st) - 0.004 * (X / (0.05 * MASS)) ** 2
    F_HALS += np.where(hals_vorn, staerke * furche(dz, 0.0009 * MASS) * glatt(0.045 * MASS - np.abs(X), 0, 0.015), 0)
for f in (F_STIRN, F_AUGE, F_MUND, F_HALS):
    f[IST_MUND | IST_AUGENHOEHLE] = 0
    np.clip(f, -1, 1, out=f)
zeit("Falten")

# ================================================================= 8. Feine Höhen: Poren, Lippenlinien, Hautbild
# Poren als Grübchen (3D-Zellrauschen, Zellgröße 0,55 mm), Stärke je Gegend
zell = 0.0008 * MASS
porenstaerke = np.clip(0.35 + 0.65 * gauss(nase, 0.012 * MASS) + 0.45 * (gauss(wange["l"], 0.02 * MASS) + gauss(wange["r"], 0.02 * MASS))
                       + 0.25 * gauss(stirn, 0.025 * MASS) - 0.35 * (1 - glatt(lid_d, 0.002, 0.008)), 0, 1) * (1 - LIPPENROT)
porenstaerke[IST_MUND | IST_AUGENHOEHLE] = 0
HOEHE = np.zeros(N)
zelle = np.floor(POS / zell).astype(np.int64)
naechst = np.full(N, np.inf)
for dx in (-1, 0, 1):
    for dy in (-1, 0, 1):
        for dz in (-1, 0, 1):
            z_ = zelle + np.array([dx, dy, dz])
            h = (z_[:, 0] * 73856093) ^ (z_[:, 1] * 19349663) ^ (z_[:, 2] * 83492791)
            h = (h * 2654435761) & 0xFFFFFFFF
            off = np.stack([(h & 1023) / 1023.0, ((h >> 10) & 1023) / 1023.0, ((h >> 20) & 1023) / 1023.0], 1)
            dd = np.linalg.norm(POS - (z_ + off) * zell, axis=1)
            naechst = np.minimum(naechst, dd)
HOEHE += -0.00006 * porenstaerke * np.exp(-(naechst / (0.3 * zell)) ** 2)
# Lippenlinien: senkrechte Rillen auf dem Lippenrot (Abstand etwa 0,9 mm, leicht gewellt)
phase = X / (0.0009 * MASS) + 0.6 * np.sin(Z / (0.002 * MASS))
HOEHE += -0.000025 * LIPPENROT * glatt(np.cos(phase * 2 * math.pi), 0.3, 1.0)
# Unregelmäßigkeiten aus dem Hautbild (Hochpass der Helligkeit)
hell_ = ALB.mean(1)
HOEHE += 0.00006 * (hell_ - glatt_bild(ALB, np.ones(N, np.float32), 3.0).mean(1)) / max(MITTEL.mean(), 1e-3) * (~IST_MUND)
zeit("Poren, Lippenlinien")


# ================================================================= 9. Bilder schreiben
def ins_bild(werte, rand=12):
    """Bildpunktwerte (N, k) in ein Bild (G, G, k); leere Stellen mit Nachbarwerten auffüllen (Mipmaps)."""
    k = werte.shape[1]
    feld = np.zeros((G, G, k), np.float32)
    gueltig = np.zeros((G, G), bool)
    feld[J, I] = werte
    gueltig[J, I] = True
    for _ in range(rand):
        summe = np.zeros_like(feld)
        zahl = np.zeros((G, G), np.float32)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            summe += np.roll(np.roll(feld * gueltig[..., None], dy, 0), dx, 1)
            zahl += np.roll(np.roll(gueltig.astype(np.float32), dy, 0), dx, 1)
        neu = (~gueltig) & (zahl > 0)
        feld[neu] = summe[neu] / zahl[neu][:, None]
        gueltig |= neu
    return feld


def schreiben(name, feld):
    feld = np.ascontiguousarray(feld[::-1])          # Zeile 0 = oben (v = 1)
    k = feld.shape[2]
    spec = oiio.ImageSpec(G, G, k, oiio.FLOAT)
    spec.attribute("oiio:UnassociatedAlpha", 1)          # Alpha ist ein eigener Kanal, nicht vormultipliziert
    buf = oiio.ImageBuf(spec)
    buf.set_pixels(oiio.ROI(0, G, 0, G, 0, 1, 0, k), np.clip(feld, 0, 1).astype(np.float32))
    buf.write(os.path.join(AUS, name), "uint8")


schreiben("haut_farbe.png", ins_bild(np.column_stack([FARBE, BRAUE])))
schreiben("haut_masken.png", ins_bild(np.column_stack([ROT, GLANZ, HOEHLE, BART])))
schreiben("haut_falten.png", ins_bild(np.column_stack([F_STIRN, F_AUGE, F_MUND, F_HALS]) * 0.5 + 0.5))
# Normalen aus den Höhen: Gefälle je Meter entlang u und v
hb = ins_bild(HOEHE[:, None], rand=3)[..., 0]
gu = (np.roll(hb, -1, 1) - np.roll(hb, 1, 1)) / 2
gv = (np.roll(hb, -1, 0) - np.roll(hb, 1, 0)) / 2
su = gu[J, I] / np.maximum(M_U, 1e-7)
sv = gv[J, I] / np.maximum(M_V, 1e-7)
nt = np.column_stack([-su, -sv, np.ones(N)])
nt /= np.linalg.norm(nt, axis=1, keepdims=True)
schreiben("haut_normal.png", ins_bild(np.column_stack([nt[:, 0] * 0.5 + 0.5, nt[:, 1] * 0.5 + 0.5, porenstaerke])))
# Augenbilder aus RoboPoets (CC0) umwandeln
maske = bild_lesen(os.path.join(ROBO, "iris_color_mask.webp"))
iris = np.zeros(maske.shape[:2] + (3,), np.float32)
iris[..., 0], iris[..., 1] = maske[..., 0], maske[..., 1]
iris[..., 2] = bild_lesen(os.path.join(ROBO, "iris_depth.webp"))[..., 0]
for name, feld in (("iris_muster.png", iris), ("lederhaut.png", bild_lesen(os.path.join(ROBO, "sclera_color.webp")))):
    buf = oiio.ImageBuf(oiio.ImageSpec(feld.shape[1], feld.shape[0], 3, oiio.FLOAT))
    buf.set_pixels(oiio.ROI(0, feld.shape[1], 0, feld.shape[0], 0, 1, 0, 3), np.ascontiguousarray(feld))
    buf.write(os.path.join(AUS, name), "uint8")
bericht = {"bildpunkte": int(N), "groesse": G, "mittel_linear": [round(float(x), 4) for x in MITTEL],
           "mm_je_bildpunkt_gesicht": round(float(np.median(M_U[(VORN > 0.5) & ~IST_BUESTE]) * 1000), 3)}
import json  # noqa: E402
json.dump(bericht, open(os.path.join(AUS, "haut.json"), "w"), indent=1)
zeit("FERTIG " + json.dumps(bericht))
print("FERTIG Haut", AUS)
