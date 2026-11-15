"""Neubau Rathaus: alle Masse an einer Stelle (Aussen, sieben Innenraeume, Oeffnungen, Treppen, Plaetze) - reines Python.

    python tools/modelle/neubau/rathaus_masse.py [--grundriss=<ordner>] [--soll-ist]

Die Blender-Skripte (rathaus_aussen.py, rathaus_<raum>.py) lesen ihre Masse von hier; damit sind innen und aussen
dieselben Waende, Oeffnungen und Geschosse ("innen = aussen minus Wandstaerke"). --soll-ist rechnet die Pruefungen aus
(Figurenmasse aller Voelker bis zum Oger, Treppen, Tueren, Grundflaeche) und gibt eine Tabelle aus; --grundriss zeichnet je
Geschoss eine Skizze (PNG, matplotlib). Was nur aus der fertigen GLB zu messen ist (Dreiecke, Lage der Anker, Durchdringung
der Sitzenden), prueft rathaus_messen.py.

Koordinaten wie bei allen Bauskripten: X rechts, Y nach hinten (Markt bei -Y, im Spiel +Z), Z oben; Ursprung in der Mitte der
belegten Flaeche (Katalog "rathaus": 16 x 10 m).
"""
import math
import os
import sys

# ── Figuren (Hoehe in Metern, Schulterbreite) ───────────────────────────────────────────────────────────────────────
# Aus den Massstaeben der Figuren (tools/modelle/sitzpruefung.py im Branch phase5/figuren-baukasten, FIGUREN_ARTEN):
# Mensch 1,75; Elb 1,85; Zwerg 1,35; Halbling 1,10; Ork 1,1 x 1,75; Oger 1,35 x 1,75 = 2,36. Der Auftrag nennt fuer den Oger
# 1,7-2,5 m: gerechnet wird mit 2,50 als oberer Grenze.
FIGUREN = {
    "Halbling": (1.10, 0.32), "Zwerg": (1.35, 0.46), "Mensch": (1.75, 0.46), "Elb": (1.85, 0.44),
    "Ork": (1.93, 0.52), "Oger": (2.50, 0.66),
}
GROESSTE = max(h for h, _ in FIGUREN.values())          # 2,50
BREITESTE = max(b for _, b in FIGUREN.values())         # 0,66

# ── Geschosse ───────────────────────────────────────────────────────────────────────────────────────────────────────
Z_SOCKEL = 0.30          # Fussboden Erdgeschoss (Sockel 12 cm eingelassen, Oberkante 0,30)
Z_EG_DECKE = 4.30        # Unterkante der Decke ueber dem Erdgeschoss (4,00 licht)
Z_OG = 4.60              # Fussboden Obergeschoss
Z_TRAUFE = 8.40          # Oberkante der Obergeschosswaende = Decke der Ratshalle (3,80 licht)
BALKEN_H = 0.24          # Deckenbalken der Ratshalle (unter der Decke)
# Deckenbalken (Lage quer zur Spannrichtung) - die Raumskripte bauen sie von hier, die Pruefung AUFLAGER sieht nach, dass ihre Auflager und
# Konsolen auf Mauerwerk sitzen und nicht in einer Fensteroeffnung (Abstand zur Laibung mindestens AUFLAGER_ABSTAND).
RATSHALLE_BALKEN = (-6.90, -4.80, -2.70, -1.10, 1.05, 3.10)     # x, Balken laufen in y; Konsole an Vorder- und Rueckwand
KONSOLE = (0.09, 0.95)                                           # halbe Breite, Hoehe unter dem Balken
HALLE_BALKEN = tuple(round(-2.05 + 0.92 * i, 2) for i in range(6))        # x, Balken in y, liegen auf der Vorderwand auf
GERICHT_BALKEN = tuple(round(-3.15 + 1.05 * i, 2) for i in range(7))      # y, Balken in x, liegen auf der linken Wand auf
AUFLAGER_ABSTAND = 0.15
Z_KERKER = -3.10         # Fussboden des Kerkers
Z_KERKER_DECKE = -0.30   # Unterkante der Decke ueber dem Kerker (2,80 licht)

# ── Hauptbau (Saalbau) ──────────────────────────────────────────────────────────────────────────────────────────────
T_STEIN = 0.60           # Erdgeschoss, Turm: Bruchstein mit Quaderecken
T_FACHWERK = 0.25        # Obergeschoss, Anbau, Trennwand im Erdgeschoss
KRAG = 0.25              # Auskragung des Obergeschosses vorn, hinten, links
EG_X = (-7.40, 3.60)     # Aussenkanten Erdgeschoss
EG_Y = (-4.30, 4.30)
OG_X = (EG_X[0] - KRAG, EG_X[1])   # rechts keine Auskragung (dort stehen Turm und Anbau)
OG_Y = (EG_Y[0] - KRAG, EG_Y[1] + KRAG)
NEIGUNG = math.tan(math.radians(52))
UEBER_TRAUFE = 0.33      # Dachueberstand an der Traufe (waagerecht; die Alpha-Cards ragen noch knapp 0,09 weiter)
UEBER_ORTGANG = 0.30
Z_FIRST = Z_TRAUFE + (OG_Y[1] - OG_Y[0]) / 2 * NEIGUNG
GAUBEN_X = (-5.00, -1.50, 2.00)    # drei Gauben auf der Marktseite (Dachboden, kein Raum)
SCHORNSTEIN = (-7.25, -6.55, 0.20, 0.90)   # x0, x1, y0, y1: vom Kamin der Ratshalle an der linken Giebelwand

# Trennwand Gerichtssaal | Eingangshalle (Fachwerk auf Steinsockel), Wand zwischen Eingangshalle und Turm/Anbau (Stein)
TRENN_X = (-2.75, -2.50)
RECHTS_X = (3.00, 3.60)

# ── Turm (vorn rechts, steht 0,30 vor und 0,20 seitlich ueber) ──────────────────────────────────────────────────────
TURM_X = (3.60, 7.80)
TURM_Y = (-4.60, -0.40)
Z_TURM_WACHE = 4.60      # Holzboden mit Waffenstaender (Ende der Wendeltreppe)
Z_TURM_UHR = 8.60        # Buehne des Uhrwerks
Z_UHR_MITTE = 9.80       # Mitte der Zifferblaetter (vorn und rechts)
UHR_R = 0.80
Z_GLOCKE = 11.60         # Boden der Glockenstube (Turm-Spitze)
Z_TURM_MAUER = 15.20     # Oberkante der Turmmauern
HELM_H = 6.00            # Pyramidenhelm
UEBER_HELM = 0.08
Z_HELM_SPITZE = Z_TURM_MAUER + HELM_H
Z_FAHNE = Z_HELM_SPITZE + 0.80    # Knauf und Wetterfahne
BODEN_DICKE = 0.15       # Holzboeden im Turm

# ── Anbau der Amtsstube (hinter dem Turm, eingeschossig, Pultdach gegen den Hauptbau) ───────────────────────────────
ANBAU_X = (3.60, 7.60)
ANBAU_Y = (TURM_Y[1], EG_Y[1])
Z_ANBAU_TRAUFE = 3.60    # Oberkante der Fachwerkwand rechts
PULT_NEIGUNG = math.tan(math.radians(30))
Z_ANBAU_FIRST = Z_ANBAU_TRAUFE + (ANBAU_X[1] - ANBAU_X[0]) * PULT_NEIGUNG   # am Hauptbau


def anbau_dach_z(x):
    """Unterseite des Pultdachs ueber x (Aussenkante der Wand rechts: Z_ANBAU_TRAUFE)."""
    return Z_ANBAU_TRAUFE + (ANBAU_X[1] - x) * PULT_NEIGUNG


