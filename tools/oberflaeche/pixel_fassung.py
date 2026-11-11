"""Pixel-Fassung der Modell-Texturen: je Stoff eine eigene, grobe, farbstufige Textur nur fuer den Pixel-Look.

    python3 -I tools/oberflaeche/pixel_fassung.py [--vorschau=<ordner>]

Im Pixel-Look sieht man die feinen Strukturen der Relief-Texturen kaum (ein Bildpunkt misst 2 bis 9 cm, Fugen sind 1 bis 2 cm breit). Dieses Skript macht
aus den vorhandenen Texturen (game/assets/texturen/modelle/relief_farbe_hoehe.webp fuer die Relief-Stoffe, modelle_farbe_hoehe.webp fuer die uebrigen;
beide werden nur gelesen, nichts davon wird veraendert) eine Fassung im Stil der Pixel-Art:

  * groeberes Muster: ein Stein, Ziegel oder eine Schindel ist etwa 3 bis 4 Bildpunkte gross (Zoom 25 und 50); dazu bekommt jeder Stoff eine eigene
    Kachelgroesse in Metern (PARAM: n Merkmale quer ueber die Quellkachel, f Wunschgroesse eines Merkmals);
  * Schatten fest in der Farbe: Fugen und Senken dunkel (verbreitert, damit sie nach dem Verkleinern auf Bildpunkte noch da sind), helle Oberkante, dunkle
    Unterkante (Licht von links oben, wie bei Pixel-Art), aus der Hoehe der Quelle;
  * wenige kraeftige Farbstufen (Helligkeit in 3 bis 5 Stufen, Farbart grob);
  * keine Normale, keine Rauheit, kein Parallax: ein Array aus Farbe (RGB).

Die mittlere Helligkeit (linear) ist die der bisherigen Schicht ("mittel" in materialbibliothek.json); der Shader teilt wie sonst durch sie, die Haeuser
behalten ihre Farbe. Ausgabe: game/assets/texturen/modelle/pixel_farbe.webp (128 x 128 je Schicht, eine Spalte, verlustfrei) und der Block "pixel" in
materialbibliothek.json (Kachelgroesse je Schicht; Schicht = Nummer in der Bibliothek, der Relief-Neuling "pflaster" hinter den bisherigen).
"""
import json
import os
import sys

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ZIEL = os.path.join(WURZEL, "game", "assets", "texturen", "modelle")
DATEN = os.path.join(WURZEL, "game", "daten", "materialbibliothek.json")
T = 128           # Texel je Schicht und Seite
STUFEN = 5        # Helligkeitsstufen
LUM = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)

# n: Merkmale (Steine, Ziegel, Bretter) quer ueber die Quellkachel (fehlt: Stoff ohne Einzelmerkmale, die Kachel bleibt, wie sie ist);
# f: Wunschgroesse eines Merkmals in Metern; fuge: Breite der verbreiterten Fuge in Texeln; glatt: Radius der Glaettung vor den Stufen; kontrast: Spreizung der Helligkeit
PARAM = {
    "bruchstein": dict(n=10, f=0.17, fuge=0, glatt=2, kontrast=1.4, schwelle=0.09),
    "quader": dict(n=4, f=0.20, fuge=1, glatt=2, kontrast=1.4, schwelle=0.10),
    "kalkputz": dict(kontrast=0.7, glatt=4, stufen=3),
    "lehmputz": dict(kontrast=0.7, glatt=4, stufen=3),
    "fachwerkholz": dict(kontrast=0.7, glatt=2, stufen=4),
    "bretter": dict(n=6, f=0.17, fuge=1, glatt=2, kontrast=1.3, schwelle=0.10),
    "dachziegel": dict(n=14, f=0.20, fuge=0, glatt=1, kontrast=1.4, schwelle=0.10),
    "schiefer": dict(n=8, f=0.20, fuge=0, glatt=2, kontrast=1.5, schwelle=0.10),
    "stroh": dict(kontrast=0.7, glatt=3, stufen=4),
    "sandstein": dict(n=8, f=0.16, fuge=0, glatt=2, kontrast=1.4, schwelle=0.12),
    "ziegelstein": dict(n=10, f=0.22, fuge=0, glatt=1, kontrast=1.4, schwelle=0.10),
    "eisen": dict(kontrast=0.7, glatt=3, stufen=4),
    "kupfer": dict(kontrast=0.7, glatt=4, stufen=4),
    "holzschindeln": dict(n=14, f=0.20, fuge=0, glatt=1, kontrast=1.4, schwelle=0.10),
    "leder": dict(kontrast=0.7, glatt=4, stufen=3),
    "tuch": dict(kontrast=0.7, glatt=4, stufen=3),
    "farbholz": dict(kontrast=0.7, glatt=3, stufen=4),
    "rinde": dict(kontrast=0.7, glatt=3, stufen=4),
    "fels": dict(kontrast=0.7, glatt=3, stufen=4),
    "planken": dict(kontrast=0.7, glatt=2, stufen=4),
    "laub": dict(kontrast=0.7, glatt=2, stufen=4),
    "nadeln": dict(kontrast=0.7, glatt=2, stufen=4),
    "fell": dict(kontrast=0.7, glatt=4, stufen=3),
    "pflaster": dict(n=10, f=0.17, fuge=1, glatt=2, kontrast=1.4, schwelle=0.10),
}
# Kacheln der bisherigen Relief-Schichten (fuer die Vorschau ALT)


