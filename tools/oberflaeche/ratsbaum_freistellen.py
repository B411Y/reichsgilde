#!/usr/bin/env python3
"""Stellt die gewaehlten Ratsbaum-Grafiken frei und skaliert sie auf die Zielgroessen.

Aufruf (vom Repo-Wurzelordner aus):
    python tools/oberflaeche/ratsbaum_freistellen.py <ordner_mit_den_roh_bildern>

Die Rohbilder (Ausgabe von ComfyUI, 1024 px) bleiben unveraendert; geschrieben wird nach
game/assets/oberflaeche/ratsbaum/. Zusammenhaengender Papier-Hintergrund am Rand wird
durchsichtig; weisse Innenflaechen (Waagschale ...) bleiben, weil sie nicht mit dem Rand zusammenhaengen.
Benoetigt numpy, Pillow, opencv-python.
"""
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

WURZEL = Path(__file__).resolve().parents[2]
ZIEL = WURZEL / "game" / "assets" / "oberflaeche" / "ratsbaum"

# Quelldatei (ohne .png) -> Zieldatei
AMT = {
    "amt_buergermeister_1": "amt_buergermeister",
    "amt_stadtrichter_1": "amt_stadtrichter",
    "amt_kaemmerer_2": "amt_kaemmerer",
    "amt_stadthauptmann_2": "amt_stadthauptmann",
    "amt_marktmeister_1": "amt_marktmeister",
    "amt_gefaengniswaerter_1": "amt_gefaengniswaerter",
    "amt_zoellner_2": "amt_zoellner",
}
TOLERANZ = 30      # Abstand zur Papierfarbe (je Farbkanal), darunter gilt ein Pixel als Hintergrund
KLEIN = 150        # Flecken im Vordergrund unter dieser Flaeche (Pixel) sind Staub und werden entfernt


def freistellen(rgb: np.ndarray) -> np.ndarray:
    h, w, _ = rgb.shape
    rand = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    papier = np.median(rand, axis=0)
    nah = (np.abs(rgb.astype(int) - papier).max(axis=2) <= TOLERANZ).astype(np.uint8)
    n, lab = cv2.connectedComponents(nah, connectivity=4)
    randlabel = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    hinten = np.isin(lab, list(randlabel))
    vorn = (~hinten).astype(np.uint8)
    # Staub entfernen
    n, lab, stat, _ = cv2.connectedComponentsWithStats(vorn, connectivity=8)
    for i in range(1, n):
        if stat[i, cv2.CC_STAT_AREA] < KLEIN:
            vorn[lab == i] = 0
    # Loecher im Vordergrund, die nicht zum Rand gehoeren, bleiben Vordergrund (schon so: nur Randverbund ist Hintergrund)
    # Rand: 1 px nach innen, dann leicht weichzeichnen
    kern = np.ones((3, 3), np.uint8)
    kern_innen = cv2.erode(vorn, kern)
    alpha = cv2.GaussianBlur(kern_innen.astype(np.float32), (0, 0), 0.8)
    alpha = np.where(vorn > 0, np.maximum(alpha, 0.0), 0.0)
    # Farbe am Rand von Papierresten befreien: Randfarbe aus dem Inneren nach aussen fortsetzen
    kern2 = cv2.erode(vorn, kern, iterations=2).astype(bool)
    farbe = rgb.astype(np.float32)
    gefuellt = kern2.copy()
    for _ in range(6):
        g = gefuellt.astype(np.float32)
        s = cv2.blur(farbe * g[..., None], (3, 3)) * 9
        z = cv2.blur(g, (3, 3)) * 9
        neu = (~gefuellt) & (z > 0)
        farbe[neu] = s[neu] / z[neu][:, None]
        gefuellt |= neu
    out = np.dstack([np.clip(farbe, 0, 255).astype(np.uint8), np.clip(alpha * 255, 0, 255).astype(np.uint8)])
    return out