# ── Innenraeume (lichte Masse) ──────────────────────────────────────────────────────────────────────────────────────
# Name -> (x0, x1, y0, y1, z_boden, z_decke, Geschoss, Kennung im Spiel)
RAEUME = {
    "eingangshalle": (TRENN_X[1], RECHTS_X[0], EG_Y[0] + T_STEIN, EG_Y[1] - T_STEIN, Z_SOCKEL, Z_EG_DECKE, "EG", "rathaus:halle"),
    "gerichtssaal": (EG_X[0] + T_STEIN, TRENN_X[0], EG_Y[0] + T_STEIN, EG_Y[1] - T_STEIN, Z_SOCKEL, Z_EG_DECKE, "EG", "rathaus:gericht"),
    "amtsstube": (RECHTS_X[1], ANBAU_X[1] - T_FACHWERK, ANBAU_Y[0], ANBAU_Y[1] - T_FACHWERK, Z_SOCKEL, Z_ANBAU_TRAUFE - 0.15, "EG", "rathaus:amt"),
    "kerker": (TRENN_X[1], RECHTS_X[0], EG_Y[0] + T_STEIN, EG_Y[1] - T_STEIN, Z_KERKER, Z_KERKER_DECKE, "Keller", "rathaus:kerker"),
    "ratshalle": (OG_X[0] + T_FACHWERK, OG_X[1] - T_FACHWERK, OG_Y[0] + T_FACHWERK, OG_Y[1] - T_FACHWERK, Z_OG, Z_TRAUFE, "OG", "rathaus:rat"),
    "turm_treppe": (TURM_X[0] + T_STEIN, TURM_X[1] - T_STEIN, TURM_Y[0] + T_STEIN, TURM_Y[1] - T_STEIN, Z_SOCKEL, Z_GLOCKE - BODEN_DICKE, "Turm", "rathaus:turm"),
    "turm_spitze": (TURM_X[0] + T_STEIN, TURM_X[1] - T_STEIN, TURM_Y[0] + T_STEIN, TURM_Y[1] - T_STEIN, Z_GLOCKE, Z_TURM_MAUER, "Turm", "rathaus:spitze"),
}
RAUM_TITEL = {"eingangshalle": "Eingangshalle", "gerichtssaal": "Gerichtssaal", "amtsstube": "Amtsstube", "kerker": "Kerker",
              "ratshalle": "Ratshalle", "turm_treppe": "Turm-Treppe", "turm_spitze": "Turm-Spitze"}

# ── Aussenwaende: Name -> (Ursprung aussen (x, y), Richtung, Laenge, Normale nach aussen) ───────────────────────────
WAENDE = {
    "vorn_eg": ((EG_X[0], EG_Y[0]), (1, 0), EG_X[1] - EG_X[0], (0, -1)),
    "hinten_eg": ((EG_X[0], EG_Y[1]), (1, 0), EG_X[1] - EG_X[0], (0, 1)),
    "links_eg": ((EG_X[0], EG_Y[0]), (0, 1), EG_Y[1] - EG_Y[0], (-1, 0)),
    "vorn_og": ((OG_X[0], OG_Y[0]), (1, 0), OG_X[1] - OG_X[0], (0, -1)),
    "hinten_og": ((OG_X[0], OG_Y[1]), (1, 0), OG_X[1] - OG_X[0], (0, 1)),
    "links_og": ((OG_X[0], OG_Y[0]), (0, 1), OG_Y[1] - OG_Y[0], (-1, 0)),
    "rechts_og": ((OG_X[1], OG_Y[0]), (0, 1), OG_Y[1] - OG_Y[0], (1, 0)),
    "turm_vorn": ((TURM_X[0], TURM_Y[0]), (1, 0), TURM_X[1] - TURM_X[0], (0, -1)),
    "turm_rechts": ((TURM_X[1], TURM_Y[0]), (0, 1), TURM_Y[1] - TURM_Y[0], (1, 0)),
    "turm_hinten": ((TURM_X[0], TURM_Y[1]), (1, 0), TURM_X[1] - TURM_X[0], (0, 1)),
    "turm_links": ((TURM_X[0], TURM_Y[0]), (0, 1), TURM_Y[1] - TURM_Y[0], (-1, 0)),
    "anbau_rechts": ((ANBAU_X[1], ANBAU_Y[0]), (0, 1), ANBAU_Y[1] - ANBAU_Y[0], (1, 0)),
    "anbau_hinten": ((ANBAU_X[0], ANBAU_Y[1]), (1, 0), ANBAU_X[1] - ANBAU_X[0], (0, 1)),
}
WAND_DICKE = {w: (T_FACHWERK if w.endswith("_og") or w.startswith("anbau") else T_STEIN) for w in WAENDE}

# ── Oeffnungen der Aussenwaende ─────────────────────────────────────────────────────────────────────────────────────
# (Wand, (a0, a1) in Weltkoordinaten entlang der Wand, (z0, z1), Art, Raum dahinter). Arten: "portal" (Rundbogen mit Tuer),
# "bogenfenster" (Rundbogen, Butzenglas, z1 = Scheitel), "fenster" (Rechteck, Butzenglas, Laeden), "schlitz" (Turm),
# "schall" (Schallarkade der Glockenstube, offen, Rundbogen).
ARKADE = [(-2.50, -1.20), (-0.40, 0.90), (1.70, 3.00)]     # drei Rundboegen der Eingangshalle, je 1,30 licht
Z_KAEMPFER = Z_SOCKEL + 2.55                                # 2,85: Kaempfer der Arkade (Tuer bis hierhin 2,55 licht)
OG_FENSTER = (Z_OG + 0.90, Z_OG + 2.80)                     # Bruestung 0,90 ueber dem Saalboden, 1,90 hoch
OEFFNUNGEN = [
    ("vorn_eg", ARKADE[0], (Z_SOCKEL + 0.95, Z_KAEMPFER + 0.65), "bogenfenster", "eingangshalle"),
    ("vorn_eg", ARKADE[1], (Z_SOCKEL, Z_KAEMPFER + 0.65), "portal", "eingangshalle"),
    ("vorn_eg", ARKADE[2], (Z_SOCKEL + 0.95, Z_KAEMPFER + 0.65), "bogenfenster", "eingangshalle"),
    ("vorn_eg", (-5.25, -4.25), (Z_SOCKEL + 1.10, Z_SOCKEL + 3.10), "bogenfenster", "gerichtssaal"),
    ("links_eg", (-2.65, -1.75), (Z_SOCKEL + 1.10, Z_SOCKEL + 3.10), "bogenfenster", "gerichtssaal"),
    ("links_eg", (-0.45, 0.45), (Z_SOCKEL + 1.10, Z_SOCKEL + 3.10), "bogenfenster", "gerichtssaal"),
    ("links_eg", (1.75, 2.65), (Z_SOCKEL + 1.10, Z_SOCKEL + 3.10), "bogenfenster", "gerichtssaal"),
    ("vorn_og", (-6.40, -5.40), OG_FENSTER, "fenster", "ratshalle"),
    ("vorn_og", (-4.20, -3.20), OG_FENSTER, "fenster", "ratshalle"),
    ("vorn_og", (-0.70, 0.30), OG_FENSTER, "fenster", "ratshalle"),
    ("vorn_og", (1.80, 2.80), OG_FENSTER, "fenster", "ratshalle"),
    # hinten auf denselben Achsen wie vorn: so finden die Konsolen der Deckenbalken auf beiden Waenden Mauerwerk (RATSHALLE_BALKEN)
    ("hinten_og", (-6.40, -5.40), OG_FENSTER, "fenster", "ratshalle"),
    ("hinten_og", (-4.20, -3.20), OG_FENSTER, "fenster", "ratshalle"),
    ("hinten_og", (1.80, 2.80), OG_FENSTER, "fenster", "ratshalle"),
    ("links_og", (-3.10, -2.10), OG_FENSTER, "fenster", "ratshalle"),
    ("links_og", (2.10, 3.10), OG_FENSTER, "fenster", "ratshalle"),
    ("anbau_rechts", (1.20, 2.10), (Z_SOCKEL + 1.00, Z_SOCKEL + 2.30), "fenster", "amtsstube"),
    ("anbau_hinten", (5.00, 5.90), (Z_SOCKEL + 1.00, Z_SOCKEL + 2.30), "fenster", "amtsstube"),
    ("turm_vorn", (5.55, 5.85), (Z_SOCKEL + 1.90, Z_SOCKEL + 2.80), "schlitz", "turm_treppe"),
    ("turm_rechts", (-2.65, -2.35), (Z_TURM_WACHE + 1.40, Z_TURM_WACHE + 2.30), "schlitz", "turm_treppe"),
    ("turm_vorn", (5.55, 5.85), (Z_TURM_WACHE + 1.40, Z_TURM_WACHE + 2.30), "schlitz", "turm_treppe"),
] + [(w, s, (Z_GLOCKE + 0.80, Z_GLOCKE + 2.90), "schall", "turm_spitze")
     for w, s in (("turm_vorn", (5.20, 6.20)), ("turm_rechts", (-3.00, -2.00)), ("turm_hinten", (5.20, 6.20)), ("turm_links", (-3.00, -2.00)))]
