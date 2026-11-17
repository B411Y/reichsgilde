"""Hinterzimmer und Bibliothek fuer die Wohnhaeuser Zwerg: Schicht auf den Raumskripten der Menschen (unveraendert).

    blender -b --factory-startup --python tools/modelle/raeume/raeume_zwerg_s23.py -- --haus=wohnhaus_zwerg_s<1|2|3>_g<n> --raum=hinterzimmer|bibliothek [--ohne-ao] [--bilder=<ordner>]

Schreibt game/assets/modelle/gebaeude/neubau/<haus>_<raum>_innen.glb (im Spiel „InteriorRoot_<Raum>“, Stadtansicht.Wohnraeume.cs: der Raum
erscheint, wenn die Datei zu diesem Haus da ist). Wie bei den Menschen gibt es die Raeume in Stufe 1 nur fuer Grundform 1 (raum_hinterzimmer.py,
raum_bibliothek.py im Grundriss der Wohnstube), in Stufe 2 und 3 fuer alle vier Grundformen (raeume_s23.py: im Obergeschoss, die Bibliothek
des Steinhauses im Dachgeschoss). Das Raummodul ist je Haus dasselbe wie beim Menschen (gleiche Masse, Tueren, Fenster: innen = aussen);
anders: Zwergenhoehen (Tisch 0,62, Sitz 0,36, Schreibtisch 0,62, Pult 0,95, Blick 1,25 m), Trim-Sheet `raeume_innen_trim_zwerg` (Steinwand,
Pflaster im Hinterzimmer, Parkett in der Bibliothek; raeume_trimsheet_zwerg.py), AO-Gruppen `wohnhaus_zwerg_raeume_innen` und
`wohnhaus_zwerg_dachraeume_innen`.
"""
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "neubau"))
import raeume_masse as M                                                  # noqa: E402
import raeume_bib as R                                                    # noqa: E402
# raeume_s23 (Stufe 2/3) erst unten laden: es setzt beim Import die Masse in raeume_masse.py auf das Obergeschoss um

VOLK = "zwerg"


def umbenennen(text):
    return text.replace("mensch", VOLK)


def schicht_anlegen():
    M.HZ_SITZ = 0.36
    M.HZ_TISCH = (M.HZ_TISCH[0], M.HZ_TISCH[1], M.HZ_TISCH[2], 0.62)
    M.BI_SESSEL_SITZ = 0.36
    M.BI_SCHREIBTISCH = M.BI_SCHREIBTISCH[:4] + (0.62,)
    M.BI_LESEPULT = (M.BI_LESEPULT[0], M.BI_LESEPULT[1], 0.95, M.BI_LESEPULT[3])
    tief = M.AUGE - 1.25
    M.AUGE = 1.25                                                   # Augenhoehe eines Zwergs
    M.HZ_KAMERA = tuple((a[0], a[1], a[2] - tief) for a in M.HZ_KAMERA)
    M.BI_KAMERA = tuple((a[0], a[1], a[2] - tief) for a in M.BI_KAMERA)
    R.GRUPPE = umbenennen(R.GRUPPE)
    R.HAUS = umbenennen(R.HAUS)
    R.BLATT = R.BLATT + "_zwerg"
    alt_uv, alt_exp = R.uv2_und_ao, R.exportiere

    def uv(*a, **kw):
        R.GRUPPE = umbenennen(R.GRUPPE)                             # die Dachbibliothek setzt die Gruppe der Menschen erst in main
        return alt_uv(*a, **kw)
    R.uv2_und_ao = uv
    R.exportiere = lambda objekte, datei: alt_exp(objekte, umbenennen(datei))
    # Fensterpaare mit durchgehendem Rahmen und die 4 mm hoehere Fensterbank der Dachbibliothek baut das Mensch-Skript selbst
    # (raeume_bib.fensterlaibung, raum_bibliothek_dach; Mensch-Nachtrag #501): die Umhuellungen der Zwerge sind gestrichen


if __name__ == "__main__":
    schicht_anlegen()
    opt = R.optionen()
    if "_s1_" in opt.get("haus", ""):                                # Stufe 1, Grundform 1: der Raum im Grundriss der Wohnstube
        if not opt["haus"].endswith("_g1"):
            raise SystemExit("Stufe 1: Raeume nur in Grundform 1 (wie bei den Menschen)")
        import raum_hinterzimmer
        import raum_bibliothek
        {"hinterzimmer": raum_hinterzimmer, "bibliothek": raum_bibliothek}[opt["raum"]].main()
    else:
        import raeume_s23
        raeume_s23.main()
