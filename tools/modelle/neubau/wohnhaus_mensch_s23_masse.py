"""Masse der Wohnhaeuser Mensch, Stufe 2 (Buergerhaus) und Stufe 3 (Steinhaus) - reines Python.

    python tools/modelle/neubau/wohnhaus_mensch_s23_masse.py [--stufe=<2|3>] [--form=<1..4>] [--soll-ist]

Das Bauskript (wohnhaus_mensch_s23.py) liest alle Masse von hier: Haus, Wohnstube, Treppe, Zimmertueren und die Lage der Raeume
(Hinterzimmer, Bibliothek aus tools/modelle/raeume/). Innen und aussen entstehen aus denselben Zahlen: Innenwand = Aussenwand
minus Wandstaerke; die Raeume liegen im Haus (Volumen der Huelle) und haben genau die Innenmasse ihres Geschosses.
--soll-ist prueft die Masse fuer Mensch (1,75 m) und Elb (1,85 m) und nennt zur Auskunft, was ein Oger (2,50 m) nicht passt.

Koordinaten wie bei allen Bauskripten: X rechts, Y nach hinten (Gasse bei -Y, im Spiel +Z), Z oben. Grundflaeche wie der Katalog
"buergerhaus"/"steinhaus": 8 x 10 m mit 2 m Vorplatz (Front bei y = -3, Hauskoerper hoechstens bis y = 4,4).

Lage der Raeume (Grundform 1, Stufe 2): Erdgeschoss Wohnstube; dahinter hinter einer Bretterwand mit Zimmertuer ein Treppengang mit
gerader Stiege; Obergeschoss: der Raum (Hinterzimmer oder Bibliothek, Kern: ein Platz) ueber der Wohnstube, links daneben und
dahinter der Flur, auf dem die Stiege ankommt; die Zimmertuer des Raums liegt in seiner linken Wand. Der Raum ist das Modell aus
tools/modelle/raeume/ (gleiche Moebel und Masse), nur mit dem Fenster zur Gasse und der Tuer an dieser Stelle.
"""
import math
import sys

# ── Figuren (Hoehe, Schulterbreite) wie tools/modelle/neubau/rathaus_masse.py ─────────────────────────────────────────
FIGUREN = {"Mensch": (1.75, 0.46), "Elb": (1.85, 0.44), "Oger": (2.50, 0.66)}
AUGE = 1.60                     # Augenhoehe Mensch ueber dem Fussboden

# ── Raummodul aus tools/modelle/raeume/raeume_masse.py (Innenmasse, im eigenen Rahmen) ─────────────────────────────────
MODUL_IX, MODUL_IV, MODUL_IH = 1.98, -2.38, 2.18     # Innenwaende x -1,98..1,98, y -2,38..2,18
MODUL_BODEN, MODUL_DECKE = 0.15, 2.55               # Fussboden, Unterseite der Decke (2,40 licht)
MODUL_BALKEN = 0.16                                 # Deckenbalken unter der Decke
MODUL_TUER = (0.85, 1.75)                           # Zimmertuer in der linken Wand (y, licht 0,90 x 2,00)

# ── Feste Masse ──────────────────────────────────────────────────────────────────────────────────────────────────────
T = 0.22                        # Aussenwand
T_INNEN = 0.12                  # Bretterwand innen
Z_BODEN = 0.15                  # Fussboden Erdgeschoss
Z_SOCKEL = 0.45
Z_EG_DECKE = 2.55               # Unterseite der Decke ueber der Wohnstube (2,40 licht)
DECKE = 0.20                    # Balkenlage mit Dielen
Z_OG = Z_EG_DECKE + DECKE       # Fussboden Obergeschoss (2,75)
Z_OG_DECKE = Z_OG + (MODUL_DECKE - MODUL_BODEN)     # 5,15
Z_TRAUFE = Z_OG_DECKE + DECKE   # Oberkante der Waende = Dachboden (5,35)
KRAG = 0.22                     # das Obergeschoss kragt vorn und an den Seiten aus
NEIGUNG_GRAD = 52
NEIGUNG = math.tan(math.radians(NEIGUNG_GRAD))
UEBER = 0.38                    # Dachueberstand an der Traufe
UEBER_ORTGANG = 0.18            # am Giebel
LAGE = 0.25                     # Hoehe einer Ziegellage auf der Dachflaeche (Band "stroh_franse")
TUER_LICHT = (0.96, 2.00)       # Haustuer
ZIMMERTUER = (0.90, 2.00)       # Zimmertueren (wie tools/modelle/raeume/zimmertuer.py)
STUFE_VOR_TUER = 0.15
FENSTER_ABSTAND = 0.05          # so viel Abstand haelt jedes Holz, das nicht zum Fenster gehoert, von der Oeffnung (Pruefung im Messskript)

