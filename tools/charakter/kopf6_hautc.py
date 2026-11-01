"""Haut Etappe C (Charakter Phase 6, Probekopf B2; eigener Code, MIT; numpy und OpenImageIO, Blenders Python).

Diagnose „Puppe“ (Halt B2, U4): die Albedo hat keine Mittelfrequenzen, keine Farbzonen, keinen Bartschatten, keine
Nasolabial- und Tränenrinne; Glanzstreifen auf Nase und unter dem Auge; orange Nasenlöcher. Dieses Werkzeug backt drei
Karten in die UV-Karte des Kopfnetzes. Grundlage ist eine Lagekarte: für jeden Bildpunkt die 3D-Lage (mm, Godot-Achsen:
x links der Figur, y oben, z vorn) und die Normale auf dem Grundnetz. Alle Zonen und Falten werden in Millimetern an
anatomischen Merkpunkten (68 ICT-Punkte und abgeleitete wie Nasion, Pronasale, Tragion) gerechnet; Ränder werden mit
3D-Rauschen unregelmäßig, links und rechts verschieden (das Rauschen hängt an der Lage).

haut_c_zonen.png (1024², linear)   R Hämoglobin (Wangen, Nase, Ohren, Kinn, Lider, Karunkel)
                                   G Sonne und Gelbton (Stirn, Schläfen, Nasenrücken, Jochbein, Ohrrand, Nacken, Scheitel)
                                   B Augenringe und Venen (Tränenrinne, inneres Oberlid, Schläfe, Mundpartie)
                                   A Bartgebiet (unter der Linie Ohr–Mundwinkel, Oberlippe, Kinn, Hals; ohne Lippen)
haut_c_glanz.png (1024², linear)   R Rauheit (0,25 + 0,4 · R: Wange matt, Stirn und Nase etwas glänzender, ohne Streifen)
                                   G Porenstärke je Zone (Nase, innere Wange grob; Lider, Lippen ohne)
                                   B Höhle (Nasenloch innen, Gehörgang: dunkel, kein Streulicht)
                                   A Feuchte (Lidrand, Karunkel)
haut_c_relief.png (2048², linear)  Höhen (0,5 = keine) in Bildpunkt-Einheiten (Höhe / Bildpunktgröße an der Stelle), je
                                   Kanal mit eigenem Maßstab (haut_c.json „relief_bp“):
                                   R Stirn (Querfalten, Zornesfalten, Nasenwurzel)            – Gewicht nach Alter
                                   G Augen (Krähenfüße, Knitter am Unterlid)                    – Gewicht nach Alter
                                   B Mittelform (Nasolabial-, Tränen-, Lid-, Kinnlippen- und Nasenflügelrinne,
                                     Philtrum, Lippenrillen)                                    – ab Jugend, wächst
                                   A Mund und Hals (Marionettenfalten, Fältchen der Oberlippe, Wangenfalten, Halsringe)
Der Hautshader (haut6c.gdshader, #define WEG_C) liest sie; die Detailkacheln kommen aus kopf6_kacheln.py.

Aufruf: $BPY -I tools/charakter/kopf6_hautc.py <kopf6.glb> <ausgabeordner> [relief=2048] [zonen=1024] [samen=6107]
"""
import json
import os
import sys
import time

import numpy as np
import OpenImageIO as oiio

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from messen.glb import Glb  # noqa: E402
from messen.merkpunkte import Kopfnetz  # noqa: E402
from kopf6_zonen import malen  # noqa: E402

VERSATZ = 1000.0
T0 = time.time()


def log(*a):
    print(f"[{time.time() - T0:6.1f} s]", *a, flush=True)


# ---------------------------------------------------------------- Lagekarte
def lagekarte(haut, H, N, g):
    """Lage (mm), Normale und Bildpunktgröße (mm) je Bildpunkt; ok = Bildpunkt gehört zum Netz (mit 6 px Saum)."""
    werte = np.concatenate([np.ones((len(H), 1)), H + VERSATZ, N + 2.0], 1)
    b = malen(haut.uv, haut.dreiecke, werte, g)
    ok = b[..., 0] > 0.5
    X = b[..., 1:4] - VERSATZ
    Nn = b[..., 4:7] - 2.0
    Nn /= np.maximum(np.linalg.norm(Nn, axis=-1, keepdims=True), 1e-9)
    # Bildpunktgröße aus den Nachbarn (nur wo beide gültig sind), sonst Median
    du = np.linalg.norm(np.roll(X, -1, 1) - X, axis=-1)
    dv = np.linalg.norm(np.roll(X, -1, 0) - X, axis=-1)
    gu = ok & np.roll(ok, -1, 1) & (du < 5.0)
    gv = ok & np.roll(ok, -1, 0) & (dv < 5.0)
    mm = np.where(gu & gv, 0.5 * (du + dv), np.where(gu, du, np.where(gv, dv, np.nan)))
    med = float(np.nanmedian(mm[ok]))
    mm = np.where(np.isfinite(mm) & (mm > 0.02), mm, med)
    return ok, X, Nn, mm


# ---------------------------------------------------------------- Rauschen
def rauschen(X, mm, samen, oktaven=1):
    """Glattes 3D-Wertrauschen 0..1 (Gitter mm, Oktaven halbieren die Weite)."""
    rng = np.random.default_rng(samen)
    tab = rng.random(1 << 16)
    s = np.zeros(len(X))
    tot = 0.0
    amp = 1.0
    for o in range(oktaven):
        Q = X / (mm / 2 ** o) + rng.random(3) * 997.0
        i = np.floor(Q).astype(np.int64)
        f = Q - i
        f = f * f * (3.0 - 2.0 * f)

        def h(a, b, c):
            k = ((i[:, 0] + a) * 73856093) ^ ((i[:, 1] + b) * 19349663) ^ ((i[:, 2] + c) * 83492791)
            return tab[k & 0xFFFF]
        x00 = h(0, 0, 0) * (1 - f[:, 0]) + h(1, 0, 0) * f[:, 0]
        x10 = h(0, 1, 0) * (1 - f[:, 0]) + h(1, 1, 0) * f[:, 0]
        x01 = h(0, 0, 1) * (1 - f[:, 0]) + h(1, 0, 1) * f[:, 0]
        x11 = h(0, 1, 1) * (1 - f[:, 0]) + h(1, 1, 1) * f[:, 0]
        y0 = x00 * (1 - f[:, 1]) + x10 * f[:, 1]
        y1 = x01 * (1 - f[:, 1]) + x11 * f[:, 1]
        s += amp * (y0 * (1 - f[:, 2]) + y1 * f[:, 2])
        tot += amp
        amp *= 0.5
    return s / tot


