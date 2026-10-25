"""Elf kachelbare Bodentexturen für die Kartenwerkstatt (Schritt 5h) – in Blender modelliert und mit Cycles von oben gebacken.

    ~/werkzeuge/blender/blender.sh -b --python tools/oberflaeche/bodentexturen_blender.py -- [Optionen]

        --nur=wiese,kies   nur diese Texturen neu backen, die übrigen kommen aus dem Zwischenspeicher
        --groesse=1024     Kantenlänge je Textur in Pixeln (256 für schnelle Proben; geschrieben wird nur bei 1024)
        --proben=64        Cycles-Proben je Pixel
        --zwischen=<ordner> Zwischenspeicher der gebackenen Rohdaten (Vorgabe ~/.cache/reichsgilde/bodentexturen)
        --nur-packen       nichts backen, nur aus dem Zwischenspeicher packen und die Vorschau schreiben
        --format=png       PNG statt WebP schreiben (verlustfrei, aber 43 statt 28 MB)
        --nah              dazu Nahaufnahmen über die Kachelecke in den Zwischenspeicher (vorschau/<name>_nah.jpg)
        --nahtprobe        statt zu backen prüfen, ob jede Textur nahtlos ist (mit --groesse=256 --proben=16)

Jede Textur ist eine kleine 3D-Szene von 6 m × 6 m: ein Bodennetz mit einem Pixel Maschenweite (Höhenfeld aus kachelbarem
Rauschen und Zellen) und darauf Tausende Einzelteile – Grashalme in Büscheln, Kiesel, Laub, Nadeln, Äste, Strohhalme,
Pflastersteine. Was über den Rand ragt, liegt ein zweites Mal auf der Gegenseite, deshalb ist das Bild nahtlos. Eine
Kamera schaut senkrecht von oben (orthografisch, Bildausschnitt genau die Kachel) und Cycles backt die Durchgänge:
Grundfarbe (Diffuse Color), Normale, Umgebungsverdeckung (AO) und zwei eigene Werte (AOV): Höhe und Rauheit.

Schreibt nach game/assets/texturen/boden/ (Format: siehe unten und docs/bereiche/leveldesign.md, Schritt 5h):
    boden_farbe_hoehe.webp     1024 × 11264 RGBA: RGB Farbe (sRGB, leichte Verdeckung eingerechnet; verlustbehaftet q95),
                                                   A Höhe (1 tief … 255 hoch, verlustfrei; nie 0, weil WebP Farbe unter
                                                   Alpha 0 verwerfen darf)
    boden_normal_rauheit.webp  1024 × 11264 RGB:  R/G Normale im Tangentenraum (OpenGL: G+ zeigt zum oberen Bildrand),
                                                   B Rauheit (0 glatt … 1 matt); verlustfrei
Beide sind Texture2DArrays mit elf Schichten untereinander, Reihenfolge wie `Bodentextur` im Kern:
    0 Wiese  1 Erde  2 Fels  3 Kies  4 Waldboden  5 Sand  6 Schlamm  7 Moos  8 Lehm  9 Pflaster  10 Mist
Das Pixelmittel jeder Farbschicht wird auf das der 5e-Textur (game/assets/texturen/boden.png) abgeglichen, damit die
Helligkeit im Spiel gleich bleibt. Dazu Vorschaubilder in docs/bilder/boden/ (je Textur 2 × 2 gekachelt und beleuchtet).
"""
import math
import os
import struct
import sys
import time
import zlib

import bpy
import numpy as np

K = 6.0                                    # Kantenlänge einer Kachel in Metern (massstab im Boden-Shader)
WURZEL = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ZIEL = os.path.join(WURZEL, "game", "assets", "texturen", "boden")
VORSCHAU = os.path.join(WURZEL, "docs", "bilder", "boden")
ALT = os.path.join(WURZEL, "game", "assets", "texturen", "boden.png")
NAMEN = "wiese erde fels kies waldboden sand schlamm moos lehm pflaster mist".split()

# Optionen
_args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
_opt = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in _args)
N = int(_opt.get("groesse", 1024))         # Pixel je Kante (und Maschen des Bodennetzes)
PROBEN = int(_opt.get("proben", 64))
ZWISCHEN = os.path.expanduser(_opt.get("zwischen", "~/.cache/reichsgilde/bodentexturen"))
FORMAT = _opt.get("format", "webp")
UEBER = K / 2 if "nahtprobe" in _opt else 0.0   # für die Nahtprobe reicht die Szene eine halbe Kachel weiter


# ═════════════════════════════════════════════════════════════════════════════
#  Kachelbare Felder (numpy). Raster M × M über die Kachel: Zeile j ↔ y = j·K/M, Spalte i ↔ x = i·K/M (y zeigt nach oben)
# ═════════════════════════════════════════════════════════════════════════════

def norm(z):
    return (z - z.min()) / (z.max() - z.min() + 1e-12)


def rauschen(groesse, saat, streckung=1.0, m=None):
    """Kachelbares Rauschen 0..1 (Zufallsspektrum, tiefpassgefiltert); `groesse` = Größe der Flecken in Metern,
    `streckung` > 1 zieht sie in x-Richtung lang."""
    m = m or N
    r = np.random.default_rng(saat)
    f = np.fft.fftfreq(m, d=K / m)
    fx, fy = np.meshgrid(f, f)
    filt = np.exp(-((fx * groesse / streckung) ** 2 + (fy * groesse) ** 2) * 4.0)
    return norm(np.fft.ifft2(np.fft.fft2(r.standard_normal((m, m))) * filt).real)


def fbm(saat, groessen, gewichte=None, m=None):
    gewichte = gewichte or [0.55 ** i for i in range(len(groessen))]
    return norm(sum(w * rauschen(g, saat + 7 * i, m=m) for i, (g, w) in enumerate(zip(groessen, gewichte))))


def weich(feld, radius):
    """Kachelbarer Weichzeichner; `radius` in Metern."""
    m = feld.shape[0]
    f = np.fft.fftfreq(m, d=K / m)
    fx, fy = np.meshgrid(f, f)
    return np.fft.ifft2(np.fft.fft2(feld) * np.exp(-2 * (np.pi * radius) ** 2 * (fx ** 2 + fy ** 2))).real


def raster(m=None):
    m = m or N
    y, x = np.mgrid[0:m, 0:m] * (K / m)
    return x, y


def abtasten(feld, x, y):
    """Feld an beliebigen Weltpunkten (bilinear, über den Rand hinweg periodisch)."""
    m = feld.shape[0]
    u, v = np.asarray(x) / K * m, np.asarray(y) / K * m
    i0, j0 = np.floor(u).astype(int), np.floor(v).astype(int)
    fu, fv = u - i0, v - j0
    i0, j0 = i0 % m, j0 % m
    i1, j1 = (i0 + 1) % m, (j0 + 1) % m
    return (feld[j0, i0] * (1 - fu) * (1 - fv) + feld[j0, i1] * fu * (1 - fv)
            + feld[j1, i0] * (1 - fu) * fv + feld[j1, i1] * fu * fv)


def zellen(x, y, n, saat, zittern=0.9):
    """Kachelbare Voronoi-Zellen: n × n Zellen über die Kachel, je eine mit einem Punkt. Gibt je Abfragepunkt die
    Nummer der nächsten Zelle, den Abstand zum nächsten (F1) und zweitnächsten Punkt (F2, Meter) und den Weg vom nächsten
    Punkt (dx, dy) zurück."""
    r = np.random.default_rng(saat)
    c = K / n
    px = (np.arange(n)[None, :] + 0.5 + (r.random((n, n)) - 0.5) * zittern) * c
    py = (np.arange(n)[:, None] + 0.5 + (r.random((n, n)) - 0.5) * zittern) * c
    x, y = np.asarray(x, float), np.asarray(y, float)
    ci, cj = np.floor(x / c).astype(int), np.floor(y / c).astype(int)
    f1 = np.full(x.shape, 1e9)
    f2 = np.full(x.shape, 1e9)
    nummer = np.zeros(x.shape, int)
    dx1 = np.zeros(x.shape)
    dy1 = np.zeros(x.shape)
    for dj in (-2, -1, 0, 1, 2):
        for di in (-2, -1, 0, 1, 2):
            ni, nj = ci + di, cj + dj
            wi, wj = ni % n, nj % n
            qx = px[wj, wi] + (ni - wi) * c
            qy = py[wj, wi] + (nj - wj) * c
            dx, dy = x - qx, y - qy
            d = np.hypot(dx, dy)
            naeher = d < f1
            f2 = np.where(naeher, f1, np.minimum(f2, d))
            f1 = np.where(naeher, d, f1)
            nummer = np.where(naeher, wj * n + wi, nummer)
            dx1 = np.where(naeher, dx, dx1)
            dy1 = np.where(naeher, dy, dy1)
    return nummer, f1, f2, dx1, dy1


def je_zelle(nummer, saat, anzahl):
    """Eine Zufallszahl 0..1 je Zellennummer."""
    return np.random.default_rng(saat).random(anzahl)[nummer]


def glatt(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def srgb(h):
    """'#rrggbb' → lineare Farbe (so rechnet Cycles)."""
    c = np.array([int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)])
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def mische(a, b, w):
    w = np.asarray(w)[..., None]
    return np.asarray(a) * (1 - w) + np.asarray(b) * w


# ═════════════════════════════════════════════════════════════════════════════
#  Netze: Bodennetz und Einzelteile (alles als ein großes Netz mit Farbe und Rauheit je Eckpunkt)
# ═════════════════════════════════════════════════════════════════════════════

class Teile:
    """Sammelt Eckpunkte, Dreiecke, Farben und Rauheit vieler Einzelteile, bevor daraus ein Blender-Netz wird."""

    def __init__(self):
        self.v, self.f, self.c, self.r, self.k = [], [], [], [], []
        self.zahl = 0

    def dazu(self, v, f, c, r, k):
        self.v.append(v.reshape(-1, 3))
        self.f.append(f.reshape(-1, 3) + self.zahl)
        self.c.append(c.reshape(-1, 3))
        self.r.append(r.reshape(-1))
        self.k.append(np.broadcast_to(k, r.shape).reshape(-1))
        self.zahl += len(self.v[-1])

    def objekt(self, name):
        if not self.v:
            return None
        return netz_objekt(name, np.concatenate(self.v), np.concatenate(self.f), np.concatenate(self.c),
                           np.concatenate(self.r), np.concatenate(self.k))


def netz_objekt(name, v, f, c, r, korn=None):
    """Blender-Netz mit den Eckpunkt-Werten farbe, rau und korn (Stärke der feinen Körnung im Stoff, 0 = keine)."""
    me = bpy.data.meshes.new(name)
    k = f.shape[1]
    me.vertices.add(len(v))
    me.vertices.foreach_set("co", v.astype(np.float32).ravel())
    me.loops.add(len(f) * k)
    me.loops.foreach_set("vertex_index", f.astype(np.int32).ravel())
    me.polygons.add(len(f))
    me.polygons.foreach_set("loop_start", np.arange(0, len(f) * k, k, dtype=np.int32))
    try:
        me.polygons.foreach_set("loop_total", np.full(len(f), k, dtype=np.int32))
    except (AttributeError, TypeError, RuntimeError):
        pass
    me.update(calc_edges=True)
    a = me.attributes.new("farbe", "FLOAT_COLOR", "POINT")
    a.data.foreach_set("color", np.hstack([c, np.ones((len(c), 1))]).astype(np.float32).ravel())
    a = me.attributes.new("rau", "FLOAT", "POINT")
    a.data.foreach_set("value", r.astype(np.float32))
    a = me.attributes.new("korn", "FLOAT", "POINT")
    a.data.foreach_set("value", (np.zeros(len(v)) if korn is None else korn).astype(np.float32))
    me.shade_smooth()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(stoff())
    return ob