def srgb_zu_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_zu_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def kachelnd_runter(a):
    """1024 -> T durch Mitteln (Kasten); die Kachel bleibt kachelnd."""
    f = a.shape[0] // T
    return a.reshape(T, f, T, f, *a.shape[2:]).mean(axis=(1, 3))


def kasten(a, r):
    """Kachelnder Kasten-Mittelwert mit Radius r (Texel)."""
    if r <= 0:
        return a
    s = np.zeros_like(a)
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            s += np.roll(np.roll(a, dy, 0), dx, 1)
    return s / float((2 * r + 1) ** 2)


def minimum(a, r):
    """Kachelnder Minimumfilter (verbreitert dunkle Stellen), Raute mit Radius r."""
    if r <= 0:
        return a
    m = a.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if abs(dx) + abs(dy) <= r:
                m = np.minimum(m, np.roll(np.roll(a, dy, 0), dx, 1))
    return m


def dilatiert(maske, r):
    """Kachelnde Aufweitung einer Maske (Raute mit Radius r)."""
    if r <= 0:
        return maske
    m = maske.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if abs(dx) + abs(dy) <= r:
                m |= np.roll(np.roll(maske, dy, 0), dx, 1)
    return m


def gebiete(frei):
    """Zusammenhaengende Gebiete (4er-Nachbarschaft, kachelnd) einer Maske -> Nummern (-1 = nicht frei), Anzahl."""
    nr = np.full(frei.shape, -1, np.int32)
    n = 0
    h, w = frei.shape
    for y0 in range(h):
        for x0 in range(w):
            if not frei[y0, x0] or nr[y0, x0] >= 0:
                continue
            stapel = [(y0, x0)]
            nr[y0, x0] = n
            while stapel:
                y, x = stapel.pop()
                for yy, xx in (((y + 1) % h, x), ((y - 1) % h, x), (y, (x + 1) % w), (y, (x - 1) % w)):
                    if frei[yy, xx] and nr[yy, xx] < 0:
                        nr[yy, xx] = n
                        stapel.append((yy, xx))
            n += 1
    return nr, n


