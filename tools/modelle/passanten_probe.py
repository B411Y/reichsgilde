"""Probe „Weg A“: Passanten mit echtem Skelett (MakeHuman/MPFB-Körper, Kleidung um 1400, Bewegungen von Quaternius).

    blender -b --factory-startup --python tools/modelle/passanten_probe.py -- \
        --aus=game/assets/modelle/figuren/probe --bilder=<Ordner> \
        --ual1=<AnimationLibrary_Godot_Standard.glb> --ual2=<UAL2_Standard.glb> [--bericht=<datei.json>] [--nur=mann|frau]

Schreibt zwei Dateien in den Ordner --aus: passant_probe.glb (Mann) und passant_probe_frau.glb (Frau).

MPFB 2 (2.0.18) muss als Erweiterung „bl_ext.user_default.mpfb“ in einem eigenen Benutzerordner liegen
(Umgebungsvariable BLENDER_USER_RESOURCES, nie im Blender des Nutzers); das Skript schaltet sie selbst ein.

Quellen (nichts davon liegt im Repository, nur die erzeugten .glb):
  * Körper, Gewichte und Spielskelett „game_engine“: MakeHuman-Grundnetz und Rig aus MPFB 2 – Assets CC0, der GPL-Code
    von MPFB läuft nur als Werkzeug; die Ausgabe gehört uns (MPFB-LICENSE.md, Abschnitt D).
  * Bewegungen: Quaternius „Universal Animation Library“ 1 und 2, Fassung Standard (CC0), über OpenGameArt (Tabelle BEWEGUNGEN).
    Übertragen auf unser Skelett hier im Skript (Richtung der Arm- und Beinknochen wie in der Quelle, Hüfthöhe angeglichen).
  * Kleidung um 1400 – eigene Formen auf dem Körper, keine MakeHuman-Kleidung: Cotte mit Gürtel, Gugel mit Schulterkragen
    und Zipfel, knöchellanger Rock bzw. Kleid, Haube und Gebende mit Schleier für Frauen, Bundschuhe mit Spitze aus Leder.

Saubere Kanten (Halsausschnitt, Gürtel, Ärmel, Gesichtsöffnung): Die Grenzen sind keine Treppen aus Flächen, sondern
Schnittlinien eines Feldes über die Ecken (`isolinie_schneiden`); beim Verkleinern bleiben Grenzecken und der Kopf dichter
(Eckengruppe „Erhalten“). Stoffe tragen die Schicht „tuch“ bzw. „leder“ der Material-Bibliothek in UV.y (wie bausteine.py:
Art + 16 × (Schicht + 1)), die Oberflächendetails des Spiels legen dort die Struktur darüber.

Skelett: 23 Knochen mit den Namen von Godots SkeletonProfileHumanoid (Root, Hips, Spine, Chest, UpperChest, Neck, Head,
Left/RightShoulder, …UpperArm, …LowerArm, …Hand, …UpperLeg, …LowerLeg, …Foot, …Toes); die Fingergewichte sind in die Hand
gelegt. Netze: je Variante „Nah_<variante>“ und „Fern_<variante>“, dazu „Last“ (Kiste, nur beim Tragen sichtbar).
Farben wie bei den Figuren aus figuren.py: Zone in UV.x, feste Farbe als Eckenfarbe (figur.gdshader).
"""

import json
import math
import os
import sys
import importlib
import addon_utils

import bmesh
import bpy
from mathutils import Matrix, Vector, Quaternion

HIER = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HIER, "..", ".."))

# ─────────────────────────────────────────────────────────────────────────────
#  Einstellungen
# ─────────────────────────────────────────────────────────────────────────────

FPS = 24
NAH_ZIEL = 3300          # Dreiecke je Variante, nah (Kopf und Kanten bekommen davon mehr, siehe verkleinern)
FERN_ZIEL = 560          # Dreiecke je Variante, fern

# Zonen wie figur.gdshader: 0 feste Farbe, 1 Gewand, 2 Beinkleid, 3 Haut, 4 Haar, 5 Kopfbedeckung
STOFFE = {  # Name: (Zone, feste Farbe sRGB, Schicht der Material-Bibliothek oder None)
    "Schuh": (0, (0.17, 0.11, 0.07), "leder"),
    "Guertel": (0, (0.26, 0.17, 0.09), "leder"),
    "Kiste": (0, (0.46, 0.33, 0.19), None),
    "Gewand": (1, (1.0, 1.0, 1.0), "tuch"),
    "Beinkleid": (2, (1.0, 1.0, 1.0), "tuch"),
    "Haut": (3, (1.0, 1.0, 1.0), None),
    "Haar": (4, (1.0, 1.0, 1.0), None),
    "Kopf": (5, (1.0, 1.0, 1.0), "tuch"),
    "Holz": (0, (0.38, 0.26, 0.15), None),
    "Eisen": (0, (0.30, 0.30, 0.32), None),
    "Holzschuh": (0, (0.50, 0.38, 0.24), None),
    "Hauer": (0, (0.84, 0.79, 0.64), None),
    "Flicken": (0, (0.30, 0.25, 0.19), "tuch"),
    "Stroh": (0, (0.66, 0.56, 0.32), None),
    "Wams": (1, (1.0, 1.0, 1.0), "leder"),
    "Strick": (0, (0.50, 0.41, 0.27), "tuch"),
    "Schnur": (0, (0.46, 0.36, 0.24), None),
    "Bart": (6, (1.0, 1.0, 1.0), None),       # Zone 6: im Spiel Haarfarbe (mit Bart) oder Haut (ohne), je Figur
    "Leinen": (0, (0.86, 0.83, 0.75), "tuch"),
    "Reisig": (0, (0.34, 0.26, 0.17), None),
}
STOFF_NAMEN = list(STOFFE)

# Knochen des MPFB-Skeletts „game_engine“ → SkeletonProfileHumanoid (Godot)
KNOCHEN = {
    "Root": "Root", "pelvis": "Hips", "spine_01": "Spine", "spine_02": "Chest", "spine_03": "UpperChest",
    "neck_01": "Neck", "head": "Head",
}
for _s, _S in (("l", "Left"), ("r", "Right")):
    KNOCHEN.update({
        f"clavicle_{_s}": f"{_S}Shoulder", f"upperarm_{_s}": f"{_S}UpperArm", f"lowerarm_{_s}": f"{_S}LowerArm",
        f"hand_{_s}": f"{_S}Hand", f"thigh_{_s}": f"{_S}UpperLeg", f"calf_{_s}": f"{_S}LowerLeg",
        f"foot_{_s}": f"{_S}Foot", f"ball_{_s}": f"{_S}Toes",
    })
FINGER = ("index", "middle", "ring", "pinky", "thumb")

# Quaternius-Rigs → unser Skelett: UAL 1 hat Rigify-DEF-Knochen, UAL 2 die Unreal-Namen (wie MPFB, nur „Head“ groß)
UAL_KNOCHEN = {
    "DEF-hips": "Hips", "DEF-spine.001": "Spine", "DEF-spine.002": "Chest", "DEF-spine.003": "UpperChest",
    "DEF-neck": "Neck", "DEF-head": "Head",
}
for _s, _S in (("L", "Left"), ("R", "Right")):
    UAL_KNOCHEN.update({
        f"DEF-shoulder.{_s}": f"{_S}Shoulder", f"DEF-upper_arm.{_s}": f"{_S}UpperArm", f"DEF-forearm.{_s}": f"{_S}LowerArm",
        f"DEF-hand.{_s}": f"{_S}Hand", f"DEF-thigh.{_s}": f"{_S}UpperLeg", f"DEF-shin.{_s}": f"{_S}LowerLeg",
        f"DEF-foot.{_s}": f"{_S}Foot", f"DEF-toe.{_s}": f"{_S}Toes",
    })
UAL_KNOCHEN.update({k: v for k, v in KNOCHEN.items() if k not in ("Root", "head")})
UAL_KNOCHEN["Head"] = "Head"

# Bewegungen: Name im Spiel → (Quelle, Clip). Reihenfolge = Reihenfolge der Zeilen in der VAT-Textur des Spiels (ProbeKlips).
BEWEGUNGEN = {
    "Stehen": ("ual1", "Idle_Loop"),
    "Gehen": ("ual1", "Walk_Loop"),
    "Tragen": ("ual2", "Walk_Carry_Loop"),
    "Reden": ("ual1", "Idle_Talking_Loop"),
    "Sitzen": ("ual1", "Sitting_Idle_Loop"),
    "Hacken": ("ual2", "TreeChopping_Loop"),
    "Buecken": ("ual2", "Farm_Harvest"),
    "Kauern": ("ual1", "Crouch_Idle_Loop"),
}
# Abgeleitete Clips (im Skript aus einem anderen gebaut, siehe betteln_bauen); sie stehen in der VAT-Textur hinter BEWEGUNGEN
ABGELEITET = ("Betteln",)

# Haltung (Klicktest 08.11.: vornübergebeugt): Bei diesen Clips wird der Oberkörper um so viel aufgerichtet, wie er im Mittel des Clips
# weiter vorgebeugt ist als in der Ruhelage, abzüglich AUFRECHT_REST Grad (ein wenig Vorlage bleibt beim Gehen natürlich)
AUFRECHT = {"Stehen", "Gehen", "Tragen", "Reden", "Sitzen"}
AUFRECHT_REST = 3.0
# Schrittweite: Beindrehungen um ihr Mittel verstärkt, damit der Clip zum Tempo der Passanten passt (Gehen 1,4 m/s, Tragen 0,95 m/s
# bei Beinfaktor 1) und der Takt nahe 1 bleibt – die Füße rutschen nicht und die Schritte wirken nicht gehetzt
SCHRITT = {"Gehen": 1.15, "Tragen": 1.35}
# Arme locker (Klicktest 08.11.: Hände vor dem Bauch): Ellbogenbeugung beim Gehen und Stehen um diesen Faktor kleiner
LOCKER = {"Gehen": 0.45, "Stehen": 0.6}
# Clips, die in der Quelle keine Schleife sind: Ende an den Anfang angleichen (Runde 4: Bücken sprang am Schleifenende)
SCHLEIFE_SCHLIESSEN = {"Buecken"}

FIGUREN = {
    # Vorlage des Nutzers (09.11., Gemini): Hände breit und fleischig mit kräftigen, natürlich gekrümmten Fingern
    "mann": {"datei": "passant_probe.glb", "gender": 1.0, "varianten": ("cotte", "gugel", "rock", "bettler", "kutte"), "bart": True,
             "ziele": {"hand-fingers-diameter-incr": 0.9, "hand-scale-incr": 0.2, "measure-wrist-circ-incr": 0.3}},
    "frau": {"datei": "passant_probe_frau.glb", "gender": 0.0, "varianten": ("haube", "gebende", "bettler"),
             "ziele": {"hand-fingers-diameter-incr": 0.6, "measure-wrist-circ-incr": 0.2}},
    # Orks und Oger (Vollausbau Schritt 3, Antworten des Nutzers 09.11.2026): dasselbe Grundnetz und Skelett, eigene Maße über MPFB-Makros und
    # Formziele (docs/WELT.md: Orks Söldner, Jäger, Knechte; Oger riesig, schwerste Arbeit). Nur Männer, barfuß, grobe ärmellose Kleidung wie
    # Unfreie: Lederwams („wams“) oder Sackleinen („sack“). Orks deutlich kräftiger als Menschen (breites Kreuz, Schultern, Arme, Nacken) und
    # leicht gebeugt; Oger wie im Entwurf mit Unterbiss und kleinen Hauern. Die Größe im Spiel bleibt beim Volksfaktor (FigurMasse: Ork 1,1,
    # Oger 1,35); das Modell selbst hat Menschengröße mit anderen Proportionen.
    "ork": {"datei": "passant_probe_ork.glb", "gender": 1.0, "varianten": ("wams",), "hauer": "ork", "beugen": 9.0,
            "arme": {"upperarm": 1.33, "lowerarm": 1.33, "hand": 1.15, "thigh": 1.15, "calf": 1.15},   # Nutzer 09.11.: zwischen v3 und v4
            "makros": {"muscle": 1.0, "weight": 0.8, "height": 0.5, "proportions": 0.1},
            "ziele": {"measure-shoulder-dist-incr": 1.0, "torso-scale-horiz-incr": 1.0, "torso-muscle-dorsi-incr": 1.0,
                      "upperarm-scale-horiz-incr": 0.8, "upperarm-scale-depth-incr": 0.8, "lowerarm-scale-horiz-incr": 0.6,
                      "lowerarm-scale-depth-incr": 0.6, "measure-neck-circ-incr": 1.0,
                      "torso-muscle-pectoral-incr": 0.7, "torso-scale-depth-incr": 0.3,
                      "measure-neck-height-decr": 0.9, "neck-scale-horiz-incr": 1.0, "neck-scale-depth-incr": 0.6,
                      "measure-upperarm-length-incr": 0.5, "measure-lowerarm-length-incr": 0.5, "hand-scale-incr": 0.6,
                      "measure-upperleg-height-decr": 0.3, "upperarm-muscle-incr": 1.0, "upperarm-shoulder-muscle-incr": 1.0,
                      "lowerarm-muscle-incr": 1.0, "measure-upperarm-circ-incr": 1.0, "upperleg-muscle-incr": 0.5,
                      "chin-width-incr": 0.8, "chin-prognathism-incr": 0.7, "chin-height-incr": 0.3, "eyebrows-trans-forward": 0.9,
                      "forehead-trans-backward": 0.6, "forehead-scale-vert-decr": 0.5, "nose-flaring-incr": 0.8,
                      "nose-scale-horiz-incr": 0.6, "nose-point-up": 0.5, "nose-scale-vert-decr": 0.4, "ear-scale-incr": 0.3,
                      "ear-rot-backward": 0.3, "mouth-scale-horiz-incr": 0.5, "ear-shape-pointed": 1.0,
                      "hand-fingers-diameter-incr": 1.0, "stomach-pregnant-incr": 0.35, "lowerleg-muscle-incr": 0.8, "foot-scale-incr": 0.5}},
    "oger": {"datei": "passant_probe_oger.glb", "gender": 1.0, "varianten": ("sack",), "hauer": "oger", "kahl": True,
             "arme": {"upperarm": 1.33, "lowerarm": 1.33, "hand": 1.18, "thigh": 1.12, "calf": 1.12},   # Nutzer 09.11.: zwischen v3 und v4
             "makros": {"muscle": 0.8, "weight": 1.0, "height": 0.55, "proportions": 0.0},
             "ziele": {"stomach-pregnant-incr": 1.0, "measure-shoulder-dist-incr": 0.6, "torso-scale-horiz-incr": 1.0,
                       "torso-scale-depth-incr": 1.0, "measure-neck-height-decr": 0.6, "neck-scale-horiz-incr": 0.6,
                       "head-scale-horiz-decr": 0.45, "head-scale-vert-decr": 0.45, "head-fat-incr": 0.5,
                       "measure-upperarm-length-incr": 0.6, "measure-lowerarm-length-incr": 0.6, "hand-scale-incr": 1.0,
                       "foot-scale-incr": 0.5, "upperarm-fat-incr": 1.0, "lowerarm-muscle-incr": 0.8, "upperleg-fat-incr": 0.8,
                       "measure-upperleg-height-decr": 0.4,
                       "chin-width-incr": 1.0, "chin-prognathism-incr": 1.0, "eyebrows-trans-forward": 1.0, "eyebrows-trans-down": 0.6,
                       "forehead-trans-backward": 0.8, "forehead-scale-vert-decr": 0.8, "nose-volume-incr": 0.8,
                       "nose-scale-horiz-incr": 0.9, "nose-flaring-incr": 0.6, "mouth-scale-horiz-incr": 0.6, "mouth-lowerlip-volume-incr": 0.7,
                       "ear-scale-incr": 0.4, "ear-shape-pointed": 1.0, "hand-fingers-diameter-incr": 1.0}},
}

# Zwerge, Elben und Halblinge (Völker-Passanten, Auftrag 10.11.2026): dasselbe Grundnetz, Skelett und dieselben Bewegungen wie bei den Menschen, eigene
# Proportionen über MPFB-Makros und Formziele. Das Modell hat Menschenmaß; das Spiel skaliert jede Figur auf ihre Körpergröße (Zwerg 1,30–1,40 m,
# Elb 1,80–1,90 m, Halbling 1,05–1,15 m, Frauen 5 % kleiner), gemessen an "hoehe_m" im Bericht. Kleidung und Schuhe wie bei Menschen.
#  * Zwerg: stämmig, breite Schultern, kurze kräftige Beine, große Hände, kurzer dicker Hals; Bart als eigene Netze (Zub_bart…), im Spiel nach Erbgut.
#  * Elb: schlank, lange Glieder, schmales Gesicht, dezent spitze Ohren (Haar lässt die Ohren frei), langes Haar (Kappe plus Zub_langhaar am Rücken).
#  * Halbling: rundlich, kurze Glieder, etwas größerer Kopf, erwachsenes Gesicht.
ZWERG_ZIELE = {
    "measure-shoulder-dist-incr": 1.0, "torso-scale-horiz-incr": 1.0, "torso-scale-depth-incr": 0.8, "torso-muscle-pectoral-incr": 0.6, "measure-waist-circ-incr": 0.6, "measure-hips-circ-incr": 0.4,
    "torso-muscle-dorsi-incr": 0.6, "measure-neck-height-decr": 1.0, "measure-neck-circ-incr": 0.9, "neck-scale-horiz-incr": 0.7,
    "measure-upperleg-height-decr": 1.0, "measure-lowerleg-height-decr": 1.0, "measure-upperarm-length-decr": 0.4, "measure-lowerarm-length-decr": 0.5,
    "hand-scale-incr": 0.8, "hand-fingers-diameter-incr": 0.9, "foot-scale-incr": 0.4,
    "upperarm-muscle-incr": 0.8, "upperarm-shoulder-muscle-incr": 0.8, "lowerarm-muscle-incr": 0.8, "upperleg-muscle-incr": 0.7, "lowerleg-muscle-incr": 0.7,
    "chin-width-incr": 0.6, "chin-prognathism-incr": 0.3, "eyebrows-trans-forward": 0.8, "forehead-scale-vert-decr": 0.3, "nose-scale-horiz-incr": 0.5,
    "nose-flaring-incr": 0.4, "head-scale-horiz-incr": 0.3, "stomach-pregnant-incr": 0.3,
}
ZWERGIN_ZIELE = {
    "measure-shoulder-dist-incr": 0.6, "torso-scale-horiz-incr": 0.5, "torso-scale-depth-incr": 0.3, "measure-neck-height-decr": 0.8,
    "neck-scale-horiz-incr": 0.4, "measure-upperleg-height-decr": 1.0, "measure-lowerleg-height-decr": 1.0, "measure-upperarm-length-decr": 0.3,
    "measure-lowerarm-length-decr": 0.4, "hand-scale-incr": 0.6, "hand-fingers-diameter-incr": 0.6, "foot-scale-incr": 0.3,
    "upperarm-muscle-incr": 0.5, "upperleg-fat-incr": 0.4, "chin-width-incr": 0.4, "eyebrows-trans-forward": 0.5, "nose-scale-horiz-incr": 0.3,
    "head-scale-horiz-incr": 0.2, "stomach-pregnant-incr": 0.2, "hip-scale-horiz-incr": 0.4,
}
ELB_ZIELE = {
    "measure-upperleg-height-incr": 0.35, "measure-lowerleg-height-incr": 0.3, "measure-upperarm-length-incr": 0.4, "measure-lowerarm-length-incr": 0.5,
    "measure-shoulder-dist-decr": 0.3, "torso-scale-horiz-decr": 0.4, "torso-scale-depth-decr": 0.4, "measure-neck-height-incr": 0.5,
    "neck-scale-horiz-decr": 0.4, "head-oval": 0.6, "head-scale-horiz-decr": 0.3, "chin-width-decr": 0.5, "chin-prognathism-decr": 0.2,
    "nose-scale-horiz-decr": 0.4, "nose-scale-vert-incr": 0.2, "mouth-scale-horiz-decr": 0.2, "ear-shape-pointed": 0.9, "ear-scale-vert-incr": 0.5, "ear-scale-incr": 0.2,
    "hand-fingers-length-incr": 0.5, "hand-fingers-diameter-decr": 0.3, "foot-scale-depth-incr": 0.2, "cheek-volume-decr": 0.5,
}
ELBIN_ZIELE = dict(ELB_ZIELE, **{"measure-shoulder-dist-decr": 0.1, "torso-scale-horiz-decr": 0.2, "torso-scale-depth-decr": 0.2})
HALBLING_ZIELE = {
    "measure-upperleg-height-decr": 0.9, "measure-lowerleg-height-decr": 0.9, "measure-upperarm-length-decr": 0.6, "measure-lowerarm-length-decr": 0.6,
    "head-scale-horiz-incr": 0.4, "head-scale-vert-incr": 0.4, "head-scale-depth-incr": 0.3, "head-fat-incr": 0.6, "cheek-volume-incr": 0.9,
    "stomach-pregnant-incr": 1.0, "torso-scale-depth-incr": 0.8, "torso-scale-horiz-incr": 0.8, "measure-waist-circ-incr": 0.8, "measure-hips-circ-incr": 0.6, "measure-neck-height-decr": 0.6, "neck-scale-horiz-incr": 0.4,
    "hand-scale-incr": 0.3, "foot-scale-incr": 0.6, "foot-scale-horiz-incr": 0.3, "nose-scale-horiz-incr": 0.2, "nose-point-up": 0.4,
    "upperarm-fat-incr": 0.6, "upperleg-fat-incr": 0.7, "buttocks-volume-incr": 0.6,
}
HALBLINGIN_ZIELE = dict(HALBLING_ZIELE, **{"stomach-pregnant-incr": 0.35, "hip-scale-horiz-incr": 0.4})
MAENNER = ("cotte", "gugel", "rock", "bettler", "kutte")
FRAUEN = ("haube", "gebende", "bettler")
FIGUREN.update({
    "zwerg": {"datei": "passant_probe_zwerg.glb", "gender": 1.0, "varianten": MAENNER, "bart": True, "bartnetze": ("voll", "zopf", "gabel"), "zeigen": ("bart",), "zeigen_bei": MAENNER, "vorschau_haar": (0.50, 0.25, 0.12),
              "makros": {"muscle": 0.95, "weight": 0.85, "height": 0.3, "proportions": 0.4}, "ziele": ZWERG_ZIELE,
              "arme": {"upperarm": 1.35, "lowerarm": 1.3, "hand": 1.25, "thigh": 1.3, "calf": 1.25}},
    "zwergin": {"datei": "passant_probe_zwergin.glb", "gender": 0.0, "varianten": FRAUEN, "bartnetze": ("kurz", "zopf"), "zeigen": ("bartkurz",), "zeigen_bei": FRAUEN,
                "makros": {"muscle": 0.6, "weight": 0.65, "height": 0.35, "proportions": 0.4}, "ziele": ZWERGIN_ZIELE,
                "arme": {"upperarm": 1.12, "lowerarm": 1.12, "hand": 1.12, "thigh": 1.12, "calf": 1.12}},
    "elb": {"datei": "passant_probe_elb.glb", "gender": 1.0, "varianten": MAENNER, "haarart": "elb", "langhaar": True, "zeigen": ("langhaar",), "zeigen_bei": ("cotte", "rock", "bettler"), "vorschau_haar": (0.80, 0.70, 0.45),
            "makros": {"muscle": 0.3, "weight": 0.2, "height": 0.55, "proportions": 0.8}, "ziele": ELB_ZIELE},
    "elbin": {"datei": "passant_probe_elbin.glb", "gender": 0.0, "varianten": FRAUEN + ("offen",), "haarart": "elb", "langhaar": True, "zeigen": ("langhaar",), "zeigen_bei": ("offen",), "vorschau_haar": (0.80, 0.70, 0.45),
              "makros": {"muscle": 0.3, "weight": 0.2, "height": 0.5, "proportions": 0.9}, "ziele": ELBIN_ZIELE},
    "halbling": {"datei": "passant_probe_halbling.glb", "gender": 1.0, "varianten": MAENNER, "bart": True,
                 "makros": {"muscle": 0.35, "weight": 1.0, "height": 0.4, "proportions": 0.4}, "ziele": HALBLING_ZIELE,
                 "arme": {"upperarm": 1.1, "lowerarm": 1.08, "hand": 1.1, "thigh": 1.1, "calf": 1.05}},
    "halblingin": {"datei": "passant_probe_halblingin.glb", "gender": 0.0, "varianten": FRAUEN,
                   "makros": {"muscle": 0.3, "weight": 1.0, "height": 0.4, "proportions": 0.4}, "ziele": HALBLINGIN_ZIELE,
                   "arme": {"upperarm": 1.08, "lowerarm": 1.06, "hand": 1.08, "thigh": 1.08, "calf": 1.05}},
})
# Größe im Spiel (Meter, Mitte der Spanne; Frauen 5 % kleiner): das Spiel setzt den Maßstab = Größe ÷ hoehe_m des Modells
ZIEL_HOEHE = {"zwerg": 1.35, "zwergin": 1.35 * 0.95, "elb": 1.85, "elbin": 1.85 * 0.95, "halbling": 1.10, "halblingin": 1.10 * 0.95}

