"""Charakter Phase 3: Bilder für Brauen und Wimpern Haar für Haar zeichnen (eigener Code, MIT; nur numpy und
OpenImageIO, läuft mit Blenders Python: kopf3.sh ruft es auf).

Aufruf:  <blender-python> -I kopf3_haartexturen.py <ausgabeordner>
Ergebnis: brauen.png (1024 × 512), wimpern.png (1024 × 1024, vier Bänder: Unterlid, Oberlid Haupt-, lange und
Fülllage; Phase 4), RGBA, linear:
  R = Zufallswert des Haars (der Shader zeigt nur Haare mit R < Dichte),
  G = Abstand der Wurzel von der Mittellinie der Braue (0 Mitte, 1 Rand; der Shader schneidet nach „Breite“),
  B = Lage entlang des Haars (0 Wurzel, 1 Spitze), A = Deckung.
Kartenlage (kopf3_haare.py): Brauen u = innen → außen, v unten → oben, untere Lage v 0..0,5, obere 0,5..1;
Wimpern u = innen → außen, v Wurzel → Spitze je Band (v = (Band + v) / 4). Bildzeile 0 = oben (v = 1).
Phase 4: Brauen dünner und lichter (Haut scheint durch), unregelmäßige Dichte (Büschel, Lücken), einzelne
abstehende Haare am Rand; Wimpern in Büscheln, an der Wurzel kräftig, zur Spitze fein, geschwungen.
Keine Fremddaten: alles entsteht aus Zufallszahlen mit festem Startwert.
"""
import math
import os
import sys

import numpy as np

B, H = 1024, 512                      # Bildgröße; je Lage bzw. Lid 1024 × 256
# Maße der Karten (wie kopf3_haare.py): Braue etwa 64 mm lang, Höhe innen 16,5 mm, außen 11,5 mm, Merkpunkte bei v 0,8
BRAUE_LAENGE_MM = 64.0
BRAUE_HOEHE_MM = (16.5, 11.5)
BRAUE_V_MERK = 0.80


def braue_hoehe_mm(u):
    return BRAUE_HOEHE_MM[0] + (BRAUE_HOEHE_MM[1] - BRAUE_HOEHE_MM[0]) * np.asarray(u)


def braue_kanten_mm(u):
    """Oberkante und Unterkante der Braue (mm über der Unterkante der Karte) an der Stelle u."""
    u = np.asarray(u, dtype=np.float64)
    h = braue_hoehe_mm(u)
    oben = BRAUE_V_MERK * h + 0.4 - 0.9 * np.clip((u - 0.75) / 0.25, 0, 1) ** 1.5
    dicke = np.interp(u, [0.0, 0.05, 0.2, 0.6, 0.85, 1.0], [5.0, 8.6, 8.0, 6.2, 3.8, 1.6])
    return oben, oben - dicke


def brauen_form(u, v):
    """Dichte (0..1) der Haarwurzeln an (u, v) in einer Lage (v 0..1) und Abstand zur Mittellinie (0 Mitte, 1 Rand)."""
    u = np.asarray(u, dtype=np.float64)
    y = np.asarray(v, dtype=np.float64) * braue_hoehe_mm(u)
    oben, unten = braue_kanten_mm(u)
    mitte, halb = (oben + unten) / 2, (oben - unten) / 2
    rand = np.abs(y - mitte) / np.maximum(halb, 0.3)
    innen = np.clip((1.25 - rand) / 0.5, 0, 1) ** 1.5
    laengs = np.clip((u - 0.015) / 0.05, 0, 1) * np.clip((0.995 - u) / 0.04, 0, 1)
    return innen * laengs * (1.0 - 0.35 * np.clip((u - 0.7) / 0.3, 0, 1)), np.clip(rand, 0, 1)


