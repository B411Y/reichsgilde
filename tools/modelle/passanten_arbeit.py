"""Arbeitsbewegungen und Werkzeuge für das Passanten-Skelett (Nutzerauftrag 14.11.2026).

    blender -b --factory-startup --python tools/modelle/passanten_arbeit.py -- \
        [--ordner=game/assets/modelle/figuren/probe] [--aus=<Ordner>] [--alt=game/assets/modelle/figuren] \
        [--clips=Haemmern,Hobeln,...] [--nur=mann,frau,...] [--bilder=<Ordner>] [--bilder_clips=...] [--bericht=<datei.json>]

Das Passanten-Skelett (23 Knochen) bekommt die 35 Arbeitsbewegungen der alten Figuren (figur_<volk>.glb, 11 Knochen, gebaut von
tools/modelle/figuren.py) unter denselben Clipnamen, und die Werkzeuge der alten Figuren als eigene Netzknoten unter denselben Namen
(Hammer, Hobel, …). Nichts ist fremd: Die Bewegungen sind die alten, selbst gebauten Bewegungen, **übertragen** aufs neue Skelett.

Übertragung je Bild (die alte Figur wird im Ganzen auf die Hüfthöhe der Passantenfigur skaliert, Faktor k):
  * Becken (Hips) dreht wie die alte Wurzel; Lage = alte Lage × k.
  * Spine, Chest, UpperChest teilen sich die Drehung des alten Rückens gegenüber der Wurzel zu je einem Drittel; Neck und Head die des
    alten Kopfs gegenüber dem Rücken je zur Hälfte.
  * Beine: Zweigelenk-Rechnung auf den alten Knöchel (× k, seitlich unter das Hüftgelenk der Figur gerückt), Knie zur Seite des alten
    Knies; der Fuß bleibt flach, solange das alte Schienbein steht, sonst dreht er mit ihm (Knien).
  * Arme: Zweigelenk-Rechnung, die Faust (FAUST m vor dem Handgelenk) auf die Mitte der alten Hand, der Ellbogen weicht zur Seite des
    alten Ellbogens aus; die Hand dreht genau wie der alte Unterarm (gerades Handgelenk). Darum ziehen die Werkzeuge dieselbe Bahn wie
    an der alten Figur.
  * Schultern, Zehen: Ruhelage.

Werkzeuge: die Netze der alten Datei desselben Volks (dieselbe Form, Eckenfarbe und Zone), × k, steif am Passanten-Knochen
(Unterarm → Hand, Wurzel → Hips, Rücken → UpperChest). Die Hellebarde der Torwache hält die linke Hand im ersten Bild von „Stehen“.

Einbau binär wie passanten_clips.py/passanten_idles.py: Das Skript hängt die Clips und Werkzeugknoten an; die vorhandenen Clips, Netze,
Skelett und Stoffe bleiben Byte für Byte (tools/modelle/passanten_idles_vergleich.py). Reihenfolge nach einem Neubau:
passanten_probe.py → passanten_clips.py → passanten_idles.py → dieses Skript. Ein zweiter Lauf auf schon bearbeitete Dateien ersetzt
die Clips und Werkzeuge gleichen Namens.
"""

import importlib.util
import json
import math
import os
import struct
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
_spec = importlib.util.spec_from_file_location("figuren", os.path.join(HIER, "figuren.py"))
F = importlib.util.module_from_spec(_spec)
sys.modules["figuren"] = F
_spec.loader.exec_module(F)

FPS = P.FPS
OPT = P.OPT
BERICHT = {}

# Die 35 Arbeitsbewegungen (Namen wie in figuren.py BEWEGUNGEN und im Spiel)
ARBEIT = ("Haemmern", "Blasebalg", "Schleifen", "Feinarbeit", "Hobeln", "Saegen", "Drechseln", "Gatter", "Behauen", "Bildhauen",
          "Moersern", "Ruehren", "Destillieren", "Mischen", "Pressen", "Zapfen", "Trinken", "Laute", "Kerzenziehen", "Schoepfen",
          "Schreiben", "Predigen", "Beten", "Knien", "Knacken", "Zaehlen", "Schlafen", "Putzen", "Fechten", "Rechnen",
          "Wiegen", "Kutschieren", "Hueten", "Eilen", "Gebeugt")

# Werkzeuge (Namen wie in den alten Figuren und in Stadtansicht.Figuren.cs WerkstattWerkzeug, dazu die Hellebarde der Torwache)
WERKZEUGE = ("Hammer", "Hobel", "Saege", "Drechseleisen", "Schlaegel", "Meissel", "Stoessel", "Kelle", "Flakon", "Krug", "Laute",
             "Dochtstab", "Sieb", "Feder", "Dietrich", "Muenzbeutel", "Lappen", "Uebungsschwert", "Rechenpfennig", "Muenze", "Hellebarde")
# Für die Bilder: welches Werkzeug zu welchem Clip gehört (wie WerkstattWerkzeug im Spiel)
WERKZEUG_JE_CLIP = {"Haemmern": ("Hammer",), "Hobeln": ("Hobel",), "Saegen": ("Saege",), "Drechseln": ("Drechseleisen",),
                    "Behauen": ("Schlaegel", "Meissel"), "Bildhauen": ("Schlaegel", "Meissel"), "Moersern": ("Stoessel",),
                    "Ruehren": ("Kelle",), "Mischen": ("Flakon",), "Zapfen": ("Krug",), "Trinken": ("Krug",), "Laute": ("Laute",),
                    "Kerzenziehen": ("Dochtstab",), "Schoepfen": ("Sieb",), "Schreiben": ("Feder",), "Knacken": ("Dietrich",),
                    "Zaehlen": ("Muenzbeutel",), "Putzen": ("Lappen",), "Fechten": ("Uebungsschwert",), "Rechnen": ("Rechenpfennig",),
                    "Wiegen": ("Muenze",), "Hueten": ("Zub_stab",)}

# Passantenfigur → alte Figur desselben Volks (Ork und Oger waren im Spiel ein skalierter Mensch, Frauen eine Frau im Menschen-Rig)
ALT_VOLK = {"mann": "mensch", "frau": "mensch", "ork": "mensch", "oger": "mensch", "zwerg": "zwerg", "zwergin": "zwerg",
            "elb": "elb", "elbin": "elb", "halbling": "halbling", "halblingin": "halbling"}

