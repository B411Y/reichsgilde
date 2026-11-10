"""Bereitet die gekauften GameDev.tv-Texturen („Open World Terrain Pack“, Terrain Layer Textures) für Reichsgilde auf.

    python -I tools/assets/privat_bauen.py --quelle "<Ordner Terrain Layer Textures>" --ziel <privater Ordner> [--repo <Reichsgilde-Ordner>]

Das Skript enthält keine Bilddaten. Die Texturen stehen unter der GameDev.tv General Asset License 2.0 (Nutzung im Spiel erlaubt, Weitergabe
als einzelne Dateien nicht) und dürfen darum nie in das öffentliche Repository (siehe docs/assets/README.md, Abschnitt „Private Texturen“).
Das Ergebnis liegt außerhalb von Git (z. B. C:\\Reichsgilde_Privat\\gamedevtv); tools/assets/privat_holen.py kopiert es nach game/assets/privat/.

Stufe 1 (sets/): je Satz fünf Karten in 2048 × 2048 (Albedo, Normale OpenGL, Rauheit, AO, Höhe), verlustfreies WebP.
Stufe 2 (spiel/): was das Spiel lädt, in 1024 × 1024:
  boden/boden_farbe_hoehe.webp, boden/boden_normal_rauheit.webp  der Boden-Atlas des Spiels (elf Schichten untereinander) mit ausgetauschten
                                                                 Schichten: Fels (Rock 1), Erde (Soil 1), Kies (Ground 1), Schlamm (Soil 2);
                                                                 die übrigen Schichten stammen aus dem Atlas im Repo (--repo)
  fels/fels_struktur.png                                         Struktur des Fels-Shaders (R, G Neigung, B Höhe, A Flecken) aus Rock 2
  schnee/schnee_farbe_hoehe.webp, schnee/schnee_normal_rauheit.webp   Schneeauflage (Snow_1)
Danach schreibt es docs/assets/privat_manifest.json (Name, Größe, SHA-256, Lizenz; keine Bilddaten) in das Repo (--manifest).
Alle Prüfwerte (Nahtlosigkeit der Kacheln, Richtung der Normalen, Helligkeit) druckt es aus.
"""
import argparse
import hashlib
import json
import os
import sys

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None

LIZENZ = "GameDev.tv General Asset License 2.0"
QUELLE = "gamedev.tv, Open World Terrain Pack (Terrain Layer Textures)"
LIZENZTEXT = """Private Texturen von Reichsgilde - nicht weitergeben
=====================================================

Herkunft:  gamedev.tv, "Open World Terrain Pack" (Terrain Layer Textures)
Lizenz:    GameDev.tv General Asset License 2.0

Sinngemaess erlaubt die Lizenz: Nutzung im (auch kommerziellen) Spiel, Veraendern der Dateien, Verkauf des fertigen Spiels.
Sinngemaess verboten: Weitergabe als einzelne Dateien (auch veraendert oder verkleinert), Einbau in etwas, woraus andere sie leicht
herausholen koennen, und sie als eigenes Werk auszugeben.

Darum liegen diese Dateien nicht im oeffentlichen Repository (Reichsgilde), nicht in Patches und nicht in Bildern des Repositorys.
Sie kommen nur ueber tools/assets/privat_holen.py nach game/assets/privat/ (von Git ignoriert) und sind Teil des fertigen Spiels.
Massgeblich ist der Lizenztext des Herstellers; diese Datei ist nur ein Merkzettel.
"""

# Satz -> (Ordner, Dateistamm; Groß-/Kleinschreibung egal)
SAETZE = {
    "rock_1": ("Rock 1", "Rock_1"),
    "rock_2": ("Rock 2", "Rock_2"),
    "ground_1": ("Ground 1", "Ground"),
    "soil_1": ("Soil 1", "Soil_1"),
    "soil_2": ("Soil 2", "Soil_2"),
    "grasses_1": ("Grasses 1", "Grass"),
    "snow_1": ("Snow_1", "Snow_1"),
}
# Schicht im Boden-Atlas (Reihenfolge wie Bodentextur im Kern) -> Satz
ATLAS_TAUSCH = {1: "soil_1", 2: "rock_1", 3: "ground_1", 6: "soil_2"}
SCHICHTEN = 11


