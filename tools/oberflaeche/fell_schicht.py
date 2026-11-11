"""Schicht „fell“ der Material-Bibliothek (Modell-Texturen): kurzes Fell für die Zugtiere, rein rechnerisch gebaut, nichts Fremdes.

    python -I tools/oberflaeche/fell_schicht.py [--probe]

Hängt eine 23. Schicht (1024 × 1024) an die beiden Atlanten game/assets/texturen/modelle/modelle_farbe_hoehe.webp und
modelle_normal_rauheit.webp (Gitter 2 × 11 → 2 × 12; die Platte der Schichten 0 bis 21 bleibt unverändert, die Farbdatei wird verlustbehaftet
neu geschrieben, q95 wie vorher), setzt in den .import-Dateien slices/vertical=12 und trägt die Schicht samt mittlerer Farbe in
game/daten/materialbibliothek.json ein. Läuft die Schicht schon (Atlas 12 Zeilen hoch), bleibt der Atlas, nur die Schicht wird ersetzt.
Die Zuordnung „Materialname → Schicht“ (namen) ergänzt zugtiere.py nicht von selbst: sie steht von Hand in der JSON-Datei.

Aussehen (Runde 13): Haare als kurze, verbogene Striche, die einem Richtungsfeld mit sanften Wirbeln folgen (line integral convolution, nahtlos),
dazu Strähnen von einigen Millimetern und ein Hauch weicher Töne; kein Muster im Großen, keine geraden Streifen (das las sich vorher wie Holzmaserung). Die Höhe
liegt in A, die Normale in R/G aus der Höhe, B Rauheit (matt-seidig 0,60 bis 0,88). Die Farbe ist nur Helligkeit mit einem Hauch Wärme: der Shader teilt
durch die mittlere Farbe und färbt mit der Modellfarbe (Eckenfarbe der Tiere). Kachel 0,25 m: 1 Pixel = 0,25 mm; die Strähnen (etwa 1 bis 2 mm breit) tragen das
Bild aus der Nähe, die feinsten Haare verschwinden in den Mip-Stufen (bewusst: sonst flimmerte es).
"""
import json
import os
import sys

import numpy as np

WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ZIEL = os.path.join(WURZEL, "game", "assets", "texturen", "modelle")
DATEN = os.path.join(WURZEL, "game", "daten", "materialbibliothek.json")
N = 256 if "--probe" in sys.argv else 1024
KACHEL = 0.25


def streifen(saat, sx, sy):
    """Nahtloses Rauschen: weißes Rauschen, im Frequenzraum gedämpft (sx, sy = Weichheit in x und y; gleich = ohne Richtung)."""
    rng = np.random.default_rng(saat)
    w = rng.standard_normal((N, N))
    fx = np.fft.fftfreq(N)[None, :]
    fy = np.fft.fftfreq(N)[:, None]
    filt = np.exp(-2 * (np.pi ** 2) * ((sx * fx) ** 2 + (sy * fy) ** 2))
    r = np.fft.ifft2(np.fft.fft2(w) * filt).real
    return (r - r.mean()) / (r.std() + 1e-9)


def weich(saat, sigma):
    """Nahtloses, weiches Rauschen ohne Richtung (isotrop), auf Mittel 0 und Streuung 1 gebracht."""
    return streifen(saat, sigma, sigma)


def abtasten(a, x, y):
    """Bilineare Probe eines nahtlos gekachelten Bildes an Stellen (x, y) in Bildpunkten."""
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = x - x0
    fy = y - y0
    x0 %= N
    y0 %= N
    x1 = (x0 + 1) % N
    y1 = (y0 + 1) % N
    return (a[y0, x0] * (1 - fx) + a[y0, x1] * fx) * (1 - fy) + (a[y1, x0] * (1 - fx) + a[y1, x1] * fx) * fy


def strich(rauschen, winkel, laenge, schritt):
    """Haarstriche aus Rauschen: je Bildpunkt der Mittelwert des Rauschens längs einer Bahn, die dem Richtungsfeld (winkel) in beide
    Richtungen folgt (line integral convolution, nahtlos). Weil das Feld sich krümmt und wirbelt, entstehen verbogene, kurze Striche
    mit Haarwirbeln statt gerader, gleichmäßiger Streifen (das las sich vorher wie Holzmaserung)."""
    ix, iy = np.meshgrid(np.arange(N, dtype=float), np.arange(N, dtype=float))
    summe = rauschen.copy()
    gewicht = np.ones((N, N))
    for richtung in (1.0, -1.0):
        x, y = ix.copy(), iy.copy()
        for k in range(1, laenge + 1):
            w = 0.5 * (1 + np.cos(np.pi * k / (laenge + 1)))          # Hann: weich auslaufende Strichenden
            th = abtasten(winkel, x, y)
            x += richtung * schritt * np.cos(th)
            y += richtung * schritt * np.sin(th)
            summe += w * abtasten(rauschen, x, y)
            gewicht += w
    r = summe / gewicht
    return (r - r.mean()) / (r.std() + 1e-9)