# Abstand Handgelenk → Mitte der Faust entlang der Hand (wie werkzeug_bauen in passanten_probe.py)
FAUST = 0.08
# Fuß flach lassen, solange das alte Schienbein weniger als so viel Grad aus der Senkrechten kippt
FUSS_FLACH_GRAD = 35.0
# Armmaß (Reichweite Schulter → Faust der Figur / der alten, skalierten Figur) unter dieser Grenze: Die Faust zielt nicht auf die alte
# Lage im Raum, sondern von der eigenen Schulter in derselben Richtung, im Abstand × Armmaß (Zwerge und Halblinge, Armmaß um 0,7:
# die alte Lage wäre nicht erreichbar). Menschen (0,94), Elben, Orks und Oger zielen auf die alte Lage.
ARMMASS_GRENZE = 0.85
# Griffposen der Finger (Nutzer 14.11.2026, „Finger C“: in der Stufe Nah greifen die Finger das Werkzeug). Winkel in Grad, um die die
# Glieder über die gekrümmte Ruhelage hinaus zur Handfläche beugen: (Grundglied, Mittelglied) je Finger, Daumen (Mittelhand, Glied).
GRIFFARTEN = {
    "faust": {"Index": (60, 70), "Middle": (62, 72), "Ring": (64, 74), "Little": (66, 76), "Thumb": (25, 40)},    # um einen Stiel
    "halb": {"Index": (35, 40), "Middle": (38, 42), "Ring": (40, 45), "Little": (42, 48), "Thumb": (15, 20)},     # Henkel, Rand, Hebelende
    "spitz": {"Index": (35, 40), "Middle": (50, 55), "Ring": (62, 70), "Little": (64, 72), "Thumb": (30, 30)},    # Feder, Münze, Dietrich
    "gestreckt": {"Index": (-12, -10), "Middle": (-14, -12), "Ring": (-16, -14), "Little": (-18, -16), "Thumb": (-6, -6)},  # Beten
}
# Anteil der Winkel je Griffart, gemessen gegen Durchdringung (tools/modelle/passanten_finger_pruefen.py): die vollen Winkel lassen die
# Finger in die Handfläche laufen; Figuren mit dicken oder kurzen Fingern (Zwerge, Halblinge, Orks, Oger) beugen weniger
GRIFF_ANTEIL = {"faust": 0.7, "halb": 1.0, "spitz": 0.85, "gestreckt": 1.0}
GRIFF_ANTEIL_DICK = {"faust": 0.55, "halb": 0.55, "spitz": 0.55, "gestreckt": 1.0}
DICKE_FINGER = ("zwerg", "zwergin", "ork", "oger", "halbling", "halblingin")
# Griff je Clip und Hand (R/L); fehlt eine Hand, bleibt sie in der Ruhelage (locker gekrümmt)
GRIFFE = {
    "Haemmern": {"R": "faust", "L": "halb"}, "Blasebalg": {"R": "faust", "L": "faust"}, "Schleifen": {"R": "halb", "L": "halb"},
    "Feinarbeit": {"R": "spitz", "L": "spitz"}, "Hobeln": {"R": "faust", "L": "halb"}, "Saegen": {"R": "faust", "L": "halb"},
    "Drechseln": {"R": "faust", "L": "faust"}, "Gatter": {"R": "faust", "L": "faust"}, "Behauen": {"R": "faust", "L": "faust"},
    "Bildhauen": {"R": "faust", "L": "faust"}, "Moersern": {"R": "faust", "L": "halb"}, "Ruehren": {"R": "faust", "L": "faust"},
    "Destillieren": {"R": "halb", "L": "halb"}, "Mischen": {"R": "halb", "L": "halb"}, "Pressen": {"R": "faust", "L": "faust"},
    "Zapfen": {"R": "halb", "L": "faust"}, "Trinken": {"R": "halb", "L": "halb"}, "Laute": {"R": "spitz", "L": "halb"},
    "Kerzenziehen": {"R": "faust"}, "Schoepfen": {"R": "faust", "L": "faust"}, "Schreiben": {"R": "spitz"},
    "Beten": {"R": "gestreckt", "L": "gestreckt"}, "Knacken": {"R": "spitz", "L": "halb"}, "Zaehlen": {"R": "spitz", "L": "halb"},
    "Putzen": {"R": "faust", "L": "halb"}, "Fechten": {"R": "faust"}, "Rechnen": {"R": "spitz", "L": "halb"},
    "Wiegen": {"R": "spitz", "L": "halb"}, "Kutschieren": {"R": "faust", "L": "faust"}, "Hueten": {},
}
# Halte-Punkt in der Handfläche: Der Griff des Werkzeugs liegt so weit vor dem Handgelenk und so weit zur Handfläche hin (Meter, Dateimaß
# eines Mannes; mit der Handlänge der Figur umgerechnet), dort, wo die gebeugten Finger den Stiel umschließen
GRIFF_TIEFE = 0.022
# Clips mit dem Stecken der Passanten (Zub_stab, an der rechten Hand): Der Stecken steht senkrecht (Teamleiter 14.11.2026, Befund
# Hueten). Die rechte Hand dreht dazu um den Griffpunkt so wenig wie möglich, bis die Achse des Steckens senkrecht steht; die Finger
# bleiben in der Ruhehand, für die Zub_stab gebaut ist.
STAB_CLIPS = ("Hueten",)
STAB_GRAD_MAX = 3.0               # Prüfung: größter Winkel des Steckens zur Senkrechten in den Schlüsselbildern
# Abstand der Schlüsselbilder (wie die alten Figuren)
SCHRITT = 2


def welt(rig, pb):
    return rig.matrix_world @ pb.matrix


def rot(m):
    return m.to_3x3().to_quaternion()


# ─────────────────────────────────────────────────────────────────────────────
#  1. Laden: Passantenfigur und alte Figur in einer Szene
# ─────────────────────────────────────────────────────────────────────────────


def alt_laden(volk):
    pfad = os.path.join(OPT.get("alt", os.path.join(REPO, "game", "assets", "modelle", "figuren")), f"figur_{volk}.glb")
    vorher_a = set(bpy.data.actions)
    rig, netze = C.figur_laden(pfad)
    for a in bpy.data.actions:
        if a not in vorher_a:
            a.name = "ALT_" + a.name.split(".")[0]
    for o in list(netze.values()) + [rig]:
        o.name = "ALT_" + o.name.split(".")[0]
    for spur in rig.animation_data.nla_tracks:
        spur.mute = True
    return rig, {o.name[4:]: o for o in netze.values()}


class Alt:
    """Die alte Figur, auf die Hüfthöhe der Passantenfigur skaliert (Faktor k): Weltlagen ihrer Knochen je Bild."""

    def __init__(self, rig, volk, k):
        self.rig, self.k, self.p = rig, k, F.VOELKER[volk]
        bpy.context.view_layer.update()
        self.ruhe = {b.name: rig.matrix_world @ b.matrix_local for b in rig.data.bones}
        p = self.p
        self.knoechel_ruhe = {s: Vector((self.ruhe[f"Unterschenkel_{s}"].translation.x, 0.0, 0.06)) for s in "LR"}
        self.hand_ruhe = {s: self.ruhe[f"Unterarm_{s}"].translation - Vector((0, 0, p["unterarm"] * 0.72 + 0.06)) for s in "LR"}

    def bild(self, act, f):
        P.aktion_setzen(self.rig, act)
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        self.pose = {pb.name: welt(self.rig, pb) for pb in self.rig.pose.bones}

    def delta(self, b):
        """Weltbewegung des Knochens gegenüber der Ruhelage (unskaliert)."""
        return self.pose[b] @ self.ruhe[b].inverted()

    def punkt(self, b, p_ruhe):
        """Ein Punkt, der in der Ruhelage bei p_ruhe am Knochen b sitzt, in der Haltung – skaliert mit k."""
        return (self.delta(b) @ p_ruhe) * self.k


# ─────────────────────────────────────────────────────────────────────────────
#  2. Übertragung je Bild
# ─────────────────────────────────────────────────────────────────────────────


class Ziel:
    """Ruhemaße der Passantenfigur und die festen Bezüge zwischen alter Hand (Unterarm) und neuer Hand."""

    def __init__(self, rig, alt):
        self.rig = rig
        bpy.context.view_layer.update()
        b = rig.data.bones
        mw = rig.matrix_world
        self.ruhe = {x.name: mw @ x.matrix_local for x in b}
        self.k = alt.k
        # seitlicher Abstand: Knöchel unter das Hüftgelenk der Figur
        self.bein_x = {"L": (mw @ b["LeftUpperLeg"].head_local).x, "R": (mw @ b["RightUpperLeg"].head_local).x}
        # Sohle der Ruhelage (steht nicht überall auf dem Boden: Elben 7 cm zu tief, Zwerge und Halblinge 11–13 cm zu hoch, siehe
        # passanten_idles.sohle_in_ruhe): Hüfte und Knöchel werden auf den Boden z = 0 bezogen
        self.boden = alt.boden
        self.knoechel_z = (mw @ b["LeftFoot"].head_local).z - self.boden
        # Hand: gewünschte Weltlage der neuen Hand, solange die alte Figur in Ruhe steht (Faust auf der alten Handmitte, Hand entlang
        # des alten Unterarms, Handfläche wie in der Ruhelage zum Körper hin); im Bild: alte Unterarmbewegung @ D0
        self.D0 = {}
        self.oberarm = (b["LeftLowerArm"].head_local - b["LeftUpperArm"].head_local).length     # Dateimaß
        self.reichweite = self.oberarm + (b["LeftHand"].head_local - b["LeftLowerArm"].head_local).length + FAUST
        self.standort = None     # (vor, hoch, Maßstab im Spiel) bei der Arbeit am Möbel kleiner Völker, siehe haltung_uebertragen
        # Armmaß: Reichweite Schulter → Faust der Figur gegen die der alten (skalierten) Figur
        p = alt.p
        arm_alt = (p["oberarm"] + p["unterarm"] * 0.72 + 0.06) * alt.k
        arm_neu = ((b["RightLowerArm"].head_local - b["RightUpperArm"].head_local).length
                   + (b["RightHand"].head_local - b["RightLowerArm"].head_local).length + FAUST)
        self.armmass = arm_neu / arm_alt
        self.arm_relativ = self.armmass < ARMMASS_GRENZE
        self.griff = {}
        self.beuge = {}
        self.griffpunkt = {}
        self.dicke_finger = False
        self.stab_achse = None      # Achse des Steckens im Raum der rechten Hand (Ruhelage), siehe art_bearbeiten
        self.clip = ""
        handlaenge = {}
        for s, S in (("L", "Left"), ("R", "Right")):
            h0 = self.ruhe[f"{S}Hand"].to_3x3()
            hk = self.ruhe[f"{S}Hand"].translation
            mitte = self.ruhe.get(f"{S}MiddleProximal")
            handlaenge[s] = (mitte.translation - hk).length if mitte else FAUST
            flaeche = (h0 @ Vector((0, 0, 1))).normalized()          # Handfläche (Knochenachse +Z, in der Ruhelage zum Körper hin)
            ruhe_hand = self.ruhe[f"{S}Hand"].translation
            ruhe_mitte = self.ruhe[f"{S}MiddleIntermediate"].translation
            for f in ("Index", "Middle", "Ring", "Little", "Thumb"):
                for g, glied in enumerate(("Metacarpal", "Proximal") if f == "Thumb" else ("Proximal", "Intermediate")):
                    name = f"{S}{f}{glied}"
                    if name not in self.ruhe:
                        continue
                    r0 = self.ruhe[name].to_3x3()
                    richtung = (r0 @ Vector((0, 1, 0))).normalized()
                    # vier Finger um EINE Achse der Hand (sonst laufen die gespreizten Finger beim Beugen ineinander), der Daumen um seine
                    bezug = richtung if f == "Thumb" else (ruhe_mitte - ruhe_hand).normalized()
                    achse = bezug.cross(flaeche).normalized()
                    # Vorzeichen so, dass ein positiver Winkel die Fingerspitze zur Handfläche dreht
                    if (Matrix.Rotation(0.5, 3, achse) @ bezug).dot(flaeche) < bezug.dot(flaeche):
                        achse = -achse
                    self.beuge[name] = ((r0.inverted() @ achse).normalized(), f, g)
            y = (h0 @ Vector((0, 1, 0))).normalized()
            q = y.rotation_difference(Vector((0, 0, -1))).to_matrix()
            r = q @ h0
            faust = alt.hand_ruhe[s] * alt.k
            d0 = r.to_4x4()
            # Halte-Punkt in der Handfläche: die alte Handmitte (dort sitzt der Griff des Werkzeugs) liegt GRIFF_TIEFE vor der Fläche
            tiefe = GRIFF_TIEFE * handlaenge[s] / 0.10
            self.griffpunkt[s] = Vector((0, FAUST, tiefe))       # Griffpunkt im Raum des Handknochens
            d0.translation = faust - r @ self.griffpunkt[s]
            self.D0[s] = d0

    def hand_soll(self, alt, s):
        """Gewünschte Weltlage der neuen Hand (Kopf = Handgelenk) im aktuellen Bild der alten Figur."""
        d = alt.delta(f"Unterarm_{s}")
        m = d.to_3x3().to_4x4()
        # Bewegung mit Skalierung k um den Ursprung: Lage k·(R·(p/k) + t) = R·p + k·t
        m.translation = d.translation * self.k
        return m @ self.D0[s]