def bodennetz(hoehe, farbe, rau, rand=6):
    """Bodennetz mit einer Masche je Pixel, etwas über den Rand hinaus (dort periodisch weitergeführt)."""
    m = hoehe.shape[0]
    rand += int(UEBER / K * m)
    idx = np.arange(-rand, m + rand + 1)
    jj, ii = np.meshgrid(idx, idx, indexing="ij")
    wj, wi = jj % m, ii % m
    v = np.stack([ii * (K / m), jj * (K / m), hoehe[wj, wi]], -1).reshape(-1, 3)
    s = len(idx)
    a = (np.arange(s - 1)[:, None] * s + np.arange(s - 1)[None, :]).ravel()
    f = np.stack([a, a + 1, a + s + 1, a + s], -1)
    return netz_objekt("boden", v, f, farbe[wj, wi].reshape(-1, 3), rau[wj, wi].reshape(-1))


def drehung(a, b, c=None):
    """Drehmatrizen Rz(a) · Rx(b) · Rz(c) je Teil (a: Richtung, b: Neigung, c: Drehung um die eigene Achse)."""
    n = len(a)
    c = np.zeros(n) if c is None else c

    def rz(w):
        m = np.zeros((n, 3, 3))
        m[:, 0, 0] = np.cos(w); m[:, 0, 1] = -np.sin(w)
        m[:, 1, 0] = np.sin(w); m[:, 1, 1] = np.cos(w)
        m[:, 2, 2] = 1
        return m

    rx = np.zeros((n, 3, 3))
    rx[:, 0, 0] = 1
    rx[:, 1, 1] = np.cos(b); rx[:, 1, 2] = -np.sin(b)
    rx[:, 2, 1] = np.sin(b); rx[:, 2, 2] = np.cos(b)
    return rz(a) @ rx @ rz(c)


def LAENGS(n):
    """Dritter Winkel für drehung() bei Stäben: legt den Stab erst längs der Neigeachse, damit die Neigung ihn aufrichtet."""
    return np.full(n, np.pi / 2)


def zufallsdrehung(rng, n):
    """Gleichverteilte zufällige Drehungen (für Kiesel und Klumpen)."""
    q = rng.standard_normal((n, 4))
    q /= np.linalg.norm(q, axis=1, keepdims=True)
    w, x, y, z = q.T
    return np.stack([
        np.stack([1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], -1),
        np.stack([2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)], -1),
        np.stack([2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)], -1)], 1)


def verteilen(teile, form, pos, rot, skal, c0, c1=None, rau0=0.9, rau1=None, radius=None, korn=0.0):
    """Legt die Form (Eckpunkte, Dreiecke, t je Eckpunkt 0..1) an jede Stelle: skaliert, gedreht, verschoben. Farbe und
    Rauheit laufen über t von c0/rau0 nach c1/rau1 (z. B. Halmgrund → Halmspitze). Teile, die über den Kachelrand ragen,
    kommen ein zweites Mal auf die Gegenseite. `korn` > 0 gibt der Oberfläche eine feine Körnung (Stein)."""
    fv, ff, ft = form
    n = len(pos)
    c1 = c0 if c1 is None else c1
    rau0 = np.broadcast_to(np.asarray(rau0, float), (n,))
    rau1 = rau0 if rau1 is None else np.broadcast_to(np.asarray(rau1, float), (n,))
    radius = np.max(np.abs(skal), axis=1) * np.linalg.norm(fv, axis=1).max() * 1.05 if radius is None else np.broadcast_to(radius, (n,))
    wahl, versatz = [], []
    for dx in (-K, 0.0, K):
        for dy in (-K, 0.0, K):                                  # (die Nahtprobe braucht nur die Seite +x, +y)
            x, y = pos[:, 0] + dx, pos[:, 1] + dy
            m = (x > -radius) & (x < K + radius + UEBER) & (y > -radius) & (y < K + radius + UEBER)
            wahl.append(np.nonzero(m)[0])
            versatz.append(np.repeat([[dx, dy, 0.0]], m.sum(), 0))
    wahl = np.concatenate(wahl)
    versatz = np.concatenate(versatz)
    for s in range(0, len(wahl), 200000):                      # in Stücken, damit der Speicher reicht
        w = wahl[s:s + 200000]
        lokal = fv[None] * skal[w][:, None, :]
        v = np.einsum("nij,nvj->nvi", rot[w], lokal) + (pos[w] + versatz[s:s + 200000])[:, None, :]
        f = ff[None] + (np.arange(len(w)) * len(fv))[:, None, None]
        c = c0[w][:, None, :] * (1 - ft)[None, :, None] + c1[w][:, None, :] * ft[None, :, None]
        r = rau0[w][:, None] * (1 - ft)[None, :] + rau1[w][:, None] * ft[None, :]
        teile.dazu(v, f, c, r, korn)


# ── Formen: (Eckpunkte, Dreiecke, t je Eckpunkt) in Einheitsgröße ─────────────────────────────────────────────────────

def form_halm(segmente=4, biegung=0.35):
    """Grashalm: schmales Band von z = 0 bis 1, zur Spitze verjüngt und nach +y gebogen; t = 0 Grund … 1 Spitze."""
    v, t = [], []
    for k in range(segmente):
        s = k / segmente
        b = 0.5 * (1 - s) ** 0.6
        for seite in (-1, 1):
            v.append((seite * b, biegung * s * s, s))
            t.append(s)
    v.append((0, biegung, 1))
    t.append(1)
    f = []
    for k in range(segmente - 1):
        a = 2 * k
        f += [(a, a + 1, a + 3), (a, a + 3, a + 2)]
    a = 2 * (segmente - 1)
    f.append((a, a + 1, 2 * segmente))
    return np.array(v, float), np.array(f), np.array(t, float)


def form_kugel(unterteilung=1, saat=None, beulen=0.0):
    """Ikosaeder-Kugel (Radius 1), auf Wunsch zufällig verbeult; t = Höhe 0 (unten) … 1 (oben)."""
    p = (1 + 5 ** 0.5) / 2
    v = [(-1, p, 0), (1, p, 0), (-1, -p, 0), (1, -p, 0), (0, -1, p), (0, 1, p), (0, -1, -p), (0, 1, -p),
         (p, 0, -1), (p, 0, 1), (-p, 0, -1), (-p, 0, 1)]
    f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7),
         (9, 8, 1)]
    v = [np.array(a, float) / np.linalg.norm(a) for a in v]
    for _ in range(unterteilung):
        mitte = {}
        neu = []

        def m(a, b):
            k = (min(a, b), max(a, b))
            if k not in mitte:
                q = v[a] + v[b]
                v.append(q / np.linalg.norm(q))
                mitte[k] = len(v) - 1
            return mitte[k]

        for a, b, c in f:
            ab, bc, ca = m(a, b), m(b, c), m(c, a)
            neu += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        f = neu
    v = np.array(v)
    if beulen and saat is not None:
        r = np.random.default_rng(saat)
        # glatte Beulen: Summe weniger zufälliger Richtungen
        d = r.standard_normal((6, 3))
        d /= np.linalg.norm(d, axis=1, keepdims=True)
        w = r.random(6) * 2 - 1
        v = v * (1 + beulen * np.tanh((v @ d.T) * 2) @ w / 3)[:, None]
        v[:, :2] *= 1 + 0.25 * beulen * (r.random(2) - 0.5)
    return v, np.array(f), (v[:, 2] - v[:, 2].min()) / (np.ptp(v[:, 2]) + 1e-9)


def form_blatt(laenge=1.0, breite=0.55, ringe=4, strahlen=8, falz=0.15, wellen=0.1, saat=0):
    """Blatt (Laub), flach in der xy-Ebene, Spitze bei +x: elliptisch, mit Mittelrippe gefalzt und leicht gewellt;
    t = 0 Mitte … 1 Rand (der Rand wird dunkler/trockener)."""
    r = np.random.default_rng(saat)
    v, t = [(0.0, 0.0, 0.0)], [0.0]
    for k in range(1, ringe + 1):
        s = k / ringe
        for j in range(strahlen):
            w = 2 * np.pi * j / strahlen
            x = np.cos(w) * laenge / 2 * s
            y = np.sin(w) * breite / 2 * s * (1 - 0.3 * np.cos(w))
            z = falz * abs(y) / (breite / 2) + wellen * s * np.sin(3 * w + r.random() * 6) * 0.5
            v.append((x, y, z))
            t.append(s)
    f = []
    for j in range(strahlen):
        f.append((0, 1 + j, 1 + (j + 1) % strahlen))
    for k in range(ringe - 1):
        a, b = 1 + k * strahlen, 1 + (k + 1) * strahlen
        for j in range(strahlen):
            j2 = (j + 1) % strahlen
            f += [(a + j, b + j, b + j2), (a + j, b + j2, a + j2)]
    return np.array(v), np.array(f), np.array(t)


def form_stab(segmente=3, seiten=5, biegung=0.08, abgeflacht=1.0):
    """Stab entlang x von −0,5 bis 0,5 (Radius 0,5): Ast, Nadel, Strohhalm; leicht gebogen; t = 0 … 1 längs."""
    v, t = [], []
    for k in range(segmente + 1):
        s = k / segmente
        x = s - 0.5
        z0 = biegung * (1 - (2 * s - 1) ** 2)
        for j in range(seiten):
            w = 2 * np.pi * j / seiten
            v.append((x, 0.5 * np.cos(w), 0.5 * np.sin(w) * abgeflacht + z0))
            t.append(s)
    f = []
    for k in range(segmente):
        a, b = k * seiten, (k + 1) * seiten
        for j in range(seiten):
            j2 = (j + 1) % seiten
            f += [(a + j, b + j, b + j2), (a + j, b + j2, a + j2)]
    return np.array(v), np.array(f), np.array(t)


def form_kopfstein(saat, ecken=0.25, kuppe=0.5, segs=24, ringe=12):
    """Pflasterstein: Superellipsoid (eckig an den Seiten, oben gewölbt), Kanten leicht abgeschlagen; Größe ±1;
    t = Höhe (unten 0 … Kuppe 1). Oben und unten ein Pol mit Fächer, damit keine entarteten Dreiecke entstehen."""
    r = np.random.default_rng(saat)
    sg = lambda q, e: np.sign(q) * np.abs(q) ** e
    v = [(0.0, 0.0, -1.0)]
    for k in range(1, ringe):
        th = np.pi * k / ringe - np.pi / 2
        for j in range(segs):
            ph = 2 * np.pi * j / segs
            ct, st, cp, sp = np.cos(th), np.sin(th), np.cos(ph), np.sin(ph)
            v.append((sg(ct, kuppe) * sg(cp, ecken), sg(ct, kuppe) * sg(sp, ecken), sg(st, kuppe)))
    v.append((0.0, 0.0, 1.0))
    v = np.array(v)
    d = r.standard_normal((8, 3))
    d /= np.linalg.norm(d, axis=1, keepdims=True)
    w = (r.random(8) - 0.3) * 0.10
    v = v * (1 + np.tanh(v @ d.T * 1.5) @ w)[:, None]
    v[:, 2] += 0.10 * (r.random() - 0.5) * v[:, 0] + 0.10 * (r.random() - 0.5) * v[:, 1]
    v[:, 2] += 0.35 * np.clip(1 - v[:, 0] ** 2 - v[:, 1] ** 2, 0, 1) * (v[:, 2] > 0)        # gewölbte Kuppe
    f = [(0, 1 + (j + 1) % segs, 1 + j) for j in range(segs)]
    for k in range(ringe - 2):
        a, b = 1 + k * segs, 1 + (k + 1) * segs
        for j in range(segs):
            j2 = (j + 1) % segs
            f += [(a + j, a + j2, b + j2), (a + j, b + j2, b + j)]
    oben = len(v) - 1
    a = 1 + (ringe - 2) * segs
    f += [(a + j, a + (j + 1) % segs, oben) for j in range(segs)]
    t = np.clip((v[:, 2] + 0.2) / 1.5, 0, 1) ** 1.5
    return v, np.array(f), t