def glatt(x, a, b):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def gauss(X, c, s):
    d = (X - np.asarray(c, float)) / np.asarray(s, float)
    return np.exp(-0.5 * (d ** 2).sum(1))


# ---------------------------------------------------------------- Linien
class Netzflaeche:
    """Linien auf die Haut legen: z jedes Punkts = vorderste Ecke im Umkreis (xy)."""

    def __init__(self, H, N):
        self.H = H
        self.N = N

    def auf_haut(self, Q, r=2.5):
        Q = np.array(Q, float)
        for k, q in enumerate(Q):
            d = np.hypot(self.H[:, 0] - q[0], self.H[:, 1] - q[1])
            sel = (d < r) & (self.N[:, 2] > -0.35)
            if not sel.any():
                sel = d < 2 * r
            if sel.any():
                Q[k, 2] = self.H[sel, 2].max()
        return Q


def kurve(punkte, n=48):
    """Catmull-Rom durch die Stützpunkte (xy; z wird später aufgelegt)."""
    P = np.array(punkte, float)
    if len(P) == 2:
        t = np.linspace(0, 1, n)[:, None]
        return P[0] * (1 - t) + P[1] * t
    P = np.vstack([2 * P[0] - P[1], P, 2 * P[-1] - P[-2]])
    aus = []
    je = max(4, n // (len(P) - 3))
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for t in np.linspace(0, 1, je, endpoint=(i == len(P) - 3)):
            aus.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    return np.array(aus)


def wellen(L, amp, rng, weite=6.0):
    """Linie in der Bildebene (xy) quer zur Richtung leicht verbiegen (glatter Zufall entlang der Linie)."""
    if amp <= 0:
        return L
    d = np.gradient(L[:, :2], axis=0)
    quer = np.stack([-d[:, 1], d[:, 0]], 1)
    quer /= np.maximum(np.linalg.norm(quer, axis=1, keepdims=True), 1e-9)
    s = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(L[:, :2], axis=0), axis=1))])
    k = np.arange(0, s[-1] + weite, weite)
    w = rng.normal(0, 1, len(k))
    off = np.interp(s, k, w) * amp
    L = L.copy()
    L[:, :2] += quer * off[:, None]
    return L


def abstand_t(X, L, z_tor=None):
    """Abstand zur Polylinie, Lage entlang der Linie (0..1) und Vektor vom nächsten Linienpunkt zum Bildpunkt.
    z_tor: Abstand nur in der Bildebene (xy) – auf steilen Flächen (seitlich am Auge) springt z der aufgelegten Linie,
    der 3D-Abstand zerfiele dort in Perlen; z dient dann nur als Tor (Bildpunkte mehr als z_tor mm vor oder hinter der
    Linie zählen nicht, z. B. die Rückseite des Kopfs)."""
    best = np.full(len(X), np.inf)
    sb = np.zeros(len(X))
    vb = np.zeros_like(X)
    qz = np.zeros(len(X))
    seg = np.linalg.norm(np.diff(L, axis=0), axis=1)
    cum = np.concatenate([[0], np.cumsum(seg)])
    k = 2 if z_tor else 3
    for i in range(len(L) - 1):
        a, b = L[i], L[i + 1]
        ab = b - a
        t = np.clip(((X[:, :k] - a[:k]) @ ab[:k]) / max(ab[:k] @ ab[:k], 1e-9), 0.0, 1.0)
        q = a + np.outer(t, ab)
        v = X - q
        d = np.linalg.norm(v[:, :k], axis=1)
        m = d < best
        best[m] = d[m]
        sb[m] = cum[i] + t[m] * seg[i]
        vb[m] = v[m]
        qz[m] = q[m, 2]
    if z_tor:
        best[np.abs(X[:, 2] - qz) > z_tor] = np.inf
        vb[:, 2] = 0.0
    return best, sb / max(cum[-1], 1e-9), vb


class Relief:
    """Höhen (mm) je Kanal auf den gültigen Bildpunkten."""

    def __init__(self, X, N, flaeche, samen):
        self.X = X
        self.N = N
        self.h = np.zeros((len(X), 4))
        self.fl = flaeche
        self.rng = np.random.default_rng(samen)
        self.samen = samen

    def linie(self, kanal, stuetzen, sigma, tiefe, enden=0.5, wellig=0.0, unterbrochen=0.0, wulst=0.0,
              wulst_seite=None, wulst_abstand=2.5, wulst_sigma=None, verlauf=None, n=60, auflegen=True):
        L = kurve(stuetzen, n)
        L = wellen(L, wellig, self.rng)
        if auflegen:
            L = self.fl.auf_haut(L)
        rand = 4.0 * max(sigma, wulst_sigma or 0) + wulst_abstand + 2.0
        lo, hi = L.min(0) - rand, L.max(0) + rand
        lo[2] -= 8.0
        hi[2] += 8.0
        idx = np.nonzero(np.all((self.X >= lo) & (self.X <= hi), 1) & (self.N[:, 2] > -0.6))[0]
        if not len(idx):
            return
        X = self.X[idx]
        d, t, v = abstand_t(X, L, z_tor=6.0)
        form = np.sin(np.pi * np.clip(t, 0.0, 1.0)) ** enden if enden > 0 else 1.0
        if verlauf is not None:
            form = form * np.interp(t, np.linspace(0, 1, len(verlauf)), verlauf)
        if unterbrochen > 0:
            r = rauschen(X, 5.0, int(self.rng.integers(1 << 30)))
            form = form * ((1 - unterbrochen) + unterbrochen * glatt(r, 0.35, 0.65))
        h = -tiefe * form * np.exp(-0.5 * (d / sigma) ** 2)
        if wulst:
            ws = wulst_sigma or 2 * sigma
            seite = 1.0
            if wulst_seite is not None:
                vn = v / np.maximum(np.linalg.norm(v, axis=1, keepdims=True), 1e-9)
                seite = glatt(vn @ np.asarray(wulst_seite, float), -0.2, 0.4)
            h += wulst * form * seite * np.exp(-0.5 * ((d - wulst_abstand) / ws) ** 2)
        self.h[idx, kanal] += h

    def feld(self, kanal, wert):
        self.h[:, kanal] += wert


