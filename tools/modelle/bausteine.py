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
    "Glas": (0.09, 0.11, 0.14),            # dunkles Fenster (bleibt auch nachts dunkel)
    "Fenster": (0.09, 0.11, 0.14),         # Fenster mit Licht: tagsüber wie Glas, nachts schaltet das Spiel es an
    "Eisen": (0.10, 0.10, 0.11),
    "Holz": (0.38, 0.28, 0.18),            # Bank, Fass, Fahnenstange
    "Bretter": (0.46, 0.41, 0.34),         # vergraute Bretter im Leerstand
    "Geruestholz": (0.62, 0.50, 0.33),     # frisches Holz
    "Loch": (0.11, 0.08, 0.06),            # fehlende Ziegel im Leerstand: Blick auf dunkles Gebälk
    "Gras": (0.31, 0.36, 0.19),
    "Hausfarbe": (0.18, 0.30, 0.58),       # Vorgabe Spielerblau; das Spiel färbt je Haus um
    # Fachwerkhaus (Gemeiner): Lehm, Stroh, altes Holz
    "Lehmputz": (0.63, 0.57, 0.46),        # fleckiger Lehmputz
    "Lehm": (0.42, 0.33, 0.24),            # abgeplatzter Putz, das Flechtwerk schaut durch
    "Stroh": (0.49, 0.43, 0.30),           # altes, vergrautes Stroh
    "Strohkante": (0.38, 0.32, 0.21),
    "Firstsoden": (0.30, 0.30, 0.20),      # Grassoden auf dem First
    "Moos": (0.40, 0.39, 0.26),
    "Holz alt": (0.33, 0.27, 0.20),        # verwitterte Bretter
    "Scheitholz": (0.60, 0.48, 0.33),      # Hirnholz am Holzstapel
    # Steinhaus (Patrizier): Haustein, Sandstein, Schiefer
    "Haustein": (0.53, 0.52, 0.49),        # graues Mauerwerk der Wände
    "Fuge": (0.40, 0.39, 0.37),            # Lagerfugen im Mauerwerk
    "Sandstein": (0.74, 0.64, 0.48),       # warmer Stein für Eckquader, Gewände, Gesimse
    "Schiefer": (0.27, 0.29, 0.33),
    "Schieferkante": (0.17, 0.18, 0.21),
    "Firstblech": (0.22, 0.23, 0.25),
    # Werkstätten
    "Glut": (0.45, 0.14, 0.05),            # Esse und Feuerschein; leuchtet (Emission), das Spiel regelt die Stärke
    "Gold": (0.86, 0.67, 0.27),
    "Kohle": (0.11, 0.11, 0.12),
    "Formsand": (0.46, 0.37, 0.27),
    "Wasser": (0.15, 0.21, 0.23),
    "Leder": (0.31, 0.20, 0.13),
    "Kupfer": (0.36, 0.56, 0.47),          # grün angelaufenes Kupfer (Dachreiter des Tischlers)
    "Schnittholz": (0.66, 0.53, 0.36),     # frisch gesägte Bretter und Hirnholz
    "Seil": (0.55, 0.48, 0.35),
    "Steinstaub": (0.70, 0.68, 0.63),      # heller Staub und Splitt am Werkplatz des Steinmetzen
    "Kraut": (0.30, 0.42, 0.22),           # frische Kräuter, Beete, Sud
    "Heu": (0.56, 0.50, 0.30),             # getrocknete Kräuterbündel
    "Rose": (0.62, 0.16, 0.22),
    "Farbe": (0.58, 0.18, 0.14),           # Farbsud im Bottich (Ware des Alchimisten)
    "Elixier": (0.24, 0.48, 0.42),         # Flaschen und Kolben
    "Violett": (0.45, 0.30, 0.55),         # Spitze des Dachreiters beim Alchimisten (wie bisher im Spiel)
    "Weinrot": (0.60, 0.25, 0.20),         # Spitze des Dachreiters beim Wirt (wie bisher im Spiel)
    "Ocker": (0.80, 0.66, 0.32),           # Wirtshausschild (wie bisher im Spiel)
    "Laub": (0.24, 0.35, 0.16),
    "Laub hell": (0.34, 0.44, 0.20),
    "Silber": (0.82, 0.82, 0.86),          # Spitze des Dachreiters beim Prediger (wie bisher im Spiel)
    "Nachtblau": (0.16, 0.18, 0.30),       # Spitze des Dachreiters beim Dieb (wie bisher im Spiel)
    "Schindel": (0.38, 0.32, 0.27),        # morsche Holzschindeln (Diebesnest, wie bisher im Spiel „Morsch“)
    "Schindelkante": (0.24, 0.20, 0.17),
    "Lumpen": (0.52, 0.48, 0.40),
    "Stadtrot": (0.72, 0.12, 0.12),        # Stadtfarben (Wappen, Schilderhäuschen, Fahne der Wache, wie bisher im Spiel)
    "Stadtweiss": (0.92, 0.90, 0.85),
    "Spitze rot": (0.72, 0.12, 0.12),      # Spitze des Dachreiters beim Gardisten (wie bisher im Spiel, metallisch)
}
RAUHEIT = {"Glas": 0.2, "Fenster": 0.2, "Eisen": 0.45, "Gold": 0.35, "Wasser": 0.1}
METALL = {"Eisen": 0.7, "Gold": 0.85, "Kupfer": 0.5, "Violett": 0.5, "Weinrot": 0.5, "Silber": 0.5, "Nachtblau": 0.5, "Spitze rot": 0.5}
LEUCHTEN = {"Glut": ((1.0, 0.45, 0.12), 3.0)}    # Farbe (sRGB) und Stärke der Emission
# Licht hinter den Fenstern bei Nacht: Farbe (sRGB) und Stärke. Nicht in der GLB (dort aus), nur für die Nachtbilder –
# im Spiel schaltet der Code die Emission des gemeinsamen Materials „Fenster“ abends an und morgens aus.
NACHTLICHT = {"Fenster": ((1.0, 0.62, 0.28), 4.0)}


def scheibe(M, o):
    """Material der Scheibe: etwa zwei von drei Fenstern sind nachts erleuchtet („Fenster“), die übrigen
    bleiben dunkel („Glas“). Fest aus der Lage des Fensters, also bei jedem Erzeugen gleich."""
    p = M @ Vector((o.u, 0, o.v))
    return "Glas" if int(abs(p.x * 3.1 + p.y * 1.7 + p.z * 2.3) * 10) % 3 == 0 else "Fenster"

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
    if name in LEUCHTEN:
        licht, staerke = LEUCHTEN[name]
        p.inputs["Emission Color"].default_value = (*_linear(licht), 1.0)
        p.inputs["Emission Strength"].default_value = staerke
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


