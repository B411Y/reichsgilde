"""Tiere der Stadt und des Umlands: Krähe, Huhn (drei Farben), Hahn, Hund und Katze (je drei Farben) mit Skelett und Bewegungen.

    blender -b --python tools/modelle/tiere.py -- [--schnell] [--ohne-bilder] [--nur=hund,katze]

Schreibt game/assets/modelle/tiere/vogel_kraehe.glb, tier_huhn_braun.glb, tier_huhn_weiss.glb, tier_huhn_gesprenkelt.glb,
tier_hahn.glb, tier_hund(_schwarz,_hell).glb, tier_katze(_schwarz,_gefleckt).glb und die Vorschau docs/modelle/tiere.png. Die Krähe baut voegel.py (Rumpf, Flügel,
Skelett wie Taube und Spatz); hier steht nur ihr Aufruf. Alles andere ist hier gebaut, im selben Stil wie die Vögel:
kantige Körper aus Ringen, jede Fläche einer Farbe (Gefieder, Fell, Streifen), Ursprung zwischen den Füßen, Blender Z oben,
Blick nach -Y (im Spiel Y oben, Blick nach +Z), Meter-Maßstab, jedes Tier etwa 1,2- bis 1,5-fach vergrößert wie die Tauben.

    Huhn, Hahn   Skelett Koerper, Kopf (mit Hals), Fluegel_L/R, Schwanz, Bein_L/R
                 Stehen, Picken, Gehen, Kratzen (ein Bein scharrt), Flattern (kurzer Flügelschlag mit Hüpfer)
    Hund, Katze  Skelett Koerper, Kopf, Schwanz (Katze: Schwanz, Schwanz2), vier Beine je zweiteilig (Bein_VL_o/_u … Bein_HR_o/_u)
                 Hund: Stehen, Gehen, Traben, Sitzen, Liegen, Schnueffeln
                 Katze: Stehen, Gehen, Sitzen, Liegen, Putzen, Hocken (Lauerstellung), Sprung
    Krähe        wie die Tauben: Sitzen, Putzen, Picken, Gehen, Fliegen, Gleiten
    Alle Bewegungen sind Schleifen am Ort (Sprung: ein Durchgang); das Spiel bewegt das Tier.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import figuren as fg  # noqa: E402
import natur as nt  # noqa: E402
import voegel as vg  # noqa: E402
from bausteine import Vector  # noqa: E402

bs.FARBEN.update({
    "Huhn braun": (0.52, 0.31, 0.15), "Huhn braun dunkel": (0.38, 0.21, 0.10), "Huhn braun hell": (0.68, 0.47, 0.26),
    "Huhn weiss": (0.90, 0.88, 0.82), "Huhn weiss schatten": (0.78, 0.75, 0.68), "Huhn weiss hell": (0.95, 0.94, 0.90),
    "Huhn grau": (0.40, 0.33, 0.27), "Huhn grau hell": (0.56, 0.48, 0.39), "Huhn grau dunkel": (0.22, 0.18, 0.15),
    "Hahn rot": (0.62, 0.23, 0.09), "Hahn dunkel": (0.27, 0.11, 0.06), "Hahn gold": (0.88, 0.60, 0.14),
    "Hahn gruen": (0.04, 0.15, 0.12), "Hahn kastanie": (0.45, 0.17, 0.07), "Hahn schwarz": (0.06, 0.12, 0.09),
    "Kamm": (0.76, 0.10, 0.10), "Huhn Bein": (0.84, 0.69, 0.28), "Huhn Schnabel": (0.86, 0.66, 0.24),
    "Hund": (0.58, 0.40, 0.22), "Hund dunkel": (0.34, 0.22, 0.12), "Hund hell": (0.84, 0.74, 0.56),
    "Hund Nase": (0.10, 0.08, 0.07), "Hund Ohr": (0.42, 0.27, 0.15),
    "Hund schwarz": (0.14, 0.13, 0.12), "Hund schwarz dunkel": (0.07, 0.065, 0.06), "Hund schwarz hell": (0.31, 0.27, 0.22),
    "Hund schwarz Ohr": (0.10, 0.09, 0.085), "Hund schwarz Nase": (0.05, 0.05, 0.055),
    "Hund hell Fell": (0.88, 0.79, 0.60), "Hund hell dunkel": (0.72, 0.60, 0.42), "Hund hell hell": (0.96, 0.92, 0.82),
    "Hund hell Ohr": (0.74, 0.60, 0.41),
    "Katze": (0.62, 0.44, 0.26), "Katze dunkel": (0.40, 0.26, 0.14), "Katze hell": (0.90, 0.84, 0.74),
    "Katze Nase": (0.80, 0.50, 0.50), "Katze Auge": (0.60, 0.70, 0.20), "Pupille": (0.04, 0.04, 0.04),
    "Katze schwarz": (0.09, 0.09, 0.10), "Katze schwarz dunkel": (0.045, 0.045, 0.05), "Katze schwarz hell": (0.15, 0.15, 0.16),
    "Katze schwarz Nase": (0.30, 0.22, 0.23), "Katze schwarz Auge": (0.90, 0.76, 0.10),
    "Katze weiss": (0.92, 0.90, 0.85), "Katze weiss hell": (0.96, 0.95, 0.91), "Katze Fleck": (0.07, 0.07, 0.08),
})

FPS = 24

# Farbvarianten der Vierbeiner: Name des Grundstoffs -> Name des Stoffs der Variante (Knochen, Netz und Bewegungen bleiben gleich)
VARIANTEN = {
    ("hund", "schwarz"): {"Hund": "Hund schwarz", "Hund dunkel": "Hund schwarz dunkel", "Hund hell": "Hund schwarz hell",
                          "Hund Ohr": "Hund schwarz Ohr", "Hund Nase": "Hund schwarz Nase"},
    ("hund", "hell"): {"Hund": "Hund hell Fell", "Hund dunkel": "Hund hell dunkel", "Hund hell": "Hund hell hell",
                       "Hund Ohr": "Hund hell Ohr"},
    ("katze", "schwarz"): {"Katze": "Katze schwarz", "Katze dunkel": "Katze schwarz dunkel", "Katze hell": "Katze schwarz hell",
                           "Katze Nase": "Katze schwarz Nase", "Katze Auge": "Katze schwarz Auge"},
    ("katze", "gefleckt"): {"Katze": "Katze weiss", "Katze hell": "Katze weiss hell", "Katze dunkel": "Katze Fleck"},
}


class Abbild:
    """Stoffe mit Ersatznamen: st["Hund"] liefert bei einer Variante den Stoff der Variante."""

    def __init__(self, st, karte):
        self.st, self.karte = st, karte

    def __getitem__(self, name):
        return self.st[self.karte.get(name, name)]


def mit_variante(st, art, variante):
    return Abbild(st, VARIANTEN[(art, variante)]) if variante else st


def aus(grad):
    return tuple(math.radians(v) for v in grad)


# ─────────────────────────────────────────────────────────────────────────────
#  Bausteine
# ─────────────────────────────────────────────────────────────────────────────

def rohr(netz, st, stationen, n, stoff, kappen=(True, True)):
    """Körper entlang eines Wegs: stationen = [(Mitte, Halbachse quer (X), Halbachse hoch)]; je Ring n Ecken, jede Fläche
    nimmt den Stoff `stoff(i, k, s, c)` (Ring, Fläche, sin und cos der Mittellage: s > 0 = oben, c = Seite). Ring 0 = Ende der Kappen."""
    ringe, mitten, tang = [], [], []
    anzahl = len(stationen)
    for i, (c, ru, rv) in enumerate(stationen):
        c = Vector(c)
        a = Vector(stationen[min(i + 1, anzahl - 1)][0])
        b = Vector(stationen[max(i - 1, 0)][0])
        t = (a - b).normalized()
        u = Vector((1, 0, 0)) - t * t.x
        if u.length < 1e-4:
            u = Vector((0, 1, 0))
        u.normalize()
        v = t.cross(u)
        if abs(t.z) < 0.7:
            v = v if v.z >= 0 else -v
        else:
            v = v if v.y <= 0 else -v
        ringe.append([c + u * (ru * math.cos(2 * math.pi * k / n)) + v * (rv * math.sin(2 * math.pi * k / n)) for k in range(n)])
        mitten.append(c)
        tang.append(t)
    for i in range(anzahl - 1):
        m = (mitten[i] + mitten[i + 1]) / 2
        for k in range(n):
            j = (k + 1) % n
            quad = [ringe[i][k], ringe[i][j], ringe[i + 1][j], ringe[i + 1][k]]
            w = 2 * math.pi * (k + 0.5) / n
            netz.vieleck_nach(quad, sum(quad, Vector()) / 4 - m, st[stoff(i, k, math.sin(w), math.cos(w))])
    if kappen[0]:
        netz.vieleck_nach(ringe[0], -tang[0], st[stoff(0, 0, 0.0, 1.0)])
    if kappen[1]:
        netz.vieleck_nach(ringe[-1], tang[-1], st[stoff(anzahl - 2, 0, 0.0, 1.0)])


def unterteilen(stationen):
    """Fügt zwischen je zwei Stationen eine gemittelte ein (mehr Ringe = feinere Streifen)."""
    aus_ = []
    for a, b in zip(stationen, stationen[1:]):
        aus_.append(a)
        aus_.append(((Vector(a[0]) + Vector(b[0])) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2))
    aus_.append(stationen[-1])
    return aus_


def einfach(name):
    return lambda i, k, s, c: name


def platte(netz, st, punkte, normale, stoff, dicke=0.006):
    """Dünne Platte (Feder, Ohr, Kamm): Vorder- und Rückseite."""
    n = Vector(normale).normalized()
    netz.vieleck_nach(punkte, n, st[stoff])
    netz.vieleck_nach([p - n * dicke for p in punkte], -n, st[stoff])


def feder(netz, st, wurzel, richtung, laenge, breite, stoff, spitze=None, dicke=0.005):
    """Federraute: von der Wurzel in `richtung`, breit in X (soweit die Richtung es zulässt)."""
    d = Vector(richtung).normalized()
    q = Vector((1, 0, 0)) - d * d.x
    q = q.normalized() if q.length > 1e-4 else Vector((0, 1, 0))
    w = Vector(wurzel)
    p = [w, w + d * laenge * 0.4 + q * breite, w + d * laenge, w + d * laenge * 0.4 - q * breite]
    nrm = d.cross(q)
    platte(netz, st, p, nrm, stoff, dicke)
    if spitze:
        sp = [w + d * laenge * 0.8 + q * breite * 0.35, w + d * laenge, w + d * laenge * 0.8 - q * breite * 0.35]
        platte(netz, st, [s + nrm * dicke * 0.6 for s in sp], nrm, spitze, dicke * 0.6)


def knochen_anlegen(name, ske_edit, kopf, schwanz, eltern=None):
    b = ske_edit.edit_bones.new(name)
    b.head, b.tail = Vector(kopf), Vector(schwanz)
    b.roll = 0.0
    if eltern:
        b.parent = ske_edit.edit_bones[eltern]
    return b


def skelett(liste):
    """liste = [(Name, Kopf, Schwanz, Eltern)] – das Skelett im Bearbeitungsmodus anlegen."""
    import bpy
    arm = bpy.data.armatures.new("Skelett")
    ob = bpy.data.objects.new("Skelett", arm)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    for name, kopf, schwanz, eltern in liste:
        knochen_anlegen(name, arm, kopf, schwanz, eltern)
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in ob.pose.bones:
        pb.rotation_mode = "XYZ"
    return ob


def lerp(a, b, t):
    return a + (b - a) * t


def stueck(ph, punkte):
    """Linienzug durch [(Phase, Wert)] (Phase 0..1, aufsteigend), Werte dazwischen gerade."""
    for (p0, w0), (p1, w1) in zip(punkte, punkte[1:]):
        if p0 <= ph <= p1:
            return lerp(w0, w1, (ph - p0) / max(1e-9, p1 - p0))
    return punkte[-1][1]


def glatt(t):
    return t * t * (3 - 2 * t)


# ─────────────────────────────────────────────────────────────────────────────
#  Huhn und Hahn
# ─────────────────────────────────────────────────────────────────────────────

HUEHNER = {
    "huhn_braun": dict(sf=1.25, hahn=False, farbe="Huhn braun", farbe2="Huhn braun dunkel", bauch="Huhn braun hell",
                       hals="Huhn braun dunkel", kamm=0.7, muster="reihen"),
    "huhn_weiss": dict(sf=1.25, hahn=False, farbe="Huhn weiss", farbe2="Huhn weiss schatten", bauch="Huhn weiss hell",
                       hals="Huhn weiss", kamm=0.7, muster="reihen"),
    "huhn_gesprenkelt": dict(sf=1.25, hahn=False, farbe="Huhn grau", farbe2="Huhn weiss", bauch="Huhn grau hell",
                             hals="Huhn grau dunkel", kamm=0.7, muster="sprenkel"),
    "hahn": dict(sf=1.5, hahn=True, farbe="Hahn rot", farbe2="Hahn dunkel", bauch="Hahn dunkel", hals="Hahn gold",
                 kamm=1.5, muster="reihen"),
}
HUHN_BEWEGUNGEN = {"Stehen": 3.0, "Picken": 1.2, "Gehen": 0.9, "Kratzen": 1.2, "Flattern": 0.5}


def huhn_masse(art):
    d = dict(HUEHNER[art])
    d["z0"] = 0.27 * d["sf"]                         # Körpermitte über dem Boden
    return d


def huhn_skelett(art):
    d = huhn_masse(art)
    k, z0 = d["sf"], d["z0"]
    L = [("Koerper", (0, 0, z0), (0, 0, z0 + 0.05), None),
         ("Kopf", (0, -0.15 * k, z0 + 0.10 * k), (0, -0.15 * k, z0 + 0.20 * k), "Koerper"),
         ("Schwanz", (0, 0.22 * k, z0 + 0.06 * k), (0, 0.28 * k, z0 + 0.12 * k), "Koerper")]
    for s, seite in ((1, "L"), (-1, "R")):
        L.append((f"Fluegel_{seite}", (s * 0.13 * k, -0.07 * k, z0 + 0.085 * k), (s * 0.29 * k, -0.07 * k, z0 + 0.085 * k), "Koerper"))
        L.append((f"Bein_{seite}", (s * 0.065 * k, 0.0, z0 - 0.09 * k), (s * 0.065 * k, 0.0, z0 - 0.19 * k), "Koerper"))
    return skelett(L)


def huhn_bauen(art, st, ske):
    d = huhn_masse(art)
    k, z0, hahn = d["sf"], d["z0"], d["hahn"]
    t = fg.Teile(art)

    def V(x, y, z):
        return Vector((x * k, y * k, z0 + z * k))

    farbe, farbe2, bauch = d["farbe"], d["farbe2"], d["bauch"]
    if d["muster"] == "sprenkel":
        def leib(i, kk, s, c):
            if s < -0.35:
                return bauch
            return farbe2 if (i * 7 + kk * 11) % 5 == 0 else farbe
    elif hahn:
        def leib(i, kk, s, c):
            if i <= 2 and s < 0.5:
                return "Hahn dunkel"                         # dunkle Brust
            if i >= 8:
                return "Hahn schwarz"                        # Rücken hinten geht in den Schwanz über
            return bauch if s < -0.35 else (farbe2 if (i * 5 + kk * 3) % 7 == 0 else farbe)
    else:
        def leib(i, kk, s, c):
            return bauch if s < -0.35 else (farbe2 if (i * 5 + kk * 3) % 7 < 2 else farbe)

    with t.an("Koerper") as n:
        rohr(n, st, unterteilen([(V(0, -0.23, 0.06), 0.05 * k, 0.06 * k), (V(0, -0.15, 0.03), 0.105 * k, 0.12 * k),
                                 (V(0, -0.03, 0.0), 0.125 * k, 0.14 * k), (V(0, 0.10, 0.01), 0.11 * k, 0.125 * k),
                                 (V(0, 0.20, 0.05), 0.07 * k, 0.08 * k), (V(0, 0.26, 0.10), 0.03 * k, 0.03 * k)]), 8, leib)
    with t.an("Kopf") as n:
        hals = d["hals"]
        rohr(n, st, [(V(0, -0.15, 0.10), 0.075 * k, 0.07 * k), (V(0, -0.18, 0.18), 0.06 * k, 0.06 * k),
                     (V(0, -0.20, 0.25), 0.045 * k, 0.045 * k)], 7,
             lambda i, kk, s, c: hals if (i + kk) % 2 == 0 or hahn else d["farbe"], kappen=(False, False))
        km = V(0, -0.215, 0.30)
        nt.krone(n, st, km, 0.05 * k, 0.05 * k, saat=2, stoffe=(farbe if not hahn else "Hahn gold",) * 2, n=6, flach_unten=0.6)
        nt.ast(n, st, km + Vector((0, -0.045 * k, -0.005 * k)), km + Vector((0, -0.095 * k, -0.02 * k)), 0.026 * k, 0.006 * k,
               stoff="Huhn Schnabel", n=4)
        for s in (-1, 1):
            n.quader(km + Vector((s * 0.043 * k, -0.02 * k, 0.012 * k)), (0.012 * k, 0.026 * k, 0.026 * k), st["Taube Auge"])
            n.quader(km + Vector((s * 0.05 * k, -0.02 * k, 0.012 * k)), (0.01 * k, 0.014 * k, 0.014 * k), st["Auge"])
        # Kamm: gezackte Platte auf dem Kopf; Kehllappen unter dem Schnabel
        h = d["kamm"] * k
        zacken = [(-0.035, 0.035), (-0.032, 0.035 + 0.04 * d["kamm"]), (-0.016, 0.035 + 0.02 * d["kamm"]),
                  (0.0, 0.035 + 0.055 * d["kamm"]), (0.016, 0.035 + 0.022 * d["kamm"]),
                  (0.032, 0.035 + 0.045 * d["kamm"]), (0.04, 0.03)]
        platte(n, st, [km + Vector((0, y * k, z * k)) for y, z in zacken], (1, 0, 0), "Kamm", 0.012 * k)
        lappen = 0.03 + 0.03 * d["kamm"]
        platte(n, st, [km + Vector((0, -0.055 * k, -0.028 * k)), km + Vector((0, -0.02 * k, -0.035 * k)),
                       km + Vector((0, -0.04 * k, -0.035 * k - lappen * k))], (1, 0, 0), "Kamm", 0.012 * k)
    for s, seite in ((1, "L"), (-1, "R")):
        with t.an(f"Fluegel_{seite}") as n:
            lw = 0.7 * k
            x0, x1 = s * 0.13 * k, s * 0.29 * k
            y0, zf = -0.07 * k, z0 + 0.085 * k

            def f(a, b, hh=0.0, x0=x0, x1=x1, y0=y0, zf=zf, lw=lw):
                return Vector((x0 + (x1 - x0) * a, y0 + lw * b, zf + hh))
            ecken = [f(0, -0.16), f(0.55, -0.20), f(0.95, -0.02), f(0.9, 0.22), f(0.5, 0.36), f(0, 0.32)]
            n.vieleck_nach(ecken, (0, 0, 1), st[farbe])
            n.vieleck_nach([e - Vector((0, 0, 0.006)) for e in ecken], (0, 0, -1), st[farbe])      # hängt der Flügel, zeigt die Unterseite nach außen
            for kk in range(4):                                  # Schwungfedern als Reihe am Hinterrand
                b0 = 0.05 + 0.075 * kk
                spitze, tb = 1.02 - 0.05 * kk, 0.1 + 0.075 * kk
                fe = [f(0.5, b0 - 0.06), f(spitze, tb - 0.04), f(spitze - 0.04, tb + 0.05), f(0.5, b0 + 0.07)]
                n.vieleck_nach([e + Vector((0, 0, 0.002 + 0.001 * kk)) for e in fe], (0, 0, 1),
                               st["Hahn gruen" if hahn and kk % 2 == 0 else farbe2])
                n.vieleck_nach([e - Vector((0, 0, 0.008 + 0.001 * kk)) for e in fe], (0, 0, -1),
                               st["Hahn gruen" if hahn and kk % 2 == 0 else farbe2])
    with t.an("Schwanz") as n:
        w = V(0, 0.22, 0.07)
        if hahn:
            # drei lange, nach hinten fallende Sichelfedern (je eine Kette aus drei Federn) und ein Fächer kastanienbrauner Federn dahinter
            for x, wi in ((-0.1, 48), (-0.05, 56), (0.05, 56), (0.1, 48)):
                feder(n, st, w, Vector((x * 2, math.cos(math.radians(wi)), math.sin(math.radians(wi)))), 0.22 * k, 0.05 * k,
                      "Hahn schwarz")
            for x, hoehe in ((-0.045, 0.0), (0.0, 0.03), (0.045, 0.0)):
                p = w + Vector((x * k, 0.02 * k, hoehe * k))
                for seg, (wi, laenge, br) in enumerate(((64, 0.17, 0.034), (26, 0.17, 0.036), (-14, 0.15, 0.03))):
                    wink = math.radians(wi)
                    ri = Vector((x * 0.8, math.cos(wink), math.sin(wink)))
                    feder(n, st, p, ri, laenge * k, br * k, "Hahn gruen", dicke=0.006 * k)
                    p = p + ri.normalized() * laenge * k * 0.95
        else:
            for x, wi in ((-0.5, 38), (-0.25, 48), (0.0, 55), (0.25, 48), (0.5, 38)):
                feder(n, st, w, Vector((x, math.cos(math.radians(wi)), math.sin(math.radians(wi)))), 0.17 * k, 0.045 * k,
                      farbe2 if abs(x) < 0.3 else farbe, spitze=None)
    for s, seite in ((1, "L"), (-1, "R")):
        with t.an(f"Bein_{seite}") as n:
            x = s * 0.065
            rohr(n, st, [(V(x, 0.0, -0.05), 0.05 * k, 0.05 * k), (V(x, 0.0, -0.13), 0.035 * k, 0.035 * k)], 6,
                 lambda i, kk, ss, c: farbe if not hahn else "Hahn dunkel", kappen=(False, False))
            n.stab(V(x, 0.0, -0.12), V(x, 0.0, 0.0) * 1.0 - Vector((0, 0, z0)) + Vector((0, 0, 0.012)), 0.018 * k, 0.018 * k,
                   st["Huhn Bein"])
            for ang in (-25, 0, 25):                              # drei Zehen nach vorn, eine nach hinten
                a = math.radians(ang)
                n.stab(Vector((x * k, 0.0, 0.008)), Vector((x * k + math.sin(a) * 0.075 * k, -math.cos(a) * 0.075 * k, 0.008)),
                       0.014 * k, 0.012 * k, st["Huhn Bein"])
            n.stab(Vector((x * k, 0.0, 0.008)), Vector((x * k, 0.04 * k, 0.008)), 0.014 * k, 0.012 * k, st["Huhn Bein"])
            if hahn:                                              # Sporn
                n.stab(Vector((x * k + s * 0.012 * k, 0.012 * k, 0.07 * k)), Vector((x * k + s * 0.012 * k, 0.045 * k, 0.085 * k)),
                       0.012 * k, 0.012 * k, st["Huhn Schnabel"])
    return t.objekt(ske)


def huhn_haltung(art, name, ph):
    """{Knochen: (x, y, z) Grad}, Verschiebung des Körpers in Metern (Welt x, y, z), Spannweite der Flügel.
    Knochen zeigen hoch (Körper, Kopf, Schwanz) bzw. nach unten (Beine): positives X kippt Oberes nach vorn, Unteres nach hinten."""
    k = HUEHNER[art]["sf"]
    s = math.sin(2 * math.pi * ph)
    c = math.cos(2 * math.pi * ph)
    r = {}
    loc = [0.0, 0.0, 0.0]
    spanne = 1.0
    r["Fluegel_L"] = (-88, 0, 0)                                   # angelegt: die Platte hängt senkrecht an der Flanke
    r["Fluegel_R"] = (-88, 0, 0)
    kipp = 0.0
    if name == "Stehen":
        dreh = 40 * math.sin(2 * math.pi * ph) * (abs(math.sin(4 * math.pi * ph)) > 0.4)
        r["Kopf"] = (3 * math.sin(6 * math.pi * ph), dreh, 0)
        r["Schwanz"] = (3 * s, 0, 0)
    elif name == "Picken":
        nick = max(0.0, math.sin(4 * math.pi * ph)) ** 2
        kipp = 24 * nick + 2
        r["Kopf"] = (40 * nick, 0, 0)
        r["Schwanz"] = (-10 * nick, 0, 0)
        loc[2] = -0.03 * k * nick
        loc[1] = -0.03 * k * nick
    elif name == "Gehen":
        a = 32
        hl = max(0.0, -s)
        hr = max(0.0, s)
        r["Bein_L"] = (a * s, 0, 0)
        r["Bein_R"] = (-a * s, 0, 0)
        r["Kopf"] = (9 * math.sin(4 * math.pi * ph + 0.6), 0, 0)
        r["Schwanz"] = (2 * s, 0, 0)
        loc[2] = 0.012 * k * abs(c)
        kipp = 3
        r["_hub"] = (hl, hr)
    elif name == "Kratzen":
        q = (ph * 2) % 1.0
        x = stueck(q, [(0, 8), (0.45, -42), (0.7, 32), (1.0, 8)])
        heb = stueck(q, [(0, 0.0), (0.15, 1.0), (0.45, 1.0), (0.55, 0.0), (1.0, 0.0)])
        r["Bein_L"] = (x, 0, 0)
        r["_hub"] = (heb, 0.0)
        kipp = 14 + 4 * math.sin(4 * math.pi * ph)
        r["Kopf"] = (22 + 8 * math.sin(4 * math.pi * ph), 0, 0)
        r["Schwanz"] = (-8, 0, 0)
        loc[1] = -0.02 * k * math.sin(4 * math.pi * ph)
    elif name == "Flattern":
        w = vg.schlagwinkel(ph)
        hub = 5 + 52 * math.cos(w)
        zurueck = 14 - 16 * math.sin(w)
        r["Fluegel_L"] = (hub, 0, zurueck)
        r["Fluegel_R"] = (hub, 0, -zurueck)
        kipp = -14
        r["Kopf"] = (-8, 0, 0)
        r["Bein_L"] = (26, 0, 0)
        r["Bein_R"] = (22, 0, 0)
        r["Schwanz"] = (10 * math.cos(w), 0, 0)
        loc[2] = 0.14 * k + 0.03 * k * math.sin(w)
        spanne = 1 - 0.25 * max(0.0, -math.sin(w))
    r["Koerper"] = (kipp, 0, 0)
    for b in ("Bein_L", "Bein_R"):                                # die Füße bleiben beim Kippen unten
        x, y, z = r.get(b, (0, 0, 0))
        r[b] = (x - kipp * (0.0 if name == "Flattern" else 1.0), y, z)
    return r, tuple(loc), spanne


def huhn_pose(ob, art, name, ph):
    r, loc, spanne = huhn_haltung(art, name, ph)
    hub = r.pop("_hub", (0.0, 0.0))
    for pb in ob.pose.bones:
        pb.rotation_euler = aus(r.get(pb.name, (0, 0, 0)))
        pb.location = (0, 0, 0)
        pb.scale = (1, spanne, 1) if pb.name.startswith("Fluegel") else (1, 1, 1)
    for b, h in (("Bein_L", hub[0]), ("Bein_R", hub[1])):
        ob.pose.bones[b].scale = (1, 1 - 0.18 * h, 1)
    ob.pose.bones["Koerper"].location = (loc[0], loc[2], -loc[1])   # Knochen zeigt hoch: lokal (x, y, z) = Welt (x, z, -y)


# ─────────────────────────────────────────────────────────────────────────────
#  Vierbeiner: Hund und Katze
# ─────────────────────────────────────────────────────────────────────────────

BEINE = ("VL", "VR", "HL", "HR")


TIERE4 = {
    "hund": dict(sf=1.1, hb=0.38, vorn=-0.20, hinten=0.24, breite=0.075, oben=0.17, unten=0.19, zwei_schwanz=True),
    "katze": dict(sf=1.4, hb=0.20, vorn=-0.12, hinten=0.17, breite=0.045, oben=0.09, unten=0.10, zwei_schwanz=True),
}
HUND_BEWEGUNGEN = {"Stehen": 3.0, "Gehen": 1.1, "Traben": 0.7, "Sitzen": 3.0, "Liegen": 4.0, "Schnueffeln": 1.6}
KATZE_BEWEGUNGEN = {"Stehen": 3.0, "Gehen": 1.0, "Sitzen": 3.0, "Liegen": 4.0, "Putzen": 2.4, "Hocken": 2.4, "Sprung": 1.0}


def vierbeiner_skelett(art):
    d = TIERE4[art]
    k, hb = d["sf"], d["hb"] * d["sf"]
    ks = {"hund": (0.28, 0.50, 0.31, 0.45), "katze": (0.17, 0.27, 0.19, 0.22)}[art]
    L = [("Koerper", (0, 0, hb), (0, 0, hb + 0.05), None),
         ("Kopf", (0, -ks[0] * k, ks[1] * k), (0, -ks[0] * k, ks[1] * k + 0.08), "Koerper"),
         ("Schwanz", (0, ks[2] * k, ks[3] * k), (0, ks[2] * k + 0.1, ks[3] * k + 0.1), "Koerper"),
         ("Schwanz2", (0, ks[2] * k + 0.14 * k, ks[3] * k + 0.12 * k), (0, ks[2] * k + 0.2 * k, ks[3] * k + 0.22 * k), "Schwanz")]
    for b in BEINE:
        s = 1 if b[1] == "L" else -1
        y = d["vorn"] if b[0] == "V" else d["hinten"]
        hoeh = (0.34 if art == "hund" else 0.19) * k                 # Schulter bzw. Hüfte (Drehpunkt oben)
        L.append((f"Bein_{b}_o", (s * d["breite"] * k, y * k, hoeh), (s * d["breite"] * k, y * k, hoeh - d["oben"] * k), "Koerper"))
        L.append((f"Bein_{b}_u", (s * d["breite"] * k, y * k, hoeh - d["oben"] * k),
                  (s * d["breite"] * k, y * k, hoeh - (d["oben"] + d["unten"]) * k), f"Bein_{b}_o"))
    return skelett(L)


def hund_bauen(st, ske, variante=None):
    st = mit_variante(st, "hund", variante)
    k = TIERE4["hund"]["sf"]
    t = fg.Teile("Hund")

    def V(x, y, z):
        return Vector((x * k, y * k, z * k))

    def leib(i, kk, s, c):
        if s < -0.3:
            return "Hund hell"
        if 2 <= i <= 6 and s > 0.4:
            return "Hund dunkel"                                  # Sattel auf dem Rücken
        return "Hund"
    with t.an("Koerper") as n:
        rohr(n, st, unterteilen([(V(0, -0.30, 0.44), 0.075 * k, 0.10 * k), (V(0, -0.22, 0.435), 0.105 * k, 0.14 * k),
                                 (V(0, -0.06, 0.425), 0.1 * k, 0.125 * k), (V(0, 0.10, 0.425), 0.09 * k, 0.115 * k),
                                 (V(0, 0.24, 0.43), 0.1 * k, 0.125 * k), (V(0, 0.33, 0.43), 0.07 * k, 0.09 * k)]), 8, leib)
    with t.an("Kopf") as n:
        rohr(n, st, [(V(0, -0.27, 0.48), 0.09 * k, 0.10 * k), (V(0, -0.36, 0.57), 0.07 * k, 0.08 * k)], 7,
             lambda i, kk, s, c: "Hund hell" if s < -0.65 else "Hund", kappen=(False, False))
        rohr(n, st, [(V(0, -0.38, 0.60), 0.078 * k, 0.078 * k), (V(0, -0.46, 0.60), 0.072 * k, 0.072 * k)], 7,
             lambda i, kk, s, c: "Hund", kappen=(True, False))
        rohr(n, st, [(V(0, -0.46, 0.595), 0.066 * k, 0.062 * k), (V(0, -0.60, 0.565), 0.044 * k, 0.042 * k)], 6,
             lambda i, kk, s, c: "Hund hell" if s < 0.1 else "Hund", kappen=(False, True))
        n.quader(V(0, -0.615, 0.568), (0.052 * k, 0.03 * k, 0.036 * k), st["Hund Nase"])
        for s in (-1, 1):
            n.quader(V(s * 0.062, -0.485, 0.625), (0.016 * k, 0.034 * k, 0.03 * k), st["Auge"])
            # Schlappohr: ein flacher, nach unten schmaler werdender Lappen, der außen am Kopf hängt und unten leicht abstehen
            rohr(n, st, [(V(s * 0.072, -0.405, 0.655), 0.010 * k, 0.040 * k), (V(s * 0.092, -0.400, 0.600), 0.013 * k, 0.050 * k),
                         (V(s * 0.112, -0.395, 0.540), 0.012 * k, 0.048 * k), (V(s * 0.126, -0.390, 0.490), 0.009 * k, 0.034 * k),
                         (V(s * 0.132, -0.388, 0.455), 0.004 * k, 0.014 * k)], 6,
                 lambda i, kk, ss, c: "Hund Ohr", kappen=(True, True))
    with t.an("Schwanz") as n:
        rohr(n, st, [(V(0, 0.32, 0.45), 0.048 * k, 0.048 * k), (V(0, 0.41, 0.49), 0.044 * k, 0.044 * k)], 5,
             lambda i, kk, s, c: "Hund", kappen=(False, False))
    with t.an("Schwanz2") as n:
        rohr(n, st, [(V(0, 0.41, 0.49), 0.044 * k, 0.044 * k), (V(0, 0.52, 0.55), 0.036 * k, 0.036 * k),
                     (V(0, 0.62, 0.62), 0.016 * k, 0.016 * k)], 5,
             lambda i, kk, s, c: "Hund" if i == 0 else "Hund hell", kappen=(False, True))
    beine_bauen(t, st, "hund", V, k, "Hund", "Hund hell", "Hund dunkel")
    return t.objekt(ske)


def beine_bauen(t, st, art, V, k, farbe, pfote, strumpf):
    d = TIERE4[art]
    hoeh = 0.34 if art == "hund" else 0.19
    dick = 1.0 if art == "hund" else 0.55
    for b in BEINE:
        s = 1 if b[1] == "L" else -1
        y = d["vorn"] if b[0] == "V" else d["hinten"]
        x = s * d["breite"]
        hinten = b[0] == "H"
        with t.an(f"Bein_{b}_o") as n:
            r0 = (0.07 if hinten else 0.055) * k * dick
            rohr(n, st, [(V(x, y, hoeh + (0.0 if not hinten else 0.03)), r0, r0 * 1.3), (V(x, y, hoeh - d["oben"]), 0.036 * k * dick, 0.04 * k * dick)],
                 6, lambda i, kk, ss, c: farbe, kappen=(False, False))
        with t.an(f"Bein_{b}_u") as n:
            z0 = hoeh - d["oben"]
            if hinten and art == "hund":
                # Sprunggelenk: das Schienbein läuft nach hinten, die Mittelfußpartie wieder leicht nach vorn zur Pfote
                zs = z0 - d["unten"] * 0.52
                rohr(n, st, [(V(x, y, z0), 0.034 * k * dick, 0.036 * k * dick), (V(x, y + 0.045, zs), 0.032 * k * dick, 0.034 * k * dick),
                             (V(x, y + 0.012, 0.03), 0.026 * k * dick, 0.03 * k * dick)], 6, lambda i, kk, ss, c: farbe, kappen=(False, False))
                n.quader(V(x, y + 0.045, zs), (0.05 * k * dick, 0.05 * k * dick, 0.05 * k * dick), st[farbe])        # Knick des Sprunggelenks
                n.quader(V(x, y - 0.008, 0.016), (0.055 * k * dick, 0.085 * k * dick, 0.032 * k), st[pfote])
            else:
                rohr(n, st, [(V(x, y, z0), 0.034 * k * dick, 0.036 * k * dick), (V(x, y, 0.03), 0.027 * k * dick, 0.03 * k * dick)], 6,
                     lambda i, kk, ss, c: pfote if False else farbe, kappen=(False, False))
                n.quader(V(x, y - 0.02, 0.016), (0.055 * k * dick, 0.085 * k * dick, 0.032 * k), st[pfote])


def katze_bauen(st, ske, variante=None):
    st = mit_variante(st, "katze", variante)
    gefleckt = variante == "gefleckt"
    k = TIERE4["katze"]["sf"]
    t = fg.Teile("Katze")

    def V(x, y, z):
        return Vector((x * k, y * k, z * k))

    def leib(i, kk, s, c):
        if s < -0.3:
            return "Katze hell"
        if i <= 1:
            return "Katze hell"                                   # weiße Brust
        if gefleckt:                                              # weiß mit schwarzen Flecken (Blöcke aus Ringen und Flächen)
            return "Katze dunkel" if s > -0.35 and ((i // 2) * 3 + kk * 5) % 7 < 2 else "Katze"
        if s > -0.1 and i % 3 == 0 and i < 17:
            return "Katze dunkel"                                 # Tigerstreifen quer über den Rücken
        return "Katze"
    with t.an("Koerper") as n:
        rohr(n, st, unterteilen(unterteilen([(V(0, -0.17, 0.215), 0.045 * k, 0.065 * k), (V(0, -0.11, 0.215), 0.07 * k, 0.09 * k),
                                             (V(0, -0.02, 0.21), 0.065 * k, 0.085 * k), (V(0, 0.07, 0.21), 0.06 * k, 0.08 * k),
                                             (V(0, 0.15, 0.215), 0.068 * k, 0.088 * k), (V(0, 0.22, 0.225), 0.04 * k, 0.05 * k)])), 8, leib)
    with t.an("Kopf") as n:
        rohr(n, st, [(V(0, -0.15, 0.255), 0.05 * k, 0.055 * k), (V(0, -0.20, 0.29), 0.045 * k, 0.05 * k)], 7,
             lambda i, kk, s, c: "Katze hell" if s < -0.2 else ("Katze dunkel" if gefleckt and c > 0.2 and s > 0.1 else "Katze"),
             kappen=(False, False))
        rohr(n, st, [(V(0, -0.20, 0.30), 0.058 * k, 0.052 * k), (V(0, -0.235, 0.30), 0.062 * k, 0.054 * k),
                     (V(0, -0.275, 0.292), 0.04 * k, 0.04 * k)], 7,
             lambda i, kk, s, c: ("Katze dunkel" if gefleckt and c > 0.3 and s > 0.0 and i == 0 else
                                  "Katze hell" if s < -0.3 or i == 1 and abs(c) < 0.5 and s < 0.3 else "Katze"), kappen=(False, True))
        n.quader(V(0, -0.285, 0.295), (0.022 * k, 0.012 * k, 0.016 * k), st["Katze Nase"])
        for s in (-1, 1):
            n.quader(V(s * 0.04, -0.26, 0.315), (0.012 * k, 0.026 * k, 0.026 * k), st["Katze Auge"])
            n.quader(V(s * 0.046, -0.26, 0.315), (0.008 * k, 0.012 * k, 0.026 * k), st["Pupille"])
            platte(n, st, [V(s * 0.025, -0.20, 0.33), V(s * 0.065, -0.20, 0.335), V(s * 0.055, -0.20, 0.395)], (0, 1, 0),
                   "Katze dunkel" if gefleckt and s > 0 else "Katze", 0.012 * k)
            platte(n, st, [V(s * 0.033, -0.207, 0.338), V(s * 0.055, -0.207, 0.338), V(s * 0.05, -0.207, 0.375)], (0, 1, 0), "Katze Nase", 0.004 * k)
    with t.an("Schwanz") as n:
        rohr(n, st, [(V(0, 0.21, 0.225), 0.03 * k, 0.03 * k), (V(0, 0.29, 0.235), 0.028 * k, 0.028 * k),
                     (V(0, 0.36, 0.26), 0.026 * k, 0.026 * k)], 5,
             lambda i, kk, s, c: "Katze" if gefleckt else ("Katze dunkel" if i % 2 == 0 else "Katze"), kappen=(False, False))
    with t.an("Schwanz2") as n:
        rohr(n, st, [(V(0, 0.36, 0.26), 0.026 * k, 0.026 * k), (V(0, 0.42, 0.30), 0.024 * k, 0.024 * k),
                     (V(0, 0.45, 0.36), 0.02 * k, 0.02 * k)], 5,
             lambda i, kk, s, c: "Katze dunkel" if gefleckt or i % 2 == 1 else "Katze", kappen=(False, True))
    beine_bauen(t, st, "katze", V, k, "Katze", "Katze hell", "Katze dunkel")
    return t.objekt(ske)


def schritt(ph, a, f):
    """(Schwung oben, Beugung unten) eines Beins nach Phase: Standphase Fuß nach hinten, Schwungphase Fuß nach vorn mit Knick."""
    return -a * math.cos(2 * math.pi * ph), f * max(0.0, -math.sin(2 * math.pi * ph)) ** 0.8


def vierbeiner_haltung(art, name, ph):
    """{Knochen: (x, y, z) Grad}, Verschiebung des Körpers (Welt)."""
    k = TIERE4[art]["sf"]
    hund = art == "hund"
    s = math.sin(2 * math.pi * ph)
    c = math.cos(2 * math.pi * ph)
    r = {b: (0.0, 0.0, 0.0) for b in ("Koerper", "Kopf", "Schwanz", "Schwanz2")}
    for b in BEINE:
        r[f"Bein_{b}_o"] = (0.0, 0.0, 0.0)
        r[f"Bein_{b}_u"] = (0.0, 0.0, 0.0)
    loc = [0.0, 0.0, 0.0]

    def bein(b, o, u):
        r[f"Bein_{b}_o"] = (o, 0, 0)
        r[f"Bein_{b}_u"] = (u, 0, 0)

    def gehen(a, f, phasen, bob, nick):
        for b, p0 in zip(BEINE, phasen):
            o, u = schritt((ph + p0) % 1.0, a, f)
            bein(b, o, u * (1.0 if b[0] == "V" else -0.6) if False else u)
        loc[2] = bob * k * abs(s) if bob else 0
        r["Kopf"] = (nick * s, 0, 0)
        r["Schwanz"] = (0, 12 * math.sin(4 * math.pi * ph), 0)
        r["Schwanz2"] = (0, 14 * math.sin(4 * math.pi * ph - 0.8), 0)

    if name == "Stehen":
        atem = math.sin(2 * math.pi * ph)
        r["Kopf"] = (2 * atem, 25 * math.sin(2 * math.pi * ph) * (abs(math.sin(4 * math.pi * ph)) > 0.4), 0)
        r["Schwanz"] = (-8 if not hund else 0, 8 * math.sin(4 * math.pi * ph), 0)
        r["Schwanz2"] = (0, 10 * math.sin(4 * math.pi * ph - 0.7), 0)
        loc[2] = 0.003 * k * atem
    elif name == "Gehen":
        gehen(22 if hund else 20, 36 if hund else 40, (0.0, 0.5, 0.25, 0.75), 0.008, 4)
        if not hund:
            r["Schwanz"] = (-12, 8 * math.sin(2 * math.pi * ph), 0)
    elif name == "Traben":
        gehen(30, 48, (0.0, 0.5, 0.5, 0.0), 0.02, 3)
        r["Koerper"] = (2 * s, 0, 0)
    elif name == "Schnueffeln":
        gehen(14, 28, (0.0, 0.5, 0.25, 0.75), 0.004, 0)
        schnupper = 6 * math.sin(10 * math.pi * ph)
        r["Kopf"] = (50 + schnupper, 18 * math.sin(2 * math.pi * ph), 0)
        r["Koerper"] = (6, 0, 0)
        for b in BEINE:
            x, y, z = r[f"Bein_{b}_o"]
            r[f"Bein_{b}_o"] = (x - 6, y, z)
    elif name in ("Sitzen", "Liegen", "Hocken", "Putzen"):
        if name == "Sitzen" or name == "Putzen":
            pitch = -52 if hund else -48
            r["Koerper"] = (pitch, 0, 0)
            loc[1] = (0.17 if hund else 0.09) * k
            loc[2] = (-0.115 if hund else -0.065) * k
            for b in ("VL", "VR"):                                # Vorderbeine bleiben senkrecht
                bein(b, -pitch, 0)
            for b in ("HL", "HR"):                                # Hinterbeine: Oberschenkel nach vorn, Unterschenkel flach
                bein(b, -pitch - 78 if hund else -pitch - 80, 112)
            r["Kopf"] = (-pitch - 4, 0, 0)
            r["Schwanz"] = (50, 0, 0)
            r["Schwanz2"] = (0, 0, 0)
            r["Schwanz"] = (60, 10 * math.sin(2 * math.pi * ph), 0)
            r["Schwanz2"] = (20, 0, 0)
            if name == "Sitzen":
                r["Kopf"] = (-pitch - 4 + 3 * math.sin(4 * math.pi * ph),
                             30 * math.sin(2 * math.pi * ph) * (abs(math.sin(4 * math.pi * ph)) > 0.5), 0)
            else:                                                 # Putzen: linke Vorderpfote hoch, Kopf hin, leckt
                lick = max(0.0, math.sin(2 * math.pi * ph * 2))
                bein("VL", -pitch - 68 - 6 * lick, 118)
                r["Kopf"] = (-pitch + 26 + 12 * lick, -28, 0)
        elif name == "Liegen":
            loc[2] = -(0.2 if hund else 0.10) * k
            for b in ("VL", "VR"):
                bein(b, -85, 100)
            for b in ("HL", "HR"):
                bein(b, -95, 120)
            r["Kopf"] = (14 + 2 * math.sin(2 * math.pi * ph), 0, 0)
            r["Schwanz"] = (35, 0, 0)
            r["Schwanz2"] = (10, 0, 0)
            r["Koerper"] = (0.5 * math.sin(2 * math.pi * ph), 0, 0)
        else:                                                     # Hocken: Katze lauert, geduckt, Schwanzspitze zuckt
            loc[2] = -0.075 * k
            for b in ("VL", "VR"):
                bein(b, -62, 105)
            for b in ("HL", "HR"):
                bein(b, -58, 98)
            r["Koerper"] = (-4, 0, 0)
            r["Kopf"] = (8, 8 * math.sin(2 * math.pi * ph), 0)
            r["Schwanz"] = (-10, 0, 0)
            r["Schwanz2"] = (0, 24 * math.sin(6 * math.pi * ph), 0)
    elif name == "Sprung":
        # 0–0.3 ducken, 0.3–0.5 abstoßen und strecken, 0.5–0.75 in der Luft, 0.75–1 landen
        duck = stueck(ph, [(0, 0), (0.28, 1), (0.4, 0), (0.75, 0), (0.9, 0.7), (1.0, 0)])
        luft = stueck(ph, [(0, 0), (0.38, 0), (0.55, 1), (0.72, 1), (0.84, 0), (1.0, 0)])
        loc[2] = -0.075 * k * duck + 0.16 * k * luft
        loc[1] = -0.05 * k * luft
        r["Koerper"] = (-4 * duck - 22 * luft, 0, 0)
        for b in ("VL", "VR"):
            bein(b, -62 * duck - 45 * luft, 105 * duck + 20 * luft)
        for b in ("HL", "HR"):
            bein(b, -58 * duck + 55 * luft + 22 * (1 - luft) * 0, 98 * duck + 15 * luft)
        r["Kopf"] = (8 * duck - 10 * luft, 0, 0)
        r["Schwanz"] = (-10 * duck + 25 * luft, 0, 0)
        r["Schwanz2"] = (0, 0, 0)
    return r, tuple(loc)


def vierbeiner_pose(ob, art, name, ph):
    r, loc = vierbeiner_haltung(art, name, ph)
    for pb in ob.pose.bones:
        pb.rotation_euler = aus(r.get(pb.name, (0, 0, 0)))
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    ob.pose.bones["Koerper"].location = (loc[0], loc[2], -loc[1])


# ─────────────────────────────────────────────────────────────────────────────
#  Bewegungen anlegen, Dateien schreiben
# ─────────────────────────────────────────────────────────────────────────────

def bewegungen(ob, bewegungen_, pose, ruhe, einmal=("Sprung",)):
    import bpy
    ob.animation_data_create()
    for name, dauer in bewegungen_.items():
        bilder = max(4, int(round(dauer * FPS)))
        act = bpy.data.actions.new(name)
        ob.animation_data.action = act
        for f in range(0, bilder + 1):
            ph = (f / bilder) if name in einmal else (f % bilder) / bilder
            pose(ob, name, ph)
            for pb in ob.pose.bones:
                pb.keyframe_insert("rotation_euler", frame=f + 1)
                pb.keyframe_insert("scale", frame=f + 1)
            ob.pose.bones["Koerper"].keyframe_insert("location", frame=f + 1)
        spur = ob.animation_data.nla_tracks.new()
        spur.name = name
        spur.strips.new(name, 1, act)
        ob.animation_data.action = None
    pose(ob, ruhe, 0.0)


def alle_tiere():
    """[(Dateiname, Art, Familie)] in der Reihenfolge der Ausgabe."""
    return ([("vogel_kraehe", "kraehe", "vogel")] + [("tier_" + a, a, "huhn") for a in HUEHNER] +
            [("tier_hund", "hund", "vier"), ("tier_katze", "katze", "vier")] +
            [("tier_hund_schwarz", "hund", "vier"), ("tier_hund_hell", "hund", "vier"),
             ("tier_katze_schwarz", "katze", "vier"), ("tier_katze_gefleckt", "katze", "vier")])


def bauen(art, familie, st, variante=None):
    """Skelett und Netz in die aktuelle Szene; gibt (Skelett, Netz) zurück."""
    if familie == "vogel":
        ske = vg.skelett(art)
        return ske, vg.vogel(art, st, ske)
    if familie == "huhn":
        ske = huhn_skelett(art)
        return ske, huhn_bauen(art, st, ske)
    ske = vierbeiner_skelett(art)
    return ske, (hund_bauen(st, ske, variante) if art == "hund" else katze_bauen(st, ske, variante))


def pose_fuer(ske, art, familie, name, ph):
    if familie == "vogel":
        vg.pose_setzen(ske, *vg.haltung(art, name, ph))
    elif familie == "huhn":
        huhn_pose(ske, art, name, ph)
    else:
        vierbeiner_pose(ske, art, name, ph)


def anlegen(ske, art, familie):
    if familie == "vogel":
        vg.bewegungen_anlegen(ske, art)
    elif familie == "huhn":
        bewegungen(ske, HUHN_BEWEGUNGEN, lambda o, n, p: huhn_pose(o, art, n, p), "Stehen")
    else:
        bew = HUND_BEWEGUNGEN if art == "hund" else KATZE_BEWEGUNGEN
        bewegungen(ske, bew, lambda o, n, p: vierbeiner_pose(o, art, n, p), "Stehen")


def main():
    nur = [a.split("=", 1)[1].split(",") for a in sys.argv if a.startswith("--nur=")]
    nur = nur[0] if nur else None
    for datei, art, familie in alle_tiere():
        if nur and art not in nur and datei not in nur:
            continue
        bs.neue_szene()
        st = bs.Stoffe()
        ske, netz = bauen(art, familie, st, datei.split("_", 2)[2] if datei.count("_") >= 2 else None)
        anlegen(ske, art, familie)
        netz.data.calc_loop_triangles()
        bew = ([a.name for a in __import__("bpy").data.actions])
        print(f"{datei}: {len(netz.data.loop_triangles)} Dreiecke, Bewegungen {bew}")
        print("geschrieben:", fg.exportieren(datei, [ske, netz]))
    if "--ohne-bilder" not in sys.argv:
        vorschau("--schnell" in sys.argv)


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau
# ─────────────────────────────────────────────────────────────────────────────

VORSCHAUPFAD = os.environ.get("TIERE_VORSCHAU", os.path.join(bs.VORSCHAU))

# Haltungen je Tier für das Übersichtsbild: (Bewegung, Phase)
ANSICHT = {
    "kraehe": [("Sitzen", 0.25), ("Picken", 0.125), ("Gehen", 0.25), ("Fliegen", 0.0), ("Fliegen", 0.6)],
    "huhn_braun": [("Stehen", 0.25), ("Picken", 0.125), ("Gehen", 0.2), ("Kratzen", 0.3), ("Flattern", 0.0)],
    "huhn_weiss": [("Stehen", 0.25), ("Picken", 0.125), ("Gehen", 0.2), ("Kratzen", 0.3), ("Flattern", 0.0)],
    "huhn_gesprenkelt": [("Stehen", 0.25), ("Picken", 0.125), ("Gehen", 0.2), ("Kratzen", 0.3), ("Flattern", 0.0)],
    "hahn": [("Stehen", 0.25), ("Picken", 0.125), ("Gehen", 0.2), ("Kratzen", 0.3), ("Flattern", 0.0)],
    "hund": [("Stehen", 0.25), ("Gehen", 0.2), ("Traben", 0.2), ("Sitzen", 0.0), ("Liegen", 0.0), ("Schnueffeln", 0.2)],
    "katze": [("Stehen", 0.25), ("Gehen", 0.2), ("Sitzen", 0.0), ("Liegen", 0.0), ("Putzen", 0.3), ("Hocken", 0.0), ("Sprung", 0.6)],
}


def zeile(st, reihe_nr, art, familie, schritt_x, sichtbar, abstand=1.3):
    """Eine Reihe: das Tier in allen Haltungen von ANSICHT. Gibt die Netze zurück."""
    import bpy
    pos = ANSICHT[art]
    for k, (name, ph) in enumerate(pos):
        ske, netz = bauen(art, familie, st)
        x = (k - (len(pos) - 1) / 2) * abstand
        y = reihe_nr * 2.0
        ske.location = Vector((x * 0.7071 - y * 0.7071, x * 0.7071 + y * 0.7071, 0))
        ske.rotation_euler = (0, 0, math.radians(15))
        pose_fuer(ske, art, familie, name, ph)
        sichtbar.append(netz)
        if name in ("Fliegen", "Gleiten"):
            ske.location.z = 0.35


BILDER = {"kraehe": [("Sitzen", 0.25), ("Fliegen", 0.6)], "huhn_braun": [("Stehen", 0.25), ("Picken", 0.125), ("Flattern", 0.25)],
          "huhn_weiss": [("Stehen", 0.25), ("Picken", 0.125), ("Kratzen", 0.3)],
          "huhn_gesprenkelt": [("Stehen", 0.25), ("Gehen", 0.2), ("Picken", 0.125)], "hahn": [("Stehen", 0.25), ("Gehen", 0.2), ("Flattern", 0.25)],
          "hund": [("Stehen", 0.25), ("Traben", 0.2), ("Sitzen", 0.0)], "katze": [("Stehen", 0.25), ("Sitzen", 0.0), ("Hocken", 0.0)]}


def hochziehen(pfad, faktor):
    """Bild im Pixel-Look vergrößern: jeder Pixel wird faktor × faktor (nearest), ersetzt die Datei."""
    import bpy
    import numpy as np
    bild = bpy.data.images.load(pfad)
    w, h = bild.size
    a = np.array(bild.pixels[:], dtype=np.float32).reshape(h, w, 4)
    a = np.repeat(np.repeat(a, faktor, axis=0), faktor, axis=1)
    neu = bpy.data.images.new("hoch", w * faktor, h * faktor)
    neu.pixels.foreach_set(a.ravel())
    neu.filepath_raw = pfad
    neu.file_format = "PNG"
    neu.save()
    bpy.data.images.remove(bild)
    bpy.data.images.remove(neu)


def rendern_halb(name, sichtbar, breite, hoehe, faktor):
    """Rendert in halber Bildschirmauflösung (breite × hoehe) und zieht mit nearest um `faktor` hoch."""
    import bpy
    sz = bpy.context.scene
    sz.render.resolution_x, sz.render.resolution_y = breite, hoehe
    sz.render.resolution_percentage = 100
    sz.render.filter_size = 0.0                        # keine Weichzeichnung der Kanten, wie im Pixel-Look
    sz.cycles.use_denoising = False
    pfad = bs.rendern(name, sichtbar)
    hochziehen(pfad, faktor)
    return pfad


def vorschau(schnell):
    """Übersicht docs/modelle/tiere.png (je Tier eine Reihe mit den Haltungen) und je Tier ein Bild neben der Taube im
    Pixel-Look (halbe Auflösung, nearest hochgezogen) in $TIERE_VORSCHAU (sonst docs/modelle)."""
    import bpy
    bs.VORSCHAU = VORSCHAUPFAD
    reihen = [("taube", "vogel"), ("kraehe", "vogel"), ("huhn_braun", "huhn"), ("huhn_weiss", "huhn"), ("huhn_gesprenkelt", "huhn"),
              ("hahn", "huhn"), ("hund", "vier"), ("katze", "vier")]
    ANSICHT["taube"] = [("Sitzen", 0.25), ("Picken", 0.125), ("Gehen", 0.25), ("Fliegen", 0.0), ("Fliegen", 0.6)]
    bs.neue_szene()
    st = bs.Stoffe()
    sichtbar = []
    for z, (art, fam) in enumerate(reihen):
        zeile(st, z - (len(reihen) - 1) / 2, art, fam, 1.3, sichtbar)
    bs.vorschau_buehne(None, ziel_hoehe=0.3, ausschnitt=17.0, schnell=schnell)
    print(rendern_halb("tiere", lambda o: o in sichtbar, 750, 550, 2))
    # je Tier: Taube (sitzend), das Tier stehend und in zwei Bewegungen, schräg von oben wie in der Spielkamera
    for art, fam in reihen[1:]:
        bs.neue_szene()
        st = bs.Stoffe()
        sichtbar = []
        figuren = [("taube", "vogel", "Sitzen", 0.25)] + [(art, fam, n, p) for n, p in BILDER[art]]
        for k, (a, f, name, ph) in enumerate(figuren):
            ske, netz = bauen(a, f, st)
            x = (k - (len(figuren) - 1) / 2) * 1.15
            ske.location = Vector((x * 0.7071, x * 0.7071, 0))
            ske.rotation_euler = (0, 0, math.radians(15 if k else 15))
            pose_fuer(ske, a, f, name, ph)
            sichtbar.append(netz)
        bs.vorschau_buehne(None, ziel_hoehe=0.35, ausschnitt=5.0, schnell=schnell)
        print(rendern_halb("tier_" + art, lambda o: o in sichtbar, 400, 300, 3))


if __name__ == "__main__":
    main()
