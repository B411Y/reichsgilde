"""Charakter Phase 4: Haarkarten aus den Leitsträhnen, Kartenbild und Kopfhautkarten (eigener Code, MIT;
läuft in Blenders Python mit numpy und OpenImageIO).

- **Karten:** Die Strähnen eines Stils werden zu Büscheln zusammengefasst (am weitesten auseinanderliegende Wurzeln);
  je Büschel eine gebogene Karte entlang der Mittelsträhne, so breit wie das Büschel, mit der Fläche zum Kopf hin
  ausgerichtet (Normale vom Kopf weg, Tangente = Haarrichtung für den Glanz). Zwei Lagen: innen breit und dicht
  (deckt die Kopfhaut), außen schmal und locker (Umriss, einzelne Strähnen).
- **Kartenbild** `haar_karten.png` (2048², linear): 8 Spalten zu 256 Bildpunkten, je Spalte eine Sorte (0–3 Haupthaar
  dicht/locker, 4–5 Haupthaar außen, 6–7 Bart kraus). Je Spalte läuft v von der Wurzel (Bildzeile 0) zur Spitze.
  R = Zufallswert des Haars (Ergrauen, Ausdünnen), G = Helligkeit des Haars, B = Lage im Haar (0 Wurzel, 1 Spitze),
  A = Deckung.
- **Kopfhautkarten** in der Spiel-UV (1024²): `kopfhaut.png` RGB = Dichte der Männerfrisuren (kurz, Pagenkopf,
  zurück), A = Rang beim Ausfallen (Glatze); `kopfhaut_w.png` RGB = Frauenfrisuren (Knoten, offen, Zöpfe);
  `bart.png` RGBA = Schnurrbart, Kinnbart, Vollbart, langer Bart. Der Hautshader färbt damit die Haut unter dem Haar
  zur Haarfarbe hin (kein heller Schimmer durch das Haar, kein Helmrand) und legt Bartschatten nur, wo der Bart wächst.
"""
import math

import numpy as np

from kopf4_daten import glatt

SPALTEN = 8
ATLAS = 2048


# ---------------------------------------------------------------- Karten
LAGEN_HAAR = ({"je": 14, "anteil": 1.0, "lage": 0, "spalten": (0, 1, 2, 3), "breite": (0.006, 0.014), "faktor": 1.25},
              {"je": 6, "anteil": 0.6, "lage": 0.5, "spalten": (0, 1, 3), "breite": (0.004, 0.009), "faktor": 1.0},
              {"je": 1, "anteil": 0.4, "lage": 1, "spalten": (4, 5), "breite": (0.0025, 0.0045), "faktor": 1.0})