# ---------------------------------------------------------------- Lippen
def im_vieleck(xy, poly):
    """Punkt-in-Vieleck (Strahlverfahren) in der Bildebene."""
    x, y = xy[:, 0], xy[:, 1]
    innen = np.zeros(len(xy), bool)
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, yi = poly[i]
        xj, yj = poly[j]
        kreuzt = ((yi > y) != (yj > y)) & (x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi)
        innen ^= kreuzt
        j = i
    return innen


def abstand_vieleck(xy, poly):
    best = np.full(len(xy), np.inf)
    P = np.vstack([poly, poly[:1]])
    for a, b in zip(P[:-1], P[1:]):
        ab = b - a
        t = np.clip(((xy - a) @ ab) / max(ab @ ab, 1e-9), 0, 1)
        best = np.minimum(best, np.linalg.norm(xy - (a + np.outer(t, ab)), axis=1))
    return best


# ---------------------------------------------------------------- Zonen und Glanz (1024)
def zonen_und_glanz(X, N, P, masken_b, gk2=False):
    n = len(X)
    Z = np.zeros((n, 4))
    G = np.zeros((n, 4))
    vorn = glatt(N[:, 2], -0.25, 0.35)
    vorn_streng = glatt(N[:, 2], 0.05, 0.45)
    r1 = rauschen(X, 9.0, 11, 2)
    r2 = rauschen(X, 4.0, 12, 2)
    r3 = rauschen(X, 14.0, 13, 2)
    unruhig = 0.65 + 0.7 * r1                   # unregelmäßige Ränder und Stärke
    br_y = 0.5 * (P["br_r"][1] + P["br_l"][1])
    stirn_oben = P["g"][1] + 52.0
    ps_y = 0.5 * (P["ps_r"][1] + P["ps_l"][1])
    t_z = 0.5 * (P["t_r"][2] + P["t_l"][2])
    # Gesichtsvorderseite (vor den Ohren)
    gesicht = glatt(X[:, 2], t_z + 8.0, t_z + 25.0)
    # ---- Stirn, Schläfe, Nase, Wange, Kinn als weiche Masken (auch für Glanz und Poren)
    stirn = glatt(X[:, 1], br_y + 1.0, br_y + 7.0) * glatt(np.abs(X[:, 0]), 62.0, 45.0) * vorn \
        * glatt(X[:, 1], stirn_oben + 25.0, stirn_oben + 5.0)
    schlaefe = glatt(np.abs(X[:, 0]), 46.0, 58.0) * glatt(X[:, 1], ps_y - 10.0, ps_y) \
        * glatt(X[:, 1], br_y + 40.0, br_y + 25.0) * gesicht
    nase_l = np.array([P["n"] + [0, 2, 0], P["prn"] + [0, 1, 0], P["sn"] + [0, 2, -3]])
    d_nase = np.full(n, np.inf)
    for a, b in zip(nase_l[:-1], nase_l[1:]):
        ab = b - a
        tt = np.clip(((X - a) @ ab) / (ab @ ab), 0, 1)
        d_nase = np.minimum(d_nase, np.linalg.norm(X - (a + np.outer(tt, ab)), axis=1))
    nase = glatt(d_nase, 11.0, 6.0) * glatt(X[:, 1], P["sn"][1] - 3.0, P["sn"][1] + 1.0)
    nasenspitze = gauss(X, P["prn"] + [0, 1, -2], (7.0, 6.0, 7.0))
    kinn = gauss(X, P["pg"] + [0, 2, 0], (13.0, 10.0, 10.0)) * vorn
    wange = np.zeros(n)
    innenwange = np.zeros(n)
    unter_auge = np.zeros(n)
    lid = np.zeros(n)
    ohr = np.zeros(n)
    karunkel = np.zeros(n)
    feucht = np.zeros(n)
    wasser = np.zeros(n)
    wimpernlinie = np.zeros(n)
    for s, sg in (("r", -1.0), ("l", 1.0)):
        ex, en, ch, al = P[f"ex_{s}"], P[f"en_{s}"], P[f"ch_{s}"], P[f"al_{s}"]
        c = ex + 0.45 * (ch - ex) + np.array([sg * 8.0, 2.0, 0.0])
        wange = np.maximum(wange, gauss(X, c, (17.0, 13.0, 15.0)) * vorn)
        innenwange = np.maximum(innenwange, gauss(X, al + [sg * 15.0, 4.0, -6.0], (9.0, 9.0, 9.0)) * vorn)
        lu = [40, 41] if s == "r" else [47, 46]
        lo = [37, 38] if s == "r" else [43, 44]
        lid_u = 0.5 * (P[f"lm{lu[0]}"] + P[f"lm{lu[1]}"])
        lid_o = 0.5 * (P[f"lm{lo[0]}"] + P[f"lm{lo[1]}"])
        unter_auge = np.maximum(unter_auge, gauss(X, lid_u + [sg * 1.0, -6.5, -1.0], (11.0, 4.5, 6.0)) * vorn)
        lid = np.maximum(lid, gauss(X, 0.5 * (lid_u + lid_o), (13.0, 7.0, 8.0)))
        k = gauss(X, en + np.array([sg * -1.2, 0.0, -1.0]), (1.6, 1.4, 2.0))
        if gk2:
            # Grundkopf 2: Karunkel etwas größer (rosa Wulst im inneren Lidwinkel, Prüfer: „leerer Blick“)
            k = np.maximum(k, gauss(X, en + np.array([sg * -1.6, -0.2, -1.6]), (2.1, 1.7, 2.4)))
        karunkel = np.maximum(karunkel, k)
        ring = [36, 41, 40, 39] if s == "r" else [45, 46, 47, 42]
        oben = [36, 37, 38, 39] if s == "r" else [45, 44, 43, 42]
        for pk, st, sig in ((ring, 1.0, 0.7), (oben, 0.4, 0.6)):
            L = np.array([P[f"lm{i}"] for i in pk])
            d, _, _ = abstand_t(X, L)
            feucht = np.maximum(feucht, st * np.exp(-0.5 * (d / sig) ** 2))
        if gk2:
            # Grundkopf 2: Wasserlinie – die Lidkante, die zum Augapfel schaut (bis 1,4 mm vom Rand), feucht und rosa;
            # Wimpernlinie – ein schmaler Schatten der Wimpernwurzeln direkt über dem Oberlidrand
            mitte = P[f"ps_{s}"] + np.array([0.0, 0.0, -12.0])
            zum_auge = mitte - X
            zum_auge /= np.maximum(np.linalg.norm(zum_auge, axis=1, keepdims=True), 1e-9)
            L = np.array([P[f"lm{i}"] for i in ring + ring[:1]])
            d, _, _ = abstand_t(X, L)
            innen = glatt((N * zum_auge).sum(1), 0.05, 0.45) * glatt(d, 1.4, 0.6)
            wasser = np.maximum(wasser, innen)
            Lo = np.array([P[f"lm{i}"] for i in oben])
            do, _, _ = abstand_t(X, Lo)
            o = np.argsort(Lo[:, 0])
            ueber = glatt(X[:, 1] - np.interp(X[:, 0], Lo[o, 0], Lo[o, 1]), -0.3, 0.2)
            wimpernlinie = np.maximum(wimpernlinie, np.exp(-0.5 * (do / 0.5) ** 2) * ueber * (1.0 - innen))
        t = P[f"t_{s}"]
        o = glatt((X[:, 0] * sg - (abs(t[0]) - 4.0)), 0.0, 6.0) * glatt(np.abs(X[:, 1] - (t[1] + 5.0)), 38.0, 30.0) \
            * (np.abs(X[:, 2] - t[2]) < 32)
        ohr = np.maximum(ohr, o)
        # ---- Augenringe: unter dem Auge zum inneren Winkel stärker, inneres Oberlid
        Z[:, 2] += 0.75 * gauss(X, lid_u + [sg * -2.0, -5.5, -1.0], (9.0, 4.0, 6.0)) * vorn
        Z[:, 2] += 0.6 * gauss(X, en + [sg * -0.5, -4.0, -1.0], (4.5, 4.5, 5.0))
        Z[:, 2] += 0.35 * gauss(X, en + [sg * 2.0, 5.0, -2.0], (5.0, 3.5, 5.0))
        Z[:, 2] += 0.25 * gauss(X, lid_o + [0, 3.0, -1.5], (9.0, 3.0, 5.0)) * vorn
        # Schläfenvene (schwach)
        Z[:, 2] += 0.22 * gauss(X, ex + [sg * 14.0, 12.0, -14.0], (6.0, 12.0, 10.0)) * (r2 > 0.45)
        # ---- Bartgebiet: unter der Linie Koteletten – Wange – Mundwinkel – Nasenflügel, vor dem Ohr
        # Grenzlinie in Abhängigkeit von |x| (Gesichtsvorderseite) bzw. z (Seite)
    # Bart: Grenzhöhe über |x| (vorn) gemittelt beider Seiten
    ax = np.abs(X[:, 0])
    al_x = 0.5 * (abs(P["al_r"][0]) + abs(P["al_l"][0]))
    ch_x = 0.5 * (abs(P["ch_r"][0]) + abs(P["ch_l"][0]))
    ch_y = 0.5 * (P["ch_r"][1] + P["ch_l"][1])
    t_y = 0.5 * (P["t_r"][1] + P["t_l"][1])
    zy_x = 0.5 * (abs(P["zy_r"][0]) + abs(P["zy_l"][0]))
    # Runde 2 (Prüfer: „grauer Fleck neben der Nase wie Schmutz“): Oberlippe nur bis zum Mundwinkel, seitlich der Nase
    # kein Bart; die Grenze steigt erst außerhalb der Mundwinkel zur Wange an
    kx = [0.0, al_x * 0.5, al_x + 1.0, ch_x + 2.0, ch_x + 10.0, ch_x + 22.0, zy_x - 8.0, zy_x + 6.0]
    ky = [P["sn"][1] - 2.0, P["sn"][1] - 3.0, P["sn"][1] - 5.5, ch_y + 3.0, ch_y + 10.0, ch_y + 20.0, ps_y - 24.0, t_y + 12.0]
    grenze = np.interp(ax, kx, ky)
    # Seite hinter dem Jochbein: Koteletten bis zur Ohrwurzel
    vor_ohr = glatt(X[:, 2], t_z + 4.0, t_z + 12.0)
    bart = glatt(X[:, 1], grenze + 4.0, grenze - 6.0) * vor_ohr
    if gk2:
        # Grundkopf 2 (Prüfer: „Bartschatten mit harter Kante“): Übergang 38 mm statt 10 mm, oben flach auslaufend
        bart = glatt(X[:, 1], grenze + 10.0, grenze - 28.0) ** 2.0 * glatt(X[:, 2], t_z - 2.0, t_z + 20.0)
    # unten: Hals bis etwa 45 mm unter dem Kinn, hinten nicht in den Nacken
    bart *= glatt(X[:, 1], P["me"][1] - 55.0, P["me"][1] - 35.0)
    bart *= glatt(X[:, 2], t_z - 6.0, t_z + 6.0)
    # Lippen frei (Lippenrot), 1,5 mm Rand
    aussen = np.array([P[f"lm{i}"][:2] for i in range(48, 60)])
    lip_d = abstand_vieleck(X[:, :2], aussen)
    lip_in = im_vieleck(X[:, :2], aussen) & (X[:, 2] > P["sto"][2] - 10.0)
    lippen = np.where(lip_in, 1.0, glatt(lip_d, 1.6, 0.0)) * (X[:, 2] > P["sto"][2] - 10.0)
    bart *= 1.0 - lippen
    bart *= 0.75 + 0.5 * r2                       # fleckig dichter / lichter
    Z[:, 3] = np.clip(bart, 0, 1)
    # ---- Hämoglobin
    hb = 0.8 * wange + 0.45 * innenwange + 0.45 * nasenspitze * vorn_streng + 0.25 * nase * vorn_streng
    for s in ("r", "l"):
        hb += 0.25 * gauss(X, P[f"al_{s}"] + [0, 0, -1.0], (5.0, 4.5, 5.0)) * vorn_streng
    # Runde 3 (Prüfer: Lidränder und Ohren „glühen orange“): Lid, Lidrand und Ohr schwächer
    hb += 0.45 * ohr + 0.3 * kinn + 0.9 * karunkel + 0.12 * lid + 0.12 * stirn * (r3 > 0.5) + 0.15 * feucht
    hb += 0.15 * gauss(X, P["g"], (10.0, 8.0, 10.0))
    if gk2:
        hb += 0.35 * karunkel + 0.55 * wasser
    Z[:, 0] = hb * unruhig
    # ---- Sonne und Gelbton
    scheitel = glatt(X[:, 1], stirn_oben + 10.0, stirn_oben + 35.0)
    nacken = glatt(X[:, 2], t_z - 10.0, t_z - 30.0) * glatt(X[:, 1], t_y - 20.0, t_y - 45.0) \
        * glatt(X[:, 1], P["me"][1] - 90.0, P["me"][1] - 60.0)
    jochbein = np.zeros(n)
    for s, sg in (("r", -1.0), ("l", 1.0)):
        jochbein = np.maximum(jochbein, gauss(X, P[f"ex_{s}"] + [sg * 4.0, -16.0, -2.0], (13.0, 8.0, 12.0)))
    ohrrand = ohr * glatt(np.abs(X[:, 0]), np.abs(P["t_r"][0]) + 6.0, np.abs(P["t_r"][0]) + 14.0)
    sonne = np.maximum.reduce([0.85 * stirn, 0.55 * schlaefe, 0.6 * nase * vorn, 0.5 * jochbein * vorn, 0.6 * ohrrand,
                               0.35 * scheitel, 0.55 * nacken, 0.2 * kinn])
    Z[:, 1] = sonne * (0.7 + 0.6 * r3)
    # Mundpartie etwas dunkler (Augenringe-Kanal schwach)
    mund = gauss(X, P["sto"] + [0, 1.0, -2.0], (20.0, 13.0, 12.0)) * vorn * (1.0 - lippen)
    Z[:, 2] += 0.18 * mund
    Z[:, 2] *= 0.75 + 0.5 * r2
    if gk2:
        Z[:, 2] += 0.45 * wimpernlinie
    # ---- Glanz: Rauheit je Zone
    rau = np.full(n, 0.52)
    rau = rau + (0.42 - rau) * stirn
    rau = rau + (0.40 - rau) * np.clip(nase * 1.2, 0, 1) * vorn
    rau = rau + (0.37 - rau) * nasenspitze
    rau = rau + (0.45 - rau) * kinn
    rau = rau + (0.49 - rau) * np.clip(wange * 1.3, 0, 1)
    rau = rau + (0.50 - rau) * unter_auge
    rau = rau + (0.46 - rau) * lid * (1 - unter_auge)
    rau = rau + (0.33 - rau) * lippen
    rau = rau + (0.56 - rau) * glatt(X[:, 1], P["me"][1] - 5.0, P["me"][1] - 25.0)   # Hals
    rau += 0.04 * (r2 - 0.5)
    G[:, 0] = np.clip((rau - 0.25) / 0.4, 0, 1)
    poren = 0.5 + 0.3 * stirn * 0.0
    poren = np.full(n, 0.45)
    poren = poren + (0.65 - poren) * stirn
    poren = poren + (0.6 - poren) * np.clip(wange * 1.3, 0, 1)
    poren = poren + (0.9 - poren) * innenwange
    poren = poren + (1.0 - poren) * np.clip(nase * 1.3, 0, 1) * vorn
    poren = poren + (0.7 - poren) * kinn
    poren = poren + (0.35 - poren) * schlaefe
    poren = poren + (0.2 - poren) * unter_auge
    poren = poren + (0.08 - poren) * glatt(lid, 0.45, 0.8)
    poren = poren + (0.3 - poren) * ohr
    poren = poren * (1.0 - lippen)
    G[:, 1] = np.clip(poren, 0, 1)
    # ---- Höhle: Nasenloch innen. Eine Innenwand blickt zur Nasenlochachse (Mitte zwischen Steg und Flügelwand, über
    # der Öffnung); die Unterseite des Flügelrands und des Stegs blickt von ihr weg und bleibt Haut. Die gebackene
    # Verdeckung ist am äußeren Flügelansatz ebenso hoch und taugt dort nicht.
    al_x = 0.5 * (abs(P["al_r"][0]) + abs(P["al_l"][0]))
    sn, prn = P["sn"], P["prn"]
    ax = np.abs(X[:, 0])
    c_n = np.stack([np.sign(X[:, 0]) * 0.5 * (al_x + 0.5), np.full(n, sn[1] + 5.0),
                    np.full(n, 0.5 * (sn[2] + prn[2]) - 1.0)], 1)
    zur_achse = c_n - X
    zur_achse /= np.maximum(np.linalg.norm(zur_achse, axis=1, keepdims=True), 1e-9)
    innen = glatt((N * zur_achse).sum(1), -0.05, 0.4)
    feld = glatt(ax, 2.0, 3.5) * glatt(ax, al_x + 0.5, al_x - 1.5) * glatt(X[:, 1], sn[1] + 1.5, sn[1] + 3.0) \
        * glatt(X[:, 1], prn[1] + 14.0, prn[1] + 9.0) * glatt(X[:, 2], prn[2] - 1.5, prn[2] - 4.0) \
        * glatt(X[:, 2], sn[2] - 20.0, sn[2] - 15.0)
    # Tiefe über der Öffnung: die Unterkante der Nase steigt vom Flügelansatz zur Spitze an; die Unterseite des
    # Flügelrands (sichtbar bei Stupsnasen und schräg) bleibt Haut, dunkel wird es erst 1–4 mm darüber
    y_unten = np.interp(X[:, 2], [sn[2] - 12.0, prn[2] - 2.0], [sn[1] + 1.5, prn[1] - 3.0])
    loch = feld * innen * glatt(X[:, 1] - y_unten, 0.8, 6.0)
    nase_feld = glatt(ax, al_x + 4.0, al_x + 1.0) * glatt(np.abs(X[:, 1] - 0.5 * (sn[1] + prn[1])), 12.0, 8.0) \
        * glatt(X[:, 2], sn[2] - 14.0, sn[2] - 8.0)
    hoehle = ohr * (1.0 - nase_feld) * glatt(masken_b, 0.35, 0.9) * 0.8     # sonst nur Ohrmuschel und Gehörgang
    hoehle = np.maximum(hoehle, loch * (0.8 + 0.2 * glatt(masken_b, 0.1, 0.5)))
    G[:, 2] = np.clip(hoehle, 0, 1)
    # A: Feuchte (Lidrand, Karunkel ≥ 0,45) und Lippenrot als 0,3 (der Shader trennt beides; feucht wirkt erst ab 0,45)
    G[:, 3] = np.clip(np.maximum(np.maximum(feucht, karunkel), 0.3 * lippen * (feucht < 0.2)), 0, 1)
    if gk2:
        G[:, 3] = np.maximum(G[:, 3], np.clip(wasser, 0, 1))
    Z[:, 0] = np.clip(Z[:, 0], 0, 1)
    Z[:, 1] = np.clip(Z[:, 1], 0, 1)
    Z[:, 2] = np.clip(Z[:, 2], 0, 1)
    return Z, G, {"lippen_innen": int(lip_in.sum())}


