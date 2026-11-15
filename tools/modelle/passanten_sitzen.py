"""Sitz-Clip der Passanten je Volk und Geschlecht: der Clip „Sitzen“ neu gebaut (Nutzerauftrag 14.11.2026, Figuren-Baukasten).

STAND 14.11.2026: angewandt auf die zehn Dateien (Nutzerentscheidung: Formschlüssel „Sitzen“, Vorrücken je Volk; Rückmeldung: Knie der
kleinen Völker geknickt, mehr Bewegung als 5-s-Schleife wie die Ruhe-Clips, BEWEGUNG). Rest siehe docs/bereiche/darstellung.md. --gewichte=1 verteilt zusätzlich Hüftgewicht des Stoffs auf die Oberschenkel
(probiert, verworfen), --auflage=stoff legt das Gesäß auf den Stoff statt auf den Körper (verworfen: der Körper schwebte).

Schnittstelle zur Finger-Erweiterung (Arbeitsbewegungen): Zielform „Sitzen“ (glTF morph target, extras.targetNames, mesh.weights = [0]) in
jedem Netz Nah_* und Zub_schuerze aller zehn passant_probe*.glb, keine Animationsspur. Ein späteres Skript, das die Netze neu skinnt, muss
die Zielformen erhalten (Blender: Shape Keys bleiben bei geänderten Vertexgruppen; beim Export morph targets mitschreiben) – sonst nach ihm
dieses Skript noch einmal laufen lassen (es baut aus der Datei, die es vorfindet; vorher die alten Zielformen entfernen).

    blender -b --factory-startup --python tools/modelle/passanten_sitzen.py -- \
        [--ordner=game/assets/modelle/figuren/probe] [--aus=<Ordner>] [--bericht=<datei.json>] [--nur=mann,frau,...]

Warum: Der alte Clip (Quaternius „Sitting_Idle_Loop“, übertragen) hatte das Hüftgelenk 21–30 cm hinter dem Ursprung, die Beine für eine
feste Sitzhöhe und den Rocksaum senkrecht hängend; im Spiel steckte der Saum 2–12 cm in der Bank, Frauen hatten die Füße 5–8 cm über dem
Boden, Zwerginnen und Halblinginnen drückten 1–3 cm in die Sitzfläche (tools/modelle/sitzpruefung.py).

Der neue Clip ist **selbst gebaut** (Formeln, keine fremde Animation):
  * Hüftgelenk über dem Ursprung (die Sitzregel des Spiels legt es über den Sitzpunkt, Stadtansicht.Baukasten.cs, SitzVersatz).
  * Becken nach hinten gekippt (BECKEN): der Stoff, der am Becken hängt, schwingt nach vorn auf den Schoß; der Rücken gleicht aus und bleibt
    aufrecht. Die Oberschenkel liegen fast waagrecht, die Knie vorn.
  * Gesäß (tiefste Ecke von Körper und Stoff über der Sitzfläche) genau auf der Sitzhöhe der Figur in ProbeArtDaten (Stadtansicht.
    Passantenprobe.cs, SITZHOEHE unten) – so stimmt die Höhenrechnung des Spiels ohne Änderung.
  * Füße: auf dem Boden bei der Sitzfläche des Spiels (0,44 m + 1 cm Luft, FigurenBaukasten.Sitzflaeche); kleine Völker erreichen ihn nicht und
    lassen die Füße leicht schräg nach vorn hängen.
  * Hände liegen auf den Oberschenkeln. Bewegung (Tabelle BEWEGUNG, 5 s Schleife wie die Ruhe-Clips): Atmen hebt Brust und Schultern, der
    Oberkörper verlagert das Gewicht langsam zur Seite, der Kopf neigt sich und der Blick wandert ruhig; kleine Völker knicken die Knie ab
    (Unterschenkel hängt von der Sitzkante leicht nach hinten) und lassen die Beine langsam pendeln. Jedes Bild wird aus der Ruhelage neu
    gesetzt (auch die Knochengröße), die Quaternionen bleiben stetig: die Schleife ist nahtlos.
Nur die Animation „Sitzen“ wird binär ersetzt; Netze, Skelett, Stoffe und alle übrigen Clips bleiben Byte für Byte (Vergleich mit
tools/modelle/passanten_idles_vergleich.py). „SitzenBock“ (passanten_clips.py) bleibt der alte, er wird aus dem alten „Sitzen“ abgeleitet.
Reihenfolge nach einem Neubau: passanten_probe.py → passanten_clips.py → passanten_netze_anpassen.py → passanten_idles.py → dieses Skript.
"""

import importlib.util
import json
import math
import os
import sys
import tempfile

import bpy
from mathutils import Matrix, Quaternion, Vector

HIER = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HIER, "..", ".."))

_spec = importlib.util.spec_from_file_location("passanten_idles", os.path.join(HIER, "passanten_idles.py"))
I = importlib.util.module_from_spec(_spec)
sys.modules["passanten_idles"] = I
_spec.loader.exec_module(I)
C = I.C
P = C.P
OPT = P.OPT
FPS = P.FPS
BERICHT = {}

# Sitzhöhe je Figur (Gesäß über den Fußsohlen in Modellmetern) wie ProbeArtDaten.Sitzhoehe im Spiel, und der Maßstab, mit dem das Spiel die
# Figur einsetzt (Mensch, Ork, Oger: Hüfte/0,92 × Völkergröße; Zwerge, Elben, Halblinge: Körpergröße ÷ Modellhöhe, Frauen 5 % kleiner)
SITZHOEHE = {"mann": 0.446, "frau": 0.389, "ork": 0.439, "oger": 0.449, "zwerg": 0.371, "zwergin": 0.307, "elb": 0.496, "elbin": 0.445,
             "halbling": 0.373, "halblingin": 0.313}
MASSSTAB = {"mann": 1.0, "frau": 1.0, "ork": 1.1, "oger": 1.35, "zwerg": 1.35 / 1.573, "zwergin": 1.35 * 0.95 / 1.479, "elb": 1.85 / 1.826,
            "elbin": 1.85 * 0.95 / 1.635, "halbling": 1.10 / 1.675, "halblingin": 1.10 * 0.95 / 1.532}
SITZ_SPIEL = 0.45          # Sitzfläche des Spiels (0,44 m) + 1 cm Luft, in Spielmetern
SITZ_TIEFE = (0.25, 0.30)  # Sitzfläche hinter und vor der Sitzmitte (Spielmeter; der tiefste Sitz, Kontorstuhl 0,6 m)
SCHENKEL_MAX = 20.0       # so weit (Grad) darf der Oberschenkel sinken, damit der Fuß den Boden erreicht; sonst hängen die Füße
MESS_VORN = 0.15         # Gesäßhöhe messen bis so weit vor dem Hüftgelenk (Spielmeter): Gesäß und hinterer Oberschenkel, nicht das Knie

