"""Trim-Sheets fuer die Wohnhaeuser Mensch der Stufen 2 (Buergerhaus) und 3 (Steinhaus): abgeleitet aus den Blaettern der Stufe 1.

    blender -b --python tools/modelle/neubau/trimsheet_stufen.py -- --quellen=<ordner>

Ein Aufruf, nachdem trimsheet.py die Blaetter der Stufe 1 (trim_*, innen_trim_*) geschrieben hat: Dieses Skript liest sie und
ersetzt nur die Baender, die bei den reicheren Haeusern anders aussehen. Alle anderen Baender (Balken, Putz, Planken, Eisen,
Moebelholz, Kleinteile) bleiben Pixel fuer Pixel die der Stufe 1 - wird trimsheet.py geaendert (z. B. Rauheit oder Normale), genuegt
es, danach dieses Skript noch einmal laufen zu lassen. Die Bandgrenzen sind dieselben (trimsheet.BAENDER_AUSSEN / BAENDER_INNEN),
darum passen die Bauteile der Stufe 1 (Moebel, Gegenstaende, Fachwerk) unveraendert.

    trim_s2_*        aussen Buergerhaus: Baender "stroh" und "stroh_franse" = Tonziegel (Biberschwanz, ohne Alpha)
    trim_s3_*        aussen Steinhaus:   wie trim_s2, aber Schiefer statt Ziegel und behauener Stein ("bruchstein") statt Bruchstein
    innen_trim_s23_* innen beide Stufen:  "lehmwand" = Kalkputz (weiss), "lehmboden" = links Tonfliesen, rechts Schachbrett

Quellen (Poly Haven, CC0; Herkunft in game/assets/texturen/neubau/HERKUNFT.md): clay_roof_tiles_02 (1K), roof_slates_02 (2K),
sandstone_blocks_08 (2K), white_plaster_rough_01 (2K), large_floor_tiles_02 (2K). Keine Bild-KI.
"""
import os
import sys

import bpy
import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import trimsheet as ts                                                                      # noqa: E402
from trimsheet import BAENDER_AUSSEN, BAENDER_INNEN, N, ZIEL, Blatt, Quelle, faerbe, skaliere, lade, drehe  # noqa: E402


class Quelle1k(Quelle):
    """Wie trimsheet.Quelle, aber aus den 1K-Dateien (von manchen Texturen liegt nur diese Groesse vor)."""

    def __init__(self, ordner, name, kante=1024, gedreht=False, aufloesung="1k"):
        o = os.path.join(ordner, name)
        self.farbe = skaliere(lade(os.path.join(o, f"{name}_diff_{aufloesung}.jpg")), kante, kante)
        self.normale = skaliere(lade(os.path.join(o, f"{name}_nor_gl_{aufloesung}.jpg"), daten=True), kante, kante)
        self.arm = skaliere(lade(os.path.join(o, f"{name}_arm_{aufloesung}.jpg"), daten=True), kante, kante)
        if gedreht:
            self.farbe, self.normale, self.arm = drehe(self.farbe, self.normale, self.arm)
        self.kante = kante


class SteinflaechenQuelle:
    """Glatte Steinflaeche ohne Fugen aus dem Inneren eines Quaders der Quelle `q` (Zeilen r0..r1, Spalten c0..c1 der Kantenlaenge
    von q): das Stueck wird gespiegelt gekachelt (Normale: beim Spiegeln das Vorzeichen der Kippung drehen), damit die Fugen der
    Quelltextur nicht im Band stehen - die Fugen setzt quaderband selbst, im Raster des Bauskripts."""

    def __init__(self, q, r0, r1, c0, c1):
        self.kante = r1 - r0
        self.farbe = q.farbe[r0:r1, c0:c1]
        self.normale = q.normale[r0:r1, c0:c1]
        self.arm = q.arm[r0:r1, c0:c1]

    def streifen(self, hoehe, ab=0, versatz=0):
        def kachel(f, normal):
            h, b = f.shape[:2]
            zeilen = (ab + np.arange(hoehe)); spalten = (versatz + np.arange(N))
            zk, sk = zeilen // h, spalten // b                                       # Kachelnummer
            zr, sr = zeilen % h, spalten % b
            zr = np.where(zk % 2 == 1, h - 1 - zr, zr); sr = np.where(sk % 2 == 1, b - 1 - sr, sr)   # jede zweite Kachel gespiegelt
            out = f[zr][:, sr].copy()
            if normal:                                                              # R = x-Kippung (um 0,5), G = y-Kippung
                out[..., 0] = np.where((sk % 2 == 1)[None, :], 1.0 - out[..., 0], out[..., 0])
                out[..., 1] = np.where((zk % 2 == 1)[:, None], 1.0 - out[..., 1], out[..., 1])
            return out
        return kachel(self.farbe, False), kachel(self.normale, True), kachel(self.arm, False)


