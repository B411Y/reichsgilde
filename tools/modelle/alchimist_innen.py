"""Innenraum des Alchimisten: Werkraum, darüber das Kräuterlager unter dem Dach, ab dem Kräuterladen der Laden und ab der
Alchimistenstube der achteckige Laborturm; Stufen Tinkturei, Kräuterladen, Alchimistenstube.

    blender -b --python tools/modelle/alchimist_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/alchimist_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/alchimist_innen.glb und Bilder nach docs/modelle/ (alchimist_innen-blicke.png: das Bild jedes
Blickpunkts, alchimist_innen-stufen.png: Erdgeschoss von oben je Stufe, alchimist_innen-grundriss.png: Erdgeschoss und
Dachboden mit Ankern).

Koordinaten wie beim Schmied (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte des
Werkraums auf dem Boden. Das Laboratorium ist außen verputzt auf einem Bruchsteinsockel, innen wie beim Schmied ein
Steinraum (0,5 m, gekalkter Putz, an Ecken und unten bricht der Bruchstein durch; Boden in Stufe 1 und 2 gestampfter Lehm,
in Stufe 3 Backsteinpflaster im Fischgrätmuster; Balkendecke):
    Werkraum    innen x −4 … 4, y −3,5 … 3,5, 3,4 m hoch. Der Kessel steht frei vor der Rückwand unter einem Rauchfang
                (Ruß darüber), der Kräutertisch mit dem Mörser links, ab Stufe 2 die Destille rechts; eine steile Treppe an
                der rechten Wand führt ins Kräuterlager.
    Lager       darüber unter dem Dach (Boden in 3,45 m, First entlang x, Kniestock 1,3 m, 54° wie außen, Ladeluke mit
                Kranbalken im rechten Giebel), Kräuterbündel an Leinen, Kisten, Fässer.
    Laden       ab Stufe 2 rechts neben dem Werkraum (der Ladenanbau außen): x 4,5 … 9, y −3,5 … 0,5, 3,0 m hoch; Tresen,
                Regale mit Tiegeln und Flaschen, Tür zur Gasse (nur Kulisse), Schild.
    Turm        ab Stufe 3 hinter dem Werkraum: achteckig wie außen, innen 4,6 m von Wand zu Wand (Mitte x 1,4, y 6,3),
                5,2 m hoch, Backsteinboden; Athanor mit Blasebalg und Alembik, Tierkreis an der Wand, Bücher.
Alle Räume sind rundum geschlossen, die Kamera steht im Raum.

Knoten im GLB – dieselben Regeln wie in schmied_innen.glb, tischler_innen.glb und steinmetz_innen.glb (Raum_…, Decke_…,
Stufe1–Stufe3, Bedienung_…, Tuer_…, Einrichtung_…, Holztuer, Blick_…, Arbeit_…, Lager_…, Werkstueck_…, Licht_…, Kranbalken,
Himmel_Lager; Tuer_Werk_Lager ist die Treppe, Tuer_Lager_Werk die Bodenluke, beide mit extras „schild“), dazu:
    Raum_Laden                  extras „stufe“ = 2, ebenso Tuer_Werk_Laden, Tuer_Laden_Werk und alle Anker und Blickpunkte
                                darin; in Stufe 1 ist die Tür im Werkraum vermauert
    Raum_Turm                   extras „stufe“ = 3, ebenso Tuer_Werk_Turm, Tuer_Turm_Werk und alles darin; in Stufe 1 und 2
                                ist die Tür vermauert
    Fischgraet_Laden            Backsteinpflaster des Ladens (unter Raum_Laden), extras „stufe“ = 3: liegt 2 cm über dem
                                Lehmboden und deckt ihn ab Stufe 3 zu
    Auslage_1–8                 Stellplätze der Ware im Laden (leere Knoten auf den Regalbrettern, Mitte der Brettoberkante,
                                Blick −Z in den Laden): ein Platz ist 0,85 m breit, 0,32 m tief, 0,40 m hoch bis zum nächsten
                                Brett; das Spiel stellt dort Tiegel und Flaschen in der Farbe der Ware nach Menge auf
    Werkstueck_1–4              hier keine leeren Knoten, sondern Netze im Material „Fluessigkeit“, das das Spiel in der Farbe
                                der Ware färbt (Ursprung in der Mitte der Oberfläche): 1 der Brei im Mörser, 2 die Brühe im
                                Kessel, 3 der Auszug im Brennkolben der Destille (stufe 2), 4 der Auszug im Kolben des Alembiks
                                am Athanor (stufe 3, im Turm). Darunter liegt jeweils ein dunkler Boden: Ohne Werkstück ist das
                                Gefäß leer. Kinder im selben Material (färbt das Spiel mit): Blase_1–7 (Werkstueck_2), Tropfen
                                und Destillat_3 (Werkstueck_3), Destillat_4 (Werkstueck_4)
    Dampf_1–3                   leere Knoten, an denen das Spiel bei der Arbeit Dampf aufsteigen lässt: über der Brühe im
                                Kessel, am Hals des Brennkolbens (stufe 2), am Hals des Kolbens im Turm (stufe 3)
    Arbeit_1_Moersern, Arbeit_2_Ruehren, Arbeit_3_Destillieren (stufe 2), Arbeit_4_Blasebalg (stufe 3, im Turm): die Höhen
                                passen zu figuren.ARBEITSHOEHEN („Moerser“, „Kessel“, „Ofen“, „Hebel“) und figuren.VORLAGE
Animation „Arbeit“ (4 s, als Schleife zu spielen, solange gearbeitet wird): Im Kessel steigen Blasen auf und platzen, an der
Vorlage der Destille bildet sich ein Tropfen und fällt in den Trichter (zweimal je Schleife). Ruht der Betrieb (Zeit 0),
ist keine Blase und kein Tropfen zu sehen. Glut und Dampf sind Sache des Spiels.
Lichter: extras „art“ = „Fenster“, „Lampe“ (Laterne) und „Glut“ (Feuer unter dem Kessel, Feuertür der Destille ab Stufe 2,
Athanor ab Stufe 3).
Materialien: „Farben“ (Eckenfarben; die Materialart für die Oberflächendetails je Fläche als UV.y, bausteine.arten_setzen),
„Fluessigkeit“ (Werkstücke), „Hausfarbe“ (Hoftor, Holztür, Ladentür, Flügel der Ladeluke, Schild), „Fenster“ (auch der
Himmel hinter der Ladeluke), „Glut“ (Feuer, Laternen), „Eisen“, „Wasser“.
Einrichtung_Destillierhelm: kupferner Helm an einer Kette unter dem Rauchfang über dem Kessel (hochgezogen, damit gerührt
werden kann), Schnabel und Kühlrohr zu einem Krug neben dem Kessel – in jeder Stufe gleich, so wird der Kessel zur
Brennblase. Die Destille ab Stufe 2 hat ihren eigenen Tonhelm.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import figuren as fg  # noqa: E402  (Arbeitshöhen der Bewegungen Mörsern, Rühren, Destillieren, Blasebalg)
import innenausbau as ia  # noqa: E402  (Böden, Putz, Ruß, Decke, Treppe, Dachboden, Prüfbericht, Vorschau)
import schmied_innen as si  # noqa: E402  (Mauern, Fenster, Pult, Türen, Werkzeugwand, Kisten, Löschfass, Blasebalg, Anker)
import tischler_innen as ti  # noqa: E402  (Hoftor, Laterne, Animation, Export)

NAME = "alchimist_innen"
STUFEN = ("Tinkturei", "Kräuterladen", "Alchimistenstube")

bs.FARBEN.update({
    "Fluessigkeit": (0.30, 0.42, 0.22),     # Werkstück (Brei, Brühe, Auszug, Destillat): das Spiel färbt es in der Farbe der Ware
    "Glaskolben": (0.56, 0.64, 0.58),       # grünliches Glas der Kolben und Flaschen (deckend gemalt)
    "Kupferblech": (0.60, 0.35, 0.21),      # blankes Kupfer: Kessel, Helm, Kühlrohr
    "Ziegelmauer": (0.46, 0.28, 0.21),      # Ofen der Destille und Athanor (in den Oberflächendetails mit Mauerstruktur)
    "Tiegel": (0.70, 0.60, 0.46),           # irdene Tiegel und der Tonhelm
    "Tiegel dunkel": (0.48, 0.38, 0.28),
    "Steingut": (0.55, 0.52, 0.45),        # graues Steinzeug: Krüge, Flaschen
    "Nachthimmel": (0.16, 0.19, 0.31),      # Grund des Tierkreises
    "Pergament": (0.80, 0.72, 0.55),
    "Kraut dunkel": (0.23, 0.32, 0.17),
    "Leinen": (0.84, 0.81, 0.72),
})
bs.RAUHEIT["Fluessigkeit"] = 0.35           # eigenes Material (nicht in „Farben“): das Spiel färbt es je Werkstück

# ── Maße ─────────────────────────────────────────────────────────────────────
WAND = si.WAND
WX0, WX1, WY0, WY1 = -4.0, 4.0, -3.5, 3.5                          # Werkraum innen
H_WERK = 3.4
Z_LAGER = H_WERK + ia.DIELE                                        # Boden des Kräuterlagers unter dem Dach
DACH = ia.Dachboden(WX0, WX1, WY0, WY1, Z_LAGER, 54)
LX0, LX1, LY0, LY1 = WX1 + WAND, 9.0, WY0, 0.5                     # Laden innen (ab Stufe 2)
H_LADEN = 3.0
TA = 2.3                                                           # Turm: Abstand der Wände von der Mitte (innen)
TR = TA / math.cos(math.radians(22.5))                             # … und der Ecken
TM = Vector((1.4, WY1 + WAND + TA, 0))                             # Mitte des Turms (ab Stufe 3)
H_TURM = 5.2
TREPPE_X, TREPPE_FUSS, TREPPE_OBEN = 3.63, 3.0, 1.6               # steile Treppe an der rechten Wand, steigt nach vorn
LUKE = (3.25, WX1, 1.56, 2.70)                                     # Treppenloch in der Decke (x0, x1, y0, y1)
LADELUKE_Y = -1.1                                                  # Ladeluke im rechten Giebel (Mitte), 1,2 × 1,8 m
LADENTUER_Y, LADENTUER_B, LADENTUER_H = -2.3, 1.0, 2.2             # Tür Werkraum – Laden (rechte Wand)
TURMTUER_B, TURMTUER_H = 1.0, 2.2                                  # Tür Werkraum – Turm (Rückwand, bei TM.x)
HOFTOR_Y, HOFTOR_B, HOFTOR_H = -0.3, 2.6, 3.0                      # Hoftor in der linken Wand
HOLZTUER_X, HOLZTUER_B, HOLZTUER_H = -1.6, 0.95, 2.1               # Tür zum Hof in der Vorderwand
GASSENTUER_X = 5.4                                                 # Ladentür zur Gasse (nur Kulisse)
SICHTFELD = 50.0                                                   # Grad, senkrecht; Bild 16:9
FPS = 24
ARBEIT_BILDER = 4 * FPS                                            # Länge der Animation „Arbeit“

# Arbeitsplätze (Fußpunkt der Figur) und Geräte – die Höhen kommen aus figuren.ARBEITSHOEHEN
W1 = Vector((-2.3, 1.15, 0))                    # Mörsern am Kräutertisch, Blick −y
MOERSER = W1 + Vector((0, -fg.ARBEITSHOEHEN["Moerser"][0], 0))
KESSEL = Vector((-0.3, 2.15, 0))                # Kessel auf dem Dreifuß
W2 = KESSEL + Vector((0, fg.ARBEITSHOEHEN["Kessel"][0], 0))       # Rühren, Blick −y (zur Kamera)
KESSEL_R, KESSEL_RAND, BRUEHE = 0.42, 0.95, 0.85
W3 = Vector((1.2, 1.1, 0))                      # Destillieren, Blick +x
OFEN_VOR = 0.46                                 # Vorderseite des Ofens vor der Figur (der Schieber steht 3 cm vor)
OFEN = W3 + Vector((OFEN_VOR + 0.31, 0.03, 0))  # Mitte des Ofens (0,62 × 0,6 m, 0,75 m hoch)
VORLAGE = W3 + Vector((fg.VORLAGE[1], fg.VORLAGE[0], 0))          # Blick +x: vor → +x, rechts → −y
ATHANOR = TM + Vector((0.9, 1.0, 0))            # im Turm, die Feuertür nach −y
KSCHLOT = (KESSEL.x - 0.45, KESSEL.x + 0.45, WY1 - 0.55, WY1)     # Schlot des Rauchfangs durch Decke und Dachboden

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Werk_0": ((0.4, -3.1, 2.7), (-0.4, 1.8, 0.9)),          # Kessel, Kräutertisch, Destille, Tür zum Turm
    "Blick_Werk_L1": ((1.8, -2.4, 2.6), (-4.0, 0.2, 0.9)),         # Hoftor, Personalbuch, Kräutertisch
    "Blick_Werk_L2": ((2.4, 0.8, 2.7), (-3.4, -3.0, 0.8)),         # Holztür, Personalbuch, Fenster
    "Blick_Werk_R1": ((-1.0, -3.0, 2.7), (3.4, 2.4, 0.9)),         # schräg: Destille, Treppe, Tür zum Turm
    "Blick_Werk_R2": ((-1.2, -1.2, 2.6), (4.0, -0.9, 1.0)),        # Werkzeugwand, Tür zum Laden
    "Blick_Lager_0": ((3.3, -1.5, Z_LAGER + 2.1), (-3.2, -0.5, Z_LAGER + 0.7)),     # den First entlang: Ladeluke, Stapel, Kisten
    "Blick_Lager_L1": ((-3.2, -2.5, Z_LAGER + 2.0), (3.8, 0.6, Z_LAGER + 0.9)),     # zurück: Kräuterlager am Giebel, Fenster
    "Blick_Lager_R1": ((2.5, -0.25, Z_LAGER + 2.2), (3.6, 2.1, Z_LAGER - 0.6)),     # hinunter in die Bodenluke mit der Treppe
    "Blick_Laden_0": ((5.0, -3.1, 2.3), (7.2, 0.4, 1.0)),          # Tresen, Regale, Schild
    "Blick_Laden_L1": ((8.6, -3.0, 2.3), (4.8, -0.3, 1.0)),        # Regale, Tür in den Werkraum
    "Blick_Laden_R1": ((5.0, -0.3, 2.3), (8.8, -2.9, 1.0)),        # Tür zur Gasse, Fenster, Tresen
    "Blick_Turm_0": ((1.25, 4.5, 2.8), (1.25, 7.6, 1.15)),         # Athanor, Blasebalg, Alembik (der Arbeiter nicht links oben)
    "Blick_Turm_L1": ((2.8, 4.7, 2.7), (-0.4, 7.6, 1.6)),          # Tierkreis, Bücher, Blasebalg
    "Blick_Turm_R1": ((-0.2, 5.0, 2.7), (3.5, 6.8, 1.0)),          # Alembik und Vorlage, Fenster
}

Oe = si.Oe
WERK_OEFFNUNGEN = {
    "vorne": [Oe(0.4, 1.3, 0.9, 1.2), Oe(2.6, 1.3, 0.9, 1.2), Oe(HOLZTUER_X, 0.0, HOLZTUER_B, HOLZTUER_H)],
    "rechts": [Oe(LADENTUER_Y, 0.0, LADENTUER_B, LADENTUER_H)],
    "hinten": [Oe(-2.6, 1.3, 0.9, 1.2), Oe(TM.x, 0.0, TURMTUER_B, TURMTUER_H)],
    "links": [Oe(HOFTOR_Y, 0.0, HOFTOR_B, HOFTOR_H), Oe(2.4, 1.3, 0.9, 1.2)],
}
LADEN_OEFFNUNGEN = {
    "vorne": [Oe(GASSENTUER_X, 0.0, 1.0, 2.1), Oe(7.5, 0.85, 1.5, 1.2)],
    "rechts": [Oe(-1.6, 1.3, 0.8, 1.1)],
    "hinten": [],
    "links": [Oe(LADENTUER_Y, 0.0, LADENTUER_B, LADENTUER_H)],
}
SCHILDER = {"Tuer_Werk_Lager": "Treppe zum Kräuterlager", "Tuer_Lager_Werk": "Treppe hinunter zum Werkraum"}   # extras „schild“

ecken = ia.ecken


# ─────────────────────────────────────────────────────────────────────────────
#  Hilfen
# ─────────────────────────────────────────────────────────────────────────────

_lichter = [0]


def licht(punkt, richtung, art, stufe=1):
    """Licht_<n> (fortlaufend): „Fenster“ blickt in den Raum, „Lampe“ nach unten, „Glut“ ohne Richtung."""
    _lichter[0] += 1
    o = si.licht(f"Licht_{_lichter[0]}", punkt, richtung, art, stufe)
    if art == "Lampe":
        o.rotation_euler = (math.radians(-90), 0, 0)          # Blender +Y (Spiel −Z) zeigt nach unten
    return o


def drehkoerper(netz, st, mitte, profil, stoff, n=12, innen=False, dreh=0.0):
    """Rotationskörper um die Senkrechte durch `mitte` (Boden in mitte.z): profil = [(z, r)] in Reihenfolge, die Flächen zeigen
    nach außen (bei `innen` nach innen: Innenwand eines Gefäßes). Ein Abschnitt mit gleichem z ist ein Ring nach oben bzw. unten."""
    m = Vector((mitte[0], mitte[1], mitte[2] if len(mitte) > 2 else 0.0))
    m_ = st[stoff] if isinstance(stoff, str) else stoff
    ringe = [[m + Vector((r * math.cos(dreh + 2 * math.pi * k / n), r * math.sin(dreh + 2 * math.pi * k / n), z)) for k in range(n)]
             for z, r in profil]
    for i in range(len(profil) - 1):
        (z0, r0), (z1, r1) = profil[i], profil[i + 1]
        for k in range(n):
            j = (k + 1) % n
            quad = [ringe[i][k], ringe[i][j], ringe[i + 1][j], ringe[i + 1][k]]
            mitte_q = sum(quad, Vector()) / 4
            radial = Vector((mitte_q.x - m.x, mitte_q.y - m.y, 0)).normalized()
            normale = radial * (z1 - z0) + Vector((0, 0, -(r1 - r0)))
            if normale.length < 1e-9:
                continue
            netz.vieleck_nach(quad, -normale if innen else normale, m_)


def scheibe(netz, st, mitte, r, z, stoff, n=12, dreh=0.0, unten=False):
    """Waagrechte n-eckige Scheibe in Höhe z (nach oben, mit `unten` nach unten)."""
    netz.vieleck_nach([(mitte[0] + r * math.cos(dreh + 2 * math.pi * k / n), mitte[1] + r * math.sin(dreh + 2 * math.pi * k / n), z)
                       for k in range(n)], (0, 0, -1 if unten else 1), st[stoff] if isinstance(stoff, str) else stoff)


def am_ort(netz, ort):
    """Objekt aus dem Netz mit dem Ursprung bei `ort` (die Ecken liegen weiter in Weltkoordinaten): für Werkstücke, Blasen und
    Tropfen, die das Spiel bewegt, färbt oder ausblendet."""
    o = netz.objekt()
    o.data.transform(Matrix.Translation(-Vector(ort)))
    o.location = Vector(ort)
    return o


def vermauert(netz, st, a, b, o, rueck=0.05):
    """Die Türöffnung o in der Wand a → b (Raum links) ist mit Bruchstein zugemauert, etwas hinter der Innenseite: eine
    Mörtelfläche, davor Lagen einzelner Steine wie beim Durchbruch des Putzes (innenausbau.durchbruch)."""
    wand = ia.Wand(a, b, [o])
    u = wand.ops[0][0]
    u0, u1 = u - o.b / 2, u + o.b / 2
    netz.vieleck_nach([wand.p(u0, 0, -rueck), wand.p(u1, 0, -rueck), wand.p(u1, o.h, -rueck), wand.p(u0, o.h, -rueck)], wand.n,
                      st["Moertel"])
    zufall = random.Random(int(abs(u) * 100))
    j = 0.013
    v0 = 0.0
    while v0 < o.h - 0.05:
        v1 = min(v0 + zufall.uniform(0.2, 0.3), o.h)
        if o.h - v1 < 0.12:
            v1 = o.h
        x0 = u0
        while x0 < u1 - 0.05:
            x1 = min(x0 + zufall.uniform(0.24, 0.42), u1)
            if u1 - x1 < 0.12:
                x1 = u1
            ecken_ = [(x0 + j + zufall.uniform(0, 0.02), v0 + j + zufall.uniform(0, 0.015)),
                      ((x0 + x1) / 2 + zufall.uniform(-0.04, 0.04), v0 + j + zufall.uniform(-0.005, 0.01)),
                      (x1 - j - zufall.uniform(0, 0.02), v0 + j + zufall.uniform(0, 0.015)),
                      (x1 - j - zufall.uniform(0, 0.02), v1 - j - zufall.uniform(0, 0.015)),
                      ((x0 + x1) / 2 + zufall.uniform(-0.04, 0.04), v1 - j - zufall.uniform(-0.005, 0.01)),
                      (x0 + j + zufall.uniform(0, 0.02), v1 - j - zufall.uniform(0, 0.015))]
            netz.vieleck_nach([wand.p(x, y, -rueck + 0.005) for x, y in ecken_], wand.n, st[zufall.choice(ia.STEINE)])
            x0 = x1
        v0 = v1


def fenster_und_licht(netz, st, kanten, oeffnungen, anker, stufe=1, dicke=WAND):
    """Scheiben aller Fenster eines Raums (mit Gittern wie beim Schmied) und je ein Licht „Fenster“ (Blick −Z in den Raum)."""
    for seite, liste in oeffnungen.items():
        a, b = kanten[seite]
        d = (Vector((b[0], b[1], 0)) - Vector((a[0], a[1], 0))).normalized()
        n = Vector((-d.y, d.x, 0))
        for o in liste:
            if o.v < 0.05:
                continue
            si.fenster(netz, st, a, b, o, dicke)
            wand = ia.Wand(a, b, [o])
            p = wand.p(wand.ops[0][0], o.v + o.h / 2, 0.1)
            anker.append(licht(p, (n.x, n.y, 0), "Fenster", stufe))


def flasche(netz, st, fuss, h=0.26, r=0.06, stoff="Glaskolben", hals=None, n=6):
    """Flasche: bauchig mit Hals (h Höhe, r Halbmesser), Korken oben."""
    hals = hals or r * 0.4
    drehkoerper(netz, st, fuss, [(0.0, r * 0.8), (0.02, r), (h * 0.55, r), (h * 0.7, hals), (h, hals)], stoff, n=n)
    scheibe(netz, st, fuss, hals, fuss[2] + h if len(fuss) > 2 else h, "Holz alt", n=n)


def buendel(netz, st, oben, laenge=0.45, stoff="Heu", n=5):
    """Kräuterbündel kopfüber: oben die gebundenen Stiele (an Stange oder Leine), nach unten fächern Blätter und Blüten auf."""
    oben = Vector(oben)
    dreh = (oben.x * 7 + oben.y * 3) % 1.3
    drehkoerper(netz, st, oben, [(-laenge, 0.035), (-laenge * 0.6, 0.072), (-laenge * 0.22, 0.04), (0.0, 0.012)], stoff, n=n, dreh=dreh)
    netz.alle, alle = True, netz.alle
    scheibe(netz, st, oben, 0.035, oben.z - laenge, stoff, n=n, dreh=dreh, unten=True)
    netz.alle = alle


def offenes_fass(netz, st, boden, inhalt, r=0.3, h=0.8):
    """Offenes Fass mit Eisenreifen, darin 6 cm unter dem Rand ein Auszug oder Kräuter."""
    drehkoerper(netz, st, boden, [(0.0, r * 0.9), (h * 0.5, r), (h, r * 0.92), (h, r * 0.86)], "Holz", n=8, dreh=math.pi / 8)
    drehkoerper(netz, st, boden, [(h - 0.06, r * 0.86), (h, r * 0.86)], "Holz alt", n=8, innen=True, dreh=math.pi / 8)
    scheibe(netz, st, boden, r * 0.86, h - 0.06, inhalt, n=8, dreh=math.pi / 8)
    for z, f in ((0.12, 0.94), (h - 0.16, 0.97)):
        drehkoerper(netz, st, boden, [(z, r * f + 0.012), (z + 0.05, r * f + 0.012)], "Eisen", n=8, dreh=math.pi / 8)


def tiegel(netz, st, fuss, r=0.07, h=0.09, stoff="Tiegel", inhalt=None, n=8):
    """Irdener Salbentiegel, oben offen mit Inhalt oder mit Deckel."""
    z = fuss[2] if len(fuss) > 2 else 0.0
    drehkoerper(netz, st, fuss, [(0.0, r * 0.85), (h * 0.3, r), (h, r * 0.92)], stoff, n=n)
    scheibe(netz, st, fuss, r * 0.92, z + h - (0.012 if inhalt else 0.0), inhalt or "Tiegel dunkel", n=n)


# ─────────────────────────────────────────────────────────────────────────────
#  Werkraum: Kessel, Rauchfang, Kräutertisch, Destille
# ─────────────────────────────────────────────────────────────────────────────

def kessel(netz, st, anker):
    """Kupferkessel auf einem eisernen Dreifuß über offenem Feuer: Ring aus Steinen auf einer Herdplatte, Glut, Scheite. Innen
    ein dunkler Boden 5 cm unter der Brühe (ohne Werkstück ist der Kessel leer). Gibt die Brühe (Werkstueck_2) zurück."""
    k = KESSEL
    scheibe(netz, st, k, 0.66, 0.008, "Bruch dunkel", n=10)                                  # Herdplatte
    zufall = random.Random(4)
    for i in range(9):                                                                      # Steinring um das Feuer
        w = 2 * math.pi * i / 9 + zufall.uniform(-0.1, 0.1)
        g = zufall.uniform(0.15, 0.2)
        p = k + Vector((0.5 * math.cos(w), 0.5 * math.sin(w), 0))
        netz.quader((0, 0, 0.008 + g * 0.3), (g, g * 0.8, g * 0.6), st[("Bruch", "Bruch dunkel", "Bruch warm")[i % 3]],
                    Matrix.Translation(p) @ Matrix.Rotation(w, 4, "Z"))
    scheibe(netz, st, k, 0.36, 0.014, "Glut", n=10)                                          # Glut
    for i, w in enumerate((0.3, 1.4, 2.5)):                                                 # Scheite, über Kreuz
        a = k + Vector((0.38 * math.cos(w), 0.38 * math.sin(w), 0.05 + 0.02 * i))
        b = k + Vector((-0.2 * math.cos(w), -0.2 * math.sin(w), 0.09 + 0.02 * i))
        netz.stab(a, b, 0.09, 0.09, st["Holz"])
    for i in range(7):                                                                      # Kohlen
        w, r = zufall.uniform(0, 2 * math.pi), zufall.uniform(0.05, 0.28)
        p = k + Vector((r * math.cos(w), r * math.sin(w), 0.014))
        g = zufall.uniform(0.04, 0.07)
        fuss = [p + Vector((g * math.cos(w + 2.1 * j), g * math.sin(w + 2.1 * j), 0)) for j in range(3)]
        for j in range(3):
            netz.vieleck_nach([fuss[j], fuss[(j + 1) % 3], p + Vector((0, 0, g * 0.7))], (fuss[j] + fuss[(j + 1) % 3]) / 2 - p
                              + Vector((0, 0, 0.3)), st["Kohle"])
    anker.append(licht(k + Vector((0, 0, 0.25)), (0, 0, 1), "Glut"))
    # Dreifuß: drei Beine, ein Ring unter dem Bauch
    for i in range(3):
        w = math.radians(90 + 120 * i)
        netz.stab(k + Vector((0.6 * math.cos(w), 0.6 * math.sin(w), 0)), k + Vector((0.44 * math.cos(w), 0.44 * math.sin(w), 0.66)),
                  0.04, 0.04, st["Eisen"])
    for i in range(12):
        a, b = (k + Vector((0.45 * math.cos(2 * math.pi * j / 12), 0.45 * math.sin(2 * math.pi * j / 12), 0.64)) for j in (i, i + 1))
        netz.stab(a, b, 0.035, 0.03, st["Eisen"], quer=(0, 0, 1))
    # Kessel: bauchig, Wulst am Rand, Innenwand bis zum dunklen Boden
    drehkoerper(netz, st, k, [(0.48, 0.0), (0.5, 0.2), (0.57, 0.36), (0.68, 0.45), (0.84, 0.445), (KESSEL_RAND, KESSEL_R),
                              (KESSEL_RAND + 0.015, KESSEL_R + 0.02), (KESSEL_RAND + 0.03, KESSEL_R), (KESSEL_RAND + 0.03, KESSEL_R - 0.025)],
                "Kupferblech", n=14)
    drehkoerper(netz, st, k, [(BRUEHE - 0.06, KESSEL_R - 0.025), (KESSEL_RAND + 0.03, KESSEL_R - 0.025)], "Kupferblech", n=14,
                innen=True)
    scheibe(netz, st, k, KESSEL_R - 0.025, BRUEHE - 0.06, "Loch", n=14)
    for s in (-1, 1):                                                                       # Ösen und Henkel
        netz.quader(k + Vector((s * (KESSEL_R + 0.03), 0, KESSEL_RAND - 0.06)), (0.04, 0.07, 0.08), st["Eisen"])
    teile = 9
    for i in range(teile):
        w0, w1 = math.pi * i / teile, math.pi * (i + 1) / teile
        a = k + Vector((-(KESSEL_R + 0.03) * math.cos(w0), 0, KESSEL_RAND - 0.04 + 0.3 * math.sin(w0)))
        b = k + Vector((-(KESSEL_R + 0.03) * math.cos(w1), 0, KESSEL_RAND - 0.04 + 0.3 * math.sin(w1)))
        netz.stab(a, b, 0.022, 0.022, st["Eisen"])
    # Brühe: Werkstück 2 mit den Blasen
    bruehe = bs.Netz("Werkstueck_2")
    scheibe(bruehe, st, k, KESSEL_R - 0.026, BRUEHE, "Fluessigkeit", n=14)
    return am_ort(bruehe, k + Vector((0, 0, BRUEHE)))


def rauchfang(netz, st):
    """Rauchfang über dem Kessel an der Rückwand: unten breit mit Balken, oben schmal an der Wand, darüber der Schlot bis in die
    Decke (verrußter Lehm wie beim Schmied), an zwei Eisenstangen von der Decke gehalten."""
    M = si.rahmen(KESSEL.x, WY1, 180)                          # örtlich: +y von der Rückwand in den Raum
    z0, z1 = 2.05, 2.75
    bx, by, ox, oy = 0.95, WY1 - KESSEL.y + 0.6, 0.45, 0.55
    ul, ur, ol, or_ = (M @ Vector(p) for p in ((-bx, by, z0), (bx, by, z0), (-ox, oy, z1), (ox, oy, z1)))
    wl, wr, wol, wor = (M @ Vector(p) for p in ((-bx, 0, z0), (bx, 0, z0), (-ox, 0, z1), (ox, 0, z1)))
    netz.vieleck_nach([ul, ur, or_, ol], M.to_3x3() @ Vector((0, 1, 1.5)), st["Russ"])
    netz.vieleck_nach([wl, ul, ol, wol], M.to_3x3() @ Vector((-1, 0, 0.3)), st["Russ"])
    netz.vieleck_nach([ur, wr, wor, or_], M.to_3x3() @ Vector((1, 0, 0.3)), st["Russ"])
    alle, netz.alle = netz.alle, True
    netz.vieleck_nach([ul, ur, wr, wl], (0, 0, -1), st["Russ"])                               # Unterseite: Blick hinein
    si.q(netz, st, M, (0, by - 0.05, z0 - 0.06), (2 * bx + 0.1, 0.12, 0.14), "Fachwerk", ohne=("-y",))
    for s in (-1, 1):
        si.q(netz, st, M, (s * bx, (by - 0.11) / 2, z0 - 0.06), (0.12, by - 0.11, 0.14), "Fachwerk", ohne=("-y", "+y"))
    si.q(netz, st, M, (0, oy / 2, (z1 + H_WERK) / 2), (2 * ox, oy, H_WERK - z1), "Russ", ohne=("-y", "+z", "-z"))
    netz.alle = alle
    for s in (-1, 1):                                                                       # Halter an der Decke
        netz.stab(M @ Vector((s * (bx - 0.1), by - 0.06, z0 + 0.02)), M @ Vector((s * (bx - 0.25), by - 0.4, H_WERK)), 0.03, 0.03,
                  st["Eisen"])


def schlot_dachboden(netz, st):
    """Verputzter Schlot des Rauchfangs im Dachboden: an der hinteren Wand vom Boden durch die Dachschräge."""
    x0, x1, y0, y1 = KSCHLOT
    z0, z1 = H_WERK, DACH.dach_z(y0) + 0.15
    netz.vieleck_nach([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, -1, 0), st["Schlot"])
    for x, n in ((x0, -1), (x1, 1)):
        netz.vieleck_nach([(x, y0, z0), (x, y1, z0), (x, y1, DACH.dach_z(y1) + 0.15), (x, y0, z1)], (n, 0, 0), st["Schlot"])


def kraeutertisch(netz, st):
    """Kräutertisch: Platte auf Böcken, darauf der steinerne Mörser (vorn bei der Figur), ein Schneidbrett mit Messer, Häufchen
    frischer und getrockneter Kräuter, Schalen mit Pulver; unten ein Bord mit Körben. Gibt den Brei (Werkstueck_1) zurück."""
    holz = st["Holz"]
    tx, ty = W1.x, W1.y - 0.62                                                   # Mitte der Platte
    netz.quader((tx, ty, 0.75), (1.8, 0.75, 0.06), holz)
    for x in (tx - 0.8, tx + 0.8):
        for y in (ty - 0.3, ty + 0.3):
            netz.stab((x, y, 0), (x, y, 0.72), 0.07, 0.07, holz)
        netz.quader((x, ty, 0.2), (0.06, 0.62, 0.06), holz)
    netz.quader((tx, ty, 0.24), (1.66, 0.6, 0.03), st["Holz alt"])                          # Bord
    for i, x in enumerate((tx - 0.5, tx + 0.15)):                                           # Körbe mit Kräutern
        bs.achteck(netz, st, Vector((x, ty, 0.255)), 0.2, 0.0, 0.2, "Holz alt")
        bs.rund_oben(netz, st, (x, ty), 0.18, 0.458 + 0.004 * i, ("Kraut", "Heu")[i])
    # Mörser aus Sandstein auf der Platte, die Mulde mit dem Brei
    m = MOERSER
    oben = 0.78
    drehkoerper(netz, st, m + Vector((0, 0, oben)), [(0.0, 0.1), (0.02, 0.12), (0.12, 0.12), (0.14, 0.11), (0.14, 0.09)], "Sandstein",
                n=10)
    drehkoerper(netz, st, m + Vector((0, 0, oben)), [(0.085, 0.09), (0.14, 0.09)], "Sandstein", n=10, innen=True)
    scheibe(netz, st, m, 0.09, oben + 0.085, "Loch", n=10)
    # Schneidbrett, Messer, Kräuterhäufchen, Schalen
    netz.quader((tx + 0.45, ty - 0.05, 0.79), (0.42, 0.28, 0.025), st["Schnittholz"])
    netz.stab((tx + 0.33, ty - 0.08, 0.81), (tx + 0.55, ty - 0.02, 0.81), 0.03, 0.006, st["Eisen"], quer=(0, 0, 1))
    netz.stab((tx + 0.55, ty - 0.02, 0.81), (tx + 0.66, ty + 0.01, 0.815), 0.025, 0.02, st["Holz alt"])
    zufall = random.Random(8)
    for i, (dx, dy, stoff) in enumerate(((-0.62, -0.1, "Kraut"), (-0.35, -0.18, "Heu"), (0.5, 0.2, "Kraut dunkel"), (-0.7, 0.15, "Heu"))):
        c = Vector((tx + dx, ty + dy, 0.78))
        rand = [c + Vector((0.13 * math.cos(2 * math.pi * k / 7) * zufall.uniform(0.8, 1.1), 0.1 * math.sin(2 * math.pi * k / 7), 0.002))
                for k in range(7)]
        spitze = c + Vector((0, 0, 0.06))
        for k in range(7):
            netz.vieleck_nach([rand[k], rand[(k + 1) % 7], spitze], (rand[k] + rand[(k + 1) % 7]) / 2 - c + Vector((0, 0, 0.1)), st[stoff])
    for i, (dx, dy, inhalt) in enumerate(((0.2, 0.22, "Farbe"), (0.62, -0.22, "Rose"), (-0.15, 0.25, "Steinstaub"))):
        tiegel(netz, st, (tx + dx, ty + dy, 0.78), r=0.075, h=0.06, inhalt=inhalt)
    brei = bs.Netz("Werkstueck_1")
    scheibe(brei, st, m, 0.088, oben + 0.097, "Fluessigkeit", n=10)
    return am_ort(brei, m + Vector((0, 0, oben + 0.097)))


def kraeuter_ueber_tisch(netz, st):
    """Eine Stange über dem Kräutertisch, an Seilen von der Decke, daran Kräuterbündel kopfüber."""
    x0, x1, y = W1.x - 0.95, W1.x + 0.95, W1.y - 0.75
    z = 2.45
    netz.stab((x0, y, z), (x1, y, z), 0.06, 0.06, st["Holz"])
    for x in (x0 + 0.1, x1 - 0.1):
        netz.stab((x, y, z), (x, y, H_WERK), 0.02, 0.02, st["Seil"])
    zufall = random.Random(5)
    for k in range(8):
        x = x0 + 0.15 + (x1 - x0 - 0.3) * k / 7
        buendel(netz, st, (x, y + zufall.uniform(-0.02, 0.02), z - 0.03), laenge=zufall.uniform(0.32, 0.5),
                          stoff=("Heu", "Kraut", "Heu", "Rose")[k % 4])


def ofen_mit_tuer(netz, st, mitte, breite, tiefe, hoehe, tuer_u, tuer, stoff="Ziegelmauer", vorn=(-1, 0)):
    """Gemauerter Ofen (Quader, `mitte` unten in der Mitte) mit einer Feuertür auf der Seite `vorn` (Richtung in x, y):
    Öffnung (u = seitlich, z0, z1, b) mit Laibung, dahinter die Glut, eiserner Rahmen. Gibt (Punkt vor der Glut, Normale der Vorderseite, örtliches System)
    zurück."""
    d = Vector((vorn[0], vorn[1], 0))
    q = d.cross(Vector((0, 0, 1)))                                # entlang der Vorderseite (rechtshändig: x, vorn, oben)
    M = Matrix((q, d, Vector((0, 0, 1)))).transposed().to_4x4()
    M = Matrix.Translation(Vector(mitte)) @ M                     # örtlich: x entlang der Vorderseite, y nach vorn
    m = st[stoff]
    hb, ht = breite / 2, tiefe / 2
    u0, u1 = tuer_u - tuer[2] / 2, tuer_u + tuer[2] / 2
    z0, z1 = tuer[0], tuer[1]

    def P(x, y, z):
        return M @ Vector((x, y, z))
    vorn_n = M.to_3x3() @ Vector((0, 1, 0))
    for a, b, c, e in (((-hb, 0), (u0, 0), (u0, hoehe), (-hb, hoehe)), ((u1, 0), (hb, 0), (hb, hoehe), (u1, hoehe)),
                       ((u0, 0), (u1, 0), (u1, z0), (u0, z0)), ((u0, z1), (u1, z1), (u1, hoehe), (u0, hoehe))):
        netz.vieleck_nach([P(a[0], ht, a[1]), P(b[0], ht, b[1]), P(c[0], ht, c[1]), P(e[0], ht, e[1])], vorn_n, m)
    for x, s in ((-hb, -1), (hb, 1)):
        netz.vieleck_nach([P(x, -ht, 0), P(x, ht, 0), P(x, ht, hoehe), P(x, -ht, hoehe)], M.to_3x3() @ Vector((s, 0, 0)), m)
    netz.vieleck_nach([P(-hb, -ht, 0), P(hb, -ht, 0), P(hb, -ht, hoehe), P(-hb, -ht, hoehe)], -vorn_n, m)
    netz.vieleck_nach([P(-hb, -ht, hoehe), P(hb, -ht, hoehe), P(hb, ht, hoehe), P(-hb, ht, hoehe)], (0, 0, 1), m)
    t = 0.08                                                      # Laibung der Tür, dahinter die Glut
    for a, b, n in (((u0, z0), (u0, z1), (1, 0, 0)), ((u1, z0), (u1, z1), (-1, 0, 0))):
        netz.vieleck_nach([P(a[0], ht, a[1]), P(b[0], ht, b[1]), P(b[0], ht - t, b[1]), P(a[0], ht - t, a[1])], M.to_3x3() @ Vector(n), m)
    netz.vieleck_nach([P(u0, ht, z0), P(u1, ht, z0), P(u1, ht - t, z0), P(u0, ht - t, z0)], (0, 0, 1), m)
    alle, netz.alle = netz.alle, True
    netz.vieleck_nach([P(u0, ht, z1), P(u1, ht, z1), P(u1, ht - t, z1), P(u0, ht - t, z1)], (0, 0, -1), m)
    netz.alle = alle
    netz.vieleck_nach([P(u0, ht - t, z0), P(u1, ht - t, z0), P(u1, ht - t, z1), P(u0, ht - t, z1)], vorn_n, st["Glut"])
    e = st["Eisen"]
    for x0, x1, y0_, y1_ in ((u0 - 0.03, u1 + 0.03, z1, z1 + 0.03), (u0 - 0.03, u1 + 0.03, z0 - 0.03, z0), (u0 - 0.03, u0, z0, z1),
                             (u1, u1 + 0.03, z0, z1)):
        netz.vieleck_nach([P(x0, ht + 0.004, y0_), P(x1, ht + 0.004, y0_), P(x1, ht + 0.004, y1_), P(x0, ht + 0.004, y1_)], vorn_n, e)
    return P(tuer_u, ht - t + 0.1, (z0 + z1) / 2), vorn_n, M


def destille(netz, st, k):
    """Destille (ab Stufe 2) für Stufe k: Ofen aus Ziegeln mit Feuertür und Schieber zur Figur (Platz 3), darauf der
    Brennkolben im Eisenkranz, der Tonhelm, das Kühlrohr mit Kühlmantel hinunter zur Vorlage im Trichter auf einem Schemel.
    Der Auszug im Kolben ist Werkstueck_3 (eigener Knoten)."""
    vor_o, hoehe_o = fg.ARBEITSHOEHEN["Ofen"]
    tuer_u = (W3.y - 0.12) - OFEN.y                               # Feuertür 12 cm rechts der Figur (örtlich x = Welt +y)
    glut, n, M = ofen_mit_tuer(netz, st, OFEN, 0.6, 0.62, 0.75, tuer_u, (0.13, 0.43, 0.26), vorn=(-1, 0))
    e = st["Eisen"]
    netz.quader((tuer_u, 0.31 + 0.02, hoehe_o), (0.24, 0.04, 0.06), e, M)                  # Schieber
    netz.quader((tuer_u + 0.1, 0.31 + 0.05, hoehe_o), (0.03, 0.04, 0.04), st["Holz alt"], M)  # Griff
    for i in range(10):                                                                     # Kranz für den Kolben
        a, b = (OFEN + Vector((0.2 * math.cos(2 * math.pi * j / 10), 0.2 * math.sin(2 * math.pi * j / 10), 0.765)) for j in (i, i + 1))
        netz.stab(a, b, 0.03, 0.03, e, quer=(0, 0, 1))
    # Brennkolben: unten der Auszug (Werkstueck_3), oben Glas, langer Hals
    drehkoerper(netz, st, OFEN, [(0.88, 0.205), (0.95, 0.17), (1.0, 0.11), (1.04, 0.05), (1.22, 0.045)], "Glaskolben", n=12)
    # Tonhelm auf dem Hals, mit Schnabel
    drehkoerper(netz, st, OFEN, [(1.2, 0.055), (1.23, 0.125), (1.35, 0.13), (1.43, 0.1), (1.49, 0.05), (1.51, 0.0)], "Tiegel dunkel",
                n=12)
    kopf = OFEN + Vector((0, 0, 1.34))
    zum = (VORLAGE - OFEN)
    zum.z = 0
    zum.normalize()
    anfang = kopf + zum * 0.12
    ende = Vector((VORLAGE.x, VORLAGE.y, 1.2))
    netz.stab(anfang - zum * 0.04, ende, 0.035, 0.035, st["Kupferblech"], quer=(0, 0, 1))
    mitte = anfang.lerp(ende, 0.45)
    netz.stab(mitte - (ende - anfang).normalized() * 0.12, mitte + (ende - anfang).normalized() * 0.12, 0.1, 0.1, st["Kupferblech"],
              quer=(0, 0, 1))                                                                # Kühlmantel
    netz.stab(ende, ende + Vector((0, 0, -0.04)), 0.02, 0.02, st["Kupferblech"])            # Tülle
    # Schemel mit der Vorlage im Trichter
    v = VORLAGE
    scheibe(netz, st, v, 0.18, 0.5, "Holz", n=8)
    for i in range(3):
        w = math.radians(30 + 120 * i)
        netz.stab(v + Vector((0.16 * math.cos(w), 0.16 * math.sin(w), 0)), v + Vector((0.11 * math.cos(w), 0.11 * math.sin(w), 0.49)),
                  0.04, 0.04, st["Holz"])
    drehkoerper(netz, st, v + Vector((0, 0, 0.5)), [(0.0, 0.06), (0.025, 0.12), (0.1, 0.14), (0.19, 0.11), (0.25, 0.04), (0.47, 0.03),
                                                    (0.47, 0.03), (0.53, 0.07), (0.53, 0.06)], "Glaskolben", n=10)
    drehkoerper(netz, st, v + Vector((0, 0, 0.5)), [(0.48, 0.03), (0.53, 0.06)], "Glaskolben", n=10, innen=True)
    scheibe(netz, st, v, 0.03, 0.98, "Loch", n=10)
    return glut, n


def destille_werkstueck(st):
    """Werkstueck_3: der Auszug im Brennkolben (unten bauchig, in der Farbe der Ware), mit den Kindern „Destillat_3“ (Band im
    Bauch der Vorlage) und „Tropfen“ (an der Tülle, fällt in den Trichter)."""
    netz = bs.Netz("Werkstueck_3")
    drehkoerper(netz, st, OFEN, [(0.765, 0.17), (0.8, 0.2), (0.88, 0.205)], "Fluessigkeit", n=12)
    o = am_ort(netz, OFEN + Vector((0, 0, 0.84)))
    destillat = bs.Netz("Destillat_3")
    drehkoerper(destillat, st, VORLAGE + Vector((0, 0, 0.5)), [(0.004, 0.121), (0.06, 0.141), (0.1, 0.1415)], "Fluessigkeit", n=10)
    kind = am_ort(destillat, VORLAGE + Vector((0, 0, 0.56)))
    tropfen = bs.Netz("Tropfen", alle=True)
    t0 = Vector((VORLAGE.x, VORLAGE.y, 1.15))
    drehkoerper(tropfen, st, t0, [(-0.016, 0.0), (-0.008, 0.012), (0.004, 0.009), (0.014, 0.0)], "Fluessigkeit", n=6)
    o_tropfen = am_ort(tropfen, t0)
    for k in (kind, o_tropfen):
        k.parent = o
        k.matrix_parent_inverse = Matrix.Identity(4)
        k.location -= o.location
    return o, o_tropfen


def blasen(st, bruehe):
    """Blase_1–7 auf der Brühe (Kinder von Werkstueck_2): kleine Kuppeln, die wachsen und platzen. Gibt [(Objekt, Bewegung)]."""
    zufall = random.Random(21)
    bewegt = []
    dauer = ARBEIT_BILDER / FPS
    for i in range(7):
        w, r = zufall.uniform(0, 2 * math.pi), math.sqrt(zufall.uniform(0.05, 1.0)) * 0.3
        groesse = zufall.uniform(0.022, 0.042)
        netz = bs.Netz(f"Blase_{i + 1}")
        mitte = KESSEL + Vector((r * math.cos(w), r * math.sin(w), BRUEHE))
        drehkoerper(netz, st, mitte, [(0.0, groesse), (groesse * 0.5, groesse * 0.86), (groesse * 0.85, groesse * 0.5), (groesse, 0.0)],
                    "Fluessigkeit", n=6)
        o = am_ort(netz, mitte)
        o.parent = bruehe
        o.matrix_parent_inverse = Matrix.Identity(4)
        o.location -= bruehe.location
        o.scale = (0.01, 0.01, 0.01)                                # Ruhelage: keine Blase
        # zwei Auftritte je Schleife, keiner über die Grenze der Schleife (bei Zeit 0 ist keine Blase zu sehen)
        auftritte = []
        for j in range(2):
            anfang = 0.15 + (i * 0.27 + j * 1.9 + zufall.uniform(0, 0.3)) % (dauer - 1.1)
            auftritte.append((anfang, zufall.uniform(0.45, 0.8)))

        def bewegung(bild, auftritte=auftritte):
            t = bild / FPS
            s = 0.01
            for anfang, wachsen in auftritte:
                a = t - anfang
                if 0 <= a < wachsen:
                    s = 0.2 + 0.8 * fg.weich(a / wachsen)
                elif wachsen <= a < wachsen + 0.08:
                    s = 1.0 + 0.4 * (a - wachsen) / 0.08
            return None, None, (s, s, s * (0.8 if s > 1 else 1.0))
        bewegt.append((o, bewegung))
    return bewegt


def tropfen_bewegung(o):
    """Der Tropfen hängt an der Tülle und wächst (1,4 s), fällt in den Trichter (0,2 s) und ist weg; zweimal je Schleife."""
    ruhe = o.location.copy()
    fallen = 1.15 - 0.99                         # bis in den Trichter (Rand 1,03 m)

    def bewegung(bild):
        t = bild / FPS
        for anfang in (0.25, 2.25):
            a = t - anfang
            if 0 <= a < 1.4:
                s = 0.25 + 0.75 * fg.weich(a / 1.4)
                return ruhe, None, (s, s, s * (1 + 0.25 * fg.weich(a / 1.4)))
            if 1.4 <= a < 1.6:
                f = (a - 1.4) / 0.2
                return ruhe - Vector((0, 0, fallen * f * f)), None, (0.9, 0.9, 1.35)
        return ruhe, None, (0.01, 0.01, 0.01)
    o.scale = (0.01, 0.01, 0.01)
    return [(o, bewegung)]


def destillierhelm(netz, st):
    """Gekauft: kupferner Destillierhelm an einer Kette unter dem Rauchfang, hochgezogen und neben den Kessel geschwenkt (so
    kann gerührt werden, und er verdeckt den Rührenden nicht); Schnabel mit Kühlrohr zu einem Steinkrug neben dem Kessel. In
    jeder Stufe gleich."""
    k = KESSEL + Vector((-0.62, -0.1, 1.45))                         # Unterkante des Helms
    drehkoerper(netz, st, k, [(0.0, 0.3), (0.03, 0.34), (0.06, 0.3), (0.2, 0.3), (0.32, 0.22), (0.4, 0.1), (0.42, 0.0)], "Kupferblech",
                n=14)
    alle, netz.alle = netz.alle, True
    scheibe(netz, st, k, 0.3, 0.0 + k.z, "Kupferblech", n=14, unten=True)
    netz.alle = alle
    netz.stab(k + Vector((0, 0, 0.42)), Vector((k.x, k.y, 2.3)), 0.02, 0.02, st["Eisen"])   # Kette zum Rauchfang
    kopf = k + Vector((0, 0, 0.16))
    ende = KESSEL + Vector((-1.25, -0.55, 0.55))
    anfang = kopf + (Vector((ende.x, ende.y, kopf.z)) - kopf).normalized() * 0.32
    netz.stab(anfang, ende + Vector((0, 0, 0.12)), 0.04, 0.04, st["Kupferblech"], quer=(0, 0, 1))
    krug = Vector((ende.x, ende.y, 0))
    drehkoerper(netz, st, krug, [(0.0, 0.12), (0.06, 0.15), (0.32, 0.15), (0.42, 0.1), (0.5, 0.07)], "Steingut", n=10)
    scheibe(netz, st, krug, 0.07, 0.5, "Loch", n=10)
    netz.stab(krug + Vector((0.14, 0, 0.42)), krug + Vector((0.2, 0, 0.18)), 0.03, 0.03, st["Steingut"])                 # Henkel


def werkbank(netz, st, M):
    """Arbeitstisch unter der Werkzeugwand (örtlich, +y in den Raum): Platte, Beine, ein Bord, ein paar Tiegel und Flaschen."""
    holz = st["Holz"]
    si.q(netz, st, M, (0, 0.34, 0.82), (1.7, 0.66, 0.06), "Holz")
    for x in (-0.78, 0.78):
        for y in (0.06, 0.62):
            netz.stab(M @ Vector((x, y, 0)), M @ Vector((x, y, 0.79)), 0.07, 0.07, holz)
    si.q(netz, st, M, (0, 0.34, 0.22), (1.56, 0.58, 0.03), "Holz alt")
    for i, x in enumerate((-0.55, -0.25, 0.3)):
        tiegel(netz, st, M @ Vector((x, 0.3 + 0.05 * i, 0.235)), r=0.08, h=0.12, stoff=("Tiegel", "Steingut", "Tiegel")[i])
    netz.quader(M @ Vector((-0.65, 0.4, 0.86)), (0.22, 0.3, 0.02), st["Pergament"])          # Rezept


def werkzeug1(netz, st, M):
    """Gekauft: Messingmörser mit Stößel auf einem Wandbord, Spatel und Löffel an den Haken (linke Hälfte)."""
    si.q(netz, st, M, (-0.4, 0.13, 1.31), (0.8, 0.22, 0.03), "Holz")
    for x in (-0.7, -0.1):
        si.q(netz, st, M, (x, 0.06, 1.24), (0.04, 0.1, 0.12), "Holz")
    m = M @ Vector((-0.55, 0.13, 1.325))
    drehkoerper(netz, st, m, [(0.0, 0.07), (0.02, 0.085), (0.1, 0.08), (0.12, 0.085), (0.12, 0.06)], "Kupferblech", n=10)
    scheibe(netz, st, m, 0.06, m.z + 0.1, "Loch", n=10)
    netz.stab(m + Vector((0, 0, 0.06)), m + M.to_3x3() @ Vector((0.05, 0.04, 0.26)), 0.03, 0.03, st["Kupferblech"])
    for k, (x, z) in enumerate(si.HAKEN):
        if x < 0 and z > 2:
            laenge = 0.26 + 0.04 * (k % 2)
            netz.stab(M @ Vector((x, 0.1, z + 0.02)), M @ Vector((x, 0.1, z - laenge + 0.06)), 0.018, 0.012, st["Holz alt"])
            netz.stab(M @ Vector((x, 0.1, z - laenge + 0.06)), M @ Vector((x, 0.1, z - laenge)), 0.045 if k % 2 else 0.03, 0.01,
                      st["Eisen"])
        elif x < 0 and k % 2 == 0:
            netz.stab(M @ Vector((x, 0.1, z + 0.02)), M @ Vector((x, 0.1, z - 0.28)), 0.02, 0.012, st["Holz"])
            bs.achteck(netz, st, M @ Vector((x, 0.1, z - 0.36)), 0.045, 0.0, 0.08, "Kupferblech")


def werkzeug2(netz, st, M):
    """Gekauft (ab Stufe 2): feine Waage, Retorte, Kolben und Phiolen auf dem Arbeitstisch, ein Sieb am Haken (rechte Hälfte)."""
    e = st["Eisen"]
    w = M @ Vector((0.45, 0.3, 0.85))
    bs.achteck(netz, st, w, 0.07, 0.0, 0.02, "Kupferblech")
    netz.stab(w, w + Vector((0, 0, 0.36)), 0.016, 0.016, e)
    quer = M.to_3x3() @ Vector((1, 0, 0))
    netz.stab(w + Vector((0, 0, 0.34)) - quer * 0.2, w + Vector((0, 0, 0.34)) + quer * 0.2, 0.012, 0.014, e)
    for s in (-1, 1):
        a = w + Vector((0, 0, 0.34)) + quer * (0.19 * s)
        netz.stab(a, a - Vector((0, 0, 0.2)), 0.006, 0.006, e)
        bs.achteck(netz, st, a - Vector((0, 0, 0.215)), 0.065, 0.0, 0.015, "Kupferblech")
    r = M @ Vector((0.0, 0.32, 0.85))                                                       # Retorte: Bauch und schräger Hals
    drehkoerper(netz, st, r, [(0.0, 0.05), (0.04, 0.1), (0.1, 0.1), (0.15, 0.06), (0.17, 0.03)], "Glaskolben", n=10)
    netz.stab(r + Vector((0, 0, 0.16)), r + Vector((0, 0, 0.16)) - quer * 0.28 + Vector((0, 0, -0.1)), 0.025, 0.025, st["Glaskolben"])
    for i, x in enumerate((0.15, 0.22, 0.75)):
        flasche(netz, st, M @ Vector((x, 0.5 - 0.06 * i, 0.85)), h=0.2 - 0.03 * i, r=0.035 + 0.01 * i)
    x, z = si.HAKEN[6]
    sieb = M @ Vector((x + 0.2, 0.1, 1.95))
    netz.stab(M @ Vector((x + 0.2, 0.1, 2.22)), sieb + Vector((0, 0, 0.1)), 0.015, 0.015, e)
    bs.achteck(netz, st, sieb - Vector((0, 0, 0.0)), 0.11, 0.0, 0.06, "Holz alt")


def handschuhe(netz, st):
    """Gekauft: zwei Paar Lederhandschuhe an Haken neben dem Rauchfang (Rückwand)."""
    M = si.rahmen(-1.75, WY1, 180)
    si.q(netz, st, M, (0, 0.03, 1.85), (0.62, 0.06, 0.07), "Fachwerk", ohne=("-y",))
    for k, x in enumerate((-0.16, 0.16)):
        netz.stab(M @ Vector((x, 0.03, 1.8)), M @ Vector((x, 0.12, 1.82)), 0.02, 0.02, st["Eisen"])
        for j, dx in enumerate((-0.05, 0.05)):
            unten = 1.36 - 0.04 * ((k + j) % 2)
            ecken_ = [(x + dx - 0.05, 0.1, 1.78), (x + dx + 0.05, 0.1, 1.78), (x + dx + 0.07, 0.09 + 0.02 * j, unten),
                      (x + dx - 0.06, 0.09 + 0.02 * j, unten)]
            netz.vieleck_nach([M @ Vector(p) for p in ecken_], M.to_3x3() @ Vector((0, 1, 0)), st["Leder"])
            netz.vieleck_nach([M @ (Vector(p) - Vector((0, 0.01, 0))) for p in ecken_], M.to_3x3() @ Vector((0, -1, 0)), st["Leder"])


# ─────────────────────────────────────────────────────────────────────────────
#  Kräuterlager unter dem Dach
# ─────────────────────────────────────────────────────────────────────────────

KRAEUTER_Y = (-1.85, 0.95)                         # Kräuterlager am rechten Giebel (vor der Bodenluke)


def kraeuterlager(netz, st, z):
    """Kräuterlager am rechten Giebel: zwei Pfosten, drei Stangen voller Kräuterbündel, darunter Körbe und Säcke."""
    x = WX1 - 0.35
    y0, y1 = KRAEUTER_Y
    for y in (y0 + 0.1, y1 - 0.1):
        netz.quader((x, y, z + 1.2), (0.1, 0.1, 2.4), st["Holz"])
        netz.quader((x, y, z + 0.05), (0.5, 0.12, 0.1), st["Holz alt"])
    zufall = random.Random(31)
    for i, h in enumerate((1.05, 1.65, 2.25)):
        netz.stab((x, y0, z + h), (x, y1, z + h), 0.05, 0.05, st["Holz"])
        for k in range(11):
            y = y0 + 0.2 + (y1 - y0 - 0.4) * k / 10
            buendel(netz, st, (x + zufall.uniform(-0.03, 0.03), y, z + h - 0.03), laenge=zufall.uniform(0.3, 0.5),
                              stoff=("Heu", "Kraut", "Heu", "Rose", "Kraut dunkel")[(k + i) % 5])
    for i, y in enumerate((y0 + 0.55, (y0 + y1) / 2, y1 - 0.5)):
        b = Vector((x - 0.25, y, z))
        if i == 1:
            bs.achteck(netz, st, b, 0.2, 0.0, 0.5, "Leinen", n=6)
            bs.achteck(netz, st, b + Vector((0, 0, 0.5)), 0.1, 0.0, 0.08, "Seil", n=6)
        else:
            bs.achteck(netz, st, b, 0.24, 0.0, 0.3, "Holz alt")
            bs.rund_oben(netz, st, b, 0.22, z + 0.305, ("Heu", "Kraut")[i // 2])


def gespiegelt(quelle, ziel, xm=0.0):
    """Überträgt die Flächen des Netzes `quelle` an der Ebene x = xm gespiegelt in `ziel` (Umlaufsinn umgedreht, damit die
    Flächen wieder nach außen zeigen)."""
    for f, i in zip(quelle.flaechen, quelle.fstoff):
        ziel.vieleck([Vector((2 * xm - quelle.ecken[k].x, quelle.ecken[k].y, quelle.ecken[k].z)) for k in reversed(f)],
                     quelle.stoffe[i], immer=True)


def ladeluke_links(lager, kran, himmel, st, luke):
    """Ladeluke mit Flügeln, Kranbalken und Himmel im linken Giebel: innenausbau.Dachboden.ladeluke baut sie im rechten Giebel,
    hier wird sie an der Mitte des Raums (x = 0) gespiegelt. Gibt den Punkt für das Licht zurück."""
    teile = [bs.Netz("_Luke", alle=True), bs.Netz("_Kran", alle=True), bs.Netz("_Himmel")]
    punkt = DACH.ladeluke(*teile, st, luke.s, luke.b, luke.h, WAND)
    for quelle, ziel in zip(teile, (lager, kran, himmel)):
        gespiegelt(quelle, ziel)
    return Vector((-punkt.x, punkt.y, punkt.z))


def darrhorden(netz, st, x0, x1):
    """Gekauft: Kräuterregale (Darrhorden) am vorderen Kniestock, niedrig unter der Dachschräge: ein Gestell mit vier flachen
    Horden voller Kräuter zum Trocknen."""
    z, y = Z_LAGER, WY0 + 0.32
    for x in (x0, (x0 + x1) / 2, x1):
        netz.quader((x, y, z + 0.6), (0.08, 0.08, 1.2), st["Holz"])
        netz.quader((x, y + 0.04, z + 0.05), (0.1, 0.46, 0.1), st["Holz alt"])
    zufall = random.Random(12)
    for k, h in enumerate((0.25, 0.52, 0.79, 1.06)):
        netz.quader(((x0 + x1) / 2, y, z + h), (x1 - x0 + 0.1, 0.48, 0.05), st["Holz alt"], ohne=("+z",))
        netz.quader(((x0 + x1) / 2, y, z + h + 0.025), (x1 - x0 + 0.1, 0.48, 0.0), st["Holz alt"], ohne=("-z", "+x", "-x", "+y", "-y"))
        x = x0 + 0.08
        while True:
            b = zufall.uniform(0.25, 0.45)
            if x + b > x1 - 0.03:
                break
            netz.quader((x + b / 2, y, z + h + 0.04), (b, 0.38, 0.03), st[zufall.choice(("Heu", "Kraut", "Kraut dunkel", "Rose"))],
                        ohne=("-z",))
            x += b + 0.05


def leinen_mit_kraeutern(netz, st, z):
    """Zwei Leinen den First entlang, daran Kräuterbündel hoch über den Stellplätzen."""
    zufall = random.Random(17)
    h = z + DACH.kehle - 0.12                     # knapp unter den Kehlbalken, über Kopfhöhe der Kamera
    for y in (-0.75, 0.95):
        netz.stab((WX0 + 1.4, y, h), (WX1 - 1.4, y, h), 0.015, 0.015, st["Seil"])
        x = WX0 + 1.7
        while x < WX1 - 1.6:
            buendel(netz, st, (x, y, h - 0.01), laenge=zufall.uniform(0.26, 0.38),
                    stoff=("Heu", "Kraut", "Rose", "Heu", "Kraut dunkel")[int(x * 7) % 5])
            x += zufall.uniform(0.32, 0.5)


def faesser(netz, st, z):
    """Drei Fässer mit Auszügen am hinteren Kniestock und ein liegendes."""
    for i, (x, inhalt) in enumerate(((1.8, "Kraut"), (2.5, "Farbe"), (3.2, "Fluessigkeit"))):
        b = Vector((x, WY1 - 0.4, z))
        bs.fass(netz, st, b, r=0.3, h=0.8, reifen=(0.12, 0.62))
        bs.rund_oben(netz, st, b, 0.27, z + 0.801, "Holz alt")
    netz.stab((0.9, WY1 - 0.45, z + 0.3), (1.4, WY1 - 0.45, z + 0.3), 0.55, 0.55, st["Holz"])  # liegendes Fass (vereinfacht)


# ─────────────────────────────────────────────────────────────────────────────
#  Laden
# ─────────────────────────────────────────────────────────────────────────────

REGAL_X = [4.85 + 0.95 * k for k in range(5)]              # Ständer der Regale an der Rückwand des Ladens
REGAL_Y = LY1 - 0.2                                        # Mitte der Bretter (0,38 tief)
BRETTER = (0.35, 0.8, 1.22, 1.64, 2.06)                    # Oberkanten der Bretter
AUSLAGE_BRETTER = (0.8, 1.22)                              # darauf die Stellplätze Auslage_1–8


def regale(netz, st):
    """Regale an der Rückwand des Ladens: fünf Ständer, fünf Bretter, oben ein Kranz. Auf den beiden Auslagebrettern bleibt Platz
    für die Ware (das Spiel stellt sie auf), auf den übrigen stehen Tiegel, Flaschen, Kästchen und Gläser."""
    holz = st["Holz"]
    x0, x1 = REGAL_X[0], REGAL_X[-1]
    for x in REGAL_X:
        netz.quader((x, REGAL_Y, 1.2), (0.05, 0.4, 2.4), holz, ohne=("+y",))
    for z in BRETTER:
        netz.quader(((x0 + x1) / 2, REGAL_Y, z - 0.015), (x1 - x0, 0.38, 0.03), holz, ohne=("+y",))
    netz.quader(((x0 + x1) / 2, REGAL_Y + 0.02, 2.42), (x1 - x0 + 0.1, 0.44, 0.06), st["Fachwerk"], ohne=("+y",))
    zufall = random.Random(9)
    for z in BRETTER:
        if z in AUSLAGE_BRETTER:
            continue
        for a, b in zip(REGAL_X, REGAL_X[1:]):
            x = a + 0.08
            while x < b - 0.12:
                art = zufall.choice(("tiegel", "flasche", "flasche", "kasten", "glas"))
                y = REGAL_Y + zufall.uniform(-0.06, 0.04)
                if art == "tiegel":
                    tiegel(netz, st, (x + 0.06, y, z), r=0.06, h=0.08, inhalt=None if zufall.random() < 0.5 else "Tiegel dunkel")
                    x += 0.15
                elif art == "flasche":
                    flasche(netz, st, (x + 0.05, y, z), h=zufall.uniform(0.18, 0.3), r=zufall.uniform(0.035, 0.055),
                            stoff=zufall.choice(("Glaskolben", "Glaskolben", "Steingut")))
                    x += 0.12
                elif art == "kasten":
                    netz.quader((x + 0.1, y, z + 0.06), (0.18, 0.2, 0.12), st[zufall.choice(("Holz alt", "Fachwerk"))])
                    x += 0.23
                else:
                    drehkoerper(netz, st, (x + 0.07, y, z), [(0.0, 0.06), (0.16, 0.06), (0.16, 0.0)], "Glaskolben", n=8)
                    x += 0.16
    for k, x in enumerate((REGAL_X[0] + 0.3, REGAL_X[2] + 0.5, REGAL_X[3] + 0.2)):           # Bündel am Kranz
        buendel(netz, st, (x, REGAL_Y - 0.21, 2.39), laenge=0.4, stoff=("Heu", "Kraut", "Rose")[k])


def auslage_anker():
    """Auslage_1–8: je Fach eines Auslagebretts ein Stellplatz (Mitte der Brettoberkante, Blick in den Laden, −y)."""
    liste = []
    nummer = 1
    for z in reversed(AUSLAGE_BRETTER):
        for a, b in zip(REGAL_X, REGAL_X[1:]):
            liste.append(bs.anker(f"Auslage_{nummer}", ((a + b) / 2, REGAL_Y, z), 180))
            nummer += 1
    return liste


def tresen(netz, st):
    """Tresen quer im Laden: Kasten mit Füllungen, Platte, darauf eine Waage, ein Rechnungsbuch, Tiegel und ein Messingmörser;
    dahinter ein Schemel."""
    x0, x1, y0, y1 = 5.5, 8.3, -1.65, -1.1
    holz, alt = st["Holz"], st["Holz alt"]
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, 0.47), (x1 - x0, y1 - y0, 0.94), alt)
    for k in range(5):                                                                      # Füllungen vorn
        x = x0 + (x1 - x0) * (k + 0.5) / 5
        netz.quader((x, y0 - 0.006, 0.47), ((x1 - x0) / 5 - 0.12, 0.012, 0.66), holz, ohne=("+y",))
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2 - 0.03, 0.965), (x1 - x0 + 0.1, y1 - y0 + 0.12, 0.05), holz)
    z = 0.99
    w = Vector((7.6, -1.35, z))                                                             # Waage
    bs.achteck(netz, st, w, 0.08, 0.0, 0.025, "Kupferblech")
    netz.stab(w, w + Vector((0, 0, 0.4)), 0.02, 0.02, st["Eisen"])
    netz.stab(w + Vector((-0.24, 0, 0.38)), w + Vector((0.24, 0, 0.38)), 0.014, 0.016, st["Eisen"])
    for s in (-1, 1):
        a = w + Vector((0.23 * s, 0, 0.38))
        netz.stab(a, a - Vector((0, 0, 0.24)), 0.006, 0.006, st["Eisen"])
        bs.achteck(netz, st, a - Vector((0, 0, 0.255)), 0.08, 0.0, 0.015, "Kupferblech")
    buch = Vector((6.3, -1.3, z))                                                           # Rechnungsbuch, aufgeschlagen
    for s in (-1, 1):
        netz.quader(buch + Vector((s * 0.12, 0, 0.01)), (0.23, 0.3, 0.02), st["Leder"])
        netz.quader(buch + Vector((s * 0.115, 0, 0.025)), (0.21, 0.28, 0.012), st["Pergament"])
    for i, x in enumerate((5.75, 5.95, 7.0)):
        tiegel(netz, st, (x, -1.42 + 0.1 * (i % 2), z), r=0.07, h=0.08, inhalt=("Rose", None, "Kraut")[i])
    m = Vector((8.0, -1.3, z))
    drehkoerper(netz, st, m, [(0.0, 0.07), (0.02, 0.085), (0.1, 0.08), (0.12, 0.085), (0.12, 0.06)], "Kupferblech", n=10)
    scheibe(netz, st, m, 0.06, z + 0.1, "Loch", n=10)
    s = Vector((6.9, -0.6, 0))                                                              # Schemel
    scheibe(netz, st, s, 0.17, 0.62, "Holz", n=8)
    for i in range(3):
        w_ = math.radians(90 + 120 * i)
        netz.stab(s + Vector((0.15 * math.cos(w_), 0.15 * math.sin(w_), 0)), s + Vector((0.1 * math.cos(w_), 0.1 * math.sin(w_), 0.61)),
                  0.04, 0.04, st["Holz"])


def ladenschild(netz, st):
    """Schild über den Regalen: Brett in der Hausfarbe mit Mörser und Stößel in Ocker."""
    M = si.rahmen(6.75, LY1, 180)
    si.q(netz, st, M, (0, 0.03, 2.72), (1.3, 0.06, 0.42), "Hausfarbe", ohne=("-y",))
    si.q(netz, st, M, (0, 0.065, 2.72), (1.38, 0.01, 0.48), "Fachwerk", ohne=("-y", "+y"))
    netz.vieleck_nach([M @ Vector(p) for p in ((-0.14, 0.062, 2.6), (0.14, 0.062, 2.6), (0.18, 0.062, 2.76), (-0.18, 0.062, 2.76))],
                      M.to_3x3() @ Vector((0, 1, 0)), st["Ocker"])
    netz.vieleck_nach([M @ Vector(p) for p in ((0.02, 0.063, 2.72), (0.06, 0.063, 2.72), (0.2, 0.063, 2.88), (0.16, 0.063, 2.88))],
                      M.to_3x3() @ Vector((0, 1, 0)), st["Ocker"])
    for s in (-1, 1):
        netz.stab(M @ Vector((s * 0.5, 0.03, 2.93)), M @ Vector((s * 0.5, 0.03, H_LADEN)), 0.02, 0.02, st["Eisen"])


def ladeneinrichtung(netz, st):
    """Kleinkram im Laden: ein Korb und ein Fass bei der Gassentür, ein Bord mit Bündeln an der rechten Wand."""
    korb = Vector((6.25, -3.05, 0))
    bs.achteck(netz, st, korb, 0.22, 0.0, 0.35, "Holz alt")
    bs.rund_oben(netz, st, korb, 0.2, 0.355, "Kraut")
    offenes_fass(netz, st, (4.95, -3.05, 0), "Rose", r=0.28, h=0.75)
    M = si.rahmen(LX1, -0.3, 90)
    si.q(netz, st, M, (0, 0.03, 2.25), (1.0, 0.06, 0.07), "Fachwerk", ohne=("-y",))
    for k in range(5):
        buendel(netz, st, M @ Vector((-0.4 + 0.2 * k, 0.08, 2.22)), laenge=0.38, stoff=("Heu", "Kraut")[k % 2])


# ─────────────────────────────────────────────────────────────────────────────
#  Laborturm
# ─────────────────────────────────────────────────────────────────────────────

def turm_ecke(k):
    """Innenecke k (0 … 7) des Achtecks, gegen den Uhrzeigersinn ab −112,5° (Ecke 0 und 1 begrenzen die Vorderwand)."""
    w = math.radians(-112.5 + 45 * k)
    return (TM.x + TR * math.cos(w), TM.y + TR * math.sin(w))


TURM_FENSTER = {0: [], 1: [], 2: [Oe(TM.y, 1.6, 0.55, 1.1)], 3: [], 4: [Oe(TM.x - 0.3, 1.8, 0.55, 1.1)], 5: [], 6: [],
                7: [Oe(TM.x - TA * math.sqrt(0.5), 1.6, 0.55, 1.1)]}   # Wand k liegt bei −90° + 45° · k (0 vorn, 2 rechts, 4 hinten, 6 links)
BUECHER_WAND = 6
TURM_TUER = Oe(TM.x, 0.0, TURMTUER_B, TURMTUER_H)
TIERKREIS_WAND = 5                                      # Wand zwischen Ecke 5 und 6 (links hinten)


def turm_huelle(netz, st, anker):
    """Acht Wände in Kalkputz (die Vorderwand ist die Rückwand des Werkraums, von hier ohne Außenseite), Bruchstein an Ecken und
    unten, Fenster mit Licht."""
    netz.alle = True
    for k in range(8):
        a, b = turm_ecke(k), turm_ecke(k + 1)
        oeffnungen = [TURM_TUER] if k == 0 else TURM_FENSTER[k]
        si.wand(netz, st, a, b, H_TURM, oeffnungen, stoff="Kalkputz", aussen=k != 0, laibung=k != 0, enden=(False, False))
    netz.alle = False
    for k in range(8):
        a, b = turm_ecke(k), turm_ecke(k + 1)
        oeffnungen = [TURM_TUER] if k == 0 else TURM_FENSTER[k]
        frei = [(0.25, 1.65)] if k == TIERKREIS_WAND else ()
        ia.durchbruch(netz, st, a, b, H_TURM, oeffnungen, saat=60 + 5 * k, frei=frei, ecke_hoch=(0.9, 1.9))
        for o in TURM_FENSTER[k]:
            si.fenster(netz, st, a, b, o)
            wand = ia.Wand(a, b, [o])
            anker.append(licht(wand.p(wand.ops[0][0], o.v + o.h / 2, 0.1), (wand.n.x, wand.n.y, 0), "Fenster", 3))


def athanor(netz, st, anker):
    """Athanor: achteckiger Ziegelofen auf einem Bruchsteinsockel mit Kuppel und Abzug in die Decke, vorn (−y) die Feuertür mit
    Glut, links der Blasebalg mit Hebel (Platz 4, Bewegung „Blasebalg“ des Schmieds: Griff 1,36 m hoch, die Figur 0,42 m
    dahinter), rechts die Kapelle mit dem Sandbad, darin der Kolben (Werkstueck_4) mit kupfernem Alembik, dessen Schnabel in
    eine Vorlage auf einem Schemel führt."""
    a = ATHANOR
    r = 0.55
    ap = r * math.cos(math.radians(22.5))
    bs.achteck(netz, st, a, r + 0.08, 0.0, 0.22, "Bruch dunkel")
    m = st["Ziegelmauer"]
    pk = [a + Vector((r * math.cos(math.radians(22.5 + 45 * k)), r * math.sin(math.radians(22.5 + 45 * k)), 0)) for k in range(8)]
    for k in range(8):
        p0, p1 = pk[k], pk[(k + 1) % 8]
        n = ((p0 + p1) / 2 - a).normalized()
        if k == 5:                                                                          # Vorderseite (−y): die Feuertür
            continue
        netz.vieleck_nach([p0 + Vector((0, 0, 0.22)), p1 + Vector((0, 0, 0.22)), p1 + Vector((0, 0, 1.6)), p0 + Vector((0, 0, 1.6))], n, m)
    glut, _, _ = ofen_mit_tuer(netz, st, a + Vector((0, -ap + 0.25, 0.22)), (pk[5] - pk[6]).length, 0.5, 1.38, 0.0,
                               (0.12, 0.48, 0.34), vorn=(0, -1))
    anker.append(licht(glut, (0, -1, 0), "Glut", 3))
    drehkoerper(netz, st, a, [(1.6, r), (1.78, 0.46), (1.9, 0.28), (1.95, 0.17)], "Ziegelmauer", n=8, dreh=math.radians(22.5))
    drehkoerper(netz, st, a, [(1.95, 0.15), (H_TURM, 0.15)], "Schlot", n=8, dreh=math.radians(22.5))
    for z in (0.95, 1.45):                                                                  # Eisenbänder
        drehkoerper(netz, st, a, [(z, r + 0.012), (z + 0.06, r + 0.012)], "Eisen", n=8, dreh=math.radians(22.5))
    # Blasebalg links mit Hebel (wie beim Schmied, die Düse in der linken Seite)
    M = Matrix.Translation((a.x - ap - 0.02, a.y, 0))
    P = si.blasebalg(netz, st, M, 0.0, -1, 0.62, 0.42, 0.3, 0.22, 0.0)
    netz.quader(P(0.38, 0, 0.11), (0.5, 0.36, 0.22), st["Bruch dunkel"])
    pfosten = P(0.7, 0, 0)
    netz.stab(pfosten, pfosten + Vector((0, 0, 1.58)), 0.1, 0.1, st["Holz"])
    griff = P(1.18, 0, fg.ARBEITSHOEHEN["Hebel"][1])
    netz.stab(P(0.2, 0, 1.6), griff, 0.06, 0.06, st["Holz"])
    netz.stab(P(0.32, 0, 0.62), P(0.32, 0, 1.58), 0.025, 0.025, st["Eisen"])
    platz = griff + Vector((-fg.ARBEITSHOEHEN["Hebel"][0], 0, 0))
    # Kapelle mit Sandbad rechts, Kolben und Alembik
    k0 = a + Vector((ap, 0, 0))
    netz.quader(k0 + Vector((0.3, 0, 0.47)), (0.6, 0.62, 0.94), m, ohne=("-x",))
    netz.quader(k0 + Vector((0.3, 0, 0.965)), (0.56, 0.56, 0.05), st["Eisen"], ohne=("-z",))
    kolben = k0 + Vector((0.3, 0, 0.99))
    scheibe(netz, st, kolben, 0.26, 0.991, "Steinstaub", n=10)
    drehkoerper(netz, st, kolben, [(0.12, 0.155), (0.17, 0.12), (0.21, 0.06), (0.36, 0.045)], "Glaskolben", n=10)
    drehkoerper(netz, st, kolben, [(0.34, 0.055), (0.37, 0.13), (0.47, 0.135), (0.55, 0.09), (0.6, 0.04), (0.61, 0.0)], "Kupferblech",
                n=10)
    vorlage = a + Vector((ap + 0.45, -0.95, 0))
    kopf = kolben + Vector((0, 0, 0.45))
    zum = Vector((vorlage.x - kopf.x, vorlage.y - kopf.y, 0)).normalized()
    netz.stab(kopf + zum * 0.12, Vector((vorlage.x, vorlage.y, 0.98)), 0.03, 0.03, st["Kupferblech"], quer=(0, 0, 1))
    scheibe(netz, st, vorlage, 0.17, 0.45, "Holz", n=8)
    for i in range(3):
        w = math.radians(30 + 120 * i)
        netz.stab(vorlage + Vector((0.15 * math.cos(w), 0.15 * math.sin(w), 0)), vorlage + Vector((0.1 * math.cos(w), 0.1 * math.sin(w), 0.44)),
                  0.04, 0.04, st["Holz"])
    drehkoerper(netz, st, vorlage + Vector((0, 0, 0.45)), [(0.0, 0.06), (0.025, 0.11), (0.1, 0.13), (0.18, 0.1), (0.24, 0.035),
                                                          (0.5, 0.03)], "Glaskolben", n=10)
    werkstueck = bs.Netz("Werkstueck_4")
    drehkoerper(werkstueck, st, kolben, [(0.0, 0.09), (0.02, 0.14), (0.12, 0.155)], "Fluessigkeit", n=10)
    o = am_ort(werkstueck, kolben + Vector((0, 0, 0.06)))
    destillat = bs.Netz("Destillat_4")
    drehkoerper(destillat, st, vorlage + Vector((0, 0, 0.45)), [(0.004, 0.111), (0.06, 0.1275), (0.09, 0.128)], "Fluessigkeit", n=10)
    kind = am_ort(destillat, vorlage + Vector((0, 0, 0.5)))
    kind.parent = o
    kind.matrix_parent_inverse = Matrix.Identity(4)
    kind.location -= o.location
    return platz, o, kolben + Vector((0, 0, 0.36))


def tierkreis(netz, st):
    """Tierkreis an der Wand links hinten: dunkle Scheibe mit Ring, zwölf Speichen, Sonne in der Mitte, Sterne in den Feldern."""
    a, b = turm_ecke(TIERKREIS_WAND), turm_ecke(TIERKREIS_WAND + 1)
    wand = ia.Wand(a, b)
    u, v = wand.laenge / 2, 2.75
    n = 24

    def p(du, dv, w):
        return wand.p(u + du, v + dv, w)

    def ring(r0, r1, w, stoff, teile=n):
        for k in range(teile):
            w0, w1 = 2 * math.pi * k / teile, 2 * math.pi * (k + 1) / teile
            netz.vieleck_nach([p(r0 * math.cos(w0), r0 * math.sin(w0), w), p(r1 * math.cos(w0), r1 * math.sin(w0), w),
                               p(r1 * math.cos(w1), r1 * math.sin(w1), w), p(r0 * math.cos(w1), r0 * math.sin(w1), w)], wand.n, st[stoff])
    netz.vieleck_nach([p(0.62 * math.cos(2 * math.pi * k / n), 0.62 * math.sin(2 * math.pi * k / n), 0.006) for k in range(n)], wand.n,
                      st["Nachthimmel"])
    ring(0.62, 0.69, 0.008, "Ocker")
    ring(0.5, 0.53, 0.008, "Ocker")
    for k in range(12):
        w = 2 * math.pi * k / 12
        d = Vector((math.cos(w), math.sin(w)))
        q = Vector((-d.y, d.x)) * 0.008
        netz.vieleck_nach([p(*(d * 0.16 - q), 0.008), p(*(d * 0.62 - q), 0.008), p(*(d * 0.62 + q), 0.008), p(*(d * 0.16 + q), 0.008)],
                          wand.n, st["Ocker"])
        m = Vector((math.cos(w + math.pi / 12), math.sin(w + math.pi / 12))) * 0.575         # Zeichen im äußeren Ring
        for j in range(3 if k % 2 else 4):
            s = m + Vector((math.cos(j * 2.1 + k), math.sin(j * 2.1 + k))) * 0.018
            netz.vieleck_nach([p(*(s + Vector(e) * 0.009), 0.01) for e in ((-1, -1), (1, -1), (1, 1), (-1, 1))], wand.n, st["Leinen"])
        m2 = Vector((math.cos(w + math.pi / 12), math.sin(w + math.pi / 12))) * 0.34          # Sterne
        netz.vieleck_nach([p(*(m2 + Vector((math.cos(2 * math.pi * j / 5), math.sin(2 * math.pi * j / 5))) * 0.025), 0.01) for j in range(5)],
                          wand.n, st["Leinen"])
    netz.vieleck_nach([p(0.14 * math.cos(2 * math.pi * k / 12), 0.14 * math.sin(2 * math.pi * k / 12), 0.01) for k in range(12)], wand.n,
                      st["Ocker"])


def buecher(netz, st):
    """Bücherbord an der linken Wand des Turms und ein Lesepult mit aufgeschlagenem Folianten und Kerze."""
    a, b = turm_ecke(BUECHER_WAND), turm_ecke(BUECHER_WAND + 1)
    wand = ia.Wand(a, b)
    M = Matrix((wand.d, wand.n, Vector((0, 0, 1)))).transposed().to_4x4()
    M = Matrix.Translation(wand.p(wand.laenge / 2 - 0.0, 0, 0)) @ M                        # örtlich: x entlang, y in den Raum
    zufall = random.Random(3)
    for z in (0.9, 1.35, 1.8):
        si.q(netz, st, M, (0, 0.17, z - 0.015), (1.3, 0.32, 0.03), "Holz")
        x = -0.6
        while x < 0.55:
            dicke = zufall.uniform(0.04, 0.08)
            hoch = zufall.uniform(0.24, 0.36)
            neig = 0.0 if zufall.random() < 0.85 else 0.25
            t = M @ Matrix.Translation((x + dicke / 2, 0.16, z + hoch / 2)) @ Matrix.Rotation(neig, 4, "Y")
            netz.quader((0, 0, 0), (dicke, 0.22 + 0.04 * zufall.random(), hoch), st[zufall.choice(("Leder", "Rose", "Kraut dunkel", "Ocker",
                                                                                                     "Fachwerk"))], t)
            x += dicke + 0.01
    for x in (-0.65, 0.65):
        si.q(netz, st, M, (x, 0.17, 1.4), (0.04, 0.34, 1.0), "Holz", ohne=("-y",))
    pult = si.rahmen(TM.x - 1.75, TM.y - 0.35, -90)                             # vor dem Bücherbord, der Leser steht zur Mitte
    si.pult(netz, st, pult)
    kerze = pult @ Vector((-0.42, 0.2, 1.08))
    bs.achteck(netz, st, kerze, 0.035, 0.0, 0.03, "Kupferblech")
    bs.achteck(netz, st, kerze + Vector((0, 0, 0.03)), 0.015, 0.0, 0.12, "Leinen", n=6)


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen
# ─────────────────────────────────────────────────────────────────────────────

def bauen():
    import bpy
    bs.neue_szene()
    ia.RUSS_QUELLEN.clear()
    _lichter[0] = 0
    st = bs.Stoffe()
    werk, lager = bs.Netz("Raum_Werk"), bs.Netz("Raum_Lager", alle=True)
    laden, turm = bs.Netz("Raum_Laden"), bs.Netz("Raum_Turm")
    decke_w, decke_l = bs.Netz("Decke_Werk", alle=True), bs.Netz("Decke_Lager", alle=True)
    decke_d, decke_t = bs.Netz("Decke_Laden", alle=True), bs.Netz("Decke_Turm", alle=True)
    kran, himmel = bs.Netz("Kranbalken", alle=True), bs.Netz("Himmel_Lager")
    teile = {}                    # Name → (Netz, Raum)

    def teil(name, raum="Werk"):
        if name not in teile:
            teile[name] = (bs.Netz(name), raum)
        return teile[name][0]

    anker = []                    # (Objekt, Raum)
    fertig = []                   # schon gebaute Objekte (Werkstücke): (Objekt, Raum)

    def setzen(o, raum="Werk", stufe=None):
        if stufe is not None:
            o["stufe"] = stufe
        anker.append((o, raum))

    # ── Werkraum: Hülle ───────────────────────────────────────────────────────
    W = WERK_OEFFNUNGEN
    kanten = ecken(WX0, WX1, WY0, WY1)
    si.steinraum(werk, decke_w, lager, st, WX0, WX1, WY0, WY1, H_WERK, {
        "vorne": dict(oeffnungen=W["vorne"]),
        "rechts": dict(oeffnungen=W["rechts"], aussen=False),
        "hinten": dict(oeffnungen=W["hinten"], aussen=False),
        "links": dict(oeffnungen=W["links"]),
    }, luke=LUKE, schlote=[KSCHLOT], saat=4,
        frei={"hinten": [(WX1 - KESSEL.x - 1.1, WX1 - KESSEL.x + 1.1)]})
    liste = []
    fenster_und_licht(werk, st, kanten, W, liste)
    for o in liste:
        setzen(o)
    ia.russ(werk, st, *kanten["hinten"], WX1 - KESSEL.x, 0.95, 2.05, H_WERK, W["hinten"])      # Ruß über dem Rauchfang
    rauchfang(werk, st)
    liste = []
    fertig.append((kessel(teil("Bedienung_Produktion_Kessel"), st, liste), "Werk"))
    for o in liste:
        setzen(o)
    fertig.append((kraeutertisch(teil("Bedienung_Produktion_Kraeutertisch"), st), "Werk"))
    kraeuter_ueber_tisch(werk, st)
    offenes_fass(werk, st, (-3.1, 2.65, 0), "Farbe", r=0.62, h=0.6)                       # Farbbottich (wie außen in der Tinkturei)
    werk.stab((-2.9, 2.75, 0.45), (-2.5, 3.15, 1.45), 0.06, 0.06, st["Holz"])             # Rührholz
    for i, (x, y) in enumerate(((-3.55, 1.6), (-3.5, 0.95))):                              # Kräuterkörbe an der linken Wand
        bs.achteck(werk, st, Vector((x, y, 0)), 0.24, 0.0, 0.34, "Holz alt")
        bs.rund_oben(werk, st, (x, y), 0.22, 0.345, ("Kraut", "Heu")[i])
    ti.laterne(werk, st, Vector((-0.8, -0.2, 2.95)), H_WERK - 0.26)                       # zwischen zwei Deckenbalken
    setzen(licht(Vector((-0.8, -0.2, 2.8)), (0, 0, -1), "Lampe"))

    # ── Werkraum: Bedienung, Türen, Einrichtung ──────────────────────────────
    si.pult(teil("Bedienung_Personal_Buch"), st, si.rahmen(WX0 + 0.05, -2.6, -90))
    M_wand = si.rahmen(WX1, -0.1, 90)
    werkbank(werk, st, M_wand)
    si.werkzeugwand(teil("Bedienung_Einrichten_Werkzeugwand"), st, M_wand)
    werkzeug1(teil("Einrichtung_Werkzeug1"), st, M_wand)
    werkzeug2(teil("Einrichtung_Werkzeug2"), st, M_wand)
    destillierhelm(teil("Einrichtung_Destillierhelm"), st)
    ti.hoftor(teil("Bedienung_Karren_Hoftor"), st, si.rahmen(WX0, HOFTOR_Y, -90), HOFTOR_B, HOFTOR_H, r=0.12)
    M_tor = si.rahmen(WX0, HOFTOR_Y, -90)
    for s in (-1, 1):
        si.q(werk, st, M_tor, (s * (HOFTOR_B / 2 + 0.11), 0.04, (HOFTOR_H + 0.1) / 2), (0.22, 0.08, HOFTOR_H + 0.1), "Fachwerk", ohne=("-y",))
    si.q(werk, st, M_tor, (0, 0.05, HOFTOR_H + 0.16), (HOFTOR_B + 0.6, 0.1, 0.28), "Fachwerk", ohne=("-y",))
    M_holz = si.rahmen(HOLZTUER_X, WY0, 0)
    si.bretttuer(teil("Holztuer"), st, M_holz, HOLZTUER_B, HOLZTUER_H)
    si.tuerstock(werk, st, M_holz, HOLZTUER_B, HOLZTUER_H)
    si.eisentuer(teil("Einrichtung_Eisentuer"), st, M_holz, HOLZTUER_B, HOLZTUER_H)
    si.loeschfass(teil("Einrichtung_Loeschfass"), st, Vector((-1.65, 3.05, 0)))
    handschuhe(teil("Einrichtung_Handschuhe"), st)
    ia.treppe(teil("Tuer_Werk_Lager"), st, TREPPE_X, TREPPE_FUSS, TREPPE_OBEN, Z_LAGER)       # Treppe hinauf ins Kräuterlager
    M_lt = si.rahmen(WX1, LADENTUER_Y, 90)                                                  # Tür zum Laden (ab Stufe 2)
    si.bretttuer(teil("Tuer_Werk_Laden"), st, M_lt, LADENTUER_B, LADENTUER_H, stoff="Brett")
    si.tuerstock(teil("Tuer_Werk_Laden"), st, M_lt, LADENTUER_B, LADENTUER_H)
    si.bretttuer(teil("Tuer_Laden_Werk", "Laden"), st, si.rahmen(LX0, LADENTUER_Y, -90), LADENTUER_B, LADENTUER_H, stoff="Brett")
    M_tt = si.rahmen(TM.x, WY1, 180)                                                        # Tür zum Turm (ab Stufe 3)
    si.bretttuer(teil("Tuer_Werk_Turm"), st, M_tt, TURMTUER_B, TURMTUER_H, stoff="Brett")
    si.tuerstock(teil("Tuer_Werk_Turm"), st, M_tt, TURMTUER_B, TURMTUER_H)
    si.bretttuer(teil("Tuer_Turm_Werk", "Turm"), st, si.rahmen(TM.x, WY1 + WAND, 0), TURMTUER_B, TURMTUER_H, stoff="Brett")

    # ── Werkraum: Arbeitsplätze, Werkstücke, Dampf ───────────────────────────
    setzen(si.arbeit("Arbeit_1_Moersern", W1, (0, -1), 1))
    setzen(si.arbeit("Arbeit_2_Ruehren", W2, (0, -1), 1))
    setzen(si.arbeit("Arbeit_3_Destillieren", W3, (1, 0), 2))
    setzen(bs.anker("Dampf_1", KESSEL + Vector((0, 0, BRUEHE + 0.04))))
    setzen(bs.anker("Dampf_2", OFEN + Vector((0, 0, 1.2))), stufe=2)

    # ── Stufen (kumulativ), Destille, bewegte Teile ──────────────────────────
    stufen = [bs.Netz(f"Stufe{k}") for k in (1, 2, 3)]
    ziegel = 0
    glut3 = None
    for k, netz in enumerate(stufen, 1):
        if k < 3:
            ia.lehmboden(netz, st, WX0, WX1, WY0, WY1, saat=6)
        else:
            ziegel = ia.fischgraet(netz, st, WX0, WX1, WY0, WY1, saat=6)
        if k == 1:
            vermauert(netz, st, *kanten["rechts"], W["rechts"][0])
        if k <= 2:
            vermauert(netz, st, *kanten["hinten"], W["hinten"][1])
        if k >= 2:
            glut3 = destille(netz, st, k)
    stufen_obj = [s.objekt() for s in stufen]
    setzen(licht(glut3[0], (glut3[1].x, glut3[1].y, 0), "Glut", 2))
    bruehe = fertig[0][0]
    bewegt = blasen(st, bruehe)
    werkstueck3, tropfen = destille_werkstueck(st)
    werkstueck3["stufe"] = 2
    fertig.append((werkstueck3, "Werk"))
    bewegt += tropfen_bewegung(tropfen)
    print(f"Backsteinpflaster: {ziegel} Ziegel")

    # ── Kräuterlager unter dem Dach ───────────────────────────────────────────
    z = Z_LAGER
    DACH.kniestoecke(lager, st, "Kalkputz")
    luke = Oe(LADELUKE_Y, 0.0, 1.2, 1.8)                      # Höhen über dem Dachboden
    gfenster = Oe(0.0, 2.6, 0.6, 0.6)
    DACH.giebel(lager, st, False, [luke], "Kalkputz", WAND)   # Ladeluke im linken Giebel: zum Hof (Hoftor links), nicht über dem Laden
    DACH.giebel(lager, st, True, [gfenster], "Kalkputz", WAND)
    for i, (seite, oeff) in enumerate((("vorne", []), ("hinten", []), ("links", [luke]), ("rechts", [gfenster]))):
        ia.durchbruch(lager, st, *kanten[seite], DACH.kniestock, oeff, saat=40 + 7 * i, boden=z, ecke_hoch=(0.8, 1.25),
                      frei=[(0.0, 0.9), (WX1 - KSCHLOT[1] - 0.2, WX1 - KSCHLOT[0] + 0.2)] if seite == "hinten" else
                      [(KRAEUTER_Y[0] - WY0 - 0.2, LUKE[3] - WY0 + 0.15)] if seite == "rechts" else ())  # nicht hinter Gestell und Luke
    DACH.dach(decke_l, st, aussparen=[KSCHLOT[:2]])
    licht_luke = ladeluke_links(lager, kran, himmel, st, luke)
    si.fenster(lager, st, *kanten["rechts"], Oe(gfenster.s, z + gfenster.v, gfenster.b, gfenster.h))
    lager.alle = True
    schlot_dachboden(lager, st)
    ia.bodenluke(teil("Tuer_Lager_Werk", "Lager"), st, LUKE, z, gelaender_x=LUKE[0] - 0.05)
    setzen(licht(licht_luke, (1, 0, 0), "Fenster"), "Lager")
    setzen(licht(Vector((WX1 - 0.1, gfenster.s, z + gfenster.v + gfenster.h / 2)), (-1, 0, 0), "Fenster"), "Lager")
    kraeuterlager(teil("Bedienung_Lager_Kraeuter", "Lager"), st, z)
    si.kisten(lager, st, Vector((-2.4, -1.95, z)))                                            # bei der Ladeluke
    darrhorden(teil("Einrichtung_Regale", "Lager"), st, 0.4, 3.3)                            # vorn, niedrig
    leinen_mit_kraeutern(lager, st, z)
    faesser(lager, st, z)
    nummer = 1
    for y in (1.5, 0.55):
        for x in (-1.6, -0.6, 0.4, 1.4):
            setzen(bs.anker(f"Lager_{nummer}", (x, y, z)), "Lager")
            nummer += 1

    # ── Laden (ab Stufe 2) ───────────────────────────────────────────────────
    L = LADEN_OEFFNUNGEN
    kanten_l = ecken(LX0, LX1, LY0, LY1)
    si.steinraum(laden, decke_d, None, st, LX0, LX1, LY0, LY1, H_LADEN, {
        "vorne": dict(oeffnungen=L["vorne"], enden=(False, True)),        # am Werkraum nicht über dessen Außenseite
        "rechts": dict(oeffnungen=L["rechts"]),
        "hinten": dict(oeffnungen=L["hinten"], enden=(True, False)),
        "links": dict(oeffnungen=L["links"], aussen=False, laibung=False),
    }, saat=14, frei={"hinten": [(0.0, LX1 - LX0)]})
    liste = []
    fenster_und_licht(laden, st, kanten_l, L, liste, stufe=2)
    for o in liste:
        setzen(o, "Laden")
    ia.lehmboden(laden, st, LX0, LX1, LY0, LY1, saat=15, flecken=6, halme=14)
    pflaster = bs.Netz("Fischgraet_Laden")
    ziegel_laden = ia.fischgraet(pflaster, st, LX0, LX1, LY0, LY1, saat=16, z=0.02)
    regale(laden, st)
    tresen(teil("Bedienung_Lager_Tresen", "Laden"), st)
    ladenschild(laden, st)
    ladeneinrichtung(laden, st)
    M_gt = si.rahmen(GASSENTUER_X, LY0, 0)                                                  # Tür zur Gasse (nur Kulisse)
    si.bretttuer(laden, st, M_gt, 1.0, 2.1)
    si.tuerstock(laden, st, M_gt, 1.0, 2.1)
    si.tuerstock(laden, st, si.rahmen(LX0, LADENTUER_Y, -90), LADENTUER_B, LADENTUER_H)
    ti.laterne(laden, st, Vector((6.9, -1.0, 2.4)), H_LADEN - 0.26)
    setzen(licht(Vector((6.9, -1.0, 2.25)), (0, 0, -1), "Lampe", 2), "Laden")
    for o in auslage_anker():
        setzen(o, "Laden", stufe=2)
    print(f"Backsteinpflaster im Laden: {ziegel_laden} Ziegel")

    # ── Laborturm (ab Stufe 3) ───────────────────────────────────────────────
    liste = []
    turm_huelle(turm, st, liste)
    for o in liste:
        setzen(o, "Turm")
    umriss = [turm_ecke(k) for k in range(8)]
    ziegel_turm = ia.fischgraet(turm, st, TM.x - TR, TM.x + TR, TM.y - TA, TM.y + TA, saat=18, umriss=umriss)
    loch = (ATHANOR.x - 0.16, ATHANOR.x + 0.16, ATHANOR.y - 0.16, ATHANOR.y + 0.16)
    ia.balkendecke(decke_t, st, TM.x - TR, TM.x + TR, TM.y - TA, TM.y + TA, H_TURM, schlote=[loch], abdeckung=WAND, saat=19)
    liste = []
    platz4, werkstueck4, dampf3 = athanor(turm, st, liste)
    for o in liste:
        setzen(o, "Turm")
    werkstueck4["stufe"] = 3
    fertig.append((werkstueck4, "Turm"))
    tierkreis(turm, st)
    buecher(turm, st)
    ti.laterne(turm, st, Vector((TM.x - 0.4, TM.y - 0.3, 3.6)), H_TURM - 0.26)
    setzen(licht(Vector((TM.x - 0.4, TM.y - 0.3, 3.45)), (0, 0, -1), "Lampe", 3), "Turm")
    setzen(si.arbeit("Arbeit_4_Blasebalg", platz4, (1, 0), 3), "Turm")
    setzen(bs.anker("Dampf_3", dampf3), "Turm", stufe=3)
    print(f"Backsteinpflaster im Turm: {ziegel_turm} Ziegel")

    # ── Blickpunkte ──────────────────────────────────────────────────────────
    for name, (auge, ziel) in BLICKE.items():
        raum = name.split("_")[1]
        setzen(si.blick(name, auge, ziel), raum, stufe={"Laden": 2, "Turm": 3}.get(raum))

    # ── Objekte, Zustände, Hierarchie ─────────────────────────────────────────
    raeume = {"Werk": werk.objekt(), "Lager": lager.objekt(), "Laden": laden.objekt(), "Turm": turm.objekt()}
    raeume["Laden"]["stufe"] = 2
    raeume["Turm"]["stufe"] = 3
    kinder = {r: [] for r in raeume}
    kinder["Werk"] += [decke_w.objekt()] + stufen_obj
    kinder["Lager"] += [decke_l.objekt(), kran.objekt(), himmel.objekt()]
    o_pflaster = pflaster.objekt()
    o_pflaster["stufe"] = 3
    kinder["Laden"] += [decke_d.objekt(), o_pflaster]
    kinder["Turm"].append(decke_t.objekt())
    for o, raum in fertig:
        kinder[raum].append(o)
    for name, (netz, raum) in teile.items():
        o = netz.objekt()
        kinder[raum].append(o)
        if name.startswith("Einrichtung_"):
            bs.zustand(o)
        if name in ("Tuer_Werk_Laden", "Tuer_Laden_Werk"):
            o["stufe"] = 2
        if name in ("Tuer_Werk_Turm", "Tuer_Turm_Werk"):
            o["stufe"] = 3
        if name in SCHILDER:
            o["schild"] = SCHILDER[name]
    for o, raum in anker:
        kinder[raum].append(o)
    for raum, liste in kinder.items():
        for o in liste:
            o.parent = raeume[raum]
            o.matrix_parent_inverse = Matrix.Identity(4)
    stufen_obj[0]["zustand"] = True
    for o in stufen_obj[1:]:
        bs.zustand(o)
    ti.animieren(bewegt, ARBEIT_BILDER)
    bpy.context.view_layer.update()
    return list(raeume.values()) + [o for liste in kinder.values() for o in liste]


def main():
    objekte = bauen()
    vorher, nachher = ia.farben_zusammenlegen(objekte)
    print(f"Materialien: {vorher} → {nachher}")
    ia.pruefbericht(objekte, NAME)
    print("geschrieben:", ti.exportieren(objekte, NAME))
    if "--ohne-bilder" in sys.argv:
        return
    ia.vorschau(NAME, list(BLICKE), STUFEN, "--schnell" in sys.argv, (WX0 - WAND, LX1 + WAND, WY0 - WAND, TM.y + TA + WAND),
                (WX0 - WAND, WX1 + WAND, WY0 - WAND, WY1 + WAND), DACH, {"Werk", "Laden", "Turm"}, bild=1 + int(1.5 * FPS), lampe=80,
                fuell=[(0, 0, H_WERK - 0.4, 170, 1), (0, 0.3, Z_LAGER + 3.0, 90, 1), ((LX0 + LX1) / 2, (LY0 + LY1) / 2, H_LADEN - 0.4, 120, 2),
                       (TM.x, TM.y, H_TURM - 0.6, 170, 3)])
    verkleinern(NAME + "-blicke", 0.875)


def verkleinern(name, faktor):
    """docs/modelle/<name>.png auf `faktor` verkleinern: 14 Blickpunkte zu 640 × 360 ergäben über 3 MB."""
    import OpenImageIO as oiio
    pfad = os.path.join(bs.VORSCHAU, name + ".png")
    bild = oiio.ImageBuf(pfad)
    spec = bild.spec()
    klein = oiio.ImageBufAlgo.resize(bild, roi=oiio.ROI(0, round(spec.width * faktor), 0, round(spec.height * faktor), 0, 1, 0,
                                                        spec.nchannels))
    klein.write(pfad)
    print("verkleinert:", pfad)


if __name__ == "__main__":
    main()
