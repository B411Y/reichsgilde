"""Passanten: Grundkörper je Volk mit Skelett, Trachtteilen und Bewegungen.

    blender -b --python tools/modelle/figuren.py        oder
    python tools/modelle/figuren.py [--schnell] [--ohne-bilder] [--nur-bewegungen]

Schreibt game/assets/modelle/figur_mensch.glb, figur_zwerg.glb, figur_elb.glb, figur_halbling.glb
und Vorschaubilder nach docs/modelle/ (figuren.png: alle Körper und Trachten, figuren-bewegungen.png: die Bewegungen).

Inhalt je Datei (Blender: Z oben, Blick nach -Y; im Spiel Y oben, Blick nach +Z):
    Skelett         elf Knochen: Wurzel, Ruecken, Kopf, Oberarm_L/R, Unterarm_L/R, Oberschenkel_L/R, Unterschenkel_L/R
                    („L“ ist die linke Seite der Figur). Jeder Teil hängt starr an genau einem Knochen.
    Koerper_Mann    Wams bis zur Mitte des Oberschenkels, Beinkleid, Schuhe, Gürtel; beim Zwerg mit Bart
    Koerper_Frau    Kleid bis über die Knöchel, Zopf
    Trachtteile     Strohhut, Barett, Kapuze, Haube, Schleier, Schuerze, Kiepe, Buendel, Tasche, Stab, Hacke, Hammer
                    – standardmäßig aus, das Spiel blendet sie je Rolle ein (wie heute `TrachtVon`; den Hammer beim Hämmern)
    Bewegungen      Stehen, Gehen, Eilen, Gebeugt, Tragen, Sitzen, Reden, Hacken, Buecken, Hueten,
                    Ziehen (Handkarren), Kutschieren (auf dem Bock),
                    Haemmern, Blasebalg, Schleifen (stehend), Feinarbeit (sitzend) – Arbeit in der Werkstatt
                    – jede eine Schleife am Ort (ohne Vorwärtsbewegung; das Spiel schiebt die Figur)
                    Die Werkstatt-Bewegungen zielen mit den Händen auf feste Höhen (ARBEITSHOEHEN, wie im Innenraum
                    schmied_innen.glb): Jedes Volk erreicht sie mit eigenen Armwinkeln (`arm_ik`).
Material: alle Teile (Körper, Trachtteile, Werkzeug) tragen nur ein Material „Figur“ – eine Figur ist so ein
Zeichenaufruf statt sechs. Je Ecke stehen zwei Angaben im Netz, je Fläche konstant:
    COLOR_0         Grundfarbe als sRGB-Wert (wie bei den Umlandbetrieben, siehe bausteine.farben_zusammenlegen)
    TEXCOORD_0      (Godot: UV) x = Zone, y = 0:  0 feste Farbe aus COLOR_0 (Auge, Leder, Holz, Eisen, Leinen …),
                    1 Gewand, 2 Beinkleid, 3 Haut, 4 Haar, 5 Kopfbedeckung (ZONEN). Das Spiel färbt die Zonen 1–5
                    je Figur; in COLOR_0 steht dort die Vorgabefarbe, ohne eigenen Shader sieht die Figur also aus wie bisher.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Gewand": (0.30, 0.40, 0.55),          # Vorgabe; das Spiel färbt je Figur
    "Beinkleid": (0.30, 0.26, 0.22),
    "Haut": (0.86, 0.68, 0.55),
    "Haar": (0.32, 0.22, 0.14),
    "Kopfbedeckung": (0.78, 0.74, 0.64),
    "Leinen": (0.84, 0.81, 0.72),
    "Geflecht": (0.56, 0.44, 0.27),
    "Auge": (0.08, 0.07, 0.07),
})

FPS = 24
VOELKER = {
    #            Höhe, Hüfte, Knie, Schulter, Hals, Kopfradius, Schulterbreite/2, Hüftbreite/2, Oberarm, Unterarm, Fuß, Rumpftiefe
    "mensch": dict(huefte=0.92, knie=0.50, schulter=1.45, hals=0.07, kopf=0.12, schulterbreite=0.20, hueftbreite=0.12,
                   oberarm=0.29, unterarm=0.38, fuss=0.24, tiefe=0.12),
    "zwerg": dict(huefte=0.58, knie=0.31, schulter=1.06, hals=0.05, kopf=0.135, schulterbreite=0.26, hueftbreite=0.16,
                  oberarm=0.24, unterarm=0.30, fuss=0.25, tiefe=0.16),
    "elb": dict(huefte=1.04, knie=0.57, schulter=1.60, hals=0.08, kopf=0.115, schulterbreite=0.18, hueftbreite=0.11,
                oberarm=0.32, unterarm=0.42, fuss=0.25, tiefe=0.105),
    "halbling": dict(huefte=0.53, knie=0.29, schulter=0.90, hals=0.05, kopf=0.125, schulterbreite=0.155, hueftbreite=0.11,
                     oberarm=0.2, unterarm=0.26, fuss=0.24, tiefe=0.11),
}
ZONEN = {"Gewand": 1, "Beinkleid": 2, "Haut": 3, "Haar": 4, "Kopfbedeckung": 5}   # alle übrigen Stoffe: Zone 0
TRACHT = ["Strohhut", "Barett", "Kapuze", "Haube", "Schleier", "Schuerze", "Kiepe", "Buendel", "Tasche", "Stab", "Hacke", "Hammer"]
HAMMER = 0.3          # vom Mittelpunkt der Hand bis zur Mitte des Hammerkopfs, in Verlängerung des Unterarms
# Wo die Hände bei der Arbeit sind (Meter vor dem Fußpunkt der Figur, Höhe über dem Boden) – passend zu den Ankern
# im Innenraum: Amboss 0,68 m vor der Figur, Bahn 0,86 m hoch; Hebel des Blasebalgs 0,42 m vor ihr, 1,36 m hoch;
# Schleifstein (Mitte 0,6 m vor ihr, 0,72 m hoch, Halbmesser 0,38); Feinwerktisch 0,77 m hoch.
ARBEITSHOEHEN = {"Amboss": (0.68, 0.90), "Zange": (0.52, 0.93), "Hebel": (0.42, 1.36), "Stein": (0.36, 0.99),
                 "Tisch": (0.44, 0.81)}


# ─────────────────────────────────────────────────────────────────────────────
#  Geometrie: Ringe übereinander (Rumpf, Glieder, Kopf) – die Figur steht in Ruhe senkrecht
# ─────────────────────────────────────────────────────────────────────────────

class Teile:
    """Netz plus Buch, welche Ecken zu welchem Knochen gehören (jede Ecke starr an einem Knochen)."""

    def __init__(self, name):
        self.netz = bs.Netz(name)
        self.gruppen = []          # (Knochen, erste Ecke, letzte Ecke + 1)
        self.knochen = None

    def an(self, knochen):
        self.knochen = knochen
        return self

    def __enter__(self):
        self._start = len(self.netz.ecken)
        return self.netz

    def __exit__(self, *a):
        self.gruppen.append((self.knochen, self._start, len(self.netz.ecken)))

    def objekt(self, skelett):
        import bpy
        o = self.netz.objekt()
        for name in {g[0] for g in self.gruppen}:
            o.vertex_groups.new(name=name)
        for knochen, a, b in self.gruppen:
            if b > a:
                o.vertex_groups[knochen].add(list(range(a, b)), 1.0, "REPLACE")
        o.parent = skelett
        mod = o.modifiers.new("Skelett", "ARMATURE")
        mod.object = skelett
        bpy.context.view_layer.update()
        return o


def ringe(netz, st, liste, stoff, n=8, oben=True, unten=False, cx=0.0, cy=0.0, dreh=math.pi / 8):
    """Körper aus waagrechten Ringen: liste = [(z, rx, ry[, dy])]; Seiten als Vierecke, auf Wunsch Deckel."""
    rs = []
    for eintrag in liste:
        z, rx, ry = eintrag[:3]
        dy = eintrag[3] if len(eintrag) > 3 else 0.0
        rs.append([Vector((cx + rx * math.cos(dreh + 2 * math.pi * k / n), cy + dy + ry * math.sin(dreh + 2 * math.pi * k / n), z))
                   for k in range(n)])
    m = st[stoff] if isinstance(stoff, str) else stoff
    for i in range(len(rs) - 1):
        mitte = sum(rs[i] + rs[i + 1], Vector()) / (2 * n)
        for k in range(n):
            j = (k + 1) % n
            quad = [rs[i][k], rs[i][j], rs[i + 1][j], rs[i + 1][k]]
            netz.vieleck_nach(quad, sum(quad, Vector()) / 4 - mitte, m)
    if oben:
        netz.vieleck_nach(rs[-1], (0, 0, 1), m)
    if unten:
        netz.vieleck_nach(rs[0], (0, 0, -1), m)


def koerper(teile, st, p, frau, zwerg):
    """Grundkörper aus Ringen; jeder Teil an seinem Knochen."""
    hb, sb, t = p["hueftbreite"], p["schulterbreite"], p["tiefe"]
    hu, kn, sc = p["huefte"], p["knie"], p["schulter"]
    taille = hu + (sc - hu) * 0.25
    hals_z = sc + p["hals"]
    kopf_m = hals_z + p["kopf"] * 0.95
    r = p["kopf"]
    # Beine: Oberschenkel, Unterschenkel mit Schuh
    for s, seite in ((1, "L"), (-1, "R")):
        x = s * hb * 0.7
        with teile.an(f"Oberschenkel_{seite}") as n:
            ringe(n, st, [(kn, hb * 0.42, hb * 0.45), (hu - 0.02, hb * 0.6, hb * 0.62)], "Beinkleid", n=6, cx=x, oben=False)
        with teile.an(f"Unterschenkel_{seite}") as n:
            ringe(n, st, [(0.09, hb * 0.32, hb * 0.34), (kn + 0.02, hb * 0.42, hb * 0.45)], "Beinkleid", n=6, cx=x, oben=False)
            f = p["fuss"]
            n.quader((x, -f * 0.25, 0.045), (hb * 0.75, f, 0.09), st["Leder"], ohne=("-z",))
    # Arme mit Händen
    for s, seite in ((1, "L"), (-1, "R")):
        x = s * (sb + 0.01)
        oa, ua = p["oberarm"], p["unterarm"]
        z_ell = sc - 0.03 - oa
        with teile.an(f"Oberarm_{seite}") as n:
            ringe(n, st, [(z_ell, 0.045 * sb / 0.2, 0.05), (sc + 0.02, 0.06 * sb / 0.2, 0.065)], "Gewand", n=6, cx=x)
        with teile.an(f"Unterarm_{seite}") as n:
            ringe(n, st, [(z_ell - ua * 0.72, 0.04 * sb / 0.2, 0.042), (z_ell + 0.02, 0.048 * sb / 0.2, 0.05)], "Gewand",
                  n=6, cx=x, oben=False)
            n.quader((x, 0, z_ell - ua * 0.72 - 0.06), (0.06, 0.07, 0.13), st["Haut"])
    # Rumpf (am Rücken), unten Gürtel; Wams oder Kleid an der Wurzel
    with teile.an("Ruecken") as n:
        ringe(n, st, [(taille, hb * 1.05, t * 0.95), (sc - 0.08, sb * 0.98, t * 1.05), (sc + 0.01, sb * 0.8, t * 0.85)],
              "Gewand", n=8)
        ringe(n, st, [(sc, 0.045, 0.045), (hals_z + 0.02, 0.04, 0.04)], "Haut", n=6, oben=False)
    with teile.an("Wurzel") as n:
        ringe(n, st, [(taille - 0.025, hb * 1.08, t * 0.98), (taille + 0.025, hb * 1.08, t * 0.98)], "Leder", n=8,
              oben=False)
        if frau:     # Kleid bis über die Knöchel
            ringe(n, st, [(0.12, hb * 2.1, t * 2.0), (kn, hb * 1.65, t * 1.55), (taille - 0.02, hb * 1.06, t * 0.96)],
                  "Gewand", n=8, oben=False)
        else:        # Wams bis zur Mitte des Oberschenkels
            ringe(n, st, [((hu + kn) / 2 + 0.03, hb * 1.5, t * 1.25), (taille - 0.02, hb * 1.06, t * 0.96)], "Gewand",
                  n=8, oben=False)
    # Kopf: Gesicht, Nase, Augen, Haar
    with teile.an("Kopf") as n:
        ringe(n, st, [(kopf_m - r * 0.95, r * 0.45, r * 0.5), (kopf_m - r * 0.55, r * 0.85, r * 0.9),
                      (kopf_m, r, r * 1.02), (kopf_m + r * 0.55, r * 0.85, r * 0.88), (kopf_m + r * 0.95, r * 0.35, r * 0.38)],
              "Haut", n=8)
        n.quader((0, -r * 1.0, kopf_m - r * 0.1), (r * 0.22, r * 0.25, r * 0.35), st["Haut"])
        for s in (-1, 1):
            n.vieleck_nach([(s * r * 0.38 - r * 0.09, -r * 0.93, kopf_m + r * 0.14), (s * r * 0.38 + r * 0.09, -r * 0.93, kopf_m + r * 0.14),
                            (s * r * 0.38 + r * 0.09, -r * 0.93, kopf_m + r * 0.26), (s * r * 0.38 - r * 0.09, -r * 0.93, kopf_m + r * 0.26)],
                           (0, -1, 0), st["Auge"])
        # Haar: Kappe hinten und oben, etwas größer als der Kopf
        ringe(n, st, [(kopf_m - r * 0.35, r * 1.06, r * 1.0, r * 0.3), (kopf_m + r * 0.5, r * 0.95, r * 0.97, r * 0.06),
                      (kopf_m + r * 1.02, r * 0.42, r * 0.44)], "Haar", n=8)
        if frau:     # Zopf hinten
            ringe(n, st, [(kopf_m - r * 2.2, r * 0.18, r * 0.18, r * 1.0), (kopf_m - r * 0.2, r * 0.3, r * 0.3, r * 0.95)],
                  "Haar", n=5)
        if zwerg and not frau:   # Bart über Kinn und Brust
            ringe(n, st, [(kopf_m - r * 2.0, r * 0.25, r * 0.15, -r * 1.05), (kopf_m - r * 1.1, r * 0.7, r * 0.42, -r * 0.85),
                          (kopf_m - r * 0.3, r * 0.82, r * 0.55, -r * 0.55)], "Haar", n=6)


def tracht(name, st, p):
    """Ein Trachtteil (eigenes Objekt, starr an einem Knochen)."""
    t = Teile(name)
    r, sc, hu, kn = p["kopf"], p["schulter"], p["huefte"], p["knie"]
    kopf_m = sc + p["hals"] + r * 0.95
    sb, tf, hb = p["schulterbreite"], p["tiefe"], p["hueftbreite"]
    taille = hu + (sc - hu) * 0.25
    hand_z = sc - 0.03 - p["oberarm"] - p["unterarm"] * 0.72 - 0.06
    xr = -(sb + 0.01)                       # rechte Hand
    if name == "Strohhut":
        with t.an("Kopf") as n:
            ringe(n, st, [(kopf_m + r * 0.45, r * 2.6, r * 2.6), (kopf_m + r * 0.55, r * 2.5, r * 2.5)], "Stroh", n=10)
            ringe(n, st, [(kopf_m + r * 0.55, r * 1.05, r * 1.05), (kopf_m + r * 1.35, r * 0.8, r * 0.8)], "Stroh", n=8)
    elif name == "Barett":
        with t.an("Kopf") as n:
            ringe(n, st, [(kopf_m + r * 0.55, r * 1.08, r * 1.08), (kopf_m + r * 0.85, r * 1.35, r * 1.3, r * 0.1),
                          (kopf_m + r * 1.0, r * 1.25, r * 1.2, r * 0.1)], "Kopfbedeckung", n=8)
    elif name in ("Kapuze", "Schleier", "Haube"):
        with t.an("Kopf") as n:
            if name == "Haube":
                ringe(n, st, [(kopf_m - r * 0.2, r * 1.1, r * 1.12, r * 0.12), (kopf_m + r * 0.6, r * 1.02, r * 1.02, r * 0.05),
                              (kopf_m + r * 1.1, r * 0.5, r * 0.5)], "Kopfbedeckung", n=8)
            else:      # Kapuze bzw. Schleier fallen bis auf die Schultern
                ringe(n, st, [(sc - 0.03, sb * 0.95, tf * 1.2, r * 0.25), (kopf_m - r * 0.6, r * 1.25, r * 1.2, r * 0.3),
                              (kopf_m + r * 0.5, r * 1.12, r * 1.12, r * 0.12), (kopf_m + r * 1.12, r * 0.5, r * 0.5, r * 0.1)],
                      "Kopfbedeckung", n=8)
    elif name == "Schuerze":
        with t.an("Wurzel") as n:
            y = -tf * 1.02
            n.vieleck_nach([(-hb * 1.0, y, taille), (hb * 1.0, y, taille), (hb * 1.25, y - 0.1 * hb, kn + 0.03),
                            (-hb * 1.25, y - 0.1 * hb, kn + 0.03)], (0, -1, 0), st["Leinen"])
            n.vieleck_nach([(-hb * 1.25, y - 0.1 * hb + 0.01, kn + 0.03), (hb * 1.25, y - 0.1 * hb + 0.01, kn + 0.03),
                            (hb * 1.0, y + 0.01, taille), (-hb * 1.0, y + 0.01, taille)], (0, 1, 0), st["Leinen"])
    elif name == "Kiepe":
        with t.an("Ruecken") as n:
            ringe(n, st, [(taille, sb * 0.8, tf * 0.8, tf * 1.9), (sc + 0.12, sb * 1.05, tf * 1.05, tf * 2.1)], "Geflecht", n=6)
            for s in (-1, 1):
                n.stab((s * sb * 0.6, -tf * 0.9, sc - 0.02), (s * sb * 0.6, tf * 1.2, sc + 0.02), 0.03, 0.025, st["Leder"])
    elif name == "Buendel":
        with t.an("Ruecken") as n:      # auf der rechten Schulter, die Hand hält es (Bewegung „Tragen“)
            n.stab((xr - 0.05, -0.35, sc + 0.1), (xr - 0.05, 0.35, sc + 0.1), 0.24, 0.24, st["Holz alt"])
    elif name == "Tasche":
        with t.an("Wurzel") as n:
            n.quader((-hb * 1.4, -0.02, hu - 0.05), (0.06, 0.2, 0.22), st["Leder"])
            n.stab((-hb * 1.4, 0, hu + 0.06), (sb * 0.9, 0, sc - 0.02), 0.03, 0.02, st["Leder"])
    elif name == "Stab":
        with t.an("Wurzel") as n:         # steht senkrecht rechts vor der Figur; beim Hüten greift die Hand ihn
            n.stab((xr - 0.04, -0.3, 0.0), (xr - 0.04, -0.3, sc + 0.35), 0.045, 0.045, st["Holz"])
    elif name == "Hacke":
        with t.an("Unterarm_R") as n:     # Stiel verlängert den Unterarm nach unten, das Blatt zeigt nach vorn
            unten = hand_z - 0.85
            n.stab((xr, 0, hand_z + 0.08), (xr, 0, unten), 0.04, 0.04, st["Holz"])
            n.quader((xr, -0.09, unten + 0.02), (0.12, 0.2, 0.04), st["Eisen"])
    elif name == "Hammer":
        with t.an("Unterarm_R") as n:     # Stiel verlängert den Unterarm, der Kopf liegt quer (Bahn nach vorn, Finne nach hinten)
            unten = hand_z - HAMMER
            n.stab((xr, 0, hand_z + 0.06), (xr, 0, unten + 0.02), 0.032, 0.032, st["Holz"])
            n.quader((xr, -0.015, unten), (0.065, 0.17, 0.065), st["Eisen"])
    return t


# ─────────────────────────────────────────────────────────────────────────────
#  Skelett und Bewegungen
# ─────────────────────────────────────────────────────────────────────────────

def skelett(p):
    import bpy
    arm = bpy.data.armatures.new("Skelett")
    ob = bpy.data.objects.new("Skelett", arm)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    sc, hu, kn = p["schulter"], p["huefte"], p["knie"]
    kopf_z = sc + p["hals"]

    def knochen(name, kopf, schwanz, eltern=None):
        b = arm.edit_bones.new(name)
        b.head, b.tail = kopf, schwanz
        if eltern:
            b.parent = arm.edit_bones[eltern]
        return b

    knochen("Wurzel", (0, 0, hu), (0, 0, hu + 0.08))
    knochen("Ruecken", (0, 0, hu), (0, 0, sc), "Wurzel")
    knochen("Kopf", (0, 0, kopf_z), (0, 0, kopf_z + 2 * p["kopf"]), "Ruecken")
    for s, seite in ((1, "L"), (-1, "R")):
        x = s * (p["schulterbreite"] + 0.01)
        ell = sc - 0.03 - p["oberarm"]
        knochen(f"Oberarm_{seite}", (x, 0, sc - 0.03), (x, 0, ell), "Ruecken")
        knochen(f"Unterarm_{seite}", (x, 0, ell), (x, 0, ell - p["unterarm"]), f"Oberarm_{seite}")
        xb = s * p["hueftbreite"] * 0.7
        knochen(f"Oberschenkel_{seite}", (xb, 0, hu), (xb, 0, kn), "Wurzel")
        knochen(f"Unterschenkel_{seite}", (xb, 0, kn), (xb, 0, 0.06), f"Oberschenkel_{seite}")
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in ob.pose.bones:
        pb.rotation_mode = "XYZ"
    return ob


def g(w):
    return math.radians(w)


def arm_ik(p, lehne, hoch, vor, hoehe, extra=0.0):
    """Winkel (Grad) für Oberarm und Unterarm (X, wie in `haltung`), damit die Hand – mit `extra` Metern Werkzeug in
    Verlängerung des Unterarms dessen Ende – `vor` Meter vor dem Fußpunkt auf `hoehe` Metern liegt. Der Rumpf ist um
    `lehne` Grad nach vorn geneigt, die Hüfte um `hoch` verschoben. Zu weit weg: der Arm zeigt gestreckt dorthin.
    Der Ellbogen knickt nach vorn (die natürliche Lösung)."""
    l1 = p["oberarm"]
    l2 = p["unterarm"] * 0.72 + 0.06 + extra
    w = math.radians(lehne)
    rumpf = p["schulter"] - 0.03 - p["huefte"]
    dx = vor - rumpf * math.sin(w)
    dz = hoehe - (p["huefte"] + hoch + rumpf * math.cos(w))
    d = min(math.hypot(dx, dz), l1 + l2 - 1e-4)
    ziel = math.atan2(dx, -dz)                       # Richtung zum Ziel, von unten gemessen, nach vorn positiv
    a = ziel - math.acos(max(-1.0, min(1.0, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
    b = math.atan2(dx - l1 * math.sin(a), -(dz + l1 * math.cos(a)))
    # Der Arm hängt am geneigten Rumpf: im Knochen zählt der Winkel zum Rumpf, vorn ist dort −X
    return -(math.degrees(a) + lehne), -(math.degrees(b) - math.degrees(a))


def haltung(name, phase, p):
    """Pose zur Phase 0…1 einer Bewegung: {Knochen: (x, y, z) in Grad}, dazu die Hüfthöhe (Meter, relativ).
    Vorzeichen: Bei Beinen und Armen (hängende Knochen) schwingt negatives X nach vorn, beim Rücken und Kopf
    (stehende Knochen) neigt positives X nach vorn. Z dreht um die Hochachse."""
    s = math.sin(2 * math.pi * phase)
    c = math.cos(2 * math.pi * phase)
    s2 = math.sin(4 * math.pi * phase)
    r = {}
    hoch = 0.0

    def gang(bein, arm, knie, takt_hoch, vor=0.0):
        nonlocal hoch
        r["Oberschenkel_L"] = (-bein * s, 0, 0)
        r["Oberschenkel_R"] = (bein * s, 0, 0)
        r["Unterschenkel_L"] = (knie * max(0.0, -c) + 4, 0, 0)          # Knie beugt beim Durchschwingen
        r["Unterschenkel_R"] = (knie * max(0.0, c) + 4, 0, 0)
        r["Oberarm_L"] = (arm * s, 0, 6)
        r["Oberarm_R"] = (-arm * s, 0, -6)
        r["Unterarm_L"] = (-12 - arm * 0.4 * max(0.0, s), 0, 0)
        r["Unterarm_R"] = (-12 - arm * 0.4 * max(0.0, -s), 0, 0)
        r["Ruecken"] = (vor, 0, 4 * s)
        r["Kopf"] = (-vor * 0.6, 0, -3 * s)
        hoch = takt_hoch * abs(s2) - takt_hoch * 0.5

    if name == "Stehen":
        r["Ruecken"] = (1.5 * s, 0, 0)
        r["Kopf"] = (-1.5 * s, 0, 3 * math.sin(2 * math.pi * phase + 1))
        for seite, z in (("L", 6), ("R", -6)):
            r[f"Oberarm_{seite}"] = (2 * s, 0, z)
            r[f"Unterarm_{seite}"] = (-8, 0, 0)
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (0, 0, 2), (0, 0, -2)
        hoch = 0.006 * s
    elif name == "Gehen":
        gang(24, 22, 38, 0.035)
    elif name == "Eilen":
        gang(32, 32, 50, 0.05, vor=9)
    elif name == "Gebeugt":
        gang(14, 6, 24, 0.02, vor=26)
        for seite in ("L", "R"):
            r[f"Unterarm_{seite}"] = (-30, 0, 0)
        r["Kopf"] = (-22, 0, 0)
        hoch -= 0.04
    elif name == "Tragen":
        gang(18, 14, 30, 0.025, vor=6)
        r["Oberarm_R"] = (-20, 0, -150)                                 # rechte Hand hält die Last auf der Schulter
        r["Unterarm_R"] = (-110, 0, 0)
    elif name == "Sitzen":
        hoch = -(p["huefte"] - (p["knie"] - 0.02)) + 0.006 * s           # Hüfte auf Sitzhöhe (etwa Kniehöhe)
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (-88, 0, 4), (-88, 0, -4)
        r["Unterschenkel_L"], r["Unterschenkel_R"] = (88, 0, 0), (88, 0, 0)
        r["Ruecken"] = (2 + 1.5 * s, 0, 0)
        r["Kopf"] = (-2 - 1.5 * s, 0, 0)
        for seite, z in (("L", 4), ("R", -4)):
            r[f"Oberarm_{seite}"] = (-18, 0, z)
            r[f"Unterarm_{seite}"] = (-55, 0, 0)
    elif name == "Reden":
        r["Ruecken"] = (1.5 * s, 0, 5 * math.sin(2 * math.pi * phase * 2))
        r["Kopf"] = (6 * max(0.0, s2), 0, 8 * math.sin(2 * math.pi * phase))
        r["Oberarm_R"] = (-35 - 20 * max(0.0, s), 0, -18)
        r["Unterarm_R"] = (-60 - 25 * abs(s2), 0, 0)
        r["Oberarm_L"] = (-15 - 10 * max(0.0, -s), 0, 10)
        r["Unterarm_L"] = (-45, 0, 0)
    elif name == "Hacken":
        # Ausholen (0–0,45), Schlag (0,45–0,6), Herausziehen (0,6–1)
        if phase < 0.45:
            a = phase / 0.45
        elif phase < 0.6:
            a = 1 - (phase - 0.45) / 0.15
        else:
            a = 0.0
        hub = 105 * a
        r["Ruecken"] = (22 - 14 * a, 0, -6)
        r["Kopf"] = (-6, 0, 0)
        for seite, z in (("L", 14), ("R", -14)):
            r[f"Oberarm_{seite}"] = (-25 - hub, 0, z)
            r[f"Unterarm_{seite}"] = (-25 - 40 * a, 0, 0)
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (-14, 0, 4), (10, 0, -4)
        r["Unterschenkel_L"], r["Unterschenkel_R"] = (18, 0, 0), (6, 0, 0)
        hoch = -0.03
    elif name == "Buecken":
        b = 0.5 - 0.5 * math.cos(2 * math.pi * phase)                   # 0 → 1 → 0
        r["Ruecken"] = (70 * b, 0, 0)
        r["Kopf"] = (-25 * b, 0, 0)
        for seite, z in (("L", 6), ("R", -6)):
            r[f"Oberarm_{seite}"] = (-40 * b, 0, z)
            r[f"Unterarm_{seite}"] = (-10 - 15 * b, 0, 0)
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (-35 * b, 0, 3), (-35 * b, 0, -3)
        r["Unterschenkel_L"], r["Unterschenkel_R"] = (60 * b, 0, 0), (60 * b, 0, 0)
        hoch = -0.16 * b * p["huefte"] / 0.92
    elif name == "Ziehen":                                              # Handkarren: Holme in den Händen, vorgebeugt
        gang(20, 0, 34, 0.03, vor=14)
        for seite, z in (("L", 12), ("R", -12)):
            r[f"Oberarm_{seite}"] = (14, 0, z)
            r[f"Unterarm_{seite}"] = (-28, 0, 0)
    elif name == "Kutschieren":                                         # auf dem Bock, Zügel in beiden Händen
        hoch = -(p["huefte"] - (p["knie"] - 0.02)) + 0.012 * abs(s2)
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (-82, 0, 6), (-82, 0, -6)
        r["Unterschenkel_L"], r["Unterschenkel_R"] = (70, 0, 0), (70, 0, 0)
        r["Ruecken"] = (6 + 2 * s, 0, 0)
        r["Kopf"] = (-6, 0, 4 * math.sin(2 * math.pi * phase))
        for seite, z in (("L", 8), ("R", -8)):
            r[f"Oberarm_{seite}"] = (-38 - 4 * s, 0, z)
            r[f"Unterarm_{seite}"] = (-42, 0, 0)
    elif name == "Haemmern":                                            # am Amboss, Hammer in der Rechten
        # Ausholen (0–0,5), Schlag (0,5–0,62), Nachfedern, kurz Ruhe auf dem Werkstück
        if phase < 0.5:
            a = math.sin(math.pi / 2 * phase / 0.5)
        elif phase < 0.62:
            a = 1 - (phase - 0.5) / 0.12
        elif phase < 0.74:
            a = 0.12 * math.sin(math.pi * (phase - 0.62) / 0.12)
        else:
            a = 0.0
        lehne = 8 + 4 * a
        hoch = -0.015 * (1 - a)
        ober, unter = arm_ik(p, lehne, hoch, *ARBEITSHOEHEN["Amboss"], extra=HAMMER)
        r["Oberarm_R"] = (ober - 100 * a, 0, -6)
        r["Unterarm_R"] = (unter - 45 * a, 0, 0)
        ober, unter = arm_ik(p, lehne, hoch, *ARBEITSHOEHEN["Zange"])       # die Linke hält das Werkstück mit der Zange
        r["Oberarm_L"], r["Unterarm_L"] = (ober, 0, 12), (unter, 0, 0)
        r["Ruecken"] = (lehne, 0, -5 + 6 * a)
        r["Kopf"] = (18 - lehne, 0, 4 - 4 * a)
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (-10, 0, 5), (7, 0, -5)
        r["Unterschenkel_L"], r["Unterschenkel_R"] = (12 + 6 * (1 - a), 0, 0), (6 + 6 * (1 - a), 0, 0)
    elif name == "Blasebalg":                                           # beide Hände am Hebel: hinunterziehen, hochlassen
        zug = 0.5 - 0.5 * math.cos(2 * math.pi * phase)                 # 0 oben → 1 unten → 0
        lehne = 4 + 10 * zug
        hoch = -0.035 * zug
        vor, hoehe = ARBEITSHOEHEN["Hebel"]
        ober, unter = arm_ik(p, lehne, hoch, vor, hoehe + 0.1 - 0.22 * zug)
        for seite, z in (("L", 10), ("R", -10)):
            r[f"Oberarm_{seite}"], r[f"Unterarm_{seite}"] = (ober, 0, z), (unter, 0, 0)
        r["Ruecken"] = (lehne, 0, 0)
        r["Kopf"] = (2 - lehne * 0.6, 0, 6 * math.sin(2 * math.pi * phase + 0.4))
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (-12 - 10 * zug, 0, 4), (10 - 8 * zug, 0, -4)
        r["Unterschenkel_L"], r["Unterschenkel_R"] = (8 + 18 * zug, 0, 0), (4 + 14 * zug, 0, 0)
    elif name == "Schleifen":                                           # Werkstück mit beiden Händen an den Stein, rechter Fuß tritt
        lehne = 16
        hoch = -0.01
        vor, hoehe = ARBEITSHOEHEN["Stein"]
        hin = 0.025 * math.sin(4 * math.pi * phase)                     # zweimal je Schleife über den Stein
        ober, unter = arm_ik(p, lehne, hoch, vor + hin, hoehe + 0.5 * hin)
        for seite, z in (("L", 12), ("R", -12)):
            r[f"Oberarm_{seite}"], r[f"Unterarm_{seite}"] = (ober, 0, z), (unter, 0, 0)
        r["Ruecken"] = (lehne, 0, 0)
        r["Kopf"] = (22 - lehne, 0, 0)
        r["Oberschenkel_L"], r["Unterschenkel_L"] = (4, 0, 4), (6, 0, 0)
        r["Oberschenkel_R"] = (-26 - 14 * s, 0, -3)                     # Tretbrett vor dem rechten Fuß
        r["Unterschenkel_R"] = (34 + 10 * s, 0, 0)
    elif name == "Feinarbeit":                                          # sitzend am Tisch, kleine Bewegungen der Hände
        hoch = -(p["huefte"] - (p["knie"] - 0.02)) + 0.004 * s
        lehne = 14 + 2 * s
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (-86, 0, 6), (-86, 0, -6)
        r["Unterschenkel_L"], r["Unterschenkel_R"] = (82, 0, 0), (86, 0, 0)
        vor, hoehe = ARBEITSHOEHEN["Tisch"]
        ober, unter = arm_ik(p, lehne, hoch, vor + 0.02 * math.sin(6 * math.pi * phase), hoehe + 0.025 * max(0.0, s2))
        r["Oberarm_R"], r["Unterarm_R"] = (ober, 0, -14), (unter, 0, 0)
        ober, unter = arm_ik(p, lehne, hoch, vor - 0.03, hoehe + 0.01 * s)
        r["Oberarm_L"], r["Unterarm_L"] = (ober, 0, 14), (unter, 0, 0)
        r["Ruecken"] = (lehne, 0, 3 * s)
        r["Kopf"] = (30 - lehne, 0, 5 * math.sin(2 * math.pi * phase + 1.0))
    elif name == "Hueten":
        r["Oberarm_R"] = (-28, 0, -10)                                   # hält den Stab vor sich
        r["Unterarm_R"] = (-55, 0, 0)
        r["Oberarm_L"] = (3 * s, 0, 6)
        r["Unterarm_L"] = (-10, 0, 0)
        r["Ruecken"] = (2, 0, 6 * s)
        r["Kopf"] = (-2, 0, 14 * math.sin(2 * math.pi * phase + 0.6))
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (0, 0, 3 + 2 * s), (0, 0, -3)
        hoch = 0.004 * s
    return {k: tuple(g(w) for w in v) for k, v in r.items()}, hoch


BEWEGUNGEN = {   # Name: Dauer in Sekunden (eine Schleife)
    "Stehen": 3.0, "Gehen": 1.0, "Eilen": 0.7, "Gebeugt": 1.4, "Tragen": 1.2, "Sitzen": 3.0, "Reden": 2.0,
    "Hacken": 1.3, "Buecken": 2.4, "Hueten": 4.0, "Ziehen": 1.3, "Kutschieren": 1.0,
    "Haemmern": 0.9, "Blasebalg": 1.6, "Schleifen": 1.2, "Feinarbeit": 2.0,
}


def pose_setzen(ob, pose, hoch):
    for pb in ob.pose.bones:
        pb.rotation_euler = pose.get(pb.name, (0.0, 0.0, 0.0))
        pb.location = (0.0, 0.0, 0.0)
    # Die Wurzel zeigt nach oben (lokales Y = Welt-Z): Hüfthöhe über ihr lokales Y
    ob.pose.bones["Wurzel"].location = (0.0, hoch, 0.0)


def bewegungen_anlegen(ob, p):
    import bpy
    ob.animation_data_create()
    for name, dauer in BEWEGUNGEN.items():
        bilder = int(round(dauer * FPS))
        act = bpy.data.actions.new(name)
        ob.animation_data.action = act
        for f in range(0, bilder + 1, 2):
            pose, hoch = haltung(name, (f % bilder) / bilder, p)
            pose_setzen(ob, pose, hoch)
            for pb in ob.pose.bones:
                pb.keyframe_insert("rotation_euler", frame=f + 1)
            ob.pose.bones["Wurzel"].keyframe_insert("location", frame=f + 1)
        spur = ob.animation_data.nla_tracks.new()
        spur.name = name
        spur.strips.new(name, 1, act)
        ob.animation_data.action = None
    pose_setzen(ob, *haltung("Stehen", 0.0, p))


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen, Export, Vorschau
# ─────────────────────────────────────────────────────────────────────────────

def figur_bauen(volk, st):
    p = VOELKER[volk]
    ske = skelett(p)
    objekte = [ske]
    for frau in (False, True):
        t = Teile("Koerper_Frau" if frau else "Koerper_Mann")
        koerper(t, st, p, frau, volk == "zwerg")
        objekte.append(t.objekt(ske))
    for name in TRACHT:
        o = tracht(name, st, p).objekt(ske)
        o["teil"] = True
        o["sichtbar"] = False
        objekte.append(o)
    zonen_setzen([o for o in objekte if o.type == "MESH"])
    return ske, objekte


def figur_material():
    """Das eine Material „Figur“: Grundfarbe aus der Eckenfarbe (sRGB, für die Blender-Vorschau über Gamma 2,2 linear)."""
    import bpy
    m = bpy.data.materials.get("Figur")
    if m is not None:
        return m
    m = bpy.data.materials.new("Figur")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    baum = m.node_tree
    p = baum.nodes.get("Principled BSDF")
    p.inputs["Roughness"].default_value = 0.9
    p.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)      # glTF multipliziert Faktor und Eckenfarbe
    ecke = baum.nodes.new("ShaderNodeVertexColor")
    ecke.layer_name = "Farbe"
    gamma = baum.nodes.new("ShaderNodeGamma")
    gamma.inputs["Gamma"].default_value = 2.2
    baum.links.new(ecke.outputs["Color"], gamma.inputs["Color"])
    baum.links.new(gamma.outputs["Color"], p.inputs["Base Color"])
    m.diffuse_color = (0.6, 0.6, 0.6, 1.0)
    return m


def zonen_setzen(netze):
    """Ersetzt alle Materialien durch „Figur“: Farbe des alten Materials (sRGB aus FARBEN) als Eckenfarbe „Farbe“,
    Zone nach ZONEN als UV „Zone“ (x)."""
    figur = figur_material()
    for o in netze:
        me = o.data
        alte = [m.name if m else "" for m in me.materials]
        farbe = me.color_attributes.new("Farbe", "BYTE_COLOR", "CORNER")
        me.color_attributes.active_color = farbe
        me.color_attributes.render_color_index = me.color_attributes.find("Farbe")
        uv = me.uv_layers.new(name="Zone")
        werte, zonen = [1.0] * (len(me.loops) * 4), [0.0] * (len(me.loops) * 2)
        for poly in me.polygons:
            name = alte[poly.material_index] if poly.material_index < len(alte) else ""
            f = bs.FARBEN.get(name.split(".")[0], (1.0, 0.0, 1.0))
            z = float(ZONEN.get(name.split(".")[0], 0))
            for li in poly.loop_indices:
                werte[li * 4:li * 4 + 4] = [f[0], f[1], f[2], 1.0]
                zonen[li * 2] = z
        farbe.data.foreach_set("color", werte)
        uv.data.foreach_set("uv", zonen)
        me.materials.clear()
        me.materials.append(figur)
        me.polygons.foreach_set("material_index", [0] * len(me.polygons))
        me.update()


def exportieren(name, objekte):
    import bpy
    pfad = os.path.join(bs.MODELLE, name + ".glb")
    for o in bpy.context.scene.objects:
        o.select_set(o in objekte)
    bpy.ops.export_scene.gltf(
        filepath=pfad, export_format="GLB", use_selection=True, export_yup=True, export_apply=False,
        export_extras=True, export_normals=True, export_texcoords=True, export_skins=True, export_animations=True,
        export_animation_mode="NLA_TRACKS", export_morph=False, export_lights=False, export_cameras=False,
        export_image_format="NONE", export_vertex_color="ACTIVE", export_all_vertex_colors=False)
    return pfad


def bericht(volk, objekte):
    print(f"\nfigur_{volk}: Dreiecke")
    for o in objekte:
        if o.type == "MESH":
            o.data.calc_loop_triangles()
            print(f"  {o.name:14s} {len(o.data.loop_triangles):5d}")


def main():
    import bpy
    schnell = "--schnell" in sys.argv
    for volk in VOELKER:
        bs.neue_szene()
        st = bs.Stoffe()
        ske, objekte = figur_bauen(volk, st)
        bewegungen_anlegen(ske, VOELKER[volk])
        bericht(volk, objekte)
        print("geschrieben:", exportieren("figur_" + volk, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    if "--nur-bewegungen" not in sys.argv:
        vorschau_figuren(schnell)
    vorschau_bewegungen(schnell)


def kopie(ske, objekte, ort, dreh=0.0):
    """Skelett samt Körperteilen kopieren (fürs Bild) und an `ort` stellen."""
    import bpy
    neu = ske.copy()
    neu.data = ske.data
    neu.animation_data_clear()
    bpy.context.scene.collection.objects.link(neu)
    neu.location = ort
    neu.rotation_euler = (0, 0, dreh)
    teile = []
    for o in objekte:
        k = o.copy()
        k.data = o.data.copy()                    # eigene Materialliste je Kopie (fürs Umfärben)
        bpy.context.scene.collection.objects.link(k)
        k.parent = neu
        k.matrix_parent_inverse = Matrix.Identity(4)
        k.location = (0, 0, 0)
        k.modifiers["Skelett"].object = neu
        teile.append(k)
    return neu, teile


TRACHTEN_BILD = [   # Volk, Frau, Teile, Gewand, Kopfbedeckung – wie die Rollen im Spiel
    ("mensch", False, ["Barett"], (0.55, 0.12, 0.12), (0.12, 0.11, 0.12)),          # Ratsherr
    ("mensch", True, ["Haube", "Schuerze"], (0.58, 0.45, 0.30), (0.78, 0.74, 0.64)),  # Magd
    ("mensch", False, ["Kapuze"], (0.36, 0.24, 0.14), (0.36, 0.24, 0.14)),          # Mönch
    ("mensch", False, ["Strohhut", "Stab"], (0.40, 0.36, 0.24), (0.78, 0.66, 0.40)),  # Hirte
    ("zwerg", False, ["Kiepe"], (0.24, 0.42, 0.26), None),                           # Krämer
    ("zwerg", True, ["Haube"], (0.45, 0.22, 0.50), (0.78, 0.74, 0.64)),
    ("elb", False, ["Tasche"], (0.20, 0.32, 0.62), None),                            # Bote
    ("elb", True, ["Schleier"], (0.12, 0.11, 0.12), (0.92, 0.92, 0.88)),             # Nonne
    ("halbling", False, ["Strohhut", "Hacke"], (0.58, 0.50, 0.36), (0.78, 0.66, 0.40)),  # Feldarbeiter
    ("halbling", True, ["Buendel", "Haube"], (0.42, 0.30, 0.20), (0.78, 0.74, 0.64)),  # Holzsammlerin
]


def vorschau_figuren(schnell):
    """Alle Völker in typischer Tracht nebeneinander (je eigene Farben über Kopien der Materialien)."""
    import bpy
    bs.neue_szene()
    st = bs.Stoffe()
    figuren = {}
    for volk in VOELKER:
        figuren[volk] = figur_bauen(volk, st)
        for o in figuren[volk][1] + [figuren[volk][0]]:
            o.location.z -= 50                     # Vorlagen aus dem Bild
    alle = []
    for i, (volk, frau, teile, gewand, kopf) in enumerate(TRACHTEN_BILD):
        ske, objekte = figuren[volk]
        wahl = [o for o in objekte if o.type == "MESH" and o.name.split(".")[0] in teile + ["Koerper_Frau" if frau else "Koerper_Mann"]]
        x = (i - (len(TRACHTEN_BILD) - 1) / 2) * 0.9
        neu, kopien = kopie(ske, wahl, Vector((x * 0.7071, x * 0.7071, 0)))
        pose_setzen(neu, *haltung("Hueten" if "Stab" in teile else "Tragen" if "Buendel" in teile else "Stehen", 0.2,
                                  VOELKER[volk]))
        for k in kopien:
            umfaerben(k, {"Gewand": gewand, "Kopfbedeckung": kopf, "Haut": HAUT[volk], "Haar": HAAR[i % len(HAAR)],
                          "Beinkleid": BEINKLEID[i % len(BEINKLEID)]})
        alle += kopien
    bs.vorschau_buehne(None, ziel_hoehe=0.9, ausschnitt=9.2, schnell=schnell)
    print(bs.rendern("figuren", lambda o: o in alle))


HAAR = [(0.32, 0.22, 0.14), (0.62, 0.48, 0.26), (0.14, 0.12, 0.10), (0.55, 0.30, 0.16)]     # Beispielfarben fürs Bild
BEINKLEID = [(0.30, 0.26, 0.22), (0.24, 0.28, 0.36), (0.40, 0.34, 0.24)]
HAUT = {"mensch": (0.86, 0.68, 0.55), "zwerg": (0.80, 0.60, 0.48), "elb": (0.93, 0.85, 0.76), "halbling": (0.88, 0.70, 0.56)}


def umfaerben(o, farben):
    """Fürs Bild: die Eckenfarben der Zonen dieses Objekts (eigene Netzkopie) umfärben, wie es das Spiel je Figur
    mit seinem Shader tut. `farben`: {Zonenname: sRGB} (Namen wie in ZONEN)."""
    me = o.data
    uv = me.uv_layers["Zone"].data
    farbe = me.color_attributes["Farbe"].data
    nach_zone = {ZONEN[n]: f for n, f in farben.items() if f is not None}
    for poly in me.polygons:
        f = nach_zone.get(int(round(uv[poly.loop_indices[0]].uv[0])))
        if f is None:
            continue
        for li in poly.loop_indices:
            farbe[li].color = (f[0], f[1], f[2], 1.0)


def vorschau_bewegungen(schnell):
    """Je Bewegung eine Reihe: vier Phasen nebeneinander (Mensch, Mann; beim Hüten mit Stab, Tragen mit Bündel, Hämmern mit
    Hammer). Für die Werkstatt-Bewegungen stehen Amboss, Hebel, Schleifstein und Tisch dort, wo sie im Innenraum stehen.
    Die Beschriftung sind Textobjekte in der Szene (braucht kein Pillow)."""
    import bpy
    bs.neue_szene()
    st = bs.Stoffe()
    p = VOELKER["mensch"]
    ske, objekte = figur_bauen("mensch", st)
    for o in objekte:
        o.location.z -= 50
    reihen = list(BEWEGUNGEN)
    phasen = (0.0, 0.25, 0.5, 0.75)
    alle = []
    abstand = 2.7
    for r, name in enumerate(reihen):
        teile = {"Hueten": ["Stab"], "Tragen": ["Buendel"], "Hacken": ["Hacke", "Strohhut"], "Haemmern": ["Hammer"]}.get(name, [])
        wahl = [o for o in objekte if o.type == "MESH" and o.name.split(".")[0] in teile + ["Koerper_Mann"]]
        sy = ((len(reihen) - 1) / 2 - r) * abstand
        for k, ph in enumerate(phasen):
            # Spalten nach rechts im Bild, Reihen nach hinten
            sx = (k - 1.5) * 1.1
            ort = Vector((sx * 0.7071 - sy * 0.7071, sx * 0.7071 + sy * 0.7071, 0))
            neu, kopien = kopie(ske, wahl, ort, dreh=math.radians(-90))     # Seitenansicht: Blick nach links im Bild (−x)
            pose_setzen(neu, *haltung(name, ph, p))
            alle += kopien
            hilfe = arbeitsplatz(name, ort, st, p)
            if hilfe is not None:
                alle.append(hilfe)
        alle.append(beschriftung(name, Vector(((-2.3) * 0.7071 - sy * 0.7071, (-2.3) * 0.7071 + sy * 0.7071, 0.9))))
    bs.vorschau_buehne(None, ziel_hoehe=0.9, ausschnitt=22.0 * len(reihen) / 12, schnell=schnell)
    szene = bpy.context.scene
    szene.render.resolution_x, szene.render.resolution_y = 1000, int(1800 * len(reihen) / 12)
    print(bs.rendern("figuren-bewegungen", lambda o: o in alle))


def arbeitsplatz(name, ort, st, p):
    """Fürs Bild: was vor der Figur steht (Bank, Amboss, Hebel, Schleifstein, Tisch); die Figur blickt nach −x."""
    netz = bs.Netz("_Hilfe")
    holz, eisen = st["Holz"], st["Eisen"]
    if name in ("Sitzen", "Kutschieren", "Feinarbeit"):                  # Bank bzw. Hocker unter der Hüfte
        netz.quader(ort + Vector((0, 0, p["knie"] - 0.06)), (0.5, 0.5, 0.08), holz)
        for dx in (-0.2, 0.2):
            netz.quader(ort + Vector((dx, 0, (p["knie"] - 0.1) / 2)), (0.06, 0.4, p["knie"] - 0.1), holz)
    if name == "Haemmern":
        vor, _ = ARBEITSHOEHEN["Amboss"]
        netz.quader(ort + Vector((-vor, 0, 0.25)), (0.5, 0.5, 0.5), st["Holz alt"])
        netz.quader(ort + Vector((-vor, 0, 0.68)), (0.17, 0.5, 0.36), eisen)
    elif name == "Blasebalg":
        vor, hoehe = ARBEITSHOEHEN["Hebel"]
        netz.stab(ort + Vector((-vor, 0, hoehe)), ort + Vector((-vor - 0.9, 0, hoehe + 0.2)), 0.06, 0.06, holz)
        netz.stab(ort + Vector((-vor - 0.45, 0, 0)), ort + Vector((-vor - 0.45, 0, hoehe + 0.12)), 0.1, 0.1, holz)
    elif name == "Schleifen":
        bs.achteck(netz, st, ort + Vector((-0.6, 0.0, 0.0)), 0.06, 0.0, 0.72, "Holz")
        dreh = Matrix.Translation(ort + Vector((-0.6, 0, 0))) @ Matrix.Rotation(math.radians(90), 4, "Z")
        netz.prisma([(0.38 * math.cos(2 * math.pi * k / 12), 0.72 + 0.38 * math.sin(2 * math.pi * k / 12)) for k in range(12)],
                    -0.07, 0.07, st["Sandstein"], matrix=dreh)
        netz.quader(ort + Vector((-0.3, 0.09, 0.05)), (0.3, 0.2, 0.04), holz)            # Tretbrett vor dem rechten Fuß
    elif name == "Feinarbeit":
        vor, hoehe = ARBEITSHOEHEN["Tisch"]
        netz.quader(ort + Vector((-vor - 0.25, 0, hoehe - 0.07)), (0.62, 0.9, 0.06), holz)
        for dx in (-0.05, -0.5):
            netz.quader(ort + Vector((-vor - 0.25 + dx + 0.27, 0, (hoehe - 0.1) / 2)), (0.05, 0.8, hoehe - 0.1), holz)
    return netz.objekt() if netz.dreiecke() else None


def beschriftung(text, ort):
    """Fürs Bild: Text in der Szene, zur Spielkamera gedreht (Schrift des Spiels, Alegreya, OFL)."""
    import bpy
    kurve = bpy.data.curves.new("_Text_" + text, "FONT")
    kurve.body = text
    kurve.size = 0.62
    kurve.align_x, kurve.align_y = "RIGHT", "CENTER"
    try:
        kurve.font = bpy.data.fonts.load(os.path.join(bs.WURZEL, "game", "assets", "schriften", "Alegreya-Variabel.ttf"),
                                         check_existing=True)
    except RuntimeError:
        pass
    o = bpy.data.objects.new("_Text_" + text, kurve)
    o.visible_shadow = False
    o.location = ort
    o.rotation_euler = (math.radians(90 - bs.KAMERA_NEIGUNG), 0, math.radians(-45 + 90))   # wie die Kamera in bs.blick(0)
    m = bpy.data.materials.new("_Schrift")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    pr = m.node_tree.nodes["Principled BSDF"]
    pr.inputs["Base Color"].default_value = (0.87, 0.84, 0.75, 1)
    pr.inputs["Emission Color"].default_value = (0.87, 0.84, 0.75, 1)
    pr.inputs["Emission Strength"].default_value = 1.0
    kurve.materials.append(m)
    bpy.context.scene.collection.objects.link(o)
    return o


if __name__ == "__main__":
    main()