# ═════════════════════════════════════════════════════════════════════════════
#  Szene, Stoff, Backen
# ═════════════════════════════════════════════════════════════════════════════

_stoff = {}


def stoff():
    """Ein Stoff für alles: Farbe und Rauheit aus den Eckpunkt-Werten, dazu die Höhe und Rauheit als eigene Durchgänge."""
    if "s" in _stoff and _stoff["s"].name in bpy.data.materials:
        return _stoff["s"]
    m = bpy.data.materials.new("boden")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    nt = m.node_tree
    nt.nodes.clear()
    farbe = nt.nodes.new("ShaderNodeAttribute"); farbe.attribute_name = "farbe"
    rau = nt.nodes.new("ShaderNodeAttribute"); rau.attribute_name = "rau"
    korn = nt.nodes.new("ShaderNodeAttribute"); korn.attribute_name = "korn"
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    aus = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(rau.outputs["Fac"], bsdf.inputs["Roughness"])
    nt.links.new(bsdf.outputs[0], aus.inputs["Surface"])
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    xyz = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Position"], xyz.inputs[0])

    def rechne(op, a, b=None):
        m = nt.nodes.new("ShaderNodeMath")
        m.operation = op
        for i, w in enumerate((a, b)):
            if w is None:
                continue
            if isinstance(w, (int, float)):
                m.inputs[i].default_value = w
            else:
                nt.links.new(w, m.inputs[i])
        return m.outputs[0]

    # Feine Körnung (Stein): x und y laufen je einmal um einen Kreis, das 4D-Rauschen darauf ist genau kachelbar.
    # Radius K/2π: eine Einheit im Rauschen ≈ ein Meter.
    ring = K / (2 * math.pi)
    a = rechne("MULTIPLY", xyz.outputs["X"], 2 * math.pi / K)
    b = rechne("MULTIPLY", xyz.outputs["Y"], 2 * math.pi / K)
    lage = nt.nodes.new("ShaderNodeCombineXYZ")
    nt.links.new(rechne("MULTIPLY", rechne("COSINE", a), ring), lage.inputs[0])
    nt.links.new(rechne("MULTIPLY", rechne("SINE", a), ring), lage.inputs[1])
    nt.links.new(rechne("MULTIPLY", rechne("COSINE", b), ring), lage.inputs[2])
    w4 = rechne("MULTIPLY", rechne("SINE", b), ring)
    summe = None
    for groesse, staerke in ((0.004, 1.6), (0.018, 0.9)):
        tex = nt.nodes.new("ShaderNodeTexNoise")
        tex.noise_dimensions = "4D"
        tex.inputs["Scale"].default_value = 1 / groesse
        tex.inputs["Detail"].default_value = 3.0
        nt.links.new(lage.outputs[0], tex.inputs["Vector"])
        nt.links.new(w4, tex.inputs["W"])
        teil = rechne("MULTIPLY", rechne("SUBTRACT", tex.outputs["Fac"], 0.5), staerke)
        summe = teil if summe is None else rechne("ADD", summe, teil)
    faktor = rechne("ADD", rechne("MULTIPLY", summe, korn.outputs["Fac"]), 1.0)
    mal = nt.nodes.new("ShaderNodeVectorMath")
    mal.operation = "SCALE"
    nt.links.new(farbe.outputs["Color"], mal.inputs[0])
    nt.links.new(faktor, mal.inputs["Scale"])
    nt.links.new(mal.outputs[0], bsdf.inputs["Base Color"])
    for name, quelle in (("hoehe", xyz.outputs["Z"]), ("rauheit", rau.outputs["Fac"])):
        a = nt.nodes.new("ShaderNodeOutputAOV")
        try:
            a.aov_name = name
        except AttributeError:
            a.name = name
        nt.links.new(quelle, a.inputs["Value"])
    _stoff["s"] = m
    return m


def neue_szene(ao_weite):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _stoff.clear()
    sz = bpy.context.scene
    sz.render.engine = "CYCLES"
    geraet = "CPU"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "CUDA"
        prefs.get_devices()
        if any(d.type == "CUDA" for d in prefs.devices):
            for d in prefs.devices:
                d.use = d.type == "CUDA"
            geraet = "GPU"
    except (KeyError, TypeError, AttributeError, RuntimeError):
        pass
    sz.cycles.device = geraet
    sz.cycles.samples = PROBEN
    sz.cycles.use_adaptive_sampling = False
    sz.cycles.use_denoising = False
    sz.cycles.max_bounces = 0
    for name, wert in (("pixel_filter_type", "GAUSSIAN"), ("filter_type", "GAUSSIAN")):
        try:
            setattr(sz.cycles, name, wert)
        except (AttributeError, TypeError):
            pass
    sz.cycles.filter_width = 1.2
    sz.render.resolution_x = sz.render.resolution_y = N
    sz.render.resolution_percentage = 100
    sz.render.film_transparent = False
    welt = bpy.data.worlds.new("welt")
    sz.world = welt
    welt.light_settings.distance = ao_weite
    vl = sz.view_layers[0]
    vl.use_pass_diffuse_color = True
    vl.use_pass_normal = True
    vl.use_pass_ambient_occlusion = True
    for name in ("hoehe", "rauheit"):
        a = vl.aovs.add()
        a.name = name
        a.type = "VALUE"
    kam = bpy.data.cameras.new("kamera")
    kam.type = "ORTHO"
    kam.ortho_scale = K
    kam.clip_start = 0.1
    kam.clip_end = 20
    ob = bpy.data.objects.new("kamera", kam)
    ob.location = (K / 2, K / 2, 8)
    sz.collection.objects.link(ob)
    sz.camera = ob
    return geraet


def backen(name):
    """Rendert die Szene als EXR (unkomprimiert, 32 bit) und liest die Durchgänge zurück, oberste Bildzeile zuerst."""
    sz = bpy.context.scene
    s = sz.render.image_settings
    try:
        s.media_type = "MULTI_LAYER_IMAGE"
    except (AttributeError, TypeError):
        pass
    try:
        s.file_format = "OPEN_EXR_MULTILAYER"
    except TypeError:
        s.file_format = "OPEN_EXR"
    s.exr_codec = "NONE"
    s.color_depth = "32"
    pfad = os.path.join(ZWISCHEN, name + ".exr")
    sz.render.filepath = pfad
    bpy.ops.render.render(write_still=True)
    kanaele = lies_exr(pfad)
    os.remove(pfad)
    return kanaele


def lies_exr(pfad):
    """Kleiner Leser für unkomprimierte Scanline-EXR, ein- oder mehrteilig (so schreibt sie backen()). Gibt je Kanal
    ein Bild zurück, Name wie „ViewLayer.DiffCol.R“, oberste Bildzeile zuerst."""
    with open(pfad, "rb") as f:
        d = f.read()
    assert d[:4] == b"\x76\x2f\x31\x01", "kein EXR"
    mehrteilig = bool(struct.unpack("<I", d[4:8])[0] & 0x1000)
    pos, koepfe = 8, []
    while True:
        kopf = {}
        while d[pos] != 0:
            e = d.index(b"\0", pos); name = d[pos:e].decode(); pos = e + 1
            e = d.index(b"\0", pos); pos = e + 1
            groesse = struct.unpack("<i", d[pos:pos + 4])[0]; pos += 4
            kopf[name] = d[pos:pos + groesse]; pos += groesse
        pos += 1
        if kopf:
            koepfe.append(kopf)
        if not mehrteilig or d[pos] == 0 or not kopf:
            break
    if mehrteilig:
        pos += 1
    teile = []
    for kopf in koepfe:
        assert kopf["compression"][0] == 0, "EXR ist komprimiert"
        b, p, kanaele = kopf["channels"], 0, []
        while b[p] != 0:
            e = b.index(b"\0", p)
            kanaele.append((b[p:e].decode(), struct.unpack("<i", b[e + 1:e + 5])[0]))
            p = e + 17
        x0, y0, x1, y1 = struct.unpack("<4i", kopf["dataWindow"])
        h = y1 - y0 + 1
        teil = kopf["name"].rstrip(b"\0").decode() if "name" in kopf else ""
        teile.append((teil, kanaele, x1 - x0 + 1, h, y0, np.frombuffer(d, "<u8", h, pos)))
        pos += 8 * h
    art = {0: ("<u4", 4), 1: ("<f2", 2), 2: ("<f4", 4)}
    aus = {}
    for nr, (teil, kanaele, w, h, y0, offsets) in enumerate(teile):
        namen = [n if "." in n or not teil else f"{teil}.{n}" for n, _ in kanaele]
        for n in namen:
            aus[n] = np.empty((h, w), np.float32)
        for o in offsets:
            o = int(o) + (4 if mehrteilig else 0)
            y = struct.unpack("<i", d[o:o + 4])[0] - y0
            q = o + 8
            for n, (_, t) in zip(namen, kanaele):
                dt, gr = art[t]
                aus[n][y] = np.frombuffer(d, dt, w, q)
                q += w * gr
    return aus


def kanal(daten, pass_name, teile="RGB"):
    """Holt einen Durchgang (z. B. 'Diffuse Color', 'Normal') aus den EXR-Kanälen, egal wie Blender die Ebene nennt."""
    erg = []
    for t in teile:
        treffer = [k for k in daten if k.split(".")[-2:] == [pass_name, t]]
        if not treffer:
            raise KeyError(f"{pass_name}.{t} fehlt; vorhanden: {sorted(daten)}")
        erg.append(daten[treffer[0]])
    return np.stack(erg, -1)


# ═════════════════════════════════════════════════════════════════════════════
#  Die elf Texturen. Jede baut ihre Szene und gibt (AO-Anteil in der Farbe, Normalenstärke) zurück.
# ═════════════════════════════════════════════════════════════════════════════

def wogen(saat, groessen):
    """Polster-Rauschen 0..1: rundliche Buckel, dazwischen schmale Senken (wo das Rauschen die Mitte kreuzt)."""
    return np.clip(np.abs(2 * fbm(saat, groessen) - 1) / 0.45, 0, 1) ** 0.7


def verzerrt(x, y, staerke, groessen, saat):
    """Weltkoordinaten mit kachelbarem Rauschen verschoben – macht Zellen und Wellen unregelmäßig."""
    return x + staerke * (fbm(saat, groessen) - 0.5), y + staerke * (fbm(saat + 50, groessen) - 0.5)


