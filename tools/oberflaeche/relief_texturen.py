"""Relief-Texturen der Modelle: aus CC0-Texturen von Poly Haven (https://polyhaven.com, Lizenz CC0 1.0) packen.

    python3 -I tools/oberflaeche/relief_texturen.py --quellen=<ordner mit einem Unterordner je Poly-Haven-Name>  [--acg=<ordner mit ambientCG-Paketen>] [--vorschau=<ordner>]

Die Quellordner holt man einmal von Hand (je Textur eigener Ordner, nie im Repository): von api.polyhaven.com/files/<name> die 1k-JPG-Dateien
`<name>_diff_1k.jpg` (Farbe), `<name>_nor_gl_1k.jpg` (Normale, OpenGL), `<name>_arm_1k.jpg` (R Verdeckung, G Rauheit, B Metall) und
`<name>_disp_1k.jpg` (Hoehe). Das Skript schreibt nach game/assets/texturen/modelle/ zwei Texture2DArrays im Gitter (wie die Material-Bibliothek,
tools/oberflaeche/materialien_blender.py, eine Spalte, 1024 x 1024 je Schicht):

    relief_farbe_hoehe.webp         RGBA: RGB Farbe (sRGB, verlustbehaftet q95; Kontrast und Farbigkeit gedaempft), A Hoehe (255 oben, 0 Fuge; verlustfrei)
    relief_normal_rauheit_ao.webp   RGBA: R/G Normale (OpenGL), B Rauheit (verlustbehaftet q95: die Quellen sind selbst JPEG, verlustfrei waere dreimal so gross), A Verdeckung (AO, verlustfrei)

und in game/daten/materialbibliothek.json den Block "relief" (Schicht je Textur: Kachel in Metern, mittlere Farbe linear, Tiefe in Metern, Staerke).
Der Shader (oberflaeche.gdshader) nimmt die Relief-Schicht, wenn es sie gibt, sonst die bisherige Schicht gleichen Namens. Die Farbe hier ist nur
Zeichnung: Der Shader teilt durch die mittlere Farbe und faerbt mit der Modellfarbe, die Haeuser behalten ihre Farben.
"""
import json
import os
import sys

import numpy as np
from PIL import Image

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ZIEL = os.path.join(WURZEL, "game", "assets", "texturen", "modelle")
DATEN = os.path.join(WURZEL, "game", "daten", "materialbibliothek.json")
N = 1024
AO_FARBE = 0.55   # wie REL_AO_FARBE in oberflaeche.gdshader

# Schicht (Name in der Bibliothek; "basis" ist die bisherige Schicht, die ersetzt wird, sonst neu), Poly-Haven-Name, Kachel in Metern (Mass der
# Aufnahme, ggf. gerundet), Projektion wie in der Bibliothek, Reihenhoehe, Tiefe des Reliefs in Metern (Parallax), Staerke der Normalen,
# Kontrast und Farbigkeit der Abweichung vom Mittel (1 = wie die Aufnahme).
SCHICHTEN = [
    dict(name="bruchstein", ph="stone_wall", kachel=2.0, projektion="wand", tiefe=0.05, staerke=1.0, kontrast=0.85, farbe=0.6),
    dict(name="quader", ph="rustic_stone_wall", kachel=1.6, projektion="wand", tiefe=0.04, staerke=1.0, kontrast=0.85, farbe=0.6),
    dict(name="kalkputz", ph="white_plaster_rough_01", kachel=1.5, projektion="wand", tiefe=0.02, staerke=1.2, kontrast=0.9, farbe=0.6),
    dict(name="lehmputz", ph="clay_plaster", kachel=2.0, projektion="wand", tiefe=0.03, staerke=1.1, kontrast=0.9, farbe=0.6),
    dict(name="fachwerkholz", ph="rough_wood", kachel=0.8, projektion="wand", tiefe=0.02, staerke=1.0, kontrast=0.8, farbe=0.5),
    dict(name="bretter", ph="old_planks_02", kachel=2.0, projektion="wand", tiefe=0.02, staerke=1.0, kontrast=0.85, farbe=0.5),
    dict(name="dachziegel", ph="clay_roof_tiles_02", kachel=1.25, projektion="reihen", reihe=0.45, tiefe=0.05, staerke=1.0, kontrast=0.85, farbe=0.6),
    dict(name="schiefer", ph="RoofingTiles003", quelle="acg", kachel=1.5, projektion="reihen", reihe=0.32, tiefe=0.03, staerke=1.1, kontrast=0.85, farbe=0.6),
    dict(name="stroh", ph="thatch_roof_angled", kachel=0.9, projektion="dach", tiefe=0.05, staerke=1.0, kontrast=0.8, farbe=0.6),
    dict(name="sandstein", ph="yellow_stone_wall", kachel=2.0, projektion="wand", tiefe=0.04, staerke=1.0, kontrast=0.85, farbe=0.6),
    dict(name="ziegelstein", ph="medieval_red_brick", kachel=2.0, projektion="wand", tiefe=0.03, staerke=1.0, kontrast=0.85, farbe=0.6),
    dict(name="holzschindeln", ph="roof_slates_02", kachel=1.5, projektion="reihen", reihe=0.28, tiefe=0.03, staerke=1.1, kontrast=0.85, farbe=0.5),
    dict(name="pflaster", ph="cobblestone_floor_01", kachel=1.5, projektion="wand", tiefe=0.05, staerke=1.0, kontrast=0.85, farbe=0.6, neu=True, nass=0.55, flach=True),
]