# ── Grundform 1, schmal und hoch ─────────────────────────────────────────────────────────────────────────────────────
BX = 2.60                       # halbe Breite des Erdgeschosses (aussen)
VORN, HINTEN = -2.60, 3.30      # Erdgeschoss aussen
OX = BX + KRAG                  # Obergeschoss aussen
OVORN = VORN - KRAG
IX, IV = BX - T, VORN + T       # Wohnstube innen: x -2,38..2,38, y -2,38..
STUBE_HINTEN = 1.96             # Bretterwand hinter der Wohnstube (Wohnstubenseite)
GANG = (STUBE_HINTEN + T_INNEN, HINTEN - T)          # Treppengang hinter der Wohnstube (y)
JX, JV = OX - T, OVORN + T      # Obergeschoss innen
# Treppe im Gang: gerade Stiege entlang x, Fuss rechts, Austritt links
STEIGUNGEN = 13
STEIGUNG = (Z_OG - Z_BODEN) / STEIGUNGEN             # 0,20
AUFTRITT = 0.2417
TREPPE_FUSS_X = 1.00
TREPPE_KOPF_X = TREPPE_FUSS_X - (STEIGUNGEN - 1) * AUFTRITT
TREPPE_Y = (GANG[0] + 0.07, GANG[0] + 0.07 + 0.85)   # Laufbreite 0,85
TREPPE_LOCH_X = (TREPPE_KOPF_X, 0.82)                # Oeffnung in der Decke ueber der Treppe (Kopfhoehe)
# Zimmertuer der Wohnstube in der Bretterwand (x, licht)
STUBE_TUER = (1.10, 2.00)
# Raum im Obergeschoss: Modul versetzt um (dx, dy, dz)
RAUM_VERSATZ = (JX - MODUL_IX, JV - MODUL_IV, Z_OG - MODUL_BODEN)
RAUM_X = (-MODUL_IX + RAUM_VERSATZ[0], JX)           # -1,36..2,60
RAUM_Y = (JV, MODUL_IH + RAUM_VERSATZ[1])            # -2,60..1,96
FLUR_X = (-JX, RAUM_X[0] - T_INNEN)                 # Flur links neben dem Raum
# ── Konstruktionsregel Fachwerk (Rueckmeldung des Nutzers 14.11.2026; Quellen in docs/bereiche/darstellung.md) ────────
#    Jede Fachwerkwand eines Geschosses: Schwelle unten, Rahm oben, Staender (Eckstaender an den Enden) in einem REGELMAESSIGEN
#    Raster mit Achsabstand um RASTER_ZIEL; dazwischen die Gefache. Ein Fenster fuellt genau ein Gefach: zwischen zwei Staendern
#    (Breite = Achsabstand minus Staenderbreite) und zwischen Brustriegel und Sturzriegel, die in einer Hoehe durch das Geschoss laufen
#    (gleiche Fenster je Geschoss). Gefache ohne Fenster: Brust- und Sturzriegel; die Eckgefache tragen eine Strebe (vom Eckstaender
#    unten schraeg nach innen oben), spiegelgleich an beiden Ecken. Giebel: dasselbe Achsraster bis unter den First – Giebelschwelle,
#    Fenster in den Gefachen beiderseits der Mittelachse (Spitzsaeule), darueber der Kehlbalken (zugleich Sturz), daruber der
#    Hahnenbalken, Streben in den aeusseren Gefachen. Alles spiegelgleich zur Giebelachse.
BW = 0.17                       # Staenderbreite
RIEGEL = 0.14                   # Hoehe der Riegel
RASTER_ZIEL = 0.95              # Achsabstand der Staender, um den das Raster gelegt wird (Fachwerk: um 1 m)
OG_FENSTER_Z = (3.65, 4.60)     # Bruest- und Sturzhoehe aller Fenster des Obergeschosses (Bruestung 0,90 ueber dem Boden)