def moospolster(saat, staerke=1.0):
    """Höhe, Farbe und Polsterform eines Moosteppichs (für Moos und die Moosflecken im Waldboden)."""
    k = 0.65 * wogen(saat, [0.14, 0.06]) + 0.35 * wogen(saat + 1, [0.05, 0.02])
    fasern = norm(rauschen(0.004, saat + 2) + 0.6 * rauschen(0.0025, saat + 3))
    art = fbm(saat + 4, [0.5, 0.18])
    h = staerke * (0.022 * k + 0.004 * fasern)
    farbe = mische(srgb("#33481e"), srgb("#5e7a2c"), np.clip(k * 1.2, 0, 1))
    farbe = mische(farbe, srgb("#9aae46"), 0.55 * glatt(0.55, 0.9, fasern) * k)
    farbe = mische(farbe, srgb("#6e8a28"), 0.45 * glatt(0.6, 0.8, art))            # gelblichere Art
    farbe = mische(farbe, srgb("#3e5a34"), 0.35 * (1 - glatt(0.2, 0.4, art)))      # bläulich-dunkle Art
    farbe = mische(farbe, srgb("#3a3020"), 0.75 * (1 - glatt(0.05, 0.3, k)))      # Senken zwischen den Polstern
    return h, farbe, k


def boden_rau(wert):
    return np.full((N, N), float(wert))


def wiese(rng):
    x, y = raster()
    gross = fbm(1, [1.6, 0.6])                                    # große Flecken: üppig ↔ mager, gelblich ↔ bläulich
    mittel = fbm(2, [0.25, 0.08])
    h = 0.006 * fbm(3, [0.3, 0.05, 0.012])
    erde = mische(srgb("#4a3f2a"), srgb("#3c4426"), mittel)
    bodennetz(h, erde, boden_rau(0.95))
    halm = form_halm(4, 0.30)
    teile = Teile()

    def halme(pos, richtung, neigung, laenge, breite, gruen, gelb):
        n = len(pos)
        rot = drehung(richtung, neigung, rng.uniform(-0.3, 0.3, n))
        skal = np.stack([breite, breite, laenge], -1)
        grund = gruen * rng.uniform(0.35, 0.55, n)[:, None]
        spitze = mische(gruen, gelb, rng.uniform(0.15, 0.5, n)) * rng.uniform(1.0, 1.25, n)[:, None]
        verteilen(teile, halm, pos, rot, skal, grund, spitze, 0.9, 0.75, radius=laenge * 1.1)

    def farbton(px, py, n):
        g, m = abtasten(gross, px, py), abtasten(mittel, px, py)
        ton = np.clip(0.55 * g + 0.45 * rng.random(n), 0, 1)
        gruen = mische(mische(srgb("#4f7d3c"), srgb("#6f9640"), ton), srgb("#5a8a52"), 0.4 * (m > 0.6) * rng.random(n))
        trocken = rng.random(n) < 0.03 + 0.08 * glatt(0.55, 0.9, g)
        gruen[trocken] = mische(srgb("#a8975c"), srgb("#8c7c48"), rng.random(trocken.sum()))
        return gruen

    # Büschel: Halme stehen um einen Mittelpunkt und neigen sich nach außen
    zahl = 22000
    mx, my = rng.random(zahl) * K, rng.random(zahl) * K
    dichte = abtasten(gross, mx, my)
    je = rng.integers(6, 16, zahl) + (dichte * 6).astype(int)
    bx, by = np.repeat(mx, je), np.repeat(my, je)
    n = len(bx)
    w = rng.random(n) * 2 * np.pi
    r = 0.025 * np.sqrt(rng.random(n))
    px, py = bx + r * np.cos(w), by + r * np.sin(w)
    pos = np.stack([px, py, abtasten(h, px, py)], -1)
    richtung = w - np.pi / 2 + rng.normal(0, 0.5, n)            # Rz dreht +y (Biegerichtung) nach außen
    laenge = rng.uniform(0.07, 0.17, n) * (0.8 + 0.4 * abtasten(gross, px, py))
    halme(pos, richtung, rng.uniform(0.3, 1.1, n), laenge, rng.uniform(0.006, 0.010, n), farbton(px, py, n),
          np.repeat([srgb("#b6b65a")], n, 0))
    # einzelne Halme dazwischen
    n = 90000
    px, py = rng.random(n) * K, rng.random(n) * K
    pos = np.stack([px, py, abtasten(h, px, py)], -1)
    halme(pos, rng.random(n) * 2 * np.pi, rng.uniform(0.4, 1.3, n), rng.uniform(0.04, 0.12, n),
          rng.uniform(0.005, 0.008, n), farbton(px, py, n), np.repeat([srgb("#a9b05a")], n, 0))
    # Klee und kleine Rosetten in Flecken
    blatt = form_blatt(1.0, 0.9, 3, 8, 0.05, 0.05, 1)
    n = 14000
    px, py = rng.random(n) * K, rng.random(n) * K
    behalten = abtasten(mittel, px, py) > 0.62
    px, py = px[behalten], py[behalten]
    n = len(px)
    pos = np.stack([px, py, abtasten(h, px, py) + rng.uniform(0.01, 0.04, n)], -1)
    s = rng.uniform(0.012, 0.022, n)
    farbe = mische(srgb("#3f6a32"), srgb("#58803c"), rng.random(n))
    verteilen(teile, blatt, pos, drehung(rng.random(n) * 7, rng.uniform(0, 0.4, n)), np.stack([s, s, s], -1),
              farbe, farbe * 0.8, 0.7)
    teile.objekt("halme")
    return 0.55, 0.55


def erde(rng):
    x, y = raster()
    feucht = fbm(11, [0.3, 0.1])
    schollen = fbm(12, [0.35, 0.12, 0.04])
    nummer, f1, f2, _, _ = zellen(x, y, 150, 15, 0.95)              # Krümel: kleine Buckel mit Ritzen dazwischen
    kruemel = np.sqrt(np.clip(1 - (f1 / (K / 150 * 0.75)) ** 2, 0, 1)) * (0.5 + je_zelle(nummer, 16, 22500))
    ritze = 1 - glatt(0.0, 0.004, f2 - f1)
    h = 0.016 * schollen + 0.004 * kruemel - 0.003 * ritze + 0.004 * (fbm(14, [1.5]) - 0.5)
    farbe = mische(srgb("#6e5236"), srgb("#4e3a26"), 0.4 * glatt(0.4, 0.8, feucht))
    farbe = mische(farbe, srgb("#8a6c4c"), 0.45 * glatt(0.55, 0.85, schollen) * (1 - feucht))
    farbe = mische(farbe, srgb("#3a2a1c"), 0.5 * ritze)
    farbe = farbe * (0.85 + 0.25 * kruemel)[..., None]
    bodennetz(h, farbe, 0.9 + 0.08 * (1 - feucht))
    teile = Teile()
    formen = [form_kugel(1, 100 + i, 0.5) for i in range(10)]

    def klumpen(n, gmin, gmax, platt, ton_hell):
        px, py = rng.random(n) * K, rng.random(n) * K
        g = rng.uniform(gmin, gmax, n)
        f = rng.integers(0, len(formen), n)
        z = abtasten(h, px, py) - g * rng.uniform(0.0, 0.35, n) * platt
        fe = abtasten(feucht, px, py)
        basis = mische(mische(srgb("#6a4e33"), srgb("#4c3825"), fe), srgb("#8c6e4e"), ton_hell * rng.random(n))
        basis *= rng.uniform(0.85, 1.15, n)[:, None]
        for k, form in enumerate(formen):
            w = f == k
            m = w.sum()
            skal = np.stack([g[w] * rng.uniform(0.8, 1.2, m), g[w] * rng.uniform(0.7, 1.1, m), g[w] * platt], -1)
            verteilen(teile, form, np.stack([px[w], py[w], z[w]], -1), zufallsdrehung(rng, m), skal,
                      basis[w] * 0.8, basis[w] * 1.12, 0.95)

    klumpen(9000, 0.010, 0.030, 0.6, 0.6)
    klumpen(50000, 0.003, 0.008, 0.7, 0.4)
    # Steinchen
    n = 1800
    px, py = rng.random(n) * K, rng.random(n) * K
    g = rng.uniform(0.005, 0.014, n)
    farbe = mische(srgb("#8a8278"), srgb("#a89a84"), rng.random(n)) * rng.uniform(0.8, 1.1, n)[:, None]
    verteilen(teile, formen[3], np.stack([px, py, abtasten(h, px, py)], -1), zufallsdrehung(rng, n),
              np.stack([g, g * 0.8, g * 0.6], -1), farbe * 0.85, farbe, 0.7)
    # Würzelchen und Halmreste
    stab = form_stab(4, 4, 0.1)
    n = 900
    px, py = rng.random(n) * K, rng.random(n) * K
    l = rng.uniform(0.03, 0.10, n)
    d = rng.uniform(0.0015, 0.003, n)
    farbe = mische(srgb("#9c8458"), srgb("#5c4630"), rng.random(n))
    verteilen(teile, stab, np.stack([px, py, abtasten(h, px, py) + 0.002], -1),
              drehung(rng.random(n) * 7, rng.uniform(-0.15, 0.15, n), LAENGS(n)),
              np.stack([l, d, d], -1), farbe, farbe * 0.9, 0.85)
    teile.objekt("krume")
    return 0.6, 0.8


def fels(rng):
    x, y = raster()
    wx, wy = verzerrt(x, y, 0.35, [0.7, 0.25], 20)
    nummer, f1, f2, dx, dy = zellen(wx, wy, 6, 21, 0.9)
    a = je_zelle(nummer, 22, 36) - 0.5
    b = je_zelle(nummer, 23, 36) - 0.5
    c = je_zelle(nummer, 24, 36)
    riss = f2 - f1                                                 # 0 auf der Grenze zweier Platten
    breite = 0.004 + 0.04 * fbm(19, [0.4, 0.12]) ** 3              # Risse mal fein, mal klaffend
    platte = 0.07 * c + 0.07 * (a * dx + b * dy)                   # jede Platte eigen geneigt und gehoben
    vx, vy = verzerrt(x, y, 0.08, [0.2, 0.06], 25)
    nummer2, g1, g2, _, _ = zellen(vx, vy, 20, 26, 0.95)
    feinriss = np.where(je_zelle(nummer2, 27, 400) > 0.55, g2 - g1, 1.0)
    grob = fbm(28, [0.5, 0.18, 0.06])
    fein = fbm(29, [0.02, 0.007])
    schicht = rauschen(0.05, 30, 8.0)                               # feine Schichtung längs
    narben = rauschen(0.01, 31)
    tief = 1 - glatt(0.0, breite, riss)
    h = (platte + 0.025 * grob + 0.005 * fein + 0.004 * schicht - 0.004 * glatt(0.68, 0.85, narben)
         - 0.05 * tief - 0.012 * (1 - glatt(0.0, breite + 0.04, riss)) - 0.006 * (1 - glatt(0.0, 0.004, feinriss)))
    ton = je_zelle(nummer, 32, 36)
    farbe = mische(srgb("#7e7a74"), srgb("#a39c90"), ton)
    farbe = mische(farbe, srgb("#5e5c58"), 0.45 * glatt(0.5, 0.8, grob))
    farbe = mische(farbe, srgb("#9a7c5a"), 0.4 * glatt(0.6, 0.85, fbm(33, [0.4, 0.12])))      # Rost
    farbe = farbe * (0.9 + 0.2 * schicht)[..., None]
    kante = glatt(0.0, breite, riss) * (1 - glatt(breite, breite + 0.03, riss))
    farbe = mische(farbe, srgb("#b2aca0"), 0.35 * kante)                                    # verwitterte Kanten
    farbe = mische(farbe, srgb("#4a4436"), 0.4 * glatt(0.35, 0.1, grob))                    # Schmutz in Mulden
    # Flechten: runde Flecken, grauweiß und gelborange
    lnr, l1, _, _, _ = zellen(x, y, 70, 34, 0.9)
    lr = 0.006 + 0.025 * je_zelle(lnr, 35, 4900) ** 2
    da = je_zelle(lnr, 36, 4900) < 0.3 * glatt(0.45, 0.7, fbm(37, [0.5, 0.15]))
    rand_rauh = 0.5 + 1.0 * rauschen(0.006, 38)
    flechte = da * (1 - glatt(lr * 0.7, lr, l1 * rand_rauh))
    gelb = je_zelle(lnr, 39, 4900) < 0.25
    farbe = mische(farbe, np.where(gelb[..., None], srgb("#b0904a"), srgb("#a9ab98")), 0.6 * flechte)
    moos = (1 - glatt(0.0, breite + 0.03, riss)) * glatt(0.45, 0.6, rauschen(0.3, 40))
    farbe = mische(farbe, srgb("#4a6430"), 0.8 * moos)
    farbe = mische(farbe, srgb("#2e2c26"), (0.3 + 0.4 * glatt(0.004, 0.02, breite)) * tief)
    farbe *= (0.92 + 0.16 * fein)[..., None]
    bodennetz(h + 0.002 * flechte, farbe, 0.82 + 0.12 * flechte + 0.05 * moos)
    # loser Grus in den Rissen
    teile = Teile()
    formen = [form_kugel(1, 200 + i, 0.5) for i in range(6)]
    n = 60000
    px, py = rng.random(n) * K, rng.random(n) * K
    q = abtasten(riss, px, py)
    w = q < abtasten(breite, px, py) * rng.uniform(0.5, 2.5, n)
    px, py = px[w], py[w]
    n = len(px)
    g = rng.uniform(0.004, 0.02, n)
    farbe = mische(srgb("#6e6a62"), srgb("#9a948a"), rng.random(n)) * rng.uniform(0.8, 1.1, n)[:, None]
    k = rng.integers(0, len(formen), n)
    for i, form in enumerate(formen):
        s = k == i
        verteilen(teile, form, np.stack([px[s], py[s], abtasten(h, px[s], py[s]) + 0.1 * g[s]], -1),
                  zufallsdrehung(rng, s.sum()), np.stack([g[s], g[s] * 0.8, g[s] * 0.6], -1) * 0.5, farbe[s] * 0.8,
                  farbe[s], 0.8, korn=0.4)
    teile.objekt("grus")
    return 0.5, 0.9