class Bild:
    def __init__(self, b=None, h=None):
        self.B, self.H = b or B, h or H
        self.a = np.zeros((self.H, self.B), np.float32)
        self.r = np.ones((self.H, self.B), np.float32)
        self.g = np.zeros((self.H, self.B), np.float32)
        self.b = np.zeros((self.H, self.B), np.float32)

    def haar(self, pkt, breite, wert_r, wert_g, deckung=1.0):
        """pkt: Polylinie (k,2) in Bildpunkten (x, y; y = Zeile von oben), breite (k) in Bildpunkten."""
        x0, y0 = np.floor(pkt.min(0) - breite.max() - 2).astype(int)
        x1, y1 = np.ceil(pkt.max(0) + breite.max() + 2).astype(int)
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
            t = np.clip(((q - a) @ ab) / max(ab @ ab, 1e-9), 0, 1)
            d = np.linalg.norm(q - (a + t[:, None] * ab), axis=1)
            besser = d < best
            best[besser] = d[besser]
            best_s[besser] = (k + t[besser]) / (n - 1)
            best_w[besser] = breite[k] * (1 - t[besser]) + breite[k + 1] * t[besser]
        # Deckung mit Kantenglättung; dünner als ein Bildpunkt: blasser statt dünner
        w = np.maximum(best_w, 1.0)
        dk = np.clip(w / 2 - best + 0.5, 0, 1) * np.minimum(best_w, 1.0) * deckung
        dk = dk.reshape(X.shape)
        s = best_s.reshape(X.shape)
        ys, xs = slice(y0, y1 + 1), slice(x0, x1 + 1)
        neu = dk > 0.2
        self.r[ys, xs] = np.where(neu, wert_r, self.r[ys, xs])
        self.g[ys, xs] = np.where(neu, wert_g, self.g[ys, xs])
        self.b[ys, xs] = np.where(neu, s, self.b[ys, xs])
        # überlagerte Haare decken mehr (statt nur des Maximums)
        self.a[ys, xs] = 1 - (1 - self.a[ys, xs]) * (1 - dk)

    def speichern(self, pfad):
        import OpenImageIO as oiio
        # Ränder der Haare: Farbkanäle nach außen ausdehnen (sonst ziehen Mipmaps den Hintergrund herein)
        r, g, b = self.r.copy(), self.g.copy(), self.b.copy()
        gueltig = self.a > 0.02
        for _ in range(6):
            for feld in (r, g, b):
                summe = np.zeros_like(feld)
                zahl = np.zeros_like(feld)
                for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                    summe += np.roll(np.roll(np.where(gueltig, feld, 0), dy, 0), dx, 1)
                    zahl += np.roll(np.roll(gueltig.astype(np.float32), dy, 0), dx, 1)
                feld[:] = np.where(gueltig, feld, np.where(zahl > 0, summe / np.maximum(zahl, 1), feld))
            gueltig = gueltig | (np.roll(gueltig, 1, 0) | np.roll(gueltig, -1, 0) | np.roll(gueltig, 1, 1) | np.roll(gueltig, -1, 1))
        feld = np.stack([r, g, b, self.a], -1).astype(np.float32)
        spec = oiio.ImageSpec(self.B, self.H, 4, oiio.FLOAT)
        spec.attribute("oiio:UnassociatedAlpha", 1)      # R, G, B sind Daten, nicht mit A vormultipliziert
        buf = oiio.ImageBuf(spec)
        buf.set_pixels(oiio.ROI(0, self.B, 0, self.H, 0, 1, 0, 4), feld)
        buf.write(pfad, "uint8")


def _zeile(v, lage):
    """v (0..1 in der Lage/Lid) und Lage 0/1 -> Bildzeile (von oben)."""
    vb = 0.5 * lage + 0.5 * np.asarray(v)
    return (1.0 - vb) * H


def _wolke(rng, n=6):
    """Langsames Rauschen über (u, v): Büschel und Lücken der Braue."""
    k = rng.normal(0, 1, (n, 2)) * np.array([9.0, 4.0])
    ph = rng.random(n) * 2 * np.pi
    def f(u, v):
        return sum(np.sin(k[i, 0] * u + k[i, 1] * v + ph[i]) for i in range(n)) / math.sqrt(n)
    return f