def fachwerk(netz, M, L, H, oeffnungen, st, kreuze=False, eckstreben=True, feld=FELD, schief=0.0, saat=0):
    """Fachwerk auf einer Wand der Länge L und Höhe H (der Putz kommt vom Aufrufer):
    Schwelle, Rähm, Ständer an Ecken und Öffnungen, Füllständer, Brust- und Sturzriegel,
    Streben in den Eckfeldern (zur Ecke ansteigend) und auf Wunsch Andreaskreuze in der Brüstung.
    `schief` (Meter): Füllständer stehen bis zu so viel schräg – für alte, krumme Häuser."""
    holz = st["Fachwerk"]
    zufall = random.Random(saat)
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
            n = math.ceil((b - a) / feld) - 1
            voll += [a + (b - a) * k / (n + 1) for k in range(1, n + 1)]
    voll.append(staender[-1])
    felder = [(a, b, oeffnung_bei((a + b) / 2)) for a, b in zip(voll, voll[1:])]

    fenster = [o for o in oeffnungen if o.art == "fenster"]
    v_brust = min((o.v for o in fenster), default=0.9)
    v_sturz = max((o.v + o.h for o in fenster), default=None)

    balken(netz, M, -h, B / 2, h, B / 2, holz, vor=VOR_SCHWELLE, enden=True)            # Schwelle
    balken(netz, M, -h, H - B / 2, h, H - B / 2, holz, vor=VOR_SCHWELLE, enden=True)    # Rähm
    for u in voll:
        krumm = zufall.uniform(-schief, schief) if schief and u not in staender else 0.0
        balken(netz, M, u - krumm, B, u + krumm, H - B, holz)

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


def fenster(netz, M, o, st, winkel=32, kreuz="Fachwerk", winkel_rechts=None):
    """Glas, Fensterkreuz und zwei halb geöffnete Läden in der Hausfarbe.
    `kreuz`: Material des Fensterkreuzes oder None (Luke ohne Kreuz)."""
    y = -0.006
    netz.vieleck([M @ Vector(p) for p in ((o.u - o.b / 2, y, o.v), (o.u + o.b / 2, y, o.v),
                                          (o.u + o.b / 2, y, o.v + o.h), (o.u - o.b / 2, y, o.v + o.h))], st[scheibe(M, o)])
    y, d, vk = -0.02, 0.025, o.v + o.h * 0.58       # Fensterkreuz: flache Leisten vor dem Glas
    for ecken in (((o.u - d, o.v), (o.u + d, o.v), (o.u + d, o.v + o.h), (o.u - d, o.v + o.h)),
                  ((o.u - o.b / 2, vk - d), (o.u + o.b / 2, vk - d), (o.u + o.b / 2, vk + d), (o.u - o.b / 2, vk + d))):
        if kreuz:
            netz.vieleck([M @ Vector((u, y, v)) for u, v in ecken], st[kreuz])
    if not o.laeden:
        return
    breite = o.b / 2
    for s in (-1, 1):
        w = winkel_rechts if (s > 0 and winkel_rechts is not None) else winkel
        dreh = -w if s > 0 else 180 + w
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


def reihenkante(st, stufe, stoff="Ziegelkante"):
    """Für Netz.prisma: kurze Kanten (die Stöße der Reihen) bekommen die dunkle Kante."""
    return lambda p, q: st[stoff] if (q - p).length < max(stufe, KANTE) * 1.2 else None


# Dachdeckungen: Material der Fläche, der Reihenkante, des Firsts; Reihenbreite, Stufe, Dicke
ZIEGEL = dict(deckung="Dachziegel", kante="Ziegelkante", first="Firstziegel", reihe=0.45, stufe=0.06, dicke=0.22)
STROH = dict(deckung="Stroh", kante="Strohkante", first="Firstsoden", reihe=0.7, stufe=0.09, dicke=0.38)
SCHIEFER = dict(deckung="Schiefer", kante="Schieferkante", first="Firstblech", reihe=0.32, stufe=0.04, dicke=0.18)
SCHINDEL = dict(deckung="Schindel", kante="Schindelkante", first="Holz alt", reihe=0.28, stufe=0.035, dicke=0.16)


def satteldach(netz, st, x0, x1, y_vorn, y_hinten, z_traufe, neigung, traufe=0.35, ortgang=0.35,
               dicke=0.22, reihe=0.45, stufe=0.06, deckung="Dachziegel", kante="Ziegelkante", first="Firstziegel",
               first_dicke=0.24, enden="Fachwerk", matrix=None):
    """Satteldach mit First entlang X über der Wand von y_vorn bis y_hinten, Wandoberkante z_traufe.
    Alle Maße im lokalen System; `matrix` setzt es in die Welt (z. B. gedreht für einen Giebel zur Gasse).
    Stirnseiten in `enden` (Holz wirkt wie ein Windbrett). Gibt Maße für weitere Teile zurück."""
    t = matrix if matrix is not None else Matrix.Identity(4)
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
    netz.prisma(profil, x0 - ortgang, x1 + ortgang, st[deckung], m_enden=st[enden], kante=reihenkante(st, stufe, kante),
                matrix=t)
    # First: über Eck gestellter Balken
    firstbalken = (t @ Matrix.Translation(((x0 + x1) / 2, y_first, oben_first.y + 0.02 + (first_dicke - 0.24) * 0.3))
                   @ Matrix.Rotation(math.radians(45), 4, "X"))
    netz.quader((0, 0, 0), (x1 - x0 + 2 * ortgang + 0.06, first_dicke, first_dicke), st[first], firstbalken)

    def oben_vorn(y):
        """Höhe der Dachoberfläche vorn bei y (ohne Zahnstufe)."""
        return z_traufe + (y - y_vorn) * tan + dicke / cos

    def oben_hinten(y):
        return z_traufe + (y_hinten - y) * tan + dicke / cos

    return {"y_first": y_first, "z_first": oben_first.y, "oben_vorn": oben_vorn, "oben_hinten": oben_hinten,
            "tan": tan, "dicke": dicke, "traufe_vorn": traufe_vorn, "traufe_hinten": traufe_hinten,
            "richtung_vorn": Vector((cos, sin)), "normale_vorn": Vector((-sin, cos)),
            "richtung_hinten": Vector((-cos, sin)), "normale_hinten": Vector((sin, cos)), "matrix": t,
            "deckung": deckung, "kante": kante}


# ─────────────────────────────────────────────────────────────────────────────
#  Teile auf dem Dach: Gaube, Loch (Leerstand)
# ─────────────────────────────────────────────────────────────────────────────

def gaube(netz, st, dach, oeffnungen, x0, x1, y_front, neigung=17, hoehe=1.45, art="fachwerk", koerper=None):
    """Schleppgaube auf der vorderen Dachfläche (lokal, wie das Dach): Putzkörper, Front mit Fenster,
    flaches Dach mit derselben Deckung. `art`: "fachwerk" (Ständer und Riegel) oder "stein" (Gewände).
    `koerper`: Material des Körpers statt Putz bzw. Haustein (z. B. "Bretter")."""
    t = dach["matrix"]
    tan_g = math.tan(math.radians(neigung))
    z_fuss = dach["oben_vorn"](y_front)
    z_kopf = z_fuss + hoehe
    tiefe = hoehe / (dach["tan"] - tan_g)              # bis die Gaube das Hauptdach trifft
    y_hinten = y_front + tiefe
    profil = [(y_front, z_fuss - 0.3), (y_front, z_kopf), (y_hinten, z_kopf + tiefe * tan_g), (y_hinten, z_kopf + tiefe * tan_g - 0.35)]
    netz.prisma(profil, x0, x1, st[koerper or ("Putz" if art == "fachwerk" else "Haustein")], matrix=t)
    M = t @ wandmatrix(((x0 + x1) / 2, y_front, z_fuss), "vorne")
    o = Oeffnung("fenster", 0, 0.4, 0.8 if art == "fachwerk" else 0.7, 0.72 if art == "fachwerk" else 0.8)
    if art == "fachwerk":
        fachwerk(netz, M, x1 - x0, hoehe, [o], st, eckstreben=False)
        fenster(netz, M, o, st)
    else:
        gewaende(netz, M, o, st)
    oeffnungen.append((M, o))
    deckung = dach["deckung"]
    richtung = Vector((math.cos(math.radians(neigung)), math.sin(math.radians(neigung))))
    normale = Vector((-richtung.y, richtung.x))
    ueber = 0.3
    unten = Vector((y_front, z_kopf)) - richtung * ueber
    laenge = (tiefe + ueber + 0.25) / richtung.x
    ende = unten + richtung * laenge
    oben = zaehne(unten, richtung, normale, laenge, 0.12, reihe=0.4, stufe=0.04)
    profil = [tuple(unten), tuple(ende)] + [tuple(p) for p in reversed(oben)]
    netz.prisma(profil, x0 - 0.18, x1 + 0.18, st[deckung], m_enden=st["Fachwerk"],
                kante=reihenkante(st, 0.04, dach["kante"]), matrix=t)


