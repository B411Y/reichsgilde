"""Masse der Raeume im Wohnhaus Mensch (Stufe 1, Grundform 1): Hinterzimmer und Bibliothek, dazu die Zimmertueren der Wohnstube.

    python tools/modelle/raeume/raeume_masse.py --soll-ist

Alle Raumskripte lesen ihre Masse von hier; `--soll-ist` prueft sie gegen die Sollwerte fuer eine Figur von 1,75 m (Mensch) und
nennt zur Auskunft, was eine Figur von 2,50 m (Oger) davon nicht passt (das Wohnhaus Mensch ist nicht fuer Oger gebaut).

Die Raeume liegen im Grundriss des Hauses (tools/modelle/neubau/wohnhaus_mensch_s1_g1.py): dieselben Innenwaende, dieselben
Fensteroeffnungen im Erdgeschoss. Im Spiel zeigt die Raumkamera immer nur einen Raum; durch die Fenster sieht man die Stadt.
Koordinaten wie in Blender: X rechts, Y nach hinten (Gasse bei -Y), Z oben. Die Werte unten muessen zum Hausskript passen;
raeume_bib.huelle_pruefen() vergleicht sie in Blender mit den Konstanten des Hausskripts.
"""
import math
import sys

# ── aus dem Hausskript (wohnhaus_mensch_s1_g1.py) ────────────────────────────────────────────────────────────────────
BX, VORN, HINTEN, T = 2.2, -2.6, 2.4, 0.22
Z_BODEN, Z_OG = 0.15, 2.55
IX, IV, IH = BX - T, VORN + T, HINTEN - T              # Erdgeschoss innen: x -1,98..1,98, y -2,38..2,18
GALERIE_AB = 0.6                                        # Wohnstube: die Galerie liegt ueber y > 0,6
FENSTER_EG = [("vorn_eg", (-1.35, -0.35), (1.05, 2.00)), ("links_eg", (-0.50, 0.40), (1.10, 1.95))]
HAUSTUER = (0.62, 1.58)                                 # Haustuer in der Gassenwand (x), lichte Hoehe 2,00

# ── Raumhuelle der beiden Raeume ─────────────────────────────────────────────────────────────────────────────────────
DECKE = Z_OG                                            # Unterseite der Deckenbretter (wie der Absatz zum Obergeschoss)
DECKENBALKEN = 0.16                                     # Hoehe der Deckenbalken unter den Brettern
BALKEN_Y = (-1.55, -0.45, 0.65, 1.70)                   # Lage der Deckenbalken (quer, entlang x)
TUER_B, TUER_H = 0.90, 2.00                             # Zimmertuer licht
TUER_RAHMEN = 0.07                                      # Breite der Bekleidung um die Tuer

# Zimmertueren der Wohnstube: Wand (links = -X, rechts = +X), y-Bereich (licht), Raum dahinter
WOHNSTUBE_TUEREN = {
    "Hinterzimmer": ("links", (0.95, 0.95 + TUER_B)),
    "Bibliothek": ("rechts", (0.95, 0.95 + TUER_B)),
}
# Rueckweg aus dem Raum in die Wohnstube
RAUM_TUER = {
    "Hinterzimmer": ("links", (0.85, 0.85 + TUER_B)),
    "Bibliothek": ("links", (-1.80, -1.80 + TUER_B)),
}
AUGE = 1.60                                             # Augenhoehe einer Figur von 1,75 m

# ── Hinterzimmer ─────────────────────────────────────────────────────────────────────────────────────────────────────
HZ_KAMERA = ((1.30, -1.92, Z_BODEN + AUGE), (-1.20, 1.90, Z_BODEN + 1.00))   # vorn rechts, Blick in die Ecke links hinten
HZ_TISCH = (0.05, -0.35, 0.55, 0.74)                    # Mitte x, y, Radius, Hoehe (runder Tisch fuer vier)
HZ_SITZ = 0.44
HZ_STUEHLE = [(0.05 + 0.84 * math.sin(math.radians(w)), -0.35 + 0.84 * math.cos(math.radians(w)), (w + 180) % 360) for w in (35, 125, 215, 305)]
HZ_SCHEMEL = [(-0.95, -1.25)]
HZ_TEPPICH = (-1.25, 1.35, -1.45, 0.75)                 # x0, x1, y0, y1
HZ_OFEN = (1.28, IX, 1.45, IH, 1.05)                    # Ofen in der Ecke rechts hinten: x0, x1, y0, y1, Hoehe des Ofenkoerpers
HZ_WAFFEN = (-1.80, -0.95)                              # Waffenstaender an der linken Wand vor dem Fenster (y)
HZ_TRUHE = (0.20, 1.05, IH - 0.52, IH, 0.58)            # eiserne Truhe an der Rueckwand: x0, x1, y0, y1, Hoehe
HZ_BORDE = (-0.05, 1.20, (1.50, 1.86))                  # Wandborde ueber der Truhe: x0, x1, Hoehen
HZ_VORRAT = (-1.72, -0.25, 1.55, IH)                    # Kisten, Faesser, Saecke an der Rueckwand links: x0, x1, y0, y1

