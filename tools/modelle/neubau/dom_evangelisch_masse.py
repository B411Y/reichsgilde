"""Neubau Dom evangelisch: alle Masse an einer Stelle (Huelle aussen, Innenraum, Oeffnungen, Plaetze) - reines Python.

    python tools/modelle/neubau/dom_evangelisch_masse.py

Form nach dem Artwork des Nutzers (24.11.2026): verschachtelte Backsteinkirche. Vorn (-Y) das Langhaus (Saalkirche, 11 m breit) mit
Sandsteingiebel und Vorhalle, dahinter das Achteck (zweiter Raum: Altarraum mit Zeltgewoelbe), dahinter ein Chor als Anbau mit
5/8-Schluss (nur aussen, mit Blendfenstern; der Altar steht im Achteck), zwei Treppentuermchen mit Kegeldach, ein grosser runder
Turm links vor der Westwand. Die Module des Bausatzes (Dom_Module.blend) geben nur die Groessenordnungen vor.

Gleicher Aufbau wie dom_masse.py (Dom katholisch): wand(), punkt(), OEFFNUNGEN, soll_ist(). Waende laufen gegen den Uhrzeigersinn
(Aussenflaeche rechts der Laufrichtung, Normale (dy, -dx)); die Innenflaeche liegt um die Wandstaerke nach innen versetzt.
Koordinaten: X rechts, Y nach hinten (Portal bei -Y, im Spiel +Z), Z oben; Ursprung in der Mitte der belegten Flaeche.
"""
import math
import sys

# ── Figuren (wie dom_masse.py) ──────────────────────────────────────────────────────────────────────────────────────────
FIGUREN = {
    "Halbling": (1.10, 0.32), "Zwerg": (1.35, 0.46), "Mensch": (1.75, 0.46), "Elb": (1.85, 0.44),
    "Ork": (1.93, 0.52), "Oger": (2.50, 0.66),
}
GROESSTE = max(h for h, _ in FIGUREN.values())
BREITESTE = max(b for _, b in FIGUREN.values())

FLAECHE = (20.0, 38.0)   # belegte Flaeche (Breite x Tiefe) fuer den Katalog, wie beim Dom katholisch
BUDGET = (8000, 17000)   # Dreiecke der Huelle (Style-Guide: Monument 8.000-15.000; Nutzer 25.11.2026: Optik vor Optimierung, Schieferlage 0,30 m, bis 17.000)
XA = 0.0                 # Achse von Langhaus, Achteck und Chor

# ── Hoehen ──────────────────────────────────────────────────────────────────────────────────────────────────────────────
Z_SOCKEL = 0.30          # Fussboden des Langhauses (drei Stufen vor der Vorhalle)
Z_CHOR = 0.75            # Fussboden des Achtecks (drei Stufen hoeher)
Z_ALTAR = 1.05           # Podest des Hochaltars
Z_STEIN = 2.40           # Oberkante des Sandsteinsockels aussen (zwei Lagen Backstein zu 1,20 m)

# ── Langhaus ────────────────────────────────────────────────────────────────────────────────────────────────────────────
T_WAND = 0.70
X_AUSSEN = 5.50          # halbe Breite aussen
X_SEITE = X_AUSSEN - T_WAND      # 4,80: halbe lichte Breite
Y_WEST = -15.00          # Aussenflaeche der Westwand (Giebel)
Y_INNEN0 = Y_WEST + T_WAND
Z_TRAUFE_L = 8.40
NEIGUNG_L = math.tan(math.radians(42))
Z_FIRST_L = Z_TRAUFE_L + (X_AUSSEN + 0.30) * NEIGUNG_L
Z_KAEMPFER = 7.40        # Kaempfer der Kreuzgewoelbe im Langhaus (Fensterscheitel)
Z_SCHEITEL_L = Z_KAEMPFER + X_SEITE     # Scheitel: Bogenhoehe = halbe Spannweite
JOCHE = 4

