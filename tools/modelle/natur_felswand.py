"""Felswand-Satz (Felsen zweite Generation, Etappe R4): große Wandstücke, mittlere Stücke, Gesimse, Pfeiler und ein
Schuttkegel als gewachsener Fels. Die 8 Formen aus natur_felsen.py (F1) bleiben als Füller daneben bestehen.

    blender -b --python tools/modelle/natur_felswand.py -- [--schnell] [--ohne-bilder] [--bilder=<ordner>]
            [--nur=wand_gross_1,pfeiler_1] [--ausgabe=<ordner>] [--konzept=<bild1>,<bild2>] [--nur-form] [--probe]
(--nur-form: nur formen und Vorschaubilder, ohne Karten, ohne Backen und ohne Export; --probe: statt der Vorschau nur
 zwei Bilder der gewählten Stücke im Hang, neben dem ersten Konzeptbild – für schnelle Formproben)

Schreibt game/assets/modelle/natur/natur_felswand.glb und die Strukturkarten game/assets/texturen/felswand/
(mit --ausgabe=<ordner> stattdessen alles in diesen Ordner, z. B. für einen Lauf auf einem anderen Rechner):
    felswand_normal.webp     Normale (Tangentenraum, OpenGL: G+ = UV-v nach oben), verlustfrei
    felswand_hoehlung.webp   R Umgebungsverdeckung (1 = frei), G Kante (1 = scharfe, vorstehende Kante),
                             B Rinne (1 = Kluft oder Fuge), verlustfrei
Beide 4096 × 2048 Pixel. Die großen Stücke haben je ein Feld 1024 × 1024, die mittleren Stücke und Gesimse je 512 × 512
(Lage in FELDER). Kanäle wie bei F1, damit das Fels-Material (F2) die Karten ohne Änderung liest.

Bauweise (R4b: ein Felskörper aus einem Dichtefeld statt gestapelter Kluftblöcke, die wie Mauerwerk aussahen):
  * Jedes Stück ist ein Feld f(x, y, z) (< 0 = Fels) auf einem Gitter von 5–8 cm; die Fläche entsteht mit Surface Nets.
    Das Feld ist eine Summe aus
      - Grundkörper: Wand mit nach hinten geneigter Front, deren Enden in den Hang zurücklaufen und niedriger werden;
        Pfeiler mit gerundetem Querschnitt, nach oben schlanker; Gesims als Platte,
      - Schichtung: Bänke unterschiedlicher Dicke, um 20–40° geneigt (Fallen und Fallrichtung je Stück, SCHICHTUNG).
        Weiche Lagen wittern zurück (Kehle, die harte Bank darüber hängt über), Bankkanten gerundet – ein
        Treppenprofil entlang der Fallrichtung, mit Rauschen in Dicke und Rücksprung,
      - große Formen aus niederfrequentem Rauschen (Rippen, Rinnen) und ein kleiner Versatz und eine leichte Drehung je
        Hauptkluft-Säule, damit große, zusammenhängende Flächen mit eigenen Winkeln entstehen,
      - Oberkante: jede Säule endet an einer Schichtfläche (Schichtköpfe, Sägezahn wie in Konzept 1), gerundet,
      - Hauptklüfte (Voronoi-Kanten in der Ebene quer zur Kluftachse) als schmale Kerben, nur wenige tief (Spalten),
      - unten eine Schutthalde aus kantigen Steinen und einzelnen Brocken, die flach in den Boden ausläuft.
  * Danach Glätten, Voxel-Remesh (3–5 cm) und feines Aufrauen: Haarrisse (3D-Voronoi, ein Netz aus feinen Linien, nur
    stellenweise), die Hauptklüfte als feine Linie, Feinschichtung in einzelnen Bänken, flache Abplatzungen, Körnung.
    Dann unten abgeschnitten (offen), auf die Spielstufen vereinfacht und gebacken.
  * Rückseite (R7): Die Spielkamera dreht in 90°-Schritten, ein einzeln gestempeltes Stück wird auch von hinten gesehen. Darum
    schließt jedes Stück hinten mit einer groben Bruchfläche knapp vor y = T/2 (Felder von etwa 3 m mit eigenem Rücksprung und
    eigener Neigung, leicht nach vorn geneigt, oben gerundet); die feine Oberfläche ist dort gedämpft, damit das Vereinfachen
    hinten wenige Dreiecke lässt, und ihre UV-Inseln sind im Atlas auf 45 % verkleinert (die Schauseite behält ihre Auflösung).

Knoten „<Form>_<Stufe>“ (0 fein, 1 mittel, 2 grob), Ursprung Mitte der Grundfläche, z = 0 Unterkante, nichts darunter;
im Spiel Y oben, Schauseite +Z, die Rückseite (−Z) gehört in den Hang, ist aber geschlossen (R7). Unterseite offen (außer bei
den Gesimsen, deren Unterseite beim Stapeln als Überhang zu sehen ist).
Extras je Knoten: „kennung“, „stufe“, „schicht_neigung“ (Neigung der Schichtfugen in der Wandebene in Grad, + = nach +X
steigend; für Shader-Bänder, die der Schichtung folgen sollen). Ein Material „Fels“ (Platzhalter, das Spiel setzt das
Fels-Material). Vertexfarbe COLOR_0 (linear) wie F1: R Höhlung (Verdeckung × (1 − 0,6 · Rinne)), G Kante, B Höhe im Stück
(0 unten … 1 oben), A Tiefe (0 vorn … 1 hinten). UV0: Atlas der Strukturkarten. Kein UV2.
"""

import math
import os
import random
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oberflaeche"))
import bausteine as bs  # noqa: E402
import bodentexturen_blender as bt  # noqa: E402  (nur der WebP-Schreiber)

NAME = "natur_felswand"
TEXTUREN = os.path.join(bs.WURZEL, "game", "assets", "texturen", "felswand")
ATLAS = (4096, 2048)
RUECK_UV = 0.45          # so groß bleiben die UV-Inseln der Rückseite im Atlas (Maßstab, R7)

# Kennung, Knotenname, Dreiecke je Stufe 0/1/2, Voxelgröße (m) des hohen Netzes
FORMEN = (
    ("wand_gross_1", "WandGross1", (11000, 3600, 1100), 0.045),     # gebankte Wand 20 × 10 × 16
    ("wand_gross_2", "WandGross2", (11000, 3600, 1100), 0.045),     # hohe, säulige Wand 14 × 10 × 20
    ("wand_gross_3", "WandGross3", (11000, 3600, 1100), 0.045),     # Wand mit zwei Absätzen 18 × 11 × 12
    ("wand_mittel_1", "WandMittel1", (5000, 1700, 500), 0.035),     # steil geschichtete Wand 9 × 6 × 8
    ("wand_mittel_2", "WandMittel2", (5000, 1700, 500), 0.035),     # Eckstück 6 × 6 × 10, rechte Seite offen sichtbar
    ("wand_mittel_3", "WandMittel3", (5000, 1700, 500), 0.035),     # Absatz 8 × 6,5 × 5
    ("gesims_1", "Gesims1", (3000, 1000, 300), 0.03),               # eine Bank 10 × 4 × 1,8 zum Stapeln
    ("gesims_2", "Gesims2", (3000, 1000, 300), 0.03),               # zwei Bänke 8 × 4 × 3, obere kragt vor
    ("pfeiler_1", "Pfeiler1", (6000, 2000, 600), 0.035),            # freistehender Turm 4 × 4 × 17–19 (mit Schuttfuß)
    ("pfeilerbuendel", "Pfeilerbuendel", (11000, 3600, 1100), 0.045),  # vier Pfeiler vor einer Rückwand 10 × 8 × 15
    ("schuttkegel", "Schuttkegel", (6000, 2000, 600), 0.03),        # Schutthalde am Wandfuß 15 × 9 × 4
)
KNOTEN = {k: n for k, n, _, _ in FORMEN}
DREIECKE = {k: d for k, _, d, _ in FORMEN}
VOXEL = {k: v for k, _, _, v in FORMEN}
# Atlasfelder (Pixel links, oben, Größe): große Stücke 1024, mittlere und Gesimse 512
FELDER = {
    "wand_gross_1": (0, 0, 1024), "wand_gross_2": (1024, 0, 1024), "wand_gross_3": (2048, 0, 1024),
    "pfeilerbuendel": (3072, 0, 1024), "pfeiler_1": (0, 1024, 1024), "schuttkegel": (1024, 1024, 1024),
    "wand_mittel_1": (2048, 1024, 512), "wand_mittel_2": (2560, 1024, 512), "wand_mittel_3": (2048, 1536, 512),
    "gesims_1": (2560, 1536, 512), "gesims_2": (3072, 1024, 512),
}

# Gesteine: Anteile der Lagenarten (hart, mittel, weich), Bankdicke (m) und Rücksprung (m, + = zurück) je Lagenart,
# Rundung der Bankkanten (m), Anteil fein geschichteter Bänke und (wo angegeben) Anteil offener Bankfugen
GESTEIN = {
    # Grauwacke, Schiefer-Sandstein-Wechsel (Konzept 1): kräftige Bänke, weiche Zwischenlagen mit Kehlen
    "grauwacke": dict(anteile=(0.45, 0.35, 0.20), dicke=((0.5, 1.6), (0.18, 0.55), (0.1, 0.3)),
                      rueck=((-0.15, 0.08), (0.08, 0.32), (0.45, 1.05)), rund=(0.18, 0.42), lamellen=0.65),
    # Trachyt (Konzept 2): dicke, harte Bänke, kaum Kehlen; die Säulen machen die Klüfte
    "trachyt": dict(anteile=(0.78, 0.18, 0.04), dicke=((2.2, 5.0), (0.8, 1.6), (0.2, 0.4)),
                    rueck=((-0.1, 0.1), (0.05, 0.22), (0.3, 0.6)), rund=(0.15, 0.32), lamellen=0.0, fuge_offen=0.35),
    # Sandstein, dünnbankig (Konzept 4 links): viele dünne Bänke, regelmäßigere Kehlen
    "sandstein": dict(anteile=(0.42, 0.38, 0.20), dicke=((0.4, 1.3), (0.18, 0.55), (0.1, 0.25)),
                      rueck=((-0.1, 0.1), (0.08, 0.3), (0.35, 0.85)), rund=(0.14, 0.32), lamellen=0.6),
}

# Je Stück: Grundkörper (art), Maße B × T × H (m, T einschließlich Schuttfuß), Gestein, Schichtung (fallen in Grad,
# richtung = Fallrichtung in Grad: 0 fällt nach +X, 90 in den Hang, −90 zum Betrachter), Kluftachse (fallen, richtung;
# ohne Angabe senkrecht zur Schichtung), Hauptkluft-Zellen (m), Anteil tiefer Spalten, Versatz der Säulen (m),
# große Formen (m), Rippen und Rinnen entlang der Wand (m), Anteil offener Bankfugen, Front-Neigung (m zurück je m Höhe), Schutthalde (Höhe von–bis, m), Gitter (m),
# Rückseite (Neigung nach vorn in m je m Höhe, größter Abstand von der Hinterkante in m; R7)
STANDARD = dict(lehne=0.22, kluft=5.0, anis=1.0, spalten=0.2, spalt_tiefe=2.2, spalt_breite=0.4, relief=0.45,
                gross=1.1, rippe=1.0, kluft_rund=0.3, fuge_offen=0.7, welle=0.3, schutt=(0.5, 1.6), raster=0.08, hoehe=(0.8, 1.0), enden=(1.0, 1.0),
                offen_rechts=False, stufen=(), risse=1.8, bruch=0.13, rund_oben=0.6, kluft_achse=None,
                rueck=(0.03, 0.8))
