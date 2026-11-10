"""Zugtiere: Pferd und Ochse aus dem „LowPoly Animated Farm Animal Pack“ von Quaternius (CC0), an den Stil der Welt angepasst.

    blender -b --python tools/modelle/zugtiere.py -- [--nur=pferd_rotbraun,...] [--ohne-bilder] [--schnell]

Schreibt game/assets/modelle/tiere/tier_pferd_rotbraun.glb, tier_pferd_schwarzbraun.glb, tier_ochse_rotbraun.glb, tier_ochse_falb.glb
und Vorschaubilder (Umgebungsvariable ZUGTIERE_VORSCHAU, sonst docs/modelle/).

Quelle (Lizenz CC0 1.0, an der Seite und in der mitgelieferten License.txt geprüft, siehe docs/assets/protokoll.csv): die Blender-Dateien
Horse.blend und Cow.blend aus „Farm Animals by @Quaternius.zip“ (https://opengameart.org/content/lowpoly-animated-farm-animal-pack).
Die Dateien liegen nicht im Repository: Ordner mit den .blend-Dateien in der Umgebungsvariablen RG_FARM_BLENDS, sonst
$RG_KITS/Quaternius_Farm_Animals_CC0/Blends (Standard von RG_KITS: ~/werkzeuge/assets/entpackt).

Was dieses Skript an der Vorlage ändert (alles Eigenbau, die Form kommt von Quaternius):
  * Maßstab auf Meter (Pferd Widerrist rund 1,55 m, Ochse rund 1,4 m), Ursprung am Boden zwischen den Füßen,
    Blick nach -Y (Blender), im Spiel +Z.
  * Pferd zum Kaltblut: tieferer, breiterer Rumpf, dickere Beine und kräftigerer Hals. Kuh zum Ochsen: Euter weg, Nase hell, ein Fell,
    kräftiger Hals, leicht erhöhter Widerrist, Hörner (eigenes Rohr).
  * Einmal Unterteilen (Catmull-Clark) und glatt schattiert statt der Facetten der Vorlage; Fell nach Fläche (Fell, Dunkel, Maul, Huf)
    als Eckenfarbe mit Verlauf (Bauch dunkler, Rücken heller, leichtes Rauschen) in einem Material „Farben“ wie die übrigen Tiere.
  * Zubehör im selben Netz (ein Zeichenaufruf): Pferd Kummet, Hölzer und Bauchgurt, Ochse Nackenjoch; Leder und Holz nehmen die Schichten der
    Material-Bibliothek (Leder, Holz). Eine Schicht „Fell“ gibt es dort nicht (offen).
  * Skelett: die 28 Knochen der Vorlage (IK in Knochendrehungen gebacken, Fußknochen bleiben als Ziele für die Hufe).
Netze: „Nah_<art>“ (mit Zubehör) und „Fern_<art>“ (stark vereinfacht), beide am Skelett; Anker „Anker_Geschirr“ (Zugpunkt: Mitte des
Kummets bzw. Jochs, Blickrichtung des Tieres = Blickrichtung des Ankers; passt zu Anker_Deichsel des Karrens).
Bewegungen (Schleifen): Stehen (Idle der Vorlage), Gehen (WalkSlow der Vorlage, auf das Karrentempo gedehnt: Pferd 1,3 m/s, Ochse 0,8 m/s,
die Füße rutschen nicht), Ziehen (Gehen mit gesenktem Kopf und Hals, Rumpf nach vorn: Eigenbau aus Knochendrehungen), Fressen (Stehen mit
gesenktem Kopf).
"""

import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Quaternion

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import figuren as fg  # noqa: E402
import zugtiere_anim as za  # noqa: E402
import zugtiere_kopf as zk  # noqa: E402
import zugtiere_ochse as zo  # noqa: E402
from bausteine import Vector  # noqa: E402

FPS = 24
KUMMET_BOGEN = 0.10
KITS = os.environ.get("RG_KITS", os.path.expanduser("~/werkzeuge/assets/entpackt"))
QUELLE = os.environ.get("RG_FARM_BLENDS", os.path.join(KITS, "Quaternius_Farm_Animals_CC0", "Blends"))
VORSCHAU = os.environ.get("ZUGTIERE_VORSCHAU", bs.VORSCHAU)
PASSANT = os.path.join(bs.MODELLE, "figuren", "probe", "passant_probe.glb")

# ── Paletten (sRGB, gedeckt) ──
PALETTEN = {
    "pferd_rotbraun": {"Fell": (0.42, 0.22, 0.11), "Dunkel": (0.075, 0.048, 0.036), "Maul": (0.30, 0.20, 0.15), "Huf": (0.20, 0.165, 0.14)},
    "pferd_schwarzbraun": {"Fell": (0.23, 0.155, 0.11), "Dunkel": (0.065, 0.055, 0.05), "Maul": (0.33, 0.25, 0.19), "Huf": (0.17, 0.14, 0.12)},
    "ochse_rotbraun": {"Fell": (0.44, 0.25, 0.14), "Dunkel": (0.21, 0.12, 0.075), "Maul": (0.60, 0.46, 0.38), "Huf": (0.25, 0.21, 0.175)},
    "ochse_falb": {"Fell": (0.67, 0.51, 0.32), "Dunkel": (0.44, 0.31, 0.19), "Maul": (0.80, 0.66, 0.52), "Huf": (0.30, 0.25, 0.20)},
}
HAARFARBE = {"pferd_rotbraun": (0.33, 0.175, 0.085), "pferd_schwarzbraun": (0.085, 0.07, 0.065),
             "ochse_rotbraun": (0.19, 0.095, 0.055), "ochse_falb": (0.15, 0.105, 0.07)}      # Fuchs: Mähne heller rotbraun, Rappe fast schwarz, Falbe dunkel
bs.FARBEN.update({
    "Horn": (0.80, 0.74, 0.60), "Hornspitze": (0.20, 0.17, 0.14), "Gurt": (0.12, 0.10, 0.09),
})
for _n, _p in PALETTEN.items():
    _p["Krone"] = tuple(0.35 * a + 0.65 * b for a, b in zip(_p["Fell"], _p["Huf"]))      # Hufkrone: weicher Übergang Beinfarbe -> Horn
    for _k, _f in _p.items():
        bs.FARBEN[f"{_n} {_k}"] = _f
    bs.FARBEN[f"{_n} Haar"] = HAARFARBE[_n]
FELL_STOFFE = ("Fell", "Dunkel", "Maul", "Huf", "Haar", "Krone")

# Material-Plätze (Reihenfolge fest): Index = Fläche vor dem Unterteilen
PLATZ = ["Fell", "Dunkel", "Maul", "Huf", "Horn", "Hornspitze", "Leder", "Holz", "Gurt", "Haar", "Krone"]
HAAR = 9
KRONE = 10
FELL_PLAETZE = {0, 1, 2, 3}

TIERE = {
    # art: Quelle, Blenderdatei, Faktor Quelle -> Meter, Zieltempo (m/s), Kopfneigung (Grad) für Ziehen und Fressen
    "pferd": dict(blend="Horse.blend", objekt="Horse", s=0.37, tempo=1.3,
                  ziehen={"Shoulders": 6.0, "Neck": 9.0, "Head": 5.0, "Torso": 1.5},
                  fressen={"Shoulders": 14.0, "Neck": 40.0, "Head": 22.0}),
    "ochse": dict(blend="Cow.blend", objekt="Cow", s=0.31, tempo=0.8,
                  ziehen={"Shoulders": 8.0, "Neck": 14.0, "Head": 8.0, "Torso": 2.0},
                  fressen={"Shoulders": 14.0, "Neck": 36.0, "Head": 24.0}),
}
DATEIEN = [("tier_pferd_rotbraun", "pferd", "rotbraun"), ("tier_pferd_schwarzbraun", "pferd", "schwarzbraun"),
           ("tier_ochse_rotbraun", "ochse", "rotbraun"), ("tier_ochse_falb", "ochse", "falb")]