def dachloch(netz, st, dach, seite, x, s, r, stoff="Loch"):
    """Ausgefranster Fleck auf einer Dachfläche: `seite` "vorn"/"hinten", x entlang des Firsts,
    s = Abstand von der Traufe entlang der Fläche, r = Größe. Leerstand: dunkles Loch mit zwei Latten.
    Mit stoff="Moos" ein Moosfleck (ohne Latten)."""
    t = dach["matrix"]
    start, rich, nor = dach["traufe_" + seite], dach["richtung_" + seite], dach["normale_" + seite]
    hoehe = dach["dicke"] + 0.07 + (0.04 if stoff == "Moos" else 0)

    def punkt(dx, ds, mehr=0.0):
        p = start + rich * (s + ds) + nor * (hoehe + mehr)
        return t @ Vector((x + dx, p.x, p.y))

    zacken = [1.0, 0.65, 0.95, 0.7, 1.05, 0.6, 0.9, 0.75]
    rand = [punkt(r * z * math.cos(k * math.pi / 4), r * 0.8 * z * math.sin(k * math.pi / 4)) for k, z in enumerate(zacken)]
    normale = (t.to_3x3() @ Vector((0, nor.x, nor.y))).normalized()
    netz.vieleck_nach(rand, normale, st[stoff])
    if stoff == "Loch":
        for ds in (-0.18 * r / 0.4, 0.2 * r / 0.4):
            netz.stab(punkt(-r * 0.95, ds, 0.03), punkt(r * 0.95, ds, 0.03), 0.06, 0.03, st["Holz"], quer=normale)


# ─────────────────────────────────────────────────────────────────────────────
#  Kleinteile am Haus
# ─────────────────────────────────────────────────────────────────────────────