def srgb_zu_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_zu_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


# Dateinamen je Quelle: Poly Haven "<name>_<art>_1k.jpg" (arm = R Verdeckung, G Rauheit, B Metall), ambientCG "<name>_1K-JPG_<Art>.jpg"
ACG = {"diff": "Color", "disp": "Displacement", "nor_gl": "NormalGL", "rau": "Roughness"}


def laden(ordner, name, art, quelle="ph"):
    if quelle == "acg":
        pfad = os.path.join(ordner, name, f"{name}_1K-JPG_{ACG[art]}.jpg")
    else:
        pfad = os.path.join(ordner, name, f"{name}_{art}_1k.jpg")
    bild = Image.open(pfad)
    if bild.size != (N, N):
        raise SystemExit(f"{pfad}: {bild.size}, erwartet {N} x {N}")
    return np.asarray(bild.convert("RGB"), dtype=np.float32) / 255.0


def naht(flaeche):
    """Wie sichtbar ist die Naht beim Kacheln? Verhaeltnis des Sprungs an der Kante zum mittleren Sprung benachbarter Punkte (1 = unsichtbar)."""
    g = flaeche.mean(axis=2)
    innen = (np.abs(np.diff(g, axis=0)).mean() + np.abs(np.diff(g, axis=1)).mean()) / 2
    kante = (np.abs(g[0] - g[-1]).mean() + np.abs(g[:, 0] - g[:, -1]).mean()) / 2
    return float(kante / max(innen, 1e-6))


def tiefpass(a, sigma):
    """Kachelnder Gauss-Tiefpass (FFT) eines Feldes N x N (x Kanaele)."""
    ky = np.fft.fftfreq(N)[:, None]
    kx = np.fft.rfftfreq(N)[None, :]
    g = np.exp(-2 * (np.pi ** 2) * (kx ** 2 + ky ** 2) * sigma ** 2)
    if a.ndim == 2:
        return np.fft.irfft2(np.fft.rfft2(a) * g, s=(N, N))
    return np.dstack([np.fft.irfft2(np.fft.rfft2(a[:, :, k]) * g, s=(N, N)) for k in range(a.shape[2])])


def verdeckung_aus_hoehe(hoehe):
    """Wo eine Quelle keine Verdeckung (AO) mitbringt: Senken gegenueber der weichgezeichneten Umgebung dunkler (kachelnd gerechnet)."""
    f = np.fft.rfft2(hoehe)
    ky = np.fft.fftfreq(N)[:, None]
    kx = np.fft.rfftfreq(N)[None, :]
    weich = np.fft.irfft2(f * np.exp(-2 * (np.pi ** 2) * (kx ** 2 + ky ** 2) * 8.0 ** 2), s=(N, N))
    return np.clip(1.0 - 2.2 * np.maximum(weich - hoehe, 0.0), 0.35, 1.0).astype(np.float32)


