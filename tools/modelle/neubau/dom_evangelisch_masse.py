"""Neubau Dom evangelisch: alle Masse an einer Stelle (Aussen, Innenraum, Oeffnungen, Plaetze) - reines Python.

    python tools/modelle/neubau/dom_evangelisch_masse.py [--soll-ist]

Gleicher Aufbau wie dom_masse.py (Dom katholisch): WAENDE, OEFFNUNGEN, wand(), punkt(), Einrichtung, soll_ist(). Die Bausteine
aus dom_bib.py arbeiten mit beiden Massdateien; die Blender-Skripte dom_evangelisch_aussen.py und dom_evangelisch_innen.py lesen
ihre Masse von hier ("innen = aussen minus Wandstaerke").

Bauform nach der Vorlage "Dom evangelisch": dreischiffige Hallenkirche aus Backstein unter einem grossen Schieferdach, Sockel,
Ecken, Gewaende und Vorhalle aus Sandstein, ein runder Turm mit Kupferhaube vorn links vor der Westwand, Chor mit 5/10-Schluss.
Keine Turmuhr (um 1400 gibt es sie hier nicht).

Koordinaten wie bei allen Bauskripten: X rechts, Y nach hinten (Portal bei -Y, im Spiel +Z; der Chor bei +Y), Z oben; Ursprung
in der Mitte der belegten Flaeche (FLAECHE).
"""
import math
import sys

# ── Figuren (wie dom_masse.py) ───────────────────────────────────────────────────────────────────────────────────────
FIGUREN = {
    "Halbling": (1.10, 0.32), "Zwerg": (1.35, 0.46), "Mensch": (1.75, 0.46), "Elb": (1.85, 0.44),
    "Ork": (1.93, 0.52), "Oger": (2.50, 0.66),
}
GROESSTE = max(h for h, _ in FIGUREN.values())
BREITESTE = max(b for _, b in FIGUREN.values())

FLAECHE = (20.0, 38.0)   # belegte Flaeche (Breite x Tiefe) fuer den Katalog, wie beim Dom katholisch; Paket 3 setzt sie in die Karten
BUDGET = (8000, 15000)   # Dreiecke der Huelle (Style-Guide: Monument)

# ── Hoehen ───────────────────────────────────────────────────────────────────────────────────────────────────────────
Z_SOCKEL = 0.30          # Fussboden (drei Stufen vor der Vorhalle)
Z_CHOR = 0.75            # Fussboden des Chors (drei Stufen hoeher)
Z_ALTAR = 1.05           # Podest des Hochaltars
Z_STEIN = 1.20           # Oberkante des Sandsteinsockels aussen, darueber Backstein
Z_TRAUFE = 11.40         # Traufe der Halle und des Chors
NEIGUNG = math.tan(math.radians(52))
Z_KAEMPFER = 7.00        # Kaempfer aller Gewoelbe (Halle: drei gleich hohe Schiffe)
Z_SCHEITEL = 11.00       # Scheitel der Gewoelbe (4,00 ueber dem Kaempfer)

# ── Grundriss ────────────────────────────────────────────────────────────────────────────────────────────────────────
T_WAND = 0.70
X_MITTE = 3.50           # halbe lichte Breite des Chors und des Chorbogens
X_ARKADE = X_MITTE + T_WAND      # 4,20: Aussenflaeche der Chorwaende
X_SEITE = 7.00           # Innenflaeche der Seitenschiffwaende (Halle 14,00 m licht)
X_AUSSEN = X_SEITE + T_WAND      # 7,70
PFEILER_X = 3.85         # Achse der Rundpfeiler (Mittelschiff 7,70, Seitenschiffe 3,15 zwischen Achse und Wand)
PFEILER_R = 0.45
Y_WEST = -10.90          # Aussenflaeche der Westwand (Giebel)
Y_INNEN0 = Y_WEST + T_WAND       # Innenflaeche der Westwand
JOCH = 4.25
JOCHE = 4
Y_INNEN1 = Y_INNEN0 + JOCHE * JOCH   # Innenflaeche der Ostwand der Halle (6,80)
Y_OST = Y_INNEN1 + T_WAND        # Aussenflaeche der Ostwand (7,50); dort beginnt der Chor
Y_APSIS = Y_OST + 4.00           # Mitte des Chorschlusses
R_APSIS = 4.20           # Umkreis der Aussenflaeche (5 Seiten von 10)
R_APSIS_INNEN = 3.50
PFEILER_Y = tuple(round(Y_INNEN0 + JOCH * i, 3) for i in range(1, JOCHE))
Z_FIRST = Z_TRAUFE + (X_AUSSEN + 0.30) * NEIGUNG         # First der Halle
Z_CHORFIRST = Z_TRAUFE + (X_ARKADE + 0.30) * NEIGUNG     # First des Chors (niedriger: der Ostgiebel der Halle steht darueber)