def kies(rng):
    x, y = raster()
    h = 0.004 * fbm(41, [0.4, 0.1, 0.01])
    farbe = mische(srgb("#7a7264"), srgb("#5e584e"), fbm(42, [0.3, 0.05]))
    bodennetz(h, farbe, boden_rau(0.95))
    teile = Teile()
    formen = [form_kugel(1, 300 + i, 0.6) for i in range(14)]
    paletten = [("#9a968e", 3), ("#7e7a74", 3), ("#b8ab92", 3), ("#a88c64", 1.5), ("#cfcac0", 1), ("#5c5852", 1.2),
                ("#9a7c6c", 0.8), ("#8e9088", 1.5)]
    p = np.array([w for _, w in paletten]); p /= p.sum()
    fl = fbm(43, [0.8, 0.25])
    for lage, n, gmin, gmax, sink in ((0, 55000, 0.016, 0.045, 0.35), (1, 25000, 0.012, 0.035, 0.15)):
        px, py = rng.random(n) * K, rng.random(n) * K
        g = np.exp(rng.uniform(np.log(gmin), np.log(gmax), n))
        wahl = rng.choice(len(paletten), n, p=p)
        basis = np.array([srgb(c) for c, _ in paletten])[wahl] * rng.uniform(0.8, 1.15, n)[:, None]
        basis = mische(basis, basis * np.array([1.06, 1.0, 0.9]), abtasten(fl, px, py))
        k = rng.integers(0, len(formen), n)
        z = abtasten(h, px, py) + g * (0.45 - sink) * 0.6 + lage * 0.006
        for i, form in enumerate(formen):
            s = k == i
            m = s.sum()
            skal = np.stack([g[s], g[s] * rng.uniform(0.6, 0.9, m), g[s] * rng.uniform(0.4, 0.6, m)], -1) * 0.5
            rot = drehung(rng.random(m) * 7, rng.normal(0, 0.25, m), rng.random(m) * 7)
            verteilen(teile, form, np.stack([px[s], py[s], z[s]], -1), rot, skal, basis[s] * 0.85, basis[s] * 1.08,
                      rng.uniform(0.55, 0.85, m), korn=0.4)
    teile.objekt("kiesel")
    return 0.6, 0.85


def waldboden(rng):
    x, y = raster()
    fleck = fbm(51, [0.3, 0.1, 0.04]) + 0.25 * (rauschen(0.02, 56) - 0.5)
    moos = glatt(0.55, 0.62, fleck)
    mh, mfarbe, _ = moospolster(57, 0.8)
    mfarbe = mische(mfarbe, srgb("#4a5028"), 0.6)                                  # gedämpfter als freies Moos
    h = 0.012 * fbm(52, [0.5, 0.15]) + mh * moos
    farbe = mische(srgb("#30291c"), srgb("#443c26"), fbm(54, [0.2, 0.05]))
    farbe = mische(farbe, srgb("#56502c"), 0.4 * rauschen(0.006, 55))
    farbe = mische(farbe, mfarbe, moos)
    bodennetz(h, farbe, boden_rau(0.92))
    teile = Teile()
    streu = 1 - moos
    # Nadeln: ein rotbrauner Teppich
    nadel = form_stab(2, 3, 0.05)
    n = 140000
    px, py = rng.random(n) * K, rng.random(n) * K
    behalten = rng.random(n) < 0.08 + 0.92 * abtasten(streu, px, py)
    px, py = px[behalten], py[behalten]
    n = len(px)
    l = rng.uniform(0.012, 0.028, n)
    d = rng.uniform(0.0025, 0.0035, n)
    farbe = mische(srgb("#7a5a32"), srgb("#4e4026"), rng.random(n))                   # Nadeln, alt und braun
    farbe = mische(farbe, srgb("#6e6a3a"), 0.5 * (rng.random(n) < 0.3))               # frischer, olivgrau
    verteilen(teile, nadel, np.stack([px, py, abtasten(h, px, py) + rng.uniform(0.0, 0.012, n)], -1),
              drehung(rng.random(n) * 7, rng.uniform(-0.2, 0.2, n), LAENGS(n)), np.stack([l, d, d], -1), farbe,
              farbe * 1.1, 0.75)
    # Laub in Lagen, jüngeres oben heller
    formen = [form_blatt(1.0, rng.uniform(0.55, 0.8), 4, 10, rng.uniform(0.05, 0.15), 0.12, 400 + i) for i in range(8)]
    n = 26000
    px, py = rng.random(n) * K, rng.random(n) * K
    behalten = rng.random(n) < 0.3 + 0.7 * abtasten(streu, px, py) * glatt(0.2, 0.6, abtasten(fbm(58, [0.3, 0.1]), px, py) + 0.3)
    px, py = px[behalten], py[behalten]
    n = len(px)
    l = rng.uniform(0.04, 0.09, n)
    lage = rng.random(n)
    farben = np.array([srgb(c) for c in ("#74502a", "#5a4222", "#8e6a34", "#3e321c", "#7e6432", "#6a5e32", "#94783c",
                                         "#5e5c30", "#6a6834", "#55572c", "#7a7440")])
    basis = farben[rng.integers(0, len(farben), n)] * rng.uniform(0.75, 1.1, n)[:, None]
    basis = mische(basis, srgb("#2e2418"), 0.5 * (1 - lage))                     # unten: dunkel und vermodert
    k = rng.integers(0, len(formen), n)
    z = abtasten(h, px, py) + 0.012 * lage
    for i, form in enumerate(formen):
        s = k == i
        m = s.sum()
        rot = drehung(rng.random(m) * 7, rng.normal(0, 0.2, m), np.zeros(m))
        verteilen(teile, form, np.stack([px[s], py[s], z[s]], -1), rot, np.stack([l[s], l[s], l[s]], -1),
                  basis[s], basis[s] * 0.75, 0.8)
    # Äste und Zweige
    ast = form_stab(5, 6, 0.06)
    n = 450
    px, py = rng.random(n) * K, rng.random(n) * K
    l = np.exp(rng.uniform(np.log(0.05), np.log(0.4), n))
    d = l * rng.uniform(0.025, 0.045, n)
    farbe = mische(srgb("#4e4034"), srgb("#3a2e24"), rng.random(n))
    verteilen(teile, ast, np.stack([px, py, abtasten(h, px, py) + d * 0.4], -1),
              drehung(rng.random(n) * 7, rng.normal(0, 0.04, n), LAENGS(n)), np.stack([l, d, d], -1), farbe,
              farbe * 1.15, 0.85)
    # Zapfen
    zapfen = form_kugel(1, 500, 0.15)
    n = 60
    px, py = rng.random(n) * K, rng.random(n) * K
    g = rng.uniform(0.03, 0.05, n)
    farbe = np.repeat([srgb("#6a4428")], n, 0)
    verteilen(teile, zapfen, np.stack([px, py, abtasten(h, px, py) + g * 0.15], -1),
              drehung(rng.random(n) * 7, np.full(n, np.pi / 2)), np.stack([g * 0.35, g * 0.35, g], -1), farbe * 0.8,
              farbe * 1.2, 0.85)
    teile.objekt("streu")
    return 0.6, 0.75


