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
    Trachtteile     Strohhut, Barett, Kapuze, Haube, Schleier, Schuerze, Kiepe, Buendel, Tasche, Stab, Hacke, Hammer,
                    Hobel, Saege, Drechseleisen, Schlaegel, Meissel
                    – standardmäßig aus, das Spiel blendet sie je Rolle ein (wie heute `TrachtVon`; das Werkzeug bei der
                    passenden Bewegung: Hammer beim Hämmern, Hobel beim Hobeln, Säge beim Sägen, Drechseleisen beim Drechseln,
                    Schlägel und Meißel beim Behauen und Bildhauen)
    Bewegungen      Stehen, Gehen, Eilen, Gebeugt, Tragen, Sitzen, Reden, Hacken, Buecken, Hueten,
                    Ziehen (Handkarren), Kutschieren (auf dem Bock),
                    Haemmern, Blasebalg, Schleifen (stehend), Feinarbeit (sitzend) – Arbeit in der Schmiede,
                    Hobeln, Saegen (Handsäge am Sägebock), Drechseln (ein Fuß tritt das Pedal), Gatter (Stamm nachschieben)
                    – Arbeit beim Tischler,
                    Behauen (Schlägel rechts, Meißel links am Werkblock), Bildhauen (feine Meißelarbeit an einer Figur in
                    Brust- bis Kopfhöhe) – Arbeit beim Steinmetz
                    – jede eine Schleife am Ort (ohne Vorwärtsbewegung; das Spiel schiebt die Figur)
                    Die Werkstatt-Bewegungen zielen mit den Händen (beim Drechseln auch mit dem rechten Fuß) auf feste
                    Höhen (ARBEITSHOEHEN, PEDAL – wie in schmied_innen.glb, tischler_innen.glb und steinmetz_innen.glb): Jedes Volk erreicht sie
                    mit eigenen Arm- und Beinwinkeln (`arm_ik`, `arm_ziel`, `bein_ik`).
Material: alle Teile (Körper, Trachtteile, Werkzeug) tragen nur ein Material „Figur“ – eine Figur ist so ein
Zeichenaufruf statt sechs. Je Ecke stehen zwei Angaben im Netz, je Fläche konstant:
    COLOR_0         Grundfarbe als sRGB-Wert (wie bei den Umlandbetrieben, siehe bausteine.farben_zusammenlegen)
    TEXCOORD_0      (Godot: UV) x = Zone, y = Materialart für den Holzschnitt-Look (bausteine.materialart):  0 feste Farbe aus COLOR_0 (Auge, Leder, Holz, Eisen, Leinen …),
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
TRACHT = ["Strohhut", "Barett", "Kapuze", "Haube", "Schleier", "Schuerze", "Kiepe", "Buendel", "Tasche", "Stab", "Hacke", "Hammer",
          "Hobel", "Saege", "Drechseleisen", "Schlaegel", "Meissel"]
HAMMER = 0.3          # vom Mittelpunkt der Hand bis zur Mitte des Hammerkopfs, in Verlängerung des Unterarms
# Wo die Hände bei der Arbeit sind (Meter vor dem Fußpunkt der Figur, Höhe über dem Boden) – passend zu den Ankern
# im Innenraum: Amboss 0,68 m vor der Figur, Bahn 0,86 m hoch; Hebel des Blasebalgs 0,42 m vor ihr, 1,36 m hoch;
# Schleifstein (Mitte 0,6 m vor ihr, 0,72 m hoch, Halbmesser 0,38); Feinwerktisch 0,77 m hoch.
ARBEITSHOEHEN = {"Amboss": (0.68, 0.90), "Zange": (0.52, 0.93), "Hebel": (0.42, 1.36), "Stein": (0.36, 0.99),
                 "Tisch": (0.44, 0.81),
                 # Tischler (tischler_innen.glb): Hobelbank 0,86 m, Brett 4 cm, Griff des Hobels 0,98 m, 0,5 m vor der Figur;
                 # Oberkante des Stamms auf dem Sägebock 1,02 m, 0,55 m vor ihr; Werkstück der Drehbank 0,5 m vor ihr auf
                 # 1,05 m (Auflage knapp davor); Stirn des Stamms im Gatter 0,5 m vor ihr, Achse 0,9 m
                 "Hobel": (0.5, 0.98), "Bock": (0.55, 1.02), "Drehbank": (0.46, 1.05), "Stamm": (0.5, 0.9),
                 # Steinmetz (steinmetz_innen.glb): Schneide des Meißels auf der Oberseite des Werkblocks (0,86 m), 0,46 m vor der
                 # Figur; an der Steinfigur in der Bauhütte vor der Brust (Mitte der feinen Arbeit)
                 "Block": (0.46, 0.86), "Figur": (0.52, 1.22)}
