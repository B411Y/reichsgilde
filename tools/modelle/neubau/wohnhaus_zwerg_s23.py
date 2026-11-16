"""Neubau der Gebaeude: Wohnhaus Zwerg, Stufe 2 (Buergerhaus) und Stufe 3 (Steinhaus), Grundformen 1 bis 4 - aussen und innen.

    blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_zwerg_s23.py -- --stufe=<2|3> --form=<1|2|3|4> [--bilder=<ordner>] [--ohne-ao]

Wie bei Stufe 1 (wohnhaus_zwerg_s1.py) eine SCHICHT auf den Bauskripten der Menschen (wohnhaus_mensch_s23.py mit _masse und _formen,
unveraendert):
  * Stoff, Dateinamen, Atlasgruppen: trim_s2_zwerg bzw. trim_s3_zwerg (aussen), innen_trim_s23_zwerg (innen), Gruppen wohnhaus_zwerg_s<n>
    und wohnhaus_zwerg_innen; alle Dateien heissen wohnhaus_zwerg_s<stufe>_g<form>[_innen].glb. Im Spiel liegen die Blaetter als Ebenen 4 und 5
    im Haeuser-Array (Steinebenen.cs, trimsheet_ebenen.py: der alte dunkle Stein der Zwerge, im Steinhaus das Band „bruchstein“ heller Haustein).
  * Masse, Waende, Fenster, Tuer, Treppe, Raeume, Wappenregel (wappen_ort): die des Menschenhauses (innen exakt massgleich mit aussen; Menschen
    muessen hinein). Zwergenhoehen: Tisch 0,62, Sitz 0,36, Blick 1,25 m, Apotheke/Bauplan/Werkzeug auf Greifhoehe (wie Stufe 1).
  * Stufe 2 (Kunstwerk wohnhaus_lvl2_zwerg): Erdgeschoss aus dunklem Quader (Band „putz“ wie Stufe 1), Obergeschoss und Giebel in Fachwerk mit
    Steinfuellung nach der Konstruktionsregel des Menschen (Raster 0,95 m, ein Fenster je Gefach, Giebel bis zum First), Schieferdach.
  * Stufe 3 (Kunstwerk wohnhaus_lvl3_zwerg): GANZ STEIN. Mauer dunkler Quader (Band „putz“), Werkstein (Eckquader, Gewaende, Gesims,
    Treppengiebel, Schornstein) heller Haustein im Band „bruchstein“; der Mittelpfosten zwischen zwei Fenstern eines Paars ist ein
    HOLZPFOSTEN 25 x 10 cm (Nutzer 16.11.2026; kein Steinpfeiler mit tiefen Laibungen, keine Ankereisen), Laeden und Balkenkoepfe dunkles Holz.
  * vor der Tuer ein Amboss (Kunstwerke), innerhalb der Dreiecksgrenze 4.000 der Huelle.
Messung: wohnhaus_zwerg_neu_messen.py; Soll/Ist: wohnhaus_zwerg_s23_masse.py.
"""
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
sys.path.insert(0, os.path.join(os.path.dirname(HIER), "raeume"))
import bpy                                                         # noqa: E402,F401
from mathutils import Vector                                       # noqa: E402
import wohnhaus_mensch_s1_g1 as g1                                 # noqa: E402
import wohnhaus_mensch_s1 as s1                                    # noqa: E402
import wohnhaus_mensch_s23 as S23                                  # noqa: E402
import zwerg_teile as zt                                           # noqa: E402
import wohnhaus_zwerg_s1 as Z1                                     # noqa: E402  (Konstanten der Stufe 1)
import wohnhaus_zwerg_s23_masse as ZM                              # noqa: E402

VOLK = "zwerg"
BLICK_TIEFER = Z1.BLICK_TIEFER
APOTHEKE_TIEFER, BAUPLAN_TIEFER = Z1.APOTHEKE_TIEFER, Z1.BAUPLAN_TIEFER


def umbenennen(text):
    return text.replace("mensch", VOLK)


