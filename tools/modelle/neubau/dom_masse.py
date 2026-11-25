"""Neubau Dom katholisch: alle Masse an einer Stelle (Aussen, Innenraum, Oeffnungen, Plaetze) - reines Python.

    python tools/modelle/neubau/dom_masse.py [--soll-ist]

Die Blender-Skripte (dom_katholisch_aussen.py, dom_katholisch_innen.py) lesen ihre Masse von hier; damit sind innen und aussen
dieselben Waende und Oeffnungen ("innen = aussen minus Wandstaerke"). --soll-ist rechnet die Pruefungen aus (Figurenmasse aller
Voelker bis zum Oger, Tuer, Gaenge, Baenke, Flaeche) und gibt eine Tabelle aus. Was nur aus der fertigen GLB zu messen ist
(Dreiecke, Lage der Scheiben und Anker), prueft dom_messen.py.

Bauform nach der Vorlage "Dom katholisch": dreischiffige Basilika mit Doppelturmfassade im Westen, Querhaus, Chor mit
5/10-Schluss, Strebepfeilern und Strebeboegen ueber den Seitenschiffen, grauer Werkstein mit gelblichem Sandstein.

Koordinaten wie bei allen Bauskripten: X rechts, Y nach hinten (Portal bei -Y, im Spiel +Z; der Chor bei +Y), Z oben; Ursprung
in der Mitte der belegten Flaeche (FLAECHE).
"""
import math
import sys

# ── Figuren (wie rathaus_masse.py) ───────────────────────────────────────────────────────────────────────────────────
FIGUREN = {
    "Halbling": (1.10, 0.32), "Zwerg": (1.35, 0.46), "Mensch": (1.75, 0.46), "Elb": (1.85, 0.44),
    "Ork": (1.93, 0.52), "Oger": (2.50, 0.66),
}
GROESSTE = max(h for h, _ in FIGUREN.values())
BREITESTE = max(b for _, b in FIGUREN.values())

FLAECHE = (20.0, 38.0)   # belegte Flaeche (Breite x Tiefe) fuer den Katalog; Paket 3 setzt sie in die Karten
BUDGET = (8000, 17000)   # Dreiecke der Huelle (Style-Guide: Monument 8.000-15.000; Nutzer 25.11.2026: Optik vor Optimierung, Schieferlage 0,30 m, bis 17.000)

# ── Hoehen ───────────────────────────────────────────────────────────────────────────────────────────────────────────
Z_SOCKEL = 0.30          # Fussboden (drei Stufen vor dem Portal)
Z_CHOR = 0.75            # Fussboden des Chors (drei Stufen hoeher)
Z_ALTAR = 1.05           # Podest des Hochaltars
Z_SEITE = 6.80           # Oberkante der Seitenschiffwaende (Traufe der Pultdaecher)
Z_PULT = 9.20            # Anschluss der Pultdaecher an die Hochschiffwand
Z_TRAUFE = 14.20         # Traufe des Hochschiffs, des Querhauses und des Chors
NEIGUNG = math.tan(math.radians(55))
Z_KAEMPFER = 9.90        # Kaempfer der Gewoelbe
Z_SCHEITEL = 13.90       # Scheitel der Gewoelbe (4,00 ueber dem Kaempfer)
Z_ARKADE = (5.20, 7.60)  # Kaempfer und Scheitel der Arkadenboegen
Z_SEITE_DECKE = (6.50, 8.90)   # Decke der Seitenschiffe (folgt dem Pultdach): an der Aussenwand, an der Arkade