def haltung_uebertragen(rig, ziel, alt):
    """Setzt die Passantenfigur auf das aktuelle Bild der alten Figur; gibt die Fehler (Meter) von Faust und Knöchel zurück."""
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
        pb.scale = Vector((1, 1, 1))
    bpy.context.view_layer.update()
    pbs = rig.pose.bones
    k = ziel.k
    dw, dr, dk = (alt.delta(b) for b in ("Wurzel", "Ruecken", "Kopf"))
    qw, qr, qk = rot(dw), rot(dr), rot(dk)
    # Becken
    h0 = ziel.ruhe["Hips"]
    w_ruhe = alt.ruhe["Wurzel"].translation
    w_jetzt = alt.pose["Wurzel"].translation
    m = (qw.to_matrix() @ h0.to_3x3()).to_4x4()
    m.translation = h0.translation - Vector((0, 0, ziel.boden)) + (w_jetzt - w_ruhe) * k
    pbs["Hips"].matrix = rig.matrix_world.inverted() @ m
    bpy.context.view_layer.update()
    # Rücken in drei Teilen, Kopf in zwei
    e = qr @ qw.inverted()
    for name in ("Spine", "Chest", "UpperChest"):
        I.welt_drehen(pbs[name], Quaternion().slerp(e, 1 / 3).to_matrix())
    e2 = qk @ qr.inverted()
    for name in ("Neck", "Head"):
        I.welt_drehen(pbs[name], Quaternion().slerp(e2, 0.5).to_matrix())
    fehler = {}
    # Beine
    for s, S in (("L", "Left"), ("R", "Right")):
        du = alt.delta(f"Unterschenkel_{s}")
        kn_ruhe = alt.knoechel_ruhe[s]
        kn = alt.punkt(f"Unterschenkel_{s}", kn_ruhe)
        knoechel = Vector((ziel.bein_x[s] + kn.x - kn_ruhe.x * k, kn.y, kn.z - kn_ruhe.z * k + ziel.knoechel_z))
        knie_alt = alt.punkt(f"Unterschenkel_{s}", alt.ruhe[f"Unterschenkel_{s}"].translation)
        huefte_alt = alt.punkt(f"Oberschenkel_{s}", alt.ruhe[f"Oberschenkel_{s}"].translation)
        pol = knie_alt - (huefte_alt + kn) * 0.5
        if pol.length < 1e-3:
            pol = qw @ Vector((0, -1, 0))
        schien = (du.to_3x3() @ Vector((0, 0, -1))).normalized()
        kipp = math.degrees(math.acos(max(-1.0, min(1.0, -schien.z))))
        if kipp < FUSS_FLACH_GRAD:
            vor = du.to_3x3() @ Vector((0, -1, 0))
            gier = math.atan2(-vor.x, -vor.y) if Vector((vor.x, vor.y)).length > 1e-4 else 0.0
            fuss = Matrix.Rotation(gier, 3, "Z")
        else:
            fuss = du.to_3x3()
        huefte_neu = rig.matrix_world @ pbs[f"{S}UpperLeg"].head
        pol = pol_sicher(pol, huefte_neu, knoechel, qw @ Vector((0, -1, 0)))
        f, _ = I.bein_setzen(rig, S, rig.matrix_world.inverted() @ knoechel, pol, fuss)
        fehler[f"knoechel_{s}"] = f
    # Arme
    for s, S in (("L", "Left"), ("R", "Right")):
        bpy.context.view_layer.update()
        schulter_w = rig.matrix_world @ pbs[f"{S}UpperArm"].head
        soll = ziel.hand_soll(alt, s)
        if s == "R" and ziel.clip in STAB_CLIPS and ziel.stab_achse is not None:
            dreh = soll.to_3x3()
            achse = dreh @ ziel.stab_achse
            oben = Vector((0, 0, 1 if achse.z >= 0 else -1))
            neu = achse.rotation_difference(oben).to_matrix() @ dreh
            griff = soll @ ziel.griffpunkt[s]
            soll = neu.to_4x4()
            soll.translation = griff - neu @ ziel.griffpunkt[s]
        if ziel.standort is not None:
            # Arbeit am Möbel, kleines Volk (Nutzer 14.11.2026): Die Figur steht im Spiel um „vor“ näher am Möbel und um „hoch“ auf dem
            # Tritt; die Faust zielt wie bei den Menschen auf die alte Handmitte am Möbel (Spielmaß), gerechnet in Dateimaß
            vor, hoch, seit, mass = ziel.standort
            alt_welt = alt.punkt(f"Unterarm_{s}", alt.hand_ruhe[s]) / ziel.k
            ziel_datei = (alt_welt - Vector((seit, -vor, hoch))) / mass
            soll.translation = ziel_datei - soll.to_3x3() @ ziel.griffpunkt[s]
        elif ziel.arm_relativ:
            # kurzer Arm: die Faust liegt von der eigenen Schulter aus in derselben Richtung wie die alte, im Abstand × Armmaß
            bpy.context.view_layer.update()
            sch = rig.matrix_world @ pbs[f"{S}UpperArm"].head
            sch_a = alt.punkt(f"Oberarm_{s}", alt.ruhe[f"Oberarm_{s}"].translation)
            faust = soll @ ziel.griffpunkt[s]
            soll.translation += sch + (faust - sch_a) * ziel.armmass - faust
        ell_alt = alt.punkt(f"Unterarm_{s}", alt.ruhe[f"Unterarm_{s}"].translation)
        sch_alt = alt.punkt(f"Oberarm_{s}", alt.ruhe[f"Oberarm_{s}"].translation)
        faust_alt = alt.punkt(f"Unterarm_{s}", alt.hand_ruhe[s])
        pol = ell_alt - (sch_alt + faust_alt) * 0.5
        if pol.length < 0.01:
            pol = Vector(((1.0 if s == "L" else -1.0), 0.2, -0.6))
        bpy.context.view_layer.update()
        schulter_neu = rig.matrix_world @ pbs[f"{S}UpperArm"].head
        pol = pol_sicher(pol, schulter_neu, soll.translation, Vector(((1.0 if s == "L" else -1.0), 0.2, -0.6)))
        C.arm_ziel_setzen(rig, S, soll.translation, pol)
        hb = pbs[f"{S}Hand"]
        bpy.context.view_layer.update()
        m = soll.to_3x3().to_4x4()
        m.translation = (rig.matrix_world @ hb.matrix).translation
        hb.matrix = rig.matrix_world.inverted() @ m
        bpy.context.view_layer.update()
        fehler[f"hand_{s}"] = ((rig.matrix_world @ hb.matrix).translation - soll.translation).length
        if s == "R" and ziel.clip in STAB_CLIPS and ziel.stab_achse is not None:
            a = ((rig.matrix_world @ hb.matrix).to_3x3() @ ziel.stab_achse).normalized()
            fehler["stab_grad"] = math.degrees(math.acos(min(1.0, abs(a.z))))
        griffart = ziel.griff.get(s, "")
        winkel = GRIFFARTEN.get(griffart)
        if winkel:
            anteil = (GRIFF_ANTEIL_DICK if ziel.dicke_finger else GRIFF_ANTEIL)[griffart]
            for name, (achse, f, g) in ziel.beuge.items():
                if name.startswith(S):
                    pbs[name].rotation_quaternion = Quaternion(achse, math.radians(winkel[f][g] * anteil))
            bpy.context.view_layer.update()
        faust_neu = (rig.matrix_world @ hb.matrix) @ ziel.griffpunkt[s]
        fehler[f"faust_zu_alt_{s}"] = (faust_neu - faust_alt).length
        VERSATZ.append((faust_alt.copy(), faust_neu.copy(), schulter_w.copy()))
        if ziel.standort is not None:
            vor, hoch, seit, mass = ziel.standort
            REST.append(faust_alt / ziel.k - (faust_neu * mass + Vector((seit, -vor, hoch))))
            # Ergonomie: Hand gegen Schulter (Meter im Spiel, + = Hand über der Schulter)
            ERGO.append((faust_neu.z - schulter_w.z) * mass)
    return fehler