def fass(netz, st, boden, r=0.33, h=0.95, reifen=(0.2, 0.75), stoff="Holz"):
    """Fass oder Hackklotz: Achteck aus Holz, auf Wunsch mit Eisenreifen."""
    def ring(radius, z0, z1, m):
        profil = [(radius * math.cos(math.radians(22.5 + 45 * k)), radius * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
        dreh = Matrix.Translation(boden) @ Matrix.Rotation(math.radians(-90), 4, "Y")   # Prisma-x → Welt-z
        netz.prisma(profil, z0, z1, m, matrix=dreh)
    ring(r, 0, h, st[stoff])
    for z in reifen:
        ring(r + 0.015, z, z + 0.06, st["Eisen"])


def buschel(netz, st, fuss, groesse=0.35, stoff="Gras"):
    """Unkraut oder Grasbüschel: zwei gekreuzte, spitze Dreiecke."""
    for w in (20, 110):
        d = Vector((math.cos(math.radians(w)), math.sin(math.radians(w)), 0)) * groesse / 2
        spitze = fuss + Vector((0, 0, groesse * 1.2))
        n = d.cross(Vector((0, 0, 1)))
        netz.vieleck_nach([fuss - d, fuss + d, spitze], n, st[stoff])
        netz.vieleck_nach([fuss - d, fuss + d, spitze], -n, st[stoff])


SCHILD = [(-0.34, 0.42), (0.34, 0.42), (0.34, -0.02), (0.0, -0.44), (-0.34, -0.02)]


def wappen(netz, M, u, v, st, groesse=1.0, rahmen="Fachwerk"):
    """Wappenschild in der Hausfarbe auf einer Wand, mit Rahmen und Eisenhaken."""
    g = groesse
    platte(netz, M, [(u + x * 1.16 * g, v + y * 1.12 * g) for x, y in SCHILD], -0.085, -0.05, st[rahmen])
    platte(netz, M, [(u + x * g, v + y * g) for x, y in SCHILD], -0.11, -0.08, st["Hausfarbe"])
    balken(netz, M, u - 0.24 * g, v + 0.52 * g, u + 0.24 * g, v + 0.52 * g, st["Eisen"], vor=0.12, breite=0.05)


def fahne(netz, st, fuss, hoehe=2.7, richtung=-1):
    """Fahnenstange mit Schwalbenschwanz-Tuch in der Hausfarbe. Das Tuch weht nach `richtung` (±X)."""
    x, y, z = fuss
    netz.stab((x, y, z), (x, y, z + hoehe), 0.08, 0.08, st["Holz"])
    netz.quader((x, y, z + hoehe + 0.05), (0.14, 0.14, 0.14), st["Eisen"])
    tuch = [(0, 0), (1.25, 0.05), (0.98, 0.38), (1.25, 0.72), (0, 0.72)]
    dreh = Matrix.Rotation(math.radians(90 if richtung < 0 else -90), 4, "Z")
    netz.prisma(tuch, -0.015, 0.015, st["Hausfarbe"],
                matrix=Matrix.Translation((x + 0.04 * richtung, y, z + hoehe - 0.85)) @ dreh)


def baugeruest(netz, st, x0, x1, y_innen, y_aussen, ebenen=(2.15, 4.25), hoehe_innen=5.35, hoehe_aussen=6.3, felder=3):
    """Stangengerüst vor der Gassenseite: zwei Reihen Stangen, Längs- und Querriegel, Bohlen,
    Geländer, Kreuzstreben, Leiter und ein paar Balken am Boden."""
    holz = st["Geruestholz"]
    xs = [x0 + (x1 - x0) * i / felder for i in range(felder + 1)]
    for x in xs:
        netz.stab((x, y_aussen, 0), (x, y_aussen, hoehe_aussen), 0.13, 0.13, holz)
        netz.stab((x, y_innen, 0), (x, y_innen, hoehe_innen), 0.13, 0.13, holz)
    for z in ebenen:
        for y in (y_innen, y_aussen):
            netz.stab((x0 - 0.15, y, z), (x1 + 0.15, y, z), 0.1, 0.1, holz)
        for x in xs:
            netz.stab((x, y_innen + 0.1, z + 0.08), (x, y_aussen - 0.1, z + 0.08), 0.09, 0.09, holz, quer=(1, 0, 0))
        tief = y_innen - y_aussen
        for k in range(3):
            y = y_aussen + tief * (k + 0.5) / 3
            netz.quader(((x0 + x1) / 2, y, z + 0.15), (x1 - x0 + 0.1, tief / 3 - 0.03, 0.05), holz)
        netz.stab((x0 - 0.1, y_aussen - 0.02, z + 1.0), (x1 + 0.1, y_aussen - 0.02, z + 1.0), 0.08, 0.08, holz)
    oben = ebenen[-1]
    netz.stab((x0, y_aussen - 0.08, 0.2), (xs[-2], y_aussen - 0.08, oben), 0.1, 0.06, holz)
    netz.stab((xs[1], y_aussen - 0.12, 0.2), (x1, y_aussen - 0.12, oben), 0.1, 0.06, holz)
    fuss, kopf = (x1 - 0.45, y_aussen - 0.38, 0), (x1 - 0.45, y_aussen - 0.12, ebenen[0] + 0.9)
    for dx in (-0.22, 0.22):
        netz.stab((fuss[0] + dx, fuss[1], fuss[2]), (kopf[0] + dx, kopf[1], kopf[2]), 0.07, 0.05, holz)
    sprossen = int(kopf[2] / 0.32)
    for k in range(1, sprossen):
        p = Vector(fuss).lerp(Vector(kopf), k / sprossen)
        netz.stab((p.x - 0.22, p.y, p.z), (p.x + 0.22, p.y, p.z), 0.04, 0.04, holz)
    for k in range(3):
        netz.quader((x0 + 1.2, y_aussen - 0.36 + 0.1 * k, 0.06 + 0.11 * (k == 1)), (2.2, 0.12, 0.12), holz)


def achteck(netz, st, boden, r, z0, z1, stoff="Sandstein", n=8):
    """Stehendes n-Eck von z0 bis z1 über `boden` (Säulenschaft, Becken, Mühlstein)."""
    profil = [(r * math.cos(math.radians(180 / n + 360 / n * k)), r * math.sin(math.radians(180 / n + 360 / n * k)))
              for k in range(n)]
    dreh = Matrix.Translation(Vector(boden)) @ Matrix.Rotation(math.radians(-90), 4, "Y")   # Prisma-x → Welt-z
    netz.prisma(profil, z0, z1, st[stoff] if isinstance(stoff, str) else stoff, matrix=dreh)


def rund_oben(netz, st, mitte, r, z, stoff="Loch", n=8):
    """Flaches n-Eck nach oben (Loch im Mühlstein, Wasser oder Schatten im Becken)."""
    netz.vieleck_nach([(mitte[0] + r * math.cos(math.radians(180 / n + 360 / n * k)),
                        mitte[1] + r * math.sin(math.radians(180 / n + 360 / n * k)), z) for k in range(n)],
                      (0, 0, 1), st[stoff])


def pultdach(netz, st, x0, x1, y0, y1, z_hoch, z_tief, stoff="Holz alt", entlang="x"):
    """Einfaches Pultdach aus Brettern mit Fugen; fällt entlang x (von x0 hoch nach x1 tief)."""
    neig = math.atan2(z_hoch - z_tief, x1 - x0)
    laenge = (x1 - x0 + 0.3) / math.cos(neig)
    mitte = Vector(((x0 + x1 + 0.3) / 2, (y0 + y1) / 2, (z_hoch + z_tief) / 2 + 0.05 - 0.15 * math.tan(neig)))
    t = Matrix.Translation(mitte) @ Matrix.Rotation(neig, 4, "Y")
    netz.quader((0, 0, 0), (laenge, y1 - y0 + 0.4, 0.08), st[stoff], t)
    y = -(y1 - y0 + 0.4) / 2 + 0.32
    while y < (y1 - y0 + 0.4) / 2 - 0.1:
        netz.vieleck([t @ Vector(p) for p in ((-laenge / 2, y - 0.02, 0.045), (laenge / 2, y - 0.02, 0.045),
                                              (laenge / 2, y + 0.02, 0.045), (-laenge / 2, y + 0.02, 0.045))], st["Fachwerk"])
        y += 0.32


def tor_fluegel(netz, st, M, o):
    """Zweiflügeliges Brettertor in der Hausfarbe mit Z-Streben und Eisenbändern."""
    for s in (-1, 1):
        u = o.u + s * o.b / 4
        netz.quader((u, -0.01, o.v + o.h / 2), (o.b / 2 - 0.03, 0.04, o.h), st["Hausfarbe"], M, ohne=("+y",))
        for f in (0.15, 0.85):
            netz.quader((u, -0.04, o.v + o.h * f), (o.b / 2 - 0.12, 0.03, 0.12), st["Fachwerk"], M, ohne=("+y",))
        netz.stab(M @ Vector((u - s * (o.b / 4 - 0.12), -0.045, o.v + o.h * 0.15)),
                  M @ Vector((u + s * (o.b / 4 - 0.12), -0.045, o.v + o.h * 0.85)), 0.11, 0.03, st["Fachwerk"],
                  quer=M.to_3x3() @ Vector((0, 1, 0)), ohne=("+y",))
        for f in (0.3, 0.7):
            netz.quader((o.u + s * 0.25, -0.06, o.v + o.h * f), (0.4, 0.02, 0.06), st["Eisen"], M, ohne=("+y",))


def pferd(netz, st, ort, winkel):
    """Schlichtes Zugpferd aus Quadern und Stäben, Blick in Richtung `winkel` (Grad, von +x gegen den Uhrzeigersinn)."""
    M = Matrix.Translation(Vector(ort)) @ Matrix.Rotation(math.radians(winkel), 4, "Z")
    fell, dunkel = st["Leder"], st["Kohle"]
    netz.quader((0, 0, 0.95), (1.0, 0.36, 0.42), fell, matrix=M)
    for x in (-0.38, 0.38):
        for y in (-0.12, 0.12):
            netz.stab((x, y, 0), (x, y, 0.8), 0.1, 0.1, fell, matrix=M)
    netz.stab((0.4, 0, 1.0), (0.68, 0, 1.45), 0.2, 0.16, fell, matrix=M)            # Hals
    netz.quader((0.82, 0, 1.36), (0.4, 0.16, 0.18), fell, matrix=M)                 # Kopf
    netz.stab((0.4, 0, 1.2), (0.64, 0, 1.56), 0.05, 0.06, dunkel, matrix=M)         # Mähne
    netz.stab((-0.5, 0, 1.12), (-0.6, 0, 0.62), 0.06, 0.08, dunkel, matrix=M)       # Schweif


def steingiebel(netz, st, M, L, hoch, oeffnungen, stoff="Bruchstein", mit_fenster=True):
    """Giebeldreieck aus Mauerwerk mit Lagerfugen (je Höhe so breit wie der Giebel) und einem Steinfenster."""
    halb = L / 2
    platte(netz, M, [(-halb, 0), (halb, 0), (0, hoch)], 0, 0.4, st[stoff])
    v = 0.42
    while v < hoch - 0.4:
        b = halb * (1 - (v + 0.014) / hoch) - 0.03
        netz.vieleck([M @ Vector(p) for p in ((-b, -0.003, v - 0.014), (b, -0.003, v - 0.014),
                                               (b, -0.003, v + 0.014), (-b, -0.003, v + 0.014))], st["Fuge"])
        v += 0.42
    if mit_fenster and hoch > 2.2:
        o = Oeffnung("fenster", 0.0, 0.6, 0.6, 0.85, laeden=False)
        gewaende(netz, M, o, st, kreuz=False)
        oeffnungen.append((M, o))


def treppengiebel(netz, st, M, halb, z_dach0, tan, oeffnungen, fenster, stufen=5, oben_halb=0.45, dicke=0.45,
                  stoff="Haustein"):
    """Treppengiebel über einer Wand der halben Breite `halb`: Mauerscheibe mit Stufen, die über das Dach ragen,
    Sandsteinplatten auf jeder Stufe, Lagerfugen und Fenster (`fenster`: Liste von Öffnungen).
    `z_dach0`: Höhe der Dachoberfläche an der Wandkante über dem Fuß des Giebels.
    Gibt die Höhe des obersten Blocks zurück (für die Fahne)."""
    def dach_bei(a):                                     # Höhe der Dachoberfläche bei |u| = a
        return z_dach0 + (halb - a) * tan

    grenzen = [halb - k * (halb - oben_halb) / stufen for k in range(stufen + 1)]   # von außen nach innen
    hoehen = [dach_bei(grenzen[k + 1]) + 0.35 for k in range(stufen)]
    spitze = dach_bei(0) + 0.75
    rechts = [(halb, 0)]
    for k in range(stufen):
        rechts += [(grenzen[k], hoehen[k]), (grenzen[k + 1], hoehen[k])]
    rechts += [(oben_halb, spitze)]
    umriss = rechts + [(-u, v) for u, v in reversed(rechts)]
    platte(netz, M, umriss, 0, dicke, st[stoff])
    for k in range(stufen):
        b = grenzen[k] - grenzen[k + 1]
        for s in (-1, 1):
            netz.quader((s * (grenzen[k] + grenzen[k + 1]) / 2, dicke / 2, hoehen[k] + 0.05),
                        (b + 0.12, dicke + 0.12, 0.1), st["Sandstein"], M)
    netz.quader((0, dicke / 2, spitze + 0.06), (2 * oben_halb + 0.14, dicke + 0.14, 0.12), st["Sandstein"], M)
    v = 0.42
    while v < spitze - 0.1:
        breite = max([g for g, h in zip(grenzen, hoehen + [spitze]) if h > v + 0.05] + [oben_halb])
        if v > hoehen[-1]:
            breite = oben_halb
        netz.vieleck([M @ Vector(p) for p in ((-breite + 0.01, -0.003, v - 0.014), (breite - 0.01, -0.003, v - 0.014),
                                               (breite - 0.01, -0.003, v + 0.014), (-breite + 0.01, -0.003, v + 0.014))],
                     st["Fuge"])
        v += 0.42
    for o in fenster:
        gewaende(netz, M, o, st)
        oeffnungen.append((M, o))
    return spitze + 0.12


def turmhelm(netz, st, mitte, r, z0, hoehe, stoff="Schiefer", n=8):
    """Spitzer Turmhelm: n-eckige Pyramide über einem Turm (Umkreis r, Fuß bei z0), mit goldenem Knauf und Eisenspitze."""
    mitte = Vector(mitte)
    spitze = mitte + Vector((0, 0, z0 + hoehe))
    ecken = [mitte + Vector((r * math.cos(math.radians(180 / n + 360 / n * k)), r * math.sin(math.radians(180 / n + 360 / n * k)),
                             z0 - 0.05)) for k in range(n)]
    for k in range(n):
        a, b = ecken[k], ecken[(k + 1) % n]
        netz.vieleck_nach([a, b, spitze], (a + b) / 2 - (mitte + Vector((0, 0, z0 + 0.6))), st[stoff])
    achteck(netz, st, spitze - Vector((0, 0, 0.35)), 0.12, 0.0, 0.25, "Gold")
    netz.stab(spitze - Vector((0, 0, 0.1)), spitze + Vector((0, 0, 0.6)), 0.04, 0.04, st["Eisen"])


def grabstein(netz, st, fuss, dreh=0.0, breite=0.6, hoehe=1.0, dicke=0.14, form="rund", stoff="Sandstein"):
    """Grabstein auf einem Sockel, die Schauseite (mit zwei Schriftzeilen) zur Gasse. `form`: "rund" oder "giebel"."""
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh - 90), 4, "Z")   # Prisma-x → zur Gasse
    netz.quader((0, 0, 0.06), (dicke + 0.16, breite + 0.16, 0.12), st["Bruchstein dunkel"], t)
    b = breite / 2
    if form == "rund":
        profil = [(-b, 0.12), (b, 0.12)] + [(b * math.cos(math.radians(30 * k)), 0.12 + hoehe - b + b * math.sin(math.radians(30 * k)))
                                            for k in range(7)]
    else:
        profil = [(-b, 0.12), (b, 0.12), (b, 0.12 + hoehe - 0.22), (0, 0.12 + hoehe), (-b, 0.12 + hoehe - 0.22)]
    netz.prisma(profil, -dicke / 2, dicke / 2, st[stoff], matrix=t)
    n = t.to_3x3() @ Vector((1, 0, 0))
    for k in range(2):
        z = 0.12 + hoehe * (0.55 - 0.13 * k)
        w = b * (0.65 - 0.15 * k)
        netz.vieleck_nach([t @ Vector((dicke / 2 + 0.003, yy, zz)) for yy, zz in ((-w, z), (w, z), (w, z + 0.035), (-w, z + 0.035))],
                          n, st["Fuge"])


