"""Felswand-Satz (Felsen zweite Generation, Etappe R4): große Wandstücke, mittlere Stücke, Gesimse, Pfeiler und ein
Schuttkegel, gebaut aus Schichtbänken und Kluftblöcken statt aus runden Brocken. Die 8 Formen aus natur_felsen.py (F1)
bleiben als Füller daneben bestehen.

    blender -b --python tools/modelle/natur_felswand.py -- [--schnell] [--ohne-bilder] [--bilder=<ordner>]
            [--nur=wand_gross_1,pfeiler_1] [--ausgabe=<ordner>] [--konzept=<bild1>,<bild2>] [--nur-form]
(--nur-form: nur formen und Vorschaubilder ohne Karten, ohne Backen und ohne Export – für schnelle Proben)

Schreibt game/assets/modelle/natur/natur_felswand.glb und die Strukturkarten game/assets/texturen/felswand/
(mit --ausgabe=<ordner> stattdessen alles in diesen Ordner, z. B. für einen Lauf auf einem anderen Rechner):
    felswand_normal.webp     Normale (Tangentenraum, OpenGL: G+ = UV-v nach oben), verlustfrei
    felswand_hoehlung.webp   R Umgebungsverdeckung (1 = frei), G Kante (1 = scharfe, vorstehende Kante),
                             B Rinne (1 = Kluft oder Fuge), verlustfrei
Beide 4096 × 2048 Pixel. Die großen Stücke haben je ein Feld 1024 × 1024, die mittleren Stücke und Gesimse je 512 × 512
(Lage in FELDER). Kanäle wie bei F1, damit das Fels-Material (F2) die Karten ohne Änderung liest.

Bauweise (statt vereinigter Superellipsoide wie in F1):
  * Schichtbänke: waagrechte Lagen (Dicke je Stück einstellbar), dazwischen dünne, weiche Lagen, die zurückwittern –
    so entstehen Kehlen und Überhänge an der Bank darüber.
  * Kluftblöcke: jede Bank ist durch senkrechte Klüfte in Blöcke geteilt; jeder Block ist ein Quader mit leicht
    gekippten, ebenen Flächen und gebrochenen Ecken (Fasen, an der Vorderkante oben oft ein großer Abbruch).
    Zwischen den Blöcken bleibt eine Fuge, hinter ihnen ein Kern: Die Fugen sind echte, etwa 1 m tiefe Spalten.
  * Hauptklüfte teilen die Wand in Säulen mit eigenem Vorsprung und eigener Höhe; die oberen Bänke springen zurück
    oder fehlen (Abbruchkante), die Enden laufen in den Hang zurück, damit sich Stücke überlappen lassen.
  * Alles wird mit dem Voxel-Remesher (Voxel 4–5 cm) zu einer Hülle vereinigt, fein aufgeraut (Risse, Körnung),
    unten bei z = 0 und hinten an der Hangseite abgeschnitten (offen), dann auf die Spielstufen vereinfacht.

Knoten „<Form>_<Stufe>“ (0 fein, 1 mittel, 2 grob), Ursprung Mitte der Grundfläche, z = 0 Unterkante, nichts darunter;
im Spiel Y oben, Schauseite +Z, die Rückseite (−Z) gehört in den Hang. Unterseite offen (außer bei den Gesimsen, deren
Unterseite beim Stapeln als Überhang zu sehen ist), Rückseite offen (außer beim freistehenden Pfeiler).
Extras je Knoten: „kennung“, „stufe“. Ein Material „Fels“ (Platzhalter, das Spiel setzt das Fels-Material).
Vertexfarbe COLOR_0 (linear) wie F1: R Höhlung (Verdeckung × (1 − 0,6 · Rinne)), G Kante, B Höhe im Stück (0 unten … 1
oben), A Tiefe (0 vorn … 1 hinten). UV0: Atlas der Strukturkarten. Kein UV2.
"""

import math
import os
import random
import sys

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