def _weich(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def _rauschen(p):
    """Gleichbleibendes Rauschen 0..1 aus der Lage (kein Zufall, jedes Erzeugen gleich)."""
    s = math.sin(p.x * 12.9898 + p.y * 78.233 + p.z * 37.719) * 43758.5453
    return s - math.floor(s)


def _glatt(p):
    """Weiches Rauschen 0..1 (Werte-Rauschen mit glatter Überblendung, jedes Erzeugen gleich)."""
    ix, iy, iz = math.floor(p.x), math.floor(p.y), math.floor(p.z)
    fx, fy, fz = _weich(p.x - ix), _weich(p.y - iy), _weich(p.z - iz)

    def e(a, b, c):
        return _rauschen(Vector((a * 1.0 + 0.5, b * 1.0 + 0.5, c * 1.0 + 0.5)))
    r = 0.0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (fx if dx else 1 - fx) * (fy if dy else 1 - fy) * (fz if dz else 1 - fz)
                r += w * e(ix + dx, iy + dy, iz + dz)
    return r


# ─────────────────────────────────────────────────────────────────────────────
#  Vorlage laden, Bewegungen der Vorlage aufnehmen
# ─────────────────────────────────────────────────────────────────────────────

def laden(art):
    cfg = TIERE[art]
    bs.neue_szene()
    szene = bpy.context.scene
    szene.render.fps = FPS
    with bpy.data.libraries.load(os.path.join(QUELLE, cfg["blend"]), link=False) as (v, nach):
        nach.objects = list(v.objects)
        nach.actions = list(v.actions)
    for o in nach.objects:
        if o:
            szene.collection.objects.link(o)
    ske = next(o for o in nach.objects if o and o.type == "ARMATURE")
    netz = next(o for o in nach.objects if o and o.type == "MESH")
    ske.name = ske.data.name = "Skelett"
    return ske, netz


def aufnehmen(ske, aktion):
    """Spielt eine Aktion der Vorlage ab und merkt sich je Bild die fertige (IK eingerechnete) Knochendrehung und -verschiebung
    (matrix_basis) sowie die Lage der Fußknochen. Das Bild am Ende (gleich dem ersten) entfällt."""
    szene = bpy.context.scene
    act = bpy.data.actions[aktion]
    ske.animation_data_create()
    ske.animation_data.action = act
    a, e = int(act.frame_range[0]), int(act.frame_range[1])
    bilder, fuesse = [], []
    for f in range(a, e):
        szene.frame_set(f)
        bpy.context.view_layer.update()
        r = {}
        for pb in ske.pose.bones:
            m = ske.convert_space(pose_bone=pb, matrix=pb.matrix, from_space="POSE", to_space="LOCAL")
            loc, q, _ = m.decompose()
            r[pb.name] = (loc.copy(), q.copy())
        bilder.append(r)
        fuesse.append({n: (ske.matrix_world @ ske.pose.bones[n].tail).copy() for n in ("FrontFoot.L", "FrontFoot.R", "BackFoot.L", "BackFoot.R")})
    ske.animation_data.action = None
    # Vorzeichen der Drehungen fortlaufend (kein Umklappen beim Mischen)
    for n in bilder[0]:
        for i in range(1, len(bilder)):
            if bilder[i][n][1].dot(bilder[i - 1][n][1]) < 0:
                bilder[i][n] = (bilder[i][n][0], -bilder[i][n][1])
    return bilder, fuesse


def standgeschwindigkeit(fuesse):
    """Mittleres Tempo der Fußbewegung im Stand (Einheiten der Vorlage je Sekunde, nach +Y = der Körper läuft nach -Y):
    Bild für Bild, wo beide Nachbarbilder am Boden sind."""
    boden = min(p.z for f in fuesse for p in f.values())
    n = len(fuesse)
    summe, zahl = 0.0, 0
    for name in fuesse[0]:
        for i in range(n):
            p, q = fuesse[i][name], fuesse[(i + 1) % n][name]
            if p.z <= boden + 0.03 and q.z <= boden + 0.03:
                summe += (q.y - p.y) * FPS
                zahl += 1
    return summe / max(zahl, 1)


# ─────────────────────────────────────────────────────────────────────────────
#  Netz umformen
# ─────────────────────────────────────────────────────────────────────────────

class Gewichte:
    def __init__(self, bm, ob):
        self.dl = bm.verts.layers.deform.verify()
        self.idx = {g.name: g.index for g in ob.vertex_groups}

    def summe(self, v, namen):
        d = v[self.dl]
        return min(1.0, sum(d.get(self.idx[n], 0.0) for n in namen))

    def dominant(self, v):
        d = v[self.dl]
        namen = {i: n for n, i in self.idx.items()}
        return namen[max(d.keys(), key=lambda k: d[k])] if d.keys() else ""


def radial(bm, gw, namen, a, b, faktor):
    """Dicker (faktor > 1) um die Achse a-b, gewichtet nach den Knochengruppen `namen`."""
    ab = b - a
    for v in bm.verts:
        w = gw.summe(v, namen)
        if w <= 0.0:
            continue
        t = max(0.0, min(1.0, (v.co - a).dot(ab) / ab.length_squared))
        p = a + ab * t
        v.co = p + (v.co - p) * (1.0 + (faktor - 1.0) * w)


def knochen(ske, name):
    b = ske.data.bones[name]
    return Vector(b.head_local), Vector(b.tail_local)


def formen(art, bm, gw, ske, ob):
    """Kaltblut bzw. Ochse: Maße der Vorlage (Einheiten der Vorlage, vor dem Maßstab) ändern. Gibt je Fläche den Materialplatz zurück."""
    pferd = art == "pferd"
    rumpf = ("Body", "Torso", "Hips", "Back", "Shoulders")
    belly_z = 3.9 if pferd else 3.1
    for v in bm.verts:
        w = gw.summe(v, rumpf)
        if w <= 0.0:
            continue
        v.co.x *= 1.0 + (0.14 if pferd else 0.05) * w
        v.co.z -= (0.30 if pferd else 0.10) * w * _weich((belly_z - v.co.z) / 0.9)       # tieferer Bauch
    for seite in ("L", "R"):
        for vorn in ("Front", "Back"):
            oben, unten = f"{vorn}UpLeg.{seite}", f"{vorn}LowLeg.{seite}"
            fuss = f"{vorn}Foot.{seite}"
            radial(bm, gw, [oben], *knochen(ske, oben), 1.65 if pferd else 1.26)
            radial(bm, gw, [unten, fuss], *knochen(ske, unten), 1.52 if pferd else 1.20)
    radial(bm, gw, ["Neck"], *knochen(ske, "Neck"), 1.22 if pferd else 1.24)
    radial(bm, gw, ["Shoulders"], *knochen(ske, "Shoulders"), 1.12 if pferd else 1.20)
    if not pferd:                                                   # Widerrist des Ochsen etwas höher
        for v in bm.verts:
            w = gw.summe(v, ["Shoulders", "Neck"])
            if w > 0.0 and v.co.z > 3.3:
                v.co.z += 0.28 * w * math.exp(-((v.co.y + 2.3) / 0.95) ** 2)
    if pferd:                                                       # größerer Kopf (von vorn wirkte er klein und spitz)
        kh, _ = knochen(ske, "Head")
        for v in bm.verts:
            w = gw.summe(v, ["Head"])
            if w > 0.0:
                v.co = kh + (v.co - kh) * (1.0 + 0.32 * w)
    # Fläche -> Materialplatz
    klassen = {}
    for fl in bm.faces:
        c = fl.calc_center_median()
        quelle = ob.data.materials[fl.material_index].name if ob.data.materials[fl.material_index] else ""
        dom = {gw.dominant(v) for v in fl.verts}
        if c.z < 0.40:
            k = KRONE                                               # unterer Beinrand: einheitlicher Übergang zum neuen Huf
        elif pferd and quelle == "Material.006":
            k = 1                                                   # Mähne, Schweif, Schopf
        elif pferd and c.z < 1.05 and any("Leg" in d or "Foot" in d for d in dom):
            k = 1                                                   # dunkle Beine des Braunen
        elif pferd and c.y < -4.55 and "Head" in dom:
            k = 2                                                   # Maul
        elif (not pferd) and quelle == "Pink":
            k = 2
        elif (not pferd) and any(d.startswith("Tail") for d in dom) and c.z < 3.3:
            k = 1                                                   # Schweif bzw. Schwanzquaste
        else:
            k = 0
        klassen[fl.index] = k
    zaehl = {}
    for k in klassen.values():
        zaehl[PLATZ[k]] = zaehl.get(PLATZ[k], 0) + 1
    print("Flaechen je Stoff:", zaehl)
    return klassen


def euter_weg(bm, ob):
    weg = [fl for fl in bm.faces if ob.data.materials[fl.material_index].name == "Pink" and fl.calc_center_median().y > 0.5]
    bmesh.ops.delete(bm, geom=weg, context="FACES")
    lose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=lose, context="VERTS")
    rand = [e for e in bm.edges if e.is_boundary]                    # das Loch des Euters schließen
    if rand:
        bmesh.ops.holes_fill(bm, edges=rand, sides=12)


# ─────────────────────────────────────────────────────────────────────────────
#  Rohre (Hörner, Gurte, Zubehör)
# ─────────────────────────────────────────────────────────────────────────────

def rohr(bm, gw, gruppe, platz, pfad, radien, n=6, geschlossen=False, mitte=None, achse=None, teilung=None, platz2=None):
    """Rohr entlang `pfad` (Liste von Punkten) mit wechselndem Radius. Offen: Querschnitt mit Parallelverschiebung, Kappen an den Enden.
    Geschlossen (Ring um `achse` durch `mitte`): Querschnitt zeigt nach außen und entlang der Achse. `teilung` = Index, ab dem `platz2`
    gilt (Hornspitze). Alle Ecken bekommen das Gewicht 1 in der Gruppe `gruppe`."""
    ringe = []
    if geschlossen:
        for p, r in zip(pfad, radien):
            u = (p - mitte)
            u -= achse * u.dot(achse)
            u.normalize()
            ringe.append([p + (math.cos(2 * math.pi * k / n) * u + math.sin(2 * math.pi * k / n) * achse) * r for k in range(n)])
    else:
        u = None
        for i, (p, r) in enumerate(zip(pfad, radien)):
            t = (pfad[min(i + 1, len(pfad) - 1)] - pfad[max(i - 1, 0)]).normalized()
            if u is None:
                u = t.cross(Vector((0, 0, 1)))
                if u.length < 0.2:
                    u = t.cross(Vector((1, 0, 0)))
            u = (u - t * u.dot(t)).normalized()
            w = t.cross(u)
            ringe.append([p + (math.cos(2 * math.pi * k / n) * u + math.sin(2 * math.pi * k / n) * w) * r for k in range(n)])
    vv = [[bm.verts.new(p) for p in ring] for ring in ringe]
    neu = [v for ring in vv for v in ring]
    stuecke = len(vv) if geschlossen else len(vv) - 1
    for i in range(stuecke):
        a, b = vv[i], vv[(i + 1) % len(vv)]
        pl = platz2 if (teilung is not None and i >= teilung) else platz
        for k in range(n):
            fl = bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
            fl.material_index = pl
            fl.smooth = True
    if not geschlossen:
        for ring, pl in ((vv[0][::-1], platz), (vv[-1], platz2 if platz2 is not None else platz)):
            fl = bm.faces.new(ring)
            fl.material_index = pl
            fl.smooth = True
    for v in neu:
        v[gw.dl][gw.idx[gruppe]] = 1.0
    return neu


def schnitt(bm, gw, namen, p, achse, dicke):
    """Ecken (Gruppen `namen`) in einer Scheibe der Dicke `dicke` senkrecht zu `achse` durch p."""
    return [v.co.copy() for v in bm.verts if gw.summe(v, namen) > 0.3 and abs((v.co - p).dot(achse)) < dicke]


# ─────────────────────────────────────────────────────────────────────────────
#  Zubehör
# ─────────────────────────────────────────────────────────────────────────────

def kummet(bm, gw, ske, s, ziel_hoehe=1.595):
    """Pferd: Kummet (Polster aus Leder) mit zwei Hölzern am Halsansatz (Schulterknochen, Mitte in `ziel_hoehe`), dazu Bauchgurt.
    Gibt den Zugpunkt (Mitte des Kummets) zurück."""
    a, b = knochen(ske, "Shoulders")
    achse = (b - a).normalized()
    na, nb = knochen(ske, "Neck")
    achse = (achse * 0.45 + (nb - na).normalized() * 0.55).normalized()      # Ring senkrecht zur Halsrichtung am Halsansatz
    t = max(0.0, min(1.0, (ziel_hoehe - a.z) / (b.z - a.z)))
    p = a + (b - a) * t
    lat = Vector((1, 0, 0))
    senk = achse.cross(lat).normalized()                               # quer zum Hals, in der Mittelebene
    pts = schnitt(bm, gw, ["Neck", "Shoulders", "Torso"], p, achse, 0.07)
    zentrum = sum(pts, Vector((0, 0, 0))) / len(pts)
    p = p + (zentrum - p) - achse * (zentrum - p).dot(achse)               # Mitte des Querschnitts, nicht der Knochenpunkt
    rx = max(abs(q.x - p.x) for q in pts) + 0.004
    rs = max(abs((q - p).dot(senk)) for q in pts) + 0.004
    n = 20
    bogen = KUMMET_BOGEN                                                 # Kummet von der Seite gebogen: die Seiten wölben sich nach vorn, Ober- und Unterseite liegen zurück
    ring = [p + (math.cos(2 * math.pi * k / n) * rx * lat + math.sin(2 * math.pi * k / n) * rs * senk) + achse * (bogen * math.cos(2 * math.pi * k / n) ** 2) for k in range(n)]
    pad_r = [0.054 + 0.026 * max(0.0, -math.sin(2 * math.pi * k / n)) ** 1.3 for k in range(n)]      # Unterteil (Brustpolster) dicker als oben
    rohr(bm, gw, "Shoulders", 6, ring, pad_r, n=8, geschlossen=True, mitte=p, achse=achse)
    # Hamen: flache Hölzer in einem geschlossenen Oval um den Polsterring (Kummetform): an den Seiten Holz, oben und unten Lederriemen;
    # am Scheitel jeder Seite ein deutlicher Zugring (Eisen) in der Seitenebene
    zug = 0.080 + 0.012
    n_h = 48
    ringe_b = []
    for k in range(n_h):
        w = 2 * math.pi * k / n_h
        rad = (math.cos(w) * lat * (rx + zug) + math.sin(w) * senk * (rs + zug))
        radn = (math.cos(w) * lat / max(rx + zug, 1e-3) + math.sin(w) * senk / max(rs + zug, 1e-3)).normalized()
        c = p + rad + achse * (bogen * math.cos(w) ** 2)
        ringe_b.append([bm.verts.new(c + achse * 0.040 + radn * 0.012), bm.verts.new(c + achse * 0.040 - radn * 0.012),
                        bm.verts.new(c - achse * 0.040 - radn * 0.012), bm.verts.new(c - achse * 0.040 + radn * 0.012)])
    for rg in ringe_b:
        for v in rg:
            v[gw.dl][gw.idx["Shoulders"]] = 1.0
    for k in range(n_h):
        w = 2 * math.pi * (k + 0.5) / n_h
        pl = 7 if abs(math.cos(w)) > 0.30 else 8
        a_, b_ = ringe_b[k], ringe_b[(k + 1) % n_h]
        for i in range(4):
            fl = bm.faces.new((a_[i], a_[(i + 1) % 4], b_[(i + 1) % 4], b_[i]))
            fl.material_index = pl
            fl.smooth = False
    for sx in (-1, 1):
        mitte_r = p + sx * (rx + zug + 0.075) * lat + achse * bogen
        u1, u2 = achse, senk
        pf = [mitte_r + (math.cos(2 * math.pi * k / 14) * u1 + math.sin(2 * math.pi * k / 14) * u2) * 0.048 for k in range(14)]
        rohr(bm, gw, "Shoulders", 8, pf, [0.014] * 14, n=6, geschlossen=True, mitte=mitte_r, achse=sx * lat)
        rohr(bm, gw, "Shoulders", 8, [p + sx * (rx + zug) * lat + achse * bogen, mitte_r - sx * lat * 0.035], [0.018, 0.018], n=6)
    # Hamenspitzen oben (zwei Hölzer mit Knauf, durch einen Riemen verbunden) und Zughaken unten
    obenp = p + senk * (rs + zug) + achse * 0.0
    for sx in (-1, 1):
        fuss_ = obenp + sx * lat * 0.050
        rohr(bm, gw, "Shoulders", 7, [fuss_ - senk * 0.02, fuss_ + senk * 0.085, fuss_ + senk * 0.115 + achse * 0.01], [0.017, 0.015, 0.011], n=6)
        rohr(bm, gw, "Shoulders", 7, [fuss_ + senk * 0.108 + achse * 0.008, fuss_ + senk * 0.150 + achse * 0.012], [0.026, 0.026], n=8)
    rohr(bm, gw, "Shoulders", 8, [obenp + lat * 0.050 + senk * 0.060, obenp + senk * 0.075 + achse * 0.01, obenp - lat * 0.050 + senk * 0.060], [0.008, 0.008, 0.008], n=5)
    untenp = p - senk * (rs + zug) + achse * 0.0
    for sx in (-1, 1):
        rohr(bm, gw, "Shoulders", 9, [untenp + sx * lat * 0.05 + senk * 0.01, untenp + sx * lat * 0.05 - senk * 0.045 + achse * 0.01, untenp + sx * lat * 0.05 - senk * 0.075 + achse * 0.045], [0.014, 0.013, 0.010], n=6)
    # Sattelgurt (Bauchgurt) hinter den Vorderbeinen: liegt der Haut an (Strahl vom Querschnittsmittelpunkt), kein frei hängender Reif
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromBMesh(bm)
    y = (a.y + knochen(ske, "Hips")[0].y) * 0.5
    scheibe = schnitt(bm, gw, ["Torso", "Body"], Vector((0, y, 1.0)), Vector((0, 1, 0)), 0.09)
    zmin, zmax = min(q.z for q in scheibe), max(q.z for q in scheibe)
    mz = (zmin + zmax) / 2
    m = 28
    gurt = []
    for k in range(m):
        w = 2 * math.pi * k / m
        d = Vector((math.cos(w), 0, math.sin(w)))
        hit = bvh.ray_cast(Vector((0, y, mz)) + d * 1.0, -d, 1.2)
        if hit[0] is None:
            hit = bvh.ray_cast(Vector((0, y, mz)), d, 1.2)
        gurt.append(hit[0] + d * 0.010 if hit[0] is not None else Vector((0, y, mz)) + d * 0.35)
    rohr(bm, gw, "Torso", 8, gurt, [0.020] * m, n=6, geschlossen=True, mitte=Vector((0, y, mz)), achse=Vector((0, 1, 0)))
    return p