ZIFFERBLAETTER = [("turm_vorn", 5.70), ("turm_rechts", -2.50)]    # Mitte entlang der Wand, Hoehe Z_UHR_MITTE

# ── Innentueren und Durchgaenge ─────────────────────────────────────────────────────────────────────────────────────
TUER_LICHT = (1.20, 2.60)    # Breite, Hoehe (bis zum Scheitel des flachen Bogens bzw. Sturz)
# Name -> (von, nach, Wandachse, Lage, Ebene): Achse "x" = Wand quer zur X-Achse (Lage = Mitte in y), "y" = Wand quer zur Y-Achse
TUEREN = {
    "Tuer_Halle_Gericht": ("eingangshalle", "gerichtssaal", "x", (TRENN_X, (-1.60, -0.40)), Z_SOCKEL),
    "Tuer_Halle_Turm": ("eingangshalle", "turm_treppe", "x", ((RECHTS_X[0], TURM_X[0] + T_STEIN), (-2.80, -1.60)), Z_SOCKEL),
    "Tuer_Halle_Amt": ("eingangshalle", "amtsstube", "x", (RECHTS_X, (0.40, 1.60)), Z_SOCKEL),
    "Tuer_Halle_Kerker": ("eingangshalle", "kerker", "y", ((1.80, 3.00), (2.45, 2.50)), Z_SOCKEL),
    "Tuer_Halle_Stadt": ("eingangshalle", "stadt", "y", (ARKADE[1], (EG_Y[0], EG_Y[0] + T_STEIN)), Z_SOCKEL),
}
# Durchgang ohne Tuer: Treppen und Luke
UEBERGAENGE = {
    "Treppe_Halle_Rat": ("eingangshalle", "ratshalle"),
    "Treppe_Kerker_Halle": ("kerker", "eingangshalle"),
    "Leiter_Turm_Spitze": ("turm_treppe", "turm_spitze"),
}

# ── Treppen ─────────────────────────────────────────────────────────────────────────────────────────────────────────
# Haupttreppe in der Eingangshalle: Lauf 1 an der Trennwand nach hinten, Podest in der Ecke hinten links, Lauf 2 an der
# Rueckwand nach rechts; kommt in der Ratshalle hinten rechts an.
TREPPE_BREITE = 1.20
H_STUFEN = 22
H_STEIG = (Z_OG - Z_SOCKEL) / H_STUFEN           # 0,1955
H_AUFTRITT = 0.28
H_LAUF1 = 9                                      # Stufen bis zum Podest
H_LAUF1_Y0 = RAEUME["eingangshalle"][3] - TREPPE_BREITE - (H_LAUF1 - 1) * H_AUFTRITT   # Antritt
H_PODEST = (TRENN_X[1], TRENN_X[1] + TREPPE_BREITE, RAEUME["eingangshalle"][3] - TREPPE_BREITE, RAEUME["eingangshalle"][3])
H_LAUF2_X1 = H_PODEST[1] + (H_STUFEN - H_LAUF1 - 1) * H_AUFTRITT                       # Austritt in der Ratshalle
# Treppenloch in der Decke (wo die Kopfhoehe ueber der Stufe sonst unter 2,60 faellt) und Gelaender in der Ratshalle
TREPPENLOCH = (TRENN_X[1], H_LAUF2_X1, 2.00, RAEUME["eingangshalle"][3])
# Kellertreppe unter Lauf 2: von der Kellertuer (hinten rechts) an der Rueckwand nach links hinab
K_STUFEN = 17
K_STEIG = (Z_SOCKEL - Z_KERKER) / K_STUFEN       # 0,20
K_AUFTRITT = 0.25
K_X0 = 1.80                                      # Kante des oberen Podests (Tuer x 1,80-3,00)
K_X1 = K_X0 - (K_STUFEN - 1) * K_AUFTRITT        # Fuss der Treppe
KELLERLOCH = (K_X1, K_X0, RAEUME["eingangshalle"][3] - TREPPE_BREITE, RAEUME["eingangshalle"][3])
# Wendeltreppe im Turm (Stein) durchgehend bis zur Uhrbuehne, Leiter zur Glockenstube. (Der Plan hatte bei 4,60 eine Wachstube mit
# Holztreppe: Im Schacht von 3 x 3 m bliebe ueber der Wendeltreppe kein Boden mit Kopfhoehe - sie entfaellt, die Wendel laeuft durch.)
W_MITTE = ((TURM_X[0] + TURM_X[1]) / 2, (TURM_Y[0] + TURM_Y[1]) / 2)
W_SPINDEL, W_AUSSEN, W_LAUFLINIE = 0.15, 1.45, 0.80
W_STUFEN = 42
W_STEIG = (Z_TURM_UHR - Z_SOCKEL) / W_STUFEN
W_WINKEL = math.radians(20.5)
W_START = math.radians(180)                 # erste Stufe an der Tuer (Westseite), dann gegen den Uhrzeigersinn
W_ENDE = W_START + W_STUFEN * W_WINKEL      # Austritt auf die Uhrbuehne
BUEHNE_WINKEL = math.radians(60)            # die Buehne deckt hinter dem Austritt nur so viel der Wendel (Kopfhoehe darunter)
LEITER = (Z_GLOCKE - Z_TURM_UHR, math.radians(75), 0.28, 0.55)    # Hoehe, Neigung, Sprossenabstand, Breite
LUKE = (1.20, 1.20)
LUKE_LAGE = (6.00, 7.20, -2.90, -1.70)      # x0, x1, y0, y1 im Boden der Glockenstube
LEITER_FUSS = (6.60, -3.10)                 # auf der Buehne; oben bei y -2,30 in der Luke