def brauen(pfad, rng):
    bild = Bild()
    wolke = _wolke(rng)
    for lage, anzahl in ((0, 820), (1, 230)):
        haare = []
        while len(haare) < anzahl:
            u, v = rng.random(), rng.random() * 1.12 - 0.06
            dichte, rand = brauen_form(u, np.clip(v, 0, 1))
            # unregelmäßig: Büschel und Lücken; am Rand vereinzelt abstehende Haare
            dichte = dichte * np.clip(0.75 + 0.45 * wolke(u, v), 0.25, 1.2)
            if dichte < 0.05 and rng.random() < 0.05:
                # einzelne abstehende Haare knapp (bis 1,2 mm) außerhalb des Randes
                ob, un = braue_kanten_mm(u)
                y_mm = v * float(braue_hoehe_mm(u))
                if un - 1.2 < y_mm < ob + 1.2 and 0.08 < u < 0.95:
                    dichte = 0.5
            if rng.random() > dichte or not (0.0 < v < 1.0):
                continue
            haare.append((u, v, float(rand), rng.random()))
        haare.sort(key=lambda h: -h[3])                  # hohe Zufallswerte zuerst, niedrige liegen oben
        for u, v, rand, r in haare:
            h_mm = float(braue_hoehe_mm(u))
            y = v * h_mm
            oben, unten = braue_kanten_mm(u)
            obenteil = (y - (oben + unten) / 2) / max((oben - unten) / 2, 0.3)
            # Wuchsrichtung (Grad, 0 = nach außen, 90 = nach oben): innen steil, im Bogen flach, außen leicht abwärts;
            # die oberen Haare liegen flacher als die unteren (Fischgrät)
            steil = np.interp(u, [0.0, 0.1, 0.3, 0.7, 1.0], [70, 58, 26, 14, -10])
            w = math.radians(steil - 20 * max(obenteil, 0) * min(u / 0.3, 1) + rng.normal(0, 8))
            laenge = np.interp(u, [0, 0.15, 0.6, 1], [3.6, 5.0, 6.0, 4.6]) * rng.uniform(0.75, 1.15)
            if lage == 1:
                laenge *= 1.1
            krumm = rng.normal(-0.03, 0.025)               # Biegung je mm (legt sich an)
            k = 9
            x_mm, y_mm = u * BRAUE_LAENGE_MM, y
            pkt = []
            for i in range(k):
                pkt.append((x_mm, y_mm))
                x_mm += math.cos(w) * laenge / (k - 1)
                y_mm += math.sin(w) * laenge / (k - 1)
                w += krumm * laenge / (k - 1)
            pkt = np.array(pkt)
            uu = pkt[:, 0] / BRAUE_LAENGE_MM
            vv = pkt[:, 1] / braue_hoehe_mm(np.clip(uu, 0, 1))
            # Haare nicht über die Oberkante der Braue (+1 mm) hinaus und nie aus der Karte: dort abschneiden
            ob, _ = braue_kanten_mm(np.clip(uu, 0, 1))
            gut = (uu > 0.002) & (uu < 0.998) & (vv > 0.005) & (vv < 0.99) & (pkt[:, 1] < ob + 1.0)
            bis = int(np.argmin(gut)) if not gut.all() else k
            if bis < 3:
                continue
            pkt, uu, vv = pkt[:bis], uu[:bis], vv[:bis]
            px = np.stack([uu * B, _zeile(vv, lage)], 1)
            dicke_mm = rng.uniform(0.045, 0.08)
            breite = np.linspace(1.0, 0.25, k)[:bis] ** 0.8 * dicke_mm / (BRAUE_LAENGE_MM / B)
            bild.haar(px, breite, r, rand, deckung=rng.uniform(0.55, 0.95))
    bild.speichern(pfad)
    return bild