def _kette(ske, namen):
    """Punkte (Kopf des ersten Knochens, dann die Schwänze) einer Knochenkette."""
    return [Vector(ske.data.bones[namen[0]].head_local)] + [Vector(ske.data.bones[n].tail_local) for n in namen]


def _auf_kette(pts, s):
    """Punkt bei s (0 bis 1 über die Länge der Kette)."""
    laengen = [(pts[i + 1] - pts[i]).length for i in range(len(pts) - 1)]
    ziel = s * sum(laengen)
    for i, l in enumerate(laengen):
        if ziel <= l or i == len(laengen) - 1:
            u = max(0.0, min(1.0, ziel / l if l else 0.0))
            return pts[i].lerp(pts[i + 1], u)
        ziel -= l


class Haar:
    """Haar aus verjüngten Bändern und gefächerten Flächen mit eigenem Stoff „Haar“, nur im Nahnetz. Je Fläche ein Helligkeitsfaktor
    (Spalten abwechselnd heller und dunkler: wirkt wie Strähnen), keine Alphatextur."""

    def __init__(self, bm, gw):
        self.bm, self.gw = bm, gw
        self.faktor = []                      # (Fläche, Faktor)

    def _gewicht(self, v, gewichte, p, i):
        for name, wert in gewichte(p, i).items():
            v[self.gw.dl][self.gw.idx[name]] = wert

    def straehne(self, pfad, breite, seite, gewichte, stufe=0, nr=0):
        """Band entlang `pfad` (Wurzel zuerst), halbe Breite verjüngt zur Spitze; `seite` = Richtung der Breite."""
        bm = self.bm
        n = len(pfad)
        ringe = []
        for i, p in enumerate(pfad):
            if i < n - 1:
                w = breite * (1.0 - i / (n - 1)) ** 0.75
                ringe.append([bm.verts.new(p - seite * w), bm.verts.new(p + seite * w)])
            else:
                ringe.append([bm.verts.new(p)])
        for i, ring in enumerate(ringe):
            for v in ring:
                self._gewicht(v, gewichte, pfad[i], i)
        f = 0.82 + 0.34 * _rauschen(Vector((nr * 1.7, stufe * 3.1, 0.5)))
        for i in range(n - 1):
            a, b = ringe[i], ringe[i + 1]
            fl = bm.faces.new((a[0], a[1], b[1], b[0])) if len(b) == 2 else bm.faces.new((a[0], a[1], b[0]))
            fl.material_index = HAAR
            fl.smooth = True
            self.faktor.append((fl, f))

    def flaeche(self, gitter, gewichte, stufe=0, umdrehen=False, hell=1.0, ring=False):
        """Geschlossene Fläche aus einem Gitter von Punkten gitter[spalte][zeile] (Zeile 0 = Wurzel); die untere Kante darf
        je Spalte verschieden lang sein (gezackt). Spalten wechseln die Helligkeit."""
        bm = self.bm
        vs = [[bm.verts.new(p) for p in sp] for sp in gitter]
        for c, sp in enumerate(vs):
            for r, v in enumerate(sp):
                self._gewicht(v, gewichte, gitter[c][r], r)
        for c in range(len(vs) - (0 if ring else 1)):
            f = 0.80 + 0.34 * _rauschen(Vector((c * 1.3, stufe * 2.7, 0.3)))
            c2 = (c + 1) % len(vs)
            for r in range(len(vs[c]) - 1):
                q = (vs[c][r], vs[c2][r], vs[c2][r + 1], vs[c][r + 1])
                fl = bm.faces.new(q[::-1] if umdrehen else q)
                fl.material_index = HAAR
                fl.smooth = True
                self.faktor.append((fl, hell * f * (0.74 + 0.55 * r / max(1, len(vs[c]) - 2))))

    def lock(self, pfad, breiten, dicken, aussen, gewichte, f0, f1, jitter=1.0):
        """Geschlossene Locke: Rohr mit rautenförmigem Querschnitt (Breite quer zur Laufrichtung, Dicke nach `aussen`), spitz auslaufend;
        glatt schattiert, Helligkeit von Wurzel (f0, dunkler) zur Spitze (f1, heller). Die Wurzel ist mit einem Viereck verschlossen."""
        bm = self.bm
        n = len(pfad)
        ringe = []
        for i, p in enumerate(pfad):
            t = (pfad[min(i + 1, n - 1)] - pfad[max(i - 1, 0)]).normalized()
            b = t.cross(aussen)
            if b.length < 1e-4:
                b = t.cross(Vector((0, 0, 1)))
            b.normalize()
            o = b.cross(t).normalized()
            if o.dot(aussen) < 0:
                o = -o
            if i < n - 1:
                ringe.append([bm.verts.new(p + b * breiten[i]), bm.verts.new(p + o * dicken[i]),
                              bm.verts.new(p - b * breiten[i]), bm.verts.new(p - o * dicken[i] * 0.5)])
            else:
                ringe.append([bm.verts.new(p)])
        for i, ring in enumerate(ringe):
            for v in ring:
                self._gewicht(v, gewichte, pfad[i], i)
        for i in range(n - 1):
            a, b = ringe[i], ringe[i + 1]
            f = jitter * (f0 + (f1 - f0) * (i + 0.5) / (n - 1))
            if len(b) == 1:
                tri = [(a[k], a[(k + 1) % 4], b[0]) for k in range(4)]
            else:
                tri = [(a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]) for k in range(4)]
            for q in tri:
                fl = bm.faces.new(q)
                fl.material_index = HAAR
                fl.smooth = True
                self.faktor.append((fl, f))
        fl = bm.faces.new(ringe[0][::-1])
        fl.material_index = HAAR
        fl.smooth = True
        self.faktor.append((fl, f0 * jitter))

    def buendel(self, mitte_pfad, halbmesser, laenge_extra, n_band, breite, gewichte, stufe=0, achse1=None):
        """Rundes Bündel aus n_band Bändern um die Linie `mitte_pfad` (Liste von Punkten, Wurzel zuerst), `halbmesser(i)` je Punkt, am Ende
        `laenge_extra` Meter nach unten verlängert."""
        pfade = [mitte_pfad[i] for i in range(len(mitte_pfad))]
        pfade.append(pfade[-1] + Vector((0, 0.03, -laenge_extra)))
        n = len(pfade)
        for k in range(n_band):
            ang = 2 * math.pi * k / n_band
            lang = 1.0 - 0.28 * _rauschen(Vector((k, stufe, 1.0)))                # Längenunterschied: die Spitzen sind gezackt
            pf = []
            for i, q in enumerate(pfade):
                tang = (pfade[min(i + 1, n - 1)] - pfade[max(i - 1, 0)]).normalized()
                e1 = Vector((1, 0, 0))
                e1 = (e1 - tang * e1.dot(tang)).normalized()
                e2 = tang.cross(e1)
                r = halbmesser(i, n)
                dazu = (q - pfade[-2]) * 0.0
                pf.append(q + (math.cos(ang) * e1 + math.sin(ang) * e2) * r)
            ende = int(round((n - 1) * lang))
            pf = pf[(k % 4):max(4, ende + 1)]                                      # Ansatz gestaffelt: keine geschlossene Kante, kein Block
            tg = (Vector((-math.sin(ang), 0, 0)) * 1.0 + Vector((0, math.cos(ang), 0)) * 0.0)
            seite = Vector((-math.sin(ang), 0, 0)) if abs(math.sin(ang)) > 0.2 else Vector((0, 1, 0))
            seite = (seite - (pf[-1] - pf[0]).normalized() * seite.dot((pf[-1] - pf[0]).normalized())).normalized()
            self.straehne(pf, breite, seite, gewichte, stufe, k)