PEDAL = (0.2, 0.345, 0.09)   # Pedal der Drehbank unter dem rechten Fuß: vor der Figur, Höhe der Oberseite oben und getreten
SAEGE = (0.2, 0.33)          # Säge: Abstand von der Mitte der Hand bis zur Oberkante des Stamms beim Stoß und beim Zug
KLUEPFEL_R = 0.062           # Halbmesser des Kopfs: Seine Seite trifft den Meißel
FAUST_R = (-0.17, -0.16, 0.07)   # beim Behauen: rechte Hand gegenüber dem Kopf des Meißels (seitlich, vor, Höhe), vor der Brust
MEISSEL = (0.12, 0.13)       # Meißel: von der Mitte der linken Hand bis zur Schneide und bis zum Kopf (Schlagfläche)
MEISSEL_RICHTUNG = (0.0, 0.25, -1.0)   # beim Behauen vom Kopf zur Schneide (seitlich, vor, Höhe): fast senkrecht, leicht nach vorn
BILDHAUEN_RICHTUNG = (0.0, 0.6, -0.8)  # beim Bildhauen (vorläufig, wird gemessen)


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
    elif name == "Saege":
        t.netz.alle = True                # dreht sich mit dem Arm: auch Flächen, die in Ruhe nach unten zeigen
        with t.an("Unterarm_R") as n:     # Blatt verlängert den Unterarm (in Ruhe nach unten), die Zähne auf der Seite +y
            zaehne = [q for k in range(10) for q in ((0.045, -0.62 + 0.055 * k), (0.063, -0.5925 + 0.055 * k))]   # (y, z)
            n.prisma([(-0.06, -0.07), (-0.015, -0.62)] + zaehne + [(0.045, -0.07)], -0.003, 0.003, st["Eisen"],
                     matrix=Matrix.Translation((xr, 0, hand_z)))
            n.quader((xr, 0.0, hand_z + 0.01), (0.03, 0.11, 0.13), st["Holz"])                   # Griff um die Hand
    return t


REQUISITEN = {"Hobel": ("Hobeln", 0.3), "Drechseleisen": ("Drechseln", 0.5), "Meissel": ("Behauen", 0.55),
              "Schlaegel": ("Behauen", 0.55)}   # Bewegung, Phase


def requisit(name, st, p, ske):
    """Werkzeug in der Hand für eine Bewegung (REQUISITEN): in deren Haltung gebaut – Hobel flach auf dem Brett,
    Drechseleisen von der Hand zum Werkstück, Meißel (linke Hand) mit der Schneide auf dem Block, Klüpfel mit dem Kopf auf dem
    Meißel – und mit der Lage des Unterarms in die Ruhelage zurückgerechnet. Es hängt starr am Unterarm und folgt ihm durch
    die ganze Bewegung."""
    import bpy
    bewegung, phase = REQUISITEN[name]
    seite = "L" if name == "Meissel" else "R"
    pose_setzen(ske, *haltung(bewegung, phase, p))
    bpy.context.view_layer.update()
    knochen = f"Unterarm_{seite}"
    hin = ske.pose.bones[knochen].matrix @ ske.data.bones[knochen].matrix_local.inverted()   # Ruhelage → Haltung
    zurueck = hin.inverted()
    hand_z = p["schulter"] - 0.03 - p["oberarm"] - p["unterarm"] * 0.72 - 0.06
    x = (1 if seite == "L" else -1) * (p["schulterbreite"] + 0.01)
    hand = hin @ Vector((x, 0, hand_z))                                        # Mitte der Hand in der Haltung
    holz, eisen = st["Holz"], st["Eisen"]
    t = Teile(name)
    t.netz.alle = True                            # dreht sich mit dem Arm: auch Flächen, die in Ruhe nach unten zeigen
    with t.an(knochen) as n:
        if name == "Hobel":                       # Sohle 8 cm unter der Hand, Stoß nach links (+x), Griff unter der Hand
            M = zurueck @ Matrix.Translation(hand)
            n.quader((0.09, 0, -0.045), (0.26, 0.065, 0.07), holz, M)
            n.quader((0.0, 0, 0.0), (0.06, 0.03, 0.03), st["Holz alt"], M)                       # Griff
            n.quader((0.19, 0, -0.0), (0.035, 0.035, 0.03), st["Holz alt"], M)                   # Nase
            n.quader((0.07, 0, -0.005), (0.02, 0.05, 0.015), eisen, M)                           # Eisen und Keil
        elif name == "Schlaegel":                 # Klüpfel: Stiel quer durch die Faust bis zum runden Kopf auf dem Meißel
            ziel = _blender(meissel_kopf(p, *haltung(bewegung, phase, p))) + Vector((0, 0, KLUEPFEL_R + 0.004))
            d = (ziel - hand).normalized()
            quer = zurueck.to_3x3() @ Vector((0, 0, 1))
            n.stab(zurueck @ (hand - d * 0.06), zurueck @ (ziel - d * 0.05), 0.032, 0.032, holz, quer=quer)
            ring = [(0.0, 0.62), (0.05, 0.9), (0.115, 1.0), (0.14, 0.9)]          # Kopf: wird zum Ende hin dicker
            achse = zurueck.to_3x3() @ d
            for (u0, f0), (u1, f1) in zip(ring, ring[1:]):
                drehteil(n, zurueck @ (ziel + d * (u0 - 0.07)), achse, u1 - u0, KLUEPFEL_R * f0, KLUEPFEL_R * f1,
                         st["Holz alt"], deckel=(u0 == 0.0, u1 == 0.14))
        elif name == "Meissel":                   # Eisen durch die Faust: Kopf oben, Schneide auf dem Stein (vorn = −y)
            a = Vector((MEISSEL_RICHTUNG[0], -MEISSEL_RICHTUNG[1], MEISSEL_RICHTUNG[2])).normalized()
            kopf, schneide = hand - a * MEISSEL[1], hand + a * MEISSEL[0]
            quer = zurueck.to_3x3() @ Vector((1, 0, 0))
            n.stab(zurueck @ kopf, zurueck @ (kopf + a * 0.03), 0.03, 0.03, eisen, quer=quer)            # Kopf, breitgeschlagen
            n.stab(zurueck @ (kopf + a * 0.03), zurueck @ (schneide - a * 0.04), 0.019, 0.019, eisen, quer=quer)
            n.stab(zurueck @ (schneide - a * 0.04), zurueck @ schneide, 0.026, 0.008, eisen, quer=quer)  # Schneide
        else:                                     # Drechseleisen: Heft hinter der Hand, Klinge bis an das Werkstück
            vor, hoehe = ARBEITSHOEHEN["Drehbank"]
            spitze = Vector((0.02, -(vor - 0.01), hoehe))
            achse = (spitze - hand).normalized()
            quer = zurueck.to_3x3() @ Vector((0, 0, 1))
            n.stab(zurueck @ (hand - achse * 0.13), zurueck @ (hand + achse * 0.06), 0.035, 0.035, holz, quer=quer)
            n.stab(zurueck @ (hand + achse * 0.06), zurueck @ (hand + achse * 0.085), 0.045, 0.045, eisen, quer=quer)
            n.stab(zurueck @ (hand + achse * 0.085), zurueck @ spitze, 0.022, 0.012, eisen, quer=quer)
    pose_setzen(ske, {}, 0.0)
    bpy.context.view_layer.update()
    return t


