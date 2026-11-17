"""Pruefung der Steinebenen (Texture2DArrays aus trimsheet_ebenen.py), je Ebene und Band, ohne Blender:

    python -I tools/modelle/neubau/ebenen_pruefung.py [--array=haeuser|innen|rathaus|bruecke] [--ordner=<ordner>] [--layout=<datei.py>] [--gegenprobe]
           [--als-blatt=<ordner>]

Prueft (Rueckgabewert 1 bei Fehlern, Ausgabe mit Praefix PRUEFUNG):
  * <array>_ebenen_albedo/normal/orm.png vorhanden, gleich gross, 2048 breit, Hoehe = 2048 x Zahl der Ebenen in ARRAYS;
  * je Ebene und Band (Bandgrenzen aus dem Layout des Blatts) die Werte von Pruefung 12 und 15 (uv2_pruefung.py) auf das Blatt
    statt auf die Flaechen eines Modells: Albedo im Mittel mindestens 0,025 (Eisenbaender ausgenommen), Normalmap im Mittel nahe
    (0,5, 0,5, 1) (Blau mindestens 0,6, Rot und Gruen hoechstens 0,35 von 0,5), ORM: Verdeckung im Mittel mindestens 0,05, Rauheit
    nicht im ganzen Band 0 bzw. 1;
  * Fugenkontrast in den Steinbaendern, die das Skript ersetzt hat: Streuung der Helligkeit (Abstand 10./90. Perzentil durch das
    Mittel) mindestens FUGE_MIN – eine leere oder flache Ebene faellt auf.
--gegenprobe: dieselbe Pruefung mit einer zweiten Fassung, in der eine Ebene leer (0) ist; sie muss Fehler melden.
--als-blatt=<ordner>: schreibt jede Ebene als <array>_e<i>_{albedo,normal,orm}.png, damit uv2_pruefung.py (Pruefungen 12 bis 15
mit einem Modell) eine Ebene wie ein Blatt pruefen kann: --texturen=<ordner> --blatt=<array>_e<i>.
"""
import os
import sys

import numpy as np
from PIL import Image

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import trimsheet_ebenen as te        # noqa: E402  (ARRAYS, layout_lesen, N)

N = te.N
ALBEDO_MIN = 0.025
NORMAL_B_MIN, NORMAL_RG_ABW = 0.6, 0.35
ORM_AO_MIN, ORM_RAU_RAND = 0.05, 0.01
FUGE_MIN = 0.25          # gemessen 15.11.2026: 0,40 (K5) bis 0,85 (K4) in den ersetzten Steinbaendern; leere Ebene 0


def lade(pfad):
    with Image.open(pfad) as b:
        return np.asarray(b, dtype=np.float32) / 255.0


def pruefe(array, felder, zeilen, layout=None):
    """felder: dict art -> Feld (alle Ebenen untereinander). Rueckgabe Fehlerzahl; schreibt Zeilen."""
    cfg = te.ARRAYS[array]
    baender = te.layout_lesen(layout or cfg["layout"][0], cfg["layout"][1])
    ebenen = cfg["ebenen"]
    fehler = 0

    def melde(text):
        nonlocal fehler
        fehler += 1
        zeilen.append("   FEHLER " + text)
    for art in ("albedo", "normal", "orm"):
        f = felder.get(art)
        if f is None:
            melde(f"{art}: Bild fehlt"); continue
        if f.shape[1] != N or f.shape[0] != N * len(ebenen):
            melde(f"{art}: {f.shape[1]} x {f.shape[0]} statt {N} x {N * len(ebenen)}")
    if fehler:
        return fehler
    alb, nor, orm = felder["albedo"], felder["normal"], felder["orm"]
    for i, (name, blatt, ersatz, *_nach) in enumerate(ebenen):
        o0 = i * N
        werte = []
        for band, (v0, v1, _m) in baender.items():
            a, e = o0 + int(round(v0 * N)), o0 + int(round(v1 * N))
            al = alb[a:e, :, :3]; nm = nor[a:e, :, :3].reshape(-1, 3).mean(axis=0); om = orm[a:e, :, :3]
            hell = al @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
            ort = f"Ebene {i} {name}, Band {band}"
            if "eisen" not in band and float(hell.mean()) < ALBEDO_MIN:
                melde(f"{ort}: Albedo {hell.mean():.3f}")
            if nm[2] < NORMAL_B_MIN or abs(nm[0] - 0.5) > NORMAL_RG_ABW or abs(nm[1] - 0.5) > NORMAL_RG_ABW:
                melde(f"{ort}: Normalmap ({nm[0]:.2f}, {nm[1]:.2f}, {nm[2]:.2f})")
            if float(om[..., 0].mean()) < ORM_AO_MIN:
                melde(f"{ort}: Verdeckung {om[..., 0].mean():.2f}")
            if om[..., 1].max() <= ORM_RAU_RAND or om[..., 1].min() >= 1 - ORM_RAU_RAND:
                melde(f"{ort}: Rauheit flaechig {om[..., 1].min():.2f}..{om[..., 1].max():.2f}")
            if band in ersatz:
                p10, p90 = np.percentile(hell, (10, 90))
                fuge = float((p90 - p10) / max(float(hell.mean()), 1e-4))
                werte.append(f"{band} {ersatz[band] if isinstance(ersatz[band], str) else ersatz[band][0]} Fugenkontrast {fuge:.2f}")
                if fuge < FUGE_MIN:
                    melde(f"{ort}: Fugenkontrast {fuge:.2f} < {FUGE_MIN}")
        zeilen.append(f"   Ebene {i} {name} (Blatt {blatt}): {len(baender)} Baender" + (": " + "; ".join(werte) if werte else ""))
    return fehler


def main():
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in sys.argv[1:] if a.startswith("--"))
    array = opt.get("array", "haeuser")
    ordner = opt.get("ordner", te.ARRAYS[array]["ordner"])
    felder = {}
    for art in ("albedo", "normal", "orm"):
        p = os.path.join(ordner, f"{array}_ebenen_{art}.png")
        if os.path.exists(p):
            felder[art] = lade(p)
    zeilen = [f"PRUEFUNG Steinebenen {array} ({ordner})"]
    fehler = pruefe(array, felder, zeilen, opt.get("layout"))
    zeilen.append(f"PRUEFUNG {array}: {fehler} Fehler")
    gegen_ok = True
    if "gegenprobe" in opt and not fehler:
        leer = {k: v.copy() for k, v in felder.items()}
        k = len(te.ARRAYS[array]["ebenen"]) - 1
        for v in leer.values():
            v[k * N:(k + 1) * N] = 0
        gz = []
        gf = pruefe(array, leer, gz, opt.get("layout"))
        gegen_ok = gf > 0
        zeilen.append(f"PRUEFUNG Gegenprobe (Ebene {k} leer): {gf} Fehler - " + ("schlaegt an" if gegen_ok else "SCHLAEGT NICHT AN"))
    if "als-blatt" in opt:
        os.makedirs(opt["als-blatt"], exist_ok=True)
        for i in range(len(te.ARRAYS[array]["ebenen"])):
            for art, f in felder.items():
                teil = (np.clip(f[i * N:(i + 1) * N], 0, 1) * 255 + 0.5).astype(np.uint8)
                Image.fromarray(teil).save(os.path.join(opt["als-blatt"], f"{array}_e{i}_{art}.png"))
        zeilen.append(f"Ebenen als Blaetter in {opt['als-blatt']}")
    print("\n".join(zeilen))
    return 1 if fehler or not gegen_ok else 0


if __name__ == "__main__":
    sys.exit(main())