def gewaende_zwerg(h, wand, spannen, z0, z1):
    """Gewaende des Steinhauses wie bei den Menschen (Haustein: Gewaendesteine, Sohlbank, Sturz), aber der Mittelpfosten eines
    Fensterpaars ist ein Holzpfosten 25 x 10 cm (deckt beide Laibungskanten mit je 4 cm, 10 cm vor der Mauer, von der Sohlbank bis zum
    Sturz). Er ist ein Holzakzent: versteinern() laesst ihn Holz."""
    st, band = "Bruchstein", S23.WERKSTEIN
    links, rechts = spannen[0][0], spannen[-1][1]
    for a, b in ((links - 0.13, links), (rechts, rechts + 0.13)):
        S23.wkasten(h, st, wand, a, b, 0.0, 0.05, z0 - 0.02, z1 + 0.02, band, ohne=("-z", "+z"), laengs="z")

    def pfosten():
        for i in range(len(spannen) - 1):
            mitte = (spannen[i][1] + spannen[i + 1][0]) / 2
            S23.wkasten(h, "Fachwerk", wand, mitte - 0.125, mitte + 0.125, 0.0, 0.10, z0 - 0.005, z1 + 0.005, "balken_c", laengs="z")
    if len(spannen) > 1:
        S23.holz_akzent(h, pfosten)
    S23.wkasten(h, st, wand, links - 0.17, rechts + 0.17, 0.0, 0.08, z0 - 0.11, z0 - 0.005, band, laengs="x")
    S23.wkasten(h, st, wand, links - 0.17, rechts + 0.17, 0.0, 0.06, z1 + 0.005, z1 + 0.17, band, laengs="x")


def schicht_anlegen(stufe):
    """Stoff, Dateinamen und Atlasgruppen auf den Zwerg umstellen; Zwergenhoehen; Stein der Zwerge; Dinge der Zwerge."""
    alt_stoff, alt_uv, alt_exp = g1.stoff, g1.uv2_und_ao, g1.exportiere
    g1.stoff = lambda name, blatt=None, **kw: alt_stoff(name, (blatt + "_" + VOLK) if blatt else None, **kw)   # trim_s2 -> trim_s2_zwerg, innen_trim_s23 -> ..._zwerg
    g1.uv2_und_ao = lambda gruppen, gruppe, modell, mit_ao=True: alt_uv(gruppen, umbenennen(gruppe), umbenennen(modell), mit_ao)
    g1.exportiere = lambda objekte, datei: alt_exp(objekte, umbenennen(datei))
    g1.TISCH_HOEHE, g1.SITZ_HOEHE = Z1.ZWERG_TISCH, Z1.ZWERG_SITZ
    alt_anker, alt_init = g1.anker, s1.Anker.__init__

    def ort(name, o):
        o = Vector(o)
        if name in ("Blick_Auge", "Blick_Ziel"): o.z -= BLICK_TIEFER
        return o
    g1.anker = lambda name, o, blick=None, **kw: alt_anker(name, ort(name, o), blick, **kw)
    s1.Anker.__init__ = lambda self, name, o, blick=None, **ex: alt_init(self, name, ort(name, o), blick, **ex)
    # Greifhoehe wie Stufe 1
    a_apo, a_plan = g1.baue_hausapotheke, g1.baue_bauplan
    # tiefer gehaengt laege die Seitenwand der Apotheke in der Ebene der Riegelstuetze an der Haustuer (deckungsgleich): 1,5 cm zur Seite
    g1.baue_hausapotheke = lambda: s1.versetze(a_apo(), t=(0.015, 0.0), dz=-APOTHEKE_TIEFER)
    g1.baue_bauplan = lambda: s1.versetze(a_plan(), dz=-BAUPLAN_TIEFER)
    g1.baue_werkzeug = lambda: zt.werkzeug(g1.IX)
    # Innen: Steinpflaster statt Dielen (Stufe 3 behaelt das Schachbrett aus dunklem und hellem Stein)
    alt_streifen = S23.streifen_boden
    S23.streifen_boden = lambda r, band, *a, **kw: alt_streifen(r, "lehmboden" if band == "bohlen" else band, *a, **kw)
    # Mauer aus dunklem Quader der Zwerge (Band „putz“ wie im Erdgeschoss der Stufe 1); bei den Menschen Bruchstein bzw. K1
    alt_wand = S23.steinwand
    S23.steinwand = lambda h, stoff, wand, z0, z1, band="putz", laibung=True: alt_wand(h, stoff, wand, z0, z1, band, laibung)
    if stufe == 3:
        # Werkstein (Eckquader, Gewaende, Abdeckplatten) im Band „bruchstein“: der helle Haustein der Zwerge (Ebene zwerg_s3)
        S23.WERKSTEIN = "bruchstein"
        S23.gewaende = gewaende_zwerg
    # Aussen: Amboss neben der Haustuer an der Gassenwand (Kunstwerke Stufe 2 und 3)
    alt_haus = S23.baue_haus

    def haus():
        h = alt_haus()
        vorher = h.dreiecke()
        tuer = next(f for f in S23.M.FENSTER if f[3] == "tuer")
        wand, (a0, a1) = tuer[0], tuer[1]
        (ox, oy), (ex, ey), laenge, (nx, ny) = S23.WAENDE[wand]
        start = ox if ex else oy
        s0, s1_ = a0 - start, a1 - start
        rechts = laenge - s1_ >= s0                          # auf der Seite mit mehr Platz
        sa = s1_ + 0.85 if rechts else s0 - 0.85
        lage = lambda s, raus: (ox + ex * s + nx * raus, oy + ey * s + ny * raus)
        zt.schmiedegeraet(h, lage(sa, 0.50), (nx, ny), (ex * (1 if rechts else -1), ey * (1 if rechts else -1)), schlank=True, nur_amboss=True)
        print("DREIECKE Schmiedegeraet:", h.dreiecke() - vorher)
        return h
    S23.baue_haus = haus


