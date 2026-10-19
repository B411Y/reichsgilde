"""Erzeugt alle Modelle neu (Wohnhäuser, Werkstätten, Innenräume, Einwohnerhäuser, Sonderbauten, Umland, Natur, Figuren, Markt und Karren) und rendert die
Übersicht der Wohnhaus-Stufen.

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
               ("steinhaus", "Steinhaus – Patrizier"), ("stadtpalais", "Stadtpalais – Edler"),
               ("stadthof", "Stadthof – Freiherr")]
# Belegte Fläche (Breite, Tiefe) je Wohnhaus: daraus der Abstand in der Übersicht
FLAECHEN = {"fachwerkhaus": (8, 10), "buergerhaus": (8, 10), "steinhaus": (8, 10), "stadtpalais": (12, 10),
            "stadthof": (16, 14)}
WERKSTAETTEN = ["schmied", "tischler", "steinmetz", "alchimist", "wirt", "prediger", "dieb", "gardist", "geldleiher",
                "parfumeur"]          # je Beruf ein Modell mit Stufe1–Stufe3
INNENRAEUME = ["schmied_innen", "tischler_innen"]      # je Beruf ein Innenraum: Räume, Stufen, Bedienung, Einrichtung, Blickpunkte, Arbeitsplätze
EINWOHNERHAEUSER = ["einwohner_ratsherrenhaus", "einwohner_buergerhaus", "einwohner_wohnhaus"]      # je Hausart ein Modell mit Variante1–Variante3
SONDERBAUTEN = ["dom", "rathaus", "stadtmauer"]          # Gebäude der Stadt: ohne Zustände und Hausfarbe
UMLAND = ["grube", "holzschlag", "steinbruch", "kraeutergarten", "duftgarten", "bauernhof", "klosterhof",
          "wachturm"]         # Umlandbetriebe: Haus, Arbeit, Fahne, Leerstand (gemeinsame Teile in landbau.py)
BERGE = ["berg_grube"]        # Felsberg hinter der Grube (eigene Datei, Knoten „Berg“)
UMLANDSTELLEN = ["umland_stellen"]                       # Weide, Feld und Wald der Einwohner
NATUR = ["natur_baeume", "natur_buesche", "natur_boden", "natur_steine", "natur_holz"]   # Baukästen, gemeinsame Teile in natur.py
MARKT = ["markt"]             # ein Skript, schreibt marktplatz.glb (Platz und Anker) und marktstaende.glb (Baukasten)
KARREN = ["karren"]           # ein Skript, schreibt karren_hand/ochse/krieg.glb mit Zugtieren und Bewegungen
VOEGEL = ["voegel"]           # ein Skript, schreibt vogel_taube/spatz.glb mit Skelett und Bewegungen
FIGUREN = ["figuren"]         # ein Skript, schreibt figur_mensch/zwerg/elb/halbling.glb mit Skelett und Bewegungen


def erzeugen(name, schalter):
    skript = os.path.join(ORDNER, name + ".py")
    if bpy.app.binary_path and os.path.basename(bpy.app.binary_path).lower().startswith("blender"):   # Blender als Programm
        befehl = [bpy.app.binary_path, "-b", "--python", skript, "--"] + schalter
    else:
        befehl = [sys.executable, skript] + schalter
    print(">>", " ".join(befehl))
    subprocess.run(befehl, check=True)


def uebersicht(schnell=False, luft=0.5):
    """Alle Wohnhäuser nebeneinander, Zustände ausgeblendet, gleiche Kamera wie im Spiel.
    Die Häuser stehen quer zur Blickrichtung; jedes nimmt im Bild etwa (Breite + Tiefe) · 0,71 m ein."""
    bs.neue_szene()
    breiten = [sum(FLAECHEN.get(name, (8, 10))) * 0.7071 for name, _ in WOHNHAEUSER]
    orte = [0.0]
    for a, b in zip(breiten, breiten[1:]):
        orte.append(orte[-1] + (a + b) / 2 + luft)
    mitte = (orte[0] + orte[-1]) / 2
    ausschnitt = orte[-1] - orte[0] + (breiten[0] + breiten[-1]) / 2 + 2.0     # Bildbreite in Metern
    for i, (name, _) in enumerate(WOHNHAEUSER):
        vorher = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=os.path.join(bs.MODELLE, name + ".glb"))
        for o in set(bpy.data.objects) - vorher:
            if o.parent is None:      # quer zur Blickrichtung aufreihen, damit sich keins verdeckt
                o.location.x += (orte[i] - mitte) * 0.7071
                o.location.y += (orte[i] - mitte) * 0.7071
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
        for i, (_, text) in enumerate(WOHNHAEUSER):      # zweizeilig: „Stufe n“, darunter Haus und Stand
            x = int(breite * (0.5 + (orte[i] - mitte) / ausschnitt))
            for zeile, y in ((f"Stufe {i + 1}", 0.9), (text, 0.95)):
                zeichnen.text((x, int(bild.size[1] * y)), zeile, fill=(240, 236, 220), font=schrift, anchor="ms")
        bild.save(pfad, optimize=True)
    except ImportError:
        pass
    return pfad


def main():
    schalter = [a for a in sys.argv if a in ("--schnell", "--ohne-bilder")]
    for name in [n for n, _ in WOHNHAEUSER] + WERKSTAETTEN + INNENRAEUME + EINWOHNERHAEUSER + SONDERBAUTEN + UMLAND + BERGE + UMLANDSTELLEN + NATUR + FIGUREN + MARKT + KARREN + VOEGEL:
        erzeugen(name, schalter)
    if "--ohne-bilder" not in schalter:
        print(uebersicht(schnell="--schnell" in schalter))


if __name__ == "__main__":
    main()
