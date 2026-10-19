"""Holzschnitt-Vorschau ohne das Spiel: baut ein kleines Godot-Projekt in einem Ordner, lädt ein paar Modelle
mit game/shader/holzschnitt*.gdshader und rendert drei Bilder (tag, nacht, aus) nach docs/modelle/holzschnitt*.png.

    python tools/holzschnitt/vorschau.py <godot-programm> [ordner]

Braucht ein Display (unter Linux z. B. Xvfb, DISPLAY=:78). Getestet mit --rendering-driver opengl3; mit vulkan ist es der
Forward+-Renderer des Spiels. Das Testprojekt in vorschau/test.gd zeigt auch, wie die Materialien zugeordnet werden.
"""
import os
import shutil
import subprocess
import sys

from PIL import Image, ImageDraw

WURZEL = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
MODELLE = ["schmied", "fachwerkhaus", "grube", "berg_grube", "natur_baeume", "karren_ochse", "figur_mensch"]
PROJEKT = """config_version=5
[application]
run/main_scene="res://test.tscn"
[display]
window/size/viewport_width=1280
window/size/viewport_height=800
[rendering]
renderer/rendering_method="{renderer}"
[shader_globals]
holzschnitt_umgebung={{
"type": "vec3",
"value": Vector3(0.3, 0.3, 0.32)
}}
"""


def main():
    godot = sys.argv[1]
    ordner = sys.argv[2] if len(sys.argv) > 2 else os.path.join(WURZEL, ".holzschnitt-vorschau")
    treiber = os.environ.get("TREIBER", "opengl3")
    os.makedirs(ordner, exist_ok=True)
    for name in MODELLE:
        shutil.copy(os.path.join(WURZEL, "game", "assets", "modelle", name + ".glb"), ordner)
    for datei in os.listdir(os.path.join(WURZEL, "game", "assets", "holzschnitt")):
        if datei.endswith((".png", ".json")):
            shutil.copy(os.path.join(WURZEL, "game", "assets", "holzschnitt", datei), ordner)
    for datei in ("holzschnitt.gdshader", "holzschnitt_umriss.gdshader"):
        shutil.copy(os.path.join(WURZEL, "game", "shader", datei), ordner)
    shutil.copy(os.path.join(os.path.dirname(os.path.abspath(__file__)), "vorschau", "test.gd"), ordner)
    with open(os.path.join(ordner, "test.tscn"), "w") as f:
        f.write('[gd_scene load_steps=2 format=3]\n[ext_resource type="Script" path="res://test.gd" id="1"]\n'
                '[node name="Test" type="Node3D"]\nscript = ExtResource("1")\n')
    with open(os.path.join(ordner, "project.godot"), "w") as f:
        f.write(PROJEKT.format(renderer="gl_compatibility" if treiber == "opengl3" else "forward_plus"))
    subprocess.run([godot, "--headless", "--import", "--path", ordner], capture_output=True, timeout=600)
    for modus in ("aus", "tag", "nacht"):
        subprocess.run([godot, "--rendering-driver", treiber, "--path", ordner, "--", "--modus=" + modus], timeout=300)
    bilder = [Image.open(os.path.join(ordner, f"bild_{m}.png")).convert("RGB").crop((240, 0, 1040, 800)) for m in ("aus", "tag", "nacht")]
    gesamt = Image.new("RGB", (3 * 800 + 20, 800), "white")
    for i, (b, text) in enumerate(zip(bilder, ("Ohne Holzschnitt", "Holzschnitt, Tag", "Holzschnitt, Nacht"))):
        gesamt.paste(b, (i * 810, 0))
        ImageDraw.Draw(gesamt).rectangle((i * 810, 0, i * 810 + 170, 26), fill="black")
        ImageDraw.Draw(gesamt).text((i * 810 + 8, 7), text, fill="white")
    pfad = os.path.join(WURZEL, "docs", "modelle", "holzschnitt.png")
    gesamt.save(pfad, optimize=True)
    print(pfad)


if __name__ == "__main__":
    main()