# ── Grundriss ────────────────────────────────────────────────────────────────────────────────────────────────────────
T_WAND = 0.70            # alle Mauern ausser den Tuermen
T_TURM = 0.90
X_MITTE = 3.50           # halbe lichte Breite des Mittelschiffs (7,00 m)
X_ARKADE = X_MITTE + T_WAND      # 4,20: Aussenflaeche der Hochschiffwand
X_SEITE = 7.20           # Innenflaeche der Seitenschiffwand (Seitenschiff 3,00 m licht)
X_AUSSEN = X_SEITE + T_WAND      # 7,90
X_QUER = 9.40            # Aussenflaeche der Querhausgiebel
Y_WEST = -17.50          # Westflaeche der Tuerme
Y_WESTWAND = -17.00      # Westwand zwischen den Tuermen (0,50 hinter den Tuermen)
Y_TURM = -11.90          # Ostflaeche der Tuerme = Beginn des Langhauses
JOCH = 4.20
JOCHE = 4
Y_QUER = (Y_TURM + JOCHE * JOCH, Y_TURM + JOCHE * JOCH + 5.60)     # 4,90 ... 10,50: Aussenflaechen des Querhauses
Y_APSIS = 13.50          # Mitte des Chorschlusses
R_APSIS = 4.20           # Umkreis der Aussenflaeche (5 Seiten von 10)
R_APSIS_INNEN = 3.50
X_TURM = (2.30, X_AUSSEN)        # Turm rechts (links gespiegelt): 5,60 x 5,60
PFEILER_Y = tuple(round(Y_TURM + JOCH * i, 2) for i in range(1, JOCHE))   # Rundpfeiler der Arkaden: -7,70; -3,50; 0,70
PFEILER_R = 0.42
PFEILER_X = X_MITTE + T_WAND / 2       # die Pfeiler stehen in der Achse der Hochschiffwand (3,85)
VIERUNG_R = 0.55         # Vierungspfeiler bei Y_QUER
Z_FIRST = Z_TRAUFE + (X_ARKADE + 0.30) * NEIGUNG


def apsis_ecke(k, r=R_APSIS):
    """Ecke k (0 rechts ... 5 links) des Chorschlusses."""
    w = math.radians(36 * k)
    return (r * math.cos(w), Y_APSIS + r * math.sin(w))


# ── Tuerme ───────────────────────────────────────────────────────────────────────────────────────────────────────────
TURM_STUFEN = (0.0, 7.4, 13.8, 20.2)     # Unterkanten der Geschosse (Gesimse dazwischen); je Geschoss 0,12 eingezogen
Z_GLOCKEN = (20.2, 25.6)                 # Glockenstube
Z_HELM = 25.6                            # Fuss des Helms (Wimperge bis 28,6)
Z_SPITZE = 40.0                          # Helmspitze; Kreuz bis 42,4


def durchsicht_erlaubt(p):
    """Punkt p (x, y, z) im dunklen Hohlraum der Glockenstube (hinter den Schallöffnungen, mind. 0,40 m hinter der Aussenflaeche des Geschosses):
    dort treffen Blicke durch die Oeffnungen die Rueckseiten der nach innen gewendeten Hohlraumflaechen (dom_geometrie.py, wie beim Dom evangelisch)."""
    x, y, z = abs(p[0]), p[1], p[2]
    e = 0.36 + 0.40
    return (Z_GLOCKEN[0] + 0.5 <= z <= Z_GLOCKEN[1] - 0.2 and X_TURM[0] + e <= x <= X_TURM[1] - e and Y_WEST + e <= y <= Y_TURM - e)
Z_KREUZ = 42.4

# ── Aussenwaende: Name -> (Anfang, Ende, Dicke); die Aussenseite liegt rechts der Laufrichtung Anfang -> Ende ──────────
# (im Grundriss von oben gesehen; so laufen alle Waende im Uhrzeigersinn um den Bau).