def karten(st, wurzeln_, h, rng, lagen=LAGEN_HAAR, bart=False, mitte=None):
    """Karten aus Strähnen (S,P,3). Je Lage: „je“ Strähnen je Büschel (1 = eine schmale Strähnenkarte je Strähne für einen
    Anteil der Strähnen), Lage 0 innen … 1 außen, Spalten des Kartenbilds, Breite (min, max), Faktor auf die Breite.
    Gibt Ecken, Normalen, Tangenten, UV, UV2 (Lage im Haar, Zufall), Farbe (Rang-Platzhalter, Zufall, Lage, –)
    und Indizes zurück; dazu je Karte die Nummer der Mittelsträhne (für den Rang)."""
    S, P, _ = st.shape
    mitte = h.kopfmitte if mitte is None else mitte
    E, N, T, UV, UV2, F, I, MS = [], [], [], [], [], [], [], []
    for la in lagen:
        je, anteil, lage, spalten, breite, faktor = la["je"], la["anteil"], la["lage"], la["spalten"], la["breite"], la["faktor"]
        if je <= 1:
            wahl = np.sort(rng.choice(S, max(1, int(S * anteil)), replace=False))
            gruppen = [(m, np.array([m])) for m in wahl]
        else:
            # Büschel nach dem Weg der Strähnen (Wurzel, Mitte, Spitze), damit eine Karte nicht zwei Richtungen mittelt
            from kopf4_frisuren import buschel, buschel_merkmal
            nk = max(4, int(S / je * anteil))
            wahl, zu = buschel(buschel_merkmal(st), nk, rng)
            gruppen = [(m, np.nonzero(zu == b)[0]) for b, m in enumerate(wahl)]
        for m, glieder in gruppen:
            if len(glieder) == 0:
                continue
            c = st[m].copy()
            if lage == 0 and len(glieder) > 2:
                c = 0.5 * c + 0.5 * st[glieder].mean(0)
            tang = np.gradient(c, axis=0)
            tang /= np.maximum(np.linalg.norm(tang, axis=1, keepdims=True), 1e-9)
            aus = c - mitte
            aus -= tang * (aus * tang).sum(1, keepdims=True)
            aus /= np.maximum(np.linalg.norm(aus, axis=1, keepdims=True), 1e-9)
            quer = np.cross(tang, aus)
            if len(glieder) > 1:
                ab = ((st[glieder] - c[None]) * quer[None]).sum(2)
                w = np.clip(2.0 * np.sqrt((ab ** 2).mean(0)), breite[0], breite[1])
            else:
                w = np.full(P, rng.uniform(*breite))
            w = w * faktor
            t = np.linspace(0, 1, P)
            w *= (1.0 - 0.4 * t ** 2) if not bart else (1.0 - 0.3 * t)
            w = np.maximum(w, breite[0] * 0.6)
            # Strähnenkarten etwas um die Haarrichtung gedreht (nicht alle flach auf dem Kopf)
            if je <= 1:
                dreh = rng.uniform(-0.6, 0.6)
                quer = quer * np.cos(dreh) + aus * np.sin(dreh)
                aus = np.cross(quer, tang)
            spalte = int(rng.choice(spalten))
            u0 = spalte / SPALTEN
            uw = 1.0 / SPALTEN
            zufall = rng.random()
            o = len(E)
            for j in range(P):
                for k, a in enumerate((-1.0, 0.0, 1.0)):
                    pkt = c[j] + quer[j] * a * w[j] * 0.5 + aus[j] * (0.15 * w[j] * (1 - abs(a)) - 0.05 * w[j])
                    E.append(pkt)
                    N.append(aus[j])
                    T.append(np.concatenate([tang[j], [1.0]]))
                    UV.append((u0 + uw * (0.03 + 0.94 * (a + 1) / 2), t[j] * 0.995 + 0.002))
                    UV2.append((t[j], zufall))
                    F.append((0.0, zufall, lage, 0.0))
            for j in range(P - 1):
                for k in range(2):
                    a0 = o + j * 3 + k
                    a1 = a0 + 1
                    b0 = a0 + 3
                    b1 = b0 + 1
                    I.extend([a0, b0, a1, a1, b0, b1])
            MS.append((m, o, P * 3))
    return (np.array(E), np.array(N), np.array(T), np.array(UV), np.array(UV2), np.array(F), np.array(I, np.int64), MS)


