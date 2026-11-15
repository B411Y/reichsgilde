"""Klappbruecke Koeln: Masse, Soll gegen Ist (reines Python, kein Blender).

    python tools/modelle/klappbruecke_masse.py

Die Konstanten oben sind die Masse des Modells (`klappbruecke.py` liest sie von hier). Der Rest liest die Quellen im Repo
(Schiffe, Karren, Karte, Wasserplan) per Textsuche und rechnet nach: Durchfahrt gegen Schiffsbreite und Rah, Fahrbahn gegen
zwei Karren, Torhaustuer gegen Oger, Aufschwenken der Klappen gegen Mast und Takelage, Weg der Gegengewichte.
Alles ist nur gerechnet (kein Spielbild, kein Wasserstand aus der Karte); was geschaetzt ist, steht als "geschaetzt" da.

Koordinaten des Modells (Blender, Meter): X = Brueckenachse (Torhaus_L bei -X = Westufer/Koeln, Torhaus_R bei +X = Deutz), Y = Flussrichtung
(+Y = Norden = Stroemungsrichtung; Karte: Norden ist -z, die Brueckenstrasse liegt bei z = 8, Rhein-Mitte bei x = 90), Z oben, Z = 0 Wasserlinie.
Beim glTF-Export (Y nach oben) wird Blender-Y zu glTF -Z = Godot -Z: das passt zur Karte (Norden = -z).
"""
import json
import math
import os
import re

# ── Masse des Modells (Meter, Grad) ───────────────────────────────────────────────────────────────────────────────────────
DURCH = 13.0            # lichte Weite der Durchfahrt zwischen den Torhaeusern (Drehachsen bei x = +-6,5)
HALB = DURCH / 2
DECK = 2.4              # Oberkante Fahrbahn ueber der Wasserlinie (geschaetzt: Ufer + 1,4 m wie die Steinbruecke des Wasserplans)
ZP = DECK - 0.2         # Drehachse der Klappen (Zapfen)
KLAPPE_L = HALB - 0.05  # Laenge einer Klappe (0,1 m Fuge in der Mitte)
KLAPPE_B = 7.0          # Breite einer Klappe (|y| <= 3,5), Bohlenbelag
LICHT_B = 6.4           # lichte Fahrbahnbreite zwischen den Gelaendern
THETA = 75.0            # Klappwinkel offen (Grad)
UA = 4.8                # Abstand des Kettenangriffs von der Drehachse (auf der Klappe)
YK = 4.3                # |y| von Kette, Kettenrad und Gegengewicht
ZW = 10.2               # Hoehe der Kettenradmitte (Wasserlinie = 0)
RW = 0.4                # Radius Kettenrad
AW = 1.0                # Kettenradmitte: Abstand von der Drehachse zur Mitte hin (x_abs = 5,5)
RH = 0.5                # Radius Haspelrad (Handspeichen)
PYL_X = (HALB, HALB + 4.0)      # Torhaus: von der Durchfahrt bis zum Land (Tiefe 4,0 m)
PYL_Y = (3.6, 6.2)              # Turmpfeiler (Betrag von y)
TUER = (1.3, 2.9)               # Torhaustuer (Breite, Hoehe) auf der Landseite, im Turmpfeiler bei y = +-4,9
BOGEN = (7.2, 4.0, 1.0)         # Durchgang unter dem Torhaus: Weite (y), Seitenhoehe, Pfeilhoehe (Segmentbogen)
RAMPE = 2.0                     # Rampe je Ufer (Meter): so viel gibt der Wasserplan der Bruecke (vorher 10 m West, 4 m Ost); Parameter fuer Ende_Rampe
UFER_TIEF = 1.4                 # Fahrbahn liegt so weit ueber dem Ufer (Wasserplan: Deck = Uferhoehe + SteinDeck 1,4 m)
FELD = 8.0                      # Bogenfeld der Seitenspannen (wie Wasserplan.SteinBogen)
ZWISCHENPFEILER = 2.0           # Pfeiler zwischen zwei Bogenfeldern (wie Wasserplan.SteinPfeiler)
MITTELTEIL = 2 * PYL_X[1]       # Mittelteil mit den Torhaeusern (x = +-10,5): 21 m, wie Wasserplan.KlappMittelteil (Torhauspfeiler je 4 m, Durchfahrt 13 m)
SCHRANKE_X = PYL_X[1] + 1.0     # Schrankenbaum je Ufer: 1 m landwaerts vor dem Torhaus (Betrag von x), quer zur Fahrbahn
WARTE_X = SCHRANKE_X + 4.5      # Haltepunkt (Gespannmitte) des ersten wartenden Pferdekarrens, Vorderkante an der Schranke
GEW = (0.7, 0.7, 1.1)           # Gegengewicht (x, y, z)

