"""Neubau Wohnhaus Zwerg, Stufe 1: Soll gegen Ist der Masse (reines Python, ohne Blender).

    python tools/modelle/neubau/zwerg_masse.py            # nur das Soll (Figurenmasse und die daraus folgenden Mindestmasse)

Das Bauskript (wohnhaus_zwerg_s1.py) misst am Ende jeder Grundform den Ist-Stand aus der gebauten Szene (Anker, Netze,
Konstanten der Menschenskripte) und ruft `pruefe(form, ist)` auf: eine Tabelle Soll gegen Ist je Zeile, mit ok oder „zu knapp“.
Muster: rathaus_masse.py (Branch phase5/neubau-rathaus). Die Zahlen stehen mit Herkunft in docs/bereiche/darstellung.md.

Regel (Auftrag): innen exakt maßgleich mit aussen, fuer Zwerg und Zwergin belegt; zugleich muss eine Menschenfigur von 1,75 m durch
die Haustuer passen und im Raum stehen koennen (Besucher und Mitarbeiter anderer Voelker). Oger (2,50 m wie beim Rathaus) ist hier nicht
Pflicht; die Tabelle nennt trotzdem, was er braeuchte.
"""
import sys

# Hoehe in Metern, Schulterbreite. Zwerg 1,30-1,40 (Mittel 1,35), Zwergin mal 0,95: ProbeKoerpergroesse / ProbeArtDaten in
# game/scripts (docs/bereiche/darstellung.md, Passanten-Probe). Mensch 1,75, Elb 1,85, Halbling 1,10, Ork 1,1 x 1,75,
# Oger 1,35 x 1,75 = 2,36 (Obergrenze des Rathaus-Auftrags 2,50).
FIGUREN = {
    "Zwergin": (1.28, 0.44), "Zwerg": (1.35, 0.46), "Halbling": (1.10, 0.32), "Mensch": (1.75, 0.46),
    "Elb": (1.85, 0.44), "Ork": (1.93, 0.52), "Oger": (2.50, 0.66),
}
KOPF = 0.20             # Luft ueber dem Kopf in einer Tuer (Hut, Nicken)
SCHULTER_LUFT = 0.30    # Luft neben den Schultern in der Tuer
RAUM_LUFT = 0.30        # Luft ueber dem Kopf im Raum
GREIF_FAKTOR = 1.25     # Greifhoehe mit gehobenem Arm im Verhaeltnis zur Koerpergroesse
SITZ_FAKTOR = (0.25, 0.28)    # Sitzhoehe / Koerpergroesse: Mensch 0,44/1,75 = 0,25 (die Probe), Zwerg gedrungener
TISCH_ABSTAND = (0.24, 0.30)  # Tischplatte ueber der Sitzflaeche
AUGE_FAKTOR = 0.93      # Augenhoehe / Koerpergroesse (1,60 / 1,75 = 0,91 bei den Menschen; Zwerg 1,25 / 1,35)


def soll():
    """Mindestmasse je Figur: Tuer (Hoehe, Breite), Raumhoehe, Greifhoehe."""
    z = {}
    for name, (h, b) in FIGUREN.items():
        z[name] = dict(tuer_h=round(h + KOPF, 2), tuer_b=round(b + SCHULTER_LUFT, 2), raum_h=round(h + RAUM_LUFT, 2), greif=round(h * GREIF_FAKTOR, 2),
                       sitz=tuple(round(h * f, 2) for f in SITZ_FAKTOR), auge=round(h * AUGE_FAKTOR, 2))
    return z