# Haltung je Figur
#   becken   Becken nach hinten gekippt (Grad); der Rücken gleicht es aus
#   schenkel Oberschenkel nach unten geneigt (Grad, 0 = waagrecht)
#   lehnen   Oberkörper nach vorn (Grad)
#   spreiz   Knie seitlich auseinander (Meter je Seite, auf die Hüftbreite)
#   fuss_vor Knöchel vor dem Knie (Meter), wenn der Fuß den Boden erreicht
#   haengen  Fuß nach unten gekippt (Grad), wenn er den Boden nicht erreicht
BASIS = dict(becken=20.0, schenkel=4.0, lehnen=10.0, spreiz=0.0, fuss_vor=0.0, haengen=30.0)
SITZE = {art: dict(BASIS) for art in SITZHOEHE}
SITZE["oger"].update(spreiz=0.06, lehnen=10.0, schenkel=35.0, knie_fest=True)   # Oger: Knie tief (unter den Tischplatten der Menschen)
SITZE["ork"].update(spreiz=0.05, lehnen=10.0, schenkel=10.0)
DAUER = 5.0               # Länge der Schleife (Sekunden), wie die Ruhe-Clips (passanten_idles.py)
for _art in SITZE:
    SITZE[_art]["art"] = _art

# Bewegung im Sitzen je Figur (Amplituden im Maß der Ruhe-Clips, Nutzer 14.11.2026):
#   schulter_cm  Schultern heben sich beim Einatmen (cm, für 0,93 m Hüfthöhe)    nicken  Brust hebt/nickt beim Einatmen (Grad)
#   neigen       Oberkörper neigt sich langsam zur Seite (Grad, Gewicht verlagern) neigen_kopf  Kopf neigt sich (Grad)
#   drehen       Blick wandert nach links und rechts (Grad, ruhige Wechsel)
#   zurueck, pendeln  nur wenn die Füße hängen: Unterschenkel von der Senkrechten nach hinten (Grad) und Pendeln (± Grad, langsam)
BEWEGUNG = {
    "mann": dict(schulter_cm=1.5, nicken=4.5, neigen=1.5, neigen_kopf=2.5, drehen=12, zurueck=5, pendeln=7),
    "frau": dict(schulter_cm=1.2, nicken=4.0, neigen=1.5, neigen_kopf=2.5, drehen=10, zurueck=5, pendeln=7),
    "elb": dict(schulter_cm=1.0, nicken=3.5, neigen=1.0, neigen_kopf=2.0, drehen=8, zurueck=5, pendeln=6),
    "elbin": dict(schulter_cm=1.0, nicken=3.5, neigen=1.0, neigen_kopf=2.0, drehen=9, zurueck=5, pendeln=6),
    "zwerg": dict(schulter_cm=2.0, nicken=6.0, neigen=2.0, neigen_kopf=2.5, drehen=14, zurueck=6, pendeln=8),
    "zwergin": dict(schulter_cm=1.8, nicken=5.5, neigen=2.0, neigen_kopf=2.5, drehen=12, zurueck=6, pendeln=8),
    "halbling": dict(schulter_cm=1.5, nicken=5.0, neigen=2.0, neigen_kopf=3.0, drehen=16, zurueck=6, pendeln=7),
    "halblingin": dict(schulter_cm=1.5, nicken=5.0, neigen=2.0, neigen_kopf=3.0, drehen=15, zurueck=6, pendeln=7),
    "ork": dict(schulter_cm=2.0, nicken=6.0, neigen=1.5, neigen_kopf=2.0, drehen=12, zurueck=5, pendeln=6),
    "oger": dict(schulter_cm=2.5, nicken=7.5, neigen=1.5, neigen_kopf=2.0, drehen=10, zurueck=5, pendeln=6),
}


def sitz_ziel(art):
    """Gesäßhöhe des Clips (Modellmeter): die Sitzhöhe aus ProbeArtDaten; bei großen Figuren (Elb, Ork, Oger), deren Gesäß im Spiel höher läge als die
    Sitzfläche, so tief, dass sie bei Füßen auf dem Boden genau auf der Sitzfläche sitzen (das Spiel setzt den Ursprung dann auf den Boden,
    FigurenBaukasten.SitzUrsprung)."""
    return min(SITZHOEHE[art], SITZ_SPIEL / MASSSTAB[art])


def aktualisieren():
    bpy.context.view_layer.update()


# ─────────────────────────────────────────────────────────────────────────────
#  Gewichte: Stoff unter dem Becken folgt auch den Oberschenkeln
# ─────────────────────────────────────────────────────────────────────────────
# Rock, Cotte, Kleid und Schürze hängen im Passanten-Modell unterhalb des Beckens fast nur am Knochen „Hips“. Im Sitzen bleibt dieser Stoff
# darum senkrecht unter dem Becken hängen und steckt in der Bank – keine Haltung des Beckens hebt ihn weit genug (er hängt bis 25 cm unter
# dem Hüftgelenk). Darum geht ein Teil des Hüftgewichts auf die Oberschenkel über, umso mehr, je tiefer die Ecke unter dem Hüftgelenk liegt
# (STOFF_VON … STOFF_BIS), links und rechts nach der Seite (STOFF_BREITE): Im Sitzen liegt der Stoff dann auf dem Schoß; beim Gehen schwingt
# der Saum mit den Beinen (in der Mitte mit beiden zugleich). Lagen, Netze und alle Animationen bleiben, nur JOINTS_0/WEIGHTS_0 der Ecken
# ändern sich.
STOFF_NETZE = ("Nah_", "Fern_", "Zub_schuerze")
STOFF_VON, STOFF_BIS = 0.06, 0.22   # Tiefe unter dem Hüftgelenk (Meter, für 0,93 m Hüfthöhe), ab der Stoff zu wandern beginnt / ganz wandert
STOFF_ANTEIL = 0.85                  # höchstens so viel des Hüftgewichts geht auf die Oberschenkel
STOFF_BREITE = 0.16                  # Übergang links–rechts (Meter, für 0,93 m Hüfthöhe)


def _acc_lesen(j, b, idx):
    import numpy as np
    a = j["accessors"][idx]
    bv = j["bufferViews"][a["bufferView"]]
    typ = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[a["componentType"]]
    n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]]
    groesse = np.dtype(typ).itemsize * n
    schritt = bv.get("byteStride", groesse)
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    zeilen = [bytes(b[start + i * schritt:start + i * schritt + groesse]) for i in range(a["count"])]
    return np.frombuffer(b"".join(zeilen), dtype=typ).reshape(a["count"], n).copy(), a


def _acc_anhaengen(j, b, daten, vorlage):
    a = {k: v for k, v in vorlage.items() if k not in ("bufferView", "byteOffset", "min", "max")}
    roh = daten.tobytes()
    while len(b) % 4:
        b.append(0)
    j["bufferViews"].append({"buffer": 0, "byteOffset": len(b), "byteLength": len(roh)})
    b.extend(roh)
    a["bufferView"] = len(j["bufferViews"]) - 1
    j["accessors"].append(a)
    return len(j["accessors"]) - 1


