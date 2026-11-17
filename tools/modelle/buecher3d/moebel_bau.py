"""Lesepult (fuer das Personalbuch) und Schreibpult (fuer das Kontorbuch) als eigene Moebelmodelle im Stil der Welt: Grundkoerper mit festen Farben
(Holz, Eisen, Leder, Kohle), die Struktur kommt im Spiel aus der Material-Bibliothek wie bei allen Weltmodellen; kein Holzschnitt-Shader.
  blender -b --python tools/modelle/buecher3d/moebel_bau.py            schreibt game/assets/modelle/requisiten/lesepult.glb und schreibpult.glb
Der Knoten `Buch_Auflage` (leerer Knoten) sitzt auf der Schraege des Pults, seine lokale Z-Achse ist die Normale der Flaeche, seine Y-Achse zeigt den Hang
hinauf, X nach rechts: Das Buch (Ursprung = Mitte auf der Auflage, Unterseite bei z = 0) wird Kind dieses Knotens, ohne Drehung und Versatz.
Blickrichtung wie bei allen Modellen: Vorderseite -Y in Blender, +Z im Spiel (die Schraege faellt zum Betrachter hin ab).
Der Stubentisch des Wohnhauses ist kein eigenes Modell (er entsteht im Spiel aus Grundkoerpern, Stadtansicht.Raeume.cs `WohnstubeEinrichten`):
das Familienbuch liegt auf seiner Platte (Hoehe 0,80 m, Mitte der Platte), ohne Neigung."""
import math
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HIER, ".."))
import bausteine as bs  # noqa: E402
from bausteine import Vector, Matrix  # noqa: E402


def rx(grad):
    return Matrix.Rotation(math.radians(grad), 4, "X")


def auflage(name, matrix, lokal):
    """Leerer Knoten Buch_Auflage: Lage = `lokal` im System der schiefen Platte, Drehung = Neigung der Platte."""
    o = bpy_objekt(name)
    p = matrix @ Vector(lokal)
    o.location = p
    o.rotation_euler = matrix.to_euler()
    return o


def bpy_objekt(name):
    import bpy
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = 0.1
    obj["auflage"] = True
    bpy.context.scene.collection.objects.link(obj)
    return obj


def lesepult():
    bs.neue_szene()
    st = bs.Stoffe()
    n = bs.Netz("Lesepult", alle=True)
    phi = 22.0
    zc = 1.02
    M = Matrix.Translation((0, 0, zc)) @ rx(phi)
    # Fuss: Kreuz aus zwei Balken
    n.quader((0, 0, 0.035), (0.64, 0.13, 0.07), st["Holz alt"])
    n.quader((0, 0, 0.0325), (0.13, 0.62, 0.062), st["Holz alt"])
    # Saeule mit Bund und Kapitell
    n.quader((0, 0, 0.535), (0.12, 0.12, 0.93), st["Holz"])
    n.quader((0, 0, 0.32), (0.17, 0.17, 0.04), st["Holz alt"])
    n.quader((0, 0, 0.9775), (0.22, 0.22, 0.055), st["Holz alt"])
    # Streben vom Fuss zur Saeule
    for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        n.stab((sx * 0.27, sy * 0.27, 0.07), (sx * 0.07, sy * 0.07, 0.46), 0.045, 0.04, st["Holz alt"], quer=(1, 0, 0) if sy else (0, 1, 0))
    # Wangen links und rechts unter der Platte (Dreiecks-Trapeze, Hoehe nach der Neigung)
    cs, sn = math.cos(math.radians(phi)), math.sin(math.radians(phi))
    yv = -0.25 * cs + 0.02 * sn
    zv = zc - 0.25 * sn - 0.02 * cs
    yh = 0.25 * cs + 0.02 * sn
    zh = zc + 0.25 * sn - 0.02 * cs
    for x0 in (-0.35, 0.315):
        n.prisma([(yv, zv), (yh, zh), (yh, zh - 0.05), (yv, zv - 0.05)], x0, x0 + 0.035, st["Holz alt"])
    # schraege Platte mit Leiste unten (haelt das Buch) und Rueckleiste
    n.quader((0, 0, 0), (0.72, 0.50, 0.04), st["Holz"], matrix=M)
    n.quader((0, -0.235, 0.035), (0.70, 0.03, 0.03), st["Holz alt"], matrix=M)
    n.quader((0, 0.24, 0.03), (0.70, 0.02, 0.02), st["Holz alt"], matrix=M)
    # Eisenbeschlaege an den Plattenecken
    for sx in (-1, 1):
        n.quader((sx * 0.335, -0.235, 0.0), (0.03, 0.03, 0.045), st["Eisen"], matrix=M)
    obj = n.objekt()
    a = auflage("Buch_Auflage", M, (0.0, 0.012, 0.02))
    return "lesepult", [obj, a]