STUECKE = {
    "wand_gross_1": dict(art="wand", B=20, T=10, H=16, gestein="grauwacke", fallen=32, richtung=200, lehne=0.3,
                         kluft=6.5, spalten=0.22, relief=0.3, gross=0.6, rippe=1.3, schutt=(0.7, 2.0), risse=2.0),
    "wand_gross_2": dict(art="wand", B=14, T=10, H=20, gestein="trachyt", fallen=22, richtung=-30, kluft_achse=(5, 60),
                         lehne=0.14, kluft=3.4, spalten=0.4, spalt_tiefe=3.0, relief=0.7, gross=0.9, rippe=1.6, hoehe=(0.7, 1.0),
                         rund_oben=0.8, risse=2.4),
    "wand_gross_3": dict(art="wand", B=18, T=11, H=12, gestein="sandstein", fallen=22, richtung=-12, lehne=0.2,
                         kluft=6.0, spalten=0.15, relief=0.3, gross=0.7, rippe=1.1, stufen=((4.2, 2.2), (8.3, 2.0)), risse=2.0,
                         hoehe=(0.88, 1.0)),
    "wand_mittel_1": dict(art="wand", B=9, T=6, H=8, gestein="grauwacke", fallen=30, richtung=-5, lehne=0.22,
                          kluft=4.0, relief=0.3, gross=0.6, rippe=1.0, schutt=(0.5, 1.3), raster=0.06),
    "wand_mittel_2": dict(art="wand", B=6, T=6, H=10, gestein="grauwacke", fallen=26, richtung=160, lehne=0.18,
                          kluft=3.6, relief=0.4, gross=0.6, rippe=0.9, enden=(1.0, 0.0), offen_rechts=True, schutt=(0.5, 1.3),
                          raster=0.06),
    "wand_mittel_3": dict(art="wand", B=8, T=6.5, H=5, gestein="sandstein", fallen=20, richtung=20, lehne=0.2,
                          kluft=4.0, relief=0.3, gross=0.5, rippe=0.8, stufen=((2.4, 2.0),), schutt=(0.4, 1.0), raster=0.06,
                          hoehe=(0.9, 1.0)),
    "gesims_1": dict(art="gesims", B=10, T=4, H=1.8, gestein="sandstein", fallen=3, richtung=90, kluft=3.5, rueck=(0.0, 0.6),
                     relief=0.15, gross=0.35, rippe=0.2, welle=0.08, schutt=None, raster=0.05, rund_oben=0.35, spalten=0.0,
                     bruch=0.05),
    "gesims_2": dict(art="gesims2", B=8.4, T=4, H=2.95, gestein="sandstein", fallen=3, richtung=90, kluft=3.2, rueck=(0.0, 0.6),
                     relief=0.15, gross=0.3, rippe=0.2, welle=0.08, schutt=None, raster=0.05, rund_oben=0.35, spalten=0.0,
                     bruch=0.05),
    "pfeiler_1": dict(art="pfeiler", B=4, T=4, H=20, gestein="trachyt", fallen=15, richtung=45, kluft_achse=(3, 0),
                      kluft=3.3, spalten=0.35, spalt_tiefe=1.2, relief=0.25, gross=0.4, rippe=0.3, schutt=(0.3, 0.7),
                      raster=0.06, hoehe=(0.92, 1.0), risse=1.4, kluft_rund=0.15),
    "pfeilerbuendel": dict(art="buendel", B=10, T=8, H=15, gestein="trachyt", fallen=18, richtung=-60,
                           kluft_achse=(4, 30), kluft=2.8, spalten=0.35, relief=0.4, gross=0.6, rippe=1.0, lehne=0.1,
                           schutt=(0.4, 1.2), raster=0.07, hoehe=(0.88, 1.0)),
    "schuttkegel": dict(art="schutt", B=12.5, T=9, H=4, gestein="grauwacke", fallen=30, richtung=0, raster=0.06,
                        schutt=None, rueck=(0.25, 1.8)),
}
# Fallen und Fallrichtung der Schichtung je Stück (für Shader-Bänder: Neigung in der Wandebene als Extra im Knoten)
SCHICHTUNG = {k: (v["fallen"], v["richtung"]) for k, v in STUECKE.items()}
BOESCHUNG = math.tan(math.radians(36))     # Schutthalden


def argumente():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    wert = lambda n, d: next((x.split("=", 1)[1] for x in a if x.startswith(f"--{n}=")), d)  # noqa: E731
    nur = wert("nur", None)
    konzept = wert("konzept", "")
    return dict(schnell="--schnell" in a, nur_form="--nur-form" in a or "--probe" in a, probe="--probe" in a,
                ohne_bilder="--ohne-bilder" in a, bilder=wert("bilder", bs.VORSCHAU),
                nur=nur.split(",") if nur else None, ausgabe=wert("ausgabe", None),
                konzept=[k for k in konzept.split(",") if k])


# ── 1. Rauschen und Zellen (numpy, auf ganzen Punktmengen) ───────────────────────────────────────────────────────────
_M1, _M2, _S33 = np.uint64(0xFF51AFD7ED558CCD), np.uint64(0xC4CEB9FE1A85EC53), np.uint64(33)


def zufall(*zahlen, k=0):
    """Gleichverteilt in [0, 1) je Ganzzahl-Tupel (z. B. Zellkennung) und Kanal k; gleiche Eingabe, gleiche Zahl."""
    with np.errstate(over="ignore"):
        h = np.uint64((0x9E3779B97F4A7C15 * (k + 1)) & 0xFFFFFFFFFFFFFFFF)
        for zahl in zahlen:
            h = h ^ (np.asarray(zahl, np.int64).astype(np.uint64) + np.uint64(0x632BE59BD9B4E019))
            h = h ^ (h >> _S33)
            h = h * _M1
            h = h ^ (h >> _S33)
            h = h * _M2
            h = h ^ (h >> _S33)
    return (h >> np.uint64(11)).astype(np.float64) * (1.0 / 9007199254740992.0)


class Rauschen:
    """Gradientenrauschen (Perlin) in 3D, Werte etwa −1 … 1."""

    def __init__(self, saat):
        r = np.random.default_rng(saat)
        self.p = np.concatenate([r.permutation(256)] * 2).astype(np.intp)
        g = r.normal(size=(256, 3))
        self.g = (g / np.linalg.norm(g, axis=1, keepdims=True)).astype(np.float32)

    def __call__(self, x, y, z):
        x, y, z = (np.asarray(a, np.float64) for a in (x, y, z))
        xi, yi, zi = np.floor(x), np.floor(y), np.floor(z)
        xf, yf, zf = (x - xi).astype(np.float32), (y - yi).astype(np.float32), (z - zi).astype(np.float32)
        X, Y, Z = (xi.astype(np.int64) & 255), (yi.astype(np.int64) & 255), (zi.astype(np.int64) & 255)
        u, v, w = (t * t * t * (t * (t * 6 - 15) + 10) for t in (xf, yf, zf))
        p, g = self.p, self.g
        A, B = p[X] + Y, p[X + 1] + Y
        AA, AB, BA, BB = p[A] + Z, p[A + 1] + Z, p[B] + Z, p[B + 1] + Z

        def d(h, dx, dy, dz):
            G = g[p[h]]
            return G[..., 0] * dx + G[..., 1] * dy + G[..., 2] * dz

        def l(t, a, b):
            return a + t * (b - a)
        y1 = l(v, l(u, d(AA, xf, yf, zf), d(BA, xf - 1, yf, zf)), l(u, d(AB, xf, yf - 1, zf), d(BB, xf - 1, yf - 1, zf)))
        y2 = l(v, l(u, d(AA + 1, xf, yf, zf - 1), d(BA + 1, xf - 1, yf, zf - 1)),
               l(u, d(AB + 1, xf, yf - 1, zf - 1), d(BB + 1, xf - 1, yf - 1, zf - 1)))
        return l(w, y1, y2).astype(np.float64) * 1.5


def fbm(r, x, y, z, oktaven=3, lac=2.03, gain=0.5):
    s, a, f, norm = 0.0, 1.0, 1.0, 0.0
    for i in range(oktaven):
        s = s + a * r(x * f + 17.3 * i, y * f, z * f)
        norm += a
        a *= gain
        f *= lac
    return s / norm


def _tabelle(i0, i1, saat, zitter, dims):
    """Zellpunkte (Lage in der Zelle 0 … 1) für alle Zellen von i0 − 3 … i1 + 3 je Achse."""
    achsen = [np.arange(a - 3, b + 4) for a, b in zip(i0, i1)]
    gitter = np.meshgrid(*achsen, indexing="ij")
    return [0.5 + zitter * (zufall(*gitter, saat, k=1 + d) - 0.5) for d in range(dims)], [a - 3 for a in i0]


def zellen2(u, v, groesse, saat, zitter=0.9):
    """Voronoi in der Ebene (u, v) in Metern. Gibt (Abstand zur nächsten Zellgrenze in m, Zelle u, Zelle v,
    Zellpunkt u, Zellpunkt v in m) zurück; die Zellgrenzen sind gerade Linien (Klüfte)."""
    U, V = u / groesse, v / groesse
    iu, iv = np.floor(U).astype(np.int64), np.floor(V).astype(np.int64)
    fu, fv = U - iu, V - iv
    (tu, tv), (u0, v0) = _tabelle((iu.min(), iv.min()), (iu.max(), iv.max()), saat, zitter, 2)
    beste = np.full(U.shape, 1e9)
    mu, mv = np.zeros(U.shape, np.int64), np.zeros(U.shape, np.int64)
    au, av = np.zeros(U.shape), np.zeros(U.shape)
    for du in (-1, 0, 1):
        for dv in (-1, 0, 1):
            cu, cv = iu + du - u0, iv + dv - v0
            pu, pv = du + tu[cu, cv], dv + tv[cu, cv]
            d = (pu - fu) ** 2 + (pv - fv) ** 2
            m = d < beste
            beste = np.where(m, d, beste)
            mu, mv = np.where(m, du, mu), np.where(m, dv, mv)
            au, av = np.where(m, pu, au), np.where(m, pv, av)
    kante = np.full(U.shape, 1e9)
    for du in range(-2, 3):
        for dv in range(-2, 3):
            if du == 0 and dv == 0:
                continue
            ou, ov = mu + du, mv + dv
            cu, cv = iu + ou - u0, iv + ov - v0
            pu, pv = ou + tu[cu, cv], ov + tv[cu, cv]
            nu, nv = pu - au, pv - av
            ln = np.sqrt(nu * nu + nv * nv) + 1e-9
            kante = np.minimum(kante, (((au + pu) * 0.5 - fu) * nu + ((av + pv) * 0.5 - fv) * nv) / ln)
    return kante * groesse, iu + mu, iv + mv, (iu + au) * groesse, (iv + av) * groesse


def zellen3(x, y, z, groesse, saat, zitter=0.9):
    """Voronoi im Raum. Gibt (Abstand zur Grenzfläche zum zweitnächsten Zellpunkt in m, Zellkennung als Tupel,
    Zellpunkt (x, y, z) in m, Abstand zum Zellpunkt in m) zurück."""
    P = [np.asarray(a, np.float64) / groesse for a in (x, y, z)]
    I = [np.floor(a).astype(np.int64) for a in P]
    F = [a - i for a, i in zip(P, I)]
    t, o = _tabelle([i.min() for i in I], [i.max() for i in I], saat, zitter, 3)
    d1, d2 = np.full(P[0].shape, 1e9), np.full(P[0].shape, 1e9)
    a = [np.zeros(P[0].shape) for _ in range(3)]
    b = [np.zeros(P[0].shape) for _ in range(3)]
    ka = [np.zeros(P[0].shape, np.int64) for _ in range(3)]
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                c = (I[0] + dx - o[0], I[1] + dy - o[1], I[2] + dz - o[2])
                p = [dx + t[0][c], dy + t[1][c], dz + t[2][c]]
                d = (p[0] - F[0]) ** 2 + (p[1] - F[1]) ** 2 + (p[2] - F[2]) ** 2
                m1 = d < d1
                m2 = ~m1 & (d < d2)
                for j in range(3):
                    b[j] = np.where(m1, a[j], np.where(m2, p[j], b[j]))
                    a[j] = np.where(m1, p[j], a[j])
                    ka[j] = np.where(m1, I[j] + (dx, dy, dz)[j], ka[j])
                d2 = np.where(m1, d1, np.where(m2, d, d2))
                d1 = np.where(m1, d, d1)
    ln = np.sqrt(sum((b[j] - a[j]) ** 2 for j in range(3))) + 1e-9
    mitte = [(I[j] + a[j]) * groesse for j in range(3)]
    return (d2 - d1) / (2 * ln) * groesse, tuple(ka), mitte, np.sqrt(d1) * groesse