# ── Achteck ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
OKT_M = (0.0, 3.2)       # Mitte
OKT_A = 6.60             # Apothem aussen (Mitte bis Wandmitte)
OKT_A_INNEN = OKT_A - T_WAND     # 5,90
Z_TRAUFE_O = 13.20
Z_SPITZE_O = 23.60       # Spitze des Zeltdachs
Z_OKT_KAEMPFER = 12.00   # Fuss des Zeltgewoelbes
Z_SCHEITEL = 17.20       # Scheitel des Zeltgewoelbes (hoechster Punkt des Innenraums)
Y_INNEN1 = OKT_M[1] - OKT_A_INNEN      # -2,70: Stirnwand des Langhauses = Vorderseite des Achtecks (Bogen in den Altarraum)
X_FLAECHE = OKT_A_INNEN * math.tan(math.pi / 8)  # halbe Laenge einer Innenflaeche des Achtecks (2,44)
X_BOGEN = X_FLAECHE - 0.20       # halbe Breite des Bogens in der Stirnwand (Pfeilerrest 0,20 je Seite)
Z_BOGEN = Z_SOCKEL + 7.10     # Kaempfer des Bogens
Z_BOGEN_SCHEITEL = Z_KAEMPFER    # Scheitel des Bogens (der Zeltgewoelbe-Fuss liegt dahinter hoeher)

# ── Chor (Anbau hinten, nur aussen) ─────────────────────────────────────────────────────────────────────────────────────
CHOR_M = (0.0, 13.1)
CHOR_A = 4.50
Z_TRAUFE_C = 7.20
NEIGUNG_C = math.tan(math.radians(50))
Z_FIRST_C = Z_TRAUFE_C + (CHOR_A + 0.30) * NEIGUNG_C
CHOR_Y0 = 7.90           # die Seitenwaende des Chors beginnen im Achteck
Y_APSIS = CHOR_M[1]
R_APSIS = CHOR_A
# Langhaus endet im Achteck, wo seine Aussenwand auf eine Flaeche des Achtecks stoesst
_T = math.tan(math.pi / 8)
Y_LANG1 = OKT_M[1] - (OKT_A * (1 + _T) - X_AUSSEN) + 0.10       # -0,53

# ── Vorhalle ────────────────────────────────────────────────────────────────────────────────────────────────────────────
VH_X = 1.00              # Mitte
VH_HALB = 2.60
VH_TIEFE = 3.00
VH_Z = (4.80, 4.80 + VH_HALB + 0.25)       # Traufe, First (45 Grad)
PORTAL = (2.40, Z_SOCKEL, 3.00)            # Breite, Schwelle, Sturz (lichte Hoehe 2,70)
PORTAL_X = VH_X
VORHALLE_Y = Y_WEST - VH_TIEFE

# ── Treppentuermchen ────────────────────────────────────────────────────────────────────────────────────────────────────
TUERMCHEN = [(7.0, -0.9), (5.8, 8.2)]
TM_R = 1.30
Z_TM_WAND = 12.0
Z_TM_SPITZE = 17.0

# ── Turm: rund (16 Seiten), links vor der Westwand; verjuengt sich durchgehend schraeg (Kegelstumpf) nach oben ─────────────
TURM_M = (-5.6, -15.5)
TURM_SEITEN = 16
TURM_R = 3.30                # Umkreis am Fuss (ueber dem Sockel, bei Z_TURMSOCKEL)
TURM_R_OBEN = 2.65           # Umkreis unter der Glockenstube (bei Z_GLOCKEN[0]); Glockenstube und Haube beziehen sich darauf
LISENE_STUFEN = ((3.00, 9.00, 0.55, 0.42), (9.00, 14.00, 0.40, 0.34), (14.00, 17.90, 0.27, 0.27))   # z0, z1, Vorsprung, halbe Breite (abgetreppt)
TURM_BAENDER = (9.00, 14.80) # Sandsteingesimse als Baender ohne Absatz
Z_TURMSOCKEL = 3.00
Z_GLOCKEN = (19.40, 23.00)
Z_HAUBE = 23.00
Z_SPITZE = 29.90
Z_KNAUF = 31.30
Z_KREUZ = Z_KNAUF + 0.06     # hoechster Punkt der Huelle (Knauf statt Kreuz)
X_QUER = OKT_A               # groesste halbe Breite der Huelle (Achteck)
Z_FIRST = Z_FIRST_L


