"""Gemeinsame Bausteine für die Blender-Skripte der Gebäudemodelle.

Jedes Modell entsteht aus einem Skript in diesem Ordner:
    blender -b --python tools/modelle/<name>.py        (Blender als Programm)
    python tools/modelle/<name>.py                     (Blender als Python-Modul „bpy“)
Es erzeugt game/assets/modelle/<name>.glb und Vorschaubilder in docs/modelle/.

Konventionen (siehe docs/bereiche/darstellung.md):
- Meter. Hier in Blender ist Z oben und die Vorderseite zeigt nach -Y;
  der glTF-Export dreht das auf Y oben und Vorderseite +Z, wie das Spiel es braucht.
- Ursprung = Mitte der belegten Fläche (inkl. 2 m Vorplatz) auf dem Boden.
- Die Hausfarbe (Tür, Läden, Fahne, Wappen) ist das Material „Hausfarbe“.
- Zustände sind eigene Objekte mit festen Namen („Leerstand“, „Geruest“, „Stufe1“ …).
  glTF kennt keine Sichtbarkeit, darum kommen sie sichtbar aus der Datei;
  das Spiel blendet sie nach dem Laden per Namen aus.

Geometrie wird nicht mit Blender-Operatoren gebaut, sondern als Listen von Ecken und
Flächen gesammelt (Klasse Bauteil) und am Ende in ein Mesh geschrieben. Das ist schnell,
braucht keinen Kontext und ergibt ein Objekt je Zustand statt hunderter Einzelknoten.
"""

import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

# ─────────────────────────────────────────────────────────────────────────────
#  Pfade und Szene
# ─────────────────────────────────────────────────────────────────────────────

WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
MODELLE = os.path.join(WURZEL, "game", "assets", "modelle")
VORSCHAU = os.path.join(WURZEL, "docs", "modelle")


