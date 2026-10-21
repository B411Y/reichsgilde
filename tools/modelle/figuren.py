"""Passanten: Grundkörper je Volk mit Skelett, Trachtteilen und Bewegungen.

    blender -b --python tools/modelle/figuren.py        oder
    python tools/modelle/figuren.py [--schnell] [--ohne-bilder] [--nur-bewegungen]

Schreibt game/assets/modelle/figuren/figur_mensch.glb, figur_zwerg.glb, figur_elb.glb, figur_halbling.glb
und Vorschaubilder nach docs/modelle/ (figuren.png: alle Körper und Trachten, figuren-bewegungen.png: die Bewegungen).

Inhalt je Datei (Blender: Z oben, Blick nach -Y; im Spiel Y oben, Blick nach +Z):
    Skelett         elf Knochen: Wurzel, Ruecken, Kopf, Oberarm_L/R, Unterarm_L/R, Oberschenkel_L/R, Unterschenkel_L/R
                    („L“ ist die linke Seite der Figur). Jeder Teil hängt starr an genau einem Knochen.
    Koerper_Mann    Wams bis zur Mitte des Oberschenkels, Beinkleid, Schuhe, Gürtel; beim Zwerg mit Bart
    Koerper_Frau    Kleid bis über die Knöchel, Zopf
    Trachtteile     Strohhut, Barett, Kapuze, Haube, Schleier, Schuerze, Kiepe, Buendel, Tasche, Stab, Hacke, Hammer,
                    Hobel, Saege, Drechseleisen, Schlaegel, Meissel, Stoessel, Kelle, Flakon, Krug, Laute, Feder, Sieb,
                    Dochtstab, Dietrich, Muenzbeutel
                    – standardmäßig aus, das Spiel blendet sie je Rolle ein (wie heute `TrachtVon`; das Werkzeug bei der
                    passenden Bewegung: Hammer beim Hämmern, Hobel beim Hobeln, Säge beim Sägen, Drechseleisen beim Drechseln,
                    Schlägel und Meißel beim Behauen und Bildhauen, Stößel beim Mörsern, Kelle beim Rühren, Flakon beim
                    Mischen, Krug beim Zapfen und Trinken, Laute bei „Laute“, Dochtstab beim Kerzenziehen, Sieb beim
                    Schöpfen, Feder beim Schreiben, Dietrich beim Knacken, Muenzbeutel beim Zählen)
    Bewegungen      Stehen, Gehen, Eilen, Gebeugt, Tragen, Sitzen, Reden, Hacken, Buecken, Hueten,
                    Ziehen (Handkarren), Kutschieren (auf dem Bock),
                    Haemmern, Blasebalg, Schleifen (stehend), Feinarbeit (sitzend) – Arbeit in der Schmiede,
                    Hobeln, Saegen (Handsäge am Sägebock), Drechseln (ein Fuß tritt das Pedal), Gatter (Stamm nachschieben)
                    – Arbeit beim Tischler,
                    Behauen (Schlägel rechts, Meißel links am Werkblock), Bildhauen (feine Meißelarbeit an einer Figur in
                    Brust- bis Kopfhöhe) – Arbeit beim Steinmetz,
                    Moersern (Stößel kreist im Mörser und stößt, die Linke hält den Mörser), Ruehren (mit langer Kelle im
                    Kessel, die Linke greift den Stiel), Destillieren (bückt sich zur Feuertür der Destille und regelt die
                    Flamme, richtet sich auf und prüft den Tropfen an der Vorlage; ruhig) – Arbeit beim Alchimisten,
                    Mischen (aus dem Flakon in das Mischglas gießen, den Flakon schwenken; die Linke hält das Glas),
                    Pressen (beide Hände am Querhebel der Spindelpresse: ihn hinüberschieben und zurückziehen, dabei je
                    einen Schritt zur Seite; die Wurzel verschiebt sich dazu seitlich) – Arbeit beim Parfumeur,
                    Zapfen (den Krug unter den Zapfhahn halten, die Linke dreht den Hahn auf und zu, den vollen Krug auf der
                    Theke abstellen) – Arbeit beim Wirt, Trinken (im Sitzen: den Krug vom Tisch an den Mund heben, trinken,
                    absetzen; für die Gäste, das Spiel versetzt die Phase je Gast), Laute (der Barde steht und spielt),
                    Kerzenziehen (den Dochtstab mit beiden Händen in den Wachstrog senken, heben, abtropfen lassen),
                    Schoepfen (das Sieb schräg hinten in die Bütte tauchen, unter dem Brei zur Figur ziehen, heben, schütteln,
                    abtropfen lassen), Schreiben (sitzend am Pult: eine Zeile mit der Feder, die Feder ins Tintenhorn) – Arbeit
                    beim Prediger, Predigen (auf der Kanzel: die Hände auf der Brüstung, die Rechte weist nach oben, beide Arme
                    öffnen sich zur Gemeinde), Beten (sitzend, die Hände gefaltet) und Knien (kniend, die Hände gefaltet; am
                    Beichtstuhl) – für Prediger und Gläubige,
                    Knacken (den Dietrich ins Schloss am Übungsbrett führen, stochern und horchen, die Linke flach auf dem
                    Brett), Zaehlen (sitzend: Münzen vom Haufen in den offenen Beutel in der Linken), Schlafen (auf dem Rücken
                    liegend, ruhig atmend; die Wurzel kippt um −90°) – für die Diebe
                    – jede eine Schleife am Ort (ohne Vorwärtsbewegung; das Spiel schiebt die Figur)
                    Die Werkstatt-Bewegungen zielen mit den Händen (beim Drechseln auch mit dem rechten Fuß) auf feste
                    Höhen (ARBEITSHOEHEN, PEDAL, VORLAGE, MISCHGLAS, PRESSE, ZAPFHAHN, KRUG_TISCH, KERZEN, BUETTE, SCHREIBEN,
                    KANZEL, KNACKEN, ZAEHLEN – wie in schmied_innen.glb, tischler_innen.glb, steinmetz_innen.glb,
                    alchimist_innen.glb, parfumeur_innen.glb, wirt_innen.glb, prediger_innen.glb und dieb_innen.glb):
                    Jedes Volk erreicht sie
                    mit eigenen Arm- und Beinwinkeln (`arm_ik`, `arm_ziel`, `bein_ik`).
Material: alle Teile (Körper, Trachtteile, Werkzeug) tragen nur ein Material „Figur“ – eine Figur ist so ein
Zeichenaufruf statt sechs. Je Ecke stehen zwei Angaben im Netz, je Fläche konstant:
    COLOR_0         Grundfarbe als sRGB-Wert (wie bei den Umlandbetrieben, siehe bausteine.farben_zusammenlegen)
    TEXCOORD_0      (Godot: UV) x = Zone, y = Materialart für die Oberflächendetails (bausteine.materialart):  0 feste Farbe aus COLOR_0 (Auge, Leder, Holz, Eisen, Leinen …),
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
    "Flakonglas": (0.56, 0.46, 0.74),      # lila Glas des Flakons beim Mischen (deckend gemalt)
    "Krugton": (0.62, 0.58, 0.50),         # Steinzeugkrug des Wirts, salzglasiert
    "Krugblau": (0.25, 0.32, 0.52),        # blau bemalte Bänder am Krug
    "Lautenholz": (0.55, 0.32, 0.16),      # gewölbter Bauch der Laute aus Spänen
    "Lautendecke": (0.84, 0.70, 0.46),     # Decke aus heller Fichte
    "Kerzenwachs": (0.90, 0.84, 0.64),     # frisch gezogene Kerzen am Dochtstab (Bienenwachs)
    "Docht": (0.78, 0.74, 0.64),
    "Gaensefeder": (0.92, 0.90, 0.84),     # Fahne der Schreibfeder
    "Federkiel": (0.80, 0.72, 0.52),
    "Siebgitter": (0.62, 0.50, 0.30),      # Messingdraht der Schöpfform
    "Papierbrei": (0.88, 0.86, 0.80),      # nasser Bogen auf dem Sieb
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
          "Hobel", "Saege", "Drechseleisen", "Schlaegel", "Meissel", "Stoessel", "Kelle", "Flakon", "Krug", "Laute", "Feder", "Sieb",
          "Dochtstab", "Dietrich", "Muenzbeutel"]
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
                 "Block": (0.46, 0.86), "Figur": (0.52, 1.22),
                 # Alchimist (alchimist_innen.glb): Spitze des Stößels auf dem Boden der Mulde (0,87 m, Rand 0,95 m), 0,45 m vor
                 # der Figur; Spitze der Kelle im Kessel (Mitte 0,7 m vor ihr, Brühe bis 0,85 m); Schieber an der Feuertür der
                 # Destille
                 "Moerser": (0.45, 0.87), "Kessel": (0.7, 0.70), "Ofen": (0.48, 0.52)}
PEDAL = (0.2, 0.345, 0.09)   # Pedal der Drehbank unter dem rechten Fuß: vor der Figur, Höhe der Oberseite oben und getreten
SAEGE = (0.2, 0.33)          # Säge: Abstand von der Mitte der Hand bis zur Oberkante des Stamms beim Stoß und beim Zug
KLUEPFEL_R = 0.062           # Halbmesser des Kopfs: Seine Seite trifft den Meißel
FAUST_R = (-0.17, -0.16, 0.07)   # beim Behauen: rechte Hand gegenüber dem Kopf des Meißels (seitlich, vor, Höhe), vor der Brust
MEISSEL = (0.12, 0.13)       # Meißel: von der Mitte der linken Hand bis zur Schneide und bis zum Kopf (Schlagfläche)
MEISSEL_RICHTUNG = (0.0, 0.25, -1.0)   # beim Behauen vom Kopf zur Schneide (seitlich, vor, Höhe): fast senkrecht, leicht nach vorn
BILDHAUEN_RICHTUNG = (0.0, 0.6, -0.8)  # beim Bildhauen (vorläufig, wird gemessen)
VORLAGE = (-0.45, 0.4, 0.92)           # Destillieren: die Rechte am Hals der Vorlage, rechts vor der Figur (seitlich, vor, Höhe)
FAUST = {"Stoessel": (-0.01, -0.05, 0.19),   # rechte Faust gegenüber der Spitze des Werkzeugs (seitlich, vor, Höhe) in der
         "Kelle": (-0.10, -0.36, 0.52)}      # Haltung von REQUISITEN: Stößel fast senkrecht, Kelle steil von oben in den Kessel
                                             # (die Kelle beim Menschen; kleinere Völker halten sie tiefer, siehe _faust)
KELLE_GRIFF = 0.3                      # beim Rühren greift die Linke den Stiel so weit unter der rechten Faust
# Parfumeur (parfumeur_innen.glb): Mitte der Öffnung des Mischglases auf dem Mischtisch (seitlich, vor, Höhe) – die Rechte gießt
# aus dem Flakon hinein, die Linke hält das Glas. Spindelpresse: Achse der Spindel `abstand` vor der Figur (Mitte des Hubs),
# Querhebel in `hoehe`, die Rechte greift ihn `rechts`, die Linke `links` von der Achse; er schwenkt um ±`winkel` Grad.
MISCHGLAS = (-0.1, 0.42, 0.97)
FLAKON_MUND = 0.096                    # Mund des Flakons über der Mitte der Hand (aufrecht gehalten)
PRESSE = {"abstand": 1.05, "hoehe": 1.0, "rechts": 0.6, "links": 0.78, "winkel": 18.0}
# Wirt (wirt_innen.glb): Theke 1,05 m hoch, ihre Hinterkante 0,28 m vor der Figur. Auf der Theke rechts vor ihr liegt das
# Zapffass, aus seinem Boden ragt der Hahn zur Figur: Tülle bei ZAPFHAHN, das Küken (der Griff zum Drehen) bei HAHN_GRIFF;
# den vollen Krug stellt sie bei KRUG_AB ab (Mitte des Bodens). Der Krug: Halbmesser `r`, Höhe `h`; beim Halten liegt die
# Mitte seines Bodens `seite` zur Innenseite der Faust (nach links) und `tief` unter der Mitte der Hand.
THEKE = 1.05
ZAPFHAHN = (-0.22, 0.55, 1.26)
HAHN_GRIFF = (-0.25, 0.55, 1.39)
KRUG_AB = (0.14, 0.5, THEKE)
KRUG = {"r": 0.046, "h": 0.15, "seite": 0.078, "tief": 0.07}
# Gäste: Mitte des Bodens des Krugs auf dem Tisch (seitlich, vor, Höhe über der Sitzhöhe – die Hüfte im Sitzen liegt auf
# Kniehöhe − 2 cm, beim Menschen 0,48 m; der Tisch ist dann 0,78 m hoch). Das Spiel hebt kleine Völker auf die Sitzhöhe des
# Menschen (wie bei der Feinarbeit), so steht der Krug bei jedem Volk auf demselben Tisch.
KRUG_TISCH = (-0.16, 0.4, 0.3)
# Prediger (prediger_innen.glb). Kerzenziehen am Wachstrog: Mitte `vor` vor der Figur, Rand `rand`, Wachs `wachs` hoch; der
# Dochtstab liegt quer in beiden Fäusten, die Kerzen hängen `kerze` Meter darunter und tauchen `tief` ins Wachs (Mensch; die
# anderen Völker mit dem Quadrat ihrer Schulterhöhe: Halblinge ziehen kurze Kerzen, sonst kämen sie nicht über den Rand). Schöpfen an der Bütte: Mitte `vor`, Rand `rand`, Brei `brei` hoch; das Sieb (die
# Schöpfform, `sieb` tief, so breit wie die Schultern) taucht bei `fern` ein, wird `unter` dem Brei bis `nah` zur Figur gezogen,
# `ueber` dem Rand geschüttelt und tropft bei `tropf` ab. Kerzen und Sieb bleiben dabei waagrecht: Die Unterarme behalten in der
# ganzen Bewegung ihre Richtung, Neigung und Knie gleichen die Höhe aus (`beide_haende`).
KERZEN = {"vor": 0.46, "rand": 0.84, "wachs": 0.77, "kerze": 0.30, "tief": 0.2}
KERZE_R = 0.013
BUETTE = {"vor": 0.52, "rand": 0.92, "brei": 0.84, "fern": 0.64, "nah": 0.40, "tropf": 0.44, "unter": 0.06, "ueber": 0.10,
          "sieb": 0.30}
# Schreiben (sitzend am Schreibpult): Spitze der Feder am Anfang der Zeile (seitlich, vor, Höhe über der Sitzhöhe – wie beim
# Trinken hebt das Spiel kleine Völker auf die Sitzhöhe des Menschen), die Zeile läuft `zeile` Meter nach rechts; die Mündung
# des Tintenhorns rechts am Pult; die Platte ist um `neigung` Grad zur Figur geneigt.
SCHREIBEN = {"blatt": (0.07, 0.40, 0.40), "zeile": 0.13, "horn": (-0.22, 0.33, 0.37), "neigung": 30.0}
KANZEL = (0.46, 0.95)        # Predigen: Brüstung der Kanzel (vor der Figur, Höhe über dem Boden der Kanzel)
# Dieb (dieb_innen.glb). Knacken am Übungsbrett: das Schlüsselloch (seitlich, vor, Höhe) auf der Vorderseite des Bretts; der
# Dietrich ragt `dietrich` Meter aus der Faust und steckt `tief` im Schloss, die Linke liegt flach bei `hand_l` auf dem Brett.
# Zählen am Zähltisch (sitzend; Höhen über der Sitzhöhe wie beim Trinken, der Tisch ist also 0,78 m hoch): der Münzhaufen vor
# der Figur rechts, der Beutel links (Mitte seines Bodens auf dem Tisch), die Linke hält ihn am Hals offen. Schlafen: auf dem
# Rücken, die Hüfte `tiefe` + 1 cm über dem Anker (Oberkante des Strohsacks), der Kopf hinter dem Anker, die Füße vorn.
KNACKEN = {"schloss": (-0.03, 0.40, 1.02), "dietrich": 0.11, "tief": 0.015, "hand_l": (0.17, 0.375, 1.1)}
ZAEHLEN = {"haufen": (-0.12, 0.40, 0.30), "beutel": (0.16, 0.30, 0.30)}
MUENZBEUTEL = {"r": 0.05, "h": 0.08, "hals": 0.03}   # Beutel: Halbmesser und Höhe des Bauchs, darüber der offene Hals


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
    elif name == "Laute":
        t.netz.alle = True
        with t.an("Ruecken") as n:        # vor dem Bauch, der Hals schräg nach links oben (die Linke greift ihn)
            c, a, f, k = laute_lage(p)
            q = a.cross(f)
            profil = [(-0.2, 0.03), (-0.17, 0.09), (-0.1, 0.13), (-0.02, 0.135), (0.06, 0.105), (0.11, 0.065), (0.14, 0.03)]
            teile = 6

            def bauch(u, r, j):           # gewölbter Bauch aus Spänen hinter der flachen Decke (j: 0 … teile über die Rückseite)
                w = math.pi * j / teile
                return c + a * (u * k) + q * (r * k * math.cos(w)) - f * (r * k * 0.8 * math.sin(w))
            for (u0, r0), (u1, r1) in zip(profil, profil[1:]):
                for j in range(teile):
                    quad = [bauch(u0, r0, j), bauch(u0, r0, j + 1), bauch(u1, r1, j + 1), bauch(u1, r1, j)]
                    n.vieleck_nach(quad, sum(quad, Vector()) / 4 - (c + a * ((u0 + u1) / 2 * k)), st["Lautenholz"])
            rand = [bauch(u, r, 0) for u, r in profil] + [bauch(u, r, teile) for u, r in reversed(profil)]
            n.vieleck_nach(rand, f, st["Lautendecke"])                       # flache Decke
            n.vieleck_nach([c + a * (k * (0.02 + 0.032 * math.cos(w))) + q * (k * 0.032 * math.sin(w)) + f * 0.003
                            for w in (2 * math.pi * j / 8 for j in range(8))], f, st["Auge"])          # Rosette
            n.stab(c - a * 0.12 * k - q * 0.05 * k + f * 0.008, c - a * 0.12 * k + q * 0.05 * k + f * 0.008, 0.014, 0.014,
                   st["Lautenholz"], quer=f)                                 # Steg
            hals0, hals1 = c + a * 0.13 * k - f * 0.012 * k, c + a * 0.45 * k - f * 0.012 * k
            n.stab(hals0, hals1, 0.045 * k, 0.03 * k, st["Lautenholz"], quer=f)                   # Hals mit Griffbrett
            n.stab(hals0 + f * 0.017 * k, hals1 + f * 0.017 * k, 0.04 * k, 0.004, st["Auge"], quer=f)
            knick = (a - f * 0.9).normalized()                               # Wirbelkasten, nach hinten abgeknickt
            n.stab(hals1, hals1 + knick * 0.14 * k, 0.04 * k, 0.03 * k, st["Lautenholz"], quer=f)
    elif name == "Saege":
        t.netz.alle = True                # dreht sich mit dem Arm: auch Flächen, die in Ruhe nach unten zeigen
        with t.an("Unterarm_R") as n:     # Blatt verlängert den Unterarm (in Ruhe nach unten), die Zähne auf der Seite +y
            zaehne = [q for k in range(10) for q in ((0.045, -0.62 + 0.055 * k), (0.063, -0.5925 + 0.055 * k))]   # (y, z)
            n.prisma([(-0.06, -0.07), (-0.015, -0.62)] + zaehne + [(0.045, -0.07)], -0.003, 0.003, st["Eisen"],
                     matrix=Matrix.Translation((xr, 0, hand_z)))
            n.quader((xr, 0.0, hand_z + 0.01), (0.03, 0.11, 0.13), st["Holz"])                   # Griff um die Hand
    return t


REQUISITEN = {"Hobel": ("Hobeln", 0.3), "Drechseleisen": ("Drechseln", 0.5), "Meissel": ("Behauen", 0.55),
              "Schlaegel": ("Behauen", 0.55), "Stoessel": ("Moersern", 0.0), "Kelle": ("Ruehren", 0.0),
              "Flakon": ("Mischen", 0.0), "Krug": ("Zapfen", 0.0), "Feder": ("Schreiben", 0.0), "Sieb": ("Schoepfen", 0.0),
              "Dochtstab": ("Kerzenziehen", 0.0), "Dietrich": ("Knacken", 0.3), "Muenzbeutel": ("Zaehlen", 0.0)}   # Bewegung, Phase


def requisit(name, st, p, ske):
    """Werkzeug in der Hand für eine Bewegung (REQUISITEN): in deren Haltung gebaut – Hobel flach auf dem Brett,
    Drechseleisen von der Hand zum Werkstück, Meißel (linke Hand) mit der Schneide auf dem Block, Klüpfel mit dem Kopf auf dem
    Meißel, Stößel von der Faust in die Mulde, Kelle von der Faust schräg in den Kessel, Flakon aufrecht in der Faust – und mit
    der Lage des Unterarms in die Ruhelage zurückgerechnet. Es hängt starr am Unterarm und folgt ihm durch die ganze Bewegung."""
    import bpy
    bewegung, phase = REQUISITEN[name]
    seite = "L" if name in ("Meissel", "Muenzbeutel") else "R"
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
        elif name == "Stoessel":                  # hölzerner Stößel: oben aus der Faust, unten zur Keule verdickt
            spitze = _blender(_werkzeug_koerper(bewegung, phase, p)[2])
            d = (spitze - hand).normalized()
            for (u0, r0), (u1, r1) in zip(((-0.07, 0.014), (0.1, 0.017), (0.17, 0.027)), ((0.1, 0.017), (0.17, 0.027), (0.2, 0.024))):
                drehteil(n, zurueck @ (hand + d * u0), zurueck.to_3x3() @ d, u1 - u0, r0, r1, holz, n=6,
                         deckel=(u0 == -0.07, u1 == 0.2))
        elif name == "Kelle":                     # lange Rührkelle: Stiel aus der Faust bis in den Kessel, unten ein flaches Blatt
            spitze = _blender(_werkzeug_koerper(bewegung, phase, p)[2])
            d = (spitze - hand).normalized()
            quer = zurueck.to_3x3() @ Vector((1, 0, 0))
            n.stab(zurueck @ (hand - d * 0.1), zurueck @ (spitze - d * 0.16), 0.032, 0.032, holz, quer=quer)
            n.stab(zurueck @ (spitze - d * 0.2), zurueck @ spitze, 0.09, 0.016, st["Holz alt"], quer=quer)   # Blatt
        elif name == "Flakon":                    # gläserner Flakon, aufrecht in der Faust: Bauch, Schulter, Hals mit Goldring, Lippe
            achse = zurueck.to_3x3() @ Vector((0, 0, 1))
            profil = [(-0.15, 0.03), (-0.135, 0.046), (-0.02, 0.046), (0.025, 0.034), (0.045, 0.012), (0.074, 0.011),
                      (0.084, 0.016), (FLAKON_MUND, 0.014)]                 # die Faust hält ihn an der Schulter
            for (u0, r0), (u1, r1) in zip(profil, profil[1:]):
                drehteil(n, zurueck @ (hand + Vector((0, 0, u0))), achse, u1 - u0, r0, r1,
                         st["Gold"] if u0 == 0.074 else st["Flakonglas"], deckel=(u0 == profil[0][0], u1 == FLAKON_MUND))
        elif name == "Krug":                      # Steinzeugkrug, aufrecht, der Henkel in der Faust: Bauch zur Innenseite der Hand
            boden = hand + Vector((KRUG["seite"], 0, -KRUG["tief"]))
            achse = zurueck.to_3x3() @ Vector((0, 0, 1))
            r, h = KRUG["r"], KRUG["h"]
            profil = [(0.0, r * 0.82), (0.012, r * 0.9), (h * 0.45, r), (h * 0.85, r * 0.86), (h * 0.9, r * 0.9), (h, r * 0.88)]
            for (u0, r0), (u1, r1) in zip(profil, profil[1:]):
                drehteil(n, zurueck @ (boden + Vector((0, 0, u0))), achse, u1 - u0, r0, r1,
                         st["Krugblau"] if u0 in (0.012, h * 0.85) else st["Krugton"], deckel=(u0 == 0.0, False))
            drehteil(n, zurueck @ (boden + Vector((0, 0, h - 0.012))), achse, 0.004, r * 0.84, r * 0.84, st["Haar"],
                     deckel=(False, True))                                  # Bier (dunkel) knapp unter dem Rand
            quer = zurueck.to_3x3() @ Vector((0, 1, 0))
            griff = [Vector((-r * 0.9, 0, h * 0.82)), Vector((-KRUG["seite"], 0, KRUG["tief"] + 0.035)),
                     Vector((-KRUG["seite"], 0, KRUG["tief"] - 0.035)), Vector((-r * 0.9, 0, h * 0.2))]
            for a, b in zip(griff, griff[1:]):                             # Henkel: von der Wand durch die Faust zurück
                n.stab(zurueck @ (boden + a), zurueck @ (boden + b), 0.022, 0.014, st["Krugton"], quer=quer)
        elif name == "Dochtstab":                 # quer in beiden Fäusten (die Linke am anderen Ende), darunter fünf Kerzen an Dochten
            k = p["schulter"] / VOELKER["mensch"]["schulter"]
            breit = -hand.x
            oben = zurueck.to_3x3() @ Vector((0, 0, 1))
            n.stab(zurueck @ (hand - Vector((0.08, 0, 0))), zurueck @ Vector((breit + 0.08, hand.y, hand.z)), 0.024, 0.024, holz,
                   quer=oben)
            laenge = KERZEN["kerze"] * k * k
            for j in range(5):
                x = hand.x + 0.07 + (2 * breit - 0.14) * j / 4
                docht = Vector((x, hand.y, hand.z - 0.012))
                n.stab(zurueck @ docht, zurueck @ (docht - Vector((0, 0, 0.03))), 0.005, 0.005, st["Docht"],
                       quer=zurueck.to_3x3() @ Vector((0, 1, 0)))
                fuss = docht - Vector((0, 0, 0.03 + laenge))
                profil = ((0.0, 0.004), (0.018, KERZE_R * 1.25), (laenge - 0.01, KERZE_R * 0.85), (laenge, KERZE_R * 0.5))
                for (u0, r0), (u1, r1) in zip(profil, profil[1:]):
                    drehteil(n, zurueck @ (fuss + Vector((0, 0, u0))), oben, u1 - u0, r0, r1, st["Kerzenwachs"], n=6,
                             deckel=(False, u1 == laenge))
        elif name == "Sieb":                      # Schöpfform waagrecht zwischen den Fäusten: Rahmen, Deckel, Stege, Gitter, nasser Bogen
            breit, tief = -hand.x + 0.035, BUETTE["sieb"] / 2
            M = zurueck @ Matrix.Translation((0, hand.y, hand.z - 0.015))                     # x quer, y vor (−), z oben; z 0 = Gitter
            for sx in (-1, 1):
                n.quader((sx * breit, 0, 0.0), (0.03, 2 * tief + 0.03, 0.035), holz, M)
                n.quader((sx * (breit - 0.006), 0, 0.03), (0.022, 2 * tief + 0.02, 0.025), st["Holz alt"], M)    # Deckel
            for sy in (-1, 1):
                n.quader((0, sy * tief, 0.0), (2 * breit - 0.03, 0.03, 0.035), holz, M)
                n.quader((0, sy * (tief - 0.006), 0.03), (2 * breit - 0.042, 0.022, 0.025), st["Holz alt"], M)
            for j in range(5):                                                                  # Stege unter dem Gitter
                n.quader((-breit + 2 * breit * (j + 1) / 6, 0, -0.012), (0.012, 2 * tief - 0.02, 0.02), holz, M)
            for z, b, stoff in ((0.0, 0.0, "Siebgitter"), (0.005, 0.016, "Papierbrei")):
                ecken = [M @ Vector((x * (breit - b), y * (tief - b), z)) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
                n.vieleck(ecken, st[stoff])
                if not b:
                    n.vieleck(list(reversed(ecken)), st[stoff])
        elif name == "Feder":                     # Gänsekiel: die Spitze auf dem Blatt, durch die Faust nach hinten oben, dann die Fahne
            spitze = _blender(_schreiben_spitze(0.0, p)[0])
            d = (hand - spitze).normalized()
            quer = d.cross(Vector((0, 0, 1))).normalized()
            q3 = zurueck.to_3x3() @ quer
            n.stab(zurueck @ spitze, zurueck @ (spitze + d * 0.012), 0.002, 0.004, st["Kohle"], quer=q3)
            n.stab(zurueck @ (spitze + d * 0.012), zurueck @ (hand + d * 0.06), 0.007, 0.007, st["Federkiel"], quer=q3)
            fahne = [(0.03, 0.0), (0.07, 0.016), (0.15, 0.022), (0.21, 0.012), (0.24, 0.0)]       # (ab der Faust, Breite)
            for (u0, b0), (u1, b1) in zip(fahne, fahne[1:]):
                for s, f in ((1, 1.0), (-1, 0.7)):
                    ecken = [hand + d * u0 + quer * s * 0.002, hand + d * u1 + quer * s * 0.002]
                    ecken += [hand + d * u + quer * s * (0.002 + b * f) for u, b in ((u1, b1), (u0, b0)) if b > 0]
                    ecken = [zurueck @ e for e in ecken]
                    n.vieleck(ecken, st["Gaensefeder"])
                    n.vieleck(list(reversed(ecken)), st["Gaensefeder"])
        elif name == "Dietrich":                  # Haken aus Eisendraht: der Griff (Ring) in der Faust, die Spitze im Schloss
            s, v, h = KNACKEN["schloss"]
            spitze = _blender((s, v + KNACKEN["tief"], h))
            d = (spitze - hand).normalized()
            quer = d.cross(Vector((0, 0, 1))).normalized()
            oben = quer.cross(d)
            q3 = zurueck.to_3x3() @ quer
            n.stab(zurueck @ (hand - d * 0.035), zurueck @ (spitze - d * 0.012), 0.004, 0.004, eisen, quer=q3)
            n.stab(zurueck @ (spitze - d * 0.012), zurueck @ (spitze + oben * 0.008), 0.004, 0.004, eisen, quer=q3)   # Haken
            for k in range(6):                                                                        # Ring hinten
                w0, w1 = 2 * math.pi * k / 6, 2 * math.pi * (k + 1) / 6
                mitte = hand - d * 0.05
                a = mitte + (quer * math.cos(w0) + oben * math.sin(w0)) * 0.016
                b = mitte + (quer * math.cos(w1) + oben * math.sin(w1)) * 0.016
                n.stab(zurueck @ a, zurueck @ b, 0.005, 0.005, eisen, quer=zurueck.to_3x3() @ d)
        elif name == "Muenzbeutel":               # Lederbeutel aufrecht auf dem Tisch, oben offen; die linke Faust hält ihn an der Seite
            m = MUENZBEUTEL
            bs_, bv, bh = ZAEHLEN["beutel"]
            boden = _blender((bs_, bv, p["knie"] - 0.02 + bh + 0.002))
            achse = zurueck.to_3x3() @ Vector((0, 0, 1))
            r, h, oben = m["r"], m["h"], m["h"] + m["hals"]
            profil = [(0.0, r * 0.6), (0.02, r * 0.92), (h * 0.5, r), (h, r * 0.8), (h + m["hals"] * 0.7, r * 0.62), (oben, r * 0.8)]
            for (u0, r0), (u1, r1) in zip(profil, profil[1:]):
                drehteil(n, zurueck @ (boden + Vector((0, 0, u0))), achse, u1 - u0, r0, r1, st["Leder"], n=8,
                         deckel=(u0 == 0.0, False))
            drehteil(n, zurueck @ (boden + Vector((0, 0, oben - 0.012))), achse, 0.002, r * 0.64, r * 0.64, st["Kohle"], n=8,
                     deckel=(False, True))                                                     # dunkle Öffnung
            drehteil(n, zurueck @ (boden + Vector((0, 0, h + 0.006))), achse, 0.008, r * 0.72, r * 0.7, st["Seil"], n=8,
                     deckel=(False, False))                                                    # Zugband
            n.stab(zurueck @ (boden + Vector((-r * 0.7, -0.01, h + 0.01))), zurueck @ (boden + Vector((-r * 1.0, -0.03, h - 0.05))),
                   0.006, 0.006, st["Seil"])
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
    elif name in ("Moersern", "Ruehren"):                               # Stößel bzw. Kelle in der Rechten, die Spitze genau geführt
        winkel, hoch = werkzeug_fuehren(name, phase, p)
        r.update(winkel)
    elif name == "Destillieren":                                        # Flamme regeln, Tropfen prüfen
        winkel, hoch = destillieren(phase, p)
        r.update(winkel)
    elif name == "Mischen":                                             # aus dem Flakon ins Mischglas gießen, schwenken
        winkel, hoch = mischen(phase, p)
        r.update(winkel)
    elif name == "Pressen":                                             # Querhebel der Spindelpresse hinüber und zurück
        winkel, hoch = pressen(phase, p)
        r.update(winkel)
    elif name in ("Zapfen", "Trinken", "Laute"):                        # Wirt: zapfen, Gäste trinken, der Barde spielt
        winkel, hoch = {"Zapfen": zapfen, "Trinken": trinken, "Laute": laute}[name](phase, p)
        r.update(winkel)
    elif name in PREDIGER:                                              # Prediger: Kerzen ziehen, schöpfen, schreiben, predigen, beten
        winkel, hoch = PREDIGER[name](phase, p)
        r.update(winkel)
    elif name in DIEB:                                                  # Dieb: Schloss knacken, Beute zählen, schlafen
        winkel, hoch = DIEB[name](phase, p)
        r.update(winkel)
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


def unterarm_lage(p, pose, hoch, seite, seitlich=0.0):
    """Matrix, die einen Punkt am Unterarm der Seite „L“/„R“ aus der Ruhelage in die Haltung bringt – dieselbe Rechnung wie
    Blender (Wurzel, Ruecken, Oberarm, Unterarm; Euler XYZ im Knochen). `pose`: {Knochen: (x, y, z) im Bogenmaß} wie aus
    `haltung`, `hoch`: Hüfte, `seitlich`: Wurzel zur Seite verschoben (+ links). Koordinaten der Figur in Blender (Blick −Y)."""
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
            lokal = Matrix.Translation((seitlich, hoch, 0)) @ lokal
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


# ── Alchimist: Stößel und Kelle (starr in der rechten Faust, ihre Spitze wird geführt wie Meißel und Klüpfel) ──

def _moersern_koerper(phase, p):
    """Mörsern: Rumpf, Beine, linker Arm (hält den Mörser am Rand) und die Spitze des Stößels (seitlich, vor, Höhe). Zwei Runden
    in der Mulde (0–0,6), dann zwei Stöße: langsam heben, schnell hinunter, kurz Ruhe."""
    vor, hoehe = ARBEITSHOEHEN["Moerser"]
    rk = 0.035
    if phase < 0.6:
        w = 2 * math.pi * 2 * phase / 0.6
        spitze, hub = Vector((rk * math.cos(w), vor + rk * math.sin(w), hoehe)), 0.0
    else:
        h = (2 * (phase - 0.6) / 0.4) % 1
        hub = weich(h / 0.6) if h < 0.6 else 1 - ((h - 0.6) / 0.12) ** 2 if h < 0.72 else 0.0
        spitze = Vector((rk, vor, hoehe + 0.08 * hub))
    lehne, hoch = 17 + 3 * hub, -0.02
    r = {"Ruecken": (lehne, 0, 2 * math.sin(2 * math.pi * phase)), "Kopf": (26 - lehne, 0, 0)}
    r["Oberschenkel_L"], r["Oberschenkel_R"] = (-8, 0, 5), (6, 0, -5)
    r["Unterschenkel_L"], r["Unterschenkel_R"] = (10, 0, 0), (6, 0, 0)
    r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", (0.15, vor + 0.01, hoehe + 0.1))
    return r, hoch, spitze


def _ruehren_koerper(phase, p):
    """Rühren: Rumpf und Beine; die Spitze der Kelle zieht eine Runde durch den Kessel, der Rumpf neigt und dreht sich mit."""
    vor, hoehe = ARBEITSHOEHEN["Kessel"]
    w = 2 * math.pi * phase
    rk = 0.16
    spitze = Vector((rk * math.cos(w), vor + 0.8 * rk * math.sin(w), hoehe + 0.02 * math.sin(2 * w)))
    lehne, hoch, drehung = 16 + 5 * math.sin(w), -0.02, 9 * math.cos(w)
    r = {"Ruecken": (lehne, drehung, 0), "Kopf": (28 - lehne, -0.4 * drehung, 0)}
    r["Oberschenkel_L"], r["Oberschenkel_R"] = (-10, 0, 5), (8, 0, -5)
    r["Unterschenkel_L"], r["Unterschenkel_R"] = (12, 0, 0), (8, 0, 0)
    return r, hoch, spitze


def _faust(name, p):
    """FAUST für das Volk: Die Kelle hält jedes Volk gleich steil, die Faust liegt um seine Schulterhöhe tiefer oder höher."""
    k = p["schulter"] / VOELKER["mensch"]["schulter"] if name == "Kelle" else 1.0
    return Vector(FAUST[name]) * k


def _werkzeug_koerper(bewegung, phase, p):
    return (_moersern_koerper if bewegung == "Moersern" else _ruehren_koerper)(phase, p)


_WERKZEUG = {}


def werkzeug_ruhe(name, p):
    """Spitze des Werkzeugs `name` (Stößel, Kelle) und – bei der Kelle – der Griff der linken Hand am Stiel in der Ruhelage des
    rechten Unterarms (Blender). Gebaut in der Haltung von REQUISITEN: Dort liegt die Faust FAUST gegenüber der Spitze."""
    schluessel = (name,) + tuple(sorted(p.items()))
    if schluessel not in _WERKZEUG:
        bewegung, phase = REQUISITEN[name]
        r, hoch, spitze = _werkzeug_koerper(bewegung, phase, p)
        r["Oberarm_R"], r["Unterarm_R"] = arm_genau(p, r, hoch, "R", tuple(spitze + _faust(name, p)))
        lage = unterarm_lage(p, _bogen(r), hoch, "R")
        hand, ziel = lage @ hand_ruhe(p, "R"), _blender(spitze)
        griff = hand + (ziel - hand).normalized() * KELLE_GRIFF
        _WERKZEUG[schluessel] = (lage.inverted() @ ziel, lage.inverted() @ griff)
    return _WERKZEUG[schluessel]


def werkzeug_fuehren(bewegung, phase, p):
    """Mörsern und Rühren: Die rechte Faust wird so geführt, dass die Spitze von Stößel bzw. Kelle (starr in der Faust) genau
    auf ihrer Bahn liegt; beim Rühren greift die Linke den Stiel KELLE_GRIFF unter der Faust. Gibt (Winkel in Grad, Hüfte)."""
    name = "Stoessel" if bewegung == "Moersern" else "Kelle"
    r, hoch, spitze = _werkzeug_koerper(bewegung, phase, p)
    spitze_ruhe, griff_ruhe = werkzeug_ruhe(name, p)
    faust = spitze + _faust(name, p)
    for _ in range(24):
        r["Oberarm_R"], r["Unterarm_R"] = arm_genau(p, r, hoch, "R", tuple(faust))
        fehler = spitze - _figur(unterarm_lage(p, _bogen(r), hoch, "R") @ spitze_ruhe)
        if fehler.length < 2e-4:
            break
        faust += fehler * 0.5
    if name == "Kelle":
        griff = _figur(unterarm_lage(p, _bogen(r), hoch, "R") @ griff_ruhe)
        r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", tuple(griff))
    return r, hoch


_DESTILLIEREN = {}


def destillier_stand(p):
    """Die drei Haltungen des Destillierens je Volk: Rumpf (Neigung, Drehung), Hüfte, rechte Hand, linke Hand, Kopf (Neigung,
    Drehung). An der Feuertür geht die Figur so tief in die Hocke, an der Vorlage neigt sie sich so weit, bis die Rechte ihr
    Ziel erreicht (große Völker tiefer, kleine weniger)."""
    schluessel = tuple(sorted(p.items()))
    if schluessel not in _DESTILLIEREN:
        vor_o, hoehe_o = ARBEITSHOEHEN["Ofen"]
        k = p["huefte"] / VOELKER["mensch"]["huefte"]

        def trifft(lehne, drehung, hoch, ziel):
            r = {"Ruecken": (lehne, drehung, 0)}
            ober, unter = arm_genau(p, r, hoch, "R", ziel)
            hand = _figur(unterarm_lage(p, _bogen({**r, "Oberarm_R": ober, "Unterarm_R": unter}), hoch, "R") @ hand_ruhe(p, "R"))
            return (hand - Vector(ziel)).length < 0.01
        ofen = (-0.12, vor_o, hoehe_o)
        # so wenig in die Hocke wie möglich, dabei der Rumpf 20–36° vorgeneigt
        lehne_o, hoch = next(((w, h) for h in (-k * 0.02 * i for i in range(2, 26)) for w in range(20, 38, 4) if trifft(w, -4, h, ofen)),
                             (28, -0.4 * k))
        lehne = next((w for w in range(12, 42, 2) if trifft(w, -30, -0.01, VORLAGE)), 40)
        _DESTILLIEREN[schluessel] = {
            "stehen": (4, 0, 0.0, (-0.24, 0.12, 0.84 * k), (0.24, 0.1, 0.84 * k), (6, 0)),
            "ofen": (lehne_o, -4, hoch, ofen, (0.15, 0.3, 0.6 * k + hoch), (8 - lehne_o * 0.5, 2)),
            "vorlage": (lehne, -30, -0.01, VORLAGE, (0.18, 0.14, 0.86 * k), (34 - lehne, -12)),
        }
    return _DESTILLIEREN[schluessel]


def destillieren(phase, p):
    """Destillieren (ruhig): stehen (0–0,08), bücken zur Feuertür (bis 0,3), den Schieber regeln (bis 0,42), aufrichten und zur
    Vorlage drehen (bis 0,58), die Rechte am Hals der Vorlage, den Tropfen prüfen (bis 0,86), zurück. Die Füße bleiben stehen
    (bein_ik). Gibt (Winkel in Grad, Hüfte)."""
    stand = destillier_stand(p)
    folge = ((0.08, "stehen", "stehen"), (0.3, "stehen", "ofen"), (0.42, "ofen", "ofen"), (0.58, "ofen", "vorlage"),
             (0.86, "vorlage", "vorlage"), (1.0, "vorlage", "stehen"))
    anfang = 0.0
    for ende, a, b in folge:
        if phase <= ende:
            t = weich((phase - anfang) / (ende - anfang)) if a != b else 0.0
            break
        anfang = ende
    A, B = stand[a], stand[b]

    def mix(x, y):
        return tuple(u + (v - u) * t for u, v in zip(x, y)) if isinstance(x, tuple) else x + (y - x) * t
    lehne, drehung, hoch = mix(A[0], B[0]), mix(A[1], B[1]), mix(A[2], B[2])
    rechts, links, kopf = Vector(mix(A[3], B[3])), mix(A[4], B[4]), mix(A[5], B[5])
    if a == b == "ofen":                                                # den Schieber hin und her
        rechts.x += 0.03 * math.sin(2 * math.pi * (phase - anfang) / (ende - anfang))
    if a == b == "vorlage":                                             # nicken: der Tropfen fällt
        kopf = (kopf[0] + 5 * math.sin(4 * math.pi * (phase - anfang) / (ende - anfang)), kopf[1])
    r = {"Ruecken": (lehne, drehung, 0), "Kopf": (kopf[0], kopf[1], 0)}
    r["Oberarm_R"], r["Unterarm_R"] = arm_genau(p, r, hoch, "R", tuple(rechts))
    r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", links)
    for seite, vor, z in (("L", 0.06, 5), ("R", -0.03, -5)):
        ober, unter = bein_ik(p, hoch, vor, 0.0)
        r[f"Oberschenkel_{seite}"], r[f"Unterschenkel_{seite}"] = (ober, 0, z), (unter, 0, 0)
    return r, hoch


# ── Parfumeur: Mischen (Flakon starr in der rechten Faust, sein Mund wird geführt) und Pressen (Querhebel, Schritt zur Seite) ──

def _rolle(unter, rolle):
    """Winkel des Unterarms (Grad, Euler XYZ wie Blender), um `rolle` Grad um seine eigene Achse gedreht: erst die Drehung um
    die Achse des Knochens, dann die Beugung. Die Hand liegt auf der Achse und bleibt, wo sie ist – nur, was sie hält, kippt."""
    from mathutils import Euler
    alt = Euler(tuple(math.radians(w) for w in unter), "XYZ")
    m = alt.to_matrix() @ Euler((0.0, math.radians(rolle), 0.0), "XYZ").to_matrix()
    return tuple(math.degrees(w) for w in m.to_euler("XYZ", alt))


_FLAKON = {}


def flakon_ruhe(p):
    """Mund des Flakons in der Ruhelage des rechten Unterarms (Blender), dazu das Vorzeichen der Drehung um die Achse des
    Unterarms, die ihn zum Mischglas (zur linken Seite der Figur) kippt. Gebaut ist er in der Haltung von REQUISITEN (aufrecht,
    FLAKON_MUND über der Mitte der Hand)."""
    schluessel = tuple(sorted(p.items()))
    if schluessel not in _FLAKON:
        r, hoch, _, _ = _mischen_ziele(REQUISITEN["Flakon"][1], p)
        lage = unterarm_lage(p, _bogen(r), hoch, "R")
        mund = lage.inverted() @ (lage @ hand_ruhe(p, "R") + Vector((0, 0, FLAKON_MUND)))
        probe = {**r, "Unterarm_R": _rolle(r["Unterarm_R"], 40.0)}
        links = _figur(unterarm_lage(p, _bogen(probe), hoch, "R") @ mund).x > _figur(lage @ mund).x
        _FLAKON[schluessel] = (mund, 1.0 if links else -1.0)
    return _FLAKON[schluessel]


def _mischen_ziele(phase, p):
    """Rumpf, Beine, linke Hand am Glas und – wo sie frei geführt wird – die rechte Hand für Mischen: (Winkel in Grad, Hüfte,
    Ziel, (Rolle, Halten, Mund)). Ziel: ("hand", Punkt) – Mitte der Hand –, ("mund", Punkt) – Mund des Flakons – oder
    ("uebergang", t) zwischen Halten und Gießen; Rolle: Kippung des Flakons in Grad. Halten (0–0,1), zum Glas heben und kippen
    (bis 0,24), gießen (bis 0,46), zurück (bis 0,58), den Flakon in zwei kleinen Runden schwenken (bis 0,94), halten."""
    s_g, v_g, h_g = MISCHGLAS
    k = p["schulter"] / VOELKER["mensch"]["schulter"]
    halt = Vector((s_g - 0.11, 0.3, 0.94 + 0.08 * k))                 # aufrecht rechts neben dem Glas
    mund = Vector((s_g - 0.01, v_g, h_g + 0.05))                      # über der Öffnung des Glases
    giessen = 0.0
    if phase < 0.1:
        ziel, rolle = ("hand", halt), 0.0
    elif phase < 0.24:
        t = weich((phase - 0.1) / 0.14)
        ziel, rolle, giessen = ("uebergang", t), 68.0 * t, t
    elif phase < 0.46:
        t = weich((phase - 0.24) / 0.22)
        ziel, rolle, giessen = ("mund", mund), 68.0 + 16.0 * math.sin(math.pi * t), 1.0
    elif phase < 0.58:
        t = 1 - weich((phase - 0.46) / 0.12)
        ziel, rolle, giessen = ("uebergang", t), 68.0 * t, t
    elif phase < 0.94:
        u = (phase - 0.58) / 0.36
        w = 4 * math.pi * u
        h = math.sin(math.pi * u)
        ziel = ("hand", halt + Vector((0.03 * (math.cos(w) - 1) * h, 0.03 * math.sin(w) * h, 0.05 * h)))
        rolle = 12.0 * math.sin(w) * h
    else:
        ziel, rolle = ("hand", halt), 0.0
    lehne, hoch = 12 + 6 * giessen, -0.015
    r = {"Ruecken": (lehne, 5 * giessen, 0), "Kopf": (24 - lehne, 6 * giessen, 0)}
    r["Oberschenkel_L"], r["Oberschenkel_R"] = (-6, 0, 5), (4, 0, -5)
    r["Unterschenkel_L"], r["Unterschenkel_R"] = (8, 0, 0), (5, 0, 0)
    r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", (s_g + 0.075, v_g + 0.01, h_g - 0.07))
    if ziel[0] == "hand":
        r["Oberarm_R"], r["Unterarm_R"] = arm_genau(p, r, hoch, "R", tuple(ziel[1]))
    return r, hoch, ziel, (rolle, halt, mund)


def _faust_fuer_mund(p, r, hoch, rolle, mund, start):
    """Rechte Hand so führen, dass der Mund des Flakons (um `rolle` Grad gekippt) bei `mund` liegt. Setzt den rechten Arm in
    `r` und gibt die Hand zurück."""
    mund_ruhe, vorzeichen = flakon_ruhe(p)
    faust = Vector(start)
    for _ in range(40):
        r["Oberarm_R"], unter = arm_genau(p, r, hoch, "R", tuple(faust))
        r["Unterarm_R"] = _rolle(unter, vorzeichen * rolle)
        fehler = mund - _figur(unterarm_lage(p, _bogen(r), hoch, "R") @ mund_ruhe)
        if fehler.length < 2e-4:
            break
        faust += fehler * 0.6
    return faust


_GIESSEN = {}


def mischen(phase, p):
    """Mischen: Der Flakon (starr in der rechten Faust, gebaut beim Halten) gießt mit dem Mund genau über der Öffnung des
    Mischglases (MISCHGLAS), danach schwenkt die Figur ihn; die Linke hält das Glas. Gibt (Winkel in Grad, Hüfte)."""
    r, hoch, ziel, (rolle, halt, mund) = _mischen_ziele(phase, p)
    vorzeichen = flakon_ruhe(p)[1]
    if ziel[0] == "mund":
        _faust_fuer_mund(p, r, hoch, rolle, ziel[1], halt)
    elif ziel[0] == "uebergang":                                        # zwischen Halten und Gießen: Hand und Kippung mischen
        schluessel = tuple(sorted(p.items()))
        if schluessel not in _GIESSEN:                                  # Hand beim Beginn des Gießens (Rumpf ganz geneigt)
            r1, hoch1, _, _ = _mischen_ziele(0.24, p)
            _GIESSEN[schluessel] = _faust_fuer_mund(p, r1, hoch1, 68.0, mund, halt)
        r["Oberarm_R"], unter = arm_genau(p, r, hoch, "R", tuple(halt.lerp(_GIESSEN[schluessel], ziel[1])))
        r["Unterarm_R"] = _rolle(unter, vorzeichen * rolle)
    elif rolle:
        r["Unterarm_R"] = _rolle(r["Unterarm_R"], vorzeichen * rolle)
    return r, hoch


def presse_winkel(phase):
    """Schwenk des Querhebels der Spindelpresse in Grad (+: zur linken Seite der Figur): halten (0–0,1), hinüberschieben (bis
    0,45), halten (bis 0,55), zurückziehen (bis 0,9), halten. Das Raummodell dreht die Spindel genau so (parfumeur_innen)."""
    w = PRESSE["winkel"]
    if phase < 0.1:
        return -w
    if phase < 0.45:
        return -w + 2 * w * weich((phase - 0.1) / 0.35)
    if phase < 0.55:
        return w
    if phase < 0.9:
        return w - 2 * w * weich((phase - 0.55) / 0.35)
    return -w


def _presse_seitlich(winkel):
    """Seitliche Lage der Wurzel bei diesem Schwenk: Die Figur bleibt vor der Mitte zwischen ihren Händen am Hebel."""
    return (PRESSE["rechts"] + PRESSE["links"]) / 2 * math.sin(math.radians(winkel))


def _presse_fuesse(phase, p):
    """Füße beim Pressen: {Seite: (seitlich, Hub)}. Der Fuß in Schubrichtung tritt zuerst zur Seite, der andere folgt;
    beim Zurückziehen umgekehrt."""
    breit = p["hueftbreite"] * 0.7 + 0.03
    a, b = _presse_seitlich(-PRESSE["winkel"]), _presse_seitlich(PRESSE["winkel"])
    lage = {"L": (a + breit, 0.0), "R": (a - breit, 0.0)}
    for anfang, von, nach, vorn in ((0.1, a, b, "L"), (0.55, b, a, "R")):
        if phase < anfang:
            break
        u = min(1.0, (phase - anfang) / 0.35)
        for seite, u0 in ((vorn, 0.0), ("R" if vorn == "L" else "L", 0.42)):
            t = weich((u - u0) / 0.58)
            lage[seite] = ((breit if seite == "L" else -breit) + von + (nach - von) * t, 0.07 * math.sin(math.pi * t))
    return lage


def seitlich(name, phase, p):
    """Seitliche Verschiebung der Wurzel (Meter, + zur linken Seite der Figur) – nur beim Pressen, sonst 0."""
    return _presse_seitlich(presse_winkel(phase)) if name == "Pressen" else 0.0


def pressen(phase, p):
    """Pressen: Beide Hände am Querhebel der Spindelpresse (PRESSE), der um ±winkel schwenkt; die Wurzel geht seitlich mit
    (`seitlich`), die Füße treten zur Seite (bein_ik in der zur Seite gekippten Ebene des Beins, Oberschenkel Z kippt sie).
    Gibt (Winkel in Grad, Hüfte)."""
    w = presse_winkel(phase)
    sx = _presse_seitlich(w)
    a = PRESSE["abstand"]
    if 0.1 <= phase < 0.45:
        kraft = math.sin(math.pi * (phase - 0.1) / 0.35)
    elif 0.55 <= phase < 0.9:
        kraft = math.sin(math.pi * (phase - 0.55) / 0.35)
    else:
        kraft = 0.0
    fuesse = _presse_fuesse(phase, p)
    xb = p["hueftbreite"] * 0.7
    vor = {"L": 0.05, "R": -0.03}
    beine = (p["huefte"] - 0.06) * 0.97                                # Knie immer ein wenig gebeugt
    hoch = -0.025
    for seite, (fs, hub) in fuesse.items():
        ds = fs - (sx + (xb if seite == "L" else -xb))
        hoch = min(hoch, math.sqrt(max(0.0, beine ** 2 - ds ** 2 - vor[seite] ** 2)) + hub - (p["huefte"] - 0.06))
    drehung = -0.7 * math.degrees(math.atan2(sx, a))                  # der Rumpf bleibt zur Spindel gedreht
    lehne = 10 + 5 * kraft
    r = {"Ruecken": (lehne, drehung, 0), "Kopf": (12 - lehne * 0.6, -0.5 * drehung, 0)}
    for seite, (fs, hub) in fuesse.items():
        ds = fs - (sx + (xb if seite == "L" else -xb))
        dz = 0.06 + hub - (p["huefte"] + hoch)
        ober, unter = bein_ik(p, 0.0, vor[seite], p["huefte"] - 0.06 - math.hypot(ds, dz))
        r[f"Oberschenkel_{seite}"] = (ober, 0, math.degrees(math.atan2(-ds, -dz)))
        r[f"Unterschenkel_{seite}"] = (unter, 0, 0)
    for seite in ("R", "L"):
        rh = PRESSE["rechts" if seite == "R" else "links"]
        ziel = (rh * math.sin(math.radians(w)) - sx, a - rh * math.cos(math.radians(w)), PRESSE["hoehe"])
        r[f"Oberarm_{seite}"], r[f"Unterarm_{seite}"] = arm_genau(p, r, hoch, seite, ziel)
    return r, hoch


# ── Wirt: Zapfen und Trinken (der Krug starr in der rechten Faust, sein Boden bzw. sein Mund wird geführt), Laute (am Rücken) ──

def _rumpf_lage(p, pose, hoch, kopf=False):
    """Wie unterarm_lage für den Rücken (mit `kopf` für den Kopf): Matrix, die einen Punkt am Rumpf bzw. Kopf aus der Ruhelage in
    die Haltung bringt (Koordinaten der Figur in Blender, Blick −Y)."""
    from mathutils import Euler
    kette = [("Wurzel", (0, 0, p["huefte"])), ("Ruecken", (0, 0, p["huefte"]))]
    if kopf:
        kette.append(("Kopf", (0, 0, p["schulter"] + p["hals"])))
    m, vorher = Matrix.Identity(4), None
    for name, k in kette:
        ruhe = _ruhe(Vector(k), False)
        lokal = Euler(pose.get(name, (0.0, 0.0, 0.0)), "XYZ").to_matrix().to_4x4()
        if name == "Wurzel":
            lokal = Matrix.Translation((0, hoch, 0)) @ lokal
        m = (ruhe if vorher is None else m @ vorher.inverted() @ ruhe) @ lokal
        vorher = ruhe
    return m @ vorher.inverted()


def _mund_ruhe(p):
    """Mitte des Mundes in der Ruhelage (Blender): vorn am Kopf, unter der Nase."""
    r = p["kopf"]
    return Vector((0, -r * 0.92, p["schulter"] + p["hals"] + r * 0.95 - r * 0.42))


def _faust_fuer(p, r, hoch, punkt_ruhe, ziel, start, rolle=0.0, seite="R"):
    """Hand der Seite so führen, dass `punkt_ruhe` (ein Punkt am Unterarm in der Ruhelage, etwa der Boden des Krugs) bei `ziel`
    (seitlich, vor, Höhe) liegt; der Unterarm ist dabei um `rolle` Grad um seine Achse gedreht. Setzt den Arm in `r` und gibt
    die Hand zurück."""
    faust = Vector(start)
    for _ in range(40):
        r[f"Oberarm_{seite}"], unter = arm_genau(p, r, hoch, seite, tuple(faust))
        r[f"Unterarm_{seite}"] = _rolle(unter, rolle) if rolle else unter
        fehler = Vector(ziel) - _figur(unterarm_lage(p, _bogen(r), hoch, seite) @ punkt_ruhe)
        if fehler.length < 2e-4:
            break
        faust += fehler * 0.6
    return faust


def _zapfen_rumpf(stand, p):
    """Rumpf, Beine und Hüfte einer Haltung beim Zapfen: „warten“ (aufrecht), „zapf“ (zum Hahn rechts gedreht), „ab“ (nach
    links zur Abstellstelle)."""
    lehne, drehung = {"warten": (6, 0), "zapf": (11, -12), "ab": (10, 8)}[stand]
    hoch = -0.012
    r = {"Ruecken": (lehne, drehung, 0), "Kopf": (20 - lehne, -0.5 * drehung, 0)}
    return r, hoch


def _zapfen_mix(a, b, t, p):
    """Rumpf zwischen zwei Haltungen des Zapfens."""
    (ra, ha), (rb, hb) = _zapfen_rumpf(a, p), _zapfen_rumpf(b, p)
    return {k: tuple(x + (y - x) * t for x, y in zip(ra[k], rb[k])) for k in ra}, ha + (hb - ha) * t


def _zapfen_warten(p):
    """Rechte Hand beim Warten (mit dem leeren Krug über der Theke) und linke Hand auf der Theke."""
    k = p["schulter"] / VOELKER["mensch"]["schulter"]
    return Vector((-0.12, 0.33, THEKE + 0.12 + 0.05 * (k - 1))), Vector((0.2, 0.36, THEKE + 0.035))


_KRUG = {}


def krug_ruhe(p):
    """Boden und Mund (Mitte) des Krugs in der Ruhelage des rechten Unterarms (Blender). Gebaut ist er in der Haltung von
    REQUISITEN (Zapfen, Phase 0: „warten“, aufrecht in der Faust)."""
    schluessel = tuple(sorted(p.items()))
    if schluessel not in _KRUG:
        r, hoch = _zapfen_rumpf("warten", p)
        rechts, _ = _zapfen_warten(p)
        r["Oberarm_R"], r["Unterarm_R"] = arm_genau(p, r, hoch, "R", tuple(rechts))
        lage = unterarm_lage(p, _bogen(r), hoch, "R")
        boden = lage @ hand_ruhe(p, "R") + Vector((KRUG["seite"], 0, -KRUG["tief"]))
        _KRUG[schluessel] = (lage.inverted() @ boden, lage.inverted() @ (boden + Vector((0, 0, KRUG["h"]))))
    return _KRUG[schluessel]


_ZAPFEN = {}


def zapfen_faeuste(p):
    """Rechte Hand in den drei Haltungen des Zapfens: warten (fest), zapf (der Boden des Krugs 2 cm unter der Tülle) und ab
    (der Boden auf der Theke bei KRUG_AB)."""
    schluessel = tuple(sorted(p.items()))
    if schluessel not in _ZAPFEN:
        warten, _ = _zapfen_warten(p)
        boden = krug_ruhe(p)[0]
        faeuste = {"warten": warten}
        for stand, ziel in (("zapf", (ZAPFHAHN[0], ZAPFHAHN[1], ZAPFHAHN[2] - KRUG["h"] - 0.02)),
                            ("ab", (KRUG_AB[0], KRUG_AB[1], KRUG_AB[2] + 0.004))):
            r, hoch = _zapfen_rumpf(stand, p)
            faeuste[stand] = _faust_fuer(p, r, hoch, boden, ziel, warten)
        _ZAPFEN[schluessel] = faeuste
    return _ZAPFEN[schluessel]


def zapfen(phase, p):
    """Zapfen (4 s): warten (0–0,1), den Krug unter den Hahn führen (bis 0,24), die Linke greift das Küken (bis 0,26) und dreht
    es auf (bis 0,32), füllen (bis 0,6), zudrehen (bis 0,66), den vollen Krug nach links tragen und auf der Theke abstellen (bis
    0,84), stehen lassen (bis 0,9), den nächsten nehmen (zurück nach „warten“). Das Raummodell lässt in derselben Zeit Bier aus
    der Tülle laufen (wirt_innen). Gibt (Winkel in Grad, Hüfte)."""
    faeuste = zapfen_faeuste(p)
    warten_l = _zapfen_warten(p)[1]
    folge = ((0.1, "warten", "warten"), (0.24, "warten", "zapf"), (0.66, "zapf", "zapf"), (0.84, "zapf", "ab"),
             (0.9, "ab", "ab"), (1.0, "ab", "warten"))
    anfang = 0.0
    for ende, a, b in folge:
        if phase <= ende:
            t = weich((phase - anfang) / (ende - anfang)) if a != b else 0.0
            break
        anfang = ende
    r, hoch = _zapfen_mix(a, b, t, p)
    rechts = faeuste[a].lerp(faeuste[b], t)
    if (a, b) == ("zapf", "ab"):
        rechts += Vector((0, -0.04, 0.08)) * math.sin(math.pi * t)                  # im Bogen über die Theke
    zu, auf = Vector(HAHN_GRIFF), Vector(HAHN_GRIFF) + Vector((0.01, -0.025, -0.02))
    if phase < 0.12:
        links = warten_l
    elif phase < 0.26:
        links = warten_l.lerp(zu, weich((phase - 0.12) / 0.14))
    elif phase < 0.32:
        links = zu.lerp(auf, weich((phase - 0.26) / 0.06))
    elif phase < 0.6:
        links = auf
    elif phase < 0.66:
        links = auf.lerp(zu, weich((phase - 0.6) / 0.06))
    elif phase < 0.8:
        links = zu.lerp(warten_l, weich((phase - 0.66) / 0.14))
    else:
        links = warten_l
    r["Oberarm_R"], r["Unterarm_R"] = arm_genau(p, r, hoch, "R", tuple(rechts))
    r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", tuple(links))
    if 0.32 <= phase < 0.6:                                                        # schaut in den Krug
        r["Kopf"] = (r["Kopf"][0] + 6 * math.sin(math.pi * (phase - 0.32) / 0.28), r["Kopf"][1], 0)
    for seite, vor, z in (("L", 0.05, 5), ("R", -0.03, -5)):
        ober, unter = bein_ik(p, hoch, vor, 0.0)
        r[f"Oberschenkel_{seite}"], r[f"Unterschenkel_{seite}"] = (ober, 0, z), (unter, 0, 0)
    return r, hoch


def _trinken_rumpf(stand, p, phase=0.0):
    """Rumpf, Beine und Hüfte im Sitzen beim Trinken: „tisch“ (vorgeneigt, der Krug auf dem Tisch) oder „mund“ (aufgerichtet,
    der Kopf in den Nacken)."""
    hoch = -(p["huefte"] - (p["knie"] - 0.02)) + 0.004 * math.sin(2 * math.pi * phase)
    lehne, kopf = {"tisch": (10, 8), "mund": (-2, -16)}[stand]
    r = {"Ruecken": (lehne, 0, 0), "Kopf": (kopf, 0, 0)}
    r["Oberschenkel_L"], r["Oberschenkel_R"] = (-86, 0, 7), (-86, 0, -7)
    r["Unterschenkel_L"], r["Unterschenkel_R"] = (84, 0, 0), (80, 0, 0)
    return r, hoch


_TRINKEN = {}


def trinken_faeuste(p):
    """Rechte Hand und Drehung des Unterarms (Grad) beim Trinken: „tisch“ – der Boden des Krugs auf dem Tisch (KRUG_TISCH) –
    und „mund“ – der Mund des Krugs an den Lippen, der Krug so gekippt, dass er möglichst steil zum Gesicht zeigt."""
    schluessel = tuple(sorted(p.items()))
    if schluessel not in _TRINKEN:
        boden, mund_k = krug_ruhe(p)
        sitz = p["knie"] - 0.02

        def drehen(r, hoch, punkt, ziel, start, soll, rollen):
            """Hand und Drehung des Unterarms, bei denen `punkt` des Krugs bei `ziel` liegt und seine Achse am besten `soll` folgt."""
            beste = None
            for rolle in rollen:
                faust = _faust_fuer(p, r, hoch, punkt, ziel, start, rolle)
                lage = unterarm_lage(p, _bogen(r), hoch, "R")
                achse = (_figur(lage @ mund_k) - _figur(lage @ boden)).normalized()
                wert = achse.dot(soll) - 5 * (_figur(lage @ punkt) - Vector(ziel)).length
                if beste is None or wert > beste[0]:
                    beste = (wert, faust, float(rolle))
            return beste[1], beste[2]
        r, hoch = _trinken_rumpf("tisch", p)
        start = Vector((KRUG_TISCH[0] - KRUG["seite"], KRUG_TISCH[1], sitz + KRUG_TISCH[2] + KRUG["tief"]))
        tisch = drehen(r, hoch, boden, (KRUG_TISCH[0], KRUG_TISCH[1], sitz + KRUG_TISCH[2] + 0.004), start, Vector((0, 0, 1)),
                       range(-60, 61, 10))                                        # aufrecht auf dem Tisch
        r, hoch = _trinken_rumpf("mund", p)
        lippen = _figur(_rumpf_lage(p, _bogen(r), hoch, kopf=True) @ _mund_ruhe(p))
        mund = drehen(r, hoch, mund_k, lippen + Vector((0, 0.035, 0.005)), lippen + Vector((-0.08, 0.1, -0.1)),
                      Vector((0, -math.sin(math.radians(60)), math.cos(math.radians(60)))), range(-90, 91, 10))   # steil zum Gesicht
        _TRINKEN[schluessel] = {"tisch": tisch, "mund": mund}
    return _TRINKEN[schluessel]


def trinken(phase, p):
    """Trinken im Sitzen (4 s): der Krug auf dem Tisch, die Linke redet mit (0–0,3), den Krug an den Mund heben (bis 0,44),
    trinken – Kopf in den Nacken (bis 0,62), absetzen (bis 0,76), ruhen. Gibt (Winkel in Grad, Hüfte)."""
    faeuste = trinken_faeuste(p)
    folge = ((0.3, "tisch", "tisch"), (0.44, "tisch", "mund"), (0.62, "mund", "mund"), (0.76, "mund", "tisch"),
             (1.0, "tisch", "tisch"))
    anfang = 0.0
    for ende, a, b in folge:
        if phase <= ende:
            t = weich((phase - anfang) / (ende - anfang)) if a != b else 0.0
            break
        anfang = ende
    ra, ha = _trinken_rumpf(a, p, phase)
    rb, hb = _trinken_rumpf(b, p, phase)
    r = {k: tuple(x + (y - x) * t for x, y in zip(ra[k], rb[k])) for k in ra}
    hoch = ha + (hb - ha) * t
    (fa, wa), (fb, wb) = faeuste[a], faeuste[b]
    rechts, rolle = fa.lerp(fb, t), wa + (wb - wa) * t
    if a == b == "mund":                                                          # beim Trinken kippt der Krug weiter
        u = (phase - anfang) / (ende - anfang)
        rolle += 8 * math.sin(math.pi * u) * (1 if wa >= faeuste["tisch"][1] else -1)
        r["Kopf"] = (r["Kopf"][0] - 4 * math.sin(math.pi * u), 0, 0)
    if a != b:                                                                     # im Bogen: nicht durch die Tischkante
        rechts += Vector((0, -0.05, 0.03)) * math.sin(math.pi * t)
    r["Oberarm_R"], unter = arm_genau(p, r, hoch, "R", tuple(rechts))
    r["Unterarm_R"] = _rolle(unter, rolle) if rolle else unter
    sitz = p["knie"] - 0.02
    links = Vector((0.2, 0.34, sitz + KRUG_TISCH[2] + 0.035))
    if phase < 0.3:                                                                # die Linke redet mit
        u = phase / 0.3
        links += Vector((-0.04, -0.06, 0.16)) * math.sin(math.pi * u) + Vector((0.03, 0, 0.03)) * math.sin(3 * math.pi * u)
    r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", tuple(links))
    return r, hoch


def laute_lage(p):
    """Lage der Laute am Rumpf in der Ruhelage (Blender): Mitte der Decke `c` vor dem Bauch etwas rechts, Längsachse `a` (zum
    Hals: nach links oben), Richtung der Decke `f` (nach vorn, leicht hoch), Maßstab `k` (nach der Schulterhöhe des Volks)."""
    k = p["schulter"] / VOELKER["mensch"]["schulter"]
    hu, sc = p["huefte"], p["schulter"]
    c = Vector((-0.07 * k, -(p["tiefe"] + 0.12 * k), hu + (sc - hu) * 0.42))
    a = Vector((0.8, -0.1, 0.56)).normalized()
    f = Vector((0, -1, 0.2))
    f = (f - a * f.dot(a)).normalized()
    return c, a, f, k


def laute(phase, p):
    """Laute (2 s): Der Barde steht, wiegt sich und wippt; die Rechte schlägt viermal die Saiten über der Rosette (quer zu den
    Saiten), die Linke greift am Hals und wandert ein wenig. Die Laute hängt am Rücken (Trachtteil), die Hände folgen ihr. Gibt
    (Winkel in Grad, Hüfte)."""
    s = math.sin(2 * math.pi * phase)
    hoch = -0.01 - 0.008 * abs(math.sin(4 * math.pi * phase))
    r = {"Ruecken": (4 + 1.5 * s, 6, 2 * s), "Kopf": (14, 10, -6 + 4 * s)}
    lage = _rumpf_lage(p, _bogen(r), hoch)
    c, a, f, k = laute_lage(p)
    q = a.cross(f)
    schlag = math.sin(8 * math.pi * phase)
    rechts = c - a * 0.05 * k + f * 0.06 * k + q * 0.035 * k * schlag
    links = c + a * (0.3 + 0.04 * math.sin(2 * math.pi * phase + 0.5)) * k - f * 0.01 * k - q * 0.03 * k
    r["Oberarm_R"], r["Unterarm_R"] = arm_genau(p, r, hoch, "R", tuple(_figur(lage @ rechts)))
    r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", tuple(_figur(lage @ links)))
    for seite, vor, z in (("L", 0.07, 6), ("R", -0.05, -6)):
        ober, unter = bein_ik(p, hoch, vor, 0.0)
        r[f"Oberschenkel_{seite}"], r[f"Unterschenkel_{seite}"] = (ober, 0, z), (unter, 0, 0)
    return r, hoch


# ── Prediger: Kerzenziehen und Schöpfen (beide Fäuste halten ein Gerät quer), Schreiben, Predigen, Beten, Knien ──────────

def _beide_haende(p, vor, hoehe, unter):
    """Beide Hände in der Ebene ihrer Schultern `vor` Meter vor dem Fußpunkt auf `hoehe`, die Unterarme in der Welt `unter` Grad
    aus der Senkrechten nach vorn gehoben (0 hängend, 90 waagrecht): Alle Knochen drehen nur um die Querachse, so bleibt, was die
    Fäuste quer halten (Dochtstab, Sieb), in jeder Haltung gleich ausgerichtet. Die Höhe gleichen die Neigung des Rumpfs und die
    Kniebeuge aus (je mehr Neigung, desto tiefer die Hüfte); gewählt wird die kleinste Neigung, bei der der Ellbogen nach vorn
    knickt. Gibt (Neigung, Hüfte, Oberarm, Unterarm, Fehler in Metern, ob es die kleinste Neigung überhaupt ist) – Winkel in Grad
    wie arm_ik."""
    rumpf, l1, l2 = p["schulter"] - 0.03 - p["huefte"], p["oberarm"], p["unterarm"] * 0.72 + 0.06
    b = math.radians(unter)
    ex, ez = vor - l2 * math.sin(b), hoehe + l2 * math.cos(b)          # dort muss der Ellbogen sein
    tief = 0.28 * p["huefte"] / VOELKER["mensch"]["huefte"]

    def schulter(lehne):
        w = math.radians(lehne)
        hoch = -tief * weich((lehne - 5) / 45)
        return rumpf * math.sin(w), p["huefte"] + hoch + rumpf * math.cos(w), hoch

    def abstand(lehne):
        sx, sz, _ = schulter(lehne)
        return math.hypot(ex - sx, ez - sz) - l1

    def oberarm(lehne):
        sx, sz, _ = schulter(lehne)
        return math.atan2(ex - sx, -(ez - sz))

    def knickt_vor(lehne):
        return -0.05 <= b - oberarm(lehne) <= math.radians(150)
    lehnen = [-10.0 + 0.25 * k for k in range(361)]
    wurzeln = []
    for l0, l1_ in zip(lehnen, lehnen[1:]):
        if (abstand(l0) <= 0) == (abstand(l1_) <= 0):
            continue
        a_, b_ = l0, l1_
        for _ in range(40):
            m = (a_ + b_) / 2
            a_, b_ = (a_, m) if (abstand(a_) <= 0) != (abstand(m) <= 0) else (m, b_)
        wurzeln.append((a_ + b_) / 2)
    gute = [w for w in wurzeln if knickt_vor(w)]
    if gute:
        lehne, fehler, erste = gute[0], 0.0, gute[0] == wurzeln[0]
    else:                                                              # nicht erreichbar: so nah wie möglich
        lehne = min(lehnen, key=lambda l: abs(abstand(l)) + (0.0 if knickt_vor(l) else 1.0))
        fehler, erste = abs(abstand(lehne)), False
    a = math.degrees(oberarm(lehne))
    return lehne, schulter(lehne)[2], -(a + lehne), -(unter - a), fehler, erste


_UNTERARM = {}


def _unterarm_fest(name, p, ziel):
    """Richtung der Unterarme (Grad aus der Senkrechten) für eine Bewegung mit quer gehaltenem Gerät, je Volk eigen und in der
    ganzen Bewegung fest: Die Fäuste müssen in jeder Phase ihr Ziel `ziel(phase)` = (vor, Höhe) treffen, die Neigung darf nicht
    springen (immer die kleinste Lösung), und unter allen solchen Richtungen gilt die, bei der sich der Rumpf am wenigsten neigt
    und die Knie am wenigsten beugen."""
    schluessel = (name, tuple(sorted(p.items())))
    if schluessel not in _UNTERARM:
        beste = None
        for unter in range(0, 121, 2):
            erg = [_beide_haende(p, *ziel(k / 96), unter) for k in range(97)]
            fehler = sum(e[4] for e in erg) / len(erg) + sum(0.05 for e in erg if not e[5])
            sprung = max(abs(a[0] - b[0]) for a, b in zip(erg, erg[1:]))
            wert = 20 * fehler + (1.0 if sprung > 10 else 0.0) + max(abs(e[0]) for e in erg) / 60 + 3 * max(-e[1] for e in erg)
            if beste is None or wert < beste[0]:
                beste = (wert, unter)
        _UNTERARM[schluessel] = beste[1]
    return _UNTERARM[schluessel]


def _quer_halten(name, p, phase, ziel, kopf):
    """Haltung mit beiden Fäusten bei ziel(phase) = (vor, Höhe) und fester Richtung der Unterarme (_unterarm_fest), Füße flach
    am Boden, der Kopf um `kopf` Grad zum Gerät geneigt. Gibt (Winkel in Grad, Hüfte)."""
    unter = _unterarm_fest(name, p, ziel)
    lehne, hoch, ober, unter_k = _beide_haende(p, *ziel(phase), unter)[:4]
    r = {"Ruecken": (lehne, 0, 0), "Kopf": (kopf - 0.6 * lehne, 0, 0)}
    for seite in ("L", "R"):
        r[f"Oberarm_{seite}"], r[f"Unterarm_{seite}"] = (ober, 0, 0), (unter_k, 0, 0)
    for seite, v, z in (("L", 0.06, 4), ("R", -0.04, -4)):
        o, u = bein_ik(p, hoch, v, 0.0)
        r[f"Oberschenkel_{seite}"], r[f"Unterschenkel_{seite}"] = (o, 0, z), (u, 0, 0)
    return r, hoch


def kerzen_hoehen(p):
    """Höhe der Fäuste (des Dochtstabs) beim Kerzenziehen: über dem Trog (die Kerzen 5 cm über dem Rand) und eingetaucht."""
    k2 = (p["schulter"] / VOELKER["mensch"]["schulter"]) ** 2
    unter = 0.042 + KERZEN["kerze"] * k2                                     # Faust bis zur Unterkante der Kerzen
    return KERZEN["rand"] + 0.05 + unter, KERZEN["wachs"] - KERZEN["tief"] * k2 + unter


def kerzen_ziel(phase, p):
    """Fäuste beim Kerzenziehen (vor, Höhe): Die Kerzen hängen über dem Wachstrog (0–0,1), sinken langsam hinein (bis 0,3),
    bleiben im Wachs (bis 0,42), werden herausgehoben (bis 0,6) und tropfen ab (bis 1)."""
    oben, unten = kerzen_hoehen(p)
    if phase < 0.1:
        h = oben
    elif phase < 0.3:
        h = oben + (unten - oben) * weich((phase - 0.1) / 0.2)
    elif phase < 0.42:
        h = unten
    elif phase < 0.6:
        h = unten + (oben - unten) * weich((phase - 0.42) / 0.18)
    else:
        h = oben
    return KERZEN["vor"], h


def kerzenziehen(phase, p):
    """Kerzenziehen (4 s, kerzen_ziel): den Dochtstab mit beiden Fäusten senken, die Kerzen ins Wachs tauchen, heben, abtropfen
    lassen. Gibt (Winkel in Grad, Hüfte)."""
    return _quer_halten("Kerzenziehen", p, phase, lambda ph: kerzen_ziel(ph, p), 34)


def schoepf_punkte(p):
    """Lagen der Fäuste beim Schöpfen (vor, Höhe); das Gitter des Siebs liegt 1,5 cm unter ihnen."""
    b = BUETTE
    oben, unten = b["rand"] + b["ueber"] + 0.015, b["brei"] - b["unter"] + 0.015
    return {"tropf": (b["tropf"], oben), "fern": (b["fern"], unten), "nah": (b["nah"], unten)}


def schoepf_ziel(phase, p):
    """Fäuste beim Schöpfen (vor, Höhe): vom Abtropfen schräg nach hinten unten, hinten in den Brei tauchen (0–0,22), unter dem
    Brei zur Figur ziehen (bis 0,4), waagrecht herausheben (bis 0,5), schütteln – vor und zurück, damit sich die Fasern
    verfilzen (bis 0,64), abtropfen lassen (bis 1). Das Sieb bleibt dabei immer über dem Inneren der Bütte."""
    punkte = schoepf_punkte(p)
    folge = ((0.22, "tropf", "fern"), (0.4, "fern", "nah"), (0.5, "nah", "tropf"), (1.0, "tropf", "tropf"))
    anfang = 0.0
    for ende, a, b in folge:
        if phase <= ende:
            t = weich((phase - anfang) / (ende - anfang)) if a != b else 0.0
            break
        anfang = ende
    (va, ha), (vb, hb) = punkte[a], punkte[b]
    v, h = va + (vb - va) * t, ha + (hb - ha) * t
    if 0.5 <= phase < 0.64:
        v += 0.025 * math.sin(2 * math.pi * 3 * (phase - 0.5) / 0.14)
    return v, h


def schoepfen(phase, p):
    """Schöpfen (4 s, schoepf_ziel): das Sieb waagrecht durch die Bütte ziehen, schütteln, abtropfen lassen – dann lässt das
    Raummodell Tropfen aus dem Sieb fallen (prediger_innen). Gibt (Winkel in Grad, Hüfte)."""
    return _quer_halten("Schoepfen", p, phase, lambda ph: schoepf_ziel(ph, p), 30)


def _schreiben_rumpf(phase, p):
    """Sitzen am Pult: Hüfte auf Sitzhöhe, vorgeneigt, der Kopf über dem Blatt."""
    hoch = -(p["huefte"] - (p["knie"] - 0.02)) + 0.003 * math.sin(2 * math.pi * phase)
    r = {"Ruecken": (16, 0, 0), "Kopf": (24, 0, 0)}
    r["Oberschenkel_L"], r["Oberschenkel_R"] = (-86, 0, 6), (-86, 0, -6)
    r["Unterschenkel_L"], r["Unterschenkel_R"] = (84, 0, 0), (80, 0, 0)
    return r, hoch


def _schreiben_spitze(phase, p):
    """Spitze der Feder (seitlich, vor, Höhe) zur Phase und wie weit der Blick beim Tintenhorn ist (0 … 1): eine Zeile schreiben
    (0–0,5; kleine Auf- und Abstriche, zwischen den Wörtern kurz abgesetzt), die Feder ins Tintenhorn tauchen (bis 0,74), zurück
    an den Anfang der nächsten Zeile (bis 0,86), ansetzen und kurz innehalten."""
    sitz = p["knie"] - 0.02
    n = math.radians(SCHREIBEN["neigung"])
    hoch_auf_blatt = Vector((0, math.cos(n), math.sin(n)))                       # auf dem Blatt nach oben (weg von der Figur)
    ab = Vector((0, -math.sin(n), math.cos(n)))                                  # vom Blatt weg (zur Figur und hoch)
    s0, v0, h0 = SCHREIBEN["blatt"]
    start = Vector((s0, v0, sitz + h0))
    ende = start - Vector((SCHREIBEN["zeile"], 0, 0))
    hs, hv, hh = SCHREIBEN["horn"]
    horn_oben, horn_tief = Vector((hs, hv, sitz + hh + 0.04)), Vector((hs, hv, sitz + hh - 0.03))
    if phase < 0.5:
        u = phase / 0.5
        return (start.lerp(ende, u) + hoch_auf_blatt * 0.006 * math.sin(2 * math.pi * 12 * u)
                + ab * 0.004 * max(0.0, math.sin(2 * math.pi * 3 * u)) ** 4), 0.0
    wege = ((0.6, ende, horn_oben, 0.0, 1.0), (0.66, horn_oben, horn_tief, 1.0, 1.0), (0.74, horn_tief, horn_oben, 1.0, 1.0),
            (0.86, horn_oben, start + ab * 0.02, 1.0, 0.0), (0.92, start + ab * 0.02, start, 0.0, 0.0), (1.0, start, start, 0.0, 0.0))
    anfang = 0.5
    for bis, a, b, blick_a, blick_b in wege:
        if phase <= bis:
            t = weich((phase - anfang) / (bis - anfang))
            bogen = ab * 0.03 * math.sin(math.pi * t) if bis == 0.6 else Vector()
            return a.lerp(b, t) + bogen, blick_a + (blick_b - blick_a) * t
        anfang = bis
    return start, 0.0


_FEDER = {}


def feder_ruhe(p):
    """Spitze der Feder in der Ruhelage des rechten Unterarms (Blender) und die Faust beim Ansetzen (seitlich, vor, Höhe): Gebaut
    ist die Feder in der Haltung von REQUISITEN (Schreiben, Phase 0: die Spitze am Anfang der Zeile, die Faust 3 cm rechts, 6 cm
    näher und 6,5 cm höher)."""
    schluessel = tuple(sorted(p.items()))
    if schluessel not in _FEDER:
        r, hoch = _schreiben_rumpf(0.0, p)
        spitze = _schreiben_spitze(0.0, p)[0]
        hand = spitze + Vector((-0.03, -0.06, 0.065))
        r["Oberarm_R"], r["Unterarm_R"] = arm_genau(p, r, hoch, "R", tuple(hand))
        lage = unterarm_lage(p, _bogen(r), hoch, "R")
        _FEDER[schluessel] = (lage.inverted() @ _blender(spitze), hand)
    return _FEDER[schluessel]


def schreiben(phase, p):
    """Schreiben (4 s, sitzend am Pult): Die Rechte führt die Spitze der Feder (_schreiben_spitze), die Linke hält das Blatt, der
    Kopf folgt der Feder und dreht sich zum Tintenhorn. Gibt (Winkel in Grad, Hüfte)."""
    r, hoch = _schreiben_rumpf(phase, p)
    spitze_ruhe, hand0 = feder_ruhe(p)
    ziel, horn = _schreiben_spitze(phase, p)
    start = _schreiben_spitze(0.0, p)[0]
    _faust_fuer(p, r, hoch, spitze_ruhe, tuple(ziel), hand0 + (ziel - start))
    sitz = p["knie"] - 0.02
    links = Vector((0.18, 0.37, sitz + 0.37)) + Vector((0, 0.01, 0.005)) * math.sin(2 * math.pi * phase)
    r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", tuple(links))
    zeile = min(1.0, phase / 0.5) if phase < 0.5 else 0.0
    r["Kopf"] = (24 + 4 * horn, -14 * horn - 6 * zeile + 3, 0)
    return r, hoch


def _predigt_folge(p):
    """Schlüsselhaltungen beim Predigen: (Phase, rechte Hand, linke Hand, Neigung, Drehung des Rumpfs, Kopf nicken, Kopf drehen).
    Hände als (seitlich, vor, Höhe); auf der Brüstung der Kanzel (KANZEL) liegen sie bei jedem Volk gleich hoch."""
    k = p["schulter"] / VOELKER["mensch"]["schulter"]
    sc = p["schulter"]
    vor, h = KANZEL
    br, bl = Vector((-0.2 * k, vor, h + 0.03)), Vector((0.2 * k, vor, h + 0.03))
    oben = Vector((-0.1 * k, 0.34 * k, sc + 0.36 * k))
    offen_r, offen_l = Vector((-0.46 * k, 0.3 * k, sc - 0.1 * k)), Vector((0.42 * k, 0.3 * k, sc - 0.14 * k))
    weiter = Vector((0, 0.04 * k, 0.04 * k))
    return ((0.0, br, bl, 5, 0, 10, 0), (0.14, br, bl, 5, 0, 6, 16), (0.3, oben, bl, 1, -6, -10, -4),
            (0.44, oben + Vector((0.02 * k, 0.03 * k, 0.03 * k)), bl, 0, -8, -12, -10), (0.6, offen_r, offen_l, 2, -14, 0, -20),
            (0.74, offen_r + weiter, offen_l + weiter, 2, 12, 0, 20), (0.88, br, bl, 6, 0, 12, 0), (1.0, br, bl, 5, 0, 10, 0))


def predigen(phase, p):
    """Predigen (6 s, stehend auf der Kanzel): die Hände auf der Brüstung, der Blick über die Gemeinde (0–0,14), die Rechte weist
    nach oben (bis 0,44), beide Arme öffnen sich zur Gemeinde, der Rumpf dreht sich von rechts nach links (bis 0,74), zurück auf
    die Brüstung. Gibt (Winkel in Grad, Hüfte)."""
    folge = _predigt_folge(p)
    for a, b in zip(folge, folge[1:]):
        if phase <= b[0]:
            t = weich((phase - a[0]) / (b[0] - a[0]))
            break
    rechts, links = a[1].lerp(b[1], t), a[2].lerp(b[2], t)
    lehne, dreh, nick, kopf = (a[i] + (b[i] - a[i]) * t for i in range(3, 7))
    hoch = -0.008
    r = {"Ruecken": (lehne, dreh, 0), "Kopf": (nick, kopf - 0.5 * dreh, 0)}
    r["Oberarm_R"], r["Unterarm_R"] = arm_genau(p, r, hoch, "R", tuple(rechts))
    r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", tuple(links))
    for seite, vor, z in (("L", 0.06, 5), ("R", -0.04, -5)):
        o, u = bein_ik(p, hoch, vor, 0.0)
        r[f"Oberschenkel_{seite}"], r[f"Unterschenkel_{seite}"] = (o, 0, z), (u, 0, 0)
    return r, hoch


def _gefaltet(p, r, hoch):
    """Die Hände vor der Brust gefaltet (die Fäuste berühren sich), nach der Hüfte der Haltung."""
    k = p["schulter"] / VOELKER["mensch"]["schulter"]
    h = p["huefte"] + hoch + 0.6 * (p["schulter"] - p["huefte"])
    for seite, s in (("L", 1), ("R", -1)):
        r[f"Oberarm_{seite}"], r[f"Unterarm_{seite}"] = arm_genau(p, r, hoch, seite, (s * 0.028 * k, 0.21 * k, h))


def beten(phase, p):
    """Beten (4 s, sitzend auf der Bank): die Hände gefaltet, der Kopf geneigt, ruhiges Atmen. Gibt (Winkel in Grad, Hüfte)."""
    s = math.sin(2 * math.pi * phase)
    hoch = -(p["huefte"] - (p["knie"] - 0.02)) + 0.003 * s
    r = {"Ruecken": (5 + 1.5 * s, 0, 0), "Kopf": (20 + 3 * s, 0, 0)}
    r["Oberschenkel_L"], r["Oberschenkel_R"] = (-88, 0, 4), (-88, 0, -4)
    r["Unterschenkel_L"], r["Unterschenkel_R"] = (88, 0, 0), (88, 0, 0)
    _gefaltet(p, r, hoch)
    return r, hoch


def knien(phase, p):
    """Knien (4 s, etwa am Beichtstuhl): auf beiden Knien, die Unterschenkel liegen nach hinten, die Hände gefaltet, der Kopf
    geneigt; die Knie 6,5 cm über dem Fußpunkt (dort liegt die Kniebank). Gibt (Winkel in Grad, Hüfte)."""
    s = math.sin(2 * math.pi * phase)
    hoch = 0.065 - p["knie"] + 0.003 * s
    r = {"Ruecken": (3 + 1.0 * s, 0, 0), "Kopf": (22 + 3 * s, 0, 0)}
    r["Oberschenkel_L"], r["Oberschenkel_R"] = (-4, 0, 3), (-4, 0, -3)
    r["Unterschenkel_L"], r["Unterschenkel_R"] = (92, 0, 0), (92, 0, 0)
    _gefaltet(p, r, hoch)
    return r, hoch


PREDIGER = {"Kerzenziehen": kerzenziehen, "Schoepfen": schoepfen, "Schreiben": schreiben, "Predigen": predigen, "Beten": beten,
            "Knien": knien}


# ── Dieb: Schloss knacken, Beute zählen, schlafen ────────────────────────────────────────────────────────────────────────

def _knacken_rumpf(p):
    """Neigung des Rumpfs (Grad) und Hüfte beim Knacken: Wer größer ist, beugt sich zum Schloss hinunter, der Kopf nah am Brett;
    Zwerge und Halblinge stehen fast aufrecht."""
    ueber = p["schulter"] - KNACKEN["schloss"][2]
    return max(0.0, min(11.0, ueber * 30.0)), -max(0.0, min(0.03, (ueber - 0.3) * 0.15))


def knacken(phase, p):
    """Knacken (4 s, stehend am Übungsbrett): den Dietrich ins Schloss führen (0–0,1), stochern und horchen – kleine Bewegungen der
    Hand, der Unterarm dreht ein wenig hin und her, der Kopf dreht das rechte Ohr zum Brett (bis 0,7) –, das Schloss springt: den
    Dietrich drehen (bis 0,78), herausziehen (bis 0,9), kurz ansehen, wieder ansetzen. Die Linke liegt flach auf dem Brett. Gibt
    (Winkel in Grad, Hüfte)."""
    s, v, h = KNACKEN["schloss"]
    ein = Vector((s - 0.005, v + KNACKEN["tief"] - KNACKEN["dietrich"], h - 0.012))
    aus = ein + Vector((0.01, -0.08, -0.03))
    if phase < 0.1:
        t = 1 - weich(phase / 0.1)
    elif phase < 0.78:
        t = 0.0
    elif phase < 0.9:
        t = weich((phase - 0.78) / 0.12)
    else:
        t = 1.0
    rechts = ein.lerp(aus, t)
    horchen = weich((phase - 0.08) / 0.12) * (1 - weich((phase - 0.72) / 0.12))
    rolle = 0.0
    if 0.12 < phase < 0.7:                                                         # stochern: kleine, schnelle Bewegungen
        u = (phase - 0.12) / 0.58
        rechts += Vector((0.002 * math.sin(14 * math.pi * u), 0.0, 0.004 * math.sin(22 * math.pi * u)))
        rolle = 6 * math.sin(10 * math.pi * u)
    elif 0.7 <= phase < 0.78:                                                      # das Schloss springt: den Dietrich drehen
        rechts += Vector((0.0, 0.0, 0.006 * math.sin(math.pi * (phase - 0.7) / 0.08)))
        rolle = 28 * math.sin(math.pi * (phase - 0.7) / 0.08)
    lehne, hoch = _knacken_rumpf(p)
    lehne += 3 * horchen
    r = {"Ruecken": (lehne, 4 + 4 * horchen, 0),
         "Kopf": (10 - 0.5 * lehne + 6 * horchen + 2 * math.sin(2 * math.pi * phase), 32 * horchen, 14 * horchen)}
    r["Oberarm_R"], unter = arm_genau(p, r, hoch, "R", tuple(rechts))
    r["Unterarm_R"] = _rolle(unter, rolle) if rolle else unter
    links = Vector(KNACKEN["hand_l"]) + Vector((0, 0, 0.004 * math.sin(2 * math.pi * phase)))
    r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", tuple(links))
    for seite, vor, z in (("L", 0.08, 5), ("R", -0.05, -5)):
        o, u = bein_ik(p, hoch, vor, 0.0)
        r[f"Oberschenkel_{seite}"], r[f"Unterschenkel_{seite}"] = (o, 0, z), (u, 0, 0)
    return r, hoch


def _zaehlen_punkte(p):
    """Wohin die Rechte beim Zählen greift (seitlich, vor, Höhe): in den Haufen (zwei Stellen), über die Öffnung des Beutels und
    der Scheitel des Bogens dazwischen; dazu die Linke an der Seite des Beutels."""
    sitz = p["knie"] - 0.02
    hs, hv, hh = ZAEHLEN["haufen"]
    bs_, bv, bh = ZAEHLEN["beutel"]
    tisch = sitz + hh
    oben = tisch + MUENZBEUTEL["h"] + MUENZBEUTEL["hals"]
    return {"haufen": Vector((hs, hv - 0.02, tisch + 0.035)), "haufen2": Vector((hs + 0.05, hv - 0.05, tisch + 0.035)),
            "beutel": Vector((bs_ - 0.02, bv, oben + 0.035)), "bogen": Vector(((hs + bs_) / 2, (hv + bv) / 2 - 0.02, oben + 0.07)),
            "links": Vector((bs_ + 0.075, bv - 0.01, tisch + MUENZBEUTEL["h"] * 0.6))}


def zaehlen(phase, p):
    """Zählen (4 s, sitzend am Zähltisch): mit den Fingern im Münzhaufen zählen (0–0,1), eine Handvoll über den Beutel heben
    (bis 0,22), hineinfallen lassen (bis 0,36), zurück in den Haufen (bis 0,48), zählen, die nächste Handvoll (bis 0,84), zurück.
    Die Linke hält den Beutel aufrecht und offen; der Kopf folgt der Rechten. Gibt (Winkel in Grad, Hüfte)."""
    q = _zaehlen_punkte(p)
    folge = ((0.0, "haufen"), (0.1, "haufen"), (0.22, "bogen"), (0.3, "beutel"), (0.36, "bogen"), (0.48, "haufen2"), (0.58, "haufen2"),
             (0.7, "bogen"), (0.78, "beutel"), (0.84, "bogen"), (1.0, "haufen"))
    for (a, pa), (b, pb) in zip(folge, folge[1:]):
        if phase <= b:
            t = weich((phase - a) / (b - a)) if pa != pb else 0.0
            break
    rechts = q[pa].lerp(q[pb], t)
    if pa == pb and pa.startswith("haufen"):                                       # zählen: die Finger tippen
        rechts += Vector((0.0, 0.0, 0.012 * abs(math.sin(6 * math.pi * (phase - a) / (b - a)))))
    s = math.sin(2 * math.pi * phase)
    hoch = -(p["huefte"] - (p["knie"] - 0.02)) + 0.003 * s
    lehne = 16 + 1.5 * s
    r = {"Ruecken": (lehne, 0, 0), "Kopf": (26 - lehne * 0.5, 70 * (rechts.x - 0.02), 0)}
    r["Oberschenkel_L"], r["Oberschenkel_R"] = (-86, 0, 7), (-86, 0, -7)
    r["Unterschenkel_L"], r["Unterschenkel_R"] = (84, 0, 0), (82, 0, 0)
    r["Oberarm_R"], r["Unterarm_R"] = arm_genau(p, r, hoch, "R", tuple(rechts))
    r["Oberarm_L"], r["Unterarm_L"] = arm_genau(p, r, hoch, "L", tuple(q["links"]))
    return r, hoch


_SCHLAFEN = {}


def schlaf_arme(p):
    """Winkel (Grad) des linken Arms beim Schlafen – Oberarm (X, Y, Z) und Unterarm (X) –, damit die Hand auf dem Bauch liegt (vor
    der Hüfte zum Kopf hin, knapp links der Mitte, auf der Bauchdecke) und der Ellbogen neben dem Rumpf auf dem Strohsack bleibt;
    gesucht in einem groben, dann feinen Raster. Der rechte Arm ist das Spiegelbild (Y und Z mit umgekehrtem Vorzeichen)."""
    import itertools
    schluessel = tuple(sorted(p.items()))
    if schluessel not in _SCHLAFEN:
        pose, hoch = {"Wurzel": (-90, 0, 0), "Ruecken": (8, 0, 0)}, p["tiefe"] + 0.01 - p["huefte"]
        k = p["schulter"] / VOELKER["mensch"]["schulter"]
        ziel = Vector((0.07 * k, -0.17 * k, 2 * p["tiefe"] + 0.05))
        xs = p["schulterbreite"] + 0.01
        ell_ruhe = Vector((xs, 0, p["schulter"] - 0.03 - p["oberarm"]))

        def guete(w):
            m = unterarm_lage(p, _bogen({**pose, "Oberarm_L": w[:3], "Unterarm_L": (w[3], 0, 0)}), hoch, "L")
            hand, ell = _figur(m @ hand_ruhe(p, "L")), _figur(m @ ell_ruhe)
            return ((hand - ziel).length + 2 * max(0.0, 0.11 - ell.z) + max(0.0, 0.95 * xs - ell.x)
                    + 0.5 * max(0.0, ell.x - xs - 0.1))
        beste = min(itertools.product(range(-60, 11, 6), range(-90, 91, 6), range(-30, 41, 6), range(-140, 1, 6)), key=guete)
        for schritt in (3, 1):
            beste = min(itertools.product(*[(w - schritt, w, w + schritt) for w in beste]), key=guete)
        _SCHLAFEN[schluessel] = beste
    return _SCHLAFEN[schluessel]


def schlafen(phase, p):
    """Schlafen (4 s, auf dem Rücken liegend): ruhig atmen – die Brust hebt und senkt sich –, der Kopf leicht erhöht wie auf einem
    Strohkissen, das linke Knie angezogen, die Hände auf dem Bauch. Die Wurzel kippt nach hinten: Die Hüfte liegt `tiefe` + 1 cm
    über dem Fußpunkt (Oberkante des Strohsacks), der Kopf hinter ihm, die Füße zeigen in Blickrichtung. Gibt (Winkel in Grad,
    Hüfte)."""
    s = math.sin(2 * math.pi * phase)
    hoch = p["tiefe"] + 0.01 - p["huefte"] + 0.002 * s
    r = {"Wurzel": (-90, 0, 0), "Ruecken": (8 + 1.2 * s, 0, 0), "Kopf": (14 - 0.8 * s, 0, 3)}
    ox, oy, oz, ux = schlaf_arme(p)
    r["Oberarm_L"], r["Unterarm_L"] = (ox, oy, oz), (ux - 1.5 * s, 0, 0)          # die Hände heben sich mit dem Atem
    r["Oberarm_R"], r["Unterarm_R"] = (ox, -oy, -oz), (ux - 1.5 * s, 0, 0)
    r["Oberschenkel_L"], r["Unterschenkel_L"] = (-25, 0, 6), (55, 0, 0)
    r["Oberschenkel_R"], r["Unterschenkel_R"] = (-3, 0, -4), (6, 0, 0)
    return r, hoch


DIEB = {"Knacken": knacken, "Zaehlen": zaehlen, "Schlafen": schlafen}


BEWEGUNGEN = {   # Name: Dauer in Sekunden (eine Schleife)
    "Stehen": 3.0, "Gehen": 1.0, "Eilen": 0.7, "Gebeugt": 1.4, "Tragen": 1.2, "Sitzen": 3.0, "Reden": 2.0,
    "Hacken": 1.3, "Buecken": 2.4, "Hueten": 4.0, "Ziehen": 1.3, "Kutschieren": 1.0,
    "Haemmern": 0.9, "Blasebalg": 1.6, "Schleifen": 1.2, "Feinarbeit": 2.0,
    "Hobeln": 1.2, "Saegen": 1.0, "Drechseln": 1.0, "Gatter": 2.0,
    "Behauen": 1.0, "Bildhauen": 1.6,
    "Moersern": 2.0, "Ruehren": 2.4, "Destillieren": 4.0,
    "Mischen": 3.0, "Pressen": 3.0,
    "Zapfen": 4.0, "Trinken": 4.0, "Laute": 2.0,
    "Kerzenziehen": 4.0, "Schoepfen": 4.0, "Schreiben": 4.0, "Predigen": 6.0, "Beten": 4.0, "Knien": 4.0,
    "Knacken": 4.0, "Zaehlen": 4.0, "Schlafen": 4.0,
}


def pose_setzen(ob, pose, hoch, seitlich=0.0):
    for pb in ob.pose.bones:
        pb.rotation_euler = pose.get(pb.name, (0.0, 0.0, 0.0))
        pb.location = (0.0, 0.0, 0.0)
    # Die Wurzel zeigt nach oben (lokales Y = Welt-Z): Hüfthöhe über ihr lokales Y, seitlich über ihr lokales X (+ links)
    ob.pose.bones["Wurzel"].location = (seitlich, hoch, 0.0)


def bewegungen_anlegen(ob, p):
    import bpy
    ob.animation_data_create()
    for name, dauer in BEWEGUNGEN.items():
        bilder = int(round(dauer * FPS))
        act = bpy.data.actions.new(name)
        ob.animation_data.action = act
        for f in range(0, bilder + 1, 2):
            pose, hoch = haltung(name, (f % bilder) / bilder, p)
            pose_setzen(ob, pose, hoch, seitlich(name, (f % bilder) / bilder, p))
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
        bs.arten_setzen(me, alte, uv_name="Zone")                  # Oberflächendetails: Struktur je Fläche als UV.y
        me.materials.clear()
        me.materials.append(figur)
        me.polygons.foreach_set("material_index", [0] * len(me.polygons))
        me.update()


def exportieren(name, objekte):
    import bpy
    pfad = bs.modellpfad(name)
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
                 "Bildhauen": ["Schlaegel", "Meissel"], "Moersern": ["Stoessel"], "Ruehren": ["Kelle"],
                 "Mischen": ["Flakon"], "Zapfen": ["Krug"], "Trinken": ["Krug"], "Laute": ["Laute"], "Kerzenziehen": ["Dochtstab"],
                 "Schoepfen": ["Sieb"], "Schreiben": ["Feder"], "Knacken": ["Dietrich"], "Zaehlen": ["Muenzbeutel"]}.get(name, [])
        wahl = [o for o in objekte if o.type == "MESH" and o.name.split(".")[0] in teile + ["Koerper_Mann"]]
        sy = ((len(reihen) - 1) / 2 - r) * abstand
        for k, ph in enumerate(phasen):
            # Spalten nach rechts im Bild, Reihen nach hinten
            sx = (k - 1.5) * 1.1
            ort = Vector((sx * 0.7071 - sy * 0.7071, sx * 0.7071 + sy * 0.7071, 0))
            neu, kopien = kopie(ske, wahl, ort, dreh=math.radians(-90))     # Seitenansicht: Blick nach links im Bild (−x)
            pose_setzen(neu, *haltung(name, ph, p), seitlich(name, ph, p))
            alle += kopien
            hilfe = arbeitsplatz(name, ort, st, p, ph)
            if hilfe is not None:
                alle.append(hilfe)
        alle.append(beschriftung(name, Vector(((-2.3) * 0.7071 - sy * 0.7071, (-2.3) * 0.7071 + sy * 0.7071, 0.9))))
    bs.vorschau_buehne(None, ziel_hoehe=0.9, ausschnitt=22.0 * len(reihen) / 12, schnell=schnell)
    szene = bpy.context.scene
    f = 0.86                                                            # unter 3 MB: Ausschnitt gleich, weniger Bildpunkte
    szene.render.resolution_x, szene.render.resolution_y = round(900 * f), round(1620 * len(reihen) / 12 * f)
    szene.render.image_settings.color_mode = "RGB"                      # ohne Alphakanal und stark gepackt: kleinere Datei
    szene.render.image_settings.compression = 100
    print(bs.rendern("figuren-bewegungen", lambda o: o in alle))


def arbeitsplatz(name, ort, st, p, phase=0.0):
    """Fürs Bild: was vor der Figur steht (Bank, Amboss, Hebel, Schleifstein, Tisch); die Figur blickt nach −x, ihre linke
    Seite ist −y. `phase`: Stand der Bewegung (der Querhebel der Presse schwenkt mit)."""
    netz = bs.Netz("_Hilfe")
    holz, eisen = st["Holz"], st["Eisen"]
    if name in ("Sitzen", "Kutschieren", "Feinarbeit", "Trinken", "Schreiben", "Beten", "Zaehlen"):   # Bank bzw. Hocker unter der Hüfte
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
    elif name == "Moersern":                                            # Tisch, Mörser darauf
        vor, hoehe = ARBEITSHOEHEN["Moerser"]
        netz.quader(ort + Vector((-vor - 0.12, 0, 0.75)), (0.6, 1.2, 0.06), holz)
        bs.achteck(netz, st, ort + Vector((-vor, 0, 0.78)), 0.12, 0.0, 0.17, "Sandstein")
        bs.rund_oben(netz, st, ort + Vector((-vor, 0, 0)), 0.085, 0.951, "Loch")
    elif name == "Ruehren":                                             # offener Kessel, Brühe 10 cm unter dem Rand
        vor, hoehe = ARBEITSHOEHEN["Kessel"]
        m = ort + Vector((-vor, 0, 0))
        for k in range(12):
            a, b = (m + Vector((0.42 * math.cos(2 * math.pi * j / 12), 0.42 * math.sin(2 * math.pi * j / 12), 0)) for j in (k, k + 1))
            netz.vieleck_nach([a + Vector((0, 0, 0.5)), b + Vector((0, 0, 0.5)), b + Vector((0, 0, 0.95)), a + Vector((0, 0, 0.95))],
                              (a + b) / 2 - m, eisen)
        bs.rund_oben(netz, st, m, 0.41, 0.85, "Kraut", n=12)
    elif name == "Destillieren":                                        # Ofen mit Feuertür vor der Figur, Vorlage rechts
        vor, hoehe = ARBEITSHOEHEN["Ofen"]
        netz.quader(ort + Vector((-vor - 0.37, 0.12, 0.38)), (0.7, 0.7, 0.76), st["Bruchstein dunkel"])
        sx, sv, sh = VORLAGE
        bs.achteck(netz, st, ort + Vector((-sv, -sx, 0.45)), 0.13, 0.0, 0.26, "Elixier")
        bs.achteck(netz, st, ort + Vector((-sv, -sx, 0.45)), 0.03, 0.26, 0.5, "Elixier")
        netz.quader(ort + Vector((-sv, -sx, 0.22)), (0.3, 0.3, 0.44), holz)
    elif name == "Mischen":                                             # Tisch, darauf das Mischglas
        sg, vg, hg = MISCHGLAS
        netz.quader(ort + Vector((-vg - 0.1, 0, 0.75)), (0.6, 1.2, 0.06), holz)
        bs.achteck(netz, st, ort + Vector((-vg, -sg, 0.78)), 0.06, 0.0, hg - 0.78, "Elixier")
        bs.rund_oben(netz, st, ort + Vector((-vg, -sg, 0)), 0.05, hg - 0.02, "Loch")
    elif name == "Pressen":                                             # Spindel und Querhebel (schwenkt mit der Phase)
        a, h = PRESSE["abstand"], PRESSE["hoehe"]
        w = math.radians(presse_winkel(phase))
        achse = ort + Vector((-a, 0, 0))
        netz.stab(achse + Vector((0, 0, 0.6)), achse + Vector((0, 0, 1.9)), 0.12, 0.12, holz)
        ende = Vector((-(a - 0.95 * math.cos(w)), -0.95 * math.sin(w), 0))
        netz.stab(achse + Vector((0, 0, h)) - (ende - Vector((-a, 0, 0))), ort + ende + Vector((0, 0, h)), 0.07, 0.07, holz)
        netz.quader(achse + Vector((0, 0, 0.3)), (0.7, 0.7, 0.6), st["Holz alt"])
    elif name == "Zapfen":                                              # Theke, darauf das Zapffass mit dem Hahn
        netz.quader(ort + Vector((-0.28 - 0.325, 0, THEKE / 2)), (0.65, 1.6, THEKE), holz)
        sx, sv, sh = ZAPFHAHN
        for x0 in (-sx + 0.08,):
            netz.stab(ort + Vector((-sv, x0, sh + 0.16)), ort + Vector((-sv, x0 + 0.7, sh + 0.16)), 0.48, 0.48, st["Holz alt"],
                      quer=(0, 0, 1))
        netz.stab(ort + Vector((-sv, -sx + 0.08, sh + 0.04)), ort + Vector((-sv, -sx, sh + 0.04)), 0.03, 0.03, st["Gold"])
        netz.stab(ort + Vector((-sv, -sx, sh + 0.04)), ort + Vector((-sv, -sx, sh)), 0.025, 0.025, st["Gold"])
    elif name == "Trinken":                                             # Tisch vor der Bank
        sitz = p["knie"] - 0.02
        hoehe = sitz + KRUG_TISCH[2]
        netz.quader(ort + Vector((-0.6, 0, hoehe - 0.03)), (0.8, 1.4, 0.06), holz)
        for dy in (-0.6, 0.6):
            netz.quader(ort + Vector((-0.6, dy, (hoehe - 0.06) / 2)), (0.6, 0.06, hoehe - 0.06), holz)
    elif name == "Kerzenziehen":                                        # Herd mit dem Wachstrog, darin das Wachs
        vor = KERZEN["vor"]
        netz.quader(ort + Vector((-vor, 0, (KERZEN["rand"] - 0.2) / 2)), (0.5, 1.0, KERZEN["rand"] - 0.2), st["Bruchstein dunkel"])
        netz.quader(ort + Vector((-vor, 0, KERZEN["rand"] - 0.1)), (0.34, 0.92, 0.2), eisen)
        netz.quader(ort + Vector((-vor, 0, KERZEN["wachs"])), (0.3, 0.86, 0.01), st["Kerzenwachs"])
    elif name == "Schoepfen":                                           # Bütte mit dem Brei
        b = BUETTE
        netz.quader(ort + Vector((-b["vor"], 0, b["rand"] / 2)), (0.78, 1.0, b["rand"]), st["Holz alt"])
        netz.quader(ort + Vector((-b["vor"], 0, b["brei"])), (0.7, 0.92, b["rand"] - b["brei"]), st["Papierbrei"])
    elif name == "Schreiben":                                           # Pult mit schräger Platte vor dem Hocker, Tintenhorn
        sitz = p["knie"] - 0.02
        s0, v0, h0 = SCHREIBEN["blatt"]
        n = math.radians(SCHREIBEN["neigung"])
        mitte = ort + Vector((-v0 - 0.02, 0, sitz + h0 - 0.012))
        netz.quader((0, 0, 0), (0.5, 0.7, 0.024), holz, Matrix.Translation(mitte) @ Matrix.Rotation(n, 4, "Y"))
        netz.quader(ort + Vector((-v0 - 0.05, 0, (sitz + h0 - 0.12) / 2)), (0.3, 0.6, sitz + h0 - 0.12), holz)
        hs, hv, hh = SCHREIBEN["horn"]
        bs.achteck(netz, st, ort + Vector((-hv, -hs, sitz + hh - 0.1)), 0.025, 0.0, 0.1, "Holz alt")
    elif name == "Predigen":                                            # Brüstung der Kanzel vor der Figur
        vor, h = KANZEL
        netz.quader(ort + Vector((-vor - 0.05, 0, h / 2)), (0.1, 1.2, h), st["Holz alt"])
    elif name == "Knien":                                               # Kniebank unter den Knien und Unterschenkeln
        netz.quader(ort + Vector((0.2, 0, -0.02)), (0.6, 0.6, 0.04), st["Holz alt"])
    elif name == "Knacken":                                             # Übungsbrett mit dem Schloss vor der Figur
        sx, sv, sh = KNACKEN["schloss"]
        netz.quader(ort + Vector((-sv - 0.03, 0, 1.0)), (0.06, 1.0, 1.2), st["Holz alt"])
        netz.quader(ort + Vector((-sv + 0.006, -sx, sh)), (0.012, 0.09, 0.12), eisen)
    elif name == "Zaehlen":                                             # Tisch vor der Bank, darauf der Münzhaufen
        sitz = p["knie"] - 0.02
        hs, hv, hh = ZAEHLEN["haufen"]
        netz.quader(ort + Vector((-0.6, 0, sitz + hh - 0.03)), (0.8, 1.4, 0.06), holz)
        for dy in (-0.6, 0.6):
            netz.quader(ort + Vector((-0.6, dy, (sitz + hh - 0.06) / 2)), (0.6, 0.06, sitz + hh - 0.06), holz)
        bs.achteck(netz, st, ort + Vector((-hv, -hs, sitz + hh)), 0.07, 0.0, 0.03, "Gold")
    elif name == "Schlafen":                                            # Strohsack unter dem Körper
        netz.quader(ort + Vector((-0.05, 0, -0.06)), (2.0, 0.8, 0.12), st["Leinen"])
    return netz.objekt() if netz.dreiecke() else None


def beschriftung(text, ort):
    """Fürs Bild: Text in der Szene, zur Spielkamera gedreht (Schrift des Spiels, Alegreya, OFL)."""
    import bpy
    kurve = bpy.data.curves.new("_Text_" + text, "FONT")
    kurve.body = text
    kurve.size = 0.62
    kurve.align_x, kurve.align_y = "RIGHT", "CENTER"
    try:
        kurve.font = bpy.data.fonts.load(os.path.join(bs.WURZEL, "game", "assets", "oberflaeche", "schriften", "Alegreya-Variabel.ttf"),
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