def _waende():
    w = {}
    yq0, yq1 = Y_QUER
    w["seite_l"] = ((-X_AUSSEN, Y_TURM), (-X_AUSSEN, yq0), T_WAND)
    w["quer_l_w"] = ((-X_AUSSEN, yq0), (-X_QUER, yq0), T_WAND)       # nur der vorspringende Teil; darueber siehe quer_l_w_oben
    w["quer_l_g"] = ((-X_QUER, yq0), (-X_QUER, yq1), T_WAND)
    w["quer_l_o"] = ((-X_QUER, yq1), (-X_ARKADE, yq1), T_WAND)
    w["chor_l"] = ((-X_ARKADE, yq1), (-X_ARKADE, Y_APSIS), T_WAND)
    for k in range(5):
        w[f"apsis_{k}"] = (apsis_ecke(5 - k), apsis_ecke(4 - k), (R_APSIS - R_APSIS_INNEN) * math.cos(math.radians(18)))
    w["chor_r"] = ((X_ARKADE, Y_APSIS), (X_ARKADE, yq1), T_WAND)
    w["quer_r_o"] = ((X_ARKADE, yq1), (X_QUER, yq1), T_WAND)
    w["quer_r_g"] = ((X_QUER, yq1), (X_QUER, yq0), T_WAND)
    w["quer_r_w"] = ((X_QUER, yq0), (X_AUSSEN, yq0), T_WAND)
    w["seite_r"] = ((X_AUSSEN, yq0), (X_AUSSEN, Y_TURM), T_WAND)
    w["west"] = ((X_TURM[0], Y_WESTWAND), (-X_TURM[0], Y_WESTWAND), T_WAND)
    # Hochschiff (Obergaden) ueber den Pultdaechern
    w["ober_l"] = ((-X_ARKADE, Y_TURM), (-X_ARKADE, yq0), T_WAND)
    w["ober_r"] = ((X_ARKADE, yq0), (X_ARKADE, Y_TURM), T_WAND)
    w["quer_l_w_oben"] = ((-X_ARKADE, yq0), (-X_AUSSEN, yq0), T_WAND)
    w["quer_r_w_oben"] = ((X_AUSSEN, yq0), (X_ARKADE, yq0), T_WAND)
    return w


def wand(name):
    """(Ursprung, Richtung, Laenge, Aussennormale, Dicke) einer Aussenwand."""
    (ax, ay), (bx, by), d = WAENDE[name]
    l = math.hypot(bx - ax, by - ay)
    ex, ey = (bx - ax) / l, (by - ay) / l
    return (ax, ay), (ex, ey), l, (-ey, ex), d       # Aussen = links der Laufrichtung bei Umlauf im Uhrzeigersinn von oben


WAENDE = _waende()

# ── Oeffnungen: (Wand, s0, s1, z0, z1, Art, Steilheit) - s entlang der Wand ab ihrem Anfang ───────────────────────────
# Art "spitz": Spitzbogenfenster (Scheitel bei z1, Bogenhoehe = Steilheit x halbe Breite), "portal": Tuer, "rose": Rundfenster.
FENSTER_SEITE = (1.30, 2.20, 5.80)       # Breite, Bruestung, Scheitel
FENSTER_OBER = (1.40, 10.20, 13.20)
FENSTER_CHOR = (1.20, 3.60, 9.60)          # Scheitel unter dem Gewoelbekaempfer (der Chorschluss hat keine Stichkappen)
FENSTER_QUER = (2.40, 4.20, 12.40)
PORTAL = (2.40, Z_SOCKEL, 3.90)          # Breite, Schwelle, Sturz (lichte Hoehe 3,60)
ROSE = (0.0, 10.60, 1.70)                # x, z der Mitte, Radius
STEIL = 1.25