def stoffgewichte(j, b):
    """Verteilt Hüftgewicht des Stoffs unter dem Becken auf die Oberschenkel (siehe oben). Gibt die Zahl der geänderten Ecken je Netz zurück."""
    import numpy as np
    erg = {}
    for nd in j["nodes"]:
        name = nd.get("name", "").split(".")[0]
        if "mesh" not in nd or "skin" not in nd or not name.startswith(STOFF_NETZE):
            continue
        skin = j["skins"][nd["skin"]]
        gelenke = [j["nodes"][k]["name"] for k in skin["joints"]]
        ibm, _ = _acc_lesen(j, b, skin["inverseBindMatrices"])
        ruhe = {gelenke[i]: np.linalg.inv(ibm[i].reshape(4, 4).T)[:3, 3] for i in range(len(gelenke))}
        hips, links, rechts = (gelenke.index(k) for k in ("Hips", "LeftUpperLeg", "RightUpperLeg"))
        hoehe = ruhe["Hips"][1]
        mass = hoehe / 0.93
        geaendert = 0
        for prim in j["meshes"][nd["mesh"]]["primitives"]:
            at = prim["attributes"]
            pos, _ = _acc_lesen(j, b, at["POSITION"])
            jo, ja = _acc_lesen(j, b, at["JOINTS_0"])
            ge, ga = _acc_lesen(j, b, at["WEIGHTS_0"])
            ge = ge.astype(np.float64)
            for i in range(len(pos)):
                tiefe = hoehe - pos[i, 1]
                t = (tiefe - STOFF_VON * mass) / ((STOFF_BIS - STOFF_VON) * mass)
                if t <= 0:
                    continue
                t = min(1.0, t)
                f = STOFF_ANTEIL * t * t * (3 - 2 * t)
                slot = [s for s in range(4) if jo[i, s] == hips and ge[i, s] > 0]
                if not slot:
                    continue
                s0 = slot[0]
                wandert = ge[i, s0] * f
                if wandert < 1e-4:
                    continue
                wl = min(1.0, max(0.0, 0.5 + pos[i, 0] / (STOFF_BREITE * mass)))
                ge[i, s0] -= wandert
                for gelenk, anteil in ((links, wl), (rechts, 1 - wl)):
                    if anteil <= 0:
                        continue
                    da = [s for s in range(4) if jo[i, s] == gelenk and (ge[i, s] > 0 or gelenk != 0)]
                    if da:
                        ge[i, da[0]] += wandert * anteil
                        continue
                    frei = min(range(4), key=lambda s: 9 if s == s0 or (jo[i, s] in (links, rechts) and ge[i, s] > 0) else ge[i, s])
                    if ge[i, frei] > 0:          # kleinstes übriges Gewicht wird verteilt auf die anderen
                        rest = ge[i, frei]
                        ge[i, frei] = 0
                        ge[i, s0] += rest
                    jo[i, frei] = gelenk
                    ge[i, frei] = wandert * anteil
                ge[i] /= ge[i].sum()
                geaendert += 1
            if geaendert:
                at["JOINTS_0"] = _acc_anhaengen(j, b, jo.astype(jo.dtype), ja)
                at["WEIGHTS_0"] = _acc_anhaengen(j, b, ge.astype(np.float32), ga)
        erg[nd.get("name")] = geaendert
    return erg


UNTERBEIN = ("LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot", "LeftToes", "RightToes")
_beinanteil = {}

# Knochenstrecken des Körpers mit Halbmesser (Meter für 0,93 m Hüfthöhe): was weiter weg liegt, ist abstehender Stoff (wie sitzpruefung.py)
KOERPER = [(s + a, s + b, 0.10) for s in ("Left", "Right") for a, b in (("UpperLeg", "LowerLeg"), ("LowerLeg", "Foot"))]
KOERPER += [("Hips", "Spine", 0.17), ("Spine", "Chest", 0.17)]


def koerper_ecken(rig, ecken, mass):
    """Nur die Ecken, die nahe an einer Knochenstrecke des Körpers liegen (Körper, anliegender Stoff), ohne abstehenden Stoff."""
    mb = rig.pose.bones
    strecken = [(mb[a].head.copy(), mb[b].head.copy(), r * mass) for a, b, r in KOERPER]
    erg = []
    for v in ecken:
        for pa, pb, r in strecken:
            ab = pb - pa
            t = max(0.0, min(1.0, (v - pa).dot(ab) / max(ab.length_squared, 1e-12)))
            if (v - (pa + ab * t)).length < r:
                erg.append(v)
                break
    return erg


def netz_ecken(netze, ohne_unterbein=False):
    """Alle Ecken der Nah- und Fußnetze (ohne Bettler: der Bettler sitzt nicht) im Weltraum, ausgewertet (Skelett angewandt).
    ohne_unterbein: nur Ecken, die zu weniger als der Hälfte an Unterschenkel und Fuß hängen (was auf der Sitzfläche liegen kann)."""
    dg = bpy.context.evaluated_depsgraph_get()
    erg = []
    for name, obj in netze.items():
        k = name.split(".")[0]
        if not (k.startswith("Nah_") or k.startswith("Fuss_")) or k == "Nah_bettler":
            continue
        if ohne_unterbein and name not in _beinanteil:
            gruppen = {g.index: g.name for g in obj.vertex_groups}
            _beinanteil[name] = [sum(g.weight for g in v.groups if gruppen.get(g.group) in UNTERBEIN) for v in obj.data.vertices]
        ev = obj.evaluated_get(dg)
        me = ev.to_mesh()
        mw = obj.matrix_world
        if ohne_unterbein:
            anteil = _beinanteil[name]
            erg.extend(mw @ v.co for i, v in enumerate(me.vertices) if i < len(anteil) and anteil[i] < 0.5)
        else:
            erg.extend(mw @ v.co for v in me.vertices)
        ev.to_mesh_clear()
    return erg