def sand(rng):
    x, y = raster()
    wx, wy = verzerrt(x, y, 0.5, [0.8, 0.3, 0.12], 61)
    phase_a = 2 * np.pi * (5 * wx + 47 * wy) / K                    # ganze Wellenzahlen: kachelbar
    phase_b = 2 * np.pi * (-11 * wx + 39 * wy) / K
    wa = np.sin(phase_a) + 0.35 * np.sin(2 * phase_a + 0.8)
    wb = np.sin(phase_b) + 0.35 * np.sin(2 * phase_b + 0.8)
    welle = mische(wa[..., None], wb[..., None], glatt(0.4, 0.6, fbm(62, [0.8, 0.3])))[..., 0]
    staerke = glatt(0.35, 0.7, fbm(63, [0.9, 0.35]))                 # nur stellenweise Rippel
    duene = fbm(64, [1.5, 0.5])
    korn = rauschen(0.004, 65)
    tritt = glatt(0.6, 0.85, fbm(69, [0.12, 0.04]))
    h = 0.003 * staerke * welle + 0.02 * duene + 0.0008 * korn - 0.003 * tritt * (1 - staerke)
    farbe = mische(srgb("#b49a68"), srgb("#c8b486"), 0.5 + 0.25 * welle * staerke)
    farbe = mische(farbe, srgb("#8a7a5a"), 0.35 * (1 - glatt(-1.2, -0.4, welle)) * staerke)    # Schwermineral in Tälern
    farbe = farbe * (0.86 + 0.28 * korn)[..., None]
    bunt = rauschen(0.003, 66)
    farbe = mische(farbe, srgb("#6a5e50"), 0.35 * (bunt > 0.72))
    farbe = mische(farbe, srgb("#e0d6c0"), 0.4 * (bunt < 0.25))
    farbe = mische(farbe, srgb("#9a8460"), 0.2 * glatt(0.45, 0.75, fbm(67, [0.25, 0.08])))     # feuchtere Stellen
    bodennetz(h, farbe, 0.97 - 0.1 * glatt(0.45, 0.75, fbm(67, [0.25, 0.08])))
    teile = Teile()
    formen = [form_kugel(1, 600 + i, 0.5) for i in range(6)]
    n = 1400
    px, py = rng.random(n) * K, rng.random(n) * K
    g = rng.uniform(0.005, 0.016, n)
    farbe = mische(srgb("#8e8678"), srgb("#c0b49c"), rng.random(n))
    k = rng.integers(0, len(formen), n)
    for i, form in enumerate(formen):
        s = k == i
        verteilen(teile, form, np.stack([px[s], py[s], abtasten(h, px[s], py[s])], -1), zufallsdrehung(rng, s.sum()),
                  np.stack([g[s], g[s] * 0.8, g[s] * 0.55], -1) * 0.5, farbe[s] * 0.85, farbe[s], 0.7)
    schale = form_blatt(1.0, 0.8, 3, 8, 0.25, 0.0, 9)
    n = 300
    px, py = rng.random(n) * K, rng.random(n) * K
    g = rng.uniform(0.006, 0.014, n)
    farbe = mische(srgb("#e8e0d0"), srgb("#c8b8a0"), rng.random(n))
    verteilen(teile, schale, np.stack([px, py, abtasten(h, px, py) + 0.001], -1), drehung(rng.random(n) * 7,
              rng.normal(0, 0.3, n)), np.stack([g, g, g], -1), farbe, farbe * 0.9, 0.6)
    # angespültes Ried und Hölzchen
    stab = form_stab(4, 4, 0.06)
    n = 300
    px, py = rng.random(n) * K, rng.random(n) * K
    l = rng.uniform(0.03, 0.15, n)
    d = rng.uniform(0.002, 0.005, n)
    farbe = mische(srgb("#8a7a5a"), srgb("#5a4a38"), rng.random(n))
    verteilen(teile, stab, np.stack([px, py, abtasten(h, px, py)], -1), drehung(rng.random(n) * 7,
              rng.normal(0, 0.03, n), LAENGS(n)), np.stack([l, d, d * 0.6], -1), farbe, farbe * 0.9, 0.8)
    teile.objekt("koerner")
    return 0.5, 0.9


def schlamm(rng):
    x, y = raster()
    grob = fbm(71, [0.35, 0.12, 0.05])
    wx, wy = verzerrt(x, y, 0.15, [0.3, 0.1], 75)
    zertreten = wogen(76, [0.09, 0.035])                              # durchgewalkt: Wülste und Kerben
    fein = fbm(72, [0.03, 0.01])
    h = 0.035 * grob + 0.008 * zertreten + 0.002 * fein
    pegel = np.percentile(h, 20)
    tiefe = pegel - h
    nummer, f1, f2, _, _ = zellen(wx, wy, 30, 73, 0.9)
    trocken = glatt(0.62, 0.8, norm(h))
    risse = (1 - glatt(0.0, 0.005, f2 - f1)) * trocken
    h = h - 0.004 * risse
    h = np.maximum(h, pegel)
    ufer = glatt(-0.01, 0.0, tiefe)                                   # nasser Rand um die Pfützen
    farbe = mische(srgb("#5a4a32"), srgb("#463a28"), fbm(74, [0.25, 0.06]))
    farbe = mische(farbe, srgb("#3e3426"), 0.15 * (1 - zertreten))
    farbe = mische(farbe, srgb("#6e5e44"), 0.4 * trocken)
    farbe = mische(farbe, srgb("#302820"), 0.6 * risse)
    farbe = mische(farbe, srgb("#342a1e"), 0.5 * ufer)
    farbe = mische(farbe, srgb("#2a2418"), 0.85 * glatt(0.0, 0.006, tiefe))
    rau = 0.6 + 0.3 * trocken - 0.2 * (1 - trocken) * glatt(0.3, 0.7, fein) - 0.25 * ufer
    rau = np.where(tiefe > 0.0005, 0.06, rau)
    bodennetz(h, farbe, np.clip(rau, 0.05, 1))
    teile = Teile()
    formen = [form_kugel(1, 700 + i, 0.5) for i in range(6)]
    n = 2500
    px, py = rng.random(n) * K, rng.random(n) * K
    g = rng.uniform(0.006, 0.02, n)
    farbe = mische(srgb("#5a5246"), srgb("#6e6658"), rng.random(n))
    farbe = mische(farbe, srgb("#4a3c2a"), 0.5 * rng.random(n))                    # verschmiert
    k = rng.integers(0, len(formen), n)
    for i, form in enumerate(formen):
        s = k == i
        verteilen(teile, form, np.stack([px[s], py[s], abtasten(h, px[s], py[s]) - 0.1 * g[s]], -1),
                  zufallsdrehung(rng, s.sum()), np.stack([g[s], g[s] * 0.8, g[s] * 0.6], -1) * 0.5, farbe[s] * 0.6,
                  farbe[s], 0.35)
    stab = form_stab(4, 4, 0.06)
    n = 600
    px, py = rng.random(n) * K, rng.random(n) * K
    l = rng.uniform(0.04, 0.15, n)
    d = rng.uniform(0.002, 0.004, n)
    farbe = mische(srgb("#6a5a38"), srgb("#3e3424"), rng.random(n))
    verteilen(teile, stab, np.stack([px, py, abtasten(h, px, py) + 0.0005], -1), drehung(rng.random(n) * 7,
              rng.normal(0, 0.03, n), LAENGS(n)), np.stack([l, d, d * 0.6], -1), farbe, farbe * 0.9, 0.4)
    teile.objekt("reste")
    return 0.45, 0.8


def moos(rng):
    h, farbe, k = moospolster(81)
    gross = fbm(83, [0.7, 0.25])
    h = h + 0.01 * gross
    bodennetz(h, farbe, 0.92 - 0.05 * k)
    teile = Teile()
    # Sporenkapseln auf Stielen: rotbraune Pünktchen über den Polstern
    stiel = form_halm(3, 0.2)
    n = 14000
    px, py = rng.random(n) * K, rng.random(n) * K
    behalten = rng.random(n) < glatt(0.45, 0.7, abtasten(gross, px, py)) * abtasten(k, px, py)
    px, py = px[behalten], py[behalten]
    n = len(px)
    l = rng.uniform(0.012, 0.025, n)
    farbe = mische(srgb("#8a4a26"), srgb("#b07a34"), rng.random(n))
    verteilen(teile, stiel, np.stack([px, py, abtasten(h, px, py)], -1), drehung(rng.random(n) * 7,
              rng.uniform(0.2, 0.9, n)), np.stack([np.full(n, 0.004), np.full(n, 0.004), l], -1), farbe * 0.7,
              farbe * 1.2, 0.8)
    # ein paar Nadeln und Blättchen in den Senken
    nadel = form_stab(2, 3, 0.05)
    n = 6000
    px, py = rng.random(n) * K, rng.random(n) * K
    behalten = abtasten(k, px, py) < rng.uniform(0.2, 0.6, n)
    px, py = px[behalten], py[behalten]
    n = len(px)
    l = rng.uniform(0.015, 0.03, n)
    d = np.full(n, 0.0028)
    farbe = mische(srgb("#8a5a32"), srgb("#5a4028"), rng.random(n))
    verteilen(teile, nadel, np.stack([px, py, abtasten(h, px, py) + 0.002], -1), drehung(rng.random(n) * 7,
              rng.normal(0, 0.1, n), LAENGS(n)), np.stack([l, d, d], -1), farbe, farbe, 0.8)
    teile.objekt("sporen")
    return 0.6, 0.8


def lehm(rng):
    x, y = raster()
    welle = fbm(91, [1.0, 0.35, 0.1])
    korn = rauschen(0.004, 92)
    h = 0.016 * welle + 0.0006 * korn
    for k in range(70):                                               # flache Tritte
        cx, cy = rng.random() * K, rng.random() * K
        ddx = (x - cx + K / 2) % K - K / 2
        ddy = (y - cy + K / 2) % K - K / 2
        w = rng.random() * np.pi
        u = ddx * np.cos(w) + ddy * np.sin(w)
        v = -ddx * np.sin(w) + ddy * np.cos(w)
        d = (u / 0.05) ** 2 + (v / 0.12) ** 2
        h -= 0.004 * (1 - glatt(0.6, 1.0, d))
    wx, wy = verzerrt(x, y, 0.06, [0.15, 0.05], 99)
    nummer, f1, f2, _, _ = zellen(wx, wy, 26, 93, 0.9)
    riss = f2 - f1
    vx, vy = verzerrt(x, y, 0.03, [0.06, 0.02], 100)
    nummer2, g1, g2, _, _ = zellen(vx, vy, 70, 94, 0.9)
    riss2 = np.where(je_zelle(nummer2, 95, 4900) > 0.45, g2 - g1, 1.0)
    rissig = 0.3 + 0.7 * glatt(0.2, 0.45, fbm(96, [1.0, 0.35]))
    breite = 0.003 + 0.0045 * fbm(101, [0.3, 0.1])
    tief = np.maximum(1 - glatt(0.0, breite, riss), 0.6 * (1 - glatt(0.0, 0.0018, riss2))) * rissig
    rand = glatt(breite, breite * 2, riss) * (1 - glatt(breite * 2, 0.025, riss)) * rissig
    h = h - 0.008 * tief + 0.001 * rand
    getreten = glatt(0.45, 0.75, fbm(97, [0.5, 0.15]))
    ton = je_zelle(nummer, 98, 676)
    farbe = mische(srgb("#a07a50"), srgb("#b48c62"), 0.2 * glatt(0.4, 0.8, welle) + 0.4 * ton * rissig)
    farbe = mische(farbe, srgb("#8a6440"), 0.25 * getreten)
    feucht = glatt(0.5, 0.8, fbm(103, [0.5, 0.2]))                     # große weiche Flecken, dunkler und feuchter
    farbe = mische(farbe, srgb("#7a5838"), 0.3 * feucht)
    farbe = mische(farbe, srgb("#4a3420"), 0.85 * tief)
    farbe = mische(farbe, srgb("#c4a07a"), 0.3 * rand)
    bleich = glatt(0.82, 0.95, fbm(104, [0.2, 0.06]))                  # einzelne hellere, ausgeblichene Stellen
    farbe = mische(farbe, srgb("#d8c09a"), 0.5 * bleich * (1 - tief))
    farbe *= (0.9 + 0.2 * korn)[..., None]
    bunt = rauschen(0.003, 102)
    farbe = mische(farbe, srgb("#6a5440"), 0.4 * (bunt > 0.72))
    farbe = mische(farbe, srgb("#d0b08a"), 0.3 * (bunt < 0.22))
    bodennetz(h, farbe, 0.9 - 0.22 * getreten)
    teile = Teile()
    formen = [form_kugel(1, 900 + i, 0.5) for i in range(6)]
    n = 7000
    px, py = rng.random(n) * K, rng.random(n) * K
    g = rng.uniform(0.005, 0.018, n)
    farbe = mische(srgb("#8a7a64"), srgb("#a08a6a"), rng.random(n))
    k = rng.integers(0, len(formen), n)
    for i, form in enumerate(formen):
        s = k == i
        verteilen(teile, form, np.stack([px[s], py[s], abtasten(h, px[s], py[s]) - 0.15 * g[s]], -1),
                  zufallsdrehung(rng, s.sum()), np.stack([g[s], g[s] * 0.8, g[s] * 0.6], -1) * 0.5, farbe[s] * 0.8,
                  farbe[s], 0.6)
    stab = form_stab(4, 4, 0.03)
    n = 500
    px, py = rng.random(n) * K, rng.random(n) * K
    l = rng.uniform(0.03, 0.12, n)
    d = rng.uniform(0.002, 0.004, n)
    farbe = mische(srgb("#b8a060"), srgb("#8a7440"), rng.random(n))
    verteilen(teile, stab, np.stack([px, py, abtasten(h, px, py) - d * 0.2], -1), drehung(rng.random(n) * 7,
              rng.normal(0, 0.02, n), LAENGS(n)), np.stack([l, d, d * 0.4], -1), farbe, farbe * 0.9, 0.7)
    teile.objekt("einschluss")
    return 0.5, 0.8