def glatt(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def smin(a, b, k):
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.minimum(a, b) - h * h * k * 0.25


def smax(a, b, k):
    return -smin(-a, -b, k)


def richtung(fallen, azimut):
    """Normale einer Fläche, die um `fallen` Grad in Richtung `azimut` (0 = +X, 90 = +Y in den Hang) einfällt."""
    a, p = math.radians(fallen), math.radians(azimut)
    return np.array([math.sin(a) * math.cos(p), math.sin(a) * math.sin(p), math.cos(a)])


def basis(n):
    """Zwei Achsen senkrecht zu n: e1 waagrecht (Streichen), e2 = n × e1."""
    e1 = np.cross((0.0, 0.0, 1.0), n)
    e1 = e1 / np.linalg.norm(e1) if np.linalg.norm(e1) > 1e-6 else np.array([1.0, 0.0, 0.0])
    return e1, np.cross(n, e1)


def schicht_neigung(form):
    """Neigung der Schichtfugen in der Wandebene (Grad, + = nach +X steigend)."""
    n = richtung(*SCHICHTUNG[form])
    return round(math.degrees(math.atan2(-n[0], n[2])), 1)


# ── 2. Grundkörper ───────────────────────────────────────────────────────────────────────────────────────────────────
def rundbox(x, y, hx, hy, r):
    qx, qy = np.abs(x) - hx + r, np.abs(y) - hy + r
    return np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2) + np.minimum(np.maximum(qx, qy), 0) - r


class Wand:
    """Front bei y = yv (Fuß), nach hinten geneigt; Enden laufen in den Hang zurück und werden niedriger."""
    oben_schicht = True
    zellhoehe = False

    def __init__(self, fels, B, H, yv, yh, lehne, enden=(1.0, 1.0), offen_rechts=False, x0=0.0, schutt=True):
        self.f, self.B, self.H, self.yv, self.yh, self.lehne = fels, B, H, yv, yh, lehne
        self.enden, self.offen_rechts, self.x0, self.schutt = enden, offen_rechts, x0, schutt
        self.unten = None

    def ende_rueck(self, X):
        x, rand = X - self.x0, min(3.5, self.B * 0.25)
        r = self.enden[0] * 3.2 * np.clip((-self.B / 2 + rand - x) / rand, 0, 1) ** 1.5
        if not self.offen_rechts:
            r = r + self.enden[1] * 3.2 * np.clip((x - (self.B / 2 - rand)) / rand, 0, 1) ** 1.5
        return r

    def seite(self, X, Y, Z, R):
        x = X - self.x0
        f = np.minimum(self.yv + self.lehne * np.maximum(Z, 0) + self.ende_rueck(X) + R, self.yh - 1.2) - Y
        # seitlich nicht über B/2 hinaus (Rücksprünge wirken mit), das offene Ende darf 0,4 m vorspringen
        rechts = x - self.B / 2 + (np.maximum(R, -0.4) if self.offen_rechts else 0.3 * np.maximum(R, 0))
        f = smax(f, rechts, 0.5)
        return smax(f, -x - self.B / 2 + 0.3 * np.maximum(R, 0), 0.5)

    def hoehe(self, X, Y):
        x, rand = X - self.x0, min(4.0, self.B * 0.3)
        t = self.H * (1 + 0.05 * self.f.r[2](x / 3.0, 0.5, 2.5))
        for sg, st in ((-1, self.enden[0]), (1, 0.0 if self.offen_rechts else self.enden[1])):
            d = self.B / 2 - sg * x
            t = t * (1 - 0.45 * st * np.clip((rand - d) / rand, 0, 1) ** 1.2)
        return np.minimum(t, self.H)

    def fuss(self, X, Y):
        return self.yv + self.ende_rueck(X) - Y


class Pfeiler:
    """Säule mit gerundetem Querschnitt, nach oben schlanker. Der Kopf jeder Teilsäule (Hauptkluft-Zelle) ist eigen hoch,
    ohne Einrasten auf die (beim Trachyt sehr dicken) Bänke."""
    oben_schicht = False
    zellhoehe = True

    def __init__(self, fels, cx, cy, B, T, H, verj=0.14, rund=0.7, schutt=True):
        self.f, self.cx, self.cy, self.B, self.T, self.H = fels, cx, cy, B, T, H
        self.verj, self.rund, self.schutt, self.unten = verj, rund, schutt, None

    def seite(self, X, Y, Z, R):
        k = 1 - self.verj * np.clip(Z / self.H, 0, 1)
        return rundbox(X - self.cx, Y - self.cy, self.B / 2 * k, self.T / 2 * k, self.rund) + R

    def hoehe(self, X, Y):
        """Kopf zum Rand hin niedriger (bis 83 %): keine dünnen Randsplitter über der Mitte."""
        r = np.hypot((X - self.cx) / (self.B / 2), (Y - self.cy) / (self.T / 2))
        return self.H * (1 - 0.12 * np.clip(r, 0, 1.2) ** 2)

    def fuss(self, X, Y):
        return rundbox(X - self.cx, Y - self.cy, self.B / 2, self.T / 2, self.rund)


class Platte:
    """Eine Bank (Gesims): Front bei yv, Unterseite geschlossen."""
    oben_schicht = False
    zellhoehe = False

    def __init__(self, fels, B, z0, z1, yv, yh, enden=0.6):
        self.f, self.B, self.z0, self.z1, self.yv, self.yh, self.enden = fels, B, z0, z1, yv, yh, enden
        self.unten, self.schutt = z0, False

    def seite(self, X, Y, Z, R):
        rand = min(2.5, self.B * 0.3)
        e = self.enden * 1.6 * np.clip((np.abs(X) - (self.B / 2 - rand)) / rand, 0, 1) ** 1.5
        f = np.minimum(self.yv + e + R, self.yh - 0.8) - Y
        return smax(f, np.abs(X) - self.B / 2 + 0.3 * np.maximum(R, 0), 0.4)

    def hoehe(self, X, Y):
        return self.z1 + 0.05 * self.f.r[2](X / 2.0, Y / 2.0, 0.3)

    def fuss(self, X, Y):
        return self.yv - Y


