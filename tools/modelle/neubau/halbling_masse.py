"""Neubau Wohnhaus Halbling, Stufe 1: Soll gegen Ist der Masse (reines Python, ohne Blender). Muster: elb_masse.py, zwerg_masse.py.

    python tools/modelle/neubau/halbling_masse.py            # nur das Soll

Regel (Auftrag 17.11.2026, Kunstwerk wohnhaus_lvl1_halbling.jpg): Halblinge sind klein (1,05 bis 1,15 m in der Passanten-Probe,
Halblinginnen 5 % kleiner), ihr Haus im Kunstwerk ist aber ein Fachwerkhaus in Menschengroesse (die Haustuer ist gut doppelt so hoch
wie die Faesser davor). Darum gilt fuer Haustuer, Decken, Herdhaube und Dachgeschoss: es passen Halblinge, Zwerge und Menschen (1,75 m)
hinein; der Elb (1,85 m) steht zur Kenntnis in der Tabelle (die Tuer der Menschen ist 2,00 m). Tisch, Sitz und Blick (Standard-
Innenkamera) sind fuer den Halbling (1,10 m) gemacht wie bei den Zwergen (wohnhaus_zwerg_s1.py). Figurenmasse und Zuschlaege aus
zwerg_masse.py.
"""
import sys

import zwerg_masse as ZM

HALBLING_GROESST = 1.15         # Passanten-Probe: 1,10 m Mitte, je Figur +-5 cm (Stadtansicht.Passantenprobe.ProbeKoerpergroesse)
FIGUREN = dict(ZM.FIGUREN)
FIGUREN["Halbling (groesster)"] = (HALBLING_GROESST, 0.34)
FIGUREN["Halblingin"] = (round(1.10 * 0.95, 2), 0.30)
PFLICHT = ("Halbling (groesster)", "Halbling", "Halblingin", "Zwerg", "Mensch")   # Elb nur zur Kenntnis
KENNTNIS = ("Elb",)
DG_LUFT = 0.10                # Dachgeschoss (Schlafen, Kehlbalken): Kopffreiheit ueber dem Scheitel
DG_STEHBREITE = 1.0           # Breite, auf der man im Dachgeschoss aufrecht steht


def soll():
    z = {}
    for name, (h, b) in FIGUREN.items():
        z[name] = dict(tuer_h=round(h + ZM.KOPF, 2), tuer_b=round(b + ZM.SCHULTER_LUFT, 2), raum_h=round(h + ZM.RAUM_LUFT, 2),
                       haube_h=round(h + 0.05, 2), sitz=tuple(round(h * f, 2) for f in ZM.SITZ_FAKTOR), auge=round(h * ZM.AUGE_FAKTOR, 2))
    return z


def pruefe(form, ist, ausgabe=print):
    """Tabelle Soll gegen Ist. `ist`: tuer_h, tuer_b, raum_h, haube_h, sitz, tisch, auge, stufe (dg_h, dg_breite). Liefert (Zeilen, Fehler)."""
    s = soll()
    zeilen, fehler = [], 0
    for fig in PFLICHT + KENNTNIS:
        z = s[fig]
        pflicht = fig in PFLICHT
        for name, soll_, ist_ in ((f"Haustuer Hoehe, {fig} {FIGUREN[fig][0]:.2f} m", z["tuer_h"], ist["tuer_h"]),
                                  (f"Haustuer Breite, {fig}", z["tuer_b"], ist["tuer_b"]),
                                  (f"Raumhoehe (Decke ueber dem Fussboden), {fig}", z["raum_h"], ist["raum_h"]),
                                  (f"Herdhaube lichte Hoehe, {fig} (aufrecht darunter)", z["haube_h"], ist["haube_h"])):
            ok = ist_ + 1e-6 >= soll_
            zeilen.append((name + ("" if pflicht else " (zur Kenntnis)"), soll_, ist_, ok or not pflicht))
            fehler += 0 if (ok or not pflicht) else 1
    if "dg_h" in ist:
        for fig in PFLICHT:
            hf = FIGUREN[fig][0]
            ok = ist["dg_h"] + 1e-6 >= hf + DG_LUFT
            zeilen.append((f"Dachgeschoss: lichte Hoehe unter dem Kehlbalken, {fig}", round(hf + DG_LUFT, 2), ist["dg_h"], ok)); fehler += 0 if ok else 1
            b = ist["dg_breite"](hf + 0.05)
            ok = b + 1e-6 >= DG_STEHBREITE
            zeilen.append((f"Dachgeschoss: Breite aufrecht ({hf:.2f} m + 0,05), {fig}", DG_STEHBREITE, round(b, 2), ok)); fehler += 0 if ok else 1
    h = FIGUREN["Halbling"][0]
    lo, hi = round(h * ZM.SITZ_FAKTOR[0], 2), round(h * ZM.SITZ_FAKTOR[1], 2)
    for name, sollwert, istwert, ok in (
            ("Sitzhoehe (Schemel, Bank), Halbling", f"{lo:.2f}-{hi:.2f}", ist["sitz"], lo - 0.02 <= ist["sitz"] <= hi + 0.02),
            ("Tischhoehe, Halbling", f"{lo + ZM.TISCH_ABSTAND[0]:.2f}-{hi + ZM.TISCH_ABSTAND[1]:.2f}", ist["tisch"],
             lo + ZM.TISCH_ABSTAND[0] - 0.02 <= ist["tisch"] <= hi + ZM.TISCH_ABSTAND[1] + 0.02),
            ("Augenhoehe (Blick_Auge), Halbling", round(h * ZM.AUGE_FAKTOR, 2), ist["auge"], abs(ist["auge"] - h * ZM.AUGE_FAKTOR) <= 0.08),
            ("Stufe vor der Tuer", "<= 0.20", ist["stufe"], ist["stufe"] <= 0.20)):
        zeilen.append((name, sollwert, istwert, ok))
        fehler += 0 if ok else 1
    ausgabe(f"SOLLIST Grundform {form}: Soll gegen Ist (Halbling)")
    for name, sollwert, istwert, ok in zeilen:
        ausgabe(f"SOLLIST   {name:72s} Soll {str(sollwert):>12s}   Ist {format(istwert, '.2f'):>6s}   {'ok' if ok else 'FEHLT'}")
    return zeilen, fehler


if __name__ == "__main__":
    for name, z in soll().items():
        print(f"{name:22s} {FIGUREN[name][0]:.2f} m: Tuer {z['tuer_h']:.2f} x {z['tuer_b']:.2f}, Raum {z['raum_h']:.2f}, Haube {z['haube_h']:.2f}, "
              f"Sitz {z['sitz'][0]:.2f}-{z['sitz'][1]:.2f}, Auge {z['auge']:.2f}")
    sys.exit(0)