def pruefe(form, ist, ausgabe=print):
    """Tabelle Soll gegen Ist. `ist`: Schluessel tuer_h, tuer_b, raum_h, haube_h, sitz, tisch, auge, bruestung, stufe, greif (dict Name ->
    Oberkante), galerie_stehbreite (dict Figurname -> Breite in Metern oder 0). Liefert (Zeilen, Zahl der Fehler fuer Zwerg und Mensch)."""
    s = soll()
    zeilen, fehler = [], 0

    def zeile(name, sollwert, istwert, ok, bem=""):
        nonlocal fehler
        zeilen.append((name, sollwert, istwert, ok, bem))

    for fig in ("Zwergin", "Zwerg", "Mensch", "Elb", "Ork", "Oger"):
        pflicht = fig in ("Zwergin", "Zwerg", "Mensch")
        z = s[fig]
        for name, soll_, ist_, mehr in (
            (f"Haustuer Hoehe, {fig} {FIGUREN[fig][0]:.2f} m", z["tuer_h"], ist["tuer_h"], True),
            (f"Haustuer Breite, {fig}", z["tuer_b"], ist["tuer_b"], True),
            (f"Raumhoehe (Decke ueber dem Fussboden), {fig}", z["raum_h"], ist["raum_h"], True),
            (f"Herdhaube lichte Hoehe, {fig} (aufrecht darunter)", round(FIGUREN[fig][0] + 0.05, 2), ist["haube_h"], True),
        ):
            ok = ist_ + 1e-6 >= soll_
            zeile(name, soll_, ist_, ok, "" if ok else ("zu knapp (nicht Pflicht)" if not pflicht else "ZU KNAPP"))
            if pflicht and not ok: fehler += 1
    for fig in ("Zwergin", "Zwerg"):
        h = FIGUREN[fig][0]
        lo, hi = round(h * SITZ_FAKTOR[0], 2), round(h * SITZ_FAKTOR[1], 2)
        zeile(f"Sitzhoehe (Schemel, Bank), {fig}", f"{lo:.2f}-{hi:.2f}", ist["sitz"], lo - 0.02 <= ist["sitz"] <= hi + 0.02)
        zeile(f"Tischhoehe, {fig}", f"{lo + TISCH_ABSTAND[0]:.2f}-{hi + TISCH_ABSTAND[1]:.2f}", ist["tisch"], lo + TISCH_ABSTAND[0] - 0.02 <= ist["tisch"] <= hi + TISCH_ABSTAND[1] + 0.02)
        zeile(f"Augenhoehe (Blick_Auge), {fig}", round(h * AUGE_FAKTOR, 2), ist["auge"], abs(ist["auge"] - h * AUGE_FAKTOR) <= 0.08)
        zeile(f"Fensterbruestung (Blick hinaus), {fig}", f"<= {round(h * AUGE_FAKTOR - 0.15, 2)}", ist["bruestung"], ist["bruestung"] <= h * AUGE_FAKTOR - 0.15 + 1e-6)
        for name, oben in sorted(ist["greif"].items()):
            zeile(f"Greifhoehe: {name} (Oberkante), {fig}", f"<= {round(h * GREIF_FAKTOR, 2)}", oben, oben <= h * GREIF_FAKTOR + 1e-6)
    zeile("Stufe vor der Tuer (Menschen- und Zwergenbein)", "<= 0.20", ist["stufe"], ist["stufe"] <= 0.20)
    for fig, b in ist["galerie_stehbreite"].items():
        zeile(f"Galerie: Breite, auf der {fig} aufrecht steht ({FIGUREN[fig][0]:.2f} m + 0,05)", ">= 1.0 (Zwerge)" if fig.startswith("Zwerg") else "-", round(b, 2), b >= 1.0 if fig.startswith("Zwerg") else True,
              "" if fig.startswith("Zwerg") or b > 0 else "dort nur kriechen")
    ausgabe(f"SOLLIST Grundform {form}: Soll gegen Ist")
    for name, sollwert, istwert, ok, bem in zeilen:
        ausgabe(f"SOLLIST   {name:64s} Soll {str(sollwert):>12s}   Ist {istwert if isinstance(istwert, str) else format(istwert, '.2f'):>6s}   {'ok' if ok else 'FEHLT'} {bem}")
    fehler += sum(1 for n, sw, iw, ok, b in zeilen if not ok and ("Zwerg" in n) and "Greifhoehe" in n)
    return zeilen, fehler


if __name__ == "__main__":
    for name, z in soll().items():
        print(f"{name:9s} {FIGUREN[name][0]:.2f} m: Tuer {z['tuer_h']:.2f} x {z['tuer_b']:.2f}, Raum {z['raum_h']:.2f}, Greifhoehe {z['greif']:.2f}, Sitz {z['sitz'][0]:.2f}-{z['sitz'][1]:.2f}, Auge {z['auge']:.2f}")
    sys.exit(0)