# ── 3. Felskörper als Dichtefeld ─────────────────────────────────────────────────────────────────────────────────────
class Fels:
    def __init__(self, form, saat, schnell=False):
        self.form, self.saat = form, saat
        p = self.p = dict(STANDARD, **STUECKE[form])
        self.z = random.Random(saat)
        self.r = [Rauschen(saat * 13 + i) for i in range(10)]
        self.nb = richtung(p["fallen"], p["richtung"])
        self.nj = richtung(*p["kluft_achse"]) if p["kluft_achse"] else self.nb
        self.e1, self.e2 = basis(self.nj)
        # entlang der Wand, aber längs der Kluftachse fest: Koordinate für Rippen und seitliche Schwankung
        q = np.array([1.0, 0.0, 0.0]) - self.nj[0] * self.nj
        self.eq = q / np.linalg.norm(q)
        B, T, H = p["B"], p["T"], p["H"]
        self.yh = T / 2
        self.raster = p["raster"] * (1.5 if schnell else 1.0)
        self.laenge = (p["schutt"][1] / BOESCHUNG + 0.3) if p["schutt"] else 0.4     # Schuttfuß vor der Wand
        unten = -0.8 if p["art"] not in ("gesims", "gesims2") else -0.3
        rand = {"pfeiler": self.laenge + 0.6, "buendel": 2.0}.get(p["art"], 1.6)
        # vorn genug Rand, damit Halde und Brocken auslaufen und nicht am Gitterrand abgeschnitten werden
        vorn = {"pfeiler": self.laenge + 0.6, "buendel": self.laenge + 0.6, "wand": 1.2}.get(p["art"], 0.4)
        self.bereich = ((-B / 2 - rand, B / 2 + rand), (-T / 2 - vorn, self.yh + vorn + 0.2), (unten, H + 1.0))
        self.koerper = self.koerper_anlegen()
        self.schichten_anlegen()
        self.stufen_s = [(float(np.dot(self.nb, (0.0, self.vorn_bei(zs), zs))), r) for zs, r in p["stufen"]]

    def vorn_bei(self, z):
        k = self.koerper[0]
        return getattr(k, "yv", 0.0) + getattr(k, "lehne", 0.0) * z

    def koerper_anlegen(self):
        p, f = self.p, self
        B, T, H, art = p["B"], p["T"], p["H"], p["art"]
        laenge = self.laenge
        if art == "wand":
            return [Wand(f, B, H, -T / 2 + laenge, self.yh, p["lehne"], p["enden"], p["offen_rechts"])]
        if art == "gesims":
            return [Platte(f, B, 0.0, H, -T / 2 + 0.2, self.yh)]
        if art == "gesims2":                 # untere Bank, darüber eine vorkragende
            return [Platte(f, B - 0.4, 0.0, 1.35, -T / 2 + 1.0, self.yh), Platte(f, B, 1.25, H, -T / 2 + 0.2, self.yh)]
        if art == "pfeiler":
            return [Pfeiler(f, 0, 0, B, T, H)]
        if art == "buendel":                 # vier Pfeiler in einem flachen Bogen vor einer niedrigeren Rückwand
            teile = [Wand(f, B, 9.0, 0.6, self.yh, p["lehne"], (0.8, 0.8))]
            for mx, my, b, t, h in ((-3.4, -1.0, 2.8, 3.2, 12.5), (-0.6, -1.8, 3.2, 3.4, 15.0),
                                    (2.3, -1.4, 2.6, 3.0, 13.5), (4.0, 0.3, 2.0, 2.4, 10.0)):
                teile.append(Pfeiler(f, mx, my, b, t, h, verj=0.1, rund=0.6))
            return teile
        return []                            # Schuttkegel: eigenes Feld

    def schichten_anlegen(self):
        g = GESTEIN[self.p["gestein"]]
        (x0, x1), (y0, y1), (z0, z1) = self.bereich
        ecken = [float(np.dot(self.nb, (x, y, h))) for x in (x0, x1) for y in (y0, y1) for h in (z0, z1)]
        s, ende = min(ecken) - 3.0, max(ecken) + 3.0
        grenzen, rueck, rund, breite, lamelle, arten, vorher = [s], [], [], [], [], [], -1
        while s < ende:
            a = self.z.choices((0, 1, 2), weights=g["anteile"])[0]
            if a == 2 and vorher == 2:
                a = 1
            d = self.z.uniform(*g["dicke"][a])
            rueck.append(self.z.uniform(*g["rueck"][a]))
            rund.append(self.z.uniform(*g["rund"]) * (0.35 if a == 2 else 1.0) * min(1.0, d / 0.8))
            breite.append(min(0.45 * d, 0.45))
            lamelle.append(self.z.uniform(0.09, 0.22) if a < 2 and self.z.random() < g["lamellen"] else 0.0)
            s += d
            grenzen.append(s)
            arten.append(a)
            vorher = a
        self.grenzen, self.rueck, self.rund = np.array(grenzen), np.array(rueck), np.array(rund)
        self.rundbreite, self.lamelle = np.array(breite), np.array(lamelle)
        # offene Bankfugen (gerundete Kanten, Rille): an weichen Lagen immer, sonst nur ein Teil – geschlossene Fugen
        # lassen mehrere Lagen als eine dicke Bank stehen (unterschiedlich dicke Bänke statt gleicher Bretter)
        offen = [self.z.random() < g.get("fuge_offen", self.p["fuge_offen"]) for _ in grenzen]
        for i, a in enumerate(arten):
            if a == 2:
                offen[i] = offen[i + 1] = True
        self.offen = np.array(offen, np.float64)

    # Bausteine des Feldes
    def schicht_s(self, X, Y, Z):
        s = X * self.nb[0] + Y * self.nb[1] + Z * self.nb[2]
        return s + self.p["welle"] * (fbm(self.r[1], X / 7.0, Y / 7.0, Z / 7.0, 2)
                                      + 0.4 * self.r[2](X / 2.2 + 3.1, Y / 2.2, Z / 2.2))

    def laengs(self, X, Y, Z):
        return X * self.eq[0] + Y * self.eq[1] + Z * self.eq[2]

    def bank(self, s):
        return np.clip(np.searchsorted(self.grenzen, s, side="right") - 1, 0, len(self.rueck) - 1)

    def schicht_rueck(self, s, w):
        """Treppenprofil quer zur Schichtung: Rücksprung je Bank und gerundete Bankkanten, beides entlang der Wand
        (w) schwankend – mal scharfe, mal tief ausgewitterte Kante."""
        i = self.bank(s)
        quer = np.clip(0.5 + 0.6 * self.r[3](w / 5.0, i * 1.37, 0.5), 0, 1)
        r = self.rueck[i] * (0.45 + 1.1 * quer)
        kante = np.clip(0.55 + 0.75 * self.r[3](w / 3.0, i * 2.11 + 40.0, 1.5), 0.1, 1.4)
        unten = self.offen[i] * (1 - np.clip((s - self.grenzen[i]) / self.rundbreite[i], 0, 1)) ** 2
        oben = self.offen[i + 1] * (1 - np.clip((self.grenzen[i + 1] - s) / self.rundbreite[i], 0, 1)) ** 2
        return r + self.rund[i] * kante * np.maximum(unten, oben)

    def kluftebene(self, X, Y, Z):
        u = X * self.e1[0] + Y * self.e1[1] + Z * self.e1[2]
        v = X * self.e2[0] + Y * self.e2[1] + Z * self.e2[2]
        return u, v

    def hauptkluefte(self, u, v):
        a = self.p["anis"]
        kante, zu, zv, cu, cv = zellen2(u, v / a, self.p["kluft"], self.saat + 11)
        return kante, zu, zv, cu, cv * a

    def oberkante(self, kp, zu, zv, cu, cv):
        """Schichtfläche (s-Wert), an der eine Säule (Hauptkluft-Zelle) oben endet: die Bankgrenze unter der Höhe, die
        der Körper an der Säulenachse hat (je Säule etwas niedriger, manchmal eine Bank mehr)."""
        e1, e2, nj = self.e1, self.e2, self.nj
        bx, by, bz = cu * e1[0] + cv * e2[0], cu * e1[1] + cv * e2[1], cu * e1[2] + cv * e2[2]
        lo, hi = self.p["hoehe"]
        fak = lo + (hi - lo) * zufall(zu, zv, self.saat, k=7) ** 0.7
        px, py = bx, by
        for _ in range(3):
            ht = kp.hoehe(px, py) * fak
            t = (ht - bz) / nj[2]
            px, py = bx + t * nj[0], by + t * nj[1]
        sc = px * self.nb[0] + py * self.nb[1] + ht * self.nb[2]
        i = np.searchsorted(self.grenzen, sc, side="right") - 1
        i = np.clip(i, 0, len(self.grenzen) - 2)
        tief = sc - self.grenzen[i] > 1.5                  # höchstens 1,5 m unter die Zielhöhe einrasten
        i = i + ((zufall(zu, zv, self.saat, k=8) < 0.3) | tief)
        return self.grenzen[np.clip(i, 0, len(self.grenzen) - 1)]

    def feld(self, X, Y, Z):
        if self.p["art"] == "schutt":
            return self.feld_schutt(X, Y, Z)
        p = self.p
        s = self.schicht_s(X, Y, Z)
        u, v = self.kluftebene(X, Y, Z)
        kante, zu, zv, cu, cv = self.hauptkluefte(u, v)
        w = self.laengs(X, Y, Z)
        # Rücksprung: Schichtung, Säulenversatz mit leichter Drehung, große Formen (vor allem entlang der Wand), Absätze
        R = self.schicht_rueck(s, w)
        R = R + p["relief"] * (2 * zufall(zu, zv, self.saat, k=5) - 1)
        R = R + 0.07 * (2 * zufall(zu, zv, self.saat, k=6) - 1) * (u - cu) \
              + 0.05 * (2 * zufall(zu, zv, self.saat, k=9) - 1) * (v - cv)
        R = R + p["gross"] * fbm(self.r[0], X / 9.0, Y / 9.0, Z / 24.0, 3)
        rippe = self.rippe(w, zu, zv)
        R = R + rippe
        # Säulenkanten an den Hauptklüften gerundet und ausgewittert (vorspringende Säule: runde Kante)
        R = R + p["kluft_rund"] * (1 - np.clip(kante / 0.6, 0, 1)) ** 2
        for sk, rk in self.stufen_s:
            R = R + rk * glatt(sk - 0.25, sk + 0.25, s)
        f = np.full(X.shape, 1e3)
        for kp in self.koerper:
            fk = kp.seite(X, Y, Z, R)
            if kp.oben_schicht:                 # Rinnen enden tiefer als Rippen
                fk = smax(fk, s - self.oberkante(kp, zu, zv, cu, cv) + 0.8 * np.maximum(rippe, 0), p["rund_oben"])
            if kp.zellhoehe:                   # ebener Kopf je Teilsäule, Höhe aus der Wölbung an der Säulenmitte
                lo, hi = p["hoehe"]
                fak = lo + (hi - lo) * zufall(zu, zv, self.saat, k=7) ** 0.7
                mx = cu * self.e1[0] + cv * self.e2[0]
                my = cu * self.e1[1] + cv * self.e2[1]
                fk = smax(fk, Z - kp.hoehe(mx, my) * fak, p["rund_oben"])
                fk = smax(fk, Z - kp.H, 0.5)
            else:
                fk = smax(fk, Z - kp.hoehe(X, Y), 0.5)
            if kp.unten is not None:
                fk = smax(fk, kp.unten + 0.04 * self.r[4](X / 1.5, Y / 1.5, 0.7) - Z, 0.25)
            f = smin(f, fk, 0.5)
        # Hauptklüfte als Kerbe, die nach innen schmaler wird; nur wenige tief (Spalten)
        m = 0.5 + 0.55 * self.r[5](X / 7.0, Y / 7.0, Z / 7.0)
        tief = glatt(1 - p["spalten"] - 0.05, 1 - p["spalten"] + 0.08, m) if p["spalten"] > 0 else 0.0
        D = 0.25 + (p["spalt_tiefe"] - 0.25) * tief
        hw = 0.045 + (p["spalt_breite"] / 2 - 0.045) * tief
        kerbe = np.minimum(hw * np.clip(1 + f / D, 0, 1) - kante, f + D)
        f = smax(f, kerbe, 0.05)
        # gebrochene Flächen: jedes Feld zwischen den Haarrissen etwas vor oder zurück und leicht gedreht
        nah = np.abs(f) < 0.5
        if nah.any():
            f[nah] = f[nah] + self.bruchflaechen(X[nah], Y[nah], Z[nah])
        # Schutthalde am Fuß
        if p["schutt"]:
            f = self.halde(f, X, Y, Z, rippe, rippe + p["relief"] * (2 * zufall(zu, zv, self.saat, k=5) - 1))
        # geschlossene Rückseite zuletzt (auch die Halde reicht sonst nach hinten durch); der Pfeiler ist rundum zu
        if p["art"] != "pfeiler":
            f = smax(f, self.rueckseite(X, Y, Z), 0.45)
        return f

    def rueckseite(self, X, Y, Z):
        """Feld der Rückseite (R7, < 0 = davor): grobe Bruchfläche knapp vor der Hinterkante y = T/2, aus Feldern von etwa
        3 m (in der Ebene x, z), jedes mit eigenem Rücksprung und eigener Neigung, dazu sanfte Wellen; nach oben leicht nach
        vorn geneigt, aber nie weiter als rueck[1] vor der Hinterkante (sonst würde die Wand oben zu dünn)."""
        lehne, tiefst = self.p["rueck"]
        _, zx, zz, cx, cz = zellen2(X, Z, 3.0, self.saat + 61)
        d = 0.4 * zufall(zx, zz, self.saat, k=16) \
            + 0.18 * (2 * zufall(zx, zz, self.saat, k=17) - 1) * (X - cx) \
            + 0.14 * (2 * zufall(zx, zz, self.saat, k=18) - 1) * (Z - cz)
        _, kx, kz, _, _ = zellen2(X + 0.5 * Z, Z, 1.3, self.saat + 62)        # kleinere Ausbrüche in den Feldern
        d = d + 0.12 * zufall(kx, kz, self.saat, k=19)
        d = d + 0.25 * (fbm(self.r[1], X / 6.0, 3.7, Z / 6.0, 2) + 0.6)
        yb = self.yh - 0.12 - lehne * np.maximum(Z, 0) - np.clip(d, 0.0, 0.75)
        return Y - np.maximum(yb, self.yh - tiefst)

    def rippe(self, w, zu, zv):
        """Rippen und Rinnen: je Hauptkluft-Säule ein eigener Vorsprung (ebene Fläche, Stufe an der Kluft), dazu ein
        sanfter Wechsel entlang der Wand (w); + = Rinne."""
        stufe = 2 * zufall(zu, zv, self.saat, k=13) - 1
        sanft = np.clip(2.0 * fbm(self.r[9], w / 6.5, 0.37, 0.71, 2), -1, 1)
        return self.p["rippe"] * (0.55 * stufe + 0.45 * sanft)

    def halde(self, f, X, Y, Z, rippe, versatz):
        """Schutthalde vor dem Fuß (36°), folgt der Front samt Säulenversatz (versatz, + = zurück); in Rinnen höher."""
        p = self.p
        h0, h1 = p["schutt"]
        fuss = np.full(X.shape, 1e3)
        for kp in self.koerper:
            if kp.schutt:
                fuss = np.minimum(fuss, kp.fuss(X, Y) + versatz)
        ht = h0 + (h1 - h0) * np.clip(0.5 + 0.7 * self.r[6](X / 6.0, 0.3, 1.1)
                                      + 0.5 * np.maximum(rippe, 0) / max(p["rippe"], 0.1), 0, 1)
        aus = np.ones(X.shape)
        for kp in self.koerper:              # an den Wandenden läuft die Halde aus (kein Schnitt am Gitterrand)
            if isinstance(kp, Wand):
                aus = aus * glatt(0.0, 3.0, kp.B / 2 - 0.3 - np.abs(X - kp.x0))
        ht = ht * aus
        zt = ht - BOESCHUNG * np.maximum(fuss, 0)
        nah = (Z < h1 + 1.2) & (Z > zt - 0.6)
        ft = Z - zt
        if nah.any():
            ft[nah] = ft[nah] + self.steine(X[nah], Y[nah], Z[nah], 0.45, 0.09) * aus[nah] + 0.1 * (1 - aus[nah])
            ft[nah] = smin(ft[nah], self.brocken(X[nah], Y[nah], Z[nah], zt[nah], ht[nah]), 0.12)
        return smin(f, ft, 0.35)

    def brocken(self, X, Y, Z, zboden, ht):
        """Einzelne kantige Brocken auf der Halde: Voronoi-Zelle im Grundriss (ebene Seiten), oben gerundet und flach,
        nur ein Teil der Zellen trägt einen Brocken."""
        k, zx, zy, cx, cy = zellen2(X, Y, 2.2, self.saat + 51)
        # nicht an den auslaufenden Wandenden und nicht vor dem Haldenfuß
        da = (zufall(zx, zy, self.saat, k=14) < 0.3) & (ht > 0.45) & (zboden > -0.2)
        r = 0.35 + 0.45 * zufall(zx, zy, self.saat, k=15)
        dz = (Z - np.maximum(zboden, 0) - 0.15 * r) / 0.65
        rund = np.sqrt((X - cx) ** 2 + (Y - cy) ** 2 + dz * dz) - r
        return np.where(da, np.maximum(rund, 0.1 - k), 1e3)

    def bruchflaechen(self, X, Y, Z):
        """Versatz (m, + = zurück) je Feld der Haarrisse (dieselben Zellen wie die Risse in feinheit)."""
        _, kid, mitte, _ = zellen3(X, Y, Z, self.p["risse"], self.saat + 21)
        a = self.p["bruch"]
        d = a * (2 * zufall(*kid, self.saat, k=11) - 1)
        for j, P in enumerate((X, Y, Z)):
            d = d + 0.05 * (2 * zufall(*kid, self.saat, k=12 + j) - 1) * (P - mitte[j])
        return d * self.rissmaske(X, Y, Z)

    def rissmaske(self, X, Y, Z):
        """Wo Haarrisse und gebrochene Flächen sind (gut die Hälfte), dazwischen große ruhige Flächen."""
        return glatt(-0.3, 0.15, self.r[7](X / 7.0, Y / 7.0, Z / 7.0))

    def steine(self, X, Y, Z, groesse, tiefe):
        """Kantige, flach liegende Steine: Fugen zwischen Voronoi-Zellen, jede Zelle etwas höher oder tiefer."""
        k, kid, _, _ = zellen3(X, Y, Z * 1.7, groesse, self.saat + 31)
        hub = (zufall(*kid, self.saat, k=3) - 0.5) * 2 * tiefe
        return tiefe * (1 - glatt(0.0, 0.08, k)) - hub

    def feld_schutt(self, X, Y, Z):
        """Schuttkegel: Fächer mit Böschung um 33°, Spitze hinten an der Wand; kantige Steine, am Fuß größer."""
        B, T, H = self.p["B"], self.p["T"], self.p["H"]
        ry = (T / 2 - Y) / T
        rx = np.abs(X) / (B / 2)
        r = np.sqrt(ry * ry + (rx * 0.85) ** 2)
        auf = np.clip((1 - r) * 4, 0, 1)            # 1 auf dem Kegel, 0 außerhalb: dort keine Steinhaut am Boden
        zc = H * np.maximum(0.0, 1 - r) ** 1.15 + 0.15 * self.r[0](X / 2.5, Y / 2.5, 3.3) * auf - 0.06 * (1 - auf)
        f = Z - zc
        nah = (np.abs(f) < 0.8) & (auf > 0)
        if nah.any():
            fuss = np.clip(1 - zc[nah] / H, 0, 1)
            klein = self.steine(X[nah], Y[nah], Z[nah], 0.4, 0.08)
            k, kid, _, _ = zellen3(X[nah], Y[nah], Z[nah] * 1.6, 0.9, self.saat + 41)
            gross = 0.16 * (1 - glatt(0.0, 0.12, k)) - (zufall(*kid, self.saat, k=4) - 0.5) * 0.36
            f[nah] = f[nah] + np.where(fuss > 0.55, gross + 0.4 * klein, klein) * auf[nah]
        return smax(f, self.rueckseite(X, Y, Z), 0.6)          # hinten geschlossen (R7)

    # Gitter und Fläche
    def gitter(self):
        h = self.raster
        (x0, x1), (y0, y1), (z0, z1) = self.bereich
        xs, ys, zs = (np.arange(a, b + h, h) for a, b in ((x0, x1), (y0, y1), (z0, z1)))
        feld = np.empty((len(xs), len(ys), len(zs)), np.float32)
        schritt = max(1, int(450000 / (len(xs) * len(ys))))
        for k in range(0, len(zs), schritt):
            X, Y, Z = np.meshgrid(xs, ys, zs[k:k + schritt], indexing="ij")
            feld[:, :, k:k + schritt] = self.feld(X.ravel(), Y.ravel(), Z.ravel()).reshape(X.shape)
        feld[0], feld[-1], feld[:, 0], feld[:, -1], feld[:, :, 0], feld[:, :, -1] = 1, 1, 1, 1, 1, 1
        return feld, np.array((x0, y0, z0)), h

    # feine Oberfläche nach dem Remesh
    def feinheit(self, co, no):
        """Verschiebung in m entlang der Normale: Haarrisse, Hauptklüfte als Linie, Feinschichtung, Abplatzungen,
        Körnung."""
        X, Y, Z = co[:, 0], co[:, 1], co[:, 2]
        p = self.p
        d = np.zeros(len(X))
        # Haarrisse: ein Netz aus feinen Linien (3D-Voronoi), nur stellenweise; leichte Facette je Rissfeld
        k, kid, mitte, _ = zellen3(X, Y, Z, p["risse"], self.saat + 21)
        auslaufen = glatt(-0.15, 0.3, self.r[6](X / 1.3 + 9.1, Y / 1.3, Z / 1.3))   # Risse enden, keine Vielecke
        d -= 0.05 * (1 - glatt(0.0, 0.035, k)) ** 1.5 * self.rissmaske(X, Y, Z) * auslaufen
        if p["art"] != "schutt":
            u, v = self.kluftebene(X, Y, Z)
            kante = self.hauptkluefte(u, v)[0]
            d -= 0.04 * (1 - glatt(0.0, 0.04, kante))
            # Feinschichtung in einzelnen Bänken (schmale Rillen parallel zur Schichtung)
            s = self.schicht_s(X, Y, Z)
            i = self.bank(s)
            lam = self.lamelle[i]
            t = np.where(lam > 0, (s - self.grenzen[i]) / np.maximum(lam, 1e-3), 0.5)
            t = t + 0.35 * self.r[5](X / 1.6 + 4.4, Y / 1.6, Z / 1.6)                      # Lagen wellig
            fr = t - np.floor(t)
            d -= np.where(lam > 0, 0.035 * (1 - np.clip(np.minimum(fr, 1 - fr) / 0.22, 0, 1)) ** 2, 0.0)
        # Abplatzungen: flache Schalen
        _, _, _, r1 = zellen3(X, Y, Z, 0.7, self.saat + 23)
        d -= 0.02 * (1 - np.clip(r1 / 0.5, 0, 1)) ** 2
        # Körnung
        d += 0.006 * fbm(self.r[8], X / 0.12, Y / 0.12, Z / 0.12, 3)
        # auf der Rückseite (R7) gedämpft: grob, damit das Vereinfachen dort wenige Dreiecke lässt
        if p["art"] != "pfeiler":
            d *= 1 - 0.8 * glatt(0.35, 0.75, no[:, 1]) * glatt(self.yh - 1.9, self.yh - 1.0, Y)
        return d