def _oeffnungen():
    o = []
    mitte = lambda name: wand(name)[2] / 2
    for seite in ("seite_l", "seite_r", "ober_l", "ober_r"):
        b, z0, z1 = FENSTER_SEITE if seite.startswith("seite") else FENSTER_OBER
        for i in range(JOCHE):
            s = (i + 0.5) * JOCH
            o.append((seite, s - b / 2, s + b / 2, z0, z1, "spitz", STEIL))
    for name in ("quer_l_g", "quer_r_g"):
        b, z0, z1 = FENSTER_QUER
        o.append((name, mitte(name) - b / 2, mitte(name) + b / 2, z0, z1, "spitz", STEIL))
    for name in ("chor_l", "chor_r") + tuple(f"apsis_{k}" for k in range(5)):
        b, z0, z1 = FENSTER_CHOR
        o.append((name, mitte(name) - b / 2, mitte(name) + b / 2, z0, z1, "spitz", STEIL))
    b, z0, z1 = PORTAL
    o.append(("west", mitte("west") - b / 2, mitte("west") + b / 2, z0, z1, "portal", 0.0))
    o.append(("west", mitte("west") - ROSE[2], mitte("west") + ROSE[2], ROSE[1] - ROSE[2], ROSE[1] + ROSE[2], "rose", 0.0))
    return o


OEFFNUNGEN = _oeffnungen()


def punkt(name, s, z, raus=0.0):
    """Punkt auf der Aussenflaeche einer Wand, um `raus` nach aussen (negativ: nach innen) versetzt."""
    (ox, oy), (ex, ey), _, (nx, ny), _ = wand(name)
    return (ox + ex * s + nx * raus, oy + ey * s + ny * raus, z)


# ── Einrichtung (Innenraum) ──────────────────────────────────────────────────────────────────────────────────────────
MITTELGANG = 1.40                        # zwischen den Bankbloecken
BANK = (2.60, 0.45, 1.45)                # Breite, Sitztiefe, Abstand der Reihen
BANK_REIHEN = 8
BANK_Y0 = -9.60                          # vorderste... hinterste Reihe steht im Westen; die Reihen laufen nach Osten
BANK_X = MITTELGANG / 2 + BANK[0] / 2    # Mitte eines Bankblocks (2,00)
SITZE_JE_BANK = (-0.85, 0.0, 0.85)
SITZHOEHE = 0.44                         # Sitzflaeche ueber dem Boden (FigurenBaukasten.Sitzflaeche)
KANZEL = (-2.75, 2.60, 1.50)             # x, y, Hoehe des Kanzelbodens ueber dem Fussboden
TAUFSTEIN = (-5.70, -9.60)
CHORSTUFEN_Y = (9.30, 10.20)             # drei Stufen quer durch die Vierung (Ostseite), 0,30 tief, 0,15 hoch
ALTARSTUFEN_Y = (14.10, 14.70)           # zwei Stufen zum Podest
HOCHALTAR = (0.0, 15.75)                 # Mitte der Mensa (2,60 x 1,00, 1,00 hoch)
RETABEL_Y = 16.35                        # Bildseite des Schreins; 10 cm weiter vorn als zuerst, damit Schrein, Sockel und Fluegel frei vor dem Chorschluss stehen
RETABEL_HALB = 1.40                      # halbe Breite des Schreins, dort haengen die Fluegel
RETABEL_FLUEGEL = 1.07                   # Laenge eines Fluegels; um 45 Grad nach vorn aufgeklappt
CHORGESTUEHL = (10.90, 13.30)            # y-Bereich, an beiden Chorwaenden
MARIENALTAR = (-7.30, 7.70)              # im linken Querhausarm an der Giebelwand
ABENDMAHLSTISCH = (0.0, 8.40)            # evangelisch: vor den Chorstufen
KREUZ = (0.0, 16.50)                     # evangelisch: Holzkreuz im Chorschluss
SCHRANKE_Y = (BANK_Y0 - 0.6, BANK_Y0 + (BANK_REIHEN - 1) * BANK[2] + 0.8)   # Simultankirche: Schranke im Mittelgang
BLICK_AUGE = (0.0, -11.3, Z_SOCKEL + 1.60)   # am Anfang des Mittelschiffs (aus der Vorhalle fuellten deren Waende das breite Bild)
BLICK_ZIEL = (0.0, 10.0, 5.2)
PLATZ_BISCHOF = (0.0, 14.90, Z_ALTAR)    # vor dem Hochaltar, Blick zur Gemeinde
PLATZ_PFARRER = (0.0, 9.00, Z_SOCKEL)    # hinter dem Abendmahlstisch, Blick zur Gemeinde
WEIHRAUCH = (1.70, 12.60, 2.60)          # haengt an der Kette vom Gewoelbe