def pol_sicher(pol, von, nach, ersatz):
    """Pol für die Zweigelenk-Rechnung: muss quer zur Linie von→nach stehen, sonst (fast parallel) der Ersatz."""
    rz = (nach - von)
    if rz.length < 1e-6:
        return ersatz
    rz.normalize()
    for kandidat in (pol, ersatz, Vector((0, -1, 0)), Vector((1, 0, 0))):
        quer = kandidat - rz * kandidat.dot(rz)
        if kandidat.length > 1e-6 and quer.length > 0.05 * kandidat.length:
            return kandidat
    return ersatz


ERGO = []         # je Bild und Hand: Höhe der Faust über der Schulter im Spiel (Meter) nach Hocker und Standort
# Bezug Mensch (Nutzer 14.11.2026, Ergonomie): je Arbeitsclip die mittlere Höhe der Faust gegenüber der Schulter des Mannes an derselben
# Werkbank, in Oberarmlängen (meist zwischen Ellbogen- und Brusthöhe); die kleinen Völker sollen am Möbel ebenso stehen
REFERENZ = {}
REST = []         # je Bild und Hand: Abstand Faust – alte Handmitte im Spiel nach Tritt und Standort (Meter)
VERSATZ = []      # je Bild und Hand: (alte Handmitte, neue Faust) im Dateimaß, für die Tritthöhe der kleinen Völker
# Maßstab im Spiel (Stadtansicht.Passantenprobe: Körpergröße / Höhe der Datei; Zwerg 1,35 m, Halbling 1,10 m, Frauen × 0,95): die alte
# Figur stand unskaliert, ihre Handmitte (Datei / k) ist die Lage am Möbel; die neue Faust steht im Spiel bei Datei × Maßstab
SPIEL_MASS = {"zwerg": 1.35 / 1.573, "zwergin": 1.35 * 0.95 / 1.479, "halbling": 1.10 / 1.675, "halblingin": 1.10 * 0.95 / 1.532}
# Stehende Arbeit an Möbeln (dort steht ein kleines Volk auf dem Tritt); sitzende Clips regelt die Sitzhöhe
AN_MOEBELN = ("Haemmern", "Blasebalg", "Schleifen", "Hobeln", "Saegen", "Drechseln", "Gatter", "Behauen", "Bildhauen", "Moersern",
              "Ruehren", "Destillieren", "Mischen", "Pressen", "Zapfen", "Kerzenziehen", "Schoepfen", "Knacken", "Wiegen")


def tritt_rechnen(paare_je_clip, k, mass):
    """Tritt und Standort der kleinen Völker im Spiel (Meter; Nutzer 14.11.2026: je Volk und Geschlecht der Hocker, den es braucht, und
    so nah an das Möbel, wie nötig). Versatz je Bild und Hand = alte Handmitte am Möbel − neue Faust (die alte Figur stand unskaliert,
    die neue steht im Maß der Passanten). Je Clip der stehenden Arbeit: Standort vor = Mittel des Versatzes nach vorn (die Figur rückt
    so weit näher), Höhe je Clip = Mittel nach oben. Tritt je Figur: eine Höhe für alle Clips (Mittel über alle, nicht unter 0: tiefer
    stellen geht nicht). Rest = Abstand Faust – alte Handmitte nach Tritt und Standort (Mittel und Höchstwert je Clip); seitlich wird
    nicht verschoben."""
    versatz_je_clip = {c: [a / k - n * mass for a, n, *_ in liste] for c, liste in paare_je_clip.items() if c in AN_MOEBELN}
    if not versatz_je_clip:
        return None
    alle = [v for liste in versatz_je_clip.values() for v in liste]
    hoehe = max(0.0, sum(v.z for v in alle) / len(alle))
    clips = {}
    for c, liste in versatz_je_clip.items():
        m = sum(liste, Vector()) / len(liste)
        vor = -m.y                       # die Figur blickt nach −Y: „vor“ ist −Y
        rest = [(v - Vector((0, m.y, hoehe))).length for v in liste]
        rest_eigen = [(v - Vector((0, m.y, max(0.0, m.z)))).length for v in liste]
        clips[c] = {"vor_m": round(vor, 4), "hoch_noetig_m": round(m.z, 4), "seitlich_m": round(m.x, 4),
                    "rest_mittel_m": round(sum(rest) / len(rest), 4), "rest_max_m": round(max(rest), 4),
                    "rest_max_m_hoehe_je_clip": round(max(rest_eigen), 4)}
    return {"tritt_hoehe_m": round(hoehe, 4), "clips": clips,
            "rest_mittel_m": round(sum(c["rest_mittel_m"] for c in clips.values()) / len(clips), 4),
            "rest_max_m": round(max(c["rest_max_m"] for c in clips.values()), 4)}