def messen(stufe, form, ausgabe_ordner=None):
    """Ist-Masse aus der gebauten Szene (Anker, Netze) und den Massen der Mensch-Skripte; Soll gegen Ist: wohnhaus_zwerg_s23_masse.pruefe."""
    M = S23.M
    objs = bpy.data.objects
    z0 = M.Z_BODEN
    anker = {o.name: o.location.copy() for o in objs if o.type == "EMPTY"}
    tuer = next(f for f in M.FENSTER if f[3] == "tuer")
    bruestung = min(f[2][0] for f in M.FENSTER if f[3] == "fenster" and f[2][0] < M.Z_EG_DECKE)

    def oben(name):
        o = objs.get(name)
        return None if o is None else max((o.matrix_world @ Vector(c)).z for c in o.bound_box) - z0
    greif = {n[n.index("_") + 1:]: oben(n) for n in ("Gegenstand_Truhe", "Gegenstand_Familienbuch", "Gegenstand_Bauplan", "Gegenstand_Werkzeug", "Gegenstand_Geld", "Einrichtung_Hausapotheke")}
    greif = {k: v for k, v in greif.items() if v is not None}
    loch = M.TREPPE_LOCH_X[1]
    stufe_hoch = (M.TREPPE_FUSS_X - loch) / M.AUFTRITT * M.STEIGUNG
    ist = dict(tuer_h=tuer[2][1] - tuer[2][0], tuer_b=tuer[1][1] - tuer[1][0], raum_h=M.Z_EG_DECKE - z0, haube_h=g1.Z_HAUBE - z0, sitz=g1.SITZ_HOEHE,
               tisch=g1.TISCH_HOEHE, auge=anker["Blick_Auge"].z - z0, bruestung=bruestung - z0, stufe=M.STUFE_VOR_TUER, greif=greif, galerie_stehbreite={},
               zimmertuer=M.ZIMMERTUER, treppe_steigung=M.STEIGUNG, treppe_kopfhoehe=M.Z_EG_DECKE - (z0 + stufe_hoch), og_raum_h=M.Z_OG_DECKE - M.Z_OG,
               dach_bib_stehbreite=None)
    zeilen = []
    ZM.pruefe(stufe, form, ist, ausgabe=zeilen.append)
    for z in zeilen: print(z)
    if ausgabe_ordner:
        os.makedirs(ausgabe_ordner, exist_ok=True)
        open(os.path.join(ausgabe_ordner, f"soll_ist_s{stufe}_g{form}.txt"), "w", encoding="utf-8").write("\n".join(z.replace("SOLLIST ", "", 1) for z in zeilen) + "\n")


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    stufe = int(opt.get("stufe", "2"))
    schicht_anlegen(stufe)
    S23.main()
    messen(stufe, int(opt.get("form", "1")), opt.get("bilder"))


if __name__ == "__main__":
    main()
