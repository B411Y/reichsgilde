"""Wohnhaus Zwerg Stufe 2 (Buergerhaus) und 3 (Steinhaus): Soll gegen Ist der Masse (reines Python, ohne Blender).

    python tools/modelle/neubau/wohnhaus_zwerg_s23_masse.py       # nur das Soll

Dieses Modul haengt nur an zwerg_masse.py
(Figuren, Zwergenhoehen). Das Bauskript (wohnhaus_zwerg_s23.py) misst am Ende jeder Grundform den Ist-Stand aus der Szene und ruft
`pruefe(stufe, form, ist)`. Regeln (Auftrag): Zwergenhoehen fuer Moebel, Tueren und Decken so, dass Menschen hineinpassen, Elb mitgerechnet;
Ork und Oger werden nicht betrachtet (Nutzer 16.11.2026).
"""
import sys

import zwerg_masse as ZM

TISCH, SITZ, AUGE_ZWERG = 0.62, 0.36, 1.25           # Stufe 1 (Entscheidung), gelten unveraendert
TREPPE_STEIGUNG_MAX = 0.20                           # Steigung der Treppe ins Obergeschoss (Mensch 13 x 0,20)
ZIMMERTUER_MIN = (0.80, 1.95)                        # Zimmertuer: Mensch 1,75 + 0,20 hoch, schmal wie die Haustuer G2/G4 (Nutzer)
KOPF_TREPPE = 0.10                                   # Luft ueber dem Kopf an der Treppenoeffnung


def pruefe(stufe, form, ist, ausgabe=print):
    """`ist`: wie bei zwerg_masse.pruefe (Schluessel tuer_h, tuer_b, raum_h, haube_h, sitz, tisch, auge, bruestung, stufe, greif,
    galerie_stehbreite) und zusaetzlich zimmertuer (Breite, Hoehe), treppe_steigung, treppe_kopfhoehe (lichte Hoehe unter der Decken-
    oeffnung ueber der Lauflinie), og_raum_h (lichte Hoehe der Raeume im Obergeschoss bzw. Dachgeschoss, oberste Stelle),
    dach_bib_stehbreite (Breite der Dachgeschoss-Bibliothek, auf der ein Zwerg bzw. Mensch aufrecht steht; Stufe 3, sonst None)."""
    zeilen, fehler = ZM.pruefe(f"S{stufe} G{form}", ist, ausgabe=lambda z: None)
    out = [f"SOLLIST Stufe {stufe} Grundform {form}: Soll gegen Ist (Zwerg, Zwergin, Mensch Pflicht; Elb zur Kenntnis)"]
    out += ["SOLLIST " + f"  {n:64s} Soll {str(s):>12s}   Ist {i if isinstance(i, str) else format(i, '.2f'):>6s}   {'ok' if ok else 'FEHLT'} {b}"
            for n, s, i, ok, b in zeilen]
    extra = []
    tb, th = ist["zimmertuer"]
    extra.append(("Zimmertuer (Hinterzimmer, Bibliothek) Breite x Hoehe, Mensch 1,75 m", f">= {ZIMMERTUER_MIN[0]:.2f} x {ZIMMERTUER_MIN[1]:.2f}", f"{tb:.2f}x{th:.2f}",
                  tb >= ZIMMERTUER_MIN[0] - 1e-6 and th >= ZIMMERTUER_MIN[1] - 1e-6))
    extra.append(("Treppe: Steigung", f"<= {TREPPE_STEIGUNG_MAX:.2f}", ist["treppe_steigung"], ist["treppe_steigung"] <= TREPPE_STEIGUNG_MAX + 1e-6))
    extra.append(("Treppe: lichte Hoehe an der Deckenoeffnung, Mensch 1,75 m", f">= {1.75 + KOPF_TREPPE:.2f}", ist["treppe_kopfhoehe"], ist["treppe_kopfhoehe"] >= 1.75 + KOPF_TREPPE - 1e-6))
    extra.append(("Raeume im Obergeschoss: Raumhoehe (oberste Stelle), Mensch 1,75 m", f">= {1.75 + ZM.RAUM_LUFT:.2f}", ist["og_raum_h"], ist["og_raum_h"] >= 1.75 + ZM.RAUM_LUFT - 1e-6))
    if ist.get("dach_bib_stehbreite") is not None:      # Stufe 3: Bibliothek im Dachgeschoss unter den Schraegen
        for fig, b in ist["dach_bib_stehbreite"].items():
            pflicht = fig.startswith("Zwerg")
            extra.append((f"Dachgeschoss-Bibliothek: Breite, auf der {fig} aufrecht steht", ">= 1.0 (Zwerge)" if pflicht else "-", f"{b:.2f}", b >= 1.0 if pflicht else True))
    for n, s, i, ok in extra:
        out.append("SOLLIST " + f"  {n:64s} Soll {str(s):>12s}   Ist {i if isinstance(i, str) else format(i, '.2f'):>6s}   {'ok' if ok else 'FEHLT'}")
        if not ok: fehler += 1
    for z in out: ausgabe(z)
    return out, fehler


if __name__ == "__main__":
    for name, z in ZM.soll().items():
        print(f"{name:9s} {ZM.FIGUREN[name][0]:.2f} m: Tuer {z['tuer_h']:.2f} x {z['tuer_b']:.2f}, Raum {z['raum_h']:.2f}, Greifhoehe {z['greif']:.2f}")
    print(f"Zwergenhoehen: Tisch {TISCH}, Sitz {SITZ}, Blick {AUGE_ZWERG}; Zimmertuer mindestens {ZIMMERTUER_MIN[0]} x {ZIMMERTUER_MIN[1]}")
    sys.exit(0)