def wimpern(pfad, rng):
    """Vier Bänder (je 1024 × 256): 0 Unterlid, 1 Oberlid Hauptlage, 2 Oberlid lang und locker, 3 Oberlid kurz (Fülle).
    Wimpern in Büscheln (Spitzen laufen zusammen), an der Wurzel kräftig, zur Spitze fein, leicht geschwungen."""
    BH = 1024
    bild = Bild(B, BH)
    # (Band, Anzahl, Länge mm innen/Mitte/außen, Karte mm, Dicke mm, Büschel, Schwung)
    baender = ((0, 46, (2.0, 3.8, 4.0), (2.6, 4.2, 4.3), 0.045, (1, 2), 0.3),
               (1, 120, (5.4, 8.4, 7.6), (5.8, 8.8, 8.2), 0.09, (2, 4), 0.6),
               (2, 46, (6.0, 9.4, 8.8), (6.5, 9.9, 9.2), 0.075, (1, 3), 0.9),
               (3, 90, (2.6, 4.4, 4.0), (5.8, 8.8, 8.2), 0.06, (1, 2), 0.3))
    for band, anzahl, laenge_mm, karte_mm, dicke, (b_min, b_max), schwung in baender:
        lid_mm = 30.0 if band else 28.0
        haare = []
        buschel = 0
        while len(haare) < anzahl:
            um = rng.uniform(0.04, 0.99)
            if band == 0:
                dichte = np.interp(um, [0.04, 0.3, 0.6, 0.95, 0.99], [0.15, 0.4, 0.9, 1.0, 0.5])
            else:
                dichte = np.interp(um, [0.04, 0.2, 0.5, 0.9, 0.99], [0.3, 0.85, 1.0, 0.95, 0.5])
            if rng.random() > dichte:
                continue
            n = int(rng.integers(b_min, b_max + 1))
            spitze_du = rng.normal(0.02 + 0.07 * um, 0.02)
            zusammen = rng.uniform(0.5, 0.9)              # Spitzen laufen im Büschel zusammen
            for j in range(n):
                haare.append((um + rng.normal(0, 0.007), spitze_du, rng.random(), um, zusammen))
            buschel += 1
        haare.sort(key=lambda h: -h[2])
        for u, spitze_du, r, um, zusammen in haare:
            u = float(np.clip(u, 0.01, 0.995))
            karte = float(np.interp(u, [0, 0.55, 1], karte_mm))
            lang = float(np.interp(u, [0, 0.55, 1], laenge_mm)) * rng.uniform(0.72, 1.06)
            s_ende = min(lang / karte, 0.985)
            k = 10
            s = np.linspace(0.02, s_ende, k)
            f = (s / max(s_ende, 1e-3))
            # Wurzel am eigenen Ort, Spitze zum Büschel hin; leichter S-Schwung quer
            uu = u * (1 - zusammen * f ** 1.5) + um * zusammen * f ** 1.5
            uu = uu + spitze_du * f ** 1.4 * (lang / lid_mm) * 3.0 + schwung * 0.004 * np.sin(np.pi * f) * rng.normal(0, 1)
            vb = (band + np.clip(s * 0.995, 0, 0.995)) / 4.0
            px = np.stack([np.clip(uu, 0, 1) * B, (1.0 - vb) * BH], 1)
            breite = np.linspace(1.0, 0.12, k) ** 0.7 * dicke * rng.uniform(0.75, 1.2) / (lid_mm / B)
            bild.haar(px, breite, r, 0.0, deckung=rng.uniform(0.8, 1.0))
    bild.speichern(pfad)
    return bild


if __name__ == "__main__":
    aus = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(aus, exist_ok=True)
    rng = np.random.default_rng(1480)
    brauen(os.path.join(aus, "brauen.png"), rng)
    wimpern(os.path.join(aus, "wimpern.png"), rng)
    print("FERTIG Haarbilder", aus)