def fassung(farbe_srgb, hoehe, p, ziel_mittel):
    """farbe_srgb T x T x 3 (0..1), hoehe T x T (1 oben, 0 Fuge) -> sRGB-Textur mit eingebackenem Schatten, mittlere lineare Farbe = ziel_mittel.
    Stoffe mit Einzelmerkmalen (Steine, Ziegel, Bretter: "n" in PARAM) werden nach der Hoehe in Fugen und Gebiete zerlegt, jedes Gebiet bekommt eine
    flache Farbe in wenigen Stufen, die Fuge ist dunkel, die Kanten links oben hell und rechts unten dunkel. Die uebrigen Stoffe werden stark geglaettet
    und in wenige Stufen gesetzt (Helligkeit absolut, damit ein ruhiger Stoff ruhig bleibt)."""
    lin = srgb_zu_linear(farbe_srgb)
    lum = (lin * LUM).sum(axis=2)
    ml = float(lum.mean())
    mittel = lin.reshape(-1, 3).mean(axis=0)
    k = p.get("kontrast", 1.4)
    merkmale = "n" in p and hoehe is not None and float(hoehe.std()) > 0.05
    if merkmale:
        hs = kasten(hoehe, 1)
        rr = max(3, int(round(T / p["n"] / 2)))                       # Umgebung etwa ein Merkmal gross
        fuge = (hs - kasten(hoehe, rr)) < -p.get("schwelle", 0.10)    # tiefer als die Umgebung: Fuge
        fuge = kasten(fuge.astype(np.float32), 1) > 0.5               # Mehrheitsfilter: vereinzelte Punkte weg
        fuge = dilatiert(fuge, p.get("fuge", 1))
        nr, n = gebiete(~fuge)
        out = np.zeros_like(lin)
        fest = np.zeros(fuge.shape, bool)
        for g in range(n):
            m = nr == g
            if int(m.sum()) < 6:
                continue                      # zu klein: bleibt Fuge
            c = lin[m].mean(axis=0)
            hell = float((c * LUM).sum()) / ml
            hq = np.clip(1.0 + np.round((hell - 1.0) * k * 4.0) / 4.0, 0.45, 1.7)   # Stufen zu je 0,25
            ton = c / max(float((c * LUM).sum()), 1e-4)
            ton = np.round(ton * 8) / 8
            ton = ton / max(float((ton * LUM).sum()), 1e-4)
            out[m] = ton * hq * ml
            fest |= m
        fugenfarbe = mittel * 0.42
        out = np.where(fest[:, :, None], out, fugenfarbe)
        # Kanten: Gebiet mit Fuge links oder oben (zwei Texel) = Oberkante hell, mit Fuge rechts oder unten = Unterkante dunkel
        for d in (1, 2):
            links = np.roll(~fest, d, 1) | np.roll(~fest, d, 0)
            rechts = np.roll(~fest, -d, 1) | np.roll(~fest, -d, 0)
            out = np.where(fest[:, :, None] & links[:, :, None], out * 1.28, out)
            out = np.where(fest[:, :, None] & rechts[:, :, None] & ~links[:, :, None], out * 0.78, out)
    else:
        lin = kasten(lin, p.get("glatt", 3))
        lum = (lin * LUM).sum(axis=2)
        st = p.get("stufen", STUFEN)
        rel = 1.0 + (lum / max(ml, 1e-4) - 1.0) * k
        q = np.round(np.clip(rel, 0.3, 1.9) / (0.9 / st)) * (0.9 / st)    # Stufen zu je 0,9/st (3 bis 4 Stufen nebeneinander)
        ton = lin / np.maximum(lum[:, :, None], 1e-4)
        ton = np.round(ton * 8) / 8
        ton = ton / np.maximum((ton * LUM).sum(axis=2, keepdims=True), 1e-4)
        out = ton * (q * ml)[:, :, None]
    # mittlere Helligkeit wie die bisherige Schicht (linear)
    for _ in range(4):
        m = out.reshape(-1, 3).mean(axis=0)
        out = np.clip(out * (ziel_mittel / np.maximum(m, 1e-4)), 0.0, 1.0)
    return linear_zu_srgb(out), out.reshape(-1, 3).mean(axis=0)