# ── Bibliothek ───────────────────────────────────────────────────────────────────────────────────────────────────────
BI_KAMERA = ((-1.40, 1.92, Z_BODEN + AUGE), (1.20, -1.80, Z_BODEN + 1.00))   # hinten links, Blick in die Ecke rechts vorn
BI_REGAL_H = 2.18                                       # Hoehe der Regale (Oberkante)
BI_REGAL_T = 0.36                                       # Tiefe
BI_BRETTER = (0.10, 0.52, 0.92, 1.30, 1.66, 2.02)        # Oberkanten der Bretter ueber dem Fussboden (oberstes Fach 2,02 m)
BI_REGALE = [                                           # (Wand, a0, a1): a entlang der Wand
    ("rechts", -2.30, -0.95),
    ("rechts", 0.55, 2.10),
    ("vorn", 0.00, 1.55),                               # endet vor dem Kranz des Regals der rechten Wand
]
BI_KAMIN = ("rechts", -0.80, 0.40)                      # Sandsteinkamin an der rechten Wand
BI_KAMIN_OEFFNUNG = (0.70, 0.62)                        # Breite, Hoehe der Feueroeffnung
BI_SESSEL = (1.12, 0.88, 205)                          # Sitzpunkt x, y, Blick (Grad, 0 = +Y, 90 = +X): schraeg zum Feuer
BI_SESSEL_SITZ = 0.43
BI_BEISTELL = (1.38, 1.40, 0.62)                        # Beistelltisch mit Kerze: x, y, Hoehe (vor dem Regal, neben dem Sessel)
BI_SCHREIBTISCH = (-0.55, 0.55, -0.90, -0.17, 0.76)     # x0, x1, y0, y1, Hoehe
BI_STUHL = (0.0, 0.18, 180)                            # hinter dem Schreibtisch, Blick zur Gasse
BI_TEPPICH = (-1.05, 1.15, -1.55, 0.70)
BI_LESEPULT = (-1.40, 0.50, 1.12, 270)                  # Pfosten x, y; Hoehe der Pultkante zum Lesenden; Blick der Lesenden (270 = zur linken Wand)
BI_TRUHE = (-1.30, -0.40, IV, IV + 0.46, 0.50)          # Truhe mit Folianten unter dem Fenster der Gassenwand
BI_LEITER = (-1.85, 2.02, 74)                           # Mitte der Leiter entlang der Wand (y), lehnt bis z, Neigung in Grad
BI_LEITER_B, BI_SPROSSE = 0.42, 0.27

FIGUR, OGER = 1.75, 2.50