# Vergleichsbild der Völker (Schritt 3): Größe im Spiel je Volk (FigurMasse, Größe Y), Menschen 1
VERGLEICH_MASS = {"mann": 1.0, "frau": 1.0, "ork": 1.1, "oger": 1.35}
VERGLEICH = []

# Hautfarbe der Vorschau je Volk (sRGB); das Spiel setzt seine eigene (ProbeHaut)
VORSCHAU_HAUT = {"ork": (0.42, 0.50, 0.30), "oger": (0.60, 0.54, 0.45)}

# Fußformen: eigene Netze, für nah und fern dieselben (Schritt 1 des Vollausbaus): Halbschuh aus Leder (Bürger), Holzschuh (Arme, Bauern),
# barfuß (nur Bettler)
FUSSFORMEN = ("halb", "holz", "barfuss")
FUSS_ZIEL = {"halb": 200, "holz": 160, "barfuss": 220, "schuerze": 320, "bart": 220
}

# Vorschaufarben (sRGB, gedeckte Pflanzenfarben) je Zone und Variante – nur für die Bilder, das Spiel setzt die Farben selbst
VORSCHAU = {
    "cotte": {1: (0.45, 0.22, 0.17), 2: (0.30, 0.26, 0.21), 3: (0.72, 0.52, 0.40), 4: (0.20, 0.13, 0.08), 5: (0.6, 0.5, 0.3)},
    "gugel": {1: (0.30, 0.36, 0.24), 2: (0.24, 0.24, 0.25), 3: (0.72, 0.52, 0.40), 4: (0.22, 0.15, 0.10), 5: (0.43, 0.20, 0.15)},
    "rock": {1: (0.24, 0.30, 0.40), 2: (0.34, 0.28, 0.19), 3: (0.72, 0.52, 0.40), 4: (0.48, 0.26, 0.13), 5: (0.6, 0.5, 0.3)},
    "haube": {1: (0.30, 0.40, 0.52), 2: (0.30, 0.26, 0.21), 3: (0.74, 0.55, 0.44), 4: (0.33, 0.22, 0.13), 5: (0.70, 0.52, 0.22)},
    "gebende": {1: (0.32, 0.22, 0.26), 2: (0.30, 0.26, 0.21), 3: (0.74, 0.55, 0.44), 4: (0.33, 0.22, 0.13), 5: (0.56, 0.60, 0.66)},
    "bettler": {1: (0.36, 0.34, 0.30), 2: (0.28, 0.25, 0.21), 3: (0.70, 0.52, 0.41), 4: (0.24, 0.18, 0.12), 5: (0.50, 0.46, 0.38)},
    "wams": {1: (0.34, 0.24, 0.15), 2: (0.28, 0.24, 0.20), 3: (0.72, 0.52, 0.40), 4: (0.10, 0.09, 0.08), 5: (0.34, 0.24, 0.15)},
    "sack": {1: (0.62, 0.50, 0.30), 2: (0.28, 0.24, 0.20), 3: (0.72, 0.52, 0.40), 4: (0.10, 0.09, 0.08), 5: (0.50, 0.45, 0.35)},
    "kutte": {1: (0.36, 0.24, 0.14), 2: (0.28, 0.24, 0.20), 3: (0.72, 0.52, 0.40), 4: (0.22, 0.15, 0.10), 5: (0.36, 0.24, 0.14)},
}


def optionen():
    aus = {}
    if "--" in sys.argv:
        for a in sys.argv[sys.argv.index("--") + 1:]:
            if a.startswith("--") and "=" in a:
                k, v = a[2:].split("=", 1)
                aus[k] = v
    return aus


OPT = optionen()
BERICHT = {"quellen": {}}


def bibliothek():
    """Schichtnummern der Material-Bibliothek (Reihenfolge der Schichten in materialbibliothek.json)."""
    with open(os.path.join(REPO, "game", "daten", "materialbibliothek.json"), encoding="utf-8") as f:
        daten = json.load(f)
    return {s["name"]: i for i, s in enumerate(daten["schichten"])}


BIB = bibliothek()


def holen(paket, name):
    for m in list(sys.modules):
        if m.endswith(paket):
            return getattr(importlib.import_module(m), name)
    raise RuntimeError("MPFB nicht gefunden: " + paket)


