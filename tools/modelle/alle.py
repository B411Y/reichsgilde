"""Erzeugt alle Modelle neu (Wohnhäuser, Werkstätten und Einwohnerhäuser) und rendert die Übersicht der Wohnhaus-Stufen.

    python tools/modelle/alle.py [--schnell] [--ohne-bilder]
    blender -b --python tools/modelle/alle.py -- [--schnell]

Jedes Modell läuft in einem eigenen Prozess (frische Blender-Szene). Danach lädt dieses Skript
die fertigen GLB-Dateien, stellt sie im selben Maßstab nebeneinander an eine Gasse und rendert
docs/modelle/wohnhaeuser.png – so sieht man, wie das Haus mit dem Stand wächst.
"""

import os
import subprocess
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402

ORDNER = os.path.dirname(os.path.abspath(__file__))
# Reihenfolge = Stufe; Beschriftung für die Übersicht
WOHNHAEUSER = [("fachwerkhaus", "Fachwerkhaus – Gemeiner"), ("buergerhaus", "Bürgerhaus – Bürger"),
               ("steinhaus", "Steinhaus – Patrizier"), ("stadtpalais", "Stadtpalais – Edler")]
WERKSTAETTEN = ["schmied", "tischler", "steinmetz", "alchimist", "wirt", "prediger", "dieb", "gardist", "geldleiher",
                "parfumeur"]          # je Beruf ein Modell mit Stufe1–Stufe3
EINWOHNERHAEUSER = ["einwohner_ratsherrenhaus"]      # je Hausart ein Modell mit Variante1–Variante3


def erzeugen(name, schalter):
    skript = os.path.join(ORDNER, name + ".py")
    if bpy.app.binary_path and os.path.basename(bpy.app.binary_path).lower().startswith("blender"):   # Blender als Programm
        befehl = [bpy.app.binary_path, "-b", "--python", skript, "--"] + schalter
    else:
        befehl = [sys.executable, skript] + schalter
    print(">>", " ".join(befehl))
    subprocess.run(befehl, check=True)


def uebersicht(schnell=False, abstand=14.0):
    """Alle Wohnhäuser nebeneinander, Zustände ausgeblendet, gleiche Kamera wie im Spiel."""
    bs.neue_szene()
    mitte = (len(WOHNHAEUSER) - 1) / 2
    ausschnitt = (len(WOHNHAEUSER) - 1) * abstand + 18.0      # Bildbreite in Metern: alle Häuser und etwas Rand
    for i, (name, _) in enumerate(WOHNHAEUSER):
        vorher = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=os.path.join(bs.MODELLE, name + ".glb"))
        for o in set(bpy.data.objects) - vorher:
            if o.parent is None:      # quer zur Blickrichtung aufreihen, damit sich keins verdeckt
                o.location.x += (i - mitte) * abstand * 0.7071
                o.location.y += (i - mitte) * abstand * 0.7071
            if o.name.split(".")[0] in ("Leerstand", "Geruest"):
                o.hide_render = True
    bs.vorschau_buehne(None, ziel_hoehe=6.2, ausschnitt=ausschnitt, schnell=schnell)
    szene = bpy.context.scene
    pfad = os.path.join(bs.VORSCHAU, "wohnhaeuser.png")
    szene.render.resolution_x, szene.render.resolution_y = 1600, 900
    szene.render.filepath = pfad
    bs.blick(0)
    bpy.ops.render.render(write_still=True)
    try:
        from PIL import Image, ImageDraw, ImageFont
        bild = Image.open(pfad).convert("RGB")
        zeichnen = ImageDraw.Draw(bild)
        groesse = int(26 * bild.size[0] / 1600)
        try:                       # Schrift des Spiels (Alegreya, OFL) – kennt Umlaute
            schrift = ImageFont.truetype(os.path.join(bs.WURZEL, "game", "assets", "schriften", "Alegreya-Variabel.ttf"), groesse)
        except OSError:
            schrift = ImageFont.load_default()
        breite = bild.size[0]
        for i, (_, text) in enumerate(WOHNHAEUSER):
            x = int(breite * (0.5 + (i - mitte) * abstand / ausschnitt))
            zeichnen.text((x, int(bild.size[1] * 0.94)), f"Stufe {i + 1}: {text}", fill=(240, 236, 220), font=schrift,
                          anchor="ms")
        bild.save(pfad, optimize=True)
    except ImportError:
        pass
    return pfad


def main():
    schalter = [a for a in sys.argv if a in ("--schnell", "--ohne-bilder")]
    for name in [n for n, _ in WOHNHAEUSER] + WERKSTAETTEN + EINWOHNERHAEUSER:
        erzeugen(name, schalter)
    if "--ohne-bilder" not in schalter:
        print(uebersicht(schnell="--schnell" in schalter))


if __name__ == "__main__":
    main()