def clip_uebertragen(rig, ziel, alt, name):
    act = bpy.data.actions[f"ALT_{name}"]
    a, e = int(act.frame_range[0]), int(act.frame_range[1])
    bilder = e - a
    ziel.griff = GRIFFE.get(name, {})
    ziel.clip = name
    # Schlüssel wie in der alten Figur jedes zweite Bild (figuren.py bewegungen_anlegen), dazwischen linear; das letzte gleicht dem ersten
    posen, fehler = [], {}
    VERSATZ.clear()
    for f in list(range(0, bilder, SCHRITT)):
        alt.bild(act, a + f)
        try:
            fe = haltung_uebertragen(rig, ziel, alt)
        except ValueError as fehler_text:
            raise RuntimeError(f"{name} Bild {f}: {fehler_text}")
        for kk, v in fe.items():
            fehler[kk] = max(fehler.get(kk, 0.0), v)
        posen.append((f, {pb.name: (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in rig.pose.bones}))
    posen.append((bilder, posen[0][1]))
    C.aktion_ersetzen(rig, name)
    neu = aktion_schreiben(rig, name, posen)
    if fehler.get("stab_grad", 0.0) > STAB_GRAD_MAX:
        raise RuntimeError(f"{name}: Stecken {fehler['stab_grad']:.1f}° aus der Senkrechten (erlaubt {STAB_GRAD_MAX}°)")
    return neu, {"bilder": bilder, "sekunden": round(bilder / FPS, 4), "schluessel": len(posen), "versatz": list(VERSATZ),
                 "fehler_max_m": {kk: round(v, 4) for kk, v in fehler.items()}}


def aktion_schreiben(rig, name, posen):
    """Wie passanten_clips.aktion_schreiben, aber mit Bildnummern: posen = [(Bild ab 0, Haltung)]; lineare Schlüssel."""
    neu = bpy.data.actions.new(name)
    rig.animation_data_create()
    rig.animation_data.action = neu
    P.aktion_setzen(rig, neu)
    for f, haltung in posen:
        for pb in rig.pose.bones:
            q, loc = haltung[pb.name]
            pb.rotation_quaternion = q
            pb.location = loc if pb.name == "Hips" else Vector()
            pb.keyframe_insert("rotation_quaternion", frame=f + 1)
            if pb.name == "Hips":
                pb.keyframe_insert("location", frame=f + 1)
    for schicht in neu.layers:
        for streifen in schicht.strips:
            for bag in streifen.channelbags:
                for fc in bag.fcurves:
                    for kp in fc.keyframe_points:
                        kp.interpolation = "LINEAR"
    rig.animation_data.action = None
    spur = rig.animation_data.nla_tracks.new()
    spur.name = name
    spur.strips.new(name, 1, neu)
    return neu


# ─────────────────────────────────────────────────────────────────────────────
#  3. Werkzeuge
# ─────────────────────────────────────────────────────────────────────────────

KNOCHEN_NEU = {"Unterarm_L": "LeftHand", "Unterarm_R": "RightHand", "Wurzel": "Hips", "Ruecken": "UpperChest"}


# Tritt (Nutzer 14.11.2026): kleiner Holzhocker unter den Füßen, nur wo die Figur höher stehen muss; Maße im Spiel (Meter)
REICH_ANTEIL = 0.97              # so weit darf der Arm zum Ziel gestreckt sein (nicht ganz durchgedrückt)
STANDORT_RUNDEN = 3               # so oft werden Standort und Höhe je Clip um den mittleren Rest nachgeführt
# Hocker je Möbel (Nutzer 14.11.2026: ein Hocker ist ein fester Gegenstand; höchstens eine Höhe je Arbeitsplatz/Möbel): Die
# Destille, der Steinblock des Steinmetzen und der Hebel des Blasebalgs stehen in den Innenräumen anders hoch als die Werkbänke
# und der Amboss; alle übrigen stehenden Arbeitsclips teilen sich den Hocker „Werkbank“.
MOEBEL = {"Destillieren": "Destille", "Behauen": "Steinblock", "Bildhauen": "Steinblock", "Blasebalg": "Blasebalg"}
TRITT_MIN_HOEHE = 0.03            # darunter kein Tritt (ein Brett von 1–2 cm sähe man nicht)
TRITT_FLAECHE = (0.46, 0.34)      # Breite (quer) und Tiefe der Trittfläche
TRITT_BRETT = 0.03                # Dicke des Trittbretts


def tritt_bauen(rig, hoehe, mass, alt_netze, name="Tritt"):
    """Hocker als Netz `name` („Tritt_<Möbel>“) am Knochen Root: Oberkante bei den Fußsohlen (z = 0), Unterkante bei −Höhe (die Figur steht im Spiel um
    die Höhe erhöht). Brett mit gefasten Kanten auf vier Beinen mit Querleisten; Farbe und Zone wie das Holz der alten Werkzeuge
    (Eckenfarbe und UV einer Holzfläche des alten Hobels)."""
    import bmesh
    h, b, t, d = hoehe / mass, TRITT_FLAECHE[0] / mass, TRITT_FLAECHE[1] / mass, TRITT_BRETT / mass
    bm = bmesh.new()

    def quader(mitte, groesse, fase=0.0):
        erg = bmesh.ops.create_cube(bm, size=1.0)
        for v in erg["verts"]:
            v.co = Vector((mitte[0] + v.co.x * groesse[0], mitte[1] + v.co.y * groesse[1], mitte[2] + v.co.z * groesse[2]))
        if fase > 0:
            kanten = list({e for f in erg["verts"] for e in f.link_edges})
            bmesh.ops.bevel(bm, geom=kanten, offset=fase, segments=1, affect="EDGES")

    quader((0, 0, -d / 2), (b, t, d), fase=d * 0.25)
    bein = 0.04 / mass
    for sx in (-1, 1):
        for sy in (-1, 1):
            quader((sx * (b / 2 - bein), sy * (t / 2 - bein), -d - (h - d) / 2), (bein, bein, h - d))
        quader((sx * (b / 2 - bein), 0, -h * 0.55), (bein * 0.8, t - 2 * bein, bein * 0.8))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    # Holz der alten Werkzeuge: Eckenfarbe und Zone/Art der häufigsten Farbe des Hobels
    hobel = alt_netze["Hobel"].data
    farben = hobel.color_attributes.active_color or hobel.color_attributes[0]
    uv = hobel.uv_layers[0]
    zaehl = {}
    for li in range(len(hobel.loops)):
        c = tuple(round(x, 3) for x in farben.data[li].color)
        zaehl.setdefault(c, []).append(tuple(uv.data[li].uv))
    farbe, uvs = max(zaehl.items(), key=lambda x: len(x[1]))
    fa = me.color_attributes.new(farben.name, farben.data_type, "CORNER")
    me.color_attributes.active_color = fa
    neu_uv = me.uv_layers.new(name=uv.name)
    for li in range(len(me.loops)):
        fa.data[li].color = farbe
        neu_uv.data[li].uv = uvs[0]
    ob.vertex_groups.new(name="Root").add(list(range(len(me.vertices))), 1.0, "REPLACE")
    ob.parent = rig
    ob.modifiers.new("Skelett", "ARMATURE").object = rig
    me.materials.append(alt_netze["Hobel"].data.materials[0])
    return ob, sum(len(pl.vertices) - 2 for pl in me.polygons)


def werkzeug_bauen(rig, ziel, alt, alt_netze, name, stehen):
    """Kopiert das alte Werkzeugnetz `name` an die Passantenfigur: jede Ecke steif an ihren neuen Knochen, so gerechnet, dass sie
    in jeder Haltung dort liegt, wo sie an der alten (skalierten) Figur lag."""
    quelle = alt_netze[name]
    me = quelle.data.copy()
    me.name = name
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    k = ziel.k
    gruppen = {g.index: g.name for g in quelle.vertex_groups}
    neu_gruppen = {}
    rig_inv = rig.matrix_world.inverted()
    # Hellebarde: Lage der linken Hand im ersten Bild von „Stehen“ der Passantenfigur statt des alten Stehens
    hand_stehen = None
    if name == "Hellebarde":
        P.aktion_setzen(rig, stehen)
        bpy.context.scene.frame_set(int(stehen.frame_range[0]))
        bpy.context.view_layer.update()
        hand_stehen = welt(rig, rig.pose.bones["LeftHand"])
        alt.bild(bpy.data.actions["ALT_Stehen"], int(bpy.data.actions["ALT_Stehen"].frame_range[0]))
        rig.animation_data.action = None
        bpy.context.scene.frame_set(1)
    for v in me.vertices:
        g = max(v.groups, key=lambda x: x.weight)
        alt_name = gruppen[g.group]
        neu = KNOCHEN_NEU[alt_name]
        p = quelle.matrix_world @ v.co * k
        if alt_name.startswith("Unterarm"):
            s = alt_name[-1]
            if hand_stehen is not None:
                # Weltlage an der alten Figur im ersten Bild von Stehen (senkrecht), um den Versatz der Fäuste verschoben, dann an die
                # neue Hand im ersten Bild von „Stehen“ der Passantenfigur gehängt
                soll = ziel.hand_soll(alt, s)
                versatz = hand_stehen @ ziel.griffpunkt[s] - soll @ ziel.griffpunkt[s]
                p_welt = soll @ (ziel.D0[s].inverted() @ p) + versatz
                p = ziel.ruhe[neu] @ (hand_stehen.inverted() @ p_welt)
            else:
                p = ziel.ruhe[neu] @ (ziel.D0[s].inverted() @ p)
        elif alt_name == "Wurzel":
            p = p + (ziel.ruhe["Hips"].translation - alt.ruhe["Wurzel"].translation * k)
        v.co = rig_inv @ p
        neu_gruppen.setdefault(neu, []).append(v.index)
    ob.vertex_groups.clear()
    for knochen, ecken in neu_gruppen.items():
        ob.vertex_groups.new(name=knochen).add(ecken, 1.0, "REPLACE")
    ob.parent = rig
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.modifiers.new("Skelett", "ARMATURE").object = rig
    me.materials.clear()
    me.materials.append(quelle.data.materials[0])
    return ob, sum(len(pl.vertices) - 2 for pl in me.polygons)


# ─────────────────────────────────────────────────────────────────────────────
#  4. glTF: Clips und Werkzeugknoten anhängen
# ─────────────────────────────────────────────────────────────────────────────


def werkzeuge_uebertragen(quelle, ziel_j, ziel_b, namen):
    """Kopiert die Netzknoten `namen` (mit Haut) aus der Hilfsdatei `quelle` in (ziel_j, ziel_b): Accessoren binär, JOINTS_0 auf die
    Knochenfolge der Ziel-Haut umgerechnet, Material = das eine Material „Figur“ des Ziels; Knoten als Kind des Skelett-Knotens wie
    die übrigen Netze. Ein gleichnamiger Knoten im Ziel bekommt das neue Netz."""
    qj, qb = C.glb_lesen(quelle)
    q_gelenke = [qj["nodes"][i]["name"] for i in qj["skins"][0]["joints"]]
    z_gelenke = [ziel_j["nodes"][i]["name"] for i in ziel_j["skins"][0]["joints"]]
    umrechnen = bytes(z_gelenke.index(n) for n in q_gelenke)
    figur = next(i for i, m in enumerate(ziel_j["materials"]) if m["name"] == "Figur")
    skelett = next(i for i, n in enumerate(ziel_j["nodes"]) if n.get("name") == "Skelett")
    komp = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}
    groesse = {5126: 4, 5125: 4, 5123: 2, 5121: 1}

    def acc(idx, gelenke=False):
        a = qj["accessors"][idx]
        bv = qj["bufferViews"][a["bufferView"]]
        n = a["count"] * komp[a["type"]] * groesse[a["componentType"]]
        assert "byteStride" not in bv or bv["byteStride"] == komp[a["type"]] * groesse[a["componentType"]]
        start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        roh = bytes(qb[start:start + n])
        if gelenke:
            assert a["componentType"] == 5121
            roh = roh.translate(umrechnen + bytes(range(len(umrechnen), 256)))
        while len(ziel_b) % 4:
            ziel_b.append(0)
        v = {"buffer": 0, "byteOffset": len(ziel_b), "byteLength": len(roh)}
        if "target" in bv:
            v["target"] = bv["target"]
        ziel_j["bufferViews"].append(v)
        ziel_b.extend(roh)
        na = {k: a[k] for k in ("componentType", "count", "type", "min", "max", "normalized") if k in a}
        na["bufferView"] = len(ziel_j["bufferViews"]) - 1
        ziel_j["accessors"].append(na)
        return len(ziel_j["accessors"]) - 1

    erg = {}
    for name in namen:
        qn = next(n for n in qj["nodes"] if n.get("name") == name and "mesh" in n)
        qm = qj["meshes"][qn["mesh"]]
        prims = []
        for p in qm["primitives"]:
            attr = {k: acc(v, gelenke=(k == "JOINTS_0")) for k, v in p["attributes"].items()}
            prims.append({"attributes": attr, "indices": acc(p["indices"]), "material": figur, "mode": p.get("mode", 4)})
        ziel_j["meshes"].append({"name": name, "primitives": prims})
        mi = len(ziel_j["meshes"]) - 1
        vorhanden = next((i for i, n in enumerate(ziel_j["nodes"]) if n.get("name") == name and "mesh" in n), None)
        if vorhanden is not None:
            ziel_j["nodes"][vorhanden]["mesh"] = mi
        else:
            ziel_j["nodes"].append({"mesh": mi, "name": name, "skin": 0})
            ziel_j["nodes"][skelett].setdefault("children", []).append(len(ziel_j["nodes"]) - 1)
        erg[name] = sorted(set(qm["primitives"][0]["attributes"]))
    return erg


