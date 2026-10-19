"""Gemeinsame Teile der Naturmodelle (Bäume, Büsche, Bodenpflanzen, Steine, Totholz). Kein eigenes Modell –
die Skripte natur_baeume.py, natur_buesche.py, natur_boden.py, natur_steine.py und natur_holz.py importieren es.

Jede Datei ist ein Baukasten: je Art und Variante ein Knoten („Tanne1“, „Tanne2“ …), jeder mit eigenem Ursprung
am Fuß (Mitte auf dem Boden). Das Spiel setzt Kopien davon in die Landschaft (am besten als MultiMesh).
Keine Hausfarbe, keine Zustände, keine Anker (`pruefen.py … --natur`).

Jahreszeiten: Laub trägt die Materialien „Laub“, „Laub hell“ und „Laub dunkel“ – nur diese drei färbt das Spiel
je Jahreszeit um (Herbst gelb und rot). Nadeln („Tanne“, „Tanne hell“), Gras, Blüten und Rinde bleiben.

Die Teile sind sparsam (meist unter 200 Dreiecke): Im Umland stehen davon Hunderte.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402,F401  (setzt die Farben für Erde, Tanne, Fels, Rinde …)
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Laub dunkel": (0.18, 0.28, 0.13),
    "Rinde hell": (0.52, 0.50, 0.46),      # glatte Buchenrinde
    "Birkenrinde": (0.86, 0.84, 0.78),
    "Rinde schwarz": (0.12, 0.11, 0.10),   # Flecken der Birke
    "Totholz": (0.45, 0.40, 0.33),         # vergraut, ohne Rinde
    "Apfel": (0.70, 0.16, 0.12),
    "Blüte weiß": (0.93, 0.92, 0.84),
    "Beere": (0.16, 0.10, 0.18),
    "Ginster": (0.88, 0.72, 0.14),
    "Mohn": (0.80, 0.16, 0.10),
    "Kornblume": (0.25, 0.38, 0.78),
    "Pilzhut": (0.72, 0.14, 0.10),
    "Pilzhut braun": (0.46, 0.30, 0.18),
    "Pilzstiel": (0.88, 0.85, 0.76),
    "Farn": (0.27, 0.42, 0.18),
    "Nessel": (0.22, 0.34, 0.16),
    "Kolben": (0.36, 0.24, 0.14),
})


# Winter: krone() zeichnet kahle Äste (in WINTER_RINDE) statt Laub, tupfen() (Blüten, Beeren, Äpfel) entfällt.
# Die Baukästen setzen das für ihre „…_Winter“-Knoten (natur_baeume, natur_buesche).
WINTER = False
WINTER_RINDE = "Rinde"


def zufall(saat):
    return random.Random(saat * 7919 + 13)


# ─────────────────────────────────────────────────────────────────────────────
#  Grundformen
# ─────────────────────────────────────────────────────────────────────────────

def krone(netz, st, mitte, rx, rz, saat=0, stoffe=("Laub", "Laub hell"), n=7, ry=None, flach_unten=0.55):
    """Laubkrone als unregelmäßiger Körper: drei Ringe aus n Ecken zwischen Unter- und Oberpol.
    `rx`/`ry` waagrechte Halbachsen, `rz` senkrechte; `flach_unten`: wie weit die Unterseite eingezogen ist.
    Die Flächen wechseln die Farbe (heller oben). Im Winter (WINTER) stehen statt einer Laubkrone kahle Äste im selben Raum."""
    if WINTER and any(s.startswith("Laub") for s in stoffe):
        bs.kahle_aeste(netz, st, mitte, rx * 0.95, rz, ry=ry, saat=saat, stoff=WINTER_RINDE)
        return
    z = zufall(saat)
    ry = rx if ry is None else ry
    m = Vector(mitte)
    ringe = []
    for f_z, f_r in ((-0.55, 0.78), (0.05, 1.0), (0.6, 0.72)):
        dreh = z.uniform(0, 1)
        ringe.append([m + Vector((rx * f_r * z.uniform(0.82, 1.12) * math.cos(dreh + 2 * math.pi * k / n),
                                  ry * f_r * z.uniform(0.82, 1.12) * math.sin(dreh + 2 * math.pi * k / n),
                                  rz * f_z + z.uniform(-0.08, 0.08) * rz)) for k in range(n)])
    oben = m + Vector((z.uniform(-0.1, 0.1) * rx, z.uniform(-0.1, 0.1) * ry, rz))
    unten = m + Vector((0, 0, -rz * flach_unten - 0.2 * rz))
    for i in range(2):
        for k in range(n):
            a, b = ringe[i][k], ringe[i][(k + 1) % n]
            c, d = ringe[i + 1][(k + 1) % n], ringe[i + 1][k]
            netz.vieleck_nach([a, b, c, d], (a + b + c + d) / 4 - m, st[stoffe[(k + i) % len(stoffe)]])
    for k in range(n):
        a, b = ringe[2][k], ringe[2][(k + 1) % n]
        netz.vieleck_nach([a, b, oben], (a + b + oben) / 3 - m, st[stoffe[-1] if k % 2 else stoffe[0]])
        a, b = ringe[0][k], ringe[0][(k + 1) % n]
        netz.vieleck_nach([a, b, unten], (a + b + unten) / 3 - m, st[stoffe[0]])


def ast(netz, st, a, b, r0, r1=None, stoff="Rinde", n=5):
    """Ast oder Stamm als n-eckiger Kegelstumpf von a (Dicke r0) nach b (Dicke r1)."""
    a, b = Vector(a), Vector(b)
    r1 = r0 * 0.6 if r1 is None else r1
    achse = (b - a).normalized()
    hilf = Vector((1, 0, 0)) if abs(achse.x) < 0.9 else Vector((0, 1, 0))
    u = achse.cross(hilf).normalized()
    v = achse.cross(u)
    ring_a = [a + (u * math.cos(2 * math.pi * k / n) + v * math.sin(2 * math.pi * k / n)) * r0 for k in range(n)]
    ring_b = [b + (u * math.cos(2 * math.pi * k / n) + v * math.sin(2 * math.pi * k / n)) * r1 for k in range(n)]
    for k in range(n):
        j = (k + 1) % n
        quad = [ring_a[k], ring_a[j], ring_b[j], ring_b[k]]
        netz.vieleck_nach(quad, sum(quad, Vector()) / 4 - (a + b) / 2, st[stoff])
    netz.vieleck_nach(ring_b, achse, st[stoff])


def blatt(netz, st, fuss, richtung, laenge, breite, stoff, hoch=0.3, dicke=0.01):
    """Flaches Blatt oder Wedel (Raute) vom Fuß nach außen, leicht angehoben; Ober- und Unterseite
    um `dicke` versetzt (zwei Flächen in einer Ebene flimmern)."""
    f = Vector(fuss)
    r = Vector((richtung[0], richtung[1], 0)).normalized()
    q = Vector((-r.y, r.x, 0))
    spitze = f + r * laenge + Vector((0, 0, laenge * hoch))
    mitte = f + r * laenge * 0.45 + Vector((0, 0, laenge * hoch * 0.7))
    ecken = [f, mitte + q * breite / 2, spitze, mitte - q * breite / 2]
    oben = Vector((0, 0, 1))
    netz.vieleck_nach(ecken, oben, st[stoff])
    netz.vieleck_nach([e - Vector((0, 0, dicke)) for e in ecken], -oben + r * 0.3, st[stoff])


def tupfen(netz, st, punkt, groesse, stoff, n=4):
    """Kleiner Farbtupfer (Blüte, Beere, Apfel): flache Pyramide nach oben. Im Winter keiner."""
    if WINTER:
        return
    p = Vector(punkt)
    ring = [p + Vector((groesse * math.cos(2 * math.pi * k / n + 0.4), groesse * math.sin(2 * math.pi * k / n + 0.4), 0))
            for k in range(n)]
    spitze = p + Vector((0, 0, groesse * 0.8))
    for k in range(n):
        a, b = ring[k], ring[(k + 1) % n]
        netz.vieleck_nach([a, b, spitze], (a + b) / 2 - p + Vector((0, 0, 0.5 * groesse)), st[stoff])


def punkte_auf_krone(mitte, rx, rz, anzahl, saat, ry=None):
    """Punkte auf der oberen Hälfte einer Krone (für Äpfel, Blüten, Beeren) – etwas außerhalb der Fläche."""
    z = zufall(saat)
    ry = rx if ry is None else ry
    m = Vector(mitte)
    pkt = []
    for i in range(anzahl):
        w = 2 * math.pi * (i + z.uniform(0, 0.6)) / anzahl
        h = z.uniform(-0.15, 0.55)
        f = math.sqrt(max(0.0, 1 - h * h)) * 1.06
        pkt.append(m + Vector((rx * f * math.cos(w), ry * f * math.sin(w), rz * h)))
    return pkt


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau: alle Knoten einer Datei im Raster, mit Namen
# ─────────────────────────────────────────────────────────────────────────────

def bericht(name, objekte):
    """Dreiecke je Knoten und Flimmern je Knoten (jeder Knoten steht für sich)."""
    bs.bericht(name, objekte, einzeln=True)


def ausgeben(name, objekte, spalten, abstand, ziel_hoehe=1.5, ausschnitt=30.0, zeilenabstand=None, nur=None):
    """Export, dann alle Knoten (oder nur die in `nur`) im Raster nebeneinander rendern (nur fürs Bild verschoben) und beschriften."""
    bericht(name, objekte)
    print("geschrieben:", bs.exportieren(name, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    bild(name, nur if nur is not None else objekte, spalten, abstand, ziel_hoehe, ausschnitt, zeilenabstand)


def bild(name, objekte, spalten, abstand, ziel_hoehe=1.5, ausschnitt=30.0, zeilenabstand=None):
    """docs/modelle/<name>.png: die Knoten im Raster, beschriftet. Die übrigen Knoten bleiben unsichtbar."""
    import bpy
    from bpy_extras.object_utils import world_to_camera_view
    schnell = "--schnell" in sys.argv
    zeilen = math.ceil(len(objekte) / spalten)
    for i, o in enumerate(objekte):
        sx, sy = i % spalten, i // spalten
        # Raster quer zur Blickrichtung: Spalten nach rechts unten im Bild, Zeilen nach hinten
        x = (sx - (spalten - 1) / 2) * abstand
        y = ((zeilen - 1) / 2 - sy) * (zeilenabstand or abstand)
        o.location = Vector((x * 0.7071 - y * 0.7071, x * 0.7071 + y * 0.7071, 0))
    szene = bpy.context.scene
    if szene.camera is None:
        bs.vorschau_buehne(None, ziel_hoehe=ziel_hoehe, ausschnitt=ausschnitt, schnell=schnell)
    else:                                   # zweites Bild derselben Datei: Bühne steht schon
        szene.camera.data.ortho_scale = ausschnitt
        szene["_ziel"] = ziel_hoehe
        bs.blick(0)
    bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = 1600, 1200
    pfad = bs.rendern(name, lambda o: o in objekte)
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        return
    szene = bpy.context.scene
    bild = Image.open(pfad).convert("RGB")
    zeichnen = ImageDraw.Draw(bild)
    w, h = bild.size
    try:
        schrift = ImageFont.truetype(os.path.join(bs.WURZEL, "game", "assets", "schriften", "Alegreya-Variabel.ttf"),
                                     int(h / 42))
    except OSError:
        schrift = ImageFont.load_default()
    for o in objekte:
        p = world_to_camera_view(szene, szene.camera, o.location - Vector((0.0, 0.0, 0.05)))
        zeichnen.text((int(p.x * w), int((1 - p.y) * h) + int(h / 40)), o.name, fill=(240, 236, 220), font=schrift,
                      anchor="mt")
    bild.save(pfad, optimize=True)
    print(pfad)