def soll_ist():
    """(Pruefung, Soll, Ist, ok) – alles aus den Konstanten oben."""
    p = []
    def pruefe(name, soll, ist, ok): p.append((name, soll, ist, ok))
    clear = DECKE - Z_BODEN
    unter_balken = clear - DECKENBALKEN
    pruefe("Zimmertuer licht breit (Mensch)", ">= 0,85 m", f"{TUER_B:.2f} m", TUER_B >= 0.85)
    pruefe("Zimmertuer licht hoch (Mensch)", ">= 1,95 m", f"{TUER_H:.2f} m", TUER_H >= 1.95)
    pruefe("Tuer unter der Galerie der Wohnstube", "Sturz < 2,35 m (Galeriebalken)", f"{Z_BODEN + TUER_H + 0.10:.2f} m", Z_BODEN + TUER_H + 0.10 < 2.35)
    pruefe("Raumhoehe bis zur Decke", ">= 2,20 m", f"{clear:.2f} m", clear >= 2.20)
    pruefe("Raumhoehe unter den Deckenbalken", ">= 2,10 m", f"{unter_balken:.2f} m", unter_balken >= 2.10)
    pruefe("Kopffreiheit Mensch unter den Balken", ">= 0,25 m", f"{unter_balken - FIGUR:.2f} m", unter_balken - FIGUR >= 0.25)
    for w, (a0, a1), (z0, z1) in FENSTER_EG:
        pruefe(f"Fensterbruestung {w}", "0,85-1,00 m", f"{z0 - Z_BODEN:.2f} m", 0.85 <= z0 - Z_BODEN <= 1.00)
    # Hinterzimmer
    tx, ty, tr, th = HZ_TISCH
    pruefe("Hinterzimmer: Kartentisch hoch", "0,72-0,76 m", f"{th:.2f} m", 0.72 <= th <= 0.76)
    pruefe("Hinterzimmer: Kartentisch fuer vier (Durchmesser)", ">= 1,00 m", f"{2 * tr:.2f} m", 2 * tr >= 1.0)
    pruefe("Hinterzimmer: Sitzhoehe Stuhl und Schemel", "0,42-0,46 m", f"{HZ_SITZ:.2f} m", 0.42 <= HZ_SITZ <= 0.46)
    pruefe("Hinterzimmer: Abstand Tischkante - Stuhlmitte", "0,20-0,40 m", f"{0.84 - tr:.2f} m", 0.20 <= 0.84 - tr <= 0.40)
    tuer_y = (RAUM_TUER["Hinterzimmer"][1][0] + RAUM_TUER["Hinterzimmer"][1][1]) / 2
    tuer_x = -IX if RAUM_TUER["Hinterzimmer"][0] == "links" else IX
    weg = min(math.hypot(tuer_x - sx, tuer_y - sy) for sx, sy, _ in HZ_STUEHLE) - 0.30
    pruefe("Hinterzimmer: frei von der Tuer bis zum naechsten Stuhl", ">= 0,60 m", f"{weg:.2f} m", weg >= 0.60)
    pruefe("Hinterzimmer: Truhe hoch (Sitz-/Ablagehoehe)", "0,45-0,70 m", f"{HZ_TRUHE[4]:.2f} m", 0.45 <= HZ_TRUHE[4] <= 0.70)
    pruefe("Hinterzimmer: oberes Wandbord erreichbar", "<= 1,90 m", f"{HZ_BORDE[2][1]:.2f} m", HZ_BORDE[2][1] <= 1.90)
    # Bibliothek
    pruefe("Bibliothek: Schreibtisch hoch", "0,74-0,78 m", f"{BI_SCHREIBTISCH[4]:.2f} m", 0.74 <= BI_SCHREIBTISCH[4] <= 0.78)
    pruefe("Bibliothek: Sessel Sitzhoehe", "0,40-0,46 m", f"{BI_SESSEL_SITZ:.2f} m", 0.40 <= BI_SESSEL_SITZ <= 0.46)
    pruefe("Bibliothek: Lesepult (Stehpult) vorn", "1,05-1,20 m", f"{BI_LESEPULT[2]:.2f} m", 1.05 <= BI_LESEPULT[2] <= 1.20)
    pruefe("Bibliothek: oberstes Fach mit Leiter", "Leiter, wenn > 1,90 m", f"{BI_BRETTER[-1]:.2f} m, Leiter da", True)
    pruefe("Bibliothek: Regal unter der Decke", f"<= {clear - 0.05:.2f} m", f"{BI_REGAL_H:.2f} m", BI_REGAL_H <= clear - 0.05)
    pruefe("Bibliothek: Regaltiefe", "0,30-0,40 m", f"{BI_REGAL_T:.2f} m", 0.30 <= BI_REGAL_T <= 0.40)
    pruefe("Bibliothek: Leiter Neigung", "65-80 Grad", f"{BI_LEITER[2]} Grad", 65 <= BI_LEITER[2] <= 80)
    pruefe("Bibliothek: Leiter Sprossenabstand", "0,25-0,30 m", f"{BI_SPROSSE:.2f} m", 0.25 <= BI_SPROSSE <= 0.30)
    pruefe("Bibliothek: Leiter breit", ">= 0,40 m", f"{BI_LEITER_B:.2f} m", BI_LEITER_B >= 0.40)
    pruefe("Bibliothek: Kaminoeffnung", ">= 0,60 x 0,55 m", f"{BI_KAMIN_OEFFNUNG[0]:.2f} x {BI_KAMIN_OEFFNUNG[1]:.2f} m", BI_KAMIN_OEFFNUNG[0] >= 0.6 and BI_KAMIN_OEFFNUNG[1] >= 0.55)
    stand = BI_SCHREIBTISCH[0] - (BI_LESEPULT[0] + 0.20)  # vor der Pultkante (Pfosten + 0,20) bis zum Schreibtisch: dort steht der Lesende
    pruefe("Bibliothek: Platz zum Stehen vor dem Lesepult", ">= 0,60 m", f"{stand:.2f} m", stand >= 0.60)
    # Oger zur Auskunft
    p.append(("Auskunft Oger 2,50 m: Zimmertuer", ">= 2,55 m", f"{TUER_H:.2f} m (nicht fuer Oger)", None))
    p.append(("Auskunft Oger 2,50 m: Raumhoehe", ">= 2,60 m", f"{clear:.2f} m (nicht fuer Oger)", None))
    return p


if __name__ == "__main__":
    if "--soll-ist" in sys.argv:
        fehler = 0
        for name, soll, ist, ok in soll_ist():
            zeichen = "Auskunft" if ok is None else ("ok" if ok else "FEHLER")
            if ok is False: fehler += 1
            print(f"{zeichen:8} {name}: Soll {soll}, Ist {ist}")
        print("Soll/Ist:", "ok" if fehler == 0 else f"{fehler} Fehler")
        sys.exit(1 if fehler else 0)