def flaeche_aus_feld(f, ursprung, h):
    """Surface Nets: je Gitterzelle mit Vorzeichenwechsel ein Punkt (Mittel der Kantenschnitte), je Gitterkante mit
    Wechsel ein Viereck aus den vier Zellen um sie. Gibt (Punkte, Vierecke) zurück, Flächen nach außen gerichtet."""
    n = f.shape
    zn = (n[0] - 1, n[1] - 1, n[2] - 1)
    innen = f < 0
    idx, pos, kanten = [], [], []
    for achse in range(3):
        a0, a1 = [slice(None)] * 3, [slice(None)] * 3
        a0[achse], a1[achse] = slice(0, -1), slice(1, None)
        f0, f1 = f[tuple(a0)], f[tuple(a1)]
        I = np.nonzero(innen[tuple(a0)] != innen[tuple(a1)])
        t = f0[I] / (f0[I] - f1[I])
        p = np.stack(I, 1).astype(np.float64)
        p[:, achse] += t
        b, c = [x for x in range(3) if x != achse]
        zellen = []
        for db, dc in ((1, 1), (0, 1), (0, 0), (1, 0)):
            zi = [I[0], I[1], I[2]]
            zi[b], zi[c] = I[b] - db, I[c] - dc
            lin = np.ravel_multi_index(tuple(zi), zn)
            idx.append(lin)
            pos.append(p)
            zellen.append(lin)
        q = np.stack(zellen, 1)
        aussen_plus = f0[I] < 0                  # innen am unteren Ende: Normale zeigt in +achse
        if achse == 1:
            aussen_plus = ~aussen_plus           # Ebene (x, z): x × z = −y
        q = np.where(aussen_plus[:, None], q, q[:, ::-1])
        kanten.append(q)
    idx = np.concatenate(idx)
    pos = np.concatenate(pos)
    zellen, umkehr = np.unique(idx, return_inverse=True)
    anzahl = np.bincount(umkehr)
    punkte = np.stack([np.bincount(umkehr, weights=pos[:, j]) / anzahl for j in range(3)], 1)
    punkte = ursprung + punkte * h
    vier = np.searchsorted(zellen, np.concatenate(kanten))
    return punkte, vier


def netz_aus(punkte, vierecke, name):
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(punkte))
    me.vertices.foreach_set("co", punkte.astype(np.float32).ravel())
    me.loops.add(vierecke.size)
    me.loops.foreach_set("vertex_index", vierecke.astype(np.int32).ravel())
    me.polygons.add(len(vierecke))
    me.polygons.foreach_set("loop_start", np.arange(0, vierecke.size, 4, dtype=np.int32))
    me.update(calc_edges=True)
    me.validate()
    return me


# ── 4. Hohes Netz ────────────────────────────────────────────────────────────────────────────────────────────────────
def hoch_netz(form, saat, schnell):
    t0 = time.time()
    fels = Fels(form, saat, schnell)
    feld, ursprung, h = fels.gitter()
    t1 = time.time()
    punkte, vier = flaeche_aus_feld(feld, ursprung, h)
    del feld
    me = netz_aus(punkte, vier, f"_Hoch_{form}")
    obj = bpy.data.objects.new(f"_Hoch_{form}", me)
    bpy.context.scene.collection.objects.link(obj)
    gl = obj.modifiers.new("Glatt", "SMOOTH")
    gl.factor, gl.iterations = 0.5, 2
    anwenden(obj)
    mod = obj.modifiers.new("Remesh", "REMESH")
    mod.mode = "VOXEL"
    mod.voxel_size = VOXEL[form] * (1.8 if schnell else 1.0)
    mod.adaptivity = 0.0
    anwenden(obj)
    aufrauen(obj, fels)
    abschneiden(obj, form)
    for p in obj.data.polygons:
        p.use_smooth = True
    print(f"  {form}: Feld {feld_text(fels)} in {t1 - t0:.0f} s, Netz in {time.time() - t1:.0f} s", flush=True)
    return obj


def feld_text(fels):
    (x0, x1), (y0, y1), (z0, z1) = fels.bereich
    return f"{(x1 - x0) / fels.raster:.0f}×{(y1 - y0) / fels.raster:.0f}×{(z1 - z0) / fels.raster:.0f} ({fels.raster * 100:.0f} cm)"