def haare(bm, gw, ske, art):
    """Pferd: Mähne (gefächerte Fläche am Halskamm, außen am Hals herunter), Stirnschopf, Schweifhaar (rundes Bündel), Fesselbehang.
    Ochse: Schwanzquaste. Gibt das Haar-Objekt zurück."""
    h = Haar(bm, gw)
    namen_schweif = ["Tail1", "Tail2", "Tail3", "Tail4"]
    schweif = _kette(ske, namen_schweif)

    def w_schweif(p, i):
        best, bd, bu = 0, 1e9, 0.0
        for k in range(len(namen_schweif)):
            a, b = schweif[k], schweif[k + 1]
            ab = b - a
            u = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
            d = (p - (a + ab * u)).length
            if d < bd:
                best, bd, bu = k, d, u
        if best + 1 < len(namen_schweif):
            return {namen_schweif[best]: 1.0 - 0.5 * bu, namen_schweif[best + 1]: 0.5 * bu}
        return {namen_schweif[best]: 1.0}

    if art == "pferd":
        bmesh.ops.delete(bm, geom=[fl for fl in bm.faces if sum(gw.summe(v, namen_schweif) for v in fl.verts) / len(fl.verts) > 0.85], context="FACES")   # die flache Platte der Vorlage
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
        a, b = knochen(ske, "Neck")
        achse = (b - a).normalized()
        kamm = []
        for u in (-0.30, -0.12, 0.05, 0.25, 0.45, 0.65, 0.85, 1.0):
            q = a + (b - a) * u
            kand = [v.co for v in bm.verts if gw.summe(v, ["Neck", "Shoulders", "Head"]) > 0.3
                    and abs((v.co - q).dot(achse)) < 0.05 and abs(v.co.x) < 0.1]
            if kand:
                top = max(kand, key=lambda c: c.z)
                kamm.append(Vector((0.0, top.y, top.z)))

        def halbbreite(y, z):
            xs = [abs(v.co.x) for v in bm.verts if gw.summe(v, ["Neck"]) > 0.3
                  and abs(v.co.y - y) < 0.06 and abs(v.co.z - z) < 0.05]
            return max(xs) if xs else 0.10

        if len(kamm) >= 3:
            # Mähne: EINE geschlossene Schale über den Halskamm; jeder Punkt liegt auf der Haut des Halses (Strahl gegen das Netz, dazu 1 cm)
            from mathutils.bvhtree import BVHTree
            bvh = BVHTree.FromBMesh(bm)

            def haut(y, z, seite, dicke):
                if seite == 0:                                          # Scheitel: von oben auf den Kamm
                    hit = bvh.ray_cast(Vector((0.0, y, z + 0.5)), Vector((0, 0, -1)), 1.0)
                else:
                    hit = bvh.ray_cast(Vector((seite * 1.0, y, z)), Vector((-seite, 0, 0)), 1.5)
                if hit[0] is None:
                    return Vector((seite * 0.10, y, z))
                n = hit[1]
                if (seite == 0 and n.z < 0) or (seite != 0 and n.x * seite < 0):
                    n = -n
                return hit[0] + n.normalized() * dicke

            from mathutils.kdtree import KDTree
            haut_ecken = [v for v in bm.verts if v.is_valid and not any(l.material_index == HAAR for l in v.link_faces)]
            kd = KDTree(len(haut_ecken))
            for i_, v_ in enumerate(haut_ecken):
                kd.insert(v_.co, i_)
            kd.balance()
            inv = {i: n for n, i in gw.idx.items()}

            def w_haut(p, i):
                _, j, _ = kd.find(p)
                d = haut_ecken[j][gw.dl]
                return {inv[k]: d[k] for k in d.keys()}

            # Unterlage: dünne, kürzere geschlossene Schale auf der Haut (deckt Lücken zwischen den Locken, keine sichtbare Kante)
            spalten = 24
            gitter = []
            for c in range(spalten):
                s_ = c / (spalten - 1)
                f = s_ * (len(kamm) - 1)
                i0 = min(len(kamm) - 2, int(f))
                p0 = kamm[i0].lerp(kamm[i0 + 1], f - i0)
                rand = min(_weich(s_ / 0.35), (1.0 - s_) * 5.0 + 0.35)
                laenge = 0.62 * rand * (0.26 + 0.05 * math.sin(math.pi * (0.1 + 0.8 * s_)))
                links = 0.7 * rand * 0.07
                sp = []
                for r in range(4):
                    q = 1.0 - r / 3.0
                    z = p0.z - laenge * q
                    y = p0.y + 0.04 * q * q * (1 + s_)
                    sp.append(haut(y, z, -1.0, 0.004 + 0.008 * q) if q > 0.05 else haut(p0.y, p0.z, 0.0, 0.004))
                sp.append(haut(p0.y, p0.z, 0.0, 0.005))
                for r in range(1, 3):
                    q = r / 2.0
                    sp.append(haut(p0.y + 0.004 * q, p0.z - links * q, 1.0, 0.004))
                gitter.append(sp)
            h.flaeche(gitter, w_haut, 0, hell=0.60)
            # Locken: breite, weiche Strähnen vom Kamm (vom Genick bis zum Widerrist) über die Halsseite herab; Länge und Schwung wechseln fließend,
            # die Ecken hängen an den Gewichten der nächsten Hautecke (folgen Hals, Schulter und Kopf)
            anzahl = 38
            for c in range(anzahl):
                s_ = (c + 0.5) / anzahl
                f = s_ * (len(kamm) - 1)
                i0 = min(len(kamm) - 2, int(f))
                p0 = kamm[i0].lerp(kamm[i0 + 1], f - i0)
                rand = min(0.10 + 0.90 * _weich(s_ / 0.38), (1.0 - s_) * 6.0 + 0.50)           # am Genick kurz auslaufend, keine Kante
                zuf = _rauschen(Vector((c * 2.3, 4.0, 0.7)))
                welle = 0.5 + 0.5 * math.sin(2 * math.pi * 1.6 * s_ + 0.6)
                laenge = rand * (0.30 + 0.07 * math.sin(math.pi * (0.1 + 0.8 * s_))) * (0.78 + 0.34 * welle + 0.14 * zuf)
                pf, br, di = [], [], []
                pf.append(haut(p0.y, p0.z, 0.0, 0.016))
                br.append(0.032)
                di.append(0.018)
                for k in range(1, 7):
                    q = k / 6.0
                    z = p0.z - laenge * q
                    y = p0.y + 0.07 * q * q * (1 + s_) + 0.034 * math.sin(2 * math.pi * (0.8 * q + 1.3 * s_) + c * 0.9) * q
                    pf.append(haut(y, z, -1.0, 0.014 + 0.032 * math.sin(math.pi * (0.10 + 0.75 * q)) + 0.010 * zuf))
                    br.append(0.036 * (1.0 - 0.78 * q ** 1.4))
                    di.append(0.022 * (1.0 - 0.72 * q))
                aussen = Vector((-1, 0, 0.4)).normalized()
                h.lock(pf, br, di, aussen, w_haut, 0.72, 1.12, jitter=(0.80 if c % 2 else 1.06) * (0.92 + 0.16 * zuf))     # zwei Töne im Wechsel, Spitzen nur wenig heller
        # Loch der Vorlage schließen, Schweifrübe (Rundkörper aus der Kruppe) mit Haarlocken, die aus ihr wachsen
        rand_k = [e for e in bm.edges if e.is_boundary]
        if rand_k:
            bmesh.ops.triangulate(bm, faces=bmesh.ops.holes_fill(bm, edges=rand_k, sides=12)["faces"])
        loch = Vector((0.0, 0.99, 1.78))
        kette = [Vector((0.0, loch.y - 0.07, loch.z)), schweif[0].lerp(schweif[1], 0.15)] + schweif[1:]

        def punkt_d(d):
            """Punkt und Richtung bei Strecke d entlang der Schweifkette (über das Ende hinaus geradeaus)."""
            rest = d
            for j in range(len(kette) - 1):
                l = (kette[j + 1] - kette[j]).length
                if rest <= l:
                    return kette[j].lerp(kette[j + 1], rest / l), (kette[j + 1] - kette[j]).normalized()
                rest -= l
            t = (kette[-1] - kette[-2]).normalized()
            return kette[-1] + t * rest, t

        ruebe_l = 0.34
        pfad_r = [punkt_d(ruebe_l * m / 8.0)[0] for m in range(9)]
        rohr(bm, gw, "Tail1", HAAR, pfad_r, [0.092, 0.092, 0.088, 0.082, 0.078, 0.074, 0.072, 0.070, 0.066], n=12)
        gesamt = 1.35
        lockenzahl = 40
        for c in range(lockenzahl):
            ang = 2 * math.pi * (c + 0.35 * _rauschen(Vector((c, 9.0, 0.1)))) / lockenzahl
            innen = 1.0 if c % 3 else 0.55
            zuf = _rauschen(Vector((c * 1.9, 6.0, 0.4)))
            ende = gesamt * (0.62 + 0.38 * (1.0 - zuf ** 1.5))
            pf, br, di = [], [], []
            seg = 8
            for k in range(seg + 1):
                d = 0.20 + (ende - 0.20) * k / seg
                q, t = punkt_d(d)
                e1 = Vector((1, 0, 0))
                e1 = (e1 - t * e1.dot(t)).normalized()
                e2 = t.cross(e1).normalized()
                fa = min(1.0, k / 2.0)
                rad = (0.066 + 0.030 * math.sin(math.pi * min(1.0, d / 1.0)) ** 0.8) * innen * (0.55 + 0.45 * fa)
                pf.append(q + (math.cos(ang) * e1 + math.sin(ang) * e2) * rad + e1 * 0.012 * math.sin(2 * math.pi * (d * 1.6 + c * 0.13)))
                u = k / seg
                br.append(0.020 * (1.0 - 0.85 * u ** 1.5))
                di.append(0.020 * (1.0 - 0.80 * u))
            aussen = pf[0] - punkt_d(0.2)[0]
            aussen = aussen.normalized() if aussen.length > 1e-3 else Vector((0, 0, 1))
            h.lock(pf, br, di, aussen, w_schweif, 0.70, 1.22, jitter=0.82 + 0.34 * _rauschen(Vector((c * 0.7, 2.0, 0.9))))
    else:
        # Schwanzquaste: weiche Spindel aus überlappenden Locken, die aus dem dünnen Schwanz wachsen und nach unten fächern (keine Platten)
        basis = [_auf_kette(schweif, 0.70 + 0.30 * m / 4.0) for m in range(5)]
        basis += [basis[-1] + Vector((0, 0, -0.10)), basis[-1] + Vector((0, 0, -0.20))]
        mitte_pfad = basis

        def punkt_q(d):
            rest = d
            for j in range(len(mitte_pfad) - 1):
                l = (mitte_pfad[j + 1] - mitte_pfad[j]).length
                if rest <= l:
                    return mitte_pfad[j].lerp(mitte_pfad[j + 1], rest / l), (mitte_pfad[j + 1] - mitte_pfad[j]).normalized()
                rest -= l
            return mitte_pfad[-1] + Vector((0, 0, -rest)), Vector((0, 0, -1))

        gesamt = sum((mitte_pfad[j + 1] - mitte_pfad[j]).length for j in range(len(mitte_pfad) - 1))
        for c in range(20):
            ang = 2 * math.pi * (c + 0.3 * _rauschen(Vector((c, 3.0, 0.2)))) / 20
            innen = 1.0 if c % 2 else 0.55
            zuf = _rauschen(Vector((c * 1.7, 8.0, 0.3)))
            ende = gesamt * (0.72 + 0.28 * (1.0 - zuf ** 1.3)) + 0.10
            pf, br, di = [], [], []
            seg = 7
            for k in range(seg + 1):
                u = k / seg
                d = ende * u
                q, t = punkt_q(d)
                e1 = Vector((1, 0, 0))
                e1 = (e1 - t * e1.dot(t)).normalized()
                e2 = t.cross(e1).normalized()
                rad = (0.012 + 0.050 * math.sin(math.pi * min(1.0, 0.15 + 0.85 * u)) ** 0.9) * innen * (0.35 + 0.65 * min(1.0, k / 2.0))
                pf.append(q + (math.cos(ang) * e1 + math.sin(ang) * e2) * rad)
                br.append(0.016 * (1.0 - 0.88 * u ** 1.4))
                di.append(0.016 * (1.0 - 0.82 * u))
            aussen = pf[2] - punkt_q(ende * 2 / seg)[0]
            aussen = aussen.normalized() if aussen.length > 1e-3 else Vector((1, 0, 0))
            h.lock(pf, br, di, aussen, w_schweif, 0.40, 0.80, jitter=0.82 + 0.34 * _rauschen(Vector((c * 0.8, 1.0, 0.6))))
    return h


def joch(bm, gw, ske, s, ziel_hoehe=1.45):
    """Ochse: Nackenjoch (Querholz mit zwei Bügeln unter dem Hals) am Halsansatz; die Lage am Halsknochen wird so gewählt, dass der
    Zugpunkt (Mitte des Querholzes) möglichst nahe an `ziel_hoehe` liegt. Gibt den Zugpunkt zurück."""
    a, b = knochen(ske, "Neck")
    beste = None
    for k in range(0, 36):
        u = 0.0 + 0.02 * k
        p = a + (b - a) * u
        kand = [v.co.z for v in bm.verts if gw.summe(v, ["Neck", "Shoulders"]) > 0.3 and abs(v.co.x) < 0.08 and abs(v.co.y - p.y) < 0.1]
        if not kand:
            continue
        z0 = max(kand) + 0.035
        if beste is None or abs(z0 + 0.03 - ziel_hoehe) < beste[0]:
            beste = (abs(z0 + 0.03 - ziel_hoehe), p, z0)
    _, p, z0 = beste
    unter = min((v.co.z for v in bm.verts if gw.summe(v, ["Neck"]) > 0.3 and abs(v.co.x) < 0.1 and abs(v.co.y - p.y) < 0.1), default=p.z - 0.2)
    breite = max(abs(v.co.x) for v in bm.verts if gw.summe(v, ["Neck", "Shoulders"]) > 0.3 and abs(v.co.y - p.y) < 0.1) + 0.04
    rohr(bm, gw, "Shoulders", 7, [Vector((-breite - 0.2, p.y, z0)), Vector((0, p.y, z0 + 0.01)), Vector((breite + 0.2, p.y, z0))],
         [0.046, 0.054, 0.046], n=4)
    for sx in (-1, 1):                                                # Bügel
        x = sx * (breite - 0.02)
        rohr(bm, gw, "Shoulders", 7, [Vector((x, p.y, z0)), Vector((x + sx * 0.012, p.y, (z0 + unter) / 2)), Vector((x, p.y, unter - 0.02))],
             [0.024, 0.022, 0.020], n=6)
    rohr(bm, gw, "Shoulders", 7, [Vector((-breite + 0.02, p.y, unter - 0.02)), Vector((breite - 0.02, p.y, unter - 0.02))], [0.016, 0.016], n=6)
    return Vector((0, p.y, z0 + 0.03))


