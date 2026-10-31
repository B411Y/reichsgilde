"""Zonenkarte der Haut für Weg A des Probekopfs „Halt B2“ (Charakter Phase 6; eigener Code, MIT; numpy und
OpenImageIO, Blenders Python).

Diagnose: Der Albedo-Inhalt der Köpfe ist gleichförmig („Puppe“). Echte Gesichter haben Zonen: Rötung auf Wangen,
Nase, Ohren und Kinn, dunklere Haut unter den Augen, einen feuchten Lidrand mit Karunkel im inneren Augenwinkel, eine
matte (nicht glänzende) Nasenwurzel. Die Karte entsteht aus den 68 ICT-Merkpunkten am Grundnetz der .glb (Lage in mm,
Godot-Achsen: x links der Figur, y oben, z vorn) und wird in die UV-Karte des Netzes gemalt (Dreiecke je Bildpunkt
baryzentrisch gefüllt, Ränder 6 Bildpunkte gedehnt gegen Nähte).

Kanäle (linear, 0–1):  R Rötung (Wangen, Nasenspitze und -flügel, Ohren, Kinn, Karunkel)
                       G Schatten unter dem Auge, Oberlid etwas
                       B Feuchte (Lidrand unten stark, oben schwächer; Karunkel)
                       A Nasenrücken (Glanz senken)
Der Hautshader (haut6a.gdshader, #define WEG_A) liest sie als „zonen_tex“.

Aufruf: $BPY -I tools/charakter/kopf6_zonen.py <kopf6.glb> <ausgabeordner> [groesse=1024]
"""
import os
import sys

import numpy as np
import OpenImageIO as oiio

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from messen.glb import Glb  # noqa: E402
from messen.merkpunkte import Kopfnetz  # noqa: E402


def gauss(H, c, s):
    """Gaußglocke um c mit Halbachsen s = (sx, sy, sz) in mm."""
    d = (H - c) / np.asarray(s, float)
    return np.exp(-0.5 * (d ** 2).sum(1))


def abstand_linie(H, punkte):
    """Kleinster Abstand jeder Ecke zu einer Polylinie (mm)."""
    best = np.full(len(H), np.inf)
    for a, b in zip(punkte[:-1], punkte[1:]):
        ab = b - a
        t = np.clip(((H - a) @ ab) / max(ab @ ab, 1e-9), 0.0, 1.0)
        best = np.minimum(best, np.linalg.norm(H - (a + np.outer(t, ab)), axis=1))
    return best


def zonen_ecken(H, N, P):
    """Werte je Ecke (n × 4)."""
    z = np.zeros((len(H), 4))
    vorn = np.clip((N[:, 2] + 0.2) / 0.5, 0.0, 1.0)
    vorn_streng = np.clip((N[:, 2] - 0.1) / 0.4, 0.0, 1.0)    # Nase: nicht in die Nasenlöcher (die zeigen nach unten)
    for s, sg in (("r", -1.0), ("l", 1.0)):
        ex, en, ch, al = P[f"ex_{s}"], P[f"en_{s}"], P[f"ch_{s}"], P[f"al_{s}"]
        # Wange (Jochbeinbogen bis unter das Auge, etwas nach außen)
        c = ex + 0.45 * (ch - ex) + np.array([sg * 9.0, 2.0, 0.0])
        z[:, 0] += 0.85 * gauss(H, c, (15.0, 11.0, 14.0)) * vorn
        # Nasenflügel
        z[:, 0] += 0.35 * gauss(H, al, (4.5, 4.0, 5.0)) * vorn_streng
        # unter dem Auge: Mitte des Unterlids 6 mm tiefer, zum inneren Winkel stärker
        lid_u = 0.5 * (P["lm40" if s == "r" else "lm47"] + P["lm41" if s == "r" else "lm46"])
        z[:, 1] += 0.8 * gauss(H, lid_u + np.array([0.0, -6.0, -1.0]), (10.0, 4.5, 6.0)) * vorn
        z[:, 1] += 0.7 * gauss(H, en + np.array([sg * -1.0, -3.5, -1.0]), (4.0, 4.0, 5.0))
        # Oberlid über der Falte etwas dunkler (gering)
        lid_o = 0.5 * (P["lm37" if s == "r" else "lm43"] + P["lm38" if s == "r" else "lm44"])
        z[:, 1] += 0.3 * gauss(H, lid_o + np.array([0.0, 4.0, -1.5]), (9.0, 3.0, 5.0)) * vorn
        # Karunkel (innerer Augenwinkel): rosig und feucht
        k = gauss(H, en + np.array([sg * -1.2, 0.0, -1.0]), (1.6, 1.4, 2.0))
        z[:, 0] += 0.9 * k
        z[:, 2] += 1.0 * k
        # Lidrand: unten stark (Tränensaum), oben schwächer
        ring = [36, 41, 40, 39] if s == "r" else [45, 46, 47, 42]
        oben = [36, 37, 38, 39] if s == "r" else [45, 44, 43, 42]
        z[:, 2] += 1.0 * np.exp(-0.5 * (abstand_linie(H, np.array([P[f"lm{i}"] for i in ring])) / 0.7) ** 2)
        z[:, 2] += 0.4 * np.exp(-0.5 * (abstand_linie(H, np.array([P[f"lm{i}"] for i in oben])) / 0.6) ** 2)
        # Ohr: alles seitlich der Tragion (Ohrmuschel), in Ohrhöhe
        t = P[f"t_{s}"]
        ohr = np.clip((H[:, 0] * sg - (abs(t[0]) - 4.0)) / 6.0, 0.0, 1.0) \
            * np.clip(1.0 - np.abs(H[:, 1] - (t[1] + 5.0)) / 38.0, 0.0, 1.0) * (np.abs(H[:, 2] - t[2]) < 30)
        z[:, 0] += 0.7 * ohr
    # Nasenspitze, Kinn
    z[:, 0] += 0.55 * gauss(H, P["prn"] + np.array([0.0, 1.0, 0.0]), (7.0, 6.0, 6.0)) * vorn_streng
    z[:, 0] += 0.35 * gauss(H, P["pg"], (10.0, 8.0, 10.0)) * vorn
    # Nasenrücken: von der Nasenwurzel bis kurz vor die Spitze, schmal
    n, prn = P["n"], P["prn"]
    ruecken = abstand_linie(H, np.array([n + np.array([0.0, 3.0, 0.0]), n + 0.75 * (prn - n)]))
    z[:, 3] = np.exp(-0.5 * (ruecken / 3.5) ** 2) * vorn
    return np.clip(z, 0.0, 1.0)