# Kennung, Knotenname, Dreiecke je Stufe 0/1/2, Voxelgröße (m) des hohen Netzes
FORMEN = (
    ("wand_gross_1", "WandGross1", (11000, 3600, 1100), 0.045),     # gebankte Wand 20 × 8 × 16
    ("wand_gross_2", "WandGross2", (11000, 3600, 1100), 0.045),     # hohe, säulige Wand 14 × 8 × 20
    ("wand_gross_3", "WandGross3", (11000, 3600, 1100), 0.045),     # Bruchwand mit zwei Absätzen 18 × 10 × 12
    ("wand_mittel_1", "WandMittel1", (5000, 1700, 500), 0.035),     # gebankte Wand 9 × 5 × 8
    ("wand_mittel_2", "WandMittel2", (5000, 1700, 500), 0.035),     # Eckstück 6 × 5 × 10, rechte Seite offen sichtbar
    ("wand_mittel_3", "WandMittel3", (5000, 1700, 500), 0.035),     # Absatz 8 × 6 × 5
    ("gesims_1", "Gesims1", (3000, 1000, 300), 0.03),               # eine Bank 10 × 4 × 1,8 zum Stapeln
    ("gesims_2", "Gesims2", (3000, 1000, 300), 0.03),               # zwei Bänke 8 × 4 × 3, obere kragt vor
    ("pfeiler_1", "Pfeiler1", (6000, 2000, 600), 0.035),            # freistehender Turm 4 × 4 × 18
    ("pfeilerbuendel", "Pfeilerbuendel", (11000, 3600, 1100), 0.045),  # vier Pfeiler vor einer Rückwand 10 × 8 × 15
    ("schuttkegel", "Schuttkegel", (6000, 2000, 600), 0.03),        # Schutthalde am Wandfuß 14 × 9 × 4
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


def argumente():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    wert = lambda n, d: next((x.split("=", 1)[1] for x in a if x.startswith(f"--{n}=")), d)  # noqa: E731
    nur = wert("nur", None)
    konzept = wert("konzept", "")
    return dict(schnell="--schnell" in a, nur_form="--nur-form" in a, ohne_bilder="--ohne-bilder" in a, bilder=wert("bilder", bs.VORSCHAU),
                nur=nur.split(",") if nur else None, ausgabe=wert("ausgabe", None),
                konzept=[k for k in konzept.split(",") if k])


# ── 1. Bausteine: Kluftblock als Schnitt von Ebenen ─────────────────────────────────────────────────────────────────
def huelle(punkte):
    bm = bmesh.new()
    for p in punkte:
        bm.verts.new(p)
    erg = bmesh.ops.convex_hull(bm, input=bm.verts)
    weg = [v for v in erg.get("geom_interior", []) if isinstance(v, bmesh.types.BMVert)]
    weg += [v for v in erg.get("geom_unused", []) if isinstance(v, bmesh.types.BMVert)]
    if weg:
        bmesh.ops.delete(bm, geom=list(set(weg)), context="VERTS")
    return bm


def polytop(punkte, ebenen):
    """Konvexer Körper: Hülle der `punkte`, nacheinander an jeder Ebene (Punkt, Normale nach außen) beschnitten."""
    pts = punkte
    for p, n in ebenen:
        bm = huelle(pts)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=p, plane_no=n, clear_outer=True)
        pts = [v.co.copy() for v in bm.verts]
        bm.free()
        if len(pts) < 4:
            return None
    return huelle(pts)


def gedreht(n, gier, nick):
    """Normale n um die Hochachse (gier) und um die Querachse (nick) gedreht, Winkel in Grad."""
    g, k = math.radians(gier), math.radians(nick)
    x, y, h = n
    x, y = x * math.cos(g) - y * math.sin(g), x * math.sin(g) + y * math.cos(g)
    y, h = y * math.cos(k) - h * math.sin(k), y * math.sin(k) + h * math.cos(k)
    return Vector((x, y, h)).normalized()


def klotz(z, x0, x1, y0, y1, h0, h1, fallen=0.0, deckel=None, seite=6.0, front=(14.0, 9.0), lehne=0.0, brueche=2,
          bruch_tief=(0.12, 0.45), fasen=0.35):
    """Kluftblock: zwischen zwei Kluftflächen (links, rechts; je um `seite` Grad verdreht), Bankfuge unten und oben
    (Gefälle `fallen` = dz je dx, gleich für die ganze Wand), Vorderfläche (Gier, Nick bis `front` Grad) und Rückfläche.
    `deckel`: z der Abbruchkante oben (waagrecht, leicht verkippt), sonst die Bankfuge. Dazu `brueche` große
    Abbruchflächen an den vorderen Ecken und schmale Fasen an den Kanten – ebene Flächen, scharfe Kanten."""
    xm, ym = (x0 + x1) / 2, (y0 + y1) / 2
    ua, ub = h0 + fallen * x0, h0 + fallen * x1
    oa, ob = h1 + fallen * x0, h1 + fallen * x1
    if deckel is not None:
        oa, ob = min(oa, deckel), min(ob, deckel)
    zu, zo = min(ua, ub) - 0.5, max(oa, ob) + 0.5
    if max(oa, ob) - max(ua, ub) < 0.12:
        return None
    zm = (max(ua, ub) + min(oa, ob)) / 2
    pts = [Vector((x, y, h)) for x in (x0 - 0.6, x1 + 0.6) for y in (y0 - 0.6, y1 + 0.6) for h in (zu, zo)]
    fn = Vector((-fallen, 0, 1)).normalized()
    ebenen = [
        (Vector((x0, ym, zm)), gedreht(Vector((-1, 0, lehne)), z.uniform(-seite, seite), 0)),
        (Vector((x1, ym, zm)), gedreht(Vector((1, 0, -lehne)), z.uniform(-seite, seite), 0)),
        (Vector((xm, ym, h0 + fallen * xm)), gedreht(-fn, 0, z.uniform(-1.5, 1.5))),
        (Vector((xm, y0, zm)), gedreht(Vector((0, -1, 0)), z.uniform(-front[0], front[0]), z.uniform(-front[1], front[1] * 0.6))),
        (Vector((xm, y1, zm)), Vector((0, 1, 0))),
    ]
    if deckel is not None and deckel < max(h1 + fallen * x0, h1 + fallen * x1):
        ebenen.append((Vector((xm, ym, deckel)), gedreht(Vector((0, 0, 1)), z.uniform(-180, 180), z.uniform(-6, 6))))
        ebenen.append((Vector((xm, ym, h1 + fallen * xm)), gedreht(fn, 0, z.uniform(-1.5, 1.5))))
    else:
        ebenen.append((Vector((xm, ym, h1 + fallen * xm)), gedreht(fn, 0, z.uniform(-1.5, 1.5))))
    m = min(x1 - x0, max(oa, ob) - min(ua, ub), y1 - y0)
    for _ in range(brueche):                   # große Abbrüche an vorderen Ecken und Kanten (meist oben)
        sx = z.choice((-1, 1)) * z.uniform(0.0, 1.0)
        sz = 1.0 if z.random() < 0.7 else -0.6
        n = Vector((sx, -z.uniform(0.6, 1.4), sz * z.uniform(0.4, 1.3))).normalized()
        ecke = Vector((xm + sx * (x1 - x0) / 2, y0, (max(oa, ob) if sz > 0 else min(ua, ub))))
        ebenen.append((ecke - n * (m * z.uniform(*bruch_tief)), n))
    for kante_x in (x0, x1):                   # Fasen an den senkrechten Vorderkanten
        if z.random() < fasen:
            n = Vector((math.copysign(1, kante_x - xm), -1, z.uniform(-0.3, 0.3))).normalized()
            ebenen.append((Vector((kante_x, y0, zm)) - n * z.uniform(0.04, 0.16), n))
    if z.random() < fasen:                     # Fase an der Vorderkante oben
        n = Vector((z.uniform(-0.2, 0.2), -1, 1)).normalized()
        ebenen.append((Vector((xm, y0, max(oa, ob))) - n * z.uniform(0.05, 0.2), n))
    return polytop(pts, ebenen)