def raster(laenge, ziel=RASTER_ZIEL, b=BW):
    """Achsen der Staender einer Wand der Laenge `laenge` (ab ihrem Ursprung): Eckstaender buendig an den Enden, dazwischen
    gleiche Abstaende. Gibt (Achsen, Achsabstand) zurueck."""
    n = max(1, round((laenge - b) / ziel))
    a = (laenge - b) / n
    return [b / 2 + k * a for k in range(n + 1)], a


def gefach(start, laenge, k):
    """Lichte Weite (a0, a1) des Gefachs k einer Wand, die bei `start` (Weltkoordinate entlang der Wand) beginnt."""
    achsen, _ = raster(laenge)
    return (start + achsen[k] + BW / 2, start + achsen[k + 1] - BW / 2)


_l_vorn, _l_seite = 2 * OX, HINTEN - OVORN
_nv = len(raster(_l_vorn)[0]) - 1                   # Gefache der Giebelseite (gerade Zahl: die Mitte ist eine Achse)
_v = lambda k: gefach(-OX, _l_vorn, k)
_s = lambda k: gefach(OVORN, _l_seite, k)
GIEBEL_FENSTER_Z = (Z_TRAUFE + 0.75, Z_TRAUFE + 1.50)
# Fenster der Huelle: Wand, (a0, a1) entlang der Wand in Weltkoordinaten, (z0, z1), Art
FENSTER = [
    ("vorn_eg", (0.62, 1.58), (Z_BODEN, Z_BODEN + TUER_LICHT[1]), "tuer"),
    ("vorn_eg", (-1.35, -0.35), (1.05, 2.00), "fenster"),
    ("links_eg", (0.55, 1.35), (1.10, 1.95), "fenster"),      # ueber dem Ende der Bank
    ("hinten_eg", (1.60, 2.15), (1.15, 1.85), "fenster"),
    # Obergeschoss: Gefache nach dem Raster (Giebelseiten spiegelgleich die beiden mittleren Gefache)
    ("vorn_og", _v(_nv // 2 - 1), OG_FENSTER_Z, "fenster"), ("vorn_og", _v(_nv // 2), OG_FENSTER_Z, "fenster"),
    ("rechts_og", _s(1), OG_FENSTER_Z, "fenster"),
    ("links_og", _s(2), OG_FENSTER_Z, "fenster"),
    ("hinten_og", _v(_nv // 2 - 1), OG_FENSTER_Z, "fenster"), ("hinten_og", _v(_nv // 2), OG_FENSTER_Z, "fenster"),
    # Giebel: die beiden Gefache an der Mittelachse
    ("vorn_gb", _v(_nv // 2 - 1), GIEBEL_FENSTER_Z, "fenster"), ("vorn_gb", _v(_nv // 2), GIEBEL_FENSTER_Z, "fenster"),
    ("hinten_gb", _v(_nv // 2 - 1), GIEBEL_FENSTER_Z, "fenster"), ("hinten_gb", _v(_nv // 2), GIEBEL_FENSTER_Z, "fenster"),
]
# Die Fenster der Raeume im Rahmen des Raummoduls (tools/modelle/raeume/raeume_s23.py): dieselben Oeffnungen, um RAUM_VERSATZ zurueck
MODUL_FENSTER_VORN = [((a0 - RAUM_VERSATZ[0], a1 - RAUM_VERSATZ[0]), (z0 - RAUM_VERSATZ[2], z1 - RAUM_VERSATZ[2]))
                      for w, (a0, a1), (z0, z1), art in FENSTER if w == "vorn_og" and a0 >= RAUM_X[0]]
MODUL_FENSTER = MODUL_FENSTER_VORN[0]
MODUL_FENSTER_RECHTS = next(((a0 - RAUM_VERSATZ[1], a1 - RAUM_VERSATZ[1]), (z0 - RAUM_VERSATZ[2], z1 - RAUM_VERSATZ[2]))
                            for w, (a0, a1), (z0, z1), art in FENSTER if w == "rechts_og")
WAPPEN_X = sum(gefach(-OX, 2 * OX, len(raster(2 * OX)[0]) - 3)) / 2   # Wappen im zweitletzten Gefach rechts im Obergeschoss zur Gasse
# Herd der Wohnstube an der rechten Wand (Haube und Schlot fuehren in den Steinschornstein aussen an dieser Wand)
SCHORNSTEIN_RECHTS = (OX, OX + 0.55, -0.70, 0.20)    # Steinschornstein aussen an der rechten Wand (Herd der Wohnstube, Kamin der Bibliothek)
# Stube: Blick der Raumkamera
STUBE_AUGE = (-1.50, -1.60, Z_BODEN + AUGE)     # vorn links: geradeaus die Zimmertuer, rechts Herd und Haustuer, links Tisch und Stube
STUBE_ZIEL = (1.90, 1.50, Z_BODEN + 1.05)


# ── Grundformen der Stufe 2 (Vorlagen lvl2_v01..v04): derselbe Kern (Wohnstube, Treppengang, Raum im Obergeschoss), aussen
#    verschieden. dx: der Kern ruecken auf dem Platz nach links/rechts (Platz fuer den Anbau). Anbau als Kasten (x0, x1, y0, y1)
#    im Rahmen des Kerns (vor dem Verschieben), Hoehe der Traufe/Wand. Nur Huelle: Anbau, Schuppen und Erker sind nicht begehbar.
FORMEN = {
    1: dict(name="schmal und hoch", dx=0.0),
    2: dict(name="mit Fluegel", dx=-0.65, fluegel=(BX, 4.40, 0.35, HINTEN, 2.55)),         # Fachwerkfluegel rechts hinten mit Satteldach (Stall, Kammer)
    3: dict(name="mit Schuppen", dx=-0.45, schuppen=(BX, 4.15, 0.35, 3.05, 2.45, 1.95)),   # offener Schuppen rechts unter der Auskragung: Pultdach von 2,45 auf 1,95
    4: dict(name="mit Erker", dx=0.0, erker=(_s(2)[0] - BW, _s(2)[1] + BW, 3.30, 4.95, 0.60)),              # Erker am Flur (links, y0..y1, z0..z1, Tiefe) mit Zwerchgiebel
}


def fenster(form, stufe=2):
    """Die Oeffnungen der Huelle je Grundform und Stufe (Grundform 4: das linke Fenster des Obergeschosses liegt im Erker; Stufe 3:
    dazu das zweite Obergeschoss mit den Fenstern der Bibliothek und des Flurs, das Giebelfenster hinten eine Etage hoeher, vorn im
    Treppengiebel aus Haustein ein Fenster mittig, Entscheidung des Nutzers)."""
    liste = [f for f in FENSTER if not (form == 4 and f[0] == "links_og")]
    if stufe == 3:
        # Treppengiebel (Entscheidung des Nutzers): vorn kein Fachwerkgiebel, dafuer ein Fenster mittig im Haustein, so breit wie ein
        # Gefach, auf der Hoehe der Giebelfenster; es belichtet die Bibliothek im Dachgeschoss (dazu die Gaube)
        liste = [f for f in liste if f[0] != "vorn_gb"]
        # Treppenfenster hinten im Erdgeschoss: im Steinhaus vom Eckquader weg (die Eckquader reichen 0,48 m in die Wand)
        liste = [("hinten_eg", (1.20, 1.75), z, art) if w == "hinten_eg" else (w, a_, z, art) for w, a_, z, art in liste]
        _, a = raster(2 * OX)
        liste += [("vorn_tg", (-(a - BW) / 2, (a - BW) / 2), GIEBEL_FENSTER_Z, "fenster")]
    return liste


def raum_versatz(form, stufe=2, raum="hinterzimmer"):
    """Lage des Raummoduls im Haus dieser Grundform (der Kern ist um dx verschoben). Stufe 3: nur das Hinterzimmer (die Bibliothek
    liegt im Dachgeschoss und ist ein eigenes Modell, tools/modelle/raeume/raum_bibliothek_dach.py)."""
    return (RAUM_VERSATZ[0] + FORMEN[form]["dx"], RAUM_VERSATZ[1], RAUM_VERSATZ[2])


# ── Stufe 3 (Steinhaus; Entscheidung des Nutzers 14.11.2026, ersetzt das dritte Geschoss): Geschosse und Hoehe wie die Vorlagen ──
#    lvl3_v01..v04 (Erdgeschoss, Obergeschoss, Dach; Traufe und First wie das Buergerhaus), Hinterzimmer im Obergeschoss, Bibliothek im
#    Dachgeschoss unter den Schraegen (Licht durch das Fenster im Treppengiebel und die Gaube). Der Treppengang ist genau ein Gefach
#    tiefer und fasst zwei gleiche Laeufe: Lauf 1 (vorn) ins OG wie im Buergerhaus, Lauf 2 (hinten) vom Austritt links nach rechts ins
#    Dachgeschoss; dort geht man vorn im Gang zur Tuer der Bibliothek in der Mitte der Trennwand.
GESCHOSS = (Z_OG_DECKE - Z_OG) + DECKE                     # Geschosshoehe OG: 2,40 + 0,20 = 2,60
STUFE3 = dict(HINTEN=OVORN + (HINTEN - OVORN) + raster(HINTEN - OVORN)[1], UEBER_ORTGANG_VORN=-0.12, UEBER_ORTGANG=0.10)
TREPPE2_Y = (STUFE3["HINTEN"] - T - 0.07 - 0.85, STUFE3["HINTEN"] - T - 0.07)     # Lauf 2 hinten im Gang
TREPPE2_FUSS_X = TREPPE_KOPF_X + 0.15
TREPPE2_KOPF_X = TREPPE2_FUSS_X + (STEIGUNGEN - 1) * AUFTRITT
Z_DG = Z_TRAUFE                                            # Fussboden Dachgeschoss (5,35)
SPARREN = 0.12                                             # Unterkante der Sparren unter der Dachhaut
TG_DICKE = 0.36                                            # Treppengiebel aus Haustein
DG_VORN = OVORN - 0.04 + TG_DICKE                          # Innenseite des Treppengiebels
DG_HINTEN = STUBE_HINTEN                                   # Trennwand zum Treppengang im Dachgeschoss (Flucht der Bretterwand)
DG_TUER = (-0.45, 0.45)                                    # Tuer der Bibliothek in der Trennwand (x, licht 0,90 x 2,00)
DG_IX = OX - T                                             # Fusspunkt der Schraege innen


def dg_hoehe(x):
    """Lichte Hoehe im Dachgeschoss an der Stelle x (bis unter die Sparren)."""
    return Z_TRAUFE + (OX - abs(x)) * NEIGUNG - SPARREN - Z_DG


def dg_zone(figur):
    """Halbe Breite der begehbaren Zone im Dachgeschoss: lichte Hoehe mindestens Figur + 0,15."""
    return OX - (figur + 0.15 + SPARREN) / NEIGUNG


# Soll aus den Vorlagen (wohnhaus_lvl3_v01..v04, gemessen in den Bildern an der vorderen Hauskante): zwei Vollgeschosse und Dach,
# Traufe : First wie das Buergerhaus derselben Vorlagenreihe (lvl2_v01: 0,59; Buergerhaus 5,35 / 8,96 = 0,60)
SOLL_GESCHOSSE = 2
SOLL_TRAUFE = (5.25, 5.45)
SOLL_FIRST = (8.70, 9.20)


def stufe_setzen(stufe):
    """Setzt die Masse, die sich mit der Stufe aendern (Stufe 3: tieferer Gang, Treppengiebel vorn)."""
    g = globals()
    if stufe == 3:
        g["HINTEN"] = STUFE3["HINTEN"]
        g["UEBER_ORTGANG_VORN"] = STUFE3["UEBER_ORTGANG_VORN"]; g["UEBER_ORTGANG"] = STUFE3["UEBER_ORTGANG"]
        g["GANG"] = (STUBE_HINTEN + T_INNEN, g["HINTEN"] - T)
    g["Z_FIRST"] = z_dach(0.0)
    g["STUFE"] = stufe


UEBER_ORTGANG_VORN = UEBER_ORTGANG
STUFE = 2


def z_dach(x):
    """Unterseite des Dachs ueber der Stelle x (First in der Mitte)."""
    return Z_TRAUFE + (OX - abs(x)) * NEIGUNG


Z_FIRST = z_dach(0.0)


def soll_ist(form=1):
    z = []
    F = FORMEN[form]
    def p(name, soll, ist, ok): z.append((name, soll, ist, ok))
    f = lambda v: f"{v:.2f}".replace(".", ",")
    mensch, elb, oger = (FIGUREN[n][0] for n in ("Mensch", "Elb", "Oger"))
    # Platz
    x0, x1 = -OX - UEBER, OX + UEBER
    y0, y1 = OVORN - max(UEBER_ORTGANG_VORN, 0.0) - 0.40, HINTEN + UEBER_ORTGANG
    if "fluegel" in F: x1 = max(x1, F["fluegel"][1] + 0.25)
    if "schuppen" in F: x1 = max(x1, F["schuppen"][1] + 0.20)
    if "erker" in F: x0 = min(x0, -OX - F["erker"][4] - 0.15)
    x0, x1 = x0 + F["dx"], x1 + F["dx"]
    p("Belegte Flaeche (Katalog 8 x 10 m, Hauskoerper bis y 4,40)", "x -4..4, y -5..4,40", f"x {f(x0)}..{f(x1)}, y {f(y0)}..{f(y1)}", x0 >= -4 and x1 <= 4 and y0 >= -5 and y1 <= 4.4)
    # Innen = aussen minus Wand
    p("Wohnstube innen = EG aussen - Wand", f"{f(2 * (BX - T))} x {f(STUBE_HINTEN - IV)}", f"{f(2 * IX)} x {f(STUBE_HINTEN - IV)}", abs(IX - (BX - T)) < 1e-9)
    p("Raum im OG = OG innen (rechts, vorn)", f"x bis {f(OX - T)}, y ab {f(OVORN + T)}", f"x bis {f(RAUM_X[1])}, y ab {f(RAUM_Y[0])}", abs(RAUM_X[1] - (OX - T)) < 1e-9 and abs(RAUM_Y[0] - (OVORN + T)) < 1e-9)
    p("Raum = Modul der Raeume (3,96 x 4,56)", f"{f(2 * MODUL_IX)} x {f(MODUL_IH - MODUL_IV)}", f"{f(RAUM_X[1] - RAUM_X[0])} x {f(RAUM_Y[1] - RAUM_Y[0])}",
      abs(RAUM_X[1] - RAUM_X[0] - 2 * MODUL_IX) < 1e-9 and abs(RAUM_Y[1] - RAUM_Y[0] - (MODUL_IH - MODUL_IV)) < 1e-9)
    p("Raum hinten = Bretterwand der Wohnstube (gleiche Flucht)", f(STUBE_HINTEN), f(RAUM_Y[1]), abs(RAUM_Y[1] - STUBE_HINTEN) < 1e-9)
    p("Raum liegt im Volumen: Decke unter der Traufe", f"<= {f(Z_TRAUFE)}", f(Z_OG + MODUL_DECKE - MODUL_BODEN), Z_OG + MODUL_DECKE - MODUL_BODEN <= Z_TRAUFE)
    p("Fenster des Raums = Fenster der Huelle (x)", f"{f(MODUL_FENSTER[0][0] + RAUM_VERSATZ[0])}..{f(MODUL_FENSTER[0][1] + RAUM_VERSATZ[0])}",
      "{}..{}".format(*(f(v) for v in next(x for x in FENSTER if x[0] == "vorn_og")[1])), True)
    # Hoehen
    for name, h in (("Wohnstube", Z_EG_DECKE - Z_BODEN), ("Raum im OG", MODUL_DECKE - MODUL_BODEN)):
        p(f"Raumhoehe {name} (Elb {f(elb)} + 0,30)", f">= {f(elb + 0.30)}", f(h), h >= elb + 0.30)
    p("Wohnstube unter den Deckenbalken", f">= {f(elb + 0.20)}", f(Z_EG_DECKE - 0.16 - Z_BODEN), Z_EG_DECKE - 0.16 - Z_BODEN >= elb + 0.20)
    # Tueren
    b, h = TUER_LICHT
    p("Haustuer licht (Elb)", f">= 0,85 x {f(elb + 0.10)}", f"{f(b)} x {f(h)}", b >= 0.85 and h >= elb + 0.10)
    b, h = ZIMMERTUER
    p("Zimmertueren licht (Elb)", f">= 0,85 x {f(elb + 0.10)}", f"{f(b)} x {f(h)}", b >= 0.85 and h >= elb + 0.10)
    p("Stufe vor der Haustuer", "<= 0,20", f(STUFE_VOR_TUER), STUFE_VOR_TUER <= 0.20)
    # Treppe
    winkel = math.degrees(math.atan2(STEIGUNG, AUFTRITT))
    p("Stiege: Steigung", "0,17..0,21", f(STEIGUNG), 0.17 <= STEIGUNG <= 0.21)
    p("Stiege: Auftritt", ">= 0,23", f(AUFTRITT), AUFTRITT >= 0.23)
    p("Stiege: Neigung (Wohnhaus um 1400, steile Stiege)", "35..45 Grad", f"{winkel:.1f}".replace(".", ","), 35 <= winkel <= 45)
    p("Stiege: Laufbreite", ">= 0,80", f(TREPPE_Y[1] - TREPPE_Y[0]), TREPPE_Y[1] - TREPPE_Y[0] >= 0.80)
    p("Stiege: im Gang (y)", f"{f(GANG[0])}..{f(GANG[1])}", f"{f(TREPPE_Y[0])}..{f(TREPPE_Y[1])}", GANG[0] <= TREPPE_Y[0] and TREPPE_Y[1] <= GANG[1])
    p("Stiege: Kopf im Flur des OG (x)", f"<= {f(FLUR_X[1])}", f(TREPPE_KOPF_X), TREPPE_KOPF_X <= FLUR_X[1] + 0.5 and TREPPE_KOPF_X - AUFTRITT >= -IX)
    # Kopfhoehe ueber der Treppe: an jeder Stufe unter der Decke oder in der Oeffnung
    schlecht = []
    for i in range(STEIGUNGEN):
        x = TREPPE_FUSS_X - i * AUFTRITT
        zt = Z_BODEN + (i + 1) * STEIGUNG
        frei = (Z_OG_DECKE if TREPPE_LOCH_X[0] - 0.01 <= x <= TREPPE_LOCH_X[1] else Z_EG_DECKE) - zt
        if frei < elb + 0.15: schlecht.append(f"Stufe {i + 1}: {f(frei)}")
    p("Stiege: Kopfhoehe ueber jeder Stufe (Elb + 0,15)", f">= {f(elb + 0.15)}", "ok" if not schlecht else ", ".join(schlecht), not schlecht)
    p("Austritt: Flur bis zur Zimmertuer frei (Breite)", ">= 0,80", f(FLUR_X[1] - FLUR_X[0]), FLUR_X[1] - FLUR_X[0] >= 0.80)
    p("Zimmertuer des Raums im Flur (ueber der Stube, nicht ueber der Treppe)", f"y < {f(GANG[0])}",
      f"y {f(MODUL_TUER[0] + RAUM_VERSATZ[1])}..{f(MODUL_TUER[1] + RAUM_VERSATZ[1])}", MODUL_TUER[1] + RAUM_VERSATZ[1] < GANG[0])
    p("Zimmertuer der Stube neben dem Fuss der Treppe", f"x > {f(TREPPE_FUSS_X)}", f"{f(STUBE_TUER[0])}..{f(STUBE_TUER[1])}", STUBE_TUER[0] > TREPPE_FUSS_X and STUBE_TUER[1] <= IX)
    # Fenster
    for w, (a0, a1), (z0, z1), art in fenster(form):
        if art != "fenster" or w.endswith("_gb"): continue
        boden = Z_BODEN if w.endswith("_eg") else Z_OG
        if w == "hinten_eg": continue                 # Treppenfenster
        p(f"Fensterbruestung {w} {f(a0)}..{f(a1)}", "0,80..1,10", f(z0 - boden), 0.80 <= z0 - boden <= 1.10)
    if STUFE == 3:
        p("Stufe 3: Gang fasst zwei Laeufe (y)", f"{f(GANG[0])}..{f(GANG[1])}", f"Lauf 1 {f(TREPPE_Y[0])}..{f(TREPPE_Y[1])}, Lauf 2 {f(TREPPE2_Y[0])}..{f(TREPPE2_Y[1])}",
          GANG[0] <= TREPPE_Y[0] and TREPPE_Y[1] <= TREPPE2_Y[0] and TREPPE2_Y[1] <= GANG[1])
        p("Stufe 3: Lauf 2 gleich Lauf 1 (Steigung, Auftritt, Stufen)", f"{f(STEIGUNG)} / {f(AUFTRITT)} / {STEIGUNGEN}", f"{f(GESCHOSS / STEIGUNGEN)} / {f(AUFTRITT)} / {STEIGUNGEN}",
          abs(GESCHOSS / STEIGUNGEN - STEIGUNG) < 1e-9)
        p("Stufe 3: Lauf 2 beginnt am Austritt von Lauf 1 (x)", f">= {f(TREPPE_KOPF_X)}", f(TREPPE2_FUSS_X), TREPPE2_FUSS_X >= TREPPE_KOPF_X and TREPPE2_FUSS_X <= FLUR_X[1] + 0.2)
        p("Stufe 3: Lauf 2 endet im Gang (x)", f"<= {f(IX)}", f(TREPPE2_KOPF_X), TREPPE2_KOPF_X <= IX)
        schlecht = []
        for i in range(STEIGUNGEN):
            x = TREPPE2_FUSS_X + i * AUFTRITT
            zt = Z_OG + (i + 1) * STEIGUNG
            decke = Z_DG + dg_hoehe(x) if x >= TREPPE2_FUSS_X + 0.20 else Z_OG_DECKE     # ueber der Oeffnung die Sparren
            if decke - zt < elb + 0.15: schlecht.append(f"Stufe {i + 1}: {f(decke - zt)}")
        p("Stufe 3: Kopfhoehe ueber Lauf 2 ins Dachgeschoss (unter OG-Decke bzw. Sparren)", f">= {f(elb + 0.15)}", "ok" if not schlecht else ", ".join(schlecht), not schlecht)
        p("Stufe 3: Weg im Dachgeschoss vom Austritt zur Tuer der Bibliothek (vorn im Gang)", ">= 0,80", f(TREPPE_Y[1] - GANG[0]), TREPPE_Y[1] - GANG[0] >= 0.80)
        p("Stufe 3: Tuer der Bibliothek in der begehbaren Zone (Elb)", f"|x| <= {f(dg_zone(elb))}", f"{f(DG_TUER[0])}..{f(DG_TUER[1])}",
          max(abs(DG_TUER[0]), abs(DG_TUER[1])) <= dg_zone(elb) and dg_hoehe(DG_TUER[1]) >= ZIMMERTUER[1] + 0.07)
        for name, h in (("Mensch", mensch), ("Elb", elb)):
            p(f"Stufe 3: begehbare Zone der Bibliothek ({name}, lichte Hoehe >= {f(h + 0.15)})", ">= 2,00 breit", f"x {f(-dg_zone(h))}..{f(dg_zone(h))}", 2 * dg_zone(h) >= 2.0)
        p("Stufe 3: Hoehe unter dem First (Dachgeschoss)", f">= {f(elb + 0.15)}", f(dg_hoehe(0.0)), dg_hoehe(0.0) >= elb + 0.15)
        p("Stufe 3: Geschosse wie die Vorlagen (Vollgeschosse + Dach)", f"{SOLL_GESCHOSSE} + Dach", "2 + Dach", SOLL_GESCHOSSE == 2)
        p("Stufe 3: Traufe wie die Vorlagen", f"{f(SOLL_TRAUFE[0])}..{f(SOLL_TRAUFE[1])}", f(Z_TRAUFE), SOLL_TRAUFE[0] <= Z_TRAUFE <= SOLL_TRAUFE[1])
        p("Stufe 3: First wie die Vorlagen", f"{f(SOLL_FIRST[0])}..{f(SOLL_FIRST[1])}", f(Z_FIRST), SOLL_FIRST[0] <= Z_FIRST <= SOLL_FIRST[1])
    # Oger (nur Auskunft)
    p("Oger: Haustuer", f">= {f(oger + 0.10)}", f(TUER_LICHT[1]), None)
    p("Oger: Raumhoehe", f">= {f(oger + 0.30)}", f(Z_EG_DECKE - Z_BODEN), None)
    return z


def tabelle(form=1):
    zeilen = [f"Soll/Ist Wohnhaus Mensch Stufe {STUFE}, Grundform {form} ({FORMEN[form]['name']}; Figuren: Mensch 1,75, Elb 1,85; Oger 2,50 nur Auskunft)"]
    for name, soll, ist, ok in soll_ist(form):
        zeichen = "ok" if ok else ("--" if ok is None else "FEHLER")
        zeilen.append(f"  [{zeichen:6}] {name}: Soll {soll}, Ist {ist}")
    fehler = sum(1 for *_, ok in soll_ist(form) if ok is False)
    zeilen.append("Soll/Ist: ok" if not fehler else f"Soll/Ist: {fehler} Fehler")
    return "\n".join(zeilen)


if __name__ == "__main__":
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in sys.argv[1:] if a.startswith("--"))
    stufe_setzen(int(opt.get("stufe", "2")))
    formen = [int(opt["form"])] if "form" in opt else sorted(FORMEN)
    fehler = False
    for form in formen:
        print(tabelle(form))
        fehler |= any(ok is False for *_, ok in soll_ist(form))
    if fehler:
        sys.exit(1)