def haltung(rig, cfg, t, mass, ruhe, huefte_z, fuss_z):
    """Sitzhaltung zur Phase t (0..1): Hüftgelenk über dem Ursprung in der Höhe huefte_z; Füße auf fuss_z (oder hängend)."""
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
        pb.scale = Vector((1.0, 1.0, 1.0))      # auch die Größe: Rundungsfehler aus pb.matrix sammelten sich sonst von Bild zu Bild (Knie wanderte 6 mm)
    aktualisieren()
    mb = rig.pose.bones
    w = 2 * math.pi * t
    atem = 0.5 - 0.5 * math.cos(w)                         # ein ruhiger Atemzug je Schleife (5 s), 0 aus … 1 ein
    bw = BEWEGUNG[cfg["art"]]

    # Becken nach hinten kippen (unten nach vorn): um die Querachse +X, die Unterkante wandert nach −Y (vorn)
    I.welt_drehen(mb["Hips"], I.achse(1, 0, 0, -cfg["becken"]))
    aktualisieren()
    m = mb["Hips"].matrix.copy()
    m.translation = Vector((0.0, 0.0, huefte_z))
    mb["Hips"].matrix = m
    aktualisieren()
    # Rücken gleicht aus (aufrecht), leicht nach vorn; Atmen hebt die Brust
    for k, anteil in (("Spine", 0.5), ("Chest", 0.3), ("UpperChest", 0.2)):
        I.welt_drehen(mb[k], I.achse(1, 0, 0, (cfg["becken"] + cfg["lehnen"]) * anteil))
    # Gewicht leicht verlagern: der Oberkörper neigt sich langsam zur Seite und zurück (das Becken bleibt auf der Sitzfläche)
    I.welt_drehen(mb["Spine"], I.achse(0, 1, 0, bw["neigen"] * math.sin(w + 0.4)))
    I.welt_drehen(mb["Chest"], I.achse(0, 1, 0, -0.4 * bw["neigen"] * math.sin(w + 0.4)))
    # Atmen: die Brust hebt sich und nickt, die Schultern heben sich
    I.welt_drehen(mb["UpperChest"], I.achse(1, 0, 0, -bw["nicken"] * atem))
    for seite, sx in (("Left", 1.0), ("Right", -1.0)):
        aktualisieren()
        lang = max(0.05, (mb[f"{seite}UpperArm"].head - mb[f"{seite}Shoulder"].head).length)
        hub = math.degrees(math.atan(1.7 * bw["schulter_cm"] / 100.0 * mass / lang))   # 1,7: am Schultergelenk gemessen ergibt das den Hub
        I.welt_drehen(mb[f"{seite}Shoulder"], I.achse(0, 1, 0, sx * hub * atem))
    # Kopf: neigt sich leicht, der Blick wandert ruhig von einer Seite zur anderen (lange Halte, weicher Wechsel)
    blick = bw["drehen"] * math.tanh(1.5 * math.sin(w + 1.1)) / math.tanh(1.5)   # weich: gebackene Zwischenbilder bleiben genau
    neig = bw["neigen_kopf"] * math.sin(2 * w + 0.7)
    I.ausrichten_teilweise(mb["Neck"], I.achse(0, 0, 1, 0.35 * blick) @ ruhe["Neck_rot"], 0.8)
    I.ausrichten_teilweise(mb["Head"], I.achse(0, 0, 1, blick) @ I.achse(0, 1, 0, neig) @ ruhe["Head_rot"], 1.0)

    # Beine: Knie vorn (Oberschenkel fast waagrecht), Knöchel auf dem Boden vor dem Knie oder hängend
    hang = False
    for seite, sx in (("Left", 1.0), ("Right", -1.0)):
        aktualisieren()
        h = mb[f"{seite}UpperLeg"].head.copy()
        l1 = (mb[f"{seite}LowerLeg"].head - mb[f"{seite}UpperLeg"].head).length
        l2 = (mb[f"{seite}Foot"].head - mb[f"{seite}LowerLeg"].head).length
        s = math.radians(cfg["schenkel"])
        knie = h + Vector((sx * cfg["spreiz"] * mass, -l1 * math.cos(s), -l1 * math.sin(s)))
        knoechel_hoch = ruhe[f"{seite}Foot"].z - ruhe["boden"]          # Knöchel über der Sohle
        ziel_z = fuss_z + knoechel_hoch
        fall = knie.z - ziel_z                               # so tief muss der Unterschenkel vom Knie aus reichen
        if not cfg.get("knie_fest"):
            # Kniewinkel um 90°: den Oberschenkel so neigen, dass der Unterschenkel senkrecht bis zum Boden reicht (wenn das geht)
            sin90 = (h.z - ziel_z - l2 * 0.995) / l1
            if math.sin(math.radians(-15)) <= sin90 <= math.sin(math.radians(SCHENKEL_MAX)):
                s = math.asin(sin90)
                knie = h + Vector((sx * cfg["spreiz"] * mass, -l1 * math.cos(s), -l1 * math.sin(s)))
                fall = knie.z - ziel_z
        if fall > l2 * 0.98:
            # Oberschenkel etwas weiter senken (bis SCHENKEL_MAX), wenn der Fuß dann den Boden erreicht
            sin_s = (h.z - ziel_z - l2 * 0.98) / l1
            if sin_s <= math.sin(math.radians(SCHENKEL_MAX)):
                s = math.asin(max(sin_s, math.sin(s)))
                knie = h + Vector((sx * cfg["spreiz"] * mass, -l1 * math.cos(s), -l1 * math.sin(s)))
                fall = knie.z - ziel_z
        if fall <= l2 * 0.981:
            # Boden erreichbar: Knöchel auf dem Boden unter dem Knie (Kniewinkel um 90°; das Knie stellt die Zweigelenk-Rechnung)
            # der Knöchel liegt genau eine Unterschenkellänge vom geplanten Knie (sonst stellt die Zweigelenk-Rechnung das Knie hoch)
            vor = math.sqrt(max(0.0, (l2 * 0.995) ** 2 - fall * fall))
            knoechel = Vector((knie.x, knie.y - vor, ziel_z))
            fuss_dreh = I.achse(0, 0, 1, sx * 6)
        else:
            # Boden nicht erreichbar (kleine Völker): Das Knie knickt ab, der Unterschenkel hängt von der Sitzkante leicht nach hinten und
            # pendelt langsam (die Beine gegeneinander versetzt); der Fuß ist nach unten gekippt
            hang = True
            r = math.radians(-bw["zurueck"] + bw["pendeln"] * math.sin(w + (0.0 if sx > 0 else 1.7)))
            knoechel = knie + Vector((0, -l2 * math.sin(r), -l2 * math.cos(r)))
            fuss_dreh = I.achse(1, 0, 0, -cfg["haengen"]) @ I.achse(0, 0, 1, sx * 6)
        I.bein_setzen(rig, seite, knoechel, Vector((sx * 0.1, -1.0, 0.3)), fuss_dreh)

    # Arme: Hände auf den Oberschenkeln, etwas vor der Mitte
    for seite, sx in (("Left", 1.0), ("Right", -1.0)):
        aktualisieren()
        h = mb[f"{seite}UpperLeg"].head
        k = mb[f"{seite}LowerLeg"].head
        ziel = h.lerp(k, 0.45) + Vector((sx * 0.01 * mass, 0.0, 0.10 * mass))   # nah an der Hüfte: Unterarme vor keiner Tischkante
        C.arm_ziel_setzen(rig, seite, ziel, Vector((sx * 0.6, 0.8, 0.0)), Vector((0.0, 0.0, -1.0)))
    return hang