# ---------------------------------------------------------------- Kartenbild
class Bild:
    def __init__(self, b, h):
        self.B, self.H = b, h
        self.a = np.zeros((h, b), np.float32)
        self.r = np.ones((h, b), np.float32)
        self.g = np.full((h, b), 0.5, np.float32)
        self.b = np.zeros((h, b), np.float32)

    def haar(self, pkt, breite, wert_r, wert_g, deckung=1.0, s0=0.0, s1=1.0):
        """pkt: Polylinie (k,2) in Bildpunkten (x, Zeile), breite (k) in Bildpunkten; s0..s1 = Lage im Haar."""
        bm = float(breite.max())
        x0, y0 = np.floor(pkt.min(0) - bm - 2).astype(int)
        x1, y1 = np.ceil(pkt.max(0) + bm + 2).astype(int)
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, self.B - 1), min(y1, self.H - 1)
        if x1 <= x0 or y1 <= y0:
            return
        X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        q = np.stack([X.ravel(), Y.ravel()], 1)
        best = np.full(len(q), np.inf)
        best_s = np.zeros(len(q))
        best_w = np.zeros(len(q))
        n = len(pkt)
        for k in range(n - 1):
            a, b = pkt[k], pkt[k + 1]
            ab = b - a
            # nur Bildpunkte nahe dem Abschnitt (schneller)
            lo = np.minimum(a, b) - bm - 2
            hi = np.maximum(a, b) + bm + 2
            sel = np.nonzero((q[:, 0] >= lo[0]) & (q[:, 0] <= hi[0]) & (q[:, 1] >= lo[1]) & (q[:, 1] <= hi[1]))[0]
            if len(sel) == 0:
                continue
            qq = q[sel]
            t = np.clip(((qq - a) @ ab) / max(ab @ ab, 1e-9), 0, 1)
            d = np.linalg.norm(qq - (a + t[:, None] * ab), axis=1)
            besser = d < best[sel]
            ss = sel[besser]
            best[ss] = d[besser]
            best_s[ss] = (k + t[besser]) / (n - 1)
            best_w[ss] = breite[k] * (1 - t[besser]) + breite[k + 1] * t[besser]
        w = np.maximum(best_w, 1.0)
        dk = np.clip(w / 2 - best + 0.5, 0, 1) * np.minimum(best_w, 1.0) * deckung
        dk = dk.reshape(X.shape)
        s = (s0 + (s1 - s0) * best_s).reshape(X.shape)
        ys, xs = slice(y0, y1 + 1), slice(x0, x1 + 1)
        neu = dk > 0.25
        self.r[ys, xs] = np.where(neu, wert_r, self.r[ys, xs])
        self.g[ys, xs] = np.where(neu, wert_g, self.g[ys, xs])
        self.b[ys, xs] = np.where(neu, s, self.b[ys, xs])
        # Deckung sammeln (überlagerte Haare decken mehr)
        self.a[ys, xs] = 1 - (1 - self.a[ys, xs]) * (1 - dk)

    def speichern(self, pfad):
        import OpenImageIO as oiio
        r, g, b = self.r.copy(), self.g.copy(), self.b.copy()
        gueltig = self.a > 0.02
        for _ in range(8):
            for feld in (r, g, b):
                summe = np.zeros_like(feld)
                zahl = np.zeros_like(feld)
                for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                    summe += np.roll(np.roll(np.where(gueltig, feld, 0), dy, 0), dx, 1)
                    zahl += np.roll(np.roll(gueltig.astype(np.float32), dy, 0), dx, 1)
                feld[:] = np.where(gueltig, feld, np.where(zahl > 0, summe / np.maximum(zahl, 1), feld))
            gueltig = gueltig | (np.roll(gueltig, 1, 0) | np.roll(gueltig, -1, 0) | np.roll(gueltig, 1, 1) | np.roll(gueltig, -1, 1))
        feld = np.stack([r, g, b, np.clip(self.a, 0, 1)], -1).astype(np.float32)
        spec = oiio.ImageSpec(self.B, self.H, 4, oiio.FLOAT)
        spec.attribute("oiio:UnassociatedAlpha", 1)
        buf = oiio.ImageBuf(spec)
        buf.set_pixels(oiio.ROI(0, self.B, 0, self.H, 0, 1, 0, 4), feld)
        buf.write(pfad, "uint8")


def kartenbild(pfad, rng):
    """Haare je Spalte zeichnen. Breite der Spalte = Breite der Karte (etwa 1–2 cm), Höhe = ganze Haarlänge."""
    bild = Bild(ATLAS, ATLAS)
    sw = ATLAS // SPALTEN
    sorten = [  # (Haare, Dicke px, Wellen px, Länge min, Rand weich, kraus)
        (150, 2.2, 3.0, 0.75, 0.18, 0.0), (120, 2.0, 5.0, 0.7, 0.22, 0.0),
        (170, 2.4, 2.0, 0.8, 0.15, 0.0), (110, 1.9, 7.0, 0.65, 0.25, 0.0),
        (55, 1.8, 6.0, 0.6, 0.3, 0.0), (40, 1.7, 9.0, 0.55, 0.32, 0.0),
        (110, 2.4, 10.0, 0.55, 0.25, 1.0), (80, 2.2, 14.0, 0.5, 0.3, 1.0)]
    for sp, (zahl, dicke, welle, lmin, weich, kraus) in enumerate(sorten):
        x0 = sp * sw
        haare = []
        for _ in range(zahl):
            # Wurzeln über die Breite verteilt, am Rand seltener (weiche Kartenkante)
            while True:
                u = rng.random()
                if rng.random() < glatt(min(u, 1 - u), 0.0, weich):
                    break
            haare.append((u, rng.random()))
        haare.sort(key=lambda h: -h[1])
        for u, r in haare:
            k = 24
            # Wurzeln nicht auf einer Linie (sonst harte Kante an der Kartenwurzel), Spitzen verstreut
            start = rng.uniform(0.0, 0.16) ** 1.3
            laenge = rng.uniform(lmin, 1.0) * (1.0 - start)
            s = np.linspace(start, min(start + laenge, 0.995), k)
            phase = rng.random() * 2 * np.pi
            wl = rng.uniform(0.18, 0.35)
            seit = rng.normal(0, 0.06)               # leichte Schräge
            x = (u + seit * (s - start) * 0.25) * (sw - 8) + 4 + welle * np.sin(2 * np.pi * s / wl + phase) * (s - start)
            if kraus:
                x = x + np.cumsum(rng.normal(0, kraus * 2.2, k))
                x = x - (x[0] - (u * (sw - 8) + 4))
            x = np.clip(x, 2, sw - 3) + x0
            y = s * (ATLAS - 1)
            pkt = np.stack([x, y], 1)
            br = np.minimum(1.0, np.linspace(0.0, 1.0, k) * 8) ** 0.5 * np.linspace(1.0, 0.2, k) ** 0.7 * dicke * rng.uniform(0.75, 1.2)
            bild.haar(pkt, np.maximum(br, 0.2), r, rng.uniform(0.3, 0.7), deckung=rng.uniform(0.6, 0.95), s0=0.0, s1=1.0)
    bild.speichern(pfad)