def lade_blatt(name):
    """Liest ein fertiges Blatt (albedo, normal, orm) in ein trimsheet.Blatt."""
    b = Blatt()
    for art in ("albedo", "normal", "orm"):
        bild = bpy.data.images.load(os.path.join(ZIEL, f"{name}_{art}.png"))
        if art != "albedo":
            bild.colorspace_settings.name = "Non-Color"
        w, h = bild.size
        feld = np.empty(w * h * 4, dtype=np.float32)
        bild.pixels.foreach_get(feld)
        feld = feld.reshape(h, w, 4)[::-1].copy()
        bpy.data.images.remove(bild)
        if art == "albedo":
            b.albedo[..., :3] = ts.linear_zu_srgb(feld[..., :3]); b.albedo[..., 3] = feld[..., 3]
        elif art == "normal":
            b.normal[...] = feld[..., :3]
        else:
            b.orm[...] = feld[..., :3]
    return b


def dach_band(blatt, q, ton, kontrast=1.1, eigenfarbe=0.35, ab=0):
    """Dachdeckung. Band "stroh_franse" (0,25 m hoch): eine Lage Ziegel bzw. Schiefer - die Dachskripte legen die Dachflaeche in
    Lagen von 0,25 m mit kleiner Stufe, jede Lage fuellt das Band (oben Schatten der naechsten Lage). Band "stroh": dieselbe Deckung
    in groesserem Ausschnitt (Gaubendaecher). Der Shader zeichnet im Band "stroh" im Pixel-Look Halmstriche, darum liegen die
    grossen Dachflaechen im Fransenband."""
    for name, versatz in (("stroh", 0), ("stroh_franse", 300)):
        band = BAENDER_AUSSEN[name]
        a, e = blatt.zeilen(band)
        f, n, arm = q.streifen(e - a, ab=ab + versatz, versatz=versatz)
        blatt.setze(band, faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe), n, arm)
        blatt.albedo[a:e, :, 3] = 1.0                       # keine Fransen: Ziegel und Schiefer brauchen keine Alpha-Cards
    blatt.verlauf(BAENDER_AUSSEN["stroh_franse"], 0.62, 1.0, 34)


# Helligkeit der Dachbaender (Mensch-Nachtrag 16.11.2026, wie trimsheet_zwerg_stufen.SCHIEFER_HELL): mittlere Leuchtdichte der Baender
# „stroh“ und „stroh_franse“ in der gespeicherten PNG (Werte 0..1 der sRGB-Bytes). Vorher Schiefer 0,12 (sRGB rund 31, linear 0,014:
# „Schieferdaecher Mensch S3 tags fast schwarz“) und Ziegel 0,08 (Farbe sRGB 49/14/8, linear 0,010: dunkler als der Schiefer).
SCHIEFER_HELL = 0.33      # wie bei den Zwergen: sRGB rund 85, sichtbar dunkelgrauer Schiefer mit Struktur
ZIEGEL_HELL = 0.20        # Tonziegel: sRGB rund 101/39/24 (dunkles Ziegelrot statt fast schwarz)