def kanaele_kuerzen(j, namen):
    """In den neuen Clips nur die Drehungen aller Knochen und die Lage der Hüfte behalten: Lage der übrigen Knochen und Größe sind in
    jedem Clip der Datei die Ruhewerte (der Exporter schreibt sie trotzdem); so bleiben JSON und Puffer klein."""
    hips = next(i for i, n in enumerate(j["nodes"]) if n.get("name") == "Hips")
    for a in j["animations"]:
        if a["name"] not in namen:
            continue
        behalten = [c for c in a["channels"] if c["target"]["path"] == "rotation"
                    or (c["target"]["path"] == "translation" and c["target"]["node"] == hips)]
        neu_s = []
        for c in behalten:
            neu_s.append(a["samplers"][c["sampler"]])
            c["sampler"] = len(neu_s) - 1
        a["channels"], a["samplers"] = behalten, neu_s


def unbenutzte_netze_entfernen(j):
    """Netze, auf die kein Knoten mehr zeigt (ersetzte Werkzeuge eines zweiten Laufs), entfernen und die Nummern nachziehen."""
    benutzt = sorted({n["mesh"] for n in j["nodes"] if "mesh" in n})
    neu = {alt: i for i, alt in enumerate(benutzt)}
    j["meshes"] = [j["meshes"][i] for i in benutzt]
    for n in j["nodes"]:
        if "mesh" in n:
            n["mesh"] = neu[n["mesh"]]


# ─────────────────────────────────────────────────────────────────────────────
#  5. Bilder: Mittelbild je Clip in der Spielkamera und von der Seite
# ─────────────────────────────────────────────────────────────────────────────

BILDER = {}   # (Zeile, Clip) → Bildfeld


def bild():
    import numpy as np
    sc = bpy.context.scene
    pfad = os.path.join(tempfile.gettempdir(), "rg_arbeit_tmp.png")
    sc.render.filepath = pfad
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(pfad, check_existing=False)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return a