def durchsicht_erlaubt(p):
    """Punkt p (x, y, z) im Inneren der offenen Glockenstube (innerhalb der Pfosten, unter dem Haubenring): dort darf man durchsehen und
    Rueckseiten treffen (dom_geometrie.py). Der Haubenring selbst und alles ausserhalb gehoert nicht dazu."""
    if Z_KNAUF - 2.2 <= p[2] <= Z_KNAUF + 0.6 and math.hypot(p[0] - (XA + TURM_M[0]), p[1] - TURM_M[1]) < 0.7: return True     # Luft um Stange und Knauf ueber dem Helm
    return Z_GLOCKEN[0] - 0.1 <= p[2] < Z_GLOCKEN[1] - 0.05 and math.hypot(p[0] - (XA + TURM_M[0]), p[1] - TURM_M[1]) < TURM_R_OBEN - 0.05


def vieleck(mx, my, apothem, n=8, phase=0.0):
    """Regelmaessiges n-Eck, Flaeche k mit Normale phase + k * 360/n Grad: Liste (Ecke a, Ecke b, Normale), gegen den Uhrzeigersinn."""
    r = apothem / math.cos(math.pi / n)
    aus = []
    for k in range(n):
        w = phase + 2 * math.pi * k / n
        aus.append(((mx + r * math.cos(w - math.pi / n), my + r * math.sin(w - math.pi / n)),
                    (mx + r * math.cos(w + math.pi / n), my + r * math.sin(w + math.pi / n)), (math.cos(w), math.sin(w))))
    return aus


# ── Aussenwaende: Name -> (Anfang, Ende, Dicke) ─────────────────────────────────────────────────────────────────────────
def _waende():
    w = {}
    w["west"] = ((-X_AUSSEN, Y_WEST), (X_AUSSEN, Y_WEST), T_WAND)
    w["seite_r"] = ((X_AUSSEN, Y_WEST), (X_AUSSEN, Y_LANG1), T_WAND)
    w["seite_l"] = ((-X_AUSSEN, Y_LANG1), (-X_AUSSEN, Y_WEST), T_WAND)
    for k, (a, b, _) in enumerate(vieleck(*OKT_M, OKT_A, 8, 0.0)):
        w[f"okt_{k}"] = (a, b, T_WAND)
    ys = CHOR_M[1] + CHOR_A * _T
    w["chor_r"] = ((CHOR_A, CHOR_Y0), (CHOR_A, ys), T_WAND)
    w["chor_l"] = ((-CHOR_A, ys), (-CHOR_A, CHOR_Y0), T_WAND)
    for k, (a, b, _) in enumerate(vieleck(*CHOR_M, CHOR_A, 8, 0.0)):
        if k in (1, 2, 3): w[f"chor_{k}"] = (a, b, T_WAND)
    return w


WAENDE = _waende()


def wand(name):
    """(Ursprung, Richtung, Laenge, Aussennormale, Dicke) einer Aussenwand."""
    (ax, ay), (bx, by), d = WAENDE[name]
    l = math.hypot(bx - ax, by - ay)
    ex, ey = (bx - ax) / l, (by - ay) / l
    return (ax, ay), (ex, ey), l, (ey, -ex), d


def punkt(name, s, z, raus=0.0):
    """Punkt auf der Aussenflaeche einer Wand, um `raus` nach aussen (negativ: nach innen) versetzt."""
    (ox, oy), (ex, ey), _, (nx, ny), _ = wand(name)
    return (ox + ex * s + nx * raus, oy + ey * s + ny * raus, z)


# ── Oeffnungen: (Wand, s0, s1, z0, z1, Art, Steilheit) - s entlang der Wand ab ihrem Anfang; nur solche mit Innenseite ─────
SEITE_FENSTER = (1.40, 2.90, 7.40, 1.5)         # Breite, Bruestung, Scheitel, Steilheit
SEITE_Y = (-12.70, -8.40, -4.10)                # Mitten der Fenster im Langhaus (beide Seiten)
OKT_FENSTER = (0.80, 7.60, 11.80, 1.6)          # Paar im Achteck
OKT_ABSTAND = 0.30
CHOR_FENSTER = (1.30, 2.90, 6.50, 1.6)         # Blendfenster des Chors (nur aussen)
# Das grosse Fenster sitzt ganz ueber dem Vorhallendach (First VH_Z[1] + Deckung + 0,45); die kleinen stehen beiderseits im Giebel,
# in Wandmitte x = VH_X -/+ 2,30 (links neben dem Turm, rechts mit Abstand zur Dachschraege des Langhauses)
WEST_FENSTER = ((VH_X, 2.40, VH_Z[1] + 0.55, 11.40, 1.9), (None, 0.80, 7.90, 9.40, 1.7), (None, 0.80, 7.90, 9.40, 1.7))
WEST_KLEIN = (VH_X - 2.30, VH_X + 2.30)