def trockenmauer(netz, st, punkte, hoehe=0.95, dicke=0.4):
    """Mauer aus Bruchstein um den Hof, mit Sandsteinabdeckung. An den Ecken stoßen die Abschnitte stumpf
    aneinander (jeder reicht bis zur Außenkante des vorigen), so decken sich keine Flächen."""
    pts = [Vector((x, y, 0)) for x, y in punkte]
    for i in range(len(pts) - 1):
        d = (pts[i + 1] - pts[i]).normalized()
        a = pts[i] + (d * dicke / 2 if i > 0 else Vector())
        b = pts[i + 1] + (d * dicke / 2 if i < len(pts) - 2 else Vector())
        netz.stab(a + Vector((0, 0, hoehe / 2)), b + Vector((0, 0, hoehe / 2)), dicke, hoehe, st["Bruchstein"], quer=(0, 0, 1))
        netz.stab(a + Vector((0, 0, hoehe + 0.05)), b + Vector((0, 0, hoehe + 0.05)), dicke + 0.08, 0.1, st["Sandstein"],
                  quer=(0, 0, 1), ohne=("-z",))


def linde(netz, st, fuss, hoehe=6.6, r=2.3, saat=0):
    """Linde: Stamm und eine Krone aus drei Achteckringen, die Flächen abwechselnd hell und dunkel."""
    m = Vector(fuss)
    achteck(netz, st, m, 0.24, 0.0, hoehe * 0.45, "Holz")
    zufall = random.Random(saat)
    ringe = [(0.55, hoehe * 0.36), (1.0, hoehe * 0.55), (0.82, hoehe * 0.78)]
    punkte = []
    for i, (f, z) in enumerate(ringe):
        punkte.append([m + Vector((r * f * zufall.uniform(0.9, 1.08) * math.cos(math.radians(22.5 * i + 45 * k)),
                                   r * f * zufall.uniform(0.9, 1.08) * math.sin(math.radians(22.5 * i + 45 * k)), z))
                       for k in range(8)])
    gipfel = m + Vector((0, 0, hoehe))
    mitte_k = m + Vector((0, 0, hoehe * 0.58))
    for i in range(len(punkte) - 1):
        for k in range(8):
            a, b = punkte[i][k], punkte[i][(k + 1) % 8]
            c, d = punkte[i + 1][(k + 1) % 8], punkte[i + 1][k]
            stoff = st["Laub" if (k + i) % 2 else "Laub hell"]
            netz.vieleck_nach([a, b, c], (a + b + c) / 3 - mitte_k, stoff)
            netz.vieleck_nach([a, c, d], (a + c + d) / 3 - mitte_k, stoff)
    for k in range(8):
        a, b = punkte[-1][k], punkte[-1][(k + 1) % 8]
        netz.vieleck_nach([a, b, gipfel], (a + b) / 2 - mitte_k, st["Laub hell" if k % 2 else "Laub"])
    unten = [p for p in punkte[0]]
    netz.vieleck_nach(unten, (0, 0, -1), st["Laub"])