def hoerner(bm, gw, ske, s):
    kopf_a, kopf_b = knochen(ske, "Head")
    for sx in (-1, 1):
        kand = [v for v in bm.verts if gw.summe(v, ["Head"]) > 0.5 and abs(v.co.y - kopf_a.y) < 0.22 and sx * v.co.x > 0.03]
        base = max(kand, key=lambda v: v.co.z).co.copy()
        base.x += sx * 0.04
        pfad = [base + Vector(d) for d in ((0, 0, -0.02), (sx * 0.09, -0.03, 0.02), (sx * 0.17, -0.07, 0.05),
                                          (sx * 0.23, -0.13, 0.10), (sx * 0.25, -0.20, 0.17), (sx * 0.22, -0.26, 0.24))]
        rohr(bm, gw, "Head", 4, pfad, [0.046, 0.041, 0.034, 0.026, 0.016, 0.006], n=7, teilung=4, platz2=5)


# ─────────────────────────────────────────────────────────────────────────────
#  Eckenfarbe: Verlauf
# ─────────────────────────────────────────────────────────────────────────────

def verlauf(me, ob, plaetze, fell_flaechen, haar_faktor=None, art=None, variante=None):
    """Gegenschattierung wie bei Huftieren: Rücken dunkler, Bauch und Innenseiten der Beine heller, Beine unten dunkler („Stiefel“),
    dazu leichtes Rauschen – nur auf Fell (nicht auf Leder und Holz). Haarsträhnen: je Strähne ein Helligkeitsfaktor, Wurzel dunkler."""
    attr = me.color_attributes.get("Farbe")
    if attr is None:
        return
    zs = [v.co.z for v in me.vertices]
    z0, z1 = min(zs), max(zs)
    hoehe = max(0.3, z1 - z0)
    namen_g = {g.index: g.name for g in ob.vertex_groups}
    for poly in me.polygons:
        haar = haar_faktor.get(poly.index) if haar_faktor else None
        if haar is None and poly.index not in fell_flaechen:
            continue
        for li in poly.loop_indices:
            v = me.vertices[me.loops[li].vertex_index]
            p = v.co
            if haar is not None:
                f = haar * (0.80 + 0.30 * _weich((p.z - z0) / hoehe))
            else:
                n = v.normal
                bauch = _weich(-n.z * 1.6 - 0.1)                       # Unterseite
                ruecken = _weich(n.z * 1.4 - 0.2)                      # Oberseite
                f = 0.95 + 0.50 * bauch - 0.20 * ruecken
                f *= 1.0 - 0.40 * _weich((0.55 - p.z) / 0.45)           # unten dunkler
                f *= 1.0 + (_rauschen(p * 3.0) - 0.5) * 0.05
                # Fellschattierung: großflächige Töne (Handbreit bis Handspanne) und Haarstrich in Körperrichtung
                f *= 1.0 + 0.30 * (_glatt(p * 4.5) - 0.5) + 0.18 * (_glatt(Vector((p.x * 11.0, p.y * 3.0, p.z * 11.0))) - 0.5)
                kh_ = sum(g.weight for g in v.groups if namen_g.get(g.group) in ("Head", "Neck"))
                if art == "ochse":
                    f *= 1.0 - 0.20 * min(1.0, kh_)                       # Kopf und Hals dunkler als der Rumpf
                if art == "pferd" and variante == "schwarzbraun":
                    band = _weich(1.0 - abs(n.z - 0.40) / 0.45)            # Schimmer: Kanten der Muskeln und Flanken fangen Licht
                    f *= 1.0 + 0.55 * band * (0.45 + 0.55 * _glatt(Vector((p.x * 7.0, p.y * 2.2, p.z * 7.0))))
                    d_ = _glatt(p * 17.0)                                   # Apfelung: weiche hellere Flecken
                    f *= 1.0 + 0.32 * _weich((d_ - 0.58) / 0.14) * _weich(n.z + 0.35)
            c = attr.data[li].color
            attr.data[li].color = (min(1.0, c[0] * f), min(1.0, c[1] * f), min(1.0, c[2] * f), 1.0)


def abzeichen(me, ob, ske, art, variante, fell_flaechen):
    """Binnenzeichnung nach Farbschlag: Fuchs und roter Ochse mit Stirnstern und schmaler Blesse, Rappe mit hellem Mehlmaul, Falbe mit dunklem Aalstrich
    am Rücken; als Eckenfarbe (Mischen über weiche Gewichte)."""
    attr = me.color_attributes.get("Farbe")
    if attr is None:
        return
    namen = {g.index: g.name for g in ob.vertex_groups}
    a = Vector(ske.data.bones["Head"].head_local)
    ax = Vector(ske.data.bones["Head"].tail_local) - a
    laenge = ax.length
    ax.normalize()
    for poly in me.polygons:
        if poly.index not in fell_flaechen:
            continue
        for li in poly.loop_indices:
            v = me.vertices[me.loops[li].vertex_index]
            p, n = v.co, v.normal
            kopf = sum(g.weight for g in v.groups if namen.get(g.group) == "Head")
            rumpf = sum(g.weight for g in v.groups if namen.get(g.group) in ("Torso", "Hips", "Neck", "Shoulders", "Body"))
            t = (p - a).dot(ax) / max(laenge, 1e-3)
            k = (1.0, 1.0, 1.0)
            if variante in ("rotbraun",) and kopf > 0.5:
                breit = 0.050 if art == "pferd" else 0.075
                if art == "pferd":
                    w = _weich((0.80 - t) / 0.2) * _weich((t - 0.0) / 0.2) * _weich((breit - abs(p.x)) / 0.012) * _weich(n.z * 3.0 + 0.2)
                    w *= 1.0 if t > 0.35 else 1.0
                else:
                    w = _weich((0.55 - t) / 0.15) * _weich((t - 0.10) / 0.15) * _weich((breit - abs(p.x)) / 0.012) * _weich(n.z * 3.0 + 0.2)
                k = (1.0 + 1.1 * w, 1.0 + 2.2 * w, 1.0 + 3.2 * w)
            elif variante == "schwarzbraun" and kopf > 0.5 and t > 0.55:
                w = _weich((t - 0.55) / 0.45)
                k = (1.0 + 0.28 * w, 1.0 + 0.32 * w, 1.0 + 0.34 * w)
            elif variante == "falb" and rumpf > 0.4 and abs(p.x) < 0.095 and n.z > 0.10:
                w = _weich((0.095 - abs(p.x)) / 0.040) * _weich((n.z - 0.10) / 0.3)       # Aalstrich: breit, weich ausklingend
                k = (1.0 - 0.50 * w, 1.0 - 0.60 * w, 1.0 - 0.66 * w)
            c = attr.data[li].color
            attr.data[li].color = (min(1.0, c[0] * k[0]), min(1.0, c[1] * k[1]), min(1.0, c[2] * k[2]), 1.0)


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen
# ─────────────────────────────────────────────────────────────────────────────

def schreiben_clip(ske, name, rahmen, mit_loc):
    ske.animation_data_create()
    act = bpy.data.actions.new(name)
    ske.animation_data.action = act
    for i, r in enumerate(rahmen):
        for pb in ske.pose.bones:
            loc, q = r[pb.name]
            pb.rotation_mode = "QUATERNION"
            pb.location = loc
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=i + 1)
            if pb.name in mit_loc:
                pb.keyframe_insert("location", frame=i + 1)
    spur = ske.animation_data.nla_tracks.new()
    spur.name = name
    spur.strips.new(name, 1, act)
    ske.animation_data.action = None


def abtasten(bilder, phase):
    """Rahmen (Knochen -> (Verschiebung, Drehung)) an der Phase 0..1 der Schleife, linear bzw. kugelförmig gemischt."""
    n = len(bilder)
    x = (phase % 1.0) * n
    i = int(math.floor(x))
    t = x - i
    a, b = bilder[i % n], bilder[(i + 1) % n]
    return {k: (a[k][0].lerp(b[k][0], t), a[k][1].slerp(b[k][1], t)) for k in a}


def schleife(bilder, anzahl, skala):
    """anzahl Bilder + Schlussbild (gleich dem ersten), Verschiebungen mit `skala` (Maßstab) multipliziert."""
    out = []
    for f in range(anzahl + 1):
        r = abtasten(bilder, (f % anzahl) / anzahl)
        out.append({k: (v[0] * skala, v[1].copy()) for k, v in r.items()})
    return out


def versatz(rahmen, ske, grad, wellen=None):
    """Dreht Knochen zusätzlich um die Querachse (Nase nach unten bei positivem Winkel): grad = {Knochen: Winkel}.
    wellen = (Takte je Schleife, Anteil) lässt den Winkel kauen/atmen."""
    achsen = {}
    for n in grad:
        b = ske.data.bones[n]
        achsen[n] = b.matrix_local.to_3x3().inverted() @ Vector((1, 0, 0))
    n_r = len(rahmen)
    aus = []
    for i, r in enumerate(rahmen):
        neu = dict(r)
        faktor = 1.0
        if wellen:
            faktor += wellen[1] * math.sin(2 * math.pi * wellen[0] * i / max(1, n_r - 1))
        for n, g in grad.items():
            loc, q = neu[n]
            neu[n] = (loc, Quaternion(achsen[n], math.radians(g * faktor)) @ q)
        aus.append(neu)
    return aus


# Lage des Ankers im Ziehen gegenüber der Ruhelage (Godot-Koordinaten x, Höhe, Strecke vor dem Ursprung; Mittel über die Schleife, gemessen mit zugtiere_gespann.py):
# der Rumpf sinkt im Gehen und Ziehen, bis jedes Bein sein Hufziel erreicht, das Geschirr sinkt mit
ANKER_ZIEHEN_VERSATZ = {"pferd": (0.003, -0.083, 0.003), "ochse": (0.004, -0.120, 0.007)}
ANKER_ABWEICHUNG_MM = {"pferd": (18, 25), "ochse": (51, 39)}     # Ziehen, Gehen ohne Zug (um die Mitte, gemessen mit zugtiere_gespann.py)