def drehteil(netz, fuss, achse, laenge, r0, r1, m, n=8, deckel=(True, True)):
    """Kegelstumpf von `fuss` entlang `achse` (Länge `laenge`, Halbmesser r0 → r1), n-eckig, auf Wunsch mit Deckeln."""
    achse = Vector(achse).normalized()
    u = achse.orthogonal().normalized()
    v = achse.cross(u)
    ringe = [[fuss + achse * h + (u * math.cos(2 * math.pi * k / n) + v * math.sin(2 * math.pi * k / n)) * r for k in range(n)]
             for h, r in ((0.0, r0), (laenge, r1))]
    for k in range(n):
        j = (k + 1) % n
        quad = [ringe[0][k], ringe[0][j], ringe[1][j], ringe[1][k]]
        netz.vieleck_nach(quad, sum(quad, Vector()) / 4 - (fuss + achse * laenge / 2), m)
    if deckel[0]:
        netz.vieleck_nach(ringe[0], -achse, m)
    if deckel[1]:
        netz.vieleck_nach(ringe[1], achse, m)


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


def arm_ziel(p, lehne, hoch, seite, ziel, drehung=0.0, extra=0.0):
    """Winkel (Grad) für Oberarm (X, Y) und Unterarm (X), damit die Hand der Seite „L“ oder „R“ bei `ziel` = (seitlich, vor,
    hoehe) liegt – seitlich: + zur linken Seite der Figur, vor und hoehe ab dem Fußpunkt. Der Rumpf ist um `lehne` Grad nach
    vorn geneigt und um `drehung` Grad nach links gedreht (Ruecken Y). Wie arm_ik, nur schwenkt der Oberarm dazu um die
    Hochachse (Y: + schwenkt die Hand nach rechts), statt in der Ebene des Rumpfs zu bleiben."""
    x, v, h = ziel
    w = math.radians(drehung)
    xt, vt = x * math.cos(w) - v * math.sin(w), x * math.sin(w) + v * math.cos(w)     # im gedrehten Rumpf
    rumpf = p["schulter"] - 0.03 - p["huefte"]
    xs = (1 if seite == "L" else -1) * (p["schulterbreite"] + 0.01)
    vs = rumpf * math.sin(math.radians(lehne))
    dx, dv = xt - xs, vt - vs
    ober, unter = arm_ik(p, lehne, hoch, vs + math.hypot(dx, dv), h, extra)
    return (ober, -math.degrees(math.atan2(dx, dv)), 0.0), (unter, 0.0, 0.0)