def bilder_machen(art, rig, netze, cfg, alt_rig, alt_netze, werkzeuge, clips, mit_alt):
    """Mittelbild jedes Clips: neue Figur in der Spielkamera und von links; bei `mit_alt` dazu die alte Figur in der Spielkamera."""
    C.vorschau_vorbereiten(rig, netze, cfg)
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = 300, 460
    koerper = {n for n, o in netze.items() if n.startswith(("Nah_", "Fuss_")) and not o.hide_render}
    alt_koerper = alt_netze["Koerper_Frau" if cfg["gender"] < 0.5 else "Koerper_Mann"]
    alt_koerper.color = (0.78, 0.62, 0.46, 1)
    for o in list(werkzeuge.values()) + list(alt_netze.values()):
        o.color = (0.42, 0.33, 0.24, 1)
    alt_koerper.color = (0.78, 0.62, 0.46, 1)
    for spur in rig.animation_data.nla_tracks:
        spur.mute = True

    def zeigen(neu, zeige):
        for n, o in netze.items():
            o.hide_render = not (neu and (n in koerper or n in zeige))
        for n, o in werkzeuge.items():
            o.hide_render = not (neu and n in zeige)
        for n, o in alt_netze.items():
            o.hide_render = not ((not neu) and (o is alt_koerper or n in zeige or (n == "Stab" and "Zub_stab" in zeige)))

    for clip in clips:
        act = next(a for a in bpy.data.actions if a.name.split(".")[0] == clip)
        alt_act = bpy.data.actions[f"ALT_{clip}"]
        n = int(act.frame_range[1] - act.frame_range[0])
        zeige = set(WERKZEUG_JE_CLIP.get(clip, ()))
        zeigen(True, zeige)
        P.aktion_setzen(rig, act)
        alt_rig.animation_data.action = None
        sc.frame_set(int(act.frame_range[0]) + n // 2)
        I.spielkamera(sc, 0.85, 2.2)
        BILDER[(f"{art} Spielkamera", clip)] = bild()
        C.kamera_setzen(sc, (6.0, 0.0, 0.95), (0, 0, 0.85), 2.2)
        BILDER[(f"{art} Seite", clip)] = bild()
        if mit_alt:
            zeigen(False, zeige)
            rig.animation_data.action = None
            P.aktion_setzen(alt_rig, alt_act)
            sc.frame_set(int(alt_act.frame_range[0]) + n // 2)
            I.spielkamera(sc, 0.85, 2.2)
            BILDER[("alt Spielkamera", clip)] = bild()
            C.kamera_setzen(sc, (6.0, 0.0, 0.95), (0, 0, 0.85), 2.2)
            BILDER[("alt Seite", clip)] = bild()
            alt_rig.animation_data.action = None
    rig.animation_data.action = None


def mitte_und_groesse(ob):
    """Mitte und größte Ausdehnung des ausgewerteten (gehäuteten) Netzes im Weltraum."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    me = ev.to_mesh()
    pkt = [ob.matrix_world @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    lo = Vector((min(p.x for p in pkt), min(p.y for p in pkt), min(p.z for p in pkt)))
    hi = Vector((max(p.x for p in pkt), max(p.y for p in pkt), max(p.z for p in pkt)))
    return (lo + hi) / 2, max(hi - lo)


def werkzeug_nahbilder(art, rig, netze, cfg, alt_rig, alt_netze, werkzeuge):
    """Je Werkzeug ein Nahbild an der Hand im Mittelbild seines Clips (Hellebarde: erstes Bild von Stehen), neue Figur aus der
    Spielrichtung und von links, darunter die alte Figur aus der Spielrichtung; Farben = Eckenfarbe (zeigt die Zone-0-Farbe)."""
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = 300, 300
    sh = sc.display.shading
    sh.color_type = "VERTEX"
    for spur in rig.animation_data.nla_tracks:
        spur.mute = True
    koerper = {n for n, o in netze.items() if n.startswith(("Nah_", "Fuss_")) and not o.hide_viewport}
    alt_koerper = alt_netze["Koerper_Frau" if cfg["gender"] < 0.5 else "Koerper_Mann"]
    g, nn = math.radians(I.GIER), math.radians(I.NEIGUNG)
    blick = Vector((-math.sin(g) * math.cos(nn), math.cos(g) * math.cos(nn), -math.sin(nn)))
    for wz in WERKZEUGE:
        clip = "Stehen" if wz == "Hellebarde" else next(c for c, t in WERKZEUG_JE_CLIP.items() if wz in t)
        act = next(a for a in bpy.data.actions if a.name.split(".")[0] == clip and not a.name.startswith("ALT_"))
        alt_act = bpy.data.actions[f"ALT_{clip}"]
        n = int(act.frame_range[1] - act.frame_range[0])
        bild_nr = 0 if wz == "Hellebarde" else n // 2
        for neu in (True, False):
            for nm, o in netze.items():
                o.hide_render = not (neu and nm in koerper)
            for nm, o in werkzeuge.items():
                o.hide_render = not (neu and nm == wz)
            for nm, o in alt_netze.items():
                o.hide_render = not ((not neu) and (o is alt_koerper or nm == wz))
            if neu:
                alt_rig.animation_data.action = None
                P.aktion_setzen(rig, act)
                sc.frame_set(int(act.frame_range[0]) + bild_nr)
            else:
                rig.animation_data.action = None
                P.aktion_setzen(alt_rig, alt_act)
                sc.frame_set(int(alt_act.frame_range[0]) + bild_nr)
            bpy.context.view_layer.update()
            mitte, gr = mitte_und_groesse(werkzeuge[wz] if neu else alt_netze[wz])
            breite = max(0.45, gr * 1.5)
            C.kamera_setzen(sc, tuple(mitte - blick * 6.0), tuple(mitte), breite)
            BILDER[(f"{'neu' if neu else 'alt'} {art} Spielrichtung", wz)] = bild()
            if neu:
                C.kamera_setzen(sc, tuple(mitte + Vector((6.0, 0, 0))), tuple(mitte), breite)
                BILDER[(f"neu {art} Seite", wz)] = bild()
    rig.animation_data.action = None
    alt_rig.animation_data.action = None
    sh.color_type = "OBJECT"


def tafel_schreiben(clips, zeilen, ziel, hoehe=460):
    """Kontaktbogen: Spalten = Clips, Zeilen = Figur/Ansicht (oben die erste Zeile)."""
    import numpy as np
    reihen = []
    for z in zeilen:
        teile = []
        for c in clips:
            a = BILDER.get((z, c))
            if a is None:
                a = np.ones((hoehe, 300, 4), dtype=np.float32)
            teile.append(np.concatenate([a, np.full((a.shape[0], 4, 4), (0.2, 0.2, 0.2, 1), dtype=np.float32)], axis=1))
        reihe = np.concatenate(teile, axis=1)
        reihen.append(np.concatenate([reihe, np.full((4, reihe.shape[1], 4), (0.2, 0.2, 0.2, 1), dtype=np.float32)], axis=0))
    gesamt = np.concatenate(list(reversed(reihen)), axis=0)     # Pixelzeile 0 ist unten
    h, w = gesamt.shape[:2]
    img = bpy.data.images.new("tafel", w, h, alpha=False)
    img.pixels.foreach_set(gesamt.reshape(-1))
    img.filepath_raw = ziel
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


# ─────────────────────────────────────────────────────────────────────────────
#  6. Ablauf
# ─────────────────────────────────────────────────────────────────────────────


def art_bearbeiten(art, clips, bild_clips):
    cfg = P.FIGUREN[art]
    ordner = OPT.get("ordner", os.path.join(REPO, "game", "assets", "modelle", "figuren", "probe"))
    quelle_glb = os.path.join(ordner, cfg["datei"])
    aus_glb = os.path.join(OPT.get("aus", ordner), cfg["datei"])
    C.szene_leeren()
    alt_rig, alt_netze = alt_laden(ALT_VOLK[art])
    rig, netze = C.figur_laden(quelle_glb)
    # Zweiter Lauf auf schon bearbeitete Dateien: deren Werkzeuge und Hocker werden neu gebaut (gleiche Namen)
    for n in list(netze):
        if n.split(".")[0] in WERKZEUGE or n.startswith("Tritt_"):
            bpy.data.objects.remove(netze.pop(n))
    stehen = next(a for a in bpy.data.actions if a.name.split(".")[0] == "Stehen")
    for spur in rig.animation_data.nla_tracks:
        spur.mute = True
    bpy.context.view_layer.update()
    rig.animation_data.action = None
    for pb in rig.pose.bones:        # Ruhelage (das Stummschalten der Spuren setzt die Haltung nicht zurück)
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
        pb.scale = Vector((1, 1, 1))
    bpy.context.view_layer.update()
    boden = I.sohle_in_ruhe(netze)
    huefte_neu = (rig.matrix_world @ rig.data.bones["LeftUpperLeg"].head_local).z - boden
    k = huefte_neu / F.VOELKER[ALT_VOLK[art]]["huefte"]
    alt = Alt(alt_rig, ALT_VOLK[art], k)
    alt.boden = boden
    ziel = Ziel(rig, alt)
    ziel.dicke_finger = art in DICKE_FINGER
    if "Zub_stab" in netze:
        # Achse des Steckens: die zwei am weitesten voneinander entfernten Ecken (Ruhelage), im Raum der rechten Hand
        ecken = [rig.matrix_world.inverted() @ (netze["Zub_stab"].matrix_world @ v.co) for v in netze["Zub_stab"].data.vertices]
        a = max(ecken, key=lambda v: (v - ecken[0]).length)
        b = max(ecken, key=lambda v: (v - a).length)
        ziel.stab_achse = (ziel.ruhe["RightHand"].to_3x3().inverted() @ (b - a)).normalized()
    bericht = {"datei": quelle_glb, "alt": f"figur_{ALT_VOLK[art]}.glb", "k": round(k, 4), "sohle_ruhe_m": round(boden, 4), "armmass": round(ziel.armmass, 4),
               "arm_relativ": ziel.arm_relativ, "clips": {}, "werkzeuge": {}}
    versatz = {}
    for name in clips:
        _, b = clip_uebertragen(rig, ziel, alt, name)
        versatz[name] = b.pop("versatz")
        bericht["clips"][name] = b
    if art == "mann":
        for c, liste in versatz.items():
            if c in AN_MOEBELN:
                REFERENZ[c] = sum((n.z - sch.z) / ziel.oberarm for a, n, sch in liste) / len(liste)
        bericht["mensch_bezug_oberarme"] = {c: round(v, 3) for c, v in sorted(REFERENZ.items())}
    if ziel.arm_relativ and any(c in AN_MOEBELN for c in clips):
        # Zweiter Durchgang für die Arbeit am Möbel: Tritt (eine Höhe je Figur) und Standort je Clip aus dem ersten Durchgang, die Faust
        # trifft die alte Handmitte wie bei den Menschen
        tritt = tritt_rechnen(versatz, k, SPIEL_MASS[art])
        # Hocker (Nutzer 14.11.2026): EINE feste Höhe je Figur (ein Gegenstand, er ändert sich nie zwischen den Clips): der Median der
        # nötigen Höhen aller stehenden Arbeitsclips (unter TRITT_MIN_HOEHE kein Hocker). Die Unterschiede zwischen den Clips gleicht der
        # Standort aus (vor, seitlich je Clip, STANDORT_RUNDEN-mal um den mittleren Rest nachgeführt); die Faust zielt auf die alte
        # Handmitte, die Arme behalten ihre Länge.
        # Ergonomie (Nutzer 14.11.2026): Die Hand am Möbel liegt zur Schulter wie beim Mann an derselben Werkbank (etwa Ellbogen- bis
        # Brusthöhe, nie über der Schulter). Nötige Höhe je Bild: Möbelhöhe der Hand − (Schulter der Figur + Bezug × Oberarm), alles im
        # Spielmaß; je Clip das Mittel, je Möbel der Median.
        mass = SPIEL_MASS[art]
        for c, liste in versatz.items():
            if c not in tritt["clips"] or c not in REFERENZ:
                continue
            noetig_c = [(a / k).z - sch.z * mass - REFERENZ[c] * ziel.oberarm * mass for a, n, sch in liste]
            tritt["clips"][c]["hoch_noetig_m"] = round(sum(noetig_c) / len(noetig_c), 4)
        # Reihenfolge der Bedingungen (Nutzer 14.11.2026): Zuerst trifft die Hand das Möbel, dann gilt die Ergonomie. Je Möbel die höchste
        # Hockerhöhe bis zur ergonomischen (Median der Clips), bei der jeder Clip des Möbels mit einem Standort „vor“ überall erreichbar
        # bleibt (Abstand Schulter – alte Handmitte ≤ REICH_ANTEIL × Reichweite des Arms); ist das nirgends möglich, die Höhe mit dem
        # kleinsten Überstand.
        reich = REICH_ANTEIL * ziel.reichweite * mass

        def ueberstand(liste, hoehe, vor, seit):
            return max(max(0.0, ((a / k) - (sch * mass + Vector((seit, -vor, hoehe)))).length - reich) for a, n, sch in liste)

        def bester_standort(c, hoehe):
            liste, seit = versatz[c], tritt["clips"][c]["seitlich_m"]
            werte = [(ueberstand(liste, hoehe, v / 100, seit), abs(v / 100 - tritt["clips"][c]["vor_m"]), v / 100) for v in range(-10, 51)]
            return min(werte)

        hocker, start_vor = {}, {}
        for gruppe in sorted({MOEBEL.get(c, "Werkbank") for c in tritt["clips"] if c in clips}):
            gc = [c for c in tritt["clips"] if c in clips and MOEBEL.get(c, "Werkbank") == gruppe]
            noetig = sorted(max(0.0, tritt["clips"][c]["hoch_noetig_m"]) for c in gc)
            ergo = noetig[len(noetig) // 2] if len(noetig) % 2 else (noetig[len(noetig) // 2 - 1] + noetig[len(noetig) // 2]) / 2
            kandidaten = [h / 100 for h in range(0, int(ergo * 100) + 1)] + [ergo]
            bewertet = [(h, sum(bester_standort(c, h)[0] for c in gc)) for h in kandidaten]
            erreichbar = [x for x in bewertet if x[1] <= 0.005 * len(gc)]
            beste = max(erreichbar) if erreichbar else min(bewertet, key=lambda x: x[1])
            hocker[gruppe] = beste[0] if beste[0] >= TRITT_MIN_HOEHE else 0.0
            for c in gc:
                start_vor[c] = bester_standort(c, hocker[gruppe])[2]
            bericht.setdefault("hocker_wahl", {})[gruppe] = {"ergonomisch_m": round(ergo, 4), "gewaehlt_m": round(hocker[gruppe], 4),
                                                            "ueberstand_m": round(beste[1], 4)}
        lage = {c: [start_vor[c], hocker[MOEBEL.get(c, "Werkbank")], w["seitlich_m"]] for c, w in tritt["clips"].items() if c in clips}
        ergebnis = {}
        for runde in range(STANDORT_RUNDEN + 1):
            for name, (vor, hoch, seit) in lage.items():
                ziel.standort = (vor, hoch, seit, SPIEL_MASS[art])
                REST.clear()
                ERGO.clear()
                _, b = clip_uebertragen(rig, ziel, alt, name)
                b.pop("versatz")
                bericht["clips"][name] = b
                laengen = [v.length for v in REST]
                ergebnis[name] = {"vor_m": round(vor, 4), "hoch_m": round(hoch, 4), "seitlich_m": round(seit, 4), "rest_mittel_m": round(sum(laengen) / len(laengen), 4),
                                  "rest_max_m": round(max(laengen), 4), "hand_ueber_schulter_mittel_m": round(sum(ERGO) / len(ERGO), 4),
                                  "hand_ueber_schulter_max_m": round(max(ERGO), 4),
                                  "mensch_hand_ueber_schulter_m": round(REFERENZ.get(name, 0.0) * ziel.oberarm * mass, 4)}
                m = sum(REST, Vector()) / len(REST)
                lage[name] = [vor - m.y, hoch, seit + m.x]
        bericht["tritt"] = {"tritt_hoehe_m": round(max(hocker.values()), 4), "hocker_m": {g: round(h, 4) for g, h in hocker.items()},
                            "clips": ergebnis,
                            "rest_mittel_m": round(sum(w["rest_mittel_m"] for w in ergebnis.values()) / len(ergebnis), 4),
                            "rest_max_m": round(max(w["rest_max_m"] for w in ergebnis.values()), 4)}
        ziel.standort = None
    rig.animation_data.action = None
    werkzeuge = {}
    for name in WERKZEUGE:
        ob, tri = werkzeug_bauen(rig, ziel, alt, alt_netze, name, stehen)
        werkzeuge[name] = ob
        bericht["werkzeuge"][name] = {"dreiecke": tri, "knochen": sorted(g.name for g in ob.vertex_groups)}
    if ziel.arm_relativ and "tritt" in bericht:
        for gruppe, hoehe in bericht["tritt"]["hocker_m"].items():
            if hoehe < TRITT_MIN_HOEHE:
                continue
            ob, tri = tritt_bauen(rig, hoehe, SPIEL_MASS[art], alt_netze, f"Tritt_{gruppe}")
            werkzeuge[ob.name] = ob
            bericht["werkzeuge"][ob.name] = {"dreiecke": tri, "knochen": ["Root"], "hoehe_m": hoehe}
    if bild_clips:
        bilder_machen(art, rig, netze, cfg, alt_rig, alt_netze, werkzeuge, bild_clips, art == "mann")
    if OPT.get("werkzeugbilder") == "1":
        if not bild_clips:
            C.vorschau_vorbereiten(rig, netze, cfg)
        werkzeug_nahbilder(art, rig, netze, cfg, alt_rig, alt_netze, werkzeuge)
    if OPT.get("schreiben", "1") != "1":
        return bericht
    # Hilfsdatei: Skelett, neue Clips, Werkzeuge
    for spur in list(rig.animation_data.nla_tracks):
        if spur.name not in clips:
            rig.animation_data.nla_tracks.remove(spur)
        else:
            spur.mute = False
    rig.animation_data.action = None
    for o in bpy.context.scene.objects:
        o.select_set(o is rig or o in werkzeuge.values())
    hilfe = os.path.join(tempfile.gettempdir(), f"rg_arbeit_{art}.glb")
    bpy.ops.export_scene.gltf(
        filepath=hilfe, export_format="GLB", use_selection=True, export_yup=True, export_apply=False,
        export_animations=True, export_animation_mode="NLA_TRACKS", export_anim_slide_to_zero=True, export_morph=False,
        export_skins=True, export_lights=False, export_cameras=False, export_image_format="NONE",
        export_vertex_color="ACTIVE", export_force_sampling=False)
    j, b = C.glb_lesen(quelle_glb)
    C.animationen_uebertragen(hilfe, j, b, clips)
    kanaele_kuerzen(j, clips)
    bericht["attribute"] = werkzeuge_uebertragen(hilfe, j, b, list(werkzeuge))
    unbenutzte_netze_entfernen(j)
    j, b = I.unbenutztes_entfernen(j, b)
    os.makedirs(os.path.dirname(aus_glb), exist_ok=True)
    C.glb_schreiben(aus_glb, j, b)
    bericht["groesse_vorher_b"] = os.path.getsize(quelle_glb) if quelle_glb != aus_glb else None
    bericht["groesse_b"] = os.path.getsize(aus_glb)
    return bericht


def hauptteil():
    arten = [a for a in P.FIGUREN if a in OPT.get("nur", ",".join(P.FIGUREN)).split(",")]
    clips = [c for c in OPT.get("clips", ",".join(ARBEIT)).split(",") if c]
    bild_clips = [c for c in OPT.get("bilder_clips", "").split(",") if c] if "bilder" in OPT else []
    if any(a in SPIEL_MASS for a in arten) and "mann" not in arten:
        # Bezug Mensch für die Hocker der kleinen Völker: der Mann nur gerechnet, nicht geschrieben
        schreiben = OPT.get("schreiben", "1")
        OPT["schreiben"] = "0"
        BERICHT["mann_bezug"] = art_bearbeiten("mann", [c for c in clips if c in AN_MOEBELN], []).get("mensch_bezug_oberarme")
        OPT["schreiben"] = schreiben
    for art in arten:
        BERICHT[art] = art_bearbeiten(art, clips, bild_clips)
    if bild_clips:
        os.makedirs(OPT["bilder"], exist_ok=True)
        zeilen = []
        for art in arten:
            zeilen += [f"{art} Spielkamera", f"{art} Seite"]
            if art == "mann":
                zeilen += ["alt Spielkamera", "alt Seite"]
        name = OPT.get("tafel", "tafel")
        tafel_schreiben(bild_clips, zeilen, os.path.join(OPT["bilder"], f"{name}.png"))
        BERICHT["tafel"] = {"zeilen_von_oben": zeilen, "spalten": bild_clips}
    if OPT.get("werkzeugbilder") == "1":
        os.makedirs(OPT["bilder"], exist_ok=True)
        zeilen = []
        for art in arten:
            zeilen += [f"neu {art} Spielrichtung", f"neu {art} Seite", f"alt {art} Spielrichtung"]
        for teil in range(0, len(WERKZEUGE), 7):
            spalten = list(WERKZEUGE[teil:teil + 7])
            tafel_schreiben(spalten, zeilen, os.path.join(OPT["bilder"], f"werkzeuge_{teil // 7 + 1}.png"), hoehe=300)
        BERICHT["werkzeugtafeln"] = {"zeilen_von_oben": zeilen, "spalten": list(WERKZEUGE)}
    # Tritt und Standort der kleinen Völker fürs Spiel (ArbeitsTritt.cs): nur beim Schreiben der Dateien, vorhandene Figuren bleiben
    tritte = {art: {"tritt_hoehe_m": v["tritt"]["tritt_hoehe_m"], "hocker_m": v["tritt"]["hocker_m"],
                    "clips": {c: {"moebel": MOEBEL.get(c, "Werkbank"), "vor_m": w["vor_m"], "seitlich_m": w["seitlich_m"],
                                  "faust_ueber_schulter_mittel_m": w["hand_ueber_schulter_mittel_m"],
                                  "faust_ueber_schulter_max_m": w["hand_ueber_schulter_max_m"],
                                  "mensch_faust_ueber_schulter_m": w["mensch_hand_ueber_schulter_m"]}
                              for c, w in sorted(v["tritt"]["clips"].items())}}
              for art, v in BERICHT.items() if isinstance(v, dict) and v.get("tritt")}
    if tritte and OPT.get("schreiben", "1") == "1":
        pfad = OPT.get("tritt_json", os.path.join(REPO, "game", "daten", "arbeitstritt.json"))
        alt = json.load(open(pfad, encoding="utf-8")) if os.path.exists(pfad) else {}
        alt.update(tritte)
        with open(pfad, "w", encoding="utf-8", newline=chr(10)) as f:
            json.dump(dict(sorted(alt.items())), f, indent=1, ensure_ascii=False)
            f.write(chr(10))
    if "bericht" in OPT:
        with open(OPT["bericht"], "w", encoding="utf-8") as f:
            json.dump(BERICHT, f, indent=1, ensure_ascii=False, default=str)
    print("BERICHT", json.dumps(BERICHT, indent=1, ensure_ascii=False, default=str))


if __name__ == "__main__":
    hauptteil()