def pflaster(rng):
    x, y = raster()
    grund = -0.02
    h = grund + 0.004 * fbm(101, [0.3, 0.05])
    fugenmoos = glatt(0.5, 0.7, fbm(102, [0.5, 0.15]))
    farbe = mische(srgb("#4a4232"), srgb("#5e5440"), fbm(103, [0.1, 0.02]))
    farbe = mische(farbe, srgb("#3e5226"), 0.7 * fugenmoos)
    bodennetz(h, farbe, boden_rau(0.95))
    teile = Teile()
    formen = [form_kopfstein(1000 + i) for i in range(16)]
    # Reihen verschiedener Breite, darin Steine verschiedener Länge
    reihen = 32
    rh = 1 + rng.uniform(-0.15, 0.15, reihen)
    grenzen = np.concatenate([[0], np.cumsum(rh / rh.sum())]) * K
    pos, gr, dreh, stossfugen = [], [], [], []
    for j in range(reihen):
        y0, y1 = grenzen[j], grenzen[j + 1]
        anzahl = int(round(K / ((y1 - y0) * rng.uniform(1.0, 1.25))))
        l = 1 + rng.uniform(-0.3, 0.3, anzahl)
        xs = np.concatenate([[0], np.cumsum(l / l.sum())]) * K + rng.random() * K
        stossfugen.append(np.sort(xs[:-1] % K))
        for i in range(anzahl):
            fuge = rng.uniform(0.008, 0.016)
            pos.append(((xs[i] + xs[i + 1]) / 2 % K + rng.normal(0, 0.003), (y0 + y1) / 2 + rng.normal(0, 0.003)))
            gr.append(((xs[i + 1] - xs[i] - fuge) / 2, (y1 - y0 - fuge) / 2))
            dreh.append(rng.normal(0, 0.04))
    pos, gr, dreh = np.array(pos), np.array(gr), np.array(dreh)
    n = len(pos)
    hoch = rng.uniform(0.05, 0.065, n)
    paletten = [("#6c6a66", 3), ("#8e8a84", 3), ("#9c9280", 2), ("#7e6e68", 1), ("#55524e", 1.5), ("#7a7466", 2)]
    p = np.array([w for _, w in paletten]); p /= p.sum()
    basis = np.array([srgb(c) for c, _ in paletten])[rng.choice(len(paletten), n, p=p)]
    basis *= rng.uniform(0.82, 1.15, n)[:, None]
    oben = basis * 1.1
    unten = mische(basis, srgb("#4a4232"), 0.6)
    k = rng.integers(0, len(formen), n)
    z = grund - 0.3 * hoch + rng.normal(0, 0.003, n)                # Gleicher unter dem Fugengrund: fast volle Breite
    for i, form in enumerate(formen):
        s = k == i
        m = s.sum()
        skal = np.stack([gr[s, 0], gr[s, 1], hoch[s]], -1)
        verteilen(teile, form, np.stack([pos[s, 0], pos[s, 1], z[s]], -1),
                  drehung(dreh[s], rng.normal(0, 0.03, m), np.zeros(m)), skal, unten[s], oben[s], 0.85,
                  rng.uniform(0.5, 0.7, m), korn=0.6)
    # Gras und Moos in den Fugen
    halm = form_halm(3, 0.3)
    n = 80000
    px, py = rng.random(n) * K, rng.random(n) * K
    zj = np.clip(np.searchsorted(grenzen, py) - 1, 0, reihen - 1)
    rand_y = np.minimum(py - grenzen[zj], grenzen[zj + 1] - py)
    rand_x = np.empty(n)
    for j in range(reihen):
        s = zj == j
        b = np.concatenate([stossfugen[j] - K, stossfugen[j], stossfugen[j] + K])
        i = np.searchsorted(b, px[s])
        rand_x[s] = np.minimum(np.abs(px[s] - b[i - 1]), np.abs(b[i] - px[s]))
    w = (np.minimum(rand_x, rand_y) < 0.008) & (rng.random(n) < 0.1 + 0.9 * abtasten(fugenmoos, px, py))
    px, py = px[w], py[w]
    n = len(px)
    l = rng.uniform(0.02, 0.06, n)
    gruen = mische(srgb("#4f7a34"), srgb("#7a8a40"), rng.random(n))
    verteilen(teile, halm, np.stack([px, py, abtasten(h, px, py)], -1), drehung(rng.random(n) * 7,
              rng.uniform(0.5, 1.3, n)), np.stack([np.full(n, 0.006), np.full(n, 0.006), l], -1), gruen * 0.5,
              gruen * 1.1, 0.8)
    teile.objekt("steine")
    return 0.65, 0.9


def mist(rng):
    x, y = raster()
    grob = fbm(111, [0.3, 0.1, 0.04])
    h = 0.03 * grob + 0.004 * fbm(112, [0.02, 0.008])
    pegel = np.percentile(h, 10)
    tiefe = pegel - h
    h = np.maximum(h, pegel)
    farbe = mische(srgb("#4a3a22"), srgb("#3a3020"), fbm(113, [0.2, 0.05]))
    farbe = mische(farbe, srgb("#2a2414"), 0.8 * glatt(0.0, 0.004, tiefe))
    rau = np.where(tiefe > 0.0003, 0.1, 0.55 + 0.3 * glatt(0.4, 0.8, grob))
    bodennetz(h, farbe, rau)
    teile = Teile()
    stroh = glatt(0.35, 0.65, fbm(114, [0.3, 0.1]))
    zug = fbm(115, [0.4, 0.15]) * 4 * np.pi                          # Strohhalme liegen gruppenweise gleich
    # Mistfladen und Klumpen
    formen = [form_kugel(2, 1100 + i, 0.6) for i in range(8)]
    n = 2600
    px, py = rng.random(n) * K, rng.random(n) * K
    g = rng.uniform(0.03, 0.09, n)
    farbe = mische(srgb("#4a3c22"), srgb("#3a3a1e"), rng.random(n)) * rng.uniform(0.75, 1.15, n)[:, None]
    k = rng.integers(0, len(formen), n)
    for i, form in enumerate(formen):
        s = k == i
        m = s.sum()
        verteilen(teile, form, np.stack([px[s], py[s], abtasten(h, px[s], py[s]) - g[s] * 0.06], -1),
                  drehung(rng.random(m) * 7, rng.normal(0, 0.1, m)),
                  np.stack([g[s], g[s] * 0.8, g[s] * 0.25], -1) * 0.5, farbe[s] * 0.75, farbe[s], 0.5, 0.3)
    # Stroh: breit gedrückte, gebogene Halme
    halm = form_stab(4, 6, 0.2, 0.4)
    n = 30000
    px, py = rng.random(n) * K, rng.random(n) * K
    behalten = rng.random(n) < 0.12 + 0.88 * abtasten(stroh, px, py)
    px, py = px[behalten], py[behalten]
    n = len(px)
    l = np.exp(rng.uniform(np.log(0.03), np.log(0.15), n))
    d = rng.uniform(0.009, 0.014, n)
    nassheit = rng.random(n) ** 1.3
    farbe = mische(mische(srgb("#d0b25a"), srgb("#a88a40"), rng.random(n)), srgb("#5e4a24"), nassheit)
    farbe = mische(farbe, srgb("#8a8470"), 0.3 * (rng.random(n) < 0.15))            # ausgeblichen
    richtung = abtasten(zug, px, py) + rng.normal(0, 0.5, n)
    verteilen(teile, halm, np.stack([px, py, abtasten(h, px, py) + rng.uniform(0.0, 0.03, n)], -1),
              drehung(richtung, rng.normal(0, 0.3, n), rng.normal(0, 0.08, n)),      # flach liegend, etwas verdreht
              np.stack([l, d, d], -1), farbe * 0.9, farbe * rng.uniform(0.9, 1.1, n)[:, None],
              0.6 + 0.3 * (1 - nassheit))
    teile.objekt("stroh")
    return 0.6, 0.85


TEXTUREN = {"wiese": wiese, "erde": erde, "fels": fels, "kies": kies, "waldboden": waldboden, "sand": sand,
            "schlamm": schlamm, "moos": moos, "lehm": lehm, "pflaster": pflaster, "mist": mist}

# Höhe: Mittelwert nach dem Normieren (bestimmt, wer an Grenzen eher oben liegt)
HOEHE_MITTEL = {"wiese": 0.55, "erde": 0.40, "fels": 0.55, "kies": 0.50, "waldboden": 0.45, "sand": 0.45,
                "schlamm": 0.35, "moos": 0.55, "lehm": 0.40, "pflaster": 0.55, "mist": 0.45}


# ═════════════════════════════════════════════════════════════════════════════
#  Aufbereiten: Farbe abgleichen, Höhe normieren, Normalen in den Tangentenraum, Rauheit
# ═════════════════════════════════════════════════════════════════════════════

def nach_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def alte_mittel():
    """Pixelmittel je Schicht der 5e-Textur (sRGB 0..1)."""
    img = bpy.data.images.load(ALT)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    p = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(p)
    p = p.reshape(h, w, 4)[::-1]
    s = h // 11
    return {n: p[i * s:(i + 1) * s, :, :3].reshape(-1, 3).mean(0) for i, n in enumerate(NAMEN)}


def aufbereiten(name, roh, ziel_mittel):
    albedo, normale, ao, hoehe, rau, ao_anteil, n_staerke = (roh[k] for k in (
        "albedo", "normale", "ao", "hoehe", "rau", "ao_anteil", "n_staerke"))
    lin = albedo * (1 - ao_anteil * (1 - ao[..., None]))
    # Farbe: je Kanal so skalieren, dass das sRGB-Mittel dem der 5e-Textur entspricht
    for _ in range(6):
        ist = nach_srgb(lin).reshape(-1, 3).mean(0)
        lin = lin * ((ziel_mittel / np.maximum(ist, 1e-4)) ** 2.2)[None, None, :]
    farbe = nach_srgb(lin)
    # Höhe: 0,5 %–99,5 % auf 0..1, dann so gekrümmt, dass der Mittelwert passt
    lo, hi = np.percentile(hoehe, [0.5, 99.5])
    h = np.clip((hoehe - lo) / (hi - lo + 1e-9), 0, 1)
    a, b = 0.1, 10.0
    for _ in range(40):
        g = math.sqrt(a * b)
        if (h ** g).mean() > HOEHE_MITTEL[name]:
            a = g
        else:
            b = g
    h = h ** math.sqrt(a * b)
    # Normale: Rückseiten (Halme) zum Betrachter drehen, Stärke anpassen
    n = normale.copy()
    n[n[..., 2] < 0] *= -1
    n[..., :2] *= n_staerke
    n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-9
    return farbe, h, n, np.clip(rau, 0, 1)


# ═════════════════════════════════════════════════════════════════════════════
#  Schreiben: PNG (ohne Pillow) und JPEG-Vorschau (über Blender)
# ═════════════════════════════════════════════════════════════════════════════

