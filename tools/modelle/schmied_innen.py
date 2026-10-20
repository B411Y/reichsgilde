"""Innenraum des Schmieds: Werkraum und darüber das Lager unter dem Dach, Stufen Gießerei, Schmiede, Goldschmiede.

    blender -b --python tools/modelle/schmied_innen.py -- [--schnell] [--ohne-bilder]
    python tools/modelle/schmied_innen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/schmied_innen.glb und Bilder nach docs/modelle/ (schmied_innen-blicke.png: das Bild
jedes Blickpunkts, schmied_innen-stufen.png: Erdgeschoss von oben je Stufe, schmied_innen-grundriss.png: Erdgeschoss
und Dachboden mit Ankern).

Koordinaten (Blender: Z oben; im Spiel Y oben, Blender −Y = Spiel +Z): Ursprung in der Mitte des Werkraums auf dem
Boden. Werkraum innen x −4,5 … 4,5, y −3,25 … 3,25, 3,6 m hoch, Wände 0,5 m: gekalkter Putz, an den Ecken und unten
bricht der Bruchstein durch, über dem Rauchfang Ruß; Boden in Stufe 1 und 2 gestampfter Lehm, in Stufe 3
Backsteinpflaster im Fischgrätmuster; Balkendecke. Das Lager liegt darüber unter dem Dach (Boden in 3,65 m, gleiche
Grundfläche, First entlang x, Kniestock 1,3 m, 50°): Dielen, Sparren mit Schalung, Kehlbalken, Ladeluke mit
Kranbalken im rechten Giebel. Eine steile Treppe an der rechten Wand führt durch ein Loch in der Decke hinauf. Beide
Räume sind rundum geschlossen, die Kamera steht im Raum (Bausteine dafür in innenausbau.py).

Knoten im GLB (alle unter „Raum_Werk“ bzw. „Raum_Lager“; beide Raumknoten liegen im Ursprung, die Lage der Kinder ist
also zugleich die Lage im Modell):
    Raum_Werk, Raum_Lager       Wände, Boden, Fenster und die Grundausstattung, in jeder Stufe gleich
    Decke_Werk, Decke_Lager     Balkendecke des Werkraums mit Treppenloch, Dach des Lagers (eigene Knoten, damit ein
                                Bild von oben sie ausblenden kann)
    Stufe1–Stufe3               Ausstattung je Stufe, kumulativ wie außen: genau einer sichtbar, jeder enthält alles,
                                was die Stufe hat (Stufe1 Gießerei: Gussformen; Stufe2 dazu zweite Esse und Schleifbock;
                                Stufe3 dazu Feinwerktisch mit Waage)
    Bedienung_…                 anklickbare Gegenstände (je ein Knoten, das Spiel baut die Klickfläche aus dem Umriss)
    Tuer_<von>_<nach>           Tür in einen anderen Raum (anklickbar): Tuer_Werk_Lager ist die Treppe hinauf,
                                Tuer_Lager_Werk die Bodenluke mit Klappe und Geländer; extras „schild“: Text fürs Spiel
    Einrichtung_…               gekaufte Einrichtung, standardmäßig aus; Holztuer wird ausgeblendet, sobald
                                Einrichtung_Eisentuer gekauft ist
    Blick_<Raum>_<n>            Blickpunkte der Kamera (leere Knoten, Kamera blickt mit −Z, oben +Y): 0 Mitte,
                                L1/L2 links, R1/R2 rechts – das Spiel schwenkt bei Maus am Bildrand zum nächsten
    Arbeit_<n>_<Bewegung>       Arbeitsplätze (Figur mit dieser Bewegung, Blick −Z zum Werkstück, Figur um 180° drehen);
                                extras „stufe“: ab welcher Stufe es den Platz gibt
    Lager_<n>                   Stellplätze für Warenstapel auf dem Boden des Lagers
    Werkstueck_<n>              Stück in Arbeit (nimmt die Farbe der Ware an); extras „stufe“
    Licht_<n>                   Lichtquellen: extras „art“ = „Glut“ (Esse) oder „Fenster“ (Tageslicht, −Z in den Raum,
                                auch an der Ladeluke), „stufe“ ab welcher Stufe
    Kranbalken, Himmel_Lager    unter Raum_Lager: Kranbalken mit Rolle und Seil durch die Ladeluke, dahinter eine Fläche
                                „Fenster“ als Himmel
    Schlot2                     unter Raum_Lager: Schlot der zweiten Esse im Dachboden, extras „stufe“ = 2 (gehört zum Lager
                                und seiner Lichtebene, nicht zu Stufe2/Stufe3 im Werkraum)
Materialien: „Farben“ (Eckenfarben, innenausbau.farben_zusammenlegen mit dem Rußverlauf; die Materialart für den
Holzschnitt je Fläche als UV.y, bausteine.arten_setzen – „keine“, wo das Modell Steine, Ziegel oder Bretter schon
zeichnet), dazu „Glut“ (Esse, leuchtet), „Fenster“ (Scheiben, Himmel hinter der Ladeluke),
„Hausfarbe“ (Tore und Türen), „Eisen“, „Wasser“, „Gold“.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import innenausbau as ia  # noqa: E402  (Böden, Putz, Ruß, Decke, Treppe, Dachboden, Prüfbericht, Vorschau)
import schmied  # noqa: E402  (Kohlehaufen wie außen)

NAME = "schmied_innen"
STUFEN = ("Gießerei", "Schmiede", "Goldschmiede")

bs.FARBEN.update({
    "Russ": (0.24, 0.22, 0.21),            # verrußter Lehm am Rauchfang
    "Leinen": (0.84, 0.81, 0.72),          # Buchseiten
    "Fels Sandstein": (0.66, 0.57, 0.43),  # Schleifstein (roher Sandstein, wie beim Steinmetz)
})

# ── Maße ─────────────────────────────────────────────────────────────────────
WX0, WX1, WY0, WY1 = -4.5, 4.5, -3.25, 3.25     # Werkraum innen
H_WERK = 3.6
Z_LAGER = H_WERK + ia.DIELE                     # Boden des Lagers unter dem Dach (gleiche Grundfläche)
WAND = 0.5
UEBER = 0.3                                     # Außenseite der Wände reicht so weit über die Decke (Abdeckung)
ESSE_X = -0.4                                   # Esse an der Rückwand
SCHLOT = (ESSE_X - 0.45, ESSE_X + 0.45, WY1 - 0.6, WY1)   # Rauchfang der Esse: durch Decke und Dachboden (Wechsel)
SCHLOT2 = (-3.05, -2.15, WY0, WY0 + 0.6)       # zweite Esse (ab Stufe 2): Schlot im Dachboden vorn
TREPPE_X, TREPPE_FUSS, TREPPE_OBEN = 4.13, 3.05, 1.8    # steile Treppe an der rechten Wand, steigt nach vorn
LUKE = (3.74, WX1, 1.76, 2.78)                  # Treppenloch in der Decke (x0, x1, y0, y1)
LADELUKE_Y = -1.1                               # Ladeluke im rechten Giebel (Mitte), 1,2 × 1,8 m
DACH = ia.Dachboden(WX0, WX1, WY0, WY1, Z_LAGER, 50)
HOFTOR_Y, HOFTOR_B, HOFTOR_H = 0.75, 2.6, 2.9  # Hoftor in der linken Wand
HOLZTUER_X = -2.95                             # Tür zum Hof in der Rückwand
AMBOSS = Vector((ESSE_X, 1.1, 0))                # Horn nach +x, der Schmied steht zwischen Amboss und Esse
SICHTFELD = 50.0                                # Grad, senkrecht; Bild 16:9

# Blickpunkte: Auge, Ziel (Blender-Koordinaten)
BLICKE = {
    "Blick_Werk_0": ((0.0, -3.1, 2.6), (-0.1, 1.8, 0.75)),        # Esse, Amboss, Blasebalg, Trog
    "Blick_Werk_L1": ((1.3, -2.5, 2.5), (-4.0, 0.3, 0.9)),         # Hoftor, Personalbuch
    "Blick_Werk_L2": ((2.2, 1.0, 2.6), (-3.4, -2.3, 0.8)),         # zweite Esse, Personalbuch
    "Blick_Werk_R1": ((-1.0, -0.6, 2.6), (4.0, 1.4, 1.0)),         # Werkbank, Werkzeugwand, Treppe ins Lager
    "Blick_Werk_R2": ((0.2, 1.2, 2.6), (3.6, -2.2, 0.7)),         # Schleifbock, Feinwerktisch
    "Blick_Lager_0": ((-3.9, -1.4, Z_LAGER + 2.1), (3.5, -0.4, Z_LAGER + 0.7)),     # den First entlang: Ladeluke, Stapel
    "Blick_Lager_L1": ((3.7, -2.4, Z_LAGER + 2.0), (-4.2, 0.9, Z_LAGER + 0.9)),     # zurück: Regal am Giebel, Stapel
    "Blick_Lager_R1": ((3.0, -0.1, Z_LAGER + 2.2), (4.1, 2.3, Z_LAGER - 0.6)),      # hinunter in die Bodenluke mit der Treppe
}


class Oe:
    """Öffnung in einer Wand: s = Mitte entlang der Wand (Welt-x bzw. Welt-y), v Unterkante, b Breite, h Höhe."""

    def __init__(self, s, v, b, h):
        self.s, self.v, self.b, self.h = s, v, b, h


def rahmen(x, y, drehung, z=0.0):
    """Örtliches System eines Gegenstands an der Wand: +y zeigt in den Raum, z oben. `drehung` wie bei Ankern:
    0 = Vorderwand (Raum in +Y), 180 = Rückwand, −90 = linke Wand (Raum in +X), 90 = rechte Wand."""
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(drehung), 4, "Z")


def wand(netz, st, a, b, hoehe, oeffnungen=(), stoff="Bruchstein", dicke=WAND, aussen=True, laibung=True,
         enden=(True, True), fugen=False, saat=0):
    """Wand mit Öffnungen, a → b auf der Innenkante (Grundriss), der Raum liegt links der Laufrichtung.
    Innenseite bis `hoehe`, Außenseite (`aussen`) bis über die Decke und an den Enden (`enden`) um die Wanddicke
    verlängert, so schließen die Ecken. Laibungen der Öffnungen (Sturz nach unten: das Netz muss `alle` haben).
    `fugen`: Lager- und Stoßfugen auf der Innenseite (Mauerwerk)."""
    a3, b3 = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    d = b3 - a3
    laenge = d.length
    d.normalize()
    n = Vector((-d.y, d.x, 0))
    m = st[stoff]
    entlang_x = abs(d.x) > 0.5

    def p(u, v, w=0.0):
        return a3 + d * u + Vector((0, 0, v)) - n * w

    ops = sorted((((o.s - (a3.x if entlang_x else a3.y)) * (d.x if entlang_x else d.y)), o) for o in oeffnungen)
    grenzen = [0.0] + [g for u, o in ops for g in (u - o.b / 2, u + o.b / 2)] + [laenge]
    for i in range(len(grenzen) - 1):
        u0, u1 = grenzen[i], grenzen[i + 1]
        if u1 - u0 < 1e-4:
            continue
        o = next((o for u, o in ops if abs((u0 + u1) / 2 - u) < o.b / 2), None)
        for v0, v1 in ([(0.0, hoehe)] if o is None else [(0.0, o.v), (o.v + o.h, hoehe)]):
            if v1 - v0 > 1e-4:
                netz.vieleck_nach([p(u0, v0), p(u1, v0), p(u1, v1), p(u0, v1)], n, m)
        if aussen:
            ua = u0 - (dicke if i == 0 and enden[0] else 0.0)
            ub = u1 + (dicke if i == len(grenzen) - 2 and enden[1] else 0.0)
            for v0, v1 in ([(0.0, hoehe + UEBER)] if o is None else [(0.0, o.v), (o.v + o.h, hoehe + UEBER)]):
                if v1 - v0 > 1e-4:
                    netz.vieleck_nach([p(ua, v0, dicke), p(ub, v0, dicke), p(ub, v1, dicke), p(ua, v1, dicke)], -n, m)
    if laibung:
        for u, o in ops:
            u0, u1, v0, v1 = u - o.b / 2, u + o.b / 2, o.v, o.v + o.h
            netz.vieleck_nach([p(u0, v0), p(u0, v1), p(u0, v1, dicke), p(u0, v0, dicke)], d, m)
            netz.vieleck_nach([p(u1, v0), p(u1, v1), p(u1, v1, dicke), p(u1, v0, dicke)], -d, m)
            netz.vieleck_nach([p(u0, v1), p(u1, v1), p(u1, v1, dicke), p(u0, v1, dicke)], (0, 0, -1), m)
            netz.vieleck_nach([p(u0, v0), p(u1, v0), p(u1, v0, dicke), p(u0, v0, dicke)], (0, 0, 1),
                              st["Bruchstein dunkel"] if v0 == 0 else m)
    if not fugen:
        return
    zufall = random.Random(saat)

    def frei(v0, v1, rand=0.0):
        """Stücke der Innenseite zwischen den Höhen v0 und v1, die keine Öffnung schneidet."""
        stuecke = [(0.0, laenge)]
        for u, o in ops:
            if o.v - rand < v1 and o.v + o.h + rand > v0:
                a_, b_ = u - o.b / 2 - rand, u + o.b / 2 + rand
                stuecke = [s for x0, x1 in stuecke for s in ((x0, min(x1, a_)), (max(x0, b_), x1)) if s[1] - s[0] > 0.05]
        return stuecke

    fuge, w, f = st["Fuge"], -0.003, 0.014
    abstand = 0.45
    hoehen = [k * abstand for k in range(1, int(hoehe / abstand) + 1) if k * abstand < hoehe - 0.15]
    for v in hoehen:
        for x0, x1 in frei(v - f, v + f):
            netz.vieleck_nach([p(x0, v - f, w), p(x1, v - f, w), p(x1, v + f, w), p(x0, v + f, w)], n, fuge)
    schichten = [0.0] + hoehen + [hoehe]
    for k in range(len(schichten) - 1):
        v0, v1 = schichten[k] + f, schichten[k + 1] - f
        stuecke = frei(v0, v1)
        u = zufall.uniform(0.15, 0.7)
        while u < laenge - 0.15:
            if any(x0 + 0.06 < u < x1 - 0.06 for x0, x1 in stuecke):
                netz.vieleck_nach([p(u - 0.012, v0, w), p(u + 0.012, v0, w), p(u + 0.012, v1, w), p(u - 0.012, v1, w)], n, fuge)
            u += zufall.uniform(0.55, 0.95)


def fenster(netz, st, a, b, o, dicke=WAND):
    """Scheibe (Material „Fenster“) in der Öffnung o der Wand a → b, mit Holzkreuz und Eisengittern davor (innen)."""
    a3, b3 = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    d = (b3 - a3).normalized()
    n = Vector((-d.y, d.x, 0))
    entlang_x = abs(d.x) > 0.5
    u = (o.s - (a3.x if entlang_x else a3.y)) * (d.x if entlang_x else d.y)
    t = 0.55 * dicke

    def p(du, v, w):
        return a3 + d * (u + du) + Vector((0, 0, v)) - n * w

    h2 = o.b / 2
    netz.vieleck_nach([p(-h2, o.v, t), p(h2, o.v, t), p(h2, o.v + o.h, t), p(-h2, o.v + o.h, t)], n, st["Fenster"])
    vk = o.v + o.h * 0.6
    for du0, du1, v0, v1 in ((-0.03, 0.03, o.v, o.v + o.h), (-h2, h2, vk - 0.03, vk + 0.03)):     # Fensterkreuz
        netz.vieleck_nach([p(du0, v0, t - 0.015), p(du1, v0, t - 0.015), p(du1, v1, t - 0.015), p(du0, v1, t - 0.015)],
                          n, st["Fachwerk"])
    for k in (1, 3):                                                                              # Gitterstäbe
        du = -h2 + o.b * k / 4
        netz.stab(p(du, o.v, t - 0.09), p(du, o.v + o.h, t - 0.09), 0.025, 0.025, st["Eisen"], quer=n, ohne=("+x", "-x"))
    netz.stab(p(-h2, o.v + o.h * 0.3, t - 0.1), p(h2, o.v + o.h * 0.3, t - 0.1), 0.025, 0.025, st["Eisen"], quer=n,
              ohne=("+x", "-x"))


def steinraum(netz, decke, oben, st, x0, x1, y0, y1, hoehe, waende, luke=None, schlote=(), saat=0, frei=None):
    """Raum im Steinhaus: Wände aus gekalktem Putz (waende = {seite: dict(oeffnungen, aussen, laibung, enden)}), an den Ecken
    und unten bricht der Bruchstein durch (`frei` = {seite: [(u0, u1)]}: dort nicht, etwa hinter der Esse); Balkendecke mit
    Treppenloch `luke` und den Löchern `schlote`, die Dielen darüber kommen in `oben` (ohne Raum darüber: None, dann mit
    Abdeckung). Gibt die Höhe des Bodens darüber zurück."""
    kanten = ia.ecken(x0, x1, y0, y1)
    netz.alle = True
    for seite, angaben in waende.items():
        wand(netz, st, *kanten[seite], hoehe, stoff="Kalkputz", **angaben)
    netz.alle = False
    for i, (seite, angaben) in enumerate(waende.items()):
        ia.durchbruch(netz, st, *kanten[seite], hoehe, angaben.get("oeffnungen", ()), saat=saat + 17 * i,
                      frei=(frei or {}).get(seite, ()))
    return ia.balkendecke(decke, st, x0, x1, y0, y1, hoehe, luke=luke, schlote=schlote, oben=oben,
                          abdeckung=None if oben is not None else WAND, saat=saat)


# ─────────────────────────────────────────────────────────────────────────────
#  Einrichtung des Schmieds (örtliche Systeme: +y in den Raum)
# ─────────────────────────────────────────────────────────────────────────────

def q(netz, st, M, mitte, masse, stoff, ohne=()):
    netz.quader(mitte, masse, st[stoff], M, ohne=ohne)


def esse(netz, st, M, breite=1.6, tiefe=1.1, herd=0.75, hoehe=H_WERK, saat=0):
    """Esse an der Wand: gemauerter Herd mit Glut und Kohlen, Rauchfang aus verrußtem Lehm, Schlot in die Decke."""
    q(netz, st, M, (0, tiefe / 2, herd / 2), (breite, tiefe, herd), "Bruchstein dunkel", ohne=("-y", "+z"))
    r = 0.16
    q(netz, st, M, (0, tiefe - r / 2, herd + 0.05), (breite + 0.04, r, 0.1), "Bruchstein", ohne=("-z",))
    for s in (-1, 1):
        q(netz, st, M, (s * (breite / 2 - r / 2 + 0.01), (tiefe - r) / 2, herd + 0.05), (r + 0.02, tiefe - r, 0.1), "Bruchstein",
          ohne=("-z", "-y", "+y"))
    innen = breite / 2 - r
    netz.vieleck_nach([M @ Vector(p) for p in ((-innen, 0, herd), (innen, 0, herd), (innen, tiefe - r, herd), (-innen, tiefe - r, herd))],
                      (0, 0, 1), st["Glut"])
    zufall = random.Random(saat)
    for k in range(9):                       # Kohlen auf der Glut
        x, y = zufall.uniform(-innen + 0.1, innen - 0.1), zufall.uniform(0.12, tiefe - r - 0.1)
        g = zufall.uniform(0.06, 0.11)
        unten = [M @ Vector((x + sx * g, y + sy * g * 0.8, herd)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        spitze = M @ Vector((x + g * 0.2, y, herd + g * 0.8))
        for i in range(4):
            j = (i + 1) % 4
            netz.vieleck_nach([unten[i], unten[j], spitze], (unten[i] + unten[j]) / 2 - M @ Vector((x, y, herd - 0.2)), st["Kohle"])
    # Rauchfang: unten breit über dem Herd, oben schmal an der Wand
    z0, z1 = 1.85, 2.8
    bx, by, ox, oy = breite / 2 + 0.1, tiefe + 0.1, 0.45, 0.6
    ul, ur, ol, or_ = (M @ Vector(p) for p in ((-bx, by, z0), (bx, by, z0), (-ox, oy, z1), (ox, oy, z1)))
    wl, wr, wol, wor = (M @ Vector(p) for p in ((-bx, 0, z0), (bx, 0, z0), (-ox, 0, z1), (ox, 0, z1)))
    vorn = M.to_3x3() @ Vector((0, 1, 0.5))
    netz.vieleck_nach([ul, ur, or_, ol], vorn, st["Russ"])
    netz.vieleck_nach([wl, ul, ol, wol], M.to_3x3() @ Vector((-1, 0, 0.3)), st["Russ"])
    netz.vieleck_nach([ur, wr, wor, or_], M.to_3x3() @ Vector((1, 0, 0.3)), st["Russ"])
    q(netz, st, M, (0, by - 0.05, z0 - 0.06), (2 * bx + 0.1, 0.12, 0.14), "Fachwerk", ohne=("-y",))       # Rauchfangbalken
    for s in (-1, 1):
        q(netz, st, M, (s * (bx + 0.0), (by - 0.11) / 2, z0 - 0.06), (0.12, by - 0.11, 0.14), "Fachwerk", ohne=("-y", "+y"))
    q(netz, st, M, (0, oy / 2, (z1 + hoehe) / 2), (2 * ox, oy, hoehe - z1), "Russ", ohne=("-y", "+z", "-z"))


def blasebalg(netz, st, M, x0, s, laenge, breite, hoehe, z0, y):
    """Blasebalg liegend neben der Esse: Düse bei x0, nach s·x hin (s = ±1) lang, Leder zwischen zwei Brettern."""
    def P(x, yy, z):
        return M @ Vector((x0 + s * x, y + yy, z))
    b2 = breite / 2
    netz.quader((0, 0, 0), (1, 1, 1), st["Holz"], M @ Matrix.Translation((x0 + s * laenge / 2, y, z0 + 0.02))
                @ Matrix.Diagonal((laenge, breite, 0.04, 1)))
    oben_a, oben_b = z0 + 0.07, z0 + 0.04 + hoehe
    netz.stab(P(0.08, 0, oben_a + 0.02), P(laenge, 0, oben_b + 0.02), 0.04, breite, st["Holz"], quer=M.to_3x3() @ Vector((0, 1, 0)))
    aussen = M.to_3x3() @ Vector((s, 0, 0))
    for sy in (-1, 1):
        n = M.to_3x3() @ Vector((0, sy, 0))
        netz.vieleck_nach([P(0.08, sy * (b2 - 0.02), z0 + 0.04), P(laenge - 0.02, sy * (b2 - 0.02), z0 + 0.04),
                           P(laenge - 0.02, sy * (b2 - 0.02), oben_b), P(0.08, sy * (b2 - 0.02), oben_a)], n, st["Leder"])
    netz.vieleck_nach([P(laenge - 0.02, -b2 + 0.02, z0 + 0.04), P(laenge - 0.02, b2 - 0.02, z0 + 0.04),
                       P(laenge - 0.02, b2 - 0.02, oben_b), P(laenge - 0.02, -b2 + 0.02, oben_b)], aussen, st["Leder"])
    netz.stab(P(-0.08, 0, z0 + 0.06), P(0.1, 0, z0 + 0.06), 0.06, 0.06, st["Eisen"])            # Düse in den Herd
    return P


def werkstatt_werk(raum, st, teil, anker):
    """Grundausstattung des Werkraums (alle Stufen): Esse, Blasebalg, Amboss, Löschtrog, Kohle, Türrahmen."""
    M = rahmen(ESSE_X, WY1, 180)
    esse(raum, st, M, saat=3)
    anker.append(licht("Licht_1", M @ Vector((0, 0.5, 1.0)), (0, 0, 1), "Glut"))
    # kleiner Blasebalg rechts der Esse (örtlich −x), Hebel nach rechts; der Arbeiter steht am Griff
    P = blasebalg(raum, st, M, -0.8, -1, 0.62, 0.42, 0.3, 0.22, 0.75)
    raum.quader((0, 0, 0), (1, 1, 1), st["Bruchstein dunkel"], M @ Matrix.Translation((-1.1, 0.75, 0.11))
                @ Matrix.Diagonal((0.5, 0.36, 0.22, 1)))
    pfosten = P(0.7, 0, 0)
    raum.stab(pfosten, pfosten + Vector((0, 0, 1.58)), 0.1, 0.1, st["Holz"])
    griff = P(1.18, 0, 1.36)
    raum.stab(P(0.2, 0, 1.6), griff, 0.06, 0.06, st["Holz"])                      # Hebelstange über den Pfosten
    raum.stab(P(0.32, 0, 0.62), P(0.32, 0, 1.58), 0.025, 0.025, st["Eisen"])       # Zugstange zum oberen Brett
    anker.append(arbeit("Arbeit_2_Blasebalg", griff + Vector((0.42, 0, 0)), (-1, 0), 1))        # rechts vom Griff, Blick zur Esse
    # Amboss vor der Esse, Löschtrog rechts davon, Kohle unter dem Fenster
    amboss(teil("Bedienung_Produktion_Amboss"), st, AMBOSS)
    anker.append(arbeit("Arbeit_1_Haemmern", AMBOSS + Vector((0, 0.68, 0)), (0, -1), 1))     # Blick zur Kamera
    anker.append(bs.anker("Werkstueck_1", AMBOSS + Vector((-0.04, 0, AMBOSS_HOEHE))))
    trog(raum, st, Vector((0.85, 1.2, 0)), 0.6, 1.1, 0.62)
    schmied.kohlehaufen(raum, st, Vector((2.9, 2.75, 0)), 1.2, 0.8, 0.45)
    korb(raum, st, Vector((1.9, 1.6, 0)))
    # Türstock der Holztür zum Hof (Rückwand)
    tuerstock(raum, st, rahmen(HOLZTUER_X, WY1, 180), 0.95, 2.1)


AMBOSS_HOEHE = 0.86        # Oberkante der Bahn


def amboss(netz, st, fuss, drehung=-90):
    """Amboss auf einem Holzklotz mit Eisenreifen: Fuß, Taille, Bahn, Ferse und Horn (ohne Drehung Horn nach +y,
    mit −90° nach +x – so zeigt die Kamera den Umriss)."""
    bs.fass(netz, st, fuss, r=0.3, h=0.5, reifen=(0.4,), stoff="Holz alt")
    t = Matrix.Translation(fuss + Vector((0, 0, 0.5))) @ Matrix.Rotation(math.radians(drehung), 4, "Z")
    eisen = st["Eisen"]
    netz.prisma([(-0.18, 0.0), (0.18, 0.0), (0.13, 0.07), (-0.13, 0.07)], -0.12, 0.12, eisen, matrix=t)
    netz.prisma([(-0.12, 0.05), (0.12, 0.05), (0.07, 0.09), (0.07, 0.22), (0.2, 0.27), (0.2, 0.36), (-0.26, 0.36), (-0.26, 0.3),
                 (-0.07, 0.22), (-0.07, 0.09)], -0.085, 0.085, eisen, matrix=t)
    basis = [t @ Vector(p) for p in ((-0.075, 0.2, 0.275), (0.075, 0.2, 0.275), (0.075, 0.2, 0.36), (-0.075, 0.2, 0.36))]
    spitze = t @ Vector((0, 0.52, 0.335))
    mitte = t @ Vector((0, 0.3, 0.31))
    for i in range(4):
        a, b = basis[i], basis[(i + 1) % 4]
        netz.vieleck_nach([a, b, spitze], (a + b) / 2 - mitte, eisen)
    netz.vieleck_nach([t @ Vector(p) for p in ((-0.02, -0.2, 0.3605), (0.02, -0.2, 0.3605), (0.02, -0.16, 0.3605), (-0.02, -0.16, 0.3605))],
                      (0, 0, 1), st["Kohle"])                                  # Loch in der Ferse


def trog(netz, st, mitte, breite, laenge, hoehe, wasser=0.1):
    """Löschtrog aus Bohlen (entlang y), oben offen mit Wasser."""
    holz, d = st["Holz alt"], 0.06
    netz.quader((mitte.x, mitte.y, hoehe / 2), (breite, laenge, hoehe), holz, ohne=("+z",))
    for sx, sy, bx, by in ((-1, 0, d, laenge), (1, 0, d, laenge), (0, -1, breite - 2 * d, d), (0, 1, breite - 2 * d, d)):
        x = mitte.x + sx * (breite - d) / 2
        y = mitte.y + sy * (laenge - d) / 2
        netz.quader((x, y, hoehe + 0.0), (bx, by, 0.001), holz, ohne=("-z", "+x", "-x", "+y", "-y"))
    innen = [(mitte.x - breite / 2 + d, mitte.y - laenge / 2 + d), (mitte.x + breite / 2 - d, mitte.y - laenge / 2 + d),
             (mitte.x + breite / 2 - d, mitte.y + laenge / 2 - d), (mitte.x - breite / 2 + d, mitte.y + laenge / 2 - d)]
    zw = hoehe - wasser
    for i in range(4):
        (ax, ay), (bx, by) = innen[i], innen[(i + 1) % 4]
        n = Vector((mitte.x - (ax + bx) / 2, mitte.y - (ay + by) / 2, 0))
        netz.vieleck_nach([(ax, ay, zw), (bx, by, zw), (bx, by, hoehe), (ax, ay, hoehe)], n, holz)
    netz.vieleck_nach([(x, y, zw) for x, y in innen], (0, 0, 1), st["Wasser"])
    for f in (0.15, 0.85):           # Eisenbänder
        netz.quader((mitte.x, mitte.y - laenge / 2 + laenge * f, hoehe * 0.5), (breite + 0.02, 0.05, hoehe * 0.9), st["Eisen"],
                    ohne=("+z", "-z"))


def korb(netz, st, fuss):
    """Weidenkorb mit Holzkohle."""
    bs.achteck(netz, st, fuss, 0.26, 0.0, 0.42, "Holz alt")
    bs.rund_oben(netz, st, fuss, 0.24, 0.43, "Kohle")


def tuerstock(netz, st, M, b, h, stoff="Fachwerk"):
    """Türstock innen um eine Öffnung (Mitte unten auf der Innenseite): zwei Pfosten und ein Sturzbalken."""
    for s in (-1, 1):
        q(netz, st, M, (s * (b / 2 + 0.07), 0.03, h / 2), (0.14, 0.06, h + 0.04), stoff, ohne=("-y",))
    q(netz, st, M, (0, 0.04, h + 0.11), (b + 0.44, 0.08, 0.18), stoff, ohne=("-y",))


def bretttuer(netz, st, M, b, h, stoff="Hausfarbe", rueck=0.06, ring=True):
    """Brettertür in der Öffnung (Mitte unten auf der Innenseite), `rueck` hinter der Innenseite, mit Eisenbändern."""
    q(netz, st, M, (0, -rueck - 0.025, h / 2), (b - 0.02, 0.05, h - 0.01), stoff, ohne=("-y",))
    for k in range(1, 5):          # Fugen zwischen den Brettern
        u = -b / 2 + b * k / 5
        netz.vieleck_nach([M @ Vector(p) for p in ((u - 0.008, -rueck + 0.002, 0.02), (u + 0.008, -rueck + 0.002, 0.02),
                                                   (u + 0.008, -rueck + 0.002, h - 0.03), (u - 0.008, -rueck + 0.002, h - 0.03))],
                          M.to_3x3() @ Vector((0, 1, 0)), st["Fachwerk"])
    for f in (0.18, 0.82):
        q(netz, st, M, (-b * 0.06, -rueck + 0.008, h * f), (b * 0.8, 0.014, 0.07), "Eisen", ohne=("-y",))
    if ring:
        q(netz, st, M, (b * 0.32, -rueck + 0.02, h * 0.5), (0.07, 0.04, 0.09), "Eisen", ohne=("-y",))


def hoftor(netz, st):
    """Zweiflügeliges Hoftor in der linken Wand (Karren): Flügel in der Hausfarbe mit Querleisten und Streben,
    Bänder, Sperrbalken, Pfosten und Sturzbalken."""
    M = rahmen(WX0, HOFTOR_Y, -90)
    b, h, r = HOFTOR_B, HOFTOR_H, 0.12
    for s in (-1, 1):
        u = s * b / 4
        q(netz, st, M, (u, -r - 0.035, h / 2), (b / 2 - 0.02, 0.07, h - 0.01), "Hausfarbe", ohne=("-y",))
        for z in (0.35, 1.45, 2.55):
            q(netz, st, M, (u, -r + 0.02, z), (b / 2 - 0.14, 0.04, 0.14), "Fachwerk", ohne=("-y",))
        netz.stab(M @ Vector((u - s * (b / 4 - 0.12), -r + 0.025, 0.4)), M @ Vector((u + s * (b / 4 - 0.12), -r + 0.025, 1.4)),
                  0.12, 0.035, st["Fachwerk"], quer=M.to_3x3() @ Vector((0, 1, 0)), ohne=("-y",))
        for z in (0.5, 2.4):
            q(netz, st, M, (s * (b / 2 - 0.3), -r + 0.005, z), (0.5, 0.01, 0.06), "Eisen", ohne=("-y",))
        q(netz, st, M, (s * 0.12, -r + 0.06, 1.2), (0.06, 0.04, 0.22), "Eisen", ohne=("-y",))         # Halter des Balkens
    q(netz, st, M, (0, -r + 0.12, 1.2), (b - 0.3, 0.1, 0.12), "Holz")                                 # Sperrbalken
    for s in (-1, 1):
        q(netz, st, M, (s * (b / 2 + 0.11), 0.04, (h + 0.1) / 2), (0.22, 0.08, h + 0.1), "Fachwerk", ohne=("-y",))
    q(netz, st, M, (0, 0.05, h + 0.16), (b + 0.6, 0.1, 0.28), "Fachwerk", ohne=("-y",))


def pult(netz, st, M):
    """Schreibpult mit dem aufgeschlagenen Personalbuch, Tintenfass und Feder (örtlich, +y zum Leser)."""
    holz = st["Holz"]
    for x in (-0.3, 0.3):
        for y, z in ((0.12, 1.12), (0.55, 0.97)):
            netz.stab(M @ Vector((x, y, 0)), M @ Vector((x, y, z)), 0.06, 0.06, holz)
    q(netz, st, M, (0, 0.34, 0.25), (0.6, 0.5, 0.03), "Holz")                                       # Ablage unten
    for k in range(3):
        q(netz, st, M, (-0.2 + 0.16 * k, 0.32, 0.37), (0.12, 0.28, 0.21 - 0.03 * k), "Leder" if k % 2 else "Holz alt")
    neig = math.atan2(1.15 - 1.0, 0.5)
    platte = M @ Matrix.Translation((0, 0.34, 1.07)) @ Matrix.Rotation(-neig, 4, "X")
    netz.quader((0, 0, 0), (0.74, 0.56, 0.04), holz, platte)
    netz.quader((0, -0.25, 0.04), (0.74, 0.05, 0.05), holz, platte)                                  # Leiste oben
    netz.quader((0, 0.27, 0.04), (0.74, 0.03, 0.05), holz, platte)                                   # Leiste unten (hält das Buch)
    for s in (-1, 1):                                     # Buch: Deckel, darauf zwei leicht gewölbte Seiten mit Zeilen
        netz.quader((s * 0.155, 0.02, 0.03), (0.3, 0.4, 0.02), st["Leder"], platte)
        seite = platte @ Matrix.Translation((s * 0.15, 0.02, 0.05)) @ Matrix.Rotation(math.radians(-6 * s), 4, "Y")
        netz.quader((0, 0, 0), (0.27, 0.36, 0.025), st["Leinen"], seite)
        for k in range(7):
            netz.vieleck([seite @ Vector(p) for p in ((-0.1, -0.14 + 0.04 * k, 0.0135), (0.1 - 0.04 * (k % 3), -0.14 + 0.04 * k, 0.0135),
                                                     (0.1 - 0.04 * (k % 3), -0.13 + 0.04 * k, 0.0135), (-0.1, -0.13 + 0.04 * k, 0.0135))],
                         st["Kohle"])
    bs.achteck(netz, st, platte @ Vector((0.3, -0.24, 0.065)), 0.03, 0.0, 0.05, "Kohle")              # Tintenfass
    netz.stab(platte @ Vector((0.3, -0.24, 0.08)), platte @ Vector((0.34, -0.36, 0.26)), 0.012, 0.03, st["Leinen"])


def werkbank(netz, st, M):
    """Werkbank an der Wand (Länge entlang örtlich x): Platte, Beine, Ablage, Schraubstock, Werkzeug."""
    holz = st["Holz"]
    q(netz, st, M, (0, 0.36, 0.82), (1.8, 0.72, 0.08), "Holz")
    for x in (-0.82, 0.82):
        for y in (0.06, 0.64):
            netz.stab(M @ Vector((x, y, 0)), M @ Vector((x, y, 0.78)), 0.08, 0.08, holz)
        q(netz, st, M, (x, 0.35, 0.18), (0.06, 0.5, 0.06), "Holz")
    q(netz, st, M, (0, 0.35, 0.2), (1.6, 0.6, 0.03), "Holz alt")                                     # Ablage unten
    for k, (x, b) in enumerate(((-0.6, 0.4), (-0.15, 0.3), (0.4, 0.5))):                             # Eisen auf der Ablage
        q(netz, st, M, (x, 0.3 + 0.05 * k, 0.24), (b, 0.08, 0.05), "Eisen")
    # Schraubstock vorn rechts
    q(netz, st, M, (0.65, 0.72, 0.93), (0.1, 0.14, 0.16), "Eisen")
    q(netz, st, M, (0.65, 0.8, 0.93), (0.1, 0.03, 0.16), "Eisen")
    netz.stab(M @ Vector((0.65, 0.82, 0.9)), M @ Vector((0.65, 0.98, 0.9)), 0.025, 0.025, st["Eisen"])
    netz.stab(M @ Vector((0.56, 0.98, 0.9)), M @ Vector((0.74, 0.98, 0.9)), 0.02, 0.02, st["Eisen"])
    # Zange und Hammer auf der Platte
    for d in (-0.02, 0.02):
        netz.stab(M @ Vector((-0.55, 0.3 + d, 0.875)), M @ Vector((-0.2, 0.36 + d * 3, 0.875)), 0.015, 0.02, st["Eisen"])
    netz.stab(M @ Vector((0.05, 0.25, 0.875)), M @ Vector((0.35, 0.28, 0.875)), 0.025, 0.03, st["Holz alt"])
    q(netz, st, M, (0.36, 0.28, 0.885), (0.05, 0.12, 0.045), "Eisen")


def werkzeugwand(netz, st, M):
    """Werkzeugwand über der Werkbank: Brett mit Leiste und zwei Reihen Haken."""
    q(netz, st, M, (0, 0.02, 1.75), (1.7, 0.04, 1.2), "Holz alt", ohne=("-y",))
    q(netz, st, M, (0, 0.06, 2.37), (1.8, 0.06, 0.08), "Fachwerk", ohne=("-y",))
    for z in (2.2, 1.6):
        for k in range(9):
            x = -0.76 + 0.19 * k
            netz.stab(M @ Vector((x, 0.04, z)), M @ Vector((x, 0.13, z + 0.03)), 0.025, 0.025, st["Holz"])


HAKEN = [(-0.76 + 0.19 * k, z) for z in (2.2, 1.6) for k in range(9)]     # Haken der Werkzeugwand (x, z)


def zange(netz, st, M, x, z, laenge=0.45):
    """Zange am Haken: zwei Schenkel, oben gekreuzt."""
    for s in (-1, 1):
        y = 0.09 + 0.008 * s
        netz.stab(M @ Vector((x - s * 0.02, y, z + 0.02)), M @ Vector((x + s * 0.035, y - 0.01, z - laenge)), 0.016, 0.02, st["Eisen"])


def hammer_an_wand(netz, st, M, x, z, laenge=0.36, kopf=0.13):
    netz.stab(M @ Vector((x, 0.1, z + 0.02)), M @ Vector((x, 0.1, z - laenge)), 0.03, 0.03, st["Holz alt"])
    q(netz, st, M, (x, 0.1, z - laenge - 0.02), (kopf, 0.05, 0.05), "Eisen")


def gussformen(netz, st):
    """Gießerei: Formkästen mit Formsand, Barrenformen, Tiegel, Sandkiste in der Ecke hinten links."""
    for k in range(3):
        x, y, z = -4.15, 2.9, 0.1 + 0.21 * k
        netz.quader((x, y, z), (0.56 - 0.04 * k, 0.5, 0.2), st["Formsand"])
        netz.quader((x, y, z + 0.1 + 0.005), (0.6 - 0.04 * k, 0.54, 0.03), st["Holz alt"], ohne=("-z",))
    netz.quader((-3.65, 2.97, 0.2), (0.38, 0.45, 0.4), st["Holz alt"], ohne=("+z",))                 # Sandkiste
    netz.vieleck_nach([(-3.84, 2.75, 0.38), (-3.46, 2.75, 0.38), (-3.46, 3.19, 0.395), (-3.84, 3.19, 0.395)], (0, 0, 1), st["Formsand"])
    for k in range(3):                                                                                 # Barrenformen
        netz.quader((-4.2 + 0.3 * k, 2.3, 0.05), (0.24, 0.46, 0.1), st["Eisen"])
        netz.vieleck_nach([(-4.27 + 0.3 * k, 2.12, 0.101), (-4.13 + 0.3 * k, 2.12, 0.101), (-4.13 + 0.3 * k, 2.48, 0.101),
                           (-4.27 + 0.3 * k, 2.48, 0.101)], (0, 0, 1), st["Kohle"])
    # Tiegel (Achteckbecher) mit Gießzange
    bs.achteck(netz, st, Vector((-3.3, 2.2, 0)), 0.14, 0.0, 0.28, "Bruchstein dunkel")
    bs.rund_oben(netz, st, (-3.3, 2.2), 0.11, 0.281, "Kohle")
    for d in (-0.03, 0.03):
        netz.stab((-3.18, 2.2 + d, 0.25 + d / 3), (-2.7, 2.0 + d * 2, 0.03 + d / 3), 0.02, 0.02, st["Eisen"])


def schuerzen(netz, st):
    """Lederschürzen an Haken (Rückwand über den Gussformen)."""
    M = rahmen(-4.0, WY1, 180)
    q(netz, st, M, (0, 0.03, 2.05), (0.9, 0.06, 0.08), "Fachwerk", ohne=("-y",))
    for k, x in enumerate((-0.25, 0.22)):
        netz.stab(M @ Vector((x, 0.03, 2.0)), M @ Vector((x, 0.12, 2.02)), 0.02, 0.02, st["Eisen"])
        oben, unten = 1.97, 1.25 - 0.05 * k
        ecken = [(x - 0.16, 0.1, oben), (x + 0.16, 0.1, oben), (x + 0.24, 0.08 + 0.04 * k, unten), (x - 0.24, 0.08 + 0.04 * k, unten)]
        netz.vieleck_nach([M @ Vector(p) for p in ecken], M.to_3x3() @ Vector((0, 1, 0)), st["Leder"])
        netz.vieleck_nach([M @ (Vector(p) - Vector((0, 0.01, 0))) for p in ecken], M.to_3x3() @ Vector((0, -1, 0)), st["Leder"])


def schleifbock(netz, st, mitte):
    """Tretschleifbock: Schleifstein (Achse entlang y), Gestell, Wassertrog darunter, Tretbrett zum Arbeiter (−x)."""
    holz = st["Holz"]
    r, z = 0.38, 0.72
    for sy in (-1, 1):
        for dx in (-0.25, 0.25):
            netz.stab(mitte + Vector((dx, sy * 0.2, 0)), mitte + Vector((dx * 0.4, sy * 0.2, z)), 0.07, 0.07, holz)
        netz.stab(mitte + Vector((-0.3, sy * 0.2, 0.3)), mitte + Vector((0.3, sy * 0.2, 0.3)), 0.06, 0.06, holz)
    teile = 12
    ring = [(r * math.cos(2 * math.pi * k / teile), z + r * math.sin(2 * math.pi * k / teile)) for k in range(teile)]
    dreh = Matrix.Translation(mitte) @ Matrix.Rotation(math.radians(90), 4, "Z")      # Prisma-x → Welt-y (Achse)
    netz.prisma(ring, -0.07, 0.07, st["Fels Sandstein"], matrix=dreh, m_enden=st["Steinstaub"])
    netz.stab(mitte + Vector((0, -0.28, z)), mitte + Vector((0, 0.28, z)), 0.04, 0.04, st["Eisen"])
    netz.stab(mitte + Vector((0, -0.28, z)), mitte + Vector((0, -0.28, z - 0.22)), 0.03, 0.03, st["Eisen"])   # Kurbel
    trog(netz, st, mitte + Vector((0, 0, 0)), 0.5, 0.26, 0.28, wasser=0.06)
    netz.stab(mitte + Vector((0.0, -0.28, z - 0.22)), mitte + Vector((-0.3, -0.2, 0.08)), 0.03, 0.03, holz)     # Pleuel
    netz.quader(mitte + Vector((-0.3, -0.09, 0.05)), (0.3, 0.24, 0.04), holz)                                   # Tretbrett vor dem rechten Fuß
    netz.quader(mitte + Vector((0.48, 0.0, 0.42)), (0.3, 0.4, 0.04), holz)                                      # Ablage
    for sy in (-1, 1):
        netz.stab(mitte + Vector((0.48, sy * 0.15, 0)), mitte + Vector((0.48, sy * 0.15, 0.4)), 0.05, 0.05, holz)


def feinwerktisch(netz, st, M):
    """Goldschmiede: Feinwerktisch am Fenster mit Waage, Kästchen, Feilen, Gold; Hocker davor (örtlich +y)."""
    holz = st["Holz"]
    q(netz, st, M, (0, 0.32, 0.74), (1.2, 0.62, 0.06), "Holz")
    for x in (-0.54, 0.54):
        for y in (0.06, 0.58):
            netz.stab(M @ Vector((x, y, 0)), M @ Vector((x, y, 0.71)), 0.06, 0.06, holz)
    q(netz, st, M, (0, 0.65, 0.7), (0.3, 0.06, 0.02), "Leder")                                      # Fellschurz-Kante
    # Waage: Fuß, Säule, Balken, zwei Schalen
    bs.achteck(st=st, netz=netz, boden=M @ Vector((-0.35, 0.25, 0.77)), r=0.06, z0=0.0, z1=0.02, stoff="Eisen")
    netz.stab(M @ Vector((-0.35, 0.25, 0.79)), M @ Vector((-0.35, 0.25, 1.12)), 0.018, 0.018, st["Eisen"])
    netz.stab(M @ Vector((-0.53, 0.25, 1.1)), M @ Vector((-0.17, 0.25, 1.1)), 0.012, 0.015, st["Eisen"])
    for s in (-1, 1):
        x = -0.35 + s * 0.17
        netz.stab(M @ Vector((x, 0.25, 1.1)), M @ Vector((x, 0.25, 0.9)), 0.006, 0.006, st["Eisen"])
        bs.achteck(netz, st, M @ Vector((x, 0.25, 0.88)), 0.07, 0.0, 0.02, "Gold")
    q(netz, st, M, (0.0, 0.18, 0.8), (0.22, 0.16, 0.1), "Fachwerk")                                 # Kästchen
    q(netz, st, M, (0.0, 0.18, 0.855), (0.23, 0.17, 0.012), "Gold")
    for k in range(4):                                                                                # Goldstücke und Feilen
        q(netz, st, M, (0.25 + 0.06 * k, 0.42, 0.78), (0.03, 0.03, 0.015), "Gold")
        netz.stab(M @ Vector((0.2 + 0.07 * k, 0.2, 0.775)), M @ Vector((0.22 + 0.07 * k, 0.05, 0.775)), 0.012, 0.012, st["Eisen"])
    # Hocker
    bs.achteck(netz, st, M @ Vector((0, 0.95, 0.42)), 0.19, 0.0, 0.05, "Holz")
    for k in range(3):
        w = 2 * math.pi * k / 3
        netz.stab(M @ Vector((0.12 * math.cos(w), 0.95 + 0.12 * math.sin(w), 0)), M @ Vector((0.08 * math.cos(w), 0.95 + 0.08 * math.sin(w), 0.42)),
                  0.04, 0.04, holz)


def esse2(netz, st):
    """Schmiede: zweite, kleinere Esse an der Vorderwand links, mit Blasebalg."""
    M = rahmen(-2.6, WY0, 0)
    esse(netz, st, M, breite=1.3, tiefe=1.0, saat=7)
    P = blasebalg(netz, st, M, 0.65, 1, 0.5, 0.36, 0.26, 0.2, 0.55)
    netz.quader((0, 0, 0), (1, 1, 1), st["Bruchstein dunkel"], M @ Matrix.Translation((0.9, 0.55, 0.1))
                @ Matrix.Diagonal((0.42, 0.3, 0.2, 1)))
    netz.stab(P(0.58, 0, 0), P(0.58, 0, 1.45), 0.09, 0.09, st["Holz"])
    netz.stab(P(0.15, 0, 1.48), P(0.98, 0, 1.3), 0.05, 0.05, st["Holz"])
    netz.stab(P(0.26, 0, 0.55), P(0.26, 0, 1.46), 0.02, 0.02, st["Eisen"])
    bs.fass(netz, st, M @ Vector((-1.05, 0.5, 0)), r=0.25, h=0.55, reifen=(0.35,))
    bs.rund_oben(netz, st, M @ Vector((-1.05, 0.5, 0)), 0.23, 0.551, "Kohle")


def werkzeug1(netz, st, M):
    """Gekauft: Zangen- und Hammersatz an der Werkzeugwand (linke Hälfte)."""
    for k, (x, z) in enumerate(HAKEN):
        if x < 0:
            if z > 2:
                zange(netz, st, M, x, z, laenge=0.42 + 0.05 * (k % 2))
            else:
                hammer_an_wand(netz, st, M, x, z, laenge=0.3 + 0.04 * (k % 3), kopf=0.11 + 0.03 * (k % 2))


def werkzeug2(netz, st, M):
    """Gekauft (ab Stufe 2): feineres Werkzeug, Feilen, Durchschläge, kleine Hämmer (rechte Hälfte)."""
    for k, (x, z) in enumerate(HAKEN):
        if x > 0.01:
            if z > 2:
                netz.stab(M @ Vector((x, 0.1, z + 0.02)), M @ Vector((x, 0.1, z - 0.32)), 0.018, 0.012, st["Eisen"])
                netz.stab(M @ Vector((x, 0.1, z - 0.32)), M @ Vector((x, 0.1, z - 0.42)), 0.025, 0.025, st["Holz alt"])
            else:
                hammer_an_wand(netz, st, M, x, z, laenge=0.22, kopf=0.08)


def regal(netz, st, M, breite=2.4, hoehe=2.2, tiefe=0.45, boeden=4, saat=0):
    """Wandregal mit Waren (Säcke, Fässchen, Ballen, Eisenstangen) – örtlich, +y in den Raum."""
    for x in (-breite / 2, breite / 2):
        q(netz, st, M, (x, tiefe / 2, hoehe / 2), (0.05, tiefe, hoehe), "Holz", ohne=("-y",))
    zufall = random.Random(saat)
    for k in range(boeden):
        z = 0.12 + (hoehe - 0.2) * k / (boeden - 1)
        q(netz, st, M, (0, tiefe / 2, z), (breite - 0.05, tiefe, 0.04), "Holz", ohne=("-y",))
        if k == boeden - 1:
            continue
        x = -breite / 2 + 0.1
        while x < breite / 2 - 0.3:
            art = zufall.choice(("sack", "fass", "ballen", "eisen", "kiste"))
            b = zufall.uniform(0.22, 0.4)
            if x + b > breite / 2 - 0.05:
                break
            mitte = M @ Vector((x + b / 2, tiefe * 0.5, z + 0.02))
            if art == "sack":
                bs.achteck(netz, st, mitte, b / 2, 0.0, 0.32, "Leinen", n=6)
                bs.achteck(netz, st, mitte + Vector((0, 0, 0.32)), b / 4, 0.0, 0.06, "Seil", n=6)
            elif art == "fass":
                bs.fass(netz, st, mitte, r=b / 2 * 0.9, h=0.38, reifen=(0.08, 0.28))
            elif art == "ballen":
                q(netz, st, M, (x + b / 2, tiefe * 0.5, z + 0.15), (b, tiefe * 0.7, 0.26), zufall.choice(("Rose", "Kraut", "Lumpen")))
            elif art == "eisen":
                for j in range(3):
                    q(netz, st, M, (x + b / 2, tiefe * 0.5, z + 0.04 + 0.05 * j), (b, 0.06 + 0.07 * (2 - j), 0.05), "Eisen")
            else:
                q(netz, st, M, (x + b / 2, tiefe * 0.5, z + 0.12), (b, tiefe * 0.8, 0.2), "Holz alt")
            x += b + zufall.uniform(0.03, 0.1)


def kisten(netz, st, mitte):
    """Kistenstapel in der Ecke: Bretterkisten mit dunklen Kanten."""
    for (dx, dy, z, b, t, h) in ((0, 0, 0, 0.8, 0.7, 0.55), (0.0, 0.05, 0.55, 0.7, 0.6, 0.5), (-0.85, 0.05, 0, 0.7, 0.6, 0.6),
                                 (-0.85, 0.0, 0.6, 0.55, 0.5, 0.4), (0.05, -0.85, 0, 0.6, 0.6, 0.45)):
        c = mitte + Vector((dx, dy, z + h / 2))
        netz.quader(c, (b, t, h), st["Holz alt"])
        for sx in (-1, 1):
            netz.quader(c + Vector((sx * (b / 2 - 0.03), 0, 0)), (0.07, t + 0.012, h + 0.012), st["Fachwerk"], ohne=("-z",))
        netz.quader(c + Vector((0, 0, 0)), (b - 0.14, t + 0.008, 0.06), st["Fachwerk"], ohne=("-z", "+z", "+x", "-x"))


# ─────────────────────────────────────────────────────────────────────────────
#  Anker
# ─────────────────────────────────────────────────────────────────────────────

def drehung_von(richtung):
    """Grad um Z, so dass die Blickrichtung (Blender +Y des Ankers, im Spiel −Z) nach `richtung` (x, y) zeigt."""
    return math.degrees(math.atan2(-richtung[0], richtung[1]))


def arbeit(name, punkt, richtung, stufe):
    o = bs.anker(name, (punkt[0], punkt[1], 0.0), drehung_von(richtung))
    o["stufe"] = stufe
    return o


def licht(name, punkt, richtung, art, stufe=1):
    """Lichtquelle: „Glut“ (Esse, Punktlicht) oder „Fenster“ (Tageslicht, Blick −Z in den Raum)."""
    o = bs.anker(name, punkt, drehung_von(richtung[:2]) if (richtung[0] or richtung[1]) else 0.0)
    o["art"] = art
    o["stufe"] = stufe
    return o


def blick(name, auge, ziel):
    """Blickpunkt: im Spiel blickt die Kamera mit −Z (Blender +Y), oben ist +Y (Blender +Z)."""
    import bpy
    auge, ziel = Vector(auge), Vector(ziel)
    vor = (ziel - auge).normalized()
    rechts = vor.cross(Vector((0, 0, 1))).normalized()
    oben = rechts.cross(vor)
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "ARROWS"
    obj.matrix_world = Matrix((rechts, vor, oben)).transposed().to_4x4()
    obj.location = auge
    obj["blick"] = True
    bpy.context.scene.collection.objects.link(obj)
    return obj


# ─────────────────────────────────────────────────────────────────────────────
#  Bauen
# ─────────────────────────────────────────────────────────────────────────────

def bauen():
    bs.neue_szene()
    ia.RUSS_QUELLEN.clear()
    st = bs.Stoffe()
    werk, lager = bs.Netz("Raum_Werk"), bs.Netz("Raum_Lager", alle=True)
    decke_w, decke_l = bs.Netz("Decke_Werk", alle=True), bs.Netz("Decke_Lager", alle=True)
    kran, himmel = bs.Netz("Kranbalken", alle=True), bs.Netz("Himmel_Lager")
    teile = {}

    def teil(name):
        if name not in teile:
            teile[name] = bs.Netz(name)
        return teile[name]

    anker = []

    # ── Werkraum: Hülle (Kalkputz mit Bruchstein an Ecken und unten, Balkendecke mit Treppenloch) ─────────────
    f_vorne = [Oe(-0.4, 1.3, 0.9, 1.2), Oe(2.0, 1.3, 0.9, 1.2)]
    f_hinten = [Oe(3.3, 1.3, 0.9, 1.2)]
    f_rechts = [Oe(-2.5, 1.2, 0.9, 1.2)]
    holztuer = Oe(HOLZTUER_X, 0.0, 0.95, 2.1)
    hof = Oe(HOFTOR_Y, 0.0, HOFTOR_B, HOFTOR_H)
    steinraum(werk, decke_w, lager, st, WX0, WX1, WY0, WY1, H_WERK, {
        "vorne": dict(oeffnungen=f_vorne),
        "rechts": dict(oeffnungen=f_rechts),
        "hinten": dict(oeffnungen=f_hinten + [holztuer]),
        "links": dict(oeffnungen=[hof]),
    }, luke=LUKE, schlote=[SCHLOT], saat=5, frei={"hinten": [(WX1 - ESSE_X - 1.0, WX1 - ESSE_X + 0.9)]})
    kanten = ia.ecken(WX0, WX1, WY0, WY1)
    for seite, liste in (("vorne", f_vorne), ("rechts", f_rechts), ("hinten", f_hinten)):
        for o in liste:
            fenster(werk, st, *kanten[seite], o)
    for i, o in enumerate(f_hinten + f_rechts + f_vorne):
        if o in f_hinten:
            punkt, richtung = (o.s, WY1 - 0.1, o.v + o.h / 2), (0, -1, 0)
        elif o in f_rechts:
            punkt, richtung = (WX1 - 0.1, o.s, o.v + o.h / 2), (-1, 0, 0)
        else:
            punkt, richtung = (o.s, WY0 + 0.1, o.v + o.h / 2), (0, 1, 0)
        anker.append(licht(f"Licht_{i + 3}", punkt, richtung, "Fenster"))
    ia.russ(werk, st, *kanten["hinten"], WX1 - ESSE_X, 0.9, 1.85, H_WERK, f_hinten + [holztuer])     # Ruß über dem Rauchfang
    werkstatt_werk(werk, st, teil, anker)

    # ── Werkraum: Bedienung und Einrichtung ──────────────────────────────────
    pult(teil("Bedienung_Personal_Buch"), st, rahmen(WX0 + 0.05, -1.45, -90))
    M_bank = rahmen(WX1, 0.8, 90)
    werkbank(teil("Bedienung_Produktion_Werkbank"), st, M_bank)
    anker.append(bs.anker("Werkstueck_2", M_bank @ Vector((-0.2, 0.42, 0.865))))
    werkzeugwand(teil("Bedienung_Einrichten_Werkzeugwand"), st, M_bank)
    werkzeug1(teil("Einrichtung_Werkzeug1"), st, M_bank)
    werkzeug2(teil("Einrichtung_Werkzeug2"), st, M_bank)
    hoftor(teil("Bedienung_Karren_Hoftor"), st)
    M_holz = rahmen(HOLZTUER_X, WY1, 180)
    bretttuer(teil("Holztuer"), st, M_holz, holztuer.b, holztuer.h)
    eisentuer(teil("Einrichtung_Eisentuer"), st, M_holz, holztuer.b, holztuer.h)
    ia.treppe(teil("Tuer_Werk_Lager"), st, TREPPE_X, TREPPE_FUSS, TREPPE_OBEN, Z_LAGER)       # Treppe hinauf ins Lager
    grosser_blasebalg(teil("Einrichtung_Blasebalg"), st, rahmen(ESSE_X, WY1, 180))
    loeschfass(teil("Einrichtung_Loeschfass"), st, Vector((-1.88, 2.88, 0)))
    schuerzen(teil("Einrichtung_Schuerzen"), st)

    # ── Stufen (kumulativ): Boden, Ausstattung ────────────────────────────────
    M_fein = rahmen(WX1, -2.5, 90)
    SCHLEIF = Vector((1.7, -2.2, 0))
    stufen = [bs.Netz(f"Stufe{k}") for k in (1, 2, 3)]
    ziegel = 0
    for k, netz in enumerate(stufen, 1):
        if k < 3:
            ia.lehmboden(netz, st, WX0, WX1, WY0, WY1, saat=7)
        else:
            ziegel = ia.fischgraet(netz, st, WX0, WX1, WY0, WY1, saat=7)
        gussformen(netz, st)
        if k >= 2:
            esse2(netz, st)
            schleifbock(netz, st, SCHLEIF)
            ia.russ(netz, st, *kanten["vorne"], -2.6 - WX0, 0.75, 1.85, H_WERK, f_vorne)
        if k >= 3:
            feinwerktisch(netz, st, M_fein)
    print(f"Backsteinpflaster: {ziegel} Ziegel")
    anker.append(arbeit("Arbeit_3_Schleifen", SCHLEIF + Vector((-0.6, 0.0, 0)), (1, 0), 2))
    anker.append(arbeit("Arbeit_4_Feinarbeit", M_fein @ Vector((0, 0.95, 0)), (1, 0), 3))
    w3 = bs.anker("Werkstueck_3", SCHLEIF + Vector((0.48, 0.0, 0.445)))
    w3["stufe"] = 2
    w4 = bs.anker("Werkstueck_4", M_fein @ Vector((0.0, 0.5, 0.775)))
    w4["stufe"] = 3
    anker += [w3, w4, licht("Licht_2", rahmen(-2.6, WY0, 0) @ Vector((0, 0.45, 1.0)), (0, 0, 1), "Glut", stufe=2)]

    # ── Lager unter dem Dach ─────────────────────────────────────────────────
    z = Z_LAGER
    DACH.kniestoecke(lager, st, "Kalkputz")
    luke = Oe(LADELUKE_Y, 0.0, 1.2, 1.8)                      # Höhen über dem Dachboden
    gfenster = Oe(0.0, 2.55, 0.6, 0.6)
    DACH.giebel(lager, st, False, [gfenster], "Kalkputz", WAND)
    DACH.giebel(lager, st, True, [luke], "Kalkputz", WAND)
    for i, (seite, oeff) in enumerate((("vorne", []), ("hinten", []), ("links", [gfenster]), ("rechts", [luke]))):
        ia.durchbruch(lager, st, *kanten[seite], DACH.kniestock, oeff, saat=40 + 7 * i, boden=z, ecke_hoch=(0.8, 1.25),
                      frei=[(0.0, 0.9), (WX1 - SCHLOT[1] - 0.2, WX1 - SCHLOT[0] + 0.2)] if seite == "hinten" else
                      [(LUKE[2] - WY0 - 0.15, LUKE[3] - WY0 + 0.15)] if seite == "rechts" else ())        # nicht über der Bodenluke
    DACH.dach(decke_l, st, aussparen=[SCHLOT[:2], SCHLOT2[:2]])
    licht_luke = DACH.ladeluke(lager, kran, himmel, st, LADELUKE_Y, luke.b, luke.h, WAND)
    fenster(lager, st, *kanten["links"], Oe(gfenster.s, z + gfenster.v, gfenster.b, gfenster.h))
    schlot(lager, st, SCHLOT, vorn=False)
    schlot2 = bs.Netz("Schlot2", alle=True)                   # Schlot der zweiten Esse (ab Stufe 2), im Lager wie der erste
    schlot(schlot2, st, SCHLOT2, vorn=True)
    ia.bodenluke(teil("Tuer_Lager_Werk"), st, LUKE, z, gelaender_x=LUKE[0] - 0.05)
    n_licht = len([a for a in anker if a.name.startswith("Licht_")])
    anker.append(licht(f"Licht_{n_licht + 1}", licht_luke, (-1, 0, 0), "Fenster"))
    anker.append(licht(f"Licht_{n_licht + 2}", (WX0 + 0.1, gfenster.s, z + gfenster.v + gfenster.h / 2), (1, 0, 0), "Fenster"))
    regal(teil("Bedienung_Lager_Regal"), st, rahmen(WX0, 0.0, -90, z), saat=4)              # am linken Giebel
    kisten(teil("Bedienung_Lager_Kisten"), st, Vector((3.4, -2.0, z)))                       # vorn bei der Ladeluke
    for k, (x, b) in enumerate(((-3.78, 1.25), (-1.35, 1.4))):                              # niedrig unter der Dachschräge
        regal(teil("Einrichtung_Regale"), st, rahmen(x, WY0, 0, z), breite=b, hoehe=1.1, tiefe=0.4, boeden=3, saat=10 + k)
    nummer = 1
    for y in (1.5, 0.55):
        for x in (-1.6, -0.6, 0.4, 1.4):
            anker.append(bs.anker(f"Lager_{nummer}", (x, y, z)))
            nummer += 1

    # ── Blickpunkte ──────────────────────────────────────────────────────────
    anker += [blick(name, auge, ziel) for name, (auge, ziel) in BLICKE.items()]

    # ── Objekte, Zustände, Hierarchie ─────────────────────────────────────────
    raum_w, raum_l = werk.objekt(), lager.objekt()
    o_schlot2 = schlot2.objekt()
    o_schlot2["stufe"] = 2
    kinder = {raum_w: [decke_w.objekt()] + [s.objekt() for s in stufen],
              raum_l: [decke_l.objekt(), kran.objekt(), himmel.objekt(), o_schlot2]}
    for name, netz in teile.items():
        o = netz.objekt()
        im_lager = name in ("Bedienung_Lager_Regal", "Bedienung_Lager_Kisten", "Einrichtung_Regale", "Tuer_Lager_Werk")
        kinder[raum_l if im_lager else raum_w].append(o)
        if name.startswith("Einrichtung_"):
            bs.zustand(o)
    teile_obj = {o.name: o for liste in kinder.values() for o in liste}
    teile_obj["Tuer_Werk_Lager"]["schild"] = "Treppe zum Lager"
    teile_obj["Tuer_Lager_Werk"]["schild"] = "Treppe hinunter zum Werkraum"
    for a in anker:
        im_lager = a.name.startswith(("Lager_", "Blick_Lager")) or (a.name.startswith("Licht_") and a.location.z > Z_LAGER - 0.2)
        kinder[raum_l if im_lager else raum_w].append(a)
    for eltern, liste in kinder.items():
        for o in liste:
            o.parent = eltern
            o.matrix_parent_inverse = Matrix.Identity(4)
            if o.name == "Stufe1":
                o["zustand"] = True
            elif o.name in ("Stufe2", "Stufe3"):
                bs.zustand(o)
    return [raum_w, raum_l] + [o for liste in kinder.values() for o in liste]


def schlot(netz, st, r, vorn):
    """Verputzter Schlot im Dachboden über einem Rauchfang (r = x0, x1, y0, y1 an der Wand vorn oder hinten): vom Boden
    durch die Dachschräge."""
    x0, x1, y0, y1 = r
    z0, z1 = H_WERK, Z_LAGER + 2.4
    seite = y1 if vorn else y0                                  # Seite zum Raum
    netz.vieleck_nach([(x0, seite, z0), (x1, seite, z0), (x1, seite, z1), (x0, seite, z1)], (0, 1 if vorn else -1, 0), st["Schlot"])
    for x, n in ((x0, -1), (x1, 1)):
        netz.vieleck_nach([(x, y0, z0), (x, y1, z0), (x, y1, z1), (x, y0, z1)], (n, 0, 0), st["Schlot"])


def eisentuer(netz, st, M, b, h):
    """Gekauft: eisenbeschlagene Tür statt der Holztür (etwas weiter vorn, damit sich nichts deckt), mit Nieten."""
    rueck = 0.035
    q(netz, st, M, (0, -rueck - 0.03, (h - 0.03) / 2), (b - 0.05, 0.06, h - 0.03), "Eisen", ohne=("-y",))
    for f in (0.12, 0.38, 0.62, 0.88):
        q(netz, st, M, (0, -rueck + 0.008, h * f), (b - 0.06, 0.016, 0.08), "Kohle", ohne=("-y",))
    for s in (-1, 1):
        q(netz, st, M, (s * (b / 2 - 0.07), -rueck + 0.01, h / 2), (0.08, 0.02, h - 0.1), "Kohle", ohne=("-y",))
    for k in range(5):
        for j in range(3):
            q(netz, st, M, (-b / 2 + 0.2 + (b - 0.4) * j / 2, -rueck + 0.026, h * (0.12 + 0.19 * k)), (0.035, 0.012, 0.035), "Eisen",
              ohne=("-y",))
    q(netz, st, M, (b * 0.3, -rueck + 0.03, h * 0.5), (0.09, 0.05, 0.12), "Eisen", ohne=("-y",))


def grosser_blasebalg(netz, st, M):
    """Gekauft: großer Blasebalg auf Konsolen über dem kleinen, an der Wand rechts der Esse, mit Kette zum Hebel."""
    P = blasebalg(netz, st, M, -0.82, -1, 1.05, 0.55, 0.42, 1.02, 0.32)
    for x in (0.2, 0.85):
        netz.stab(P(x, -0.27, 1.0), P(x, 0.25, 1.0), 0.06, 0.08, st["Fachwerk"], quer=M.to_3x3() @ Vector((1, 0, 0)))
    netz.stab(P(0.02, 0.0, 1.07), P(-0.25, 0.0, 0.85), 0.07, 0.07, st["Eisen"])            # Rohr hinunter in den Herd
    netz.stab(P(0.55, 0.15, 1.5), P(0.55, 0.43, 1.62), 0.02, 0.02, st["Eisen"])             # Kette zum Hebel


def loeschfass(netz, st, fuss):
    """Gekauft: Löschfass mit Wasser und Eimer neben der Esse."""
    bs.fass(netz, st, fuss, r=0.32, h=0.85, reifen=(0.15, 0.68))
    bs.rund_oben(netz, st, fuss, 0.3, 0.8, "Wasser")
    eimer = fuss + Vector((0.42, -0.25, 0))
    bs.achteck(netz, st, eimer, 0.13, 0.0, 0.26, "Holz alt")
    bs.rund_oben(netz, st, eimer, 0.115, 0.261, "Wasser")
    netz.stab(eimer + Vector((-0.13, 0, 0.26)), eimer + Vector((0.13, 0, 0.26)), 0.015, 0.015, st["Eisen"])


def main():
    objekte = bauen()
    vorher, nachher = ia.farben_zusammenlegen(objekte)
    print(f"Materialien: {vorher} → {nachher}")
    ia.pruefbericht(objekte, NAME)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    rand = (WX0 - WAND, WX1 + WAND, WY0 - WAND, WY1 + WAND)
    ia.vorschau(NAME, list(BLICKE), STUFEN, "--schnell" in sys.argv, rand, rand, DACH, {"Werk"}, glut=260,
                fuell=[(0, 0, H_WERK - 0.4, 140, 1), (0, 0.3, Z_LAGER + 3.0, 90, 1)])


if __name__ == "__main__":
    main()