def main():
    opt = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in sys.argv[1:])
    daten = json.load(open(DATEN, encoding="utf-8"))
    schichten = list(daten["schichten"])
    rel = {s["name"]: (k, s) for k, s in enumerate(daten["relief"]["schichten"])}
    namen = [s["name"] for s in schichten] + [s["name"] for s in daten["relief"]["schichten"] if s.get("neu")]
    bib_farbe = Image.open(os.path.join(ZIEL, "modelle_farbe_hoehe.webp")).convert("RGBA")
    rel_farbe = Image.open(os.path.join(ZIEL, "relief_farbe_hoehe.webp")).convert("RGBA")
    anzahl = 24   # Platz im Array: so viele Schichten wie die Bibliothek (2 x 12); Schichten ohne Fassung bleiben leer (kachel 0)
    stapel, eintraege, vorschau = [], [], []
    for b in range(anzahl):
        if b >= len(namen):
            stapel.append(np.zeros((T, T, 3), np.float32))
            eintraege.append({"name": "", "kachel": 0.0})
            continue
        name = namen[b]
        p = PARAM[name]
        if name in rel:
            k, r = rel[name]
            quelle = rel_farbe.crop((0, k * 1024, 1024, k * 1024 + 1024))
            ziel_mittel = np.array(r["mittel"], np.float32)
            kachel_quelle = r["kachel"]
        else:
            quelle = bib_farbe.crop(((b % 2) * 1024, (b // 2) * 1024, (b % 2) * 1024 + 1024, (b // 2) * 1024 + 1024))
            ziel_mittel = np.array(schichten[b]["mittel"], np.float32)
            kachel_quelle = schichten[b]["kachel"]
        a = np.asarray(quelle, np.float32) / 255.0
        farbe = kachelnd_runter(a[:, :, :3])
        hoehe = kachelnd_runter(a[:, :, 3])
        tex, mittel = fassung(farbe, hoehe, p, ziel_mittel)
        kachel = round(p["n"] * p["f"], 3) if "n" in p else kachel_quelle
        stapel.append(tex.astype(np.float32))
        eintraege.append({"name": name, "kachel": kachel})
        verh = float((mittel * LUM).sum() / (ziel_mittel * LUM).sum())
        print(f"{b:2d} {name:14s} Kachel {kachel:5.2f} m (vorher {kachel_quelle:4.2f})  Helligkeit gegen die bisherige Schicht {verh * 100:6.1f} %")
        vorschau.append((name, farbe, tex, kachel, kachel_quelle))
    bild = Image.fromarray((np.concatenate(stapel, axis=0) * 255.0 + 0.5).astype(np.uint8), "RGB")
    pfad = os.path.join(ZIEL, "pixel_farbe.webp")
    bild.save(pfad, "WEBP", lossless=True, quality=100, method=6, exact=True)
    print(f"{os.path.relpath(pfad, WURZEL)}: {os.path.getsize(pfad) / 1e6:.2f} MB, {anzahl} Schichten {T} x {T}")
    if "vorschau" in opt:
        os.makedirs(opt["vorschau"], exist_ok=True)
        vorschau_bild(vorschau, os.path.join(opt["vorschau"], "pixel_fassungen.png"))
    block = {"_hinweis": "Pixel-Fassung (tools/oberflaeche/pixel_fassung.py, pixel_farbe.webp): je Schicht der Bibliothek (gleiche Nummer) eine grobe, farbstufige Textur "
                         "nur fuer den Pixel-Look; kachel = Meter je Kachel, 0 = keine Fassung (dann gilt die normale Schicht).", "schichten": eintraege}
    text = open(DATEN, encoding="utf-8").read()
    i = text.find('\n  "pixel":')
    neu = '  "pixel": ' + json.dumps(block, indent=2, ensure_ascii=False).replace("\n", "\n  ")
    basis = text[:i] if i >= 0 else text.rstrip().rstrip("}")
    text = basis.rstrip().rstrip(",") + ",\n" + neu + "\n}\n"
    open(DATEN, "w", encoding="utf-8", newline="\n").write(text)
    print("materialbibliothek.json: Block pixel geschrieben")


def vorschau_bild(liste, pfad):
    """Je Stoff links ALT (bisherige Farbe, auf Bildpunkte verkleinert), rechts NEU (Pixel-Fassung): Wandstueck 2 m x 2 m bei 4,6 cm je Bildpunkt (Zoom 50), 5fach."""
    mpp = 0.046
    px = 43
    z = 5
    spalten = 3
    zelle = px * z
    zeilen = (len(liste) + spalten - 1) // spalten
    blatt = Image.new("RGB", (spalten * (2 * zelle + 14), zeilen * (zelle + 14)), (30, 30, 30))
    ys, xs = np.mgrid[0:px, 0:px]
    for i, (name, farbe, tex, kachel, kachel_alt) in enumerate(liste):
        def feld(t, kach):
            # Kasten-Mittel ueber den Bildpunkt (wie eine Mip-Stufe) statt Einzelpunkt
            sub = 4
            acc = np.zeros((px, px, 3), np.float32)
            for sy in range(sub):
                for sx in range(sub):
                    u = (((xs + (sx + 0.5) / sub) * mpp / kach * T).astype(int)) % T
                    v = (((ys + (sy + 0.5) / sub) * mpp / kach * T).astype(int)) % T
                    acc += srgb_zu_linear(t[v, u])
            return linear_zu_srgb(acc / (sub * sub))

        def zu_bild(f):
            return Image.fromarray((np.clip(f, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB").resize((zelle, zelle), Image.NEAREST)
        x0 = (i % spalten) * (2 * zelle + 14)
        y0 = (i // spalten) * (zelle + 14)
        blatt.paste(zu_bild(feld(farbe, kachel_alt)), (x0, y0))
        blatt.paste(zu_bild(feld(tex, kachel)), (x0 + zelle, y0))
    blatt.save(pfad)
    print("Vorschau:", pfad)


if __name__ == "__main__":
    main()