def dach_aufhellen(blatt, ziel):
    """Hebt die Dachbaender („stroh“, „stroh_franse“) gleichmaessig auf die mittlere Helligkeit `ziel`; Zeichnung, Farbton und Verlauf
    bleiben (alle Kanaele mit demselben Faktor)."""
    for name in ("stroh", "stroh_franse"):
        a, e = blatt.zeilen(BAENDER_AUSSEN[name])
        lin = ts.srgb_zu_linear(blatt.albedo[a:e, :, :3])
        hell = float((lin @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)).mean())
        blatt.albedo[a:e, :, :3] = ts.linear_zu_srgb(np.clip(lin * (ziel / max(hell, 1e-4)), 0, 1))
        print(f"Dach {name}: Helligkeit {hell:.4f} -> {ziel}")


def quader_plan(breite_px, hoehe_px, px_m, saat):
    """Quaderverband eines Mauerstreifens (eine Lage des Bandes, Hoehe = Bandhoehe): Liste der Steine (x0, x1, y0, y1) in Pixeln,
    waagerecht nahtlos (x laeuft ueber den Rand und wird beim Malen umgebrochen). Abschnitte mit einem hohen Stein ueber die ganze
    Lage wechseln mit Abschnitten aus zwei Schichten verschiedener Hoehe, deren Stossfugen gegeneinander versetzt sind - so ergeben
    die gestapelten Lagen (die Wand verschiebt jede Lage waagerecht, u_start) einen unregelmaessigen Verband."""
    z = np.random.default_rng(saat)
    steine = []
    x = 0
    while x < breite_px - 1:
        if z.random() < 0.45:                                       # ein hoher Stein ueber die ganze Lage
            w = int(z.uniform(0.38, 0.62) * px_m)
            w = min(w, breite_px - x)
            steine.append((x, x + w, 0, hoehe_px))
        else:                                                       # zwei Schichten, Teilung zwischen 40 und 60 %
            w = min(int(z.uniform(0.9, 1.7) * px_m), breite_px - x)
            teil = int(hoehe_px * z.uniform(0.4, 0.6))
            for y0, y1 in ((0, teil), (teil, hoehe_px)):
                a = x
                while a < x + w - 1:
                    b = min(a + int(z.uniform(0.28, 0.66) * px_m), x + w)
                    if x + w - b < 0.2 * px_m: b = x + w             # kein Splitter am Ende des Abschnitts
                    steine.append((a, b, y0, y1)); a = b
        x += w
    return steine


def quader_reihe(breite_px, hoehe_px, px_m, saat, breite_m=(0.55, 1.1)):
    """Eine einzige Quaderreihe ueber die ganze Bandhoehe (Werksteinband des Steinhauses): gleich hohe Quader unterschiedlicher
    Breite (breite_m in Metern bei `px_m` Bildpunkten je Meter), waagerecht nahtlos. Die Mauer legt Reihe auf Reihe (u_start
    versetzt jede Lage), Gewaende und Pfeiler zeigen die Reihe als ein Stein mit gelegentlicher Stossfuge."""
    z = np.random.default_rng(saat)
    steine, x = [], 0
    while x < breite_px - 1:
        w = int(z.uniform(*breite_m) * px_m)
        if breite_px - (x + w) < 0.4 * px_m: w = breite_px - x          # kein Splitter am Ende der Reihe
        w = min(w, breite_px - x)
        steine.append((x, x + w, 0, hoehe_px)); x += w
    return steine