# ---------------------------------------------------------------- Relief (2048)
def relief_bauen(X, N, P, fl, samen):
    R = Relief(X, N, fl, samen)
    rng = R.rng
    br_y = 0.5 * (P["br_r"][1] + P["br_l"][1])
    g = P["g"]
    # ---- R: Stirn
    # Stirnfalten: ungleiche Abstände, jede aus zwei bis drei versetzten Stücken (nicht als durchgehende Ringe),
    # dazwischen kurze Nebenfalten
    y = br_y + 10.0
    for k, tiefe in enumerate((0.26, 0.32, 0.3, 0.22)):
        y += rng.uniform(5.5, 9.0) if k else 0.0
        breite = 38.0 - 2.0 * k + rng.normal(0, 2.0)
        bogen = 3.0 + rng.normal(0, 1.0)
        grenzen = np.sort(rng.uniform(-0.45, 0.45, int(rng.integers(1, 3)))) * breite
        stuecke = np.concatenate([[-breite], grenzen, [breite]])
        for a, b in zip(stuecke[:-1], stuecke[1:]):
            dy = rng.normal(0, 0.9)
            xs = np.linspace(a - 2.0, b + 2.0, 5)
            stuetz = [[x, y + dy + bogen * (x / breite) ** 2 + rng.normal(0, 0.4), 0] for x in xs]
            R.linie(0, stuetz, sigma=rng.uniform(0.75, 1.05), tiefe=tiefe * rng.uniform(0.75, 1.1), enden=0.6,
                    wellig=0.5, unterbrochen=0.4, n=50)
    for k in range(7):
        x0 = rng.uniform(-30, 30)
        y0 = br_y + rng.uniform(12.0, 34.0)
        lg = rng.uniform(5.0, 12.0)
        R.linie(0, [[x0, y0, 0], [x0 + lg, y0 + rng.normal(0, 1.2), 0]], sigma=0.5, tiefe=0.1, enden=0.8, wellig=0.3,
                n=16)
    for sg in (-1, 1):
        x0 = sg * (5.5 + rng.normal(0, 0.6))
        R.linie(0, [[x0, br_y - 3.0, 0], [x0 + sg * 0.8, br_y + 5.0, 0], [x0 + sg * 2.2, br_y + 12.0, 0]],
                sigma=0.85, tiefe=0.2, enden=0.8, wellig=0.35)
    R.linie(0, [[-8, P["n"][1] + 3.0, 0], [0, P["n"][1] + 2.5, 0], [8, P["n"][1] + 3.0, 0]], sigma=0.7, tiefe=0.15,
            enden=0.6)
    # ---- G: Augen
    for s, sg in (("r", -1.0), ("l", 1.0)):
        ex = P[f"ex_{s}"]
        for wi in (-48, -28, -10, 6, 22, 38):
            w = np.radians(wi + rng.normal(0, 5.0))
            start = 3.5 + rng.uniform(0, 2.0)
            laenge = rng.uniform(8.0, 15.0) * (0.75 if abs(wi) > 40 else 1.0)
            krumm = rng.normal(-1.0, 0.8)
            pts = []
            for f in np.linspace(0, 1, 5):
                rr = start + f * laenge
                pts.append([ex[0] + sg * rr * np.cos(w), ex[1] + rr * np.sin(w) + krumm * f * f, 0])
            R.linie(1, pts, sigma=rng.uniform(0.4, 0.6), tiefe=rng.uniform(0.18, 0.28), enden=0.7, wellig=0.3,
                    unterbrochen=0.3)
        lu = [41, 40] if s == "r" else [46, 47]
        a, b = P[f"lm{lu[0]}"], P[f"lm{lu[1]}"]
        en = P[f"en_{s}"]
        for k in range(9):
            f = rng.uniform(0.1, 1.1)
            basis = en + (ex - en) * f
            y = basis[1] - rng.uniform(2.5, 7.5)
            lg = rng.uniform(3.0, 6.5)
            w = np.radians(rng.uniform(-35, -5))
            R.linie(1, [[basis[0], y, 0], [basis[0] + sg * lg * np.cos(w), y + lg * np.sin(w), 0]], sigma=0.32,
                    tiefe=rng.uniform(0.06, 0.12), enden=0.8, wellig=0.15, n=16)
    # ---- B: Mittelform
    for s, sg in (("r", -1.0), ("l", 1.0)):
        en, ex, ps, al, ch = P[f"en_{s}"], P[f"ex_{s}"], P[f"ps_{s}"], P[f"al_{s}"], P[f"ch_{s}"]
        # Nasolabialfalte mit Wangenwulst außen
        R.linie(2, [[al[0] + sg * 2.5, al[1] + 4.5, 0], [al[0] + sg * 5.0, al[1] - 3.0, 0],
                    [al[0] + sg * 7.5, al[1] - 13.0, 0], [ch[0] + sg * 8.5, ch[1] - 7.0, 0]],
                sigma=1.7, tiefe=0.6, enden=0.45, wellig=0.3, wulst=0.3, wulst_seite=[sg * 0.8, 0.6, 0.0],
                wulst_abstand=3.2, wulst_sigma=3.0, verlauf=[0.7, 1.0, 1.0, 0.75, 0.4], n=70)
        # Nasenflügelrinne (Bogen außen um den Flügel)
        c = np.array([al[0] - sg * 4.0, al[1] + 0.5, 0])
        phi = np.radians(np.linspace(105, -75, 9))
        R.linie(2, [[c[0] + sg * 5.2 * np.cos(p), c[1] + 5.2 * np.sin(p), 0] for p in phi], sigma=0.65, tiefe=0.45,
                enden=0.4, n=50)
        # Tränenrinne: innen tief, außen als Lid-Wangen-Grenze flacher
        R.linie(2, [[en[0] + sg * 1.0, en[1] - 2.5, 0], [en[0] + sg * 5.5, en[1] - 8.0, 0],
                    [ps[0] + sg * 2.0, en[1] - 12.0, 0], [ex[0] + sg * 3.0, ex[1] - 12.5, 0]],
                sigma=1.6, tiefe=0.45, enden=0.35, wellig=0.2, wulst=0.15, wulst_seite=[0, -1.0, 0], wulst_abstand=3.5,
                wulst_sigma=2.5, verlauf=[1.0, 1.0, 0.65, 0.35], n=60)
        # Unterlidfalte (4 mm unter dem Lidrand)
        lu = [39, 40, 41, 36] if s == "r" else [42, 47, 46, 45]
        L = [P[f"lm{i}"] + [0, -3.8 - 0.6 * (0 < j < 3), 0] for j, i in enumerate(lu)]
        R.linie(2, [[q[0], q[1], 0] for q in L], sigma=0.55, tiefe=0.16, enden=0.6, wellig=0.15, n=40)
        # Oberlidfalte (Deckfalte), außen etwas höher
        lo = [39, 38, 37, 36] if s == "r" else [42, 43, 44, 45]
        L = [P[f"lm{i}"] + [sg * 0.5 * j, 5.5 + 0.6 * j, 0] for j, i in enumerate(lo)]
        R.linie(2, [[q[0], q[1], 0] for q in L], sigma=0.7, tiefe=0.22, enden=0.5, wellig=0.1, n=40)
    # Kinnlippenfurche
    li_y = P["li"][1] if np.isfinite(P["li"][1]) else P["sto"][1] - 10.0
    ys = li_y - 0.45 * (li_y - P["pg"][1])
    R.linie(2, [[-15, ys + 3.5, 0], [-7, ys + 0.3, 0], [0, ys - 0.3, 0], [7, ys + 0.3, 0], [15, ys + 3.5, 0]],
            sigma=2.0, tiefe=0.45, enden=0.5, wellig=0.2, n=60)
    # Philtrum: zwei Leisten, Rinne dazwischen
    sn, ls = P["sn"], P["ls"] if np.isfinite(P["ls"][1]) else P["sto"] + [0, 6, 0]
    for sg in (-1, 1):
        R.linie(2, [[sg * 3.8, sn[1] - 1.5, 0], [sg * 5.2, ls[1] + 1.0, 0]], sigma=1.0, tiefe=-0.18, enden=0.4, n=20)
    R.linie(2, [[0, sn[1] - 1.5, 0], [0, ls[1] + 1.5, 0]], sigma=1.6, tiefe=0.12, enden=0.4, n=20)
    # Lippenrillen (senkrecht, auf dem Lippenrot)
    aussen = np.array([P[f"lm{i}"][:2] for i in range(48, 60)])
    innen = np.array([P[f"lm{i}"][:2] for i in range(60, 68)])
    vorn_lippe = X[:, 2] > P["sto"][2] - 10.0
    auf = im_vieleck(X[:, :2], aussen) & ~im_vieleck(X[:, :2], innen) & vorn_lippe
    idx = np.nonzero(auf)[0]
    if len(idx):
        xy = X[idx, :2]
        rand = np.minimum(abstand_vieleck(xy, aussen), abstand_vieleck(xy, innen))
        w = rauschen(X[idx], 3.0, samen + 7)
        sp = 0.95
        phase = (xy[:, 0] + 1.6 * (w - 0.5)) / sp
        rille = np.clip(1.0 - np.abs(phase - np.round(phase)) * 2.0, 0, 1) ** 5
        R.h[idx, 2] += -0.11 * rille * glatt(rand, 0.2, 1.4) * (0.6 + 0.8 * rauschen(X[idx], 2.0, samen + 8))
    # ---- A: Mund und Hals
    for s, sg in (("r", -1.0), ("l", 1.0)):
        ch, al = P[f"ch_{s}"], P[f"al_{s}"]
        R.linie(3, [[ch[0] + sg * 1.5, ch[1] - 2.5, 0], [ch[0] + sg * 3.0, ch[1] - 12.0, 0],
                    [ch[0] + sg * 5.5, ch[1] - 22.0, 0]], sigma=1.3, tiefe=0.45, enden=0.5, wellig=0.3, wulst=0.2,
                wulst_seite=[sg, 0.2, 0], wulst_abstand=2.4, wulst_sigma=2.0, n=40)
        # Fältchen der Oberlippe und Unterlippe
        for k in range(5):
            x = sg * (2.5 + 3.4 * k + rng.normal(0, 0.6))
            yo = np.interp(abs(x), [0, 4, 10, 16, 24], [P["lm51"][1], P["lm50"][1] + 0.3, P["lm49"][1] + 1.0,
                                                         P["lm48"][1] + 2.0, P["lm48"][1] + 2.0])
            lg = rng.uniform(3.0, 7.0) * (1.0 - 0.1 * k)
            R.linie(3, [[x, yo + 0.8, 0], [x + sg * rng.uniform(0.3, 1.5), yo + 0.8 + lg, 0]], sigma=0.3,
                    tiefe=rng.uniform(0.06, 0.11), enden=0.7, n=14)
        for k in range(3):
            x = sg * (3.0 + 4.5 * k + rng.normal(0, 0.7))
            yu = np.interp(abs(x), [0, 6, 14, 24], [P["lm57"][1], P["lm56"][1] - 0.3, P["lm55"][1] - 1.0,
                                                    P["lm54"][1] - 1.0])
            lg = rng.uniform(2.5, 4.5)
            R.linie(3, [[x, yu - 1.0, 0], [x + sg * rng.uniform(0.2, 1.0), yu - 1.0 - lg, 0]], sigma=0.3,
                    tiefe=rng.uniform(0.05, 0.09), enden=0.7, n=12)
        # Wangenfalten (seitlich der Nasolabialfalte, schräg nach unten)
        for k in range(2):
            x0 = al[0] + sg * (16.0 + 7.0 * k + rng.normal(0, 1.0))
            y0 = al[1] + 3.0 - 3.0 * k
            R.linie(3, [[x0, y0, 0], [x0 + sg * 3.0, y0 - 14.0, 0], [x0 + sg * 5.0, y0 - 26.0, 0]],
                    sigma=1.4, tiefe=0.13, enden=0.8, wellig=0.6, unterbrochen=0.5, n=40)
    me = P["me"]
    for dy, tf in ((-27.0, 0.26), (-38.0, 0.2), (-47.0, 0.14)):
        y0 = me[1] + dy + rng.normal(0, 1.5)
        a, b = -rng.uniform(25, 46), rng.uniform(25, 46)
        xs = np.linspace(a, b, 6)
        R.linie(3, [[x, y0 + 3.0 * (x / 45.0) ** 2 + rng.normal(0, 0.8), 0] for x in xs], sigma=rng.uniform(1.0, 1.5),
                tiefe=tf, enden=0.6, wellig=1.0, unterbrochen=0.5, n=80)
    return R.h