def zaun(netz, st, punkte, hoehe=1.1, abstand=2.0, stoff="Holz alt"):
    """Bretterzaun entlang eines Linienzugs [(x, y), …]: Pfosten und zwei Latten je Abschnitt."""
    holz = st[stoff]
    for i, ((x0, y0), (x1, y1)) in enumerate(zip(punkte, punkte[1:])):
        laenge = math.hypot(x1 - x0, y1 - y0)
        n = max(1, round(laenge / abstand))
        for k in range(0 if i == 0 else 1, n + 1):     # Eckpfosten nur einmal (doppelte Flächen flimmern)
            x, y = x0 + (x1 - x0) * k / n, y0 + (y1 - y0) * k / n
            netz.quader((x, y, hoehe / 2 + 0.05), (0.12, 0.12, hoehe + 0.1), holz)
        for z in (hoehe * 0.35, hoehe * 0.85):
            netz.stab((x0, y0, z), (x1, y1, z), 0.1, 0.05, holz, quer=(0, 0, 1))


def fachwerkgiebel(netz, st, M, L, hoch, oeffnungen, laeden=False):
    """Giebeldreieck mit Fachwerk für beliebige Maße: Schwelle, Firstsäule, Kehlbalken, Ständer neben
    zwei Fenstern, Streben. Alle Maße wachsen mit der Giebelgröße, nichts ragt über das Dach."""
    holz = st["Fachwerk"]
    halb = L / 2
    platte(netz, M, [(-halb, 0), (halb, 0), (0, hoch)], 0, 0.3, st["Putz"])

    def rand(u):
        return hoch * (1 - abs(u) / halb)

    balken(netz, M, -halb + 0.05, B / 2, halb - 0.05, B / 2, holz, vor=VOR_SCHWELLE, enden=True)
    balken(netz, M, 0, B, 0, hoch - 0.35, holz)
    vk = hoch * 0.5
    breite_k = halb * (1 - (vk + B) / hoch) - 0.05
    balken(netz, M, -breite_k, vk + B / 2, breite_k, vk + B / 2, holz, vor=VOR_SCHWELLE)
    fb, fv = 0.6, 0.45
    fh = min(0.95, vk - fv - 0.25)
    fu = halb * 0.33
    for s in (-1, 1):
        for u in (fu - fb / 2 - B / 2, fu + fb / 2 + B / 2):
            balken(netz, M, s * u, B, s * u, min(vk, rand(u + B / 2) - 0.1), holz)
        for v in (fv - B / 2, fv + fh + B / 2):
            balken(netz, M, s * 0.07, v, s * (fu + fb / 2 + B), v, holz, vor=VOR_RIEGEL)
        a = halb * 0.8
        balken(netz, M, s * (fu + fb / 2 + B), B, s * a, max(B + 0.2, rand(a) - 0.2), holz, vor=VOR_STREBE)
        if hoch - 0.8 > vk + B + 0.3:
            balken(netz, M, s * halb * 0.3, vk + B, s * 0.08, hoch - 0.8, holz, vor=VOR_STREBE)
        o = Oeffnung("fenster", s * fu, fv, fb, fh, laeden=laeden)
        fenster(netz, M, o, st)
        oeffnungen.append((M, o))


def dachreiter(netz, st, first, spitze="Gold", koerper="Putz", ecken="Fachwerk"):
    """Dachreiter auf dem First mit vierseitiger Spitze (wie bisher im Spiel ab Stufe 3): golden beim Schmied,
    kupfergrün beim Tischler, Schiefer beim Steinmetz – `spitze` ist das Material. `koerper` und `ecken`:
    Putz mit Fachwerkständern oder Mauerwerk mit Eckquadern."""
    x, y, z = first
    netz.quader((x, y, z + 0.6), (1.3, 1.3, 1.6), st[koerper])
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader((x + sx * 0.6, y + sy * 0.6, z + 0.6), (0.14, 0.14, 1.6), st[ecken])
    for s in (-1, 1):
        netz.vieleck_nach([(x - 0.3, y + s * 0.66, z + 0.7), (x + 0.3, y + s * 0.66, z + 0.7),
                           (x + 0.3, y + s * 0.66, z + 1.25), (x - 0.3, y + s * 0.66, z + 1.25)], (0, s, 0), st["Loch"])
    gipfel = Vector((x, y, z + 3.4))
    ecken = [Vector((x + sx * 0.85, y + sy * 0.85, z + 1.4)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        j = (i + 1) % 4
        netz.vieleck_nach([ecken[i], ecken[j], gipfel], (ecken[i] + ecken[j]) / 2 - Vector((x, y, z + 1.0)), st[spitze])
    netz.quader((x, y, z + 3.6), (0.1, 0.1, 0.45), st[spitze])


# ─────────────────────────────────────────────────────────────────────────────
#  Mauerwerk: Eckquader, Gesimse, Lagerfugen, Gewände, Rundbogenportal
# ─────────────────────────────────────────────────────────────────────────────

def eckquader(netz, st, x, y, sx, sy, z0, z1, hoehe=0.55, lang=0.62, kurz=0.34, vor=0.035, stoff="Sandstein"):
    """Eckquader an der Hausecke (x, y); sx, sy = ±1 zeigen nach außen. Lang und kurz im Wechsel,
    mit einer schmalen Fuge dazwischen. Die Innenseiten fehlen (sie liegen in der Wand)."""
    n = max(1, round((z1 - z0) / hoehe))
    h = (z1 - z0) / n
    for k in range(n):
        lx, ly = (lang, kurz) if k % 2 == 0 else (kurz, lang)
        mitte = (x - sx * (lx - vor) / 2, y - sy * (ly - vor) / 2, z0 + h * k + (h - 0.03) / 2)
        netz.quader(mitte, (lx + vor, ly + vor, h - 0.03), st[stoff],
                    ohne=("-x" if sx > 0 else "+x", "-y" if sy > 0 else "+y"))


def gesims(netz, st, x0, x1, y0, y1, z, hoehe=0.16, vor=0.07, stoff="Sandstein"):
    """Umlaufendes Gesims: ein Band rund um den Baukörper von (x0, y0) bis (x1, y1) bei Höhe z."""
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, z + hoehe / 2), (x1 - x0 + 2 * vor, y1 - y0 + 2 * vor, hoehe), st[stoff])