def quaderband(blatt, band, q, ton, saat, ab=0, px_m=512, fuge_px=12, fase_px=9, streuung=0.08, kontrast=0.45, eigenfarbe=0.08,
               plan=None, fuge_x_px=None, warm_streuung=0.025, fuge_dunkel=0.55):
    """Behauene Steinquader im Band: Oberflaeche aus der Quelltextur (umgefaerbt), je Stein eigener Ton (Helligkeit und leichte
    Waerme), dunkle, vertiefte Fugen (12 px = 2,3 cm: bleiben im Pixel-Look - Mip 2,5 Stufen groeber - als Linie lesbar) und eine
    Fase an jeder Steinkante in der Normale (Licht oben und links, Schatten unten und rechts wie bei echter Tiefe).
    `plan`: fertige Steinliste (x0, x1, y0, y1) statt quader_plan; `fuge_x_px`: Breite der senkrechten Fugen, falls abweichend
    (das Band kann waagerecht und senkrecht verschieden gedehnt sein, siehe Werksteinband)."""
    a, e = blatt.zeilen(band)
    h = e - a
    f, n, arm = q.streifen(h, ab=ab)
    farbe = faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe)
    z = np.random.default_rng(saat + 1)
    stein_id = np.full((h, N), -1, dtype=np.int32)
    rand = np.full((h, N), 1e9, dtype=np.float32)                   # Abstand zur naechsten Steinkante (Pixel)
    nx = np.zeros((h, N), dtype=np.float32); ny = np.zeros((h, N), dtype=np.float32)
    if plan is None: plan = quader_plan(N, h, px_m, saat)
    halb = fuge_px / 2
    halb_x = (fuge_x_px if fuge_x_px is not None else fuge_px) / 2
    for i, (x0, x1, y0, y1) in enumerate(plan):
        ys = np.arange(y0, y1)
        xs = np.arange(x0, x1)
        dy0, dy1 = ys - y0 + 0.5, y1 - ys - 0.5                     # Abstand zur oberen / unteren Kante
        dx0, dx1 = xs - x0 + 0.5, x1 - xs - 0.5
        # Fuge: halbe Fugenbreite an jeder Kante (oben/unten am Bandrand ebenso: zwei Lagen ergeben eine ganze Fuge)
        innen_y = np.minimum(dy0, dy1) - halb; innen_x = np.minimum(dx0, dx1) - halb_x
        d = np.minimum(innen_y[:, None], innen_x[None, :])
        cols = xs % N
        stein_id[y0:y1][:, cols] = i
        rand[y0:y1][:, cols] = d
        # Fase: Normale kippt zur naechsten Kante hin (oben +y, unten -y, links -x, rechts +x)
        k = lambda t: np.clip(1 - t / fase_px, 0, 1) * (t >= 0)
        ty0, ty1 = (dy0 - halb)[:, None], (dy1 - halb)[:, None]
        tx0, tx1 = (dx0 - halb_x)[None, :], (dx1 - halb_x)[None, :]
        ny[y0:y1][:, cols] = 0.55 * (k(ty0) - k(ty1)) * (np.minimum(ty0, ty1) <= np.minimum(tx0, tx1))
        nx[y0:y1][:, cols] = 0.55 * (k(tx1) - k(tx0)) * (np.minimum(tx0, tx1) < np.minimum(ty0, ty1))
    # Ton je Stein: Helligkeit +- streuung, dazu waermer oder kuehler
    hell = z.uniform(1 - streuung, 1 + streuung, len(plan)).astype(np.float32)
    warm = z.uniform(-warm_streuung, warm_streuung, len(plan)).astype(np.float32)
    faktor = np.stack([hell * (1 + warm), hell, hell * (1 - warm)], axis=1)
    farbe = farbe * faktor[np.maximum(stein_id, 0)]
    fuge = rand < 0
    nah = np.clip(rand / fase_px, 0, 1)                             # Kante etwas dunkler (abgerundet, Schmutz in der Fase)
    farbe = farbe * (0.86 + 0.14 * nah)[..., None]
    farbe = np.where(fuge[..., None], farbe * fuge_dunkel, farbe)
    # Normale: Quelltextur halb so stark, dazu die Fasen; in der Fuge flach
    normale = 0.5 + (n - 0.5) * 0.5
    normale[..., 0] = np.clip(normale[..., 0] + nx * 0.5, 0, 1)
    normale[..., 1] = np.clip(normale[..., 1] + ny * 0.5, 0, 1)
    normale = np.where(fuge[..., None], np.array([0.5, 0.5, 1.0], dtype=np.float32), normale)
    arm = arm.copy()
    arm[..., 0] = np.where(fuge, arm[..., 0] * 0.5, arm[..., 0] * (0.8 + 0.2 * nah))
    arm[..., 1] = np.where(fuge, 0.95, arm[..., 1])
    blatt.setze(band, np.clip(farbe, 0, 1), normale, arm)
    return plan