def finde(ordner, stamm, art):
    """Die Datei einer Karte: <stamm>_<art>.png, nicht die 'a'-Varianten der Albedo."""
    for name in sorted(os.listdir(ordner)):
        n = name.lower()
        if n.endswith(".png") and n == f"{stamm.lower()}_{art}.png":
            return os.path.join(ordner, name)
    for name in sorted(os.listdir(ordner)):  # Grasses: "Grass 1_basecolor.png"
        n = name.lower()
        if n.endswith(f"_{art}.png") and "a_" not in n and n.startswith(stamm.lower()):
            return os.path.join(ordner, name)
    raise FileNotFoundError(f"{stamm}_{art} in {ordner}")


def laden(pfad, groesse, kanaele):
    """PNG -> float32 0..1 (H, W, kanaele); 16 Bit und 8 Bit; verkleinert mit Lanczos (ganze Zahl des Maßstabs, Rand umschlungen genügt hier nicht nötig)."""
    bild = Image.open(pfad)
    if bild.mode.startswith("I"):
        feld = np.asarray(bild, dtype=np.float32) / 65535.0  # 16 Bit Grau (I;16 oder I)
        if feld.shape[0] != groesse:
            feld = np.asarray(Image.fromarray(feld, "F").resize((groesse, groesse), Image.LANCZOS))
        return feld[..., None] if kanaele == 1 else np.repeat(feld[..., None], kanaele, axis=2)
    if kanaele == 1:
        bild = bild.convert("L")
    elif kanaele == 3:
        bild = bild.convert("RGB")
    else:
        bild = bild.convert("RGBA")
    if bild.size[0] != groesse:
        bild = bild.resize((groesse, groesse), Image.LANCZOS)
    a = np.asarray(bild, dtype=np.float32) / 255.0
    return a[..., None] if kanaele == 1 else a


def speichere(feld, pfad, **opt):
    os.makedirs(os.path.dirname(pfad), exist_ok=True)
    a = np.clip(np.round(feld * 255.0), 0, 255).astype(np.uint8)
    if a.shape[2] == 1:
        a = a[..., 0]
    Image.fromarray(a).save(pfad, **opt)


def nahtlos(a):
    """Verhältnis des Sprungs über den Rand zum mittleren Sprung zwischen Nachbarpunkten (1 = nahtlos), je Achse; Grau."""
    g = a.mean(axis=2) if a.ndim == 3 else a
    r = []
    for achse in (0, 1):
        innen = np.abs(np.diff(g, axis=achse)).mean()
        rand = np.abs(np.take(g, 0, axis=achse) - np.take(g, -1, axis=achse)).mean()
        r.append(rand / max(innen, 1e-6))
    return tuple(r)


def normale_richtung(normal, hoehe):
    """+1: G der Normale folgt dem Anstieg der Höhe nach unten im Bild (OpenGL, Y+ nach oben); -1: DirectX (G umdrehen)."""
    h = hoehe[..., 0]
    gz = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) / 2.0  # Anstieg zeilenweise nach unten
    gx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) / 2.0
    ny = normal[..., 1] * 2 - 1
    nx = normal[..., 0] * 2 - 1
    ky = float(np.corrcoef(ny.ravel(), gz.ravel())[0, 1])
    kx = float(np.corrcoef(nx.ravel(), (-gx).ravel())[0, 1])
    return kx, ky


def normale_ok(n):
    """Normale 0..1 -> xy -1..1, z neu gerechnet, Länge 1 (nach dem Verkleinern)."""
    xy = n[..., :2] * 2 - 1
    z = np.sqrt(np.clip(1 - (xy ** 2).sum(axis=2), 0, 1))
    l = np.sqrt((xy ** 2).sum(axis=2) + z ** 2)
    return xy / l[..., None], z / l