def anwenden(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    neu = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    alt = obj.data
    obj.modifiers.clear()
    name = alt.name
    obj.data = neu
    bpy.data.meshes.remove(alt)
    neu.name = name


def aufrauen(obj, fels):
    """Feine Oberfläche (Fels.feinheit) entlang der Punktnormalen."""
    me = obj.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(n, 3)
    no = np.empty(n * 3)
    me.vertices.foreach_get("normal", no)
    no = no.reshape(n, 3)
    d = np.empty(n)
    for a in range(0, n, 200000):
        d[a:a + 200000] = fels.feinheit(co[a:a + 200000], no[a:a + 200000])
    co += no * d[:, None]
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def unten_offen(form):
    return not form.startswith("gesims")


def hinterkante(form):
    """y der Hinterkante (Blender +Y, Hangseite); die Rückseite liegt knapp davor (R7)."""
    return STUECKE[form]["T"] / 2


def abschneiden(obj, form):
    """Unter z = 0 weg (Unterseite offen); die Rückseite ist geschlossen (R7), dort wird nicht geschnitten."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    if unten_offen(form):
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, 0), plane_no=(0, 0, 1), clear_inner=True)
    else:                                    # geschlossene Unterseite: der Remesher reicht ein halbes Voxel unter 0
        tief = min(v.co.z for v in bm.verts)
        bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, -tief))
    nur_groesste_insel(bm, obj.name)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def nur_groesste_insel(bm, name=""):
    """Nur das größte zusammenhängende Teil behalten: lose Splitter, schwebende Scherben, dünne Haut am Gitterboden.
    Gesucht wird über die Punktobjekte selbst (nach bisect_plane haben neue Punkte keinen gültigen Index)."""
    gesehen, inseln = set(), []
    for v in bm.verts:
        if v in gesehen:
            continue
        stapel, insel = [v], []
        gesehen.add(v)
        while stapel:
            w = stapel.pop()
            insel.append(w)
            for e in w.link_edges:
                o = e.other_vert(w)
                if o not in gesehen:
                    gesehen.add(o)
                    stapel.append(o)
        inseln.append(insel)
    if len(inseln) < 2:
        return
    inseln.sort(key=len, reverse=True)
    weg = [v for i in inseln[1:] for v in i]
    print(f"  {name}: {len(inseln) - 1} lose Teile entfernt ({len(weg)} Punkte, größtes {len(inseln[1])}, "
          f"behalten {len(inseln[0])})", flush=True)
    bmesh.ops.delete(bm, geom=weg, context="VERTS")


# ── 5. Spielnetze ────────────────────────────────────────────────────────────────────────────────────────────────────
def vereinfachen(quelle, name, ziel, form):
    me = quelle.data.copy()
    me.name = name
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    dreiecke = sum(len(p.vertices) - 2 for p in me.polygons)
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = min(1.0, ziel / max(1, dreiecke))
    mod.use_collapse_triangulate = True
    anwenden(obj)
    obj.modifiers.new("Tri", "TRIANGULATE")
    anwenden(obj)
    for v in obj.data.vertices:              # Collapse legt Punkte am offenen Rand auch knapp unter den Schnitt
        if v.co.z < 0 and unten_offen(form):
            v.co.z = 0.0
    # Collapse kann doppelte Flächen und lose Kanten hinterlassen und kleine Teile abtrennen: entfernen, sonst meldet
    # der glTF-Export „not valid“ und bereinigt selbst
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    nur_groesste_insel(bm, name)
    lose = [v for v in bm.verts if not v.link_faces]
    if lose:
        bmesh.ops.delete(bm, geom=lose, context="VERTS")
    lose = [e for e in bm.edges if not e.link_faces]
    if lose:
        bmesh.ops.delete(bm, geom=lose, context="EDGES")
    bm.to_mesh(obj.data)
    bm.free()
    if obj.data.validate():
        print(f"  {name}: doppelte oder ungültige Flächen entfernt", flush=True)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def rueckseite_verkleinern(obj, form):
    """UV-Inseln, die nach hinten zeigen und an der Hinterkante liegen (R7), um ihre Mitte auf RUECK_UV verkleinern; das
    folgende Packen behält die Verhältnisse, so bleibt der Schauseite mehr Atlas. Inseln: Flächen, die über eine Kante mit
    gleichen UV an beiden Enden zusammenhängen. Gibt die Zahl der verkleinerten Inseln zurück."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.active
    bm.faces.ensure_lookup_table()
    eltern = list(range(len(bm.faces)))

    def wurzel(i):
        while eltern[i] != i:
            eltern[i] = eltern[eltern[i]]
            i = eltern[i]
        return i
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        f1, f2 = e.link_faces
        a = {l.vert: l[uv].uv.copy() for l in f1.loops if l.vert in e.verts}
        b = {l.vert: l[uv].uv.copy() for l in f2.loops if l.vert in e.verts}
        if all((a[v] - b[v]).length < 1e-6 for v in e.verts):
            r1, r2 = wurzel(f1.index), wurzel(f2.index)
            if r1 != r2:
                eltern[r1] = r2
    inseln = {}
    for f in bm.faces:
        inseln.setdefault(wurzel(f.index), []).append(f)
    yh, anzahl = hinterkante(form), 0
    for flaechen in inseln.values():
        groesse = sum(f.calc_area() for f in flaechen)
        if groesse <= 0:
            continue
        ny = sum(f.normal.y * f.calc_area() for f in flaechen) / groesse
        my = sum(f.calc_center_median().y * f.calc_area() for f in flaechen) / groesse
        if ny > 0.6 and my > yh - 2.0:
            schleifen = [l for f in flaechen for l in f.loops]
            mitte = sum((l[uv].uv for l in schleifen), Vector((0.0, 0.0))) / len(schleifen)
            for l in schleifen:
                l[uv].uv = mitte + (l[uv].uv - mitte) * RUECK_UV
            anzahl += 1
    bm.to_mesh(me)
    bm.free()
    return anzahl


def dreiecke_hinten(obj, form):
    """Dreiecke auf der Rückseite (zeigt nach hinten, liegt an der Hinterkante) und alle – zur Kontrolle (R7)."""
    yh = hinterkante(form)
    hinten = sum(1 for p in obj.data.polygons if p.normal.y > 0.6 and p.center.y > yh - 1.5)
    return hinten, len(obj.data.polygons)


def aufklappen(obj, form):
    """UV: Smart Project, in das Atlasfeld der Form gepackt."""
    for o in bpy.context.scene.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.004, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    klein = rueckseite_verkleinern(obj, form)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    print(f"  {obj.name}: {klein} UV-Inseln der Rückseite auf {RUECK_UV:.0%} verkleinert", flush=True)
    px, py, groesse = FELDER[form]
    rand = 4 / groesse                                       # 4 Pixel Abstand zum Nachbarfeld
    uv = obj.data.uv_layers.active.data
    for l in uv:
        u, v = l.uv
        u = rand + u * (1 - 2 * rand)
        v = rand + v * (1 - 2 * rand)
        l.uv = ((px + u * groesse) / ATLAS[0], 1 - (py + (1 - v) * groesse) / ATLAS[1])   # Pixel-Zeile 0 oben


# ── 6. Backen ────────────────────────────────────────────────────────────────────────────────────────────────────────
def bild(name, farbe):
    img = bpy.data.images.new(name, ATLAS[0], ATLAS[1], alpha=False, float_buffer=True)
    img.colorspace_settings.name = "Non-Color"
    img.generated_color = farbe
    return img


def stoff_mit_knoten(name):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    return m


def back_stoff(img):
    m = stoff_mit_knoten("_Backziel")
    knoten = m.node_tree.nodes.new("ShaderNodeTexImage")
    knoten.image = img
    m.node_tree.nodes.active = knoten
    return m


def hoch_stoff():
    """Emission = (Kante, Rinne, 0) aus der Pointiness des hohen Netzes."""
    m = stoff_mit_knoten("_Hoch")
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    aus = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    kante = nt.nodes.new("ShaderNodeMapRange")
    kante.inputs["From Min"].default_value, kante.inputs["From Max"].default_value = 0.52, 0.6
    rinne = nt.nodes.new("ShaderNodeMapRange")
    rinne.inputs["From Min"].default_value, rinne.inputs["From Max"].default_value = 0.48, 0.41
    nt.links.new(geo.outputs["Pointiness"], kante.inputs["Value"])
    nt.links.new(geo.outputs["Pointiness"], rinne.inputs["Value"])
    comb = nt.nodes.new("ShaderNodeCombineColor")
    nt.links.new(kante.outputs["Result"], comb.inputs[0])
    nt.links.new(rinne.outputs["Result"], comb.inputs[1])
    nt.links.new(comb.outputs[0], em.inputs["Color"])
    nt.links.new(em.outputs[0], aus.inputs["Surface"])
    return m


def backen(paare, schnell):
    """paare: [(form, hoch, spiel)] – backt alle Formen in drei Atlanten; gibt (NORMAL, AO, EMIT) als numpy zurück."""
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    print("Cycles backt auf:", bs.cycles_geraet(szene))
    szene.cycles.samples = 16 if schnell else 48
    welt = bpy.data.worlds.new("_Backwelt")
    try:
        welt.light_settings.distance = 2.5           # Verdeckung: Fugen und Kehlen dunkel, große Flächen frei
    except AttributeError:
        print("WARNUNG: AO-Abstand nicht einstellbar, Vorgabe bleibt")
    szene.world = welt
    bake = szene.render.bake
    bake.use_selected_to_active = True
    bake.use_clear = False
    bake.margin = 4
    bake.margin_type = "EXTEND"
    bake.target = "IMAGE_TEXTURES"
    bilder = {"NORMAL": bild("_normal", (0.5, 0.5, 1.0, 1.0)), "AO": bild("_ao", (1, 1, 1, 1)),
              "EMIT": bild("_kante", (0, 0, 0, 1))}
    stoffe = {k: back_stoff(v) for k, v in bilder.items()}
    hs = hoch_stoff()
    for form, hoch, spiel in paare:
        hoch.data.materials.clear()
        hoch.data.materials.append(hs)
        bake.cage_extrusion = 0.25
        bake.max_ray_distance = 0.6
        for o in szene.objects:                  # nur das Paar rendert (alle Stücke stehen im Ursprung)
            o.hide_render = o not in (hoch, spiel)
        for art in ("NORMAL", "AO", "EMIT"):
            spiel.data.materials.clear()
            spiel.data.materials.append(stoffe[art])
            for o in szene.objects:
                o.select_set(o in (hoch, spiel))
            bpy.context.view_layer.objects.active = spiel
            if art == "NORMAL":
                bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT")
            else:
                bpy.ops.object.bake(type=art)
        print(f"  gebacken: {spiel.name}", flush=True)
    aus = {}
    for k, img in bilder.items():
        px = np.empty(ATLAS[0] * ATLAS[1] * 4, np.float32)
        img.pixels.foreach_get(px)
        aus[k] = px.reshape(ATLAS[1], ATLAS[0], 4)[::-1]                    # Zeile 0 = oben
    return aus


def texturen_schreiben(gebacken, ordner):
    os.makedirs(ordner, exist_ok=True)
    normal = np.clip(np.round(gebacken["NORMAL"][:, :, :3] * 255), 0, 255).astype(np.uint8)
    ao = gebacken["AO"][:, :, 0]
    kr = gebacken["EMIT"]
    hoehlung = np.dstack([ao, kr[:, :, 0], kr[:, :, 1]])
    hoehlung = np.clip(np.round(hoehlung * 255), 0, 255).astype(np.uint8)
    pfade = []
    for datei, daten in (("felswand_normal.webp", normal), ("felswand_hoehlung.webp", hoehlung)):
        pfad = os.path.join(ordner, datei)
        groesst, _ = bt.webp(pfad, daten, 100)
        print(f"geschrieben: {pfad} (Abweichung nach Zurücklesen höchstens {groesst.max()})")
        pfade.append(pfad)
    return pfade


def vertexfarben(obj, gebacken):
    """COLOR_0 je Ecke: R Höhlung, G Kante (aus den gebackenen Karten an der UV), B Höhe, A Tiefe."""
    me = obj.data
    attr = me.color_attributes.new("Farbe", "FLOAT_COLOR", "CORNER")
    uv = np.empty(len(me.loops) * 2)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    vi = np.empty(len(me.loops), np.int64)
    me.loops.foreach_get("vertex_index", vi)
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)[vi]
    px = np.clip((uv[:, 0] * ATLAS[0]).astype(int), 0, ATLAS[0] - 1)
    py = np.clip(((1 - uv[:, 1]) * ATLAS[1]).astype(int), 0, ATLAS[1] - 1)
    ao, kr = gebacken["AO"], gebacken["EMIT"]
    zmax = max(1e-6, co[:, 2].max())
    y0, y1 = co[:, 1].min(), co[:, 1].max()
    farbe = np.stack([ao[py, px, 0] * (1 - 0.6 * kr[py, px, 1]), kr[py, px, 0], np.clip(co[:, 2] / zmax, 0, 1),
                      (co[:, 1] - y0) / max(1e-6, y1 - y0)], axis=1).astype(np.float32)
    attr.data.foreach_set("color", farbe.ravel())
    me.color_attributes.active_color = attr


def platzhalter_stoff():
    m = stoff_mit_knoten("Fels")
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.32, 0.31, 0.29, 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    return m