# ── Ratshalle: Sitzung (Buergermeister und sechs Ratsherren), Schreiber, Redner, Zuhoererbank ────────────────────────
SITZ_HOEHE = 0.44        # Regel des Figuren-Baukastens (Sitzpunkt, sitzpruefung.py "neu")
STUHL_BREITE = 0.62
TISCH_HOEHE = 0.76
TISCH_TIEFE = 0.80
KNIERAUM = 0.70          # Unterkante der Tischplatte ueber dem Boden (Tafel ohne Zarge: der Rocksaum des Ogers liegt bei 0,68)
SITZ_ABSTAND = 1.20
STUHL_VOR = 0.35        # Fusspunkt der Sitzenden vor der Tischkante
STUHL_HINTEN = 0.45     # Lehne hinter dem Fusspunkt
# U-Tafel: Kopf zum Podest rechts (Buergermeister), zwei Schenkel nach links; Stuehle aussen, Blick zur Tafelmitte
U_MITTE_Y = -1.15
U_SCHENKEL_Y = (U_MITTE_Y - 1.00, U_MITTE_Y + 1.00)      # Mittellinie der beiden Schenkel
U_X = (-3.20, 1.00)                                      # Schenkel von links bis zum Kopftisch
PODEST = (1.90, 3.35, U_MITTE_Y - 1.20, U_MITTE_Y + 1.20, 0.30)   # x0, x1, y0, y1, Hoehe
SITZE_X = (0.25, -1.10, -2.45)                            # drei Stuehle je Schenkel, mittig zwischen den Tischbeinen (Fugen bei -1,80 und -0,40)
SITZ_VORDERKANTE = 0.18  # die Sitzflaeche reicht so weit vor den Sitzpunkt (sitzpruefung.py: KANTE "Ratssitzung"; Thron 0,19)


def ratsplaetze():
    """Plaetze der Sitzung: Sitzpunkt (Huefte ueber diesem Punkt, Regel des Figuren-Baukastens) x, y, Boden z, Blick in Grad um Z
    (0 = +Y, 90 = +X), Rang. Buergermeister zuerst (auf dem Podest, Blick die Tafel entlang nach -X), dann je Schenkel."""
    p = [("Anker_Buergermeister", (2.80, U_MITTE_Y, Z_OG + PODEST[4]), -90.0, 1)]
    nummer = 1
    for x in SITZE_X:
        for seite, y in ((-1, U_SCHENKEL_Y[0] - TISCH_TIEFE / 2 - STUHL_VOR), (1, U_SCHENKEL_Y[1] + TISCH_TIEFE / 2 + STUHL_VOR)):
            p.append((f"Anker_Sitz_{nummer}", (x, y, Z_OG), 0.0 if seite < 0 else 180.0, nummer + 1))
            nummer += 1
    return p