def clips_schreiben(art, cfg, clips, daten, zug=None):
    """game/daten/zugtiere.json: je Tierart die Clips mit Bildern, Dauer, Schleife, Weg des Rumpfes je Bild und Anlaufkurve (für den Code)."""
    pfad = os.path.join(bs.WURZEL, "game", "daten", "zugtiere.json")
    alle = json.load(open(pfad, encoding="utf-8")) if os.path.exists(pfad) else {}
    par = za.PARAMETER[art]
    klips = {}
    for name in REIHENFOLGE:
        n = len(clips[name]) - 1
        k = {"bilder": n + 1, "dauer": round(n / FPS, 4), "schleife": not name.startswith(("Anfahren", "Anhalten"))}
        if name in daten:
            k["weg"] = [round(x, 5) for x in daten[name]["X"]]
            k["anlauf"] = [round(x, 4) for x in daten[name]["A"]]
        klips[name] = k
    alle.setdefault("hinweis", "Erzeugt von tools/modelle/zugtiere.py. Zeiten in Sekunden, Wege in Metern, Bilder zu 24 je Sekunde.")
    alle["fps"] = FPS
    alle["regeln"] = {
        "schleife": "Jeder Clip hat ein Schlussbild gleich dem ersten (Bilder = Dauer * 24 + 1), Godot nennt ihn deshalb um 1/24 s zu lang. "
                    "Schleifen (\"schleife\": true): Animation.Length = dauer des Clips aus dieser Datei (je Tierart und Clip: tiere.<art>.klips.<Clip>.dauer; "
                    "tiere.<art>.schleife = Dauer von Gehen und Ziehen) setzen und LoopMode = Linear, dann ist die Periode genau dauer und die Naht ohne Sprung. "
                    "Anfahren_/Anhalten_ laufen einmal in voller Länge.",
        "rad": "Drehwinkel eines Rads (Bogenmaß) = Weg des Karrens (m) / Halbmesser des Rads (m); bei Tempo v dreht es mit v / Halbmesser rad/s. "
               "Der Karren fährt mit v · anlauf(t) des gerade gespielten Clips (Anfahren_*: anlauf steigt 0 bis 1, Anhalten_*: fällt 1 bis 0).",
        "blenden": "Stehen, Stehen_Ruhen, Stehen_Umschauen, Fressen beginnen und enden in derselben Grundpose: ohne Blendzeit nacheinander "
                   "spielbar, nach jedem Durchlauf zufällig die nächste Variante. Stehen -> Anfahren_X: Blendzeit 0,05 s. Anfahren_X endet in "
                   "Bild 0 von Gehen/Ziehen: danach Gehen/Ziehen von Anfang, Blendzeit 0. Gehen <-> Ziehen: Blendzeit 0,4 s und dieselbe "
                   "Abspielstelle behalten (seek), die Hufe sind in beiden gleich geplant. Gehen/Ziehen -> Anhalten_X: erst am Ende des "
                   "laufenden Durchlaufs (Bild 0), dann Anhalten_X mit Blendzeit 0; danach Stehen mit Blendzeit 0,05 s.",
        "gespann": "Anker_Geschirr (Zugpunkt, Blickrichtung des Tiers) hängt am eigenen, nie bewegten Knochen Geschirr (Kind des Wurzelknochens, in Godot ein BoneAttachment3D) "
                   "und liegt in ALLEN Clips fest auf der mittleren Lage des Geschirrs im Ziehen (gemessene Abweichung 0,0 mm in Stehen, Gehen, Ziehen); Kummet bzw. Joch wippen mit dem Rumpf darunter "
                   "(Pferd +-35 mm, Ochse +-50 mm), die Zugstränge geben nach. Beim Anhalten gibt es deshalb keinen Sprung am Karren. "
                   "Platzierung: Tier-Wurzel = Position(Anker_Deichsel des Karrens) - Drehung(Karren) * tiere.<art>.anker_ziehen_godot (x, Höhe, Strecke vor dem Ursprung; "
                   "Godot-Koordinaten; Pferd 1,56 m hoch, 0,67 m vor dem Ursprung, Ochse 1,56 m und 1,03 m; anker_lokal_godot ist derselbe Wert); "
                   "Ausrichtung des Tiers = Ausrichtung des Karrens (beide blicken nach +Z). Im Stehen sitzt das Kummet 8 cm (Pferd) bzw. 12 cm (Ochse) höher als im Ziehen "
                   "(der Rumpf sinkt beim Gehen und Ziehen, sonst erreichen die Beine ihre Hufziele nicht), der Anker nicht. Beim Zweispänner Anker_Pferd_L (x = +0,62) und Anker_Pferd_R (x = -0,62) statt Anker_Deichsel "
                   "(Abstand der Tiere 1,24 m).",
        "blendzeit_gehen_ziehen": {"pferd": 0.4, "ochse": 0.6},
        "phasenregel": "Überblenden zwischen Gehen und Ziehen nur, wenn beide Clips dieselbe Abspielstelle (Phase) haben: Der Code setzt den neuen Clip auf "
                       "dieselbe Stelle (seek) und blendet dann. Läuft ein Tier mit absichtlich versetzter Stelle (zweites Pferd, Versatz 0,5 Schleifen), "
                       "behält es seinen Versatz in beiden Clips (derselbe Versatz im neuen Clip), sonst würden die Hufe beim Blenden ineinander greifen. "
                       "Wechsel zwischen Stehen, Stehen-Varianten und Anfahren_/Anhalten_ sind ohne Blendzeit möglich (Abstand höchstens 2,2 mm).",
        "mehrere_tiere": "Zwei Pferde nie im Gleichtakt: das zweite startet Gehen/Ziehen mit seek(0,5 · Dauer) bzw. um diese Zeit später "
                         "(Fußfolge gegen das erste verschoben); Stehen-Varianten je Tier zufällig und zeitlich versetzt.",
        "lod": "Dreiecksbudget (Runde 10): Nahnetz Pferd rund 11 400, Ochse 6 170 Dreiecke; Fernnetz Pferd rund 1 210, Ochse 862. Nahnetz (Skelett) nur bei Zoom 25 und höchstens 4 Tieren gleichzeitig im Bild (Zweispänner 2 x 11 400 = 22 800 plus Karren); alle weiteren Tiere und ab Zoom 50 die gebackene Vertex-Animation (Nahnetz gebacken), ab Zoom 100 das Fernnetz (genügt für die Spielkamera). Das Fernnetz der Pferde liegt 10 mm höher (Decimate-Ausgleich).",
        "vat": "Gehen und Ziehen mit 24 Bildern je Sekunde backen (nicht 12): Mitte zwischen zwei Bildern höchstens rund 1 cm Abweichung, "
               "bei 12 rund 7 bis 9 cm (siehe docs/bereiche/darstellung.md).",
    }
    if zug is not None:
        v_ = ANKER_ZIEHEN_VERSATZ[art]
        klips_extra = {"anker_lokal_godot": [round(zug.x + v_[0], 3), round(zug.z + v_[1], 3), round(-zug.y + v_[2], 3)],
                       "anker_ziehen_godot": [round(zug.x + v_[0], 3), round(zug.z + v_[1], 3), round(-zug.y + v_[2], 3)]}
    else:
        klips_extra = {}
    alle.setdefault("tiere", {})[art] = {**klips_extra, "tempo": par["v"], "schleife": par["T"], "schrittlaenge": round(par["v"] * par["T"], 4),
                                          "weg": "Rumpfweg je Bild (m); Anfahren: Tempo = v * anlauf(t), siehe docs/bereiche/darstellung.md", "klips": klips}
    with open(pfad, "w", encoding="utf-8", newline="\n") as f:
        json.dump(alle, f, ensure_ascii=False, indent=1, sort_keys=True)
        f.write("\n")


REIHENFOLGE = ["Stehen", "Stehen_Ruhen", "Stehen_Umschauen", "Fressen", "Gehen", "Ziehen", "Anfahren_Gehen", "Anhalten_Gehen",
               "Anfahren_Ziehen", "Anhalten_Ziehen"]


def _kegel(bm, gw, fuss, mitte, r_oben, r_unten, hoehe, n=16, platz=3, unten=None):
    """Kegelstumpf (Huf bzw. Klaue) auf der Achse des Beins: oben (innen im Bein versteckt) etwas schmaler als das Bein, dann Hufkrone (Stoff Krone),
    dann Horn (Stoff `platz`), unten am breitesten; Mitte auf der Beinachse."""
    reihen = ((mitte.z + 0.02, r_oben * 0.95, 0), (mitte.z - hoehe * 0.20, r_oben * 1.03, KRONE), (mitte.z - hoehe * 0.55, r_oben + (r_unten - r_oben) * 0.65, platz),
              (mitte.z - hoehe, r_unten, platz))
    ringe = []
    for z, r, _ in reihen:
        ringe.append([bm.verts.new(Vector((mitte.x + math.cos(2 * math.pi * k / n) * r, mitte.y + math.sin(2 * math.pi * k / n) * r * 1.12, z))) for k in range(n)])
    # Gewichte: oben am Unterschenkel (bewegt sich mit dem Bein, keine Lücke), unten am Hufziel (Sohle bleibt flach, kein Rutschen), dazwischen gemischt
    for ring, (wf, wb) in zip(ringe, ((0.0, 1.0), (0.0, 1.0), (0.5, 0.5), (1.0, 0.0))):
        for v in ring:
            v[gw.dl][gw.idx[fuss]] = wf
            if unten is not None and wb > 0.0:
                v[gw.dl][gw.idx[unten]] = wb
    for idx in range(3):
        a, b = ringe[idx], ringe[idx + 1]
        for k in range(n):
            fl = bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
            fl.material_index = reihen[idx + 1][2]
            fl.smooth = True
    fl = bm.faces.new(ringe[-1][::-1])
    fl.material_index = platz


