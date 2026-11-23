"""Trim-Sheets der Zwergenhaeuser Stufe 2 (Buergerhaus) und 3 (Steinhaus): abgeleitet aus den Blaettern der Zwerge Stufe 1.

    blender -b --python tools/modelle/neubau/trimsheet_zwerg_stufen.py -- --quellen=<ordner>

(Entwurf, ohne Basis nur auf Syntax geprueft: baut auf trimsheet_stufen.py der Menschen Stufe 2/3 auf, das erst mit dem Mensch-PR da ist.)
Ein Aufruf, nachdem trimsheet_zwerg.py `trim_zwerg_*` und `innen_trim_zwerg_*` geschrieben hat. Ersetzt wie trimsheet_stufen.py nur
die Baender, die bei den reicheren Haeusern anders aussehen; die Bandgrenzen sind dieselben:

    trim_s2_zwerg_*        aussen Buergerhaus: Baender "stroh"/"stroh_franse" = Schiefer (Kunstwerk Zwerg Stufe 2: graues Schieferdach)
    trim_s3_zwerg_*        aussen Steinhaus:   Schiefer, dazu "bruchstein" = Sandstein (Kunstwerk Stufe 3: Gewaende, Gesimse, Treppengiebel
                           aus hellbraunem Haustein auf dunklem Mauerwerk; "putz" bleibt der dunkle Quader der Zwerge)
    innen_trim_s23_zwerg_* innen: Steinwand und Pflaster der Zwerge bleiben; Bodenfeld rechts = Schachbrett aus dunklem und hellem Stein
Quellen (Poly Haven CC0, Abruf wie Stufe 1): roof_slates_02, sandstone_blocks_08, large_floor_tiles_02. Keine Bild-KI.
"""
import os
import sys

import bpy
import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import trimsheet as ts                                                       # noqa: E402
from trimsheet import BAENDER_AUSSEN, BAENDER_INNEN, N, Quelle, faerbe        # noqa: E402
import trimsheet_stufen as st                                                # noqa: E402  (lade_blatt, dach_band, FLIESEN_U)


SCHIEFER_HELL = 0.33      # mittlerer Wert der Schieferbaender in der gespeicherten PNG (sRGB rund 85 von 255; speichere() legt den hier
                          # „linear“ gerechneten Wert als sRGB-Byte ab): vorher 0,11 (sRGB 27) – im Spiel schwarz (Klicktest 16.11.2026),
                          # in Blender nur dunkelgrau; Kunstwerke Stufe 2 und 3: mittelgrauer Schiefer


ZIEGEL_TON = (0.56, 0.30, 0.22)       # Tonziegel der Zwerge Stufe 3 (Menschen Stufe 2: 0,52 / 0,33 / 0,26), etwas roetlicher
ZIEGEL_HELL_ZWERG = 0.24              # mittlere Helligkeit der Dachbaender (Menschen 0,20)


def schiefer_aufhellen(blatt):
    """Hebt die Dachbaender („stroh“, „stroh_franse“) linear auf SCHIEFER_HELL; Zeichnung und Verlauf bleiben."""
    from trimsheet import srgb_zu_linear, linear_zu_srgb
    for name in ("stroh", "stroh_franse"):
        a, e = blatt.zeilen(BAENDER_AUSSEN[name])
        lin = srgb_zu_linear(blatt.albedo[a:e, :, :3])
        hell = float((lin @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)).mean())
        blatt.albedo[a:e, :, :3] = linear_zu_srgb(np.clip(lin * (SCHIEFER_HELL / max(hell, 1e-4)), 0, 1))
        print(f"Schiefer {name}: Helligkeit {hell:.4f} -> {SCHIEFER_HELL}")


def baue(quellen, nur=None):
    """`nur="s3"`: nur trim_s3_zwerg neu schreiben (Stufe 2 und die Innenblaetter bleiben unberuehrt)."""
    schiefer = Quelle(quellen, "roof_slates_02", kante=1024)
    if nur != "s3":
        s2 = st.lade_blatt("trim_zwerg")
        st.dach_band(s2, schiefer, (0.36, 0.37, 0.40), kontrast=1.2, eigenfarbe=0.08)
        schiefer_aufhellen(s2)
        s2.speichere("trim_s2_zwerg")
    s3 = st.lade_blatt("trim_zwerg")
    # Dach Stufe 3 (Nutzer 23.11.2026): gebrannte Tonziegel statt Schiefer (Stufe 1 braeunlich, Stufe 2 blau bleiben)
    st.dach_band(s3, st.Quelle1k(quellen, "clay_roof_tiles_02", kante=1024, aufloesung="2k"), ZIEGEL_TON, eigenfarbe=0.2)
    st.dach_aufhellen(s3, ZIEGEL_HELL_ZWERG)
    # Werkstein (Eckquader, Gewaende, Gesimse, Giebel): Quaderlagen mit tiefen Fugen und Fase wie bei den Menschen, in der braunen Farbe der
    # Zwerge (Nutzer 23.11.2026: braune Steine ohne sichtbare Fugen, im Pixel-Look nicht lesbar): breite Fugen, starke Fase
    stein = Quelle(quellen, "sandstone_blocks_08", kante=1024)
    st.quaderband(s3, BAENDER_AUSSEN["bruchstein"], stein, (0.60, 0.52, 0.42), saat=47, ab=210, fuge_px=22, fuge_x_px=22, fase_px=12,
                  streuung=0.10, kontrast=0.8, eigenfarbe=0.2)
    s3.speichere("trim_s3_zwerg")
    if nur == "s3": return
    # Innen: Schachbrettfeld rechts im Band "lehmboden" (links bleibt das Pflaster der Zwerge)
    i = st.lade_blatt("innen_trim_zwerg")
    fliesen = Quelle(quellen, "large_floor_tiles_02", kante=1024)
    a, e = i.zeilen(BAENDER_INNEN["lehmboden"])
    h = e - a
    f, n, arm = fliesen.streifen(h, ab=40)
    kachel_x, kachel_y = int(round(st.FLIESEN_U * N)) / 6, h / 2
    xx = np.arange(N)[None, :] % (N // 2)
    yy = np.arange(h)[:, None]
    kx = np.minimum(xx // kachel_x, 5).astype(int); ky = np.minimum(yy // kachel_y, 1).astype(int)
    fuge = (np.minimum(xx % kachel_x, kachel_x - xx % kachel_x) < 3) | (np.minimum(yy % kachel_y, kachel_y - yy % kachel_y) < 3)
    rechts = np.arange(N)[None, :] >= N // 2
    hell = faerbe(f, (0.62, 0.60, 0.56), kontrast=0.9, eigenfarbe=0.15)
    dunkel = faerbe(f, (0.22, 0.22, 0.23), kontrast=0.9, eigenfarbe=0.15)
    schach = np.where(((kx + ky) % 2 == 0)[..., None], hell, dunkel)
    alt = i.albedo[a:e, :, :3].copy()
    farbe = np.where(rechts[..., None], schach, alt)
    farbe = np.where((fuge & rechts)[..., None], farbe * 0.45, farbe)
    i.setze(BAENDER_INNEN["lehmboden"], np.clip(farbe, 0, 1), n, arm)
    i.speichere("innen_trim_s23_zwerg")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ts.ZIEL = ts.ZIEL
    baue(opt["quellen"], opt.get("nur"))