def stein_flaeche(blatt, band, q, ton, ab=0, kontrast=1.0, eigenfarbe=0.3, fase_px=10):
    """Ein einzelner Werkstein ueber die ganze Bandhoehe (Eckquader, Gewaende, Abdeckplatten - die Steine sind echte Geometrie mit
    Fuge dazwischen): Oberflaeche aus der Quelltextur, Fase oben und unten in der Normale."""
    a, e = blatt.zeilen(band)
    f, n, arm = q.streifen(e - a, ab=ab)
    normale = 0.5 + (n - 0.5) * 0.6
    blatt.setze(band, faerbe(f, ton, kontrast=kontrast, eigenfarbe=eigenfarbe), normale, arm)
    blatt.fase(band, px=fase_px, staerke=0.5)


def baue(quellen, nur=None):
    """`nur="s3"`: nur das Blatt trim_s3 neu schreiben (die anderen Blaetter bleiben unberuehrt)."""
    # Aussen, Stufe 2: Tonziegel
    if nur in (None, "s2"):
        s2 = lade_blatt("trim")
        dach_band(s2, Quelle1k(quellen, "clay_roof_tiles_02", kante=1024), (0.52, 0.33, 0.26), eigenfarbe=0.2)
        dach_aufhellen(s2, ZIEGEL_HELL)
        s2.speichere("trim_s2")
    if nur == "s2": return
    # Aussen, Stufe 3: Schiefer und behauener Stein
    s3 = lade_blatt("trim")
    dach_band(s3, Quelle(quellen, "roof_slates_02", kante=1024), (0.38, 0.39, 0.42), kontrast=1.2, eigenfarbe=0.08)
    dach_aufhellen(s3, SCHIEFER_HELL)
    # Mauer: Quaderverband aus hellgrauem Haustein (wie die Vorlagen wohnhaus_lvl3_*), Band "bruchstein" = eine Lage von 0,40 m
    stein = Quelle(quellen, "sandstone_blocks_08", kante=1024)
    quaderband(s3, BAENDER_AUSSEN["bruchstein"], stein, (0.66, 0.65, 0.62), saat=31, ab=210)
    # Werkstein (Band "putz", im Steinhaus sonst ungenutzt): heller, grau-sandfarbener Haustein als EINE Quaderreihe ueber die
    # Bandhoehe (Nutzer 22.11.2026: Obergeschoss nicht ocker, nicht wie Putz, sichtbare Fugen). Das Band ist 8 m breit und 1,6 m
    # hoch; die Reihe ist 0,45 m hoch gedacht (Wand: hoehe=WERK_LAGE im Bauskript, Gewaende und Pfeiler zeigen sie als ganzen Stein).
    # Quader 0,55 bis 1,1 m breit, Fugen 2,2 cm; die Oberflaeche kommt aus dem Inneren eines Quaders von sandstone_blocks_08.
    # Der Shader toent das Band mit der Putzfarbe des Hauses: fuer das Steinhaus der Menschen gilt dort ein neutraler Steinton.
    k = stein.kante / 1024.0
    glatt = SteinflaechenQuelle(stein, int(225 * k), int(392 * k), int(367 * k), int(630 * k))
    a, e = s3.zeilen(BAENDER_AUSSEN["putz"])
    px_m = N / BAENDER_AUSSEN["putz"][2]
    reihe = quader_reihe(N, e - a, px_m, saat=47)
    quaderband(s3, BAENDER_AUSSEN["putz"], glatt, WERKSTEIN_TON, saat=47, plan=reihe, fuge_px=20, fuge_x_px=6, fase_px=8,
               streuung=0.06, kontrast=0.6, eigenfarbe=0.03, warm_streuung=0.01)
    s3.speichere("trim_s3")
    if nur == "s3": return
    # Innen, beide Stufen
    i = lade_blatt("innen_trim")
    putz = Quelle(quellen, "white_plaster_rough_01")
    a, e = i.zeilen(BAENDER_INNEN["lehmwand"])
    f, n, arm = putz.streifen(e - a, ab=420)
    i.setze(BAENDER_INNEN["lehmwand"], faerbe(f, (0.86, 0.83, 0.76), kontrast=0.7, eigenfarbe=0.1), n, arm)
    i.verlauf(BAENDER_INNEN["lehmwand"], 0.93, 0.85, 50)
    # Fliesenboden im Band "lehmboden" (0,6 m hoch): links Tonfliesen (UV.x 0..FLIESEN_U), rechts Schachbrett (0,5..0,5 + FLIESEN_U),
    # je 6 x 2 Fliesen von 0,30 m mit Fuge - die Bauskripte legen den Boden in Streifen von 0,6 x 1,8 m genau auf ein Feld.
    fliesen = Quelle(quellen, "large_floor_tiles_02", kante=1024)
    a, e = i.zeilen(BAENDER_INNEN["lehmboden"])
    h = e - a
    f, n, arm = fliesen.streifen(h, ab=40)
    breite = int(round(FLIESEN_U * N))
    kachel_x, kachel_y = breite / 6, h / 2
    xx = np.arange(N)[None, :] % (N // 2)
    yy = np.arange(h)[:, None]
    kx = np.minimum(xx // kachel_x, 5).astype(int); ky = np.minimum(yy // kachel_y, 1).astype(int)
    fuge = (np.minimum(xx % kachel_x, kachel_x - xx % kachel_x) < 3) | (np.minimum(yy % kachel_y, kachel_y - yy % kachel_y) < 3)
    rechts = (np.arange(N)[None, :] >= N // 2)
    ton = faerbe(f, (0.55, 0.31, 0.23), kontrast=1.0, eigenfarbe=0.3)
    hell = faerbe(f, (0.80, 0.78, 0.72), kontrast=0.9, eigenfarbe=0.15)
    dunkel = faerbe(f, (0.28, 0.28, 0.29), kontrast=0.9, eigenfarbe=0.15)
    schach = np.where(((kx + ky) % 2 == 0)[..., None], hell, dunkel)
    farbe = np.where(rechts[..., None], schach, ton)
    # Fliesen leicht verschieden im Ton, Fugen dunkel und tiefer
    zufall = np.random.default_rng(5).uniform(0.9, 1.08, (2, 6, 2))
    farbe = farbe * zufall[ky, kx, rechts.astype(int)][..., None]
    farbe = np.where(fuge[..., None], farbe * 0.45, farbe)
    arm = arm.copy(); arm[..., 0] = np.where(fuge, arm[..., 0] * 0.6, arm[..., 0])
    i.setze(BAENDER_INNEN["lehmboden"], np.clip(farbe, 0, 1), n, arm)
    i.speichere("innen_trim_s23")


WERK_LAGE = 0.45       # Hoehe einer Quaderlage im Werksteinband (m): die Wand des Patrizierhauses setzt das Band mit dieser Hoehe
# Grau-sandfarbener Haustein. ACHTUNG Trim-Sheet-Pipeline: Blatt.speichere schreibt srgb_zu_linear(Farbe) in die PNG-Bytes
# (Byte = Farbe hoch 2,2; trimsheet_ebenen.py und Godot lesen die Bytes unveraendert). Der Zielton im Spiel (Bytes etwa 176/170/156) ist
# darum hier als Farbe^(1/2,2) mit Zuschlag fuer Streuung und Fugen angegeben, nicht als der gewollte Ton selbst.
WERKSTEIN_TON = (0.87, 0.855, 0.825)
FLIESEN_U = 0.45      # Breite eines Fliesenfelds im Band "lehmboden" (UV.x), entspricht 1,8 m (Band: 4 m je UV.x 1)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    baue(opt["quellen"], opt.get("nur"))