def apsis_ecke(k, r=R_APSIS):
    """Ecke k (0 rechts ... 5 links) des Chorschlusses."""
    w = math.radians(36 * k)
    return (r * math.cos(w), Y_APSIS + r * math.sin(w))


# ── Turm: rund (16 Seiten), steht vorn links vor der Westwand; eine Seite liegt 3 cm hinter der Wandflaeche ───────────
TURM_SEITEN = 16
TURM_R = 2.80                            # Umkreis des Schafts
TURM_M = (-5.20, Y_WEST + 0.03 - TURM_R * math.cos(math.pi / TURM_SEITEN))      # Mitte
Z_TURMSOCKEL = 3.00                      # Sandsteinsockel des Turms (um 0,12 breiter)
Z_GLOCKEN = (19.00, 22.60)               # offene Glockenstube aus Holz
Z_HAUBE = 22.60                          # Fuss der Kupferhaube
Z_SPITZE = 30.40                         # Spitze der Haube; Kreuz bis Z_KREUZ
Z_KREUZ = 32.40

# ── Vorhalle aus Sandstein vor dem Portal ────────────────────────────────────────────────────────────────────────────
VORHALLE_X = 1.70                        # halbe Breite
VORHALLE_Y = Y_WEST - 2.40               # Stirnflaeche
VORHALLE_Z = (4.30, 4.30 + (1.70 + 0.25) * 1.0)      # Traufe, First (45 Grad)

# ── Aussenwaende: Name -> (Anfang, Ende, Dicke); die Aussenseite liegt links der Laufrichtung (Umlauf im Uhrzeigersinn von oben) ──


def _waende():
    w = {}
    w["seite_l"] = ((-X_AUSSEN, Y_WEST), (-X_AUSSEN, Y_OST), T_WAND)
    w["ost_l"] = ((-X_AUSSEN, Y_OST), (-X_ARKADE, Y_OST), T_WAND)
    w["chor_l"] = ((-X_ARKADE, Y_OST), (-X_ARKADE, Y_APSIS), T_WAND)
    for k in range(5):
        w[f"apsis_{k}"] = (apsis_ecke(5 - k), apsis_ecke(4 - k), (R_APSIS - R_APSIS_INNEN) * math.cos(math.radians(18)))
    w["chor_r"] = ((X_ARKADE, Y_APSIS), (X_ARKADE, Y_OST), T_WAND)
    w["ost_r"] = ((X_ARKADE, Y_OST), (X_AUSSEN, Y_OST), T_WAND)
    w["seite_r"] = ((X_AUSSEN, Y_OST), (X_AUSSEN, Y_WEST), T_WAND)
    w["west"] = ((X_AUSSEN, Y_WEST), (-X_AUSSEN, Y_WEST), T_WAND)
    return w


def wand(name):
    """(Ursprung, Richtung, Laenge, Aussennormale, Dicke) einer Aussenwand."""
    (ax, ay), (bx, by), d = WAENDE[name]
    l = math.hypot(bx - ax, by - ay)
    ex, ey = (bx - ax) / l, (by - ay) / l
    return (ax, ay), (ex, ey), l, (-ey, ex), d


WAENDE = _waende()

# ── Oeffnungen: (Wand, s0, s1, z0, z1, Art, Steilheit) - s entlang der Wand ab ihrem Anfang ───────────────────────────
FENSTER_SEITE = (1.30, 2.60, 9.20)       # Breite, Bruestung, Scheitel (im Schildbogen des Gewoelbes)
FENSTER_CHOR = (1.20, 2.60, 6.80)        # Scheitel unter dem Gewoelbekaempfer (der Chorschluss hat keine Stichkappen)
FENSTER_WEST = ((0.0, 1.20, 6.60, 10.40), (-1.75, 0.90, 6.60, 9.40), (1.75, 0.90, 6.60, 9.40))   # x, Breite, Bruestung, Scheitel
PORTAL = (2.40, Z_SOCKEL, 3.00)          # Breite, Schwelle, Sturz (lichte Hoehe 2,70); aussen sitzt die Tuer in der Vorhalle
STEIL = 1.25


