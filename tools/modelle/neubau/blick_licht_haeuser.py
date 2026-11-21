"""Pruefung 12b, 12c und 38 (blick_licht_pruefung.py) an den Wohnhaeusern der Menschen und Zwerge, Stufe 1 bis 3, Grundform 1-4:
12b an der Huelle, 12c in Wohnstube, Hinterzimmer und Bibliothek (soweit das Haus sie hat), 38 in der Wohnstube. Dieselben Fehlermuster wie beim
Klicktest der Elben (18.11.2026): Vordach ohne Dicke, Dachlagen nach unten, Grundlicht Licht_Raum hinter den Flaechen zur Kamera,
Deckenbalken vor der Wand im Standardblick.

Aufruf aus wohnhaus_mensch_messen.py und wohnhaus_zwerg_neu_messen.py (blick_licht), oder fuer alle Haeuser eines Volks auf einmal:

    blender -b --factory-startup --python tools/modelle/neubau/blick_licht_haeuser.py -- --volk=<mensch|zwerg> [--haus=<name>]
            [--ordner=<glb-ordner>] [--texturen=<ordner>]
"""
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import blick_licht_pruefung as B                                          # noqa: E402

WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(HIER)))
ORDNER = os.path.join(WURZEL, "game", "assets", "modelle", "gebaeude", "neubau")
TEXTUREN = os.path.join(WURZEL, "game", "assets", "texturen", "neubau")


def raeume(haus):
    """(Endung, Blatt, AO-Atlas) je Raum: Wohnstube (Stufe 1 innen_trim, Stufe 2/3 innen_trim_s23), Hinterzimmer und Bibliothek
    (raeume_innen_trim; Bibliothek der Stufe 3 im Dachgeschoss mit eigenem Atlas). Bei den Zwergen die Blaetter mit "_zwerg"."""
    volk = "zwerg" if "_zwerg_" in haus else "mensch"
    z = "_zwerg" if volk == "zwerg" else ""
    stufe = int(haus.rsplit("_s", 1)[1].split("_")[0])
    wohn = ("innen_trim" if stufe == 1 else "innen_trim_s23") + z
    return (("", wohn, f"wohnhaus_{volk}_innen_ao_atlas.png"),
            ("_hinterzimmer", "raeume_innen_trim" + z, f"wohnhaus_{volk}_raeume_innen_ao_atlas.png"),
            ("_bibliothek", "raeume_innen_trim" + z,
             f"wohnhaus_{volk}_{'dachraeume' if stufe == 3 else 'raeume'}_innen_ao_atlas.png"))


def blick_licht(haus, ordner=None, texturen=None):
    """12b an der Huelle, 12c in jedem vorhandenen Raum. Rueckgabe (Zeilen, Fehlerzahl)."""
    ordner, texturen = ordner or ORDNER, texturen or TEXTUREN
    zeilen, fehler = [], 0
    z, f = B.rueckseite_offen(os.path.join(ordner, f"{haus}.glb"))
    zeilen.append(f"12b. Keine offene Rueckseite aussen (von oben nur die Rueckseite sichtbar): {'ok' if not f else str(f) + ' FEHLER'}")
    zeilen += z; fehler += f
    for endung, blatt, atlas in raeume(haus):
        glb = os.path.join(ordner, f"{haus}{endung}_innen.glb")
        if not os.path.exists(glb):
            continue
        z, f = B.licht_im_blick(glb, os.path.join(texturen, f"{blatt}_albedo.png"), os.path.join(texturen, atlas))
        zeilen.append(f"12c. Helle Flaechen im Standardblick mit direktem Licht ({endung[1:] or 'wohnstube'}): {'ok' if not f else str(f) + ' FEHLER'}")
        zeilen += z; fehler += f
        if endung: continue                                    # 38 nur in der Wohnstube (wie bei den Halblingen)
        z, f = B.blick_wand(glb)
        zeilen.append(f"38. Standardblick nicht dicht vor einer Wand (wohnstube): {'ok' if not f else 'FEHLER'}"); zeilen += z; fehler += f
    return zeilen, fehler


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    haeuser = [opt["haus"]] if "haus" in opt else [f"wohnhaus_{opt['volk']}_s{s}_g{g}" for s in (1, 2, 3) for g in (1, 2, 3, 4)]
    summe = 0
    for haus in haeuser:
        zeilen, fehler = blick_licht(haus, opt.get("ordner"), opt.get("texturen"))
        print(f"MESSUNG Messung 12b/12c {haus}")
        for z in zeilen: print("MESSUNG " + z)
        print(f"MESSUNG Ergebnis {haus}: {fehler} Fehler")
        summe += fehler
    print(f"MESSUNG Summe: {summe} Fehler")


if __name__ == "__main__":
    main()