def png(pfad, bild):
    """RGB oder RGBA als PNG; je Zeile der günstigste der fünf PNG-Filter, dann zlib."""
    hoehe, breite, kanaele = bild.shape
    roh = bild.astype(np.int16)
    links = np.pad(roh, ((0, 0), (1, 0), (0, 0)))[:, :-1]
    oben = np.pad(roh, ((1, 0), (0, 0), (0, 0)))[:-1]
    obenlinks = np.pad(roh, ((1, 0), (1, 0), (0, 0)))[:-1, :-1]
    p = links + oben - obenlinks
    pa, pb, pc = np.abs(p - links), np.abs(p - oben), np.abs(p - obenlinks)
    paeth = np.where((pa <= pb) & (pa <= pc), links, np.where(pb <= pc, oben, obenlinks))
    filter_ = [roh, roh - links, roh - oben, roh - (links + oben) // 2, roh - paeth]
    gefiltert = [(f % 256).astype(np.uint8).reshape(hoehe, -1) for f in filter_]
    kosten = np.stack([np.minimum(g, 256 - g.astype(np.int16)).sum(axis=1) for g in gefiltert])
    wahl = kosten.argmin(axis=0)
    zeilen = np.choose(wahl[:, None], gefiltert)
    daten = np.hstack([wahl.astype(np.uint8)[:, None], zeilen]).tobytes()

    def chunk(t, c):
        return struct.pack(">I", len(c)) + t + c + struct.pack(">I", zlib.crc32(t + c) & 0xffffffff)

    art = {3: 2, 4: 6}[kanaele]
    with open(pfad, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", breite, hoehe, 8, art, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(daten, 9)) + chunk(b"IEND", b""))


def webp(pfad, bild, qualitaet):
    """RGB(A) als WebP über Blender; Qualität 100 = verlustfrei. Ein verlustbehaftetes RGBA-WebP speichert Alpha
    trotzdem verlustfrei. Gibt die größte Abweichung nach dem Zurücklesen je Kanal zurück (Prüfung)."""
    h, w, k = bild.shape
    img = bpy.data.images.new("webp", w, h, alpha=k == 4)
    img.colorspace_settings.name = "Non-Color"
    px = bild[::-1].astype(np.float32) / 255
    if k == 3:
        px = np.dstack([px, np.ones((h, w), np.float32)])
    img.pixels.foreach_set(px.ravel())
    img.file_format = "WEBP"
    img.save(filepath=pfad, quality=qualitaet)
    bpy.data.images.remove(img)
    zurueck = bpy.data.images.load(pfad)
    zurueck.colorspace_settings.name = "Non-Color"
    q = np.empty(w * h * 4, np.float32)
    zurueck.pixels.foreach_get(q)
    bpy.data.images.remove(zurueck)
    q = np.round(q.reshape(h, w, 4)[::-1, :, :k] * 255)
    return np.abs(q - bild).reshape(-1, k).max(0), np.abs(q - bild).reshape(-1, k).mean(0)


def jpeg(pfad, rgb, qualitaet=88):
    h, w, _ = rgb.shape
    img = bpy.data.images.new("vorschau", w, h, alpha=False)
    px = np.dstack([rgb[::-1], np.ones((h, w))]).astype(np.float32)
    img.pixels.foreach_set(px.ravel())
    sz = bpy.context.scene
    sz.view_settings.view_transform = "Standard"
    try:
        sz.render.image_settings.media_type = "IMAGE"
    except (AttributeError, TypeError):
        pass
    sz.render.image_settings.file_format = "JPEG"
    sz.render.image_settings.quality = qualitaet
    img.save_render(pfad, scene=sz)
    bpy.data.images.remove(img)


def beleuchtet(farbe, n, hoehe):
    """Einfache Beleuchtung für die Vorschau: Sonne von links oben, etwas Himmel."""
    licht = np.array([-0.45, 0.45, 0.77])
    licht /= np.linalg.norm(licht)
    diffus = np.clip((n * licht).sum(-1), 0, 1)
    return np.clip(farbe * (0.3 + 0.95 * diffus)[..., None], 0, 1)


def verkleinern(bild, faktor):
    h, w = bild.shape[:2]
    return bild.reshape(h // faktor, faktor, w // faktor, faktor, -1).mean((1, 3))


# ═════════════════════════════════════════════════════════════════════════════

def nahtprobe(name):
    """Backt die Szene dreimal klein: wie immer, mit anderem Zufallsstart und um eine halbe Kachel verschoben. Ist die
    Textur nahtlos, weicht das verschobene Bild vom gerollten Original nicht mehr ab als zwei Läufe an derselben Stelle.
    Gibt das Verhältnis zurück (≈ 1 = nahtlos)."""
    sz = bpy.context.scene
    bilder = []
    for saat, versatz in ((1, 0.0), (2, 0.0), (3, K / 2)):
        sz.cycles.seed = saat
        sz.camera.location = (K / 2 + versatz, K / 2 + versatz, 8)
        bilder.append(kanal(backen(name), "Diffuse Color"))
    m = bilder[0].shape[0]
    gerollt = np.roll(bilder[0], (m // 2, -m // 2), (0, 1))        # Bildzeilen laufen nach unten, y nach oben
    rauschen_ = np.abs(bilder[1] - bilder[0]).mean()
    mitte = slice(m // 2 - 3, m // 2 + 3)
    naht = max(np.abs(bilder[2] - gerollt)[mitte].mean(), np.abs(bilder[2] - gerollt)[:, mitte].mean())
    return naht / max(rauschen_, 1e-9), np.abs(bilder[2] - gerollt).mean() / max(rauschen_, 1e-9)


def backe(name):
    t0 = time.time()
    rng = np.random.default_rng(NAMEN.index(name) * 1000 + 7)
    texturen_ao = {"wiese": 0.05, "erde": 0.03, "fels": 0.05, "kies": 0.03, "waldboden": 0.04, "sand": 0.02,
                   "schlamm": 0.04, "moos": 0.03, "lehm": 0.02, "pflaster": 0.05, "mist": 0.04}
    geraet = neue_szene(texturen_ao[name])
    ao_anteil, n_staerke = TEXTUREN[name](rng)
    dreiecke = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
    if "nahtprobe" in _opt:
        naht, gesamt = nahtprobe(name)
        print(f"[{name}] Nahtprobe: Mitte {naht:.2f}, ganzes Bild {gesamt:.2f} (≈ 1 = nahtlos)", flush=True)
        return
    t1 = time.time()
    d = backen(name)
    roh = {"albedo": kanal(d, "Diffuse Color"), "normale": kanal(d, "Normal", "XYZ"),
           "ao": kanal(d, "Ambient Occlusion", "R")[..., 0], "hoehe": kanal(d, "hoehe", "X")[..., 0],
           "rau": kanal(d, "rauheit", "X")[..., 0],
           "ao_anteil": ao_anteil, "n_staerke": n_staerke}
    np.savez(os.path.join(ZWISCHEN, f"{name}_{N}.npz"), **roh)
    print(f"[{name}] {geraet}, {dreiecke / 1e6:.1f} Mio. Polygone, Aufbau {t1 - t0:.0f} s, Backen {time.time() - t1:.0f} s",
          flush=True)


def packen():
    alt = alte_mittel()
    farben, normalen, vorschau, fehlt, kanaele = [], [], [], [], []
    for name in NAMEN:
        pfad = os.path.join(ZWISCHEN, f"{name}_{N}.npz")
        if not os.path.exists(pfad):
            fehlt.append(name)
            vorschau.append(np.full((N, N, 3), 0.12))
            continue
        roh = dict(np.load(pfad))
        farbe, h, n, rau = aufbereiten(name, roh, alt[name])
        print(f"[{name}] Mittel neu {np.round(farbe.reshape(-1, 3).mean(0) * 255, 1)} alt {np.round(alt[name] * 255, 1)}"
              f" | Höhe {h.mean():.2f} ± {h.std():.2f} | Rauheit {rau.mean():.2f}")
        farben.append(np.dstack([np.round(farbe * 255), 1 + np.round(h * 254)]).astype(np.uint8))   # A nie 0
        normalen.append(np.dstack([np.round((n[..., 0] * 0.5 + 0.5) * 255), np.round((n[..., 1] * 0.5 + 0.5) * 255),
                                   np.round(rau * 255)]).astype(np.uint8))
        vorschau.append(beleuchtet(farbe, n, h))
        a = N // 4                                             # vier Kanäle nebeneinander, 1:1, über die Kachelecke
        stueck = lambda b: np.roll(b, (a // 2, a // 2), (0, 1))[:a, :a]
        kanaele.append((name, np.hstack([stueck(farbe), stueck(np.dstack([h] * 3)),
                                         stueck(n * 0.5 + 0.5), stueck(np.dstack([rau] * 3))])))
    if fehlt or N != 1024:
        print("nichts geschrieben:", f"es fehlen {', '.join(fehlt)}" if fehlt else "Probegröße")
    else:
        os.makedirs(ZIEL, exist_ok=True)
        for datei, schichten, qualitaet in (("boden_farbe_hoehe", farben, 95), ("boden_normal_rauheit", normalen, 100)):
            if FORMAT == "webp":
                pfad = os.path.join(ZIEL, datei + ".webp")
                groesst, mittel = webp(pfad, np.vstack(schichten), qualitaet)
                print(f"  WebP q{qualitaet}: Abweichung je Kanal höchstens {groesst}, im Mittel {mittel.round(2)}")
            else:
                pfad = os.path.join(ZIEL, datei + ".png")
                png(pfad, np.vstack(schichten))
            print("geschrieben:", os.path.relpath(pfad, WURZEL), f"({os.path.getsize(pfad) / 1e6:.1f} MB)")
    ziel = VORSCHAU if N == 1024 else os.path.join(ZWISCHEN, "vorschau")
    os.makedirs(ziel, exist_ok=True)
    kacheln = []
    for name, v in zip(NAMEN, vorschau):
        gekachelt = np.vstack([np.hstack([v, v])] * 2)
        jpeg(os.path.join(ziel, f"{name}.jpg"), verkleinern(gekachelt, 2) if N == 1024 else gekachelt)
        kacheln.append(verkleinern(gekachelt, max(1, gekachelt.shape[0] // 512)))
    if "nah" in _opt:                                         # Nahaufnahmen über die Kachelecke (Nähte, Einzelheiten)
        for name, bild in kanaele:
            jpeg(os.path.join(ZWISCHEN, "vorschau", f"{name}_kanaele.jpg"), bild)
        for name, v in zip(NAMEN, vorschau):
            jpeg(os.path.join(ZWISCHEN, "vorschau", f"{name}_nah.jpg"), np.roll(v, (N // 4, N // 4), (0, 1))[:N // 2, :N // 2])
    leer = np.full_like(kacheln[0], 0.12)
    reihen = [np.hstack((kacheln + [leer])[r * 4:(r + 1) * 4]) for r in range(3)]
    jpeg(os.path.join(ziel, "uebersicht.jpg"), np.vstack(reihen))
    print("Vorschau:", ziel)


def main():
    os.makedirs(ZWISCHEN, exist_ok=True)
    if "nur-packen" not in _opt:
        nur = _opt.get("nur", ",".join(NAMEN)).split(",")
        for name in nur:
            backe(name)
    if "nahtprobe" in _opt:
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)
    packen()


if __name__ == "__main__":   # tools/oberflaeche/materialien_blender.py leiht sich die Werkzeuge oben
    main()