def _oeffnungen():
    o = []
    mitte = lambda name: wand(name)[2] / 2
    b, z0, z1 = FENSTER_SEITE
    for seite in ("seite_l", "seite_r"):
        for i in range(JOCHE):
            s = T_WAND + (i + 0.5) * JOCH
            o.append((seite, s - b / 2, s + b / 2, z0, z1, "spitz", STEIL))
    b, z0, z1 = FENSTER_CHOR
    for name in ("chor_l", "chor_r") + tuple(f"apsis_{k}" for k in range(5)):
        o.append((name, mitte(name) - b / 2, mitte(name) + b / 2, z0, z1, "spitz", STEIL))
    b, z0, z1 = PORTAL
    o.append(("west", mitte("west") - b / 2, mitte("west") + b / 2, z0, z1, "portal", 0.0))
    for x, b, z0, z1 in FENSTER_WEST:
        o.append(("west", mitte("west") - x - b / 2, mitte("west") - x + b / 2, z0, z1, "spitz", STEIL))
    return o


OEFFNUNGEN = _oeffnungen()


def punkt(name, s, z, raus=0.0):
    """Punkt auf der Aussenflaeche einer Wand, um `raus` nach aussen (negativ: nach innen) versetzt."""
    (ox, oy), (ex, ey), _, (nx, ny), _ = wand(name)
    return (ox + ex * s + nx * raus, oy + ey * s + ny * raus, z)


# ── Einrichtung (Innenraum) ──────────────────────────────────────────────────────────────────────────────────────────
MITTELGANG = 1.40
BANK = (2.60, 0.45, 1.45)                # Breite, Sitztiefe, Abstand der Reihen
BANK_REIHEN = 8
BANK_Y0 = Y_INNEN0 + 1.80                # hinterste Reihe (Westen); die Reihen laufen nach Osten
BANK_X = MITTELGANG / 2 + BANK[0] / 2
SITZE_JE_BANK = (-0.85, 0.0, 0.85)
SITZHOEHE = 0.44
KANZEL = (-2.55, Y_INNEN0 + 13.40, 1.50) # x, y, Hoehe des Kanzelbodens: am dritten Pfeiler links
TAUFSTEIN = (-5.60, Y_INNEN0 + 1.80)
CHORSTUFEN_Y = (Y_INNEN1 - 0.90, Y_INNEN1)       # drei Stufen vor dem Chorbogen
ALTARSTUFEN_Y = (Y_APSIS + 0.60, Y_APSIS + 1.20)
HOCHALTAR = (0.0, Y_APSIS + 2.25)
RETABEL_Y = Y_APSIS + 2.85
RETABEL_HALB = 1.40
RETABEL_FLUEGEL = 1.07
CHORGESTUEHL = (Y_APSIS - 3.00, Y_APSIS - 0.60)
MARIENALTAR = (-6.00, Y_INNEN1 - 0.70)   # im linken Seitenschiff an der Aussenwand, neben dem letzten Fenster
ORGEL = ((4.40, X_SEITE - 0.06), (Y_INNEN1 - 2.70, Y_INNEN1 - 0.06), 3.00)   # Empore im rechten Seitenschiff vor der Ostwand: x, y, Hoehe des Bodens
ABENDMAHLSTISCH = (0.0, Y_INNEN1 - 1.80)
KREUZ = (0.0, Y_APSIS + 3.00)
SCHRANKE_Y = (BANK_Y0 - 0.6, BANK_Y0 + (BANK_REIHEN - 1) * BANK[2] + 0.8)
BLICK_AUGE = (0.0, Y_INNEN0 + 0.50, Z_SOCKEL + 1.60)
BLICK_ZIEL = (0.0, Y_APSIS - 2.0, 4.2)
PLATZ_BISCHOF = (0.0, Y_APSIS + 1.40, Z_ALTAR)
PLATZ_PFARRER = (0.0, Y_INNEN1 - 1.20, Z_SOCKEL)
WEIHRAUCH = (1.70, Y_APSIS - 0.90, 2.60)


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
    """Aeusserste Punkte (x, y) des Retabels auf der rechten Seite (wie dom_masse.py)."""
    w = math.radians(45); c, s = math.cos(w), math.sin(w)
    ry, hx, t = RETABEL_Y, RETABEL_HALB, 0.06
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
            if (0 - a[0]) * nx + (Y_APSIS - a[1]) * ny < 0: nx, ny = -nx, -ny
            kleinster = min(kleinster, (px - a[0]) * nx + (py - a[1]) * ny)
    return kleinster


def umriss():
    """(x0, x1, y0, y1) der belegten Flaeche: Turmsockel, Strebepfeiler, Stufen."""
    x0 = TURM_M[0] - TURM_R - 0.12
    x1 = X_AUSSEN + 0.90
    y0 = min(TURM_M[1] - TURM_R - 0.12, VORHALLE_Y - 1.30)
    y1 = Y_APSIS + R_APSIS + 0.85
    return x0, x1, y0, y1


# ── Soll und Ist ─────────────────────────────────────────────────────────────────────────────────────────────────────

