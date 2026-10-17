"""Bausteine für die Gebäudemodelle aus Blender-Skripten.

Jedes Modell hat ein eigenes Skript in diesem Ordner und läuft auf zwei Wegen:

    blender -b --python tools/modelle/<name>.py        Blender als Programm
    python tools/modelle/<name>.py                     Blender als Python-Modul (pip install bpy)

Es schreibt game/assets/modelle/<name>.glb und Vorschaubilder nach docs/modelle/.

Koordinaten beim Bauen (Blender): Meter, Z oben, Vorderseite des Hauses nach -Y.
Der glTF-Export macht daraus Y oben und Vorderseite +Z – so, wie das Spiel es braucht.
Ursprung = Mitte der belegten Fläche (mit Vorplatz) auf dem Boden.

Grundidee: Die Geometrie wird nicht mit Blender-Operatoren gebaut, sondern als Vielecke
gesammelt (Klasse Netz) und am Ende als ein Mesh je Objekt geschrieben. Das ist schnell,
braucht keinen Fensterkontext und ergibt wenige Knoten.

Flächen, die nach unten zeigen, lässt Netz weg: Die Spielkamera schaut immer 40° von oben,
eine Fläche, deren Normale mehr als 50° unter die Waagrechte zeigt, sieht sie nie.
Das spart rund ein Sechstel der Dreiecke.
"""

import math
import os
import random

import bpy
from mathutils import Matrix, Vector

WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
MODELLE = os.path.join(WURZEL, "game", "assets", "modelle")
VORSCHAU = os.path.join(WURZEL, "docs", "modelle")

KAMERA_NEIGUNG = 40.0        # Grad, wie im Spiel
UNSICHTBAR_Z = -0.77         # Normale mit z darunter (> 50° nach unten) sieht die Kamera nie


