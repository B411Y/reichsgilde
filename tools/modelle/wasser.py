"""Wasser: Holz- und Steinbrücke als Bausteine, Fähre mit Anlegesteg, Hafen, Furt- und Ufersteine (Kartenwerkstatt 5c).

    blender -b --python tools/modelle/wasser.py -- [--schnell] [--ohne-bilder]        oder
    python tools/modelle/wasser.py [--schnell] [--ohne-bilder]

Schreibt nach game/assets/modelle/wasser/ fünf Dateien und Vorschaubilder docs/modelle/wasser_<…>.png:
    wasser_holzbruecke.glb   Holz_Joch, Holz_Ende, Holz_Gelaender
    wasser_steinbruecke.glb  Stein_Bogen, Stein_Pfeiler, Stein_Rampe, Stein_Bruestung
    wasser_faehre.glb        Faehre (Kinder Klappe_A, Klappe_B), Anlegesteg
    wasser_hafen.glb         Hafen, Tretrad, Ausleger, Ware, Kahn1, Kahn2 und die Anker
    wasser_steine.glb        Furt1–3, Ufersteine1–3

Höhen: z = 0 ist die Uferkante (das Gelände am Ufer). Wo der Wasserspiegel liegt, bestimmt die Karte; die Vorschau und die
schwimmenden Teile (Kähne) nehmen WASSER = −1,0 m an. Pfähle, Pfeiler und Spundwand reichen bis FUSS = −4,5 m, damit sie
auch in tiefem Wasser im Grund stecken (was unter dem Gelände liegt, sieht man nicht).

Brücken – Blender: X entlang der Brücke, +Y stromauf, Z oben; im Spiel (glTF): X entlang der Brücke, −Z stromauf, Y oben.
Jedes Teil liegt mit seinem Ursprung auf Höhe der Uferkante; die Anschlüsse sind leere Kinderknoten „Anschluss_…“ auf der
Fahrbahn (ihre Höhe ist die Deckhöhe), extras des Teils: „laenge“ (m entlang X) und „deckhoehe“ (m über der Uferkante).
    Holz_Joch        4 m Bohlenbelag auf Streckbalken, in der Mitte (x = 0) ein Joch: Holm auf vier Pfählen, Zangen,
                     Kreuzstreben, Eisbock stromauf. Anschluss_Joch_A bei x = −2, Anschluss_Joch_B bei x = +2, Deck 0,6 m.
                     Breite 4,6 m über die Saumbalken, Fahrbahn 4,3 m.
    Holz_Ende        Auflager an der Uferkante: Böschungsbalken (liegende Stämme) mit Pfählen davor, 1 m Bohlen,
                     dann eine Erdrampe hinab zum Weg, Geländer auf beiden Seiten. Anschluss_Ende_Bruecke bei x = 0 (0,6 m),
                     Anschluss_Ende_Weg bei x = −4 (0 m). Das Land liegt bei −X.
    Holz_Gelaender   Geländer einer Seite für ein Joch: zwei Pfosten (x = ±1), Handlauf und Knieholm, Kopfbänder.
                     Steht auf der Seite −Y (im Spiel +Z, stromab); für die andere Seite um 180° gedreht setzen.
    Reihen: Endstück A mit Anschluss_Ende_Bruecke an die Uferkante, n = max(1, round(W / 4)) Joche (W = Weite zwischen
    den Uferkanten), jedes in X um W / (4 n) gestreckt, Endstück B um 180° gedreht an der anderen Uferkante.
    Stein_Bogen      8 m Spannweite (x = −4 … 4), Stichbogen: Kämpfer −0,7 m, Scheitel 0,8 m, Bogensteine aus Sandstein,
                     Fahrbahn aus Pflaster auf 1,4 m, Gesims. Anschluss_Bogen_A/B bei x = ∓4. Breite 5,4 m.
    Stein_Pfeiler    2 m (x = −1 … 1) mit spitzem Eisbrecher stromauf (+Y Blender, −Z im Spiel), darüber eine Nische in der
                     Brüstung, stromab ein niedriger Vorkopf. Hat seine Brüstung schon. Anschluss_Pfeiler_A/B bei x = ∓1.
    Stein_Rampe      Widerlager (x = −2 … 0) mit Flügelmauern entlang des Ufers und Rampe bis x = −10, beide Brüstungen
                     und Endpfeiler. Anschluss_Rampe_Bruecke bei x = 0 (1,4 m), Anschluss_Rampe_Weg bei x = −10 (0 m).
    Stein_Bruestung  Brüstung einer Seite für einen Bogen (8 m), Seite −Y wie beim Holzgeländer.
    Reihen: Rampe A an der Uferkante, dann Bogen, Pfeiler, Bogen, …, Bogen (n Bögen, n − 1 Pfeiler), Rampe B um 180°
    gedreht: W = 8 n + 2 (n − 1), also n = round((W + 2) / 10), die Bögen samt Brüstung in X um (W − 2 (n − 1)) / (8 n)
    gestreckt. Pfeiler nie drehen (der Eisbrecher zeigt stromauf).

Fähre – X ist die Fahrtrichtung über den Fluss (wie bei den Brücken), +Y stromauf.
    Faehre           Prahm 9 m × 4 m, Ursprung auf der Wasserlinie in der Mitte, Deck 0,3 m darüber; Seilwinde (Haspel)
                     stromauf, zwei Seilführungen. Kinder Klappe_A (Ursprung am Scharnier x = −4,5) und Klappe_B (x = +4,5),
                     je 1,6 m lang: Drehen um die Querachse hebt die Spitze (im Spiel Klappe_A RotateZ(−w), Klappe_B
                     RotateZ(+w)). Anker_Seil_A/B an den Seilführungen (dort läuft das Fährseil weiter zu den Stegen),
                     Anker_Faehrmann an der Winde, Anker_Karren in der Mitte (Blick +X).
    Anlegesteg       Ursprung an der Uferkante; das Land liegt bei −X, der Steg führt 6 m ins Wasser und fällt dabei von
                     0,05 m (x = −1) auf −0,45 m (x = 6). Seilpfosten auf beiden Seiten (Anker_Pfosten_A bei +Y Blender =
                     −Z im Spiel, Anker_Pfosten_B gegenüber), Fährglocke bei −Y. Anschluss_Steg_Weg bei x = −1,
                     Anschluss_Steg_Wasser bei x = 6: Dort legt die Fähre an (die Klappe liegt 0,4 m auf dem Steg, der
                     Ursprung der Fähre also 5,7 m weiter, auf dem Wasserspiegel). Zweimal gesetzt, am anderen Ufer um 180°
                     gedreht; das Fährseil läuft stromauf von Anker_Pfosten_A (erster Steg) über Anker_Seil_A/B der Fähre
                     zu Anker_Pfosten_B (gedrehter Steg).

Hafen – Blender: Land bei −Y (Weg von vorn), Wasser bei +Y; im Spiel Wasser bei −Z. Ursprung in der Mitte der Landfläche
16 × 14 m; die Uferkante (Vorderkante der Spundwand) liegt bei y = 7 (im Spiel z = −7), Anker_Ufer in ihrer Mitte, Blick
aufs Wasser. Kai (Bohlen auf 0,25 m) von y = 2 bis 7, Spundwand mit Pfählen, Poller, Leiter, Lagerschuppen, Tretradkran.
    Hafen     alles Feste (Tür und Läden des Schuppens in Holz: der Hafen gehört der Stadt);  Tretrad  Ursprung in der
    Radachse (dreht um X);  Ausleger  Ursprung in der Kranachse (dreht um die Hochachse);  Ware  Fässer, Kisten, Säcke auf
    dem Kai;  Kahn1 (10 m, mit Hütte), Kahn2 (7 m, um 180° gedreht, Bug an Bug)  Ursprung auf ihrer Wasserlinie, liegen auf
    WASSER vor dem Kai – das Spiel kann sie auf den Wasserspiegel der Karte heben oder senken (die Festmacher gehen mit).
    Anker_Ufer, Anker_Tuer (Schuppen), Anker_Karren (Hof), Anker_Anleger_1/2 (Kai vor den Kähnen), Anker_Tretrad (im Rad).

Steine (Streu-Teile, je 4 m lang entlang X):
    Furt1–3        Trittsteine, Schotterbank, gesetzte Steinfurt. Ursprung auf dem Wasserspiegel; die Steine ragen
                   bis 0,12 m heraus und reichen bis −0,6 m (Bäche bis 0,5 m Tiefe).
    Ufersteine1–3  Steinschüttung, gesetztes Steinpflaster, Pfahlreihe mit Steinen. Ursprung an der Uferkante, das Wasser
                   liegt bei +Y (im Spiel −Z); gebaut für eine Böschung von 45° bis 1,6 m unter die Kante.
Keine Hausfarbe, keine Zustände; Prüfung mit `pruefen.py <glb> --wasser`.
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

bs.FARBEN.update({
    "Pflaster": (0.55, 0.52, 0.47),
    "Teerholz": (0.19, 0.15, 0.12),        # geteerte Planken der Kähne und der Fähre
    "Kies": (0.46, 0.44, 0.39),
    "Sack": (0.70, 0.62, 0.46),
    "Bronze": (0.52, 0.40, 0.22),          # Fährglocke
})

# Getrennt bleiben (nicht in „Farben“, Modell-Texturen Gruppe 3): die Materialien, auf die die Material-Bibliothek ihre
# Texturen legt (game/daten/materialbibliothek.json, „namen“) – wie in hafenbauten.py.
with open(os.path.join(bs.WURZEL, "game", "daten", "materialbibliothek.json"), encoding="utf-8") as _f:
    BEHALTEN = set(bs.BENANNTE_STOFFE) | set(json.load(_f)["namen"])

WASSER = -1.0           # Wasserspiegel der Vorschau und der Kähne (m unter der Uferkante)
FUSS = -4.5             # Unterkante von Pfählen, Pfeilern und Spundwand

# Holzbrücke
HD = 0.6                # Deckhöhe (Oberkante der Bohlen)
JOCH = 4.0
H_ENDE = 4.0

# Steinbrücke
SB = 2.7                # halbe Breite über alles (Fahrbahn zwischen den Brüstungen 4,6 m)
SD = 1.4                # Deckhöhe (Oberkante Pflaster)
BOGEN = 8.0
PFEILER = 2.0
RAMPE = 10.0
WIDERLAGER = 2.0
KAEMPFER = -0.7
STICH = 1.5
RING = 0.45             # Dicke des Bogenrings
BR = 0.4                # Dicke der Brüstung
BR_HOCH = 0.85          # Höhe der Brüstung ohne Abdeckplatte

# Fähre
F_HALB = 4.5            # halbe Länge des Prahms
F_DECK = 0.3            # Deck über der Wasserlinie
KLAPPE = 1.6

# Hafen
UFER = 7.0              # Uferkante (Vorderkante der Spundwand)
KAI = 0.25              # Oberkante der Kaibohlen


# ─────────────────────────────────────────────────────────────────────────────
#  Grundformen
# ─────────────────────────────────────────────────────────────────────────────

def linie(netz, st, a, b, normale, stoff, breite=0.028, abstand=0.003):
    """Naht oder Kante als schmaler Streifen von a nach b, 3 mm vor der Fläche mit der Normale `normale`."""
    a, b, n = Vector(a), Vector(b), Vector(normale).normalized()
    q = (b - a).cross(n).normalized() * breite / 2
    a, b = a + n * abstand, b + n * abstand
    netz.vieleck_nach([a - q, b - q, b + q, a + q], n, st[stoff])


def klotz(netz, m, ecken, ohne=()):
    """Körper aus 8 Ecken: unten a, b, c, d (gegen den Uhrzeigersinn von oben), oben e, f, g, h darüber.
    Seiten „s0“ (a–b), „s1“ (b–c), „s2“ (c–d), „s3“ (d–a), „+z“, „-z“ lassen sich weglassen."""
    a, b, c, d, e, f, g, h = [Vector(p) for p in ecken]
    mitte = (a + b + c + d + e + f + g + h) / 8
    for k, flaeche in (("-z", (a, d, c, b)), ("+z", (e, f, g, h)), ("s0", (a, b, f, e)), ("s1", (b, c, g, f)),
                       ("s2", (c, d, h, g)), ("s3", (d, a, e, h))):
        if k not in ohne:
            netz.vieleck_nach(flaeche, sum(flaeche, Vector()) / 4 - mitte, m)


def kasten(netz, st, x0, x1, y0, y1, z0, z1, stoff, ohne=()):
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), st[stoff], ohne=ohne)


def pfahl(netz, st, x, y, z0, z1, r=0.14, stoff="Holz", n=6, kopf=None):
    lb.saeule(netz, st, (x, y, 0), r, z0, z1, stoff, enden=kopf, n=n)


def walze(netz, st, y, z, r, x0, x1, stoff, n=8):
    """Liegender Zylinder entlang X (Trommel, Achse, Rolle)."""
    netz.prisma([(y + r * math.cos(2 * math.pi * k / n), z + r * math.sin(2 * math.pi * k / n)) for k in range(n)],
                x0, x1, st[stoff])


def bohlen(netz, st, x0, x1, y0, y1, hoehe, saat, dicke=0.08, breite=0.333,
           stoffe=("Holz alt", "Holz", "Holz alt", "Bretter", "Holz", "Holz alt")):
    """Quer liegende Bohlen (entlang Y) von x0 bis x1, Oberkante hoehe(x); schmale Fugen dazwischen, jede Bohle eine Spur
    höher oder tiefer. Die Enden (y0, y1) stecken unter Saumbalken und haben keine Stirnseite."""
    zf = nt.zufall(saat)
    n = max(1, round((x1 - x0) / breite))
    b = (x1 - x0) / n
    for k in range(n):
        xa, xb = x0 + k * b + 0.012, x0 + (k + 1) * b - 0.012
        dz = zf.uniform(-0.014, 0.004)
        za, zb = hoehe(xa) + dz, hoehe(xb) + dz
        klotz(netz, st[stoffe[(k * 5 + saat) % len(stoffe)]],
              [(xa, y0, za - dicke), (xb, y0, zb - dicke), (xb, y1, zb - dicke), (xa, y1, za - dicke),
               (xa, y0, za), (xb, y0, zb), (xb, y1, zb), (xa, y1, za)], ohne=("-z", "s0", "s2"))


def latte_schraeg(netz, st, x0, x1, y0, y1, z0, z1, hoehe, stoff, ohne=("-z",)):
    """Balken entlang X von x0 bis x1 (Querschnitt y0–y1 × hoehe), dessen Oberkante von z0 auf z1 steigt oder fällt."""
    klotz(netz, st[stoff], [(x0, y0, z0 - hoehe), (x1, y0, z1 - hoehe), (x1, y1, z1 - hoehe), (x0, y1, z0 - hoehe),
                            (x0, y0, z0), (x1, y0, z1), (x1, y1, z1), (x0, y1, z0)], ohne=ohne)


def anschluss(eltern, name, punkt, drehung):
    """Leerer Kinderknoten des Teils `eltern` (Anschluss oder Anker); drehung 90 = Blick −X, 270 = Blick +X, 0 = Blick +Y."""
    a = bs.anker(name, punkt, drehung)
    a.parent = eltern
    a.matrix_parent_inverse = Matrix.Identity(4)
    a.location = Vector(punkt)
    return a


def teil(netz, laenge=None, deckhoehe=None):
    """Netz → Objekt; Maße als extras für die Darstellung."""
    o = netz.objekt()
    if laenge is not None:
        o["laenge"] = laenge
    if deckhoehe is not None:
        o["deckhoehe"] = deckhoehe
    return o


# ─────────────────────────────────────────────────────────────────────────────
#  Holzbrücke
# ─────────────────────────────────────────────────────────────────────────────

def saumbalken(netz, st, x0, x1, ohne=("+x", "-x", "-z")):
    """Saumbalken an beiden Seiten: halten die Bohlenenden und stehen 8 cm als Schrammbord über die Fahrbahn."""
    for s in (-1, 1):
        kasten(netz, st, x0, x1, s * 2.25 - 0.1, s * 2.25 + 0.1, HD - 0.3, HD + 0.08, "Holz", ohne=ohne)


def holz_joch(st):
    netz = bs.Netz("Holz_Joch")
    h = JOCH / 2
    bohlen(netz, st, -h, h, -2.15, 2.15, lambda x: HD, saat=1)
    for y in (-1.6, 0.0, 1.6):                                   # Streckbalken unter den Bohlen
        kasten(netz, st, -h, h, y - 0.11, y + 0.11, HD - 0.34, HD - 0.08, "Holz", ohne=("+x", "-x", "-z", "+z"))
    saumbalken(netz, st, -h, h)
    # Joch: Holm auf vier Pfählen, zwei Zangen, Kreuzstreben auf beiden Seiten
    kasten(netz, st, -0.16, 0.16, -2.6, 2.6, HD - 0.64, HD - 0.34, "Holz", ohne=("-z",))
    for y in (-2.0, -0.7, 0.7, 2.0):
        pfahl(netz, st, 0, y, FUSS, HD - 0.64)
    for s in (-1, 1):
        kasten(netz, st, s * 0.2 - 0.03, s * 0.2 + 0.03, -2.3, 2.3, -0.62, -0.38, "Holz alt", ohne=("-z",))
        netz.stab((s * 0.19, -s * 2.0, -0.05), (s * 0.19, s * 2.0, -1.5), 0.16, 0.07, st["Holz alt"], quer=(1, 0, 0))
    # Eisbock stromauf: schräger Balken mit Eisenkante vor dem äußeren Pfahl, gestützt von einem eigenen Pfahl
    netz.stab((0, 2.15, HD - 0.5), (0, 4.0, -2.6), 0.22, 0.22, st["Holz"], quer=(1, 0, 0))
    netz.stab((0, 2.2, HD - 0.4), (0, 4.0, -2.45), 0.05, 0.08, st["Eisen"], quer=(1, 0, 0))
    pfahl(netz, st, 0, 3.4, FUSS, -1.55, r=0.13)
    o = teil(netz, JOCH, HD)
    return [o, anschluss(o, "Anschluss_Joch_A", (-h, 0, HD), 90), anschluss(o, "Anschluss_Joch_B", (h, 0, HD), 270)]


def gelaender_stueck(netz, st, y, x0, x1, z0, z1, pfosten):
    """Handlauf und Knieholm von x0 (Oberkante Fahrbahn z0) bis x1 (z1), Pfosten an den x in `pfosten`."""
    oben, knie = 1.15, 0.65
    latte_schraeg(netz, st, x0, x1, y - 0.07, y + 0.07, z0 + oben, z1 + oben, 0.12, "Holz alt", ohne=("-z", "s1", "s3"))
    latte_schraeg(netz, st, x0, x1, y - 0.04, y + 0.04, z0 + knie, z1 + knie, 0.1, "Holz alt", ohne=("-z", "s1", "s3"))
    for x in pfosten:
        t = (x - x0) / (x1 - x0)
        z = z0 + (z1 - z0) * t
        netz.quader((x, y, z + (oben - 0.12) / 2 - 0.15), (0.14, 0.15, oben - 0.12 + 0.3), st["Holz"], ohne=("-z",))
        for s in (-1, 1):                                        # Kopfbänder unter dem Handlauf (nicht über das Stück hinaus)
            if not x0 - 1e-6 <= x + s * 0.5 <= x1 + 1e-6:
                continue
            dz = (z1 - z0) / (x1 - x0)
            netz.stab((x + s * 0.07, y, z + 0.78 + s * 0.07 * dz), (x + s * 0.5, y, z + oben - 0.12 + s * 0.5 * dz),
                      0.08, 0.06, st["Holz"])


def holz_gelaender(st):
    netz = bs.Netz("Holz_Gelaender")
    gelaender_stueck(netz, st, -2.25, -JOCH / 2, JOCH / 2, HD, HD, (-1.0, 1.0))
    return [teil(netz, JOCH, HD)]


def holz_ende(st):
    netz = bs.Netz("Holz_Ende")
    rampe = lambda x: HD * (x + H_ENDE) / (H_ENDE - 1.0)       # noqa: E731  Oberkante der Erdrampe (x −4 … −1)
    bohlen(netz, st, -1.0, 0.0, -2.15, 2.15, lambda x: HD, saat=7)
    for y in (-1.6, 0.0, 1.6):
        kasten(netz, st, -1.0, 0.0, y - 0.11, y + 0.11, HD - 0.34, HD - 0.08, "Holz", ohne=("+x", "-z", "+z"))
    saumbalken(netz, st, -1.2, 0.0, ohne=("+x", "-z"))
    # Auflager: liegende Stämme vor der Uferböschung, die oberste trägt die Streckbalken; Pfähle davor halten sie
    z = HD - 0.34 - 0.14
    while z > -1.9:
        lb.stamm(netz, st, (-0.14, -3.3, z), (-0.14, 3.3, z), 0.14, n=6)
        z -= 0.27
    for y in (-2.6, -1.0, 1.0, 2.6):
        pfahl(netz, st, 0.12, y, FUSS, HD - 0.3, r=0.12, kopf="Scheitholz")
    # Erdrampe zum Weg mit Fahrspuren, die Seiten als Grasböschung
    netz.vieleck_nach([(-H_ENDE, -2.3, 0.0), (-1.0, -2.3, HD), (-1.0, 2.3, HD), (-H_ENDE, 2.3, 0.0)], (0, 0, 1), st["Erde hell"])
    for y in (-0.85, 0.85):
        netz.vieleck_nach([(-H_ENDE + 0.2, y - 0.16, rampe(-H_ENDE + 0.2) + 0.004), (-1.0, y - 0.16, HD + 0.004),
                           (-1.0, y + 0.16, HD + 0.004), (-H_ENDE + 0.2, y + 0.16, rampe(-H_ENDE + 0.2) + 0.004)],
                          (0, 0, 1), st["Erde"])
    for s in (-1, 1):
        a, b, c = Vector((-H_ENDE, s * 2.3, 0)), Vector((-1.0, s * 2.3, HD)), Vector((0.0, s * 2.3, HD - 0.08))
        d, e = Vector((0.0, s * 3.2, 0)), Vector((-1.0, s * (2.3 + HD * 1.4), 0))
        netz.vieleck_nach([a, b, e], (0, s, 1), st["Gras"])
        netz.vieleck_nach([b, c, d, e], (0, s, 1), st["Gras"])
        bs.buschel(netz, st, Vector((-2.6, s * 2.75, 0.12)), groesse=0.45)
        lb.felsbrocken(netz, st, (-H_ENDE + 0.3, s * 2.55, 0), 0.28, 0.42, saat=5 + s)     # Prellsteine
        # Geländer: waagrecht über dem Auflager, dann die Rampe hinab
        gelaender_stueck(netz, st, s * 2.25, -1.0, 0.0, HD, HD, (-1.0,))
        gelaender_stueck(netz, st, s * 2.25, -2.9, -1.0, rampe(-2.9), HD, (-2.9,))
    o = teil(netz, H_ENDE, HD)
    return [o, anschluss(o, "Anschluss_Ende_Bruecke", (0, 0, HD), 270), anschluss(o, "Anschluss_Ende_Weg", (-H_ENDE, 0, 0), 90)]


# ─────────────────────────────────────────────────────────────────────────────
#  Steinbrücke
# ─────────────────────────────────────────────────────────────────────────────

def bogenmasse():
    """Stichbogen über BOGEN: Halbmesser, Höhe des Kreismittelpunkts, halber Öffnungswinkel."""
    s = BOGEN / 2
    r = (s * s + STICH * STICH) / (2 * STICH)
    return r, KAEMPFER + STICH - r, math.asin(s / r)


def pflaster(netz, st, ecken):
    """Pflasterfläche (Ecken nach oben); die Steine zeichnen die Oberflächendetails."""
    netz.vieleck_nach(ecken, (0, 0, 1), st["Pflaster"])


def bruestung(netz, st, x0, x1, y, z0=SD, z1=SD, ohne=("s1", "s3", "-z")):
    """Brüstung von x0 bis x1 auf der Seite y (±), Fuß von z0 auf z1 steigend; Abdeckplatte aus Sandstein."""
    s = 1 if y > 0 else -1
    ya, yb = sorted((s * (SB + 0.015), s * (SB - BR)))
    latte_schraeg(netz, st, x0, x1, ya, yb, z0 + BR_HOCH, z1 + BR_HOCH, BR_HOCH, "Haustein", ohne=ohne)
    latte_schraeg(netz, st, x0, x1, ya - 0.04, yb + 0.04, z0 + BR_HOCH + 0.12, z1 + BR_HOCH + 0.12, 0.12, "Sandstein",
                  ohne=ohne)


def gesims_stein(netz, st, x0, x1, y, ohne=("+x", "-x", "-z")):
    """Gesims unter der Brüstung an der Außenseite y (±)."""
    s = 1 if y > 0 else -1
    kasten(netz, st, x0, x1, s * SB - 0.02 if s > 0 else -SB - 0.08, s * SB + 0.08 if s > 0 else -SB + 0.02,
           SD - 0.24, SD - 0.04, "Sandstein", ohne=ohne + (("-y",) if s > 0 else ("+y",)))


def stein_bogen(st):
    netz = bs.Netz("Stein_Bogen")
    r, zc, w0 = bogenmasse()
    h = BOGEN / 2
    n = 12
    punkte = [(r * math.sin(-w0 + 2 * w0 * i / n), zc + r * math.cos(-w0 + 2 * w0 * i / n)) for i in range(n + 1)]

    for y in (-SB, SB):
        s = 1 if y > 0 else -1
        for (xa, za), (xb, zb) in zip(punkte, punkte[1:]):          # Stirnmauer über dem Bogen
            netz.vieleck_nach([(xa, y, za), (xb, y, zb), (xb, y, SD), (xa, y, SD)], (0, s, 0), st["Haustein"])
        steine = 13                                                  # Bogensteine, der Schlussstein etwas größer
        for j in range(steine):
            w_a = -w0 + 2 * w0 * j / steine + 0.006
            w_b = -w0 + 2 * w0 * (j + 1) / steine - 0.006
            d = RING + (0.1 if j == steine // 2 else 0.0)
            ring = [(r * math.sin(w_a), zc + r * math.cos(w_a)), (r * math.sin(w_b), zc + r * math.cos(w_b)),
                    ((r + d) * math.sin(w_b), zc + (r + d) * math.cos(w_b)), ((r + d) * math.sin(w_a), zc + (r + d) * math.cos(w_a))]
            netz.vieleck_nach([(x, y + s * (0.02 + 0.006 * (j % 2)), z) for x, z in ring], (0, s, 0), st["Sandstein"])
        gesims_stein(netz, st, -h, h, y)
    for (xa, za), (xb, zb) in zip(punkte, punkte[1:]):              # Laibung (unter dem Scheitel sieht die Kamera sie nicht)
        netz.vieleck_nach([(xa, -SB, za), (xb, -SB, zb), (xb, SB, zb), (xa, SB, za)],
                          (-(xa + xb) / 2, 0, zc - (za + zb) / 2), st["Bruchstein"])
    pflaster(netz, st, [(-h, -SB, SD), (h, -SB, SD), (h, SB, SD), (-h, SB, SD)])
    o = teil(netz, BOGEN, SD)
    return [o, anschluss(o, "Anschluss_Bogen_A", (-h, 0, SD), 90), anschluss(o, "Anschluss_Bogen_B", (h, 0, SD), 270)]


def stein_bruestung(st):
    netz = bs.Netz("Stein_Bruestung")
    bruestung(netz, st, -BOGEN / 2, BOGEN / 2, -SB)
    return [teil(netz, BOGEN, SD)]


def stein_pfeiler(st):
    netz = bs.Netz("Stein_Pfeiler")
    p = PFEILER / 2
    spitze = SB + 1.8                       # Eisbrecher stromauf (+Y)
    hinten = -SB - 1.0                      # Vorkopf stromab (−Y)
    nische = {}
    # Pfeilerkörper: bis zum Kämpfer mit Stirnseiten (unter den Bögen sichtbar), darüber nur die Seite stromab
    kasten(netz, st, -p, p, -SB, SB, FUSS, KAEMPFER, "Haustein", ohne=("-z", "+z", "+y", "-y"))
    netz.vieleck_nach([(-p, -SB, -0.2), (p, -SB, -0.2), (p, -SB, SD), (-p, -SB, SD)], (0, -1, 0), st["Haustein"])
    kasten(netz, st, -p - 0.06, p + 0.06, -SB, SB, KAEMPFER - 0.18, KAEMPFER, "Sandstein", ohne=("-z", "+y", "-y"))
    # Eisbrecher bis zur Fahrbahn, oben eine Nische mit Brüstung
    for x in (-p, p):
        s = 1 if x > 0 else -1
        a, b = Vector((x, SB, 0)), Vector((0, spitze, 0))
        normale = Vector((s * (spitze - SB), p, 0)).normalized()
        netz.vieleck_nach([(a.x, a.y, FUSS), (b.x, b.y, FUSS), (b.x, b.y, SD), (a.x, a.y, SD)], normale, st["Haustein"])
        # Gesims entlang der Kante und die Brüstung der Nische (Fünfeck: innen parallel zur Kante)
        innen = -normale
        a_i = a + innen * BR
        richtung = (b - a).normalized()
        start = a_i + richtung * ((SB - BR - a_i.y) / richtung.y)
        ende = a_i + richtung * ((0 - a_i.x) / richtung.x)
        nische[s] = (start, ende)
        ecken = [Vector(p) for p in ((x, SB - BR, 0), (x, SB, 0), (0.0, spitze, 0), (0.0, ende.y, 0), (start.x, start.y, 0))]
        mitte = sum(ecken, Vector()) / len(ecken)
        for z0, z1, stoff in ((SD, SD + BR_HOCH, "Haustein"), (SD + BR_HOCH, SD + BR_HOCH + 0.12, "Sandstein")):
            for i in range(len(ecken)):
                e0, e1 = ecken[i], ecken[(i + 1) % len(ecken)]
                if abs(e0.x) < 1e-6 and abs(e1.x) < 1e-6:     # Mittelfuge: dort stößt die andere Hälfte an
                    continue
                seite = [(e0.x, e0.y, z0), (e1.x, e1.y, z0), (e1.x, e1.y, z1), (e0.x, e0.y, z1)]
                netz.vieleck_nach(seite, Vector(((e0.x + e1.x) / 2, (e0.y + e1.y) / 2, 0)) - mitte, st[stoff])
            netz.vieleck_nach([(e.x, e.y, z1) for e in ecken], (0, 0, 1), st[stoff])
        g0, g1 = a + normale * 0.08, b + normale * 0.08
        klotz(netz, st["Sandstein"], [(a.x, a.y, SD - 0.24), (b.x, b.y, SD - 0.24), (g1.x, g1.y, SD - 0.24), (g0.x, g0.y, SD - 0.24),
                                      (a.x, a.y, SD - 0.04), (b.x, b.y, SD - 0.04), (g1.x, g1.y, SD - 0.04), (g0.x, g0.y, SD - 0.04)],
              ohne=("-z", "s0"))
    # Vorkopf stromab: niedrig, mit schräger Haube
    for x in (-p, p):
        s = 1 if x > 0 else -1
        normale = (s, -1, 0)
        netz.vieleck_nach([(x, -SB, FUSS), (0, hinten, FUSS), (0, hinten, -0.2), (x, -SB, -0.2)], normale, st["Haustein"])
        netz.vieleck_nach([(x, -SB, -0.2), (0, hinten, -0.2), (x, -SB, 0.6)], normale, st["Haustein"])
        netz.vieleck_nach([(x, -SB, 0.6), (0, hinten, -0.2), (0, -SB, 0.6)], (s * 0.2, -0.6, 1), st["Sandstein"])
    gesims_stein(netz, st, -p, p, -SB)
    bruestung(netz, st, -p, p, -SB)
    # Fahrbahn: bis zur Brüstung stromauf, dann die Nische
    pflaster(netz, st, [(-p, -SB, SD), (p, -SB, SD), (p, SB - BR, SD), (-p, SB - BR, SD)])
    (links, spitze_n), (rechts, _) = nische[-1], nische[1]
    netz.vieleck_nach([(links.x, links.y, SD), (rechts.x, rechts.y, SD), (0, spitze_n.y, SD)], (0, 0, 1), st["Pflaster"])
    o = teil(netz, PFEILER, SD)
    return [o, anschluss(o, "Anschluss_Pfeiler_A", (-p, 0, SD), 90), anschluss(o, "Anschluss_Pfeiler_B", (p, 0, SD), 270)]


def stein_rampe(st):
    netz = bs.Netz("Stein_Rampe")
    w = -WIDERLAGER
    rampe = lambda x: SD * (x + RAMPE) / (RAMPE - WIDERLAGER)   # noqa: E731  Oberkante der Rampe (x −10 … −2)
    # Widerlager: Stirn zum Fluss bis zum Kämpfer, darüber trägt es den Bogen
    netz.vieleck_nach([(0, -SB, FUSS), (0, SB, FUSS), (0, SB, KAEMPFER), (0, -SB, KAEMPFER)], (1, 0, 0), st["Haustein"])
    kasten(netz, st, -0.02, 0.06, -SB, SB, KAEMPFER - 0.18, KAEMPFER, "Sandstein", ohne=("-z", "+y", "-y", "-x"))
    for y in (-SB, SB):
        s = 1 if y > 0 else -1
        # Seitenwand: Widerlager und Rampe in einem Umriss
        netz.vieleck_nach([(-RAMPE, y, -0.4), (w, y, -0.4), (w, y, SD), (-RAMPE, y, 0.0)], (0, s, 0), st["Haustein"])
        netz.vieleck_nach([(w, y, FUSS + 1.5), (0, y, FUSS + 1.5), (0, y, SD), (w, y, SD)], (0, s, 0), st["Haustein"])
        gesims_stein(netz, st, w, 0, y, ohne=("+x", "-z"))
        # Flügelmauer entlang des Ufers (Stirn zum Fluss), oben schräg bis auf 0,3 m
        y1 = s * (SB + 2.8)
        netz.vieleck_nach([(0, y, FUSS + 1.5), (0, y1, FUSS + 1.5), (0, y1, 0.3), (0, y, SD - 0.24)], (1, 0, 0), st["Haustein"])
        klotz(netz, st["Sandstein"], [(-0.6, y, SD - 0.24), (-0.6, y1, 0.3), (0.06, y1, 0.3), (0.06, y, SD - 0.24),
                                      (-0.6, y, SD - 0.12), (-0.6, y1, 0.42), (0.06, y1, 0.42), (0.06, y, SD - 0.12)],
              ohne=("-z", "s3"))
        netz.vieleck_nach([(-0.6, y1, -0.3), (0.06, y1, -0.3), (0.06, y1, 0.3), (-0.6, y1, 0.3)], (0, s, 0), st["Haustein"])
        # Brüstung über dem Widerlager, schräg die Rampe hinab, am Fuß ein Endpfeiler mit Kappe
        bruestung(netz, st, w, 0, y, ohne=("s1", "-z"))
        bruestung(netz, st, -RAMPE + 0.9, w, y, z0=rampe(-RAMPE + 0.9), z1=SD, ohne=("s3", "-z"))
        ym = s * (SB - BR / 2)
        kasten(netz, st, -RAMPE + 0.3, -RAMPE + 0.9, ym - 0.32, ym + 0.32, -0.2, 1.2, "Haustein", ohne=("-z", "+z"))
        kasten(netz, st, -RAMPE + 0.25, -RAMPE + 0.95, ym - 0.37, ym + 0.37, 1.2, 1.32, "Sandstein", ohne=("-z",))
        lb.kegel(netz, (-RAMPE + 0.6, ym), 0.45, 1.32, 1.62, st["Sandstein"], n=4, dreh=math.pi / 4)
    pflaster(netz, st, [(-RAMPE, -SB, 0.0), (w, -SB, SD), (w, SB, SD), (-RAMPE, SB, 0.0)])
    pflaster(netz, st, [(w, -SB, SD), (0, -SB, SD), (0, SB, SD), (w, SB, SD)])
    o = teil(netz, RAMPE, SD)
    return [o, anschluss(o, "Anschluss_Rampe_Bruecke", (0, 0, SD), 270), anschluss(o, "Anschluss_Rampe_Weg", (-RAMPE, 0, 0), 90)]


# ─────────────────────────────────────────────────────────────────────────────
#  Fähre und Anlegesteg
# ─────────────────────────────────────────────────────────────────────────────

def faehre(st):
    netz = bs.Netz("Faehre")
    b = 2.0
    unten = -0.45
    # Rumpf: Kasten mit schrägen Enden (Prahm), geteert; das Deck aus Bohlen
    profil = [(3.8, unten), (-3.8, unten), (-F_HALB, F_DECK), (F_HALB, F_DECK)]      # (−x, z), siehe bs.platte
    netz.prisma(profil, -b, b, st["Teerholz"], matrix=Matrix.Rotation(math.radians(90), 4, "Z"),
                kante=lambda p, q: st["Bretter"] if abs(p.y - F_DECK) < 1e-6 and abs(q.y - F_DECK) < 1e-6 else None)
    for s in (-1, 1):
        kasten(netz, st, -4.0, 4.0, s * b - 0.12 if s > 0 else -b, b if s > 0 else -b + 0.12, F_DECK, F_DECK + 0.38,
               "Holz alt", ohne=("-z",))
        kasten(netz, st, -4.05, 4.05, s * b - 0.14 if s > 0 else -b - 0.02, b + 0.02 if s > 0 else -b + 0.14,
               F_DECK + 0.38, F_DECK + 0.45, "Holz", ohne=("-z",))
        for x in (-3.0, -1.0, 1.0, 3.0):                          # Rungen außen am Setzbord
            kasten(netz, st, x - 0.06, x + 0.06, s * (b + 0.06) - 0.05, s * (b + 0.06) + 0.05, -0.05, F_DECK + 0.45, "Holz")
    # Seilwinde (Haspel) stromauf: zwei Böcke, Trommel mit aufgewickeltem Seil, Handspeichen
    yw, zw = 1.45, F_DECK + 0.85
    for x in (-0.7, 0.7):
        for dy, dicke in ((-0.35, 0.12), (0.35, 0.1)):
            netz.stab((x, yw + dy, F_DECK), (x, yw, zw + 0.1), 0.12, dicke, st["Holz"], quer=(1, 0, 0))
    walze(netz, st, yw, zw, 0.2, -0.72, 0.72, "Holz")
    walze(netz, st, yw, zw, 0.06, -0.98, 0.98, "Holz", n=6)
    walze(netz, st, yw, zw, 0.235, -0.35, 0.35, "Seil")
    for x in (-0.9, 0.9):
        netz.stab((x, yw - 0.6, zw), (x, yw + 0.6, zw), 0.07, 0.07, st["Holz"], quer=(1, 0, 0))
        netz.stab((x, yw, zw - 0.6), (x, yw, zw + 0.6), 0.07, 0.06, st["Holz"], quer=(1, 0, 0))
    # Seilführungen an den Enden, das Fährseil läuft über die Trommel
    for x in (-4.1, 4.1):
        netz.stab((x, 1.85, F_DECK - 0.4), (x, 1.85, F_DECK + 1.05), 0.14, 0.14, st["Holz"])
        netz.prisma([(1.85 + 0.09 * math.cos(2 * math.pi * k / 6), F_DECK + 1.1 + 0.09 * math.sin(2 * math.pi * k / 6)) for k in range(6)],
                    x - 0.1, x + 0.1, st["Eisen"])
        netz.stab((x, 1.85, F_DECK + 1.2), (math.copysign(0.3, x), yw + 0.1, zw + 0.23), 0.045, 0.045, st["Seil"])
    for k in range(2):                                            # Staken am Setzbord
        netz.stab((-3.6 + 0.3 * k, -1.62 + 0.25 * k, F_DECK + 0.04), (3.2, -1.68 + 0.25 * k, F_DECK + 0.04), 0.06, 0.06, st["Holz alt"])
    o = teil(netz, 2 * F_HALB, F_DECK)
    klappen = []
    for name, s in (("Klappe_A", -1), ("Klappe_B", 1)):
        k = bs.Netz(name)
        klotz(k, st["Bretter"], [(0, -1.7, -0.1), (s * KLAPPE, -1.7, -0.06), (s * KLAPPE, 1.7, -0.06), (0, 1.7, -0.1),
                                 (0, -1.7, 0.0), (s * KLAPPE, -1.7, 0.0), (s * KLAPPE, 1.7, 0.0), (0, 1.7, 0.0)], ohne=("-z",))
        for i in range(4):                                        # Querleisten gegen das Rutschen
            x = s * (0.25 + 0.38 * i)
            kasten(k, st, x - 0.04, x + 0.04, -1.6, 1.6, 0.0, 0.04, "Holz", ohne=("-z",))
        for y in (-1.2, 1.2):                                     # Scharniere
            kasten(k, st, -0.12, 0.12, y - 0.06, y + 0.06, -0.02, 0.012, "Eisen", ohne=("-z",))
        ko = k.objekt()
        ko.parent = o
        ko.matrix_parent_inverse = Matrix.Identity(4)
        ko.location = Vector((s * F_HALB, 0, F_DECK))
        klappen.append(ko)
    anker = [anschluss(o, "Anker_Seil_A", (-4.1, 1.85, F_DECK + 1.2), 90), anschluss(o, "Anker_Seil_B", (4.1, 1.85, F_DECK + 1.2), 270),
             anschluss(o, "Anker_Faehrmann", (0.0, 0.85, F_DECK), 0), anschluss(o, "Anker_Karren", (0.0, -0.5, F_DECK), 270)]
    return [o] + klappen + anker


def steg_hoehe(x):
    """Oberkante des Anlegestegs: am Land (x = −1) 0,05 m, am Wasserende (x = 6) −0,45 m."""
    return 0.05 - 0.5 * (x + 1.0) / 7.0


def anlegesteg(st):
    netz = bs.Netz("Anlegesteg")
    x0, x1 = -1.0, 6.0
    bohlen(netz, st, x0, x1, -2.05, 2.05, steg_hoehe, saat=21)
    for y in (-1.4, 0.0, 1.4):                                    # Streckbalken
        latte_schraeg(netz, st, x0, x1, y - 0.1, y + 0.1, steg_hoehe(x0) - 0.08, steg_hoehe(x1) - 0.08, 0.26, "Holz",
                      ohne=("-z", "+z"))
    for s in (-1, 1):                                             # Saumbalken
        latte_schraeg(netz, st, x0, x1, s * 2.15 - 0.1, s * 2.15 + 0.1, steg_hoehe(x0) + 0.06, steg_hoehe(x1) + 0.06, 0.38,
                      "Holz")
    kasten(netz, st, -0.8, -0.5, -2.3, 2.3, -0.1, steg_hoehe(-0.5) - 0.34, "Holz alt", ohne=("-z",))   # Schwelle am Land
    for x in (1.6, 3.8, 5.8):                                     # Joche
        zt = steg_hoehe(x) - 0.34
        kasten(netz, st, x - 0.15, x + 0.15, -2.4, 2.4, zt - 0.28, zt, "Holz", ohne=("-z",))
        for y in (-1.9, -0.7, 0.7, 1.9):
            pfahl(netz, st, x, y, FUSS, zt - 0.28)
    kasten(netz, st, x1 - 0.12, x1 + 0.04, -2.3, 2.3, steg_hoehe(x1) - 0.4, steg_hoehe(x1) + 0.05, "Holz", ohne=("-z",))
    # Seilpfosten auf beiden Seiten (der Steg wird am anderen Ufer gedreht gesetzt) mit Strebe und Eisenring
    for s in (-1, 1):
        pfahl(netz, st, 0.4, s * 2.9, -0.3, 1.9, r=0.2, n=8, kopf="Scheitholz")
        netz.stab((-0.7, s * 2.9, 0.0), (0.3, s * 2.9, 1.25), 0.16, 0.16, st["Holz"])
        walze(netz, st, s * 2.9, 1.5, 0.23, 0.32, 0.48, "Eisen")
        lb.felsbrocken(netz, st, (-0.75, s * 2.72, 0), 0.3, 0.25, saat=3 + s)
    # Fährglocke stromab hinter dem Seilpfosten: zwei Pfosten, Querholz, kleines Dach, Glocke am Seil
    gx, gy = -1.5, -3.0
    for dx in (-0.35, 0.35):
        netz.quader((gx + dx, gy, 1.0), (0.12, 0.12, 2.0), st["Holz"], ohne=("-z",))
    netz.stab((gx - 0.5, gy, 1.95), (gx + 0.5, gy, 1.95), 0.12, 0.1, st["Holz"])
    for s in (-1, 1):
        netz.vieleck_nach([(gx - 0.6, gy, 2.35), (gx + 0.6, gy, 2.35), (gx + 0.6, gy + s * 0.4, 2.0), (gx - 0.6, gy + s * 0.4, 2.0)],
                          (0, s, 1), st["Schindel"])
    lb.kegel(netz, (gx, gy), 0.16, 1.55, 1.85, st["Bronze"], n=8)
    netz.stab((gx, gy, 1.85), (gx, gy, 1.95), 0.03, 0.03, st["Eisen"])
    netz.stab((gx + 0.05, gy, 1.55), (gx + 0.12, gy, 0.6), 0.025, 0.025, st["Seil"])
    o = teil(netz, x1 - x0)
    return [o, anschluss(o, "Anschluss_Steg_Weg", (x0, 0, steg_hoehe(x0)), 90),
            anschluss(o, "Anschluss_Steg_Wasser", (x1, 0, steg_hoehe(x1)), 270),
            anschluss(o, "Anker_Pfosten_A", (0.4, 2.9, 1.5), 270), anschluss(o, "Anker_Pfosten_B", (0.4, -2.9, 1.5), 270)]


# ─────────────────────────────────────────────────────────────────────────────
#  Kähne
# ─────────────────────────────────────────────────────────────────────────────

def kahn(st, name, laenge, breite, bord=0.55, tief=0.45, sprung=0.35, stationen=12, kajuete=False, saat=0):
    """Flachbodiger Lastkahn entlang X (Bug +X), Ursprung auf der Wasserlinie in der Mitte. Löffelförmig hochgezogene
    Enden mit kurzem Vor- und Achterdeck, Laderaum mit Bodenbrettern, Mast mit gerefftem Segel, Steuerruder am Heck."""
    netz = bs.Netz(name)
    ringe = []
    for i in range(stationen + 1):
        t = -1 + 2 * i / stationen
        a = abs(t)
        w = breite / 2 * (1 - (max(0.0, a - 0.45) / 0.55) ** 2 * 0.7)
        zb = -tief + (tief + bord * 0.6) * (max(0.0, a - 0.55) / 0.45) ** 2
        ringe.append((t * laenge / 2, w, w * 0.82, zb, bord + sprung * a * a))
    boden = -tief + 0.15
    mitte = Vector((0, 0, bord))

    def flur(ring):
        """Höhe und halbe Breite der Bodenbretter an einer Station (zu den Enden hin steigen sie mit dem Rumpfboden)."""
        _, w, b_, z, g = ring
        fz = max(boden, z + 0.06)
        return fz, b_ + (w - b_) * (fz - z) / (g - z) - 0.07

    for i, ((xa, wa, ba, za, ga), (xb, wb, bb, zb_, gb)) in enumerate(zip(ringe, ringe[1:])):
        (fza, fa_), (fzb, fb_) = flur(ringe[i]), flur(ringe[i + 1])
        ende = i in (0, stationen - 1)
        for s in (-1, 1):
            seite = [(xa, s * ba, za), (xb, s * bb, zb_), (xb, s * wb, gb), (xa, s * wa, ga)]
            unten, oben = [Vector(p) for p in seite[:2]], [Vector(p) for p in seite[:1:-1]]
            mitte_a, mitte_b = unten[0].lerp(oben[0], 0.5), unten[1].lerp(oben[1], 0.5)
            richtung = sum((Vector(p) for p in seite), Vector()) / 4 - mitte
            netz.vieleck_nach([unten[0], unten[1], mitte_b, mitte_a], richtung, st["Teerholz"])
            netz.vieleck_nach([mitte_a, mitte_b, oben[1], oben[0]], richtung, st["Holz alt"])
            if not ende:                                          # Innenseite und Dollbord
                ia = (xa, s * (wa - 0.07), ga - 0.02)
                ib = (xb, s * (wb - 0.07), gb - 0.02)
                netz.vieleck_nach([(xa, s * fa_, fza), (xb, s * fb_, fzb), ib, ia], (0, -s, 0.3), st["Holz alt"])
                netz.vieleck_nach([(xa, s * wa, ga), (xb, s * wb, gb), (xb, s * (wb - 0.07), gb), (xa, s * (wa - 0.07), ga)],
                                  (0, 0, 1), st["Holz"])
        netz.vieleck_nach([(xa, -ba, za), (xb, -bb, zb_), (xb, bb, zb_), (xa, ba, za)], ((xa + xb) / 2, 0, -1), st["Teerholz"])
        if ende:                                                  # Vor- und Achterdeck
            netz.vieleck_nach([(xa, -wa, ga), (xb, -wb, gb), (xb, wb, gb), (xa, wa, ga)], (0, 0, 1), st["Bretter"])
        else:
            netz.vieleck_nach([(xa, -fa_, fza), (xb, -fb_, fzb), (xb, fb_, fzb), (xa, fa_, fza)], (0, 0, 1), st["Bretter"])
    for i in (1, stationen - 1):                                  # Schotten unter den Decks
        x, w, b_, z, g = ringe[i]
        fz, f = flur(ringe[i])
        netz.vieleck_nach([(x, -f, fz), (x, f, fz), (x, w - 0.07, g), (x, -(w - 0.07), g)], (-x, 0, 0), st["Holz alt"])
    for x, w, b_, z, g in (ringe[0], ringe[-1]):                  # Stevenflächen
        netz.vieleck_nach([(x, -b_, z), (x, b_, z), (x, w, g), (x, -w, g)], (x, 0, 0), st["Teerholz"])
    # Mast mit gerefftem Segel an der Rah, Steuerruder am Heck
    xm = laenge * 0.18
    netz.stab((xm, 0, boden), (xm, 0, bord + 5.5), 0.16, 0.16, st["Holz"])
    walze(netz, st, 0, bord + 4.6, 0.16, xm - 0.12, xm + 0.12, "Holz")
    netz.stab((xm + 0.05, -1.7, bord + 4.9), (xm + 0.05, 1.7, bord + 4.9), 0.09, 0.09, st["Holz"], quer=(1, 0, 0))
    netz.prisma([(0.22 * math.cos(2 * math.pi * k / 6), bord + 4.72 + 0.17 * math.sin(2 * math.pi * k / 6)) for k in range(6)],
                -1.5, 1.5, st["Segeltuch"], matrix=Matrix.Translation((xm + 0.05, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    for s in (-1, 1):                                             # Wanten
        netz.stab((xm, s * (breite / 2 - 0.1), bord), (xm, s * 0.08, bord + 5.3), 0.03, 0.03, st["Seil"], quer=(1, 0, 0))
    x_heck, _, _, _, g_heck = ringe[0]
    netz.stab((x_heck - 0.1, 0, g_heck + 0.3), (x_heck - 0.9, 0, -0.5), 0.6, 0.08, st["Teerholz"], quer=(0, 1, 0))
    netz.stab((x_heck - 0.2, 0, g_heck + 0.2), (x_heck + 1.6, 0, g_heck + 0.45), 0.07, 0.07, st["Holz"])
    if kajuete:                                                   # kleine Hütte des Schiffers auf dem Achterdeck
        xk = -laenge / 2 + 1.7
        kasten(netz, st, xk - 0.8, xk + 0.8, -0.85, 0.85, boden, bord + 0.9, "Holz alt", ohne=("-z", "+z"))
        bs.satteldach(netz, st, xk - 0.8, xk + 0.8, -0.85, 0.85, bord + 0.9, 28, traufe=0.15, ortgang=0.12, enden="Holz alt",
                      **bs.SCHINDEL)
        for s in (-1, 1):
            bs.platte(netz, bs.wandmatrix((xk + s * 0.8, 0, bord + 0.9), "rechts" if s > 0 else "links"),
                      [(-0.85, 0), (0.85, 0), (0, 0.85 * math.tan(math.radians(28)))], 0, 0.1, st["Holz alt"])
        kasten(netz, st, xk + 0.8, xk + 0.83, -0.3, 0.3, boden + 0.4, bord + 0.65, "Loch", ohne=("-z",))
    # Ladung im Raum: Fässer, Säcke unter einer Plane, Kisten
    zf = nt.zufall(saat)
    x = -laenge / 2 + (3.0 if kajuete else 1.6)
    while x < xm - 0.9:
        for y in (-0.45, 0.45):
            if zf.random() < 0.8:
                bs.fass(netz, st, Vector((x, y, boden)), r=0.3, h=0.8, reifen=(0.15, 0.6))
        x += 0.68
    lb.kiste(netz, st, (xm + 0.9, -0.3, boden), (0.9, 0.8, 0.6), dreh=0.1)
    netz.quader((xm + 1.6, 0.25, boden + 0.4), (0.9, 1.0, 0.8), st["Segeltuch"], ohne=("-z",))
    return netz


def kahn_seil(netz, st, von, nach):
    """Festmacher vom Kahn zum Poller, leicht durchhängend (zwei Stücke)."""
    von, nach = Vector(von), Vector(nach)
    mitte = von.lerp(nach, 0.5) - Vector((0, 0, 0.25))
    netz.stab(von, mitte, 0.04, 0.04, st["Seil"])
    netz.stab(mitte, nach, 0.04, 0.04, st["Seil"])


# ─────────────────────────────────────────────────────────────────────────────
#  Hafen
# ─────────────────────────────────────────────────────────────────────────────

POLLER = [(-6.6, UFER - 0.45), (-1.4, UFER - 0.45), (1.8, UFER - 0.45), (6.8, UFER - 0.45)]
KRAN = Vector((3.6, 6.55, KAI))       # Fuß der Kransäule (Kaiserstiel)
RAD = Vector((3.6, 4.15, 2.55))       # Achse des Tretrads
RAD_R = 2.0


def hafen(st):
    netz = bs.Netz("Hafen")
    # Hof aus festgetretener Erde, ein Streifen Pflaster vor dem Kai
    lb.flaeche(netz, st, lb.rechteck(-8, -7, 8, 1.85), 0.01, "Erde hell")
    lb.flaeche(netz, st, lb.rechteck(-1.2, -1.2, 8, 1.85), 0.02, "Pflaster")
    # Kai: Bohlen auf Holz, Bordschwelle zum Hof, Kopfbalken an der Uferkante, Spundwand mit Pfählen
    bohlen(netz, st, -8.0, 8.0, 2.05, UFER - 0.2, lambda x: KAI, saat=31)
    kasten(netz, st, -8.0, 8.0, 1.85, 2.05, 0.0, KAI + 0.02, "Holz", ohne=("-z",))
    kasten(netz, st, -8.0, 8.0, UFER - 0.2, UFER, KAI - 0.25, KAI + 0.08, "Holz", ohne=("-z",))
    for x0 in (-8.0, 7.84):
        kasten(netz, st, x0, x0 + 0.16, 2.05, UFER - 0.2, 0.0, KAI, "Holz alt", ohne=("-z",))
    netz.vieleck_nach([(-8, UFER, FUSS), (8, UFER, FUSS), (8, UFER, KAI - 0.25), (-8, UFER, KAI - 0.25)], (0, 1, 0), st["Holz alt"])
    for k in range(8):
        pfahl(netz, st, -7.0 + 2.0 * k, UFER + 0.15, FUSS, KAI + 0.3, r=0.15, n=7, kopf="Scheitholz")
    for x, y in POLLER:
        pfahl(netz, st, x, y, KAI, KAI + 0.55, r=0.17, n=8, kopf="Scheitholz")
        kasten(netz, st, x - 0.2, x + 0.2, y - 0.2, y + 0.2, KAI + 0.32, KAI + 0.4, "Eisen")
    for dx in (-0.25, 0.25):                                       # Leiter an der Spundwand
        netz.stab((0.4 + dx, UFER + 0.09, KAI + 0.5), (0.4 + dx, UFER + 0.09, -1.6), 0.07, 0.06, st["Holz"])
    for k in range(7):
        z = KAI + 0.2 - 0.3 * k
        netz.stab((0.15, UFER + 0.1, z), (0.65, UFER + 0.1, z), 0.04, 0.04, st["Holz"])
    # Lagerschuppen mit Ziegeldach, Tür zum Hof
    lb.huette(netz, st, -7.5, -1.8, -4.6, 0.8, 3.0, wand="bretter", deckung=bs.ZIEGEL, neigung=42, tuer_u=-0.9,
              fenster_vorn=(1.6,), giebel_fenster=True)
    netz.stoffe = [st["Holz"] if m == st["Hausfarbe"] else m for m in netz.stoffe]    # Tür und Läden: der Hafen gehört der Stadt
    # Kranbalken am Giebel zum Kai mit Rolle und Seil
    netz.stab((-1.8, -1.9, 4.2), (-0.6, -1.9, 4.2), 0.16, 0.16, st["Holz"])
    walze(netz, st, -1.9, 4.05, 0.08, -0.85, -0.7, "Eisen", n=6)
    netz.stab((-0.78, -1.9, 4.0), (-0.78, -1.9, 2.4), 0.03, 0.03, st["Seil"])
    # Tretradkran: Böcke für die Radachse, Kaiserstiel mit Streben, Seil von der Trommel zur Säule
    for x in (RAD.x - 1.0, RAD.x + 1.0):
        for dy, dicke in ((-1.5, 0.18), (1.5, 0.16)):
            netz.stab((x, RAD.y + dy, KAI), (x, RAD.y, RAD.z + 0.18), 0.18, dicke, st["Holz"], quer=(1, 0, 0))
        netz.stab((x, RAD.y - 1.6, KAI + 0.1), (x, RAD.y + 1.6, KAI + 0.1), 0.2, 0.2, st["Holz"], quer=(1, 0, 0))
        kasten(netz, st, x - 0.12, x + 0.12, RAD.y - 0.2, RAD.y + 0.2, RAD.z - 0.1, RAD.z + 0.25, "Holz alt")
    pfahl(netz, st, KRAN.x, KRAN.y, KAI, 5.6, r=0.22, n=8, kopf="Holz")
    for dx, dy in ((-1.2, 0.0), (1.2, 0.0), (0.0, -1.0)):
        netz.stab((KRAN.x + dx, KRAN.y + dy, KAI), (KRAN.x + dx * 0.12, KRAN.y + dy * 0.12, 2.2), 0.14, 0.14, st["Holz"])
    trommel = Vector((RAD.x - 1.22, RAD.y + 0.15, RAD.z + 0.29))
    netz.stab(trommel, (KRAN.x, KRAN.y - 0.15, 5.5), 0.04, 0.04, st["Seil"])
    o = netz.objekt()
    return o


def tretrad(st):
    """Tretrad: zwei Kränze, Speichen, Trittbretter innen, Achse mit Seiltrommel. Ursprung in der Achse (dreht um X)."""
    netz = bs.Netz("Tretrad", alle=True)
    n, r, b = 16, RAD_R, 0.65
    for x in (-b, b):
        for k in range(n):
            w0, w1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
            d = 0.05 + 0.003 * (k % 2)                            # abwechselnd dicker: Nachbarn nicht in einer Ebene
            for ra, rb, normale in ((r, r, 1), (r - 0.14, r - 0.14, -1)):
                p = [(x - d, ra * math.cos(w0), ra * math.sin(w0)), (x + d, ra * math.cos(w0), ra * math.sin(w0)),
                     (x + d, rb * math.cos(w1), rb * math.sin(w1)), (x - d, rb * math.cos(w1), rb * math.sin(w1))]
                wm = (w0 + w1) / 2
                netz.vieleck_nach(p, (0, normale * math.cos(wm), normale * math.sin(wm)), st["Holz"])
            for sx in (-1, 1):
                p = [(x + sx * d, r * math.cos(w0), r * math.sin(w0)), (x + sx * d, r * math.cos(w1), r * math.sin(w1)),
                     (x + sx * d, (r - 0.14) * math.cos(w1), (r - 0.14) * math.sin(w1)),
                     (x + sx * d, (r - 0.14) * math.cos(w0), (r - 0.14) * math.sin(w0))]
                netz.vieleck_nach(p, (sx, 0, 0), st["Holz"])
        for k in range(8):                                        # Speichen, abwechselnd dicker (sonst eine Ebene)
            w = 2 * math.pi * (k + 0.5) / 8
            netz.stab((x, 0.18 * math.cos(w), 0.18 * math.sin(w)), (x, (r - 0.1) * math.cos(w), (r - 0.1) * math.sin(w)),
                      0.08, 0.08 + 0.012 * (k % 2), st["Holz"], quer=(1, 0, 0))
        netz.prisma([(0.2 * math.cos(2 * math.pi * k / 8), 0.2 * math.sin(2 * math.pi * k / 8)) for k in range(8)],
                    x - 0.07, x + 0.07, st["Holz alt"])           # Nabe
    for k in range(n):                                            # Trittbretter: y lokal = nach außen, z = entlang des Kranzes
        w = 2 * math.pi * (k + 0.5) / n
        t = Matrix.Translation((0, (r - 0.165) * math.cos(w), (r - 0.165) * math.sin(w))) @ Matrix.Rotation(w, 4, "X")
        netz.quader((0, 0, 0), (2 * b - 0.1, 0.05, 0.3), st["Bretter"], matrix=t)
    walze(netz, st, 0, 0, 0.13, -1.4, 1.15, "Holz")
    walze(netz, st, 0, 0, 0.3, -1.32, -1.12, "Holz", n=10)
    walze(netz, st, 0, 0, 0.32, -1.29, -1.15, "Seil", n=10)
    o = netz.objekt()
    o.location = RAD
    return o


def ausleger(st):
    """Ausleger des Krans mit Strebe, Rolle, Seil, Haken und einem Fass als Last. Ursprung in der Kranachse am Kai."""
    netz = bs.Netz("Ausleger")
    fuss, spitze = Vector((0, -0.2, 5.0 - KAI)), Vector((0, 2.6, 6.35 - KAI))
    netz.stab(fuss, spitze, 0.22, 0.2, st["Holz"], quer=(1, 0, 0))
    netz.stab((0, 0.2, 2.9 - KAI), fuss.lerp(spitze, 0.55), 0.16, 0.14, st["Holz"], quer=(1, 0, 0))
    for s in (-1, 1):
        netz.stab((s * 0.26, -0.1, 5.2 - KAI), (s * 0.26, -0.1, 4.4 - KAI), 0.08, 0.08, st["Holz alt"])
    walze(netz, st, spitze.y, spitze.z - 0.05, 0.14, -0.06, 0.06, "Eisen", n=6)
    netz.stab(fuss + Vector((0, 0.1, 0.18)), spitze + Vector((0, 0, 0.1)), 0.04, 0.04, st["Seil"])
    haken = Vector((0, spitze.y + 0.1, 1.9 - KAI))
    netz.stab(spitze + Vector((0, 0.12, -0.1)), haken, 0.04, 0.04, st["Seil"])
    netz.quader(haken + Vector((0, 0, -0.08)), (0.08, 0.06, 0.18), st["Eisen"])
    for s in (-1, 1):
        netz.stab(haken + Vector((s * 0.03, 0, -0.15)), haken + Vector((s * 0.3, 0, -0.6)), 0.03, 0.03, st["Seil"])
    bs.fass(netz, st, haken + Vector((0, 0, -1.4)), r=0.32, h=0.85)
    o = netz.objekt()
    o.location = KRAN
    return o


def ware(st):
    """Fässer, Kisten und Säcke auf dem Kai und vor dem Schuppen."""
    netz = bs.Netz("Ware")
    for i, (x, y) in enumerate(((-6.3, 3.0), (-5.6, 3.0), (-5.95, 3.6), (-4.9, 3.0))):
        bs.fass(netz, st, Vector((x, y, KAI)), r=0.32, h=0.9, reifen=(0.2 + 0.012 * i, 0.75 - 0.012 * i))
    bs.fass(netz, st, Vector((-5.95, 3.3, KAI + 0.9)), r=0.3, h=0.85)
    for x, y, dreh in ((-3.4, 3.2, 0.1), (-2.4, 3.4, -0.2), (-3.0, 4.4, 0.3)):
        lb.kiste(netz, st, (x, y, KAI), (0.8, 0.7, 0.6), dreh=dreh)
    lb.kiste(netz, st, (-3.0, 3.3, KAI + 0.6), (0.7, 0.6, 0.5), dreh=0.25)
    for k, (x, y) in enumerate(((0.6, 3.0), (1.1, 3.1), (0.85, 3.6), (1.4, 3.7))):
        lb.saeule(netz, st, (x, y, KAI), 0.24, 0, 0.45, "Sack", n=7)
        nt.krone(netz, st, (x, y, KAI + 0.52), 0.22, 0.09, saat=k, stoffe=("Sack", "Sack"), n=6, flach_unten=0.0)
    for i, (x, y) in enumerate(((-1.2, -3.0), (-0.48, -3.1))):
        bs.fass(netz, st, Vector((x, y, 0.02)), r=0.32, h=0.9, reifen=(0.2 + 0.012 * i, 0.75 - 0.012 * i))
    lb.kiste(netz, st, (-0.9, -2.2, 0.02), (0.8, 0.7, 0.6), dreh=-0.15)
    return netz.objekt()


def kaehne(st):
    """Zwei Kähne längsseits am Kai, Festmacher zu den Pollern. Ursprung je auf der eigenen Wasserlinie."""
    objekte = []
    for name, x, laenge, breite, kajuete, drehung, poller in (
            ("Kahn1", -4.0, 10.0, 2.8, True, 0.0, (POLLER[0], POLLER[1])),
            ("Kahn2", 5.0, 7.0, 2.4, False, 180.0, (POLLER[2], POLLER[3]))):     # Bug an Bug, die Ruder nach außen
        ort = Vector((x, UFER + 0.35 + breite / 2, WASSER))
        zurueck = Matrix.Rotation(math.radians(-drehung), 4, "Z")
        netz = kahn(st, name, laenge, breite, kajuete=kajuete, saat=len(name) + int(x))
        kai = -1 if drehung == 0 else 1                           # Seite des Kahns zum Kai (lokales y)
        for (px, py) in poller:
            ziel = zurueck @ Vector((px - ort.x, py - ort.y, KAI + 0.45 - ort.z))
            kahn_seil(netz, st, (math.copysign(laenge / 2 - 0.9, ziel.x), kai * (breite / 2 - 0.15), 0.7), ziel)
        o = netz.objekt()
        o.location = ort
        o.rotation_euler = (0, 0, math.radians(drehung))
        objekte.append(o)
    return objekte


def hafen_bauen(st):
    teile = [hafen(st), tretrad(st), ausleger(st), ware(st)] + kaehne(st)
    anker = [bs.anker("Anker_Ufer", (0, UFER, KAI), 0), bs.anker("Anker_Tuer", (-5.55, -4.95, 0), 0),
             bs.anker("Anker_Karren", (2.4, 0.2, 0), 0),
             bs.anker("Anker_Anleger_1", (-3.6, UFER - 0.6, KAI), 0), bs.anker("Anker_Anleger_2", (6.0, UFER - 0.6, KAI), 0),
             bs.anker("Anker_Tretrad", (RAD.x, RAD.y, RAD.z - RAD_R + 0.17), 90)]
    return teile + anker


# ─────────────────────────────────────────────────────────────────────────────
#  Furt- und Ufersteine
# ─────────────────────────────────────────────────────────────────────────────

FLACH = ((1.0, 0.0), (0.95, 0.8), (0.7, 1.0))       # Profil flacher, oben abgeschliffener Steine


def bis_ende(x1, ende=2.0, rest=0.2):
    """Ende eines Steins in einer Reihe bis `ende`: Bliebe dahinter weniger als `rest`, reicht der Stein bis ans Ende."""
    return ende if x1 > ende - rest else x1


def furt(netz, st, i):
    zf = nt.zufall(70 + i)
    if i == 0:                                       # Trittsteine in einer Reihe, dazwischen Kiesel
        for k in range(7):
            x = -2.1 + 0.7 * k + zf.uniform(-0.08, 0.08)
            y = zf.uniform(-0.15, 0.15)
            r = zf.uniform(0.24, 0.32)
            lb.felsbrocken(netz, st, (x, y, -0.6), r, 0.6 + zf.uniform(0.06, 0.12), saat=700 + k, n=7, profil=FLACH)
        for k in range(10):
            lb.felsbrocken(netz, st, (zf.uniform(-2.2, 2.2), zf.uniform(-0.9, 0.9), -0.12), zf.uniform(0.08, 0.15), 0.14,
                           saat=720 + k, n=5)
    elif i == 1:                                     # Schotterbank für Karren: flacher Hügel aus Kies, Spuren, Kiesel
        n, ringe = 10, []
        for f, z in ((1.0, -0.18), (0.75, -0.02), (0.4, 0.03)):
            ringe.append([Vector((2.3 * f * math.cos(2 * math.pi * k / n) * zf.uniform(0.9, 1.08),
                                  1.7 * f * math.sin(2 * math.pi * k / n) * zf.uniform(0.9, 1.08), z + zf.uniform(-0.02, 0.02)))
                          for k in range(n)])
        for a, b in zip(ringe, ringe[1:]):
            for k in range(n):
                netz.vieleck_nach([a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]], (0, 0, 1), st["Kies"])
        netz.vieleck_nach(ringe[-1], (0, 0, 1), st["Kies"])
        for k in range(14):
            w, r = zf.uniform(0, 2 * math.pi), zf.uniform(0.3, 1.0)
            lb.felsbrocken(netz, st, (2.2 * r * math.cos(w), 1.6 * r * math.sin(w), -0.08), zf.uniform(0.07, 0.16), 0.13,
                           saat=740 + k, n=5)
    else:                                            # gesetzte Steinfurt: Platten in Reihen, Randsteine stromauf
        for row in range(6):
            y = -1.25 + 0.46 * row
            x = -2.2 + (0.25 if row % 2 else 0.0)
            while x < 2.1:
                x1 = bis_ende(x + zf.uniform(0.45, 0.7), 2.2)
                z = zf.uniform(-0.01, 0.04)
                kasten(netz, st, x + 0.02, x1 - 0.02, y + 0.02, y + 0.44, -0.3, z,
                       "Bruchstein" if zf.random() < 0.5 else "Fels", ohne=("-z",))
                x = x1
        for k in range(6):
            lb.felsbrocken(netz, st, (-2.0 + 0.8 * k, 1.75 + zf.uniform(-0.05, 0.1), -0.6), zf.uniform(0.3, 0.38), 0.75, saat=760 + k,
                           n=6, profil=FLACH)


def ufersteine(netz, st, i):
    zf = nt.zufall(80 + i)
    boeschung = lambda y: -y                                            # noqa: E731  45°-Böschung ins Wasser (+Y)
    if i == 0:                                       # Steinschüttung auf der Böschung
        for k in range(18):
            x = -1.9 + 3.8 * (k % 6) / 5 + zf.uniform(-0.2, 0.2)
            y = 0.25 + 0.5 * (k // 6) + zf.uniform(-0.1, 0.1)
            r = zf.uniform(0.25, 0.4)
            lb.felsbrocken(netz, st, (x, y, boeschung(y) - r * 0.6), r, r * 1.3, saat=800 + k, n=6)
        for k in range(4):
            bs.buschel(netz, st, Vector((-1.8 + 1.2 * k, -0.1, 0.0)), groesse=0.45)
    elif i == 1:                                     # gesetztes Steinpflaster auf der Böschung, oben Decksteine
        quer = 1 / math.sqrt(2)
        for reihe in range(5):
            d = 0.3 + 0.44 * reihe                   # Abstand von der Kante entlang der Böschung
            y, z = d * quer, -d * quer
            x = -2.0 + (0.22 if reihe % 2 else 0.0)
            ende = 2.0 - 0.013 * reihe                # Enden gestaffelt (sonst liegen die Stirnseiten in einer Ebene)
            while x < ende - 0.05:
                x1 = bis_ende(x + zf.uniform(0.38, 0.62), ende)
                # Stein im System der Böschung: x entlang des Ufers, y hangabwärts, z aus der Böschung heraus
                t = Matrix(((1, 0, 0, (x + x1) / 2), (0, quer, quer, y), (0, -quer, quer, z), (0, 0, 0, 1)))
                vor = zf.uniform(0.05, 0.1)
                netz.quader((0, 0, (vor - 0.15) / 2), (x1 - x - 0.03, 0.4, 0.15 + vor),
                            st["Bruchstein" if zf.random() < 0.6 else "Bruchstein dunkel"], matrix=t, ohne=("-z",))
                x = x1
        x = -2.0
        while x < 1.95:
            x1 = bis_ende(x + zf.uniform(0.5, 0.8))
            kasten(netz, st, x + 0.025, x1 - 0.025, -0.35, 0.12, -0.15, 0.06 + zf.uniform(0, 0.03), "Bruchstein", ohne=("-z",))
            x = x1
    else:                                            # Pfahlreihe an der Kante, Steine davor im Wasser
        for k in range(12):
            x = -1.9 + 3.8 * k / 11
            pfahl(netz, st, x, 0.15 + zf.uniform(-0.03, 0.03), -1.8, zf.uniform(0.05, 0.28), r=0.09, n=5, stoff="Holz alt",
                  kopf="Scheitholz")
        linie(netz, st, (-2.0, 0.26, -0.1), (2.0, 0.26, -0.1), (0, 1, 0), breite=0.12, stoff="Holz alt")
        for k in range(9):
            x = -1.8 + 3.6 * k / 8 + zf.uniform(-0.15, 0.15)
            y = 0.6 + zf.uniform(0, 0.5)
            r = zf.uniform(0.22, 0.34)
            lb.felsbrocken(netz, st, (x, y, boeschung(y) - r * 0.5), r, r * 1.2, saat=830 + k, n=6)


def steine_bauen(st):
    teile = []
    for art, bauer in (("Furt", furt), ("Ufersteine", ufersteine)):
        for i in range(3):
            netz = bs.Netz(f"{art}{i + 1}")
            bauer(netz, st, i)
            o = netz.objekt()
            o["laenge"] = 4.0
            teile.append(o)
    return teile


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau: Bühne mit Flussbett, Kopien der Teile
# ─────────────────────────────────────────────────────────────────────────────

def _wasserstoff():
    import bpy
    m = bpy.data.materials.get("_Wasser") or bpy.data.materials.new("_Wasser")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*bs._linear((0.13, 0.20, 0.21)), 1)
    p.inputs["Roughness"].default_value = 0.12
    return m


def buehne(ausschnitt, ziel_hoehe, schnell):
    """Kamera, Sonne und Himmel wie bei den anderen Modellen, aber ohne den flachen Boden (den legt gelaende an)."""
    import bpy
    bs.vorschau_buehne(None, ziel_hoehe=ziel_hoehe, ausschnitt=ausschnitt, schnell=schnell)
    bpy.data.objects.remove(bpy.data.objects["_Umgebung"])


def gelaende(profil, wasser, quer=60.0, achse="x"):
    """Hilfsgelände der Vorschau: Höhenprofil [(u, z), …] quer zum Fluss (entlang `achse`), das Wasser als Fläche
    zwischen den u in `wasser` auf Höhe WASSER (bzw. dem dritten Wert)."""
    netz = bs.Netz("_Gelaende")
    boden, ufer = bs._hilfsstoff("_Boden", (0.33, 0.33, 0.25)), bs._hilfsstoff("_Ufer", (0.36, 0.31, 0.24))

    def p(u, v, z):
        return (u, v, z) if achse == "x" else (v, u, z)
    for (u0, z0), (u1, z1) in zip(profil, profil[1:]):
        m = boden if z0 >= -0.01 and z1 >= -0.01 else ufer
        netz.vieleck_nach([p(u0, -quer, z0), p(u1, -quer, z1), p(u1, quer, z1), p(u0, quer, z0)], (0, 0, 1), m)
    netz.objekt()
    w = bs.Netz("_Wasser")
    w0, w1 = wasser[0], wasser[1]
    hoehe = wasser[2] if len(wasser) > 2 else WASSER
    w.vieleck_nach([p(w0, -quer, hoehe), p(w1, -quer, hoehe), p(w1, quer, hoehe), p(w0, quer, hoehe)], (0, 0, 1), _wasserstoff())
    w.objekt()


def kopie(o, ort, drehung=0.0, eltern=None):
    """Kopie eines Teils samt Netz-Kindern (für die Vorschau), um `drehung` Grad um Z gedreht."""
    import bpy
    k = bpy.data.objects.new("_K_" + o.name, o.data)
    bpy.context.scene.collection.objects.link(k)
    if eltern is None:
        k.location = Vector(ort)
        k.rotation_euler = (0, 0, math.radians(drehung))
    else:
        k.parent = eltern
        k.matrix_parent_inverse = Matrix.Identity(4)
        k.location = o.location.copy()
        k.rotation_euler = o.rotation_euler.copy()
    for c in o.children:
        if c.type == "MESH":
            kopie(c, None, eltern=k)
    return k


def alle_kopien():
    import bpy
    return [o for o in bpy.context.scene.objects if o.name.startswith("_K_")]


def bild_teile(name, objekte, orte, ausschnitt, ziel_hoehe):
    """Die Teile nebeneinander auf flachem Boden (nur fürs Bild verschoben). Gibt den Bildpfad zurück."""
    import bpy
    netze = [o for o in objekte if o.type == "MESH" and o.parent is None]
    alt = {o: o.location.copy() for o in netze}
    for o, (x, y) in zip(netze, orte):
        o.location = Vector((x * 0.7071 - y * 0.7071, x * 0.7071 + y * 0.7071, 0)) + Vector((0, 0, alt[o].z))
    boden = bs.Netz("_Flach")
    boden.quader((0, 0, -0.05), (200, 200, 0.1), bs._hilfsstoff("_Boden", (0.33, 0.33, 0.25)))
    flach = boden.objekt()
    szene = bpy.context.scene
    szene.camera.data.ortho_scale = ausschnitt
    szene["_ziel"] = ziel_hoehe
    sichtbar = set(netze) | {k for o in netze for k in o.children_recursive}
    pfad = bs.rendern(name, lambda o: o in sichtbar and o.name != "_Gelaende")
    for o in netze:
        o.location = alt[o]
    bpy.data.objects.remove(flach)
    return pfad


def bild_aufbau(name, ausschnitt, ziel_hoehe, drehung=0):
    import bpy
    szene = bpy.context.scene
    szene.camera.data.ortho_scale = ausschnitt
    szene["_ziel"] = ziel_hoehe
    kopien = set(alle_kopien())
    return bs.rendern(name, lambda o: o in kopien, drehung)


def stempel(text):
    """Beschriftung oben links in den nächsten Renderbildern (Blenders Stempel, braucht kein Pillow); None schaltet ab."""
    import bpy
    r = bpy.context.scene.render
    for k in dir(r):
        if k.startswith("use_stamp_"):
            setattr(r, k, False)
    r.use_stamp = bool(text)
    if text:
        r.use_stamp_note = True
        r.stamp_note_text = text
        r.stamp_font_size = max(12, int(24 * r.resolution_y * r.resolution_percentage / 100 / 900))
        r.stamp_foreground = (0.94, 0.92, 0.86, 1.0)
        r.stamp_background = (0.0, 0.0, 0.0, 0.55)


def nebeneinander(name, bilder):
    """Bilder gleicher Größe nebeneinander zu docs/modelle/<name>.png (OpenImageIO, liegt Blender bei); die Einzelbilder
    werden gelöscht."""
    stempel(None)
    return bs.zusammensetzen(name, bilder, spalten=len(bilder))


def entfernen(objekte):
    import bpy
    for o in objekte:
        bpy.data.objects.remove(o)


def mitte_setzen(kopien, mitte):
    for k in kopien:
        if k.parent is None:
            k.location -= Vector(mitte)


def vorschau_holz(objekte, schnell):
    t = {o.name: o for o in objekte}
    buehne(30.0, 0.6, schnell)
    stempel("Teile: Holz_Joch, Holz_Ende, Holz_Gelaender")
    a = bild_teile("_wasser_holz_teile", objekte, [(-7.5, 0), (0, 0), (6.5, 0)], 26.0, 0.8)
    # Aufbau: 12 m Weite, drei Joche, Geländer auf beiden Seiten
    weite = 12.0
    gelaende([(-60, 0), (0, 0), (1.6, -2.4), (weite - 1.6, -2.4), (weite, 0), (60, 0)], (0, weite))
    kopie(t["Holz_Ende"], (0, 0, 0))
    kopie(t["Holz_Ende"], (weite, 0, 0), 180)
    for i in range(3):
        x = 2.0 + 4.0 * i
        kopie(t["Holz_Joch"], (x, 0, 0))
        kopie(t["Holz_Gelaender"], (x, 0, 0))
        kopie(t["Holz_Gelaender"], (x, 0, 0), 180)
    mitte_setzen(alle_kopien(), (weite / 2, 0, 0))
    for n in ("_Gelaende", "_Wasser"):
        import bpy
        bpy.data.objects[n].location.x -= weite / 2
    stempel("12 m Weite: Endstück, 3 Joche mit Geländer, Endstück")
    b = bild_aufbau("_wasser_holz_aufbau", 26.0, 0.0)
    return nebeneinander("wasser_holzbruecke", [a, b])


def vorschau_stein(objekte, schnell):
    import bpy
    t = {o.name: o for o in objekte}
    buehne(40.0, 1.0, schnell)
    stempel("Teile: Stein_Bogen, Stein_Pfeiler, Stein_Rampe, Stein_Bruestung")
    a = bild_teile("_wasser_stein_teile", objekte, [(-12, 0), (-4.5, -1), (8, 0), (-4.5, 6)], 34.0, 1.0)
    # Aufbau: 28 m Weite, drei Bögen, zwei Pfeiler, Rampen an beiden Ufern
    weite = 3 * BOGEN + 2 * PFEILER
    gelaende([(-80, 0), (0, 0), (2.0, -2.8), (weite - 2.0, -2.8), (weite, 0), (80, 0)], (0, weite))
    kopie(t["Stein_Rampe"], (0, 0, 0))
    kopie(t["Stein_Rampe"], (weite, 0, 0), 180)
    x = 0.0
    for i in range(3):
        kopie(t["Stein_Bogen"], (x + BOGEN / 2, 0, 0))
        kopie(t["Stein_Bruestung"], (x + BOGEN / 2, 0, 0))
        kopie(t["Stein_Bruestung"], (x + BOGEN / 2, 0, 0), 180)
        x += BOGEN
        if i < 2:
            kopie(t["Stein_Pfeiler"], (x + PFEILER / 2, 0, 0))
            x += PFEILER
    mitte_setzen(alle_kopien(), (weite / 2, 0, 0))
    for n in ("_Gelaende", "_Wasser"):
        bpy.data.objects[n].location.x -= weite / 2
    stempel("28 m Weite: Rampe, 3 Bögen, 2 Pfeiler, Rampe")
    b = bild_aufbau("_wasser_stein_aufbau", 52.0, 0.0)
    return nebeneinander("wasser_steinbruecke", [a, b])


def faehre_setzen(t, steg_x, richtung):
    """Fähre an einem Steg (Steg bei steg_x, Wasser in `richtung` ±1): Klappe liegt 0,4 m auf dem Steg."""
    scharnier = steg_x + richtung * (6.0 - 0.4 + KLAPPE)
    f = kopie(t["Faehre"], (scharnier + richtung * F_HALB, 0, WASSER), 0 if richtung > 0 else 180)
    # Klappe zum Steg auf dessen Höhe neigen
    hub = steg_hoehe(6.0 - 0.4) - (WASSER + F_DECK)
    winkel = math.asin(max(-1.0, min(1.0, hub / KLAPPE)))
    for k in f.children:
        if k.name.endswith("Klappe_A"):
            k.rotation_euler = (0, winkel, 0)
    return f


def vorschau_faehre(objekte, schnell):
    import bpy
    t = {o.name: o for o in objekte}
    buehne(30.0, 0.5, schnell)
    weite = 34.0
    gelaende([(-60, 0), (0, 0), (2.0, -2.6), (weite - 2.0, -2.6), (weite, 0), (60, 0)], (0, weite))
    kopie(t["Anlegesteg"], (0, 0, 0))
    kopie(t["Anlegesteg"], (weite, 0, 0), 180)
    f = faehre_setzen(t, 0.0, 1)
    # Fährseil vom Seilpfosten über die Fähre zum anderen Steg
    seil = bs.Netz("_Seil")
    st = bs.Stoffe()
    bpy.context.view_layer.update()
    a = Vector((0.4, 2.9, 1.5))                                   # Anker_Pfosten_A des ersten Stegs
    b = Vector((weite - 0.4, 2.9, 1.5))                           # Anker_Pfosten_B des gedrehten Stegs
    fa = f.matrix_world @ Vector((-4.1, 1.85, F_DECK + 1.2))
    fb = f.matrix_world @ Vector((4.1, 1.85, F_DECK + 1.2))
    seil.stab(a, fa, 0.05, 0.05, st["Seil"])
    seil.stab(fb, b, 0.05, 0.05, st["Seil"])
    seil.objekt()
    mitte_setzen(alle_kopien(), (11.0, 0, 0))
    for n in ("_Gelaende", "_Wasser", "_Seil"):
        bpy.data.objects[n].location.x -= 11.0
    stempel("Fähre am Anlegesteg")
    nah = bild_aufbau("_wasser_faehre_nah", 22.0, -0.3)
    for n in ("_Gelaende", "_Wasser", "_Seil"):
        bpy.data.objects[n].location.x -= weite / 2 - 11.0
    for k in alle_kopien():
        if k.parent is None:
            k.location.x -= weite / 2 - 11.0
    stempel("34 m Weite: zwei Anlegestege, Fähre, Fährseil")
    weit = bild_aufbau("_wasser_faehre_weit", 46.0, 0.0)
    return nebeneinander("wasser_faehre", [nah, weit])


def vorschau_hafen(objekte, schnell):
    buehne(30.0, 1.5, schnell)
    gelaende([(-60, 0), (UFER, 0), (UFER + 0.01, -3.2), (60, -3.2)], (UFER, 60), achse="y")
    import bpy
    for n in ("_Gelaende", "_Wasser"):
        bpy.data.objects[n].location.y -= 3.0
    for o in objekte:
        if o.parent is None:
            o.location.y -= 3.0
    alles = set(objekte) | {k for o in objekte for k in o.children_recursive}
    pfad = bs.rendern("wasser_hafen", lambda o: o in alles)
    bilder = [pfad]
    if not schnell:                         # vier Blickrichtungen und die Anker
        import bpy
        szene = bpy.context.scene
        prozent = szene.render.resolution_percentage
        szene.render.resolution_percentage = prozent // 2
        teile = []
        for k, text in ((0, "Blick von vorne rechts"), (1, "Blick von hinten rechts"), (3, "Blick von vorne links"),
                        (2, "Blick von hinten links")):
            stempel(text)
            teile.append(bs.rendern(f"_wasser_hafen-blick{k}", lambda o: o in alles, k))
        szene.render.resolution_percentage = prozent
        stempel(None)
        bilder.append(bs.zusammensetzen("wasser_hafen-rundum", teile, spalten=2))
        bilder.append(bs.anker_bild("wasser_hafen", lambda o: o in alles))
    for o in objekte:
        if o.parent is None:
            o.location.y += 3.0
    return bilder


def vorschau_steine(objekte, schnell):
    import bpy
    t = {o.name: o for o in objekte}
    buehne(16.0, -0.3, schnell)
    # Bach entlang Y (Furten quer darüber) und daneben ein Ufer entlang X
    gelaende([(-60, 0), (-2.6, 0), (-2.2, -0.45), (2.2, -0.45), (2.6, 0), (60, 0)], (-2.6, 2.6, 0.0))
    for i in range(3):
        kopie(t[f"Furt{i + 1}"], (0, (i - 1) * 4.6, 0))
    stempel("Furt1–3: Trittsteine, Schotterbank, Steinfurt")
    a = bild_aufbau("_wasser_steine_furt", 16.0, -0.3)
    entfernen(alle_kopien() + [bpy.data.objects["_Gelaende"], bpy.data.objects["_Wasser"]])
    # Wasser zur Kamera hin (−Y): die Teile um 180° gedreht, wie im Spiel bei einer anderen Blickrichtung
    gelaende([(-60, -1.6), (-1.6, -1.6), (0, 0), (60, 0)], (-60, -0.6), achse="y")
    for i in range(3):
        kopie(t[f"Ufersteine{i + 1}"], ((i - 1) * 4.6, 0, 0), 180)
    stempel("Ufersteine1–3: Steinschüttung, Steinpflaster, Pfahlreihe")
    b = bild_aufbau("_wasser_steine_ufer", 16.0, -0.6)
    return nebeneinander("wasser_steine", [a, b])


# ─────────────────────────────────────────────────────────────────────────────
#  Ausgabe
# ─────────────────────────────────────────────────────────────────────────────

DATEIEN = [
    ("wasser_holzbruecke", lambda st: holz_joch(st) + holz_ende(st) + holz_gelaender(st), vorschau_holz),
    ("wasser_steinbruecke", lambda st: stein_bogen(st) + stein_pfeiler(st) + stein_rampe(st) + stein_bruestung(st),
     vorschau_stein),
    ("wasser_faehre", lambda st: faehre(st) + anlegesteg(st), vorschau_faehre),
    ("wasser_hafen", hafen_bauen, vorschau_hafen),
    ("wasser_steine", steine_bauen, vorschau_steine),
]


def main():
    schnell = "--schnell" in sys.argv
    nur = [a.split("=", 1)[1] for a in sys.argv if a.startswith("--nur=")]
    for name, bauen, vorschau in DATEIEN:
        if nur and name not in nur:
            continue
        bs.neue_szene()
        st = bs.Stoffe()
        objekte = bauen(st)
        netze = [o for o in objekte if o.type == "MESH"]
        bs.bericht(name, netze + [o for o in objekte if o.type != "MESH"], einzeln=True)
        bs.BIBLIOTHEK_JE_FLAECHE = True     # Modell-Texturen, Gruppe 3: was in „Farben“ landet, trägt seine Schicht je Fläche
        vorher, nachher = bs.farben_zusammenlegen(netze, behalten=BEHALTEN)
        print(f"  Materialien: {vorher} → {nachher}")
        print("geschrieben:", bs.exportieren(name, objekte))
        if "--ohne-bilder" not in sys.argv:
            print(vorschau(objekte, schnell))


if __name__ == "__main__":
    main()
