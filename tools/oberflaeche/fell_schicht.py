"""Schicht „fell“ der Material-Bibliothek (Modell-Texturen): kurzes Fell für die Zugtiere, rein rechnerisch gebaut, nichts Fremdes.

    python -I tools/oberflaeche/fell_schicht.py [--probe]

Hängt eine 23. Schicht (1024 × 1024) an die beiden Atlanten game/assets/texturen/modelle/modelle_farbe_hoehe.webp und
modelle_normal_rauheit.webp (Gitter 2 × 11 → 2 × 12; die Platte der Schichten 0 bis 21 bleibt unverändert, die Farbdatei wird verlustbehaftet
neu geschrieben, q95 wie vorher), setzt in den .import-Dateien slices/vertical=12 und trägt die Schicht samt mittlerer Farbe in
game/daten/materialbibliothek.json ein. Läuft die Schicht schon (Atlas 12 Zeilen hoch), bleibt der Atlas, nur die Schicht wird ersetzt.
Die Zuordnung „Materialname → Schicht“ (namen) ergänzt zugtiere.py nicht von selbst: sie steht von Hand in der JSON-Datei.

Aussehen: Haare als feine Striche entlang der Bildhöhe (auf der Flanke läuft das Fell so von oben nach unten), nahtlos gekachelt (FFT-
Filter), drei Größen übereinander, ohne Muster im Großen (sonst sähe man die Kachel im Raster); die Höhe liegt in A, die Normale in R/G aus der Höhe, B Rauheit. Die Farbe ist nur Helligkeit mit
einem Hauch Wärme: der Shader teilt durch die mittlere Farbe und färbt mit der Modellfarbe (Eckenfarbe der Tiere).
Kachel 0,5 m: 1 Pixel = 0,5 mm, ein Haar etwa 2 bis 3 Pixel breit (bewusst gröber als in der Natur: von der Spielkamera aus sähe man sonst nichts).
"""
import json
import os
import sys

import numpy as np

WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ZIEL = os.path.join(WURZEL, "game", "assets", "texturen", "modelle")
DATEN = os.path.join(WURZEL, "game", "daten", "materialbibliothek.json")
N = 256 if "--probe" in sys.argv else 1024
KACHEL = 0.5


def streifen(saat, sx, sy):
    """Nahtloses Rauschen mit Streifen entlang der y-Achse: weißes Rauschen, im Frequenzraum anisotrop gedämpft."""
    rng = np.random.default_rng(saat)
    w = rng.standard_normal((N, N))
    fx = np.fft.fftfreq(N)[None, :]
    fy = np.fft.fftfreq(N)[:, None]
    filt = np.exp(-2 * (np.pi ** 2) * ((sx * fx) ** 2 + (sy * fy) ** 2))
    r = np.fft.ifft2(np.fft.fft2(w) * filt).real
    return (r - r.mean()) / (r.std() + 1e-9)


def bauen():
    s = N / 1024.0
    grob = streifen(11, 5.0 * s, 70.0 * s)          # Haarbüschel
    mittel = streifen(12, 2.6 * s, 26.0 * s)         # einzelne Haare, länger
    fein = streifen(13, 1.3 * s, 9.0 * s)            # Härchen
    h = 0.45 * grob + 0.75 * mittel + 0.55 * fein
    h = (h - h.min()) / (h.max() - h.min())
    # Haare stehen als Rücken, die Täler dazwischen sind dunkler (Unterwolle)
    hell = 0.66 + 0.34 * h ** 1.25
    hell = np.clip(hell, 0.2, 1.0)
    farbe = np.dstack([hell * 0.97, hell * 0.93, hell * 0.88])            # Hauch warm
    # Normale aus der Höhe (OpenGL: G+ zum oberen Bildrand); wegen der Streifen vor allem seitlich geneigt
    stark = 22.0
    nx = -np.gradient(h, axis=1) * stark
    ny = np.gradient(h, axis=0) * stark          # Zeile 0 oben: + Richtung nach unten, OpenGL G+ nach oben
    nz = np.ones_like(h)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx / l, ny / l, nz / l
    rau = np.clip(0.92 - 0.12 * h, 0, 1)
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