SCHREIBER = ((-4.70, U_SCHENKEL_Y[0] - 0.75, Z_OG), 90.0)      # Pult vor dem vorderen Schenkel, Blick zur Tafel
REDNER = ((-4.30, U_MITTE_Y, Z_OG), 90.0)                     # an der offenen Seite der Tafel, Blick zum Buergermeister
BANK = [((-6.55 + i * 0.70, 3.80, Z_OG), 180.0) for i in range(4)]   # Zuhoererbank an der Rueckwand links, Blick in den Saal


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Soll gegen Ist (Rechnung)
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def soll_ist():
    """Liste aus (Pruefung, Soll, Ist, ok). Ist aus den Konstanten oben gerechnet."""
    z = []
    def p(name, soll, ist, ok): z.append((name, soll, ist, ok))
    f = lambda v: f"{v:.2f}".replace(".", ",")
    # Grundflaeche mit Dachueberstand und Helm
    x0 = min(OG_X[0] - UEBER_ORTGANG, EG_X[0]); x1 = TURM_X[1] + UEBER_HELM
    y0 = min(OG_Y[0] - UEBER_TRAUFE, TURM_Y[0] - UEBER_HELM); y1 = OG_Y[1] + UEBER_TRAUFE
    p("Belegte Flaeche (Katalog 16 x 10 m)", "<= 16,00 x 10,00", f"{f(x1 - x0)} x {f(y1 - y0)}", x1 - x0 <= 16.0 and y1 - y0 <= 10.0)
    p("Hoehe Wetterfahne (Blitz 22 m)", "<= 22,0", f(Z_FAHNE), Z_FAHNE <= 22.05)
    # Innen = aussen minus Wand
    eh, gs, am, ke, rh, tt, ts = (RAEUME[n] for n in ("eingangshalle", "gerichtssaal", "amtsstube", "kerker", "ratshalle", "turm_treppe", "turm_spitze"))
    p("Gerichtssaal links = Aussenwand + Stein", f(EG_X[0] + T_STEIN), f(gs[0]), abs(gs[0] - (EG_X[0] + T_STEIN)) < 1e-6)
    p("EG vorn/hinten = Aussenwand + Stein", f"{f(EG_Y[0] + T_STEIN)} / {f(EG_Y[1] - T_STEIN)}", f"{f(eh[2])} / {f(eh[3])}", abs(eh[2] - (EG_Y[0] + T_STEIN)) < 1e-6)
    p("Ratshalle = OG aussen - Fachwerk", f"{f(OG_X[0] + T_FACHWERK)}..{f(OG_X[1] - T_FACHWERK)} x {f(OG_Y[0] + T_FACHWERK)}..{f(OG_Y[1] - T_FACHWERK)}",
      f"{f(rh[0])}..{f(rh[1])} x {f(rh[2])}..{f(rh[3])}", True)
    p("Turm innen = aussen - 2 x Stein", f(TURM_X[1] - TURM_X[0] - 2 * T_STEIN), f(tt[1] - tt[0]), abs((tt[1] - tt[0]) - (TURM_X[1] - TURM_X[0] - 2 * T_STEIN)) < 1e-6)
    p("Amtsstube = Anbau - Fachwerk", f"{f(ANBAU_X[1] - T_FACHWERK - RECHTS_X[1])} x {f(ANBAU_Y[1] - T_FACHWERK - ANBAU_Y[0])}", f"{f(am[1] - am[0])} x {f(am[3] - am[2])}", True)
    p("Kerker unter der Eingangshalle (gleiche Waende)", f"{f(eh[0])}..{f(eh[1])}", f"{f(ke[0])}..{f(ke[1])}", ke[:4] == eh[:4])
    # Geschosse
    for name, r in (("Eingangshalle", eh), ("Gerichtssaal", gs), ("Kerker", ke), ("Ratshalle", rh), ("Turm-Spitze", ts)):
        p(f"Raumhoehe {name} (Oger {f(GROESSTE)} + 0,30)", f">= {f(GROESSTE + 0.30)}", f(r[5] - r[4]), r[5] - r[4] >= GROESSTE + 0.30)
    p("Ratshalle unter den Deckenbalken", f">= {f(GROESSTE + 0.30)}", f(Z_TRAUFE - BALKEN_H - Z_OG), Z_TRAUFE - BALKEN_H - Z_OG >= GROESSTE + 0.30)
    p("Amtsstube an der niedrigen Wand (Pultdach)", f">= {f(GROESSTE + 0.30)}", f(anbau_dach_z(am[1]) - 0.15 - Z_SOCKEL), anbau_dach_z(am[1]) - 0.15 - Z_SOCKEL >= GROESSTE + 0.30)
    kopf_b = (2 * math.pi - BUEHNE_WINKEL) / W_WINKEL * W_STEIG - BODEN_DICKE
    p("Turm: Kopfhoehe unter der Uhrbuehne (ueber der Wendel)", f">= {f(GROESSTE + 0.10)}", f(kopf_b), kopf_b >= GROESSTE + 0.10)
    p("Turm: Uhrbuehne bis Glockenboden", f">= {f(GROESSTE + 0.30)}", f(Z_GLOCKE - BODEN_DICKE - Z_TURM_UHR), Z_GLOCKE - BODEN_DICKE - Z_TURM_UHR >= GROESSTE + 0.30)
    p("Geschosse: EG-Boden / OG-Boden / Traufe", "0,30 / 4,60 / 8,40", f"{f(Z_SOCKEL)} / {f(Z_OG)} / {f(Z_TRAUFE)}", True)
    # Tueren
    b, h = TUER_LICHT
    p("Innentueren licht (Oger)", f">= {f(BREITESTE + 0.44)} x {f(GROESSTE + 0.10)}", f"{f(b)} x {f(h)}", b >= BREITESTE + 0.44 and h >= GROESSTE + 0.10)
    a0, a1 = ARKADE[1]
    p("Portal licht bis Kaempfer / Scheitel", f">= {f(BREITESTE + 0.44)} x {f(GROESSTE + 0.10)}", f"{f(a1 - a0)} x {f(Z_KAEMPFER - Z_SOCKEL)} / {f(Z_KAEMPFER + 0.65 - Z_SOCKEL)}",
      a1 - a0 >= BREITESTE + 0.44 and Z_KAEMPFER - Z_SOCKEL >= GROESSTE + 0.05)
    p("Luke zur Glockenstube", f">= {f(BREITESTE + 0.44)} x {f(BREITESTE + 0.44)}", f"{f(LUKE[0])} x {f(LUKE[1])}", min(LUKE) >= BREITESTE + 0.44)
    # Treppen
    for name, steig, auftritt in (("Haupttreppe", H_STEIG, H_AUFTRITT), ("Kellertreppe", K_STEIG, K_AUFTRITT),
                                  ("Wendeltreppe (Lauflinie 0,80)", W_STEIG, W_LAUFLINIE * W_WINKEL)):
        s = 2 * steig + auftritt
        p(f"{name}: Steigung / Auftritt / Schrittmass", "<= 0,21 / >= 0,25 / 0,59-0,69", f"{f(steig)} / {f(auftritt)} / {f(s)}",
          steig <= 0.21 and auftritt >= 0.25 and 0.59 <= s <= 0.69)
    p("Laufbreite Haupt- und Kellertreppe", f">= {f(BREITESTE + 0.44)}", f(TREPPE_BREITE), TREPPE_BREITE >= BREITESTE + 0.44)
    p("Wendeltreppe Laufbreite", f">= {f(BREITESTE + 0.44)}", f(W_AUSSEN - W_SPINDEL), W_AUSSEN - W_SPINDEL >= BREITESTE + 0.44)
    # Kopfhoehe: Haupttreppe unter der Decke ausserhalb des Treppenlochs (letzte Stufe vor dem Loch)
    erste_im_loch = next(i for i in range(1, H_STUFEN + 1) if H_LAUF1_Y0 + (i - 1) * H_AUFTRITT >= TREPPENLOCH[2] or i > H_LAUF1)
    z_vor = Z_SOCKEL + (erste_im_loch - 1) * H_STEIG
    p("Haupttreppe Kopfhoehe vor dem Treppenloch", f">= {f(GROESSTE + 0.10)}", f(Z_EG_DECKE - z_vor), Z_EG_DECKE - z_vor >= GROESSTE + 0.10)
    kopf_w = (2 * math.pi / W_WINKEL) * W_STEIG
    p("Wendeltreppe Kopfhoehe nach einer Windung", f">= {f(GROESSTE + 0.10)}", f(kopf_w), kopf_w >= GROESSTE + 0.10)
    # Kellertreppe unter Lauf 2 der Haupttreppe: kleinste Kopfhoehe (Unterseite Lauf 2 minus Stufe darunter)
    kopf_k = min((H_PODEST[1] < x and Z_SOCKEL + (H_LAUF1 + (x - H_PODEST[1]) / H_AUFTRITT) * H_STEIG - 0.25 or Z_SOCKEL + H_LAUF1 * H_STEIG - 0.25)
                 - (Z_SOCKEL - (K_X0 - x) / K_AUFTRITT * K_STEIG) for x in [K_X1 + i * 0.05 for i in range(int((K_X0 - K_X1) / 0.05) + 1)])
    p("Kellertreppe Kopfhoehe unter der Haupttreppe", f">= {f(GROESSTE + 0.10)}", f(kopf_k), kopf_k >= GROESSTE + 0.10)
    p("Kellertuer unter Lauf 2 (Unterseite ueber der Tuer)", f">= {f(Z_SOCKEL + TUER_LICHT[1])}",
      f(Z_SOCKEL + (H_LAUF1 + (K_X0 - H_PODEST[1]) / H_AUFTRITT) * H_STEIG - 0.25), Z_SOCKEL + (H_LAUF1 + (K_X0 - H_PODEST[1]) / H_AUFTRITT) * H_STEIG - 0.25 >= Z_SOCKEL + TUER_LICHT[1])
    p("Haupttreppe passt (Austritt vor der rechten Wand)", f"<= {f(RAEUME['eingangshalle'][1])}", f(H_LAUF2_X1), H_LAUF2_X1 <= RAEUME["eingangshalle"][1] - 0.6)
    p("Kellertreppe passt (Fuss vor der Trennwand)", f">= {f(TRENN_X[1])}", f(K_X1), K_X1 >= TRENN_X[1])
    hoehe_l, neig, sprosse, breite = LEITER
    p("Leiter zur Glockenstube: Neigung / Sprossen / Breite", "65-75 Grad / 0,25-0,30 / >= 0,50", f"{math.degrees(neig):.0f} / {f(sprosse)} / {f(breite)}", 65 <= math.degrees(neig) <= 75)
    # Sitzung
    pl = ratsplaetze()
    p("Sitze der Sitzung (Buergermeister + 6)", "7", str(len(pl)), len(pl) == 7)
    p("Sitzhoehe / Tischhoehe / Knieraum", "0,44 / 0,74-0,78 / >= 0,64", f"{f(SITZ_HOEHE)} / {f(TISCH_HOEHE)} / {f(KNIERAUM)}", SITZ_HOEHE == 0.44 and KNIERAUM >= 0.64)
    p("Stuhlbreite (Oger-Huefte ~0,54)", ">= 0,58", f(STUHL_BREITE), STUHL_BREITE >= 0.58)
    p("Abstand der Stuehle (Oger-Schulter 0,66)", ">= 0,90", f(SITZ_ABSTAND), SITZ_ABSTAND >= 0.90)
    frei = U_SCHENKEL_Y[1] - U_SCHENKEL_Y[0] - TISCH_TIEFE
    p("Frei zwischen den Schenkeln der Tafel", ">= 1,20", f(frei), frei >= 1.20)
    rh = RAEUME["ratshalle"]
    vorn = (U_SCHENKEL_Y[0] - TISCH_TIEFE / 2 - STUHL_VOR - STUHL_HINTEN) - rh[2]
    p("Gang hinter den vorderen Stuehlen bis zur Wand", f">= {f(BREITESTE + 0.24)}", f(vorn), vorn >= BREITESTE + 0.24)
    hinten = TREPPENLOCH[2] - (U_SCHENKEL_Y[1] + TISCH_TIEFE / 2 + STUHL_VOR + STUHL_HINTEN)
    p("Gang hinter den hinteren Stuehlen bis zum Gelaender", f">= {f(BREITESTE + 0.24)}", f(hinten), hinten >= BREITESTE + 0.24)
    # Auflager und Konsolen der Deckenbalken auf Mauerwerk, nicht in Fensteroeffnungen
    for raum in ("eingangshalle", "gerichtssaal", "ratshalle"):
        liste = [a for a in auflager() if a[0] == raum]
        schlecht = [a for a in liste if not auflager_frei(a)]
        p(f"{RAUM_TITEL[raum]}: Balkenauflager auf Mauerwerk (Laibung >= {f(AUFLAGER_ABSTAND)})", f"{len(liste)} von {len(liste)}",
          f"{len(liste) - len(schlecht)} von {len(liste)}" + (f" (in der Oeffnung: {', '.join(a[1] for a in schlecht)})" if schlecht else ""), not schlecht)
    probe = ("ratshalle", "Gegenprobe", "vorn_og", -0.20 - KONSOLE[0], -0.20 + KONSOLE[0], Z_TRAUFE - BALKEN_H - KONSOLE[1], Z_TRAUFE - BALKEN_H)
    p("Gegenprobe: Konsole ueber dem Fenster bei x -0,20 wird erkannt", "erkannt", "erkannt" if not auflager_frei(probe) else "NICHT erkannt", not auflager_frei(probe))
    return z