def sitzplaetze():
    """(Nummer ab 1, x, y, links): vorn (im Osten) zuerst, wie Glaeubig_<n> gefuellt wird."""
    aus = []
    for reihe in range(BANK_REIHEN - 1, -1, -1):
        y = BANK_Y0 + reihe * BANK[2]
        for seite in (-1, 1):
            for dx in SITZE_JE_BANK:
                aus.append((seite * BANK_X + dx, y, seite < 0))
    return [(i + 1, x, y, links) for i, (x, y, links) in enumerate(aus)]


def retabel_punkte():
    """Aeusserste Punkte (x, y) des Retabels auf der rechten Seite: Sockel hinten, Rueckseite des Schreins, Angel und Ende des Fluegels (Rueckseite)."""
    w = math.radians(45); c, s = math.cos(w), math.sin(w)
    ry, hx, t = RETABEL_Y, RETABEL_HALB, 0.06               # t: Dicke der Tafeln
    return [(1.50, ry + 0.12), (hx, ry + t), (1.46, ry + 0.09), (hx + s * t, ry + c * t),
            (hx + RETABEL_FLUEGEL * c + s * t, ry - RETABEL_FLUEGEL * s + c * t)]


def retabel_abstand():
    """Kleinster Abstand der Punkte des Retabels zur Innenflaeche des Chorschlusses (negativ = in der Wand)."""
    ecken = [apsis_ecke(k, R_APSIS_INNEN) for k in range(6)]
    kleinster = 1e9
    for px, py in retabel_punkte():
        for a, b in zip(ecken, ecken[1:]):
            ex, ey = b[0] - a[0], b[1] - a[1]; l = math.hypot(ex, ey)
            nx, ny = -ey / l, ex / l
            if (0 - a[0]) * nx + (Y_APSIS - a[1]) * ny < 0: nx, ny = -nx, -ny      # Normale in den Raum
            kleinster = min(kleinster, (px - a[0]) * nx + (py - a[1]) * ny)
    return kleinster


# ── Soll und Ist ─────────────────────────────────────────────────────────────────────────────────────────────────────