def sitz_bauen(rig, art, netze):
    cfg = SITZE[art]
    _beinanteil.clear()
    for spur in rig.animation_data.nla_tracks:
        spur.mute = True
    bilder = int(round(DAUER * FPS))
    ruhe = I.ruhe_lesen(rig)
    ruhe["boden"] = I.sohle_in_ruhe(netze)
    mass = (rig.data.bones["LeftUpperLeg"].head_local.z - ruhe["boden"]) / I.BEZUG_HUEFTE
    sitz = sitz_ziel(art)
    fuss_z = sitz - SITZ_SPIEL / MASSSTAB[art]             # Sohle unter dem Modellboden, damit sie im Spiel auf dem Boden steht
    hinten, vorn = (x / MASSSTAB[art] for x in SITZ_TIEFE)
    # Hüfthöhe so, dass die tiefste Ecke des Körpers (ohne abstehenden Stoff, ohne Unterschenkel) über der Sitzfläche auf der Sitzhöhe liegt
    huefte_z = sitz + 0.10 * mass
    for _ in range(8):
        haltung(rig, cfg, 0.0, mass, ruhe, huefte_z, fuss_z)
        aktualisieren()
        # gemessen wird unter Gesäß und hinterem Oberschenkel (bis MESS_VORN vor dem Hüftgelenk), nicht am Knie
        ecken = [v for v in netz_ecken(netze, True) if -min(vorn, MESS_VORN / MASSSTAB[art]) <= v.y <= hinten and abs(v.x) < 0.35]
        if OPT.get("auflage", "koerper") == "koerper":
            ecken = koerper_ecken(rig, ecken, mass)
        tiefste = min(v.z for v in ecken)
        huefte_z += 0.8 * (sitz - tiefste)
    posen, haengt = [], False
    for i in range(bilder):
        haengt = haltung(rig, cfg, i / bilder, mass, ruhe, huefte_z, fuss_z) or haengt
        posen.append({pb.name: (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in rig.pose.bones})
    posen.append({k: (q.copy(), l.copy()) for k, (q, l) in posen[0].items()})
    # Quaternionen stetig halten (q und −q sind dieselbe Drehung; ein Vorzeichenwechsel ließe die Zwischenbilder den langen Weg drehen –
    # an der Naht der Schleife sprangen sonst die Hände um 1 cm)
    for i in range(1, len(posen)):
        for k, (q, l) in posen[i].items():
            if q.dot(posen[i - 1][k][0]) < 0:
                posen[i][k] = (-q, l)
    # Kontrolle im ersten Bild: Gesäß, Füße
    haltung(rig, cfg, 0.0, mass, ruhe, huefte_z, fuss_z)
    aktualisieren()
    alle = netz_ecken(netze)
    sitzflaeche = [v for v in netz_ecken(netze, True) if -vorn <= v.y <= hinten and abs(v.x) < 0.35]
    ecken = koerper_ecken(rig, sitzflaeche, mass)
    stoff_tiefste = min(v.z for v in sitzflaeche)
    fuesse = [v for v in alle if v.y < -vorn]
    for spur in rig.animation_data.nla_tracks:
        spur.mute = False
    C.aktion_ersetzen(rig, "Sitzen")
    act, n = C.aktion_schreiben(rig, "Sitzen", posen)
    return act, n, {
        "bilder": n, "sekunden": round(n / FPS, 3), "massstab_modell": round(mass, 3), "hueftgelenk_hoehe_m": round(huefte_z, 4),
        "gesaess_m": round(min(v.z for v in ecken), 4), "sitzhoehe_soll_m": sitz, "stoff_tiefste_m": round(stoff_tiefste, 4),
        "sohle_tiefste_m": round(min(v.z for v in fuesse), 4) if fuesse else None, "sohle_soll_m": round(fuss_z, 4),
        "fuesse_haengen": haengt}


# ─────────────────────────────────────────────────────────────────────────────
#  Vorrücken und Formschlüssel (ohne Blender, aus der fertigen Datei: CPU-Skinning wie tools/modelle/sitzpruefung.py)
# ─────────────────────────────────────────────────────────────────────────────
# Vorrücken: Kleine Völker haben kurze Oberschenkel; mit dem Hüftgelenk über der Sitzmitte stieße ihr Schienbein an die Vorderkante tiefer Stühle.
# Sie sitzen darum so weit vor, dass die Rückseite des Unterschenkels unter der Sitzfläche (Brettdicke) VORDERKANTE + 1 cm vor der Sitzmitte
# liegt. Das Spiel rechnet es je Sitz aus der Vorderkante und dem Schienbein (Bericht „schienbein_spiel_m“; FigurenBaukasten.SitzSchienbein,
# VorrueckenFuer – nach einem Neubau dort und in sitzpruefung.py SCHIENBEIN nachtragen). VORDERKANTE gilt nur für den Bericht.
VORDERKANTE = 0.30        # tiefste Sitzfläche des Spiels: Kontorstuhl 0,6 m (Spielmeter, halbe Tiefe)
BRETT = 0.10              # unter der Sitzfläche so tief prüfen (Spielmeter): Sitzbrett und Zarge

# Formschlüssel „Sitzen“ (Blend Shape, eine Zielform je Netz): verschiebt im Bindungsraum genau die Ecken, die in der Sitzpose (Bild 0) über der
# Sitzfläche unter der Sitzhöhe liegen, so dass sie dort knapp über der Sitzfläche liegen (der Stoff hinter den Oberschenkeln, den das Gesäß
# „plattdrückt“). Unterschenkel und Füße bleiben, am vorderen und hinteren Rand der Sitzfläche läuft die Verschiebung aus (der Stoff fällt
# über die Kante). Gesetzt wird er im Spiel beim Clipwechsel (ProbeBewegungSetzen: 1 nur im Clip „Sitzen“, FigurenBaukasten.SitzSchluessel),
# nicht über eine Spur der Animation: Das Spiel entfernt die ungenutzten Kleidervarianten jeder Figur, Spuren auf sie gäben Warnungen.
FORM_NETZE = ("Nah_", "Zub_schuerze")
FORM_NAME = "Sitzen"
FORM_LUFT = 0.004         # so weit über der Sitzhöhe (Modellmeter)
FORM_RAND = 0.06          # Auslauf am Rand der Sitzfläche (Spielmeter)
# Rock folgt dem Knie (Nutzer 15.11.2026): Der Rock hängt in der Ruhelage bis zu den Knöcheln und ist an Oberschenkel, Hüfte (und Fuß) gebunden;
# im Sitzen dreht der Oberschenkel ihn waagrecht nach vorn – bis 30 cm über das Knie hinaus (tools/modelle/rock_pruefung.py). Die Zielform schiebt
# darum allen Stoff im Bereich der Beine, der vor der Kniefront läge, auf die Kniefront zurück: Er knickt am Knie und fällt senkrecht.
KNIE_HALB = 0.055         # halbe Kniedicke nach vorn (Modellmeter, wie rock_pruefung.py)
FALT_LUFT = 0.015         # so weit vor der Kniefront hängt der Stoff (Modellmeter)
FALT_BREIT = 0.20         # seitlich so weit um das Knie (Modellmeter)
FALT_HOCH = 0.12          # bis so hoch über dem Hüftgelenk (Stoff auf den Oberschenkeln liegt bei kleinen Völkern darüber)
SCHUERZE_LUFT = 0.012     # die Schürze faltet so weit vor dem Rock (sonst lägen beide in einer Ebene und der Stoff flimmert durch)
FALT_TIEF = 0.06          # unter der Kniehöhe: Stoff bis so weit hinter der Kniefront kommt auf die Front (die vordere Bahn hängt senkrecht)


def _glb_figur(pfad):
    if HIER not in sys.path:
        sys.path.insert(0, HIER)
    from sitzpruefung import Glb
    return Glb(pfad)


def _skin(g, lok, nd):
    """Je Ecke die gemischte Skin-Matrix (4×4) in der Pose `lok` und die Ecken des Bindungsraums, je Primitive."""
    import numpy as np
    welt = g.welt(lok)
    skin = g.j["skins"][nd["skin"]]
    inv = g.accessor(skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)
    gel = np.array([welt[k] @ inv[i] for i, k in enumerate(skin["joints"])])
    namen = [g.j["nodes"][k]["name"] for k in skin["joints"]]
    erg = []
    for prim in g.j["meshes"][nd["mesh"]]["primitives"]:
        pos = g.accessor(prim["attributes"]["POSITION"])
        jo = g.accessor(prim["attributes"]["JOINTS_0"]).astype(int)
        ge = g.accessor(prim["attributes"]["WEIGHTS_0"])
        mix = sum(ge[:, s, None, None] * gel[jo[:, s]] for s in range(4))
        unter = sum(ge[:, s] * np.isin(jo[:, s], [namen.index(n) for n in UNTERBEIN if n in namen]) for s in range(4))
        erg.append((pos, mix, unter))
    return erg


def vorruecken(pfad, art):
    """Vorrücken (Spielmeter) für diese Figur, aus der Sitzpose: 0 bei langen Oberschenkeln."""
    import numpy as np
    g = _glb_figur(pfad)
    m = MASSSTAB[art]
    hinterkante = []
    for zeit in [i * 0.25 for i in range(int(g.clipdauer("Sitzen") / 0.25) + 1)]:   # über die ganze Schleife (die Beine pendeln)
        lok = g.pose("Sitzen", zeit)
        huefte = g.welt(lok)[g.knoten("Hips")][:3, 3]
        hinterkante += _hinterkanten(g, lok, art, huefte)
    if not hinterkante:
        return None
    return round(min(hinterkante) * m, 3)                # Schienbein: Rückseite des Unterschenkels vor dem Hüftgelenk (Spielmeter)


def _hinterkanten(g, lok, art, huefte):
    import numpy as np
    m = MASSSTAB[art]
    hinterkante = []
    for nd in g.j["nodes"]:
        if "mesh" not in nd or "skin" not in nd or not nd.get("name", "").startswith(("Nah_", "Fuss_")) or nd["name"].startswith("Nah_bettler"):
            continue
        for pos, mix, unter in _skin(g, lok, nd):
            s = np.einsum("nij,nj->ni", mix, np.c_[pos, np.ones(len(pos))])[:, :3]
            band = (unter > 0.5) & (s[:, 1] < sitz_ziel(art)) & (s[:, 1] > sitz_ziel(art) - BRETT / m)
            if band.any():
                hinterkante.append((s[band, 2] - huefte[2]).min())
    return hinterkante


# Sitzende Clips mit eigener Zielform (Rock folgt dem Knie in jeder Sitzhaltung; FigurenBaukasten.SitzForm): „Sitzen“ hebt dazu den Stoff
# auf die Sitzfläche, die Arbeitsclips nur das Falten am Knie. Name der Zielform: „Sitzen“ bzw. „Sitzen_<Clip>“.
FORM_CLIPS = ("Sitzen", "Feinarbeit", "Trinken", "Schreiben", "Beten", "Zaehlen", "Putzen", "Rechnen", "Kutschieren")
ARM_KNOCHEN = ("Shoulder", "Arm", "Hand", "Thumb", "Index", "Middle", "Ring", "Little")


def form_name(clip):
    return FORM_NAME if clip == "Sitzen" else f"{FORM_NAME}_{clip}"


def _anteil(g, nd, prim, teile):
    """Je Ecke der Anteil der Gewichte an Knochen, deren Name eines der `teile` enthält."""
    import numpy as np
    skin = g.j["skins"][nd["skin"]]
    wahl = np.array([any(t in g.j["nodes"][k]["name"] for t in teile) for k in skin["joints"]])
    jo = g.accessor(prim["attributes"]["JOINTS_0"]).astype(int)
    ge = g.accessor(prim["attributes"]["WEIGHTS_0"])
    return sum(ge[:, s] * wahl[jo[:, s]] for s in range(4))


def _armanteil(g, nd, prim):
    """Je Ecke der Anteil der Gewichte an Schulter, Arm, Hand und Fingern (diese Ecken faltet die Zielform nie: Hände liegen oft vor dem Knie)."""
    import numpy as np
    skin = g.j["skins"][nd["skin"]]
    arm = np.array([any(t in g.j["nodes"][k]["name"] for t in ARM_KNOCHEN) for k in skin["joints"]])
    jo = g.accessor(prim["attributes"]["JOINTS_0"]).astype(int)
    ge = g.accessor(prim["attributes"]["WEIGHTS_0"])
    return sum(ge[:, s] * arm[jo[:, s]] for s in range(4))


def _form_ziele(g, nd, clip, art, vor_spiel):
    """Je Primitive die Verschiebung im Bindungsraum (Modellmeter) für die Zielform dieses Clips, aus seinem ersten Bild."""
    import numpy as np
    lok = g.pose(clip, 0.0)
    m = MASSSTAB[art]
    welt = g.welt(lok)
    huefte = welt[g.knoten("Hips")][:3, 3]
    knie_lr = [welt[g.knoten(k)][:3, 3] for k in ("LeftLowerLeg", "RightLowerLeg")]
    boden = min(welt[g.knoten(k)][1, 3] for k in ("LeftFoot", "RightFoot")) - 0.04   # kurz über der Sohle (Knöchel rund 6 cm hoch)
    mitte = huefte[2] - vor_spiel / m                    # Sitzmitte (vorn = +Z im glTF)
    hinten, vorn = (SITZ_TIEFE[0] + 0.30) / m, SITZ_TIEFE[1] / m    # hinten weiter: kleine Völker rücken bis 0,3 m vor
    rand = FORM_RAND / m
    vorn = min(vorn, min(k[2] for k in knie_lr) - mitte - rand - 0.02 / m)   # nur hinter den Knien heben
    linie = sitz_ziel(art) + FORM_LUFT
    erg = []
    for prim, (pos, mix, unter) in zip(g.j["meshes"][nd["mesh"]]["primitives"], _skin(g, lok, nd)):
        s = np.einsum("nij,nj->ni", mix, np.c_[pos, np.ones(len(pos))])[:, :3]
        ziel = s.copy()
        if clip == "Sitzen":
            # Anteil: 1 auf der Sitzfläche, am Rand linear auf 0
            z = s[:, 2] - mitte
            anteil = np.clip(np.minimum((z + hinten + rand) / rand, (vorn + rand - z) / rand), 0, 1)
            anteil *= (np.abs(s[:, 0]) < 0.35 / m)    # auch Stoff am Unterschenkel (Sack des Ogers), solange er hinter dem Knie liegt
            ziel[:, 1] += np.maximum(0.0, linie - s[:, 1]) * anteil
        # Rock folgt dem Knie: Stoff im Bereich der Beine (bis FALT_HOCH über der Hüfte), der vor der Kniefront läge, auf die Kniefront zurück;
        # unter der Kniehöhe die vordere Bahn (bis FALT_TIEF hinter der Front) senkrecht auf die Front – der Rock knickt am Knie und fällt
        frei = (_armanteil(g, nd, prim) < 0.5) & (unter < 0.5)   # weder Arm noch Unterschenkel (der Körper selbst bleibt, nur Stoff faltet)
        schien = _anteil(g, nd, prim, ("LowerLeg",)) > 0.5      # Schienbein (Beinling) des Netzes: der Stoff hängt davor, nie dahinter
        for kn in knie_lr:
            anderes = knie_lr[0][0] + knie_lr[1][0] - kn[0]
            naeher = np.abs(s[:, 0] - kn[0]) <= np.abs(s[:, 0] - anderes)
            bein = schien & naeher & (np.abs(s[:, 0] - kn[0]) < 0.12)
            # Faltlinie: über dem Knie die Kniefront, darunter die Kniefront oder – wo das Schienbein weiter vorn liegt – die Front des
            # Schienbeins auf dieser Höhe (der Stoff fällt senkrecht und legt sich über ein schräg nach vorn stehendes Schienbein)
            bp = s[bein]
            luft = FALT_LUFT + (SCHUERZE_LUFT if nd["name"].startswith("Zub_schuerze") else 0.0)   # die Schürze liegt vor dem Rock

            def grenze_bei(y):
                g_ = np.full(len(y), kn[2] + KNIE_HALB)
                for i in np.where(y < kn[1])[0]:
                    nah = np.abs(bp[:, 1] - y[i]) < 0.04
                    if nah.any():
                        g_[i] = max(g_[i], float(bp[nah, 2].max()))
                return g_ + luft

            grenze = grenze_bei(s[:, 1])
            bereich = frei & naeher & (np.abs(s[:, 0] - kn[0]) < FALT_BREIT) & (s[:, 1] < huefte[1] + FALT_HOCH)
            vor_knie = bereich & (ziel[:, 2] > grenze)
            bahn = bereich & (s[:, 1] < kn[1]) & (ziel[:, 2] > grenze - FALT_TIEF) & ~vor_knie
            # Was vor der Front liegt, klappt um die Kniekante nach unten (Abstand zur Front wird Höhe – der Stoff behält seine Länge und
            # seine Außenseite bleibt außen), höchstens bis kurz über den Boden; dort liegt er auf der Front der neuen Höhe. Die vordere
            # Bahn darunter liegt senkrecht auf der Front
            ueber = ziel[vor_knie, 2] - grenze[vor_knie]
            ziel[vor_knie, 1] = np.maximum(ziel[vor_knie, 1] - ueber, boden)
            ziel[vor_knie, 2] = grenze_bei(ziel[vor_knie, 1])
            ziel[bahn, 2] = grenze[bahn]
        d = np.zeros_like(pos)
        wer = np.linalg.norm(ziel - s, axis=1) > 1e-5
        for i in np.where(wer)[0]:
            d[i] = np.linalg.solve(mix[i][:3, :3], ziel[i] - s[i])
        erg.append((d, int(wer.sum()), float(np.linalg.norm(ziel - s, axis=1).max()) if len(s) else 0.0, ziel, s))
    return erg


def _normalen_delta(j, b, prim, d):
    """Normalen-Verschiebung der Zielform (Bindungsraum): Normalen der verschobenen Form minus die alten, nur an Ecken, deren Dreiecke sich
    bewegen. Ohne sie behielte der gefaltete Stoff die Normalen der waagrechten Lage und leuchtete hell (das „gelbe Dreieck“ im Schoß)."""
    import numpy as np
    if "NORMAL" not in prim["attributes"] or "indices" not in prim:
        return None
    pos = _acc_lesen(j, b, prim["attributes"]["POSITION"])[0].astype(np.float64)
    alt = _acc_lesen(j, b, prim["attributes"]["NORMAL"])[0].astype(np.float64)
    dr = _acc_lesen(j, b, prim["indices"])[0].reshape(-1, 3).astype(np.int64)
    bewegt = np.linalg.norm(d, axis=1) > 1e-6
    if not bewegt.any():
        return np.zeros_like(alt, dtype=np.float32)
    # Ecken an derselben Stelle (Nähte der UV) teilen ihre Normale
    schluessel = np.round(pos / 1e-5).astype(np.int64)
    _, gruppe = np.unique(schluessel, axis=0, return_inverse=True)
    gruppe = gruppe.reshape(-1)
    def normalen(p):
        f = np.cross(p[dr[:, 1]] - p[dr[:, 0]], p[dr[:, 2]] - p[dr[:, 0]])
        summe = np.zeros((gruppe.max() + 1, 3))
        for k in range(3):
            np.add.at(summe, gruppe[dr[:, k]], f)
        n = summe[gruppe]
        return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
    vorher, nachher = normalen(pos), normalen(pos + d)
    betroffen = np.zeros(len(pos), dtype=bool)
    beruehrt = bewegt[dr].any(axis=1)
    betroffen[dr[beruehrt].reshape(-1)] = True
    betroffen = np.isin(gruppe, np.unique(gruppe[betroffen]))   # auch die Zwillinge an den Nähten
    # Drehung der Normale wie die der Fläche: alte Normale um die Änderung der berechneten Normale gedreht
    delta = np.zeros_like(alt)
    delta[betroffen] = (alt[betroffen] + (nachher[betroffen] - vorher[betroffen]))
    delta[betroffen] /= np.maximum(np.linalg.norm(delta[betroffen], axis=1, keepdims=True), 1e-12)
    delta[betroffen] -= alt[betroffen]
    return delta.astype(np.float32)


def _kleid_flaeche(g, nd, ziele):
    """Kleid (Netz Nah_*) im Stehen (Bild 0, Zielformen 0) und in der Zielform eines Clips, mit den Dreiecken ohne Arme (alle Primitive zusammen)."""
    import numpy as np
    stehen = g.pose("Stehen", 0.0)
    rp, zp, dr, arm, ab = [], [], [], [], 0
    for prim, (pos, mix, unter), z in zip(g.j["meshes"][nd["mesh"]]["primitives"], _skin(g, stehen, nd), ziele):
        rp.append(np.einsum("nij,nj->ni", mix, np.c_[pos, np.ones(len(pos))])[:, :3])
        zp.append(z[3])
        dr.append(g.accessor(prim["indices"]).reshape(-1, 3).astype(np.int64) + ab)
        arm.append(_armanteil(g, nd, prim) > 0.3)
        ab += len(pos)
    rp, zp, dr, arm = np.concatenate(rp), np.concatenate(zp), np.concatenate(dr), np.concatenate(arm)
    return rp, zp, dr[~arm[dr].any(axis=1)]


def _schuerze_ziele(g, nd, kleid):
    """Die Schürze folgt dem Kleid: Jede Ecke sucht im Stehen den nächsten Punkt auf dem Kleid (Dreieck und baryzentrische Lage, ohne Arme) und
    ihren Abstand entlang der Normale; in der Zielform liegt sie am selben Punkt des verformten Kleides im selben Abstand (mindestens
    SCHUERZE_LUFT nach außen) – auf dem Schoß, mit dem Knick am Knie, nie im Kleid und nie davor in der Luft."""
    import numpy as np
    from sitzpruefung import naechste_dreiecke, dreieck_lage
    rp, zp, dr = kleid
    erg = []
    stehen = g.pose("Stehen", 0.0)
    for (pos, mix_r, _), (pos2, mix, _u) in zip(_skin(g, stehen, nd), _skin(g, g._form_lok, nd)):
        r = np.einsum("nij,nj->ni", mix_r, np.c_[pos, np.ones(len(pos))])[:, :3]
        s = np.einsum("nij,nj->ni", mix, np.c_[pos, np.ones(len(pos))])[:, :3]
        idx, bary = naechste_dreiecke(r, rp, dr)
        q_r, n_r = dreieck_lage(rp, dr, idx, bary)
        abstand = np.einsum("ij,ij->i", r - q_r, n_r)
        abstand = np.sign(abstand + 1e-12) * np.maximum(np.abs(abstand), SCHUERZE_LUFT)
        q_z, n_z = dreieck_lage(zp, dr, idx, bary)
        ziel = q_z + n_z * abstand[:, None]
        d = np.zeros_like(pos)
        for i in range(len(pos)):
            d[i] = np.linalg.solve(mix[i][:3, :3], ziel[i] - s[i])
        erg.append((d, len(pos), float(np.linalg.norm(ziel - s, axis=1).max()), ziel, s))
    return erg


def formschluessel(j, b, pfad, art, vor_spiel):
    """Legt in jedes Netz FORM_NETZE je sitzendem Clip eine Zielform („Sitzen“, „Sitzen_<Clip>“). Gibt je Netz und Clip die Zahl der
    verschobenen Ecken und die größte Verschiebung (Modellmeter) zurück."""
    import numpy as np
    g = _glb_figur(pfad)
    clips = [c for c in FORM_CLIPS if any(a["name"] == c for a in g.j.get("animations", []))]
    erg = {}
    knoten = [(i, nd) for i, nd in enumerate(j["nodes"]) if "mesh" in nd and "skin" in nd and nd.get("name", "").startswith(FORM_NETZE)
              and not nd["name"].startswith("Nah_bettler")]
    knoten.sort(key=lambda x: x[1]["name"].startswith("Zub_schuerze"))   # erst die Kleider, die Schürze legt sich darauf
    kleid = {}
    for knoten_idx, nd in knoten:
        mesh = j["meshes"][nd["mesh"]]
        if nd["name"].startswith("Zub_schuerze") and kleid:
            je_clip = {}
            for c in clips:
                g._form_lok = g.pose(c, 0.0)
                je_clip[c] = _schuerze_ziele(g, g.j["nodes"][knoten_idx], kleid[c])
        else:
            je_clip = {c: _form_ziele(g, g.j["nodes"][knoten_idx], c, art, vor_spiel) for c in clips}
            if nd["name"].startswith("Nah_") and (not kleid or nd["name"] == "Nah_haube"):
                kleid = {c: _kleid_flaeche(g, g.j["nodes"][knoten_idx], je_clip[c]) for c in clips}
        for p_idx, prim in enumerate(mesh["primitives"]):
            prim["targets"] = []
            for c in clips:
                daten = je_clip[c][p_idx][0].astype(np.float32)
                idx = _acc_anhaengen(j, b, daten, {"componentType": 5126, "count": len(daten), "type": "VEC3"})
                j["accessors"][idx]["min"] = [float(v) for v in daten.min(axis=0)]
                j["accessors"][idx]["max"] = [float(v) for v in daten.max(axis=0)]
                ziel = {"POSITION": idx}
                nd_delta = _normalen_delta(j, b, prim, je_clip[c][p_idx][0])
                if nd_delta is not None:
                    ziel["NORMAL"] = _acc_anhaengen(j, b, nd_delta, {"componentType": 5126, "count": len(nd_delta), "type": "VEC3"})
                prim["targets"].append(ziel)
        mesh["weights"] = [0.0] * len(clips)
        mesh.setdefault("extras", {})["targetNames"] = [form_name(c) for c in clips]
        erg[nd["name"]] = {c: {"ecken": sum(x[1] for x in je_clip[c]), "groesste_m": round(max(x[2] for x in je_clip[c]), 4)} for c in clips}
    return erg


def art_bearbeiten(art):
    cfg = P.FIGUREN[art]
    quelle_glb = os.path.join(OPT.get("ordner", os.path.join(REPO, "game", "assets", "modelle", "figuren", "probe")), cfg["datei"])
    aus_glb = os.path.join(OPT.get("aus", os.path.dirname(quelle_glb)), cfg["datei"])
    # 1. Gewichte des Stoffs (binär, ohne Blender); die Figur für den Clip wird aus dieser Fassung geladen
    j, b = C.glb_lesen(quelle_glb)
    gewichte = stoffgewichte(j, b) if OPT.get("gewichte", "0") == "1" else {}
    zwischen = os.path.join(tempfile.gettempdir(), f"rg_sitzen_gewichte_{art}.glb")
    C.glb_schreiben(zwischen, j, b)
    C.szene_leeren()
    rig, netze = C.figur_laden(zwischen)
    act, n, bericht = sitz_bauen(rig, art, netze)
    bericht["datei"] = quelle_glb
    bericht["stoffgewichte_ecken"] = gewichte
    for spur in list(rig.animation_data.nla_tracks):
        if spur.name != "Sitzen":
            rig.animation_data.nla_tracks.remove(spur)
    rig.animation_data.action = None
    for o in bpy.context.scene.objects:
        o.select_set(o is rig)
    hilfe = os.path.join(tempfile.gettempdir(), f"rg_sitzen_{art}.glb")
    bpy.ops.export_scene.gltf(
        filepath=hilfe, export_format="GLB", use_selection=True, export_yup=True, export_apply=False,
        export_animations=True, export_animation_mode="NLA_TRACKS", export_anim_slide_to_zero=True, export_morph=False,
        export_skins=True, export_lights=False, export_cameras=False, export_image_format="NONE")
    C.animationen_uebertragen(hilfe, j, b, ["Sitzen"])
    j, b = I.unbenutztes_entfernen(j, b)
    # 3. Vorrücken (kleine Völker) und Formschlüssel „Sitzen“ aus der fertigen Sitzpose
    zwischen2 = os.path.join(tempfile.gettempdir(), f"rg_sitzen_clip_{art}.glb")
    C.glb_schreiben(zwischen2, j, b)
    schienbein = vorruecken(zwischen2, art)
    bericht["schienbein_spiel_m"] = schienbein
    bericht["vorruecken_kontor_spiel_m"] = round(max(0.0, VORDERKANTE + 0.01 - schienbein), 3) if schienbein is not None else 0.0
    bericht["formschluessel"] = formschluessel(j, b, zwischen2, art, 0.0)
    os.makedirs(os.path.dirname(aus_glb), exist_ok=True)
    C.glb_schreiben(aus_glb, j, b)
    bericht["geschrieben"] = aus_glb
    bericht["groesse_b"] = os.path.getsize(aus_glb)
    return bericht


def hauptteil():
    arten = [a for a in P.FIGUREN if a in OPT.get("nur", ",".join(P.FIGUREN)).split(",")]
    for art in arten:
        BERICHT[art] = art_bearbeiten(art)
    if "bericht" in OPT:
        with open(OPT["bericht"], "w", encoding="utf-8") as f:
            json.dump(BERICHT, f, indent=1, ensure_ascii=False, default=str)
    print("BERICHT", json.dumps(BERICHT, indent=1, ensure_ascii=False, default=str))


if __name__ == "__main__":
    hauptteil()