def szene_leeren():
    """Leere Szene ohne Würfel, Kamera und Licht."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    szene = bpy.context.scene
    szene.unit_settings.system = "METRIC"
    szene.unit_settings.scale_length = 1.0
    return szene


# ─────────────────────────────────────────────────────────────────────────────
#  Materialien: schlicht, ohne Bildtexturen
# ─────────────────────────────────────────────────────────────────────────────

_materialien = {}


def material(name, farbe, rauheit=0.9, metall=0.0):
    """Ein Material mit fester Farbe (sRGB-Werte 0–1). Gleicher Name = gleiches Material."""
    if name in _materialien:
        return _materialien[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*_linear(farbe), 1.0)
    bsdf.inputs["Roughness"].default_value = rauheit
    bsdf.inputs["Metallic"].default_value = metall
    m.diffuse_color = (*_linear(farbe), 1.0)
    _materialien[name] = m
    return m


def _linear(farbe):
    """sRGB → linear, damit die Farben im Spiel so aussehen wie hier angegeben."""
    def k(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(k(c) for c in farbe)


# Gedeckte Farben des düsteren Reichs; die Werte lehnen sich an Stadtansicht.cs an.
FARBEN = {
    "Putz": (0.76, 0.70, 0.58),
    "Holz": (0.30, 0.21, 0.13),        # dunkle Eiche des Fachwerks
    "Bruchstein": (0.44, 0.42, 0.38),
    "Ziegel": (0.52, 0.26, 0.18),      # Biberschwanz
    "Schiefer": (0.28, 0.30, 0.34),
    "Glas": (0.16, 0.19, 0.24),
    "Eisen": (0.12, 0.12, 0.13),
    "Bretter": (0.40, 0.33, 0.24),     # vergraute Bretter des Leerstands
    "Geruestholz": (0.58, 0.46, 0.29), # frisches Holz
    "Hausfarbe": (0.15, 0.28, 0.60),   # Vorgabe: Spielerblau, das Spiel färbt je Haus um
}


def standardmaterialien():
    """Legt die üblichen Materialien an und gibt sie als Wörterbuch zurück."""
    m = {name: material(name, farbe) for name, farbe in FARBEN.items()}
    m["Glas"] = material("Glas", FARBEN["Glas"], rauheit=0.25, metall=0.3)
    m["Eisen"] = material("Eisen", FARBEN["Eisen"], rauheit=0.6, metall=0.5)
    return m


# ─────────────────────────────────────────────────────────────────────────────
#  Bauteil: sammelt Quader und Prismen und wird am Ende ein Objekt
# ─────────────────────────────────────────────────────────────────────────────

class Bauteil:
    """Sammelt Geometrie für ein Objekt. Jede Fläche kennt ihr Material."""

    def __init__(self, name):
        self.name = name
        self.ecken = []
        self.flaechen = []
        self.flaechen_material = []
        self.materialien = []

    def _materialnummer(self, m):
        if m not in self.materialien:
            self.materialien.append(m)
        return self.materialien.index(m)

    def _einfuegen(self, ecken, flaechen, m):
        start = len(self.ecken)
        self.ecken.extend(ecken)
        nummer = self._materialnummer(m)
        for f in flaechen:
            self.flaechen.append(tuple(start + i for i in f))
            self.flaechen_material.append(nummer)

    def quader(self, mitte, masse, m, matrix=None, dreh=None):
        """Quader um `mitte` (x, y, z) mit Kantenlängen `masse`.
        `dreh` = (x, y, z) in Grad um die eigene Mitte, `matrix` setzt das Ganze in die Welt."""
        hx, hy, hz = masse[0] / 2, masse[1] / 2, masse[2] / 2
        lokal = [Vector((sx * hx, sy * hy, sz * hz))
                 for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
        # Reihenfolge: 0 (-,-,-) 1 (+,-,-) 2 (-,+,-) 3 (+,+,-) 4 (-,-,+) 5 (+,-,+) 6 (-,+,+) 7 (+,+,+)
        flaechen = [(0, 2, 3, 1),   # unten
                    (4, 5, 7, 6),   # oben
                    (0, 1, 5, 4),   # -y
                    (2, 6, 7, 3),   # +y
                    (0, 4, 6, 2),   # -x
                    (1, 3, 7, 5)]   # +x
        t = Matrix.Translation(Vector(mitte))
        if dreh is not None:
            t = t @ _drehung(dreh)
        if matrix is not None:
            t = matrix @ t
        self._einfuegen([t @ v for v in lokal], flaechen, m)

    def prisma(self, umriss, dicke, m, matrix=None):
        """Prisma: Vieleck `umriss` [(u, v), …] in der lokalen XZ-Ebene, um `dicke` entlang
        +Y ausgezogen (von y=0 bis y=dicke). `matrix` setzt es in die Welt."""
        if _flaeche(umriss) < 0:
            umriss = list(reversed(umriss))
        n = len(umriss)
        # Vorderseite bei y=0 zeigt nach -Y: Umriss im Uhrzeigersinn von vorn gesehen → hier umdrehen
        vorn = [Vector((u, 0.0, v)) for u, v in umriss]
        hinten = [Vector((u, dicke, v)) for u, v in umriss]
        ecken = vorn + hinten
        flaechen = [tuple(range(n))]                               # vorn (Normale -Y)
        flaechen.append(tuple(n + i for i in reversed(range(n))))  # hinten (Normale +Y)
        for i in range(n):
            j = (i + 1) % n
            flaechen.append((j, i, n + i, n + j))
        if matrix is not None:
            ecken = [matrix @ v for v in ecken]
        self._einfuegen(ecken, flaechen, m)

    def dreiecke(self):
        return sum(len(f) - 2 for f in self.flaechen)

    def objekt(self, eltern=None):
        """Schreibt das Gesammelte in ein Mesh und hängt das Objekt in die Szene."""
        mesh = bpy.data.meshes.new(self.name)
        mesh.from_pydata([tuple(v) for v in self.ecken], [], self.flaechen)
        for p, nummer in zip(mesh.polygons, self.flaechen_material):
            p.material_index = nummer
            p.use_smooth = False
        for m in self.materialien:
            mesh.materials.append(m)
        mesh.validate(verbose=False)
        mesh.update()
        _normalen_nach_aussen(mesh)
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        if eltern is not None:
            obj.parent = eltern
        return obj


def _flaeche(umriss):
    """Vorzeichenbehaftete Fläche eines Vielecks (u, v): positiv = gegen den Uhrzeigersinn."""
    s = 0.0
    for i in range(len(umriss)):
        u1, v1 = umriss[i]
        u2, v2 = umriss[(i + 1) % len(umriss)]
        s += u1 * v2 - u2 * v1
    return s / 2


def _drehung(dreh):
    rx, ry, rz = (math.radians(a) for a in dreh)
    return Matrix.Rotation(rz, 4, "Z") @ Matrix.Rotation(ry, 4, "Y") @ Matrix.Rotation(rx, 4, "X")


def _normalen_nach_aussen(mesh):
    """Sicherheitsnetz: alle Flächen nach außen drehen (jedes Teil ist ein geschlossener Körper)."""
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def wandmatrix(mitte_unten, richtung):
    """Matrix für eine Wand: lokal u = entlang der Wand, v = Höhe (lokales Z), lokal -Y = nach außen.
    `richtung` ist die Außennormale: „vorne“ (-Y), „rechts“ (+X), „hinten“ (+Y), „links“ (-X)."""
    winkel = {"vorne": 0, "rechts": 90, "hinten": 180, "links": 270}[richtung]
    return Matrix.Translation(Vector(mitte_unten)) @ Matrix.Rotation(math.radians(winkel), 4, "Z")


# ─────────────────────────────────────────────────────────────────────────────
#  Fachwerkwand: Putzfeld mit Schwelle, Rähm, Ständern, Riegeln, Streben und Öffnungen
# ─────────────────────────────────────────────────────────────────────────────

BALKEN = 0.16       # Balkenbreite
BALKENTIEFE = 0.22  # Balkentiefe (ragt VORSPRUNG vor den Putz)
VORSPRUNG = 0.07


class Oeffnung:
    """Fenster oder Tür in einer Wand, in Wandkoordinaten: u = Mitte, v = Unterkante."""

    def __init__(self, art, u, v, b, h, laden=True, sprossen=True):
        self.art, self.u, self.v, self.b, self.h, self.laden, self.sprossen = art, u, v, b, h, laden, sprossen


def fachwerk(teil, matrix, breite, hoehe, oeffnungen, m, geschoss_schwelle=True, abstand=1.6, streben=True):
    """Fachwerk auf eine Wandfläche: Breite `breite` (u von -b/2 bis b/2), Höhe `hoehe` (v von 0).
    Die Putzfläche selbst baut der Aufrufer (ein großer Quader je Geschoss).
    Gibt die Liste der Öffnungen zurück (für Leerstand-Bretter)."""
    b = BALKEN
    y = -VORSPRUNG + BALKENTIEFE / 2   # Balkenmitte: Außenkante ragt VORSPRUNG vor den Putz (y < 0 = außen)

    def waag(u1, u2, v):
        teil.quader(((u1 + u2) / 2, y, v + b / 2), (u2 - u1, BALKENTIEFE, b), m["Holz"], matrix)

    def senk(u, v1, v2):
        teil.quader((u, y, (v1 + v2) / 2), (b, BALKENTIEFE, v2 - v1), m["Holz"], matrix)

    def strebe(u1, v1, u2, v2):
        laenge = math.hypot(u2 - u1, v2 - v1)
        winkel = math.degrees(math.atan2(v2 - v1, u2 - u1))
        teil.quader(((u1 + u2) / 2, y, (v1 + v2) / 2), (laenge, BALKENTIEFE, b), m["Holz"],
                    matrix, dreh=(0, -winkel, 0))

    halb = breite / 2
    # Schwelle und Rähm
    if geschoss_schwelle:
        waag(-halb, halb, 0)
    waag(-halb, halb, hoehe - b)
    # Ständer: Ecken, Öffnungsränder, Füllung
    staender = {-halb + b / 2, halb - b / 2}
    for o in oeffnungen:
        staender.add(o.u - o.b / 2 - b / 2)
        staender.add(o.u + o.b / 2 + b / 2)
    staender = sorted(staender)
    gefuellt = []
    for i in range(len(staender) - 1):
        u1, u2 = staender[i], staender[i + 1]
        gefuellt.append(u1)
        ist_oeffnung = any(abs((u1 + u2) / 2 - o.u) < o.b / 2 for o in oeffnungen)
        if not ist_oeffnung and u2 - u1 > abstand * 1.4:
            n = int((u2 - u1) / abstand)
            for k in range(1, n + 1):
                gefuellt.append(u1 + (u2 - u1) * k / (n + 1))
    gefuellt.append(staender[-1])
    gefuellt = sorted(set(round(u, 4) for u in gefuellt))
    for u in gefuellt:
        senk(u, 0, hoehe)
    # Riegel um Öffnungen (Brüstung und Sturz) und Streben in leeren Feldern
    for i in range(len(gefuellt) - 1):
        u1, u2 = gefuellt[i], gefuellt[i + 1]
        mitte = (u1 + u2) / 2
        o = next((o for o in oeffnungen if abs(mitte - o.u) < o.b / 2), None)
        if o is not None:
            if o.v > b:
                waag(u1, u2, o.v - b)
            if o.v + o.h + b < hoehe - b:
                waag(u1, u2, o.v + o.h)
        elif streben and u2 - u1 > 0.7:
            # Strebe lehnt zur näheren Hausecke (die klassischen „Männer“ des Fachwerks)
            rand = b * 0.9
            if mitte > 0:
                strebe(u1 + rand, b, u2 - rand, hoehe - b)
            else:
                strebe(u1 + rand, hoehe - b, u2 - rand, b)
        elif not streben and u2 - u1 > 0.7:
            waag(u1, u2, hoehe * 0.5)
    return oeffnungen


def fenster(teil, matrix, o, m):
    """Glas in der Öffnung, dazu Sprossenkreuz und offene Läden in der Hausfarbe."""
    teil.quader((o.u, -0.015, o.v + o.h / 2), (o.b, 0.03, o.h), m["Glas"], matrix)
    if o.sprossen:
        teil.quader((o.u, -0.045, o.v + o.h / 2), (0.05, 0.04, o.h), m["Holz"], matrix)
        teil.quader((o.u, -0.045, o.v + o.h / 2), (o.b, 0.04, 0.05), m["Holz"], matrix)
    if o.laden:
        lb = o.b * 0.5
        for seite in (-1, 1):
            u = o.u + seite * (o.b / 2 + BALKEN + lb / 2 + 0.02)
            teil.quader((u, -VORSPRUNG - 0.03, o.v + o.h / 2), (lb, 0.05, o.h), m["Hausfarbe"], matrix)


def tuer(teil, matrix, o, m):
    """Brettertür in der Hausfarbe mit zwei Eisenbändern, in die Wand zurückgesetzt."""
    teil.quader((o.u, -0.02, o.v + o.h / 2), (o.b, 0.04, o.h), m["Hausfarbe"], matrix)
    for v in (0.3, 0.7):
        teil.quader((o.u, -0.05, o.v + o.h * v), (o.b * 0.85, 0.03, 0.07), m["Eisen"], matrix)
    teil.quader((o.u + o.b * 0.3, -0.06, o.v + o.h * 0.48), (0.08, 0.06, 0.08), m["Eisen"], matrix)


def bretter(teil, matrix, o, m):
    """Leerstand: zwei gekreuzte und ein waagrechtes Brett vor der Öffnung."""
    y = -VORSPRUNG - 0.12
    laenge = math.hypot(o.b + 0.3, o.h + 0.2)
    winkel = math.degrees(math.atan2(o.h + 0.2, o.b + 0.3))
    for w in (winkel, -winkel):
        teil.quader((o.u, y, o.v + o.h / 2), (laenge, 0.05, 0.2), m["Bretter"], matrix, dreh=(0, -w, 0))
    teil.quader((o.u, y + 0.05, o.v + o.h * 0.5), (o.b + 0.4, 0.05, 0.2), m["Bretter"], matrix)


# ─────────────────────────────────────────────────────────────────────────────
#  Dächer
# ─────────────────────────────────────────────────────────────────────────────

def satteldach(teil, m, x_von, x_bis, y_von, y_bis, z_traufe, neigung, ueberstand_traufe=0.55,
               ueberstand_giebel=0.4, dicke=0.25, reihen=1.2, material="Ziegel"):
    """Satteldach mit First entlang X. `y_von`/`y_bis` = Außenkanten der Wände (Traufseiten),
    `z_traufe` = Oberkante der Wand. Gibt (first_y, first_z) der Dachunterseite zurück.
    Sichtbare Reihen (Biberschwanz) als dünne Leisten auf jeder Dachfläche."""
    neig = math.radians(neigung)
    y_first = (y_von + y_bis) / 2
    halb = (y_bis - y_von) / 2
    z_first = z_traufe + halb * math.tan(neig)
    laenge_x = (x_bis - x_von) + 2 * ueberstand_giebel
    x_mitte = (x_von + x_bis) / 2
    waag = halb + ueberstand_traufe              # waagrechte Länge einer Dachhälfte
    schraeg = waag / math.cos(neig)              # Länge entlang der Dachfläche
    for seite in (-1, 1):                        # -1 = vorne (-Y), +1 = hinten
        normale = Vector((0, seite * math.sin(neig), math.cos(neig)))
        richtung = Vector((0, seite * math.cos(neig), -math.sin(neig)))  # vom First abwärts
        first = Vector((x_mitte, y_first, z_first))
        mitte = first + richtung * (schraeg / 2) + normale * (dicke / 2)
        dreh = (-seite * neigung, 0, 0)
        teil.quader(tuple(mitte), (laenge_x, schraeg, dicke), m[material], dreh=dreh)
        # Reihenleisten
        n = int(schraeg / reihen)
        for k in range(1, n + 1):
            p = first + richtung * (k * reihen) + normale * (dicke + 0.025)
            teil.quader(tuple(p), (laenge_x - 0.02, 0.09, 0.05), m[material], dreh=dreh)
    # Firstbalken
    teil.quader((x_mitte, y_first, z_first + dicke / math.cos(neig) + 0.02), (laenge_x, 0.32, 0.16), m["Holz"])
    return y_first, z_first


def giebelwand(teil, m, x, richtung, y_von, y_bis, z_traufe, z_first, dicke, fachwerk_balken=True):
    """Dreieckiger Giebel (Putz) zwischen Traufhöhe und First an der Seite x; mit Fachwerk."""
    y_first = (y_von + y_bis) / 2
    halb = (y_bis - y_von) / 2
    rise = z_first - z_traufe
    matrix = wandmatrix((x, y_first, z_traufe), richtung)
    umriss = [(-halb, 0), (halb, 0), (0, rise)]
    teil.prisma(umriss, dicke, m["Putz"], matrix)
    if not fachwerk_balken:
        return
    b = BALKEN
    y = -VORSPRUNG + BALKENTIEFE / 2

    def breite_bei(v):
        return 2 * halb * (1 - v / rise)

    # Schwelle, Mittelständer, Kehlbalken, zwei Streben
    teil.quader((0, y, b / 2), (2 * halb, BALKENTIEFE, b), m["Holz"], matrix)
    teil.quader((0, y, rise * 0.5), (b, BALKENTIEFE, rise * 0.92), m["Holz"], matrix)
    vk = rise * 0.42
    teil.quader((0, y, vk), (breite_bei(vk) - 0.1, BALKENTIEFE, b), m["Holz"], matrix)
    for s in (-1, 1):
        u1, v1 = s * (halb * 0.45), b
        u2, v2 = s * 0.12, vk + rise * 0.3
        laenge = math.hypot(u2 - u1, v2 - v1)
        w = math.degrees(math.atan2(v2 - v1, u2 - u1))
        teil.quader(((u1 + u2) / 2, y, (v1 + v2) / 2), (laenge, BALKENTIEFE, b), m["Holz"], matrix, dreh=(0, -w, 0))
        # Pfosten in halber Breite
        u = s * halb * 0.5
        teil.quader((u, y, (b + breite_hoehe(halb, rise, u)) / 2), (b, BALKENTIEFE, breite_hoehe(halb, rise, u) - b), m["Holz"], matrix)


def breite_hoehe(halb, rise, u):
    """Höhe des Giebeldreiecks an der Stelle u."""
    return rise * (1 - abs(u) / halb)


# ─────────────────────────────────────────────────────────────────────────────
#  Geruest: Stangen, Riegel, Laufbretter vor der Gassenseite
# ─────────────────────────────────────────────────────────────────────────────

def geruest(teil, m, x_von, x_bis, y_wand, hoehe, ebenen=(2.4, 4.8), abstand=0.9):
    """Baugerüst vor einer Wand bei y_wand (Vorderseite, Gerüst steht davor bei kleinerem y)."""
    holz = m["Geruestholz"]
    y_aussen = y_wand - abstand
    y_innen = y_wand - 0.25
    n = max(2, int((x_bis - x_von) / 2.2) + 1)
    xs = [x_von + (x_bis - x_von) * i / (n - 1) for i in range(n)]
    for x in xs:
        teil.quader((x, y_aussen, hoehe / 2), (0.14, 0.14, hoehe), holz)
        teil.quader((x, y_innen, hoehe / 2 - 0.3), (0.14, 0.14, hoehe - 0.6), holz)
    for z in ebenen:
        for y in (y_aussen, y_innen):
            teil.quader(((x_von + x_bis) / 2, y, z), (x_bis - x_von + 0.3, 0.12, 0.12), holz)
        for x in xs:
            teil.quader((x, (y_aussen + y_innen) / 2, z), (0.12, abstand - 0.25 + 0.14, 0.12), holz)
        # Laufbretter
        breite = abstand - 0.25
        for k in range(3):
            y = y_innen - 0.05 - breite * (k + 0.5) / 3
            teil.quader(((x_von + x_bis) / 2, y, z + 0.1), (x_bis - x_von + 0.2, breite / 3 - 0.03, 0.05), holz)
        # Geländer
        teil.quader(((x_von + x_bis) / 2, y_aussen, z + 1.0), (x_bis - x_von + 0.3, 0.08, 0.08), holz)
    # Diagonalstrebe außen
    laenge = math.hypot(x_bis - x_von, ebenen[-1])
    w = math.degrees(math.atan2(ebenen[-1], x_bis - x_von))
    teil.quader(((x_von + x_bis) / 2, y_aussen - 0.1, ebenen[-1] / 2), (laenge, 0.08, 0.10), holz, dreh=(0, -w, 0))


# ─────────────────────────────────────────────────────────────────────────────
#  Export und Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def exportieren(name, objekte):
    """Schreibt die Objekte als GLB (Y oben, Vorderseite +Z) nach game/assets/modelle/<name>.glb."""
    os.makedirs(MODELLE, exist_ok=True)
    pfad = os.path.join(MODELLE, name + ".glb")
    for o in bpy.context.scene.objects:
        o.select_set(o in objekte)
    bpy.ops.export_scene.gltf(
        filepath=pfad, export_format="GLB", use_selection=True, export_yup=True,
        export_apply=True, export_animations=False, export_lights=False, export_cameras=False,
        export_extras=True, export_normals=True, export_texcoords=False, export_skins=False,
        export_morph=False, export_image_format="NONE")
    return pfad


def vorschau_einrichten(groesse=20.0, flaeche=(8, 10), ziel_hoehe=4.5, schnell=False):
    """Boden, Straße, Rahmen der belegten Fläche, Sonne und die Spielkamera:
    orthografisch, 40° geneigt, Blick diagonal von vorne rechts. Nur für das Bild, nicht im Export."""
    m_boden = material("_Boden", (0.30, 0.32, 0.24))
    m_strasse = material("_Strasse", (0.33, 0.32, 0.30))
    m_rahmen = material("_Rahmen", (0.85, 0.80, 0.55))
    t = Bauteil("_Umgebung")
    t.quader((0, 0, -0.05), (60, 60, 0.1), m_boden)
    t.quader((0, -flaeche[1] / 2 - 3.3, -0.04), (60, 6.5, 0.1), m_strasse)
    bx, by = flaeche[0] / 2, flaeche[1] / 2
    for (mitte, masse) in (((0, -by, 0), (flaeche[0], 0.08, 0.02)), ((0, by, 0), (flaeche[0], 0.08, 0.02)),
                           ((-bx, 0, 0), (0.08, flaeche[1], 0.02)), ((bx, 0, 0), (0.08, flaeche[1], 0.02))):
        t.quader(mitte, masse, m_rahmen)
    umgebung = t.objekt()

    szene = bpy.context.scene
    kam_daten = bpy.data.cameras.new("Kamera")
    kam_daten.type = "ORTHO"
    kam_daten.ortho_scale = groesse
    kam_daten.clip_end = 300
    kamera = bpy.data.objects.new("Kamera", kam_daten)
    neigung = math.radians(40)
    abstand = 60.0
    kamera.location = (abstand * math.cos(neigung) / math.sqrt(2), -abstand * math.cos(neigung) / math.sqrt(2),
                       ziel_hoehe + abstand * math.sin(neigung))
    kamera.rotation_euler = (math.radians(50), 0, math.radians(45))
    szene.collection.objects.link(kamera)
    szene.camera = kamera

    sonne_daten = bpy.data.lights.new("Sonne", "SUN")
    sonne_daten.energy = 3.0
    sonne_daten.color = (1.0, 0.93, 0.82)
    sonne_daten.angle = math.radians(3)
    sonne = bpy.data.objects.new("Sonne", sonne_daten)
    sonne.rotation_euler = (math.radians(52), 0, math.radians(-20))
    szene.collection.objects.link(sonne)

    welt = bpy.data.worlds.new("Welt")
    welt.use_nodes = True
    hintergrund = welt.node_tree.nodes["Background"]
    hintergrund.inputs["Color"].default_value = (0.42, 0.47, 0.55, 1)
    hintergrund.inputs["Strength"].default_value = 0.9
    szene.world = welt

    szene.render.engine = "CYCLES"
    szene.cycles.device = "CPU"
    szene.cycles.samples = 16 if schnell else 64
    szene.cycles.use_denoising = True
    szene.render.resolution_x = 1200
    szene.render.resolution_y = 900
    szene.render.resolution_percentage = 50 if schnell else 100
    szene.render.film_transparent = False
    szene.view_settings.view_transform = "Standard"
    return umgebung, kamera, sonne


def rendern(name, sichtbar):
    """Rendert docs/modelle/<name>.png. `sichtbar`: Funktion objekt → bool für die Zustände."""
    os.makedirs(VORSCHAU, exist_ok=True)
    for o in bpy.context.scene.objects:
        if o.type == "MESH":
            o.hide_render = not sichtbar(o)
    pfad = os.path.join(VORSCHAU, name + ".png")
    bpy.context.scene.render.filepath = pfad
    bpy.ops.render.render(write_still=True)
    return pfad


def bericht(name, objekte):
    """Gibt die Dreieckszahl je Objekt und gesamt aus."""
    gesamt = 0
    print(f"\n{name}: Dreiecke je Objekt")
    for o in objekte:
        o.data.calc_loop_triangles()
        n = len(o.data.loop_triangles)
        gesamt += n
        print(f"  {o.name:12s} {n:6d}")
    print(f"  {'gesamt':12s} {gesamt:6d}\n")
    return gesamt
