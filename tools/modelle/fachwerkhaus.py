"""Fachwerkhaus – erste Stufe des Wohnhauses der Spielhäuser (8 × 10 m belegte Fläche).

    blender -b --python tools/modelle/fachwerkhaus.py   oder   python tools/modelle/fachwerkhaus.py

Erzeugt game/assets/modelle/fachwerkhaus.glb und drei Vorschaubilder in docs/modelle/.
Objekte: „Haus“, „Wappen“, „Fahne“ (Wappen und Fahne blendet das Spiel im Leerstand aus),
„Leerstand“ (Bretter vor Tür und Fenstern) und „Geruest“ (Baustelle) – beide standardmäßig aus.

Aufbau in Blender-Koordinaten (Z oben, Vorderseite -Y): Bruchsteinsockel, verputztes
Erdgeschoss mit Fachwerk, vorkragendes Obergeschoss auf Balkenköpfen, steiles Biberschwanzdach
mit Traufe zur Gasse, Gaube, Schornstein. Haus 6,5 × 6,5 m, dahinter 1,5 m Hof, davor 2 m Vorplatz.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as b  # noqa: E402

NAME = "fachwerkhaus"
FLAECHE = (8, 10)  # belegte Fläche X × Y (mit Vorplatz)


def bauen():
    szene = b.szene_leeren()
    m = b.standardmaterialien()
    haus = b.Bauteil("Haus")
    oeffnungen = []  # (Matrix, Oeffnung) für die Bretter des Leerstands

    # ── Maße ──────────────────────────────────────────────────────────────────
    y_vorn, y_hinten = -3.0, 3.5          # Erdgeschoss, Außenkanten
    x_halb = 3.25
    z_sockel, h_eg, h_og = 0.5, 2.9, 2.6
    z_eg = z_sockel
    z_og = z_eg + h_eg                     # 3,4
    z_traufe = z_og + h_og                 # 6,0
    krag = 0.3                             # Vorkragung des Obergeschosses vorn und seitlich
    xo_halb, yo_vorn = x_halb + krag, y_vorn - krag

    # ── Sockel und Putzkörper ─────────────────────────────────────────────────
    haus.quader((0, (y_vorn + y_hinten) / 2, z_sockel / 2), (2 * x_halb + 0.14, y_hinten - y_vorn + 0.14, z_sockel), m["Bruchstein"])
    haus.quader((0, (y_vorn + y_hinten) / 2, z_eg + h_eg / 2), (2 * x_halb, y_hinten - y_vorn, h_eg), m["Putz"])
    haus.quader((0, (yo_vorn + y_hinten) / 2, z_og + h_og / 2), (2 * xo_halb, y_hinten - yo_vorn, h_og), m["Putz"])
    # Balkenköpfe unter der Vorkragung
    x = -xo_halb + 0.3
    while x <= xo_halb - 0.3:
        haus.quader((x, yo_vorn + 0.25, z_og - 0.08), (0.16, 0.5 + krag, 0.16), m["Holz"])
        x += 1.0
    for s in (-1, 1):
        y = yo_vorn + 0.4
        while y <= y_hinten - 0.3:
            haus.quader((s * (xo_halb - 0.25), y, z_og - 0.08), (0.5 + krag, 0.16, 0.16), m["Holz"])
            y += 1.2
    # Stufe vor der Tür
    haus.quader((1.0, y_vorn - 0.28, z_sockel / 2), (1.7, 0.6, z_sockel), m["Bruchstein"])

    # ── Wände mit Fachwerk ────────────────────────────────────────────────────
    def wand(mitte, richtung, breite, hoehe, oeff, streben=True):
        matrix = b.wandmatrix(mitte, richtung)
        b.fachwerk(haus, matrix, breite, hoehe, oeff, m, streben=streben)
        for o in oeff:
            (b.tuer if o.art == "tuer" else b.fenster)(haus, matrix, o, m)
            oeffnungen.append((matrix, o))

    F = b.Oeffnung
    y_m = (y_vorn + y_hinten) / 2
    yo_m = (yo_vorn + y_hinten) / 2
    # Erdgeschoss (Schwelle liegt auf dem Sockel)
    wand((0, y_vorn, z_eg), "vorne", 2 * x_halb, h_eg,
         [F("tuer", 1.0, 0.0, 1.2, 2.2), F("fenster", -1.7, 0.95, 1.0, 1.1)])
    wand((x_halb, y_m, z_eg), "rechts", y_hinten - y_vorn, h_eg,
         [F("fenster", -1.6, 0.95, 0.9, 1.1), F("fenster", 1.4, 0.95, 0.9, 1.1)])
    wand((-x_halb, y_m, z_eg), "links", y_hinten - y_vorn, h_eg,
         [F("fenster", -1.4, 0.95, 0.9, 1.1), F("fenster", 1.6, 0.95, 0.9, 1.1)])
    wand((0, y_hinten, z_eg), "hinten", 2 * x_halb, h_eg,
         [F("fenster", -1.8, 0.95, 0.9, 1.1, laden=False, sprossen=False), F("fenster", 1.8, 0.95, 0.9, 1.1, laden=False, sprossen=False)], streben=False)
    # Obergeschoss
    wand((0, yo_vorn, z_og), "vorne", 2 * xo_halb, h_og,
         [F("fenster", -2.2, 0.85, 0.9, 1.1), F("fenster", 0.0, 0.85, 0.9, 1.1), F("fenster", 2.2, 0.85, 0.9, 1.1)])
    wand((xo_halb, yo_m, z_og), "rechts", y_hinten - yo_vorn, h_og,
         [F("fenster", -1.7, 0.85, 0.9, 1.1), F("fenster", 1.5, 0.85, 0.9, 1.1)])
    wand((-xo_halb, yo_m, z_og), "links", y_hinten - yo_vorn, h_og,
         [F("fenster", -1.5, 0.85, 0.9, 1.1), F("fenster", 1.7, 0.85, 0.9, 1.1)])
    wand((0, y_hinten, z_og), "hinten", 2 * xo_halb, h_og,
         [F("fenster", -2.0, 0.85, 0.9, 1.1, laden=False, sprossen=False), F("fenster", 2.0, 0.85, 0.9, 1.1, laden=False, sprossen=False)], streben=False)

    # ── Dach, Giebel, Gaube, Schornstein ──────────────────────────────────────
    neigung = 50
    y_first, z_first = b.satteldach(haus, m, -xo_halb, xo_halb, yo_vorn, y_hinten, z_traufe, neigung)
    for s, richtung in ((1, "rechts"), (-1, "links")):
        b.giebelwand(haus, m, s * xo_halb, richtung, yo_vorn, y_hinten, z_traufe, z_first, 0.3)
    # Gaube auf der Gassenseite: Putzkasten, Fenster, Walmkeil
    gx, gy = -1.2, -2.5
    z_dach_bei_gy = z_first - abs(gy - y_first) * math.tan(math.radians(neigung)) + 0.25 / math.cos(math.radians(neigung))
    g_h = 1.5
    haus.quader((gx, (gy + (-0.2)) / 2, z_dach_bei_gy - 0.5 + (g_h + 0.5) / 2), (1.5, -0.2 - gy, g_h + 0.5), m["Putz"])
    matrix = b.wandmatrix((gx, gy, z_dach_bei_gy), "vorne")
    o = F("fenster", 0.0, 0.35, 0.7, 0.8, laden=False, sprossen=False)
    for s in (-1, 1):  # Eckständer der Gaube
        haus.quader((s * (0.75 - b.BALKEN / 2), -b.VORSPRUNG + b.BALKENTIEFE / 2, g_h / 2), (b.BALKEN, b.BALKENTIEFE, g_h), m["Holz"], matrix)
    haus.quader((0, -b.VORSPRUNG + b.BALKENTIEFE / 2, o.v - b.BALKEN / 2), (1.5, b.BALKENTIEFE, b.BALKEN), m["Holz"], matrix)
    b.fenster(haus, matrix, o, m)
    oeffnungen.append((matrix, o))
    dach_oben = z_dach_bei_gy + g_h
    haus.prisma([(-0.95, dach_oben - 0.05), (0.95, dach_oben - 0.05), (0, dach_oben + 0.8)], 2.4, m["Ziegel"],
                b.Matrix.Translation(b.Vector((gx, gy - 0.3, 0))))
    # Schornstein auf der Rückseite
    haus.quader((-2.3, 1.8, 9.6), (0.7, 0.7, 3.4), m["Bruchstein"])
    haus.quader((-2.3, 1.8, 11.36), (0.95, 0.95, 0.12), m["Bruchstein"])

    # ── Wappen und Fahne in der Hausfarbe ─────────────────────────────────────
    wappen = b.Bauteil("Wappen")
    wappen.quader((1.0, yo_vorn - 0.12, z_og - 0.5), (0.7, 0.08, 0.8), m["Hausfarbe"])
    wappen.quader((1.0, yo_vorn - 0.06, z_og - 0.08), (0.12, 0.2, 0.08), m["Eisen"])
    fahne = b.Bauteil("Fahne")
    fahne.quader((2.6, y_first, z_first + 1.4), (0.1, 0.1, 3.0), m["Holz"])
    fahne.quader((2.6, y_first, z_first + 2.95), (0.18, 0.18, 0.12), m["Eisen"])
    fahne.quader((2.6 + 0.66, y_first, z_first + 2.5), (1.2, 0.04, 0.75), m["Hausfarbe"])

    # ── Zustände ──────────────────────────────────────────────────────────────
    leerstand = b.Bauteil("Leerstand")
    for matrix, o in oeffnungen:
        b.bretter(leerstand, matrix, o, m)
    geruest = b.Bauteil("Geruest")
    b.geruest(geruest, m, -xo_halb - 0.1, xo_halb + 0.1, yo_vorn, 7.2)

    objekte = [t.objekt() for t in (haus, wappen, fahne, leerstand, geruest)]
    for o in objekte[3:]:
        o["sichtbar"] = False  # Hinweis in den glTF-„extras“: Zustand, standardmäßig aus
    return objekte


def main():
    objekte = bauen()
    b.bericht(NAME, objekte)
    pfad = b.exportieren(NAME, objekte)
    print("geschrieben:", pfad)
    if "--ohne-bilder" in sys.argv:
        return
    b.vorschau_einrichten(groesse=20, flaeche=FLAECHE, schnell="--schnell" in sys.argv)
    zustaende = {
        NAME: lambda o: o.name not in ("Leerstand", "Geruest"),
        NAME + "-leerstand": lambda o: o.name not in ("Geruest", "Wappen", "Fahne"),
        NAME + "-geruest": lambda o: o.name != "Leerstand",
    }
    for bild, sichtbar in zustaende.items():
        print("Bild:", b.rendern(bild, sichtbar))


if __name__ == "__main__":
    main()