# ---------------------------------------------------------------- Schreiben
def schreiben(pfad, bild):
    g = bild.shape[0]
    spec = oiio.ImageSpec(g, g, 4, oiio.FLOAT)
    spec.alpha_channel = -1                       # Datenkanal, kein Alpha (sonst teilt OIIO RGB durch A)
    spec.attribute("oiio:UnassociatedAlpha", 1)
    buf = oiio.ImageBuf(spec)
    buf.set_pixels(oiio.ROI(0, g, 0, g, 0, 1, 0, 4), np.ascontiguousarray(bild.astype(np.float32)))
    buf.write(pfad, "uint8")


def lesen(pfad, g):
    # Kanal 4 ist ein Datenkanal: ohne diesen Hinweis multipliziert OIIO RGB beim Lesen mit A (vormultipliziert)
    cfg = oiio.ImageSpec()
    cfg.attribute("oiio:UnassociatedAlpha", 1)
    buf = oiio.ImageBuf(pfad, 0, 0, cfg)
    a = buf.get_pixels(oiio.FLOAT)
    if a.shape[0] != g:
        k = a.shape[0] // g
        a = a.reshape(g, k, g, k, a.shape[-1]).mean((1, 3))
    return a


def main():
    glb_pfad, aus = sys.argv[1], sys.argv[2]
    opt = dict(a.split("=", 1) for a in sys.argv[3:])
    g_rel = int(opt.get("relief", "2048"))
    g_zon = int(opt.get("zonen", "1024"))
    samen = int(opt.get("samen", "6107"))
    glb = Glb(glb_pfad)
    netz = Kopfnetz(glb)
    haut = netz.haut
    P = netz.punkte({})
    H = haut.grund * 1000.0
    N = haut.flaechennormalen(haut.grund)
    fl = Netzflaeche(H, N)
    os.makedirs(aus, exist_ok=True)
    masken = os.path.join(aus, "haut_masken.png")
    # ---- Zonen und Glanz
    ok, X, Nn, _ = lagekarte(haut, H, N, g_zon)
    log("Lagekarte", g_zon, int(ok.sum()), "Bildpunkte")
    mb = lesen(masken, g_zon)[..., 2] if os.path.exists(masken) else np.zeros((g_zon, g_zon))
    Z, G, info = zonen_und_glanz(X[ok], Nn[ok], P, mb[ok], gk2=opt.get("gk2", "0") == "1")
    bz = np.zeros((g_zon, g_zon, 4))
    bg = np.zeros((g_zon, g_zon, 4))
    bg[..., 0] = 0.6
    bg[..., 1] = 0.4
    bz[ok] = Z
    bg[ok] = G
    schreiben(os.path.join(aus, "haut_c_zonen.png"), bz)
    schreiben(os.path.join(aus, "haut_c_glanz.png"), bg)
    log("Zonen und Glanz", {k: round(float(Z[:, i].max()), 2) for i, k in enumerate("RGBA")}, info)
    # ---- Relief
    ok, X, Nn, mm = lagekarte(haut, H, N, g_rel)
    log("Lagekarte", g_rel, int(ok.sum()), "Bildpunkte, Median", round(float(np.median(mm[ok])), 3), "mm je Bildpunkt")
    h = relief_bauen(X[ok], Nn[ok], P, fl, samen)
    hb = h / mm[ok][:, None]                      # Bildpunkt-Einheiten: Gefälle = Differenz benachbarter Bildpunkte
    massstab = [float(max(np.percentile(np.abs(hb[:, i]), 99.95), 1e-3)) * 1.05 for i in range(4)]
    bild = np.full((g_rel, g_rel, 4), 0.5)
    rng = np.random.default_rng(samen + 1)
    q = 0.5 + 0.5 * np.clip(hb / np.array(massstab), -1, 1)
    q += (rng.random(q.shape) - 0.5) / 255.0       # Zittern gegen Stufen im 8-Bit-Gefälle
    bild[ok] = np.clip(q, 0, 1)
    schreiben(os.path.join(aus, "haut_c_relief.png"), bild)
    beschr = {"relief_bp": [round(m, 5) for m in massstab], "relief_groesse": g_rel, "zonen_groesse": g_zon,
              "mm_je_bildpunkt_median": round(float(np.median(mm[ok])), 4), "samen": samen,
              "kanaele": {"relief": "R Stirn, G Augen, B Mittelform, A Mund/Hals",
                          "zonen": "R Hämoglobin, G Sonne/Gelb, B Augenringe/Venen, A Bart",
                          "glanz": "R Rauheit (0,25 + 0,4 R), G Poren, B Höhle, A Feuchte"}}
    json.dump(beschr, open(os.path.join(aus, "haut_c.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    log("RELIEF", beschr["relief_bp"], "max mm", [round(float(np.abs(h[:, i]).max()), 3) for i in range(4)])
    log("FERTIG", aus)


if __name__ == "__main__":
    main()