def aktiv(obj):
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        if o is not None:
            o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def dreiecke(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def neues_objekt(name, me):
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    return obj


def kopie(quelle, name):
    neu = quelle.copy()
    neu.data = quelle.data.copy()
    neu.name = neu.data.name = name
    bpy.context.collection.objects.link(neu)
    return neu


# ─────────────────────────────────────────────────────────────────────────────
#  1. Mensch aus MPFB
# ─────────────────────────────────────────────────────────────────────────────

def mensch_bauen(gender, makros_extra=None, ziele=None):
    addon_utils.enable("bl_ext.user_default.mpfb", default_set=True, persistent=False)
    mpfb = sys.modules["bl_ext.user_default.mpfb"]
    BERICHT["quellen"]["mpfb"] = str(getattr(mpfb, "VERSION", "2.0.18"))
    ts = holen("mpfb.services.targetservice", "TargetService")
    hs = holen("mpfb.services.humanservice", "HumanService")
    makros = dict(ts.get_default_macro_info_dict())
    # etwa 25 Jahre, mittlere Kraft und Statur; Herkunft wie bei den Porträtköpfen (charakter-werkzeug)
    makros.update({"gender": gender, "age": 0.5, "muscle": 0.5, "weight": 0.5, "height": 0.5, "proportions": 0.5})
    makros["race"] = {"caucasian": 1.0, "asian": 0.0, "african": 0.0}
    makros.update(makros_extra or {})
    mensch = hs.create_human(mask_helpers=False, detailed_helpers=True, extra_vertex_groups=True,
                             feet_on_ground=True, macro_detail_dict=makros)
    # Formziele (Orks, Oger) vor dem Skelett, damit das Skelett auf die neue Form passt; „hand-scale-incr“ usw. ohne Seite gilt für beide
    for name, wert in (ziele or {}).items():
        namen = [name] if name.startswith(("measure", "torso", "neck", "head", "chin", "eyebrows", "forehead", "nose", "mouth",
                                           "stomach", "lowerlegs", "upperlegs", "hip", "buttocks", "pelvis")) else [f"l-{name}", f"r-{name}"]
        if name == "measure-wrist-circ-incr":
            namen = [name]
        for n in namen:
            pfad = ts.target_full_path(n)
            if pfad is None:
                raise RuntimeError("Formziel fehlt: " + n)
            ts.load_target(mensch, pfad, weight=wert, name=n)
    if ziele:
        BERICHT.setdefault("formziele", {})[str(gender)] = dict(ziele)
    rig = hs.add_builtin_rig(mensch, "game_engine")
    ts.bake_targets(mensch)
    for m in list(mensch.modifiers):
        if m.type != "ARMATURE":
            mensch.modifiers.remove(m)
    return mensch, rig, makros


def glieder_verdicken(mensch, rig, faktoren):
    """Massige Glieder (Orks, Oger, Vorlage des Nutzers 09.11.): Arme und Beine rücken radial von der Achse ihres Knochens weg (Faktor nach den
    Gewichten gemischt, Gelenke gehen weich über); die Hand samt Fingern wird als Ganzes um das Handgelenk vergrößert (radial verschoben
    wurden die Finger zu Splittern). Die Formziele von MPFB allein ließen die Arme dünn."""
    namen = gruppen_index(mensch)
    achsen = {}
    for b in rig.data.bones:
        for basis, f in faktoren.items():
            if basis != "hand" and b.name in (f"{basis}_l", f"{basis}_r"):
                achsen[b.name] = (b.head_local.copy(), b.tail_local.copy(), f)
    gelenk = {s: rig.data.bones[f"hand_{s}"].head_local.copy() for s in ("l", "r")}
    flaeche = {}
    for s, vz in (("l", 1.0), ("r", -1.0)):
        f_ = (rig.data.bones[f"middle_01_{s}"].head_local - gelenk[s]).normalized()
        q = Vector((-vz, -0.3, 0.0))                       # Handfläche zum Oberschenkel (wie haende_entspannen)
        flaeche[s] = (q - f_ * q.dot(f_)).normalized()
    hand_f = faktoren.get("hand", 1.0)
    me = mensch.data
    for v in me.vertices:
        verschiebung = Vector()
        hand_w = {"l": 0.0, "r": 0.0}
        for e in v.groups:
            n = namen.get(e.group)
            if n is None or e.weight <= 0:
                continue
            if n in achsen:
                a, b, f = achsen[n]
                d = b - a
                u = max(0.0, min(1.0, (v.co - a).dot(d) / d.length_squared))
                # Muskelform: Oberarm in der Mitte am dicksten (Bizeps, Schulter), Unterarm am Ellbogen dick, zum Handgelenk schmaler
                if n.startswith("upperarm"):
                    form = 0.75 + 0.5 * math.sin(math.pi * u)
                elif n.startswith("lowerarm"):
                    form = 1.25 - 0.6 * u
                else:
                    form = 1.0
                verschiebung += (v.co - (a + d * u)) * (f - 1.0) * form * e.weight
            elif n.startswith(("hand_",) + FINGER) and n[-2:] in ("_l", "_r"):
                hand_w[n[-1]] += e.weight
        for s in ("l", "r"):
            if hand_w[s] > 0:
                w = min(hand_w[s], 1.0)
                verschiebung += (v.co - gelenk[s]) * (hand_f - 1.0) * w
                # Handfläche dicker (sonst flach): quer zur Handebene um 25 % mehr
                quer = flaeche[s]
                verschiebung += quer * (v.co - gelenk[s]).dot(quer) * 0.25 * w
        v.co += verschiebung
    me.update()


def haende_entspannen(mensch, rig):
    """Die Hände des Grundnetzes sind flach mit gespreizten Fingern (Klicktest 08.11.: „gespreizte Hände“). Die Finger werden locker
    zur Handfläche gekrümmt und diese Haltung ins Netz übernommen (neue Ruhelage); danach gehen die Fingergewichte in die Hand."""
    kn = rig.data.bones
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion()

    def drehen(name, achse, grad):
        r = kn[name].matrix_local.to_3x3()
        lokal = (r.inverted() @ achse).normalized()
        pb = rig.pose.bones[name]
        pb.rotation_quaternion = Quaternion(lokal, math.radians(grad)) @ pb.rotation_quaternion

    beugen = {"index": (14, 20, 12), "middle": (16, 22, 13), "ring": (18, 24, 15), "pinky": (21, 26, 17)}   # Vorlage 09.11.: natürlich gekrümmt
    for s, vorzeichen in (("l", 1.0), ("r", -1.0)):
        f = (kn[f"middle_03_{s}"].tail_local - kn[f"hand_{s}"].head_local).normalized()
        handflaeche = Vector((-vorzeichen, -0.3, 0.0))          # die Hände hängen mit der Fläche zum Oberschenkel
        handflaeche = (handflaeche - f * handflaeche.dot(f)).normalized()
        achse = f.cross(handflaeche).normalized()                # Drehung um diese Achse krümmt zur Handfläche
        for finger, winkel in beugen.items():
            for g, w in enumerate(winkel):
                drehen(f"{finger}_0{g + 1}_{s}", achse, w)
        for g, w in enumerate((6, 10, 8)):
            drehen(f"thumb_0{g + 1}_{s}", achse, w)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mensch.evaluated_get(dg)
    me = ev.to_mesh()
    lagen = [0.0] * (len(me.vertices) * 3)
    me.vertices.foreach_get("co", lagen)
    ev.to_mesh_clear()
    if len(lagen) == len(mensch.data.vertices) * 3:
        if mensch.data.shape_keys:
            mensch.shape_key_clear()
        mensch.data.vertices.foreach_set("co", lagen)
        mensch.data.update()
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion()
    bpy.context.view_layer.update()


def gruppen_index(obj):
    return {g.index: g.name for g in obj.vertex_groups}


def teil_kopieren(quelle, name, behalten):
    """Kopie von quelle mit nur den Ecken, für die behalten(ecke, gruppen) wahr ist."""
    neu = kopie(quelle, name)
    namen = gruppen_index(neu)
    bm = bmesh.new()
    bm.from_mesh(neu.data)
    deform = bm.verts.layers.deform.active
    weg = [v for v in bm.verts if not behalten(v, {namen[i]: w for i, w in v[deform].items()} if deform else {})]
    bmesh.ops.delete(bm, geom=weg, context="VERTS")
    bm.to_mesh(neu.data)
    bm.free()
    return neu


def in_gruppe(name, grenze=0.5):
    return lambda v, g: g.get(name, 0.0) > grenze


def knochen_von(v, namen, deform):
    """Herrschender Knochen einer Ecke (Finger zählen als Hand)."""
    beste, bw = "", 0.0
    for i, w in v[deform].items():
        n = namen.get(i, "")
        if n.split("_")[0] in FINGER:
            n = "hand_" + n[-1]
        if n in KNOCHEN and w > bw:
            beste, bw = n, w
    return beste


def stoffe_anlegen(obj):
    obj.data.materials.clear()
    for name in STOFF_NAMEN:
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        obj.data.materials.append(m)


def stoff(name):
    return STOFF_NAMEN.index(name)


# ─────────────────────────────────────────────────────────────────────────────
#  2. Saubere Schnittlinien
# ─────────────────────────────────────────────────────────────────────────────

def isolinie_schneiden(bm, feld):
    """Schneidet das Netz entlang der Nulllinie von feld(ecke) (None = Ecke nicht betroffen): Kanten mit Vorzeichenwechsel
    bekommen eine neue Ecke auf der Nulllinie, die neuen Ecken jeder Fläche werden verbunden. Die Gewichte der neuen Ecken
    mischt Blender aus den Enden der Kante. Danach liegt jede Fläche ganz auf einer Seite."""
    werte = {v: feld(v) for v in bm.verts}
    neue = set()
    for e in list(bm.edges):
        a, b = e.verts
        fa, fb = werte.get(a), werte.get(b)
        if fa is None or fb is None or (fa > 0) == (fb > 0) or abs(fa) < 1e-7 or abs(fb) < 1e-7:
            continue
        t = fa / (fa - fb)
        ziel = a.co.lerp(b.co, t)
        _, v = bmesh.utils.edge_split(e, a, t)
        v.co = ziel
        werte[v] = 0.0
        neue.add(v)
    for f in list(bm.faces):
        auf = [v for v in f.verts if v in neue]
        if len(auf) == 2 and not any(a in auf and b in auf for a, b in (e.verts for e in f.edges)):
            try:
                bmesh.ops.connect_verts(bm, verts=auf)
            except Exception:
                pass
    bm.normal_update()
    return werte


def seite(f, feld):
    """Wert des Feldes in der Mitte einer Fläche (Mittel ihrer Ecken; nach dem Schnitt hat jede Fläche ein Vorzeichen)."""
    werte = [feld(v) for v in f.verts]
    werte = [w for w in werte if w is not None]
    return sum(werte) / len(werte) if werte else None


def dicken(bm, dicke_von, glaetten=()):
    """Verschiebt jede Ecke um das Mittel der Dicken ihrer Flächen entlang der Normalen; danach glätten (Stoff, Schritte)."""
    bm.normal_update()
    versatz = []
    for v in bm.verts:
        ds = [dicke_von(STOFF_NAMEN[f.material_index]) for f in v.link_faces]
        versatz.append(sum(ds) / len(ds) if ds else 0.0)
    for v, d in zip(bm.verts, versatz):
        if d:
            v.co += v.normal * d
    for name, schritte in glaetten:
        i = stoff(name)
        innen = [v for v in bm.verts if v.link_faces and all(f.material_index == i for f in v.link_faces)]
        for _ in range(schritte):
            bmesh.ops.smooth_vert(bm, verts=innen, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)


# ─────────────────────────────────────────────────────────────────────────────
#  3. Körper, Kleidung, Schuhe
# ─────────────────────────────────────────────────────────────────────────────

def koerper_zonen(koerper, masse):
    """Ordnet jeder Fläche des Körpers einen Stoff zu (Haut, Gewand, Beinkleid, Gürtel) mit sauberen Schnittlinien:
    runder Halsausschnitt, Gürtel als Band, Ärmel bis 1 cm vor dem Handgelenk; die Füße kommen weg (Schuhe, siehe schuh_bauen)."""
    stoffe_anlegen(koerper)
    namen = gruppen_index(koerper)
    bm = bmesh.new()
    bm.from_mesh(koerper.data)
    deform = bm.verts.layers.deform.active
    kn = {v: knochen_von(v, namen, deform) for v in bm.verts}

    def knochen(v):
        if v not in kn:
            kn[v] = knochen_von(v, namen, deform)
        return kn[v]

    # Halsausschnitt: Höhe vorn am Brustbein, hinten am Halsansatz, zu den Seiten hin steigend (runder Ausschnitt)
    hals = masse["hals_kopf"]
    z_vorn, z_hinten = masse["schluesselbein"] - 0.012, masse["hals_fuss"] + 0.01

    def ausschnitt(v):
        if knochen(v) not in ("neck_01", "head", "spine_03", "clavicle_l", "clavicle_r", "spine_02"):
            return None
        x, y, z = v.co
        if x * x + (y - hals.y) ** 2 > 0.2 ** 2:
            return None
        u = min(max((y - (hals.y - 0.07)) / 0.14, 0.0), 1.0)
        return z - (z_vorn + (z_hinten - z_vorn) * u * u * (3 - 2 * u) + 3.0 * x * x)

    def guertel(v):
        if knochen(v) not in ("pelvis", "spine_01", "spine_02") or abs(v.co.x) > 0.3:
            return None
        return abs(v.co.z - masse["guertel"]) - 0.02

    def manschette(v, s):
        if knochen(v) not in (f"lowerarm_{s}", f"hand_{s}"):
            return None
        return (v.co - masse[f"handgelenk_{s}"]).dot(masse[f"unterarm_{s}"]) + 0.012

    def knoechel(v):
        if knochen(v) not in ("calf_l", "calf_r", "foot_l", "foot_r", "ball_l", "ball_r"):
            return None
        return v.co.z - (masse["knoechel"] - 0.004)

    # Der Gürtel sitzt auf dem Rock (rock_bauen): am Körper liegt er darunter und bleibt Gewand
    for feld in (ausschnitt, lambda v: manschette(v, "l"), lambda v: manschette(v, "r"), knoechel):
        isolinie_schneiden(bm, feld)

    weg = []
    for f in bm.faces:
        zaehler = {}
        for v in f.verts:
            zaehler[knochen(v)] = zaehler.get(knochen(v), 0) + 1
        k = max(zaehler, key=zaehler.get)
        hoehe = seite(f, knoechel)
        if hoehe is not None and hoehe < 0:
            weg.append(f)                       # im Schuh
            continue
        if k.startswith(("head", "neck")):
            name = "Haut"
        elif k.startswith("hand"):
            name = "Haut"
        elif k.startswith(("thigh", "calf", "foot", "ball")):
            name = "Beinkleid"
        else:
            name = "Gewand"
        a = seite(f, ausschnitt)
        if a is not None:
            name = "Haut" if a > 0 else ("Gewand" if name != "Beinkleid" else name)
        for s in ("l", "r"):
            m = seite(f, lambda v: manschette(v, s))
            if m is not None:
                name = "Haut" if m > 0 else "Gewand"
        f.material_index = stoff(name)
    # Die Füße unter dem Knöchelschnitt als eigenes Netz (barfuß, nur Bettler): Haut, sonst wie der Körper
    bm.faces.index_update()
    im_schuh = {f.index for f in weg}
    bm_fuss = bm.copy()
    bm_fuss.faces.ensure_lookup_table()
    bmesh.ops.delete(bm_fuss, geom=[f for f in bm_fuss.faces if f.index not in im_schuh], context="FACES")
    bmesh.ops.delete(bm_fuss, geom=[v for v in bm_fuss.verts if not v.link_faces], context="VERTS")
    for f in bm_fuss.faces:
        f.material_index = stoff("Haut")
    fuesse = kopie(koerper, "Fuesse")
    bm_fuss.to_mesh(fuesse.data)
    bm_fuss.free()
    bmesh.ops.delete(bm, geom=weg, context="FACES")
    dicke = {"Gewand": 0.012, "Guertel": 0.020, "Beinkleid": 0.006}     # Beinlinge locker (Vorlage 09.11.)
    dicken(bm, lambda n: dicke.get(n, 0.0), (("Gewand", 8), ("Beinkleid", 2)))
    bm.to_mesh(koerper.data)
    bm.free()
    return fuesse


def koerper_aussparen(koerper, unten, oben, name):
    """Kopie des Körpers ohne Becken und Beine zwischen unten und oben (Meter): Unter dem Rock sieht man sie nie, beim Gehen
    stachen die Oberschenkel sonst durch den Stoff; spart nebenbei Dreiecke."""
    neu = kopie(koerper, name)
    namen = gruppen_index(neu)
    bm = bmesh.new()
    bm.from_mesh(neu.data)
    deform = bm.verts.layers.deform.active
    bm.verts.ensure_lookup_table()
    kn = [knochen_von(v, namen, deform) for v in bm.verts]
    weg = []
    for f in bm.faces:
        zaehler = {}
        for v in f.verts:
            zaehler[kn[v.index]] = zaehler.get(kn[v.index], 0) + 1
        k = max(zaehler, key=zaehler.get)
        if k.startswith(("pelvis", "thigh", "calf")) and unten < f.calc_center_median().z < oben:
            weg.append(f)
    bmesh.ops.delete(bm, geom=weg, context="FACES")
    bm.to_mesh(neu.data)
    bm.free()
    return neu


def gesicht_wert(co, masse, art):
    """Gugel und Gebende: Ellipse vor dem Gesicht (nur die vordere Hälfte des Kopfes); < 0 = Öffnung, > 0 = bedeckt."""
    k = masse["kopfmitte"]
    if co.y > k.y - 0.01:
        return 1.0
    if art == "gugel":
        ax, zu, zo = 0.068, masse["kinn"] - 0.005, masse["stirn"] - 0.005
    elif art == "gebende":
        ax, zu, zo = 0.055, masse["kinn"] + 0.018, masse["stirn"] - 0.02
    elif art == "haube":
        ax, zu, zo = 0.066, masse["kinn"] - 0.075, masse["stirn"] - 0.014   # Kinn frei, Wangen und Ohren bedeckt (Vorlage 09.11.)
    else:
        return 1.0
    zm, az = (zu + zo) / 2, (zo - zu) / 2
    return (co.x / ax) ** 2 + ((co.z - zm) / az) ** 2 - 1.0


def kappe_wert(co, masse, art):
    """Haube und Haar: über einer schrägen Ebene von der Stirn (vorn) zum Nacken (hinten); > 0 = bedeckt. Haar: Ansatz höher, im Nacken kürzer."""
    k = masse["kopfmitte"]
    if art in ("haar", "haarelb"):
        # Vorlage 09.11.: mittellang, über die Ohren und in den Nacken
        vorn, hinten = masse["stirn"] - 0.006, masse["hals_fuss"] + 0.035
    else:
        vorn, hinten = masse["stirn"] - 0.02, masse["hals_fuss"] + 0.01
    u = min(max((co.y - (k.y - 0.09)) / 0.18, 0.0), 1.0)
    u = u * u * (3 - 2 * u)
    return co.z - (vorn + (hinten - vorn) * u)


def kopf_aussparen(koerper, masse, art, name):
    """Kopie des Körpers ohne die Kopf- und Halsflächen, die Haar, Haube, Gugel oder Gebende ganz bedecken (mit Rand zur Öffnung): Sonst stach
    nach dem Verkleinern die Haut als helle Pünktchen durch die Kopfbedeckung (Klicktest 08.11.: orange Punkte an der Gugel)."""
    neu = kopie(koerper, name)
    namen = gruppen_index(neu)
    bm = bmesh.new()
    bm.from_mesh(neu.data)
    deform = bm.verts.layers.deform.active

    def kopfanteil(v):
        g = {namen[i]: w for i, w in v[deform].items()}
        return g.get("head", 0) + g.get("neck_01", 0)

    ansatz = None
    if art == "haube":
        # Haaransatz (NAS 09.11.): ein Streifen Haar unter dem Haubenrand vorn und an den Schläfen, sauber geschnitten
        ansatz = lambda v: gesicht_wert(v.co, masse, art) + 0.22 if kopfanteil(v) > 0.3 else None
        isolinie_schneiden(bm, ansatz)
    weg = []
    for f in bm.faces:
        c = f.calc_center_median()
        if ansatz is not None and seite(f, ansatz) is not None and seite(f, ansatz) > 0 and c.y < masse["kopfmitte"].y + 0.02 \
                and c.z > masse["stirn"] - 0.075 and gesicht_wert(c, masse, art) <= 0.35:
            f.material_index = stoff("Haar")
        if sum(kopfanteil(v) for v in f.verts) / len(f.verts) < 0.6:
            # Gugel: auch Schultern und Brust unter dem Schulterkragen (Runde 4: rotbraune Pünktchen = Cotte durch den Kragen)
            if art == "gugel" and c.z > masse["kragen"] + 0.03 and gesicht_wert(c, masse, art) > 0.35:
                weg.append(f)
            # Gebende: Nacken und Schultern unter dem Schleier (hinten)
            elif art == "gebende" and c.y > masse["kopfmitte"].y + 0.01 and c.z > masse["schluesselbein"] - 0.04 and abs(c.x) < 0.2:
                weg.append(f)
            continue
        if art in ("gugel", "gebende"):
            bedeckt = gesicht_wert(c, masse, art) > 0.35
        elif art == "haube":
            bedeckt = gesicht_wert(c, masse, art) > 0.35 and c.z > masse["hals_fuss"] + 0.05
        else:
            bedeckt = kappe_wert(c, masse, art) > 0.015
            if art == "haarelb" and ohr_abstand(c, masse) < 0.012:
                bedeckt = False         # Ohren frei: dort bleibt die Haut
        if bedeckt:
            weg.append(f)
    bmesh.ops.delete(bm, geom=weg, context="FACES")
    bm.to_mesh(neu.data)
    bm.free()
    return neu


def kopfteil_bauen(koerper, masse, name, art):
    """Kopfbedeckung aus den Kopf- und Halsflächen des Körpers, aufgedickt, mit glatt geschnittener Gesichtsöffnung:
    „gugel“ (Kapuze mit Schulterkragen, Zipfel), „gebende“ (eng um Kopf und Kinn, Schleier über den Nacken bis auf die Schultern),
    „haube“ (Leinenhaube über Scheitel und Hinterkopf, Gesicht und Ohren frei)."""
    k = masse["kopfmitte"]
    obj = kopie(koerper, name)
    namen = gruppen_index(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    deform = bm.verts.layers.deform.active

    def gruppen(v):
        return {namen[i]: w for i, w in v[deform].items()}

    def kopfanteil(v):
        g = gruppen(v)
        return g.get("head", 0) + g.get("neck_01", 0)

    def gesicht(v):
        return gesicht_wert(v.co, masse, art)

    def haube(v):
        return kappe_wert(v.co, masse, art)

    def kragen(v):
        return v.co.z - masse["kragen"]

    def schleier_hoehe(v):
        return v.co.z - (masse["schluesselbein"] - 0.07)

    def schleier_tiefe(v):
        return v.co.y - (k.y - 0.02)

    def haube_unten(v):
        return v.co.z - (masse["hals_fuss"] + 0.03)

    if art in ("haar", "haarelb"):
        isolinie_schneiden(bm, lambda v: haube(v) if kopfanteil(v) > 0.3 else None)
        if art == "haarelb":
            isolinie_schneiden(bm, lambda v: ohr_abstand(v.co, masse) if kopfanteil(v) > 0.3 else None)
    else:
        isolinie_schneiden(bm, lambda v: gesicht(v) if kopfanteil(v) > 0.3 else None)
    if art == "haube":
        isolinie_schneiden(bm, lambda v: haube_unten(v) if kopfanteil(v) > 0.3 else None)
    if art == "gugel":
        isolinie_schneiden(bm, lambda v: kragen(v) if kopfanteil(v) <= 0.3 else None)
    if art == "gebende":
        isolinie_schneiden(bm, lambda v: schleier_hoehe(v) if kopfanteil(v) <= 0.3 else None)
        isolinie_schneiden(bm, lambda v: schleier_tiefe(v) if kopfanteil(v) <= 0.3 else None)
    weg = []
    for f in bm.faces:
        c = f.calc_center_median()
        anteil = sum(kopfanteil(v) for v in f.verts) / len(f.verts)
        if art == "gugel":
            drin = (anteil > 0.3 or seite(f, kragen) > 0) and seite(f, gesicht) > 0
        elif art == "gebende":
            schleier = seite(f, schleier_tiefe) > 0 and seite(f, schleier_hoehe) > 0 and abs(c.x) < 0.24
            drin = (anteil > 0.3 and seite(f, gesicht) > 0) or schleier
        elif art == "haube":
            drin = anteil > 0.3 and seite(f, gesicht) > 0 and seite(f, haube_unten) > 0
        else:
            drin = anteil > 0.3 and seite(f, haube) > 0
            if art == "haarelb":
                drin = drin and seite(f, lambda v: ohr_abstand(v.co, masse)) > 0
        if not drin:
            weg.append(f)
    bmesh.ops.delete(bm, geom=weg, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    stoffe_anlegen(obj)
    for f in bm.faces:
        f.material_index = stoff("Haar" if art in ("haar", "haarelb") else "Kopf")
    bm.normal_update()
    neu = []
    for v in bm.verts:
        if art == "gugel":
            d = 0.022 if v.co.z > masse["hals_kopf"].z else 0.030
        elif art == "gebende":
            d = 0.012 if v.co.z > masse["hals_kopf"].z else 0.018
        elif art in ("haar", "haarelb"):
            d = 0.018 + 0.016 * _zerzaust(v.co)      # zerzaust: ungleich dick
        else:
            d = 0.017      # Haube: Leinen mit etwas Fülle über dem Haar
        neu.append(v.co + v.normal * d)
    for v, c in zip(bm.verts, neu):
        v.co = c
    for _ in range(7 if art == "haube" else 3):   # Haube: der Rand an den Ohren war unruhig (Runde 4)
        bmesh.ops.smooth_vert(bm, verts=[v for v in bm.verts if not v.is_boundary], factor=0.5,
                              use_axis_x=True, use_axis_y=True, use_axis_z=True)
    if art == "haube":
        saum_umschlagen(bm, k)
    bm.to_mesh(obj.data)
    bm.free()
    teile = [obj]
    if art == "gugel":
        teile.append(zipfel_bauen(obj, masse))
    return teile


def saum_umschlagen(bm, mitte, tiefe=0.014, wulst=0.003):
    """Saum der Haube (NAS 09.11.): Der offene Rand wird etwas aufgeworfen (Wulst) und als schmales Band nach innen zur Haut umgeschlagen. Das Band
    ist beidseitig (zwei Flächenlagen), weil es je nach Blick von innen oder außen zu sehen ist; so zeigt die Haube eine dunkle Kante statt
    unmittelbar in die Haut überzugehen."""
    bm.normal_update()
    rand = [e for e in bm.edges if e.is_boundary]
    alt = {v for e in rand for v in e.verts}
    richtung = {v: (v.co - mitte).normalized() for v in alt}   # vom Kopfmittelpunkt aus: gleichmäßiger als die Eckennormalen (Runde 5: Saum zerknittert)
    for v in alt:
        v.co += richtung[v] * wulst
    erg = bmesh.ops.extrude_edge_only(bm, edges=rand)
    neu = [g for g in erg["geom"] if isinstance(g, bmesh.types.BMVert)]
    for nv in neu:
        quelle = next((e.other_vert(nv) for e in nv.link_edges if e.other_vert(nv) in alt), None)
        if quelle is not None:
            nv.co = quelle.co - richtung[quelle] * tiefe + Vector((0, 0, -0.003))
    band = [g for g in erg["geom"] if isinstance(g, bmesh.types.BMFace)]
    for f in band:
        f.material_index = stoff("Leinen")    # Vorlage 09.11.: weißer Innenrand der Haube
    kopie_ = bmesh.ops.duplicate(bm, geom=band)
    bmesh.ops.reverse_faces(bm, faces=[g for g in kopie_["geom"] if isinstance(g, bmesh.types.BMFace)])
    bm.normal_update()


def _zerzaust(c):
    """Fester Zufall 0…1 aus der Lage (Strähnen etwa 3 cm), damit jeder Lauf dasselbe Haar baut."""
    return (math.sin(c.x * 211.0 + c.z * 97.0) * math.sin(c.y * 157.0 - c.z * 61.0) * 0.5 + 0.5)


def saum_fransen(obj, tiefe=0.018):
    """Leicht ausgefranster Saum (Cotte der Vorlage): die untersten Ecken ungleich tief."""
    me = obj.data
    unten = min(v.co.z for v in me.vertices)
    for v in me.vertices:
        if v.co.z < unten + 0.03:
            w = math.atan2(v.co.x, v.co.y)
            v.co.z -= tiefe * (0.5 + 0.5 * math.sin(w * 17.0) * math.sin(w * 7.0 + 0.7)) * (1 - (v.co.z - unten) / 0.03)
    me.update()
    return obj


def bart_zone(koerper, masse):
    """Bart bzw. Stoppeln (Vorlage 09.11.) als Zone 6 der Haut: Kinn, Kiefer und Oberlippe, mit glatt geschnittenem oberem Rand. Ob ein Mann
    einen Bart trägt, entscheidet das Spiel je Figur (Haarfarbe oder Haut) – kein eigenes Netz, also keine aufgesetzte Kante und kein Flimmern."""
    namen = gruppen_index(koerper)
    bm = bmesh.new()
    bm.from_mesh(koerper.data)
    deform = bm.verts.layers.deform.active
    k = masse["kopfmitte"]
    vorn = [v.co for v in bm.verts if abs(v.co.x) < 0.035 and masse["kinn"] - 0.01 < v.co.z < masse["kinn"] + 0.05 and v.co.y < k.y]
    y_vorn = min(c.y for c in vorn) if vorn else k.y - 0.09
    mund = sorted(c.z for c in vorn if c.y < y_vorn + 0.02)
    z_mund = mund[len(mund) // 3] if mund else masse["kinn"] + 0.02

    def kopf(v):
        return sum(w for gi, w in v[deform].items() if namen[gi] == "head")

    def oben(v):        # > 0 = über der Bartgrenze (an den Wangen höher, Koteletten)
        if kopf(v) < 0.5 or v.co.y > k.y + 0.01:
            return None
        return v.co.z - (z_mund + 0.012 + 0.014 * min(abs(v.co.x) / 0.07, 1.0))   # v4: tiefer an den Wangen (wirkte wie eine Maske)

    isolinie_schneiden(bm, oben)
    haut = stoff("Haut")
    for f in bm.faces:
        if f.material_index != haut:
            continue
        c = f.calc_center_median()
        o = seite(f, oben)
        lippe = abs(c.z - z_mund) < 0.006 and abs(c.x) < 0.022 and c.y < y_vorn + 0.012
        if o is not None and o < 0 and c.z > masse["kinn"] - 0.05 and abs(c.x) < 0.08 and not lippe:
            f.material_index = stoff("Bart")
    bm.to_mesh(koerper.data)
    bm.free()


def schnuerung_bauen(koerper, masse):
    """Schnürung vorn am Wams (Vorlage 09.11.): Kreuzbänder vom Halsausschnitt bis über den Gürtel, auf die Brust gelegt (Strahl von vorn)."""
    from mathutils.bvhtree import BVHTree
    bm_k = bmesh.new()
    bm_k.from_mesh(koerper.data)
    baum = BVHTree.FromBMesh(bm_k)
    punkte = []
    z = masse["schluesselbein"] - 0.06
    while z > masse["guertel"] + 0.05:
        reihe = []
        for x in (-0.016, 0.016):
            treffer = baum.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
            reihe.append((treffer[0] if treffer[0] is not None else Vector((x, masse["brust_y"], z))) + Vector((0, -0.005, 0)))
        punkte.append(reihe)
        z -= 0.05
    bm_k.free()
    bm = bmesh.new()
    flaechen = []
    for (l1, r1), (l2, r2) in zip(punkte, punkte[1:]):
        flaechen += _zylinder(bm, l1, r2, 0.0035, n=4)
        flaechen += _zylinder(bm, r1, l2, 0.0035, n=4)
    me = bpy.data.meshes.new("Schnuerung")
    bm.to_mesh(me)
    bm.free()
    obj = neues_objekt("Schnuerung", me)
    stoffe_anlegen(obj)
    for p_ in me.polygons:
        p_.material_index = stoff("Schnur")
    g3 = obj.vertex_groups.new(name="spine_03")
    g2 = obj.vertex_groups.new(name="spine_02")
    for v in me.vertices:
        u = max(0.0, min(1.0, (masse["schluesselbein"] - v.co.z) / 0.3))
        g3.add([v.index], 1 - u, "REPLACE")
        g2.add([v.index], u, "REPLACE")
    return obj


def haar_struppig(obj):
    """Struppiges kurzes Haar (Ork, Vorlage 09.11.; NAS-Klicktest: „Kappe mit Zacken“): Der Rand an Stirn, Schläfen und Nacken läuft in
    ungleich langen Strähnen aus (Zickzack, bis 3 cm tiefer, die Ränder schont das Verkleinern), innen stehen Büschel ab. Keine neuen Dreiecke."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    rand = {v for v in bm.verts if v.is_boundary}
    mitte_x = sum(v.co.x for v in bm.verts) / max(len(bm.verts), 1)
    mitte_y = sum(v.co.y for v in bm.verts) / max(len(bm.verts), 1)
    neu = {}
    for v in bm.verts:
        w = math.atan2(v.co.x - mitte_x, v.co.y - mitte_y)
        if v in rand:
            zacke = abs((w * 16.0 / (2 * math.pi)) % 1.0 - 0.5) * 2.0          # Dreieckswelle: 16 Strähnen rundum
            lang = 0.5 + 0.5 * math.sin(w * 5.0 + 0.9)                          # ungleich lang
            neu[v] = v.co + Vector((0, 0, -0.032 * zacke * lang)) + v.normal * 0.006
        else:
            z_ = math.sin(v.co.x * 520.0 + v.co.z * 310.0) * math.sin(v.co.y * 470.0 - v.co.z * 260.0)
            neu[v] = v.co + v.normal * (0.008 + 0.016 * max(0.0, z_))
    for v, c in neu.items():
        v.co = c
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def guertel_zubehoer(rock, masse):
    """Gürtel nach der Vorlage: langes herabhängendes Riemenende vorn links der Schnalle, Schnalle (Eisen) und eine Gürteltasche aus Leder
    an der rechten Hüfte; am Becken, das Riemenende unten etwas am Oberschenkel."""
    bm = bmesh.new()
    zs = [v.co for v in rock.data.vertices if abs(v.co.z - masse["guertel"]) < 0.02]
    vorn_y = min(c.y for c in zs) - 0.004
    stoffe_ = []
    flaechen = _kasten(bm, Vector((-0.045, vorn_y - 0.004, masse["guertel"] - 0.24)), (0.014, 0.003),
                       Vector((-0.045, vorn_y - 0.004, masse["guertel"] + 0.01)), (0.014, 0.003))
    stoffe_ += ["Guertel"] * len(flaechen)
    flaechen = _kasten(bm, Vector((-0.02, vorn_y - 0.006, masse["guertel"] - 0.016)), (0.016, 0.003),
                       Vector((-0.02, vorn_y - 0.006, masse["guertel"] + 0.016)), (0.016, 0.003))
    stoffe_ += ["Eisen"] * len(flaechen)
    hx = max(c.x for c in zs) + 0.012
    flaechen = _kasten(bm, Vector((-hx, masse["kopfmitte"].y - 0.04, masse["guertel"] - 0.13)), (0.022, 0.055),
                       Vector((-hx, masse["kopfmitte"].y - 0.04, masse["guertel"] - 0.005)), (0.02, 0.05))
    stoffe_ += ["Schuh"] * len(flaechen)
    me = bpy.data.meshes.new("GuertelZubehoer")
    bm.to_mesh(me)
    bm.free()
    obj = neues_objekt("GuertelZubehoer", me)
    stoffe_anlegen(obj)
    for p_, s_ in zip(me.polygons, stoffe_):
        p_.material_index = stoff(s_)
    gp = obj.vertex_groups.new(name="pelvis")
    gt = obj.vertex_groups.new(name="thigh_l")
    for v in me.vertices:
        tief = max(0.0, min(1.0, (masse["guertel"] - 0.04 - v.co.z) / 0.2)) if v.co.x < -0.03 and abs(v.co.x + 0.045) < 0.02 else 0.0
        gp.add([v.index], 1 - 0.5 * tief, "REPLACE")
        if tief > 0:
            gt.add([v.index], 0.5 * tief, "REPLACE")
    return obj


def zipfel_bauen(vorlage, masse):
    """Zipfel der Gugel: ein Kegel vom Hinterkopf den Rücken hinab, am Kopf starr, unten mit Hals und Brustkorb."""
    me = bpy.data.meshes.new("Zipfel")
    obj = neues_objekt("Zipfel", me)
    k = masse["kopfmitte"]
    ringe = [  # (Höhe, Abstand nach hinten, Halbmesser, Gewichte)
        (k.z + 0.05, k.y + 0.075, 0.045, {"head": 1.0}),
        (k.z - 0.06, k.y + 0.115, 0.032, {"head": 0.6, "neck_01": 0.4}),
        (k.z - 0.20, k.y + 0.140, 0.022, {"neck_01": 0.3, "spine_03": 0.7}),
        (k.z - 0.36, k.y + 0.155, 0.010, {"spine_03": 1.0}),
    ]
    n = 6
    bm = bmesh.new()
    reihen = []
    for z, y, r, _ in ringe:
        reihen.append([bm.verts.new((r * math.cos(2 * math.pi * i / n), y + 0.6 * r * math.sin(2 * math.pi * i / n), z)) for i in range(n)])
    for a, b in zip(reihen, reihen[1:]):
        for i in range(n):
            bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    spitze = bm.verts.new((0, ringe[-1][1] + 0.01, ringe[-1][0] - 0.05))
    for i in range(n):
        bm.faces.new((reihen[-1][i], reihen[-1][(i + 1) % n], spitze))
    bm.to_mesh(me)
    bm.free()
    stoffe_anlegen(obj)
    for p in me.polygons:
        p.material_index = stoff("Kopf")
    gewichte = [g for _, _, _, g in ringe for _ in range(n)] + [ringe[-1][3]]
    for name in ("head", "neck_01", "spine_03"):
        obj.vertex_groups.new(name=name)
    for i, g in enumerate(gewichte):
        for name, w in g.items():
            obj.vertex_groups[name].add([i], w, "REPLACE")
    return obj


def rock_bauen(rockhelfer, masse, saum, name):
    """Rockteil der Cotte (Saum am Knie) oder langer Rock (Saum über dem Knöchel) aus dem Rockhelfer des Grundnetzes:
    oben abgeschnitten am Gürtel, unten am Saum (fehlt Länge, wird der untere Rand nach unten verlängert und leicht ausgestellt).
    Gewichte: Becken oben, nach unten immer mehr die Oberschenkel (links/rechts nach der Seite), damit der Rock dem Schritt folgt."""
    obj = kopie(rockhelfer, name)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    oben = masse["guertel"] + 0.018
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, oben), plane_no=(0, 0, 1),
                           clear_outer=True)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    unten_jetzt = min(v.co.z for v in bm.verts)
    if saum > unten_jetzt + 0.01:
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, saum), plane_no=(0, 0, 1),
                               clear_inner=True)
    elif saum < unten_jetzt - 0.01:
        rand = [e for e in bm.edges if e.is_boundary and all(v.co.z < unten_jetzt + 0.03 for v in e.verts)]
        schritte = max(1, int(math.ceil((unten_jetzt - saum) / 0.12)))
        mitte = Vector((0, sum(v.co.y for e in rand for v in e.verts) / (2 * len(rand)), 0))
        for _ in range(schritte):
            erg = bmesh.ops.extrude_edge_only(bm, edges=rand)
            neue = [g for g in erg["geom"] if isinstance(g, bmesh.types.BMVert)]
            for v in neue:
                waag = Vector((v.co.x - mitte.x, v.co.y - mitte.y, 0))
                v.co.z -= (unten_jetzt - saum) / schritte
                v.co += waag.normalized() * 0.012 if waag.length > 1e-6 else Vector()
            rand = [g for g in erg["geom"] if isinstance(g, bmesh.types.BMEdge) and all(v in neue for v in g.verts)]
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # Gürtel: ein Band oben am Rock mit glatter Unterkante (Leder), 3 mm weiter außen
    unterkante = masse["guertel"] - 0.02
    isolinie_schneiden(bm, lambda v: v.co.z - unterkante)
    bm.normal_update()
    huefte = masse["huefte"]
    for v in bm.verts:
        # ausgestellt: unterhalb der Hüfte bis zu 7 cm weiter, damit das vorschwingende Bein im Rock bleibt
        u = max(0.0, min((huefte - v.co.z) / masse["oberschenkel"], 1.6))
        waag = Vector((v.co.x, v.co.y - masse["kopfmitte"].y, 0))
        v.co += v.normal * (0.019 if v.co.z > unterkante + 1e-4 else 0.016)
        if waag.length > 1e-6:
            v.co += waag.normalized() * 0.045 * u
    bm.faces.index_update()
    guertel_flaechen = [f for f in bm.faces if f.calc_center_median().z > unterkante]
    bm.to_mesh(obj.data)
    gurt = {f.index for f in guertel_flaechen}
    bm.free()
    stoffe_anlegen(obj)
    for p in obj.data.polygons:
        p.material_index = stoff("Guertel" if p.index in gurt else "Gewand")
    for g in list(obj.vertex_groups):
        obj.vertex_groups.remove(g)
    for n in ("pelvis", "thigh_l", "thigh_r"):
        obj.vertex_groups.new(name=n)
    for v in obj.data.vertices:
        z = v.co.z
        if z >= huefte:
            a = 0.0
        else:
            u = min((huefte - z) / masse["oberschenkel"], 1.0)
            a = 0.85 * u * u * (3 - 2 * u)
        s = max(-1.0, min(1.0, v.co.x / 0.09))
        wl, wr = a * (0.5 + 0.5 * s), a * (0.5 - 0.5 * s)
        if 1 - a > 1e-4:
            obj.vertex_groups["pelvis"].add([v.index], 1 - a, "REPLACE")
        if wl > 1e-4:
            obj.vertex_groups["thigh_l"].add([v.index], wl, "REPLACE")
        if wr > 1e-4:
            obj.vertex_groups["thigh_r"].add([v.index], wr, "REPLACE")
    return obj


def aermellos(vorlage, masse, name, stoffname, flicken=False):
    """Ärmelloses Oberteil (Orks und Oger): die Ärmel der Cotte 4 cm hinter dem Schultergelenk abgeschnitten (Arme nackt, die Aufdickung
    zurückgenommen), das Übrige im Stoff „Wams“ (Leder) oder als Sackleinen mit Flicken."""
    obj = kopie(vorlage, name)
    namen = gruppen_index(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    deform = bm.verts.layers.deform.active

    def arm(v, s):
        if knochen_von(v, namen, deform) not in (f"upperarm_{s}", f"lowerarm_{s}", f"hand_{s}"):
            return None
        a = masse[f"schulter_{s}"]
        d = (masse[f"ellbogen_{s}"] - a).normalized()
        return (v.co - a).dot(d) + 0.02       # 2 cm vor dem Gelenk (v3: 4 cm dahinter stand der Stoff wie ein Brett ab)

    for s in ("l", "r"):
        isolinie_schneiden(bm, lambda v, s=s: arm(v, s))
    gewand, haut = stoff("Gewand"), stoff("Haut")
    entbloesst = set()
    for f in bm.faces:
        if f.material_index == stoff("Beinkleid"):
            f.material_index = haut          # ohne Beinkleid: nackte Beine
            continue
        if f.material_index != gewand:
            continue
        nackt = any((w := seite(f, lambda v, s=s: arm(v, s))) is not None and w > 0 for s in ("l", "r"))
        if nackt:
            f.material_index = haut
            entbloesst.add(f)
        elif flicken and _flicken(f.calc_center_median()):
            f.material_index = stoff("Flicken")
        else:
            f.material_index = stoff(stoffname)
    bm.normal_update()
    # Die Aufdickung der Cotte (1,2 cm) nur dort zurücknehmen, wo vorher Ärmel war – anteilig an den Rändern. Vorher wurden auch die Hände
    # um 1,2 cm eingezogen: die Finger fielen zu dünnen Krallen zusammen (Vorlage des Nutzers 09.11.: „Hände dünn, komisch“).
    for v in bm.verts:
        if v.link_faces:
            anteil = sum(1 for f in v.link_faces if f in entbloesst) / len(v.link_faces)
            if anteil > 0:
                v.co -= v.normal * 0.012 * anteil
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def stoff_tauschen(vorlage, name, von, nach):
    obj = kopie(vorlage, name)
    a, b = stoff(von), stoff(nach)
    for p_ in obj.data.polygons:
        if p_.material_index == a:
            p_.material_index = b
    return obj


HAUER = {"ork": (0.021, 0.0055, 0.024, 0.012), "oger": (0.022, 0.005, 0.022, 0.003)}   # Abstand zur Mitte, Dicke, Länge, vor dem Kinn


def hauer_bauen(koerper, masse, volk="ork"):
    """Hauer der Orks (docs/spielbuch/erbgut.md): zwei kleine Kegel aus dem Unterkiefer neben den Mundwinkeln, nach oben und leicht
    nach außen, starr am Kopf. Die Lage misst sich am Netz: die vordersten Punkte des Gesichts knapp über dem Kinn."""
    me = koerper.data
    k = masse["kopfmitte"]
    vorn = [v.co for v in me.vertices if abs(v.co.x) < 0.035 and masse["kinn"] - 0.01 < v.co.z < masse["kinn"] + 0.05 and v.co.y < k.y]
    y_vorn = min(c.y for c in vorn) if vorn else k.y - 0.09
    mund = [c for c in vorn if c.y < y_vorn + 0.02]
    z_mund = sorted(c.z for c in mund)[len(mund) // 3] if mund else masse["kinn"] + 0.02
    bm = bmesh.new()
    n = 5
    for s in (1, -1):
        seitlich, dicke, laenge, vor = HAUER[volk]
        fuss = Vector((s * seitlich, y_vorn + vor, z_mund - 0.006))
        richtung = Vector((s * 0.25, -0.15, 1.0)).normalized()
        quer = Vector((1, 0, 0)).cross(richtung).normalized()
        quer2 = richtung.cross(quer)
        ring = [bm.verts.new(fuss + dicke * (math.cos(2 * math.pi * i / n) * quer + math.sin(2 * math.pi * i / n) * quer2)) for i in range(n)]
        spitze = bm.verts.new(fuss + richtung * laenge)
        for i in range(n):
            bm.faces.new((ring[i], ring[(i + 1) % n], spitze))
        bm.faces.new(list(reversed(ring)))
    bm.normal_update()
    me2 = bpy.data.meshes.new("Hauer")
    bm.to_mesh(me2)
    bm.free()
    obj = neues_objekt("Hauer", me2)
    stoffe_anlegen(obj)
    for p in me2.polygons:
        p.material_index = stoff("Hauer")
    obj.vertex_groups.new(name="head").add(list(range(len(me2.vertices))), 1.0, "REPLACE")
    return obj


def _flicken(c):
    """Flicken auf zerlumpter Kleidung: gut jede siebzehnte Zelle eines 14-cm-Rasters (fest aus der Lage, damit jeder Lauf gleich ist)."""
    return (int((c.x + 1) * 7) * 31 + int(c.z * 7) * 17 + int((c.y + 1) * 7) * 13) % 17 == 0


def bettler_koerper(vorlage, masse, name):
    """Bettler (Schritt 2 des Vollausbaus): Ärmel bis zur Mitte des Unterarms zerrissen (Unterarm nackt), Flicken auf der Cotte."""
    obj = kopie(vorlage, name)
    namen = gruppen_index(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    deform = bm.verts.layers.deform.active

    def aermel(v, s):
        if knochen_von(v, namen, deform) not in (f"lowerarm_{s}", f"hand_{s}"):
            return None
        e = masse[f"ellbogen_{s}"]
        d = masse[f"handgelenk_{s}"] - e
        return (v.co - e).dot(d.normalized()) - 0.45 * d.length

    for s in ("l", "r"):
        isolinie_schneiden(bm, lambda v, s=s: aermel(v, s))
    gewand, haut, flicken = stoff("Gewand"), stoff("Haut"), stoff("Flicken")
    entbloesst = set()
    for f in bm.faces:
        if f.material_index != gewand:
            continue
        nackt = False
        for s in ("l", "r"):
            w = seite(f, lambda v, s=s: aermel(v, s))
            if w is not None and w > 0:
                nackt = True
        if nackt:
            f.material_index = haut
            entbloesst.add(f)
        elif _flicken(f.calc_center_median()):
            f.material_index = flicken
    bm.normal_update()
    for v in bm.verts:                      # nackter Unterarm: die Aufdickung der Cotte zurücknehmen (nicht an den Händen)
        if v.link_faces:
            anteil = sum(1 for f in v.link_faces if f in entbloesst) / len(v.link_faces)
            if anteil > 0:
                v.co -= v.normal * 0.012 * anteil
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def bettler_rock(vorlage, name):
    """Zerlumpter Rock: Saum in Fetzen (bis 4 cm ungleich lang), Flicken."""
    obj = kopie(vorlage, name)
    me = obj.data
    unten = min(v.co.z for v in me.vertices)
    for v in me.vertices:
        if v.co.z < unten + 0.05:
            w = math.atan2(v.co.x, v.co.y)
            fetzen = 0.5 + 0.5 * math.sin(w * 11.0) * math.sin(w * 4.0 + 1.3)
            v.co.z -= 0.04 * fetzen * (1 - (v.co.z - unten) / 0.05)
    gewand, flicken = stoff("Gewand"), stoff("Flicken")
    for p in me.polygons:
        if p.material_index == gewand and _flicken(p.center):
            p.material_index = flicken
    me.update()
    return obj


def haar_bauen(haarhelfer, masse):
    """Kurzes Männerhaar aus dem Haarhelfer des Grundnetzes: hinten bis etwa Ohrläppchen, vorn über der Stirn abgeschnitten,
    als geschlossene, geglättete Kappe etwas über dem Kopf (die Strähnenkarten des Helfers flimmern sonst)."""
    obj = kopie(haarhelfer, "Haar")
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    k = masse["kopfmitte"]
    weg = [v for v in bm.verts if v.co.z < masse["kinn"] + 0.06 or (v.co.y < k.y - 0.045 and v.co.z < masse["stirn"] + 0.005)]
    bmesh.ops.delete(bm, geom=weg, context="VERTS")
    for _ in range(6):
        bmesh.ops.smooth_vert(bm, verts=[v for v in bm.verts if not v.is_boundary], factor=0.5,
                              use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * 0.007
    bm.to_mesh(obj.data)
    bm.free()
    stoffe_anlegen(obj)
    for p in obj.data.polygons:
        p.material_index = stoff("Haar")
    for g in list(obj.vertex_groups):
        obj.vertex_groups.remove(g)
    obj.vertex_groups.new(name="head").add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    return obj


def schuh_bauen(koerper_quelle, s, masse, form="halb"):
    """Bundschuh um 1400 aus Leder mit leichter Spitze: ein gelofteter Körper von der Ferse bis über die Zehen, Schaft bis
    über den Knöchel (das Bein endet darin, siehe koerper_zonen). Maße aus den Fußecken des Grundnetzes. Ferse und Schaft am
    Fuß, die Spitze zum Zehenknochen hin."""
    namen = gruppen_index(koerper_quelle)
    fuss = []
    for v in koerper_quelle.data.vertices:
        g = {namen[e.group]: e.weight for e in v.groups}
        if g.get(f"foot_{s}", 0) + g.get(f"ball_{s}", 0) > 0.5:
            fuss.append(v.co.copy())
    xm = sum(p.x for p in fuss) / len(fuss)
    breite = max(p.x for p in fuss) - min(p.x for p in fuss)
    ferse, zehe = max(p.y for p in fuss) + 0.004, min(p.y for p in fuss) - 0.014   # Spitze gut 1 cm über die Zehen
    sohle = min(p.z for p in fuss) - 0.002
    # Runde 4 (Nutzer: „klobig“): flacher Halbschuh statt hohem Schaft – Oberkante knapp über dem Knöchel, das Bein endet darin
    # (Schnitt am Knöchel, koerper_zonen); kaum breiter als der Fuß
    schaft = masse["knoechel"] + 0.012
    holz = form == "holz"
    if not holz:
        schaft = masse["knoechel"] + 0.055      # Vorlage 09.11.: knöchelhoher Bundschuh aus dunklem Leder
    if holz:
        # Holzschuh (Arme, Bauern): aus einem Stück, breiter, hohe runde Zehenkappe, leicht aufgebogene Spitze, Ferse geschlossen
        zehe += 0.006
    ball_t = (ferse - masse[f"ballen_{s}"].y) / (ferse - zehe)

    def schnitt(t):
        # halbe Breite, Höhe der Oberkante, Versatz der Mitte zur Innenseite (die Spitze zeigt leicht nach innen)
        b = breite / 2 + (0.011 if holz else 0.006)
        if t < 0.15:
            hb = b * (0.70 + 0.30 * t / 0.15)
        elif t < 0.7:
            hb = b
        else:
            hb = b * (1 - (0.42 if holz else 0.62) * ((t - 0.7) / 0.3) ** 1.5)   # vorn rund zulaufend, nicht spitz
        if t < 0.38:
            oben = schaft
        else:
            u = min((t - 0.38) / 0.42, 1.0)
            oben = schaft + (sohle + 0.038 - schaft) * (u * u * (3 - 2 * u))
        if t > 0.88:
            oben = sohle + 0.038 - (t - 0.88) / 0.12 * 0.008   # Zehenkappe rund, nicht keilförmig
        if holz:
            oben = max(oben + 0.012, sohle + 0.058 - 0.012 * max(0.0, t - 0.85) / 0.15)
        return hb, oben

    n_t, n_r = 10, 10
    bm = bmesh.new()
    reihen = []
    zentren = []
    for i in range(n_t + 1):
        t = i / n_t
        y = ferse + (zehe - ferse) * t
        hb, oben = schnitt(t)
        innen = (-1 if s == "l" else 1) * 0.012 * max(0.0, t - 0.6) / 0.4
        mitte_z = (oben + sohle) / 2
        hz = (oben - sohle) / 2
        ring = []
        for j in range(n_r):
            w = 2 * math.pi * j / n_r
            x = xm + innen + hb * math.cos(w)
            z = mitte_z + hz * math.sin(w)
            z = max(z, sohle)                              # flache Sohle
            ring.append(bm.verts.new((x, y, z)))
        reihen.append((ring, t))
        zentren.append(Vector((xm + innen, y, mitte_z)))
    for (a, _), (b, _) in zip(reihen, reihen[1:]):
        for j in range(n_r):
            bm.faces.new((a[j], a[(j + 1) % n_r], b[(j + 1) % n_r], b[j]))
    hinten = bm.verts.new(zentren[0] + Vector((0, 0.004, 0)))
    for j in range(n_r):
        bm.faces.new((reihen[0][0][(j + 1) % n_r], reihen[0][0][j], hinten))
    spitze = bm.verts.new(zentren[-1] + Vector((0, -0.006, 0.010 if holz else 0.0)))
    for j in range(n_r):
        bm.faces.new((reihen[-1][0][j], reihen[-1][0][(j + 1) % n_r], spitze))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(f"Schuh_{form}_{s}")
    bm.to_mesh(me)
    bm.free()
    obj = neues_objekt(f"Schuh_{form}_{s}", me)
    stoffe_anlegen(obj)
    for p in me.polygons:
        p.material_index = stoff("Holzschuh" if holz else "Schuh")
        if not holz:
            # Schnürung (Vorlage 09.11.): Band oben am Schaft und vorn über dem Spann
            c = p.center
            t_ = (ferse - c.y) / (ferse - zehe)
            if (c.z > schaft - 0.012 and t_ < 0.45) or (0.30 < t_ < 0.62 and abs(c.x - xm) < 0.012 and c.z > sohle + 0.035):
                p.material_index = stoff("Schnur")
    gf = obj.vertex_groups.new(name=f"foot_{s}")
    gb = obj.vertex_groups.new(name=f"ball_{s}")
    gc = obj.vertex_groups.new(name=f"calf_{s}")
    for v in me.vertices:
        t = (ferse - v.co.y) / (ferse - zehe)
        zeh = max(0.0, min(1.0, (t - (ball_t - 0.08)) / 0.16))
        zeh = zeh * zeh * (3 - 2 * zeh)
        schaft_anteil = max(0.0, min(1.0, (v.co.z - (masse["knoechel"] + 0.02)) / 0.06)) if t < 0.45 else 0.0
        wf = (1 - zeh) * (1 - 0.6 * schaft_anteil)
        if wf > 1e-4:
            gf.add([v.index], wf, "REPLACE")
        if zeh > 1e-4:
            gb.add([v.index], zeh, "REPLACE")
        if schaft_anteil > 1e-4:
            gc.add([v.index], 0.6 * schaft_anteil * (1 - zeh), "REPLACE")
    return obj


def zusammenfuegen(teile, name):
    kopien = []
    for t in teile:
        k = kopie(t, t.name + "_k")
        kopien.append(k)
    aktiv(kopien[0])
    for k in kopien:
        k.select_set(True)
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = obj.data.name = name
    return obj


# ─────────────────────────────────────────────────────────────────────────────
#  4. Gewichte, Verkleinern, Zonen-Attribute
# ─────────────────────────────────────────────────────────────────────────────

def gewichte_aufraeumen(obj):
    """Finger → Hand, alle übrigen Gruppen weg, höchstens 4 Knochen je Ecke, normiert, Namen nach SkeletonProfileHumanoid."""
    namen = gruppen_index(obj)
    werte = []
    for v in obj.data.vertices:
        g = {}
        for e in v.groups:
            n = namen[e.group]
            if n.split("_")[0] in FINGER:
                n = "hand_" + n[-1]
            if n in KNOCHEN and n != "Root":
                g[KNOCHEN[n]] = g.get(KNOCHEN[n], 0.0) + e.weight
        beste = sorted(g.items(), key=lambda x: -x[1])[:4]
        summe = sum(w for _, w in beste)
        if summe <= 1e-6:
            beste, summe = [("Hips", 1.0)], 1.0
        werte.append([(n, w / summe) for n, w in beste])
    for g in list(obj.vertex_groups):
        obj.vertex_groups.remove(g)
    for n in sorted(set(KNOCHEN.values())):
        obj.vertex_groups.new(name=n)
    for i, liste in enumerate(werte):
        for n, w in liste:
            obj.vertex_groups[n].add([i], w, "REPLACE")


def erhalten_markieren(obj, fern=False):
    """Eckengruppe „Erhalten“ für das Verkleinern: Grenzen zwischen Stoffen und offene Ränder 1, Kopf 0,8, Hände 0,85 (NAS 09.11.: bei 0,4 wurden
    Finger zu langen Spitzen) –
    dort fallen weniger Ecken weg (sonst wird die Ausschnittkante wieder zur Treppe und das Gesicht kantig). Fernnetz: nur der Gürtel
    (0,7; das schmale Band zerfiel sonst in Lücken)."""
    me = obj.data
    gruppe = obj.vertex_groups.new(name="Erhalten")
    kopf = obj.vertex_groups["Head"].index
    haende = {obj.vertex_groups["LeftHand"].index, obj.vertex_groups["RightHand"].index}
    guertel = stoff("Guertel")
    stoffe = {}
    flicken = stoff("Flicken")
    for p in me.polygons:
        if p.material_index == flicken:
            continue                   # Flicken-Ränder nicht schonen (Ork 09.11.: sonst blieb fürs Verkleinern nur die Hand übrig)
        for vi in p.vertices:
            stoffe.setdefault(vi, set()).add(p.material_index)
    bm = bmesh.new()
    bm.from_mesh(me)
    rand = {v.index for v in bm.verts if v.is_boundary}
    bm.free()
    for v in me.vertices:
        w = 0.0
        if not fern:
            for e in v.groups:
                if e.group == kopf and e.weight > 0.5:
                    w = max(w, 0.8)
                if e.group in haende and e.weight > 0.5:
                    w = max(w, 0.97)         # Hände stark schonen (Vorlage 09.11.) (Vorlage 09.11.: Finger blieben dünne Stäbe)
        if fern:
            w = 0.7 if guertel in stoffe.get(v.index, ()) else 0.0
        elif len(stoffe.get(v.index, ())) > 1 or v.index in rand:
            w = 1.0
        if w > 0:
            gruppe.add([v.index], w, "REPLACE")


def verkleinern(obj, ziel, schonen):
    jetzt = dreiecke(obj)
    if jetzt > ziel:
        aktiv(obj)
        m = obj.modifiers.new("Verkleinern", "DECIMATE")
        m.decimate_type = "COLLAPSE"
        m.ratio = ziel / jetzt
        m.use_collapse_triangulate = True
        if schonen:
            # Blender: Gewicht = wie stark verkleinert wird, darum umgekehrt (Erhalten 1 = bleibt)
            m.vertex_group = "Erhalten"
            m.invert_vertex_group = True
            m.vertex_group_factor = 4.0
        while obj.modifiers[0] != m:
            bpy.ops.object.modifier_move_up(modifier=m.name)
        bpy.ops.object.modifier_apply(modifier=m.name)
    if "Erhalten" in obj.vertex_groups:
        obj.vertex_groups.remove(obj.vertex_groups["Erhalten"])
    return dreiecke(obj)


def figur_material():
    m = bpy.data.materials.get("Figur") or bpy.data.materials.new("Figur")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    baum = m.node_tree
    p = baum.nodes.get("Principled BSDF")
    p.inputs["Roughness"].default_value = 0.9
    if not any(n.type == "VERTEX_COLOR" for n in baum.nodes):
        ecke = baum.nodes.new("ShaderNodeVertexColor")
        ecke.layer_name = "Farbe"
        gamma = baum.nodes.new("ShaderNodeGamma")
        gamma.inputs["Gamma"].default_value = 2.2
        baum.links.new(ecke.outputs["Color"], gamma.inputs["Color"])
        baum.links.new(gamma.outputs["Color"], p.inputs["Base Color"])
    return m


def zonen_setzen(obj):
    """Stoff je Fläche → UV „Zone“: x = Zone, y = Art + 16 × (Schicht der Bibliothek + 1) (Wolle/Leinen „tuch“, Leder „leder“;
    wie bausteine.py, oberflaeche.gdshader liest es); feste Farbe als Eckenfarbe „Farbe“ (wie figuren.py); ein Material „Figur“."""
    me = obj.data
    alte = [m.name.split(".")[0] if m else "" for m in me.materials]
    for uv in list(me.uv_layers):
        me.uv_layers.remove(uv)
    for c in list(me.color_attributes):
        me.color_attributes.remove(c)
    farbe = me.color_attributes.new("Farbe", "BYTE_COLOR", "CORNER")
    me.color_attributes.active_color = farbe
    me.color_attributes.render_color_index = me.color_attributes.find("Farbe")
    uv = me.uv_layers.new(name="Zone")
    werte, zonen = [1.0] * (len(me.loops) * 4), [0.0] * (len(me.loops) * 2)
    for poly in me.polygons:
        zone, f, schicht = STOFFE.get(alte[poly.material_index], (0, (1, 0, 1), None))
        code = 16.0 * (BIB[schicht] + 1) if schicht in BIB else 0.0
        for li in poly.loop_indices:
            werte[li * 4:li * 4 + 4] = [f[0], f[1], f[2], 1.0]
            zonen[li * 2] = float(zone)
            zonen[li * 2 + 1] = code
    farbe.data.foreach_set("color", werte)
    uv.data.foreach_set("uv", zonen)
    me.materials.clear()
    me.materials.append(figur_material())
    me.polygons.foreach_set("material_index", [0] * len(me.polygons))
    # Weiche Schattierung (die Figuren sind 9–25 Pixel hoch, harte Kanten würden flimmern)
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.update()


# ─────────────────────────────────────────────────────────────────────────────
#  5. Skelett umbenennen
# ─────────────────────────────────────────────────────────────────────────────

def skelett_umbauen(rig, name):
    aktiv(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones
    for b in list(eb):
        if b.name not in KNOCHEN:
            eb.remove(b)
    for b in eb:
        b.name = KNOCHEN[b.name]
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.name = name
    rig.data.name = name
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return rig


# ─────────────────────────────────────────────────────────────────────────────
#  6. Bewegungen übertragen
# ─────────────────────────────────────────────────────────────────────────────

QUELLNETZE = {}


def ual_laden(pfad):
    vorher = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=pfad)
    neu = [o for o in bpy.data.objects if o not in vorher]
    rig = next(o for o in neu if o.type == "ARMATURE")
    for o in neu:
        if o.type == "MESH" and o.name.startswith("Mannequin"):
            o.hide_render = True
            QUELLNETZE[rig.name] = o      # bleibt für die Vergleichsbilder
        elif o.type == "MESH":
            bpy.data.objects.remove(o)
    rig.animation_data_create()
    # Der Import legt weitere Clips als NLA-Spuren ab; sie würden sich in die Haltung mischen
    for spur in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(spur)
    return rig


def aktion_setzen(obj, act):
    obj.animation_data.action = act
    if hasattr(obj.animation_data, "action_slot") and getattr(act, "slots", None) and len(act.slots) and obj.animation_data.action_slot is None:
        obj.animation_data.action_slot = act.slots[0]


def clip_aktion(clip):
    kandidaten = [a for a in bpy.data.actions if a.name.split(".")[0] == clip]
    if not kandidaten:
        raise RuntimeError(f"Clip {clip} fehlt")
    return kandidaten[0]


def richtung(rig, b):
    m = rig.matrix_world
    return ((m @ b.tail_local) - (m @ b.head_local)).normalized()


def uebertragen(quelle, ziel, act, name):
    """Überträgt einen Clip: Jeder Knochen bekommt im Weltraum dieselbe Drehung gegenüber einer Bezugshaltung. Arme und Beine
    werden dafür nach der Richtung ihrer Knochen ausgerichtet (unser Grundnetz steht in A-Haltung, Quaternius in T-Haltung);
    Rumpf, Hals, Kopf, Schultern und Füße nicht (ihre Knochenachsen sagen nichts über die Haltung). Die Hüfte bekommt
    zusätzlich die Verschiebung der Quellhüfte, mit dem Verhältnis der Hüfthöhen."""
    aktion_setzen(quelle, act)
    a, e = int(act.frame_range[0]), int(act.frame_range[1])
    mq, mz = quelle.matrix_world, ziel.matrix_world
    paare = [(q, z) for q, z in UAL_KNOCHEN.items() if q in quelle.data.bones and z in ziel.data.bones]
    bezug = {}
    for q, z in paare:
        bq, bz = quelle.data.bones[q], ziel.data.bones[z]
        rq = (mq @ bq.matrix_local).to_quaternion()
        rz = (mz @ bz.matrix_local).to_quaternion()
        if z.endswith(("UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg")):
            dreh = richtung(ziel, bz).rotation_difference(richtung(quelle, bq))
        else:
            dreh = Quaternion()
        bezug[z] = (rq, dreh @ rz)
    qhuefte = next(q for q, z in paare if z == "Hips")
    hq = quelle.data.bones[qhuefte].head_local.z
    hz = ziel.data.bones["Hips"].head_local.z
    hq_ruhe = mq @ quelle.data.bones[qhuefte].head_local
    hz_ruhe = mz @ ziel.data.bones["Hips"].head_local
    massstab = hz / hq
    q_von_z = {z: q for q, z in paare}

    # Vorlauf: Neigung des Oberkörpers (Hüfte → Hals, nach vorn positiv; die Figur schaut nach −Y) und Drehungen der Beine über den Clip
    beine = [z for z in q_von_z if z.endswith(("UpperLeg", "LowerLeg", "Foot", "UpperArm"))]   # Oberarme: Mittel für das lockere Hängen
    deltas = {z: [] for z in beine}
    neigungen = []

    def neigung(unten, oben):
        v = oben - unten
        return math.atan2(-v.y, v.z)

    hals_q = q_von_z["Neck"]
    kopf_q = q_von_z["Head"]
    kopfneigungen = []
    for bild in range(a, e + 1):
        bpy.context.scene.frame_set(bild)
        neigungen.append(neigung(mq @ quelle.pose.bones[qhuefte].head, mq @ quelle.pose.bones[hals_q].head))
        kp = mq @ quelle.pose.bones[kopf_q].matrix
        kopfneigungen.append(neigung(kp.to_translation(), kp @ Vector((0, 0.15, 0))))
        for z in beine:
            rq_ruhe, _ = bezug[z]
            deltas[z].append((mq @ quelle.pose.bones[q_von_z[z]].matrix).to_quaternion() @ rq_ruhe.inverted())
    ruhe_neigung = neigung(hq_ruhe, mq @ quelle.data.bones[hals_q].head_local)
    vorgebeugt = sum(neigungen) / len(neigungen) - ruhe_neigung
    korrektur = max(0.0, vorgebeugt - math.radians(AUFRECHT_REST)) if name in AUFRECHT else 0.0
    zurueck = Quaternion(mz.to_quaternion() @ Vector((1, 0, 0)), -korrektur)
    # Kopf: der Blick der Quaternius-Puppe hängt nach unten; Hals und Kopf so weit heben, wie der Kopf im Mittel über die
    # Ruhelage hinaus (nach der Korrektur des Rumpfes) nach vorn kippt, abzüglich 4 Grad
    kr = mq @ quelle.data.bones[kopf_q].matrix_local
    ruhe_kopf = neigung(kr.to_translation(), kr @ Vector((0, 0.15, 0)))
    kopf_vor = sum(kopfneigungen) / len(kopfneigungen) - ruhe_kopf - korrektur
    kopf_korrektur = max(0.0, kopf_vor - math.radians(4.0)) if name in AUFRECHT else 0.0
    kopf_zurueck = Quaternion(mz.to_quaternion() @ Vector((1, 0, 0)), -kopf_korrektur)
    oberkoerper = {"Spine", "Chest", "UpperChest", "Neck", "Head"} | {f"{s}{t}" for s in ("Left", "Right")
                                                                     for t in ("Shoulder", "UpperArm", "LowerArm", "Hand")}
    schritt = SCHRITT.get(name, 1.0)
    locker = LOCKER.get(name, 1.0)
    alt_unterarm, neu_unterarm = {}, {}
    mittel = {}
    for z, liste in deltas.items():
        summe = Quaternion((0, 0, 0, 0))
        for q in liste:
            q = q.copy()
            if q.dot(liste[0]) < 0:
                q.negate()
            summe = Quaternion((summe.w + q.w, summe.x + q.x, summe.y + q.y, summe.z + q.z))
        mittel[z] = summe.normalized()
    BERICHT.setdefault("uebertragung", {})[name] = {"vorgebeugt_grad": math.degrees(vorgebeugt), "korrektur_grad": math.degrees(korrektur), "kopf_korrektur_grad": math.degrees(kopf_korrektur),
                                                    "schrittfaktor": schritt}

    def schritt_verstaerken(z, delta):
        if schritt == 1.0 or z not in mittel or z.endswith("UpperArm"):
            return delta
        m = mittel[z]
        rest = m.inverted() @ delta
        achse, winkel = rest.to_axis_angle()
        if winkel > math.pi:
            winkel -= 2 * math.pi
        return m @ Quaternion(achse, winkel * schritt)

    neu = bpy.data.actions.new(name)
    ziel.animation_data_create()
    ziel.animation_data.action = neu
    reihenfolge = [b.name for b in ziel.data.bones]  # Eltern stehen vor Kindern
    for bild in range(a, e + 1):
        bpy.context.scene.frame_set(bild)
        posen = {}
        for zn in reihenfolge:
            bz = ziel.data.bones[zn]
            pb = ziel.pose.bones[zn]
            eltern = bz.parent
            ruhe_rel = (eltern.matrix_local.inverted() @ bz.matrix_local) if eltern else bz.matrix_local
            elter_pose = posen[eltern.name] if eltern else Matrix.Identity(4)
            if zn in q_von_z:
                qp = quelle.pose.bones[q_von_z[zn]]
                rq_pose = (mq @ qp.matrix).to_quaternion()
                rq_ruhe, rz_bezug = bezug[zn]
                delta = schritt_verstaerken(zn, rq_pose @ rq_ruhe.inverted())
                welt = delta @ rz_bezug
                if locker < 1.0 and zn.endswith("UpperArm"):
                    # Arme hängen lassen: die Quaternius-Puppe trägt die Arme vor dem Körper; ihr Schwingen um das Mittel bleibt (×0,8),
                    # das Mittel selbst wird zur hängenden Haltung (leicht seitlich)
                    schwung = delta @ mittel[zn].inverted()
                    achse_s, winkel_s = schwung.to_axis_angle()
                    if winkel_s > math.pi:
                        winkel_s -= 2 * math.pi
                    seitlich = 0.18 if zn.startswith("Left") else -0.18
                    haengen = (rz_bezug @ Vector((0, 1, 0))).rotation_difference(Vector((seitlich, 0.04, -1.0)).normalized())
                    welt = Quaternion(achse_s, winkel_s * 0.8) @ haengen @ rz_bezug
                if zn in oberkoerper:
                    welt = zurueck @ welt
                if zn in ("Neck", "Head"):
                    welt = kopf_zurueck @ welt
                if locker < 1.0 and zn.endswith("LowerArm"):
                    # Ellbogen weniger gebeugt: Beugung gegenüber der Bezugshaltung um den Faktor „locker“ verkleinern
                    oberarm = mz.to_quaternion() @ posen[eltern.name].to_quaternion()
                    ruhe_rel_q = bezug[eltern.name][1].inverted() @ bezug[zn][1]
                    beuge = oberarm.inverted() @ welt @ ruhe_rel_q.inverted()
                    achse, winkel = beuge.to_axis_angle()
                    if winkel > math.pi:
                        winkel -= 2 * math.pi
                    alt_unterarm[zn] = welt
                    BERICHT.setdefault("ellbogen", {}).setdefault(name, []).append(round(math.degrees(winkel), 1))
                    welt = oberarm @ Quaternion(achse, winkel * locker) @ ruhe_rel_q
                    neu_unterarm[zn] = welt
                if locker < 1.0 and zn.endswith("Hand") and zn.replace("Hand", "LowerArm") in neu_unterarm:
                    ua = zn.replace("Hand", "LowerArm")
                    # Handgelenk gerade: die Hand setzt den Unterarm fort (die Fäuste der Quaternius-Puppe knickten die offenen Hände nach vorn ab)
                    welt = neu_unterarm[ua] @ bezug[ua][1].inverted() @ bezug[zn][1]
                arm = mz.to_quaternion().inverted() @ welt
                ort = (elter_pose @ ruhe_rel).to_translation()
                if zn == "Hips":
                    qh = mq @ qp.matrix.to_translation()
                    ort = mz.inverted() @ (hz_ruhe + (qh - hq_ruhe) * massstab)
                gewollt = Matrix.Translation(ort) @ arm.to_matrix().to_4x4()
            else:
                gewollt = elter_pose @ ruhe_rel
            basis = ruhe_rel.inverted() @ elter_pose.inverted() @ gewollt
            loc, rot, _ = basis.decompose()
            pb.rotation_quaternion = rot
            pb.location = loc if zn == "Hips" else Vector()
            pb.keyframe_insert("rotation_quaternion", frame=bild - a + 1)
            if zn == "Hips":
                pb.keyframe_insert("location", frame=bild - a + 1)
            posen[zn] = elter_pose @ ruhe_rel @ Matrix.LocRotScale(pb.location, rot, None)
    ziel.animation_data.action = None
    spur = ziel.animation_data.nla_tracks.new()
    spur.name = name
    spur.strips.new(name, 1, neu)
    return neu, e - a


def tempo_messen(ziel, act, bilder):
    """Geschwindigkeit (m/s) des Standfußes (Gehen auf der Stelle): so schnell muss die Figur vorwärts, damit nichts rutscht."""
    ziel.animation_data.action = act
    werte = {"LeftFoot": [], "RightFoot": []}
    for f in range(1, bilder + 2):
        bpy.context.scene.frame_set(f)
        for n in werte:
            werte[n].append((ziel.matrix_world @ ziel.pose.bones[n].head).copy())
    ziel.animation_data.action = None
    v = []
    for n, lagen in werte.items():
        tiefste = min(p.z for p in lagen)
        for p0, p1 in zip(lagen, lagen[1:]):
            if p0.z < tiefste + 0.02 and p1.z < tiefste + 0.02:
                v.append((p1.y - p0.y) * FPS)
    v.sort()
    return v[len(v) // 2] if v else 0.0


def gesaess_tiefe(koerper, rig):
    """Wie weit (Meter) das Gesäß in der Ruhelage unter dem Kopf des Hüftknochens liegt (tiefste Ecke am Becken)."""
    namen = gruppen_index(koerper)
    tief = min((v.co.z for v in koerper.data.vertices
                if any(namen[e.group] == "pelvis" and e.weight > 0.5 for e in v.groups)), default=0.0)
    return rig.data.bones["pelvis"].head_local.z - tief


def _kurven(act, pfad):
    """F-Kurven einer Aktion zu einem Datenpfad (auch bei geschichteten Aktionen ab Blender 4.4)."""
    kurven = []
    if hasattr(act, "fcurves") and act.fcurves:
        kurven = [fc for fc in act.fcurves if fc.data_path == pfad]
    if not kurven and hasattr(act, "layers"):
        for schicht in act.layers:
            for streifen in schicht.strips:
                for bag in getattr(streifen, "channelbags", []):
                    kurven += [fc for fc in bag.fcurves if fc.data_path == pfad]
    return sorted(kurven, key=lambda fc: fc.array_index)


def schleife_schliessen(rig, act):
    """Für Clips, die keine Schleife sind (Farm_Harvest → „Buecken“): Jede Knochendrehung bekommt über den Clip verteilt den Unterschied
    zwischen letztem und erstem Bild abgezogen (Slerp), die Hüftlage ebenso – das letzte Bild gleicht dem ersten, kein Sprung am Ende."""
    for pb in rig.pose.bones:
        kurven = _kurven(act, f'pose.bones["{pb.name}"].rotation_quaternion')
        if len(kurven) == 4:
            n = len(kurven[0].keyframe_points)
            qs = [Quaternion([kurven[k].keyframe_points[i].co[1] for k in range(4)]) for i in range(n)]
            for i in range(1, n):
                if qs[i].dot(qs[i - 1]) < 0:
                    qs[i].negate()
            fehler = qs[0] @ qs[-1].inverted()
            for i, q in enumerate(qs):
                neu = Quaternion().slerp(fehler, i / max(n - 1, 1)) @ q
                for k in range(4):
                    kurven[k].keyframe_points[i].co[1] = neu[k]
        lagen = _kurven(act, f'pose.bones["{pb.name}"].location')
        for fc in lagen:
            pk = fc.keyframe_points
            n = len(pk)
            d = pk[n - 1].co[1] - pk[0].co[1]
            for i in range(n):
                pk[i].co[1] -= d * i / max(n - 1, 1)
        for fc in kurven + lagen:
            for k in fc.keyframe_points:
                k.handle_left_type = k.handle_right_type = "AUTO_CLAMPED"
            fc.update()


def bodenkontakt(rig, netz, act, bilder):
    """Füße auf den Boden: Die tiefste Ecke des Netzes je Bild soll im unteren Viertel des Clips auf 0 liegen (Runde 4: die Figuren steckten im Pflaster).
    Die Übertragung hält nur die Hüfthöhe im Verhältnis, nicht die Fußsohle; die Hüfte wird um den Unterschied gehoben bzw. gesenkt.
    Gibt die Verschiebung in Metern zurück."""
    rig.animation_data.action = act
    tiefste = []
    mw = netz.matrix_world
    for f in range(1, bilder + 2):
        bpy.context.scene.frame_set(f)
        dg = bpy.context.evaluated_depsgraph_get()
        ev = netz.evaluated_get(dg)
        me = ev.to_mesh()
        tiefste.append(min((mw @ v.co).z for v in me.vertices))
        ev.to_mesh_clear()
    rig.animation_data.action = None
    # unteres Viertel der tiefsten Punkte je Bild: der Standfuß steht auf 0, ein kurz abtauchender Zeh beim Abdruck hebt nicht den ganzen Clip
    tiefste.sort()
    d = -tiefste[len(tiefste) // 4]
    if abs(d) > 0.002:
        lokal = rig.data.bones["Hips"].matrix_local.to_3x3().inverted() @ (rig.matrix_world.to_3x3().inverted() @ Vector((0, 0, d)))
        for fc in _kurven(act, 'pose.bones["Hips"].location'):
            for k in fc.keyframe_points:
                k.co[1] += lokal[fc.array_index]
                k.handle_left[1] += lokal[fc.array_index]
                k.handle_right[1] += lokal[fc.array_index]
            fc.update()
    return d


def sitz_messen(rig, netz, act, tiefe):
    """Höhe des Gesäßes über den Fußsohlen beim Sitzen (erstes Bild): Hüftknochen minus Gesäßtiefe, gegen die tiefste Ecke."""
    rig.animation_data.action = act
    bpy.context.scene.frame_set(1)
    huefte = (rig.matrix_world @ rig.pose.bones["Hips"].head).z
    dg = bpy.context.evaluated_depsgraph_get()
    ev = netz.evaluated_get(dg)
    me = ev.to_mesh()
    mw = netz.matrix_world
    tief_alle = min((mw @ v.co).z for v in me.vertices)
    ev.to_mesh_clear()
    rig.animation_data.action = None
    return huefte - tiefe - tief_alle, tief_alle


# ─────────────────────────────────────────────────────────────────────────────
#  7. Last (Kiste beim Tragen)
# ─────────────────────────────────────────────────────────────────────────────

def werkzeug_bauen(rig, hacken):
    """Axt für „Hacken“: Stiel durch beide Fäuste in der ersten Haltung des Clips, Blatt hinter der rechten Hand. Jede Ecke hängt nach
    ihrer Lage auf dem Stiel an der linken bzw. rechten Hand (das Blatt ganz rechts) und wird mit dieser Mischung in die Ruhelage gerechnet."""
    rig.animation_data.action = hacken
    bpy.context.scene.frame_set(1)
    mw = rig.matrix_world
    pose = {s: mw @ rig.pose.bones[f"{s}Hand"].matrix for s in ("Left", "Right")}
    ruhe = {s: mw @ rig.data.bones[f"{s}Hand"].matrix_local for s in ("Left", "Right")}
    # Faust: etwa 8 cm vom Handgelenk entlang der Hand
    faust = {s: pose[s] @ Vector((0, 0.08, 0)) for s in ("Left", "Right")}
    rig.animation_data.action = None
    bpy.context.scene.frame_set(1)
    l, r = faust["Left"], faust["Right"]
    u = (r - l).normalized()
    anfang, ende = l - u * 0.12, r + u * 0.30
    seite = u.cross(Vector((0, 0, 1)))
    if seite.length < 1e-3:
        seite = Vector((1, 0, 0))
    seite.normalize()
    oben = seite.cross(u).normalized()
    bm = bmesh.new()
    teile = []

    def quader(mitte, a, b, c, stoffname):
        erg = bmesh.ops.create_cube(bm, size=1.0)
        for v in erg["verts"]:
            p = v.co.copy()
            v.co = mitte + a * p.x + b * p.y + c * p.z
        teile.append((set(erg["verts"]), stoffname))

    laenge = (ende - anfang).length
    quader((anfang + ende) / 2, u * laenge, seite * 0.035, oben * 0.035, "Holz")
    quader(ende - u * 0.04 + oben * 0.05, u * 0.06, seite * 0.025, oben * 0.16, "Eisen")
    me = bpy.data.meshes.new("Werkzeug")
    misch = {}
    bm.verts.index_update()
    for v in bm.verts:
        t = max(0.0, min(1.0, (v.co - l).dot(u) / max((r - l).length, 1e-3)))
        misch[v.index] = t
        m = (pose["Left"] @ ruhe["Left"].inverted()) * (1 - t) + (pose["Right"] @ ruhe["Right"].inverted()) * t
        v.co = m.inverted() @ v.co
    stoff_je_flaeche = []
    for f in bm.faces:
        name = next(n for vs, n in teile if f.verts[0] in vs)
        stoff_je_flaeche.append(name)
    bm.to_mesh(me)
    bm.free()
    obj = neues_objekt("Werkzeug", me)
    stoffe_anlegen(obj)
    for p, name in zip(me.polygons, stoff_je_flaeche):
        p.material_index = stoff(name)
    gl = obj.vertex_groups.new(name="LeftHand")
    gr = obj.vertex_groups.new(name="RightHand")
    for i, t in misch.items():
        if 1 - t > 1e-4:
            gl.add([i], 1 - t, "REPLACE")
        if t > 1e-4:
            gr.add([i], t, "REPLACE")
    zonen_setzen(obj)
    me.polygons.foreach_set("use_smooth", [False] * len(me.polygons))
    obj.parent = rig
    obj.modifiers.new("Skelett", "ARMATURE").object = rig
    return obj


def _drehung_aus(achse_y_lokal, quer_lokal, achse_y_welt, quer_welt):
    """Drehung, die die Knochenachse (lokal Y) auf achse_y_welt und die Richtung quer_lokal möglichst auf quer_welt bringt."""
    def basis(a, b):
        a = a.normalized()
        b = (b - a * b.dot(a)).normalized()
        return Matrix((a, b, a.cross(b))).transposed()
    return (basis(achse_y_welt, quer_welt) @ basis(achse_y_lokal, quer_lokal).inverted()).to_quaternion()


def betteln_bauen(rig, kauern, name="Betteln"):
    """Bettelgeste (Schritt 2): „Kauern“ (Quaternius Crouch_Idle_Loop), dazu streckt die rechte Hand die offene Fläche nach vorn oben und
    hebt und senkt sich langsam. Die Hand der Ruhelage zeigt mit der Fläche zum Oberschenkel (+X für die rechte Hand); diese Richtung wird
    nach oben gedreht."""
    neu = kauern.copy()
    neu.name = name
    rig.animation_data.action = neu
    a, e = int(neu.frame_range[0]), int(neu.frame_range[1])
    mw = rig.matrix_world
    kn = rig.data.bones
    flaeche_ruhe = Vector((1.0, 0.0, 0.0))     # rechte Hand: Fläche zum Körper
    ziele = {  # Knochen: (Richtung der Knochenachse, Richtung der Handfläche) in der Welt; die Figur schaut nach −Y, rechts ist −X
        "RightUpperArm": (Vector((-0.25, -0.65, -0.55)), Vector((0, 0, 1))),
        "RightLowerArm": (Vector((-0.08, -0.95, 0.10)), Vector((0, 0, 1))),
        "RightHand": (Vector((0.0, -0.92, 0.05)), Vector((0, 0, 1))),
    }
    for bild in range(a, e + 1):
        bpy.context.scene.frame_set(bild)
        schwung = 0.08 * math.sin(2 * math.pi * (bild - a) / max(e - a, 1))
        for knochen, (achse, flaeche) in ziele.items():
            b = kn[knochen]
            ruhe = (mw @ b.matrix_local).to_3x3()
            lokal_flaeche = ruhe.inverted() @ flaeche_ruhe
            achse_w = (achse + Vector((0, 0, schwung if knochen != "RightUpperArm" else schwung * 0.5))).normalized()
            welt = _drehung_aus(Vector((0, 1, 0)), lokal_flaeche, achse_w, flaeche)
            pb = rig.pose.bones[knochen]
            arm = mw.to_quaternion().inverted() @ welt
            pb.matrix = Matrix.LocRotScale(pb.matrix.to_translation(), arm, None)
            bpy.context.view_layer.update()
            pb.keyframe_insert("rotation_quaternion", frame=bild)
    rig.animation_data.action = None
    spur = rig.animation_data.nla_tracks.new()
    spur.name = name
    spur.strips.new(name, 1, neu)
    return neu, e - a


def zubehoer_masse(koerper, masse):
    """Maße am Körpernetz für Hüte, Kiepe und Tasche: Scheitel, Rücken und Brust in Brusthöhe, Hüftbreite."""
    zs = [v.co.copy() for v in koerper.data.vertices]
    masse["kopf_oben"] = max(c.z for c in zs)
    brust = [c for c in zs if masse["schluesselbein"] - 0.28 < c.z < masse["schluesselbein"] - 0.05 and abs(c.x) < 0.12]
    masse["ruecken_y"] = max(c.y for c in brust)
    masse["brust_y"] = min(c.y for c in brust)
    masse["huefte_x"] = max(c.x for c in zs if abs(c.z - masse["huefte"]) < 0.04)


def schuerze_bauen(rock, masse):
    """Schürze (Knecht und Magd): eine eigene, gerade geschnittene Bahn vom Gürtel bis eine Handbreit über das Knie, oben 30 cm, unten 38 cm
    breit, 3 cm vor dem Rock (Strahl von vorn auf den knielangen Rock); jede Ecke nimmt die Gewichte der getroffenen Rockfläche, damit die
    Schürze den Beinen folgt. Farbe der Kopfbedeckung (Zone 5, im Spiel Leinen). Runde 5: aus Rockflächen ausgeschnitten war der Rand gezackt
    und der Rock stach durch."""
    from mathutils.bvhtree import BVHTree
    namen = gruppen_index(rock)
    bm_r = bmesh.new()
    bm_r.from_mesh(rock.data)
    bm_r.faces.ensure_lookup_table()
    deform = bm_r.verts.layers.deform.active
    baum = BVHTree.FromBMesh(bm_r)
    oben, unten = masse["guertel"] - 0.01, masse["knie"] + 0.04
    spalten, zeilen = 6, 6
    bm = bmesh.new()
    raster, gewichte = [], []
    for j in range(zeilen + 1):
        v_ = j / zeilen
        z = oben + (unten - oben) * v_
        halb = 0.15 + 0.04 * v_
        reihe = []
        for i in range(spalten + 1):
            x = -halb + 2 * halb * i / spalten
            lage, _, index, _ = baum.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
            g = {}
            if lage is None:
                lage = Vector((x, masse["kopfmitte"].y - 0.16, z))
                g = {"pelvis": 1.0}
            else:
                f = bm_r.faces[index]
                for ecke in f.verts:
                    for gi, w in ecke[deform].items():
                        g[namen[gi]] = g.get(namen[gi], 0.0) + w / len(f.verts)
                lage = lage + Vector((0, -0.03, 0))
            reihe.append(bm.verts.new(lage))
            gewichte.append(g)
        raster.append(reihe)
    for j in range(zeilen):
        for i in range(spalten):
            bm.faces.new((raster[j][i], raster[j + 1][i], raster[j + 1][i + 1], raster[j][i + 1]))   # Fläche nach vorn (−Y)
    bm_r.free()
    me = bpy.data.meshes.new("Schuerze")
    bm.to_mesh(me)
    bm.free()
    obj = neues_objekt("Schuerze", me)
    stoffe_anlegen(obj)
    for p_ in me.polygons:
        p_.material_index = stoff("Kopf")
    for i, g in enumerate(gewichte):
        for n, w in g.items():
            gruppe = obj.vertex_groups.get(n) or obj.vertex_groups.new(name=n)
            gruppe.add([i], w, "REPLACE")
    return obj


def _zubehoer_objekt(rig, name, bm, stoffe_je_flaeche, gewichte):
    """bmesh → Objekt am Skelett: beidseitig (jede Fläche doppelt, umgedreht), Stoff je Fläche, feste Gewichte {Knochen: Anteil} oder je Ecke."""
    flaechen = list(bm.faces)
    for f, s in zip(flaechen, list(stoffe_je_flaeche)):
        f.material_index = stoff(s)
    kopie_ = bmesh.ops.duplicate(bm, geom=flaechen)
    bmesh.ops.reverse_faces(bm, faces=[g for g in kopie_["geom"] if isinstance(g, bmesh.types.BMFace)])
    bm.verts.index_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    zahl = len(bm.verts)
    lagen = [v.co.copy() for v in bm.verts]
    bm.free()
    obj = neues_objekt(name, me)
    mats = [f.material_index for f in me.polygons]
    stoffe_anlegen(obj)
    me.polygons.foreach_set("material_index", mats)
    for i in range(zahl):
        g = gewichte(lagen[i]) if callable(gewichte) else gewichte
        for knochen, w in g.items():
            gruppe = obj.vertex_groups.get(knochen) or obj.vertex_groups.new(name=knochen)
            gruppe.add([i], w, "REPLACE")
    zonen_setzen(obj)
    me.polygons.foreach_set("use_smooth", [False] * len(me.polygons))
    obj.parent = rig
    obj.modifiers.new("Skelett", "ARMATURE").object = rig
    return obj


def _drehkoerper(bm, profil, mitte, n=12):
    """Rotationskörper um die Senkrechte durch mitte (x, y) aus (Halbmesser, Höhe), von unten nach oben; oben geschlossen."""
    ringe = [[bm.verts.new((mitte[0] + r * math.cos(2 * math.pi * i / n), mitte[1] + r * math.sin(2 * math.pi * i / n), z)) for i in range(n)]
             for r, z in profil]
    neu = []
    for a, b in zip(ringe, ringe[1:]):
        for i in range(n):
            neu.append(bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i])))
    neu.append(bm.faces.new(ringe[-1]))
    return neu


def _zylinder(bm, a, b, r, n=6):
    u = (b - a).normalized()
    q = u.cross(Vector((0, 0, 1)))
    if q.length < 1e-3:
        q = u.cross(Vector((1, 0, 0)))
    q.normalize()
    w = u.cross(q)
    ringe = [[bm.verts.new(p + r * (math.cos(2 * math.pi * i / n) * q + math.sin(2 * math.pi * i / n) * w)) for i in range(n)] for p in (a, b)]
    neu = [bm.faces.new((ringe[0][i], ringe[0][(i + 1) % n], ringe[1][(i + 1) % n], ringe[1][i])) for i in range(n)]
    neu += [bm.faces.new(ringe[0]), bm.faces.new(ringe[1])]
    return neu


def _kasten(bm, unten_mitte, unten_halb, oben_mitte, oben_halb):
    """Kasten mit unten und oben eigener halber Breite und Tiefe (x, y), Mitten (x, y, z)."""
    ecken = []
    for mitte, (hx, hy) in ((unten_mitte, unten_halb), (oben_mitte, oben_halb)):
        ecken.append([bm.verts.new(mitte + Vector((sx * hx, sy * hy, 0))) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    u, o = ecken
    neu = [bm.faces.new((u[i], u[(i + 1) % 4], o[(i + 1) % 4], o[i])) for i in range(4)]
    neu += [bm.faces.new(list(reversed(u))), bm.faces.new(o)]
    return neu


def zubehoer_bauen(rig, masse, stehen):
    """Berufszubehör (Schritt 4 des Vollausbaus) als eigene Netze am Skelett, im Spiel nach Tracht und Rolle zugeschaltet:
    Strohhut (Hirte, Feldarbeiter), Barett (Ratsherr, Bürgersohn), Eisenhut (Wache), Kiepe (Krämer), Reisigbündel (Holzsammler),
    Tasche (Bote), Stab (Hirte). Hüte am Kopf, Kiepe und Bündel am Rücken, Tasche an der Hüfte, der Stab in der rechten Faust (gestellt in
    der ersten Haltung von „Stehen“ und in die Ruhelage zurückgerechnet wie die Axt)."""
    k = masse["kopfmitte"]
    ko = masse["kopf_oben"]
    kopf_xy = (0.0, k.y + 0.008)
    aus = {}

    bm = bmesh.new()
    b = ko - 0.062
    f = _drehkoerper(bm, [(0.19, b - 0.018), (0.108, b), (0.104, ko + 0.006), (0.074, ko + 0.032)], kopf_xy, n=14)
    aus["stroh"] = _zubehoer_objekt(rig, "Zub_stroh", bm, ["Stroh"] * len(f), {"Head": 1.0})

    bm = bmesh.new()
    f = _drehkoerper(bm, [(0.104, ko - 0.048), (0.128, ko - 0.004), (0.122, ko + 0.022), (0.06, ko + 0.03)], kopf_xy, n=14)
    aus["barett"] = _zubehoer_objekt(rig, "Zub_barett", bm, ["Kopf"] * len(f), {"Head": 1.0})

    bm = bmesh.new()
    b = ko - 0.072
    f = _drehkoerper(bm, [(0.2, b - 0.022), (0.118, b), (0.116, ko - 0.02), (0.088, ko + 0.022), (0.04, ko + 0.036)], kopf_xy, n=14)
    aus["eisenhut"] = _zubehoer_objekt(rig, "Zub_eisenhut", bm, ["Eisen"] * len(f), {"Head": 1.0})

    ry = masse["ruecken_y"]
    sb = masse["schluesselbein"]
    bm = bmesh.new()
    f = _kasten(bm, Vector((0, ry + 0.15, masse["huefte"] + 0.04)), (0.15, 0.11), Vector((0, ry + 0.19, sb + 0.05)), (0.21, 0.15))
    rueck = {"UpperChest": 0.65, "Chest": 0.35}
    aus["kiepe"] = _zubehoer_objekt(rig, "Zub_kiepe", bm, ["Holz"] * len(f), rueck)

    bm = bmesh.new()
    f = []
    for i, (dx, dz) in enumerate(((0, 0), (0.035, 0.05), (-0.03, 0.045), (0.01, -0.05), (-0.04, -0.03))):
        a = Vector((-0.30 + dx, ry + 0.10, sb - 0.36 + dz))
        e = Vector((0.32 + dx, ry + 0.10, sb + 0.10 + dz))
        f += _zylinder(bm, a, e, 0.03, n=5)
    aus["buendel"] = _zubehoer_objekt(rig, "Zub_buendel", bm, ["Reisig"] * len(f), rueck)

    hx = masse["huefte_x"] + 0.03
    bm = bmesh.new()
    f = _kasten(bm, Vector((hx, k.y - 0.01, masse["huefte"] - 0.10)), (0.03, 0.10), Vector((hx, k.y - 0.01, masse["huefte"] + 0.06)), (0.025, 0.09))
    aus["tasche"] = _zubehoer_objekt(rig, "Zub_tasche", bm, ["Guertel"] * len(f), {"Hips": 1.0})

    # Stab: senkrecht in der rechten Faust der ersten Haltung von „Stehen“, dann in die Ruhelage der Hand zurück
    rig.animation_data.action = stehen
    bpy.context.scene.frame_set(1)
    mw = rig.matrix_world
    pose = mw @ rig.pose.bones["RightHand"].matrix
    ruhe = mw @ rig.data.bones["RightHand"].matrix_local
    faust = pose @ Vector((0, 0.08, 0))
    rig.animation_data.action = None
    bpy.context.scene.frame_set(1)
    bm = bmesh.new()
    f = _zylinder(bm, faust - Vector((0, 0, faust.z - 0.02)), faust + Vector((0, 0, 0.62)), 0.017, n=6)
    zurueck = (pose @ ruhe.inverted()).inverted()
    for v in bm.verts:
        v.co = zurueck @ v.co
    aus["stab"] = _zubehoer_objekt(rig, "Zub_stab", bm, ["Holz"] * len(f), {"RightHand": 1.0})
    return aus


def haltung_beugen(rig, act, grad):
    """Leicht gebeugte Haltung (Orks): Rücken um grad Grad nach vorn (auf Spine, Chest, UpperChest verteilt), Hals und Kopf richten den Blick
    wieder fast nach vorn. Geändert werden die Schlüssel der Drehkurven (wie schleife_schliessen), jede mit derselben festen Drehung davor."""
    verteilung = {"Spine": 0.3, "Chest": 0.35, "UpperChest": 0.35, "Neck": -0.45, "Head": -0.4}
    for k, anteil in verteilung.items():
        achse = (rig.data.bones[k].matrix_local.to_3x3().inverted() @ Vector((1, 0, 0))).normalized()
        extra = Quaternion(achse, math.radians(grad * anteil))
        kurven = _kurven(act, f'pose.bones["{k}"].rotation_quaternion')
        if len(kurven) != 4:
            BERICHT.setdefault("warnungen", []).append(f"haltung_beugen: {k} ohne Drehkurven in {act.name}")
            continue
        n = min(len(c.keyframe_points) for c in kurven)
        for i in range(n):
            q = extra @ Quaternion([kurven[j].keyframe_points[i].co[1] for j in range(4)])
            for j in range(4):
                kp = kurven[j].keyframe_points[i]
                d = q[j] - kp.co[1]
                kp.co[1] = q[j]
                kp.handle_left[1] += d
                kp.handle_right[1] += d
        for c in kurven:
            c.update()


def last_bauen(rig, tragen):
    """Kiste zwischen den Händen in der ersten Haltung von „Tragen“; je zur Hälfte an beiden Händen."""
    rig.animation_data.action = tragen
    bpy.context.scene.frame_set(1)
    mw = rig.matrix_world
    hl = mw @ rig.pose.bones["LeftHand"].matrix
    hr = mw @ rig.pose.bones["RightHand"].matrix
    ruhe_l = mw @ rig.data.bones["LeftHand"].matrix_local
    ruhe_r = mw @ rig.data.bones["RightHand"].matrix_local
    mitte = (hl.to_translation() + hr.to_translation()) / 2
    # schmaler als der Abstand der Handgelenke: die gekrümmten Finger stachen sonst durch die Seiten (Runde 4)
    breite = max(0.26, (hl.to_translation() - hr.to_translation()).length - 0.11)
    rig.animation_data.action = None
    bpy.context.scene.frame_set(1)
    groesse = Vector((breite, 0.30, 0.26))
    zentrum = mitte + Vector((0, -0.05, -0.06))
    me = bpy.data.meshes.new("Last")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    misch = ((hl @ ruhe_l.inverted()) + (hr @ ruhe_r.inverted())) * 0.5
    inv = misch.inverted()
    for v in bm.verts:
        v.co = inv @ (zentrum + Vector((v.co.x * groesse.x, v.co.y * groesse.y, v.co.z * groesse.z)))
    bm.to_mesh(me)
    bm.free()
    obj = neues_objekt("Last", me)
    stoffe_anlegen(obj)
    for p in me.polygons:
        p.material_index = stoff("Kiste")
    for n in ("LeftHand", "RightHand"):
        obj.vertex_groups.new(name=n).add(list(range(len(me.vertices))), 0.5, "REPLACE")
    zonen_setzen(obj)
    me.polygons.foreach_set("use_smooth", [False] * len(me.polygons))
    obj.parent = rig
    obj.modifiers.new("Skelett", "ARMATURE").object = rig
    return obj


# ─────────────────────────────────────────────────────────────────────────────
#  8. Ablauf je Figur
# ─────────────────────────────────────────────────────────────────────────────

def masse_messen(rig):
    b = rig.data.bones
    kopf = b["head"]
    aus = {
        "huefte": b["thigh_l"].head_local.z,
        "oberschenkel": (b["thigh_l"].head_local - b["calf_l"].head_local).length,
        "knie": b["calf_l"].head_local.z,
        "knoechel": b["foot_l"].head_local.z,
        "guertel": b["pelvis"].head_local.z + 0.035,
        "kopfmitte": (kopf.head_local + kopf.tail_local) / 2,
        "kinn": kopf.head_local.z - 0.035,
        "stirn": kopf.head_local.z + 0.115,
        "hals_kopf": b["neck_01"].head_local.copy(),
        "hals_fuss": b["neck_01"].head_local.z,
        "schluesselbein": b["clavicle_l"].head_local.z,
        "kragen": b["clavicle_l"].head_local.z - 0.10,
    }
    for s in ("l", "r"):
        aus[f"handgelenk_{s}"] = b[f"hand_{s}"].head_local.copy()
        aus[f"unterarm_{s}"] = (b[f"hand_{s}"].head_local - b[f"lowerarm_{s}"].head_local).normalized()
        aus[f"ballen_{s}"] = b[f"ball_{s}"].head_local.copy()
        aus[f"ellbogen_{s}"] = b[f"lowerarm_{s}"].head_local.copy()
        aus[f"schulter_{s}"] = b[f"upperarm_{s}"].head_local.copy()
    return aus


# ─────────────────────────────────────────────────────────────────────────────
#  Völker: Ohren, Bärte, langes Haar (Zwerge und Elben, Auftrag 10.11.2026)
# ─────────────────────────────────────────────────────────────────────────────

def ohr_lagen(koerper, masse):
    """Mitte beider Ohren (die äußersten Kopfecken je Seite): Das Haar der Elben lässt sie frei."""
    namen = gruppen_index(koerper)
    k = masse["kopfmitte"]
    kopf = []
    for v in koerper.data.vertices:
        w = sum(e.weight for e in v.groups if namen[e.group] == "head")
        if w > 0.5 and abs(v.co.y - k.y) < 0.07 and masse["kinn"] < v.co.z < masse["kopf_oben"] - 0.02:
            kopf.append(v.co.copy())
    aus = []
    for s_ in (1, -1):
        seite_ = [c for c in kopf if c.x * s_ > 0]
        xm = max(abs(c.x) for c in seite_)
        spitzen = [c for c in seite_ if abs(c.x) > xm - 0.015]
        aus.append(sum(spitzen, Vector()) / len(spitzen))
    return aus


OHR_LOCH = 0.052      # Halbmesser der Aussparung im Haar um die Ohrmitte


def ohr_abstand(co, masse):
    return min((co - o).length for o in masse["ohren"]) - OHR_LOCH


def netz_baum(koerper):
    """BVH-Baum des Körpers (Kopien der Ecken: bleibt gültig, wenn der Körper später entfernt wird)."""
    from mathutils.bvhtree import BVHTree
    ecken = [v.co.copy() for v in koerper.data.vertices]
    flaechen = [tuple(p.vertices) for p in koerper.data.polygons]
    return BVHTree.FromPolygons(ecken, flaechen)


def _schnitt(baum, x, z, hinten):
    """y des Körpers an (x, z): von vorn (hinten=False, erster Treffer) oder von hinten, sonst None."""
    start = Vector((x, 1.0 if hinten else -1.0, z))
    ort, _, _, _ = baum.ray_cast(start, Vector((0, -1.0 if hinten else 1.0, 0)))
    return None if ort is None else ort.y


def _gitter(bm, ecken, hinten=False):
    """Vierecksgitter aus ecken[i][j] (Reihe, Spalte); Reihenfolge der Ecken so, dass die Normalen nach außen zeigen."""
    flaechen = []
    for i in range(len(ecken) - 1):
        for j in range(len(ecken[0]) - 1):
            a, b, c, d = ecken[i][j], ecken[i][j + 1], ecken[i + 1][j + 1], ecken[i + 1][j]
            flaechen.append(bm.faces.new((a, b, c, d) if not hinten else (a, d, c, b)))
    return flaechen


def _glatt(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


# Bartformen: (Länge unter dem Kinn, halbe Breite oben, Oberkante an der Mitte relativ zum Mund, Spitze, Zöpfe)
BART_FORMEN = {
    "bart": dict(laenge=0.23, breite=0.078, oben=0.010, spitze=0.05, zopf=0),
    "bartzopf": dict(laenge=0.09, breite=0.076, oben=0.010, spitze=0.03, zopf=1),
    "bartgabel": dict(laenge=0.07, breite=0.076, oben=0.010, spitze=0.02, zopf=2),
    "bartkurz": dict(laenge=0.045, breite=0.056, oben=-0.014, spitze=0.025, zopf=0),
}


def mund_lage(koerper, masse):
    """(y der vordersten Gesichtspunkte, z des Mundes) wie in bart_zone."""
    k = masse["kopfmitte"]
    vorn = [v.co for v in koerper.data.vertices if abs(v.co.x) < 0.035 and masse["kinn"] - 0.01 < v.co.z < masse["kinn"] + 0.05 and v.co.y < k.y]
    y_vorn = min(c.y for c in vorn) if vorn else k.y - 0.09
    mund = sorted(c.z for c in vorn if c.y < y_vorn + 0.02)
    return y_vorn, (mund[len(mund) // 3] if mund else masse["kinn"] + 0.02)


def bart_bauen(rig, baum, masse, mund, name):
    """Bart eines Zwergs als eigenes Netz am Skelett (Zone 4, Haarfarbe): ein Tuch vom Schnurrbart und den Koteletten bis zur Spitze, das dem Körper
    (Kinn, Hals, Brust) mit wachsendem Abstand folgt (Strahlen von vorn); „zopf“ und „gabel“ hängen die Enden als ein bzw. zwei geflochtene Zöpfe
    mit Band an."""
    f = BART_FORMEN[name]
    _, z_mund = mund
    bm = bmesh.new()
    n_r, n_s = 9, 13
    a0, z_ende0 = f["breite"], masse["kinn"] - f["laenge"]
    y_vor = [None] * (n_s + 1)
    gitter = []
    gew = {}
    for i in range(n_r + 1):
        v = i / n_r
        reihe = []
        for j in range(n_s + 1):
            s_ = -1 + 2 * j / n_s
            a = a0 * (0.88 + 0.30 * math.sin(math.pi * min(v * 1.7, 1.0))) * (1 - 0.62 * v ** 1.6)
            x = a * s_
            z_oben = z_mund + f["oben"] + 0.030 * abs(s_) ** 3
            z_unten = z_ende0 - f["spitze"] * (1 - abs(s_) ** 1.5)
            z = z_oben + (z_unten - z_oben) * v
            y = _schnitt(baum, x, z, False)
            if y is None:
                y = y_vor[j] if y_vor[j] is not None else mund[0]
            if y_vor[j] is not None:
                y = min(y, y_vor[j])
            y_vor[j] = y
            dicke = 0.008 + 0.040 * _glatt(v / 0.45) * (1 - 0.35 * abs(s_) ** 2) - 0.012 * v * v
            y_ = y - dicke - 0.005 * math.sin(s_ * 11.0 + v * 3.0) * v
            p = Vector((x, y_, z))
            reihe.append(bm.verts.new(p))
            gew[reihe[-1]] = min(max((masse["kinn"] - 0.01 - z) / 0.10, 0.0), 0.85)     # Anteil Brustkorb: oben am Kopf, unten am Brustkorb
        gitter.append(reihe)
    flaechen = _gitter(bm, gitter)
    stoffe = ["Haar"] * len(flaechen)
    for z_, (x_zopf) in enumerate([0.0] if f["zopf"] == 1 else [-0.022, 0.022] if f["zopf"] == 2 else []):
        # Zopf: Reihe überlappender, abwechselnd gekippter Wülste von der Barthöhe bis 0,22 m unter dem Kinn, Band am Ende
        z0 = z_ende0 - f["spitze"] * 0.5
        n_w, h_w = 7, 0.032
        for w in range(n_w):
            z = z0 - w * h_w * 0.82
            y = _schnitt(baum, x_zopf, z, False)
            y = (y if y is not None else mund[0]) - 0.050
            kipp = 0.006 * (1 if w % 2 else -1)
            g = bmesh.ops.create_uvsphere(bm, u_segments=5, v_segments=3, radius=1.0)
            for vv in g["verts"]:
                vv.co = Vector((x_zopf + kipp + vv.co.x * 0.014, y + vv.co.y * 0.012, z + vv.co.z * 0.016))
                gew[vv] = 0.85
            fl = list({fa for vv in g["verts"] for fa in vv.link_faces})
            flaechen += fl
            stoffe += ["Haar"] * len(fl)
        z_b = z0 - n_w * h_w * 0.82 - 0.004
        y_b = (_schnitt(baum, x_zopf, z_b, False) or mund[0]) - 0.050
        band = _zylinder(bm, Vector((x_zopf, y_b, z_b + 0.008)), Vector((x_zopf, y_b, z_b - 0.008)), 0.0105, n=6)
        for q in band:
            for vv in q.verts:
                gew[vv] = 0.85
        flaechen += band
        stoffe += ["Strick"] * len(band)
    # die Zöpfe sind eigene Netzteile: Stoff je Fläche in Reihenfolge von bm.faces
    reihenfolge = list(bm.faces)
    stoff_von = dict(zip(flaechen, stoffe))
    stoffe = [stoff_von.get(fa, "Haar") for fa in reihenfolge]
    return _zubehoer_gewichtet(rig, "Zub_" + name, bm, stoffe, lambda vv, w=gew: w.get(vv, 0.0), kopf_unten=True)


def _zubehoer_gewichtet(rig, name, bm, stoffe, anteil, kopf_unten=False):
    """Wie _zubehoer_objekt, aber die Gewichte je Ecke: Kopf (Head) bis Brustkorb (UpperChest) nach anteil(ecke) in 0–1."""
    ecken = list(bm.verts)
    bm.verts.index_update()
    w_von = [anteil(v) for v in ecken]
    lagen = {v.index: i for i, v in enumerate(ecken)}
    flaechen = list(bm.faces)
    for fa, s_ in zip(flaechen, stoffe):
        fa.material_index = stoff(s_)
    kopie_ = bmesh.ops.duplicate(bm, geom=flaechen)
    bmesh.ops.reverse_faces(bm, faces=[g for g in kopie_["geom"] if isinstance(g, bmesh.types.BMFace)])
    bm.verts.index_update()
    # die doppelten Ecken (Duplikat) erben die Gewichte der Vorlage: über die Lage zuordnen
    alle = list(bm.verts)
    lage_w = {tuple(round(c, 5) for c in v.co): w for v, w in zip(ecken, w_von)}
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    zahl = len(bm.verts)
    lagen_ = [v.co.copy() for v in alle]
    bm.free()
    obj = neues_objekt(name, me)
    mats = [f.material_index for f in me.polygons]
    stoffe_anlegen(obj)
    me.polygons.foreach_set("material_index", mats)
    for i in range(zahl):
        w = lage_w.get(tuple(round(c, 5) for c in lagen_[i]), 0.0)
        for knochen, g in (("Head", 1.0 - w), ("UpperChest", w)):
            if g > 0.001:
                (obj.vertex_groups.get(knochen) or obj.vertex_groups.new(name=knochen)).add([i], g, "REPLACE")
    zonen_setzen(obj)
    me.polygons.foreach_set("use_smooth", [False] * len(me.polygons))
    obj.parent = rig
    obj.modifiers.new("Skelett", "ARMATURE").object = rig
    return obj


def langhaar_bauen(rig, baum, masse, name="langhaar", ende=0.30):
    """Langes Haar der Elben am Rücken (Zone 4): Ein Tuch vom Nacken bis ende Meter unter dem Schlüsselbein, das dem Rücken folgt (Strahlen von
    hinten), unten ungleich lang; Strähnen als flache Wellen. Oben unter der Haarkappe, die den Kopf deckt."""
    bm = bmesh.new()
    n_r, n_s = 12, 12
    z_oben = masse["hals_fuss"] + 0.075
    z_ende = masse["schluesselbein"] - ende
    y_rueck = [None] * (n_s + 1)
    gitter = []
    gew = {}
    for i in range(n_r + 1):
        v = i / n_r
        reihe = []
        for j in range(n_s + 1):
            s_ = -1 + 2 * j / n_s
            a = 0.078 + 0.016 * math.sin(math.pi * min(v * 1.3, 1.0)) - 0.03 * v ** 3
            x = a * s_
            z = z_oben + (z_ende - 0.035 * abs(math.sin(j * 2.1)) - z_oben) * v
            y = _schnitt(baum, x * 0.7, z, True)
            if y is None:
                y = y_rueck[j] if y_rueck[j] is not None else masse["kopfmitte"].y + 0.05
            if y_rueck[j] is not None:
                y = max(y, y_rueck[j])
            y_rueck[j] = y
            dicke = 0.020 + 0.006 * math.sin(math.pi * v) + 0.004 * math.sin(s_ * 8.0 + v * 4.0)
            reihe.append(bm.verts.new(Vector((x * (1 + 0.15 * v), y + dicke, z))))
            gew[reihe[-1]] = _glatt((z_oben - 0.02 - z) / 0.20)
        gitter.append(reihe)
    flaechen = _gitter(bm, gitter, hinten=True)
    return _zubehoer_gewichtet(rig, "Zub_" + name, bm, ["Haar"] * len(flaechen), lambda vv, w=gew: w.get(vv, 0.0))



def figur_bauen(art, quellen):
    cfg = FIGUREN[art]
    for o in list(bpy.data.objects):
        if o.name not in [q.name for q in quellen.values()] and o not in QUELLNETZE.values() and o not in VERGLEICH:
            bpy.data.objects.remove(o)
    mensch, rig, makros = mensch_bauen(cfg["gender"], cfg.get("makros"), cfg.get("ziele"))
    haende_entspannen(mensch, rig)
    if cfg.get("arme"):
        glieder_verdicken(mensch, rig, cfg["arme"])
    masse = masse_messen(rig)
    bericht = {"makros": makros, "masse": {k: (list(v) if isinstance(v, Vector) else v) for k, v in masse.items()}, "netze": {},
               "bewegungen": {}}

    koerper = teil_kopieren(mensch, "Koerper", in_gruppe("body"))
    tiefe = gesaess_tiefe(koerper, rig)
    zubehoer_masse(koerper, masse)
    bericht["hoehe_m"] = masse["kopf_oben"]
    ha = "haarelb" if cfg.get("haarart") == "elb" else "haar"
    if ha == "haarelb":
        masse["ohren"] = ohr_lagen(koerper, masse)
    baum = netz_baum(koerper) if cfg.get("bartnetze") or cfg.get("langhaar") else None
    mund = mund_lage(koerper, masse) if cfg.get("bartnetze") else None
    if art in ZIEL_HOEHE:
        VERGLEICH_MASS[art] = ZIEL_HOEHE[art] / masse["kopf_oben"]
        bericht["massstab_ziel"] = VERGLEICH_MASS[art]
    rockhelfer = teil_kopieren(mensch, "Rockhelfer", in_gruppe("helper-skirt"))
    haarhelfer = teil_kopieren(mensch, "Haarhelfer", in_gruppe("helper-hair"))
    roh = kopie(mensch, "Roh")                     # für die Fußmaße der Schuhe
    bpy.data.objects.remove(mensch)
    schuhe = {form: [schuh_bauen(roh, s, masse, form) for s in ("l", "r")] for form in ("halb", "holz")}
    fuesse_roh = koerper_zonen(koerper, masse)
    if cfg.get("bart"):
        bart_zone(koerper, masse)
    bpy.data.objects.remove(roh)
    unter_guertel = masse["guertel"] - 0.03
    koerper_knie = koerper_aussparen(koerper, masse["knie"] + 0.02, unter_guertel, "KoerperKnie")
    koerper_lang = koerper_aussparen(koerper, masse["knoechel"] + 0.12, unter_guertel, "KoerperLang")
    rock_knie = rock_bauen(rockhelfer, masse, masse["knie"] - 0.03, "RockKnie")
    halb_saum = (masse["huefte"] + masse["knie"]) / 2 - 0.03          # Vorlage 09.11.: Cotte bis zur Mitte des Oberschenkels
    rock_halb = saum_fransen(rock_bauen(rockhelfer, masse, halb_saum, "RockHalb"))
    koerper_halb = koerper_aussparen(koerper, halb_saum + 0.03, unter_guertel, "KoerperHalb")
    rock_lang = rock_bauen(rockhelfer, masse, masse["knoechel"] + 0.05, "RockLang")
    hilfen = [koerper, koerper_knie, koerper_lang, rock_knie, rock_lang, rock_halb, koerper_halb, rockhelfer, haarhelfer, fuesse_roh] + schuhe["halb"] + schuhe["holz"]
    teile = {}
    if cfg["gender"] >= 0.5:
        haar = kopfteil_bauen(koerper, masse, "Haar", ha)[0]
        gugel = kopfteil_bauen(koerper, masse, "Gugel", "gugel")
        k_cotte = kopf_aussparen(koerper_halb, masse, ha, "KoerperCotte")
        k_gugel = kopf_aussparen(koerper_halb, masse, "gugel", "KoerperGugel")
        guertel_z = guertel_zubehoer(rock_halb, masse)
        hilfen.append(guertel_z)
        k_rock = kopf_aussparen(koerper_lang, masse, ha, "KoerperRock")
        k_cotte_knie = kopf_aussparen(koerper_knie, masse, ha, "KoerperCotteKnie")
        k_bettler = bettler_koerper(k_cotte_knie, masse, "KoerperBettler")
        hilfen.append(k_cotte_knie)
        r_bettler = bettler_rock(rock_knie, "RockBettler")
        k_kutte = kopf_aussparen(koerper_lang, masse, "gugel", "KoerperKutte")
        hilfen += [haar, k_cotte, k_gugel, k_rock, k_bettler, r_bettler, k_kutte] + gugel
        if "wams" in cfg["varianten"] or "sack" in cfg["varianten"]:
            rock_kurz = rock_bauen(rockhelfer, masse, masse["knie"] + 0.07, "RockKurz")
            # Körper unter dem kurzen Rock erst 9 cm über dem Knie aussparen (mit dem Knie-Körper blieb zwischen Saum und Bein eine Lücke)
            k_kurz_roh = koerper_aussparen(koerper, masse["knie"] + 0.09, unter_guertel, "KoerperKurzRoh")
            # Oger fast kahl (Vorlage): ohne Haar, der Kopf bleibt Haut
            k_kurz = k_kurz_roh if cfg.get("kahl") else kopf_aussparen(k_kurz_roh, masse, "haar", "KoerperKurz")
            hilfen += [k_kurz_roh, k_kurz]
            # Vorlage 09.11.: ärmellos, geflickt, ausgefranster Saum, Strick als Gürtel
            k_wams = aermellos(k_kurz, masse, "KoerperWams", "Wams", flicken=True)
            r_wams = stoff_tauschen(stoff_tauschen(bettler_rock(rock_kurz, "RockWamsRoh"), "RockWamsLeder", "Gewand", "Wams"), "RockWams", "Guertel", "Strick")
            k_sack = aermellos(k_kurz, masse, "KoerperSack", "Gewand", flicken=True)
            r_sack = stoff_tauschen(bettler_rock(rock_kurz, "RockSackRoh"), "RockSack", "Guertel", "Strick")
            for n_ in ("RockWamsRoh", "RockWamsLeder", "RockSackRoh"):
                hilfen.append(bpy.data.objects[n_])
            hilfen += [rock_kurz, k_wams, r_wams, k_sack, r_sack]
        teile = {
            "cotte": [k_cotte, rock_halb, haar, guertel_z],
            "gugel": [k_gugel, rock_halb, guertel_z] + gugel,
            "rock": [k_rock, rock_lang, haar],
            "bettler": [k_bettler, r_bettler, haar],
            "kutte": [k_kutte, rock_lang] + gugel,       # Mönch (Schritt 4): langer Rock mit Gugel
        }
        if "wams" in cfg["varianten"] or "sack" in cfg["varianten"]:
            kopf_ = [] if cfg.get("kahl") else [haar_struppig(kopie(haar, "HaarStruppig"))]
            hilfen += kopf_
            schnur = schnuerung_bauen(k_wams, masse)
            hilfen.append(schnur)
            teile["wams"] = [k_wams, r_wams, schnur] + kopf_
            teile["sack"] = [k_sack, r_sack] + kopf_
        if cfg.get("hauer"):
            hauer = hauer_bauen(koerper, masse, cfg["hauer"])
            hilfen.append(hauer)
            for liste in teile.values():
                liste.append(hauer)
    else:
        haube = kopfteil_bauen(koerper, masse, "Haube", "haube")
        gebende = kopfteil_bauen(koerper, masse, "Gebende", "gebende")
        k_haube = kopf_aussparen(koerper_lang, masse, "haube", "KoerperHaube")
        k_gebende = kopf_aussparen(koerper_lang, masse, "gebende", "KoerperGebende")
        k_bettler = bettler_koerper(k_haube, masse, "KoerperBettler")
        r_bettler = bettler_rock(rock_lang, "RockBettler")
        hilfen += haube + gebende + [k_haube, k_gebende, k_bettler, r_bettler]
        offen = []
        if "offen" in cfg["varianten"]:
            haar_f = kopfteil_bauen(koerper, masse, "Haar", ha)[0]
            k_offen = kopf_aussparen(koerper_lang, masse, ha, "KoerperOffen")
            offen = [k_offen, rock_lang, haar_f]
            hilfen += [haar_f, k_offen]
        teile = {
            "offen": offen,
            "haube": [k_haube, rock_lang] + haube,
            "gebende": [k_gebende, rock_lang] + gebende,
            "bettler": [k_bettler, r_bettler] + haube,
        }
    teile = {k: v for k, v in teile.items() if k in cfg["varianten"]}
    netze = []
    for variante, liste in teile.items():
        for stufe, ziel in (("Nah", NAH_ZIEL), ("Fern", FERN_ZIEL)):
            obj = zusammenfuegen(liste, f"{stufe}_{variante}")
            vorher = dreiecke(obj)
            gewichte_aufraeumen(obj)
            erhalten_markieren(obj, fern=stufe == "Fern")
            nachher = verkleinern(obj, ziel, True)
            zonen_setzen(obj)
            bericht["netze"][obj.name] = {"dreiecke_vorher": vorher, "dreiecke": nachher, "ecken": len(obj.data.vertices)}
            netze.append(obj)
    # Fußformen als eigene Netze (nah und fern dieselben): Halbschuh, Holzschuh, barfuß
    fuesse = {}
    schuerze = schuerze_bauen(rock_knie, masse)
    hilfen.append(schuerze)
    extra = [("schuerze", [schuerze])]
    for form, liste in [("halb", schuhe["halb"]), ("holz", schuhe["holz"]), ("barfuss", [fuesse_roh])] + extra:
        obj = zusammenfuegen(liste, f"Zub_{form}" if form in ("schuerze", "bart") else f"Fuss_{form}")
        vorher = dreiecke(obj)
        gewichte_aufraeumen(obj)
        nachher = verkleinern(obj, FUSS_ZIEL.get(form, 160), False)
        zonen_setzen(obj)
        bericht["netze"][obj.name] = {"dreiecke_vorher": vorher, "dreiecke": nachher, "ecken": len(obj.data.vertices)}
        fuesse[form] = obj
    zubehoer = {z: fuesse.pop(z) for z in ("schuerze", "bart") if z in fuesse}
    for t in {id(h): h for h in hilfen}.values():      # ohne Doppelte (der kahle Oger nimmt den Körper ohne Kopfschnitt)
        if t.name in bpy.data.objects:
            bpy.data.objects.remove(t)

    skelett_umbauen(rig, "Skelett")
    for obj in netze + list(fuesse.values()) + list(zubehoer.values()):
        obj.parent = rig
        for m in list(obj.modifiers):
            obj.modifiers.remove(m)
        obj.modifiers.new("Skelett", "ARMATURE").object = rig
    bericht["knochen"] = [b.name for b in rig.data.bones]
    bericht["huefte_m"] = rig.data.bones["LeftUpperLeg"].head_local.z

    aktionen = {}
    for name, (q, clip) in BEWEGUNGEN.items():
        neu, bilder = uebertragen(quellen[q], rig, clip_aktion(clip), name)
        aktionen[name] = (neu, bilder)
        bericht["bewegungen"][name] = {"quelle": q, "clip": clip, "bilder": bilder, "sekunden": bilder / FPS}
        if name in SCHLEIFE_SCHLIESSEN:
            schleife_schliessen(rig, neu)
        if cfg.get("beugen"):
            haltung_beugen(rig, neu, cfg["beugen"])
        # Bodenkontakt am Halbschuh gemessen (die Füße sind das Tiefste; Holzschuh und barfuß haben dieselbe Sohle bis auf Millimeter)
        bericht["bewegungen"][name]["boden_korrektur_m"] = bodenkontakt(rig, fuesse["halb"], neu, bilder)
    aktionen["Betteln"] = betteln_bauen(rig, aktionen["Kauern"][0])
    bericht["bewegungen"]["Betteln"] = {"quelle": "Kauern", "clip": "abgeleitet", "bilder": aktionen["Betteln"][1]}
    for name in ("Gehen", "Tragen"):
        bericht["bewegungen"][name]["tempo_m_s"] = tempo_messen(rig, aktionen[name][0], aktionen[name][1])
    sitz, sohle = sitz_messen(rig, fuesse["halb"], aktionen["Sitzen"][0], tiefe)
    bericht["sitzhoehe_m"] = sitz
    bericht["sitzen_sohle_z"] = sohle
    last = last_bauen(rig, aktionen["Tragen"][0])
    bericht["netze"]["Last"] = {"dreiecke": dreiecke(last), "ecken": len(last.data.vertices)}
    werkzeug = werkzeug_bauen(rig, aktionen["Hacken"][0])
    bericht["netze"]["Werkzeug"] = {"dreiecke": dreiecke(werkzeug), "ecken": len(werkzeug.data.vertices)}
    zubehoer.update(zubehoer_bauen(rig, masse, aktionen["Stehen"][0]))
    # Bärte der Zwerge und langes Haar der Elben (eigene Netze, im Spiel nach Erbgut und Variante zugeschaltet)
    for bn in cfg.get("bartnetze", ()):
        name = {"voll": "bart", "zopf": "bartzopf", "gabel": "bartgabel", "kurz": "bartkurz"}[bn]
        zubehoer[name] = bart_bauen(rig, baum, masse, mund, name)
    if cfg.get("langhaar"):
        zubehoer["langhaar"] = langhaar_bauen(rig, baum, masse)
    for name, obj in zubehoer.items():
        bericht["netze"][obj.name] = {"dreiecke": dreiecke(obj), "ecken": len(obj.data.vertices)}
    bericht["uebertragung"] = BERICHT.pop("uebertragung", {})
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()

    pfad = os.path.abspath(os.path.join(OPT["aus"], cfg["datei"]))
    os.makedirs(os.path.dirname(pfad), exist_ok=True)
    for o in bpy.context.scene.objects:
        o.select_set(o in netze or o in fuesse.values() or o in zubehoer.values() or o is rig or o is last or o is werkzeug)
    bpy.ops.export_scene.gltf(
        filepath=pfad, export_format="GLB", use_selection=True, export_yup=True, export_apply=False,
        export_extras=True, export_normals=True, export_texcoords=True, export_skins=True, export_animations=True,
        export_animation_mode="NLA_TRACKS", export_anim_slide_to_zero=True, export_morph=False, export_lights=False,
        export_cameras=False, export_image_format="NONE", export_vertex_color="ACTIVE", export_all_vertex_colors=False)
    print("Geschrieben:", pfad)
    if "bilder" in OPT:
        vorschau(art, rig, netze, fuesse, last, werkzeug, aktionen, OPT["bilder"], quellen, zubehoer)
    return bericht


def hauptteil():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    bpy.context.scene.render.fps = FPS
    quellen = {"ual1": ual_laden(OPT["ual1"]), "ual2": ual_laden(OPT["ual2"])}
    for art in FIGUREN:
        if art in OPT.get("nur", art).split(","):
            BERICHT[art] = figur_bauen(art, quellen)
    if len({k["volk"] for k in VERGLEICH}) > 1 and "bilder" in OPT:
        vergleich_rendern(OPT["bilder"])
    if "bericht" in OPT:
        with open(OPT["bericht"], "w", encoding="utf-8") as f:
            json.dump(BERICHT, f, indent=1, ensure_ascii=False, default=str)
    print(json.dumps(BERICHT, indent=1, ensure_ascii=False, default=str))


# ─────────────────────────────────────────────────────────────────────────────
#  9. Vorschaubilder (Eevee, nur Bilder; Stoffe mit der Textur der Bibliothek)
# ─────────────────────────────────────────────────────────────────────────────

_SCHICHTBILDER = {}


def vergleich_rendern(ordner):
    """Alle gebauten Völker nebeneinander in ihrer Spielgröße (Mensch, Ork, Oger), von vorn schräg und von der Seite, dazu die Köpfe."""
    sc = bpy.context.scene
    for o in sc.objects:
        if o.type == "MESH":
            o.hide_render = True
    reihe = []
    for k in VERGLEICH:
        if k["volk"] not in [r for r in reihe]:
            reihe.append(k["volk"])
    abstand = 0.95 if len(reihe) <= 3 else 0.9
    for k in VERGLEICH:
        i = reihe.index(k["volk"])
        k.location.x = (i - (len(reihe) - 1) / 2) * abstand
        k.hide_render = False
    cam = bpy.data.objects["Kamera"]
    sc.camera = cam
    hoehe = 1.0
    sc.render.resolution_x, sc.render.resolution_y = 1400, 1100
    for name, von, mitte, groesse in (("vergleich_voelker", Vector((1.6, -5.0, 1.2)), Vector((0.0, 0, 1.0)), 3.5 + 0.25 * max(len(reihe) - 3, 0)),
                                      ("vergleich_voelker_seite", Vector((5.0, -1.2, 0.4)), Vector((0, 0, 1.05)), 3.0),
                                      ("vergleich_koepfe", Vector((0.8, -5.0, 0.4)), Vector((0, 0, 1.55)), 2.9)):
        cam.data.ortho_scale = groesse
        cam.location = mitte + von
        cam.rotation_euler = (-von).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(ordner, name + ".png")
        bpy.ops.render.render(write_still=True)
    sc.render.resolution_x, sc.render.resolution_y = 900, 1000


def schichtbild(name):
    """Eine Schicht (Farbe) aus modelle_farbe_hoehe.webp (2 × 11 Schichten zu 1024 px) als eigenes Bild für die Vorschau."""
    if name in _SCHICHTBILDER:
        return _SCHICHTBILDER[name]
    pfad = os.path.join(REPO, "game", "assets", "texturen", "modelle", "modelle_farbe_hoehe.webp")
    gross = bpy.data.images.get("Bibliothek") or bpy.data.images.load(pfad)
    gross.name = "Bibliothek"
    import numpy as np
    w, h = gross.size
    px = np.empty(w * h * 4, dtype=np.float32)
    gross.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    i = BIB[name]
    seite_ = w // 2
    zeile, spalte = i // 2, i % 2
    # Blender zählt Zeilen von unten
    oben = h - (zeile + 1) * seite_
    ausschnitt = px[oben:oben + seite_, spalte * seite_:(spalte + 1) * seite_].copy()
    ausschnitt[..., 3] = 1.0
    bild = bpy.data.images.new("Schicht_" + name, seite_, seite_)
    bild.pixels.foreach_set(ausschnitt.ravel())
    with open(os.path.join(REPO, "game", "daten", "materialbibliothek.json"), encoding="utf-8") as f:
        sch = json.load(f)["schichten"][i]
    _SCHICHTBILDER[name] = (bild, sch["kachel"], sch["mittel"])
    return _SCHICHTBILDER[name]


def vorschau_material(name, schicht):
    """Eckenfarbe „Vorschau“ (sRGB) × Textur der Schicht ÷ ihre mittlere Farbe, kastenprojiziert in Objektkoordinaten."""
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    baum = m.node_tree
    p = baum.nodes.get("Principled BSDF")
    p.inputs["Roughness"].default_value = 0.85
    ecke = baum.nodes.new("ShaderNodeVertexColor")
    ecke.layer_name = "Vorschau"
    if schicht is None:
        baum.links.new(ecke.outputs["Color"], p.inputs["Base Color"])
        return m
    bild, kachel, mittel = schichtbild(schicht)
    koord = baum.nodes.new("ShaderNodeTexCoord")
    mal = baum.nodes.new("ShaderNodeMapping")
    mal.inputs["Scale"].default_value = (1 / kachel, 1 / kachel, 1 / kachel)
    tex = baum.nodes.new("ShaderNodeTexImage")
    tex.image = bild
    tex.projection = "BOX"
    tex.projection_blend = 0.3
    teil = baum.nodes.new("ShaderNodeMix")
    teil.data_type = "RGBA"
    teil.blend_type = "DIVIDE"
    teil.inputs["Factor"].default_value = 1.0
    teil.inputs[7].default_value = (mittel[0], mittel[1], mittel[2], 1.0)
    mult = baum.nodes.new("ShaderNodeMix")
    mult.data_type = "RGBA"
    mult.blend_type = "MULTIPLY"
    mult.inputs["Factor"].default_value = 1.0
    baum.links.new(koord.outputs["Object"], mal.inputs["Vector"])
    baum.links.new(mal.outputs["Vector"], tex.inputs["Vector"])
    baum.links.new(tex.outputs["Color"], teil.inputs[6])
    baum.links.new(ecke.outputs["Color"], mult.inputs[6])
    baum.links.new(teil.outputs[2], mult.inputs[7])
    baum.links.new(mult.outputs[2], p.inputs["Base Color"])
    return m


VORSCHAU_FUSS = {"offen": "halb", "wams": "barfuss", "sack": "barfuss", "cotte": "halb", "gugel": "halb", "rock": "halb", "haube": "halb", "gebende": "halb", "bettler": "barfuss"}


# Berufe in der Vorschau (Schritt 4): (Name, Variante, Fußform, Zubehör)
VORSCHAU_BERUFE = {
    "mann": [("tageloehner", "cotte", "holz", ()), ("knecht", "cotte", "holz", ("schuerze",)), ("hirte", "cotte", "holz", ("stroh", "stab")),
             ("feldarbeiter", "cotte", "holz", ("stroh",)), ("holzsammler", "cotte", "holz", ("buendel",)), ("kraemer", "cotte", "halb", ("kiepe",)),
             ("bote", "cotte", "halb", ("tasche",)), ("ratsherr", "rock", "halb", ("barett",)), ("buergersohn", "cotte", "halb", ("barett",)),
             ("moench", "kutte", "halb", ()), ("wache", "cotte", "halb", ("eisenhut",)), ("bettler", "bettler", "barfuss", ())],
    "frau": [("magd", "haube", "holz", ("schuerze",)), ("kraemerin", "haube", "halb", ("kiepe",)), ("feldarbeiterin", "haube", "holz", ("stroh",)),
             ("holzsammlerin", "haube", "holz", ("buendel",)), ("nonne", "gebende", "halb", ()), ("buergerin", "gebende", "halb", ()),
             ("bettlerin", "bettler", "barfuss", ())],
}


def vorschau(art, rig, netze, fuesse, last, werkzeug, aktionen, ordner, quellen, zubehoer=None):
    zubehoer = zubehoer or {}
    os.makedirs(ordner, exist_ok=True)
    sc = bpy.context.scene
    for motor in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = motor
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = 900, 1000
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    welt = sc.world or bpy.data.worlds.new("Welt")
    sc.world = welt
    try:
        welt.use_nodes = True
    except AttributeError:
        pass
    hg = welt.node_tree.nodes.get("Background")
    if hg:
        hg.inputs["Color"].default_value = (0.20, 0.20, 0.22, 1.0)
        hg.inputs["Strength"].default_value = 0.9
    if "Sonne" not in bpy.data.objects:
        licht = bpy.data.lights.new("Sonne", "SUN")
        licht.energy = 3.5
        sonne = neues_objekt("Sonne", licht)
        sonne.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
    if "Kamera" in bpy.data.objects:
        cam = bpy.data.objects["Kamera"]
        cam_d = cam.data
    else:
        cam_d = bpy.data.cameras.new("Kamera")
        cam = neues_objekt("Kamera", cam_d)
    cam_d.type = "ORTHO"
    sc.camera = cam

    def blick(ziel, groesse, von=Vector((2.6, -4.2, 2.4))):
        cam_d.ortho_scale = groesse
        cam.location = ziel + von
        cam.rotation_euler = (-von).to_track_quat("-Z", "Y").to_euler()

    # Vorschaufarben und Stoffe mit Textur: zweite Eckenfarbe, Material je Schicht
    stoffe = [vorschau_material("V_glatt", None), vorschau_material("V_tuch", "tuch"), vorschau_material("V_leder", "leder")]
    for obj in netze + list(fuesse.values()) + list(zubehoer.values()) + [last, werkzeug]:
        me = obj.data
        zone = me.uv_layers["Zone"].data
        basis = me.color_attributes["Farbe"].data
        v = me.color_attributes.new("Vorschau", "BYTE_COLOR", "CORNER")
        variante = obj.name.split("_")[-1]
        pal = dict(VORSCHAU.get(variante, VORSCHAU["cotte"]))
        if art in VORSCHAU_HAUT:
            pal[3] = VORSCHAU_HAUT[art]
        if "vorschau_haar" in FIGUREN[art]:
            pal[4] = FIGUREN[art]["vorschau_haar"]
        if variante == "offen":
            pal = {**VORSCHAU["haube"], 3: pal[3], 4: pal[4]}
        werte = []
        for li in range(len(me.loops)):
            z = int(round(zone[li].uv[0]))
            c = (tuple(0.3 * h + 0.7 * b for h, b in zip(pal[3], pal[4])) if z == 6 else pal.get(z)) if z else tuple(basis[li].color[:3])   # Vorschau: Bart an (wie im Spiel 70 % Haarfarbe)   # „Farbe“ hält die sRGB-Zahlen als Rohwerte (wie figuren.py, das Spiel liest sie als sRGB)
            werte += [c[0], c[1], c[2], 1.0]
        v.data.foreach_set("color_srgb", werte)   # Vorschaufarben sind sRGB wie die Farben im Spiel
        me.color_attributes.active_color = v
        me.materials.clear()
        for s_ in stoffe:
            me.materials.append(s_)
        for p in me.polygons:
            code = int(round(zone[p.loop_indices[0]].uv[1]))
            schicht = code // 16 - 1
            p.material_index = 1 if schicht == BIB["tuch"] else 2 if schicht == BIB["leder"] else 0
    posen = [("Stehen", 20), ("Gehen", 8), ("Tragen", 12), ("Reden", 30), ("Sitzen", 10), ("Hacken", 8), ("Buecken", 30)]
    if OPT.get("schnell"):
        posen = [("Stehen", 20), ("Gehen", 8)]
    alle = netze + list(fuesse.values()) + list(zubehoer.values()) + [last, werkzeug]
    for obj in netze:
        stufe, variante = obj.name.split("_")
        fuss = fuesse[VORSCHAU_FUSS.get(variante, "halb")]
        bart_ = [zubehoer[z] for z in FIGUREN[art].get("zeigen", ()) if z in zubehoer and variante in FIGUREN[art].get("zeigen_bei", ())]
        eigene = [("Gehen", 8), ("Betteln", 20), ("Kauern", 20)] if variante == "bettler" else posen
        for bewegung, bild in eigene:
            if stufe == "Fern" and bewegung != "Gehen":
                continue
            rig.animation_data.action = aktionen[bewegung][0]
            sc.frame_set(bild)
            for o in alle:
                o.hide_render = not (o is obj or o is fuss or o in bart_ or (o is last and bewegung == "Tragen") or (o is werkzeug and bewegung == "Hacken"))
            blick(Vector((0, 0, 0.9 if bewegung != "Sitzen" else 0.7)), 2.3)
            sc.render.filepath = os.path.join(ordner, f"{art}_{variante}_{stufe.lower()}_{bewegung.lower()}.png")
            bpy.ops.render.render(write_still=True)
        if stufe == "Nah":
            # Kopf und Hals nah (Ausschnitt, Gesicht, Kopfbedeckung) und Füße (Schuhe), stehend
            rig.animation_data.action = aktionen["Stehen"][0]
            sc.frame_set(20)
            for o in alle:
                o.hide_render = o is not obj and o is not fuss and o not in bart_
            kopf_z = rig.data.bones["Head"].head_local.z - 0.04
            blick(Vector((0, 0, kopf_z)), 0.55 * kopf_z / 1.55, Vector((1.4, -4.0, 0.9)))
            sc.render.filepath = os.path.join(ordner, f"{art}_{variante}_kopf.png")
            bpy.ops.render.render(write_still=True)
            blick(Vector((0, -0.05, 0.12)), 0.6, Vector((2.0, -3.4, 1.6)))
            sc.render.filepath = os.path.join(ordner, f"{art}_{variante}_schuhe.png")
            bpy.ops.render.render(write_still=True)
            # Hände nah (Vorlage 09.11.): rechte Hand von vorn-seitlich, stehend
            hand = rig.matrix_world @ rig.pose.bones["RightHand"].head
            blick(hand + Vector((0, 0, -0.06)), 0.32, Vector((-2.6, -3.6, 0.6)))
            sc.render.filepath = os.path.join(ordner, f"{art}_{variante}_haende.png")
            bpy.ops.render.render(write_still=True)
            # Haltung von der Seite (aufrecht oder vorgebeugt?): Stehen und Gehen
            for bewegung, bild in (("Stehen", 20), ("Gehen", 8)):
                rig.animation_data.action = aktionen[bewegung][0]
                sc.frame_set(bild)
                blick(Vector((0, 0, 0.9)), 2.3, Vector((5.0, 0.0, 0.3)))
                sc.render.filepath = os.path.join(ordner, f"{art}_{variante}_seite_{bewegung.lower()}.png")
                bpy.ops.render.render(write_still=True)
    # Reihe: alle Varianten nebeneinander im Gehen (nah)
    rig.animation_data.action = aktionen["Gehen"][0]
    sc.frame_set(8)
    for o in alle:
        o.hide_render = True
    kopien = []
    varianten = [o for o in netze if o.name.startswith("Nah_")]
    for i, quelle in enumerate(varianten):
        va_ = quelle.name.split("_")[-1]
        zeig_ = [zubehoer[z] for z in FIGUREN[art].get("zeigen", ()) if z in zubehoer and va_ in FIGUREN[art].get("zeigen_bei", ())]
        for teil in [quelle, fuesse[VORSCHAU_FUSS.get(va_, "halb")]] + zeig_:
            dg = bpy.context.evaluated_depsgraph_get()
            me = bpy.data.meshes.new_from_object(teil.evaluated_get(dg))
            k = neues_objekt(f"Reihe_{quelle.name}_{teil.name}", me)
            k.matrix_world = rig.matrix_world
            k.location.x += (i - (len(varianten) - 1) / 2) * 0.85
            me.color_attributes.active_color = me.color_attributes["Vorschau"]
            kopien.append(k)
    blick(Vector((0, 0, 0.9)), 3.4)
    sc.render.resolution_x, sc.render.resolution_y = 1400, 1000
    sc.render.filepath = os.path.join(ordner, f"{art}_reihe_gehen.png")
    bpy.ops.render.render(write_still=True)
    for k in kopien:
        bpy.data.objects.remove(k)
    sc.render.resolution_x, sc.render.resolution_y = 900, 1000
    # Berufe nebeneinander im Gehen (Schritt 4)
    if art in VORSCHAU_BERUFE:
        rig.animation_data.action = aktionen["Gehen"][0]
        sc.frame_set(8)
        for o in alle:
            o.hide_render = True
        kopien = []
        reihe = [b for b in VORSCHAU_BERUFE[art] if f"Nah_{b[1]}" in bpy.data.objects]
        for i, (beruf, variante, fuss, zub) in enumerate(reihe):
            for teil in [bpy.data.objects[f"Nah_{variante}"], fuesse[fuss]] + [zubehoer[z] for z in zub if z in zubehoer]:
                dg = bpy.context.evaluated_depsgraph_get()
                me = bpy.data.meshes.new_from_object(teil.evaluated_get(dg))
                k_ = neues_objekt(f"Beruf_{beruf}_{teil.name}", me)
                k_.matrix_world = rig.matrix_world
                k_.location.x += (i - (len(reihe) - 1) / 2) * 0.75
                me.color_attributes.active_color = me.color_attributes["Vorschau"]
                kopien.append(k_)
        breite = 0.75 * len(reihe) + 0.6
        sc.render.resolution_x, sc.render.resolution_y = 2000, 900
        blick(Vector((0, 0, 0.95)), breite, Vector((0.9, -5.0, 1.6)))
        sc.render.filepath = os.path.join(ordner, f"{art}_berufe.png")
        bpy.ops.render.render(write_still=True)
        for k_ in kopien:
            bpy.data.objects.remove(k_)
        sc.render.resolution_x, sc.render.resolution_y = 900, 1000
    # Völkervergleich: die erste Variante stehend in der Größe des Spiels, für das gemeinsame Bild am Ende
    if art in VERGLEICH_MASS:
        rig.animation_data.action = aktionen["Stehen"][0]
        sc.frame_set(20)
        quelle = varianten[0]
        zeig_ = [zubehoer[z] for z in FIGUREN[art].get("zeigen", ()) if z in zubehoer and quelle.name.split("_")[-1] in FIGUREN[art].get("zeigen_bei", ())]
        for teil in [quelle, fuesse[VORSCHAU_FUSS.get(quelle.name.split("_")[-1], "halb")]] + zeig_:
            dg = bpy.context.evaluated_depsgraph_get()
            me = bpy.data.meshes.new_from_object(teil.evaluated_get(dg))
            k = neues_objekt(f"Vergleich_{art}_{teil.name}", me)
            k.matrix_world = rig.matrix_world
            k.scale = Vector((1, 1, 1)) * VERGLEICH_MASS[art]
            k["volk"] = art
            me.color_attributes.active_color = me.color_attributes["Vorschau"]
            k.hide_render = True
            VERGLEICH.append(k)
    # Vergleich: die Quaternius-Puppe im selben Bild ihres Clips (nur einmal, beim Mann)
    if art == "mann" and OPT.get("quelle", "1") == "1":
        for bewegung, bild in posen:
            q, clip = BEWEGUNGEN[bewegung]
            qrig = quellen[q]
            netz = QUELLNETZE.get(qrig.name)
            if netz is None:
                continue
            aktion_setzen(qrig, clip_aktion(clip))
            sc.frame_set(bild - 1)
            netz.hide_render = False
            blick(Vector((0, 0, 0.9 if bewegung != "Sitzen" else 0.7)), 2.3)
            sc.render.filepath = os.path.join(ordner, f"quelle_{bewegung.lower()}.png")
            bpy.ops.render.render(write_still=True)
            netz.hide_render = True
    for o in alle:
        o.hide_render = True


if __name__ == "__main__":      # nicht beim Einbinden (tools/modelle/passanten_clips.py nimmt Funktionen von hier)
    hauptteil()