def schreibpult():
    bs.neue_szene()
    st = bs.Stoffe()
    n = bs.Netz("Schreibpult", alle=True)
    phi = 16.0
    zf = 0.80                                     # Hoehe der Platte an der Vorderkante der Wangen
    lang = 0.62                                   # Laenge der Platte entlang der Schraege
    cs, sn = math.cos(math.radians(phi)), math.sin(math.radians(phi))
    z_mitte = zf + (lang / 2) * sn * (0.3 / 0.31)  # Mitte der Oberflaeche
    z_mitte = zf + 0.30 * math.tan(math.radians(phi))
    M = Matrix.Translation((0, 0, z_mitte - 0.02 / cs)) @ rx(phi)
    hoch = zf + 0.60 * math.tan(math.radians(phi))
    for x0 in (-0.50, 0.46):
        n.prisma([(-0.30, 0.0), (-0.30, zf - 0.042), (0.30, hoch - 0.042), (0.30, 0.0)], x0, x0 + 0.04, st["Holz alt"])
    n.quader((0, 0, 0.215), (0.92, 0.56, 0.03), st["Holz"])                       # Ablagebrett
    n.quader((0, 0.285, 0.47), (0.92, 0.025, 0.86), st["Holz alt"])               # Rueckwand
    n.quader((0, -0.29, 0.40), (0.92, 0.025, 0.05), st["Holz alt"])               # Querriegel vorn
    # Platte, Leiste unten, Federrinne oben, Schreibunterlage aus Leder
    n.quader((0, 0, 0), (1.02, 0.62, 0.04), st["Holz"], matrix=M)
    n.quader((0, -0.30, 0.035), (1.02, 0.025, 0.03), st["Holz alt"], matrix=M)
    n.quader((0, 0.285, 0.03), (1.02, 0.045, 0.02), st["Holz alt"], matrix=M)
    n.quader((-0.04, -0.02, 0.023), (0.66, 0.42, 0.006), st["Leder"], matrix=M)
    # Tintenfass rechts oben und Federkiel-Brett
    n.quader((0.40, 0.19, 0.0425), (0.07, 0.07, 0.045), st["Kohle"], matrix=M)
    n.quader((0.40, 0.19, 0.067), (0.045, 0.045, 0.006), st["Eisen"], matrix=M)
    n.quader((0.40, -0.02, 0.025), (0.09, 0.30, 0.01), st["Holz alt"], matrix=M)
    for sx in (-1, 1):
        n.quader((sx * 0.505, -0.30, 0.0), (0.03, 0.03, 0.045), st["Eisen"], matrix=M)
    obj = n.objekt()
    a = auflage("Buch_Auflage", M, (-0.04, 0.0, 0.026))
    return "schreibpult", [obj, a]


def main():
    for bau in (lesepult, schreibpult):
        name, objekte = bau()
        bs.bericht(name, [o for o in objekte if o.type == "MESH"])
        bs.BIBLIOTHEK_JE_FLAECHE = True
        vorher, nachher = bs.farben_zusammenlegen(objekte)
        print(f"  Materialien: {vorher} -> {nachher}")
        a = next(o for o in objekte if o.type == "EMPTY")
        print(f"  Buch_Auflage bei ({a.location.x:.4f}, {a.location.y:.4f}, {a.location.z:.4f}), Neigung {math.degrees(a.rotation_euler.x):.1f} Grad um X")
        print("geschrieben:", bs.exportieren(name, objekte))


if __name__ == "__main__":
    main()