def bauen():
    """Runde 13: Haarkontrast etwa dreimal, Normale etwa zweieinhalbmal so stark, Kachel 0,25 m (1 Pixel = 0,25 mm), Haar als verbogene Striche
    in einem Richtungsfeld mit Wirbeln (LIC), dazu Strähnen und weiche Töne; Rauheit matt-seidig (Haarspitzen etwas glatter)."""
    s = N / 1024.0
    # Richtungsfeld: Grundrichtung nach unten (+y, wie das Fell an der Flanke läuft), verbogen und mit Wirbeln
    winkel = np.pi / 2 + 0.75 * weich(31, 90.0 * s) + 0.30 * weich(32, 30.0 * s)
    fein = strich(np.random.default_rng(41).standard_normal((N, N)), winkel, int(round(11 * s)), 1.0)        # einzelne Haare, etwa 6 mm
    kluft = strich(weich(42, 5.0 * s), winkel, int(round(18 * s)), 1.5)                                        # Strähnen, etwa 2 cm
    fleck = weich(43, 40.0 * s) + 0.4 * weich(44, 14.0 * s)                                                    # weiche, ungerichtete Töne
    h = 0.60 * fein + 0.95 * kluft + 0.10 * fleck
    lo, hi = np.percentile(h, [1.0, 99.0])
    h = np.clip((h - lo) / (hi - lo), 0.0, 1.0)
    # Haare stehen als Rücken, Täler dazwischen sind dunkler (Unterwolle); Kontrast etwa dreimal so groß wie in Runde 11
    hell = 0.62 + 0.38 * h ** 1.15
    farbe = np.dstack([hell * 0.97, hell * 0.93, hell * 0.88])            # Hauch warm
    # Normale aus der Höhe (OpenGL: G+ zum oberen Bildrand); die Höhe leicht geglättet, damit nicht jeder Bildpunkt kippt
    hg = (h + np.roll(h, 1, 0) + np.roll(h, -1, 0) + np.roll(h, 1, 1) + np.roll(h, -1, 1)) / 5.0
    stark = 28.0
    nx = -np.gradient(hg, axis=1) * stark
    ny = np.gradient(hg, axis=0) * stark          # Zeile 0 oben: + Richtung nach unten, OpenGL G+ nach oben
    nz = np.ones_like(h)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx / l, ny / l, nz / l
    # Rauheit: matt-seidig (0,60 bis 0,88; Runde 11: 0,80 bis 0,92), Haarspitzen etwas glatter als die Täler
    rau = np.clip(0.88 - 0.28 * h, 0, 1)
    return farbe, h, (nx, ny, nz), rau


def linear(c8):
    c = c8 / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def main():
    from PIL import Image
    farbe, h, (nx, ny, nz), rau = bauen()
    f8 = np.dstack([np.round(farbe * 255), 1 + np.round(h * 254)]).astype(np.uint8)
    n8 = np.dstack([np.round((nx * 0.5 + 0.5) * 255), np.round((ny * 0.5 + 0.5) * 255), np.round(rau * 255)]).astype(np.uint8)
    mittel = [round(float(c), 5) for c in linear(f8[..., :3].astype(float)).reshape(-1, 3).mean(0)]
    print("Mittel linear", mittel, "Höhe", round(float(h.mean()), 3), "±", round(float(h.std()), 3))
    if N != 1024:
        Image.fromarray(f8[..., :3]).save(os.path.join(WURZEL, "fell_probe.png"))
        return
    for datei, neu, qual in (("modelle_farbe_hoehe", f8, 95), ("modelle_normal_rauheit", n8, None)):
        pfad = os.path.join(ZIEL, datei + ".webp")
        alt = np.array(Image.open(pfad))
        zeilen_alt = alt.shape[0] // N
        if zeilen_alt == 11:                          # Schicht 22 = Zeile 11, Spalte 0 (Zeile für Zeile, zwei Spalten)
            ext = np.zeros((alt.shape[0] + N, alt.shape[1], alt.shape[2]), alt.dtype)
            ext[:alt.shape[0]] = alt
            alt = ext
        alt[11 * N:12 * N, 0:N] = neu
        im = Image.fromarray(alt)
        if qual is None:
            im.save(pfad, lossless=True, quality=100, method=6)
        else:
            im.save(pfad, quality=qual, method=6)
        print("geschrieben:", os.path.relpath(pfad, WURZEL), alt.shape, f"{os.path.getsize(pfad) / 1e6:.1f} MB")
        imp = pfad + ".import"
        t = open(imp, encoding="utf-8").read().replace("slices/vertical=11", "slices/vertical=12")
        open(imp, "w", encoding="utf-8", newline="\n").write(t)
    d = json.load(open(DATEN, encoding="utf-8"))
    sch = [s for s in d["schichten"] if s["name"] != "fell"]
    sch.append({"name": "fell", "kachel": KACHEL, "projektion": "wand", "mittel": mittel})
    d["schichten"] = sch
    for pal in ("pferd_rotbraun", "pferd_schwarzbraun", "ochse_rotbraun", "ochse_falb"):
        for st in ("Fell", "Dunkel", "Haar"):
            d["namen"][f"{pal} {st}"] = "fell"
    zeilen = ["{"]
    teile = [f'  "_hinweis": {json.dumps(d["_hinweis"], ensure_ascii=False)}',
             '  "schichten": [\n' + ",\n".join("    " + json.dumps(s, ensure_ascii=False) for s in d["schichten"]) + "\n  ]",
             '  "namen": {\n' + ",\n".join(f"    {json.dumps(k, ensure_ascii=False)}: {json.dumps(v, ensure_ascii=False)}" for k, v in d["namen"].items()) + "\n  }"]
    zeilen.append(",\n".join(teile))
    zeilen.append("}")
    open(DATEN, "w", encoding="utf-8", newline="\n").write("\n".join(zeilen) + "\n")
    print("geschrieben:", os.path.relpath(DATEN, WURZEL))


if __name__ == "__main__":
    main()