# ---------------------------------------------------------------- Kopfhautkarten
def uv_rastern(kopf, G):
    tid = np.full((G, G), -1, np.int32)
    bc = np.zeros((G, G, 3), np.float32)
    for k in range(len(kopf.tri_uv)):
        q = kopf.tri_uv[k] * G
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
        innen = (w0 >= -1e-3) & (w1 >= -1e-3) & (w2 >= -1e-3)
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
    return J, I, T_, W


def dichte_karte(punkte_bild, wurzeln_, sigma, abstand0):
    """Deckung aus Wurzeldichte: 1 - exp(-Σ Gauß / Erwartung) über einen KD-Baum."""
    from mathutils import Vector
    from mathutils.kdtree import KDTree
    aus = np.zeros(len(punkte_bild))
    if len(wurzeln_) == 0:
        return aus
    baum = KDTree(len(wurzeln_))
    for i, p in enumerate(wurzeln_):
        baum.insert(Vector(p), i)
    baum.balance()
    erwartet = 2 * math.pi * sigma ** 2 / abstand0 ** 2
    for j, q in enumerate(punkte_bild):
        s = 0.0
        for _, _, d in baum.find_range(Vector(q), 3 * sigma):
            s += math.exp(-d * d / (2 * sigma * sigma))
        aus[j] = s
    return 1.0 - np.exp(-2.2 * aus / max(erwartet, 1e-6))


def karte_speichern(pfad, G, J, I, kanaele, unschaerfe=1):
    import OpenImageIO as oiio
    feld = np.zeros((G, G, 4), np.float32)
    for k, c in enumerate(kanaele):
        if c is not None:
            feld[J, I, k] = c
    # Ränder der UV-Inseln nach außen ausdehnen (Mipmaps), dann leicht glätten
    gueltig = np.zeros((G, G), bool)
    gueltig[J, I] = True
    for _ in range(6):
        summe = np.zeros_like(feld)
        zahl = np.zeros((G, G, 1), np.float32)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            summe += np.roll(np.roll(np.where(gueltig[..., None], feld, 0), dy, 0), dx, 1)
            zahl += np.roll(np.roll(gueltig.astype(np.float32)[..., None], dy, 0), dx, 1)
        feld = np.where(gueltig[..., None], feld, np.where(zahl > 0, summe / np.maximum(zahl, 1), feld))
        gueltig = gueltig | (zahl[..., 0] > 0)
    for _ in range(unschaerfe):
        feld = (feld * 4 + np.roll(feld, 1, 0) + np.roll(feld, -1, 0) + np.roll(feld, 1, 1) + np.roll(feld, -1, 1)) / 8
    feld = np.ascontiguousarray(feld[::-1])          # wie kopf3_backen.py: Zeile 0 = oben (v = 1)
    spec = oiio.ImageSpec(G, G, 4, oiio.FLOAT)
    spec.attribute("oiio:UnassociatedAlpha", 1)
    buf = oiio.ImageBuf(spec)
    buf.set_pixels(oiio.ROI(0, G, 0, G, 0, 1, 0, 4), np.clip(feld, 0, 1))
    buf.write(pfad, "uint8")