def soll_ist():
    z = []
    f = lambda v: f"{v:.2f}".replace(".", ",")
    x0, x1, y0, y1 = umriss()
    breite, tiefe = 2 * max(-x0, x1), 2 * max(-y0, y1)        # um den Ursprung (Mitte der belegten Flaeche)
    z.append(("Belegte Flaeche Breite", f"<= {f(FLAECHE[0])}", f(breite), breite <= FLAECHE[0]))
    z.append(("Belegte Flaeche Tiefe", f"<= {f(FLAECHE[1])}", f(tiefe), tiefe <= FLAECHE[1]))
    z.append(("Portal lichte Hoehe", f">= {f(GROESSTE + 0.2)} (Oger + 0,20)", f(PORTAL[2] - PORTAL[1]), PORTAL[2] - PORTAL[1] >= GROESSTE + 0.2 - 1e-9))
    z.append(("Portal lichte Breite", f">= {f(2 * BREITESTE)} (zwei Oger)", f(PORTAL[0]), PORTAL[0] >= 2 * BREITESTE))
    z.append(("Mittelgang", f">= {f(2 * BREITESTE)} (zwei Oger)", f(MITTELGANG), MITTELGANG >= 2 * BREITESTE))
    seitengang = PFEILER_X - PFEILER_R - (BANK_X + BANK[0] / 2)
    z.append(("Gang zwischen Bank und Pfeiler", ">= 0,00 (Baenke stehen im Mittelschiff)", f(seitengang), seitengang >= 0))
    z.append(("Seitenschiff lichte Breite", f">= {f(3 * BREITESTE)}", f(X_SEITE - PFEILER_X - PFEILER_R), X_SEITE - PFEILER_X - PFEILER_R >= 3 * BREITESTE))
    frei = BANK[2] - BANK[1] - 0.07
    z.append(("Kniefreiheit zwischen den Baenken", ">= 0,60", f(frei), frei >= 0.60))
    z.append(("Pfeiler lichter Abstand", ">= 3,00", f(JOCH - 2 * PFEILER_R), JOCH - 2 * PFEILER_R >= 3.0))
    z.append(("Halle lichte Hoehe", "10,70 (Scheitel 11,00)", f(Z_SCHEITEL - Z_SOCKEL), abs(Z_SCHEITEL - Z_SOCKEL - 10.7) < 0.01))
    z.append(("Gewoelbe unter der Traufe", f"< {f(Z_TRAUFE)}", f(Z_SCHEITEL), Z_SCHEITEL < Z_TRAUFE))
    z.append(("Gewoelbe: Bogenhoehe >= halbe Spannweite", f">= {f(PFEILER_X)}", f(Z_SCHEITEL - Z_KAEMPFER), Z_SCHEITEL - Z_KAEMPFER >= PFEILER_X))
    z.append(("Westfenster ueber dem First der Vorhalle", f">= {f(VORHALLE_Z[1] + 0.2)}", f(FENSTER_WEST[0][2]), FENSTER_WEST[0][2] >= VORHALLE_Z[1] + 0.2))
    z.append(("Sitzplaetze der Gemeinde", "48 (wie bisher)", str(len(sitzplaetze())), len(sitzplaetze()) == 48))
    hinten = BANK_Y0 - 0.5
    z.append(("Baenke hinter dem Portal", f">= {f(Y_INNEN0 + 1.0)}", f(hinten), hinten >= Y_INNEN0 + 1.0))
    vorn = BANK_Y0 + (BANK_REIHEN - 1) * BANK[2] + 0.5
    z.append(("Baenke vor der Kanzel", f"<= {f(KANZEL[1] - 0.9)}", f(vorn), vorn <= KANZEL[1] - 0.9))
    for py in PFEILER_Y:
        for name, (kx, ky), r in (("Kanzel (Schalldeckel)", KANZEL[:2], 0.95), ("Taufstein", TAUFSTEIN, 0.60)):
            d = math.hypot(kx + PFEILER_X, ky - py) - PFEILER_R - r
            if d < 1.0: z.append((f"{name} frei vom Pfeiler bei y {f(py)}", ">= 0,03", f(d), d >= 0.03))
    z.append(("Turmhoehe zu Firsthoehe", "1,3 - 1,6", f(Z_SPITZE / Z_FIRST), 1.3 <= Z_SPITZE / Z_FIRST <= 1.6))
    z.append(("Turm frei neben der Vorhalle", ">= 0,30", f(-VORHALLE_X - 0.25 - (TURM_M[0] + TURM_R + 0.12)), -VORHALLE_X - 0.25 - (TURM_M[0] + TURM_R + 0.12) >= 0.30))
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