def satz_bauen(quelle, ziel, name, ordner, stamm):
    d = os.path.join(quelle, ordner)
    pfad = os.path.join(ziel, "sets", name)
    marke = os.path.join(pfad, "albedo.webp")
    if os.path.exists(marke):
        print(f"  {name}: schon vorhanden (2048)")
        return
    print(f"  {name}: lade …")
    alb = laden(finde(d, stamm, "basecolor"), 2048, 3)
    nor = laden(finde(d, stamm, "normal"), 2048, 3)
    rau = laden(finde(d, stamm, "roughness"), 2048, 1)
    ao = laden(finde(d, stamm, "ambientocclusion"), 2048, 1)
    hoe = laden(finde(d, stamm, "height"), 2048, 1)
    speichere(alb, os.path.join(pfad, "albedo.webp"), lossless=True)
    speichere(nor, os.path.join(pfad, "normal_opengl.webp"), lossless=True)
    speichere(rau, os.path.join(pfad, "rauheit.webp"), lossless=True)
    speichere(ao, os.path.join(pfad, "ao.webp"), lossless=True)
    speichere(hoe, os.path.join(pfad, "hoehe.webp"), lossless=True)


def satz_lesen(ziel, name, groesse):
    pfad = os.path.join(ziel, "sets", name)
    kart = {}
    for art, k in (("albedo", 3), ("normal_opengl", 3), ("rauheit", 1), ("ao", 1), ("hoehe", 1)):
        bild = Image.open(os.path.join(pfad, art + ".webp")).convert("RGB" if k == 3 else "L")
        if bild.size[0] != groesse:
            bild = bild.resize((groesse, groesse), Image.LANCZOS)
        a = np.asarray(bild, dtype=np.float32) / 255.0
        kart[art] = a if k == 3 else a[..., None]
    return kart


def hoehe_strecken(h):
    lo, hi = np.percentile(h, 1), np.percentile(h, 99)
    return np.clip((h - lo) / max(hi - lo, 1e-6), 0, 1)


def luma(rgb):
    return float((rgb[..., 0] * 0.3 + rgb[..., 1] * 0.55 + rgb[..., 2] * 0.15).mean())


def normal_xy_gl(kart):
    """Normale mit OpenGL-Richtung (G+ nach oben im Bild); dreht G um, wenn die Quelle DirectX ist (Prüfung gegen die Höhe)."""
    kx, ky = normale_richtung(kart["normal_opengl"], kart["hoehe"])
    gedreht = ky < 0
    n = kart["normal_opengl"].copy()
    if gedreht:
        n[..., 1] = 1 - n[..., 1]
    return n, (kx, ky, gedreht)


def auf_groesse(a, g):
    if a.shape[0] == g:
        return a
    k = a.shape[2]
    out = []
    for i in range(k):
        out.append(np.asarray(Image.fromarray(a[..., i], "F").resize((g, g), Image.LANCZOS)))
    return np.stack(out, axis=2)