def bein_ik(p, hoch, vor, hoehe):
    """Winkel (Grad) für Oberschenkel und Unterschenkel (X), damit die Sohle unter dem Knöchel `vor` Meter vor dem Fußpunkt auf
    `hoehe` steht; das Knie knickt nach vorn. Zu weit weg: das Bein zeigt gestreckt dorthin."""
    l1, l2 = p["huefte"] - p["knie"], p["knie"] - 0.06
    dz = hoehe + 0.06 - (p["huefte"] + hoch)
    d = min(math.hypot(vor, dz), l1 + l2 - 1e-4)
    a = math.atan2(vor, -dz) + math.acos(max(-1.0, min(1.0, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
    b = math.atan2(vor - l1 * math.sin(a), -(dz + l1 * math.cos(a)))
    return -math.degrees(a), math.degrees(a) - math.degrees(b)


def weich(t):
    """0 → 1 mit sanftem Anfang und Ende."""
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def tritt(phase):
    """Pedal der Drehbank: 0 oben, 1 getreten – in 40 % des Takts hinunter, dann zieht die Federstange es zurück
    (wie tischler_innen.tritt_anteil, damit Fuß und Pedal im selben Takt gehen)."""
    if phase < 0.4:
        return 0.5 - 0.5 * math.cos(math.pi * phase / 0.4)
    return 0.5 + 0.5 * math.cos(math.pi * (phase - 0.4) / 0.6)


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
    elif name == "Hobeln":                                              # an der Hobelbank, Hobel nach links schieben
        # Stoß (0–0,6): der Hobel wandert 0,4 m nach links, der Rumpf dreht und neigt sich mit; zurück (0,6–1) leicht angehoben
        if phase < 0.6:
            a, heben = weich(phase / 0.6), 0.0
        else:
            a, heben = 1 - weich((phase - 0.6) / 0.4), 0.03 * math.sin(math.pi * (phase - 0.6) / 0.4)
        vor, hoehe = ARBEITSHOEHEN["Hobel"]
        u = -0.17 + 0.4 * a
        lehne, drehung = 16 + 8 * a, -8 + 22 * a
        hoch = -0.02 - 0.02 * a
        r["Oberarm_R"], r["Unterarm_R"] = arm_ziel(p, lehne, hoch, "R", (u - 0.06, vor, hoehe + heben), drehung)
        r["Oberarm_L"], r["Unterarm_L"] = arm_ziel(p, lehne, hoch, "L", (u + 0.13, vor + 0.02, hoehe + 0.01 + heben), drehung)
        r["Ruecken"] = (lehne, drehung, 0)
        r["Kopf"] = (24 - lehne, -0.4 * drehung, 0)
        r["Oberschenkel_L"], r["Unterschenkel_L"] = (-12 - 6 * a, 0, 6), (14 + 4 * a, 0, 0)
        r["Oberschenkel_R"], r["Unterschenkel_R"] = (6 + 4 * a, 0, -4), (8, 0, 0)
    elif name == "Saegen":                                              # Handsäge am Sägebock, die Linke hält den Stamm
        # Das Blatt verlängert den Unterarm (wie der Hammer) und liegt immer auf der Oberkante des Stamms; die Hand geht vor
        # und zurück, der Abstand Hand–Stamm wechselt zwischen SAEGE[0] (Stoß) und SAEGE[1] (Zug)
        stoss = 0.5 - 0.5 * math.cos(2 * math.pi * phase)               # 0 Zug … 1 Stoß … 0
        vor, hoehe = ARBEITSHOEHEN["Bock"]
        lehne = 24 + 6 * stoss
        hoch = -0.03
        extra = SAEGE[1] + (SAEGE[0] - SAEGE[1]) * stoss
        r["Oberarm_R"], r["Unterarm_R"] = arm_ziel(p, lehne, hoch, "R", (-0.2, vor, hoehe), extra=extra)
        r["Oberarm_L"], r["Unterarm_L"] = arm_ziel(p, lehne, hoch, "L", (0.2, vor - 0.05, hoehe + 0.03))
        r["Ruecken"] = (lehne, -6, 0)
        r["Kopf"] = (30 - lehne, 4, 0)
        r["Oberschenkel_L"], r["Unterschenkel_L"] = (-16, 0, 6), (18, 0, 0)
        r["Oberschenkel_R"], r["Unterschenkel_R"] = (10 + 3 * stoss, 0, -6), (6, 0, 0)
    elif name == "Drechseln":                                           # an der Wippdrehbank: rechter Fuß tritt, Hände führen das Eisen
        d = tritt(phase)
        vor, hoehe = ARBEITSHOEHEN["Drehbank"]
        lehne = 12 + 3 * d
        hoch = -0.015 - 0.015 * d
        quer = 0.02 * math.sin(2 * math.pi * phase)                     # das Eisen wandert ein wenig am Werkstück entlang
        r["Oberarm_L"], r["Unterarm_L"] = arm_ziel(p, lehne, hoch, "L", (0.04 + quer, vor - 0.07, hoehe - 0.015))
        r["Oberarm_R"], r["Unterarm_R"] = arm_ziel(p, lehne, hoch, "R", (-0.06 + quer, vor - 0.27, hoehe - 0.07))
        r["Ruecken"] = (lehne, 4, 0)
        r["Kopf"] = (26 - lehne, -3, 0)
        pv, oben, unten = PEDAL
        ober, unter = bein_ik(p, hoch, pv, unten + (oben - unten) * (1 - d))
        r["Oberschenkel_R"], r["Unterschenkel_R"] = (ober, 0, -3), (unter, 0, 0)
        r["Oberschenkel_L"], r["Unterschenkel_L"] = (2, 0, 4), (6 + 4 * d, 0, 0)
    elif name == "Gatter":                                              # Stamm nachschieben: beide Hände an der Stirn
        if phase < 0.45:
            a = weich(phase / 0.45)
        elif phase < 0.65:
            a = 1.0
        else:
            a = 1 - weich((phase - 0.65) / 0.35)
        vor, hoehe = ARBEITSHOEHEN["Stamm"]
        lehne = 14 + 24 * a
        hoch = -0.02 - 0.05 * a
        r["Oberarm_R"], r["Unterarm_R"] = arm_ziel(p, lehne, hoch, "R", (-0.12, vor, hoehe - 0.04))
        r["Oberarm_L"], r["Unterarm_L"] = arm_ziel(p, lehne, hoch, "L", (0.12, vor, hoehe + 0.05))
        r["Ruecken"] = (lehne, 0, 0)
        r["Kopf"] = (26 - lehne * 0.7, 0, 0)
        r["Oberschenkel_L"], r["Unterschenkel_L"] = (-18 - 10 * a, 0, 5), (18 + 14 * a, 0, 0)
        r["Oberschenkel_R"], r["Unterschenkel_R"] = (6 + 20 * a, 0, -5), (8 - 4 * a, 0, 0)
    elif name in ("Behauen", "Bildhauen"):                              # Meißel in der Linken, der Klüpfel schlägt auf seinen Kopf
        r.update(meisseln(name, phase, p))
        hoch = r.pop("_hoch")[0]
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


def _ruhe(kopf, unten):
    """Ruhelage eines Knochens in der Armatur, wie Blender sie anlegt (Rolle 0): Y entlang des Knochens (senkrecht nach oben
    oder unten), X nach links der Figur, Kopf des Knochens als Ort."""
    y = Vector((0, 0, -1 if unten else 1))
    x = Vector((1, 0, 0))
    m = Matrix((x, y, x.cross(y))).transposed().to_4x4()
    m.translation = kopf
    return m


def unterarm_lage(p, pose, hoch, seite):
    """Matrix, die einen Punkt am Unterarm der Seite „L“/„R“ aus der Ruhelage in die Haltung bringt – dieselbe Rechnung wie
    Blender (Wurzel, Ruecken, Oberarm, Unterarm; Euler XYZ im Knochen). `pose`: {Knochen: (x, y, z) im Bogenmaß} wie aus
    `haltung`, `hoch`: Hüfte. Koordinaten der Figur in Blender (Blick −Y)."""
    from mathutils import Euler
    sc, hu = p["schulter"], p["huefte"]
    xs = (1 if seite == "L" else -1) * (p["schulterbreite"] + 0.01)
    kette = (("Wurzel", (0, 0, hu), False), ("Ruecken", (0, 0, hu), False), (f"Oberarm_{seite}", (xs, 0, sc - 0.03), True),
             (f"Unterarm_{seite}", (xs, 0, sc - 0.03 - p["oberarm"]), True))
    m, vorher = Matrix.Identity(4), None
    for name, kopf, unten in kette:
        ruhe = _ruhe(Vector(kopf), unten)
        lokal = Euler(pose.get(name, (0.0, 0.0, 0.0)), "XYZ").to_matrix().to_4x4()
        if name == "Wurzel":
            lokal = Matrix.Translation((0, hoch, 0)) @ lokal
        m = (ruhe if vorher is None else m @ vorher.inverted() @ ruhe) @ lokal
        vorher = ruhe
    return m @ vorher.inverted()


def hand_ruhe(p, seite, extra=0.0):
    """Mitte der Hand in der Ruhelage (Blender), mit `extra` Metern Werkzeug in Verlängerung des Unterarms dessen Ende."""
    hand_z = p["schulter"] - 0.03 - p["oberarm"] - p["unterarm"] * 0.72 - 0.06
    return Vector(((1 if seite == "L" else -1) * (p["schulterbreite"] + 0.01), 0, hand_z - extra))


def _figur(v):
    """Blender-Koordinaten der Figur → (seitlich, vor, Höhe) wie in ARBEITSHOEHEN."""
    return Vector((v.x, -v.y, v.z))


def _blender(v):
    """(seitlich, vor, Höhe) → Blender-Koordinaten der Figur (Blick −Y)."""
    return Vector((v[0], -v[1], v[2]))


def arm_genau(p, pose, hoch, seite, ziel, extra=0.0):
    """Wie arm_ziel, aber genau: Die Hand (mit `extra` Metern Werkzeug dessen Ende) liegt bei `ziel` (seitlich, vor, Höhe).
    arm_ziel rechnet den Arm vereinfacht (bei stark gebeugtem Arm liegt die Hand einige Zentimeter daneben); hier wird das
    Ziel verschoben, bis die nachgerechnete Hand (unterarm_lage) trifft. `pose`: die übrigen Winkel in Grad (Ruecken: Neigung,
    Drehung). Gibt die Winkel (Grad) für Oberarm und Unterarm zurück."""
    lehne, drehung = pose["Ruecken"][0], pose["Ruecken"][1]
    soll, z = Vector(ziel), Vector(ziel)
    for _ in range(8):
        ober, unter = arm_ziel(p, lehne, hoch, seite, tuple(z), drehung, extra)
        winkel = _bogen({**pose, f"Oberarm_{seite}": ober, f"Unterarm_{seite}": unter})
        fehler = soll - _figur(unterarm_lage(p, winkel, hoch, seite) @ hand_ruhe(p, seite, extra))
        if fehler.length < 1e-4:
            break
        z += fehler
    return ober, unter


_MEISSEL = {}


def meissel_ruhe(p):
    """Schneide und Kopf des Meißels in der Ruhelage des linken Unterarms (Blender): Der Meißel ist in der Haltung „Behauen“
    beim Schlag gebaut (REQUISITEN) und hängt starr am Unterarm – so lässt sich seine Lage in jeder Haltung nachrechnen."""
    schluessel = tuple(sorted(p.items()))
    if schluessel not in _MEISSEL:
        bewegung, phase = REQUISITEN["Meissel"]
        r, hoch, _, _ = _meissel_haltung(bewegung, phase, p)
        lage = unterarm_lage(p, _bogen(r), hoch, "L")
        zurueck = lage.inverted()
        hand = lage @ hand_ruhe(p, "L")
        a = Vector((MEISSEL_RICHTUNG[0], -MEISSEL_RICHTUNG[1], MEISSEL_RICHTUNG[2])).normalized()
        _MEISSEL[schluessel] = (zurueck @ (hand + a * MEISSEL[0]), zurueck @ (hand - a * MEISSEL[1]))
    return _MEISSEL[schluessel]


def _bogen(winkel):
    """{Knochen: Winkel in Grad} → im Bogenmaß (wie haltung sie liefert)."""
    return {k: tuple(math.radians(w) for w in v) for k, v in winkel.items()}


def meissel_kopf(p, winkel, hoch):
    """Kopf (Schlagfläche) des Meißels in einer Haltung (Winkel im Bogenmaß wie aus haltung) als (seitlich, vor, Höhe)."""
    return _figur(unterarm_lage(p, winkel, hoch, "L") @ meissel_ruhe(p)[1])


_SCHLAEGEL = {}


def schlaegel_ruhe(p):
    """Mitte des Klüpfelkopfs in der Ruhelage des rechten Unterarms (Blender), wie meissel_ruhe."""
    schluessel = tuple(sorted(p.items()))
    if schluessel not in _SCHLAEGEL:
        bewegung, phase = REQUISITEN["Schlaegel"]
        winkel, hoch = haltung(bewegung, phase, p)
        lage = unterarm_lage(p, winkel, hoch, "R")
        ziel = _blender(meissel_kopf(p, winkel, hoch)) + Vector((0, 0, KLUEPFEL_R + 0.004))
        _SCHLAEGEL[schluessel] = lage.inverted() @ ziel
    return _SCHLAEGEL[schluessel]


def _meissel_haltung(name, phase, p):
    """Rumpf, Beine und linker Arm mit dem Meißel für meisseln: (Winkel in Grad, Hüfte, Ausholen 0…1, Ausholwinkel des
    rechten Arms)."""
    r = {}
    if name == "Behauen":
        if phase < 0.45:
            a = weich(phase / 0.45)
        elif phase < 0.55:
            a = 1 - ((phase - 0.45) / 0.1) ** 2
        elif phase < 0.65:
            a = 0.08 * math.sin(math.pi * (phase - 0.55) / 0.1)
        else:
            a = 0.0
        vor, hoehe = ARBEITSHOEHEN["Block"]
        lehne, hoch = 20 + 2 * a, -0.03
        schneide = Vector((0.03 + 0.012 * math.sin(2 * math.pi * phase), vor, hoehe))
        ausholen = (-78 * a, -36 * a)
        r["Ruecken"] = (lehne, 0, 0)
        r["Kopf"] = (30 - lehne, 0, 3 - 3 * a)
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (-12, 0, 5), (8, 0, -5)
        r["Unterschenkel_L"], r["Unterschenkel_R"] = (16, 0, 0), (10, 0, 0)
        richtung = Vector(MEISSEL_RICHTUNG).normalized()
        r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", tuple(schneide - richtung * MEISSEL[0]))
    else:
        halb = (2 * phase) % 1
        if halb < 0.55:
            a = weich(halb / 0.55)
        elif halb < 0.65:
            a = 1 - ((halb - 0.55) / 0.1) ** 2
        elif halb < 0.75:
            a = 0.06 * math.sin(math.pi * (halb - 0.65) / 0.1)
        else:
            a = 0.0
        vor, hoehe = ARBEITSHOEHEN["Figur"]
        lehne, hoch = 7 + 1.5 * math.sin(2 * math.pi * phase), -0.01
        schneide = Vector((0.02 + 0.03 * math.sin(2 * math.pi * phase), vor, hoehe + 0.05 * math.sin(2 * math.pi * phase + 1.2)))
        ausholen = (-18 * a, -30 * a)
        r["Ruecken"] = (lehne, 0, 0)
        r["Kopf"] = (8 - lehne + 40 * (schneide.z - hoehe), 0, 4 * math.sin(2 * math.pi * phase))
        r["Oberschenkel_L"], r["Oberschenkel_R"] = (-6, 0, 5), (5, 0, -5)
        r["Unterschenkel_L"], r["Unterschenkel_R"] = (8, 0, 0), (6, 0, 0)
        # Die Hand so führen, dass die Schneide (starr in der Faust) an der Figur liegt
        hand = schneide - Vector(BILDHAUEN_RICHTUNG).normalized() * MEISSEL[0]
        spitze_ruhe = meissel_ruhe(p)[0]
        for _ in range(6):
            r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", tuple(hand))
            winkel = _bogen(r)
            hand += schneide - _figur(unterarm_lage(p, winkel, hoch, "L") @ spitze_ruhe)
    return r, hoch, a, ausholen


def meisseln(name, phase, p):
    """Behauen am Werkblock und Bildhauen an der Steinfigur: {Knochen: Winkel in Grad}, dazu „_hoch“ (Hüfte).
    Die Linke hält den Meißel (MEISSEL, starr am Unterarm, gebaut in der Haltung „Behauen“ beim Schlag) mit der Schneide auf
    dem Stein, die Rechte schlägt mit der Seite des Klüpfelkopfs auf den Kopf des Meißels. Behauen: Ausholen (0–0,45),
    Schlag (0,45–0,55), Rückprall, Ruhe; die Schneide wandert dabei ein wenig über den Block. Bildhauen: zwei kurze Schläge je
    Schleife, aufrecht, die Linke führt den Meißel vor der Brust langsam höher und tiefer, nach links und rechts.
    Beide Arme mit arm_genau: Die Schneide sitzt auf dem Stein, der Klüpfel trifft den Kopf des Meißels."""
    r, hoch, a, ausholen = _meissel_haltung(name, phase, p)
    # Der Klüpfel (starr in der rechten Faust, gebaut beim Schlag des Behauens) trifft mit der Seite seines Kopfs den Kopf des
    # Meißels: beim Behauen von oben – dort steht die Faust fest vor der Brust (FAUST_R) –, beim Bildhauen (der Meißel liegt
    # flacher) von rechts hinten; dort wird die Faust so geführt, dass der Kopf trifft.
    kopf = meissel_kopf(p, _bogen(r), hoch)
    if name == "Behauen":
        (ox, oy, oz), (ux, uy, uz) = arm_genau(p, r, hoch, "R", tuple(kopf + Vector(FAUST_R)))
    else:
        spitze = _figur(unterarm_lage(p, _bogen(r), hoch, "L") @ meissel_ruhe(p)[0])
        anschlag = ((kopf - spitze).normalized() + Vector((-0.9, 0, 0))).normalized()
        soll = kopf + anschlag * (KLUEPFEL_R + 0.004)
        klopfer_ruhe = schlaegel_ruhe(p)
        faust = soll + Vector(FAUST_R) - Vector((0, 0, KLUEPFEL_R + 0.004))
        for _ in range(24):                     # gedämpft: Der Klüpfel dreht sich mit dem Unterarm, sonst schwingt es hin und her
            (ox, oy, oz), (ux, uy, uz) = arm_genau(p, r, hoch, "R", tuple(faust))
            winkel = _bogen({**r, "Oberarm_R": (ox, oy, oz), "Unterarm_R": (ux, uy, uz)})
            fehler = soll - _figur(unterarm_lage(p, winkel, hoch, "R") @ klopfer_ruhe)
            if fehler.length < 2e-4:
                break
            faust += fehler * 0.4
    r["Oberarm_R"] = (ox + ausholen[0], oy * (1 - 0.8 * a), oz + 14 * a)       # beim Ausholen nach rechts oben, nicht vors Gesicht
    r["Unterarm_R"] = (ux + ausholen[1], uy, uz)
    r["_hoch"] = (hoch, 0, 0)
    return r


BEWEGUNGEN = {   # Name: Dauer in Sekunden (eine Schleife)
    "Stehen": 3.0, "Gehen": 1.0, "Eilen": 0.7, "Gebeugt": 1.4, "Tragen": 1.2, "Sitzen": 3.0, "Reden": 2.0,
    "Hacken": 1.3, "Buecken": 2.4, "Hueten": 4.0, "Ziehen": 1.3, "Kutschieren": 1.0,
    "Haemmern": 0.9, "Blasebalg": 1.6, "Schleifen": 1.2, "Feinarbeit": 2.0,
    "Hobeln": 1.2, "Saegen": 1.0, "Drechseln": 1.0, "Gatter": 2.0,
    "Behauen": 1.0, "Bildhauen": 1.6,
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
        o = (requisit(name, st, p, ske) if name in REQUISITEN else tracht(name, st, p)).objekt(ske)
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
        bs.arten_setzen(me, alte, uv_name="Zone")                  # Holzschnitt: Struktur je Fläche als UV.y
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
        teile = {"Hueten": ["Stab"], "Tragen": ["Buendel"], "Hacken": ["Hacke", "Strohhut"], "Haemmern": ["Hammer"],
                 "Hobeln": ["Hobel"], "Saegen": ["Saege"], "Drechseln": ["Drechseleisen"], "Behauen": ["Schlaegel", "Meissel"],
                 "Bildhauen": ["Schlaegel", "Meissel"]}.get(name, [])
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
    szene.render.image_settings.color_mode = "RGB"                      # ohne Alphakanal und stark gepackt: kleinere Datei
    szene.render.image_settings.compression = 100
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
    elif name == "Hobeln":                                              # Hobelbank quer vor der Figur, Brett darauf
        vor, hoehe = ARBEITSHOEHEN["Hobel"]
        netz.quader(ort + Vector((-vor - 0.12, 0, 0.81)), (0.6, 1.6, 0.1), holz)
        netz.quader(ort + Vector((-vor, 0, 0.88)), (0.22, 1.4, 0.04), st["Schnittholz"])
        for dy in (-0.65, 0.65):
            netz.quader(ort + Vector((-vor - 0.12, dy, 0.38)), (0.5, 0.08, 0.76), holz)
    elif name == "Saegen":                                              # Stamm quer auf einem Bock
        vor, hoehe = ARBEITSHOEHEN["Bock"]
        netz.stab(ort + Vector((-vor, -0.9, hoehe - 0.17)), ort + Vector((-vor, 0.9, hoehe - 0.17)), 0.34, 0.34, st["Holz alt"])
        for dy in (-0.55, 0.55):
            netz.quader(ort + Vector((-vor, dy, 0.34)), (0.4, 0.08, 0.68), holz)
    elif name == "Drechseln":                                           # Bett, Werkstück, Auflage, Pedal (halb getreten)
        vor, hoehe = ARBEITSHOEHEN["Drehbank"]
        netz.stab(ort + Vector((-vor - 0.04, -0.4, hoehe)), ort + Vector((-vor - 0.04, 0.4, hoehe)), 0.09, 0.09, st["Schnittholz"])
        netz.quader(ort + Vector((-vor - 0.04, 0, 0.84)), (0.3, 1.6, 0.12), holz)
        netz.quader(ort + Vector((-vor + 0.06, 0, 0.99)), (0.04, 0.8, 0.03), holz)
        pv, oben, unten = PEDAL
        netz.stab(ort + Vector((-pv + 0.05, -0.09, (oben + unten) / 2 - 0.02)), ort + Vector((-pv - 0.9, -0.09, 0.06)), 0.12, 0.04,
                  st["Holz alt"], quer=(0, 0, 1))
    elif name == "Gatter":                                              # Stamm auf dem Wagen, die Stirn zur Figur
        vor, hoehe = ARBEITSHOEHEN["Stamm"]
        netz.stab(ort + Vector((-vor, 0, hoehe)), ort + Vector((-vor - 0.5, 0, hoehe)), 0.6, 0.6, st["Holz alt"])
        netz.quader(ort + Vector((-vor - 0.25, 0, 0.45)), (0.5, 0.7, 0.3), holz)
    elif name == "Behauen":                                             # Block auf dem Bock, die Schneide auf seiner Oberseite
        vor, hoehe = ARBEITSHOEHEN["Block"]
        netz.quader(ort + Vector((-vor - 0.25, 0, hoehe - 0.19)), (0.6, 0.8, 0.38), st["Sandstein"])
        netz.quader(ort + Vector((-vor - 0.25, 0, (hoehe - 0.38) / 2)), (0.5, 0.6, hoehe - 0.38), st["Holz alt"])
    elif name == "Bildhauen":                                           # Figur auf dem Bock, ihre Vorderseite an der Schneide
        vor, hoehe = ARBEITSHOEHEN["Figur"]
        netz.quader(ort + Vector((-vor - 0.2, 0, 0.25)), (0.6, 0.6, 0.5), holz)
        netz.quader(ort + Vector((-vor - 0.18, 0, 1.15)), (0.36, 0.5, 1.3), st["Sandstein"])
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