def fugen(netz, M, L, v0, v1, st, abstand=0.42):
    """Lagerfugen als dunkle Linien auf der Wandfläche: Mauerwerk aus Schichten, kaum Dreiecke.
    Fenster, Gewände und Glas liegen davor und decken sie ab."""
    v = v0 + abstand
    while v < v1 - 0.05:
        netz.vieleck([M @ Vector(p) for p in ((-L / 2, -0.003, v - 0.014), (L / 2, -0.003, v - 0.014),
                                               (L / 2, -0.003, v + 0.014), (-L / 2, -0.003, v + 0.014))], st["Fuge"])
        v += abstand


def gewaende(netz, M, o, st, kreuz=True, verdachung=False, glas=None, gitter=False, stein="Sandstein"):
    """Steinfenster: Glas, Gewände aus Sandstein (Laibung, Sturz, vorstehende Sohlbank),
    Fensterkreuz in der Hausfarbe (gestrichene Flügel), auf Wunsch eine Verdachung darüber.
    `glas`: Material der Scheibe (z. B. "Glut" für Feuerschein), `gitter`: Eisenstäbe statt Kreuz."""
    sand = st[stein]
    g, vor = 0.16, 0.05
    y = -0.006
    netz.vieleck([M @ Vector(p) for p in ((o.u - o.b / 2, y, o.v), (o.u + o.b / 2, y, o.v),
                                          (o.u + o.b / 2, y, o.v + o.h), (o.u - o.b / 2, y, o.v + o.h))], st[glas or scheibe(M, o)])
    if gitter:
        for k in range(1, 4):
            u = o.u - o.b / 2 + o.b * k / 4
            balken(netz, M, u, o.v, u, o.v + o.h, st["Eisen"], vor=0.03, breite=0.035)
        balken(netz, M, o.u - o.b / 2, o.v + o.h * 0.5, o.u + o.b / 2, o.v + o.h * 0.5, st["Eisen"], vor=0.035, breite=0.035)
        kreuz = False
    for s in (-1, 1):
        netz.quader((o.u + s * (o.b / 2 + g / 2), (-vor + 0.02) / 2, o.v + o.h / 2), (g, vor + 0.02, o.h), sand, M,
                    ohne=("+y", "+z", "+x" if s > 0 else "-x"))
    netz.quader((o.u, (-vor + 0.02) / 2, o.v + o.h + g / 2), (o.b + 2 * g, vor + 0.02, g), sand, M, ohne=("+y", "+x", "-x"))
    netz.quader((o.u, (-vor - 0.05 + 0.02) / 2, o.v - 0.06), (o.b + 2 * g + 0.12, vor + 0.07, 0.12), sand, M, ohne=("+y",))
    if verdachung:
        netz.quader((o.u, (-vor - 0.08 + 0.02) / 2, o.v + o.h + g + 0.06), (o.b + 2 * g + 0.2, vor + 0.1, 0.12), sand, M,
                    ohne=("+y",))
    if kreuz:
        d, vk, yk = 0.03, o.v + o.h * 0.62, -0.02
        for ecken in (((o.u - d, o.v), (o.u + d, o.v), (o.u + d, o.v + o.h), (o.u - d, o.v + o.h)),
                      ((o.u - o.b / 2, vk - d), (o.u + o.b / 2, vk - d), (o.u + o.b / 2, vk + d), (o.u - o.b / 2, vk + d))):
            netz.vieleck([M @ Vector((u, yk, v)) for u, v in ecken], st["Hausfarbe"])


def portal(netz, M, o, st, teile=8):
    """Rundbogenportal: Tür in der Hausfarbe (Rechteck mit Halbkreis), Bogen aus Sandstein mit Schlussstein.
    o.h ist die Höhe bis zum Scheitel des Bogens."""
    r = o.b / 2
    kaempfer = o.v + o.h - r
    bogen = [(o.u + r * math.cos(math.pi * k / teile), kaempfer + r * math.sin(math.pi * k / teile)) for k in range(teile + 1)]
    platte(netz, M, [(o.u - r, o.v), (o.u + r, o.v)] + bogen, -0.01, 0.03, st["Hausfarbe"])
    for f in (0.25, 0.6):
        balken(netz, M, o.u - r * 0.85, o.v + (o.h - r) * f * 1.4, o.u + r * 0.85, o.v + (o.h - r) * f * 1.4, st["Eisen"],
               vor=0.03, breite=0.07)
    g = 0.2
    aussen = [(o.u + (r + g) * math.cos(math.pi * k / teile), kaempfer + (r + g) * math.sin(math.pi * k / teile))
              for k in range(teile + 1)]
    ring = [(o.u + r + g, o.v)] + aussen + [(o.u - r - g, o.v), (o.u - r, o.v)] + list(reversed(bogen)) + [(o.u + r, o.v)]
    platte(netz, M, ring, -0.06, 0.0, st["Sandstein"])
    netz.quader((o.u, -0.045, kaempfer + r + g * 0.45), (0.26, 0.1, g + 0.12), st["Sandstein"], M, ohne=("+y",))


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


def anker(name, punkt, drehung=0.0):
    """Ankerpunkt: ein leerer Knoten, den das Spiel nach dem Laden sucht (Name „Anker_…“).
    `punkt` liegt dort, wo eine Figur oder ein Karren ankommt (Höhe = Stufe davor), `drehung` (Grad um Z)
    so, dass die Blickrichtung ins Haus zeigt: In Blender ist das lokal +Y, im Spiel (Godot) −Z.
    Für eine Tür an der Gassenseite also 0, an der rechten Seite 90, hinten 180, links 270."""
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "SINGLE_ARROW"
    obj.location = Vector(punkt)
    obj.rotation_euler = (0, 0, math.radians(drehung))
    obj["anker"] = True
    bpy.context.scene.collection.objects.link(obj)
    return obj


def anker_bild(name, sichtbar):
    """Vorschau docs/modelle/<name>-anker.png: jeder Anker als Stab mit Pfeil am Boden (Personen magenta,
    Karren türkis); der Pfeil zeigt ins Haus. Die Markierungen werden danach wieder entfernt."""
    farben = {"Karren": _hilfsstoff("_Anker_Karren", (0.1, 0.85, 0.85))}
    tuer = _hilfsstoff("_Anker_Tuer", (0.95, 0.15, 0.75))
    marken = []
    for o in [o for o in bpy.context.scene.objects if o.get("anker")]:
        m = farben.get(o.name.split("_")[-1], tuer)
        netz = Netz("_Marke_" + o.name)
        t = o.matrix_world
        lang = 1.4 if "Karren" in o.name else 0.9
        # Pfeil vor dem Anker, die Spitze auf dem Ankerpunkt: zeigt ins Haus
        netz.vieleck([t @ Vector(p) for p in ((-0.4, -lang, 0.03), (0.4, -lang, 0.03), (0, 0, 0.03))], m, immer=True)
        netz.stab(t @ Vector((0, 0, 0)), t @ Vector((0, 0, 1.8)), 0.08, 0.08, m)
        netz.quader((0, 0, 1.85), (0.22, 0.22, 0.22), m, matrix=t)
        marken.append(netz.objekt())
    pfad = rendern(name + "-anker", lambda o: sichtbar(o) or o.name.startswith("_Marke_"))
    for o in marken:
        bpy.data.objects.remove(o)
    return pfad