def brocken(z, mitte, r, flach=0.6, flaechen=9):
    """Kantiger Schuttbrocken: Kugel aus zufälligen Bruchebenen, flach gelagert."""
    pts = [Vector((mitte[0] + x * r * 1.3, mitte[1] + y * r * 1.3, mitte[2] + h * r * flach * 1.3))
           for x in (-1, 1) for y in (-1, 1) for h in (-1, 1)]
    ebenen = []
    for _ in range(flaechen):
        n = Vector((z.gauss(0, 1), z.gauss(0, 1), z.gauss(0, 1) * 1.4)).normalized()
        d = r * z.uniform(0.55, 1.0) * (flach if abs(n.z) > 0.7 else 1.0)
        ebenen.append((Vector(mitte) + n * d, n))
    return polytop(pts, ebenen)


def kasten(x0, x1, y0, y1, z0, z1):
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, h)) for x in (x0, x1) for y in (y0, y1) for h in (z0, z1)]
    bmesh.ops.convex_hull(bm, input=vs)
    return bm


# ── 2. Wandstück aus Bänken und Säulen ───────────────────────────────────────────────────────────────────────────────
def baenke(z, unten, oben, dicke, weich):
    """Liste (z0, z1, ist_weich) von unten bis über `oben`."""
    lagen, h = [], unten
    while h < oben:
        if lagen and not lagen[-1][2] and z.random() < weich:
            d = z.uniform(0.2, 0.45)
            lagen.append((h, h + d, True))
        else:
            d = z.uniform(*dicke)
            lagen.append((h, h + d, False))
        h += d
    return lagen