def neue_szene():
    """Leere Szene ohne Würfel, Kamera und Licht."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _stoffe.clear()
    return bpy.context.scene


# ─────────────────────────────────────────────────────────────────────────────
#  Stoffe (Materialien): feste Farben, keine Bilder
# ─────────────────────────────────────────────────────────────────────────────

# sRGB-Werte 0–1, gedeckt und eher dunkel – ein düsteres, bodenständiges Reich.
FARBEN = {
    "Putz": (0.74, 0.69, 0.58),            # Kalkputz, leicht ocker und vergraut
    "Fachwerk": (0.22, 0.15, 0.10),        # dunkle, alte Eiche
    "Bruchstein": (0.47, 0.45, 0.41),
    "Bruchstein dunkel": (0.35, 0.34, 0.32),
    "Dachziegel": (0.50, 0.27, 0.19),      # Biberschwanz, gedecktes Rotbraun
    "Firstziegel": (0.36, 0.20, 0.15),
    "Ziegelkante": (0.32, 0.18, 0.13),     # Stoßkante jeder Ziegelreihe
    "Glas": (0.09, 0.11, 0.14),
    "Eisen": (0.10, 0.10, 0.11),
    "Holz": (0.38, 0.28, 0.18),            # Bank, Fass, Fahnenstange
    "Bretter": (0.46, 0.41, 0.34),         # vergraute Bretter im Leerstand
    "Geruestholz": (0.62, 0.50, 0.33),     # frisches Holz
    "Loch": (0.11, 0.08, 0.06),            # fehlende Ziegel im Leerstand: Blick auf dunkles Gebälk
    "Gras": (0.31, 0.36, 0.19),
    "Hausfarbe": (0.18, 0.30, 0.58),       # Vorgabe Spielerblau; das Spiel färbt je Haus um
}
RAUHEIT = {"Glas": 0.2, "Eisen": 0.45}
METALL = {"Eisen": 0.7}

_stoffe = {}


def _linear(c):
    """sRGB → linear (Blender rechnet linear, der glTF-Export ebenso)."""
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def stoff(name):
    """Material nach Name aus FARBEN; gleicher Name = dasselbe Material."""
    if name in _stoffe:
        return _stoffe[name]
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True          # ab Blender 5 immer an, die Eigenschaft ist dort veraltet
    except AttributeError:
        pass
    p = m.node_tree.nodes.get("Principled BSDF")
    farbe = (*_linear(FARBEN[name]), 1.0)
    p.inputs["Base Color"].default_value = farbe
    p.inputs["Roughness"].default_value = RAUHEIT.get(name, 0.9)
    p.inputs["Metallic"].default_value = METALL.get(name, 0.0)
    m.diffuse_color = farbe
    _stoffe[name] = m
    return m


class Stoffe(dict):
    """st["Putz"] legt das Material beim ersten Zugriff an."""

    def __missing__(self, name):
        self[name] = stoff(name)
        return self[name]


# ─────────────────────────────────────────────────────────────────────────────
#  Netz: sammelt Vielecke und wird ein Objekt
# ─────────────────────────────────────────────────────────────────────────────

def _newell(punkte):
    """Normale eines (auch nicht ganz ebenen) Vielecks nach Newell, Länge = doppelte Fläche."""
    n = Vector((0.0, 0.0, 0.0))
    for i, a in enumerate(punkte):
        b = punkte[(i + 1) % len(punkte)]
        n.x += (a.y - b.y) * (a.z + b.z)
        n.y += (a.z - b.z) * (a.x + b.x)
        n.z += (a.x - b.x) * (a.y + b.y)
    return n


def _flaeche2d(profil):
    """Vorzeichenbehaftete Fläche (positiv = gegen den Uhrzeigersinn)."""
    return sum(profil[i][0] * profil[(i + 1) % len(profil)][1] - profil[(i + 1) % len(profil)][0] * profil[i][1]
               for i in range(len(profil))) / 2


def _richtung(dir_, quer):
    """Drehmatrix mit x = dir_, y = quer (senkrecht gestellt), z = x × y."""
    x = dir_.normalized()
    y = Vector(quer)
    if abs(y.normalized().dot(x)) > 0.99:
        y = Vector((0, 0, 1)) if abs(x.z) < 0.9 else Vector((1, 0, 0))
    y = (y - x * y.dot(x)).normalized()
    z = x.cross(y)
    return Matrix((x, y, z)).transposed().to_4x4()


class Netz:
    """Sammelt Geometrie für ein Objekt. Jede Fläche kennt ihr Material."""

    def __init__(self, name):
        self.name = name
        self.ecken = []
        self.flaechen = []
        self.fstoff = []
        self.stoffe = []

    # Grundform: ein Vieleck, Ecken gegen den Uhrzeigersinn von außen gesehen
    def vieleck(self, punkte, m, immer=False):
        punkte = [Vector(p) for p in punkte]
        n = _newell(punkte)
        if n.length < 1e-10:
            return
        if not immer and n.z / n.length < UNSICHTBAR_Z:
            return
        if m not in self.stoffe:
            self.stoffe.append(m)
        start = len(self.ecken)
        self.ecken.extend(punkte)
        self.flaechen.append(tuple(range(start, start + len(punkte))))
        self.fstoff.append(self.stoffe.index(m))

    def vieleck_nach(self, punkte, normale, m):
        """Vieleck, das nach `normale` zeigen soll – die Reihenfolge wird passend gedreht."""
        punkte = [Vector(p) for p in punkte]
        if _newell(punkte).dot(Vector(normale)) < 0:
            punkte.reverse()
        self.vieleck(punkte, m)

    def quader(self, mitte, masse, m, matrix=None, ohne=()):
        """Quader um `mitte` mit Kantenlängen `masse`. `ohne`: Seiten weglassen ("+x", "-z" …),
        bezogen auf den Quader vor `matrix` – z. B. "+y" für die Seite, die an der Wand liegt."""
        hx, hy, hz = (a / 2 for a in masse)
        t = Matrix.Translation(Vector(mitte))
        if matrix is not None:
            t = matrix @ t
        seiten = {
            "+x": ((hx, -hy, -hz), (hx, hy, -hz), (hx, hy, hz), (hx, -hy, hz)),
            "-x": ((-hx, -hy, -hz), (-hx, -hy, hz), (-hx, hy, hz), (-hx, hy, -hz)),
            "+y": ((-hx, hy, -hz), (-hx, hy, hz), (hx, hy, hz), (hx, hy, -hz)),
            "-y": ((-hx, -hy, -hz), (hx, -hy, -hz), (hx, -hy, hz), (-hx, -hy, hz)),
            "+z": ((-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz)),
            "-z": ((-hx, -hy, -hz), (-hx, hy, -hz), (hx, hy, -hz), (hx, -hy, -hz)),
        }
        for k, ecken in seiten.items():
            if k not in ohne:
                self.vieleck([t @ Vector(e) for e in ecken], m)

    def stab(self, a, b, breite, dicke, m, matrix=None, quer=(0, 1, 0), ohne=(), mehr=0.0):
        """Balken von Punkt a nach b. `dicke` liegt entlang `quer`, `breite` senkrecht dazu.
        `mehr` verlängert ihn an beiden Enden zusammen um diesen Betrag."""
        a, b = Vector(a), Vector(b)
        laenge = (b - a).length
        t = Matrix.Translation((a + b) / 2) @ _richtung(b - a, quer)
        if matrix is not None:
            t = matrix @ t
        self.quader((0, 0, 0), (laenge + mehr, dicke, breite), m, t, ohne)

    def prisma(self, profil, x0, x1, m, matrix=None, m_enden=None, kante=None):
        """Vieleck `profil` [(y, z), …] entlang X von x0 bis x1 ausgezogen. Darf eingebuchtet sein.
        `m_enden`: eigenes Material für die beiden Stirnseiten.
        `kante(p, q)`: liefert für eine Profilkante ein anderes Material oder None."""
        profil = [tuple(p) for p in profil]
        if _flaeche2d(profil) < 0:
            profil = list(reversed(profil))
        t = matrix if matrix is not None else Matrix.Identity(4)
        vorn = [t @ Vector((x1, y, z)) for y, z in profil]
        hinten = [t @ Vector((x0, y, z)) for y, z in profil]
        self.vieleck(vorn, m_enden or m)
        self.vieleck(list(reversed(hinten)), m_enden or m)
        n = len(profil)
        for i in range(n):
            j = (i + 1) % n
            mk = kante(Vector(profil[i]), Vector(profil[j])) if kante else None
            self.vieleck([hinten[i], hinten[j], vorn[j], vorn[i]], mk or m)

    def dreiecke(self):
        return sum(len(f) - 2 for f in self.flaechen)

    def objekt(self):
        """Schreibt das Gesammelte als Mesh in die Szene und gibt das Objekt zurück."""
        mesh = bpy.data.meshes.new(self.name)
        mesh.from_pydata([tuple(v) for v in self.ecken], [], self.flaechen)
        for m in self.stoffe:
            mesh.materials.append(m)
        for p, i in zip(mesh.polygons, self.fstoff):
            p.material_index = i
            p.use_smooth = False
        mesh.validate(verbose=False)
        mesh.update()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


# ─────────────────────────────────────────────────────────────────────────────
#  Wände: lokales Wandsystem
#     u = entlang der Wand (von außen gesehen nach rechts), v = Höhe ab Wandfuß,
#     y = in die Wand hinein (y < 0 ist außen). Ursprung: Wandmitte, Wandfuß, Außenfläche.
# ─────────────────────────────────────────────────────────────────────────────

B = 0.15                  # Balkenbreite des Fachwerks
VOR_PFOSTEN = 0.05        # wie weit Ständer vor dem Putz stehen
VOR_SCHWELLE = 0.055      # Schwelle und Rähm etwas weiter
VOR_RIEGEL = 0.04
VOR_STREBE = 0.034
FELD = 1.2                # größter Ständerabstand ohne Öffnung


def wandmatrix(punkt, aussen):
    """Wandsystem an `punkt` (Wandmitte, Fuß, Außenfläche); `aussen` = Richtung der Außenseite."""
    winkel = {"vorne": 0, "rechts": 90, "hinten": 180, "links": 270}[aussen]
    return Matrix.Translation(Vector(punkt)) @ Matrix.Rotation(math.radians(winkel), 4, "Z")


class Oeffnung:
    """Fenster oder Tür im Wandsystem: Mitte u, Unterkante v, Breite b, Höhe h."""

    def __init__(self, art, u, v, b, h, laeden=True):
        self.art, self.u, self.v, self.b, self.h, self.laeden = art, u, v, b, h, laeden


def balken(netz, M, u0, v0, u1, v1, m, vor=VOR_PFOSTEN, breite=B, ein=0.02, mehr=0.0, enden=False):
    """Fachwerkbalken im Wandsystem: steht `vor` vor dem Putz, steckt `ein` darin.
    Ohne `enden` fehlen die Stirnseiten – sie stoßen an Schwelle, Rähm oder Ständer und sind nie zu sehen."""
    y = (-vor + ein) / 2
    ohne = ("+y",) if enden else ("+y", "+x", "-x")
    netz.stab((u0, y, v0), (u1, y, v1), breite, vor + ein, m, matrix=M, quer=(0, 1, 0), ohne=ohne, mehr=mehr)


def platte(netz, M, umriss, y0, y1, m, m_enden=None):
    """Ebene Platte im Wandsystem: Umriss [(u, v), …], Dicke von y0 bis y1."""
    dreh = Matrix.Rotation(math.radians(90), 4, "Z")   # Prisma-x → Wand-y, Prisma-y → Wand -u
    netz.prisma([(-u, v) for u, v in umriss], y0, y1, m, matrix=M @ dreh, m_enden=m_enden)


def fachwerk(netz, M, L, H, oeffnungen, st, kreuze=False, eckstreben=True):
    """Fachwerk auf einer Wand der Länge L und Höhe H (der Putz kommt vom Aufrufer):
    Schwelle, Rähm, Ständer an Ecken und Öffnungen, Füllständer, Brust- und Sturzriegel,
    Streben in den Eckfeldern (zur Ecke ansteigend) und auf Wunsch Andreaskreuze in der Brüstung."""
    holz = st["Fachwerk"]
    h = L / 2
    # Ständer: Ecken und Öffnungsränder, dicht beieinander liegende zusammengelegt
    roh = sorted([-h + B / 2, h - B / 2] + [o.u + s * (o.b / 2 + B / 2) for o in oeffnungen for s in (-1, 1)])
    staender = []
    for u in roh:
        if staender and u - staender[-1] < B * 1.5:
            staender[-1] = (staender[-1] + u) / 2
        else:
            staender.append(u)

    def oeffnung_bei(u):
        return next((o for o in oeffnungen if abs(u - o.u) < o.b / 2), None)

    voll = []
    for a, b in zip(staender, staender[1:]):
        voll.append(a)
        if oeffnung_bei((a + b) / 2) is None:
            n = math.ceil((b - a) / FELD) - 1
            voll += [a + (b - a) * k / (n + 1) for k in range(1, n + 1)]
    voll.append(staender[-1])
    felder = [(a, b, oeffnung_bei((a + b) / 2)) for a, b in zip(voll, voll[1:])]

    fenster = [o for o in oeffnungen if o.art == "fenster"]
    v_brust = min((o.v for o in fenster), default=0.9)
    v_sturz = max((o.v + o.h for o in fenster), default=None)

    balken(netz, M, -h, B / 2, h, B / 2, holz, vor=VOR_SCHWELLE, enden=True)            # Schwelle
    balken(netz, M, -h, H - B / 2, h, H - B / 2, holz, vor=VOR_SCHWELLE, enden=True)    # Rähm
    for u in voll:
        balken(netz, M, u, B, u, H - B, holz)

    def riegel_laeufe(v):
        """Riegel bei Höhe v über alle Felder, unterbrochen an Türen."""
        lauf = None
        for a, b, o in felder + [(felder[-1][1], None, "ende")]:
            tuer = o == "ende" or (o is not None and o.art == "tuer")
            if not tuer and lauf is None:
                lauf = a
            if tuer and lauf is not None:
                balken(netz, M, lauf, v, a, v, holz, vor=VOR_RIEGEL)
                lauf = None

    riegel_laeufe(v_brust - B / 2)
    if v_sturz is not None and v_sturz + B < H - B - 0.2:
        riegel_laeufe(v_sturz + B / 2)
    for o in oeffnungen:
        if o.art == "tuer" and o.v + o.h + B < H - B - 0.1:
            balken(netz, M, o.u - o.b / 2 - B, o.v + o.h + B / 2, o.u + o.b / 2 + B, o.v + o.h + B / 2, holz, vor=VOR_RIEGEL)

    def kreuz(u0, v0, u1, v1):
        balken(netz, M, u0, v0, u1, v1, holz, vor=VOR_STREBE, breite=B * 0.85)
        balken(netz, M, u0, v1, u1, v0, holz, vor=VOR_STREBE + 0.004, breite=B * 0.85)

    for i, (a, b, o) in enumerate(felder):
        x0, x1 = a + B / 2, b - B / 2
        if o is not None and o.art == "tuer":
            continue
        ende = i == 0 or i == len(felder) - 1
        if o is None and ende and eckstreben and x1 - x0 > 0.45:
            # Strebe steigt von der Schwelle zum Eckständer an
            if i == 0:
                balken(netz, M, x1, B, x0, H - B, holz, vor=VOR_STREBE)
            else:
                balken(netz, M, x0, B, x1, H - B, holz, vor=VOR_STREBE)
        elif kreuze and v_brust - 2 * B > 0.35:
            oben = (o.v if o is not None else v_brust) - B
            kreuz(x0, B, x1, oben)


def fenster(netz, M, o, st, winkel=32):
    """Glas, Fensterkreuz und zwei halb geöffnete Läden in der Hausfarbe."""
    y = -0.006
    netz.vieleck([M @ Vector(p) for p in ((o.u - o.b / 2, y, o.v), (o.u + o.b / 2, y, o.v),
                                          (o.u + o.b / 2, y, o.v + o.h), (o.u - o.b / 2, y, o.v + o.h))], st["Glas"])
    y, d, vk = -0.02, 0.025, o.v + o.h * 0.58       # Fensterkreuz: flache Leisten vor dem Glas
    for ecken in (((o.u - d, o.v), (o.u + d, o.v), (o.u + d, o.v + o.h), (o.u - d, o.v + o.h)),
                  ((o.u - o.b / 2, vk - d), (o.u + o.b / 2, vk - d), (o.u + o.b / 2, vk + d), (o.u - o.b / 2, vk + d))):
        netz.vieleck([M @ Vector((u, y, v)) for u, v in ecken], st["Fachwerk"])
    if not o.laeden:
        return
    breite = o.b / 2
    for s in (-1, 1):
        dreh = -winkel if s > 0 else 180 + winkel
        t = (M @ Matrix.Translation((o.u + s * o.b / 2, -VOR_PFOSTEN - 0.012, o.v + o.h / 2))
             @ Matrix.Rotation(math.radians(dreh), 4, "Z"))
        netz.quader((breite / 2, 0, 0), (breite, 0.035, o.h), st["Hausfarbe"], t)


def tuer(netz, M, o, st):
    """Brettertür in der Hausfarbe, leicht zurückgesetzt, mit Eisenbändern und Ring."""
    netz.quader((o.u, -0.01, o.v + o.h / 2), (o.b, 0.04, o.h), st["Hausfarbe"], M, ohne=("+y",))
    for f in (0.22, 0.78):
        netz.quader((o.u - o.b * 0.08, -0.037, o.v + o.h * f), (o.b * 0.78, 0.014, 0.07), st["Eisen"], M, ohne=("+y",))
    netz.quader((o.u + o.b * 0.32, -0.05, o.v + o.h * 0.5), (0.07, 0.04, 0.09), st["Eisen"], M, ohne=("+y",))


def bretter(netz, M, o, st, nummer):
    """Leerstand: Bretter vor der Öffnung, etwas schief genagelt (fest aus der Nummer)."""
    zufall = random.Random(nummer * 7919)
    holz = st["Bretter"]

    def brett(mu, mv, laenge, winkel, y):
        w = math.radians(winkel)
        du, dv = math.cos(w) * laenge / 2, math.sin(w) * laenge / 2
        netz.stab((mu - du, y, mv - dv), (mu + du, y, mv + dv), 0.17, 0.03, holz, matrix=M, ohne=("+y",))

    mitte = o.v + o.h / 2
    if o.art == "tuer":
        for k, f in enumerate((0.2, 0.5, 0.8)):
            brett(o.u, o.v + o.h * f, o.b + 0.25, zufall.uniform(-7, 7), -0.07 - 0.01 * k)
        brett(o.u, mitte, math.hypot(o.b, o.h * 0.75), math.degrees(math.atan2(o.h * 0.75, o.b)) * (1 if nummer % 2 else -1), -0.11)
    else:
        diag = math.degrees(math.atan2(o.h, o.b))
        laenge = math.hypot(o.b, o.h) + 0.05
        brett(o.u, mitte, laenge, diag + zufall.uniform(-5, 5), -0.075)
        brett(o.u, mitte, laenge, -diag + zufall.uniform(-5, 5), -0.105)
        if o.b > 0.8:
            brett(o.u, mitte + zufall.uniform(-0.1, 0.1), o.b + 0.15, zufall.uniform(-4, 4), -0.135)


# ─────────────────────────────────────────────────────────────────────────────
#  Dächer: Ziegelreihen als Sägezahn im Querschnitt
# ─────────────────────────────────────────────────────────────────────────────

KANTE = 0.065  # Breite des dunklen Streifens an der Unterkante jeder Ziegelreihe


def zaehne(start, richtung, normale, laenge, dicke, reihe=0.45, stufe=0.05):
    """Oberseite einer Dachfläche als Sägezahn im Querschnitt (y, z): Jede Ziegelreihe liegt
    mit ihrer Unterkante auf der Reihe darunter. Liefert die Punkte von der Traufe bis zum Ende.
    Je Reihe ein schmaler Streifen an der Unterkante (Länge KANTE): Bei steilen Dächern sieht die
    Kamera die Stoßkanten selbst nicht, der Streifen bekommt darum die dunkle Ziegelkante."""
    n = max(1, round(laenge / reihe))
    punkte = []
    for k in range(n):
        s0, s1 = laenge * k / n, laenge * (k + 1) / n
        punkte.append(start + richtung * s0 + normale * (dicke + stufe))
        punkte.append(start + richtung * (s0 + KANTE) + normale * (dicke + stufe * (1 - KANTE / (s1 - s0))))
        punkte.append(start + richtung * s1 + normale * dicke)
    return punkte


def reihenkante(st, stufe):
    """Für Netz.prisma: kurze Kanten (die Stöße der Ziegelreihen) bekommen die dunkle Ziegelkante."""
    return lambda p, q: st["Ziegelkante"] if (q - p).length < max(stufe, KANTE) * 1.2 else None


def satteldach(netz, st, x0, x1, y_vorn, y_hinten, z_traufe, neigung, traufe=0.35, ortgang=0.35,
               dicke=0.22, reihe=0.45, stufe=0.06):
    """Satteldach mit First entlang X über der Wand von y_vorn bis y_hinten, Wandoberkante z_traufe.
    Stirnseiten in Holz (wirkt wie ein Windbrett). Gibt Maße für weitere Teile zurück."""
    th = math.radians(neigung)
    tan, cos, sin = math.tan(th), math.cos(th), math.sin(th)
    halb = (y_hinten - y_vorn) / 2
    y_first = (y_vorn + y_hinten) / 2
    unten_first = Vector((y_first, z_traufe + halb * tan))
    oben_first = Vector((y_first, unten_first.y + dicke / cos))
    traufe_vorn = Vector((y_vorn - traufe, z_traufe - traufe * tan))
    traufe_hinten = Vector((y_hinten + traufe, z_traufe - traufe * tan))
    laenge = (halb + traufe) / cos + dicke * tan       # bis zum oberen Firstpunkt
    vorn = zaehne(traufe_vorn, Vector((cos, sin)), Vector((-sin, cos)), laenge, dicke, reihe, stufe)
    hinten = zaehne(traufe_hinten, Vector((-cos, sin)), Vector((sin, cos)), laenge, dicke, reihe, stufe)
    profil = [traufe_vorn, unten_first, traufe_hinten] + hinten[:-1] + [oben_first] + list(reversed(vorn[:-1]))
    netz.prisma(profil, x0 - ortgang, x1 + ortgang, st["Dachziegel"], m_enden=st["Fachwerk"], kante=reihenkante(st, stufe))
    # Firstziegel: über Eck gestellter Balken
    first = Matrix.Translation((0, y_first, oben_first.y + 0.02)) @ Matrix.Rotation(math.radians(45), 4, "X")
    netz.quader((0, 0, 0), (x1 - x0 + 2 * ortgang + 0.06, 0.24, 0.24), st["Firstziegel"], first)

    def oben_vorn(y):
        """Höhe der Dachoberfläche vorn bei y (ohne Zahnstufe)."""
        return z_traufe + (y - y_vorn) * tan + dicke / cos

    def oben_hinten(y):
        return z_traufe + (y_hinten - y) * tan + dicke / cos

    return {"y_first": y_first, "z_first": oben_first.y, "oben_vorn": oben_vorn, "oben_hinten": oben_hinten,
            "tan": tan, "dicke": dicke, "traufe_vorn": traufe_vorn, "traufe_hinten": traufe_hinten,
            "richtung_vorn": Vector((cos, sin)), "normale_vorn": Vector((-sin, cos)),
            "richtung_hinten": Vector((-cos, sin)), "normale_hinten": Vector((sin, cos))}


# ─────────────────────────────────────────────────────────────────────────────
#  Export, Bericht, Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def zustand(obj):
    """Markiert ein Objekt als Zustand: im Spiel standardmäßig ausgeblendet.
    glTF kennt keine Sichtbarkeit; der Hinweis steht in den „extras“ des Knotens."""
    obj["zustand"] = True
    obj["sichtbar"] = False
    return obj


def exportieren(name, objekte):
    """GLB nach game/assets/modelle/<name>.glb – Y oben, Vorderseite +Z, nur diese Objekte."""
    os.makedirs(MODELLE, exist_ok=True)
    pfad = os.path.join(MODELLE, name + ".glb")
    for o in bpy.context.scene.objects:
        o.select_set(o in objekte)
    bpy.ops.export_scene.gltf(
        filepath=pfad, export_format="GLB", use_selection=True, export_yup=True, export_apply=True,
        export_extras=True, export_normals=True, export_texcoords=False, export_animations=False,
        export_skins=False, export_morph=False, export_lights=False, export_cameras=False,
        export_image_format="NONE")
    return pfad


def bericht(name, objekte):
    """Dreiecke je Objekt (so, wie sie im Export landen)."""
    print(f"\n{name}: Dreiecke")
    summe = 0
    for o in objekte:
        o.data.calc_loop_triangles()
        n = len(o.data.loop_triangles)
        summe += n
        print(f"  {o.name:10s} {n:6d}")
    print(f"  {'gesamt':10s} {summe:6d}\n")


def vorschau_buehne(flaeche, ziel_hoehe=5.0, ausschnitt=17.0, schnell=False):
    """Boden, Gasse, Rahmen der belegten Fläche, Sonne, Himmel und die Spielkamera
    (orthografisch, 40° Neigung, diagonal). Nur fürs Bild, wird nicht exportiert."""
    szene = bpy.context.scene
    umgebung = Netz("_Umgebung")
    st = {n: _hilfsstoff(n, f) for n, f in (("_Boden", (0.33, 0.33, 0.25)), ("_Gasse", (0.36, 0.34, 0.31)),
                                            ("_Rahmen", (0.86, 0.80, 0.52)))}
    umgebung.quader((0, 0, -0.05), (80, 80, 0.1), st["_Boden"])
    umgebung.quader((0, -flaeche[1] / 2 - 3.25, -0.045), (80, 6.5, 0.1), st["_Gasse"])
    bx, by = flaeche[0] / 2, flaeche[1] / 2
    for mitte, masse in (((0, -by, 0.005), (flaeche[0] + 0.08, 0.08, 0.01)), ((0, by, 0.005), (flaeche[0] + 0.08, 0.08, 0.01)),
                         ((-bx, 0, 0.005), (0.08, flaeche[1], 0.01)), ((bx, 0, 0.005), (0.08, flaeche[1], 0.01))):
        umgebung.quader(mitte, masse, st["_Rahmen"])
    umgebung.objekt()

    kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    kam.data.type = "ORTHO"
    kam.data.ortho_scale = ausschnitt
    kam.data.clip_end = 400
    szene.collection.objects.link(kam)
    szene.camera = kam
    sonne = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN"))
    sonne.data.energy = 3.2
    sonne.data.color = (1.0, 0.94, 0.84)
    sonne.data.angle = math.radians(2.5)
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

    szene.render.engine = "CYCLES"
    szene.cycles.device = "CPU"
    szene.cycles.samples = 16 if schnell else 96
    szene.cycles.use_denoising = True
    szene.render.resolution_x, szene.render.resolution_y = 1200, 900
    szene.render.resolution_percentage = 50 if schnell else 100
    szene.view_settings.view_transform = "Standard"
    szene["_ziel"] = ziel_hoehe
    blick(0)


def _hilfsstoff(name, farbe):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*_linear(farbe), 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.95
    return m


def blick(drehung):
    """Kamera und Sonne in eine der vier Spielrichtungen: 0 = von vorne rechts,
    1 = hinten rechts, 2 = hinten links, 3 = vorne links (je 90° weiter)."""
    szene = bpy.context.scene
    az = math.radians(-45 + 90 * drehung)
    neig = math.radians(KAMERA_NEIGUNG)
    ziel = Vector((0, 0, szene["_ziel"]))
    kam = szene.camera
    kam.location = ziel + 80 * Vector((math.cos(az) * math.cos(neig), math.sin(az) * math.cos(neig), math.sin(neig)))
    kam.rotation_euler = (math.radians(90 - KAMERA_NEIGUNG), 0, az + math.radians(90))
    # Sonne von vorne, etwas von rechts der Kamera: beide sichtbaren Seiten hell, aber verschieden
    zum_licht = Vector((math.cos(az - math.radians(28)), math.sin(az - math.radians(28)), 1.05)).normalized()
    bpy.data.objects["Sonne"].rotation_euler = (-zum_licht).to_track_quat("-Z", "Y").to_euler()


def rendern(name, sichtbar, drehung=0):
    """Rendert docs/modelle/<name>.png. `sichtbar(obj)` entscheidet je Objekt."""
    os.makedirs(VORSCHAU, exist_ok=True)
    blick(drehung)
    for o in bpy.context.scene.objects:
        if o.type == "MESH":
            o.hide_render = not (o.name.startswith("_") or sichtbar(o))
    pfad = os.path.join(VORSCHAU, name + ".png")
    bpy.context.scene.render.filepath = pfad
    bpy.ops.render.render(write_still=True)
    return pfad


def rundum(name, sichtbar):
    """Vier Ansichten (alle Drehungen der Spielkamera) in einem Bild docs/modelle/<name>-rundum.png.
    Braucht Pillow; fehlt es, bleiben die vier Einzelbilder liegen."""
    szene = bpy.context.scene
    prozent = szene.render.resolution_percentage
    szene.render.resolution_percentage = prozent // 2
    teile = [rendern(f"_{name}-blick{k}", sichtbar, k) for k in range(4)]
    szene.render.resolution_percentage = prozent
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        print("Pillow fehlt – Einzelbilder bleiben:", teile)
        return None
    bilder = [Image.open(p).convert("RGB") for p in teile]
    w, h = bilder[0].size
    gesamt = Image.new("RGB", (2 * w, 2 * h))
    namen = ["vorne rechts", "hinten rechts", "vorne links", "hinten links"]
    for i, (bild, k) in enumerate(zip(bilder, (0, 1, 3, 2))):
        bild = bilder[k]
        x, y = (i % 2) * w, (i // 2) * h
        gesamt.paste(bild, (x, y))
        ImageDraw.Draw(gesamt).text((x + 10, y + 8), "Blick von " + namen[i], fill=(240, 236, 220))
    pfad = os.path.join(VORSCHAU, name + "-rundum.png")
    gesamt.save(pfad, optimize=True)
    for p in teile:
        os.remove(p)
    return pfad