def schicht(ordner, s):
    q = s.get("quelle", "ph")
    farbe = srgb_zu_linear(laden(ordner, s["ph"], "diff", q))
    mittel = farbe.reshape(-1, 3).mean(axis=0)
    lum = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    # Abweichung vom Mittel daempfen: Kontrast (Helligkeit) und Farbigkeit (Farbton) getrennt
    verhaeltnis = farbe / np.maximum(mittel, 1e-4)
    hell = (verhaeltnis * lum).sum(axis=2, keepdims=True) / (mittel * lum).sum() * (mittel * lum).sum()
    hell = np.maximum(hell, 1e-4)
    verhaeltnis = hell + (verhaeltnis - hell) * s["farbe"]
    verhaeltnis = 1.0 + (verhaeltnis - 1.0) * s["kontrast"]
    farbe = np.maximum(verhaeltnis, 0.02) * mittel
    mittel = farbe.reshape(-1, 3).mean(axis=0)
    hoehe = laden(ordner, s["ph"], "disp", q)[:, :, 0]
    lo, hi = np.percentile(hoehe, [1, 99])
    hoehe = np.clip((hoehe - lo) / max(hi - lo, 1e-4), 0, 1)
    nor = laden(ordner, s["ph"], "nor_gl", q)
    if q == "acg":
        rau, ao = laden(ordner, s["ph"], "rau", q)[:, :, 0], verdeckung_aus_hoehe(hoehe)
    else:
        arm = laden(ordner, s["ph"], "arm")
        rau, ao = arm[:, :, 1], arm[:, :, 0]
    if s.get("flach"):
        # Grossraeumige Helligkeits-/Farbflecken und Neigungen der Aufnahme entfernen (sonst kachelt ein Karo oder eine Raute sichtbar)
        farbe = farbe / np.maximum(tiefpass(farbe, 200.0), 1e-3) * tiefpass(farbe, 200.0).reshape(-1, 3).mean(axis=0)
        nor = nor.copy()
        for k in (0, 1):
            nor[:, :, k] = np.clip(nor[:, :, k] - tiefpass(nor[:, :, k], 160.0) + 0.5, 0, 1)
    farbe_srgb = linear_zu_srgb(farbe)
    # Der Shader dunkelt die Farbe mit der Verdeckung ab (mix(1, AO, AO_FARBE)); das Mittel muss das enthalten, sonst wird jede Flaeche um diesen Anteil dunkler
    # als die Modellfarbe (Pflaster wurde anthrazit). So bleibt die mittlere Helligkeit gleich der Modellfarbe.
    if True:
        ao_faktor = 1.0 - AO_FARBE * (1.0 - ao)
        mittel = (farbe * ao_faktor[:, :, None]).reshape(-1, 3).mean(axis=0)
        # mittlere Helligkeit des Streiflichts auf der geneigten Flaeche (Sonne 35 und 55 Grad, Normale + Neigung wie im Shader) ausgleichen
        tx, ty = (nor[:, :, 0] * 2 - 1) * s["staerke"], (nor[:, :, 1] * 2 - 1) * s["staerke"]
        n3 = np.dstack([tx, np.ones_like(tx), ty])
        n3 /= np.linalg.norm(n3, axis=2, keepdims=True)
        cosm = 0.0
        for el in (35.0, 55.0):
            L = np.array([np.cos(np.radians(el)) * 0.7071, np.sin(np.radians(el)), np.cos(np.radians(el)) * 0.7071])
            cosm += np.maximum(0.0, (n3 * L).sum(2)).mean() / L[1] / 2
        mittel = mittel * cosm
        s["licht_mittel"] = float(cosm)
        s["ao_mittel"] = float(ao_faktor.mean())
    fh = np.dstack([farbe_srgb, hoehe])
    nr = np.dstack([nor[:, :, 0], nor[:, :, 1], rau, ao])
    return fh, nr, [float(x) for x in mittel], naht(farbe_srgb), naht(nor)