def _oeffnungen():
    o = []
    b, z0, z1, st = SEITE_FENSTER
    _, _, l, _, _ = wand("seite_r")
    for y in SEITE_Y:
        s = y - Y_WEST
        o.append(("seite_r", s - b / 2, s + b / 2, z0, z1, "spitz", st))
    _, _, l, _, _ = wand("seite_l")
    for y in SEITE_Y:
        if y < TURM_M[1] + TURM_R + 0.30: continue      # das erste Fenster der linken Seite stuende hinter dem Turm (Pruefung 25.11.2026)
        s = Y_LANG1 - y
        o.append(("seite_l", s - b / 2, s + b / 2, z0, z1, "spitz", st))
    l = wand("west")[2]
    (xm, bw, zb0, zb1, st1), (_, bk, zk0, zk1, st2), _ = WEST_FENSTER
    sm = l / 2 + xm
    o.append(("west", sm - bw / 2, sm + bw / 2, zb0, zb1, "spitz", st1))
    o.append(("west", sm - PORTAL[0] / 2, sm + PORTAL[0] / 2, PORTAL[1], PORTAL[2], "portal", 0.0))
    for xk in WEST_KLEIN:
        o.append(("west", l / 2 + xk - bk / 2, l / 2 + xk + bk / 2, zk0, zk1, "spitz", st2))
    b, z0, z1, st = OKT_FENSTER
    for k in (0, 4):
        l = wand(f"okt_{k}")[2]
        for sg in (-1, 1):
            m = l / 2 + sg * (OKT_ABSTAND + b / 2)
            o.append((f"okt_{k}", m - b / 2, m + b / 2, z0, z1, "spitz", st))
    return o


OEFFNUNGEN = _oeffnungen()


def blendfenster():
    """Fenster des Chors (nur aussen, ohne Innenraum): (Wand, s0, s1, z0, z1, Art, Steilheit)."""
    b, z0, z1, st = CHOR_FENSTER
    o = []
    for name, mitten in (("chor_r", (1.9, 5.5)), ("chor_l", (wand("chor_l")[2] - 1.9, wand("chor_l")[2] - 5.5)), ("chor_1", (None,)), ("chor_2", (None,)), ("chor_3", (None,))):
        l = wand(name)[2]
        for m in mitten:
            m = l / 2 if m is None else m
            o.append((name, m - b / 2, m + b / 2, z0, z1, "spitz", st))
    return o


# ── Einrichtung (Innenraum, Langhaus 9,60 m breit, 11,6 m lang bis zur Stirnwand) ───────────────────────────────────────
MITTELGANG = 1.40
BANK = (1.70, 0.45, 1.15)                # Breite, Sitztiefe, Abstand der Reihen
BANK_REIHEN = 8
BANK_Y0 = Y_INNEN0 + 1.60                # hinterste Reihe (Westen); die Reihen laufen nach Osten
BANK_X = MITTELGANG / 2 + BANK[0] / 2
SITZE_JE_BANK = (-0.52, 0.0, 0.52)
SITZHOEHE = 0.44
KANZEL = (-3.75, -7.00, 1.50)            # x, y, Hoehe des Kanzelbodens: an der Wand zwischen zwei Fenstern
TAUFSTEIN = (-3.60, Y_INNEN0 + 1.80)
CHORSTUFEN_Y = (Y_INNEN1 - 0.90, Y_INNEN1)       # drei Stufen vor dem Bogen
ALTARSTUFEN_Y = (6.20, 6.80)
HOCHALTAR = (0.0, 8.00)
RETABEL_Y = 8.60
RETABEL_HALB = 1.40
RETABEL_FLUEGEL = 1.07
CHORGESTUEHL = (1.20, 5.20)              # an den beiden Seitenflaechen des Achtecks (x = +-5,90)
MARIENALTAR = (-3.80, -3.40)             # im Langhaus an der linken Wand, neben dem letzten Fenster
ORGEL = ((2.60, X_SEITE - 0.06), (-7.60, -4.90), 3.00)   # Empore an der rechten Wand zwischen zwei Fenstern: x, y, Hoehe des Bodens
ABENDMAHLSTISCH = (0.0, 4.90)            # im Achteck vor den Altarstufen (auf dem Fussboden des Achtecks)
KREUZ = (0.0, 8.75)
FAHNEN_Y = (-10.55, -6.25)               # katholisch: Fahnen an den Wandpfeilern zwischen den Fenstern
SCHRANKE_Y = (BANK_Y0 - 0.6, BANK_Y0 + (BANK_REIHEN - 1) * BANK[2] + 0.8)
BLICK_AUGE = (0.0, Y_INNEN0 + 0.50, Z_SOCKEL + 1.60)
BLICK_ZIEL = (0.0, 3.0, 5.0)
PLATZ_BISCHOF = (0.0, 7.15, Z_ALTAR)
PLATZ_PFARRER = (0.0, 5.70, Z_CHOR)
WEIHRAUCH = (2.60, 1.60, 2.60)
WEIHRAUCH_OBEN = 14.50                   # Haken unter dem Zeltgewoelbe


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
    """Aeusserste Punkte (x, y) des Retabels auf der rechten Seite."""
    w = math.radians(45); c, s = math.cos(w), math.sin(w)
    ry, hx, t = RETABEL_Y, RETABEL_HALB, 0.06
    return [(1.50, ry + 0.12), (hx, ry + t), (1.46, ry + 0.09), (hx + s * t, ry + c * t),
            (hx + RETABEL_FLUEGEL * c + s * t, ry - RETABEL_FLUEGEL * s + c * t)]