def zuschneiden(rgba: np.ndarray, rand: int, quadrat: bool) -> Image.Image:
    a = rgba[..., 3]
    ys, xs = np.where(a > 8)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    bild = Image.fromarray(rgba[y0:y1, x0:x1], "RGBA")
    breite, hoehe = bild.size
    if quadrat:
        kante = max(breite, hoehe) + 2 * rand
        flaeche = Image.new("RGBA", (kante, kante), (0, 0, 0, 0))
        flaeche.paste(bild, ((kante - breite) // 2, (kante - hoehe) // 2))
        return flaeche
    flaeche = Image.new("RGBA", (breite + 2 * rand, hoehe + 2 * rand), (0, 0, 0, 0))
    flaeche.paste(bild, (rand, rand))
    return flaeche


def verkleinern(bild: Image.Image, groesse: tuple[int, int]) -> Image.Image:
    # in vormultipliziertem Alpha rechnen, sonst entsteht ein heller/dunkler Saum
    return bild.convert("RGBa").resize(groesse, Image.LANCZOS).convert("RGBA")


def kachel(bild: Image.Image) -> Image.Image:
    """Schneidet aus dem Seil eine waagerecht nahtlos wiederholbare Kachel (Ueberblendung der Naht ueber 24 px)."""
    a = np.array(bild).astype(np.float32)
    breite = a.shape[1]
    w = 24
    beste = (1e9, 0, 0)
    for periode in range(120, 700, 2):
        for x0 in range(20, breite - periode - w - 20, 4):
            d = float(np.abs(a[:, x0:x0 + w] - a[:, x0 + periode:x0 + periode + w]).mean())
            if d < beste[0]:
                beste = (d, periode, x0)
    _, periode, x0 = beste
    s = a[:, x0:x0 + periode + w]
    t = np.linspace(0, 1, w)[None, :, None]
    aus = s[:, :periode].copy()
    aus[:, :w] = s[:, periode:periode + w] * (1 - t) + s[:, :w] * t
    return Image.fromarray(aus.astype(np.uint8), "RGBA")


def laden(quelle: Path, name: str) -> np.ndarray:
    return np.array(Image.open(quelle / f"{name}.png").convert("RGB"))


def main() -> None:
    quelle = Path(sys.argv[1])
    ZIEL.mkdir(parents=True, exist_ok=True)
    for q, z in AMT.items():
        bild = zuschneiden(freistellen(laden(quelle, q)), 24, True)
        verkleinern(bild, (256, 256)).save(ZIEL / f"{z}.png", optimize=True)
    # Spruchband: 512 x 192, Seitenverhaeltnis bleibt, Rest transparent
    band = zuschneiden(freistellen(laden(quelle, "spruchband_1")), 8, False)
    faktor = min(512 / band.width, 192 / band.height)
    klein = verkleinern(band, (round(band.width * faktor), round(band.height * faktor)))
    flaeche = Image.new("RGBA", (512, 192), (0, 0, 0, 0))
    flaeche.paste(klein, ((512 - klein.width) // 2, (192 - klein.height) // 2))
    flaeche.save(ZIEL / "ratsbaum_spruchband.png", optimize=True)
    # Pergament unveraendert (nur 1024 x 768 sicherstellen)
    perg = Image.open(quelle / "pergament_2.png").convert("RGB")
    if perg.size != (1024, 768):
        perg = perg.resize((1024, 768), Image.LANCZOS)
    perg.save(ZIEL / "ratsbaum_pergament.png", optimize=True)
    # Linie: freistellen, dann ein Stueck suchen, das sich an beiden Enden gleicht (eine Seilwindung), und die Naht ueberblenden
    lin = zuschneiden(freistellen(laden(quelle, "linie_2")), 0, False)
    kachel(lin).save(ZIEL / "ratsbaum_linie.png", optimize=True)

if __name__ == "__main__":
    main()