def auflager():
    """Auflager und Konsolen der Deckenbalken an den Aussenwaenden: (Raum, Name, Wand, a0, a1, z0, z1), a in Weltkoordinaten entlang der Wand."""
    l = []
    zk = (Z_TRAUFE - BALKEN_H - KONSOLE[1], Z_TRAUFE - BALKEN_H)
    for x in RATSHALLE_BALKEN:
        for wand in ("vorn_og", "hinten_og"):
            l.append(("ratshalle", f"Konsole x {x:+.2f} {wand}", wand, x - KONSOLE[0], x + KONSOLE[0], *zk))
    for x in HALLE_BALKEN:
        l.append(("eingangshalle", f"Balken x {x:+.2f}", "vorn_eg", x - 0.10, x + 0.10, Z_EG_DECKE - 0.24, Z_EG_DECKE))
    for y in GERICHT_BALKEN:
        l.append(("gerichtssaal", f"Balken y {y:+.2f}", "links_eg", y - 0.11, y + 0.11, Z_EG_DECKE - 0.24, Z_EG_DECKE))
    return l


def auflager_frei(a, abstand=None):
    """Sitzt das Auflager auf Mauerwerk? Nein, wenn es einer Oeffnung derselben Wand in der Lage und in der Hoehe naeher als `abstand` kommt."""
    abstand = AUFLAGER_ABSTAND if abstand is None else abstand
    _, _, wand, a0, a1, z0, z1 = a
    for w, (o0, o1), (oz0, oz1), art, _ in OEFFNUNGEN:
        if w != wand: continue
        if a1 > o0 - abstand and a0 < o1 + abstand and z1 > oz0 - abstand and z0 < oz1 + abstand:
            return False
    return True


def tabelle():
    zeilen = ["| Pruefung | Soll | Ist | |", "|---|---|---|---|"]
    for name, soll, ist, ok in soll_ist():
        zeilen.append(f"| {name} | {soll} | {ist} | {'ok' if ok else 'NEIN'} |")
    return "\n".join(zeilen)


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
#  Grundrisse (PNG)
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