def schreiben(stapel, pfad, verlustfrei):
    """Schichten untereinander in ein Bild (eine Spalte); verlustfrei oder RGB q95 mit verlustfreier Alpha."""
    bild = Image.fromarray((np.concatenate(stapel, axis=0) * 255.0 + 0.5).astype(np.uint8), "RGBA")
    if verlustfrei:
        bild.save(pfad, "WEBP", lossless=True, quality=100, method=4, exact=True)
    else:
        bild.save(pfad, "WEBP", lossless=False, quality=95, alpha_quality=100, method=4, exact=True)
    print(f"{os.path.relpath(pfad, WURZEL)}: {os.path.getsize(pfad) / 1e6:.1f} MB, {len(stapel)} Schichten")


def main():
    opt = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in sys.argv[1:])
    ordner = opt.get("quellen")
    if not ordner:
        raise SystemExit(__doc__)
    farben, normalen, eintraege = [], [], []
    for s in SCHICHTEN:
        fh, nr, mittel, n1, n2 = schicht(opt.get("acg", ordner) if s.get("quelle") == "acg" else ordner, s)
        print(f"{s['name']:14s} {s['ph']:24s} mittel {mittel[0]:.3f} {mittel[1]:.3f} {mittel[2]:.3f}  AO-Faktor {s["ao_mittel"]:.2f} Licht {s["licht_mittel"]:.2f}  Naht Farbe {n1:.2f} Normale {n2:.2f}")
        farben.append(fh)
        normalen.append(nr)
        e = {"name": s["name"], "kachel": s["kachel"], "projektion": s["projektion"], "mittel": [round(x, 5) for x in mittel],
             "tiefe": s["tiefe"], "staerke": s["staerke"], "nass": s.get("nass", 1.0), "ao_mittel": round(s["ao_mittel"], 4)}
        if "reihe" in s:
            e["reihe"] = s["reihe"]
        if s.get("neu"):
            e["neu"] = True
        eintraege.append(e)
    schreiben(farben, os.path.join(ZIEL, "relief_farbe_hoehe.webp"), False)
    schreiben(normalen, os.path.join(ZIEL, "relief_normal_rauheit_ao.webp"), False)
    if "vorschau" in opt:
        os.makedirs(opt["vorschau"], exist_ok=True)
        for i, s in enumerate(SCHICHTEN):
            kleine = [Image.fromarray((x * 255 + 0.5).astype(np.uint8)).resize((256, 256)) for x in
                      (farben[i][:, :, :3], np.dstack([normalen[i][:, :, :2], np.full((N, N), 0.5)]),
                       np.dstack([farben[i][:, :, 3]] * 3), np.dstack([normalen[i][:, :, 3], normalen[i][:, :, 2], normalen[i][:, :, 2]]))]
            blatt = Image.new("RGB", (1024, 256))
            for k, b in enumerate(kleine):
                blatt.paste(b, (k * 256, 0))
            blatt.save(os.path.join(opt["vorschau"], f"schicht_{i:02d}_{s['name']}.jpg"), quality=88)
    # Block "relief" in der Bibliothek ersetzen oder anhaengen (der Rest der Datei bleibt, wie er ist)
    text = open(DATEN, encoding="utf-8").read()
    block = '  "relief": ' + json.dumps({"_hinweis": "Relief-Texturen (CC0, Poly Haven): tools/oberflaeche/relief_texturen.py. Gleicher Name wie eine bisherige Schicht ersetzt "
                                          "sie, wo die Dateien da sind; 'neu' = Schicht ohne Vorgaenger (nur mit Relief). 'namen' bildet Materialnamen auf neue Schichten ab.",
                                          "schichten": eintraege, "namen": {"Pflaster": "pflaster"}}, indent=2, ensure_ascii=False).replace("\n", "\n  ")
    i = text.find('\n  "relief":')
    basis = text[:i] if i >= 0 else text.rstrip().rstrip("}")
    text = basis.rstrip().rstrip(",") + ",\n" + block + "\n}\n"
    open(DATEN, "w", encoding="utf-8", newline="\n").write(text)
    print("materialbibliothek.json: Block relief geschrieben")


if __name__ == "__main__":
    main()