def spiel_bauen(ziel, repo, bericht):
    g = 1024
    s = {n: satz_lesen(ziel, n, g) for n in SAETZE}
    # --- Boden-Atlas ---
    fh_alt = Image.open(os.path.join(repo, "game/assets/texturen/boden/boden_farbe_hoehe.webp")).convert("RGBA")
    nr_alt = Image.open(os.path.join(repo, "game/assets/texturen/boden/boden_normal_rauheit.webp")).convert("RGB")
    fh = np.asarray(fh_alt, dtype=np.float32) / 255.0
    nr = np.asarray(nr_alt, dtype=np.float32) / 255.0
    assert fh.shape[0] == g * SCHICHTEN, fh.shape
    for nr_s, name in ATLAS_TAUSCH.items():
        k = s[name]
        z0, z1 = nr_s * g, (nr_s + 1) * g
        alt = fh[z0:z1, :, :3]
        neu = k["albedo"] * (0.6 + 0.4 * k["ao"])  # AO mild eingerechnet, wie im Atlas des Spiels
        gain = float(np.clip(luma(alt) / max(luma(neu), 1e-6), 0.5, 1.5))
        neu = np.clip(neu * gain, 0, 1)
        bericht.append(f"Atlas Schicht {nr_s} <- {name}: Helligkeit alt {luma(alt):.3f}, neu vorher {luma(k['albedo']):.3f}, Faktor {gain:.2f}")
        h = hoehe_strecken(k["hoehe"])
        fh[z0:z1, :, :3] = neu
        fh[z0:z1, :, 3:4] = h
        n, _ = normal_xy_gl(k)
        xy, z = normale_ok(n)
        nr[z0:z1, :, 0] = xy[..., 0] * 0.5 + 0.5
        nr[z0:z1, :, 1] = xy[..., 1] * 0.5 + 0.5
        nr[z0:z1, :, 2] = k["rauheit"][..., 0]
    os.makedirs(os.path.join(ziel, "spiel", "boden"), exist_ok=True)
    speichere(fh, os.path.join(ziel, "spiel/boden/boden_farbe_hoehe.webp"), lossless=False, quality=95)
    speichere(nr, os.path.join(ziel, "spiel/boden/boden_normal_rauheit.webp"), lossless=True)
    # --- Fels-Struktur (Rock 2): R = Neigung in U, G = Neigung in V (Zeilen nach unten) = -ny(OpenGL), B Höhe, A Flecken ---
    k = s["rock_2"]
    n, _ = normal_xy_gl(k)
    xy, z = normale_ok(n)
    # gleich stark wie die bisherige Struktur (Standardabweichung der Neigung), damit Kante und Relief im Shader gleich wirken
    alt = np.asarray(Image.open(os.path.join(repo, "game/assets/texturen/fels/fels_struktur.png")).convert("RGBA"), dtype=np.float32)
    s_alt = float(((alt[..., :2] - 128.0) / 127.0).std())
    su, sv = xy[..., 0], -xy[..., 1]
    s_neu = float(np.sqrt((su ** 2 + sv ** 2).mean() / 2))
    f = s_alt / max(s_neu, 1e-6)
    bericht.append(f"Fels-Struktur: Neigung alt {s_alt:.3f}, neu {s_neu:.3f}, Faktor {f:.2f}")
    su, sv = np.clip(su * f, -1, 1), np.clip(sv * f, -1, 1)
    h = hoehe_strecken(k["hoehe"])
    lum = (k["albedo"] * np.array([0.3, 0.55, 0.15])).sum(axis=2)
    fleck = hoehe_strecken(lum[..., None] - 0.0)  # helle Flechten und dunkle Stellen als Flecken, 0..1
    # grobe Flecken statt feinem Korn: weichzeichnen (Wrap) über etwa 8 Bildpunkte
    fl = fleck[..., 0]
    fk = np.fft.fft2(fl)
    fy = np.fft.fftfreq(g)[:, None]
    fx = np.fft.fftfreq(g)[None, :]
    fl = np.real(np.fft.ifft2(fk * np.exp(-(fx ** 2 + fy ** 2) * (2 * np.pi ** 2) * 8.0 ** 2)))
    fl = hoehe_strecken(fl)
    kod = lambda w: np.clip(np.round(128 + 127 * w), 1, 255) / 255.0
    st = np.dstack([kod(su), kod(sv), h[..., 0], fl])
    os.makedirs(os.path.join(ziel, "spiel", "fels"), exist_ok=True)
    speichere(st, os.path.join(ziel, "spiel/fels/fels_struktur.png"))
    # --- Schnee (Snow_1) ---
    k = s["snow_1"]
    neu = k["albedo"] * (0.7 + 0.3 * k["ao"])
    os.makedirs(os.path.join(ziel, "spiel", "schnee"), exist_ok=True)
    speichere(np.dstack([neu, hoehe_strecken(k["hoehe"])]), os.path.join(ziel, "spiel/schnee/schnee_farbe_hoehe.webp"), lossless=True)
    n, _ = normal_xy_gl(k)
    xy, z = normale_ok(n)
    speichere(np.dstack([xy[..., 0] * 0.5 + 0.5, xy[..., 1] * 0.5 + 0.5, np.clip(k["rauheit"][..., 0], 0.6, 1.0)]), os.path.join(ziel, "spiel/schnee/schnee_normal_rauheit.webp"), lossless=True)
    bericht.append(f"Schnee: mittlere Albedo {luma(k['albedo']):.3f}, Rauheit {float(k['rauheit'].mean()):.2f}")