def malen(uv, dreiecke, werte, g):
    """Werte je Ecke in die UV-Karte (g × g) malen; leere Bildpunkte bleiben −1."""
    bild = np.full((g, g, werte.shape[1]), -1.0)
    q = uv * g - 0.5
    for t in dreiecke:
        a, b, c = q[t[0]], q[t[1]], q[t[2]]
        x0, x1 = int(np.floor(min(a[0], b[0], c[0]))), int(np.ceil(max(a[0], b[0], c[0])))
        y0, y1 = int(np.floor(min(a[1], b[1], c[1]))), int(np.ceil(max(a[1], b[1], c[1])))
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, g - 1), min(y1, g - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        p = np.stack([xs.ravel(), ys.ravel()], 1).astype(float)
        v0, v1, v2 = b - a, c - a, p - a
        den = v0[0] * v1[1] - v1[0] * v0[1]
        if abs(den) < 1e-12:
            continue
        u = (v2[:, 0] * v1[1] - v1[0] * v2[:, 1]) / den
        v = (v0[0] * v2[:, 1] - v2[:, 0] * v0[1]) / den
        innen = (u >= -0.02) & (v >= -0.02) & (u + v <= 1.02)
        if not innen.any():
            continue
        w = (1 - u - v)[innen, None] * werte[t[0]] + u[innen, None] * werte[t[1]] + v[innen, None] * werte[t[2]]
        bild[p[innen, 1].astype(int), p[innen, 0].astype(int)] = w
    # Ränder dehnen (gegen Nähte): leere Bildpunkte bekommen den Mittelwert gefüllter Nachbarn
    for _ in range(6):
        leer = bild[..., 0] < 0
        if not leer.any():
            break
        summe = np.zeros_like(bild)
        anzahl = np.zeros(bild.shape[:2])
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            nb = np.roll(np.roll(bild, dy, 0), dx, 1)
            ok = nb[..., 0] >= 0
            summe[ok] += nb[ok]
            anzahl += ok
        neu = leer & (anzahl > 0)
        bild[neu] = summe[neu] / anzahl[neu, None]
    bild[bild < 0] = 0.0
    return bild


def main():
    glb_pfad, aus = sys.argv[1], sys.argv[2]
    opt = dict(a.split("=", 1) for a in sys.argv[3:])
    g = int(opt.get("groesse", "1024"))
    glb = Glb(glb_pfad)
    netz = Kopfnetz(glb)
    haut = netz.haut
    P = netz.punkte({})
    H = haut.grund * 1000.0
    N = haut.flaechennormalen(haut.grund)
    werte = zonen_ecken(H, N, P)
    bild = malen(haut.uv, haut.dreiecke, werte, g)
    os.makedirs(aus, exist_ok=True)
    pfad = os.path.join(aus, "haut_zonen.png")
    # Kanal 4 ist ein Datenkanal (Nasenrücken), kein Alpha: sonst teilt OpenImageIO beim PNG-Schreiben RGB durch A
    # (vormultipliziert -> nicht vormultipliziert), und wo A klein ist, sättigen die Zonen auf 1
    spec = oiio.ImageSpec(g, g, 4, oiio.FLOAT)
    spec.alpha_channel = -1
    spec.attribute("oiio:UnassociatedAlpha", 1)
    buf = oiio.ImageBuf(spec)
    buf.set_pixels(oiio.ROI(0, g, 0, g, 0, 1, 0, 4), np.ascontiguousarray(bild.astype(np.float32)))
    buf.write(pfad, "uint8")
    print("ZONEN", pfad, {k: round(float(werte[:, i].max()), 2) for i, k in enumerate("RGBA")},
          "Ecken > 0,1:", [int((werte[:, i] > 0.1).sum()) for i in range(4)])


if __name__ == "__main__":
    main()
