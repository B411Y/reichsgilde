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
    R.fensterlaibung = fensterlaibung


def fensterlaibung(teil, band, bank_band=None):
    """Wie raeume_bib.fensterlaibung, aber ein Fensterpaar (Abstand unter 0,30 m) bekommt durchgehende Bretter oben und unten und einen
    gemeinsamen Mittelpfosten: zwei einzelne Rahmen lagen im Pfeiler deckungsgleich uebereinander (uv2_pruefung 12)."""
    S = R.S
    for wand in ("vorn", "links"):
        fen = sorted(R.fenster_der_wand(wand))
        gruppen = []
        for f in fen:
            if gruppen and f[0] - gruppen[-1][-1][1] < 0.30 and abs(f[2] - gruppen[-1][-1][2]) < 1e-3: gruppen[-1].append(f)
            else: gruppen.append([f])
        n = R.wandpunkt(wand, 0, 0, 1) - R.wandpunkt(wand, 0, 0, 0)
        for g in gruppen:
            a0, a1, z0, z1 = g[0][0], g[-1][1], g[0][2], g[0][3]
            bretter = [(R.wandpunkt(wand, a0 - 0.10, z0 - 0.04), R.wandpunkt(wand, a1 + 0.10, z0 - 0.04), 0.08),
                       (R.wandpunkt(wand, a0 - 0.10, z1 + 0.04), R.wandpunkt(wand, a1 + 0.10, z1 + 0.04), 0.08),
                       (R.wandpunkt(wand, a0 - 0.05, z0), R.wandpunkt(wand, a0 - 0.05, z1), 0.10),
                       (R.wandpunkt(wand, a1 + 0.05, z0), R.wandpunkt(wand, a1 + 0.05, z1), 0.10)]
            for (_, b_, _, _), (c_, _, _, _) in zip(g, g[1:]):          # Mittelpfosten im Pfeiler
                bretter.append((R.wandpunkt(wand, (b_ + c_) / 2, z0), R.wandpunkt(wand, (b_ + c_) / 2, z1), c_ - b_))
            for a, b, breite in bretter:
                teil.brett(S, a, b, breite, n, band, tiefe=0.035)
            if bank_band:
                for f0, f1, _, _ in g:
                    p0 = R.wandpunkt(wand, f0, z0, 0.04); p1 = R.wandpunkt(wand, f1, z0, -M.T / 2 + 0.03)
                    teil.kasten(S, (min(p0.x, p1.x), min(p0.y, p1.y), z0 - 0.03), (max(p0.x, p1.x), max(p0.y, p1.y), z0), bank_band)


def dachbank_anheben(raeume_s23):
    """Steinhaus: raum_bibliothek_dach.teile umhuellen (vorher die Masse des Steinhauses setzen: das Modul liest sie beim Import)."""
    raeume_s23.HM.stufe_setzen(3)
    import raum_bibliothek_dach as RD
    from mathutils import Vector
    alt_teile = RD.teile

    def teile(Q):
        """Dachbibliothek: die Fensterbank am Treppengiebel 4 mm hoeher, ihre Oberseite lag sonst in der Ebene der unteren
        Laibung und deckte sie auf 2 cm Tiefe (deckungsgleich, uv2_pruefung 12)."""
        t = alt_teile(Q)
        a0, a1, z0, _ = RD.fenster_tg()
        x0, x1, y0, y1 = a0 - 0.06 - 1e-4, a1 + 0.06 + 1e-4, RD.V - 0.02 - 1e-4, RD.V + 0.06 + 1e-4
        for teil in t[:1]:
            neu = []
            for pts, uvs, st, fa, gl in teil.flaechen:
                if all(x0 <= p.x <= x1 and y0 <= p.y <= y1 and z0 - 0.05 - 1e-4 <= p.z <= z0 + 1e-4 for p in pts):
                    pts = [p + Vector((0, 0, 0.004)) for p in pts]
                neu.append((pts, uvs, st, fa, gl))
            teil.flaechen = neu
        return t
    RD.teile = teile


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
        stufe = int(opt["haus"].rsplit("_s", 1)[1].split("_")[0])
        if stufe == 3 and opt.get("raum") == "bibliothek":
            dachbank_anheben(raeume_s23)
        raeume_s23.main()