def pruefen(ziel, bericht):
    bericht.append("Nahtlosigkeit (Rand/innen je Achse, 1 = nahtlos; unter 2 gilt als unauffällig) und Normalen (Korrelation x, y; y > 0 = OpenGL):")
    for name in SAETZE:
        k = satz_lesen(ziel, name, 1024)
        nx, ny = nahtlos(k["albedo"])
        hx, hy = nahtlos(k["hoehe"])
        kx, ky = normale_richtung(k["normal_opengl"], k["hoehe"])
        bericht.append(f"  {name:10s} Albedo {nx:.2f}/{ny:.2f}  Höhe {hx:.2f}/{hy:.2f}  Normale x {kx:+.2f} y {ky:+.2f}")


def sha(pfad):
    h = hashlib.sha256()
    with open(pfad, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def manifest_schreiben(spiel, ziel_manifest):
    eintraege = []
    for wurzel, _, dateien in os.walk(spiel):
        for d in sorted(dateien):
            p = os.path.join(wurzel, d)
            rel = os.path.relpath(p, spiel).replace(os.sep, "/")
            eintraege.append({"datei": rel, "groesse": os.path.getsize(p), "sha256": sha(p), "lizenz": LIZENZ})
    eintraege.sort(key=lambda e: e["datei"])
    daten = {"version": 1, "ziel": "game/assets/privat", "quelle": QUELLE, "lizenz": LIZENZ, "dateien": eintraege}
    with open(ziel_manifest, "w", encoding="utf-8", newline="\n") as f:
        json.dump(daten, f, ensure_ascii=False, indent=2)
        f.write("\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--quelle", required=True)
    ap.add_argument("--ziel", required=True)
    ap.add_argument("--repo", default=os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")))
    ap.add_argument("--manifest", default=None)
    ap.add_argument("--ohne-manifest", action="store_true")
    ap.add_argument("--nur-manifest", action="store_true", help="nur das Manifest neu schreiben (nach dem Import: die .import-Dateien liegen dann in spiel/)")
    a = ap.parse_args()
    bericht = []
    if a.nur_manifest:
        manifest_schreiben(os.path.join(a.ziel, "spiel"), a.manifest or os.path.join(a.repo, "docs/assets/privat_manifest.json"))
        return 0
    print("Stufe 1: Sätze (2048)")
    for name, (ordner, stamm) in SAETZE.items():
        satz_bauen(a.quelle, a.ziel, name, ordner, stamm)
    pruefen(a.ziel, bericht)
    print("Stufe 2: Dateien des Spiels (1024)")
    spiel_bauen(a.ziel, a.repo, bericht)
    with open(os.path.join(a.ziel, "spiel", "LIZENZ.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(LIZENZTEXT)
    with open(os.path.join(a.ziel, "LIZENZ.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(LIZENZTEXT)
    if not a.ohne_manifest:
        manifest_schreiben(os.path.join(a.ziel, "spiel"), a.manifest or os.path.join(a.repo, "docs/assets/privat_manifest.json"))
    print("\n".join(bericht))


if __name__ == "__main__":
    sys.exit(main())