def wandstueck(z, B, T, H, dicke=(0.7, 2.0), weich=0.3, kluft=(1.2, 3.0), haupt=(4.0, 7.0), lehne_zurueck=0.06,
               relief=1.2, enden=(1.0, 1.0), ueberhang=0.35, abbruch=0.35, stufen=(), unten=-1.2, fuge=(0.02, 0.06),
               saeulen_hoehe=(0.72, 1.0), fallen_grad=0.0, offen_rechts=False, y_vorn=None, seite=7.0,
               front_grad=(3.0, 2.0), brueche=(0, 2), verwachsen=0.35):
    """Kluftblöcke einer Wand: Breite B (x), Tiefe T (y, vorn −T/2), Höhe H (z). Gibt eine Liste von bmesh zurück.
    fallen_grad: Schichtfallen in der Wandebene (die Bänke laufen schräg durch die Wand, wie in Konzept 1).
    stufen: [(z, rücksprung)] – über z springt die Front um rücksprung nach hinten (Absätze wie im Steinbruch).
    enden: (links, rechts) Stärke, mit der die Enden in den Hang zurücklaufen (0 = Ende steht frei, Eckstück)."""
    teile = []
    fallen = math.tan(math.radians(fallen_grad))
    yv0 = -T / 2 if y_vorn is None else y_vorn
    yh = T / 2 + 0.6                                   # hinten über den Schnitt hinaus
    saat = z.uniform(0, 100)
    reserve = abs(fallen) * B / 2
    lagen = baenke(z, unten - reserve, H + 2.5 + reserve, dicke, weich)
    # Bank-Vorsprung: Zufallsweg, nach weicher Lage oft ein Vorsprung (Überhang); weiche Lagen wittern zurück
    vor, bank_vor = 0.0, []
    for i, (h0, h1, w) in enumerate(lagen):
        vor = max(-0.7, min(0.7, vor + z.uniform(-0.3, 0.3)))
        extra = z.uniform(0.4, 1.1) if (i > 0 and lagen[i - 1][2] and z.random() < ueberhang and h0 > 1.0) else 0.0
        bank_vor.append(vor + extra if not w else vor - z.uniform(0.4, 1.0))
    # Säulen zwischen Hauptklüften
    grenzen, x = [-B / 2], -B / 2
    while True:
        x += z.uniform(*haupt)
        if x > B / 2 - haupt[0] * 0.5:
            break
        grenzen.append(x)
    grenzen.append(B / 2)
    saeulen = []
    for i in range(len(grenzen) - 1):
        saeulen.append(dict(x0=grenzen[i], x1=grenzen[i + 1], vor=z.uniform(-0.8, 0.8) * relief,
                            top=H * z.uniform(*saeulen_hoehe), lehne=z.uniform(-0.04, 0.04)))
    max(saeulen, key=lambda s: s["x1"] - s["x0"])["top"] = H
    # Hauptklüfte laufen nicht schnurgerade: je Bank versetzt (±0,45 m), verschieden breit, manchmal überbrückt
    kluft_lage = {(gi, li): (z.uniform(-0.45, 0.45), 0.0 if z.random() < 0.18 else z.uniform(0.06, 0.55))
                  for gi in range(1, len(grenzen) - 1) for li in range(len(lagen))}

    def front(xx, hh, s, bv):
        """y der Vorderfläche an (xx, hh) für Säule s und Bankvorsprung bv (größer = weiter vorn)."""
        y = yv0 - s["vor"] - bv + lehne_zurueck * max(0.0, hh)
        y -= relief * 0.6 * noise.noise(Vector((xx / 6.0, hh / 8.0, saat)))
        for sz, rueck in stufen:
            if hh > sz:
                y += rueck
        rand = 3.5                                   # Enden laufen in den Hang zurück
        if xx < -B / 2 + rand:
            y += enden[0] * 3.2 * ((-B / 2 + rand - xx) / rand) ** 1.5
        if xx > B / 2 - rand and not offen_rechts:
            y += enden[1] * 3.2 * ((xx - (B / 2 - rand)) / rand) ** 1.5
        return min(y, T / 2 - 1.6)

    def hoehe_bei(xx, s):
        t = s["top"] * (1 + 0.06 * noise.noise(Vector((xx / 3.0, saat, 2.0))))
        rand = 4.0
        for sg, st in ((-1, enden[0]), (1, 0.0 if offen_rechts else enden[1])):
            d = (B / 2 - sg * xx)
            if d < rand:
                t *= 1 - 0.55 * st * ((rand - d) / rand) ** 1.2
        return min(t, H)

    for si, s in enumerate(saeulen):
        for li, (h0, h1, weich_lage) in enumerate(lagen):
            bv = bank_vor[li]
            sx0, sx1 = s["x0"], s["x1"]
            if si > 0:
                v, b = kluft_lage[(si, li)]
                sx0 += v + b / 2
            if si < len(saeulen) - 1:
                v, b = kluft_lage[(si + 1, li)]
                sx1 += v - b / 2
            if h0 + fallen * (sx0 if fallen > 0 else sx1) > H + 0.5:
                continue
            if h1 + fallen * (sx1 if fallen > 0 else sx0) < -0.4:
                continue
            x = sx0
            versatz = z.uniform(0.2, 0.9)
            breiten = []
            while x < sx1 - 0.3:
                w = max(0.5, z.uniform(*kluft) * (versatz if not breiten else 1.0))
                if x + w > sx1 - 0.6:
                    w = sx1 - x
                breiten.append((x, x + w))
                x += w
            zusammen = []
            for b in breiten:                          # verwachsene Nachbarn: keine Kluft dazwischen
                if zusammen and z.random() < verwachsen and b[1] - zusammen[-1][0] < kluft[1] * 1.8:
                    zusammen[-1] = (zusammen[-1][0], b[1])
                else:
                    zusammen.append(b)
            for bx0, bx1 in zusammen:
                xm = (bx0 + bx1) / 2
                top = hoehe_bei(xm, s)
                za, zb = h0 + fallen * xm, h1 + fallen * xm
                if za >= top - 0.15 or zb < -0.3:
                    continue
                oberste = zb >= top - 0.05
                if oberste and z.random() < abbruch * 0.45:   # Block der obersten Bank fehlt: Kerbe in der Abbruchkante
                    continue
                g = z.uniform(*fuge)
                zmitte = (za + min(zb, top)) / 2
                yf = front(xm, zmitte, s, bv) + z.uniform(-0.05, 0.05)
                if oberste:
                    yf += z.uniform(0.0, 1.8) * abbruch       # oben springt die Kante unregelmäßig zurück
                tiefe = yh - yf if oberste else z.uniform(1.5, 2.6)
                k = klotz(z, bx0 + g / 2, bx1 - g / 2, yf, yf + tiefe, h0 + g / 2, h1 - g / 2, fallen=fallen,
                          deckel=top if oberste else None, seite=seite, lehne=s["lehne"],
                          front=front_grad if not weich_lage else (3.0, 2.0),
                          brueche=z.randint(*brueche) if not weich_lage else 0,
                          fasen=0.35 if not weich_lage else 0.9)
                if k:
                    teile.append(k)
        # Kern hinter den Blöcken in Scheiben zu 1,5 m: Fugen werden etwa 1 m tiefe Spalten
        sx0, sx1 = s["x0"] + (0.5 if si > 0 else 0.0), s["x1"] - (0.5 if si < len(saeulen) - 1 else 0.0)
        top_s = max(hoehe_bei(xx, s) for xx in (sx0 + 0.3, (sx0 + sx1) / 2, sx1 - 0.3))
        h = unten - reserve
        while h < top_s - 0.6:
            h2 = min(h + 1.5, top_s - 0.4)
            n_x = max(1, round((sx1 - sx0) / 1.0))
            for k in range(n_x):                     # in Streifen zu etwa 1 m, damit der Kern den Enden folgt
                xa, xb = sx0 + (sx1 - sx0) * k / n_x, sx0 + (sx1 - sx0) * (k + 1) / n_x
                yk = min(front(xx, hh, s, bank_vor[min(range(len(lagen)), key=lambda i: abs((lagen[i][0] + lagen[i][1]) / 2 + fallen * xx - hh))])
                         for xx in (xa, xb) for hh in (h, h2)) + 1.1
                hb = min(h2, min(hoehe_bei(xa, s), hoehe_bei(xb, s)) - 0.4)
                if hb > h + 0.1:
                    teile.append(kasten(xa - 0.02, xb + 0.02, yk, yh, max(unten - reserve, h - 0.02), hb + 0.02))
            h = h2
        # Hauptkluft: zurückgesetzte Füllung, damit man nicht durch die Wand sieht
        if s["x1"] < B / 2 - 0.01:
            yk = front(s["x1"], H / 2, s, 0.0) + z.uniform(2.0, 3.2)
            teile.append(kasten(s["x1"] - 0.8, s["x1"] + 0.8, min(yk, yh - 0.5), yh, unten, min(s["top"], H) - 1.0))
    return teile