def exportieren(pfad, objekte):
    """GLB – Y oben, Vorderseite +Z, nur diese Objekte (wie bausteine.exportieren, aber mit freiem Pfad)."""
    os.makedirs(os.path.dirname(pfad), exist_ok=True)
    for o in bpy.context.scene.objects:
        o.select_set(o in objekte)
    bpy.ops.export_scene.gltf(
        filepath=pfad, export_format="GLB", use_selection=True, export_yup=True, export_apply=True,
        export_extras=True, export_normals=True, export_texcoords=True, export_animations=False,
        export_skins=False, export_morph=False, export_lights=False, export_cameras=False,
        export_image_format="NONE", export_vertex_color="ACTIVE", export_all_vertex_colors=False)
    return pfad


# ── 7. Vorschau ──────────────────────────────────────────────────────────────────────────────────────────────────────
def vorschau_stoff(ordner):
    """Grauer Sandstein mit den gebackenen Karten, Bänder nach Weltlage und Moos auf Oberkanten – grob so, wie F2 und
    R1 sie zeigen werden (nur Vorschau, nicht im Spiel)."""
    m = stoff_mit_knoten("_Vorschau")
    nt = m.node_tree
    L = nt.links.new
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.9
    nimg = nt.nodes.new("ShaderNodeTexImage")
    nimg.image = bpy.data.images.load(os.path.join(ordner, "felswand_normal.webp"))
    nimg.image.colorspace_settings.name = "Non-Color"
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    L(nimg.outputs["Color"], nmap.inputs["Color"])
    L(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    himg = nt.nodes.new("ShaderNodeTexImage")
    himg.image = bpy.data.images.load(os.path.join(ordner, "felswand_hoehlung.webp"))
    himg.image.colorspace_settings.name = "Non-Color"
    trenn = nt.nodes.new("ShaderNodeSeparateColor")
    L(himg.outputs["Color"], trenn.inputs[0])
    # Bänder nach Weltlage: Rauschen nur über z
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    pos = nt.nodes.new("ShaderNodeSeparateXYZ")
    L(geo.outputs["Position"], pos.inputs[0])
    zv = nt.nodes.new("ShaderNodeCombineXYZ")
    L(pos.outputs[2], zv.inputs[2])
    band = nt.nodes.new("ShaderNodeTexNoise")
    band.inputs["Scale"].default_value = 0.9
    band.inputs["Detail"].default_value = 3
    L(zv.outputs[0], band.inputs["Vector"])
    rampe = nt.nodes.new("ShaderNodeValToRGB")
    rampe.color_ramp.elements[0].color = (0.085, 0.08, 0.07, 1)
    rampe.color_ramp.elements[1].color = (0.19, 0.175, 0.15, 1)
    L(band.outputs["Fac"], rampe.inputs["Fac"])
    hell = nt.nodes.new("ShaderNodeMix")
    hell.data_type = "RGBA"
    L(trenn.outputs[1], hell.inputs["Factor"])
    L(rampe.outputs["Color"], hell.inputs[6])
    hell.inputs[7].default_value = (0.30, 0.29, 0.27, 1)
    dunkel = nt.nodes.new("ShaderNodeMix")
    dunkel.data_type = "RGBA"
    L(trenn.outputs[2], dunkel.inputs["Factor"])
    L(hell.outputs[2], dunkel.inputs[6])
    dunkel.inputs[7].default_value = (0.02, 0.02, 0.018, 1)
    ao = nt.nodes.new("ShaderNodeMix")
    ao.data_type = "RGBA"
    ao.blend_type = "MULTIPLY"
    ao.inputs["Factor"].default_value = 1.0
    L(dunkel.outputs[2], ao.inputs[6])
    L(trenn.outputs[0], ao.inputs[7])
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    L(geo.outputs["Normal"], sep.inputs[0])
    moos_f = nt.nodes.new("ShaderNodeMapRange")
    moos_f.inputs["From Min"].default_value, moos_f.inputs["From Max"].default_value = 0.75, 0.95
    L(sep.outputs[2], moos_f.inputs["Value"])
    rausch = nt.nodes.new("ShaderNodeTexNoise")
    rausch.inputs["Scale"].default_value = 0.6
    mal = nt.nodes.new("ShaderNodeMath")
    mal.operation = "MULTIPLY"
    L(moos_f.outputs["Result"], mal.inputs[0])
    L(rausch.outputs["Fac"], mal.inputs[1])
    stufe = nt.nodes.new("ShaderNodeMapRange")
    stufe.inputs["From Min"].default_value, stufe.inputs["From Max"].default_value = 0.3, 0.45
    L(mal.outputs[0], stufe.inputs["Value"])
    moos = nt.nodes.new("ShaderNodeMix")
    moos.data_type = "RGBA"
    L(stufe.outputs["Result"], moos.inputs["Factor"])
    L(ao.outputs[2], moos.inputs[6])
    moos.inputs[7].default_value = (0.045, 0.07, 0.02, 1)
    L(moos.outputs[2], bsdf.inputs["Base Color"])
    return m


def licht_und_welt(szene):
    sonne = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN"))
    sonne.data.energy = 3.0
    sonne.data.color = (1.0, 0.94, 0.84)
    sonne.data.angle = math.radians(2.5)
    sonne.rotation_euler = (math.radians(50), 0, math.radians(-40))
    szene.collection.objects.link(sonne)
    welt = bpy.data.worlds.new("Himmel")
    try:
        welt.use_nodes = True
    except AttributeError:
        pass
    welt.node_tree.nodes["Background"].inputs["Color"].default_value = (0.40, 0.45, 0.53, 1)
    welt.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    szene.world = welt


def kopie(o, st, ort, dreh=0.0, einsinken=0.0, massstab=1.0):
    k = bpy.data.objects.new(f"_V_{o.name}_{len(bpy.data.objects)}", o.data.copy())
    k.data.materials.clear()
    k.data.materials.append(st)
    k.location = (ort[0], ort[1], ort[2] - einsinken * o.dimensions.z * massstab)
    k.rotation_euler = (0, 0, math.radians(dreh))
    k.scale = (massstab,) * 3
    bpy.context.scene.collection.objects.link(k)
    return k


def hang_netz(name, stoff, hoehe_fn, x0, x1, y0, y1, n=120):
    bm = bmesh.new()
    g = {}
    for i in range(n + 1):
        for j in range(n + 1):
            x = x0 + (x1 - x0) * i / n
            y = y0 + (y1 - y0) * j / n
            g[(i, j)] = bm.verts.new((x, y, hoehe_fn(x, y)))
    for i in range(n):
        for j in range(n):
            bm.faces.new((g[(i, j)], g[(i + 1, j)], g[(i + 1, j + 1)], g[(i, j + 1)]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(stoff)
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def kamera_setzen(kam, ziel, abstand, neigung, drehung=0.0, ortho=None, linse=35):
    n, d = math.radians(neigung), math.radians(drehung)
    richtung = Vector((math.sin(d) * math.cos(n), math.cos(d) * math.cos(n), -math.sin(n)))   # von vorn (−Y)
    kam.location = Vector(ziel) - richtung * abstand
    kam.rotation_euler = richtung.to_track_quat("-Z", "Y").to_euler()
    if ortho:
        kam.data.type = "ORTHO"
        kam.data.ortho_scale = ortho
    else:
        kam.data.type = "PERSP"
        kam.data.lens = linse
    kam.data.clip_end = 1000


def rendern(szene, pfad, groesse, schnell, sichtbar):
    szene.render.resolution_x, szene.render.resolution_y = groesse
    szene.render.resolution_percentage = 50 if schnell else 100
    for o in szene.objects:
        if o.type == "MESH":
            o.hide_render = o not in sichtbar
    szene.render.filepath = pfad
    bpy.ops.render.render(write_still=True)
    print("Bild:", pfad, flush=True)
    return pfad


def nebeneinander(links, rechts, aus):
    """Zwei Bilder auf gleiche Höhe gebracht nebeneinander (für den Vergleich mit dem Konzeptbild)."""
    a = bpy.data.images.load(links)
    b = bpy.data.images.load(rechts)
    h = a.size[1]
    b.scale(max(1, round(b.size[0] * h / b.size[1])), h)
    pa = np.empty(a.size[0] * h * 4, np.float32)
    a.pixels.foreach_get(pa)
    pb = np.empty(b.size[0] * h * 4, np.float32)
    b.pixels.foreach_get(pb)
    ganz = np.concatenate([pa.reshape(h, a.size[0], 4), pb.reshape(h, b.size[0], 4)], axis=1)
    ganz[:, :, 3] = 1
    img = bpy.data.images.new("_neben", ganz.shape[1], h, alpha=False)
    img.pixels.foreach_set(ganz.ravel())
    img.filepath_raw = aus
    img.file_format = "PNG"
    img.save()
    print("Bild:", aus, flush=True)


def vorschau(spielnetze, formen, texturordner, ordner, schnell, konzept):
    """Alle Stücke (Stufe 0/1/2), eine zusammengestellte Felswand im Hang (neben Konzeptbildern) und ein Nahbild."""
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    bs.cycles_geraet(szene)
    szene.cycles.samples = 16 if schnell else 48
    szene.cycles.use_denoising = True
    try:
        szene.view_settings.view_transform = "AgX"
    except TypeError:
        szene.view_settings.view_transform = "Standard"
    licht_und_welt(szene)
    st = vorschau_stoff(texturordner) if texturordner else probe_stoff()
    gras = bs._hilfsstoff("_Gras", (0.30, 0.34, 0.19))
    kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    szene.collection.objects.link(kam)
    szene.camera = kam
    os.makedirs(ordner, exist_ok=True)
    bilder = []

    # a) alle Stücke, Reihen = Stufe 0/1/2, 20 % eingesunken
    boden = hang_netz("_Boden", gras, lambda x, y: 0.0, -150, 150, -80, 80, n=2)
    sichtbar, x = [boden], 0.0
    stufe0 = [boden]
    lage = []
    for form in formen:
        b = spielnetze[(form, 0)].dimensions.x
        lage.append((form, x + b / 2))
        x += b + 3.0
    breite = x - 3.0
    for form, mx in lage:
        for s in range(3):
            o = spielnetze[(form, s)]
            k = kopie(o, st, (mx - breite / 2, -32.0 + s * 34.0, 0), einsinken=0.15)
            sichtbar.append(k)
            if s == 0:
                stufe0.append(k)
    kamera_setzen(kam, (0, 4, 6), 400, 40, ortho=breite + 10)
    bilder.append(rendern(szene, os.path.join(ordner, "r4_alle_stufen.png"), (3200, 1500), schnell, sichtbar))
    # nur Stufe 0, größer
    kamera_setzen(kam, (0, -32, 6), 400, 25, ortho=breite * 0.52 + 6)
    for teil, (a, b) in (("links", (0, len(lage) // 2)), ("rechts", (len(lage) // 2, len(lage)))):
        mitte = (lage[a][1] + lage[b - 1][1]) / 2 - breite / 2
        breite_teil = lage[b - 1][1] - lage[a][1] + 24
        kamera_setzen(kam, (mitte, -32, 7), 400, 22, ortho=breite_teil)
        bilder.append(rendern(szene, os.path.join(ordner, f"r4_stufe0_{teil}.png"), (2400, 1100), schnell, stufe0))

    # b) zusammengestellte Felswand im Hang (Spielkamera 40°, perspektivisch) aus 6 Stücken
    def hang(x, y):                  # steigt erst 2,5 m hinter der Fußlinie an (die Front liegt bis dahin zurück)
        h = 0.0
        if y > 1.0:
            h = min(21.0, (y - 1.0) * 3.6)
        h += 0.6 * noise.noise(Vector((x / 9, y / 9, 1.7))) + 0.15 * noise.noise(Vector((x / 2.5, y / 2.5, 4.2)))
        return h - 0.02 * abs(x)
    hang_o = hang_netz("_Hang", gras, hang, -60, 60, -40, 60, n=160)
    satz = []
    ox = 400.0                                                       # abseits der Reihen
    hang_o.location.x = ox
    # (Form, Ort x/y, Drehung, Einsinken als Anteil der Höhe, Höhe des Fußes)
    zusammen = [("wand_gross_1", (0, 0.4), 0, 0.04, 0.0), ("wand_gross_2", (-14, 1.0), 8, 0.04, 0.0),
                ("wand_mittel_1", (13.5, -0.6), -10, 0.05, 0.0), ("pfeiler_1", (-7.5, -4.2), 20, 0.05, 0.0),
                ("gesims_1", (13.5, 1.6), -8, 0.0, 6.6), ("schuttkegel", (-8.0, -5.0), 10, 0.03, 0.0),
                ("wand_mittel_3", (20.5, 0.0), -20, 0.06, 0.0)]
    for form, (px, py), dreh, ein, fuss in zusammen:
        if (form, 0) not in spielnetze:
            continue
        satz.append(kopie(spielnetze[(form, 0)], st, (ox + px, py, fuss), dreh, einsinken=ein))
    kamera_setzen(kam, (ox, 2, 6), 70, 40, drehung=-12, linse=40)
    wand_bild = rendern(szene, os.path.join(ordner, "r4_felswand.png"), (1800, 1000), schnell, satz + [hang_o])
    bilder.append(wand_bild)
    for i, k in enumerate(konzept):
        aus = os.path.join(ordner, f"r4_felswand_neben_konzept_{i + 1}.png")
        nebeneinander(wand_bild, k, aus)
        bilder.append(aus)
    # c) Nahbild: Spielkamera nah (Zoom etwa 25 m) und schräg von unten auf die Bänke
    kamera_setzen(kam, (ox + 1, -2, 7), 26, 30, drehung=18, linse=35)
    bilder.append(rendern(szene, os.path.join(ordner, "r4_nah.png"), (1800, 1000), schnell, satz + [hang_o]))
    kamera_setzen(kam, (ox - 1, 0, 5), 16, 8, drehung=-25, linse=30)
    bilder.append(rendern(szene, os.path.join(ordner, "r4_nah_unten.png"), (1800, 1000), schnell, satz + [hang_o]))
    return bilder


def probe_stoff():
    """Nur für Formproben ohne Backen: Fels mit Verdeckung (AO-Knoten, Cycles), Kanten etwas heller, Moos auf
    Oberflächen – damit man die Form ohne gebackene Karten beurteilen kann."""
    m = stoff_mit_knoten("_Probe")
    nt = m.node_tree
    L = nt.links.new
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.92
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.inputs["Distance"].default_value = 2.0
    ao.samples = 8
    ao.inputs["Color"].default_value = (0.2, 0.19, 0.175, 1)
    dunkel = nt.nodes.new("ShaderNodeMix")
    dunkel.data_type = "RGBA"
    dunkel.blend_type = "MULTIPLY"
    L(ao.outputs["AO"], dunkel.inputs["Factor"])
    dunkel.inputs[6].default_value = (0.2, 0.19, 0.175, 1)
    dunkel.inputs[7].default_value = (0.15, 0.15, 0.15, 1)
    farbe = nt.nodes.new("ShaderNodeMix")
    farbe.data_type = "RGBA"
    L(ao.outputs["AO"], farbe.inputs["Factor"])
    L(dunkel.outputs[2], farbe.inputs[6])
    farbe.inputs[7].default_value = (0.2, 0.19, 0.175, 1)
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    L(geo.outputs["Normal"], sep.inputs[0])
    moos_f = nt.nodes.new("ShaderNodeMapRange")
    moos_f.inputs["From Min"].default_value, moos_f.inputs["From Max"].default_value = 0.78, 0.95
    L(sep.outputs[2], moos_f.inputs["Value"])
    rausch = nt.nodes.new("ShaderNodeTexNoise")
    rausch.inputs["Scale"].default_value = 0.5
    mal = nt.nodes.new("ShaderNodeMath")
    mal.operation = "MULTIPLY"
    L(moos_f.outputs["Result"], mal.inputs[0])
    L(rausch.outputs["Fac"], mal.inputs[1])
    stufe = nt.nodes.new("ShaderNodeMapRange")
    stufe.inputs["From Min"].default_value, stufe.inputs["From Max"].default_value = 0.28, 0.42
    L(mal.outputs[0], stufe.inputs["Value"])
    moos = nt.nodes.new("ShaderNodeMix")
    moos.data_type = "RGBA"
    L(stufe.outputs["Result"], moos.inputs["Factor"])
    L(farbe.outputs[2], moos.inputs[6])
    moos.inputs[7].default_value = (0.05, 0.075, 0.025, 1)
    L(moos.outputs[2], bsdf.inputs["Base Color"])
    return m


def probe_bilder(hochnetze, spielnetze, formen, ordner, konzept):
    """Schnelle Formprobe: je Stück das hohe Netz (wie es mit gebackener Normale wirken soll) und Stufe 0 in einem
    Hang, von schräg oben (wie Konzept 1), nah und von der Seite; das erste Bild neben dem ersten Konzeptbild. Die
    Bilder sind immer klein (halbe Größe, 16 Proben); die Netze sind mit --schnell grob, sonst wie im Export."""
    schnell = True
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    bs.cycles_geraet(szene)
    szene.cycles.samples = 16 if schnell else 32
    szene.cycles.use_denoising = True
    try:
        szene.view_settings.view_transform = "AgX"
    except TypeError:
        szene.view_settings.view_transform = "Standard"
    licht_und_welt(szene)
    szene.objects["Sonne"].rotation_euler = (math.radians(58), 0, math.radians(50))   # Streiflicht von rechts vorn
    szene.objects["Sonne"].data.energy = 4.5
    szene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35
    st = probe_stoff()
    gras = bs._hilfsstoff("_Gras", (0.30, 0.34, 0.19))
    gt = gras.node_tree
    flecken = gt.nodes.new("ShaderNodeTexNoise")
    flecken.inputs["Scale"].default_value = 0.12
    flecken.inputs["Detail"].default_value = 4
    koord = gt.nodes.new("ShaderNodeTexCoord")
    gt.links.new(koord.outputs["Object"], flecken.inputs["Vector"])
    rampe = gt.nodes.new("ShaderNodeValToRGB")
    rampe.color_ramp.elements[0].position, rampe.color_ramp.elements[1].position = 0.35, 0.65
    rampe.color_ramp.elements[0].color = (0.02, 0.04, 0.01, 1)
    rampe.color_ramp.elements[1].color = (0.16, 0.16, 0.06, 1)
    gt.links.new(flecken.outputs["Fac"], rampe.inputs["Fac"])
    gt.links.new(rampe.outputs["Color"], gt.nodes["Principled BSDF"].inputs["Base Color"])
    kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    szene.collection.objects.link(kam)
    szene.camera = kam
    os.makedirs(ordner, exist_ok=True)
    bilder = []
    for n, form in enumerate(formen):
        p = dict(STANDARD, **STUECKE[form])
        if p["art"] not in ("wand", "buendel", "pfeiler"):
            continue
        fels = Fels(form, 0)
        ox = 200.0 * (n + 1)
        H, yh = p["H"], p["T"] / 2
        yv = fels.koerper[0].yv if p["art"] == "wand" else (-p["T"] / 2 if p["art"] == "pfeiler" else 0.6)
        y0 = yv + 2.5                                   # die Front liegt bis etwa 2,5 m hinter der Fußlinie
        k = max(2.4, (H + 0.5) / max(0.5, yh - y0 - 0.3))

        def hang(x, y, y0=y0, k=k, H=H, frei=p["art"] == "pfeiler"):
            h = 0.0 if frei else min(H + 0.5, max(0.0, (y - y0) * k))
            return h + 0.25 * noise.noise(Vector((x / 7, y / 7, 1.7))) + 0.08 * noise.noise(Vector((x / 2, y / 2, 4.2)))
        hang_o = hang_netz(f"_Hang_{form}", gras, hang, -45, 45, -35, 30, n=180)
        hang_o.location.x = ox
        hoch = kopie(hochnetze[form], st, (ox, 0, 0))
        fein = kopie(spielnetze[(form, 0)], st, (ox, 0, 0))
        groesse = max(p["B"], H)
        for name, objekt in (("hoch", hoch), ("stufe0", fein)):
            kamera_setzen(kam, (ox - 1.0, 0.0, H * 0.4), groesse * 2.0, 34, drehung=28, linse=35)
            pfad = rendern(szene, os.path.join(ordner, f"probe_{form}_{name}.png"), (1600, 900), schnell,
                           [objekt, hang_o])
            bilder.append(pfad)
            if name == "hoch" and konzept and n == 0:
                aus = os.path.join(ordner, f"probe_{form}_neben_konzept.png")
                nebeneinander(pfad, konzept[0], aus)
                bilder.append(aus)
        kamera_setzen(kam, (ox + 1.0, -1.0, H * 0.45), groesse * 0.9, 14, drehung=-22, linse=30)
        bilder.append(rendern(szene, os.path.join(ordner, f"probe_{form}_nah.png"), (1600, 900), schnell,
                              [hoch, hang_o]))
        kamera_setzen(kam, (ox, 0.0, H * 0.5), groesse * 2.2, 5, drehung=-70, linse=35)      # von der Seite
        bilder.append(rendern(szene, os.path.join(ordner, f"probe_{form}_seite.png"), (1600, 900), schnell,
                              [hoch, hang_o]))
    return bilder


def main():
    arg = argumente()
    schnell = arg["schnell"]
    bs.neue_szene()
    formen = [f for f, _, _, _ in FORMEN if not arg["nur"] or f in arg["nur"]]
    if arg["nur"] or schnell:
        print("WARNUNG: --nur/--schnell überschreibt Modell und Atlas mit einer Probe – vor dem Commit ohne beides neu bauen")
    ausgabe = arg["ausgabe"]
    texturordner = ausgabe or TEXTUREN
    glb = os.path.join(ausgabe, NAME + ".glb") if ausgabe else bs.modellpfad(NAME)
    paare, spielnetze, hochnetze = [], {}, {}
    for i, (form, knoten, _, _) in enumerate(FORMEN):
        if form not in formen:
            continue
        hoch = hoch_netz(form, 7300 + 29 * i, schnell)
        tri_hoch = sum(len(p.vertices) - 2 for p in hoch.data.polygons)
        stufe0 = vereinfachen(hoch, f"{knoten}_0", DREIECKE[form][0], form)
        aufklappen(stufe0, form)
        spielnetze[(form, 0)] = stufe0
        for s in (1, 2):
            spielnetze[(form, s)] = vereinfachen(stufe0, f"{knoten}_{s}", DREIECKE[form][s], form)
        paare.append((form, hoch, stufe0))
        hochnetze[form] = hoch
        d = stufe0.dimensions
        hinten, alle = dreiecke_hinten(stufe0, form)
        print(f"{form} ({knoten}): {d.x:.2f} × {d.y:.2f} × {d.z:.2f} m (B × T × H), hoch {tri_hoch} Dreiecke, Stufen "
              + " / ".join(str(len(spielnetze[(form, s)].data.polygons)) for s in range(3))
              + f", Rückseite {hinten} von {alle} ({hinten / max(1, alle):.0%})", flush=True)
    if arg["nur_form"]:                               # schnelle Formprobe: ohne Backen, ohne Export
        for _, h, _ in paare:
            h.hide_render = True
        if arg["probe"]:
            probe_bilder(hochnetze, spielnetze, formen, arg["bilder"], arg["konzept"])
        else:
            vorschau(spielnetze, formen, None, arg["bilder"], True, arg["konzept"])
        return
    gebacken = backen(paare, schnell)
    texturen_schreiben(gebacken, texturordner)
    fels = platzhalter_stoff()
    teile = []
    for form in formen:
        for s in range(3):
            o = spielnetze[(form, s)]
            vertexfarben(o, gebacken)
            o.data.materials.clear()
            o.data.materials.append(fels)
            o["kennung"] = form
            o["stufe"] = s
            o["schicht_neigung"] = schicht_neigung(form)
            teile.append(o)
    print("geschrieben:", exportieren(glb, teile))
    if not arg["ohne_bilder"]:
        for _, h, _ in paare:
            h.hide_render = True
        vorschau(spielnetze, formen, texturordner, arg["bilder"], schnell, arg["konzept"])


if __name__ == "__main__":
    main()