# ── Quellen im Repo ───────────────────────────────────────────────────────────────────────────────────────────────────────
HIER = os.path.dirname(os.path.abspath(__file__))
WURZEL = os.path.dirname(os.path.dirname(HIER))


def lies(pfad):
    with open(os.path.join(WURZEL, pfad), encoding="utf-8") as f:
        return f.read()


def zahl(text, muster):
    m = re.search(muster, text)
    return float(m.group(1).replace(",", ".")) if m else None


def kinematik(theta_grad):
    """Kettenstrang (Klappe -> Kettenrad) und Gegengewicht bei Klappwinkel theta. Alles in Brueckenachsenmass (a = Abstand von der Drehachse zur Mitte)."""
    t = math.radians(theta_grad)
    aA, zA = UA * math.cos(t), ZP + UA * math.sin(t)
    aF, zF = AW + RW, ZW
    l1 = math.hypot(aA - aF, zA - zF)
    return aA, zA, l1


def main():
    ob = lies("tools/modelle/oberlaender.py")
    nl = lies("tools/modelle/niederlaender.py")
    ob_l, ob_b = [float(v) for v in re.search(r"LAENGE, BREITE = ([\d.]+), ([\d.]+)", ob).groups()]
    nl_l, nl_b = [float(v) for v in re.search(r"LAENGE, BREITE = ([\d.]+), ([\d.]+)", nl).groups()]
    karren = {k: zahl(lies("game/scripts/Stadtansicht.KarrenHof.cs"), k + r'" => ([\d.]+)f') for k in ("Pferdekarren", "Ochsenkarren")}
    haupt = zahl(lies("src/Reichsgilde.Core/Bauen/Stadtkarte.cs"), r"Wegklasse\.Haupt => ([\d.]+),")
    holz_bis = zahl(lies("src/Reichsgilde.Core/Bauen/Wasserplan.cs"), r"HolzBis = ([\d.]+)")
    lange = zahl(lies("src/Reichsgilde.Core/Bauen/Wasserplan.cs"), r"LangeBruecke = ([\d.]+)")
    steinbogen = zahl(lies("src/Reichsgilde.Core/Bauen/Wasserplan.cs"), r"SteinBogen = (\d+)")
    steindeck = zahl(lies("src/Reichsgilde.Core/Bauen/Wasserplan.cs"), r"SteinDeck = [\d.]+, SteinDeck = ([\d.]+)") or 1.4
    # Schiffe aus den glb-Messungen (Blender-Import, 15.11.2026, Idle-Lauf): Rah/Takelage und Mastspitze
    schiffe = {
        "Oberlaender": dict(rumpf=ob_b, laenge=ob_l, rah=7.84, mast=11.85, fahne=13.26, takel_b=3.92, takel_z=(1.19, 11.46)),
        "Niederlaender": dict(rumpf=nl_b, laenge=nl_l, rah=7.84, mast=10.0, fahne=11.41, takel_b=3.92, takel_z=(0.86, 9.61)),
    }
    karte = json.load(open(os.path.join(WURZEL, "game/karten/koeln.json"), encoding="utf-8"))
    rhein = next(w for w in karte["wasser"] if w["name"] == "Rhein")

    print("== Klappbruecke Koeln: Masse, Soll gegen Ist (nur gerechnet) ==\n")
    print("Lage: Brueckenstrasse z = 8, Rhein-Mitte x = 90 (linkes Ufer x = 60, Breite dort 60 m), Rhein fliesst nach Norden (-z); Modell-Y = Norden.")
    print(f"Ist (Wasserplan): Rhein 60 m > HolzBis {holz_bis:.0f} m -> Steinbruecke, {round((60 + 2) / 10)} Boegen zu je ~{(60 - 2 * (round((62 / 10)) - 1)) / round(62 / 10):.1f} m, "
          f"{round(62 / 10) - 1} Pfeiler zu 2 m, Rampen 10 m, Deck +{steindeck} m ueber Ufer, Strasse 'Haupt' {haupt:.0f} m breit; LangeBruecke-Hinweis ab {lange:.0f} m (Rhein genau 60 m).")
    print(f"Modell: Durchfahrt {DURCH} m (ersetzt rund einen Bogen plus Pfeiler: 8,3 + 2 = 10,3 m) -> Torhaeuser je 4 m tief, Gesamtlaenge Mittelteil {DURCH + 2 * 4:.0f} m.\n")
    print(f"Wasserplan mit Klappbruecke (Kern, Datenversion 90, PR 478): Rheinbruecke 64,1 m, Pfeiler bei -21,3 / -8,5 / +8,5 / +21,3 m von der Flussmitte, Torhauspfeiler je 4 m, Durchfahrt {DURCH} m, Mittelteil {MITTELTEIL:.0f} m (= 2 x 8,5 + 4); Bogenfelder je Seite 2 zu 9,8 m; Rampe je Ufer {RAMPE} m: Rampensteigung {UFER_TIEF / RAMPE * 100:.0f} % bei {UFER_TIEF} m Hoehe (Karren schaffen etwa 10-12 %: Rampe von rund {UFER_TIEF / 0.12:.0f} m oder Auffahrt im Gelaende noetig).")

    print("-- Durchfahrt (Klappen offen) --")
    for n, s in schiffe.items():
        soll_rumpf = s["rumpf"] + 2 * 1.4
        soll_rah = s["rah"] + 2 * 1.0
        print(f"{n}: Rumpf {s['rumpf']} m (Soll lichte Weite >= Rumpf + 2*1,4 Luft = {soll_rumpf:.1f}), Rah/Takelage {s['rah']} m auf {s['takel_z'][0]}..{s['takel_z'][1]} m Hoehe, "
              f"Mastspitze {s['mast']} m, Fahne {s['fahne']} m ueber Wasser")
    print(f"Ist: lichte Weite {DURCH} m. Kettenraeder und Haspel stehen bei |x| >= {HALB - AW - RW + 0.0 - 0.0:.2f} (Kettenrad vorn) bzw. {HALB - AW + 0.0 - RH + 0.0:.2f} m (Haspel) -> frei in Rahhoehe: {2 * (HALB - AW - RH):.1f} m "
          f"(Soll Rah + 2*1,0 = {7.84 + 2:.1f}): " + ("ok" if 2 * (HALB - AW - RH) >= 7.84 + 2 - 1e-9 else "ZU ENG"))
    print("Hoehe offen: nach oben frei (keine Struktur ueber der Durchfahrt); begrenzt nur durch die aufgeklappten Klappen seitlich.")
    for th in (70, 75, 80):
        t = math.radians(th)
        spitze_z = ZP + KLAPPE_L * math.sin(t)
        vor = KLAPPE_L * math.cos(t)
        print(f"  Klappwinkel {th}: Klappenspitze {spitze_z:.2f} m ueber Wasser, ragt {vor:.2f} m in die Durchfahrt (Spitze bei |x| = {HALB - vor:.2f}); "
              f"Abstand zur Takelage-Breite 3,92: {HALB - vor - 3.92:.2f} m")
    print("  (Takelage bei 75 Grad: Spitze liegt bei 8,4 m, die Rah erst ab ~9,5 m: sie geht darueber; die breite Takelage ist dort schmaler.)")
    print("Hinweis Schiffsmodell: 'Mast' reicht in y von -4,0 bis 4,0 (Rah), Laufplanke bis -4,8 / -5,2 (einseitig, am Ufer); gemessen an den glb.\n")

    print("-- Geschlossen: Fahrbahn --")
    print(f"Fahrbahnhoehe {DECK} m ueber Wasser (geschaetzt; die Karte kennt keinen Wasserspiegel, Wasserplan: Deck = Uferhoehe + {steindeck} m). Unterkante Klappe {DECK - 0.55:.2f} m -> kein Schiff passiert geschlossen (Nachen 1,3 m breit mit Schiffer ~2,0 m hoch).")
    pk, ok = karren["Pferdekarren"], karren["Ochsenkarren"]
    soll = 2 * pk + 3 * 0.4
    print(f"Karren (Gespannbreite aus Stadtansicht.KarrenHof.cs): Pferd {pk} m, Ochse {ok} m; Soll lichte Breite = 2*{pk} + 3*0,4 Luft = {soll:.1f} m (Fussweg ist keine eigene Spur).")
    print(f"Ist: lichte Breite {LICHT_B} m (Klappe {KLAPPE_B} m mit Wangen): " + ("ok" if LICHT_B >= soll else "ZU SCHMAL") + f"; bestehende Strasse 'Haupt' {haupt:.0f} m: " + ("ok" if haupt >= soll else f"zu schmal um {soll - haupt:.1f} m (Passanten drueckt Karren aus)"))
    print("Karren messen (glb): Pferd 2,38 x 8,85 (mit Gespann ~8,9 m lang), Ochse 2,08 x 6,01, Hand 1,50 x 3,27; Hoehe 2,68 / 2,43 / 1,35.")
    tor_h = DECK + BOGEN[1]
    print(f"Durchgang im Torhaus: Weite {BOGEN[0]} m, Seitenhoehe {BOGEN[1]} m, Scheitel {BOGEN[1] + BOGEN[2]} m ueber Fahrbahn (Soll Karren hoch 2,68 + 0,5 = 3,2: ok); Tuer {TUER[0]} x {TUER[1]} m (Soll Oger 2,50 + 0,3 = 2,8: " + ("ok" if TUER[1] >= 2.8 else "zu niedrig") + ").")
    print("Figurenmasse: Mensch 1,75 m, Zwerg 1,35 m, Elb 1,85 m, Oger 2,50 m (Auftrag).\n")

    print("-- Schranken und Wartezonen (nur Mass, fuer die Ablaufphase: Karren halten an, `WartetAnBruecke`) --")
    pl = 8.85
    print(f"Schranke je Ufer bei |x| = {SCHRANKE_X} m (Baum quer, {LICHT_B} m lang, 1 m hinter der Landseite des Torhauses bei {PYL_X[1]} m); Fussgaenger- und Karrenhalt davor.")
    print(f"Haltepunkt des ersten Karrens (Gespannmitte) bei |x| = {WARTE_X} m; Pferdegespann {pl} m lang -> zweiter Karren {WARTE_X + pl + 0.5:.1f} m, dritter {WARTE_X + 2 * (pl + 0.5):.1f} m.")
    print(f"Wartezone (2 Pferdegespanne hintereinander + Luft): von |x| = {SCHRANKE_X} bis {SCHRANKE_X + 2 * pl + 1.0:.1f} m, also {2 * pl + 1.0:.1f} m gerade Strasse; Ochsenkarren ({6.01} m) und Handkarren ({3.27} m) kuerzer.")
    print(f"Die bestehende Brueckenstrasse hat vor den Ufern Rampen/Wege: Wartezone braucht je Ufer >= {SCHRANKE_X + 2 * pl + 1.0 - HALB:.1f} m ab Drehachse gerade und {KLAPPE_B} m breit (Haupt 5 m: zu schmal, siehe oben).")
    pfosten_y, baum_z, brüstung_i, baum_ende = 3.0, 1.05, 3.2, 3.15
    print(f"Schrankenbaum (Modell Schranke_L/R, |x| = {SCHRANKE_X} m): Pfosten bei y = +{pfosten_y} m (Nordseite), Baum {baum_z} m ueber der Fahrbahn (Bruestung 1,0 m), "
          f"reicht von y = +{pfosten_y} bis -{baum_ende} m = {pfosten_y + baum_ende:.2f} m; Fahrbahn lichte {LICHT_B} m: Spalt am Pfosten {brüstung_i - pfosten_y - 0.17:.2f} m, an der Baumspitze {brüstung_i - baum_ende:.2f} m -> "
          f"Fahrbahn und Fussweg ganz gesperrt (kein Durchgang unter dem Baum: Zwerg 1,35 m, Mensch 1,75 m, Elb 1,85 m, Oger 2,50 m sind alle hoeher als {baum_z} m). Offen steht der Baum fast senkrecht (80 Grad), Fahrbahn 6,4 m frei, Karren (2,4 m breit) passen.")
    print("Passanten: Wartestreifen am Rand vor der Schranke (0,4 m je Person), 2 m am Torhaus je Seite; Zierschiff: Oeffnung 20 min vor Ankunft (Kern), Karren warten dann bis zu der Oeffnungsdauer.\n")

    print("-- Zugwerk (gerechnet) --")
    for th in (0, 45, 60, 75):
        aA, zA, l1 = kinematik(th)
        print(f"  theta {th:>2}: Kettenstrang {l1:.2f} m, Anschlagpunkt a={aA:.2f} z={zA:.2f}")
    l0 = kinematik(0)[2]; l75 = kinematik(THETA)[2]
    hub = l0 - l75
    zc0 = ZW - 0.9 - GEW[2] / 2
    print(f"  Hub des Gegengewichts {hub:.2f} m: Mitte geschlossen {zc0:.2f} -> offen {zc0 - hub:.2f} m; Unterkante offen {zc0 - hub - GEW[2] / 2:.2f} m (Fahrbahn {DECK} m): "
          + ("ok" if zc0 - hub - GEW[2] / 2 >= DECK + 0.1 else "STOESST AN"))
    print(f"  Kettenrad dreht {math.degrees(hub / RW):.0f} Grad beim Oeffnen (Radius {RW} m).")


if __name__ == "__main__":
    main()