def grundriss(ordner):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.patches import Rectangle, Circle, Wedge, Polygon

    os.makedirs(ordner, exist_ok=True)
    WAND, RAUM, TREPPE, LOCH = "#5b524a", "#efe6d2", "#b98d5a", "#cfc3a8"

    def wand(ax, x0, x1, y0, y1, farbe=WAND):
        ax.add_patch(Rectangle((x0, y0), x1 - x0, y1 - y0, color=farbe, lw=0))

    def raum(ax, name, farbe=RAUM, titel=None):
        x0, x1, y0, y1, zb, zd = RAEUME[name][:6]
        ax.add_patch(Rectangle((x0, y0), x1 - x0, y1 - y0, color=farbe, lw=0))
        ax.text((x0 + x1) / 2, (y0 + y1) / 2, f"{titel or RAUM_TITEL[name]}\n{x1 - x0:.2f} x {y1 - y0:.2f} m\nlicht {zd - zb:.2f} m".replace(".", ","),
                ha="center", va="center", fontsize=8, color="#222")

    def tuer(ax, name):
        _, _, achse, ((a0, a1), (b0, b1)), _ = TUEREN[name]
        ax.add_patch(Rectangle((a0, b0), a1 - a0, b1 - b0, color="#c0392b", lw=0, zorder=5))

    def oeffnungen(ax, waende):
        for w, (a0, a1), (z0, z1), art, _ in OEFFNUNGEN:
            if w not in waende: continue
            (ox, oy), (ex, ey), _, (nx, ny) = WAENDE[w]
            d = WAND_DICKE[w]
            if ex:
                ax.add_patch(Rectangle((a0, oy - (d if ny > 0 else 0)), a1 - a0, d, color="#7fb3d5" if art != "portal" else "#c0392b", lw=0, zorder=5))
            else:
                ax.add_patch(Rectangle((ox - (d if nx > 0 else 0), a0), d, a1 - a0, color="#7fb3d5", lw=0, zorder=5))

    def stufen(ax, x0, x1, y0, y1, n, laengs):
        ax.add_patch(Rectangle((x0, y0), x1 - x0, y1 - y0, color=TREPPE, lw=0, zorder=3))
        for i in range(1, n):
            if laengs == "y":
                y = y0 + i * (y1 - y0) / n; ax.plot([x0, x1], [y, y], color="#6b4a2b", lw=0.6, zorder=4)
            else:
                x = x0 + i * (x1 - x0) / n; ax.plot([x, x], [y0, y1], color="#6b4a2b", lw=0.6, zorder=4)

    def rahmen(ax, titel):
        ax.set_aspect("equal"); ax.set_xlim(-8.5, 8.5); ax.set_ylim(5.5, -5.5)   # Markt unten im Bild? Nein: Markt oben (Y nach unten gezeichnet)
        ax.set_ylim(-5.6, 5.6); ax.invert_yaxis()
        ax.set_title(titel, fontsize=10)
        ax.add_patch(Rectangle((-8, -5), 16, 10, fill=False, ls="--", lw=0.8, color="#888"))
        ax.text(0, -5.35, "Markt (vorn, im Spiel +Z)", ha="center", fontsize=7, color="#666")
        ax.grid(True, lw=0.3, color="#ddd"); ax.set_xticks(range(-8, 9, 2)); ax.set_yticks(range(-5, 6, 1)); ax.tick_params(labelsize=6)

    def massstab(ax):
        ax.plot([5.5, 7.5], [4.9, 4.9], color="k", lw=1.5); ax.text(6.5, 5.25, "2 m", ha="center", fontsize=7)

    # ── Erdgeschoss ──
    fig, ax = plt.subplots(figsize=(11, 7.5)); rahmen(ax, f"Erdgeschoss (Boden {Z_SOCKEL:.2f} m, licht {Z_EG_DECKE - Z_SOCKEL:.2f} m)".replace(".", ","))
    wand(ax, EG_X[0], EG_X[1], EG_Y[0], EG_Y[1])
    wand(ax, TURM_X[0], TURM_X[1], TURM_Y[0], TURM_Y[1])
    wand(ax, ANBAU_X[0], ANBAU_X[1], ANBAU_Y[0], ANBAU_Y[1], "#8a6d4f")
    for n in ("eingangshalle", "gerichtssaal", "amtsstube", "turm_treppe"): raum(ax, n)
    x0, x1, y0, y1 = RAEUME["eingangshalle"][:4]
    # Haupttreppe
    stufen(ax, TRENN_X[1], TRENN_X[1] + TREPPE_BREITE, H_LAUF1_Y0, H_PODEST[2], H_LAUF1 - 1, "y")
    ax.add_patch(Rectangle((H_PODEST[0], H_PODEST[2]), H_PODEST[1] - H_PODEST[0], H_PODEST[3] - H_PODEST[2], color="#a07848", lw=0, zorder=3))
    stufen(ax, H_PODEST[1], H_LAUF2_X1, H_PODEST[2], H_PODEST[3], H_STUFEN - H_LAUF1 - 1, "x")
    ax.annotate("", xy=(H_LAUF2_X1 - 0.1, y1 - 0.6), xytext=(TRENN_X[1] + 0.6, H_LAUF1_Y0 + 0.1), arrowprops=dict(arrowstyle="->", color="#333", connectionstyle="angle,angleA=0,angleB=90"))
    ax.text(0.3, 1.9, f"Treppe hinauf zur Ratshalle\n{H_STUFEN} x {H_STEIG:.3f} / {H_AUFTRITT:.2f}".replace(".", ","), fontsize=7, ha="center")
    ax.text(2.4, 2.2, "Kellertuer\n(unter Lauf 2)", fontsize=6.5, ha="center", color="#c0392b")
    # Wendeltreppe
    ax.add_patch(Circle(W_MITTE, W_AUSSEN, color=TREPPE, zorder=3)); ax.add_patch(Circle(W_MITTE, W_SPINDEL, color=WAND, zorder=4))
    for i in range(int(2 * math.pi / W_WINKEL) + 1):
        a = i * W_WINKEL
        ax.plot([W_MITTE[0] + W_SPINDEL * math.cos(a), W_MITTE[0] + W_AUSSEN * math.cos(a)], [W_MITTE[1] + W_SPINDEL * math.sin(a), W_MITTE[1] + W_AUSSEN * math.sin(a)], color="#6b4a2b", lw=0.5, zorder=4)
    ax.text(W_MITTE[0], TURM_Y[0] + 0.25, "Turm-Treppe", ha="center", fontsize=8, zorder=6)
    for t in ("Tuer_Halle_Gericht", "Tuer_Halle_Turm", "Tuer_Halle_Amt", "Tuer_Halle_Kerker", "Tuer_Halle_Stadt"): tuer(ax, t)
    oeffnungen(ax, ("vorn_eg", "links_eg", "hinten_eg", "anbau_rechts", "anbau_hinten", "turm_vorn", "turm_rechts"))
    ax.text(0.25, -4.75, "Portal", ha="center", fontsize=7, color="#c0392b")
    massstab(ax)
    fig.savefig(os.path.join(ordner, "grundriss_eg.png"), dpi=130, bbox_inches="tight"); plt.close(fig)

    # ── Keller ──
    fig, ax = plt.subplots(figsize=(11, 7.5)); rahmen(ax, f"Keller: Kerker (Boden {Z_KERKER:.2f} m, licht {Z_KERKER_DECKE - Z_KERKER:.2f} m)".replace(".", ","))
    wand(ax, TRENN_X[0], RECHTS_X[1], EG_Y[0], EG_Y[1])
    raum(ax, "kerker")
    stufen(ax, K_X1, K_X0, KELLERLOCH[2], KELLERLOCH[3], K_STUFEN - 1, "x")
    ax.add_patch(Rectangle((K_X0, KELLERLOCH[2]), RAEUME["kerker"][1] - K_X0, TREPPE_BREITE, color="#a07848", zorder=3))
    ax.text((K_X0 + K_X1) / 2, KELLERLOCH[2] - 0.3, f"Treppe hinauf zur Eingangshalle {K_STUFEN} x {K_STEIG:.2f} / {K_AUFTRITT:.2f}".replace(".", ","), fontsize=7, ha="center")
    for i, yy in enumerate((-3.70, -1.20)):
        ax.add_patch(Rectangle((TRENN_X[1], yy), 1.90, 2.40, color="#d6cbb4", zorder=3)); ax.plot([TRENN_X[1] + 1.90] * 2, [yy, yy + 2.40], color="#333", lw=2.5, zorder=4)
        ax.text(TRENN_X[1] + 0.95, yy + 1.2, f"Zelle {i + 1}", ha="center", fontsize=7, zorder=5)
    massstab(ax)
    fig.savefig(os.path.join(ordner, "grundriss_keller.png"), dpi=130, bbox_inches="tight"); plt.close(fig)

    # ── Obergeschoss ──
    fig, ax = plt.subplots(figsize=(11, 7.5)); rahmen(ax, f"Obergeschoss (Boden {Z_OG:.2f} m, licht {Z_TRAUFE - Z_OG:.2f} m)".replace(".", ","))
    wand(ax, OG_X[0], OG_X[1], OG_Y[0], OG_Y[1], "#8a6d4f")
    wand(ax, TURM_X[0], TURM_X[1], TURM_Y[0], TURM_Y[1])
    raum(ax, "ratshalle", titel="Ratshalle")
    ax.add_patch(Rectangle((TREPPENLOCH[0], TREPPENLOCH[2]), TREPPENLOCH[1] - TREPPENLOCH[0], TREPPENLOCH[3] - TREPPENLOCH[2], color=LOCH, zorder=3, hatch="//"))
    ax.text((TREPPENLOCH[0] + TREPPENLOCH[1]) / 2, (TREPPENLOCH[2] + TREPPENLOCH[3]) / 2, "Treppenloch mit Gelaender", fontsize=7, ha="center", zorder=5)
    # U-Tafel, Podest, Plaetze
    ax.add_patch(Rectangle((PODEST[0], PODEST[2]), PODEST[1] - PODEST[0], PODEST[3] - PODEST[2], color="#c9b28c", zorder=3))
    for y in U_SCHENKEL_Y:
        ax.add_patch(Rectangle((U_X[0], y - TISCH_TIEFE / 2), U_X[1] - U_X[0], TISCH_TIEFE, color="#8b5a2b", zorder=4))
    ax.add_patch(Rectangle((U_X[1], U_SCHENKEL_Y[0] - TISCH_TIEFE / 2), TISCH_TIEFE, U_SCHENKEL_Y[1] - U_SCHENKEL_Y[0] + TISCH_TIEFE, color="#8b5a2b", zorder=4))
    for name, (x, y, _), blick, platz in ratsplaetze():
        ax.add_patch(Circle((x, y), 0.28, color="#c0392b" if platz == 1 else "#2e86c1", zorder=5))
        ax.text(x, y, str(platz), ha="center", va="center", fontsize=6, color="w", zorder=6)
    for (x, y, _), _b in BANK:
        ax.add_patch(Circle((x, y), 0.25, color="#7d3c98", zorder=5))
    ax.add_patch(Circle(SCHREIBER[0][:2], 0.25, color="#27ae60", zorder=5)); ax.text(SCHREIBER[0][0], SCHREIBER[0][1] - 0.45, "Schreiber", ha="center", fontsize=6)
    ax.add_patch(Circle(REDNER[0][:2], 0.25, color="#e67e22", zorder=5)); ax.text(REDNER[0][0], REDNER[0][1] + 0.5, "Redner", ha="center", fontsize=6)
    ax.text(-5.35, 3.05, "Zuhoererbank", ha="center", fontsize=6)
    ax.add_patch(Rectangle((RAEUME["ratshalle"][0], -1.1), 0.55, 2.2, color="#a39383", zorder=4)); ax.text(-6.6, 0, "Kamin", rotation=90, fontsize=7, va="center", zorder=5)
    oeffnungen(ax, ("vorn_og", "hinten_og", "links_og"))
    ax.text(W_MITTE[0], W_MITTE[1], "Turm\n(Wendeltreppe)", ha="center", va="center", fontsize=7, color="w")
    ax.add_patch(Rectangle((ANBAU_X[0], ANBAU_Y[0]), ANBAU_X[1] - ANBAU_X[0], ANBAU_Y[1] - ANBAU_Y[0], fill=False, hatch="..", color="#aaa"))
    ax.text(5.6, 2.0, "Pultdach\nder Amtsstube", ha="center", fontsize=7, color="#777")
    massstab(ax)
    fig.savefig(os.path.join(ordner, "grundriss_og.png"), dpi=130, bbox_inches="tight"); plt.close(fig)

    # ── Turm: Ebenen und Schnitt ──
    fig, axe = plt.subplots(1, 4, figsize=(15, 4.6))
    for ax, (titel, z) in zip(axe, (("EG 0,30: Wendeltreppe", Z_SOCKEL), ("4,60: Wendeltreppe", Z_TURM_WACHE), ("Uhrbuehne 8,60: Leiter", Z_TURM_UHR), ("Glockenstube 11,60", Z_GLOCKE))):
        ax.set_aspect("equal"); ax.set_xlim(TURM_X[0] - 0.4, TURM_X[1] + 0.4); ax.set_ylim(TURM_Y[1] + 0.4, TURM_Y[0] - 0.4)
        wand(ax, TURM_X[0], TURM_X[1], TURM_Y[0], TURM_Y[1]); ax.add_patch(Rectangle((TURM_X[0] + T_STEIN, TURM_Y[0] + T_STEIN), 3.0, 3.0, color=RAUM))
        x0, x1, y0, y1 = RAEUME["turm_treppe"][:4]
        if z == Z_SOCKEL:
            ax.add_patch(Circle(W_MITTE, W_AUSSEN, color=TREPPE)); ax.add_patch(Circle(W_MITTE, W_SPINDEL, color=WAND))
            tuer(ax, "Tuer_Halle_Turm")
        elif z == Z_TURM_WACHE:
            ax.add_patch(Circle(W_MITTE, W_AUSSEN, color=TREPPE)); ax.add_patch(Circle(W_MITTE, W_SPINDEL, color=WAND)); ax.text(W_MITTE[0], y0 + 0.3, "Wendel (2. Windung)", ha="center", fontsize=7)
        elif z == Z_TURM_UHR:
            ax.add_patch(Rectangle((x0 + 0.2, y0 + 0.2), 1.6, 1.2, color="#7f8c8d")); ax.text(x0 + 1.0, y0 + 0.8, "Uhrwerk", ha="center", fontsize=7)
            ax.add_patch(Rectangle((LUKE_LAGE[0], LUKE_LAGE[2]), LUKE[0], LUKE[1], fill=False, hatch="//")); ax.text(LUKE_LAGE[0] + 0.6, LUKE_LAGE[2] + 0.6, "Luke\nLeiter", ha="center", fontsize=7)
        else:
            ax.add_patch(Circle(W_MITTE, 0.55, color="#b7950b")); ax.text(W_MITTE[0], W_MITTE[1], "Glocke", ha="center", va="center", fontsize=7)
            ax.add_patch(Rectangle((LUKE_LAGE[0], LUKE_LAGE[2]), LUKE[0], LUKE[1], fill=False, hatch="//"))
            oeffnungen(ax, ("turm_vorn", "turm_rechts", "turm_hinten", "turm_links"))
        ax.set_title(titel, fontsize=9); ax.tick_params(labelsize=6)
    fig.savefig(os.path.join(ordner, "grundriss_turm.png"), dpi=130, bbox_inches="tight"); plt.close(fig)

    # ── Schnitt quer durch Eingangshalle, Kerker, Ratshalle (Blick von links), daneben der Turm ──
    fig, ax = plt.subplots(figsize=(11, 9))
    ax.set_aspect("equal"); ax.set_xlim(-6, 6); ax.set_ylim(-4, 23); ax.set_title("Schnitte: Hauptbau quer (x = 0,25) und Turm (Mitte)", fontsize=10)
    wand(ax, EG_Y[0], EG_Y[1], Z_KERKER - 0.4, Z_OG); wand(ax, OG_Y[0], OG_Y[1], Z_OG, Z_TRAUFE, "#8a6d4f")
    for n, farbe in (("kerker", RAUM), ("eingangshalle", RAUM), ("ratshalle", RAUM)):
        x0, x1, y0, y1, zb, zd = RAEUME[n][:6]
        ax.add_patch(Rectangle((y0, zb), y1 - y0, zd - zb, color=farbe))
        ax.text((y0 + y1) / 2, (zb + zd) / 2, f"{RAUM_TITEL[n]}\nlicht {zd - zb:.2f}".replace(".", ","), ha="center", va="center", fontsize=8)
    ax.add_patch(Polygon([(OG_Y[0] - UEBER_TRAUFE, Z_TRAUFE - UEBER_TRAUFE * NEIGUNG), (0, Z_FIRST), (OG_Y[1] + UEBER_TRAUFE, Z_TRAUFE - UEBER_TRAUFE * NEIGUNG)], color="#4d5560"))
    ax.plot([-6, 6], [0, 0], color="#6e5", lw=1)
    for z, t in ((Z_SOCKEL, "0,30"), (Z_EG_DECKE, "4,30"), (Z_OG, "4,60"), (Z_TRAUFE, "8,40"), (Z_FIRST, f"{Z_FIRST:.2f}".replace(".", ",")), (Z_KERKER, "-3,10")):
        ax.text(-5.9, z, t, fontsize=7, va="center")
    # Figuren als Mass: Mensch 1,75 und Oger 2,50 in der Eingangshalle und der Ratshalle
    for zb in (Z_SOCKEL, Z_OG, Z_KERKER):
        for dy, (h, b) in ((-1.5, FIGUREN["Mensch"]), (-0.6, FIGUREN["Oger"])):
            ax.add_patch(Rectangle((dy - b / 2, zb), b, h, color="#e67e22", alpha=0.8))
    # Turm rechts daneben (verschoben)
    tx = 2.0
    wand(ax, tx, tx + 4.2, Z_SOCKEL - 0.3, Z_TURM_MAUER)
    ax.add_patch(Rectangle((tx + 0.6, Z_SOCKEL), 3.0, Z_GLOCKE - BODEN_DICKE - Z_SOCKEL, color=RAUM, alpha=0.0))
    ax.add_patch(Polygon([(tx - UEBER_HELM, Z_TURM_MAUER), (tx + 2.1, Z_HELM_SPITZE), (tx + 4.2 + UEBER_HELM, Z_TURM_MAUER)], color="#4d5560"))
    ax.text(tx + 2.1, Z_HELM_SPITZE + 0.4, f"Helm {Z_HELM_SPITZE:.1f} / Fahne {Z_FAHNE:.1f}".replace(".", ","), ha="center", fontsize=7)
    for z, t in ((Z_TURM_UHR, "Uhrwerk 8,60"), (Z_GLOCKE, "Glockenstube 11,60")):
        ax.plot([tx + 0.6, tx + 3.6], [z, z], color="#6b4a2b", lw=2); ax.text(tx + 2.1, z + 0.25, t, ha="center", fontsize=7, color="w")
    ax.add_patch(Circle((tx + 2.1, Z_UHR_MITTE), UHR_R, fill=False, color="w", lw=1.5))
    fig.savefig(os.path.join(ordner, "schnitt.png"), dpi=110, bbox_inches="tight"); plt.close(fig)
    print("Grundrisse geschrieben nach", ordner)


if __name__ == "__main__":
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in sys.argv[1:] if a.startswith("--"))
    if "grundriss" in opt:
        grundriss(opt["grundriss"])
    if "soll-ist" in opt or not opt:
        print(tabelle())
        if not all(ok for *_, ok in soll_ist()):
            sys.exit(1)