def retabel_abstand():
    """Kleinster Abstand der Punkte des Retabels zu den Innenflaechen des Achtecks hinten (negativ = in der Wand)."""
    flaechen = vieleck(*OKT_M, OKT_A_INNEN, 8, 0.0)
    kleinster = 1e9
    for px, py in retabel_punkte():
        for k in (0, 1, 2):
            a, b, n = flaechen[k]
            kleinster = min(kleinster, OKT_A_INNEN - ((px - OKT_M[0]) * n[0] + (py - OKT_M[1]) * n[1]))
    return kleinster


def umriss():
    """(x0, x1, y0, y1) der belegten Flaeche: Turmsockel, Turmchen, Strebepfeiler, Stufen."""
    x0 = TURM_M[0] - TURM_R - 0.12 - 0.55     # Lisenenfuesse
    x1 = max(OKT_A, TUERMCHEN[0][0] + TM_R + 0.25)
    y0 = min(TURM_M[1] - TURM_R - 0.12, VORHALLE_Y - 1.30)
    y1 = CHOR_M[1] + (CHOR_A / math.cos(math.pi / 8) + 0.85) * math.sin(math.radians(67.5))     # Strebepfeiler an den Ecken des Chorschlusses
    return x0, x1, y0, y1


# ── Soll und Ist ────────────────────────────────────────────────────────────────────────────────────────────────────────
def soll_ist():
    z = []
    f = lambda v: f"{v:.2f}".replace(".", ",")
    x0, x1, y0, y1 = umriss()
    z.append(("Belegte Flaeche Breite", f"<= {f(FLAECHE[0])}", f(x1 - x0), x1 - x0 <= FLAECHE[0]))
    z.append(("Belegte Flaeche Tiefe", f"<= {f(FLAECHE[1])}", f(y1 - y0), y1 - y0 <= FLAECHE[1]))
    z.append(("Portal lichte Hoehe", f">= {f(GROESSTE + 0.2)} (Oger + 0,20)", f(PORTAL[2] - PORTAL[1]), PORTAL[2] - PORTAL[1] >= GROESSTE + 0.2 - 1e-9))
    z.append(("Portal lichte Breite", f">= {f(2 * BREITESTE)} (zwei Oger)", f(PORTAL[0]), PORTAL[0] >= 2 * BREITESTE))
    z.append(("Mittelgang", f">= {f(2 * BREITESTE)} (zwei Oger)", f(MITTELGANG), MITTELGANG >= 2 * BREITESTE))
    seitengang = X_SEITE - (BANK_X + BANK[0] / 2)
    z.append(("Gang zwischen Bank und Wand", ">= 1,32 (zwei Oger nebeneinander)", f(seitengang), seitengang >= 2 * BREITESTE))
    frei = BANK[2] - BANK[1] - 0.07
    z.append(("Kniefreiheit zwischen den Baenken", ">= 0,60", f(frei), frei >= 0.60))
    z.append(("Gewoelbe Langhaus: Bogenhoehe = halbe Spannweite", f">= {f(X_SEITE)}", f(Z_SCHEITEL_L - Z_KAEMPFER), Z_SCHEITEL_L - Z_KAEMPFER >= X_SEITE - 1e-9))
    z.append(("Gewoelbe Langhaus unter dem First", f"< {f(Z_FIRST_L)}", f(Z_SCHEITEL_L), Z_SCHEITEL_L < Z_FIRST_L - 0.3))
    z.append(("Zeltgewoelbe unter dem Zeltdach", f"< {f(Z_SPITZE_O)}", f(Z_SCHEITEL), Z_SCHEITEL < Z_SPITZE_O - 1.0))
    z.append(("Bogen zum Altarraum unter dem Zeltgewoelbe-Fuss", f"<= {f(Z_OKT_KAEMPFER)}", f(Z_BOGEN_SCHEITEL), Z_BOGEN_SCHEITEL <= Z_OKT_KAEMPFER))
    z.append(("Sitzplaetze der Gemeinde", "48 (wie bisher)", str(len(sitzplaetze())), len(sitzplaetze()) == 48))
    hinten = BANK_Y0 - 0.5
    z.append(("Baenke hinter dem Portal", f">= {f(Y_INNEN0 + 1.0)}", f(hinten), hinten >= Y_INNEN0 + 1.0))
    vorn = BANK_Y0 + (BANK_REIHEN - 1) * BANK[2] + 0.5
    z.append(("Baenke vor den Stufen", f"<= {f(CHORSTUFEN_Y[0])}", f(vorn), vorn <= CHORSTUFEN_Y[0]))
    kx, ky, _ = KANZEL
    z.append(("Kanzel frei von den Baenken (Schalldeckel)", ">= 0,10", f(-kx - 0.95 - (BANK_X + BANK[0] / 2)), -kx - 0.95 - (BANK_X + BANK[0] / 2) >= 0.10))
    z.append(("Kanzel (Schalldeckel) frei von der Wand", ">= 0,03", f(X_SEITE + kx - 0.95), X_SEITE + kx - 0.95 >= 0.03))
    z.append(("Taufstein frei von den Baenken", ">= 0,10", f(-TAUFSTEIN[0] - 0.60 - (BANK_X + BANK[0] / 2)), -TAUFSTEIN[0] - 0.60 - (BANK_X + BANK[0] / 2) >= 0.10))
    z.append(("Turmhoehe zu Firsthoehe des Achtecks", "1,2 - 1,6", f(Z_SPITZE / Z_SPITZE_O), 1.2 <= Z_SPITZE / Z_SPITZE_O <= 1.6))
    luecke = (VH_X - VH_HALB - 0.25) - (TURM_M[0] + TURM_R + 0.12)
    z.append(("Turm frei neben der Vorhalle", ">= 0,30", f(luecke), luecke >= 0.30))
    z.append(("Blick_Auge", "1,60 ueber dem Boden", f(BLICK_AUGE[2] - Z_SOCKEL), abs(BLICK_AUGE[2] - Z_SOCKEL - 1.6) < 1e-6))
    frei = retabel_abstand()
    z.append(("Retabel frei vor der Wand des Achtecks (Sockel, Schrein, Fluegel)", ">= 0,04", f(frei), frei >= 0.04))
    tisch = ABENDMAHLSTISCH[1] + 0.50
    z.append(("Abendmahlstisch vor den Altarstufen", f"<= {f(ALTARSTUFEN_Y[0])}", f(tisch), tisch <= ALTARSTUFEN_Y[0]))
    gy0, gy1 = CHORGESTUEHL
    ok_g = gy0 >= OKT_M[1] - OKT_A_INNEN * _T and gy1 <= OKT_M[1] + OKT_A_INNEN * _T
    z.append(("Chorgestuehl an der Flaeche des Achtecks", f"y {f(OKT_M[1] - OKT_A_INNEN * _T)} - {f(OKT_M[1] + OKT_A_INNEN * _T)}", f"{f(gy0)} - {f(gy1)}", ok_g))
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