# ── 3. Pfeiler, Schutt ───────────────────────────────────────────────────────────────────────────────────────────────
def pfeiler(z, mx, my, B, T, H, nx=2, ny=2, lagen=(1.2, 3.5), unten=-1.2, verjuengung=0.12):
    """Turm aus nx × ny Teilsäulen mit unregelmäßigen Lagerfugen (Trachyt), Teilsäulen verschieden hoch; die Blöcke
    sind stärker gebrochen (verwitterte, aber kantige Säulen wie in Konzept 2)."""
    teile = []
    for i in range(nx):
        for j in range(ny):
            x0 = mx - B / 2 + B * i / nx
            x1 = mx - B / 2 + B * (i + 1) / nx
            y0 = my - T / 2 + T * j / ny
            y1 = my - T / 2 + T * (j + 1) / ny
            top = H * (1.0 if (i, j) == (nx // 2, ny // 2) else z.uniform(0.62, 0.97))
            lehne = z.uniform(-0.03, 0.03)
            h = unten
            while h < top - 0.3:
                d = min(z.uniform(*lagen), top - h)
                if top - (h + d) < 0.6:
                    d = top - h
                ein = verjuengung * max(0.0, h) / H * (x1 - x0)   # nach oben schlanker
                g = z.uniform(0.04, 0.1)
                v = z.uniform(-0.18, 0.18)
                oben = top - h - d < 0.1
                k = klotz(z, x0 + ein / 2 + g / 2 + v, x1 - ein / 2 - g / 2 + v, y0 + ein / 2 + g / 2,
                          y1 - ein / 2 - g / 2, h + g / 2, h + d - g / 2, seite=5.0, front=(8.0, 6.0), lehne=lehne,
                          brueche=z.randint(2, 4) if oben else z.randint(0, 2), bruch_tief=(0.1, 0.35), fasen=0.9)
                if k:
                    teile.append(k)
                h += d
            k = 0.45
            teile.append(kasten(x0 + k, x1 - k, y0 + k, y1 - k, unten, top - 0.5))
    return teile


def schuttkegel(z, B, T, H, unten=-0.6):
    """Halde: Fächer mit Böschungswinkel um 33°, die Kegelspitze hinten an der Wand; darauf dicht kantige, flach
    gelagerte Brocken, große rollen weiter (am Fuß größer)."""
    teile = []
    n = 48
    bm = bmesh.new()
    gitter = {}
    for i in range(n + 1):
        for j in range(n + 1):
            x = -B / 2 + B * i / n
            y = -T / 2 + T * j / n
            gitter[(i, j)] = bm.verts.new((x, y, kegel_hoehe(x, y, B, T, H)))
    for i in range(n):
        for j in range(n):
            bm.faces.new((gitter[(i, j)], gitter[(i + 1, j)], gitter[(i + 1, j + 1)], gitter[(i, j + 1)]))
    rand = [e for e in bm.edges if e.is_boundary]           # nach unten zu einem Körper schließen
    erg = bmesh.ops.extrude_edge_only(bm, edges=rand)
    for v in [e for e in erg["geom"] if isinstance(e, bmesh.types.BMVert)]:
        v.co.z = unten
    bmesh.ops.contextual_create(bm, geom=[e for e in bm.edges if all(abs(v.co.z - unten) < 1e-6 for v in e.verts)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    teile.append(bm)
    for _ in range(2600):
        x = z.uniform(-B / 2, B / 2)
        y = z.uniform(-T / 2, T / 2)
        h = kegel_hoehe(x, y, B, T, H)
        if h <= 0.02 and z.random() < 0.9:
            continue
        unten_anteil = 1 - min(1.0, h / H)
        r = z.uniform(0.07, 0.2) + unten_anteil ** 2 * z.uniform(0.0, 0.45)
        b = brocken(z, (x, y, h + r * z.uniform(-0.25, 0.05)), r, flach=z.uniform(0.4, 0.75))
        if b:
            teile.append(b)
    return teile


def kegel_hoehe(x, y, B, T, H):
    """Höhe des Schuttfächers: Spitze hinten (y = T/2) in der Mitte, Böschung zur Front und zu den Seiten."""
    ry = (T / 2 - y) / T                      # 0 hinten … 1 vorn
    rx = abs(x) / (B / 2)
    r = math.sqrt(ry * ry + (rx * 0.85) ** 2)
    h = H * max(0.0, 1 - r) ** 1.15
    h += 0.12 * noise.noise(Vector((x / 2.5, y / 2.5, 3.3)))
    return max(0.0, h)


def form_teile(form, z):
    """Liste der Teile (bmesh) einer Form. Vorderseite −Y (im Spiel +Z), Hang hinten (+Y)."""
    if form == "wand_gross_1":       # gebankte Wand, kräftige Bänke mit weichen Lagen und Überhängen
        return wandstueck(z, 20, 8, 16, dicke=(0.8, 2.0), weich=0.35, kluft=(1.2, 2.8), haupt=(3.5, 6.5), relief=1.4,
                          ueberhang=0.45, fallen_grad=9.0)
    if form == "wand_gross_2":       # hohe, säulige Wand: dicke Bänke, viele senkrechte Klüfte (Trachyt, Konzept 2)
        return wandstueck(z, 14, 8, 20, dicke=(1.8, 4.0), weich=0.12, kluft=(0.9, 1.8), haupt=(2.5, 4.5), relief=1.8,
                          ueberhang=0.25, lehne_zurueck=0.04, saeulen_hoehe=(0.6, 1.0), seite=4.0,
                          front_grad=(4.0, 3.0))
    if form == "wand_gross_3":       # Bruchwand mit zwei Absätzen, dünne, regelmäßige Bänke (Steinbruch, Konzept 4)
        return wandstueck(z, 18, 10, 12, dicke=(0.5, 1.2), weich=0.15, kluft=(1.0, 2.4), haupt=(4.0, 7.0), relief=0.7,
                          ueberhang=0.15, lehne_zurueck=0.02, stufen=((4.2, 2.2), (8.3, 2.0)), abbruch=0.25,
                          saeulen_hoehe=(0.85, 1.0), seite=2.0, front_grad=(2.0, 1.0), brueche=(0, 1))
    if form == "wand_mittel_1":
        return wandstueck(z, 9, 5, 8, dicke=(0.6, 1.6), weich=0.35, kluft=(1.0, 2.2), haupt=(3.0, 4.5), relief=0.9,
                          ueberhang=0.45, fallen_grad=-15.0)
    if form == "wand_mittel_2":      # Eckstück: rechtes Ende frei sichtbar
        return wandstueck(z, 6, 5, 10, dicke=(1.0, 2.6), weich=0.2, kluft=(0.9, 1.8), haupt=(2.4, 3.4), relief=0.9,
                          enden=(1.0, 0.0), offen_rechts=True, saeulen_hoehe=(0.7, 1.0))
    if form == "wand_mittel_3":      # niedriger Absatz: vordere Bank, die obere springt 2 m zurück
        return wandstueck(z, 8, 6, 5, dicke=(0.5, 1.3), weich=0.25, kluft=(1.0, 2.4), haupt=(3.0, 4.5), relief=0.6,
                          stufen=((2.4, 2.0),), saeulen_hoehe=(0.85, 1.0))
    if form == "gesims_1":           # eine Bank zum Stapeln, Unterseite geschlossen (sie ist als Überhang zu sehen)
        return wandstueck(z, 10, 4, 1.8, dicke=(1.8, 1.8), weich=0.0, kluft=(1.0, 2.6), haupt=(3.5, 5.0), relief=0.5,
                          unten=0.0, abbruch=0.15, ueberhang=0.0, lehne_zurueck=0.0, enden=(0.6, 0.6),
                          saeulen_hoehe=(1.0, 1.0))
    if form == "gesims_2":           # zwei Bänke, die obere kragt vor
        teile = wandstueck(z, 8, 4, 1.3, dicke=(1.3, 1.3), weich=0.0, kluft=(1.0, 2.2), haupt=(3.0, 4.5), relief=0.4,
                           unten=0.0, abbruch=0.0, ueberhang=0.0, lehne_zurueck=0.0, enden=(0.6, 0.6),
                           saeulen_hoehe=(1.0, 1.0), y_vorn=-1.2)
        oben = wandstueck(z, 8.4, 4, 1.6, dicke=(1.6, 1.6), weich=0.0, kluft=(1.2, 2.6), haupt=(3.0, 4.5), relief=0.4,
                          unten=0.0, abbruch=0.2, ueberhang=0.0, lehne_zurueck=0.0, enden=(0.5, 0.5),
                          saeulen_hoehe=(1.0, 1.0), y_vorn=-2.0)
        for t in oben:
            bmesh.ops.translate(t, verts=t.verts, vec=(0, 0, 1.35))
        return teile + oben
    if form == "pfeiler_1":
        return pfeiler(z, 0, 0, 4, 4, 18, nx=2, ny=2)
    if form == "pfeilerbuendel":     # vier Pfeiler in einem flachen Bogen vor einer niedrigeren Rückwand
        teile = wandstueck(z, 10, 8, 9, dicke=(1.5, 3.0), weich=0.1, kluft=(0.9, 1.8), haupt=(2.5, 4.0), relief=0.8,
                           y_vorn=0.2, saeulen_hoehe=(0.75, 1.0))
        for mx, my, b, t, h in ((-3.4, -1.0, 2.8, 3.2, 12.5), (-0.6, -2.0, 3.2, 3.6, 15.0), (2.3, -1.5, 2.6, 3.0, 13.5),
                                (4.0, 0.4, 2.0, 2.4, 10.0)):
            teile += pfeiler(z, mx, my, b, t, h, nx=2, ny=1)
        return teile
    if form == "schuttkegel":
        return schuttkegel(z, 14, 9, 4)
    raise ValueError(form)


# ── 4. Hohes Netz ────────────────────────────────────────────────────────────────────────────────────────────────────
def hoch_netz(form, saat, schnell):
    z = random.Random(saat)
    bm = bmesh.new()
    for teil in form_teile(form, z):
        me = bpy.data.meshes.new("_t")
        teil.to_mesh(me)
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        teil.free()
    me = bpy.data.meshes.new(f"_Hoch_{form}")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(f"_Hoch_{form}", me)
    bpy.context.scene.collection.objects.link(obj)
    mod = obj.modifiers.new("Remesh", "REMESH")
    mod.mode = "VOXEL"
    mod.voxel_size = VOXEL[form] * (1.8 if schnell else 1.0)
    mod.adaptivity = 0.0
    anwenden(obj)
    aufrauen(obj, saat, SCHICHTUNG.get(form))
    abschneiden(obj, form)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def anwenden(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    neu = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    alt = obj.data
    obj.modifiers.clear()
    name = alt.name
    obj.data = neu
    bpy.data.meshes.remove(alt)
    neu.name = name


# Feine Schichtung innerhalb der Bänke: (Abstand der Schichtfugen in m, Gefälle in Grad wie die Bänke der Form)
SCHICHTUNG = {"wand_gross_1": (0.32, 9.0), "wand_mittel_1": (0.28, -15.0), "wand_gross_3": (0.45, 0.0),
              "gesims_1": (0.3, 0.0), "gesims_2": (0.3, 0.0), "wand_mittel_3": (0.4, 0.0)}


def aufrauen(obj, saat, schichtung=None):
    """Feine Oberfläche in Metern: Haarrisse (Voronoi-Ränder), flache Dellen der Bruchflächen, Körnung, dazu feine
    Schichtfugen (schmale Rillen parallel zu den Bänken). Klein gehalten, damit die ebenen Flächen und scharfen Kanten
    der Blöcke bleiben (kein Knubbeln)."""
    me = obj.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(n, 3)
    no = np.empty(n * 3)
    me.vertices.foreach_get("normal", no)
    no = no.reshape(n, 3)
    versatz = Vector((saat * 0.731 % 50, saat * 0.377 % 50, saat * 0.913 % 50))
    d = np.empty(n)
    for i in range(n):
        p = Vector(co[i]) + versatz
        q = p + noise.noise_vector(p / 1.5, noise_basis="PERLIN_ORIGINAL") * 0.25
        abst, _ = noise.voronoi(q / 0.9, distance_metric="DISTANCE", exponent=2.5)
        riss = max(0.0, 1.0 - (abst[1] - abst[0]) / 0.05)
        delle = noise.noise(p / 0.7, noise_basis="PERLIN_ORIGINAL")
        rau = noise.fractal(p / 0.12, 0.6, 2.2, 3, noise_basis="BLENDER")
        rille = 0.0
        if schichtung:
            t = (co[i][2] - math.tan(math.radians(schichtung[1])) * co[i][0]) / schichtung[0]
            t += 0.25 * noise.noise(p / 2.0)
            f = t - math.floor(t)
            rille = max(0.0, 1.0 - min(f, 1.0 - f) / 0.09) ** 2
        d[i] = -0.03 * riss * riss + 0.012 * delle + 0.007 * rau - 0.035 * rille
    co += no * d[:, None]
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def hinten_offen(form):
    return form != "pfeiler_1"


def unten_offen(form):
    return not form.startswith("gesims")


def hinterkante(form):
    """y der Schnittebene an der Hangseite (Blender +Y)."""
    return {"wand_gross_1": 4, "wand_gross_2": 4, "wand_gross_3": 5, "wand_mittel_1": 2.5, "wand_mittel_2": 2.5,
            "wand_mittel_3": 3, "gesims_1": 2, "gesims_2": 2, "pfeilerbuendel": 4, "schuttkegel": 4.3}.get(form)


def abschneiden(obj, form):
    """Unter z = 0 weg (Unterseite offen), hinten an der Hangseite weg (Rückseite offen)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    if unten_offen(form):
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, 0), plane_no=(0, 0, 1), clear_inner=True)
    else:                                    # geschlossene Unterseite: der Remesher reicht ein halbes Voxel unter 0
        tief = min(v.co.z for v in bm.verts)
        bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, -tief))
    if hinten_offen(form):
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, hinterkante(form), 0), plane_no=(0, 1, 0), clear_outer=True)
    # lose Splitter (Inseln mit wenigen Flächen) entfernen
    bm.verts.ensure_lookup_table()
    gesehen, inseln = set(), []
    for v in bm.verts:
        if v.index in gesehen:
            continue
        stapel, insel = [v], []
        gesehen.add(v.index)
        while stapel:
            w = stapel.pop()
            insel.append(w)
            for e in w.link_edges:
                o = e.other_vert(w)
                if o.index not in gesehen:
                    gesehen.add(o.index)
                    stapel.append(o)
        inseln.append(insel)
    groesste = max(len(i) for i in inseln)
    weg = [v for i in inseln if len(i) < max(200, groesste * 0.002) for v in i]
    if weg:
        bmesh.ops.delete(bm, geom=weg, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


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
    yh = hinterkante(form) if hinten_offen(form) else None
    for v in obj.data.vertices:              # Collapse legt Punkte am offenen Rand auch knapp über die Schnitte
        if v.co.z < 0:
            v.co.z = 0.0
        if yh is not None and v.co.y > yh:
            v.co.y = yh
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def aufklappen(obj, form):
    """UV: Smart Project, in das Atlasfeld der Form gepackt."""
    for o in bpy.context.scene.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.004, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
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
    st = vorschau_stoff(texturordner) if texturordner else bs._hilfsstoff("_Grau", (0.42, 0.40, 0.37))
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
    def hang(x, y):
        h = 0.0
        if y > -1.0:
            h = min(21.0, (y + 1.0) * 2.6)
        h += 0.6 * noise.noise(Vector((x / 9, y / 9, 1.7))) + 0.15 * noise.noise(Vector((x / 2.5, y / 2.5, 4.2)))
        return h - 0.02 * abs(x)
    hang_o = hang_netz("_Hang", gras, hang, -60, 60, -40, 60, n=160)
    satz = []
    ox = 400.0                                                       # abseits der Reihen
    hang_o.location.x = ox
    # (Form, Ort x/y, Drehung, Einsinken als Anteil der Höhe, Höhe des Fußes)
    zusammen = [("wand_gross_1", (0, 4.0), 0, 0.08, 0.0), ("wand_gross_2", (-13, 4.5), 8, 0.08, 0.0),
                ("wand_mittel_1", (12.5, 2.5), -10, 0.1, 0.0), ("pfeiler_1", (-6.5, -1.0), 20, 0.08, 0.0),
                ("gesims_1", (11.5, 4.0), -8, 0.0, 6.4), ("schuttkegel", (-1.0, -4.3), 0, 0.03, 0.0),
                ("wand_mittel_3", (19, -1.5), -20, 0.1, 0.0)]
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
    paare, spielnetze = [], {}
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
        d = stufe0.dimensions
        print(f"{form} ({knoten}): {d.x:.2f} × {d.y:.2f} × {d.z:.2f} m (B × T × H), hoch {tri_hoch} Dreiecke, Stufen "
              + " / ".join(str(len(spielnetze[(form, s)].data.polygons)) for s in range(3)), flush=True)
    if arg["nur_form"]:                               # schnelle Formprobe: ohne Backen, ohne Export
        for _, h, _ in paare:
            h.hide_render = True
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
            teile.append(o)
    print("geschrieben:", exportieren(glb, teile))
    if not arg["ohne_bilder"]:
        for _, h, _ in paare:
            h.hide_render = True
        vorschau(spielnetze, formen, texturordner, arg["bilder"], schnell, arg["konzept"])


if __name__ == "__main__":
    main()