def klauen(bm, gw, ske, art):
    """Pferd: ein Huf, Ochse: Klauenpaar mit flachem Spalt, jeweils unmittelbar am Beinnetz: Die offene Kante des abgeschnittenen Beins (Ecken unter 10,5 cm,
    auf eine Höhe gelegt) ist der obere Ring des Hufs; darunter Hufkrone (weicher Übergang), Horn und Sohle sind nur seine vergrößerten Kopien.
    So gibt es keine Lücke, keine Manschette und keinen gezackten Rand."""
    zu_loeschen = [fl for fl in bm.faces if fl.calc_center_median().z < 0.105 and any(gw.summe(v, ["FrontFoot.L", "FrontFoot.R", "BackFoot.L", "BackFoot.R",
                                                                                              "FrontLowLeg.L", "FrontLowLeg.R", "BackLowLeg.L", "BackLowLeg.R"]) > 0.5 for v in fl.verts)]
    bmesh.ops.delete(bm, geom=zu_loeschen, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    kanten = [e for e in bm.edges if e.is_boundary and min(v.co.z for v in e.verts) < 0.3]
    neu_geom = bmesh.ops.subdivide_edges(bm, edges=kanten, cuts=(3 if art == "ochse" else 1), use_grid_fill=False)["geom_split"]
    for v in [g for g in neu_geom if isinstance(g, bmesh.types.BMVert)]:             # neue Mittelecken erben die Gewichte der Nachbarn (Mittel)
        nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
        if len(nb) == 2:
            for gi in set(nb[0][gw.dl].keys()) | set(nb[1][gw.dl].keys()):
                v[gw.dl][gi] = (nb[0][gw.dl].get(gi, 0.0) + nb[1][gw.dl].get(gi, 0.0)) * 0.5
    rand_ecken = [v for v in bm.verts if any(e.is_boundary for e in v.link_edges) and v.co.z < 0.3]
    for seite in ("L", "R"):
        for vorn in ("Front", "Back"):
            fuss = f"{vorn}Foot.{seite}"
            unten = f"{vorn}LowLeg.{seite}"
            pts = [v.co.copy() for v in bm.verts if gw.summe(v, [fuss, unten]) > 0.5 and 0.12 < v.co.z < 0.18]
            if len(pts) < 3:
                continue
            cx = sum(q.x for q in pts) / len(pts)
            cy = sum(q.y for q in pts) / len(pts)
            ring = [v for v in rand_ecken if math.hypot(v.co.x - cx, v.co.y - cy) < 0.22 and gw.summe(v, [fuss, unten]) > 0.3]
            if len(ring) < 6:
                print("KLAUE ohne Ring", fuss, len(ring))
                continue
            ring.sort(key=lambda v: math.atan2(v.co.y - cy, v.co.x - cx))
            zb = sum(v.co.z for v in ring) / len(ring)
            for v in ring:
                v.co.z = zb
            rr = max(math.hypot(v.co.x - cx, v.co.y - cy) for v in ring)
            if art == "pferd":
                zeilen = ((0.028, 1.05, KRONE, 0.0, 1.0), (0.070, 1.14, 3, 0.5, 0.5), (zb, 1.26, 3, 1.0, 0.0))
            else:
                zeilen = ((0.022, 1.03, KRONE, 0.0, 1.0), (0.058, 1.07, 3, 0.5, 0.5), (zb, 1.13, 3, 1.0, 0.0))
            ringe = [ring]
            gk = []
            for dz, f, mat, wf, wb in zeilen:
                neu = []
                gk_z = []
                for v in ring:
                    dx, dy = v.co.x - cx, v.co.y - cy
                    k = f
                    kx = ky = k
                    g = 0.0
                    if art == "ochse" and mat == 3:                              # tiefer Spalt zwischen den Klauen, vorn und hinten (bis 2,5 cm)
                        g = math.exp(-(dx / (0.30 * rr)) ** 2) * min(1.0, abs(dy) / (0.6 * rr)) * (1.0 if dz > 0.07 else 0.6)
                        ky = k * (1.0 - 0.55 * g)
                    nv = bm.verts.new(Vector((cx + dx * kx, cy + dy * ky, zb - dz)))
                    nv[gw.dl][gw.idx[fuss]] = wf
                    if wb > 0.0:
                        nv[gw.dl][gw.idx[unten]] = wb
                    neu.append(nv)
                    gk_z.append(g)
                ringe.append(neu)
                gk.append(gk_z)
            n = len(ring)
            for i in range(len(ringe) - 1):
                a_, b_ = ringe[i], ringe[i + 1]
                mat = zeilen[i][2]
                for k in range(n):
                    fl = bm.faces.new((a_[k], b_[k], b_[(k + 1) % n], a_[(k + 1) % n]))
                    fl.material_index = KRONE if (art == "ochse" and mat == 3 and gk[i][k] + gk[i][(k + 1) % n] > 0.5) else mat
                    fl.smooth = True
            fl = bm.faces.new(ringe[-1][::-1][::-1])
            fl.material_index = 3
            fl.normal_flip()


def hufe_fest(ob, hoehe=0.03):
    """Die Ecken des Hufs (unterhalb `hoehe` Meter) hängen nur am Hufziel: sie kippen mit dem Unterschenkel sonst mit und die Sohle rutscht."""
    namen = ("FrontFoot.L", "FrontFoot.R", "BackFoot.L", "BackFoot.R")
    ids = {g.name: g.index for g in ob.vertex_groups}
    gruppen = {ids[n]: ob.vertex_groups[n] for n in namen}
    for v in ob.data.vertices:
        if v.co.z >= hoehe:
            continue
        fuss = [g for g in v.groups if g.group in gruppen and g.weight > 0.25]
        if not fuss:
            continue
        gewaehlt = max(fuss, key=lambda g: g.weight).group
        alle = [g.group for g in v.groups]
        for gi in alle:
            if gi != gewaehlt:
                ob.vertex_groups[gi].remove([v.index])
        gruppen[gewaehlt].add([v.index], 1.0, "REPLACE")


def sohlen_messen(ob, ske):
    """Höhe der Sohle jedes Hufs (Meter) in der Ruhepose: die tiefste Ecke der Gruppe des Hufziels."""
    ids = {g.name: g.index for g in ob.vertex_groups}
    aus = {}
    for fn in ("FrontFoot.L", "FrontFoot.R", "BackFoot.L", "BackFoot.R"):
        zs = [v.co.z for v in ob.data.vertices if any(x.group == ids[fn] and x.weight > 0.5 for x in v.groups)]
        aus[fn] = min(zs) if zs else 0.0
    return aus


def ohren_anlegen(ske, ob, art):
    """Zwei Ohrknochen (Ohr_L, Ohr_R) am Kopf: die Ecken der Ohren (Pferd: oben am Genick, Ochse: seitlich) bekommen Gewicht von 0 am Ansatz bis 1 an der
    Spitze. Das Netz stellt die Ohren schon dar; die Knochen lassen sie spielen."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    gw = Gewichte(bm, ob)
    ha, hb = knochen(ske, "Head")
    kopf = [v for v in bm.verts if gw.summe(v, ["Head"]) > 0.5 and (v.co - ha).length < (0.26 if art == "pferd" else 0.32)]
    if art == "pferd":
        zm = max(v.co.z for v in kopf)
        kand = [v for v in kopf if v.co.z > zm - 0.10 and abs(v.co.x) > 0.02]
    else:
        xm = max(abs(v.co.x) for v in kopf)
        kand = [v for v in kopf if abs(v.co.x) > 0.75 * xm and abs(v.co.y - ha.y) < 0.12 and v.co.z > ha.z - 0.04]
    def bb(vs):
        if not vs:
            return None
        return [tuple(round(f(c), 3) for c in ((v.co.x, v.co.y, v.co.z) for v in vs)) for f in (min,)] if False else (
            [round(min(v.co[i] for v in vs), 3) for i in range(3)], [round(max(v.co[i] for v in vs), 3) for i in range(3)])
    print("Ohren: Kandidaten", len(kand), "von", len(kopf), "Kopfecken; Kopf", bb(kopf), "Ohrkandidaten", bb(kand), "Poll", tuple(round(c, 3) for c in ha))
    gruppen = {}
    for seite, name in ((1.0, "Ohr_L"), (-1.0, "Ohr_R")):
        vs = [v for v in kand if v.co.x * seite > 0.0]
        if len(vs) < 3:
            print("Ohren: keine Ecken für", name)
            continue
        gruppen[name] = [(v.index, v.co.copy()) for v in vs]
    bm.free()
    if not gruppen:
        return
    bpy.context.view_layer.objects.active = ske
    for o in bpy.context.scene.objects:
        o.select_set(o is ske)
    bpy.ops.object.mode_set(mode="EDIT")
    daten = {}
    for name, lst in gruppen.items():
        ps = [c for _, c in lst]
        if art == "pferd":
            sp = max(ps, key=lambda c: c.z)
            basis = min(ps, key=lambda c: c.z)
        else:
            sp = max(ps, key=lambda c: abs(c.x))
            basis = min(ps, key=lambda c: abs(c.x))
        mitte = sum(ps, Vector((0, 0, 0))) / len(ps)
        kopf_pt = Vector((mitte.x if abs(basis.x) < 0.5 * abs(mitte.x) else basis.x, basis.y, basis.z))
        eb = ske.data.edit_bones.new(name)
        eb.head = basis
        eb.tail = sp if (sp - basis).length > 0.03 else basis + Vector((0, 0, 0.05))
        eb.parent = ske.data.edit_bones["Head"]
        daten[name] = (basis.copy(), eb.tail.copy())
    bpy.ops.object.mode_set(mode="OBJECT")
    kopfgr = ob.vertex_groups["Head"]
    for name, lst in gruppen.items():
        basis, spitze = daten[name]
        achse = spitze - basis
        vg = ob.vertex_groups.new(name=name)
        for idx, co in lst:
            u = max(0.0, min(1.0, (co - basis).dot(achse) / max(1e-6, achse.length_squared)))
            w = u * u * (3 - 2 * u)
            vg.add([idx], w, "REPLACE")
            kopfgr.add([idx], 1.0 - w, "REPLACE")
    print("Ohren:", {n: (round(a.z, 3), round(b.z, 3)) for n, (a, b) in daten.items()})


def bodenkontakt(ske, ob):
    """Tiefster Punkt des Netzes (Meter) über die Bilder von Gehen, Ziehen und Stehen: 0 = Hufe stehen auf dem Boden, negativ = im Boden."""
    szene = bpy.context.scene
    ergebnis = []
    for name in ("Gehen", "Ziehen", "Stehen"):
        ske.animation_data.action = bpy.data.actions[name]
        a, e = bpy.data.actions[name].frame_range
        a_e = (a, e)
        tief = 1e9
        for k in range(12):
            szene.frame_set(int(round(a + (e - a) * k / 12)))
            dg = bpy.context.evaluated_depsgraph_get()
            me = ob.evaluated_get(dg).to_mesh()
            tief = min(tief, min(v.co.z for v in me.vertices))
            ob.evaluated_get(dg).to_mesh_clear()
        ergebnis.append(tief)
    ske.animation_data.action = None
    szene.frame_set(1)
    return ergebnis


def bauen(datei, art, variante, bilder_ausgeben=True):
    cfg = TIERE[art]
    s = cfg["s"]
    pal = f"{art}_{variante}"
    bs.BIBLIOTHEK_JE_FLAECHE = True
    ske, ob = laden(art)
    ob.name = f"Nah_{art}"
    ob.data.name = f"Nah_{art}"
    ske.animation_data_clear()
    for pb in ske.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
    for m in list(ob.modifiers):
        ob.modifiers.remove(m)
    ske.data.pose_position = "REST"

    # Netz umformen (Einheiten der Vorlage)
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    gw = Gewichte(bm, ob)
    bm.faces.ensure_lookup_table()
    if art == "ochse":
        euter_weg(bm, ob)
    klassen = formen(art, bm, gw, ske, ob)
    bm.faces.ensure_lookup_table()
    for fl in bm.faces:
        fl.material_index = klassen.get(fl.index, 0)
    bm.to_mesh(me)
    bm.free()
    me.materials.clear()
    for n in PLATZ:
        me.materials.append(bs.stoff(f"{pal} {n}" if n in FELL_STOFFE else n))
    # unterteilen, glatt
    mod = ob.modifiers.new("Unterteilen", "SUBSURF")
    mod.levels = 1
    mod.render_levels = 1
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.scene.objects:
        o.select_set(o is ob)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    glatt = ob.modifiers.new("Glaetten", "SMOOTH")                  # nimmt die Facetten der Vorlage (Kruppe, Bauch) weg
    glatt.factor = 0.5 if art == "pferd" else 0.35
    glatt.iterations = 6 if art == "pferd" else 2
    bpy.ops.object.modifier_apply(modifier=glatt.name)
    # Maßstab in Meter, Füße auf den Boden
    me.transform(Matrix.Scale(s, 4))
    zmin = min(v.co.z for v in me.vertices)
    me.transform(Matrix.Translation((0, 0, -zmin)))
    bpy.context.view_layer.objects.active = ske
    for o in bpy.context.scene.objects:
        o.select_set(o is ske)
    bpy.ops.object.mode_set(mode="EDIT")
    ursprung = {eb.name: (eb.head.copy(), eb.tail.copy()) for eb in ske.data.edit_bones}      # erst merken: verbundene Knochen teilen Enden
    for eb in ske.data.edit_bones:
        eb.head = ursprung[eb.name][0] * s + Vector((0, 0, -zmin))
        eb.tail = ursprung[eb.name][1] * s + Vector((0, 0, -zmin))
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    if art == "pferd":
        zk.ohren_knochen(ske, ob)
    else:
        ohren_anlegen(ske, ob, art)

    # Zubehör und Hörner (Meter)
    bm = bmesh.new()
    bm.from_mesh(me)
    gw = Gewichte(bm, ob)
    if art == "pferd":
        print(zk.kopf_pferd(bm, gw, ske, ob))
        zug = kummet(bm, gw, ske, 1.0)
        haar_obj = haare(bm, gw, ske, art)
        klauen(bm, gw, ske, art)
    else:
        print(zo.ochse_form(bm, gw, ske))
        hoerner(bm, gw, ske, 1.0)
        zug = joch(bm, gw, ske, 1.0)
        haar_obj = haare(bm, gw, ske, art)
        klauen(bm, gw, ske, art)
    bm.faces.ensure_lookup_table()
    bm.faces.index_update()
    fell_flaechen = {fl.index for fl in bm.faces if fl.material_index in FELL_PLAETZE}
    haar_faktor = {fl.index: f for fl, f in haar_obj.faktor}
    for fl in bm.faces:
        fl.smooth = True
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.update()
    # Farben zusammenlegen, Verlauf
    vorher, nachher = bs.farben_zusammenlegen([ob])
    verlauf(me, ob, PLATZ, fell_flaechen, haar_faktor, art, variante)
    abzeichen(me, ob, ske, art, variante, fell_flaechen)
    m = ob.modifiers.new("Skelett", "ARMATURE")
    m.object = ske
    ob.parent = ske
    ob.matrix_parent_inverse = Matrix.Identity(4)
    # Fernnetz
    fern = ob.copy()
    fern.data = ob.data.copy()
    fern.name = fern.data.name = f"Fern_{art}"
    bpy.context.scene.collection.objects.link(fern)
    me.calc_loop_triangles()
    dreiecke_nah = len(me.loop_triangles)
    bmf = bmesh.new()
    bmf.from_mesh(fern.data)
    bmesh.ops.delete(bmf, geom=[fl for fl in bmf.faces if fl.material_index == HAAR], context="FACES")
    bmesh.ops.delete(bmf, geom=[v for v in bmf.verts if not v.link_faces], context="VERTS")
    bmf.to_mesh(fern.data)
    bmf.free()
    d = fern.modifiers.new("Verkleinern", "DECIMATE")
    d.decimate_type = "COLLAPSE"
    d.ratio = 0.14 if art != "pferd" else 0.108                     # Pferd: nach dem neuen Kopf dieselbe Fernnetz-Größe wie zuvor (rund 1180 Dreiecke)
    d.use_collapse_triangulate = True
    fern.modifiers.move(len(fern.modifiers) - 1, 0)
    bpy.context.view_layer.objects.active = fern
    for o in bpy.context.scene.objects:
        o.select_set(o is fern)
    bpy.ops.object.modifier_apply(modifier=d.name)
    if art == "pferd":
        fern.data.transform(Matrix.Translation((0, 0, 0.010)))      # Decimate zieht die Hufspitzen bis 9 mm unter den Boden
    fern.data.validate()
    fern.data.calc_loop_triangles()
    dreiecke_fern = len(fern.data.loop_triangles)
    for o in (ob, fern):                                             # höchstens vier Knochen je Ecke (glTF)
        bpy.context.view_layer.objects.active = o
        for q in bpy.context.scene.objects:
            q.select_set(q is o)
        bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
        bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    # Anker
    # Der Anker liegt in ALLEN Bewegungen fest auf der mittleren Lage des Geschirrs im Ziehen (eigener, nie bewegter Knochen „Geschirr“ am Wurzelknochen):
    # Kummet bzw. Joch wippen mit dem Rumpf darunter (Zugstränge geben nach), der Anker springt beim Anhalten nicht und der Karren sieht keinen Versatz.
    v_ = ANKER_ZIEHEN_VERSATZ[art]
    pos = Vector((zug.x + v_[0], zug.y - v_[2], zug.z + v_[1]))
    wurzel = next(b.name for b in ske.data.bones if b.parent is None)
    bpy.context.view_layer.objects.active = ske
    for q in bpy.context.scene.objects:
        q.select_set(q is ske)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = ske.data.edit_bones.new("Geschirr")
    eb.head = pos
    eb.tail = pos + Vector((0, -0.05, 0))
    eb.parent = ske.data.edit_bones[wurzel]
    eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    anker = bs.anker("Anker_Geschirr", pos, 0.0)
    anker.parent = ske
    anker.parent_type = "BONE"
    anker.parent_bone = "Geschirr"
    kn = ske.data.bones[anker.parent_bone]
    anker.matrix_parent_inverse = (ske.matrix_world @ kn.matrix_local @ Matrix.Translation((0, kn.length, 0))).inverted()
    for o in (ob, fern):
        hufe_fest(o)
    sohle = sohlen_messen(ob, ske)

    # Bewegungen: eigene Planung (zugtiere_anim.py), danach IK wegbacken
    ske.data.pose_position = "POSE"
    rumpf = za.Rumpf(ske, art, sohle)
    clips, daten = {}, {}
    for name, mit_zug, modus in (("Gehen", False, "schleife"), ("Ziehen", True, "schleife"), ("Anfahren_Gehen", False, "anfahren"),
                             ("Anhalten_Gehen", False, "anhalten"), ("Anfahren_Ziehen", True, "anfahren"), ("Anhalten_Ziehen", True, "anhalten")):
        clips[name], daten[name] = za.gang_rahmen(rumpf, art, mit_zug, modus)
    for name in za.STEHEN_DAUER:
        clips[name] = za.stehen_rahmen(rumpf, art, name)
    rumpf.zuruecksetzen()
    for pb in ske.pose.bones:
        for c in list(pb.constraints):
            pb.constraints.remove(c)
    for clip in clips.values():                                     # Vorzeichen der Drehungen fortlaufend (kein Umklappen beim Mischen)
        for n in clip[0]:
            for k in range(1, len(clip)):
                if clip[k][n][1].dot(clip[k - 1][n][1]) < 0:
                    clip[k][n] = (clip[k][n][0], -clip[k][n][1])
    mit_loc = {pb.name for pb in ske.pose.bones if any(r[pb.name][0].length > 1e-5 for clip in clips.values() for r in clip)}
    for name in REIHENFOLGE:
        schreiben_clip(ske, name, clips[name], mit_loc)
    boden = bodenkontakt(ske, ob)
    tempo_ist = cfg["tempo"]
    n_geh = len(clips["Gehen"]) - 1
    v_quelle = 0.0
    geh = [0] * n_geh
    for o in (ob, fern):                                             # nur die Eckenfarbe „Farbe“ in die Datei (die Vorlage brachte eine eigene Farbschicht mit)
        ca = o.data.color_attributes
        print("Farbschichten vor dem Schreiben:", [c.name for c in ca], "aktiv", ca.active_color.name if ca.active_color else None)
        if "Col" in ca and "Farbe" in ca:
            ca.remove(ca["Col"])
        ca.active_color = ca["Farbe"]
        ca.render_color_index = ca.find("Farbe")
    pfad = fg.exportieren(datei, [ske, ob, fern, anker])
    print(f"{datei}: Nah {dreiecke_nah} Dreiecke, Fern {dreiecke_fern}, Stoffe {vorher}->{nachher}, Gehen {n_geh} Bilder "
          f"(Quelle {v_quelle:.2f} m/s bei {len(geh)} Bildern, jetzt {tempo_ist:.2f} m/s, Ziel {cfg['tempo']}), Zugpunkt {tuple(round(x, 3) for x in zug)}, "
          f"tiefster Punkt im Gehen {boden[0]:.3f} m, im Ziehen {boden[1]:.3f} m, im Stehen {boden[2]:.3f} m")
    print("geschrieben:", pfad)
    clips_schreiben(art, cfg, clips, daten, zug)
    return ske, ob, fern, anker, zug


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def _licht_und_boden():
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    bs.cycles_geraet(szene)
    szene.cycles.samples = 24
    szene.cycles.use_denoising = True
    szene.view_settings.view_transform = "Standard"
    sonne = bpy.data.objects.new("_Sonne", bpy.data.lights.new("_Sonne", "SUN"))
    sonne.data.energy = 3.2
    sonne.data.color = (1.0, 0.94, 0.84)
    sonne.data.angle = math.radians(2.5)
    sonne.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    szene.collection.objects.link(sonne)
    welt = bpy.data.worlds.new("Himmel")
    try:
        welt.use_nodes = True
    except AttributeError:
        pass
    hg = welt.node_tree.nodes["Background"]
    hg.inputs["Color"].default_value = (0.40, 0.45, 0.53, 1)
    hg.inputs["Strength"].default_value = 0.85
    szene.world = welt
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=30)
    me = bpy.data.meshes.new("_Boden")
    bm.to_mesh(me)
    bm.free()
    boden = bpy.data.objects.new("_Boden", me)
    boden.data.materials.append(bs._hilfsstoff("_Boden", (0.33, 0.33, 0.25)))
    szene.collection.objects.link(boden)
    kam = bpy.data.objects.new("_Kamera", bpy.data.cameras.new("_Kamera"))
    szene.collection.objects.link(kam)
    szene.camera = kam
    return kam


def _pose(ske, name, phase):
    ske.animation_data.action = bpy.data.actions[name]
    a, e = bpy.data.actions[name].frame_range
    bpy.context.scene.frame_set(int(round(a + phase * (e - a))))
    bpy.context.view_layer.update()


def _render(kam, ziel, az, hoehe, ortho, abstand, datei, breite=1000, hoch=620):
    szene = bpy.context.scene
    szene.render.resolution_x, szene.render.resolution_y = breite, hoch
    kam.data.type = "ORTHO" if ortho else "PERSP"
    if ortho:
        kam.data.ortho_scale = abstand
    else:
        kam.data.lens = 50
    kam.data.clip_end = 500
    az = math.radians(az)
    el = math.radians(hoehe)
    d = 60 if ortho else abstand
    kam.location = ziel + Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el))) * d
    richt = (ziel - kam.location).normalized()
    kam.rotation_euler = richt.to_track_quat("-Z", "Y").to_euler()
    os.makedirs(VORSCHAU, exist_ok=True)
    szene.render.filepath = os.path.join(VORSCHAU, datei + ".png")
    bpy.ops.render.render(write_still=True)


def vorschau(datei, art, ske, zug, schnell):
    szene = bpy.context.scene
    hoch_tier = 2.4 if art == "pferd" else 1.7
    kam = _licht_und_boden()
    if schnell:
        szene.cycles.samples = 8
    # Mann zum Größenvergleich
    mann = None
    if os.path.exists(PASSANT):
        vorher = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=PASSANT)
        neu = [o for o in bpy.data.objects if o not in vorher]
        sichtbar = None
        for o in neu:
            if o.type == "MESH":
                if o.name.startswith("Nah_") and sichtbar is None:
                    sichtbar = o
                else:
                    o.hide_render = True
        wurzel = [o for o in neu if o.parent is None]
        for o in wurzel:
            o.location = (0, -(1.55 if art == "pferd" else 1.5) - 1.6, 0)
        mann = neu
    for o in bpy.data.objects:
        if o.name.startswith(("Fern_",)) and o.type == "MESH":
            o.hide_render = True
    tier_mitte = Vector((0, 0, hoch_tier / 2 - 0.1))
    for name, phase, sicht, zielv, az, el, ortho, ab, brt, hh in (
        ("stand_seite", 0.0, "Stehen", Vector((0, -0.5, 1.0)), 0, 0, True, 6.0, 1000, 620),
        ("gehen_seite_a", 0.1, "Gehen", Vector((0, -0.5, 1.0)), 0, 0, True, 6.0, 1000, 620),
        ("gehen_seite_b", 0.6, "Gehen", Vector((0, -0.5, 1.0)), 0, 0, True, 6.0, 1000, 620),
        ("ziehen_seite", 0.1, "Ziehen", Vector((0, -0.5, 1.0)), 0, 0, True, 6.0, 1000, 620),
        ("fressen_seite", 0.3, "Fressen", Vector((0, -0.5, 0.9)), 0, 0, True, 6.0, 1000, 620),
        ("nah_schraeg", 0.1, "Stehen", Vector((0, -2.0, 1.0)), -115, 14, False, 8.0, 900, 700),
        ("weit_spielkamera", 0.1, "Gehen", Vector((0, -1.0, 0.8)), -45 - 90, 40, True, 11.0, 900, 700),
    ):
        _pose(ske, sicht, phase)
        _render(kam, zielv, az, el, ortho, ab, f"{datei}_{name}", brt, hh)
    for o in bpy.data.objects:                                         # Fernnetz statt Nahnetz
        if o.name.startswith(("Fern_", "Nah_")) and o.type == "MESH" and o.parent is ske:
            o.hide_render = o.name.startswith("Nah_")
    _pose(ske, "Stehen", 0.0)
    _render(kam, Vector((0, -0.5, 1.0)), 0, 0, True, 6.0, f"{datei}_fernnetz_seite", 1000, 620)
    if os.environ.get("ZUGTIERE_NAH"):                                   # Nahbilder mit dem Fell (Nachbau des Shaders, siehe zugtiere_pruefen.py)
        import zugtiere_pruefen as zp
        for o in bpy.data.objects:
            if o.type == "MESH" and o.name.startswith("Fern_"):
                o.hide_render = True
            if o.type == "MESH" and o.name == f"Nah_{art}":
                o.hide_render = False
        for o in (mann or []):
            if o.type == "MESH":
                o.hide_render = True
        zp.fell_material(bpy.data.objects[f"Nah_{art}"])
        zp.nahbilder_ohne_buehne(ske, datei, os.environ["ZUGTIERE_NAH"], kam)
    return mann


def main():
    nur = [a.split("=", 1)[1].split(",") for a in sys.argv if a.startswith("--nur=")]
    nur = nur[0] if nur else None
    for datei, art, variante in DATEIEN:
        if nur and datei.replace("tier_", "") not in nur:
            continue
        ske, ob, fern, anker, zug = bauen(datei, art, variante)
        if "--ohne-bilder" not in sys.argv:
            vorschau(datei, art, ske, zug, "--schnell" in sys.argv)


if __name__ == "__main__":
    main()
