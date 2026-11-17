"""Neubau Wohnhaus Elb, Stufe 1: Soll gegen Ist der Masse (reines Python, ohne Blender). Muster: zwerg_masse.py.

    python tools/modelle/neubau/elb_masse.py            # nur das Soll

Regel (Auftrag 17.11.2026): Elben sind schlanker und hoeher (Elb 1,85 m, in der Passanten-Probe 1,80 bis 1,90 m, Elbinnen 5 %
kleiner); Haustuer und Decken muessen fuer Elben, Menschen (1,75 m) und Zwerge passen. Gerechnet wird mit dem groessten Elb (1,90 m):
Haustuer 1,90 + 0,20 = 2,10 m, Herdhaube 1,90 + 0,05 = 1,95 m. Die Figurenmasse und Zuschlaege kommen aus zwerg_masse.py.
"""
import sys

import zwerg_masse as ZM

ELB_GROESST = 1.90              # Passanten-Probe: 1,85 m Mitte, je Figur +-5 cm (Stadtansicht.Passantenprobe.ProbeKoerpergroesse)
FIGUREN = dict(ZM.FIGUREN)
FIGUREN["Elb (groesster)"] = (ELB_GROESST, 0.44)
FIGUREN["Elbin"] = (round(1.85 * 0.95, 2), 0.42)
PFLICHT = ("Elb (groesster)", "Elb", "Elbin", "Mensch", "Zwerg", "Zwergin")


def soll():
    z = {}
    for name, (h, b) in FIGUREN.items():
        z[name] = dict(tuer_h=round(h + ZM.KOPF, 2), tuer_b=round(b + ZM.SCHULTER_LUFT, 2), raum_h=round(h + ZM.RAUM_LUFT, 2),
                       haube_h=round(h + 0.05, 2), sitz=tuple(round(h * f, 2) for f in ZM.SITZ_FAKTOR), auge=round(h * ZM.AUGE_FAKTOR, 2))
    return z


def pruefe(form, ist, ausgabe=print):
    """Tabelle Soll gegen Ist. `ist`: tuer_h, tuer_b, raum_h, haube_h, sitz, tisch, auge, stufe. Liefert (Zeilen, Fehler)."""
    s = soll()
    zeilen, fehler = [], 0
    for fig in PFLICHT:
        z = s[fig]
        for name, soll_, ist_ in ((f"Haustuer Hoehe, {fig} {FIGUREN[fig][0]:.2f} m", z["tuer_h"], ist["tuer_h"]),
                                  (f"Haustuer Breite, {fig}", z["tuer_b"], ist["tuer_b"]),
                                  (f"Raumhoehe (Decke ueber dem Fussboden), {fig}", z["raum_h"], ist["raum_h"]),
                                  (f"Herdhaube lichte Hoehe, {fig} (aufrecht darunter)", z["haube_h"], ist["haube_h"])):
            ok = ist_ + 1e-6 >= soll_
            zeilen.append((name, soll_, ist_, ok))
            fehler += 0 if ok else 1
    h = FIGUREN["Elb"][0]
    lo, hi = round(h * ZM.SITZ_FAKTOR[0], 2), round(h * ZM.SITZ_FAKTOR[1], 2)
    for name, sollwert, istwert, ok in (
            ("Sitzhoehe (Schemel, Bank), Elb", f"{lo:.2f}-{hi:.2f}", ist["sitz"], lo - 0.02 <= ist["sitz"] <= hi + 0.02),
            ("Tischhoehe, Elb", f"{lo + ZM.TISCH_ABSTAND[0]:.2f}-{hi + ZM.TISCH_ABSTAND[1]:.2f}", ist["tisch"],
             lo + ZM.TISCH_ABSTAND[0] - 0.02 <= ist["tisch"] <= hi + ZM.TISCH_ABSTAND[1] + 0.02),
            ("Augenhoehe (Blick_Auge), Elb", round(h * ZM.AUGE_FAKTOR, 2), ist["auge"], abs(ist["auge"] - h * ZM.AUGE_FAKTOR) <= 0.08),
            ("Stufe vor der Tuer", "<= 0.20", ist["stufe"], ist["stufe"] <= 0.20)):
        zeilen.append((name, sollwert, istwert, ok))
        fehler += 0 if ok else 1
    ausgabe(f"SOLLIST Grundform {form}: Soll gegen Ist (Elb)")
    for name, sollwert, istwert, ok in zeilen:
        ausgabe(f"SOLLIST   {name:64s} Soll {str(sollwert):>12s}   Ist {format(istwert, '.2f'):>6s}   {'ok' if ok else 'FEHLT'}")
    return zeilen, fehler


if __name__ == "__main__":
    for name, z in soll().items():
        print(f"{name:16s} {FIGUREN[name][0]:.2f} m: Tuer {z['tuer_h']:.2f} x {z['tuer_b']:.2f}, Raum {z['raum_h']:.2f}, Haube {z['haube_h']:.2f}")
    sys.exit(0)
