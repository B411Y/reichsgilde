"""Charakter Phase 3: Bilder für Brauen und Wimpern Haar für Haar zeichnen (eigener Code, MIT; nur numpy und
OpenImageIO, läuft mit Blenders Python: kopf3.sh ruft es auf).

Aufruf:  <blender-python> -I kopf3_haartexturen.py <ausgabeordner>
Ergebnis: brauen.png (1024 × 512), wimpern.png (1024 × 1024, vier Bänder: Unterlid, Oberlid in drei Reihen;
Phase 5), RGBA, linear:
  R = Zufallswert des Haars (der Shader zeigt nur Haare mit R < Dichte),
  G = Abstand der Wurzel von der Mittellinie der Braue (0 Mitte, 1 Rand; der Shader schneidet nach „Breite“),
  B = Lage entlang des Haars (0 Wurzel, 1 Spitze), A = Deckung.
Kartenlage (kopf3_haare.py): Brauen u = innen → außen, v unten → oben, untere Lage v 0..0,5, obere 0,5..1;
Wimpern u = innen → außen, v Wurzel → Spitze je Band (v = (Band + v) / 4). Bildzeile 0 = oben (v = 1).
Phase 4: Brauen dünner und lichter (Haut scheint durch), unregelmäßige Dichte (Büschel, Lücken), einzelne
abstehende Haare am Rand; Wimpern in Büscheln, an der Wurzel kräftig, zur Spitze fein, geschwungen.
Keine Fremddaten: alles entsteht aus Zufallszahlen mit festem Startwert.

Phase 6 (Etappe B, S7): sechs Brauenformen je Geschlecht als brauen6_<m|w>_<0..5>.png (Formvorbild: MakeHuman
eyebrow001–012, CC0, nur angesehen): 0 gerade, 1 Bogen, 2 Knick, 3 kräftiger Kopf, 4 dünn auslaufend, 5 buschig. Die
Darstellung legt sie als Texture2DArray an und wählt die Form aus dem Gen „brauen_form“.
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


FRAU = False                          # Phase 5: brauen_w.png – schmaler, mit Bogen, dünnes Ende
FORM = -1                             # Phase 6: Brauenform 0–5 (−1 = Form der Phase 5)
FORMEN6 = ["gerade", "Bogen", "Knick", "kräftiger Kopf", "dünn auslaufend", "buschig"]


def braue_kanten_mm(u, frau=None):
    """Oberkante und Unterkante der Braue (mm über der Unterkante der Karte) an der Stelle u. Frauen (Phase 5, nach
    Fotos): Kopf 7 mm, Körper 5–6 mm, das Ende läuft auf 1 mm aus; der Bogen hebt die Braue bei etwa 60 % um 1,3 mm
    (Oberkante) bzw. 2 mm (Unterkante). Männer: Kopf bis 9 mm, gerader, das Ende kräftiger."""
    frau = FRAU if frau is None else frau
    u = np.asarray(u, dtype=np.float64)
    h = braue_hoehe_mm(u)
    if FORM >= 0:
        return braue_kanten6(u, h, frau)
    oben = BRAUE_V_MERK * h + 0.4 - 0.9 * np.clip((u - 0.75) / 0.25, 0, 1) ** 1.5
    if frau:
        bogen = np.exp(-((u - 0.6) / 0.22) ** 2)
        oben = oben + 1.3 * bogen - 0.8 * np.clip((u - 0.8) / 0.2, 0, 1)
        dicke = np.interp(u, [0.0, 0.05, 0.2, 0.55, 0.8, 1.0], [4.2, 7.0, 6.4, 5.0, 3.0, 1.0])
        return oben, oben - dicke
    dicke = np.interp(u, [0.0, 0.05, 0.2, 0.6, 0.85, 1.0], [5.5, 9.2, 8.6, 6.8, 4.4, 2.0])
    return oben, oben - dicke


def braue_kanten6(u, h, frau):
    """Phase 6: Ober- und Unterkante der sechs Brauenformen (mm über der Unterkante der Karte). Frauen schmaler (× 0,75)
    und mit etwas mehr Bogen. Die Unterkante liegt bei allen Formen innen gleich (die Lage der Braue steuert das Gen
    „brauen_stand“ über die Form der Haut)."""
    grund = BRAUE_V_MERK * h + 0.4
    f = 0.75 if frau else 1.0
    bogen_w = 0.5 if frau else 0.0
    if FORM == 0:      # gerade: kaum Bogen, gleichmäßig
        oben = grund - 0.5 * np.clip((u - 0.8) / 0.2, 0, 1) + bogen_w * np.exp(-((u - 0.6) / 0.25) ** 2)
        dicke = np.interp(u, [0.0, 0.05, 0.3, 0.8, 1.0], [5.0, 7.0, 6.6, 5.2, 2.6])
    elif FORM == 1:    # Bogen: weich gewölbt, höchste Stelle bei 55 %
        oben = grund + (2.0 + bogen_w) * np.exp(-((u - 0.55) / 0.3) ** 2) - 1.2 * np.clip((u - 0.8) / 0.2, 0, 1)
        dicke = np.interp(u, [0.0, 0.05, 0.3, 0.7, 1.0], [4.6, 6.6, 6.0, 4.4, 1.6])
    elif FORM == 2:    # Knick: gerader Anstieg bis 65 %, dann abfallend (Winkel)
        spitze = np.where(u < 0.65, u / 0.65, 1.0 - (u - 0.65) / 0.35 * 1.4)
        oben = grund - 0.6 + (2.6 + bogen_w) * np.clip(spitze, -0.4, 1.0)
        dicke = np.interp(u, [0.0, 0.05, 0.4, 0.7, 1.0], [5.0, 7.0, 6.2, 4.6, 1.8])
    elif FORM == 3:    # kräftiger Kopf: breiter Kopf, schnell schmaler werdend
        oben = grund + 0.6 - 0.9 * np.clip((u - 0.7) / 0.3, 0, 1) + bogen_w * np.exp(-((u - 0.5) / 0.25) ** 2)
        dicke = np.interp(u, [0.0, 0.04, 0.2, 0.5, 0.8, 1.0], [8.0, 11.0, 9.6, 6.0, 3.4, 1.6])
    elif FORM == 4:    # dünn auslaufend: schmal, langes feines Ende
        oben = grund - 0.3 + (1.0 + bogen_w) * np.exp(-((u - 0.5) / 0.3) ** 2) - 1.0 * np.clip((u - 0.7) / 0.3, 0, 1)
        dicke = np.interp(u, [0.0, 0.05, 0.25, 0.55, 0.8, 1.0], [3.6, 5.0, 4.4, 3.0, 1.6, 0.8])
    else:              # buschig: breit überall, unregelmäßiger Rand
        oben = grund + 0.9 + bogen_w * np.exp(-((u - 0.55) / 0.3) ** 2) - 0.6 * np.clip((u - 0.85) / 0.15, 0, 1)
        dicke = np.interp(u, [0.0, 0.05, 0.3, 0.8, 1.0], [7.2, 9.6, 9.2, 7.0, 4.0])
    dicke = dicke * f
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
    # zur Mitte (Kopf und Körper) dichter, zum Ende lichter (Fotos)
    return innen * laengs * (1.0 - 0.4 * np.clip((u - 0.65) / 0.35, 0, 1)), np.clip(rand, 0, 1)


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


def brauen(pfad, rng, frau=False):
    """Phase 5: dichter und kräftiger als Phase 4 (Frauen hatten „fast keine Brauen“); Frauen eigenes Bild (Form oben)."""
    global FRAU
    FRAU = frau
    bild = Bild()
    wolke = _wolke(rng)
    mehr = {5: 1.45, 3: 1.25, 4: 0.7}.get(FORM, 1.0)
    for lage, anzahl in ((0, int(880 * mehr)), (1, int(260 * mehr))) if not frau else ((0, int(720 * mehr)), (1, int(170 * mehr))):
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
            # Wuchsrichtung (Grad, 0 = nach außen, 90 = nach oben): innen steil aufwärts, im Körper laufen untere
            # (aufwärts) und obere Haare (abwärts) zur Mittellinie zusammen (Fischgrät), das Ende liegt flach abwärts
            steil = np.interp(u, [0.0, 0.1, 0.3, 0.7, 1.0], [75, 60, 24, 10, -12])
            fisch = np.interp(u, [0.0, 0.12, 0.3, 0.8, 1.0], [0.0, 10.0, 22.0, 18.0, 6.0])
            w = math.radians(steil - fisch * obenteil + rng.normal(0, 7))
            laenge = np.interp(u, [0, 0.15, 0.6, 1], [3.6, 5.0, 6.0, 4.6]) * rng.uniform(0.75, 1.15) * (1.3 if FORM == 5 else 1.0)
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
            dicke_mm = rng.uniform(0.055, 0.095) if not frau else rng.uniform(0.045, 0.075)
            breite = np.linspace(1.0, 0.25, k)[:bis] ** 0.8 * dicke_mm / (BRAUE_LAENGE_MM / B)
            bild.haar(px, breite, r, rand, deckung=rng.uniform(0.62, 0.98))
    bild.speichern(pfad)
    return bild


def wimpern(pfad, rng):
    """Vier Bänder (je 1024 × 256): 0 Unterlid, 1–3 Oberlid in drei Reihen (vorn lang, dahinter kürzer und lichter);
    Phase 5. Wimpern einzeln und in kleinen Büscheln (Spitzen laufen zusammen), an der Wurzel kräftig und dicht, zur
    Spitze fein; die Länge je Wimper streut unter der Kartenlänge (kopf3_haare.wimper_laenge), ein paar kurze dazwischen."""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import kopf3_haare as kh
    BH = 1024
    bild = Bild(B, BH)
    # (Band, Oberlid?, Anzahl, Längenfaktor der Karte, Dicke an der Wurzel mm, Büschel, Schwung)
    baender = ((0, False, 55, 1.0, 0.075, (1, 2), 0.3),
               (1, True, 120, 1.0, 0.115, (1, 3), 0.6),
               (2, True, 70, 0.84, 0.1, (1, 2), 0.5),
               (3, True, 45, 0.62, 0.085, (1, 2), 0.3))
    for band, oben, anzahl, l_f, dicke, (b_min, b_max), schwung in baender:
        lid_mm = 30.0 if oben else 27.0
        haare = []
        while len(haare) < anzahl:
            um = rng.uniform(0.0, 1.0)
            if oben:
                dichte = np.interp(um, [0.0, 0.12, 0.4, 0.85, 1.0], [0.25, 0.75, 1.0, 1.0, 0.55])
            else:
                dichte = np.interp(um, [0.0, 0.3, 0.6, 0.95, 1.0], [0.1, 0.35, 0.9, 1.0, 0.5])
            if rng.random() > dichte:
                continue
            n = int(rng.integers(b_min, b_max + 1))
            spitze_du = rng.normal(0.015 + 0.05 * um, 0.015)
            zusammen = rng.uniform(0.4, 0.85)             # Spitzen laufen im Büschel zusammen
            for j_ in range(n):
                haare.append((um + rng.normal(0, 0.006), spitze_du, rng.random(), um, zusammen))
        haare.sort(key=lambda h: -h[2])
        for u, spitze_du, r, um, zusammen in haare:
            u = float(np.clip(u, 0.005, 0.995))
            # Länge relativ zur Karte: meist 75–100 %, jede sechste kurz (nachwachsend)
            anteil = rng.uniform(0.75, 1.0) if rng.random() > 0.16 else rng.uniform(0.35, 0.65)
            s_ende = min(anteil, 0.985)
            k = 12
            s_ = np.linspace(0.0, s_ende, k)
            f = s_ / max(s_ende, 1e-3)
            lang_mm = float(kh.wimper_laenge(u, oben)) * 1000 * l_f * s_ende
            uu = u * (1 - zusammen * f ** 1.6) + um * zusammen * f ** 1.6
            uu = uu + spitze_du * f ** 1.4 * (lang_mm / lid_mm) * 2.5 + schwung * 0.003 * np.sin(np.pi * f) * rng.normal(0, 1)
            vb = (band + np.clip(s_ * 0.995, 0, 0.995)) / 4.0
            px = np.stack([np.clip(uu, 0, 1) * B, (1.0 - vb) * BH], 1)
            # kräftige Wurzel, fein auslaufende Spitze
            breite = (1.0 - 0.85 * f ** 1.2) * dicke * rng.uniform(0.8, 1.2) / (lid_mm / B)
            bild.haar(px, breite, r, 0.0, deckung=rng.uniform(0.92, 1.0))
    bild.speichern(pfad)
    return bild


if __name__ == "__main__":
    aus = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(aus, exist_ok=True)
    rng = np.random.default_rng(1480)
    brauen(os.path.join(aus, "brauen.png"), rng)
    brauen(os.path.join(aus, "brauen_w.png"), rng, frau=True)
    wimpern(os.path.join(aus, "wimpern.png"), rng)
    # Phase 6: sechs Brauenformen je Geschlecht (eigener Startwert, damit die Bilder der Phase 5 gleich bleiben)
    rng6 = np.random.default_rng(1506)
    for wer in ("m", "w"):
        for f in range(6):
            FORM = f
            brauen(os.path.join(aus, f"brauen6_{wer}_{f}.png"), rng6, frau=(wer == "w"))
    FORM = -1
    print("FERTIG Haarbilder", aus)