def soll_ist():
    z = []
    f = lambda v: f"{v:.2f}".replace(".", ",")
    breite = 2 * (X_QUER + 0.50 + 0.06)                     # Querhaus mit Strebepfeilern
    tiefe = (Y_APSIS + R_APSIS + 0.90) - (Y_WEST - 1.30)   # Chorstreben ... Portalstufen
    z.append(("Belegte Flaeche Breite", f"<= {f(FLAECHE[0])}", f(breite), breite <= FLAECHE[0]))
    z.append(("Belegte Flaeche Tiefe", f"<= {f(FLAECHE[1])}", f(tiefe), tiefe <= FLAECHE[1]))
    z.append(("Portal lichte Hoehe", f">= {f(GROESSTE + 0.2)} (Oger + 0,20)", f(PORTAL[2] - PORTAL[1]), PORTAL[2] - PORTAL[1] >= GROESSTE + 0.2))
    z.append(("Portal lichte Breite", f">= {f(2 * BREITESTE)} (zwei Oger)", f(PORTAL[0]), PORTAL[0] >= 2 * BREITESTE))
    z.append(("Mittelgang", f">= {f(2 * BREITESTE)} (zwei Oger)", f(MITTELGANG), MITTELGANG >= 2 * BREITESTE))
    seitengang = PFEILER_X - PFEILER_R - (BANK_X + BANK[0] / 2)
    z.append(("Gang zwischen Bank und Pfeiler", ">= 0,00 (Baenke stehen im Mittelschiff)", f(seitengang), seitengang >= 0))
    z.append(("Seitenschiff lichte Breite", f">= {f(3 * BREITESTE)}", f(X_SEITE - X_ARKADE), X_SEITE - X_ARKADE >= 3 * BREITESTE))
    frei = BANK[2] - BANK[1] - 0.07
    z.append(("Kniefreiheit zwischen den Baenken", ">= 0,60", f(frei), frei >= 0.60))
    z.append(("Arkade lichte Hoehe", f">= {f(GROESSTE + 1.0)}", f(Z_ARKADE[1] - Z_SOCKEL), Z_ARKADE[1] - Z_SOCKEL >= GROESSTE + 1.0))
    z.append(("Arkade lichte Breite", ">= 3,00", f(JOCH - 2 * PFEILER_R), JOCH - 2 * PFEILER_R >= 3.0))
    z.append(("Mittelschiff lichte Hoehe", "13,60 (Scheitel 13,90)", f(Z_SCHEITEL - Z_SOCKEL), abs(Z_SCHEITEL - Z_SOCKEL - 13.6) < 0.01))
    z.append(("Gewoelbe unter der Traufe", f"< {f(Z_TRAUFE)}", f(Z_SCHEITEL), Z_SCHEITEL < Z_TRAUFE))
    z.append(("Seitenschiffdecke unter dem Pultdach", f"<= {f(Z_SEITE - 0.25)} / {f(Z_PULT - 0.25)}", f"{f(Z_SEITE_DECKE[0])} / {f(Z_SEITE_DECKE[1])}",
              Z_SEITE_DECKE[0] <= Z_SEITE - 0.25 and Z_SEITE_DECKE[1] <= Z_PULT - 0.25))
    z.append(("Arkadenscheitel unter der Seitenschiffdecke", f"< {f(Z_SEITE_DECKE[1])}", f(Z_ARKADE[1]), Z_ARKADE[1] < Z_SEITE_DECKE[1]))
    z.append(("Obergadenfenster ueber dem Pultdach", f">= {f(Z_PULT + 0.5)}", f(FENSTER_OBER[1]), FENSTER_OBER[1] >= Z_PULT + 0.5))
    z.append(("Sitzplaetze der Gemeinde", "48 (wie bisher)", str(len(sitzplaetze())), len(sitzplaetze()) == 48))
    hinten = BANK_Y0 - 0.5
    z.append(("Baenke hinter dem Eingangsbogen", f">= {f(Y_TURM + 1.0)}", f(hinten), hinten >= Y_TURM + 1.0))
    vorn = BANK_Y0 + (BANK_REIHEN - 1) * BANK[2] + 0.5
    z.append(("Baenke vor der Kanzel", f"<= {f(KANZEL[1] - 0.9)}", f(vorn), vorn <= KANZEL[1] - 0.9))
    z.append(("Turmhoehe zu Fassadenbreite", "2,2 - 3,0", f(Z_SPITZE / (2 * X_AUSSEN)), 2.2 <= Z_SPITZE / (2 * X_AUSSEN) <= 3.0))
    z.append(("Blick_Auge", "1,60 ueber dem Boden", f(BLICK_AUGE[2] - Z_SOCKEL), abs(BLICK_AUGE[2] - Z_SOCKEL - 1.6) < 1e-6))
    frei = retabel_abstand()
    z.append(("Retabel frei vor der Chorwand (Sockel, Schrein, Fluegel)", ">= 0,04", f(frei), frei >= 0.04))
    return z


def tabelle(zeilen):
    aus = ["| Mass | Soll | Ist | |", "|---|---|---|---|"]
    for name, soll, ist, ok in zeilen:
        aus.append(f"| {name} | {soll} | {ist} | {'ok' if ok else 'FEHLER'} |")
    return "\n".join(aus)


if __name__ == "__main__":
    zeilen = soll_ist()
    print(tabelle(zeilen))
    sys.exit(0 if all(z[3] for z in zeilen) else 1)
