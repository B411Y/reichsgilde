"""Wohnhaus Elb Stufe 2 (Buergerhaus) und 3 (Steinhaus): Soll gegen Ist der Masse (reines Python, ohne Blender).

    python tools/modelle/neubau/wohnhaus_elb_s23_masse.py       # nur das Soll

Haengt nur an elb_masse.py (Figuren, groesster Elb 1,90 m; Muster: wohnhaus_zwerg_s23_masse.py). Das Bauskript (wohnhaus_elb_s23.py) misst
am Ende jeder Grundform den Ist-Stand und ruft `pruefe(stufe, form, ist)`. Regel wie Stufe 1: Haustuer, Zimmertueren, Decken, Treppe und
Herdhaube fuer Elben (bis 1,90 m), Elbinnen und Menschen (1,75 m); Zwerge gehen nicht in Elbenhaeuser (Auftrag 17.11.2026).
"""
import sys

import elb_masse as EB
import zwerg_masse as ZM

TREPPE_STEIGUNG_MAX = 0.20                           # Steigung der Treppe ins Obergeschoss (Mensch 13 x 0,20)
KOPF_TREPPE = 0.10                                   # Luft ueber dem Kopf an der Treppenoeffnung


def pruefe(stufe, form, ist, ausgabe=print):
    """`ist`: wie bei elb_masse.pruefe (tuer_h, tuer_b, raum_h, haube_h, sitz, tisch, auge, stufe) und dazu zimmertuer (Breite, Hoehe),
    treppe_steigung, treppe_kopfhoehe (lichte Hoehe unter der Deckenoeffnung ueber der Lauflinie), og_raum_h (lichte Hoehe der Raeume im
    Obergeschoss). Liefert (Zeilen, Fehler)."""
    zeilen = []
    _z, fehler = EB.pruefe(f"S{stufe} G{form}", ist, ausgabe=zeilen.append)
    soll = EB.soll()
    tb, th = ist["zimmertuer"]
    extra = []
    for fig in EB.PFLICHT:
        h = EB.FIGUREN[fig][0]
        extra.append((f"Zimmertuer (Hinterzimmer, Bibliothek) Breite x Hoehe, {fig} {h:.2f} m", f">= {soll[fig]['tuer_b']:.2f} x {soll[fig]['tuer_h']:.2f}",
                      f"{tb:.2f}x{th:.2f}", tb + 1e-6 >= soll[fig]["tuer_b"] and th + 1e-6 >= soll[fig]["tuer_h"]))
    h = EB.ELB_GROESST
    extra.append(("Treppe: Steigung", f"<= {TREPPE_STEIGUNG_MAX:.2f}", ist["treppe_steigung"], ist["treppe_steigung"] <= TREPPE_STEIGUNG_MAX + 1e-6))
    extra.append((f"Treppe: lichte Hoehe an der Deckenoeffnung, Elb {h:.2f} m", f">= {h + KOPF_TREPPE:.2f}", ist["treppe_kopfhoehe"], ist["treppe_kopfhoehe"] >= h + KOPF_TREPPE - 1e-6))
    extra.append((f"Raeume im Obergeschoss: Raumhoehe, Elb {h:.2f} m", f">= {h + ZM.RAUM_LUFT:.2f}", ist["og_raum_h"], ist["og_raum_h"] >= h + ZM.RAUM_LUFT - 1e-6))
    for n, s, i, ok in extra:
        zeilen.append("SOLLIST " + f"  {n:64s} Soll {str(s):>12s}   Ist {i if isinstance(i, str) else format(i, '.2f'):>9s}   {'ok' if ok else 'FEHLT'}")
        if not ok: fehler += 1
    for z in zeilen: ausgabe(z)
    return zeilen, fehler


if __name__ == "__main__":
    for name, z in EB.soll().items():
        print(f"{name:16s} {EB.FIGUREN[name][0]:.2f} m: Tuer {z['tuer_h']:.2f} x {z['tuer_b']:.2f}, Raum {z['raum_h']:.2f}, Haube {z['haube_h']:.2f}")
    sys.exit(0)