def nachtbild(name, sichtbar, drehung=0):
    """Vorschau bei Nacht docs/modelle/<name>-nacht.png: Mondlicht, dunkler Himmel, Licht hinter den Fenstern
    und Glut der Essen an – so, wie das Spiel es abends schalten soll."""
    szene = bpy.context.scene
    sonne = bpy.data.objects["Sonne"].data
    hg = szene.world.node_tree.nodes["Background"].inputs
    alt = (sonne.energy, tuple(sonne.color), tuple(hg["Color"].default_value), hg["Strength"].default_value)
    sonne.energy, sonne.color = 0.35, (0.62, 0.70, 1.0)
    hg["Color"].default_value, hg["Strength"].default_value = (0.05, 0.07, 0.13, 1), 0.5
    an = []
    for name_stoff, (farbe, staerke) in NACHTLICHT.items():
        if name_stoff in _stoffe:
            p = _stoffe[name_stoff].node_tree.nodes["Principled BSDF"]
            p.inputs["Emission Color"].default_value = (*_linear(farbe), 1.0)
            p.inputs["Emission Strength"].default_value = staerke
            an.append(p)
    pfad = rendern(name + "-nacht", sichtbar, drehung)
    for p in an:
        p.inputs["Emission Strength"].default_value = 0.0
    sonne.energy, sonne.color = alt[0], alt[1]
    hg["Color"].default_value, hg["Strength"].default_value = alt[2], alt[3]
    return pfad


def deckungsgleich(objekte, grenze=0.004):
    """Sucht Flächen, die in derselben Ebene liegen, in dieselbe Richtung zeigen und sich überdecken.
    Im Spiel flimmern sie (Z-Fighting), in Cycles werden sie schwarz. Vergleicht die Umrisse als Rechtecke
    in der Ebene – schräge Flächen können also auch einmal fälschlich gemeldet werden.
    Gibt Liste [(Ort, Objekt A, Objekt B, Fläche)] zurück."""
    import collections
    eimer = collections.defaultdict(list)
    for o in objekte:
        if o.type != "MESH":
            continue
        for p in o.data.polygons:
            n = p.normal
            if p.area < grenze:
                continue
            # Achsen in der Ebene: waagrecht und „hoch“ (bei Wänden), sonst entlang X – so bleiben Umrisse eng
            u = n.cross(Vector((0, 0, 1))).normalized() if abs(n.z) < 0.9 else Vector((1, 0, 0))
            v = n.cross(u)
            punkte = [o.data.vertices[i].co for i in p.vertices]
            us, vs = [q.dot(u) for q in punkte], [q.dot(v) for q in punkte]
            schluessel = (round(n.x, 2), round(n.y, 2), round(n.z, 2), round(n.dot(p.center), 3))
            eimer[schluessel].append((o.name, p.center.copy(), (min(us), max(us), min(vs), max(vs))))
    funde = []
    for liste in eimer.values():
        for i in range(len(liste)):
            for j in range(i + 1, len(liste)):
                a, b = liste[i][2], liste[j][2]
                du = min(a[1], b[1]) - max(a[0], b[0])
                dv = min(a[3], b[3]) - max(a[2], b[2])
                if du > 0.01 and dv > 0.01 and du * dv > grenze:
                    funde.append((liste[i][1], liste[i][0], liste[j][0], du * dv))
    return funde


def bericht(name, objekte, einzeln=False):
    """Dreiecke je Objekt (so, wie sie im Export landen); Anker mit ihrer Lage.
    `einzeln`: jedes Objekt für sich auf Flimmern prüfen (Baukasten, dessen Teile alle im Nullpunkt liegen)."""
    print(f"\n{name}: Dreiecke")
    summe = 0
    for o in objekte:
        if o.type != "MESH":
            print(f"  {o.name:10s} Anker bei ({o.location.x:.2f}, {o.location.y:.2f}, {o.location.z:.2f})")
            continue
        o.data.calc_loop_triangles()
        n = len(o.data.loop_triangles)
        summe += n
        print(f"  {o.name:10s} {n:6d}")
    print(f"  {'gesamt':10s} {summe:6d}\n")
    # Deckungsgleiche Flächen: je sichtbarer Kombination (Haus mit genau einer Stufe bzw. Variante, Zustände dazu)
    feste = [o for o in objekte if o.type == "MESH" and not o.name.startswith(("Stufe", "Variante"))]
    stufen = [o for o in objekte if o.type == "MESH" and o.name.startswith(("Stufe", "Variante"))] or [None]
    funde = []
    if einzeln:
        for o in objekte:
            if o.type == "MESH":
                funde += deckungsgleich([o])
        stufen = []
    for stufe in stufen:
        funde += deckungsgleich(feste + ([stufe] if stufe else []))
    funde = list({(round(f[0].x, 2), round(f[0].y, 2), round(f[0].z, 2), f[1], f[2]): f for f in funde}.values())
    if funde:
        print(f"  WARNUNG: {len(funde)} deckungsgleiche Flächen (flimmern im Spiel), größte:")
        for ort, a, b, flaeche in sorted(funde, key=lambda f: -f[3])[:8]:
            print(f"    bei ({ort.x:.2f}, {ort.y:.2f}, {ort.z:.2f})  {a} / {b}  {flaeche:.2f} m²")
    else:
        print("  keine deckungsgleichen Flächen")


def vorschau_buehne(flaeche, ziel_hoehe=5.0, ausschnitt=17.0, schnell=False):
    """Boden, Gasse, Rahmen der belegten Fläche, Sonne, Himmel und die Spielkamera
    (orthografisch, 40° Neigung, diagonal). Nur fürs Bild, wird nicht exportiert."""
    szene = bpy.context.scene
    umgebung = Netz("_Umgebung")
    st = {n: _hilfsstoff(n, f) for n, f in (("_Boden", (0.33, 0.33, 0.25)), ("_Gasse", (0.36, 0.34, 0.31)),
                                            ("_Rahmen", (0.86, 0.80, 0.52)))}
    umgebung.quader((0, 0, -0.05), (120, 120, 0.1), st["_Boden"])
    if flaeche is not None:      # Gasse vor dem Haus und Rahmen der belegten Fläche
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


def nebeneinander(name, bilder, texte):
    """Mehrere Vorschaubilder (gleicher Ausschnitt) nebeneinander mit Beschriftung: docs/modelle/<name>.png.
    Schrift des Spiels (Alegreya, OFL); ohne Pillow bleibt es bei den Einzelbildern."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        return None
    teile = [Image.open(b).convert("RGB") for b in bilder]
    w, h = teile[0].size
    rand = int(w * 0.07)                       # links und rechts ist in jedem Bild nur Boden
    gesamt = Image.new("RGB", (len(teile) * (w - 2 * rand), h))
    for i, t in enumerate(teile):
        gesamt.paste(t.crop((rand, 0, w - rand, h)), (i * (w - 2 * rand), 0))
    zeichnen = ImageDraw.Draw(gesamt)
    try:
        schrift = ImageFont.truetype(os.path.join(WURZEL, "game", "assets", "schriften", "Alegreya-Variabel.ttf"), int(h / 30))
    except OSError:
        schrift = ImageFont.load_default()
    for i, text in enumerate(texte):
        zeichnen.text((int((i + 0.5) * (w - 2 * rand)), int(h * 0.96)), text, fill=(240, 236, 220), font=schrift, anchor="ms")
    pfad = os.path.join(VORSCHAU, name + ".png")
    gesamt.save(pfad, optimize=True)
    return pfad
